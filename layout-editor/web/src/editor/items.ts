import {
  snapPlacement,
  resolveFootprint,
  normalizeRot,
  itemScaleX,
  itemScaleZ,
  newEditorKey,
  stepDisplayDecimals
} from "./coords";
import {
  S,
  CELL,
  EditorItem,
  EditorFloor
} from "./state";
import { itemLayerOfIt } from "./catalog";
import { isCollisionItem } from "./stubControls";
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
import { pointInWalkable } from "./floors";
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

export function enrichItem(raw: LayoutItem, editorKey: string): EditorItem {
  const wp = raw.worldPosition ?? raw.localPosition;
  const fp = resolveFootprint(raw);
  return {
    ...raw,
    _editorKey: editorKey,
    footprint: fp,
    _wx: wp.x,
    _wz: wp.z,
    _parentWx: wp.x - raw.localPosition.x,
    _parentWz: wp.z - raw.localPosition.z,
  };
}

export function enrichFloor(raw: FloorObject, key: string): EditorFloor {
  const wp = raw.worldPosition ?? raw.localPosition;
  const w = raw.widthCells > 0 ? raw.widthCells : Math.max(1, Math.round(raw.widthUnits / CELL));
  const d = raw.depthCells > 0 ? raw.depthCells : Math.max(1, Math.round(raw.depthUnits / CELL));
  return {
    ...raw,
    _key: key,
    _wx: wp.x,
    _wz: wp.z,
    _wCells: w,
    _dCells: d,
  };
}

export function snapItemWorld(item: EditorItem, wx: number, wz: number): { x: number; z: number } {
  const fp = resolveFootprint(item);
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
  item.localPosition.x = snapValue(item._wx - item._parentWx, S.freeSnapStep);
  item.localPosition.z = snapValue(item._wz - item._parentWz, S.freeSnapStep);
}

export function nudgeItem(item: EditorItem, dx: number, dz: number, dy = 0) {
  if (moveBlockedAt(item, item._wx + dx, item._wz + dz)) {
    setStatus("目标位置与玩家重叠，不可放置", false);
    return;
  }
  pushHistory();
  item._wx += dx;
  item._wz += dz;
  item.localPosition.x = item._wx - item._parentWx;
  item.localPosition.z = item._wz - item._parentWz;
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
    it.localPosition.x = it._wx - it._parentWx;
    it.localPosition.z = it._wz - it._parentWz;
    const cat = S.catalogByGuid.get(it.prefabGuid);
    if (cat?.stack) trySnapUtensilToHost(it, cat, S.items, S.catalogByGuid);
  }
  if (blocked) setStatus("部分目标位置与玩家重叠，已跳过", false);
}

export function updateCtxCoord(item: EditorItem) {
  const el = document.getElementById("ctx-coord");
  if (el) {
    const d = stepDisplayDecimals(S.freeSnapStep);
    el.textContent = `x ${item.localPosition.x.toFixed(d)} · y ${item.localPosition.y.toFixed(d)} · z ${item.localPosition.z.toFixed(d)}`;
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
  // Remove move-control group membership for deleted items; drop emptied groups.
  const deletedInstanceIds = new Set(
    S.items.filter((i) => kill.has(i._editorKey)).map((i) => i.instanceId)
  );
  for (const g of S.moveControls) {
    if (!deletedInstanceIds.size) break;
    g.itemInstanceIds = g.itemInstanceIds.filter((id) => !deletedInstanceIds.has(id));
    if (g.memberOffsets)
      g.memberOffsets = g.memberOffsets.filter((o) => !deletedInstanceIds.has(o.instanceId));
    if (g.memberStatic)
      g.memberStatic = g.memberStatic.filter((m) => !deletedInstanceIds.has(m.instanceId));
  }
  S.moveControls = S.moveControls.filter((g) => g.itemInstanceIds.length > 0 || g.floorInstanceIds.length > 0 || g.objectInstanceIds.length > 0);
  // 删除物品时同步清理指向它的开关联动（断头台/饮料机按钮）
  if (deletedInstanceIds.size)
    S.switchLinks = S.switchLinks.filter((l) => !deletedInstanceIds.has(l.switchId) && !deletedInstanceIds.has(l.targetId));
  S.items = S.items.filter((i) => !kill.has(i._editorKey));
  // 同步清理按钮↔移动组联动（须在物品列表更新之后：源按钮被删则联动失效）
  cleanOrphanedButtonLinks();
  // 同步清理按钮↔事件组联动（源按钮/目标物品被删则相应事件失效）
  cleanOrphanedButtonEvents();
  if (S.activeMoveGroupId && !S.moveControls.some((g) => g.id === S.activeMoveGroupId)) {
    S.activeMoveGroupId = null;
    S.activeMoveEventIdx = null;
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
  const fp = resolveFootprint(item);
  const rot = normalizeRot(item.localRotationY);
  const swapped = rot === 90 || rot === 270;
  const cw = swapped ? fp.cellsZ : fp.cellsX;
  const cd = swapped ? fp.cellsX : fp.cellsZ;
  const spanW = cw * CELL * itemScaleX(item);
  const spanD = cd * CELL * itemScaleZ(item);
  return {
    minX: item._wx - spanW / 2,
    minZ: item._wz - spanD / 2,
    maxX: item._wx + spanW / 2,
    maxZ: item._wz + spanD / 2,
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

export function addFromCatalog(cat: CatalogItem, wx: number, wz: number, recordHistory = true): EditorItem | null {
  if (cat.id === "Player") {
    setStatus("玩家固定在场景中，不可添加", false);
    return null;
  }
  const snapped = snapPlacement(cat.footprint.cellsX, cat.footprint.cellsZ, 0, cat.guid, wx, wz);
  const probe = {
    _editorKey: "",
    _wx: snapped.x,
    _wz: snapped.z,
    prefabGuid: cat.guid,
    prefabAssetPath: cat.assetPath,
    footprint: cat.footprint,
    stubKind: "",
  } as unknown as EditorItem;
  if (moveBlockedAt(probe, snapped.x, snapped.z)) {
    setStatus("该位置与玩家重叠，无法放置", false);
    return null;
  }
  if (recordHistory) pushHistory();
  const id = `new:${cat.guid}:${crypto.randomUUID()}`;
  const editorKey = newEditorKey();
  const item: EditorItem = {
    instanceId: id,
    _editorKey: editorKey,
    hierarchyPath: id,
    prefabGuid: cat.guid,
    prefabAssetPath: cat.assetPath,
    parentPath: cat.defaultParent,
    displayName: cat.id,
    localPosition: { x: snapped.x, y: 0, z: snapped.z },
    worldPosition: { x: snapped.x, y: 0, z: snapped.z },
    localRotationY: 0,
    footprint: cat.footprint,
    _wx: snapped.x,
    _wz: snapped.z,
    _parentWx: 0,
    _parentWz: 0,
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
    // 空气墙：核心层隐形碰撞块（无 prefab，场景应用时生成 1×1×1.132 BoxCollider 空气墙）。
    // prefabGuid 保留 catalog guid 供分层识别；应用走 stubKind=Collision 分支，不使用 prefab。
    item.stubKind = "Collision";
    item.airWall = true;
    item.prefabAssetPath = "";
    item.parentPath = cat.defaultParent;
    item.walkable = false;
  }
  S.items.push(item);
  trySnapUtensilToHost(item, cat, S.items, S.catalogByGuid);
  setSelection([editorKey]);
  draw();
  warnItemVoid(item);
  return item;
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
