import type { CatalogItem, CatalogStackMeta, LayoutItem } from "./types";

export type EditorLike = LayoutItem & {
  _editorKey: string;
  _wx: number;
  _wz: number;
  _parentWx: number;
  _parentWz: number;
};

const CELL = 1.2;

/** Host rules for utensils (see 使用手册 §3.3). */
export function hostMatchesRule(hostPrefabId: string, rule: string): boolean {
  const id = hostPrefabId;
  if (rule === "counter_standard") {
    if (id.includes("CounterCorner")) return false;
    const counterLike = [
      "Counter",
      "ChoppingCounter",
      "Dispenser",
      "Bin",
      "ServingStation",
      "PlateReturn",
      "Sink",
      "Cooker",
      "FryingStation",
      "Mixer",
      "Oven",
      "ConveyorStation",
    ];
    return counterLike.some((p) => id === p || id.startsWith(p));
  }
  if (rule === "cooker") return id === "Cooker";
  if (rule === "frying_station") return id === "FryingStation";
  if (rule === "mixer") return id === "Mixer" || id.startsWith("Mixer");
  if (rule === "blender") return id === "Blender";
  if (rule === "barbeque") return id === "Barbeque";
  if (rule === "campfire") return id === "Campfire";
  return false;
}

export function isStackHostCatalog(cat: CatalogItem | undefined): boolean {
  if (!cat) return false;
  return cat.category === "counters" || cat.category === "mechanisms" || cat.category?.startsWith("counters/") || false;
}

export function isStackUtensilCatalog(cat: CatalogItem | undefined): boolean {
  return (
    (cat?.category === "utensils" || cat?.category?.startsWith("utensils/") === true) &&
    Boolean(cat.stack?.hostRule)
  );
}

export function hostRuleLabelZh(rule: CatalogStackMeta["hostRule"]): string {
  switch (rule) {
    case "counter_standard":
      return "桌台/操作台";
    case "cooker":
      return "灶台";
    case "frying_station":
      return "煎炸台";
    case "mixer":
      return "搅拌台";
    case "blender":
      return "搅拌机";
    case "barbeque":
      return "烧烤架";
    case "campfire":
      return "篝火";
    default:
      return rule;
  }
}

export function drawLayerForItem(
  item: EditorLike,
  catalogByGuid: Map<string, CatalogItem>
): number {
  const cat = catalogByGuid.get(item.prefabGuid);
  if (cat?.layoutTier === "decor" || cat?.category === "art") return 30;
  if (isStackUtensilCatalog(cat) || item.parentPath.includes("Utensils")) return 20;
  if (isStackHostCatalog(cat) || item.parentPath.includes("Counters")) return 10;
  return 15;
}

function footprintOf(
  item: EditorLike,
  catalogByGuid: Map<string, CatalogItem>
): { cellsX: number; cellsZ: number } {
  const cat = catalogByGuid.get(item.prefabGuid);
  const cx = item.footprint?.cellsX ?? cat?.footprint?.cellsX ?? 1;
  const cz = item.footprint?.cellsZ ?? cat?.footprint?.cellsZ ?? 1;
  return { cellsX: cx > 0 ? cx : 1, cellsZ: cz > 0 ? cz : 1 };
}

function pointInFootprint(
  item: EditorLike,
  wx: number,
  wz: number,
  catalogByGuid: Map<string, CatalogItem>
): boolean {
  const fp = footprintOf(item, catalogByGuid);
  const rot = ((item.localRotationY % 360) + 360) % 360;
  const swap = rot === 90 || rot === 270;
  const hw = ((swap ? fp.cellsZ : fp.cellsX) * CELL) / 2;
  const hh = ((swap ? fp.cellsX : fp.cellsZ) * CELL) / 2;
  const dx = wx - item._wx;
  const dz = wz - item._wz;
  const rad = (rot * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  const lx = dx * cos + dz * sin;
  const lz = -dx * sin + dz * cos;
  return Math.abs(lx) <= hw && Math.abs(lz) <= hh;
}

export function findStackHost(
  utensil: EditorLike,
  utensilCat: CatalogItem,
  wx: number,
  wz: number,
  sceneItems: EditorLike[],
  catalogByGuid: Map<string, CatalogItem>
): EditorLike | null {
  const rule = utensilCat.stack?.hostRule;
  if (!rule) return null;

  let best: EditorLike | null = null;
  let bestArea = Infinity;

  for (const host of sceneItems) {
    if (host === utensil) continue;
    const hostCat = catalogByGuid.get(host.prefabGuid);
    if (!isStackHostCatalog(hostCat)) continue;
    const hostId = hostCat?.id ?? "";
    if (!hostMatchesRule(hostId, rule)) continue;
    if (!pointInFootprint(host, wx, wz, catalogByGuid)) continue;
    const fp = footprintOf(host, catalogByGuid);
    const area = fp.cellsX * fp.cellsZ;
    if (area < bestArea) {
      bestArea = area;
      best = host;
    }
  }
  return best;
}

export function applyStackOnHost(
  utensil: EditorLike,
  host: EditorLike,
  stackY: number
): void {
  utensil._wx = host._wx;
  utensil._wz = host._wz;
  if (utensil.localPosition.y <= 0.001) {
    utensil.localPosition.y = stackY;
  }
  utensil.localPosition.x = snapScalar(utensil._wx - utensil._parentWx);
  utensil.localPosition.z = snapScalar(utensil._wz - utensil._parentWz);
}

function snapScalar(v: number): number {
  return Math.round(v * 1000) / 1000;
}

export function trySnapUtensilToHost(
  utensil: EditorLike,
  utensilCat: CatalogItem | undefined,
  sceneItems: EditorLike[],
  catalogByGuid: Map<string, CatalogItem>
): boolean {
  if (!utensilCat?.stack?.hostRule) return false;
  const host = findStackHost(
    utensil,
    utensilCat,
    utensil._wx,
    utensil._wz,
    sceneItems,
    catalogByGuid
  );
  if (!host) return false;
  applyStackOnHost(utensil, host, utensilCat.stack.y);
  return true;
}
