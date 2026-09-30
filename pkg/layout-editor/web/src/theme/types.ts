import type { BtnViewFn, ModalViewFn } from "../ui/views/types";

export type ThemeId = "black-gold" | "pink" | "sky-blue" | "white";

export interface ThemeDefinition {
  id: ThemeId;
  label: string;
  /** Swatch color shown in the theme picker. */
  swatch: string;
  cssDataTheme: string;
  publicCancelBtnViewFn?: BtnViewFn;
  publicPrimaryBtnViewFn?: BtnViewFn;
  publicDangerBtnViewFn?: BtnViewFn;
  publicMBtnViewFn?: BtnViewFn;
  publicSmallBtnViewFn?: BtnViewFn;
  publicChipBtnViewFn?: BtnViewFn;
  publicLinkBtnViewFn?: BtnViewFn;
  publicNavLinkViewFn?: BtnViewFn;
  publicModalPanelViewFn?: ModalViewFn;
}

export const THEME_STORAGE_KEY = "oc2-ui-theme";
