#!/usr/bin/env node
/**
 * 生成 commonW3「冰沙 / 冰淇淋」扩展（模型与图标待定，仅落盘 .asset + names.json）。
 *
 * 冰沙（milk_slush）：牛奶 + 冰 + 2× 同款 Web 果汁水果（10 味）
 * 冰淇淋（ice_cream）：2× 冰 + 牛奶 + 1 食材（10 果味 + 香草 + 巧克力，共 12 道）
 *
 * 用法：
 *   node gen-commonw3-dairy-drinks.mjs           # dry-run
 *   node gen-commonw3-dairy-drinks.mjs --apply   # 落盘并清理旧 models/icons
 */
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const W3 = "Assets/commonW3";
const CONFIG_PATH = path.join(repoRoot, `${W3}/custom_recipes/CustomRecipeConfig.asset`);
const NAMES_PATH = path.join(repoRoot, `${W3}/custom_recipes/names.json`);
const LAYOUT_RECIPE_ICONS = path.join(repoRoot, "layout-editor/web/public/icons/recipes");
const LAYOUT_DIST_RECIPE_ICONS = path.join(repoRoot, "layout-editor/web/dist/icons/recipes");

const SCRIPT_CUSTOM_RECIPE = "83fb008bcc8e793429b02c178c430815";
const GLASS_PLATING_GUID = "9f2781f346fbd6d42a008e004f255d92";
const BLENDER_ICON_GUID = "7a77fe98553178640a4b8a48d10057bc";

const ICE = "ef868e413995bdf5ea3c0058f6dd5db0";
/** DLC11_Milk — 冰淇淋与冰沙共用 */
const MILK = "af1fe45a85aaf6448f00b5bdd59a7002";
const VANILLA = "73c28675ea0968be18f65b2d948e3227";
const CHOCOLATE = "d2a417e941d3a3f4181cfadf039d720d";

const SCORE = 100;
const NEXT_SEQUENCE = 58322086;

/** 与 gen-commonw3-smoothies / gen-commonw3-mixed-smoothies 对齐的 Web 果汁水果 */
const FRUITS = [
  { key: "Grape", zh: "葡萄", en: "Grape", guid: "067651d383836c606d0d325fe3b3dbd7" },
  { key: "Orange", zh: "橙子", en: "Orange", guid: "c0721b5197a5193a62cac90b2e010560" },
  { key: "Peach", zh: "桃子", en: "Peach", guid: "c4a38be9154f4a2dfba533c6fdb72aa9" },
  { key: "Blueberry", zh: "蓝莓", en: "Blueberry", guid: "a192356a1fd01504eadf0d90ffa2de62" },
  { key: "Blackberry", zh: "黑莓", en: "Blackberry", guid: "1e87aab4d91d5e2b460a8de8fe55b737" },
  { key: "Raspberry", zh: "树莓", en: "Raspberry", guid: "2c4b11eca006b60f3ae5a95e5cf4c609" },
  { key: "Banana", zh: "香蕉", en: "Banana", guid: "b6f2aa157beed2140a12c5cc8a66f8c1" },
  { key: "Melon", zh: "西瓜", en: "Melon", guid: "f57c067108d7dd543873dd5df1414aea" },
  { key: "Pineapple", zh: "菠萝", en: "Pineapple", guid: "6207742210e05564daf15e9c5d4c727b" },
  { key: "Strawberry", zh: "草莓", en: "Strawberry", guid: "e1e6338f70e25804d8a1081909bcb066" },
];

const MILK_SLUSH_UIDS = {
  Pineapple: 58322068,
  Strawberry: 58322069,
  Blueberry: 58322070,
  Banana: 58322071,
  Melon: 58322072,
  Grape: 58322073,
  Orange: 58322074,
  Peach: 58322075,
  Blackberry: 58322076,
  Raspberry: 58322077,
};

const ICE_CREAM_UIDS = {
  Vanilla: 58322054,
  Chocolate: 58322055,
  Strawberry: 58322056,
  Blackberry: 58322057,
  Grape: 58322078,
  Orange: 58322079,
  Peach: 58322080,
  Blueberry: 58322081,
  Raspberry: 58322082,
  Banana: 58322083,
  Melon: 58322084,
  Pineapple: 58322085,
};

const md5 = (s) => crypto.createHash("md5").update(s).digest("hex");
const recipeGuid = (id) => md5(`commonw3-recipe:${id}`);
const folderGuid = (rel) => md5(`commonw3-folder:${rel}`);

const ASSET_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
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

const MILK_SLUSH_RECIPES = FRUITS.map((f) => ({
  id: `Web_MilkSlush_${f.key}`,
  category: "milk_slush",
  zh: `Web ${f.zh}牛奶冰沙`,
  en: `Web ${f.en} Milk Slush`,
  uid: MILK_SLUSH_UIDS[f.key],
  composition: [MILK, ICE, f.guid, f.guid],
}));

const ICE_CREAM_RECIPES = [
  {
    id: "Web_IceCream_Vanilla",
    category: "ice_cream",
    zh: "Web 香草冰淇淋",
    en: "Web Vanilla Ice Cream",
    uid: ICE_CREAM_UIDS.Vanilla,
    composition: [ICE, ICE, MILK, VANILLA],
  },
  {
    id: "Web_IceCream_Chocolate",
    category: "ice_cream",
    zh: "Web 巧克力冰淇淋",
    en: "Web Chocolate Ice Cream",
    uid: ICE_CREAM_UIDS.Chocolate,
    composition: [ICE, ICE, MILK, CHOCOLATE],
  },
  ...FRUITS.map((f) => ({
    id: `Web_IceCream_${f.key}`,
    category: "ice_cream",
    zh: `Web ${f.zh}冰淇淋`,
    en: `Web ${f.en} Ice Cream`,
    uid: ICE_CREAM_UIDS[f.key],
    composition: [ICE, ICE, MILK, f.guid],
  })),
];

const ALL_RECIPES = [...MILK_SLUSH_RECIPES, ...ICE_CREAM_RECIPES];

function buildRecipeYaml(r) {
  const compLines = r.composition
    .map((g) => `  - {fileID: 11400000, guid: ${g}, type: 2}`)
    .join("\n");
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
  type: 3
  recipeName: ${r.id}
  uID: ${r.uid}
  score: ${SCORE}
  platingStepSO: {fileID: 11400000, guid: ${GLASS_PLATING_GUID}, type: 2}
  modelSO: {fileID: 0}
  model: {fileID: 0}
  iconSO: {fileID: 0}
  icon: {fileID: 0}
  compositionSOs:
${compLines}
  optionalSOs: []
  cookingStepSO: {fileID: 0}
  cookingStepIconSO: {fileID: 0}
  cookingStepIcon: {fileID: 0}
  cookingProgress: 0
  mixingIconSO: {fileID: 11400000, guid: ${BLENDER_ICON_GUID}, type: 2}
  mixingIcon: {fileID: 0}
  mixingProgress: 1
`;
}

function updateNamesJson() {
  const existing = JSON.parse(fs.readFileSync(NAMES_PATH, "utf8").replace(/^\uFEFF/, ""));
  const dairyIds = new Set(ALL_RECIPES.map((r) => r.id));
  const names = (existing.names ?? []).filter((n) => !dairyIds.has(n.id) && !n.id.startsWith("Web_MilkSlush_") && !n.id.startsWith("Web_IceCream_"));
  for (const r of ALL_RECIPES) names.push({ id: r.id, zh: r.zh, en: r.en });
  names.sort((a, b) => a.id.localeCompare(b.id));
  return "\uFEFF" + JSON.stringify({ schemaVersion: 1, names }, null, 2) + "\n";
}

function updateConfigNextSequence() {
  const raw = fs.readFileSync(CONFIG_PATH, "utf8");
  const updated = raw.replace(/^  nextSequence: \d+/m, `  nextSequence: ${NEXT_SEQUENCE}`);
  if (updated === raw) throw new Error("CustomRecipeConfig.asset 未找到 nextSequence 行");
  return updated;
}

function rmrf(abs) {
  if (!fs.existsSync(abs)) return;
  for (const ent of fs.readdirSync(abs, { withFileTypes: true })) {
    const p = path.join(abs, ent.name);
    if (ent.isDirectory()) rmrf(p);
    else fs.unlinkSync(p);
  }
  fs.rmdirSync(abs);
}

function cleanCategoryAssets(category) {
  const base = path.join(repoRoot, `${W3}/custom_recipes/${category}`);
  if (!fs.existsSync(base)) return;

  for (const ent of fs.readdirSync(base, { withFileTypes: true })) {
    const p = path.join(base, ent.name);
    if (ent.isDirectory()) {
      if (ent.name === "icons" || ent.name === "models") rmrf(p);
      continue;
    }
    if (ent.name.endsWith(".asset") || ent.name.endsWith(".asset.meta")) fs.unlinkSync(p);
  }
}

function ensureCategoryFolders() {
  for (const category of ["milk_slush", "ice_cream"]) {
    const base = `${W3}/custom_recipes/${category}`;
    const abs = path.join(repoRoot, base);
    fs.mkdirSync(path.join(abs, "icons"), { recursive: true });
    fs.mkdirSync(path.join(abs, "models"), { recursive: true });
    const writes = [
      [`${base}.meta`, FOLDER_META(folderGuid(base))],
      [`${base}/icons.meta`, FOLDER_META(folderGuid(`${base}/icons`))],
      [`${base}/models.meta`, FOLDER_META(folderGuid(`${base}/models`))],
    ];
    for (const [rel, meta] of writes) {
      const p = path.join(repoRoot, rel);
      if (!fs.existsSync(p)) fs.writeFileSync(p, meta, "utf8");
    }
  }
}

function removeLayoutIcons() {
  for (const dir of [LAYOUT_RECIPE_ICONS, LAYOUT_DIST_RECIPE_ICONS]) {
    if (!fs.existsSync(dir)) continue;
    for (const ent of fs.readdirSync(dir)) {
      if (ent.startsWith("Web_MilkSlush_") || ent.startsWith("Web_IceCream_")) {
        fs.unlinkSync(path.join(dir, ent));
      }
    }
  }
}

function buildPlan() {
  const plan = [];
  for (const r of ALL_RECIPES) {
    const assetRel = `${W3}/custom_recipes/${r.category}/${r.id}.asset`;
    plan.push({
      rel: assetRel,
      content: buildRecipeYaml(r),
      meta: ASSET_META(recipeGuid(r.id)),
      note: `${r.zh} uid=${r.uid}`,
    });
  }
  plan.push({ rel: `${W3}/custom_recipes/names.json`, content: updateNamesJson(), meta: null });
  plan.push({
    rel: `${W3}/custom_recipes/CustomRecipeConfig.asset`,
    content: updateConfigNextSequence(),
    meta: null,
    note: `nextSequence=${NEXT_SEQUENCE}`,
  });
  return plan;
}

const apply = process.argv.includes("--apply");
const plan = buildPlan();

console.log(`冰沙 ${MILK_SLUSH_RECIPES.length} 道，冰淇淋 ${ICE_CREAM_RECIPES.length} 道（模型/icon 待定）`);

for (const item of plan) {
  if (!apply) {
    console.log(`[dry] ${item.rel}${item.note ? ` (${item.note})` : ""}`);
    continue;
  }
}

if (!apply) {
  console.log(`\ndry-run：${plan.length} 个文件；加 --apply 执行落盘。`);
  process.exit(0);
}

for (const category of ["milk_slush", "ice_cream"]) cleanCategoryAssets(category);
ensureCategoryFolders();
removeLayoutIcons();

for (const item of plan) {
  const abs = path.join(repoRoot, item.rel);
  fs.mkdirSync(path.dirname(abs), { recursive: true });
  fs.writeFileSync(abs, item.content, "utf8");
  if (item.meta) fs.writeFileSync(abs + ".meta", item.meta, "utf8");
  console.log(`写入 ${item.rel}${item.note ? ` (${item.note})` : ""}`);
}

console.log(`\n完成：${plan.length} 个文件。commonW3 菜谱 +${ALL_RECIPES.length - 9}（67→${67 + ALL_RECIPES.length - 9}）`);
console.log("后续：补齐 models/icons 后 Unity → Build AssetBundles（commonW3）");
