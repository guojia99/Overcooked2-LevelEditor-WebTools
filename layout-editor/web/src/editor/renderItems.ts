import {
  normalizeRot,
  worldToCanvas,
  resolveFootprint,
  itemScaleX,
  itemScaleZ,
  prefabIdFromPath
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
  catalogItemForGuidOrPath
} from "./catalog";
import {
  drawLabelInBox,
  itemLabel,
  drawDispenserIngredient,
  drawCatalogItemIcon,
  drawCannonSwitchStarIcon
} from "./labels";
import {
  isCollisionItem,
  stubKindOf
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
import {
  isServingStationItem,
  isPlateReturnItem,
  isGlassReturnItem
} from "./servingLinks";

/** 空气箱（隐形碰撞块）：编辑器内以虚线框 + 半透明填充标示，游戏内不可见。 */
function drawCollisionMarker(item: EditorItem, selected: boolean) {
  const fp = resolveFootprint(item);
  const rot = normalizeRot(item.localRotationY);
  const center = worldToCanvas(item._wx, item._wz);
  const cellPx = CELL * PX_PER_UNIT * S.scale;
  const w = fp.cellsX * cellPx * itemScaleX(item);
  const h = fp.cellsZ * cellPx * itemScaleZ(item);
  const ctx = dom.ctx;
  const rotated = rot === 90 || rot === 270;
  const [dw, dh] = rotated ? [h, w] : [w, h];

  ctx.save();
  ctx.translate(center.x, center.y);
  ctx.rotate((rot * Math.PI) / 180);
  ctx.fillStyle = selected ? "rgba(120,160,255,0.28)" : "rgba(120,160,255,0.12)";
  ctx.fillRect(-dw / 2, -dh / 2, dw, dh);
  ctx.strokeStyle = selected ? "rgba(140,180,255,0.95)" : "rgba(120,160,255,0.55)";
  ctx.lineWidth = 1.2;
  ctx.setLineDash([4, 3]);
  ctx.strokeRect(-dw / 2, -dh / 2, dw, dh);
  ctx.setLineDash([]);
  ctx.fillStyle = selected ? "rgba(180,210,255,0.95)" : "rgba(150,190,255,0.75)";
  drawLabelInBox(ctx, "空气墙", dw, dh);
  ctx.restore();
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
  const center = worldToCanvas(item._wx, item._wz);
  const cellPx = CELL * PX_PER_UNIT * S.scale;
  const sx = itemScaleX(item);
  const sz = itemScaleZ(item);
  const w = fp.cellsX * cellPx * sx;
  const h = fp.cellsZ * cellPx * sz;
  const id = prefabIdFromPath(item.prefabAssetPath);
  const isUtensil = isStackUtensilCatalog(cat) || id === "Backpack";
  const isPlayer = isPlayerItem(item);
  const paint = paintStyleForItem(cat, item.parentPath, selected);

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

  dom.ctx.fillStyle = paint.fill;
  dom.ctx.fillRect(-bw / 2, -bh / 2, bw, bh);

  dom.ctx.strokeStyle = paint.stroke;
  dom.ctx.lineWidth = selected ? 2 : 1;
  dom.ctx.strokeRect(-bw / 2, -bh / 2, bw, bh);

  if (!isUtensil && (fp.cellsX > 1 || fp.cellsZ > 1)) {
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
    const drawn = drawDispenserIngredient(dom.ctx, item, bw, bh) || drawCatalogItemIcon(dom.ctx, cat, item, bw, bh) || drawCannonSwitchStarIcon(dom.ctx, item, bw, bh);
    if (!drawn) {
      drawLabelInBox(dom.ctx, itemLabel(item), bw - 4, bh - 4);
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
}

export function drawSurfaceItem(item: EditorItem, selected: boolean) {
  const cat = catalogItemForGuidOrPath(item.prefabGuid, item.prefabAssetPath);
  const fp = resolveFootprint(item);
  const rot = normalizeRot(item.localRotationY);
  const center = worldToCanvas(item._wx, item._wz);
  const cellPx = CELL * PX_PER_UNIT * S.scale;
  const sx = itemScaleX(item);
  const sz = itemScaleZ(item);
  const w = fp.cellsX * cellPx * sx;
  const h = fp.cellsZ * cellPx * sz;
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

  if (paint.emoji) {
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
  dom.ctx.restore();
}

export function drawTeleportalLinks() {
  const tp = teleportals();
  const byInst = new Map(tp.map((i) => [i.instanceId, i]));
  for (const t of tp) {
    const exitId = t.teleportal?.exitPortalInstanceId;
    if (!exitId || exitId === t.instanceId) continue;
    const p = byInst.get(exitId);
    if (!p) continue;
    const a = worldToCanvas(t._wx, t._wz);
    const b = worldToCanvas(p._wx, p._wz);
    const color = PORTAL_COLORS[t.teleportal?.portalColor ?? 0] ?? "#c792ea";
    dom.ctx.save();
    dom.ctx.strokeStyle = color;
    dom.ctx.globalAlpha = 0.45;
    dom.ctx.lineWidth = 1.5;
    dom.ctx.setLineDash([6, 4]);
    dom.ctx.beginPath();
    dom.ctx.moveTo(a.x, a.y);
    dom.ctx.lineTo(b.x, b.y);
    dom.ctx.stroke();
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

export function worldToItemLocal(item: EditorItem, wx: number, wz: number): { lx: number; lz: number } {
  const dx = wx - item._wx;
  const dz = wz - item._wz;
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
    .sort((a, b) => itemDrawCompare(b, a));
  return sorted.filter((item) => {
    const fp = resolveFootprint(item);
    const { lx, lz } = worldToItemLocal(item, wx, wz);
    const hw = ((fp.cellsX * CELL) / 2) * itemScaleX(item);
    const hh = ((fp.cellsZ * CELL) / 2) * itemScaleZ(item);
    return Math.abs(lx) <= hw && Math.abs(lz) <= hh;
  });
}

export function computeTeleportalLabels(): Map<string, string> {
  const tp = teleportals();
  const byInst = new Map(tp.map((i) => [i.instanceId, i]));
  const label = new Map<string, string>();
  let n = 0;
  for (const t of tp) {
    if (label.has(t.instanceId)) continue;
    const lab = (++n).toString();
    label.set(t.instanceId, lab);
    const exitId = t.teleportal?.exitPortalInstanceId;
    if (exitId && byInst.has(exitId) && !label.has(exitId)) label.set(exitId, lab);
  }
  for (const t of tp) if (!label.has(t.instanceId)) label.set(t.instanceId, "?");
  return label;
}

export function isConveyorItem(item: EditorItem): boolean {
  return item.stubKind === "Conveyor" || prefabIdFromPath(item.prefabAssetPath) === "ConveyorStation";
}

export function isTeleportalItem(item: EditorItem): boolean {
  return item.stubKind === "Teleportal" || prefabIdFromPath(item.prefabAssetPath) === "Teleportal";
}

export function teleportals(): EditorItem[] {
  return S.items.filter(isTeleportalItem);
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
  { match: isServingStationItem, type: "上菜台", color: "#f9ab00" },
  { match: isPlateReturnItem, type: "脏盘台", color: "#7bd889" },
  { match: isGlassReturnItem, type: "脏杯台", color: "#5ec8e0" },
  { match: (it) => stubKindOf(it) === "CookingUtensil", type: "锅具", color: "#e8915b" },
  { match: (it) => stubKindOf(it) === "Dispenser" && prefabIdFromPath(it.prefabAssetPath) === "Backpack", type: "背包", color: "#d4a574" },
  { match: (it) => stubKindOf(it) === "Dispenser", type: "食材箱", color: "#5b9be8" },
  { match: isFoodSpawnerItem, type: "生成器", color: "#9be88a" },
  { match: (it) => stubKindOf(it) === "Travelator", type: "移动板", color: "#c792ea" },
  { match: isConveyorItem, type: "传送带", color: "#e8d24e" },
  { match: (it) => stubKindOf(it) === "Flamethrower", type: "喷火器", color: "#e85b5b" },
  { match: (it) => stubKindOf(it) === "Burner", type: "喷射器", color: "#d97742" },
  { match: (it) => stubKindOf(it) === "CleanPlateStack", type: "盘堆", color: "#8db8e8" },
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
