#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""validate_pasta.py — commonW5 意面资产生态静态校验。

校验项：
  1. pasta 目录所有 YAML 引用的 guid 必须可解析（工程内 .meta 存在），
     杜绝对原版 bundle 内 guid 的直连（v1 事故根因）。
  2. 保留 guid 不漂移（海鲜意面/图标/盘体/lookup/BoiledWrapper）。
  3. 三道菜谱 composition 链完整（→ 中间产物 CustomRecipeSO → 外库懒加载 SO）。
  4. lookup 克隆 uID 与原版一致（Pasta/SushiFish/SushiPrawn/Pot/FryingPan）。
  5. names.json 三道菜谱齐备；图标 png/web 副本存在。
  6. 外库引用只指向白名单 SO（common01/02/03、commonW1 懒加载 wrapper）。
"""
import json
import os
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
W5 = os.path.join(REPO, "Assets/commonW5")
PASTA = os.path.join(W5, "custom_recipes/pasta")

KNOWN_VANILLA = {
    "b3eb2e8aee1422f479da965c73850259",  # BoiledPasta 节点（v1 直连事故）
    "183b14d0ac926ee40b54d553b0359c14",  # SushiFish 节点
    "20167b8eec0dd1545ab24c8dbff7455f",  # SushiPrawn 节点
    "d47608530669a974f9e89cd24b9eb616",  # FryingPan 步骤
    "6e2ff5b410f21ca45939d8818f521a9d",  # Plate 步骤
    "c447027a9896104449261f3e178d1e10",  # plated_fish_sliced_02
    "9a50e331e5de3054eaf74505d6bb1805",  # plated_prawn_01
    "f2dffadffc27578499d68290e8fe07a8",  # plated_pasta_01
    "169ad6d93c832484fba55cae25d6e705",  # plated_pasta_fish_01
    "b6a2ecf0d199b914584849295eeee9e8",  # plated_pasta_prawn_01
    "9b32081d3e2e1144dbc72f0a159ec965",  # recipe_seafood_pasta_01
    "a4f5cb0444b0230478401b1a24debcb5",  # sh_gloss_d_e_hl
}

EXTERNAL_WHITELIST = {
    # 外库懒加载 SO（bundleName+assetPath，只读引用）
    "02b04fdf6fef0e944870fd5e06375ed1",  # common01 Plate
    "7ac8ffa91f182f14c8d14bfe5badd2ac",  # common01 PastaSO
    "bbf0e92641aa4ab49bda6bc68117e01f",  # common01 SushiFishSO
    "df4a04d1549f9ee41a991153bd723110",  # common01 SushiPrawnSO
    "5db05a4f76f128a4b8297bdce834fe96",  # common02 Pot
    "8b70d2ada088ef74e84700ee193b1812",  # common02 PotIcon
    "72bee3db65786774683403e9c362cb97",  # common02 FryingPan
    "292a7ea66aa4d1d48a3477fe3a56675c",  # common02 FryingPanIcon
    "1f86c3da2a2e1ac4e8e26cbd3371e2e3",  # common03 plated_fish_sliced_02 模型
    "1e7d19a6b7b9b37317d9306c6986328b",  # commonW1 Sushi_PlainPrawn_SO
    "f099b5194b8af4747789845c829644e5",  # commonW5 原版煎鱼 prep_fish_sliced_01
    "acc68c6c595c313b61e68dfa7c2396dc",  # commonW5 原版煎虾 prawn_sliced_diced_01
}

KEEP_GUIDS = {
    "a2bf51c527a7713bbb5734aded8f83a7": "Web_Seafood_Pasta.asset",
    "194b542045424df04cc384529d3fd89f": "icons/Web_Seafood_Pasta.png",
    "413f3858a18f067c6ba9a912447276ed": "models/Web_Seafood_Pasta_Plate.prefab",
    "4165416d3ee79ad49f518e75525caa25": "lookups/Web_Seafood_Pasta_ComboLookup.asset",
    # BoiledWrapper 跨路径搬移（pseudo_prefab_so→orderdefinitions），必须用新 guid：
    # 旧 guid 49bf8774… 在 Unity Library 仍映射旧路径，复用会触发 "Failed to unload" 断言。
}
FORBIDDEN_GUIDS = {
    "49bf8774c7f2abe8099784509132a768",  # v1 BoiledWrapper（跨路径复用 = Failed to unload）
}

UIDS = json.load(open(os.path.join(os.path.dirname(__file__), "assets/uids.json")))

errors = []
warns = []


def project_guid_map():
    m = {}
    for root, _dirs, files in os.walk(os.path.join(REPO, "Assets")):
        for fn in files:
            if not fn.endswith(".meta"):
                continue
            p = os.path.join(root, fn)
            try:
                head = open(p, errors="replace").read(200)
            except OSError:
                continue
            g = re.search(r"guid: ([0-9a-f]{32})", head)
            if g:
                m[g.group(1)] = p
    return m


GUIDS = project_guid_map()


def check_yaml_refs():
    for root, _dirs, files in os.walk(PASTA):
        for fn in files:
            if not fn.endswith((".asset", ".prefab", ".mat")):
                continue
            p = os.path.join(root, fn)
            text = open(p, encoding="utf-8", errors="replace").read()
            rel = os.path.relpath(p, PASTA)
            for g in re.findall(r"guid: ([0-9a-f]{32})", text):
                if g in KNOWN_VANILLA:
                    errors.append(f"{rel}: 直连原版 bundle guid {g}（v1 事故根因，禁止）")
                elif g in FORBIDDEN_GUIDS:
                    errors.append(f"{rel}: 引用禁用 guid {g}（跨路径复用 → Failed to unload）")
                elif g in EXTERNAL_WHITELIST:
                    continue
                elif g not in GUIDS:
                    errors.append(f"{rel}: 引用 guid {g} 工程内不存在")
                elif "/Scripts/" in GUIDS[g] or "/Editor/" in GUIDS[g]:
                    continue  # 脚本引用
    # 纹理/材质/shader 目录里的引用也走同一路径（已包含在上面的 Walk 中）


def check_keep_guids():
    for g, rel in KEEP_GUIDS.items():
        meta = os.path.join(PASTA, rel + ".meta")
        if not os.path.exists(meta):
            errors.append(f"keep 缺失: {rel}")
            continue
        head = open(meta, errors="replace").read(200)
        found = re.search(r"guid: ([0-9a-f]{32})", head)
        if not found or found.group(1) != g:
            errors.append(f"keep guid 漂移: {rel} 期望 {g} 实际 {found and found.group(1)}")
    # 全目录（含 .meta）不得再出现禁用 guid
    for root, _dirs, files in os.walk(PASTA):
        for fn in files:
            p = os.path.join(root, fn)
            head = open(p, errors="replace").read(400)
            for g in FORBIDDEN_GUIDS:
                if g in head:
                    errors.append(f"{os.path.relpath(p, PASTA)}: 含禁用 guid {g}")


def read_asset(rel):
    return open(os.path.join(PASTA, rel), encoding="utf-8", errors="replace").read()


def guid_of(rel):
    meta = open(os.path.join(PASTA, rel + ".meta"), errors="replace").read(200)
    return re.search(r"guid: ([0-9a-f]{32})", meta).group(1)


def check_recipe_chain():
    # 中间产物存在性 + composition 链
    nodes = {
        "boiled": guid_of("orderdefinitions/Web_Seafood_Pasta_BoiledWrapper.asset"),
        "fish": guid_of("orderdefinitions/Web_Seafood_Pasta_FishNode.asset"),
        "prawn": guid_of("orderdefinitions/Web_Seafood_Pasta_PrawnNode.asset"),
    }
    recipes = {
        "Web_Seafood_Pasta": [nodes["boiled"], nodes["fish"], nodes["prawn"]],
        "Web_Fish_Pasta": [nodes["boiled"], nodes["fish"]],
        "Web_Prawn_Pasta": [nodes["boiled"], nodes["prawn"]],
    }
    for rid, comp in recipes.items():
        text = read_asset(f"{rid}.asset")
        for g in comp:
            if g not in text:
                errors.append(f"{rid}: composition 缺 {g}")
        if "optionalSOs: []" not in text:
            errors.append(f"{rid}: optionalSOs 应为空（全必选）")
        score = re.search(r"score: (\d+)", text)
        expect = 80 if rid == "Web_Seafood_Pasta" else 60
        if not score or int(score.group(1)) != expect:
            errors.append(f"{rid}: 分数应为 {expect}")
        # 盘体模型
        if "413f3858a18f067c6ba9a912447276ed" not in text:
            errors.append(f"{rid}: 未引用本地盘体 prefab")
    # 中间产物：type=2 Cooked、score=0、composition 指向白名单食材
    for key, ing in (("boiled", "7ac8ffa9"), ("fish", "bbf0e926"), ("prawn", "df4a04d1")):
        rel = {
            "boiled": "orderdefinitions/Web_Seafood_Pasta_BoiledWrapper.asset",
            "fish": "orderdefinitions/Web_Seafood_Pasta_FishNode.asset",
            "prawn": "orderdefinitions/Web_Seafood_Pasta_PrawnNode.asset",
        }[key]
        text = read_asset(rel)
        if "type: 2" not in text:
            errors.append(f"{rel}: 应为 Cooked(2)")
        if "score: 0" not in text:
            errors.append(f"{rel}: 中间产物 score 应为 0")
        if ing not in text:
            errors.append(f"{rel}: composition 未指向食材 SO {ing}")


def check_lookup_uids():
    expect = {
        "Web_Pasta_Clone_Pasta": UIDS["pastaNode"],
        "Web_Pasta_Clone_SushiFish": UIDS["sushiFishNode"],
        "Web_Pasta_Clone_SushiPrawn": UIDS["sushiPrawnNode"],
        "Web_Pasta_Step_Pot": UIDS["potStep"],
        "Web_Pasta_Step_FryingPan": UIDS["fryingPanStep"],
    }
    for name, uid in expect.items():
        text = read_asset(f"lookup_nodes/{name}.asset")
        m = re.search(r"m_uID: (\d+)", text)
        if not m or int(m.group(1)) != uid:
            errors.append(f"lookup_nodes/{name}: uID 期望 {uid} 实际 {m and m.group(1)}")
    # lookup 引用齐全
    lookup = read_asset("lookups/Web_Seafood_Pasta_ComboLookup.asset")
    for name in ("Web_Pasta_Clone_BoiledPasta", "Web_Pasta_Clone_FriedSushiFish",
                 "Web_Pasta_Clone_FriedSushiPrawn"):
        g = guid_of(f"lookup_nodes/{name}.asset")
        if g not in lookup:
            errors.append(f"ComboLookup 缺克隆 {name}")
    for pf in ("plated_pasta_01", "plated_pasta_fish_01", "plated_pasta_prawn_01",
               "recipe_seafood_pasta_01"):
        g = guid_of(f"models/{pf}.prefab")
        if g not in lookup:
            errors.append(f"ComboLookup 缺本地模型 {pf}")


def check_names_icons():
    data = json.loads(open(os.path.join(W5, "custom_recipes/names.json"),
                           encoding="utf-8-sig").read())
    ids = {n["id"] for n in data["names"]}
    for rid in ("Web_Seafood_Pasta", "Web_Fish_Pasta", "Web_Prawn_Pasta"):
        if rid not in ids:
            errors.append(f"names.json 缺 {rid}")
        icon = os.path.join(PASTA, "icons", rid + ".png")
        if not os.path.exists(icon):
            errors.append(f"缺图标 {rid}.png")
        web = os.path.join(REPO, "layout-editor/web/public/icons/recipes", rid + ".png")
        if not os.path.exists(web):
            warns.append(f"web public 缺图标 {rid}.png")


def check_mesh_mats():
    for pf, n_mat in (("plated_pasta_01", 1), ("plated_pasta_fish_01", 2),
                      ("plated_pasta_prawn_01", 2), ("recipe_seafood_pasta_01", 3)):
        text = read_asset(f"models/{pf}.prefab")
        mats = re.findall(r"- \{fileID: 2100000, guid: ([0-9a-f]{32})", text)
        if len(mats) != n_mat:
            errors.append(f"{pf}: 材质槽数 {len(mats)} ≠ {n_mat}")
        mesh = re.search(r"m_Mesh: \{fileID: 4300000, guid: ([0-9a-f]{32})", text)
        if not mesh:
            errors.append(f"{pf}: 未引用本地 mesh")
    # 材质 → 本地 shader + 本地贴图
    for fn in os.listdir(os.path.join(PASTA, "models/materials")):
        if not fn.endswith(".mat"):
            continue
        text = read_asset(f"models/materials/{fn}")
        if "WebPlatedFood" not in open(os.path.join(PASTA, "models/WebPlatedFood.shader")).read()[:60]:
            pass
        shader = re.search(r"m_Shader: \{fileID: 4800000, guid: ([0-9a-f]{32})", text)
        shader_guid = guid_of("models/WebPlatedFood.shader")
        if not shader or shader.group(1) != shader_guid:
            errors.append(f"{fn}: 未使用本地 WebPlatedFood shader")


check_yaml_refs()
check_keep_guids()
check_recipe_chain()
check_lookup_uids()
check_names_icons()
check_mesh_mats()

for w in warns:
    print("WARN ", w)
if errors:
    for e in errors:
        print("FAIL ", e)
    sys.exit(1)
print("VALIDATE PASS — pasta 资产引用链完整，无原版直连 guid")
