/** Shared "菜谱清单列表" (recipe list) card UI, used by:
 *  - /recipes page (recipeList.ts)
 *  - 关卡编辑器「已选菜谱」dialog (main.ts openSelectedRecipesDialog)
 *  - 汇总页 (levels.ts renderLevelSummary)
 *
 *  Card = product area (UI_DLC07_Recipe_Background_Main_01.png) + cooking
 *  groups (Recipe_Background_2.png, one per step, ingredient chips + step
 *  icons). Recipe grouping rules live in recipeGroups.ts. */
import type { RecipeEntry } from "./types";
import { foodGroupLabel } from "./ingredientLabels";
import { recipeTypeLabel } from "./recipeTypes";
import {
  deriveCookingGroups,
  mergeFinalMarkers,
  normalizeCookingGroups,
  type CookingGroup,
} from "./recipeGroups";

/** RecipeEntry from the bridge/static JSON plus the backend-computed cooking groups
 *  (absent in stale data — recipeGroups.deriveCookingGroups is the fallback). */
export type RecipeWithGroups = RecipeEntry & { cookingGroups?: CookingGroup[] };

/** 烹饪步骤 → 图标（锅具/烤箱/搅拌器等）：
 *  catalog = 游戏解包图标；steps = 从 sprite-dump 挑选的静态图标
 *  （烤串=竹签烤串成品图，烤棉花糖=棉花糖饼干成品图）。 */
export const STEP_ICON_SRC: Record<string, string> = {
  Pot: "/icons/catalog/Pot.png",
  FryingPan: "/icons/catalog/FryPan.png",
  DeepFatFryer: "/icons/catalog/FrierBasket.png",
  OvenTray: "/icons/catalog/Oven.png",
  Steamer: "/icons/catalog/Steamer.png",
  Mixer: "/icons/catalog/MixerBowl.png",
  Blender: "/icons/catalog/BlenderCup.png",
  MixingBowl: "/icons/catalog/MixerBowl.png",
  GriddlePan: "/icons/steps/splitpan.png",
  KebabSkewer: "/icons/steps/grill.png",
  ToastingFork: "/icons/steps/toastingfork.png",
};

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

/** Recipe icon <img> with placeholder fallback. */
export function recipeImgHtml(r: RecipeEntry, cls = ""): string {
  const src = `/icons/recipes/${encodeURIComponent(r.id)}.png`;
  return `<img class="${cls}" loading="lazy" src="${esc(src)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`;
}

/** Ingredient chip <img> with placeholder fallback. */
export function ingredientChipHtml(ingId: string, title?: string): string {
  const src = `/icons/ingredients/${encodeURIComponent(ingId)}.png`;
  return `<span class="rl-chip" title="${esc(title || ingId)}">
    <img loading="lazy" src="${esc(src)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">
  </span>`;
}

export function stepIconHtml(step: string): string {
  const src = STEP_ICON_SRC[step];
  if (!src) return "";
  return `<img class="rl-step-icon" loading="lazy" src="${src}" alt="" onerror="this.onerror=null;this.style.display='none'">`;
}

/** One cooking group box: ingredient chips + step icons (if any). */
export function rlGroupHtml(g: CookingGroup, ingredientName?: (id: string) => string): string {
  const chips = g.ingredients.map((ing) => ingredientChipHtml(ing, ingredientName?.(ing))).join("");
  const hasStep = !!g.step;
  const icons = [g.step, ...(g.extraSteps ?? []).map((e) => e.step)]
    .filter(Boolean)
    .map(stepIconHtml)
    .join("");
  return `<div class="rl-group${hasStep ? " has-step" : ""}">
    <div class="rl-group-chips">${chips || '<span class="muted small">—</span>'}</div>
    ${hasStep ? `<div class="rl-step">${icons}</div>` : ""}
  </div>`;
}

/** Ingredient chip list from ids (fallback when no cooking groups exist). */
function plainGroupHtml(ingredientIds: string[], ingredientName?: (id: string) => string): string {
  return `<div class="rl-group"><div class="rl-group-chips">${ingredientIds.map((ing) => ingredientChipHtml(ing, ingredientName?.(ing))).join("")}</div></div>`;
}

export interface RlCardOptions {
  /** Full recipe catalog for cooking-group fallback derivation. */
  allRecipes?: RecipeWithGroups[];
  /** Extra badge text (e.g. "本关" for levelset recipes). */
  extraBadge?: string;
  /** Ingredient id → Chinese name, used for chip tooltips. */
  ingredientName?: (id: string) => string;
}

/** Compute the merged cooking groups for a card (backend data + fallback derivation). */
export function computeCardGroups(r: RecipeWithGroups, opts: RlCardOptions = {}): CookingGroup[] {
  const groups = r.cookingGroups
    ? normalizeCookingGroups(r, r.cookingGroups)
    : normalizeCookingGroups(r, deriveCookingGroups(r, opts.allRecipes ?? []));
  return mergeFinalMarkers(groups);
}

/** Full "菜谱清单列表" card: product area + cooking groups. */
export function rlCardHtml(r: RecipeWithGroups, opts: RlCardOptions = {}): string {
  const merged = computeCardGroups(r, opts);

  const badges = [
    r.intermediate ? `<span class="rl-badge rl-badge-inter">半成品</span>` : "",
    r.isCustom ? `<span class="rl-badge rl-badge-custom">自定义</span>` : "",
    opts.extraBadge ? `<span class="rl-badge rl-badge-dlc">${esc(opts.extraBadge)}</span>` : "",
    r.group && r.group !== "core"
      ? `<span class="rl-badge rl-badge-dlc">${esc(foodGroupLabel(r.group))}</span>`
      : "",
    !r.intermediate ? `<span class="rl-badge rl-badge-score">⭐ ${r.score ?? 0}</span>` : "",
  ].join("");

  const groupsHtml =
    merged.length > 0
      ? merged.map((g) => rlGroupHtml(g, opts.ingredientName)).join("")
      : (r.ingredients ?? []).length > 0
        ? plainGroupHtml(r.ingredients ?? [], opts.ingredientName)
        : '<div class="rl-group"><span class="muted small">组成信息缺失</span></div>';

  return `<article class="rl-card" title="${esc(r.id)}">
    <div class="rl-product">
      ${recipeImgHtml(r)}
      <div class="rl-prod-name">${esc(r.nameZh)}<span class="rl-prod-en">${esc(r.nameEn || r.id)}</span></div>
      <div class="rl-prod-badges">${badges}</div>
    </div>
    <div class="rl-groups">
      ${groupsHtml}
    </div>
  </article>`;
}

/** Category section: one row of cards with a type title (汉堡 / 卷饼 / …). */
export function rlSectionHtml(type: string, cardsHtml: string, count: number): string {
  return `<section class="rl-section">
    <h2 class="rl-section-title">${esc(recipeTypeLabel(type))}<span class="rl-section-count">${count}</span></h2>
    <div class="rl-grid">${cardsHtml}</div>
  </section>`;
}
