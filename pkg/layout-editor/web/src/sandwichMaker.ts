/**
 * sandwichMaker.ts —— 三明治组装工作台（/custom-recipes/sandwich-maker）。
 *
 * 上游 2026-11 新增 CustomRecipeOptionalSandwichSO / ICustomRecipeOptional。
 * 本页是轻量编辑器：toast 底层 + 夹心（optionalSOs）+ 酱料（sauceSOs）+
 * 容量/分数/装盘模型；保存走 /api/custom-recipes/create|update（optionalKind=sandwich）。
 */
import { navHtml, wireNav } from "./nav";
import { setStatus } from "./editor/status";
import { sandwichPath, parseRoute } from "./route";
import * as api from "./api";
import type {
  CustomRecipeConfig,
  CustomRecipeEdit,
  CustomRecipeReferences,
  CustomRecipeSummary,
  IngredientEntry,
  LevelSetInfo,
} from "./types";

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function showError(e: unknown): void {
  setStatus((e as Error).message ?? String(e), false);
}

function checked(values: string[] | undefined, id: string): string {
  return (values ?? []).includes(id) ? "checked" : "";
}

function selected(value: string | undefined, id: string): string {
  return value === id ? "selected" : "";
}

export async function renderSandwichMakerView(app: HTMLElement): Promise<void> {
  const route = parseRoute();
  let sets: LevelSetInfo[] = [];
  try {
    sets = await api.fetchSets();
  } catch (e) {
    app.innerHTML = `${navHtml("recipes")}<div class="panel"><p class="muted">无法读取关卡集：${esc((e as Error).message)}</p></div>`;
    wireNav();
    return;
  }

  const routeSet = route.setId ?? "";
  let setName = sets.some((s) => s.setName === routeSet) ? routeSet : (sets[0]?.setName ?? "");
  if (!setName) {
    app.innerHTML = `${navHtml("recipes")}<div class="panel"><p class="muted">还没有关卡集。</p></div>`;
    wireNav();
    return;
  }

  async function load(): Promise<{
    cfg: CustomRecipeConfig;
    refs: CustomRecipeReferences;
    recipes: CustomRecipeSummary[];
    editing: CustomRecipeSummary | null;
  }> {
    const cfg = await api.fetchCustomRecipeConfig(setName);
    const refsData = await api.fetchCustomRecipeReferences(setName);
    const list = await api.fetchCustomRecipes(setName);
    const id = route.sandwichId ?? "";
    const current = id ? list.find((r) => r.id === id && r.optionalKind === "sandwich") ?? null : null;
    return { cfg, refs: refsData, recipes: list, editing: current };
  }

  let loaded: Awaited<ReturnType<typeof load>>;
  try {
    loaded = await load();
  } catch (e) {
    app.innerHTML = `${navHtml("recipes")}<div class="panel"><p class="muted">加载失败：${esc((e as Error).message)}</p></div>`;
    wireNav();
    return;
  }

  const config = loaded.cfg;
  const refs = loaded.refs;
  const recipes = loaded.recipes;
  const current = loaded.editing;
  const categories = config.categories ?? [];
  // 新分类默认落到 "sandwich"；不要回落到分类列表第一项（通常是被后端保留的 burger）。
  const defaultCategory = "sandwich";
  const ingredientEntries = await api.fetchIngredients(setName).catch(() => [] as IngredientEntry[]);
  const ingredients = ingredientEntries;
  const models = refs.reusableModels ?? [];
  const plating = refs.platingContainers?.[0]?.id ?? "Plate";

  const setOptions = sets
    .map((s) => `<option value="${esc(s.setName)}" ${selected(setName, s.setName)}>${esc(s.levelSetNameZH || s.setName)}</option>`)
    .join("");
  const categoryOptions = categories
    .map((c) => `<option value="${esc(c.id)}" ${selected(current?.category ?? defaultCategory, c.id)}>${esc(c.zh || c.id)}</option>`)
    .join("") + (categories.some((c) => c.id === defaultCategory)
      ? ""
      : `<option value="${defaultCategory}" ${selected(current?.category ?? defaultCategory, defaultCategory)}>🥪 三明治</option>`);
  const ingredientOptions = (selectedId: string | undefined) =>
    ingredients
      .map((i) => `<option value="${esc(i.id)}" ${selected(selectedId, i.id)}>${esc(i.nameZh || i.id)}</option>`)
      .join("");
  const modelOptions = models
    .map((m) => `<option value="${esc(m.guid)}" data-id="${esc(m.id)}">${esc(m.nameZh || m.id)}</option>`)
    .join("");

  const fillings = current?.fillingIds ?? [];
  const sauces = current?.sauceIds ?? [];

  app.innerHTML = `
    ${navHtml("recipes")}
    <div class="panel">
      <div class="panel-head">
        <h2>🥪 三明治组装工作台</h2>
        <div class="muted">CustomRecipeOptionalSandwichSO（toast + 夹心 + 酱料）</div>
      </div>
      <div class="cc-grid" style="margin-bottom:12px">
        <label class="m-field">关卡集
          <select id="sw-set" class="rl-select">${setOptions}</select>
        </label>
        <label class="m-field">分类
          <select id="sw-category" class="rl-select">${categoryOptions}</select>
        </label>
      </div>
      <div class="cc-grid">
        <label class="m-field">标识 id（字母数字下划线）
          <input id="sw-id" class="rl-select" value="${esc(current?.id ?? "")}" ${current ? "disabled" : ""} placeholder="如 Web_Sandwich_Ham">
        </label>
        <label class="m-field">中文名
          <input id="sw-zh" class="rl-select" value="${esc(current?.nameZh ?? "")}" placeholder="如 火腿三明治">
        </label>
        <label class="m-field">英文名
          <input id="sw-en" class="rl-select" value="${esc(current?.nameEn ?? "")}" placeholder="Ham Sandwich">
        </label>
        <label class="m-field">分数
          <input id="sw-score" type="number" class="rl-select" value="${current?.score ?? 80}">
        </label>
        <label class="m-field">容量（夹心槽）
          <input id="sw-capacity" type="number" min="1" class="rl-select" value="${current?.sandwichCapacity ?? 8}">
        </label>
        <label class="m-field">Toast 底层（必填）
          <select id="sw-toast" class="rl-select">
            <option value="">— 选择食材 —</option>
            ${ingredientOptions(current?.toastId)}
          </select>
        </label>
        <label class="m-field">装盘模型（可选）
          <select id="sw-model" class="rl-select">
            <option value="">— 不指定 —</option>
            ${modelOptions}
          </select>
        </label>
      </div>

      <details open style="margin-top:12px">
        <summary><b>夹心（optionalSOs）</b></summary>
        <div class="cc-item-grid" style="max-height:240px;overflow:auto">
          ${ingredients.map((i) => `<label class="m-field" style="display:flex;gap:6px;align-items:center"><input type="checkbox" class="sw-filling" value="${esc(i.id)}" ${checked(fillings, i.id)}> ${esc(i.nameZh || i.id)}</label>`).join("")}
        </div>
      </details>

      <details style="margin-top:12px">
        <summary><b>酱料（sauceSOs）</b></summary>
        <div class="cc-item-grid" style="max-height:200px;overflow:auto">
          ${ingredients.map((i) => `<label class="m-field" style="display:flex;gap:6px;align-items:center"><input type="checkbox" class="sw-sauce" value="${esc(i.id)}" ${checked(sauces, i.id)}> ${esc(i.nameZh || i.id)}</label>`).join("")}
        </div>
      </details>

      <div style="margin-top:16px;display:flex;gap:8px;align-items:center">
        <button type="button" class="m-btn primary" id="sw-save">${current ? "保存修改" : "创建三明治"}</button>
        ${current ? `<a class="m-btn" href="${sandwichPath(setName)}">新建</a>` : ""}
        <span id="sw-status" class="muted"></span>
      </div>

      <h3 style="margin-top:24px">已有三明治</h3>
      <div class="cc-item-grid">
        ${recipes.filter((r) => r.optionalKind === "sandwich").map((r) => `
          <a class="m-card" href="${sandwichPath(setName, r.id)}" style="text-decoration:none">
            <b>${esc(r.nameZh || r.id)}</b><br>
            <span class="muted">${esc(r.id)} · ${r.score} 分</span>
          </a>`).join("") || `<p class="muted">暂无</p>`}
      </div>
    </div>`;

  wireNav();
  const statusEl = document.getElementById("sw-status") as HTMLElement | null;
  const setLocalStatus = (msg: string, ok = true) => {
    if (statusEl) statusEl.textContent = msg;
    setStatus(msg, ok);
  };

  document.getElementById("sw-set")?.addEventListener("change", (e) => {
    const v = (e.target as HTMLSelectElement).value;
    location.assign(sandwichPath(v));
  });

  document.getElementById("sw-save")?.addEventListener("click", async () => {
    try {
      const id = (document.getElementById("sw-id") as HTMLInputElement).value.trim();
      if (!id) return setLocalStatus("请填写标识 id。", false);
      if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(id)) return setLocalStatus("标识 id 仅允许英文字母/数字/下划线，且不能以数字开头。", false);
      const toast = (document.getElementById("sw-toast") as HTMLSelectElement).value;
      if (!toast) return setLocalStatus("请选择 Toast 底层食材。", false);
      const fillingsSel = Array.from(document.querySelectorAll<HTMLInputElement>(".sw-filling:checked")).map((x) => x.value);
      const saucesSel = Array.from(document.querySelectorAll<HTMLInputElement>(".sw-sauce:checked")).map((x) => x.value);
      const score = Number((document.getElementById("sw-score") as HTMLInputElement).value) || 0;
      const capacity = Number((document.getElementById("sw-capacity") as HTMLInputElement).value) || 8;
      const category = (document.getElementById("sw-category") as HTMLSelectElement).value || defaultCategory;
      const model = (document.getElementById("sw-model") as HTMLSelectElement).value;
      const body: CustomRecipeEdit = {
        setName,
        assetPath: current?.assetPath,
        recipeName: current?.recipeName || id,
        nameZh: (document.getElementById("sw-zh") as HTMLInputElement).value.trim() || id,
        nameEn: (document.getElementById("sw-en") as HTMLInputElement).value.trim() || id,
        category,
        score,
        type: "Composite",
        compositionIds: [],
        cookingStepId: "",
        cookingStepIconId: "",
        platingStepId: plating,
        mixingIconId: "",
        modelPrefabId: model,
        cookingProgress: 1,
        mixingProgress: 1,
        modelScale: 1,
        modelRotationX: 0,
        modelRotationY: 0,
        modelRotationZ: 0,
        modelPositionX: 0,
        modelPositionY: 0,
        modelPositionZ: 0,
        modelPivotX: 0,
        modelPivotY: 0,
        modelPivotZ: 0,
        optionalKind: "sandwich",
        toastGuid: toast,
        fillingGuids: fillingsSel,
        sauceGuids: saucesSel,
        ingredientContainerCapacity: capacity,
        ingredientModelSoGuids: [],
        ingredientModelGuids: [],
        sauceInPlateModelGuids: [],
        sauceOnToastModelGuids: [],
        wrongSauceInPlateGuid: "",
        wrongSauceOnToastGuid: "",
      };
      if (current) await api.updateCustomRecipe(body);
      else await api.createCustomRecipe(body);
      setLocalStatus(current ? "已保存。" : "已创建。");
      location.assign(sandwichPath(setName, current ? current.id : id));
    } catch (e) {
      showError(e);
    }
  });
}
