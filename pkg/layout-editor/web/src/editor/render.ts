import {
  worldToCanvas,
  canvasToWorld,
  resolveFootprint,
  itemScaleX,
  itemScaleZ,
  normalizeRot,
  COORD_ORIGIN_OFFSET,
  pasteGridOrigin,
} from "./coords";
export { COORD_ORIGIN_OFFSET } from "./coords";
import {
  S,
  CELL,
  PX_PER_UNIT,
  isFloorLikeLayer,
  EditorItem
} from "./state";
import { dom } from "./dom";
import { getCanvasVoidBg } from "../theme";
import {
  isActiveItemLayer,
  itemCategoryOf,
  floorCategoryOf,
  categoryVisible,
  hideBackgroundContentOnFloorLayer,
  catalogItemForGuidOrPath
} from "./catalog";
import { isFloorHiddenBySceneGroup, isItemHiddenBySceneGroup } from "./layerGroupVisibility";
import { isCollisionItem, isAirWallItem } from "./stubControls";
import { itemIntersectsWorldRect } from "./items";
import {
  isSelected,
  selectionKeys,
  setFloorSelection
} from "./selection";
import {
  drawItem,
  itemDrawCompare,
  drawWorkstationOverlapAlertOutlines,
  isPlayerItem
} from "./renderItems";
import {
  drawFloorPlanes,
  drawFloorAdjacentSeams,
  drawWalkable,
  drawSurfaceItems,
  drawKillPlanes,
  drawVoidHatch
} from "./renderFloors";
import { drawAnimControlOverlay, previewMemberPositions, previewFxState } from "./animControl";
import { floorInHeightFilter, itemInHeightFilter } from "./floorHeight";
import { drawTeleportalLinks, drawSwitchLinks, drawHoldGroupLinks, drawTerminalLinks, drawMarkerLightLinks, drawButtonPartnerLinks, drawCoaxialLinks, drawBurnerFocusedMarkers } from "./renderItems";
import { drawSceneLights } from "./renderLights";
import { drawServingLinks } from "./servingLinks";
import {
  isSurfaceItem,
  canvasVoidTheme,
  bgThemeKeyForDeathType
} from "../floorColors";
import {
  computeTeleportalLabels,
  computeParamLabels
} from "./renderItems";
import { scene3dInvalidate } from "./scene3dBridge";
import { itemInCurrentPart, itemInReferencePart, floorInCurrentPart } from "./partLevel";

let refreshHooks: () => void = () => {};
export function setRefreshHooks(fn: () => void): void {
  refreshHooks = fn;
}

export function coordOrigin(): { x: number; z: number } {
  if (S.gridInfo?.found) {
    return pasteGridOrigin();
  }
  const b = computeLevelBounds();
  const base = b ? { x: Math.round(b.cx / CELL) * CELL, z: Math.round(b.cz / CELL) * CELL } : { x: 0, z: 0 };
  return {
    x: base.x + COORD_ORIGIN_OFFSET.x * CELL,
    z: base.z + COORD_ORIGIN_OFFSET.z * CELL,
  };
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
  if (S.currentLayer === "anim" && S.animMode === "members") {
    const next = S.marqueeAdd ? new Set(S.selectedKeys) : new Set<string>();
    for (const it of S.items) {
      // 分 P：跨 P 框选会绑出跨阶段成员（转场/组根冲突），严格限制在当前 P。
      if (!itemInCurrentPart(it)) continue;
      if (isCollisionItem(it) && !isAirWallItem(it)) continue;
      const cat = itemCategoryOf(it);
      if (cat === "background") continue;
      if (!categoryVisible(cat)) continue;
      if (marqueeHitsItem(it)) next.add(it._editorKey);
    }
    S.selectedKeys = next;
    const nextFloors = S.marqueeAdd ? new Set(S.selectedFloorKeys) : new Set<string>();
    for (const f of S.floors) {
      if (!floorInCurrentPart(f)) continue;
      if (f.surfaceKind === "background") continue;
      // 空气地板（仅碰撞盒）允许框选入组：其碰撞盒对象随动画组动画一起走。
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
      if (!floorInCurrentPart(f)) continue;
      const isBg = f.surfaceKind === "background";
      // 地板层/背景层严格分离：地板层框选无条件排除背景（水面等），
      // 背景层框选只收背景。跨层选中已由 setLayer 的 clearSelection 清空。
      if (layerBg ? !isBg : isBg) continue;
      if (!categoryVisible(isBg ? "background" : "floors")) continue;
      if (!floorInHeightFilter(f)) continue;
      if (isFloorHiddenBySceneGroup(f)) continue;
      if (inRect(f._wx, f._wz)) nextFloors.add(f._key);
    }
    setFloorSelection([...nextFloors]);
    const next = S.marqueeAdd ? new Set(S.selectedKeys) : new Set<string>();
    for (const it of S.items) {
      if (!itemInCurrentPart(it)) continue;
      const cat = itemCategoryOf(it);
      if (cat !== (layerBg ? "background" : "floors")) continue;
      if (isCollisionItem(it) && !isAirWallItem(it)) continue;
      if (!categoryVisible(cat)) continue;
      if (!itemInHeightFilter(it)) continue;
      if (isItemHiddenBySceneGroup(it)) continue;
      if (inRect(it._wx, it._wz)) next.add(it._editorKey);
    }
    S.selectedKeys = next;
    const keys = selectionKeys();
    S.selectedKey = keys.length ? keys[keys.length - 1] : null;
    return;
  }
  const next = S.marqueeAdd ? new Set(S.selectedKeys) : new Set<string>();
  for (const it of S.items) {
    if (!itemInCurrentPart(it)) continue;
    if (!isActiveItemLayer(it)) continue;
    if (isCollisionItem(it) && !isAirWallItem(it)) continue;
    if (!categoryVisible(itemCategoryOf(it))) continue;
    if (isItemHiddenBySceneGroup(it)) continue;
    if (!itemInHeightFilter(it)) continue;
    if (marqueeHitsItem(it)) next.add(it._editorKey);
  }
  S.selectedKeys = next;
  const keys = selectionKeys();
  S.selectedKey = keys.length ? keys[keys.length - 1] : null;
}

type PreviewPosMap = Map<string, { x: number; z: number; y?: number; rotY?: number; tiltX?: number; tiltZ?: number; scale?: number; alpha?: number }> | null;

/** Draw an item at its preview pose when the anim preview is playing:
 *  a dim ghost stays at the original spot while the item itself moves/rotates
 *  (drawn at full opacity so it stays visible even inside the dimmed inactive pass). */
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
  // fade（淡入淡出）：ghost 全局透明度乘 alpha（0=隐身不可见）。
  dom.ctx.globalAlpha = Math.max(0, Math.min(1, pp.alpha ?? 1));
  // scale 事件：绕预览位姿缩放图标（缩没消失；tilt 俯视无 2D 表现，3D 预览可见）。
  const psc = pp.scale ?? 1;
  if (psc !== 1) {
    const c = worldToCanvas(pp.x, pp.z);
    dom.ctx.translate(c.x, c.y);
    dom.ctx.scale(Math.max(0.001, psc), Math.max(0.001, psc));
    dom.ctx.translate(-c.x, -c.y);
  }
  drawItem(
    {
      ...item,
      _wx: pp.x,
      _wz: pp.z,
      localRotationY: pp.rotY != null ? item.localRotationY + pp.rotY : item.localRotationY,
    },
    selected
  );
  dom.ctx.restore();
}

/** 分 P 参考叠显（§3.4）：把参考层（base 或其他 P）的物件以极低透明度画在
 *  当前 P 之下，供作者对齐布局；纯视图状态——不可点选、不写入文档。 */
function drawReferenceOverlay(previewPos: PreviewPosMap): void {
  if (!S.partLevel?.enabled || S.partReferences.size === 0) return;
  const refs = S.items.filter(
    (it) =>
      itemInReferencePart(it) &&
      categoryVisible(itemCategoryOf(it)) &&
      !isItemHiddenBySceneGroup(it) &&
      itemInHeightFilter(it)
  );
  if (refs.length === 0) return;
  dom.ctx.save();
  dom.ctx.globalAlpha = 0.16;
  for (const it of refs) drawItemPreview(it, false, previewPos);
  dom.ctx.restore();
}

/** Floor members of the previewed group: translucent quad at the simulated spot
 *  (the original plane stays drawn dimmed by the normal floor pass). */
function drawPreviewFloorGhosts(pos: PreviewPosMap): void {  if (!pos) return;
  const cellPx = CELL * PX_PER_UNIT * S.scale;
  for (const f of S.floors) {
    const pp = pos.get(f.instanceId);
    if (!pp) continue;
    const c = worldToCanvas(pp.x, pp.z);
    const rad = (-normalizeRot(f.localRotationY + (pp.rotY ?? 0)) * Math.PI) / 180;
    const psc = Math.max(0.001, pp.scale ?? 1);
    const w = f._wCells * cellPx * psc;
    const h = f._dCells * cellPx * psc;
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
  // 3D 模式：跳过全部 2D 绘制，改为标脏 3D 场景图（内部增量 diff + rAF 合并）。
  // 约 200 处 draw() 调用点因此自动变成「通知 3D」，无需逐处改造。
  // refreshHooks 仍要跑：右侧物品清单与地板状态条与视口无关。
  if (S.viewMode === "3d") {
    scene3dInvalidate();
    refreshHooks();
    return;
  }

  const w = dom.canvas.clientWidth;
  const h = dom.canvas.clientHeight;
  if (dom.canvas.width !== w || dom.canvas.height !== h) {
    dom.canvas.width = w;
    dom.canvas.height = h;
  }

  // Move-preview playback: per-frame simulated positions of the previewed
  // group's members, so the actual components visibly move along the route.
  const previewPos = previewMemberPositions();
  // 全屏特效预览：抖动 = 整个世界层平移（背景留驻，模拟相机抖出画框）；
  // 闪光 = 最后叠一层全屏 overlay。
  const fx = previewFxState();

  const onFloor = isFloorLikeLayer(S.currentLayer);
  const layerBg = S.currentLayer === "background";
  const vis = S.layerVisibility[S.currentLayer];
  const theme = canvasVoidTheme(S.deathInfo?.deathType);
  // 坠落死亡（fall）时画布底色可采用相机背景色（所见即所得）。
  const camVoidBg =
    onFloor &&
    bgThemeKeyForDeathType(S.deathInfo?.deathType) === "void" &&
    isHexColor(S.cameraInfo?.backgroundColor)
      ? S.cameraInfo!.backgroundColor
      : null;
  dom.ctx.fillStyle = camVoidBg ?? (onFloor ? theme.fill : getCanvasVoidBg());
  dom.ctx.fillRect(0, 0, w, h);

  // 相机抖动：世界层整体偏移（网格 / 地板 / 物品 / 相机视野全部随之）。
  const shaking = fx && (Math.abs(fx.shakeX) >= 0.05 || Math.abs(fx.shakeY) >= 0.05);
  if (shaking) {
    dom.ctx.save();
    dom.ctx.translate(fx.shakeX, fx.shakeY);
  }

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
    } else if (
      layerBg &&
      vis.background &&
      S.floors.some((f) => f.surfaceKind === "background")
    ) {
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
      drawKillPlanes();
    }
    // Selected floors stay clearly visible on both kinds.
    if (S.selectedFloorKeys.size > 0) {
      drawFloorPlanes(true, "floor");
      if (layerBg) drawFloorPlanes(true, "background");
    }
    // Selected surface items (flooredge, background planes…) — same multi-select pass as floors.
    if (S.selectedKeys.size > 0) {
      drawSurfaceItems(true, "floor", previewPos);
      if (layerBg) drawSurfaceItems(true, "background", previewPos);
    }
    // Ghost items (non-surface) from other layers, dimmed by category visibility.
    // On the background layer the ambient effects (落雪 BGM…) are active content
    // and must stay fully visible with selection highlights.
    const ghostItems = S.items.filter(
      (it) =>
        // 分 P：仅绘制当前 P 的物件（参考层走下方专门的低透明叠显通道）。
        itemInCurrentPart(it) &&
        !isSurfaceItem(catalogItemForGuidOrPath(it.prefabGuid, it.prefabAssetPath)) &&
        !hideBackgroundContentOnFloorLayer(it) &&
        categoryVisible(itemCategoryOf(it)) &&
        !isItemHiddenBySceneGroup(it) &&
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
    drawReferenceOverlay(previewPos);
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
        // 分 P：动画层/非地板层同样只看当前 P；参考层低透明叠显。
        itemInCurrentPart(it) &&
        !isActiveItemLayer(it) &&
        categoryVisible(itemCategoryOf(it)) &&
        !isItemHiddenBySceneGroup(it) &&
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
        dom.ctx.globalAlpha = S.animMode === "members" ? 0.6 : 0.32;
        for (const it of rest) drawItemPreview(it, false, previewPos);
        dom.ctx.restore();
      }
    }
    const sorted = S.items
      .filter(isActiveItemLayer)
      // 分 P：非地板层（核心/装饰/动画）主绘制通道同样只看当前 P。
      .filter(itemInCurrentPart)
      .filter(
        (it) =>
          (categoryVisible(itemCategoryOf(it)) && !isItemHiddenBySceneGroup(it)) ||
          isSelected(it._editorKey)
      )
      // 高度过滤：范围外的物品不绘制；已选中的豁免（避免拖动中消失）。
      .filter((it) => itemInHeightFilter(it) || isSelected(it._editorKey))
      .sort(itemDrawCompare);
    S.teleportalLabels = computeTeleportalLabels();
    computeParamLabels();
    for (const item of sorted) {
      drawItemPreview(item, isSelected(item._editorKey), previewPos);
    }
    drawReferenceOverlay(previewPos);
    drawTeleportalLinks();
    drawSwitchLinks();
    drawHoldGroupLinks();
    drawButtonPartnerLinks();
    drawCoaxialLinks();
    drawTerminalLinks();
    drawMarkerLightLinks();
    drawServingLinks();
    // 燃烧弹射器：唯一选中时显示全部波次落点（核心层专属叠加）。
    drawBurnerFocusedMarkers();
  }

  if ((!isFloorLikeLayer(S.currentLayer) || S.activeAnimGroupId) &&
      (S.activeRightTab === "anim" || S.activeAnimGroupId)) {
    drawPreviewFloorGhosts(previewPos);
    drawAnimControlOverlay();
  }

  drawSceneLights();
  if (S.showCameraFov || S.cameraInfo?.splitScreen) drawCameraFrustum();
  drawCameraIntroMarkers();

  if (S.showCoords) drawCoordAxes();

  // 结束相机抖动平移（marquee 是 UI 框选反馈，不参与抖动；闪光 overlay 全屏固定）。
  if (shaking) dom.ctx.restore();

  // 闪电 overlay：全屏闪电色闪光（强度 → 透明度，峰值 4 → ~0.7 封顶）。
  if (fx && fx.flash > 0.001) {
    dom.ctx.save();
    dom.ctx.globalAlpha = Math.min(0.85, fx.flash / 6);
    dom.ctx.fillStyle = fx.flashColor;
    dom.ctx.fillRect(0, 0, w, h);
    dom.ctx.restore();
  }

  drawWorkstationOverlapAlertOutlines();

  if (S.marqueeing) drawMarquee();

  refreshHooks();
}

function isHexColor(hex: string | undefined): hex is string {
  return !!hex && /^#[0-9a-fA-F]{6}$/.test(hex);
}

/** 游戏相机视野范围估算：FOV 视锥四条边线与地面 y=0 的交点连成四边形。
 *  非分屏：单主镜头参考；分屏：按场景玩家位置绘制最多 4 个镜头参考，主镜头参考隐藏。 */
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
  if (fwd.y >= -1e-4) return; // 相机没朝下看，视野与地面无交线

  if (cam.splitScreen) {
    const players = S.items.filter(isPlayerItem).slice(0, 4);
    if (players.length === 0) return; // 分屏模式下不再画主镜头参考

    const cfgs = cam.splitScreenCameras ?? [];
    const palette = [
      { fill: "rgba(231,76,60,0.06)", stroke: "rgba(231,76,60,0.85)" },
      { fill: "rgba(52,152,219,0.06)", stroke: "rgba(52,152,219,0.85)" },
      { fill: "rgba(46,204,113,0.06)", stroke: "rgba(46,204,113,0.85)" },
      { fill: "rgba(241,196,15,0.06)", stroke: "rgba(241,196,15,0.85)" },
    ];
    // 分屏单目标默认跟随距离 = 主相机 m_minDistance（与运行时 MultiplayerCamera
    // 单目标一致：相机位 = 厨师位 - minDistance*forward）。
    const md = Number(cam.minDistance);
    const baseDist = Number.isFinite(md) && md > 0 ? md : 5;
    for (let i = 0; i < players.length; i++) {
      const p = players[i];
      const c = cfgs[i];
      if (c?.visible === false) continue;
      const raw = Number(c?.size ?? 1);
      const size = Number.isFinite(raw) ? Math.min(4, Math.max(0.25, raw)) : 1;
      const off = c?.offset ?? { x: 0, y: 0, z: 0 };
      const dist = baseDist * size;
      const origin = {
        x: p._wx - fwd.x * dist + off.x,
        y: -fwd.y * dist + off.y,
        z: p._wz - fwd.z * dist + off.z,
      };
      const refFov = c?.fieldOfView && c.fieldOfView > 0 ? c.fieldOfView : fov;
      const viewAspect = splitViewAspect(players.length, i);
      const hPct = Math.round((viewAspect / (16 / 9)) * 100);
      drawFrustumReference(origin, fwd, right, up, refFov, viewAspect, palette[i % palette.length],
        `🎥 镜头 ${i + 1} · 水平 ${hPct}% · ${size.toFixed(2)}×`, `镜头 ${i + 1}`);
    }
    return;
  }

  drawFrustumReference(cam.position, fwd, right, up, fov, 16 / 9,
    { fill: "rgba(94,234,212,0.07)", stroke: "rgba(94,234,212,0.8)" },
    `🎥 相机 FOV ${fov}° · 16:9（静态估算，运行时随玩家推拉）`);
}

/** 分屏 viewport 的宽高比（屏幕按 16:9 估算）：2P 左右半宽；3P 上 60% 宽/半高、下两半宽半高；4P 2×2。 */
function splitViewAspect(count: number, index: number): number {
  const screen = 16 / 9;
  if (count === 2) return screen * 0.5;
  if (count === 3) return index === 0 ? screen * (0.6 / 0.5) : screen;
  return screen; // 1P 全屏 / 4P 2×2（半宽半高）
}

/** 进场动画目标标记：focus = 聚焦点标记（X/Z 均生效，画布实际坐标）；tour =
 *  A/B X 标记 + A→B→最终相机位的水平巡回虚线路径（tour 仅 X 平移，Z 恒为主相机
 *  Z，绘制同款取 Z 保证所见即所得）；orbit = 绕最终位的圆弧虚线；dive/dollyIn =
 *  沿视轴的远端直线。分屏模式下不画。 */
export function drawCameraIntroMarkers(): void {
  const cam = S.cameraInfo;
  const intro = cam?.intro;
  if (!cam || cam.splitScreen || !intro) return;
  const ctx = dom.ctx;
  const stroke = "rgba(236,72,153,0.9)";
  ctx.save();
  ctx.strokeStyle = stroke;
  ctx.fillStyle = stroke;
  ctx.lineWidth = 1.5;

  const drawPoint = (x: number, z: number, label: string): void => {
    const c = worldToCanvas(x, z);
    ctx.setLineDash([4, 3]);
    ctx.beginPath();
    ctx.arc(c.x, c.y, 9, 0, Math.PI * 2);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.beginPath();
    ctx.moveTo(c.x - 14, c.y);
    ctx.lineTo(c.x + 14, c.y);
    ctx.moveTo(c.x, c.y - 14);
    ctx.lineTo(c.x, c.y + 14);
    ctx.stroke();
    ctx.font = "bold 11px sans-serif";
    ctx.textAlign = "left";
    ctx.textBaseline = "bottom";
    ctx.fillText(label, c.x + 16, c.y - 6);
  };

  /** 视轴水平投影单位向量（世界 XZ；pitch/yaw 快照推导）。 */
  const axisDir = (): { x: number; z: number } => {
    const yaw = ((cam.yaw ?? 0) * Math.PI) / 180;
    const pitch = ((cam.pitch ?? 60) * Math.PI) / 180;
    const cp = Math.cos(pitch);
    return { x: Math.sin(yaw) * cp, z: Math.cos(yaw) * cp };
  };

  if (intro.mode === "focus" && intro.focusTarget) {
    drawPoint(intro.focusTarget.x, intro.focusTarget.z, "🎬 进场聚焦");
  } else if (intro.mode === "tour" && intro.tourA && intro.tourB) {
    const camZ = Number.isFinite(cam.position?.z) ? (cam.position as { z: number }).z : intro.tourA.z;
    const a = worldToCanvas(intro.tourA.x, camZ);
    const b = worldToCanvas(intro.tourB.x, camZ);
    const f = cam.position ? worldToCanvas(cam.position.x, cam.position.z) : null;
    // 巡回路径：A → B → 最终相机位（归位）。Z 恒为主相机 Z → 水平线。
    ctx.setLineDash([7, 5]);
    ctx.globalAlpha = 0.75;
    ctx.beginPath();
    ctx.moveTo(a.x, a.y);
    ctx.lineTo(b.x, b.y);
    if (f) ctx.lineTo(f.x, f.y);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.globalAlpha = 1;
    drawPoint(intro.tourA.x, camZ, "🎬 巡回 A · X");
    drawPoint(intro.tourB.x, camZ, "🎬 巡回 B · X");
    if (f) {
      ctx.beginPath();
      ctx.arc(f.x, f.y, 4, 0, Math.PI * 2);
      ctx.fill();
      ctx.font = "11px sans-serif";
      ctx.textAlign = "left";
      ctx.textBaseline = "bottom";
      ctx.fillText("归位", f.x + 8, f.y - 4);
    }
  } else if (intro.mode === "orbit") {
    // 环绕路径：offset(θ) = R·(sinθ, cosθ−1)，θ 从 ±orbitAngle 归零（与烘焙一致）。
    const cxw = cam.position?.x ?? 0;
    const czw = cam.position?.z ?? 0;
    const r = intro.startDistance && intro.startDistance >= 2 ? intro.startDistance : 8;
    const th0 = ((intro.orbitDirection === "left" ? -1 : 1) * (intro.orbitAngle ?? 180) * Math.PI) / 180;
    ctx.setLineDash([7, 5]);
    ctx.globalAlpha = 0.75;
    ctx.beginPath();
    for (let i = 0; i <= 40; i++) {
      const th = th0 * (1 - i / 40);
      const c = worldToCanvas(cxw + Math.sin(th) * r, czw + (Math.cos(th) - 1) * r);
      if (i === 0) ctx.moveTo(c.x, c.y);
      else ctx.lineTo(c.x, c.y);
    }
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.globalAlpha = 1;
    const s = worldToCanvas(cxw + Math.sin(th0) * r, czw + (Math.cos(th0) - 1) * r);
    ctx.beginPath();
    ctx.arc(s.x, s.y, 4, 0, Math.PI * 2);
    ctx.fill();
    ctx.font = "bold 11px sans-serif";
    ctx.textAlign = "left";
    ctx.textBaseline = "bottom";
    ctx.fillText(`🎬 ${intro.orbitDirection === "left" ? "左" : "右"}环绕 ${Math.round(intro.orbitAngle ?? 180)}°`, s.x + 8, s.y - 6);
  } else if (intro.mode === "dive" || intro.mode === "dollyIn") {
    // 远端直线：起始 = 最终位 ∓ 视轴·d（dive 另有高度，画布只示水平投影）。
    const d = intro.startDistance && intro.startDistance > 0 ? intro.startDistance : 5;
    const ax = axisDir();
    const sign = intro.mode === "dive" ? -1 : -1;
    const sx = (cam.position?.x ?? 0) + ax.x * d * sign;
    const sz = (cam.position?.z ?? 0) + ax.z * d * sign;
    const a = worldToCanvas(sx, sz);
    const f = cam.position ? worldToCanvas(cam.position.x, cam.position.z) : null;
    ctx.setLineDash([7, 5]);
    ctx.globalAlpha = 0.75;
    ctx.beginPath();
    ctx.moveTo(a.x, a.y);
    if (f) ctx.lineTo(f.x, f.y);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.globalAlpha = 1;
    drawPoint(sx, sz, intro.mode === "dive" ? "🎬 俯冲起点" : "🎬 推近起点");
  }

  ctx.restore();
}

function drawFrustumReference(
  P: { x: number; y: number; z: number },
  fwd: { x: number; y: number; z: number },
  right: { x: number; y: number; z: number },
  up: { x: number; y: number; z: number },
  fov: number,
  aspect: number,
  palette: { fill: string; stroke: string },
  label: string,
  targetLabel?: string
): void {
  const vy = ((fov / 2) * Math.PI) / 180;
  const tanVy = Math.tan(vy);
  const tanVh = tanVy * aspect;

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
  ctx.fillStyle = palette.fill;
  ctx.fill();

  ctx.setLineDash([6, 4]);
  ctx.strokeStyle = palette.stroke;
  ctx.lineWidth = 1.5;
  ctx.stroke();

  // 视线中心线：相机位置 → 注视点。
  const camC = worldToCanvas(P.x, P.z);
  const tgtC = worldToCanvas(target.x, target.z);
  ctx.strokeStyle = palette.stroke;
  ctx.globalAlpha = 0.55;
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(camC.x, camC.y);
  ctx.lineTo(tgtC.x, tgtC.y);
  ctx.stroke();
  ctx.setLineDash([]);

  // 注视点十字。
  ctx.beginPath();
  ctx.moveTo(tgtC.x - 6, tgtC.y);
  ctx.lineTo(tgtC.x + 6, tgtC.y);
  ctx.moveTo(tgtC.x, tgtC.y - 6);
  ctx.lineTo(tgtC.x, tgtC.y + 6);
  ctx.stroke();
  ctx.globalAlpha = 1;

  if (targetLabel) {
    ctx.font = "bold 11px sans-serif";
    ctx.textAlign = "left";
    ctx.textBaseline = "bottom";
    ctx.fillStyle = palette.stroke;
    ctx.fillText(targetLabel, tgtC.x + 8, tgtC.y - 8);
  }

  // 相机位置标记。
  ctx.fillStyle = palette.stroke;
  ctx.beginPath();
  ctx.arc(camC.x, camC.y, 4, 0, Math.PI * 2);
  ctx.fill();
  ctx.font = "11px sans-serif";
  ctx.textAlign = "left";
  ctx.textBaseline = "bottom";
  ctx.fillText(label, camC.x + 8, camC.y - 4);

  ctx.restore();
}
