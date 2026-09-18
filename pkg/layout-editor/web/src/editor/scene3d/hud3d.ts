/**
 * 3D 视口的屏幕控件（HUD）：方向平移盘 + 缩放按钮。
 *
 * 为什么需要：3D 下右键拖是环绕视角，平移只有「中键 / 空格+左键 / Alt+左键」
 * 这些隐藏手势，触控板或单键鼠标用户基本摸不到。这里补一组显式的上下左右按钮，
 * 支持点按（走一步）与长按（连续平移）。
 *
 * 控件是动态创建的，只在 3D 挂载期间存在，不污染 2D 的 DOM。
 */

import { Scene3DCtx } from "./ctx";

/** 单次点按的平移像素量（会按当前视距换算成世界位移）。 */
const STEP_PX = 90;
/** 长按时每帧的平移像素量。 */
const HOLD_PX_PER_FRAME = 9;
/** 按住多久（毫秒）之后转入连续平移。 */
const HOLD_DELAY = 260;

let root: HTMLDivElement | null = null;
let raf = 0;
let holdTimer: number | null = null;
let active: { dx: number; dy: number } | null = null;

export function mountHud3D(host: HTMLElement, ctx: Scene3DCtx): void {
  if (root) return;
  root = document.createElement("div");
  root.className = "hud3d";
  root.innerHTML = `
    <div class="hud3d-pad" role="group" aria-label="平移视角">
      <button type="button" class="hud3d-btn hud3d-up"    data-dx="0"  data-dy="-1" title="上移视角（也可用 中键拖 / 空格+左键拖）">▲</button>
      <button type="button" class="hud3d-btn hud3d-left"  data-dx="-1" data-dy="0"  title="左移视角">◀</button>
      <button type="button" class="hud3d-btn hud3d-home"  data-home="1" title="居中到整关">◎</button>
      <button type="button" class="hud3d-btn hud3d-right" data-dx="1"  data-dy="0"  title="右移视角">▶</button>
      <button type="button" class="hud3d-btn hud3d-down"  data-dx="0"  data-dy="1"  title="下移视角">▼</button>
    </div>
    <div class="hud3d-zoom">
      <button type="button" class="hud3d-btn" data-zoom="-1" title="放大（滚轮同样可用，且以鼠标位置为准）">＋</button>
      <button type="button" class="hud3d-btn" data-zoom="1"  title="缩小">－</button>
    </div>
  `;
  host.appendChild(root);

  const stop = () => {
    if (holdTimer != null) {
      window.clearTimeout(holdTimer);
      holdTimer = null;
    }
    if (raf) {
      cancelAnimationFrame(raf);
      raf = 0;
    }
    active = null;
  };

  const viewportH = () => Math.max(1, ctx.canvas.clientHeight);

  const loop = () => {
    if (!active) return;
    ctx.cam.panView(active.dx * HOLD_PX_PER_FRAME, active.dy * HOLD_PX_PER_FRAME, viewportH());
    ctx.invalidate();
    raf = requestAnimationFrame(loop);
  };

  root.addEventListener("mousedown", (e) => {
    const btn = (e.target as HTMLElement).closest(".hud3d-btn") as HTMLElement | null;
    if (!btn) return;
    // 别让按钮上的拖动落到画布上变成框选。
    e.preventDefault();
    e.stopPropagation();

    if (btn.dataset.home) {
      void import("./index").then((m) => m.fitScene3D());
      return;
    }
    if (btn.dataset.zoom) {
      ctx.cam.zoomAt(parseFloat(btn.dataset.zoom), null);
      ctx.invalidate();
      return;
    }
    const dx = parseFloat(btn.dataset.dx ?? "0");
    const dy = parseFloat(btn.dataset.dy ?? "0");
    // 点按先走一步，长按再转连续，兼顾"微调"与"快速移动"。
    ctx.cam.panView(dx * STEP_PX, dy * STEP_PX, viewportH());
    ctx.invalidate();
    holdTimer = window.setTimeout(() => {
      active = { dx, dy };
      loop();
    }, HOLD_DELAY);
  });

  root.addEventListener("click", (e) => e.stopPropagation());
  root.addEventListener("contextmenu", (e) => e.preventDefault());
  window.addEventListener("mouseup", stop);
  window.addEventListener("blur", stop);

  (root as HTMLDivElement & { _stop?: () => void })._stop = stop;
}

export function unmountHud3D(): void {
  if (!root) return;
  const stop = (root as HTMLDivElement & { _stop?: () => void })._stop;
  if (stop) {
    stop();
    window.removeEventListener("mouseup", stop);
    window.removeEventListener("blur", stop);
  }
  if (root.parentElement) root.parentElement.removeChild(root);
  root = null;
}
