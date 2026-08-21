import {
  S,
  PickCandidate
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
import { itemCategoryOf } from "./catalog";
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
import { showContextMenu, showWaypointContextMenu } from "./ui/contextMenu";
import { openFloorEditorModal } from "./floorEditorModal";
import {
  addFloorAt,
  addAirFloorAt,
  dragFloor,
  finalizeFloor,
  isThemedFloor
} from "./floors";
import {
  addFromCatalog,
  moveBlockedAt,
  syncLocalFromWorld,
  warnItemVoid,
  deleteSelected,
  nudgeSelectedItems
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
import { hitTestAll } from "./renderItems";
import { hitTestFloorsAll } from "./renderFloors";
import { floorWalkY, floorLayerIndex } from "./floorHeight";
import { updateMarqueeSelection } from "./render";
import { hitTestWaypoints, waypointInfo, deleteWaypoint, activeGroup, updateMovePickBar, exitMoveMode, removeSelectedMembers } from "./moveControl";
import { renderRightPanel, updatePanelTabButtons } from "./panels";
import { isPlayerItem } from "./renderItems";

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
          const fHits = hitTestFloorsAll(wx, wz);
          // 空气地板（仅碰撞盒）不参与移动组成员选点。
          const fHit = fHits.length > 0 && fHits[0].floor.surfaceKind !== "background" && !fHits[0].floor.airFloor
            ? fHits[0].floor
            : null;
          if (hits.length > 0 || fHit) {
            hideDetail();
            hideContextMenu();
            if (e.shiftKey) {
              if (hits.length > 0) {
                for (const it of hits) {
                  if (isSelected(it._editorKey)) S.selectedKeys.delete(it._editorKey);
                  else S.selectedKeys.add(it._editorKey);
                }
                const keys = selectionKeys();
                S.selectedKey = keys.length ? keys[keys.length - 1] : null;
              }
              if (fHit) {
                if (S.selectedFloorKeys.has(fHit._key)) S.selectedFloorKeys.delete(fHit._key);
                else S.selectedFloorKeys.add(fHit._key);
              }
            } else if (hits.length > 0) {
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
        : undefined;
      const keepItem = S.selectedKey
        ? surfaceHits.find((it) => it._editorKey === S.selectedKey)
        : undefined;
      if (fHits.length + surfaceHits.length > 1 && !keepFloor && !keepItem) {
        hideDetail();
        hideContextMenu();
        const candidates: PickCandidate[] = [];
        for (const fh of fHits) {
          // 多层高度重叠：候选带行走面高度标注，方便区分各层地板。
          const fhH = floorWalkY(fh.floor);
          const hTag = fhH > 0.005 ? ` · h=${fhH.toFixed(2)} L${floorLayerIndex(fhH)}` : "";
          candidates.push({
            title: `${surfaceKindLabelZh(fh.floor.surfaceKind)} ${fh.floor._wCells}×${fh.floor._dCells}格${hTag}`,
            sub: `地板 · ${isThemedFloor(fh.floor) ? fh.floor.displayName : (fh.floor.materialName ?? "无材质")}`,
            onPick: () => {
              setFloorSelection([fh.floor._key]);
              clearSelection();
              draw();
            },
          });
        }
        for (const it of surfaceHits) {
          candidates.push({
            title: itemLabel(it),
            sub: `${surfaceKindLabelZh(S.catalogByGuid.get(it.prefabGuid)?.surfaceKind)} · ${prefabIdFromPath(it.prefabAssetPath)}`,
            onPick: () => {
              clearFloorSelection();
              setSelection([it._editorKey]);
              draw();
            },
          });
        }
        showPickTip(candidates, e.clientX, e.clientY);
        draw();
        return;
      }
      // 聚焦优先级：已选中的地板 > 已选中的地板层物品 > 最上层地板。
      // 否则重叠时点击已选中的物品（如压力开关）会被其下方的地板抢走焦点。
      const fHit = keepFloor ?? (keepItem ? null : fHits[0]) ?? null;
      if (fHit) {
        S.dragSnapshot = snapshotState();
        clearSelection();
        if (e.shiftKey) {
          const next = new Set(S.selectedFloorKeys);
          if (next.has(fHit.floor._key)) next.delete(fHit.floor._key);
          else next.add(fHit.floor._key);
          setFloorSelection([...next], fHit.floor._key);
        } else if (S.selectedFloorKeys.size > 1 && S.selectedFloorKeys.has(fHit.floor._key)) {
          S.selectedFloorKey = fHit.floor._key;
          S.dragFloorKey = fHit.floor._key;
          S.dragFloorMode = "move";
          S.dragFloorEdge = "";
          S.dragFloorGroupKeys = [...S.selectedFloorKeys];
          S.dragLastWx = wx;
          S.dragLastWz = wz;
        } else {
          setFloorSelection([fHit.floor._key]);
          S.dragFloorKey = fHit.floor._key;
          S.dragFloorMode = fHit.mode;
          S.dragFloorEdge = fHit.edge;
          S.dragFloorAnchorX = fHit.anchorX;
          S.dragFloorAnchorZ = fHit.anchorZ;
          S.dragFloorGroupKeys = [];
          if (fHit.mode === "resize") dom.canvas.style.cursor = "grabbing";
        }
      } else {
        clearFloorSelection();
        const itemHit = keepItem ?? surfaceHits[0] ?? null;
        if (itemHit) {
          S.dragSnapshot = snapshotState();
          setSelection([itemHit._editorKey]);
          S.dragItemKey = itemHit._editorKey;
          S.dragGroupKeys = [];
          S.dragOffsetX = wx - itemHit._wx;
          S.dragOffsetZ = wz - itemHit._wz;
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
      }
      draw();
      return;
    }

    const hits = hitTestAll(wx, wz);
    // After a marquee multi-select, clicking any selected item keeps the group
    // (and starts a group drag) instead of popping the overlap picker.
    const groupHit = S.selectedKeys.size > 1 ? hits.find((it) => isSelected(it._editorKey)) : undefined;
    const already = groupHit ?? (S.selectedKey ? hits.find((it) => it._editorKey === S.selectedKey) : undefined);
    if (hits.length > 1 && !e.shiftKey && !already) {
      hideDetail();
      hideContextMenu();
      showPickTip(
        hits.map((it) => ({
          title: itemLabel(it),
          sub: prefabIdFromPath(it.prefabAssetPath) || "—",
          onPick: () => {
            setSelection([it._editorKey]);
            draw();
          },
        })),
        e.clientX,
        e.clientY
      );
      draw();
      return;
    }
    const hit = already ?? hits[0] ?? null;
    if (hit) {
      S.dragSnapshot = snapshotState();
      hideDetail();
      hideContextMenu();
      if (e.shiftKey) {
        if (isSelected(hit._editorKey)) S.selectedKeys.delete(hit._editorKey);
        else S.selectedKeys.add(hit._editorKey);
        S.selectedKey = hit._editorKey;
        S.dragItemKey = isSelected(hit._editorKey) ? hit._editorKey : null;
        S.dragGroupKeys = S.selectedKeys.size > 1 && S.dragItemKey ? selectionKeys() : [];
        if (S.dragItemKey) {
          S.dragOffsetX = wx - hit._wx;
          S.dragOffsetZ = wz - hit._wz;
          S.dragLastWx = hit._wx;
          S.dragLastWz = hit._wz;
        }
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
        : undefined;
      const keepItem = S.selectedKey
        ? surfaceHits.find((it) => it._editorKey === S.selectedKey)
        : undefined;
      if (fHits.length + surfaceHits.length > 1 && !keepFloor && !keepItem) {
        hideDetail();
        hideContextMenu();
        const candidates: PickCandidate[] = [];
        for (const fh of fHits) {
          const fhH = floorWalkY(fh.floor);
          const hTag = fhH > 0.005 ? ` · h=${fhH.toFixed(2)} L${floorLayerIndex(fhH)}` : "";
          candidates.push({
            title: `${surfaceKindLabelZh(fh.floor.surfaceKind)} ${fh.floor._wCells}×${fh.floor._dCells}格${hTag}`,
            sub: `地板 · ${isThemedFloor(fh.floor) ? fh.floor.displayName : (fh.floor.materialName ?? "无材质")}`,
            onPick: () => {
              setFloorSelection([fh.floor._key]);
              clearSelection();
              openFloorEditorModal(fh.floor);
              draw();
            },
          });
        }
        for (const it of surfaceHits) {
          candidates.push({
            title: itemLabel(it),
            sub: `${surfaceKindLabelZh(S.catalogByGuid.get(it.prefabGuid)?.surfaceKind)} · ${prefabIdFromPath(it.prefabAssetPath)}`,
            onPick: () => {
              clearFloorSelection();
              setSelection([it._editorKey]);
              hideDetail();
              showContextMenu(it, e.clientX, e.clientY);
              draw();
            },
          });
        }
        showPickTip(candidates, e.clientX, e.clientY);
        draw();
        return;
      }
      // 聚焦优先级：已选中的地板 > 已选中的地板层物品 > 最上层地板。
      // 否则重叠时点击已选中的物品（如压力开关）会被其下方的地板抢走焦点。
      const fHit = keepFloor ?? (keepItem ? null : fHits[0]) ?? null;
      if (fHit) {
        setFloorSelection([fHit.floor._key]);
        openFloorEditorModal(fHit.floor);
        draw();
        return;
      }
      const itemHit = keepItem ?? surfaceHits[0] ?? null;
      if (itemHit) {
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
      hideDetail();
      hideContextMenu();
      showPickTip(
        hits.map((it) => ({
          title: itemLabel(it),
          sub: prefabIdFromPath(it.prefabAssetPath) || "—",
          onPick: () => {
            setSelection([it._editorKey]);
            hideDetail();
            showContextMenu(it, e.clientX, e.clientY);
            draw();
          },
        })),
        e.clientX,
        e.clientY
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
      showContextMenu(hit, e.clientX, e.clientY);
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
        if (!S.marqueeing && !S.panning && !S.dragFloorKey && !S.dragItemKey) draw();
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
      if (S.dragFloorGroupKeys.length > 1) {
        const { x: wx, z: wz } = canvasToWorld(mx, my);
        const dx = wx - S.dragLastWx;
        const dz = wz - S.dragLastWz;
        for (const k of S.dragFloorGroupKeys) {
          const f = S.floors.find((x) => x._key === k);
          if (f) {
            f._wx += dx;
            f._wz += dz;
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

    if (!S.dragItemKey) {
      if ((S.currentLayer === "floor" || S.currentLayer === "background") && !S.pendingNewFloor) {
        const { x: wx, z: wz } = canvasToWorld(mx, my);
        const fHits = hitTestFloorsAll(wx, wz);
        const cursor = fHits[0]?.mode === "resize" ? "grab" : "";
        if (dom.canvas.style.cursor !== cursor) dom.canvas.style.cursor = cursor;
      }
      return;
    }
    const item = S.items.find((i) => i._editorKey === S.dragItemKey);
    if (!item) return;
    const { x: wx, z: wz } = canvasToWorld(mx, my);
    if (S.dragGroupKeys.length > 1) {
      const newWx = wx - S.dragOffsetX;
      const newWz = wz - S.dragOffsetZ;
      const dx = newWx - S.dragLastWx;
      const dz = newWz - S.dragLastWz;
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
      const keys = S.dragFloorGroupKeys.length > 1 ? S.dragFloorGroupKeys : [S.dragFloorKey];
      for (const k of keys) {
        const f = S.floors.find((x) => x._key === k);
        if (f) finalizeFloor(f);
      }
    }
    S.dragFloorKey = null;
    S.dragFloorGroupKeys = [];
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
    if (S.dragGroupKeys.length > 1) {
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
    addFromCatalog(cat, wx, wz);
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
        nudgeSelectedItems(dx, dz);
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
      S.marqueeing = false;
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
        if (S.selectedFloorKeys.size === 1 && S.selectedFloorKey) {
          const f = S.floors.find((x) => x._key === S.selectedFloorKey);
          if (f) {
            pushHistory();
            f.localRotationY = normalizeRot(f.localRotationY + (e.shiftKey ? -90 : 90));
            draw();
          }
          return;
        }
        if (S.selectedFloorKeys.size === 0 && S.selectedKey) {
          const item = S.items.find((i) => i._editorKey === S.selectedKey);
          if (item && isSurfaceItem(S.catalogByGuid.get(item.prefabGuid))) {
            pushHistory();
            item.localRotationY = normalizeRot(item.localRotationY + (e.shiftKey ? -90 : 90));
            syncLocalFromWorld(item);
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
      pushHistory();
      item.localRotationY = normalizeRot(item.localRotationY + (e.shiftKey ? -90 : 90));
      syncLocalFromWorld(item);
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
