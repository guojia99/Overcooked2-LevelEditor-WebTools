import {
  S,
  PickCandidate,
  EditorItem
} from "./state";
import { dom } from "./dom";
import {
  uuid,
  normalizeRot,
  prefabIdFromPath,
  canvasToWorld,
  snapValue
} from "./coords";
import { itemLabel } from "./labels";
import { itemCategoryOf, isResizableBackgroundItem } from "./catalog";
import {
  isSurfaceItem,
  surfaceKindLabelZh
} from "../floorColors";
import {
  isSelected,
  selectionKeys,
  setSelection,
  clearSelection,
  setFloorSelection,
  clearFloorSelection
} from "./selection";
import {
  hideDetail,
  hideContextMenu,
  hidePickTip
} from "./ui/overlay";
import { showPickTip } from "./ui/pickTip";
import {
  showItemOverlapPickTip,
  showSurfaceOverlapPickTip,
  showMoveMemberOverlapPickTip,
} from "./ui/pickOverlap";
import { showBatchHeightMenu, showContextMenu, showWaypointContextMenu } from "./ui/contextMenu";
import { selectionHeightTargetCount } from "./selectionHeight";
import { batchNudgeSelected, batchRotateSelected, batchTransformCount } from "./selectionTransform";
import { openFloorEditorModal } from "./floorEditorModal";
import {
  addFloorAt,
  addAirFloorAt,
  dragFloor,
  finalizeFloor,
  isThemedFloor,
  maybeSyncMaterialTilingOnResize,
} from "./floors";
import {
  addFromCatalog,
  addFromCatalogBatch,
  moveBlockedAt,
  syncLocalFromWorld,
  rotateItemByDelta,
  warnItemVoid,
  deleteSelected,
  nudgeSelectedItems,
  resizeItem,
  resizeAirWall
} from "./items";
import {
  addCombo,
  comboById
} from "./combos";
import {
  copySelection,
  cutSelection,
  pasteClipboard,
  copyFloors,
  cutFloors,
  pasteFloors,
  duplicateFloors
} from "./clipboard";
import {
  pushHistory,
  undo,
  redo,
  commitDragSnapshot,
  snapshotState
} from "./historyOps";
import { draw } from "./render";
import { closeModal } from "../modals";
import { hitTestAll, hitTestItemResizeHandle } from "./renderItems";
import { hitTestFloorsAll, type FloorHit } from "./renderFloors";
import { floorWalkY, floorLayerIndex } from "./floorHeight";
import { updateMarqueeSelection } from "./render";
import { hitTestWaypoints, waypointInfo, deleteWaypoint, activeGroup, updateMovePickBar, exitMoveMode, removeSelectedMembers } from "./moveControl";
import { renderRightPanel, updatePanelTabButtons } from "./panels";
import { isPlayerItem } from "./renderItems";
import { isAirWallItem } from "./stubControls";
import { clearPalettePick, palettePlaceCountFor } from "./palette";

/** Shift+点在物品/地板上：按下先聚焦，拖动超过阈值再框选；短按切换选中或弹重叠列表。 */
const SHIFT_MARQUEE_THRESHOLD = 5;
let shiftMarqueePending = false;
let shiftMarqueeStartX = 0;
let shiftMarqueeStartY = 0;
let shiftPickClientX = 0;
let shiftPickClientY = 0;
let shiftPickItems: EditorItem[] = [];
let shiftPickSurface: { fHits: FloorHit[]; surfaceHits: EditorItem[] } | null = null;
let shiftToggleItemKey: string | null = null;
let shiftToggleWasSelected = false;
let shiftToggleFloorKey: string | null = null;
let shiftToggleFloorWasSelected = false;

export function resetOverlapMarqueePending(): void {
  shiftMarqueePending = false;
  shiftPickItems = [];
  shiftPickSurface = null;
  shiftToggleItemKey = null;
  shiftToggleFloorKey = null;
}

function beginShiftMarqueeAtHit(
  mx: number,
  my: number,
  clientX: number,
  clientY: number,
  opts: {
    items?: EditorItem[];
    fHits?: FloorHit[];
    surfaceHits?: EditorItem[];
  }
): void {
  const fHits = opts.fHits ?? [];
  const surfaceHits = opts.surfaceHits ?? opts.items ?? [];
  const itemHits = opts.items ?? surfaceHits;

  shiftMarqueePending = true;
  shiftMarqueeStartX = mx;
  shiftMarqueeStartY = my;
  shiftPickClientX = clientX;
  shiftPickClientY = clientY;
  shiftPickItems = itemHits.length > 1 ? itemHits : [];
  shiftPickSurface =
    fHits.length + surfaceHits.length > 1 ? { fHits, surfaceHits } : null;

  shiftToggleItemKey = null;
  shiftToggleFloorKey = null;

  const primaryItem = surfaceHits[0] ?? itemHits[0] ?? null;
  const primaryFloor = !primaryItem && fHits[0] ? fHits[0].floor : null;

  if (primaryItem) {
    shiftToggleItemKey = primaryItem._editorKey;
    shiftToggleWasSelected = isSelected(primaryItem._editorKey);
    if (!shiftToggleWasSelected) S.selectedKeys.add(primaryItem._editorKey);
    S.selectedKey = primaryItem._editorKey;
  } else if (primaryFloor) {
    shiftToggleFloorKey = primaryFloor._key;
    shiftToggleFloorWasSelected = S.selectedFloorKeys.has(primaryFloor._key);
    if (!shiftToggleFloorWasSelected) S.selectedFloorKeys.add(primaryFloor._key);
    S.selectedFloorKey = primaryFloor._key;
  }
}

function promoteShiftMarqueeToBox(mx: number, my: number): void {
  shiftMarqueePending = false;
  shiftToggleItemKey = null;
  shiftToggleFloorKey = null;
  shiftPickItems = [];
  shiftPickSurface = null;
  S.marqueeing = true;
  S.marqueeAdd = true;
  S.marqueeStartX = shiftMarqueeStartX;
  S.marqueeStartY = shiftMarqueeStartY;
  S.marqueeCurX = mx;
  S.marqueeCurY = my;
  updateMarqueeSelection();
}

function finishShiftMarqueeClick(): boolean {
  if (!shiftMarqueePending) return false;
  shiftMarqueePending = false;

  if (shiftPickItems.length > 1) {
    showItemOverlapPickTip(shiftPickItems, shiftPickClientX, shiftPickClientY, true);
  } else if (shiftPickSurface) {
    const { fHits, surfaceHits } = shiftPickSurface;
    if (fHits.length + surfaceHits.length > 1) {
      const itemKeys = surfaceHits.map((it) => it._editorKey);
      const floorKeys = fHits.map((fh) => fh.floor._key);
      showSurfaceOverlapPickTip(
        fHits,
        surfaceHits,
        shiftPickClientX,
        shiftPickClientY,
        true,
        overlapBatchHeader(itemKeys, floorKeys, shiftPickClientX, shiftPickClientY)
      );
    }
  } else {
    if (shiftToggleItemKey && shiftToggleWasSelected) {
      S.selectedKeys.delete(shiftToggleItemKey);
      const keys = selectionKeys();
      S.selectedKey = keys.length ? keys[keys.length - 1] : null;
    }
    if (shiftToggleFloorKey && shiftToggleFloorWasSelected) {
      S.selectedFloorKeys.delete(shiftToggleFloorKey);
      const keys = [...S.selectedFloorKeys];
      S.selectedFloorKey = keys.length ? keys[keys.length - 1] : null;
    }
  }

  shiftPickItems = [];
  shiftPickSurface = null;
  shiftToggleItemKey = null;
  shiftToggleFloorKey = null;
  return true;
}

/** 批量微调应覆盖的键：已有 ≥2 项选中时保留完整选区，否则仅取点击处重叠项。 */
function batchAdjustKeysForOverlap(
  hitItemKeys: string[],
  hitFloorKeys: string[]
): { itemKeys: string[]; floorKeys: string[] } {
  const curItems = selectionKeys();
  const curFloors = [...S.selectedFloorKeys];
  if (curItems.length + curFloors.length >= 2) {
    return { itemKeys: curItems, floorKeys: curFloors };
  }
  const itemKeys = [...new Set([...curItems, ...hitItemKeys])];
  const floorKeys = [...new Set([...curFloors, ...hitFloorKeys])];
  if (itemKeys.length + floorKeys.length >= 2) {
    return { itemKeys, floorKeys };
  }
  return { itemKeys: hitItemKeys, floorKeys: hitFloorKeys };
}

function openOverlapBatchAdjust(
  hitItemKeys: string[],
  hitFloorKeys: string[],
  clientX: number,
  clientY: number
): void {
  const { itemKeys, floorKeys } = batchAdjustKeysForOverlap(hitItemKeys, hitFloorKeys);
  setSelection(itemKeys, itemKeys.length ? itemKeys[itemKeys.length - 1] : undefined);
  setFloorSelection(floorKeys, floorKeys.length ? floorKeys[floorKeys.length - 1] : undefined);
  hideDetail();
  showBatchHeightMenu(clientX, clientY);
  draw();
}

function overlapBatchHeader(
  hitItemKeys: string[],
  hitFloorKeys: string[],
  clientX: number,
  clientY: number
) {
  const { itemKeys, floorKeys } = batchAdjustKeysForOverlap(hitItemKeys, hitFloorKeys);
  const total = itemKeys.length + floorKeys.length;
  if (total < 2) return undefined;
  const curTotal = selectionKeys().length + S.selectedFloorKeys.size;
  const sub =
    curTotal >= 2 && total > hitItemKeys.length + hitFloorKeys.length
      ? "含全部已选 · 微移 · 旋转 · 高度"
      : "微移 · 旋转 · 高度";
  return {
    label: `批量微调（${total} 项）`,
    sub,
    onClick: () => openOverlapBatchAdjust(hitItemKeys, hitFloorKeys, clientX, clientY),
  };
}

export function updateCanvasCursor() {
  dom.canvas.classList.remove("pan-ready", "pan-active");
  if (S.panning) dom.canvas.classList.add("pan-active");
  else if (S.spaceHeld) dom.canvas.classList.add("pan-ready");
}

export function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  return tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT" || target.isContentEditable;
}

/** Arrow-key nudge hold state: one undo entry per press-and-hold gesture. */
let arrowNudgeHeld = false;

function activeFloorDragKeys(): string[] {
  if (S.dragFloorGroupKeys.length > 0) return [...S.dragFloorGroupKeys];
  if (S.dragFloorKey) return [S.dragFloorKey];
  return [];
}

function isSurfaceLayerGroupDrag(): boolean {
  return activeFloorDragKeys().length > 0 && S.dragGroupKeys.length > 0;
}

/** Begin a floor-layer group drag that may include both floors and surface items. */
function beginSurfaceLayerGroupDrag(
  wx: number,
  wz: number,
  floorAnchorKey: string | null,
  itemAnchorKey: string | null
): void {
  if (S.selectedFloorKeys.size > 0) {
    const floorKeys = [...S.selectedFloorKeys];
    const fk =
      floorAnchorKey && S.selectedFloorKeys.has(floorAnchorKey)
        ? floorAnchorKey
        : floorKeys[floorKeys.length - 1];
    S.dragFloorKey = fk;
    S.dragFloorMode = "move";
    S.dragFloorEdge = "";
    S.dragFloorGroupKeys = floorKeys;
  }
  if (S.selectedKeys.size > 0) {
    const keys = selectionKeys();
    const ik =
      itemAnchorKey && S.selectedKeys.has(itemAnchorKey)
        ? itemAnchorKey
        : keys[keys.length - 1];
    S.dragItemKey = ik;
    S.dragGroupKeys = keys;
    const anchor = S.items.find((i) => i._editorKey === ik);
    if (anchor) {
      S.dragOffsetX = wx - anchor._wx;
      S.dragOffsetZ = wz - anchor._wz;
    }
  }
  S.dragLastWx = wx;
  S.dragLastWz = wz;
}

function applySurfaceLayerGroupDelta(dx: number, dz: number): void {
  for (const k of activeFloorDragKeys()) {
    const f = S.floors.find((x) => x._key === k);
    if (f) {
      f._wx += dx;
      f._wz += dz;
    }
  }
  const groupSet = new Set(S.dragGroupKeys);
  for (const k of S.dragGroupKeys) {
    const it = S.items.find((i) => i._editorKey === k);
    if (!it) continue;
    const nx = it._wx + dx;
    const nz = it._wz + dz;
    if (moveBlockedAt(it, nx, nz, groupSet)) continue;
    it._wx = nx;
    it._wz = nz;
  }
}

export function setupCanvas() {
  dom.canvas.addEventListener("wheel", (e) => {
    e.preventDefault();
    const factor = e.deltaY > 0 ? 0.9 : 1.1;
    S.scale = Math.min(4, Math.max(0.25, S.scale * factor));
    draw();
  });

  dom.canvas.addEventListener("mousedown", (e) => {
    const rect = dom.canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;
    const { x: wx, z: wz } = canvasToWorld(mx, my);

    if (e.button === 1 || (e.button === 0 && e.altKey) || (e.button === 0 && S.spaceHeld)) {
      S.panning = true;
      S.lastMx = mx;
      S.lastMy = my;
      hideDetail();
      updateCanvasCursor();
      return;
    }

    if (e.button === 2) return;
    if (e.button !== 0) return;

    resetOverlapMarqueePending();
    hidePickTip();

    // Move control waypoint interactions: active on the dedicated move layer,
    // and on the floor layer while a move group is active.
    if (S.currentLayer === "move" || S.activeMoveGroupId || S.currentLayer === "floor" || S.currentLayer === "background") {
      const onMoveLayer = S.currentLayer === "move";
      // In "members" mode waypoints are intentionally not clickable so items /
      // floors under them can be picked.
      const inMembersMode =
        onMoveLayer && S.moveMode === "members" && !!activeGroup();

      if (!inMembersMode) {
        // 0.5 格内的路点视为重合：命中多个时弹出选点列表（与物品选点一致）。
        const wpHits = hitTestWaypoints(wx, wz);
        if (wpHits.length > 0) {
          let wpId: string;
          if (S.selectedWaypointId && wpHits.some((h) => h.id === S.selectedWaypointId)) {
            // Already selected one of the stacked points → drag it directly.
            wpId = S.selectedWaypointId;
          } else if (wpHits.length > 1) {
            showPickTip(
              wpHits.map((h) => {
                const info = waypointInfo(h.id);
                return {
                  title: `路点 #${info?.num ?? "?"}`,
                  sub: info
                    ? `${info.group.displayName} · (${info.wp.x.toFixed(2)}, ${info.wp.z.toFixed(2)})`
                    : "—",
                  onPick: () => {
                    S.selectedWaypointId = h.id;
                    renderRightPanel();
                    draw();
                  },
                };
              }),
              e.clientX,
              e.clientY
            );
            draw();
            return;
          } else {
            wpId = wpHits[0].id;
          }
          // Clicking a waypoint without an active group enters that group's editor.
          if (!S.activeMoveGroupId) {
            const g = S.moveControls.find((grp) =>
              grp.waypoints.some((w) => w.id === wpId)
            );
            if (g) {
              S.activeMoveGroupId = g.id;
              S.activeMoveEventIdx = null;
              S.activeRightTab = "move";
              updatePanelTabButtons();
            }
          }
          // Clicking an existing waypoint only selects it — adding it to the event
          // route is an explicit action ("添加选中路点" button in the event card).
          if (S.selectedWaypointId !== wpId) {
            S.selectedWaypointId = wpId;
            renderRightPanel();
          }
          if (e.button === 0) {
            S.dragSnapshot = snapshotState();
            S.draggingWaypointId = wpId;
            draw();
          }
          return;
        }
      }

      if (onMoveLayer) {
        // Members mode: click / marquee selects items AND floors.
        if (S.moveMode === "members" && activeGroup()) {
          // Pure background content (water, sky…) is never selectable as a member.
          const hits = hitTestAll(wx, wz, true).filter(
            (it) => itemCategoryOf(it) !== "background"
          );
          const fHits = hitTestFloorsAll(wx, wz).filter(
            (fh) => fh.floor.surfaceKind !== "background"
          );
          // 空气地板（仅碰撞盒）可入移动组：其 Col_AirFloor 碰撞盒会被烘焙
          // reparent + 动画，行走碰撞随组移动。
          const fHit = fHits.length > 0 ? fHits[0].floor : null;
          // 空气地板与上方物品重叠（如隐形桥被岛体地砖盖住）时，物品恒优先
          // 会让空气地板永远点不到——弹候选列表让两者都可选。
          if (hits.length > 0 && fHit?.airFloor) {
            showMoveMemberOverlapPickTip(hits, fHit, e.clientX, e.clientY, e.shiftKey, () => {
              updateMovePickBar();
              renderRightPanel();
              draw();
            });
            draw();
            return;
          }
          if (e.shiftKey && (hits.length > 0 || fHit)) {
            hideDetail();
            hideContextMenu();
            beginShiftMarqueeAtHit(mx, my, e.clientX, e.clientY, {
              items: hits,
              fHits,
              surfaceHits: hits,
            });
            updateMovePickBar();
            renderRightPanel();
            draw();
            return;
          }
          if (hits.length > 0 || fHit) {
            hideDetail();
            hideContextMenu();
            if (hits.length > 0) {
              setSelection([hits[0]._editorKey]);
              clearFloorSelection();
            } else if (fHit) {
              clearSelection();
              setFloorSelection([fHit._key]);
            }
            updateMovePickBar();
            renderRightPanel();
            draw();
            return;
          }
          // Empty canvas in members mode: start a marquee to select items + floors.
          S.marqueeing = true;
          S.marqueeAdd = e.shiftKey;
          S.marqueeStartX = mx;
          S.marqueeStartY = my;
          S.marqueeCurX = mx;
          S.marqueeCurY = my;
          if (!S.marqueeAdd) {
            clearSelection();
            clearFloorSelection();
            hideDetail();
            hideContextMenu();
          }
          draw();
          return;
        }
        // Waypoints mode: clicking empty canvas places a waypoint (mouseup).
        if (S.moveMode === "waypoints" && activeGroup()) {
          S.marqueeing = true;
          S.marqueeAdd = e.shiftKey;
          S.marqueeStartX = mx;
          S.marqueeStartY = my;
          S.marqueeCurX = mx;
          S.marqueeCurY = my;
          if (!S.marqueeAdd) {
            clearSelection();
            clearFloorSelection();
            hideDetail();
            hideContextMenu();
          }
          draw();
          return;
        }
        // Dedicated move layer without an active mode: only waypoints react to clicks.
        return;
      }
    }

    if (S.currentLayer === "floor" || S.currentLayer === "background") {
      if (S.pendingNewAirFloor) {
        S.pendingNewAirFloor = false;
        dom.canvas.style.cursor = "";
        addAirFloorAt(wx, wz);
        return;
      }
      if (S.pendingNewFloor) {
        const cat = S.pendingNewFloorCat;
        S.pendingNewFloor = false;
        S.pendingNewFloorCat = null;
        dom.canvas.style.cursor = "";
        addFloorAt(wx, wz, cat);
        return;
      }
      const layerBg = S.currentLayer === "background";
      const fHits = hitTestFloorsAll(wx, wz).filter((fh) =>
        layerBg ? fh.floor.surfaceKind === "background" : fh.floor.surfaceKind !== "background"
      );
      // Floor/background surfaces + ambient effects, per layer.
      const surfaceHits = hitTestAll(wx, wz, true).filter((it) => {
        const cat = itemCategoryOf(it);
        return layerBg ? cat === "background" : cat === "floors";
      });
      const keepFloor = S.selectedFloorKey
        ? fHits.find((fh) => fh.floor._key === S.selectedFloorKey)
        : S.selectedFloorKeys.size > 1
          ? fHits.find((fh) => S.selectedFloorKeys.has(fh.floor._key))
          : undefined;
      const keepItem =
        S.selectedKeys.size > 1
          ? surfaceHits.find((it) => isSelected(it._editorKey))
          : S.selectedKey
            ? surfaceHits.find((it) => it._editorKey === S.selectedKey)
            : undefined;
      if (e.shiftKey && (fHits.length > 0 || surfaceHits.length > 0)) {
        const resizeItem = surfaceHits[0];
        const rHit =
          resizeItem && layerBg && isResizableBackgroundItem(resizeItem)
            ? hitTestItemResizeHandle(resizeItem, wx, wz)
            : null;
        if (!rHit) {
          beginShiftMarqueeAtHit(mx, my, e.clientX, e.clientY, { fHits, surfaceHits });
          hideDetail();
          hideContextMenu();
          draw();
          return;
        }
      }
      if (fHits.length + surfaceHits.length > 1 && !keepFloor && !keepItem) {
        const itemKeys = surfaceHits.map((it) => it._editorKey);
        const floorKeys = fHits.map((fh) => fh.floor._key);
        showSurfaceOverlapPickTip(
          fHits,
          surfaceHits,
          e.clientX,
          e.clientY,
          e.shiftKey,
          overlapBatchHeader(itemKeys, floorKeys, e.clientX, e.clientY)
        );
        draw();
        return;
      }
      // 聚焦优先级：已选中的地板 > 已选中的地板层物品 > 最上层地板。
      // 否则重叠时点击已选中的物品（如压力开关）会被其下方的地板抢走焦点。
      const fHit = keepFloor ?? (keepItem ? null : fHits[0]) ?? null;
      if (fHit) {
        S.dragSnapshot = snapshotState();
        if (fHit.mode === "resize") {
          clearSelection();
          setFloorSelection([fHit.floor._key]);
          S.dragFloorKey = fHit.floor._key;
          S.dragFloorMode = fHit.mode;
          S.dragFloorEdge = fHit.edge;
          S.dragFloorAnchorX = fHit.anchorX;
          S.dragFloorAnchorZ = fHit.anchorZ;
          S.dragFloorGroupKeys = [];
          S.dragFloorResizePrevW = fHit.floor._wCells;
          S.dragFloorResizePrevD = fHit.floor._dCells;
          if (fHit.mode === "resize") dom.canvas.style.cursor = "grabbing";
        } else if (
          S.selectedFloorKeys.has(fHit.floor._key) &&
          (S.selectedFloorKeys.size > 1 || S.selectedKeys.size > 0)
        ) {
          S.selectedFloorKey = fHit.floor._key;
          beginSurfaceLayerGroupDrag(wx, wz, fHit.floor._key, null);
        } else {
          clearSelection();
          setFloorSelection([fHit.floor._key]);
          S.dragFloorKey = fHit.floor._key;
          S.dragFloorMode = fHit.mode;
          S.dragFloorEdge = fHit.edge;
          S.dragFloorAnchorX = fHit.anchorX;
          S.dragFloorAnchorZ = fHit.anchorZ;
          S.dragFloorGroupKeys = [];
          if (fHit.mode === "resize") {
            S.dragFloorResizePrevW = fHit.floor._wCells;
            S.dragFloorResizePrevD = fHit.floor._dCells;
            dom.canvas.style.cursor = "grabbing";
          }
        }
      } else {
        clearFloorSelection();
        const itemHit = keepItem ?? surfaceHits[0] ?? null;
        if (itemHit) {
          S.dragSnapshot = snapshotState();
          if (
            isSelected(itemHit._editorKey) &&
            (S.selectedKeys.size > 1 || S.selectedFloorKeys.size > 0)
          ) {
            S.selectedKey = itemHit._editorKey;
            const rHit =
              layerBg && isResizableBackgroundItem(itemHit)
                ? hitTestItemResizeHandle(itemHit, wx, wz)
                : null;
            if (rHit) {
              clearFloorSelection();
              S.dragItemResizeKey = itemHit._editorKey;
              S.dragItemResizeEdge = rHit.edge;
              S.dragItemResizeAnchorX = rHit.anchorX;
              S.dragItemResizeAnchorZ = rHit.anchorZ;
              dom.canvas.style.cursor = "grabbing";
            } else {
              beginSurfaceLayerGroupDrag(wx, wz, null, itemHit._editorKey);
            }
          } else {
            clearFloorSelection();
            setSelection([itemHit._editorKey]);
            const rHit =
              layerBg && isResizableBackgroundItem(itemHit)
                ? hitTestItemResizeHandle(itemHit, wx, wz)
                : null;
            if (rHit) {
              S.dragItemResizeKey = itemHit._editorKey;
              S.dragItemResizeEdge = rHit.edge;
              S.dragItemResizeAnchorX = rHit.anchorX;
              S.dragItemResizeAnchorZ = rHit.anchorZ;
              dom.canvas.style.cursor = "grabbing";
            } else {
              S.dragItemKey = itemHit._editorKey;
              S.dragGroupKeys = [];
              S.dragOffsetX = wx - itemHit._wx;
              S.dragOffsetZ = wz - itemHit._wz;
            }
          }
        } else {
          S.marqueeing = true;
          S.marqueeAdd = e.shiftKey;
          S.marqueeStartX = mx;
          S.marqueeStartY = my;
          S.marqueeCurX = mx;
          S.marqueeCurY = my;
          if (!S.marqueeAdd) {
            clearSelection();
            clearFloorSelection();
            hideDetail();
            hideContextMenu();
          }
        }
      }
      draw();
      return;
    }

    const hits = hitTestAll(wx, wz);
    // After a marquee multi-select, clicking any selected item keeps the group
    // (and starts a group drag) instead of popping the overlap picker.
    const groupHit = S.selectedKeys.size > 1 ? hits.find((it) => isSelected(it._editorKey)) : undefined;
    const already = groupHit ?? (S.selectedKey ? hits.find((it) => it._editorKey === S.selectedKey) : undefined);
    if (e.shiftKey && hits.length > 0 && !isPlayerItem(hits[0])) {
      const airResize =
        S.currentLayer === "items" && isAirWallItem(hits[0])
          ? hitTestItemResizeHandle(hits[0], wx, wz)
          : null;
      if (!airResize) {
        beginShiftMarqueeAtHit(mx, my, e.clientX, e.clientY, { items: hits });
        hideDetail();
        hideContextMenu();
        draw();
        return;
      }
    }
    if (hits.length > 1 && !already) {
      showItemOverlapPickTip(hits, e.clientX, e.clientY, e.shiftKey);
      draw();
      return;
    }
    const hit = already ?? hits[0] ?? null;
    if (hit) {
      S.dragSnapshot = snapshotState();
      hideDetail();
      hideContextMenu();
      const airResize =
        S.currentLayer === "items" && isAirWallItem(hit) && !e.shiftKey
          ? hitTestItemResizeHandle(hit, wx, wz)
          : null;
      if (airResize) {
        setSelection([hit._editorKey]);
        S.dragItemResizeKey = hit._editorKey;
        S.dragItemResizeEdge = airResize.edge;
        S.dragItemResizeAnchorX = airResize.anchorX;
        S.dragItemResizeAnchorZ = airResize.anchorZ;
        dom.canvas.style.cursor = "grabbing";
        S.dragItemKey = null;
        S.dragGroupKeys = [];
      } else if (S.selectedKeys.size > 1 && isSelected(hit._editorKey)) {
        S.selectedKey = hit._editorKey;
        S.dragItemKey = hit._editorKey;
        S.dragGroupKeys = selectionKeys();
        S.dragOffsetX = wx - hit._wx;
        S.dragOffsetZ = wz - hit._wz;
        S.dragLastWx = hit._wx;
        S.dragLastWz = hit._wz;
      } else {
        setSelection([hit._editorKey]);
        S.dragItemKey = hit._editorKey;
        S.dragGroupKeys = [];
        S.dragOffsetX = wx - hit._wx;
        S.dragOffsetZ = wz - hit._wz;
      }
    } else {
      S.marqueeing = true;
      S.marqueeAdd = e.shiftKey;
      S.marqueeStartX = mx;
      S.marqueeStartY = my;
      S.marqueeCurX = mx;
      S.marqueeCurY = my;
      if (!S.marqueeAdd) {
        clearSelection();
        hideDetail();
        hideContextMenu();
      }
    }
    draw();
  });

  dom.canvas.addEventListener("contextmenu", (e) => {
    e.preventDefault();
    const rect = dom.canvas.getBoundingClientRect();
    const { x: wx, z: wz } = canvasToWorld(e.clientX - rect.left, e.clientY - rect.top);
    // Waypoints: right-click shows the same coordinate adjustment as items.
    // Overlapping points (0.5 格) first show the picker list, like item picking.
    // (Members pick mode keeps waypoints out of the way, same as left-click.)
    const wpMenuActive =
      (S.currentLayer === "move" || S.activeMoveGroupId) &&
      !(S.currentLayer === "move" && S.moveMode === "members" && !!activeGroup());
    if (wpMenuActive) {
      const wpHits = hitTestWaypoints(wx, wz);
      if (wpHits.length > 0) {
        const already =
          !!S.selectedWaypointId && wpHits.some((h) => h.id === S.selectedWaypointId);
        if (wpHits.length > 1 && !already) {
          hideDetail();
          hideContextMenu();
          showPickTip(
            wpHits.map((h) => {
              const info = waypointInfo(h.id);
              return {
                title: `路点 #${info?.num ?? "?"}`,
                sub: info
                  ? `${info.group.displayName} · (${info.wp.x.toFixed(2)}, ${info.wp.z.toFixed(2)})`
                  : "—",
                onPick: () => {
                  S.selectedWaypointId = h.id;
                  renderRightPanel();
                  draw();
                  showWaypointContextMenu(h.id, e.clientX, e.clientY);
                },
              };
            }),
            e.clientX,
            e.clientY
          );
          draw();
          return;
        }
        const selId =
          S.selectedWaypointId && wpHits.some((h) => h.id === S.selectedWaypointId)
            ? S.selectedWaypointId
            : null;
        const wpId = selId ?? wpHits[0].id;
        S.selectedWaypointId = wpId;
        renderRightPanel();
        draw();
        showWaypointContextMenu(wpId, e.clientX, e.clientY);
        return;
      }
    }
    if (S.currentLayer === "floor" || S.currentLayer === "background") {
      const layerBg = S.currentLayer === "background";
      const fHits = hitTestFloorsAll(wx, wz).filter((fh) =>
        layerBg ? fh.floor.surfaceKind === "background" : fh.floor.surfaceKind !== "background"
      );
      // Floor/background surfaces + ambient effects, per layer.
      const surfaceHits = hitTestAll(wx, wz, true).filter((it) => {
        const cat = itemCategoryOf(it);
        return layerBg ? cat === "background" : cat === "floors";
      });
      const keepFloor = S.selectedFloorKey
        ? fHits.find((fh) => fh.floor._key === S.selectedFloorKey)
        : S.selectedFloorKeys.size > 1
          ? fHits.find((fh) => S.selectedFloorKeys.has(fh.floor._key))
          : undefined;
      const keepItem =
        S.selectedKeys.size > 1
          ? surfaceHits.find((it) => isSelected(it._editorKey))
          : S.selectedKey
            ? surfaceHits.find((it) => it._editorKey === S.selectedKey)
            : undefined;
      if (fHits.length + surfaceHits.length > 1 && !keepFloor && !keepItem) {
        const itemKeys = surfaceHits.map((it) => it._editorKey);
        const floorKeys = fHits.map((fh) => fh.floor._key);
        showSurfaceOverlapPickTip(
          fHits,
          surfaceHits,
          e.clientX,
          e.clientY,
          false,
          overlapBatchHeader(itemKeys, floorKeys, e.clientX, e.clientY),
          {
            onPickFloor: (fh) => {
              setFloorSelection([fh.floor._key]);
              clearSelection();
              openFloorEditorModal(fh.floor);
              draw();
            },
            onPickItem: (it) => {
              clearFloorSelection();
              setSelection([it._editorKey]);
              hideDetail();
              showContextMenu(it, e.clientX, e.clientY);
              draw();
            },
          }
        );
        draw();
        return;
      }
      // 聚焦优先级：已选中的地板 > 已选中的地板层物品 > 最上层地板。
      // 否则重叠时点击已选中的物品（如压力开关）会被其下方的地板抢走焦点。
      const fHit = keepFloor ?? (keepItem ? null : fHits[0]) ?? null;
      if (fHit) {
        if (
          selectionHeightTargetCount() >= 2 &&
          S.selectedFloorKeys.has(fHit.floor._key)
        ) {
          hideDetail();
          showBatchHeightMenu(e.clientX, e.clientY);
          draw();
          return;
        }
        setFloorSelection([fHit.floor._key]);
        openFloorEditorModal(fHit.floor);
        draw();
        return;
      }
      const itemHit = keepItem ?? surfaceHits[0] ?? null;
      if (itemHit) {
        if (selectionHeightTargetCount() >= 2 && isSelected(itemHit._editorKey)) {
          hideDetail();
          showBatchHeightMenu(e.clientX, e.clientY);
          draw();
          return;
        }
        setSelection([itemHit._editorKey]);
        hideDetail();
        showContextMenu(itemHit, e.clientX, e.clientY);
        draw();
        return;
      }
      hideDetail();
      hideContextMenu();
      hidePickTip();
      return;
    }
    const hits = hitTestAll(wx, wz);
    const already = S.selectedKey ? hits.find((it) => it._editorKey === S.selectedKey) : undefined;
    if (hits.length > 1 && !already) {
      const itemKeys = hits.map((it) => it._editorKey);
      showItemOverlapPickTip(
        hits,
        e.clientX,
        e.clientY,
        false,
        overlapBatchHeader(itemKeys, [], e.clientX, e.clientY),
        {
          onPickItem: (it) => {
            setSelection([it._editorKey]);
            hideDetail();
            showContextMenu(it, e.clientX, e.clientY);
            draw();
          },
        }
      );
      draw();
      return;
    }
    const hit = already ?? hits[0] ?? null;
    if (hit) {
      if (!(S.selectedKeys.size > 1 && isSelected(hit._editorKey))) {
        setSelection([hit._editorKey]);
      } else {
        S.selectedKey = hit._editorKey;
      }
      hideDetail();
      if (selectionHeightTargetCount() >= 2 && isSelected(hit._editorKey)) {
        showBatchHeightMenu(e.clientX, e.clientY);
      } else {
        showContextMenu(hit, e.clientX, e.clientY);
      }
      draw();
    } else {
      hideDetail();
      hideContextMenu();
      hidePickTip();
    }
  });

  window.addEventListener("mousemove", (e) => {
    const rect = dom.canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;

    if (S.showCoords) {
      const inCanvas = mx >= 0 && my >= 0 && mx <= rect.width && my <= rect.height;
      const nx = inCanvas ? mx : -1;
      const ny = inCanvas ? my : -1;
      if (nx !== S.hoverCx || ny !== S.hoverCy) {
        S.hoverCx = nx;
        S.hoverCy = ny;
        if (!S.marqueeing && !S.panning && !S.dragFloorKey && !S.dragItemKey && !shiftMarqueePending) draw();
      }
    }

    if (shiftMarqueePending) {
      const dx = mx - shiftMarqueeStartX;
      const dy = my - shiftMarqueeStartY;
      if (Math.hypot(dx, dy) >= SHIFT_MARQUEE_THRESHOLD) {
        promoteShiftMarqueeToBox(mx, my);
      }
    }

    if (S.marqueeing) {
      S.marqueeCurX = mx;
      S.marqueeCurY = my;
      updateMarqueeSelection();
      draw();
      return;
    }

    if (S.panning) {
      S.panX += mx - S.lastMx;
      S.panY += my - S.lastMy;
      S.lastMx = mx;
      S.lastMy = my;
      draw();
      return;
    }

    if (S.draggingWaypointId) {
      const { x: wx, z: wz } = canvasToWorld(mx, my);
      const group = activeGroup();
      if (group) {
        const wp = group.waypoints.find((w) => w.id === S.draggingWaypointId);
        if (wp) {
          // 路点精度跟随全局精度（自由吸附步长）。
          wp.x = snapValue(wx, S.freeSnapStep);
          wp.z = snapValue(wz, S.freeSnapStep);
          S.dirty = true;
        }
      }
      draw();
      return;
    }

    if (S.dragFloorKey) {
      if (S.dragFloorMode === "resize") dom.canvas.style.cursor = "grabbing";
      if (isSurfaceLayerGroupDrag() || S.dragFloorGroupKeys.length > 1) {
        const { x: wx, z: wz } = canvasToWorld(mx, my);
        const dx = wx - S.dragLastWx;
        const dz = wz - S.dragLastWz;
        if (isSurfaceLayerGroupDrag()) {
          applySurfaceLayerGroupDelta(dx, dz);
        } else {
          for (const k of S.dragFloorGroupKeys) {
            const f = S.floors.find((x) => x._key === k);
            if (f) {
              f._wx += dx;
              f._wz += dz;
            }
          }
        }
        S.dragLastWx = wx;
        S.dragLastWz = wz;
      } else {
        const f = S.floors.find((x) => x._key === S.dragFloorKey);
        if (f) dragFloor(f, mx, my);
      }
      draw();
      return;
    }

    if (S.dragItemResizeKey) {
      dom.canvas.style.cursor = "grabbing";
      const it = S.items.find((i) => i._editorKey === S.dragItemResizeKey);
      if (it) {
        const { x: wx, z: wz } = canvasToWorld(mx, my);
        if (isAirWallItem(it)) resizeAirWall(it, wx, wz);
        else resizeItem(it, wx, wz);
      }
      draw();
      return;
    }

    if (!S.dragItemKey) {
      const { x: wx, z: wz } = canvasToWorld(mx, my);
      if ((S.currentLayer === "floor" || S.currentLayer === "background") && !S.pendingNewFloor) {
        const fHits = hitTestFloorsAll(wx, wz);
        let cursor = fHits[0]?.mode === "resize" ? "grab" : "";
        // Background layer: hovering a selected water/background plane's corner
        // shows the resize cursor too.
        if (!cursor && S.currentLayer === "background" && S.selectedKey) {
          const sel = S.items.find((i) => i._editorKey === S.selectedKey);
          if (sel && isResizableBackgroundItem(sel) && hitTestItemResizeHandle(sel, wx, wz)) {
            cursor = "grab";
          }
        }
        if (dom.canvas.style.cursor !== cursor) dom.canvas.style.cursor = cursor;
      } else if (S.currentLayer === "items" && S.selectedKey) {
        const sel = S.items.find((i) => i._editorKey === S.selectedKey);
        const cursor =
          sel && isAirWallItem(sel) && hitTestItemResizeHandle(sel, wx, wz) ? "grab" : "";
        if (dom.canvas.style.cursor !== cursor) dom.canvas.style.cursor = cursor;
      }
      return;
    }
    const item = S.items.find((i) => i._editorKey === S.dragItemKey);
    if (!item) return;
    const { x: wx, z: wz } = canvasToWorld(mx, my);
    if (S.dragGroupKeys.length > 1 || isSurfaceLayerGroupDrag()) {
      const newWx = wx - S.dragOffsetX;
      const newWz = wz - S.dragOffsetZ;
      const dx = newWx - S.dragLastWx;
      const dz = newWz - S.dragLastWz;
      if (isSurfaceLayerGroupDrag()) {
        applySurfaceLayerGroupDelta(dx, dz);
      } else {
        const groupSet = new Set(S.dragGroupKeys);
        for (const k of S.dragGroupKeys) {
          const it = S.items.find((i) => i._editorKey === k);
          if (it) {
            const nx = it._wx + dx;
            const nz = it._wz + dz;
            if (moveBlockedAt(it, nx, nz, groupSet)) continue;
            it._wx = nx;
            it._wz = nz;
          }
        }
      }
      S.dragLastWx = newWx;
      S.dragLastWz = newWz;
    } else {
      const nx = wx - S.dragOffsetX;
      const nz = wz - S.dragOffsetZ;
      if (!moveBlockedAt(item, nx, nz)) {
        item._wx = nx;
        item._wz = nz;
        syncLocalFromWorld(item);
      }
    }
    draw();
  });

  window.addEventListener("mouseup", () => {
    if (S.panning) {
      S.panning = false;
      updateCanvasCursor();
    }
    if (S.draggingWaypointId) {
      S.draggingWaypointId = null;
      S.dirty = true;
      commitDragSnapshot();
      draw();
    }
    if (S.dragFloorKey) {
      if (S.dragFloorMode === "resize") dom.canvas.style.cursor = "";
      for (const k of activeFloorDragKeys()) {
        const f = S.floors.find((x) => x._key === k);
        if (f) {
          if (S.dragFloorMode === "resize") {
            maybeSyncMaterialTilingOnResize(f, S.dragFloorResizePrevW, S.dragFloorResizePrevD);
          }
          finalizeFloor(f);
        }
      }
    }
    S.dragFloorKey = null;
    S.dragFloorGroupKeys = [];
    if (S.dragItemResizeKey) {
      dom.canvas.style.cursor = "";
      const it = S.items.find((i) => i._editorKey === S.dragItemResizeKey);
      if (it) {
        syncLocalFromWorld(it);
        warnItemVoid(it);
      }
      S.dragItemResizeKey = null;
      commitDragSnapshot();
      draw();
      return;
    }
    if (finishShiftMarqueeClick()) {
      draw();
      return;
    }
    if (S.marqueeing) {
      S.marqueeing = false;
      const dx = S.marqueeCurX - S.marqueeStartX;
      const dy = S.marqueeCurY - S.marqueeStartY;
      if (
        S.currentLayer === "move" &&
        S.moveMode === "waypoints" &&
        activeGroup() &&
        Math.abs(dx) < 5 &&
        Math.abs(dy) < 5
      ) {
        // Single click on empty canvas in waypoint mode: place a waypoint and select it.
        const { x: wx, z: wz } = canvasToWorld(S.marqueeStartX, S.marqueeStartY);
        const group = activeGroup();
        if (group) {
          pushHistory();
          // 路点精度跟随全局精度（自由吸附步长）。
          const wp = {
            id: uuid(),
            x: snapValue(wx, S.freeSnapStep),
            z: snapValue(wz, S.freeSnapStep),
          };
          group.waypoints.push(wp);
          S.selectedWaypointId = wp.id;
          // 放置自动编入：勾选时新路点直接追加到当前事件路线末尾。
          if (S.moveRouteAutoAdd && S.activeMoveEventIdx !== null) {
            const evt = group.events[S.activeMoveEventIdx];
            if (evt && evt.type === "move") {
              if (!evt.waypointIds) evt.waypointIds = [];
              evt.waypointIds.push(wp.id);
            }
          }
          S.dirty = true;
        }
      } else {
        const keys = selectionKeys();
        S.selectedKey = keys.length ? keys[keys.length - 1] : null;
      }
      updateMovePickBar();
      renderRightPanel();
      draw();
    }
    if (S.dragGroupKeys.length > 0) {
      for (const k of S.dragGroupKeys) {
        const it = S.items.find((i) => i._editorKey === k);
        if (it) {
          syncLocalFromWorld(it);
          warnItemVoid(it);
        }
      }
      S.dragGroupKeys = [];
    } else if (S.dragItemKey) {
      const item = S.items.find((i) => i._editorKey === S.dragItemKey);
      if (item) {
        syncLocalFromWorld(item);
        warnItemVoid(item);
      }
    }
    S.dragItemKey = null;
    commitDragSnapshot();
  });

  dom.canvas.addEventListener("dragover", (e) => {
    e.preventDefault();
  });

  dom.canvas.addEventListener("drop", (e) => {
    e.preventDefault();
    const rect = dom.canvas.getBoundingClientRect();
    const { x: wx, z: wz } = canvasToWorld(e.clientX - rect.left, e.clientY - rect.top);
    const guid = e.dataTransfer?.getData("text/plain");
    // 联合组合（palette「联合组合」分类）：一次放置多个物品并自动完成联动配置。
    if (guid && guid.startsWith("combo:")) {
      const def = comboById(guid.substring("combo:".length));
      if (def) addCombo(def, wx, wz);
      return;
    }
    const cat = guid ? S.catalogByGuid.get(guid) : S.dragCatalog;
    if (!cat) {
      if (S.dragCombo) addCombo(S.dragCombo, wx, wz);
      return;
    }
    const batch = S.dragCatalogBatch > 1 ? S.dragCatalogBatch : palettePlaceCountFor(cat.guid);
    if (batch > 1) addFromCatalogBatch(cat, wx, wz, batch);
    else addFromCatalog(cat, wx, wz);
  });

  window.addEventListener("keydown", (e) => {
    if (e.code === "Space" && !isTypingTarget(e.target)) {
      e.preventDefault();
      if (!S.spaceHeld) {
        S.spaceHeld = true;
        updateCanvasCursor();
      }
    }

    // 方向键：上下左右微调选中的物品 / 路点（按住连续移动，整段只记一次撤销）。
    const arrowDirs: Record<string, [number, number]> = {
      ArrowLeft: [-1, 0],
      ArrowRight: [1, 0],
      ArrowUp: [0, 1],
      ArrowDown: [0, -1],
    };
    const adir = arrowDirs[e.key];
    if (adir && !isTypingTarget(e.target) && !S.panning && !S.marqueeing) {
      e.preventDefault();
      const step = S.freeSnapStep;
      const dx = adir[0] * step;
      const dz = adir[1] * step;
      const wpContext =
        S.currentLayer === "move" ||
        ((S.currentLayer === "floor" || S.currentLayer === "background") && !!S.activeMoveGroupId);
      const wpNudge =
        wpContext && S.selectedWaypointId && activeGroup()
          ? activeGroup()!.waypoints.find((w) => w.id === S.selectedWaypointId) ?? null
          : null;
      const itemKeys = S.currentLayer === "move" ? [] : selectionKeys();
      if (!wpNudge && itemKeys.length === 0) return;
      if (!arrowNudgeHeld) {
        pushHistory();
        arrowNudgeHeld = true;
      }
      if (wpNudge) {
        wpNudge.x = snapValue(wpNudge.x + dx, S.freeSnapStep);
        wpNudge.z = snapValue(wpNudge.z + dz, S.freeSnapStep);
        S.dirty = true;
        renderRightPanel();
      } else {
        if (batchTransformCount() >= 2) {
          batchNudgeSelected(dx, dz);
        } else {
          nudgeSelectedItems(dx, dz);
        }
        S.dirty = true;
        renderRightPanel();
      }
      draw();
      return;
    }

    if (e.key === "Escape") {
      hideDetail();
      hideContextMenu();
      hidePickTip();
      closeModal();
      clearPalettePick();
      S.marqueeing = false;
      resetOverlapMarqueePending();
      S.pendingNewFloor = false;
      S.pendingNewFloorCat = null;
      S.pendingNewAirFloor = false;
      dom.canvas.style.cursor = "";
      if (S.moveMode !== "none") exitMoveMode();
    }

    if ((e.ctrlKey || e.metaKey) && (e.key === "z" || e.key === "Z") && !isTypingTarget(e.target)) {
      if (e.shiftKey) redo();
      else undo();
      e.preventDefault();
      return;
    }
    if ((e.ctrlKey || e.metaKey) && (e.key === "x" || e.key === "X") && !isTypingTarget(e.target)) {
      if (S.currentLayer === "floor" || S.currentLayer === "background") cutFloors();
      else if (S.currentLayer !== "move") cutSelection();
      e.preventDefault();
      return;
    }
    if ((e.ctrlKey || e.metaKey) && (e.key === "c" || e.key === "C") && !isTypingTarget(e.target)) {
      if (S.currentLayer === "floor" || S.currentLayer === "background") copyFloors();
      else copySelection();
      e.preventDefault();
      return;
    }
    if ((e.ctrlKey || e.metaKey) && (e.key === "v" || e.key === "V") && !isTypingTarget(e.target)) {
      if (S.currentLayer === "floor" || S.currentLayer === "background") pasteFloors();
      else pasteClipboard();
      e.preventDefault();
      return;
    }
    if ((e.ctrlKey || e.metaKey) && (e.key === "d" || e.key === "D") && !isTypingTarget(e.target)) {
      if (S.currentLayer === "floor" || S.currentLayer === "background") duplicateFloors();
      e.preventDefault();
      return;
    }

    // Move layer: no scene item operations — Delete removes members / waypoints instead.
    if (S.currentLayer === "move") {
      if (isTypingTarget(e.target)) return;
      if (e.key === "Delete" || e.key === "Backspace") {
        if (S.moveMode === "waypoints" && S.selectedWaypointId && activeGroup()) {
          pushHistory();
          deleteWaypoint(activeGroup()!, S.selectedWaypointId);
          renderRightPanel();
          draw();
        } else if (S.moveMode === "members" && activeGroup()) {
          removeSelectedMembers();
        }
        e.preventDefault();
      }
      return;
    }

    if (S.currentLayer === "floor" || S.currentLayer === "background") {
      if (isTypingTarget(e.target)) return;
      if (e.key === "Delete" || e.key === "Backspace") {
        const killItems = new Set(
          selectionKeys().filter((k) => {
            const it = S.items.find((i) => i._editorKey === k);
            return it && isSurfaceItem(S.catalogByGuid.get(it.prefabGuid));
          })
        );
        if (S.selectedFloorKeys.size === 0 && killItems.size === 0) return;
        pushHistory();
        S.floors = S.floors.filter((x) => !S.selectedFloorKeys.has(x._key));
        S.items = S.items.filter((i) => !killItems.has(i._editorKey));
        clearFloorSelection();
        clearSelection();
        hideDetail();
        draw();
        return;
      }
      if (e.key === "r" || e.key === "R") {
        const delta = e.shiftKey ? -90 : 90;
        if (batchTransformCount() >= 2) {
          batchRotateSelected(delta);
          draw();
          return;
        }
        if (S.selectedFloorKeys.size === 1 && S.selectedFloorKey) {
          const f = S.floors.find((x) => x._key === S.selectedFloorKey);
          if (f) {
            pushHistory();
            f.localRotationY = normalizeRot(f.localRotationY + delta);
            draw();
          }
          return;
        }
        if (S.selectedFloorKeys.size === 0 && S.selectedKey) {
          const item = S.items.find((i) => i._editorKey === S.selectedKey);
          if (item && isSurfaceItem(S.catalogByGuid.get(item.prefabGuid))) {
            pushHistory();
            rotateItemByDelta(item, delta);
            draw();
          }
        }
      }
      return;
    }

    if (!S.selectedKey) return;
    const item = S.items.find((i) => i._editorKey === S.selectedKey);
    if (!item) return;

    if (isTypingTarget(e.target)) return;

    if (e.key === "Delete" || e.key === "Backspace") {
      deleteSelected();
    }
    if ((e.key === "r" || e.key === "R") && !isPlayerItem(item)) {
      const delta = e.shiftKey ? -90 : 90;
      if (batchTransformCount() >= 2) {
        batchRotateSelected(delta);
      } else {
        pushHistory();
        rotateItemByDelta(item, delta);
      }
      draw();
    }
  });

  window.addEventListener("keyup", (e) => {
    if (e.key.startsWith("Arrow")) arrowNudgeHeld = false;
    if (e.code === "Space") {
      S.spaceHeld = false;
      S.panning = false;
      updateCanvasCursor();
    }
  });

  window.addEventListener("blur", () => {
    S.spaceHeld = false;
    S.panning = false;
    arrowNudgeHeld = false;
    updateCanvasCursor();
  });

  document.addEventListener("mousedown", (e) => {
    if (!dom.detailEl.classList.contains("hidden") && !dom.detailEl.contains(e.target as Node)) {
      hideDetail();
    }
    if (!dom.ctxMenuEl.classList.contains("hidden") && !dom.ctxMenuEl.contains(e.target as Node)) {
      hideContextMenu();
    }
    if (
      e.target !== dom.canvas &&
      !dom.pickTipEl.classList.contains("hidden") &&
      !dom.pickTipEl.contains(e.target as Node)
    ) {
      hidePickTip();
    }
  });

  window.addEventListener("resize", () => draw());
}
