import type { RecipeEntry } from "./types";
import { foodGroupLabel } from "./ingredientLabels";
import {
  CHOP_COUNTER_RULE,
  MODEL_CALIBRATION,
  PICKUP_SEC,
  PLATE_TAKE_SEC,
  QUEUE_PENALTY,
  SERVE_ACTION_SEC,
  STEP_TO_UTENSIL,
  UTENSIL_RULES,
  ingredientNeedsChop,
} from "./autoScoreKnowledge";
import {
  effectiveChopPerItemSec,
  hazardMultiplier,
  type KitchenStats,
} from "./kitchenAnalysis";

// 星级模型（保持官方拟合框架）：
//   星级分 = roundTime ÷ 单菜耗时 × 人数效率 × 平均菜价 × 星级比例 × 难度系数
// 官方 oc1_story 全部 28 张图（448 个星级数据点）拟合；官方图难度系数 d 分布
// 0.35(5-6) ~ 1.90(1-4)，几何均值 ≈ 1.0，故新图默认 1.0x。
// 注：3P/4P 效率在官方拟合值(2.18/2.36)基础上上调，让多人分数拉开差距（残差 9.5%→10.4%）。
const PLAYER_EFFICIENCY = [1.0, 1.65, 2.3, 2.6];
const STAR_RATIOS = [0.26, 0.58, 0.91, 1.43];

// ---------- 通用线性回退模型（无场景布局数据时） ----------
// 用 oc1_story 全部 28 张官方图最小二乘拟合，每关带独立难度系数 d 时平均相对误差约 11.9%。
const SEC_PER_INGREDIENT = 9.2;
const SEC_PER_COOK_STEP = 10.6;
const SEC_FIXED_OVERHEAD = 5.1;

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
  /** 分解估时（秒，按图定分模型；回退模型下均为 0 且 timeSec=线性估时）。 */
  fetchSec: number;
  prepSec: number;
  cookSec: number;
  serveSec: number;
}

export interface AutoScoreResult {
  details: AutoScoreRecipeDetail[];
  avgTimeSec: number;
  maxTimeSec: number;
  avgPrice: number;
  stars: number[][];
  /** kitchen = 按图定分（设备感知+步行）；linear = 通用线性回退。 */
  model: "kitchen" | "linear";
  /** 按图定分模型下的厨房风险乘数。 */
  hazardMult: number;
}

export function round5(v: number): number {
  return Math.max(5, Math.round(v / 5) * 5);
}

/** 菜谱工序步骤（锅具种类）：cookingGroups 优先，回退 cookingStep。 */
function recipeUtensilKinds(r: RecipeEntry): string[] {
  const groups = (r as { cookingGroups?: Array<{ step: string }> }).cookingGroups;
  const fromGroups = (groups ?? []).map((g) => g.step).filter((s) => STEP_TO_UTENSIL[s] !== undefined);
  if (fromGroups.length) return fromGroups.map((s) => STEP_TO_UTENSIL[s]);
  const step = r.cookingStep ?? "";
  return STEP_TO_UTENSIL[step] !== undefined ? [STEP_TO_UTENSIL[step]] : [];
}

/** 锅具稀缺排队伍乘数：同类锅具数量 < 并发需求（≈人数）时罚项。 */
function utensilQueueMult(kitchen: KitchenStats, kind: string, players: number): number {
  const info = kitchen.utensils[kind as keyof typeof kitchen.utensils];
  const supply = info?.count ?? 0;
  if (supply >= players) return 1;
  const demand = Math.max(1, players);
  return 1 + QUEUE_PENALTY * ((demand - supply) / demand);
}

/** 按图定分：单菜耗时分解（取材/切配/烹饪/装上）。 */
function kitchenRecipeTimeSec(
  r: RecipeEntry,
  kitchen: KitchenStats,
  players: number
): { total: number; fetchSec: number; prepSec: number; cookSec: number; serveSec: number } {
  const ingredients = r.ingredients ?? [];
  const choppedCount = ingredients.filter((i) => ingredientNeedsChop(i)).length;

  // 取材：每份取料 + 步行（需切食材先送切配区，其余直送烹饪区）
  const fetchWalk = choppedCount > 0 && kitchen.walk.srcToPrepSec > 0 ? kitchen.walk.srcToPrepSec : kitchen.walk.srcToCookSec;
  const fetchSec = ingredients.length * PICKUP_SEC + ingredients.length * fetchWalk;

  // 切配：断头台/切菜台加权吞吐（effectiveChopPerItemSec 已含按钮开销摊派）
  const chopPerItem = effectiveChopPerItemSec(kitchen) ?? CHOP_COUNTER_RULE.perItemSec;
  const prepWalk = choppedCount > 0 ? kitchen.walk.prepToCookSec : 0;
  const prepSec = choppedCount * chopPerItem + choppedCount * prepWalk;

  // 烹饪：各工序基准时长（场景 stub 优先）× 锅具排队罚项
  let cookSec = 0;
  for (const kind of recipeUtensilKinds(r)) {
    const info = kitchen.utensils[kind as keyof typeof kitchen.utensils];
    const base = info?.cookSec ?? UTENSIL_RULES[kind as keyof typeof UTENSIL_RULES]?.cookSec ?? 12;
    cookSec += base * utensilQueueMult(kitchen, kind, players);
  }

  // 装盘上菜：取餐具 + 步行 + 上菜动作
  const serveSec = PLATE_TAKE_SEC + kitchen.walk.cookToPlateSec + kitchen.walk.cookToServeSec + SERVE_ACTION_SEC;

  const total = (fetchSec + prepSec + cookSec + serveSec) * MODEL_CALIBRATION * hazardMultiplier(kitchen);
  return { total, fetchSec, prepSec, cookSec, serveSec };
}

export function computeAutoScores(
  recipes: RecipeEntry[],
  roundTimes: number[],
  kitchen?: KitchenStats | null
): AutoScoreResult | null {
  const usable = recipes.filter((r) => (r.score ?? 0) > 0);
  if (usable.length === 0) return null;

  // 锅具并发需求参考值：单菜估时对 1P~4P 共用，取中值 2 档估排队
  const players = 2;

  const useKitchen = !!kitchen && kitchen.coreItemCount > 0;
  const details: AutoScoreRecipeDetail[] = usable.map((r) => {
    const ing = r.ingredientCount ?? r.ingredients?.length ?? 1;
    const cook = r.cookingStepCount ?? (r.cookingStep ? 1 : 0);
    let timeSec: number;
    let fetchSec = 0;
    let prepSec = 0;
    let cookSec = 0;
    let serveSec = 0;
    if (useKitchen) {
      const t = kitchenRecipeTimeSec(r, kitchen!, players);
      timeSec = t.total;
      fetchSec = t.fetchSec;
      prepSec = t.prepSec;
      cookSec = t.cookSec;
      serveSec = t.serveSec;
    } else {
      timeSec = ing * SEC_PER_INGREDIENT + cook * SEC_PER_COOK_STEP + SEC_FIXED_OVERHEAD;
    }
    return {
      name: r.nameZh || r.id,
      groupLabel: foodGroupLabel(r.group),
      ingredientCount: ing,
      cookingStepCount: cook,
      score: r.score ?? 0,
      timeSec,
      fetchSec,
      prepSec,
      cookSec,
      serveSec,
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
  return {
    details,
    avgTimeSec,
    maxTimeSec,
    avgPrice,
    stars,
    model: useKitchen ? "kitchen" : "linear",
    hazardMult: useKitchen ? hazardMultiplier(kitchen!) : 1,
  };
}

export function applyRatio(baseStars: number[], ratio: number): number[] {
  return baseStars.map((v) => round5(v * ratio));
}
