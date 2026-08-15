#!/usr/bin/env node
/**
 * import-dlc-content.mjs — 一次性生成游戏 DLC 内容源库（供编辑器扫描 + 按需拷贝打包）。
 *
 * 输入：dump_bundle/manifest.json（游戏 AssetBundle 导出清单，bundle + container 精确查表）
 * 输出（全部在插件目录内，遵守项目铁律）：
 *   Assets/Editor/LayoutEditor/Import/Ingredients/dlcXX/<id>.asset      PseudoPrefabSO 食材/中间产物
 *   Assets/Editor/LayoutEditor/Import/Recipes/dlcXX/<id>.asset          PseudoPrefabSORecipe 菜谱
 *   Assets/Editor/LayoutEditor/Import/CookingSteps/<id>.asset           烹饪步骤（HotPot/RoastingTray）
 *   Assets/Editor/LayoutEditor/Import/prefabs/{category}/<id>.prefab    道具占位 prefab
 *   Assets/Editor/LayoutEditor/Import/pseudo_prefab_so/{category}/<id>.asset  道具 PseudoPrefabSO
 * 每个 .asset/.prefab 同时生成确定性 .meta（md5(id) 派生，重复运行内容不变）。
 *
 * 用法：node layout-editor/scripts/import-dlc-content.mjs [--dry]
 */
import fs from "fs";
import path from "path";
import crypto from "crypto";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, "../..");
const MANIFEST = path.join(repoRoot, "dump_bundle", "manifest.json");
const OUT_ROOT = path.join(repoRoot, "Assets/Editor/LayoutEditor/Import");
const ING_JSON = path.join(repoRoot, "layout-editor/web/public/ingredients.json");

const DRY = process.argv.includes("--dry");

// ---------------------------------------------------------------------------
// Script guids (same as common assets)
// ---------------------------------------------------------------------------
const GUID = {
  PseudoPrefabSO: "0cff7c13895ab9e47a5e02d4619cc3b9",
  PseudoPrefabSORecipe: "753d9e70603f6a140b05f30f176ec2dd",
  PseudoPrefabStub: "0f66cc8b36034eb4c8eec31e1994e471",
  PseudoPrefab: "d58b99f9c4313714e9c4b11f1534ae6f",
  PseudoPrefabMeshWithMaterialStub: "112ba37cd9beca344a6d81d8d70ce245",
  PseudoPrefabMeshWithMaterial: "0b5a5966234bbe045815f7430d55e69d",
};

// ---------------------------------------------------------------------------
// Manifest index
// ---------------------------------------------------------------------------
const manifest = JSON.parse(fs.readFileSync(MANIFEST, "utf8"));
/** lowerContainer -> first object entry */
const containerIndex = new Map();
for (const o of manifest.objects) {
  const c = (o.container || "").toLowerCase();
  if (!containerIndex.has(c)) containerIndex.set(c, o);
}

/** Find a GameObject/prefab container whose path ends with `/name.prefab` (exact basename). */
function prefabContainer(name, { exact = true } = {}) {
  const target = name.toLowerCase();
  for (const [c, o] of containerIndex) {
    if (!c.endsWith(".prefab")) continue;
    const base = c.slice(c.lastIndexOf("/") + 1, -".prefab".length);
    if (exact ? base === target : base.startsWith(target)) return o;
  }
  return null;
}

/** Find any container (asset) whose path contains every segment (order independent). */
function assetContainer(...segments) {
  const segs = segments.map((s) => s.toLowerCase());
  for (const [c, o] of containerIndex) {
    if (segs.every((s) => c.includes(s))) return o;
  }
  return null;
}

function guid(prefix, id) {
  return crypto.createHash("md5").update(`${prefix}:${id}`).digest("hex");
}

// ---------------------------------------------------------------------------
// Output helpers
// ---------------------------------------------------------------------------
let written = 0;

function write(file, content) {
  if (DRY) {
    console.log("  [dry] " + path.relative(repoRoot, file));
    return;
  }
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content, "utf8");
  written++;
}

function metaYaml(g) {
  return `fileFormatVersion: 2
guid: ${g}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}

function pseudoPrefabAsset(id, prefabName, bundleName, assetPath) {
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${GUID.PseudoPrefabSO}, type: 3}
  m_Name: ${id}
  m_EditorClassIdentifier: 
  prefabName: ${prefabName}
  bundleName: ${bundleName}
  assetPath: ${assetPath}
`;
}

function recipeAsset(id, prefabName, bundleName, assetPath, score) {
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${GUID.PseudoPrefabSORecipe}, type: 3}
  m_Name: ${id}
  m_EditorClassIdentifier: 
  prefabName: ${prefabName}
  bundleName: ${bundleName}
  assetPath: ${assetPath}
  score: ${score}
`;
}

/** Placeholder prefab for placeable props (mirrors Assets/common02 Blender/Airbed templates). */
function propPrefab(id, pseudoGuid, isDecor) {
  const stubScript = isDecor ? GUID.PseudoPrefabMeshWithMaterialStub : GUID.PseudoPrefabStub;
  const secondScript = isDecor ? GUID.PseudoPrefabMeshWithMaterial : GUID.PseudoPrefab;
  const go = 1554132891853676;
  const tr = 4341917854781290;
  const c1 = 114096038827637246;
  const c2 = 114967938430519454;
  const field = isDecor ? "  materialSO: {fileID: 0}\n" : "";
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1001 &100100000
Prefab:
  m_ObjectHideFlags: 1
  serializedVersion: 2
  m_Modification:
    m_TransformParent: {fileID: 0}
    m_Modifications: []
    m_RemovedComponents: []
  m_ParentPrefab: {fileID: 0}
  m_RootGameObject: {fileID: ${go}}
  m_IsPrefabParent: 1
--- !u!1 &${go}
GameObject:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  serializedVersion: 5
  m_Component:
  - component: {fileID: ${tr}}
  - component: {fileID: ${c1}}
  - component: {fileID: ${c2}}
  m_Layer: 0
  m_Name: ${id}
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &${tr}
Transform:
  m_ObjectHideFlags: 1
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${go}}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &${c1}
MonoBehaviour:
  m_ObjectHideFlags: 1
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${go}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${stubScript}, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  pseudoPrefabSO: {fileID: 11400000, guid: ${pseudoGuid}, type: 2}
${field}--- !u!114 &${c2}
MonoBehaviour:
  m_ObjectHideFlags: 1
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${go}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${secondScript}, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  childGameObject: {fileID: 0}
`;
}

// ---------------------------------------------------------------------------
// Curated content lists (verified against dump_bundle)
// ---------------------------------------------------------------------------

/** 新增食材：id -> dlc。不包含已导入的 dlc02/dlc05（检查见下文）。 */
const INGREDIENTS = {
  "03": ["dlc03_chocolate", "driedfruit", "marshmallow", "milk", "orange", "whippedcream"],
  "04": ["bokchoy", "dlc04_meat", "dlc04_orange", "dlc04_prawn", "grapes", "noodles", "peach"],
  "07": ["apple", "beef_roast", "blackberry", "broccoli", "cherry", "chicken_roast", "dlc07_cheese", "dlc07_potato", "leek"],
  "08": ["dlc08_bun", "dlc08_cheesesticks", "dlc08_chicken", "dlc08_onion", "dlc08_onion_ring", "dlc08_potato", "drink01", "drink02", "drink03", "frankfurter", "hotdogbun", "ketchup", "mustard", "raspberry"],
  "09": ["dlc09_beef_roast", "dlc09_broccoli", "dlc09_carrot", "dlc09_chicken_roast", "dlc09_chocolate", "dlc09_driedfruit", "dlc09_egg", "dlc09_flour", "dlc09_marshmallow", "dlc09_milk", "dlc09_orange", "dlc09_pancakestrawberry", "dlc09_potato", "dlc09_whippedcream"],
  "10": ["dlc10_bokchoy", "dlc10_grapes", "dlc10_meat", "dlc10_noodles", "dlc10_orange", "dlc10_peach", "dlc10_prawn"],
  "11": ["corn", "dlc11_cucumber", "dlc11_frankfurter", "dlc11_hotdogbun", "dlc11_ketchup", "dlc11_lettuce", "dlc11_milk", "dlc11_mustard", "dlc11_onion", "dlc11_tomato", "dlc11onion_salad", "icecube", "orangesoda", "rootbeer", "vanilla"],
  "13": ["dlc13_chocolate", "dlc13_egg", "dlc13_flour", "dlc13_grapes", "dlc13_melon", "dlc13_orange", "dlc13_peach", "dlc13_strawberry"],
};

/**
 * 食材 prefab 别名：部分食材在 bundle 中无同名 prefab（或名字不同），
 * 运行时 LoadAsset<GameObject>(assetPath) 必须命中真实 prefab，否则返回 null。
 * 值 = bundle 中实际存在的 prefab 容器 basename。
 */
const INGREDIENT_PREFAB_FIX = {
  "corn": "dlc11_corn",
  "vanilla": "dlc11_vanilla",
  "dlc08_bun": "hotdogbun",
  "dlc08_cheesesticks": "dlc08_cheese_sticks",
};

/**
 * 无可用 prefab 的食材（bundle 中不存在任何对应 GameObject prefab，仅 orderdefinitions
 * 资产）。运行时无法 LoadAsset<GameObject> —— 必须由 AutoFillIngredients 从
 * allIngredients 排除，食材箱配置这类食材也会在运行时失败。
 */
const NO_PREFAB_INGREDIENTS = new Set([
  "ketchup", "mustard", "orangesoda", "rootbeer", "dlc11onion_salad",
  "dlc11_ketchup", "dlc11_mustard",
]);

/** 中间产物菜谱（mixedingredients / cookedingredients，score=0，与 dlc02 的
 *  MixedFlourEggStrawberry 先例一致：作为 PseudoPrefabSORecipe 而非食材资产）。
 *  key = container 子目录。 */
const INTERMEDIATE_RECIPES = {
  mixedingredients: {
    "03": ["mixedfloureggdriedfruit", "mixedfloureggdriedfruitorange"],
    "07": ["mixedfloureggapple", "mixedfloureggappleblackberry", "mixedfloureggapplecherry", "mixedfloureggblackberry", "mixedfloureggcherry"],
    "08": ["mixedfloureggraspberry"],
    "09": ["dlc09_mixedflouregg", "dlc09_mixedfloureggchocolate", "dlc09_mixedfloureggdriedfruit", "dlc09_mixedfloureggdriedfruitorange", "dlc09_mixedfloureggstrawberry"],
    "13": ["dlc13_mixedfloureggchocolate", "dlc13_mixedfloureggchocolatestrawberry", "dlc13_mixedfloureggstrawberries", "dlc13_mixedfloureggwatermelon"],
  },
  cookedingredients: {
    "05": ["roastedmarshmallow"],
    "08": ["boiledfrankfurter", "dlc08_friedchips", "friedcheese_sticks", "friedchickenburger", "friedonion_rings", "friedonions"],
    "11": ["dlc11_boiledfrankfurter", "dlc11_friedonions"],
  },
};

/** 新菜谱（recipeitems 枚举，exclude optional 已按前缀判 0 分）；dlc02 仅补漏。 */
const RECIPES = {
  "02": ["chocolatesmoothie", "meatpineapplemushroomchickenkebob"],
  "03": ["christmaspudding", "christmaspuddingwithorange", "hotchocolate", "hotchocolatecream", "hotchocolatemallow", "hotchocolatemallowcream", "optionalhotchocolate"],
  "04": ["fruitplatter_grapespeach", "fruitplatter_orangegrapes", "fruitplatter_orangepeach", "fruitplatter_orangepeachgrapes", "hotpot_doublemeat", "hotpot_doubleprawn", "hotpot_meat", "hotpot_mixed", "hotpot_permutationscooked", "hotpot_permutationsraw", "hotpot_prawn"],
  "07": ["beefpotatocarrotbroccoliroast", "beefpotatocarrotroast", "chickenpotatocarrotbroccoliroast", "chickenpotatocarrotroast", "fruitpie_apple", "fruitpie_appleblackberry", "fruitpie_applecherry", "fruitpie_blackberry", "fruitpie_cherry", "onionbroccolicheesesoup", "onioncarrotpotatosoup", "onionpotatosoupleek", "optionalroast", "optionalsoup"],
  "08": ["cheesesticks", "chickenburger", "donut_chocolate", "donut_plain", "donut_raspberry", "hotdog_ketchup", "hotdog_ketchup_mustard", "hotdog_mustard", "hotdog_onions", "hotdog_onions_ketchup", "hotdog_onions_mustard", "hotdog_plain", "md_burger_cheesesticks_drink03", "md_burger_drink01", "md_burger_fries", "md_burger_fries_cheesesticks", "md_burger_fries_drink02", "md_burger_onionrings", "md_burger_onionrings_cheesesticks", "md_burger_onionrings_drink01", "md_c_burger_cheesesticks", "md_c_burger_cheesesticks_drink02", "md_c_burger_drink03", "md_c_burger_fries_cheesesticks", "md_c_burger_fries_drink03", "md_c_burger_fries_onionrings", "md_c_burger_onionrings", "md_c_burger_onionrings_drink01", "onionrings", "optional_bun_frank_onions_ketchup_mustard", "optional_bun_ketchup", "optional_bun_ketchup_mustard", "optional_bun_mustard", "optional_bun_onions", "optional_bun_onions_ketchup", "optional_bun_onions_ketchup_mustard", "optional_bun_onions_mustard", "optional_frank_onions_ketchup_mustard", "optional_frankfurter_ketchup", "optional_frankfurter_mustard", "optional_frankfurter_mustard_ketchup", "optional_frankfurter_onions", "optional_frankfurter_onions_ketchup", "optional_frankfurter_onions_mustard", "optional_onions_ketchup", "optional_onions_ketchup_mustard", "optional_onions_mustard", "optionalhotdogs", "optionalmd"],
  "09": ["dlc09_beefpotatocarrotbroccoliroast", "dlc09_beefpotatocarrotroast", "dlc09_chickenpotatocarrotbroccoliroast", "dlc09_chickenpotatocarrotroast", "dlc09_christmaspudding", "dlc09_christmaspuddingwithorange", "dlc09_hotchocolate", "dlc09_hotchocolatecream", "dlc09_hotchocolatemallow", "dlc09_hotchocolatemallowcream", "dlc09_pancake_chocolate", "dlc09_pancake_plain", "dlc09_pancake_strawberry", "dlc09_optionalhotchocolate"],
  "10": ["dlc10_fruitplatter_grapespeach", "dlc10_fruitplatter_orangegrapes", "dlc10_fruitplatter_orangepeach", "dlc10_fruitplatter_orangepeachgrapes", "dlc10_hotpot_doublemeat", "dlc10_hotpot_doubleprawn", "dlc10_hotpot_meat", "dlc10_hotpot_mixed", "dlc10_hotpot_permutationscooked", "dlc10_hotpot_permutationsraw", "dlc10_hotpot_prawn"],
  "11": ["dlc11_hotdog_ketchup", "dlc11_hotdog_ketchup_mustard", "dlc11_hotdog_mustard", "dlc11_hotdog_onions", "dlc11_hotdog_onions_ketchup", "dlc11_hotdog_onions_mustard", "dlc11_hotdog_plain", "icecream_chocolate", "icecream_vanilla", "orangesodafloat_chocolate", "orangesodafloat_vanilla", "rootbeerfloat_chocolate", "rootbeerfloat_vanilla", "salad_corn_onion", "salad_cucumber_onion", "salad_cucumber_tomato_onion", "salad_tomato_onion", "tomato_corn_onion", "tomato_cucumber_onion", "dlc11_optional_bun_frank_onions_ketchup_mustard", "dlc11_optional_bun_ketchup", "dlc11_optional_bun_ketchup_mustard", "dlc11_optional_bun_mustard", "dlc11_optional_bun_onions", "dlc11_optional_bun_onions_ketchup", "dlc11_optional_bun_onions_ketchup_mustard", "dlc11_optional_bun_onions_mustard", "dlc11_optional_frank_onions_ketchup_mustard", "dlc11_optional_frankfurter_ketchup", "dlc11_optional_frankfurter_mustard", "dlc11_optional_frankfurter_mustard_ketchup", "dlc11_optional_frankfurter_onions", "dlc11_optional_frankfurter_onions_ketchup", "dlc11_optional_frankfurter_onions_mustard", "dlc11_optional_onions_ketchup", "dlc11_optional_onions_ketchup_mustard", "dlc11_optional_onions_mustard", "dlc11_optionalhotdogs", "dlc11_optionalsalad", "optional_chocolateorangefloat", "optional_chocolaterootfloat", "optional_vanillaorangefloat", "optional_vanillarootfloat"],
  "13": ["dlc13_fruitplatter_grapespeach", "dlc13_fruitplatter_orangegrapes", "dlc13_fruitplatter_orangepeach", "dlc13_fruitplatter_orangepeachgrapes", "dlc13_moonpie_chocolate", "dlc13_moonpie_chocolatestrawberry", "dlc13_moonpie_strawberry", "dlc13_moonpie_watermelon"],
};

/** 烹饪步骤（CookingStepData，资产只导一套，dlc10/dlcd09 复用）。 */
const COOKING_STEPS = {
  HotPot: ["dlc04", "data/recipes/cookingstepdata/hotpot.asset"],
  RoastingTray: ["dlc07", "data/recipes/cookingstepdata/roastingtray.asset"],
};

/**
 * 核心道具（80 个）。category: utensils / mechanisms / counters / art(theme)。
 * 细分在 build-catalog.mjs 的 UTENSIL_SUBCATEGORY / COUNTER_SUBCATEGORY 完成。
 */
const PROPS = [
  // ---- dlc03 圣诞/热可可 ----
  ["utensil_ingredient_spray_01", "utensils"], ["dlc03_utensil_mixer", "utensils"], ["dlc03_utensil_pot", "utensils"],
  ["equipment_mug_01", "utensils"], ["cleanmugstack", "utensils"], ["dirtymug", "utensils"], ["dirtymugstack", "utensils"],
  ["workstation_mug_return", "counters"], ["workstation_sink_mug_01_wood", "counters"], ["workstation_mixer_01", "counters"],
  ["dispenser_crate_04", "art/dlc03_christmas"],
  // ---- dlc09 仙境/热可可 ----
  ["dlc09_utensil_ingredient_spray", "utensils"], ["dlc09_utensil_frying_pan", "utensils"], ["dlc09_utensil_mixer", "utensils"], ["dlc09_utensil_pot", "utensils"], ["dlc09_utensil_roasting_tray", "utensils"],
  ["dlc09_equipment_mug_01", "utensils"], ["dlc09_cleanmugstack", "utensils"], ["dlc09_dirtymug", "utensils"], ["dlc09_dirtymugstack", "utensils"],
  ["dlc09_oven", "counters"], ["dlc09_workstation_mug_return_winter", "counters"], ["dlc09_workstation_sink_mug_01_wood", "counters"],
  ["dlc09_dispenser_crate_winter", "art/dlc09_wonderland"],
  // ---- dlc11 夏季饮料 ----
  ["dlc11_drink_dispenser", "counters"], ["dlc11_condiment_dispenser", "counters"],
  ["dlc11_equipment_glass_01", "utensils"], ["dlc11_cleanglassstack", "utensils"], ["dlc11_dirtyglass", "utensils"], ["dlc11_dirtyglassstack", "utensils"],
  ["dlc11_workstation_glass_return_01", "counters"], ["workstation_sink_01_summer", "counters"],
  // ---- dlc04 火锅 ----
  ["cooking_region_floorburner", "counters"], ["utensil_large_pot_01", "utensils"], ["utensil_big_ol_spoon", "utensils"], ["pushable_object", "mechanisms"],
  // ---- dlc10 火锅 ----
  ["dlc10_cooking_region_floorburner", "counters"], ["utensil_dlc10_large_pot_01", "utensils"], ["utensil_dlc10_big_ol_spoon", "utensils"], ["dlc10_pushable_object", "mechanisms"],
  ["dlc10_workstation_mixer", "counters"], ["dlc10_dispenser_crate", "art/dlc10"],
  // ---- dlc07 部落/烤炉 ----
  ["oven_furnace_medieval", "counters"], ["oven_medieval", "counters"], ["workstation_furnace_01", "counters"],
  ["utensil_roasting_tray", "utensils"], ["utensil_coalbucket_01", "utensils"],
  ["dlc07_utensil_frying_pan_01", "utensils"], ["dlc07_utensil_mixer_01", "utensils"], ["dlc07_utensil_pot_01", "utensils"],
  ["workstation_mixer_03", "counters"], ["dispenser_coal_01", "art/dlc07_horde"], ["dispenser_crate_05", "art/dlc07_horde"],
  ["dlc07_coal", "mechanisms"], ["p_dlc7_coal_bucket_coal_01", "utensils"],
  // ---- dlc08 马戏团 ----
  ["dlc08_drink_machine", "counters"], ["dlc08_condiment_dispenser", "counters"], ["dlc08_oven_02", "counters"],
  ["dlc08_utensil_fire_extinguisher", "utensils"], ["dlc08_utensil_frying_pan", "utensils"], ["dlc08_utensil_mixer_01", "utensils"], ["dlc08_utensil_pot_01", "utensils"], ["dlc08_frierbasket", "utensils"], ["dlc08_equipment_tray", "utensils"],
  ["dlc08_cleantraystack", "utensils"], ["dlc08_dirtytray", "utensils"], ["dlc08_dirtytraystack", "utensils"], ["dlc08_dispenser_crate_circus", "art/dlc08_circus"],
  ["dlc08_workstation_mixer", "counters"], ["dlc08_workstation_tray_return", "counters"],
  // ---- dlc13 巧克力节 ----
  ["dlc13_workstation_cooker_01", "counters"], ["dlc13_lotuspressureswitch_large", "mechanisms"], ["dlc13_lotuspressureswitch_small", "mechanisms"],
  ["dlc13_utensil_mixer_01", "utensils"], ["dlc13_workstation_bin_01", "counters"], ["dlc13_workstation_mixer_01", "counters"],
  ["dlc13_workstation_plate_return", "counters"], ["dlc13_workstation_plate_station", "counters"], ["dlc13_workstation_sink_01_wood", "counters"],
  ["dlc13_dispenser_crate_camping_new", "art/dlc13"],
];

// ---------------------------------------------------------------------------
// Already-imported ids (common01/common02 only) — skip those
// ---------------------------------------------------------------------------
function alreadyImported() {
  const set = new Set();
  // common01/common02 下已存在的食材/菜谱（含 dlc02/dlc05 子目录）
  for (const root of ["Assets/common01/food/Ingredients", "Assets/common02/food/Ingredients",
    "Assets/common01/food/Recipes", "Assets/common02/food/Recipes"]) {
    const abs = path.join(repoRoot, root);
    if (!fs.existsSync(abs)) continue;
    const walk = (dir) => {
      for (const n of fs.readdirSync(dir)) {
        const p = path.join(dir, n);
        if (fs.statSync(p).isDirectory()) walk(p);
        else if (n.endsWith(".asset")) {
          const txt = fs.readFileSync(p, "utf8");
          const m = txt.match(/m_Name:\s*(\S+)/);
          if (m) set.add(m[1].toLowerCase());
        }
      }
    };
    walk(abs);
  }
  return set;
}

// ---------------------------------------------------------------------------
// Emit ingredients
// ---------------------------------------------------------------------------
function emitIngredients() {
  const imported = alreadyImported();
  let count = 0;
  for (const [dlc, list] of Object.entries(INGREDIENTS)) {
    const dir = path.join(OUT_ROOT, "Ingredients", `dlc${dlc}`);
    for (const id of [...new Set(list)]) {
      if (imported.has(id.toLowerCase())) {
        console.log(`  跳过（已导入）: ${id}`);
        continue;
      }
      // prefab 容器：先精确名，再别名表（运行时 LoadAsset<GameObject> 必须命中 prefab）
      let o = prefabContainer(id);
      if (!o && INGREDIENT_PREFAB_FIX[id]) {
        o = prefabContainer(INGREDIENT_PREFAB_FIX[id]);
      }
      if (!o && !NO_PREFAB_INGREDIENTS.has(id)) {
        console.warn(`  !! 找不到食材 prefab: ${id}`);
        continue;
      }
      // 无 prefab 食材：assetPath 指向 order 资产（运行时不可加载，AutoFill 会排除）
      if (!o) {
        o = assetContainer("orderdefinitions", "ingredients", `${id}.asset`);
        if (!o) {
          console.warn(`  !! 找不到容器: ${id}`);
          continue;
        }
      }
      const assetPath = "Assets/" + o.container.replace(/^assets\//, "");
      const prefabName = o.container.includes(".prefab")
        ? o.container.slice(o.container.lastIndexOf("/") + 1, -".prefab".length)
        : id;
      const file = path.join(dir, `${id}.asset`);
      write(file, pseudoPrefabAsset(id, prefabName, o.bundle, assetPath));
      write(file + ".meta", metaYaml(guid("ing", id)));
      count++;
    }
  }
  console.log(`食材生成: ${count}`);
}

// ---------------------------------------------------------------------------
// Emit recipes (main dishes + intermediates)
// ---------------------------------------------------------------------------
function emitRecipes() {
  const imported = alreadyImported();
  let count = 0;
  const emit = (dlc, id, sub, score) => {
    if (imported.has(id.toLowerCase()) && dlc !== "02") {
      console.log(`  跳过（已导入）: ${id}`);
      return;
    }
    const o = assetContainer("orderdefinitions", sub, `${id}.asset`);
    if (!o) {
      console.warn(`  !! 找不到菜谱容器: ${id}`);
      return;
    }
    const assetPath = "Assets/" + o.container.replace(/^assets\//, "");
    const prefabName = camelize(id.replace(/^dlc\d+_/, ""));
    const dir = path.join(OUT_ROOT, "Recipes", `dlc${dlc}`);
    const file = path.join(dir, `${id}.asset`);
    write(file, recipeAsset(id, prefabName, o.bundle, assetPath, score));
    write(file + ".meta", metaYaml(guid("recipe", id)));
    count++;
  };
  for (const [dlc, list] of Object.entries(RECIPES)) {
    for (const id of [...new Set(list)]) {
      const score = /^optional|_optional/.test(id) || id.includes("permutation") ? 0 : 100;
      emit(dlc, id, "recipeitems", score);
    }
  }
  for (const [sub, byDlc] of Object.entries(INTERMEDIATE_RECIPES)) {
    for (const [dlc, list] of Object.entries(byDlc)) {
      for (const id of [...new Set(list)]) emit(dlc, id, sub, 0);
    }
  }
  console.log(`菜谱生成: ${count}`);
}

// ---------------------------------------------------------------------------
// Emit cooking steps
// ---------------------------------------------------------------------------
function emitCookingSteps() {
  let count = 0;
  for (const [name, [dlc, sub]] of Object.entries(COOKING_STEPS)) {
    const o = assetContainer("dlc" + dlc, sub.split("/")[0], name.toLowerCase() + ".asset") ||
      assetContainer(sub, name.toLowerCase() + ".asset");
    if (!o) {
      console.warn(`  !! 找不到烹饪步骤容器: ${name}`);
      continue;
    }
    const assetPath = "Assets/" + o.container.replace(/^assets\//, "");
    const file = path.join(OUT_ROOT, "CookingSteps", `${name}.asset`);
    write(file, pseudoPrefabAsset(name, name, o.bundle, assetPath));
    write(file + ".meta", metaYaml(guid("step", name)));
    count++;
  }
  console.log(`烹饪步骤生成: ${count}`);
}

// ---------------------------------------------------------------------------
// Emit props (placeholder prefab + pseudo asset)
// ---------------------------------------------------------------------------
function emitProps() {
  let count = 0;
  for (const [id, category] of PROPS) {
    const o = prefabContainer(id);
    if (!o) {
      // tolerate dump names like "workstation_mixer_01 (1)"
      const loose = prefabContainer(id, { exact: false });
      if (!loose) {
        console.warn(`  !! 找不到道具容器: ${id}`);
        continue;
      }
    }
    const found = o ?? prefabContainer(id, { exact: false });
    const assetPath = "Assets/" + found.container.replace(/^assets\//, "");
    const isDecor = category.startsWith("art/");
    const catDir = isDecor ? category.replace("art/", "art/") : category;

    const pseudoDir = path.join(OUT_ROOT, "pseudo_prefab_so", catDir);
    const prefabDir = path.join(OUT_ROOT, "prefabs", catDir);
    const pseudoGuid = guid("pseudo", id);

    const pseudoFile = path.join(pseudoDir, `${id}.asset`);
    write(pseudoFile, pseudoPrefabAsset(id, id, found.bundle, assetPath));
    write(pseudoFile + ".meta", metaYaml(pseudoGuid));

    const prefabFile = path.join(prefabDir, `${id}.prefab`);
    write(prefabFile, propPrefab(id, pseudoGuid, isDecor));
    write(prefabFile + ".meta", metaYaml(guid("prefab", id)));
    count++;
  }
  console.log(`道具生成: ${count}`);
}

function camelize(s) {
  return s
    .split(/[_\s]+/)
    .filter(Boolean)
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join("");
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------
console.log(`生成源库 → ${path.relative(repoRoot, OUT_ROOT)}${DRY ? "（dry-run）" : ""}`);
emitIngredients();
emitRecipes();
emitCookingSteps();
emitProps();
console.log(`完成，共写出 ${written} 个文件${DRY ? "（未落盘）" : ""}。`);
