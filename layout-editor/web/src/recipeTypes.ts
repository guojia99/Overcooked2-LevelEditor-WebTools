import type { RecipeEntry } from "./types";

export const RECIPE_TYPE_ZH: Record<string, string> = {
  burger: "汉堡",
  burrito: "卷饼",
  pizza: "披萨",
  pasta: "意面",
  sushi: "寿司",
  salad: "沙拉",
  soup: "汤",
  fry: "炸物",
  steamed: "蒸菜",
  cake: "蛋糕",
  pancake: "松饼",
  smoothie: "冰沙",
  kebab: "烤串",
  breakfast: "早餐",
  smores: "棉花糖饼干",
  batter: "面糊（半成品）",
  other: "其他",
};

export const RECIPE_TYPE_ORDER = [
  "burger",
  "burrito",
  "pizza",
  "pasta",
  "sushi",
  "salad",
  "soup",
  "fry",
  "steamed",
  "cake",
  "pancake",
  "smoothie",
  "kebab",
  "breakfast",
  "smores",
  "batter",
  "other",
];

export function recipeTypeLabel(type: string | undefined): string {
  return RECIPE_TYPE_ZH[type ?? "other"] ?? type ?? "其他";
}

/** Group recipes by family, preserving RECIPE_TYPE_ORDER. */
export function groupRecipesByType(recipes: RecipeEntry[]): [string, RecipeEntry[]][] {
  const byType = new Map<string, RecipeEntry[]>();
  for (const r of recipes) {
    const t = r.type ?? "other";
    if (!byType.has(t)) byType.set(t, []);
    byType.get(t)!.push(r);
  }
  const order = (t: string) => {
    const i = RECIPE_TYPE_ORDER.indexOf(t);
    return i < 0 ? 99 : i;
  };
  return [...byType.entries()].sort((a, b) => order(a[0]) - order(b[0]));
}
