import { dom } from "../dom";

export function hideDetail() {
  dom.detailEl.classList.add("hidden");
}

export function hideContextMenu() {
  dom.ctxMenuEl.classList.add("hidden");
}

export function hidePickTip() {
  dom.pickTipEl.classList.add("hidden");
}
