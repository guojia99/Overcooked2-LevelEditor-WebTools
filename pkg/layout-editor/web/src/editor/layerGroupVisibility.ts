import type { EditorFloor, EditorItem } from "./state";
import { S, type SceneListLayer } from "./state";
import { catalogItemForGuidOrPath, itemCategoryOf, itemLayerOfIt } from "./catalog";
import { isCollisionItem } from "./stubControls";
import { itemLabel } from "./labels";
import { isSelected } from "./selection";
import { itemInCurrentPart, floorInCurrentPart } from "./partLevel";

/** Display-name key for decor layer grouping / batch hide (matches right panel). */
export function decorGroupKey(it: EditorItem): string {
  return itemLabel(it);
}

export function floorGroupKey(f: EditorFloor): string {
  const name = (f.displayName || "").trim();
  return `floor:${name || f._key}`;
}

export function surfaceGroupKey(it: EditorItem): string {
  return `surf:${itemLabel(it)}`;
}

export function coreGroupKey(it: EditorItem): string {
  const cat = catalogItemForGuidOrPath(it.prefabGuid, it.prefabAssetPath);
  const key = cat?.category && S.corePaletteGroupMeta.has(cat.category) ? cat.category : "__other";
  return key;
}

export function hiddenSetForLayer(layer: SceneListLayer): Set<string> {
  return S.hiddenSceneGroups[layer];
}

export function isGroupHidden(layer: SceneListLayer, key: string): boolean {
  return hiddenSetForLayer(layer).has(key);
}

export function toggleGroupHidden(layer: SceneListLayer, key: string): void {
  const set = hiddenSetForLayer(layer);
  if (set.has(key)) set.delete(key);
  else set.add(key);
}

export function clearAllHiddenSceneGroups(): void {
  for (const layer of SCENE_LIST_LAYERS) {
    hiddenSetForLayer(layer).clear();
  }
}

export function hiddenSceneGroupsSig(): string {
  return SCENE_LIST_LAYERS
    .map((l) => `${l}:${[...hiddenSetForLayer(l)].sort().join(",")}`)
    .join("\0");
}

const SCENE_LIST_LAYERS: SceneListLayer[] = ["decor", "floor", "background", "items"];

function sceneListLayerForFloor(f: EditorFloor): "floor" | "background" {
  return f.surfaceKind === "background" ? "background" : "floor";
}

function groupKeyForItemOnSceneList(it: EditorItem): { layer: SceneListLayer; key: string } | null {
  const cat = itemCategoryOf(it);
  if (cat === "decor") return { layer: "decor", key: decorGroupKey(it) };
  if (cat === "items") return { layer: "items", key: coreGroupKey(it) };
  if (cat === "floors") return { layer: "floor", key: surfaceGroupKey(it) };
  if (cat === "background") return { layer: "background", key: surfaceGroupKey(it) };
  return null;
}

export function isDecorGroupHidden(it: EditorItem): boolean {
  if (itemLayerOfIt(it) !== "decor") return false;
  return isGroupHidden("decor", decorGroupKey(it));
}

export function isItemHiddenBySceneGroup(it: EditorItem): boolean {
  const g = groupKeyForItemOnSceneList(it);
  if (!g) return false;
  if (!isGroupHidden(g.layer, g.key)) return false;
  return !isSelected(it._editorKey);
}

/** @deprecated Use isItemHiddenBySceneGroup */
export function isItemHiddenByDecorGroup(it: EditorItem): boolean {
  return isItemHiddenBySceneGroup(it);
}

export function isFloorHiddenBySceneGroup(f: EditorFloor): boolean {
  const layer = sceneListLayerForFloor(f);
  if (!isGroupHidden(layer, floorGroupKey(f))) return false;
  return !S.selectedFloorKeys.has(f._key);
}

export function enumerateGroupKeysForLayer(layer: SceneListLayer): string[] {
  const keys = new Set<string>();
  if (layer === "decor") {
    for (const it of S.items) {
      if (itemLayerOfIt(it) !== "decor" || !itemInCurrentPart(it)) continue;
      keys.add(decorGroupKey(it));
    }
    return [...keys].sort((a, b) => a.localeCompare(b, "zh"));
  }
  if (layer === "items") {
    for (const it of S.items) {
      if (itemLayerOfIt(it) !== "items" || !itemInCurrentPart(it)) continue;
      keys.add(coreGroupKey(it));
    }
    return [...keys].sort((a, b) => {
      const la = S.corePaletteGroupMeta.get(a) ?? (a === "__other" ? "其他" : a);
      const lb = S.corePaletteGroupMeta.get(b) ?? (b === "__other" ? "其他" : b);
      return la.localeCompare(lb, "zh");
    });
  }
  const layerBg = layer === "background";
  for (const f of S.floors) {
    if (!floorInCurrentPart(f)) continue;
    const isBg = f.surfaceKind === "background";
    if (isBg === layerBg) keys.add(floorGroupKey(f));
  }
  for (const it of S.items) {
    if (!itemInCurrentPart(it) || isCollisionItem(it)) continue;
    const cat = itemCategoryOf(it);
    if (layerBg ? cat === "background" : cat === "floors") {
      keys.add(surfaceGroupKey(it));
    }
  }
  return [...keys].sort((a, b) => a.localeCompare(b, "zh"));
}

export function pruneHiddenSceneGroups(): void {
  for (const layer of SCENE_LIST_LAYERS) {
    const valid = new Set(enumerateGroupKeysForLayer(layer));
    const set = hiddenSetForLayer(layer);
    for (const k of [...set]) {
      if (!valid.has(k)) set.delete(k);
    }
  }
}

export function showAllGroupsOnLayer(layer: SceneListLayer): void {
  hiddenSetForLayer(layer).clear();
}

export function hideAllGroupsOnLayer(layer: SceneListLayer): void {
  const set = hiddenSetForLayer(layer);
  set.clear();
  for (const k of enumerateGroupKeysForLayer(layer)) {
    set.add(k);
  }
}
