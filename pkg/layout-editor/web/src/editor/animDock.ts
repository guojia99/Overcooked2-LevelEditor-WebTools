import { dom } from "./dom";
import { draw } from "./render";

/**
 * 底部「动画与联动编排台」停靠面板（非模态）。
 *
 * 取代旧的全屏遮罩弹窗 `.anim-editor-modal`：编排台停靠在画布下方，
 * 画布随之收缩、预览始终可见且可交互（需求 4 / 7）。动画组编辑（模式 A）
 * 与按钮触发编排（模式 B）都渲染进本面板的 body 中。
 */

const MIN_HEIGHT = 200;
const DEFAULT_HEIGHT = 360;
/** 收缩时至少给画布留出的高度，避免编排台把画布挤没。 */
const CANVAS_MIN = 140;

let gripWired = false;
let dockHeight = DEFAULT_HEIGHT;

function loadHeight(): number {
  try {
    const v = parseFloat(localStorage.getItem("animDockHeight") || "0");
    if (v >= MIN_HEIGHT) return v;
  } catch {
    /* ignore */
  }
  return DEFAULT_HEIGHT;
}

function maxHeight(): number {
  const wrap = dom.animDock?.parentElement as HTMLElement | null;
  const total = wrap ? wrap.getBoundingClientRect().height : window.innerHeight;
  return Math.max(MIN_HEIGHT, total - CANVAS_MIN);
}

function applyHeight(h: number): void {
  dockHeight = Math.max(MIN_HEIGHT, Math.min(maxHeight(), h));
  if (dom.animDock) dom.animDock.style.height = dockHeight + "px";
}

function wireGrip(): void {
  if (gripWired) return;
  gripWired = true;
  const grip = document.getElementById("anim-dock-grip");
  grip?.addEventListener("mousedown", (e) => {
    e.preventDefault();
    const startY = e.clientY;
    const startH = dom.animDock.getBoundingClientRect().height;
    document.body.classList.add("anim-dock-resizing");
    const onMove = (ev: MouseEvent) => {
      applyHeight(startH + (startY - ev.clientY));
      draw();
    };
    const onUp = () => {
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", onUp);
      document.body.classList.remove("anim-dock-resizing");
      try {
        localStorage.setItem("animDockHeight", String(Math.round(dockHeight)));
      } catch {
        /* ignore */
      }
      draw();
    };
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);
  });
}

export function isAnimDockOpen(): boolean {
  return !!dom.animDock && !dom.animDock.classList.contains("hidden");
}

/** 当前编排台内容容器（未打开时返回 null）。 */
export function animDockBody(): HTMLElement | null {
  return isAnimDockOpen() ? dom.animDockBody : null;
}

/** 打开编排台，清空旧内容并返回内容容器；画布随之收缩重排。 */
export function openAnimDock(): HTMLElement {
  wireGrip();
  applyHeight(loadHeight());
  dom.animDock.classList.remove("hidden");
  dom.animDockBody.innerHTML = "";
  draw();
  return dom.animDockBody;
}

/** 关闭编排台，画布恢复满高。 */
export function closeAnimDock(): void {
  if (!dom.animDock) return;
  dom.animDock.classList.add("hidden");
  dom.animDockBody.innerHTML = "";
  draw();
}
