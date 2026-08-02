import type { CatalogItem, DeathInfo } from "./types";

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
  water: ["raft_water"],
  sand: ["sand_01"],
  goo: ["alien_gue"],
};

export function themeBackgroundPrefabIds(themeKey: string): string[] {
  return THEME_BACKGROUND_PREFABS[themeKey] ?? [];
}

/** All environment prefabs managed by theme switching (mutually exclusive). */
export function allThemeBackgroundPrefabIds(): string[] {
  const ids = new Set<string>();
  for (const list of Object.values(THEME_BACKGROUND_PREFABS)) {
    for (const id of list) ids.add(id);
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
  for (const [theme, prefs] of Object.entries(THEME_BACKGROUND_PREFABS)) {
    for (const p of prefs) {
      if (ids.has(p)) return theme;
    }
  }
  return null;
}

export function bgThemeTooltip(theme: BgTheme): string {
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
  if (/city/.test(n)) return "city";
  if (/alien/.test(n)) return "alien";
  if (/raft/.test(n)) return "raft";
  if (/snow/.test(n)) return "snow";
  if (/ice/.test(n)) return "ice";
  if (/sand/.test(n)) return "sand";
  if (/walkway/.test(n)) return "walkway";
  if (/carpet/.test(n)) return "carpet";
  return "other";
}

/** Ordered theme groups for the solid-floor material picker. Sky/FX materials
 *  have no entry here and are therefore hidden from the list. */
export const FLOOR_MATERIAL_GROUPS: { key: string; labelZh: string }[] = [
  { key: "kevin", labelZh: "凯文（故事厨房）" },
  { key: "airballoon", labelZh: "热气球" },
  { key: "blacktiles", labelZh: "太空黑砖" },
  { key: "wizard", labelZh: "魔法学校" },
  { key: "city", labelZh: "城市" },
  { key: "alien", labelZh: "外星地板" },
  { key: "raft", labelZh: "木筏" },
  { key: "snow", labelZh: "雪地" },
  { key: "ice", labelZh: "冰面" },
  { key: "sand", labelZh: "沙地" },
  { key: "walkway", labelZh: "栈道" },
  { key: "carpet", labelZh: "地毯" },
  { key: "other", labelZh: "其他" },
];

/** Bilingual (zh / en) label for a floor material id, e.g. mat_kevin_floor_12x8. */
const MAT_ZH: Record<string, string> = {
  kevin: "凯文",
  raft: "木筏",
  airballoon: "热气球",
  city: "城市",
  path: "路面",
  sp: "太空",
  blacktiles: "黑砖",
  wizard: "魔法学校",
  stonefloor: "石地板",
  woodfloor: "木地板",
  floor: "地板",
  tile: "砖",
  carpet: "地毯",
  sky: "天空",
  wood: "木",
  stone: "石",
  snow: "雪",
  ice: "冰",
  sand: "沙",
  alien: "外星",
  dark: "深色",
  old: "复古",
};

const MAT_EN: Record<string, string> = {
  kevin: "Kevin",
  raft: "Raft",
  airballoon: "Air Balloon",
  city: "City",
  path: "Path",
  sp: "Space",
  blacktiles: "Black Tiles",
  wizard: "Wizard",
  stonefloor: "Stone Floor",
  woodfloor: "Wood Floor",
  floor: "Floor",
  tile: "Tile",
  carpet: "Carpet",
  sky: "Sky",
  wood: "Wood",
  stone: "Stone",
  snow: "Snow",
  ice: "Ice",
  sand: "Sand",
  alien: "Alien",
  dark: "Dark",
  old: "Old",
};

export function materialBilingual(id: string): { zh: string; en: string } {
  let n = id ?? "";
  if (n.toLowerCase().startsWith("mat_")) n = n.slice(4);
  const sizeMatch = n.match(/(\d+)x(\d+)$/);
  const size = sizeMatch ? `${sizeMatch[1]}×${sizeMatch[2]}` : "";

  const tokens = n.replace(/_?\d+x\d+$/, "").split("_").filter((t) => t.length > 0);
  const zhParts: string[] = [];
  const enParts: string[] = [];
  for (const t of tokens) {
    const lower = t.toLowerCase();
    zhParts.push(MAT_ZH[lower] ?? t);
    enParts.push(MAT_EN[lower] ?? t);
  }
  // Trailing numeric variant token (e.g. the "1" in sp_blacktiles_1_11x3).
  const variantMatch = n.match(/_?(\d+)_\d+x\d+$/);
  const variant = variantMatch ? variantMatch[1] : "";

  const zh = (zhParts.join("") + (variant ? " " + variant : "") + (size ? " " + size : "")).trim();
  const en = (enParts.join(" ") + (variant ? " " + variant : "") + (size ? " (" + size + ")" : "")).trim();
  return { zh, en };
}
