import {
  normalizeRot,
  worldToCanvas
} from "./coords";
import {
  S,
  CELL,
  PX_PER_UNIT,
  EditorFloor
} from "./state";
import { dom } from "./dom";
import { categoryVisible } from "./catalog";
import { getFloorImage } from "./iconCaches";
import { drawLabelInBox } from "./labels";
import { drawItem } from "./renderItems";
import { drawLayerForItem } from "../stacking";
import {
  surfacePaint,
  isSurfaceItem
} from "../floorColors";
import {
  floorWalkY,
  floorInHeightFilter,
  floorLayerIndex,
  itemInHeightFilter
} from "./floorHeight";

export function floorRectPx(f: EditorFloor) {
  const center = worldToCanvas(f._wx, f._wz);
  const cellPx = CELL * PX_PER_UNIT * S.scale;
  const bw = f._wCells * cellPx;
  const bh = f._dCells * cellPx;
  const rot = normalizeRot(f.localRotationY);
  return { center, bw, bh, rot };
}

export function hexToRgba(hex: string, alpha: number): string {
  const h = hex.replace("#", "");
  const full = h.length === 3 ? h.split("").map((c) => c + c).join("") : h;
  const r = parseInt(full.slice(0, 2), 16);
  const g = parseInt(full.slice(2, 4), 16);
  const b = parseInt(full.slice(4, 6), 16);
  if ([r, g, b].some((v) => Number.isNaN(v))) return hex;
  return `rgba(${r},${g},${b},${alpha})`;
}

/** warp（透视贴合）预览的相机基向量（与 render.ts drawCameraFrustum 同一数学，
 *  Unity Quaternion.Euler(pitch, yaw, 0) ZXY 顺序，16:9）。 */
function warpCamBasis() {
  const cam = S.cameraInfo;
  if (!cam || !cam.position) return null;
  const fovRaw = Number(cam.fieldOfView);
  const fov = Number.isFinite(fovRaw) && fovRaw > 0 ? fovRaw : 45;
  const pitch = (((cam.pitch ?? 0) % 360) + 360) % 360;
  const yaw = cam.yaw ?? 0;
  const th = (pitch * Math.PI) / 180;
  const ps = (yaw * Math.PI) / 180;
  const cosT = Math.cos(th);
  const sinT = Math.sin(th);
  const cosP = Math.cos(ps);
  const sinP = Math.sin(ps);
  const vy = ((fov / 2) * Math.PI) / 180;
  const tanVy = Math.tan(vy);
  return {
    fwd: { x: sinP * cosT, y: -sinT, z: cosP * cosT },
    right: { x: cosP, y: 0, z: -sinP },
    up: { x: sinT * sinP, y: cosT, z: sinT * cosP },
    tanVh: tanVy * (16 / 9),
    tanVy,
    px: cam.position.x,
    py: cam.position.y,
    pz: cam.position.z
  };
}

/** 屏幕 NDC (sx, sy ∈ [-1,1]) 射线与高度 y 地面的交点；射线不朝下时返回 null。 */
function warpGroundPoint(
  b: NonNullable<ReturnType<typeof warpCamBasis>>,
  sx: number,
  sy: number,
  y: number
): { x: number; z: number } | null {
  const dx = b.fwd.x + b.tanVh * sx * b.right.x + b.tanVy * sy * b.up.x;
  const dy = b.fwd.y + b.tanVh * sx * b.right.y + b.tanVy * sy * b.up.y;
  const dz = b.fwd.z + b.tanVh * sx * b.right.z + b.tanVy * sy * b.up.z;
  if (dy >= -1e-6) return null;
  const t = (y - b.py) / dy;
  return { x: b.px + t * dx, z: b.pz + t * dz };
}

/** warp 地板预览：把图片按 N 个水平条带仿射映射进相机视野的地面梯形
 *  （条带内为平行四边形近似，20 条带肉眼无差）。相机信息缺失或视野越地平线时
 *  退化为普通矩形铺开预览。90°/270° 旋转在预览中暂不体现（仅 0/180）。 */
function drawWarpImagePreview(f: EditorFloor, img: HTMLImageElement, alpha: number) {
  const ctx = dom.ctx;
  const b = warpCamBasis();
  const y = floorWalkY(f);
  const corners =
    b &&
    [-1, 1, 1, -1].map((sx, i) => warpGroundPoint(b, sx, i < 2 ? 1 : -1, y));
  const quad = corners && corners.every((c) => c != null) ? (corners as { x: number; z: number }[]) : null;

  if (!quad) {
    // 无相机：按地板矩形整图铺开做近似预览。
    const { bw, bh } = floorRectPx(f);
    ctx.save();
    ctx.globalAlpha = alpha;
    ctx.drawImage(img, -bw / 2, -bh / 2, bw, bh);
    ctx.restore();
    return;
  }

  const rot = normalizeRot(f.imageRotation ?? 0);
  const N = 20;
  const imgH = img.naturalHeight || img.height;
  const prevAlpha = ctx.globalAlpha;
  ctx.globalAlpha = alpha;
  for (let j = 0; j < N; j++) {
    const v0 = -1 + (2 * j) / N;
    const v1 = -1 + (2 * (j + 1)) / N;
    const A = warpGroundPoint(b!, -1, v0, y)!;
    const B = warpGroundPoint(b!, -1, v1, y)!;
    const D = warpGroundPoint(b!, 1, v0, y)!;
    const a = worldToCanvas(A.x, A.z);
    const bb = worldToCanvas(B.x, B.z);
    const d = worldToCanvas(D.x, D.z);
    // 图片 v（底=0）对应屏幕 v（-1 底）；180° 旋转翻转条带源。
    let vs0 = (v0 + 1) / 2;
    let vs1 = (v1 + 1) / 2;
    if (rot === 180) {
      const t = vs0;
      vs0 = 1 - vs1;
      vs1 = 1 - t;
    }
    const sy = (1 - vs1) * imgH;
    const sh = Math.max(1, (vs1 - vs0) * imgH);
    ctx.save();
    ctx.translate(a.x, a.y);
    ctx.transform(d.x - a.x, d.y - a.y, bb.x - a.x, bb.y - a.y, 0, 0);
    ctx.drawImage(img, 0, sy, img.naturalWidth || img.width, sh, 0, 0, 1, 1);
    ctx.restore();
  }
  ctx.globalAlpha = prevAlpha;

  // 视野梯形描边：直观标出「画面四角」在哪。
  const pts = quad.map((c) => worldToCanvas(c.x, c.z));
  ctx.save();
  ctx.beginPath();
  ctx.moveTo(pts[0].x, pts[0].y);
  for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i].x, pts[i].y);
  ctx.closePath();
  ctx.setLineDash([6, 4]);
  ctx.strokeStyle = "rgba(94,234,212,0.85)";
  ctx.lineWidth = 1.5;
  ctx.stroke();
  ctx.restore();
}

export function drawFloorPlane(f: EditorFloor, selected: boolean, ghost: boolean) {
  const { center, bw, bh, rot } = floorRectPx(f);
  const paint = surfacePaint(f.surfaceKind, selected);
  const walkH = floorWalkY(f);
  const hTag = walkH > 0.005 ? ` · h=${walkH.toFixed(2)} L${floorLayerIndex(walkH)}` : "";
  // 空气地板：无可见地板，仅可行走区——半透明橙色虚线框 + 标注。
  if (f.airFloor) {
    dom.ctx.save();
    dom.ctx.translate(center.x, center.y);
    dom.ctx.rotate((-rot * Math.PI) / 180);
    dom.ctx.fillStyle = selected ? "rgba(249,171,0,0.30)" : "rgba(249,171,0,0.12)";
    dom.ctx.fillRect(-bw / 2, -bh / 2, bw, bh);
    dom.ctx.strokeStyle = selected ? "#f9ab00" : "rgba(249,171,0,0.7)";
    dom.ctx.lineWidth = selected ? 2.5 : ghost ? 1 : 1.5;
    dom.ctx.setLineDash([7, 4]);
    dom.ctx.strokeRect(-bw / 2, -bh / 2, bw, bh);
    dom.ctx.setLineDash([]);
    if (selected && !ghost && S.selectedFloorKeys.size === 1 && !(f._wCells === 1 && f._dCells === 1)) {
      dom.ctx.fillStyle = "#f9ab00";
      for (const hx of [-bw / 2, bw / 2]) {
        for (const hy of [-bh / 2, bh / 2]) {
          dom.ctx.fillRect(hx - 3, hy - 3, 6, 6);
        }
      }
    }
    dom.ctx.fillStyle = "rgba(249,171,0,0.98)";
    dom.ctx.textAlign = "center";
    dom.ctx.textBaseline = "middle";
    drawLabelInBox(dom.ctx, `${f._wCells}×${f._dCells}格 空气地板${hTag}`, bw - 6, bh - 6);
    dom.ctx.restore();
    return;
  }
  const isImage = f.surfaceKind === "solid" && !!f.imageTexturePath;
  const img = isImage ? getFloorImage(f.imageTexturePath!) : null;
  // Manual tint overrides the fill (only when enabled) so the recolor is visible
  // in the canvas immediately; disabled tint falls back to the kind/material color.
  const fill = f.tintEnabled && f.tintColor ? hexToRgba(f.tintColor, selected ? 0.92 : 0.78) : paint.fill;

  // warp（透视贴合）：绝对像素坐标绘制（自带 alpha），画在虚线边框/标签之下。
  const isWarpImg = !!img && f.imageMode === "warp";
  if (isWarpImg) {
    const warpAlpha = f.imageOpacity != null ? Math.max(0, Math.min(1, f.imageOpacity)) : 1;
    drawWarpImagePreview(f, img!, warpAlpha);
  }

  dom.ctx.save();
  dom.ctx.translate(center.x, center.y);
  dom.ctx.rotate((-rot * Math.PI) / 180);

  if (img && !isWarpImg) {
    // Draw the uploaded image: "tile" repeats once per cell, "stretch" fills.
    // imageRotation rotates the texture itself in 90° steps (clockwise from above).
    const cellPx = CELL * PX_PER_UNIT * S.scale;
    const imgRot = normalizeRot(f.imageRotation ?? 0);
    const imgRad = (imgRot * Math.PI) / 180;
    const swapImg = imgRot === 90 || imgRot === 270;
    const prevAlpha = dom.ctx.globalAlpha;
    dom.ctx.globalAlpha = f.imageOpacity != null ? Math.max(0, Math.min(1, f.imageOpacity)) : 1;
    if (f.imageMode === "tile" && cellPx > 2) {
      const w = Math.max(1, f._wCells);
      const d = Math.max(1, f._dCells);
      for (let i = 0; i < w; i++) {
        for (let j = 0; j < d; j++) {
          const ccx = -bw / 2 + (i + 0.5) * cellPx;
          const ccy = -bh / 2 + (j + 0.5) * cellPx;
          if (imgRot) {
            dom.ctx.save();
            dom.ctx.translate(ccx, ccy);
            dom.ctx.rotate(imgRad);
            dom.ctx.drawImage(img, -cellPx / 2, -cellPx / 2, cellPx, cellPx);
            dom.ctx.restore();
          } else {
            dom.ctx.drawImage(img, ccx - cellPx / 2, ccy - cellPx / 2, cellPx, cellPx);
          }
        }
      }
    } else if (imgRot) {
      dom.ctx.save();
      dom.ctx.rotate(imgRad);
      const rw = swapImg ? bh : bw;
      const rh = swapImg ? bw : bh;
      dom.ctx.drawImage(img, -rw / 2, -rh / 2, rw, rh);
      dom.ctx.restore();
    } else {
      dom.ctx.drawImage(img, -bw / 2, -bh / 2, bw, bh);
    }
    dom.ctx.globalAlpha = prevAlpha;
  } else if (!img) {
    dom.ctx.fillStyle = fill;
    dom.ctx.fillRect(-bw / 2, -bh / 2, bw, bh);
  }

  // 高度叠加：抬高的地板按层增亮（同层同色、层越高越亮），图片/染色地板
  // 同样以叠加层实现，不破坏原有填色。
  if (walkH > 0.005) {
    const li = floorLayerIndex(walkH);
    dom.ctx.fillStyle = `rgba(255,255,255,${Math.min(0.4, 0.08 + li * 0.09)})`;
    dom.ctx.fillRect(-bw / 2, -bh / 2, bw, bh);
  }

  // Dashed outer border to convey the floor surface.
  dom.ctx.strokeStyle = paint.stroke;
  dom.ctx.lineWidth = selected ? 2.5 : ghost ? 1 : 1.5;
  dom.ctx.setLineDash(selected ? [] : [7, 4]);
  dom.ctx.strokeRect(-bw / 2, -bh / 2, bw, bh);
  dom.ctx.setLineDash([]);

  // Faint dashed internal cell grid → "perspective" tiling (skip for tiled
  // images, whose own tiling already conveys the cells).
  const cellPx = CELL * PX_PER_UNIT * S.scale;
  if (!ghost && cellPx > 6 && !(img && f.imageMode === "tile")) {
    dom.ctx.strokeStyle = "rgba(255,255,255,0.10)";
    dom.ctx.lineWidth = 1;
    dom.ctx.setLineDash([3, 4]);
    for (let i = 1; i < f._wCells; i++) {
      const x = -bw / 2 + i * cellPx;
      dom.ctx.beginPath();
      dom.ctx.moveTo(x, -bh / 2);
      dom.ctx.lineTo(x, bh / 2);
      dom.ctx.stroke();
    }
    for (let j = 1; j < f._dCells; j++) {
      const y = -bh / 2 + j * cellPx;
      dom.ctx.beginPath();
      dom.ctx.moveTo(-bw / 2, y);
      dom.ctx.lineTo(bw / 2, y);
      dom.ctx.stroke();
    }
    dom.ctx.setLineDash([]);
  }

  // Resize handles when exactly one floor is selected (1×1 地板不可拖拽缩放，不显示柄)。
  if (selected && !ghost && S.selectedFloorKeys.size === 1 && !(f._wCells === 1 && f._dCells === 1)) {
    dom.ctx.fillStyle = "#f9ab00";
    for (const hx of [-bw / 2, bw / 2]) {
      for (const hy of [-bh / 2, bh / 2]) {
        dom.ctx.fillRect(hx - 3, hy - 3, 6, 6);
      }
    }
  }

  // Label: size in cells + kind emoji.
  dom.ctx.beginPath();
  dom.ctx.rect(-bw / 2 + 2, -bh / 2 + 2, bw - 4, bh - 4);
  dom.ctx.clip();
  dom.ctx.fillStyle = paint.label;
  dom.ctx.textAlign = "center";
  dom.ctx.textBaseline = "middle";
  const emoji = paint.emoji ? paint.emoji + " " : "";
  drawLabelInBox(dom.ctx, `${emoji}${f._wCells}×${f._dCells}格${hTag}`, bw - 6, bh - 6);
  dom.ctx.restore();
}

export function drawFloorPlanes(highlight: boolean, kind: "floor" | "background" = "floor") {
  // 多层高度重叠时按行走面高度升序绘制（高的后画 = 压在上面），使视觉上的
  // 最上层与命中顺序一致。
  const sorted = [...S.floors].sort((a, b) => floorWalkY(a) - floorWalkY(b));
  for (const f of sorted) {
    // Backdrop planes (often huge, default-white in Unity) belong to the
    // dedicated background layer; theme fill shows the void color elsewhere.
    const isBg = f.surfaceKind === "background";
    if (isBg !== (kind === "background")) continue;
    // 高度过滤：范围外的地板不绘制（也不可点选，见 hitTestFloorsAll）。
    if (kind === "floor" && !floorInHeightFilter(f)) continue;
    const selected = highlight && S.selectedFloorKeys.has(f._key);
    drawFloorPlane(f, selected, false);
  }
}

export function drawFloorAdjacentSeams() {
  if (S.floors.length < 2) return;
  const tol = CELL * 0.35;
  for (let i = 0; i < S.floors.length; i++) {
    if (!floorInHeightFilter(S.floors[i])) continue;
    for (let j = i + 1; j < S.floors.length; j++) {
      if (!floorInHeightFilter(S.floors[j])) continue;
      const a = S.floors[i];
      const b = S.floors[j];
      const aL = a._wx - (a._wCells * CELL) / 2;
      const aR = a._wx + (a._wCells * CELL) / 2;
      const aT = a._wz + (a._dCells * CELL) / 2;
      const aB = a._wz - (a._dCells * CELL) / 2;
      const bL = b._wx - (b._wCells * CELL) / 2;
      const bR = b._wx + (b._wCells * CELL) / 2;
      const bT = b._wz + (b._dCells * CELL) / 2;
      const bB = b._wz - (b._dCells * CELL) / 2;

      // Vertical shared edge (left/right touch, z ranges overlap).
      const zLo = Math.max(aB, bB);
      const zHi = Math.min(aT, bT);
      if (zHi - zLo > tol) {
        if (Math.abs(aR - bL) <= tol) drawSeam(aR, zLo, aR, zHi);
        else if (Math.abs(aL - bR) <= tol) drawSeam(aL, zLo, aL, zHi);
      }
      // Horizontal shared edge (top/bottom touch, x ranges overlap).
      const xLo = Math.max(aL, bL);
      const xHi = Math.min(aR, bR);
      if (xHi - xLo > tol) {
        if (Math.abs(aT - bB) <= tol) drawSeam(xLo, aT, xHi, aT);
        else if (Math.abs(aB - bT) <= tol) drawSeam(xLo, aB, xHi, aB);
      }
    }
  }
  dom.ctx.setLineDash([]);
}

export function drawSeam(wx1: number, wz1: number, wx2: number, wz2: number) {
  const p1 = worldToCanvas(wx1, wz1);
  const p2 = worldToCanvas(wx2, wz2);
  dom.ctx.strokeStyle = "rgba(255,235,170,0.5)";
  dom.ctx.lineWidth = 1.5;
  dom.ctx.setLineDash([5, 4]);
  dom.ctx.beginPath();
  dom.ctx.moveTo(p1.x, p1.y);
  dom.ctx.lineTo(p2.x, p2.y);
  dom.ctx.stroke();
}

export function floorLocalPoint(f: EditorFloor, wx: number, wz: number): { lx: number; lz: number } {
  const dx = wx - f._wx;
  const dz = wz - f._wz;
  const rad = (normalizeRot(f.localRotationY) * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  return { lx: dx * cos + dz * sin, lz: -dx * sin + dz * cos };
}

export interface FloorHit {
  floor: EditorFloor;
  mode: "move" | "resize";
  edge: string;
  anchorX: number;
  anchorZ: number;
}

export function hitTestFloorsAll(wx: number, wz: number): FloorHit[] {  const hits: FloorHit[] = [];
  for (let i = S.floors.length - 1; i >= 0; i--) {
    const f = S.floors[i];
    if (f.surfaceKind === "background") {
      if (!categoryVisible("background")) continue;
      // Backgrounds are only directly editable on the dedicated background layer
      // (or when the user explicitly unlocks them on the floor layer).
      if (!S.backgroundEditable && S.currentLayer !== "background") continue;
    } else if (!categoryVisible("floors")) {
      continue;
    }
    // 高度过滤：范围外的地板不参与点选/框选/拖动（「全部」时不过滤，
    // 多层重叠的地板仍按既有候选列表逻辑逐块选择）。
    if (!floorInHeightFilter(f)) continue;
    const hw = (f._wCells * CELL) / 2;
    const hh = (f._dCells * CELL) / 2;
    const { lx, lz } = floorLocalPoint(f, wx, wz);
    if (Math.abs(lx) > hw || Math.abs(lz) > hh) continue;

    // 1×1 地板：角点容差（0.9）大于半格（0.6）会覆盖整个矩形，导致一拖就进入
    // 缩放（无法移动、容易误缩放）。1×1 地板恒为移动模式，缩放改由地板弹窗改尺寸。
    const oneCell = f._wCells === 1 && f._dCells === 1;
    if (!oneCell) {
      // Corner handle hit → resize.
      const handleTol = Math.max(CELL * 0.5, 0.9);
      const nearLeft = lx < -hw + handleTol;
      const nearRight = lx > hw - handleTol;
      const nearBottom = lz < -hh + handleTol;
      const nearTop = lz > hh - handleTol;
      if ((nearLeft || nearRight) && (nearBottom || nearTop)) {
        const edge = `${nearRight ? "R" : "L"}${nearTop ? "T" : "B"}`;
        // Anchor = opposite corner in world space.
        const ax = nearRight ? -hw : hw;
        const az = nearTop ? -hh : hh;
        const rad = (normalizeRot(f.localRotationY) * Math.PI) / 180;
        const cos = Math.cos(rad);
        const sin = Math.sin(rad);
        const anchorX = f._wx + ax * cos - az * sin;
        const anchorZ = f._wz + ax * sin + az * cos;
        hits.push({ floor: f, mode: "resize", edge, anchorX, anchorZ });
        continue;
      }
    }
    hits.push({ floor: f, mode: "move", edge: "", anchorX: 0, anchorZ: 0 });
  }
  return hits;
}

export function drawSurfaceItems(
  highlight: boolean,
  tier: "floor" | "background" = "floor",
  previewPos: Map<string, { x: number; z: number; y?: number }> | null = null
) {
  const sorted = [...S.items]
    .filter((it) => isSurfaceItem(S.catalogByGuid.get(it.prefabGuid)))
    // 高度过滤：范围外的表面物品不绘制；已选中的豁免（避免拖动中消失）。
    .filter((it) => itemInHeightFilter(it) || it._editorKey === S.selectedKey)
    .filter((it) => (S.catalogByGuid.get(it.prefabGuid)?.surfaceTier ?? "floor") === tier)
    .sort((a, b) => drawLayerForItem(a, S.catalogByGuid) - drawLayerForItem(b, S.catalogByGuid));
  for (const item of sorted) {
    const selected = highlight && item._editorKey === S.selectedKey;
    // Move preview: draw the surface item at its simulated position (dim ghost
    // at the original spot), so counters/platforms visibly move with the route.
    const pp = previewPos?.get(item.instanceId);
    if (!pp) {
      drawItem(item, selected);
      continue;
    }
    dom.ctx.save();
    dom.ctx.globalAlpha = 0.22;
    drawItem(item, false);
    dom.ctx.restore();
    dom.ctx.save();
    dom.ctx.globalAlpha = 1;
    drawItem({ ...item, _wx: pp.x, _wz: pp.z }, selected);
    dom.ctx.restore();
  }
}

export function floorUnionBBox(): { x0: number; z0: number; x1: number; z1: number } | null {
  let x0 = Infinity;
  let z0 = Infinity;
  let x1 = -Infinity;
  let z1 = -Infinity;
  for (const f of S.floors) {
    if (f.surfaceKind === "background") continue;
    const hw = (f._wCells * CELL) / 2;
    const hh = (f._dCells * CELL) / 2;
    x0 = Math.min(x0, f._wx - hw);
    x1 = Math.max(x1, f._wx + hw);
    z0 = Math.min(z0, f._wz - hh);
    z1 = Math.max(z1, f._wz + hh);
  }
  if (!isFinite(x0)) return null;
  return { x0, z0, x1, z1 };
}

/** Sub-rectangles of W that lie outside F (W \ F), as world XZ rects. */
export function rectMinus(
  wx0: number,
  wz0: number,
  wx1: number,
  wz1: number,
  f: { x0: number; z0: number; x1: number; z1: number }
): Array<{ x0: number; z0: number; x1: number; z1: number }> {
  const ox0 = Math.max(wx0, f.x0);
  const ox1 = Math.min(wx1, f.x1);
  const oz0 = Math.max(wz0, f.z0);
  const oz1 = Math.min(wz1, f.z1);
  if (ox0 >= ox1 || oz0 >= oz1) return [{ x0: wx0, z0: wz0, x1: wx1, z1: wz1 }];
  const out: Array<{ x0: number; z0: number; x1: number; z1: number }> = [];
  if (wx0 < ox0) out.push({ x0: wx0, z0: wz0, x1: ox0, z1: wz1 });
  if (ox1 < wx1) out.push({ x0: ox1, z0: wz0, x1: wx1, z1: wz1 });
  if (wz0 < oz0) out.push({ x0: ox0, z0: wz0, x1: ox1, z1: oz0 });
  if (oz1 < wz1) out.push({ x0: ox0, z0: oz0, x1: ox1, z1: wz1 });
  return out;
}
export function drawWalkable() {
  const floorBox = floorUnionBBox();
  for (const r of S.walkable) {
    const a = worldToCanvas(r.cx - r.sx / 2, r.cz + r.sz / 2);
    const b = worldToCanvas(r.cx + r.sx / 2, r.cz - r.sz / 2);
    const x = Math.min(a.x, b.x);
    const y = Math.min(a.y, b.y);
    const bw = Math.abs(b.x - a.x);
    const bh = Math.abs(b.y - a.y);
    const ice = r.surfaceType === "ice";
    dom.ctx.fillStyle = ice ? "rgba(120,200,235,0.16)" : "rgba(150,140,120,0.14)";
    dom.ctx.fillRect(x, y, bw, bh);
    dom.ctx.strokeStyle = ice ? "rgba(90,170,210,0.4)" : "rgba(160,150,130,0.3)";
    dom.ctx.setLineDash([4, 3]);
    dom.ctx.lineWidth = 1;
    dom.ctx.strokeRect(x, y, bw, bh);
    dom.ctx.setLineDash([]);

    if (floorBox) {
      const air = rectMinus(
        r.cx - r.sx / 2,
        r.cz - r.sz / 2,
        r.cx + r.sx / 2,
        r.cz + r.sz / 2,
        floorBox
      );
      let labeled = false;
      for (const seg of air) {
        const sa = worldToCanvas(seg.x0, seg.z1);
        const sb = worldToCanvas(seg.x1, seg.z0);
        const sx = Math.min(sa.x, sb.x);
        const sy = Math.min(sa.y, sb.y);
        const sw = Math.abs(sb.x - sa.x);
        const sh = Math.abs(sb.y - sa.y);
        dom.ctx.save();
        dom.ctx.fillStyle = "rgba(249,171,0,0.16)";
        dom.ctx.fillRect(sx, sy, sw, sh);
        dom.ctx.strokeStyle = "rgba(249,171,0,0.7)";
        dom.ctx.setLineDash([6, 4]);
        dom.ctx.lineWidth = 1.5;
        dom.ctx.strokeRect(sx, sy, sw, sh);
        dom.ctx.setLineDash([]);
        if (!labeled && sw > 24 && sh > 14) {
          dom.ctx.fillStyle = "rgba(249,171,0,0.98)";
          dom.ctx.font = "bold 11px sans-serif";
          dom.ctx.textAlign = "left";
          dom.ctx.textBaseline = "top";
          dom.ctx.fillText("空气地板（可行走但无可见地板）", sx + 4, sy + 4);
          labeled = true;
        }
        dom.ctx.restore();
      }
    }
  }
}

export function drawKillPlanes() {
  const planes = S.deathInfo?.killPlanes ?? [];
  for (const kp of planes) {
    if ((kp.sx ?? 0) <= 0.001 || (kp.sz ?? 0) <= 0.001) continue;
    const a = worldToCanvas(kp.cx - kp.sx / 2, kp.cz + kp.sz / 2);
    const b = worldToCanvas(kp.cx + kp.sx / 2, kp.cz - kp.sz / 2);
    const x = Math.min(a.x, b.x);
    const y = Math.min(a.y, b.y);
    const w = Math.abs(b.x - a.x);
    const h = Math.abs(b.y - a.y);
    dom.ctx.save();
    dom.ctx.fillStyle = "rgba(242,139,130,0.10)";
    dom.ctx.fillRect(x, y, w, h);
    dom.ctx.strokeStyle = "rgba(242,139,130,0.95)";
    dom.ctx.lineWidth = 2;
    dom.ctx.setLineDash([8, 5]);
    dom.ctx.strokeRect(x, y, w, h);
    dom.ctx.setLineDash([]);
    dom.ctx.fillStyle = "rgba(242,139,130,0.98)";
    dom.ctx.font = "bold 12px sans-serif";
    dom.ctx.textAlign = "left";
    dom.ctx.textBaseline = "top";
    dom.ctx.fillText(`坠落区 · ${kp.respawnType || "死亡"}`, x + 5, y + 5);
    dom.ctx.restore();
  }
}

export function drawVoidHatch(color: string) {
  const w = dom.canvas.clientWidth;
  const h = dom.canvas.clientHeight;
  dom.ctx.save();
  dom.ctx.strokeStyle = color;
  dom.ctx.lineWidth = 1;
  const step = 26;
  dom.ctx.beginPath();
  for (let x = -h; x < w; x += step) {
    dom.ctx.moveTo(x, 0);
    dom.ctx.lineTo(x + h, h);
  }
  dom.ctx.stroke();
  dom.ctx.restore();
}
