export interface BtnViewModel {
  tag: "button" | "a";
  className: string;
  attrs: Record<string, string>;
  label: string;
  innerHtml?: string;
}

export type BtnViewFn = (base: BtnViewModel) => BtnViewModel;

export interface ModalViewModel {
  panelClass: string;
  titleClass: string;
  bodyClass: string;
  footerClass: string;
}

export type ModalViewFn = (base: ModalViewModel) => ModalViewModel;

export type BtnVariant =
  | "cancel"
  | "primary"
  | "danger"
  | "default"
  | "small"
  | "small-primary"
  | "chip"
  | "link"
  | "nav";
