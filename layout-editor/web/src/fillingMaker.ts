/**
 * fillingMaker.ts —— 🥩 夹心工作台（/custom-recipes/filling-maker）。
 *
 * 设计要点：**夹心就是一道 0 分的自定义菜谱**，所以本页不自己实现编辑逻辑，
 * 而是直接调用菜谱编辑器 `renderRecipeForm(..., { mode: "filling" })`。
 * 编辑器（组成增删、烹饪装盘、模型上传/贴图槽位/预览校准/诊断，共 1300+ 行）
 * 后续任何改动，本页自动继承，无需同步。
 *
 * 本文件只负责「工作台外壳」：关卡集选择 + 已有夹心列表 + 新建/编辑/预览/删除入口。
 * 卡片渲染复用共享的 recipeCard.ts / recipeCardCustom.ts（菜谱管理页与菜谱清单页同款）。
 */
import * as api from "./api";
import { navHtml, wireNav } from "./nav";
import { showBusy, hideBusy } from "./busy";
import { fillingPath, parseRoute } from "./route";
import { rlCardHtml, type RecipeWithGroups } from "./recipeCard";
import { normalizeCustomRecipeCard } from "./recipeCardCustom";
import { openRecipeModelPreview } from "./recipeModelPreview";
import { foodIconSrc } from "./foodIconChain";
import { renderRecipeForm } from "./customRecipes";
import type { CustomRecipeSummary, LevelSetInfo } from "./types";

/** 夹心在关卡集内的默认分类目录。 */
const FILLING_CATEGORY = "burger_filling";

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function setStatus(msg: string, ok = true): void {
  const el = document.getElementById("fm-status");
  if (!el) return;
  el.textContent = msg;
  el.classList.toggle("err", !ok);
  el.classList.toggle("ok", ok && msg.length > 0);
}

/** commonW2 共享库（只读参考）。 */
function isSharedLib(r: CustomRecipeSummary): boolean {
  return r.assetPath.replace(/\\/g, "/").includes("/commonW2/");
}

/** 夹心 = 0 分自定义菜谱。共享库只看 filling/ 子分类；本关卡集看 0 分条目。 */
function isFilling(r: CustomRecipeSummary): boolean {
  if ((r.score ?? 0) > 0) return false;
  if (isSharedLib(r)) return (r.subcategory ?? "") === "filling";
  return true;
}

export async function renderFillingMakerView(app: HTMLElement): Promise<void> {
  document.body.classList.add("manage-bg");

  // 严格路由 /custom-recipes/filling-maker/{set}[/{fillingId}]：按 URL 定位关卡集
  const route = parseRoute();
  const routeSet = route.page === "filling-maker" ? route.setId ?? "" : "";
  let sets: LevelSetInfo[] = [];
  try {
    sets = await api.fetchSets();
  } catch {
    /* 下面统一提示 */
  }
  if (sets.length === 0) {
    app.innerHTML = `${navHtml("custom-recipes")}
      <div class="manage-bar"><h1 class="m-title">🥩 夹心工作台</h1></div>
      <div class="manage-content"><div class="m-block"><h3>没有可用关卡集</h3>
        <p class="muted">夹心归属关卡集。请先在「关卡管理」创建关卡集，并打开过一次其自定义菜谱页（初始化配置）。
        若是桥接未启动，请在 Unity 中 Tools → Layout Editor → 启动服务。</p></div></div>`;
    wireNav();
    return;
  }

  const routeSetValid = routeSet !== "" && sets.some((s) => s.setName === routeSet);
  let setName = routeSetValid ? routeSet : sets[0].setName;
  // 裸路径 / 非法集：replaceState 成严格 URL（非法集时丢弃 fillingId）
  const initialFillingId = routeSetValid ? route.fillingId ?? "" : "";
  const canonical = fillingPath(setName, initialFillingId || undefined);
  if (location.pathname !== canonical) history.replaceState(null, "", canonical);

  function syncPath(p: string): void {
    if (location.pathname !== p) history.pushState(null, "", p);
  }

  function onFormPath(): boolean {
    const r = parseRoute();
    return r.page === "filling-maker" && !!r.fillingId;
  }

  /** 本集可编辑夹心（深链 fillingId → assetPath 解析用；共享库只读不参与）。 */
  let lastMine: CustomRecipeSummary[] = [];

  async function render(): Promise<void> {
    app.innerHTML = `
      ${navHtml("custom-recipes")}
      <div class="manage-bar">
        <h1 class="m-title">🥩 夹心工作台</h1>
        <span class="status" id="fm-status"></span>
        <span style="flex:1"></span>
        <a class="m-btn" href="/custom-recipes/burger-maker">🍔 汉堡组装工作台</a>
        <a class="m-btn" href="/custom-recipes">← 菜谱管理</a>
      </div>
      <div class="manage-content" id="fm-content"><p class="muted">加载中…</p></div>
    `;
    wireNav();

    const content = document.getElementById("fm-content")!;
    let recipes: CustomRecipeSummary[] = [];
    try {
      showBusy("加载夹心…");
      recipes = await api.fetchCustomRecipes(setName);
    } catch (e) {
      content.innerHTML = `<div class="m-block"><h3>加载失败</h3><p class="muted">${esc(
        (e as Error).message
      )}</p><p class="muted">请确认 Unity 编辑器已启动且桥接服务运行中。</p></div>`;
      hideBusy();
      return;
    } finally {
      hideBusy();
    }

    const mine = recipes.filter((r) => !isSharedLib(r) && isFilling(r));
    const shared = recipes.filter((r) => isSharedLib(r) && isFilling(r));
    lastMine = mine;

    const cardHtml = (r: CustomRecipeSummary, readonly: boolean): string => {
      let inner: string;
      try {
        // 走共享降级链：commonW2 夹心共用的 FriedGeneric.png（一整颗洋葱）会被跳过，
        // 改用 /icons/recipes/<id>.png → 叶食材图标，保证各夹心图标可区分。
        inner = rlCardHtml(normalizeCustomRecipeCard(r) as RecipeWithGroups, {
          iconSrc: () =>
            foodIconSrc({
              id: r.id,
              assetPath: r.assetPath,
              iconState: r.iconState,
              iconFallbackIds: r.ingredients ?? [],
              isCustom: true,
            }),
        });
      } catch (e) {
        inner = `<div class="m-card"><h3>${esc(r.nameZh)}</h3><p class="muted">卡片渲染失败：${esc(
          (e as Error).message
        )}</p></div>`;
      }
      // 有模型 = model(本地 prefab) 或 modelSO(bundle 指针) 任一存在；
      // 可预览 = 能解析出本地网格文件（bundle 指针没有本地网格，网页看不了）
      const hasAnyModel = r.hasModel || Boolean(r.hasModelSO) || Boolean(r.previewable);
      const model = hasAnyModel
        ? '<span class="bm-badge ok">有模型</span>'
        : '<span class="bm-badge warn" title="没有模型的夹心在汉堡里那一层看不见，且不会出现在汉堡工作台候选中">⚠ 无模型</span>';
      return `
        <div class="cr-card-wrap">
          <div class="cr-card-inner">${inner}</div>
          <div class="cr-card-foot">
            ${readonly ? '<span class="cr-cat-tag">共享库（只读）</span>' : `<span class="cr-cat-tag">${esc(r.category)}</span>`}
            ${model}
            <span style="flex:1"></span>
            ${r.previewable ? `<button class="m-btn small" data-fm-preview="${esc(r.assetPath)}" data-fm-name="${esc(r.nameZh)}" title="3D 模型在线预览">👁</button>` : ""}
            ${readonly ? "" : `<button class="m-btn small" data-fm-edit="${esc(r.assetPath)}">编辑</button>`}
            ${readonly ? "" : `<button class="m-btn small danger" data-fm-del="${esc(r.assetPath)}">删除</button>`}
          </div>
        </div>`;
    };

    content.innerHTML = `
      <div class="m-actions-row">
        <span class="muted">关卡集</span>
        <select id="fm-set" class="rl-select">
          ${sets
            .map(
              (s) =>
                `<option value="${esc(s.setName)}" ${s.setName === setName ? "selected" : ""}>${esc(
                  s.levelSetNameZH || s.setName
                )}（${esc(s.setName)}）</option>`
            )
            .join("")}
        </select>
        <span style="flex:1"></span>
        <button class="m-btn primary" id="fm-new">＋ 新建夹心</button>
      </div>
      <p class="modal-hint">夹心 = <b>0 分的自定义菜谱</b>，不可单独点单，只作为汉堡的一层。
        新建后给它一个模型（可用「模板网格 + 自制贴图」零建模），就能在
        <b>🍔 汉堡组装工作台</b> 的候选里选用 —— 没有模型的夹心不会出现在候选中，因为游戏里那层看不见。</p>

      <section class="rl-section">
        <h2 class="rl-section-title">本关卡集的夹心<span class="rl-section-count">${mine.length}</span></h2>
        ${mine.length > 0
          ? `<div class="rl-grid">${mine.map((r) => cardHtml(r, false)).join("")}</div>`
          : `<p class="muted">还没有夹心。点右上角「＋ 新建夹心」创建第一个。</p>`}
      </section>

      <section class="rl-section">
        <h2 class="rl-section-title">🍔 Burger大全 共享夹心（只读参考）<span class="rl-section-count">${shared.length}</span></h2>
        <p class="muted small">这些来自 commonW2 共享库，所有关卡集都能直接选用；要改请在本关卡集另建一个。</p>
        ${shared.length > 0
          ? `<div class="rl-grid">${shared.map((r) => cardHtml(r, true)).join("")}</div>`
          : `<p class="muted">共享库中没有夹心条目。</p>`}
      </section>
    `;
    setStatus(`本关卡集 ${mine.length} 个夹心 · 共享库 ${shared.length} 个`);
    wire();
    // 列表视图与 URL 保持一致（表单深链 /{fillingId} 时由 handleRoute 负责后续跳转）
    if (!onFormPath()) syncPath(fillingPath(setName));
  }

  function openForm(assetPath: string | null): void {
    void renderRecipeForm(app, setName, assetPath, {
      mode: "filling",
      score: 0,
      category: FILLING_CATEGORY,
      onBack: () => {
        syncPath(fillingPath(setName));
        void render();
      },
    });
  }

  /** 路由分发：列表路径渲染列表；表单路径（new 或具体夹心 id）直开编辑表单。 */
  async function handleRoute(): Promise<void> {
    await render();
    const r = parseRoute();
    if (r.page === "filling-maker" && r.setId === setName && r.fillingId) {
      if (r.fillingId === "new") {
        openForm(null);
        return;
      }
      const hit = lastMine.find((x) => x.id === r.fillingId);
      if (hit) openForm(hit.assetPath);
      else setStatus(`未找到夹心「${r.fillingId}」，请从列表重新进入。`, false);
    }
  }

  function wire(): void {
    document.getElementById("fm-set")?.addEventListener("change", (e) => {
      setName = (e.target as HTMLSelectElement).value;
      // 切集 = 新工作台状态：URL 去掉 fillingId（replace 不产生历史）
      history.replaceState(null, "", fillingPath(setName));
      void handleRoute();
    });
    document.getElementById("fm-new")?.addEventListener("click", () => {
      syncPath(fillingPath(setName, "new"));
      openForm(null);
    });
    document.querySelectorAll<HTMLButtonElement>("[data-fm-edit]").forEach((b) =>
      b.addEventListener("click", () => {
        const path = b.dataset.fmEdit!;
        const hit = lastMine.find((x) => x.assetPath === path);
        syncPath(fillingPath(setName, hit?.id ?? "new"));
        openForm(path);
      })
    );
    document.querySelectorAll<HTMLButtonElement>("[data-fm-preview]").forEach((b) =>
      b.addEventListener("click", () => {
        void openRecipeModelPreview(b.dataset.fmPreview!, b.dataset.fmName ?? "夹心模型", {
          fitTarget: "plate",
          onError: (msg) => setStatus(msg, false),
        });
      })
    );
    document.querySelectorAll<HTMLButtonElement>("[data-fm-del]").forEach((b) =>
      b.addEventListener("click", () => {
        const path = b.dataset.fmDel!;
        if (!confirm(`确定删除夹心「${path.split("/").pop()}」？\n引用它的汉堡会丢失这一层。`)) return;
        void (async () => {
          try {
            showBusy("删除中…");
            await api.deleteCustomRecipe(path);
            await render();
            setStatus("已删除。");
          } catch (e) {
            setStatus((e as Error).message, false);
          } finally {
            hideBusy();
          }
        })();
      })
    );
  }

  window.addEventListener("popstate", () => void handleRoute());
  await handleRoute();
}
