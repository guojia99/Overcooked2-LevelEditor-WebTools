/** Recipe picker filter helpers for the level-editor recipes dialog (select tab). */
import type { RecipeEntry } from "./types";
import { tidyCatalogNameZh } from "./displayLabels";
import { UTENSIL_KIND_BY_ID, STATION_CLASS_ZH, STATION_CLASS_BY_ID } from "./autoScoreKnowledge";
import { functionalBaseId } from "./editor/recipeKnowledge";
import {
  STEP_UTENSILS,
  deriveCookingGroups,
  mergeFinalMarkers,
  normalizeCookingGroups,
  type CookingGroup,
} from "./recipeGroups";

export type RecipeWithGroups = RecipeEntry & { cookingGroups?: CookingGroup[] };

function resolveGroups(r: RecipeWithGroups, allRecipes?: RecipeWithGroups[]): CookingGroup[] {
  if (r.cookingGroups?.length) {
    return normalizeCookingGroups(r, r.cookingGroups);
  }
  const norm = { ...r, intermediate: r.isCustom ? false : r.intermediate };
  return mergeFinalMarkers(normalizeCookingGroups(r, deriveCookingGroups(norm, allRecipes ?? [])));
}

/** 烹饪方式 id → 中文（与 guide/icons.ts 一致）。 */
export const COOK_STEP_LABEL_ZH: Record<string, string> = {
  Pot: "煮锅",
  FryingPan: "煎锅",
  DeepFatFryer: "炸篮",
  OvenTray: "烤箱",
  Steamer: "蒸笼",
  Mixer: "搅拌碗",
  Blender: "搅拌杯",
  MixingBowl: "搅拌碗",
  GriddlePan: "煎烤盘",
  KebabSkewer: "烤串",
  ToastingFork: "烤棉花糖叉",
  HotPot: "大火锅",
  RoastingTray: "烤托盘",
  OvenCakeTin: "蛋糕模",
};

/** 工作台/道具 catalog id → 中文（过滤芯片与自动填充清单）。 */
const UTENSIL_CATALOG_LABEL_ZH: Record<string, string> = {
  Cooker: "灶台",
  FryingStation: "炸台",
  Oven: "烤箱",
  Mixer: "搅拌台",
  Blender: "搅拌机",
  Barbeque: "烧烤架",
  Campfire: "篝火",
  ChoppingCounter: "切菜台",
  workstation_guillotine_01: "断头台",
  ServingStation: "上菜台",
  Bin: "垃圾箱",
  Counter: "操作台",
  Switch: "按钮",
  CleanPlateStack: "干净盘子堆",
  CleanGlassStack: "干净玻璃杯堆",
  cleanmugstack: "干净马克杯堆",
  dlc08_cleantraystack: "干净餐盘堆",
  Sink: "水槽",
  SinkPlate: "洗盘子水槽",
  SinkGlass: "洗杯子水槽",
  PlateReturn: "脏盘回收台",
  GlassReturn: "脏杯回收台",
  Dispenser: "食材箱",
};

/** 锅具/道具过滤与清单展示用中文名（含未进目录的 DLC 网格 id）。 */
export function utensilFilterLabel(
  id: string,
  catalog?: { nameZh?: string; id?: string } | null
): string {
  const cid = catalog?.id ?? id;
  const fromCatalog = catalog?.nameZh ? tidyCatalogNameZh(catalog.nameZh, cid) : "";
  if (fromCatalog && fromCatalog !== id) return fromCatalog;
  const kind = UTENSIL_KIND_BY_ID[id];
  if (kind && COOK_STEP_LABEL_ZH[kind]) return COOK_STEP_LABEL_ZH[kind];
  if (COOK_STEP_LABEL_ZH[id]) return COOK_STEP_LABEL_ZH[id];
  const station = STATION_CLASS_BY_ID[id];
  if (station && STATION_CLASS_ZH[station]) return STATION_CLASS_ZH[station];
  if (UTENSIL_CATALOG_LABEL_ZH[id]) return UTENSIL_CATALOG_LABEL_ZH[id];
  const base = functionalBaseId(id);
  if (base !== id) return utensilFilterLabel(base, null);
  return fromCatalog || id;
}

const STANDARD_SCORES = [20, 40, 60, 80, 100, 120];

export type ScoreFilter = "all" | "other" | number;

export interface RecipePickerFilterState {
  /** 菜谱分类（type 字段，如 burger / pizza / sushi），多选 OR。 */
  types: Set<string>;
  cookSteps: Set<string>;
  utensils: Set<string>;
  ingredients: Set<string>;
  score: ScoreFilter;
}

export function emptyRecipePickerFilters(): RecipePickerFilterState {
  return {
    types: new Set(),
    cookSteps: new Set(),
    utensils: new Set(),
    ingredients: new Set(),
    score: "all",
  };
}

/** 当前已启用的筛选条件数量（用于工具栏徽标）。 */
export function countActivePickerFilters(filters: RecipePickerFilterState): number {
  let n = 0;
  if (filters.types.size > 0) n++;
  if (filters.cookSteps.size > 0) n++;
  if (filters.utensils.size > 0) n++;
  if (filters.ingredients.size > 0) n++;
  if (filters.score !== "all") n++;
  return n;
}

export function scoreMatches(s: number | undefined, filter: ScoreFilter): boolean {
  if (filter === "all") return true;
  const v = s ?? 0;
  if (filter === "other") return !STANDARD_SCORES.includes(v);
  return v === filter;
}

/** 收集菜谱涉及的烹饪步骤 id。 */
export function recipeCookSteps(r: RecipeWithGroups, allRecipes?: RecipeWithGroups[]): string[] {
  const out = new Set<string>();
  if (r.cookingStep) out.add(r.cookingStep);
  const groups = resolveGroups(r, allRecipes);
  for (const g of groups) {
    if (g.step) out.add(g.step);
    for (const e of g.extraSteps ?? []) if (e.step) out.add(e.step);
    for (const steps of Object.values(g.ingredientSteps ?? {})) {
      for (const s of steps) out.add(s);
    }
  }
  return [...out];
}

/** 收集菜谱涉及的锅具 catalog id。 */
export function recipeUtensilIds(r: RecipeWithGroups, allRecipes?: RecipeWithGroups[]): string[] {
  const out = new Set<string>();
  const groups = resolveGroups(r, allRecipes);
  for (const g of groups) {
    for (const u of g.utensils ?? []) out.add(u);
    if (g.step) for (const u of STEP_UTENSILS[g.step] ?? []) out.add(u);
    for (const e of g.extraSteps ?? []) {
      for (const u of e.utensils ?? []) out.add(u);
      if (e.step) for (const u of STEP_UTENSILS[e.step] ?? []) out.add(u);
    }
  }
  return [...out];
}

/** 叶食材 id 集合（展开中间产物）。 */
export function recipeLeafIngredientIds(
  r: RecipeEntry,
  leafFn: (id: string) => string[]
): Set<string> {
  const out = new Set<string>();
  for (const ing of r.ingredients ?? []) {
    for (const leaf of leafFn(ing)) out.add(leaf);
  }
  return out;
}

export function recipeMatchesFilters(
  r: RecipeWithGroups,
  filters: RecipePickerFilterState,
  leafFn: (id: string) => string[],
  allRecipes?: RecipeWithGroups[]
): boolean {
  if (!scoreMatches(r.score, filters.score)) return false;

  if (filters.types.size > 0) {
    const t = r.type ?? "other";
    if (!filters.types.has(t)) return false;
  }

  if (filters.cookSteps.size > 0) {
    const steps = recipeCookSteps(r, allRecipes);
    if (!steps.some((s) => filters.cookSteps.has(s))) return false;
  }

  if (filters.utensils.size > 0) {
    const uts = recipeUtensilIds(r, allRecipes);
    if (!uts.some((u) => filters.utensils.has(u))) return false;
  }

  if (filters.ingredients.size > 0) {
    const leaves = recipeLeafIngredientIds(r, leafFn);
    for (const ing of filters.ingredients) {
      if (!leaves.has(ing)) return false;
    }
  }

  return true;
}

/** 锅具 catalog id → 图标（优先 catalog 缩略图）。 */
export function utensilIconSrc(id: string): string {
  return `/icons/catalog/${encodeURIComponent(id)}.png`;
}

/** 从菜谱列表汇总可选分类 type（按 RECIPE_TYPE_ORDER 稳定排序）。 */
export function collectTypesFromRecipes(
  recipes: RecipeEntry[],
  order: readonly string[]
): { type: string; count: number; iconRecipeId?: string }[] {
  const byType = new Map<string, { count: number; iconRecipeId?: string }>();
  for (const r of recipes) {
    const t = r.type ?? "other";
    const cur = byType.get(t) ?? { count: 0, iconRecipeId: undefined };
    cur.count++;
    if (!cur.iconRecipeId && r.id && r.icon !== false) cur.iconRecipeId = r.id;
    byType.set(t, cur);
  }
  const rank = (t: string) => {
    const i = order.indexOf(t);
    return i < 0 ? 99 : i;
  };
  return [...byType.entries()]
    .sort((a, b) => rank(a[0]) - rank(b[0]) || a[0].localeCompare(b[0]))
    .map(([type, meta]) => ({ type, ...meta }));
}

/** 从菜谱列表汇总可选烹饪步骤（排序稳定）。 */
export function collectCookStepsFromRecipes(recipes: RecipeWithGroups[]): string[] {
  const out = new Set<string>();
  for (const r of recipes) {
    for (const s of recipeCookSteps(r, recipes)) out.add(s);
  }
  return [...out].sort((a, b) =>
    (COOK_STEP_LABEL_ZH[a] ?? a).localeCompare(COOK_STEP_LABEL_ZH[b] ?? b, "zh")
  );
}

/** 从菜谱列表汇总可选锅具 catalog id。 */
export function collectUtensilsFromRecipes(recipes: RecipeWithGroups[]): string[] {
  const out = new Set<string>();
  for (const r of recipes) {
    for (const u of recipeUtensilIds(r, recipes)) out.add(u);
  }
  return [...out].sort();
}

/** 从菜谱列表汇总叶食材 id。 */
export function collectLeafIngredientsFromRecipes(
  recipes: RecipeEntry[],
  leafFn: (id: string) => string[]
): string[] {
  const out = new Set<string>();
  for (const r of recipes) {
    for (const id of recipeLeafIngredientIds(r, leafFn)) out.add(id);
  }
  return [...out];
}
