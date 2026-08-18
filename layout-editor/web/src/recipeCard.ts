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
  deriveCompositionGroups,
  deriveCookingGroups,
  isCookStepLike,
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
  // 大火锅/烤托盘暂无专属图标，复用煮锅/烤箱
  HotPot: "/icons/catalog/Pot.png",
  RoastingTray: "/icons/catalog/Oven.png",
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

/** One cooking group box: ingredient chips + step icons (if any).
 *  食材级步骤角标（如炒饭的米 → Pot 煮锅角标）：渲染在该食材图标右下角。 */
export function rlGroupHtml(g: CookingGroup, ingredientName?: (id: string) => string): string {
  const chips = g.ingredients
    .map((ing) => {
      const badgeSteps = g.ingredientSteps?.[ing] ?? [];
      const badges = badgeSteps
        .map((s) => `<span class="rl-chip-badge">${stepIconHtml(s)}</span>`)
        .join("");
      return `<span class="rl-chip" title="${esc(ingredientName?.(ing) || ing)}">
        <img loading="lazy" src="/icons/ingredients/${encodeURIComponent(ing)}.png" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">
        ${badges}
      </span>`;
    })
    .join("");
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
  /** 禁用原因（web 内置未放开等）：卡片置灰 + ⛔徽标。 */
  disabledReason?: string;
  /** Ingredient id → Chinese name, used for chip tooltips. */
  ingredientName?: (id: string) => string;
  /** Custom product icon src override (e.g. levelset custom recipe icons
   *  served via the bridge instead of the static /icons/recipes/ folder). */
  iconSrc?: (r: RecipeEntry) => string;
}

/** Compute the merged cooking groups for a card (backend data + fallback derivation).
 *  自定义菜谱（isCustom）：忽略后端 cookingGroups 与 intermediate（后端按旧 score<=0
 *  中间产物语义计算，不含 mixing/分步组成），统一走前端镜像推导（intermediate:false +
 *  mixing + 分步组成），与自定义菜谱列表 / 「组装效果（实时预览）」渲染完全一致。
 *  带 compositionIds 的自定义菜谱：先展开组成（含 Mixed 子菜谱的搅拌组），再追加自身
 *  烹饪步骤标记组（如 CheesePrawn = 面糊搅拌 + 炸制）。 */
export function computeCardGroups(r: RecipeWithGroups, opts: RlCardOptions = {}): CookingGroup[] {
  const norm: RecipeWithGroups = { ...r, intermediate: r.isCustom ? false : r.intermediate };
  const compIds = r.compositionIds ?? [];
  const all = opts.allRecipes ?? [];
  let groups: CookingGroup[];
  if (r.cookingGroups && !r.isCustom) {
    groups = normalizeCookingGroups(r, r.cookingGroups);
  } else if (r.isCustom && r.mixing) {
    // Mixed 类型自定义菜谱：先搅拌（MixingBowl）再烹饪（最终步骤标记），
    // 即使带 compositionIds（纯叶食材）也走搅拌推导，不展开组成
    groups = normalizeCookingGroups(r, deriveCookingGroups(norm, all));
  } else if (r.isCustom && compIds.length > 0) {
    // 组成子菜谱先各自成组；自身还有烹饪步骤时（两阶段，如炒饭=煮米→炒），
    // deriveCompositionGroups 会把已烹饪子菜谱步骤作食材角标、普通食材并入主框、
    // 全生食材组成则被自身烹饪步骤全部包裹（如 Fried2_Shrimp 鱼虾同框）
    groups = normalizeCookingGroups(r, deriveCompositionGroups(norm, all));
  } else if (compIds.length > 0 && !isCookStepLike(r.cookingStep)) {
    groups = normalizeCookingGroups(r, deriveCompositionGroups(norm, all));
  } else {
    groups = normalizeCookingGroups(r, deriveCookingGroups(norm, all));
  }
  return mergeFinalMarkers(groups);
}

/** 卡片徽标/分组展示统一的自定义菜谱归一化：自定义菜谱恒 intermediate:false。
 *  （与 toRecipeCard 一致：所有自定义菜谱均可作组成/订单菜谱。） */
export function cardIntermediate(r: RecipeWithGroups): boolean {
  return r.isCustom ? false : !!r.intermediate;
}

/** Full "菜谱清单列表" card: product area + cooking groups. */
export function rlCardHtml(r: RecipeWithGroups, opts: RlCardOptions = {}): string {
  const merged = computeCardGroups(r, opts);
  const intermediate = cardIntermediate(r);

  const badges = [
    opts.disabledReason ? `<span class="rl-badge rl-badge-disabled">⛔ 禁用</span>` : "",
    intermediate ? `<span class="rl-badge rl-badge-inter">半成品</span>` : "",
    r.isCustom ? `<span class="rl-badge rl-badge-custom">自定义</span>` : "",
    opts.extraBadge ? `<span class="rl-badge rl-badge-dlc">${esc(opts.extraBadge)}</span>` : "",
    r.group && r.group !== "core"
      ? `<span class="rl-badge rl-badge-dlc">${esc(foodGroupLabel(r.group))}</span>`
      : "",
    !intermediate ? `<span class="rl-badge rl-badge-score">⭐ ${r.score ?? 0}</span>` : "",
  ].join("");

  const groupsHtml =
    merged.length > 0
      ? merged.map((g) => rlGroupHtml(g, opts.ingredientName)).join("")
      : (r.ingredients ?? []).length > 0
        ? plainGroupHtml(r.ingredients ?? [], opts.ingredientName)
        : '<div class="rl-group"><span class="muted small">组成信息缺失</span></div>';

  const prodIcon = opts.iconSrc
    ? `<img loading="lazy" src="${esc(opts.iconSrc(r))}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`
    : recipeImgHtml(r);

  return `<article class="rl-card${opts.disabledReason ? " rl-card-disabled" : ""}" title="${esc(opts.disabledReason ? `${r.id}（${opts.disabledReason}）` : r.id)}">
    <div class="rl-product">
      ${prodIcon}
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
