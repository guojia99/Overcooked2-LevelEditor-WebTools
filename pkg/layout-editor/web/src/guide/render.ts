import { guidePath } from "../route";
import type { GuideBlock, GuideNode } from "./types";
import type { IngredientEntry, RecipeEntry } from "../types";
import {
  renderIconPathsBlock,
  renderIngredientSamples,
  renderRecipeSamples,
  renderUtensilIcons,
} from "./icons";
import { renderChangelogFromMd } from "../changelog";

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function stripHtml(s: string): string {
  return s.replace(/<[^>]+>/g, "");
}

/** 侧栏结果里把 query 包成 <mark>（纯文本输入）。 */
function highlightMatch(text: string, query: string): string {
  if (!query) return esc(text);
  const lower = text.toLowerCase();
  const q = query.toLowerCase();
  let out = "";
  let i = 0;
  while (i < text.length) {
    const idx = lower.indexOf(q, i);
    if (idx < 0) {
      out += esc(text.slice(i));
      break;
    }
    out += esc(text.slice(i, idx));
    out += `<mark class="guide-search-mark">${esc(text.slice(idx, idx + q.length))}</mark>`;
    i = idx + q.length;
  }
  return out;
}

/** 可搜索正文：标题 + 各 block 纯文本（不含 dynamic）。 */
function nodeSearchText(node: GuideNode): string {
  const parts: string[] = [node.title];
  for (const b of node.blocks ?? []) {
    switch (b.type) {
      case "paragraph":
        parts.push(stripHtml(b.text));
        break;
      case "steps":
      case "bullets":
        parts.push(...b.items.map(stripHtml));
        break;
      case "callout":
      case "note":
        parts.push(b.text);
        break;
      case "code":
        parts.push(b.code);
        break;
      case "table":
        parts.push(...b.header, ...b.rows.flat().map(stripHtml));
        break;
      case "kbdTable":
        parts.push(...b.rows.flat());
        break;
      case "link":
        parts.push(b.label);
        break;
      default:
        break;
    }
  }
  return parts.join(" ");
}

function nodeMatchesQuery(node: GuideNode, q: string): boolean {
  return nodeSearchText(node).toLowerCase().includes(q);
}

function clearBodySearchHighlights(body: HTMLElement): void {
  body.querySelectorAll("mark.guide-search-mark").forEach((mark) => {
    const parent = mark.parentNode;
    if (!parent) return;
    parent.replaceChild(document.createTextNode(mark.textContent ?? ""), mark);
    parent.normalize();
  });
  body.querySelectorAll(".guide-search-match").forEach((el) => el.classList.remove("guide-search-match"));
}

/** 在元素子树文本节点中包裹匹配片段（跳过已有 mark）。 */
function highlightTextNodes(root: HTMLElement, query: string): void {
  const q = query.toLowerCase();
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
    acceptNode(node) {
      if (node.parentElement?.closest("mark.guide-search-mark")) return NodeFilter.FILTER_REJECT;
      const t = node.textContent ?? "";
      return t.trim() && t.toLowerCase().includes(q) ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_SKIP;
    },
  });
  const nodes: Text[] = [];
  while (walker.nextNode()) nodes.push(walker.currentNode as Text);

  for (const node of nodes) {
    const text = node.textContent ?? "";
    const lower = text.toLowerCase();
    let idx = lower.indexOf(q);
    if (idx < 0) continue;
    const frag = document.createDocumentFragment();
    let last = 0;
    while (idx >= 0) {
      if (idx > last) frag.appendChild(document.createTextNode(text.slice(last, idx)));
      const mark = document.createElement("mark");
      mark.className = "guide-search-mark";
      mark.textContent = text.slice(idx, idx + query.length);
      frag.appendChild(mark);
      last = idx + query.length;
      idx = lower.indexOf(q, last);
    }
    if (last < text.length) frag.appendChild(document.createTextNode(text.slice(last)));
    node.parentNode?.replaceChild(frag, node);
  }
}

function applyBodySearchHighlights(
  body: HTMLElement,
  query: string,
  index: { node: GuideNode; pageId: string }[],
): void {
  clearBodySearchHighlights(body);
  if (!query) return;
  const matchIds = new Set(index.filter(({ node }) => nodeMatchesQuery(node, query)).map(({ node }) => node.id));
  body.querySelectorAll<HTMLElement>(".guide-anchor").forEach((el) => {
    const id = el.dataset.guideId;
    if (!id || !matchIds.has(id)) return;
    el.classList.add("guide-search-match");
    highlightTextNodes(el, query);
  });
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
    case "code":
      return `<pre class="guide-code"><code data-language="${esc(block.language ?? "text")}">${esc(block.code)}</code></pre>`;
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
        case "changelog":
          return `<div class="changelog-content guide-changelog">${renderChangelogFromMd(ctx.changelogMd ?? "")}</div>`;
        case "mcp-http-debug":
          return `<div class="mcp-http-debug" data-mcp-debug>
            <div class="mcp-debug-actions">
              <button type="button" class="btn secondary" data-mcp-action="health">检查 Bridge</button>
              <button type="button" class="btn secondary" data-mcp-action="manifest">读取清单</button>
              <button type="button" class="btn secondary" data-mcp-action="tools">读取工具</button>
            </div>
            <pre class="mcp-debug-output" data-mcp-output>尚未执行请求。</pre>
          </div>`;
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
  changelogMd?: string;
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
  const link = `<a class="guide-tree-link" href="#guide-${esc(node.id)}" data-guide-id="${esc(node.id)}">${esc(node.title)}</a>`;
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
      clearBodySearchHighlights(bodyEl);
      treeRoots.forEach((t) => (t.style.display = ""));
      if (subtreeTitle) subtreeTitle.style.display = "";
      return;
    }
    treeRoots.forEach((t) => (t.style.display = "none"));
    if (subtreeTitle) subtreeTitle.style.display = "none";
    const hits = index.filter(({ node }) => nodeMatchesQuery(node, q));
    applyBodySearchHighlights(bodyEl, q, index);
    const byPage = new Map<string, GuideNode[]>();
    hits.forEach(({ node, pageId }) => {
      const arr = byPage.get(pageId) ?? [];
      arr.push(node);
      byPage.set(pageId, arr);
    });
    const items: string[] = [];
    if (hits.length) {
      items.push(`<li class="guide-search-count">找到 ${hits.length} 个小节</li>`);
    }
    opts.chapters.forEach((ch) => {
      const nodes = byPage.get(ch.id);
      if (!nodes?.length) return;
      items.push(`<li class="guide-search-group">${ch.icon ?? "📄"} ${esc(ch.title)}</li>`);
      nodes.forEach((n) => {
        items.push(
          `<li class="guide-tree-leaf guide-search-hit"><a class="guide-tree-link guide-search-link" href="${guidePath(ch.id)}" data-page="${esc(ch.id)}" data-guide-id="${esc(n.id)}">${highlightMatch(n.title, q)}</a></li>`,
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
