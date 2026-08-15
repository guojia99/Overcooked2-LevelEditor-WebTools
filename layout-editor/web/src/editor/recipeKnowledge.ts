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
  HotPot: ["cooking_region_floorburner", "utensil_large_pot_01"],
  RoastingTray: ["Oven", "utensil_roasting_tray"],
};

export const CHOPPABLE_INGREDIENTS = new Set([
  "LettuceSO",
  "TomatoSO",
  "OnionSO",
  "CarrotSO",
  "CucumberSO",
  "MushroomSO",
]);

export const FLOUR_INGREDIENTS = new Set(["FlourSO", "dlc09_flour", "dlc13_flour"]);
export const EGG_INGREDIENTS = new Set(["EggSO", "DLC05_Egg", "dlc09_egg", "dlc13_egg"]);

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

export const PLATE_CLEANUP: { dirty: string; sink: string; ret: string }[] = [
  { dirty: "dlc08_dirtytraystack", sink: "Sink", ret: "PlateReturn" },
];
export const GLASS_CLEANUP: { dirty: string; sink: string; ret: string }[] = [
  { dirty: "dlc11_dirtyglassstack", sink: "SinkGlass", ret: "GlassReturn" },
];
export const MUG_CLEANUP: { dirty: string; sink: string; ret: string }[] = [
  { dirty: "dlc09_dirtymugstack", sink: "workstation_sink_mug_01_wood", ret: "workstation_mug_return" },
];

/** 奶油喷罐道具 id（冰淇淋汽水/冰淇淋/奶油类菜谱需要）。 */
export const CREAM_SPRAY_IDS = ["utensil_ingredient_spray_01", "dlc09_utensil_ingredient_spray"];
/** 奶油喷罐可喷出的发泡奶油食材 id。 */
export const CREAM_INGREDIENT_IDS = ["whippedcream", "dlc09_whippedcream"];

/** 识别需要奶油喷罐的菜谱：冰淇淋汽水/冰淇淋类型、id 含 float、或食材含发泡奶油。 */
export function recipeNeedsCreamSpray(r: RecipeEntry): boolean {
  const ings = r.ingredients ?? [];
  if (ings.some((i) => CREAM_INGREDIENT_IDS.includes(i))) return true;
  const t = r.type ?? "";
  if (t === "float" || t === "icecream") return true;
  return /float/i.test(r.id ?? "");
}

export function recipeNeedsMug(r: RecipeEntry): boolean {
  return (r.type ?? "") === "hotchocolate";
}

export function recipeNeedsGlass(r: RecipeEntry): boolean {
  const t = r.type ?? "";
  return t === "float" || t === "icecream" || t === "smoothie";
}

/** DLC 换皮/变体 → 功能等价的基础道具 id：场景里放了变体即视为具备基础功能
 *  （如放了 dlc09_oven 就算有了 Oven），供自动填充/缺失分析使用。 */
export const FUNCTIONAL_BASE: Record<string, string> = {
  dlc08_oven_02: "Oven",
  dlc09_oven: "Oven",
  oven_medieval: "Oven",
  oven_furnace_medieval: "Oven",
  workstation_furnace_01: "Oven",
  dlc13_workstation_cooker_01: "Oven",
  dlc03_utensil_pot: "Pot",
  dlc07_utensil_pot_01: "Pot",
  dlc08_utensil_pot_01: "Pot",
  dlc09_utensil_pot: "Pot",
  dlc07_utensil_frying_pan_01: "FryPan",
  dlc08_utensil_frying_pan: "FryPan",
  dlc09_utensil_frying_pan: "FryPan",
  dlc08_frierbasket: "FrierBasket",
  dlc09_utensil_roasting_tray: "utensil_roasting_tray",
  dlc03_utensil_mixer: "MixerBowl",
  dlc07_utensil_mixer_01: "MixerBowl",
  dlc08_utensil_mixer_01: "MixerBowl",
  dlc09_utensil_mixer: "MixerBowl",
  dlc13_utensil_mixer_01: "MixerBowl",
  workstation_mixer_01: "Mixer",
  workstation_mixer_03: "Mixer",
  dlc10_workstation_mixer: "Mixer",
  dlc08_workstation_mixer: "Mixer",
  dlc13_workstation_mixer_01: "Mixer",
  dlc11_cleanglassstack: "CleanGlassStack",
  dlc11_equipment_glass_01: "Glass",
  dlc09_cleanmugstack: "cleanmugstack",
  dlc09_dirtymug: "dirtymug",
  dlc09_dirtymugstack: "dirtymugstack",
  dlc09_equipment_mug_01: "equipment_mug_01",
  dlc13_workstation_sink_01_wood: "Sink",
  workstation_sink_01_summer: "Sink",
  dlc13_workstation_bin_01: "Bin",
  dlc13_workstation_plate_return: "PlateReturn",
  dlc11_workstation_glass_return_01: "GlassReturn",
  dlc09_workstation_sink_mug_01_wood: "workstation_sink_mug_01_wood",
  dlc09_workstation_mug_return_winter: "workstation_mug_return",
  dlc13_workstation_plate_station: "ServingStation",
  dlc09_utensil_ingredient_spray: "utensil_ingredient_spray_01",
  dlc10_pushable_object: "pushable_object",
  utensil_dlc10_big_ol_spoon: "utensil_big_ol_spoon",
  dlc08_utensil_fire_extinguisher: "FireExtinguisher",
};

/** 把一个 prefab id 归一化为其功能基础 id（变体→基础；非变体→自身）。 */
export function functionalBaseId(id: string): string {
  return FUNCTIONAL_BASE[id] ?? id;
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

export const WORKSTATION_UTENSILS = new Set([
  "Oven",
  "Cooker",
  "Mixer",
  "Blender",
  "FryingStation",
  "Campfire",
  "Barbeque",
]);

export function isFlourBranchRecipe(r: RecipeEntry): boolean {
  const ings = r.ingredients ?? [];
  return ings.some((i) => FLOUR_INGREDIENTS.has(i)) && ings.some((i) => EGG_INGREDIENTS.has(i));
}

export function findBatterIntermediateForRecipe(r: RecipeEntry): RecipeEntry | undefined {
  const leafs = new Set(r.ingredients ?? []);
  let best: RecipeEntry | undefined;
  for (const inter of S.intermediatesCache) {
    const step = inter.cookingStep ?? "";
    if (step !== "Mixer" && step !== "MixingBowl") continue;
    const ings = inter.ingredients ?? [];
    if (ings.length === 0 || !ings.every((i) => leafs.has(i))) continue;
    if (!best || ings.length > (best.ingredients ?? []).length) best = inter;
  }
  return best;
}

export function recipeLacksIntermediate(r: RecipeEntry): boolean {
  return isFlourBranchRecipe(r) && !findBatterIntermediateForRecipe(r);
}

export function missingIntermediateRecipes(recipes: RecipeEntry[]): RecipeEntry[] {
  return recipes.filter(recipeLacksIntermediate);
}

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
  for (const r of recipes) {
    if (!isFlourBranchRecipe(r)) continue;
    const inter = findBatterIntermediateForRecipe(r);
    if (!inter) continue;
    const batter = inter.ingredients ?? [];
    for (const ing of batter) add("MixerBowl", ing);
    const uts = STEP_UTENSILS[r.cookingStep ?? ""] ?? [];
    const vessel = uts[uts.length - 1];
    if (vessel && !WORKSTATION_UTENSILS.has(vessel)) {
      for (const ing of batter) add(vessel, ing);
    }
  }
  // 泛化兜底：用户自建的中间产物/自定义菜谱（如煎蛋）按「叶食材 ⊂ 菜谱叶食材 + 步骤匹配」
  // 自动分配——同样只分配叶食材（鸡蛋），而非菜谱本身。
  for (const r of recipes) {
    const leafs = new Set(r.ingredients ?? []);
    for (const inter of S.intermediatesCache) {
      const step = inter.cookingStep ?? "";
      const container = STEP_CONTAINER[step];
      if (!container || inter.group !== "levelset") continue;
      const ings = inter.ingredients ?? [];
      if (ings.length === 0 || !ings.every((i) => leafs.has(i))) continue;
      for (const ing of ings) add(container, ing);
    }
  }
  return result;
}

export function computeRequiredUtensils(ingredientIds: Set<string>, steps: Set<string>, platingSteps?: Iterable<string>, recipes?: RecipeEntry[]): string[] {
  const set = new Set<string>(["ServingStation", "Bin"]);
  const recs = recipes ?? [];
  const hasGlassPlating = platingSteps ? [...platingSteps].includes("Glass") : false;
  const needMug = recs.some(recipeNeedsMug);
  const needGlass = recs.some(recipeNeedsGlass) || hasGlassPlating;
  const needPlate = recs.length === 0 || recs.some((r) => !recipeNeedsMug(r) && !recipeNeedsGlass(r));
  if (needPlate) {
    set.add("CleanPlateStack");
    for (const c of PLATE_CLEANUP) {
      set.add(c.dirty);
      set.add(c.sink);
      set.add(c.ret);
    }
  }
  if (needGlass) {
    set.add("CleanGlassStack");
    for (const c of GLASS_CLEANUP) {
      set.add(c.dirty);
      set.add(c.sink);
      set.add(c.ret);
    }
  }
  if (needMug) {
    set.add("cleanmugstack");
    for (const c of MUG_CLEANUP) {
      set.add(c.dirty);
      set.add(c.sink);
      set.add(c.ret);
    }
  }
  if (platingSteps) {
    for (const s of requiredPlateStacks(platingSteps)) set.add(s);
  }
  if (recipes) {
    for (const r of recipes) {
      if (recipeNeedsCreamSpray(r)) {
        for (const id of CREAM_SPRAY_IDS) set.add(id);
      }
    }
  }
  for (const s of steps) (STEP_UTENSILS[s] ?? []).forEach((u) => set.add(u));
  for (const r of recs) {
    if (isFlourBranchRecipe(r)) {
      set.add("Mixer");
      set.add("MixerBowl");
    }
  }
  // 自定义菜谱/中间产物（递归组装策略）：其叶食材 ⊆ 已选菜谱叶食材时，
  // 该菜谱自身的烹饪步骤也产生锅具需求（如鸡蛋汉堡 → 煎蛋 → 煎锅）。
  for (const inter of S.intermediatesCache) {
    const step = inter.cookingStep ?? "";
    if (!step || inter.group !== "levelset") continue;
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
