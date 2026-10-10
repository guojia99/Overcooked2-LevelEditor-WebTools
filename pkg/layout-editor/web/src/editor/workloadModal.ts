import { closeModal, openModal } from "../modals";
import { cancelBtnHtml, modalBtnHtml, primaryBtnHtml } from "../ui/views/button";
import { fetchLevelDetail, fetchLevelRecipes, fetchRecipeCatalog, fetchLevelWorkload, saveLevelWorkload } from "../api";
import type { LevelWorkloadData, RecipeEntry, WorkloadMode } from "../types";
import { S, CELL } from "./state";
import { buildDocument } from "./serialize";
import {
  analyzeWorkload,
  WORKLOAD_COLORS,
  WORKLOAD_ALGORITHM_VERSION,
  DEFAULT_WALK_SPEED,
  DEFAULT_WASH_SEC,
  workloadCellKey,
  type PlayerWorkloadBreakdown,
} from "./workloadAnalysis";
import {
  drawWorkloadHoverCell,
  initWorkloadViewport,
  renderWorkloadCanvas,
  resetWorkloadViewport,
  workloadCellFromPointer,
  workloadPanBy,
  workloadZoomBy,
  workloadZoomPercent,
} from "./workloadRender";

const MODES: WorkloadMode[] = ["2p", "3p", "4p"];
const MODE_LABELS: Record<WorkloadMode, string> = { "2p": "双人", "3p": "三人", "4p": "四人" };
const NAMES = ["玩家 1", "玩家 2", "玩家 3", "玩家 4"];

let mode: WorkloadMode = "2p";
let data: LevelWorkloadData = { schemaVersion: 1, modes: {} };
let recipes: RecipeEntry[] = [];
let dirty = false;
let painting = false;
let erasing = false;
let brushSize = 1;
let activePlayer = 0;
let history: string[][][] = [];
let redoStack: string[][][] = [];
let hoverCell: { cx: number; cz: number } | null = null;
let panning = false;
let lastPanX = 0;
let lastPanY = 0;
let resizeObserver: ResizeObserver | null = null;

function n(): number { return Number(mode[0]); }
function playerIndices(): number[] { return Array.from({ length: n() }, (_, i) => i); }
function cells(): string[][] {
  const entry = data.modes[mode] ?? { players: Array.from({ length: n() }, () => ({ cells: [] })) };
  data.modes[mode] = entry;
  while (entry.players.length < n()) entry.players.push({ cells: [] });
  entry.players.length = n();
  return entry.players.map((p) => p.cells);
}
function esc(s: unknown): string {
  return String(s ?? "").replace(/[&<>\"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;" }[c] ?? c));
}
function parseCell(k: string): [number, number] {
  const [x, z] = k.split(",").map(Number);
  return [x, z];
}
function normalizeSavedCells(): void {
  if (data.schemaVersion >= 2) return;
  for (const entry of Object.values(data.modes)) {
    for (const player of entry?.players ?? []) {
      player.cells = player.cells.map((raw) => {
        const [x, z] = parseCell(raw);
        const cx = Math.round(x / 2);
        const cz = Math.round(z / 2);
        return workloadCellKey(cx * CELL, cz * CELL);
      }).filter((raw, index, all) => all.indexOf(raw) === index);
    }
  }
}
function cloneGroups(): string[][] { return cells().map((group) => group.slice()); }
function restoreGroups(snapshot: string[][]): void {
  const groups = cells();
  groups.forEach((group, index) => { group.length = 0; group.push(...(snapshot[index] ?? [])); });
}

function brushCells(cx: number, cz: number): Array<{ cx: number; cz: number }> {
  const startX = cx - Math.floor((brushSize - 1) / 2);
  const startZ = cz - Math.floor((brushSize - 1) / 2);
  const out: Array<{ cx: number; cz: number }> = [];
  for (let dx = 0; dx < brushSize; dx++) {
    for (let dz = 0; dz < brushSize; dz++) {
      out.push({ cx: startX + dx, cz: startZ + dz });
    }
  }
  return out;
}

function renderCanvas(): void {
  const canvas = document.getElementById("workload-canvas") as HTMLCanvasElement | null;
  if (!canvas) return;
  if (hoverCell && !painting) {
    drawWorkloadHoverCell(canvas, cells(), hoverCell, brushSize, activePlayer);
    return;
  }
  renderWorkloadCanvas(canvas, cells());
}

function paintAt(clientX: number, clientY: number): void {
  const canvas = document.getElementById("workload-canvas") as HTMLCanvasElement;
  const { cx, cz } = workloadCellFromPointer(canvas, clientX, clientY);
  const list = cells();
  const target = list[activePlayer];
  for (const cell of brushCells(cx, cz)) {
    const k = workloadCellKey(cell.cx * CELL, cell.cz * CELL);
    const idx = target.indexOf(k);
    if (erasing) {
      if (idx >= 0) target.splice(idx, 1);
    } else if (idx < 0) {
      target.push(k);
    }
  }
  dirty = true;
  renderCanvas();
}

function breakdownRow(label: string, sec: number): string {
  if (sec < 0.05) return "";
  return `<div class="wl-bd-row"><span>${label}</span><span>${sec.toFixed(1)}s</span></div>`;
}

function breakdownHtml(bd: PlayerWorkloadBreakdown, playerIndex: number): string {
  const color = WORKLOAD_COLORS[playerIndex];
  const rows = [
    breakdownRow("取材", bd.fetch),
    breakdownRow("切配", bd.prep),
    breakdownRow("烹饪", bd.cook),
    breakdownRow("装盘", bd.plate),
    breakdownRow("上菜", bd.serve),
    breakdownRow("洗碗", bd.wash),
    breakdownRow("步行", bd.walk),
  ].filter(Boolean).join("");
  if (!rows) return "";
  return `<div class="wl-breakdown-card" style="--chip:${color}">
    <div class="wl-breakdown-title" style="color:${color}">${NAMES[playerIndex]}</div>
    ${rows}
  </div>`;
}

function resultHtml(): string {
  const cfg = (window as unknown as { __workloadDetail?: { configs: any[] } }).__workloadDetail?.configs?.[n() - 1];
  const r = analyzeWorkload(buildDocument(""), recipes, cfg, mode, cells(), DEFAULT_WASH_SEC, DEFAULT_WALK_SPEED);
  const bars = r.percentages.map((v, i) => {
    const color = WORKLOAD_COLORS[i];
    const width = Math.max(0, Math.min(100, v));
    return `<div class="wl-bar-row">
      <span class="wl-bar-label" style="color:${color}">${NAMES[i]}</span>
      <div class="wl-bar-track"><div class="wl-bar-fill" style="width:${width.toFixed(1)}%;background:${color}"></div></div>
      <span class="wl-bar-pct">${v.toFixed(1)}%</span>
      <span class="wl-bar-sec">${r.seconds[i].toFixed(1)}s</span>
    </div>`;
  }).join("");
  const breakdown = r.breakdown.map((bd, i) => breakdownHtml(bd, i)).filter(Boolean).join("");
  const overlap = r.overlap.length
    ? r.overlap.map((o) => `<div class="wl-overlap-item"><span>玩家 ${esc(o.key)}</span><span>${o.seconds.toFixed(1)}s · ${o.share.toFixed(1)}%</span></div>`).join("")
    : '<div class="wl-overlap-empty muted">无重叠工序</div>';
  const warnList = r.warnings.length
    ? `<div class="wl-warnings">${r.warnings.map((w) => `<div class="wl-warn-item">${esc(w)}</div>`).join("")}</div>`
    : "";
  const status = r.warning
    ? `<div class="wl-alert wl-alert-warn">最高与最低差距 <b>${r.warningGap.toFixed(1)}</b> 个百分点，建议调整区域划分。</div>`
    : `<div class="wl-alert wl-alert-ok">有效任务差距 <b>${r.warningGap.toFixed(1)}</b> 个百分点。</div>`;
  return `<div class="wl-results-body">
    ${status}
    ${bars}
    ${breakdown ? `<div class="wl-breakdown"><div class="wl-overlap-title">工序分解</div>${breakdown}</div>` : ""}
    <div class="wl-overlap"><div class="wl-overlap-title">重叠区域</div>${overlap}</div>
    ${warnList}
    <div class="wl-ignored muted">已忽略工序 ${r.ignored} 步（站点格未落入任何玩家区域） · 算法 v${r.algorithmVersion}</div>
  </div>`;
}

function refreshResult(): void {
  const el = document.getElementById("workload-result");
  if (el) el.innerHTML = resultHtml();
}

function playerChipsHtml(): string {
  return playerIndices().map((i) => {
    const color = WORKLOAD_COLORS[i];
    return `<button type="button" class="wl-player-chip${i === activePlayer ? " active" : ""}" data-player="${i}" style="--chip:${color}" title="${NAMES[i]}"><span class="wl-chip-dot"></span>P${i + 1}</button>`;
  }).join("");
}

function playerSelectHtml(): string {
  return playerIndices().map((i) => `<option value="${i}">${NAMES[i]}</option>`).join("");
}

function playerLegendHtml(): string {
  return playerIndices().map((i) => `<div class="wl-legend-row"><span class="wl-legend-swatch" style="background:${WORKLOAD_COLORS[i]}"></span>${NAMES[i]}</div>`).join("");
}

function syncPlayerChips(): void {
  document.querySelectorAll<HTMLElement>(".wl-player-chip").forEach((el) => {
    const pi = Number(el.dataset.player);
    el.classList.toggle("active", pi === activePlayer);
  });
  const select = document.getElementById("workload-player") as HTMLSelectElement | null;
  if (select) select.value = String(activePlayer);
}

function refreshPlayerPanel(): void {
  activePlayer = Math.min(activePlayer, n() - 1);
  const chipsEl = document.querySelector(".wl-player-chips");
  if (chipsEl) chipsEl.innerHTML = playerChipsHtml();
  const select = document.getElementById("workload-player") as HTMLSelectElement | null;
  if (select) {
    select.innerHTML = playerSelectHtml();
    select.value = String(activePlayer);
  }
  const legendEl = document.querySelector(".wl-legend");
  if (legendEl) legendEl.innerHTML = playerLegendHtml();
  wirePlayerChipHandlers();
  syncPlayerChips();
}

function wirePlayerChipHandlers(): void {
  document.querySelectorAll<HTMLElement>(".wl-player-chip").forEach((el) => {
    el.addEventListener("click", () => {
      activePlayer = Number(el.dataset.player);
      syncPlayerChips();
    });
  });
}

function syncToolMode(): void {
  document.getElementById("workload-paint")?.classList.toggle("active", !erasing);
  document.getElementById("workload-erase")?.classList.toggle("active", erasing);
}

function syncZoomLabel(): void {
  const el = document.getElementById("workload-zoom-label");
  if (el) el.textContent = `${workloadZoomPercent()}%`;
}

function toolsHtml(): string {
  const chips = playerChipsHtml();
  return `<div class="wl-shell">
    <aside class="wl-sidebar">
      <div class="wl-section">
        <div class="wl-section-title">人数模式</div>
        <div class="wl-seg wl-modes">${MODES.map((m) => modalBtnHtml(MODE_LABELS[m], m === mode ? "active" : "", { "data-wl-mode": m })).join("")}</div>
      </div>
      <div class="wl-section">
        <div class="wl-section-title">当前玩家</div>
        <div class="wl-player-chips">${chips}</div>
        <select id="workload-player" class="wl-player-select" aria-label="选择玩家">${playerSelectHtml()}</select>
      </div>
      <div class="wl-section">
        <div class="wl-section-title">画笔工具</div>
        <div class="wl-section-title">涂抹模式</div>
        <div class="wl-seg wl-tool-mode">
          ${modalBtnHtml("画笔", erasing ? "" : "active", { id: "workload-paint" })}
          ${modalBtnHtml("橡皮擦", erasing ? "active" : "", { id: "workload-erase" })}
        </div>
        <label class="wl-field">画笔大小 <input id="workload-size" type="number" min="1" max="8" value="${brushSize}"></label>
        <div class="wl-tool-row">
          ${modalBtnHtml("清空当前", "wl-tool-btn wl-tool-btn-danger", { id: "workload-clear" })}
        </div>
        <div class="wl-tool-row">
          ${modalBtnHtml("撤销", "wl-tool-btn", { id: "workload-undo" })}
          ${modalBtnHtml("重做", "wl-tool-btn", { id: "workload-redo" })}
        </div>
      </div>
      <div class="wl-legend">${playerLegendHtml()}</div>
      <p class="wl-hint">底图与主编辑器地板层、核心层一致；涂抹按格子对齐，区域可重叠。</p>
    </aside>
    <div class="wl-stage">
      <div class="wl-zoom-bar">
        ${modalBtnHtml("−", "wl-zoom-btn", { id: "workload-zoom-out", title: "缩小" })}
        <button type="button" id="workload-zoom-reset" class="wl-zoom-btn wl-zoom-label" title="重置视图"><span id="workload-zoom-label">100%</span></button>
        ${modalBtnHtml("+", "wl-zoom-btn", { id: "workload-zoom-in", title: "放大" })}
      </div>
      <canvas id="workload-canvas"></canvas>
      <div class="wl-stage-hint">滚轮缩放 · Alt / 中键拖拽平移</div>
    </div>
    <aside class="wl-results" id="workload-result"><p class="muted wl-results-placeholder">配置区域后点击「计算」查看推测结果。</p></aside>
  </div>`;
}

function cleanupObservers(): void {
  resizeObserver?.disconnect();
  resizeObserver = null;
}

function workloadFooterInnerHtml(): string {
  return `<div class="wl-embedded-footer">
    <span class="wl-disclaimer">试验性功能 · 仅供参考，不代表实际游玩结果</span>
    ${modalBtnHtml("计算", "modal-btn", { id: "workload-calc" })}
    ${primaryBtnHtml("保存", { id: "workload-save" })}
  </div>`;
}

/** 挂载工作量推测 UI（工具与历史 Tab 或独立弹窗）。 */
export async function mountWorkloadEditor(
  mount: HTMLElement,
  detail: import("../types").LevelDetail,
  setName: string,
  embedded: boolean
): Promise<{ dispose: () => void; isDirty: () => boolean }> {
  cleanupObservers();
  const level = await fetchLevelRecipes(detail.sceneAssetPath);
  const [catalog, saved] = await Promise.all([
    fetchRecipeCatalog(setName),
    fetchLevelWorkload(detail.levelInfoAssetPath),
  ]);
  recipes = catalog.filter((r) => level.recipeGuids.includes(r.guid) || level.recipeIds?.includes(r.id));
  data = saved ?? { schemaVersion: 2, modes: {} };
  normalizeSavedCells();
  data.schemaVersion = 2;
  (window as unknown as { __workloadDetail?: unknown }).__workloadDetail = detail;
  mode = "2p";
  activePlayer = 0;
  erasing = false;
  brushSize = 1;
  dirty = false;
  history = [];
  redoStack = [];
  hoverCell = null;

  mount.innerHTML = toolsHtml() + workloadFooterInnerHtml();
  const levelInfoAssetPath = detail.levelInfoAssetPath;
  const $ = <T extends Element = HTMLElement>(id: string): T | null =>
    (mount.querySelector(`#${id}`) || document.getElementById(id)) as T | null;

  const canvas = mount.querySelector("#workload-canvas") as HTMLCanvasElement;
  initWorkloadViewport(canvas);
  renderCanvas();
  syncZoomLabel();

  $("workload-zoom-in")?.addEventListener("click", () => {
    workloadZoomBy(1.15);
    syncZoomLabel();
    renderCanvas();
  });
  $("workload-zoom-out")?.addEventListener("click", () => {
    workloadZoomBy(1 / 1.15);
    syncZoomLabel();
    renderCanvas();
  });
  $("workload-zoom-reset")?.addEventListener("click", () => {
    resetWorkloadViewport(canvas);
    syncZoomLabel();
    renderCanvas();
  });

  mount.querySelectorAll<HTMLElement>("[data-wl-mode]").forEach((el) => {
    el.addEventListener("click", () => {
      mode = el.dataset.wlMode as WorkloadMode;
      mount.querySelectorAll("[data-wl-mode]").forEach((x) => x.classList.toggle("active", x === el));
      refreshPlayerPanel();
      renderCanvas();
    });
  });

  wirePlayerChipHandlers();

  $("workload-player")?.addEventListener("change", (e) => {
    activePlayer = Number((e.target as HTMLSelectElement).value);
    syncPlayerChips();
  });

  $("workload-size")?.addEventListener("change", (e) => {
    brushSize = Math.max(1, Math.min(8, Number((e.target as HTMLInputElement).value) || 1));
  });

  $("workload-paint")?.addEventListener("click", () => {
    erasing = false;
    syncToolMode();
  });
  $("workload-erase")?.addEventListener("click", () => {
    erasing = true;
    syncToolMode();
  });

  $("workload-clear")?.addEventListener("click", () => {
    cells()[activePlayer].length = 0;
    dirty = true;
    renderCanvas();
  });

  $("workload-calc")?.addEventListener("click", refreshResult);

  $("workload-undo")?.addEventListener("click", () => {
    const previous = history.pop();
    if (!previous) return;
    redoStack.push(cloneGroups());
    restoreGroups(previous);
    dirty = true;
    renderCanvas();
  });

  $("workload-redo")?.addEventListener("click", () => {
    const next = redoStack.pop();
    if (!next) return;
    history.push(cloneGroups());
    restoreGroups(next);
    dirty = true;
    renderCanvas();
  });

  $("workload-save")?.addEventListener("click", async () => {
    data.schemaVersion = 2;
    data.calculationSnapshot = {
      algorithmVersion: WORKLOAD_ALGORITHM_VERSION,
      savedAt: new Date().toISOString(),
      layout: buildDocument(""),
      recipes,
      configs: ((window as unknown as { __workloadDetail?: { configs: any[] } }).__workloadDetail?.configs ?? []),
      parameters: { washSec: DEFAULT_WASH_SEC, walkSpeed: DEFAULT_WALK_SPEED },
    };
    await saveLevelWorkload(levelInfoAssetPath, data);
    dirty = false;
    ($("workload-save") as HTMLButtonElement).textContent = "已保存";
  });

  if (!embedded) {
    document.querySelector("[data-wl-cancel]")?.addEventListener("click", () => {
      if (dirty && !window.confirm("工作量区域尚未保存，确定关闭吗？")) return;
      cleanupObservers();
      closeModal();
    });
  }

  canvas.addEventListener("wheel", (e) => {
    e.preventDefault();
    workloadZoomBy(e.deltaY > 0 ? 0.9 : 1.1);
    syncZoomLabel();
    renderCanvas();
  }, { passive: false });

  canvas.addEventListener("mousedown", (e) => {
    if (e.button === 1 || (e.button === 0 && e.altKey)) {
      panning = true;
      lastPanX = e.clientX;
      lastPanY = e.clientY;
      e.preventDefault();
      return;
    }
    if (e.button !== 0) return;
    painting = true;
    history.push(cloneGroups());
    redoStack = [];
    paintAt(e.clientX, e.clientY);
  });
  canvas.addEventListener("mousemove", (e) => {
    if (panning) {
      workloadPanBy(e.clientX - lastPanX, e.clientY - lastPanY);
      lastPanX = e.clientX;
      lastPanY = e.clientY;
      renderCanvas();
      return;
    }
    hoverCell = workloadCellFromPointer(canvas, e.clientX, e.clientY);
    if (painting) paintAt(e.clientX, e.clientY);
    else renderCanvas();
  });
  canvas.addEventListener("mouseleave", () => {
    hoverCell = null;
    if (!painting && !panning) renderCanvas();
  });
  window.addEventListener("mouseup", () => {
    painting = false;
    panning = false;
  });

  resizeObserver = new ResizeObserver(() => renderCanvas());
  resizeObserver.observe(canvas);
  syncPlayerChips();

  return {
    dispose: cleanupObservers,
    isDirty: () => dirty,
  };
}

export async function openWorkloadModal(): Promise<void> {
  if (!S.scenePath) return;
  const level = await fetchLevelRecipes(S.scenePath);
  const detail = await fetchLevelDetail(level.levelInfoAssetPath);
  openModal(
    "工作量推测",
    `<div id="workload-mount" class="wb-workload-mount"></div>`,
    `${modalBtnHtml("关闭", "modal-btn", { "data-wl-cancel": "" })}`,
    { panelClass: "workload-modal", closeOnBackdrop: false }
  );
  const mount = document.getElementById("workload-mount")!;
  await mountWorkloadEditor(mount, detail, S.currentLevelSet, false);
}
