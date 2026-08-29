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

function renderSectionBody(node: GuideNode, ctx: GuideRenderContext): string {
  if (!node.blocks?.length) return "";
  return node.blocks.map((b) => renderBlock(b, ctx)).join("");
}

function renderContentSection(node: GuideNode, ctx: GuideRenderContext, depth: number): string {
  const tag = depth <= 1 ? "h2" : depth === 2 ? "h3" : "h4";
  const body = renderSectionBody(node, ctx);
  const childHtml = node.children?.map((c) => renderContentSection(c, ctx, depth + 1)).join("") ?? "";
  const hasBody = !!body;
  const hasChildren = !!childHtml;
  if (!hasBody && !hasChildren) return "";
  return `
    <section class="guide-anchor" id="guide-${esc(node.id)}" data-guide-id="${esc(node.id)}">
      <${tag} class="guide-heading">${esc(node.title)}</${tag}>
      ${body}
      ${childHtml}
    </section>`;
}

function renderTreeNode(node: GuideNode, depth: number): string {
  const hasChildren = node.children && node.children.length > 0;
  const link = `<a class="guide-tree-link" href="#guide-${esc(node.id)}" data-guide-id="${esc(node.id)}">${esc(node.title)}</a>`;
  if (!hasChildren) {
    return `<li class="guide-tree-leaf" data-guide-title="${esc(node.title.toLowerCase())}">${link}</li>`;
  }
  const open = depth < 1 ? " open" : "";
  const kids = node.children!.map((c) => renderTreeNode(c, depth + 1)).join("");
  return `
    <li class="guide-tree-branch" data-guide-title="${esc(node.title.toLowerCase())}">
      <details class="guide-tree-details"${open}>
        <summary>${link}</summary>
        <ul class="guide-tree-nested">${kids}</ul>
      </details>
    </li>`;
}

export type GuideRenderContext = {
  ingredients: IngredientEntry[];
  recipes: RecipeEntry[];
};

export function renderGuideTree(nodes: GuideNode[]): string {
  return `<ul class="guide-tree-root">${nodes.map((n) => renderTreeNode(n, 0)).join("")}</ul>`;
}

export function renderGuideBody(nodes: GuideNode[], ctx: GuideRenderContext): string {
  return nodes.map((n) => renderContentSection(n, ctx, 0)).join("");
}

export function wireGuidePage(root: HTMLElement): void {
  const sidebar = root.querySelector<HTMLElement>(".guide-sidebar");
  const body = root.querySelector<HTMLElement>(".guide-body");
  const search = root.querySelector<HTMLInputElement>("#guide-search");
  if (!sidebar || !body) return;

  const links = () => Array.from(sidebar.querySelectorAll<HTMLAnchorElement>(".guide-tree-link"));
  const sections = () => Array.from(body.querySelectorAll<HTMLElement>(".guide-anchor"));

  links().forEach((a) => {
    a.addEventListener("click", (e) => {
      e.preventDefault();
      const id = a.dataset.guideId;
      const el = id ? body.querySelector<HTMLElement>(`#guide-${CSS.escape(id)}`) : null;
      el?.scrollIntoView({ behavior: "smooth", block: "start" });
      history.replaceState(null, "", `#guide-${id}`);
      setActive(id ?? "");
    });
  });

  function setActive(id: string): void {
    links().forEach((a) => a.classList.toggle("active", a.dataset.guideId === id));
  }

  const observer = new IntersectionObserver(
    (entries) => {
      const visible = entries
        .filter((e) => e.isIntersecting)
        .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);
      const top = visible[0]?.target as HTMLElement | undefined;
      if (top?.dataset.guideId) setActive(top.dataset.guideId);
    },
    { root: null, rootMargin: "-20% 0px -60% 0px", threshold: 0 }
  );
  sections().forEach((s) => observer.observe(s));

  search?.addEventListener("input", () => {
    const q = search.value.trim().toLowerCase();
    const branches = sidebar.querySelectorAll<HTMLElement>(".guide-tree-branch, .guide-tree-leaf");
    branches.forEach((li) => {
      const title = li.dataset.guideTitle ?? "";
      const nested = li.querySelectorAll<HTMLElement>(".guide-tree-branch, .guide-tree-leaf");
      let match = !q || title.includes(q);
      nested.forEach((child) => {
        const ct = child.dataset.guideTitle ?? "";
        if (ct.includes(q)) {
          match = true;
          child.style.display = "";
          child.closest("details")?.setAttribute("open", "");
        } else if (q) {
          child.style.display = "none";
        } else {
          child.style.display = "";
        }
      });
      li.style.display = match || !q ? "" : "none";
      if (match && q) li.querySelector("details")?.setAttribute("open", "");
    });
  });

  const hash = location.hash.match(/^#guide-(.+)$/);
  if (hash?.[1]) {
    requestAnimationFrame(() => {
      body.querySelector<HTMLElement>(`#guide-${CSS.escape(hash[1])}`)?.scrollIntoView({ block: "start" });
      setActive(hash[1]);
    });
  }
}
