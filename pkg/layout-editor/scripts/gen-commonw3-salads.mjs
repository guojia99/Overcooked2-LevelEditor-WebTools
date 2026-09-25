#!/usr/bin/env node
/**
 * 生成 commonW3「🥗 沙拉大全」共享库（纯数据包：DLC11 食材全排列，零官方组成重复）。
 *
 * 背景：官方 DLC11 沙拉共 6 道（salad_corn/cucumber/tomato/cucumber_tomato_onion、
 * tomato_corn/cucumber_onion），全部含洋葱底、salad_ 前缀额外带生菜。本库三组矩阵：
 *  ① 洋葱底（无生菜）：官方没有的组合；
 *  ② 洋葱底 + 生菜（官方副本 4 道，与官方组成相同，同关勿与官方 dlc11 matchlist
 *     混用，W2 官方汉堡副本同款先例）；
 *  ③ 生菜底（无洋葱，Web_LSalad_*）：生菜代替洋葱作底——组成上天然与 ①② 及
 *     官方全部去重（它们都含洋葱），命名加「生菜底」标记避免与「Web X沙拉」撞名。
 * 命名统一 Web 前缀。
 *
 * 组成食材全部引用 common03 整颗 DLC11 食材 SO（运行时 GetIngredientOrderNode
 * 经 WorkableItem 链解析切碎节点——与 commonW2 汉堡/DLC11 玉米同机制）；
 * 成品模型 dlc11_compositesalad（bundle428，官方 6 道沙拉共用）；装盘 Plate。
 *
 * guid 纪律（md5 确定性，重跑内容不变）：
 *   目录    md5("commonw3-folder:<相对路径>")
 *   菜谱    md5("commonw3-recipe:<id>")
 *   图标    md5("commonw3-icon:<id>")
 *   模型 SO md5("pseudo:dlc11_compositesalad")（对齐 import-dlc-content pseudo: 命名空间）
 *   其余    md5("commonw3-asset:<相对路径>")
 *
 * 图标：默认统一缺省（中性灰「？」占位，脚本内置生成，guid 稳定）——
 * 后续补图时直接覆盖 icons/*.png 字节即可，零引用改动。
 * 启用原版图标的 6 道：官方副本 3 道（dlc11_ui_*，组成与官方一致）+
 * 生菜底 3 道（bundle22 ui_lettucesalad/_01 系列，画面本就是无洋葱的
 * 生菜底沙拉，128x120 同规格）。
 *
 * 用法：node gen-commonw3-salads.mjs [--apply]（默认 dry-run 只打印计划）
 */
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import zlib from "node:zlib";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");

/** dump_bundle/Assets 根（只读源；icon 字段为相对它的 png 路径，不带扩展名）。 */
const DUMP_ASSETS = path.join(repoRoot, "dump_bundle/Assets");

// ---------------------------------------------------------------- 常量

/** common03 整颗 DLC11 食材 SO（运行时解析为切碎节点，食材箱/菜谱卡片同 id）。 */
const ING = {
  L: { guid: "a981e8745a1cda0a67d5b89c7dc72790" }, // DLC11_Lettuce 生菜
  T: { guid: "8f92929d152f8c5d1148ad15a002b20c" }, // DLC11_Tomato 番茄
  C: { guid: "346c97285c09802fc24b95dd5ed2c4ed" }, // DLC11_Cucumber 黄瓜
  N: { guid: "8d4a5aaf71fdf648b0648dd20ae1a920" }, // DLC11_Corn 玉米
  O: { guid: "1d5a4daa4406430cac79e018fea51a55" }, // DLC11_Onion_Salad 沙拉洋葱
};
const PLATE_GUID = "02b04fdf6fef0e944870fd5e06375ed1"; // common01 PlatingSteps/Plate
const SCRIPT_CUSTOM_RECIPE = "83fb008bcc8e793429b02c178c430815"; // CustomRecipeSO
const SCRIPT_PSEUDO_SO = "0cff7c13895ab9e47a5e02d4619cc3b9"; // PseudoPrefabSO
const SCRIPT_RECIPE_CONFIG = "5edb07060a0c6c140aa72ffb8fd3f021"; // CustomRecipeConfigSO
const UID_PREFIX = 58322;

/** 组成顺序：生菜 → 番茄 → 黄瓜 → 玉米 → 洋葱垫底（对齐官方 composition 顺序）。
 *  icon = 官方原图标文件名（dump_bundle dlc11 gui/icons，不带 .png），
 *  省略则用统一缺省占位。官方副本与官方菜谱组成相同，同关勿混用（W2 官方汉堡副本同款先例）。 */
const RECIPES = [
  { id: "Web_Salad_Lettuce", zh: "Web 生菜沙拉", en: "Web Lettuce Salad", ings: ["L", "O"] },
  { id: "Web_Salad_Tomato", zh: "Web 番茄沙拉", en: "Web Tomato Salad", ings: ["T", "O"] },
  { id: "Web_Salad_Cucumber", zh: "Web 黄瓜沙拉", en: "Web Cucumber Salad", ings: ["C", "O"] },
  { id: "Web_Salad_Corn", zh: "Web 玉米沙拉", en: "Web Corn Salad", ings: ["N", "O"] },
  { id: "Web_Salad_CucumberCorn", zh: "Web 黄瓜玉米沙拉", en: "Web Cucumber Corn Salad", ings: ["C", "N", "O"] },
  { id: "Web_Salad_TomatoCucumberCorn", zh: "Web 番茄黄瓜玉米沙拉", en: "Web Tomato Cucumber Corn Salad", ings: ["T", "C", "N", "O"] },
  // ---- ② 洋葱底 + 生菜（官方副本 4 道，与官方组成相同；3 道启用官方原图标）----
  { id: "Web_Salad_LettuceTomato", zh: "Web 生菜番茄沙拉", en: "Web Lettuce Tomato Salad", ings: ["L", "T", "O"], icon: "downloadablecontent/dlc11/dlc_assets/gui/icons/dlc11_ui_onionlettucetomato" },
  { id: "Web_Salad_LettuceCucumber", zh: "Web 生菜黄瓜沙拉", en: "Web Lettuce Cucumber Salad", ings: ["L", "C", "O"] },
  { id: "Web_Salad_LettuceCorn", zh: "Web 生菜玉米沙拉", en: "Web Lettuce Corn Salad", ings: ["L", "N", "O"], icon: "downloadablecontent/dlc11/dlc_assets/gui/icons/dlc11_ui_onionlettucecorn" },
  { id: "Web_Salad_LettuceTomatoCucumber", zh: "Web 生菜番茄黄瓜沙拉", en: "Web Lettuce Tomato Cucumber Salad", ings: ["L", "T", "C", "O"], icon: "downloadablecontent/dlc11/dlc_assets/gui/icons/dlc11_ui_onionlettucetomatocucumber" },
  // ---- ②′ 洋葱底 × 生菜 + 玉米三拼/全家福（官方没有的组合）----
  { id: "Web_Salad_LettuceTomatoCorn", zh: "Web 生菜番茄玉米沙拉", en: "Web Lettuce Tomato Corn Salad", ings: ["L", "T", "N", "O"] },
  { id: "Web_Salad_LettuceCucumberCorn", zh: "Web 生菜黄瓜玉米沙拉", en: "Web Lettuce Cucumber Corn Salad", ings: ["L", "C", "N", "O"] },
  { id: "Web_Salad_LettuceTomatoCucumberCorn", zh: "Web 田园全家福沙拉", en: "Web Garden Salad", ings: ["L", "T", "C", "N", "O"] },
  // ---- ③ 生菜底（无洋葱）：生菜代替洋葱作底，天然与 ①②/官方去重 ----
  { id: "Web_LSalad_Lettuce", zh: "Web 纯生菜沙拉", en: "Web Plain Lettuce Salad", ings: ["L"], icon: "gui/icons/recipes/ui_lettucesalad_01" },
  { id: "Web_LSalad_Tomato", zh: "Web 生菜底番茄沙拉", en: "Web Tomato Salad (Lettuce Base)", ings: ["L", "T"], icon: "gui/icons/recipes/ui_lettucetomatosalad_01" },
  { id: "Web_LSalad_Cucumber", zh: "Web 生菜底黄瓜沙拉", en: "Web Cucumber Salad (Lettuce Base)", ings: ["L", "C"] },
  { id: "Web_LSalad_Corn", zh: "Web 生菜底玉米沙拉", en: "Web Corn Salad (Lettuce Base)", ings: ["L", "N"] },
  { id: "Web_LSalad_TomatoCucumber", zh: "Web 生菜底番茄黄瓜沙拉", en: "Web Tomato Cucumber Salad (Lettuce Base)", ings: ["L", "T", "C"], icon: "gui/icons/recipes/ui_lettucetomatocucumbersalad_01" },
  { id: "Web_LSalad_TomatoCorn", zh: "Web 生菜底番茄玉米沙拉", en: "Web Tomato Corn Salad (Lettuce Base)", ings: ["L", "T", "N"] },
  { id: "Web_LSalad_CucumberCorn", zh: "Web 生菜底黄瓜玉米沙拉", en: "Web Cucumber Corn Salad (Lettuce Base)", ings: ["L", "C", "N"] },
  { id: "Web_LSalad_TomatoCucumberCorn", zh: "Web 生菜底番茄黄瓜玉米沙拉", en: "Web Tomato Cucumber Corn Salad (Lettuce Base)", ings: ["L", "T", "C", "N"] },
];

// ---------------------------------------------------------------- 缺省图标

/** PNG CRC32（标准表驱动）。 */
const CRC_TABLE = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();
function pngCrc(buf) {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = CRC_TABLE[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}
function pngChunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type, "ascii"), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(pngCrc(body));
  return Buffer.concat([len, body, crc]);
}

/** 统一缺省图标：128×120 RGBA，浅灰底 + 深灰边 + 中灰「？」。
 *  与官方 dlc11_ui_* 图标（128×120）同尺寸；纯程序生成、字节确定。 */
function makePlaceholderPng(w = 128, h = 120) {
  const bg = [228, 228, 228];
  const border = [199, 199, 199];
  const fg = [140, 140, 140];
  const GLYPH = [
    "01110",
    "10001",
    "00001",
    "00110",
    "01100",
    "00000",
    "01100",
  ];
  const s = 12;
  const gw = 5 * s;
  const gh = 7 * s;
  const ox = (w - gw) >> 1;
  const oy = (h - gh) >> 1;
  const stride = w * 4 + 1;
  const raw = Buffer.alloc(stride * h);
  for (let y = 0; y < h; y++) {
    raw[y * stride] = 0; // filter: none
    for (let x = 0; x < w; x++) {
      let c = bg;
      if (x === 0 || y === 0 || x === w - 1 || y === h - 1) c = border;
      const gx = x - ox;
      const gy = y - oy;
      if (gx >= 0 && gy >= 0 && gx < gw && gy < gh) {
        if (GLYPH[Math.floor(gy / s)][Math.floor(gx / s)] === "1") c = fg;
      }
      const i = y * stride + 1 + x * 4;
      raw[i] = c[0];
      raw[i + 1] = c[1];
      raw[i + 2] = c[2];
      raw[i + 3] = 255;
    }
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0);
  ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8; // bit depth
  ihdr[9] = 6; // RGBA
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    pngChunk("IHDR", ihdr),
    pngChunk("IDAT", zlib.deflateSync(raw, { level: 9 })),
    pngChunk("IEND", Buffer.alloc(0)),
  ]);
}
const PLACEHOLDER_PNG = makePlaceholderPng();

// ---------------------------------------------------------------- guid

const md5 = (s) => crypto.createHash("md5").update(s).digest("hex");
const folderGuid = (rel) => md5(`commonw3-folder:${rel}`);
const recipeGuid = (id) => md5(`commonw3-recipe:${id}`);
const iconGuid = (id) => md5(`commonw3-icon:${id}`);
const assetGuid = (rel) => md5(`commonw3-asset:${rel}`);
const compositeSaladGuid = md5("pseudo:dlc11_compositesalad");

// ---------------------------------------------------------------- 模板

const FOLDER_META = (guid, bundleName) => `fileFormatVersion: 2
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

/** TextureImporter（Sprite 单帧，128×120 RGBA）——镜像 commonW2 pasta 图标配置。 */
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

const yamlStr = (s) => s.replace(/[^\x20-\x7e]/g, (ch) => {
  const hex = ch.codePointAt(0).toString(16).toUpperCase().padStart(4, "0");
  return `\\u${hex}`;
});

/** CustomRecipeSO（type 1 = Composite，全部生切食材，无烹饪/搅拌）。 */
const RECIPE_ASSET = (r, uid) => `%YAML 1.1
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
  type: 1
  recipeName: ${r.id}
  uID: ${uid}
  score: ${r.ings.length * 20}
  platingStepSO: {fileID: 11400000, guid: ${PLATE_GUID}, type: 2}
  modelSO: {fileID: 11400000, guid: ${compositeSaladGuid}, type: 2}
  model: {fileID: 0}
  iconSO: {fileID: 0}
  icon: {fileID: 21300000, guid: ${iconGuid(r.id)}, type: 3}
  compositionSOs:
${r.ings.map((k) => `  - {fileID: 11400000, guid: ${ING[k].guid}, type: 2}`).join("\n")}
  optionalSOs: []
  cookingStepSO: {fileID: 0}
  cookingStepIconSO: {fileID: 0}
  cookingStepIcon: {fileID: 0}
  cookingProgress: 0
  mixingIconSO: {fileID: 0}
  mixingIcon: {fileID: 0}
  mixingProgress: 0
`;

/** DLC11 官方沙拉成品模型索引（bundle428，六道官方沙拉共用同一 compositesalad）。 */
const COMPOSITE_SALAD_SO = `%YAML 1.1
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
  m_Name: dlc11_compositesalad
  m_EditorClassIdentifier: 
  prefabName: dlc11_compositesalad
  bundleName: bundle428
  assetPath: Assets/downloadablecontent/dlc11/dlc_assets/prefabs/meals/dlc11_compositesalad.prefab
`;

const CONFIG_ASSET = `%YAML 1.1
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
  nextSequence: ${RECIPES.length + 1}
  categories:
  - id: salad
    zh: "${yamlStr("沙拉大全")}"
    en: Salad
  subcategories: []
  modelTransforms: []
`;

// ---------------------------------------------------------------- 输出计划

const W3 = "Assets/commonW3";
/** [相对 Assets/ 的目标路径, 内容, meta 模板]；meta 与目标同目录同名 + .meta。 */
function buildPlan() {
  const out = [];
  const add = (rel, content, metaFn, metaGuid) => {
    out.push({ rel, content, meta: metaFn(metaGuid ?? assetGuid(rel)) });
  };

  // 目录 meta（assetBundleName 仅根目录标记，子目录继承——镜像 commonW2 布局）
  out.push({ rel: `${W3}.meta`, content: FOLDER_META(folderGuid(W3), "commonW3"), meta: null });
  for (const d of [
    `${W3}/custom_recipes`,
    `${W3}/custom_recipes/salad`,
    `${W3}/custom_recipes/salad/icons`,
    `${W3}/pseudo_prefab_so`,
  ]) {
    out.push({ rel: `${d}.meta`, content: FOLDER_META(folderGuid(d), ""), meta: null });
  }

  // 成品模型 SO + 配置 + names.json
  add(`${W3}/pseudo_prefab_so/dlc11_compositesalad.asset`, COMPOSITE_SALAD_SO, ASSET_META, compositeSaladGuid);
  add(`${W3}/custom_recipes/CustomRecipeConfig.asset`, CONFIG_ASSET, ASSET_META);
  const names = {
    schemaVersion: 1,
    names: RECIPES.map((r) => ({ id: r.id, zh: r.zh, en: r.en })),
  };
  // 与 commonW2 一致：UTF-8 BOM + 2 空格缩进
  add(
    `${W3}/custom_recipes/names.json`,
    "\uFEFF" + JSON.stringify(names, null, 2) + "\n",
    TEXT_META
  );

  // 菜谱 + 图标（icon 字段 = 官方原图标；缺省占位仅写入不存在或仍为占位的文件，
  // 已被真实图覆盖的图标跳过，避免重跑覆盖用户补图）
  RECIPES.forEach((r, i) => {
    add(`${W3}/custom_recipes/salad/${r.id}.asset`, RECIPE_ASSET(r, UID_PREFIX * 1000 + i + 1), ASSET_META, recipeGuid(r.id));
    let content = PLACEHOLDER_PNG;
    let note = "缺省占位（待补图）";
    if (r.icon) {
      const iconSrc = path.join(DUMP_ASSETS, `${r.icon}.png`);
      if (!fs.existsSync(iconSrc)) throw new Error(`官方图标缺失: ${iconSrc}`);
      content = fs.readFileSync(iconSrc);
      note = `官方原图标 ← ${r.icon}.png`;
    }
    const iconAbs = path.join(repoRoot, `${W3}/custom_recipes/salad/icons/${r.id}.png`);
    if (!r.icon && fs.existsSync(iconAbs)) {
      const existing = fs.readFileSync(iconAbs);
      if (!existing.equals(PLACEHOLDER_PNG)) {
        out.push({
          rel: `${W3}/custom_recipes/salad/icons/${r.id}.png`,
          content: existing,
          meta: ICON_META(iconGuid(r.id)),
          binary: true,
          note: "已存在真实图标，保留不覆盖",
        });
        return;
      }
    }
    out.push({
      rel: `${W3}/custom_recipes/salad/icons/${r.id}.png`,
      content,
      meta: ICON_META(iconGuid(r.id)),
      binary: true,
      note,
    });
  });
  return out;
}

// ---------------------------------------------------------------- 主流程

const apply = process.argv.includes("--apply");
const plan = buildPlan();

const rootAbs = path.join(repoRoot, "Assets");
for (const item of plan) {
  const abs = path.join(repoRoot, item.rel);
  if (!apply) {
    console.log(`[dry] ${item.rel}${item.note ? `  (${item.note})` : ""}`);
    continue;
  }
  fs.mkdirSync(path.dirname(abs), { recursive: true });
  if (item.binary) fs.writeFileSync(abs, item.content);
  else fs.writeFileSync(abs, item.content, "utf8");
  if (item.meta) fs.writeFileSync(abs + ".meta", item.meta, "utf8");
  console.log(`写入 ${item.rel}${item.note ? `  (${item.note})` : ""}`);
}

if (!apply) {
  console.log(`\ndry-run：${plan.length} 个文件待生成。加 --apply 执行写入。`);
} else {
  console.log(`\n完成：${plan.length} 个文件已写入。请在 Unity 中刷新并 Build AssetBundles（commonW3）。`);
}
