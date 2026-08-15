import type {
  CatalogItem,
  CounterAppearanceOption
} from "../types";
import { isSurfaceItem } from "../floorColors";
import {
  S,
  EditorItem,
  LayerKey,
  VisibilityCategory
} from "./state";
import { prefabIdFromPath } from "./coords";

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
  if (/(pfx|splash|particle|wake|gun)/.test(id)) return false;
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

export function ingredientEntryById(id: string) {
  return S.ingredientsCache.find((i) => i.id === id);
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
