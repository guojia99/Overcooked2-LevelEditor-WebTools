import { GUIDE_TREE } from "./guide/content";

export type NavPage =
  | "layout"
  | "manage"
  | "custom-recipes"
  | "burger-maker"
  | "filling-maker"
  | "recipes"
  | "guide"
  | "dependencies"
  | "assignment"
  | "changelog";

export type AppPage =
  | "layout"
  | "manage"
  | "dependencies"
  | "assignment"
  | "custom-recipes"
  | "burger-maker"
  | "filling-maker"
  | "guide"
  | "changelog";

export interface ParsedRoute {
  page: AppPage;
  guidePageId?: string;
  /** 严格路由的关卡集段：/layout/{set}/...、/custom-recipes/{set}、/dependencies/{set} 等。 */
  setId?: string;
  /** /layout/{set}/{sceneName}（场景文件名，不含 .unity）。 */
  sceneName?: string;
  /** /custom-recipes/{set}/recipe/{recipeId}；保留字 "new" = 新建。 */
  recipeId?: string;
  /** /custom-recipes/burger-maker/{set}/{burgerId}（已保存成品汉堡的菜谱 id）。 */
  burgerId?: string;
  /** /custom-recipes/filling-maker/{set}/{fillingId}；保留字 "new" = 新建。 */
  fillingId?: string;
  /** /dependencies/{set}/{levelId}、/manage/{set}/{levelId}、/assignment/{set}/{levelId}（关卡数据目录名）。 */
  levelId?: string;
  /** /manage/{set}/{levelId}/summary（汇总页）；缺省 = 关卡详细编辑。 */
  manageView?: "summary";
}

function defaultGuidePageId(): string {
  return GUIDE_TREE[0]?.id ?? "overview";
}

function normalizeGuidePageId(id: string): string {
  return GUIDE_TREE.some((c) => c.id === id) ? id : defaultGuidePageId();
}

function seg(v: string): string {
  try {
    return decodeURIComponent(v);
  } catch {
    return v;
  }
}

function encSeg(v: string): string {
  return encodeURIComponent(v);
}

// ---- 严格路径构造 ----

export function layoutPath(set: string, scene: string): string {
  return `/layout/${encSeg(set)}/${encSeg(scene)}`;
}

export function depsPath(set?: string, levelId?: string): string {
  if (!set) return "/dependencies";
  return levelId ? `/dependencies/${encSeg(set)}/${encSeg(levelId)}` : `/dependencies/${encSeg(set)}`;
}

// ---- /manage 子路由（关卡列表 → 关卡详细编辑 / 汇总页；levelId = 关卡数据目录名）----

export function manageLevelListPath(set: string): string {
  return `/manage/${encSeg(set)}`;
}

export function manageLevelDetailPath(set: string, levelId: string): string {
  return `/manage/${encSeg(set)}/${encSeg(levelId)}`;
}

export function manageLevelSummaryPath(set: string, levelId: string): string {
  return `/manage/${encSeg(set)}/${encSeg(levelId)}/summary`;
}

/** 菜谱分工（关卡级）：/assignment/{set}/{levelId}（levelId = LevelInfo 资产所在数据目录名）。 */
export function assignmentPath(set: string, levelId: string): string {
  return `/assignment/${encSeg(set)}/${encSeg(levelId)}`;
}

export function recipeListPath(set: string): string {
  return `/custom-recipes/${encSeg(set)}`;
}

/** recipeId 传 "new" 表示新建。 */
export function recipeFormPath(set: string, recipeId: string): string {
  return `/custom-recipes/${encSeg(set)}/recipe/${encSeg(recipeId)}`;
}

export function burgerPath(set?: string, burgerId?: string): string {
  if (!set) return "/custom-recipes/burger-maker";
  return burgerId
    ? `/custom-recipes/burger-maker/${encSeg(set)}/${encSeg(burgerId)}`
    : `/custom-recipes/burger-maker/${encSeg(set)}`;
}

export function fillingPath(set?: string, fillingId?: string): string {
  if (!set) return "/custom-recipes/filling-maker";
  return fillingId
    ? `/custom-recipes/filling-maker/${encSeg(set)}/${encSeg(fillingId)}`
    : `/custom-recipes/filling-maker/${encSeg(set)}`;
}

/** 旧版 /layout?scene=Assets/LevelSets/<set>/scenes/<scene>.unity → 严格路径；解析失败返回 null。 */
function strictLayoutFromSceneQuery(): string | null {
  const scene = new URLSearchParams(location.search).get("scene");
  if (!scene) return null;
  const m = /\/LevelSets\/([^/]+)\/scenes\/([^/]+)\.unity$/i.exec(scene.replace(/\\/g, "/"));
  if (!m) return null;
  return layoutPath(m[1], m[2]);
}

/** Normalize legacy hash / index.html / query-param URLs and canonicalize root paths. */
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

  // 旧版 query 深链 → 严格路由（旧书签 / 外部工具链接不断链）
  const strictLayout = strictLayoutFromSceneQuery();
  if (strictLayout && (path === "/layout" || path === "/")) {
    history.replaceState(null, "", strictLayout);
    return;
  }
  if (path === "/custom-recipes/filling-maker") {
    const set = new URLSearchParams(location.search).get("set");
    if (set) {
      history.replaceState(null, "", fillingPath(set));
      return;
    }
  }

  if (path === "/guide") {
    history.replaceState(null, "", `/guide/${defaultGuidePageId()}${location.search}`);
    return;
  }

  if (path === "/") {
    history.replaceState(null, "", "/manage");
  }
}

export function parseRoute(pathname = location.pathname): ParsedRoute {
  const path = pathname.replace(/\/+$/, "") || "/";

  if (path === "/layout") return { page: "layout" };
  let m = /^\/layout\/([^/]+)\/([^/]+)$/.exec(path);
  if (m) return { page: "layout", setId: seg(m[1]), sceneName: seg(m[2]) };

  if (path === "/manage") return { page: "manage" };
  // /manage 子路由（注意先匹配三段 summary，再两段详细编辑，最后一段关卡列表）
  m = /^\/manage\/([^/]+)\/([^/]+)\/summary$/.exec(path);
  if (m) return { page: "manage", setId: seg(m[1]), levelId: seg(m[2]), manageView: "summary" };
  m = /^\/manage\/([^/]+)\/([^/]+)$/.exec(path);
  if (m) return { page: "manage", setId: seg(m[1]), levelId: seg(m[2]) };
  m = /^\/manage\/([^/]+)$/.exec(path);
  if (m) return { page: "manage", setId: seg(m[1]) };
  if (path === "/changelog") return { page: "changelog" };

  if (path === "/dependencies") return { page: "dependencies" };
  m = /^\/dependencies\/([^/]+)\/([^/]+)$/.exec(path);
  if (m) return { page: "dependencies", setId: seg(m[1]), levelId: seg(m[2]) };
  m = /^\/dependencies\/([^/]+)$/.exec(path);
  if (m) return { page: "dependencies", setId: seg(m[1]) };

  // 菜谱分工（关卡级严格路由；裸 /assignment 无意义，落到默认 layout→manage）
  m = /^\/assignment\/([^/]+)\/([^/]+)$/.exec(path);
  if (m) return { page: "assignment", setId: seg(m[1]), levelId: seg(m[2]) };

  m = /^\/custom-recipes\/burger-maker\/([^/]+)\/([^/]+)$/.exec(path);
  if (m) return { page: "burger-maker", setId: seg(m[1]), burgerId: seg(m[2]) };
  m = /^\/custom-recipes\/burger-maker\/([^/]+)$/.exec(path);
  if (m) return { page: "burger-maker", setId: seg(m[1]) };
  if (path === "/custom-recipes/burger-maker") return { page: "burger-maker" };

  m = /^\/custom-recipes\/filling-maker\/([^/]+)\/([^/]+)$/.exec(path);
  if (m) return { page: "filling-maker", setId: seg(m[1]), fillingId: seg(m[2]) };
  m = /^\/custom-recipes\/filling-maker\/([^/]+)$/.exec(path);
  if (m) return { page: "filling-maker", setId: seg(m[1]) };
  if (path === "/custom-recipes/filling-maker") return { page: "filling-maker" };

  if (path === "/custom-recipes") return { page: "custom-recipes" };
  m = /^\/custom-recipes\/([^/]+)\/recipe\/([^/]+)$/.exec(path);
  if (m) return { page: "custom-recipes", setId: seg(m[1]), recipeId: seg(m[2]) };
  m = /^\/custom-recipes\/([^/]+)$/.exec(path);
  if (m) return { page: "custom-recipes", setId: seg(m[1]) };

  const guideMatch = /^\/guide\/([A-Za-z0-9_-]+)$/.exec(path);
  if (guideMatch) {
    return { page: "guide", guidePageId: normalizeGuidePageId(guideMatch[1]) };
  }
  if (path === "/guide") {
    return { page: "guide", guidePageId: defaultGuidePageId() };
  }

  if (path === "/") return { page: "manage" };

  return { page: "layout" };
}

export function pathFor(page: NavPage): string {
  if (page === "recipes") return "/recipes";
  if (page === "burger-maker") return "/custom-recipes/burger-maker";
  if (page === "filling-maker") return "/custom-recipes/filling-maker";
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
