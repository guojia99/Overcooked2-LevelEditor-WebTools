/**
 * 物件世界空间包围盒（写回校验等与 3D itemBoxOf 口径对齐，但不依赖 three.js）。
 */

import { catalogItemForGuidOrPath } from "./catalog";
import { EditorItem } from "./state";
import {
  FALLBACK_ITEM_HEIGHT,
  MAX_ITEM_HEIGHT,
  MAX_PIVOT_OFFSET,
  MIN_ITEM_HEIGHT,
} from "./scene3d/constants";

function pivotOffset(baseY: number | undefined, clamped: boolean): number {
  if (baseY == null || !Number.isFinite(baseY)) return 0;
  if (clamped) return 0;
  if (Math.abs(baseY) > MAX_PIVOT_OFFSET) return 0;
  return baseY;
}

/** 世界 Y 范围 [minY, maxY]（底面 baseY + catalog 实测高度，与 meshItems.itemBoxOf 一致）。 */
export function itemWorldYExtents(item: EditorItem): { minY: number; maxY: number } {
  const cat = catalogItemForGuidOrPath(item.prefabGuid, item.prefabAssetPath);
  const itemY = item.localPosition?.y ?? 0;
  const rawH = cat?.height ?? cat?.footprint?.sizeY ?? FALLBACK_ITEM_HEIGHT;
  const h = Math.min(MAX_ITEM_HEIGHT, Math.max(MIN_ITEM_HEIGHT, rawH));
  const clamped = rawH > MAX_ITEM_HEIGHT;
  const baseY = itemY + pivotOffset(cat?.baseY, clamped);
  return { minY: baseY, maxY: baseY + h };
}

export function aabbOverlap3D(
  a: { minX: number; maxX: number; minZ: number; maxZ: number; minY: number; maxY: number },
  b: { minX: number; maxX: number; minZ: number; maxZ: number; minY: number; maxY: number },
  eps = 0.02
): boolean {
  return (
    Math.min(a.maxX, b.maxX) - Math.max(a.minX, b.minX) > eps &&
    Math.min(a.maxZ, b.maxZ) - Math.max(a.minZ, b.minZ) > eps &&
    Math.min(a.maxY, b.maxY) - Math.max(a.minY, b.minY) > eps
  );
}
