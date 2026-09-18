import * as api from "./api";
import type {
  BunUsage,
  BunUsageReport,
  CustomRecipeCategory,
  CustomRecipeConfig,
  CustomRecipeEdit,
  CustomRecipeReferences,
  CustomRecipeSummary,
  IngredientEntry,
  LevelSetInfo,
  RecipeEntry,
} from "./types";
import { showBusy, hideBusy, withBusy } from "./busy";
import { navHtml, wireNav } from "./nav";
import { burgerPath, fillingPath, parseRoute, recipeFormPath, recipeListPath } from "./route";
import { BUN_CORE_ID, BUN_DLC2_ID, BUN_DLC8_ID } from "./recipeGroups";
import { foodGroupLabel, visibleIngredients, visibleRecipes } from "./ingredientLabels";
import { recipeTypeLabel, RECIPE_TYPE_ORDER } from "./recipeTypes";
import { closeModal, openModal } from "./modals";
import { rlCardHtml, type RecipeWithGroups } from "./recipeCard";
import { normalizeCustomRecipeCard } from "./recipeCardCustom";
import { fmt4, fmtCm, footprintOf, u2cm } from "./modelUnits";
import { sanitizeUploadFileName } from "./fbxTextureRename";
import { ensureObjMtllib, renameMtlTextureRefs } from "./mtlTextureRename";
import { openRecipeModelPreview } from "./recipeModelPreview";

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

const IDENT_RE = /^[A-Za-z_][A-Za-z0-9_]*$/;

/** 列表页记住的当前分类过滤（进入新建/编辑菜谱后返回时保持）。 */
let lastActiveCategoryId = "";

function setStatus(msg: string, ok = true): void {
  const el = document.getElementById("cr-status");
  if (!el) return;
  el.textContent = msg;
  el.classList.toggle("err", !ok);
  el.classList.toggle("ok", ok && msg.length > 0);
}

function shell(app: HTMLElement, title: string): HTMLElement {
  document.body.classList.add("manage-bg");
  app.innerHTML = `
    ${navHtml("custom-recipes")}
    <div class="manage-bar">
      <h1 class="m-title">${esc(title)}</h1>
      <span class="status" id="cr-status"></span>
      <span style="flex:1"></span>
    </div>
    <div class="manage-content" id="cr-content"></div>
  `;
  wireNav();
  return document.getElementById("cr-content")!;
}

/** 把 URL 同步到当前视图（pushState；深链初载时路径已一致则不动）。 */
function syncCrPath(path: string): void {
  if (location.pathname !== path) history.pushState(null, "", path);
}

let crPopstateWired = false;

/** /custom-recipes 路由分发：选集（无 id）/ 列表（{set}）/ 编辑表单（{set}/recipe/{id|new}）。 */
async function renderCustomRecipesRoute(app: HTMLElement): Promise<void> {
  const r = parseRoute();
  if (r.page === "custom-recipes" && r.setId && r.recipeId) {
    await openRecipeFormById(app, r.setId, r.recipeId);
    return;
  }
  if (r.page === "custom-recipes" && r.setId) {
    await renderRecipeList(app, r.setId);
    return;
  }
  await renderSetChooser(app);
}

/** 深链 /custom-recipes/{set}/recipe/{id}：按菜谱 id 解析 assetPath 后打开编辑表单。 */
async function openRecipeFormById(app: HTMLElement, setName: string, recipeId: string): Promise<void> {
  if (recipeId === "new") {
    await renderRecipeForm(app, setName, null);
    return;
  }
  let recipes: CustomRecipeSummary[] = [];
  try {
    recipes = await api.fetchCustomRecipes(setName);
  } catch (e) {
    showError(e);
    return;
  }
  // 本集菜谱优先（commonW2 共享库同 id 条目只读，不作为深链编辑目标）
  const hit =
    recipes.find((x) => x.id === recipeId && !isCommonW2Recipe(x)) ??
    recipes.find((x) => x.id === recipeId) ??
    recipes.find((x) => x.assetPath.replace(/\\/g, "/").endsWith("/" + recipeId + ".asset"));
  if (!hit) {
    await renderRecipeList(app, setName);
    setStatus(`未找到菜谱「${recipeId}」，已返回列表。`, false);
    return;
  }
  await renderRecipeForm(app, setName, hit.assetPath);
}

export async function renderCustomRecipesView(app: HTMLElement): Promise<void> {
  if (!crPopstateWired) {
    crPopstateWired = true;
    window.addEventListener("popstate", () => void renderCustomRecipesRoute(app));
  }
  await renderCustomRecipesRoute(app);
}

async function renderSetChooser(app: HTMLElement): Promise<void> {
  syncCrPath("/custom-recipes");
  const content = shell(app, "自定义菜谱管理");
  setBusy("加载关卡集…");

  let sets: LevelSetInfo[] = [];
  try {
    sets = await api.fetchSets();
  } catch (e) {
    showError(e);
    return;
  }
  setStatus(`共 ${sets.length} 个关卡集`);

  content.innerHTML = `
    <div class="m-section-title">选择关卡集</div>
    <p class="modal-hint">选择要管理自定义菜谱的关卡集。首次进入会自动初始化配置。</p>
    <div class="m-grid">${sets
      .map(
        (s) => `
      <div class="m-card">
        <h3>${esc(s.levelSetNameZH || s.setName)} <span class="muted">(${esc(s.levelSetName || s.setName)})</span></h3>
        <div class="m-meta">
          标识：${esc(s.setName)} · 关卡数：${s.levelCount}<br>
          作者：${esc(s.author || "—")} · 版本：${esc(s.version || "—")}
        </div>
        <div class="m-actions">
          <button class="m-btn primary" data-open="${esc(s.setName)}">管理菜谱</button>
        </div>
      </div>`
      )
      .join("") || '<p class="muted">暂无关卡集</p>'}
    </div>
  `;

  content.querySelectorAll<HTMLButtonElement>("[data-open]").forEach((b) =>
    b.addEventListener("click", () => void renderRecipeList(app, b.dataset.open!))
  );
}

function setBusy(msg: string): void {
  const el = document.getElementById("cr-content");
  if (el) el.innerHTML = `<p class="muted">${esc(msg)}</p>`;
  setStatus(msg);
}

function showError(e: unknown): void {
  const msg = e instanceof Error ? e.message : String(e);
  setStatus(msg, false);
  const el = document.getElementById("cr-content");
  if (el) el.innerHTML = `<div class="m-block"><h3>出错</h3><p>${esc(msg)}</p></div>`;
}

/** 关卡集自定义菜谱的成品图标（经专用端点从 CustomRecipeSO.icon 资产读取，
 *  不依赖文件名约定，也不经过 data-file 的路径解析）。 */
function crIconSrc(r: { assetPath: string }): string {
  return `/api/custom-recipes/icon?assetPath=${encodeURIComponent(r.assetPath)}`;
}

/** commonW2 Burger大全 共享库：不属于本关卡集自定义菜谱，列表页不展示。 */
function isCommonW2Recipe(r: CustomRecipeSummary): boolean {
  return r.assetPath.replace(/\\/g, "/").includes("/commonW2/");
}

function foodIconImg(kind: "ingredients" | "recipes", id: string | undefined): string {
  const src = id ? `/icons/${kind}/${encodeURIComponent(id)}.png` : "/icons/_placeholder.png";
  return `<img class="food-icon" loading="lazy" src="${src}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`;
}

// ==================== Recipe List ====================

interface RecipeLikeCard extends RecipeWithGroups {}

function toRecipeCard(r: CustomRecipeSummary): RecipeLikeCard {
  return normalizeCustomRecipeCard(r) as RecipeLikeCard;
}

async function renderRecipeList(app: HTMLElement, setName: string): Promise<void> {
  syncCrPath(recipeListPath(setName));
  const content = shell(app, `自定义菜谱 · ${esc(setName)}`);
  setBusy("加载菜谱配置…");

  let config: CustomRecipeConfig;
  let recipes: CustomRecipeSummary[];
  let catalog: RecipeEntry[] = [];
  let ingredientNames = new Map<string, string>();
  let platingNames = new Map<string, string>();
  // 组成推导上下文：关卡集菜谱 + 目录全部菜谱（与「组装效果（实时预览）」一致）
  let recipeLikes: RecipeLikeCard[] = [];
  try {
    [config, recipes] = await Promise.all([
      api.fetchCustomRecipeConfig(setName),
      api.fetchCustomRecipes(setName),
    ]);
    const refs = await api.fetchCustomRecipeReferences(setName).catch(() => null);
    for (const c of refs?.platingContainers ?? []) platingNames.set(c.id, c.nameZh || c.id);
    let ingLoadFailed = false;
    const [ings, catalogFetched] = await Promise.all([
      api.fetchIngredients().catch(() => {
        ingLoadFailed = true;
        return [] as IngredientEntry[];
      }),
      api.fetchRecipeCatalog(setName).catch(() => [] as RecipeEntry[]),
    ]);
    catalog = catalogFetched;
    for (const i of ings) ingredientNames.set(i.id, i.nameZh);
    if (ingLoadFailed) setStatus("⚠️ 食材数据加载失败（/api/catalog/ingredients），卡片可能缺少食材图标");
  } catch (e) {
    showError(e);
    return;
  }
  /** 列表展示用：仅本关卡集 custom_recipes（不含 commonW2 共享库）。 */
  function getDisplayRecipes(): CustomRecipeSummary[] {
    return recipes.filter((r) => !isCommonW2Recipe(r));
  }

  setStatus(`${getDisplayRecipes().length} 个菜谱 · UID前缀：${config.uidPrefix}`);

  const categories = config.categories ?? [];
  /** 分类 chip：排除 Burger大全 保留分类（共享库，非本集菜谱）。 */
  const listCategories = categories.filter((c) => c.id !== "burger");

  function catDisplay(c: CustomRecipeCategory): string {
    return c.zh || c.id;
  }

  // 从模块级记忆恢复分类过滤（进入新建/编辑后返回时保持）；分类已被删除则回退「全部」
  let activeCategoryId = listCategories.some((c) => c.id === lastActiveCategoryId) ? lastActiveCategoryId : "";
  /** 二级分类过滤（分类目录下的子目录，如 burger_filling/ 下再分层）；""=全部。 */
  let activeSubcategoryId = "";
  const subcategories = config.subcategories ?? [];
  /** 子分类显示名：桥接下发的元数据优先，缺则回退目录名。 */
  function subDisplay(parent: string, id: string): string {
    const m = subcategories.find((s) => s.parent === parent && s.id === id);
    return m?.zh || id;
  }
  function subOrder(parent: string, id: string): number {
    const m = subcategories.find((s) => s.parent === parent && s.id === id);
    return m?.order ?? 999;
  }
  let searchQuery = "";
  /** 分数过滤：全部 / 其他（不在 0/20/…/120 档位）/ 指定分数。 */
  let scoreFilter: "all" | "other" | number = "all";
  /** 状态多选（叠加=交集）：中间产物（score<=0）/ 搅拌物（type==="Mixed"）/ 成品（score>0）；都不勾 = 不限。 */
  let filterIntermediate = false;
  let filterMixed = false;
  let filterFinished = false;

  /** 旧桥接数据无 ingredients/cookingGroups 时，用 compositionIds 反查食材名兜底。 */
  function cardRecipe(r: CustomRecipeSummary): RecipeLikeCard {
    const card = toRecipeCard(r);
    if (!card.ingredients && card.compositionIds) {
      card.ingredients = card.compositionIds.filter((id) => ingredientNames.has(id));
    }
    return card;
  }

  // 组成推导上下文（与「组装效果（实时预览）」一致）：含官方成品菜与本关卡集全部自定义菜谱。
  recipeLikes = buildCompositionContext(recipes, catalog).allRecipeLikes as RecipeLikeCard[];

  function filteredRecipes(): CustomRecipeSummary[] {
    const base = getDisplayRecipes();
    let list = activeCategoryId ? base.filter((r) => r.category === activeCategoryId) : base;
    if (activeCategoryId && activeSubcategoryId)
      list = list.filter((r) => (r.subcategory ?? "") === activeSubcategoryId);
    if (filterIntermediate) list = list.filter((r) => r.score <= 0);
    if (filterMixed) list = list.filter((r) => r.type === "Mixed");
    if (filterFinished) list = list.filter((r) => r.score > 0);
    if (scoreFilter !== "all") {
      const tiers = [0, 20, 40, 60, 80, 100, 120];
      list = list.filter((r) =>
        scoreFilter === "other" ? !tiers.includes(r.score ?? 0) : (r.score ?? 0) === scoreFilter
      );
    }
    const q = searchQuery.trim().toLowerCase();
    if (q) {
      list = list.filter((r) => {
        const hay = [r.nameZh, r.nameEn ?? "", r.id, ...(r.ingredients ?? [])]
          .join(" ")
          .toLowerCase();
        return hay.includes(q);
      });
    }
    return list;
  }

  function renderGrid(): string {
    const filtered = filteredRecipes();

    if (filtered.length === 0) {      if (getDisplayRecipes().length === 0 && searchQuery === "" && activeCategoryId === "") {
        // 空列表时自动诊断：区分"目录确实没菜谱"与"桥接旧版/资产加载失败"
        void (async () => {
          const el = document.getElementById("cr-grid");
          if (!el || el.dataset.diag) return;
          el.dataset.diag = "1";
          try {
            const diag = await api.fetchCustomRecipeScanDiag(setName).catch(() => null);
            if (!diag) {
              el.innerHTML = `<div class="m-block">
                <h3>暂无菜谱</h3>
                <p class="muted">桥接不支持诊断接口（旧版本）。若磁盘 <code>Assets/LevelSets/${esc(setName)}/custom_recipes/</code> 下已有菜谱，请：
                <b>在 Unity 中 Tools → Layout Editor → 停止服务 → 启动服务</b>（或重启 Unity 重新编译），再刷新本页。</p>
              </div>`;
              return;
            }
            const hint = diag.dirExists
              ? `磁盘上检测到 <b>${diag.fsAssets.length}</b> 个 .asset 文件，扫描命中 <b>${diag.scannedCount}</b> 个自定义菜谱，成功加载 <b>${diag.loadedCount}</b> 个。`
              : "目录不存在，尚未创建任何菜谱。";
            const action = diag.scannedCount > 0 && diag.loadedCount === 0
              ? "<p class=\"mp-status err\">⚠️ 文件存在但资产加载失败（CustomRecipeSO 类型未加载）。请重启 Unity 并确认 Console 无编译错误。</p>"
              : diag.scannedCount === 0 && diag.fsAssets.length > 0
                ? "<p class=\"mp-status err\">⚠️ 文件系统有资产但脚本 guid 不匹配（可能是旧版脚本/副本）。</p>"
                : "";
            el.innerHTML = `<div class="m-block">
              <h3>暂无菜谱</h3>
              <p class="muted">${hint}</p>
              ${action}
              <p class="muted small" style="margin-top:8px">点击右上角「+ 新建菜谱」开始创建；若已创建但未显示，请重启 Unity 桥（Tools → Layout Editor → 停止服务 → 启动服务）后刷新。</p>
            </div>`;
          } catch {
            el.innerHTML = `<div class="m-block"><h3>暂无菜谱</h3><p class="muted">点击右上角「+ 新建菜谱」开始创建。</p></div>`;
          }
        })();
        return '<p class="muted">加载中…</p>';
      }
      return '<p class="muted">没有匹配的菜谱。点击右上角「+ 新建菜谱」开始创建。</p>';
    }

    const cardWrapHtml = (r: CustomRecipeSummary): string => {
      let cardHtml: string;
      try {
        cardHtml = rlCardHtml(cardRecipe(r), {
          allRecipes: recipeLikes,
          ingredientName: (id) => ingredientNames.get(id) ?? id,
          iconSrc: () => crIconSrc(r),
        });
      } catch (e) {
        cardHtml = `<div class="m-card"><h3>${esc(r.nameZh)}</h3><p class="muted">卡片渲染失败：${esc((e as Error).message)}</p></div>`;
      }
      const cat = categories.find((c) => c.id === r.category);
      const plating = r.platingStepId ? platingNames.get(r.platingStepId) : "";
      const compCount = (r.compositionIds ?? []).length;
      // 组装定义（OptionalBurger/Pizza）与成品汉堡均在汉堡工作台编辑
      const isAssembly = r.optionalKind === "burger" || r.optionalKind === "pizza";
      const isFinishedBurger = Boolean(r.isFinishedBurger);
      const gotoBurgerWorkbench = isAssembly || isFinishedBurger;
      const burgerProductAttr = isFinishedBurger ? ` data-burger-product="${esc(r.id)}"` : "";
      return `
      <div class="cr-card-wrap">
        <div class="cr-card-inner">${cardHtml}</div>
        <div class="cr-card-foot">
          <span class="cr-cat-tag">${esc(catDisplay(cat ?? { id: r.category, zh: r.category, en: r.category }))}</span>
          ${plating ? `<span class="cr-cat-tag cr-plate-tag" title="装盘容器">🍽 ${esc(plating)}</span>` : ""}
          <span class="muted small">${isAssembly ? "组装定义" : isFinishedBurger ? "成品汉堡" : `UID ${r.uID}`} · 组成 ${compCount} 项</span>
          <span style="flex:1"></span>
          ${r.previewable ?? r.hasModel ? `<button class="m-btn small" data-preview="${esc(r.assetPath)}" title="3D 模型在线预览">👁</button>` : ""}
          ${gotoBurgerWorkbench
            ? `<button class="m-btn small" data-goto-burger${burgerProductAttr} title="${isFinishedBurger ? "在汉堡工作台载入并编辑此成品汉堡" : "组装定义的可选夹心与堆叠模型在汉堡工作台中管理"}">🍔 工作台</button>`
            : `<button class="m-btn small" data-edit="${esc(r.assetPath)}">编辑</button>`}
          <button class="m-btn small danger" data-del="${esc(r.assetPath)}">删除</button>
        </div>
      </div>`;
    };

    return `<div class="rl-grid">${filtered.map(cardWrapHtml).join("")}</div>`;
  }

  /** 分类 chips（过滤栏第二行）：全部 / 各分类（含计数）+ 新建/管理分类。 */
  function renderCatChips(): string {
    const display = getDisplayRecipes();
    return `
      <button type="button" class="rl-chip-btn cr-cat-chip${activeCategoryId === "" ? " active" : ""}" data-cat="">全部 <span class="rl-cnt">${display.length}</span></button>
      ${listCategories
        .map((c) => {
          const count = display.filter((r) => r.category === c.id).length;
          return `<button type="button" class="rl-chip-btn cr-cat-chip${activeCategoryId === c.id ? " active" : ""}" data-cat="${esc(c.id)}">${esc(catDisplay(c))} <span class="rl-cnt">${count}</span></button>`;
        })
        .join("")}
      <span class="cr-cat-tools">
        <button class="m-btn" id="cr-new-cat">+ 新建分类</button>
        ${listCategories.length > 0 ? '<button class="m-btn" id="cr-manage-cat">管理分类</button>' : ""}
      </span>`;
  }

  /** 二级分类 chips（选中某个分类时才出现）：分类目录下的子目录。
   *  子目录归属由后端按资产路径派生（custom_recipes/<分类>/<子分类>/xxx.asset），
   *  显示名走桥接下发的 subcategories 元数据，缺失则直接显示目录名。 */
  function renderSubChips(): string {
    if (!activeCategoryId) return "";
    const inCat = getDisplayRecipes().filter((r) => r.category === activeCategoryId);
    const subs = new Map<string, number>();
    for (const r of inCat) {
      const s = r.subcategory ?? "";
      if (!s) continue;
      subs.set(s, (subs.get(s) ?? 0) + 1);
    }
    if (subs.size === 0) return "";
    const rootCount = inCat.filter((r) => !(r.subcategory ?? "")).length;
    const ordered = [...subs.entries()].sort(
      (a, b) => subOrder(activeCategoryId, a[0]) - subOrder(activeCategoryId, b[0]) || a[0].localeCompare(b[0])
    );
    return `
      <button type="button" class="rl-chip-btn cr-sub-chip${activeSubcategoryId === "" ? " active" : ""}" data-sub="">全部 <span class="rl-cnt">${inCat.length}</span></button>
      ${ordered
        .map(
          ([id, n]) =>
            `<button type="button" class="rl-chip-btn cr-sub-chip${activeSubcategoryId === id ? " active" : ""}" data-sub="${esc(id)}">${esc(subDisplay(activeCategoryId, id))} <span class="rl-cnt">${n}</span></button>`
        )
        .join("")}
      ${rootCount > 0 ? `<span class="muted small">未分子类 ${rootCount}</span>` : ""}`;
  }

  content.innerHTML = `
    <div class="m-actions-row">
      <button class="m-btn" id="cr-back">← 返回关卡集列表</button>
      <span class="muted">当前关卡集：<b>${esc(setName)}</b></span>
      <span style="flex:1"></span>
      <button class="m-btn" id="cr-unify-bun" title="把本关卡集自定义菜谱里的汉堡面包皮统一换成同一种">🍞 统一面包皮</button>
      <button class="m-btn" id="cr-new-filling" title="夹心 = 0 分自定义菜谱，做好后可在汉堡工作台选用">🥩 夹心工作台</button>
      <button class="m-btn" id="cr-new-burger">🍔 新增汉堡菜谱</button>
      <button class="m-btn primary" id="cr-new-recipe">+ 新建菜谱</button>
    </div>
    <div class="cr-toolbar">
      <input type="search" id="cr-search" class="rl-search" placeholder="搜索菜名 / ID / 食材…" autocomplete="off">
      <select id="cr-score-filter" class="rl-select" title="按分数过滤">
        <option value="all">全部分数</option>
        <option value="0">0 分</option>
        <option value="20">20 分</option>
        <option value="40">40 分</option>
        <option value="60">60 分</option>
        <option value="80">80 分</option>
        <option value="100">100 分</option>
        <option value="120">120 分</option>
        <option value="other">其他分数</option>
      </select>
      <button type="button" class="rl-chip-btn cr-state-chip${filterIntermediate ? " active" : ""}" data-state="intermediate" title="只看 0 分半成品（可与其他状态叠加 = 交集）">🧩 中间产物</button>
      <button type="button" class="rl-chip-btn cr-state-chip${filterMixed ? " active" : ""}" data-state="mixed" title="只看 Mixed 搅拌类菜谱（可与其他状态叠加 = 交集）">🥣 搅拌物</button>
      <button type="button" class="rl-chip-btn cr-state-chip${filterFinished ? " active" : ""}" data-state="finished" title="只看可点单的成品菜（score>0，可与其他状态叠加 = 交集）">🍽 成品</button>
    </div>
    <div class="cr-toolbar cr-cat-bar" id="cr-cat-chips">${renderCatChips()}</div>
    <div class="cr-toolbar cr-sub-bar" id="cr-sub-chips">${renderSubChips()}</div>
    <div id="cr-grid">${renderGrid()}</div>
  `;

  function refreshView(): void {
    void (async () => {
      showBusy("加载…");
      try {
        [config, recipes] = await Promise.all([
          api.fetchCustomRecipeConfig(setName),
          api.fetchCustomRecipes(setName),
        ]);
      } catch (e) {
        showError(e);
        return;
      } finally {
        hideBusy();
      }
      setStatus(`${getDisplayRecipes().length} 个菜谱 · UID前缀：${config.uidPrefix}`);
      document.getElementById("cr-cat-chips")!.innerHTML = renderCatChips();
      wireCatChips();
      refreshSubChips();
      document.getElementById("cr-grid")!.innerHTML = renderGrid();
      wireGridButtons();
    })();
  }

  function wireToolbar(): void {
    document.getElementById("cr-search")?.addEventListener("input", (e) => {
      searchQuery = (e.target as HTMLInputElement).value;
      document.getElementById("cr-grid")!.innerHTML = renderGrid();
      wireGridButtons();
    });
    document.getElementById("cr-score-filter")?.addEventListener("change", (e) => {
      const v = (e.target as HTMLSelectElement).value;
      scoreFilter = v === "all" ? "all" : v === "other" ? "other" : Number(v);
      document.getElementById("cr-grid")!.innerHTML = renderGrid();
      wireGridButtons();
    });
    document.querySelectorAll<HTMLButtonElement>(".cr-state-chip").forEach((b) => {
      b.addEventListener("click", () => {
        if (b.dataset.state === "intermediate") filterIntermediate = !filterIntermediate;
        else if (b.dataset.state === "mixed") filterMixed = !filterMixed;
        else if (b.dataset.state === "finished") filterFinished = !filterFinished;
        b.classList.toggle("active");
        document.getElementById("cr-grid")!.innerHTML = renderGrid();
        wireGridButtons();
      });
    });
  }

  /** 二级 chips 重绘 + 重新绑定（分类切换或数据刷新后调用）。 */
  function refreshSubChips(): void {
    const el = document.getElementById("cr-sub-chips");
    if (!el) return;
    el.innerHTML = renderSubChips();
    el.style.display = el.innerHTML.trim() ? "" : "none";
    wireSubChips();
  }

  function wireSubChips(): void {
    document.querySelectorAll<HTMLButtonElement>(".cr-sub-chip").forEach((b) => {
      b.addEventListener("click", () => {
        activeSubcategoryId = b.dataset.sub ?? "";
        refreshSubChips();
        document.getElementById("cr-grid")!.innerHTML = renderGrid();
        wireGridButtons();
      });
    });
  }

  function wireCatChips(): void {
    document.querySelectorAll<HTMLButtonElement>(".cr-cat-chip").forEach((b) => {
      b.addEventListener("click", () => {
        activeCategoryId = b.dataset.cat ?? "";
        lastActiveCategoryId = activeCategoryId;
        // 切分类时二级过滤必须清空（旧子分类在新分类下不存在）
        activeSubcategoryId = "";
        document.getElementById("cr-cat-chips")!.innerHTML = renderCatChips();
        wireCatChips();
        refreshSubChips();
        document.getElementById("cr-grid")!.innerHTML = renderGrid();
        wireGridButtons();
      });
    });
    document.getElementById("cr-new-cat")?.addEventListener("click", () =>
      openNewCategoryModal(setName, (newId) => {
        lastActiveCategoryId = newId ?? "";
        // 完全重建列表页，确保新分类立即出现在过滤栏
        void renderRecipeList(app, setName);
      })
    );
    document.getElementById("cr-manage-cat")?.addEventListener("click", () =>
      openManageCategoriesModal(setName, config.categories, () => void renderRecipeList(app, setName))
    );
  }

  function wireGridButtons(): void {
    // 汉堡工作台：严格路由 /custom-recipes/burger-maker/{set}[/{burgerId}]（整页跳转重载）
    document.querySelectorAll<HTMLElement>("[data-goto-burger]").forEach((b) =>
      b.addEventListener("click", () => {
        const productId = b.dataset.burgerProduct;
        location.assign(burgerPath(setName, productId || undefined));
      })
    );
    document.querySelectorAll<HTMLButtonElement>("[data-edit]").forEach((b) =>
      b.addEventListener("click", () => {
        const path = b.dataset.edit!;
        const hit = recipes.find((x) => x.assetPath === path);
        const rid = hit?.id ?? (path.split("/").pop() ?? "").replace(/\.asset$/, "");
        syncCrPath(recipeFormPath(setName, rid || "new"));
        void renderRecipeForm(app, setName, path);
      })
    );
    document.querySelectorAll<HTMLButtonElement>("[data-preview]").forEach((b) =>
      b.addEventListener("click", () => {
        const path = b.dataset.preview!;
        // 回显已保存的变换 + 按 Unity 实际尺寸校准自动适配
        const r = recipes.find((x) => x.assetPath === path);
        const s = r && r.modelScale > 0 ? r.modelScale : 1;
        void openRecipeModelPreview(path, path.split("/").pop() ?? path, {
          fitTarget: r?.platingStepId === "Glass" ? "cup" : "plate",
          scale: r?.modelScale ?? 1,
          rotationX: r?.modelRotationX ?? 0,
          rotationY: r?.modelRotationY ?? 0,
          rotationZ: r?.modelRotationZ ?? 0,
          positionX: r?.modelPositionX ?? 0,
          positionY: r?.modelPositionY ?? 0,
          positionZ: r?.modelPositionZ ?? 0,
          pivotX: r?.modelPivotX ?? 0,
          pivotY: r?.modelPivotY ?? 0,
          pivotZ: r?.modelPivotZ ?? 0,
          unitySize:
            r && r.boundsSizeX != null && r.boundsSizeY != null && r.boundsSizeZ != null
              ? {
                  x: r.boundsSizeX / s,
                  y: r.boundsSizeY / s,
                  z: r.boundsSizeZ / s,
                  minY: ((r.boundsMinY ?? 0) - (r.modelPositionY ?? 0)) / s,
                }
              : undefined,
        });
      })
    );
    document.querySelectorAll<HTMLButtonElement>("[data-del]").forEach((b) =>
      b.addEventListener("click", () => confirmDeleteRecipe(app, setName, b.dataset.del!, refreshView))
    );
  }

  wireToolbar();
  wireCatChips();
  refreshSubChips();
  wireGridButtons();

  document.getElementById("cr-back")?.addEventListener("click", () => void renderSetChooser(app));
  document.getElementById("cr-unify-bun")?.addEventListener("click", () =>
    openBunSwapModal(setName, refreshView)
  );
  document.getElementById("cr-new-burger")?.addEventListener("click", () => {
    location.assign(burgerPath(setName));
  });
  document.getElementById("cr-new-filling")?.addEventListener("click", () => {
    location.assign(fillingPath(setName));
  });
  document.getElementById("cr-new-recipe")?.addEventListener("click", () => {
    syncCrPath(recipeFormPath(setName, "new"));
    void renderRecipeForm(app, setName, null, activeCategoryId ? { category: activeCategoryId } : undefined);
  });
}

// ==================== 🍞 一键统一面包皮 ====================

/** 目标面包皮的代价/风险说明（按 id）。等价性结论见 recipeGroups.ts BUN_EQUIVALENT_IDS。 */
function bunTargetNote(id: string): string {
  if (id === BUN_DLC8_ID)
    return "Burger大全规范面皮；与核心面包 IngredientOrderNode uID 同为 16088，订单匹配等价。";
  if (id === BUN_CORE_ID)
    return "主线核心面包，不引入额外 DLC 依赖；与 DLC8 面皮 uID 16088 等价。";
  if (id === BUN_DLC2_ID)
    return "⚠️ 未经 uID 实测，不在核心面包/DLC8 面皮的等价组内 —— 已有食材箱可能接不上订单。";
  return "";
}

/** 一道菜谱的面包层汇总（多层合并成一行）。 */
interface BunRecipeRow {
  assetPath: string;
  id: string;
  nameZh: string;
  isAssembly: boolean;
  isFinishedBurger: boolean;
  /** 当前面包皮 id（去重，通常只有一种）。 */
  bunIds: string[];
  /** 面包层数（含组装定义的 bunSO 字段）。 */
  layers: number;
}

function groupBunUsages(usages: BunUsage[]): BunRecipeRow[] {
  const byPath = new Map<string, BunRecipeRow>();
  for (const u of usages) {
    let row = byPath.get(u.assetPath);
    if (!row) {
      row = {
        assetPath: u.assetPath,
        id: u.id,
        nameZh: u.nameZh,
        isAssembly: u.isAssembly,
        isFinishedBurger: u.isFinishedBurger,
        bunIds: [],
        layers: 0,
      };
      byPath.set(u.assetPath, row);
    }
    if (!row.bunIds.includes(u.bunId)) row.bunIds.push(u.bunId);
    row.layers++;
  }
  return [...byPath.values()].sort((a, b) => a.nameZh.localeCompare(b.nameZh, "zh"));
}

/**
 * 🍞 一键统一面包皮：把本关卡集 `custom_recipes/` 内自定义菜谱的面包层统一换成同一种。
 *
 * 只改菜谱资产 —— 引用这些菜谱的关卡（BurgerOptional.bunSO 重算 / DLC 匹配表补全 /
 * bundle 依赖重建，均在 POST /api/level-recipes 里）与场景食材箱都**不动**，
 * 只在弹窗与结果里提醒作者自行处理。commonW2 共享库与官方汉堡由后端路径闸门硬拒。
 */
async function openBunSwapModal(setName: string, onDone: () => void): Promise<void> {
  showBusy("扫描面包皮…");
  let report: BunUsageReport;
  try {
    report = await api.fetchBunUsage(setName);
  } catch (e) {
    setStatus(e instanceof Error ? e.message : String(e), false);
    return;
  } finally {
    hideBusy();
  }

  const rows = groupBunUsages(report.usages);
  const sharedRows = groupBunUsages(report.sharedUsages);
  const bunById = new Map(report.buns.map((b) => [b.id, b]));
  const bunName = (id: string): string => bunById.get(id)?.nameZh ?? id;

  if (rows.length === 0) {
    openModal(
      "🍞 统一面包皮",
      `<p class="modal-hint">关卡集 <b>${esc(setName)}</b> 的 <code>custom_recipes/</code> 里没有引用汉堡面包皮的自定义菜谱，无需统一。</p>
       ${sharedRows.length > 0 ? `<p class="muted small">（commonW2 共享库里有 ${sharedRows.length} 道含面包的汉堡，属全关卡集共用资产，本工具不会修改。）</p>` : ""}`,
      '<button class="m-btn" data-close>关闭</button>'
    );
    document.querySelector<HTMLButtonElement>("[data-close]")?.addEventListener("click", closeModal);
    return;
  }

  // 默认目标：DLC8 面皮（Burger大全规范面皮）；不可用时回退到候选池第一个。
  const available = report.buns.filter((b) => b.bundleAvailable !== false);
  let target =
    available.find((b) => b.id === BUN_DLC8_ID)?.id ?? available[0]?.id ?? report.buns[0]?.id ?? "";
  /** 用户手动取消勾选的菜谱（其余默认全选）。 */
  const unchecked = new Set<string>();

  /** 该行是否已全部是目标面包皮（无需替换，置灰）。 */
  const isDone = (r: BunRecipeRow): boolean => r.bunIds.length === 1 && r.bunIds[0] === target;
  const pendingRows = (): BunRecipeRow[] => rows.filter((r) => !isDone(r));
  const selectedRows = (): BunRecipeRow[] => pendingRows().filter((r) => !unchecked.has(r.assetPath));

  const targetHtml = (): string =>
    report.buns
      .map((b) => {
        const disabled = b.bundleAvailable === false;
        const note = bunTargetNote(b.id);
        return `<label class="bs-target${disabled ? " disabled" : ""}${b.id === target ? " active" : ""}">
          <input type="radio" name="bs-target" value="${esc(b.id)}" ${b.id === target ? "checked" : ""} ${disabled ? "disabled" : ""}>
          ${foodIconImg("ingredients", b.id)}
          <span class="bs-target-text">
            <b>${esc(b.nameZh)}</b> <span class="muted small">${esc(b.id)}${b.bundleName ? ` · ${esc(b.bundleName)}` : ""}</span>
            ${disabled ? '<span class="mp-status err">bundle 未就绪，不可选</span>' : ""}
            ${note ? `<span class="muted small bs-note">${esc(note)}</span>` : ""}
          </span>
        </label>`;
      })
      .join("");

  const rowsHtml = (): string =>
    rows
      .map((r) => {
        const done = isDone(r);
        const checked = !done && !unchecked.has(r.assetPath);
        const kind = r.isAssembly ? "组装定义" : r.isFinishedBurger ? "成品汉堡" : "菜谱";
        const from = r.bunIds.map((b) => esc(bunName(b))).join(" / ");
        return `<label class="bs-row${done ? " done" : ""}">
          <input type="checkbox" class="bs-cb" value="${esc(r.assetPath)}" ${checked ? "checked" : ""} ${done ? "disabled" : ""}>
          <span class="bs-row-main">
            <b>${esc(r.nameZh)}</b>
            <span class="muted small">${esc(r.id)} · ${kind} · ${r.layers} 层面包</span>
          </span>
          <span class="bs-row-swap">${done ? `<span class="muted">已是 ${esc(bunName(target))}</span>` : `${from} <b>→</b> ${esc(bunName(target))}`}</span>
        </label>`;
      })
      .join("");

  const sharedHtml = (): string => {
    if (sharedRows.length === 0) return "";
    return `<details class="bs-shared">
      <summary>不可修改的 ${sharedRows.length} 项（commonW2 共享库）</summary>
      <p class="muted small">commonW2「🍔 Burger大全」是<b>全关卡集共用</b>的共享库，改它会污染所有关卡集，后端会硬拒。
      官方汉堡（Burger_Plain_SO 等）的面包锁死在游戏 bundle 内是 DLC2 面皮，同样无法更改。</p>
      <div class="bs-shared-list">${sharedRows
        .map(
          (r) =>
            `<div class="muted small">${esc(r.nameZh)} <span>${esc(r.bunIds.map(bunName).join(" / "))}</span></div>`
        )
        .join("")}</div>
    </details>`;
  };

  const body = `
    <p class="modal-hint">把关卡集 <b>${esc(setName)}</b> 的 <code>custom_recipes/</code> 内自定义菜谱的汉堡面包皮统一换成同一种（成品汉堡的组成层 + 本地组装定义的 bunSO）。</p>
    <div class="m-section-title">目标面包皮</div>
    <div class="bs-targets" id="bs-targets">${targetHtml()}</div>
    <div class="m-section-title bs-head">受影响的菜谱 <span class="muted small" id="bs-count"></span>
      <span style="flex:1"></span>
      <button type="button" class="m-btn small" id="bs-all">全选</button>
      <button type="button" class="m-btn small" id="bs-none">全不选</button>
    </div>
    <div class="bs-rows" id="bs-rows">${rowsHtml()}</div>
    ${sharedHtml()}
    <div class="bs-remind">
      <b>替换后需要你自己做的两件事（本工具不联动）：</b>
      <ol>
        <li>对<b>引用了这些菜谱的关卡</b>各重存一次菜谱 —— 才会重算 <code>BurgerOptional.bunSO</code>、补全 DLC 匹配表、重建 bundle 依赖。</li>
        <li>检查这些关卡场景里的<b>食材箱</b>是否还给着旧面包皮，需要的话自行替换。</li>
      </ol>
    </div>
    <p class="mp-status" id="bs-status"></p>
  `;

  openModal(
    "🍞 统一面包皮",
    body,
    `<button class="m-btn" id="bs-cancel">取消</button>
     <button class="m-btn primary" id="bs-apply">执行替换</button>`,
    { closeOnBackdrop: false }
  );
  document.querySelector(".modal-panel")?.classList.add("wide");

  const $rows = document.getElementById("bs-rows")!;
  const $count = document.getElementById("bs-count")!;
  const $apply = document.getElementById("bs-apply") as HTMLButtonElement;
  const $status = document.getElementById("bs-status")!;

  function refreshCount(): void {
    const n = selectedRows().length;
    const done = rows.length - pendingRows().length;
    $count.textContent = `已选 ${n} / 待替换 ${pendingRows().length}${done > 0 ? ` · ${done} 项已是目标` : ""}`;
    $apply.disabled = n === 0;
    $apply.textContent = n > 0 ? `执行替换（${n} 道）` : "执行替换";
  }

  function wireRows(): void {
    $rows.querySelectorAll<HTMLInputElement>(".bs-cb").forEach((cb) =>
      cb.addEventListener("change", () => {
        if (cb.checked) unchecked.delete(cb.value);
        else unchecked.add(cb.value);
        refreshCount();
      })
    );
  }

  function redrawRows(): void {
    $rows.innerHTML = rowsHtml();
    wireRows();
    refreshCount();
  }

  document.getElementById("bs-targets")?.addEventListener("change", (e) => {
    const input = e.target as HTMLInputElement;
    if (input.name !== "bs-target") return;
    target = input.value;
    // 切目标后「已是目标」的行会变，必须整体重绘
    document.getElementById("bs-targets")!.innerHTML = targetHtml();
    redrawRows();
  });
  document.getElementById("bs-all")?.addEventListener("click", () => {
    unchecked.clear();
    redrawRows();
  });
  document.getElementById("bs-none")?.addEventListener("click", () => {
    for (const r of pendingRows()) unchecked.add(r.assetPath);
    redrawRows();
  });
  document.getElementById("bs-cancel")?.addEventListener("click", closeModal);

  $apply.addEventListener("click", () => {
    void (async () => {
      const picked = selectedRows();
      if (picked.length === 0) return;
      $apply.disabled = true;
      $status.textContent = "替换中…";
      $status.className = "mp-status";
      try {
        const res = await api.replaceBun({
          setName,
          targetBunId: target,
          assetPaths: picked.map((r) => r.assetPath),
        });
        closeModal();
        const warn = res.warnings.length > 0 ? ` · ${res.warnings.join(" ")}` : "";
        const skip = res.skipped.length > 0 ? ` · 跳过 ${res.skipped.length} 项` : "";
        setStatus(
          `已把 ${res.changed} 道菜谱的 ${res.layers} 个面包层换成「${bunName(target)}」${skip}${warn}`,
          true
        );
        onDone();
      } catch (e) {
        $apply.disabled = false;
        $status.textContent = e instanceof Error ? e.message : String(e);
        $status.className = "mp-status err";
      }
    })();
  });

  redrawRows();
}

// ==================== Recipe Form Page (Create / Edit) ====================

interface SubItem {
  id: string;
  nameZh: string;
  nameEn?: string;
  score: number;
  cookingStepId: string;
  hasIcon: boolean;
  assetPath: string;
  /** 官方菜谱 vs 本关卡集自定义菜谱 */
  source: "official" | "levelset";
  recipeType: string;
  /** 本关卡集自定义菜谱的分类目录 id */
  category: string;
  ingredients: string[];
}

function categoryFromAssetPath(assetPath: string): string {
  const m = /custom_recipes\/([^/]+)\/[^/]+\.asset$/i.exec(assetPath.replace(/\\/g, "/"));
  return m ? m[1] : "";
}

/** 组成选择器上下文：关卡集自定义菜谱 + 目录全部菜谱（含官方成品与中间产物）。 */
function buildCompositionContext(
  levelsetRecipes: CustomRecipeSummary[],
  catalog: RecipeEntry[],
  excludeAssetPath?: string | null
): { subItems: SubItem[]; recipeIdSet: Set<string>; allRecipeLikes: RecipeWithGroups[] } {
  const subItems: SubItem[] = [];
  const seen = new Set<string>();
  const recipeIdSet = new Set<string>();
  const allRecipeLikes: RecipeWithGroups[] = [];
  const likesSeen = new Set<string>();

  const addSummary = (r: CustomRecipeSummary): void => {
    if (excludeAssetPath && r.assetPath === excludeAssetPath) return;
    if (seen.has(r.id)) return;
    seen.add(r.id);
    recipeIdSet.add(r.id);
    subItems.push({
      id: r.id,
      nameZh: r.nameZh,
      nameEn: r.nameEn,
      score: r.score,
      cookingStepId: r.cookingStepId,
      hasIcon: r.hasIcon,
      assetPath: r.assetPath,
      source: "levelset",
      recipeType: r.type || "custom",
      category: r.category || categoryFromAssetPath(r.assetPath),
      ingredients: r.ingredients,
    });
  };

  const addCatalog = (c: RecipeEntry): void => {
    if (excludeAssetPath && c.assetPath === excludeAssetPath) return;
    if (seen.has(c.id)) return;
    seen.add(c.id);
    recipeIdSet.add(c.id);
    const isLevelset = !!(c.isCustom && c.group === "levelset");
    subItems.push({
      id: c.id,
      nameZh: c.nameZh,
      nameEn: c.nameEn,
      score: c.score ?? 0,
      cookingStepId: c.cookingStep ?? "",
      hasIcon: c.icon !== false,
      assetPath: c.assetPath,
      source: isLevelset ? "levelset" : "official",
      recipeType: c.type ?? (isLevelset ? "custom" : "other"),
      category: isLevelset ? categoryFromAssetPath(c.assetPath) : "",
      ingredients: c.ingredients ?? [],
    });
  };

  const addLike = (r: RecipeWithGroups): void => {
    if (!r.id || likesSeen.has(r.id)) return;
    likesSeen.add(r.id);
    allRecipeLikes.push(r);
  };

  for (const r of levelsetRecipes) {
    addSummary(r);
    if (!excludeAssetPath || r.assetPath !== excludeAssetPath) {
      addLike(normalizeCustomRecipeCard(r) as RecipeWithGroups);
    }
  }

  for (const c of visibleRecipes(catalog)) {
    addCatalog(c);
    if (!excludeAssetPath || c.assetPath !== excludeAssetPath) {
      addLike(
        c.isCustom
          ? ({ ...c, isCustom: true, intermediate: false } as RecipeWithGroups)
          : (c as RecipeWithGroups)
      );
    }
  }

  subItems.sort(
    (a, b) =>
      (a.source === "levelset" ? 0 : 1) - (b.source === "levelset" ? 0 : 1) ||
      a.score - b.score ||
      a.nameZh.localeCompare(b.nameZh, "zh")
  );

  return { subItems, recipeIdSet, allRecipeLikes };
}

/** 菜谱编辑器的可选预设。
 *  `mode: "filling"` = 夹心模式：夹心本质就是一道 0 分自定义菜谱，因此**复用同一个编辑器**，
 *  只做预设与增量（预设 0 分 / Cooked / 盘子装盘 / burger_filling 分类，模型区多一条
 *  「模板网格 + 自制贴图」零建模通道）。普通新建菜谱行为完全不变。 */
export interface RecipeFormPresets {
  score?: number;
  category?: string;
  mode?: "filling";
  /** 夹心模式默认模板网格 id（缺省 FriedFishCake）。 */
  templateMeshId?: string;
  /** 覆盖「返回」与保存后的跳转（夹心工作台页用；缺省回菜谱列表）。 */
  onBack?: () => void;
}

export async function renderRecipeForm(
  app: HTMLElement,
  setName: string,
  assetPath: string | null,
  presets?: RecipeFormPresets
): Promise<void> {
  const isEdit = assetPath != null;
  const isFilling = presets?.mode === "filling";
  const content = shell(app, isEdit ? (isFilling ? "编辑夹心" : "编辑菜谱") : (isFilling ? "🥩 新建夹心" : "新建菜谱"));
  setBusy("加载参考数据…");

  let ingredients: IngredientEntry[] = [];
  let refs: CustomRecipeReferences;
  let config: CustomRecipeConfig;
  let recipe: CustomRecipeSummary | undefined;
  let levelsetRecipes: CustomRecipeSummary[] = [];
  let catalog: RecipeEntry[] = [];

  const EMPTY_REFS: CustomRecipeReferences = {
    cookingSteps: [],
    platingSteps: [],
    platingContainers: [],
    icons: [],
    reusableModels: [],
    ingredients: [],
  };

  try {
    [ingredients, refs, config] = await Promise.all([
      api.fetchIngredients().catch(() => [] as IngredientEntry[]),
      api.fetchCustomRecipeReferences(setName).catch(() => EMPTY_REFS),
      api.fetchCustomRecipeConfig(setName),
    ]);
    ingredients = visibleIngredients(ingredients);
    [levelsetRecipes, catalog] = await Promise.all([
      api.fetchCustomRecipes(setName).catch(() => [] as CustomRecipeSummary[]),
      api.fetchRecipeCatalog(setName).catch(() => [] as RecipeEntry[]),
    ]);
    if (isEdit) {
      recipe = levelsetRecipes.find((r) => r.assetPath === assetPath);
    }
  } catch (e) {
    showError(e);
    hideBusy();
    return;
  }
  hideBusy();

  // ---- 组成状态：id → 数量（同一种食材/中间产物可出现多次） ----
  const compositionCtx = buildCompositionContext(levelsetRecipes, catalog, isEdit ? assetPath : null);
  const { subItems, recipeIdSet: subIdSet, allRecipeLikes } = compositionCtx;

  const selectedIng = new Map<string, number>();
  const selectedSub = new Map<string, number>();
  for (const id of recipe?.compositionIds ?? []) {
    const target = subIdSet.has(id) ? selectedSub : selectedIng;
    target.set(id, (target.get(id) ?? 0) + 1);
  }

  const isNew = !isEdit;
  const recipeName = recipe?.recipeName ?? "";
  const nameZh = recipe?.nameZh ?? "";
  const nameEn = recipe?.nameEn ?? "";
  const categoryId =
    recipe?.category ??
    presets?.category ??
    (isFilling ? "burger_filling" : config.categories?.length > 0 ? config.categories[0].id : "");
  // 夹心不可单独点单，恒 0 分（字段仍可编辑，只是默认值与提示不同）
  const score = recipe?.score ?? presets?.score ?? 0;
  const type = recipe?.type ?? (isFilling || presets?.score === 0 ? "Cooked" : "Composite");
  const cookingStepId = recipe?.cookingStepId ?? "";
  const cookingStepIconId = recipe?.cookingStepIconId ?? "";
  const platingStepId = recipe?.platingStepId ?? (isFilling ? "Plate" : "");
  const mixingIconId = recipe?.mixingIconId ?? "";
  const modelPrefabId = "";

  // 「burger」为 Burger大全共享库保留分类：新建/编辑本集菜谱时不可选
  const categories = (config.categories ?? []).filter((c) => c.id !== "burger");

  const ingById = new Map<string, IngredientEntry>();
  for (const i of ingredients) ingById.set(i.id, i);

  const ingLoadFailed = ingredients.length === 0;

  const subById = new Map<string, SubItem>();
  for (const s of subItems) subById.set(s.id, s);

  function catDisplay(c: CustomRecipeCategory): string {
    return c.zh || c.id;
  }

  /** 按 Unity 实际导入尺寸（菜谱摘要的包围盒 ÷ 已保存缩放）反推模型原始尺寸（Unity 单位），
   *  供 cm 尺寸输入与自动适配校准（three.js 预览尺寸可能与 Unity 不一致）。 */
  function rawSizeOf(): { x: number; y: number; z: number; minY: number } | undefined {
    if (!recipe || recipe.boundsSizeX == null || recipe.boundsSizeY == null || recipe.boundsSizeZ == null) return undefined;
    const s = recipe.modelScale > 0 ? recipe.modelScale : 1;
    return {
      x: recipe.boundsSizeX / s,
      y: recipe.boundsSizeY / s,
      z: recipe.boundsSizeZ / s,
      minY: ((recipe.boundsMinY ?? 0) - (recipe.modelPositionY ?? 0)) / s,
    };
  }

  function catSelectHtml(): string {
    let options = categories
      .map((c) => `<option value="${esc(c.id)}" ${c.id === categoryId ? "selected" : ""}>${esc(catDisplay(c))}</option>`)
      .join("");
    if (!categories.some((c) => c.id === categoryId) && categoryId) {
      options += `<option value="${esc(categoryId)}" selected>${esc(categoryId)}</option>`;
    }
    return `<select id="cr-type-cat" class="m-select">${options}</select>`;
  }

  function selectHtml(entries: { guid?: string; id: string; nameZh: string }[], current: string, fieldId: string): string {
    const unique = new Map<string, string>();
    for (const e of entries) {
      if (!unique.has(e.id)) unique.set(e.id, e.nameZh || e.id);
    }
    return `<select id="${fieldId}" class="m-select">
      <option value="">— 不设置 —</option>
      ${[...unique.entries()]
        .map(([id, name]) => `<option value="${esc(id)}" ${id === current ? "selected" : ""}>${esc(name)} (${esc(id)})</option>`)
        .join("")}
    </select>`;
  }

  // ---- 组成渲染（弹窗选择 + 已选列表） ----

  function expandSelection(): string[] {
    const out: string[] = [];
    for (const [id, n] of selectedIng) for (let i = 0; i < n; i++) out.push(id);
    for (const [id, n] of selectedSub) for (let i = 0; i < n; i++) out.push(id);
    return out;
  }

  /** 选择弹窗内的卡片：点击选中（数量 1），已选时显示 −/＋ 数量调整。 */
  function pickerCardHtml(id: string, isSub: boolean, count: number): string {
    const selected = count > 0;
    const stepper = selected
      ? `<div class="cp-count">
          <button type="button" class="cp-step" data-cpdec data-cpid="${esc(id)}" data-cpsub="${isSub ? 1 : 0}">−</button>
          <span class="cp-num">${count}</span>
          <button type="button" class="cp-step" data-cpinc data-cpid="${esc(id)}" data-cpsub="${isSub ? 1 : 0}">＋</button>
        </div>`
      : "";
    if (isSub) {
      const s = subById.get(id);
      if (!s) return "";
      const roleBadge =
        s.score <= 0
          ? `<span class="cr-badge-inter">中间产物</span>`
          : `<span class="cr-badge-done">成品菜</span>`;
      const src =
        s.source === "levelset" ? crIconSrc(s) : `/icons/recipes/${encodeURIComponent(s.id)}.png`;
      const srcTag = s.source === "levelset" ? "本关卡集" : "官方";
      return `<div class="pick-card cp-card${selected ? " selected" : ""}" data-cpid="${esc(id)}" data-cpsub="1" title="${esc(s.id)}">
        <span class="pc-head"><img class="food-icon" loading="lazy" src="${esc(src)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'" /><span class="pc-name">${esc(s.nameZh)}${roleBadge}${s.nameEn ? ` <span class="muted pc-en">${esc(s.nameEn)}</span>` : ""}</span></span>
        <span class="muted small">${s.cookingStepId ? esc(s.cookingStepId) : "无烹饪步骤"} · ${(s.ingredients ?? []).length} 种食材 · ${srcTag}</span>
        ${stepper}
      </div>`;
    }
    const ing = ingById.get(id);
    if (!ing) return "";
    const badge = ing.group && ing.group !== "core" ? ` <span class="pc-badge">${foodGroupLabel(ing.group)}</span>` : "";
    const en = (ing.nameEn && ing.nameEn.trim()) || "";
    return `<div class="pick-card cp-card${selected ? " selected" : ""}" data-cpid="${esc(id)}" data-cpsub="0" title="${esc(ing.id)}">
      <span class="pc-head">${foodIconImg("ingredients", ing.id)}<span class="pc-name">${esc(ing.nameZh)}${badge}${en ? ` <span class="muted pc-en">${en}</span>` : ""}</span></span>
      ${stepper}
    </div>`;
  }

  function openCompositionPicker(): void {
    const draft = new Map<string, number>();
    for (const [id, n] of selectedIng) draft.set(id, n);
    for (const [id, n] of selectedSub) draft.set(id, n);
    type CompFilter = "all" | "ing" | "sub" | "inter" | "levelset";
    let filter: CompFilter = "all";
    let q = "";

    const catLabel = (catId: string): string => {
      const c = categories.find((x) => x.id === catId);
      return c ? c.zh || c.id : catId || "未分类";
    };

    function recipeMatchesTab(s: SubItem): boolean {
      if (filter === "sub") return s.source === "official" && s.score > 0;
      if (filter === "inter") return s.source === "official" && s.score <= 0;
      if (filter === "levelset") return s.source === "levelset";
      return true;
    }

    function matchesQuery(id: string, isSub: boolean): boolean {
      const query = q.trim().toLowerCase();
      if (!query) return true;
      if (!isSub) {
        const ing = ingById.get(id);
        if (!ing) return false;
        return (
          ing.nameZh.toLowerCase().includes(query) ||
          (ing.nameEn ?? "").toLowerCase().includes(query) ||
          ing.id.toLowerCase().includes(query)
        );
      }
      const s = subById.get(id);
      if (!s) return false;
      return (
        s.nameZh.toLowerCase().includes(query) ||
        (s.nameEn ?? "").toLowerCase().includes(query) ||
        s.id.toLowerCase().includes(query)
      );
    }

    function filteredIds(): { id: string; isSub: boolean }[] {
      const out: { id: string; isSub: boolean }[] = [];
      if (filter === "all" || filter === "ing") {
        for (const i of ingredients) {
          if (!matchesQuery(i.id, false)) continue;
          out.push({ id: i.id, isSub: false });
        }
      }
      if (filter === "all" || filter === "sub" || filter === "inter" || filter === "levelset") {
        for (const s of subItems) {
          if (!recipeMatchesTab(s)) continue;
          if (!matchesQuery(s.id, true)) continue;
          out.push({ id: s.id, isSub: true });
        }
      }
      return out;
    }

    function cardsHtml(items: { id: string; isSub: boolean }[]): string {
      return items.map((x) => pickerCardHtml(x.id, x.isSub, draft.get(x.id) ?? 0)).join("");
    }

    function sectionHtml(title: string, items: { id: string; isSub: boolean }[]): string {
      if (!items.length) return "";
      return `<div class="cp-section">
        <div class="cp-section-title">${esc(title)}</div>
        <div class="pick-grid">${cardsHtml(items)}</div>
      </div>`;
    }

    function groupByRecipeType(items: { id: string; isSub: boolean }[]): [string, { id: string; isSub: boolean }[]][] {
      const map = new Map<string, { id: string; isSub: boolean }[]>();
      for (const x of items) {
        const t = subById.get(x.id)?.recipeType ?? "other";
        if (!map.has(t)) map.set(t, []);
        map.get(t)!.push(x);
      }
      const order = (t: string) => {
        const i = RECIPE_TYPE_ORDER.indexOf(t);
        return i < 0 ? 99 : i;
      };
      return [...map.entries()].sort((a, b) => order(a[0]) - order(b[0]));
    }

    function groupByCategory(items: { id: string; isSub: boolean }[]): [string, { id: string; isSub: boolean }[]][] {
      const map = new Map<string, { id: string; isSub: boolean }[]>();
      for (const x of items) {
        const cat = subById.get(x.id)?.category ?? "";
        if (!map.has(cat)) map.set(cat, []);
        map.get(cat)!.push(x);
      }
      const order = categories.map((c) => c.id);
      return [...map.entries()].sort((a, b) => {
        const ia = order.indexOf(a[0]);
        const ib = order.indexOf(b[0]);
        return (ia < 0 ? 99 : ia) - (ib < 0 ? 99 : ib);
      });
    }

    function pickerGridHtml(items: { id: string; isSub: boolean }[]): string {
      if (!items.length) return "";
      if (filter === "sub") {
        return groupByRecipeType(items)
          .map(([type, arr]) => sectionHtml(recipeTypeLabel(type), arr))
          .join("");
      }
      if (filter === "inter") {
        return groupByRecipeType(items)
          .map(([type, arr]) => sectionHtml(recipeTypeLabel(type), arr))
          .join("");
      }
      if (filter === "levelset") {
        const subs = items.filter((x) => x.isSub);
        const finished = subs.filter((x) => (subById.get(x.id)?.score ?? 0) > 0);
        const inter = subs.filter((x) => (subById.get(x.id)?.score ?? 0) <= 0);
        let html = "";
        if (finished.length) {
          html += `<div class="cp-section-band">成品菜</div>`;
          html += groupByCategory(finished)
            .map(([cat, arr]) => sectionHtml(catLabel(cat), arr))
            .join("");
        }
        if (inter.length) {
          html += `<div class="cp-section-band">中间产物</div>`;
          html += groupByCategory(inter)
            .map(([cat, arr]) => sectionHtml(catLabel(cat), arr))
            .join("");
        }
        return html;
      }
      return `<div class="pick-grid">${cardsHtml(items)}</div>`;
    }

    function bodyHtml(): string {
      const ids = filteredIds();
      const chips = [
        `<button type="button" class="cr-comp-chip${filter === "all" ? " active" : ""}" data-filter="all">全部</button>`,
        `<button type="button" class="cr-comp-chip${filter === "ing" ? " active" : ""}" data-filter="ing">食材</button>`,
        `<button type="button" class="cr-comp-chip${filter === "sub" ? " active" : ""}" data-filter="sub">成品菜</button>`,
        `<button type="button" class="cr-comp-chip${filter === "inter" ? " active" : ""}" data-filter="inter">中间产物</button>`,
        `<button type="button" class="cr-comp-chip${filter === "levelset" ? " active" : ""}" data-filter="levelset">本关卡集</button>`,
      ].join("");
      const loadWarn = ingLoadFailed && (filter === "all" || filter === "ing")
        ? '<p class="mp-status err">⚠️ 未加载到食材数据（桥接 /api/catalog/ingredients 异常），请刷新重试或检查 Unity 桥。</p>'
        : "";
      const grid = pickerGridHtml(ids);
      return `
        <div class="cr-comp-toolbar">
          <input type="search" id="cp-search" class="ing-search" placeholder="搜索名称 / ID…" autocomplete="off">
          <div class="ing-groups">${chips}</div>
        </div>
        ${loadWarn}
        <div class="modal-scroll" id="cp-scroll">
          <div id="cp-grid">${grid}</div>
          ${ids.length ? "" : '<p class="muted">没有匹配的项</p>'}
        </div>`;
    }

    openModal(
      "添加食材 / 菜谱",
      `<p class="modal-hint">点击卡片加入（默认 1 份），再次点击 − / ＋ 调整数量。<b>官方成品菜按类型分组；本关卡集自定义菜谱单独一页</b>（含成品与中间产物）。</p>
       <div id="cp-body">${bodyHtml()}</div>`,
      `<button type="button" class="m-btn" data-cancel>取消</button>
       <button type="button" class="m-btn primary" data-ok>确定</button>`
    );
    const panel = document.querySelector(".modal-panel");
    if (panel) {
      panel.classList.add("wide");
      panel.classList.add("cp-panel");
    }

    const body = document.getElementById("cp-body")!;

    function renderBody(): void {
      body.innerHTML = bodyHtml();
      wireBody();
    }

    function wireBody(): void {
      document.getElementById("cp-search")?.addEventListener("input", (e) => {
        q = (e.target as HTMLInputElement).value;
        const grid = document.getElementById("cp-grid");
        if (grid) {
          const ids = filteredIds();
          grid.innerHTML = pickerGridHtml(ids);
          const scroll = document.getElementById("cp-scroll");
          if (scroll) {
            const empty = scroll.querySelector("p.muted");
            if (empty) empty.remove();
            if (ids.length === 0) {
              scroll.insertAdjacentHTML("beforeend", '<p class="muted">没有匹配的项</p>');
            }
          }
        }
      });
      document.querySelectorAll<HTMLButtonElement>("#cp-body .cr-comp-chip").forEach((b) => {
        b.addEventListener("click", () => {
          filter = (b.dataset.filter as CompFilter) ?? "all";
          renderBody();
        });
      });
      document.getElementById("cp-grid")?.addEventListener("click", (e) => {
        const t = e.target as HTMLElement;
        const step = t.closest<HTMLButtonElement>(".cp-step");
        const card = t.closest<HTMLElement>(".cp-card");
        if (!card) return;
        const cid = card.dataset.cpid!;
        const isSub = card.dataset.cpsub === "1";
        let count = draft.get(cid) ?? 0;
        if (step) {
          const delta = step.dataset.cpinc !== undefined ? 1 : -1;
          count = Math.max(0, count + delta);
        } else {
          count = count > 0 ? 0 : 1;
        }
        if (count <= 0) draft.delete(cid);
        else draft.set(cid, count);
        card.outerHTML = pickerCardHtml(cid, isSub, count);
      });
    }

    wireBody();
    document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
    document.querySelector("[data-ok]")?.addEventListener("click", () => {
      selectedIng.clear();
      selectedSub.clear();
      for (const [id, n] of draft) {
        const target = subIdSet.has(id) ? selectedSub : selectedIng;
        target.set(id, n);
      }
      closeModal();
      renderCompList();
      renderPreview();
    });
  }

  /** 表单内已选列表（行 + 数量 stepper + 移除）。 */
  function renderCompList(): void {
    const el = document.getElementById("cr-comp-list");
    if (!el) return;

    const rows: string[] = [];
    for (const [id, n] of selectedIng) {
      const i = ingById.get(id);
      rows.push(`<div class="cr-comp-row" data-rowid="ing:${esc(id)}">
        ${foodIconImg("ingredients", id)}
        <span class="cr-row-name">${esc(i?.nameZh ?? id)}</span>
        <span class="cr-row-stepper">
          <button type="button" class="cr-step" data-rowdec="ing:${esc(id)}">−</button>
          <span class="cr-step-num">${n}</span>
          <button type="button" class="cr-step" data-rowinc="ing:${esc(id)}">＋</button>
        </span>
        <button type="button" class="cr-step-del" data-rowdel="ing:${esc(id)}" title="移除">×</button>
      </div>`);
    }
    for (const [id, n] of selectedSub) {
      const s = subById.get(id);
      const badge =
        s && s.score <= 0
          ? '<span class="cr-chip-tag cr-chip-inter">中间产物</span>'
          : '<span class="cr-chip-tag cr-chip-done">成品菜</span>';
      const srcTag =
        s?.source === "levelset"
          ? '<span class="cr-chip-tag cr-chip-levelset">本关卡集</span>'
          : '<span class="cr-chip-tag">官方</span>';
      rows.push(`<div class="cr-comp-row cr-comp-row-sub" data-rowid="sub:${esc(id)}">
        ${s ? `<img class="food-icon" loading="lazy" src="${esc(s.source === "levelset" ? crIconSrc(s) : `/icons/recipes/${encodeURIComponent(s.id)}.png`)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'" />` : ""}
        <span class="cr-row-name">${esc(s?.nameZh ?? id)}${srcTag}${badge}</span>
        <span class="cr-row-stepper">
          <button type="button" class="cr-step" data-rowdec="sub:${esc(id)}">−</button>
          <span class="cr-step-num">${n}</span>
          <button type="button" class="cr-step" data-rowinc="sub:${esc(id)}">＋</button>
        </span>
        <button type="button" class="cr-step-del" data-rowdel="sub:${esc(id)}" title="移除">×</button>
      </div>`);
    }

    el.innerHTML = rows.length
      ? rows.join("")
      : '<p class="muted small" style="margin:4px 0">尚未选择。点击下方「添加」选择食材或菜谱。</p>';

    const total = expandSelection().length;
    const hint = document.getElementById("cr-comp-hint");
    if (hint) {
      hint.textContent = rows.length
        ? `共 ${total} 份组成（食材 ${[...selectedIng.values()].reduce((a, b) => a + b, 0)} · 菜谱 ${[...selectedSub.values()].reduce((a, b) => a + b, 0)}）`
        : "";
    }

    el.querySelectorAll<HTMLButtonElement>("[data-rowinc], [data-rowdec], [data-rowdel]").forEach((b) => {
      b.addEventListener("click", () => {
        const kind = (b.dataset.rowinc ?? b.dataset.rowdec ?? b.dataset.rowdel ?? "") as "ing:" | "sub:" | "";
        const [k, id] = kind.split(":");
        const target = k === "ing" ? selectedIng : selectedSub;
        const cur = target.get(id) ?? 0;
        if (b.dataset.rowdel !== undefined) {
          target.delete(id);
        } else if (b.dataset.rowinc !== undefined) {
          target.set(id, cur + 1);
        } else {
          if (cur <= 1) target.delete(id);
          else target.set(id, cur - 1);
        }
        renderCompList();
        renderPreview();
      });
    });
  }

  function expandLeaf(ids: string[]): string[] {
    const out: string[] = [];
    for (const id of ids) {
      const s = subById.get(id);
      if (s && (s.ingredients ?? []).length > 0) out.push(...s.ingredients);
      else out.push(id);
    }
    return out;
  }

  // 本地已选图标（未保存即可在组装预览中看到）
  let iconPreviewUrl: string | null = null;

  function renderPreview(): void {
    const el = document.getElementById("cr-preview");
    if (!el) return;
    const rname = (document.getElementById("cr-rec-name") as HTMLInputElement)?.value.trim() ?? "";
    const zh = (document.getElementById("cr-zh") as HTMLInputElement)?.value.trim() ?? "";
    const en = (document.getElementById("cr-en") as HTMLInputElement)?.value.trim() ?? "";
    const scoreVal = Number((document.getElementById("cr-score") as HTMLInputElement)?.value) || 0;
    const typeVal = (document.getElementById("cr-type") as HTMLSelectElement)?.value ?? "Composite";
    // Mixed 类型：烹饪步骤为所选步骤（保存同值），由 mixing 标记带出搅拌步骤
    const cookStep = typeVal === "Composite" ? "" : (document.getElementById("cr-cook-step") as HTMLSelectElement)?.value ?? "";
    const compIds = expandSelection();
    const preview: RecipeWithGroups = {
      guid: "",
      id: rname || "preview",
      nameZh: zh || rname || "未命名菜谱",
      nameEn: en || undefined,
      assetPath: "",
      isCustom: true,
      mixing: typeVal === "Mixed",
      group: "levelset",
      score: scoreVal,
      intermediate: false,
      ingredients: expandLeaf(compIds),
      compositionIds: compIds,
      cookingStep: cookStep || undefined,
      type: "custom",
    };
    el.innerHTML = rlCardHtml(preview, {
      allRecipes: allRecipeLikes,
      ingredientName: (id) => ingById.get(id)?.nameZh ?? id,
      iconSrc: iconPreviewUrl
        ? () => iconPreviewUrl!
        : isEdit && recipe?.hasIcon && recipe.assetPath
          ? () => crIconSrc(recipe)
          : undefined,
    });
  }

  // ---- 表单 DOM ----

  content.innerHTML = `
    <div class="m-actions-row">
      <button class="m-btn" id="cr-form-back">← 返回菜谱列表</button>
      <span class="muted">关卡集：<b>${esc(setName)}</b> · ${isEdit ? `编辑 ${esc(recipeName)}` : "新建菜谱"}</span>
      <span style="flex:1"></span>
      <button class="m-btn primary" id="cr-form-save">💾 保存</button>
    </div>
    <div class="cr-form">
      ${isFilling ? `<p class="modal-hint cr-filling-hint">🥩 <b>夹心</b>就是一道 <b>0 分的自定义菜谱</b>——不可单独点单，只作为汉堡的一层。
        做好并给它一个模型后，就能在「🍔 汉堡组装工作台」的候选里选用（没有模型的夹心会被过滤，因为游戏里那层看不见）。
        下面用的是和普通菜谱完全相同的编辑器，额外多了一条「模板网格 + 自制贴图」的零建模通道。</p>` : ""}
      <div class="cr-section">
        <div class="m-section-title">组装效果（实时预览）</div>
        <div id="cr-preview" class="cr-preview"></div>
      </div>
      <div class="cr-section">
        <div class="m-section-title">基本信息</div>
        <label class="m-field">标识符 recipeName（仅字母/数字/下划线，创建后不可修改）
          <input type="text" id="cr-rec-name" value="${esc(recipeName)}" ${isEdit ? "disabled" : ""} placeholder="MyRecipe">
        </label>
        <div class="cr-form-grid">
          <label class="m-field">中文名<input type="text" id="cr-zh" value="${esc(nameZh)}" placeholder="我的菜谱"></label>
          <label class="m-field">英文名<input type="text" id="cr-en" value="${esc(nameEn)}" placeholder="My Recipe"></label>
          <label class="m-field">分类 ${catSelectHtml()}
            <button type="button" class="m-btn" id="cr-new-cat-inline" style="margin-top:6px">+ 新建分类</button>
          </label>
          <label class="m-field">UID（自动生成）<input type="text" value="${recipe?.uID ?? (isNew ? config.uidPrefix * 1000 + config.nextSequence : "—")}" disabled></label>
          <label class="m-field">菜谱图标（PNG，卡片图）<input type="file" id="cr-icon-upload" accept="image/png">
            <span class="muted small">上传后立即在组装预览中显示</span></label>
        </div>
      </div>
      <div class="cr-section">
        <div class="m-section-title">组成（食材 / 菜谱）</div>
        <div class="cr-comp-list" id="cr-comp-list"></div>
        <div class="cr-comp-toolbar" style="margin-top:10px">
          <button type="button" class="m-btn primary" id="cr-add-comp">＋ 添加食材 / 菜谱</button>
          <span class="muted small" id="cr-comp-hint" style="margin-left:auto"></span>
        </div>
      </div>
      <div class="cr-section">
        <div class="m-section-title">烹饪与装盘</div>
        <div class="cr-form-grid">
          <label class="m-field">类型
            <select id="cr-type" class="m-select">
              <option value="Composite" ${type === "Composite" ? "selected" : ""}>Composite（组合）</option>
              <option value="Cooked" ${type === "Cooked" ? "selected" : ""}>Cooked（烹饪）</option>
              <option value="Mixed" ${type === "Mixed" ? "selected" : ""}>Mixed（搅拌）</option>
            </select>
          </label>
          <label class="m-field">分数<input type="number" id="cr-score" value="${score}" min="0">
            <span class="muted small">${isFilling
              ? "夹心一般保持 <b>0 分</b>：0 分 = 中间产物，不进订单、只作为组成层"
              : "菜谱分值（卡片展示）"}</span>
          </label>
          <label class="m-field" id="cr-cook-step-field">烹饪步骤 ${selectHtml(refs.cookingSteps, cookingStepId, "cr-cook-step")}</label>
          <label class="m-field" id="cr-cook-icon-field">烹饪图标 ${selectHtml(refs.icons, cookingStepIconId, "cr-cook-icon")}</label>
          <label class="m-field" id="cr-cook-prog-field">烹饪程度
            <select id="cr-cook-prog" class="m-select">
              <option value="0">Raw（生）</option>
              <option value="1" selected>Cooked（熟）</option>
              <option value="2">Burnt（焦）</option>
            </select>
          </label>
          <label class="m-field" id="cr-plate-field">装盘容器 ${selectHtml(
            refs.platingContainers?.length
              ? refs.platingContainers
              : (refs.platingSteps ?? []).filter((e) => e.id === "Plate" || e.id === "Glass"),
            platingStepId,
            "cr-plate-step"
          )}
            <span class="muted small">决定上桌容器（盘子/杯子）</span>
          </label>
          <label class="m-field" id="cr-mix-icon-field">搅拌图标 ${selectHtml(refs.icons, mixingIconId, "cr-mix-icon")}</label>
          <label class="m-field" id="cr-mix-prog-field">搅拌程度
            <select id="cr-mix-prog" class="m-select">
              <option value="0">Unmixed（未搅拌）</option>
              <option value="1" selected>Mixed（已搅拌）</option>
              <option value="2">OverMixed（过度搅拌）</option>
            </select>
          </label>
        </div>
      </div>
      <div class="cr-section">
        <div class="m-section-title">模型（3D）</div>
        <div class="cr-model-block">
          <div class="cr-model-upload">
            <label class="m-field">上传 3D 模型（FBX / OBJ，可多选 .mtl 与贴图）<input type="file" id="cr-model-file" accept=".fbx,.obj,.mtl" multiple></label>
            <label class="m-field">复用已有模型 ${selectHtml(refs.reusableModels, modelPrefabId, "cr-model-ref")}</label>
          </div>
          <div class="m-field cr-tex-field">补充贴图（点击格子选择/替换）
            <div class="cr-tex-slots">
              <button type="button" class="cr-tex-slot" data-tex="base_color">
                <span class="cr-tex-thumb" id="cr-tex-thumb-base_color">＋</span>
                <span class="cr-tex-name">base_color <b>必传</b></span>
              </button>
              <button type="button" class="cr-tex-slot" data-tex="roughness">
                <span class="cr-tex-thumb" id="cr-tex-thumb-roughness">＋</span>
                <span class="cr-tex-name">roughness</span>
              </button>
              <button type="button" class="cr-tex-slot" data-tex="metallic">
                <span class="cr-tex-thumb" id="cr-tex-thumb-metallic">＋</span>
                <span class="cr-tex-name">metallic</span>
              </button>
              <button type="button" class="cr-tex-slot" data-tex="normal">
                <span class="cr-tex-thumb" id="cr-tex-thumb-normal">＋</span>
                <span class="cr-tex-name">normal</span>
              </button>
            </div>
            <input type="file" id="cr-model-texture" accept=".png,.jpg,.jpeg,image/png,image/jpeg" hidden>
          </div>
          <div class="cr-model-template">
            <div class="m-section-title small">零建模：模板网格 + 自制贴图</div>
            <p class="muted small">不会建模也能给夹心做出模型：选一个内置模板网格，再用取色/上传/画笔做一张 1024×1024 贴图即可。
              模板 FBX 会被<b>拷贝一份</b>到本菜谱目录（模板本身只读，拷贝后与共享库无引用关系）。
              若上方已选了自己的 FBX/OBJ，则以你的模型为准，本区忽略。</p>
            <div class="cr-form-grid">
              <label class="m-field">模板网格
                <select id="cr-tpl-mesh" class="m-select"><option value="">（不使用模板）</option></select>
              </label>
              <label class="m-field">贴图
                <button type="button" class="m-btn" id="cr-tpl-tex">🎨 编辑贴图</button>
                <span class="muted small" id="cr-tpl-tex-state">未设置</span>
              </label>
              <label class="m-field">预览
                <button type="button" class="m-btn" id="cr-tpl-preview">🔍 预览模板效果</button>
                <span class="muted small">用当前贴图实时渲染，不写盘</span>
              </label>
            </div>
          </div>
          <div class="cr-model-tools">
            <label class="m-field">在线预览<button type="button" class="m-btn" id="cr-preview-model">👁 预览并调整方向/大小</button></label>
            <label class="m-field">模型诊断<button type="button" class="m-btn" id="cr-diagnose">🔍 检查装盘链路</button></label>
          </div>
        </div>
      </div>
    </div>
    <div class="cr-form-footer">
      <button class="m-btn" id="cr-form-back2">← 返回菜谱列表</button>
      <span style="flex:1"></span>
      <button class="m-btn primary" id="cr-form-save2">💾 保存</button>
    </div>
  `;

  renderPreview();
  renderCompList();

  // ---- 模板网格 + 自制贴图（零建模通道；与上方 FBX 上传互斥，FBX 优先） ----

  let tplMeshId = "";
  let tplTexBase64 = "";
  let tplTexColor = "#c06205";

  const tplSelect = document.getElementById("cr-tpl-mesh") as HTMLSelectElement | null;
  if (tplSelect) {
    void api
      .fetchModelTemplates()
      .then((tpls) => {
        if (tpls.length === 0) return;
        const want = presets?.templateMeshId ?? tpls.find((t) => t.isDefault)?.id ?? tpls[0].id;
        tplSelect.innerHTML =
          '<option value="">（不使用模板）</option>' +
          tpls
            .map((t) => `<option value="${esc(t.id)}">${esc(t.id)}${t.isDefault ? "（默认）" : ""}</option>`)
            .join("");
        // 夹心模式默认就选上模板，降低「做不出模型」的门槛
        if (isFilling) {
          tplSelect.value = want;
          tplMeshId = want;
        }
      })
      .catch(() => {
        // 旧桥没有该端点：保持「不使用模板」
      });
    tplSelect.addEventListener("change", () => {
      tplMeshId = tplSelect.value;
    });
  }

  const tplTexState = document.getElementById("cr-tpl-tex-state");
  const setTplTexState = (): void => {
    if (tplTexState) tplTexState.textContent = tplTexBase64 ? `已设置（${tplTexColor}）` : "未设置（用模板自带贴图）";
  };
  setTplTexState();

  document.getElementById("cr-tpl-tex")?.addEventListener("click", () => {
    const rname =
      (document.getElementById("cr-rec-name") as HTMLInputElement)?.value.trim() || recipeName || "Filling";
    void import("./textureEditor").then((m) =>
      m.openTextureEditor({
        fileName: `${rname}_Tex.png`,
        templateId: tplMeshId || presets?.templateMeshId || "FriedFishCake",
        initialColor: tplTexColor,
        onDone: (res) => {
          tplTexBase64 = res.base64;
          tplTexColor = res.color;
          setTplTexState();
          setStatus("贴图已设置，保存时会随模板网格一起写入。");
        },
      })
    );
  });

  document.getElementById("cr-tpl-preview")?.addEventListener("click", () => {
    void (async () => {
      const id = tplMeshId || presets?.templateMeshId || "FriedFishCake";
      try {
        const [{ fetchTemplateMeshBuffer, previewLocalModel }] = await Promise.all([
          import("./recipeModelPreview"),
        ]);
        const buffer = await fetchTemplateMeshBuffer(id);
        const textures: File[] = [];
        if (tplTexBase64) {
          const bin = atob(tplTexBase64);
          const arr = new Uint8Array(bin.length);
          for (let i = 0; i < bin.length; i++) arr[i] = bin.charCodeAt(i);
          textures.push(new File([arr], "tex.png", { type: "image/png" }));
        }
        await previewLocalModel({
          title: `模板模型预览 · ${id}`,
          buffer,
          fileName: `${id}.fbx`,
          textures,
        });
      } catch (e) {
        setStatus((e as Error).message || "模板预览失败。", false);
      }
    })();
  });

  // ---- 交互 ----

  function updateTypeFields(): void {
    // 类型只影响预览图标与保存字段，烹饪/搅拌参数始终同时展示
    renderPreview();
  }

  document.getElementById("cr-add-comp")?.addEventListener("click", () => openCompositionPicker());

  const modelFileInput = document.getElementById("cr-model-file") as HTMLInputElement | null;
  const modelTextureInput = document.getElementById("cr-model-texture") as HTMLInputElement | null;

  /** 模型变换（缩放/三轴旋转/三轴位置 + 原点偏移 pivot）：仅在 3D 预览内调整
   *  （「👁 预览并调整方向/大小」），保存时提交，不在表单直接设置。 */
  interface ModelTransform {
    scale: number;
    rotationX: number;
    rotationY: number;
    rotationZ: number;
    positionX: number;
    positionY: number;
    positionZ: number;
    pivotX: number;
    pivotY: number;
    pivotZ: number;
  }

  /** 当前模型变换状态（从已保存菜谱初始化，预览调整后更新）。 */
  const modelT: ModelTransform = {
    scale: recipe?.modelScale ?? 1,
    rotationX: recipe?.modelRotationX ?? 0,
    rotationY: recipe?.modelRotationY ?? 0,
    rotationZ: recipe?.modelRotationZ ?? 0,
    positionX: recipe?.modelPositionX ?? 0,
    positionY: recipe?.modelPositionY ?? 0,
    positionZ: recipe?.modelPositionZ ?? 0,
    pivotX: recipe?.modelPivotX ?? 0,
    pivotY: recipe?.modelPivotY ?? 0,
    pivotZ: recipe?.modelPivotZ ?? 0,
  };
  /** 最近一次上传返回的 Unity 实际原始尺寸（新菜谱无已保存 bounds 时供状态显示）。 */
  let lastRawSize: { x: number; y: number; z: number } | null = null;

  const readModelTransform = (): ModelTransform => ({ ...modelT });

  const writeModelTransform = (t: ModelTransform): void => {
    Object.assign(modelT, t);
  };

  /** 按 Unity 实际导入尺寸（菜谱摘要的包围盒 ÷ 已保存缩放）反推模型原始尺寸，
   *  供自动适配校准（three.js 预览尺寸可能与 Unity 不一致）。 */
  const computeUnitySize = (): { x: number; y: number; z: number; minY: number } | undefined => {
    return rawSizeOf();
  };

  /** 选择文件后待提交的模型数据（保存时 FBX 会改写内部贴图引用、OBJ 会改写 mtllib/map_* 引用再上传）；null = 未选择新模型。 */
  let rawModel: Uint8Array | null = null;
  let rawModelName = "";
  /** OBJ 的 MTL 文本（与 .obj 同时选择时读取；可选）。 */
  let rawMtl: string | null = null;
  /** OBJ 附带选择的贴图（不占贴图格子）：原名 → 落盘名（sanitizeUploadFileName）。 */
  let rawModelImages: { orig: string; disk: string; bytes: Uint8Array; file: File }[] = [];
  /** 补充的贴图：按用途类别（base_color 必传，其余可选），点击格子选择/替换。 */
  type TexClass = import("./fbxTextureRename").TextureClass;
  interface PendingTexture { file: File; bytes: Uint8Array; url: string }
  const pendingTextures: Partial<Record<TexClass, PendingTexture>> = {};
  let currentTexSlot: TexClass | null = null;

  const writeAdjustBack = (t: ModelTransform): void => {
    writeModelTransform(t);
  };

  /** 贴图落盘文件名：{菜名}_{类别}{扩展名}（与后端 SanitizeUploadFileName 结果一致）。 */
  const texDiskName = (recipeId: string, cls: TexClass, fileName: string): string => {
    const m = /\.([^.]+)$/.exec(fileName);
    let ext = m ? "." + m[1].toLowerCase() : ".png";
    if (ext !== ".png" && ext !== ".jpg" && ext !== ".jpeg") ext = ".png";
    return `${recipeId}_${cls}${ext}`;
  };

  /** 当前各类别贴图的落盘文件名映射（菜名标识符为文件名前缀）。 */
  const texDiskNames = (): Partial<Record<TexClass, string>> => {
    const rname = (document.getElementById("cr-rec-name") as HTMLInputElement | null)?.value.trim() || "texture";
    const out: Partial<Record<TexClass, string>> = {};
    for (const cls of Object.keys(pendingTextures) as TexClass[]) {
      const t = pendingTextures[cls];
      if (t) out[cls] = texDiskName(rname, cls, t.file.name);
    }
    return out;
  };

  /** 从文件选择中提取：主模型（.fbx/.obj，取第一个）、MTL、附带图片，随后自动打开 3D 预览。 */
  const collectModelFiles = (files: FileList | null): void => {
    if (!files || files.length === 0) return;
    const list = Array.from(files);
    const model = list.find((f) => /\.(fbx|obj)$/i.test(f.name));
    const mtl = list.find((f) => /\.mtl$/i.test(f.name));
    if (!model) {
      setStatus("请选择 FBX 或 OBJ 模型文件。", false);
      return;
    }
    void withBusy("正在读取模型文件…", async () => {
      rawModel = new Uint8Array(await model.arrayBuffer());
      rawModelName = model.name;
      rawMtl = mtl ? new TextDecoder("utf-8").decode(await mtl.arrayBuffer()) : null;
      rawModelImages = [];
      for (const f of list) {
        if (f === model || f === mtl || !/\.(png|jpg|jpeg)$/i.test(f.name)) continue;
        const disk = sanitizeUploadFileName(f.name);
        if (!disk) continue;
        rawModelImages.push({ orig: f.name, disk, bytes: new Uint8Array(await f.arrayBuffer()), file: f });
      }
      openLocalPreview();
    });
  };

  /** 有补充贴图时改写模型内部贴图引用名为落盘文件名：
   *  FBX → fbxTextureRename（二进制改写）；OBJ → 改写 MTL map_* 引用 + OBJ mtllib。 */
  const buildRenamedModel = async (): Promise<{ bytes: Uint8Array; mtlText: string | null; renamed: number; fbx: boolean } | null> => {
    if (!rawModel) return null;
    const isFbx = /\.fbx$/i.test(rawModelName);
    const diskNames = texDiskNames();
    if (isFbx) {
      const { renameFbxTextureRefs } = await import("./fbxTextureRename");
      const r = renameFbxTextureRefs(rawModel, diskNames);
      return { bytes: r.bytes, mtlText: null, renamed: r.renamed, fbx: true };
    }
    // OBJ：MTL 引用改写（附带图片原名→落盘名；贴图格子类别→落盘名）+ mtllib 指向 {菜名}.mtl
    const id = (document.getElementById("cr-rec-name") as HTMLInputElement | null)?.value.trim() || "texture";
    let mtlText = rawMtl ?? null;
    let renamed = 0;
    if (mtlText != null) {
      const extra: Record<string, string> = {};
      for (const img of rawModelImages) extra[img.orig] = img.disk;
      const r = renameMtlTextureRefs(mtlText, diskNames, extra);
      mtlText = r.mtl;
      renamed += r.renamed;
    }
    const text = new TextDecoder("utf-8").decode(rawModel);
    const objText = rawMtl != null ? ensureObjMtllib(text, id + ".mtl") : text;
    if (objText !== text) renamed += 1;
    return { bytes: new TextEncoder().encode(objText), mtlText, renamed, fbx: false };
  };

  /** 选择 FBX/OBJ / 补充贴图后：加载中 → 自动打开 3D 预览（贴图作为材质注入）。 */
  const openLocalPreview = (): void => {
    const model = rawModel;
    if (!model) return;
    void withBusy("正在加载 3D 预览…", async () => {
      const baseTex = pendingTextures.base_color;
      const { openModelPreview } = await import("./modelPreview");
      const injectFiles = [...(baseTex ? [baseTex.file] : []), ...rawModelImages.map((i) => i.file)];
      const plateSel = document.getElementById("cr-plate-step") as HTMLSelectElement | null;
      openModelPreview({
        title: rawModelName,
        resourceBase: "",
        modelFileName: rawModelName,
        localBuffer: model.buffer.slice(model.byteOffset, model.byteOffset + model.byteLength) as ArrayBuffer,
        localMtl: rawMtl ?? undefined,
        localTextures: injectFiles.length > 0 ? injectFiles : undefined,
        fitTarget: plateSel?.value === "Glass" ? "cup" : "plate",
        ...readModelTransform(),
        unitySize: computeUnitySize(),
        onAdjust: writeAdjustBack,
      });
    });
  };

  modelFileInput?.addEventListener("change", () => collectModelFiles(modelFileInput.files));

  // 补充贴图：4 个类别格子，点击选择/替换该类别贴图；已选 FBX 时自动重新预览上色
  document.querySelectorAll<HTMLButtonElement>(".cr-tex-slot").forEach((btn) => {
    btn.addEventListener("click", () => {
      currentTexSlot = (btn.dataset.tex as TexClass) ?? null;
      if (!currentTexSlot || !modelTextureInput) return;
      modelTextureInput.value = "";
      modelTextureInput.click();
    });
  });
  modelTextureInput?.addEventListener("change", () => {
    const file = modelTextureInput.files?.[0];
    const cls = currentTexSlot;
    if (!file || !cls) return;
    void withBusy("正在加载贴图…", async () => {
      const old = pendingTextures[cls];
      if (old) URL.revokeObjectURL(old.url);
      const url = URL.createObjectURL(file);
      pendingTextures[cls] = { file, bytes: new Uint8Array(await file.arrayBuffer()), url };
      const thumb = document.getElementById("cr-tex-thumb-" + cls);
      if (thumb) thumb.innerHTML = `<img src="${url}" alt="${cls}">`;
      if (rawModel) openLocalPreview();
    });
  });

  document.getElementById("cr-preview-model")?.addEventListener("click", () => {
    // 若已选择新文件 → 本地预览；否则预览已保存的模型
    if (modelFileInput?.files?.[0]) {
      openLocalPreview();
      return;
    }
    if (!assetPath) return;
    const plateSel = document.getElementById("cr-plate-step") as HTMLSelectElement | null;
    void withBusy("正在加载 3D 预览（首次加载较慢）…", async () => {
      await openRecipeModelPreview(assetPath, recipeName || (assetPath.split("/").pop() ?? assetPath), {
        fitTarget: plateSel?.value === "Glass" ? "cup" : "plate",
        ...readModelTransform(),
        unitySize: computeUnitySize(),
        onAdjust: writeAdjustBack,
      });
    });
  });
  // 模型诊断：检查引用/渲染完整性/匹配链路（只读），结果用弹窗展示并支持复制
  document.getElementById("cr-diagnose")?.addEventListener("click", () => {
    if (!assetPath) {
      alert("保存菜谱后才能诊断。");
      return;
    }
    void withBusy("正在诊断…", async () => {
      try {
        const d = await api.diagnoseCustomRecipe(assetPath);
        const lines: string[] = [];
        if (d.error) lines.push(`错误：${d.error}`);
        lines.push(`模型引用：${d.modelDirect ? "model 直引 ✓" : "无直引"}${d.modelSOBased ? "（另有 modelSO）" : ""}`);
        lines.push(`模型路径：${d.modelPath || "—"}${d.modelPath ? `（${d.modelType}）` : ""}`);
        lines.push(`模型结构：${d.modelStructure || "—"}`);
        lines.push(`渲染器 ${d.rendererCount} 个 · 含网格 ${d.meshCount} 个 · 含材质 ${d.materialCount} 个（网格/材质齐全才可见）`);
        if (d.boundsSizeX || d.boundsSizeY || d.boundsSizeZ) {
          const sizes = [["X", d.boundsSizeX], ["Y", d.boundsSizeY], ["Z", d.boundsSizeZ]] as const;
          const thin = [...sizes].sort((a, b) => a[1] - b[1])[0];
          lines.push(`Unity 内模型包围盒（含变换）：X ${fmtCm(u2cm(d.boundsSizeX))} cm · Y ${fmtCm(u2cm(d.boundsSizeY))} cm · Z ${fmtCm(u2cm(d.boundsSizeZ))} cm`);
          lines.push(`薄轴 = ${thin[0]}（${fmtCm(u2cm(thin[1]))} cm）：${thin[0] === "Y" ? "模型平躺 ✓" : "模型竖立 ✗，需在预览中用「旋转 90°」或手动设 X/Z 旋转摆平"}；底面 Y = ${fmtCm(u2cm(d.boundsMinY))} cm`);
        }
        lines.push(`组成：${d.compositionCount} 项（≥1 才可装盘匹配）`);
        lines.push(`烹饪步骤：${d.cookingStepSet ? "已配置 ✓" : "未配置"} · 装盘步骤：${d.platingStepSet ? "已配置 ✓" : "未配置"}`);
        lines.push(`装盘模型 GetModel：${d.platingPrefabSet ? "非空 ✓" : "为空 ✗（游戏中会显示空盘子）"}`);
        lines.push(`变换：缩放 ${fmt4(d.modelScale)} · 旋转 X/Y/Z ${d.modelRotationX}°/${d.modelRotationY}°/${d.modelRotationZ}° · 位置 ${fmtCm(u2cm(d.modelPositionX))}/${fmtCm(u2cm(d.modelPositionY))}/${fmtCm(u2cm(d.modelPositionZ))} cm（1 单位 = 100 cm）`);
        openDiagnoseModal(lines);
      } catch (e) {
        openDiagnoseModal([`诊断失败：${(e as Error).message || String(e)}`]);
      }
    });
  });

  /** 装盘链路诊断结果弹窗：支持一键复制全部数据。 */
  function openDiagnoseModal(lines: string[]): void {
    const text = lines.join("\n");
    const body = `<div class="diag-scroll"><pre class="diag-text">${esc(text)}</pre></div>`;
    openModal(
      "装盘链路诊断",
      body,
      `<button type="button" class="m-btn" id="diag-copy">📋 复制链路数据</button>
       <button type="button" class="m-btn primary" data-cancel>关闭</button>`
    );
    document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
    const copyBtn = document.getElementById("diag-copy");
    copyBtn?.addEventListener("click", async () => {
      let ok = false;
      try {
        await navigator.clipboard.writeText(text);
        ok = true;
      } catch {
        // 非安全上下文（IP 访问）无 navigator.clipboard：退回 execCommand 方案
        try {
          const ta = document.createElement("textarea");
          ta.value = text;
          ta.style.position = "fixed";
          ta.style.opacity = "0";
          document.body.appendChild(ta);
          ta.select();
          ok = document.execCommand("copy");
          document.body.removeChild(ta);
        } catch {
          ok = false;
        }
      }
      copyBtn.textContent = ok ? "已复制 ✓" : "复制失败";
      setTimeout(() => {
        if (copyBtn) copyBtn.textContent = "📋 复制链路数据";
      }, 2000);
    });
  }
  document.getElementById("cr-type")?.addEventListener("change", updateTypeFields);
  document.getElementById("cr-score")?.addEventListener("input", renderPreview);
  document.getElementById("cr-zh")?.addEventListener("input", renderPreview);
  document.getElementById("cr-en")?.addEventListener("input", renderPreview);
  document.getElementById("cr-rec-name")?.addEventListener("input", renderPreview);
  document.getElementById("cr-cook-step")?.addEventListener("change", renderPreview);
  // 上传图标后立即在组装预览中显示
  document.getElementById("cr-icon-upload")?.addEventListener("change", (e) => {
    if (iconPreviewUrl) {
      URL.revokeObjectURL(iconPreviewUrl);
      iconPreviewUrl = null;
    }
    const file = (e.target as HTMLInputElement).files?.[0];
    if (file) iconPreviewUrl = URL.createObjectURL(file);
    renderPreview();
  });
  updateTypeFields();

  document.getElementById("cr-new-cat-inline")?.addEventListener("click", () => openNewCategoryModalInline(setName, async (newId) => {
    config = await api.fetchCustomRecipeConfig(setName);
    const sel = document.getElementById("cr-type-cat") as HTMLSelectElement;
    if (sel && newId) {
      sel.innerHTML = config.categories.map((c) => `<option value="${esc(c.id)}">${esc(c.zh || c.id)}</option>`).join("");
      sel.value = newId;
    }
  }));

  /** 返回目标：夹心工作台等外部调用方可用 presets.onBack 接管；缺省回菜谱列表。 */
  const goBack = (): void => {
    if (presets?.onBack) presets.onBack();
    else void renderRecipeList(app, setName);
  };
  document.getElementById("cr-form-back")?.addEventListener("click", goBack);
  document.getElementById("cr-form-back2")?.addEventListener("click", goBack);

  const doSave = async (): Promise<void> => {
    const rname = (document.getElementById("cr-rec-name") as HTMLInputElement).value.trim();
    if (!rname) return alert("请填写标识符");
    if (!IDENT_RE.test(rname)) return alert("标识符仅允许英文字母/数字/下划线，且不能以数字开头");

    const typeVal = (document.getElementById("cr-type") as HTMLSelectElement).value;
    const savedT = readModelTransform();
    const dto: CustomRecipeEdit = {
      setName: isEdit ? undefined : setName,
      assetPath: isEdit ? assetPath! : undefined,
      recipeName: rname,
      nameZh: (document.getElementById("cr-zh") as HTMLInputElement).value.trim(),
      nameEn: (document.getElementById("cr-en") as HTMLInputElement).value.trim(),
      category: (document.getElementById("cr-type-cat") as HTMLSelectElement).value,
      score: Number((document.getElementById("cr-score") as HTMLInputElement).value) || 0,
      type: typeVal,
      compositionIds: expandSelection(),
      cookingStepId: typeVal === "Composite" ? "" : (document.getElementById("cr-cook-step") as HTMLSelectElement)?.value ?? "",
      cookingStepIconId: (document.getElementById("cr-cook-icon") as HTMLSelectElement)?.value ?? "",
      platingStepId: (document.getElementById("cr-plate-step") as HTMLSelectElement)?.value ?? "",
      mixingIconId: (document.getElementById("cr-mix-icon") as HTMLSelectElement)?.value ?? "",
      modelPrefabId: (document.getElementById("cr-model-ref") as HTMLSelectElement)?.value ?? "",
      cookingProgress: Number((document.getElementById("cr-cook-prog") as HTMLSelectElement)?.value ?? "1") || 1,
      mixingProgress: Number((document.getElementById("cr-mix-prog") as HTMLSelectElement)?.value ?? "1") || 1,
      modelScale: savedT.scale,
      modelRotationX: savedT.rotationX,
      modelRotationY: savedT.rotationY,
      modelRotationZ: savedT.rotationZ,
      modelPositionX: savedT.positionX,
      modelPositionY: savedT.positionY,
      modelPositionZ: savedT.positionZ,
      modelPivotX: savedT.pivotX,
      modelPivotY: savedT.pivotY,
      modelPivotZ: savedT.pivotZ,
    };

    showBusy("保存中…");
    try {
      if (isEdit) {
        await api.updateCustomRecipe(dto);
      } else {
        await api.createCustomRecipe(dto);
      }

      const actualPath = assetPath || `Assets/LevelSets/${setName}/custom_recipes/${dto.category}/${rname}.asset`;

      const iconFile = (document.getElementById("cr-icon-upload") as HTMLInputElement).files?.[0];
      if (iconFile) {
        const base64 = await fileToBase64(iconFile);
        await api.uploadCustomRecipeIcon(setName, actualPath, iconFile.name, base64);
      }

      const modelFiles = (document.getElementById("cr-model-file") as HTMLInputElement).files;
      const modelFile =
        modelFiles && modelFiles.length > 0
          ? Array.from(modelFiles).find((f) => /\.(fbx|obj)$/i.test(f.name))
          : undefined;

      // 零建模通道：没传自己的模型但选了模板网格 → 后端拷贝一份模板 FBX 字节到本菜谱
      // models 目录并改名（模板只读、不拷 .meta），贴图走同一套上传管线。
      if (!modelFile && tplMeshId) {
        const tplUploads: { fileName: string; base64: string }[] = [];
        if (tplTexBase64) tplUploads.push({ fileName: `${rname}_Tex.png`, base64: tplTexBase64 });
        const diskNames0 = texDiskNames();
        for (const cls of Object.keys(pendingTextures) as TexClass[]) {
          const t = pendingTextures[cls];
          const diskName = diskNames0[cls];
          if (t && diskName) tplUploads.push({ fileName: diskName, base64: await bytesToBase64(t.bytes) });
        }
        const tplRes = await api.uploadCustomRecipeTemplateModel(setName, actualPath, tplMeshId, tplUploads);
        if (tplRes && tplRes.rawSizeX != null && tplRes.rawSizeZ != null) {
          lastRawSize = { x: tplRes.rawSizeX, y: tplRes.rawSizeY ?? 0, z: tplRes.rawSizeZ };
        }
        setStatus(`已用模板网格「${tplMeshId}」生成模型${tplTexBase64 ? "（含自制贴图）" : ""}。`);
      }

      if (modelFile) {
        const isFbx = /\.fbx$/i.test(modelFile.name);
        // 选择文件后尚未预览过（如直接点保存）时补读模型与 MTL
        if (!rawModel || !rawModelName) {
          rawModel = new Uint8Array(await modelFile.arrayBuffer());
          rawModelName = modelFile.name;
          const mtlF = modelFiles ? Array.from(modelFiles).find((f) => /\.mtl$/i.test(f.name)) : undefined;
          rawMtl = mtlF ? new TextDecoder("utf-8").decode(await mtlF.arrayBuffer()) : null;
        }
        // FBX 需 base_color 彩色贴图（否则 Unity 丢失贴图索引、模型灰色）；OBJ 由 MTL 定义材质，不强制
        if (isFbx && !pendingTextures.base_color) {
          setStatus("请先在 base_color 格子中选择彩色贴图。", false);
          return;
        }
        // 模型改名 + 贴图引用名改写（FBX 二进制 / OBJ MTL 文本），再连同贴图一起上传；
        // Unity 导入时按引用名自动把贴图链接进内嵌材质（无需额外材质球）。
        const renamedRes = (await buildRenamedModel()) ?? { bytes: rawModel, mtlText: rawMtl, renamed: 0, fbx: isFbx };
        if (renamedRes.renamed === 0 && (isFbx || rawMtl)) {
          alert(
            isFbx
              ? "警告：FBX 中未找到可改写的贴图引用（可能不是二进制 FBX 或不含贴图引用），贴图引用名未修改。"
              : "警告：OBJ/MTL 中未找到可改写的贴图引用，贴图引用名未修改。"
          );
        }
        const uploads = [{ fileName: rname + (isFbx ? ".fbx" : ".obj"), base64: await bytesToBase64(renamedRes.bytes) }];
        if (!isFbx && renamedRes.mtlText != null) {
          uploads.push({ fileName: rname + ".mtl", base64: await bytesToBase64(new TextEncoder().encode(renamedRes.mtlText)) });
        }
        const diskNames = texDiskNames();
        for (const cls of Object.keys(pendingTextures) as TexClass[]) {
          const t = pendingTextures[cls];
          const diskName = diskNames[cls];
          if (t && diskName) uploads.push({ fileName: diskName, base64: await bytesToBase64(t.bytes) });
        }
        for (const img of rawModelImages) {
          uploads.push({ fileName: img.disk, base64: await bytesToBase64(img.bytes) });
        }
        const uploadRes = await api.uploadCustomRecipeModelFiles(setName, actualPath, uploads);
        // 上传成功后按 Unity 实际导入尺寸自动校准缩放/位置（three.js 预览尺寸可能与 Unity 相差百倍；
        // 目标足迹：盘子 85 cm（对齐官方蛋炒饭参考 82~87 cm，游戏盘子直径 100 cm）、杯子 37 cm；
        // 以容器中心为原点：盘子承物面在中心上方 4.77 cm、杯内底在中心下方 23.8 cm（模型底面放过去）
        if (uploadRes && uploadRes.rawSizeX != null && uploadRes.rawSizeZ != null) {
          lastRawSize = { x: uploadRes.rawSizeX, y: uploadRes.rawSizeY ?? 0, z: uploadRes.rawSizeZ };
          const footprint = Math.max(Math.abs(uploadRes.rawSizeX), Math.abs(uploadRes.rawSizeZ)) || 1;
          const isGlass = (document.getElementById("cr-plate-step") as HTMLSelectElement)?.value === "Glass";
          const target = isGlass ? 0.37 : 0.85;
          const calScale = Math.max(1e-6, target / footprint);
          // Y 恒为 0（不自动下沉），高度在预览里手动调整
          const calPosY = 0;
          const calT = { ...savedT, scale: calScale, positionX: 0, positionY: calPosY, positionZ: 0 };
          writeModelTransform(calT);
          // 校准值重新保存（菜谱已创建/更新，直接按资产路径再更新一次变换）
          await api.updateCustomRecipe({
            ...dto,
            setName: undefined,
            assetPath: actualPath,
            modelScale: calT.scale,
            modelRotationX: calT.rotationX,
            modelRotationY: calT.rotationY,
            modelRotationZ: calT.rotationZ,
            modelPositionX: calT.positionX,
            modelPositionY: calT.positionY,
            modelPositionZ: calT.positionZ,
            modelPivotX: calT.pivotX,
            modelPivotY: calT.pivotY,
            modelPivotZ: calT.pivotZ,
          });
          setStatus(
            `已按 Unity 实际尺寸自动校准：尺寸 ${fmtCm(u2cm(target))} cm（缩放 ${fmt4(calScale)}）· 位置Y ${fmtCm(u2cm(calPosY))} cm（${isGlass ? "杯子" : "盘子"}参照；可重新打开预览微调，游戏内直接生效）`
          );
        }
      }

      const finalT = readModelTransform();
      setStatus(
        `${isEdit ? "已更新菜谱" : "已创建菜谱"} · 模型变换已保存：足迹 ${fmtCm(u2cm((footprintOf(lastRawSize ?? rawSizeOf() ?? { x: 1, y: 1, z: 1 }) || 1) * finalT.scale))} cm · 旋转 ${finalT.rotationX}°/${finalT.rotationY}°/${finalT.rotationZ}° · 位置 ${fmtCm(u2cm(finalT.positionX))}/${fmtCm(u2cm(finalT.positionY))}/${fmtCm(u2cm(finalT.positionZ))} cm（重新打开可回显，游戏内直接生效）`
      );
      // 模型已在选择文件时预览并调整过，保存后直接返回列表（分类过滤跟随本菜谱实际保存的分类）
      lastActiveCategoryId = dto.category;
      goBack();
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  };

  document.getElementById("cr-form-save")?.addEventListener("click", () => void doSave());
  document.getElementById("cr-form-save2")?.addEventListener("click", () => void doSave());

  function fileToBase64(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => {
        const result = reader.result as string;
        const comma = result.indexOf(",");
        resolve(comma >= 0 ? result.substring(comma + 1) : result);
      };
      reader.onerror = reject;
      reader.readAsDataURL(file);
    });
  }

  function bytesToBase64(bytes: Uint8Array): Promise<string> {
    let binary = "";
    const chunk = 0x8000;
    for (let i = 0; i < bytes.length; i += chunk) {
      binary += String.fromCharCode(...bytes.subarray(i, i + chunk));
    }
    return Promise.resolve(btoa(binary));
  }
}

// ==================== Delete Confirmation ====================

function confirmDeleteRecipe(_app: HTMLElement, _setName: string, assetPath: string, onDone: () => void): void {
  const fileName = assetPath.split("/").pop() ?? assetPath;
  openModal(
    `删除菜谱 · ${esc(fileName)}`,
    `<p>将永久删除菜谱资源及其模型文件夹，且<b>不可恢复</b>。若其他菜谱引用了它作为子菜谱，组成将失效。</p>`,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn danger" data-ok>确认删除</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", async () => {
    showBusy("删除中…");
    try {
      await api.deleteCustomRecipe(assetPath);
      closeModal();
      setStatus("已删除菜谱");
      onDone();
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });
}

// ==================== Category Modals ====================

function openNewCategoryModal(setName: string, onDone: (id: string) => void): void {
  openModal(
    "新建分类",
    `<label class="m-field">分类ID（仅字母/数字/下划线，用于目录名）<input type="text" id="cr-cat-id" placeholder="MyCategory"></label>
     <label class="m-field">中文名<input type="text" id="cr-cat-zh" placeholder="我的分类"></label>
     <label class="m-field">英文名<input type="text" id="cr-cat-en" placeholder="My Category"></label>`,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>创建</button>`
  );
  wireModalCancelOk(closeModal, async () => {
    const id = (document.getElementById("cr-cat-id") as HTMLInputElement).value.trim();
    if (!id) return alert("请填写分类ID");
    if (!IDENT_RE.test(id)) return alert("分类ID仅允许英文字母/数字/下划线");
    const zh = (document.getElementById("cr-cat-zh") as HTMLInputElement).value.trim();
    const en = (document.getElementById("cr-cat-en") as HTMLInputElement).value.trim();
    showBusy("创建分类…");
    try {
      await api.addCustomRecipeCategory(setName, id, zh || id, en || id);
      closeModal();
      setStatus("已创建分类");
      onDone(id);
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });
}

function openNewCategoryModalInline(setName: string, onDone: (id: string) => void): void {
  const id = prompt("分类ID（仅字母/数字/下划线）：");
  if (!id || !IDENT_RE.test(id)) { alert("ID非法"); return; }
  const zh = prompt("分类中文名：") || id;
  const en = prompt("分类英文名：") || id;
  showBusy("创建分类…");
  api.addCustomRecipeCategory(setName, id, zh, en)
    .then(() => { setStatus("已创建分类"); onDone(id); })
    .catch((e) => setStatus((e as Error).message, false))
    .finally(() => hideBusy());
}

function openManageCategoriesModal(
  setName: string,
  categories: CustomRecipeCategory[],
  onDone: () => void
): void {

  function catDisplay(c: CustomRecipeCategory): string {
    return `${c.zh || c.id}${c.en ? ` (${c.en})` : ""}`;
  }

  const list = categories
    .map(
      (c) => `
    <div class="m-row" style="margin-bottom:8px">
      <span style="flex:1">${esc(catDisplay(c))} <span class="muted">[${esc(c.id)}]</span></span>
      <button class="m-btn" data-rename="${esc(c.id)}">重命名</button>
      <button class="m-btn danger" data-delcat="${esc(c.id)}">删除</button>
    </div>`
    )
    .join("");

  openModal(
    "管理分类",
    `<div class="modal-scroll">${list || '<p class="muted">暂无分类</p>'}</div>`,
    `<button type="button" class="m-btn" data-cancel>关闭</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);

  document.querySelectorAll<HTMLButtonElement>("[data-rename]").forEach((b) => {
    b.addEventListener("click", () => {
      const oldId = b.dataset.rename!;
      const cat = categories.find((c) => c.id === oldId);
      openRenameCategoryModal(setName, oldId, cat?.zh ?? oldId, cat?.en ?? oldId, () => {
        closeModal();
        onDone();
      });
    });
  });

  document.querySelectorAll<HTMLButtonElement>("[data-delcat]").forEach((b) => {
    b.addEventListener("click", () => {
      const catId = b.dataset.delcat!;
      const cat = categories.find((c) => c.id === catId);
      openModal(
        `删除分类 · ${esc(cat?.zh || catId)}`,
        `<p>将检查关卡使用情况。如果有关卡正在使用该分类的菜谱，则不允许删除。</p>`,
        `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn danger" data-ok>确认删除</button>`
      );
      document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
      document.querySelector("[data-ok]")?.addEventListener("click", async () => {
        showBusy("检查中…");
        try {
          await api.deleteCustomRecipeCategory(setName, catId);
          closeModal();
          setStatus("已删除分类");
          onDone();
        } catch (e) {
          setStatus((e as Error).message, false);
          hideBusy();
        }
      });
    });
  });
}

function openRenameCategoryModal(
  setName: string,
  oldId: string,
  oldZh: string,
  oldEn: string,
  onDone: () => void
): void {
  openModal(
    `重命名分类 · ${esc(oldZh || oldId)}`,
    `<label class="m-field">分类ID（仅字母/数字/下划线）<input type="text" id="cr-rename-id" value="${esc(oldId)}"></label>
     <label class="m-field">中文名<input type="text" id="cr-rename-zh" value="${esc(oldZh)}"></label>
     <label class="m-field">英文名<input type="text" id="cr-rename-en" value="${esc(oldEn)}"></label>`,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>确认</button>`
  );
  wireModalCancelOk(closeModal, async () => {
    const newId = (document.getElementById("cr-rename-id") as HTMLInputElement).value.trim();
    if (!newId) return alert("请填写分类ID");
    if (!IDENT_RE.test(newId)) return alert("分类ID仅允许英文字母/数字/下划线");
    const newZh = (document.getElementById("cr-rename-zh") as HTMLInputElement).value.trim();
    const newEn = (document.getElementById("cr-rename-en") as HTMLInputElement).value.trim();
    showBusy("重命名…");
    try {
      await api.renameCustomRecipeCategory(setName, oldId, newId, newZh || newId, newEn || newId);
      closeModal();
      setStatus("已重命名分类");
      onDone();
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });
}

function wireModalCancelOk(closeModal: () => void, onOk: () => void): void {
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", onOk);
}
