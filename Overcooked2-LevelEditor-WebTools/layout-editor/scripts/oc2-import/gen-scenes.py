#!/usr/bin/env python3
"""
gen-scenes.py — 由 AssetRipper 源场景生成编辑器 .unity 场景（oc2_dlc_story）。

骨架 = Assets/Template/s_template.unity 的固定 fileID 结构（直接以常量复刻），
内容 = 源场景逐项映射：
  - RenderSettings 环境光/雾
  - CampaignGameEnvironment 实例 mods：GridManager 位置 + m_gridHalfSize.X/Z、
    KillPlane 位置/缩放/m_respawnType
  - Design/Collision：Col_Floor（源 Ground 碰撞盒）+ Col_Wall*（源 Wall* 碰撞盒）
  - Design/KillPlanes：源 Design/KillPlanes + Collision/KillPlane* -> RespawnCollider
  - Design/Counters：源 Work Surfaces / Cooking Stations / Gameplay Stations
  - Design/Utensils：源 Utensils / Glasses / Plates / Mugs / Trays
  - Chefs：4 个 Player prefab 实例（源位置/朝向）
  - Art/Lights：平行光（源 Directional light 旋转/颜色/强度）
  - Art/Ground/Floor：按网格缩放的地砖 quad
  - Art/Scenery：装饰伪 prefab 实例
  - PseudoPrefabManager stub：levelInfo / 音乐 / 环境音 / 音频目录

用法：
  python3 gen-scenes.py                 # 全部
  python3 gen-scenes.py OC2_DLC02_1_1   # 指定
"""
import json
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from oc2_common import (ROOT, ASSETS, Scene, EditorIndex, map_item, base_name,
                        deterministic_guid)
from importlib.machinery import SourceFileLoader
gla = SourceFileLoader("gen_level_assets", os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "gen-level-assets.py")).load_module()

OUT_SCENES = os.path.join(ASSETS, "LevelSets", "oc2_dlc_story", "scenes")
TEMPLATE = os.path.join(ASSETS, "Template", "s_template.unity")

# ---- 模板骨架固定 fileID ----
FID = {
    "design_go": 1601241121, "design_tr": 1601241122,
    "collision_go": 426971518, "collision_tr": 426971519,
    "utensils_go": 300944383, "utensils_tr": 300944384,
    "counters_go": 492637707, "counters_tr": 492637708,
    "floor_col_go": 1772028758, "floor_col_tr": 1772028759, "floor_col_box": 1772028760,
    "chefs_go": 274136423, "chefs_tr": 274136424,
    "art_go": 137629142, "art_tr": 137629143,
    "lights_go": 750247770, "lights_tr": 750247771,
    "day_go": 846550584, "day_tr": 846550585, "day_light": 846550586,
    "ground_go": 1443152664, "ground_tr": 1443152665,
    "floor_go": 55104702, "floor_tr": 55104703, "floor_mr": 55104704, "floor_mf": 55104705,
    "cam_root_go": 420415744, "cam_root_tr": 420415746,
    "debug_go": 409915802,
    "mgr_go": 975597275, "mgr_stub": 975597277,
}
ENV_GUID = "0c70fdc0bdf8b644e82e3ebc3f7a8140"
ENV_ROOT_TR = 4331603006656700
ENV_GRID_TR = 4591096154185836
ENV_GRID_COMP = 114964503590968644
ENV_KP_TR = 4387514011412392
ENV_KP_RESPAWN = 114694390702290380
PLAYER_GUID = "78d1be00b5b01df4ca974d31ced391b8"
PLAYER_TR = 4319532165554580
PLAYER_GO = 1382526047942262
PLAYER_PID = 114800773322134856
FLOOR_MAT_GUID = "8a4202dd5921a734291b1222e0725671"
RESPAWN_SCRIPT_GUID = "41abd1af01e045ca82b6eace136ae95a"

SCENE_META_TMPL = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


class FidGen:
    def __init__(self):
        self.n = 3_000_000_000

    def next(self):
        self.n += 1
        return self.n


def fnum(s):
    """raw 字符串原样；float 简化。"""
    if isinstance(s, str):
        return s
    if isinstance(s, float) and s == int(s):
        return str(int(s))
    return repr(s) if isinstance(s, float) else str(s)


def yaw_from_quat(x, y, z, w):
    return math.degrees(math.atan2(2 * (w * y + x * z), 1 - 2 * (y * y + x * x)))


# ---------------------------------------------------------------------------
# YAML 片段生成
# ---------------------------------------------------------------------------

def go_yaml(fid, name, comps, layer=0):
    lines = [f"--- !u!1 &{fid}", "GameObject:",
             "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
             "  m_PrefabInternal: {fileID: 0}", "  serializedVersion: 5",
             "  m_Component:"]
    lines += [f"  - component: {{fileID: {c}}}" for c in comps]
    lines += [f"  m_Layer: {layer}", f"  m_Name: {name}",
              "  m_TagString: Untagged", "  m_Icon: {fileID: 0}",
              "  m_NavMeshLayer: 0", "  m_StaticEditorFlags: 0", "  m_IsActive: 1"]
    return "\n".join(lines)


def tr_yaml(fid, go, parent, root_order, pos, rot, scale, children):
    ch = "\n".join(f"  - {{fileID: {c}}}" for c in children)
    return "\n".join([
        f"--- !u!4 &{fid}", "Transform:",
        "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
        "  m_PrefabInternal: {fileID: 0}", f"  m_GameObject: {{fileID: {go}}}",
        f"  m_LocalRotation: {{x: {rot[0]}, y: {rot[1]}, z: {rot[2]}, w: {rot[3]}}}",
        f"  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}",
        f"  m_LocalScale: {{x: {scale[0]}, y: {scale[1]}, z: {scale[2]}}}",
        "  m_Children:" + ("" if children else " []"),
        *([ch] if children else []),
        f"  m_Father: {{fileID: {parent}}}",
        f"  m_RootOrder: {root_order}",
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}"])


def box_yaml(fid, go, size, center, trigger=0):
    return "\n".join([
        f"--- !u!65 &{fid}", "BoxCollider:",
        "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
        "  m_PrefabInternal: {fileID: 0}", f"  m_GameObject: {{fileID: {go}}}",
        "  m_Material: {fileID: 0}", f"  m_IsTrigger: {trigger}", "  m_Enabled: 1",
        "  serializedVersion: 2",
        f"  m_Size: {{x: {size[0]}, y: {size[1]}, z: {size[2]}}}",
        f"  m_Center: {{x: {center[0]}, y: {center[1]}, z: {center[2]}}}"])


def respawn_yaml(fid, go, rtype):
    return "\n".join([
        f"--- !u!114 &{fid}", "MonoBehaviour:",
        "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
        "  m_PrefabInternal: {fileID: 0}", f"  m_GameObject: {{fileID: {go}}}",
        "  m_Enabled: 1", "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {RESPAWN_SCRIPT_GUID}, type: 3}}",
        "  m_Name: ", "  m_EditorClassIdentifier: ",
        f"  m_respawnType: {rtype}",
        "  m_respawnFilter:", "    serializedVersion: 2", "    m_Bits: 4294967295",
        "  m_onlyRespawnables: 0", "  m_onRespawnTrigger: ",
        "  m_onDeathEffect: {fileID: 0}"])


def mod_line(target, pguid, path, value, objref=None):
    objref = objref or "{fileID: 0}"
    return (f"    - target: {{fileID: {target}, guid: {pguid}, type: 2}}\n"
            f"      propertyPath: {path}\n"
            f"      value: {value}\n"
            f"      objectReference: {objref}")


def prefab_instance(iid, tid, pguid, ptr, parent, mods):
    return "\n".join([
        f"--- !u!1001 &{iid}", "Prefab:",
        "  m_ObjectHideFlags: 0", "  serializedVersion: 2",
        "  m_Modification:", f"    m_TransformParent: {{fileID: {parent}}}",
        "    m_Modifications:", *mods, "    m_RemovedComponents: []",
        f"  m_ParentPrefab: {{fileID: 100100000, guid: {pguid}, type: 2}}",
        "  m_IsPrefabParent: 0",
        f"--- !u!4 &{tid} stripped", "Transform:",
        f"  m_PrefabParentObject: {{fileID: {ptr}, guid: {pguid}, type: 2}}",
        f"  m_PrefabInternal: {{fileID: {iid}}}"])


# ---------------------------------------------------------------------------
# 源场景信息提取
# ---------------------------------------------------------------------------

def raw(doc, name):
    m = re.search(re.escape(name) + r": (\{[^}]*\}|[^\n]*)", doc.body)
    return m.group(1).strip() if m else None


def raw_vec(doc, name, default=("0", "0", "0")):
    v = raw(doc, name)
    if not v or not v.startswith("{"):
        return default
    parts = dict(re.findall(r"(\w): ([^,}]+)", v))
    return (parts.get("x", "0"), parts.get("y", "0"), parts.get("z", "0"))


def raw_quat(doc, name="m_LocalRotation", default=("0", "0", "0", "1")):
    v = raw(doc, name)
    if not v or not v.startswith("{"):
        return default
    parts = dict(re.findall(r"(\w): ([^,}]+)", v))
    return (parts.get("x", "0"), parts.get("y", "0"),
            parts.get("z", "0"), parts.get("w", "1"))


class SourceInfo:
    def __init__(self, scene):
        self.scene = scene
        self.grid_pos = ("0", "0", "0")
        self.grid_half = (9, 5)
        self.cell = 1.2
        self.killplane_pos = ("0", "-1", "0")
        self.killplane_scale = ("7", "1", "7")
        self.killplane_respawn = 1
        self.players = {}
        self.dir_light = None
        self.render = {}
        self.floor_box = None      # (pos, rot, scale, size, center) 源 Ground 碰撞
        self.walls = []            # Col_Wall 数据
        self.killplanes = []       # 额外表杀
        self.counters = []         # (nodeName, trDoc, resolve)
        self.utensils = []
        self.decor = []
        self.misses = []
        self._parse()

    def _parse(self):
        sc = self.scene
        env = sc.find_root("CampaignGameEnvironment")
        for c in sc.children.get(env, []):
            go = sc.tr2go.get(c)
            nm = sc.go_name(go)
            tr = sc.docs[c]
            if nm == "GridManager":
                self.grid_pos = raw_vec(tr, "m_LocalPosition")
                for m in sc.monos(go):
                    if "m_gridHalfSize" in m.body:
                        gx = re.search(r"m_gridHalfSize:\s*\n\s*X: (\d+)", m.body)
                        gz = re.search(r"Z: (\d+)", m.body[m.body.find("m_gridHalfSize"):])
                        if gx and gz:
                            self.grid_half = (int(gx.group(1)), int(gz.group(1)))
                        sz = re.search(r"m_size: \{x: ([\d.]+)", m.body)
                        if sz:
                            self.cell = float(sz.group(1))
            elif nm == "KillPlane":
                self.killplane_pos = raw_vec(tr, "m_LocalPosition")
                self.killplane_scale = raw_vec(tr, "m_LocalScale", ("7", "1", "7"))
                for m in sc.monos(go):
                    r = re.search(r"m_respawnType: (\d+)", m.body)
                    if r:
                        self.killplane_respawn = int(r.group(1))
        # 玩家
        ch = sc.find_root("Chefs")
        for c in sc.children.get(ch, []):
            go = sc.tr2go.get(c)
            nm = sc.go_name(go)
            m = re.match(r"Player (\d+)$", nm or "")
            if m:
                tr = sc.docs[c]
                hint = raw(tr, "m_LocalEulerAnglesHint")
                hy = "180"
                if hint:
                    hm = re.search(r"y: ([^,}]+)", hint)
                    if hm:
                        hy = hm.group(1)
                self.players[int(m.group(1))] = {
                    "pos": raw_vec(tr, "m_LocalPosition"),
                    "rot": raw_quat(tr),
                    "hintY": hy,
                }
        # 平行光
        art = sc.find_root("Art")
        li = sc.find_child(art, "Lights") if art else None
        if li:
            for c in sc.children.get(li, []):
                go = sc.tr2go.get(c)
                for comp in sc.components(go):
                    dc = sc.docs.get(comp)
                    if dc and dc.cls == 108 and dc.field_str("m_Type") == "1":
                        self.dir_light = {
                            "rot": raw_quat(sc.docs[c]),
                            "color": raw(dc, "m_Color"),
                            "intensity": dc.field_str("m_Intensity"),
                            "shadowStrength": re.search(
                                r"m_Strength: ([\d.]+)", dc.body).group(1)
                            if re.search(r"m_Strength: ([\d.]+)", dc.body) else "0.629",
                        }
                        break
                if self.dir_light:
                    break
        # RenderSettings
        rs = next((d for d in sc.docs.values() if d.cls == 104), None)
        if rs:
            for f in ("m_Fog", "m_FogColor", "m_FogMode", "m_FogDensity",
                      "m_AmbientSkyColor", "m_AmbientEquatorColor",
                      "m_AmbientGroundColor", "m_AmbientIntensity", "m_AmbientMode",
                      "m_IndirectSpecularColor"):
                v = raw(rs, f)
                if v is not None:
                    self.render[f] = v
        # Design/Collision：递归收集 Ground/Wall/KillPlane 碰撞盒
        design = sc.find_root("Design")
        col = sc.find_child(design, "Collision") if design else None
        if col:
            self._walk_collision(col)

        # Design/KillPlanes：额外表杀平面
        kp_grp = sc.find_child(design, "KillPlanes") if design else None
        if kp_grp:
            for c in sc.children.get(kp_grp, []):
                go = sc.tr2go.get(c)
                tr = sc.docs[c]
                box = next((sc.docs[x] for x in sc.components(go)
                            if x in sc.docs and sc.docs[x].cls == 65), None)
                self._add_killplane(go, tr, box)

        # Design 其余组：递归映射物件，按目录分类到 Counters/Utensils/Scenery
        UTENSIL_GROUPS = {"utensils", "glasses", "plates", "mugs", "trays"}
        if design:
            for g in sc.children.get(design, []):
                gname = (sc.tr_name(g) or "").lower()
                if gname in ("collision", "killplanes"):
                    continue
                self._walk_items(g, "utensils" if gname in UTENSIL_GROUPS else None)

        # Art：除 Lights/Ground/烘焙节点外全部当装饰走
        art = sc.find_root("Art")
        if art:
            for g in sc.children.get(art, []):
                gname = (sc.tr_name(g) or "").lower()
                if gname in ("lights", "ground", "mesh baker", "mesh bakers",
                             "spotlights", "light probes"):
                    continue
                self._walk_items(g, "decor")
        # 根级游离 Scenery（如 s_city_h14）
        for r in sc.roots:
            if (sc.tr_name(r) or "").lower() == "scenery":
                self._walk_items(r, "decor")

    def _walk_collision(self, root_tr):
        sc = self.scene
        for c in sc.children.get(root_tr, []):
            go = sc.tr2go.get(c)
            nm = (base_name(sc.go_name(go)) or "").lower()
            tr = sc.docs[c]
            box = next((sc.docs[x] for x in sc.components(go)
                        if x in sc.docs and sc.docs[x].cls == 65), None)
            if box is None:
                self._walk_collision(c)  # 嵌套组（如 Walls (1)）
                continue
            if nm.startswith("ground"):
                self.floor_box = {
                    "pos": raw_vec(tr, "m_LocalPosition"),
                    "rot": raw_quat(tr),
                    "scale": raw_vec(tr, "m_LocalScale", ("1", "1", "1")),
                    "size": raw_vec(box, "m_Size"),
                    "center": raw_vec(box, "m_Center"),
                }
            elif nm.startswith("wall"):
                self.walls.append({
                    "pos": raw_vec(tr, "m_LocalPosition"), "rot": raw_quat(tr),
                    "scale": raw_vec(tr, "m_LocalScale", ("1", "1", "1")),
                    "size": raw_vec(box, "m_Size"), "center": raw_vec(box, "m_Center"),
                })
            elif nm.startswith("killplane"):
                self._add_killplane(go, tr, box)

    def _walk_items(self, root_tr, forced_bucket):
        """递归映射物件。forced_bucket: 'utensils' | 'decor' | None(按目录分类)。"""
        sc = self.scene
        idx = EditorIndexHolder.get()
        stack = list(sc.children.get(root_tr, []))
        while stack:
            tr = stack.pop()
            kind, b, *rest = map_item(sc, idx, tr)
            if kind == "place":
                node_name = sc.go_name(sc.tr2go.get(tr))
                tr_doc = sc.docs[tr]
                res = rest[0]
                if forced_bucket == "utensils":
                    self.utensils.append((node_name, tr_doc, res))
                elif forced_bucket == "decor":
                    self.decor.append((node_name, tr_doc, res))
                elif self._is_utensil(res):
                    self.utensils.append((node_name, tr_doc, res))
                elif self._is_counter(res):
                    self.counters.append((node_name, tr_doc, res))
                else:
                    self.decor.append((node_name, tr_doc, res))
            elif kind == "group":
                stack.extend(sc.children.get(tr, []))
            elif kind == "miss":
                self.misses.append(b)

    _cat_cache = {}

    @classmethod
    def _category(cls, res):
        ph = res["placeholder"]
        if ph not in cls._cat_cache:
            idx = EditorIndexHolder.get()
            it = idx.catalog.get(res.get("type") or "") or \
                next((v for v in idx.catalog.values() if v.get("assetPath") == ph), {})
            cls._cat_cache[ph] = (it.get("category") or "").lower()
        return cls._cat_cache[ph]

    @classmethod
    def _is_utensil(cls, res):
        return "utensil" in cls._category(res) or "equipment" in cls._category(res)

    @classmethod
    def _is_counter(cls, res):
        c = cls._category(res)
        return c.startswith("counters") or c.startswith("workstation")

    def _add_killplane(self, go, tr, box):
        rtype = 2
        for m in self.scene.monos(go):
            r = re.search(r"m_respawnType: (\d+)", m.body)
            if r:
                rtype = int(r.group(1))
        layer = self.scene.gos[go].field_str("m_Layer") or "0"
        self.killplanes.append({
            "pos": raw_vec(tr, "m_LocalPosition"), "rot": raw_quat(tr),
            "scale": raw_vec(tr, "m_LocalScale", ("1", "1", "1")),
            "size": raw_vec(box, "m_Size") if box else ("1", "1", "1"),
            "center": raw_vec(box, "m_Center") if box else ("0", "0", "0"),
            "rtype": rtype, "layer": layer,
        })


class EditorIndexHolder:
    _idx = None

    @classmethod
    def get(cls):
        if cls._idx is None:
            cls._idx = EditorIndex()
        return cls._idx


# ---------------------------------------------------------------------------
# 场景组装
# ---------------------------------------------------------------------------

def build_scene(level):
    src = Scene(os.path.join(ROOT, level["sourceScene"]))
    info = SourceInfo(src)
    idx = EditorIndexHolder.get()
    fg = FidGen()
    docs = []

    # 1) 头部 4 个文档（模板原文，RenderSettings 打补丁）
    tmpl = open(TEMPLATE, "r", encoding="utf-8").read()
    head = tmpl[:tmpl.find("--- !u!1 &55104702")]
    for k, v in info.render.items():
        head = re.sub(re.escape(k) + r": [^\n]*", f"{k}: {v}", head, count=1)
    docs.append(head.rstrip("\n"))

    # 2) 环境 prefab 实例（模板块 + mods）
    env_mods = [
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_LocalPosition.x", "0"),
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_LocalPosition.y", "0"),
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_LocalPosition.z", "0"),
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_LocalRotation.x", "-0"),
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_LocalRotation.y", "-0"),
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_LocalRotation.z", "-0"),
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_LocalRotation.w", "1"),
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_RootOrder", "0"),
        mod_line(ENV_GRID_TR, ENV_GUID, "m_LocalPosition.x", info.grid_pos[0]),
        mod_line(ENV_GRID_TR, ENV_GUID, "m_LocalPosition.y", info.grid_pos[1]),
        mod_line(ENV_GRID_TR, ENV_GUID, "m_LocalPosition.z", info.grid_pos[2]),
        mod_line(ENV_GRID_COMP, ENV_GUID, "m_gridHalfSize.X", str(info.grid_half[0])),
        mod_line(ENV_GRID_COMP, ENV_GUID, "m_gridHalfSize.Z", str(info.grid_half[1])),
        mod_line(ENV_KP_TR, ENV_GUID, "m_LocalPosition.x", info.killplane_pos[0]),
        mod_line(ENV_KP_TR, ENV_GUID, "m_LocalPosition.y", info.killplane_pos[1]),
        mod_line(ENV_KP_TR, ENV_GUID, "m_LocalPosition.z", info.killplane_pos[2]),
        mod_line(ENV_KP_TR, ENV_GUID, "m_LocalScale.x", info.killplane_scale[0]),
        mod_line(ENV_KP_TR, ENV_GUID, "m_LocalScale.y", info.killplane_scale[1]),
        mod_line(ENV_KP_TR, ENV_GUID, "m_LocalScale.z", info.killplane_scale[2]),
        mod_line(ENV_KP_RESPAWN, ENV_GUID, "m_respawnType", str(info.killplane_respawn)),
    ]
    env_block = prefab_instance(1056758579, 0, ENV_GUID, ENV_ROOT_TR, 0, env_mods)
    # 去掉 stripped Transform（env 用 stripped GameObject 集合代替）
    env_block = env_block[:env_block.find(f"--- !u!4 &0 stripped")]
    docs.append(env_block)
    for sfid, pfid in ((1056758580, 1695328107371074), (1056758581, 1263439063448506),
                       (1056758582, 1538596508456208), (1056758583, 1083572226556830),
                       (1056758584, 1272937010316338), (1056758585, 1910194520664404)):
        docs.append("\n".join([
            f"--- !u!1 &{sfid} stripped", "GameObject:",
            f"  m_PrefabParentObject: {{fileID: {pfid}, guid: {ENV_GUID}, type: 2}}",
            f"  m_PrefabInternal: {{fileID: 1056758579}}"]))
    docs.append("\n".join([
        "--- !u!114 &1056758586 stripped", "MonoBehaviour:",
        f"  m_PrefabParentObject: {{fileID: 114101035504861502, guid: {ENV_GUID}, type: 2}}",
        "  m_PrefabInternal: {fileID: 1056758579}",
        "  m_Script: {fileID: 11500000, guid: b3b3020cd743f2a49408c8f20306f0c2, type: 3}"]))

    # 3) Design 子组物件 -> prefab 实例
    counter_children, utensil_children, scenery_children = [], [], []
    item_docs = []

    def emit_item(name, tr, res, parent_fid, order):
        anchors = idx.placeholder_anchors(res["placeholder"])
        pguid = anchors["guid"]
        iid, tid = fg.next(), fg.next()
        pos = raw_vec(tr, "m_LocalPosition")
        rot = raw_quat(tr)
        scale = raw_vec(tr, "m_LocalScale", ("1", "1", "1"))
        mods = [
            mod_line(anchors["tr"], pguid, "m_LocalPosition.x", pos[0]),
            mod_line(anchors["tr"], pguid, "m_LocalPosition.y", pos[1]),
            mod_line(anchors["tr"], pguid, "m_LocalPosition.z", pos[2]),
            mod_line(anchors["tr"], pguid, "m_LocalRotation.x", rot[0]),
            mod_line(anchors["tr"], pguid, "m_LocalRotation.y", rot[1]),
            mod_line(anchors["tr"], pguid, "m_LocalRotation.z", rot[2]),
            mod_line(anchors["tr"], pguid, "m_LocalRotation.w", rot[3]),
            mod_line(anchors["tr"], pguid, "m_RootOrder", str(order)),
            mod_line(anchors["go"], pguid, "m_Name", name),
        ]
        if res.get("soGuid") and res["kind"] == "appearance":
            mods.append(mod_line(
                anchors["stub"], pguid, "pseudoPrefabSO", "",
                f"{{fileID: 11400000, guid: {res['soGuid']}, type: 2}}"))
        if scale != ("1", "1", "1"):
            for i, ax in enumerate("xyz"):
                mods.append(mod_line(anchors["tr"], pguid, f"m_LocalScale.{ax}", scale[i]))
        try:
            yaw = yaw_from_quat(*[float(v) for v in rot])
            mods.append(mod_line(anchors["tr"], pguid, "m_LocalEulerAnglesHint.y",
                                 fnum(round(yaw, 4))))
        except (ValueError, TypeError):
            pass
        item_docs.append(prefab_instance(iid, tid, pguid, anchors["tr"], parent_fid, mods))
        return tid

    # 分类已在 SourceInfo 完成
    for name, tr, res in info.counters:
        counter_children.append(emit_item(name, tr, res, FID["counters_tr"],
                                          len(counter_children)))
    for name, tr, res in info.utensils:
        utensil_children.append(emit_item(name, tr, res, FID["utensils_tr"],
                                          len(utensil_children)))
    scenery_parent = fg.next()  # Scenery 组 transform fid（后面用）
    scenery_go = fg.next()
    for name, tr, res in info.decor:
        scenery_children.append(emit_item(name, tr, res, scenery_parent,
                                          len(scenery_children)))

    # 4) Collision：Col_Floor + Col_Wall*
    col_children = [FID["floor_col_tr"]]
    wall_docs = []
    for i, w in enumerate(info.walls):
        g, t, b = fg.next(), fg.next(), fg.next()
        nm = "Col_Wall" if i == 0 else f"Col_Wall ({i})"
        wall_docs.append(go_yaml(g, nm, [t, b], layer=10))
        wall_docs.append(tr_yaml(t, g, FID["collision_tr"], len(col_children),
                                 w["pos"], w["rot"], w["scale"], []))
        wall_docs.append(box_yaml(b, g, w["size"], w["center"]))
        col_children.append(t)
    fb = info.floor_box or {"pos": (info.grid_pos[0], "0", info.grid_pos[2]),
                            "rot": ("-0", "-0", "-0", "1"), "scale": ("1", "1", "1"),
                            "size": (fnum(2 * info.grid_half[0] * info.cell), "0.4",
                                     fnum(2 * info.grid_half[1] * info.cell)),
                            "center": ("0", "-0.2", "0")}
    floor_col_docs = [
        go_yaml(FID["floor_col_go"], "Col_Floor",
                [FID["floor_col_tr"], FID["floor_col_box"]], layer=9),
        tr_yaml(FID["floor_col_tr"], FID["floor_col_go"], FID["collision_tr"], 0,
                fb["pos"], fb["rot"], fb["scale"], []),
        box_yaml(FID["floor_col_box"], FID["floor_col_go"], fb["size"], fb["center"]),
    ]

    # 5) KillPlanes 组（有额外表杀时）
    kp_group_docs = []
    kp_children = []
    kp_go = kp_tr = None
    for i, kp in enumerate(info.killplanes):
        g, t, b, r = fg.next(), fg.next(), fg.next(), fg.next()
        nm = "KillPlane" if i == 0 else f"KillPlane ({i})"
        kp_group_docs.append(go_yaml(g, nm, [t, b, r], layer=int(kp["layer"])))
        kp_group_docs.append(tr_yaml(t, g, kp_tr or 0, i, kp["pos"], kp["rot"],
                                     kp["scale"], []))
        kp_group_docs.append(box_yaml(b, g, kp["size"], kp["center"], trigger=1))
        kp_group_docs.append(respawn_yaml(r, g, kp["rtype"]))
        kp_children.append(t)
    design_children = [FID["collision_tr"], FID["utensils_tr"], FID["counters_tr"]]
    if kp_children:
        kp_go, kp_tr = fg.next(), fg.next()
        # 回填父引用
        kp_group_docs = [d.replace(f"m_Father: {{fileID: 0}}", f"m_Father: {{fileID: {kp_tr}}}", 1)
                         if f"--- !u!4 " in d else d for d in kp_group_docs]
        kp_group_docs.insert(0, go_yaml(kp_go, "KillPlanes", [kp_tr]))
        kp_group_docs.insert(1, tr_yaml(kp_tr, kp_go, FID["design_tr"],
                                        3, ("0", "0", "0"), ("0", "0", "0", "1"),
                                        ("1", "1", "1"), kp_children))
        design_children.append(kp_tr)

    # 6) 玩家实例
    player_children = []
    player_docs = []
    for n in sorted(info.players):
        p = info.players[n]
        iid, tid = fg.next(), fg.next()
        mods = [
            mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalPosition.x", p["pos"][0]),
            mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalPosition.y", p["pos"][1]),
            mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalPosition.z", p["pos"][2]),
            mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalRotation.x", p["rot"][0]),
            mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalRotation.y", p["rot"][1]),
            mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalRotation.z", p["rot"][2]),
            mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalRotation.w", p["rot"][3]),
            mod_line(PLAYER_TR, PLAYER_GUID, "m_RootOrder", str(n - 1)),
            mod_line(PLAYER_GO, PLAYER_GUID, "m_Name", f"Player {n}"),
            mod_line(PLAYER_PID, PLAYER_GUID, "playerID", str(n - 1)),
            mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalEulerAnglesHint.y", p["hintY"]),
        ]
        player_docs.append(prefab_instance(iid, tid, PLAYER_GUID, PLAYER_TR,
                                           FID["chefs_tr"], mods))
        player_children.append(tid)

    # 7) 骨架组文档
    skeleton = [
        # Design
        go_yaml(FID["design_go"], "Design", [FID["design_tr"]]),
        tr_yaml(FID["design_tr"], FID["design_go"], 0, 1, ("0", "0", "0"),
                ("0", "0", "0", "1"), ("1", "1", "1"), design_children),
        # Collision
        go_yaml(FID["collision_go"], "Collision", [FID["collision_tr"]]),
        tr_yaml(FID["collision_tr"], FID["collision_go"], FID["design_tr"], 0,
                ("0", "0", "0"), ("0", "0", "0", "1"), ("1", "1", "1"), col_children),
        # Utensils
        go_yaml(FID["utensils_go"], "Utensils", [FID["utensils_tr"]]),
        tr_yaml(FID["utensils_tr"], FID["utensils_go"], FID["design_tr"], 1,
                ("0", "0", "0"), ("0", "0", "0", "1"), ("1", "1", "1"), utensil_children),
        # Counters
        go_yaml(FID["counters_go"], "Counters", [FID["counters_tr"]]),
        tr_yaml(FID["counters_tr"], FID["counters_go"], FID["design_tr"], 2,
                ("0", "0", "0"), ("0", "0", "0", "1"), ("1", "1", "1"), counter_children),
        # Chefs
        go_yaml(FID["chefs_go"], "Chefs", [FID["chefs_tr"]]),
        tr_yaml(FID["chefs_tr"], FID["chefs_go"], 0, 3, ("0", "0", "0"),
                ("-0", "-0", "-0", "1"), ("1", "1", "1"), player_children),
        # Art
        go_yaml(FID["art_go"], "Art", [FID["art_tr"]]),
        tr_yaml(FID["art_tr"], FID["art_go"], 0, 4, ("0", "0", "0"),
                ("0", "0", "0", "1"), ("1", "1", "1"),
                [FID["lights_tr"], FID["ground_tr"], scenery_parent]),
        # Lights
        go_yaml(FID["lights_go"], "Lights", [FID["lights_tr"]]),
        tr_yaml(FID["lights_tr"], FID["lights_go"], FID["art_tr"], 0,
                ("0", "0", "0"), ("-0", "-0", "-0", "1"), ("1", "1", "1"),
                [FID["day_tr"]]),
        # Scenery
        go_yaml(scenery_go, "Scenery", [scenery_parent]),
        tr_yaml(scenery_parent, scenery_go, FID["art_tr"], 2, ("0", "0", "0"),
                ("0", "0", "0", "1"), ("1", "1", "1"), scenery_children),
        # Ground
        go_yaml(FID["ground_go"], "Ground", [FID["ground_tr"]]),
        tr_yaml(FID["ground_tr"], FID["ground_go"], FID["art_tr"], 1,
                ("0", "0", "0"), ("-0", "-0", "-0", "1"), ("1", "1", "1"),
                [FID["floor_tr"]]),
    ]

    # 平行光
    dl = info.dir_light or {}
    light_color = dl.get("color", "{r: 1, g: 0.93333334, b: 0.7921569, a: 1}")
    light = "\n".join([
        go_yaml(FID["day_go"], "day", [FID["day_tr"], FID["day_light"]]),
        tr_yaml(FID["day_tr"], FID["day_go"], FID["lights_tr"], 0,
                ("0", "0", "0"), dl.get("rot", ("0.78188676", "0.07891235",
                                                "-0.2563485", "0.5627713")),
                ("1", "1", "1"), []),
        f"--- !u!108 &{FID['day_light']}", "Light:",
        "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
        "  m_PrefabInternal: {fileID: 0}", f"  m_GameObject: {{fileID: {FID['day_go']}}}",
        "  m_Enabled: 1", "  serializedVersion: 8", "  m_Type: 1",
        f"  m_Color: {light_color}",
        f"  m_Intensity: {dl.get('intensity', '0.7')}",
        "  m_Range: 10", "  m_SpotAngle: 30", "  m_CookieSize: 10",
        "  m_Shadows:", "    m_Type: 1", "    m_Resolution: -1",
        "    m_CustomResolution: -1",
        f"    m_Strength: {dl.get('shadowStrength', '0.629')}",
        "    m_Bias: 0.01", "    m_NormalBias: 0", "    m_NearPlane: 0.1",
        "  m_Cookie: {fileID: 0}", "  m_DrawHalo: 0", "  m_Flare: {fileID: 0}",
        "  m_RenderMode: 0", "  m_CullingMask:", "    serializedVersion: 2",
        "    m_Bits: 4294967263", "  m_Lightmapping: 4", "  m_AreaSize: {x: 1, y: 1}",
        "  m_BounceIntensity: 1", "  m_ColorTemperature: 6570",
        "  m_UseColorTemperature: 0", "  m_ShadowRadius: 0", "  m_ShadowAngle: 0",
    ])

    # Floor quad（网格尺寸 + 模板材质）
    fw = 2 * info.grid_half[0] * info.cell / 10 * 1.15
    fh = 2 * info.grid_half[1] * info.cell / 10 * 1.15
    floor_docs = [
        go_yaml(FID["floor_go"], "Floor", [FID["floor_tr"], FID["floor_mr"], FID["floor_mf"]]),
        tr_yaml(FID["floor_tr"], FID["floor_go"], FID["ground_tr"], 0,
                (info.grid_pos[0], "-0.05", info.grid_pos[2]),
                ("-0", "-0", "-0", "1"), (fnum(round(fw, 4)), "1", fnum(round(fh, 4))), []),
        "\n".join([
            f"--- !u!23 &{FID['floor_mr']}", "MeshRenderer:",
            "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
            "  m_PrefabInternal: {fileID: 0}",
            f"  m_GameObject: {{fileID: {FID['floor_go']}}}",
            "  m_Enabled: 1", "  m_CastShadows: 1", "  m_ReceiveShadows: 1",
            "  m_DynamicOccludee: 1", "  m_MotionVectors: 1", "  m_LightProbeUsage: 0",
            "  m_ReflectionProbeUsage: 1", "  m_Materials:",
            f"  - {{fileID: 2100000, guid: {FLOOR_MAT_GUID}, type: 2}}",
            "  m_StaticBatchInfo:", "    firstSubMesh: 0", "    subMeshCount: 0",
            "  m_StaticBatchRoot: {fileID: 0}", "  m_ProbeAnchor: {fileID: 0}",
            "  m_LightProbeVolumeOverride: {fileID: 0}", "  m_ScaleInLightmap: 1",
            "  m_PreserveUVs: 1", "  m_IgnoreNormalsForChartDetection: 0",
            "  m_ImportantGI: 0", "  m_StitchLightmapSeams: 0",
            "  m_SelectedEditorRenderState: 3", "  m_MinimumChartSize: 4",
            "  m_AutoUVMaxDistance: 0.5", "  m_AutoUVMaxAngle: 89",
            "  m_LightmapParameters: {fileID: 0}", "  m_SortingLayerID: 0",
            "  m_SortingLayer: 0", "  m_SortingOrder: 0"]),
        "\n".join([
            f"--- !u!33 &{FID['floor_mf']}", "MeshFilter:",
            "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
            "  m_PrefabInternal: {fileID: 0}",
            f"  m_GameObject: {{fileID: {FID['floor_go']}}}",
            "  m_Mesh: {fileID: 10209, guid: 0000000000000000e000000000000000, type: 0}"]),
    ]

    # 8) 模板中保持不变的大块：相机 / Debug / PseudoPrefabManager（stub 打补丁）
    def grab(start_marker, end_marker):
        i = tmpl.find(start_marker)
        j = tmpl.find(end_marker, i)
        return tmpl[i:j].rstrip("\n")

    cam_block = grab("--- !u!1 &420415744", "--- !u!1 &426971518")
    # 相机根位置对齐网格中心（模板偏移：z = centerZ - 11.6）
    gx = float(info.grid_pos[0])
    gz = float(info.grid_pos[2])
    cam_block = cam_block.replace(
        "m_LocalPosition: {x: 5.4, y: 22, z: -15.2}",
        f"m_LocalPosition: {{x: {fnum(round(gx, 4))}, y: 22, z: {fnum(round(gz - 11.6, 4))}}}")
    cam_child = grab("--- !u!1 &2005137850", "--- !u!1001 &2031069185")
    debug_block = grab("--- !u!1 &409915802", "--- !u!1 &420415744")
    mgr_block = grab("--- !u!1 &975597275", "--- !u!1001 &1056758579")

    # PseudoPrefabManager stub 数据（与 LevelInfo 同源）
    music_guid, dir_guids, amb_hex, _b, _w = gla.audio_for_level(level)
    lid = level["id"]
    info_guid = deterministic_guid("oc2_dlc_story", f"{lid}/LevelInfo_{lid}.asset")
    mgr_block = re.sub(r"levelInfo: \{fileID: 11400000, guid: [0-9a-f]+, type: 2\}",
                       f"levelInfo: {{fileID: 11400000, guid: {info_guid}, type: 2}}",
                       mgr_block)
    mgr_block = re.sub(r"InLevelMusicSO: \{fileID: 11400000, guid: [0-9a-f]+, type: 2\}",
                       f"InLevelMusicSO: {{fileID: 11400000, guid: {music_guid}, type: 2}}"
                       if music_guid else "InLevelMusicSO: {fileID: 0}", mgr_block)
    mgr_block = re.sub(r"InLevelAmbiences: [^\n]*",
                       f"InLevelAmbiences: {amb_hex}", mgr_block)
    dirs_yaml = "\n".join(
        f"  - {{fileID: 11400000, guid: {g}, type: 2}}" for g in dir_guids)
    mgr_block = re.sub(r"AudioDirectorySOs:\n(?:  - \{[^\n]*\n)+",
                       f"AudioDirectorySOs:\n{dirs_yaml}\n", mgr_block)

    # 9) 组装（环境实例、Design、物件、骨架、玩家、灯光、地板、相机、Debug、Manager）
    out = list(docs)
    out += skeleton
    out += floor_col_docs + wall_docs + kp_group_docs
    out += item_docs + player_docs
    out.append(light)
    out += floor_docs
    out += [cam_block, cam_child, debug_block, mgr_block]
    text = "\n".join(d.rstrip("\n") for d in out) + "\n"

    # 场景物件 SO 的 bundle 并入 LevelInfo.dependencies（LevelInfo 已由 gen-level-assets 生成）
    item_bundles = set()
    for _nm, _tr, res in info.counters + info.utensils + info.decor:
        so = idx.so_by_guid.get(res["soGuid"]) if res.get("soGuid") else None
        if so and so.get("bundleName"):
            item_bundles.add(so["bundleName"])
    return text, info, info_guid, item_bundles


def merge_level_info_deps(lid, item_bundles):
    """把场景物件 SO 的 bundle 并入 LevelInfo.dependencies。"""
    p = os.path.join(ASSETS, "LevelSets", "oc2_dlc_story", "data", lid,
                     f"LevelInfo_{lid}.asset")
    if not os.path.exists(p):
        return
    txt = open(p, "r", encoding="utf-8").read()
    m = re.search(r"  dependencies:\n((?:  - bundle\d+\n)*)", txt)
    if not m:
        return
    deps = set(re.findall(r"  - (bundle\d+)", m.group(1)))
    deps |= item_bundles
    if not deps:
        block = "  dependencies: []\n"
    else:
        block = "  dependencies:\n" + "".join(f"  - {b}\n" for b in
                                             sorted(deps, key=lambda s: int(s[6:])))
    txt = txt[:m.start()] + block + txt[m.end():]
    with open(p, "w", encoding="utf-8") as f:
        f.write(txt)


def main():
    with open(os.path.join(ROOT, "layout-editor", "scripts", "oc2-import",
                           "out", "levels.json"), "r", encoding="utf-8") as f:
        levels = json.load(f)["levels"]
    only = set(a for a in sys.argv[1:] if not a.startswith("--"))
    report = []
    for lv in levels:
        if only and lv["id"] not in only:
            continue
        text, info, info_guid, item_bundles = build_scene(lv)
        scene_name = lv["editorSceneName"]
        os.makedirs(OUT_SCENES, exist_ok=True)
        path = os.path.join(OUT_SCENES, scene_name + ".unity")
        with open(path, "w", encoding="utf-8") as f:
            f.write(text)
        guid = deterministic_guid("oc2_dlc_story", f"scenes/{scene_name}.unity")
        with open(path + ".meta", "w", encoding="utf-8") as f:
            f.write(SCENE_META_TMPL.format(guid=guid))
        merge_level_info_deps(lv["id"], item_bundles)
        report.append({
            "id": lv["id"], "scene": scene_name, "guid": guid,
            "levelInfoGuid": info_guid,
            "counters": len(info.counters), "utensils": len(info.utensils),
            "decor": len(info.decor), "walls": len(info.walls),
            "killplanes": len(info.killplanes), "players": len(info.players),
            "misses": sorted(set(info.misses)),
        })
        r = report[-1]
        print(f"{lv['id']}: counters={r['counters']} utensils={r['utensils']} "
              f"decor={r['decor']} walls={r['walls']} kp={r['killplanes']} "
              f"players={r['players']} misses={r['misses']}")
    out = os.path.join(ROOT, "layout-editor", "scripts", "oc2-import", "out",
                       "gen-scenes-report.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=1)
    print(f"\n报告 -> {out}")


if __name__ == "__main__":
    main()
