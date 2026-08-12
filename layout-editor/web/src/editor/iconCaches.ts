import { imageFloorUrl } from "../api";

let redraw: () => void = () => {};

/** 注入画布重绘回调（由 render 模块调用，解决 iconCache onload → draw 的循环依赖）。 */
export function setRedraw(fn: () => void): void {
  redraw = fn;
}

/** Image-floor texture cache: texturePath → HTMLImageElement (loaded or loading).
 *  On load completion we trigger a redraw so the image appears on the canvas. */
const floorImageCache = new Map<string, HTMLImageElement>();
export function getFloorImage(texturePath: string): HTMLImageElement | null {
  if (!texturePath) return null;
  const existing = floorImageCache.get(texturePath);
  if (existing) return existing.complete && existing.naturalWidth > 0 ? existing : null;
  const img = new Image();
  img.onload = () => redraw();
  img.src = imageFloorUrl(texturePath);
  floorImageCache.set(texturePath, img);
  return null;
}

export function clearFloorImageCache(texturePath: string): void {
  floorImageCache.delete(texturePath);
}

/** Ingredient icon cache: ingredientId → HTMLImageElement. Used to draw the selected ingredient
 *  icon on 食材箱 (Dispenser) items on the layout canvas. Triggers a redraw on load. */
const ingredientIconCache = new Map<string, HTMLImageElement>();
export function getIngredientIcon(ingId: string): HTMLImageElement | null {
  if (!ingId) return null;
  const existing = ingredientIconCache.get(ingId);
  if (existing) return existing.complete && existing.naturalWidth > 0 ? existing : null;
  const img = new Image();
  img.onload = () => redraw();
  img.src = `/icons/ingredients/${ingId}.png`;
  ingredientIconCache.set(ingId, img);
  return null;
}

/** Core-item (锅具/道具) icon cache: catalogId → HTMLImageElement. */
const catalogIconCache = new Map<string, HTMLImageElement>();
export function getCatalogIcon(catId: string): HTMLImageElement | null {
  if (!catId) return null;
  const existing = catalogIconCache.get(catId);
  if (existing) return existing.complete && existing.naturalWidth > 0 ? existing : null;
  const img = new Image();
  img.onload = () => redraw();
  img.src = `/icons/catalog/${catId}.png`;
  catalogIconCache.set(catId, img);
  return null;
}
