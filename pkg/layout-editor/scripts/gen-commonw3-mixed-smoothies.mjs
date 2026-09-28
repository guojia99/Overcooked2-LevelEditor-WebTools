#!/usr/bin/env node
/**
 * 生成 commonW3「Web 混果汁」（共用官方什锦果汁 OBJ，独立漫反射贴图）。
 *
 * 配方字段与 backup_20260911/汁/*.asset 一致（搅拌杯 Mixed + Glass，非 Cooked/搅拌碗）。
 *
 * 前置：node gen-mega-smoothie-textures.mjs --apply
 *
 * 用法：
 *   node gen-commonw3-mixed-smoothies.mjs [--apply]
 *   node gen-commonw3-mixed-smoothies.mjs --sync-icons
 *   node gen-commonw3-mixed-smoothies.mjs --import-icons [--apply]
 */
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const workspaceRoot = path.resolve(repoRoot, "..");
const MEGA_DIR = path.join(workspaceRoot, "backup_rebuild_20260917_132624/Models/什锦果汁");
const SHARED_OBJ = path.join(MEGA_DIR, "_shared/什锦果汁.obj");
const LAYOUT_RECIPE_ICONS = path.join(repoRoot, "layout-editor/web/public/icons/recipes");
const LAYOUT_DIST_RECIPE_ICONS = path.join(repoRoot, "layout-editor/web/dist/icons/recipes");
const COMMONW3_ICONS = path.join(repoRoot, "Assets/commonW3/custom_recipes/smoothie/icons");
const MIXED_ICON_SHEET = path.join(workspaceRoot, "backup_rebuild_20260917_132624/12果汁.png");
const MIXED_ICON_COLS = 3;
const MIXED_ICON_ROWS = 4;
/** 12果汁.png 精灵图槽位数（配方可多于 12，多出的图标需手动维护）。 */
const MIXED_ICON_GRID_SLOTS = MIXED_ICON_COLS * MIXED_ICON_ROWS;
const SMOOTHIE_MODELS_DIR = path.join(repoRoot, "Assets/commonW3/custom_recipes/smoothie/models");

const SCRIPT_CUSTOM_RECIPE = "83fb008bcc8e793429b02c178c430815";
const SCRIPT_RECIPE_CONFIG = "5edb07060a0c6c140aa72ffb8fd3f021";
/** 搅拌杯步骤图标 BlenderIcon（≠ Mixer 搅拌碗 910d8538…）。 */
const BLENDER_ICON_GUID = "7a77fe98553178640a4b8a48d10057bc";
/** Glass 装盘（backup_20260911/汁 同款）。 */
const GLASS_PLATING_GUID = "9f2781f346fbd6d42a008e004f255d92";
const UID_PREFIX = 58322;
const SALAD_COUNT = 21;
/** 与 gen-commonw3-smoothies.mjs RECIPES 数量保持同步 */
const SINGLE_SMOOTHIE_COUNT = 6;

const ING = {
  Banana: "b6f2aa157beed2140a12c5cc8a66f8c1",
  Melon: "f57c067108d7dd543873dd5df1414aea",
  SmoothiePineapple: "6207742210e05564daf15e9c5d4c727b",
  SmoothieStrawberry: "e1e6338f70e25804d8a1081909bcb066",
  DLC04_Grapes: "067651d383836c606d0d325fe3b3dbd7",
  DLC04_Orange: "c0721b5197a5193a62cac90b2e010560",
  DLC04_Peach: "c4a38be9154f4a2dfba533c6fdb72aa9",
  Blueberry: "a192356a1fd01504eadf0d90ffa2de62",
  DLC07_Blackberry: "1e87aab4d91d5e2b460a8de8fe55b737",
  DLC08_Raspberry: "2c4b11eca006b60f3ae5a95e5cf4c609",
};

/** 混饮：至少 3 种不同水果，每种各 1 份（≠ 单果汁的 3× 同款）。 */
function uniqueIngCount(ings) {
  return new Set(ings).size;
}

function assertMixedRecipe(r) {
  const unique = uniqueIngCount(r.ings);
  if (r.ings.length !== unique) {
    throw new Error(`${r.id} 含重复食材（混饮每种水果仅 1 份）`);
  }
  if (unique < 3) {
    throw new Error(`${r.id} 仅 ${unique} 种不同食材（混饮至少 3 种）`);
  }
  if (unique > 5) {
    throw new Error(`${r.id} 超过 5 种食材（混饮最多 5 种）`);
  }
}

/** 混果汁分数：3 种食材 80，4 种 100，5 种 120 */
function scoreForIngCount(count) {
  const scores = { 3: 80, 4: 100, 5: 120 };
  const score = scores[count];
  if (score === undefined) {
    throw new Error(`混果汁食材数量 ${count} 无对应分数（仅支持 3/4/5）`);
  }
  return score;
}

/** 已下架混饮（清理资产与 names.json 用）。 */
const RETIRED_MIXED_IDS = ["Web_Smoothie_Mix_Purple"];

const MIXED_RECIPES = [
  {
    id: "Web_Smoothie_Mix_Red",
    zh: "Web 红果混饮",
    en: "Web Red Fruit Mix",
    uid: 58322025,
    ings: ["SmoothieStrawberry", "Melon", "DLC04_Peach"],
  },
  {
    id: "Web_Smoothie_Mix_Orange",
    zh: "Web 橙果混饮",
    en: "Web Orange Fruit Mix",
    uid: 58322026,
    ings: ["DLC04_Orange", "SmoothieStrawberry", "Banana", "SmoothiePineapple"],
  },
  {
    id: "Web_Smoothie_Mix_Yellow",
    zh: "Web 黄果混饮",
    en: "Web Yellow Fruit Mix",
    uid: 58322027,
    ings: ["Banana", "SmoothiePineapple", "DLC04_Peach"],
  },
  {
    id: "Web_Smoothie_Mix_Green",
    zh: "Web 青果混饮",
    en: "Web Green Fruit Mix",
    uid: 58322028,
    ings: ["DLC04_Grapes", "Banana", "Melon", "SmoothiePineapple"],
  },
  {
    id: "Web_Smoothie_Mix_Pink",
    zh: "Web 粉果混饮",
    en: "Web Pink Fruit Mix",
    uid: 58322030,
    ings: ["SmoothieStrawberry", "Melon", "DLC04_Peach", "Banana"],
  },
  {
    id: "Web_Smoothie_Mix_Brown",
    zh: "Web 深果混饮",
    en: "Web Dark Fruit Mix",
    uid: 58322031,
    ings: ["DLC04_Grapes", "DLC07_Blackberry", "Blueberry", "SmoothieStrawberry", "Melon"],
  },
  {
    id: "Web_Smoothie_Mix_Coral",
    zh: "Web 珊瑚混饮",
    en: "Web Coral Fruit Mix",
    uid: 58322032,
    ings: ["DLC04_Orange", "SmoothieStrawberry", "Melon", "DLC08_Raspberry"],
  },
  {
    id: "Web_Smoothie_Mix_Tropical",
    zh: "Web 热带混饮",
    en: "Web Tropical Fruit Mix",
    uid: 58322033,
    ings: ["DLC04_Orange", "Banana", "SmoothiePineapple"],
  },
  {
    id: "Web_Smoothie_Mix_Berry",
    zh: "Web 浆果混饮",
    en: "Web Berry Fruit Mix",
    uid: 58322034,
    ings: ["Blueberry", "DLC07_Blackberry", "SmoothieStrawberry", "DLC08_Raspberry"],
  },
  {
    id: "Web_Smoothie_Mix_Sunrise",
    zh: "Web 晨曦混饮",
    en: "Web Sunrise Fruit Mix",
    uid: 58322035,
    ings: ["DLC04_Orange", "DLC04_Peach", "Banana", "Blueberry"],
  },
  {
    id: "Web_Smoothie_Mix_Meadow",
    zh: "Web 田园混饮",
    en: "Web Meadow Fruit Mix",
    uid: 58322036,
    ings: ["DLC04_Peach", "SmoothiePineapple", "Melon"],
  },
  {
    id: "Web_Smoothie_Mix_Bramble",
    zh: "Web 野莓混饮",
    en: "Web Bramble Berry Mix",
    uid: 58322037,
    iconCopyFrom: "Web_Smoothie_Mix_Purple",
    ings: ["Blueberry", "DLC07_Blackberry", "DLC08_Raspberry"],
  },
];

function mixedRecipeUid(r, index) {
  if (r.uid != null) return r.uid;
  return UID_PREFIX * 1000 + SALAD_COUNT + SINGLE_SMOOTHIE_COUNT + index + 1;
}

for (const r of MIXED_RECIPES) assertMixedRecipe(r);

const md5 = (s) => crypto.createHash("md5").update(s).digest("hex");
const folderGuid = (rel) => md5(`commonw3-folder:${rel}`);
const recipeGuid = (id) => md5(`commonw3-recipe:${id}`);
const iconGuid = (id) => md5(`commonw3-icon:${id}`);
const modelGuid = (id) => md5(`commonw3-model:${id}`);
const assetGuid = (rel) => md5(`commonw3-asset:${rel}`);

const yamlStr = (s) =>
  s.replace(/[^\x20-\x7e]/g, (ch) => {
    const hex = ch.codePointAt(0).toString(16).toUpperCase().padStart(4, "0");
    return `\\u${hex}`;
  });

const FOLDER_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
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
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
  - buildTarget: Standalone
    maxTextureSize: 2048
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
  maxTextureSize: 2048
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
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  platformSettings:
  - buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
  - buildTarget: Standalone
    maxTextureSize: 2048
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
  animations:
    legacyGenerateAnimations: 4
    bakeSimulation: 0
    resampleCurves: 1
    optimizeGameObjects: 0
    motionNodeName: 
    rigImportErrors: 
    rigImportWarnings: 
    animationImportErrors: 
    animationImportWarnings: 
    animationRetargetingWarnings: 
    animationDoRetargetingWarnings: 0
    importAnimatedCustomProperties: 0
    animationCompression: 1
    animationRotationError: 0.5
    animationPositionError: 0.5
    animationScaleError: 0.5
    animationWrapMode: 0
    extraExposedTransformPaths: []
    extraUserProperties: []
    clipAnimations: []
    isReadable: 1
  meshes:
    lODScreenPercentages: []
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    importVisibility: 1
    importBlendShapes: 1
    importCameras: 1
    importLights: 1
    swapUVChannels: 0
    generateSecondaryUV: 0
    useFileUnits: 1
    optimizeMeshForGPU: 1
    keepQuads: 0
    weldVertices: 1
    preserveHierarchy: 1
    indexFormat: 0
    secondaryUVAngleDistortion: 8
    secondaryUVAreaDistortion: 15.000001
    secondaryUVHardAngle: 88
    secondaryUVPackMargin: 4
    useFileScale: 1
  tangentSpace:
    normalSmoothAngle: 60
    normalImportMode: 0
    tangentImportMode: 3
    normalCalculationMode: 4
  importAnimation: 1
  copyAvatar: 0
  humanDescription:
    serializedVersion: 2
    human: []
    skeleton: []
    armTwist: 0.5
    foreArmTwist: 0.5
    upperLegTwist: 0.5
    legTwist: 0.5
    armStretch: 0.05
    legStretch: 0.05
    feetSpacing: 0
    rootMotionBoneName: 
    rootMotionBoneRotation: {x: 0, y: 0, z: 0, w: 1}
    hasTranslationDoF: 0
    hasExtraRoot: 0
    skeletonHasParents: 1
  lastHumanDescriptionAvatarSource: {instanceID: 0}
  animationType: 0
  humanoidOversampling: 1
  additionalBone: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

const MAT_NAME = "mat_recipe_megaSmoothie_01";

function ingGuids(ings) {
  return ings.map((k) => {
    const g = ING[k];
    if (!g) throw new Error(`未知食材: ${k}`);
    return g;
  });
}

function compositionYaml(guids) {
  return guids.map((g) => `  - {fileID: 11400000, guid: ${g}, type: 2}`).join("\n");
}

/** 从 models/<id>/<id>.obj.meta 解析 model 引用（对齐 backup_20260911/汁）。 */
function modelRefForRecipe(recipeId) {
  const metaPath = path.join(SMOOTHIE_MODELS_DIR, recipeId, `${recipeId}.obj.meta`);
  if (fs.existsSync(metaPath)) {
    const raw = fs.readFileSync(metaPath, "utf8");
    const guid = /^guid: ([a-f0-9]+)/m.exec(raw)?.[1];
    if (guid) {
      const fileID = /^\s+100002:/m.test(raw) ? 100002 : 100000;
      return { fileID, guid };
    }
  }
  // 新菜谱首次落盘前 .meta 尚不存在，用与 OBJ_META 相同的确定性 guid
  return { fileID: 100000, guid: modelGuid(recipeId) };
}

const RECIPE_ASSET = (r, uid, modelRef) => `%YAML 1.1
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
  type: 3
  recipeName: ${r.id}
  uID: ${uid}
  score: ${scoreForIngCount(uniqueIngCount(r.ings))}
  platingStepSO: {fileID: 11400000, guid: ${GLASS_PLATING_GUID}, type: 2}
  modelSO: {fileID: 0}
  model: {fileID: ${modelRef.fileID}, guid: ${modelRef.guid}, type: 3}
  iconSO: {fileID: 0}
  icon: {fileID: 21300000, guid: ${iconGuid(r.id)}, type: 3}
  compositionSOs:
${compositionYaml(ingGuids(r.ings))}
  optionalSOs: []
  cookingStepSO: {fileID: 0}
  cookingStepIconSO: {fileID: 0}
  cookingStepIcon: {fileID: 0}
  cookingProgress: 0
  mixingIconSO: {fileID: 11400000, guid: ${BLENDER_ICON_GUID}, type: 2}
  mixingIcon: {fileID: 0}
  mixingProgress: 1
`;

function patchObj(raw, id) {
  const lines = raw.replace(/\r\n/g, "\n").split("\n");
  const out = [`mtllib ${id}.mtl`, `usemtl ${MAT_NAME}`];
  for (const line of lines) {
    if (line.startsWith("mtllib ") || line.startsWith("usemtl ")) continue;
    if (line.startsWith("o ")) out.push(`o ${id}`);
    else out.push(line);
  }
  return out.join("\n");
}

function buildMtl(texFile) {
  return `newmtl ${MAT_NAME}\nKd 1.000 1.000 1.000\nmap_Kd ${texFile}\n`;
}

function loadExistingNames() {
  const p = path.join(repoRoot, "Assets/commonW3/custom_recipes/names.json");
  if (!fs.existsSync(p)) return { schemaVersion: 1, names: [] };
  return JSON.parse(fs.readFileSync(p, "utf8").replace(/^\uFEFF/, ""));
}

function buildNamesJson() {
  const existing = loadExistingNames();
  const retired = new Set(RETIRED_MIXED_IDS);
  const names = (existing.names ?? []).filter((n) => {
    if (retired.has(n.id)) return false;
    if (n.id.startsWith("Web_Smoothie_Mix_")) return false;
    return true;
  });
  for (const r of MIXED_RECIPES) names.push({ id: r.id, zh: r.zh, en: r.en });
  return "\uFEFF" + JSON.stringify({ schemaVersion: 1, names }, null, 2) + "\n";
}

function buildConfigAsset() {
  const nextSequence = SALAD_COUNT + SINGLE_SMOOTHIE_COUNT + MIXED_RECIPES.length + 1;
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
  m_Script: {fileID: 11500000, guid: ${SCRIPT_RECIPE_CONFIG}, type: 3}
  m_Name: CustomRecipeConfig
  m_EditorClassIdentifier: 
  uidPrefix: ${UID_PREFIX}
  nextSequence: ${nextSequence}
  categories:
  - id: salad
    zh: "${yamlStr("沙拉大全")}"
    en: Salad
  - id: smoothie
    zh: "${yamlStr("Web 果汁大全")}"
    en: Web Smoothie
  subcategories: []
  modelTransforms: []
`;
}

/** 从 backup_rebuild/12果汁.png（3×4，左→右、上→下）导入前 12 张混果汁图标。 */
function importMixedIcons(apply) {
  if (!fs.existsSync(MIXED_ICON_SHEET)) throw new Error(`精灵图缺失: ${MIXED_ICON_SHEET}`);
  const ids = MIXED_RECIPES.slice(0, MIXED_ICON_GRID_SLOTS).map((r) => r.id);
  if (ids.length < MIXED_ICON_GRID_SLOTS) {
    throw new Error(`配方数 ${MIXED_RECIPES.length} 少于精灵图槽位 ${MIXED_ICON_GRID_SLOTS}`);
  }
  if (!apply) {
    for (let i = 0; i < ids.length; i++) {
      const col = i % MIXED_ICON_COLS;
      const row = Math.floor(i / MIXED_ICON_COLS);
      console.log(`[dry] icons/${ids[i]}.png  ← 12果汁.png [${row},${col}]`);
    }
    console.log(`\ndry-run：${ids.length} 张图标待导入。加 --apply 执行。`);
    return;
  }
  fs.mkdirSync(COMMONW3_ICONS, { recursive: true });
  const pyHelper = path.join(path.dirname(fileURLToPath(import.meta.url)), ".import-mixed-icons-tmp.py");
  const py = String.raw`
import json, sys, base64, io
from pathlib import Path
from PIL import Image

payload = json.loads(sys.argv[1])
sheet = Path(payload["sheet"])
img = Image.open(sheet).convert("RGBA")
w, h = img.size
cols, rows = payload["cols"], payload["rows"]
cw, ch = w // cols, h // rows
out = {}
for i, rid in enumerate(payload["ids"]):
    c = i % cols
    r = i // cols
    crop = img.crop((c * cw, r * ch, (c + 1) * cw, (r + 1) * ch))
    buf = io.BytesIO()
    crop.save(buf, format="PNG")
    out[rid] = base64.b64encode(buf.getvalue()).decode("ascii")
print(json.dumps(out))
`;
  fs.writeFileSync(pyHelper, py, "utf8");
  try {
    const arg = JSON.stringify({
      sheet: MIXED_ICON_SHEET,
      cols: MIXED_ICON_COLS,
      rows: MIXED_ICON_ROWS,
      ids,
    });
    const raw = execSync(`python3 "${pyHelper}" ${JSON.stringify(arg)}`, {
      encoding: "utf8",
      maxBuffer: 32 * 1024 * 1024,
    });
    const parsed = JSON.parse(raw.trim());
    for (const r of MIXED_RECIPES) {
      const png = path.join(COMMONW3_ICONS, `${r.id}.png`);
      fs.writeFileSync(png, Buffer.from(parsed[r.id], "base64"));
      fs.writeFileSync(png + ".meta", ICON_META(iconGuid(r.id)), "utf8");
      console.log(`写入 icons/${r.id}.png`);
    }
  } finally {
    try {
      fs.unlinkSync(pyHelper);
    } catch {
      /* ignore */
    }
  }
  syncLayoutEditorIcons();
  console.log(`\n完成：${ids.length} 张混果汁图标已导入并同步到 layout-editor。`);
}

function syncLayoutEditorIcons() {
  if (!fs.existsSync(COMMONW3_ICONS)) return;
  fs.mkdirSync(LAYOUT_RECIPE_ICONS, { recursive: true });
  const hasDist = fs.existsSync(path.dirname(LAYOUT_DIST_RECIPE_ICONS));
  if (hasDist) fs.mkdirSync(LAYOUT_DIST_RECIPE_ICONS, { recursive: true });
  const icons = fs.readdirSync(COMMONW3_ICONS).filter((f) => f.endsWith(".png"));
  for (const f of icons) {
    const src = path.join(COMMONW3_ICONS, f);
    fs.copyFileSync(src, path.join(LAYOUT_RECIPE_ICONS, f));
    if (hasDist) fs.copyFileSync(src, path.join(LAYOUT_DIST_RECIPE_ICONS, f));
    console.log(`同步 layout-editor 图标 ${f}`);
  }
}

const W3 = "Assets/commonW3";

function buildPlan() {
  if (!fs.existsSync(SHARED_OBJ)) throw new Error(`共享 OBJ 缺失: ${SHARED_OBJ}`);
  const sharedObjRaw = fs.readFileSync(SHARED_OBJ, "utf8");
  const out = [];
  const add = (rel, content, metaFn, metaGuid, opts = {}) => {
    out.push({
      rel,
      content,
      meta: metaFn ? metaFn(metaGuid ?? assetGuid(rel)) : null,
      binary: !!opts.binary,
      note: opts.note ?? "",
    });
  };

  add(`${W3}/custom_recipes/CustomRecipeConfig.asset`, buildConfigAsset(), ASSET_META, null, {
    note: `更新 nextSequence（含 ${MIXED_RECIPES.length} 混果汁）`,
  });
  add(`${W3}/custom_recipes/names.json`, buildNamesJson(), TEXT_META, null, {
    note: `追加 ${MIXED_RECIPES.length} 条混果汁名称`,
  });

  MIXED_RECIPES.forEach((r, i) => {
    const uid = mixedRecipeUid(r, i);
    const texName = `${r.id}_base_color.png`;
    const texSrc = path.join(MEGA_DIR, "variants", r.id, texName);
    if (!fs.existsSync(texSrc)) throw new Error(`贴图缺失: ${texSrc}（先运行 gen-mega-smoothie-textures.mjs --apply）`);

    add(`${W3}/custom_recipes/smoothie/${r.id}.asset`, RECIPE_ASSET(r, uid, modelRefForRecipe(r.id)), ASSET_META, recipeGuid(r.id), {
      note: `${r.zh} uid=${uid}`,
    });

    const modelDir = `${W3}/custom_recipes/smoothie/models/${r.id}`;
    out.push({ rel: `${modelDir}.meta`, content: FOLDER_META(folderGuid(modelDir)), meta: null, note: "模型目录" });

    const objRel = `${modelDir}/${r.id}.obj`;
    add(objRel, patchObj(sharedObjRaw, r.id), OBJ_META, modelGuid(r.id), {
      note: "共用什锦果汁网格",
    });

    const mtlRel = `${modelDir}/${r.id}.mtl`;
    add(mtlRel, buildMtl(texName), null, null, { note: "漫反射材质" });

    const texRel = `${modelDir}/${texName}`;
    add(texRel, fs.readFileSync(texSrc), TEXTURE_META, assetGuid(texRel), {
      binary: true,
      note: `← variants/${r.id}`,
    });
  });

  return out;
}

/** 将已下架混饮的图标复制给目标菜谱（保留目标确定性 icon guid）。 */
function promoteIcon(fromId, toId) {
  const src = path.join(COMMONW3_ICONS, `${fromId}.png`);
  const dst = path.join(COMMONW3_ICONS, `${toId}.png`);
  if (fs.existsSync(dst)) return;
  if (!fs.existsSync(src)) {
    console.warn(`源图标缺失，跳过 ${fromId} → ${toId}: ${src}`);
    return;
  }
  fs.mkdirSync(COMMONW3_ICONS, { recursive: true });
  fs.copyFileSync(src, dst);
  fs.writeFileSync(dst + ".meta", ICON_META(iconGuid(toId)), "utf8");
  console.log(`图标 ${fromId}.png → ${toId}.png`);
}

function removeRetiredMixedAssets() {
  for (const id of RETIRED_MIXED_IDS) {
    const relPaths = [
      `${W3}/custom_recipes/smoothie/${id}.asset`,
      `${W3}/custom_recipes/smoothie/icons/${id}.png`,
      `${W3}/custom_recipes/smoothie/models/${id}`,
    ];
    for (const rel of relPaths) {
      const abs = path.join(repoRoot, rel);
      if (fs.existsSync(abs)) {
        fs.rmSync(abs, { recursive: true, force: true });
        console.log(`删除 ${rel}`);
      }
      const meta = abs + ".meta";
      if (fs.existsSync(meta)) {
        fs.rmSync(meta, { force: true });
        console.log(`删除 ${rel}.meta`);
      }
    }
    for (const iconDir of [LAYOUT_RECIPE_ICONS, LAYOUT_DIST_RECIPE_ICONS]) {
      const icon = path.join(iconDir, `${id}.png`);
      if (fs.existsSync(icon)) {
        fs.rmSync(icon, { force: true });
        console.log(`删除 layout-editor 图标 ${id}.png`);
      }
    }
  }
}

function cleanStaleModelFiles(modelDirRel, keepNames) {
  const absDir = path.join(repoRoot, modelDirRel);
  if (!fs.existsSync(absDir)) return;
  const keep = new Set(keepNames);
  for (const name of fs.readdirSync(absDir)) {
    if (name.endsWith(".meta") || name.endsWith(".prefab")) continue;
    if (keep.has(name)) continue;
    const abs = path.join(absDir, name);
    fs.rmSync(abs, { force: true });
    const meta = abs + ".meta";
    if (fs.existsSync(meta)) fs.rmSync(meta, { force: true });
    console.log(`删除旧文件 ${modelDirRel}/${name}`);
  }
}

const apply = process.argv.includes("--apply");
const syncIconsOnly = process.argv.includes("--sync-icons");
const importIcons = process.argv.includes("--import-icons");

if (importIcons) {
  importMixedIcons(apply);
  process.exit(0);
}

if (syncIconsOnly) {
  syncLayoutEditorIcons();
  console.log("\n完成：layout-editor 果汁图标已同步。");
  process.exit(0);
}

const plan = buildPlan();
const modelKeeps = new Map();
for (const r of MIXED_RECIPES) {
  const modelDir = `${W3}/custom_recipes/smoothie/models/${r.id}`;
  modelKeeps.set(modelDir, [`${r.id}.obj`, `${r.id}.mtl`, `${r.id}_base_color.png`]);
}

for (const item of plan) {
  if (!apply) {
    console.log(`[dry] ${item.rel}${item.note ? `  (${item.note})` : ""}`);
    continue;
  }
  const abs = path.join(repoRoot, item.rel);
  fs.mkdirSync(path.dirname(abs), { recursive: true });
  if (item.binary) fs.writeFileSync(abs, item.content);
  else fs.writeFileSync(abs, item.content, "utf8");
  if (item.meta) fs.writeFileSync(abs + ".meta", item.meta, "utf8");
  console.log(`写入 ${item.rel}${item.note ? `  (${item.note})` : ""}`);
}

if (apply) {
  for (const r of MIXED_RECIPES) {
    if (r.iconCopyFrom) promoteIcon(r.iconCopyFrom, r.id);
  }
  removeRetiredMixedAssets();
  for (const [dir, names] of modelKeeps) cleanStaleModelFiles(dir, names);
  syncLayoutEditorIcons();
}

if (!apply) {
  console.log(`\ndry-run：${plan.length} 个文件待生成（不含 icons/）。加 --apply 执行。`);
} else {
  console.log(`\n完成：${plan.length} 个文件已写入。请在 Unity 中 Bake commonW3 Smoothie Models。`);
}
