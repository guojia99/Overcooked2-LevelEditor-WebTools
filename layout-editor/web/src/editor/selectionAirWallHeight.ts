import { CELL, S, EditorItem } from "./state";
import { selectionKeys } from "./selection";
import { isPlayerItem } from "./renderItems";
import { isAirWallItem } from "./stubControls";
import { airWallHeightCells, airWallHeightMeters, setAirWallHeight } from "./items";
import { pushHistory } from "./historyOps";
import { setStatus } from "./status";
import { draw } from "./render";

export function selectedAirWalls(): EditorItem[] {
  return selectionKeys()
    .map((k) => S.items.find((i) => i._editorKey === k))
    .filter((it): it is EditorItem => !!it && !isPlayerItem(it) && isAirWallItem(it));
}

export function selectionAirWallCount(): number {
  return selectedAirWalls().length;
}

export function selectionAirWallHeightSummary(): {
  count: number;
  mixed: boolean;
  displayCells: number | null;
} {
  const items = selectedAirWalls();
  const hs = items.map(airWallHeightCells);
  const mixed = hs.length > 1 && hs.some((h) => h !== hs[0]);
  return {
    count: items.length,
    mixed,
    displayCells: hs.length ? hs[0] : null,
  };
}

export function batchSetAirWallHeight(hCells: number): number {
  const h = Math.max(1, Math.round(hCells));
  const items = selectedAirWalls();
  if (!items.length) return 0;
  pushHistory();
  for (const it of items) setAirWallHeight(it, h);
  S.dirty = true;
  draw();
  return items.length;
}

export function airWallHeightRowHtml(item: EditorItem): string {
  const h = airWallHeightCells(item);
  const m = airWallHeightMeters(h);
  return `
    <div class="ctx-nudge-row">
      <span class="ctx-label">碰撞高度(格) <span class="ctx-scale-val" id="ctx-aw-h-val">≈${m.toFixed(1)}m</span></span>
      <div class="ctx-nudge">
        <button type="button" data-aw-h-dy="-1" title="降低 1 格">−1</button>
        <input type="number" id="ctx-aw-h-input" class="ctx-input ctx-pos-input" min="1" step="1" value="${h}" title="碰撞盒高度（格，1格=${CELL}m；写回后生效）" />
        <button type="button" data-aw-h-dy="1" title="升高 1 格">+1</button>
      </div>
    </div>`;
}

export function selectionAirWallHeightRowHtml(): string {
  const { count, mixed, displayCells } = selectionAirWallHeightSummary();
  if (count < 2) return "";
  const val = displayCells == null ? "" : String(displayCells);
  const mixedTag = mixed ? ' <span class="ctx-mixed-tag">混合</span>' : "";
  const m = displayCells == null ? "" : `≈${airWallHeightMeters(displayCells).toFixed(1)}m`;
  return `
    <div class="ctx-nudge-row ctx-batch-row">
      <span class="ctx-label">空气墙碰撞高度${mixedTag} <span class="ctx-scale-val" id="sel-aw-count">${count} 个${m ? ` · ${m}` : ""}</span></span>
      <div class="ctx-nudge">
        <button type="button" data-sel-aw-dy="-1" title="全体降低 1 格">−1</button>
        <input type="number" id="sel-aw-h-input" class="ctx-input ctx-pos-input" min="1" step="1" value="${val}" placeholder="格" title="统一设为该碰撞高度（回车或同步）" />
        <button type="button" data-sel-aw-dy="1" title="全体升高 1 格">+1</button>
        <button type="button" id="sel-aw-sync" class="ctx-sync-btn" title="将全部空气墙同步为输入框中的高度">同步</button>
      </div>
    </div>`;
}

export function wireAirWallHeightRow(root: HTMLElement, item: EditorItem): void {
  const input = root.querySelector<HTMLInputElement>("#ctx-aw-h-input");
  if (!input) return;

  const refresh = () => {
    const h = airWallHeightCells(item);
    if (document.activeElement !== input) input.value = String(h);
    const val = root.querySelector("#ctx-aw-h-val");
    if (val) val.textContent = `≈${airWallHeightMeters(h).toFixed(1)}m`;
  };

  const apply = (h: number) => {
    if (!(h >= 1)) return;
    pushHistory();
    setAirWallHeight(item, h);
    S.dirty = true;
    refresh();
    draw();
    setStatus(`空气墙碰撞高度已设为 ${airWallHeightCells(item)} 格（≈${airWallHeightMeters(airWallHeightCells(item)).toFixed(1)}m，写回后生效）`);
  };

  root.querySelectorAll<HTMLButtonElement>("[data-aw-h-dy]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const dy = parseInt(btn.dataset.awHDy ?? "0", 10);
      apply(airWallHeightCells(item) + dy);
    });
  });

  input.addEventListener("change", () => {
    const v = parseInt(input.value, 10);
    if (!(v >= 1)) return;
    apply(v);
  });
}

export function wireSelectionAirWallHeightRow(
  root: HTMLElement,
  onApplied?: (count: number) => void
): void {
  const input = root.querySelector<HTMLInputElement>("#sel-aw-h-input");
  if (!input) return;

  const refreshVal = () => {
    const { mixed, displayCells } = selectionAirWallHeightSummary();
    if (document.activeElement === input) return;
    input.value = displayCells == null ? "" : String(displayCells);
    input.placeholder = "格";
    const tag = root.querySelector(".ctx-mixed-tag");
    if (tag) tag.classList.toggle("hidden", !mixed);
    const countEl = root.querySelector("#sel-aw-count");
    if (countEl && displayCells != null) {
      countEl.textContent = `${selectionAirWallCount()} 个 · ≈${airWallHeightMeters(displayCells).toFixed(1)}m`;
    }
  };

  const applyAbsolute = () => {
    const raw = input.value.trim();
    if (!raw) return;
    const v = parseInt(raw, 10);
    if (!(v >= 1)) return;
    const n = batchSetAirWallHeight(v);
    if (!n) return;
    refreshVal();
    onApplied?.(n);
    setStatus(`已将 ${n} 个空气墙碰撞高度设为 ${v} 格（≈${airWallHeightMeters(v).toFixed(1)}m，写回后生效）`);
  };

  root.querySelectorAll<HTMLButtonElement>("[data-sel-aw-dy]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const dy = parseInt(btn.dataset.selAwDy ?? "0", 10);
      const { displayCells } = selectionAirWallHeightSummary();
      const base = displayCells ?? 1;
      const n = batchSetAirWallHeight(base + dy);
      if (!n) return;
      refreshVal();
      onApplied?.(n);
      const h = airWallHeightCells(selectedAirWalls()[0]);
      setStatus(`已将 ${n} 个空气墙碰撞高度调整为 ${h} 格（写回后生效）`);
    });
  });

  input.addEventListener("change", applyAbsolute);
  root.querySelector("#sel-aw-sync")?.addEventListener("click", applyAbsolute);
}
