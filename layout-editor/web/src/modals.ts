import type { IngredientEntry, LayoutItem, RecipeEntry } from "./types";
import {
  foodGroupLabel,
  visibleIngredients,
  ingredientCategoryOf,
  INGREDIENT_CATEGORIES,
} from "./ingredientLabels";
import { groupRecipesByType, recipeTypeLabel } from "./recipeTypes";

/** Inline <img> for an ingredient/recipe: try the extracted icon PNG (unless explicitly known
 *  missing), else / on error fall back to the shared placeholder (MissingIngredient_Icon). */
function iconImg(kind: "ingredients" | "recipes", id: string | undefined, hasIcon?: boolean): string {
  const src = id && hasIcon !== false ? `/icons/${kind}/${id}.png` : "/icons/_placeholder.png";
  return `<img class="food-icon" loading="lazy" src="${src}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`;
}

/** A clickable ingredient tile (image + name). Wraps a visually-hidden checkbox so the existing
 *  `input:checked` gathering still works; selected tiles get a green border via :has(). */
/** 选择器额外选项：single = 单选（食材箱等）；isDisabled = 禁用项置灰不可选（web 内置未放开）。 */
export interface IngredientPickerOptions {
  single?: boolean;
  isDisabled?: (ing: IngredientEntry) => string | null;
}

function ingredientCard(ing: IngredientEntry, checked: boolean, opts?: IngredientPickerOptions): string {
  const badge =
    ing.group && ing.group !== "core" ? ` <span class="pc-badge">${foodGroupLabel(ing.group)}</span>` : "";
  const en = (ing.nameEn && ing.nameEn.trim()) || "";
  const disabledReason = opts?.isDisabled?.(ing) ?? null;
  const disBadge = disabledReason ? ` <span class="pc-badge pc-badge-disabled">⛔ 禁用</span>` : "";
  const input = opts?.single
    ? `<input type="radio" name="ing-pick-single" value="${ing.guid}" ${checked ? "checked" : ""} ${disabledReason ? "disabled" : ""}>`
    : `<input type="checkbox" value="${ing.guid}" ${checked ? "checked" : ""} ${disabledReason ? "disabled" : ""}>`;
  return `<label class="pick-card${disabledReason ? " pick-card-disabled" : ""}"${disabledReason ? ` title="${disabledReason}"` : ""}>
    ${input}
    <span class="pc-head">${iconImg("ingredients", ing.id, ing.icon)}<span class="pc-name">${ing.nameZh}${badge}${disBadge}${en ? ` <span class="muted pc-en">${en}</span>` : ""}</span></span>
  </label>`;
}

/** Grid of ingredient cards. */
function ingredientGrid(ingredients: IngredientEntry[], selected: Set<string>, opts?: IngredientPickerOptions): string {
  return `<div class="pick-grid">${ingredients.map((i) => ingredientCard(i, selected.has(i.guid), opts)).join("")}</div>`;
}

/** Sync the `.selected` class on every pick-card with its checkbox state (visual green border,
 *  works even without CSS :has() support). Call after the modal body is in the DOM. */
function syncPickCards(root: ParentNode): void {
  root.querySelectorAll<HTMLInputElement>(".pick-card input[type=checkbox], .pick-card input[type=radio]").forEach((cb) => {
    const card = cb.closest(".pick-card");
    if (card) card.classList.toggle("selected", cb.checked);
    cb.addEventListener("change", () => {
      if (cb.type === "radio" && cb.checked) {
        // 单选：清掉其它卡片的选中态
        (cb.closest(".pick-grid") ?? root).querySelectorAll(".pick-card.selected").forEach((c) => {
          if (c !== card) c.classList.remove("selected");
        });
      }
      card?.classList.toggle("selected", cb.checked);
    });
  });
}

export function ensureModalRoot(): HTMLElement {
  let root = document.getElementById("modal-root");
  if (!root) {
    root = document.createElement("div");
    root.id = "modal-root";
    document.body.appendChild(root);
  }
  return root;
}

export function openModal(title: string, bodyHtml: string, footerHtml: string): HTMLElement {
  const root = ensureModalRoot();
  root.innerHTML = `
    <div class="modal-backdrop" data-modal-backdrop>
      <div class="modal-panel" role="dialog">
        <h2 class="modal-title">${title}</h2>
        <div class="modal-body">${bodyHtml}</div>
        <div class="modal-footer">${footerHtml}</div>
      </div>
    </div>
  `;
  root.querySelector("[data-modal-backdrop]")?.addEventListener("click", (e) => {
    if (e.target === e.currentTarget) closeModal();
  });
  return root;
}

export function closeModal() {
  const root = document.getElementById("modal-root");
  if (root) root.innerHTML = "";
}

export function openIngredientPicker(
  ingredients: IngredientEntry[],
  currentGuid: string | undefined,
  onSave: (guid: string) => void
) {
  ingredients = visibleIngredients(ingredients);
  const grid = `<div class="pick-grid" id="ing-pick-grid">${ingredients
    .map((ing) => {
      const sel = ing.guid === currentGuid;
      const badge = ing.group && ing.group !== "core" ? ` <span class="pc-badge">${foodGroupLabel(ing.group)}</span>` : "";
      const en = (ing.nameEn && ing.nameEn.trim()) || "";
      return `<label class="pick-card${sel ? " selected" : ""}" data-guid="${ing.guid}">
        <input type="radio" name="ing-pick" value="${ing.guid}" ${sel ? "checked" : ""}>
        <span class="pc-head">${iconImg("ingredients", ing.id, ing.icon)}<span class="pc-name">${ing.nameZh}${badge}${en ? ` <span class="muted pc-en">${en}</span>` : ""}</span></span>
      </label>`;
    })
    .join("")}</div>`;

  openModal(
    "食材箱 · 选择食材",
    `<p class="modal-hint">点击选择食材 (spawnerItemPrefabSO)</p>${grid}`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn primary" data-ok>确定</button>`
  );

  // toggle .selected on click (single-select)
  document.querySelectorAll<HTMLElement>("#ing-pick-grid .pick-card").forEach((card) => {
    card.addEventListener("click", () => {
      document.querySelectorAll("#ing-pick-grid .pick-card.selected").forEach((c) => c.classList.remove("selected"));
      card.classList.add("selected");
    });
  });

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", () => {
    const checked = document.querySelector<HTMLInputElement>("#ing-pick-grid input:checked");
    if (checked) onSave(checked.value);
    closeModal();
  });
}

export function openFoodSpawnerEditor(
  item: LayoutItem,
  ingredients: IngredientEntry[],
  onSave: (patch: NonNullable<LayoutItem["foodSpawner"]>) => void
) {
  const fs = item.foodSpawner ?? {};
  const selected = new Set(fs.attachmentPrefabGuids ?? []);
  const grid = ingredientGrid(ingredients, selected, {
    // node 型食材无实体 prefab，挂点生成器无法实例化
    isDisabled: (i) => (i.nodeOnly ? "node 型食材（无实体 prefab），生成器无法生成" : null),
  });

  openModal(
    "食材生成器 · 参数",
    `
    <label class="modal-check"><input type="checkbox" id="modal-spawn-order" ${fs.spawnInOrder !== false ? "checked" : ""} /> 按顺序生成 (spawnInOrder)</label>
    <label class="modal-check"><input type="checkbox" id="modal-trigger-start" ${fs.triggerAtStart !== false ? "checked" : ""} /> 开局触发 (triggerAtStart)</label>
    <label class="modal-field">触发间隔 triggerTime (秒)<input type="number" id="modal-trigger-time" step="0.5" min="0" value="${fs.triggerTime ?? 5}" /></label>
    <p class="modal-hint">attachmentPrefabSOs（勾选食材，权重均分）</p>
    <div class="modal-scroll">${grid}</div>
    `,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn primary" data-ok>确定</button>`
  );

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", () => {
    const guids: string[] = [];
    document.querySelectorAll<HTMLInputElement>(".modal-scroll input:checked").forEach((el) => {
      guids.push(el.value);
    });
    const n = guids.length;
    const weights = n > 0 ? guids.map(() => 1 / n) : [];
    onSave({
      spawnInOrder: (document.getElementById("modal-spawn-order") as HTMLInputElement).checked,
      triggerAtStart: (document.getElementById("modal-trigger-start") as HTMLInputElement).checked,
      triggerTime: Number((document.getElementById("modal-trigger-time") as HTMLInputElement).value),
      attachmentPrefabGuids: guids,
      weights,
    });
    closeModal();
  });
  syncPickCards(document);
}

export function openIngredientMultiPicker(
  title: string,
  hint: string,
  ingredients: IngredientEntry[],
  selectedGuids: string[],
  onSave: (guids: string[]) => void,
  intermediates?: RecipeEntry[],
  opts?: IngredientPickerOptions
) {
  ingredients = visibleIngredients(ingredients);
  const groups = [...new Set(ingredients.map((i) => i.group ?? "other"))]
    .filter((g) => g !== "core")
    .sort();
  const hasIntermediates = intermediates && intermediates.length > 0;
  const selectedInit = new Set(selectedGuids);
  const hasSelected = selectedInit.size > 0;
  // 分类 chips：只显示当前列表里实际出现的分类
  const cats = INGREDIENT_CATEGORIES.filter((c) =>
    ingredients.some((i) => ingredientCategoryOf(i.id) === c.key)
  );

  // card for intermediate recipe (uses recipe icons)
  function recipeCard(r: RecipeEntry, checked: boolean): string {
    const en = (r.nameEn && r.nameEn.trim()) || "";
    return `<label class="pick-card">
      <input type="checkbox" value="${r.guid}" ${checked ? "checked" : ""}>
      <span class="pc-head">${iconImg("recipes", r.id, r.icon)}<span class="pc-name">${r.nameZh} <span class="pc-badge">中间产物</span>${en ? ` <span class="muted pc-en">${en}</span>` : ""}</span></span>
    </label>`;
  }

  function recipeGrid(recipes: RecipeEntry[], selected: Set<string>): string {
    return `<div class="pick-grid">${recipes.map((r) => recipeCard(r, selected.has(r.guid))).join("")}</div>`;
  }

  const filterBar = `
    <div class="ing-filter-bar">
      <input type="search" id="ing-pick-search" class="ing-search" placeholder="搜索食材…" />
      <div class="ing-groups">
        <button type="button" class="ing-group-btn active" data-group="">全部</button>
        ${groups.map((g) => `        <button type="button" class="ing-group-btn" data-group="${g}" ${g === "web" ? 'title="Web内置 · 保存时自动打包到本关卡集"' : ""}>${foodGroupLabel(g)}</button>${""}`).join("")}
        ${hasIntermediates ? '<button type="button" class="ing-group-btn" data-group="__intermediate__">中间产物</button>' : ""}
        ${hasSelected ? '<button type="button" class="ing-group-btn" data-group="__selected__" title="只显示当前已勾选的条目，便于审查与取消">✓ 已选</button>' : ""}
      </div>
    </div>
    ${cats.length > 1 ? `
    <div class="ing-filter-bar ing-cat-bar">
      <span class="ing-cat-label">分类</span>
      <div class="ing-groups">
        <button type="button" class="ing-group-btn active" data-cat="">全部</button>
        ${cats.map((c) => `<button type="button" class="ing-group-btn" data-cat="${c.key}">${c.label}</button>`).join("")}
      </div>
    </div>` : ""}`;

  function buildFiltered(): string {
    const selected = new Set(selectedGuids);
    const activeGroup = (document.querySelector(".ing-groups .ing-group-btn.active[data-group]") as HTMLElement)?.dataset.group ?? "";
    const activeCat = (document.querySelector(".ing-groups .ing-group-btn.active[data-cat]") as HTMLElement)?.dataset.cat ?? "";
    const q = (document.getElementById("ing-pick-search") as HTMLInputElement)?.value?.trim()?.toLowerCase() ?? "";
    const catMatch = (i: IngredientEntry) => !activeCat || ingredientCategoryOf(i.id) === activeCat;
    const textMatch = (i: IngredientEntry) =>
      !q || i.nameZh.toLowerCase().includes(q) || (i.nameEn ?? "").toLowerCase().includes(q) || i.id.toLowerCase().includes(q);

    // 已选 tab：当前勾选的食材 + 中间产物（仍受分类/搜索过滤）
    if (activeGroup === "__selected__") {
      const selIngs = ingredients.filter((i) => selected.has(i.guid) && catMatch(i) && textMatch(i));
      const selRecs = (intermediates ?? []).filter((r) => selected.has(r.guid));
      return ingredientGrid(selIngs, selected, opts) + (selRecs.length ? recipeGrid(selRecs, selected) : "");
    }

    // intermediates group
    if (activeGroup === "__intermediate__" && intermediates) {
      return recipeGrid(intermediates, selected);
    }

    let filtered = ingredients;
    if (activeGroup) filtered = filtered.filter((i) => (i.group ?? "other") === activeGroup);
    filtered = filtered.filter(catMatch);
    filtered = filtered.filter(textMatch);
    return ingredientGrid(filtered, selected, opts);
  }

  function applyFilter(): void {
    const container = document.getElementById("ing-pick-container");
    if (container) container.innerHTML = buildFiltered();
    syncPickCards(container ?? document);
  }

  openModal(
    title,
    `<p class="modal-hint">${hint}</p>
     ${filterBar}
     <div class="modal-scroll" id="ing-pick-container">${buildFiltered()}</div>`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     ${opts?.single ? '<button type="button" class="modal-btn" data-clear>清除设置</button>' : ""}
     <button type="button" class="modal-btn primary" data-ok>确定</button>`
  );

  const panel = document.querySelector(".modal-panel");
  if (panel) panel.classList.add("wide");

  document.getElementById("ing-pick-search")?.addEventListener("input", applyFilter);
  // 分组 tab 与分类 chips 各自独立高亮/过滤（data-group / data-cat）
  document.querySelectorAll(".ing-group-btn[data-group]").forEach((btn) => {
    btn.addEventListener("click", () => {
      document.querySelectorAll(".ing-group-btn[data-group]").forEach((b) => b.classList.remove("active"));
      btn.classList.add("active");
      applyFilter();
    });
  });
  document.querySelectorAll(".ing-group-btn[data-cat]").forEach((btn) => {
    btn.addEventListener("click", () => {
      document.querySelectorAll(".ing-group-btn[data-cat]").forEach((b) => b.classList.remove("active"));
      btn.classList.add("active");
      applyFilter();
    });
  });

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-clear]")?.addEventListener("click", () => {
    onSave([]);
    closeModal();
  });
  document.querySelector("[data-ok]")?.addEventListener("click", () => {
    const guids: string[] = [];
    document.querySelectorAll<HTMLInputElement>("#ing-pick-container input:checked").forEach((el) => {
      guids.push(el.value);
    });
    onSave(guids);
    closeModal();
  });
}

export function openRecipePicker(
  recipes: RecipeEntry[],
  selectedGuids: string[],
  levelName: string,
  onSave: (guids: string[]) => void
) {
  const set = new Set(selectedGuids);
  const list = groupRecipesByType(recipes.filter((r) => !r.intermediate))
    .map(([type, items]) => {
      const header = `<p class="modal-group-header">${recipeTypeLabel(type)} (${items.length})</p>`;
      const rows = items
        .map((r) => {
          const src = r.group && r.group !== "core" ? ` <span class="muted">[${foodGroupLabel(r.group)}]</span>` : "";
          return `<label class="modal-check"><input type="checkbox" value="${r.guid}" ${set.has(r.guid) ? "checked" : ""} /> ${iconImg("recipes", r.id, r.icon)}${r.nameZh}${r.nameEn ? ` <span class="muted">(${r.nameEn})</span>` : ""}${src}</label>`;
        })
        .join("");
      return header + rows;
    })
    .join("");

  openModal(
    `关卡菜谱 · ${levelName || "未命名"}`,
    `<p class="modal-hint">勾选本关订单菜谱（写入 LevelInfoSO.recipes）</p><div class="modal-scroll">${list}</div>`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn primary" data-ok>保存</button>`
  );

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", () => {
    const guids: string[] = [];
    document.querySelectorAll<HTMLInputElement>(".modal-scroll input:checked").forEach((el) => {
      guids.push(el.value);
    });
    onSave(guids);
    closeModal();
  });
}
