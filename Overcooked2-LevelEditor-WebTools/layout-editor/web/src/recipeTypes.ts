import type { RecipeEntry } from "./types";

export const RECIPE_TYPE_ZH: Record<string, string> = {
  burger: "汉堡",
  mealdeal: "套餐",
  burrito: "卷饼",
  pizza: "披萨",
  pasta: "意面",
  sushi: "寿司",
  salad: "沙拉",
  soup: "汤",
  hotdog: "热狗",
  hotchocolate: "热可可",
  hotpot: "火锅",
  roast: "烤菜",
  pie: "水果派",
  pudding: "布丁",
  moonpie: "月饼",
  fruitplatter: "水果拼盘",
  float: "冰淇淋汽水",
  icecream: "冰淇淋",
  donut: "甜甜圈",
  fry: "炸物",
  steamed: "蒸菜",
  cake: "蛋糕",
  pancake: "松饼",
  smoothie: "果汁",
  kebab: "烤串",
  breakfast: "早餐",
  smores: "棉花糖饼干",
  batter: "面糊（半成品）",
  custom: "自定义菜谱",
  other: "其他",
};

export const RECIPE_TYPE_ORDER = [
  "burger",
  "mealdeal",
  "burrito",
  "pizza",
  "pasta",
  "sushi",
  "salad",
  "soup",
  "hotdog",
  "hotchocolate",
  "hotpot",
  "roast",
  "pie",
  "pudding",
  "moonpie",
  "fruitplatter",
  "float",
  "icecream",
  "donut",
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
