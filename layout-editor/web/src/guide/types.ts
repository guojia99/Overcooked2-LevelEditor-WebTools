/** Structured content block for a guide section body. */
export type GuideBlock =
  | { type: "paragraph"; text: string }
  | { type: "steps"; items: string[] }
  | { type: "bullets"; items: string[] }
  | { type: "callout"; text: string }
  | { type: "kbdTable"; rows: [string, string][] }
  | { type: "link"; label: string; href: string; external?: boolean }
  | { type: "dynamic"; kind: "ingredient-samples" | "recipe-samples" | "utensil-icons" | "icon-paths" };

/** Tree node: branch (children) or leaf (blocks). Both may coexist (intro + children). */
export type GuideNode = {
  id: string;
  title: string;
  children?: GuideNode[];
  blocks?: GuideBlock[];
};
