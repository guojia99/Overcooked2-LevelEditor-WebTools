import { CELL, S, EditorFloor, EditorItem } from "./state";
import { snapValue, stepDecimals, stepDisplayDecimals } from "./coords";
import { selectionKeys } from "./selection";
import { isPlayerItem } from "./renderItems";
import { floorWalkY, floorVisualYForWalkHeight } from "./floorHeight";
import { isAirFloor, isThemedFloor } from "./floors";
import { floorLocalPoint } from "./renderFloors";
import { pushHistory } from "./historyOps";
import { setStatus } from "./status";

export function selectedFloorsForHeight(): EditorFloor[] {
  return [...S.selectedFloorKeys]
    .map((k) => S.floors.find((f) => f._key === k))
    .filter((f): f is EditorFloor => !!f);
}

export function selectedItemsForHeight(): EditorItem[] {
  return selectionKeys()
    .map((k) => S.items.find((i) => i._editorKey === k))
    .filter((it): it is EditorItem => !!it && !isPlayerItem(it));
}

export function selectionHeightTargetCount(): number {
  return selectedFloorsForHeight().length + selectedItemsForHeight().length;
}

export function primarySelectionHeight(): number | null {
  if (S.selectedKey) {
    const it = S.items.find((i) => i._editorKey === S.selectedKey);
    if (it && S.selectedKeys.has(it._editorKey) && !isPlayerItem(it)) {
      return it.localPosition?.y ?? 0;
    }
  }
  if (S.selectedFloorKey && S.selectedFloorKeys.has(S.selectedFloorKey)) {
    const f = S.floors.find((x) => x._key === S.selectedFloorKey);
    if (f) return floorWalkY(f);
  }
  const floors = selectedFloorsForHeight();
  if (floors.length) return floorWalkY(floors[0]);
  const items = selectedItemsForHeight();
  if (items.length) return items[0].localPosition?.y ?? 0;
  return null;
}

export function selectionHeightSummary(): {
  floorCount: number;
  itemCount: number;
  mixed: boolean;
  displayY: number | null;
} {
  const floors = selectedFloorsForHeight();
  const items = selectedItemsForHeight();
  const ys: number[] = [];
  for (const f of floors) ys.push(floorWalkY(f));
  for (const it of items) ys.push(it.localPosition?.y ?? 0);
  const mixed =
    ys.length > 1 && ys.some((y) => Math.abs(y - ys[0]) > 1e-4);
  const primary = primarySelectionHeight();
  return {
    floorCount: floors.length,
    itemCount: items.length,
    mixed,
    displayY: primary ?? (ys.length ? ys[0] : null),
  };
}

/** 将一块地板的行走面高度设为 h（与地板编辑弹窗一致；可选抬升板上物品）。 */
export function applyFloorWalkHeight(
  f: EditorFloor,
  h1: number,
  liftItems = false
): number {
  const h0 = floorWalkY(f);
  const target = Math.round(h1 * 100) / 100;
  if (Math.abs(target - h0) < 1e-6) return 0;
  const kind = isAirFloor(f) ? "air" : isThemedFloor(f) ? "themed" : "plane";
  const y = floorVisualYForWalkHeight(target, kind);
  f.localPosition.y = y;
  if (f.worldPosition) f.worldPosition.y = y;
  if (!liftItems) return 0;
  const dy = target - h0;
  const hw = (f._wCells * CELL) / 2;
  const hh = (f._dCells * CELL) / 2;
  let lifted = 0;
  for (const it of S.items) {
    const { lx, lz } = floorLocalPoint(f, it._wx, it._wz);
    if (Math.abs(lx) > hw + 0.01 || Math.abs(lz) > hh + 0.01) continue;
    const iy = it.localPosition?.y ?? 0;
    if (Math.abs(iy - h0) > 0.1) continue;
    it.localPosition.y = snapValue(iy + dy, S.freeSnapStep);
    if (it.worldPosition) it.worldPosition.y = it.localPosition.y;
    lifted++;
  }
  return lifted;
}

/** 批量调整当前选中地板/物品的高度（绝对值或增量）。 */
export function batchAdjustSelectionHeight(opts: {
  absoluteY?: number;
  deltaY?: number;
  liftItemsOnFloors?: boolean;
}): { floors: number; items: number; lifted: number } {
  const floors = selectedFloorsForHeight();
  const items = selectedItemsForHeight();
  if (floors.length + items.length < 2) {
    return { floors: 0, items: 0, lifted: 0 };
  }
  pushHistory();
  let floorN = 0;
  let itemN = 0;
  let lifted = 0;
  const step = S.freeSnapStep;

  if (opts.absoluteY != null && isFinite(opts.absoluteY)) {
    const y = snapValue(opts.absoluteY, step);
    for (const f of floors) {
      if (Math.abs(floorWalkY(f) - y) >= 1e-6) {
        lifted += applyFloorWalkHeight(f, y, !!opts.liftItemsOnFloors);
        floorN++;
      }
    }
    for (const it of items) {
      const cur = it.localPosition?.y ?? 0;
      if (Math.abs(cur - y) >= 1e-6) {
        it.localPosition.y = y;
        if (it.worldPosition) it.worldPosition.y = y;
        itemN++;
      }
    }
  } else if (opts.deltaY != null && isFinite(opts.deltaY) && Math.abs(opts.deltaY) > 1e-9) {
    const dy = opts.deltaY;
    const lift = opts.liftItemsOnFloors !== false;
    for (const f of floors) {
      const next = snapValue(floorWalkY(f) + dy, step);
      if (Math.abs(next - floorWalkY(f)) >= 1e-6) {
        lifted += applyFloorWalkHeight(f, next, lift);
        floorN++;
      }
    }
    for (const it of items) {
      const next = snapValue((it.localPosition?.y ?? 0) + dy, step);
      it.localPosition.y = next;
      if (it.worldPosition) it.worldPosition.y = next;
      itemN++;
    }
  }

  if (floorN + itemN > 0) S.dirty = true;
  return { floors: floorN, items: itemN, lifted };
}

export function selectionHeightRowHtml(): string {
  const { floorCount, itemCount, mixed, displayY } = selectionHeightSummary();
  const total = floorCount + itemCount;
  if (total < 2) return "";
  const step = S.freeSnapStep;
  const d = stepDisplayDecimals(step);
  const dec = stepDecimals(step);
  const val = displayY == null ? "" : displayY.toFixed(d);
  const mixedTag = mixed ? ' <span class="ctx-mixed-tag">混合</span>' : "";
  const parts: string[] = [];
  if (floorCount) parts.push(`${floorCount} 地板`);
  if (itemCount) parts.push(`${itemCount} 物品`);
  return `
    <div class="ctx-nudge-row ctx-batch-row">
      <span class="ctx-label">批量高度${mixedTag} <span class="ctx-scale-val" id="sel-h-count">${parts.join(" · ")}</span></span>
      <div class="ctx-nudge">
        <button type="button" data-sel-h-dy="-${step}" title="全体降低 ${step}">−${step.toFixed(dec)}</button>
        <input type="number" id="sel-h-input" class="ctx-input ctx-pos-input" step="${step}" value="${val}" placeholder="Y" title="统一设为该高度（回车或同步）" />
        <button type="button" data-sel-h-dy="${step}" title="全体升高 ${step}">+${step.toFixed(dec)}</button>
        <button type="button" id="sel-h-sync" class="ctx-sync-btn" title="将全部同步为输入框中的高度">同步</button>
      </div>
    </div>`;
}

/** 绑定批量高度控件（右键菜单 / 地板栏共用）。 */
export function wireSelectionHeightRow(
  root: HTMLElement,
  onApplied?: (result: { floors: number; items: number; lifted: number }) => void
): void {
  const input = root.querySelector<HTMLInputElement>("#sel-h-input");
  if (!input) return;

  const refreshVal = () => {
    const { mixed, displayY } = selectionHeightSummary();
    if (document.activeElement === input) return;
    const d = stepDisplayDecimals(S.freeSnapStep);
    input.value = displayY == null ? "" : displayY.toFixed(d);
    input.placeholder = "Y";
    const tag = root.querySelector(".ctx-mixed-tag");
    if (tag) tag.classList.toggle("hidden", !mixed);
  };

  const applyAbsolute = () => {
    const raw = input.value.trim();
    if (!raw) return;
    const y = parseFloat(raw);
    if (!isFinite(y)) return;
    const result = batchAdjustSelectionHeight({ absoluteY: y, liftItemsOnFloors: true });
    if (result.floors + result.items === 0) return;
    refreshVal();
    onApplied?.(result);
    setStatus(`已将选中项高度设为 ${snapValue(y, S.freeSnapStep).toFixed(stepDisplayDecimals(S.freeSnapStep))}`);
  };

  root.querySelectorAll<HTMLButtonElement>("[data-sel-h-dy]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const dy = parseFloat(btn.dataset.selHDy ?? "0");
      const result = batchAdjustSelectionHeight({ deltaY: dy });
      if (result.floors + result.items === 0) return;
      refreshVal();
      onApplied?.(result);
      const msg =
        result.lifted > 0
          ? `已调整 ${result.floors} 块地板、${result.items} 个物品（${result.lifted} 个板上物品已随动）`
          : `已调整 ${result.floors} 块地板、${result.items} 个物品高度`;
      setStatus(msg);
    });
  });

  input.addEventListener("change", applyAbsolute);
  root.querySelector("#sel-h-sync")?.addEventListener("click", applyAbsolute);
}
