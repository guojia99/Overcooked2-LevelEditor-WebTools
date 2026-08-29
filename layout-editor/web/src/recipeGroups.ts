/** Ingredient cooking groups for the recipe-list page.
 *  `cookingGroups` on recipe catalog entries is computed by the backend
 *  (LayoutEditorRecipeKnowledge.ComputeCookingGroups / build-catalog.mjs).
 *  This module mirrors that algorithm as a fallback for stale data, merges the
 *  final-step marker group into the previous group (so the final pot icon stays
 *  inside the ingredient box), and applies the same id normalizations as
 *  api.ts fetchRecipeCatalog. */

export interface CookingGroup {
  step: string;
  utensils: string[];
  ingredients: string[];
  /** Extra step icons appended to this group (final pot after mixing, merged from the marker group). */
  extraSteps?: { step: string; utensils: string[] }[];
  /** 食材级步骤角标：ingredientId → 步骤列表（如炒饭的米 → Pot），渲染在该食材图标右下角。 */
  ingredientSteps?: Record<string, string[]>;
}

export interface RecipeLike {
  id?: string;
  type?: string;
  cookingStep?: string;
  ingredients?: string[];
  /** Direct composition ids for custom recipes (sub-recipe ids and/or ingredient ids). */
  compositionIds?: string[];
  intermediate?: boolean;
  /** Mixed 类型自定义菜谱：先搅拌（MixingBowl）再烹饪。 */
  mixing?: boolean;
}

export interface IntermediateLike extends RecipeLike {}

/** Mirrors STEP_UTENSILS in build-catalog.mjs / LayoutEditorRecipeKnowledge.cs. */
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

/** node 型食材（匹配节点，无实体 prefab）→ 食材箱可生成的整食材；
 *  以及建箱等价替换（同 uID 节点的核心版/dlc04 版优先）。
 *  - 沙拉洋葱：原版 dlc11 关卡食材箱给整个沙拉洋葱（dlc11_onion_salad，prefab 型，
 *    切 8 刀变 ChoppedOnion_Salad 匹配 dlc11onion_salad 节点）；节点本体不能进食材箱
 *    （运行时 LoadAsset<GameObject> 为 null → PseudoPrefabDispenser.Setup NRE）。
 *  - DLC8 汉堡胚 → 核心汉堡面包（ChoppedBunSO）：二者携带的 IngredientOrderNode
 *    uID 同为 16088（bundle 实测），订单匹配完全等价；套餐统一用核心面包，
 *    不再依赖 bundle359 的 dlc08_choppedbun。
 *  - DLC10 火锅/拼盘食材 → DLC04 版（uID 718144~718150 一一相等，换皮复用）：
 *    dlc10_* 建箱统一映射到无前缀/dlc04 版（bundle226），少一个 bundle 依赖；
 *    混选两套换皮菜谱时也只建一套箱（uID 等价，一箱即匹配两套订单）。 */
export const NODE_INGREDIENT_SOURCES: Record<string, string> = {
  dlc11onion_salad: "dlc11_onion_salad",
  dlc08_bun: "ChoppedBunSO",
  dlc10_bokchoy: "bokchoy",
  dlc10_meat: "dlc04_meat",
  dlc10_orange: "dlc04_orange",
  dlc10_prawn: "dlc04_prawn",
  dlc10_grapes: "grapes",
  dlc10_noodles: "noodles",
  dlc10_peach: "peach",
};

const COOK_STEPS = new Set(Object.keys(STEP_UTENSILS));

/** True when the step id is a real cooking step (pot, pan, steamer, …). */
export function isCookStepLike(s: string | undefined): boolean {
  return !!s && COOK_STEPS.has(s);
}

/** Custom recipe "工序" grouping (mirrors the backend composition branch).
 *  Direct compositions are expanded — sub-recipes group under their own cooking
 *  step with their leaf ingredients, plain ingredients fall into the cooking box.
 *
 *  Cases (hasOwnStep = 自身有烹饪步骤):
 *  - 有自身步骤 + 已烹饪子菜谱（两阶段，如炒饭）：子菜谱步骤作该食材的角标
 *    （煮米 → 米右下角 Pot 角标），普通食材并入同一框，自身步骤作主图标（煎锅）。
 *  - 有自身步骤 + 仅搅拌子菜谱（面糊，如 CheesePrawn）：搅拌框 + 自身步骤标记（双图标）。
 *  - 有自身步骤 + 全生食材组成（如 Fried2_Shrimp）：当前烹饪步骤包裹全部食材（单框）。
 *  - 无自身烹饪步骤（Composite/组装型）：子菜谱独立成组在前，普通食材归生组。
 *  Composition order defines the group order. */
export function deriveCompositionGroups(
  r: RecipeLike,
  allRecipes: IntermediateLike[]
): CookingGroup[] {
  const compIds = r.compositionIds ?? [];
  if (compIds.length === 0) return [];

  const byId = new Map<string, RecipeLike>();
  for (const x of allRecipes) {
    if (x.id && !byId.has(x.id)) byId.set(x.id, x);
  }

  const finalStep = r.cookingStep ?? "";
  const hasOwnStep = COOK_STEPS.has(finalStep);
  // 普通食材（不是子菜谱 / 子菜谱无烹饪步骤的食材）
  const plain: string[] = [];
  // 每个子菜谱（按自身烹饪步骤）单独成组，互不合并：
  // 例如「炸薯条+炸鱼排+蘑菇汤」套餐中，炸薯条与炸鱼排即使同为 DeepFatFryer 也分开显示。
  // 子菜谱为 Mixed 类型（mixing）时即使无 cookingStep 也按 MixingBowl 成组（搅拌）。
  const groups: CookingGroup[] = [];
  const subGroups = new Map<string, CookingGroup>();
  for (const compId of compIds) {
    const sub = byId.get(compId);
    if (sub && (sub.ingredients ?? []).length > 0) {
      const step = COOK_STEPS.has(sub.cookingStep ?? "")
        ? sub.cookingStep!
        : sub.mixing
          ? "MixingBowl"
          : "";
      if (step) {
        // 同一子菜谱重复出现（数量叠加）时并入同一组；不同子菜谱即使步骤相同也分开。
        const key = `${step}::${compId}`;
        let g = subGroups.get(key);
        if (!g) {
          g = { step, utensils: STEP_UTENSILS[step] ?? [], ingredients: [] };
          subGroups.set(key, g);
          groups.push(g);
        }
        for (const ing of sub.ingredients!) g.ingredients.push(ing);
      } else {
        for (const ing of sub.ingredients!) plain.push(ing);
      }
    } else {
      plain.push(compId);
    }
  }

  const result: CookingGroup[] = [];
  if (hasOwnStep) {
    // 食材级步骤角标（如炒饭的米 → Pot 煮锅角标）
    const badgeSteps: Record<string, string[]> = {};
    // 主框食材：已烹饪子菜谱产物 + 普通食材
    const mainIngs: string[] = [];
    // 搅拌子菜谱保留为独立搅拌框（CheesePrawn）
    const keepBoxes: CookingGroup[] = [];
    for (const g of groups) {
      if (g.step === "MixingBowl") {
        keepBoxes.push(g);
        continue;
      }
      // 已烹饪子菜谱（如煮米）：其步骤作该食材角标，食材并入主框
      for (const ing of g.ingredients) {
        mainIngs.push(ing);
        (badgeSteps[ing] = badgeSteps[ing] ?? []).push(g.step);
      }
    }
    for (const ing of plain) mainIngs.push(ing);

    result.push(...keepBoxes);
    if (mainIngs.length > 0) {
      result.push({
        step: finalStep,
        utensils: STEP_UTENSILS[finalStep] ?? [],
        ingredients: mainIngs,
        ingredientSteps: Object.keys(badgeSteps).length ? badgeSteps : undefined,
      });
    } else if (keepBoxes.length > 0) {
      // 只有搅拌子菜谱（如 CheesePrawn）：自身烹饪步骤作为标记组（并入同格双图标）
      result.push({ step: finalStep, utensils: STEP_UTENSILS[finalStep] ?? [], ingredients: [] });
    } else {
      // 组成全是生食材：当前烹饪步骤包裹全部（如 Fried2_Shrimp 鱼虾同框）
      result.push({ step: finalStep, utensils: STEP_UTENSILS[finalStep] ?? [], ingredients: plain });
    }
  } else {
    if (plain.length > 0) result.push({ step: "", utensils: [], ingredients: plain });
    result.push(...groups);
  }
  return result;
}

/** Fallback derivation of ingredient cooking groups (mirrors the backend algorithm). */
export function deriveCookingGroups(r: RecipeLike, allRecipes: IntermediateLike[]): CookingGroup[] {
  const finalStep = r.cookingStep ?? "";
  const ingredients = r.ingredients ?? [];
  if (ingredients.length === 0) return [];

  const isCookStep = (s: string | undefined): boolean => !!s && COOK_STEPS.has(s);
  const has = (id: string): boolean => ingredients.includes(id);

  // 半成品：直接按自身烹饪步骤成组
  if (r.intermediate) {
    const step = isCookStep(finalStep) ? finalStep : "";
    return [{ step, utensils: STEP_UTENSILS[step] ?? [], ingredients }];
  }

  // Mixed 类型自定义菜谱：
  //  - 若自身烹饪步骤本身就是混合步骤（Blender / Mixer / MixingBowl），
  //    该步骤即搅拌本身 → 只显示该混合图标（单图标，如冰蓝莓沙 = 搅拌机）。
  //  - 否则先搅拌（MixingBowl）再烹饪（最终步骤标记，并入同格双图标），
  //    例：面团+肉搅拌 → MixingBowl 组 + 烹饪步骤标记组。
  if (r.mixing) {
    if (finalStep === "Blender" || finalStep === "Mixer" || finalStep === "MixingBowl") {
      return [{ step: finalStep, utensils: STEP_UTENSILS[finalStep] ?? [], ingredients: [...ingredients] }];
    }
    const groups: CookingGroup[] = [
      { step: "MixingBowl", utensils: STEP_UTENSILS["MixingBowl"] ?? [], ingredients: [...ingredients] },
    ];
    if (isCookStep(finalStep)) {
      groups.push({ step: finalStep, utensils: STEP_UTENSILS[finalStep] ?? [], ingredients: [] });
    }
    return groups;
  }

  const prep = new Map<string, string>(); // ingredient -> step ("" = raw)

  // 煎锅中间产物覆盖（汉堡肉排；面包胚/生菜保持生）
  if (finalStep === "FryingPan") {
    const candidates = allRecipes
      .filter(
        (x) =>
          x.intermediate &&
          isCookStep(x.cookingStep) &&
          (x.cookingStep === "FryingPan" || x.cookingStep === "MixingBowl") &&
          (x.ingredients ?? []).length > 0 &&
          (x.ingredients ?? []).every((ing) => ingredients.includes(ing))
      )
      .sort(
        (a, b) =>
          (b.ingredients?.length ?? 0) - (a.ingredients?.length ?? 0) ||
          (a.id ?? "").localeCompare(b.id ?? "")
      );
    for (const cand of candidates) {
      for (const ing of cand.ingredients ?? []) {
        if (!prep.has(ing)) prep.set(ing, cand.cookingStep!);
      }
    }
  }

  // 食材本身是半成品（如月饼引用搅拌面糊 dlc13_mixedflouregg*）：
  // 按半成品自身的烹饪步骤成组；最终烹饪步骤（烤箱）作为标记组追加。
  let interBranch = false;
  {
    const byId = new Map<string, RecipeLike>();
    for (const x of allRecipes) {
      if (x.id && !byId.has(x.id)) byId.set(x.id, x);
    }
    for (const ing of ingredients) {
      const sub = byId.get(ing);
      if (sub && sub.intermediate && isCookStep(sub.cookingStep)) {
        if (!prep.has(ing)) prep.set(ing, sub.cookingStep!);
        interBranch = true;
      }
    }
  }

  let flourBranch = false;
  if (r.type === "sushi") {
    for (const ing of ingredients) prep.set(ing, ing === "SushiRiceSO" || ing === "RiceSO" ? "Steamer" : "");
  } else if (r.type === "burrito") {
    for (const ing of ingredients) {
      prep.set(ing, ing === "TortillaSO" ? "" : ing === "SushiRiceSO" || ing === "RiceSO" ? "Pot" : "FryingPan");
    }
  } else if (r.type === "smores") {
    for (const ing of ingredients) prep.set(ing, ing === "DLC05_Marshmallow" ? "ToastingFork" : "");
  } else if (finalStep === "Pot" && has("PastaSO")) {
    for (const ing of ingredients) prep.set(ing, ing === "PastaSO" ? "Pot" : "FryingPan");
  } else if (r.type === "hotdog") {
    // 只有热狗肠需要煮；洋葱单独煎；面包/芥末/番茄酱无需烹饪
    for (const ing of ingredients) {
      prep.set(ing, ing === "frankfurter" || ing === "dlc11_frankfurter" ? "Pot" : ing === "dlc08_onion" || ing === "dlc11_onion" ? "FryingPan" : "");
    }
  } else if (r.type === "hotchocolate") {
    // 全程只有牛奶+巧克力需要煮；奶油/棉花糖是单独的（无需烹饪）
    for (const ing of ingredients) {
      prep.set(ing, ing === "milk" || ing === "dlc09_milk" || ing === "dlc03_chocolate" || ing === "dlc09_chocolate" ? "Pot" : "");
    }
  } else if (r.type === "float") {
    // 冰淇淋汽水：汽水单独（无需搅拌）；牛奶/口味/冰块进搅拌机（Blender）
    for (const ing of ingredients) {
      prep.set(ing, ing === "orangesoda" || ing === "rootbeer" ? "" : "Blender");
    }
  } else if (finalStep === "DeepFatFryer" && r.type !== "donut" || (r.type === "fry" && !isCookStep(finalStep))) {
    // 名称前缀推断的 fry（如 FriedEgg）不得覆盖显式烹饪步骤（FryingPan 等）；
    // 甜甜圈走搅拌+炸篮分支（flourBranch）
    for (const ing of ingredients) prep.set(ing, "DeepFatFryer");
  } else if (
    r.type === "cake" ||
    (has("FlourSO") && finalStep !== "Mixer" && finalStep !== "MixingBowl")
  ) {
    // 蛋糕：搅拌 + 烤箱；面糊/面团（松饼/饺子）：搅拌 + 最终锅具
    flourBranch = r.type !== "cake";
    const cookStep = r.type === "cake" ? "OvenTray" : isCookStep(finalStep) ? finalStep : "";
    for (const ing of ingredients) {
      if (!prep.has(ing)) {
        prep.set(ing, ing === "FlourSO" || ing === "EggSO" ? "MixingBowl" : cookStep);
      }
    }
  }

  // 未分配的食材回退
  const anyAssigned = [...prep.values()].some((s) => s !== "");
  const fallbackStep = anyAssigned ? "" : isCookStep(finalStep) ? finalStep : "";
  for (const ing of ingredients) {
    if (!prep.has(ing)) prep.set(ing, fallbackStep);
  }

  const groupMap = new Map<string, string[]>();
  const order: string[] = [];
  for (const ing of ingredients) {
    const st = prep.get(ing)!;
    const lst = groupMap.get(st);
    if (lst) lst.push(ing);
    else {
      groupMap.set(st, [ing]);
      order.push(st);
    }
  }

  const ordered = order.filter((s) => s !== "");
  if ((flourBranch || interBranch) && isCookStep(finalStep) && !groupMap.has(finalStep) && order.length > 0) {
    ordered.push(finalStep);
  }

  const splitPerIngredient =
    (finalStep === "DeepFatFryer" && r.type !== "donut") || (r.type === "fry" && !isCookStep(finalStep)) || (finalStep === "Pot" && has("PastaSO"));

  const result: CookingGroup[] = [];
  const raw = groupMap.get("");
  if (raw && raw.length > 0) result.push({ step: "", utensils: [], ingredients: raw });
  for (const st of ordered) {
    const ings = groupMap.get(st) ?? [];
    if (splitPerIngredient && st !== "" && ings.length > 1) {
      for (const ing of ings) {
        result.push({ step: st, utensils: STEP_UTENSILS[st] ?? [], ingredients: [ing] });
      }
    } else {
      result.push({ step: st, utensils: STEP_UTENSILS[st] ?? [], ingredients: ings });
    }
  }
  return result;
}

/** Merge the final-step marker group (empty ingredients, e.g. 松饼的煎锅) into the
 *  previous group, and merge a mixing group (面粉搅拌) with the following cooking-step
 *  group (饺子：面粉+肉）into a single box — ingredients side by side, step icons
 *  (搅拌碗+蒸笼/煎锅) side by side. */
export function mergeFinalMarkers(groups: CookingGroup[]): CookingGroup[] {
  const out: CookingGroup[] = [];
  for (let i = 0; i < groups.length; i++) {
    const g = groups[i];

    // 搅拌组 + 后续步骤组 → 合并为一格
    if (g.step === "MixingBowl" && i + 1 < groups.length) {
      const next = groups[i + 1];
      if (next.step && next.step !== "MixingBowl") {
        out.push({
          step: g.step,
          utensils: g.utensils,
          ingredients: [...g.ingredients, ...next.ingredients],
          extraSteps: [
            ...(g.extraSteps ?? []),
            { step: next.step, utensils: next.utensils },
            ...(next.extraSteps ?? []),
          ],
        });
        i++;
        continue;
      }
    }

    // 空标记组 → 并入前一组（松饼的煎锅标记）
    if (g.ingredients.length === 0 && g.step) {
      const prev = out[out.length - 1];
      if (prev) {
        prev.extraSteps = prev.extraSteps ?? [];
        prev.extraSteps.push({ step: g.step, utensils: g.utensils });
      }
      continue;
    }

    out.push({ ...g, extraSteps: g.extraSteps ? [...g.extraSteps] : undefined });
  }
  return out;
}

/** Backend-computed cookingGroups ride along fetchRecipeCatalog, but the entry-level id
 *  normalizations of api.ts (sushi Steamer→Pot, pizza DLC05_Dough→DoughSO) are not applied
 *  to them — apply the same tweaks here and merge groups that collapse to one step.
 *  后端 ingredientSteps 是 pair 数组（JsonUtility 不支持 Dictionary）→ 归一化为 Record。 */
export function normalizeCookingGroups(r: RecipeLike, groups: CookingGroup[]): CookingGroup[] {
  if (!groups || groups.length === 0) return groups ?? [];
  const stepMap = new Map<string, string>();
  if (r.type === "sushi") stepMap.set("Steamer", "Pot");
  const ingMap = new Map<string, string>();
  if (r.type === "pizza" && r.id !== "Pizza_Olives") ingMap.set("DLC05_Dough", "DoughSO");
  const mapIng = (i: string): string => ingMap.get(i) ?? i;
  const mapSteps = (raw: unknown): Record<string, string[]> | undefined => {
    if (raw == null) return undefined;
    const out: Record<string, string[]> = {};
    if (Array.isArray(raw)) {
      // pair 数组：[{ ingredient, steps: [] }]
      for (const pair of raw as { ingredient?: string; steps?: string[] }[]) {
        if (!pair || pair.ingredient == null) continue;
        out[mapIng(pair.ingredient)] = (pair.steps ?? []).map((s) => stepMap.get(s) ?? s);
      }
    } else {
      // Record<ingredient, step[]>
      for (const [k, v] of Object.entries(raw as Record<string, string[]>)) {
        out[mapIng(k)] = (v ?? []).map((s) => stepMap.get(s) ?? s);
      }
    }
    return Object.keys(out).length ? out : undefined;
  };
  if (stepMap.size === 0 && ingMap.size === 0) {
    // 无 id 归一化时也需把后端 pair 数组 ingredientSteps 转成 Record
    return groups.map((g) => (g.ingredientSteps ? { ...g, ingredientSteps: mapSteps(g.ingredientSteps) } : g));
  }

  const out: CookingGroup[] = [];
  for (const g of groups) {
    const step = stepMap.get(g.step) ?? g.step;
    const ings = g.ingredients.map(mapIng);
    const prev = out.find((x) => x.step === step);
    if (prev) prev.ingredients = prev.ingredients.concat(ings);
    else out.push({ step, utensils: g.utensils, ingredients: ings, ingredientSteps: mapSteps(g.ingredientSteps) });
  }
  return out;
}
