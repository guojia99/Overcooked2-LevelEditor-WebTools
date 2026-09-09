import { navHtml, wireNav } from "./nav";
import { GUIDE_TREE } from "./guide/content";
import { renderGuidePage, renderGuideSidebar, wireGuidePage } from "./guide/render";
import type { GuideNode } from "./guide/types";
import type { IngredientEntry, RecipeEntry } from "./types";
import { guidePath, navigateTo, parseRoute } from "./route";

export function goGuide(): void {
  navigateTo("guide");
}

function pageIdFromPath(): string {
  const route = parseRoute();
  if (route.page === "guide" && route.guidePageId) return route.guidePageId;
  return GUIDE_TREE[0]?.id ?? "overview";
}

async function loadCatalog(): Promise<{ ingredients: IngredientEntry[]; recipes: RecipeEntry[] }> {
  try {
    const ingRes = await fetch("/ingredients.json");
    const ingData = ingRes.ok ? await ingRes.json() : { ingredients: [] };
    const ingredients = (ingData.ingredients ?? []) as IngredientEntry[];

    let recipes: RecipeEntry[] = [];
    const recIndexRes = await fetch("/recipes.json");
    if (recIndexRes.ok) {
      const index = (await recIndexRes.json()) as { groupFiles?: Record<string, string> };
      const files = Object.values(index.groupFiles ?? {});
      if (files.length) {
        const groups = await Promise.all(
          files.map(async (f) => {
            const rr = await fetch(`/${f}`);
            if (!rr.ok) return [];
            const data = await rr.json();
            return (data.recipes ?? []) as RecipeEntry[];
          })
        );
        recipes = groups.flat();
      }
    }
    return { ingredients, recipes };
  } catch {
    return { ingredients: [], recipes: [] };
  }
}

type GuideCtx = { ingredients: IngredientEntry[]; recipes: RecipeEntry[] };

const shellHtml = () => `
  ${navHtml("guide")}
  <div class="manage-bar guide-bar">
    <h1 class="m-title">📘 功能说明</h1>
    <input type="search" id="guide-search" class="guide-search" placeholder="搜索全部章节…" autocomplete="off">
  </div>
  <div class="guide-layout">
    <aside class="guide-sidebar"></aside>
    <div class="guide-body manage-content"></div>
  </div>
`;

function renderPage(app: HTMLElement, ctx: GuideCtx, pageId: string, sectionId?: string): void {
  const sidebar = app.querySelector<HTMLElement>(".guide-sidebar");
  const body = app.querySelector<HTMLElement>(".guide-body");
  if (!sidebar || !body) return;

  const search = app.querySelector<HTMLInputElement>("#guide-search");
  const query = search?.value.trim().toLowerCase() ?? "";

  const idx = GUIDE_TREE.findIndex((c) => c.id === pageId);
  const chapter: GuideNode = GUIDE_TREE[idx] ?? GUIDE_TREE[0];
  sidebar.innerHTML = renderGuideSidebar(GUIDE_TREE, chapter.id);
  body.innerHTML = renderGuidePage(chapter, ctx, idx, GUIDE_TREE.length);
  body.scrollTop = 0;

  wireGuidePage(app, {
    chapters: GUIDE_TREE,
    pageId: chapter.id,
    onNavigatePage: (next, section) => {
      history.pushState({ guidePage: next }, "", guidePath(next));
      renderPage(app, ctx, next, section);
    },
  });

  if (sectionId) {
    requestAnimationFrame(() => {
      body.querySelector<HTMLElement>(`#guide-${CSS.escape(sectionId)}`)?.scrollIntoView({ block: "start" });
    });
  }

  const searchInput = app.querySelector<HTMLInputElement>("#guide-search");
  if (searchInput && query) {
    searchInput.value = query;
    searchInput.dispatchEvent(new Event("input", { bubbles: true }));
    if (!sectionId) searchInput.focus();
  }
}

export async function renderGuideView(app: HTMLElement, initialPageId?: string): Promise<void> {
  document.body.classList.add("manage-bg");
  const catalog = await loadCatalog();
  const ctx = { ingredients: catalog.ingredients, recipes: catalog.recipes };

  app.innerHTML = shellHtml();

  wireNav();

  const initial = initialPageId ?? pageIdFromPath();
  const canonical = guidePath(initial);
  if (location.pathname !== canonical) {
    history.replaceState({ guidePage: initial }, "", canonical);
  }
  renderPage(app, ctx, initial);

  window.addEventListener("popstate", () => {
    renderPage(app, ctx, pageIdFromPath());
  });
}
