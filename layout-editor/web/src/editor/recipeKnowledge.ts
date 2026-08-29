import { S } from "./state";
import { VARIANT_TO_BASE } from "./itemVariants";
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
  OvenCakeTin: ["Oven", "utensil_cake_tin_01"],
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
  { dirty: "dirtyplatestack", sink: "Sink", ret: "PlateReturn" },
];
export const GLASS_CLEANUP: { dirty: string; sink: string; ret: string }[] = [
  { dirty: "dlc11_dirtyglassstack", sink: "SinkGlass", ret: "GlassReturn" },
];
export const MUG_CLEANUP: { dirty: string; sink: string; ret: string }[] = [
  // 马克杯套装（写回时后端自动补挂 stub，dlc09 皮肤可用）
  { dirty: "dirtymugstack", sink: "workstation_sink_mug_01_wood", ret: "workstation_mug_return" },
];
export const TRAY_CLEANUP: { dirty: string; sink: string; ret: string }[] = [
  // 餐盘套装：普通水槽洗不了餐盘，必须洗餐盘水槽（dlc08 马戏团）+ 餐盘回收台
  { dirty: "dlc08_dirtytraystack", sink: "dlc08_workstation_01_tray_sink_circus", ret: "dlc08_workstation_tray_return" },
];

/** 奶油喷罐道具 id（仅识别用：场景里任一款在场都算已有喷罐）。 */
export const CREAM_SPRAY_IDS = ["utensil_ingredient_spray_01", "dlc09_utensil_ingredient_spray"];
/** 自动填充默认放置的奶油喷罐：DLC3 版（dlc09 版不自动填充，且其 m_OrderPrefab
 *  原版数据即损坏——由 LayoutEditorIngredientSprayPatch 在 Play 期补齐的是 dlc03 版路径）。 */
export const CREAM_SPRAY_DEFAULT_ID = "utensil_ingredient_spray_01";
/** 奶油喷罐可喷出的发泡奶油食材 id。 */
export const CREAM_INGREDIENT_IDS = ["whippedcream", "dlc09_whippedcream"];

/** 识别需要奶油喷罐的菜谱：仅按「食材含发泡奶油」判定。
 *  冰淇淋汽水/冰淇淋的奶+冰+口味走搅拌机（Blender），不需要喷罐。 */
export function recipeNeedsCreamSpray(r: RecipeEntry): boolean {
  const ings = r.ingredients ?? [];
  return ings.some((i) => CREAM_INGREDIENT_IDS.includes(i));
}

/** 汽水机（DLC11 饮料机）道具 id：冰淇淋汽水的汽水从这里取（放置形态 + 开关联动循环切换）。 */
export const SODA_MACHINE_IDS = ["dlc11_drink_dispenser"];
/** 汽水机可输出的汽水食材 id（node 型，无实体 prefab，不能进食材箱）。 */
export const SODA_MACHINE_INGREDIENT_IDS = ["orangesoda", "rootbeer"];

/** 饮料机（DLC8）道具 id：套餐的饮料从这里取（放置形态 + 开关联动循环切换）。 */
export const DRINK_MACHINE_IDS = ["dlc08_drink_machine"];
/** 饮料机可输出的饮料食材 id（drink01/02/03）。 */
export const DRINK_MACHINE_INGREDIENT_IDS = ["drink01", "drink02", "drink03"];

/** 识别需要汽水机的菜谱：食材含汽水（橙味汽水/沙士汽水）。
 *  汽水是 node 型食材，运行时只能由汽水机产出 —— 不建食材箱。 */
export function recipeNeedsSodaMachine(r: RecipeEntry): boolean {
  return (r.ingredients ?? []).some((i) => SODA_MACHINE_INGREDIENT_IDS.includes(i));
}

/** 识别需要饮料机（DLC8）的菜谱：食材含饮料（套餐）。 */
export function recipeNeedsDrinkMachine(r: RecipeEntry): boolean {
  return (r.ingredients ?? []).some((i) => DRINK_MACHINE_INGREDIENT_IDS.includes(i));
}

export function recipeNeedsMug(r: RecipeEntry): boolean {
  return (r.type ?? "") === "hotchocolate";
}

/** 拼盘/套餐类（餐盘组装装盘）：水果拼盘 + 马戏团套餐。成品放餐盘（tray）
 *  而非普通盘子——普通水槽洗不了餐盘，需要洗餐盘水槽 + 餐盘回收台。 */
export function recipeNeedsTray(r: RecipeEntry): boolean {
  const t = r.type ?? "";
  return t === "fruitplatter" || t === "mealdeal";
}

export function recipeNeedsGlass(r: RecipeEntry): boolean {
  const t = r.type ?? "";
  return t === "float" || t === "icecream" || t === "smoothie";
}

/** 每族「重复 DLC 换皮」默认屏蔽的来源组：只保留族内首选 DLC 一版，避免菜单里出现
 *  同一道菜的多个 DLC 皮肤（换皮菜品规格一致，重复上架无意义）。火锅的厨房道具
 *  （地炉/大锅）默认是 DLC10 版，因此保留 dlc10 菜谱、屏蔽 dlc04。 */
export const DUPLICATE_DLC_BLOCK: Record<string, string[]> = {
  hotdog: ["dlc11"],
  hotchocolate: ["dlc09"],
  hotpot: ["dlc04"],
  roast: ["dlc09"],
  pudding: ["dlc09"],
  fruitplatter: ["dlc13"],
};

/** 该菜谱是否属于「默认屏蔽的重复 DLC」来源组。 */
export function isRecipeDlcBlocked(r: RecipeEntry): boolean {
  const blocked = DUPLICATE_DLC_BLOCK[r.type ?? ""];
  if (!blocked) return false;
  return blocked.includes(r.group ?? "");
}

/** DLC 换皮/变体 → 功能等价的基础道具 id：场景里放了变体即视为具备基础功能
 *  （如放了 dlc09_oven 就算有了 Oven），供自动填充/缺失分析使用。
 *  唯一数据源见 itemVariants.ts（调色板合并与右键换肤同源）。 */
export const FUNCTIONAL_BASE: Record<string, string> = VARIANT_TO_BASE;

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
  // 月饼：搅拌面糊（半成品）→ 搅拌碗（烤箱为工作站，不分配食材）
  moonpie: {
    "MixerBowl": [
      "dlc13_mixedfloureggchocolate",
      "dlc13_mixedfloureggchocolatestrawberry",
      "dlc13_mixedfloureggstrawberries",
      "dlc13_mixedfloureggwatermelon",
    ],
  },
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
  // 月饼：食材即搅拌面糊半成品 → 搅拌碗
  if (type === "moonpie" || id.includes("moonpie")) keys.add("moonpie");
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
  Blender: "BlenderCup",
  // 火锅：食材装进大锅（叠放在地面灶台上）
  HotPot: "utensil_large_pot_01",
  // 烤菜：食材装进烤盘（叠放在烤箱/工作台上）
  RoastingTray: "utensil_roasting_tray",
  // 蛋糕：食材装进蛋糕模具（叠放在烤箱内）
  OvenCakeTin: "utensil_cake_tin_01",
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

/** 搅拌型中间产物：官方面糊用 cookingStep=Mixer/MixingBowl；自定义 Mixed 类型用 mixing 标记。 */
export function isMixIntermediate(inter: RecipeEntry): boolean {
  const step = inter.cookingStep ?? "";
  return step === "Mixer" || step === "MixingBowl" || !!inter.mixing;
}

export function findBatterIntermediateForRecipe(r: RecipeEntry): RecipeEntry | undefined {
  const interById = new Map(S.intermediatesCache.map((x) => [x.id, x]));
  // 组成直接引用搅拌中间产物（如 mooncake_meat → Mixed_FlourEggMeat）
  for (const cid of r.compositionIds ?? []) {
    const sub = interById.get(cid);
    if (sub && isMixIntermediate(sub)) return sub;
  }
  const leafs = new Set(r.ingredients ?? []);
  let best: RecipeEntry | undefined;
  for (const inter of S.intermediatesCache) {
    if (!isMixIntermediate(inter)) continue;
    const ings = inter.ingredients ?? [];
    if (ings.length === 0 || !ings.every((i) => leafs.has(i))) continue;
    if (!best || ings.length > (best.ingredients ?? []).length) best = inter;
  }
  return best;
}

export function recipeLacksIntermediate(r: RecipeEntry): boolean {
  // Mixed 类型自定义菜谱：叶食材直接进搅拌碗，无需单独面糊中间产物
  if (r.mixing) return false;
  if (!isFlourBranchRecipe(r)) return false;
  const interById = new Map(S.intermediatesCache.map((x) => [x.id, x]));
  for (const i of r.ingredients ?? []) {
    const sub = interById.get(i);
    if (sub && isMixIntermediate(sub)) return false;
  }
  return !findBatterIntermediateForRecipe(r);
}

export function missingIntermediateRecipes(recipes: RecipeEntry[]): RecipeEntry[] {
  return recipes.filter(recipeLacksIntermediate);
}

export function leafIngredientIds(id: string): string[] {
  const inter = S.intermediatesCache.find((x) => x.id === id);
  const ings = inter?.ingredients;
  return ings && ings.length > 0 ? ings : [id];
}

export function computeUtensilIngredientFill(
  recipes: RecipeEntry[]
): Map<string, { ings: string[]; intermediates: string[] }> {
  // 数据驱动（与游戏 OrderDefinitionNode / FryableObjectsLookup 一致，bundle 实测）：
  //  - 汤类：全部叶食材直接进汤锅（OnionCarrotPotatoSoup comp=3 食材节点, step=Pot）；
  //  - 热狗：香肠进汤锅、洋葱进煎锅（面包/酱料不加热，按 cookingGroups 分组）；
  //  - 搅拌类：叶食材进搅拌杯（BlenderCup，MixableContainer.approved）；
  //  - 面粉系（派/布丁/甜甜圈/松饼）：搅拌碗 ← 面糊叶食材；最终热锅（炸篮/煎锅）
  //    ← 面糊中间产物节点本身（Donut comp=1 面糊节点；原版 FryableObjectsLookup 含
  //    MixedFlourEgg 系节点而非生面粉）；烤箱类工作站无 stub，跳过。
  //  - 食材直接引用混合中间产物的菜谱同面糊规则。
  const result = new Map<string, { ings: string[]; intermediates: string[] }>();
  const addIng = (ut: string, iid: string) => {
    if (!result.has(ut)) result.set(ut, { ings: [], intermediates: [] });
    const e = result.get(ut)!;
    if (!e.ings.includes(iid)) e.ings.push(iid);
  };
  const addInter = (ut: string, iid: string) => {
    if (!result.has(ut)) result.set(ut, { ings: [], intermediates: [] });
    const e = result.get(ut)!;
    if (!e.intermediates.includes(iid)) e.intermediates.push(iid);
  };

  const interById = new Map(S.intermediatesCache.map((x) => [x.id, x]));
  const isMixStep = (s?: string) => s === "Mixer" || s === "MixingBowl";
  const isMixSub = (x: RecipeEntry) => isMixStep(x.cookingStep) || !!x.mixing;
  // 菜谱最终热锅容器（工作站无 stub，返回空）
  const finalVesselOf = (r: RecipeEntry): string => {
    const uts = STEP_UTENSILS[r.cookingStep ?? ""] ?? [];
    const vessel = uts[uts.length - 1];
    return vessel && !WORKSTATION_UTENSILS.has(vessel) ? vessel : "";
  };

  for (const r of recipes) {
    if (!r || r.intermediate) continue;
    // 食材直接引用混合中间产物（月饼型：食材即面糊半成品）
    const refMix = (r.ingredients ?? [])
      .map((i) => interById.get(i))
      .filter((x): x is RecipeEntry => !!x && isMixSub(x));
    if (refMix.length > 0) {
      for (const b of refMix) {
        for (const ing of b.ingredients ?? []) addIng("MixerBowl", ing);
        const vessel = finalVesselOf(r);
        if (vessel) addInter(vessel, b.id);
      }
      continue;
    }
    // 组成引用搅拌中间产物（如 mooncake_meat → Mixed_FlourEggMeat）
    const mixComps = (r.compositionIds ?? [])
      .map((cid) => interById.get(cid))
      .filter((x): x is RecipeEntry => !!x && isMixSub(x));
    if (mixComps.length > 0) {
      for (const b of mixComps) {
        for (const ing of b.ingredients ?? []) addIng("MixerBowl", ing);
        const vessel = finalVesselOf(r);
        if (vessel) addInter(vessel, b.id);
      }
      continue;
    }
    // Mixed 类型：叶食材直接进搅拌碗（如 mooncake_Orange 四料直接搅拌）
    if (r.mixing && isFlourBranchRecipe(r)) {
      for (const ing of r.ingredients ?? []) addIng("MixerBowl", ing);
      const vessel = finalVesselOf(r);
      if (vessel) addInter(vessel, r.id);
      continue;
    }
    // 面粉系：面糊中间产物为准（其食材表=真实下搅拌碗的内容）
    if (isFlourBranchRecipe(r)) {
      const batter = findBatterIntermediateForRecipe(r);
      if (batter) {
        for (const ing of batter.ingredients ?? []) addIng("MixerBowl", ing);
        const vessel = finalVesselOf(r);
        if (vessel) addInter(vessel, batter.id);
        continue;
      }
      // 无面糊中间产物：按展示分组兜底（面粉/鸡蛋→搅拌碗）
    }
    // 直接加热类：按 cookingGroups 的步骤分组把叶食材放进对应容器；
    // 组成员是混合中间产物时——混合容器（搅拌碗）加其叶食材、加热容器加中间产物节点。
    for (const g of r.cookingGroups ?? []) {
      const container = STEP_CONTAINER[g.step];
      if (!container) continue;
      for (const ing of g.ingredients ?? []) {
        const sub = interById.get(ing);
        if (sub && isMixSub(sub)) {
          if (container === "MixerBowl") {
            for (const leaf of sub.ingredients ?? []) addIng(container, leaf);
          } else {
            addInter(container, sub.id);
          }
        } else {
          addIng(container, ing);
        }
      }
    }
  }
  return result;
}

export function computeIntermediatesForUtensils(recipes: RecipeEntry[]): Map<string, string[]> {
  const result = new Map<string, string[]>();
  const add = (ut: string, iid: string) => {
    if (!result.has(ut)) result.set(ut, []);
    if (!result.get(ut)!.includes(iid)) result.get(ut)!.push(iid);
  };
  const interById = new Map(S.intermediatesCache.map((x) => [x.id, x]));
  const isMixStep = (s?: string) => s === "Mixer" || s === "MixingBowl";
  for (const r of recipes) {
    // 家族过滤：INTERMEDIATE_ASSIGN 的预设中间产物（如核心系面糊）只有其叶食材
    // ⊆ 本菜谱叶食材（经一层中间产物展开）时才适用——避免 dlc09/dlc13 变体松饼
    // 混入核心系的草莓/蓝莓面糊。
    const expandedLeafs = new Set((r.ingredients ?? []).flatMap((i) => leafIngredientIds(i)));
    for (const key of intermediateKeysForRecipe(r)) {
      const assign = INTERMEDIATE_ASSIGN[key];
      if (!assign) continue;
      for (const [ut, iids] of Object.entries(assign)) {
        for (const iid of iids) {
          const iidLeafs = leafIngredientIds(iid);
          if (!iidLeafs.every((l) => expandedLeafs.has(l))) continue;
          // 混合型中间产物（面糊）进加热锅具时注册节点本身（原版 FryableObjectsLookup
          // 含 MixedFlourEgg 系节点而非生面粉）；搅拌碗仍展开叶食材。
          const sub = interById.get(iid);
          if (sub && isMixIntermediate(sub) && ut !== "MixerBowl") {
            add(ut, sub.id);
            continue;
          }
          for (const ing of iidLeafs) add(ut, ing);
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
      add(vessel, inter.id);
    }
  }
  // 泛化兜底：用户自建的中间产物/自定义菜谱（如煎蛋）按「叶食材 ⊂ 菜谱叶食材 + 步骤匹配」
  // 自动分配——同样只分配叶食材（鸡蛋），而非菜谱本身。
  for (const r of recipes) {
    const leafs = new Set(r.ingredients ?? []);
    for (const inter of S.intermediatesCache) {
      if (inter.group !== "levelset") continue;
      const container = inter.mixing ? "MixerBowl" : STEP_CONTAINER[inter.cookingStep ?? ""];
      if (!container) continue;
      const ings = inter.ingredients ?? [];
      if (ings.length === 0 || !ings.every((i) => leafs.has(i))) continue;
      for (const ing of ings) add(container, ing);
    }
  }
  // Mixed 类型自定义菜谱：叶食材直接进搅拌碗
  for (const r of recipes) {
    if (!r.mixing || !isFlourBranchRecipe(r)) continue;
    for (const ing of r.ingredients ?? []) add("MixerBowl", ing);
  }
  return result;
}

export function computeRequiredUtensils(ingredientIds: Set<string>, steps: Set<string>, platingSteps?: Iterable<string>, recipes?: RecipeEntry[]): string[] {
  const set = new Set<string>(["ServingStation", "Bin"]);
  const recs = recipes ?? [];
  const hasGlassPlating = platingSteps ? [...platingSteps].includes("Glass") : false;
  const needMug = recs.some(recipeNeedsMug);
  const needGlass = recs.some(recipeNeedsGlass) || hasGlassPlating;
  const needTray = recs.some(recipeNeedsTray);
  const needPlate =
    recs.length === 0 || recs.some((r) => !recipeNeedsMug(r) && !recipeNeedsGlass(r) && !recipeNeedsTray(r));
  if (needPlate) {
    set.add("CleanPlateStack");
    for (const c of PLATE_CLEANUP) {
      // 脏容器堆（c.dirty）由回收台在游戏内自动生成，不纳入自动填充
      set.add(c.sink);
      set.add(c.ret);
    }
  }
  if (needTray) {
    // 拼盘/套餐：干净餐盘堆 + 洗餐盘水槽 + 餐盘回收台（普通盘子/水槽洗不了餐盘）
    set.add("dlc08_cleantraystack");
    for (const c of TRAY_CLEANUP) {
      set.add(c.sink);
      set.add(c.ret);
    }
  }
  if (needGlass) {
    set.add("CleanGlassStack");
    for (const c of GLASS_CLEANUP) {
      set.add(c.sink);
      set.add(c.ret);
    }
  }
  if (needMug) {
    set.add("cleanmugstack");
    for (const c of MUG_CLEANUP) {
      set.add(c.sink);
      set.add(c.ret);
    }
  }
  if (platingSteps && needPlate) {
    // 纯拼盘/套餐选择（needPlate=false）不补普通盘子堆；Glass 装盘由 needGlass 分支覆盖
    for (const s of requiredPlateStacks(platingSteps)) set.add(s);
  }
  if (recipes) {
    for (const r of recipes) {
      if (recipeNeedsCreamSpray(r)) {
        // 只要求默认 DLC3 喷罐（不自动填充 dlc09 版；已有任一款喷罐由
        // recipesDialogs 按 CREAM_SPRAY_IDS 识别为满足）
        set.add(CREAM_SPRAY_DEFAULT_ID);
      }
      // 冰淇淋汽水：汽水从汽水机取（放置形态机器 + 开关联动循环切换）
      if (recipeNeedsSodaMachine(r)) {
        for (const id of SODA_MACHINE_IDS) set.add(id);
        set.add("Switch");
      }
      // 套餐：饮料从饮料机取（放置形态机器 + 开关联动循环切换）
      if (recipeNeedsDrinkMachine(r)) {
        for (const id of DRINK_MACHINE_IDS) set.add(id);
        set.add("Switch");
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
  // 输出统一为家族基准 id（变体→基础）：与 existingPrefabIds 的 functionalBaseId
  // 归一化对齐——场景里放的是哪个皮肤都算「已有」，自动放置/提示用默认皮肤。
  return [...set].map(functionalBaseId);
}
