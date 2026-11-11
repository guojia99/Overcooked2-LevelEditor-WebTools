#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""补 commonW6 食材箱皮肤的 3D 预览（exports/counter-skins）。

Unity 菜单「导出桌台皮肤 3D 模型与俯视图」会覆盖全量。编辑器没开时，本脚本只补
后面 6 款：从 StreamingAssets bundle 抽出网格/贴图，轴与绕序对齐
LayoutEditorCounterSkin3dExport（X 取反、面序倒排）。

圣诞 / 部落与已导出的 Dispenser_04 / Dispenser_05 是同一只 prefab，直接复用文件。
"""
import json
import os
import re
import shutil

import numpy as np
from PIL import Image

import UnityPy
from UnityPy.environment import Environment

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
WINDOWS = os.path.join(ROOT, "Assets", "StreamingAssets", "Windows")
OUT = os.path.join(ROOT, "exports", "counter-skins")
MANIFEST = os.path.join(OUT, "manifest.json")

# 实例化时根位移/旋转被替换，根缩放保留（与 Unity Instantiate(pos, rot) 一致）。
TARGETS = [
    ("Dispenser_Firewood", "bundle250",
     "assets/downloadablecontent/dlc05/dlc_assets/prefabs/shared kitchen/dispenser_crate_firewood.prefab"),
    ("Dispenser_Circus", "bundle359",
     "assets/downloadablecontent/dlc08/dlc_assets/prefabs/shared kitchen/dlc08_dispenser_crate_circus.prefab"),
    ("Dispenser_Winter", "bundle405",
     "assets/downloadablecontent/dlc09/dlc_assets/prefabs/sharedkitchen/dlc09_dispenser_crate_winter.prefab"),
    ("Dispenser_Wood13", "bundle449",
     "assets/downloadablecontent/dlc13/assets/prefabs/sharedkitchen/dlc13_dispenser_crate_camping_new.prefab"),
]
ALIASES = [
    ("Dispenser_Christmas", "Dispenser_04"),
    ("Dispenser_Medieval", "Dispenser_05"),
]

TOP_SIZE = 256
MAX_TEX = 1024
PAD = 1.04
CAB_RE = re.compile(r"cab-([0-9a-f]{32})", re.I)


def fmt(n):
    if n != n or n in (float("inf"), float("-inf")):
        return "0"
    s = ("%.6f" % n).rstrip("0").rstrip(".")
    return s if s not in ("", "-0") else "0"


def mul(a, b):
    out = [[0.0] * 4 for _ in range(4)]
    for i in range(4):
        for j in range(4):
            out[i][j] = a[i][0] * b[0][j] + a[i][1] * b[1][j] + a[i][2] * b[2][j] + a[i][3] * b[3][j]
    return out


def trs(pos, rot, scale):
    x, y, z, w = rot
    xx, yy, zz = x * x, y * y, z * z
    xy, xz, yz = x * y, x * z, y * z
    wx, wy, wz = w * x, w * y, w * z
    r00, r01, r02 = 1 - 2 * (yy + zz), 2 * (xy - wz), 2 * (xz + wy)
    r10, r11, r12 = 2 * (xy + wz), 1 - 2 * (xx + zz), 2 * (yz - wx)
    r20, r21, r22 = 2 * (xz - wy), 2 * (yz + wx), 1 - 2 * (xx + yy)
    sx, sy, sz = scale
    return [
        [r00 * sx, r01 * sy, r02 * sz, pos[0]],
        [r10 * sx, r11 * sy, r12 * sz, pos[1]],
        [r20 * sx, r21 * sy, r22 * sz, pos[2]],
        [0.0, 0.0, 0.0, 1.0],
    ]


def apply_point(m, v):
    x, y, z = v
    return (
        m[0][0] * x + m[0][1] * y + m[0][2] * z + m[0][3],
        m[1][0] * x + m[1][1] * y + m[1][2] * z + m[1][3],
        m[2][0] * x + m[2][1] * y + m[2][2] * z + m[2][3],
    )


def vec3(v):
    return (float(v.x), float(v.y), float(v.z))


def quat(q):
    return (float(q.x), float(q.y), float(q.z), float(q.w))


class BundleEnv(object):
    def __init__(self):
        self.env = Environment()
        self.loaded = set()
        self._cab = None

    def cab_index(self):
        if self._cab is not None:
            return self._cab
        idx = {}
        rx = re.compile(br"CAB-([0-9a-fA-F]{32})")
        for name in os.listdir(WINDOWS):
            path = os.path.join(WINDOWS, name)
            if not os.path.isfile(path):
                continue
            with open(path, "rb") as f:
                head = f.read(16384)
            m = rx.search(head)
            if m:
                idx["cab-" + m.group(1).decode().lower()] = path
        self._cab = idx
        return idx

    def load_bundle(self, name_or_path):
        path = name_or_path
        if not os.path.isabs(path):
            path = os.path.join(WINDOWS, name_or_path)
        if path in self.loaded:
            return
        print("  load", os.path.basename(path))
        self.env.load_file(path)
        self.loaded.add(path)

    def ensure_pptr(self, pptr):
        if pptr is None or getattr(pptr, "m_PathID", 0) in (0, None):
            return None
        try:
            return pptr.read()
        except FileNotFoundError:
            self._load_pptr_file(pptr)
            return pptr.read()

    def _load_pptr_file(self, pptr):
        fid = int(getattr(pptr, "m_FileID", 0) or 0)
        if fid <= 0:
            raise FileNotFoundError("local pptr missing")
        ext = pptr.assetsfile.externals[fid - 1]
        m = CAB_RE.search(str(ext))
        if not m:
            raise FileNotFoundError("no cab in " + str(ext))
        cab = "cab-" + m.group(1).lower()
        path = self.cab_index().get(cab)
        if not path:
            raise FileNotFoundError("cab not in Windows: " + cab)
        self.load_bundle(path)


def component_of(go, name):
    for c in go.m_Component:
        comp = c.component.read()
        if type(comp).__name__ == name:
            return comp
    return None


def parse_exported_mesh(text):
    """UnityPy Mesh.export() → 共享顶点 + 子网格面。"""
    verts = []
    uvs = []
    groups = []
    current = None
    for line in text.splitlines():
        if line.startswith("v "):
            p = line.split()
            verts.append((float(p[1]), float(p[2]), float(p[3])))
        elif line.startswith("vt "):
            p = line.split()
            uvs.append((float(p[1]), float(p[2])))
        elif line.startswith("g "):
            current = {"name": line[2:].strip() or "mesh", "faces": []}
            groups.append(current)
        elif line.startswith("f "):
            if current is None:
                current = {"name": "mesh", "faces": []}
                groups.append(current)
            face = []
            for tok in line.split()[1:]:
                bits = tok.split("/")
                vi = int(bits[0]) - 1
                ti = int(bits[1]) - 1 if len(bits) > 1 and bits[1] else vi
                face.append((vi, ti))
            if len(face) >= 3:
                current["faces"].append(face[:3])
    if not groups:
        groups = [{"name": "mesh", "faces": []}]
    return verts, uvs, groups


def tint_of(mat):
    saved = getattr(mat, "m_SavedProperties", None)
    colors = getattr(saved, "m_Colors", None) if saved is not None else None
    if isinstance(colors, dict):
        for key in ("_Color", "_TintColor"):
            c = colors.get(key)
            if c is not None:
                return (float(c.r), float(c.g), float(c.b), float(getattr(c, "a", 1)))
    return (1.0, 1.0, 1.0, 1.0)


def iter_texenvs(texenvs):
    if isinstance(texenvs, dict):
        return list(texenvs.items())
    out = []
    if isinstance(texenvs, list):
        for item in texenvs:
            if isinstance(item, tuple) and len(item) == 2:
                out.append(item)
    return out


def diffuse_pptr(mat):
    saved = getattr(mat, "m_SavedProperties", None)
    pairs = iter_texenvs(getattr(saved, "m_TexEnvs", None) if saved is not None else None)
    if not pairs:
        return None
    order = ["_DiffuseMap", "_Diffuse", "_MainTex", "_Albedo", "_BaseMap", "_BaseColorMap"]
    by_name = {str(k): slot for k, slot in pairs}

    def tex_of(slot):
        tex = getattr(slot, "m_Texture", None)
        if tex is not None and getattr(tex, "m_PathID", 0):
            return tex
        return None

    for key in order:
        found = tex_of(by_name.get(key))
        if found is not None:
            return found
    for key, slot in pairs:
        low = str(key).lower()
        if any(s in low for s in ("normal", "spec", "mask", "emiss", "lightmap", "rmeao")):
            continue
        if "alpha" in low and "colour" not in low and "color" not in low:
            continue
        found = tex_of(slot)
        if found is not None:
            return found
    return None


def texture_image(env, pptr):
    tex = env.ensure_pptr(pptr)
    if tex is None or not hasattr(tex, "image"):
        return None
    img = tex.image
    if img is None:
        return None
    img = img.convert("RGBA")
    # 与 ForceVisibleAlpha 同：RGB 有色但 alpha 全 0 时补成不透明。
    arr = np.asarray(img)
    if arr[..., 3].mean() < 16 and arr[..., :3].sum(axis=2).mean() > 24:
        arr = arr.copy()
        arr[..., 3] = 255
        img = Image.fromarray(arr, "RGBA")
    w, h = img.size
    scale = MAX_TEX / float(max(w, h))
    if scale < 1:
        img = img.resize((max(1, int(w * scale)), max(1, int(h * scale))), Image.Resampling.LANCZOS)
    return img


def collect_renderers(env, root_tr):
    found = []

    def walk(tr, parent, is_root):
        go = tr.m_GameObject.read()
        if not is_root and hasattr(go, "m_IsActive") and not go.m_IsActive:
            return
        pos = (0.0, 0.0, 0.0) if is_root else vec3(tr.m_LocalPosition)
        rot = (0.0, 0.0, 0.0, 1.0) if is_root else quat(tr.m_LocalRotation)
        # 静态箱盖在资源里是张开的参考姿态。Animator 默认 Idle 会把它扣上，
        # Unity 导出看到的是扣上之后的样子：盖子本地旋转等价于绕 Y 转 180°。
        if (not is_root) and go.m_Name.endswith("_Lid") and abs(rot[0]) < 1e-3 and abs(rot[1]) < 1e-3 and abs(rot[2]) < 1e-3 and abs(abs(rot[3]) - 1.0) < 1e-2:
            rot = (0.0, 1.0, 0.0, 0.0)
        world = mul(parent, trs(pos, rot, vec3(tr.m_LocalScale)))
        smr = component_of(go, "SkinnedMeshRenderer")
        mr = component_of(go, "MeshRenderer")
        mf = component_of(go, "MeshFilter")
        rend = smr or mr
        mesh_pptr = None
        if smr is not None and getattr(smr, "m_Enabled", True):
            mesh_pptr = smr.m_Mesh
        elif mr is not None and mf is not None and getattr(mr, "m_Enabled", True):
            mesh_pptr = mf.m_Mesh
        if rend is not None and mesh_pptr is not None and getattr(mesh_pptr, "m_PathID", 0):
            found.append((go.m_Name, world, rend, mesh_pptr))
        for ch in tr.m_Children or []:
            walk(ch.read(), world, False)

    ident = [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]
    walk(root_tr, ident, True)
    return found


def raster_top(tris, images, tints, bounds):
    minx, miny, minz, maxx, maxy, maxz = bounds
    ext_x = (maxx - minx) * 0.5 * PAD
    ext_z = (maxz - minz) * 0.5 * PAD
    max_ext = max(ext_x, ext_z, 1e-4)
    w = max(8, int(round(TOP_SIZE * ext_x / max_ext)))
    h = max(8, int(round(TOP_SIZE * ext_z / max_ext)))
    cx = (minx + maxx) * 0.5
    cz = (minz + maxz) * 0.5
    # 画面上 = 世界 -Z：PNG 顶行对应较小的 Z。画面右 = 世界 +X。
    img = np.zeros((h, w, 4), dtype=np.uint8)
    depth = np.full((h, w), -1e9, dtype=np.float32)

    def to_px(x, z):
        px = (x - (cx - ext_x)) / (2 * ext_x) * (w - 1)
        py = (z - (cz - ext_z)) / (2 * ext_z) * (h - 1)
        return px, py

    order = sorted(range(len(tris)), key=lambda i: tris[i][0][1] + tris[i][1][1] + tris[i][2][1])
    for ti in order:
        a, b, c, uv_a, uv_b, uv_c, mi = tris[ti]
        pts = [to_px(a[0], a[2]), to_px(b[0], b[2]), to_px(c[0], c[2])]
        ys = [p[1] for p in pts]
        xs = [p[0] for p in pts]
        x0 = max(0, int(min(xs)))
        x1 = min(w - 1, int(max(xs)) + 1)
        y0 = max(0, int(min(ys)))
        y1 = min(h - 1, int(max(ys)) + 1)
        if x1 < x0 or y1 < y0:
            continue
        tex = images[mi]
        tint = tints[mi]
        arr = np.asarray(tex) if tex is not None else None
        for py in range(y0, y1 + 1):
            for px in range(x0, x1 + 1):
                # barycentric
                x, y = px + 0.5, py + 0.5
                den = (pts[1][1] - pts[2][1]) * (pts[0][0] - pts[2][0]) + (pts[2][0] - pts[1][0]) * (pts[0][1] - pts[2][1])
                if abs(den) < 1e-8:
                    continue
                wa = ((pts[1][1] - pts[2][1]) * (x - pts[2][0]) + (pts[2][0] - pts[1][0]) * (y - pts[2][1])) / den
                wb = ((pts[2][1] - pts[0][1]) * (x - pts[2][0]) + (pts[0][0] - pts[2][0]) * (y - pts[2][1])) / den
                wc = 1 - wa - wb
                if wa < -0.001 or wb < -0.001 or wc < -0.001:
                    continue
                z = wa * a[1] + wb * b[1] + wc * c[1]
                if z < depth[py, px]:
                    continue
                depth[py, px] = z
                if arr is None:
                    col = [int(tint[0] * 255), int(tint[1] * 255), int(tint[2] * 255), 255]
                else:
                    u = wa * uv_a[0] + wb * uv_b[0] + wc * uv_c[0]
                    v = wa * uv_a[1] + wb * uv_b[1] + wc * uv_c[1]
                    th, tw = arr.shape[0], arr.shape[1]
                    tx = int(min(tw - 1, max(0, u * tw)))
                    ty = int(min(th - 1, max(0, (1 - v) * th)))
                    sample = arr[ty, tx]
                    col = [
                        int(min(255, sample[0] * tint[0])),
                        int(min(255, sample[1] * tint[1])),
                        int(min(255, sample[2] * tint[2])),
                        int(sample[3]),
                    ]
                img[py, px] = col
    # PNG 顶行 = 世界 -Z，当前 py 增大是 +Z，所以上下翻转。
    img = np.flipud(img)
    return Image.fromarray(img, "RGBA")


def export_one(env, file_key, bundle, asset_path):
    print(file_key)
    env.load_bundle(bundle)
    prefab = None
    want = asset_path.lower()
    for path, obj in env.env.container.items():
        if path.lower() == want:
            prefab = obj
            break
    if prefab is None:
        raise RuntimeError("prefab 不在 bundle 里: " + asset_path)
    go = prefab.read()
    root = component_of(go, "Transform")
    renderers = collect_renderers(env, root)
    if not renderers:
        raise RuntimeError("没有可渲染网格")

    obj_lines = ["# OC2 counter skin " + file_key, "mtllib " + file_key + ".mtl"]
    mtl_lines = ["# " + file_key]
    mat_files = []
    mat_images = []
    mat_tints = []
    tex_index = {}
    v_base = 0
    all_pts = []
    top_tris = []

    for name, world, rend, mesh_pptr in renderers:
        mesh = env.ensure_pptr(mesh_pptr)
        verts, uvs, groups = parse_exported_mesh(mesh.export())
        if not verts:
            continue
        world_verts = [apply_point(world, v) for v in verts]
        all_pts.extend(world_verts)
        for x, y, z in world_verts:
            obj_lines.append("v %s %s %s" % (fmt(-x), fmt(y), fmt(z)))
        has_uv = len(uvs) > 0
        if has_uv:
            for i in range(len(world_verts)):
                uv = uvs[i] if i < len(uvs) else (0.0, 0.0)
                obj_lines.append("vt %s %s" % (fmt(uv[0]), fmt(uv[1])))
        materials = list(getattr(rend, "m_Materials", []) or [])
        # UnityPy 的 g 分组前面常有一个空组，真正的子网格从 _0 开始。
        face_groups = [g for g in groups if g["faces"]]
        for si, group in enumerate(face_groups):
            mat_pptr = materials[min(si, len(materials) - 1)] if materials else None
            mat_obj = env.ensure_pptr(mat_pptr) if mat_pptr is not None else None
            # 用 PPtr 身份去重，避免两个不同材质被当成同一个 Python 对象。
            key = (
                getattr(mat_pptr, "m_FileID", 0),
                getattr(mat_pptr, "m_PathID", 0),
                id(getattr(mat_pptr, "assetsfile", None)),
            ) if mat_pptr is not None else ("none", si, name)
            if key in tex_index:
                mat_idx = tex_index[key]
            else:
                mat_idx = len(mat_files)
                tex_index[key] = mat_idx
                tint = tint_of(mat_obj) if mat_obj is not None else (0.75, 0.75, 0.75, 1.0)
                img = None
                if mat_obj is not None:
                    pptr = diffuse_pptr(mat_obj)
                    if pptr is not None:
                        try:
                            img = texture_image(env, pptr)
                        except Exception as ex:
                            print("  贴图失败", name, ex)
                mat_name = "%s_m%d.png" % (file_key, mat_idx)
                if img is not None:
                    img.save(os.path.join(OUT, mat_name))
                    mat_files.append(mat_name)
                else:
                    mat_files.append("")
                mat_images.append(img)
                mat_tints.append(tint)
                mtl_lines.append("newmtl %s_m%d" % (file_key, mat_idx))
                mtl_lines.append("Kd %s %s %s" % (fmt(tint[0]), fmt(tint[1]), fmt(tint[2])))
                if mat_files[mat_idx]:
                    mtl_lines.append("map_Kd " + mat_files[mat_idx])
                mtl_lines.append("")
            obj_lines.append("g %s_%d" % (name, si))
            obj_lines.append("usemtl %s_m%d" % (file_key, mat_idx))
            for face in group["faces"]:
                ids = []
                uvs_f = []
                pts = []
                for vi, ti in face:
                    idx = vi + 1 + v_base
                    ids.append(idx)
                    if has_uv:
                        uv = uvs[ti] if 0 <= ti < len(uvs) else (0.0, 0.0)
                        uvs_f.append(uv)
                    pts.append(world_verts[vi])
                # X 镜像后绕序反转
                ids = list(reversed(ids))
                if has_uv:
                    uvs_f = list(reversed(uvs_f))
                    obj_lines.append("f %d/%d %d/%d %d/%d" % (ids[0], ids[0], ids[1], ids[1], ids[2], ids[2]))
                    top_tris.append((pts[2], pts[1], pts[0], uvs_f[0], uvs_f[1], uvs_f[2], mat_idx))
                else:
                    obj_lines.append("f %d %d %d" % (ids[0], ids[1], ids[2]))
        v_base += len(world_verts)

    if not all_pts:
        raise RuntimeError("网格顶点为空")
    xs = [p[0] for p in all_pts]
    ys = [p[1] for p in all_pts]
    zs = [p[2] for p in all_pts]
    bounds = (min(xs), min(ys), min(zs), max(xs), max(ys), max(zs))
    top = raster_top(top_tris, mat_images, mat_tints, bounds)
    top.save(os.path.join(OUT, file_key + "_top.png"))
    with open(os.path.join(OUT, file_key + ".obj"), "w", encoding="utf-8") as f:
        f.write("\n".join(obj_lines) + "\n")
    with open(os.path.join(OUT, file_key + ".mtl"), "w", encoding="utf-8") as f:
        f.write("\n".join(mtl_lines) + "\n")
    size = {
        "x": round(bounds[3] - bounds[0], 6),
        "y": round(bounds[4] - bounds[1], 6),
        "z": round(bounds[5] - bounds[2], 6),
    }
    entry = {
        "top": file_key + "_top.png",
        "obj": file_key + ".obj",
        "mtl": file_key + ".mtl",
        "mats": [{"tex": name} for name in mat_files if name],
        "size": size,
        "minY": round(bounds[1], 6),
    }
    print("  size", size, "minY", entry["minY"], "mats", len(entry["mats"]), "tris", len(top_tris))
    return entry


def alias_one(file_key, source_key):
    print("alias", file_key, "<-", source_key)
    for ext in (".obj", ".mtl", "_top.png"):
        src = os.path.join(OUT, source_key + ext)
        dst = os.path.join(OUT, file_key + ext)
        if ext == ".obj" or ext == ".mtl":
            text = open(src, encoding="utf-8").read().replace(source_key, file_key)
            open(dst, "w", encoding="utf-8").write(text)
        else:
            shutil.copyfile(src, dst)
    # 材质贴图：清单里有几张就拷几张，文件名按序号。
    manifest = json.load(open(MANIFEST, encoding="utf-8"))
    src_entry = manifest["items"][source_key]
    mats = []
    for i, mat in enumerate(src_entry["mats"]):
        src_name = mat["tex"]
        dst_name = "%s_m%d.png" % (file_key, i)
        shutil.copyfile(os.path.join(OUT, src_name), os.path.join(OUT, dst_name))
        mats.append({"tex": dst_name})
    # obj/mtl 里的贴图名也换成新文件名
    for ext in (".obj", ".mtl"):
        path = os.path.join(OUT, file_key + ext)
        text = open(path, encoding="utf-8").read()
        for i, mat in enumerate(src_entry["mats"]):
            text = text.replace(mat["tex"], "%s_m%d.png" % (file_key, i))
        open(path, "w", encoding="utf-8").write(text)
    return {
        "top": file_key + "_top.png",
        "obj": file_key + ".obj",
        "mtl": file_key + ".mtl",
        "mats": mats,
        "size": src_entry["size"],
        "minY": src_entry["minY"],
    }


def main():
    os.makedirs(OUT, exist_ok=True)
    manifest = json.load(open(MANIFEST, encoding="utf-8"))
    env = BundleEnv()
    for file_key, bundle, path in TARGETS:
        manifest["items"][file_key] = export_one(env, file_key, bundle, path)
        if "failures" in manifest and file_key in manifest["failures"]:
            del manifest["failures"][file_key]
    for file_key, source in ALIASES:
        manifest["items"][file_key] = alias_one(file_key, source)
    with open(MANIFEST, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print("manifest", len(manifest["items"]))


if __name__ == "__main__":
    main()
