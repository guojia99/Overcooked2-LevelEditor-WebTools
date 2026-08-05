import * as api from "./api";
import type {
  CustomRecipeCategory,
  CustomRecipeConfig,
  CustomRecipeEdit,
  CustomRecipeReferences,
  CustomRecipeSummary,
  IngredientEntry,
  LevelSetInfo,
} from "./types";
import { showBusy, hideBusy } from "./busy";
import { navHtml, wireNav } from "./nav";
import { foodGroupLabel } from "./ingredientLabels";
import { closeModal, openModal } from "./modals";

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
    <div class="cr-warn-banner">⚠️ 本功能研发中，请勿使用</div>
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

// ==================== Recipe List ====================

async function renderRecipeList(app: HTMLElement, setName: string): Promise<void> {
  const content = shell(app, `自定义菜谱 · ${esc(setName)}`);
  setBusy("加载菜谱配置…");

  let config: CustomRecipeConfig;
  let recipes: CustomRecipeSummary[];
  try {
    config = await api.fetchCustomRecipeConfig(setName);
    recipes = await api.fetchCustomRecipes(setName);
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

  function renderGrid(catId: string): string {
    const filtered = catId
      ? recipes.filter((r) => r.category === catId)
      : recipes;

    if (filtered.length === 0) {
      return '<p class="muted">该分类下暂无菜谱</p>';
    }

    return `<div class="m-grid">${filtered
      .map(
        (r) => `
      <div class="m-card cr-card">
        <div class="cr-card-head">
          ${r.hasIcon ? `<img class="cr-icon" src="/api/level/data-file?path=${encodeURIComponent(r.assetPath.replace(/_SO\.asset$/, ""))}/models/${encodeURIComponent(r.id.replace(/_SO$/, ""))}_Icon.png" alt="" onerror="this.style.display='none'" />` : ""}
          <span class="cr-type-badge">${esc(r.type)}</span>
          ${r.score > 0 ? "" : '<span class="m-badge warn">半成品</span>'}
        </div>
        <h4>${esc(r.nameZh || r.recipeName)}</h4>
        <div class="m-meta">
          ${r.nameEn ? `${esc(r.nameEn)}<br>` : ""}
          id: ${esc(r.recipeName)}<br>
          UID: ${r.uID} · 分数: ${r.score}<br>
          食材: ${(r.compositionIds ?? []).length} 种
          ${r.cookingStepId ? ` · ${esc(r.cookingStepId)}` : ""}
          ${r.hasModel ? ' · <span class="ok">模型</span>' : ""}
        </div>
        <div class="m-actions">
          <button class="m-btn" data-edit="${esc(r.assetPath)}">编辑</button>
          <button class="m-btn danger" data-del="${esc(r.assetPath)}">删除</button>
        </div>
      </div>`
      )
      .join("")}</div>`;
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
    <div class="cr-layout">
      <div id="cr-sidebar">${renderSidebar()}</div>
      <div id="cr-grid">${renderGrid("")}</div>
    </div>
  `;

  function refreshView(): void {
    void (async () => {
      showBusy("加载…");
      try {
        config = await api.fetchCustomRecipeConfig(setName);
        recipes = await api.fetchCustomRecipes(setName);
      } catch (e) {
        showError(e);
        return;
      } finally {
        hideBusy();
      }
      setStatus(`${recipes.length} 个菜谱 · UID前缀：${config.uidPrefix}`);
      document.getElementById("cr-sidebar")!.innerHTML = renderSidebar();
      wireSidebar();
      document.getElementById("cr-grid")!.innerHTML = renderGrid(activeCategoryId);
      wireGridButtons();
    })();
  }

  function wireSidebar(): void {
    document.querySelectorAll<HTMLButtonElement>(".cr-cat-item").forEach((b) => {
      b.addEventListener("click", () => {
        activeCategoryId = b.dataset.cat ?? "";
        document.getElementById("cr-sidebar")!.innerHTML = renderSidebar();
        wireSidebar();
        document.getElementById("cr-grid")!.innerHTML = renderGrid(activeCategoryId);
        wireGridButtons();
      });
    });
    document.getElementById("cr-new-cat")?.addEventListener("click", () => openNewCategoryModal(setName, refreshView));
    document.getElementById("cr-manage-cat")?.addEventListener("click", () => openManageCategoriesModal(setName, config.categories, refreshView));
  }

  function wireGridButtons(): void {
    document.querySelectorAll<HTMLButtonElement>("[data-edit]").forEach((b) =>
      b.addEventListener("click", () => void renderRecipeForm(app, setName, b.dataset.edit!))
    );
    document.querySelectorAll<HTMLButtonElement>("[data-del]").forEach((b) =>
      b.addEventListener("click", () => confirmDeleteRecipe(app, setName, b.dataset.del!, refreshView))
    );
  }

  wireSidebar();
  wireGridButtons();

  document.getElementById("cr-back")?.addEventListener("click", () => void renderCustomRecipesView(app));
  document.getElementById("cr-new-recipe")?.addEventListener("click", () => void renderRecipeForm(app, setName, null));
}

// ==================== Recipe Form Page (Create / Edit) ====================

async function renderRecipeForm(
  app: HTMLElement,
  setName: string,
  assetPath: string | null
): Promise<void> {
  const isEdit = assetPath != null;
  const content = shell(app, isEdit ? "编辑菜谱" : "新建菜谱");
  setBusy("加载参考数据…");

  let ingredients: IngredientEntry[] = [];
  let refs: CustomRecipeReferences;
  let config: CustomRecipeConfig;
  let recipe: CustomRecipeSummary | undefined;

  try {
    [ingredients, refs, config] = await Promise.all([
      api.fetchIngredients().catch(() => [] as IngredientEntry[]),
      api.fetchCustomRecipeReferences(setName),
      api.fetchCustomRecipeConfig(setName),
    ]);
    if (isEdit) {
      const all = await api.fetchCustomRecipes(setName);
      recipe = all.find((r) => r.assetPath === assetPath);
    }
  } catch (e) {
    showError(e);
    hideBusy();
    return;
  }
  hideBusy();

  const isNew = !isEdit;
  const recipeName = recipe?.recipeName ?? "";
  const nameZh = recipe?.nameZh ?? "";
  const nameEn = recipe?.nameEn ?? "";
  const categoryId = recipe?.category ?? (config.categories?.length > 0 ? config.categories[0].id : "");
  const score = recipe?.score ?? 0;
  const type = recipe?.type ?? "Cooked";
  const compIds = recipe?.compositionIds ?? [];
  const cookingStepId = recipe?.cookingStepId ?? "";
  const cookingStepIconId = "";
  const platingStepId = recipe?.platingStepId ?? "";
  const mixingIconId = "";
  const modelPrefabId = "";

  const categories = config.categories ?? [];

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

  function ingCardHtml(ing: IngredientEntry, checked: boolean): string {
    const badge = ing.group && ing.group !== "core" ? ` <span class="pc-badge">${foodGroupLabel(ing.group)}</span>` : "";
    const en = (ing.nameEn && ing.nameEn.trim()) || "";
    return `<label class="pick-card${checked ? " selected" : ""}" data-guid="${ing.guid}">
      <input type="checkbox" value="${esc(ing.id)}" data-ingid="${esc(ing.id)}" ${checked ? "checked" : ""}>
      <span class="pc-head"><img class="food-icon" src="/icons/ingredients/${esc(ing.id)}.png" alt="" onerror="this.src='/icons/_placeholder.png'" /><span class="pc-name">${esc(ing.nameZh)}${badge}${en ? ` <span class="muted pc-en">${en}</span>` : ""}</span></span>
    </label>`;
  }

  const ingGrid = `<div class="pick-grid">${ingredients
    .map((i) => ingCardHtml(i, compIds.includes(i.id)))
    .join("")}</div>`;

  content.innerHTML = `
    <div class="m-actions-row">
      <button class="m-btn" id="cr-form-back">← 返回菜谱列表</button>
      <span class="muted">关卡集：<b>${esc(setName)}</b> · ${isEdit ? `编辑 ${esc(recipeName)}` : '新建菜谱'}</span>
      <span style="flex:1"></span>
      <button class="m-btn primary" id="cr-form-save">💾 保存</button>
    </div>
    <div class="modal-scroll" style="max-height:calc(100vh - 160px);padding:0 8px;">
    <label class="m-field">标识符 recipeName（仅字母/数字/下划线，创建后不可修改）
      <input type="text" id="cr-rec-name" value="${esc(recipeName)}" ${isEdit ? "disabled" : ""} placeholder="MyRecipe">
    </label>
    <div class="m-row">
      <label class="m-field">中文名<input type="text" id="cr-zh" value="${esc(nameZh)}" placeholder="我的菜谱"></label>
      <label class="m-field">英文名<input type="text" id="cr-en" value="${esc(nameEn)}" placeholder="My Recipe"></label>
    </div>
    <label class="m-field">分类 ${catSelectHtml()}
      <button type="button" class="m-btn" id="cr-new-cat-inline" style="margin-left:8px">+ 新建分类</button>
    </label>
    <div class="m-row">
      <label class="m-field">类型
        <select id="cr-type" class="m-select">
          <option value="Composite" ${type === "Composite" ? "selected" : ""}>Composite（组合）</option>
          <option value="Cooked" ${type === "Cooked" ? "selected" : ""}>Cooked（烹饪）</option>
          <option value="Mixed" ${type === "Mixed" ? "selected" : ""}>Mixed（搅拌）</option>
        </select>
      </label>
      <label class="m-field">分数<input type="number" id="cr-score" value="${score}" min="0"></label>
    </div>
    <label class="m-field">UID（自动生成）<input type="text" value="${recipe?.uID ?? (isNew ? config.uidPrefix * 1000 + config.nextSequence : "—")}" disabled></label>
    <div class="m-section-title">食材</div>
    <p class="modal-hint">选择菜谱所需的食材（至少一种）</p>
    <div id="cr-ing-grid">${ingGrid}</div>
    <div class="m-row">
      <label class="m-field">烹饪步骤 ${selectHtml(refs.cookingSteps, cookingStepId, "cr-cook-step")}</label>
      <label class="m-field">烹饪图标 ${selectHtml(refs.icons, cookingStepIconId, "cr-cook-icon")}</label>
    </div>
    <div class="m-row" id="cr-cook-row">
      <label class="m-field">烹饪程度
        <select id="cr-cook-prog" class="m-select">
          <option value="0">Raw（生）</option>
          <option value="1" selected>Cooked（熟）</option>
          <option value="2">Burnt（焦）</option>
        </select>
      </label>
    </div>
    <label class="m-field">装盘步骤 ${selectHtml(refs.platingSteps, platingStepId, "cr-plate-step")}</label>
    <div class="m-row" id="cr-mix-row" style="display:none">
      <label class="m-field">搅拌图标 ${selectHtml(refs.icons, mixingIconId, "cr-mix-icon")}</label>
    </div>
    <div class="m-row" id="cr-mix-prog-row" style="display:none">
      <label class="m-field">搅拌程度
        <select id="cr-mix-prog" class="m-select">
          <option value="0">Unmixed（未搅拌）</option>
          <option value="1" selected>Mixed（已搅拌）</option>
          <option value="2">OverMixed（过度搅拌）</option>
        </select>
      </label>
    </div>
    <div class="m-section-title">模型</div>
    <div class="m-row">
      <label class="m-field">复用已有模型 ${selectHtml(refs.reusableModels, modelPrefabId, "cr-model-ref")}</label>
    </div>
    <p class="modal-hint">或上传新的 3D 模型（FBX/OBJ）</p>
    <div class="m-row">
      <label class="m-field">上传图标（PNG，用作菜谱卡片图）<input type="file" id="cr-icon-upload" accept="image/png"></label>
      <label class="m-field">上传 3D 模型<input type="file" id="cr-model-upload" accept=".fbx,.obj"></label>
    </div>
    </div>
  `;

  function updateTypeFields(): void {
    const sel = (document.getElementById("cr-type") as HTMLSelectElement).value;
    const cookRow = document.getElementById("cr-cook-row");
    const cookStep = document.getElementById("cr-cook-step")?.closest(".m-field");
    const mixRow = document.getElementById("cr-mix-row");
    const mixProgRow = document.getElementById("cr-mix-prog-row");

    if (cookRow) cookRow.style.display = sel === "Composite" ? "none" : "";
    if (cookStep) (cookStep as HTMLElement).style.display = sel === "Composite" ? "none" : "";
    if (mixRow) mixRow.style.display = sel === "Mixed" ? "" : "none";
    if (mixProgRow) mixProgRow.style.display = sel === "Mixed" ? "" : "none";
  }

  document.getElementById("cr-type")?.addEventListener("change", updateTypeFields);
  updateTypeFields();

  document.getElementById("cr-new-cat-inline")?.addEventListener("click", () => openNewCategoryModalInline(setName, async () => {
    config = await api.fetchCustomRecipeConfig(setName);
    const sel = document.getElementById("cr-type-cat") as HTMLSelectElement;
    if (sel) {
      const last = config.categories[config.categories.length - 1];
      if (last) {
        sel.innerHTML = config.categories.map((c) => `<option value="${esc(c.id)}">${esc(c.zh || c.id)}</option>`).join("");
        sel.value = last.id;
      }
    }
  }));

  document.getElementById("cr-form-back")?.addEventListener("click", () => void renderRecipeList(app, setName));

  document.getElementById("cr-form-save")?.addEventListener("click", async () => {
    const rname = (document.getElementById("cr-rec-name") as HTMLInputElement).value.trim();
    if (!rname) return alert("请填写标识符");
    if (!IDENT_RE.test(rname)) return alert("标识符仅允许英文字母/数字/下划线，且不能以数字开头");

    const dto: CustomRecipeEdit = {
      setName: isEdit ? undefined : setName,
      assetPath: isEdit ? assetPath! : undefined,
      recipeName: rname,
      nameZh: (document.getElementById("cr-zh") as HTMLInputElement).value.trim(),
      nameEn: (document.getElementById("cr-en") as HTMLInputElement).value.trim(),
      category: (document.getElementById("cr-type-cat") as HTMLSelectElement).value,
      score: Number((document.getElementById("cr-score") as HTMLInputElement).value) || 0,
      type: (document.getElementById("cr-type") as HTMLSelectElement).value,
      compositionIds: [],
      cookingStepId: (document.getElementById("cr-cook-step") as HTMLSelectElement)?.value ?? "",
      cookingStepIconId: (document.getElementById("cr-cook-icon") as HTMLSelectElement)?.value ?? "",
      platingStepId: (document.getElementById("cr-plate-step") as HTMLSelectElement)?.value ?? "",
      mixingIconId: (document.getElementById("cr-mix-icon") as HTMLSelectElement)?.value ?? "",
      modelPrefabId: (document.getElementById("cr-model-ref") as HTMLSelectElement)?.value ?? "",
      cookingProgress: Number((document.getElementById("cr-cook-prog") as HTMLSelectElement)?.value ?? "1") || 1,
      mixingProgress: Number((document.getElementById("cr-mix-prog") as HTMLSelectElement)?.value ?? "1") || 1,
    };

    document.querySelectorAll<HTMLInputElement>("#cr-ing-grid input:checked").forEach((el) => {
      const ingId = el.dataset.ingid;
      if (ingId) dto.compositionIds.push(ingId);
    });

    showBusy("保存中…");
    try {
      if (isEdit) {
        await api.updateCustomRecipe(dto);
      } else {
        await api.createCustomRecipe(dto);
      }

      const actualPath = assetPath || `Assets/LevelSets/${setName}/custom_recipes/${dto.category}/${rname}.asset`;

      const iconFile = (document.getElementById("cr-icon-upload") as HTMLInputElement).files?.[0];
      const modelFile = (document.getElementById("cr-model-upload") as HTMLInputElement).files?.[0];

      if (iconFile) {
        const base64 = await fileToBase64(iconFile);
        await api.uploadCustomRecipeIcon(setName, actualPath, iconFile.name, base64);
      }

      if (modelFile) {
        const base64 = await fileToBase64(modelFile);
        await api.uploadCustomRecipeModel(setName, actualPath, modelFile.name, base64);
      }

      setStatus(isEdit ? "已更新菜谱" : "已创建菜谱");
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
}

// ==================== Delete Confirmation ====================

function confirmDeleteRecipe(_app: HTMLElement, _setName: string, assetPath: string, onDone: () => void): void {
  const fileName = assetPath.split("/").pop() ?? assetPath;
  openModal(
    `删除菜谱 · ${esc(fileName)}`,
    `<p>将永久删除菜谱资源及其模型文件夹，且<b>不可恢复</b>。</p>`,
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

function openNewCategoryModal(setName: string, onDone: () => void): void {
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
      onDone();
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });
}

function openNewCategoryModalInline(setName: string, onDone: () => void): void {
  const id = prompt("分类ID（仅字母/数字/下划线）：");
  if (!id || !IDENT_RE.test(id)) { alert("ID非法"); return; }
  const zh = prompt("分类中文名：") || id;
  const en = prompt("分类英文名：") || id;
  showBusy("创建分类…");
  api.addCustomRecipeCategory(setName, id, zh, en)
    .then(() => { setStatus("已创建分类"); onDone(); })
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
