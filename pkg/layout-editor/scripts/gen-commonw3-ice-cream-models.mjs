#!/usr/bin/env node
/**
 * 从 backup 冰淇淋 FBX（内嵌 PBR 贴图）生成 commonW3 模型目录。
 *
 * 流程：提取内嵌贴图 → 压缩至 512 → FBX 原始比例导出 OBJ（约 1.1× 放大 + 高度 15% 上移，不做果汁/冰沙杯体拉伸）→ 绑定菜谱 model 字段。
 * 目录结构对齐 milk_slush/models/Web_MilkSlush_Blackberry/。
 *
 * 用法：
 *   node gen-commonw3-ice-cream-models.mjs           # dry-run
 *   node gen-commonw3-ice-cream-models.mjs --apply
 *
 * 后处理：Unity → Layout Editor → Bake commonW3 Migration Models
 */
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { extractFbxEmbeddedTextures } from "./lib/fbx-extract-embedded-textures.mjs";
import { fbxToNormalizedObj, iceCreamExportOpts } from "./lib/fbx-mesh-to-obj.mjs";
import { ensureMeshSimplifier } from "./lib/mesh-simplify.mjs";
import { compressTexture, DEFAULT_TEXTURE_MAX } from "./lib/compress-texture.mjs";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const workspaceRoot = path.resolve(repoRoot, "..");
const W3 = "Assets/commonW3";
const BACKUP_MODELS = path.join(workspaceRoot, "backup_rebuild_20260917_132624/冰淇淋模型/模型");
const ICE_CREAM_DIR = path.join(repoRoot, `${W3}/custom_recipes/ice_cream`);

const TEXTURE_MAX = DEFAULT_TEXTURE_MAX;

const RECIPES = [
  { id: "Web_IceCream_Vanilla", fbx: "香草冰淇淋.fbx" },
  { id: "Web_IceCream_Chocolate", fbx: "巧克力冰淇淋.fbx" },
  { id: "Web_IceCream_Strawberry", fbx: "草莓冰淇淋.fbx" },
  { id: "Web_IceCream_Grape", fbx: "葡萄冰淇淋.fbx" },
  { id: "Web_IceCream_Orange", fbx: "橙子冰淇淋.fbx" },
  { id: "Web_IceCream_Peach", fbx: "桃子冰淇淋.fbx" },
  { id: "Web_IceCream_Blueberry", fbx: "蓝莓冰淇淋.fbx" },
  { id: "Web_IceCream_Blackberry", fbx: "黑莓冰淇淋.fbx" },
  { id: "Web_IceCream_Raspberry", fbx: "覆盆子冰淇淋.fbx" },
  { id: "Web_IceCream_Banana", fbx: "香蕉冰淇淋.fbx" },
  { id: "Web_IceCream_Melon", fbx: "西瓜冰淇淋.fbx" },
  { id: "Web_IceCream_Pineapple", fbx: "菠萝冰淇淋.fbx" },
];

const TEXTURE_CLASSES = ["base_color", "normal", "roughness", "metallic"];

const md5 = (s) => crypto.createHash("md5").update(s).digest("hex");
const folderGuid = (rel) => md5(`commonw3-folder:${rel}`);
const modelGuid = (id) => md5(`commonw3-model:${id}`);
const assetGuid = (rel) => md5(`commonw3-asset:${rel}`);

function texDiskName(recipeId, cls) {
  return `${recipeId}_${cls}.png`;
}

const FOLDER_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
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
  maxTextureSize: ${TEXTURE_MAX}
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
    maxTextureSize: ${TEXTURE_MAX}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
  - buildTarget: Standalone
    maxTextureSize: ${TEXTURE_MAX}
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

function bindModelInRecipe(assetPath, recipeId) {
  const guid = modelGuid(recipeId);
  const modelLine = `  model: {fileID: 100000, guid: ${guid}, type: 3}`;
  let raw = fs.readFileSync(assetPath, "utf8");
  if (raw.includes(modelLine)) return false;
  const replaced = raw.replace(/^  model: \{fileID: 0\}$/m, modelLine);
  if (replaced === raw) {
    const replaced2 = raw.replace(/^  model: \{fileID: \d+, guid: [a-f0-9]+, type: 3\}$/m, modelLine);
    if (replaced2 === raw) throw new Error(`${assetPath}: 无法更新 model 字段`);
    fs.writeFileSync(assetPath, replaced2, "utf8");
    return true;
  }
  fs.writeFileSync(assetPath, replaced, "utf8");
  return true;
}

function writeFile(abs, content, meta, binary = false) {
  fs.mkdirSync(path.dirname(abs), { recursive: true });
  if (binary) fs.writeFileSync(abs, content);
  else fs.writeFileSync(abs, content, "utf8");
  if (meta) fs.writeFileSync(`${abs}.meta`, meta, "utf8");
}

function processRecipe(r) {
  const fbxPath = path.join(BACKUP_MODELS, r.fbx);
  if (!fs.existsSync(fbxPath)) throw new Error(`FBX 缺失: ${fbxPath}`);

  const fbxBytes = fs.readFileSync(fbxPath);
  const textures = extractFbxEmbeddedTextures(fbxBytes);
  const texByCls = new Map(textures.map((t) => [t.cls, t]));
  if (!texByCls.has("base_color")) {
    throw new Error(`${r.id}: FBX 内未找到 base_color 贴图`);
  }

  const modelDirRel = `${W3}/custom_recipes/ice_cream/models/${r.id}`;
  const modelDirAbs = path.join(repoRoot, modelDirRel);
  const baseColorDisk = texDiskName(r.id, "base_color");

  const { obj, mtl, vertexCount, faceCount, sourceFaceCount } = fbxToNormalizedObj(fbxBytes, {
    recipeId: r.id,
    baseColorFile: baseColorDisk,
    ...iceCreamExportOpts(r.id),
  });

  const written = [];
  writeFile(path.join(modelDirAbs, `${r.id}.obj`), obj, OBJ_META(modelGuid(r.id)));
  const decimated = sourceFaceCount > faceCount ? ` ←${sourceFaceCount}f` : "";
  written.push(`${r.id}.obj (${vertexCount}v/${faceCount}f${decimated})`);

  writeFile(
    path.join(modelDirAbs, `${r.id}.mtl`),
    mtl,
    TEXT_META(assetGuid(`${modelDirRel}/${r.id}.mtl`)),
  );
  written.push(`${r.id}.mtl`);

  const tmpDir = path.join(repoRoot, "layout-editor/scripts/.tmp-ice-cream-tex");
  fs.mkdirSync(tmpDir, { recursive: true });

  for (const cls of TEXTURE_CLASSES) {
    const tex = texByCls.get(cls);
    if (!tex) continue;
    const disk = texDiskName(r.id, cls);
    const rawPath = path.join(tmpDir, `${r.id}_${cls}_raw.png`);
    const outAbs = path.join(modelDirAbs, disk);
    fs.writeFileSync(rawPath, Buffer.from(tex.bytes));
    compressTexture(rawPath, outAbs, TEXTURE_MAX);
    writeFile(
      outAbs,
      fs.readFileSync(outAbs),
      TEXTURE_META(assetGuid(`${modelDirRel}/${disk}`), { normalMap: cls === "normal" }),
      true,
    );
    const kb = Math.round(fs.statSync(outAbs).size / 1024);
    written.push(`${disk} (${kb}KB, ←${tex.fileName})`);
    try {
      fs.unlinkSync(rawPath);
    } catch {
      /* ignore */
    }
  }

  const folderMetaAbs = path.join(repoRoot, `${modelDirRel}.meta`);
  if (!fs.existsSync(folderMetaAbs)) {
    fs.writeFileSync(folderMetaAbs, FOLDER_META(folderGuid(modelDirRel)), "utf8");
  }

  const recipeAsset = path.join(ICE_CREAM_DIR, `${r.id}.asset`);
  bindModelInRecipe(recipeAsset, r.id);

  return { id: r.id, written, fbxMb: (fbxBytes.length / 1024 / 1024).toFixed(1) };
}

const apply = process.argv.includes("--apply");

async function main() {
if (!apply) {
  for (const r of RECIPES) {
    const fbxPath = path.join(BACKUP_MODELS, r.fbx);
    const ok = fs.existsSync(fbxPath);
    console.log(`[dry] ${r.id} ← ${r.fbx}  ${ok ? "OK" : "缺失"}`);
  }
  console.log("\ndry-run；加 --apply 落盘模型并绑定菜谱。");
  return;
}

await ensureMeshSimplifier();

fs.mkdirSync(path.join(repoRoot, `${W3}/custom_recipes/ice_cream/models`), { recursive: true });
const modelsMeta = path.join(repoRoot, `${W3}/custom_recipes/ice_cream/models.meta`);
if (!fs.existsSync(modelsMeta)) {
  fs.writeFileSync(modelsMeta, FOLDER_META(folderGuid(`${W3}/custom_recipes/ice_cream/models`)), "utf8");
}

let totalOut = 0;
for (const r of RECIPES) {
  const result = processRecipe(r);
  console.log(`\n${result.id} (源 FBX ${result.fbxMb}MB)`);
  for (const line of result.written) console.log(`  ${line}`);
  totalOut++;
}

try {
  fs.rmSync(path.join(repoRoot, "layout-editor/scripts/.tmp-ice-cream-tex"), { recursive: true, force: true });
} catch {
  /* ignore */
}

console.log(`\n完成：${totalOut} 道冰淇淋模型已落盘并绑定菜谱。`);
console.log("Unity → Layout Editor → Bake commonW3 Migration Models → Build AssetBundles（commonW3）");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
