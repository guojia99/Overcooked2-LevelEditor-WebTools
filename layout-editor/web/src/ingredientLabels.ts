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
};

export function foodGroupLabel(group: FoodGroup | undefined): string {
  if (!group) return "";
  return FOOD_GROUP_ZH[group] ?? group;
}

export function foodGroupBadge(group: FoodGroup | undefined): string {
  const label = foodGroupLabel(group);
  return label && group !== "core" ? `[${label}] ` : "";
}

/** 展示层去重：同 id 时隐藏通用内容项，保留 levelset 组条目。 */
export function visibleIngredients(ingredients: IngredientEntry[]): IngredientEntry[] {
  const levelsetIds = new Set(
    ingredients.filter((i) => i.group === "levelset").map((i) => i.id)
  );
  return ingredients.filter((i) => {
    if (i.group === "levelset") return true;
    if (levelsetIds.has(i.id)) return false;
    return true;
  });
}

/** 菜谱展示层去重（同 visibleIngredients 规则）。 */
export function visibleRecipes<T extends { id: string; group?: string; assetPath?: string }>(recipes: T[]): T[] {
  const levelsetIds = new Set(recipes.filter((r) => r.group === "levelset").map((r) => r.id));
  return recipes.filter((r) => {
    if (r.group === "levelset") return true;
    if (levelsetIds.has(r.id)) return false;
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

/** 食材简单分类（选择器分类 chips 用）。按顺序匹配 id，先命中先归。 */
export const INGREDIENT_CATEGORIES: { key: string; label: string }[] = [
  { key: "vegetable", label: "蔬菜" },
  { key: "fruit", label: "水果" },
  { key: "meat", label: "肉类·水产" },
  { key: "dairy", label: "乳蛋·烘焙" },
  { key: "staple", label: "主食" },
  { key: "drink", label: "饮料" },
  { key: "sauce", label: "酱料" },
  { key: "other", label: "其他" },
];

const INGREDIENT_CATEGORY_RULES: [string, RegExp][] = [
  // 饮料：冰块/汽水/可乐/饮料机饮料
  ["drink", /icecube|soda|rootbeer|^drink\d+$/],
  // 酱料：番茄酱/芥末酱（node 型浇头）
  ["sauce", /ketchup|mustard/],
  // 主食：米/面/意面/玉米饼/面包/饼干
  ["staple", /rice|noodle|pasta|tortilla|bun|cracker/],
  // 乳蛋·烘焙：蛋/面粉/面团/奶酪/巧克力/蜂蜜/奶/奶油/棉花糖/香草
  ["dairy", /egg|flour|dough|cheese|chocolate|honeycomb|milk|cream|marshmallow|vanilla/],
  // 肉类·水产：肉/鸡/火鸡/培根/香肠/热狗肠/烤肉/鱼/虾/鸡块/辣香肠
  ["meat", /meat|chicken|turkey|bacon|sausage|frankfurter|roast|fish|prawn|nugget|pepperoni/],
  // 水果：橙/苹果/葡萄/桃/樱桃/莓/瓜/香蕉/菠萝/果干
  ["fruit", /orange|apple|grape|peach|cherry|berry|melon|banana|pineapple|driedfruit/],
  // 蔬菜：生菜/番茄/黄瓜/胡萝卜/洋葱/土豆/西兰花/西芹/白菜/玉米/蘑菇/海苔/豆/橄榄
  ["vegetable", /lettuce|tomato|cucumber|carrot|onion|potato|broccoli|leek|bokchoy|corn|mushroom|seaweed|beans|olive/],
];

/** 食材 id → 分类 key（未命中归 other）。 */
export function ingredientCategoryOf(id: string): string {
  const lower = (id ?? "").toLowerCase();
  for (const [key, re] of INGREDIENT_CATEGORY_RULES) {
    if (re.test(lower)) return key;
  }
  return "other";
}

/** 分类 key → 中文标签。 */
export function ingredientCategoryLabel(key: string): string {
  return INGREDIENT_CATEGORIES.find((c) => c.key === key)?.label ?? key;
}
