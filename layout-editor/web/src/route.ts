import { GUIDE_TREE } from "./guide/content";

export type NavPage =
  | "layout"
  | "manage"
  | "custom-recipes"
  | "burger-maker"
  | "recipes"
  | "guide"
  | "dependencies"
  | "changelog";

export type AppPage =
  | "layout"
  | "manage"
  | "dependencies"
  | "custom-recipes"
  | "burger-maker"
  | "guide"
  | "changelog";

export interface ParsedRoute {
  page: AppPage;
  guidePageId?: string;
}

function defaultGuidePageId(): string {
  return GUIDE_TREE[0]?.id ?? "overview";
}

function normalizeGuidePageId(id: string): string {
  return GUIDE_TREE.some((c) => c.id === id) ? id : defaultGuidePageId();
}

/** Normalize legacy hash / index.html URLs and canonicalize root paths. */
export function migrateLegacyUrl(): void {
  let path = location.pathname;
  const search = location.search;
  let hash = location.hash;

  if (path === "/index.html" || path.endsWith("/index.html")) {
    path = path.replace(/\/?index\.html$/, "") || "/";
    history.replaceState(null, "", path + search + hash);
  }

  if (hash.startsWith("#/")) {
    const target = hash.slice(1) + search;
    history.replaceState(null, "", target);
    hash = "";
  }

  path = location.pathname.replace(/\/+$/, "") || "/";
  if (path === "/guide") {
    history.replaceState(null, "", `/guide/${defaultGuidePageId()}${location.search}`);
    return;
  }

  if (path === "/") {
    const scene = new URLSearchParams(location.search).get("scene");
    if (scene) {
      history.replaceState(null, "", `/layout${location.search}`);
    } else {
      history.replaceState(null, "", "/manage");
    }
  }
}

export function parseRoute(pathname = location.pathname): ParsedRoute {
  const path = pathname.replace(/\/+$/, "") || "/";

  if (path === "/layout") return { page: "layout" };
  if (path === "/manage") return { page: "manage" };
  if (path === "/dependencies") return { page: "dependencies" };
  if (path === "/custom-recipes/burger-maker") return { page: "burger-maker" };
  if (path === "/custom-recipes") return { page: "custom-recipes" };
  if (path === "/changelog") return { page: "changelog" };

  const guideMatch = /^\/guide\/([A-Za-z0-9_-]+)$/.exec(path);
  if (guideMatch) {
    return { page: "guide", guidePageId: normalizeGuidePageId(guideMatch[1]) };
  }
  if (path === "/guide") {
    return { page: "guide", guidePageId: defaultGuidePageId() };
  }

  if (path === "/") {
    const scene = new URLSearchParams(location.search).get("scene");
    if (scene) return { page: "layout" };
    return { page: "manage" };
  }

  return { page: "layout" };
}

export function pathFor(page: NavPage): string {
  if (page === "recipes") return "/recipes";
  if (page === "burger-maker") return "/custom-recipes/burger-maker";
  if (page === "guide") return `/guide/${defaultGuidePageId()}`;
  return `/${page}`;
}

export function guidePath(pageId: string): string {
  return `/guide/${normalizeGuidePageId(pageId)}`;
}

/** Navigate to a top-level page (full navigation + reload for index.html SPA views). */
export function navigateTo(page: NavPage): void {
  if (page === "recipes") {
    location.href = "/recipes";
    return;
  }
  const target = pathFor(page);
  if (location.pathname === target && !location.search) {
    location.reload();
    return;
  }
  location.assign(target);
}
