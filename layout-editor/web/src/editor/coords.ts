import {
  snapFootprintCenter,
  snapValue
} from "../snap";
export { snapValue };
import {
  S,
  CELL,
  HALF_CELL,
  MAGNET_THRESHOLD,
  PX_PER_UNIT,
  FOOTPRINT_BY_ID,
  EditorItem
} from "./state";
import { dom } from "./dom";
import type {
  CatalogItem,
  LayoutItem
} from "../types";
import { itemLayerOf, catalogItemForGuidOrPath } from "./catalog";

/** Decimal places needed to display positions at a given step (0.6 → 1, 0.1 → 1, 0.01 → 2, 0.001 → 3). */
export function stepDecimals(step: number): number {
  return Math.min(3, Math.max(0, Math.ceil(-Math.log10(step))));
}

/** Decimals for coordinate displays: keep at least 2 so grid-snapped positions stay readable. */
export function stepDisplayDecimals(step: number): number {
  return Math.max(2, stepDecimals(step));
}

/** Grid magnet: gameplay items snap to the half-cell (0.6) lattice only when within
 *  MAGNET_THRESHOLD of it; otherwise they move freely at the selected precision.
 *  Decor items always place freely, same as the decoration layer. */
export function snapPlacement(
  cellsX: number,
  cellsZ: number,
  rotY: number,
  prefabGuid: string,
  wx: number,
  wz: number
): { x: number; z: number } {
  const free = { x: snapValue(wx, S.freeSnapStep), z: snapValue(wz, S.freeSnapStep) };
  if (!S.snapEnabled || itemLayerOf(S.catalogByGuid.get(prefabGuid)) === "decor") return free;
  const snapped = snapFootprintCenter(wx, wz, cellsX, cellsZ, rotY, CELL, HALF_CELL);
  return Math.hypot(snapped.x - wx, snapped.z - wz) <= MAGNET_THRESHOLD ? snapped : free;
}

export function normalizeRot(deg: number): number {
  return ((deg % 360) + 360) % 360;
}

export function prefabIdFromPath(assetPath: string | undefined): string {
  if (!assetPath) return "";
  const name = assetPath.split("/").pop() ?? "";
  return name.replace(/\.prefab$/i, "");
}

export function resolveFootprint(item: LayoutItem): { cellsX: number; cellsZ: number } {
  const cat = catalogItemForGuidOrPath(item.prefabGuid, item.prefabAssetPath);
  const catFp = cat?.footprint;
  const id = prefabIdFromPath(item.prefabAssetPath) || cat?.id || "";
  const known = FOOTPRINT_BY_ID[id];

  // 已知多格道具：目录/内置表为准（即使旧桥接回传了过时的 1×1，如火锅大锅占地修复前）。
  if (catFp && (catFp.cellsX > 1 || catFp.cellsZ > 1)) {
    return { cellsX: catFp.cellsX, cellsZ: catFp.cellsZ };
  }
  if (known && (known.cellsX > 1 || known.cellsZ > 1)) {
    return known;
  }

  const cx = item.footprint?.cellsX ?? 0;
  const cz = item.footprint?.cellsZ ?? 0;
  if (cx > 0 && cz > 0) return { cellsX: cx, cellsZ: cz };
  if (catFp && catFp.cellsX > 0 && catFp.cellsZ > 0) {
    return { cellsX: catFp.cellsX, cellsZ: catFp.cellsZ };
  }
  if (known) return known;
  return { cellsX: 1, cellsZ: 1 };
}

export function itemScaleX(item: LayoutItem): number {
  const s = item.localScale?.x;
  return s && s > 0 ? s : 1;
}

export function itemScaleZ(item: LayoutItem): number {
  const s = item.localScale?.z;
  return s && s > 0 ? s : 1;
}

export function itemUniformScale(item: LayoutItem): number {
  return itemScaleX(item);
}

export function setItemUniformScale(item: EditorItem, n: number) {
  const y = item.localScale?.y ?? 1;
  item.localScale = { x: n, y, z: n };
}

export function worldToCanvas(wx: number, wz: number): { x: number; y: number } {
  const cx = dom.canvas.width / 2 + S.panX + wx * PX_PER_UNIT * S.scale;
  const cy = dom.canvas.height / 2 + S.panY - wz * PX_PER_UNIT * S.scale;
  return { x: cx, y: cy };
}

export function canvasToWorld(cx: number, cy: number): { x: number; z: number } {
  const wx = (cx - dom.canvas.width / 2 - S.panX) / (PX_PER_UNIT * S.scale);
  const wz = -(cy - dom.canvas.height / 2 - S.panY) / (PX_PER_UNIT * S.scale);
  return { x: wx, z: wz };
}

export function newEditorKey(): string {
  return crypto.randomUUID();
}

export function escHtml(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

export type { CatalogItem };
