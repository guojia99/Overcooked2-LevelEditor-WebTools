/**
 * 自定义菜谱卡片归一化（共享给自定义菜谱列表、汇总页等所有卡片渲染处）。
 *
 * 自定义菜谱（levelset）渲染卡片时，必须与「组装效果（实时预览）」一致：
 *  - cookingStep = cookingStepId（真实烹饪步骤）
 *  - mixing = type === "Mixed"（Mixed 类型 → 先搅拌再烹饪，卡片双步骤）
 *  - intermediate 恒 false（所有自定义菜谱均可作组成/订单菜谱）
 *  - 不携带后端 cookingGroups（后端按旧 score<=0 中间产物语义计算，不含
 *    mixing/分步组成），统一走前端镜像推导（recipeCard.computeCardGroups）。
 */
import type { CustomRecipeSummary, RecipeEntry } from "./types";

export function normalizeCustomRecipeCard(r: CustomRecipeSummary): RecipeEntry {
  return {
    guid: r.guid,
    id: r.id,
    nameZh: r.nameZh,
    nameEn: r.nameEn || undefined,
    assetPath: r.assetPath,
    cookingStep: r.cookingStepId || undefined,
    ingredients: r.ingredients,
    compositionIds: r.compositionIds,
    score: r.score,
    isCustom: true,
    intermediate: false,
    mixing: r.type === "Mixed",
    group: r.group,
    type: "custom",
    // 不携带 cookingGroups：由 recipeCard.computeCardGroups 统一走前端推导
    cookingGroups: undefined,
  } as RecipeEntry;
}
