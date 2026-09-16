#!/usr/bin/env python3
"""
supplement-oc2dlc-floors.py — 为 oc2_dlc_story 关卡集补齐生成时未解析成功的地板物件。

背景：gen-scenes.py 生成场景时按名字把源场景物件映射到 pseudo wrapper，
部分地板物件（草地卡/苔藓/沙地贴花/土路/地面网格等）当时在索引里查不到
（gen-scenes-report.json 的 misses），导致关卡内地板视觉缺口。

策略：
  1. misses 过滤出地板类名字，映射到游戏 bundle 内真实资产（.prefab / .fbx
     GameObject 容器，见 MAPPING 表——由 manifest 侦察生成，人工校对）。
  2. 资产落位 commonW2（与 restore-oc2dlc-story 同约定）：
     - 工程已有同 assetPath basename 的 pseudo SO → 复用；wrapper 缺则按
       确定性 guid(md5("prefab:<id>")) 补建；
     - 否则新建 SO(guid=md5("pseudo:<id>")) + wrapper，模板与
       import-dlc-content.mjs 完全一致（种子 fileID 三组件空壳）。
  3. 按源场景重走 gen-scenes 同款遍历（Design/Art 子树），把漏掉的名字
     作为 Prefab 实例追加到生成场景的 Art/Scenery 下（位置/旋转/缩放取源值）。
     幂等：场景中已存在同名实例则跳过。

不处理（记录到报告）：内建 Quad+材质的地板（Kitchen_Tile_ 等，属地板 quad
材质范畴）、场景唯一网格（m_dlc5_ground_2_5、dlc11 楼顶/路面等，包内无资产）、
绑骨/部件噪声。

用法：python3 layout-editor/scripts/oc2-import/supplement-oc2dlc-floors.py
"""
import hashlib
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ASSETS = os.path.join(ROOT, "Assets")
SET_DIR = os.path.join(ASSETS, "LevelSets", "oc2_dlc_story")
W2_ROOT = os.path.join(ASSETS, "commonW2")
OUT_DIR = os.path.join(HERE, "out")
LEVELS_JSON = os.path.join(OUT_DIR, "levels.json")
REPORT_JSON = os.path.join(OUT_DIR, "gen-scenes-report.json")
OC2_EDITOR = os.path.abspath(os.path.join(ROOT, ".."))
RIPPER_FALLBACK = os.path.join(OC2_EDITOR, "backup_20260826")

sys.path.insert(0, HERE)
from oc2_common import Scene, base_name

# ---------------------------------------------------------------- 漏项 → 游戏资产映射
# miss base_name -> (origin_dlc, bundleName, assetPath)
MAPPING = {
    # dlc04 dressingassets（dlc05/dlc10/dlc13 复用）
    "p_dlc5_grass_card_a":  ("dlc04", "bundle226", "assets/downloadablecontent/dlc04/dlc_assets/prefabs/dressingassets/p_dlc4_grass_card_a.prefab"),
    "p_dlc5_grass_card_b":  ("dlc04", "bundle226", "assets/downloadablecontent/dlc04/dlc_assets/prefabs/dressingassets/p_dlc4_grass_card_b.prefab"),
    "p_dlc5_grass_card_c":  ("dlc04", "bundle226", "assets/downloadablecontent/dlc04/dlc_assets/prefabs/dressingassets/p_dlc4_grass_card_c.prefab"),
    "p_dlc13_grass_card_a": ("dlc04", "bundle226", "assets/downloadablecontent/dlc04/dlc_assets/prefabs/dressingassets/p_dlc4_grass_card_a.prefab"),
    "p_dlc13_grass_card_b": ("dlc04", "bundle226", "assets/downloadablecontent/dlc04/dlc_assets/prefabs/dressingassets/p_dlc4_grass_card_b.prefab"),
    "p_dlc13_grass_card_c": ("dlc04", "bundle226", "assets/downloadablecontent/dlc04/dlc_assets/prefabs/dressingassets/p_dlc4_grass_card_c.prefab"),
    "m_dlc4_moss_01": ("dlc04", "bundle226", "assets/downloadablecontent/dlc04/dlc_assets/prefabs/dressingassets/p_dlc4_moss_01.prefab"),
    "m_dlc4_moss_02": ("dlc10", "bundle420", "assets/downloadablecontent/dlc10/dlc_assets/models/dressingassets/m_dlc4_moss_02.fbx"),
    "m_dlc4_moss_03": ("dlc04", "bundle226", "assets/downloadablecontent/dlc04/dlc_assets/prefabs/dressingassets/p_dlc4_moss_03.prefab"),
    "m_dlc4_moss_04": ("dlc04", "bundle226", "assets/downloadablecontent/dlc04/dlc_assets/prefabs/dressingassets/p_dlc4_moss_04.prefab"),
    "m_dlc4_mossfloor_02": ("dlc10", "bundle420", "assets/downloadablecontent/dlc10/dlc_assets/models/dressingassets/m_dlc4_mossfloor_02.fbx"),
    "m_dlc4_mossfloor_03": ("dlc10", "bundle420", "assets/downloadablecontent/dlc10/dlc_assets/models/dressingassets/m_dlc4_mossfloor_03.fbx"),
    "m_dlc4_lvl7_crazyfloor_01": ("dlc04", "bundle225", "assets/downloadablecontent/dlc04/dlc_assets/models/dressingassets/m_dlc4_lvl7_crazyfloor_01.fbx"),
    # dlc02 沙滩
    "m_dlc2_sanddecal_05": ("dlc02", "bundle163", "assets/downloadablecontent/dlc02/dlc_assets/models/beach theme/m_dlc2_sanddecal_05.fbx"),
    "m_dlc2_sanddecal_06": ("dlc02", "bundle163", "assets/downloadablecontent/dlc02/dlc_assets/models/beach theme/m_dlc2_sanddecal_06.fbx"),
    "m_dlc2_floorpiece_2_2": ("dlc02", "bundle163", "assets/downloadablecontent/dlc02/dlc_assets/models/beach theme/m_dlc2_floorpiece_2_2.fbx"),
    "Sand_Quad_tile_3x1_01": ("dlc02", "bundle163", "assets/downloadablecontent/dlc02/dlc_assets/models/beach theme/m_dlc2_quad_tile_3x1_01.fbx"),
    "wet_sand": ("dlc02", "bundle163", "assets/downloadablecontent/dlc02/dlc_assets/models/beach theme/m_dlc2_beach_sea_plane_wet_sand.fbx"),
    "m_dlc2_seaplane_ThroneRoom_01 wet_sand": ("dlc02", "bundle163", "assets/downloadablecontent/dlc02/dlc_assets/models/beach theme/m_dlc2_beach_sea_plane_wet_sand.fbx"),
    # dlc03 / dlc05 / dlc08 / dlc10
    "m_dlc3_robin_ground_01": ("dlc03", "bundle209", "assets/downloadablecontent/dlc03/dlc_assets/models/winterwonderland/m_dlc3_robin_ground_01.fbx"),
    "m_dlc5_camp_ground_Special": ("dlc05", "bundle248", "assets/downloadablecontent/dlc05/dlc_assets/models/dressing assets/m_dlc5_camp_ground_special.fbx"),
    "m_dlc08_ground_1_2": ("dlc08", "bundle358", "assets/downloadablecontent/dlc08/dlc_assets/prefabs/dressing assets/m_dlc08_ground_1_2.fbx"),
    "m_dlc10_path_01": ("dlc10", "bundle420", "assets/downloadablecontent/dlc10/dlc_assets/models/dressingassets/m_dlc10_path_01.fbx"),
    "m_dlc10_path_02": ("dlc10", "bundle420", "assets/downloadablecontent/dlc10/dlc_assets/models/dressingassets/m_dlc10_path_02.fbx"),
}

# wrapper/SO 落位主题子目录（与 commonW2/prefabs 既有 art/<theme>/ 布局一致）
THEME_BY_DLC = {
    "dlc02": "dlc02_beach", "dlc03": "dlc03_christmas", "dlc04": "dlc04",
    "dlc05": "dlc05_camping", "dlc07": "dlc07_horde", "dlc08": "dlc08_circus",
    "dlc09": "dlc09_wonderland", "dlc10": "dlc10", "dlc11": "dlc11_summer",
    "dlc13": "dlc13",
}


def theme_dir(dlc):
    return THEME_BY_DLC.get(dlc, dlc)


SO_SCRIPT = "0cff7c13895ab9e47a5e02d4619cc3b9"      # PseudoPrefabSO
STUB_SCRIPT = "0f66cc8b36034eb4c8eec31e1994e471"     # PseudoPrefabStub
PSEUDO_SCRIPT = "d58b99f9c4313714e9c4b11f1534ae6f"   # PseudoPrefab
# wrapper 种子模板 fileID（与 import-dlc-content.mjs propPrefab 一致）
SEED = {"go": 1554132891853676, "tr": 4341917854781290,
        "stub": 114096038827637246, "pseudo": 114967938430519454}

META_TMPL = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""
PREFAB_META_TMPL = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
FOLDER_META_TMPL = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName: commonW2
  assetBundleVariant:
"""

SO_TMPL = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {{fileID: 0}}
  m_PrefabInternal: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {so_script}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier:
  prefabName: {name}
  bundleName: {bundle}
  assetPath: {path}
"""

WRAPPER_TMPL = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1001 &100100000
Prefab:
  m_ObjectHideFlags: 1
  serializedVersion: 2
  m_Modification:
    m_TransformParent: {{fileID: 0}}
    m_Modifications: []
    m_RemovedComponents: []
  m_ParentPrefab: {{fileID: 0}}
  m_RootGameObject: {{fileID: {go}}}
  m_IsPrefabParent: 1
--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {{fileID: 0}}
  m_PrefabInternal: {{fileID: 100100000}}
  serializedVersion: 5
  m_Component:
  - component: {{fileID: {tr}}}
  - component: {{fileID: {stub}}}
  - component: {{fileID: {pseudo}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr}
Transform:
  m_ObjectHideFlags: 1
  m_PrefabParentObject: {{fileID: 0}}
  m_PrefabInternal: {{fileID: 100100000}}
  m_GameObject: {{fileID: {go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Children: []
  m_Father: {{fileID: 0}}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &{stub}
MonoBehaviour:
  m_ObjectHideFlags: 1
  m_PrefabParentObject: {{fileID: 0}}
  m_PrefabInternal: {{fileID: 100100000}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {stub_script}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  pseudoPrefabSO: {{fileID: 11400000, guid: {soguid}, type: 2}}
--- !u!114 &{pseudo}
MonoBehaviour:
  m_ObjectHideFlags: 1
  m_PrefabParentObject: {{fileID: 0}}
  m_PrefabInternal: {{fileID: 100100000}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {pseudo_script}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  childGameObject: {{fileID: 0}}
"""


def det_guid(prefix, key):
    return hashlib.md5(f"{prefix}:{key}".encode("utf-8")).hexdigest()


def ensure_folder_meta(abs_dir):
    created = 0
    cur = abs_dir
    while cur.startswith(W2_ROOT) and cur != W2_ROOT:
        meta = cur + ".meta"
        if not os.path.exists(meta):
            rel = os.path.relpath(cur, W2_ROOT).replace(os.sep, "/")
            open(meta, "w").write(FOLDER_META_TMPL.format(
                guid=det_guid("commonw2-folder", rel)))
            created += 1
        cur = os.path.dirname(cur)
    return created


def meta_guid(path):
    m = re.search(r"guid: ([0-9a-f]{32})", open(path, errors="ignore").read(300))
    return m.group(1) if m else None


# ---------------------------------------------------------------- 资产解析/创建

def scan_project_sos():
    """所有素材库 pseudo SO：assetPath basename(lower, 无扩展名) -> so info。"""
    idx = {}
    for lib in ("common01", "common02", "common03", "commonW1", "commonW2"):
        base = os.path.join(ASSETS, lib, "pseudo_prefab_so")
        if not os.path.isdir(base):
            continue
        for dp, _ds, fs in os.walk(base):
            for f in fs:
                if not f.endswith(".asset"):
                    continue
                p = os.path.join(dp, f)
                txt = open(p, errors="ignore").read()
                ap = re.search(r"assetPath: (.*)$", txt, re.M)
                if not ap:
                    continue
                apath = ap.group(1).strip()
                key = os.path.basename(apath).rsplit(".", 1)[0].lower()
                g = meta_guid(p + ".meta")
                if g and key not in idx:
                    idx[key] = {"path": p, "guid": g, "assetPath": apath}
    return idx


def find_wrapper_for_so(so_guid):
    """工程 prefabs 里 stub 引用该 SO 的 wrapper：返回 (path, guid, anchors)。"""
    pat = re.compile(r"pseudoPrefabSO: \{fileID: 11400000, guid: " + so_guid)
    for lib in ("common01", "common02", "common03", "commonW1", "commonW2"):
        base = os.path.join(ASSETS, lib, "prefabs")
        if not os.path.isdir(base):
            continue
        for dp, _ds, fs in os.walk(base):
            for f in fs:
                if not f.endswith(".prefab"):
                    continue
                p = os.path.join(dp, f)
                txt = open(p, errors="ignore").read()
                if not pat.search(txt):
                    continue
                g = meta_guid(p + ".meta")
                anchors = parse_anchors(txt)
                if g and anchors:
                    return {"path": p, "guid": g, "anchors": anchors}
    return None


def parse_anchors(prefab_txt):
    go = re.search(r"m_RootGameObject: \{fileID: (\d+)\}", prefab_txt)
    if not go:
        return None
    go = int(go.group(1))
    go_doc = re.search(
        r"--- !u!1 &%d\nGameObject:.*?\n(  m_Component:\n(?:  - component: \{fileID: \d+\}\n)+)" % go,
        prefab_txt, re.S)
    if not go_doc:
        return None
    fids = [int(x) for x in re.findall(r"fileID: (\d+)", go_doc.group(1))]
    tr = stub = None
    for seg in re.finditer(r"--- !u!(\d+) &(\d+)\n", prefab_txt):
        cls, fid = int(seg.group(1)), int(seg.group(2))
        if fid not in fids:
            continue
        if cls == 4:
            tr = fid
        elif cls == 114:
            body = prefab_txt[seg.end():seg.end() + 700]
            if "pseudoPrefabSO" in body:
                stub = fid
    if tr is None or stub is None:
        return None
    return {"go": go, "tr": tr, "stub": stub}


def resolve_assets(so_index):
    """每个资产 key（assetPath basename）-> {soGuid, prefabGuid, anchors, created[]}。"""
    out = {}
    created_files = []
    for miss_name, (dlc, bundle, apath) in MAPPING.items():
        key = os.path.basename(apath).rsplit(".", 1)[0]
        if key in out:
            out[miss_name] = out[key + "#RES"]  # 同资产不同 miss 名复用
            continue
        so = so_index.get(key.lower())
        created = []
        if so is None:
            so_path = os.path.join(W2_ROOT, "pseudo_prefab_so", dlc, "art",
                                   theme_dir(dlc), key + ".asset")
            os.makedirs(os.path.dirname(so_path), exist_ok=True)
            g = det_guid("pseudo", key)
            open(so_path, "w").write(SO_TMPL.format(
                so_script=SO_SCRIPT, name=key, bundle=bundle, path=apath))
            open(so_path + ".meta", "w").write(META_TMPL.format(guid=g))
            created += [so_path, so_path + ".meta"]
            so = {"path": so_path, "guid": g, "assetPath": apath}
        wrap = find_wrapper_for_so(so["guid"])
        if wrap is None:
            wrap_path = os.path.join(W2_ROOT, "prefabs", dlc, "art",
                                     theme_dir(dlc), key + ".prefab")
            os.makedirs(os.path.dirname(wrap_path), exist_ok=True)
            wg = det_guid("prefab", key)
            open(wrap_path, "w").write(WRAPPER_TMPL.format(
                name=key, go=SEED["go"], tr=SEED["tr"], stub=SEED["stub"],
                pseudo=SEED["pseudo"], soguid=so["guid"],
                stub_script=STUB_SCRIPT, pseudo_script=PSEUDO_SCRIPT))
            open(wrap_path + ".meta", "w").write(PREFAB_META_TMPL.format(guid=wg))
            created += [wrap_path, wrap_path + ".meta"]
            wrap = {"path": wrap_path, "guid": wg, "anchors": dict(SEED)}
        ensure_folder_meta(os.path.dirname(so["path"]))
        ensure_folder_meta(os.path.dirname(wrap["path"]))
        created_files += created
        info = {"key": key, "bundle": bundle, "assetPath": apath,
                "soGuid": so["guid"], "soPath": so["path"],
                "prefabGuid": wrap["guid"], "prefabPath": wrap["path"],
                "anchors": wrap["anchors"], "created": created}
        out[key] = info
        out[key + "#RES"] = info
        out[miss_name] = info
    return out, created_files


# ---------------------------------------------------------------- 场景追加

def walk_source(sc):
    """镜像 gen-scenes 的遍历：返回 [(base_name, tr_doc)]（Design/Art 子树叶子）。"""
    found = []
    UTENSIL_SKIP = ("collision", "killplanes")
    ART_SKIP = ("lights", "ground", "mesh baker", "mesh bakers",
                "spotlights", "light probes")

    def walk(tr, top):
        for c in sc.children.get(tr, []):
            go = sc.tr2go.get(c)
            nm = base_name(sc.go_name(go) or "") or ""
            if not sc.children.get(c):
                found.append((nm, sc.docs[c], top))
            else:
                walk(c, top)

    design = sc.find_root("Design")
    if design:
        for g in sc.children.get(design, []):
            gname = (sc.tr_name(g) or "").lower()
            if gname in UTENSIL_SKIP:
                continue
            walk(g, "design")
    art = sc.find_root("Art")
    if art:
        for g in sc.children.get(art, []):
            gname = (sc.tr_name(g) or "").lower()
            if gname in ART_SKIP:
                continue
            walk(g, "art")
    for r in sc.roots:
        if (sc.tr_name(r) or "").lower() == "scenery":
            walk(r, "art")
    return found


def fnum(v):
    f = float(v)
    return repr(f) if f != int(f) else str(int(f))


def mod_line(target, pguid, path, value, objref=None):
    objref = objref or "{fileID: 0}"
    return (f"    - target: {{fileID: {target}, guid: {pguid}, type: 2}}\n"
            f"      propertyPath: {path}\n"
            f"      value: {value}\n"
            f"      objectReference: {objref}")


def append_scene(scene_path, items, report):
    """items: [(name, pguid, anchors, pos, rot, scale)]。返回追加数。"""
    txt = open(scene_path).read()
    # 已有名（幂等）
    existing_names = set(re.findall(r"propertyPath: m_Name\n\s*value: (.*)", txt))
    # Scenery transform fid：GO 名 Scenery -> transform 组件
    m = re.search(r"m_Name: Scenery\n", txt)
    if not m:
        report["no_scenery"].append(scene_path)
        return 0
    head = txt.rfind("--- !u!1 &", 0, m.start())
    go_seg = txt[head:m.end()]
    go_fid = int(re.search(r"--- !u!1 &(\d+)", go_seg).group(1))
    tr_fid = None
    for cm in re.finditer(r"component: \{fileID: (\d+)\}", go_seg):
        fid = int(cm.group(1))
        d = re.search(r"--- !u!4 &%d\nTransform:" % fid, txt)
        if d:
            tr_fid = fid
            break
    if tr_fid is None:
        report["no_scenery"].append(scene_path)
        return 0
    # 现有 children 数
    tr_doc = re.search(r"--- !u!4 &%d\nTransform:\n(.*?)(?=\n--- !u!|\Z)" % tr_fid, txt, re.S)
    body = tr_doc.group(1)
    children = re.findall(r"- \{fileID: (\d+)\}", body.split("m_Children:")[1].split("m_Father:")[0]) \
        if "m_Children:" in body else []
    order = len(children)
    # 最大 fileID
    max_fid = max(int(x) for x in re.findall(r"^--- !u!\d+ &(-?\d+)", txt, re.M))
    added = []
    for name, pguid, anchors, pos, rot, scale in items:
        if name in existing_names:
            continue
        iid = max_fid + 1
        tid = max_fid + 2
        max_fid += 2
        mods = [
            mod_line(anchors["tr"], pguid, "m_LocalPosition.x", fnum(pos[0])),
            mod_line(anchors["tr"], pguid, "m_LocalPosition.y", fnum(pos[1])),
            mod_line(anchors["tr"], pguid, "m_LocalPosition.z", fnum(pos[2])),
            mod_line(anchors["tr"], pguid, "m_LocalRotation.x", fnum(rot[0])),
            mod_line(anchors["tr"], pguid, "m_LocalRotation.y", fnum(rot[1])),
            mod_line(anchors["tr"], pguid, "m_LocalRotation.z", fnum(rot[2])),
            mod_line(anchors["tr"], pguid, "m_LocalRotation.w", fnum(rot[3])),
            mod_line(anchors["tr"], pguid, "m_RootOrder", str(order)),
            mod_line(anchors["go"], pguid, "m_Name", name),
        ]
        if tuple(map(str, scale)) != ("1", "1", "1"):
            for i, ax in enumerate("xyz"):
                mods.append(mod_line(anchors["tr"], pguid, f"m_LocalScale.{ax}", fnum(scale[i])))
        doc = "\n".join([
            f"--- !u!1001 &{iid}", "Prefab:",
            "  m_ObjectHideFlags: 0", "  serializedVersion: 2",
            "  m_Modification:", f"    m_TransformParent: {{fileID: {tr_fid}}}",
            "    m_Modifications:", *mods, "    m_RemovedComponents: []",
            f"  m_ParentPrefab: {{fileID: 100100000, guid: {pguid}, type: 2}}",
            "  m_IsPrefabParent: 0",
            f"--- !u!4 &{tid} stripped", "Transform:",
            f"  m_PrefabParentObject: {{fileID: {anchors['tr']}, guid: {pguid}, type: 2}}",
            f"  m_PrefabInternal: {{fileID: {iid}}}"])
        added.append((tid, doc))
        order += 1
    if not added:
        return 0
    # Scenery transform m_Children 追加
    if children:
        old_block = body[body.index("m_Children:"):body.index("m_Father:")]
        new_block = old_block + "".join(f"  - {{fileID: {tid}}}\n" for tid, _ in added)
        new_body = body.replace(old_block, new_block)
    else:
        new_body = body.replace(
            "  m_Children: []",
            "  m_Children:\n" + "".join(f"  - {{fileID: {tid}}}\n" for tid, _ in added))
    txt = txt[:tr_doc.start(1)] + new_body + txt[tr_doc.end(1):]
    txt = txt.rstrip("\n") + "\n" + "\n".join(d for _, d in added) + "\n"
    open(scene_path, "w").write(txt)
    return len(added)


def main():
    levels = json.load(open(LEVELS_JSON))["levels"]
    rep = json.load(open(REPORT_JSON))
    miss_by_level = {r["id"]: [m.strip("'\" ") for m in r.get("misses", [])]
                     for r in rep}

    print("[1/4] 解析资产映射 …")
    so_index = scan_project_sos()
    assets, created = resolve_assets(so_index)
    print("      资产:", len({id(v) for v in assets.values()}), "新建文件:", len(created))

    print("[2/4] 源场景拾取漏放实例 …")
    report = {"no_scenery": [], "levels": {}, "created_files": created,
              "ancestry_warn": []}
    placed_total = 0
    for lv in levels:
        lid = lv["id"]
        want = {n for n in miss_by_level.get(lid, []) if n in MAPPING}
        if not want:
            continue
        sp = os.path.normpath(os.path.join(OC2_EDITOR, lv["sourceScene"]))
        if not os.path.exists(sp):
            sp = os.path.normpath(os.path.join(
                RIPPER_FALLBACK, lv["sourceScene"].replace("../", "")))
        if not os.path.exists(sp):
            report["levels"][lid] = {"error": "source scene not found"}
            continue
        sc = Scene(sp)
        items = []
        for nm, tr_doc, top in walk_source(sc):
            if nm not in want:
                continue
            info = assets.get(nm)
            if not info:
                continue
            pos = tr_doc.field_vec3("m_LocalPosition") or (0, 0, 0)
            rot = tr_doc.field_quat("m_LocalRotation") or (0, 0, 0, 1)
            scale = tr_doc.field_vec3("m_LocalScale") or (1, 1, 1)
            full_name = sc.go_name(sc.tr2go.get(tr_doc.fid))
            items.append((full_name, info["prefabGuid"], info["anchors"],
                          pos, rot, scale))
        if not items:
            continue
        scene_path = os.path.join(SET_DIR, "scenes",
                                  "s_%s.unity" % lid.lower())
        n = append_scene(scene_path, items, report)
        placed_total += n
        report["levels"][lid] = {"wanted": sorted(want), "placed": n}

    print("[3/4] 完成：追加实例", placed_total, "涉及关卡", len(report["levels"]))
    print("[4/4] 写报告 …")
    json.dump(report, open(os.path.join(OUT_DIR, "floor-supplement-report.json"), "w"),
              ensure_ascii=False, indent=1)
    if report["no_scenery"]:
        print("  ⚠ 无 Scenery 节点:", report["no_scenery"])


if __name__ == "__main__":
    main()
