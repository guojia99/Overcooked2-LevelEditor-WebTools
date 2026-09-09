#!/usr/bin/env node
/**
 * migrate-burger-recipes.mjs —— 把汉堡自定义菜谱收纳进 Assets/commonW2（Burger大全）。
 *
 * 来源：
 *  A. otherRepicesData/100 Burger/          全量（成品11 + 中间产物13 + 组装定义 OptionalBurger）
 *     - 丢弃 OptionalBurgerExpand.asset（与 OptionalBurger.asset 内容完全相同的副本）
 *     - 修复 ChickenPatty 的悬空食材引用 7b0da7f9… → common01 ChickenSO（f5e2ed0b…）
 *  B. otherRepicesData/custom_recipes 2/呆喵新菜谱/汉堡/
 *     - 成品汉堡：剔除夹心 >8 层（29全都有巨无霸 9层、36超级无敌巨无霸 16层）
 *     - 夹心定义（CustomRecipeOptionalBurgerSO）：保留 11素/26鸡肉/27牛肉/33菠萝牛肉 四个；
 *       丢弃 28（服务已剔除的 29、且与 27 重名 burger003）、37/50（服务已剔除的 36/49）
 *     - 丢弃整个「需要搅拌的汉堡」系列 47-59（仅被 49/50 引用，49/50 已剔除）
 *     - recipeName 去重修正：30鸡肉汉堡 LettuceChickenBurger→ChickenBurger；
 *       32菠萝牛肉汉堡 PineappleChickenBurger→PineappleMeatBurger
 *     - 中间产物 31-46 全量保留（含 35菠萝片，被 33夹心 引用）
 *
 * 复制策略：文件 + .meta 原样拷贝 → GUID 全部保留，内部引用零改写；
 * 引用闭包：从选中菜谱出发递归收集源目录内被引用的本地资产（图标 png / prefab / fbx / mat / 贴图）。
 *
 * 目标布局：
 *  Assets/commonW2/                       folder meta assetBundleName=commonW2
 *  └── custom_recipes/
 *      ├── CustomRecipeConfig.asset       uidPrefix=19990（沿用 100 Burger 号段）, nextSequence=113
 *      ├── names.json                     中英名映射
 *      └── burger/
 *          ├── *.asset                    菜谱（65 个）
 *          ├── models/                    模型类资产（100Burger Models 全部 + 呆喵 prefab/fbx/mat/贴图）
 *          └── icons/                     呆喵成品汉堡订单图标 png
 *
 * 用法：node layout-editor/scripts/migrate-burger-recipes.mjs [--force]
 * 幂等：目标已存在且非 --force 时直接报错退出。
 */
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";

const ROOT = path.resolve(import.meta.dirname, "../..");
const SRC_100 = path.join(ROOT, "otherRepicesData/100 Burger");
const SRC_DM = path.join(ROOT, "otherRepicesData/custom_recipes 2/呆喵新菜谱/汉堡");
const SRC_DM_MIX = path.join(SRC_DM, "需要搅拌的汉堡");
const SRC_CR2 = path.join(ROOT, "otherRepicesData/custom_recipes 2");
const DST = path.join(ROOT, "Assets/commonW2/custom_recipes");
const DST_BURGER = path.join(DST, "burger");
const DST_MODELS = path.join(DST_BURGER, "models");
const DST_ICONS = path.join(DST_BURGER, "icons");

const SCRIPT_GUIDS = new Set([
  "83fb008bcc8e793429b02c178c430815", // CustomRecipeSO
  "e7bb274eb901e2042b1c49a42ecec9df", // CustomRecipeOptionalBurgerSO
  "60297950c88d0d646ac0eca5dc831262", // CustomRecipeOptionalPizzaSO
  "0cff7c13895ab9e47a5e02d4619cc3b9", // PseudoPrefabSO
  "753d9e70603f6a140b05f30f176ec2dd", // PseudoPrefabSORecipe
]);

/** uID 统一号段：58321xxx（8 位，58321001 起按 recipeName 字典序递增；
 *  覆盖源数据中的 0（100 Burger 中间产物/组装定义）与旧号段（呆喵 1000xxx / 成品 19990xxx）。 */
const UID_PREFIX = 58321;

/** ChickenPatty 悬空引用修复：源数据的生鸡肉 guid 在全仓库无对应资产 → common01 ChickenSO */
const GUID_REMAP = new Map([
  ["7b0da7f99b2b118f3127794b84effd06", "f5e2ed0b5f84bfb4da375a513302084e"],
]);

/** 呆喵 recipeName 去重修正（文件名 → 新 recipeName）：
 *  30/32 修正张冠李戴的英文名；
 *  34/39/40/44 与 common01 既有中间产物（FriedMeat/FriedOnion/FriedMushroom/FriedPotato）撞名；
 *  35 原名为中文转义，统一改为 ASCII id（recipeName 仅作显示名/names.json id，运行时按 guid 引用）；
 *  11/26/27/33 组装定义原名 burger001~004（无语义且 27/28 重名），改为语义化 id。 */
const RECIPE_NAME_FIX = new Map([
  ["30鸡肉汉堡.asset", "ChickenBurger"],
  ["32菠萝牛肉汉堡.asset", "PineappleMeatBurger"],
  ["34煎牛肉.asset", "PanfriedBeef"],
  ["39煎洋葱.asset", "PanfriedOnion"],
  ["40煎蘑菇.asset", "PanfriedMushroom"],
  ["44炸土豆饼.asset", "FriedPotatoCake"],
  ["35菠萝片.asset", "PineappleSlice"],
  ["11素汉堡夹心.asset", "VeggieBurgerAssembly"],
  ["26鸡肉汉堡夹心.asset", "ChickenBurgerAssembly"],
  ["27牛肉汉堡夹心.asset", "MeatBurgerAssembly"],
  ["33菠萝牛肉堡夹心.asset", "PineappleMeatBurgerAssembly"],
]);

/** 非菜谱资产（图标/模型/贴图/材质）重命名：源文件 basename → 目标 basename。
 *  全部 ASCII 化（中文/空格/序号前缀一律移除）；引用均按 guid，重命名安全。 */
const FILE_RENAME = new Map(Object.entries({
  // ---- 呆喵成品/夹心订单图标（按所属菜谱 recipeName 命名） ----
  "1生菜汉堡ui.png": "LettuceBurger.png",
  "2生菜西红柿汉堡ui.png": "LettuceTomatoBurger.png",
  "3双层菠萝汉堡ui.png": "DoublePineappleBurger.png",
  "4黄瓜汉堡ui.png": "CucumberBurger.png",
  "5西红柿黄瓜汉堡ui.png": "TomatoCucumberBurger.png",
  "6生菜黄瓜汉堡ui.png": "LettuceCucumberBurger.png",
  "7菠萝汉堡ui.png": "PineappleBurger.png",
  "8全都有汉堡.png": "LettuceTomatoCucumberPineappleBurger.png",
  "9生菜西红柿黄瓜汉堡ui.png": "LettuceTomatoCucumberBurger.png",
  "10西红柿汉堡ui.png": "TomatoBurger.png",
  "12生菜鸡肉汉堡.png": "LettuceChickenBurger.png",
  "13生菜西红柿鸡肉汉堡.png": "LettuceTomatoChickenBurger.png",
  "14黄瓜鸡肉汉堡.png": "CucumberChickenBurger.png",
  "15西红柿黄瓜鸡肉汉堡.png": "TomatoCucumberChickenBurger.png",
  "16生菜黄瓜鸡肉汉堡.png": "LettuceCucumberChickenBurger.png",
  "17菠萝鸡肉汉堡.png": "PineappleChickenBurger.png",
  "18汉堡皇巨无霸堡.png": "SupremeBurger.png", // 18/25 两个成品共用
  "19芝士鸡肉汉堡.png": "CheeseChickenBurger.png",
  "20生菜芝士鸡肉汉堡.png": "LettuceCheeseChickenBurger.png",
  "21生菜西红柿黄瓜鸡肉汉堡.png": "LettuceTomatoCucumberChickenBurger.png",
  "22生菜黄瓜牛肉汉堡.png": "LettuceCucumberMeatBurger.png",
  "23黄瓜牛肉汉堡.png": "CucumberMeatBurger.png",
  "24西红柿黄瓜牛肉汉堡.png": "TomatoCucumberMeatBurger.png",
  "30鸡肉汉堡.png": "ChickenBurger.png",
  "33菠萝牛肉汉堡.png": "PineappleMeatBurger.png",
  // ---- 呆喵模型 prefab（中间产物/夹心层模型） ----
  "菠萝片.prefab": "PineappleSlice.prefab",
  "黄瓜片.prefab": "CucumberSlice.prefab",
  "西红柿片.prefab": "TomatoSlice.prefab",
  "煎鸡肉新.prefab": "FriedChickenPatty.prefab",
  "煎蘑菇.prefab": "PanfriedMushroom.prefab",
  "煎牛肉饼.prefab": "FriedBeefPatty.prefab",
  "煎牛肉新.prefab": "FriedBeefNew.prefab",
  "煎香肠.prefab": "FriedSausage.prefab",
  "煎洋葱.prefab": "PanfriedOnion.prefab",
  "煎芝士片.prefab": "FriedCheese.prefab",
  "炸土豆饼.prefab": "FriedPotatoCake.prefab",
  "炸虾饼.prefab": "FriedShrimpCake.prefab",
  "炸鱼饼.prefab": "FriedFishCake.prefab",
  "炸玉米饼.prefab": "FriedCornCake.prefab",
  // ---- 呆喵白模 fbx ----
  "肠.fbx": "Sausage.fbx",
  "汉堡夹心菠萝片无材质.fbx": "BurgerPineappleSlice.fbx",
  "汉堡夹心黄瓜片无材质.fbx": "BurgerCucumberSlice.fbx",
  "汉堡夹心西红柿片无材质.fbx": "BurgerTomatoSlice.fbx",
  "煎鸡肉.fbx": "FriedChicken.fbx",
  "煎鸡肉新.fbx": "FriedChickenNew.fbx",
  "煎蘑菇.fbx": "FriedMushroom.fbx",
  "煎洋葱.fbx": "FriedOnion.fbx",
  "煎芝士片.fbx": "FriedCheese.fbx",
  "炸土豆饼.fbx": "FriedPotatoCake.fbx",
  "炸鱼饼.fbx": "FriedFishCake.fbx",
  // ---- 呆喵材质/贴图 ----
  "菠萝片.mat": "PineappleSlice.mat", "菠萝片.png": "PineappleSlice.png",
  "黄瓜片.mat": "CucumberSlice.mat", "黄瓜片.png": "CucumberSlice.png",
  "西红柿片.mat": "TomatoSlice.mat", "西红柿片.png": "TomatoSlice.png",
  "煎鸡肉.mat": "FriedChicken.mat", "煎鸡肉.png": "FriedChicken.png",
  "煎鸡肉 1.mat": "FriedChicken_1.mat", "煎鸡肉 1.png": "FriedChicken_1.png",
  "煎蘑菇2.mat": "FriedMushroom2.mat", "煎蘑菇1.png": "FriedMushroom1.png",
  "煎牛肉.mat": "FriedBeef.mat", "煎牛肉.png": "FriedBeef.png",
  "煎洋葱 1.mat": "FriedOnion_1.mat", "煎洋葱.png": "FriedOnion.png",
  "煎芝士片.mat": "FriedCheese.mat", "煎芝士片.png": "FriedCheese.png",
  "炸土豆饼.mat": "FriedPotatoCake.mat", "炸土豆饼.png": "FriedPotatoCake.png",
  "炸虾饼 3.mat": "FriedShrimpCake_3.mat", "炸虾饼.png": "FriedShrimpCake.png",
  "炸鱼饼 3.mat": "FriedFishCake_3.mat", "炸鱼饼.png": "FriedFishCake.png",
  "炸玉米饼 1.mat": "FriedCornCake_1.mat", "炸玉米饼.png": "FriedCornCake.png",
  "肠2.mat": "Sausage2.mat", "肠2.png": "Sausage2.png",
}));

/** 解析 YAML 字符串标量（去引号 + \uXXXX 解码） */
function decodeYamlStr(s) {
  s = (s ?? "").trim();
  if (s.startsWith('"') && s.endsWith('"'))
    s = s.slice(1, -1).replace(/\\u([0-9A-Fa-f]{4})/g, (_, h) => String.fromCharCode(parseInt(h, 16)));
  return s;
}

/** 呆喵：剔除的成品/夹心/搅拌系列（夹心>8层 + 仅服务已剔除成品的资产） */
const DM_EXCLUDE = new Set([
  "29汉堡皇全都有巨无霸堡.asset", // 9层
  "36超级无敌巨无霸堡.asset", // 16层
  "28汉堡皇全部汉堡夹心.asset", // 服务29，且与27重名 burger003
  "37超级无敌巨无霸堡夹心.asset", // 服务36
]);

/** 100 Burger 中文名（names.json zh） */
const ZH_100 = {
  BaconSausage: "培根煎香肠",
  BreakfastCheeseBurger: "早餐芝士汉堡",
  BreakfastLettuceBurger: "早餐生菜汉堡",
  BreakfastMeatBurger: "早餐牛肉汉堡",
  BreakfastOnionBurger: "早餐洋葱汉堡",
  ChickenLettuceCheeseTomatoBurger: "鸡肉生菜芝士番茄汉堡",
  ChickenMeatPineappleCucumberBurger: "鸡肉牛肉菠萝黄瓜汉堡",
  ChickenMushroomTwoCheeseBurger: "鸡肉蘑菇双芝汉堡",
  ChickenPatty: "煎鸡肉饼",
  DoublePrawnBurger: "双层虾堡",
  EggSausage: "煎蛋香肠",
  FriedMushroom: "煎蘑菇",
  FriedOnion: "煎洋葱",
  FriedPrawn: "炸虾",
  LettuceCucumberPrawnBurger: "生菜黄瓜虾堡",
  LettuceTomatoPrawnBurger: "生菜番茄虾堡",
  MeatCornBurger: "牛肉玉米汉堡",
  MeatMushroomBurger: "牛肉蘑菇汉堡",
  MeatOnionBurger: "牛肉洋葱汉堡",
  MixedMeatEgg: "搅拌牛肉蛋",
  MixedMeatMushroom: "搅拌牛肉蘑菇",
  MixedMeatOnion: "搅拌牛肉洋葱",
  OptionalBurger: "汉堡组装定义",
  PanfriedMeatEgg: "香煎牛肉蛋",
  PanfriedMeatMushroom: "香煎牛肉蘑菇",
  PanfriedMeatOnion: "香煎牛肉洋葱",
};

// ---------------------------------------------------------------- 工具

const guidRe = /^guid: ([0-9a-f]{32})/m;
function readGuid(metaPath) {
  if (!fs.existsSync(metaPath)) return null;
  return guidRe.exec(fs.readFileSync(metaPath, "utf8"))?.[1] ?? null;
}
function newGuid() {
  return crypto.randomBytes(16).toString("hex");
}
/** 提取文本中全部 32 位 hex guid（含脚本 guid，调用方过滤） */
function extractGuids(text) {
  const out = new Set();
  for (const m of text.matchAll(/guid: ([0-9a-f]{32})/g)) out.add(m[1]);
  return out;
}
function folderMeta(guid, bundleName = "") {
  return `fileFormatVersion: 2\nguid: ${guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: ${bundleName}\n  assetBundleVariant: \n`;
}
function textScriptMeta(guid) {
  return `fileFormatVersion: 2\nguid: ${guid}\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
}
function nativeAssetMeta(guid) {
  return `fileFormatVersion: 2\nguid: ${guid}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
}
/** YAML 标量转义（含中文时与 Unity 一致用 "…" 包裹） */
function yamlStr(s) {
  // eslint-disable-next-line no-control-regex
  return /^[\x20-\x7e]*$/.test(s) ? s : `"${[...s].map((c) => (c.charCodeAt(0) > 127 ? "\\u" + c.charCodeAt(0).toString(16).toUpperCase().padStart(4, "0") : c)).join("")}"`;
}

// ---------------------------------------------------------------- 源目录索引

/** 建立「源本地 guid → 源文件路径」索引（100 Burger + custom_recipes 2 全部） */
function indexDir(root, base = root, map = new Map()) {
  for (const e of fs.readdirSync(root, { withFileTypes: true })) {
    const p = path.join(root, e.name);
    if (e.isDirectory()) indexDir(p, base, map);
    else if (e.name.endsWith(".meta")) {
      const g = readGuid(p);
      if (g) map.set(g, { file: p.slice(0, -5), rel: path.relative(base, p).slice(0, -5) });
    }
  }
  return map;
}
const srcIndex = new Map([...indexDir(SRC_100), ...indexDir(SRC_CR2)]);

// Assets 下已有 guid（用于报告区分「外部已存在」与「悬空」）
const assetsIndex = new Map(JSON.parse(fs.readFileSync("/tmp/assets_guid_index.json", "utf8")));

// ---------------------------------------------------------------- 选择要迁移的菜谱

/** 待复制菜谱：{ srcFile, srcDir('100'|'dm'), dstDir } */
const picked = [];
const dropped = [];

// A. 100 Burger 根目录全量（除 OptionalBurgerExpand）
for (const f of fs.readdirSync(SRC_100).sort()) {
  if (!f.endsWith(".asset")) continue;
  if (f === "OptionalBurgerExpand.asset") {
    dropped.push({ file: "100 Burger/" + f, reason: "与 OptionalBurger.asset 内容完全相同的副本" });
    continue;
  }
  picked.push({ srcFile: path.join(SRC_100, f), dstDir: DST_BURGER, origin: "100Burger" });
}

// B. 呆喵汉堡（不含需要搅拌的子目录）
for (const f of fs.readdirSync(SRC_DM).sort((a, b) => a.localeCompare(b, "zh"))) {
  if (!f.endsWith(".asset")) continue;
  if (DM_EXCLUDE.has(f)) {
    dropped.push({ file: "呆喵/" + f, reason: "夹心>8层 或 仅服务已剔除成品" });
    continue;
  }
  picked.push({ srcFile: path.join(SRC_DM, f), dstDir: DST_BURGER, origin: "daimiao" });
}
for (const f of fs.readdirSync(SRC_DM_MIX)) {
  if (f.endsWith(".asset"))
    dropped.push({ file: "呆喵/需要搅拌的汉堡/" + f, reason: "搅拌系列仅服务已剔除的 49/50，整体不迁移" });
}

// ---------------------------------------------------------------- 引用闭包

/** Unity 内置资源 guid（built-in shader 等），不参与解析也不报警 */
const isBuiltinGuid = (g) => /^0{10,}[0-9a-f]0{3,}$/.test(g);

/** 从菜谱出发的本地引用闭包（含菜谱自身 guid） */
const closure = new Map(); // guid -> srcIndex entry
const queue = [];
const unresolved = new Map(); // guid -> Set<引用者>
for (const p of picked) {
  const g = readGuid(p.srcFile + ".meta");
  if (g && srcIndex.has(g)) {
    closure.set(g, srcIndex.get(g));
    queue.push(g);
  }
}
// 100 Burger/Models 全量收纳（含未被菜谱引用的备用图标/模型，供汉堡工作台选用）
for (const f of fs.readdirSync(path.join(SRC_100, "Models"))) {
  if (f.endsWith(".meta")) continue;
  const g = readGuid(path.join(SRC_100, "Models", f + ".meta"));
  if (g && srcIndex.has(g) && !closure.has(g)) {
    closure.set(g, srcIndex.get(g));
    queue.push(g);
  }
}
while (queue.length) {
  const g = queue.shift();
  const entry = srcIndex.get(g);
  if (!entry || !fs.existsSync(entry.file)) continue;
  const stat = fs.statSync(entry.file);
  if (stat.size > 8 * 1024 * 1024) continue; // 大二进制不扫描（fbx 文本也足够小，不会到这）
  let text;
  try {
    text = fs.readFileSync(entry.file, "utf8");
  } catch {
    continue; // 二进制
  }
  if (text.includes("\u0000")) continue; // binary guard
  for (const ref of extractGuids(text)) {
    if (SCRIPT_GUIDS.has(ref) || closure.has(ref) || isBuiltinGuid(ref)) continue;
    const remap = GUID_REMAP.get(ref) ?? ref;
    if (srcIndex.has(remap)) {
      closure.set(remap, srcIndex.get(remap));
      queue.push(remap);
    } else if (!assetsIndex.has(ref)) {
      if (!unresolved.has(ref)) unresolved.set(ref, new Set());
      unresolved.get(ref).add(path.basename(entry.file));
    }
  }
}

/** 闭包里的非菜谱资产 → 目标子目录 */
function classifyExtra(entry) {
  const ext = path.extname(entry.file).toLowerCase();
  if (ext === ".png") {
    // 图标（文件名含 ui 或为汉堡成品图标）→ icons/；模型贴图 → models/
    return /汉堡|ui_/i.test(path.basename(entry.file)) && !entry.rel.startsWith("材质")
      ? DST_ICONS
      : DST_MODELS;
  }
  return DST_MODELS; // .prefab / .fbx / .mat / 其他 .asset（PseudoPrefabSO）
}

// ---------------------------------------------------------------- 执行

const force = process.argv.includes("--force");
if (fs.existsSync(DST_BURGER) && !force) {
  console.error(`目标已存在：${DST_BURGER}\n加 --force 覆盖。`);
  process.exit(1);
}
fs.rmSync(path.join(ROOT, "Assets/commonW2"), { recursive: true, force: true });
fs.mkdirSync(DST_MODELS, { recursive: true });
fs.mkdirSync(DST_ICONS, { recursive: true });

const report = { recipes: [], extras: [], dropped, remapped: [...GUID_REMAP.keys()], unresolved: [] };

/** 复制文件 + meta；recipes 应用 GUID_REMAP 与 recipeName 修正。
 *  dstName：目标文件名（ASCII 化）；省略时取 FILE_RENAME 映射或原名。 */
function copyAsset(srcFile, dstDir, { patch = false, dstName = null, uid = null } = {}) {
  const base = path.basename(srcFile);
  const outName = dstName ?? FILE_RENAME.get(base) ?? base;
  const dstFile = path.join(dstDir, outName);
  const isText = [".asset", ".prefab", ".mat", ".json", ".controller"].includes(path.extname(base).toLowerCase());
  if (isText) {
    let text = fs.readFileSync(srcFile, "utf8");
    if (patch) {
      for (const [from, to] of GUID_REMAP) text = text.split(from).join(to);
      const fix = RECIPE_NAME_FIX.get(base);
      if (fix) text = text.replace(/^(  recipeName: ).*$/m, `$1${fix}`);
      if (uid != null) text = text.replace(/^(  uID: )\d+/m, `$1${uid}`);
      // m_Name 与新文件名保持一致（Unity 惯例；原为中文转义或带序号前缀）
      const newId = outName.replace(/\.asset$/, "");
      text = text.replace(/^  m_Name: .*$/m, `  m_Name: ${newId}`);
    }
    fs.writeFileSync(dstFile, text);
  } else {
    fs.copyFileSync(srcFile, dstFile);
  }
  fs.copyFileSync(srcFile + ".meta", dstFile + ".meta");
  return dstFile;
}

// 菜谱（打补丁；文件名统一改为 recipeName + .asset，去掉中文/序号前缀；
// uID 统一 58321 号段、按 recipeName 字典序 58321001 起递增）
const nameEntries = []; // names.json
const planned = picked.map((p) => {
  const base = path.basename(p.srcFile, ".asset");
  const srcText = fs.readFileSync(p.srcFile, "utf8");
  const rawName = decodeYamlStr(/^  recipeName: (.*)$/m.exec(srcText)?.[1]) || base;
  return { ...p, base, recipeName: RECIPE_NAME_FIX.get(base + ".asset") ?? rawName };
});
planned.sort((a, b) => a.recipeName < b.recipeName ? -1 : a.recipeName > b.recipeName ? 1 : 0);
planned.forEach((p, i) => {
  const uid = UID_PREFIX * 1000 + i + 1;
  const dstFile = copyAsset(p.srcFile, p.dstDir, { patch: true, dstName: p.recipeName + ".asset", uid });
  const text = fs.readFileSync(dstFile, "utf8");
  const score = parseInt(/^  score: (.*)$/m.exec(text)?.[1] ?? "0", 10);
  const script = /m_Script: \{fileID: 11500000, guid: ([0-9a-f]{8})/.exec(text)?.[1];
  const kind = script === "e7bb274e" ? "assembly" : score > 0 ? "product" : "intermediate";
  const zh =
    p.origin === "100Burger"
      ? ZH_100[p.recipeName] ?? p.base
      : p.base.replace(/^\d+/, "");
  const en = p.origin === "100Burger" ? p.recipeName.replace(/([a-z])([A-Z])/g, "$1 $2") : p.recipeName;
  nameEntries.push({ id: p.recipeName, zh, en });
  report.recipes.push({ file: p.recipeName + ".asset", origin: p.origin, kind, recipeName: p.recipeName, zh, uID: uid });
});

// 闭包额外资产（不打补丁——prefab/mat 里的 guid 必须原样保留；文件名走 FILE_RENAME ASCII 化）
for (const [g, entry] of [...closure.entries()].sort((a, b) => a[1].rel.localeCompare(b[1].rel, "zh"))) {
  // 菜谱本体已在 picked 里复制过
  if (picked.some((p) => p.srcFile === entry.file)) continue;
  // 被剔除的呆喵资产即使被引用也不应出现（防御；正常不会发生）
  if (DM_EXCLUDE.has(path.basename(entry.file))) {
    report.unresolved.push({ guid: g, file: entry.rel, note: "被剔除资产却被保留菜谱引用！" });
    continue;
  }
  const dstDir = classifyExtra(entry);
  const dstFile = copyAsset(entry.file, dstDir);
  report.extras.push({ file: entry.rel, to: path.relative(DST_BURGER, dstFile) });
}

// 悬空引用报告
for (const [g, users] of unresolved)
  report.unresolved.push({ guid: g, users: [...users], note: GUID_REMAP.has(g) ? "已重映射" : "未解析（源数据缺失）" });

// ---------------------------------------------------------------- 目录 meta / 配置

fs.writeFileSync(path.join(ROOT, "Assets/commonW2.meta"), folderMeta(newGuid(), "commonW2"));
fs.writeFileSync(DST + ".meta", folderMeta(newGuid()));
fs.writeFileSync(DST_BURGER + ".meta", folderMeta(newGuid()));
fs.writeFileSync(DST_MODELS + ".meta", folderMeta(newGuid()));
fs.writeFileSync(DST_ICONS + ".meta", folderMeta(newGuid()));

const configYaml = `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 5edb07060a0c6c140aa72ffb8fd3f021, type: 3}
  m_Name: CustomRecipeConfig
  m_EditorClassIdentifier: 
  uidPrefix: ${UID_PREFIX}
  nextSequence: ${planned.length + 1}
  categories:
  - id: burger
    zh: ${yamlStr("Burger大全")}
    en: Burger
  modelTransforms: []
`;
fs.writeFileSync(path.join(DST, "CustomRecipeConfig.asset"), configYaml);
fs.writeFileSync(path.join(DST, "CustomRecipeConfig.asset.meta"), nativeAssetMeta(newGuid()));

nameEntries.sort((a, b) => a.id.localeCompare(b.id));
fs.writeFileSync(
  path.join(DST, "names.json"),
  JSON.stringify({ schemaVersion: 1, names: nameEntries }, null, 4) + "\n"
);
fs.writeFileSync(path.join(DST, "names.json.meta"), textScriptMeta(newGuid()));

// ---------------------------------------------------------------- 报告

const by = (k) => report.recipes.filter((r) => r.kind === k);
console.log(`迁移完成 → ${path.relative(ROOT, DST)}`);
console.log(`  菜谱：成品 ${by("product").length} / 中间产物 ${by("intermediate").length} / 组装定义 ${by("assembly").length}（共 ${report.recipes.length}）`);
console.log(`  附加资产：${report.extras.length}（图标/模型/贴图，含引用闭包）`);
console.log(`  丢弃：${report.dropped.length}`);
for (const d of report.dropped) console.log(`    - ${d.file}：${d.reason}`);
if (report.unresolved.length) {
  console.log(`  悬空引用：${report.unresolved.length}`);
  for (const u of report.unresolved) console.log(`    ! ${u.guid} ${u.note} ${u.users ? "<- " + u.users.join(",") : u.file}`);
}
fs.writeFileSync(
  path.join(ROOT, "layout-editor/scripts/migrate-burger-recipes.report.json"),
  JSON.stringify(report, null, 2)
);
console.log("报告：layout-editor/scripts/migrate-burger-recipes.report.json");
