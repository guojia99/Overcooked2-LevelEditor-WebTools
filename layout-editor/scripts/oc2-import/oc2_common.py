#!/usr/bin/env python3
"""
oc2_common.py — oc2_dlc_story 导入管线共享库。

提供：
  - AssetRipper 场景 YAML 轻量解析（文档拆分 / GameObject-Transform 树 / 字段提取）
  - 编辑器伪资产索引：
      * SO 索引：pseudo_prefab_so/**.asset 的 assetPath basename / prefabName / m_Name -> SO 信息
      * 目录索引：web/public/catalog/items.*.json 的 id -> 占位 prefab
      * 外观索引：counter-appearances.json 的 SO guid -> 占位类型（Counter/CounterCorner/...）
      * 占位 prefab 锚点：解析 .prefab 得 transform/GO/stub 组件 fileID 与默认 pseudoPrefabSO
  - 场景物件名规范化（去 " (N)" 后缀、大小写）
"""
import hashlib
import json
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
ASSETS = os.path.join(ROOT, "Assets")
WEB_PUBLIC = os.path.join(ROOT, "layout-editor", "web", "public")
RIPPER = os.path.join(os.path.dirname(ROOT), "AssetRipper_export_20260728_091744", "ExportedProject")

# ---------------------------------------------------------------------------
# YAML 场景解析（AssetRipper 2017 格式）
# ---------------------------------------------------------------------------

DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?\s*$", re.M)


class SceneDoc:
    __slots__ = ("cls", "fid", "stripped", "body")

    def __init__(self, cls, fid, stripped, body):
        self.cls = cls
        self.fid = fid
        self.stripped = stripped
        self.body = body

    def field_ref(self, name):
        m = re.search(re.escape(name) + r": \{fileID: (-?\d+)", self.body)
        return int(m.group(1)) if m else None

    def field_str(self, name):
        m = re.search(re.escape(name) + r": (.*)$", self.body, re.M)
        return m.group(1).strip() if m else None

    def field_vec3(self, name):
        m = re.search(
            re.escape(name) + r": \{x: ([-\d.eE+]+), y: ([-\d.eE+]+), z: ([-\d.eE+]+)\}",
            self.body)
        return tuple(float(v) for v in m.groups()) if m else None

    def field_quat(self, name):
        m = re.search(
            re.escape(name) + r": \{x: ([-\d.eE+]+), y: ([-\d.eE+]+), z: ([-\d.eE+]+), w: ([-\d.eE+]+)\}",
            self.body)
        return tuple(float(v) for v in m.groups()) if m else None


class Scene:
    """解析后的场景：docs + 层级树。"""

    def __init__(self, path):
        txt = open(path, "r", encoding="utf-8").read()
        self.docs = {}
        matches = list(DOC_RE.finditer(txt))
        for i, m in enumerate(matches):
            start = m.end()
            end = matches[i + 1].start() if i + 1 < len(matches) else len(txt)
            doc = SceneDoc(int(m.group(1)), int(m.group(2)), bool(m.group(3)), txt[start:end])
            self.docs[doc.fid] = doc
        self.gos = {f: d for f, d in self.docs.items() if d.cls == 1}
        self.trs = {f: d for f, d in self.docs.items() if d.cls == 4 and not d.stripped}
        self.go2tr = {}
        self.tr2go = {}
        self.children = {}
        self.father = {}
        for f, d in self.trs.items():
            g = d.field_ref("m_GameObject")
            self.go2tr[g] = f
            self.tr2go[f] = g
            ch = re.search(r"m_Children:\n((?:  - \{fileID: -?\d+\}\n)*)", d.body)
            self.children[f] = [int(x) for x in re.findall(r"fileID: (-?\d+)", ch.group(1))] if ch else []
            self.father[f] = d.field_ref("m_Father")
        self.roots = [f for f in self.trs if not self.father.get(f)]

    def go_name(self, go_fid):
        d = self.gos.get(go_fid)
        return d.field_str("m_Name") if d else None

    def tr_name(self, tr_fid):
        return self.go_name(self.tr2go.get(tr_fid, 0))

    def find_root(self, name):
        for r in self.roots:
            if self.tr_name(r) == name:
                return r
        return None

    def find_child(self, tr_fid, name):
        for c in self.children.get(tr_fid, []):
            if self.tr_name(c) == name:
                return c
        return None

    def components(self, go_fid):
        """GameObject 的组件 fileID 列表。"""
        d = self.gos.get(go_fid)
        if not d:
            return []
        return [int(x) for x in re.findall(r"component: \{fileID: (-?\d+)\}", d.body)]

    def monos(self, go_fid):
        return [self.docs[c] for c in self.components(go_fid)
                if c in self.docs and self.docs[c].cls == 114]

    def transform(self, go_fid):
        f = self.go2tr.get(go_fid)
        return self.docs.get(f) if f else None


def base_name(name):
    """'countertop_01 (4)' -> 'countertop_01'；'DispenserCrate 3' -> 'DispenserCrate'。"""
    if name is None:
        return None
    n = re.sub(r"\s*\(\d+\)\s*$", "", name).strip()
    n = re.sub(r"\s+\d+$", "", n).strip()
    return n


# 源场景名 -> 编辑器资产基名 的别名表（主题变体缺 SO 时降级到最接近项）
NAME_ALIASES = {
    # dlc02 沙滩
    "m_dlc2_spade_01": "spade_01",
    "m_dlc2_bucket_01": "bucket_01",
    "m_dlc2_aboard_01": "aboard_01",
    "dispensercrate": "Dispenser",
    "lightbluecar": "exterior_car_blue_02",
    "yellowcar": "exterior_car_yellow_01",
    "darkbluecar": "exterior_car_blue_02",
    "m_map_wizard_rock_03": "map_rock_03_1",
    # dlc09 festivemashup：DLC09_ 前缀的混搭物件复用各主题资产
    "dlc09_countertop_01_standard_circus": "dlc08_countertop_02_standard_circus",
    "dlc09_countertop_01_standard_circus_corner": "dlc08_countertop_02_standard_circus_corner",
    "dlc09_countertop_01_chopping_circus": "dlc08_countertop_01_chopping_circus",
    "dlc09_countertop_02_standard_camping": "countertop_02_standard_camping",
    "dlc09_countertop_02_standard_camping_corner": "countertop_02_standard_camping_corner",
    "dlc09_countertop_02_chopping_board_camping_no_edge": "countertop_02_chopping_board_camping_no_edge",
    "dlc09_countertop_03_standard_medieval": "countertop_03_standard_medieval",
    "dlc09_countertop_03_chopping_medieval": "countertop_03_chopping_medieval",
    "dlc09_countertop_choppingboard_01": "countertop_choppingboard_01",
    "dlc09_countertop_corner_01": "countertop_corner_01",
    "dlc09_countertop_01": "countertop_01",
    "dlc09_dispenser_crate_circus": "dlc08_dispenser_crate_circus",
    "dlc09_dispenser_crate_camping_new": "dlc13_dispenser_crate_camping_new",
    "dlc09_dispenser_crate_medieval": "Dispenser",
    "dlc09_workstation_01_sink_circus": "dlc08_workstation_01_sink_circus",
    "dlc09_workstation_mixer_circus": "dlc08_workstation_mixer",
    "dlc09_workstation_mixer_camping": "workstation_mixer_01",
    "dlc09_workstation_mixer_04": "workstation_mixer_01",
    "dlc09_workstation_sink_01_wood": "dlc13_workstation_sink_01_wood",
    "dlc09_workstation_sink_02_camping": "workstation_sink_01_camping",
    "dlc09_backpack": "backpack",
    "dlc09_stairs_campsite": "p_dlc09_stairs",
    "dlc09_gate": "gate",
}
for _i in range(1, 7):
    NAME_ALIASES[f"ropes0{_i}"] = "barrier_rope_1unit_01" if _i % 2 else "barrier_rope_2unit_01"

# 直接忽略的节点：prefab 展开的内部部件、地面（由生成器单独处理）、灯光/探针等
IGNORE_NAMES = {
    "sand", "worktop", "attachpoint", "ground", "killplane",
    "point light", "spotlight", "area light", "directional light",
    "reflection probe", "light probes", "lp anchor", "lp anchor scene",
    "mesh baker", "mesh bakers", "combined floor tile", "plane",
}
IGNORE_RE = re.compile(
    r"^(group_\d+|handle|pivot|block|tableblock|table|wall|transform\d+|group)$", re.I)


def map_item(scene, idx, tr, prefer_bundles=None):
    """
    对场景子树节点分类：
      ('place', baseName, resolve结果)
      ('group', baseName)        未命中但有子节点 -> 调用方递归
      ('skip', baseName)         忽略名单
      ('miss', baseName)
    """
    go = scene.tr2go.get(tr)
    nm = scene.go_name(go)
    b = base_name(nm)
    if b is None:
        return ("skip", nm)
    bl = b.lower()
    if bl in IGNORE_NAMES or IGNORE_RE.match(bl):
        return ("skip", b)
    r = idx.resolve(b, prefer_bundles)
    if r:
        return ("place", b, r)
    if scene.children.get(tr):
        return ("group", b)
    return ("miss", b)


# ---------------------------------------------------------------------------
# 编辑器资产索引
# ---------------------------------------------------------------------------

_GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})", re.M)


def meta_guid(asset_path):
    meta = asset_path + ".meta"
    if not os.path.exists(meta):
        return None
    m = _GUID_RE.search(open(meta, "r", encoding="utf-8").read())
    return m.group(1) if m else None


class EditorIndex:
    """common01/02/03 伪资产统一索引。"""

    def __init__(self):
        self.so_by_guid = {}          # guid -> so info
        self.so_by_base = {}          # (assetPath basename lower) -> [so info]
        self.so_by_prefabname = {}    # prefabName lower -> [so info]
        self.so_by_name = {}          # m_Name lower -> so info
        self.catalog = {}             # id -> catalog item
        self.appearance_type = {}     # so guid -> placeholder type (Counter/...)
        self.placeholder = {}         # prefab assetPath -> {tr, go, stub, pseudo, so_guid}
        self._scan_sos()
        self._scan_catalog()
        self._scan_appearances()

    # ---- pseudo SO 扫描 ----
    SO_ROOTS = (
        ("common01", "pseudo_prefab_so"), ("common02", "pseudo_prefab_so"),
        ("common03", "pseudo_prefab_so"),
        ("common01", "food", "Recipes"), ("common02", "food", "Recipes"),
        ("common03", "Recipes"),
    )

    def _scan_sos(self):
        for root in self.SO_ROOTS:
            base = os.path.join(ASSETS, root[0], *root[1:])
            if not os.path.isdir(base):
                continue
            for dirpath, _dirs, files in os.walk(base):
                for n in files:
                    if not n.endswith(".asset"):
                        continue
                    p = os.path.join(dirpath, n)
                    txt = open(p, "r", encoding="utf-8").read()
                    g = meta_guid(p)
                    if not g:
                        continue
                    name = re.search(r"m_Name: (.*)$", txt, re.M)
                    prefab_name = re.search(r"prefabName: (.*)$", txt, re.M)
                    bundle = re.search(r"bundleName: (.*)$", txt, re.M)
                    apath = re.search(r"assetPath: (.*)$", txt, re.M)
                    info = {
                        "guid": g,
                        "name": name.group(1).strip() if name else "",
                        "prefabName": prefab_name.group(1).strip() if prefab_name else "",
                        "bundleName": bundle.group(1).strip() if bundle else "",
                        # assetPath 可能是 Windows 反斜杠且大小写混杂
                        "assetPath": (apath.group(1).strip().replace("\\", "/") if apath else ""),
                        "soAssetPath": os.path.relpath(p, ROOT),
                    }
                    self.so_by_guid[g] = info
                    base_name_l = os.path.basename(info["assetPath"]).lower()
                    if base_name_l:
                        self.so_by_base.setdefault(base_name_l, []).append(info)
                    if info["prefabName"]:
                        self.so_by_prefabname.setdefault(info["prefabName"].lower(), []).append(info)
                    if info["name"]:
                        self.so_by_name[info["name"].lower()] = info

    # ---- 布局目录 ----
    def _scan_catalog(self):
        for f in sorted(os.listdir(os.path.join(WEB_PUBLIC, "catalog"))):
            if not f.startswith("items.") or not f.endswith(".json"):
                continue
            d = json.load(open(os.path.join(WEB_PUBLIC, "catalog", f), "r", encoding="utf-8"))
            for it in (d["items"] if isinstance(d, dict) else d):
                self.catalog[it["id"]] = it

    # ---- 桌台外观 ----
    def _scan_appearances(self):
        p = os.path.join(WEB_PUBLIC, "counter-appearances.json")
        d = json.load(open(p, "r", encoding="utf-8"))
        for t, lst in d["byType"].items():
            for x in lst:
                self.appearance_type[x["guid"]] = t

    # ---- 占位 prefab 锚点（懒解析）----
    def placeholder_anchors(self, asset_path):
        if asset_path in self.placeholder:
            return self.placeholder[asset_path]
        p = os.path.join(ROOT, asset_path)
        txt = open(p, "r", encoding="utf-8").read()
        docs = {}
        matches = list(DOC_RE.finditer(txt))
        for i, m in enumerate(matches):
            start = m.end()
            end = matches[i + 1].start() if i + 1 < len(matches) else len(txt)
            docs[int(m.group(2))] = (int(m.group(1)), txt[start:end])
        tr = go = stub = None
        so_guid = None
        for fid, (cls, body) in docs.items():
            if cls == 4:
                m = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
                if m:
                    tr, go = fid, int(m.group(1))
            if cls == 114 and "pseudoPrefabSO" in body:
                stub = fid
                m = re.search(r"pseudoPrefabSO: \{fileID: \d+, guid: ([0-9a-f]{32})", body)
                if m:
                    so_guid = m.group(1)
        info = {"tr": tr, "go": go, "stub": stub, "soGuid": so_guid,
                "guid": meta_guid(p)}
        self.placeholder[asset_path] = info
        return info

    # ---- 名称 -> 放置方案 ----
    def resolve(self, scene_base_name, prefer_bundles=None):
        """
        源场景物件基名（小写）-> 放置方案 dict:
          { kind: 'appearance', placeholder: <prefab path>, soGuid, soName, type }
          { kind: 'prop', placeholder: <prefab path>, soGuid, soName }
        未命中返回 None。
        """
        lname = scene_base_name.lower()
        cands = []
        # 1) assetPath basename 精确（X.prefab）
        cands += self.so_by_base.get(lname + ".prefab", [])
        # 2) prefabName 精确
        cands += self.so_by_prefabname.get(lname, [])
        # 3) 别名降级
        if not cands and lname in NAME_ALIASES:
            a = NAME_ALIASES[lname].lower()
            cands += self.so_by_base.get(a + ".prefab", [])
            cands += self.so_by_prefabname.get(a, [])
            # 别名也可命中目录核心项（如 Dispenser）
            if not cands and NAME_ALIASES[lname] in self.catalog:
                item = self.catalog[NAME_ALIASES[lname]]
                return {"kind": "prop", "placeholder": item["assetPath"],
                        "soGuid": None, "soName": NAME_ALIASES[lname], "alias": True}
        # 去重
        seen = set()
        uniq = []
        for c in cands:
            if c["guid"] not in seen:
                seen.add(c["guid"])
                uniq.append(c)
        if not uniq:
            return None
        # bundle 偏好排序
        if prefer_bundles:
            uniq.sort(key=lambda c: 0 if c["bundleName"] in prefer_bundles else 1)
        # 逐个候选尝试，直到找到可放置方案
        for so in uniq:
            # 外观 SO -> 对应占位类型
            if so["guid"] in self.appearance_type:
                t = self.appearance_type[so["guid"]]
                item = self.catalog.get(t)
                if item:
                    return {"kind": "appearance", "placeholder": item["assetPath"],
                            "soGuid": so["guid"], "soName": so["name"], "type": t}
            # 普通/装饰 SO -> 找引用该 SO 的占位 prefab
            ph = self._placeholder_for_so(so["guid"])
            if ph:
                return {"kind": "prop", "placeholder": ph, "soGuid": so["guid"], "soName": so["name"]}
        return None

    def _placeholder_for_so(self, so_guid):
        if not hasattr(self, "_so2placeholder"):
            self._so2placeholder = {}
            for it in self.catalog.values():
                ap = it.get("assetPath")
                if not ap:
                    continue
                try:
                    anchors = self.placeholder_anchors(ap)
                except Exception:
                    continue
                if anchors.get("soGuid"):
                    self._so2placeholder[anchors["soGuid"]] = ap
        return self._so2placeholder.get(so_guid)


def deterministic_guid(prefix, key):
    return hashlib.md5(f"{prefix}:{key}".encode("utf-8")).hexdigest()
