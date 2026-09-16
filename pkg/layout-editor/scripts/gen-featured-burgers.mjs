#!/usr/bin/env node
/**
 * gen-featured-burgers.mjs —— 向 commonW2「Burger大全」批量生成 21 款特色汉堡。
 *
 * 需求（用户确认）：
 *  - 夹心基础集 = 7 种经典单料：生菜/番茄/黄瓜/菠萝/芝士/牛肉饼/鸡肉饼。
 *  - 每款 2–5 层夹心；只有牛肉饼、鸡肉饼可叠到 2 层，其余 5 种各 0–1 层；牛/鸡可共存。
 *  - 不含早餐、不含搅拌（基础集天然不含，故无需额外过滤）。
 *  - score = 20 × (夹心层数 + 1)。
 *  - 与现有 44 款成品汉堡按「夹心多集」去重，只生成未出现过的组合。
 *  - 手挑 21 款特色组合（双牛/双鸡/牛鸡双拼/夏威夷/满蔬/四饼满配…）。
 *  - 不生成 icon：icon 字段留空（{fileID: 0}），后续由人工逐个补充。
 *
 * 落位：按 split-burger-subcategories.mjs 的 subcategoryOf 规则——本批全部含蛋白质，
 *   score>=100（4/5 层）→ deluxe，否则（2/3 层）→ classic。
 *
 * uID：读 CustomRecipeConfig.asset 的 nextSequence，58321*1000+seq 依次分配，回写 nextSequence。
 * names.json：保留 BOM + 4 空格缩进 + 末尾换行，追加条目。
 *
 * 用法：
 *   node layout-editor/scripts/gen-featured-burgers.mjs           # dry-run，仅打印计划
 *   node layout-editor/scripts/gen-featured-burgers.mjs --apply   # 落盘 + 写报告
 */
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, "../..");
const CR_DIR = path.join(repoRoot, "Assets/commonW2/custom_recipes");
const BURGER_DIR = path.join(CR_DIR, "burger");
const NAMES_JSON = path.join(CR_DIR, "names.json");
const CONFIG = path.join(CR_DIR, "CustomRecipeConfig.asset");
const REPORT = path.join(here, "gen-featured-burgers.report.json");

const APPLY = process.argv.includes("--apply");

// ---------------------------------------------------------------- 常量 GUID（逐字符照抄调研表）

const SCRIPT_CUSTOM_RECIPE = "83fb008bcc8e793429b02c178c430815"; // CustomRecipeSO
const GUID_PLATING = "02b04fdf6fef0e944870fd5e06375ed1"; // common01 PlatingSteps/Plate
const GUID_MODEL = "b65af1b8a1e4f2a4d82417a5981a1fbb"; // common01 CompositeBurgerSO
const GUID_BUN = "965ff691e25e50b0c5151ea9a97899d4"; // common03 dlc08_choppedbun（compositionSOs[0]）

/** 7 种经典夹心：key → { zh, en, guid }。层序 = 数组顺序（L→T→Cu→P→Ch→Ck→M）。 */
const ING = {
  Lettuce: { zh: "生菜", en: "Lettuce", guid: "45f00e942534da143a3e94855455b50b" },
  Tomato: { zh: "番茄", en: "Tomato", guid: "8a928e5055585f34c89f8535c40fea3f" },
  Cucumber: { zh: "黄瓜", en: "Cucumber", guid: "09a8ff9ede819e14e9f89df70f935402" },
  Pineapple: { zh: "菠萝", en: "Pineapple", guid: "ff0a377cca6a00a45a5a5f13ac30098d" },
  Cheese: { zh: "芝士", en: "Cheese", guid: "b785cb39803b3be40b7d7052d54595fd" },
  Chicken: { zh: "鸡肉", en: "Chicken", guid: "171d0a1a6e4db824291c137bebe0be87" },
  Meat: { zh: "牛肉", en: "Meat", guid: "4ec8a736936e7744c91d254705783c2c" },
};
/** 命名与堆叠层序（与现有资产一致）。 */
const ORDER = ["Lettuce", "Tomato", "Cucumber", "Pineapple", "Cheese", "Chicken", "Meat"];
/** 仅这两种允许叠 2 层。 */
const DOUBLABLE = new Set(["Chicken", "Meat"]);

// ---------------------------------------------------------------- 21 款特色组合（计数字典）

/** 每项：食材 → 层数（1 或 2；未列即 0）。特色说明仅用于报告。 */
const PICKS = [
  [{ Chicken: 1, Meat: 1 }, "肉食双拼：牛肉+鸡肉同堡"],
  [{ Meat: 2 }, "纯双牛肉"],
  [{ Chicken: 2 }, "纯双鸡肉"],
  [{ Cheese: 1, Meat: 2 }, "芝士双牛肉"],
  [{ Cheese: 1, Chicken: 2 }, "芝士双鸡肉"],
  [{ Cheese: 1, Chicken: 2, Meat: 2 }, "四饼芝士满配（双牛双鸡+芝士）"],
  [{ Lettuce: 1, Tomato: 1, Cheese: 1, Meat: 1 }, "经典四层：生菜番茄芝士牛肉"],
  [{ Lettuce: 1, Tomato: 1, Cheese: 1, Chicken: 1 }, "经典四层：生菜番茄芝士鸡肉"],
  [{ Pineapple: 1, Cheese: 1, Meat: 1 }, "夏威夷：菠萝芝士牛肉"],
  [{ Pineapple: 1, Cheese: 1, Chicken: 1 }, "夏威夷：菠萝芝士鸡肉"],
  [{ Pineapple: 1, Chicken: 1, Meat: 1 }, "菠萝肉食双拼"],
  [{ Lettuce: 1, Cucumber: 1, Cheese: 1, Meat: 2 }, "蔬菜双牛肉芝士"],
  [{ Lettuce: 1, Tomato: 1, Cucumber: 1, Cheese: 1, Meat: 1 }, "满蔬牛肉五层"],
  [{ Lettuce: 1, Tomato: 1, Cucumber: 1, Cheese: 1, Chicken: 1 }, "满蔬鸡肉五层"],
  [{ Lettuce: 1, Tomato: 1, Cucumber: 1, Chicken: 1, Meat: 1 }, "满蔬肉食双拼五层"],
  [{ Lettuce: 1, Pineapple: 1, Cheese: 1, Chicken: 1, Meat: 1 }, "菠萝生菜芝士双拼"],
  [{ Cucumber: 1, Cheese: 1, Chicken: 1, Meat: 1 }, "黄瓜芝士双拼"],
  [{ Tomato: 1, Cheese: 1, Meat: 2 }, "番茄芝士双牛肉"],
  [{ Lettuce: 1, Cheese: 1, Chicken: 2 }, "生菜芝士双鸡肉"],
  [{ Tomato: 1, Pineapple: 1, Cheese: 1, Chicken: 1, Meat: 1 }, "番茄菠萝芝士双拼五层"],
  [{ Lettuce: 1, Tomato: 1, Cheese: 1, Chicken: 2 }, "生菜番茄芝士双鸡肉"],
];

// ---------------------------------------------------------------- 工具（复用 migrate 脚本约定）

function newGuid() {
  return crypto.randomBytes(16).toString("hex");
}
function nativeAssetMeta(guid) {
  return `fileFormatVersion: 2\nguid: ${guid}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
}

/** 从计数字典构造 { name, zh, en, fillings:[key...], count } 。 */
function build(counts) {
  const partsEn = [];
  const partsZh = [];
  const fillings = [];
  for (const key of ORDER) {
    const c = counts[key] || 0;
    if (c === 0) continue;
    if (c > 1 && !DOUBLABLE.has(key)) throw new Error(`${key} 不允许叠 ${c} 层`);
    if (c > 2) throw new Error(`${key} 超过 2 层`);
    if (c === 2) {
      partsEn.push("Double" + ING[key].en);
      partsZh.push("双" + ING[key].zh);
    } else {
      partsEn.push(ING[key].en);
      partsZh.push(ING[key].zh);
    }
    for (let i = 0; i < c; i++) fillings.push(key);
  }
  return {
    name: partsEn.join("") + "Burger",
    zh: partsZh.join("") + "汉堡",
    en: partsEn.join(" ") + " Burger",
    fillings,
    count: fillings.length,
  };
}

/** 规范化夹心多集为可比较的 key（排序计数），用于去重。 */
function multisetKey(counts) {
  return ORDER.filter((k) => (counts[k] || 0) > 0)
    .map((k) => `${k}:${counts[k]}`)
    .join("|");
}

// ---------------------------------------------------------------- 现有成品汉堡去重索引

/** 全库 guid → 资产文件名，用于把 compositionSOs 还原成夹心 key。 */
function buildGuidToKey() {
  // 反向：夹心 guid → key
  const guidToKey = new Map();
  for (const key of ORDER) guidToKey.set(ING[key].guid, key);
  return guidToKey;
}

function metaGuidOf(assetPath) {
  const meta = assetPath + ".meta";
  if (!fs.existsSync(meta)) return "";
  for (const line of fs.readFileSync(meta, "utf8").split(/\r?\n/))
    if (line.startsWith("guid: ")) return line.slice(6).trim();
  return "";
}

/** 扫描现有 burger 成品，返回已存在的夹心多集 key 集合（仅统计 7 经典夹心可完全表示者）。 */
function existingMultisets() {
  const guidToKey = buildGuidToKey();
  const set = new Set();
  const walk = (dir) => {
    let entries;
    try {
      entries = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
      return;
    }
    for (const e of entries) {
      const full = path.join(dir, e.name);
      if (e.isDirectory()) {
        if (e.name === "models" || e.name === "icons") continue;
        walk(full);
      } else if (e.name.endsWith(".asset")) {
        const text = fs.readFileSync(full, "utf8");
        // 只看成品汉堡：type=1 且 score>0
        const type = /^\s*type:\s*(\d+)/m.exec(text)?.[1];
        const score = parseInt(/^\s*score:\s*(-?\d+)/m.exec(text)?.[1] ?? "0", 10);
        if (type !== "1" || score <= 0) continue;
        // 抓 compositionSOs 段的 guid 列表
        const comp = /compositionSOs:\s*([\s\S]*?)\n\s*optionalSOs:/.exec(text)?.[1] ?? "";
        const guids = [...comp.matchAll(/guid:\s*([0-9a-f]{32})/g)].map((m) => m[1]);
        const counts = {};
        let allKnown = true;
        for (const g of guids) {
          if (g === GUID_BUN) continue; // 面包不计
          const key = guidToKey.get(g);
          if (!key) {
            allKnown = false;
            break;
          } // 含非 7 经典夹心 → 不纳入去重集
          counts[key] = (counts[key] || 0) + 1;
        }
        if (allKnown && Object.keys(counts).length) set.add(multisetKey(counts));
      }
    }
  };
  walk(BURGER_DIR);
  return set;
}

// ---------------------------------------------------------------- 分类落位（对齐 subcategoryOf）

function subOf(count, score) {
  // 本批全部含蛋白质（Meat/Chicken），非早餐/海鲜/mega/素。
  return score >= 100 ? "deluxe" : "classic";
}

// ---------------------------------------------------------------- .asset 模板

function assetYaml({ name, uid, score, fillings }) {
  const lines = [];
  lines.push("%YAML 1.1");
  lines.push("%TAG !u! tag:unity3d.com,2011:");
  lines.push("--- !u!114 &11400000");
  lines.push("MonoBehaviour:");
  lines.push("  m_ObjectHideFlags: 0");
  lines.push("  m_PrefabParentObject: {fileID: 0}");
  lines.push("  m_PrefabInternal: {fileID: 0}");
  lines.push("  m_GameObject: {fileID: 0}");
  lines.push("  m_Enabled: 1");
  lines.push("  m_EditorHideFlags: 0");
  lines.push(`  m_Script: {fileID: 11500000, guid: ${SCRIPT_CUSTOM_RECIPE}, type: 3}`);
  lines.push(`  m_Name: ${name}`);
  lines.push("  m_EditorClassIdentifier: ");
  lines.push("  type: 1");
  lines.push(`  recipeName: ${name}`);
  lines.push(`  uID: ${uid}`);
  lines.push(`  score: ${score}`);
  lines.push(`  platingStepSO: {fileID: 11400000, guid: ${GUID_PLATING}, type: 2}`);
  lines.push(`  modelSO: {fileID: 11400000, guid: ${GUID_MODEL}, type: 2}`);
  lines.push("  model: {fileID: 0}");
  lines.push("  iconSO: {fileID: 0}");
  lines.push("  icon: {fileID: 0}"); // 空图标，后续人工补充
  lines.push("  compositionSOs:");
  lines.push(`  - {fileID: 11400000, guid: ${GUID_BUN}, type: 2}`);
  for (const key of fillings)
    lines.push(`  - {fileID: 11400000, guid: ${ING[key].guid}, type: 2}`);
  lines.push("  optionalSOs: []");
  lines.push("  cookingStepSO: {fileID: 0}");
  lines.push("  cookingStepIconSO: {fileID: 0}");
  lines.push("  cookingStepIcon: {fileID: 0}");
  lines.push("  cookingProgress: 0");
  lines.push("  mixingIconSO: {fileID: 0}");
  lines.push("  mixingIcon: {fileID: 0}");
  lines.push("  mixingProgress: 0");
  return lines.join("\n") + "\n";
}

// ---------------------------------------------------------------- 主流程

const existing = existingMultisets();

// 现有 names.json 的 id 集合（防重名）
const namesRaw = fs.readFileSync(NAMES_JSON, "utf8");
const hasBom = namesRaw.charCodeAt(0) === 0xfeff;
const namesData = JSON.parse(hasBom ? namesRaw.slice(1) : namesRaw);
const existingIds = new Set((namesData.names ?? []).map((n) => n.id));

// 读 nextSequence
const configText = fs.readFileSync(CONFIG, "utf8");
const uidPrefix = parseInt(/^\s*uidPrefix:\s*(\d+)/m.exec(configText)[1], 10);
let nextSeq = parseInt(/^\s*nextSequence:\s*(\d+)/m.exec(configText)[1], 10);

const planned = [];
const skipped = [];
const seenKeys = new Set();

for (const [counts, feature] of PICKS) {
  const b = build(counts);
  const key = multisetKey(counts);
  // 自检：层数、双层规则
  if (b.count < 2 || b.count > 5) throw new Error(`${b.name} 层数 ${b.count} 越界`);
  if ((counts.Meat || 0) > 2 || (counts.Chicken || 0) > 2)
    throw new Error(`${b.name} 肉/鸡超过 2`);
  // 去重：本批内 + 现有资产 + 现有 names id
  if (seenKeys.has(key)) {
    skipped.push({ name: b.name, reason: "本批内重复夹心多集" });
    continue;
  }
  if (existing.has(key)) {
    skipped.push({ name: b.name, reason: "现有资产已存在同夹心多集" });
    continue;
  }
  if (existingIds.has(b.name)) {
    skipped.push({ name: b.name, reason: "names.json 已有同名 id" });
    continue;
  }
  seenKeys.add(key);
  const score = 20 * (b.count + 1);
  const uid = uidPrefix * 1000 + nextSeq;
  const sub = subOf(b.count, score);
  planned.push({ ...b, feature, key, score, uid, sub });
  nextSeq += 1;
}

// ---------------------------------------------------------------- 输出计划

console.log(`Burger大全 特色汉堡生成（${APPLY ? "APPLY" : "DRY-RUN"}）`);
console.log(`  基础集：生菜/番茄/黄瓜/菠萝/芝士/牛肉饼/鸡肉饼；牛/鸡≤2 可共存；2–5 层。`);
console.log(`  候选 ${PICKS.length} → 计划生成 ${planned.length}，跳过 ${skipped.length}`);
console.log("");
console.log(`  ${"#".padStart(2)}  层 ${"分".padStart(3)}  ${"子类".padEnd(7)} uID       名称`);
planned.forEach((p, i) => {
  console.log(
    `  ${String(i + 1).padStart(2)}  ${p.count}  ${String(p.score).padStart(3)}  ${p.sub.padEnd(7)} ${p.uid}  ${p.name}  [${p.zh}] — ${p.feature}`
  );
});
if (skipped.length) {
  console.log("\n  跳过：");
  for (const s of skipped) console.log(`    - ${s.name}：${s.reason}`);
}

// ---------------------------------------------------------------- 落盘

if (APPLY) {
  const newNameEntries = [];
  for (const p of planned) {
    const dir = path.join(BURGER_DIR, p.sub);
    fs.mkdirSync(dir, { recursive: true });
    const assetPath = path.join(dir, p.name + ".asset");
    fs.writeFileSync(assetPath, assetYaml(p));
    fs.writeFileSync(assetPath + ".meta", nativeAssetMeta(newGuid()));
    newNameEntries.push({ id: p.name, zh: p.zh, en: p.en });
  }

  // 更新 names.json（保留 BOM + 4 空格缩进 + 末尾换行）
  namesData.names = [...(namesData.names ?? []), ...newNameEntries];
  const bom = hasBom ? "\uFEFF" : "";
  fs.writeFileSync(NAMES_JSON, bom + JSON.stringify(namesData, null, 4) + "\n");

  // 回写 nextSequence
  const newConfig = configText.replace(/^(\s*nextSequence:\s*)\d+/m, `$1${nextSeq}`);
  fs.writeFileSync(CONFIG, newConfig);

  console.log(`\n已生成 ${planned.length} 款 .asset(+.meta)；names.json +${newNameEntries.length}；nextSequence → ${nextSeq}`);
}

// ---------------------------------------------------------------- 报告

fs.writeFileSync(
  REPORT,
  JSON.stringify(
    {
      apply: APPLY,
      generated: planned.map((p) => ({
        name: p.name,
        zh: p.zh,
        en: p.en,
        sub: p.sub,
        count: p.count,
        score: p.score,
        uID: p.uid,
        fillings: p.fillings,
        feature: p.feature,
      })),
      skipped,
      nextSequenceAfter: nextSeq,
    },
    null,
    2
  )
);
console.log(`报告：layout-editor/scripts/gen-featured-burgers.report.json`);
