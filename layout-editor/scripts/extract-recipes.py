#!/usr/bin/env python3
"""
extract-recipes.py — 从游戏 AssetBundle 精确提取各 DLC 菜谱组成与烹饪步骤。

数据源：Assets/StreamingAssets/Windows（整个目录加载进同一环境，跨 bundle PPtr 可 deref）。
权威性：recipeitems 的 m_composition（PPtr.deref()）→ 真实食材组成；cookable 查表
（Pot/Fryable/Fryer/Griddle/Smores/RoastingTray/LargePot/Mixer/Smoothie/Cake/FruitPie/
Floats/Hotdog/Steamer）给出食材→烹饪步骤的真实映射。

输出：layout-editor/scripts/data/extracted-recipes.json
  { recipes: { id: { composition, leaves, plating, step, stepSource, bundle } }, lookups: {...} }

用法：python3 layout-editor/scripts/extract-recipes.py
"""
import json
import os
import re

import UnityPy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
WIN = os.path.join(ROOT, "Assets/StreamingAssets/Windows")
OUT = os.path.join(os.path.dirname(__file__), "data", "extracted-recipes.json")

# 查表 m_Name -> 步骤（权威）。_ING = 食材级查表；_RECIPE = 菜谱级查表。
STEP_FROM_LOOKUP = {
    "FryableObjectsLookup": "FryingPan",
    "DLC02_FryableObjectsLookup": "FryingPan",
    "DLC05_FryableObjectsLookup": "FryingPan",
    "DLC08_FryableObjectsLookup": "FryingPan",
    "DLC09_FryableObjectsLookup": "FryingPan",
    "DLC11_FryableObjectsLookup": "FryingPan",
    "FryerObjectLookup": "DeepFatFryer",
    "DLC08_FryerObjectLookup": "DeepFatFryer",
    "PotCookableObjectsLookup": "Pot",
    "DLC03_PotCookableObjectsLookup": "Pot",
    "DLC07_PotCookableObjectsLookup": "Pot",
    "DLC08_PotCookableObjectsLookup": "Pot",
    "DLC09_PotCookableObjectsLookup": "Pot",
    "DLC11_PotCookableObjectsLookup": "Pot",
    "DLC04_LargePotCookableObjectsLookup": "HotPot",
    "DLC10_LargePotCookableObjectsLookup": "HotPot",
    "DLC05_GriddleObjectsLookup": "GriddlePan",
    "DLC05_SmoresLookUp": "ToastingFork",
    "DLC07_RoastingTrayMeatLookup": "RoastingTray",
    "DLC07_RoastingTrayObjectsLookup": "RoastingTray",
    "DLC09_RoastingTrayMeatLookup": "RoastingTray",
    "DLC09_RoastingTrayObjectsLookup": "RoastingTray",
    "SteamerPrefabLookup": "Steamer",
}
MIXER_LOOKUPS = {
    "MixerPrefabLookup", "DLC02_MixerPrefabLookup", "DLC03_MixerPrefabLookup",
    "DLC05_MixerPrefabLookup", "DLC08_MixerPrefabLookup", "DLC09_MixerPrefabLookup",
}
BAKE_LOOKUPS = {
    "CakePrefabLookup": "OvenTray",
    "DLC03_CakePrefabLookup": "OvenTray",
    "DLC05_CakePrefabLookup": "OvenTray",
    "DLC09_CakePrefabLookup": "OvenTray",
    "DLC07_FruitPiePrefabLookup": "OvenTray",
}
SMOOTHIE_LOOKUPS = {"DLC02_SmoothiePrefabLookup"}
ASSEMBLY_LOOKUPS = {"DLC11_FloatsPrefabLookup", "DLC08_HotdogPrefabLookup", "DLC11_HotdogPrefabLookup"}

# 游戏数据怪癖修正：dlc08 cheesesticks/onionrings 的组成引用了同一对象（FriedOnion_Rings），
# 按 Fryer 查表（含 DLC08_CheeseSticks/DLC08_Onion_Ring）修正为真实食材。
QUIRK_COMPOSITION = {
    "CheeseSticks": ["DLC08_CheeseSticks"],
    "OnionRings": ["DLC08_Onion_Ring"],
}


def norm_name(name):
    """统一名称用于查表匹配：去空格、(i) 后缀。"""
    n = re.sub(r"\s*\(i\)\s*$", "", name or "").strip()
    return n


def dlc_of(obj):
    """由资产容器路径推导 DLC（dlc03 / core）。"""
    c = (obj.container or "").lower()
    m = re.search(r"/dlc(\d+)/", c)
    return f"dlc{m.group(1)}" if m else "core"


def collect():
    recipes = {}
    lookups = {}
    env = UnityPy.load(WIN)
    for obj in env.objects:
        try:
            tree = obj.read_typetree()
        except Exception:
            continue
        if not isinstance(tree, dict):
            continue
        if "m_composition" in tree and "m_orderGuiDescription" in tree:
            data = obj.read()
            name = getattr(data, "m_Name", "")
            if not name:
                continue
            comp = []
            for p in getattr(data, "m_composition", []) or []:
                n = deref_name(p)
                if n:
                    comp.append(n)
            recipes[name] = {
                "composition": comp,
                "plating": deref_name(getattr(data, "m_platingStep", None)),
                "optional": bool(getattr(data, "m_optional", None)),
                "dlc": dlc_of(obj),
            }
        elif "m_lookupArray" in tree and "lookup" in (tree.get("m_Name", "") or "").lower():
            data = obj.read()
            name = getattr(data, "m_Name", "")
            if not name:
                continue
            contents = []
            for e in getattr(data, "m_lookupArray", []) or []:
                n = deref_name(getattr(e, "m_content", None))
                if n:
                    contents.append(norm_name(n))
            lookups[name] = {"contents": contents, "dlc": dlc_of(obj)}
    return recipes, lookups


def deref_name(ptr):
    if ptr is None:
        return None
    try:
        obj = ptr.deref()
    except Exception:
        return None
    if obj is None:
        return None
    try:
        return getattr(obj.read(), "m_Name", None)
    except Exception:
        return None


def leaf_expand(recipe_id, recipes, memo=None):
    if memo is None:
        memo = {}
    if recipe_id in memo:
        return memo[recipe_id]
    memo[recipe_id] = []
    r = recipes.get(recipe_id)
    if not r:
        memo[recipe_id] = [recipe_id]
        return memo[recipe_id]
    out = []
    for c in r["composition"]:
        sub = recipes.get(c)
        if sub and sub["composition"]:
            out.extend(leaf_expand(c, recipes, memo))
        else:
            out.append(c)
    memo[recipe_id] = out
    return out


def steps_for_assets(assets, lookups, recipe_dlc):
    """直接组成/叶子食材在 cookable 查表中的步骤。
    同 DLC 查表优先于基础（core）查表，避免共享食材名（如 MixedFlourEggChocolate
    同时被基础煎锅与 dlc08 炸锅查表引用）误判。"""
    steps = []
    for a in assets:
        an = norm_name(a)
        for lname, step in STEP_FROM_LOOKUP.items():
            info = lookups.get(lname)
            if not info:
                continue
            if an in info["contents"]:
                if step not in steps:
                    steps.append((step, 0 if info["dlc"] == recipe_dlc else 1))
    steps.sort(key=lambda x: x[1])
    return [s for s, _ in steps]


def derive_step(recipe_id, recipes, lookups):
    r = recipes.get(recipe_id)
    if not r:
        return "", ""
    dlc = r["dlc"]
    comp = r["composition"]
    leaves = leaf_expand(recipe_id, recipes)
    steps = steps_for_assets(comp, lookups, dlc) + steps_for_assets(leaves, lookups, dlc)
    # 直接组成里的中间产物若命中【同 DLC】烘焙查表（蛋糕/派），该中间产物进烤箱
    bake_comp = None
    for c in comp:
        for lname, step in BAKE_LOOKUPS.items():
            info = lookups.get(lname)
            if not info or info["dlc"] != dlc:
                continue
            if norm_name(c) in info["contents"]:
                bake_comp = step

    # 1) 奶昔 -> Blender
    if recipe_id in lookups.get("DLC02_SmoothiePrefabLookup", {}).get("contents", []):
        return "Blender", "smoothieprefablookup"
    if "Smoothie" in recipe_id:
        return "Blender", "name"
    # 2) 组装类（漂浮/热狗）：组成中含组装查表组件（BoiledFrankfurter/IceCream_* 等）
    #    时，步骤从该组件叶子推导（如热狗 = 煮香肠 → Pot）
    for lname in ASSEMBLY_LOOKUPS:
        info = lookups.get(lname)
        if not info:
            continue
        asm = set(info["contents"])
        hit = [c for c in comp if norm_name(c) in asm]
        if not hit:
            continue
        for c in hit:
            s = steps_for_assets(leaf_expand(c, recipes, {}), lookups, dlc)
            if s:
                return s[0], "assembly"
        return (steps[0] if steps else ""), "assembly"
    # 3) 面糊类恒为 Mixer（混合成型）
    if "MixedFlour" in recipe_id:
        return "Mixer", "name"
    # 4) 煎（煎饼/炸鸡等；先于混合判定，避免基础混合查表误捕煎饼）
    for lname in STEP_FROM_LOOKUP:
        if STEP_FROM_LOOKUP[lname] == "FryingPan" and recipe_id in lookups.get(lname, {}).get("contents", []):
            return "FryingPan", lname
    # 5) 混合成型（圣诞布丁等）
    for lname in sorted(MIXER_LOOKUPS):
        if recipe_id in lookups.get(lname, {}).get("contents", []):
            return "Mixer", lname
    # 6) 炸锅
    for lname in STEP_FROM_LOOKUP:
        if STEP_FROM_LOOKUP[lname] == "DeepFatFryer" and recipe_id in lookups.get(lname, {}).get("contents", []):
            return "DeepFatFryer", lname
    # 7) 烘焙（蛋糕/派）
    for lname, step in BAKE_LOOKUPS.items():
        if recipe_id in lookups.get(lname, {}).get("contents", []):
            return step, lname
    if bake_comp:
        return bake_comp, "bake-comp"
    # 8) 名称兜底：烤串/月亮派/水果派
    if "Kebob" in recipe_id or "Kebab" in recipe_id:
        return "KebabSkewer", "name"
    if "MoonPie" in recipe_id or "FruitPie" in recipe_id:
        return "OvenTray", "name"
    # 9) 成分级步骤
    if steps:
        return steps[0], "ingredients"
    return "", "none"


def main():
    print("loading bundles...")
    recipes, lookups = collect()
    print(f"recipes: {len(recipes)}, lookups: {len(lookups)}")

    out = {}
    for rid, r in sorted(recipes.items()):
        comp = list(QUIRK_COMPOSITION.get(rid, r["composition"]))
        if not comp:
            continue
        r["composition"] = comp
        step, src = derive_step(rid, recipes, lookups)
        out[rid] = {
            "composition": comp,
            "leaves": leaf_expand(rid, recipes),
            "plating": r["plating"],
            "optional": r["optional"],
            "step": step,
            "stepSource": src,
            "dlc": r["dlc"],
        }

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump({"recipes": out, "lookups": {k: v for k, v in lookups.items()}}, f,
                  ensure_ascii=False, indent=1)
    print(f"wrote {OUT} ({len(out)} recipes)")

if __name__ == "__main__":
    main()
