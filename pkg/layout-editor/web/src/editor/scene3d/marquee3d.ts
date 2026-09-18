/**
 * 3D 框选：屏幕矩形 → 世界对象投影判定。
 *
 * 判定规则与 2D 的 marqueeHitsItem（render.ts:280）保持一致：
 *   · 普通物件 = 中心点落在矩形内
 *   · 空气墙   = 整个 AABB 与矩形相交（8 个角投影后取屏幕包围盒）
 * 这样「2D 里能框到什么，3D 里就能框到什么」。
 *
 * 框选矩形本身画在一个覆盖画布的 DOM 元素上（而不是 WebGL 里），
 * 这样零成本拿到与 2D 一致的虚线观感。
 */

import { S, EditorItem, isFloorLikeLayer } from "../state";
import { isAirWallItem } from "../stubControls";
import { setSelection, setFloorSelection } from "../selection";
import { itemCategoryOf, floorCategoryOf, categoryVisible } from "../catalog";
import { itemInHeightFilter, floorInHeightFilter } from "../floorHeight";
import { Scene3DCtx } from "./ctx";
import { worldToScreen } from "./picking";
import { itemBoxOf } from "./meshItems";
import { floorSlabOf, gapBelowFor } from "./meshFloors";
import { itemPickable3D } from "./sceneBuild";

let marqueeEl: HTMLDivElement | null = null;
/** 框选开始时的既有选择（Shift 加选用）。 */
let baseItemKeys: string[] = [];
let baseFloorKeys: string[] = [];

export function ensureMarqueeEl(host: HTMLElement): void {
  if (marqueeEl) return;
  marqueeEl = document.createElement("div");
  marqueeEl.className = "marquee3d hidden";
  host.appendChild(marqueeEl);
}

export function setMarqueeRect(x0: number, y0: number, x1: number, y1: number): void {
  if (!marqueeEl) return;
  const host = marqueeEl.parentElement;
  if (!host) return;
  const rect = host.getBoundingClientRect();
  const left = Math.min(x0, x1) - rect.left;
  const top = Math.min(y0, y1) - rect.top;
  const w = Math.abs(x1 - x0);
  const h = Math.abs(y1 - y0);
  marqueeEl.style.left = left + "px";
  marqueeEl.style.top = top + "px";
  marqueeEl.style.width = w + "px";
  marqueeEl.style.height = h + "px";
  marqueeEl.classList.remove("hidden");
  if (w < 2 && h < 2) marqueeEl.classList.add("hidden");
}

export function clearMarqueeRect(): void {
  if (marqueeEl) marqueeEl.classList.add("hidden");
  baseItemKeys = [];
  baseFloorKeys = [];
}

export function disposeMarquee(): void {
  if (marqueeEl && marqueeEl.parentElement) marqueeEl.parentElement.removeChild(marqueeEl);
  marqueeEl = null;
}

/** 执行框选判定并写入选择集。 */
export function marqueeBox(
  ctx: Scene3DCtx,
  x0: number,
  y0: number,
  x1: number,
  y1: number,
  additive: boolean
): void {
  if (!additive) {
    baseItemKeys = [];
    baseFloorKeys = [];
  } else if (!baseItemKeys.length && !baseFloorKeys.length) {
    baseItemKeys = Array.from(S.selectedKeys);
    baseFloorKeys = Array.from(S.selectedFloorKeys);
  }

  const rect = ctx.canvas.getBoundingClientRect();
  const minX = Math.min(x0, x1) - rect.left;
  const maxX = Math.max(x0, x1) - rect.left;
  const minY = Math.min(y0, y1) - rect.top;
  const maxY = Math.max(y0, y1) - rect.top;

  const itemKeys = new Set(baseItemKeys);
  const floorKeys = new Set(baseFloorKeys);
  const floorLayer = isFloorLikeLayer(S.currentLayer);

  for (const it of S.items) {
    if (!categoryVisible(itemCategoryOf(it))) continue;
    if (!itemInHeightFilter(it)) continue;
    if (!itemPickable3D(it)) continue;
    if (hitsMarquee(ctx, it, minX, minY, maxX, maxY)) itemKeys.add(it._editorKey);
  }

  if (floorLayer || S.currentLayer === "anim") {
    for (const f of S.floors) {
      if (!categoryVisible(floorCategoryOf(f))) continue;
      if (!floorInHeightFilter(f)) continue;
      const isBg = f.surfaceKind === "background";
      if (floorLayer && (isBg ? S.currentLayer !== "background" : S.currentLayer === "background")) continue;
      const slab = floorSlabOf(f, gapBelowFor(f));
      const p = worldToScreen(ctx.cam, ctx.canvas, f._wx, slab.topY, f._wz);
      if (p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY) floorKeys.add(f._key);
    }
  }

  const itemList = Array.from(itemKeys);
  setSelection(itemList, itemList[itemList.length - 1]);
  const floorList = Array.from(floorKeys);
  setFloorSelection(floorList, floorList[floorList.length - 1]);
}

function hitsMarquee(
  ctx: Scene3DCtx,
  it: EditorItem,
  minX: number,
  minY: number,
  maxX: number,
  maxY: number
): boolean {
  const b = itemBoxOf(it);
  if (!isAirWallItem(it)) {
    // 普通物件：中心点判定（与 2D 完全一致）。
    const p = worldToScreen(ctx.cam, ctx.canvas, b.cx, b.baseY + b.h / 2, b.cz);
    return p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY;
  }

  // 空气墙：8 角投影取屏幕包围盒，与框选矩形相交即命中。
  const rad = (-b.rotDeg * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  let sMinX = Infinity;
  let sMaxX = -Infinity;
  let sMinY = Infinity;
  let sMaxY = -Infinity;
  for (const sx of [-1, 1]) {
    for (const sz of [-1, 1]) {
      const lx = (sx * b.w) / 2;
      const lz = (sz * b.d) / 2;
      const wx = b.cx + lx * cos + lz * sin;
      const wz = b.cz - lx * sin + lz * cos;
      for (const sy of [0, 1]) {
        const p = worldToScreen(ctx.cam, ctx.canvas, wx, b.baseY + sy * b.h, wz);
        if (p.x < sMinX) sMinX = p.x;
        if (p.x > sMaxX) sMaxX = p.x;
        if (p.y < sMinY) sMinY = p.y;
        if (p.y > sMaxY) sMaxY = p.y;
      }
    }
  }
  return !(sMaxX < minX || sMinX > maxX || sMaxY < minY || sMinY > maxY);
}
