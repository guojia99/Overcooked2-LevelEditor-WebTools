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
import { mountVersionBadge } from "./version";

mountVersionBadge();

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
    <div class="rl-view-switch">
      <button type="button" class="m-btn rl-view-btn active" data-view="recipes">菜谱视图</button>
      <button type="button" class="m-btn rl-view-btn" data-view="ingredients">食材清单</button>
    </div>
    <input type="search" id="rl-search" class="rl-search" placeholder="搜索菜名 / 英文名 / ID / 食材…" autocomplete="off">
    <label class="rl-tool-check" title="同一道菜的多 DLC 换皮变体只保留最高 DLC 一版（如只显示「什锦火锅（DLC10）」）">
      <input type="checkbox" id="rl-web-reps" checked> 隐藏DLC重复
    </label>
    <select id="rl-score" class="rl-select" title="按分数过滤">
      <option value="all">全部分数</option>
      <option value="20">20 分</option>
      <option value="40">40 分</option>
      <option value="60">60 分</option>
      <option value="80">80 分</option>
      <option value="100">100 分</option>
      <option value="120">120 分</option>
      <option value="other">其他</option>
    </select>
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
let scoreFilter: "all" | "other" | number = "all";
let showIntermediate = false;
let view: "recipes" | "ingredients" = "recipes";
let showWebReps = true;

/** 标准分数档位（其他 = 不在这些值内）。 */
const STANDARD_SCORES = [20, 40, 60, 80, 100, 120];
function scoreMatches(s: number | undefined, filter: "all" | "other" | number): boolean {
  if (filter === "all") return true;
  const v = s ?? 0;
  if (filter === "other") return !STANDARD_SCORES.includes(v);
  return v === filter;
}

/** Web 内置去重：规范化中文名（去 DLC 后缀/空白）作聚簇键，代表 = 最高 DLC（保留后缀）。 */
function webDedupKey(name: string): string {
  return String(name ?? "").replace(/·?DLC\d+/g, "").replace(/[（）()· ]/g, "");
}
function dlcNumber(id: string): number {
  const m = /^dlc(\d+)_/.exec(id ?? "");
  return m ? parseInt(m[1], 10) : 0;
}
function dedupWeb<T extends { id: string; group?: string; nameZh?: string }>(items: T[]): T[] {
  const reps = new Map<string, T>();
  const rest: T[] = [];
  for (const it of items) {
    if (it.group !== "web") {
      rest.push(it);
      continue;
    }
    const key = webDedupKey(it.nameZh ?? "");
    const cur = reps.get(key);
    if (!cur || dlcNumber(it.id) > dlcNumber(cur.id)) reps.set(key, it);
  }
  return [...rest, ...reps.values()];
}

function card(r: RecipeWithGroups): string {
  return rlCardHtml(r, {
    allRecipes: recipes,
    ingredientName: (id) => ingredientById.get(id)?.nameZh ?? id,
    // Web 内置徽标由 foodGroupLabel("web") 自动渲染，避免重复
    extraBadge: r.group === "levelset" ? "本关" : undefined,
  });
}

function visible(): RecipeWithGroups[] {
  const q = query.trim().toLowerCase();
  // 展示层去重：Web 拷贝（custom_web）与 Import 源库同 id 时只保留拷贝；旧 levelset 副本覆盖 web 项
  const levelsetIds = new Set(recipes.filter((r) => r.group === "levelset").map((r) => r.id));
  const webCopiedIds = new Set(
    recipes.filter((r) => r.group === "web" && (r.assetPath ?? "").includes("/custom_web/")).map((r) => r.id)
  );
  let out = recipes.filter((r) => {
    if (r.group === "web" && levelsetIds.has(r.id)) return false;
    if (r.group === "web" && webCopiedIds.has(r.id) && !(r.assetPath ?? "").includes("/custom_web/")) return false;
    if (!showIntermediate && r.intermediate) return false;
    if (typeFilter !== "all" && (r.type ?? "other") !== typeFilter) return false;
    if (groupFilter !== "all" && (r.group ?? "core") !== groupFilter) return false;
    if (!scoreMatches(r.score, scoreFilter)) return false;
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
  // Web 内置仅保留代表（多 DLC 换皮变体只显示最高 DLC）
  if (showWebReps) out = dedupWeb(out);
  return out;
}

function ingredientCard(i: IngredientEntry): string {
  const badge =
    i.group && i.group !== "core" ? ` <span class="pc-badge">${esc(foodGroupLabel(i.group))}</span>` : "";
  const en = (i.nameEn && i.nameEn.trim()) ? ` <span class="muted pc-en">${esc(i.nameEn)}</span>` : "";
  return `<div class="rl-ing-card" title="${esc(i.id)}">
    <img class="food-icon" loading="lazy" src="/icons/ingredients/${encodeURIComponent(i.id)}.png" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">
    <span class="rl-ing-name">${esc(i.nameZh)}${badge}${en}</span>
    <span class="muted small">${esc(i.id)}</span>
  </div>`;
}

function renderIngredients(): string {
  const q = query.trim().toLowerCase();
  const list = ingredients.filter((i) => {
    if (showWebReps && i.group === "web") return true; // 去重在下
    if (q && !`${i.nameZh} ${i.nameEn ?? ""} ${i.id}`.toLowerCase().includes(q)) return false;
    if (groupFilter !== "all" && (i.group ?? "core") !== groupFilter) return false;
    return true;
  });
  const deduped = showWebReps ? dedupWeb(list) : list;
  if (deduped.length === 0) return '<div class="rl-empty">没有匹配的食材，试试调整搜索或筛选条件</div>';
  const groups = new Map<string, IngredientEntry[]>();
  for (const i of deduped) {
    const g = i.group ?? "core";
    if (!groups.has(g)) groups.set(g, []);
    groups.get(g)!.push(i);
  }
  const order = [...groups.keys()].sort((a, b) => {
    const rank = (g: string) => (g === "core" ? 0 : g === "levelset" ? 1 : g === "web" ? 2 : 3);
    return rank(a) - rank(b) || a.localeCompare(b);
  });
  return order
    .map((g) => {
      const arr = groups.get(g)!;
      return `<section class="rl-section">
        <h2 class="rl-section-title">${esc(foodGroupLabel(g))}<span class="rl-section-count">${arr.length}</span></h2>
        <div class="rl-ing-grid">${arr.map(ingredientCard).join("")}</div>
      </section>`;
    })
    .join("");
}

function render(): void {
  const el = document.getElementById("rl-content")!;
  // 食材清单视图下隐藏菜谱类型 tag 与分数筛选（食材无分数）
  const typesEl = document.getElementById("rl-types");
  if (typesEl) typesEl.style.display = view === "recipes" ? "" : "none";
  const scoreEl = document.getElementById("rl-score");
  if (scoreEl) scoreEl.style.display = view === "recipes" ? "" : "none";
  if (view === "ingredients") {
    el.innerHTML = renderIngredients();
    return;
  }
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
  // 计数跟随 Web 去重：隐藏 DLC 重复变体时数量与展示一致
  const orderables = showWebReps
    ? dedupWeb(recipes.filter((r) => !r.intermediate))
    : recipes.filter((r) => !r.intermediate);
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
  document.getElementById("rl-score")!.addEventListener("change", (e) => {
    const v = (e.target as HTMLSelectElement).value;
    scoreFilter = v === "all" ? "all" : v === "other" ? "other" : Number(v);
    render();
  });
  document.getElementById("rl-intermediates")!.addEventListener("change", (e) => {
    showIntermediate = (e.target as HTMLInputElement).checked;
    render();
  });
  document.getElementById("rl-web-reps")!.addEventListener("change", (e) => {
    showWebReps = (e.target as HTMLInputElement).checked;
    buildFilters();
    render();
  });
  document.querySelectorAll<HTMLButtonElement>(".rl-view-btn").forEach((btn) => {
    btn.addEventListener("click", () => {
      view = (btn.dataset.view as "recipes" | "ingredients") ?? "recipes";
      document.querySelectorAll<HTMLButtonElement>(".rl-view-btn").forEach((b) =>
        b.classList.toggle("active", b === btn)
      );
      render();
    });
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
  const orderableVisible = dedupWeb(recipes.filter((r) => !r.intermediate)).length;
  setStatus(
    `共 ${recipes.length} 个菜谱（成品 ${orderable} · Web去重后 ${orderableVisible}）${bridgeUp ? "" : " · 静态数据"}`
  );
  buildFilters();
  render();
  wire();
}

void init();
