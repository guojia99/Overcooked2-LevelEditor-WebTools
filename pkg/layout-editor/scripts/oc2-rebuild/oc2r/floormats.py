"""地板/墙块用的内置网格需要工程内的真实材质。

策略（成本最低、保真度最高）：
  1. 工程里已有同名 .mat（commonW1/commonW2/common01/02 等）-> 直接复用，不新增文件；
  2. 否则从 AssetRipper 导出原样搬运 .mat + 其引用的贴图，
     只改写 guid（确定性 md5），shader 保持 Unity 内置 Standard。
"""

import hashlib
import os
import re
import shutil

from . import paths

MAT_DIR = "Assets/commonW1/materials/oc2rebuild"
TEX_DIR = "Assets/commonW1/textures/oc2rebuild"
BUILTIN_SHADER = "{fileID: 45, guid: 0000000000000000f000000000000000, type: 0}"
# Unity 内置 Default-Material
BUILTIN_DEFAULT_MATERIAL = "{fileID: 10303, guid: 0000000000000000f000000000000000, type: 0}"

_GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")
_TEXREF_RE = re.compile(r"\{fileID: (\d+), guid: ([0-9a-f]{32}), type: 3\}")
_SHADER_RE = re.compile(r"^  m_Shader: \{[^}]*\}", re.M)


def _guid(kind, key):
    return hashlib.md5(("oc2rebuild:%s:%s" % (kind, key)).encode("utf-8")).hexdigest()


def project_materials():
    """工程内已有的 .mat：小写名 -> (相对路径, guid)。"""
    out = {}
    for root, _dirs, files in os.walk(paths.ASSETS):
        for fn in files:
            if not fn.endswith(".mat"):
                continue
            p = os.path.join(root, fn)
            mp = p + ".meta"
            g = ""
            if os.path.exists(mp):
                try:
                    m = _GUID_RE.search(open(mp, encoding="utf-8", errors="replace").read(400))
                    g = m.group(1) if m else ""
                except OSError:
                    g = ""
            if g:
                out.setdefault(fn[:-4].lower(),
                               (os.path.relpath(p, paths.REPO).replace("\\", "/"), g))
    return out


class FloorMaterials(object):
    """源材质 guid（AssetRipper）-> 工程材质 guid。"""

    def __init__(self, resolver, dry_run=False):
        self.R = resolver
        self.dry_run = dry_run
        self.existing = project_materials()
        self.map = {}
        self._proj_names = None
        self.created_guids = set()
        self.created = []
        self.reused = []
        self.mismatched = []
        self.failed = []

    def ensure(self, src_guid):
        """返回可直接写进 m_Materials 的引用串；无法解析时返回空串。"""
        if src_guid in self.map:
            return self.map[src_guid]
        if src_guid.startswith("0000000000000000"):
            self.map[src_guid] = BUILTIN_DEFAULT_MATERIAL
            return self.map[src_guid]
        rel = self.R.ar_path(src_guid)
        if not rel or not rel.lower().endswith(".mat"):
            self.failed.append((src_guid, "反编译导出里不是材质"))
            self.map[src_guid] = ""
            return ""
        name = os.path.splitext(os.path.basename(rel))[0]
        hit = self.existing.get(name.lower())
        if hit and self._same_main_texture(rel, hit[0]):
            self.map[src_guid] = _ref(hit[1])
            self.reused.append((name, hit[0]))
            return self.map[src_guid]
        if hit:
            # 工程里同名材质的主贴图与原版对不上（多半是上一版提取时贴错了槽），
            # 不复用，另起一份带后缀的干净材质
            self.mismatched.append((name, hit[0]))
            name = name + "_oc2r"
        g = self._materialize(rel, name)
        self.map[src_guid] = _ref(g) if g else ""
        return self.map[src_guid]

    def _same_main_texture(self, ar_rel, proj_rel):
        a = _main_texture_name(os.path.join(paths.AR_ASSETS, ar_rel), self.R)
        b = _main_texture_name(os.path.join(paths.REPO, proj_rel), None,
                               self._project_guid_names())
        if a is None or b is None:
            return True     # 任一方拿不到信息就不判负
        return _norm_tex(a) == _norm_tex(b)

    def _project_guid_names(self):
        if self._proj_names is None:
            idx = {}
            for root, _dirs, files in os.walk(paths.ASSETS):
                for fn in files:
                    if not fn.endswith(".meta"):
                        continue
                    p = os.path.join(root, fn)
                    try:
                        head = open(p, encoding="utf-8", errors="replace").read(200)
                    except OSError:
                        continue
                    m = _GUID_RE.search(head)
                    if m:
                        idx[m.group(1)] = os.path.splitext(fn[:-5])[0]
            self._proj_names = idx
        return self._proj_names

    # ------------------------------------------------------------------
    def _materialize(self, rel, name):
        src = os.path.join(paths.AR_ASSETS, rel)
        if not os.path.exists(src):
            self.failed.append((rel, "文件缺失"))
            return ""
        txt = open(src, encoding="utf-8", errors="replace").read()

        # 贴图引用逐个搬运
        def repl(m):
            fid, tg = m.group(1), m.group(2)
            ng = self._copy_texture(tg)
            if not ng:
                return "{fileID: 0}"
            return "{fileID: %s, guid: %s, type: 3}" % (fid, ng)

        txt = _TEXREF_RE.sub(repl, txt)
        # 自定义 shader 换成内置 Standard（工程里没有游戏的 shader 资产）
        txt = _SHADER_RE.sub("  m_Shader: " + BUILTIN_SHADER, txt, count=1)

        g = _guid("mat", rel.lower())
        dest = "%s/%s.mat" % (MAT_DIR, _safe(name))
        self._write(dest, txt, g, "mat")
        self.created.append((name, dest))
        self.existing[name.lower()] = (dest, g)
        return g

    def _copy_texture(self, tex_guid):
        rel = self.R.ar_path(tex_guid)
        if not rel:
            return ""
        src = os.path.join(paths.AR_ASSETS, rel)
        if not os.path.exists(src):
            return ""
        ext = os.path.splitext(rel)[1] or ".png"
        name = os.path.splitext(os.path.basename(rel))[0]
        g = _guid("tex", rel.lower())
        self.created_guids.add(g)
        dest = "%s/%s%s" % (TEX_DIR, _safe(name), ext)
        full = os.path.join(paths.REPO, dest)
        if not self.dry_run and not os.path.exists(full):
            os.makedirs(os.path.dirname(full), exist_ok=True)
            _ensure_folder_metas(os.path.dirname(dest))
            shutil.copyfile(src, full)
            meta_src = src + ".meta"
            if os.path.exists(meta_src):
                mt = open(meta_src, encoding="utf-8", errors="replace").read()
                mt = _GUID_RE.sub("guid: " + g, mt, count=1)
                mt = re.sub(r"^  assetBundleName: .*$", "  assetBundleName: ", mt, flags=re.M)
            else:
                mt = "fileFormatVersion: 2\nguid: %s\nTextureImporter:\n" % g
            with open(full + ".meta", "w", encoding="utf-8") as f:
                f.write(mt)
        return g

    def _write(self, rel, text, guid, kind):
        self.created_guids.add(guid)
        if self.dry_run:
            return
        full = os.path.join(paths.REPO, rel)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        _ensure_folder_metas(os.path.dirname(rel))
        with open(full, "w", encoding="utf-8") as f:
            f.write(text)
        with open(full + ".meta", "w", encoding="utf-8") as f:
            f.write("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n"
                    "  externalObjects: {}\n  mainObjectFileID: 2100000\n"
                    "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid)


def _ref(guid):
    return "{fileID: 2100000, guid: %s, type: 2}" % guid


def _safe(n):
    return re.sub(r'[\\/:*?"<>|]+', "_", n).strip()


_MAINTEX_RE = re.compile(
    r"_MainTex:\s*\n\s*m_Texture: \{fileID: \d+, guid: ([0-9a-f]{32}), type: \d\}")


def _main_texture_name(mat_path, resolver, guid_names=None):
    """取材质 _MainTex 指向的贴图名；拿不到返回 None。"""
    if not os.path.exists(mat_path):
        return None
    try:
        txt = open(mat_path, encoding="utf-8", errors="replace").read()
    except OSError:
        return None
    m = _MAINTEX_RE.search(txt)
    if not m:
        return None
    g = m.group(1)
    if resolver is not None:
        rel = resolver.ar_path(g)
        return os.path.splitext(os.path.basename(rel))[0] if rel else None
    return (guid_names or {}).get(g)


def _norm_tex(n):
    """贴图名归一化：去掉 AssetRipper 的重名后缀与常见后缀差异。"""
    n = (n or "").lower()
    n = re.sub(r"\s+\d+$", "", n)
    n = re.sub(r"[_\- ]?(d|diff|diffuse|albedo|basecolor)$", "", n)
    return n


def _ensure_folder_metas(rel_dir):
    parts = rel_dir.split("/")
    for i in range(len(parts), 1, -1):
        d = "/".join(parts[:i])
        if not d.startswith("Assets/commonW1/"):
            break
        full = os.path.join(paths.REPO, d)
        meta = full + ".meta"
        if os.path.isdir(full) and not os.path.exists(meta):
            g = _guid("folder", d)
            with open(meta, "w", encoding="utf-8") as f:
                f.write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\n"
                        "DefaultImporter:\n  externalObjects: {}\n  userData: \n"
                        "  assetBundleName: commonW1\n  assetBundleVariant: \n" % g)
