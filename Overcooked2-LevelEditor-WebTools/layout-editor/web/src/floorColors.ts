import type { CatalogItem, DeathInfo } from "./types";
import { translateMaterialId } from "./floorMaterialLabels";

export interface SurfacePaint {
  fill: string;
  stroke: string;
  label: string;
  emoji: string;
}

const SURFACE_PAINT: Record<string, SurfacePaint> = {
  solid: { fill: "rgba(150,136,110,0.55)", stroke: "rgba(110,99,80,0.95)", label: "#1a1d23", emoji: "" },
  raft: { fill: "rgba(150,108,60,0.6)", stroke: "rgba(110,75,40,0.95)", label: "#1a1d23", emoji: "🪵" },
  ice: { fill: "rgba(120,200,235,0.5)", stroke: "rgba(80,160,205,0.95)", label: "#0d2a36", emoji: "❄" },
  snow: { fill: "rgba(225,232,240,0.6)", stroke: "rgba(170,185,200,0.95)", label: "#1a1d23", emoji: "❄" },
  sand: { fill: "rgba(225,205,140,0.55)", stroke: "rgba(180,150,90,0.95)", label: "#3a2c10", emoji: "" },
  alien: { fill: "rgba(150,220,150,0.5)", stroke: "rgba(95,170,95,0.95)", label: "#143a14", emoji: "" },
  walkway: { fill: "rgba(170,145,105,0.55)", stroke: "rgba(130,105,70,0.95)", label: "#2a1f10", emoji: "" },
  carpet: { fill: "rgba(190,120,170,0.5)", stroke: "rgba(150,85,135,0.95)", label: "#2a1020", emoji: "" },
  section: { fill: "rgba(165,150,125,0.5)", stroke: "rgba(120,105,80,0.95)", label: "#1a1d23", emoji: "" },
  ground: { fill: "rgba(120,165,105,0.5)", stroke: "rgba(85,125,72,0.95)", label: "#12240e", emoji: "" },
  conveyor: { fill: "rgba(225,180,90,0.55)", stroke: "rgba(180,130,50,0.95)", label: "#2a1f10", emoji: "↔" },
  background: { fill: "rgba(120,150,220,0.4)", stroke: "rgba(90,120,190,0.9)", label: "#0d1830", emoji: "☁" },
};

export function surfacePaint(kind: string | undefined, selected: boolean): SurfacePaint {
  const base = SURFACE_PAINT[kind ?? "solid"] ?? SURFACE_PAINT.solid;
  if (!selected) return base;
  return {
    fill: "rgba(249,171,0,0.78)",
    stroke: "#f9ab00",
    label: "#1a1d23",
    emoji: base.emoji,
  };
}

export function voidFill(deathType: string | undefined): string {
  switch (deathType) {
    case "water":
      return "#0e2a47";
    case "goo":
      return "#0f2e24";
    default:
      return "#15171c";
  }
}

export interface BgTheme {
  key: string;
  labelZh: string;
  emoji: string;
  /** Void / background fill color (floor layer). */
  fill: string;
  /** Faint hazard hatch color drawn over the void to signal a fall/death zone. */
  hatch: string;
  /** Editor death type this theme represents. */
  deathType: "water" | "goo" | "fall";
}

export const BG_THEMES: BgTheme[] = [
  { key: "void", labelZh: "空洞", emoji: "🕳️", fill: "#15171c", hatch: "rgba(130,132,142,0.10)", deathType: "fall" },
  { key: "water", labelZh: "水", emoji: "💧", fill: "#0e2a47", hatch: "rgba(120,180,230,0.12)", deathType: "water" },
  { key: "sky", labelZh: "天空", emoji: "☁️", fill: "#0d1830", hatch: "rgba(140,170,220,0.10)", deathType: "fall" },
  { key: "sand", labelZh: "沙地", emoji: "🏜️", fill: "#2a2010", hatch: "rgba(200,170,100,0.10)", deathType: "fall" },
  { key: "goo", labelZh: "外星黏液", emoji: "🟢", fill: "#0f2e24", hatch: "rgba(120,220,140,0.12)", deathType: "goo" },
];

const BG_THEME_MAP: Record<string, BgTheme> = BG_THEMES.reduce(
  (acc, t) => {
    acc[t.key] = t;
    return acc;
  },
  {} as Record<string, BgTheme>
);

export function bgTheme(key: string | undefined): BgTheme {
  return (key && BG_THEME_MAP[key]) || BG_THEMES[0];
}

/** Prefab ids auto-placed when a theme is selected and missing from the scene. */
export const THEME_BACKGROUND_PREFABS: Record<string, string[]> = {
  sky: ["Sky"],
  water: ["Water_01"],
  sand: ["sand_01"],
  goo: ["alien_gue"],
};

/** Default water plane when no DLC-specific match is found. */
export const DEFAULT_THEME_WATER_PREFAB = "Water_01";

/** Preferred water backdrop per DLC / theme family (background theme dropdown). */
export const THEME_WATER_PREFAB_BY_DLC: Record<string, string> = {
  dlc02: "poolwater_01",
  dlc03: "Water_01",
  dlc04: "p_dlc4_water_01",
  dlc05: "p_dlc5_camp_water",
  dlc10: "p_dlc10_water_01",
  dlc11: "poolwater_02",
  dlc13: "p_dlc13_water_02",
  raft: "dynamic_raft_water",
  city: "city_water",
};

/** All prefab ids that may be auto-placed or inferred for the water theme. */
export function allThemeWaterPrefabIds(): string[] {
  const ids = new Set<string>([DEFAULT_THEME_WATER_PREFAB, "raft_water"]);
  for (const id of Object.values(THEME_WATER_PREFAB_BY_DLC)) ids.add(id);
  return [...ids];
}

/** Guess dominant DLC tag from level set name or placed prefab paths. */
export function inferDominantDlcTag(
  levelSet: string,
  items?: { prefabAssetPath?: string }[]
): string | null {
  const lc = (levelSet ?? "").toLowerCase();
  const ordered = Object.keys(THEME_WATER_PREFAB_BY_DLC).sort((a, b) => b.length - a.length);
  for (const dlc of ordered) {
    if (lc.includes(dlc)) return dlc;
  }
  if (lc.includes("jia_carnival") || lc.includes("mid_autumn") || lc.includes("moon")) return "dlc13";
  if (lc.includes("beach")) return "dlc02";
  if (lc.includes("camp") || lc.includes("forest")) return "dlc05";
  if (lc.includes("raft")) return "raft";
  if (lc.includes("city") || lc.includes("sushi")) return "city";

  if (!items?.length) return null;
  const counts = new Map<string, number>();
  for (const it of items) {
    const p = (it.prefabAssetPath ?? "").toLowerCase();
    const m = p.match(/\/(dlc\d{2})\//) ?? p.match(/(dlc\d{2})_/);
    if (m) counts.set(m[1], (counts.get(m[1]) ?? 0) + 1);
  }
  let best = "";
  let bestN = 0;
  for (const [dlc, n] of counts) {
    if (n > bestN) {
      bestN = n;
      best = dlc;
    }
  }
  return best || null;
}

/** Resolve the water prefab for the current level set / scene content. */
export function themeWaterPrefabId(
  levelSet?: string,
  items?: { prefabAssetPath?: string }[],
  catalogHas?: (id: string) => boolean
): string {
  const dlc = inferDominantDlcTag(levelSet ?? "", items);
  if (dlc === "raft" && catalogHas && !catalogHas("dynamic_raft_water")) {
    return "raft_water";
  }
  if (dlc && THEME_WATER_PREFAB_BY_DLC[dlc]) {
    const id = THEME_WATER_PREFAB_BY_DLC[dlc];
    if (!catalogHas || catalogHas(id)) return id;
  }
  return DEFAULT_THEME_WATER_PREFAB;
}

export function themeBackgroundPrefabIds(
  themeKey: string,
  levelSet?: string,
  items?: { prefabAssetPath?: string }[],
  catalogHas?: (id: string) => boolean
): string[] {
  if (themeKey === "water") return [themeWaterPrefabId(levelSet, items, catalogHas)];
  return THEME_BACKGROUND_PREFABS[themeKey] ?? [];
}

/** All environment prefabs managed by theme switching (mutually exclusive). */
export function allThemeBackgroundPrefabIds(): string[] {
  const ids = new Set<string>();
  for (const [theme, list] of Object.entries(THEME_BACKGROUND_PREFABS)) {
    if (theme === "water") {
      for (const id of allThemeWaterPrefabIds()) ids.add(id);
    } else {
      for (const id of list) ids.add(id);
    }
  }
  return [...ids];
}

export function isThemeBackgroundPrefabId(id: string | undefined): boolean {
  if (!id) return false;
  return allThemeBackgroundPrefabIds().includes(id);
}

/** True for theme-managed environment prefabs (mutually exclusive). */
export function isGlobalBackgroundItem(cat: CatalogItem | undefined, prefabId?: string): boolean {
  return isThemeBackgroundPrefabId(prefabId ?? cat?.id);
}

function prefabIdFromAssetPath(assetPath: string | undefined): string {
  if (!assetPath) return "";
  const name = assetPath.split("/").pop() ?? "";
  return name.replace(/\.prefab$/i, "");
}

/** Infer theme from environment prefabs present in the layout. */
export function inferBgThemeFromItems(
  layoutItems: { prefabAssetPath?: string }[]
): string | null {
  const ids = new Set(layoutItems.map((i) => prefabIdFromAssetPath(i.prefabAssetPath)));
  for (const p of allThemeWaterPrefabIds()) {
    if (ids.has(p)) return "water";
  }
  for (const [theme, prefs] of Object.entries(THEME_BACKGROUND_PREFABS)) {
    if (theme === "water") continue;
    for (const p of prefs) {
      if (ids.has(p)) return theme;
    }
  }
  return null;
}

export function bgThemeTooltip(theme: BgTheme, levelSet?: string): string {
  if (theme.key === "water") {
    const pid = themeWaterPrefabId(levelSet);
    return `${theme.labelZh}：写回时自动确保场景有 ${pid} 环境 prefab（按关卡 DLC 自动选择）`;
  }
  const prefabs = THEME_BACKGROUND_PREFABS[theme.key];
  if (prefabs?.length) {
    return `${theme.labelZh}：写回时自动确保场景有 ${prefabs.join(" / ")} 环境 prefab`;
  }
  if (theme.key === "void") {
    return `${theme.labelZh}：无背景 prefab，靠 KillPlane 扩大 + 地板间隙实现坠落区`;
  }
  return theme.labelZh;
}

export function bgThemeKeyForDeathType(dt: string | undefined): string {
  if (dt === "water") return "water";
  if (dt === "goo") return "goo";
  return "void";
}

export function deathLabelZh(info: DeathInfo | null): string {
  switch (info?.deathType) {
    case "water":
      return "水域（落水溺亡）";
    case "goo":
      return "外星黏液（坠入溶化）";
    default:
      return "空洞（坠落死亡）";
  }
}

/** True for catalog items that belong to the floor/background layer. */
export function isSurfaceItem(cat: CatalogItem | undefined): boolean {
  return cat?.surfaceTier === "floor" || cat?.surfaceTier === "background";
}

export function surfaceKindLabelZh(kind: string | undefined): string {
  switch (kind) {
    case "raft":
      return "木筏";
    case "ice":
      return "冰面（滑）";
    case "snow":
      return "雪地";
    case "sand":
      return "沙地";
    case "alien":
      return "外星地板";
    case "walkway":
      return "栈道";
    case "carpet":
      return "地毯";
    case "section":
      return "地板段";
    case "ground":
      return "大型地面";
    case "conveyor":
      return "传送带地面";
    case "background":
      return "背景";
    default:
      return "实心地板";
  }
}

/** Group key for the solid-floor material picker, by level theme. Sky /
 *  background and particle-FX materials return keys absent from
 *  FLOOR_MATERIAL_GROUPS so they are excluded from the picker entirely. */
export function floorMaterialGroup(id: string): string {
  const n = (id ?? "").toLowerCase();
  if (/skybox|starscape|sky|background/.test(n)) return "sky";
  if (/pfx|glow|particle|effect/.test(n)) return "fx";
  if (/airballoon/.test(n)) return "airballoon";
  if (/blacktiles/.test(n)) return "blacktiles";
  if (/wizard/.test(n)) return "wizard";
  if (/kevin/.test(n)) return "kevin";
  if (/sk_floor/.test(n)) return "sk";
  if (/(^|_)mi_|mine/.test(n)) return "mine";
  if (/(^|_)sp_|space/.test(n)) return "sp";
  if (/swamp/.test(n)) return "swamp";
  if (/grave/.test(n)) return "grave";
  if (/dlc2|onionhouse/.test(n)) return "beach";
  if (/dlc3|ice_floor/.test(n)) return "ice";
  if (/dlc4|mossfloor|pathedging/.test(n)) return "dlc4";
  if (/dlc5|forest/.test(n)) return "forest";
  if (/dlc07|hiddencity/.test(n)) return "hiddencity";
  if (/dlc08/.test(n)) return "circus";
  if (/dlc13/.test(n)) return "other";
  if (/city|pavement|road|yellowbox/.test(n)) return "city";
  if (/alien/.test(n)) return "alien";
  if (/raft/.test(n)) return "raft";
  if (/snow/.test(n)) return "snow";
  if (/sand|desert/.test(n)) return "sand";
  if (/walkway/.test(n)) return "walkway";
  if (/carpet/.test(n)) return "carpet";
  if (/kitchen|floortile|floor_tile/.test(n)) return "kitchen";
  return "other";
}

/** Ordered theme groups for the solid-floor material picker. Sky/FX materials
 *  have no entry here and are therefore hidden from the list. */
export const FLOOR_MATERIAL_GROUPS: { key: string; labelZh: string }[] = [
  { key: "kevin", labelZh: "凯文（故事厨房）" },
  { key: "airballoon", labelZh: "热气球" },
  { key: "blacktiles", labelZh: "太空黑砖" },
  { key: "wizard", labelZh: "魔法学校" },
  { key: "sk", labelZh: "共享厨房" },
  { key: "mine", labelZh: "矿洞" },
  { key: "sp", labelZh: "太空" },
  { key: "swamp", labelZh: "沼泽" },
  { key: "grave", labelZh: "墓地" },
  { key: "beach", labelZh: "海滩（DLC2）" },
  { key: "ice", labelZh: "冰面" },
  { key: "dlc4", labelZh: "石径苔地（DLC4）" },
  { key: "forest", labelZh: "森林营地（DLC5）" },
  { key: "hiddencity", labelZh: "隐秘之城（DLC7）" },
  { key: "circus", labelZh: "马戏团（DLC8）" },
  { key: "city", labelZh: "城市" },
  { key: "alien", labelZh: "外星地板" },
  { key: "raft", labelZh: "木筏" },
  { key: "snow", labelZh: "雪地" },
  { key: "sand", labelZh: "沙地" },
  { key: "walkway", labelZh: "栈道" },
  { key: "carpet", labelZh: "地毯" },
  { key: "kitchen", labelZh: "原版厨房/路面" },
  { key: "other", labelZh: "其他" },
];

/** Bilingual (zh / en) label for a floor material id, e.g. mat_kevin_floor_12x8. */
export function materialBilingual(id: string): { zh: string; en: string } {
  return translateMaterialId(id);
}
