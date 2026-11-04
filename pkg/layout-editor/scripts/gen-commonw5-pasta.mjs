#!/usr/bin/env node
// gen-commonw5-pasta.mjs — Web 意面家族生成器（v2，2026-11 重写）
//
// 生成内容（全部位于 Assets/commonW5，绝不触碰其它 common 库）：
//   菜谱：Web_Seafood_Pasta（海鲜意面 80 分 = 煮意面+煎寿司鱼+煎寿司虾，全必选）
//         Web_Fish_Pasta（鲜鱼意面 60 分 = 煮意面+煎寿司鱼）
//         Web_Prawn_Pasta（鲜虾意面 60 分 = 煮意面+煎寿司虾）
//   中间产物（CustomRecipeSO type=Cooked，score=0，运行时由 RecipeHelper
//     转成真节点）：Web_Seafood_Pasta_BoiledWrapper（煮意面=锅+PastaSO 懒加载）
//     / _FishNode（煎寿司鱼=FryingPan+SushiFishSO）/ _PrawnNode（煎寿司虾）
//   盘内组合视觉：Web_Seafood_Pasta_Plate（盘体，保留）+
//     Web_Seafood_Pasta_ComboLookup（组合查找表，m_content=本地 uID 克隆节点，
//     m_prefab=本地拷贝的 4 个原版 plated 模型）。
//
// 与 v1 的关键差异（v1 三处游戏内断裂的根因修复）：
//   1) 不再对原版 bundle 内资产做直连 guid 引用（v1 的 BoiledWrapper/FishNode/
//      PrawnNode/ComboLookup 直连 vanilla guid → 运行时全 null →
//      LoadAsset 抛 KeyNotFoundException / 盘子组合视觉永远空）。
//      原版资产一律经本地 PseudoPrefabSO（bundleName+assetPath 懒加载）或
//      CustomRecipeSO（RecipeHelper 运行时建真节点）引用 —— 与 steamed/burrito/
//      milk_tea 同一铁律。
//   2) ComboLookup 的 m_content 用本地克隆节点：游戏匹配按 uID 值比较
//      （IngredientAssembledNode.IsMatch 比 m_uID；CookedCompositeAssembledNode
//      另比 cookingStep uID + progress），克隆节点带原版 uID 即可匹配。
//   3) ComboLookup 的 m_prefab 用拷贝进 commonW5 的原版 plated 模型
//      （mesh→OBJ 走 kettle 同款导入管线；材质换本地 WebPlatedFood shader；
//      贴图本地拷贝）——彻底消除跨 bundle 引用。
//
// 数据源：layout-editor/scripts/pasta/assets/（collect_pasta_assets.py 产出，
// 离线可重跑；Ripped 备份目录只在 collect 阶段读取）。
//
// 用法：node layout-editor/scripts/gen-commonw5-pasta.mjs [--apply]
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const W5 = "Assets/commonW5";
const apply = process.argv.includes("--apply");

const md5 = (s) => crypto.createHash("md5").update(s).digest("hex");
const recipeGuid = (id) => md5(`commonw5-recipe:${id}`);
const recipeIconGuid = (id) => md5(`commonw5-recipe-icon:${id}`);
const folderGuid = (rel) => md5(`commonw5-folder:${rel}`);
const nodeGuid = (id) => md5(`commonw5-pasta2:${id}`);

const SCRIPT_CUSTOM_RECIPE = "83fb008bcc8e793429b02c178c430815";
const SCRIPT_COOKED_NODE = "d7c45801d396cac93dced065fd1beb5c";
const SCRIPT_INGREDIENT_NODE = "bff1936f87185a161a744a226a12f22d";
const SCRIPT_COOKING_STEP = "e9f1538c1f529e3c938516c59ef1bee2";
const SCRIPT_COMBO_LOOKUP = "5cf9dad7c310aacfd0c9f38e2d36a851";
const SCRIPT_PASTA_COSMETIC = "2660a5e0f5b60def0b9be2014e6c0ff3";
// CustomStub.PastaOriginalModels（Assets/WebCustomStubRuntime/Pasta/）
const SCRIPT_PASTA_MODELS = "522db19fa76c6509496022ba1f630faf";
// AssignableOrderDefinition（原版 Assembly-CSharp；注意结尾 63d，v1 曾误抄 63f）
const SCRIPT_ASSIGNABLE_ORDER = "bd0b993e664fe1806c93b22b6fdac63d";
const SCRIPT_RENDERER_INFO = "34856dccebb76b09e2df91366afead1a";

// ── 本库保留 guid（文件内容重写、guid 不变，防引用漂移） ─────────────────
// 注意：保留 guid 仅适用于「路径不变」的资产。跨路径搬移绝不能复用旧 guid
// （Unity Library 缓存 guid→旧路径映射，新文件声称同一 guid 会触发
//  "Failed to unload" 断言——BoiledWrapper v2 首版踩过）。
const KEEP = {
  seafoodRecipe: "a2bf51c527a7713bbb5734aded8f83a7", // Web_Seafood_Pasta.asset
  seafoodIcon: "194b542045424df04cc384529d3fd89f", // icons/Web_Seafood_Pasta.png
  plate: "413f3858a18f067c6ba9a912447276ed", // models/Web_Seafood_Pasta_Plate.prefab
  lookup: "4165416d3ee79ad49f518e75525caa25", // lookups/Web_Seafood_Pasta_ComboLookup.asset
};

// ── 只读引用的外库懒加载 SO（bundleName+assetPath，运行时解析真资产） ─────
const REF = {
  plateStep: "02b04fdf6fef0e944870fd5e06375ed1", // common01 PlatingSteps/Plate
  pastaIngredient: "7ac8ffa91f182f14c8d14bfe5badd2ac", // common01 Ingredients/PastaSO
  sushiFishIngredient: "bbf0e92641aa4ab49bda6bc68117e01f", // common01 SushiFishSO
  sushiPrawnIngredient: "df4a04d1549f9ee41a991153bd723110", // common01 SushiPrawnSO
  potStep: "5db05a4f76f128a4b8297bdce834fe96", // common02 CookingSteps/Pot
  potIcon: "8b70d2ada088ef74e84700ee193b1812", // common02 CookingSteps/icons/PotIcon
  fryingPanStep: "72bee3db65786774683403e9c362cb97", // common02 CookingSteps/FryingPan
  fryingPanIcon: "292a7ea66aa4d1d48a3477fe3a56675c", // common02 CookingSteps/icons/FryingPanIcon
  platedFishModel: "f099b5194b8af4747789845c829644e5", // bundle47 prep_fish_sliced_01（原版煎鱼）
  platedPrawnModel: "72db978f4e5c06b4db758b931057670b", // commonW2 fried_prawn_model（原版煎虾 prawn_fried_01）
  // 煎虾「锅=盘同款」：单虾接盘模型与锅内一致 = commonW2 炸虾饼（圆形煎制物），
  // 根 GameObject fileID 取自 FriedShrimpCake.prefab 的 m_RootGameObject。
  prawnPanModelPrefab: "949a48257d6a82246918e3af3e81524e",
};

// ── 原版 uID（匹配语义：按值比较） ────────────────────────────────────────
const UID_VANILLA = JSON.parse(
  fs.readFileSync(path.join(repoRoot, "layout-editor/scripts/pasta/assets/uids.json"), "utf8"),
);

const IDS = {
  final: "Web_Seafood_Pasta",
  fishFinal: "Web_Fish_Pasta",
  prawnFinal: "Web_Prawn_Pasta",
  boiledWrapper: "Web_Seafood_Pasta_BoiledWrapper",
  fishNode: "Web_Seafood_Pasta_FishNode",
  prawnNode: "Web_Seafood_Pasta_PrawnNode",
  toppingsCombo: "Web_Seafood_Pasta_ToppingsCombo",
  lookup: "Web_Seafood_Pasta_ComboLookup",
  plate: "Web_Seafood_Pasta_Plate",
};

const uID = {
  final: 58323015,
  fishFinal: 58323020,
  prawnFinal: 58323021,
  boiledWrapper: 58323019,
  fishNode: 58323017,
  prawnNode: 58323018,
  toppingsCombo: 58323022,
};

const pastaDir = `${W5}/custom_recipes/pasta`;
const nodeDir = `${pastaDir}/orderdefinitions`;
const cloneDir = `${pastaDir}/lookup_nodes`;
const lookupDir = `${pastaDir}/lookups`;
const modelDir = `${pastaDir}/models`;
const meshDir = `${modelDir}/meshes`;
const matDir = `${modelDir}/materials`;
const texDir = `${modelDir}/textures`;
const iconDir = `${pastaDir}/icons`;

const ASSETS_SRC = path.join(repoRoot, "layout-editor/scripts/pasta/assets");
const prefabsInfo = JSON.parse(fs.readFileSync(path.join(ASSETS_SRC, "prefabs.json"), "utf8"));
const materialsInfo = JSON.parse(fs.readFileSync(path.join(ASSETS_SRC, "materials.json"), "utf8"));
const texMap = JSON.parse(fs.readFileSync(path.join(ASSETS_SRC, "texmap.json"), "utf8"));

// ── 本地拷贝的模型资产（guid 确定性派生） ────────────────────────────────
const meshGuid = (name) => nodeGuid(`mesh:${name}`);
const matGuid = (name) => nodeGuid(`mat:${name}`);
const texGuid = (name) => nodeGuid(`tex:${name}`);
const prefabGuid = (name) => nodeGuid(`prefab:${name}`);
const SHADER_GUID = nodeGuid("shader:WebPlatedFood");

// guid 派生表：源 guid（Ripped/原版）→ 本地拷贝 guid
const matGuidBySource = {};
for (const [src, info] of Object.entries(materialsInfo)) {
  matGuidBySource[src] = matGuid(info.name);
}
const prefabGuidByName = {};
for (const name of Object.keys(prefabsInfo)) {
  prefabGuidByName[name] = prefabGuid(name);
}
const meshGuidByMeshName = {};
for (const info of Object.values(prefabsInfo)) {
  meshGuidByMeshName[info.meshSourceName] = meshGuid(info.meshSourceName);
}

// ── Unity meta 模板 ──────────────────────────────────────────────────────
const ASSET_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
`;
const PREFAB_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 100100000
  userData:
  assetBundleName:
  assetBundleVariant:
`;
const MAT_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 2100000
  userData:
  assetBundleName:
  assetBundleVariant:
`;
const FOLDER_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
`;
const SHADER_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
ShaderImporter:
  externalObjects: {}
  defaultTextures: []
  userData:
  assetBundleName:
  assetBundleVariant:
`;

const spriteMetaTemplate = fs.readFileSync(
  path.join(repoRoot, `${W5}/custom_recipes/burrito/icons/Web_Burrito_Meat_Chicken.png.meta`),
  "utf8",
);
const SPRITE_META = (guid) =>
  spriteMetaTemplate
    .replace(/^guid: [0-9a-f]{32}$/m, `guid: ${guid}`)
    // 成品菜谱图标保持 2048（模板来自 burrito 512，会被 Unity 重新导入降采样）
    .replace(/^(\s*maxTextureSize: )\d+$/gm, "$12048");

const textureMetaTemplate = fs.readFileSync(
  path.join(repoRoot, `${W5}/kettle/textures/t_dlc100_Kettle_Anniversary_01_d.png.meta`),
  "utf8",
);
const TEX_META = (guid) =>
  textureMetaTemplate
    .replace(/^guid: [0-9a-f]{32}$/m, `guid: ${guid}`)
    .replace(/^timeCreated: \d+$/m, "timeCreated: 1793006110");

const objMetaTemplate = fs.readFileSync(
  path.join(repoRoot, `${W5}/kettle/meshes/m_kettle_anniversary_01.obj.meta`),
  "utf8",
);
function OBJ_META(guid, meshName) {
  let text = objMetaTemplate.replace(/^guid: [0-9a-f]{32}$/m, `guid: ${guid}`);
  // fileIDToRecycleName：根节点 + 每个子网格组（usemtl mat_i → Unity 材质组名）。
  // fileID 类型号：1=100000(6位)、4=400000(6位)、21=2100000、23=2300000、
  // 33=3300000、43=4300000、95=9500000（对齐 kettle m_kettle_anniversary_01.obj.meta）。
  const subCount = meshSubmeshCount(meshName);
  let entries = "    100000: //RootNode\n    400000: //RootNode\n    9500000: //RootNode\n";
  for (let i = 0; i < subCount; i++) {
    entries += `    ${100002 + i}: ${meshName}_${i}\n`;
    entries += `    ${400002 + i}: ${meshName}_${i}\n`;
    entries += `    ${2100000 + i}: ${meshName}_${i}Mat\n`;
    entries += `    ${2300000 + i}: ${meshName}_${i}\n`;
    entries += `    ${3300000 + i}: ${meshName}_${i}\n`;
  }
  entries += `    4300000: ${meshName}_0\n`;
  text = text.replace(/  fileIDToRecycleName:\n(?:    \d+: .*\n)+/, `  fileIDToRecycleName:\n${entries}`);
  return text;
}

function meshSubmeshCount(meshName) {
  for (const info of Object.values(prefabsInfo)) {
    if (info.meshSourceName === meshName) return info.meshSubmeshCount;
  }
  throw new Error(`mesh not tracked: ${meshName}`);
}

// ── YAML 生成器 ──────────────────────────────────────────────────────────
function customRecipeCookedYaml(id, uid, opts) {
  const modelSO = opts.modelSO ? `{fileID: 11400000, guid: ${opts.modelSO}, type: 2}` : "{fileID: 0}";
  const model = opts.model ? opts.model : "{fileID: 0}";
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
  m_Script: {fileID: 11500000, guid: ${SCRIPT_CUSTOM_RECIPE}, type: 3}
  m_Name: ${id}
  m_EditorClassIdentifier:
  type: 2
  recipeName: ${id}
  uID: ${uid}
  score: 0
  platingStepSO: {fileID: 11400000, guid: ${REF.plateStep}, type: 2}
  modelSO: ${modelSO}
  model: ${model}
  iconSO: {fileID: 0}
  icon: {fileID: 0}
  compositionSOs:
  - {fileID: 11400000, guid: ${opts.ingredient}, type: 2}
  optionalSOs: []
  cookingStepSO: {fileID: 11400000, guid: ${opts.cookStep}, type: 2}
  cookingStepIconSO: {fileID: 11400000, guid: ${opts.cookIcon}, type: 2}
  cookingStepIcon: {fileID: 0}
  cookingProgress: 1
  mixingIconSO: {fileID: 0}
  mixingIcon: {fileID: 0}
  mixingProgress: 0
`;
}

function finalRecipeYaml(id, uid, score, compositionGuids, iconGuid) {
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
  m_Script: {fileID: 11500000, guid: ${SCRIPT_CUSTOM_RECIPE}, type: 3}
  m_Name: ${id}
  m_EditorClassIdentifier:
  type: 1
  recipeName: ${id}
  uID: ${uid}
  score: ${score}
  platingStepSO: {fileID: 11400000, guid: ${REF.plateStep}, type: 2}
  modelSO: {fileID: 0}
  model: {fileID: 1709254077376921, guid: ${KEEP.plate}, type: 2}
  iconSO: {fileID: 0}
  icon: {fileID: 21300000, guid: ${iconGuid}, type: 3}
  compositionSOs:
${compositionGuids.map((g) => `  - {fileID: 11400000, guid: ${g}, type: 2}`).join("\n")}
  optionalSOs: []
  cookingStepSO: {fileID: 0}
  cookingStepIconSO: {fileID: 0}
  cookingStepIcon: {fileID: 0}
  cookingProgress: 0
  mixingIconSO: {fileID: 0}
  mixingIcon: {fileID: 0}
  mixingProgress: 0
`;
}

function ingredientCloneYaml(id, uid, colour) {
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
  m_Script: {fileID: 11500000, guid: ${SCRIPT_INGREDIENT_NODE}, type: 3}
  m_Name: ${id}
  m_EditorClassIdentifier:
  m_orderGuiDescription: []
  m_platingStep: {fileID: 0}
  m_platingPrefab: {fileID: 0}
  m_uID: ${uid}
  m_iconSprite: {fileID: 0}
  m_crateLid: {fileID: 0}
  m_colour: {r: ${colour[0]}, g: ${colour[1]}, b: ${colour[2]}, a: ${colour[3]}}
`;
}

function cookingStepCloneYaml(id, uid) {
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
  m_Script: {fileID: 11500000, guid: ${SCRIPT_COOKING_STEP}, type: 3}
  m_Name: ${id}
  m_EditorClassIdentifier:
  m_icon: {fileID: 0}
  m_iconSprite: {fileID: 0}
  m_sizzleSound: 0
  m_addToSound: 0
  m_uID: ${uid}
`;
}

/** 「意面拼盘」optional 组合节点：composition 空 + optional=[煎寿司鱼,煎寿司虾]。
 *  注册进 optionalRecipeMatchListItems 后，盘内 [鱼]/[虾]/[鱼+虾]（无意面）子集
 *  都能命中本节点（汉堡 optional 同款子集匹配语义），m_platingPrefab=盘体 prefab
 *  → PastaCosmetic 按盘内容切 ComboLookup 状态；模型/装盘视觉与成品共用同一盘体。 */
function toppingsComboYaml(id, uid, optionalGuids, plateGuid) {
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
  m_Script: {fileID: 11500000, guid: ${SCRIPT_CUSTOM_RECIPE}, type: 3}
  m_Name: ${id}
  m_EditorClassIdentifier:
  type: 1
  recipeName: ${id}
  uID: ${uid}
  score: 0
  platingStepSO: {fileID: 11400000, guid: ${REF.plateStep}, type: 2}
  modelSO: {fileID: 0}
  model: {fileID: 1709254077376921, guid: ${plateGuid}, type: 2}
  iconSO: {fileID: 0}
  icon: {fileID: 0}
  compositionSOs: []
  optionalSOs:
${optionalGuids.map((g) => `  - {fileID: 11400000, guid: ${g}, type: 2}`).join("\n")}
  cookingStepSO: {fileID: 0}
  cookingStepIconSO: {fileID: 0}
  cookingStepIcon: {fileID: 0}
  cookingProgress: 0
  mixingIconSO: {fileID: 0}
  mixingIcon: {fileID: 0}
  mixingProgress: 0
`;
}

function cookedCloneYaml(id, uid, compositionGuid, cookingStepGuid) {  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${SCRIPT_COOKED_NODE}, type: 3}
  m_Name: ${id}
  m_EditorClassIdentifier:
  m_orderGuiDescription: []
  m_platingStep: {fileID: 0}
  m_platingPrefab: {fileID: 0}
  m_uID: ${uid}
  m_composition:
  - {fileID: 11400000, guid: ${compositionGuid}, type: 2}
  m_optional: []
  m_cookingStep: {fileID: 11400000, guid: ${cookingStepGuid}, type: 2}
  m_progress: 1
`;
}

function comboLookupYaml(content, prefabs) {
  const entry = (guids, prefab) => `  - m_content:
${guids.map((g) => `    - {fileID: 11400000, guid: ${g}, type: 2}`).join("\n")}
    m_prefab: {fileID: 1709254077376921, guid: ${prefab}, type: 2}
`;
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
  m_Script: {fileID: 11500000, guid: ${SCRIPT_COMBO_LOOKUP}, type: 3}
  m_Name: ${IDS.lookup}
  m_EditorClassIdentifier:
  m_lookupArray:
${content.map((c, i) => entry(c, prefabs[i])).join("")}  m_fallbackArray:
${entry([content[0][0]], prefabs[0])}  m_defaultFallback: {fileID: 1709254077376921, guid: ${prefabs[0]}, type: 2}
`;
}

function platedPrefabYaml(name, meshGuidLocal, materialGuidsLocal) {
  const goId = 1709254077376921;
  const trId = 4730242686860875;
  const mfId = 33724804207875695;
  const mrId = 23618682377417806;
  const riId = 114703437598541787;
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &${goId}
GameObject:
  serializedVersion: 5
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_Component:
  - component: {fileID: ${trId}}
  - component: {fileID: ${mfId}}
  - component: {fileID: ${mrId}}
  - component: {fileID: ${riId}}
  m_Layer: 0
  m_Name: ${name}
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &${trId}
Transform:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${goId}}
  m_LocalRotation: {x: 0, y: -0, z: -0, w: 1}
  m_LocalPosition: {x: -0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {x: 0, y: -0, z: -0}
--- !u!33 &${mfId}
MeshFilter:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${goId}}
  m_Mesh: {fileID: 4300000, guid: ${meshGuidLocal}, type: 3}
--- !u!23 &${mrId}
MeshRenderer:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${goId}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_MotionVectors: 0
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 0
  m_Materials:
${materialGuidsLocal.map((g) => `  - {fileID: 2100000, guid: ${g}, type: 2}`).join("\n")}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 0
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
--- !u!114 &${riId}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${goId}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${SCRIPT_RENDERER_INFO}, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  lightmapIndex: -1
  lightmapScaleOffset: {x: 0, y: 0, z: 0, w: 0}
--- !u!1001 &100100000
Prefab:
  serializedVersion: 2
  m_ObjectHideFlags: 1
  m_Modification:
    m_TransformParent: {fileID: 0}
    m_Modifications: []
    m_RemovedComponents: []
  m_ParentPrefab: {fileID: 0}
  m_RootGameObject: {fileID: ${goId}}
  m_IsPrefabParent: 1
`;
}

function fmtFloat(v) {
  if (Number.isInteger(v)) return v.toString();
  return String(parseFloat(v.toPrecision(8)));
}

function materialYaml(info) {
  const texLines = Object.entries(info.texEnvs)
    .map(([prop, srcGuid]) => {
      const texName = texMap[srcGuid];
      if (!texName) throw new Error(`texture source guid not mapped: ${srcGuid}`);
      return `    - ${prop}:
        m_Texture: {fileID: 2800000, guid: ${texGuid(texName)}, type: 3}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}`;
    })
    .join("\n");
  const floatLines = Object.entries(info.floats)
    .map(([k, v]) => `    - ${k}: ${fmtFloat(v)}`)
    .join("\n");
  const colorLines = Object.entries(info.colors)
    .map(([k, v]) => `    - ${k}: {r: ${fmtFloat(v[0])}, g: ${fmtFloat(v[1])}, b: ${fmtFloat(v[2])}, a: ${fmtFloat(v[3])}}`)
    .join("\n");
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 6
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_Name: ${info.name}
  m_Shader: {fileID: 4800000, guid: ${SHADER_GUID}, type: 3}
  m_ShaderKeywords:
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
${texLines}
    m_Floats:
${floatLines}
    m_Colors:
${colorLines}
`;
}

function platePrefabYaml() {
  const lookup = KEEP.lookup;
  const goId = 1709254077376921;
  const trId = 4730242686860875;
  const cosmeticId = 114724804207875695;
  const assignableId = 114618682377417806;
  // CustomStub.PastaOriginalModels（盘内组合视觉换原版模型；Awake 克隆 ComboLookup
  // 后替换 plated_pasta_* 为 bundle47 原版，防本地 OBJ 黑块）。
  // 字符串字面量：数值超出 Number.MAX_SAFE_INTEGER 会被舍入（…000）。
  const pastaModelsId = "114880000000000001";
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &${goId}
GameObject:
  serializedVersion: 5
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_Component:
  - component: {fileID: ${trId}}
  - component: {fileID: ${cosmeticId}}
  - component: {fileID: ${assignableId}}
  - component: {fileID: ${pastaModelsId}}
  m_Layer: 0
  m_Name: ${IDS.plate}
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &${trId}
Transform:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${goId}}
  m_LocalRotation: {x: 0, y: -0, z: -0, w: 1}
  m_LocalPosition: {x: -0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &${cosmeticId}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${goId}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${SCRIPT_PASTA_COSMETIC}, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  m_comboPrefabLookup: {fileID: 11400000, guid: ${lookup}, type: 2}
  m_maintainScale: 1
--- !u!114 &${assignableId}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${goId}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${SCRIPT_ASSIGNABLE_ORDER}, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  m_orderData: {fileID: 0}
--- !u!114 &${pastaModelsId}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${goId}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${SCRIPT_PASTA_MODELS}, type: 3}
  m_Name:
  m_EditorClassIdentifier:
--- !u!1001 &100100000
Prefab:
  serializedVersion: 2
  m_ObjectHideFlags: 1
  m_Modification:
    m_TransformParent: {fileID: 0}
    m_Modifications: []
    m_RemovedComponents: []
  m_ParentPrefab: {fileID: 0}
  m_RootGameObject: {fileID: ${goId}}
  m_IsPrefabParent: 1
`;
}

const SHADER_SRC = `Shader "Custom/WebPlatedFood" {
    // 原版 sh_gloss_d_e_hl / sh_gloss_d_hl / sh_gloss_d_e 的属性并集替代
    // （kettle 同款策略：保留原 .mat 属性名，仅替换 shader 引用）。
    Properties {
        _DiffuseMap ("Diffuse Map", 2D) = "white" {}
        _MaskColor ("Mask Color", Color) = (0.5, 0.5, 0.5, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0.04
        _Roughness ("Roughness", Range(0, 1)) = 0.5
        _Brightness ("Brightness", Range(0, 1)) = 0
        _highlight_colour ("Highlight Colour", Color) = (0.91, 0.87, 0.78, 1)
    }
    SubShader {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _DiffuseMap;
        struct Input { float2 uv_DiffuseMap; };
        half _Metallic;
        half _Roughness;
        half _Brightness;
        fixed4 _highlight_colour;
        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D(_DiffuseMap, IN.uv_DiffuseMap);
            fixed3 lit = lerp(c.rgb, c.rgb * _highlight_colour.rgb * 2.0, _Brightness);
            o.Albedo = lit;
            o.Metallic = _Metallic;
            o.Smoothness = 1.0 - _Roughness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "VertexLit"
}
`;

// ── 计划构建 ─────────────────────────────────────────────────────────────
const plan = [];
function addFile(rel, content, note) {
  plan.push({ rel, content, note });
}
function addCopy(rel, srcAbs, note) {
  plan.push({ rel, srcAbs, note });
}
function addFolder(rel, note) {
  addFile(`${rel}.meta`, FOLDER_META(folderGuid(rel)), note);
}
function addDelete(rel, note) {
  plan.push({ delete: rel, note });
}

function buildPlan() {
  // 目录
  addFolder(cloneDir, "lookup_nodes 目录（uID 克隆）");
  addFolder(meshDir, "meshes 目录");
  addFolder(matDir, "materials 目录");
  addFolder(texDir, "textures 目录");

  // 中间产物 CustomRecipeSO（Cooked）
  addFile(
    `${nodeDir}/${IDS.boiledWrapper}.asset`,
    customRecipeCookedYaml(IDS.boiledWrapper, uID.boiledWrapper, {
      ingredient: REF.pastaIngredient,
      cookStep: REF.potStep,
      cookIcon: REF.potIcon,
    }),
    "煮意面 wrapper（锅 + PastaSO，懒加载真节点）",
  );
  addFile(`${nodeDir}/${IDS.boiledWrapper}.asset.meta`, ASSET_META(nodeGuid(`recipe-node:${IDS.boiledWrapper}`)), "BoiledWrapper meta（新 guid：跨路径搬移不复用旧 guid）");
  const fishNodeG = nodeGuid(`recipe-node:${IDS.fishNode}`);
  const prawnNodeG = nodeGuid(`recipe-node:${IDS.prawnNode}`);
  addFile(
    `${nodeDir}/${IDS.fishNode}.asset`,
    customRecipeCookedYaml(IDS.fishNode, uID.fishNode, {
      ingredient: REF.sushiFishIngredient,
      cookStep: REF.fryingPanStep,
      cookIcon: REF.fryingPanIcon,
      modelSO: REF.platedFishModel,
    }),
    "煎寿司鱼（FryingPan + SushiFishSO）",
  );
  addFile(`${nodeDir}/${IDS.fishNode}.asset.meta`, ASSET_META(fishNodeG), "FishNode meta");
  addFile(
    `${nodeDir}/${IDS.prawnNode}.asset`,
    customRecipeCookedYaml(IDS.prawnNode, uID.prawnNode, {
      ingredient: REF.sushiPrawnIngredient,
      cookStep: REF.fryingPanStep,
      cookIcon: REF.fryingPanIcon,
      // 接盘与锅内同款（炸虾饼）：model 直引工程 prefab 优先（GetModel 直引 > modelSO），
      // modelSO 保留为兜底。直引模式与 commonW2 汉堡夹心（FriedCornPone 等）一致，真机可用。
      modelSO: REF.platedPrawnModel,
      model: `{fileID: 1717355270871722, guid: ${REF.prawnPanModelPrefab}, type: 2}`,
    }),
    "煎寿司虾（FryingPan + SushiPrawnSO；盘=锅同款炸虾饼）",
  );
  addFile(`${nodeDir}/${IDS.prawnNode}.asset.meta`, ASSET_META(prawnNodeG), "PrawnNode meta");

  // 「意面拼盘」optional 组合（composition 空 + optional=[煎鱼,煎虾]，盘内子集装盘）
  const toppingsComboG = nodeGuid(`recipe-node:${IDS.toppingsCombo}`);
  addFile(
    `${nodeDir}/${IDS.toppingsCombo}.asset`,
    toppingsComboYaml(IDS.toppingsCombo, uID.toppingsCombo, [fishNodeG, prawnNodeG], KEEP.plate),
    "意面拼盘（[鱼]/[虾]/[鱼+虾] 子集装盘匹配，plating=盘体 prefab）",
  );
  addFile(`${nodeDir}/${IDS.toppingsCombo}.asset.meta`, ASSET_META(toppingsComboG), "ToppingsCombo meta");

  // 三道菜谱
  const boiledNodeG = nodeGuid(`recipe-node:${IDS.boiledWrapper}`);
  addFile(
    `${pastaDir}/${IDS.final}.asset`,
    finalRecipeYaml(IDS.final, uID.final, 80, [boiledNodeG, fishNodeG, prawnNodeG], KEEP.seafoodIcon),
    "海鲜意面（煮意面+煎鱼+煎虾，全必选）",
  );
  addFile(`${pastaDir}/${IDS.final}.asset.meta`, ASSET_META(KEEP.seafoodRecipe), "海鲜意面 meta（guid 保留）");
  addFile(
    `${pastaDir}/${IDS.fishFinal}.asset`,
    finalRecipeYaml(IDS.fishFinal, uID.fishFinal, 60, [boiledNodeG, fishNodeG], recipeIconGuid(IDS.fishFinal)),
    "鲜鱼意面（煮意面+煎鱼）",
  );
  addFile(`${pastaDir}/${IDS.fishFinal}.asset.meta`, ASSET_META(recipeGuid(IDS.fishFinal)), "鲜鱼意面 meta");
  addFile(
    `${pastaDir}/${IDS.prawnFinal}.asset`,
    finalRecipeYaml(IDS.prawnFinal, uID.prawnFinal, 60, [boiledNodeG, prawnNodeG], recipeIconGuid(IDS.prawnFinal)),
    "鲜虾意面（煮意面+煎虾）",
  );
  addFile(`${pastaDir}/${IDS.prawnFinal}.asset.meta`, ASSET_META(recipeGuid(IDS.prawnFinal)), "鲜虾意面 meta");

  // uID 克隆节点 + 步骤
  const clonePastaG = nodeGuid("clone:Web_Pasta_Clone_Pasta");
  const cloneFishG = nodeGuid("clone:Web_Pasta_Clone_SushiFish");
  const clonePrawnG = nodeGuid("clone:Web_Pasta_Clone_SushiPrawn");
  const stepPotG = nodeGuid("clone:Web_Pasta_Step_Pot");
  const stepPanG = nodeGuid("clone:Web_Pasta_Step_FryingPan");
  const cloneBoiledG = nodeGuid("clone:Web_Pasta_Clone_BoiledPasta");
  const cloneFriedFishG = nodeGuid("clone:Web_Pasta_Clone_FriedSushiFish");
  const cloneFriedPrawnG = nodeGuid("clone:Web_Pasta_Clone_FriedSushiPrawn");

  addFile(`${cloneDir}/Web_Pasta_Clone_Pasta.asset`, ingredientCloneYaml("Web_Pasta_Clone_Pasta", UID_VANILLA.pastaNode, [1, 1, 1, 1]), "Pasta 节点克隆（uID=" + UID_VANILLA.pastaNode + "）");
  addFile(`${cloneDir}/Web_Pasta_Clone_Pasta.asset.meta`, ASSET_META(clonePastaG), "Pasta 克隆 meta");
  addFile(`${cloneDir}/Web_Pasta_Clone_SushiFish.asset`, ingredientCloneYaml("Web_Pasta_Clone_SushiFish", UID_VANILLA.sushiFishNode, [0.882353, 0.5137255, 0.4941177, 1]), "SushiFish 节点克隆");
  addFile(`${cloneDir}/Web_Pasta_Clone_SushiFish.asset.meta`, ASSET_META(cloneFishG), "SushiFish 克隆 meta");
  addFile(`${cloneDir}/Web_Pasta_Clone_SushiPrawn.asset`, ingredientCloneYaml("Web_Pasta_Clone_SushiPrawn", UID_VANILLA.sushiPrawnNode, [1, 0.6352941, 0.345098, 1]), "SushiPrawn 节点克隆");
  addFile(`${cloneDir}/Web_Pasta_Clone_SushiPrawn.asset.meta`, ASSET_META(clonePrawnG), "SushiPrawn 克隆 meta");
  addFile(`${cloneDir}/Web_Pasta_Step_Pot.asset`, cookingStepCloneYaml("Web_Pasta_Step_Pot", UID_VANILLA.potStep), "Pot 步骤克隆（uID=" + UID_VANILLA.potStep + "）");
  addFile(`${cloneDir}/Web_Pasta_Step_Pot.asset.meta`, ASSET_META(stepPotG), "Pot 克隆 meta");
  addFile(`${cloneDir}/Web_Pasta_Step_FryingPan.asset`, cookingStepCloneYaml("Web_Pasta_Step_FryingPan", UID_VANILLA.fryingPanStep), "FryingPan 步骤克隆");
  addFile(`${cloneDir}/Web_Pasta_Step_FryingPan.asset.meta`, ASSET_META(stepPanG), "FryingPan 克隆 meta");
  addFile(`${cloneDir}/Web_Pasta_Clone_BoiledPasta.asset`, cookedCloneYaml("Web_Pasta_Clone_BoiledPasta", 58323101, clonePastaG, stepPotG), "煮意面 cooked 克隆");
  addFile(`${cloneDir}/Web_Pasta_Clone_BoiledPasta.asset.meta`, ASSET_META(cloneBoiledG), "煮意面克隆 meta");
  addFile(`${cloneDir}/Web_Pasta_Clone_FriedSushiFish.asset`, cookedCloneYaml("Web_Pasta_Clone_FriedSushiFish", 58323102, cloneFishG, stepPanG), "煎寿司鱼 cooked 克隆");
  addFile(`${cloneDir}/Web_Pasta_Clone_FriedSushiFish.asset.meta`, ASSET_META(cloneFriedFishG), "煎寿司鱼克隆 meta");
  addFile(`${cloneDir}/Web_Pasta_Clone_FriedSushiPrawn.asset`, cookedCloneYaml("Web_Pasta_Clone_FriedSushiPrawn", 58323103, clonePrawnG, stepPanG), "煎寿司虾 cooked 克隆");
  addFile(`${cloneDir}/Web_Pasta_Clone_FriedSushiPrawn.asset.meta`, ASSET_META(cloneFriedPrawnG), "煎寿司虾克隆 meta");

  // 本地模型拷贝（mesh / tex / mat / shader / prefab）
  addFile(`${modelDir}/WebPlatedFood.shader`, SHADER_SRC, "本地 plated 材质 shader");
  addFile(`${modelDir}/WebPlatedFood.shader.meta`, SHADER_META(SHADER_GUID), "shader meta");

  for (const file of fs.readdirSync(path.join(ASSETS_SRC, "meshes"))) {
    if (!file.endsWith(".obj")) continue;
    const meshName = file.replace(/\.obj$/, "");
    addCopy(`${meshDir}/${file}`, path.join(ASSETS_SRC, "meshes", file), `mesh ${meshName}`);
    addFile(`${meshDir}/${file}.meta`, OBJ_META(meshGuidByMeshName[meshName], meshName), `mesh meta ${meshName}`);
  }
  for (const file of fs.readdirSync(path.join(ASSETS_SRC, "textures"))) {
    if (!file.endsWith(".png")) continue;
    const name = file.replace(/\.png$/, "");
    addCopy(`${texDir}/${file}`, path.join(ASSETS_SRC, "textures", file), `tex ${name}`);
    addFile(`${texDir}/${file}.meta`, TEX_META(texGuid(name)), `tex meta ${name}`);
  }
  for (const [srcGuid, info] of Object.entries(materialsInfo)) {
    addFile(`${matDir}/${info.name}.mat`, materialYaml(info), `mat ${info.name}`);
    addFile(`${matDir}/${info.name}.mat.meta`, MAT_META(matGuidBySource[srcGuid]), `mat meta ${info.name}`);
  }
  for (const [name, info] of Object.entries(prefabsInfo)) {
    const localMats = info.materialGuids.map((g) => matGuidBySource[g]);
    addFile(`${modelDir}/${name}.prefab`, platedPrefabYaml(name, meshGuidByMeshName[info.meshSourceName], localMats), `prefab ${name}`);
    addFile(`${modelDir}/${name}.prefab.meta`, PREFAB_META(prefabGuidByName[name]), `prefab meta ${name}`);
  }

  // ComboLookup（guid 保留 —— 盘体 prefab 引用它）
  addFile(
    `${lookupDir}/${IDS.lookup}.asset`,
    comboLookupYaml(
      [
        [cloneBoiledG],
        [cloneBoiledG, cloneFriedFishG],
        [cloneBoiledG, cloneFriedPrawnG],
        [cloneBoiledG, cloneFriedFishG, cloneFriedPrawnG],
      ],
      [
        prefabGuidByName["plated_pasta_01"],
        prefabGuidByName["plated_pasta_fish_01"],
        prefabGuidByName["plated_pasta_prawn_01"],
        prefabGuidByName["recipe_seafood_pasta_01"],
      ],
    ),
    "组合查找表（全本地引用）",
  );
  addFile(`${lookupDir}/${IDS.lookup}.asset.meta`, ASSET_META(KEEP.lookup), "lookup meta（guid 保留）");

  // 盘体 prefab（可再生成；guid 保留 —— 三道菜谱的 model 引用它）
  addFile(`${modelDir}/${IDS.plate}.prefab`, platePrefabYaml(), "盘体 prefab（PastaCosmetic + ComboLookup + AssignableOrder）");
  addFile(`${modelDir}/${IDS.plate}.prefab.meta`, PREFAB_META(KEEP.plate), "盘体 meta（guid 保留）");

  // 鲜鱼/鲜虾图标是独立原画。文件已在则不覆盖，避免退回海鲜意面占位。
  const iconSrc = path.join(repoRoot, "layout-editor/web/public/icons/recipes/Pasta_Marinara_SO.png");
  for (const id of [IDS.fishFinal, IDS.prawnFinal]) {
    const iconRel = `${iconDir}/${id}.png`;
    if (!fs.existsSync(path.join(repoRoot, iconRel))) {
      addCopy(iconRel, iconSrc, `${id} icon 占位`);
    }
    addFile(`${iconDir}/${id}.png.meta`, SPRITE_META(recipeIconGuid(id)), `${id} icon meta`);
    const webRel = `layout-editor/web/public/icons/recipes/${id}.png`;
    if (!fs.existsSync(path.join(repoRoot, webRel))) {
      addCopy(webRel, iconSrc, `${id} web icon`);
    }
    const distRel = `layout-editor/web/dist/icons/recipes/${id}.png`;
    if (fs.existsSync(path.join(repoRoot, "layout-editor/web/dist")) && !fs.existsSync(path.join(repoRoot, distRel))) {
      addCopy(distRel, iconSrc, `${id} dist icon`);
    }
  }

  // 清理：v1 遗留（pseudo_prefab_so/pasta 整目录 + 旧 orderdefinition 节点内容已被覆写）
  addDelete(`${W5}/pseudo_prefab_so/pasta`, "v1 BoiledWrapper 旧位置（已迁 custom_recipes/pasta/orderdefinitions）");
  addDelete(`${W5}/pseudo_prefab_so/pasta.meta`, "v1 目录 meta");

  mergeNames();
}

function mergeNames() {
  const rel = `${W5}/custom_recipes/names.json`;
  const abs = path.join(repoRoot, rel);
  const data = JSON.parse(fs.readFileSync(abs, "utf8").replace(/^\uFEFF/, ""));
  data.names = data.names ?? [];
  const items = [
    { id: IDS.final, zh: "Web 海鲜意面", en: "Web Seafood Pasta" },
    { id: IDS.fishFinal, zh: "Web 鲜鱼意面", en: "Web Fish Pasta" },
    { id: IDS.prawnFinal, zh: "Web 鲜虾意面", en: "Web Prawn Pasta" },
  ];
  for (const item of items) {
    const existing = data.names.find((n) => n.id === item.id);
    if (existing) {
      existing.zh = item.zh;
      existing.en = item.en;
    } else {
      data.names.push(item);
    }
  }
  addFile(rel, "\uFEFF" + JSON.stringify(data, null, 2) + "\n", "names.json merge");
}

buildPlan();

console.log(`=== gen-commonw5-pasta v2（${apply ? "APPLY" : "DRY-RUN"}） ===`);
for (const item of plan) {
  if (item.delete) {
    console.log(`  ${apply ? "rm  " : "rm? "} ${item.delete}  (${item.note})`);
  } else if (item.srcAbs) {
    console.log(`  ${apply ? "copy" : "plan"} ${item.rel}  (${item.note})`);
  } else {
    console.log(`  ${apply ? "write" : "plan"} ${item.rel}  (${item.note})`);
  }
}

if (apply) {
  for (const item of plan) {
    if (item.delete) {
      fs.rmSync(path.join(repoRoot, item.delete), { recursive: true, force: true });
      continue;
    }
    const abs = path.join(repoRoot, item.rel);
    fs.mkdirSync(path.dirname(abs), { recursive: true });
    if (item.srcAbs) fs.copyFileSync(item.srcAbs, abs);
    else fs.writeFileSync(abs, item.content, "utf8");
  }
  console.log(`\ndone: ${plan.filter((x) => !x.delete).length} files, ${plan.filter((x) => x.delete).length} deletions`);
} else {
  console.log(`\n(dry-run: ${plan.filter((x) => !x.delete).length} files, ${plan.filter((x) => x.delete).length} deletions)`);
}
