import { S } from "./state";
import type { RecipeEntry } from "../types";

export const STEP_UTENSILS: Record<string, string[]> = {
  Pot: ["Cooker", "Pot"],
  FryingPan: ["Cooker", "FryPan"],
  DeepFatFryer: ["FryingStation", "FrierBasket"],
  OvenTray: ["Oven"],
  Steamer: ["Cooker", "Steamer"],
  Mixer: ["Mixer", "MixerBowl", "Oven"],
  Blender: ["Blender", "BlenderCup"],
  GriddlePan: ["Campfire", "GriddlePan"],
  KebabSkewer: ["Barbeque", "Skewer"],
  ToastingFork: ["Campfire", "ToastingFork"],
  MixingBowl: ["Mixer", "MixerBowl"],
};

export const CHOPPABLE_INGREDIENTS = new Set([
  "LettuceSO",
  "TomatoSO",
  "OnionSO",
  "CarrotSO",
  "CucumberSO",
  "MushroomSO",
]);

export const BASE_UTENSILS = ["ServingStation", "Bin", "CleanPlateStack"];

/** 装盘容器 id → 对应的容器堆道具 id（PlatingStep → CleanPlateStack 类）。
 *  关卡菜谱分析区据此推荐盘子堆/杯子堆。 */
export const PLATING_STACK_BY_STEP: Record<string, string> = {
  Plate: "CleanPlateStack",
  Glass: "CleanGlassStack",
};

/** 需要的容器堆：盘子堆默认；含玻璃杯装盘的菜谱时额外要求杯子堆。 */
export function requiredPlateStacks(platingSteps: Iterable<string>): string[] {
  const set = new Set<string>(["CleanPlateStack"]);
  for (const p of platingSteps) {
    const stack = PLATING_STACK_BY_STEP[p];
    if (stack) set.add(stack);
  }
  return [...set];
}

export const INTERMEDIATE_ASSIGN: Record<string, Record<string, string[]>> = {
  pancake: { "FryPan": ["MixedFlourEggStrawberry", "MixedFlourEggBlueberry"], "MixerBowl": ["MixedFlourEggStrawberry", "MixedFlourEggBlueberry"] },
  pancake_strawberry: { "FryPan": ["MixedFlourEggStrawberry"], "MixerBowl": ["MixedFlourEggStrawberry"] },
  pancake_blueberry: { "FryPan": ["MixedFlourEggBlueberry"], "MixerBowl": ["MixedFlourEggBlueberry"] },
  burger: { "FryPan": ["FriedMeat"] },
  fry_chips: { "FrierBasket": ["FriedPotato"] },
  fry_fish: { "FrierBasket": ["FriedFish"] },
};

export function intermediateKeysForRecipe(r: RecipeEntry): string[] {
  const keys = new Set<string>();
  const id = r.id ?? "";
  const type = r.type ?? "";
  const ings = r.ingredients ?? [];
  const step = r.cookingStep ?? "";

  // 面糊（半成品）→ 煎锅 + 搅拌碗
  const isPancake = type === "pancake" || id.includes("Pancake") || id === "Cake_Chocolate_SO" || id === "Cake_Plain_SO";
  if (isPancake) {
    const strawberry = ings.includes("PancakeStrawberry") || id.includes("Strawberry");
    const blueberry = ings.includes("Blueberry") || id.includes("Blueberry");
    if (strawberry) keys.add("pancake_strawberry");
    if (blueberry) keys.add("pancake_blueberry");
    if (!strawberry && !blueberry) keys.add("pancake");
  }
  // 肉排 → 煎锅
  if (type === "burger" || id.startsWith("Burger_") || (step === "FryingPan" && ings.includes("MeatSO"))) {
    keys.add("burger");
  }
  // 炸薯条 / 炸鱼排 → 炸锅（炸篮）
  const isFry = type === "fry" || step === "DeepFatFryer" || id.startsWith("Fry_");
  if (isFry) {
    if (ings.includes("PotatoSO") || id === "Fry_Chips_SO" || id === "Fry_All_SO") keys.add("fry_chips");
    if (ings.includes("FishSO") || id.includes("Fish")) keys.add("fry_fish");
  }
  return [...keys];
}

export const STEP_CONTAINER: Record<string, string> = {
  FryingPan: "FryPan",
  MixingBowl: "MixerBowl",
  DeepFatFryer: "FrierBasket",
  Pot: "Pot",
  Steamer: "Steamer",
  GriddlePan: "GriddlePan",
  KebabSkewer: "Skewer",
  ToastingFork: "ToastingFork",
  Mixer: "MixerBowl",
};

export function leafIngredientIds(id: string): string[] {
  const inter = S.intermediatesCache.find((x) => x.id === id);
  const ings = inter?.ingredients;
  return ings && ings.length > 0 ? ings : [id];
}

export function computeIntermediatesForUtensils(recipes: RecipeEntry[]): Map<string, string[]> {
  const result = new Map<string, string[]>();
  const add = (ut: string, iid: string) => {
    if (!result.has(ut)) result.set(ut, []);
    if (!result.get(ut)!.includes(iid)) result.get(ut)!.push(iid);
  };
  for (const r of recipes) {
    for (const key of intermediateKeysForRecipe(r)) {
      const assign = INTERMEDIATE_ASSIGN[key];
      if (!assign) continue;
      for (const [ut, iids] of Object.entries(assign)) {
        for (const iid of iids) {
          for (const ing of leafIngredientIds(iid)) add(ut, ing);
        }
      }
    }
  }
  // 泛化兜底：用户自建的中间产物/自定义菜谱（如煎蛋）按「叶食材 ⊂ 菜谱叶食材 + 步骤匹配」
  // 自动分配——同样只分配叶食材（鸡蛋），而非菜谱本身。
  for (const r of recipes) {
    const leafs = new Set(r.ingredients ?? []);
    for (const inter of S.intermediatesCache) {
      const step = inter.cookingStep ?? "";
      const container = STEP_CONTAINER[step];
      if (!container) continue;
      const ings = inter.ingredients ?? [];
      if (ings.length === 0 || !ings.every((i) => leafs.has(i))) continue;
      for (const ing of ings) add(container, ing);
    }
  }
  return result;
}

export function computeRequiredUtensils(ingredientIds: Set<string>, steps: Set<string>, platingSteps?: Iterable<string>): string[] {
  const set = new Set<string>(BASE_UTENSILS);
  // 装盘容器堆：盘子堆默认；玻璃杯装盘的菜谱需要杯子堆
  if (platingSteps) {
    for (const s of requiredPlateStacks(platingSteps)) set.add(s);
  }
  for (const s of steps) (STEP_UTENSILS[s] ?? []).forEach((u) => set.add(u));
  // 自定义菜谱/中间产物（递归组装策略）：其叶食材 ⊆ 已选菜谱叶食材时，
  // 该菜谱自身的烹饪步骤也产生锅具需求（如鸡蛋汉堡 → 煎蛋 → 煎锅）。
  for (const inter of S.intermediatesCache) {
    const step = inter.cookingStep ?? "";
    if (!step) continue;
    const ings = inter.ingredients ?? [];
    if (ings.length === 0 || !ings.every((i) => ingredientIds.has(i))) continue;
    (STEP_UTENSILS[step] ?? []).forEach((u) => set.add(u));
  }
  for (const ing of ingredientIds) {
    if (CHOPPABLE_INGREDIENTS.has(ing)) set.add("ChoppingCounter");
    if (ing === "FlourSO") {
      set.add("Mixer");
      set.add("MixerBowl");
    }
    if (ing === "SushiRiceSO" || ing === "RiceSO") {
      set.add("Cooker");
      set.add("Pot");
    }
    if (ing === "PastaSO") {
      set.add("FryPan");
    }
  }
  return [...set];
}
