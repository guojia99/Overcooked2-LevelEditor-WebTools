import * as api from "./api";
import type {
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
import { foodGroupLabel } from "./ingredientLabels";
import { closeModal, openModal } from "./modals";
import { rlCardHtml, type RecipeWithGroups } from "./recipeCard";

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

const IDENT_RE = /^[A-Za-z_][A-Za-z0-9_]*$/;

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
    <div class="cr-warn-banner">⚠️ 自定义菜谱功能开发中，请勿在正式关卡中使用</div>
    <div class="manage-content" id="cr-content"></div>
  `;
  wireNav((target) => {
    if (target === "layout") {
      location.hash = "#/layout";
      location.reload();
    } else if (target === "manage") {
      location.hash = "#/manage";
      location.reload();
    } else if (target === "recipes") {
      location.href = "/recipes";
    }
  });
  return document.getElementById("cr-content")!;
}

export async function renderCustomRecipesView(app: HTMLElement): Promise<void> {
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

function foodIconImg(kind: "ingredients" | "recipes", id: string | undefined): string {
  const src = id ? `/icons/${kind}/${encodeURIComponent(id)}.png` : "/icons/_placeholder.png";
  return `<img class="food-icon" loading="lazy" src="${src}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`;
}

/** 菜谱 models 目录的 3D 资源访问基地址（目录式，FBX 贴图按相对路径拼接）。
 *  目录结构：custom_recipes/<分类>/models/（models 与菜谱资产同级，在分类目录下）。 */
function modelResourceBase(recipeAssetPath: string): string {
  const dir = recipeAssetPath.replace(/\/[^/]+\.asset$/, "") + "/models";
  const b64 = btoa(unescape(encodeURIComponent(dir)))
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/, "");
  return `/api/custom-recipes/model-files/${b64}/`;
}

/** 参考模型（披萨装盘模型，含盘子；OBJ 源文件为建模坐标，prefab 根 scale=0.7 才是游戏内尺寸）。 */
const REFERENCE_MODEL = {
  url:
    "/api/custom-recipes/reference-model?path=" +
    encodeURIComponent("Assets/common01/food/CustomRecipes/Pizza/models/plated_mushroom_01.prefab"),
  format: "obj" as const,
  scale: 0.7,
};

/** 打开菜谱 3D 模型在线预览（自动从 models 目录找 .fbx/.obj；three.js 按需加载）。 */
async function openRecipeModelPreview(
  recipeAssetPath: string,
  title: string,
  extra?: { scale?: number; rotationY?: number; onAdjust?: (scale: number, rotationY: number) => void }
): Promise<void> {
  try {
    const files = await api.fetchCustomRecipeModelFiles(recipeAssetPath);
    const model = files.find((f) => /\.(fbx|obj)$/i.test(f));
    if (!model) {
      alert("该菜谱尚未上传模型文件。");
      return;
    }
    const { openModelPreview } = await import("./modelPreview");
    const texUrls = files
      .filter((f) => /\.(png|jpg|jpeg)$/i.test(f))
      .map((f) => modelResourceBase(recipeAssetPath) + encodeURIComponent(f));
    openModelPreview({
      title,
      resourceBase: modelResourceBase(recipeAssetPath),
      modelFileName: model,
      scale: extra?.scale,
      rotationY: extra?.rotationY,
      onAdjust: extra?.onAdjust,
      remoteTextures: texUrls,
      referenceUrl: REFERENCE_MODEL.url,
      referenceFormat: REFERENCE_MODEL.format,
      referenceScale: REFERENCE_MODEL.scale,
    });
  } catch (e) {
    alert((e as Error).message || "模型预览加载失败。");
  }
}

// ==================== Recipe List ====================

interface RecipeLikeCard extends RecipeWithGroups {}

function toRecipeCard(r: CustomRecipeSummary): RecipeLikeCard {
  return {
    guid: r.guid,
    id: r.id,
    nameZh: r.nameZh,
    nameEn: r.nameEn || undefined,
    assetPath: r.assetPath,
    cookingStep: r.cookingStepId || undefined,
    ingredients: r.ingredients,
    compositionIds: r.compositionIds,
    score: r.score,
    isCustom: true,
    intermediate: r.intermediate,
    group: r.group,
    type: "custom",
    cookingGroups: r.cookingGroups,
  };
}

async function renderRecipeList(app: HTMLElement, setName: string): Promise<void> {
  const content = shell(app, `自定义菜谱 · ${esc(setName)}`);
  setBusy("加载菜谱配置…");

  let config: CustomRecipeConfig;
  let recipes: CustomRecipeSummary[];
  let ingredientNames = new Map<string, string>();
  let platingNames = new Map<string, string>();
  try {
    [config, recipes] = await Promise.all([
      api.fetchCustomRecipeConfig(setName),
      api.fetchCustomRecipes(setName),
    ]);
    const refs = await api.fetchCustomRecipeReferences(setName).catch(() => null);
    for (const c of refs?.platingContainers ?? []) platingNames.set(c.id, c.nameZh || c.id);
    let ingLoadFailed = false;
    const ings = await api.fetchIngredients().catch(() => {
      ingLoadFailed = true;
      return [] as IngredientEntry[];
    });
    for (const i of ings) ingredientNames.set(i.id, i.nameZh);
    if (ingLoadFailed) setStatus("⚠️ 食材数据加载失败（/api/catalog/ingredients），卡片可能缺少食材图标");
  } catch (e) {
    showError(e);
    return;
  }
  setStatus(`${recipes.length} 个菜谱 · UID前缀：${config.uidPrefix}`);

  const categories = config.categories ?? [];

  function catDisplay(c: CustomRecipeCategory): string {
    return c.zh || c.id;
  }

  let activeCategoryId = "";
  let searchQuery = "";
  let roleFilter: "all" | "done" | "half" = "all";

  function roleFilterClass(role: "all" | "done" | "half"): string {
    return roleFilter === role ? " active" : "";
  }
  /** 旧桥接数据无 ingredients/cookingGroups 时，用 compositionIds 反查食材名兜底。 */
  function cardRecipe(r: CustomRecipeSummary): RecipeLikeCard {
    const card = toRecipeCard(r);
    if (!card.ingredients && card.compositionIds) {
      card.ingredients = card.compositionIds.filter((id) => ingredientNames.has(id));
    }
    return card;
  }

  function filteredRecipes(): CustomRecipeSummary[] {
    let list = activeCategoryId ? recipes.filter((r) => r.category === activeCategoryId) : recipes;
    if (roleFilter === "done") list = list.filter((r) => !r.intermediate);
    else if (roleFilter === "half") list = list.filter((r) => r.intermediate);
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

    if (filtered.length === 0) {
      if (recipes.length === 0 && searchQuery === "" && roleFilter === "all" && activeCategoryId === "") {
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

    const cards = filtered
      .map((r) => {
        let cardHtml: string;
        try {
          cardHtml = rlCardHtml(cardRecipe(r), {
            allRecipes: recipes.map(cardRecipe),
            ingredientName: (id) => ingredientNames.get(id) ?? id,
            iconSrc: () => crIconSrc(r),
          });
        } catch (e) {
          cardHtml = `<div class="m-card"><h3>${esc(r.nameZh)}</h3><p class="muted">卡片渲染失败：${esc((e as Error).message)}</p></div>`;
        }
        const cat = categories.find((c) => c.id === r.category);
        const plating = r.platingStepId ? platingNames.get(r.platingStepId) : "";
        const compCount = (r.compositionIds ?? []).length;
        return `
      <div class="cr-card-wrap">
        <div class="cr-card-inner">${cardHtml}</div>
        <div class="cr-card-foot">
          <span class="cr-cat-tag">${esc(catDisplay(cat ?? { id: r.category, zh: r.category, en: r.category }))}</span>
          ${plating ? `<span class="cr-cat-tag cr-plate-tag" title="装盘容器">🍽 ${esc(plating)}</span>` : ""}
          ${r.intermediate ? '<span class="cr-cat-tag cr-half-tag">中间产物</span>' : ""}
          <span class="muted small">UID ${r.uID} · 组成 ${compCount} 项</span>
          <span style="flex:1"></span>
          ${r.hasModel ? `<button class="m-btn small" data-preview="${esc(r.assetPath)}" title="3D 模型在线预览">👁</button>` : ""}
          <button class="m-btn small" data-edit="${esc(r.assetPath)}">编辑</button>
          <button class="m-btn small danger" data-del="${esc(r.assetPath)}">删除</button>
        </div>
      </div>`;
      })
      .join("");

    return `<div class="rl-grid">${cards}</div>`;
  }

  function renderSidebar(): string {
    return `
    <div class="cr-sidebar">
      <div class="m-section-title">分类</div>
      <div class="cr-cat-list">
        <button class="m-btn cr-cat-item${activeCategoryId === "" ? " primary" : ""}" data-cat="">全部 (${recipes.length})</button>
        ${categories
          .map((c) => {
            const count = recipes.filter((r) => r.category === c.id).length;
            return `<button class="m-btn cr-cat-item${activeCategoryId === c.id ? " primary" : ""}" data-cat="${esc(c.id)}">${esc(catDisplay(c))} (${count})</button>`;
          })
          .join("")}
        <div class="cr-cat-actions">
          <button class="m-btn" id="cr-new-cat">+ 新建分类</button>
          ${categories.length > 0 ? '<button class="m-btn" id="cr-manage-cat">管理分类</button>' : ""}
        </div>
      </div>
    </div>`;
  }

  content.innerHTML = `
    <div class="m-actions-row">
      <button class="m-btn" id="cr-back">← 返回关卡集列表</button>
      <span class="muted">当前关卡集：<b>${esc(setName)}</b></span>
      <span style="flex:1"></span>
      <button class="m-btn primary" id="cr-new-recipe">+ 新建菜谱</button>
    </div>
    <div class="cr-toolbar">
      <input type="search" id="cr-search" class="rl-search" placeholder="搜索菜名 / ID / 食材…" autocomplete="off">
      <div class="ing-groups">
        <button type="button" class="cr-comp-chip${roleFilterClass("all")}" data-role="all">全部</button>
        <button type="button" class="cr-comp-chip${roleFilterClass("done")}" data-role="done">成品</button>
        <button type="button" class="cr-comp-chip${roleFilterClass("half")}" data-role="half">中间产物</button>
      </div>
    </div>
    <div class="cr-layout">
      <div id="cr-sidebar">${renderSidebar()}</div>
      <div id="cr-grid">${renderGrid()}</div>
    </div>
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
      setStatus(`${recipes.length} 个菜谱 · UID前缀：${config.uidPrefix}`);
      document.getElementById("cr-sidebar")!.innerHTML = renderSidebar();
      wireSidebar();
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
    document.querySelectorAll<HTMLButtonElement>("[data-role]").forEach((b) => {
      b.addEventListener("click", () => {
        roleFilter = (b.dataset.role as "all" | "done" | "half") ?? "all";
        document.querySelectorAll<HTMLButtonElement>("[data-role]").forEach((x) => x.classList.toggle("active", x === b));
        document.getElementById("cr-grid")!.innerHTML = renderGrid();
        wireGridButtons();
      });
    });
  }

  function wireSidebar(): void {
    document.querySelectorAll<HTMLButtonElement>(".cr-cat-item").forEach((b) => {
      b.addEventListener("click", () => {
        activeCategoryId = b.dataset.cat ?? "";
        document.getElementById("cr-sidebar")!.innerHTML = renderSidebar();
        wireSidebar();
        document.getElementById("cr-grid")!.innerHTML = renderGrid();
        wireGridButtons();
      });
    });
    document.getElementById("cr-new-cat")?.addEventListener("click", () =>
      openNewCategoryModal(setName, (newId) => {
        activeCategoryId = newId ?? "";
        // 完全重建列表页，确保新分类立即出现在侧栏
        void renderRecipeList(app, setName);
      })
    );
    document.getElementById("cr-manage-cat")?.addEventListener("click", () =>
      openManageCategoriesModal(setName, config.categories, () => void renderRecipeList(app, setName))
    );
  }

  function wireGridButtons(): void {
    document.querySelectorAll<HTMLButtonElement>("[data-edit]").forEach((b) =>
      b.addEventListener("click", () => void renderRecipeForm(app, setName, b.dataset.edit!))
    );
    document.querySelectorAll<HTMLButtonElement>("[data-preview]").forEach((b) =>
      b.addEventListener("click", () => {
        const path = b.dataset.preview!;
        void openRecipeModelPreview(path, path.split("/").pop() ?? path);
      })
    );
    document.querySelectorAll<HTMLButtonElement>("[data-del]").forEach((b) =>
      b.addEventListener("click", () => confirmDeleteRecipe(app, setName, b.dataset.del!, refreshView))
    );
  }

  wireToolbar();
  wireSidebar();
  wireGridButtons();

  document.getElementById("cr-back")?.addEventListener("click", () => void renderCustomRecipesView(app));
  document.getElementById("cr-new-recipe")?.addEventListener("click", () => void renderRecipeForm(app, setName, null));
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
  official: boolean;
  ingredients: string[];
}

async function renderRecipeForm(
  app: HTMLElement,
  setName: string,
  assetPath: string | null,
  presets?: { score?: number; category?: string }
): Promise<void> {
  const isEdit = assetPath != null;
  const content = shell(app, isEdit ? "编辑菜谱" : "新建菜谱");
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
  const subIdSet = new Set<string>();
  for (const r of levelsetRecipes) subIdSet.add(r.id);
  for (const c of catalog) if (c.isCustom) subIdSet.add(c.id);

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
  const categoryId = recipe?.category ?? presets?.category ?? (config.categories?.length > 0 ? config.categories[0].id : "");
  const score = recipe?.score ?? presets?.score ?? 0;
  const type = recipe?.type ?? (presets?.score === 0 ? "Cooked" : "Cooked");
  const cookingStepId = recipe?.cookingStepId ?? "";
  const cookingStepIconId = "";
  const platingStepId = recipe?.platingStepId ?? "";
  const mixingIconId = "";
  const modelPrefabId = "";

  const categories = config.categories ?? [];

  const ingById = new Map<string, IngredientEntry>();
  for (const i of ingredients) ingById.set(i.id, i);

  const ingLoadFailed = ingredients.length === 0;

  const subItems: SubItem[] = (() => {
    const list: SubItem[] = [];
    for (const r of levelsetRecipes) {
      if (isEdit && r.assetPath === assetPath) continue;
      list.push({
        id: r.id,
        nameZh: r.nameZh,
        nameEn: r.nameEn,
        score: r.score,
        cookingStepId: r.cookingStepId,
        hasIcon: r.hasIcon,
        assetPath: r.assetPath,
        official: false,
        ingredients: r.ingredients,
      });
    }
    const seen = new Set(list.map((s) => s.id));
    // 官方 CustomRecipeSO（含成品与中间产物，如蓝莓/草莓松饼糊、汤类成品）也可作为组成
    for (const c of catalog) {
      if (!c.isCustom || (c.group ?? "") === "levelset") continue;
      if (seen.has(c.id)) continue;
      list.push({
        id: c.id,
        nameZh: c.nameZh,
        nameEn: c.nameEn,
        score: c.score ?? 0,
        cookingStepId: c.cookingStep ?? "",
        hasIcon: !!c.icon,
        assetPath: c.assetPath,
        official: true,
        ingredients: c.ingredients ?? [],
      });
      seen.add(c.id);
    }
    list.sort(
      (a, b) =>
        a.score - b.score || Number(a.official) - Number(b.official) || a.nameZh.localeCompare(b.nameZh, "zh")
    );
    return list;
  })();

  const subById = new Map<string, SubItem>();
  for (const s of subItems) subById.set(s.id, s);

  // 供预览分组回退使用的全部菜谱（关卡集菜谱 + 目录）
  const allRecipeLikes: RecipeWithGroups[] = [
    ...levelsetRecipes.map(toRecipeCard),
    ...catalog.filter((c) => c.isCustom).map((c) => ({ ...c, isCustom: true }) as RecipeWithGroups),
  ];

  function catDisplay(c: CustomRecipeCategory): string {
    return c.zh || c.id;
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

  /** 选择弹窗内的卡片：点击选中（数量 1），已选时显示 −/＋ 数量调整。
   *  菜谱卡片按角色打标签：半成品（score=0）/ 成品菜（score>0，同样可作组成）。 */
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
      const badge = s.score <= 0
        ? '<span class="cr-badge-half">中间产物</span>'
        : `<span class="cr-badge-done">成品菜 · 可作组成</span>`;
      const src = s.official ? `/icons/recipes/${encodeURIComponent(s.id)}.png` : crIconSrc(s);
      return `<div class="pick-card cp-card${selected ? " selected" : ""}" data-cpid="${esc(id)}" data-cpsub="1" title="${esc(s.id)}">
        <span class="pc-head"><img class="food-icon" loading="lazy" src="${esc(src)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'" /><span class="pc-name">${esc(s.nameZh)}${badge}${s.nameEn ? ` <span class="muted pc-en">${esc(s.nameEn)}</span>` : ""}</span></span>
        <span class="muted small">${s.cookingStepId ? esc(s.cookingStepId) : "无烹饪步骤"} · ${(s.ingredients ?? []).length} 种食材${s.official ? " · 官方" : s.score <= 0 ? " · 本关卡集" : " · 本关卡集"}</span>
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
    let filter: "all" | "ing" | "sub" | "done" = "all";
    let q = "";

    function filteredIds(): { id: string; isSub: boolean }[] {
      const query = q.trim().toLowerCase();
      const out: { id: string; isSub: boolean }[] = [];
      if (filter === "all" || filter === "ing") {
        for (const i of ingredients) {
          if (query && !i.nameZh.toLowerCase().includes(query) && !(i.nameEn ?? "").toLowerCase().includes(query) && !i.id.toLowerCase().includes(query)) continue;
          out.push({ id: i.id, isSub: false });
        }
      }
      // 菜谱分组：中间产物（score<=0）/ 成品菜（score>0，同样可作组成）
      const recipes = subItems.filter(
        (s) => filter === "all" || (filter === "sub" ? s.score <= 0 : s.score > 0)
      );
      for (const s of recipes) {
        if (query && !s.nameZh.toLowerCase().includes(query) && !(s.nameEn ?? "").toLowerCase().includes(query) && !s.id.toLowerCase().includes(query)) continue;
        out.push({ id: s.id, isSub: true });
      }
      return out;
    }

    function bodyHtml(): string {
      const ids = filteredIds();
      const chips = [
        `<button type="button" class="cr-comp-chip${filter === "all" ? " active" : ""}" data-filter="all">全部</button>`,
        `<button type="button" class="cr-comp-chip${filter === "ing" ? " active" : ""}" data-filter="ing">食材</button>`,
        `<button type="button" class="cr-comp-chip${filter === "sub" ? " active" : ""}" data-filter="sub">中间产物</button>`,
        `<button type="button" class="cr-comp-chip${filter === "done" ? " active" : ""}" data-filter="done">成品菜</button>`,
      ].join("");
      const loadWarn = ingLoadFailed && filter !== "sub" && filter !== "done"
        ? '<p class="mp-status err">⚠️ 未加载到食材数据（桥接 /api/catalog/ingredients 异常），请刷新重试或检查 Unity 桥。</p>'
        : "";
      // #cp-grid 始终保留（搜索时只更新其内部），保证点击委托不丢失。
      return `
        <div class="cr-comp-toolbar">
          <input type="search" id="cp-search" class="ing-search" placeholder="搜索名称 / ID…" autocomplete="off">
          <div class="ing-groups">${chips}</div>
        </div>
        ${loadWarn}
        <div class="modal-scroll" id="cp-scroll">
          <div class="pick-grid" id="cp-grid">${ids.map((x) => pickerCardHtml(x.id, x.isSub, draft.get(x.id) ?? 0)).join("")}</div>
          ${ids.length ? "" : '<p class="muted">没有匹配的项</p>'}
        </div>`;
    }

    openModal(
      "添加食材 / 菜谱",
      `<p class="modal-hint">点击卡片加入（默认 1 份），再次点击 − / ＋ 调整数量；同一项可多次使用。<b>任何菜谱（成品菜或中间产物）都能作为本菜谱的组成</b>，如鸡蛋汉堡 = 煎蛋（成品菜）+ 面包。</p>
       <div id="cp-body">${bodyHtml()}</div>`,
      `<button type="button" class="m-btn" data-cancel>取消</button>
       <button type="button" class="m-btn primary" data-ok>确定</button>`
    );
    const panel = document.querySelector(".modal-panel");
    if (panel) panel.classList.add("wide");

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
          grid.innerHTML = filteredIds()
            .map((x) => pickerCardHtml(x.id, x.isSub, draft.get(x.id) ?? 0))
            .join("");
          const scroll = document.getElementById("cp-scroll");
          if (scroll) {
            const empty = scroll.querySelector("p.muted");
            if (empty) empty.remove();
            if (filteredIds().length === 0) {
              scroll.insertAdjacentHTML("beforeend", '<p class="muted">没有匹配的项</p>');
            }
          }
        }
      });
      document.querySelectorAll<HTMLButtonElement>("#cp-body .cr-comp-chip").forEach((b) => {
        b.addEventListener("click", () => {
          filter = (b.dataset.filter as "all" | "ing" | "sub") ?? "all";
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
          // 点击卡片主体：选中/取消
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
      const badge = (s?.score ?? 0) <= 0 ? '<span class="cr-chip-tag">中间产物</span>' : '<span class="cr-chip-tag">成品菜</span>';
      rows.push(`<div class="cr-comp-row cr-comp-row-sub" data-rowid="sub:${esc(id)}">
        ${s ? `<img class="food-icon" loading="lazy" src="${esc(s.official ? `/icons/recipes/${encodeURIComponent(s.id)}.png` : crIconSrc(s))}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'" />` : ""}
        <span class="cr-row-name">${esc(s?.nameZh ?? id)}${badge}</span>
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
      : '<p class="muted small" style="margin:4px 0">尚未选择。点击下方「添加」选择食材或中间产物。</p>';

    const total = expandSelection().length;
    const hint = document.getElementById("cr-comp-hint");
    if (hint) {
      hint.textContent = rows.length
        ? `共 ${total} 份组成（食材 ${[...selectedIng.values()].reduce((a, b) => a + b, 0)} · 中间产物 ${[...selectedSub.values()].reduce((a, b) => a + b, 0)}）`
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
    const cookStep = typeVal === "Composite" ? "" : (document.getElementById("cr-cook-step") as HTMLSelectElement)?.value ?? "";
    const compIds = expandSelection();
    const preview: RecipeWithGroups = {
      guid: "",
      id: rname || "preview",
      nameZh: zh || rname || "未命名菜谱",
      nameEn: en || undefined,
      assetPath: "",
      isCustom: true,
      group: "levelset",
      score: scoreVal,
      intermediate: scoreVal <= 0,
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
          <label class="m-field">类型
            <select id="cr-type" class="m-select">
              <option value="Composite" ${type === "Composite" ? "selected" : ""}>Composite（组合）</option>
              <option value="Cooked" ${type === "Cooked" ? "selected" : ""}>Cooked（烹饪）</option>
              <option value="Mixed" ${type === "Mixed" ? "selected" : ""}>Mixed（搅拌）</option>
            </select>
          </label>
          <label class="m-field">分数<input type="number" id="cr-score" value="${score}" min="0">
            <span class="muted small">0 = 中间产物（不直接上桌，可被其他菜谱引用）</span>
          </label>
          <label class="m-field">UID（自动生成）<input type="text" value="${recipe?.uID ?? (isNew ? config.uidPrefix * 1000 + config.nextSequence : "—")}" disabled></label>
          <label class="m-field">菜谱图标（PNG，卡片图）<input type="file" id="cr-icon-upload" accept="image/png">
            <span class="muted small">上传后立即在组装预览中显示</span></label>
        </div>
      </div>
      <div class="cr-section">
        <div class="m-section-title">组成（食材 / 菜谱）</div>
        <p class="modal-hint">食材直接选用；<b>其他菜谱（成品菜或中间产物）也可作为本菜谱的组成工序</b>（如鸡蛋汉堡 = 煎蛋 + 面包 + 生菜）。点击「添加」弹出选择器。</p>
        <div class="cr-comp-list" id="cr-comp-list"></div>
        <div class="cr-comp-toolbar" style="margin-top:10px">
          <button type="button" class="m-btn primary" id="cr-add-comp">＋ 添加食材 / 菜谱</button>
          <button type="button" class="m-btn" id="cr-new-sub">＋ 新建中间产物</button>
          <span class="muted small" id="cr-comp-hint" style="margin-left:auto"></span>
        </div>
      </div>
      <div class="cr-section">
        <div class="m-section-title">烹饪与装盘</div>
        <div class="cr-form-grid">
          <label class="m-field" id="cr-cook-step-field">烹饪步骤 ${selectHtml(refs.cookingSteps, cookingStepId, "cr-cook-step")}</label>
          <label class="m-field">烹饪图标 ${selectHtml(refs.icons, cookingStepIconId, "cr-cook-icon")}</label>
          <label class="m-field" id="cr-cook-prog-field">烹饪程度
            <select id="cr-cook-prog" class="m-select">
              <option value="0">Raw（生）</option>
              <option value="1" selected>Cooked（熟）</option>
              <option value="2">Burnt（焦）</option>
            </select>
          </label>
          <label class="m-field">装盘容器 ${selectHtml(
            refs.platingContainers?.length
              ? refs.platingContainers
              : (refs.platingSteps ?? []).filter((e) => e.id === "Plate" || e.id === "Glass"),
            platingStepId,
            "cr-plate-step"
          )}
            <span class="muted small">决定上桌容器（盘子/杯子），运行时映射为 PlatingStepData</span>
          </label>
          <label class="m-field" id="cr-mix-icon-field" style="display:none">搅拌图标 ${selectHtml(refs.icons, mixingIconId, "cr-mix-icon")}</label>
          <label class="m-field" id="cr-mix-prog-field" style="display:none">搅拌程度
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
        <p class="modal-hint">上传 <b>FBX 模型</b>（可不含材质，单独上传即可）；需要彩色时<b>补充贴图</b>：<b>base_color 彩色贴图必传</b>，roughness/metallic/normal 可选。保存时贴图会重命名为 <code>{菜名}_base_color.png</code> 等格式，并把 FBX 内部对应的贴图引用名一并改写，Unity 导入即可自动链接贴图（与美术直接拖 FBX + 贴图使用一致）。选择文件后立即打开 3D 预览，调整方向/大小后保存提交。</p>
        <div class="cr-form-grid">
          <label class="m-field">上传 3D 模型（仅 FBX）<input type="file" id="cr-model-file" accept=".fbx">
            <span class="muted small">可单独上传；若预览显示为灰色，说明 FBX 未内嵌贴图，可补充贴图</span></label>
          <label class="m-field">复用已有模型 ${selectHtml(refs.reusableModels, modelPrefabId, "cr-model-ref")}</label>
          <label class="m-field">模型缩放<input type="number" id="cr-model-scale" value="${recipe?.modelScale ?? 1}" min="0.01" step="0.05">
            <span class="muted small">实际游戏中的大小（相对原模型尺寸）</span></label>
          <label class="m-field">Y 轴旋转（度）<input type="number" id="cr-model-rot" value="${recipe?.modelRotationY ?? 0}" step="5">
            <span class="muted small">修正模型朝向（如煎蛋面朝上）</span></label>
          <label class="m-field">在线预览<button type="button" class="m-btn" id="cr-preview-model">👁 预览并调整方向/大小</button>
            <span class="muted small">已保存的模型可随时预览调整；上传新文件则自动预览</span></label>
        </div>
        <div class="m-field cr-tex-field">补充贴图（点击格子选择/替换；base_color 必传）
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
      </div>
    </div>
  `;

  renderPreview();
  renderCompList();

  // ---- 交互 ----

  function updateTypeFields(): void {
    const sel = (document.getElementById("cr-type") as HTMLSelectElement)?.value ?? "Composite";
    const cookStepField = document.getElementById("cr-cook-step-field");
    const cookProgField = document.getElementById("cr-cook-prog-field");
    const mixIconField = document.getElementById("cr-mix-icon-field");
    const mixProgField = document.getElementById("cr-mix-prog-field");
    if (cookStepField) cookStepField.style.display = sel === "Composite" ? "none" : "";
    if (cookProgField) cookProgField.style.display = sel === "Composite" ? "none" : "";
    if (mixIconField) mixIconField.style.display = sel === "Mixed" ? "" : "none";
    if (mixProgField) mixProgField.style.display = sel === "Mixed" ? "" : "none";
    renderPreview();
  }

  document.getElementById("cr-add-comp")?.addEventListener("click", () => openCompositionPicker());

  const modelScaleInput = document.getElementById("cr-model-scale") as HTMLInputElement | null;
  const modelRotInput = document.getElementById("cr-model-rot") as HTMLInputElement | null;
  const modelFileInput = document.getElementById("cr-model-file") as HTMLInputElement | null;
  const modelTextureInput = document.getElementById("cr-model-texture") as HTMLInputElement | null;

  /** 选择文件后待提交的原始 FBX（保存时若有贴图会先改写内部贴图引用名再上传）；null = 未选择新模型。 */
  let rawFbx: Uint8Array | null = null;
  let rawFbxName = "";
  /** 补充的贴图：按用途类别（base_color 必传，其余可选），点击格子选择/替换。 */
  type TexClass = import("./fbxTextureRename").TextureClass;
  interface PendingTexture { file: File; bytes: Uint8Array; url: string }
  const pendingTextures: Partial<Record<TexClass, PendingTexture>> = {};
  let currentTexSlot: TexClass | null = null;

  const writeAdjustBack = (s: number, r: number): void => {
    if (modelScaleInput) modelScaleInput.value = String(Math.round(s * 100) / 100);
    if (modelRotInput) modelRotInput.value = String(Math.round(r));
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

  /** 有补充贴图时改写 FBX 内部贴图引用名为落盘文件名（浏览器端 fbxTextureRename）。 */
  const buildRenamedFbx = async (): Promise<{ bytes: Uint8Array; renamed: number } | null> => {
    if (!rawFbx) return null;
    const { renameFbxTextureRefs } = await import("./fbxTextureRename");
    return renameFbxTextureRefs(rawFbx, texDiskNames());
  };

  /** 选择 FBX / 补充贴图后：加载中 → 自动打开 3D 预览（贴图作为材质注入）。 */
  const openLocalPreview = (): void => {
    const f = modelFileInput?.files?.[0];
    if (!f && !rawFbx) return;
    void withBusy("正在加载 3D 预览…", async () => {
      if (f) {
        rawFbx = new Uint8Array(await f.arrayBuffer());
        rawFbxName = f.name;
      }
      const renamed = await buildRenamedFbx();
      if (!renamed) return;
      const baseTex = pendingTextures.base_color;
      const { openModelPreview } = await import("./modelPreview");
      openModelPreview({
        title: rawFbxName,
        resourceBase: "",
        modelFileName: rawFbxName,
        localBuffer: renamed.bytes.buffer.slice(renamed.bytes.byteOffset, renamed.bytes.byteOffset + renamed.bytes.byteLength) as ArrayBuffer,
        localTextures: baseTex ? [baseTex.file] : undefined,
        scale: Number(modelScaleInput?.value) || 1,
        rotationY: Number(modelRotInput?.value) || 0,
        onAdjust: writeAdjustBack,
        referenceUrl: REFERENCE_MODEL.url,
        referenceFormat: REFERENCE_MODEL.format,
        referenceScale: REFERENCE_MODEL.scale,
      });
    });
  };

  modelFileInput?.addEventListener("change", openLocalPreview);

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
      if (rawFbx) openLocalPreview();
    });
  });

  document.getElementById("cr-preview-model")?.addEventListener("click", () => {
    // 若已选择新文件 → 本地预览；否则预览已保存的模型
    if (modelFileInput?.files?.[0]) {
      openLocalPreview();
      return;
    }
    if (!assetPath) return;
    void withBusy("正在加载 3D 预览（首次加载较慢）…", async () => {
      await openRecipeModelPreview(assetPath, recipeName || (assetPath.split("/").pop() ?? assetPath), {
        scale: Number(modelScaleInput?.value) || 1,
        rotationY: Number(modelRotInput?.value) || 0,
        onAdjust: writeAdjustBack,
      });
    });
  });
  // 「+ 新建中间产物」：中间产物也是完整菜谱（图标/模型/装盘容器均可配），
  // 直接进入完整新建表单（预填分数 0），保存后回到列表即可被引用。
  document.getElementById("cr-new-sub")?.addEventListener("click", () =>
    void renderRecipeForm(app, setName, null, {
      score: 0,
      category: (document.getElementById("cr-type-cat") as HTMLSelectElement)?.value || categoryId,
    })
  );

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

  document.getElementById("cr-form-back")?.addEventListener("click", () => void renderRecipeList(app, setName));

  document.getElementById("cr-form-save")?.addEventListener("click", async () => {
    const rname = (document.getElementById("cr-rec-name") as HTMLInputElement).value.trim();
    if (!rname) return alert("请填写标识符");
    if (!IDENT_RE.test(rname)) return alert("标识符仅允许英文字母/数字/下划线，且不能以数字开头");

    const typeVal = (document.getElementById("cr-type") as HTMLSelectElement).value;
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
      modelScale: Number((document.getElementById("cr-model-scale") as HTMLInputElement)?.value) || 1,
      modelRotationY: Number((document.getElementById("cr-model-rot") as HTMLInputElement)?.value) || 0,
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

      const modelFile = (document.getElementById("cr-model-file") as HTMLInputElement).files?.[0];
      if (modelFile) {
        if (!/\.fbx$/i.test(modelFile.name)) {
          setStatus("仅支持 FBX 模型文件。", false);
          return;
        }
        if (!rawFbx) {
          rawFbx = new Uint8Array(await modelFile.arrayBuffer());
          rawFbxName = modelFile.name;
        }
        // base_color 彩色贴图必传（否则 Unity 丢失贴图索引、模型灰色）
        if (!pendingTextures.base_color) {
          setStatus("请先在 base_color 格子中选择彩色贴图。", false);
          return;
        }
        // 贴图重命名为 {菜名}_{类别}.png，并把 FBX 内部贴图引用名一并改写，再连同贴图一起上传；
        // Unity 导入 FBX 时按引用名自动把贴图链接进内嵌材质（无需额外材质球）。
        const renamedRes = (await buildRenamedFbx()) ?? { bytes: rawFbx, renamed: 0 };
        if (renamedRes.renamed === 0) {
          alert("警告：FBX 中未找到可改写的贴图引用（可能不是二进制 FBX 或不含贴图引用），贴图引用名未修改。");
        }
        const uploads = [{ fileName: rawFbxName, base64: await bytesToBase64(renamedRes.bytes) }];
        const diskNames = texDiskNames();
        for (const cls of Object.keys(pendingTextures) as TexClass[]) {
          const t = pendingTextures[cls];
          const diskName = diskNames[cls];
          if (t && diskName) uploads.push({ fileName: diskName, base64: await bytesToBase64(t.bytes) });
        }
        await api.uploadCustomRecipeModelFiles(setName, actualPath, uploads);
      }

      setStatus(isEdit ? "已更新菜谱" : "已创建菜谱");
      // 模型已在选择文件时预览并调整过，保存后直接返回列表
      void renderRecipeList(app, setName);
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });

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
