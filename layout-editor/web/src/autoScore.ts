import type { RecipeEntry } from "./types";
import { foodGroupLabel } from "./ingredientLabels";

// 拟合常量：用 oc1_story 全部 28 张官方图（448 个星级数据点，3 组随机种子各 30 万次
// 坐标下降取最优）最小二乘拟合，每关带独立难度系数 d 时平均相对误差约 11.9%。模型：
//   星级分 = roundTime ÷ 单菜耗时 × 人数效率 × 平均菜价 × 星级比例 × 难度系数
// 官方图的 d 分布为 0.35(5-6) ~ 1.90(1-4)，几何均值 ≈ 1.0，故新图默认 1.0x。
// 注：3P/4P 效率在官方拟合值(2.18/2.36)基础上上调，让多人分数拉开差距（残差 9.5%→10.4%）。
const SEC_PER_INGREDIENT = 9.2;
const SEC_PER_COOK_STEP = 10.6;
const SEC_FIXED_OVERHEAD = 5.1;
const PLAYER_EFFICIENCY = [1.0, 1.65, 2.3, 2.6];
const STAR_RATIOS = [0.26, 0.58, 0.91, 1.43];

export const RATIO_MIN = 0.1;
export const RATIO_MAX = 5.0;
export const RATIO_STEP = 0.1;

// 节奏参数默认值：对齐 oc1_story 官方图（订单间隔 1P=15 其余=10，回盘恒 7 秒）
export const ORDER_INTERVAL_SEC = [15, 10, 10, 10];
export const PLATE_RETURN_SEC = 7;
// 订单超时 = 最复杂菜谱估时 × 倍率：1P 取 1.75~2.5 的中值 2.0，人越多节奏越快。
// 验证：T≈90（复杂菜谱）→ 180/135/120/110，与官方图 180/130/130/100 吻合；
//       T≈50（简单菜谱）→ 100/75/70/60，与 jia_level1_1 的 100/90/75/75 同量级。
const ORDER_LIFE_MULT = [2.0, 1.5, 1.35, 1.2];
const ORDER_LIFE_MIN = 60;
const ORDER_LIFE_MAX = 250;

/** 由最复杂菜谱的估时推算 1P~4P 的订单超时（秒，取整到 5 并限制在 60~250）。 */
export function computeOrderLifeTimes(maxRecipeTimeSec: number): number[] {
  return ORDER_LIFE_MULT.map((m) =>
    Math.min(ORDER_LIFE_MAX, Math.max(ORDER_LIFE_MIN, round5(maxRecipeTimeSec * m)))
  );
}

export interface AutoScoreRecipeDetail {
  name: string;
  groupLabel: string;
  ingredientCount: number;
  cookingStepCount: number;
  score: number;
  timeSec: number;
}

export interface AutoScoreResult {
  details: AutoScoreRecipeDetail[];
  avgTimeSec: number;
  maxTimeSec: number;
  avgPrice: number;
  stars: number[][];
}

export function round5(v: number): number {
  return Math.max(5, Math.round(v / 5) * 5);
}

function recipeTimeSec(r: RecipeEntry): { timeSec: number; ing: number; cook: number } {
  const ing = r.ingredientCount ?? r.ingredients?.length ?? 1;
  const cook = r.cookingStepCount ?? (r.cookingStep ? 1 : 0);
  return {
    timeSec: ing * SEC_PER_INGREDIENT + cook * SEC_PER_COOK_STEP + SEC_FIXED_OVERHEAD,
    ing,
    cook,
  };
}

export function computeAutoScores(recipes: RecipeEntry[], roundTimes: number[]): AutoScoreResult | null {
  const usable = recipes.filter((r) => (r.score ?? 0) > 0);
  if (usable.length === 0) return null;

  const details: AutoScoreRecipeDetail[] = usable.map((r) => {
    const t = recipeTimeSec(r);
    return {
      name: r.nameZh || r.id,
      groupLabel: foodGroupLabel(r.group),
      ingredientCount: t.ing,
      cookingStepCount: t.cook,
      score: r.score ?? 0,
      timeSec: t.timeSec,
    };
  });

  const avgTimeSec = details.reduce((s, d) => s + d.timeSec, 0) / details.length;
  const maxTimeSec = Math.max(...details.map((d) => d.timeSec));
  const avgPrice = details.reduce((s, d) => s + d.score, 0) / details.length;

  const stars: number[][] = [];
  for (let p = 0; p < 4; p++) {
    const rt = roundTimes[p] > 0 ? roundTimes[p] : 240;
    const maxScore = (rt / avgTimeSec) * PLAYER_EFFICIENCY[p] * avgPrice;
    stars.push(STAR_RATIOS.map((r) => round5(maxScore * r)));
  }
  return { details, avgTimeSec, maxTimeSec, avgPrice, stars };
}

export function applyRatio(baseStars: number[], ratio: number): number[] {
  return baseStars.map((v) => round5(v * ratio));
}
