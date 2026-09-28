import { dom } from "./dom";
import { S, CELL, PX_PER_UNIT } from "./state";
import { canvasToWorld, worldToCanvas } from "./coords";
import { computeLevelBounds, drawGrid } from "./render";
import {
  drawFloorPlanes,
  drawFloorAdjacentSeams,
  drawWalkable,
  drawSurfaceItems,
} from "./renderFloors";
import { drawItem, itemDrawCompare } from "./renderItems";
import { itemLayerOfIt } from "./catalog";
import { itemInHeightFilter } from "./floorHeight";
import {
  WORKLOAD_COLORS,
  WORKLOAD_FILL_ALPHA,
  WORKLOAD_STROKE_ALPHA,
  workloadCellKey,
} from "./workloadAnalysis";

const WL_SCALE_MIN = 0.25;
const WL_SCALE_MAX = 8;

let wlPanX = 0;
let wlPanY = 0;
let wlScale = 1;
let wlViewportReady = false;

interface ViewSnap {
  canvas: HTMLCanvasElement;
  ctx: CanvasRenderingContext2D;
  panX: number;
  panY: number;
  scale: number;
  showGrid: boolean;
  showCoords: boolean;
}

function fitViewport(canvas: HTMLCanvasElement): Pick<ViewSnap, "panX" | "panY" | "scale"> {
  const b = computeLevelBounds();
  const w = canvas.clientWidth;
  const h = canvas.clientHeight;
  if (!b || w <= 0 || h <= 0) return { panX: 0, panY: 0, scale: 1 };
  const padding = 36;
  const scaleX = (w - padding * 2) / (b.sx * PX_PER_UNIT);
  const scaleZ = (h - padding * 2) / (b.sz * PX_PER_UNIT);
  const scale = Math.min(scaleX, scaleZ, WL_SCALE_MAX);
  return {
    panX: -b.cx * PX_PER_UNIT * scale,
    panY: b.cz * PX_PER_UNIT * scale,
    scale,
  };
}

export function initWorkloadViewport(canvas: HTMLCanvasElement): void {
  wlViewportReady = false;
  resetWorkloadViewport(canvas);
}

export function resetWorkloadViewport(canvas: HTMLCanvasElement): void {
  const vp = fitViewport(canvas);
  wlPanX = vp.panX;
  wlPanY = vp.panY;
  wlScale = vp.scale;
  wlViewportReady = true;
}

export function workloadZoomPercent(): number {
  return Math.round(wlScale * 100);
}

export function workloadZoomBy(factor: number): void {
  wlScale = Math.min(WL_SCALE_MAX, Math.max(WL_SCALE_MIN, wlScale * factor));
}

export function workloadPanBy(dx: number, dy: number): void {
  wlPanX += dx;
  wlPanY += dy;
}

function resizeCanvas(canvas: HTMLCanvasElement): void {
  const w = canvas.clientWidth;
  const h = canvas.clientHeight;
  if (w > 0 && h > 0 && (canvas.width !== w || canvas.height !== h)) {
    canvas.width = w;
    canvas.height = h;
  }
}

/** 临时把工作区画布挂到 dom.ctx / S.pan*，复用主编辑器坐标与绘制管线。 */
export function withWorkloadCanvas<T>(canvas: HTMLCanvasElement, fn: () => T): T {
  resizeCanvas(canvas);
  const ctx = canvas.getContext("2d");
  if (!ctx) throw new Error("workload canvas 2d context missing");

  const snap: ViewSnap = {
    canvas: dom.canvas,
    ctx: dom.ctx,
    panX: S.panX,
    panY: S.panY,
    scale: S.scale,
    showGrid: S.showGrid,
    showCoords: S.showCoords,
  };

  dom.canvas = canvas;
  dom.ctx = ctx;
  if (!wlViewportReady) resetWorkloadViewport(canvas);
  S.panX = wlPanX;
  S.panY = wlPanY;
  S.scale = wlScale;
  S.showGrid = true;
  S.showCoords = false;

  try {
    return fn();
  } finally {
    wlPanX = S.panX;
    wlPanY = S.panY;
    wlScale = S.scale;
    dom.canvas = snap.canvas;
    dom.ctx = snap.ctx;
    S.panX = snap.panX;
    S.panY = snap.panY;
    S.scale = snap.scale;
    S.showGrid = snap.showGrid;
    S.showCoords = snap.showCoords;
  }
}

export function workloadCellFromPointer(
  canvas: HTMLCanvasElement,
  clientX: number,
  clientY: number
): { cx: number; cz: number } {
  return withWorkloadCanvas(canvas, () => {
    const r = canvas.getBoundingClientRect();
    const px = ((clientX - r.left) / r.width) * canvas.width;
    const py = ((clientY - r.top) / r.height) * canvas.height;
    const wp = canvasToWorld(px, py);
    return {
      cx: Math.round(wp.x / CELL),
      cz: Math.round(wp.z / CELL),
    };
  });
}

function parseCellKey(k: string): [number, number] | null {
  const [cx, cz] = k.split(",").map(Number);
  if (!Number.isFinite(cx) || !Number.isFinite(cz)) return null;
  return [cx, cz];
}

function drawWorkloadOverlays(areas: string[][]): void {
  const cellPx = CELL * PX_PER_UNIT * S.scale;
  if (cellPx < 2) return;

  for (let pi = 0; pi < areas.length; pi++) {
    const color = WORKLOAD_COLORS[pi] ?? "#888888";
    for (const k of areas[pi]) {
      const parsed = parseCellKey(k);
      if (!parsed) continue;
      const [cx, cz] = parsed;
      const center = worldToCanvas(cx * CELL, cz * CELL);
      const half = cellPx / 2;
      const x0 = center.x - half;
      const y0 = center.y - half;
      dom.ctx.save();
      dom.ctx.fillStyle = `${color}${WORKLOAD_FILL_ALPHA}`;
      dom.ctx.fillRect(x0, y0, cellPx, cellPx);
      dom.ctx.strokeStyle = `${color}${WORKLOAD_STROKE_ALPHA}`;
      dom.ctx.lineWidth = Math.max(2, cellPx * 0.12);
      dom.ctx.strokeRect(x0, y0, cellPx, cellPx);
      dom.ctx.strokeStyle = "rgba(255,255,255,0.45)";
      dom.ctx.lineWidth = Math.max(1, cellPx * 0.05);
      dom.ctx.setLineDash([Math.max(3, cellPx * 0.15), Math.max(2, cellPx * 0.1)]);
      dom.ctx.strokeRect(x0 + 1.5, y0 + 1.5, cellPx - 3, cellPx - 3);
      dom.ctx.setLineDash([]);
      dom.ctx.restore();
    }
  }
}

/** 与主编辑器地板层 + 核心层一致的 2D 俯视渲染（不含装饰/背景/动画叠加）。 */
export function drawWorkloadScene(areas: string[][]): void {
  const w = dom.canvas.width;
  const h = dom.canvas.height;
  dom.ctx.fillStyle = "#1a1d23";
  dom.ctx.fillRect(0, 0, w, h);

  drawGrid();
  drawWalkable();
  drawFloorPlanes(false, "floor");
  drawFloorAdjacentSeams();
  drawSurfaceItems(false, "floor", null);

  const coreItems = S.items
    .filter((it) => itemLayerOfIt(it) === "items")
    .filter((it) => itemInHeightFilter(it))
    .sort(itemDrawCompare);
  for (const item of coreItems) {
    drawItem(item, false);
  }

  drawWorkloadOverlays(areas);
}

export function renderWorkloadCanvas(canvas: HTMLCanvasElement, areas: string[][]): void {
  withWorkloadCanvas(canvas, () => {
    drawWorkloadScene(areas);
  });
}

/** 画笔预览：高亮即将涂抹的格子（不修改数据）。 */
export function drawWorkloadHoverCell(
  canvas: HTMLCanvasElement,
  areas: string[][],
  hover: { cx: number; cz: number } | null,
  brushSize: number,
  activePlayer = 0
): void {
  withWorkloadCanvas(canvas, () => {
    drawWorkloadScene(areas);
    if (!hover) return;
    const cellPx = CELL * PX_PER_UNIT * S.scale;
    if (cellPx < 2) return;
    const startX = hover.cx - Math.floor((brushSize - 1) / 2);
    const startZ = hover.cz - Math.floor((brushSize - 1) / 2);
    dom.ctx.save();
    const hoverColor = WORKLOAD_COLORS[activePlayer] ?? "#ffffff";
    dom.ctx.strokeStyle = `${hoverColor}DD`;
    dom.ctx.lineWidth = Math.max(2, cellPx * 0.12);
    dom.ctx.setLineDash([5, 4]);
    for (let dx = 0; dx < brushSize; dx++) {
      for (let dz = 0; dz < brushSize; dz++) {
        const cx = startX + dx;
        const cz = startZ + dz;
        const center = worldToCanvas(cx * CELL, cz * CELL);
        dom.ctx.strokeRect(center.x - cellPx / 2, center.y - cellPx / 2, cellPx, cellPx);
      }
    }
    dom.ctx.setLineDash([]);
    dom.ctx.restore();
  });
}

export { workloadCellKey };
