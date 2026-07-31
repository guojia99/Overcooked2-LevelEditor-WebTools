import type { RecipeEntry } from "./types";

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

export interface AutoScoreRecipeDetail {
  name: string;
  ingredientCount: number;
  cookingStepCount: number;
  score: number;
  timeSec: number;
}

export interface AutoScoreResult {
  details: AutoScoreRecipeDetail[];
  avgTimeSec: number;
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
      ingredientCount: t.ing,
      cookingStepCount: t.cook,
      score: r.score ?? 0,
      timeSec: t.timeSec,
    };
  });

  const avgTimeSec = details.reduce((s, d) => s + d.timeSec, 0) / details.length;
  const avgPrice = details.reduce((s, d) => s + d.score, 0) / details.length;

  const stars: number[][] = [];
  for (let p = 0; p < 4; p++) {
    const rt = roundTimes[p] > 0 ? roundTimes[p] : 240;
    const maxScore = (rt / avgTimeSec) * PLAYER_EFFICIENCY[p] * avgPrice;
    stars.push(STAR_RATIOS.map((r) => round5(maxScore * r)));
  }
  return { details, avgTimeSec, avgPrice, stars };
}

export function applyRatio(baseStars: number[], ratio: number): number[] {
  return baseStars.map((v) => round5(v * ratio));
}
