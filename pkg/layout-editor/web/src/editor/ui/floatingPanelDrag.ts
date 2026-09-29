/** 画布内浮动面板 / 模态框的视口钳制与顶部长按拖动。 */

export const VIEW_MARGIN = 8;
const LONG_PRESS_MS = 200;
const LONG_PRESS_MOVE_CANCEL = 8;

export interface FloatingDragOptions {
  /** 拖动手柄选择器，默认 `[data-floating-drag-handle]`。 */
  handleSelector?: string;
  /** 开始拖动时（长按触发后）回调，用于模态框从 flex 居中切换为 fixed。 */
  onDragStart?: () => void;
  /** 拖动时加在根节点上的 class。 */
  draggingClass?: string;
}

/** 顶部拖动手柄 HTML（inner 为标题区内容）。 */
export function floatingDragBarHtml(innerHtml: string, hint = "按住拖动"): string {
  return `<div class="floating-drag-bar" data-floating-drag-handle title="长按此处拖动">
    <span class="floating-drag-grip" aria-hidden="true">⋮⋮</span>
    <div class="floating-drag-body">${innerHtml}</div>
    <span class="floating-drag-hint">${hint}</span>
  </div>`;
}

/** 将 fixed 定位的浮动元素钳制在视口内。 */
export function clampFloatingElement(el: HTMLElement, anchorX?: number, anchorY?: number): void {
  const margin = VIEW_MARGIN;
  const rect = el.getBoundingClientRect();
  const vw = window.innerWidth;
  const vh = window.innerHeight;
  let left = rect.left;
  let top = rect.top;
  const maxH = vh - margin * 2;

  if (rect.width > vw - margin * 2) {
    left = margin;
  } else if (rect.right > vw - margin) {
    left =
      anchorX !== undefined
        ? Math.max(margin, anchorX - rect.width - margin)
        : vw - margin - rect.width;
  }
  if (left < margin) left = margin;

  if (rect.height > maxH) {
    top = margin;
  } else if (rect.bottom > vh - margin) {
    top =
      anchorY !== undefined
        ? Math.max(margin, anchorY - rect.height - margin)
        : vh - margin - rect.height;
  }
  if (top < margin) top = margin;

  el.style.left = `${left}px`;
  el.style.top = `${top}px`;
}

/** 在 (clientX, clientY) 附近显示浮动元素并钳制。 */
export function positionFloatingAt(el: HTMLElement, clientX: number, clientY: number): void {
  const margin = VIEW_MARGIN;
  el.style.left = `${clientX + margin}px`;
  el.style.top = `${clientY + margin}px`;
  requestAnimationFrame(() => clampFloatingElement(el, clientX, clientY));
}

const activeCleanups = new WeakMap<HTMLElement, () => void>();

/** 在 root 上挂一次拖动手势（事件委托，子树刷新后仍有效）。 */
export function initFloatingPanelDrag(root: HTMLElement, opts?: FloatingDragOptions): void {
  if (root.dataset.floatingDragInit === "1") return;
  root.dataset.floatingDragInit = "1";

  const handleSelector = opts?.handleSelector ?? "[data-floating-drag-handle]";
  const draggingClass = opts?.draggingClass ?? "floating-panel-dragging";

  root.addEventListener("pointerdown", (e) => {
    if (e.button !== 0) return;
    const bar = (e.target as HTMLElement).closest<HTMLElement>(handleSelector);
    if (!bar || !root.contains(bar)) return;

    activeCleanups.get(root)?.();

    const startX = e.clientX;
    const startY = e.clientY;
    const rect = root.getBoundingClientRect();
    const grabOffsetX = startX - rect.left;
    const grabOffsetY = startY - rect.top;
    let armed = false;

    const arm = () => {
      if (armed) return;
      armed = true;
      opts?.onDragStart?.();
      root.classList.add(draggingClass);
      bar.classList.add("floating-drag-armed");
    };

    const timer = window.setTimeout(arm, LONG_PRESS_MS);

    const onMove = (ev: PointerEvent) => {
      const dx = ev.clientX - startX;
      const dy = ev.clientY - startY;
      if (!armed) {
        if (Math.hypot(dx, dy) > LONG_PRESS_MOVE_CANCEL) cleanup();
        return;
      }
      ev.preventDefault();
      const vw = window.innerWidth;
      const vh = window.innerHeight;
      const margin = VIEW_MARGIN;
      const w = root.offsetWidth;
      const h = root.offsetHeight;
      let left = ev.clientX - grabOffsetX;
      let top = ev.clientY - grabOffsetY;
      left = Math.max(margin, Math.min(left, vw - margin - w));
      top = Math.max(margin, Math.min(top, vh - margin - h));
      root.style.left = `${left}px`;
      root.style.top = `${top}px`;
    };

    const cleanup = () => {
      window.clearTimeout(timer);
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerup", onUp);
      window.removeEventListener("pointercancel", onUp);
      root.classList.remove(draggingClass);
      bar.classList.remove("floating-drag-armed");
      activeCleanups.delete(root);
    };

    const onUp = () => cleanup();

    activeCleanups.set(root, cleanup);
    window.addEventListener("pointermove", onMove);
    window.addEventListener("pointerup", onUp);
    window.addEventListener("pointercancel", onUp);
  });
}

/** 模态框从 flex 居中切换为 fixed，便于拖动与钳制。 */
export function pinModalPanel(panel: HTMLElement, backdrop: HTMLElement): void {
  if (panel.classList.contains("modal-panel-floating")) return;
  const rect = panel.getBoundingClientRect();
  backdrop.classList.add("modal-backdrop-pinned");
  panel.classList.add("modal-panel-floating");
  panel.style.left = `${rect.left}px`;
  panel.style.top = `${rect.top}px`;
  panel.style.width = `${rect.width}px`;
}

/** 打开模态框后：若底部被裁切则自动 pin 并上推。 */
export function ensureModalInViewport(panel: HTMLElement, backdrop: HTMLElement): void {
  requestAnimationFrame(() => {
    const margin = VIEW_MARGIN;
    const rect = panel.getBoundingClientRect();
    if (rect.bottom > window.innerHeight - margin || rect.top < margin) {
      pinModalPanel(panel, backdrop);
      clampFloatingElement(panel);
    }
  });
}

/** 为 #modal-root 内新打开的 .modal-panel 接线拖动。 */
export function setupModalPanelDrag(panel: HTMLElement, backdrop: HTMLElement): void {
  initFloatingPanelDrag(panel, {
    draggingClass: "modal-panel-dragging",
    onDragStart: () => pinModalPanel(panel, backdrop),
  });
  ensureModalInViewport(panel, backdrop);
}

/** 将 absolute + transform 居中的条转为 fixed（动画组框选条等）。 */
export function pinCenteredBar(el: HTMLElement): void {
  if (el.classList.contains("floating-bar-pinned")) return;
  const rect = el.getBoundingClientRect();
  el.classList.add("floating-bar-pinned");
  el.style.position = "fixed";
  el.style.transform = "none";
  el.style.left = `${rect.left}px`;
  el.style.top = `${rect.top}px`;
}
