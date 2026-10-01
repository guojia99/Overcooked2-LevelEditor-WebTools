#!/usr/bin/env node
/**
 * 生成 commonW3「🥤 Web 果汁大全」共享库（单果搅拌机果汁：葡萄/橙子/桃子 + 蓝莓/黑莓/覆盆子）。
 *
 * 配方字段与 backup_20260911/汁/*.asset 一致（搅拌杯，非搅拌碗）：
 *   type 3 (Mixed) + platingStepSO Glass + mixingIconSO BlenderIcon + mixingProgress Mixed；
 *   cookingStepSO 留空；3× 相同水果食材。
 * 模型/贴图来自 backup_rebuild Models。
 * 图标：仅手动维护于 Assets/commonW3/custom_recipes/smoothie/icons/（--apply 不写入、不覆盖）。
 *
 * guid 纪律（md5 确定性）：
 *   目录    md5("commonw3-folder:<相对路径>")
 *   菜谱    md5("commonw3-recipe:<id>")
 *   图标    md5("commonw3-icon:<id>")
 *   FBX     md5("commonw3-model:<id>")
 *   其余    md5("commonw3-asset:<相对路径>")
 *
 * 用法：
 *   node gen-commonw3-smoothies.mjs [--apply]（默认 dry-run）
 *   node gen-commonw3-smoothies.mjs --sync-icons（仅把 commonW3/icons 同步到 layout-editor）
 * 后处理：commonW3 烘焙菜单已下线，如需重烘焙走 web 菜谱管理流程
 */
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { fbxToNormalizedObj } from "./lib/fbx-mesh-to-obj.mjs";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const workspaceRoot = path.resolve(repoRoot, "..");
const LAYOUT_RECIPE_ICONS = path.join(repoRoot, "layout-editor/web/public/icons/recipes");
const LAYOUT_DIST_RECIPE_ICONS = path.join(repoRoot, "layout-editor/web/dist/icons/recipes");
const COMMONW3_ICONS = path.join(repoRoot, "Assets/commonW3/custom_recipes/smoothie/icons");
const BACKUP_MODELS = path.join(workspaceRoot, "backup_rebuild_20260917_132624/Models");
const BERRY_BACKUP = path.join(workspaceRoot, "backup_rebuild_20260917_132624/莓果饮料模型");
const SMOOTHIE_MODELS_DIR = path.join(repoRoot, "Assets/commonW3/custom_recipes/smoothie/models");

const SCRIPT_CUSTOM_RECIPE = "83fb008bcc8e793429b02c178c430815";
const SCRIPT_RECIPE_CONFIG = "5edb07060a0c6c140aa72ffb8fd3f021";
/** 搅拌杯步骤图标 BlenderIcon（≠ Mixer 搅拌碗 910d8538…）。 */
const BLENDER_ICON_GUID = "7a77fe98553178640a4b8a48d10057bc";
/** Glass 装盘（backup_20260911/汁 同款）。 */
const GLASS_PLATING_GUID = "9f2781f346fbd6d42a008e004f255d92";
const UID_PREFIX = 58322;
const SALAD_COUNT = 21;
/** 与 gen-commonw3-mixed-smoothies.mjs MIXED_RECIPES 数量保持同步 */
const MIXED_SMOOTHIE_COUNT = 12;
const SCORE = 80;

const RECIPES = [
  {
    id: "Web_Smoothie_Grape",
    zh: "Web 葡萄果汁",
    en: "Web Grape Smoothie",
    ingGuid: "067651d383836c606d0d325fe3b3dbd7",
    backupDir: "葡萄",
    fbxSrc: "葡萄.fbx",
    /** 贴图按 Web 自定义菜谱约定落盘为 {id}_{通道}.ext，并改写 FBX 内引用。 */
    textures: [
      { src: "葡萄_fbx_basecolor.JPEG", cls: "base_color" },
      { src: "葡萄_fbx_metallic.JPEG", cls: "metallic" },
      { src: "葡萄_fbx_normal.JPEG", cls: "normal" },
      { src: "葡萄_fbx_roughness.JPEG", cls: "roughness" },
    ],
  },
  {
    id: "Web_Smoothie_Orange",
    zh: "Web 橙子果汁",
    en: "Web Orange Smoothie",
    ingGuid: "c0721b5197a5193a62cac90b2e010560",
    backupDir: "橙子",
    fbxSrc: "橙子果汁.fbx",
    textures: [
      { src: "橘子贴图 (1).png", cls: "base_color" },
      { src: "橘子贴图 (2).png", cls: "normal" },
      { src: "橘子贴图 (4).png", cls: "metallic" },
    ],
  },
  {
    id: "Web_Smoothie_Peach",
    zh: "Web 桃子果汁",
    en: "Web Peach Smoothie",
    ingGuid: "c4a38be9154f4a2dfba533c6fdb72aa9",
    backupDir: "桃子",
    fbxSrc: "桃子果汁.fbx",
    textures: [
      { src: "桃子果汁 (1).png", cls: "base_color" },
      { src: "桃子果汁 (2).png", cls: "normal" },
      { src: "桃子果汁 (3).png", cls: "roughness" },
      { src: "桃子果汁 (4).png", cls: "metallic" },
    ],
  },
  {
    id: "Web_Smoothie_Blueberry",
    zh: "Web 蓝莓果汁",
    en: "Web Blueberry Smoothie",
    ingGuid: "a192356a1fd01504eadf0d90ffa2de62",
    uid: 58322038,
    berryBackup: true,
    backupDir: "蓝莓",
    fbxSrc: "蓝莓.fbx",
    iconSrc: "icon.png",
    textures: [
      { src: "texture_pbr_20250901.png", cls: "base_color" },
      { src: "texture_pbr_20250901_normal.png", cls: "normal" },
      { src: "texture_pbr_20250901_metallic.png", cls: "metallic" },
      { src: "texture_pbr_20250901_roughness.png", cls: "roughness" },
    ],
  },
  {
    id: "Web_Smoothie_Blackberry",
    zh: "Web 黑莓果汁",
    en: "Web Blackberry Smoothie",
    ingGuid: "1e87aab4d91d5e2b460a8de8fe55b737",
    uid: 58322039,
    berryBackup: true,
    backupDir: "黑莓",
    fbxSrc: "黑莓.fbx",
    iconSrc: "icon.png",
    textures: [
      { src: "texture_pbr_20250901.png", cls: "base_color" },
      { src: "texture_pbr_20250901_normal.png", cls: "normal" },
      { src: "texture_pbr_20250901_metallic.png", cls: "metallic" },
      { src: "texture_pbr_20250901_roughness.png", cls: "roughness" },
    ],
  },
  {
    id: "Web_Smoothie_Raspberry",
    zh: "Web 覆盆子果汁",
    en: "Web Raspberry Smoothie",
    ingGuid: "2c4b11eca006b60f3ae5a95e5cf4c609",
    uid: 58322040,
    berryBackup: true,
    backupDir: "覆盆子",
    fbxSrc: "覆盆子.fbx",
    iconSrc: "icon.png",
    textures: [
      { src: "texture_pbr_20250901.png", cls: "base_color" },
      { src: "texture_pbr_20250901_normal.png", cls: "normal" },
      { src: "texture_pbr_20250901_metallic.png", cls: "metallic" },
      { src: "texture_pbr_20250901_roughness.png", cls: "roughness" },
    ],
  },
];

function recipeSrcDir(r) {
  return r.berryBackup ? path.join(BERRY_BACKUP, r.backupDir) : path.join(BACKUP_MODELS, r.backupDir);
}

function recipeUid(r, index) {
  if (r.uid != null) return r.uid;
  return UID_PREFIX * 1000 + SALAD_COUNT + index + 1;
}

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

const FOLDER_META = (guid, bundleName = "") => `fileFormatVersion: 2
guid: ${guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: ${bundleName}
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

const TEXTURE_META = (guid, { normalMap = false } = {}) => `fileFormatVersion: 2
guid: ${guid}
TextureImporter:
  fileIDToRecycleName: {}
  externalObjects: {}
  serializedVersion: 4
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: ${normalMap ? 0 : 1}
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
  textureType: ${normalMap ? 1 : 0}
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

const FBX_META = (guid) => `fileFormatVersion: 2
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

function modelExtForRecipe(r) {
  return r.berryBackup ? "obj" : "fbx";
}

/** 从 models/<id>/<id>.{fbx|obj}.meta 解析 model 引用（对齐 backup_20260911/汁）。 */
function modelRefForRecipe(recipeId, ext) {
  const metaPath = path.join(SMOOTHIE_MODELS_DIR, recipeId, `${recipeId}.${ext}.meta`);
  if (fs.existsSync(metaPath)) {
    const raw = fs.readFileSync(metaPath, "utf8");
    const guid = /^guid: ([a-f0-9]+)/m.exec(raw)?.[1];
    if (guid) {
      const fileID = /^\s+100002:/m.test(raw) ? 100002 : 100000;
      return { fileID, guid };
    }
  }
  // 首次落盘前 Unity 尚未生成 .meta；OBJ 莓果与混果汁一致用 100000
  const fileID = ext === "obj" ? 100000 : 100002;
  return { fileID, guid: modelGuid(recipeId) };
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
  score: ${SCORE}
  platingStepSO: {fileID: 11400000, guid: ${GLASS_PLATING_GUID}, type: 2}
  modelSO: {fileID: 0}
  model: {fileID: ${modelRef.fileID}, guid: ${modelRef.guid}, type: 3}
  iconSO: {fileID: 0}
  icon: {fileID: 21300000, guid: ${iconGuid(r.id)}, type: 3}
  compositionSOs:
  - {fileID: 11400000, guid: ${r.ingGuid}, type: 2}
  - {fileID: 11400000, guid: ${r.ingGuid}, type: 2}
  - {fileID: 11400000, guid: ${r.ingGuid}, type: 2}
  optionalSOs: []
  cookingStepSO: {fileID: 0}
  cookingStepIconSO: {fileID: 0}
  cookingStepIcon: {fileID: 0}
  cookingProgress: 0
  mixingIconSO: {fileID: 11400000, guid: ${BLENDER_ICON_GUID}, type: 2}
  mixingIcon: {fileID: 0}
  mixingProgress: 1
`;

/** 与 customRecipes.ts texDiskName / SanitizeUploadFileName 一致：{recipeId}_{通道}{扩展名}。 */
function texDiskName(recipeId, cls, srcFile) {
  const m = /\.([^.]+)$/.exec(srcFile);
  let ext = m ? `.${m[1].toLowerCase()}` : ".png";
  if (ext !== ".png" && ext !== ".jpg" && ext !== ".jpeg") ext = ".png";
  return `${recipeId}_${cls}${ext}`;
}

function isNormalMapClass(cls) {
  return cls === "normal";
}

/** 调用 web/src/fbxTextureRename.ts（与自定义菜谱上传管线同一套 FBX 引用改写）。 */
function renameFbxOnDisk(fbxBytes, diskNames) {
  const helper = path.join(repoRoot, "layout-editor/scripts/.rename-fbx-textures-tmp.mjs");
  const payloadPath = path.join(repoRoot, "layout-editor/scripts/.rename-fbx-textures-payload.json");
  const outPath = path.join(repoRoot, "layout-editor/scripts/.rename-fbx-textures-out.json");
  fs.writeFileSync(
    helper,
    `import fs from "node:fs";
import { renameFbxTextureRefs } from "../web/src/fbxTextureRename.ts";
const payload = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const raw = Uint8Array.from(Buffer.from(payload.b64, "base64"));
const r = renameFbxTextureRefs(raw, payload.diskNames ?? {});
fs.writeFileSync(process.argv[3], JSON.stringify({ b64: Buffer.from(r.bytes).toString("base64"), renamed: r.renamed }));
`,
    "utf8"
  );
  try {
    fs.writeFileSync(
      payloadPath,
      JSON.stringify({ b64: Buffer.from(fbxBytes).toString("base64"), diskNames }),
      "utf8",
    );
    execSync(`node --experimental-strip-types "${helper}" "${payloadPath}" "${outPath}"`, {
      encoding: "utf8",
      maxBuffer: 64 * 1024 * 1024,
    });
    const parsed = JSON.parse(fs.readFileSync(outPath, "utf8"));
    return { bytes: Uint8Array.from(Buffer.from(parsed.b64, "base64")), renamed: parsed.renamed };
  } finally {
    for (const p of [helper, payloadPath, outPath]) {
      try {
        fs.unlinkSync(p);
      } catch {
        /* ignore */
      }
    }
  }
}

/** 将 commonW3 已落盘的果汁图标同步到 layout-editor 静态目录（离线回退 /icons/recipes/<id>.png）。 */
function syncLayoutEditorIcons() {
  fs.mkdirSync(LAYOUT_RECIPE_ICONS, { recursive: true });
  const hasDist = fs.existsSync(path.dirname(LAYOUT_DIST_RECIPE_ICONS));
  if (hasDist) fs.mkdirSync(LAYOUT_DIST_RECIPE_ICONS, { recursive: true });
  for (const r of RECIPES) {
    const src = path.join(COMMONW3_ICONS, `${r.id}.png`);
    if (!fs.existsSync(src)) throw new Error(`图标缺失: ${src}`);
    const pub = path.join(LAYOUT_RECIPE_ICONS, `${r.id}.png`);
    fs.copyFileSync(src, pub);
    if (hasDist) fs.copyFileSync(src, path.join(LAYOUT_DIST_RECIPE_ICONS, `${r.id}.png`));
    console.log(`同步 layout-editor 图标 ${r.id}.png`);
  }
}

/** dry-run 时报告 icons/ 下各 PNG 是否存在（apply 不写入图标）。 */
function reportIconStatus() {
  for (const r of RECIPES) {
    const p = path.join(COMMONW3_ICONS, `${r.id}.png`);
    const ok = fs.existsSync(p);
    console.log(`[icon] ${r.id}.png  ${ok ? "已存在（保留，不写入）" : "缺失 — 请手动放入 icons/"}`);
  }
}

function loadExistingNames() {
  const p = path.join(repoRoot, "Assets/commonW3/custom_recipes/names.json");
  if (!fs.existsSync(p)) return { schemaVersion: 1, names: [] };
  const raw = fs.readFileSync(p, "utf8").replace(/^\uFEFF/, "");
  return JSON.parse(raw);
}

function buildNamesJson() {
  const existing = loadExistingNames();
  const smoothieIds = new Set(RECIPES.map((r) => r.id));
  const names = (existing.names ?? []).filter((n) => !smoothieIds.has(n.id));
  for (const r of RECIPES) names.push({ id: r.id, zh: r.zh, en: r.en });
  return "\uFEFF" + JSON.stringify({ schemaVersion: 1, names }, null, 2) + "\n";
}

function loadExistingNextSequence() {
  const p = path.join(repoRoot, "Assets/commonW3/custom_recipes/CustomRecipeConfig.asset");
  const target = SALAD_COUNT + RECIPES.length + MIXED_SMOOTHIE_COUNT + 1;
  if (!fs.existsSync(p)) return target;
  const m = fs.readFileSync(p, "utf8").match(/nextSequence:\s*(\d+)/);
  const cur = m ? parseInt(m[1], 10) : 0;
  return Math.max(cur, target);
}

function buildConfigAsset() {
  const nextSequence = loadExistingNextSequence();
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

const W3 = "Assets/commonW3";

function buildPlan() {
  const out = [];
  const add = (rel, content, metaFn, metaGuid, opts = {}) => {
    const guid = metaGuid ?? assetGuid(rel);
    let meta = null;
    if (metaFn) {
      meta = metaFn === TEXTURE_META ? TEXTURE_META(guid, { normalMap: !!opts.normalMap }) : metaFn(guid);
    }
    out.push({
      rel,
      content,
      meta,
      binary: !!opts.binary,
      note: opts.note ?? "",
    });
  };

  for (const d of [
    `${W3}/custom_recipes/smoothie`,
    `${W3}/custom_recipes/smoothie/icons`,
    `${W3}/custom_recipes/smoothie/models`,
  ]) {
    out.push({ rel: `${d}.meta`, content: FOLDER_META(folderGuid(d)), meta: null, note: "目录" });
  }

  add(`${W3}/custom_recipes/CustomRecipeConfig.asset`, buildConfigAsset(), ASSET_META, null, {
    note: "追加 smoothie 分类",
  });
  add(`${W3}/custom_recipes/names.json`, buildNamesJson(), TEXT_META, null, {
    note: `追加 ${RECIPES.length} 条单果汁名称`,
  });

  RECIPES.forEach((r, i) => {
    const uid = recipeUid(r, i);
    const modelExt = modelExtForRecipe(r);
    add(`${W3}/custom_recipes/smoothie/${r.id}.asset`, RECIPE_ASSET(r, uid, modelRefForRecipe(r.id, modelExt)), ASSET_META, recipeGuid(r.id), {
      note: `${r.zh} uid=${uid}`,
    });

    const modelDir = `${W3}/custom_recipes/smoothie/models/${r.id}`;
    out.push({ rel: `${modelDir}.meta`, content: FOLDER_META(folderGuid(modelDir)), meta: null, note: "模型目录" });

    if (r.iconSrc) {
      const iconSrc = path.join(recipeSrcDir(r), r.iconSrc);
      if (!fs.existsSync(iconSrc)) throw new Error(`图标缺失: ${iconSrc}`);
      const iconRel = `${W3}/custom_recipes/smoothie/icons/${r.id}.png`;
      add(iconRel, fs.readFileSync(iconSrc), ICON_META, iconGuid(r.id), {
        binary: true,
        note: `← ${r.backupDir}/${r.iconSrc}`,
      });
    }

    const srcDir = recipeSrcDir(r);
    const fbxSrc = path.join(srcDir, r.fbxSrc);
    if (!fs.existsSync(fbxSrc)) throw new Error(`FBX 缺失: ${fbxSrc}`);

    const diskNames = {};
    for (const tex of r.textures) {
      diskNames[tex.cls] = texDiskName(r.id, tex.cls, tex.src);
    }

    if (r.berryBackup) {
      // 莓果源模型杯体偏胖（Z/Y≈0.72 vs 葡萄≈0.55）：归一化到 Web_Smoothie_Grape 包围盒后导出 OBJ
      const baseColorFile = diskNames.base_color;
      const { obj, mtl, vertexCount, faceCount } = fbxToNormalizedObj(fs.readFileSync(fbxSrc), {
        recipeId: r.id,
        baseColorFile,
      });
      const objRel = `${modelDir}/${r.id}.obj`;
      const mtlRel = `${modelDir}/${r.id}.mtl`;
      add(objRel, obj, OBJ_META, modelGuid(r.id), {
        note: `← ${r.fbxSrc} 归一化杯体（${vertexCount}v/${faceCount}f，对齐葡萄果汁比例）`,
      });
      add(mtlRel, mtl, TEXT_META, assetGuid(mtlRel), { note: "OBJ 材质" });
    } else {
      const fbxRel = `${modelDir}/${r.id}.fbx`;
      const fbxRaw = new Uint8Array(fs.readFileSync(fbxSrc));
      const { bytes: fbxPatched, renamed } = renameFbxOnDisk(fbxRaw, diskNames);
      add(fbxRel, Buffer.from(fbxPatched), FBX_META, modelGuid(r.id), {
        binary: true,
        note: `← ${r.fbxSrc}（FBX 贴图引用改写 ${renamed} 处）`,
      });
    }

    for (const tex of r.textures) {
      const texSrc = path.join(srcDir, tex.src);
      if (!fs.existsSync(texSrc)) throw new Error(`贴图缺失: ${texSrc}`);
      const disk = diskNames[tex.cls];
      const texRel = `${modelDir}/${disk}`;
      add(texRel, fs.readFileSync(texSrc), TEXTURE_META, assetGuid(texRel), {
        binary: true,
        note: `${tex.src} → ${disk}`,
        normalMap: isNormalMapClass(tex.cls),
      });
    }
  });

  return out;
}

/** 清理 models/<id>/ 下旧的中文名贴图与过期 .meta（保留 prefab / _Icon）。 */
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

if (syncIconsOnly) {
  syncLayoutEditorIcons();
  console.log("\n完成：layout-editor 果汁图标已同步。");
  process.exit(0);
}

reportIconStatus();

const plan = buildPlan();

const modelKeeps = new Map();
for (const r of RECIPES) {
  const modelDir = `${W3}/custom_recipes/smoothie/models/${r.id}`;
  const names = r.berryBackup ? [`${r.id}.obj`, `${r.id}.mtl`] : [`${r.id}.fbx`];
  for (const tex of r.textures) names.push(texDiskName(r.id, tex.cls, tex.src));
  modelKeeps.set(modelDir, names);
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
  for (const [dir, names] of modelKeeps) cleanStaleModelFiles(dir, names);
  syncLayoutEditorIcons();
}

if (!apply) {
  console.log(`\ndry-run：${plan.length} 个文件待生成（不含 icons/*.png）。加 --apply 执行写入。`);
  console.log("仅同步 layout-editor 图标：node gen-commonw3-smoothies.mjs --sync-icons");
} else {
  console.log(`\n完成：${plan.length} 个文件已写入（icons/ 未改动），layout-editor 图标已同步。`);
  console.log("如需重新烘焙走 web 菜谱管理流程，然后构建 AssetBundles（commonW3）。");
}
