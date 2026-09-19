import * as api from "./api";
import type { IngredientEntry } from "./types";
import { navHtml, wireNav } from "./nav";
import { groupRecipesByType, recipeTypeLabel, burgerSubtypeLabel, burgerSubtypeOrder } from "./recipeTypes";
import { foodGroupLabel } from "./ingredientLabels";
import {
  rlCardHtml,
  rlSectionHtml,
  computeCardGroups,
  type RecipeWithGroups,
} from "./recipeCard";
import { createOffscreenStage, exportNodePng } from "./domSvgExport";
import { openModal, closeModal } from "./modals";
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
    <button type="button" class="m-btn" id="rl-export" title="把当前筛选出的菜谱合成一张 PNG 长图（重置筛选即导出全部）">🖼 导出图片</button>
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

wireNav();

let recipes: RecipeWithGroups[] = [];
let ingredients: IngredientEntry[] = [];
const ingredientById = new Map<string, IngredientEntry>();

let query = "";
let typeFilter = "all";
let groupFilter = "all";
let scoreFilter: "all" | "other" | number = "all";
let showIntermediate = false;
let view: "recipes" | "ingredients" = "recipes";
let showReskinReps = true;

/** 标准分数档位（其他 = 不在这些值内）。 */
const STANDARD_SCORES = [20, 40, 60, 80, 100, 120];
function scoreMatches(s: number | undefined, filter: "all" | "other" | number): boolean {
  if (filter === "all") return true;
  const v = s ?? 0;
  if (filter === "other") return !STANDARD_SCORES.includes(v);
  return v === filter;
}

/** 多 DLC 换皮去重：规范化中文名（去 DLC 后缀/空白）作聚簇键，代表 = 最高 DLC（保留后缀）。 */
function reskinDedupKey(name: string): string {
  return String(name ?? "").replace(/·?DLC\d+/gi, "").replace(/[（）()· ]/g, "");
}
/** 完整聚簇键 = 归一中文名 + 来源 + 分值 + 烹饪步骤。
 *  只靠中文名会把「同名但不是换皮」的菜谱误折叠（实测两例）：
 *   - 「生菜汉堡」：commonW2 LettuceBurger（无肉，40 分）vs 官方 Burger_Lettuce_SO
 *     （含煎肉排，60 分）——完全不同的两道菜；
 *   - 「煎蘑菇」：commonW2 FriedMushroom（DeepFatFryer）vs PanfriedMushroom（FryingPan）
 *     ——不同工序的两个中间产物。
 *  真正的跨 DLC 换皮（热狗 dlc08/dlc11 等）来源、分值、步骤都相同，仍会正确折叠。 */
function reskinClusterKey(it: {
  nameZh?: string;
  isCustom?: boolean;
  score?: number;
  cookingStep?: string;
}): string {
  return [
    reskinDedupKey(it.nameZh ?? ""),
    it.isCustom ? "custom" : "official",
    it.score ?? 0,
    it.cookingStep ?? "",
  ].join("|");
}
/** id 里的 DLC 序号。大小写不敏感：common03 正式版菜谱是 `DLC08_chickenburger`
 *  这种大写前缀，此前用区分大小写的正则会误判为 0。 */
function dlcNumber(id: string): number {
  const m = /^dlc(\d+)_/i.exec(id ?? "");
  return m ? parseInt(m[1], 10) : 0;
}
/** 同簇代表优先级：DLC 序号高者胜；同序号时「🍔 Burger大全」（commonW2，面皮统一
 *  为 DLC8、外观最完整）优先；再平票按 id 字典序取定值，保证任何调用顺序下结果
 *  一致（此前依赖数组顺序，刷新一次可能换一个代表）。 */
function reskinBetter(a: { id: string; group?: string }, b: { id: string; group?: string }): boolean {
  const da = dlcNumber(a.id);
  const db = dlcNumber(b.id);
  if (da !== db) return da > db;
  const ba = a.group === "burger" ? 1 : 0;
  const bb = b.group === "burger" ? 1 : 0;
  if (ba !== bb) return ba > bb;
  return (a.id ?? "") < (b.id ?? "");
}
function dedupReskins<
  T extends {
    id: string;
    group?: string;
    nameZh?: string;
    isCustom?: boolean;
    score?: number;
    cookingStep?: string;
  },
>(items: T[]): T[] {
  const reps = new Map<string, T>();
  for (const it of items) {
    const key = reskinClusterKey(it);
    const cur = reps.get(key);
    if (!cur || reskinBetter(it, cur)) reps.set(key, it);
  }
  return [...reps.values()];
}

/** 成品图标：自定义菜谱（Burger大全等）经桥接从 CustomRecipeSO.icon 读取，静态目录没有。 */
function recipeIconUrlOf(x: { isCustom?: boolean; assetPath?: string; id: string }): string {
  return x.isCustom && x.assetPath
    ? `/api/custom-recipes/icon?assetPath=${encodeURIComponent(x.assetPath)}`
    : `/icons/recipes/${encodeURIComponent(x.id)}.png`;
}

function card(r: RecipeWithGroups): string {
  return rlCardHtml(r, {
    allRecipes: recipes,
    ingredientName: (id) => ingredientById.get(id)?.nameZh ?? id,
    extraBadge: r.group === "levelset" ? "本关" : r.group === "burger" ? "🍔" : r.group === "commonw2" ? "📚" : undefined,
    iconSrc: recipeIconUrlOf,
  });
}

function visible(): RecipeWithGroups[] {
  const q = query.trim().toLowerCase();
  let out = recipes.filter((r) => {
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
  // 多 DLC 换皮仅保留代表（同菜名只显示最高 DLC）
  if (showReskinReps) out = dedupReskins(out);
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
    if (q && !`${i.nameZh} ${i.nameEn ?? ""} ${i.id}`.toLowerCase().includes(q)) return false;
    if (groupFilter !== "all" && (i.group ?? "core") !== groupFilter) return false;
    return true;
  });
  const deduped = showReskinReps ? dedupReskins(list) : list;
  if (deduped.length === 0) return '<div class="rl-empty">没有匹配的食材，试试调整搜索或筛选条件</div>';
  const groups = new Map<string, IngredientEntry[]>();
  for (const i of deduped) {
    const g = i.group ?? "core";
    if (!groups.has(g)) groups.set(g, []);
    groups.get(g)!.push(i);
  }
  const order = [...groups.keys()].sort((a, b) => {
    const rank = (g: string) => (g === "core" ? 0 : g === "levelset" ? 1 : 2);
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
    .map(([type, arr]) =>
      type === "burger"
        ? rlSectionHtml(type, burgerSubSectionsHtml(arr), arr.length)
        : rlSectionHtml(type, arr.map(card).join(""), arr.length)
    )
    .join("");
}

/** 汉堡组内按 Burger大全二级分类再分一层（经典/豪华/巨无霸/早餐/海鲜/素食/夹心/组装定义）。
 *  非 commonW2 的官方汉堡没有 subtype，统一落到「其他汉堡」小节并排在最后。 */
function burgerSubSectionsHtml(arr: RecipeWithGroups[]): string {
  const bySub = new Map<string, RecipeWithGroups[]>();
  for (const r of arr) {
    const s = r.subtype ?? "";
    if (!bySub.has(s)) bySub.set(s, []);
    bySub.get(s)!.push(r);
  }
  // 只有一个分组（且是未分类）时不加二级标题，保持旧观感
  if (bySub.size <= 1 && bySub.has("")) return arr.map(card).join("");
  return [...bySub.entries()]
    .sort((a, b) => burgerSubtypeOrder(a[0]) - burgerSubtypeOrder(b[0]))
    .map(
      ([sub, list]) =>
        `<div class="rl-subsection">
          <h3 class="rl-subsection-title">${esc(sub ? burgerSubtypeLabel(sub) : "其他汉堡")}<span class="rl-section-count">${list.length}</span></h3>
          <div class="rl-grid">${list.map(card).join("")}</div>
        </div>`
    )
    .join("");
}

function buildFilters(): void {
  // 计数跟随换皮去重：隐藏 DLC 重复变体时数量与展示一致
  const orderables = showReskinReps
    ? dedupReskins(recipes.filter((r) => !r.intermediate))
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
    showReskinReps = (e.target as HTMLInputElement).checked;
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
  document.getElementById("rl-export")!.addEventListener("click", () => void exportAll());
  // 点击菜谱卡片 → 详情弹窗（食材清单视图的 .rl-ing-card 不响应）
  document.getElementById("rl-content")!.addEventListener("click", (e) => {
    const cardEl = (e.target as HTMLElement).closest<HTMLElement>(".rl-card");
    if (!cardEl) return;
    const id = cardEl.dataset.id;
    if (!id) return;
    const r = recipes.find((x) => x.id === id);
    if (r) openRecipeDetail(r);
  });
}

/** 一键导出：把当前筛选出的全部菜谱（含分组标题/卡片/徽标/烹饪组）合成为一张 PNG 长图。
 *  走 DOM 快照导出（domSvgExport.ts）：**直接拍页面上已经渲染好的卡片**，样式与
 *  页面 100% 一致。页面本身没有标题页头（标题在顶栏 manage-bar 里），所以克隆
 *  #rl-content 到离屏舞台并补一个 .sum-head 页头，再整体快照。 */
async function exportAll(): Promise<void> {
  const btn = document.getElementById("rl-export") as HTMLButtonElement | null;
  const content = document.getElementById("rl-content");
  if (!content) {
    setStatus("页面内容尚未就绪", false);
    return;
  }
  const isIngredients = view === "ingredients";
  const count = isIngredients
    ? content.querySelectorAll(".rl-ing-card").length
    : visible().length;
  if (count === 0) {
    setStatus(isIngredients ? "没有可导出的食材" : "没有可导出的菜谱", false);
    return;
  }
  if (btn) btn.disabled = true;
  setStatus("正在生成图片…");
  let stage: HTMLDivElement | null = null;
  try {
    const date = new Date().toISOString().slice(0, 10);
    const title = isIngredients ? "食材清单" : "菜谱清单列表";
    const unit = isIngredients ? "个食材" : "个菜谱";
    const width = content.getBoundingClientRect().width || 1200;
    // +48 = .sum-page 的左右内边距，保证舞台里的内容宽度与页面一致（不改变换行）
    stage = createOffscreenStage(width + 48, "sum-page");
    stage.innerHTML = `
      <header class="sum-head">
        <h1 class="sum-title">${esc(title)}</h1>
        <div class="sum-sub">共 ${count} ${esc(unit)} · 导出于 ${esc(date)}</div>
      </header>
      <div data-export-host></div>
    `;
    const host = stage.querySelector("[data-export-host]")!;
    for (const child of Array.from(content.children)) host.appendChild(child.cloneNode(true));
    await waitForImages(stage);
    await exportNodePng(stage, `${title}_${count}个_${date}.png`);
    setStatus(`已导出 PNG（${count} ${unit}）`);
  } catch (e) {
    setStatus(e instanceof Error ? e.message : String(e), false);
  } finally {
    if (stage) stage.remove();
    if (btn) btn.disabled = false;
  }
}

/** 等离屏克隆里的图片解码完成（克隆节点的 <img> 需要重新触发加载，
 *  未完成时 currentSrc 可能为空，导出会漏图）。
 *
 *  ⚠ 离屏舞台定位在 left:-100000px（视口外），克隆出的 `loading="lazy"` 图片
 *  浏览器永远不会自动触发加载，load/error 都不会 fire——若不干预，Promise.all
 *  会永久挂起（症状：导出卡在“正在生成图片…”，无任何 console 输出）。
 *  这里：① 把每张图的 loading 强制改为 "eager" 逼其立即加载；② 每张图加 5s
 *  超时兜底，杜绝个别图卡死拖垮整体。 */
async function waitForImages(root: HTMLElement): Promise<void> {
  const imgs = Array.from(root.querySelectorAll("img"));
  await Promise.all(
    imgs.map(
      (img) =>
        new Promise<void>((resolve) => {
          if (img.complete && img.naturalWidth > 0) return resolve();
          let done = false;
          const finish = (): void => {
            if (done) return;
            done = true;
            resolve();
          };
          img.addEventListener("load", finish, { once: true });
          img.addEventListener("error", finish, { once: true });
          // 逼视口外的 lazy 图立即加载：改 loading 属性并重挂 src
          if (img.loading === "lazy") {
            img.loading = "eager";
            const src = img.currentSrc || img.src;
            if (src) img.src = src;
          }
          if (img.complete && img.naturalWidth > 0) return finish();
          window.setTimeout(finish, 5000);
        })
    )
  );
}

/** 烹饪步骤 id → 中文显示名（详情弹窗用）。 */
const STEP_ZH: Record<string, string> = {
  Pot: "煮锅",
  FryingPan: "煎锅",
  DeepFatFryer: "炸锅",
  OvenTray: "烤箱",
  Steamer: "蒸锅",
  Mixer: "搅拌器",
  Blender: "榨汁机",
  MixingBowl: "搅拌碗",
  GriddlePan: "煎烤盘",
  KebabSkewer: "烤串",
  ToastingFork: "烤叉",
  HotPot: "大火锅",
  RoastingTray: "烤托盘",
  OvenCakeTin: "蛋糕模",
  Plate: "盘子",
  Glass: "玻璃杯",
  Mug: "马克杯",
};
function stepZh(step: string | undefined): string {
  if (!step) return "—";
  return STEP_ZH[step] ? `${STEP_ZH[step]}（${step}）` : step;
}

/** 菜谱详情弹窗：完整参数 + 卡片预览 + 下载卡片图片按钮。 */
function openRecipeDetail(r: RecipeWithGroups): void {
  const groups = computeCardGroups(r, {
    allRecipes: recipes,
    ingredientName: (id) => ingredientById.get(id)?.nameZh ?? id,
  });
  const ingNames = (r.ingredients ?? []).map(
    (id) => ingredientById.get(id)?.nameZh ?? id
  );
  const compNames = (r.compositionIds ?? []).map((id) => {
    const rec = recipes.find((x) => x.id === id);
    return rec?.nameZh ?? ingredientById.get(id)?.nameZh ?? id;
  });
  const groupsText = groups
    .map((g) => {
      const ings = g.ingredients
        .map((i) => esc(ingredientById.get(i)?.nameZh ?? i))
        .join("、");
      const steps = [g.step, ...(g.extraSteps ?? []).map((e) => e.step)]
        .filter(Boolean)
        .map((s) => esc(stepZh(s as string)))
        .join(" → ");
      return `${ings || "—"}${steps ? `  ⟶ ${steps}` : ""}`;
    })
    .join("<br>");

  const rows: [string, string, boolean?][] = [
    ["中文名", r.nameZh],
    ["英文名 / ID", `${r.nameEn || r.id}  ·  ${r.id}`],
    ["类型", recipeTypeLabel(r.type)],
    ...(r.subtype ? ([["汉堡子类", burgerSubtypeLabel(r.subtype)]] as [string, string][]) : []),
    ["来源", foodGroupLabel(r.group ?? "core")],
    ["分数", r.intermediate ? "半成品（不计分）" : `⭐ ${r.score ?? 0}`],
    ["烹饪步骤", stepZh(r.cookingStep)],
    ["装盘容器", stepZh(r.platingStep)],
    ["食材", ingNames.length ? ingNames.join("、") : "—"],
    ...(compNames.length ? ([["组成", compNames.join("、")]] as [string, string][]) : []),
    ["工序分组", groupsText || "—", true],
    ["自定义菜谱", r.isCustom ? "是" : "否"],
    ["搅拌类", r.mixing ? "是（先搅拌再烹饪）" : "否"],
    ["GUID", r.guid || "—"],
    ["资源路径", r.assetPath || "—"],
  ];
  const rowsHtml = rows
    .map(
      ([k, v, raw]) =>
        `<tr><th>${esc(k)}</th><td>${raw ? v : esc(v)}</td></tr>`
    )
    .join("");

  const preview = rlCardHtml(r, {
    allRecipes: recipes,
    ingredientName: (id) => ingredientById.get(id)?.nameZh ?? id,
    extraBadge: r.group === "levelset" ? "本关" : r.group === "burger" ? "🍔" : r.group === "commonw2" ? "📚" : undefined,
    iconSrc: recipeIconUrlOf,
  });

  const body = `
    <div class="rl-detail">
      <div class="rl-detail-preview rl-grid">${preview}</div>
      <table class="rl-detail-table">${rowsHtml}</table>
    </div>`;
  const footer = `
    <button type="button" class="modal-btn" data-cancel>关闭</button>
    <button type="button" class="modal-btn primary" data-download>🖼 下载菜谱图片</button>`;

  const root = openModal(`📖 ${esc(r.nameZh)}`, body, footer);
  root.querySelector<HTMLButtonElement>("[data-cancel]")?.addEventListener("click", () => closeModal());
  const dl = root.querySelector<HTMLButtonElement>("[data-download]");
  dl?.addEventListener("click", () => void downloadRecipeCard(r, dl));
}

/** 把单张菜谱卡片渲染成 PNG 下载（离屏克隆 + 等图加载，与整页导出同引擎）。 */
async function downloadRecipeCard(r: RecipeWithGroups, btn: HTMLButtonElement): Promise<void> {
  const original = btn.textContent;
  btn.disabled = true;
  btn.textContent = "正在生成…";
  let stage: HTMLDivElement | null = null;
  try {
    const cardHtml = rlCardHtml(r, {
      allRecipes: recipes,
      ingredientName: (id) => ingredientById.get(id)?.nameZh ?? id,
      extraBadge: r.group === "levelset" ? "本关" : r.group === "burger" ? "🍔" : r.group === "commonw2" ? "📚" : undefined,
      iconSrc: recipeIconUrlOf,
    });
    // 卡片宽度取页面 .rl-card 实际宽度（回退 300），+48 = .sum-page 内边距
    const sample = document.querySelector<HTMLElement>(".rl-card");
    const cardW = sample ? Math.ceil(sample.getBoundingClientRect().width) : 300;
    stage = createOffscreenStage(cardW + 48, "sum-page");
    stage.innerHTML = `<div class="rl-grid">${cardHtml}</div>`;
    await waitForImages(stage);
    const date = new Date().toISOString().slice(0, 10);
    await exportNodePng(stage, `${r.nameZh || r.id}_${date}.png`);
  } catch (e) {
    setStatus(e instanceof Error ? e.message : String(e), false);
  } finally {
    if (stage) stage.remove();
    btn.disabled = false;
    btn.textContent = original;
  }
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
  const orderableVisible = dedupReskins(recipes.filter((r) => !r.intermediate)).length;
  setStatus(
    `共 ${recipes.length} 个菜谱（成品 ${orderable} · Web去重后 ${orderableVisible}）${bridgeUp ? "" : " · 静态数据"}`
  );
  buildFilters();
  render();
  wire();
}

void init();
