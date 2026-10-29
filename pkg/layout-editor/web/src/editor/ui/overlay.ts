import { dom } from "../dom";
import { syncDecorPreviewForSelection } from "./decorContextPreview";

export function hideDetail() {
  dom.detailEl.classList.add("hidden");
}

export function hideContextMenu() {
  dom.ctxMenuEl.classList.add("hidden");
  syncDecorPreviewForSelection();
}

export function hidePickTip() {
  dom.pickTipEl.classList.add("hidden");
}
