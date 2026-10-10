/** 锅具管理：中间产物/自定义菜谱选择器与「按菜谱自动填充」共用的筛选状态。 */
import type { RecipeEntry } from "./types";
import {
  isUtensilIntermediateRecipe,
  isMixIntermediate,
} from "./editor/recipeKnowledge";
import {
  recipeCookSteps,
  COOK_STEP_LABEL_ZH,
  collectCookStepsFromRecipes,
  type RecipeWithGroups,
} from "./recipePickerFilters";
import { chipBtnHtml } from "./ui/views/button";
import { foodGroupLabel } from "./ingredientLabels";

export interface UtensilRecipeFilterState {
  /** 仅自定义菜谱（isCustom）。 */
  onlyCustom: boolean;
  /** 仅搅拌类（Mixed / mixing / 搅拌步骤中间产物）。 */
  onlyStirring: boolean;
  /** 仅 0 分。 */
  onlyZeroScore: boolean;
  /** 烹饪步骤多选（OR：命中任一步骤即可）；空 = 不限。 */
  cookSteps: Set<string>;
}

export function emptyUtensilRecipeFilters(): UtensilRecipeFilterState {
  return {
    onlyCustom: false,
    onlyStirring: false,
    onlyZeroScore: false,
    cookSteps: new Set(),
  };
}

export function isCustomUtensilRecipe(r: RecipeEntry): boolean {
  return !!r.isCustom && isUtensilIntermediateRecipe(r);
}

export function isStirringUtensilRecipe(r: RecipeEntry): boolean {
  return isMixIntermediate(r) || r.type === "Mixed" || !!r.mixing;
}

export function isZeroScoreRecipe(r: RecipeEntry): boolean {
  return (r.score ?? 0) === 0;
}

/** 多选 toggle 交集；用于关卡已选菜谱子集与选择器列表。 */
export function matchesUtensilRecipeFilters(
  r: RecipeEntry,
  state: UtensilRecipeFilterState,
  allRecipes?: RecipeEntry[]
): boolean {
  if (state.onlyCustom && !r.isCustom) return false;
  if (state.onlyStirring && !isStirringUtensilRecipe(r)) return false;
  if (state.onlyZeroScore && !isZeroScoreRecipe(r)) return false;
  if (state.cookSteps.size > 0) {
    const steps = recipeCookSteps(r as RecipeWithGroups, allRecipes as RecipeWithGroups[]);
    if (!steps.some((s) => state.cookSteps.has(s))) return false;
  }
  return true;
}

export function countActiveUtensilRecipeFilters(state: UtensilRecipeFilterState): number {
  let n = 0;
  if (state.onlyCustom) n++;
  if (state.onlyStirring) n++;
  if (state.onlyZeroScore) n++;
  if (state.cookSteps.size > 0) n++;
  return n;
}

/** 搅拌相关步骤优先排序，其余按中文名。 */
const STIR_STEP_ORDER = ["Mixer", "MixingBowl", "Blender"];

export function collectUtensilCookStepChips(recipes: RecipeEntry[]): string[] {
  const steps = collectCookStepsFromRecipes(recipes as RecipeWithGroups[]);
  const stir = STIR_STEP_ORDER.filter((s) => steps.includes(s));
  const rest = steps.filter((s) => !STIR_STEP_ORDER.includes(s));
  return [...stir, ...rest];
}

export function cookStepChipLabel(step: string): string {
  return COOK_STEP_LABEL_ZH[step] ?? step;
}

/** 选择器 Tab 下的菜谱池（官方中间产物 / 自定义）。 */
export function recipesForPickerGroup(
  pickerIntermediates: RecipeEntry[],
  group: string
): RecipeEntry[] {
  if (group === "__custom__") {
    return pickerIntermediates.filter((r) => isCustomUtensilRecipe(r));
  }
  if (group === "__intermediate__") {
    return pickerIntermediates.filter((r) => !r.isCustom);
  }
  return pickerIntermediates;
}

export function filterPickerRecipes(
  recipes: RecipeEntry[],
  state: UtensilRecipeFilterState,
  recipeCatMatch: (r: RecipeEntry) => boolean,
  recipeTextMatch: (r: RecipeEntry) => boolean
): RecipeEntry[] {
  return recipes
    .filter(recipeCatMatch)
    .filter(recipeTextMatch)
    .filter((r) => matchesUtensilRecipeFilters(r, state, recipes));
}

/** 锅具菜谱细筛条 HTML（选择器 / 自动填充共用）。 */
export function utensilRecipeFilterBarHtml(
  state: UtensilRecipeFilterState,
  cookSteps: string[],
  options?: { barId?: string; hint?: string }
): string {
  const barId = options?.barId ?? "utm-recipe-filters";
  const hint = options?.hint ?? "";
  const stepChips = cookSteps
    .map((step) => {
      const active = state.cookSteps.has(step);
      return chipBtnHtml(
        cookStepChipLabel(step),
        active,
        { "data-utm-cook-step": step, type: "button", title: `烹饪步骤：${cookStepChipLabel(step)}` },
        "utm-recipe-chip"
      );
    })
    .join("");
  return `<div class="ing-filter-bar utm-recipe-filter-bar" id="${barId}" ${hint ? `title="${hint.replace(/"/g, "&quot;")}"` : ""}>
    <span class="ing-cat-label">菜谱筛选</span>
    <div class="ing-groups utm-recipe-chips">
      ${chipBtnHtml("0 分", state.onlyZeroScore, { "data-utm-state": "zero", type: "button", title: "仅 0 分（可叠加）" }, "utm-recipe-chip")}
      ${chipBtnHtml("🥣 搅拌", state.onlyStirring, { "data-utm-state": "stir", type: "button", title: "仅搅拌类（可叠加）" }, "utm-recipe-chip")}
      ${chipBtnHtml("自定义", state.onlyCustom, { "data-utm-state": "custom", type: "button", title: "仅自定义菜谱（可叠加）" }, "utm-recipe-chip")}
      ${stepChips}
    </div>
  </div>`;
}

/** 绑定细筛条点击；onChange 在状态变更后调用。 */
export function bindUtensilRecipeFilterBar(
  root: ParentNode,
  state: UtensilRecipeFilterState,
  onChange: () => void
): void {
  root.querySelectorAll<HTMLButtonElement>("[data-utm-state]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const key = btn.dataset.utmState;
      if (key === "zero") state.onlyZeroScore = !state.onlyZeroScore;
      else if (key === "stir") state.onlyStirring = !state.onlyStirring;
      else if (key === "custom") state.onlyCustom = !state.onlyCustom;
      btn.classList.toggle("active", key === "zero" ? state.onlyZeroScore : key === "stir" ? state.onlyStirring : state.onlyCustom);
      onChange();
    });
  });
  root.querySelectorAll<HTMLButtonElement>("[data-utm-cook-step]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const step = btn.dataset.utmCookStep ?? "";
      if (!step) return;
      if (state.cookSteps.has(step)) state.cookSteps.delete(step);
      else state.cookSteps.add(step);
      btn.classList.toggle("active", state.cookSteps.has(step));
      onChange();
    });
  });
}

/** 选择器用：菜谱卡片徽章。 */
export function utensilRecipeCardBadgesHtml(r: RecipeEntry): string {
  const parts: string[] = [];
  if (r.group === "levelset") parts.push('<span class="pc-badge">本关</span>');
  else if (r.isCustom && r.group && r.group !== "core") {
    const g = foodGroupLabel(r.group);
    if (g) parts.push(`<span class="pc-badge">${g}</span>`);
  }
  if (isStirringUtensilRecipe(r)) parts.push('<span class="pc-badge">搅拌</span>');
  if (isZeroScoreRecipe(r)) parts.push('<span class="pc-badge">0分</span>');
  if (!r.isCustom) parts.push('<span class="pc-badge">中间产物</span>');
  else parts.push('<span class="pc-badge">自定义</span>');
  return parts.join(" ");
}
