#!/usr/bin/env node
/**
 * stat-matchlist-mapping.mjs — 食材 → RecipeMatchList 映射全量统计。
 *
 * 背景：关卡运行时匹配表由 LevelConfigSetup.SetupConfig 组装 =
 *   story matchlist（bundle18，除非 excludeStoryRecipeMatchList）
 * + includeRecipeMatchLists（按所选菜谱 DLC 自动并入对应 dlcNN matchlist 包装，
 *   现役包装在 Assets/commonW1/pseudo_prefab_so/core/matchlists/）
 * + allIngredients（LoadIngredientSo 自动填充：只搜 common03→common01→common02）
 * + optionalRecipeMatchListItems（DLC 原始菜谱 + hotdog/pizza 特例注册）
 *
 * 本脚本从 dump_bundle 实据 + 本地 SO 库盘点 + 菜谱知识三层交叉，
 * 产出 layout-editor/scripts/data/matchlist-mapping.json：
 *   matchlists  每个 matchlist 的 bundle/节点清单（pathID 精确解析，来自
 *               dump-bundle-all.py 重导 12 个 recipe bundle 后的 manifest pathID）
 *   wrappers    两套本地包装资产（commonW1 现役 / common02 遗留）与游戏
 *               bundle 实名的大小写敏感对账
 *   ingredients 每个食材 SO：所在库、DLC、被哪些 matchlist 覆盖（直接节点
 *               证据 vs 菜谱推导）、LoadIngredientSo 可达性、node 型特例
 *   recipes     每个菜谱：分组、所属 DLC、覆盖它的 matchlist、注册通道
 *   gaps        自动发现的缺口清单（commonW1-only 食材盲区等）
 *   levelSetUsage  各 LevelSet LevelInfo 实际引用的 matchlist 包装（GUID 对账）
 *
 * 只统计、不修改任何资产/代码（修复建议见 Docs/zh/matchlist-mapping.md）。
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, "../..");
const OUT_PATH = path.join(__dirname, "data", "matchlist-mapping.json");

// ---- JSON 大整数保真：pathID/fileID 超过 2^53，JSON.parse 会丢精度。
// 16 位以上整数字面量先文本级转字符串再解析，比较全部走字符串。----
function parseJsonBig(text) {
  return JSON.parse(text.replace(/([:\[]\s*)(-?\d{16,})(\s*[,\}\]\n])/g, '$1"$2"$3'));
}
const readJsonBig = (abs) => parseJsonBig(fs.readFileSync(abs, "utf8"));
// PPtr 兼容两种导出命名：UnityPy typetree（m_FileID/m_PathID）与 AssetRipper（fileID/guid/type）。
const refFileID = (ref) => String(ref.m_FileID ?? ref.fileID ?? "");
const refPathID = (ref) => {
  const v = ref.m_PathID ?? ref.pathID ?? ref.fileID;
  return v == null ? null : String(v);
};

const DUMP = path.join(repoRoot, "dump_bundle");
const MANIFEST = readJsonBig(path.join(DUMP, "manifest.json"));

const KNOWLEDGE = JSON.parse(
  fs.readFileSync(path.join(__dirname, "data", "recipe-knowledge.json"), "utf8")
);
const RECIPES_DIR = path.join(repoRoot, "layout-editor/web/public/recipes");

// ---- 本地食材 SO 库（LoadIngredientSo 只搜前三个，commonW1 是盲区）----
const INGREDIENT_LIBS = [
  { lib: "common03", root: "Assets/common03/food/Ingredients", loadOrder: 1 },
  { lib: "common01", root: "Assets/common01/food/Ingredients", loadOrder: 2 },
  { lib: "common02", root: "Assets/common02/food/Ingredients", loadOrder: 3 },
  { lib: "commonW1", root: "Assets/commonW1/pseudo_prefab_so", loadOrder: null }, // LoadIngredientSo 不搜
];

// ---- 编辑器规则镜像（LayoutEditorCatalogApi / RoastTrayFill）----
const DLC_RE = /\/(dlc\d{2})\//;
function dlcOfPath(p) {
  if (!p) return null;
  const m = p.match(DLC_RE);
  if (m) return m[1];
  if (p.includes("/combineddlc/")) return "combineddlc";
  return null;
}
function dlcOfId(id) {
  const m = (id || "").match(/^(?:dlc(\d{2})|DLC(\d{2}))[._-]/);
  return m ? `dlc${m[1] || m[2]}` : null;
}

// node 型食材（无 GameObject prefab，只能进 optionalRecipeMatchListItems，
// 不能进 allIngredients——宿主 GetIngredientOrItemOrderNode 会按 GameObject
// 加载而崩溃；LayoutEditorCatalogApi.AddHotdogCondiments 特例注册）。
const NODE_ONLY_INGREDIENTS = new Set([
  "DLC08_Ketchup", "DLC08_Mustard",
  "dlc11_ketchup", "dlc11_mustard",
  "boiledfrankfurter", "DLC11_BoiledFrankfurter", "dlc11_boiledfrankfurter",
]);

function walkAssets(relRoot) {
  const abs = path.join(repoRoot, relRoot);
  const out = [];
  if (!fs.existsSync(abs)) return out;
  const rec = (dir) => {
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      if (e.name.endsWith(".meta")) continue;
      const p = path.join(dir, e.name);
      if (e.isDirectory()) rec(p);
      else if (e.name.endsWith(".asset")) out.push(p);
    }
  };
  rec(abs);
  return out;
}
const relOf = (abs) => path.relative(repoRoot, abs).split(path.sep).join("/");

// ---- manifest 索引：bundle → pathID → entry；全局兜底（跨 bundle 引用）----
const byBundle = new Map();
const globalByPathId = new Map();
for (const o of MANIFEST.objects || []) {
  if (!o.bundle || o.pathID == null) continue;
  if (!byBundle.has(o.bundle)) byBundle.set(o.bundle, new Map());
  byBundle.get(o.bundle).set(String(o.pathID), o);
  const k = String(o.pathID);
  if (!globalByPathId.has(k)) globalByPathId.set(k, []);
  globalByPathId.get(k).push(o);
}
function resolveRef(bundle, fileID) {
  if (fileID == null) return { entry: null, scope: "unresolved" };
  const same = byBundle.get(bundle)?.get(String(fileID));
  if (same) return { entry: same, scope: "same-bundle" };
  const cands = globalByPathId.get(String(fileID)) || [];
  if (cands.length === 1) return { entry: cands[0], scope: "cross-bundle" };
  if (cands.length > 1) return { entry: null, scope: "ambiguous", candidates: cands.map((c) => c.bundle) };
  return { entry: null, scope: "unresolved" };
}

// ---- matchlist 实体（以 manifest 中的 recipematchlist container 为准）----
const mlEntries = (MANIFEST.objects || []).filter(
  (o) => o.container && /recipematchlist\.asset$/i.test(o.container)
);
// 同 bundle 可能有多份同名 container 对象（MonoScript 冒名 / 真 matchlist 的
// _2 副本）：真 matchlist 判定看键存在性（m_recipes/m_includeLists 数组键），
// 再按节点数挑最全的一份。
function readMlJson(entry) {
  return readJsonBig(path.join(DUMP, entry.file));
}
const mlByBundle = new Map();
{
  const best = new Map(); // bundle -> {entry, score}
  for (const e of mlEntries) {
    let score = -1;
    try {
      const mb = readMlJson(e);
      const isMl = Array.isArray(mb.m_recipes) || Array.isArray(mb.m_includeLists);
      score = (isMl ? 1000 : 0)
        + ((mb.m_recipes || []).length + (mb.m_includeLists || []).length);
    } catch {
      score = -1;
    }
    const prev = best.get(e.bundle);
    if (!prev || score > prev.score) best.set(e.bundle, { entry: e, score });
  }
  for (const [b, v] of best) mlByBundle.set(b, v.entry);
}
function containerTail(c) {
  return (c || "").split("/").pop().replace(/\.asset$/i, "");
}
function nodeKind(c) {
  const s = (c || "").toLowerCase();
  if (s.includes("/ingredients/")) return "ingredient";
  if (s.includes("/cookedingredients/")) return "cookedingredient";
  if (s.includes("/recipeitems/")) return "recipe";
  if (s.includes("/cookingstepdata/")) return "cookingstep";
  if (s.includes("orderdefinitions/")) return "other-order";
  return "other";
}

const ML_KEY_BY_CONTAINER_TAIL = {
  therecipematchlist: "story",
  dlc02_recipematchlist: "dlc02",
  dlc03_recipematchlist: "dlc03",
  dlc04_recipematchlist: "dlc04",
  dlc05_recipematchlist: "dlc05",
  dlc07_recipematchlist: "dlc07",
  dlc08_recipematchlist: "dlc08",
  dlc09_recipematchlist: "dlc09",
  dlc10_recipematchlist: "dlc10",
  dlc11_recipematchlist: "dlc11",
  dlc13_recipematchlist: "dlc13",
  combineddlc_recipematchlist: "combineddlc",
};

const matchlists = {};
{
  for (const entry of mlByBundle.values()) {
    const mb = readMlJson(entry);
    const key = ML_KEY_BY_CONTAINER_TAIL[containerTail(entry.container)];
    if (!key || matchlists[key]) continue;
    const nodes = (mb.m_recipes || []).map((ref) => {
      const pid = refPathID(ref);
      const ext = refFileID(ref) !== "0"; // m_FileID≠0 = 跨文件（bundle 依赖）引用
      const r = resolveRef(entry.bundle, pid);
      return {
        pathID: pid,
        external: ext,
        container: r.entry?.container ?? null,
        name: r.entry ? containerTail(r.entry.container) : null,
        kind: r.entry ? nodeKind(r.entry.container) : null,
        resolve: r.scope,
      };
    });
    const includes = (mb.m_includeLists || []).map((ref) => {
      const pid = refPathID(ref);
      const r = resolveRef(entry.bundle, pid);
      const incKey = r.entry ? ML_KEY_BY_CONTAINER_TAIL[containerTail(r.entry.container)] : null;
      return {
        pathID: pid,
        external: refFileID(ref) !== "0",
        container: r.entry?.container ?? null,
        matchlist: incKey,
        resolve: r.scope,
      };
    });
    matchlists[key] = {
      bundle: entry.bundle,
      container: entry.container,
      pathID: String(entry.pathID),
      nodeCount: nodes.length,
      nodes,
      includeLists: includes,
      unresolved: nodes.filter((n) => n.resolve !== "same-bundle" && n.resolve !== "cross-bundle").length,
    };
  }
}

// ---- 两套本地包装资产对账 ----
function readYamlFields(absPath, fields) {
  const txt = fs.readFileSync(absPath, "utf8");
  const out = {};
  for (const f of fields) {
    const m = txt.match(new RegExp(`^\\s*${f}:\\s*(.+)$`, "m"));
    out[f] = m ? m[1].trim() : null;
  }
  return out;
}
function guidOfMeta(assetRel) {
  const meta = fs.readFileSync(path.join(repoRoot, assetRel + ".meta"), "utf8");
  const m = meta.match(/^guid:\s*([0-9a-f]{32})/m);
  return m ? m[1] : null;
}
const wrappers = [];
function collectWrappers(rootRel, set) {
  for (const abs of walkAssets(rootRel)) {
    const rel = relOf(abs);
    const f = readYamlFields(abs, ["prefabName", "bundleName", "assetPath"]);
    if (!f.assetPath || !/recipematchlist/i.test(f.assetPath)) continue;
    const tail = containerTail(f.assetPath);
    const real = mlByBundle.get(f.bundleName);
    const realTail = real ? containerTail(real.container) : null;
    wrappers.push({
      set,
      asset: rel,
      guid: guidOfMeta(rel),
      bundleName: f.bundleName,
      assetPath: f.assetPath,
      // dump 的 container 是游戏 bundle 内实名（全小写）；本地包装 assetPath
      // 与之大小写敏感比对，不一致 = 运行时按大小写敏感查找会失败的风险点。
      caseMatchesBundleAsset: realTail ? realTail === tail : null,
      bundleAssetExists: !!real,
    });
  }
}
collectWrappers("Assets/commonW1/pseudo_prefab_so/core/matchlists", "commonW1(现役)");
collectWrappers("Assets/common02/food/RecipeMatchList", "common02(遗留)");

// ---- LevelInfo 实际引用（GUID 对账）----
const levelSetUsage = {};
{
  const setRoot = path.join(repoRoot, "Assets/LevelSets");
  if (fs.existsSync(setRoot)) {
    for (const set of fs.readdirSync(setRoot, { withFileTypes: true })) {
      if (!set.isDirectory()) continue;
      const infos = walkAssets(path.join("Assets/LevelSets", set.name)).filter((a) =>
        /LevelInfo_.*\.asset$/.test(path.basename(a))
      );
      for (const info of infos) {
        const txt = fs.readFileSync(info, "utf8");
        const refs = [];
        for (const w of wrappers) {
          if (w.guid && txt.includes(w.guid)) refs.push(w.set === "commonW1(现役)" ? mlKeyOfWrapper(w) : `common02:${path.basename(w.asset, ".asset")}`);
        }
        if (refs.length)
          levelSetUsage[`${set.name}/${path.basename(info, ".asset")}`] = refs;
      }
    }
  }
}
function mlKeyOfWrapper(w) {
  const tail = containerTail(w.assetPath);
  return ML_KEY_BY_CONTAINER_TAIL[tail.toLowerCase()] ?? tail;
}

// ---- 食材 SO 盘点 ----
const ingredients = [];
const idLowerToIng = new Map();
for (const { lib, root, loadOrder } of INGREDIENT_LIBS) {
  for (const abs of walkAssets(root)) {
    const rel = relOf(abs);
    // 只收食材目录（commonW1 根很大：限定路径含 /food/）
    const lower = rel.toLowerCase();
    if (lib === "commonW1" && !/\/food\//.test(lower)) continue;
    const id = path.basename(rel, ".asset");
    const dlc = dlcOfPath(rel) || dlcOfId(id) || (lib === "common01" ? null : null);
    ingredients.push({
      id,
      lib,
      dlc,
      soPath: rel,
      loadIngredientSoOrder: loadOrder,
      nodeTypeOnly: NODE_ONLY_INGREDIENTS.has(id),
    });
    const k = id.toLowerCase();
    if (!idLowerToIng.has(k)) idLowerToIng.set(k, []);
    idLowerToIng.get(k).push(ingredients[ingredients.length - 1]);
  }
}

// matchlist 节点名 → key（小写索引，直接覆盖证据）
const mlNodeIngredientIndex = new Map(); // nameLower -> [{ml, kind}]
for (const [key, ml] of Object.entries(matchlists)) {
  for (const n of ml.nodes) {
    if (!n.name) continue;
    const k = n.name.toLowerCase();
    if (!mlNodeIngredientIndex.has(k)) mlNodeIngredientIndex.set(k, []);
    mlNodeIngredientIndex.get(k).push({ ml: key, kind: n.kind });
  }
}

// ---- 菜谱层（public/recipes 分组 + recipe-knowledge 补充）----
const recipes = [];
const groupsSeen = new Set();
{
  const files = fs.existsSync(RECIPES_DIR)
    ? fs.readdirSync(RECIPES_DIR).filter((f) => f.endsWith(".json"))
    : [];
  for (const f of files) {
    const j = JSON.parse(fs.readFileSync(path.join(RECIPES_DIR, f), "utf8"));
    const group = j.group || path.basename(f, ".json");
    groupsSeen.add(group);
    for (const r of j.recipes || []) {
      recipes.push({
        id: r.id,
        group,
        dlc: group.startsWith("dlc") ? group : null,
        source: "catalog",
        ingredients: r.ingredients || [],
      });
    }
  }
}
const catalogIds = new Set(recipes.map((r) => r.id));
for (const r of KNOWLEDGE.recipes || []) {
  if (catalogIds.has(r.id)) continue;
  // 只补录 DLC 前缀条目（dlc09/dlc10 等不在 catalog 分组的知识菜谱）；
  // 其余 knowledge-only 多为 optional 变体/中间产物（knowledge skip 策略已注明），不出明细。
  const dlc = dlcOfId(r.id);
  if (!dlc) continue;
  recipes.push({
    id: r.id,
    group: null,
    dlc,
    source: "knowledge-only(不在目录)",
    ingredients: r.ingredients || [],
  });
}

// 菜谱食材引用索引：idLower -> recipes
const ingUseIndex = new Map();
for (const r of recipes) {
  for (const ing of r.ingredients) {
    const k = ing.toLowerCase();
    if (!ingUseIndex.has(k)) ingUseIndex.set(k, []);
    ingUseIndex.get(k).push(r);
  }
}

// ---- 汇总：每个食材的覆盖与可达性 ----
const gaps = [];
for (const ing of ingredients) {
  const k = ing.id.toLowerCase();
  const coverage = [];
  const via = [];
  for (const hit of mlNodeIngredientIndex.get(k) || []) {
    coverage.push(hit.ml);
    via.push({ matchlist: hit.ml, evidence: "bundle-node(直接)" });
  }
  for (const r of ingUseIndex.get(k) || []) {
    const ml = r.dlc || "story";
    if (!coverage.includes(ml)) {
      coverage.push(ml);
      via.push({ matchlist: ml, evidence: `recipe:${r.id}(推导)` });
    }
  }
  ing.matchlistCoverage = [...new Set(coverage)];
  ing.coverageVia = via;
  ing.allIngredientsReachable =
    ing.loadIngredientSoOrder != null && !ing.nodeTypeOnly;
  if (ing.lib === "commonW1") {
    gaps.push({
      type: "loadIngredientSo-blind",
      id: ing.id,
      detail: `SO 仅在 ${ing.soPath}，LoadIngredientSo 只搜 common03/01/02，allIngredients 自动填充会静默漏掉；` +
        (ing.matchlistCoverage.length
          ? `运行时覆盖依赖 ${ing.matchlistCoverage.join("/")} 的 matchlist 并入`
          : "且未发现任何 matchlist 覆盖"),
    });
  }
  if (ing.nodeTypeOnly && ing.matchlistCoverage.length === 0) {
    gaps.push({
      type: "node-only-unregistered",
      id: ing.id,
      detail: "node 型食材（无 prefab，只能走 optionalRecipeMatchListItems 特例注册），未发现 matchlist 直接覆盖——依赖 LayoutEditorCatalogApi hotdog 特例",
    });
  }
}

// common02 遗留包装大小写风险
for (const w of wrappers) {
  if (w.bundleAssetExists && w.caseMatchesBundleAsset === false) {
    gaps.push({
      type: "wrapper-case-mismatch",
      id: w.asset,
      detail: `包装 assetPath「${w.assetPath}」与 bundle 内实名（${w.bundleName}，全小写）大小写不一致；目前零引用（遗留资产），若启用需先对齐`,
    });
  }
  if (!w.bundleAssetExists) {
    gaps.push({
      type: "wrapper-bundle-missing",
      id: w.asset,
      detail: `bundleName ${w.bundleName} 在 dump 清单中不存在`,
    });
  }
}

// 目录组缺失的 DLC（recipes 分组无 dlc09/dlc10）
for (const key of Object.keys(matchlists)) {
  if (key === "story" || key === "combineddlc") continue;
  if (!groupsSeen.has(key)) {
    gaps.push({
      type: "recipe-group-missing",
      id: key,
      detail: `${key} matchlist 存在（${matchlists[key].nodeCount} 节点），但 web 目录 recipes 分组缺失（dlc09/dlc10 菜谱不在 palette 目录，走 recipe-knowledge-only）`,
    });
  }
}

// 汇总 recipe 注册通道
for (const r of recipes) {
  r.matchlist = r.dlc || "story";
  r.registration =
    r.dlc && r.dlc !== "combineddlc"
      ? "optionalRecipeMatchListItems(DLC原始菜谱自动) + includeRecipeMatchLists(按DLC)"
      : "story matchlist 运行时无条件并入";
}

const result = {
  generatedAt: new Date().toISOString(),
  note: "统计产物：食材→matchlist 映射与缺口清单。只读统计，不改动资产；修复建议见 Docs/zh/matchlist-mapping.md。",
  source: {
    manifestObjects: (MANIFEST.objects || []).length,
    matchlistEntries: mlByBundle.size,
    recipeKnowledge: (KNOWLEDGE.recipes || []).length,
    catalogGroups: [...groupsSeen],
  },
  matchlists,
  wrappers,
  levelSetUsage,
  ingredients,
  recipes,
  gaps,
};

fs.mkdirSync(path.dirname(OUT_PATH), { recursive: true });
fs.writeFileSync(OUT_PATH, JSON.stringify(result, null, 1));

// ---- 控制台摘要 ----
const libCount = {};
for (const i of ingredients) libCount[i.lib] = (libCount[i.lib] || 0) + 1;
console.log("=== matchlist-mapping 统计 ===");
console.log(`食材 SO: ${ingredients.length}（${Object.entries(libCount).map(([k, v]) => `${k} ${v}`).join(" / ")}）`);
console.log(`matchlist 实体: ${Object.keys(matchlists).length}`);
for (const [k, ml] of Object.entries(matchlists)) {
  const ing = ml.nodes.filter((n) => n.kind === "ingredient").length;
  console.log(`  ${k.padEnd(12)} ${String(ml.nodeCount).padStart(3)} 节点（食材 ${ing}）${ml.unresolved ? ` ⚠ ${ml.unresolved} 未解析` : ""}`);
}
console.log(`菜谱: ${recipes.length}（catalog ${recipes.filter((r) => r.source === "catalog").length} / knowledge-only ${recipes.filter((r) => r.source !== "catalog").length}）`);
console.log(`LevelInfo 引用: ${Object.keys(levelSetUsage).length} 个关卡`);
console.log(`缺口: ${gaps.length}`);
for (const g of gaps) console.log(`  [${g.type}] ${g.id}`);
console.log(`\n写出: ${path.relative(repoRoot, OUT_PATH)}`);
