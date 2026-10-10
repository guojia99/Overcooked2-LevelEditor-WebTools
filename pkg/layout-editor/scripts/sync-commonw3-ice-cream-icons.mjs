#!/usr/bin/env node
/**
 * 将冰淇淋带杯 icon 落盘到 commonW3，绑定 CustomRecipeSO.icon，并同步 layout-editor。
 *
 * 源图：backup_rebuild_20260917_132624/冰淇淋模型/icon/<口味>冰淇淋带杯子.png
 * 目标：Assets/commonW3/custom_recipes/ice_cream/icons/Web_IceCream_<Key>.png
 *
 * guid：md5("commonw3-icon:<id>")
 *
 * 用法：
 *   node sync-commonw3-ice-cream-icons.mjs           # dry-run
 *   node sync-commonw3-ice-cream-icons.mjs --apply
 */
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const workspaceRoot = path.resolve(repoRoot, "..");
const ICON_SRC_DIR = path.join(workspaceRoot, "backup_rebuild_20260917_132624/冰淇淋模型/icon");
const COMMONW3_ICONS = path.join(repoRoot, "Assets/commonW3/custom_recipes/ice_cream/icons");
const RECIPES_DIR = path.join(repoRoot, "Assets/commonW3/custom_recipes/ice_cream");
const LAYOUT_RECIPE_ICONS = path.join(repoRoot, "layout-editor/web/public/icons/recipes");
const LAYOUT_DIST_RECIPE_ICONS = path.join(repoRoot, "layout-editor/web/dist/icons/recipes");

const RECIPES = [
  { key: "Vanilla", zh: "香草" },
  { key: "Chocolate", zh: "巧克力" },
  { key: "Strawberry", zh: "草莓" },
  { key: "Grape", zh: "葡萄" },
  { key: "Orange", zh: "橙子" },
  { key: "Peach", zh: "桃子" },
  { key: "Blueberry", zh: "蓝莓" },
  { key: "Blackberry", zh: "黑莓" },
  { key: "Raspberry", zh: "树莓" },
  { key: "Banana", zh: "香蕉" },
  { key: "Melon", zh: "西瓜" },
  { key: "Pineapple", zh: "菠萝" },
].map((r) => ({
  ...r,
  id: `Web_IceCream_${r.key}`,
  srcName: `${r.zh}冰淇淋带杯子.png`,
}));

const md5 = (s) => crypto.createHash("md5").update(s).digest("hex");
const iconGuid = (id) => md5(`commonw3-icon:${id}`);

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

function bindIconInRecipe(assetPath, id) {
  const guid = iconGuid(id);
  const iconLine = `  icon: {fileID: 21300000, guid: ${guid}, type: 3}`;
  let raw = fs.readFileSync(assetPath, "utf8");
  if (raw.includes(iconLine)) return false;
  const replaced = raw.replace(/^  icon: \{fileID: 0\}$/m, iconLine);
  if (replaced === raw) {
    throw new Error(`${assetPath}: 未找到可替换的 icon: {fileID: 0} 行`);
  }
  fs.writeFileSync(assetPath, replaced, "utf8");
  return true;
}

function apply() {
  fs.mkdirSync(COMMONW3_ICONS, { recursive: true });
  fs.mkdirSync(LAYOUT_RECIPE_ICONS, { recursive: true });
  const hasDist = fs.existsSync(path.dirname(LAYOUT_DIST_RECIPE_ICONS));
  if (hasDist) fs.mkdirSync(LAYOUT_DIST_RECIPE_ICONS, { recursive: true });

  let iconsWritten = 0;
  let recipesBound = 0;

  for (const r of RECIPES) {
    const src = path.join(ICON_SRC_DIR, r.srcName);
    if (!fs.existsSync(src)) throw new Error(`源图标缺失: ${src}`);

    const dst = path.join(COMMONW3_ICONS, `${r.id}.png`);
    const dstMeta = `${dst}.meta`;
    fs.copyFileSync(src, dst);
    fs.writeFileSync(dstMeta, ICON_META(iconGuid(r.id)), "utf8");
    iconsWritten++;
    console.log(`图标 ${r.id}.png ← ${r.srcName}`);

    const recipeAsset = path.join(RECIPES_DIR, `${r.id}.asset`);
    if (!fs.existsSync(recipeAsset)) throw new Error(`菜谱缺失: ${recipeAsset}`);
    if (bindIconInRecipe(recipeAsset, r.id)) {
      recipesBound++;
      console.log(`绑定 ${r.id}.asset → icon guid ${iconGuid(r.id)}`);
    } else {
      console.log(`已绑定 ${r.id}.asset（跳过）`);
    }

    const pub = path.join(LAYOUT_RECIPE_ICONS, `${r.id}.png`);
    fs.copyFileSync(dst, pub);
    if (hasDist) fs.copyFileSync(dst, path.join(LAYOUT_DIST_RECIPE_ICONS, `${r.id}.png`));
    console.log(`同步 layout-editor ${r.id}.png`);
  }

  console.log(`\n完成：${iconsWritten} 图标，${recipesBound} 道菜谱新绑定 icon。`);
}

const dryRun = !process.argv.includes("--apply");
if (dryRun) {
  for (const r of RECIPES) {
    const src = path.join(ICON_SRC_DIR, r.srcName);
    const ok = fs.existsSync(src);
    console.log(`[dry] ${r.id} ← ${r.srcName}  ${ok ? "OK" : "缺失"}`);
  }
  console.log("\ndry-run；加 --apply 执行。");
} else {
  apply();
}
