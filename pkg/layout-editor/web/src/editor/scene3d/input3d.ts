/**
 * 3D 视口输入层：与 2D 的 input.ts 语义对等。
 *
 * 鼠标分工（在保留 2D 肌肉记忆的前提下塞进相机操作）：
 *   左键空白拖拽 = 框选          左键物体拖拽 = 移动（默认 XZ 平面）
 *   右键拖拽     = 环绕旋转视角   右键点击（未拖动）= 2D 同款右键菜单
 *   中键拖拽     = 平移视角       空格/Alt + 左键 = 平移视角
 *   滚轮         = 缩放
 *
 * 所有状态变更一律走既有 ops（pushHistory → 改 S → draw），不新增历史路径。
 */

import * as THREE from "three";
import { S, CELL, EditorItem } from "../state";
import { draw } from "../render";
import { snapshotState, pushHistory, commitDragSnapshot } from "../historyOps";
import { isSelected, setSelection, clearSelection, setFloorSelection, clearFloorSelection, selectionKeys } from "../selection";
import { syncLocalFromWorld, warnItemVoid, moveBlockedAt, addFromCatalog, addFromCatalogBatch } from "../items";
import { dragFloorWorld, finalizeFloor, maybeSyncMaterialTilingOnResize, addFloorAt, addAirFloorAt } from "../floors";
import { setItemPlaneSize, setAirWallSize, setAirWallHeight, airWallHeightCells } from "../items";
import { isAirWallItem } from "../stubControls";
import { isResizableBackgroundItem, itemCategoryOf } from "../catalog";
import { showContextMenu, showWaypointContextMenu, showBatchHeightMenu } from "../ui/contextMenu";
import { hideDetail, hideContextMenu } from "../ui/overlay";
import { applyFloorWalkHeight } from "../selectionHeight";
import { batchTransformCount } from "../selectionTransform";
import { activeGroup, updateAnimPickBar } from "../animControl";
import { renderRightPanel } from "../panels";
import { snapValue } from "../../snap";
import { uuid } from "../coords";
import { comboById, addCombo } from "../combos";
import { palettePlaceCountFor } from "../palette";
import { Scene3DCtx, itemByKey, floorByKey } from "./ctx";
import { pickAll, pickItems, pickFloors, pickWaypoints, pickHandle, rayOnPlane, rayVerticalY } from "./picking";
import { toSceneZ } from "./space";
import { itemBoxOf } from "./meshItems";
import { floorSlabOf, gapBelowFor } from "./meshFloors";
import { cornerAnchorForFloor, CornerEdge } from "./handles3d";
import { marqueeBox, setMarqueeRect, clearMarqueeRect } from "./marquee3d";

type DragKind =
  | "none"
  | "camera-orbit"
  | "camera-pan"
  | "marquee"
  | "item-move"
  | "item-y"
  | "floor-move"
  | "floor-y"
  | "floor-resize"
  | "item-resize"
  | "item-height"
  | "waypoint";

interface DragState {
  kind: DragKind;
  startX: number;
  startY: number;
  lastX: number;
  lastY: number;
  moved: boolean;
  /** 拖动锚点：指针世界坐标与对象世界坐标之差。 */
  offX: number;
  offZ: number;
  lastWx: number;
  lastWz: number;
  /** Y 轴拖动起点。 */
  startValue: number;
  startPointerY: number;
  /** 多选 Y 轴拖动：每个 key 的起始高度。必须快照，否则每帧叠加 dy 会越飘越远。 */
  startYByKey: Map<string, number>;
  key: string;
  groupKeys: string[];
  edge: CornerEdge | null;
  shift: boolean;
}

const EMPTY_DRAG: DragState = {
  kind: "none",
  startX: 0,
  startY: 0,
  lastX: 0,
  lastY: 0,
  moved: false,
  offX: 0,
  offZ: 0,
  lastWx: 0,
  lastWz: 0,
  startValue: 0,
  startPointerY: 0,
  startYByKey: new Map<string, number>(),
  key: "",
  groupKeys: [],
  edge: null,
  shift: false,
};

let drag: DragState = { ...EMPTY_DRAG, startYByKey: new Map() };
let detach: (() => void) | null = null;

const MOVE_THRESHOLD = 4;

/** mousedown 的真实处理（被 onDown 的 try/catch 包着，异常不再静默丢失）。 */
function handleDown(ctx: Scene3DCtx, canvas: HTMLCanvasElement, e: MouseEvent): void {
  drag = freshDrag({ startX: e.clientX, startY: e.clientY, lastX: e.clientX, lastY: e.clientY, shift: e.shiftKey });

  // 中键 / 空格 / Alt = 平移视角；右键 = 环绕（松开若未拖动则弹右键菜单）。
  if (e.button === 1 || (e.button === 0 && (S.spaceHeld || e.altKey))) {
    drag.kind = "camera-pan";
    e.preventDefault();
    return;
  }
  if (e.button === 2) {
    drag.kind = "camera-orbit";
    return;
  }
  if (e.button !== 0) return;

  // 拖动过程可能移出画布，捕获指针保证还能收到 move/up。
  const withId = e as MouseEvent & { pointerId?: number };
  if (withId.pointerId != null && canvas.setPointerCapture) {
    try {
      canvas.setPointerCapture(withId.pointerId);
    } catch {
      /* 某些浏览器在非 pointer 事件下会抛，忽略即可 */
    }
  }

  // 1) 操作柄优先（角柄 / Y 轴柄 / 空气墙顶柄）。
  const handle = pickHandle(ctx, e.clientX, e.clientY);
  if (handle && handle.pick.handle) {
    if (beginHandleDrag(ctx, handle.pick.handle, e)) return;
  }

  // 2) 地板层「点放」模式（+新增地板 / +空气地板）。
  if (beginPendingFloorPlace(ctx, e)) return;

  // 3) 动画层：路点拾取 / 放置。
  if (S.currentLayer === "anim" && beginAnimInteraction(ctx, e)) return;

  // 4) 地板/背景层：地板与表面物件。
  if (S.currentLayer === "floor" || S.currentLayer === "background") {
    if (beginFloorInteraction(ctx, e)) return;
  }

  // 5) 普通物件。
  if (beginItemInteraction(ctx, e)) return;

  // 6) 空白：框选。
  drag.kind = "marquee";
  if (!e.shiftKey) {
    clearSelection();
    clearFloorSelection();
    hideDetail();
    hideContextMenu();
  }
  setMarqueeRect(e.clientX, e.clientY, e.clientX, e.clientY);
  draw();
}

/** 控制台自检：3D 视口没反应时，在浏览器里跑 __scene3dDiag() 一眼定位。 */
export function installDiag(ctx: Scene3DCtx): void {
  (window as unknown as { __scene3dDiag?: () => unknown }).__scene3dDiag = () => {
    const rect = ctx.canvas.getBoundingClientRect();
    const info = {
      视图模式: S.viewMode,
      当前图层: S.currentLayer,
      画布尺寸: { w: Math.round(rect.width), h: Math.round(rect.height) },
      画布可见: rect.width > 0 && rect.height > 0,
      输入已挂载: detach != null,
      场景物件数: ctx.items.size,
      场景地板数: ctx.floors.size,
      选中物件: S.selectedKeys.size,
      选中地板: S.selectedFloorKeys.size,
      空格平移卡住: S.spaceHeld,
      Y轴模式: S.yAxisDrag,
      指针世界坐标: { x: S.hoverWx, z: S.hoverWz },
      中心命中: pickAll(ctx, rect.left + rect.width / 2, rect.top + rect.height / 2).map((r) => r.pick.kind + ":" + r.pick.key),
    };
    console.table(info);
    return info;
  };
}

/** 每次起手都要新建 Map，避免多次拖动共用同一个引用。 */
function freshDrag(patch: Partial<DragState>): DragState {
  return { ...EMPTY_DRAG, startYByKey: new Map<string, number>(), ...patch };
}

/** 记录一组 key 的当前高度，供 Y 轴拖动作为基准。 */
function snapshotStartY(keys: string[]): void {
  drag.startYByKey.clear();
  for (const k of keys) {
    const it = itemByKey(k);
    if (it) drag.startYByKey.set(k, it.localPosition?.y ?? 0);
  }
}

export function attachInput3D(ctx: Scene3DCtx): void {
  detachInput3D();
  const canvas = ctx.canvas;

  const onWheel = (e: WheelEvent) => {
    e.preventDefault();
    ctx.cam.zoomAt(e.deltaY, zoomPivot(ctx, e.clientX, e.clientY));
    ctx.invalidate();
  };

  const onDown = (e: MouseEvent) => {
    try {
      handleDown(ctx, canvas, e);
    } catch (err) {
      // 早期版本里 onDown 抛异常会被浏览器静默吞掉，表现为「左键完全没反应」。
      // 出声报错，至少能一眼定位。
      console.error("[scene3d] pointerdown 处理失败:", err);
      drag = freshDrag({});
    }
  };

  const onMove = (e: MouseEvent) => {
    const dx = e.clientX - drag.lastX;
    const dy = e.clientY - drag.lastY;
    drag.lastX = e.clientX;
    drag.lastY = e.clientY;
    if (Math.abs(e.clientX - drag.startX) > MOVE_THRESHOLD || Math.abs(e.clientY - drag.startY) > MOVE_THRESHOLD) {
      drag.moved = true;
    }

    // hover 世界坐标：喂给 S.hoverWx/Wz，粘贴落点与坐标读出共用。
    const ground = rayOnPlane(ctx, e.clientX, e.clientY, currentGroundY());
    if (ground) {
      S.hoverWx = ground.x;
      S.hoverWz = ground.z;
    }

    switch (drag.kind) {
      case "camera-orbit":
        ctx.cam.orbit(dx, dy);
        ctx.invalidate();
        return;
      case "camera-pan":
        ctx.cam.pan(dx, dy, canvas.clientHeight);
        ctx.invalidate();
        return;
      case "marquee":
        setMarqueeRect(drag.startX, drag.startY, e.clientX, e.clientY);
        marqueeBox(ctx, drag.startX, drag.startY, e.clientX, e.clientY, drag.shift);
        draw();
        return;
      case "item-move":
        dragItems(ctx, e);
        return;
      case "item-y":
        dragItemY(ctx, e);
        return;
      case "floor-move":
        dragFloorMove(ctx, e);
        return;
      case "floor-y":
        dragFloorY(ctx, e);
        return;
      case "floor-resize":
        dragFloorResize(ctx, e);
        return;
      case "item-resize":
        dragItemResize(ctx, e);
        return;
      case "item-height":
        dragItemHeight(ctx, e);
        return;
      case "waypoint":
        dragWaypoint(ctx, e);
        return;
      default:
        return;
    }
  };

  const onUp = (e: MouseEvent) => {
    const kind = drag.kind;
    const moved = drag.moved;

    if (kind === "camera-orbit" && !moved) {
      // 右键未拖动 = 2D 同款右键菜单。
      openContextMenu3D(ctx, e);
    }

    if (kind === "marquee") {
      clearMarqueeRect();
      const keys = selectionKeys();
      S.selectedKey = keys.length ? keys[keys.length - 1] : null;
      // 动画路点模式：空白单击 = 放置路点（与 2D input.ts:1157 一致）。
      if (!moved && S.currentLayer === "anim" && S.animMode === "waypoints") placeWaypoint(ctx, e);
      updateAnimPickBar();
      renderRightPanel();
    }

    if (kind === "item-move" || kind === "item-y") {
      const keys = drag.groupKeys.length ? drag.groupKeys : [drag.key];
      for (const k of keys) {
        const it = itemByKey(k);
        if (it) {
          syncLocalFromWorld(it);
          warnItemVoid(it);
        }
      }
      commitDragSnapshot();
    }
    if (kind === "item-resize" || kind === "item-height") {
      const it = itemByKey(drag.key);
      if (it) {
        syncLocalFromWorld(it);
        warnItemVoid(it);
      }
      commitDragSnapshot();
    }
    if (kind === "floor-move" || kind === "floor-resize" || kind === "floor-y") {
      const f = floorByKey(drag.key);
      if (f) {
        if (kind === "floor-resize") {
          maybeSyncMaterialTilingOnResize(f, S.dragFloorResizePrevW, S.dragFloorResizePrevD);
        }
        finalizeFloor(f);
      }
      S.dragFloorKey = null;
      S.dragFloorMode = "move";
      commitDragSnapshot();
    }
    if (kind === "waypoint") {
      S.draggingWaypointId = null;
      S.dirty = true;
      commitDragSnapshot();
    }

    drag = freshDrag({});
    draw();
  };

  const onContextMenu = (e: MouseEvent) => {
    // 右键菜单由 mouseup 决定（要区分「点击」与「拖动旋转视角」）。
    e.preventDefault();
  };

  const onDragOver = (e: DragEvent) => {
    e.preventDefault();
  };

  const onDrop = (e: DragEvent) => {
    e.preventDefault();
    const hit = rayOnPlane(ctx, e.clientX, e.clientY, currentGroundY());
    if (!hit) return;
    const guid = e.dataTransfer?.getData("text/plain");
    if (guid && guid.startsWith("combo:")) {
      const def = comboById(guid.substring("combo:".length));
      if (def) addCombo(def, hit.x, hit.z);
      return;
    }
    const cat = guid ? S.catalogByGuid.get(guid) : S.dragCatalog;
    if (!cat) {
      if (S.dragCombo) addCombo(S.dragCombo, hit.x, hit.z);
      return;
    }
    const batch = S.dragCatalogBatch > 1 ? S.dragCatalogBatch : palettePlaceCountFor(cat.guid);
    if (batch > 1) addFromCatalogBatch(cat, hit.x, hit.z, batch);
    else addFromCatalog(cat, hit.x, hit.z);
  };

  canvas.addEventListener("wheel", onWheel, { passive: false });
  canvas.addEventListener("mousedown", onDown);
  window.addEventListener("mousemove", onMove);
  window.addEventListener("mouseup", onUp);
  canvas.addEventListener("contextmenu", onContextMenu);
  canvas.addEventListener("dragover", onDragOver);
  canvas.addEventListener("drop", onDrop);
  installDiag(ctx);

  detach = () => {
    canvas.removeEventListener("wheel", onWheel);
    canvas.removeEventListener("mousedown", onDown);
    window.removeEventListener("mousemove", onMove);
    window.removeEventListener("mouseup", onUp);
    canvas.removeEventListener("contextmenu", onContextMenu);
    canvas.removeEventListener("dragover", onDragOver);
    canvas.removeEventListener("drop", onDrop);
  };
}

export function detachInput3D(): void {
  if (detach) detach();
  detach = null;
  drag = freshDrag({});
}

/** 当前"地面"高度：高度过滤激活时用区间下沿，否则 0。 */
function currentGroundY(): number {
  return S.floorHeight.min != null ? S.floorHeight.min : 0;
}

/**
 * 滚轮缩放的锚点（three 场景坐标）：优先鼠标指到的实际几何命中点，
 * 指在空处则退回地面平面交点，都解不出就返回 null（退化为绕注视点缩放）。
 */
function zoomPivot(ctx: Scene3DCtx, clientX: number, clientY: number): THREE.Vector3 | null {
  const hits = pickAll(ctx, clientX, clientY);
  if (hits.length) return hits[0].point.clone();
  const ground = rayOnPlane(ctx, clientX, clientY, currentGroundY());
  if (!ground) return null;
  return new THREE.Vector3(ground.x, currentGroundY(), toSceneZ(ground.z));
}

// ───────────────────────── 交互起手式 ─────────────────────────

function beginHandleDrag(
  ctx: Scene3DCtx,
  handle: NonNullable<import("./ctx").Pickable["handle"]>,
  e: MouseEvent
): boolean {
  drag.key = handle.ownerKey;
  S.dragSnapshot = snapshotState();

  if (handle.owner === "floor") {
    const f = floorByKey(handle.ownerKey);
    if (!f) return false;
    if (handle.edge === "y") {
      drag.kind = "floor-y";
      drag.startValue = floorSlabOf(f, gapBelowFor(f)).topY;
      drag.startPointerY = rayVerticalY(ctx, e.clientX, e.clientY, { x: f._wx, z: f._wz }) ?? 0;
      return true;
    }
    const edge = edgeCodeFromPick(handle.edge);
    if (!edge) return false;
    const anchor = cornerAnchorForFloor(f, edge);
    S.dragFloorKey = f._key;
    S.dragFloorMode = "resize";
    S.dragFloorEdge = edge;
    S.dragFloorAnchorX = anchor.x;
    S.dragFloorAnchorZ = anchor.z;
    S.dragFloorResizePrevW = f._wCells;
    S.dragFloorResizePrevD = f._dCells;
    setFloorSelection([f._key]);
    drag.kind = "floor-resize";
    drag.edge = edge;
    return true;
  }

  const it = itemByKey(handle.ownerKey);
  if (!it) return false;
  if (handle.edge === "y") {
    drag.kind = "item-y";
    drag.groupKeys = S.selectedKeys.size > 1 ? selectionKeys() : [];
    drag.startValue = it.localPosition?.y ?? 0;
    drag.startPointerY = rayVerticalY(ctx, e.clientX, e.clientY, { x: it._wx, z: it._wz }) ?? 0;
    snapshotStartY(drag.groupKeys.length ? drag.groupKeys : [it._editorKey]);
    return true;
  }
  if (handle.edge === "top") {
    drag.kind = "item-height";
    drag.startValue = airWallHeightCells(it);
    drag.startPointerY = rayVerticalY(ctx, e.clientX, e.clientY, { x: it._wx, z: it._wz }) ?? 0;
    return true;
  }
  const edge = edgeCodeFromPick(handle.edge);
  if (!edge) return false;
  drag.kind = "item-resize";
  drag.edge = edge;
  setSelection([it._editorKey]);
  return true;
}

function edgeCodeFromPick(edge: string): CornerEdge | null {
  switch (edge) {
    case "ne":
      return "RT";
    case "se":
      return "RB";
    case "nw":
      return "LT";
    case "sw":
      return "LB";
    default:
      return null;
  }
}

function beginPendingFloorPlace(ctx: Scene3DCtx, e: MouseEvent): boolean {
  if (S.currentLayer !== "floor" && S.currentLayer !== "background") return false;
  if (!S.pendingNewFloor && !S.pendingNewAirFloor) return false;
  const hit = rayOnPlane(ctx, e.clientX, e.clientY, currentGroundY());
  if (!hit) return false;
  if (S.pendingNewAirFloor) {
    S.pendingNewAirFloor = false;
    addAirFloorAt(hit.x, hit.z);
  } else {
    const cat = S.pendingNewFloorCat;
    S.pendingNewFloor = false;
    S.pendingNewFloorCat = null;
    addFloorAt(hit.x, hit.z, cat);
  }
  drag.kind = "none";
  return true;
}

function beginAnimInteraction(ctx: Scene3DCtx, e: MouseEvent): boolean {
  const wps = pickWaypoints(ctx, e.clientX, e.clientY);
  if (wps.length) {
    S.selectedWaypointId = wps[0];
    S.draggingWaypointId = wps[0];
    S.dragSnapshot = snapshotState();
    drag.kind = "waypoint";
    drag.key = wps[0];
    draw();
    return true;
  }
  // 成员模式：允许选中物件/地板加入分组。
  if (S.animMode === "members") return beginItemInteraction(ctx, e) || beginFloorInteraction(ctx, e);
  return false;
}

function beginFloorInteraction(ctx: Scene3DCtx, e: MouseEvent): boolean {
  const floors = pickFloors(ctx, e.clientX, e.clientY);
  const items = pickItems(ctx, e.clientX, e.clientY);
  // 地板层只接受表面物件（普通物件在该层不可选，与 2D 一致）。
  const surfaceItems = items.filter((it) => {
    const cat = itemCategoryOf(it);
    return S.currentLayer === "background" ? cat === "background" : cat === "floors";
  });

  if (!floors.length && !surfaceItems.length) return false;

  if (surfaceItems.length && (!floors.length || preferItem(ctx, e, surfaceItems[0]))) {
    return beginItemDrag(ctx, e, surfaceItems);
  }

  const f = floors[0];
  if (!f) return false;
  S.dragSnapshot = snapshotState();
  if (e.shiftKey) {
    const next = new Set(S.selectedFloorKeys);
    if (next.has(f._key)) next.delete(f._key);
    else next.add(f._key);
    setFloorSelection(Array.from(next), f._key);
    drag.kind = "none";
    draw();
    return true;
  }
  if (!S.selectedFloorKeys.has(f._key)) setFloorSelection([f._key]);
  const hit = rayOnPlane(ctx, e.clientX, e.clientY, floorSlabOf(f, gapBelowFor(f)).topY);
  if (!hit) return false;
  S.dragFloorKey = f._key;
  S.dragFloorMode = "move";
  drag.kind = "floor-move";
  drag.key = f._key;
  drag.offX = hit.x - f._wx;
  drag.offZ = hit.z - f._wz;
  hideDetail();
  hideContextMenu();
  draw();
  return true;
}

/** 表面物件与地板重叠时，谁离相机更近就选谁。 */
function preferItem(ctx: Scene3DCtx, e: MouseEvent, item: EditorItem): boolean {
  const all = pickAll(ctx, e.clientX, e.clientY);
  for (const r of all) {
    if (r.pick.kind === "item" && r.pick.key === item._editorKey) return true;
    if (r.pick.kind === "floor") return false;
  }
  return true;
}

function beginItemInteraction(ctx: Scene3DCtx, e: MouseEvent): boolean {
  const hits = pickItems(ctx, e.clientX, e.clientY);
  if (!hits.length) return false;
  return beginItemDrag(ctx, e, hits);
}

function beginItemDrag(ctx: Scene3DCtx, e: MouseEvent, hits: EditorItem[]): boolean {
  // Shift 点击 = 加选 / 取消加选（与 2D 的 shift 语义一致）。
  if (e.shiftKey && hits.length) {
    const hit = hits[0];
    const next = new Set(S.selectedKeys);
    if (next.has(hit._editorKey)) next.delete(hit._editorKey);
    else next.add(hit._editorKey);
    setSelection(Array.from(next), hit._editorKey);
    drag.kind = "none";
    hideDetail();
    hideContextMenu();
    draw();
    return true;
  }

  // 3D 有真实深度：射线最近的那个就是用户点的那个。
  //
  // 这里刻意【不】沿用 2D 的「已选中优先」启发式（2D 靠它在重叠时稳住选择）：
  // 3D 里射线常常会穿过当前选中的大件（整面墙、大块装饰、背景面），一旦沿用，
  // 点任何地方都会把选择粘回那个大件上 —— 表现就是「点击位置和实际选中的物品
  // 不是同一个位置」。唯一保留的是多选组：点组内成员时不打散整组。
  const hit = hits[0];
  if (!hit) return false;
  const keepGroup = S.selectedKeys.size > 1 && isSelected(hit._editorKey);

  S.dragSnapshot = snapshotState();
  hideDetail();
  hideContextMenu();

  const box = itemBoxOf(hit);

  if (S.yAxisDrag) {
    drag.kind = "item-y";
    drag.key = hit._editorKey;
    drag.groupKeys = keepGroup ? selectionKeys() : [];
    drag.startValue = hit.localPosition?.y ?? 0;
    drag.startPointerY = rayVerticalY(ctx, e.clientX, e.clientY, { x: hit._wx, z: hit._wz }) ?? 0;
    if (!keepGroup) setSelection([hit._editorKey]);
    else S.selectedKey = hit._editorKey;
    snapshotStartY(drag.groupKeys.length ? drag.groupKeys : [hit._editorKey]);
    draw();
    return true;
  }

  // 先确定选择，再尝试起手拖动。顺序很重要：早期版本在射线解不出拖动平面时
  // 直接 return false，会一路落到「框选」分支把选择清空 —— 表现就是「点了没反应」。
  if (keepGroup) {
    S.selectedKey = hit._editorKey;
    drag.groupKeys = selectionKeys();
    drag.lastWx = hit._wx;
    drag.lastWz = hit._wz;
  } else {
    setSelection([hit._editorKey]);
    drag.groupKeys = [];
  }

  const hitPt = rayOnPlane(ctx, e.clientX, e.clientY, box.baseY);
  if (hitPt) {
    drag.kind = "item-move";
    drag.key = hit._editorKey;
    drag.offX = hitPt.x - hit._wx;
    drag.offZ = hitPt.z - hit._wz;
  } else {
    // 拖动平面在相机背后（极端俯仰/高台物件）：只选中，不进入移动。
    drag.kind = "none";
  }
  draw();
  return true;
}

// ───────────────────────── 拖动过程 ─────────────────────────

function dragItems(ctx: Scene3DCtx, e: MouseEvent): void {
  const item = itemByKey(drag.key);
  if (!item) return;
  const box = itemBoxOf(item);
  const hit = rayOnPlane(ctx, e.clientX, e.clientY, box.baseY);
  if (!hit) return;

  if (drag.groupKeys.length > 1) {
    const newWx = hit.x - drag.offX;
    const newWz = hit.z - drag.offZ;
    const dx = newWx - drag.lastWx;
    const dz = newWz - drag.lastWz;
    const groupSet = new Set(drag.groupKeys);
    for (const k of drag.groupKeys) {
      const it = itemByKey(k);
      if (!it) continue;
      const nx = it._wx + dx;
      const nz = it._wz + dz;
      if (moveBlockedAt(it, nx, nz, groupSet)) continue;
      it._wx = nx;
      it._wz = nz;
    }
    drag.lastWx = newWx;
    drag.lastWz = newWz;
  } else {
    const nx = hit.x - drag.offX;
    const nz = hit.z - drag.offZ;
    if (!moveBlockedAt(item, nx, nz)) {
      item._wx = nx;
      item._wz = nz;
      syncLocalFromWorld(item);
    }
  }
  draw();
}

function dragItemY(ctx: Scene3DCtx, e: MouseEvent): void {
  const item = itemByKey(drag.key);
  if (!item) return;
  const y = rayVerticalY(ctx, e.clientX, e.clientY, { x: item._wx, z: item._wz });
  if (y == null) return;
  const dy = snapValue(y - drag.startPointerY, S.freeSnapStep);
  const keys = drag.groupKeys.length ? drag.groupKeys : [drag.key];
  for (const k of keys) {
    const it = itemByKey(k);
    if (!it) continue;
    // 基准必须取拖动开始时的快照，不能读当前值——否则每帧都会再加一次 dy。
    const base = drag.startYByKey.get(k) ?? (it.localPosition?.y ?? 0);
    const ny = base + dy;
    if (!it.localPosition) it.localPosition = { x: it._wx, y: 0, z: it._wz };
    it.localPosition.y = ny;
    if (it.worldPosition) it.worldPosition.y = ny;
  }
  S.dirty = true;
  draw();
}

function dragFloorMove(ctx: Scene3DCtx, e: MouseEvent): void {
  const f = floorByKey(drag.key);
  if (!f) return;
  const top = floorSlabOf(f, gapBelowFor(f)).topY;
  const hit = rayOnPlane(ctx, e.clientX, e.clientY, top);
  if (!hit) return;
  S.dragFloorMode = "move";
  dragFloorWorld(f, hit.x - drag.offX, hit.z - drag.offZ);
  draw();
}

function dragFloorResize(ctx: Scene3DCtx, e: MouseEvent): void {
  const f = floorByKey(drag.key);
  if (!f) return;
  const top = floorSlabOf(f, gapBelowFor(f)).topY;
  const hit = rayOnPlane(ctx, e.clientX, e.clientY, top);
  if (!hit) return;
  S.dragFloorMode = "resize";
  dragFloorWorld(f, hit.x, hit.z);
  draw();
}

function dragFloorY(ctx: Scene3DCtx, e: MouseEvent): void {
  const f = floorByKey(drag.key);
  if (!f) return;
  const y = rayVerticalY(ctx, e.clientX, e.clientY, { x: f._wx, z: f._wz });
  if (y == null) return;
  const dy = snapValue(y - drag.startPointerY, S.freeSnapStep);
  // 复用 2D 的行走面高度语义（含"同时抬升其上物品"）。
  applyFloorWalkHeight(f, drag.startValue + dy, true);
  S.dirty = true;
  draw();
}

function dragItemResize(ctx: Scene3DCtx, e: MouseEvent): void {
  const it = itemByKey(drag.key);
  if (!it || !drag.edge) return;
  const box = itemBoxOf(it);
  const hit = rayOnPlane(ctx, e.clientX, e.clientY, box.baseY);
  if (!hit) return;
  // 以物件中心为锚，按指针到中心的距离换算格数（对角锚的精确复刻见 2D
  // resizeItemByCells；这里用中心对称口径，手感一致且不会跑偏）。
  const rad = (box.rotDeg * Math.PI) / 180;
  const dx = hit.x - it._wx;
  const dz = hit.z - it._wz;
  const lx = dx * Math.cos(rad) + dz * Math.sin(rad);
  const lz = -dx * Math.sin(rad) + dz * Math.cos(rad);
  const wCells = Math.max(1, Math.round((Math.abs(lx) * 2) / CELL));
  const dCells = Math.max(1, Math.round((Math.abs(lz) * 2) / CELL));
  if (isAirWallItem(it)) setAirWallSize(it, wCells, dCells);
  else if (isResizableBackgroundItem(it)) setItemPlaneSize(it, wCells, dCells);
  draw();
}

function dragItemHeight(ctx: Scene3DCtx, e: MouseEvent): void {
  const it = itemByKey(drag.key);
  if (!it) return;
  const y = rayVerticalY(ctx, e.clientX, e.clientY, { x: it._wx, z: it._wz });
  if (y == null) return;
  const dCells = Math.round((y - drag.startPointerY) / CELL);
  setAirWallHeight(it, Math.max(1, drag.startValue + dCells));
  draw();
}

function dragWaypoint(ctx: Scene3DCtx, e: MouseEvent): void {
  const group = activeGroup();
  if (!group || !S.draggingWaypointId) return;
  const wp = group.waypoints.find((w) => w.id === S.draggingWaypointId);
  if (!wp) return;
  const hit = rayOnPlane(ctx, e.clientX, e.clientY, 0.5);
  if (!hit) return;
  wp.x = snapValue(hit.x, S.freeSnapStep);
  wp.z = snapValue(hit.z, S.freeSnapStep);
  S.dirty = true;
  draw();
}

function placeWaypoint(ctx: Scene3DCtx, e: MouseEvent): void {
  const group = activeGroup();
  if (!group) return;
  const hit = rayOnPlane(ctx, e.clientX, e.clientY, currentGroundY());
  if (!hit) return;
  pushHistory();
  const wp = { id: uuid(), x: snapValue(hit.x, S.freeSnapStep), z: snapValue(hit.z, S.freeSnapStep) };
  group.waypoints.push(wp);
  S.selectedWaypointId = wp.id;
  if (S.animRouteAutoAdd && S.activeAnimEventIdx !== null) {
    const evt = group.events[S.activeAnimEventIdx];
    if (evt && evt.type === "move") {
      if (!evt.waypointIds) evt.waypointIds = [];
      evt.waypointIds.push(wp.id);
    }
  }
  S.dirty = true;
}

// ───────────────────────── 右键菜单 ─────────────────────────

function openContextMenu3D(ctx: Scene3DCtx, e: MouseEvent): void {
  const wps = pickWaypoints(ctx, e.clientX, e.clientY);
  if (S.currentLayer === "anim" && wps.length) {
    S.selectedWaypointId = wps[0];
    showWaypointContextMenu(wps[0], e.clientX, e.clientY);
    draw();
    return;
  }

  const items = pickItems(ctx, e.clientX, e.clientY);
  if (items.length) {
    const hit = items.find((it) => isSelected(it._editorKey)) ?? items[0];
    if (batchTransformCount() >= 2 && isSelected(hit._editorKey)) {
      showBatchHeightMenu(e.clientX, e.clientY);
    } else {
      if (!isSelected(hit._editorKey)) setSelection([hit._editorKey]);
      showContextMenu(hit, e.clientX, e.clientY);
    }
    draw();
    return;
  }

  const floors = pickFloors(ctx, e.clientX, e.clientY);
  if (floors.length && (S.currentLayer === "floor" || S.currentLayer === "background")) {
    setFloorSelection([floors[0]._key]);
    // 地板编辑器弹窗：与 2D 同一入口（重编辑器 UI 统一回落到既有 DOM 面板）。
    void import("../floorEditorModal").then((m) => m.openFloorEditorModal(floors[0]));
    draw();
  }
}

/** 供外部（如切换视图）读取当前指针世界坐标。 */
export function pointerWorld3D(): { x: number; z: number } | null {
  if (!Number.isFinite(S.hoverWx) || !Number.isFinite(S.hoverWz)) return null;
  return { x: S.hoverWx, z: S.hoverWz };
}
