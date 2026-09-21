/** Recipe picker filter helpers for the level-editor recipes dialog (select tab). */
import type { RecipeEntry } from "./types";
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

const STANDARD_SCORES = [20, 40, 60, 80, 100, 120];

export type ScoreFilter = "all" | "other" | number;

export interface RecipePickerFilterState {
  cookSteps: Set<string>;
  utensils: Set<string>;
  ingredients: Set<string>;
  score: ScoreFilter;
}

export function emptyRecipePickerFilters(): RecipePickerFilterState {
  return {
    cookSteps: new Set(),
    utensils: new Set(),
    ingredients: new Set(),
    score: "all",
  };
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
