#!/usr/bin/env node
/**
 * Scans common01/common02 prefabs and 使用手册.md for Chinese names.
 * Output: layout-editor/web/public/catalog.json
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
const OUT_PATH = path.join(repoRoot, "layout-editor/web/public/catalog.json");

const FOOTPRINT_OVERRIDES = {
  ServingStation: { cellsX: 2, cellsZ: 1 },
  Sink: { cellsX: 2, cellsZ: 1 },
};

const DEFAULT_PARENT = {
  counters: "Design/Counters",
  utensils: "Design/Utensils",
  mechanisms: "Design/Counters",
  Player: "Design/Chefs",
};

/** Core palette sections (strict order). */
const CORE_PALETTE = [
  { key: "counters", labelZh: "核心 · 桌台 / 工作站", labelEn: "Counters & stations" },
  { key: "utensils", labelZh: "核心 · 厨房器具（叠放在桌台上）", labelEn: "Utensils (stack on counters)" },
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

/** Utensil stack rules (使用手册 §3.3). */
const UTENSIL_STACK = {
  Plate: { y: 1, hostRule: "counter_standard" },
  CleanPlateStack: { y: 1, hostRule: "counter_standard" },
  FireExtinguisher: { y: 1, hostRule: "counter_standard" },
  Flamethrower: { y: 1, hostRule: "counter_standard" },
  Pot: { y: 0.6, hostRule: "cooker" },
  Steamer: { y: 0.6, hostRule: "cooker" },
  FryPan: { y: 0.6, hostRule: "cooker" },
  FrierBasket: { y: 0.6, hostRule: "frying_station" },
  MixerBowl: { y: 0.6, hostRule: "mixer" },
};

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
  if (seg[0] === "counters") return { category: "counters", theme: null };
  if (seg[0] === "utensils") return { category: "utensils", theme: null };
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
  } else if (id === "raft_water" || id === "alien_gue") {
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

function walkPrefabs(dir, baseAssetsPath, out) {
  const baseAbs = path.resolve(repoRoot, baseAssetsPath);
  if (!fs.existsSync(dir)) return;
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    const stat = fs.statSync(full);
    if (stat.isDirectory()) {
      walkPrefabs(full, baseAssetsPath, out);
    } else if (name.endsWith(".prefab")) {
      const assetPath = path
        .join(baseAssetsPath, path.relative(baseAbs, path.resolve(full)))
        .replace(/\\/g, "/");
      const id = name.replace(/\.prefab$/, "");
      const guid = readGuid(full + ".meta");
      if (!guid) continue;
      const { category, theme } = categorize(assetPath);
      const fp = FOOTPRINT_OVERRIDES[id] || { cellsX: 1, cellsZ: 1 };
      out.push({
        id,
        guid,
        assetPath,
        category,
        theme,
        defaultParent: DEFAULT_PARENT[category] || "Art",
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

function main() {
  const manual = fs.existsSync(MANUAL_PATH) ? fs.readFileSync(MANUAL_PATH, "utf8") : "";
  const idToRow = parseManualTable(manual);

  const items = [];
  for (const root of PREFAB_ROOTS) {
    const base = root.includes("common01") ? "Assets/common01/prefabs" : "Assets/common02/prefabs";
    walkPrefabs(root, base, items);
  }

  for (const item of items) {
    const row = idToRow.get(item.id) || idToRow.get(item.id.replace(/SO$/, ""));
    item.nameZh = tidyCatalogNameZh(row?.zh || item.id);
    item.nameEn = row?.en || item.id;
  }

  items.sort((a, b) => {
    const c = a.category.localeCompare(b.category);
    if (c !== 0) return c;
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

  const catalog = {
    generatedAt: new Date().toISOString(),
    gridCellSize: 1.2,
    itemCount: items.length,
    items,
    byCategory,
    paletteGroups,
  };

  fs.mkdirSync(path.dirname(OUT_PATH), { recursive: true });
  fs.writeFileSync(OUT_PATH, JSON.stringify(catalog, null, 2), "utf8");
  console.log(`Wrote ${items.length} prefabs to ${OUT_PATH}`);

  const distDir = path.join(repoRoot, "layout-editor/web/dist");
  if (fs.existsSync(distDir)) {
    const distCatalog = path.join(distDir, "catalog.json");
    fs.copyFileSync(OUT_PATH, distCatalog);
    console.log(`Synced catalog to ${distCatalog}`);
  }
}

main();
