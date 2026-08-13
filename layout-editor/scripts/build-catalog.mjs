#!/usr/bin/env node
/**
 * Scans common01/common02 assets and 使用手册.md for Chinese names.
 * Outputs (layout-editor/web/public/, synced to web/dist/):
 *   catalog.json         prefab layout palette
 *   ingredients.json     food ingredients (incl. dlc02/dlc05)
 *   recipes.json         recipes (original / custom / dlc, composition resolved)
 *   cooking-steps.json   cooking & plating steps
 *   audio-catalog.json   music / audio directories / death effects
 *   floor-materials.json swappable floor materials per level set
 * Recipe compositions come from data/recipe-knowledge.json (shared with the
 * Unity editor C# bridge) plus statically parsed CustomRecipeSO references.
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, "../..");

const PREFAB_ROOTS = [
  path.join(repoRoot, "Assets/common01/prefabs"),
  path.join(repoRoot, "Assets/common02/prefabs"),
];

const MANUAL_PATH = path.join(repoRoot, "使用手册.md");
const KNOWLEDGE_PATH = path.join(__dirname, "data", "recipe-knowledge.json");
const AUDIO_KNOWLEDGE_PATH = path.join(__dirname, "data", "audio-knowledge.json");
const DICTIONARY_PATH = path.join(__dirname, "data", "names-dictionary.json");
const OUT_DIR = path.join(repoRoot, "layout-editor/web/public");
const DIST_DIR = path.join(repoRoot, "layout-editor/web/dist");

/** Bumped when output schema / shared data files change; the Unity bridge reports
 *  its own version via /api/health so the web UI can warn about outdated bridges. */
const SCHEMA_VERSION = 3;

/** Unity script guids (Assets/Scripts/LevelEditorStub/*.cs.meta). */
const SCRIPT_GUID = {
  PseudoPrefabSO: "0cff7c13895ab9e47a5e02d4619cc3b9",
  PseudoPrefabSORecipe: "753d9e70603f6a140b05f30f176ec2dd",
  CustomRecipeSO: "83fb008bcc8e793429b02c178c430815",
  CustomRecipeOptionalPizzaSO: "60297950c88d0d646ac0eca5dc831262",
};

const FOOTPRINT_OVERRIDES = {
  ServingStation: { cellsX: 2, cellsZ: 1 },
  Sink: { cellsX: 2, cellsZ: 1 },
  SinkGlass: { cellsX: 2, cellsZ: 1 },
  // multi-cell decor (by naming convention: xN = N 格长, Nunit = N 格)
  crate_raft_x2_01: { cellsX: 2, cellsZ: 1 },
  Crate_raft_x3_01: { cellsX: 3, cellsZ: 1 },
  Crate_raft_x10_01: { cellsX: 10, cellsZ: 1 },
  barrier_rope_2unit_01: { cellsX: 2, cellsZ: 1 },
};

/** Measured native footprints for art decor prefabs, generated inside Unity via
 *  "Layout Editor/导出装饰实测尺寸" (LayoutEditorFootprintDump.cs). Keyed by prefab id. */
function loadMeasuredFootprints() {
  const file = path.join(__dirname, "data", "measured-footprints.json");
  if (!fs.existsSync(file)) {
    console.warn("WARN: measured-footprints.json missing; decor footprints fall back to 1x1.");
    return new Map();
  }
  try {
    const raw = JSON.parse(fs.readFileSync(file, "utf8"));
    const map = new Map();
    for (const it of raw.items || []) {
      if (it && it.id && it.cellsX > 0 && it.cellsZ > 0) {
        map.set(it.id, { cellsX: it.cellsX, cellsZ: it.cellsZ });
      }
    }
    console.log(`Measured footprints: ${map.size} entries`);
    return map;
  } catch (e) {
    console.warn(`WARN: measured-footprints.json unreadable: ${e.message}`);
    return new Map();
  }
}

const DEFAULT_PARENT = {
  counters: "Design/Counters",
  utensils: "Design/Utensils",
  mechanisms: "Design/Counters",
  Player: "Design/Chefs",
};

/** Core palette sections (strict order). */
const CORE_PALETTE = [
  { key: "counters/prep", labelZh: "核心 · 台面 / 准备", labelEn: "Countertops & prep" },
  { key: "counters/cooking", labelZh: "核心 · 烤制工作站", labelEn: "Cooking stations" },
  { key: "counters/sinks", labelZh: "核心 · 水槽 / 清洁", labelEn: "Sinks & cleaning" },
  { key: "counters/service", labelZh: "核心 · 出餐 / 加工", labelEn: "Serving & processing" },
  { key: "utensils/plates", labelZh: "核心 · 盘杯器具", labelEn: "Plates & glassware" },
  { key: "utensils/cooking", labelZh: "核心 · 锅具烤具", labelEn: "Cooking utensils" },
  { key: "utensils/mixing", labelZh: "核心 · 搅拌器具", labelEn: "Mixing & blending" },
  { key: "utensils/tools", labelZh: "核心 · 工具 / 其他", labelEn: "Tools & misc" },
  { key: "mechanisms", labelZh: "核心 · 机关", labelEn: "Mechanisms" },
  { key: "Player", labelZh: "核心 · 厨师出生点", labelEn: "Chef spawns" },
];

const ART_THEME_ZH = {
  npc: "NPC 角色",
  city_sushi: "寿司城市场景",
  camping: "露营",
  circus: "马戏团",
  wizard: "魔法学校",
  space: "太空",
  graveyard: "墓地",
  raft: "木筏",
  mine: "矿洞",
  throne: "王座",
  foodtruck: "餐车",
  air_balloon: "热气球",
  dlc03_christmas: "圣诞 DLC",
  dlc07_horde: "部落 DLC",
  dlc08_circus: "马戏团 DLC",
  dlc09_wonderland: "仙境 DLC",
};

/** Utensil stack rules (使用手册 §3.3, extended with DLC stations). */
const UTENSIL_STACK = {
  Plate: { y: 0.5, hostRule: "counter_standard" },
  CleanPlateStack: { y: 0.5, hostRule: "counter_standard" },
  Glass: { y: 0.5, hostRule: "counter_standard" },
  CleanGlassStack: { y: 0.5, hostRule: "counter_standard" },
  FireExtinguisher: { y: 1, hostRule: "counter_standard" },
  Flamethrower: { y: 1, hostRule: "counter_standard" },
  WaterGun: { y: 1, hostRule: "counter_standard" },
  Pot: { y: 0.6, hostRule: "cooker" },
  Steamer: { y: 0.6, hostRule: "cooker" },
  FryPan: { y: 0.6, hostRule: "cooker" },
  FrierBasket: { y: 0.6, hostRule: "frying_station" },
  MixerBowl: { y: 0.6, hostRule: "mixer" },
  BlenderCup: { y: 0.6, hostRule: "blender" },
  GriddlePan: { y: 0.6, hostRule: "campfire" },
  Skewer: { y: 1, hostRule: "barbeque" },
  ToastingFork: { y: 0.6, hostRule: "campfire" },
  Bellows: { y: 0.6, hostRule: "campfire" },
};

/** Utensil subcategory assignment for finer palette grouping. */
const UTENSIL_SUBCATEGORY = {
  Plate: "utensils/plates",
  Glass: "utensils/plates",
  CleanPlateStack: "utensils/plates",
  CleanGlassStack: "utensils/plates",

  Pot: "utensils/cooking",
  FryPan: "utensils/cooking",
  Steamer: "utensils/cooking",
  FrierBasket: "utensils/cooking",
  GriddlePan: "utensils/cooking",
  Skewer: "utensils/cooking",
  ToastingFork: "utensils/cooking",

  MixerBowl: "utensils/mixing",
  BlenderCup: "utensils/mixing",

  FireExtinguisher: "utensils/tools",
  Flamethrower: "utensils/tools",
  WaterGun: "utensils/tools",
  Bellows: "utensils/tools",
};

/** Counter subcategory assignment for finer palette grouping. */
const COUNTER_SUBCATEGORY = {
  Cooker: "counters/cooking",
  FryingStation: "counters/cooking",
  Oven: "counters/cooking",
  Barbeque: "counters/cooking",
  Campfire: "counters/cooking",

  Sink: "counters/sinks",
  SinkGlass: "counters/sinks",
  GlassReturn: "counters/sinks",
  PlateReturn: "counters/sinks",
  Bin: "counters/sinks",

  Counter: "counters/prep",
  CounterCorner: "counters/prep",
  ChoppingCounter: "counters/prep",

  Mixer: "counters/service",
  Blender: "counters/service",
  ServingStation: "counters/service",
  Dispenser: "counters/service",
  ConveyorStation: "counters/service",
};

/** Palette ordering: utensils cluster by paired workstation, counters by workflow. */
const HOST_SORT_ORDER = [
  "cooker",
  "frying_station",
  "mixer",
  "blender",
  "barbeque",
  "campfire",
  "counter_standard",
];

const COUNTER_SORT_ORDER = [
  "Counter",
  "CounterCorner",
  "ChoppingCounter",
  "Dispenser",
  "Cooker",
  "FryingStation",
  "Oven",
  "Mixer",
  "Blender",
  "Barbeque",
  "Campfire",
  "Sink",
  "SinkGlass",
  "Bin",
  "ServingStation",
  "PlateReturn",
  "GlassReturn",
  "ConveyorStation",
];

/** Recipe family derived from the recipe id (mirrors LayoutEditorCatalogApi.RecipeTypeOf). */
function recipeTypeOf(id) {
  const head = id.split("_")[0];
  const map = {
    Burger: "burger",
    Burrito: "burrito",
    Cake: "cake",
    Fry: "fry",
    Fried: "fry",
    Pasta: "pasta",
    Pizza: "pizza",
    Salad: "salad",
    Steamed: "steamed",
    Sushi: "sushi",
    Kebob: "kebab",
    Smoothie: "smoothie",
    Breakfast: "breakfast",
    Smores: "smores",
    Mixed: "batter",
    Mushroom: "pizza",
    Soup: "soup",
  };
  const mapped = map[head];
  if (mapped === "cake" && id.includes("Pancake")) return "pancake";
  if (mapped === "cake" && id === "Cake_Chocolate_SO") return "pancake";
  if (mapped === "cake" && id === "Cake_Plain_SO") return "pancake";
  if (mapped) return mapped;
  if (id.startsWith("Fried") || id.startsWith("Fry")) return "fry";
  if (id.startsWith("Mixed")) return "batter";
  if (id.includes("Pancake")) return "pancake";
  if (id.includes("Soup")) return "soup";
  return "other";
}

function readGuid(metaPath) {
  if (!fs.existsSync(metaPath)) return null;
  const text = fs.readFileSync(metaPath, "utf8");
  const m = text.match(/^guid:\s*([a-f0-9]+)/m);
  return m ? m[1] : null;
}

function parseManualTable(md) {
  const idToRow = new Map();
  const lines = md.split("\n");
  for (const line of lines) {
    if (!line.startsWith("|")) continue;
    if (line.includes("---|")) continue;
    const parts = line
      .split("|")
      .map((p) => p.trim())
      .filter((p) => p.length > 0);
    if (parts.length < 2) continue;
    const id = parts[0];
    const zh = parts[1];
    const en = parts.length >= 3 ? parts[2] : id;
    const loc = parts.length >= 4 ? parts[3] : "";
    const isGameplayPrefab = loc.includes("prefabs/");
    if (id === "ID" || id.includes("文件位置")) continue;
    if (/^[A-Za-z0-9_][A-Za-z0-9_.\- ]*$/.test(id) && zh && !zh.startsWith("`")) {
      const prev = idToRow.get(id);
      if (!prev || (isGameplayPrefab && !prev.fromPrefab)) {
        idToRow.set(id, { zh, en, fromPrefab: isGameplayPrefab });
      }
    }
  }
  return idToRow;
}

function categorize(assetPath) {
  const rel = assetPath.replace(/^Assets\/common0[12]\/prefabs\/?/, "");
  const seg = rel.split("/");
  const id = seg[seg.length - 1].replace(/\.prefab$/, "");
  if (seg[0] === "counters") {
    const sub = COUNTER_SUBCATEGORY[id] || "counters/service";
    return { category: sub, theme: null };
  }
  if (seg[0] === "utensils") {
    const sub = UTENSIL_SUBCATEGORY[id] || "utensils/tools";
    return { category: sub, theme: null };
  }
  if (seg[0] === "mechanisms") return { category: "mechanisms", theme: null };
  if (seg[0] === "Player" || rel === "Player.prefab") return { category: "Player", theme: null };
  if (seg[0] === "art" && seg.length > 1) return { category: "art", theme: seg[1] };
  if (seg[0] === "art") return { category: "art", theme: "misc" };
  return { category: "other", theme: seg[0] || "misc" };
}

function layoutMetaFor(id, category) {
  if (category === "art" || category === "other") {
    const surf = surfaceMeta(id);
    if (surf.surfaceTier) return { layoutTier: "floor", ...surf };
    return { layoutTier: "decor", ...surf };
  }
  const meta = { layoutTier: "core", ...surfaceMeta(id) };
  const stack = UTENSIL_STACK[id];
  if (stack) meta.stack = stack;
  return meta;
}

/** Floor/background classification for the floor layer. */
function surfaceMeta(id) {
  const lower = id.toLowerCase();
  let surfaceTier = null;
  let surfaceKind = null;

  if (/^sky$|background/.test(lower)) {
    surfaceTier = "background";
    surfaceKind = "background";
  } else if (id === "raft_water" || id === "alien_gue" || id === "sand_01") {
    // Theme background planes (water / alien goo / sand), placed as backdrops
    // and switched via the background-theme dropdown.
    surfaceTier = "background";
    surfaceKind = "background";
  } else if (id === "p_dlc5_camp_water" || id === "p_dlc5_camp_river") {
    // DLC5 camping water/river backdrop planes (analogous to raft_water).
    surfaceTier = "background";
    surfaceKind = "background";
  } else if (/^raft_raft_/.test(lower)) {
    surfaceTier = "floor";
    surfaceKind = "raft";
  } else if (/floor|carpet|blacktiles|walkway/.test(lower)) {
    surfaceTier = "floor";
    if (/ice/.test(lower)) surfaceKind = "ice";
    else if (/snow/.test(lower)) surfaceKind = "snow";
    else if (/sand/.test(lower)) surfaceKind = "sand";
    else if (/alien/.test(lower)) surfaceKind = "alien";
    else if (/walkway/.test(lower)) surfaceKind = "walkway";
    else if (/carpet/.test(lower)) surfaceKind = "carpet";
    else if (/p_floor_section/.test(lower)) surfaceKind = "section";
    else surfaceKind = "solid";
  } else if (id === "Travelator") {
    surfaceTier = "floor";
    surfaceKind = "conveyor";
  }

  const out = {};
  if (surfaceTier) out.surfaceTier = surfaceTier;
  if (surfaceKind) out.surfaceKind = surfaceKind;
  return out;
}


function walkPrefabs(dir, baseAssetsPath, out, measuredFootprints) {
  const baseAbs = path.resolve(repoRoot, baseAssetsPath);
  if (!fs.existsSync(dir)) return;
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    const stat = fs.statSync(full);
    if (stat.isDirectory()) {
      walkPrefabs(full, baseAssetsPath, out, measuredFootprints);
    } else if (name.endsWith(".prefab")) {
      const assetPath = path
        .join(baseAssetsPath, path.relative(baseAbs, path.resolve(full)))
        .replace(/\\/g, "/");
      const id = name.replace(/\.prefab$/, "");
      const guid = readGuid(full + ".meta");
      if (!guid) continue;
      const { category, theme } = categorize(assetPath);
      // Priority: hand-authored override > Unity-measured (decor only) > 1x1.
      const isDecorCategory = category === "art" || category === "other";
      const fp =
        FOOTPRINT_OVERRIDES[id] ||
        (isDecorCategory ? measuredFootprints.get(id) : undefined) ||
        { cellsX: 1, cellsZ: 1 };
      out.push({
        id,
        guid,
        assetPath,
        category,
        theme,
        defaultParent: DEFAULT_PARENT[category] || DEFAULT_PARENT[category.split("/")[0]] || "Art",
        footprint: fp,
        ...layoutMetaFor(id, category),
      });
    }
  }
}

function buildPaletteGroups(byCategory) {
  const groups = [];

  for (const def of CORE_PALETTE) {
    const list = byCategory[def.key] || [];
    if (list.length === 0) continue;
    groups.push({
      key: def.key,
      labelZh: def.labelZh,
      labelEn: def.labelEn,
      layoutTier: "core",
      itemCount: list.length,
    });
  }

  // Floor / background surfaces (aggregated across all categories/themes).
  const surfaceItems = [];
  for (const key of Object.keys(byCategory)) {
    for (const it of byCategory[key]) {
      if (it.surfaceTier) surfaceItems.push(it);
    }
  }
  if (surfaceItems.length > 0) {
    groups.push({
      key: "surface/floor",
      labelZh: "地板 / 背景",
      labelEn: "Floor & background surfaces",
      layoutTier: "floor",
      itemCount: surfaceItems.length,
    });
    byCategory["surface/floor"] = surfaceItems;
  }

  const artKeys = Object.keys(byCategory)
    .filter((k) => k.startsWith("art/"))
    .sort((a, b) => {
      const ta = a.slice(4);
      const tb = b.slice(4);
      if (ta === "npc") return -1;
      if (tb === "npc") return 1;
      return ta.localeCompare(tb);
    });

  for (const key of artKeys) {
    const theme = key.slice(4);
    const themeZh = ART_THEME_ZH[theme] || theme;
    const labelZh =
      theme === "npc" ? "装饰 · NPC 角色" : `装饰 · ${themeZh}`;
    groups.push({
      key,
      labelZh,
      labelEn: theme === "npc" ? "Decor · NPC" : `Decor · ${theme}`,
      layoutTier: "decor",
      itemCount: byCategory[key].length,
    });
  }

  const otherKeys = Object.keys(byCategory).filter(
    (k) => !k.startsWith("art/") && !k.startsWith("surface/") && !CORE_PALETTE.some((c) => c.key === k)
  );
  for (const key of otherKeys.sort()) {
    groups.push({
      key,
      labelZh: `其他 · ${key}`,
      labelEn: key,
      layoutTier: "decor",
      itemCount: byCategory[key].length,
    });
  }

  return groups;
}

function tidyCatalogNameZh(zh) {
  if (!zh) return zh;
  return zh.replace(/\s*[×xX]\d+\s*$/u, "").trim();
}

// ---------------------------------------------------------------------------
// Shared asset index + Unity YAML field parsing
// ---------------------------------------------------------------------------

function listFilesRecursive(dir, filter) {
  const out = [];
  if (!fs.existsSync(dir)) return out;
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    const stat = fs.statSync(full);
    if (stat.isDirectory()) out.push(...listFilesRecursive(full, filter));
    else if (filter(name)) out.push(full);
  }
  return out;
}

function toAssetPath(absPath) {
  return path.relative(repoRoot, absPath).replace(/\\/g, "/");
}

/** guid -> { id, assetPath } for every asset under the given roots. */
function buildGuidIndex(roots) {
  const index = new Map();
  for (const root of roots) {
    const abs = path.join(repoRoot, root);
    for (const meta of listFilesRecursive(abs, (n) => n.endsWith(".meta"))) {
      const guid = readGuid(meta);
      if (!guid) continue;
      const assetPath = toAssetPath(meta.replace(/\.meta$/, ""));
      index.set(guid, { id: path.basename(assetPath).replace(/\.[^.]+$/, ""), assetPath });
    }
  }
  return index;
}

const REF_RE = /\{fileID:\s*\d+,\s*guid:\s*([a-f0-9]+),/;

function parseRefGuid(line) {
  const m = line.match(REF_RE);
  return m ? m[1] : null;
}

/** Minimal field extractor for PseudoPrefabSO / recipe ScriptableObject YAML. */
function parseUnityAsset(filePath) {
  const text = fs.readFileSync(filePath, "utf8");
  const lines = text.split("\n");
  const out = { compositionGuids: [], optionalGuids: [] };
  let listTarget = null;
  for (const line of lines) {
    const trimmed = line.trim();
    let m;
    if ((m = trimmed.match(/^m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([a-f0-9]+)/))) {
      out.scriptGuid = m[1];
    } else if ((m = trimmed.match(/^(\w+):\s*(.*)$/))) {
      const key = m[1];
      const value = m[2];
      listTarget = null;
      if (key === "score") out.score = Number(value) || 0;
      else if (key === "uID") out.uID = Number(value) || 0;
      else if (key === "type") out.recipeType = Number(value) || 0;
      else if (key === "prefabName") out.prefabName = value;
      else if (key === "bundleName") out.bundleName = value;
      else if (key === "recipeName") out.recipeName = value;
      else if (key === "cookingStepSO") out.cookingStepGuid = parseRefGuid(value);
      else if (key === "platingStepSO") out.platingStepGuid = parseRefGuid(value);
      else if (key === "compositionSOs") listTarget = out.compositionGuids;
      else if (key === "optionalSOs") listTarget = out.optionalGuids;
    } else if (listTarget && trimmed.startsWith("- ")) {
      const guid = parseRefGuid(trimmed);
      if (guid) listTarget.push(guid);
    } else if (!trimmed.startsWith("-")) {
      listTarget = null;
    }
  }
  return out;
}

// ---------------------------------------------------------------------------
// Name fallbacks (mirror LayoutEditorManualLookup)
// ---------------------------------------------------------------------------

function fallbackNameZh(id) {
  if (!id) return "—";
  return id.endsWith("SO") ? id.slice(0, -2) : id;
}

function fallbackNameEn(id) {
  if (!id) return "—";
  const base = id.endsWith("SO") ? id.slice(0, -2) : id;
  return base.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function loadDictionary() {
  if (!fs.existsSync(DICTIONARY_PATH)) {
    console.warn(`WARN: dictionary file missing: ${DICTIONARY_PATH}`);
    return new Map();
  }
  const raw = JSON.parse(fs.readFileSync(DICTIONARY_PATH, "utf8"));
  const map = new Map();
  for (const entry of raw.names || []) {
    if (entry && entry.id) map.set(entry.id, { zh: entry.zh, en: entry.en });
  }
  return map;
}

/** Lookup order: names-dictionary.json -> 使用手册.md -> id fallback. */
function lookupName(dictionary, idToRow, id) {
  const dict = dictionary.get(id) || dictionary.get(id.replace(/SO$/, ""));
  if (dict?.zh) {
    // Dictionary entries are curated; do NOT strip "xN" (can be a real size tag like 1x28).
    return { nameZh: dict.zh, nameEn: dict.en || fallbackNameEn(id) };
  }
  const row = idToRow.get(id) || idToRow.get(id.replace(/SO$/, ""));
  return {
    nameZh: tidyCatalogNameZh(row?.zh || fallbackNameZh(id)),
    nameEn: row?.en || fallbackNameEn(id),
  };
}

/** "core" / "custom" / "dlc02" / "dlc05" from an asset path. */
function foodGroupOf(assetPath) {
  if (assetPath.includes("/CustomRecipes/")) return "custom";
  const m = assetPath.match(/\/(dlc\d+)\//);
  if (m) return m[1];
  return "core";
}

// ---------------------------------------------------------------------------
// Food scans
// ---------------------------------------------------------------------------

function scanIngredients(dictionary, idToRow) {
  const roots = ["Assets/common01/food/Ingredients", "Assets/common02/food/Ingredients"];
  const list = [];
  const seen = new Set();
  for (const root of roots) {
    for (const file of listFilesRecursive(path.join(repoRoot, root), (n) => n.endsWith(".asset"))) {
      const fields = parseUnityAsset(file);
      if (fields.scriptGuid !== SCRIPT_GUID.PseudoPrefabSO) continue;
      const guid = readGuid(file + ".meta");
      if (!guid || !seen.add(guid)) continue;
      const assetPath = toAssetPath(file);
      const id = path.basename(file, ".asset");
      list.push({
        guid,
        id,
        ...lookupName(dictionary, idToRow, id),
        assetPath,
        group: foodGroupOf(assetPath),
      });
    }
  }
  list.sort((a, b) => a.nameZh.localeCompare(b.nameZh));
  return list;
}

function loadKnowledge() {
  if (!fs.existsSync(KNOWLEDGE_PATH)) {
    console.warn(`WARN: knowledge file missing: ${KNOWLEDGE_PATH}`);
    return { recipes: {}, skip: [], cookSteps: [] };
  }
  const raw = JSON.parse(fs.readFileSync(KNOWLEDGE_PATH, "utf8"));
  const recipes = {};
  for (const entry of raw.recipes || []) recipes[entry.id] = entry;
  return {
    recipes,
    skip: new Set(raw.skip || []),
    cookSteps: new Set(raw.cookSteps || []),
  };
}

// Utensil / workstation sets per cooking step (mirrors StepUtensils in
// LayoutEditorRecipeKnowledge.cs). First entry = station, last = cooking vessel.
const STEP_UTENSILS = {
  Pot: ["Cooker", "Pot"],
  FryingPan: ["Cooker", "FryPan"],
  DeepFatFryer: ["FryingStation", "FrierBasket"],
  OvenTray: ["Oven"],
  Steamer: ["Cooker", "Steamer"],
  Mixer: ["Mixer", "MixerBowl", "Oven"],
  Blender: ["Blender", "BlenderCup"],
  GriddlePan: ["Campfire", "GriddlePan"],
  KebabSkewer: ["Barbeque", "Skewer"],
  ToastingFork: ["Campfire", "ToastingFork"],
  MixingBowl: ["Mixer", "MixerBowl"],
};

/** Recipe-book ingredient grouping (mirrors ComputeCookingGroups in LayoutEditorRecipeKnowledge.cs).
 *  Rules (evaluated in order):
 *  - 半成品：直接按自身烹饪步骤成组。
 *  - 煎锅中间产物覆盖：汉堡肉排 → FryingPan，面包胚/生菜保持生。
 *  - 寿司：只有寿司米蒸，其余不处理。
 *  - 卷饼：米饭煮，卷饼皮不处理，其余煎。
 *  - 棉花糖饼干：只有棉花糖烤，其余不处理。
 *  - 意面：意面煮，其余食材各自煎（分开成组）。
 *  - 炸物：所有食材分别炸（分开成组）。
 *  - 面糊/面团（含面粉）：面粉鸡蛋搅拌，其余进最终锅具；最终锅具图标作为标记组追加。 */
function computeCookingGroups(recipe, allRecipes, cookSteps) {
  const { cookingStep: finalStep, ingredients, type, intermediate } = recipe;
  if (!ingredients || !ingredients.length) return [];
  const isCookStep = (s) => !!s && cookSteps.has(s);

  if (intermediate) {
    const step = isCookStep(finalStep) ? finalStep : "";
    return [{ step, utensils: STEP_UTENSILS[step] || [], ingredients }];
  }

  // 自定义菜谱的"工序"分组：自身无整体烹饪步骤（Composite/组装型）时，按直接组成展开
  // —— 组成若是子菜谱（中间产物/半成品），以子菜谱自身的烹饪步骤成组并展开其叶食材；
  // 普通食材归生组。组成顺序即组顺序。（镜像 LayoutEditorRecipeKnowledge.cs）
  if (recipe.compositionIds && recipe.compositionIds.length > 0 && !isCookStep(finalStep)) {
    const byId = new Map(allRecipes.map((r) => [r.id, r]));
    const compResult = [];
    const compRaw = [];
    const compSteps = [];
    const compStepIngs = new Map();
    for (const compId of recipe.compositionIds) {
      const sub = byId.get(compId);
      if (sub && (sub.ingredients || []).length > 0) {
        const subStep = isCookStep(sub.cookingStep) ? sub.cookingStep : "";
        if (!subStep) {
          for (const ing of sub.ingredients) compRaw.push(ing);
          continue;
        }
        if (!compStepIngs.has(subStep)) {
          compStepIngs.set(subStep, []);
          compSteps.push(subStep);
        }
        for (const ing of sub.ingredients) compStepIngs.get(subStep).push(ing);
      } else {
        compRaw.push(compId);
      }
    }
    if (compRaw.length) compResult.push({ step: "", utensils: [], ingredients: compRaw });
    for (const st of compSteps) {
      compResult.push({ step: st, utensils: STEP_UTENSILS[st] || [], ingredients: compStepIngs.get(st) });
    }
    return compResult;
  }

  const prep = new Map(); // ingredient -> step ("" = raw)

  if (finalStep === "FryingPan") {
    const candidates = allRecipes
      .filter(
        (r) =>
          r.intermediate &&
          isCookStep(r.cookingStep) &&
          (r.cookingStep === "FryingPan" || r.cookingStep === "MixingBowl") &&
          (r.ingredients || []).length > 0 &&
          (r.ingredients || []).every((ing) => ingredients.includes(ing))
      )
      .sort((a, b) => b.ingredients.length - a.ingredients.length || a.id.localeCompare(b.id));
    for (const cand of candidates) {
      for (const ing of cand.ingredients) {
        if (!prep.has(ing)) prep.set(ing, cand.cookingStep);
      }
    }
  }

  let flourBranch = false;
  if (type === "sushi") {
    for (const ing of ingredients) {
      prep.set(ing, ing === "SushiRiceSO" || ing === "RiceSO" ? "Steamer" : "");
    }
  } else if (type === "burrito") {
    for (const ing of ingredients) {
      prep.set(ing, ing === "TortillaSO" ? "" : ing === "SushiRiceSO" || ing === "RiceSO" ? "Pot" : "FryingPan");
    }
  } else if (type === "smores") {
    for (const ing of ingredients) {
      prep.set(ing, ing === "DLC05_Marshmallow" ? "ToastingFork" : "");
    }
  } else if (finalStep === "Pot" && ingredients.includes("PastaSO")) {
    for (const ing of ingredients) {
      prep.set(ing, ing === "PastaSO" ? "Pot" : "FryingPan");
    }
  } else if (finalStep === "DeepFatFryer" || (type === "fry" && !isCookStep(finalStep))) {
    // 名称前缀推断的 fry（如 FriedEgg）不得覆盖显式烹饪步骤（FryingPan 等）
    for (const ing of ingredients) prep.set(ing, "DeepFatFryer");
  } else if (
    type === "cake" ||
    (ingredients.includes("FlourSO") && finalStep !== "Mixer" && finalStep !== "MixingBowl")
  ) {
    // 蛋糕：搅拌 + 烤箱；面糊/面团（松饼/饺子）：搅拌 + 最终锅具
    flourBranch = type !== "cake";
    const cookStep = type === "cake" ? "OvenTray" : isCookStep(finalStep) ? finalStep : "";
    for (const ing of ingredients) {
      if (!prep.has(ing)) {
        prep.set(ing, ing === "FlourSO" || ing === "EggSO" ? "MixingBowl" : cookStep);
      }
    }
  }

  const anyAssigned = [...prep.values()].some((s) => s !== "");
  const fallbackStep = anyAssigned ? "" : isCookStep(finalStep) ? finalStep : "";
  for (const ing of ingredients) {
    if (!prep.has(ing)) prep.set(ing, fallbackStep);
  }

  const groupMap = new Map();
  const order = [];
  for (const ing of ingredients) {
    const st = prep.get(ing);
    if (!groupMap.has(st)) {
      groupMap.set(st, []);
      order.push(st);
    }
    groupMap.get(st).push(ing);
  }

  const ordered = order.filter((s) => s !== "");
  if (flourBranch && isCookStep(finalStep) && !groupMap.has(finalStep) && order.length > 0) {
    ordered.push(finalStep);
  }

  const splitPerIngredient =
    finalStep === "DeepFatFryer" || (type === "fry" && !isCookStep(finalStep)) || (finalStep === "Pot" && ingredients.includes("PastaSO"));

  const result = [];
  if (groupMap.has("")) result.push({ step: "", utensils: [], ingredients: groupMap.get("") });
  for (const st of ordered) {
    const ings = groupMap.get(st) || [];
    if (splitPerIngredient && st !== "" && ings.length > 1) {
      for (const ing of ings) {
        result.push({ step: st, utensils: STEP_UTENSILS[st] || [], ingredients: [ing] });
      }
    } else {
      result.push({ step: st, utensils: STEP_UTENSILS[st] || [], ingredients: ings });
    }
  }
  return result;
}

function loadAudioKnowledge() {
  const empty = {
    baseBundles: ["bundle47"],
    alwaysLoadedBundles: ["bundle18"],
    mandatoryDirectoryIds: [],
    directoryEvents: [],
    themes: [],
    deathThemes: [],
    ambienceLabels: [],
    itemAudioRules: [],
  };
  if (!fs.existsSync(AUDIO_KNOWLEDGE_PATH)) {
    console.warn(`WARN: audio knowledge file missing: ${AUDIO_KNOWLEDGE_PATH}`);
    return empty;
  }
  try {
    const raw = JSON.parse(fs.readFileSync(AUDIO_KNOWLEDGE_PATH, "utf8"));
    return {
      baseBundles: raw.baseBundles || empty.baseBundles,
      alwaysLoadedBundles: raw.alwaysLoadedBundles || empty.alwaysLoadedBundles,
      mandatoryDirectoryIds: raw.mandatoryDirectoryIds || [],
      directoryEvents: raw.directoryEvents || [],
      themes: raw.themes || [],
      deathThemes: raw.deathThemes || [],
      ambienceLabels: raw.ambienceLabels || [],
      itemAudioRules: raw.itemAudioRules || [],
    };
  } catch (e) {
    console.warn(`WARN: audio knowledge file unreadable: ${e.message}`);
    return empty;
  }
}

function scanRecipes(dictionary, idToRow, guidIndex, knowledge) {
  const roots = [
    "Assets/common01/food/Recipes",
    "Assets/common01/food/CustomRecipes",
    "Assets/common02/food/Recipes",
  ];
  const customByGuid = new Map();
  const originals = [];
  for (const root of roots) {
    for (const file of listFilesRecursive(path.join(repoRoot, root), (n) => n.endsWith(".asset"))) {
      const fields = parseUnityAsset(file);
      const isCustom =
        fields.scriptGuid === SCRIPT_GUID.CustomRecipeSO ||
        fields.scriptGuid === SCRIPT_GUID.CustomRecipeOptionalPizzaSO;
      const isOriginal = fields.scriptGuid === SCRIPT_GUID.PseudoPrefabSORecipe;
      if (!isCustom && !isOriginal) continue;
      const guid = readGuid(file + ".meta");
      if (!guid) continue;
      const entry = { file, guid, id: path.basename(file, ".asset"), assetPath: toAssetPath(file), fields, isCustom };
      if (isCustom) customByGuid.set(guid, entry);
      else originals.push(entry);
    }
  }

  const list = [];
  const skipped = [];

  for (const entry of customByGuid.values()) {
    const { fields } = entry;
    const ingredientIds = [];
    const stats = { ings: 0, cooks: 0 };
    const seenCustom = new Set();
    const expand = (guid) => {
      const sub = customByGuid.get(guid);
      if (sub) {
        if (seenCustom.has(guid)) return;
        seenCustom.add(guid);
        const stepId = sub.fields.cookingStepGuid ? guidIndex.get(sub.fields.cookingStepGuid)?.id || "" : "";
        if (sub.fields.cookingStepGuid && knowledge.cookSteps.has(stepId)) stats.cooks++;
        for (const g of sub.fields.compositionGuids) expand(g);
        return;
      }
      stats.ings++;
      const leaf = guidIndex.get(guid);
      const leafId = leaf ? leaf.id : guid;
      ingredientIds.push(leafId);
    };
    if (fields.cookingStepGuid) {
      const stepId = guidIndex.get(fields.cookingStepGuid)?.id || "";
      if (knowledge.cookSteps.has(stepId)) stats.cooks++;
    }
    seenCustom.add(entry.guid);
    for (const g of fields.compositionGuids) expand(g);

    const step = fields.cookingStepGuid ? guidIndex.get(fields.cookingStepGuid)?.id || "" : "";
    const ingredientCount = stats.ings > 0 ? stats.ings : ingredientIds.length;
    list.push({
      guid: entry.guid,
      id: entry.id,
      ...lookupName(dictionary, idToRow, entry.id),
      assetPath: entry.assetPath,
      cookingStep: step,
      ingredients: ingredientIds,
      compositionIds: (fields.compositionGuids || []).map((g) => guidIndex.get(g)?.id || g),
      ingredientCount,
      cookingStepCount: stats.cooks,
      score: fields.score || 0,
      isCustom: true,
      group: foodGroupOf(entry.assetPath),
      type: recipeTypeOf(entry.id),
      intermediate: (fields.score || 0) <= 0,
    });
  }

  for (const entry of originals) {
    const { fields, id } = entry;
    if (knowledge.skip.has(id) || knowledge.skip.has(fields.prefabName)) {
      skipped.push(`${id} (knowledge skip)`);
      continue;
    }
    const k = knowledge.recipes[id] || knowledge.recipes[`${fields.prefabName}_SO`];
    if (!k) {
      skipped.push(`${id} (no composition info)`);
      continue;
    }
    const step = k.step || "";
    const ings = k.ingredients || [];
    for (const ing of ings) {
      if (![...guidIndex.values()].some((v) => v.id === ing))
        console.warn(`WARN: knowledge ${id}: unknown ingredient id ${ing}`);
    }
    list.push({
      guid: entry.guid,
      id,
      ...lookupName(dictionary, idToRow, id),
      assetPath: entry.assetPath,
      cookingStep: step,
      ingredients: ings,
      ingredientCount: ings.length,
      cookingStepCount: step && knowledge.cookSteps.has(step) ? 1 : 0,
      score: fields.score || 0,
      isCustom: false,
      group: foodGroupOf(entry.assetPath),
      type: recipeTypeOf(id),
      intermediate: (fields.score || 0) <= 0,
    });
  }

  for (const r of list) {
    r.cookingGroups = computeCookingGroups(r, list, knowledge.cookSteps);
  }

  list.sort((a, b) => a.nameZh.localeCompare(b.nameZh));
  return { list, skipped };
}

function scanCookingSteps(dictionary, idToRow) {
  const dirs = [
    { root: "Assets/common01/food/CookingSteps", kind: "cooking" },
    { root: "Assets/common02/food/CookingSteps", kind: "cooking" },
    { root: "Assets/common01/food/PlatingSteps", kind: "plating" },
    { root: "Assets/common02/food/PlatingSteps", kind: "plating" },
  ];
  const list = [];
  const seen = new Set();
  for (const { root, kind } of dirs) {
    for (const file of listFilesRecursive(path.join(repoRoot, root), (n) => n.endsWith(".asset"))) {
      const fields = parseUnityAsset(file);
      if (fields.scriptGuid !== SCRIPT_GUID.PseudoPrefabSO) continue;
      const guid = readGuid(file + ".meta");
      if (!guid || !seen.add(guid)) continue;
      const id = path.basename(file, ".asset");
      list.push({
        guid,
        id,
        ...lookupName(dictionary, idToRow, id),
        kind,
        assetPath: toAssetPath(file),
        bundleName: fields.bundleName || "",
        group: foodGroupOf(toAssetPath(file)),
      });
    }
  }
  list.sort((a, b) => a.kind.localeCompare(b.kind) || a.id.localeCompare(b.id));
  return list;
}

// ---------------------------------------------------------------------------
// Audio + floor material scans
// ---------------------------------------------------------------------------

function scanPseudoSoFolder(roots) {
  const list = [];
  const seen = new Set();
  for (const root of roots) {
    for (const file of listFilesRecursive(path.join(repoRoot, root), (n) => n.endsWith(".asset"))) {
      const guid = readGuid(file + ".meta");
      if (!guid || !seen.add(guid)) continue;
      const fields = parseUnityAsset(file);
      list.push({
        guid,
        id: path.basename(file, ".asset"),
        assetPath: toAssetPath(file),
        bundleName: fields.bundleName || "",
      });
    }
  }
  return list;
}

function scanAudioCatalog(dictionary, idToRow) {
  const musicRoots = [
    "Assets/common01/pseudo_prefab_so/audio/music",
    "Assets/common02/pseudo_prefab_so/audio/music",
  ];
  const dirRoots = [
    "Assets/common01/pseudo_prefab_so/audio/AudioDirectories",
    "Assets/common02/pseudo_prefab_so/audio/AudioDirectories",
  ];
  const soRoots = ["Assets/common01/pseudo_prefab_so", "Assets/common02/pseudo_prefab_so"];

  const music = scanPseudoSoFolder(musicRoots).map((m) => {
    const names = lookupName(dictionary, idToRow, m.id);
    return {
      guid: m.guid,
      id: m.id,
      assetPath: m.assetPath,
      bundleName: m.bundleName,
      nameZh: names.nameZh,
      nameEn: names.nameEn,
    };
  });
  music.sort((a, b) => a.id.localeCompare(b.id));

  const audioDirectories = scanPseudoSoFolder(dirRoots).map((m) => {
    const names = lookupName(dictionary, idToRow, m.id);
    return {
      guid: m.guid,
      id: m.id,
      assetPath: m.assetPath,
      bundleName: m.bundleName,
      nameZh: names.nameZh,
      nameEn: names.nameEn,
    };
  });
  audioDirectories.sort((a, b) => a.id.localeCompare(b.id));

  const deathEffects = scanPseudoSoFolder(soRoots)
    .filter((m) => m.id.includes("WaterSplash") || m.id.includes("DeathEffect"))
    .map((m) => {
      const names = lookupName(dictionary, idToRow, m.id);
      return {
        guid: m.guid,
        id: m.id,
        assetPath: m.assetPath,
        nameZh: names.nameZh,
        nameEn: names.nameEn,
      };
    });
  deathEffects.sort((a, b) => a.id.localeCompare(b.id));

  return { music, audioDirectories, deathEffects };
}

const SIZE_TAG_RE = /_(\d+)x(\d+)(?:_|$)/;

function floorRelevance(id) {
  const n = id.toLowerCase();
  if (n.includes("floor")) return 100;
  if (n.includes("raft")) return 95;
  if (n.includes("blacktiles") || n.includes("carpet")) return 90;
  if (n.includes("path")) return 80;
  if (n.includes("sky") || n.includes("background")) return 70;
  return 0;
}

// ---------------------------------------------------------------------------
// Counter appearance catalog
// ---------------------------------------------------------------------------

const THEME_NAMES_ZH = {
  "": "默认",
  Camping: "露营",
  Camping1: "露营 1",
  Camping2: "露营 2",
  Circus: "马戏团",
  Circus01: "马戏团 1",
  Circus02: "马戏团 2",
  Dark: "暗黑",
  Edge: "边缘",
  Gold: "黄金",
  Old: "旧式",
  Space: "太空",
  Space02: "太空 2",
  Slim: "窄式",
  SlimNoBlock: "窄式·无障碍",
  Airballoon: "热气球",
  Wizard: "魔法",
  Beach: "海滩",
  Beach01: "海滩 1",
  Beach02: "海滩 2",
  BeachGlass: "海滩·玻璃",
};

const COUNTER_TYPE_NAMES_ZH = {
  Counter: "普通桌台",
  CounterCorner: "角落桌台",
  ChoppingCounter: "切菜台",
  Sink: "洗碗池",
  ServingStation: "上菜口",
  Dispenser: "食材箱",
  Bin: "垃圾桶",
  Cooker: "灶台",
  FryingStation: "油炸台",
  Oven: "烤箱",
  Mixer: "搅拌台",
  PlateReturn: "脏盘台",
  ConveyorStation: "传送带",
};

function scanCounterAppearances(dictionary, idToRow) {
  const roots = [
    "Assets/common01/pseudo_prefab_so/counters",
    "Assets/common02/pseudo_prefab_so/counters",
  ];
  const byType = {};
  const seen = new Set();

  for (const root of roots) {
    for (const file of listFilesRecursive(path.join(repoRoot, root), (n) => n.endsWith(".asset"))) {
      const guid = readGuid(file + ".meta");
      if (!guid || !seen.add(guid)) continue;
      const fields = parseUnityAsset(file);
      if (fields.scriptGuid !== SCRIPT_GUID.PseudoPrefabSO) continue;
      const id = path.basename(file, ".asset");

      let counterType = null;
      const sortedTypes = Object.keys(COUNTER_TYPE_NAMES_ZH).sort((a, b) => b.length - a.length);
      for (const ct of sortedTypes) {
        if (id.startsWith(ct)) {
          counterType = ct;
          break;
        }
      }
      if (!counterType) continue;

      const themeSuffix = id.slice(counterType.length).replace(/SO$/, "");
      const themeName = THEME_NAMES_ZH[themeSuffix] ?? themeSuffix;

      if (!byType[counterType]) byType[counterType] = [];
      const dict = dictionary.get(id);
      const enDefault = `${counterType} ${themeSuffix || "Default"}`.replace(/([a-z])([A-Z])/g, "$1 $2").trim();
      const zhDefault = `${COUNTER_TYPE_NAMES_ZH[counterType] || counterType} · ${themeName}`;
      const nameZh = dict?.zh || zhDefault;
      const nameEn = dict?.en || enDefault;
      const displayZh = `${nameZh}（${nameEn}）`;
      byType[counterType].push({
        guid,
        id,
        assetPath: toAssetPath(file),
        nameZh: displayZh,
        nameEn,
        theme: themeSuffix,
        themeName,
      });
    }
  }

  for (const ct of Object.keys(byType)) {
    byType[ct].sort((a, b) => a.nameZh.localeCompare(b.nameZh));
  }
  return byType;
}

const SWITCH_MATERIAL_NAMES_ZH = {
  Grey: "灰色",
  mat_sk_switch_01: "开关材质 01",
  mat_sk_switch_02: "开关材质 02",
  mat_sk_switch_unoccupied: "未占用开关材质",
};

function scanSwitchMaterials(dictionary, idToRow) {
  const roots = [
    "Assets/common01/pseudo_prefab_so/mechanisms/Switch",
    "Assets/common02/pseudo_prefab_so/mechanisms/Switch",
  ];
  const list = [];
  const seen = new Set();

  for (const root of roots) {
    if (!fs.existsSync(path.join(repoRoot, root))) continue;
    for (const file of listFilesRecursive(path.join(repoRoot, root), (n) => n.endsWith(".asset"))) {
      const guid = readGuid(file + ".meta");
      if (!guid || !seen.add(guid)) continue;
      const fields = parseUnityAsset(file);
      if (fields.scriptGuid !== SCRIPT_GUID.PseudoPrefabSO) continue;
      const id = path.basename(file, ".asset");
      const dict = dictionary.get(id);
      const swZh = SWITCH_MATERIAL_NAMES_ZH[id];
      const nameZh = dict?.zh || swZh || fallbackNameZh(id);
      const nameEn = dict?.en || fallbackNameEn(id);
      const displayZh = `${nameZh}（${nameEn}）`;
      list.push({
        guid,
        id,
        assetPath: toAssetPath(file),
        nameZh: displayZh,
        nameEn,
      });
    }
  }
  list.sort((a, b) => a.nameZh.localeCompare(b.nameZh));
  return list;
}

function scanFloorMaterials(dictionary, idToRow) {
  const roots = [];
  const setsRoot = path.join(repoRoot, "Assets/LevelSets");
  if (fs.existsSync(setsRoot)) {
    for (const setName of fs.readdirSync(setsRoot)) {
      const matDir = path.join(setsRoot, setName, "materials");
      if (fs.existsSync(matDir) && fs.statSync(matDir).isDirectory())
        roots.push({ root: matDir, source: setName });
    }
  }
  for (const shared of ["Assets/common01/materials", "Assets/common02/materials"]) {
    const abs = path.join(repoRoot, shared);
    if (fs.existsSync(abs)) roots.push({ root: abs, source: path.basename(path.dirname(shared)) });
  }

  const list = [];
  const seen = new Set();
  for (const { root, source } of roots) {
    for (const file of listFilesRecursive(root, (n) => n.endsWith(".mat"))) {
      const guid = readGuid(file + ".meta");
      if (!guid || !seen.add(guid)) continue;
      const id = path.basename(file, ".mat");
      const size = id.match(SIZE_TAG_RE);
      const names = lookupName(dictionary, idToRow, id);
      list.push({
        guid,
        id,
        assetPath: toAssetPath(file),
        nameZh: names.nameZh,
        nameEn: names.nameEn,
        sizeTag: size ? `${size[1]}x${size[2]}` : "",
        source,
      });
    }
  }
  list.sort((a, b) => floorRelevance(b.id) - floorRelevance(a.id) || a.nameZh.localeCompare(b.nameZh));
  return list;
}

// ---------------------------------------------------------------------------

/** Stamps `icon: true` on entries that have an extracted PNG under web/public/icons/<sub>/<id>.png.
 *  Icons are produced by scripts/extract-icons.py (run separately). */
function stampIcons(list, sub) {
  const dir = path.join(OUT_DIR, "icons", sub);
  let count = 0;
  for (const it of list) {
    const has = !!it.id && fs.existsSync(path.join(dir, it.id + ".png"));
    it.icon = has;
    if (has) count++;
  }
  if (list.length) console.log(`Icons: ${count}/${list.length} ${sub} have a PNG`);
}

function writeCatalogFile(fileName, payload) {
  const outPath = path.join(OUT_DIR, fileName);
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, JSON.stringify(payload, null, 2), "utf8");
  console.log(`Wrote ${payload.itemCount ?? "?"} entries to ${outPath}`);
  if (fs.existsSync(DIST_DIR)) {
    fs.copyFileSync(outPath, path.join(DIST_DIR, fileName));
    console.log(`Synced ${fileName} to ${DIST_DIR}`);
  }
}

function main() {
  const manual = fs.existsSync(MANUAL_PATH) ? fs.readFileSync(MANUAL_PATH, "utf8") : "";
  const idToRow = parseManualTable(manual);
  const dictionary = loadDictionary();

  const items = [];
  const measuredFootprints = loadMeasuredFootprints();
  for (const root of PREFAB_ROOTS) {
    const base = root.includes("common01") ? "Assets/common01/prefabs" : "Assets/common02/prefabs";
    walkPrefabs(root, base, items, measuredFootprints);
  }

  for (const item of items) {
    const names = lookupName(dictionary, idToRow, item.id);
    item.nameZh = names.nameZh;
    item.nameEn = names.nameEn;
  }

  items.sort((a, b) => {
    const c = a.category.localeCompare(b.category);
    if (c !== 0) return c;
    if (a.category === "utensils" || a.category.startsWith("utensils/")) {
      const ha = HOST_SORT_ORDER.indexOf(a.stack?.hostRule ?? "counter_standard");
      const hb = HOST_SORT_ORDER.indexOf(b.stack?.hostRule ?? "counter_standard");
      if (ha !== hb) return ha - hb;
    }
    if (a.category === "counters" || a.category.startsWith("counters/")) {
      const ca = COUNTER_SORT_ORDER.indexOf(a.id);
      const cb = COUNTER_SORT_ORDER.indexOf(b.id);
      if (ca !== cb) return (ca < 0 ? 99 : ca) - (cb < 0 ? 99 : cb);
    }
    return a.id.localeCompare(b.id);
  });

  const byCategory = {};
  for (const item of items) {
    const key = item.category === "art" && item.theme ? `art/${item.theme}` : item.category;
    if (!byCategory[key]) byCategory[key] = [];
    byCategory[key].push(item);
  }

  // Floor/background surface items belong ONLY to the floor palette, not their decor group.
  const surfaceItems = [];
  for (const key of Object.keys(byCategory)) {
    byCategory[key] = byCategory[key].filter((it) => {
      if (it.surfaceTier) {
        surfaceItems.push(it);
        return false;
      }
      return true;
    });
  }
  if (surfaceItems.length > 0) byCategory["surface/floor"] = surfaceItems;

  const paletteGroups = buildPaletteGroups(byCategory);

  stampIcons(items, "catalog");
  writeCatalogFile("catalog.json", {
    generatedAt: new Date().toISOString(),
    schemaVersion: SCHEMA_VERSION,
    gridCellSize: 1.2,
    itemCount: items.length,
    items,
    byCategory,
    paletteGroups,
  });

  const guidIndex = buildGuidIndex([
    "Assets/common01/food",
    "Assets/common02/food",
  ]);
  const knowledge = loadKnowledge();

  const ingredients = scanIngredients(dictionary, idToRow);
  stampIcons(ingredients, "ingredients");
  writeCatalogFile("ingredients.json", {
    generatedAt: new Date().toISOString(),
    schemaVersion: SCHEMA_VERSION,
    itemCount: ingredients.length,
    ingredients,
  });

  const { list: recipes, skipped } = scanRecipes(dictionary, idToRow, guidIndex, knowledge);
  stampIcons(recipes, "recipes");
  writeCatalogFile("recipes.json", {
    generatedAt: new Date().toISOString(),
    schemaVersion: SCHEMA_VERSION,
    itemCount: recipes.length,
    recipes,
  });
  if (skipped.length > 0) console.log(`Skipped ${skipped.length} recipes:\n  ${skipped.join("\n  ")}`);

  const cookingSteps = scanCookingSteps(dictionary, idToRow);
  writeCatalogFile("cooking-steps.json", {
    generatedAt: new Date().toISOString(),
    schemaVersion: SCHEMA_VERSION,
    itemCount: cookingSteps.length,
    cookingSteps,
  });

  const audio = scanAudioCatalog(dictionary, idToRow);
  const audioKnowledge = loadAudioKnowledge();
  writeCatalogFile("audio-catalog.json", {
    generatedAt: new Date().toISOString(),
    schemaVersion: SCHEMA_VERSION,
    itemCount: audio.music.length + audio.audioDirectories.length + audio.deathEffects.length,
    music: audio.music,
    audioDirectories: audio.audioDirectories,
    deathEffects: audio.deathEffects,
    baseBundles: audioKnowledge.baseBundles,
    alwaysLoadedBundles: audioKnowledge.alwaysLoadedBundles,
    mandatoryDirectoryIds: audioKnowledge.mandatoryDirectoryIds,
    directoryEvents: audioKnowledge.directoryEvents,
    themes: audioKnowledge.themes,
    deathThemes: audioKnowledge.deathThemes,
    ambienceLabels: audioKnowledge.ambienceLabels,
    itemAudioRules: audioKnowledge.itemAudioRules ?? [],
  });

  const materials = scanFloorMaterials(dictionary, idToRow);
  writeCatalogFile("floor-materials.json", {
    generatedAt: new Date().toISOString(),
    schemaVersion: SCHEMA_VERSION,
    itemCount: materials.length,
    materials,
  });

  const counterAppearances = scanCounterAppearances(dictionary, idToRow);
  let caCount = 0;
  for (const v of Object.values(counterAppearances)) caCount += v.length;
  console.log(`Counter appearance options: ${caCount} across ${Object.keys(counterAppearances).length} types`);
  writeCatalogFile("counter-appearances.json", {
    generatedAt: new Date().toISOString(),
    schemaVersion: SCHEMA_VERSION,
    typeNames: COUNTER_TYPE_NAMES_ZH,
    themeNames: THEME_NAMES_ZH,
    byType: counterAppearances,
  });

  const switchMaterials = scanSwitchMaterials(dictionary, idToRow);
  console.log(`Switch materials: ${switchMaterials.length}`);
  writeCatalogFile("switch-materials.json", {
    generatedAt: new Date().toISOString(),
    schemaVersion: SCHEMA_VERSION,
    materials: switchMaterials,
  });

  // Copy the AssetBundle dependency graph (extracted from the game manifest) so the web UI can
  // compute transitive closures client-side for accurate BGM bundle-missing warnings.
  const manifestSrc = path.join(__dirname, "data", "bundle-manifest.json");
  if (fs.existsSync(manifestSrc)) {
    for (const dir of [OUT_DIR, DIST_DIR]) {
      if (!fs.existsSync(dir)) continue;
      fs.copyFileSync(manifestSrc, path.join(dir, "bundle-manifest.json"));
    }
    console.log(`Copied bundle-manifest.json to ${path.basename(OUT_DIR)}`);
  }
}

main();
