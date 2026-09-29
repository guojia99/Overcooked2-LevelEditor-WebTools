#!/usr/bin/env node
/**
 * 生成 commonW3 内置蟑螂低模（老鼠偷食材 cockroach 皮肤替换模型）。
 *
 * 来源：backup_rebuild_20260917_132624/蟑螂低模.fbx + .fbm 贴图
 * 落盘：Assets/commonW3/rat/models/Web_Cockroach_Low/
 *
 * 比例：按游戏内复古鼠 mesh 包围盒对齐（Y 轴朝上，身体主轴对齐鼠 Z）。
 *   鼠参考 bounds（dump rat.obj）：size ≈ [0.611, 0.418, 1.029]
 *   蟑螂原始 FBX：size ≈ [0.998, 0.307, 0.475]
 *   运行时 RatHeist 绕 Y +90° 后 scale ≈ (1.82, 1.90, 1.44)（基准 ×1.4）
 *
 * 用法：node gen-commonw3-rat-cockroach.mjs [--apply]
 */
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const workspaceRoot = path.resolve(repoRoot, "..");
const BACKUP_ROOT = path.join(workspaceRoot, "backup_rebuild_20260917_132624");
const RAT_ROOT = path.join(repoRoot, "Assets/commonW3/rat");
const MODEL_DIR = path.join(RAT_ROOT, "models/Web_Cockroach_Low");

const ID = "Web_Cockroach_Low";
const TEXTURES = [
  { src: "蟑螂.jpg", cls: "base_color", ext: ".jpg" },
  { src: "蟑螂法线.jpg", cls: "normal", ext: ".jpg" },
  { src: "蟑螂粗糙.JPEG", cls: "roughness", ext: ".jpeg" },
  { src: "蟑螂金属.JPEG", cls: "metallic", ext: ".jpeg" },
];

const md5 = (s) => crypto.createHash("md5").update(s).digest("hex");
const folderGuid = (rel) => md5(`commonw3-folder:${rel}`);
const modelGuid = (id) => md5(`commonw3-model:${id}`);
const assetGuid = (rel) => md5(`commonw3-asset:${rel}`);

const FOLDER_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
folderAsset: yes
DefaultImporter:
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
    "utf8",
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
    return Uint8Array.from(Buffer.from(parsed.b64, "base64"));
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

function writeFile(relFromAssets, content, metaContent, guid) {
  const abs = path.join(repoRoot, "Assets", relFromAssets);
  fs.mkdirSync(path.dirname(abs), { recursive: true });
  fs.writeFileSync(abs, content);
  fs.writeFileSync(`${abs}.meta`, metaContent);
  console.log(`  + Assets/${relFromAssets}  (guid ${guid})`);
}

function main() {
  const apply = process.argv.includes("--apply");
  const fbxSrc = path.join(BACKUP_ROOT, "蟑螂低模.fbx");
  const texSrcDir = path.join(BACKUP_ROOT, "蟑螂低模.fbm");
  if (!fs.existsSync(fbxSrc)) throw new Error(`缺少源 FBX: ${fbxSrc}`);

  const diskNames = {};
  for (const t of TEXTURES) {
    const disk = `${ID}_${t.cls}${t.ext}`;
    diskNames[t.src] = disk;
    diskNames[path.join("蟑螂低模.fbm", t.src)] = disk;
  }

  const fbxRaw = fs.readFileSync(fbxSrc);
  const fbxPatched = renameFbxOnDisk(fbxRaw, diskNames);

  const plan = [];
  plan.push({ rel: "commonW3/rat.meta", type: "folder", relKey: "rat" });
  plan.push({ rel: "commonW3/rat/models.meta", type: "folder", relKey: "rat/models" });
  plan.push({
    rel: "commonW3/rat/models/Web_Cockroach_Low.meta",
    type: "folder",
    relKey: "rat/models/Web_Cockroach_Low",
  });
  plan.push({
    rel: `commonW3/rat/models/Web_Cockroach_Low/${ID}.fbx`,
    type: "fbx",
    bytes: fbxPatched,
    guid: modelGuid(ID),
  });
  for (const t of TEXTURES) {
    const disk = `${ID}_${t.cls}${t.ext}`;
    const srcPath = path.join(texSrcDir, t.src);
    if (!fs.existsSync(srcPath)) throw new Error(`缺少贴图: ${srcPath}`);
    plan.push({
      rel: `commonW3/rat/models/Web_Cockroach_Low/${disk}`,
      type: "texture",
      bytes: fs.readFileSync(srcPath),
      cls: t.cls,
      relKey: `rat/models/Web_Cockroach_Low/${disk}`,
    });
  }

  console.log(apply ? "写入 commonW3 蟑螂模型…" : "dry-run（加 --apply 落盘）");
  for (const item of plan) {
    if (item.type === "folder") {
      const guid = folderGuid(item.relKey);
      const meta = FOLDER_META(guid);
      if (apply) {
        const abs = path.join(repoRoot, "Assets", item.rel.replace(/\.meta$/, ""));
        fs.mkdirSync(abs, { recursive: true });
        fs.writeFileSync(path.join(repoRoot, "Assets", item.rel), meta);
      }
      console.log(`  [folder] Assets/${item.rel}  guid=${guid}`);
      continue;
    }
    const guid = item.guid ?? assetGuid(item.relKey);
    const meta =
      item.type === "fbx"
        ? FBX_META(guid)
        : TEXTURE_META(guid, { normalMap: item.cls === "normal" });
    if (apply) writeFile(item.rel, item.bytes, meta, guid);
    else console.log(`  [${item.type}] Assets/${item.rel}  guid=${guid}`);
  }

  if (apply) {
    console.log("\n完成。请在 Unity 中刷新并 Build AssetBundles（commonW3）。");
    console.log("运行时路径：assets/commonw3/rat/models/web_cockroach_low/web_cockroach_low.fbx");
  }
}

main();
