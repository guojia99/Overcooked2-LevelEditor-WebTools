import {

  normalizeRot,
  worldToCanvas,
  resolveFootprint,
  itemScaleX,
  itemScaleZ,
  prefabIdFromPath,
  itemVisualCenterXZ
} from "./coords";
import {
  S,
  CELL,
  PX_PER_UNIT,
  EditorItem
} from "./state";
import { dom } from "./dom";
import {
  isActiveItemLayer,
  itemCategoryOf,
  categoryVisible,
  catalogItemForGuidOrPath,
  isResizableBackgroundItem,
  itemPlaneCells,
  counterTypeOfItem
} from "./catalog";
import { getCounterTopImage } from "./iconCaches";
import { itemInHeightFilter } from "./floorHeight";
import {
  drawLabelInBox,
  drawTopViewBadgeLabel,
  itemLabel,
  drawDispenserIngredient,
  drawCatalogItemIcon,
  drawCannonSwitchStarIcon
} from "./labels";
import {
  isCollisionItem,
  stubKindOf,
  isHotpotBurnerItem,
  isAirWallItem,
  isAirSlopeItem,
  isTravelatorItem
} from "./stubControls";
import { isSelected } from "./selection";
import {
  drawLayerForItem,
  isStackUtensilCatalog
} from "../stacking";
import {
  isSurfaceItem,
  surfacePaint
} from "../floorColors";
import { paintStyleForItem } from "../itemColors";
import type { CounterAppearanceCatalog } from "../types";
import { isServingStationItem,
  isPlateReturnItem,
  isGlassReturnItem
} from "./servingLinks";
import { airWallCells, airWallHeightCells, airSlopeEndY } from "./items";

/** 皮肤主色查表（guid→color），按 counterAppearances 引用做 WeakMap 缓存避免逐物品全表扫。 */
const counterSkinColorCache = new WeakMap<CounterAppearanceCatalog, Map<string, string>>();
function counterSkinColorMap(): Map<string, string> {
  const ca = S.counterAppearances;
  if (!ca) return new Map();
  let m = counterSkinColorCache.get(ca);
  if (!m) {
    m = new Map();
    for (const opts of Object.values(ca.byType)) {
      for (const o of opts) if (o.color) m.set(o.guid, o.color);
    }
    counterSkinColorCache.set(ca, m);
  }
  return m;
}

/** 「🎨 桌台皮肤」开关开启且物品设了皮肤外观时返回主色，否则 null（沿用原配色）。 */
function counterSkinFillOf(item: EditorItem): string | null {
  if (!S.counterSkinPaint || !item.pseudoPrefabGuid) return null;
  return counterSkinColorMap().get(item.pseudoPrefabGuid) ?? null;
}

/** 俯视渲染图（Unity 导出的模型顶拍）：开关开 → 皮肤 fileKey（无皮肤用 <Type>_Default）。
 *  manifest 无该条目 / 图片加载失败 / 桥离线 → null（回退主色填充）。 */
function counterTopImageOf(item: EditorItem): HTMLImageElement | null {
  if (!S.counterSkinPaint || !S.counter3d || !S.counterAppearances) return null;
  const ct = counterTypeOfItem(item);
  if (!ct) return null;
  let fileKey: string | undefined;
  const guid = item.pseudoPrefabGuid ?? "";
  if (guid) {
    for (const o of S.counterAppearances.byType[ct] ?? []) {
      if (o.guid === guid) { fileKey = o.icon; break; }
    }
  }
  if (!fileKey) fileKey = `${ct}_Default`;
  if (!S.counter3d.items[fileKey]) return null;
  return getCounterTopImage(fileKey);
}

import {
  isTeleportalItem,
  teleportals,
  teleportalRole,
  computeTeleportalLabels
} from "./teleportalLinks";

// 传送门方向模型与配对改写统一在 ./teleportalLinks；此处保留再导出，旧的
// `from "./renderItems"` 引用（render / stubControls / detailPanel）无需改动。
export { isTeleportalItem, teleportals, teleportalRole, computeTeleportalLabels };
export type { TeleportalRole } from "./teleportalLinks";

/** 空气箱（隐形碰撞块）：编辑器内以虚线框 + 半透明填充标示，游戏内不可见。 */
function drawCollisionMarker(item: EditorItem, selected: boolean) {
  const rot = normalizeRot(item.localRotationY);
  const center = worldToCanvas(item._wx, item._wz);
  const cellPx = CELL * PX_PER_UNIT * S.scale;
  const cells = isAirWallItem(item) ? airWallCells(item) : null;
  const hCells = isAirWallItem(item) ? airWallHeightCells(item) : 1;
  const w = cells ? cells.wCells * cellPx : resolveFootprint(item).cellsX * cellPx * itemScaleX(item);
  const h = cells ? cells.dCells * cellPx : resolveFootprint(item).cellsZ * cellPx * itemScaleZ(item);
  const ctx = dom.ctx;
  const rotated = rot === 90 || rot === 270;
  const [dw, dh] = rotated ? [h, w] : [w, h];

  ctx.save();
  ctx.translate(center.x, center.y);
  ctx.rotate((rot * Math.PI) / 180);
  ctx.fillStyle = selected ? "rgba(249,171,0,0.35)" : "rgba(120,160,255,0.12)";
  ctx.fillRect(-dw / 2, -dh / 2, dw, dh);
  ctx.strokeStyle = selected ? "#f9ab00" : "rgba(120,160,255,0.55)";
  ctx.lineWidth = selected ? 2.5 : 1.2;
  ctx.setLineDash(selected ? [] : [4, 3]);
  ctx.strokeRect(-dw / 2, -dh / 2, dw, dh);
  ctx.setLineDash([]);
  ctx.fillStyle = selected ? "rgba(255,220,120,0.95)" : "rgba(150,190,255,0.75)";
  drawLabelInBox(ctx, hCells > 1 ? `空气墙·${hCells}格` : "空气墙", dw, dh);
  if (showItemResizeHandles(item, selected)) drawItemResizeHandles(dw, dh);
  ctx.restore();
}

/** 空气斜坡（可行走斜坡）：渐变梯形 + 起止高度标注（如 1.0→2.0m），游戏内不可见。 */
function drawAirSlopeMarker(item: EditorItem, selected: boolean) {
  const rot = normalizeRot(item.localRotationY);
  const center = worldToCanvas(item._wx, item._wz);
  const cellPx = CELL * PX_PER_UNIT * S.scale;
  // 旋转帧内用【局部系】尺寸：宽 = widthCells（横向），长 = lengthCells（沿坡向）；
  // ctx.rotate 已承担朝向（与 drawCollisionMarker 同款约定）。
  const s = item.slope;
  const w = (s?.widthCells ?? 1) * cellPx;
  const h = (s?.lengthCells ?? 3) * cellPx;
  const startY = item.localPosition?.y ?? 0;
  const endY = airSlopeEndY(item);
  const ctx = dom.ctx;

  ctx.save();
  ctx.translate(center.x, center.y);
  // 画布 y 向下 = 编辑器 -Z；朝向角 rot（Unity 语义 forward=(sinθ,0,cosθ)）在画布
  // 上的旋转与 drawCollisionMarker 同款 rot 弧度，局部 -y 方向 = 坡的抬升方向。
  ctx.rotate((rot * Math.PI) / 180);

  // 坡面渐变：局部 -y（远端=高处）亮，+y（近端=低处）暗。设了调试显示色则
  // 用该色系（与游戏内 DebugVis 薄板所见一致）。
  const dbg = item.slope?.debugColor;
  const useDbg = typeof dbg === "string" && /^#[0-9a-fA-F]{6}$/.test(dbg);
  const grad = ctx.createLinearGradient(0, h / 2, 0, -h / 2);
  if (useDbg) {
    grad.addColorStop(0, `${dbg}26`);
    grad.addColorStop(1, `${dbg}66`);
  } else {
    grad.addColorStop(0, selected ? "rgba(249,171,0,0.18)" : "rgba(110,200,160,0.10)");
    grad.addColorStop(1, selected ? "rgba(249,171,0,0.42)" : "rgba(110,200,160,0.34)");
  }
  ctx.fillStyle = grad;
  ctx.fillRect(-w / 2, -h / 2, w, h);
  ctx.strokeStyle = selected ? "#f9ab00" : "rgba(110,200,160,0.7)";
  ctx.lineWidth = selected ? 2.5 : 1.2;
  ctx.setLineDash(selected ? [] : [4, 3]);
  ctx.strokeRect(-w / 2, -h / 2, w, h);
  ctx.setLineDash([]);

  // 登坡箭头：指向局部 -y（抬升方向），居中。
  const arrowLen = Math.min(h * 0.5, cellPx * 1.4);
  const headW = Math.min(w * 0.3, cellPx * 0.4);
  ctx.beginPath();
  ctx.moveTo(0, arrowLen / 2);
  ctx.lineTo(0, -arrowLen / 2);
  ctx.moveTo(-headW / 2, -arrowLen / 2 + headW * 0.9);
  ctx.lineTo(0, -arrowLen / 2);
  ctx.lineTo(headW / 2, -arrowLen / 2 + headW * 0.9);
  ctx.strokeStyle = selected ? "rgba(255,220,120,0.95)" : "rgba(150,230,190,0.85)";
  ctx.lineWidth = Math.max(1.5, cellPx * 0.06);
  ctx.stroke();

  // 起止高度标注（沿坡向，低端在 +y 下沿、高端在 -y 上沿）。
  ctx.fillStyle = selected ? "rgba(255,235,170,0.95)" : "rgba(170,235,205,0.85)";
  ctx.font = `bold ${Math.max(8, Math.round(cellPx * 0.17))}px sans-serif`;
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  const fmt = (v: number) => v.toFixed(1);
  ctx.fillText(`${fmt(startY)}m`, 0, h / 2 - cellPx * 0.18);
  ctx.fillText(`${fmt(endY)}m`, 0, -h / 2 + cellPx * 0.18);
  ctx.restore();
}

/** Background/water planes on the background layer show floor-style corner
 *  handles so they can be dragged to resize (non-uniform localScale). */
export function showItemResizeHandles(item: EditorItem, selected: boolean): boolean {
  if (!selected || S.selectedKeys.size !== 1) return false;
  if (isResizableBackgroundItem(item)) return S.currentLayer === "background";
  if (isAirWallItem(item)) return S.currentLayer === "items";
  return false;
}

/** Draw 4 corner resize handles in the current (translated + rotated) frame. */
function drawItemResizeHandles(bw: number, bh: number) {
  dom.ctx.fillStyle = "#f9ab00";
  for (const hx of [-bw / 2, bw / 2]) {
    for (const hy of [-bh / 2, bh / 2]) {
      dom.ctx.fillRect(hx - 3, hy - 3, 6, 6);
    }
  }
}

export function itemDrawCompare(a: EditorItem, b: EditorItem): number {  const d = drawLayerForItem(a, S.catalogByGuid) - drawLayerForItem(b, S.catalogByGuid);
  if (d !== 0) return d;
  const as = isSelected(a._editorKey) ? 1 : 0;
  const bs = isSelected(b._editorKey) ? 1 : 0;
  return as - bs;
}

/** 方向与 web 前向标记差 180° 的道具（prefab 烘焙了相反朝向）：
 *  烤箱与上菜台（含经 bundle 实测净朝向一致的换皮 dlc09_oven /
 *  dlc13_workstation_plate_station）。渲染方向加 180°，使 web 所见与
 *  游戏实际朝向一致；保存仍写原始 localRotationY（与 Unity 1:1）。
 *  其余换皮（dlc08_oven_02 / 中古炉 / dlc13 炉灶）净朝向不同，不在此列。 */
const FLIPPED_DIRECTION_IDS = new Set(["Oven", "dlc09_oven", "ServingStation", "dlc13_workstation_plate_station"]);

export function itemDisplayRotationY(item: EditorItem): number {
  const id = prefabIdFromPath(item.prefabAssetPath) ?? "";
  const flip = FLIPPED_DIRECTION_IDS.has(id) ? 180 : 0;
  return normalizeRot(item.localRotationY + flip);
}

export function drawItem(item: EditorItem, selected: boolean) {
  if (isAirSlopeItem(item)) {
    drawAirSlopeMarker(item, selected);
    return;
  }
  if (isCollisionItem(item)) {
    drawCollisionMarker(item, selected);
    return;
  }
  const cat = catalogItemForGuidOrPath(item.prefabGuid, item.prefabAssetPath);
  if (isSurfaceItem(cat)) {
    drawSurfaceItem(item, selected);
    return;
  }
  const fp = resolveFootprint(item);
  const rot = itemDisplayRotationY(item);
  const vc = itemVisualCenterXZ(item);
  const center = worldToCanvas(vc.x, vc.z);
  const cellPx = CELL * PX_PER_UNIT * S.scale;
  const sx = itemScaleX(item);
  const sz = itemScaleZ(item);
  let w = fp.cellsX * cellPx * sx;
  let h = fp.cellsZ * cellPx * sz;
  if (isResizableBackgroundItem(item)) {
    const c = itemPlaneCells(item);
    w = c.wCells * cellPx;
    h = c.dCells * cellPx;
  }
  const id = prefabIdFromPath(item.prefabAssetPath);
  const isUtensil =
    isStackUtensilCatalog(cat) || id === "Backpack" || id === "web_utensil_large_pot_01_pushable";
  const isPlayer = isPlayerItem(item);
  const paint = paintStyleForItem(cat, item.parentPath, selected);
  // 初始关闭开关（互锁按下侧 / 手动关初始态）：画布直接呈现关闭色 + 「关」角标
  // （与运行时开局外观一致——CustomStub SwitchStartVisual）。
  const swStartOff =
    (stubKindOf(item) === "Switch" || stubKindOf(item) === "CannonSwitch") &&
    item.switchStub?.startEnabled === false;

  const inset = isUtensil ? Math.min(cellPx * 0.22, 10) : 0;
  const bw = Math.max(4, w - inset * 2);
  const bh = Math.max(4, h - inset * 2);

  const rotRad = (-rot * Math.PI) / 180;
  const absCos = Math.abs(Math.cos(rotRad));
  const absSin = Math.abs(Math.sin(rotRad));
  const cw = bw * absCos + bh * absSin;
  const ch = bw * absSin + bh * absCos;

  dom.ctx.save();
  dom.ctx.translate(center.x, center.y);
  dom.ctx.rotate(rotRad);

  const topImg = counterTopImageOf(item);
  dom.ctx.fillStyle = swStartOff ? "#9c4a42" : counterSkinFillOf(item) ?? paint.fill;
  dom.ctx.fillRect(-bw / 2, -bh / 2, bw, bh);
  if (topImg) {
    // 俯视渲染图带透明度：底色（皮肤主色/原配色）透出 12%，网格与选中态隐约可见
    dom.ctx.globalAlpha = 0.88;
    dom.ctx.drawImage(topImg, -bw / 2, -bh / 2, bw, bh);
    dom.ctx.globalAlpha = 1;
  }

  dom.ctx.strokeStyle = paint.stroke;
  dom.ctx.lineWidth = selected ? 2 : 1;
  dom.ctx.strokeRect(-bw / 2, -bh / 2, bw, bh);

  if (!isUtensil && !topImg && (fp.cellsX > 1 || fp.cellsZ > 1)) {
    dom.ctx.strokeStyle = "rgba(255,255,255,0.15)";
    dom.ctx.lineWidth = 1;
    for (let i = 1; i < fp.cellsX; i++) {
      const x = -w / 2 + i * cellPx;
      dom.ctx.beginPath();
      dom.ctx.moveTo(x, -h / 2);
      dom.ctx.lineTo(x, h / 2);
      dom.ctx.stroke();
    }
    for (let j = 1; j < fp.cellsZ; j++) {
      const y = -h / 2 + j * cellPx;
      dom.ctx.beginPath();
      dom.ctx.moveTo(-w / 2, y);
      dom.ctx.lineTo(w / 2, y);
      dom.ctx.stroke();
    }
  }

  dom.ctx.fillStyle = "rgba(255,255,255,0.75)";
  dom.ctx.beginPath();
  dom.ctx.moveTo(-bw / 2 + 2, -bh / 2 + 2);
  dom.ctx.lineTo(-bw / 2 + 2, -bh / 2 + 10);
  dom.ctx.lineTo(-bw / 2 + 10, -bh / 2 + 2);
  dom.ctx.closePath();
  dom.ctx.fill();

  if (showItemResizeHandles(item, selected)) drawItemResizeHandles(bw, bh);

  dom.ctx.restore();

  dom.ctx.save();
  dom.ctx.beginPath();
  dom.ctx.rect(center.x - cw / 2 + 2, center.y - ch / 2 + 2, cw - 4, ch - 4);
  dom.ctx.clip();

  dom.ctx.fillStyle = paint.label;
  dom.ctx.textAlign = "center";
  dom.ctx.textBaseline = "middle";
  dom.ctx.translate(center.x, center.y);

  if (isPlayer) {
    drawLabelInBox(dom.ctx, itemLabel(item), bw - 4, bh - 4);
  } else {
    // 图标链始终优先（食材图 → 目录图标 → 炮开关星标），俯视图只做皮肤底图；
    // 全都无图时：有俯视图用底片标签（任何底图可读），否则常规文字标签
    const drawn = drawDispenserIngredient(dom.ctx, item, bw, bh) || drawCatalogItemIcon(dom.ctx, cat, item, bw, bh) || drawCannonSwitchStarIcon(dom.ctx, item, bw, bh);
    if (!drawn) {
      if (topImg) drawTopViewBadgeLabel(dom.ctx, itemLabel(item), bw, bh);
      else drawLabelInBox(dom.ctx, itemLabel(item), bw - 4, bh - 4);
    }
  }

  dom.ctx.restore();

  if (isConveyorItem(item)) {
    drawConveyorArrow(center, rot - 90, cellPx, item.conveyor?.conveySpeed ?? 0.5);
  } else if (isTeleportalItem(item)) {
    drawTeleportalBadge(item, center, cellPx);
  } else if (isFoodSpawnerItem(item)) {
    drawConveyorArrow(center, rot + 90, cellPx, 1, "#7bd889");
  }

  const pBadge = S.paramLabels.get(item.instanceId);
  if (pBadge)
    drawNumberBadge(center, bw, bh, cellPx, pBadge, S.paramColors.get(item.instanceId) ?? "#f9ab00", rot);
  drawTiltBadge(item, center, bh, cellPx, rot);
  if (swStartOff)
    drawNumberBadge(center, bw, bh, cellPx, "关", "#e07b6d", rot);
}

/**
 * X/Z 倾斜角标（v9）：俯视图无法表达倾斜，在物件下沿画一个小胶囊标注当前倾角。
 * 与参数角标错开：参数角标在右上角，倾斜角标在下沿中央。
 */
export function drawTiltBadge(
  item: EditorItem,
  center: { x: number; y: number },
  h: number,
  cellPx: number,
  rot = 0
) {
  const rx = item.localRotationX ?? 0;
  const rz = item.localRotationZ ?? 0;
  if (rx === 0 && rz === 0) return;
  const label = `X${Math.round(rx)}°Z${Math.round(rz)}°`;
  const fontPx = Math.max(8, Math.round(cellPx * 0.16));
  dom.ctx.save();
  dom.ctx.font = `bold ${fontPx}px sans-serif`;
  const tw = dom.ctx.measureText(label).width + fontPx * 0.7;
  const th = fontPx * 1.25;
  const rad = (-rot * Math.PI) / 180;
  const lx = 0;
  const ly = h / 2 + th * 0.7;
  const bx = center.x + lx * Math.cos(rad) - ly * Math.sin(rad);
  const by = center.y + lx * Math.sin(rad) + ly * Math.cos(rad);
  dom.ctx.fillStyle = "rgba(125,140,255,0.92)";
  dom.ctx.strokeStyle = "rgba(0,0,0,0.45)";
  dom.ctx.lineWidth = 1;
  const r = th / 2;
  dom.ctx.beginPath();
  dom.ctx.moveTo(bx - tw / 2 + r, by - th / 2);
  dom.ctx.lineTo(bx + tw / 2 - r, by - th / 2);
  dom.ctx.arc(bx + tw / 2 - r, by, r, -Math.PI / 2, Math.PI / 2);
  dom.ctx.lineTo(bx - tw / 2 + r, by + th / 2);
  dom.ctx.arc(bx - tw / 2 + r, by, r, Math.PI / 2, -Math.PI / 2);
  dom.ctx.closePath();
  dom.ctx.fill();
  dom.ctx.stroke();
  dom.ctx.fillStyle = "#fff";
  dom.ctx.textAlign = "center";
  dom.ctx.textBaseline = "middle";
  dom.ctx.fillText(label, bx, by);
  dom.ctx.restore();
}

export function drawSurfaceItem(item: EditorItem, selected: boolean) {
  const cat = catalogItemForGuidOrPath(item.prefabGuid, item.prefabAssetPath);
  const fp = resolveFootprint(item);
  const rot = normalizeRot(item.localRotationY);
  const center = worldToCanvas(item._wx, item._wz);
  const cellPx = CELL * PX_PER_UNIT * S.scale;
  const sx = itemScaleX(item);
  const sz = itemScaleZ(item);
  let w = fp.cellsX * cellPx * sx;
  let h = fp.cellsZ * cellPx * sz;
  if (isResizableBackgroundItem(item)) {
    const c = itemPlaneCells(item);
    w = c.wCells * cellPx;
    h = c.dCells * cellPx;
  }
  const paint = surfacePaint(cat?.surfaceKind, selected);

  dom.ctx.save();
  dom.ctx.translate(center.x, center.y);
  dom.ctx.rotate((-rot * Math.PI) / 180);

  const bw = Math.max(4, w);
  const bh = Math.max(4, h);
  dom.ctx.fillStyle = paint.fill;
  dom.ctx.fillRect(-bw / 2, -bh / 2, bw, bh);
  dom.ctx.strokeStyle = paint.stroke;
  dom.ctx.lineWidth = selected ? 2 : 1;
  dom.ctx.setLineDash([5, 4]);
  dom.ctx.strokeRect(-bw / 2, -bh / 2, bw, bh);
  dom.ctx.setLineDash([]);

  if (showItemResizeHandles(item, selected)) drawItemResizeHandles(bw, bh);

  const isTravelator = isTravelatorItem(item);
  if (paint.emoji && !isTravelator) {
    dom.ctx.font = `${Math.min(14, bh * 0.4)}px system-ui`;
    dom.ctx.textAlign = "center";
    dom.ctx.textBaseline = "middle";
    dom.ctx.fillStyle = paint.label;
    dom.ctx.fillText(paint.emoji, 0, 0);
  } else {
    dom.ctx.beginPath();
    dom.ctx.rect(-bw / 2 + 2, -bh / 2 + 2, bw - 4, bh - 4);
    dom.ctx.clip();
    dom.ctx.fillStyle = paint.label;
    dom.ctx.textAlign = "center";
    dom.ctx.textBaseline = "middle";
    drawLabelInBox(dom.ctx, itemLabel(item), bw - 4, bh - 4);
  }

  dom.ctx.restore();

  // 自动步道：localRotationY 0°左 / 90°上 / 180°右 / 270°下，与传送带同一套箭头绘制。
  if (isTravelator) {
    drawConveyorArrow(center, rot - 90, cellPx, item.travelator?.speed ?? 2.5);
  }
}

export function drawNumberBadge(
  center: { x: number; y: number },
  w: number,
  h: number,
  cellPx: number,
  label: string,
  color: string,
  rot = 0
) {
  const r = Math.max(4, cellPx * 0.13);
  const rad = (-rot * Math.PI) / 180;
  const cosR = Math.cos(rad);
  const sinR = Math.sin(rad);
  const lx = w / 2 - r - 1;
  const ly = -h / 2 + r + 1;
  const bx = center.x + lx * cosR - ly * sinR;
  const by = center.y + lx * sinR + ly * cosR;
  dom.ctx.save();
  dom.ctx.fillStyle = color;
  dom.ctx.strokeStyle = "rgba(0,0,0,0.45)";
  dom.ctx.lineWidth = 1;
  dom.ctx.beginPath();
  dom.ctx.arc(bx, by, r, 0, Math.PI * 2);
  dom.ctx.fill();
  dom.ctx.stroke();
  dom.ctx.fillStyle = "#1a1d23";
  dom.ctx.font = `bold ${Math.max(5, Math.round(cellPx * 0.14))}px sans-serif`;
  dom.ctx.textAlign = "center";
  dom.ctx.textBaseline = "middle";
  dom.ctx.fillText(label, bx, by);
  dom.ctx.restore();
}

export function drawConveyorArrow(center: { x: number; y: number }, rot: number, cellPx: number, speed: number, color = "#ffe49a") {
  const rad = (rot * Math.PI) / 180;
  let dx = Math.sin(rad);
  let dy = -Math.cos(rad);
  if (speed < 0) {
    dx = -dx;
    dy = -dy;
  }
  const L = cellPx * 0.42;
  const x0 = center.x - dx * L;
  const y0 = center.y - dy * L;
  const x1 = center.x + dx * L;
  const y1 = center.y + dy * L;
  dom.ctx.save();
  dom.ctx.strokeStyle = color;
  dom.ctx.fillStyle = color;
  dom.ctx.lineWidth = Math.max(2, cellPx * 0.12);
  dom.ctx.lineCap = "round";
  dom.ctx.beginPath();
  dom.ctx.moveTo(x0, y0);
  dom.ctx.lineTo(x1, y1);
  dom.ctx.stroke();
  const ah = cellPx * 0.22;
  const px = -dy;
  const py = dx;
  const bx = x1 - dx * ah;
  const by = y1 - dy * ah;
  dom.ctx.beginPath();
  dom.ctx.moveTo(x1, y1);
  dom.ctx.lineTo(bx + px * ah * 0.65, by + py * ah * 0.65);
  dom.ctx.lineTo(bx - px * ah * 0.65, by - py * ah * 0.65);
  dom.ctx.closePath();
  dom.ctx.fill();
  dom.ctx.restore();
}

export function drawTeleportalBadge(item: EditorItem, center: { x: number; y: number }, cellPx: number) {
  const color = PORTAL_COLORS[item.teleportal?.portalColor ?? 0] ?? "#c792ea";
  const label = S.teleportalLabels.get(item.instanceId) ?? "?";
  const r = cellPx * 0.46;
  dom.ctx.save();
  dom.ctx.strokeStyle = color;
  dom.ctx.lineWidth = Math.max(2.5, cellPx * 0.1);
  dom.ctx.beginPath();
  dom.ctx.arc(center.x, center.y, r, 0, Math.PI * 2);
  dom.ctx.stroke();
  if (item.teleportal?.doubleSided) {
    dom.ctx.setLineDash([4, 3]);
    dom.ctx.beginPath();
    dom.ctx.arc(center.x, center.y, r * 0.7, 0, Math.PI * 2);
    dom.ctx.stroke();
    dom.ctx.setLineDash([]);
  }
  const bx = center.x + r * 0.72;
  const by = center.y - r * 0.72;
  dom.ctx.fillStyle = color;
  dom.ctx.beginPath();
  dom.ctx.arc(bx, by, cellPx * 0.26, 0, Math.PI * 2);
  dom.ctx.fill();
  dom.ctx.fillStyle = "#1a1d23";
  dom.ctx.font = `bold ${Math.max(10, Math.round(cellPx * 0.3))}px sans-serif`;
  dom.ctx.textAlign = "center";
  dom.ctx.textBaseline = "middle";
  dom.ctx.fillText(label, bx, by);
  // 方向角标（入 / 出 / 双 / ?）：左下角小圆，与右上角的配对组号区分开。
  const role = teleportalRoleGlyph(item);
  if (role) {
    const rx = center.x - r * 0.72;
    const ry = center.y + r * 0.72;
    dom.ctx.fillStyle = "#1a1d23";
    dom.ctx.beginPath();
    dom.ctx.arc(rx, ry, cellPx * 0.26, 0, Math.PI * 2);
    dom.ctx.fill();
    dom.ctx.strokeStyle = color;
    dom.ctx.lineWidth = Math.max(1.2, cellPx * 0.04);
    dom.ctx.stroke();
    dom.ctx.fillStyle = color;
    dom.ctx.font = `bold ${Math.max(9, Math.round(cellPx * 0.26))}px sans-serif`;
    dom.ctx.fillText(role, rx, ry);
  }
  dom.ctx.restore();
}

/** 传送门方向：
 *  - "two"      双向（互指且两侧都能进）
 *  - "entrance" 单向入口（本门发送到出口门）
 *  - "exit"     仅作为出口（本门不发送）
 *  - "unbound"  未绑定出口（写回会被阻断）
 * 实现见 ./teleportalLinks（方向模型与配对改写的唯一收口）。 */
function teleportalRoleGlyph(item: EditorItem): string {
  switch (teleportalRole(item)) {
    case "two":
      return "双";
    case "entrance":
      return "入";
    case "exit":
      return "出";
    default:
      return "?";
  }
}

/** 传送门连线：单向 = 单箭头指向出口门；双向 = 两端各一个箭头（只画一次）。 */
export function drawTeleportalLinks() {
  const tp = teleportals();
  const byInst = new Map(tp.map((i) => [i.instanceId, i]));
  const drawnPairs = new Set<string>();
  for (const t of tp) {
    if (t.teleportal?.exitOnly) continue; // 出口门的回指占位不画（方向以入口为准）
    const exitId = t.teleportal?.exitPortalInstanceId;
    if (!exitId || exitId === t.instanceId) continue;
    const p = byInst.get(exitId);
    if (!p) continue;
    const twoWay = teleportalRole(t, byInst) === "two";
    if (twoWay) {
      const key = [t.instanceId, p.instanceId].sort().join("|");
      if (drawnPairs.has(key)) continue;
      drawnPairs.add(key);
    }
    const a = worldToCanvas(t._wx, t._wz);
    const b = worldToCanvas(p._wx, p._wz);
    const color = PORTAL_COLORS[t.teleportal?.portalColor ?? 0] ?? "#c792ea";
    dom.ctx.save();
    dom.ctx.strokeStyle = color;
    dom.ctx.fillStyle = color;
    dom.ctx.globalAlpha = 0.55;
    dom.ctx.lineWidth = 1.5;
    dom.ctx.setLineDash([6, 4]);
    dom.ctx.beginPath();
    dom.ctx.moveTo(a.x, a.y);
    dom.ctx.lineTo(b.x, b.y);
    dom.ctx.stroke();
    dom.ctx.setLineDash([]);
    drawLinkArrowHead(a, b);
    if (twoWay) drawLinkArrowHead(b, a);
    dom.ctx.restore();
  }
}

/** 连线箭头：画在 from→to 方向、贴近 to 端（当前 ctx 的 fillStyle 即箭头色）。 */
function drawLinkArrowHead(from: { x: number; y: number }, to: { x: number; y: number }) {
  const rad = Math.atan2(to.y - from.y, to.x - from.x);
  const ah = 9 * Math.max(0.6, S.scale);
  const gap = 10 * Math.max(0.6, S.scale); // 与门中心留空，避免压住徽标
  const tipX = to.x - Math.cos(rad) * gap;
  const tipY = to.y - Math.sin(rad) * gap;
  dom.ctx.beginPath();
  dom.ctx.moveTo(tipX, tipY);
  dom.ctx.lineTo(tipX - ah * Math.cos(rad - Math.PI / 7), tipY - ah * Math.sin(rad - Math.PI / 7));
  dom.ctx.lineTo(tipX - ah * Math.cos(rad + Math.PI / 7), tipY - ah * Math.sin(rad + Math.PI / 7));
  dom.ctx.closePath();
  dom.ctx.fill();
}

export interface ButtonPartnerEdge {
  aId: string;
  bId: string;
  mode: "conjugate" | "interlock";
}

/** 共轭/互锁配对边（每对只出现一次；供 2D/3D 连线）。 */
export function collectButtonPartnerEdges(): ButtonPartnerEdge[] {
  const edges: ButtonPartnerEdge[] = [];
  const seenPair = new Set<string>();
  const seenConj = new Set<string>();
  for (const l of S.buttonLinks) {
    if (l.pairId) {
      if (seenPair.has(l.pairId)) continue;
      const p = S.buttonLinks.find((x) => x !== l && x.pairId === l.pairId);
      if (!p) continue;
      seenPair.add(l.pairId);
      edges.push({ aId: l.sourceId, bId: p.sourceId, mode: "interlock" });
      continue;
    }
    for (const sid of l.sharedSourceIds ?? []) {
      if (!sid || sid === l.sourceId) continue;
      const key = [l.sourceId, sid].sort().join("|");
      if (seenConj.has(key)) continue;
      seenConj.add(key);
      edges.push({ aId: l.sourceId, bId: sid, mode: "conjugate" });
    }
  }
  return edges;
}

/** 共轭（青）/ 互锁（琥珀）双按钮配对连线，双向箭头。 */
export function drawButtonPartnerLinks() {
  const byInst = new Map(S.items.map((i) => [i.instanceId, i]));
  for (const e of collectButtonPartnerEdges()) {
    const a = byInst.get(e.aId);
    const b = byInst.get(e.bId);
    if (!a || !b) continue;
    const p1 = worldToCanvas(a._wx, a._wz);
    const p2 = worldToCanvas(b._wx, b._wz);
    const color = e.mode === "conjugate" ? "#5be8b5" : "#e8a14b";
    dom.ctx.save();
    dom.ctx.strokeStyle = color;
    dom.ctx.globalAlpha = 0.65;
    dom.ctx.lineWidth = 2;
    dom.ctx.setLineDash(e.mode === "interlock" ? [5, 4] : [8, 4]);
    dom.ctx.beginPath();
    dom.ctx.moveTo(p1.x, p1.y);
    dom.ctx.lineTo(p2.x, p2.y);
    dom.ctx.stroke();
    dom.ctx.setLineDash([]);
    const drawArrow = (from: { x: number; y: number }, to: { x: number; y: number }) => {
      const rad = Math.atan2(to.y - from.y, to.x - from.x);
      const ah = 7 * Math.max(0.6, S.scale);
      const gap = 10 * Math.max(0.6, S.scale);
      const tipX = to.x - Math.cos(rad) * gap;
      const tipY = to.y - Math.sin(rad) * gap;
      dom.ctx.fillStyle = color;
      dom.ctx.beginPath();
      dom.ctx.moveTo(tipX, tipY);
      dom.ctx.lineTo(tipX - ah * Math.cos(rad - 0.45), tipY - ah * Math.sin(rad - 0.45));
      dom.ctx.lineTo(tipX - ah * Math.cos(rad + 0.45), tipY - ah * Math.sin(rad + 0.45));
      dom.ctx.closePath();
      dom.ctx.fill();
    };
    drawArrow(p1, p2);
    drawArrow(p2, p1);
    dom.ctx.restore();
  }
}

/** 同轴组可视化数据（2D/3D 共用）：存活成员 + 目标 + 窗口秒数。 */
export interface CoaxialGroupVis {
  memberIds: string[];
  targetIds: string[];
  windowSeconds: number;
}

/** 同轴按钮组数据（成员/目标已按存活物品过滤）。 */
export function collectCoaxialGroups(): CoaxialGroupVis[] {
  return S.coaxialLinks
    .map((g) => ({
      memberIds: g.sourceIds.filter((id) => S.items.some((i) => i.instanceId === id)),
      targetIds: (g.targetIds ?? []).filter((id) => S.items.some((i) => i.instanceId === id)),
      windowSeconds: g.windowSeconds,
    }))
    .filter((g) => g.memberIds.length >= 2);
}

/** 同轴按钮组连线（紫色）：成员链（双向箭头虚线）+ 成员质心 → 目标（箭头）。 */
export function drawCoaxialLinks() {
  const byInst = new Map(S.items.map((i) => [i.instanceId, i]));
  const color = "#b48ef0";
  const drawArrow = (from: { x: number; y: number }, to: { x: number; y: number }, both: boolean) => {
    const rad = Math.atan2(to.y - from.y, to.x - from.x);
    const ah = 7 * Math.max(0.6, S.scale);
    const gap = 10 * Math.max(0.6, S.scale);
    const tipX = to.x - Math.cos(rad) * gap;
    const tipY = to.y - Math.sin(rad) * gap;
    dom.ctx.fillStyle = color;
    dom.ctx.beginPath();
    dom.ctx.moveTo(tipX, tipY);
    dom.ctx.lineTo(tipX - ah * Math.cos(rad - 0.45), tipY - ah * Math.sin(rad - 0.45));
    dom.ctx.lineTo(tipX - ah * Math.cos(rad + 0.45), tipY - ah * Math.sin(rad + 0.45));
    dom.ctx.closePath();
    dom.ctx.fill();
    if (both) drawArrow(to, from, false);
  };
  for (const g of collectCoaxialGroups()) {
    const pts = g.memberIds
      .map((id) => byInst.get(id))
      .filter((i): i is NonNullable<typeof i> => !!i)
      .map((i) => ({ x: i._wx, y: i._wz }));
    if (pts.length < 2) continue;
    dom.ctx.save();
    dom.ctx.strokeStyle = color;
    dom.ctx.globalAlpha = 0.6;
    dom.ctx.lineWidth = 2;
    dom.ctx.setLineDash([10, 5]);
    // 成员链：相邻成员两两相连（双向箭头 = 同组共进退）
    for (let i = 0; i + 1 < pts.length; i++) {
      const p1 = worldToCanvas(pts[i].x, pts[i].y);
      const p2 = worldToCanvas(pts[i + 1].x, pts[i + 1].y);
      dom.ctx.beginPath();
      dom.ctx.moveTo(p1.x, p1.y);
      dom.ctx.lineTo(p2.x, p2.y);
      dom.ctx.stroke();
      drawArrow(p1, p2, true);
    }
    dom.ctx.setLineDash([6, 4]);
    dom.ctx.lineWidth = 1.5;
    dom.ctx.globalAlpha = 0.55;
    // 质心 → 每个目标（单向箭头 = 集齐后广播）
    const cx = pts.reduce((s, p) => s + p.x, 0) / pts.length;
    const cy = pts.reduce((s, p) => s + p.y, 0) / pts.length;
    const center = worldToCanvas(cx, cy);
    for (const tid of g.targetIds) {
      const t = byInst.get(tid);
      if (!t) continue;
      const b = worldToCanvas(t._wx, t._wz);
      dom.ctx.beginPath();
      dom.ctx.moveTo(center.x, center.y);
      dom.ctx.lineTo(b.x, b.y);
      dom.ctx.stroke();
      drawArrow(center, b, false);
    }
    dom.ctx.setLineDash([]);
    dom.ctx.restore();
  }
}

/** 开关联动连线（switchLinks：开关 → 断头台/果汁机/酱料机等目标），橙色虚线 + 箭头指向目标。 */
export function drawSwitchLinks() {
  const byInst = new Map(S.items.map((i) => [i.instanceId, i]));
  for (const l of S.switchLinks) {
    const sw = byInst.get(l.switchId);
    const target = byInst.get(l.targetId);
    if (!sw || !target) continue;
    const a = worldToCanvas(sw._wx, sw._wz);
    const b = worldToCanvas(target._wx, target._wz);
    dom.ctx.save();
    dom.ctx.strokeStyle = "#f9ab00";
    dom.ctx.globalAlpha = 0.5;
    dom.ctx.lineWidth = 1.5;
    dom.ctx.setLineDash([6, 4]);
    dom.ctx.beginPath();
    dom.ctx.moveTo(a.x, a.y);
    dom.ctx.lineTo(b.x, b.y);
    dom.ctx.stroke();
    dom.ctx.setLineDash([]);
    const rad = Math.atan2(b.y - a.y, b.x - a.x);
    const ah = 8 * Math.max(0.6, S.scale);
    dom.ctx.fillStyle = "#f9ab00";
    dom.ctx.beginPath();
    dom.ctx.moveTo(b.x, b.y);
    dom.ctx.lineTo(b.x - Math.cos(rad - 0.45) * ah, b.y - Math.sin(rad - 0.45) * ah);
    dom.ctx.lineTo(b.x - Math.cos(rad + 0.45) * ah, b.y - Math.sin(rad + 0.45) * ah);
    dom.ctx.closePath();
    dom.ctx.fill();
    dom.ctx.restore();
  }
}

/** 控制终端连线（Terminal.pilotableObjectInstanceId → 目标物件），紫色虚线 + 箭头指向目标。 */
export function drawTerminalLinks() {
  const byInst = new Map(S.items.map((i) => [i.instanceId, i]));
  for (const tm of S.items) {
    if (stubKindOf(tm) !== "Terminal") continue;
    const targetId = tm.terminal?.pilotableObjectInstanceId;
    if (!targetId) continue;
    const target = byInst.get(targetId);
    if (!target) continue;
    const a = worldToCanvas(tm._wx, tm._wz);
    const b = worldToCanvas(target._wx, target._wz);
    dom.ctx.save();
    dom.ctx.strokeStyle = "#c75be8";
    dom.ctx.globalAlpha = 0.55;
    dom.ctx.lineWidth = 1.5;
    dom.ctx.setLineDash([6, 4]);
    dom.ctx.beginPath();
    dom.ctx.moveTo(a.x, a.y);
    dom.ctx.lineTo(b.x, b.y);
    dom.ctx.stroke();
    dom.ctx.setLineDash([]);
    const rad = Math.atan2(b.y - a.y, b.x - a.x);
    const ah = 8 * Math.max(0.6, S.scale);
    dom.ctx.fillStyle = "#c75be8";
    dom.ctx.beginPath();
    dom.ctx.moveTo(b.x, b.y);
    dom.ctx.lineTo(b.x - Math.cos(rad - 0.45) * ah, b.y - Math.sin(rad - 0.45) * ah);
    dom.ctx.lineTo(b.x - Math.cos(rad + 0.45) * ah, b.y - Math.sin(rad + 0.45) * ah);
    dom.ctx.closePath();
    dom.ctx.fill();
    dom.ctx.restore();
  }
}

export function worldToItemLocal(item: EditorItem, wx: number, wz: number): { lx: number; lz: number } {
  const origin = itemVisualCenterXZ(item);
  const dx = wx - origin.x;
  const dz = wz - origin.z;
  const rad = (normalizeRot(item.localRotationY) * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  return {
    lx: dx * cos + dz * sin,
    lz: -dx * sin + dz * cos,
  };
}

export function hitTestAll(wx: number, wz: number, allLayers?: boolean): EditorItem[] {
  const sorted = S.items
    .filter((it) => (allLayers ? true : isActiveItemLayer(it)))
    .filter((it) => categoryVisible(itemCategoryOf(it)))
    // 高度过滤：范围外的物品不参与点选/框选（「全部」时不过滤）。
    .filter(itemInHeightFilter)
    .sort((a, b) => itemDrawCompare(b, a));
  return sorted.filter((item) => {
    const fp = resolveFootprint(item);
    const { lx, lz } = worldToItemLocal(item, wx, wz);
    let hw = ((fp.cellsX * CELL) / 2) * itemScaleX(item);
    let hh = ((fp.cellsZ * CELL) / 2) * itemScaleZ(item);
    if (isResizableBackgroundItem(item)) {
      const c = itemPlaneCells(item);
      hw = (c.wCells * CELL) / 2;
      hh = (c.dCells * CELL) / 2;
    }
    return Math.abs(lx) <= hw && Math.abs(lz) <= hh;
  });
}

export interface ItemResizeHit {
  edge: string;
  anchorX: number;
  anchorZ: number;
}

/** Corner-handle hit test for a resizable background item (mirrors floor
 *  hitTestFloorsAll resize branch). Returns null when the point is not on a
 *  corner handle. */
export function hitTestItemResizeHandle(item: EditorItem, wx: number, wz: number): ItemResizeHit | null {
  const { wCells, dCells } = isAirWallItem(item) ? airWallCells(item) : itemPlaneCells(item);
  if (wCells === 1 && dCells === 1) return null;
  const hw = (wCells * CELL) / 2;
  const hh = (dCells * CELL) / 2;
  const { lx, lz } = worldToItemLocal(item, wx, wz);
  if (Math.abs(lx) > hw || Math.abs(lz) > hh) return null;
  const handleTol = Math.max(CELL * 0.5, 0.9);
  const nearLeft = lx < -hw + handleTol;
  const nearRight = lx > hw - handleTol;
  const nearBottom = lz < -hh + handleTol;
  const nearTop = lz > hh - handleTol;
  if (!((nearLeft || nearRight) && (nearBottom || nearTop))) return null;
  const edge = `${nearRight ? "R" : "L"}${nearTop ? "T" : "B"}`;
  const ax = nearRight ? -hw : hw;
  const az = nearTop ? -hh : hh;
  const rad = (normalizeRot(item.localRotationY) * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  const origin = itemVisualCenterXZ(item);
  const anchorX = origin.x + ax * cos - az * sin;
  const anchorZ = origin.z + ax * sin + az * cos;
  return { edge, anchorX, anchorZ };
}

export function isConveyorItem(item: EditorItem): boolean {
  return item.stubKind === "Conveyor" || prefabIdFromPath(item.prefabAssetPath) === "ConveyorStation";
}

export function isPlayerItem(item: EditorItem): boolean {
  return item.stubKind === "Player" || prefabIdFromPath(item.prefabAssetPath) === "Player";
}

export function isFoodSpawnerItem(item: EditorItem): boolean {
  return item.stubKind === "AttachingFoodSpawner" || prefabIdFromPath(item.prefabAssetPath) === "AttachingFoodSpawner";
}

export const PORTAL_COLORS = [  "#9ad7ff",
  "#5b8def",
  "#ef6f6f",
  "#f0a847",
  "#7bd889",
  "#9c8a5a",
  "#c792ea",
  "#b15bd9",
  "#e8945a",
];

export const PARAM_BADGE_TYPES: { match: (it: EditorItem) => boolean; type: string; color: string }[] = [
  { match: (it) => isHotpotBurnerItem(it) && it.timedSwitch?.enabled === true, type: "定时灶台", color: "#e8704b" },
  { match: isServingStationItem, type: "上菜台", color: "#f9ab00" },
  { match: isPlateReturnItem, type: "脏盘台", color: "#7bd889" },
  { match: isGlassReturnItem, type: "脏杯台", color: "#5ec8e0" },
  { match: (it) => stubKindOf(it) === "CookingUtensil", type: "锅具", color: "#e8915b" },
  { match: (it) => stubKindOf(it) === "Dispenser" && prefabIdFromPath(it.prefabAssetPath) === "Backpack", type: "背包", color: "#d4a574" },
  { match: (it) => stubKindOf(it) === "Dispenser", type: "食材箱", color: "#5b9be8" },
  { match: isFoodSpawnerItem, type: "生成器", color: "#9be88a" },
  { match: (it) => stubKindOf(it) === "Travelator" && it.travelator?.timedReverse?.enabled === true, type: "定时步道", color: "#e8704b" },
  { match: (it) => stubKindOf(it) === "Travelator", type: "移动板", color: "#c792ea" },
  { match: isConveyorItem, type: "传送带", color: "#e8d24e" },
  { match: (it) => stubKindOf(it) === "Flamethrower", type: "喷火器", color: "#e85b5b" },
  { match: (it) => stubKindOf(it) === "Burner", type: "燃烧弹射器", color: "#d97742" },
  { match: (it) => stubKindOf(it) === "CleanPlateStack", type: "盘堆", color: "#8db8e8" },
  { match: (it) => stubKindOf(it) === "Cannon", type: "大炮", color: "#e8a14b" },
  { match: (it) => stubKindOf(it) === "CannonSwitch", type: "大炮开关", color: "#e8a14b" },
  { match: (it) => stubKindOf(it) === "Switch", type: "开关", color: "#e8cf5b" },
  { match: (it) => stubKindOf(it) === "PressureSwitch", type: "压力开关", color: "#5be8b5" },
  { match: (it) => stubKindOf(it) === "Terminal", type: "终端", color: "#c75be8" },
];

export function paramBadgeInfo(item: EditorItem): { type: string; color: string } | null {
  for (const t of PARAM_BADGE_TYPES) if (t.match(item)) return { type: t.type, color: t.color };
  return null;
}

export function computeParamLabels(): void {
  S.paramLabels = new Map();
  S.paramColors = new Map();
  const counters = new Map<string, number>();
  const sorted = [...S.items]
    .filter((it) => paramBadgeInfo(it) != null)
    .sort((a, b) => (a._wz - b._wz) || (a._wx - b._wx));
  for (const it of sorted) {
    const info = paramBadgeInfo(it)!;
    const n = (counters.get(info.type) ?? 0) + 1;
    counters.set(info.type, n);
    S.paramLabels.set(it.instanceId, n.toString());
    S.paramColors.set(it.instanceId, info.color);
  }
}

/** 燃烧弹射器落点显示尺寸（米）：0.9×0.9，与独立弹窗编辑器一致。 */
const BURNER_MARKER_SIZE_M = 0.9;
/** 实测：火焰落地后维持约 20 秒。同格两波触发间隔 < 20s = 火焰重叠。 */
const BURNER_FLAME_LIFETIME_S = 20;

/** 核心层专属叠加：唯一选中的物品是燃烧弹射器时，绘制其全部波次落点
 *  （0.9×0.9 火焰方块 + 波次号徽标；同格多波对角叠放、徽标合并显示）。
 *  同格火焰重叠（间隔 < 20s 或波内重复落点）时改用红色警示描边/徽标。
 *  不选中 / 多选 / 非燃烧弹射器时不显示（火焰只在聚焦时可见）。 */
export function drawBurnerFocusedMarkers(): void {
  if (S.selectedKeys.size !== 1) return;
  const item = S.items.find((it) => it._editorKey === [...S.selectedKeys][0]);
  if (!item || stubKindOf(item) !== "Burner") return;
  const waves = item.burner?.waves ?? [];
  if (waves.length === 0) return;

  const cellPx = CELL * PX_PER_UNIT * S.scale;
  const markerPx = BURNER_MARKER_SIZE_M * PX_PER_UNIT * S.scale;
  const badgeFont = Math.max(9, cellPx * 0.28);
  const ctx = dom.ctx;

  // 各波累计触发时间（与独立弹窗编辑器同口径）：第 0 波 = 开局延迟（默认 10）；
  // 第 k 波 = 前一波 + interval[k]（wave[0].interval 不参与计时）。
  const times: number[] = [];
  {
    let acc = Math.max(0, item.burner?.startDelaySeconds ?? 10);
    for (let i = 0; i < waves.length; i++) {
      if (i > 0) acc += Math.max(1, waves[i]?.intervalSeconds ?? 30);
      times.push(Math.round(acc * 10) / 10);
    }
  }

  // 顺序逐发模式：各波内的逐发序号（阅读顺序：从上到下、从左到右）。
  const sequential = item.burner?.sequentialFire === true;
  const seqOrder = new Map<string, number>();
  if (sequential) {
    for (let w = 0; w < waves.length; w++) {
      const positions = waves[w]?.positions ?? [];
      positions
        .map((_, i) => i)
        .sort((a, b) => {
          const dz = positions[a].z - positions[b].z;
          if (Math.abs(dz) > 0.0001) return dz;
          const dx = positions[a].x - positions[b].x;
          if (Math.abs(dx) > 0.0001) return dx;
          return a - b;
        })
        .forEach((posIdx, order) => seqOrder.set(`${w}:${posIdx}`, order + 1));
    }
  }

  // 同格分组（跨波重叠 / 波内重复）。存 {w, p} 以支持逐发序号查询。
  const stacks = new Map<string, { w: number; p: number }[]>();
  for (let w = 0; w < waves.length; w++) {
    const positions = waves[w]?.positions ?? [];
    for (let p = 0; p < positions.length; p++) {
      const pos = positions[p];
      const key = `${pos.x},${pos.z}`;
      const list = stacks.get(key) ?? [];
      list.push({ w, p });
      stacks.set(key, list);
    }
  }

  ctx.save();
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  for (const [key, refs] of stacks) {
    const [x, z] = key.split(",").map(Number);
    const center = worldToCanvas(x, z);
    refs.sort((a, b) => a.w - b.w);
    const ws = refs.map((r) => r.w);
    const numbers = ws.map((w) => w + 1).join("·");
    // 重叠判定：波内重复（同刻同格），或任两波时间差 < 火焰持续 20s
    //（顺序逐发的波内 stagger < 1s，仍按波开始时刻近似判定，足够）。
    let overlapped = false;
    for (let i = 0; i < ws.length && !overlapped; i++) {
      for (let j = i + 1; j < ws.length && !overlapped; j++) {
        if (Math.abs(times[ws[j]] - times[ws[i]]) < BURNER_FLAME_LIFETIME_S) overlapped = true;
      }
      if (ws.indexOf(ws[i]) !== ws.lastIndexOf(ws[i])) overlapped = true;
    }
    for (let i = 0; i < refs.length; i++) {
      const ref = refs[i];
      const off = i * 0.14 * cellPx;
      const cxp = center.x + off;
      const cyp = center.y + off;
      ctx.globalAlpha = 0.85;
      ctx.fillStyle = overlapped ? "rgba(255,64,64,0.45)" : "rgba(255,106,61,0.5)";
      ctx.fillRect(cxp - markerPx / 2, cyp - markerPx / 2, markerPx, markerPx);
      ctx.strokeStyle = overlapped ? "#ff5c5c" : "#ff8a5c";
      ctx.lineWidth = overlapped ? Math.max(2, cellPx * 0.06) : Math.max(1, cellPx * 0.045);
      ctx.strokeRect(cxp - markerPx / 2, cyp - markerPx / 2, markerPx, markerPx);
      ctx.fillStyle = "rgba(255,214,140,0.85)";
      ctx.beginPath();
      ctx.arc(cxp, cyp, Math.max(1.5, markerPx * 0.08), 0, Math.PI * 2);
      ctx.fill();
      // 顺序逐发模式：标记右下角画波内发射序号。
      if (sequential) {
        const order = seqOrder.get(`${ref.w}:${ref.p}`);
        if (order != null) {
          ctx.font = `bold ${Math.max(8, markerPx * 0.3)}px system-ui, sans-serif`;
          ctx.fillStyle = "#ffd6a5";
          ctx.textAlign = "right";
          ctx.textBaseline = "bottom";
          ctx.fillText(String(order), cxp + markerPx / 2 - 1, cyp + markerPx / 2 + 1);
          ctx.textAlign = "center";
          ctx.textBaseline = "middle";
        }
      }
    }
    const topOff = (refs.length - 1) * 0.14 * cellPx;
    const bx = center.x + topOff + markerPx / 2;
    const by = center.y + topOff - markerPx / 2;
    ctx.globalAlpha = 1;
    ctx.font = `bold ${badgeFont}px system-ui, sans-serif`;
    const tw = ctx.measureText(numbers).width;
    const pad = badgeFont * 0.32;
    const bw = tw + pad * 2;
    const bh = badgeFont * 1.25;
    ctx.fillStyle = overlapped ? "#8f1d10" : "#7a2410";
    ctx.beginPath();
    const r = bh * 0.32;
    ctx.moveTo(bx - bw / 2 + r, by - bh);
    ctx.arcTo(bx + bw / 2, by - bh, bx + bw / 2, by, r);
    ctx.arcTo(bx + bw / 2, by, bx - bw / 2, by, r);
    ctx.arcTo(bx - bw / 2, by, bx - bw / 2, by - bh, r);
    ctx.arcTo(bx - bw / 2, by - bh, bx + bw / 2, by - bh, r);
    ctx.closePath();
    ctx.fill();
    ctx.fillStyle = overlapped ? "#ffd9d0" : "#ffe9d6";
    ctx.fillText(numbers, bx, by - bh / 2 + 0.5);
  }
  ctx.restore();
}
