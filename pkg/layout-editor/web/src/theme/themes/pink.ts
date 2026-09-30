import type { ThemeDefinition } from "../types";

export const pinkTheme: ThemeDefinition = {
  id: "pink",
  label: "粉红",
  swatch: "#E75098",
  cssDataTheme: "pink",
  publicCancelBtnViewFn: (base) => ({
    ...base,
    className: `${base.className} ui-btn-cancel`,
    attrs: { ...base.attrs, "data-ui-variant": "cancel" },
  }),
  publicPrimaryBtnViewFn: (base) => ({
    ...base,
    className: `${base.className} ui-btn-primary`,
    attrs: { ...base.attrs, "data-ui-variant": "primary" },
  }),
  publicDangerBtnViewFn: (base) => ({
    ...base,
    className: `${base.className} ui-btn-danger`,
    attrs: { ...base.attrs, "data-ui-variant": "danger" },
  }),
  publicMBtnViewFn: (base) => ({
    ...base,
    className: `${base.className} ui-btn-m`,
    attrs: { ...base.attrs, "data-ui-variant": "m" },
  }),
  publicSmallBtnViewFn: (base) => ({
    ...base,
    className: `${base.className} ui-btn-small`,
    attrs: { ...base.attrs, "data-ui-variant": "small" },
  }),
  publicChipBtnViewFn: (base) => ({
    ...base,
    className: `${base.className} ui-btn-chip`,
    attrs: { ...base.attrs, "data-ui-variant": "chip" },
  }),
  publicModalPanelViewFn: (base) => ({
    ...base,
    panelClass: `${base.panelClass} ui-modal-panel`,
  }),
};
