import type { SetExportStage } from "./types";

let overlay: HTMLDivElement | null = null;
let depth = 0;
let iconFailed = false;
let retainedResult = false;
let previousFocus: HTMLElement | null = null;

const PHASE_LABEL: Record<string, string> = {
  queued: "排队等待", starting: "启动导出", compile: "编译运行时",
  preflight: "可选包预检", "orphan-clean": "孤儿脚本检查",
  "dual-stub-clean": "双 Stub 检查", prepare: "准备场景", clean: "清理旧产物",
  build: "构建资源包", package: "收集打包文件", zip: "生成压缩包", restore: "恢复场景",
  done: "导出完成", error: "导出失败",
};
const STATE_LABEL: Record<SetExportStage["state"], string> = {
  waiting: "等待中", running: "进行中", done: "已完成", error: "失败", skipped: "已跳过（不适用）",
};

function dismissBusy(): void {
  retainedResult = false;
  overlay?.classList.add("hidden");
  overlay?.classList.remove("busy-result");
  setBusyStages();
  if (previousFocus?.isConnected) previousFocus.focus();
  previousFocus = null;
}

/**
 * 全局忙碌遮罩。加载动画为「烹饪」主题：番茄 / 芝士 / 土豆三张食材图标
 * （来自 public/icons/ingredients/）依次弹跳，所有长耗时操作共用
 * （写回、加载关卡、删除、导出关卡集……）。图标缺失时回退为圆环 spinner。
 */
function ensure(): HTMLDivElement {
  if (!overlay) {
    overlay = document.createElement("div");
    overlay.id = "busy-overlay";
    overlay.className = "busy-overlay hidden";
    overlay.innerHTML = `
      <div class="busy-box" role="dialog" aria-modal="true" aria-labelledby="busy-msg" aria-busy="true" tabindex="-1">
        <div class="busy-foods" aria-hidden="true">
          <img class="busy-food f1" src="/icons/ingredients/TomatoSO.png" alt="">
          <img class="busy-food f2" src="/icons/ingredients/CheeseSO.png" alt="">
          <img class="busy-food f3" src="/icons/ingredients/PotatoSO.png" alt="">
        </div>
        <div class="busy-fallback" aria-hidden="true"><div class="busy-spinner"></div></div>
        <div class="busy-msg" id="busy-msg" role="status" aria-live="polite" aria-atomic="true">处理中…</div>
        <div class="busy-note" id="busy-note" style="display:none"></div>
        <div class="busy-stages" id="busy-stages" role="region" aria-label="导出阶段进度，可滚动" tabindex="0" hidden></div>
        <button type="button" class="busy-close" hidden>关闭结果</button>
      </div>`;
    document.body.appendChild(overlay);
    overlay.querySelector<HTMLButtonElement>(".busy-close")?.addEventListener("click", () => {
      if (depth === 0 && retainedResult) dismissBusy();
    });
    overlay.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && retainedResult && depth === 0) {
        event.preventDefault();
        event.stopPropagation();
        dismissBusy();
      } else if (event.key === "Tab") {
        const targets = Array.from(overlay!.querySelectorAll<HTMLElement>("[tabindex='0'], button"))
          .filter((el) => !el.hidden);
        const index = targets.indexOf(document.activeElement as HTMLElement);
        event.preventDefault();
        if (targets.length) targets[index < 0
          ? (event.shiftKey ? targets.length - 1 : 0)
          : (index + (event.shiftKey ? targets.length - 1 : 1)) % targets.length].focus();
        else overlay!.querySelector<HTMLElement>(".busy-box")?.focus();
      }
    });
    // dist 未重建（无 icons 目录）等情况下回退到圆环 spinner。
    overlay.querySelectorAll<HTMLImageElement>(".busy-food").forEach((img) => {
      img.addEventListener("error", () => {
        if (!iconFailed) {
          iconFailed = true;
          overlay?.classList.add("no-food-icons");
        }
      });
    });
  }
  return overlay;
}

function setBusyNote(note: string): void {
  const box = ensure().querySelector(".busy-box");
  if (!box) return;
  let noteEl = box.querySelector("#busy-note") as HTMLDivElement | null;
  if (!noteEl) {
    noteEl = document.createElement("div");
    noteEl.id = "busy-note";
    noteEl.className = "busy-note";
    box.appendChild(noteEl);
  }
  noteEl.textContent = note;
  noteEl.style.display = note ? "" : "none";
}

export function showBusy(msg = "处理中…"): void {
  const o = ensure();
  if (depth === 0 && !retainedResult) previousFocus = document.activeElement as HTMLElement | null;
  retainedResult = false;
  o.classList.remove("busy-result");
  o.querySelector<HTMLButtonElement>(".busy-close")!.hidden = true;
  o.querySelector(".busy-box")!.setAttribute("aria-busy", "true");
  setBusyStages();
  const m = o.querySelector("#busy-msg");
  m?.setAttribute("role", "status");
  if (m) m.textContent = msg;
  setBusyNote("");
  o.classList.remove("hidden");
  depth++;
  o.querySelector<HTMLElement>(".busy-box")?.focus();
}

/** 只渲染后端快照；缺少 stages 的旧后端不展示阶段面板。 */
export function setBusyStages(rows?: readonly SetExportStage[], currentPhase?: string): void {
  const o = ensure();
  const panel = o.querySelector<HTMLDivElement>("#busy-stages")!;
  const scrollTop = panel.scrollTop;
  panel.replaceChildren();
  panel.hidden = !rows?.length;
  o.querySelector(".busy-box")!.classList.toggle("has-stages", !panel.hidden);
  if (!rows?.length) return;
  const list = document.createElement("div");
  list.setAttribute("role", "list");
  rows.forEach((row, index) => {
    const item = document.createElement("div");
    item.className = `busy-stage ${row.state}`;
    item.setAttribute("role", "listitem");
    if (row.phase === currentPhase || row.state === "running") item.setAttribute("aria-current", "step");
    const head = document.createElement("div");
    head.className = "busy-stage-head";
    const label = document.createElement("span");
    label.id = `busy-stage-${index}`;
    label.textContent = `${PHASE_LABEL[row.phase] || `其他阶段（${row.phase}）`} · ${STATE_LABEL[row.state]}${row.total > 0 ? ` · ${row.completed}/${row.total}` : ""}`;
    const elapsed = document.createElement("span");
    const ms = Math.max(0, row.elapsedMs);
    elapsed.textContent = ms < 60_000
      ? `${(ms / 1000).toFixed(2)} 秒`
      : `${Math.floor(ms / 60_000)} 分钟 ${((ms % 60_000) / 1000).toFixed(2)} 秒`;
    elapsed.className = "busy-stage-elapsed";
    head.append(label, elapsed);
    const progress = document.createElement("progress");
    progress.setAttribute("aria-labelledby", label.id);
    progress.max = row.total > 0 ? row.total : 1;
    if (row.state === "done") progress.value = progress.max;
    else if (row.state === "waiting" || row.state === "skipped") progress.value = 0;
    else if (row.total > 0) progress.value = Math.max(0, Math.min(row.completed, row.total));
    else if (row.state !== "running") progress.value = 0;
    progress.setAttribute("aria-valuetext", row.state === "running" && row.total <= 0
      ? "进行中，尚无计数" : row.state === "done" ? "已完成，100%"
        : `${STATE_LABEL[row.state]}${row.total > 0 ? `，${row.completed}/${row.total}` : ""}`);
    item.append(head, progress);
    if (row.message && (row.phase === currentPhase || row.state === "running" || row.state === "error")) {
      const detail = document.createElement("div");
      detail.className = "busy-stage-detail";
      detail.textContent = row.message;
      item.appendChild(detail);
    }
    list.appendChild(item);
  });
  const note = document.createElement("div");
  note.className = "busy-stage-note";
  note.textContent = "预检和脚本检查也计入总耗时，切换Unity不能跳过这些检查";
  panel.append(list, note);
  panel.scrollTop = scrollTop;
}

/** 保留失败快照供查看；finally 仍正常释放 depth，无需等待用户确认。 */
export function setBusyResult(message: string): void {
  retainedResult = true;
  setBusyMessage(message);
  ensure().classList.add("busy-result");
  ensure().querySelector("#busy-msg")!.setAttribute("role", "alert");
}

/** 任务进行中只更新文案（不动引用计数）。note 为可选的第二行提醒。 */
export function setBusyMessage(msg: string, note = ""): void {
  const m = ensure().querySelector("#busy-msg");
  if (m) m.textContent = msg;
  setBusyNote(note);
}

export function hideBusy(): void {
  depth = Math.max(0, depth - 1);
  if (depth === 0 && overlay) {
    overlay.querySelector(".busy-box")!.setAttribute("aria-busy", "false");
    if (retainedResult) {
      const close = overlay.querySelector<HTMLButtonElement>(".busy-close")!;
      close.hidden = false;
      close.focus();
    } else dismissBusy();
  }
}

export async function withBusy<T>(msg: string, task: () => Promise<T>): Promise<T> {
  showBusy(msg);
  try {
    return await task();
  } finally {
    hideBusy();
  }
}
