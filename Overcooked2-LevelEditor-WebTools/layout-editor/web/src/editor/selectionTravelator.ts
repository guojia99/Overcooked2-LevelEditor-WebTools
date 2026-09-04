import { S, EditorItem } from "./state";
import { selectionKeys } from "./selection";
import { isPlayerItem } from "./renderItems";
import { isTravelatorItem } from "./stubControls";
import { pushHistory } from "./historyOps";
import { setStatus } from "./status";

const DEFAULT_TRAVELATOR_SPEED = 2.5;

export function travelatorSpeed(item: EditorItem): number {
  return item.travelator?.speed ?? DEFAULT_TRAVELATOR_SPEED;
}

export function selectedTravelators(): EditorItem[] {
  return selectionKeys()
    .map((k) => S.items.find((i) => i._editorKey === k))
    .filter((it): it is EditorItem => !!it && !isPlayerItem(it) && isTravelatorItem(it));
}

export function selectionTravelatorCount(): number {
  return selectedTravelators().length;
}

export function selectionTravelatorSpeedSummary(): {
  count: number;
  mixed: boolean;
  displaySpeed: number | null;
} {
  const items = selectedTravelators();
  const speeds = items.map(travelatorSpeed);
  const mixed =
    speeds.length > 1 && speeds.some((s) => Math.abs(s - speeds[0]) > 1e-4);
  return {
    count: items.length,
    mixed,
    displaySpeed: speeds.length ? speeds[0] : null,
  };
}

export function batchSetTravelatorSpeed(speed: number): number {
  if (!isFinite(speed) || speed < 0) return 0;
  const items = selectedTravelators();
  if (!items.length) return 0;
  pushHistory();
  for (const it of items) {
    it.stubKind = "Travelator";
    it.travelator = { speed };
  }
  S.dirty = true;
  return items.length;
}

export function selectionTravelatorSpeedRowHtml(): string {
  const { count, mixed, displaySpeed } = selectionTravelatorSpeedSummary();
  if (count < 2) return "";
  const val = displaySpeed == null ? "" : displaySpeed.toFixed(2);
  const mixedTag = mixed ? ' <span class="ctx-mixed-tag">混合</span>' : "";
  return `
    <div class="ctx-nudge-row ctx-batch-row">
      <span class="ctx-label">自动步道速度${mixedTag} <span class="ctx-scale-val" id="sel-tv-count">${count} 个</span></span>
      <div class="ctx-nudge">
        <input type="number" id="sel-tv-speed" class="ctx-input ctx-pos-input" step="0.1" min="0" value="${val}" placeholder="速度" title="统一设为该速度（回车或同步）" />
        <button type="button" id="sel-tv-sync" class="ctx-sync-btn" title="将全部自动步道同步为输入框中的速度">同步</button>
      </div>
    </div>`;
}

export function wireSelectionTravelatorSpeedRow(
  root: HTMLElement,
  onApplied?: (count: number) => void
): void {
  const input = root.querySelector<HTMLInputElement>("#sel-tv-speed");
  if (!input) return;

  const refreshVal = () => {
    const { mixed, displaySpeed } = selectionTravelatorSpeedSummary();
    if (document.activeElement === input) return;
    input.value = displaySpeed == null ? "" : displaySpeed.toFixed(2);
    input.placeholder = "速度";
    const tag = root.querySelector(".ctx-mixed-tag");
    if (tag) tag.classList.toggle("hidden", !mixed);
  };

  const apply = () => {
    const raw = input.value.trim();
    if (!raw) return;
    const v = parseFloat(raw);
    if (!isFinite(v) || v < 0) return;
    const n = batchSetTravelatorSpeed(v);
    if (!n) return;
    refreshVal();
    onApplied?.(n);
    setStatus(`已将 ${n} 个自动步道速度设为 ${v.toFixed(2)}（写回后生效）`);
  };

  input.addEventListener("change", apply);
  root.querySelector("#sel-tv-sync")?.addEventListener("click", apply);
}
