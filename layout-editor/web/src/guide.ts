import { navHtml, wireNav } from "./nav";
import { GUIDE_TREE } from "./guide/content";
import { renderGuideBody, renderGuideTree, wireGuidePage } from "./guide/render";
import type { IngredientEntry, RecipeEntry } from "./types";

export function goGuide(): void {
  location.hash = "#/guide";
  location.reload();
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

export async function renderGuideView(app: HTMLElement): Promise<void> {
  document.body.classList.add("manage-bg");
  const catalog = await loadCatalog();
  const ctx = { ingredients: catalog.ingredients, recipes: catalog.recipes };

  app.innerHTML = `
    ${navHtml("guide")}
    <div class="manage-bar guide-bar">
      <h1 class="m-title">📘 功能说明</h1>
      <input type="search" id="guide-search" class="guide-search" placeholder="搜索目录…" autocomplete="off">
    </div>
    <div class="guide-layout">
      <aside class="guide-sidebar">
        <div class="guide-sidebar-title">目录</div>
        ${renderGuideTree(GUIDE_TREE)}
      </aside>
      <div class="guide-body manage-content">
        <p class="guide-intro modal-hint">左侧树状目录可折叠；点击条目跳转到对应说明。带图标的章节展示代表食材与菜谱示例。</p>
        ${renderGuideBody(GUIDE_TREE, ctx)}
      </div>
    </div>
  `;

  wireNav((target) => {
    if (target === "layout") {
      location.hash = "#/layout";
      location.reload();
    } else if (target === "manage") {
      location.hash = "#/manage";
      location.reload();
    } else if (target === "custom-recipes") {
      location.hash = "#/custom-recipes";
      location.reload();
    } else if (target === "recipes") {
      location.href = "/recipes";
    }
  });

  wireGuidePage(app);
}
