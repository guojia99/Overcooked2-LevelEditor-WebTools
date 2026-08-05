import * as api from "./api";
import type { IngredientEntry } from "./types";
import { navHtml, wireNav } from "./nav";
import { groupRecipesByType, recipeTypeLabel } from "./recipeTypes";
import { foodGroupLabel } from "./ingredientLabels";
import {
  rlCardHtml,
  rlSectionHtml,
  type RecipeWithGroups,
} from "./recipeCard";

const app = document.getElementById("app")!;
document.body.classList.add("manage-bg");

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function setStatus(msg: string, ok = true): void {
  const el = document.getElementById("rl-status");
  if (!el) return;
  el.textContent = msg;
  el.classList.toggle("err", !ok);
  el.classList.toggle("ok", ok && msg.length > 0);
}

function showError(e: unknown): void {
  const msg = e instanceof Error ? e.message : String(e);
  setStatus(msg, false);
  const el = document.getElementById("rl-content");
  if (el) el.innerHTML = `<div class="rl-empty">加载失败：${esc(msg)}</div>`;
}

app.innerHTML = `
  ${navHtml("recipes")}
  <div class="manage-bar">
    <h1 class="m-title">📖 菜谱清单列表</h1>
    <span class="status" id="rl-status">加载中…</span>
    <span style="flex: 1"></span>
    <label class="rl-tool-check" title="显示面糊、炸物部件、自选披萨部件等半成品">
      <input type="checkbox" id="rl-intermediates"> 含半成品
    </label>
    <select id="rl-group" class="rl-select" title="按来源筛选"></select>
  </div>
  <div class="rl-toolbar">
    <input type="search" id="rl-search" class="rl-search" placeholder="搜索菜名 / 英文名 / ID / 食材…" autocomplete="off">
    <div class="rl-chips" id="rl-types"></div>
  </div>
  <div class="manage-content rl-content" id="rl-content">
    <div class="rl-empty">加载中…</div>
  </div>
`;

// 本页是独立 HTML 入口（/recipes），hash 路由只在 index.html 下有效，
// 跳转其他页面必须整 URL 切回 /index.html#/...
wireNav((target) => {
  if (target === "layout") location.href = "/index.html#/layout";
  else if (target === "manage") location.href = "/index.html#/manage";
  else if (target === "custom-recipes") location.href = "/index.html#/custom-recipes";
});

let recipes: RecipeWithGroups[] = [];
let ingredients: IngredientEntry[] = [];
const ingredientById = new Map<string, IngredientEntry>();

let query = "";
let typeFilter = "all";
let groupFilter = "all";
let showIntermediate = false;

function card(r: RecipeWithGroups): string {
  return rlCardHtml(r, {
    allRecipes: recipes,
    ingredientName: (id) => ingredientById.get(id)?.nameZh ?? id,
  });
}

function visible(): RecipeWithGroups[] {
  const q = query.trim().toLowerCase();
  return recipes.filter((r) => {
    if (!showIntermediate && r.intermediate) return false;
    if (typeFilter !== "all" && (r.type ?? "other") !== typeFilter) return false;
    if (groupFilter !== "all" && (r.group ?? "core") !== groupFilter) return false;
    if (q) {
      const hay = [
        r.nameZh,
        r.nameEn ?? "",
        r.id,
        ...(r.ingredients ?? []).map((i) => ingredientById.get(i)?.nameZh ?? i),
      ]
        .join(" ")
        .toLowerCase();
      if (!hay.includes(q)) return false;
    }
    return true;
  });
}

function render(): void {
  const el = document.getElementById("rl-content")!;
  const vis = visible();
  if (vis.length === 0) {
    el.innerHTML = `<div class="rl-empty">没有匹配的菜谱，试试调整搜索或筛选条件</div>`;
    return;
  }
  el.innerHTML = groupRecipesByType(vis)
    .map(([type, arr]) => rlSectionHtml(type, arr.map(card).join(""), arr.length))
    .join("");
}

function buildFilters(): void {
  const orderables = recipes.filter((r) => !r.intermediate);
  const byType = groupRecipesByType(orderables);

  const chipsEl = document.getElementById("rl-types")!;
  const chips = [
    { type: "all", label: "全部", count: orderables.length },
    ...byType.map(([type, arr]) => ({ type, label: recipeTypeLabel(type), count: arr.length })),
  ];
  chipsEl.innerHTML = chips
    .map(
      (c) =>
        `<button type="button" class="rl-chip-btn${c.type === typeFilter ? " active" : ""}" data-type="${esc(c.type)}">${esc(c.label)}<span class="rl-cnt">${c.count}</span></button>`
    )
    .join("");

  const groupEl = document.getElementById("rl-group") as HTMLSelectElement;
  const groups = new Map<string, number>();
  for (const r of orderables) {
    const g = r.group ?? "core";
    groups.set(g, (groups.get(g) ?? 0) + 1);
  }
  const opts = ['<option value="all">全部来源</option>'];
  for (const [g, n] of groups) {
    opts.push(`<option value="${esc(g)}" ${g === groupFilter ? "selected" : ""}>${esc(foodGroupLabel(g))} (${n})</option>`);
  }
  groupEl.innerHTML = opts.join("");
}

function wire(): void {
  document.getElementById("rl-search")!.addEventListener("input", (e) => {
    query = (e.target as HTMLInputElement).value;
    render();
  });
  document.getElementById("rl-types")!.addEventListener("click", (e) => {
    const btn = (e.target as HTMLElement).closest<HTMLButtonElement>(".rl-chip-btn");
    if (!btn) return;
    typeFilter = btn.dataset.type ?? "all";
    document.querySelectorAll<HTMLButtonElement>(".rl-chip-btn").forEach((b) =>
      b.classList.toggle("active", b === btn)
    );
    render();
  });
  document.getElementById("rl-group")!.addEventListener("change", (e) => {
    groupFilter = (e.target as HTMLSelectElement).value;
    render();
  });
  document.getElementById("rl-intermediates")!.addEventListener("change", (e) => {
    showIntermediate = (e.target as HTMLInputElement).checked;
    render();
  });
}

async function init(): Promise<void> {
  try {
    const [recs, ings] = await Promise.all([
      api.fetchRecipeCatalog(""),
      api.fetchIngredients(),
    ]);
    recipes = recs as RecipeWithGroups[];
    ingredients = ings;
  } catch (e) {
    showError(e);
    return;
  }
  for (const ing of ingredients) ingredientById.set(ing.id, ing);

  const bridgeUp = await api.fetchHealth().catch(() => false);
  const orderable = recipes.filter((r) => !r.intermediate).length;
  setStatus(`共 ${recipes.length} 个菜谱（成品 ${orderable}）${bridgeUp ? "" : " · 静态数据"}`);
  buildFilters();
  render();
  wire();
}

void init();
