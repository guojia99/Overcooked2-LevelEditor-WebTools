#!/usr/bin/env node
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const W5 = "Assets/commonW5";
const apply = process.argv.includes("--apply");

const md5 = (s) => crypto.createHash("md5").update(s).digest("hex");
const recipeGuid = (id) => md5(`commonw5-recipe:${id}`);
const recipeIconGuid = (id) => md5(`commonw5-recipe-icon:${id}`);
const folderGuid = (rel) => md5(`commonw5-folder:${rel}`);

const SCRIPT_CUSTOM_RECIPE = "83fb008bcc8e793429b02c178c430815";
const SCRIPT_RECIPE_CONFIG = "5edb07060a0c6c140aa72ffb8fd3f021";
const UID_PREFIX = 58323;
const NEXT_SEQUENCE = 15;

function readGuid(rel) {
  const abs = path.join(repoRoot, rel);
  if (!fs.existsSync(abs)) throw new Error("缺少资产 meta: " + rel);
  const m = fs.readFileSync(abs, "utf8").match(/^guid: ([0-9a-f]{32})$/m);
  if (!m) throw new Error("无法解析 guid: " + rel);
  return m[1];
}

const GUID = {
  plate: readGuid("Assets/common01/food/PlatingSteps/Plate.asset.meta"),
  steamer: readGuid("Assets/common02/food/CookingSteps/Steamer.asset.meta"),
  steamerIcon: readGuid("Assets/common02/food/CookingSteps/icons/SteamerIcon.asset.meta"),
  mixerIcon: readGuid("Assets/common02/food/CookingSteps/icons/MixerIcon.asset.meta"),
  fish: readGuid("Assets/common01/food/Ingredients/SushiFishSO.asset.meta"),
  flour: readGuid("Assets/common01/food/Ingredients/FlourSO.asset.meta"),
  prawn: readGuid("Assets/common01/food/Ingredients/SushiPrawnSO.asset.meta"),
  modelFish: readGuid("Assets/commonW1/pseudo_prefab_so/core/decor/recipes/Steamed_Fish_SO.asset.meta"),
  modelPrawns: readGuid("Assets/commonW1/pseudo_prefab_so/core/decor/recipes/Steamed_Prawns_SO.asset.meta"),
};

const RECIPES = [
  {
    id: "Web_Steamed_Fish",
    zh: "Web 蒸鱼",
    en: "Web Steamed Fish",
    type: 2,
    score: 40,
    uid: 58323012,
    composition: [{ guid: GUID.fish }],
    modelSO: GUID.modelFish,
    iconSource: "layout-editor/web/public/icons/recipes/Steamed_Fish_SO.png",
    cooking: true,
    mixing: false,
  },
  {
    id: "Web_Steamed_PrawnPrep",
    zh: "Web 虾烧麦面团（中间）",
    en: "Web Prawn Shumai Dough (Intermediate)",
    type: 3,
    score: 0,
    uid: 58323013,
    composition: [{ guid: GUID.flour }, { guid: GUID.prawn }],
    modelSO: null,
    iconSource: null,
    cooking: false,
    mixing: true,
  },
  {
    id: "Web_Steamed_Prawns",
    zh: "Web 虾烧麦",
    en: "Web Prawn Shumai",
    type: 2,
    score: 80,
    uid: 58323014,
    composition: [{ recipe: "Web_Steamed_PrawnPrep" }],
    modelSO: GUID.modelPrawns,
    iconSource: "layout-editor/web/public/icons/recipes/Steamed_Prawns_SO.png",
    cooking: true,
    mixing: false,
  },
];

const recipeById = new Map(RECIPES.map((r) => [r.id, r]));

function compositionRef(c) {
  if (c.recipe) {
    if (!recipeById.has(c.recipe)) throw new Error("未知组成菜谱: " + c.recipe);
    return `{fileID: 11400000, guid: ${recipeGuid(c.recipe)}, type: 2}`;
  }
  return `{fileID: 11400000, guid: ${c.guid}, type: 2}`;
}

function recipeYaml(r) {
  const compLines = r.composition.map((c) => `  - ${compositionRef(c)}`).join("\n");
  const plating = r.type === 2
    ? `{fileID: 11400000, guid: ${GUID.plate}, type: 2}`
    : "{fileID: 0}";
  const model = r.modelSO
    ? `{fileID: 11400000, guid: ${r.modelSO}, type: 2}`
    : "{fileID: 0}";
  const icon = r.iconSource
    ? `{fileID: 21300000, guid: ${recipeIconGuid(r.id)}, type: 3}`
    : "{fileID: 0}";
  const cooking = r.cooking
    ? `  cookingStepSO: {fileID: 11400000, guid: ${GUID.steamer}, type: 2}
  cookingStepIconSO: {fileID: 11400000, guid: ${GUID.steamerIcon}, type: 2}`
    : `  cookingStepSO: {fileID: 0}
  cookingStepIconSO: {fileID: 0}`;
  const mixingIcon = r.mixing
    ? `{fileID: 11400000, guid: ${GUID.mixerIcon}, type: 2}`
    : "{fileID: 0}";
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
  type: ${r.type}
  recipeName: ${r.id}
  uID: ${r.uid}
  score: ${r.score}
  platingStepSO: ${plating}
  modelSO: ${model}
  model: {fileID: 0}
  iconSO: {fileID: 0}
  icon: ${icon}
  compositionSOs:
${compLines}
  optionalSOs: []
${cooking}
  cookingStepIcon: {fileID: 0}
  cookingProgress: ${r.cooking ? 1 : 0}
  mixingIconSO: ${mixingIcon}
  mixingIcon: {fileID: 0}
  mixingProgress: ${r.mixing ? 1 : 0}
`;
}

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

const spriteMetaTemplate = fs.readFileSync(
  path.join(repoRoot, `${W5}/custom_recipes/burrito/icons/Web_Burrito_Meat_Chicken.png.meta`),
  "utf8",
);

const SPRITE_META = (guid) => spriteMetaTemplate.replace(/^guid: [0-9a-f]{32}$/m, `guid: ${guid}`);

const plan = [];

function addFile(rel, content, note) {
  plan.push({ rel, content, note });
}

function addCopy(rel, srcRel, note) {
  plan.push({ rel, srcRel, note });
}

function buildPlan() {
  const steamDir = `${W5}/custom_recipes/steamed`;
  addFile(`${steamDir}.meta`, FOLDER_META(folderGuid(steamDir)), "steamed 目录");
  addFile(`${steamDir}/icons.meta`, FOLDER_META(folderGuid(`${steamDir}/icons`)), "steamed icons 目录");

  for (const r of RECIPES) {
    addFile(
      `${steamDir}/${r.id}.asset`,
      recipeYaml(r),
      `${r.id}  uID=${r.uid}  guid=${recipeGuid(r.id)}`,
    );
    addFile(
      `${steamDir}/${r.id}.asset.meta`,
      ASSET_META(recipeGuid(r.id)),
      `${r.id} meta`,
    );
    if (!r.iconSource) continue;
    const iconRel = `${steamDir}/icons/${r.id}.png`;
    addCopy(iconRel, r.iconSource, `${r.id} 图标`);
    addFile(`${iconRel}.meta`, SPRITE_META(recipeIconGuid(r.id)), `${r.id} 图标 meta`);
    const srcAbs = path.join(repoRoot, r.iconSource);
    if (!fs.existsSync(srcAbs)) throw new Error("缺少图标源: " + r.iconSource);
    addCopy(`layout-editor/web/public/icons/recipes/${r.id}.png`, r.iconSource, `${r.id} web 图标`);
    if (fs.existsSync(path.join(repoRoot, "layout-editor/web/dist"))) {
      addCopy(`layout-editor/web/dist/icons/recipes/${r.id}.png`, r.iconSource, `${r.id} dist 图标`);
    }
  }

  mergeNames();
  mergeConfig();
  validateUids();
}

function mergeNames() {
  const rel = `${W5}/custom_recipes/names.json`;
  const abs = path.join(repoRoot, rel);
  const data = JSON.parse(fs.readFileSync(abs, "utf8").replace(/^\uFEFF/, ""));
  data.names = data.names ?? [];
  const byId = new Map(data.names.map((n) => [n.id, n]));
  for (const r of RECIPES) {
    const prev = byId.get(r.id);
    if (prev) {
      prev.zh = r.zh;
      prev.en = r.en;
    } else {
      data.names.push({ id: r.id, zh: r.zh, en: r.en });
    }
  }
  addFile(rel, "\uFEFF" + JSON.stringify(data, null, 2) + "\n", "names.json 合并");
}

function mergeConfig() {
  const rel = `${W5}/custom_recipes/CustomRecipeConfig.asset`;
  const abs = path.join(repoRoot, rel);
  let text = fs.readFileSync(abs, "utf8");
  if (!/id: steamed\b/.test(text)) {
    const marker = "  subcategories:";
    const idx = text.indexOf(marker);
    if (idx < 0) throw new Error("CustomRecipeConfig 缺少 subcategories 段");
    const category = `  - id: steamed
    zh: "\\u84B8\\u83DC"
    en: Steamed
`;
    text = text.slice(0, idx) + category + text.slice(idx);
  }
  text = text.replace(/nextSequence: \d+/, (m) => {
    const cur = Number(m.split(":")[1].trim());
    return `nextSequence: ${Math.max(cur, NEXT_SEQUENCE)}`;
  });
  addFile(rel, text, "CustomRecipeConfig 合并（steamed 分类 / nextSequence）");
}

function validateUids() {
  const roots = [
    "Assets/commonW2/custom_recipes",
    "Assets/commonW3/custom_recipes",
    `${W5}/custom_recipes`,
  ];
  const own = new Set(RECIPES.map((r) => r.id));
  const seen = new Map();
  for (const root of roots) {
    const absRoot = path.join(repoRoot, root);
    if (!fs.existsSync(absRoot)) continue;
    for (const file of walk(absRoot, (n) => n.endsWith(".asset"))) {
      if (file.includes("/models/") || file.includes("/model_so/")) continue;
      const text = fs.readFileSync(file, "utf8");
      if (!text.includes(SCRIPT_CUSTOM_RECIPE)) continue;
      const m = text.match(/^\s*uID: (\d+)$/m);
      if (!m) continue;
      const uid = m[1];
      const id = path.basename(file, ".asset");
      if (seen.has(uid) && seen.get(uid) !== id) {
        throw new Error(`uID 冲突 ${uid}: ${seen.get(uid)} ↔ ${id}`);
      }
      seen.set(uid, id);
    }
  }
  for (const r of RECIPES) {
    const existing = seen.get(String(r.uid));
    if (existing && existing !== r.id) {
      throw new Error(`新 uID ${r.uid} 已被 ${existing} 占用`);
    }
  }
}

function walk(dir, pick) {
  const out = [];
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, ent.name);
    if (ent.isDirectory()) out.push(...walk(p, pick));
    else if (pick(p)) out.push(p);
  }
  return out;
}

buildPlan();

console.log(`=== gen-commonw5-steamed（${apply ? "APPLY" : "DRY-RUN"}） ===`);
for (const r of RECIPES) {
  console.log(`  recipe ${r.id}  uID=${r.uid}  score=${r.score}  guid=${recipeGuid(r.id)}`);
}
console.log(`  uidPrefix=${UID_PREFIX}  nextSequence=${NEXT_SEQUENCE}`);
console.log("");
for (const item of plan) {
  console.log(`  ${apply ? "写入" : "计划"} ${item.rel}  (${item.note})`);
}

if (apply) {
  for (const item of plan) {
    const abs = path.join(repoRoot, item.rel);
    fs.mkdirSync(path.dirname(abs), { recursive: true });
    if (item.srcRel) fs.copyFileSync(path.join(repoRoot, item.srcRel), abs);
    else fs.writeFileSync(abs, item.content, "utf8");
  }
  console.log(`\n完成：${plan.length} 个文件已写入。`);
} else {
  console.log(`\n（dry-run：加 --apply 落盘，共 ${plan.length} 个文件）`);
}
