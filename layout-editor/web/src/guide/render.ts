import type { GuideBlock, GuideNode } from "./types";
import type { IngredientEntry, RecipeEntry } from "../types";
import {
  renderIconPathsBlock,
  renderIngredientSamples,
  renderRecipeSamples,
  renderUtensilIcons,
} from "./icons";

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function renderBlock(block: GuideBlock, ctx: GuideRenderContext): string {
  switch (block.type) {
    case "paragraph":
      return `<p>${block.text}</p>`;
    case "steps":
      return `<ol class="guide-steps">${block.items.map((s) => `<li>${esc(s)}</li>`).join("")}</ol>`;
    case "bullets":
      return `<ul class="guide-bullets">${block.items.map((s) => `<li>${s}</li>`).join("")}</ul>`;
    case "callout":
      return `<p class="guide-callout">${esc(block.text)}</p>`;
    case "note":
      return `<p class="guide-note">${esc(block.text)}</p>`;
    case "table":
      return `<table class="guide-table guide-table-grid"><thead><tr>${block.header
        .map((h) => `<th>${esc(h)}</th>`)
        .join("")}</tr></thead><tbody>${block.rows
        .map((r) => `<tr>${r.map((c) => `<td>${c}</td>`).join("")}</tr>`)
        .join("")}</tbody></table>`;
    case "kbdTable":
      return `<table class="guide-table"><tbody>${block.rows
        .map(([k, v]) => `<tr><th>${esc(k)}</th><td>${v}</td></tr>`)
        .join("")}</tbody></table>`;
    case "link":
      return `<p><a class="guide-link" href="${esc(block.href)}"${block.external ? ' target="_blank" rel="noopener"' : ""}>${esc(block.label)}</a></p>`;
    case "dynamic":
      switch (block.kind) {
        case "icon-paths":
          return renderIconPathsBlock();
        case "ingredient-samples":
          return renderIngredientSamples(ctx.ingredients);
        case "recipe-samples":
          return renderRecipeSamples(ctx.recipes);
        case "utensil-icons":
          return renderUtensilIcons();
        default:
          return "";
      }
  }
}

function renderBlocks(node: GuideNode, ctx: GuideRenderContext): string {
  if (!node.blocks?.length) return "";
  return `<div class="guide-card-body">${node.blocks.map((b) => renderBlock(b, ctx)).join("")}</div>`;
}

function countCards(node: GuideNode): number {
  if (!node.children?.length) return node.blocks?.length ? 1 : 0;
  return node.children.reduce((n, c) => n + countCards(c), 0);
}

function cardHtml(node: GuideNode, ctx: GuideRenderContext, depth: number): string {
  const body = renderBlocks(node, ctx);
  const kids = node.children?.map((c) => cardHtml(c, ctx, depth + 1)).join("") ?? "";
  if (!body && !kids) return "";
  const tag = depth >= 3 ? "h5" : "h4";
  return `
    <article class="guide-card guide-anchor" id="guide-${esc(node.id)}" data-guide-id="${esc(node.id)}">
      <div class="guide-card-accent"></div>
      <${tag} class="guide-card-title">${esc(node.title)}</${tag}>
      ${body}
      ${kids}
    </article>`;
}

function sectionHtml(node: GuideNode, ctx: GuideRenderContext, depth: number): string {
  const intro = renderBlocks(node, ctx);
  const kids = (node.children ?? [])
    .map((c) => (c.children?.length ? sectionHtml(c, ctx, depth + 1) : cardHtml(c, ctx, depth)))
    .join("");
  if (!intro && !kids) return "";
  const cls = depth === 1 ? "guide-section" : "guide-subsection";
  const tag = depth === 1 ? "h2" : "h3";
  return `
    <section class="${cls} guide-anchor" id="guide-${esc(node.id)}" data-guide-id="${esc(node.id)}">
      <${tag} class="${depth === 1 ? "guide-section-title" : "guide-subsection-title"}">${esc(node.title)}</${tag}>
      ${intro}
      ${kids}
    </section>`;
}

export type GuideRenderContext = {
  ingredients: IngredientEntry[];
  recipes: RecipeEntry[];
};

/** Render one full chapter page: hero + sections/cards. */
export function renderGuidePage(
  chapter: GuideNode,
  ctx: GuideRenderContext,
  pageIndex: number,
  pageCount: number,
): string {
  const sections = (chapter.children ?? [])
    .map((c) => (c.children?.length ? sectionHtml(c, ctx, 1) : cardHtml(c, ctx, 1)))
    .join("");
  const cards = countCards(chapter);
  const badge = String(pageIndex + 1).padStart(2, "0");
  return `
    <header class="guide-hero">
      <div class="guide-hero-badge">${badge}<span class="guide-hero-badge-total">/${pageCount}</span></div>
      <div class="guide-hero-main">
        <h1 class="guide-hero-title">${chapter.icon ? `<span class="guide-hero-icon">${chapter.icon}</span>` : ""}${esc(chapter.title)}</h1>
        ${chapter.desc ? `<p class="guide-hero-desc">${esc(chapter.desc)}</p>` : ""}
      </div>
      <div class="guide-hero-meta">${cards} 张功能卡片</div>
    </header>
    <div class="guide-page-body">${sections}</div>`;
}

function flatten(node: GuideNode, pageId: string, out: { node: GuideNode; pageId: string }[]): void {
  out.push({ node, pageId });
  node.children?.forEach((c) => flatten(c, pageId, out));
}

/** Sidebar: chapter page buttons for all chapters + section tree of the active chapter. */
export function renderGuideSidebar(chapters: GuideNode[], activeId: string): string {
  const pages = chapters
    .map(
      (c, i) =>
        `<button type="button" class="guide-page-btn${c.id === activeId ? " active" : ""}" data-page="${esc(c.id)}">` +
        `<span class="guide-page-num">${String(i + 1).padStart(2, "0")}</span>` +
        `<span class="guide-page-label">${c.icon ? `${c.icon} ` : ""}${esc(c.title)}</span>` +
        `</button>`,
    )
    .join("");

  const active = chapters.find((c) => c.id === activeId) ?? chapters[0];
  const tree = (active?.children ?? [])
    .map((n) => sidebarNodeHtml(n, 0))
    .join("");
  return `
    <div class="guide-sidebar-title">章节</div>
    <nav class="guide-pages">${pages}</nav>
    <div class="guide-sidebar-title guide-subtree-title">本页小节</div>
    <ul class="guide-tree-root">${tree}</ul>
    <ul class="guide-search-results" hidden></ul>`;
}

function sidebarNodeHtml(node: GuideNode, depth: number): string {
  const link = `<a class="guide-tree-link" href="#/guide" data-guide-id="${esc(node.id)}">${esc(node.title)}</a>`;
  if (!node.children?.length) {
    return `<li class="guide-tree-leaf" data-guide-title="${esc(node.title.toLowerCase())}" data-guide-id="${esc(node.id)}">${link}</li>`;
  }
  const kids = node.children.map((c) => sidebarNodeHtml(c, depth + 1)).join("");
  return `
    <li class="guide-tree-branch" data-guide-title="${esc(node.title.toLowerCase())}" data-guide-id="${esc(node.id)}">
      <details class="guide-tree-details"${depth < 1 ? " open" : ""}>
        <summary>${link}</summary>
        <ul class="guide-tree-nested">${kids}</ul>
      </details>
    </li>`;
}

/** Flat search index across every chapter (titles only, matching old behavior). */
export function buildGuideSearchIndex(chapters: GuideNode[]): { node: GuideNode; pageId: string }[] {
  const out: { node: GuideNode; pageId: string }[] = [];
  chapters.forEach((c) => flatten(c, c.id, out));
  return out;
}

function setActive(sidebar: HTMLElement, body: HTMLElement, id: string): void {
  sidebar.querySelectorAll<HTMLAnchorElement>(".guide-tree-link").forEach((a) => {
    a.classList.toggle("active", a.dataset.guideId === id);
  });
  const current = body.querySelector<HTMLElement>(".guide-anchor.active-card");
  current?.classList.remove("active-card");
  const target = body.querySelector<HTMLElement>(`#guide-${CSS.escape(id)}`);
  target?.classList.add("active-card");
}

export type GuideWireOptions = {
  chapters: GuideNode[];
  pageId: string;
  /** Switch to another chapter page (router supplied). */
  onNavigatePage: (pageId: string, sectionId?: string) => void;
};

export function wireGuidePage(root: HTMLElement, opts: GuideWireOptions): void {
  const sidebar = root.querySelector<HTMLElement>(".guide-sidebar");
  const body = root.querySelector<HTMLElement>(".guide-body");
  const search = root.querySelector<HTMLInputElement>("#guide-search");
  if (!sidebar || !body) return;

  const sidebarEl = sidebar;
  const bodyEl = body;

  const index = buildGuideSearchIndex(opts.chapters);

  sidebar.querySelectorAll<HTMLButtonElement>(".guide-page-btn").forEach((btn) => {
    btn.addEventListener("click", () => {
      const page = btn.dataset.page;
      if (page && page !== opts.pageId) opts.onNavigatePage(page);
    });
  });

  sidebar.querySelectorAll<HTMLAnchorElement>(".guide-tree-link").forEach((a) => {
    a.addEventListener("click", (e) => {
      e.preventDefault();
      const id = a.dataset.guideId;
      const el = id ? body.querySelector<HTMLElement>(`#guide-${CSS.escape(id)}`) : null;
      el?.scrollIntoView({ behavior: "smooth", block: "start" });
      if (id) setActive(sidebar, body, id);
    });
  });

  const observer = new IntersectionObserver(
    (entries) => {
      const visible = entries
        .filter((e) => e.isIntersecting)
        .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);
      const top = visible[0]?.target as HTMLElement | undefined;
      if (top?.dataset.guideId) setActive(sidebar, body, top.dataset.guideId);
    },
    { root: null, rootMargin: "-20% 0px -60% 0px", threshold: 0 },
  );
  body.querySelectorAll<HTMLElement>(".guide-anchor").forEach((s) => observer.observe(s));

  function renderSearchResults(q: string): void {
    const list = sidebarEl.querySelector<HTMLUListElement>(".guide-search-results");
    const treeRoots = sidebarEl.querySelectorAll<HTMLElement>(".guide-tree-root");
    const subtreeTitle = sidebarEl.querySelector<HTMLElement>(".guide-subtree-title");
    if (!list) return;
    if (!q) {
      list.hidden = true;
      list.innerHTML = "";
      treeRoots.forEach((t) => (t.style.display = ""));
      if (subtreeTitle) subtreeTitle.style.display = "";
      return;
    }
    treeRoots.forEach((t) => (t.style.display = "none"));
    if (subtreeTitle) subtreeTitle.style.display = "none";
    const hits = index.filter(({ node }) => node.title.toLowerCase().includes(q));
    const byPage = new Map<string, GuideNode[]>();
    hits.forEach(({ node, pageId }) => {
      const arr = byPage.get(pageId) ?? [];
      arr.push(node);
      byPage.set(pageId, arr);
    });
    const items: string[] = [];
    opts.chapters.forEach((ch) => {
      const nodes = byPage.get(ch.id);
      if (!nodes?.length) return;
      items.push(`<li class="guide-search-group">${ch.icon ?? "📄"} ${esc(ch.title)}</li>`);
      nodes.forEach((n) => {
        items.push(
          `<li class="guide-tree-leaf"><a class="guide-tree-link" href="#/guide" data-page="${esc(ch.id)}" data-guide-id="${esc(n.id)}">${esc(n.title)}</a></li>`,
        );
      });
    });
    list.innerHTML = items.length
      ? items.join("")
      : `<li class="guide-search-empty">没有匹配的小节</li>`;
    list.hidden = false;
    list.querySelectorAll<HTMLAnchorElement>(".guide-tree-link").forEach((a) => {
      a.addEventListener("click", (e) => {
        e.preventDefault();
        const page = a.dataset.page ?? opts.pageId;
        const id = a.dataset.guideId;
        if (page !== opts.pageId) opts.onNavigatePage(page, id);
        else {
          const el = id ? bodyEl.querySelector<HTMLElement>(`#guide-${CSS.escape(id)}`) : null;
          el?.scrollIntoView({ behavior: "smooth", block: "start" });
          if (id) setActive(sidebarEl, bodyEl, id);
        }
      });
    });
  }

  type SearchHost = HTMLInputElement & { _guideSearchHandler?: EventListener };
  const searchEl = search as SearchHost | null;
  if (searchEl) {
    if (searchEl._guideSearchHandler) {
      searchEl.removeEventListener("input", searchEl._guideSearchHandler);
    }
    const handler = () => renderSearchResults(searchEl.value.trim().toLowerCase());
    searchEl._guideSearchHandler = handler;
    searchEl.addEventListener("input", handler);
  }
}
