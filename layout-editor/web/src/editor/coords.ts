import {
  snapFootprintCenter,
  snapCenterPivot,
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
  CENTER_PIVOT_PREFAB_IDS,
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
  const cat = S.catalogByGuid.get(prefabGuid);
  if (!S.snapEnabled || itemLayerOf(cat) === "decor") return free;
  // 中心 pivot 道具（断头台）：长轴强制吸到半格奇偶位（0.6 mod 1.2），不做阈值
  // 门控——否则一半概率落在错位格，且后端已解除 Unity 侧自动纠偏，错位会持久化。
  if (cat && CENTER_PIVOT_PREFAB_IDS.has(cat.id)) {
    return snapCenterPivot(wx, wz, cellsX, cellsZ, rotY, CELL);
  }
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
  // 半格小型厨具/工具（<1 格）：目录/内置表为准（旧文档误设为 1×1 的历史污染以目录修正）。
  if (catFp && (catFp.cellsX < 1 || catFp.cellsZ < 1)) {
    return { cellsX: catFp.cellsX, cellsZ: catFp.cellsZ };
  }
  if (known && (known.cellsX < 1 || known.cellsZ < 1)) {
    return { cellsX: known.cellsX, cellsZ: known.cellsZ };
  }
  // 旧数据强制刷新：文档 footprint 任一边 <1 而权威表（目录/内置表）≥1 时以权威值
  // 纠正（v0.6 beta03 误设 0.9 的搅拌机/搅拌杯：权威表已改回 1×1，加载与回写均刷新）。
  if (item.footprint && (item.footprint.cellsX < 1 || item.footprint.cellsZ < 1)) {
    const authFp = catFp && catFp.cellsX > 0 && catFp.cellsZ > 0 ? catFp : known;
    if (authFp && authFp.cellsX >= 1 && authFp.cellsZ >= 1) {
      return { cellsX: authFp.cellsX, cellsZ: authFp.cellsZ };
    }
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

/** UUID 生成：crypto.randomUUID 仅在安全上下文（HTTPS/localhost）可用，
 *  通过 IP/主机名访问 Unity 内嵌页面时不存在，回退到 RFC4122 v4 手工实现。 */
export function uuid(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  if (typeof crypto !== "undefined" && typeof crypto.getRandomValues === "function") {
    const b = crypto.getRandomValues(new Uint8Array(16));
    b[6] = (b[6] & 0x0f) | 0x40;
    b[8] = (b[8] & 0x3f) | 0x80;
    const h = Array.from(b, (x) => x.toString(16).padStart(2, "0")).join("");
    return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
  }
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export function newEditorKey(): string {
  return uuid();
}

export function escHtml(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

export type { CatalogItem };
