#!/usr/bin/env node
/**
 * split-burger-subcategories.mjs —— Burger大全（commonW2）细分类迁移。
 *
 * 把 Assets/commonW2/custom_recipes/burger/ 下的 74 个菜谱资产按子分类移入
 * 二级目录（assembly / classic / deluxe / mega / breakfast / seafood / veggie / filling）。
 *
 * 为什么安全（已核实）：
 *  1. LayoutEditorLevelAdminApi.ScanCustomRecipes 的 category 只取 custom_recipes/ 下
 *     **第一段**目录名（`rel.Substring(0, slash)`）→ category 仍是 "burger"，
 *     所有按 category 分组的逻辑不受影响；
 *  2. ScanAssetsByScript 用 SearchOption.AllDirectories 递归扫描 → 资产照样被发现；
 *  3. CustomRecipeNamesPath 向上回溯直到遇见 custom_recipes → names.json 定位不变；
 *  4. .asset 与 .meta 成对移动，guid 不变 → LevelInfo/组装定义的引用全部按 guid，不受影响；
 *  5. models/ 与 icons/ 留在 burger/ 根不动（CollectModels 硬编码 burger/models）。
 *
 * ⚠ 配套代码改动（必须同步，否则本关 BurgerOptional 创建失败）：
 *    LayoutEditorBurgerApi.FindCommonW2BurgerModelAssembly 的
 *    `burger/OptionalBurger.asset` / `burger/ChickenBurgerAssembly.asset`
 *    → `burger/assembly/...`
 *
 * 用法：
 *    node layout-editor/scripts/split-burger-subcategories.mjs          # dry-run，只打印计划
 *    node layout-editor/scripts/split-burger-subcategories.mjs --apply  # 实际移动并写报告
 */
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, "../..");
const BURGER_DIR = path.join(repoRoot, "Assets/commonW2/custom_recipes/burger");
const NAMES_JSON = path.join(repoRoot, "Assets/commonW2/custom_recipes/names.json");
const REPORT = path.join(here, "split-burger-subcategories.report.json");

const OPTIONAL_BURGER_SCRIPT_GUID = "e7bb274eb901e2042b1c49a42ecec9df";

/** 子分类顺序与中文名（与 CustomRecipeConfigSO.subcategories / 前端 BURGER_SUBTYPE_ZH 对齐）。 */
export const SUBCATEGORIES = [
  { id: "assembly", zh: "组装定义", en: "Assembly" },
  { id: "classic", zh: "经典汉堡", en: "Classic" },
  { id: "deluxe", zh: "豪华汉堡", en: "Deluxe" },
  { id: "mega", zh: "巨无霸汉堡", en: "Mega" },
  { id: "breakfast", zh: "早餐汉堡", en: "Breakfast" },
  { id: "seafood", zh: "海鲜汉堡", en: "Seafood" },
  { id: "veggie", zh: "素食汉堡", en: "Veggie" },
  { id: "filling", zh: "夹心/中间产物", en: "Filling" },
];

// ---------------------------------------------------------------- 工具

function readText(p) {
  return fs.readFileSync(p, "utf8");
}

function yamlField(text, key) {
  const m = new RegExp(`^\\s*${key}:\\s*(.*)$`, "m").exec(text);
  return m ? m[1].trim() : "";
}

function metaGuid(metaPath) {
  if (!fs.existsSync(metaPath)) return "";
  for (const line of readText(metaPath).split(/\r?\n/)) {
    if (line.startsWith("guid: ")) return line.slice(6).trim();
  }
  return "";
}

/** 全库 guid → 资产文件名（用于把 compositionSOs 的 guid 还原成可读 id）。 */
function buildGuidIndex() {
  const index = new Map();
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
        walk(full);
      } else if (e.name.endsWith(".asset.meta")) {
        const g = metaGuid(full);
        if (g && !index.has(g)) index.set(g, e.name.slice(0, -".asset.meta".length));
      }
    }
  };
  walk(path.join(repoRoot, "Assets"));
  return index;
}

function loadNames() {
  if (!fs.existsSync(NAMES_JSON)) return new Map();
  // names.json 带 UTF-8 BOM
  const raw = readText(NAMES_JSON).replace(/^\uFEFF/, "");
  const data = JSON.parse(raw);
  return new Map((data.names ?? []).map((n) => [n.id, n.zh]));
}

// ---------------------------------------------------------------- 分类规则

const SEAFOOD_RE = /Prawn|Shrimp|Fish/i;
const PROTEIN_RE = /Meat|Chicken|Bacon|Sausage|Egg|Patty|Beef|Pepperoni|Cheese/i;

/**
 * 子分类判定（与计划评审表一致，结果：
 *  assembly 6 / classic 18 / deluxe 7 / mega 2 / breakfast 4 / seafood 3 / veggie 10 / filling 24）。
 */
export function subcategoryOf({ id, isAssembly, type, score, fillerIds }) {
  if (isAssembly || id.endsWith("_filler")) return "assembly";
  // type: 1=Composite 2=Cooked 3=Mixed；非成品组装或 0 分 → 中间产物
  if (type !== 1 || score <= 0) return "filling";
  if (id.startsWith("Breakfast")) return "breakfast";
  if (score >= 160) return "mega";
  if (fillerIds.some((f) => SEAFOOD_RE.test(f))) return "seafood";
  if (!fillerIds.some((f) => PROTEIN_RE.test(f))) return "veggie";
  return score >= 100 ? "deluxe" : "classic";
}

// ---------------------------------------------------------------- 扫描

function scanBurgerAssets(guidIndex) {
  const rows = [];
  for (const name of fs.readdirSync(BURGER_DIR).sort()) {
    if (!name.endsWith(".asset")) continue;
    const full = path.join(BURGER_DIR, name);
    if (!fs.statSync(full).isFile()) continue;
    const text = readText(full);
    const scriptField = yamlField(text, "m_Script");
    const scriptGuid = /guid:\s*([0-9a-f]{32})/.exec(scriptField)?.[1] ?? "";
    const id = name.slice(0, -".asset".length);

    const compSeg = text.includes("compositionSOs:")
      ? text.split("compositionSOs:")[1].split("optionalSOs:")[0]
      : "";
    const fillerIds = [...compSeg.matchAll(/guid: ([0-9a-f]{32})/g)]
      .map((m) => guidIndex.get(m[1]) ?? "?")
      .filter((n) => !/choppedbun/i.test(n));

    rows.push({
      id,
      file: name,
      isAssembly: scriptGuid === OPTIONAL_BURGER_SCRIPT_GUID,
      type: Number(yamlField(text, "type") || 0),
      score: Number(yamlField(text, "score") || 0),
      fillerIds,
    });
  }
  return rows;
}

// ---------------------------------------------------------------- 主流程

function main() {
  const apply = process.argv.includes("--apply");
  if (!fs.existsSync(BURGER_DIR)) {
    console.error("未找到目录：" + BURGER_DIR);
    process.exit(1);
  }

  console.log("索引全库 guid…");
  const guidIndex = buildGuidIndex();
  const names = loadNames();
  const rows = scanBurgerAssets(guidIndex);

  const plan = rows.map((r) => ({
    ...r,
    nameZh: names.get(r.id) ?? r.id,
    sub: subcategoryOf(r),
  }));

  const bySub = new Map(SUBCATEGORIES.map((s) => [s.id, []]));
  for (const p of plan) bySub.get(p.sub).push(p);

  for (const s of SUBCATEGORIES) {
    const arr = bySub.get(s.id);
    console.log(`\n### ${s.id}/  ${s.zh}  ${arr.length} 条`);
    for (const p of arr) {
      console.log(`  ${p.id.padEnd(50)} ${String(p.nameZh).padEnd(14)} ${String(p.score).padStart(3)}分`);
    }
  }
  console.log(`\n合计 ${plan.length} 条`);

  if (!apply) {
    console.log("\n[dry-run] 未移动任何文件。确认无误后加 --apply 执行。");
    return;
  }

  // 建目录
  for (const s of SUBCATEGORIES) {
    const dir = path.join(BURGER_DIR, s.id);
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
      // Unity 需要目录 meta；沿用「子目录 assetBundleName 留空、继承 commonW2 根」的既有约定。
      const dirMeta = dir + ".meta";
      if (!fs.existsSync(dirMeta)) {
        fs.writeFileSync(
          dirMeta,
          [
            "fileFormatVersion: 2",
            "guid: " + folderGuid(s.id),
            "folderAsset: yes",
            "DefaultImporter:",
            "  externalObjects: {}",
            "  userData: ",
            "  assetBundleName: ",
            "  assetBundleVariant: ",
            "",
          ].join("\n"),
          "utf8",
        );
      }
    }
  }

  // .asset 与 .meta 成对移动（guid 不变）
  const moved = [];
  for (const p of plan) {
    const src = path.join(BURGER_DIR, p.file);
    const dst = path.join(BURGER_DIR, p.sub, p.file);
    if (!fs.existsSync(src)) {
      console.warn("跳过（源缺失）：" + src);
      continue;
    }
    fs.renameSync(src, dst);
    const srcMeta = src + ".meta";
    if (fs.existsSync(srcMeta)) fs.renameSync(srcMeta, dst + ".meta");
    else console.warn("⚠ 缺 .meta（guid 会被 Unity 重建）：" + src);
    moved.push({
      id: p.id,
      nameZh: p.nameZh,
      sub: p.sub,
      score: p.score,
      type: p.type,
      guid: metaGuid(dst + ".meta"),
      from: path.relative(repoRoot, src).replace(/\\/g, "/"),
      to: path.relative(repoRoot, dst).replace(/\\/g, "/"),
    });
  }

  fs.writeFileSync(
    REPORT,
    JSON.stringify(
      {
        generatedAt: new Date().toISOString(),
        subcategories: SUBCATEGORIES,
        counts: Object.fromEntries(SUBCATEGORIES.map((s) => [s.id, bySub.get(s.id).length])),
        moved,
      },
      null,
      2,
    ) + "\n",
    "utf8",
  );
  console.log(`\n已移动 ${moved.length} 个资产，报告：${path.relative(repoRoot, REPORT)}`);
  console.log("提示：回 Unity 触发一次 Refresh，让 AssetDatabase 更新路径映射。");
}

/** 目录 guid：确定性 md5("burgersub:<id>")，避免重复执行产生新 guid。 */
function folderGuid(id) {
  // 与 import-dlc-content.mjs 同款确定性 guid 约定
  return crypto.createHash("md5").update("burgersub:" + id).digest("hex");
}

main();
