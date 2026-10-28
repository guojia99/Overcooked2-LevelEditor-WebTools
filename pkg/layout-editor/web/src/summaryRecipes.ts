/** 汇总页 / 分工模式页共用的菜谱分组：
 *  - 官方菜谱：按类型分组（RECIPE_TYPE_ORDER 排序），如 汉堡 / 寿司 / 蛋糕…
 *  - 自定义菜谱：独立区块，按 category（关卡集 CustomRecipeConfig，如 "burger"）
 *    → subcategory（分类目录下的子目录）两级子分组；
 *  - 每个分组 / 子分类内部默认按分数升序（未配置视为 0，同分保持关卡菜谱原顺序），
 *    分组本身的排列顺序不受分数影响。
 *
 *  分类显示名优先取桥接下发的 CustomRecipeConfig（含 commonW2 Burger大全的
 *  子分类表），配置缺失时回退 RECIPE_TYPE_ZH / id 原文。 */
import type { CustomRecipeConfig, RecipeEntry } from "./types";
import { CUSTOM_CATEGORY_ORDER, customCategoryLabel, groupRecipesByType, recipeTypeLabel } from "./recipeTypes";

export interface SummarySubGroup {
  key: string;
  label: string;
  recipes: RecipeEntry[];
}

export interface SummaryGroup {
  key: string;
  label: string;
  recipes: RecipeEntry[];
  /** 二级分类子组（该分类下存在任一非空 subcategory 时启用，否则单一 grid）。 */
  subs?: SummarySubGroup[];
}

export interface SummaryGroups {
  /** 官方菜谱（非 isCustom），按 RECIPE_TYPE_ORDER。 */
  official: SummaryGroup[];
  /** 自定义菜谱独立区块，按 category 配置顺序。 */
  custom: SummaryGroup[];
}

/** 分组/子分类内部排序：分数升序（未配置视为 0）；Array.sort 稳定，同分保持原顺序。 */
export function byScoreAsc(a: RecipeEntry, b: RecipeEntry): number {
  return (a.score ?? 0) - (b.score ?? 0);
}

/** 未分类兜底显示名。 */
const UNCAT = "未分类";

interface CustomGroupingRow {
  recipe: RecipeEntry;
  category: string;
  subcategory: string;
}

/** commonW3 / commonW2 / LevelSets 的自定义菜谱都按 custom_recipes/<category>[/<sub>]/ 存放。 */
function customCategoryInfoFromAssetPath(assetPath: string | undefined): {
  category: string;
  subcategory: string;
} {
  const path = (assetPath ?? "").replace(/\\/g, "/");
  const marker = "/custom_recipes/";
  const idx = path.toLowerCase().indexOf(marker);
  if (idx < 0) return { category: "", subcategory: "" };
  const parts = path.slice(idx + marker.length).split("/").filter(Boolean);
  if (parts.length < 2) return { category: "", subcategory: "" };
  return {
    category: parts[0],
    subcategory: parts.length >= 3 ? parts[1] : "",
  };
}

function groupingCategory(row: { category?: string; subcategory?: string; assetPath?: string }): {
  category: string;
  subcategory: string;
} {
  const fromPath = customCategoryInfoFromAssetPath(row.assetPath);
  const apiCat = (row.category ?? "").trim();
  const category =
    apiCat && apiCat.toLowerCase() !== "uncategorized" ? apiCat : (fromPath.category || apiCat);
  const subcategory = (row.subcategory ?? "").trim() || fromPath.subcategory;
  return { category, subcategory };
}

export function buildSummaryGroups(selected: RecipeEntry[], customConfig: CustomRecipeConfig | null): SummaryGroups {
  const official: SummaryGroup[] = groupRecipesByType(selected.filter((r) => !r.isCustom)).map(([type, arr]) => ({
    key: type,
    label: recipeTypeLabel(type),
    recipes: [...arr].sort(byScoreAsc),
  }));

  const categories = customConfig?.categories ?? [];
  const subcategories = customConfig?.subcategories ?? [];
  const catLabel = (id: string): string =>
    categories.find((c) => c.id === id)?.zh ?? customCategoryLabel(id || "other");
  const subLabel = (cat: string, id: string): string =>
    subcategories.find((s) => s.parent === cat && s.id === id)?.zh ?? (id || UNCAT);

  const catOrder = new Map(categories.map((c, i) => [c.id, i]));
  const fallbackCatOrder = new Map(CUSTOM_CATEGORY_ORDER.map((id, i) => [id, 1000 + i]));
  const rows: CustomGroupingRow[] = [];
  for (const r of selected) {
    if (!r.isCustom) continue;
    const grouping = groupingCategory(r);
    rows.push({ recipe: r, category: grouping.category, subcategory: grouping.subcategory });
  }

  const byCat = new Map<string, CustomGroupingRow[]>();
  for (const row of rows) {
    let list = byCat.get(row.category);
    if (!list) {
      list = [];
      byCat.set(row.category, list);
    }
    list.push(row);
  }
  const catSortOrder = (id: string): number => {
    if (catOrder.has(id)) return catOrder.get(id) as number;
    return fallbackCatOrder.get(id) ?? 9999;
  };
  const catKeys = [...byCat.keys()].sort((a, b) => {
    if (!a) return -1;
    if (!b) return 1;
    const oa = catSortOrder(a);
    const ob = catSortOrder(b);
    if (oa !== ob) return oa - ob;
    return a.localeCompare(b);
  });

  const custom: SummaryGroup[] = catKeys.map((cat) => {
    const list = byCat.get(cat) ?? [];
    const label = cat ? catLabel(cat) : UNCAT;
    const sorted = list.map((row) => row.recipe).sort(byScoreAsc);
    const hasSubs = list.some((row) => !!row.subcategory);
    if (!hasSubs) {
      return { key: cat || "_uncat", label, recipes: sorted };
    }
    // 二级分类：根级（""）最前，其余按配置 order 字段，未知子分类按 id 垫后
    const subOrderOf = (id: string): number => {
      if (!id) return -1;
      const s = subcategories.find((x) => x.parent === cat && x.id === id);
      return s ? s.order : 9999;
    };
    const bySub = new Map<string, CustomGroupingRow[]>();
    for (const row of list) {
      const sub = row.subcategory;
      let arr = bySub.get(sub);
      if (!arr) {
        arr = [];
        bySub.set(sub, arr);
      }
      arr.push(row);
    }
    const subKeys = [...bySub.keys()].sort((a, b) => {
      const oa = subOrderOf(a);
      const ob = subOrderOf(b);
      if (oa !== ob) return oa - ob;
      if (!a) return -1;
      if (!b) return 1;
      return a.localeCompare(b);
    });
    const subs: SummarySubGroup[] = subKeys.map((sub) => {
      const subRows = bySub.get(sub) ?? [];
      return {
        key: sub || "_root",
        label: sub ? subLabel(cat, sub) : UNCAT,
        recipes: subRows.map((row) => row.recipe).sort(byScoreAsc),
      };
    });
    return { key: cat || "_uncat", label, recipes: sorted, subs };
  });

  return { official, custom };
}
