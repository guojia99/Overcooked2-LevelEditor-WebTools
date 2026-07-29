import type { IngredientEntry } from "./types";

export function ingredientByGuid(
  ingredients: IngredientEntry[],
  guid: string | undefined
): IngredientEntry | undefined {
  if (!guid) return undefined;
  return ingredients.find((i) => i.guid === guid);
}

/** Detail panel / status: Chinese only. */
export function ingredientNameZh(
  ingredients: IngredientEntry[],
  guid: string | undefined
): string {
  if (!guid) return "未设置";
  const ing = ingredientByGuid(ingredients, guid);
  return ing?.nameZh ?? "未知食材";
}

/** Picker lists: 中文 · English */
export function ingredientOptionLabel(ing: IngredientEntry): string {
  const en = (ing.nameEn && ing.nameEn.trim()) || ing.id;
  return `${ing.nameZh} · ${en}`;
}
