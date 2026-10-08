/**
 * 燃烧弹射器（Burner）独立弹窗编辑器（2026-10-08）。
 * 三栏布局仿工作量推测弹窗（workloadModal）：左栏 = 基础参数 + 波次列表，
 * 中部 = 独立视口画布（底图复用 drawModalBaseScene：地板层 + 核心层含玩家，
 * 与主编辑器一致、不含装饰/背景/动画叠加），右栏 = 时长提醒与操作说明。
 * 画布交互：点空格 = 当前波追加落点（格心吸附，允许重叠/重复）；拖动标记 =
 * 移动落点；右键标记 / 选中后 Delete = 删除；滚轮缩放，Alt/中键平移。
 * 「应用」一次性写回 item.burner（含 waves），pushHistory + markDirty。
 */
import { closeModal, openModal } from "../modals";
import { modalBtnHtml, primaryBtnHtml } from "../ui/views/button";
import { fetchLevelDetail, fetchLevelRecipes } from "../api";
import { S, CELL, PX_PER_UNIT, EditorItem } from "./state";
import type { BurnerWave } from "../types";
import { pushHistory, markDirty } from "./historyOps";
import { draw } from "./render";
import { setStatus } from "./status";
import { canvasToWorld, worldToCanvas, escHtml, resolveFootprint } from "./coords";
import { BURNER_FIRE_MODES } from "./ui/constants";
import {
  withWorkloadCanvas,
  drawModalBaseScene,
  initWorkloadViewport,
  resetWorkloadViewport,
  workloadZoomBy,
  workloadPanBy,
  workloadZoomPercent,
} from "./workloadRender";

const FALLBACK_ROUND_TIME = 240;
/** 落点标记显示尺寸（米）：火焰落点 0.9×0.9。 */
const MARKER_SIZE_M = 0.9;
/** 同格多波叠放时的对角偏移步长（格）。 */
const STACK_OFFSET_CELLS = 0.14;
/** 波次间隔下限（秒）。 */
const MIN_INTERVAL = 1;
/** 实测：火焰落地后维持约 20 秒。 */
const FLAME_LIFETIME_S = 20;
/** 实测：弹体空中飞行约 2 秒（Parabolic / airTime=2）。同一格被两波间隔
 *  < 20s 命中时，前一束火焰仍在燃烧 → 火焰重叠。建议波间隔 ≥ 25s
 *  （20s 燃烧 + 2s 空中 + 余量）；默认间隔保持 30s。 */
const RECOMMENDED_INTERVAL_S = 25;

/** 跨波同格火焰重叠（间隔 < 火焰持续）。 */
interface FlameOverlap {
  waveA: number;
  waveB: number;
  gap: number;
  cells: string[];
}

/** 波内重复落点（同刻同格落地，火焰即刻重合）。 */
interface WaveDuplicate {
  wave: number;
  cell: string;
  count: number;
}

interface WorkStub {
  fireMode: number;
  airTime: number;
  hideVisual: boolean;
  startDelay: number;
  sequential: boolean;
  stagger: number;
  waves: BurnerWave[];
}

interface PosRef {
  w: number;
  p: number;
}

let work: WorkStub | null = null;
let target: EditorItem | null = null;
let activeWave = 0;
let selectedPos: PosRef | null = null;
let hoverCell: { cx: number; cz: number } | null = null;
let dragging: { ref: PosRef; moved: boolean } | null = null;
let panning = false;
let lastPanX = 0;
let lastPanY = 0;
let dirty = false;
let roundTimes: number[] = [FALLBACK_ROUND_TIME, FALLBACK_ROUND_TIME, FALLBACK_ROUND_TIME, FALLBACK_ROUND_TIME];
let roundTimeKnown = false;
let history: string[] = [];
let redoStack: string[] = [];
let resizeObserver: ResizeObserver | null = null;
let keyHandler: ((e: KeyboardEvent) => void) | null = null;

// ------------------------------------------------------------------ helpers

function esc(s: unknown): string {
  return escHtml(s);
}

function snapInterval(v: number): number {
  if (!Number.isFinite(v)) return 30;
  return Math.max(MIN_INTERVAL, Math.round(v * 10) / 10);
}

function waveInterval(w: BurnerWave): number {
  return snapInterval(w?.intervalSeconds ?? 30);
}

/** 开局延迟钳制（秒，≥0；缺省 10）。 */
function snapStartDelay(v: number): number {
  if (!Number.isFinite(v)) return 10;
  return Math.max(0, Math.round(v * 10) / 10);
}

/** 逐发间隔钳制（秒，≥0.05；缺省 0.35）。 */
function snapStagger(v: number): number {
  if (!Number.isFinite(v)) return 0.35;
  return Math.max(0.05, Math.round(v * 100) / 100);
}

/** 顺序模式的波内阅读顺序排序（与后端 SortWavePositions 同口径）：
 * 从上到下（z 升）、从左到右（x 升），原序 tiebreak。 */
function sortedWaveIndices(positions: { x: number; z: number }[]): number[] {
  return positions
    .map((_, i) => i)
    .sort((a, b) => {
      const dz = positions[a].z - positions[b].z;
      if (Math.abs(dz) > 0.0001) return dz;
      const dx = positions[a].x - positions[b].x;
      if (Math.abs(dx) > 0.0001) return dx;
      return a - b;
    });
}

/** 第 i 波的累计触发时间（秒，开局起算）：
 *  第 0 波 = 开局延迟；第 k 波 = 前一波 + interval[k]（wave[0].interval 不参与计时）。 */
function waveTime(index: number): number {
  let t = snapStartDelay(work!.startDelay);
  for (let i = 1; i <= index && i < work!.waves.length; i++) {
    t += waveInterval(work!.waves[i]);
  }
  return Math.round(t * 10) / 10;
}

function totalPositions(): number {
  return work!.waves.reduce((n, w) => n + (w?.positions?.length ?? 0), 0);
}

function minRoundTime(): number {
  return Math.min(...roundTimes);
}

function maxRoundTime(): number {
  return Math.max(...roundTimes);
}

function cellKeyOf(x: number, z: number): string {
  return `${Math.round(x * 100) / 100},${Math.round(z * 100) / 100}`;
}

/** 火焰重叠分析：
 *  ① 波内重复落点（同波同格 = 同刻落地即重合）；
 *  ② 跨波同格且触发间隔 < 火焰持续 20s（airTime 对两波相同，作差抵消）——
 *     后一束落地时前一束仍在燃烧。 */
function computeOverlapAnalysis(): { overlaps: FlameOverlap[]; duplicates: WaveDuplicate[] } {
  const waves = work!.waves;
  const cellsPerWave: string[][] = waves.map((w) =>
    (w?.positions ?? []).map((p) => cellKeyOf(p.x, p.z))
  );

  const duplicates: WaveDuplicate[] = [];
  for (let w = 0; w < waves.length; w++) {
    const seen = new Map<string, number>();
    for (const cell of cellsPerWave[w]) seen.set(cell, (seen.get(cell) ?? 0) + 1);
    for (const [cell, count] of seen) {
      if (count > 1) duplicates.push({ wave: w, cell, count });
    }
  }

  const overlaps: FlameOverlap[] = [];
  for (let a = 0; a < waves.length; a++) {
    for (let b = a + 1; b < waves.length; b++) {
      const gap = Math.round((waveTime(b) - waveTime(a)) * 10) / 10;
      if (gap >= FLAME_LIFETIME_S) continue;
      const setA = new Set(cellsPerWave[a]);
      const shared = cellsPerWave[b].filter((c) => setA.has(c));
      const uniq = Array.from(new Set(shared));
      if (uniq.length > 0) overlaps.push({ waveA: a, waveB: b, gap, cells: uniq });
    }
  }

  return { overlaps, duplicates };
}

/** 波次号集合：该波参与的任何重叠/重复（用于行内 🔥 标记）。 */
function overlapWaveSet(analysis: { overlaps: FlameOverlap[]; duplicates: WaveDuplicate[] }): Set<number> {
  const set = new Set<number>();
  for (const o of analysis.overlaps) {
    set.add(o.waveA);
    set.add(o.waveB);
  }
  for (const d of analysis.duplicates) set.add(d.wave);
  return set;
}

function snapshot(): string {
  return JSON.stringify({ work, activeWave });
}

function restore(s: string): void {
  const parsed = JSON.parse(s) as { work: WorkStub; activeWave: number };
  work = parsed.work;
  activeWave = Math.min(parsed.activeWave, Math.max(0, work.waves.length - 1));
  selectedPos = null;
}

function pushLocalHistory(): void {
  history.push(snapshot());
  if (history.length > 60) history.shift();
  redoStack.length = 0;
  dirty = true;
}

function ensureWave(index: number): BurnerWave {
  while (work!.waves.length <= index) {
    work!.waves.push({ intervalSeconds: 30, positions: [] });
  }
  const w = work!.waves[index];
  if (!w.positions) w.positions = [];
  return w;
}

function cellFromPointer(canvas: HTMLCanvasElement, clientX: number, clientY: number): { cx: number; cz: number } {
  return withWorkloadCanvas(canvas, () => {
    const r = canvas.getBoundingClientRect();
    const px = ((clientX - r.left) / r.width) * canvas.width;
    const py = ((clientY - r.top) / r.height) * canvas.height;
    const wp = canvasToWorld(px, py);
    return {
      cx: Math.round(wp.x / CELL),
      cz: Math.round(wp.z / CELL),
    };
  });
}

/** 命中测试：返回距离指针最近且在容差内的落点（当前波优先）。 */
function hitTestPos(clientX: number, clientY: number): PosRef | null {
  const canvas = document.getElementById("burner-canvas") as HTMLCanvasElement | null;
  const w = work;
  if (!canvas || !w) return null;
  const r = canvas.getBoundingClientRect();
  const px = ((clientX - r.left) / r.width) * canvas.width;
  const py = ((clientY - r.top) / r.height) * canvas.height;
  // withWorkloadCanvas 临时把 S.scale 换成弹窗视口缩放，保证容差与绘制尺寸一致。
  return withWorkloadCanvas(canvas, () => {
    const tol = Math.max(10, MARKER_SIZE_M * PX_PER_UNIT * S.scale * 0.6);
    let bestActive: PosRef | null = null;
    let bestActiveDist = Infinity;
    let bestOther: PosRef | null = null;
    let bestOtherDist = Infinity;
    for (let wi = 0; wi < w.waves.length; wi++) {
      const positions = w.waves[wi]?.positions ?? [];
      for (let p = 0; p < positions.length; p++) {
        const c = worldToCanvas(positions[p].x, positions[p].z);
        const d = Math.hypot(c.x - px, c.y - py);
        if (d > tol) continue;
        if (wi === activeWave) {
          if (d < bestActiveDist) { bestActive = { w: wi, p }; bestActiveDist = d; }
        } else if (d < bestOtherDist) {
          bestOther = { w: wi, p }; bestOtherDist = d;
        }
      }
    }
    return bestActive ?? bestOther;
  });
}

function deletePos(ref: PosRef): void {
  const w = work!.waves[ref.w];
  if (!w?.positions) return;
  pushLocalHistory();
  w.positions.splice(ref.p, 1);
  selectedPos = null;
  refreshAll();
}

// ------------------------------------------------------------------ canvas

function drawBurnerScene(): void {
  drawModalBaseScene();
  if (!work) return;
  const ctx = dom2d();
  if (!ctx) return;

  const cellPx = CELL * PX_PER_UNIT * S.scale;
  const markerPx = MARKER_SIZE_M * PX_PER_UNIT * S.scale;

  // 燃烧弹射器本体高亮（发射器位置）。
  if (target) {
    const fp = resolveFootprint(target);
    const c = worldToCanvas(target._wx, target._wz);
    const wPx = fp.cellsX * cellPx;
    const hPx = fp.cellsZ * cellPx;
    ctx.save();
    ctx.strokeStyle = "#ffb257";
    ctx.lineWidth = Math.max(2, cellPx * 0.09);
    ctx.setLineDash([Math.max(4, cellPx * 0.22), Math.max(3, cellPx * 0.14)]);
    ctx.strokeRect(c.x - wPx / 2, c.y - hPx / 2, wPx, hPx);
    ctx.setLineDash([]);
    ctx.fillStyle = "rgba(255,178,87,0.92)";
    ctx.font = `bold ${Math.max(10, cellPx * 0.3)}px system-ui, sans-serif`;
    ctx.textAlign = "center";
    ctx.textBaseline = "bottom";
    ctx.fillText("燃烧弹射器", c.x, c.y - hPx / 2 - 3);
    ctx.restore();
  }

  // 按格分组实现同格多波叠放（对角偏移）+ 徽标合并波次号。
  const stacks = new Map<string, PosRef[]>();
  for (let w = 0; w < work.waves.length; w++) {
    const positions = work.waves[w]?.positions ?? [];
    for (let p = 0; p < positions.length; p++) {
      const key = `${positions[p].x},${positions[p].z}`;
      const list = stacks.get(key) ?? [];
      list.push({ w, p });
      stacks.set(key, list);
    }
  }

  // 各波累计触发时间（重叠判定用）。
  const times: number[] = work.waves.map((_, i) => waveTime(i));

  // 顺序模式：当前波内逐发序号（阅读顺序），标记右下角小字。
  const seqOrder = new Map<string, number>();
  if (work.sequential) {
    const positions = work.waves[activeWave]?.positions ?? [];
    sortedWaveIndices(positions).forEach((posIdx, order) => {
      seqOrder.set(`${activeWave}:${posIdx}`, order + 1);
    });
  }

  const badgeFont = Math.max(9, cellPx * 0.3);
  ctx.save();
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  for (const [key, refs] of stacks) {
    const [x, z] = key.split(",").map(Number);
    const center = worldToCanvas(x, z);
    refs.sort((a, b) => a.w - b.w);
    const numbers = refs.map((r) => r.w + 1).join("·");
    // 同格火焰重叠：任两波间隔 < 20s，或波内重复落点（同刻同格）。
    const ws = refs.map((r) => r.w);
    let overlapped = false;
    for (let i = 0; i < refs.length && !overlapped; i++) {
      for (let j = i + 1; j < refs.length && !overlapped; j++) {
        if (Math.abs(times[ws[j]] - times[ws[i]]) < FLAME_LIFETIME_S) overlapped = true;
      }
      if (ws.indexOf(ws[i]) !== ws.lastIndexOf(ws[i])) overlapped = true;
    }
    for (let i = 0; i < refs.length; i++) {
      const ref = refs[i];
      const isActive = ref.w === activeWave;
      const isSelected = selectedPos && selectedPos.w === ref.w && selectedPos.p === ref.p;
      const off = i * STACK_OFFSET_CELLS * cellPx;
      const cxp = center.x + off;
      const cyp = center.y + off;
      ctx.globalAlpha = isActive ? 0.95 : 0.38;
      ctx.fillStyle = overlapped
        ? (isActive ? "rgba(255,64,64,0.5)" : "rgba(255,64,64,0.45)")
        : (isActive ? "rgba(255,106,61,0.55)" : "rgba(255,106,61,0.5)");
      ctx.fillRect(cxp - markerPx / 2, cyp - markerPx / 2, markerPx, markerPx);
      ctx.strokeStyle = isSelected
        ? "#ffffff"
        : overlapped ? "#ff5c5c"
        : isActive ? "#ff8a5c" : "rgba(255,138,92,0.55)";
      ctx.lineWidth = isSelected ? Math.max(2, cellPx * 0.08) : Math.max(1, cellPx * 0.045);
      ctx.strokeRect(cxp - markerPx / 2, cyp - markerPx / 2, markerPx, markerPx);
      // 波内火焰纹理提示：中心小圆点。
      ctx.fillStyle = isActive ? "rgba(255,214,140,0.9)" : "rgba(255,214,140,0.4)";
      ctx.beginPath();
      ctx.arc(cxp, cyp, Math.max(1.5, markerPx * 0.08), 0, Math.PI * 2);
      ctx.fill();
      // 顺序模式：活动波标记右下角画逐发序号。
      if (work.sequential && isActive) {
        const order = seqOrder.get(`${ref.w}:${ref.p}`);
        if (order != null) {
          ctx.font = `bold ${Math.max(8, markerPx * 0.3)}px system-ui, sans-serif`;
          ctx.fillStyle = "#ffd6a5";
          ctx.textAlign = "right";
          ctx.textBaseline = "bottom";
          ctx.fillText(String(order), cxp + markerPx / 2 - 1, cyp + markerPx / 2 + 1);
          ctx.textAlign = "center";
          ctx.textBaseline = "middle";
        }
      }
    }
    // 徽标：栈顶合并波次号（重叠 = 红底警示）。
    const top = refs[refs.length - 1];
    const off = (refs.length - 1) * STACK_OFFSET_CELLS * cellPx;
    const bx = center.x + off + markerPx / 2;
    const by = center.y + off - markerPx / 2;
    ctx.globalAlpha = top.w === activeWave ? 1 : 0.6;
    ctx.font = `bold ${badgeFont}px system-ui, sans-serif`;
    const tw = ctx.measureText(numbers).width;
    const pad = badgeFont * 0.32;
    const bw = tw + pad * 2;
    const bh = badgeFont * 1.25;
    ctx.fillStyle = overlapped ? "#8f1d10" : top.w === activeWave ? "#7a2410" : "#3c3f46";
    roundRect(ctx, bx - bw / 2, by - bh, bw, bh, bh * 0.32);
    ctx.fill();
    ctx.fillStyle = overlapped ? "#ffd9d0" : "#ffe9d6";
    ctx.fillText(numbers, bx, by - bh / 2 + 0.5);
  }
  ctx.restore();

  // 悬停预览（当前波落点幽灵框）。
  if (hoverCell && !dragging) {
    const c = worldToCanvas(hoverCell.cx * CELL, hoverCell.cz * CELL);
    ctx.save();
    ctx.strokeStyle = "rgba(255,138,92,0.9)";
    ctx.lineWidth = Math.max(2, cellPx * 0.07);
    ctx.setLineDash([5, 4]);
    ctx.strokeRect(c.x - markerPx / 2, c.y - markerPx / 2, markerPx, markerPx);
    ctx.setLineDash([]);
    ctx.restore();
  }
}

function dom2d(): CanvasRenderingContext2D | null {
  const canvas = document.getElementById("burner-canvas") as HTMLCanvasElement | null;
  return canvas ? canvas.getContext("2d") : null;
}

function roundRect(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number): void {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}

function renderCanvas(): void {
  const canvas = document.getElementById("burner-canvas") as HTMLCanvasElement | null;
  if (!canvas || !work) return;
  withWorkloadCanvas(canvas, () => drawBurnerScene());
  syncZoomLabel();
}

function syncZoomLabel(): void {
  const el = document.getElementById("burner-zoom-label");
  if (el) el.textContent = `${workloadZoomPercent()}%`;
}

// ------------------------------------------------------------------ panels

function roundTimesText(): string {
  const distinct = Array.from(new Set(roundTimes));
  return distinct.length === 1 ? `${distinct[0]}s` : distinct.map((t) => `${t}s`).join(" / ");
}

function waveRowHtml(w: number, flameSet?: Set<number>): string {
  const wave = work!.waves[w];
  const t = waveTime(w);
  const count = wave?.positions?.length ?? 0;
  const over = t >= minRoundTime() && roundTimeKnown;
  const flame = flameSet?.has(w) === true;
  const active = w === activeWave;
  // 第 1 波 = 开局延迟后发射（间隔不参与计时）；间隔输入从第 2 波起。
  const intervalHtml = w === 0
    ? `<span class="be-wave-int be-muted" title="第 1 波在开局延迟（基础参数）后发射">开局</span>`
    : `<label class="be-wave-int" title="距上一波的间隔">
      间隔<input type="number" data-be-int="${w}" min="${MIN_INTERVAL}" step="1" value="${waveInterval(wave)}"/>s
    </label>`;
  return `<div class="be-wave-row${active ? " active" : ""}${over ? " be-wave-over" : ""}" data-be-wave="${w}">
    <span class="be-wave-radio" title="设为当前波"></span>
    <span class="be-wave-no">${w + 1}</span>
    ${intervalHtml}
    <span class="be-wave-time" title="累计触发时间">t=${t}s</span>
    <span class="be-wave-count">${count} 落点</span>
    ${flame ? `<span class="be-flame" title="与其他波同格且间隔 <20s，或波内有重复落点——火焰会重叠（详见右侧检查）">🔥</span>` : ""}
    ${over ? `<span class="be-warn" title="已到/超过关卡时长">⚠</span>` : ""}
  </div>`;
}

function basicHtml(): string {
  const b = work!;
  const modeOpts = BURNER_FIRE_MODES.map(
    (n, i) => `<option value="${i}" ${b.fireMode === i ? "selected" : ""}>${n}</option>`
  ).join("");
  return `<div class="wl-section">
        <div class="wl-section-title">基础参数</div>
        <label class="wl-field">开局延迟 <input id="be-delay" type="number" min="0" step="1" value="${snapStartDelay(b.startDelay)}"/> 秒</label>
        <label class="wl-field">开火模式 <select id="be-mode" class="ctx-input">${modeOpts}</select></label>
        <label class="wl-field">空中时间 <input id="be-air" type="number" min="0" step="0.1" value="${b.airTime}"/> 秒</label>
        <label class="wl-field"><input id="be-hide" type="checkbox" ${b.hideVisual ? "checked" : ""}/> 隐藏模型</label>
        <div class="wl-section-title" style="margin-top:10px">波内发射</div>
        <div class="wl-seg wl-tool-mode">
          ${modalBtnHtml("齐射（默认）", !b.sequential ? "active" : "", { id: "be-fire-salvo" })}
          ${modalBtnHtml("顺序逐发", b.sequential ? "active" : "", { id: "be-fire-seq" })}
        </div>
        ${b.sequential
          ? `<label class="wl-field">逐发间隔 <input id="be-stagger" type="number" min="0.05" step="0.05" value="${snapStagger(b.stagger)}"/> 秒</label>`
          : ""}
        <div class="be-delay-hint">${b.sequential
          ? "波内按阅读顺序（从上到下、从左到右）依次发射：第 i 个落点在波时刻 + i×逐发间隔"
          : "整波落点同一时刻齐射；切到「顺序逐发」可按阅读顺序错开发射"}</div>
      </div>`;
}

function wireBasic(): void {
  document.getElementById("be-delay")?.addEventListener("change", (e) => {
    pushLocalHistory();
    work!.startDelay = snapStartDelay(Number((e.target as HTMLInputElement).value));
    refreshAll();
  });
  document.getElementById("be-fire-salvo")?.addEventListener("click", () => {
    if (!work!.sequential) return;
    pushLocalHistory();
    work!.sequential = false;
    refreshBasic();
    renderCanvas();
  });
  document.getElementById("be-fire-seq")?.addEventListener("click", () => {
    if (work!.sequential) return;
    pushLocalHistory();
    work!.sequential = true;
    refreshBasic();
    renderCanvas();
  });
  document.getElementById("be-stagger")?.addEventListener("change", (e) => {
    pushLocalHistory();
    work!.stagger = snapStagger(Number((e.target as HTMLInputElement).value));
    refreshAll();
  });
  document.getElementById("be-mode")?.addEventListener("change", (e) => {
    pushLocalHistory();
    work!.fireMode = Number((e.target as HTMLSelectElement).value);
  });
  document.getElementById("be-air")?.addEventListener("change", (e) => {
    pushLocalHistory();
    work!.airTime = Math.max(0, Number((e.target as HTMLInputElement).value) || 0);
  });
  document.getElementById("be-hide")?.addEventListener("change", (e) => {
    pushLocalHistory();
    work!.hideVisual = (e.target as HTMLInputElement).checked;
  });
}

function refreshBasic(): void {
  const box = document.getElementById("be-basic");
  if (box) {
    box.innerHTML = basicHtml();
    wireBasic();
  }
}

function sidebarHtml(): string {
  const flameSet = overlapWaveSet(computeOverlapAnalysis());
  return `<div class="be-shell">
    <aside class="be-side">
      <div id="be-basic">${basicHtml()}</div>
      <div class="wl-section">
        <div class="wl-section-title">波次（第 N 波 = 一次齐发/逐发）</div>
        <div class="be-waves" id="be-waves">
          ${work!.waves.map((_, i) => waveRowHtml(i, flameSet)).join("") || `<div class="be-wave-empty">还没有波次——点「＋ 新波次」后在画布上点格放置落点</div>`}
        </div>
        <div class="wl-tool-row">
          ${modalBtnHtml("＋ 新波次", "wl-tool-btn", { id: "be-add-wave" })}
          ${modalBtnHtml("复制", "wl-tool-btn", { id: "be-dup-wave", title: "复制当前波" })}
          ${modalBtnHtml("删除", "wl-tool-btn wl-tool-btn-danger", { id: "be-del-wave", title: "删除当前波" })}
        </div>
        <div class="wl-tool-row">
          ${modalBtnHtml("清空本波落点", "wl-tool-btn wl-tool-btn-danger", { id: "be-clear-wave" })}
          ${modalBtnHtml("撤销", "wl-tool-btn", { id: "be-undo" })}
          ${modalBtnHtml("重做", "wl-tool-btn", { id: "be-redo" })}
        </div>
      </div>
    </aside>
    <div class="wl-stage">
      <div class="wl-zoom-bar">
        ${modalBtnHtml("−", "wl-zoom-btn", { id: "burner-zoom-out", title: "缩小" })}
        <button type="button" id="burner-zoom-reset" class="wl-zoom-btn wl-zoom-label" title="重置视图"><span id="burner-zoom-label">100%</span></button>
        ${modalBtnHtml("+", "wl-zoom-btn", { id: "burner-zoom-in", title: "放大" })}
      </div>
      <canvas id="burner-canvas"></canvas>
      <div class="wl-stage-hint">点空格 = 当前波加落点 · 拖动标记移动 · 右键标记/Delete 删除 · 滚轮缩放 · Alt/中键平移 · 落点可重叠 · 红色 = 火焰重叠（同格间隔 &lt;20s）</div>
    </div>
    <aside class="be-info" id="be-info">${infoHtml()}</aside>
  </div>`;
}

function infoHtml(): string {
  const n = work!.waves.length;
  const total = totalPositions();
  const last = n > 0 ? waveTime(n - 1) : 0;
  const min = minRoundTime();
  const max = maxRoundTime();
  const overWaves: number[] = [];
  for (let i = 0; i < n; i++) {
    if (waveTime(i) >= min) overWaves.push(i + 1);
  }
  const warnHtml = roundTimeKnown && overWaves.length > 0
    ? `<div class="be-info-warn">⚠ 第 ${overWaves.join("、")} 波的触发时间已到/超过关卡时长（${roundTimesText()}）——超时波次不会在局内触发，请缩短间隔或减少波次。</div>`
    : "";
  const cfgNote = roundTimeKnown
    ? `本关时长：${roundTimesText()}${min !== max ? `（提醒按最小 ${min}s 判定）` : ""}`
    : `未读取到关卡时长（按 ${FALLBACK_ROUND_TIME}s 估算）`;

  // 火焰重叠检查（实测：火焰落地后维持约 20s，弹体空中约 2s）。
  const analysis = computeOverlapAnalysis();
  const overlapHtml = analysis.overlaps
    .map((o) => `<div class="be-info-warn">🔥 第 ${o.waveA + 1} 波与第 ${o.waveB + 1} 波共用落点（${o.cells.slice(0, 3).map((c) => `(${c})`).join("、")}${o.cells.length > 3 ? "…" : ""}），间隔仅 ${o.gap}s &lt; 火焰持续 ${FLAME_LIFETIME_S}s——后一束落地时前一束仍在燃烧，火焰重叠。建议同格两波间隔 ≥ ${RECOMMENDED_INTERVAL_S}s。</div>`)
    .join("");
  const dupHtml = analysis.duplicates
    .map((d) => `<div class="be-info-warn">🔥 第 ${d.wave + 1} 波在 (${d.cell}) 有 ${d.count} 个重复落点——同刻落地火焰即刻重合，属浪费弹，建议删除多余落点。</div>`)
    .join("");
  const overlapSection = n > 0
    ? (overlapHtml || dupHtml
      ? overlapHtml + dupHtml
      : `<p class="be-info-line be-ok">无重叠火焰 ✓（同格波次间隔均 ≥ ${FLAME_LIFETIME_S}s）</p>`)
    : "";

  return `<div class="wl-section">
      <div class="wl-section-title">时长提醒</div>
      <p class="be-info-line">${esc(cfgNote)}</p>
      <p class="be-info-line">共 <b>${n}</b> 波 · <b>${total}</b> 个落点 · 末波 t=<b>${last}s</b></p>
      ${warnHtml}
    </div>
    <div class="wl-section">
      <div class="wl-section-title">火焰重叠检查</div>
      ${overlapSection || `<p class="be-info-line be-muted">添加波次与落点后自动检查。</p>`}
    </div>
    <div class="wl-section">
      <div class="wl-section-title">说明</div>
      <p class="be-info-line">开局延迟（默认 <b>10s</b>）后发射第 1 波，之后每波按「间隔」（默认 30s）顺延。</p>
      <p class="be-info-line">波内发射：${work!.sequential
        ? `<b>顺序逐发</b>——按阅读顺序（从上到下、从左到右）每 ${snapStagger(work!.stagger)}s 发一枚（当前波标记右下角数字 = 发射序号）`
        : `<b>齐射</b>——整波同刻齐发（默认）`}。</p>
      <p class="be-info-line">实测：火焰落地后维持约 <b>${FLAME_LIFETIME_S}s</b>，弹体空中约 2s。同一格两波间隔 &lt; ${FLAME_LIFETIME_S}s 会火焰重叠；合理波间隔约 <b>${RECOMMENDED_INTERVAL_S}s</b>（含空中时间），建议保持默认 <b>30s</b>。</p>
      <p class="be-info-line">每波到点时对波内全部落点同时喷射火焰（数量任意、可不同）。</p>
      <p class="be-info-line">橙色虚线框 = 弹射器本体；数字 = 波次号（同格多波叠加显示）。</p>
      <p class="be-info-line">底图 = 核心层（含玩家）；修改经「应用」写回，随 💾 写回 Unity 生效。</p>
    </div>`;
}

function refreshWavesPanel(): void {
  const box = document.getElementById("be-waves");
  if (box) {
    const flameSet = overlapWaveSet(computeOverlapAnalysis());
    box.innerHTML = work!.waves.map((_, i) => waveRowHtml(i, flameSet)).join("")
      || `<div class="be-wave-empty">还没有波次——点「＋ 新波次」后在画布上点格放置落点</div>`;
    wireWaveRows();
  }
  const info = document.getElementById("be-info");
  if (info) info.innerHTML = infoHtml();
}

function refreshAll(): void {
  refreshWavesPanel();
  renderCanvas();
}

function wireWaveRows(): void {
  document.querySelectorAll<HTMLElement>("[data-be-wave]").forEach((row) => {
    row.addEventListener("click", (e) => {
      if ((e.target as HTMLElement).closest("[data-be-int]")) return;
      activeWave = Number(row.dataset.beWave);
      selectedPos = null;
      refreshWavesPanel();
      renderCanvas();
    });
  });
  document.querySelectorAll<HTMLInputElement>("[data-be-int]").forEach((input) => {
    input.addEventListener("change", () => {
      const w = Number(input.dataset.beInt);
      pushLocalHistory();
      work!.waves[w].intervalSeconds = snapInterval(Number(input.value));
      refreshAll();
    });
  });
}

// ------------------------------------------------------------------ open

export async function openBurnerEditor(item: EditorItem): Promise<void> {
  if (!S.scenePath || !item) return;
  target = item;
  const b = item.burner ?? {};
  work = {
    fireMode: b.fireMode ?? 1,
    airTime: b.airTime ?? 2,
    hideVisual: b.hideVisual === true,
    startDelay: snapStartDelay(b.startDelaySeconds ?? 10),
    sequential: b.sequentialFire === true,
    stagger: snapStagger(b.fireStaggerSeconds ?? 0.35),
    waves: (b.waves ?? []).map((w) => ({
      intervalSeconds: snapInterval(w?.intervalSeconds ?? 30),
      positions: (w?.positions ?? []).map((p) => ({ x: p.x, z: p.z })),
    })),
  };
  activeWave = Math.max(0, work.waves.length - 1);
  selectedPos = null;
  hoverCell = null;
  dragging = null;
  dirty = false;
  history = [];
  redoStack = [];

  // 关卡时长（1P~4P roundTime）：失败降级 240。
  roundTimeKnown = false;
  try {
    const level = await fetchLevelRecipes(S.scenePath);
    if (level.levelInfoAssetPath) {
      const detail = await fetchLevelDetail(level.levelInfoAssetPath);
      const times = (detail.configs ?? []).map((c) => c?.roundTime).filter((t) => Number.isFinite(t) && (t as number) > 0);
      if (times.length > 0) {
        roundTimes = [0, 1, 2, 3].map((i) => times[i] ?? times[0]);
        roundTimeKnown = true;
      }
    }
  } catch {
    // 保持降级值。
  }

  openModal(
    "燃烧弹射器 · 火焰落点编辑",
    sidebarHtml(),
    `${modalBtnHtml("关闭", "modal-btn", { "data-be-cancel": "" })}
     ${primaryBtnHtml("应用", { id: "be-apply" })}`,
    { panelClass: "burner-modal", closeOnBackdrop: false }
  );

  const canvas = document.getElementById("burner-canvas") as HTMLCanvasElement;
  initWorkloadViewport(canvas);
  renderCanvas();

  // ---- 基础参数（含波内发射：齐射/顺序逐发开关）
  wireBasic();

  // ---- 波次操作
  document.getElementById("be-add-wave")?.addEventListener("click", () => {
    pushLocalHistory();
    work!.waves.push({ intervalSeconds: 30, positions: [] });
    activeWave = work!.waves.length - 1;
    refreshAll();
  });
  document.getElementById("be-dup-wave")?.addEventListener("click", () => {
    if (!work!.waves.length) return;
    pushLocalHistory();
    const src = work!.waves[activeWave] ?? work!.waves[work!.waves.length - 1];
    work!.waves.push({
      intervalSeconds: waveInterval(src),
      positions: (src?.positions ?? []).map((p) => ({ x: p.x, z: p.z })),
    });
    activeWave = work!.waves.length - 1;
    refreshAll();
  });
  document.getElementById("be-del-wave")?.addEventListener("click", () => {
    if (!work!.waves.length) return;
    pushLocalHistory();
    work!.waves.splice(activeWave, 1);
    activeWave = Math.min(activeWave, Math.max(0, work!.waves.length - 1));
    selectedPos = null;
    refreshAll();
  });
  document.getElementById("be-clear-wave")?.addEventListener("click", () => {
    const w = work!.waves[activeWave];
    if (!w?.positions?.length) return;
    pushLocalHistory();
    w.positions = [];
    selectedPos = null;
    refreshAll();
  });
  document.getElementById("be-undo")?.addEventListener("click", () => {
    const prev = history.pop();
    if (!prev) return;
    redoStack.push(snapshot());
    restore(prev);
    refreshAll();
  });
  document.getElementById("be-redo")?.addEventListener("click", () => {
    const next = redoStack.pop();
    if (!next) return;
    history.push(snapshot());
    restore(next);
    refreshAll();
  });

  wireWaveRows();

  // ---- 缩放
  const syncZoom = () => {
    syncZoomLabel();
    renderCanvas();
  };
  document.getElementById("burner-zoom-in")?.addEventListener("click", () => { workloadZoomBy(1.15); syncZoom(); });
  document.getElementById("burner-zoom-out")?.addEventListener("click", () => { workloadZoomBy(1 / 1.15); syncZoom(); });
  document.getElementById("burner-zoom-reset")?.addEventListener("click", () => {
    resetWorkloadViewport(canvas);
    syncZoom();
  });

  // ---- 画布交互
  canvas.addEventListener("wheel", (e) => {
    e.preventDefault();
    workloadZoomBy(e.deltaY > 0 ? 0.9 : 1.1);
    syncZoom();
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
    const hit = hitTestPos(e.clientX, e.clientY);
    if (hit) {
      selectedPos = hit;
      dragging = { ref: hit, moved: false };
      history.push(snapshot());
      redoStack.length = 0;
      renderCanvas();
      refreshWavesPanel();
      return;
    }
    // 点空格：当前波追加落点。
    if (work!.waves.length === 0) {
      work!.waves.push({ intervalSeconds: 30, positions: [] });
      activeWave = 0;
    }
    const { cx, cz } = cellFromPointer(canvas, e.clientX, e.clientY);
    pushLocalHistory();
    ensureWave(activeWave).positions!.push({ x: cx * CELL, z: cz * CELL });
    selectedPos = { w: activeWave, p: work!.waves[activeWave].positions!.length - 1 };
    refreshAll();
  });

  canvas.addEventListener("mousemove", (e) => {
    if (panning) {
      workloadPanBy(e.clientX - lastPanX, e.clientY - lastPanY);
      lastPanX = e.clientX;
      lastPanY = e.clientY;
      renderCanvas();
      return;
    }
    hoverCell = cellFromPointer(canvas, e.clientX, e.clientY);
    if (dragging) {
      const w = work!.waves[dragging.ref.w];
      if (w?.positions?.[dragging.ref.p]) {
        const pos = w.positions[dragging.ref.p]!;
        if (pos.x !== hoverCell.cx * CELL || pos.z !== hoverCell.cz * CELL) {
          dragging.moved = true;
          pos.x = hoverCell.cx * CELL;
          pos.z = hoverCell.cz * CELL;
          dirty = true;
        }
      }
      renderCanvas();
      return;
    }
    renderCanvas();
  });

  canvas.addEventListener("mouseleave", () => {
    hoverCell = null;
    if (!dragging && !panning) renderCanvas();
  });

  canvas.addEventListener("contextmenu", (e) => {
    e.preventDefault();
    const hit = hitTestPos(e.clientX, e.clientY);
    if (hit) deletePos(hit);
  });

  window.addEventListener("mouseup", onGlobalMouseUp);

  // 键盘（capture 在 document：吞掉主编辑器的 Delete/Esc，避免误删场景物品）。
  keyHandler = (e: KeyboardEvent) => {
    if (e.key === "Escape") {
      e.stopImmediatePropagation();
      e.preventDefault();
      requestClose();
      return;
    }
    if ((e.key === "Delete" || e.key === "Backspace") && selectedPos) {
      const t = e.target as HTMLElement | null;
      if (t && (t.tagName === "INPUT" || t.tagName === "SELECT" || t.tagName === "TEXTAREA")) return;
      e.stopImmediatePropagation();
      e.preventDefault();
      deletePos(selectedPos);
    }
  };
  document.addEventListener("keydown", keyHandler, true);

  // ---- footer
  document.querySelector("[data-be-cancel]")?.addEventListener("click", () => requestClose());
  document.getElementById("be-apply")?.addEventListener("click", () => {
    if (!target || !work) return;
    pushHistory();
    target.stubKind = "Burner";
    target.burner = {
      fireMode: work.fireMode,
      airTime: work.airTime,
      randomTargetOrder: false,
      hideVisual: work.hideVisual,
      startDelaySeconds: snapStartDelay(work.startDelay),
      sequentialFire: work.sequential === true,
      fireStaggerSeconds: snapStagger(work.stagger),
      waves: work.waves.map((w) => ({
        intervalSeconds: waveInterval(w),
        positions: (w?.positions ?? []).map((p) => ({ x: p.x, z: p.z })),
      })),
    };
    markDirty();
    draw();
    setStatus("已更新燃烧弹射器波次（写回 Unity 后生效）");
    teardown();
    closeModal();
  });

  resizeObserver = new ResizeObserver(() => renderCanvas());
  resizeObserver.observe(canvas);
}

function onGlobalMouseUp(): void {
  dragging = null;
  panning = false;
}

function requestClose(): void {
  if (dirty && !window.confirm("燃烧弹射器修改尚未应用，确定关闭吗？")) return;
  teardown();
  closeModal();
}

function teardown(): void {
  resizeObserver?.disconnect();
  resizeObserver = null;
  window.removeEventListener("mouseup", onGlobalMouseUp);
  if (keyHandler) {
    document.removeEventListener("keydown", keyHandler, true);
    keyHandler = null;
  }
  work = null;
  target = null;
  dirty = false;
}
