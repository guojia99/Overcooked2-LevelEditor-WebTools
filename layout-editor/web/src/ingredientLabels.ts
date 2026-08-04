import type { FoodGroup, IngredientEntry } from "./types";

const FOOD_GROUP_ZH: Record<string, string> = {
  core: "基础",
  custom: "自定义",
  dlc02: "DLC2 海滩",
  dlc05: "DLC5 露营",
  levelset: "本关卡集",
};

export function foodGroupLabel(group: FoodGroup | undefined): string {
  if (!group) return "";
  return FOOD_GROUP_ZH[group] ?? group;
}

export function foodGroupBadge(group: FoodGroup | undefined): string {
  const label = foodGroupLabel(group);
  return label && group !== "core" ? `[${label}] ` : "";
}

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

/** Picker lists: 中文 · English（DLC 项带分组徽标） */
export function ingredientOptionLabel(ing: IngredientEntry): string {
  const en = (ing.nameEn && ing.nameEn.trim()) || ing.id;
  return `${foodGroupBadge(ing.group)}${ing.nameZh} · ${en}`;
}
