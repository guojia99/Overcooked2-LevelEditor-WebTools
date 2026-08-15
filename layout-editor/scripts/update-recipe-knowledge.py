#!/usr/bin/env python3
"""
update-recipe-knowledge.py — 用 extract-recipes.py 的权威提取数据更新 recipe-knowledge.json。

映射：
  提取菜谱名（bundle m_Name） -> 编辑器菜谱 id（Import/Recipes 与 common01/02 资产文件名）
  叶子食材名（bundle m_Name） -> 编辑器食材 id（Ingredients 资产文件名）
规范化规则：小写 + 去下划线/连字符/空格 后精确比对；特殊表兜底。

用法：python3 layout-editor/scripts/update-recipe-knowledge.py [--dry]
"""
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
EXTRACTED = os.path.join(os.path.dirname(__file__), "data", "extracted-recipes.json")
KNOWLEDGE = os.path.join(os.path.dirname(__file__), "data", "recipe-knowledge.json")

RECIPE_DIRS = [
    "Assets/common01/food/Recipes",
    "Assets/common02/food/Recipes",
    "Assets/Editor/LayoutEditor/Import/Recipes",
]
ING_DIRS = [
    "Assets/common01/food/Ingredients",
    "Assets/common02/food/Ingredients",
    "Assets/Editor/LayoutEditor/Import/Ingredients",
]

# 特殊名映射（规范化后仍无法匹配的）：提取名 -> 编辑器 id
RECIPE_NAME_FIX = {
    "HotChocolate": "hotchocolate",
    "HotChocolateCream": "hotchocolatecream",
    "HotChocolateMallow": "hotchocolatemallow",
    "HotChocolateMallowCream": "hotchocolatemallowcream",
    "ChristmasPudding": "christmaspudding",
    "ChristmasPuddingWithOrange": "christmaspuddingwithorange",
    "HotPotMeat": "hotpot_meat",
    "HotPotPrawn": "hotpot_prawn",
    "HotPotDoubleMeat": "hotpot_doublemeat",
    "HotPotDoublePrawn": "hotpot_doubleprawn",
    "HotPotMixed": "hotpot_mixed",
    "IceCreamVanilla": "icecream_vanilla",
    "IceCreamChocolate": "icecream_chocolate",
    "RootBeerFloatVanilla": "rootbeerfloat_vanilla",
    "RootBeerFloatChocolate": "rootbeerfloat_chocolate",
    "OrangeSodaFloatVanilla": "orangesodafloat_vanilla",
    "OrangeSodaFloatChocolate": "orangesodafloat_chocolate",
    "DLC11HotdogPlain": "dlc11_hotdog_plain",
    "DLC11HotdogKetchup": "dlc11_hotdog_ketchup",
    "DLC11HotdogMustard": "dlc11_hotdog_mustard",
    "DLC11HotdogKetchupMustard": "dlc11_hotdog_ketchup_mustard",
    "DLC11HotdogOnions": "dlc11_hotdog_onions",
    "DLC11HotdogOnionsKetchup": "dlc11_hotdog_onions_ketchup",
    "DLC11HotdogOnionsMustard": "dlc11_hotdog_onions_mustard",
    "SaladCornOnion": "salad_corn_onion",
    "SaladCucumberOnion": "salad_cucumber_onion",
    "SaladTomatoOnion": "salad_tomato_onion",
    "SaladCucumberTomatoOnion": "salad_cucumber_tomato_onion",
    "TomatoCornOnion": "tomato_corn_onion",
    "TomatoCucumberOnion": "tomato_cucumber_onion",
    "BoiledFrankfurter": "boiledfrankfurter",
    "FriedOnions": "friedonions",
    "DLC08FriedChips": "dlc08_friedchips",
    "FriedCheeseSticks": "friedcheese_sticks",
    "FriedChickenBurger": "friedchickenburger",
    "FriedOnionRings": "friedonion_rings",
    "CheeseSticks": "cheesesticks",
    "OnionRings": "onionrings",
    "ChickenBurger": "chickenburger",
    "DonutPlain": "donut_plain",
    "DonutChocolate": "donut_chocolate",
    "DonutRaspberry": "donut_raspberry",
    "HotdogPlain": "hotdog_plain",
    "HotdogKetchup": "hotdog_ketchup",
    "HotdogMustard": "hotdog_mustard",
    "HotdogKetchupMustard": "hotdog_ketchup_mustard",
    "HotdogOnions": "hotdog_onions",
    "HotdogOnionsKetchup": "hotdog_onions_ketchup",
    "HotdogOnionsMustard": "hotdog_onions_mustard",
    "DLC11BoiledFrankfurter": "dlc11_boiledfrankfurter",
    "DLC11FriedOnions": "dlc11_friedonions",
    "ChickenTomatoKebob": "Kebob_ChickenTomato",
    "ChickenMeatTomatoKebob": "Kebob_ChickenMeatTomato",
    "MeatMushroomPineappleKebob": "Kebob_MeatMushroomPineapple",
    "MushroomPineappleTomatoKebob": "Kebob_MushroomPineappleTomato",
    "MeatPineappleMushroomChickenKebob": "meatpineapplemushroomchickenkebob",
    "HawaiianBurger": "Burger_Hawaiian",
    "BananaSmoothie": "Smoothie_Banana",
    "BananaPineappleSmoothie": "Smoothie_BananaPineapple",
    "MelonSmoothie": "Smoothie_Melon",
    "StrawberrySmoothie": "Smoothie_Strawberry",
    "MegaSmoothie": "Smoothie_Mega",
    "ChocolateSmoothie": "chocolatesmoothie",
    "StrawberryPancake": "Cake_StrawberryPancake",
    "BlueberryPancake": "Cake_BlueberryPancake",
    "MixedFlourEggStrawberry": "MixedFlourEggStrawberry",
    "MixedFlourEggBlueberry": "MixedFlourEggBlueberry",
    "RoastedMarshmallow": "roastedmarshmallow",
}

# 食材特殊名（规范化后仍无法匹配）：提取名 -> 编辑器食材 id
ING_NAME_FIX = {
    "Flour": "FlourSO",
    "Egg": "EggSO",
    "Meat": "MeatSO",
    "Chocolate": "ChocolateSO",
    "Honeycomb": "HoneycombSO",
    "Cheese": "CheeseSO",
    "Carrot": "CarrotSO",
    "Onion": "OnionSO",
    "Potato": "PotatoSO",
    "Tomato": "TomatoSO",
    "Lettuce": "LettuceSO",
    "Cucumber": "CucumberSO",
    "Broccoli": "broccoli",
    "Pasta": "PastaSO",
    "Rice": "RiceSO",
    "SushiRice": "SushiRiceSO",
    "Mushroom": "MushroomSO",
    "Chicken": "ChickenSO",
    "Prawn": "PrawnSO",
    "Fish": "FishSO",
    "DLC05Egg": "DLC05_Egg",
    "DLC05Chocolate": "DLC05_Chocolate",
    "DLC05Banana": "DLC05_Banana",
    "DLC05Strawberry": "DLC05_Strawberry",
    "DLC05Marshmallow": "DLC05_Marshmallow",
    "DLC07Cheese": "dlc07_cheese",
    "DLC07Potato": "dlc07_potato",
    "DLC08Chicken": "dlc08_chicken",
    "DLC08Onion": "dlc08_onion",
    "DLC08Potato": "dlc08_potato",
    "DLC08OnionRing": "dlc08_onion_ring",
    "DLC08CheeseSticks": "dlc08_cheesesticks",
    "DLC09Milk": "dlc09_milk",
    "DLC09Chocolate": "dlc09_chocolate",
    "DLC09Flour": "dlc09_flour",
    "DLC09Egg": "dlc09_egg",
    "DLC09Carrot": "dlc09_carrot",
    "DLC09Potato": "dlc09_potato",
    "DLC09Broccoli": "dlc09_broccoli",
    "DLC09BeefRoast": "dlc09_beef_roast",
    "DLC09ChickenRoast": "dlc09_chicken_roast",
    "DLC10Noodles": "dlc10_noodles",
    "DLC10Meat": "dlc10_meat",
    "DLC10Prawn": "dlc10_prawn",
    "DLC10BokChoy": "dlc10_bokchoy",
    "DLC11Milk": "dlc11_milk",
    "DLC11Lettuce": "dlc11_lettuce",
    "DLC11Cucumber": "dlc11_cucumber",
    "DLC11Tomato": "dlc11_tomato",
    "DLC11Onion": "dlc11_onion",
    "DLC11Frankfurter": "dlc11_frankfurter",
    "DLC11HotDogBun": "dlc11_hotdogbun",
    "DLC11OnionSalad": "dlc11onion_salad",
    "DLC13Flour": "dlc13_flour",
    "DLC13Egg": "dlc13_egg",
    "DLC13Chocolate": "dlc13_chocolate",
    "DLC13Melon": "dlc13_melon",
    "DLC13Strawberry": "dlc13_strawberry",
    "DLC13Grapes": "dlc13_grapes",
    "DLC13Peach": "dlc13_peach",
    "DLC13Orange": "dlc13_orange",
    "SmoothieStrawberry(i)": "SmoothieStrawberry",
    "SmoothiePineapple(i)": "SmoothiePineapple",
    "KebobChicken(i)": "KebobChicken",
    "KebobMeat(i)": "KebobMeat",
    "KebobMushroom(i)": "KebobMushroom",
    "KebobPineapple(i)": "KebobPineapple",
    "KebobTomato(i)": "KebobTomato",
    "BurgerPineapple(i)": "BurgerPineapple",
    "PancakeStrawberry": "PancakeStrawberry",
    "Crackers": "DLC05_Crackers",
    "Bacon": "Bacon",
    "Sausage": "Sausage",
    "Beans": "DLC05_Beans",
    "Blueberry": "Blueberry",
    "Raspberry": "raspberry",
    "Ketchup": "ketchup",
    "Mustard": "mustard",
    "Marshmallow": "marshmallow",
    "WhippedCream": "whippedcream",
    "DriedFruit": "driedfruit",
    "Orange": "orange",
    "Milk": "milk",
    "Vanilla": "vanilla",
    "IceCube": "icecube",
    "RootBeer": "rootbeer",
    "OrangeSoda": "orangesoda",
    "Corn": "corn",
    "Grapes": "grapes",
    "Peach": "peach",
    "Apple": "apple",
    "Blackberry": "blackberry",
    "Cherry": "cherry",
    "Leek": "leek",
    "BeefRoast": "beef_roast",
    "ChickenRoast": "chicken_roast",
    "ChickenRoast(dlc07)": "chicken_roast",
    "BeefRoast(dlc07)": "beef_roast",
    "DLC09ChickenRoast": "dlc09_chicken_roast",
    "DLC09BeefRoast": "dlc09_beef_roast",
    "BokChoy": "bokchoy",
    "Noodles": "noodles",
    "DLC04Meat": "dlc04_meat",
    "DLC04Prawn": "dlc04_prawn",
    "Bun": "dlc08_bun",
    "HotDogBun": "hotdogbun",
    "Frankfurter": "frankfurter",
    "Turkey": "TurkeySO",
    "Pepperoni": "PepperoniSO",
    "NuggetChicken": "NuggetChickenSO",
    "ChoppedBun": "ChoppedBunSO",
    "Dough": "DoughSO",
    "Tortilla": "TortillaSO",
    "BurritoChicken": "BurritoChickenSO",
    "BurritoMeat": "BurritoMeatSO",
    "Seaweed": "SeaweedSO",
    "SushiFish": "SushiFishSO",
    "SushiPrawn": "SushiPrawnSO",
    "PastaTomato": "PastaTomatoSO",
    "Melon": "Melon",
    "Banana": "Banana",
}


def norm(s):
    return re.sub(r"[\s_\-()]", "", (s or "").lower())


def scan_assets(dirs):
    """目录下全部 .asset 的 (m_Name, prefabName, dlc) -> 规范化名。"""
    out = []
    for d in dirs:
        abs_d = os.path.join(ROOT, d)
        if not os.path.isdir(abs_d):
            continue
        for root, _, files in os.walk(abs_d):
            for f in files:
                if not f.endswith(".asset") or f.endswith(".meta"):
                    continue
                p = os.path.join(root, f)
                try:
                    text = open(p, encoding="utf-8", errors="replace").read()
                except Exception:
                    continue
                m_name = re.search(r"^m_Name:\s*(\S+)", text, re.M)
                pn = re.search(r"^prefabName:\s*(\S+)", text, re.M)
                rel = os.path.relpath(p, os.path.join(ROOT, "Assets")).replace("\\", "/")
                m = re.search(r"/dlc(\d+)/", rel)
                dlc = f"dlc{m.group(1)}" if m else "core"
                out.append((m_name.group(1) if m_name else f[: -len(".asset")],
                            pn.group(1) if pn else None, dlc))
    return out


def build_map(entries, fixes):
    """全局规范化名 -> id（同 dlc 优先，供上下文映射回退）。"""
    by_dlc = {}
    by_norm = {}
    for name, prefab, dlc in entries:
        key = norm(name) if name else None
        if key:
            by_dlc.setdefault(dlc, {}).setdefault(key, name or prefab)
            by_norm.setdefault(key, name or prefab)
        if prefab:
            by_dlc.setdefault(dlc, {}).setdefault(norm(prefab), name or prefab)
            by_norm.setdefault(norm(prefab), name or prefab)
    for k, v in fixes.items():
        by_norm[norm(k)] = v
    return {"by_dlc": by_dlc, "by_norm": by_norm}


def map_name(name, name_map, dlc, label):
    key = norm(name)
    same = name_map["by_dlc"].get(dlc, {}).get(key)
    if same:
        return same
    return name_map["by_norm"].get(key)


def main():
    dry = "--dry" in sys.argv
    extracted = json.load(open(EXTRACTED, encoding="utf-8"))["recipes"]
    knowledge = json.load(open(KNOWLEDGE, encoding="utf-8"))

    recipe_entries = scan_assets(RECIPE_DIRS)
    ing_entries = scan_assets(ING_DIRS)
    recipe_map = build_map(recipe_entries, RECIPE_NAME_FIX)
    ing_map = build_map(ing_entries, ING_NAME_FIX)

    by_id = {r["id"]: r for r in knowledge["recipes"]}
    updated = 0
    report = []
    unmapped_recipes = []
    unmapped_ings = set()

    for ext_name, ext in sorted(extracted.items()):
        if ext["optional"]:
            continue
        dlc = ext["dlc"]
        rid = map_name(ext_name, recipe_map, dlc, "菜谱")
        if rid is None:
            unmapped_recipes.append(ext_name)
            continue
        if rid in knowledge.get("skip", []):
            continue
        ing_ids = []
        skip_recipe = False
        for i in ext["leaves"]:
            mapped = map_name(i, ing_map, dlc, "食材")
            if mapped is None:
                unmapped_ings.add(i)
                skip_recipe = True
                break
            ing_ids.append(mapped)
        if skip_recipe:
            continue
        step = ext["step"]
        old = by_id.get(rid)
        old_step = old["step"] if old else None
        old_ings = old["ingredients"] if old else None
        if (not old) or old_step != step or old_ings != ing_ids:
            report.append((rid, old_step, old_ings, step, ing_ids, ext["stepSource"]))
            updated += 1
            if not dry:
                by_id[rid] = {"id": rid, "step": step, "ingredients": ing_ids}

    if not dry:
        knowledge["recipes"] = [v for _, v in sorted(by_id.items(), key=lambda kv: kv[0])]
        json.dump(knowledge, open(KNOWLEDGE, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

    print(f"{'[dry] ' if dry else ''}更新 {updated} 条菜谱知识（knowledge 总数 {len(knowledge['recipes'])}）")
    print(f"未映射菜谱（跳过，多为编辑器无对应资产的基础中间产物）: {len(unmapped_recipes)}")
    if unmapped_ings:
        print(f"!! 未映射食材: {sorted(unmapped_ings)}")
    print("\n差异明细：")
    for rid, os_, oi, ns, ni, src in sorted(report):
        print(f"  {rid:34s} {os_ or '-':10s} {oi}  =>  {ns or '-':10s} {ni}  [{src}]")


if __name__ == "__main__":
    main()
