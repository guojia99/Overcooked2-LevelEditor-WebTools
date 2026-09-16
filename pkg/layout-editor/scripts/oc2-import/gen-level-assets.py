#!/usr/bin/env python3
"""
gen-level-assets.py — 生成 oc2_dlc_story 的 data/ 参数资产（LevelInfo + config_1p~4p）。

每个关卡目录：
  Assets/LevelSets/oc2_dlc_story/data/<LEVEL_ID>/
    LevelInfo_<LEVEL_ID>.asset     （菜谱/音频/配置引用/依赖）
    config_1p.asset ~ config_4p.asset
    （截图：DLC 无 capture，screenshot 置 {fileID: 0}）

数据来源：
  - out/levels.json（scan-levels.py）
  - dump_bundle 配置 JSON（orderLifeTime/timeBetweenOrders/plateReturnTime/m_rounds/星级线）
  - bundle pidmap（config -> RecipeList -> order node 容器名 -> 编辑器菜谱 SO guid）
  - AssetRipper 源场景（m_inLevelAmbiences 原样拷贝）
  - audio-catalog.json（必备目录 + 每 DLC 目录 + DLC_XX_Generic 音乐）

用法：
  python3 gen-level-assets.py                # 全部关卡
  python3 gen-level-assets.py OC2_DLC02_1_1  # 指定关卡
"""
import json
import os
import re
import sys
from importlib.machinery import SourceFileLoader

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from oc2_common import (ROOT, ASSETS, WEB_PUBLIC, EditorIndex, Scene, map_item,
                        base_name, meta_guid, deterministic_guid)

SCAN = SourceFileLoader("scan_levels", os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "scan-levels.py")).load_module()
DUMP = os.path.join(ROOT, "dump_bundle")
OUT_SETS = os.path.join(ASSETS, "LevelSets", "oc2_dlc_story")

GUID_LEVEL_INFO = "9613355741a1a7e429f1ad97a816f6de"      # LevelInfoSO
GUID_LEVEL_CONFIG = "29722fa34ea4be545b2b4160dc3b3f12"   # LevelConfigSetupPerPlayerCountSO

_audio_catalog = None
_pidmaps = {}
_recipe_index = None


def audio_catalog():
    global _audio_catalog
    if _audio_catalog is None:
        with open(os.path.join(WEB_PUBLIC, "audio-catalog.json"), "r", encoding="utf-8") as f:
            _audio_catalog = json.load(f)
    return _audio_catalog


def pidmap(bundle):
    if bundle not in _pidmaps:
        _pidmaps[bundle] = SCAN.load_pidmap(bundle)
    return _pidmaps[bundle]


def recipe_index():
    """order node 容器 basename(小写) -> 编辑器菜谱 SO {guid,bundleName}。"""
    global _recipe_index
    if _recipe_index is not None:
        return _recipe_index
    idx = EditorIndex()
    _recipe_index = {}
    for info in idx.so_by_guid.values():
        ap = info["assetPath"]
        if "/orderdefinitions/" not in ap.replace("\\", "/").lower():
            continue
        base = os.path.splitext(os.path.basename(ap))[0].lower()
        _recipe_index[base] = info
    return _recipe_index


def resolve_recipes(config_json, dlc):
    """config dump JSON -> (菜谱 guid 列表, 涉及 bundle 集合, 警告列表)。
    支持 Campaign（m_rounds[].m_recipes）与 Horde（m_waves.m_waves[].m_recipes）。"""
    with open(os.path.join(ROOT, config_json), "r", encoding="utf-8") as f:
        cfg = json.load(f)["MonoBehaviour"]
    warns = []
    bundles = set()
    guids = []
    ridx = recipe_index()
    # 收集全部 RecipeList 引用 pid（Campaign: m_rounds；Scripted/special: m_data；Horde: m_waves）
    rl_pids = []
    for rnd in cfg.get("m_rounds") or []:
        pid = rnd.get("m_recipes", {}).get("fileID", 0)
        if pid:
            rl_pids.append(pid)
    data = cfg.get("m_data")
    if isinstance(data, dict):
        pid = data.get("m_recipes", {}).get("fileID", 0)
        if pid:
            rl_pids.append(pid)
        for ph in data.get("Phases", []):
            pid = ph.get("Recipes", {}).get("fileID", 0)
            if pid:
                rl_pids.append(pid)
    waves = cfg.get("m_waves", {})
    if isinstance(waves, dict):
        for w in waves.get("m_waves", []):
            pid = w.get("m_recipes", {}).get("fileID", 0)
            if pid:
                rl_pids.append(pid)
    search_rl = candidate_bundles(dlc) + [b for b in content_bundles("recipelists")
                                          if b not in candidate_bundles(dlc)]
    search_orders_extra = content_bundles("orderdefinitions")
    seen_lists = set()
    for pid in rl_pids:
        rl_container = None
        rl_bundle = None
        for b in search_rl:
            c = pidmap(b).get(pid)
            if c and re.search(r"recipes?lists?", c, re.I):
                rl_container, rl_bundle = c, b
                break
        if not rl_container:
            warns.append(f"RecipeList pid {pid} 未解析")
            continue
        if rl_container in seen_lists:
            continue
        seen_lists.add(rl_container)
        rel = "Assets/" + rl_container.replace("assets/", "", 1)
        rl_json = os.path.splitext(os.path.join(DUMP, rel))[0] + ".json"
        if not os.path.exists(rl_json):
            warns.append(f"缺 RecipeList dump {rel}")
            continue
        rl = json.load(open(rl_json, "r", encoding="utf-8"))["MonoBehaviour"]
        order_search = [rl_bundle] + [b for b in search_orders_extra if b != rl_bundle]
        for entry in rl.get("m_recipes", []):
            opid = entry.get("m_order", {}).get("fileID", 0)
            oc = None
            for b in order_search:
                oc = pidmap(b).get(opid)
                if oc:
                    break
            if not oc:
                warns.append(f"order pid {opid} 未解析")
                continue
            base = os.path.splitext(os.path.basename(oc))[0].lower()
            r = ridx.get(base)
            if not r:
                warns.append(f"菜谱 {base} 无编辑器 SO")
                continue
            if r["guid"] not in guids:
                guids.append(r["guid"])
                bundles.add(r["bundleName"])
    return guids, bundles, warns


_bundle_cache = {}
_content_bundles = None


def content_bundles(kind):
    """manifest 中含特定内容目录的 bundle 列表。kind: 'recipelists' | 'orderdefinitions'。
    兼容游戏中的拼写变体：recipelist / recipelists / recipeslists。"""
    global _content_bundles
    if _content_bundles is None:
        with open(os.path.join(DUMP, "manifest.json"), "r", encoding="utf-8") as f:
            m = json.load(f)
        _content_bundles = {"recipelists": [], "orderdefinitions": []}
        rl_pat = re.compile(r"recipes?lists?", re.I)
        for o in m["objects"]:
            c = o["container"]
            if rl_pat.search(c) and o["bundle"] not in _content_bundles["recipelists"]:
                _content_bundles["recipelists"].append(o["bundle"])
            if ("orderdefinitions" in c or "recipeitems" in c) and \
                    o["bundle"] not in _content_bundles["orderdefinitions"]:
                _content_bundles["orderdefinitions"].append(o["bundle"])
    return _content_bundles[kind]


def candidate_bundles(dlc):
    """manifest 中所有包含该 DLC（或 combineddlc）container 的 bundle。"""
    if dlc in _bundle_cache:
        return _bundle_cache[dlc]
    with open(os.path.join(DUMP, "manifest.json"), "r", encoding="utf-8") as f:
        m = json.load(f)
    out = []
    for o in m["objects"]:
        c = o["container"]
        if f"/{dlc}/" in c or "/combineddlc/" in c:
            if o["bundle"] not in out:
                out.append(o["bundle"])
    _bundle_cache[dlc] = out
    return out


# 每 DLC/主题 的游戏内音乐（id 见 audio-catalog.json music 表；优先 common02 基名变体）
MUSIC_BY_DLC = {
    "dlc02": "DLC_02_Generic",
    "dlc03": "DLC_03_InGame_Music",
    "dlc04": "DLC_04_InGame_Music",
    "dlc05": "DLC_05_Level",
    "dlc09": "DLC_09_Fairground_Music",   # festivemashup 近似
    "dlc10": "DLC_10_InGame_Music",
    "dlc11": "DLC_11_Summer_Levels",
    "dlc13": "DLC_13_InGame_Music",
}
MUSIC_BY_THEME = {
    ("dlc07", "courtyard"): "DLC_07_Courtyard",
    ("dlc07", "keep"): "DLC_07_Keep",
    ("dlc07", "city"): "DLC_07_City",
    ("dlc08", "day"): "DLC_08_FairgroundDay_Theme",
    ("dlc08", "night"): "DLC_08_FairgroundNight_Theme",
    ("dlc08", "inside"): "DLC_08_BigTop_Theme",
    ("dlc08", "city"): "DLC_08_City_Theme",
}


def music_id_for(level):
    if level["id"] == "OC2_DLC02_H9":
        return "DLC_02_HiddenTrack"
    return MUSIC_BY_THEME.get((level["dlc"], level["theme"]),
                              MUSIC_BY_DLC.get(level["dlc"]))


def audio_for_level(level):
    """返回 (musicGuid, [dirGuid...], ambiencesHex, bundles, warns)。"""
    ac = audio_catalog()
    warns = []
    bundles = set()
    music = {x["id"]: x for x in ac["music"]}
    dirs = {x["id"]: x for x in ac["audioDirectories"]}
    # 音乐
    mid = music_id_for(level)
    mg = music.get(mid) if mid else None
    music_guid = None
    if mg:
        music_guid = mg["guid"]
        bundles.add(mg["bundleName"])
    else:
        warns.append(f"无音乐 SO {mid}")
    # 目录：必备 5 + DLCxxAudioDirectory(SO)
    dir_guids = []
    for did in ac["mandatoryDirectoryIds"]:
        x = dirs.get(did)
        if x and x["guid"] not in dir_guids:
            dir_guids.append(x["guid"])
            bundles.add(x["bundleName"])
    dlc_dir = f"DLC{level['dlc'][3:]}AudioDirectory"
    for cand in (dlc_dir + "SO", dlc_dir):
        x = dirs.get(cand)
        if x:
            if x["guid"] not in dir_guids:
                dir_guids.append(x["guid"])
                bundles.add(x["bundleName"])
            break
    else:
        warns.append(f"无音频目录 {dlc_dir}")
    # 环境音：源场景原样拷贝
    amb = ""
    src = os.path.join(ROOT, level["sourceScene"])
    txt = open(src, "r", encoding="utf-8").read()
    m = re.search(r"m_inLevelAmbiences: ([0-9a-f]+)", txt)
    if m:
        amb = m.group(1)
    else:
        warns.append("源场景无 m_inLevelAmbiences")
    return music_guid, dir_guids, amb, bundles, warns


def scene_item_bundles(level):
    """扫描源场景 Design/Art 物件，收集映射到的伪 SO 的 bundleName。"""
    idx = EditorIndex()
    scene = Scene(os.path.join(ROOT, level["sourceScene"]))
    bundles = set()
    design = scene.find_root("Design")
    art = scene.find_root("Art")
    stack = []
    for root, groups in ((design, ["Work Surfaces", "Gameplay Stations", "Cooking Stations",
                                   "Utensils", "Glasses", "Plates", "Mugs", "Trays",
                                   "Animated Objects", "Environment Objects"]),
                         (art, ["Scenery", "Animated Objects"])):
        if not root:
            continue
        for g in groups:
            sub = scene.find_child(root, g)
            if sub:
                stack.extend(scene.children.get(sub, []))
    while stack:
        tr = stack.pop()
        kind, b, *rest = map_item(scene, idx, tr)
        if kind == "place":
            so = idx.so_by_guid.get(rest[0]["soGuid"]) if rest[0].get("soGuid") else None
            if so and so.get("bundleName"):
                bundles.add(so["bundleName"])
        elif kind == "group":
            stack.extend(scene.children.get(tr, []))
    return bundles


CONFIG_TMPL = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {{fileID: 0}}
  m_PrefabInternal: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier: 
  orderLifeTime: {order_life}
  timeBetweenOrders: {time_between}
  plateReturnTime: {plate_return}
  survivalTimeMultiplier: 1
  roundTime: {round_time}
  m_OneStarScore: {one}
  m_TwoStarScore: {two}
  m_ThreeStarScore: {three}
  m_FourStarScore: {four}
"""

META_TMPL = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def write_asset(path, content, guid):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)
    with open(path + ".meta", "w", encoding="utf-8") as f:
        f.write(META_TMPL.format(guid=guid))


def fmt_num(v):
    """Unity YAML 数字：整数值写整数，小数原样。"""
    if isinstance(v, float) and v == int(v):
        return str(int(v))
    return repr(v) if isinstance(v, float) else str(v)


def gen_level(level, dry=False):
    lid = level["id"]
    warns = []
    deps = set()
    data_dir = os.path.join(OUT_SETS, "data", lid)
    guids = {pc: deterministic_guid("oc2_dlc_story", f"{lid}/config_{pc}p.asset")
             for pc in ("1", "2", "3", "4")}
    info_guid = deterministic_guid("oc2_dlc_story", f"{lid}/LevelInfo_{lid}.asset")

    # ---- config_Xp ----
    cfgs = {}
    for pc in ("1", "2", "3", "4"):
        v = level["variants"][pc]
        with open(os.path.join(ROOT, v["config"]), "r", encoding="utf-8") as f:
            cfg = json.load(f)["MonoBehaviour"]
        round_time = sum(r.get("m_roundTimer", 0) for r in cfg.get("m_rounds") or [])
        data = cfg.get("m_data")
        if not round_time and isinstance(data, dict):
            round_time = data.get("m_roundTimer", 0)
        s = v["stars"]
        cfgs[pc] = CONFIG_TMPL.format(
            script_guid=GUID_LEVEL_CONFIG, name=f"config_{pc}p",
            order_life=fmt_num(cfg.get("m_orderLifetime", 100)),
            time_between=fmt_num(cfg.get("m_timeBetweenOrders", 10)),
            plate_return=fmt_num(cfg.get("m_plateReturnTime", 7)),
            round_time=fmt_num(round_time),
            one=s["one"], two=s["two"], three=s["three"], four=s["four"])

    # ---- 菜谱（以 2p 配置为准）----
    recipes, rbundles, rwarns = resolve_recipes(level["variants"]["2"]["config"], level["dlc"])
    warns += rwarns
    deps |= rbundles

    # ---- 音频 ----
    music_guid, dir_guids, amb_hex, abundles, awarns = audio_for_level(level)
    warns += awarns
    deps |= abundles

    # ---- 场景物件涉及的 bundle ----
    deps |= scene_item_bundles(level)

    # ---- disableDynamicParenting（2p 配置）----
    with open(os.path.join(ROOT, level["variants"]["2"]["config"]), "r", encoding="utf-8") as f:
        ddp = json.load(f)["MonoBehaviour"].get("m_disableDynamicParenting", True)

    num = "_".join(lid.split("_")[2:])
    level_name = num.replace("_", "-") if not num.startswith("H") else num
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_PrefabParentObject: {fileID: 0}",
        "  m_PrefabInternal: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {GUID_LEVEL_INFO}, type: 3}}",
        f"  m_Name: LevelInfo_{lid}",
        "  m_EditorClassIdentifier: ",
        f"  levelName: {level_name}",
        f"  levelNameZH: {level_name}",
        "  screenshot: {fileID: 0}",
        f"  sceneName: {level['editorSceneName']}",
        "  recipes:",
    ]
    for g in recipes:
        lines.append(f"  - {{fileID: 11400000, guid: {g}, type: 2}}")
    lines += [
        "  debugRecipeCount: 0",
        "  excludeStoryRecipeMatchList: 0",
        "  includeRecipeMatchLists: []",
        "  allIngredients: []",
        "  optionalRecipeMatchListItems: []",
        "  allCookingSteps: []",
        f"  inLevelMusicSO: {{fileID: 11400000, guid: {music_guid or '0'}, type: 2}}" if music_guid
        else "  inLevelMusicSO: {fileID: 0}",
        f"  inLevelAmbiences: {amb_hex or ''}",
        "  audioDirectorySOs:",
    ]
    for g in dir_guids:
        lines.append(f"  - {{fileID: 11400000, guid: {g}, type: 2}}")
    lines += [
        f"  disableDynamicParenting: {1 if ddp else 0}",
        "  OnDeathEffectSO: {fileID: 0}",
        "  onDeathEffectSO: {fileID: 0}",
        f"  config_1p: {{fileID: 11400000, guid: {guids['1']}, type: 2}}",
        f"  config_2p: {{fileID: 11400000, guid: {guids['2']}, type: 2}}",
        f"  config_3p: {{fileID: 11400000, guid: {guids['3']}, type: 2}}",
        f"  config_4p: {{fileID: 11400000, guid: {guids['4']}, type: 2}}",
        "  dependencies:",
    ]
    for b in sorted(deps):
        lines.append(f"  - {b}")
    info_yaml = "\n".join(lines) + "\n"

    if not dry:
        for pc in ("1", "2", "3", "4"):
            write_asset(os.path.join(data_dir, f"config_{pc}p.asset"), cfgs[pc], guids[pc])
        write_asset(os.path.join(data_dir, f"LevelInfo_{lid}.asset"), info_yaml, info_guid)
    return {"id": lid, "recipes": len(recipes), "deps": sorted(deps),
            "warns": warns, "levelInfoGuid": info_guid}


def main():
    with open(os.path.join(ROOT, "layout-editor", "scripts", "oc2-import",
                           "out", "levels.json"), "r", encoding="utf-8") as f:
        levels = json.load(f)["levels"]
    only = set(sys.argv[1:])
    report = []
    for lv in levels:
        if only and lv["id"] not in only:
            continue
        r = gen_level(lv, dry="--dry" in sys.argv)
        report.append(r)
        w = (" | " + "; ".join(r["warns"])) if r["warns"] else ""
        print(f"{r['id']}: recipes={r['recipes']} deps={len(r['deps'])}{w}")
    out = os.path.join(ROOT, "layout-editor", "scripts", "oc2-import", "out",
                       "gen-assets-report.json")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=1)
    print(f"\n报告 -> {out}")


if __name__ == "__main__":
    main()
