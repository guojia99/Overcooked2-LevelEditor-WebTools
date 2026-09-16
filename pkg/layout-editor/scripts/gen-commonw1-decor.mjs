#!/usr/bin/env node
/**
 * 生成 commonW1 食材/成品菜装饰包装（prefab + 可选 pseudo_prefab_so）。
 *
 *   node layout-editor/scripts/gen-commonw1-decor.mjs
 *   node layout-editor/scripts/gen-commonw1-decor.mjs --food-only
 *   node layout-editor/scripts/gen-commonw1-decor.mjs --recipes-only
 */
import { emitIngredientDecorWrappers, emitRecipeDecorWrappers } from "./import-dlc-content.mjs";

const foodOnly = process.argv.includes("--food-only");
const recipesOnly = process.argv.includes("--recipes-only");

console.log("生成 commonW1 装饰包装（食材 + 成品菜）…");
if (!recipesOnly) emitIngredientDecorWrappers();
if (!foodOnly) emitRecipeDecorWrappers();
console.log("完成。");
