import { uuid } from "../coords";
import {
  S,
  EditorItem
} from "../state";
import type { MoveGroup } from "../../types";
import { dom } from "../dom";
import {
  normalizeRot,
  stepDecimals,
  stepDisplayDecimals,
  snapValue,
  itemUniformScale,
  setItemUniformScale,
  escHtml
} from "../coords";
import { itemLabel } from "../labels";
import { itemCategoryOf, isResizableBackgroundItem, itemPlaneCells } from "../catalog";
import { setItemPlaneSize, airWallCells, setAirWallSize } from "../items";
import { isAirWallItem } from "../stubControls";
import { isSurfaceItem } from "../../floorColors";
import {
  isPlayerItem,
  paramBadgeInfo
} from "../renderItems";
import {
  stubControlsHtml,
  counterAppearanceHtml,
  wireStubControls
} from "../stubControls";
import {
  nudgeItem,
  syncLocalFromWorld,
  deleteSelected,
  updateCtxCoord,
  rotateItemByDelta
} from "../items";
import {
  copySelection,
  cutSelection,
  pasteClipboard
} from "../clipboard";
import { pushHistory } from "../historyOps";
import { draw } from "../render";
import { setSelection } from "../selection";
import { hideContextMenu } from "./overlay";
import {
  showSurfaceItemDetail,
  showDetail
} from "./detailPanel";
import {
  renderRightPanel,
  updatePanelTabButtons
} from "../panels";
import { findGroupByItemId, waypointInfo, deleteWaypoint } from "../moveControl";
import { itemVariantHtml, wireItemVariant } from "../itemVariants";
import {
  selectionHeightRowHtml,
  selectionHeightTargetCount,
  wireSelectionHeightRow
} from "../selectionHeight";
import {
  selectionTravelatorSpeedRowHtml,
  wireSelectionTravelatorSpeedRow,
} from "../selectionTravelator";
import {
  airWallHeightRowHtml,
  selectionAirWallHeightRowHtml,
  wireAirWallHeightRow,
  wireSelectionAirWallHeightRow,
} from "../selectionAirWallHeight";
import {
  batchRotationRowHtml,
  batchDisperseRowHtml,
  batchNudgeRowLabel,
  batchNudgeSelected,
  isBatchTransform,
  wireBatchRotationRow,
  wireBatchDisperseRow,
} from "../selectionTransform";
import { updateFloorBar } from "../floorPalette";

export function moveControlCtxHtml(item: EditorItem): string {
  // Players are not movable; pure background (water/sky) is not a move member.
  if (isPlayerItem(item)) return "";
  if (itemCategoryOf(item) === "background") return "";
  const has = findGroupByItemId(item.instanceId);
  if (has) {
    return `<button type="button" class="ctx-btn" data-act="edit-move">编辑移动组</button>`;
  }
  return `<button type="button" class="ctx-btn" data-act="enable-move">创建移动组</button>`;
}

export function enableMoveControl(item: EditorItem): void {
  pushHistory();
  const group: MoveGroup = {
    id: uuid(),
    displayName: itemLabel(item),
    itemInstanceIds: [item.instanceId],
    floorInstanceIds: [],
    objectInstanceIds: [],
    memberOffsets: [],
    memberStatic: [],
    memberGroups: [],
    startDelay: 0,
    loop: false,
    loopDelay: 2,
    waypoints: [{ id: uuid(), x: item._wx, z: item._wz }],
    events: [{
      id: uuid(),
      type: "move",
      delay: 0,
      intervalSeconds: 2,
      waypointIds: [],
    }],
  };
  // The spawn waypoint is already part of the route so the bake is valid.
  group.events[0].waypointIds = [group.waypoints[0].id];
  S.moveControls.push(group);
  S.activeMoveGroupId = group.id;
  S.activeMoveEventIdx = 0;
  S.activeMoveTab = "members";
  S.moveMode = "none";
  S.activeRightTab = "move";
  updatePanelTabButtons();
  S.dirty = true;
  renderRightPanel();
  draw();
}

/** 多选地板/物品时仅显示批量高度菜单（地板层右键）。 */
export function showBatchHeightMenu(clientX: number, clientY: number) {
  if (selectionHeightTargetCount() < 2) return;
  const batch = isBatchTransform();
  const step = S.freeSnapStep;
  const nudgeLabel = batchNudgeRowLabel() || `微移 ${step.toFixed(stepDecimals(step))}`;
  dom.ctxMenuEl.innerHTML = `
    <div class="ctx-head">批量调整</div>
    ${
      batch
        ? `<div class="ctx-nudge-row">
      <span class="ctx-label">${nudgeLabel}</span>
      <div class="ctx-nudge">
        <button type="button" data-batch-nudge="-${step},0" title="左移 ${step}">←</button>
        <button type="button" data-batch-nudge="0,${step}" title="上移 ${step}">↑</button>
        <button type="button" data-batch-nudge="0,-${step}" title="下移 ${step}">↓</button>
        <button type="button" data-batch-nudge="${step},0" title="右移 ${step}">→</button>
      </div>
    </div>
    ${batchRotationRowHtml()}
    ${batchDisperseRowHtml()}`
        : ""
    }
    ${selectionHeightRowHtml()}
    ${selectionTravelatorSpeedRowHtml()}
    ${selectionAirWallHeightRowHtml()}
    <p class="close-hint">地板按行走面高度 · 物品按本地 Y · 空气墙碰撞高度按格 · Esc 关闭</p>
  `;
  const margin = 8;
  let left = clientX + margin;
  let top = clientY + margin;
  dom.ctxMenuEl.classList.remove("hidden");
  dom.ctxMenuEl.style.left = `${left}px`;
  dom.ctxMenuEl.style.top = `${top}px`;
  requestAnimationFrame(() => {
    const rect = dom.ctxMenuEl.getBoundingClientRect();
    if (rect.right > window.innerWidth - margin) {
      left = Math.max(margin, clientX - rect.width - margin);
      dom.ctxMenuEl.style.left = `${left}px`;
    }
    if (rect.bottom > window.innerHeight - margin) {
      top = Math.max(margin, clientY - rect.height - margin);
      dom.ctxMenuEl.style.top = `${top}px`;
    }
  });
  wireSelectionHeightRow(dom.ctxMenuEl, () => {
    draw();
    updateFloorBar();
  });
  wireSelectionTravelatorSpeedRow(dom.ctxMenuEl, () => {
    draw();
    updateFloorBar();
  });
  wireSelectionAirWallHeightRow(dom.ctxMenuEl, () => {
    draw();
    updateFloorBar();
  });
  wireBatchRotationRow(dom.ctxMenuEl, () => {
    draw();
    updateFloorBar();
  });
  wireBatchDisperseRow(dom.ctxMenuEl, () => {
    draw();
    updateFloorBar();
  });
  dom.ctxMenuEl.querySelectorAll<HTMLButtonElement>("[data-batch-nudge]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const parts = btn.dataset.batchNudge!.split(",").map(Number);
      batchNudgeSelected(parts[0] || 0, parts[1] || 0);
      draw();
      updateFloorBar();
    });
  });
}

export function showContextMenu(item: EditorItem, clientX: number, clientY: number) {
  const cat = S.catalogByGuid.get(item.prefabGuid);
  const isSurface = isSurfaceItem(cat);
  const isPlayer = isPlayerItem(item);
  const batchHeight = selectionHeightTargetCount() >= 2;
  const batchTransform = isBatchTransform();
  const resizable = isResizableBackgroundItem(item) || isAirWallItem(item);
  const { wCells, dCells } = isAirWallItem(item) ? airWallCells(item) : itemPlaneCells(item);
  const stubHtml = stubControlsHtml(item);
  const appearHtml = counterAppearanceHtml(item);
  const rot = normalizeRot(item.localRotationY);
  // Prepend the per-type sequence badge (e.g. 上菜台1) to the header for parameterized items.
  const pInfo = paramBadgeInfo(item);
  const pNum = pInfo ? S.paramLabels.get(item.instanceId) : undefined;
  const headBadge =
    pInfo && pNum
      ? `<span class="ctx-num-tag" style="color:${S.paramColors.get(item.instanceId) ?? "#f9ab00"}">${pInfo.type}${pNum}</span> `
      : "";

  dom.ctxMenuEl.innerHTML = `
    <div class="ctx-head">${headBadge}${itemLabel(item)}</div>
    <div class="ctx-coord" id="ctx-coord">x ${item._wx.toFixed(stepDisplayDecimals(S.freeSnapStep))} · z ${item._wz.toFixed(stepDisplayDecimals(S.freeSnapStep))}</div>
    ${
      isPlayer
        ? ""
        : `<div class="ctx-nudge-row">
      <span class="ctx-label">坐标(世界)</span>
      <div class="ctx-nudge">
        <input type="number" id="ctx-x-input" class="ctx-input ctx-pos-input" step="${S.freeSnapStep}" value="${item._wx.toFixed(stepDisplayDecimals(S.freeSnapStep))}" title="世界坐标 X（回车生效）" />
        <input type="number" id="ctx-z-input" class="ctx-input ctx-pos-input" step="${S.freeSnapStep}" value="${item._wz.toFixed(stepDisplayDecimals(S.freeSnapStep))}" title="世界坐标 Z（回车生效）" />
      </div>
    </div>`
    }
    <div class="ctx-nudge-row">
      <span class="ctx-label">${batchTransform ? batchNudgeRowLabel() || `微移 ${S.freeSnapStep.toFixed(stepDecimals(S.freeSnapStep))}` : `微移 ${S.freeSnapStep.toFixed(stepDecimals(S.freeSnapStep))}`}</span>
      <div class="ctx-nudge">
        <button type="button" data-nudge="-${S.freeSnapStep},0" title="左移 ${S.freeSnapStep}">←</button>
        <button type="button" data-nudge="0,${S.freeSnapStep}" title="上移 ${S.freeSnapStep}">↑</button>
        <button type="button" data-nudge="0,-${S.freeSnapStep}" title="下移 ${S.freeSnapStep}">↓</button>
        <button type="button" data-nudge="${S.freeSnapStep},0" title="右移 ${S.freeSnapStep}">→</button>
      </div>
    </div>
    ${
      isPlayer
        ? ""
        : batchHeight
          ? selectionHeightRowHtml()
          : `<div class="ctx-nudge-row">
      <span class="ctx-label">高度 <span id="ctx-y-val" class="ctx-scale-val">${item.localPosition.y.toFixed(stepDisplayDecimals(S.freeSnapStep))}</span></span>
      <div class="ctx-nudge">
        <button type="button" data-nudge="0,0,-${S.freeSnapStep}" title="降低 ${S.freeSnapStep}">−${S.freeSnapStep.toFixed(stepDecimals(S.freeSnapStep))}</button>
        <input type="number" id="ctx-y-input" class="ctx-input ctx-pos-input" step="${S.freeSnapStep}" value="${item.localPosition.y.toFixed(stepDisplayDecimals(S.freeSnapStep))}" title="高度 Y（回车生效）" />
        <button type="button" data-nudge="0,0,${S.freeSnapStep}" title="升高 ${S.freeSnapStep}">+${S.freeSnapStep.toFixed(stepDecimals(S.freeSnapStep))}</button>
      </div>
    </div>`
    }
    ${
      isPlayer
        ? ""
        : batchTransform
          ? `${batchRotationRowHtml()}${batchDisperseRowHtml()}`
          : `<div class="ctx-nudge-row">
      <span class="ctx-label">旋转 <span id="ctx-rot" class="ctx-scale-val">${rot}°</span></span>
      <div class="ctx-nudge">
        <button type="button" data-rot="-90" title="逆时针 90°">−90°</button>
        <input type="number" id="ctx-rot-input" class="ctx-input ctx-rot-input" min="0" max="359" step="1" value="${rot}" title="任意角度 (0~359)" />
        <button type="button" data-rot="90" title="顺时针 90°">+90°</button>
      </div>
    </div>`
    }
    ${selectionTravelatorSpeedRowHtml()}
    ${selectionAirWallHeightRowHtml()}
    ${
      isSurface && !resizable
        ? `<div class="ctx-nudge-row">
      <span class="ctx-label">缩放</span>
      <div class="ctx-nudge">
        <button type="button" data-scale="-0.5" title="缩小">−</button>
        <span id="ctx-scale" class="ctx-scale-val">${itemUniformScale(item).toFixed(1)}×</span>
        <button type="button" data-scale="0.5" title="放大">+</button>
      </div>
    </div>`
        : ""
    }
    ${
      resizable
        ? `<div class="ctx-nudge-row">
      <span class="ctx-label">大小(格)</span>
      <div class="ctx-nudge">
        <input type="number" id="ctx-bw-input" class="ctx-input ctx-pos-input" min="1" step="1" value="${wCells}" title="宽(格)（回车生效）" />
        <span class="ctx-scale-val">×</span>
        <input type="number" id="ctx-bd-input" class="ctx-input ctx-pos-input" min="1" step="1" value="${dCells}" title="高(格)（回车生效）" />
      </div>
    </div>`
        : ""
    }
    ${isAirWallItem(item) && !batchHeight ? airWallHeightRowHtml(item) : ""}
    ${stubHtml}
    ${appearHtml}
    ${itemVariantHtml(item)}
    <div class="ctx-actions">
      <div class="ctx-actions-row">
        <button type="button" class="ctx-btn" data-act="detail">详情…</button>
        ${isPlayer ? "" : `<button type="button" class="ctx-btn" data-act="copy" title="Ctrl+C">复制</button>
        <button type="button" class="ctx-btn" data-act="cut" title="Ctrl+X">裁切</button>`}
        <button type="button" class="ctx-btn" data-act="paste" title="Ctrl+V">粘贴</button>
      </div>
      ${
        isPlayer
          ? ""
          : `<div class="ctx-actions-row">
        ${!isSurface ? moveControlCtxHtml(item) : ""}
        <button type="button" class="ctx-btn danger" data-act="delete">删除</button>
      </div>`
      }
    </div>
    <p class="close-hint">点击外部或 Esc 关闭</p>
  `;

  const margin = 8;
  let left = clientX + margin;
  let top = clientY + margin;
  dom.ctxMenuEl.classList.remove("hidden");
  dom.ctxMenuEl.style.left = `${left}px`;
  dom.ctxMenuEl.style.top = `${top}px`;
  requestAnimationFrame(() => {
    const rect = dom.ctxMenuEl.getBoundingClientRect();
    if (rect.right > window.innerWidth - margin) {
      left = Math.max(margin, clientX - rect.width - margin);
      dom.ctxMenuEl.style.left = `${left}px`;
    }
    if (rect.bottom > window.innerHeight - margin) {
      top = Math.max(margin, clientY - rect.height - margin);
      dom.ctxMenuEl.style.top = `${top}px`;
    }
  });

  const refreshCtxPosInputs = () => {
    const xInp = document.getElementById("ctx-x-input") as HTMLInputElement | null;
    const zInp = document.getElementById("ctx-z-input") as HTMLInputElement | null;
    const yInp = document.getElementById("ctx-y-input") as HTMLInputElement | null;
    const d = stepDisplayDecimals(S.freeSnapStep);
    if (xInp && document.activeElement !== xInp) xInp.value = item._wx.toFixed(d);
    if (zInp && document.activeElement !== zInp) zInp.value = item._wz.toFixed(d);
    if (yInp && document.activeElement !== yInp) yInp.value = item.localPosition.y.toFixed(d);
  };
  dom.ctxMenuEl.querySelectorAll<HTMLButtonElement>("[data-nudge]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const parts = btn.dataset.nudge!.split(",").map(Number);
      if (batchTransform) {
        batchNudgeSelected(parts[0] || 0, parts[1] || 0, parts[2] || 0);
        draw();
        updateFloorBar();
        return;
      }
      nudgeItem(item, parts[0] || 0, parts[1] || 0, parts[2] || 0);
      const yEl = document.getElementById("ctx-y-val");
      if (yEl) yEl.textContent = item.localPosition.y.toFixed(stepDisplayDecimals(S.freeSnapStep));
      refreshCtxPosInputs();
    });
  });
  const applyWorldPos = () => {
    const xInp = document.getElementById("ctx-x-input") as HTMLInputElement | null;
    const zInp = document.getElementById("ctx-z-input") as HTMLInputElement | null;
    if (!xInp || !zInp) return;
    const x = parseFloat(xInp.value);
    const z = parseFloat(zInp.value);
    if (!isFinite(x) || !isFinite(z)) return;
    pushHistory();
    item._wx = x;
    item._wz = z;
    syncLocalFromWorld(item);
    updateCtxCoord(item);
    refreshCtxPosInputs();
    draw();
  };
  document.getElementById("ctx-x-input")?.addEventListener("change", applyWorldPos);
  document.getElementById("ctx-z-input")?.addEventListener("change", applyWorldPos);
  if (batchHeight) {
    wireSelectionHeightRow(dom.ctxMenuEl, () => {
      draw();
      updateFloorBar();
    });
  }
  wireSelectionTravelatorSpeedRow(dom.ctxMenuEl, () => {
    draw();
    updateFloorBar();
  });
  if (isAirWallItem(item) && !batchHeight) {
    wireAirWallHeightRow(dom.ctxMenuEl, item);
  }
  wireSelectionAirWallHeightRow(dom.ctxMenuEl, () => {
    draw();
    updateFloorBar();
  });
  if (batchTransform) {
    wireBatchRotationRow(dom.ctxMenuEl, () => {
      draw();
      updateFloorBar();
    });
    wireBatchDisperseRow(dom.ctxMenuEl, () => {
      draw();
      updateFloorBar();
    });
  }
  const yInput = document.getElementById("ctx-y-input") as HTMLInputElement | null;
  yInput?.addEventListener("change", () => {
    const y = parseFloat(yInput.value);
    if (!isFinite(y)) return;
    pushHistory();
    item.localPosition.y = snapValue(y, S.freeSnapStep);
    const yEl = document.getElementById("ctx-y-val");
    if (yEl) yEl.textContent = item.localPosition.y.toFixed(stepDisplayDecimals(S.freeSnapStep));
    updateCtxCoord(item);
    refreshCtxPosInputs();
    draw();
  });
  const applyItemSize = () => {
    const bwInp = document.getElementById("ctx-bw-input") as HTMLInputElement | null;
    const bdInp = document.getElementById("ctx-bd-input") as HTMLInputElement | null;
    if (!bwInp || !bdInp) return;
    const wv = parseInt(bwInp.value, 10);
    const dv = parseInt(bdInp.value, 10);
    if (!(wv > 0) && !(dv > 0)) return;
    const cur = isAirWallItem(item) ? airWallCells(item) : itemPlaneCells(item);
    pushHistory();
    if (isAirWallItem(item)) setAirWallSize(item, wv > 0 ? wv : cur.wCells, dv > 0 ? dv : cur.dCells);
    else setItemPlaneSize(item, wv > 0 ? wv : cur.wCells, dv > 0 ? dv : cur.dCells);
    S.dirty = true;
    draw();
  };
  document.getElementById("ctx-bw-input")?.addEventListener("change", applyItemSize);
  document.getElementById("ctx-bd-input")?.addEventListener("change", applyItemSize);
  dom.ctxMenuEl.querySelectorAll<HTMLButtonElement>("[data-scale]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const delta = parseFloat(btn.dataset.scale!);
      const next = Math.max(0.5, +(itemUniformScale(item) + delta).toFixed(2));
      pushHistory();
      setItemUniformScale(item, next);
      const scaleEl = document.getElementById("ctx-scale");
      if (scaleEl) scaleEl.textContent = `${next.toFixed(1)}×`;
      draw();
    });
  });
  let rotInputPushed = false;
  const applyRotationAbsolute = (deg: number) => {
    if (!isFinite(deg)) return;
    const target = normalizeRot(deg);
    const delta = normalizeRot(target - item.localRotationY);
    if (delta === 0) return;
    if (!rotInputPushed) {
      pushHistory();
      rotInputPushed = true;
    }
    rotateItemByDelta(item, delta);
    S.dirty = true;
    const lbl = document.getElementById("ctx-rot");
    if (lbl) lbl.textContent = `${normalizeRot(item.localRotationY)}°`;
    draw();
  };
  const applyRotationDelta = (delta: number) => {
    if (!delta || !isFinite(delta)) return;
    pushHistory();
    rotateItemByDelta(item, delta);
    S.dirty = true;
    const lbl = document.getElementById("ctx-rot");
    if (lbl) lbl.textContent = `${normalizeRot(item.localRotationY)}°`;
    const inp = document.getElementById("ctx-rot-input") as HTMLInputElement | null;
    if (inp) inp.value = String(normalizeRot(item.localRotationY));
    draw();
  };
  if (!batchTransform) {
    dom.ctxMenuEl.querySelectorAll<HTMLButtonElement>("[data-rot]").forEach((btn) => {
      btn.addEventListener("click", () => {
        applyRotationDelta(parseFloat(btn.dataset.rot!));
      });
    });
    const rotInput = document.getElementById("ctx-rot-input") as HTMLInputElement | null;
    const onRotInput = () => applyRotationAbsolute(parseFloat(rotInput?.value ?? ""));
    rotInput?.addEventListener("input", onRotInput);
    rotInput?.addEventListener("change", onRotInput);
  }
  wireStubControls(item);
  wireItemVariant(item);
  dom.ctxMenuEl.querySelector('[data-act="detail"]')?.addEventListener("click", () => {
    if ((S.currentLayer === "floor" || S.currentLayer === "background") && isSurface) {
      showSurfaceItemDetail(item, clientX, clientY);
    } else {
      showDetail(item, clientX, clientY);
    }
    hideContextMenu();
  });
  dom.ctxMenuEl.querySelector('[data-act="enable-move"]')?.addEventListener("click", () => {
    enableMoveControl(item);
    hideContextMenu();
  });
  dom.ctxMenuEl.querySelector('[data-act="edit-move"]')?.addEventListener("click", () => {
    const group = findGroupByItemId(item.instanceId);
    S.activeMoveGroupId = group?.id ?? null;
    S.activeMoveEventIdx = 0;
    S.activeMoveTab = "members";
    S.moveMode = "none";
    S.activeRightTab = "move";
    updatePanelTabButtons();
    setSelection([item._editorKey]);
    hideContextMenu();
    renderRightPanel();
    draw();
  });
  dom.ctxMenuEl.querySelector('[data-act="delete"]')?.addEventListener("click", () => deleteSelected());
  dom.ctxMenuEl.querySelector('[data-act="copy"]')?.addEventListener("click", () => {
    copySelection();
  });
  dom.ctxMenuEl.querySelector('[data-act="cut"]')?.addEventListener("click", () => {
    hideContextMenu();
    cutSelection();
  });
  dom.ctxMenuEl.querySelector('[data-act="paste"]')?.addEventListener("click", () => {
    hideContextMenu();
    const rect = dom.canvas.getBoundingClientRect();
    pasteClipboard(clientX - rect.left, clientY - rect.top);
  });
}

/** Waypoint right-click menu: world coordinates + precision nudge, exactly like
 *  items, plus route/delete actions. */
export function showWaypointContextMenu(wpId: string, clientX: number, clientY: number): void {
  const info = waypointInfo(wpId);
  if (!info) return;
  const { group, wp, num } = info;
  const d = stepDisplayDecimals(S.freeSnapStep);
  const step = S.freeSnapStep;
  // Route actions only make sense for the active group's event.
  const evt =
    group.id === S.activeMoveGroupId && S.activeMoveEventIdx !== null
      ? group.events[S.activeMoveEventIdx]
      : undefined;
  const evtMove = evt?.type === "move";
  const inRoute = !!evtMove && !!evt.waypointIds?.includes(wpId);

  dom.ctxMenuEl.innerHTML = `
    <div class="ctx-head">路点 #${num} <span class="ctx-head-sub">${escHtml(group.displayName)}</span></div>
    <div class="ctx-coord">x ${wp.x.toFixed(d)} · z ${wp.z.toFixed(d)}</div>
    <div class="ctx-nudge-row">
      <span class="ctx-label">坐标(世界)</span>
      <div class="ctx-nudge">
        <input type="number" id="wp-ctx-x" class="ctx-input ctx-pos-input" step="${step}" value="${wp.x.toFixed(d)}" title="世界坐标 X（回车生效）" />
        <input type="number" id="wp-ctx-z" class="ctx-input ctx-pos-input" step="${step}" value="${wp.z.toFixed(d)}" title="世界坐标 Z（回车生效）" />
      </div>
    </div>
    <div class="ctx-nudge-row">
      <span class="ctx-label">微移 ${step.toFixed(stepDecimals(step))}</span>
      <div class="ctx-nudge">
        <button type="button" data-wp-nudge="-${step},0" title="左移 ${step}">←</button>
        <button type="button" data-wp-nudge="0,${step}" title="上移 ${step}">↑</button>
        <button type="button" data-wp-nudge="0,-${step}" title="下移 ${step}">↓</button>
        <button type="button" data-wp-nudge="${step},0" title="右移 ${step}">→</button>
      </div>
    </div>
    <div class="ctx-actions">
      ${evtMove && !inRoute ? `<button type="button" class="ctx-btn" data-wp-act="add-route">＋ 编入当前事件路线</button>` : ""}
      ${inRoute ? `<button type="button" class="ctx-btn" data-wp-act="remove-route">从当前事件路线移除</button>` : ""}
      <button type="button" class="ctx-btn danger" data-wp-act="delete">删除路点</button>
    </div>
    <p class="close-hint">点击外部或 Esc 关闭</p>
  `;

  const margin = 8;
  let left = clientX + margin;
  let top = clientY + margin;
  dom.ctxMenuEl.classList.remove("hidden");
  dom.ctxMenuEl.style.left = `${left}px`;
  dom.ctxMenuEl.style.top = `${top}px`;
  requestAnimationFrame(() => {
    const rect = dom.ctxMenuEl.getBoundingClientRect();
    if (rect.right > window.innerWidth - margin) {
      left = Math.max(margin, clientX - rect.width - margin);
      dom.ctxMenuEl.style.left = `${left}px`;
    }
    if (rect.bottom > window.innerHeight - margin) {
      top = Math.max(margin, clientY - rect.height - margin);
      dom.ctxMenuEl.style.top = `${top}px`;
    }
  });

  const refreshInputs = () => {
    const xi = document.getElementById("wp-ctx-x") as HTMLInputElement | null;
    const zi = document.getElementById("wp-ctx-z") as HTMLInputElement | null;
    if (xi && document.activeElement !== xi) xi.value = wp.x.toFixed(d);
    if (zi && document.activeElement !== zi) zi.value = wp.z.toFixed(d);
  };
  const commit = () => {
    pushHistory();
    S.dirty = true;
    refreshInputs();
    renderRightPanel();
    draw();
  };
  dom.ctxMenuEl.querySelectorAll<HTMLButtonElement>("[data-wp-nudge]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const [dx, dz] = btn.dataset.wpNudge!.split(",").map(Number);
      wp.x = snapValue(wp.x + (dx || 0), S.freeSnapStep);
      wp.z = snapValue(wp.z + (dz || 0), S.freeSnapStep);
      commit();
    });
  });
  const applyWorldPos = () => {
    const xi = document.getElementById("wp-ctx-x") as HTMLInputElement | null;
    const zi = document.getElementById("wp-ctx-z") as HTMLInputElement | null;
    if (!xi || !zi) return;
    const x = parseFloat(xi.value);
    const z = parseFloat(zi.value);
    if (!isFinite(x) || !isFinite(z)) return;
    wp.x = snapValue(x, S.freeSnapStep);
    wp.z = snapValue(z, S.freeSnapStep);
    commit();
  };
  document.getElementById("wp-ctx-x")?.addEventListener("change", applyWorldPos);
  document.getElementById("wp-ctx-z")?.addEventListener("change", applyWorldPos);
  dom.ctxMenuEl.querySelector('[data-wp-act="delete"]')?.addEventListener("click", () => {
    pushHistory();
    deleteWaypoint(group, wpId);
    hideContextMenu();
    renderRightPanel();
    draw();
  });
  dom.ctxMenuEl.querySelector('[data-wp-act="add-route"]')?.addEventListener("click", () => {
    const e = S.activeMoveEventIdx !== null ? group.events[S.activeMoveEventIdx] : undefined;
    if (e && e.type === "move") {
      if (!e.waypointIds) e.waypointIds = [];
      if (!e.waypointIds.includes(wpId)) {
        pushHistory();
        e.waypointIds.push(wpId);
        S.dirty = true;
        renderRightPanel();
        draw();
      }
    }
    hideContextMenu();
  });
  dom.ctxMenuEl.querySelector('[data-wp-act="remove-route"]')?.addEventListener("click", () => {
    const e = S.activeMoveEventIdx !== null ? group.events[S.activeMoveEventIdx] : undefined;
    if (e && e.type === "move" && e.waypointIds) {
      const ri = e.waypointIds.indexOf(wpId);
      if (ri >= 0) {
        pushHistory();
        e.waypointIds.splice(ri, 1);
        S.dirty = true;
        renderRightPanel();
        draw();
      }
    }
    hideContextMenu();
  });
}
