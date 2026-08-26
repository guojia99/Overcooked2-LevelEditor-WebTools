import {
  worldToCanvas,
  canvasToWorld,
  resolveFootprint,
  itemScaleX,
  itemScaleZ,
  normalizeRot
} from "./coords";
import {
  S,
  CELL,
  PX_PER_UNIT,
  isFloorLikeLayer,
  EditorItem
} from "./state";
import { dom } from "./dom";
import {
  isActiveItemLayer,
  itemCategoryOf,
  floorCategoryOf,
  categoryVisible
} from "./catalog";
import { isCollisionItem, isAirWallItem } from "./stubControls";
import { itemIntersectsWorldRect } from "./items";
import {
  isSelected,
  selectionKeys,
  setFloorSelection
} from "./selection";
import {
  drawItem,
  itemDrawCompare
} from "./renderItems";
import {
  drawFloorPlanes,
  drawFloorAdjacentSeams,
  drawWalkable,
  drawSurfaceItems,
  drawKillPlanes,
  drawVoidHatch
} from "./renderFloors";
import { drawMoveControlOverlay, previewMemberPositions } from "./moveControl";
import { floorInHeightFilter, itemInHeightFilter } from "./floorHeight";
import { drawTeleportalLinks, drawSwitchLinks, drawTerminalLinks } from "./renderItems";
import { drawServingLinks } from "./servingLinks";
import {
  isSurfaceItem,
  bgTheme
} from "../floorColors";
import {
  computeTeleportalLabels,
  computeParamLabels
} from "./renderItems";

let refreshHooks: () => void = () => {};
export function setRefreshHooks(fn: () => void): void {
  refreshHooks = fn;
}

export const COORD_ORIGIN_OFFSET = { x: 3.5, z: -1.5 };

export function coordOrigin(): { x: number; z: number } {
  let base: { x: number; z: number };
  if (S.gridInfo?.found) base = { x: S.gridInfo.worldPosition.x, z: S.gridInfo.worldPosition.z };
  else {
    const b = computeLevelBounds();
    base = b ? { x: Math.round(b.cx / CELL) * CELL, z: Math.round(b.cz / CELL) * CELL } : { x: 0, z: 0 };
  }
  return { x: base.x + COORD_ORIGIN_OFFSET.x * CELL, z: base.z + COORD_ORIGIN_OFFSET.z * CELL };
}

export function drawGrid() {
  const w = dom.canvas.width;
  const h = dom.canvas.height;
  const step = CELL * PX_PER_UNIT * S.scale;
  if (step < 4) return;

  let ox = w / 2 + S.panX;
  let oy = h / 2 + S.panY;
  let halfX = 20;
  let halfZ = 20;

  if (S.gridInfo?.found) {
    const c = worldToCanvas(S.gridInfo.worldPosition.x, S.gridInfo.worldPosition.z);
    ox = c.x;
    oy = c.y;
    const cs = S.gridInfo.cellSize?.x ?? CELL;
    halfX = S.gridInfo.gridHalfSizeX || 10;
    halfZ = S.gridInfo.gridHalfSizeZ || 10;
    const gw = cs * PX_PER_UNIT * S.scale;
    dom.ctx.strokeStyle = "rgba(61, 107, 243, 0.35)";
    dom.ctx.lineWidth = 1;
    dom.ctx.strokeRect(ox - halfX * gw, oy - halfZ * gw, halfX * 2 * gw, halfZ * 2 * gw);
  }

  dom.ctx.strokeStyle = "rgba(255,255,255,0.06)";
  dom.ctx.lineWidth = 1;
  // Shift the lattice half a cell left/up so the grid lines match Unity's
  // actual physics grid (Unity cell corners land on the old line centers).
  const startX = ((ox - step / 2) % step) - step;
  for (let x = startX; x < w; x += step) {
    dom.ctx.beginPath();
    dom.ctx.moveTo(x, 0);
    dom.ctx.lineTo(x, h);
    dom.ctx.stroke();
  }
  const startY = ((oy - step / 2) % step) - step;
  for (let y = startY; y < h; y += step) {
    dom.ctx.beginPath();
    dom.ctx.moveTo(0, y);
    dom.ctx.lineTo(w, y);
    dom.ctx.stroke();
  }
}

export function drawCoordAxes() {
  const w = dom.canvas.width;
  const h = dom.canvas.height;
  const o = coordOrigin();
  const oc = worldToCanvas(o.x, o.z);
  const cellPx = CELL * PX_PER_UNIT * S.scale;
  if (cellPx < 3) return;
  const labelEvery = cellPx >= 16 ? 1 : cellPx >= 8 ? 2 : 5;

  const xMin = Math.ceil((0 - oc.x) / cellPx);
  const xMax = Math.floor((w - oc.x) / cellPx);
  const zMin = Math.ceil((oc.y - h) / cellPx);
  const zMax = Math.floor(oc.y / cellPx);

  const X_COL = "rgba(239,111,111,";
  const Z_COL = "rgba(123,216,137,";

  dom.ctx.save();
  dom.ctx.lineWidth = 1.5;
  dom.ctx.strokeStyle = `${X_COL}0.85)`;
  dom.ctx.beginPath();
  dom.ctx.moveTo(0, oc.y);
  dom.ctx.lineTo(w, oc.y);
  dom.ctx.stroke();
  dom.ctx.strokeStyle = `${Z_COL}0.85)`;
  dom.ctx.beginPath();
  dom.ctx.moveTo(oc.x, 0);
  dom.ctx.lineTo(oc.x, h);
  dom.ctx.stroke();

  dom.ctx.lineWidth = 1;
  dom.ctx.font = "10px sans-serif";
  dom.ctx.textAlign = "center";
  dom.ctx.textBaseline = "top";
  for (let cx = xMin; cx <= xMax; cx++) {
    const px = oc.x + cx * cellPx;
    dom.ctx.strokeStyle = `${X_COL}0.5)`;
    dom.ctx.beginPath();
    dom.ctx.moveTo(px, oc.y - 4);
    dom.ctx.lineTo(px, oc.y + 4);
    dom.ctx.stroke();
    if (cx !== 0 && cx % labelEvery === 0) {
      dom.ctx.fillStyle = `${X_COL}0.95)`;
      dom.ctx.fillText(String(cx), px, oc.y + 6);
    }
  }
  dom.ctx.textAlign = "left";
  dom.ctx.textBaseline = "middle";
  for (let cz = zMin; cz <= zMax; cz++) {
    const py = oc.y - cz * cellPx;
    dom.ctx.strokeStyle = `${Z_COL}0.5)`;
    dom.ctx.beginPath();
    dom.ctx.moveTo(oc.x - 4, py);
    dom.ctx.lineTo(oc.x + 4, py);
    dom.ctx.stroke();
    if (cz !== 0 && cz % labelEvery === 0) {
      dom.ctx.fillStyle = `${Z_COL}0.95)`;
      dom.ctx.fillText(String(cz), oc.x + 6, py);
    }
  }

  dom.ctx.fillStyle = "rgba(255,255,255,0.95)";
  dom.ctx.beginPath();
  dom.ctx.arc(oc.x, oc.y, 3, 0, Math.PI * 2);
  dom.ctx.fill();
  dom.ctx.font = "10px sans-serif";
  dom.ctx.textAlign = "right";
  dom.ctx.textBaseline = "top";
  dom.ctx.fillText("0", oc.x - 4, oc.y + 4);

  dom.ctx.font = "bold 11px sans-serif";
  dom.ctx.fillStyle = `${X_COL}0.95)`;
  dom.ctx.textAlign = "right";
  dom.ctx.textBaseline = "bottom";
  dom.ctx.fillText("x", w - 6, oc.y - 4);
  dom.ctx.fillStyle = `${Z_COL}0.95)`;
  dom.ctx.textAlign = "left";
  dom.ctx.textBaseline = "top";
  dom.ctx.fillText("z", oc.x + 6, 4);

  if (S.hoverCx >= 0 && S.hoverCy >= 0) {
    const wp = canvasToWorld(S.hoverCx, S.hoverCy);
    const cx = Math.round((wp.x - o.x) / CELL);
    const cz = Math.round((wp.z - o.z) / CELL);
    const txt = `(${cx}, ${cz})`;
    dom.ctx.font = "11px sans-serif";
    const tw = dom.ctx.measureText(txt).width;
    const bx = w - tw - 18;
    const by = h - 26;
    dom.ctx.fillStyle = "rgba(0,0,0,0.55)";
    dom.ctx.fillRect(bx - 6, by - 4, tw + 12, 20);
    dom.ctx.fillStyle = "#fff";
    dom.ctx.textAlign = "left";
    dom.ctx.textBaseline = "middle";
    dom.ctx.fillText(txt, bx, by + 6);
  }
  dom.ctx.restore();
}

export function computeLevelBounds(): { cx: number; cz: number; sx: number; sz: number } | null {
  let minX = Infinity;
  let maxX = -Infinity;
  let minZ = Infinity;
  let maxZ = -Infinity;
  const consider = (x: number, z: number, hx = 0, hz = 0) => {
    minX = Math.min(minX, x - hx);
    maxX = Math.max(maxX, x + hx);
    minZ = Math.min(minZ, z - hz);
    maxZ = Math.max(maxZ, z + hz);
  };
  for (const f of S.floors) {
    if (f.surfaceKind === "background") continue;
    consider(f._wx, f._wz, (f._wCells * CELL) / 2, (f._dCells * CELL) / 2);
  }
  for (const it of S.items) {
    const cat = S.catalogByGuid.get(it.prefabGuid);
    if (cat?.surfaceTier === "background") continue;
    if (cat?.surfaceTier !== "floor" && cat?.layoutTier !== "core") continue;
    const fp = resolveFootprint(it);
    consider(it._wx, it._wz, (fp.cellsX * CELL) / 2 * itemScaleX(it), (fp.cellsZ * CELL) / 2 * itemScaleZ(it));
  }
  for (const r of S.walkable) consider(r.cx, r.cz, r.sx / 2, r.sz / 2);
  if (!isFinite(minX)) return null;
  const margin = CELL;
  minX -= margin;
  minZ -= margin;
  maxX += margin;
  maxZ += margin;
  return { cx: (minX + maxX) / 2, cz: (minZ + maxZ) / 2, sx: maxX - minX, sz: maxZ - minZ };
}
export function drawMarquee() {
  const x = Math.min(S.marqueeStartX, S.marqueeCurX);
  const y = Math.min(S.marqueeStartY, S.marqueeCurY);
  const w = Math.abs(S.marqueeCurX - S.marqueeStartX);
  const h = Math.abs(S.marqueeCurY - S.marqueeStartY);
  dom.ctx.save();
  dom.ctx.fillStyle = "rgba(61,107,243,0.12)";
  dom.ctx.fillRect(x, y, w, h);
  dom.ctx.strokeStyle = "rgba(61,107,243,0.9)";
  dom.ctx.lineWidth = 1;
  dom.ctx.setLineDash([4, 3]);
  dom.ctx.strokeRect(x, y, w, h);
  dom.ctx.setLineDash([]);
  dom.ctx.restore();
}

export function updateMarqueeSelection() {
  const minX = Math.min(S.marqueeStartX, S.marqueeCurX);
  const maxX = Math.max(S.marqueeStartX, S.marqueeCurX);
  const minY = Math.min(S.marqueeStartY, S.marqueeCurY);
  const maxY = Math.max(S.marqueeStartY, S.marqueeCurY);
  const inRect = (wx: number, wz: number) => {
    const p = worldToCanvas(wx, wz);
    return p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY;
  };
  const c1 = canvasToWorld(minX, minY);
  const c2 = canvasToWorld(maxX, maxY);
  const minWx = Math.min(c1.x, c2.x);
  const maxWx = Math.max(c1.x, c2.x);
  const minWz = Math.min(c1.z, c2.z);
  const maxWz = Math.max(c1.z, c2.z);
  const marqueeHitsItem = (it: EditorItem) => {
    if (isAirWallItem(it)) return itemIntersectsWorldRect(it, minWx, maxWx, minWz, maxWz);
    return inRect(it._wx, it._wz);
  };
  // Move layer in members mode: marquee selects items AND floors across ALL layers.
  // Pure background content (water, sky…) is never selectable as a member.
  if (S.currentLayer === "move" && S.moveMode === "members") {
    const next = S.marqueeAdd ? new Set(S.selectedKeys) : new Set<string>();
    for (const it of S.items) {
      if (isCollisionItem(it) && !isAirWallItem(it)) continue;
      const cat = itemCategoryOf(it);
      if (cat === "background") continue;
      if (!categoryVisible(cat)) continue;
      if (marqueeHitsItem(it)) next.add(it._editorKey);
    }
    S.selectedKeys = next;
    const nextFloors = S.marqueeAdd ? new Set(S.selectedFloorKeys) : new Set<string>();
    for (const f of S.floors) {
      if (f.surfaceKind === "background") continue;
      // 空气地板（仅碰撞盒）允许框选入组：其碰撞盒对象随移动组动画一起走。
      if (!categoryVisible(floorCategoryOf(f))) continue;
      if (inRect(f._wx, f._wz)) nextFloors.add(f._key);
    }
    setFloorSelection([...nextFloors]);
    const keys = selectionKeys();
    S.selectedKey = keys.length ? keys[keys.length - 1] : null;
    return;
  }
  if (isFloorLikeLayer(S.currentLayer)) {
    const layerBg = S.currentLayer === "background";
    const nextFloors = S.marqueeAdd ? new Set(S.selectedFloorKeys) : new Set<string>();
    for (const f of S.floors) {
      const isBg = f.surfaceKind === "background";
      if (layerBg ? !isBg : isBg && !S.backgroundEditable) continue;
      if (!categoryVisible(isBg ? "background" : "floors")) continue;
      if (!floorInHeightFilter(f)) continue;
      if (inRect(f._wx, f._wz)) nextFloors.add(f._key);
    }
    setFloorSelection([...nextFloors]);
    const next = S.marqueeAdd ? new Set(S.selectedKeys) : new Set<string>();
    for (const it of S.items) {
      const cat = itemCategoryOf(it);
      if (cat !== (layerBg ? "background" : "floors")) continue;
      if (isCollisionItem(it) && !isAirWallItem(it)) continue;
      if (!categoryVisible(cat)) continue;
      if (!itemInHeightFilter(it)) continue;
      if (inRect(it._wx, it._wz)) next.add(it._editorKey);
    }
    S.selectedKeys = next;
    const keys = selectionKeys();
    S.selectedKey = keys.length ? keys[keys.length - 1] : null;
    return;
  }
  const next = S.marqueeAdd ? new Set(S.selectedKeys) : new Set<string>();
  for (const it of S.items) {
    if (!isActiveItemLayer(it)) continue;
    if (isCollisionItem(it) && !isAirWallItem(it)) continue;
    if (!categoryVisible(itemCategoryOf(it))) continue;
    if (!itemInHeightFilter(it)) continue;
    if (marqueeHitsItem(it)) next.add(it._editorKey);
  }
  S.selectedKeys = next;
  const keys = selectionKeys();
  S.selectedKey = keys.length ? keys[keys.length - 1] : null;
}

type PreviewPosMap = Map<string, { x: number; z: number; y?: number }> | null;

/** Draw an item at its preview position when the move preview is playing:
 *  a dim ghost stays at the original spot while the item itself moves (drawn at
 *  full opacity so it stays visible even inside the dimmed inactive pass). */
function drawItemPreview(item: EditorItem, selected: boolean, pos: PreviewPosMap): void {
  const pp = pos?.get(item.instanceId);
  if (!pp) {
    drawItem(item, selected);
    return;
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

/** Floor members of the previewed group: translucent quad at the simulated spot
 *  (the original plane stays drawn dimmed by the normal floor pass). */
function drawPreviewFloorGhosts(pos: PreviewPosMap): void {
  if (!pos) return;
  const cellPx = CELL * PX_PER_UNIT * S.scale;
  for (const f of S.floors) {
    const pp = pos.get(f.instanceId);
    if (!pp) continue;
    const c = worldToCanvas(pp.x, pp.z);
    const rad = (-normalizeRot(f.localRotationY) * Math.PI) / 180;
    const w = f._wCells * cellPx;
    const h = f._dCells * cellPx;
    dom.ctx.save();
    dom.ctx.translate(c.x, c.y);
    dom.ctx.rotate(rad);
    dom.ctx.globalAlpha = 0.5;
    dom.ctx.fillStyle = "rgba(61,107,243,0.45)";
    dom.ctx.fillRect(-w / 2, -h / 2, w, h);
    dom.ctx.strokeStyle = "rgba(255,255,255,0.65)";
    dom.ctx.lineWidth = 1.5;
    dom.ctx.strokeRect(-w / 2, -h / 2, w, h);
    dom.ctx.restore();
  }
}

export function draw() {
  const w = dom.canvas.clientWidth;
  const h = dom.canvas.clientHeight;
  if (dom.canvas.width !== w || dom.canvas.height !== h) {
    dom.canvas.width = w;
    dom.canvas.height = h;
  }

  // Move-preview playback: per-frame simulated positions of the previewed
  // group's members, so the actual components visibly move along the route.
  const previewPos = previewMemberPositions();

  const onFloor = isFloorLikeLayer(S.currentLayer);
  const layerBg = S.currentLayer === "background";
  const vis = S.layerVisibility[S.currentLayer];
  const theme = bgTheme(S.bgThemeKey);
  // 空洞主题没有背景物体，画布底色直接采用相机背景色（所见即所得）。
  const camVoidBg =
    onFloor && S.bgThemeKey === "void" && isHexColor(S.cameraInfo?.backgroundColor)
      ? S.cameraInfo!.backgroundColor
      : null;
  dom.ctx.fillStyle = camVoidBg ?? (onFloor ? theme.fill : "#1a1d23");
  dom.ctx.fillRect(0, 0, w, h);

  if (onFloor) drawVoidHatch(theme.hatch);

  if (S.showGrid) drawGrid();

  if (onFloor) {
    // The "other" floor kind stays visible (dimmed) so the split is never confusing.
    if (layerBg) {
      if (vis.floors && S.floors.some((f) => f.surfaceKind !== "background")) {
        dom.ctx.save();
        dom.ctx.globalAlpha = 0.4;
        drawFloorPlanes(false, "floor");
        dom.ctx.restore();
      }
    } else if (vis.background && S.floors.some((f) => f.surfaceKind === "background")) {
      dom.ctx.save();
      dom.ctx.globalAlpha = 0.35;
      drawFloorPlanes(false, "background");
      dom.ctx.restore();
    }
    if (layerBg) {
      if (vis.background) {
        drawFloorPlanes(false, "background");
        drawSurfaceItems(false, "background", previewPos);
      }
      if (vis.floors) {
        dom.ctx.save();
        dom.ctx.globalAlpha = 0.4;
        drawSurfaceItems(false, "floor", previewPos);
        dom.ctx.restore();
      }
    } else {
      drawWalkable();
      if (vis.floors) {
        drawFloorPlanes(false, "floor");
        drawFloorAdjacentSeams();
        drawFloorPlanes(true, "floor");
        drawSurfaceItems(false, "floor", previewPos);
      }
      if (vis.background) {
        dom.ctx.save();
        dom.ctx.globalAlpha = 0.4;
        drawSurfaceItems(false, "background", previewPos);
        dom.ctx.restore();
      }
      drawKillPlanes();
    }
    // Selected floors stay clearly visible on both kinds.
    if (S.selectedFloorKeys.size > 0) {
      drawFloorPlanes(true, "floor");
      drawFloorPlanes(true, "background");
    }
    // Selected surface items (flooredge, background planes…) — same multi-select pass as floors.
    if (S.selectedKeys.size > 0) {
      drawSurfaceItems(true, "floor", previewPos);
      drawSurfaceItems(true, "background", previewPos);
    }
    // Ghost items (non-surface) from other layers, dimmed by category visibility.
    // On the background layer the ambient effects (落雪 BGM…) are active content
    // and must stay fully visible with selection highlights.
    const ghostItems = S.items.filter(
      (it) =>
        !isSurfaceItem(S.catalogByGuid.get(it.prefabGuid)) &&
        categoryVisible(itemCategoryOf(it)) &&
        (itemInHeightFilter(it) || isSelected(it._editorKey))
    );
    if (ghostItems.length > 0) {
      S.teleportalLabels = computeTeleportalLabels();
      computeParamLabels();
      const activeGhost = ghostItems.filter((it) => isActiveItemLayer(it));
      const restGhost = ghostItems.filter((it) => !isActiveItemLayer(it));
      if (activeGhost.length > 0) {
        for (const it of activeGhost) drawItemPreview(it, isSelected(it._editorKey), previewPos);
      }
      if (restGhost.length > 0) {
        dom.ctx.save();
        dom.ctx.globalAlpha = 0.32;
        for (const it of restGhost) drawItemPreview(it, false, previewPos);
        dom.ctx.restore();
      }
    }
  } else {
    if (vis.floors && S.floors.some((f) => f.surfaceKind !== "background")) {
      dom.ctx.save();
      dom.ctx.globalAlpha = 0.5;
      drawFloorPlanes(false, "floor");
      dom.ctx.restore();
    }
    if (vis.background && S.floors.some((f) => f.surfaceKind === "background")) {
      dom.ctx.save();
      dom.ctx.globalAlpha = 0.35;
      drawFloorPlanes(false, "background");
      dom.ctx.restore();
    }
    // Selected floors (members pick / marquee) stay clearly visible.
    if (S.selectedFloorKeys.size > 0) {
      drawFloorPlanes(true, "floor");
      drawFloorPlanes(true, "background");
    }
    const inactive = S.items.filter(
      (it) =>
        !isActiveItemLayer(it) &&
        categoryVisible(itemCategoryOf(it)) &&
        (itemInHeightFilter(it) || isSelected(it._editorKey))
    );
    if (inactive.length > 0) {
      // On the move layer, selected items (pick mode / group highlight) must stay
      // clearly visible — draw them at full opacity with the selected outline.
      // In members mode every other item is brightened so on-map picking works well.
      const selInactive = inactive.filter((it) => isSelected(it._editorKey));
      const rest = inactive.filter((it) => !isSelected(it._editorKey));
      if (selInactive.length > 0) {
        for (const it of selInactive) drawItemPreview(it, true, previewPos);
      }
      if (rest.length > 0) {
        dom.ctx.save();
        dom.ctx.globalAlpha = S.moveMode === "members" ? 0.6 : 0.32;
        for (const it of rest) drawItemPreview(it, false, previewPos);
        dom.ctx.restore();
      }
    }
    const sorted = S.items
      .filter(isActiveItemLayer)
      .filter((it) => categoryVisible(itemCategoryOf(it)) || isSelected(it._editorKey))
      // 高度过滤：范围外的物品不绘制；已选中的豁免（避免拖动中消失）。
      .filter((it) => itemInHeightFilter(it) || isSelected(it._editorKey))
      .sort(itemDrawCompare);
    S.teleportalLabels = computeTeleportalLabels();
    computeParamLabels();
    for (const item of sorted) {
      drawItemPreview(item, isSelected(item._editorKey), previewPos);
    }
    drawTeleportalLinks();
    drawSwitchLinks();
    drawTerminalLinks();
    drawServingLinks();
  }

  if ((!isFloorLikeLayer(S.currentLayer) || S.activeMoveGroupId) &&
      (S.activeRightTab === "move" || S.activeMoveGroupId)) {
    drawPreviewFloorGhosts(previewPos);
    drawMoveControlOverlay();
  }

  if (S.showCameraFov) drawCameraFrustum();

  if (S.showCoords) drawCoordAxes();

  if (S.marqueeing) drawMarquee();

  refreshHooks();
}

function isHexColor(hex: string | undefined): hex is string {
  return !!hex && /^#[0-9a-fA-F]{6}$/.test(hex);
}

/** 游戏相机视野范围估算：FOV 视锥四条边线与地面 y=0 的交点连成四边形（16:9）。
 *  相机 transform 来自导出快照；运行时镜头会跟随玩家推拉，此处为静态近似。 */
export function drawCameraFrustum(): void {
  const cam = S.cameraInfo;
  if (!cam || !cam.position) return;

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

  // Unity Quaternion.Euler(pitch, yaw, 0) 作用于 +Z 的朝向（ZXY 顺序，roll=0）。
  const fwd = { x: sinP * cosT, y: -sinT, z: cosP * cosT };
  const right = { x: cosP, y: 0, z: -sinP };
  const up = { x: sinT * sinP, y: cosT, z: sinT * cosP };

  const vy = ((fov / 2) * Math.PI) / 180;
  const tanVy = Math.tan(vy);
  const tanVh = tanVy * (16 / 9);

  const P = cam.position;
  if (fwd.y >= -1e-4) return; // 相机没朝下看，视野与地面无交线

  const corners: { x: number; z: number }[] = [];
  // 周长顺序（左上 → 左下 → 右下 → 右上）避免自交四边形。
  for (const [sx, sy] of [[-1, -1], [-1, 1], [1, 1], [1, -1]] as const) {
    const dx = fwd.x + tanVh * sx * right.x + tanVy * sy * up.x;
    const dy = fwd.y + tanVh * sx * right.y + tanVy * sy * up.y;
    const dz = fwd.z + tanVh * sx * right.z + tanVy * sy * up.z;
    if (dy >= -1e-6) return; // 某条视锥边线越过地平线，四边形不闭合
    const t = -P.y / dy;
    corners.push({ x: P.x + t * dx, z: P.z + t * dz });
  }

  const tc = -P.y / fwd.y;
  const target = { x: P.x + tc * fwd.x, z: P.z + tc * fwd.z };

  const ctx = dom.ctx;
  ctx.save();

  const pts = corners.map((c) => worldToCanvas(c.x, c.z));
  ctx.beginPath();
  ctx.moveTo(pts[0].x, pts[0].y);
  for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i].x, pts[i].y);
  ctx.closePath();
  ctx.fillStyle = "rgba(94,234,212,0.07)";
  ctx.fill();

  ctx.setLineDash([6, 4]);
  ctx.strokeStyle = "rgba(94,234,212,0.8)";
  ctx.lineWidth = 1.5;
  ctx.stroke();

  // 视线中心线：相机位置 → 注视点。
  const camC = worldToCanvas(P.x, P.z);
  const tgtC = worldToCanvas(target.x, target.z);
  ctx.strokeStyle = "rgba(94,234,212,0.45)";
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(camC.x, camC.y);
  ctx.lineTo(tgtC.x, tgtC.y);
  ctx.stroke();
  ctx.setLineDash([]);

  // 注视点十字。
  ctx.strokeStyle = "rgba(94,234,212,0.7)";
  ctx.beginPath();
  ctx.moveTo(tgtC.x - 6, tgtC.y);
  ctx.lineTo(tgtC.x + 6, tgtC.y);
  ctx.moveTo(tgtC.x, tgtC.y - 6);
  ctx.lineTo(tgtC.x, tgtC.y + 6);
  ctx.stroke();

  // 相机位置标记。
  ctx.fillStyle = "rgba(94,234,212,0.95)";
  ctx.beginPath();
  ctx.arc(camC.x, camC.y, 4, 0, Math.PI * 2);
  ctx.fill();
  ctx.font = "11px sans-serif";
  ctx.textAlign = "left";
  ctx.textBaseline = "bottom";
  ctx.fillText(`🎥 相机 FOV ${fov}° · 16:9（静态估算，运行时随玩家推拉）`, camC.x + 8, camC.y - 4);

  ctx.restore();
}
