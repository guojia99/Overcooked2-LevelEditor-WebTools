#!/usr/bin/env python3
"""gen-decor-entries.py — 从 dump_bundle/manifest.json 枚举 DLC 装饰物 ID 清单。

输出：layout-editor/scripts/data/decor-entries.json（按主题分组的装饰物条目，同时是
import-dlc-content.mjs 的 emitDecor 输入源 + 翻译用 ID 列表）。

规则（与 common-w-content-tier 技能一致）：
- 容器必须真实以 .prefab 结尾（排除 .fbx/.mat/.asset 等杂项）；
- 剔除 npc/chef/player/character 角色件、地面/水面/楼梯/碰撞/死亡面/UI 等系统件、
  dlc09_horde 整主题（仅剩食物/UI 件，非装饰）；
- Unity 重复导入判定：同一 (bundle, 主题子目录) 内同时存在 `<base>` 与 `<base> N` /
  `<base> (N)` 时，后者为重复件剔除；基名不存在时保留原名（如 p_dlc13_arch 1）；
- 与 common01/common02/common03 现有全部 prefab 名去重（含清洗后 id）；
- dlc05_city / dlc08_night 与主主题同源并入；跨 bundle 同名收敛到最先出现的主题。

用法：
  python3 layout-editor/scripts/gen-decor-entries.py [--dry]
"""
import json
import os
import re
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MANIFEST = os.path.join(REPO_ROOT, "dump_bundle", "manifest.json")
OUT = os.path.join(REPO_ROOT, "layout-editor", "scripts", "data", "decor-entries.json")

DRY = "--dry" in sys.argv

# (bundle, 主题子目录, art theme, dlc) —— theme 严格对齐现有 palette 分组
TARGETS = [
    ("bundle167", "beach theme", "dlc02_beach", "02"),
    ("bundle210", "wonderland theme", "dlc03_christmas", "03"),
    ("bundle226", "dressingassets", "dlc04", "04"),
    ("bundle250", "dressing", "dlc05_camping", "05"),
    ("bundle250", "cityassets", "dlc05_camping", "05"),
    ("bundle297", "dressing assets", "dlc07_horde", "07"),
    ("bundle358", "dressing assets", "dlc08_circus", "08"),
    ("bundle358", "night", "dlc08_circus", "08"),
    ("bundle405", "camping dlc_05", "dlc09_camping", "09"),
    ("bundle405", "circus dlc_08", "dlc09_circus", "09"),
    ("bundle405", "winter dlc_03", "dlc09_wonderland", "09"),
    ("bundle405", "battlements dlc_07", "dlc09_battlements", "09"),
    ("bundle421", "dressingassets", "dlc10", "10"),
    ("bundle428", "dressing", "dlc11_summer", "11"),
    ("bundle449", "dressing", "dlc13", "13"),
    # 节日主题 map 微缩装饰（dlc04/dlC10 新年、dlc13 中秋）
    ("bundle226", "map", "dlc04", "04"),
    ("bundle421", "map", "dlc10", "10"),
    ("bundle449", "map", "dlc13", "13"),
]

EXCL = re.compile(
    r"(^|_)(npc|kevin|chef|player|character|waitress)_"
    r"|(^|_)(ground|floor|ceiling|roof|stair|ramp|walkway|_col_|col_floor|collision|"
    r"killplane|deathscreen|waterfall|river|ocean|lake|_water|skybox|_icon|_gui|_ui|"
    r"frontend|horde_mode|horde_world|hovericon|_results|mode_ui|puddin|biscuit|_candy_)\b",
    re.IGNORECASE,
)
# 地图小件噪音（collider/优化器），按 token 边界匹配（兼容 _ 分隔）。
# 不加 ^pfx_：已入库的 pfx 风/沙粒子件是有意保留的装饰。
EXTRA_EXCL = re.compile(r"(?:^|_)(?:collider|scenery|optimizer)(?=_|$)", re.IGNORECASE)
GAMEPLAY = re.compile(
    r"(countertop|workstation|dispenser_crate|service_window|_gate$|round_results|"
    r"world_map_level_preview|hovericontextui|mode_ui)",
    re.IGNORECASE,
)

# 额外 NPC 装饰（真实可放置角色，走 art/npc 主题，与 common01/02 现有 npc 合并）。
# 容器必须小写（manifest container 全小写）。dlc="core" 对应 prefabs/core/。
NPC_ENTRIES = [
    ("bundle46", "kevin_01", "assets/prefabs/characters/kevin_01.prefab", "core"),
    ("bundle46", "npc_constructionworker_01", "assets/prefabs/characters/npc_constructionworker_01.prefab", "core"),
    ("bundle46", "npc_diners_01", "assets/prefabs/characters/npc_diners_01.prefab", "core"),
    ("bundle46", "npc_lifering", "assets/prefabs/characters/npc_lifering.prefab", "core"),
    ("bundle46", "npc_waiters_01", "assets/prefabs/characters/npc_waiters_01.prefab", "core"),
    ("bundle47", "npc_buck", "assets/prefabs/overcooked_legacy/npcs/npc_buck.prefab", "core"),
    ("bundle47", "npc_business", "assets/prefabs/overcooked_legacy/npcs/npc_business.prefab", "core"),
    ("bundle47", "npc_eskimo", "assets/prefabs/overcooked_legacy/levelspecific/arctic/npc_eskimo.prefab", "core"),
    ("bundle47", "npc_glasses", "assets/prefabs/overcooked_legacy/npcs/npc_glasses.prefab", "core"),
    ("bundle47", "npc_mel", "assets/prefabs/overcooked_legacy/npcs/npc_mel.prefab", "core"),
    ("bundle47", "npc_mike", "assets/prefabs/overcooked_legacy/npcs/npc_mike.prefab", "core"),
    ("bundle47", "npc_waiter", "assets/prefabs/overcooked_legacy/npcs/npc_waiter.prefab", "core"),
    ("bundle21", "npc_penguin_1", "assets/downloadablecontent/overcooked_legacy/dlc2/dlc_assets/prefabs/npc_penguin (1).prefab", "core"),
    ("bundle167", "city_kevin_02", "assets/downloadablecontent/dlc02/dlc_assets/prefabs/beach theme/city_kevin_02.prefab", "02"),
    ("bundle250", "kevin_01_dlc5", "assets/downloadablecontent/dlc05/dlc_assets/prefabs/animatingobjects/kevin_01_dlc5.prefab", "05"),
    ("bundle210", "robinflight", "assets/downloadablecontent/dlc03/dlc_assets/prefabs/characters/robinflight.prefab", "03"),
    ("bundle210", "robinground_01", "assets/downloadablecontent/dlc03/dlc_assets/prefabs/characters/robinground_01.prefab", "03"),
    ("bundle359", "dlc08_npc_firebreather01", "assets/downloadablecontent/dlc08/dlc_assets/prefabs/npc/dlc08_npc_firebreather01.prefab", "08"),
    ("bundle359", "dlc08_npc_firebreather02", "assets/downloadablecontent/dlc08/dlc_assets/prefabs/npc/dlc08_npc_firebreather02.prefab", "08"),
    ("bundle359", "dlc08_npc_juggler01", "assets/downloadablecontent/dlc08/dlc_assets/prefabs/npc/dlc08_npc_juggler01.prefab", "08"),
    ("bundle359", "dlc08_npc_strongman01", "assets/downloadablecontent/dlc08/dlc_assets/prefabs/npc/dlc08_npc_strongman01.prefab", "08"),
    ("bundle359", "dlc08_npc_strongman02", "assets/downloadablecontent/dlc08/dlc_assets/prefabs/npc/dlc08_npc_strongman02.prefab", "08"),
]


# 中秋月饼（dlc13 食材模型，无订单定义 → 作展示装饰入 dlc13 主题）
FESTIVAL_FOOD_ENTRIES = [
    ("bundle449", "dlc13_mooncakechocolate", "assets/downloadablecontent/dlc13/assets/prefabs/ingredients/dlc13_mooncakechocolate.prefab", "13"),
    ("bundle449", "dlc13_mooncakestrawberry", "assets/downloadablecontent/dlc13/assets/prefabs/ingredients/dlc13_mooncakestrawberry.prefab", "13"),
    ("bundle449", "dlc13_mooncakestrawberrychocolate", "assets/downloadablecontent/dlc13/assets/prefabs/ingredients/dlc13_mooncakestrawberrychocolate.prefab", "13"),
    ("bundle449", "dlc13_mooncakewatermelon", "assets/downloadablecontent/dlc13/assets/prefabs/ingredients/dlc13_mooncakewatermelon.prefab", "13"),
]

# 桌椅/家具装饰（工作台外观补充调研发现的遗漏件；主题并入现有调色板组，legacy 为新主题）
FURNITURE_ENTRIES = [
    # 魔法学校（并入 art/wizard）
    ("bundle47", "wizard_table_01", "assets/prefabs/themes/wizards_school/wizard_table_01.prefab", "core", "wizard"),
    ("bundle47", "wizard_tabletop_01", "assets/prefabs/themes/wizards_school/wizard_tabletop_01.prefab", "core", "wizard"),
    ("bundle47", "wizard_tabletop_02", "assets/prefabs/themes/wizards_school/wizard_tabletop_02.prefab", "core", "wizard"),
    ("bundle47", "wizard_tabletopcorner_01", "assets/prefabs/themes/wizards_school/wizard_tabletopcorner_01.prefab", "core", "wizard"),
    ("bundle47", "wizard_tabletopedge_01", "assets/prefabs/themes/wizards_school/wizard_tabletopedge_01.prefab", "core", "wizard"),
    # 太空（并入 art/space）
    ("bundle47", "space_chairs_01", "assets/prefabs/themes/space/space_chairs_01.prefab", "core", "space"),
    # 矿洞（并入 art/mine）
    ("bundle47", "mi_table_01", "assets/prefabs/themes/mine/mi_table_01.prefab", "core", "mine"),
    ("bundle47", "mi_turntable_01", "assets/prefabs/themes/mine/mi_turntable_01.prefab", "core", "mine"),
    ("bundle47", "m_mine_turntable_01", "assets/prefabs/themes/mine/m_mine_turntable_01.prefab", "core", "mine"),
    # dlc07 部落（并入 art/dlc07_horde；原名带空格 "p_dlc07_chair_01 1"）
    ("bundle297", "p_dlc07_chair_01_1", "assets/downloadablecontent/dlc07/dlc_assets/prefabs/dressing assets/p_dlc07_chair_01 1.prefab", "07", "dlc07_horde"),
    # OC1 legacy 餐客桌（新主题 art/legacy）
    ("bundle38", "table_customers", "assets/models/overcooked_legacy/table_customers.prefab", "core", "legacy"),
    ("bundle38", "table_customers_2", "assets/models/overcooked_legacy/table_customers 2.prefab", "core", "legacy"),
]

# 基础本体主题地板（bundle47，无 dlc 前缀 → core）。几何已从 dump .obj 判定：
# QUAD=竖立 quad（rotX90 铺平）；FLAT=横铺 XZ 平面（rotX0）。cellsPerScale 由名称尺寸决定。
FLOOR_ENTRIES = [
    # QUAD（rotX:90, depthAxis:y）
    ("bundle47", "air_balloon_kitchen_floor_wood_01", "assets/prefabs/themes/air_balloon/air_balloon_kitchen_floor_wood_01.prefab", "core", "1x1"),
    ("bundle47", "floor_wood_2x2", "assets/prefabs/themes/air_balloon/floor_wood_2x2.prefab", "core", "2x2"),
    ("bundle47", "grave_kitchen_floor_01", "assets/prefabs/themes/graveyard/grave_kitchen_floor_01.prefab", "core", "1x1"),
    ("bundle47", "mi_wood_floor_16x4", "assets/prefabs/themes/mine/mi_wood_floor_16x4.prefab", "core", "16x4"),
    ("bundle47", "throne_room_floor_01", "assets/prefabs/themes/throne/throne_room_floor_01.prefab", "core", "1x1"),
    ("bundle47", "wizard_floor_10x10", "assets/prefabs/themes/wizards_school/wizard_floor_10x10.prefab", "core", "10x10"),
    ("bundle47", "wizard_stone_floor_6x6", "assets/prefabs/themes/wizards_school/wizard_stone_floor_6x6.prefab", "core", "6x6"),
    ("bundle47", "wizard_wood_floor_6x4", "assets/prefabs/themes/wizards_school/wizard_wood_floor_6x4.prefab", "core", "6x4"),
    ("bundle295", "p_dlc7_floor_01", "assets/downloadablecontent/dlc07/dlc_assets/models/dressing assets/p_dlc7_floor_01.prefab", "07", "1x1"),
    # FLAT（rotX:0, depthAxis:z）
    ("bundle47", "floor_tile_9x4", "assets/prefabs/themes/air_balloon/floor_tile_9x4.prefab", "core", "9x4"),
    ("bundle47", "floor_tiled_blue_01", "assets/prefabs/shared_kitchen/floor_tiled_blue_01.prefab", "core", "1x1"),
    ("bundle47", "floor_tiles_black_1", "assets/prefabs/themes/city_sushi/prefabs/floor_tiles_black_1.prefab", "core", "1x1"),
    ("bundle47", "kitchen_floor_5x7", "assets/prefabs/themes/city_sushi/prefabs/kitchen_floor_5x7.prefab", "core", "5x7"),
    ("bundle47", "m_city_floor_h4_01", "assets/prefabs/themes/city_sushi/prefabs/m_city_floor_h4_01.prefab", "core", "1x1"),
    ("bundle47", "mi_floor_01", "assets/prefabs/themes/mine/mi_floor_01.prefab", "core", "1x1"),
]

def sanitize(name):
    n = name.strip()
    n = n.replace("&", "_and_")
    n = n.replace("(", "_").replace(")", "")
    n = re.sub(r"\s+", "_", n)
    n = re.sub(r"_+", "_", n)
    n = n.strip("_")
    return n


def is_excluded(name):
    return bool(EXCL.search(name) or EXTRA_EXCL.search(name) or GAMEPLAY.search(name))


def existing_prefab_ids():
    ids = set()
    patterns = [
        os.path.join(REPO_ROOT, "Assets/common01/prefabs/**/*.prefab"),
        os.path.join(REPO_ROOT, "Assets/common02/prefabs/**/*.prefab"),
        os.path.join(REPO_ROOT, "Assets/common03/prefabs/**/*.prefab"),
    ]
    for pat in patterns:
        base, tail = pat.rsplit("/**/", 1)
        for root, _dirs, files in os.walk(base):
            for f in files:
                if not f.endswith(".prefab"):
                    continue
                # common03 的 art 目录是我们的装饰条目，不应视为"已存在"而排除；
                # 其余（counters/utensils/mechanisms）仍用于 id 冲突检测。
                if root.startswith(os.path.join(REPO_ROOT, "Assets/common03/prefabs")) and "/art/" in os.path.relpath(
                    os.path.join(root, f), os.path.join(REPO_ROOT, "Assets/common03/prefabs")
                ):
                    continue
                ids.add(sanitize(f[:-7]))
    return ids


def main():
    manifest = json.load(open(MANIFEST, encoding="utf-8"))
    objs = manifest["objects"]

    # (bundle, subdir) -> raw basenames (across nested folders under the theme dir)
    folder_names = {}
    for o in objs:
        c = o.get("container") or ""
        if not c.lower().endswith(".prefab"):
            continue
        bundle = o.get("bundle", "")
        subdir = None
        for _b, sub, _t, _d in TARGETS:
            if bundle == _b and ("/" + sub + "/") in c:
                subdir = sub
                break
        if not subdir:
            continue
        base = os.path.basename(c)[:-7] if c.lower().endswith(".prefab") else os.path.basename(c)
        folder_names.setdefault((bundle, subdir), set()).add(base)

    # 全局原始名集合：Unity 重复导入 `<base> N`/`<base> (N)` 的基名只要在任意
    # 目标主题存在（含跨 bundle，如 dlc11 的 citywall_brick_01c 1 基名在 dlc02），
    # 就视为重复件剔除；基名不存在时保留原名（如 p_dlc13_arch 1 / plank 1）。
    all_raws = set()
    for names in folder_names.values():
        all_raws |= {r for r in names if not is_excluded(r)}
    dup_suffixes = set()
    for r in all_raws:
        m = re.match(r"^(.+?)\s+(?:\d+|\(\d+\))$", r)
        if m and m.group(1) in all_raws:
            dup_suffixes.add(r)

    seen = existing_prefab_ids()
    entries = []
    for bundle, sub, theme, dlc in TARGETS:
        raws = folder_names.get((bundle, sub), set())
        raws = {r for r in raws if not is_excluded(r)}
        raws -= dup_suffixes
        for r in sorted(raws):
            cid = sanitize(r)
            if not cid or cid in seen:
                continue
            seen.add(cid)
            container = None
            for o in objs:
                cc = o.get("container") or ""
                if o.get("bundle") == bundle and cc.lower().endswith(".prefab") and (
                    "/" + sub + "/"
                ) in cc and os.path.basename(cc).startswith(r + ".prefab"):
                    container = cc
                    break
            if not container:
                print(f"  !! 找不到容器: {r}", file=sys.stderr)
                continue
            entries.append(
                {
                    "id": cid,
                    "bundle": bundle,
                    "container": container,
                    "theme": theme,
                    "dlc": dlc,
                }
            )

    # 额外 NPC 装饰（真实可放置角色；theme=npc，与现有 art/npc 合并）
    for bundle, cid, container, dlc in NPC_ENTRIES:
        if cid in seen:
            continue
        seen.add(cid)
        o = None
        for oo in objs:
            if oo.get("bundle") == bundle and oo.get("container") == container:
                o = oo
                break
        if not o:
            print(f"  !! 找不到 NPC 容器: {cid} {container}", file=sys.stderr)
            continue
        entries.append({"id": cid, "bundle": bundle, "container": container, "theme": "npc", "dlc": dlc})

    # 中秋月饼（作展示装饰，theme=dlc13）
    for bundle, cid, container, dlc in FESTIVAL_FOOD_ENTRIES:
        if cid in seen:
            continue
        seen.add(cid)
        o = next((oo for oo in objs if oo.get("bundle") == bundle and oo.get("container") == container), None)
        if not o:
            print(f"  !! 找不到月饼容器: {cid} {container}", file=sys.stderr)
            continue
        entries.append({"id": cid, "bundle": bundle, "container": container, "theme": "dlc13", "dlc": dlc})

    # 主题地板（基础本体；theme=floors，surfaceTier 由 build-catalog 按 id 自动归类）
    for bundle, cid, container, dlc, _size in FLOOR_ENTRIES:
        if cid in seen:
            continue
        seen.add(cid)
        o = next((oo for oo in objs if oo.get("bundle") == bundle and oo.get("container") == container), None)
        if not o:
            print(f"  !! 找不到地板容器: {cid} {container}", file=sys.stderr)
            continue
        entries.append({"id": cid, "bundle": bundle, "container": container, "theme": "floors", "dlc": dlc})

    # 桌椅/家具装饰（主题并入现有组 / legacy 新组）
    for bundle, cid, container, dlc, theme in FURNITURE_ENTRIES:
        if cid in seen:
            continue
        seen.add(cid)
        o = next((oo for oo in objs if oo.get("bundle") == bundle and oo.get("container") == container), None)
        if not o:
            print(f"  !! 找不到家具容器: {cid} {container}", file=sys.stderr)
            continue
        entries.append({"id": cid, "bundle": bundle, "container": container, "theme": theme, "dlc": dlc})

    entries.sort(key=lambda e: (e["theme"], e["id"]))
    payload = {
        "generatedAt": "2026-08-19",
        "comment": "DLC 装饰物 ID 清单（导入输入源 + 翻译参考）。id=清洗后安全文件名，container=游戏内真实资产路径，theme=art 主题目录，dlc=所属 DLC。",
        "total": len(entries),
        "entries": entries,
    }
    by_theme = {}
    for e in entries:
        by_theme.setdefault(e["theme"], []).append(e["id"])
    payload["byTheme"] = by_theme

    if DRY:
        print(f"dry-run: total={len(entries)}")
        for t, ids in by_theme.items():
            print(f"  {t}: {len(ids)}")
        return
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=1)
    print(f"Wrote {len(entries)} entries -> {os.path.relpath(OUT, REPO_ROOT)}")


if __name__ == "__main__":
    main()
