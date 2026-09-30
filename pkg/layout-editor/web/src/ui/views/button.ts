import { applyThemeBtnViewFn } from "./render";
import type { BtnViewModel } from "./types";

function baseBtn(
  className: string,
  label: string,
  attrs: Record<string, string> = {},
  tag: "button" | "a" = "button"
): BtnViewModel {
  return { tag, className, label, attrs, innerHtml: undefined };
}

function mergeAttrs(
  attrs: Record<string, string>,
  extra?: Record<string, string>
): Record<string, string> {
  return { ...attrs, ...extra };
}

/** Cancel / secondary modal button. */
export function cancelBtnHtml(
  label = "取消",
  attrs: Record<string, string> = {}
): string {
  return applyThemeBtnViewFn(
    baseBtn("modal-btn", label, mergeAttrs(attrs, { "data-cancel": "" })),
    "publicCancelBtnViewFn"
  );
}

/** Primary confirm / save modal button. */
export function primaryBtnHtml(
  label = "确定",
  attrs: Record<string, string> = {}
): string {
  return applyThemeBtnViewFn(
    baseBtn("modal-btn primary", label, mergeAttrs(attrs, { "data-ok": "" })),
    "publicPrimaryBtnViewFn"
  );
}

/** Modal footer pair: cancel + primary. */
export function modalFooterBtnsHtml(
  cancelLabel = "取消",
  okLabel = "确定",
  cancelAttrs: Record<string, string> = {},
  okAttrs: Record<string, string> = {}
): string {
  return `${cancelBtnHtml(cancelLabel, cancelAttrs)}\n     ${primaryBtnHtml(okLabel, okAttrs)}`;
}

/** Danger / delete button (manage page style). */
export function dangerBtnHtml(
  label: string,
  attrs: Record<string, string> = {}
): string {
  return applyThemeBtnViewFn(
    baseBtn("m-btn danger", label, attrs),
    "publicDangerBtnViewFn"
  );
}

export type MBtnVariant = "default" | "primary" | "danger" | "small";

/** Manage-page cancel button (m-btn style). */
export function mCancelBtnHtml(
  label = "取消",
  attrs: Record<string, string> = {}
): string {
  return mBtnHtml(label, "default", mergeAttrs(attrs, { "data-cancel": "" }));
}

/** Manage-page primary confirm button (m-btn style). */
export function mPrimaryBtnHtml(
  label = "确定",
  attrs: Record<string, string> = {}
): string {
  return mBtnHtml(label, "primary", mergeAttrs(attrs, { "data-ok": "" }));
}

/** Manage-page button. */
export function mBtnHtml(
  label: string,
  variant: MBtnVariant = "default",
  attrs: Record<string, string> = {},
  extraClass = "",
  innerHtml?: string
): string {
  const base =
    variant === "primary"
      ? "m-btn primary"
      : variant === "danger"
        ? "m-btn danger"
        : variant === "small"
          ? "m-btn small"
          : "m-btn";
  const cls = extraClass ? `${base} ${extraClass.trim()}` : base;
  const model: BtnViewModel = { ...baseBtn(cls, label, attrs), innerHtml };
  return applyThemeBtnViewFn(model, "publicMBtnViewFn");
}

export type SmallBtnVariant = "default" | "primary" | "pick-add" | "danger";

/** Small inline panel button. */
export function smallBtnHtml(
  label: string,
  variant: SmallBtnVariant = "default",
  attrs: Record<string, string> = {}
): string {
  const cls =
    variant === "primary"
      ? "btn-small primary"
      : variant === "pick-add"
        ? "btn-small pick-add"
        : variant === "danger"
          ? "btn-small btn-danger"
          : "btn-small";
  return applyThemeBtnViewFn(baseBtn(cls, label, attrs), "publicSmallBtnViewFn");
}

/** Recipe list filter chip button. */
export function chipBtnHtml(
  label: string,
  active = false,
  attrs: Record<string, string> = {},
  extraClass = "",
  innerHtml?: string
): string {
  const base = active ? "rl-chip-btn active" : "rl-chip-btn";
  const cls = extraClass ? `${base} ${extraClass.trim()}` : base;
  const model: BtnViewModel = { ...baseBtn(cls, label, attrs), innerHtml };
  return applyThemeBtnViewFn(model, "publicChipBtnViewFn");
}

/** Text-style link button. */
export function linkBtnHtml(
  label: string,
  attrs: Record<string, string> = {}
): string {
  return applyThemeBtnViewFn(
    baseBtn("link-btn", label, attrs),
    "publicLinkBtnViewFn"
  );
}

/** Top navigation tab button. */
export function navLinkBtnHtml(
  label: string,
  active = false,
  attrs: Record<string, string> = {}
): string {
  const cls = active ? "topnav-link active" : "topnav-link";
  return applyThemeBtnViewFn(baseBtn(cls, label, attrs), "publicNavLinkViewFn");
}

/** Generic modal button with explicit class list. */
export function modalBtnHtml(
  label: string,
  className = "modal-btn",
  attrs: Record<string, string> = {}
): string {
  const isPrimary = className.includes("primary");
  const isDanger = className.includes("danger");
  const isCancel = attrs["data-cancel"] !== undefined;
  const key = isPrimary
    ? "publicPrimaryBtnViewFn"
    : isDanger
      ? "publicDangerBtnViewFn"
      : "publicCancelBtnViewFn";
  return applyThemeBtnViewFn(baseBtn(className, label, attrs), key);
}
