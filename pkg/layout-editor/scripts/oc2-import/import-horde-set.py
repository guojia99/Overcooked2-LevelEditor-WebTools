#!/usr/bin/env python3
"""
import-horde-set.py — 新建 oc2_horde 关卡集：导入 dlc07 敌群地图（Battlements 01-08）。

背景：
  scan-levels.py 的 EXCLUDE_PAT 曾显式排除 s_battlements_*（守城敌群图），
  oc2_dlc_story 因此不含敌群地图。本脚本按同一套导入逻辑（gen-level-assets +
  gen-scenes + 目录星级线 + pidmap 配置解析）把 8 张敌群图导入全新关卡集
  oc2_horde，敌群玩法（波次/生命/修复）按普通关卡降级处理（与 oc2_dlc_story
  中 dlc09 敌群关同策略）。

数据链路（每关）：
  dump dlc07_coopgamescenedirectory（DLC07Battlements01..08 条目，1-4p 变体+星级线）
    → pidmap bundle293 解析 LevelConfig → dump battlements_0N_Xp.json（HordeLevelConfig）
    → gen-level-assets.gen_level：config_1p~4p + LevelInfo（菜谱走 m_waves 解析，
      音乐 DLC_07_Battlements，音频目录 DLC07）
    → gen-scenes.build_scene：HordeGameEnvironment 根 + Design/Art 同构映射
    → 截图：bundle294 dlc_07_battlements0N Sprite（UnityPy+PIL）
    → LevelSetInfo.asset（levelInfos 按序登记 8 关）

输出：Assets/LevelSets/oc2_horde/{data,scenes}（确定性 guid 前缀 "oc2_horde"）
报告：out/horde-import-report.json
用法：python3 layout-editor/scripts/oc2-import/import-horde-set.py [--dry]
"""
import hashlib
import json
import os
import re
import sys
from importlib.machinery import SourceFileLoader

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from oc2_common import ROOT, ASSETS, RIPPER, deterministic_guid  # noqa: E402

gla = SourceFileLoader("gen_level_assets", os.path.join(HERE, "gen-level-assets.py")).load_module()
gens = SourceFileLoader("gen_scenes", os.path.join(HERE, "gen-scenes.py")).load_module()
scan = SourceFileLoader("scan_levels", os.path.join(HERE, "scan-levels.py")).load_module()

SET_NAME = "oc2_horde"
SET_DIR = os.path.join(ASSETS, "LevelSets", SET_NAME)
DATA_DIR = os.path.join(SET_DIR, "data")
SCENES_DIR = os.path.join(SET_DIR, "scenes")
OUT_DIR = os.path.join(HERE, "out")
DUMP = os.path.join(ROOT, "dump_bundle")

SET_UID = "8c1f4a2e-6b7d-4e35-9f0a-51d2c7e4b6a3"
SET_NAME_EN = "Overcooked 2 - Horde"
SET_NAME_ZH = "胡闹厨房 2 - 敌群模式"
GUID_LEVEL_SET_INFO = "f6eb6fcdbbf220346afc607651d1ab93"   # LevelSetInfoSO

DLC = "dlc07"
DIR_BUNDLE = "bundle293"        # dlc07 coopgamescenedirectory 所在 bundle
SCREENSHOT_BUNDLE = "bundle294" # dlc_07_battlements0N 预览图所在 bundle
DIRECTORY_JSON = os.path.join(
    DUMP, "Assets/downloadablecontent/dlc07/dlc_assets/data/dlc07_coopgamescenedirectory.json")

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: {bundle}
  assetBundleVariant: 
"""

SCENE_META = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: {bundle}
  assetBundleVariant: 
"""


def folder_guid(rel):
    return deterministic_guid(SET_NAME, "folder/" + (rel or "."))


def shot_guid(level_id):
    return hashlib.md5((SET_NAME + "-screenshot:" + level_id).encode()).hexdigest()


def build_levels():
    """dlc07 目录 Battlements 条目 → 管线 level dict 列表。"""
    pidmap = scan.load_pidmap(DIR_BUNDLE)
    directory = json.load(open(DIRECTORY_JSON, encoding="utf-8"))["MonoBehaviour"]
    levels = []
    for entry in directory["Scenes"]:
        label = entry.get("Label", "")
        m = re.search(r"Battlements(\d+)$", label)
        if not m:
            continue
        n = int(m.group(1))
        scene = "s_battlements_%02d" % n
        src = os.path.join(RIPPER, "Assets", "downloadablecontent", DLC,
                           "dlc_assets", "Scenes", "03_Battlements", scene + ".unity")
        if not os.path.exists(src):
            raise SystemExit("源场景缺失: %s" % src)
        variants = {}
        for v in entry.get("SceneVarients", []):
            pc = str(v["PlayerCount"])
            if v["SceneName"] != scene:
                continue
            cfg_pid = v["LevelConfig"]["fileID"]
            container = pidmap.get(cfg_pid)
            if not container or "battlements" not in container:
                # 名称兜底（pidmap 未命中时）
                rel = ("Assets/downloadablecontent/dlc07/dlc_assets/data/"
                       "levelconfigs/battlements_%02d_%sp.asset" % (n, pc))
            else:
                rel = "Assets/" + container.replace("assets/", "", 1)
            cfg_json = os.path.splitext(os.path.join(DUMP, rel))[0] + ".json"
            if not os.path.exists(cfg_json):
                raise SystemExit("配置 dump 缺失: %s" % cfg_json)
            star = v["m_PCStarBoundaries"]
            variants[pc] = {
                "config": os.path.relpath(cfg_json, ROOT),
                "stars": {"one": star["m_OneStarScore"], "two": star["m_TwoStarScore"],
                          "three": star["m_ThreeStarScore"], "four": star["m_FourStarScore"]},
            }
        if sorted(variants) != ["1", "2", "3", "4"]:
            raise SystemExit("%s 人数变体不全: %s" % (scene, sorted(variants)))
        lid = "OC2_DLC07_B%02d" % n
        levels.append({
            "id": lid,
            "dlc": DLC,
            "label": label,
            "world": entry.get("World", -1),
            "order": 100 + n,
            "hidden": False,
            "levelName": "B%d" % n,
            "sceneName": scene,
            "editorSceneName": "s_%s_b%02d" % (SET_NAME, n),
            "sourceScene": os.path.relpath(src, ROOT),
            "theme": "battlements",
            "variants": variants,
        })
    levels.sort(key=lambda x: x["order"])
    if len(levels) != 8:
        raise SystemExit("预期 8 张敌群图，实际 %d" % len(levels))
    return levels


def ensure_folders():
    for rel, bundle in (("", "%s/info_%s" % (SET_NAME, SET_NAME)),
                        ("data", ""), ("scenes", "")):
        d = os.path.join(SET_DIR, rel) if rel else SET_DIR
        os.makedirs(d, exist_ok=True)
        meta = d + ".meta"
        if not os.path.exists(meta):
            with open(meta, "w", encoding="utf-8") as f:
                f.write(FOLDER_META.format(guid=folder_guid(rel), bundle=bundle))


def write_set_info(info_guids):
    infos = "\n".join("  - {fileID: 11400000, guid: %s, type: 2}" % g
                      for g in info_guids)
    text = "\n".join([
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
        f"  m_Script: {{fileID: 11500000, guid: {GUID_LEVEL_SET_INFO}, type: 3}}",
        "  m_Name: LevelSetInfo",
        "  m_EditorClassIdentifier: ",
        f"  levelSetName: {SET_NAME_EN}",
        "  levelSetNameZH: " + json.dumps(SET_NAME_ZH),
        "  author: ",
        f"  uid: {SET_UID}",
        "  version: 1.0.0",
        "  levelInfos:",
        infos,
        "",
    ])
    p = os.path.join(DATA_DIR, "LevelSetInfo.asset")
    with open(p, "w", encoding="utf-8") as f:
        f.write(text)
    with open(p + ".meta", "w", encoding="utf-8") as f:
        f.write(FOLDER_META.format(guid=deterministic_guid(
            SET_NAME, "data/LevelSetInfo.asset"), bundle=""))
    return p


def extract_screenshots(levels, dry=False):
    """bundle294 dlc_07_battlements0N Sprite → screenshot.png + LevelInfo 回填。"""
    results = {"ok": [], "failed": [], "skipped": []}
    try:
        import UnityPy
        from PIL import Image
    except ImportError:
        print("  ! 缺 UnityPy/Pillow，跳过截图（可后补）")
        results["skipped"] = [lv["id"] for lv in levels]
        return results
    meta_template = os.path.join(
        ASSETS, "LevelSets", "oc1_story", "data", "OC1_Story_1_1", "screenshot.png.meta")
    if not os.path.exists(meta_template):
        results["skipped"] = [lv["id"] for lv in levels]
        return results
    env = UnityPy.load(os.path.join(ROOT, "Assets", "StreamingAssets", "Windows",
                                    SCREENSHOT_BUNDLE))
    want = {}
    for lv in levels:
        n = int(lv["id"].rsplit("B", 1)[1])
        want["DLC_07_Battlements%02d" % n] = lv
    for o in env.objects:
        if str(o.type) not in ("213", "28"):
            continue
        try:
            d = o.read()
        except Exception:
            continue
        name = getattr(d, "m_Name", "")
        if name not in want:
            continue
        lv = want.pop(name)
        try:
            img = d.image
        except Exception as e:
            results["failed"].append([lv["id"], "read image: %s" % e])
            continue
        if dry:
            results["ok"].append([lv["id"], list(img.size)])
            continue
        png = os.path.join(DATA_DIR, lv["id"], "screenshot.png")
        img.save(png)
        g = shot_guid(lv["id"])
        with open(png + ".meta", "w", encoding="utf-8") as f:
            f.write(re.sub(r"^guid: [0-9a-f]{32}", "guid: " + g,
                           open(meta_template).read(), count=1, flags=re.M))
        info_path = os.path.join(DATA_DIR, lv["id"], "LevelInfo_%s.asset" % lv["id"])
        txt = open(info_path).read()
        new = txt.replace("screenshot: {fileID: 0}",
                          "screenshot: {fileID: 21300000, guid: %s, type: 3}" % g)
        if new != txt:
            with open(info_path, "w", encoding="utf-8") as f:
                f.write(new)
        results["ok"].append([lv["id"], list(img.size)])
    for lid in want:
        results["failed"].append([lid, "sprite not found"])
    return results


def main():
    dry = "--dry" in sys.argv
    gla.set_override(SET_NAME)
    # gen-scenes 内部以同名模块持有同一实例；双保险断言
    if gens.gla is not gla:
        gens.gla.set_override(SET_NAME)

    print("[1/6] 解析 dlc07 目录 Battlements 条目 …")
    levels = build_levels()
    print("      %d 张: %s" % (len(levels), ", ".join(lv["id"] for lv in levels)))
    with open(os.path.join(OUT_DIR, "horde-levels.json"), "w", encoding="utf-8") as f:
        json.dump({"count": len(levels), "levels": levels}, f, ensure_ascii=False, indent=1)

    print("[2/6] 建目录结构 …")
    if not dry:
        ensure_folders()

    print("[3/6] 生成 data/（config + LevelInfo）…")
    report = []
    info_guids = []
    for lv in levels:
        r = gla.gen_level(lv, dry=dry)
        report.append(r)
        info_guids.append(r["levelInfoGuid"])
        w = (" | " + "; ".join(r["warns"])) if r["warns"] else ""
        print("  %s: recipes=%d deps=%d%s" % (r["id"], r["recipes"], len(r["deps"]), w))

    print("[4/6] 生成 scenes/ …")
    scenes_report = []
    for lv in levels:
        text, info, info_guid, item_bundles = gens.build_scene(lv)
        scene_name = lv["editorSceneName"]
        scenes_report.append({
            "id": lv["id"], "scene": scene_name,
            "levelInfoGuid": info_guid,
            "guid": deterministic_guid(SET_NAME, "scenes/%s.unity" % scene_name),
            "counters": len(info.counters), "utensils": len(info.utensils),
            "decor": len(info.decor), "walls": len(info.walls),
            "killplanes": len(info.killplanes), "players": len(info.players),
            "misses": sorted(set(info.misses)),
        })
        r = scenes_report[-1]
        print("  %s: counters=%d utensils=%d decor=%d walls=%d kp=%d players=%d misses=%d"
              % (r["id"], r["counters"], r["utensils"], r["decor"], r["walls"],
                 r["killplanes"], r["players"], len(r["misses"])))
        if r["misses"]:
            print("      misses: %s" % ", ".join(r["misses"][:12]))
        if dry:
            continue
        p = os.path.join(SCENES_DIR, scene_name + ".unity")
        with open(p, "w", encoding="utf-8") as f:
            f.write(text)
        with open(p + ".meta", "w", encoding="utf-8") as f:
            f.write(SCENE_META.format(guid=r["guid"], bundle="%s/%s" % (SET_NAME, scene_name)))
        gens.merge_level_info_deps(lv["id"], item_bundles)

    print("[5/6] 写 LevelSetInfo …")
    if not dry:
        p = write_set_info(info_guids)
        print("      -> %s (%d 关)" % (os.path.relpath(p, ROOT), len(info_guids)))

    print("[6/6] 截图 …")
    shots = extract_screenshots(levels, dry=dry)
    print("      ok=%d failed=%d skipped=%d"
          % (len(shots["ok"]), len(shots["failed"]), len(shots["skipped"])))
    for lid, why in shots["failed"]:
        print("      FAIL", lid, why)

    os.makedirs(OUT_DIR, exist_ok=True)
    out = os.path.join(OUT_DIR, "horde-import-report.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump({"levels": report, "scenes": scenes_report, "screenshots": shots},
                  f, ensure_ascii=False, indent=1)
    print("\n完成 -> %s" % out)


if __name__ == "__main__":
    main()
