import { getActiveTheme } from "../../theme";
import type { BtnViewFn, BtnViewModel, ModalViewFn, ModalViewModel } from "./types";

export function escapeAttr(value: string): string {
  return value
    .replace(/&/g, "&amp;")
    .replace(/"/g, "&quot;")
    .replace(/</g, "&lt;");
}

export function renderBtnModel(model: BtnViewModel, viewFn?: BtnViewFn): string {
  const fn = viewFn ?? ((m) => m);
  const m = fn(model);
  // Empty-string values are valid boolean HTML attributes (e.g. data-leave="", disabled="").
  const attrParts = Object.entries(m.attrs).map(([k, v]) =>
    v === "" ? k : `${k}="${escapeAttr(v)}"`
  );
  const typeAttr = m.tag === "button" ? ' type="button"' : "";
  const content = m.innerHtml
    ? `${escapeAttr(m.label)}${m.innerHtml}`
    : escapeAttr(m.label);
  return `<${m.tag}${typeAttr} class="${m.className}" ${attrParts.join(" ")}>${content}</${m.tag}>`;
}

export function applyThemeBtnViewFn(
  model: BtnViewModel,
  key:
    | "publicCancelBtnViewFn"
    | "publicPrimaryBtnViewFn"
    | "publicDangerBtnViewFn"
    | "publicMBtnViewFn"
    | "publicSmallBtnViewFn"
    | "publicChipBtnViewFn"
    | "publicLinkBtnViewFn"
    | "publicNavLinkViewFn"
): string {
  const theme = getActiveTheme();
  const fn = theme[key];
  return renderBtnModel(model, fn);
}

export function applyThemeModalView(model: ModalViewModel): ModalViewModel {
  const fn: ModalViewFn | undefined = getActiveTheme().publicModalPanelViewFn;
  return fn ? fn(model) : model;
}
