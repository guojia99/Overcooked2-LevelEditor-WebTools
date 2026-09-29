import { dom } from "../dom";
import {
  clampFloatingElement,
  floatingDragBarHtml,
  initFloatingPanelDrag,
  positionFloatingAt,
} from "./floatingPanelDrag";

/** 右键菜单顶部拖动手柄（inner 通常为 `.ctx-head` 块）。 */
export const contextMenuDragBarHtml = floatingDragBarHtml;

/** 显示菜单并钳制在视口内（超高菜单顶对齐并允许内部滚动）。 */
export function positionContextMenuAt(clientX: number, clientY: number): void {
  const el = dom.ctxMenuEl;
  el.classList.remove("hidden");
  positionFloatingAt(el, clientX, clientY);
}

/** 将已显示的菜单位置钳制到视口内。 */
export function clampContextMenuInViewport(anchorX?: number, anchorY?: number): void {
  const el = dom.ctxMenuEl;
  if (el.classList.contains("hidden")) return;
  clampFloatingElement(el, anchorX, anchorY);
}

/** 在 #ctx-menu 上挂一次拖动手势（事件委托，innerHTML 刷新后仍有效）。 */
export function initContextMenuDrag(): void {
  initFloatingPanelDrag(dom.ctxMenuEl, { draggingClass: "ctx-menu-dragging" });
}
