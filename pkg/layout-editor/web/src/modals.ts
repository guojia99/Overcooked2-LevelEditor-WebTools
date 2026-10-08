import type { IngredientEntry, LayoutItem, RecipeEntry } from "./types";
import {
  cancelBtnHtml,
  modalBtnHtml,
  modalFooterBtnsHtml,
  primaryBtnHtml,
} from "./ui/views/button";
import { modalPanelClassAttr } from "./ui/views/modal";
import {
  floatingDragBarHtml,
  setupModalPanelDrag,
} from "./editor/ui/floatingPanelDrag";
import {
  foodGroupLabel,
  visibleIngredients,
  ingredientCategoryOf,
  INGREDIENT_CATEGORIES,
} from "./ingredientLabels";
import { groupRecipesByType, recipeTypeLabel } from "./recipeTypes";
import { getQuestionMarkStyles } from "./editor/iconCaches";
import { questionMarkIconUrl, crateAirIconUrl } from "./api";
import {
  levelRequiredCrateIngredientIds,
  recipeCategoryOf,
  utensilIntermediateRecipes,
} from "./editor/recipeKnowledge";

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

export interface ModalOptions {
  /** 关闭点背景遮罩关闭弹窗（默认允许）。设为 false 时只能通过按钮关闭，
   *  避免误触遮罩导致已填写内容丢失。 */
  closeOnBackdrop?: boolean;
  /** Optional class added to the dialog panel for domain-specific layouts. */
  panelClass?: string;
  /** 弹窗身份标识（写入 backdrop 的 data-modal-id）。同 id 重开 = 替换当前顶层
   *  （不压栈，供弹窗自重绘，如地板编辑器切换材质）；不同 id / 无 id = 视为
   *  子弹窗，当前顶层压栈、关闭子弹窗时原样唤回。 */
  id?: string;
}

/** 弹窗栈：打开子弹窗时把父弹窗整体（活 DOM）摘下压栈——监听器、勾选、滚动位置、
 * canvas 画作全部保留；closeModal 只关顶层并把父弹窗原样挂回。任意时刻
 * #modal-root 内只有最顶层弹窗，既有 document.querySelector("[data-ok]") 等
 * 「绑定顶层」的查询模式不受影响；「root 空 ⇔ 全部已关」不变量保持（音频弹窗的
 * MutationObserver 依赖此语义）。 */
const modalStack: HTMLElement[] = [];

export function openModal(
  title: string,
  bodyHtml: string,
  footerHtml: string,
  opts?: ModalOptions
): HTMLElement {
  const root = ensureModalRoot();
  const current = root.querySelector<HTMLElement>("[data-modal-backdrop]");
  if (current) {
    current.remove();
    if (!opts?.id || current.dataset.modalId !== opts.id) {
      modalStack.push(current);
    }
  }
  const panelCls = modalPanelClassAttr(opts?.panelClass ?? "");
  root.insertAdjacentHTML(
    "beforeend",
    `
    <div class="modal-backdrop" data-modal-backdrop${opts?.id ? ` data-modal-id="${opts.id}"` : ""}>
       <div class="${panelCls}" role="dialog">
        ${floatingDragBarHtml(`<h2 class="modal-title">${title}</h2>`)}
        <div class="modal-body">${bodyHtml}</div>
        <div class="modal-footer">${footerHtml}</div>
      </div>
    </div>
  `
  );
  const backdrop = root.querySelector<HTMLElement>("[data-modal-backdrop]");
  const panel = root.querySelector<HTMLElement>(".modal-panel");
  if (backdrop && panel) setupModalPanelDrag(panel, backdrop);
  if (opts?.closeOnBackdrop !== false) {
    backdrop?.addEventListener("click", (e) => {
      if (e.target === e.currentTarget) closeModal();
    });
  }
  return root;
}

/** 关闭最顶层弹窗；若有被压栈的父弹窗则原样唤回（状态保留）。Esc / 遮罩点击
 * 均为逐层退栈：先关子弹窗、父弹窗回来，再按一次才关父弹窗。 */
export function closeModal() {
  const root = document.getElementById("modal-root");
  root?.querySelector("[data-modal-backdrop]")?.remove();
  const parent = modalStack.pop();
  if (parent && root) root.appendChild(parent);
}

/** 关闭全部弹窗（含栈中被压住的父弹窗）。用于「执行动作后不应残留任何弹窗」
 * 或全局接管（桥看门狗）的场景。 */
export function closeAllModals() {
  modalStack.length = 0;
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
    modalFooterBtnsHtml()
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
    modalFooterBtnsHtml()
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
  const pickerIntermediates = intermediates ? utensilIntermediateRecipes(intermediates) : [];
  const hasIntermediates = pickerIntermediates.length > 0;
  // 勾选状态的唯一来源（可变）：切分组/分类/搜索会整体重建列表 DOM，勾选必须
  // 即时同步到这里、确定时从这里收集——否则跨分类勾选丢失/写回只剩当前分类
  // （与 openRandomCrateEditor 的 state Map 同款模式）。
  const selected = new Set(selectedGuids);
  // 分类 chips：食材 + 中间产物实际出现的分类
  const cats = INGREDIENT_CATEGORIES.filter(
    (c) =>
      ingredients.some((i) => ingredientCategoryOf(i.id) === c.key) ||
      pickerIntermediates.some((r) => recipeCategoryOf(r) === c.key),
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
        ${groups.map((g) => `        <button type="button" class="ing-group-btn" data-group="${g}">${foodGroupLabel(g)}</button>${""}`).join("")}
        ${hasIntermediates ? '<button type="button" class="ing-group-btn" data-group="__intermediate__">中间产物</button>' : ""}
        <button type="button" class="ing-group-btn" data-group="__selected__" title="只显示当前已勾选的条目，便于审查与取消">✓ 已选 (${selected.size})</button>
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
    const activeGroup = (document.querySelector(".ing-groups .ing-group-btn.active[data-group]") as HTMLElement)?.dataset.group ?? "";
    const activeCat = (document.querySelector(".ing-groups .ing-group-btn.active[data-cat]") as HTMLElement)?.dataset.cat ?? "";
    const q = (document.getElementById("ing-pick-search") as HTMLInputElement)?.value?.trim()?.toLowerCase() ?? "";
    const catMatch = (i: IngredientEntry) => !activeCat || ingredientCategoryOf(i.id) === activeCat;
    const textMatch = (i: IngredientEntry) =>
      !q ||
      i.nameZh.toLowerCase().includes(q) ||
      (i.nameEn ?? "").toLowerCase().includes(q) ||
      i.id.toLowerCase().includes(q);
    const recipeCatMatch = (r: RecipeEntry) => !activeCat || recipeCategoryOf(r) === activeCat;
    const recipeTextMatch = (r: RecipeEntry) =>
      !q ||
      r.nameZh.toLowerCase().includes(q) ||
      (r.nameEn ?? "").toLowerCase().includes(q) ||
      r.id.toLowerCase().includes(q) ||
      (r.ingredients ?? []).join(" ").toLowerCase().includes(q);

    // 已选 tab：当前勾选的食材 + 中间产物（仍受分类/搜索过滤）
    if (activeGroup === "__selected__") {
      const selIngs = ingredients.filter((i) => selected.has(i.guid) && catMatch(i) && textMatch(i));
      const selRecs = pickerIntermediates.filter(
        (r) => selected.has(r.guid) && recipeCatMatch(r) && recipeTextMatch(r),
      );
      return ingredientGrid(selIngs, selected, opts) + (selRecs.length ? recipeGrid(selRecs, selected) : "");
    }

    // intermediates group：应用分类 + 搜索（与食材 tab 一致）
    if (activeGroup === "__intermediate__" && hasIntermediates) {
      const filtered = pickerIntermediates.filter(recipeCatMatch).filter(recipeTextMatch);
      return filtered.length ? recipeGrid(filtered, selected) : '<p class="modal-hint">没有匹配的中间产物</p>';
    }

    let filtered = ingredients;
    if (activeGroup) filtered = filtered.filter((i) => (i.group ?? "other") === activeGroup);
    filtered = filtered.filter(catMatch);
    filtered = filtered.filter(textMatch);
    let html = ingredientGrid(filtered, selected, opts);
    // 搜索时：在食材分组下也列出名称匹配的中间产物（不必先切到「中间产物」tab）
    if (q && activeGroup !== "__intermediate__" && hasIntermediates) {
      const matched = pickerIntermediates.filter(recipeCatMatch).filter(recipeTextMatch);
      if (matched.length) html += recipeGrid(matched, selected);
    }
    return html;
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
    `${cancelBtnHtml()}${opts?.single ? `\n     ${modalBtnHtml("清除设置", "modal-btn", { "data-clear": "" })}` : ""}
     ${primaryBtnHtml()}`
  );

  const panel = document.querySelector(".modal-panel");
  if (panel) {
    panel.classList.add("wide");
    // 定高面板：列表区撑满剩余空间（约 8 行卡片），见 .modal-panel.ing-pick 样式
    panel.classList.add("ing-pick");
  }
  // 分组 tab 与分类 chips 各自独立高亮/过滤（data-group / data-cat）
  document.getElementById("ing-pick-search")?.addEventListener("input", applyFilter);
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

  // 勾选即时同步进 state（事件委托挂在容器上——applyFilter 只替换 innerHTML，
  // 容器与监听器存活；单选 radio 换选时先清空再写入唯一值）。
  const syncSelectedFromInput = (t: EventTarget | null) => {
    const el = t as HTMLInputElement | null;
    if (!el || (el.type !== "checkbox" && el.type !== "radio")) return;
    if (opts?.single) {
      if (el.type === "radio" && el.checked) {
        selected.clear();
        selected.add(el.value);
      }
    } else if (el.checked) {
      selected.add(el.value);
    } else {
      selected.delete(el.value);
    }
    const selBtn = document.querySelector<HTMLButtonElement>('.ing-group-btn[data-group="__selected__"]');
    if (selBtn) selBtn.textContent = `✓ 已选 (${selected.size})`;
  };
  document.getElementById("ing-pick-container")?.addEventListener("change", (e) => {
    syncSelectedFromInput(e.target);
  });

  // 确定从 state 收集（而非当前过滤视图的 DOM），跨分组/分类/搜索的勾选全部保留。
  document.querySelector("[data-ok]")?.addEventListener("click", () => {
    onSave(opts?.single ? [...selected].slice(-1) : [...selected]);
    closeModal();
  });
}

/** 随机食材箱编辑器：图标样式单选 + 分组/分类/搜索筛选 + 一键填充本关所需 +
 *  多选候选食材（初始配额默认 5）。候选 <2 时禁用确定（写回强校验的前端闸口）。 */
export function openRandomCrateEditor(
  ingredients: IngredientEntry[],
  current: { guids: string[]; weights: number[]; iconGuid: string; airWeight?: number },
  onSave: (guids: string[], weights: number[], iconGuid: string, airWeight: number) => void
) {
  const styles = getQuestionMarkStyles();
  let iconGuid = current.iconGuid || styles.find((s) => s.isDefault)?.guid || styles[0]?.guid || "";
  const state = new Map<string, number>();
  current.guids.forEach((g, i) => {
    if (g) state.set(g, current.weights[i] != null && current.weights[i] >= 1 ? current.weights[i] : 5);
  });
  // 空气候（独立于食材 Map）：≥1 启用，默认份数与食材一致取 5
  let airOn = (current.airWeight ?? 0) >= 1;
  let airW = current.airWeight != null && current.airWeight >= 1 ? current.airWeight : 5;

  const styleRow = (): string =>
    styles.length
      ? `<div class="qm-style-row">${styles
          .map(
            (s) => `<label class="qm-style${s.guid === iconGuid ? " selected" : ""}" title="${s.name}">
        <input type="radio" name="qm-style" value="${s.guid}" ${s.guid === iconGuid ? "checked" : ""}>
        <img src="${questionMarkIconUrl(s.guid)}" alt="">
        <span>${questionMarkZhLabel(s)}</span>
      </label>`
          )
          .join("")}</div>`
      : `<p class="modal-hint">图标样式列表加载中（保存后默认厨师帽样式）</p>`;

  const card = (ing: IngredientEntry): string => {
    const reason = ing.nodeOnly ? "node 型食材（无实体 prefab），食材箱无法生成" : null;
    const checked = !reason && state.has(ing.guid);
    const badge =
      ing.group && ing.group !== "core" ? ` <span class="pc-badge">${foodGroupLabel(ing.group)}</span>` : "";
    const en = (ing.nameEn && ing.nameEn.trim()) || "";
    const w = state.get(ing.guid) ?? 5;
    return `<div class="pick-card rc-card${checked ? " selected" : ""}${reason ? " pick-card-disabled" : ""}"${reason ? ` title="${reason}"` : ""}>
      <label class="rc-card-main"><input type="checkbox" value="${ing.guid}" ${checked ? "checked" : ""} ${reason ? "disabled" : ""}>
      ${iconImg("ingredients", ing.id, ing.icon)}<span class="pc-name">${ing.nameZh}${badge}${reason ? ' <span class="pc-badge pc-badge-disabled">⛔ 禁用</span>' : ""}${en ? ` <span class="muted pc-en">${en}</span>` : ""}</span></label>
      <input type="number" class="rc-weight" data-guid="${ing.guid}" step="1" min="1" value="${w}" title="初始权重/配额（取出 -1，归零回满）">
    </div>`;
  };

  // ---- 筛选（分组 tab + 分类 chips + 搜索），同 openIngredientMultiPicker 模式 ----
  const groups = [...new Set(ingredients.map((i) => i.group ?? "other"))]
    .filter((g) => g !== "core")
    .sort();
  const cats = INGREDIENT_CATEGORIES.filter((c) =>
    ingredients.some((i) => ingredientCategoryOf(i.id) === c.key)
  );
  let activeGroup = "";
  let activeCat = "";

  const buildFiltered = (): string => {
    const q = (document.getElementById("rc-search") as HTMLInputElement)?.value?.trim()?.toLowerCase() ?? "";
    const catMatch = (i: IngredientEntry) => !activeCat || ingredientCategoryOf(i.id) === activeCat;
    const textMatch = (i: IngredientEntry) =>
      !q || i.nameZh.toLowerCase().includes(q) || (i.nameEn ?? "").toLowerCase().includes(q) || i.id.toLowerCase().includes(q);
    let list: IngredientEntry[];
    if (activeGroup === "__selected__") {
      list = ingredients.filter((i) => state.has(i.guid) && textMatch(i));
    } else if (activeGroup) {
      list = ingredients.filter((i) => (i.group ?? "other") === activeGroup && catMatch(i) && textMatch(i));
    } else {
      list = ingredients.filter((i) => catMatch(i) && textMatch(i));
    }
    return `<div class="pick-grid">${list.map(card).join("")}</div>`;
  };

  const groupTabs = (): string => `
    <div class="ing-groups">
      <button type="button" class="ing-group-btn${activeGroup === "" ? " active" : ""}" data-group="">全部</button>
      ${groups.map((g) => `        <button type="button" class="ing-group-btn${activeGroup === g ? " active" : ""}" data-group="${g}">${foodGroupLabel(g)}</button>${""}`).join("")}
      <button type="button" class="ing-group-btn${activeGroup === "__selected__" ? " active" : ""}" data-group="__selected__" title="只显示已勾选的候选，便于审查与取消">✓ 已选 (${state.size})</button>
    </div>`;
  const catChips = (): string =>
    cats.length > 1
      ? `
    <div class="ing-filter-bar ing-cat-bar">
      <span class="ing-cat-label">分类</span>
      <div class="ing-groups">
        <button type="button" class="ing-group-btn${activeCat === "" ? " active" : ""}" data-cat="">全部</button>
        ${cats.map((c) => `        <button type="button" class="ing-group-btn${activeCat === c.key ? " active" : ""}" data-cat="${c.key}">${c.label}</button>${""}`).join("")}
      </div>
    </div>`
      : "";

  const applyFilter = (): void => {
    const container = document.getElementById("rc-container");
    if (container) container.innerHTML = buildFiltered();
    const tabs = document.getElementById("rc-groups");
    if (tabs) tabs.innerHTML = groupTabs();
    const chips = document.getElementById("rc-cats");
    if (chips) chips.innerHTML = catChips();
    bindGrid();
    bindTabs();
  };

  // 空气候卡片（弹窗顶部固定，不随筛选重建；图标 404 时显示 ∅ 文字占位）
  const airCard = (): string =>
    `<div class="pick-card rc-card rc-air-card${airOn ? " selected" : ""}" title="空气：取出时不产出食材（玩家手空），与其他候选同样的权重管理与保底">
      <label class="rc-card-main"><input type="checkbox" id="rc-air-toggle" ${airOn ? "checked" : ""}>
        <span class="rc-air-icon-wrap"><img src="${crateAirIconUrl()}" alt="空气"
          onerror="this.style.display='none';this.parentNode.querySelector('.rc-air-glyph').style.display=''"><span class="rc-air-glyph">∅</span></span>
        <span class="pc-name">空气（取出不产出）<span class="muted pc-en">Air</span></span></label>
      <input type="number" class="rc-weight" id="rc-air-weight" step="1" min="1" value="${airW}"
        title="空气每轮份数（与食材权重同语义）" ${airOn ? "" : "disabled"}>
    </div>`;

  openModal(
    "随机食材箱 · 图标与候选",
    `<p class="modal-hint">问号图标样式（盖子显示）· 真实食材 ≥1 且候选（含空气）≥2 · 每轮份数（每轮 sum 份取用恰好出齐各份数，默认 5）</p>
     ${styleRow()}
     <div id="rc-air-holder">${airCard()}</div>
     <div class="ing-filter-bar">
       <input type="search" id="rc-search" class="ing-search" placeholder="搜索食材…" />
       <button type="button" class="ing-group-btn" id="rc-fill-required" title="勾选本关已保存菜谱所需的全部叶食材（机器产出的汽水/饮料/奶油除外）">⚡ 填充本关所需</button>
       <span id="rc-fill-note" class="muted" style="font-size:11px"></span>
     </div>
     <div class="ing-filter-bar" id="rc-groups">${groupTabs()}</div>
     <div id="rc-cats">${catChips()}</div>
     <div class="modal-scroll" id="rc-container">${buildFiltered()}</div>`,
    modalFooterBtnsHtml()
  );

  const panel = document.querySelector(".modal-panel");
  if (panel) {
    panel.classList.add("wide");
    panel.classList.add("qm-editor");
  }

  const bindAirCard = (): void => {
    const holder = document.getElementById("rc-air-holder");
    const toggle = document.getElementById("rc-air-toggle") as HTMLInputElement | null;
    const weightInput = document.getElementById("rc-air-weight") as HTMLInputElement | null;
    toggle?.addEventListener("change", () => {
      airOn = toggle.checked;
      const cardEl = holder?.querySelector(".rc-air-card");
      cardEl?.classList.toggle("selected", airOn);
      if (weightInput) weightInput.disabled = !airOn;
      updateOk();
    });
    weightInput?.addEventListener("change", () => {
      const w = parseFloat(weightInput.value);
      if (isFinite(w) && w >= 1) airW = w;
      else weightInput.value = String(airW);
    });
  };

  const updateOk = (): void => {
    const ok = document.querySelector<HTMLButtonElement>("[data-ok]");
    if (ok) {
      const total = state.size + (airOn ? 1 : 0);
      ok.disabled = total < 2 || state.size < 1;
      ok.textContent = total > 0 ? `确定（已选 ${state.size} 种食材${airOn ? " + 空气" : ""}）` : "确定";
    }
  };

  const bindGrid = (): void => {
    document.querySelectorAll<HTMLInputElement>("#rc-container .rc-card input[type=checkbox]").forEach((cb) => {
      cb.addEventListener("change", () => {
        const cardEl = cb.closest(".pick-card");
        if (cb.checked) {
          const wInput = cardEl?.querySelector<HTMLInputElement>(".rc-weight");
          const w = parseFloat(wInput?.value ?? "5");
          state.set(cb.value, isFinite(w) && w >= 1 ? w : 5);
          cardEl?.classList.add("selected");
        } else {
          state.delete(cb.value);
          cardEl?.classList.remove("selected");
        }
        updateOk();
        // 已选 tab 的计数随勾选联动
        const sel = document.querySelector<HTMLButtonElement>('.ing-group-btn[data-group="__selected__"]');
        if (sel) sel.textContent = `✓ 已选 (${state.size})`;
      });
    });
    document.querySelectorAll<HTMLInputElement>("#rc-container .rc-weight").forEach((inp) => {
      inp.addEventListener("change", () => {
        if (!state.has(inp.dataset.guid ?? "")) return;
        const w = parseFloat(inp.value);
        if (isFinite(w) && w >= 1) state.set(inp.dataset.guid ?? "", w);
        else inp.value = String(state.get(inp.dataset.guid ?? "") ?? 5);
      });
    });
  };

  const bindTabs = (): void => {
    document.querySelectorAll<HTMLButtonElement>("#rc-groups .ing-group-btn[data-group]").forEach((btn) => {
      btn.addEventListener("click", () => {
        activeGroup = btn.dataset.group ?? "";
        applyFilter();
      });
    });
    document.querySelectorAll<HTMLButtonElement>("#rc-cats .ing-group-btn[data-cat]").forEach((btn) => {
      btn.addEventListener("click", () => {
        activeCat = btn.dataset.cat ?? "";
        applyFilter();
      });
    });
  };

  bindGrid();
  bindTabs();
  bindAirCard();
  updateOk();

  document.querySelectorAll<HTMLInputElement>(".qm-style input[type=radio]").forEach((radio) => {
    radio.addEventListener("change", () => {
      iconGuid = radio.value;
      document.querySelectorAll(".qm-style").forEach((el) => el.classList.remove("selected"));
      radio.closest(".qm-style")?.classList.add("selected");
    });
  });

  document.getElementById("rc-search")?.addEventListener("input", applyFilter);

  // 一键填充本关所需（菜谱叶食材展开；已勾选的保留原配额，仅补充缺失项）
  document.getElementById("rc-fill-required")?.addEventListener("click", async () => {
    const note = document.getElementById("rc-fill-note");
    if (note) note.textContent = "计算中…";
    const ids = await levelRequiredCrateIngredientIds();
    let added = 0;
    let skipped = 0;
    for (const id of ids) {
      const ing = ingredients.find((i) => i.id === id);
      if (!ing || ing.nodeOnly) {
        skipped++;
        continue;
      }
      if (!state.has(ing.guid)) {
        state.set(ing.guid, 5);
        added++;
      }
    }
    if (note)
      note.textContent = ids.length
        ? `本关所需 ${ids.length} 项：新增 ${added}，已有 ${ids.length - added}${skipped ? `，跳过 ${skipped}` : ""}`
        : "本关未找到已保存的菜谱（先在菜谱页配置）";
    activeGroup = "__selected__";
    applyFilter();
    updateOk();
  });

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", () => {
    if (state.size + (airOn ? 1 : 0) < 2 || state.size < 1) return;
    const guids = [...state.keys()];
    const weights = guids.map((g) => state.get(g) ?? 1);
    onSave(guids, weights, iconGuid, airOn ? airW : 0);
    closeModal();
  });
}

/** 图标样式的中文标签（文件名 → 中文）。 */
function questionMarkZhLabel(style: { name: string }): string {
  const map: Record<string, string> = {
    question_mark_chef_hat: "厨师帽问号",
    question_mark_blue: "蓝色问号",
    question_mark_brown: "棕色问号",
    question_mark_gray: "灰色问号",
    question_mark_green: "绿色问号",
    question_mark_orange: "橙色问号",
    question_mark_pink: "粉色问号",
    question_mark_purple: "紫色问号",
    question_mark_red: "红色问号",
    question_mark_yellow: "黄色问号",
    question_mark_food: "食物问号",
    question_mark_theme: "主题色问号",
  };
  return map[style.name] ?? style.name;
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
    `${cancelBtnHtml()} ${primaryBtnHtml("保存")}`
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
