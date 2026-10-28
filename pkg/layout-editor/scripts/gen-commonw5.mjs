#!/usr/bin/env node
/**
 * commonW5 增量库生成器：OC1/AYCE 主题 BGM + 工作台皮肤（丛林/生日）+ 自定义食材（辣椒/茶叶/葱）
 * + 卷饼菜谱库（2 成品 + 4 中间产物）。
 *
 * 用法：
 *   node layout-editor/scripts/gen-commonw5.mjs            # dry-run + 报告
 *   node layout-editor/scripts/gen-commonw5.mjs --apply    # 落盘
 *   node layout-editor/scripts/gen-commonw5.mjs --apply --skip-audio  # 跳过 ogg 转码（已生成时）
 *
 * 纪律：
 *  - 所有新资产使用确定性 guid：md5("commonw5-<kind>:<id|rel>")，不沿用参考工程 .meta；
 *  - 参考工程里的游戏脚本 guid、common04 supplies SO 引用与本仓库一致，原样保留；
 *  - 旧工程自有资产（网格/材质/贴图/prefab/node/菜谱/图标）全部重建 guid 并重定向引用；
 *  - PseudoPrefabSO.asset 直引 / IngredientOrderNode 图标与 crateLid 由
 *    LayoutEditorCommonW5Setup（Unity 域重载自动执行，幂等）补全，避免手写 PPtr 风险。
 */
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const workspaceRoot = path.resolve(repoRoot, "..");
const REF = path.join(workspaceRoot, "backup_rebuild_20260917_132624/commonW5参考");
const W5 = "Assets/commonW5";
const REF_MUSIC = path.join(REF, "common_music");
const COUNTER_REF = path.join(REF, "丛林和生日桌台");

const apply = process.argv.includes("--apply");
const skipAudio = process.argv.includes("--skip-audio");

const md5 = (s) => crypto.createHash("md5").update(s).digest("hex");
const folderGuid = (rel) => md5(`commonw5-folder:${rel}`);
const assetGuid = (rel) => md5(`commonw5-asset:${rel}`);
const musicGuid = (id) => md5(`commonw5-music:${id}`);
const musicSoGuid = (id) => md5(`commonw5-music-so:${id}`);
const counterGuid = (id, kind) => md5(`commonw5-counter-${kind}:${id}`);
const ingGuid = (id, kind) => md5(`commonw5-ingredient-${kind}:${id}`);
const recipeGuid = (id) => md5(`commonw5-recipe:${id}`);
const recipeIconGuid = (id) => md5(`commonw5-recipe-icon:${id}`);
const recipeModelGuid = (id) => md5(`commonw5-recipe-model:${id}`);

const yamlStr = (s) =>
  s.replace(/[^\x20-\x7e]/g, (ch) => {
    const hex = ch.codePointAt(0).toString(16).toUpperCase().padStart(4, "0");
    return `\\u${hex}`;
  });

// ---------------------------------------------------------------------------
// 1) 音乐清单（30 首唯一曲目；参考里的 Bagel Level_0..3 与主版本逐字节相同，已去重）
// ---------------------------------------------------------------------------
const MUSIC = [
  // OC1 经典主题
  { id: "OC1_8BitHero", group: "oc1", src: "OC1/Music/8BitHero.ogg", zh: "8-Bit 英雄（BGM）", en: "8-Bit Hero" },
  { id: "OC1_AccordionMusic", group: "oc1", src: "OC1/Music/Accordion Music.ogg", zh: "手风琴旋律（BGM）", en: "Accordion Music" },
  { id: "OC1_AllTheTrimmings", group: "oc1", src: "OC1/Music/AllTheTrimmings.ogg", zh: "节日盛宴（BGM）", en: "All the Trimmings" },
  { id: "OC1_CookingWithMama", group: "oc1", src: "OC1/Music/CookingWithMama.ogg", zh: "和妈妈一起做饭（BGM）", en: "Cooking with Mama" },
  { id: "OC1_DLCJungleIsland", group: "oc1", src: "OC1/Music/DLC_JungleIsland.ogg", zh: "丛林岛（BGM）", en: "Jungle Island" },
  { id: "OC1_EndBoss", group: "oc1", src: "OC1/Music/EndBoss.ogg", zh: "终局首领（BGM）", en: "End Boss" },
  { id: "OC1_FeelinGood", group: "oc1", src: "OC1/Music/FeelinGood.ogg", zh: "心情正好（BGM）", en: "Feelin' Good" },
  { id: "OC1_MusicLevelHell", group: "oc1", src: "OC1/Music/Music_Level_Hell.ogg", zh: "地狱关卡（BGM）", en: "Hell Level" },
  { id: "OC1_MusicLevelHorror", group: "oc1", src: "OC1/Music/Music_Level_Horror.ogg", zh: "恐怖关卡（BGM）", en: "Horror Level" },
  { id: "OC1_OnionHouse", group: "oc1", src: "OC1/Music/OnionHouseMusic.ogg", zh: "洋葱屋（BGM）", en: "Onion House" },
  { id: "OC1_OutOfThisWorld", group: "oc1", src: "OC1/Music/OutOfThisWorld.ogg", zh: "异星世界（BGM）", en: "Out of This World" },
  { id: "OC1_ShoreLeave", group: "oc1", src: "OC1/Music/ShoreLeave.ogg", zh: "登岸假期（BGM）", en: "Shore Leave" },
  // OC1 新版主题
  { id: "OC1_OKD1DLC01Level", group: "oc1", src: "OC1/Music/New Music/OKD1_DLC01_Music_Level.ogg", zh: "DLC01 关卡（BGM）", en: "DLC01 Level" },
  { id: "OC1_OKD1DLC02Level", group: "oc1", src: "OC1/Music/New Music/OKD1_DLC02_Music_Level.ogg", zh: "DLC02 关卡（BGM）", en: "DLC02 Level" },
  { id: "OC1_OKD1DLC02OnionHut", group: "oc1", src: "OC1/Music/New Music/OKD1_DLC02_Music_OnionHut.ogg", zh: "DLC02 洋葱小屋（BGM）", en: "DLC02 Onion Hut" },
  { id: "OC1_Arctic", group: "oc1", src: "OC1/Music/New Music/OKD1_Music_Arctic.ogg", zh: "极地（BGM）", en: "Arctic" },
  { id: "OC1_Boat", group: "oc1", src: "OC1/Music/New Music/OKD1_Music_Boat.ogg", zh: "船上航行（BGM）", en: "Boat" },
  { id: "OC1_FoodTruck", group: "oc1", src: "OC1/Music/New Music/OKD1_Music_FoodTruck.ogg", zh: "餐车（BGM）", en: "Food Truck" },
  { id: "OC1_HauntedHouse", group: "oc1", src: "OC1/Music/New Music/OKD1_Music_HauntedHouse.ogg", zh: "鬼屋（BGM）", en: "Haunted House" },
  { id: "OC1_Kitchen", group: "oc1", src: "OC1/Music/New Music/OKD1_Music_Kitchen.ogg", zh: "厨房（BGM）", en: "Kitchen" },
  { id: "OC1_OnionHut", group: "oc1", src: "OC1/Music/New Music/OKD1_Music_OnionHut.ogg", zh: "洋葱小屋（BGM）", en: "Onion Hut" },
  { id: "OC1_Peckoning", group: "oc1", src: "OC1/Music/New Music/OKD1_Music_Peckoning.ogg", zh: "啄食时刻（BGM）", en: "Peckoning" },
  { id: "OC1_Space", group: "oc1", src: "OC1/Music/New Music/OKD1_Music_Space.ogg", zh: "太空（BGM）", en: "Space" },
  { id: "OC1_Tutorial", group: "oc1", src: "OC1/Music/New Music/OKD1_Music_Tutorial.ogg", zh: "教程（BGM）", en: "Tutorial" },
  // AYCE 主题
  { id: "AYCE_BagelBirthdayLevel", group: "ayce", src: "AYCE/Music/Bagel_BirthdayDLC_Music_Level.ogg", zh: "生日 DLC 关卡（BGM）", en: "Birthday Level" },
  { id: "AYCE_DLC101Biome1", group: "ayce", src: "AYCE/Music/DLC_101_InGame_Biome1_Music.ogg", zh: "生态区 1（BGM）", en: "Biome 1" },
  { id: "AYCE_DLC101Biome2", group: "ayce", src: "AYCE/Music/DLC_101_InGame_Biome2_Music.ogg", zh: "生态区 2（BGM）", en: "Biome 2" },
  { id: "AYCE_DLC101Biome3", group: "ayce", src: "AYCE/Music/DLC_101_InGame_Biome3_Music.ogg", zh: "生态区 3（BGM）", en: "Biome 3" },
  { id: "AYCE_GhostShipLevel", group: "ayce", src: "AYCE/Music/OKD1_Music_GhostShip_Level.ogg", zh: "幽灵船关卡（BGM）", en: "Ghost Ship Level" },
  { id: "AYCE_Volcanic", group: "ayce", src: "AYCE/Music/OKD1_Music_Volcanic.ogg", zh: "火山（BGM）", en: "Volcanic" },
];

// ---------------------------------------------------------------------------
// 2) 桌台皮肤（参考两目录逐字节相同，合并为 2 款）
// ---------------------------------------------------------------------------
const COUNTERS = [
  {
    id: "jungle",
    soId: "CounterJungleSO",
    prefab: "web_counter_jungle",
    obj: "m_jungle_counter_01.obj",
    tex: "t_jungle_counter_d.png",
    matSrc: "m_jungle_counter_01_0Mat.mat",
    meshY: "0.263",
    zh: "丛林桌台",
    en: "Jungle Counter",
  },
  {
    id: "anniversary",
    soId: "CounterAnniversarySO",
    prefab: "web_counter_anniversary",
    obj: "m_dlc100_countertop_Anniversary_04.obj",
    tex: "t_dlc100_Countertop_Anniversary_01_D.png",
    matSrc: "m_dlc100_countertop_Anniversary_04_0Mat.mat",
    meshY: "0",
    zh: "生日桌台",
    en: "Anniversary Counter",
  },
];

// ---------------------------------------------------------------------------
// 3) 食材套件（旧工程 prefab 复制 + guid 全量重定向）
// ---------------------------------------------------------------------------
const INGREDIENTS = [
  {
    id: "spice",
    soId: "SpiceSO",
    refDir: "My_Custom/Ingredients/Spices",
    workable: "custom_spice_workable.prefab",
    unworkable: "custom_spice_unworkable.prefab",
    node: "Node_Spice.asset",
    nodeId: "Node_Spice",
    rawObj: "m_dlc101_Spices_Raw_01.obj",
    choppedObj: "m_dlc101_Spices_Chopped_01.obj",
    rawTex: "t_dlc101_Spices_Raw_01_D.png",
    choppedTex: "t_dlc101_Spices_Chopped_01_D.png",
    rawMat: "m_dlc101_Spices_Raw_01_0Mat.mat",
    choppedMat: "m_dlc101_Spices_Chopped_01_0Mat.mat",
    icon: "Spices_Icon.png",
    extraIcons: ["Spices_Icon_craft.png"],
    prefabBase: "custom_spice",
    oldRootWorkable: "custom_spice_workable",
    oldRootUnworkable: "custom_spice_unworkable",
    uid: 100000001,
    zh: "辣椒",
    en: "Chili",
  },
  {
    id: "tea",
    soId: "TeaSO",
    refDir: "My_Custom/Ingredients/Tea",
    workable: "custom_tea_workable.prefab",
    unworkable: "custom_tea_unworkable.prefab",
    node: "node_ingredent_tea_chopped.asset",
    nodeId: "Node_Tea_Chopped",
    rawObj: "m_dlc100_Tea_leaves_raw_03.obj",
    choppedObj: "m_dlc100_Tea_leaves_chopped_03.obj",
    rawTex: "t_dlc100_tea_leaves_raw_02_D.png",
    choppedTex: "t_dlc100_tea_leaves_chopped_03_D.png",
    rawMat: "m_dlc100_Tea_leaves_raw_03_0Mat.mat",
    choppedMat: "m_dlc100_Tea_leaves_chopped_03_0Mat.mat",
    icon: "DLC100_Tea_leaves_Unprep.png",
    extraIcons: ["DLC100_Tea_leaves_Unprep_2.png", "DLC100_Tea_leaves_Cut.png"],
    prefabBase: "custom_tea",
    oldRootWorkable: "custom_tea_workable",
    oldRootUnworkable: "custom_tea_unworkable",
    uid: 100000002,
    zh: "茶叶",
    en: "Tea Leaves",
  },
  {
    id: "springonion",
    soId: "SpringOnionSO",
    refDir: "SpringOnion",
    workable: "custom_springonion_workable.prefab",
    unworkable: "custom_springonion_unworkable.prefab",
    node: "Node_SpringOnion.asset",
    nodeId: "Node_SpringOnion",
    rawObj: "Spring_Onion.obj",
    choppedObj: "prep_Spring_Onion.obj",
    rawTex: "Spring_Onion.png",
    choppedTex: "prep_Spring_Onion.png",
    rawMat: "defaultMat.mat",
    choppedMat: "defaultMat 1.mat",
    icon: "SpringOnion_Icon.png",
    extraIcons: ["craft_SpringOnion_Icon.png"],
    prefabBase: "custom_springonion",
    oldRootWorkable: "custom_springonion_workable",
    oldRootUnworkable: "custom_springonion_unworkable 1",
    uid: 100000003,
    zh: "葱",
    en: "Spring Onion",
  },
];

// ---------------------------------------------------------------------------
// 4) 卷饼菜谱（uid 以 web 为准：58323 号段，逐条校验不冲突后固定分配）
// ---------------------------------------------------------------------------
const UID_PREFIX = 58323;
const BURRITOS = [
  {
    id: "Web_Burrito_Meat_Chicken",
    final: true,
    uid: 58323001,
    score: 100,
    composition: [
      "eba97984fecfb8940a72e730a55ea89f", // TortillaSO（common01）
      "Web_Burrito_RicePrep",
      "Web_Burrito_MeatPrep",
      "Web_Burrito_ChickenPrep",
    ],
    model: "Model_Burrito_Meat_Chicken",
    icon: "UI_burrito_meat_chicken.png",
    zh: "Web 肉馅鸡肉卷饼",
    en: "Web Meat & Chicken Burrito",
  },
  {
    id: "Web_Burrito_Mushroom_Chicken",
    final: true,
    uid: 58323002,
    score: 100,
    composition: [
      "eba97984fecfb8940a72e730a55ea89f",
      "Web_Burrito_RicePrep",
      "Web_Burrito_ChickenPrep",
      "Web_Burrito_MushroomPrep",
    ],
    model: "Model_Burrito_Mushroom_Chicken",
    icon: "UI_burrito_mushroom_chicken.png",
    zh: "Web 蘑菇鸡肉卷饼",
    en: "Web Mushroom & Chicken Burrito",
  },
  {
    id: "Web_Burrito_RicePrep",
    final: false,
    uid: 58323003,
    score: 0,
    composition: ["d628cc1cba14ca24f927c76c83ae9463"], // RiceSO
    cookingStepSO: "8687a65ff3d0dd94c91818e822af0aa4", // Pot
    cookingStepIconSO: "67d4429d9a2417746ab9ce083330f8ad", // PotIcon
    model: "Model_Plated_Rice",
    zh: "Web 卷饼米饭（中间）",
    en: "Web Burrito Rice (Intermediate)",
  },
  {
    id: "Web_Burrito_MeatPrep",
    final: false,
    uid: 58323004,
    score: 0,
    composition: ["4c54ee4dbc00db742a827110f1aea22d"], // BurritoMeatSO
    cookingStepSO: "48502c8d89d4ee64fb284f1665a4df43", // FryingPan
    cookingStepIconSO: "9c68f4c54ac67974bb061643da368185", // FryingPanIcon
    model: "Model_Plated_Meat",
    zh: "Web 卷饼肉馅（中间）",
    en: "Web Burrito Meat (Intermediate)",
  },
  {
    id: "Web_Burrito_ChickenPrep",
    final: false,
    uid: 58323005,
    score: 0,
    composition: ["85bd4a3b88782714091204aeb3e85e6d"], // BurritoChickenSO
    cookingStepSO: "48502c8d89d4ee64fb284f1665a4df43",
    cookingStepIconSO: "9c68f4c54ac67974bb061643da368185",
    model: "Model_Fried_Chicken",
    zh: "Web 卷饼鸡肉（中间）",
    en: "Web Burrito Chicken (Intermediate)",
  },
  {
    id: "Web_Burrito_MushroomPrep",
    final: false,
    uid: 58323006,
    score: 0,
    composition: ["2b710142f4f6a0a4f8ad21e3170ad9b3"], // MushroomSO
    cookingStepSO: "48502c8d89d4ee64fb284f1665a4df43",
    cookingStepIconSO: "9c68f4c54ac67974bb061643da368185",
    model: "Model_Plated_Mushroom",
    zh: "Web 卷饼蘑菇（中间）",
    en: "Web Burrito Mushroom (Intermediate)",
  },
];

const MODEL_WRAPPERS = {
  Model_Burrito_Meat_Chicken: {
    prefabName: "plated_burrito_meat_01",
    bundleName: "bundle47",
    assetPath: "assets/prefabs/plated/plated_burrito_meat_01.prefab",
  },
  Model_Burrito_Mushroom_Chicken: {
    prefabName: "plated_burrito_mushroom_01",
    bundleName: "bundle47",
    assetPath: "assets/prefabs/plated/plated_burrito_mushroom_01.prefab",
  },
  Model_Plated_Rice: {
    prefabName: "plated_rice",
    bundleName: "bundle47",
    assetPath: "assets/prefabs/plated/plated_rice.prefab",
  },
  Model_Plated_Meat: {
    prefabName: "plated_meat",
    bundleName: "bundle47",
    assetPath: "assets/prefabs/plated/plated_meat_01.prefab",
  },
  Model_Fried_Chicken: {
    prefabName: "p_dlc8_recipe_chicken_burger_01",
    bundleName: "bundle359",
    assetPath:
      "assets/downloadablecontent/dlc08/dlc_assets/prefabs/ingredients/p_dlc8_recipe_chicken_burger_01.prefab",
  },
  Model_Plated_Mushroom: {
    prefabName: "plated_mushroom_01",
    bundleName: "bundle47",
    assetPath: "assets/prefabs/plated/plated_mushroom_01.prefab",
  },
};

const SCRIPT_PSEUDO_SO = "0cff7c13895ab9e47a5e02d4619cc3b9";
const SCRIPT_CUSTOM_RECIPE = "83fb008bcc8e793429b02c178c430815";
const SCRIPT_RECIPE_CONFIG = "5edb07060a0c6c140aa72ffb8fd3f021";
const SCRIPT_INGREDIENT_NODE = "bff1936f87185a161a744a226a12f22d";
const PLATE_GUID = "02b04fdf6fef0e944870fd5e06375ed1";

// ---------------------------------------------------------------------------
// 通用工具
// ---------------------------------------------------------------------------
const plan = [];

function addFile(rel, content, meta, note, binary = false) {
  plan.push({ rel, content, meta, note, binary });
}

function copyFile(rel, srcAbs, meta, note) {
  plan.push({ rel, srcAbs, meta, note, binary: true });
}

const FOLDER_META = (guid, bundle = "") => `fileFormatVersion: 2
guid: ${guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: ${bundle}
  assetBundleVariant: 
`;

const ASSET_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

const TEXT_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

const PREFAB_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
PrefabImporter:
  externalObjects: {}
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

const OBJ_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
ModelImporter:
  serializedVersion: 22
  fileIDToRecycleName: {}
  externalObjects: {}
  materials:
    importMaterials: 1
    materialName: 0
    materialSearch: 1
    materialLocation: 1
  meshes:
    globalScale: 1
    meshCompression: 0
    addColliders: 0
  isReadable: 1
  importAnimation: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

const ICON_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
TextureImporter:
  fileIDToRecycleName: {}
  externalObjects: {}
  serializedVersion: 4
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: -1
    aniso: -1
    mipBias: -1
    wrapU: 1
    wrapV: 1
    wrapW: -1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  platformSettings:
  - buildTarget: DefaultTexturePlatform
    maxTextureSize: 512
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
  spritePackingTag: 
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

const TEXTURE_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
TextureImporter:
  fileIDToRecycleName: {}
  externalObjects: {}
  serializedVersion: 4
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: -1
    aniso: -1
    mipBias: -1
    wrapU: 1
    wrapV: 1
    wrapW: -1
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  platformSettings:
  - buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
  spritePackingTag: 
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

/** Unity 2017 AudioImporter：Vorbis + Streaming（BGM 长音频，quality 75 控体积）。 */
const AUDIO_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
timeCreated: 0
licenseType: Free
AudioImporter:
  serializedVersion: 6
  name:
  externalObjects: {}
  defaultSettings:
    loadType: 2
    sampleRateSetting: 0
    sampleRateOverride: 0
    compressionFormat: 1
    quality: 75
    conversionMode: 0
  platformSettingOverrides: {}
  forceToMono: 0
  normalize: 0
  preloadAudioData: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  previewData:
    previewData: []
    origSize: 0
    compSize: 0
  output:
    outputSettings:
      loadType: 0
      sampleRateSetting: 0
      sampleRateOverride: 0
      compressionFormat: 0
      quality: 0
      conversionMode: 0
    outputContainerFormat: 0
    editorOutputSettings:
      loadType: 0
      sampleRateSetting: 0
      sampleRateOverride: 0
      compressionFormat: 0
      quality: 0
      conversionMode: 0
    editorOutputContainerFormat: 0
    playerResource:
      source:
      offset: 0
      size: 0
  userData:
  assetBundleName: 
  assetBundleVariant: 
`;

function pngSize(abs) {
  const b = fs.readFileSync(abs);
  return { width: b.readUInt32BE(16), height: b.readUInt32BE(20) };
}

function readGuid(absMeta) {
  const txt = fs.readFileSync(absMeta, "utf8");
  const m = txt.match(/^guid: ([a-f0-9]{32})/m);
  if (!m) throw new Error("无法解析 guid: " + absMeta);
  return m[1];
}

function readRef(rel) {
  return fs.readFileSync(path.join(REF, rel), "utf8");
}

/** 把某 guid 在文本中的引用整体替换（覆盖 guid 常量出现处）。 */
function replaceGuid(text, oldGuid, newGuid) {
  if (!oldGuid || !newGuid) throw new Error("replaceGuid 参数缺失");
  return text.split(oldGuid).join(newGuid);
}

/** 旧工程 prefab 里指向已不存在场景对象的引用（非零 fileID + 全零 guid）：
 *  Unity 2017 解析时会报 "Could not extract GUID"；统一置空（{fileID: 0}）。 */
function nullBrokenRefs(text) {
  return text.replace(/\{fileID: -?\d+, guid: 0{32},\s*\n?\s*type: 0\}/g, "{fileID: 0}");
}

/** 将根 Transform（m_Father: {fileID: 0}）的位置/旋转归零。 */
function zeroRootTransform(text) {
  const blocks = text.split(/\n--- /);
  for (let i = 0; i < blocks.length; i++) {
    const b = blocks[i];
    if (b.startsWith("!u!4 ") && b.includes("m_Father: {fileID: 0}")) {
      blocks[i] = b
        .replace(/m_LocalRotation: \{[^}]*\}/, "m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}")
        .replace(/m_LocalPosition: \{[^}]*\}/, "m_LocalPosition: {x: 0, y: 0, z: 0}")
        .replace(/m_LocalScale: \{[^}]*\}/, "m_LocalScale: {x: 1, y: 1, z: 1}");
    }
  }
  return blocks.join("\n--- ");
}

const PSEUDO_SO = ({ name, prefabName, bundleName = "", assetPath = "", asset = null }) => `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${SCRIPT_PSEUDO_SO}, type: 3}
  m_Name: ${name}
  m_EditorClassIdentifier: 
  prefabName: ${prefabName}
  bundleName: ${bundleName}
  assetPath: ${assetPath}
  asset: ${asset ? `{fileID: ${asset.fileID}, guid: ${asset.guid}, type: ${asset.type}}` : "{fileID: 0}"}
`;

const INGREDIENT_NODE = ({ name, uid, iconGuid, iconW, iconH }) => `%YAML 1.1
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
  m_Name: ${name}
  m_EditorClassIdentifier: 
  m_orderGuiDescription: []
  m_platingStep: {fileID: 0}
  m_platingPrefab: {fileID: 0}
  m_uID: ${uid}
  m_iconSprite: {fileID: 21300000, guid: ${iconGuid}, type: 3}
  m_crateLid:
    m_atlasTexture: {fileID: 2800000, guid: ${iconGuid}, type: 3}
    m_subRect:
      serializedVersion: 2
      x: 0
      y: 0
      width: ${iconW}
      height: ${iconH}
  m_colour: {r: 1, g: 1, b: 1, a: 1}
`;

// ---------------------------------------------------------------------------
// 目录与 bundle 根
// ---------------------------------------------------------------------------
const FOLDERS = [
  "Assets/commonW5",
  "Assets/commonW5/audio",
  "Assets/commonW5/audio/music",
  "Assets/commonW5/audio/music/oc1",
  "Assets/commonW5/audio/music/ayce",
  "Assets/commonW5/counters",
  "Assets/commonW5/ingredients",
  "Assets/commonW5/pseudo_prefab_so",
  "Assets/commonW5/pseudo_prefab_so/audio",
  "Assets/commonW5/pseudo_prefab_so/audio/music",
  "Assets/commonW5/pseudo_prefab_so/food",
  "Assets/commonW5/pseudo_prefab_so/counters",
  "Assets/commonW5/custom_recipes",
  "Assets/commonW5/custom_recipes/burrito",
  "Assets/commonW5/custom_recipes/burrito/icons",
  "Assets/commonW5/custom_recipes/burrito/models",
];
for (const c of COUNTERS) FOLDERS.push(`Assets/commonW5/counters/${c.id}`);
for (const g of INGREDIENTS) FOLDERS.push(`Assets/commonW5/ingredients/${g.id}`);

function buildFolders() {
  for (const dir of FOLDERS) {
    const isRoot = dir === "Assets/commonW5";
    const metaPath = dir + ".meta";
    addFile(
      metaPath,
      FOLDER_META(folderGuid(dir), isRoot ? "commonW5" : ""),
      null,
      isRoot ? "bundle 根标记" : undefined,
    );
  }
}

// ---------------------------------------------------------------------------
// 任务 1：音乐
// ---------------------------------------------------------------------------
function buildMusic() {
  for (const m of MUSIC) {
    const rel = `${W5}/audio/music/${m.group}/${m.id}.ogg`;
    const soRel = `${W5}/pseudo_prefab_so/audio/music/${m.id}.asset`;
    const srcAbs = path.join(REF_MUSIC, m.src);

    if (!skipAudio) {
      // 瘦身：Vorbis q6（≈192kbps）+ 44.1kHz 立体声；听感基本无损，仓库体积约 -25%。
      // ffmpeg 仅做解码/重采样（本机 ffmpeg 无 libvorbis 编码器），编码由 oggenc(libvorbis) 完成。
      const outAbs = path.join(repoRoot, rel);
      fs.mkdirSync(path.dirname(outAbs), { recursive: true });
      if (apply) {
        const tmpWav = path.join(
          workspaceRoot,
          "backup_rebuild_20260917_132624/commonW5参考",
          `_tmp_${m.id}.wav`,
        );
        try {
          execFileSync(
            "ffmpeg",
            ["-y", "-v", "error", "-i", srcAbs, "-map_metadata", "-1", "-ar", "44100", "-ac", "2", tmpWav],
            { stdio: "inherit" },
          );
          execFileSync("oggenc", ["-Q", "-q", "6", "-o", outAbs, tmpWav], { stdio: "inherit" });
        } finally {
          try {
            fs.unlinkSync(tmpWav);
          } catch {
            /* ignore */
          }
        }
      }
      plan.push({ rel, note: "oggenc Vorbis q6 瘦身", audioEncode: true });
    }

    addFile(
      rel + ".meta",
      AUDIO_META(musicGuid(m.id)),
      null,
      undefined,
    );

    addFile(
      soRel,
      PSEUDO_SO({
        name: m.id,
        prefabName: yamlStr(m.zh),
      }),
      ASSET_META(musicSoGuid(m.id)),
      `音乐 SO（asset 直引由 Unity 校验菜单补全）`,
    );
  }
}

// ---------------------------------------------------------------------------
// 任务 2：桌台皮肤
// ---------------------------------------------------------------------------
function buildCounterMat(srcAbs, matName, objId) {
  // 参考 .mat 为 Standard shader，仅 _MainTex 需要绑定；其余属性原样保留。
  let text = fs.readFileSync(srcAbs, "utf8");
  text = text.replace(/^  m_Name: .*$/m, `  m_Name: ${matName}`);
  const texGuid = counterGuid(objId, "tex");
  text = text.replace(
    /(- _MainTex:\n        m_Texture: )\{fileID: [^}]*\}/m,
    `$1{fileID: 2800000, guid: ${texGuid}, type: 3}`,
  );
  return text;
}

function buildCounters() {
  const counterTemplate = fs.readFileSync(
    path.join(repoRoot, "Assets/common04/custom_counter/custom_counter.prefab"),
    "utf8",
  );
  const templateMatGuid = "7d2df6961ae6f3049a823adc8ebbb1ae"; // common04 mat_gray
  const templateMesh = "{fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}";

  for (const c of COUNTERS) {
    const dir = `${W5}/counters/${c.id}`;
    const meshGuid = counterGuid(c.id, "mesh");
    const texGuid = counterGuid(c.id, "tex");
    const matGuid = counterGuid(c.id, "mat");
    const prefabGuid = counterGuid(c.id, "prefab");
    const soGuid = counterGuid(c.id, "so");

    copyFile(`${dir}/${c.id}.obj`, path.join(COUNTER_REF, c.obj), OBJ_META(meshGuid), "网格");
    copyFile(`${dir}/${path.basename(c.tex)}`, path.join(COUNTER_REF, c.tex), TEXTURE_META(texGuid), "漫反射");
    addFile(
      `${dir}/mat_${c.id}.mat`,
      buildCounterMat(path.join(COUNTER_REF, c.matSrc), `mat_${c.id}`, c.id),
      MAT_META(matGuid),
      "Standard 材质（绑定新贴图）",
    );

    // 克隆 common04 全功能桌台模板：仅替换可见网格/材质与 Mesh 子物体摆放。
    let prefab = counterTemplate;
    prefab = prefab.replace("m_Name: custom_counter", `m_Name: ${c.prefab}`);
    prefab = prefab.split(templateMesh).join(`{fileID: 4300000, guid: ${meshGuid}, type: 3}`);
    prefab = prefab.split(`{fileID: 2100000, guid: ${templateMatGuid}, type: 2}`).join(
      `{fileID: 2100000, guid: ${matGuid}, type: 2}`,
    );
    // Mesh 子物体：改为原始尺寸（1:1）+ 按包围盒落地。
    prefab = prefab.replace(
      /m_LocalPosition: \{x: 0, y: 0\.25, z: 0\}\n  m_LocalScale: \{x: 1\.2, y: 0\.5, z: 1\.2\}/,
      `m_LocalPosition: {x: 0, y: ${c.meshY}, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}`,
    );
    prefab = nullBrokenRefs(prefab);
    addFile(`${dir}/${c.prefab}.prefab`, prefab, PREFAB_META(prefabGuid), "全功能桌台 prefab");

    addFile(
      `${W5}/pseudo_prefab_so/counters/${c.soId}.asset`,
      PSEUDO_SO({ name: c.soId, prefabName: c.prefab }),
      ASSET_META(soGuid),
      "皮肤 SO（asset 直引由 Unity 校验菜单补全）",
    );
  }
}

// ---------------------------------------------------------------------------
// 任务 3：食材
// ---------------------------------------------------------------------------
function buildIngredientMat(srcAbs, matName, texGuid) {
  let text = fs.readFileSync(srcAbs, "utf8");
  text = text.replace(/^  m_Name: .*$/m, `  m_Name: ${matName}`);
  text = text.replace(
    /(- _MainTex:\n        m_Texture: )\{fileID: [^}]*\}/m,
    `$1{fileID: 2800000, guid: ${texGuid}, type: 3}`,
  );
  return text;
}

function buildIngredients() {
  for (const g of INGREDIENTS) {
    const dir = `${W5}/ingredients/${g.id}`;
    const refDirAbs = path.join(REF, g.refDir);
    const rawMeshGuid = ingGuid(g.id, "rawmesh");
    const choppedMeshGuid = ingGuid(g.id, "choppedmesh");
    const rawTexGuid = ingGuid(g.id, "rawtex");
    const choppedTexGuid = ingGuid(g.id, "choppedtex");
    const rawMatGuid = ingGuid(g.id, "rawmat");
    const choppedMatGuid = ingGuid(g.id, "choppedmat");
    const workableGuid = ingGuid(g.id, "workable");
    const unworkableGuid = ingGuid(g.id, "unworkable");
    const nodeGuid = ingGuid(g.id, "node");
    const soGuid = ingGuid(g.id, "so");
    const iconGuid = ingGuid(g.id, "icon");

    // 参考 .meta 里的旧 guid（仅用于把 prefab 内引用重定向到新资产）
    const oldRawMesh = readGuid(path.join(refDirAbs, g.rawObj + ".meta"));
    const oldChoppedMesh = readGuid(path.join(refDirAbs, g.choppedObj + ".meta"));
    const oldRawMat = readGuid(path.join(refDirAbs, g.rawMat + ".meta"));
    const oldChoppedMat = readGuid(path.join(refDirAbs, g.choppedMat + ".meta"));
    const oldUnworkable = readGuid(path.join(refDirAbs, g.unworkable + ".meta"));
    const oldNode = readGuid(path.join(refDirAbs, g.node + ".meta"));

    // 网格 / 贴图 / 材质 / 图标
    copyFile(`${dir}/${g.rawObj}`, path.join(refDirAbs, g.rawObj), OBJ_META(rawMeshGuid), "raw 网格");
    copyFile(`${dir}/${g.choppedObj}`, path.join(refDirAbs, g.choppedObj), OBJ_META(choppedMeshGuid), "chopped 网格");
    copyFile(`${dir}/${path.basename(g.rawTex)}`, path.join(refDirAbs, g.rawTex), TEXTURE_META(rawTexGuid), "raw 贴图");
    copyFile(
      `${dir}/${path.basename(g.choppedTex)}`,
      path.join(refDirAbs, g.choppedTex),
      TEXTURE_META(choppedTexGuid),
      "chopped 贴图",
    );
    addFile(
      `${dir}/mat_${g.id}_raw.mat`,
      buildIngredientMat(path.join(refDirAbs, g.rawMat), `mat_${g.id}_raw`, rawTexGuid),
      MAT_META(rawMatGuid),
      "raw 材质",
    );
    addFile(
      `${dir}/mat_${g.id}_chopped.mat`,
      buildIngredientMat(path.join(refDirAbs, g.choppedMat), `mat_${g.id}_chopped`, choppedTexGuid),
      MAT_META(choppedMatGuid),
      "chopped 材质",
    );
    copyFile(`${dir}/${g.icon}`, path.join(refDirAbs, g.icon), ICON_META(iconGuid), "食材图标（sprite）");
    for (const extra of g.extraIcons) {
      copyFile(
        `${dir}/${extra}`,
        path.join(refDirAbs, extra),
        ICON_META(ingGuid(g.id, "icon:" + extra)),
        "附加图标（sprite）",
      );
    }

    // prefab：复制参考工程完整组件栈，guid 全量重定向 + 根变换归零
    let workable = readRef(`${g.refDir}/${g.workable}`);
    workable = workable.replace(
      `m_Name: ${g.oldRootWorkable}`,
      `m_Name: ${g.prefabBase}_workable`,
    );
    workable = replaceGuid(workable, oldRawMesh, rawMeshGuid);
    workable = replaceGuid(workable, oldRawMat, rawMatGuid);
    workable = replaceGuid(workable, oldUnworkable, unworkableGuid);
    workable = nullBrokenRefs(zeroRootTransform(workable));
    addFile(`${dir}/${g.prefabBase}_workable.prefab`, workable, PREFAB_META(workableGuid), "食材 raw prefab");

    let unworkable = readRef(`${g.refDir}/${g.unworkable}`);
    unworkable = unworkable.replace(
      `m_Name: ${g.oldRootUnworkable}`,
      `m_Name: ${g.prefabBase}_unworkable`,
    );
    unworkable = replaceGuid(unworkable, oldChoppedMesh, choppedMeshGuid);
    unworkable = replaceGuid(unworkable, oldChoppedMat, choppedMatGuid);
    unworkable = replaceGuid(unworkable, oldNode, nodeGuid);
    unworkable = nullBrokenRefs(zeroRootTransform(unworkable));
    addFile(
      `${dir}/${g.prefabBase}_unworkable.prefab`,
      unworkable,
      PREFAB_META(unworkableGuid),
      "食材 chopped prefab",
    );

    // node：uID 固定段 + 图标/crateLid 直写（Unity 校验菜单会再核验）
    const iconAbs = path.join(refDirAbs, g.icon);
    const { width, height } = pngSize(iconAbs);
    addFile(
      `${dir}/${g.nodeId}.asset`,
      INGREDIENT_NODE({
        name: g.nodeId,
        uid: g.uid,
        iconGuid,
        iconW: width,
        iconH: height,
      }),
      ASSET_META(nodeGuid),
      `食材节点 uID=${g.uid}`,
    );

    // 食材 SO：asset 直引 workable prefab（由 Unity 校验菜单补全）
    addFile(
      `${W5}/pseudo_prefab_so/food/${g.soId}.asset`,
      PSEUDO_SO({ name: g.soId, prefabName: g.prefabBase + "_workable" }),
      ASSET_META(soGuid),
      "食材 SO（asset 直引由 Unity 校验菜单补全）",
    );
  }
}

// ---------------------------------------------------------------------------
// 任务 4：卷饼菜谱
// ---------------------------------------------------------------------------
const recipeById = new Map(BURRITOS.map((r) => [r.id, r]));

function compositionRef(idOrGuid) {
  if (!idOrGuid.includes("-") && /^[a-f0-9]{32}$/.test(idOrGuid)) {
    return `{fileID: 11400000, guid: ${idOrGuid}, type: 2}`;
  }
  const r = recipeById.get(idOrGuid);
  if (!r) throw new Error("未知组成引用: " + idOrGuid);
  return `{fileID: 11400000, guid: ${recipeGuid(r.id)}, type: 2}`;
}

function buildRecipeYaml(r) {
  const compLines = r.composition.map((c) => `  - ${compositionRef(c)}`).join("\n");
  const modelGuid = recipeModelGuid(r.model);
  const modelSO = MODEL_WRAPPERS[r.model];
  if (!modelSO) throw new Error("缺少模型包装: " + r.model);
  const isFinal = !!r.final;
  const cooking = r.cookingStepSO
    ? `  cookingStepSO: {fileID: 11400000, guid: ${r.cookingStepSO}, type: 2}
  cookingStepIconSO: {fileID: 11400000, guid: ${r.cookingStepIconSO}, type: 2}`
    : `  cookingStepSO: {fileID: 0}
  cookingStepIconSO: {fileID: 0}`;
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
  m_Name: ${r.id}
  m_EditorClassIdentifier: 
  type: ${isFinal ? 1 : 2}
  recipeName: ${r.id}
  uID: ${r.uid}
  score: ${r.score}
  platingStepSO: {fileID: 11400000, guid: ${PLATE_GUID}, type: 2}
  modelSO: {fileID: 11400000, guid: ${modelGuid}, type: 2}
  model: {fileID: 0}
  iconSO: {fileID: 0}
  icon: ${isFinal ? `{fileID: 21300000, guid: ${recipeIconGuid(r.id)}, type: 3}` : "{fileID: 0}"}
  compositionSOs:
${compLines}
  optionalSOs: []
${cooking}
  cookingStepIcon: {fileID: 0}
  cookingProgress: ${isFinal ? 0 : 1}
  mixingIconSO: {fileID: 0}
  mixingIcon: {fileID: 0}
  mixingProgress: 0
`;
}

function buildRecipes() {
  // 菜谱资产
  for (const r of BURRITOS) {
    addFile(
      `${W5}/custom_recipes/burrito/${r.id}.asset`,
      buildRecipeYaml(r),
      ASSET_META(recipeGuid(r.id)),
      r.final ? "卷饼成品" : "卷饼中间产物",
    );
  }

  // 模型包装 SO（指向原版 bundle 的 plated 成品模型）
  for (const [id, m] of Object.entries(MODEL_WRAPPERS)) {
    addFile(
      `${W5}/custom_recipes/burrito/models/${id}.asset`,
      PSEUDO_SO({ name: id, prefabName: m.prefabName, bundleName: m.bundleName, assetPath: m.assetPath }),
      ASSET_META(recipeModelGuid(id)),
      "成品模型包装",
    );
  }

  // 图标（2 张 UI png → 128px → 库内 icons/ 和 web public）
  const icons = [
    { id: "Web_Burrito_Meat_Chicken", file: "UI_burrito_meat_chicken.png" },
    { id: "Web_Burrito_Mushroom_Chicken", file: "UI_burrito_mushroom_chicken.png" },
  ];
  for (const it of icons) {
    const srcAbs = path.join(REF, "rw_8/Burrito/Model", it.file);
    const rel = `${W5}/custom_recipes/burrito/icons/${it.id}.png`;
    plan.push({
      rel,
      iconSource: srcAbs,
      note: "卷饼图标 128px",
      meta: ICON_META(recipeIconGuid(it.id)),
    });
  }

  // names.json（UTF-8 BOM）——合并式：保留非卷饼条目（如奶茶分类 milk_tea，
  // 见 gen-commonw5-kettle.py），重跑本脚本不抹除增量内容。
  const names = {
    schemaVersion: 1,
    names: BURRITOS.map((r) => ({ id: r.id, zh: r.zh, en: r.en })),
  };
  const namesPathAbs = path.join(repoRoot, `${W5}/custom_recipes/names.json`);
  if (fs.existsSync(namesPathAbs)) {
    try {
      const prev = JSON.parse(fs.readFileSync(namesPathAbs, "utf8").replace(/^\uFEFF/, ""));
      const mine = new Set(names.names.map((n) => n.id));
      for (const n of prev.names ?? []) {
        if (n && n.id && !mine.has(n.id)) names.names.push(n);
      }
    } catch (e) {
      console.warn(`[gen-commonw5] names.json 合并读取失败（将整表覆盖）: ${e.message}`);
    }
  }
  addFile(
    `${W5}/custom_recipes/names.json`,
    "\uFEFF" + JSON.stringify(names, null, 2) + "\n",
    TEXT_META(assetGuid(`${W5}/custom_recipes/names.json`)),
    "卷饼中文名",
  );

  // CustomRecipeConfig：uidPrefix 58323（web uid 体系）——合并式：保留非 burrito
  // 分类（如 milk_tea），nextSequence 取现有值与卷饼条目数的较大者。
  let nextSequence = BURRITOS.length + 1;
  let extraCategories = "";
  const configPathAbs = path.join(repoRoot, `${W5}/custom_recipes/CustomRecipeConfig.asset`);
  if (fs.existsSync(configPathAbs)) {
    try {
      const prevText = fs.readFileSync(configPathAbs, "utf8");
      const seqMatch = prevText.match(/nextSequence: (\d+)/);
      if (seqMatch) nextSequence = Math.max(nextSequence, parseInt(seqMatch[1], 10));
      const catStart = prevText.indexOf("categories:");
      const catEnd = prevText.indexOf("subcategories:");
      if (catStart >= 0 && catEnd > catStart) {
        // 逐行状态机：一个分类块 = "- id:" 行 + 后继缩进属性行
        const lines = prevText.slice(catStart + "categories:".length, catEnd).split("\n");
        let keep = false;
        for (const line of lines) {
          if (line.trimStart().startsWith("- id:")) {
            keep = !line.includes("id: burrito");
          } else if (line.trim() && !/^\s{4}\S/.test(line)) {
            keep = false; // 非属性行（异常兜底）
          }
          if (keep && line.trim()) extraCategories += line + "\n";
        }
      }
    } catch (e) {
      console.warn(`[gen-commonw5] CustomRecipeConfig 合并读取失败: ${e.message}`);
    }
  }
  const config = `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${SCRIPT_RECIPE_CONFIG}, type: 3}
  m_Name: CustomRecipeConfig
  m_EditorClassIdentifier: 
  uidPrefix: ${UID_PREFIX}
  nextSequence: ${nextSequence}
  categories:
  - id: burrito
    zh: "${yamlStr("卷饼")}"
    en: Burrito
${extraCategories}  subcategories: []
  modelTransforms: []
`;
  addFile(
    `${W5}/custom_recipes/CustomRecipeConfig.asset`,
    config,
    ASSET_META(assetGuid(`${W5}/custom_recipes/CustomRecipeConfig.asset`)),
    "commonW5 菜谱配置",
  );
}

// ---------------------------------------------------------------------------
// 生成计划
// ---------------------------------------------------------------------------
buildFolders();
buildMusic();
buildCounters();
buildIngredients();
buildRecipes();

// 输出报告
const reportPath = path.join(workspaceRoot, "backup_rebuild_20260917_132624/commonW5参考/_gen_report.txt");
const lines = [
  "commonW5 生成报告 " + new Date().toISOString(),
  "",
  `音乐 ${MUSIC.length} 首（oc1 ${MUSIC.filter((m) => m.group === "oc1").length} / ayce ${MUSIC.filter((m) => m.group === "ayce").length}）`,
  ...MUSIC.map((m) => `  ${m.id}  <= ${m.src}`),
  "",
  `桌台皮肤 ${COUNTERS.length}：` + COUNTERS.map((c) => c.soId).join(", "),
  `食材 ${INGREDIENTS.length}：` + INGREDIENTS.map((g) => `${g.soId}(uID ${g.uid})`).join(", "),
  `卷饼菜谱 ${BURRITOS.length}（uid ${BURRITOS[0].uid}..${BURRITOS[BURRITOS.length - 1].uid}，uidPrefix ${UID_PREFIX}）`,
  "",
  `计划写入 ${plan.length} 个文件`,
];

if (!apply) {
  for (const item of plan) {
    console.log(`[dry] ${item.rel}${item.note ? ` (${item.note})` : ""}`);
  }
  fs.mkdirSync(path.dirname(reportPath), { recursive: true });
  fs.writeFileSync(reportPath, lines.join("\n") + "\n", "utf8");
  console.log(`\ndry-run：${plan.length} 个文件；报告 → ${reportPath}`);
  console.log("加 --apply 执行落盘。");
  process.exit(0);
}

// 落盘
for (const item of plan) {
  const abs = path.join(repoRoot, item.rel);
  fs.mkdirSync(path.dirname(abs), { recursive: true });
  if (item.audioEncode) {
    // 已在上方 ffmpeg 直接写出，跳过内容复制
    continue;
  }
  if (item.iconSource) {
    execFileSync("sips", ["-Z", "128", item.iconSource, "--out", abs], { stdio: "ignore" });
  } else if (item.srcAbs) {
    fs.copyFileSync(item.srcAbs, abs);
  } else if (item.binary) {
    fs.writeFileSync(abs, item.content);
  } else {
    fs.writeFileSync(abs, item.content, "utf8");
  }
  if (item.meta) fs.writeFileSync(abs + ".meta", item.meta, "utf8");
  console.log(`写入 ${item.rel}${item.note ? ` (${item.note})` : ""}`);
}

// 同步图标到前端 public（dist 存在时一并）
const webRecipeIconDir = path.join(repoRoot, "layout-editor/web/public/icons/recipes");
const distRecipeIconDir = path.join(repoRoot, "layout-editor/web/dist/icons/recipes");
const webIngIconDir = path.join(repoRoot, "layout-editor/web/public/icons/ingredients");
const distIngIconDir = path.join(repoRoot, "layout-editor/web/dist/icons/ingredients");
fs.mkdirSync(webRecipeIconDir, { recursive: true });
fs.mkdirSync(webIngIconDir, { recursive: true });
const hasDist = fs.existsSync(path.dirname(distRecipeIconDir));
for (const r of BURRITOS) {
  if (!r.final) continue;
  const src = path.join(repoRoot, `${W5}/custom_recipes/burrito/icons/${r.id}.png`);
  if (!fs.existsSync(src)) continue;
  fs.copyFileSync(src, path.join(webRecipeIconDir, `${r.id}.png`));
  if (hasDist) {
    fs.mkdirSync(distRecipeIconDir, { recursive: true });
    fs.copyFileSync(src, path.join(distRecipeIconDir, `${r.id}.png`));
  }
}
// 食材图标（id.png，与 /icons/ingredients/<id>.png 约定一致）
for (const g of INGREDIENTS) {
  const src = path.join(repoRoot, `${W5}/ingredients/${g.id}/${g.icon}`);
  if (!fs.existsSync(src)) continue;
  fs.copyFileSync(src, path.join(webIngIconDir, `${g.soId}.png`));
  if (hasDist) {
    fs.mkdirSync(distIngIconDir, { recursive: true });
    fs.copyFileSync(src, path.join(distIngIconDir, `${g.soId}.png`));
  }
}

fs.mkdirSync(path.dirname(reportPath), { recursive: true });
fs.writeFileSync(reportPath, lines.join("\n") + "\n", "utf8");
console.log(`\n完成：${plan.length} 个文件已写入。报告 → ${reportPath}`);
