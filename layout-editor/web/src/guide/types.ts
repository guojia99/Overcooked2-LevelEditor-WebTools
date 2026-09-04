/** Structured content block for a guide section body. */
export type GuideBlock =
  | { type: "paragraph"; text: string }
  | { type: "steps"; items: string[] }
  | { type: "bullets"; items: string[] }
  | { type: "callout"; text: string }
  | { type: "note"; text: string }
  | { type: "kbdTable"; rows: [string, string][] }
  | { type: "table"; header: string[]; rows: string[][] }
  | { type: "link"; label: string; href: string; external?: boolean }
  | { type: "dynamic"; kind: "ingredient-samples" | "recipe-samples" | "utensil-icons" | "icon-paths" };

/**
 * Tree node: branch (children) or leaf (blocks). Both may coexist (intro + children).
 *
 * Depth conventions of the guide page:
 * - depth 0: chapter (always a branch) → one paginated page with a hero header
 * - depth 1: section heading (branch) or a standalone card (leaf)
 * - depth 2: subsection heading (branch) or a card (leaf)
 * - depth 3+: rendered as cards
 */
export type GuideNode = {
  id: string;
  title: string;
  /** Emoji icon shown in hero / page nav. */
  icon?: string;
  /** Short description shown under the hero title (chapters) or as tooltip. */
  desc?: string;
  children?: GuideNode[];
  blocks?: GuideBlock[];
};
