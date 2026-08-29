#!/usr/bin/env python3
"""
scan-levels.py — 扫描 OC2 全部 DLC 合作关卡，生成 levels.json 清单。

数据源：
  - dump_bundle 中各 DLC 的 *coopgamescenedirectory.json（关卡列表、人数变体、星级线）
  - dump_bundle/manifest.json（container -> bundle/file，配置名兜底匹配）
  - AssetRipper 导出工程（场景 .unity 实际存在性 = 地面真值）
  - Assets/StreamingAssets/Windows/bundle*（UnityPy 建 path_id->container 映射，解析 LevelConfig 引用）

目录选择：优先各 DLC 自带目录（bundle 内引用可精确解析）；
dlc10/dlc13 自带目录数据陈旧、dlc11 自带目录 4p 有误列，这三者用 combineddlc 目录 + 名称兜底。

排除：ThroneRoom / StartScreen / WorldMap / *_vs / s_battlements_*（守城）。

输出：layout-editor/scripts/oc2-import/out/levels.json

用法：python3 layout-editor/scripts/oc2-import/scan-levels.py
"""
import json
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OC2_EDITOR = os.path.abspath(os.path.join(ROOT, ".."))
DUMP = os.path.join(ROOT, "dump_bundle")
BUNDLES = os.path.join(ROOT, "Assets", "StreamingAssets", "Windows")
RIPPER = os.path.join(OC2_EDITOR, "AssetRipper_export_20260728_091744", "ExportedProject")
OUT_DIR = os.path.join(ROOT, "layout-editor", "scripts", "oc2-import", "out")
CACHE_DIR = os.path.join(ROOT, "layout-editor", "scripts", "oc2-import", ".cache")

# DLC -> (目录所在 bundle, 目录 dump JSON 相对路径)
DIRECTORIES = {
    "dlc02": ("bundle161", "Assets/downloadablecontent/dlc02/dlc_assets/data/dlc02_coopgamescenedirectory.json"),
    "dlc03": ("bundle208", "Assets/downloadablecontent/dlc03/dlc_assets/data/dlc03_coopgamescenedirectory.json"),
    "dlc04": ("bundle224", "Assets/downloadablecontent/dlc04/dlc_assets/data/dlc04_coopgamescenedirectory.json"),
    "dlc05": ("bundle247", "Assets/downloadablecontent/dlc05/dlc_assets/data/dlc05_coopgamescenedirectory.json"),
    "dlc07": ("bundle293", "Assets/downloadablecontent/dlc07/dlc_assets/data/dlc07_coopgamescenedirectory.json"),
    "dlc08": ("bundle354", "Assets/downloadablecontent/dlc08/dlc_assets/data/dlc08_coopgamescenedirectory.json"),
    "dlc09": ("bundle404", "Assets/downloadablecontent/dlc09/dlc_assets/data/dlc09_coopgamescenedirectory.json"),
    "dlc10": ("bundle222", "Assets/downloadablecontent/combineddlc/dlc_assets/data/combineddlc_coopgamescenedirectory.json"),
    "dlc11": ("bundle222", "Assets/downloadablecontent/combineddlc/dlc_assets/data/combineddlc_coopgamescenedirectory.json"),
    "dlc13": ("bundle222", "Assets/downloadablecontent/combineddlc/dlc_assets/data/combineddlc_coopgamescenedirectory.json"),
}

# DLC -> scene name 前缀（从 combineddlc 混合条目里筛出本 DLC；隐藏关单独列出）
DLC_SCENE_PREFIX = {
    "dlc02": ["s_beach_", "s_resort_", "s_city_h9"],
    "dlc03": ["s_wonderland_"],
    "dlc04": ["s_chinatown_"],
    "dlc05": ["s_campsite_", "s_treehouse_", "s_city_h1"],
    "dlc07": ["s_courtyard_", "s_keep_", "s_city_h13", "s_city_h14", "s_city_h15"],
    "dlc08": ["s_day_", "s_inside_", "s_night_", "s_city_h16", "s_city_h17", "s_city_h18"],
    "dlc09": ["s_festivemashup_"],
    "dlc10": ["s_lunar_"],
    "dlc11": ["s_summer_"],
    "dlc13": ["s_moonfestival_"],
}

EXCLUDE_PAT = re.compile(r"(throneroom|startscreen|worldmap|_vs|battlements)", re.I)


def load_pidmap(bundle):
    """path_id -> container（带磁盘缓存）。"""
    os.makedirs(CACHE_DIR, exist_ok=True)
    cache = os.path.join(CACHE_DIR, f"pidmap_{bundle}.json")
    if os.path.exists(cache):
        with open(cache, "r", encoding="utf-8") as f:
            return {int(k): v for k, v in json.load(f).items()}
    import UnityPy  # 延迟导入
    env = UnityPy.load(os.path.join(BUNDLES, bundle))
    m = {o.path_id: o.container for o in env.objects if o.container}
    with open(cache, "w", encoding="utf-8") as f:
        json.dump(m, f)
    return m


def ripper_scenes():
    """AssetRipper 导出中的场景名(小写) -> .unity 路径。"""
    out = {}
    for dirpath, _dirs, files in os.walk(os.path.join(RIPPER, "Assets")):
        for n in files:
            if n.lower().endswith(".unity"):
                out[os.path.splitext(n)[0].lower()] = os.path.join(dirpath, n)
    return out


def build_config_index():
    """(dlc, 配置 basename 小写) -> dump JSON 相对路径（名称兜底匹配用）。"""
    with open(os.path.join(DUMP, "manifest.json"), "r", encoding="utf-8") as f:
        manifest = json.load(f)
    idx = {}
    for o in manifest["objects"]:
        c = o["container"]
        if "levelconfigs" not in c:
            continue
        if o["type"] not in ("CampaignLevelConfig", "ScriptedCampaignLevelConfig", "HordeLevelConfig"):
            continue
        m = re.search(r"/(dlc\d{2})/", c)
        if not m:
            continue
        base = os.path.basename(c).replace(".asset", "").lower()
        idx.setdefault((m.group(1), base), o["file"])
    return idx


def main():
    scenes_fs = ripper_scenes()
    print(f"AssetRipper 场景总数: {len(scenes_fs)}")
    cfg_index = build_config_index()

    levels = []
    problems = []

    for dlc in sorted(DLC_SCENE_PREFIX):
        bundle, rel = DIRECTORIES[dlc]
        pidmap = load_pidmap(bundle)
        with open(os.path.join(DUMP, rel), "r", encoding="utf-8") as f:
            directory = json.load(f)["MonoBehaviour"]

        prefixes = DLC_SCENE_PREFIX[dlc]
        dlc_levels = []
        for entry in directory["Scenes"]:
            label = entry.get("Label", "")
            variants = entry.get("SceneVarients", [])
            # 主场景名 = 属于本 DLC 前缀的那个（dlc11 旧目录曾把 4p 错列成 s_Inside_1_3）
            names = [v["SceneName"] for v in variants]
            cand = [n for n in names if any(n.lower().startswith(p) for p in prefixes)]
            if not cand:
                continue
            scene = cand[0]
            sl = scene.lower()
            if EXCLUDE_PAT.search(sl):
                continue
            if sl not in scenes_fs:
                problems.append(f"{dlc} {label}: AssetRipper 中不存在场景 {scene}")
                continue

            per_count = {}
            for v in variants:
                pc = str(v["PlayerCount"])
                # 1) 同 bundle pid 精确解析
                container = pidmap.get(v["LevelConfig"]["fileID"])
                if container and f"/{dlc}/" not in container and "combineddlc" not in container:
                    container = None  # 解析到了别的 DLC（陈旧条目）
                # 2) 名称兜底：<场景名去s_>_<n>p[ horde]
                if not container:
                    base = sl.replace("s_", "", 1)
                    for cand_base in (f"{base}_{pc}p", f"{base}_{pc}p horde"):
                        hit = cfg_index.get((dlc, cand_base))
                        if hit:
                            container = hit[len("Assets/"):].lower() if False else None
                            cfg_json = os.path.join(DUMP, hit)
                            container = hit  # file 字段即 dump 相对路径
                            break
                    else:
                        cfg_json = None
                    if cfg_json:
                        per_count[pc] = {"config": os.path.relpath(cfg_json, ROOT)}
                        star = v["m_PCStarBoundaries"]
                        per_count[pc]["stars"] = {
                            "one": star["m_OneStarScore"], "two": star["m_TwoStarScore"],
                            "three": star["m_ThreeStarScore"], "four": star["m_FourStarScore"],
                        }
                        continue
                    problems.append(f"{dlc} {scene} {pc}p: 配置无法解析")
                    continue
                # pid 解析成功 -> container 转 dump JSON 路径
                cfg_rel = "Assets/" + container.replace("assets/", "", 1)
                cfg_json = os.path.splitext(os.path.join(DUMP, cfg_rel))[0] + ".json"
                if not os.path.exists(cfg_json):
                    problems.append(f"{dlc} {scene} {pc}p: 缺 dump 文件 {cfg_rel}")
                    continue
                star = v["m_PCStarBoundaries"]
                per_count[pc] = {
                    "config": os.path.relpath(cfg_json, ROOT),
                    "stars": {
                        "one": star["m_OneStarScore"], "two": star["m_TwoStarScore"],
                        "three": star["m_ThreeStarScore"], "four": star["m_FourStarScore"],
                    },
                }
            if sorted(per_count) != ["1", "2", "3", "4"]:
                problems.append(f"{dlc} {scene}: 人数变体不全 {sorted(per_count)}")

            m = re.search(r"Level(\d+)$", label)
            order = int(m.group(1)) if m else 999
            hidden = "Hidden" in label

            m2 = re.search(r"_(\d+)_(\d+)$", sl)
            m3 = re.search(r"_h(\d+)$", sl)
            if m3:
                num = f"H{m3.group(1)}"
            elif m2:
                num = f"{m2.group(1)}_{m2.group(2)}"
            elif sl.endswith("_special"):
                num = "SP"
            else:
                num = f"{order:02d}"

            theme = sl.split("_")[1] if "_" in sl else sl
            dlc_levels.append({
                "id": f"OC2_{dlc.upper()}_{num}",
                "dlc": dlc,
                "label": label,
                "world": entry.get("World", -1),
                "order": order,
                "hidden": hidden,
                "sceneName": scene,
                "editorSceneName": f"s_oc2_{dlc}_{num.lower()}",
                "sourceScene": os.path.relpath(scenes_fs[sl], ROOT),
                "theme": theme,
                "variants": per_count,
            })

        dlc_levels.sort(key=lambda x: (x["hidden"], x["order"]))
        levels.extend(dlc_levels)
        print(f"{dlc}: {len(dlc_levels)} 关")

    os.makedirs(OUT_DIR, exist_ok=True)
    out = os.path.join(OUT_DIR, "levels.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump({"count": len(levels), "levels": levels}, f, ensure_ascii=False, indent=1)
    print(f"\n总计 {len(levels)} 关 -> {out}")

    if problems:
        print("\n=== 问题 ===")
        for p in problems:
            print(" -", p)


if __name__ == "__main__":
    main()
