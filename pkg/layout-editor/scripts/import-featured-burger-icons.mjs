#!/usr/bin/env node
/**
 * import-featured-burger-icons.mjs —— 把 21 张裁剪好的汉堡精灵图导入 commonW2 icons
 * 并回填对应 21 个成品汉堡 .asset 的 icon 字段。
 *
 * 来源图：backup_20260911/汉堡精灵图/cut/<NN>_<RecipeName>.png（透明背景，含盘子，各高度自适应）
 * 目标：Assets/commonW2/custom_recipes/burger/icons/<RecipeName>.png (+ sprite .meta 新 guid)
 * 回填：对应 .asset 的 `icon: {fileID: 0}` → `icon: {fileID: 21300000, guid: <png guid>, type: 3}`
 *
 * sprite .meta 模板逐字段对齐现有 burger 图标（textureType:8 / spriteMode:1 / 21300000 子资产）。
 *
 * 用法：
 *   node layout-editor/scripts/import-featured-burger-icons.mjs           # dry-run
 *   node layout-editor/scripts/import-featured-burger-icons.mjs --apply   # 落盘
 */
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, "../..");
const CUT_DIR = "/Users/guojia/Desktop/oc2_editor/backup_20260911/汉堡精灵图/cut";
const ICONS_DIR = path.join(repoRoot, "Assets/commonW2/custom_recipes/burger/icons");
const BURGER_DIR = path.join(repoRoot, "Assets/commonW2/custom_recipes/burger");
const REPORT = path.join(here, "import-featured-burger-icons.report.json");
const APPLY = process.argv.includes("--apply");

// 本批 21 款 recipe id（与 gen-featured-burgers 生成的一致）
const RECIPE_IDS = [
  "ChickenMeatBurger", "DoubleMeatBurger", "DoubleChickenBurger",
  "CheeseDoubleMeatBurger", "CheeseDoubleChickenBurger", "CheeseDoubleChickenDoubleMeatBurger",
  "LettuceTomatoCheeseMeatBurger", "LettuceTomatoCheeseChickenBurger", "PineappleCheeseMeatBurger",
  "PineappleCheeseChickenBurger", "PineappleChickenMeatBurger", "LettuceCucumberCheeseDoubleMeatBurger",
  "LettuceTomatoCucumberCheeseMeatBurger", "LettuceTomatoCucumberCheeseChickenBurger", "LettuceTomatoCucumberChickenMeatBurger",
  "LettucePineappleCheeseChickenMeatBurger", "CucumberCheeseChickenMeatBurger", "TomatoCheeseDoubleMeatBurger",
  "LettuceCheeseDoubleChickenBurger", "TomatoPineappleCheeseChickenMeatBurger", "LettuceTomatoCheeseDoubleChickenBurger",
];

function newGuid() {
  return crypto.randomBytes(16).toString("hex");
}

/** sprite .meta（逐字段对齐现有 burger 图标；仅 guid 变化）。 */
function spriteMeta(guid) {
  return `fileFormatVersion: 2
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
}

/** 递归找 <RecipeName>.asset（在 burger 子目录下） */
function findAsset(id) {
  const stack = [BURGER_DIR];
  while (stack.length) {
    const d = stack.pop();
    for (const e of fs.readdirSync(d, { withFileTypes: true })) {
      const p = path.join(d, e.name);
      if (e.isDirectory()) {
        if (e.name === "icons" || e.name === "models") continue;
        stack.push(p);
      } else if (e.name === id + ".asset") {
        return p;
      }
    }
  }
  return null;
}

// 找 cut 源图：cut/<NN>_<id>.png
const cutFiles = fs.readdirSync(CUT_DIR).filter((f) => /^\d{2}_.*\.png$/.test(f));
function findCut(id) {
  const f = cutFiles.find((f) => f.replace(/^\d{2}_/, "").replace(/\.png$/, "") === id);
  return f ? path.join(CUT_DIR, f) : null;
}

const results = [];
let errors = 0;
for (const id of RECIPE_IDS) {
  const src = findCut(id);
  const asset = findAsset(id);
  const iconPng = path.join(ICONS_DIR, id + ".png");
  const guid = newGuid();
  const r = { id, src, asset, iconPng: path.relative(repoRoot, iconPng), guid };
  if (!src) { r.error = "cut 源图缺失"; errors++; }
  if (!asset) { r.error = (r.error ? r.error + "; " : "") + ".asset 缺失"; errors++; }
  if (fs.existsSync(iconPng + ".meta")) {
    // 复用已有 guid，避免每次跑生成新 guid（幂等）
    const m = /guid: ([0-9a-f]{32})/.exec(fs.readFileSync(iconPng + ".meta", "utf8"));
    if (m) r.guid = m[1];
  }
  results.push(r);
}

console.log(`导入特色汉堡图标（${APPLY ? "APPLY" : "DRY-RUN"}）：${RECIPE_IDS.length} 款`);
for (const r of results)
  console.log(`  ${r.error ? "✗ " + r.error + " " : "✓ "}${r.id}  icon.guid=${r.guid}`);

if (errors) {
  console.error(`\n有 ${errors} 处缺失，终止（不落盘）。`);
  process.exit(1);
}

if (APPLY) {
  for (const r of results) {
    // 1. 拷贝 png
    fs.copyFileSync(r.src, path.join(ICONS_DIR, r.id + ".png"));
    // 2. 写 sprite .meta（guid 幂等）
    fs.writeFileSync(path.join(ICONS_DIR, r.id + ".png.meta"), spriteMeta(r.guid));
    // 3. 回填 .asset 的 icon 字段
    let text = fs.readFileSync(r.asset, "utf8");
    const newIcon = `  icon: {fileID: 21300000, guid: ${r.guid}, type: 3}`;
    if (/^  icon: \{fileID: 0\}$/m.test(text)) {
      text = text.replace(/^  icon: \{fileID: 0\}$/m, newIcon);
    } else {
      text = text.replace(/^  icon: \{fileID: 21300000, guid: [0-9a-f]{32}, type: 3\}$/m, newIcon);
    }
    fs.writeFileSync(r.asset, text);
  }
  console.log(`\n已导入 ${results.length} 张图标 + 回填 icon 字段。`);
}

fs.writeFileSync(REPORT, JSON.stringify({ apply: APPLY, results }, null, 2));
console.log(`报告：layout-editor/scripts/import-featured-burger-icons.report.json`);
