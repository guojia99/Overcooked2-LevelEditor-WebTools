import { applyThemeModalView } from "./render";
import type { ModalViewModel } from "./types";

export function resolveModalClasses(panelClass = ""): ModalViewModel {
  const base: ModalViewModel = {
    panelClass: panelClass ? `modal-panel ${panelClass}` : "modal-panel",
    titleClass: "modal-title",
    bodyClass: "modal-body",
    footerClass: "modal-footer",
  };
  return applyThemeModalView(base);
}

export function modalPanelClassAttr(panelClass = ""): string {
  const { panelClass: cls } = resolveModalClasses(panelClass);
  return cls;
}
