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
}

export interface RecipeLike {
  id?: string;
  type?: string;
  cookingStep?: string;
  ingredients?: string[];
  /** Direct composition ids for custom recipes (sub-recipe ids and/or ingredient ids). */
  compositionIds?: string[];
  intermediate?: boolean;
}

export interface IntermediateLike extends RecipeLike {}

/** Mirrors STEP_UTENSILS in build-catalog.mjs / LayoutEditorRecipeKnowledge.cs. */
const STEP_UTENSILS: Record<string, string[]> = {
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

const COOK_STEPS = new Set(Object.keys(STEP_UTENSILS));

/** True when the step id is a real cooking step (pot, pan, steamer, …). */
export function isCookStepLike(s: string | undefined): boolean {
  return !!s && COOK_STEPS.has(s);
}

/** Custom recipe "工序" grouping (mirrors the backend composition branch):
 *  the recipe has no overall cooking step (Composite/assembly), so its direct
 *  compositions are expanded — sub-recipes group under their own cooking step
 *  with their leaf ingredients, plain ingredients fall into the raw group.
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

  const raw: string[] = [];
  const steps: string[] = [];
  const stepIngs = new Map<string, string[]>();
  for (const compId of compIds) {
    const sub = byId.get(compId);
    if (sub && (sub.ingredients ?? []).length > 0) {
      const step = COOK_STEPS.has(sub.cookingStep ?? "") ? sub.cookingStep! : "";
      if (!step) {
        for (const ing of sub.ingredients!) raw.push(ing);
        continue;
      }
      const lst = stepIngs.get(step) ?? [];
      for (const ing of sub.ingredients!) lst.push(ing);
      stepIngs.set(step, lst);
      if (!steps.includes(step)) steps.push(step);
    } else {
      raw.push(compId);
    }
  }

  const groups: CookingGroup[] = [];
  if (raw.length > 0) groups.push({ step: "", utensils: [], ingredients: raw });
  for (const st of steps) {
    groups.push({ step: st, utensils: STEP_UTENSILS[st] ?? [], ingredients: stepIngs.get(st)! });
  }
  return groups;
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
  if (flourBranch && isCookStep(finalStep) && !groupMap.has(finalStep) && order.length > 0) {
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
 *  to them — apply the same tweaks here and merge groups that collapse to one step. */
export function normalizeCookingGroups(r: RecipeLike, groups: CookingGroup[]): CookingGroup[] {
  if (!groups || groups.length === 0) return groups ?? [];
  const stepMap = new Map<string, string>();
  if (r.type === "sushi") stepMap.set("Steamer", "Pot");
  const ingMap = new Map<string, string>();
  if (r.type === "pizza" && r.id !== "Pizza_Olives") ingMap.set("DLC05_Dough", "DoughSO");
  if (stepMap.size === 0 && ingMap.size === 0) return groups;

  const out: CookingGroup[] = [];
  for (const g of groups) {
    const step = stepMap.get(g.step) ?? g.step;
    const ings = g.ingredients.map((i) => ingMap.get(i) ?? i);
    const prev = out.find((x) => x.step === step);
    if (prev) prev.ingredients = prev.ingredients.concat(ings);
    else out.push({ step, utensils: g.utensils, ingredients: ings });
  }
  return out;
}
