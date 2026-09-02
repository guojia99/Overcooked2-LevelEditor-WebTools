/**
 * Resolve finished-dish recipe ids → visual GameObject prefab (manifest container)
 * or existing PseudoPrefabSO guid (model_so / CompositeBurgerSO etc.).
 */
import fs from "fs";
import path from "path";

const GUID_PseudoPrefabSO = "0cff7c13895ab9e47a5e02d4619cc3b9";
const GUID_PseudoPrefabSORecipe = "753d9e70603f6a140b05f30f176ec2dd";

/** recipe id → prefab basename (without .prefab) */
const PREFAB_OVERRIDES = {
  Burrito_Chicken_SO: "plated_burrito_chicken_01",
  Burrito_Meat_SO: "plated_burrito_meat_01",
  Burrito_Mushroom_SO: "plated_burrito_mushroom_01",
  Salad_Plain_SO: "recipe_lettuce_tomato_01",
  Salad_Tomato_SO: "recipe_lettuce_tomato_01",
  Salad_Cucumber_SO: "recipe_lettuce_tomato_cucumber_01",
  Sushi_Fish_SO: "m_recipe_sushi_01",
  Sushi_Cucumber_SO: "m_recipe_sushi_02",
  Sushi_PlainFish_SO: "plated_fish_sliced_02",
  Sushi_PlainPrawn_SO: "plated_prawn_01",
  Sushi_All_SO: "m_recipe_sushi_03",
  Steamed_Carrot_SO: "recipe_dimsum_01",
  Steamed_Meat_SO: "recipe_dimsum_02",
  Steamed_Prawns_SO: "recipe_dimsum_03",
  Steamed_Fish_SO: "plated_steamedfish",
  Pasta_MeatOnly_SO: "recipe_meat_pasta_01",
  Pasta_MushroomOnly_SO: "recipe_mushroom_pasta_01",
  Pasta_TomatoOnly_SO: "recipe_tomato_pasta_01",
  Pasta_Marinara_SO: "recipe_tomato_pasta_01",
  Fry_Chicken_SO: "recipe_chicken_nuggets_01",
  Fry_Chips_SO: "plated_chicken_chips",
  Fry_All_SO: "compositechickennuggetsandchips",
  Fry_Fish_And_Chips_SO: "compositefishandchips",
  Cake_Plain_SO: "recipe_pancake_01",
  Cake_Chocolate_SO: "recipe_pancake_02",
  Cake_Honey_SO: "recipe_cake_01",
  Cake_HoneyCarrot_SO: "recipe_cake_02",
  Cake_HoneyChocolate_SO: "recipe_cake_03",
  Kebob_ChickenTomato: "chickentomatokebab",
  Kebob_ChickenMeatTomato: "chickenmeattomatokebab",
  Kebob_MushroomPineappleTomato: "mushroompineappletomatokebab",
  Kebob_MeatMushroomPineapple: "meatpineapplemushroomkebab",
  OnionCarrotPotatoSoup_SO: "compositesoup",
  Soup_Mushroom_SO: "compositesoup",
  Soup_Onion_SO: "compositesoup",
  Soup_Tomato_SO: "compositesoup",
  Smoothie_Strawberry: "recipe_strawberrysmoothie_01",
  Smoothie_Banana: "recipe_bananasmoothie_01",
  Smoothie_Melon: "recipe_melonsmoothie_01",
  Smoothie_BananaPineapple: "recipe_pineapplesmoothie_01",
  Smoothie_Mega: "recipe_megasmoothie_01",
  Breakfast_Bacon_Egg: "dlc5_recipe_bacon_egg_01",
  Breakfast_Bacon_Egg_Sausage: "dlc5_recipe_bacon_egg_sausage_01",
  Breakfast_Sausage_Beans: "dlc5_recipe_sausage_beans_01",
  Breakfast_Sausage_Beans_Egg: "dlc5_recipe_sausage_egg_beans_01",
  Breakfast_Sausage_Beans_Egg_Bacon: "dlc5_recipe_bacon_egg_sausage_beans_01",
  Smores_Plain: "dlc5_recipe_smores_marshmallow_01",
  Smores_Chocolate: "dlc5_recipe_smores_chocolate_01",
  Smores_Strawberry: "dlc5_recipe_smores_strawberry_01",
  Smores_Banana: "dlc5_recipe_smores_banana_01",
  Smores_Strawberry_Banana: "dlc5_recipe_smores_banana_01",
  Cake_StrawberryPancake: "strawberrypancake",
  Cake_BlueberryPancake: "dlc5_recipe_blueberry_pancake_01",
  MixedFlourEggStrawberry: "strawberrypancake",
  MixedFlourEggBlueberry: "dlc5_recipe_blueberry_pancake_01",
  beefpotatocarrotroast: "p_dlc7_recipe_beef_potato_carrot_01",
  beefpotatocarrotbroccoliroast: "p_dlc7_recipe_beef_potato_carrot_broccoli_01",
  chickenpotatocarrotroast: "p_dlc7_recipe_chicken_potato_carrot_01",
  chickenpotatocarrotbroccoliroast: "p_dlc7_recipe_chicken_potato_carrot_broccoli_01",
  christmaspuddingwithorange: "p_dlc9_recipe_christmaspudding",
  hotchocolatecream: "p_dlc9_recipe_hotchoc_cream",
  hotchocolatemallow: "p_dlc9_recipe_hotchoc_mallows",
  hotchocolatemallowcream: "p_dlc9_recipe_hotchoc_cream_mallows",
  optionalhotchocolate: "p_dlc9_recipe_hotchoc",
  fruitplatter_grapespeach: "dlc10_recipe_fruitsalad_02",
  fruitplatter_orangegrapes: "dlc10_recipe_fruitsalad_03",
  fruitplatter_orangepeach: "dlc10_recipe_fruitsalad_01",
  fruitplatter_orangepeachgrapes: "dlc10_recipe_fruitsalad_04",
  noodlesoup_bnm: "noodlesoup_bnm",
  noodlesoup_nbmm: "noodlesoup_nbmm",
  noodlesoup_nbmp: "noodlesoup_nbmp",
  noodlesoup_nbp: "noodlesoup_nbp",
  noodlesoup_nbpp: "noodlesoup_nbpp",
};

const SO_REF_OVERRIDES = {
  Cake_StrawberryPancake: "model_StrawberryPancake",
  MixedFlourEggStrawberry: "model_StrawberryPancake",
  Cake_BlueberryPancake: "model_dlc5_recipe_blueberry_pancake_01",
  MixedFlourEggBlueberry: "model_dlc5_recipe_blueberry_pancake_01",
};

const COMPOSITE_PREFIX = [
  ["Burger_", "compositeburger"],
  ["Pizza_", "compositepizza"],
  ["Salad_", "compositesalad"],
  ["Sushi_", "compositesushi"],
  ["Pasta_", "compositepasta"],
  ["Cake_", "compositecake"],
  ["Soup_", "compositesoup"],
  ["Fry_", "compositechickennuggetsandchips"],
  ["Burrito_", "compositeburrito"],
];

const FOOD_TOKENS = [
  "broccoli", "blackberry", "blueberry", "strawberry", "christmaspudding", "mincepies",
  "hotchoc", "marshmallow", "smoothie", "pancake", "noodlesoup", "fruitsalad",
  "beef", "chicken", "potato", "carrot", "mushroom", "pineapple", "tomato", "bacon",
  "sausage", "egg", "beans", "donut", "kebab", "burrito", "sushi", "pizza", "burger",
  "pasta", "salad", "soup", "cake", "smores", "melon", "banana", "grapes", "peach",
  "orange", "cherry", "apple", "pie", "leek", "cheese", "pepperoni", "prawn", "fish",
  "meat", "rice", "noodle",
];

function isMealPrefabContainer(c) {
  const lower = c.toLowerCase();
  if (!lower.endsWith(".prefab")) return false;
  return (
    lower.includes("/meals/") ||
    lower.includes("/prefabs/recipes/") ||
    lower.includes("/plated_") ||
    lower.includes("m_recipe_") ||
    lower.includes("composite") ||
    (lower.includes("p_dlc") && lower.includes("recipe"))
  );
}

export function buildRecipeVisualContext(repoRoot, manifest, recipeIcons = {}) {
  const prefabByBase = new Map();
  const mealPrefabBases = [];
  for (const o of manifest.objects || []) {
    const c = (o.container || "").toLowerCase();
    if (!c.endsWith(".prefab")) continue;
    const base = c.slice(c.lastIndexOf("/") + 1, -".prefab".length);
    if (!prefabByBase.has(base)) prefabByBase.set(base, o);
    if (isMealPrefabContainer(c)) mealPrefabBases.push(base);
  }

  const existingSoById = new Map();
  const walkSo = (dir) => {
    if (!fs.existsSync(dir)) return;
    for (const n of fs.readdirSync(dir)) {
      const p = path.join(dir, n);
      if (fs.statSync(p).isDirectory()) walkSo(p);
      else if (n.endsWith(".asset")) {
        const text = fs.readFileSync(p, "utf8");
        if (!text.includes(GUID_PseudoPrefabSO) || text.includes(GUID_PseudoPrefabSORecipe)) continue;
        const assetPath = text.match(/assetPath:\s*(.+)/)?.[1]?.trim().replace(/\\/g, "/");
        if (!assetPath || !/\.prefab$/i.test(assetPath)) continue;
        const id = path.basename(p, ".asset");
        const g = fs.readFileSync(p + ".meta", "utf8").match(/^guid:\s*([a-f0-9]+)/m)?.[1];
        if (g) existingSoById.set(id, { guid: g, assetPath, file: p });
      }
    }
  };
  for (const root of ["Assets/common01", "Assets/common02", "Assets/common03"]) {
    walkSo(path.join(repoRoot, root));
  }

  let fileOverrides = {};
  const overridePath = path.join(repoRoot, "layout-editor/scripts/data/recipe-visual-overrides.json");
  if (fs.existsSync(overridePath)) {
    try {
      fileOverrides = JSON.parse(fs.readFileSync(overridePath, "utf8"));
    } catch {
      /* ignore */
    }
  }

  return { prefabByBase, mealPrefabBases, existingSoById, recipeIcons, fileOverrides };
}

function iconCandidates(icon) {
  if (!icon || icon === "null") return [];
  const out = [];
  if (icon.startsWith("ui_")) {
    let b = icon.slice(3).replace(/_\d+$/, "");
    out.push(`recipe_${b.toLowerCase()}_01`);
    const snake = b.replace(/([a-z])([A-Z])/g, "$1_$2").toLowerCase();
    out.push(`recipe_${snake}_01`);
    out.push(snake.replace(/_/g, ""));
    out.push(b.toLowerCase());
  }
  return out;
}

function mashedTokens(id) {
  const s = id.toLowerCase().replace(/_so$/i, "").replace(/^dlc\d+_/i, "");
  const parts = [];
  let rest = s;
  while (rest.length > 0) {
    const hit = FOOD_TOKENS.filter((t) => rest.startsWith(t)).sort((a, b) => b.length - a.length)[0];
    if (hit) {
      parts.push(hit);
      rest = rest.slice(hit.length);
    } else {
      const next = rest.match(/^[^a-z]*([a-z]+)/);
      if (!next) break;
      parts.push(next[1]);
      rest = rest.slice(next[0].length);
    }
  }
  return parts.length ? parts : s.split(/[^a-z0-9]+/).filter((t) => t.length > 2);
}

function tokenMatchPrefab(id, mealPrefabBases) {
  const tokens = mashedTokens(id);
  if (!tokens.length) return null;
  let best = null;
  let bestScore = 0;
  for (const base of mealPrefabBases) {
    const b = base.toLowerCase();
    const score = tokens.filter((t) => b.includes(t)).length;
    if (score > bestScore) {
      bestScore = score;
      best = base;
    }
  }
  return bestScore >= Math.min(2, tokens.length) ? best : null;
}

function isDecorCandidate(id, score) {
  if (/cosmetic|_raw_so$/i.test(id)) return false;
  if (/^optional/i.test(id) && !PREFAB_OVERRIDES[id]) return false;
  if (/permutation/i.test(id)) return false;
  if (score === 0 && !PREFAB_OVERRIDES[id] && !SO_REF_OVERRIDES[id]) return false;
  return true;
}

/**
 * @returns {{ kind: "so", soGuid: string, prefabBase?: string } | { kind: "prefab", prefabBase: string, bundle: string, assetPath: string } | null}
 */
export function resolveRecipeVisual(recipeId, ctx, { score = 100 } = {}) {
  if (!isDecorCandidate(recipeId, score)) return null;

  const fileOv = ctx.fileOverrides[recipeId];
  if (fileOv?.soId && ctx.existingSoById.has(fileOv.soId)) {
    const so = ctx.existingSoById.get(fileOv.soId);
    return { kind: "so", soGuid: so.guid, prefabBase: path.basename(so.assetPath, ".prefab") };
  }
  if (fileOv?.prefabBase && ctx.prefabByBase.has(fileOv.prefabBase)) {
    const o = ctx.prefabByBase.get(fileOv.prefabBase);
    const assetPath = "Assets/" + o.container.replace(/^assets\//i, "");
    return { kind: "prefab", prefabBase: fileOv.prefabBase, bundle: o.bundle, assetPath };
  }

  if (SO_REF_OVERRIDES[recipeId] && ctx.existingSoById.has(SO_REF_OVERRIDES[recipeId])) {
    const so = ctx.existingSoById.get(SO_REF_OVERRIDES[recipeId]);
    return { kind: "so", soGuid: so.guid, prefabBase: path.basename(so.assetPath, ".prefab") };
  }

  if (ctx.existingSoById.has(recipeId)) {
    const so = ctx.existingSoById.get(recipeId);
    return { kind: "so", soGuid: so.guid, prefabBase: path.basename(so.assetPath, ".prefab") };
  }

  const overrideBase = PREFAB_OVERRIDES[recipeId];
  if (overrideBase && ctx.prefabByBase.has(overrideBase)) {
    const o = ctx.prefabByBase.get(overrideBase);
    const assetPath = "Assets/" + o.container.replace(/^assets\//i, "");
    return { kind: "prefab", prefabBase: overrideBase, bundle: o.bundle, assetPath };
  }

  for (const c of iconCandidates(ctx.recipeIcons[recipeId])) {
    if (ctx.prefabByBase.has(c)) {
      const o = ctx.prefabByBase.get(c);
      const assetPath = "Assets/" + o.container.replace(/^assets\//i, "");
      return { kind: "prefab", prefabBase: c, bundle: o.bundle, assetPath };
    }
  }

  if (recipeId.startsWith("Burger_") && ctx.existingSoById.has("CompositeBurgerSO")) {
    const so = ctx.existingSoById.get("CompositeBurgerSO");
    return { kind: "so", soGuid: so.guid, prefabBase: "CompositeBurger" };
  }
  if (recipeId.startsWith("Pizza_") && ctx.existingSoById.has("CompositePizzaSO")) {
    const so = ctx.existingSoById.get("CompositePizzaSO");
    return { kind: "so", soGuid: so.guid, prefabBase: "CompositePizza" };
  }

  for (const [prefix, comp] of COMPOSITE_PREFIX) {
    if (recipeId.startsWith(prefix) && ctx.prefabByBase.has(comp)) {
      const o = ctx.prefabByBase.get(comp);
      const assetPath = "Assets/" + o.container.replace(/^assets\//i, "");
      return { kind: "prefab", prefabBase: comp, bundle: o.bundle, assetPath };
    }
  }

  const fuzzy = tokenMatchPrefab(recipeId, ctx.mealPrefabBases);
  if (fuzzy && ctx.prefabByBase.has(fuzzy)) {
    const o = ctx.prefabByBase.get(fuzzy);
    const assetPath = "Assets/" + o.container.replace(/^assets\//i, "");
    return { kind: "prefab", prefabBase: fuzzy, bundle: o.bundle, assetPath };
  }

  return null;
}

export function listRecipeAssets(repoRoot) {
  const roots = [
    "Assets/common01/food/Recipes",
    "Assets/common01/food/CustomRecipes",
    "Assets/common02/food/Recipes",
    "Assets/common03/Recipes",
  ];
  const out = [];
  const walk = (dir) => {
    for (const n of fs.readdirSync(dir)) {
      const p = path.join(dir, n);
      if (fs.statSync(p).isDirectory()) walk(p);
      else if (n.endsWith(".asset")) {
        const text = fs.readFileSync(p, "utf8");
        if (!text.includes(GUID_PseudoPrefabSORecipe)) continue;
        const id = path.basename(p, ".asset");
        const scoreM = text.match(/^  score:\s*(\d+)/m);
        const score = scoreM ? Number(scoreM[1]) : 0;
        const rel = path.relative(repoRoot, p).replace(/\\/g, "/");
        out.push({ id, score, assetPath: rel });
      }
    }
  };
  for (const root of roots) {
    const abs = path.join(repoRoot, root);
    if (fs.existsSync(abs)) walk(abs);
  }
  return out;
}
