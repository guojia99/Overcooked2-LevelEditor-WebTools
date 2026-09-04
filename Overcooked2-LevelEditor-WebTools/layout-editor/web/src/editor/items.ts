import {
  uuid,
  snapPlacement,
  resolveFootprint,
  normalizeRot,
  itemScaleX,
  itemScaleZ,
  newEditorKey,
  stepDisplayDecimals,
  prefabIdFromPath,
  isSwCornerPivotPrefabId,
  isLargePotPrefabId,
  itemPrefabId,
  itemVisualCenterXZ,
  stubToVisualOffset,
  hotpotEditorStubFromUnity,
  largePotUnityFromVisualCenter,
  largePotVisualOffsetWorld,
  editorItemUnityWorldXZ,
  visualCenterToStubXZ,
  syncItemLocalFromEditor
} from "./coords";
import {
  S,
  CELL,
  AIR_WALL_BASE_XZ,
  AIR_WALL_BASE_Y,
  EditorItem,
  EditorFloor
} from "./state";
import type { LayoutItem } from "../types";
import {
  itemLayerOfIt,
  isBackgroundPlaneCat,
  isStandingWaterQuadCat,
  isResizableBackgroundItem,
  planeCatalogFootprint,
  catalogItemForGuidOrPath,
  planeNativeForItem,
  planeScaleFromCells,
  planeCatalogFootprint,
  catalogItemById,
  ingredientIdByGuid
} from "./catalog";
import {
  isCollisionItem,
  isAirWallItem,
  isLotusPressureSwitchItem
} from "./stubControls";
import { cleanOrphanedButtonLinks } from "./buttonLinks";
import { cleanOrphanedButtonEvents } from "./buttonEvents";
import { isPlayerItem } from "./renderItems";
import { itemLabel } from "./labels";
import {
  selectionKeys,
  setSelection,
  clearSelection
} from "./selection";
import {
  hideDetail,
  hideContextMenu
} from "./ui/overlay";
import { pointInWalkable, parseMaterialTilingFromName } from "./floors";
import { floorHeightAt, floorHeightFilterActive } from "./floorHeight";
import { draw } from "./render";
import { pushHistory } from "./historyOps";
import { setStatus } from "./status";
import { snapValue } from "../snap";
import {
  trySnapUtensilToHost,
  isStackHostCatalog
} from "../stacking";
import type {
  LayoutItem,
  FloorObject,
  CatalogItem
} from "../types";

/** 旧版食材装饰（stubKind=IngredientDecor）→ commonW1 decor/food 包装 prefab。 */
function migrateIngredientDecorToWrapper(raw: LayoutItem): void {
  if (raw.stubKind !== "IngredientDecor" && !raw.ingredientDecor) return;
  const ingGuid = raw.ingredientDecor?.ingredientGuid || raw.prefabGuid;
  let ingId = prefabIdFromPath(raw.prefabAssetPath);
  if (!ingId && ingGuid) ingId = ingredientIdByGuid(ingGuid);
  const wrapper = ingId ? catalogItemById(ingId) : undefined;
  if (wrapper?.category === "decor/food") {
    raw.prefabGuid = wrapper.guid;
    raw.prefabAssetPath = wrapper.assetPath;
    if (!raw.parentPath) raw.parentPath = wrapper.defaultParent;
  }
  delete raw.stubKind;
  delete raw.ingredientDecor;
}

/** common03 decor 包装已迁至 commonW1：按 catalog id 更新 guid / assetPath。 */
function migrateDecorCommon03ToCommonW1(raw: LayoutItem): void {
  const p = raw.prefabAssetPath ?? "";
  if (!p.includes("/common03/prefabs/") || !p.includes("/decor/")) return;
  const id = prefabIdFromPath(p);
  if (!id) return;
  const wrapper = catalogItemById(id);
  if (!wrapper || !wrapper.assetPath.includes("/commonW1/")) return;
  raw.prefabGuid = wrapper.guid;
  raw.prefabAssetPath = wrapper.assetPath;
  if (!raw.parentPath) raw.parentPath = wrapper.defaultParent;
}

/** 旧版空气墙 collider 水平 1m×localScale；新版底边 1.2m，scale 为格数倍率。 */
function migrateAirWallScale(raw: LayoutItem): void {
  if (!raw.airWall || !raw.localScale) return;
  const sx = raw.localScale.x ?? 1;
  const sz = raw.localScale.z ?? 1;
  const rot = normalizeRot(raw.localRotationY ?? 0);
  const swapped = rot === 90 || rot === 270;
  const wScale = swapped ? sz : sx;
  const dScale = swapped ? sx : sz;
  const y = raw.localScale.y ?? 1;

  // 旧版 web：scale=1.2 补偿 1m 底 collider
  if (Math.abs(wScale - CELL) < 0.02 && Math.abs(dScale - CELL) < 0.02) {
    raw.localScale = { x: 1, y, z: 1 };
    return;
  }

  // 已是新版格数倍率（小整数 scale）
  const wNonInt = Math.abs(wScale - Math.round(wScale)) > 0.02;
  const dNonInt = Math.abs(dScale - Math.round(dScale)) > 0.02;
  if (!wNonInt && !dNonInt && wScale >= 1 && dScale >= 1 && wScale <= 200 && dScale <= 200) {
    return;
  }

  const wCells = Math.max(1, Math.round(wScale / CELL));
  const dCells = Math.max(1, Math.round(dScale / CELL));
  raw.localScale = swapped
    ? { x: dCells, y, z: wCells }
    : { x: wCells, y, z: dCells };
}

export function enrichItem(raw: LayoutItem, editorKey: string): EditorItem {
  // 历史场景里的裸 pushable_object（载体，无锅）已删除：迁移到可推动大火锅包装器。
  if (raw.prefabAssetPath === "Assets/common03/prefabs/core/mechanisms/pushable_object.prefab") {
    // 迁移目标随 common03 正式版重组迁至 commonW1（GUID 不变，场景引用不断）
    raw.prefabAssetPath = "Assets/commonW1/prefabs/web/hotpot/web_utensil_large_pot_01_pushable.prefab";
  }
  // 迁移：早期版本把立式水面 quad（Water_01 家族）当作平铺 XZ 面（rotX=0，深度
  // 写在 localScale.z），而这些 quad 只有 rotX=90 才平躺（深度映射到 localScale.y），
  // 否则在 Unity 里立成细长竖条。载入时修正：放平、把深度搬到 Y 轴。
  const cat = catalogItemForGuidOrPath(raw.prefabGuid, raw.prefabAssetPath);
  if (isStandingWaterQuadCat(cat) && Math.abs((raw.localRotationX ?? 0) - 90) > 1) {
    const s = raw.localScale;
    const depth = s && s.z && s.z > 0 ? s.z : s?.y ?? 1;
    raw.localScale = { x: s?.x ?? 1, y: depth, z: 1 };
    raw.localRotationX = 90;
  }
  if (raw.airWall) {
    migrateAirWallScale(raw);
    if (!raw.localScale) raw.localScale = { x: 1, y: 1, z: 1 };
    if (!(raw.localScale.y > 0)) raw.localScale.y = 1;
    ensureAirWallColliderCenter(raw);
  }
  migrateIngredientDecorToWrapper(raw);
  migrateDecorCommon03ToCommonW1(raw);
  const wp = raw.worldPosition ?? raw.localPosition;
  // 背景水面等：Unity 导出 footprint 为渲染器实测格数（如 1×26），尺寸由
  // catalog 1×1 × localScale 表达；载入时还原为目录基准，避免与 scale 二次相乘。
  const fp = isResizableBackgroundItem(raw)
    ? planeCatalogFootprint(raw)
    : resolveFootprint(raw);
  const pid = prefabIdFromPath(raw.prefabAssetPath);
  let stubX = wp.x;
  let stubZ = wp.z;
  if (isSwCornerPivotPrefabId(pid)) {
    const ed = hotpotEditorStubFromUnity(stubX, stubZ, raw.localRotationY ?? 0);
    stubX = ed.x;
    stubZ = ed.z;
  }
  return {
    ...raw,
    _editorKey: editorKey,
    footprint: fp,
    _wx: stubX,
    _wz: stubZ,
    _parentWx: wp.x - raw.localPosition.x,
    _parentWz: wp.z - raw.localPosition.z,
  };
}

export function enrichFloor(raw: FloorObject, key: string): EditorFloor {
  const wp = raw.worldPosition ?? raw.localPosition;
  const w = raw.widthCells > 0 ? raw.widthCells : Math.max(1, Math.round(raw.widthUnits / CELL));
  const d = raw.depthCells > 0 ? raw.depthCells : Math.max(1, Math.round(raw.depthUnits / CELL));
  let tilingW = raw.materialTilingW && raw.materialTilingW > 0 ? raw.materialTilingW : w;
  let tilingD = raw.materialTilingD && raw.materialTilingD > 0 ? raw.materialTilingD : d;
  if (raw.materialName) {
    const parsed = parseMaterialTilingFromName(raw.materialName);
    if (parsed) {
      if (!raw.materialTilingW || raw.materialTilingW <= 0) tilingW = parsed.w;
      if (!raw.materialTilingD || raw.materialTilingD <= 0) tilingD = parsed.d;
    }
  }
  return {
    ...raw,
    materialTilingW: tilingW,
    materialTilingD: tilingD,
    _key: key,
    _wx: wp.x,
    _wz: wp.z,
    _wCells: w,
    _dCells: d,
  };
}

export function snapItemWorld(item: EditorItem, wx: number, wz: number): { x: number; z: number } {
  const fp = resolveFootprint(item);
  const pid = itemPrefabId(item);
  if (isSwCornerPivotPrefabId(pid)) {
    const o = stubToVisualOffset(item.localRotationY);
    const snapped = snapPlacement(
      fp.cellsX,
      fp.cellsZ,
      item.localRotationY,
      item.prefabGuid,
      wx + o.x,
      wz + o.z
    );
    return visualCenterToStubXZ(pid, snapped.x, snapped.z, item.localRotationY);
  }
  if (isLargePotPrefabId(pid)) {
    const o = largePotVisualOffsetWorld(item.localRotationY);
    const snapped = snapPlacement(
      fp.cellsX,
      fp.cellsZ,
      item.localRotationY,
      item.prefabGuid,
      wx + o.x,
      wz + o.z
    );
    return largePotUnityFromVisualCenter(snapped.x, snapped.z, item.localRotationY);
  }
  return snapPlacement(fp.cellsX, fp.cellsZ, item.localRotationY, item.prefabGuid, wx, wz);
}

export function refreshUtensilStacks() {
  for (const item of S.items) {
    const cat = S.catalogByGuid.get(item.prefabGuid);
    trySnapUtensilToHost(item, cat, S.items, S.catalogByGuid);
  }
}

export function syncLocalFromWorld(item: EditorItem) {
  const snapped = snapItemWorld(item, item._wx, item._wz);
  item._wx = snapped.x;
  item._wz = snapped.z;
  const cat = S.catalogByGuid.get(item.prefabGuid);
  if (cat?.stack) {
    trySnapUtensilToHost(item, cat, S.items, S.catalogByGuid);
  }
  syncItemLocalFromEditor(item);
}

/** R 键旋转：火锅灶台等保持占地中心不动并重算 stub；其余物品保持原逻辑。 */
export function rotateItemByDelta(item: EditorItem, deltaDeg: number) {
  const pid = itemPrefabId(item);
  if (isSwCornerPivotPrefabId(pid)) {
    const vc = itemVisualCenterXZ(item);
    item.localRotationY = normalizeRot(item.localRotationY + deltaDeg);
    const stub = visualCenterToStubXZ(pid, vc.x, vc.z, item.localRotationY);
    item._wx = stub.x;
    item._wz = stub.z;
    syncItemLocalFromEditor(item);
    return;
  }
  if (isLargePotPrefabId(pid)) {
    const vc = itemVisualCenterXZ(item);
    item.localRotationY = normalizeRot(item.localRotationY + deltaDeg);
    const u = largePotUnityFromVisualCenter(vc.x, vc.z, item.localRotationY);
    item._wx = u.x;
    item._wz = u.z;
    syncItemLocalFromEditor(item);
    return;
  }
  item.localRotationY = normalizeRot(item.localRotationY + deltaDeg);
  syncLocalFromWorld(item);
}

const LOTUS_RANDOM_ROTATIONS = [0, 90, 180, 270];

/** 将场景中全部莲花压力开关随机旋转为 0° / 90° / 180° / 270°（各实例独立随机）。 */
export function batchRandomRotateLotusPressureSwitches(): number {
  const targets = S.items.filter(isLotusPressureSwitchItem);
  if (targets.length === 0) return 0;
  pushHistory();
  for (const item of targets) {
    const angle = LOTUS_RANDOM_ROTATIONS[Math.floor(Math.random() * LOTUS_RANDOM_ROTATIONS.length)];
    const delta = normalizeRot(angle - item.localRotationY);
    if (delta !== 0) rotateItemByDelta(item, delta);
  }
  S.dirty = true;
  draw();
  return targets.length;
}

export function nudgeItem(item: EditorItem, dx: number, dz: number, dy = 0) {
  if (moveBlockedAt(item, item._wx + dx, item._wz + dz)) {
    setStatus("目标位置与玩家重叠，不可放置", false);
    return;
  }
  pushHistory();
  item._wx += dx;
  item._wz += dz;
  syncItemLocalFromEditor(item);
  if (dy !== 0) {
    item.localPosition.y = snapValue(item.localPosition.y + dy, S.freeSnapStep);
  }
  const cat = S.catalogByGuid.get(item.prefabGuid);
  if (cat?.stack) trySnapUtensilToHost(item, cat, S.items, S.catalogByGuid);
  updateCtxCoord(item);
  draw();
}

/** Nudge every selected item by (dx, dz) world units. The caller owns the
 *  history entry; items blocked by the player overlap rule are skipped. */
export function nudgeSelectedItems(dx: number, dz: number): void {
  let blocked = false;
  for (const key of selectionKeys()) {
    const it = S.items.find((i) => i._editorKey === key);
    if (!it) continue;
    if (moveBlockedAt(it, it._wx + dx, it._wz + dz)) {
      blocked = true;
      continue;
    }
    it._wx += dx;
    it._wz += dz;
    syncItemLocalFromEditor(it);
    const cat = S.catalogByGuid.get(it.prefabGuid);
    if (cat?.stack) trySnapUtensilToHost(it, cat, S.items, S.catalogByGuid);
  }
  if (blocked) setStatus("部分目标位置与玩家重叠，已跳过", false);
}

export function updateCtxCoord(item: EditorItem) {
  const el = document.getElementById("ctx-coord");
  if (el) {
    const d = stepDisplayDecimals(S.freeSnapStep);
    el.textContent = `x ${item._wx.toFixed(d)} · y ${item.localPosition.y.toFixed(d)} · z ${item._wz.toFixed(d)}`;
  }
}

export function deleteSelected() {
  const keys = selectionKeys();
  if (keys.length === 0 && !S.selectedKey) return;
  const kill = new Set(keys.length ? keys : S.selectedKey ? [S.selectedKey] : []);
  for (const k of [...kill]) {
    const it = S.items.find((i) => i._editorKey === k);
    if (it && isPlayerItem(it)) kill.delete(k);
  }
  if (kill.size === 0) {
    setStatus("玩家不可删除", false);
    return;
  }
  pushHistory();
  // Remove anim-control group membership for deleted items; drop emptied groups.
  const deletedInstanceIds = new Set(
    S.items.filter((i) => kill.has(i._editorKey)).map((i) => i.instanceId)
  );
  for (const g of S.animControls) {
    if (!deletedInstanceIds.size) break;
    g.itemInstanceIds = g.itemInstanceIds.filter((id) => !deletedInstanceIds.has(id));
    if (g.memberOffsets)
      g.memberOffsets = g.memberOffsets.filter((o) => !deletedInstanceIds.has(o.instanceId));
    if (g.memberStatic)
      g.memberStatic = g.memberStatic.filter((m) => !deletedInstanceIds.has(m.instanceId));
  }
  S.animControls = S.animControls.filter((g) => g.itemInstanceIds.length > 0 || g.floorInstanceIds.length > 0 || g.objectInstanceIds.length > 0);
  // 删除物品时同步清理指向它的开关联动（断头台/饮料机按钮）
  if (deletedInstanceIds.size)
    S.switchLinks = S.switchLinks.filter((l) => !deletedInstanceIds.has(l.switchId) && !deletedInstanceIds.has(l.targetId));
  S.items = S.items.filter((i) => !kill.has(i._editorKey));
  // 同步清理按钮↔动画组联动（须在物品列表更新之后：源按钮被删则联动失效）
  cleanOrphanedButtonLinks();
  // 同步清理按钮↔事件组联动（源按钮/目标物品被删则相应事件失效）
  cleanOrphanedButtonEvents();
  if (S.activeAnimGroupId && !S.animControls.some((g) => g.id === S.activeAnimGroupId)) {
    S.activeAnimGroupId = null;
    S.activeAnimEventIdx = null;
    S.selectedWaypointId = null;
  }
  S.items = S.items.filter((i) => !kill.has(i._editorKey));
  clearSelection();
  hideDetail();
  hideContextMenu();
  draw();
}

export function moveBlockedAt(_item: EditorItem, _wx: number, _wz: number, _ignoreKeys?: Set<string>): boolean {
  return false;
}

export function checkPlayerCollisions(): string[] {
  const result: string[] = [];
  const players = S.items.filter(isPlayerItem);
  const nonPlayerGameplay = S.items.filter(
    (it) =>
      !isPlayerItem(it) &&
      !isCollisionItem(it) &&
      itemLayerOfIt(it) === "items"
  );
  // 精确 AABB 相交（与 checkWorkstationCollisions 一致），而不是网格离散化：
  // 网格化在物体不在格子正中心时会多算相邻格子，造成假重叠。
  const EPS = 0.02;
  for (const p of players) {
    const a = itemWorldAABB(p);
    for (const o of nonPlayerGameplay) {
      const b = itemWorldAABB(o);
      const overlapX = Math.min(a.maxX, b.maxX) - Math.max(a.minX, b.minX);
      const overlapZ = Math.min(a.maxZ, b.maxZ) - Math.max(a.minZ, b.minZ);
      if (overlapX > EPS && overlapZ > EPS) {
        result.push(`${itemLabel(p)} 与 ${itemLabel(o)} 重叠`);
      }
    }
  }
  return result;
}

export function itemWorldAABB(item: EditorItem): { minX: number; minZ: number; maxX: number; maxZ: number } {
  let spanW: number;
  let spanD: number;
  if (isAirWallItem(item)) {
    const c = airWallCells(item);
    spanW = c.wCells * CELL;
    spanD = c.dCells * CELL;
  } else {
    const fp = resolveFootprint(item);
    const rot = normalizeRot(item.localRotationY);
    const swapped = rot === 90 || rot === 270;
    const cw = swapped ? fp.cellsZ : fp.cellsX;
    const cd = swapped ? fp.cellsX : fp.cellsZ;
    spanW = cw * CELL * itemScaleX(item);
    spanD = cd * CELL * itemScaleZ(item);
  }
  const center = itemVisualCenterXZ(item);
  return {
    minX: center.x - spanW / 2,
    minZ: center.z - spanD / 2,
    maxX: center.x + spanW / 2,
    maxZ: center.z + spanD / 2,
  };
}

export function checkWorkstationCollisions(): string[] {
  const result: string[] = [];
  const workstations = S.items.filter(
    (it) =>
      itemLayerOfIt(it) === "items" &&
      isStackHostCatalog(S.catalogByGuid.get(it.prefabGuid))
  );
  const EPS = 0.02;
  for (let i = 0; i < workstations.length; i++) {
    const a = itemWorldAABB(workstations[i]);
    for (let j = i + 1; j < workstations.length; j++) {
      const b = itemWorldAABB(workstations[j]);
      const overlapX = Math.min(a.maxX, b.maxX) - Math.max(a.minX, b.minX);
      const overlapZ = Math.min(a.maxZ, b.maxZ) - Math.max(a.minZ, b.minZ);
      if (overlapX > EPS && overlapZ > EPS) {
        result.push(`${itemLabel(workstations[i])} 与 ${itemLabel(workstations[j])} 重叠`);
      }
    }
  }
  return result;
}

/** Apply a width/depth (in grid cells) to a background plane item: writes the
 *  correct localScale for the prefab's native depth axis and enforces its native
 *  rotX (so e.g. Water_01 lies flat with rotX=90 and depth on localScale.y). */
export function setItemPlaneSize(item: EditorItem, wCells: number, dCells: number): void {
  item.localScale = planeScaleFromCells(item, wCells, dCells);
  item.localRotationX = planeNativeForItem(item).rotX;
}

/** 空气墙碰撞高度（格）：localScale.y，默认 1 格 ≈ 1.2m（Unity 写回为 1.132×y）。 */
export function airWallHeightCells(item: EditorItem): number {
  const y = item.localScale?.y ?? 1;
  return Math.max(1, Math.round(y));
}

export function airWallHeightMeters(hCells: number): number {
  return Math.max(1, Math.round(hCells)) * CELL;
}

export function defaultAirWallColliderCenter(): { x: number; y: number; z: number } {
  return { x: 0, y: AIR_WALL_BASE_Y / 2, z: 0 };
}

export function ensureAirWallColliderCenter(item: EditorItem | LayoutItem): void {
  if (!item.airWall) return;
  if (!item.colliderCenter) {
    item.colliderCenter = defaultAirWallColliderCenter();
  }
}

export function setAirWallHeight(item: EditorItem, hCells: number): void {
  const h = Math.max(1, Math.round(hCells));
  const sx = item.localScale?.x ?? 1;
  const sz = item.localScale?.z ?? 1;
  item.localScale = { x: sx, y: h, z: sz };
  ensureAirWallColliderCenter(item);
}

/** 空气墙有效占地（格）：底 collider 1.2m × localScale，旋转 90° 时宽高互换。 */
export function airWallCells(item: EditorItem): { wCells: number; dCells: number } {
  const rot = normalizeRot(item.localRotationY);
  const swapped = rot === 90 || rot === 270;
  const sx = itemScaleX(item);
  const sz = itemScaleZ(item);
  const wM = (swapped ? sz : sx) * AIR_WALL_BASE_XZ;
  const dM = (swapped ? sx : sz) * AIR_WALL_BASE_XZ;
  return {
    wCells: Math.max(1, Math.round(wM / CELL)),
    dCells: Math.max(1, Math.round(dM / CELL)),
  };
}

export function setAirWallSize(item: EditorItem, wCells: number, dCells: number): void {
  const rot = normalizeRot(item.localRotationY);
  const swapped = rot === 90 || rot === 270;
  const wCellsClamped = Math.max(1, wCells);
  const dCellsClamped = Math.max(1, dCells);
  const y = item.localScale?.y ?? 1;
  item.localScale = swapped
    ? { x: dCellsClamped, y, z: wCellsClamped }
    : { x: wCellsClamped, y, z: dCellsClamped };
}

export function itemIntersectsWorldRect(
  item: EditorItem,
  minWx: number,
  maxWx: number,
  minWz: number,
  maxWz: number
): boolean {
  const a = itemWorldAABB(item);
  return a.maxX >= minWx && a.minX <= maxWx && a.maxZ >= minWz && a.minZ <= maxWz;
}

/** Resize a background/water plane item by dragging a corner handle. Mirrors
 *  dragFloor's resize math: the opposite corner (anchor) stays fixed while the
 *  dragged corner sets a new width/depth in cells, applied as native-aware
 *  localScale (see setItemPlaneSize). */
function resizeItemByCells(
  item: EditorItem,
  wx: number,
  wz: number,
  applySize: (it: EditorItem, w: number, d: number) => void
): void {
  const rad = (normalizeRot(item.localRotationY) * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  const dx = wx - S.dragItemResizeAnchorX;
  const dz = wz - S.dragItemResizeAnchorZ;
  const lx = dx * cos + dz * sin;
  const lz = -dx * sin + dz * cos;
  const newW = Math.max(1, Math.round(Math.abs(lx) / CELL) || 1);
  const newD = Math.max(1, Math.round(Math.abs(lz) / CELL) || 1);
  const signX = S.dragItemResizeEdge.includes("R") ? 1 : -1;
  const signZ = S.dragItemResizeEdge.includes("T") ? 1 : -1;
  const newCxLocal = (signX * (newW * CELL)) / 2;
  const newCzLocal = (signZ * (newD * CELL)) / 2;
  item._wx = S.dragItemResizeAnchorX + newCxLocal * cos - newCzLocal * sin;
  item._wz = S.dragItemResizeAnchorZ + newCxLocal * sin + newCzLocal * cos;
  applySize(item, newW, newD);
  syncItemLocalFromEditor(item);
  if (item.worldPosition) {
    const u = editorItemUnityWorldXZ(item);
    item.worldPosition.x = u.x;
    item.worldPosition.z = u.z;
  }
}

export function resizeItem(item: EditorItem, wx: number, wz: number): void {
  resizeItemByCells(item, wx, wz, setItemPlaneSize);
}

export function resizeAirWall(item: EditorItem, wx: number, wz: number): void {
  resizeItemByCells(item, wx, wz, setAirWallSize);
}

export function addFromCatalog(
  cat: CatalogItem,
  wx: number,
  wz: number,
  recordHistory = true,
  silent = false
): EditorItem | null {
  if (cat.id === "Player") {
    setStatus("玩家固定在场景中，不可添加", false);
    return null;
  }
  const snapped = snapPlacement(cat.footprint.cellsX, cat.footprint.cellsZ, 0, cat.guid, wx, wz);
  const stub = isLargePotPrefabId(cat.id)
    ? largePotUnityFromVisualCenter(snapped.x, snapped.z, 0)
    : visualCenterToStubXZ(cat.id, snapped.x, snapped.z, 0);
  const probe = {
    _editorKey: "",
    _wx: stub.x,
    _wz: stub.z,
    prefabGuid: cat.guid,
    prefabAssetPath: cat.assetPath,
    footprint: cat.footprint,
    stubKind: "",
  } as unknown as EditorItem;
  if (moveBlockedAt(probe, stub.x, stub.z)) {
    setStatus("该位置与玩家重叠，无法放置", false);
    return null;
  }
  if (recordHistory) pushHistory();
  const id = `new:${cat.guid}:${uuid()}`;
  const editorKey = newEditorKey();
  // 落在抬高的地板（高台）上时，物品初始 Y = 该处行走面高度，否则会埋进高台里。
  // 高度过滤激活时：只取当前区间内的地板；该点无可用地板则落在区间底（当前层）。
  const filterOn = floorHeightFilterActive();
  let baseY = floorHeightAt(snapped.x, snapped.z, filterOn);
  if (baseY < 0) baseY = filterOn && S.floorHeight.min != null ? S.floorHeight.min + 0.01 : 0;
  // Web 火锅家族（commonW1 web/hotpot/）默认高度统一 0.1（2026-09-03 用户实测：
  // 静态锅/可移动锅统一贴地基准；灶台（burner）是地面件保持 0）。
  if (cat.id === "web_utensil_large_pot_01" ||
      cat.id === "web_utensil_dlc10_large_pot_01" ||
      cat.id === "web_utensil_large_pot_01_pushable" ||
      cat.id === "web_dlc10_pushable_object")
    baseY = 0.1;
  const item: EditorItem = {
    instanceId: id,
    _editorKey: editorKey,
    hierarchyPath: id,
    prefabGuid: cat.guid,
    prefabAssetPath: cat.assetPath,
    parentPath: cat.defaultParent,
    displayName: cat.id,
    localPosition: { x: 0, y: baseY, z: 0 },
    worldPosition: { x: 0, y: baseY, z: 0 },
    localRotationY: 0,
    footprint: cat.footprint,
    _wx: stub.x,
    _wz: stub.z,
    _parentWx: 0,
    _parentWz: 0,
  };
  syncItemLocalFromEditor(item);
  item.localPosition.y = baseY;
  const u = editorItemUnityWorldXZ(item);
  item.worldPosition = {
    x: u.x,
    y: baseY,
    z: u.z,
  };
  if (cat.id === "Dispenser") {
    item.stubKind = "Dispenser";
    item.dispenser = {};
  }
  if (cat.id === "AttachingFoodSpawner") {
    item.stubKind = "AttachingFoodSpawner";
    item.foodSpawner = {
      spawnInOrder: true,
      triggerAtStart: true,
      triggerTime: 5,
      attachmentPrefabGuids: [],
      weights: [],
    };
  }
  if (cat.id === "ConveyorStation") {
    item.stubKind = "Conveyor";
    item.conveyor = { conveySpeed: 0.5 };
  }
  if (cat.id === "Teleportal") {
    item.stubKind = "Teleportal";
    item.teleportal = { exitPortalInstanceId: "", portalColor: 0, doubleSided: false };
  }
  if (cat.id === "AirWall") {
    // 空气墙：核心层隐形碰撞块（1.2×1.2×1.132 BoxCollider × localScale 格数倍率）。
    // prefabGuid 保留 catalog guid 供分层识别；应用走 stubKind=Collision 分支，不使用 prefab。
    item.stubKind = "Collision";
    item.airWall = true;
    item.prefabAssetPath = "";
    item.parentPath = cat.defaultParent;
    item.walkable = false;
    item.localScale = { x: 1, y: 1, z: 1 };
    item.colliderCenter = defaultAirWallColliderCenter();
  }
  // Background surface planes (water / sand / sea…) default to a manageable 6×6
  // and are laid flat via their native rotX (standing water quads need rotX=90,
  // with depth on localScale.y) so they don't spawn 1×1 or as a vertical strip.
  if (isBackgroundPlaneCat(cat)) {
    setItemPlaneSize(item, 6, 6);
    item.footprint = planeCatalogFootprint(item);
  }
  S.items.push(item);
  trySnapUtensilToHost(item, cat, S.items, S.catalogByGuid);
  if (!silent) {
    setSelection([editorKey]);
    draw();
  }
  warnItemVoid(item);
  return item;
}

/** 批量放置网格偏移（以落点为中心，按 footprint 步进排布）。 */
export function batchPlaceGridOffsets(
  count: number,
  cellsX: number,
  cellsZ: number
): Array<{ dx: number; dz: number }> {
  const cols = Math.ceil(Math.sqrt(count));
  const rows = Math.ceil(count / cols);
  const stepX = Math.max(1, cellsX) * CELL;
  const stepZ = Math.max(1, cellsZ) * CELL;
  const out: Array<{ dx: number; dz: number }> = [];
  let n = 0;
  for (let r = 0; r < rows && n < count; r++) {
    for (let c = 0; c < cols && n < count; c++) {
      out.push({
        dx: (c - (cols - 1) / 2) * stepX,
        dz: (r - (rows - 1) / 2) * stepZ,
      });
      n++;
    }
  }
  return out;
}

/** 在落点为中心网格放置 count 个相同 catalog 物品（单次撤销）。 */
export function addFromCatalogBatch(
  cat: CatalogItem,
  wx: number,
  wz: number,
  count: number
): EditorItem[] {
  const n = Math.max(1, Math.min(99, Math.round(count)));
  if (n <= 1) {
    const one = addFromCatalog(cat, wx, wz, true);
    return one ? [one] : [];
  }
  pushHistory();
  const offsets = batchPlaceGridOffsets(n, cat.footprint.cellsX, cat.footprint.cellsZ);
  const placed: EditorItem[] = [];
  let skipped = 0;
  for (const off of offsets) {
    const it = addFromCatalog(cat, wx + off.dx, wz + off.dz, false, true);
    if (it) placed.push(it);
    else skipped++;
  }
  if (placed.length) {
    setSelection(placed.map((i) => i._editorKey));
    S.dirty = true;
    draw();
    const label = cat.nameZh || cat.id;
    setStatus(
      skipped
        ? `已放置 ${placed.length}/${n} 个「${label}」（${skipped} 个因重叠跳过）`
        : `已放置 ${placed.length} 个「${label}」`,
      skipped === 0
    );
  } else {
    setStatus("无法放置：与玩家重叠或位置无效", false);
  }
  return placed;
}

export function placementBase(): { x: number; z: number } {
  // 以可行走地面区域的中心为填充起点。此前取全部物品包围盒的 min 角，
  // 背景/装饰等偏远物会把自动填充行拖到 30-40 格外。
  if (S.walkable.length === 0) return { x: 0, z: 0 };
  let minX = Infinity;
  let maxX = -Infinity;
  let minZ = Infinity;
  let maxZ = -Infinity;
  for (const r of S.walkable) {
    minX = Math.min(minX, r.cx - r.sx / 2);
    maxX = Math.max(maxX, r.cx + r.sx / 2);
    minZ = Math.min(minZ, r.cz - r.sz / 2);
    maxZ = Math.max(maxZ, r.cz + r.sz / 2);
  }
  if (!isFinite(minX)) return { x: 0, z: 0 };
  return { x: (minX + maxX) / 2, z: (minZ + maxZ) / 2 };
}

export function itemVoidWarning(item: EditorItem): string | null {
  if (S.walkable.length === 0) return null;
  if (pointInWalkable(item._wx, item._wz)) return null;
  const name = itemLabel(item);
  switch (S.deathInfo?.deathType) {
    case "water":
      return `⚠「${name}」位于水面/空洞上方（玩家会落水），已保留但请确认`;
    case "goo":
      return `⚠「${name}」位于黏液/空洞上方（玩家会坠入），已保留但请确认`;
    default:
      return `⚠「${name}」位于空洞上方（会坠落），已保留但请确认`;
  }
}

export function warnItemVoid(item: EditorItem) {
  const w = itemVoidWarning(item);
  if (w) setStatus(w, false);
}
