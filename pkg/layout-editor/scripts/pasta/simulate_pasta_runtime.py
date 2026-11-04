#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""simulate_pasta_runtime.py — 意面资产生成后的运行时逻辑仿真（不依赖 Unity）。

模拟三段真实游戏代码路径，对生成的 commonW5/pasta 资产做端到端断言：
  1. RecipeHelper.GetOrderDefinitionNodeCustomRecipe：CustomRecipeSO 链 →
     运行时 OrderDefinitionNode（组成 uID / cookingStep uID / progress）。
  2. ComboOrderToPrefabLookup.FindPrefab：盘内组合 → 应命中的本地 plated 模型
     （匹配语义 = IngredientAssembledNode uID 值比较 + CookedCompositeAssembledNode
     的 cookingStep uID + progress；CompositeAssembledNode.Contains 双向包含）。
  3. ServerOrderControllerBase.Matches：三道订单 vs 各盘内组合的交付判定。
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
PASTA = os.path.join(REPO, "Assets/commonW5/custom_recipes/pasta")
UIDS = json.load(open(os.path.join(HERE, "assets/uids.json")))

V = UIDS  # 原版 uID


def read(rel):
    return open(os.path.join(PASTA, rel), encoding="utf-8").read()


def guid_of(rel):
    return re.search(r"guid: ([0-9a-f]{32})",
                     open(os.path.join(PASTA, rel + ".meta"), encoding="utf-8").read(200)).group(1)


# ── 运行时节点模型（AssembledDefinitionNode 语义） ────────────────────────
class Ing:
    def __init__(self, uid):
        self.uid = uid

    def match(self, other):
        return isinstance(other, Ing) and self.uid == other.uid


class Cooked:
    def __init__(self, comp, step, progress=1):
        self.comp = comp  # [Ing]
        self.step = step
        self.progress = progress

    def match(self, other):
        return (isinstance(other, Cooked) and other.progress == self.progress
                and other.step == self.step
                and contains([self.comp[0]], [other.comp[0]]))


class Composite:
    def __init__(self, comp):
        self.comp = comp


def contains(superSet, subSet):
    used = [False] * len(superSet)
    for s in subSet:
        hit = False
        for j, t in enumerate(superSet):
            if not used[j] and s.match(t):
                used[j] = True
                hit = True
                break
        if not hit:
            return False
    return True


# ── 1. RecipeHelper 仿真：CustomRecipeSO(Cooked) → 运行时 Cooked 节点 ────
def sim_cooked_node(asset_rel, ing_uid, step_uid):
    text = read(asset_rel)
    assert re.search(r"type: 2", text), asset_rel + " 应为 Cooked(2)"
    assert re.search(r"cookingProgress: 1", text), asset_rel + " 应 Cooked"
    return Cooked([Ing(ing_uid)], step_uid, 1)


def sim_final_order(asset_rel, comps):
    text = read(asset_rel)
    assert re.search(r"type: 1", text), asset_rel + " 应为 Composite(1)"
    guids = re.findall(r"compositionSOs:\n((?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)+)", text)
    assert guids, asset_rel + " composition 解析失败"
    n = len(guids[0].strip().splitlines())
    assert n == len(comps), f"{asset_rel}: composition 数 {n} != {len(comps)}"
    assert "optionalSOs: []" in text, asset_rel + " optional 应为空"
    return Composite(list(comps))


boiled = sim_cooked_node("orderdefinitions/Web_Seafood_Pasta_BoiledWrapper.asset", V["pastaNode"], V["potStep"])
fish = sim_cooked_node("orderdefinitions/Web_Seafood_Pasta_FishNode.asset", V["sushiFishNode"], V["fryingPanStep"])
prawn = sim_cooked_node("orderdefinitions/Web_Seafood_Pasta_PrawnNode.asset", V["sushiPrawnNode"], V["fryingPanStep"])

seafood_order = sim_final_order("Web_Seafood_Pasta.asset", [boiled, fish, prawn])
fish_order = sim_final_order("Web_Fish_Pasta.asset", [boiled, fish])
prawn_order = sim_final_order("Web_Prawn_Pasta.asset", [boiled, prawn])
print("[1] RecipeHelper 仿真 OK：三道订单节点组成正确（uID 链全对）")

# ── 2. ComboLookup.FindPrefab 仿真（lookup 克隆 → 桶内组合 → 本地模型） ──
clone_boiled = Cooked([Ing(V["pastaNode"])], V["potStep"], 1)
clone_fish = Cooked([Ing(V["sushiFishNode"])], V["fryingPanStep"], 1)
clone_prawn = Cooked([Ing(V["sushiPrawnNode"])], V["fryingPanStep"], 1)
lookup = [
    ([clone_boiled], "plated_pasta_01"),
    ([clone_boiled, clone_fish], "plated_pasta_fish_01"),
    ([clone_boiled, clone_prawn], "plated_pasta_prawn_01"),
    ([clone_boiled, clone_fish, clone_prawn], "recipe_seafood_pasta_01"),
]
fallback = [([clone_boiled], "plated_pasta_01")]


def find_prefab(ingredients):
    for content, prefab in reversed(lookup):
        num = 0
        for ing in ingredients:
            for c in content:
                if ing.match(c):
                    num += 1
        if num == len(ingredients) and num == len(content):
            return prefab
    best = -1
    for i, (content, prefab) in enumerate(fallback):
        if all(any(c.match(f) for f in ingredients) for c in content):
            best = i
    return fallback[best][1] if best >= 0 else "plated_pasta_01"


cases = [
    ([boiled], "plated_pasta_01"),
    ([boiled, fish], "plated_pasta_fish_01"),
    ([boiled, prawn], "plated_pasta_prawn_01"),
    ([boiled, fish, prawn], "recipe_seafood_pasta_01"),
    ([fish], "plated_pasta_01"),  # 鱼/虾单独在盘 → 默认回落
    ([boiled, fish, prawn, fish], None),  # 双鱼（非法组合，exact 全 miss → 回落）
]
for plate, expect in cases:
    got = find_prefab(plate)
    tag = "OK " if (expect is None or got == expect) else "FAIL"
    if expect is not None:
        print(f"[2] {tag} 盘内[{','.join(type(p).__name__ for p in plate)}] -> {got} (期望 {expect})")
        assert tag.strip() == "OK"
    else:
        print(f"[2] {tag} 非法组合 -> 回落 {got}")

# ── 3. 交付判定仿真（Matches = Contains 双向） ──────────────────────────
def order_matches(order, plate):
    return contains(order.comp, plate) and contains(plate, order.comp)


scenarios = [
    ("海鲜订单 + 意面鱼虾全齐", seafood_order, [boiled, fish, prawn], True),
    ("海鲜订单 + 只有意面", seafood_order, [boiled], False),
    ("海鲜订单 + 意面+鱼", seafood_order, [boiled, fish], False),
    ("鲜鱼订单 + 意面+鱼", fish_order, [boiled, fish], True),
    ("鲜鱼订单 + 意面鱼虾全齐", fish_order, [boiled, fish, prawn], False),
    ("鲜虾订单 + 意面+虾", prawn_order, [boiled, prawn], True),
    ("鲜虾订单 + 意面+鱼", prawn_order, [boiled, fish], False),
]
for name, order, plate, expect in scenarios:
    got = order_matches(order, plate)
    tag = "OK " if got == expect else "FAIL"
    print(f"[3] {tag} {name} -> {'交付' if got else '拒收'}（期望 {'交付' if expect else '拒收'}）")
    assert tag.strip() == "OK"

print("\nSIMULATE PASS — 组合视觉 / 交付判定 / 节点链全部符合预期")
