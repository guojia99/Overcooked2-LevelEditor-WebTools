import type {
  CatalogItem,
  CounterAppearanceOption,
  IngredientEntry
} from "../types";
import { isSurfaceItem } from "../floorColors";
import {
  S,
  CELL,
  EditorItem,
  LayerKey,
  VisibilityCategory
} from "./state";
import { prefabIdFromPath } from "./coords";
import { NODE_INGREDIENT_SOURCES } from "../recipeGroups";

/** 资产文件名 id → canonical catalog id（过渡期兼容旧数据/静态 json 混用）。 */
export const INGREDIENT_ALIASES: Record<string, string> = {
  DLC03_Chocolate: "dlc03_chocolate",
  DLC03_DriedFruit: "driedfruit",
  DLC03_Marshmallow: "marshmallow",
  DLC03_Milk: "milk",
  DLC03_Orange: "orange",
  DLC03_WhippedCream: "whippedcream",
  DLC04_Meat: "dlc04_meat",
  DLC04_Orange: "dlc04_orange",
  DLC04_Prawn: "dlc04_prawn",
  DLC04_BokChoy: "bokchoy",
  DLC04_Noodles: "noodles",
  DLC04_Grapes: "grapes",
  DLC04_Peach: "peach",
  DLC07_Apple: "apple",
  DLC07_BeefRoast: "beef_roast",
  DLC07_Blackberry: "blackberry",
  DLC07_Broccoli: "broccoli",
  DLC07_Cheese: "dlc07_cheese",
  DLC07_Cherry: "cherry",
  DLC07_ChickenRoast: "chicken_roast",
  DLC07_Leek: "leek",
  DLC07_Potato: "dlc07_potato",
  DLC08_Cheese_Sticks: "dlc08_cheese_sticks",
  DLC08_Chicken: "dlc08_chicken",
  DLC08_ChoppedBun: "dlc08_choppedbun",
  DLC08_Drink01: "drink01",
  DLC08_Drink02: "drink02",
  DLC08_Drink03: "drink03",
  DLC08_Frankfurter: "frankfurter",
  DLC08_HotdogBun: "hotdogbun",
  DLC08_Ketchup: "ketchup",
  DLC08_Mustard: "mustard",
  DLC08_Onion: "dlc08_onion",
  DLC08_Onion_Ring: "dlc08_onion_ring",
  DLC08_Potato: "dlc08_potato",
  DLC08_Raspberry: "raspberry",
  DLC11_Corn: "dlc11_corn",
  DLC11_Cucumber: "dlc11_cucumber",
  DLC11_Icecube: "icecube",
  DLC11_Lettuce: "dlc11_lettuce",
  DLC11_Milk: "dlc11_milk",
  DLC11_Onion_Salad: "dlc11_onion_salad",
  DLC11_OrangeSoda: "orangesoda",
  DLC11_RootBeer: "rootbeer",
  DLC11_Tomato: "dlc11_tomato",
  DLC11_Vanilla: "dlc11_vanilla",
  DLC13_Chocolate: "dlc13_chocolate",
  DLC13_Egg: "dlc13_egg",
  DLC13_Flour: "dlc13_flour",
  DLC13_Melon: "dlc13_melon",
  DLC13_Strawberry: "dlc13_strawberry",
};

let ingredientById = new Map<string, IngredientEntry>();
let ingredientByIdLower = new Map<string, IngredientEntry>();

/** 在 ingredientsCache 更新后调用，重建 O(1) 查找索引。 */
export function rebuildIngredientLookup(): void {
  ingredientById = new Map();
  ingredientByIdLower = new Map();
  for (const ing of S.ingredientsCache) {
    ingredientById.set(ing.id, ing);
    ingredientByIdLower.set(ing.id.toLowerCase(), ing);
  }
}

function resolveIngredientLookupId(id: string): string {
  if (!id) return id;
  if (ingredientById.has(id)) return id;
  const fromNode = NODE_INGREDIENT_SOURCES[id];
  if (fromNode && ingredientById.has(fromNode)) return fromNode;
  const fromAlias = INGREDIENT_ALIASES[id];
  if (fromAlias && ingredientById.has(fromAlias)) return fromAlias;
  const lower = ingredientByIdLower.get(id.toLowerCase());
  if (lower) return lower.id;
  return id;
}

/** Ambient / weather background effects (落雪（BGM）, 地球（BGM）, snowfall…):
 *  they belong to the background layer, never the decor layer. */
export function isAmbientBackgroundCat(cat: CatalogItem | undefined): boolean {
  if (!cat) return false;
  const s = `${cat.nameZh} ${cat.nameEn ?? ""} ${cat.id}`.toLowerCase();
  return s.includes("（bgm）") || /(snowfall|落雪|ambient|weather)/.test(s);
}

/** Water surfaces (Water_01, poolwater_01, sea planes, sea shores…): some are
 *  catalogued as decor but are pure background — never decor items. Effect and
 *  gameplay prefabs (PFX_*, *Splash, WaterGun…) are excluded. */
export function isWaterBackgroundCat(cat: CatalogItem | undefined): boolean {
  if (!cat) return false;
  const id = (cat.id ?? "").toLowerCase();
  const zh = `${cat.nameZh} ${cat.nameEn ?? ""}`;
  const path = (cat.assetPath ?? "").toLowerCase();
  if (/(pfx|splash|particle|wake|gun|lantern|waterwheel|mooncake|seagull|seaweed|seafloat|seawave)/.test(id)) return false;
  if (id === "raft_water" || id === "city_water" || id === "dynamic_raft_water") return true;
  if (/waterbase|water_edge|mapwater/.test(id)) return true;
  if (/(^|_)(water|river|ocean|poolwater|sea_plane|sea_shore)(_|$)/.test(id)) return true;
  if (/\b(water|river|ocean)\b/.test(path)) return true;
  return /水面|河流|水域|海平面|海岸|泳池水面/.test(zh);
}

/** Which editor layer a catalog/scene item belongs to: core gameplay items,
 *  decor props, floor surfaces, or background content (water/sky/ambience…). */
export function itemLayerOf(cat: CatalogItem | undefined): LayerKey {
  if (cat?.surfaceTier === "background") return "background";
  if (cat?.layoutTier === "core") return "items";
  if (isAmbientBackgroundCat(cat)) return "background";
  if (isWaterBackgroundCat(cat)) return "background";
  if (isSurfaceItem(cat)) return "floor";
  return "decor";
}

/** Path/name heuristic for items missing from the catalog: water, sky and other
 *  background prefabs never belong to the decor layer. */
function looksLikeBackgroundPath(it: {
  prefabAssetPath?: string;
  hierarchyPath?: string;
}): boolean {
  const s = `${it.prefabAssetPath ?? ""} ${it.hierarchyPath ?? ""}`.toLowerCase();
  return (
    s.includes("water") ||
    s.includes("river") ||
    s.includes("ocean") ||
    s.includes("sea_") ||
    s.includes("sand_") ||
    s.includes("sky") ||
    s.includes("background") ||
    s.includes("raft_water")
  );
}

/** 场景物品的目录条目解析：先按 guid，失败（如 custom_web 副本 guid 随运行态
 *  变化、静态目录未收录）时按 prefab id（assetPath 文件名）回退到源库条目。 */
export function catalogItemForGuidOrPath(
  guid: string | undefined,
  assetPath: string | undefined
): CatalogItem | undefined {
  if (guid) {
    const byGuid = S.catalogByGuid.get(guid);
    if (byGuid) return byGuid;
  }
  const id = prefabIdFromPath(assetPath);
  return id ? S.catalogById.get(id) : undefined;
}

/** Layer of a scene item, with a fallback heuristic for items the catalog does
 *  not know (they must never leak into the decor layer). */
export function itemLayerOfIt(it: {
  prefabGuid: string;
  prefabAssetPath?: string;
  hierarchyPath?: string;
  stubKind?: string;
}): LayerKey {
  // 空气墙等合成表面物品按 catalog 分类（surfaceTier=floor → 地板层）
  const cat = catalogItemForGuidOrPath(it.prefabGuid, it.prefabAssetPath);
  if (cat) return itemLayerOf(cat);
  // 通用（非空气墙）碰撞块归入核心物品层
  if (it.stubKind === "Collision") return "items";
  if (looksLikeBackgroundPath(it)) return "background";
  return "decor";
}

/** Visibility category of an item: pure backgrounds (water/sky/ambience) vs floors vs decor vs core items. */
export function itemCategoryOf(it: {
  prefabGuid: string;
  prefabAssetPath?: string;
  hierarchyPath?: string;
  stubKind?: string;
}): VisibilityCategory {
  const cat = catalogItemForGuidOrPath(it.prefabGuid, it.prefabAssetPath);
  if (cat) {
    if (isSurfaceItem(cat)) return cat?.surfaceTier === "background" ? "background" : "floors";
    return itemLayerOf(cat) === "background" ? "background" : itemLayerOf(cat) === "decor" ? "decor" : "items";
  }
  // 通用（非空气墙）碰撞块归入核心物品
  if (it.stubKind === "Collision") return "items";
  const layer = itemLayerOfIt(it);
  return layer === "background" ? "background" : layer === "decor" ? "decor" : "items";
}

/** Visibility category of a floor object. */
export function floorCategoryOf(f: { surfaceKind: string }): VisibilityCategory {
  return f.surfaceKind === "background" ? "background" : "floors";
}

/** Background plane items (water / sky / sand / environment backdrops) that can
 *  be resized by width/height like a floor plane. Ambient FX (落雪 / BGM…) are
 *  point emitters with no meaningful footprint, so they are excluded. */
export function isResizableBackgroundItem(it: {
  prefabGuid: string;
  prefabAssetPath?: string;
  hierarchyPath?: string;
  stubKind?: string;
}): boolean {
  if (it.stubKind && it.stubKind !== "") return false;
  if (itemCategoryOf(it) !== "background") return false;
  const cat = catalogItemForGuidOrPath(it.prefabGuid, it.prefabAssetPath);
  if (isAmbientBackgroundCat(cat)) return false;
  return true;
}

/** A "standing quad" water plane (Water_01 family: water_01 / p_dlc*_water_*):
 *  the mesh is authored in the XY plane, so its measured XZ footprint collapses
 *  to 1×1 while its Y size is a full ~1.2m tile. It only lies flat with rotX=90,
 *  and depth then maps to the local Y axis. Small floating props that merely
 *  contain "water" in their id (lanterns, sizeY≈0.85) are excluded by the height
 *  gate. DLC5 camp water/river are measured as 1×N with ~0 height when still
 *  vertical at export — same authoring, same rotX=90 requirement. */
export function isStandingWaterQuadCat(cat: CatalogItem | undefined): boolean {
  if (!isWaterBackgroundCat(cat)) return false;
  const fp = cat!.footprint;
  const cx = fp?.cellsX ?? 1;
  const cz = fp?.cellsZ ?? 1;
  const sizeY = fp?.sizeY ?? cat!.height ?? 0;
  const height = cat!.height ?? 0;

  // Classic XY quad (Water_01 / p_dlc4_water_01 …): 1×1 footprint, ~1.2m mesh height.
  if (cx <= 1 && cz <= 1 && sizeY >= 1.0) return true;

  // Mis-measured while vertical (p_dlc5_camp_water 1×13, camp_river 1×4): thin on
  // one horizontal axis, negligible renderer height — still an XY quad, not a flat XZ plane.
  if (sizeY < 0.5 && height < 0.5) {
    if (cx <= 1 && cz > 1) return true;
    if (cz <= 1 && cx > 1) return true;
  }
  return false;
}

/** Renderer bounds Y above which a background prefab is a 3D backdrop (Sky dome,
 *  balloon, raft…) rather than a flat/planar surface we size like a floor. Tuned
 *  to include the goo backdrop (alien_gue ≈0.70) but exclude floating-lantern
 *  props (≈0.83) that merely contain "water" in their id. */
const BG_PLANE_MAX_HEIGHT = 0.75;

/** Background surface planes (water / sand / sea / river…) that behave like a
 *  floor plane: lie flat, resizable by width/height in cells, and default to a
 *  manageable 6×6 on placement. Standing water quads qualify (they flatten with
 *  rotX=90); tall 3D backdrops (Sky, balloon, raft_water) and ambient FX do not. */
export function isBackgroundPlaneCat(cat: CatalogItem | undefined): boolean {
  if (!cat) return false;
  if (isAmbientBackgroundCat(cat)) return false;
  const isBg = cat.surfaceTier === "background" || isWaterBackgroundCat(cat);
  if (!isBg) return false;
  if (isStandingWaterQuadCat(cat)) return true;
  return (cat.height ?? 0) <= BG_PLANE_MAX_HEIGHT;
}

/** Native orientation of a background plane prefab. Standing water quads only lie
 *  flat with rotX=90, and the plane's local Y axis then maps to world Z — so depth
 *  is written to localScale.y, not z. Flat planes render as-is (rotX=0, depth on
 *  z). The per-scale cell size is always the catalog footprint (1 cell = 1.2m). */
export interface PlaneNative {
  rotX: number;
  depthAxis: "y" | "z";
}

const STANDING_WATER_NATIVE: PlaneNative = { rotX: 90, depthAxis: "y" };
const FLAT_PLANE_NATIVE: PlaneNative = { rotX: 0, depthAxis: "z" };

export function planeNativeForItem(it: {
  prefabGuid: string;
  prefabAssetPath?: string;
}): PlaneNative {
  const cat = catalogItemForGuidOrPath(it.prefabGuid, it.prefabAssetPath);
  return isStandingWaterQuadCat(cat) ? STANDING_WATER_NATIVE : FLAT_PLANE_NATIVE;
}

function scaleComponent(v: number | undefined): number {
  return v && v > 0 ? v : 1;
}

/** Catalog base footprint for resizable background planes (1×1 for standing water quads).
 *  Unity export stores renderer-measured footprint on the item (e.g. 1×26); that is
 *  the effective size at export time and must not be multiplied again with localScale. */
export function planeCatalogFootprint(it: {
  prefabGuid: string;
  prefabAssetPath?: string;
}): { cellsX: number; cellsZ: number } {
  const cat = catalogItemForGuidOrPath(it.prefabGuid, it.prefabAssetPath);
  if (isStandingWaterQuadCat(cat)) {
    return { cellsX: 1, cellsZ: 1 };
  }
  const fp = cat?.footprint;
  if (fp && fp.cellsX > 0 && fp.cellsZ > 0) {
    return { cellsX: fp.cellsX, cellsZ: fp.cellsZ };
  }
  return { cellsX: 1, cellsZ: 1 };
}

/** Effective footprint of a resizable background plane in grid cells, accounting
 *  for its native depth axis (Y for rotX=90 water planes). */
export function itemPlaneCells(it: EditorItem): { wCells: number; dCells: number } {
  const nat = planeNativeForItem(it);
  const sx = scaleComponent(it.localScale?.x);
  const depthScale = scaleComponent(nat.depthAxis === "y" ? it.localScale?.y : it.localScale?.z);
  const fp = planeCatalogFootprint(it);
  return {
    wCells: Math.max(1, Math.round((fp.cellsX || 1) * sx)),
    dCells: Math.max(1, Math.round((fp.cellsZ || 1) * depthScale)),
  };
}

/** Inverse of itemPlaneCells: the localScale that yields the given cell size,
 *  writing depth to the correct axis for the prefab. */
export function planeScaleFromCells(
  it: EditorItem,
  wCells: number,
  dCells: number
): { x: number; y: number; z: number } {
  const nat = planeNativeForItem(it);
  const fp = planeCatalogFootprint(it);
  const sx = wCells / Math.max(1, fp.cellsX || 1);
  const sd = dCells / Math.max(1, fp.cellsZ || 1);
  return nat.depthAxis === "y"
    ? { x: sx, y: sd, z: 1 }
    : { x: sx, y: scaleComponent(it.localScale?.y), z: sd };
}

/** Is the given content category currently visible on the active layer? */
export function categoryVisible(cat: VisibilityCategory): boolean {
  return S.layerVisibility[S.currentLayer][cat];
}

export function isActiveItemLayer(it: {
  prefabGuid: string;
  prefabAssetPath?: string;
  hierarchyPath?: string;
}): boolean {
  return itemLayerOfIt(it) === S.currentLayer;
}

export function levelSetFromScenePath(assetPath: string): string {
  const parts = assetPath.replace(/\\/g, "/").split("/");
  const i = parts.indexOf("LevelSets");
  return i >= 0 && parts.length > i + 1 ? parts[i + 1] : "";
}

export function counterTypeOfItem(item: EditorItem): string | null {
  const prefabId = prefabIdFromPath(item.prefabAssetPath);
  if (!S.counterAppearances) return null;
  if (S.counterAppearances.byType[prefabId]) return prefabId;
  const sortedTypes = Object.keys(S.counterAppearances.byType).sort((a, b) => b.length - a.length);
  for (const ct of sortedTypes) {
    if (prefabId.startsWith(ct)) return ct;
  }
  return null;
}

export function counterAppearanceOptions(item: EditorItem): CounterAppearanceOption[] {
  const ct = counterTypeOfItem(item);
  if (!ct) return [];
  return S.counterAppearances?.byType[ct] ?? [];
}

export function catalogItemById(id: string): CatalogItem | undefined {
  for (const it of S.catalogByGuid.values()) if (it.id === id) return it;
  return undefined;
}

export function ingredientEntryById(id: string): IngredientEntry | undefined {
  if (!id) return undefined;
  const resolved = resolveIngredientLookupId(id);
  return ingredientById.get(resolved) ?? ingredientByIdLower.get(id.toLowerCase());
}

export function foodIconImg(kind: "ingredients" | "recipes", id: string | undefined, hasIcon?: boolean): string {
  // Try the real icon whenever we have an id and the flag isn't explicitly false (the live bridge
  // catalog omits the flag, so we rely on onerror to fall back to the placeholder for missing PNGs).
  const src = id && hasIcon !== false ? `/icons/${kind}/${id}.png` : "/icons/_placeholder.png";
  return `<img class="pc-img" loading="lazy" src="${src}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`;
}

/** 自定义菜谱（关卡集）的成品图标 URL（经桥接从 CustomRecipeSO.icon 资产读取）。 */
export function customRecipeIconUrl(r: { isCustom?: boolean; assetPath?: string }): string | undefined {
  return r.isCustom && r.assetPath
    ? `/api/custom-recipes/icon?assetPath=${encodeURIComponent(r.assetPath)}`
    : undefined;
}

export function ingredientGuidById(id: string): string | undefined {
  return ingredientEntryById(id)?.guid;
}

export function ingredientIdByGuid(guid: string | undefined): string | undefined {
  if (!guid) return undefined;
  return S.ingredientsCache.find((i) => i.guid === guid)?.id;
}

export function ingredientNameById(id: string): string {
  return ingredientEntryById(id)?.nameZh ?? id;
}
