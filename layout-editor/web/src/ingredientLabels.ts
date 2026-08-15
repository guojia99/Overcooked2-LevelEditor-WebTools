import type { FoodGroup, IngredientEntry } from "./types";

const FOOD_GROUP_ZH: Record<string, string> = {
  core: "基础",
  custom: "自定义",
  dlc02: "DLC2 海滩",
  dlc03: "DLC3 圣诞",
  dlc04: "DLC4 火锅",
  dlc05: "DLC5 露营",
  dlc07: "DLC7 部落",
  dlc08: "DLC8 马戏团",
  dlc09: "DLC9 仙境",
  dlc10: "DLC10 火锅",
  dlc11: "DLC11 饮料",
  dlc13: "DLC13 巧克力",
  levelset: "本关卡集",
  web: "Web内置",
};

export function foodGroupLabel(group: FoodGroup | undefined): string {
  if (!group) return "";
  return FOOD_GROUP_ZH[group] ?? group;
}

export function foodGroupBadge(group: FoodGroup | undefined): string {
  const label = foodGroupLabel(group);
  return label && group !== "core" ? `[${label}] ` : "";
}

/**
 * 展示层去重：同一食材既存在于 Web 内置源库又有关卡集副本时，选择器只显示副本
 * （数据保留全部条目，guid 始终可解析；此处仅隐藏重复项）。
 *  - 旧 custom_ingredients 拷贝（levelset 组）覆盖同 id 的 Web 内置项；
 *  - custom_web 拷贝（web 组）覆盖同 id 的 Import 源库项。
 */
export function visibleIngredients(ingredients: IngredientEntry[]): IngredientEntry[] {
  const levelsetIds = new Set(
    ingredients.filter((i) => i.group === "levelset").map((i) => i.id)
  );
  const webCopiedIds = new Set(
    ingredients
      .filter((i) => i.group === "web" && (i.assetPath ?? "").includes("/custom_web/"))
      .map((i) => i.id)
  );
  return ingredients.filter((i) => {
    if (i.group !== "web") return true;
    if (levelsetIds.has(i.id)) return false;
    if (webCopiedIds.has(i.id) && !(i.assetPath ?? "").includes("/custom_web/")) return false;
    return true;
  });
}

/** 菜谱展示层去重（同 visibleIngredients 规则）。 */
export function visibleRecipes<T extends { id: string; group?: string; assetPath?: string }>(recipes: T[]): T[] {
  const levelsetIds = new Set(recipes.filter((r) => r.group === "levelset").map((r) => r.id));
  const webCopiedIds = new Set(
    recipes.filter((r) => r.group === "web" && (r.assetPath ?? "").includes("/custom_web/")).map((r) => r.id)
  );
  return recipes.filter((r) => {
    if (r.group !== "web") return true;
    if (levelsetIds.has(r.id)) return false;
    if (webCopiedIds.has(r.id) && !(r.assetPath ?? "").includes("/custom_web/")) return false;
    return true;
  });
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
