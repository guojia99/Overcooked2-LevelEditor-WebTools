import {
  S,
  CELL
} from "../state";
import {
  computeRequiredUtensils,
  STEP_UTENSILS,
  computeIntermediatesForUtensils,
  recipeNeedsCreamSpray,
  recipeLacksIntermediate,
  missingIntermediateRecipes,
  functionalBaseId,
  CREAM_SPRAY_IDS,
  CREAM_INGREDIENT_IDS
} from "../recipeKnowledge";
import {
  prefabIdFromPath,
  escHtml
} from "../coords";
import {
  catalogItemById,
  ingredientEntryById,
  foodIconImg,
  customRecipeIconUrl,
  ingredientGuidById,
  ingredientIdByGuid,
  ingredientNameById
} from "../catalog";
import {
  openModal,
  closeModal
} from "../../modals";
import { setStatus } from "../status";
import {
  addFromCatalog,
  placementBase
} from "../items";
import { defaultUtensilCapacity } from "../stubControls";
import { foodGroupLabel, visibleRecipes } from "../../ingredientLabels";
import {
  groupRecipesByType,
  recipeTypeLabel
} from "../../recipeTypes";
import { tidyCatalogNameZh } from "../../displayLabels";
import {
  fetchHealthInfo,
  STALE_BRIDGE_MSG,
  fetchRecipeCatalog,
  fetchLevelRecipes,
  saveLevelRecipes,
  syncLevelSetWeb
} from "../../api";
import type { RecipeEntry } from "../../types";

export type RecipeTab = "select" | "selected" | "autofill";

export interface RecipesDialogOptions {
  openTab?: RecipeTab;
  /** 关卡集（缺省用当前场景关卡集）。 */
  setName?: string;
}

const TAB_META: Record<RecipeTab, { label: string; emoji: string; needScene: boolean }> = {
  select: { label: "选择菜谱", emoji: "🍽️", needScene: true },
  selected: { label: "已选菜谱", emoji: "✅", needScene: true },
  autofill: { label: "自动填充道具", emoji: "🧺", needScene: true },
};

export async function openRecipesDialog(opts: RecipesDialogOptions = {}) {
  const health = await fetchHealthInfo();
  if (!health.recipeApi) {
    setStatus(STALE_BRIDGE_MSG, false);
    return;
  }
  const hasScene = !!S.scenePath;
  const levelSet = opts.setName ?? S.currentLevelSet ?? "";

  // 打开关卡集：版本不一致立即同步一次；v0.0.0 不同步且 web 内置禁用。
  let activeTab: RecipeTab = opts.openTab ?? "select";
  if (levelSet) {
    try {
      const sync = await syncLevelSetWeb(levelSet);
      S.webSyncVersion = sync.version;
      S.webSyncDisabled = sync.disabled;
      if (sync.changed > 0) setStatus(`已同步 Web 内置内容（${sync.changed} 项）`);
    } catch {
      /* 版本/同步接口不可用不影响菜谱弹窗 */
    }
  }

  // ---------- 选择/已选 数据 ----------
  let recipes: RecipeEntry[] = [];
  let level: { levelInfoAssetPath: string; levelName: string; recipeGuids: string[]; recipeIds?: string[] } | null = null;
  if (hasScene) {
    try {
      [recipes, level] = await Promise.all([
        fetchRecipeCatalog(S.currentLevelSet),
        fetchLevelRecipes(S.scenePath),
      ]);
    } catch (e) {
      setStatus((e as Error).message, false);
      return;
    }
    if (!level?.levelInfoAssetPath) {
      setStatus("未找到该场景对应的 LevelInfoSO", false);
      return;
    }
  }

  const selected = new Set<string>(level?.recipeGuids ?? []);
  let orderable: RecipeEntry[] = [];
  let byGuid = new Map<string, RecipeEntry>();
  let levelSetRecipes: RecipeEntry[] = [];
  let webInstalled: RecipeEntry[] = [];
  let coreRecipes: RecipeEntry[] = [];
  S.intermediatesCache = recipes.filter((r) => r.intermediate || r.isCustom);

  /** 从 recipes 重建派生集合（加载或安装/移除后刷新）。 */
  const recomputeGroups = () => {
    byGuid = new Map(recipes.map((r) => [r.guid, r]));
    orderable = recipes.filter((r) => !r.intermediate);
    S.intermediatesCache = recipes.filter((r) => r.intermediate || r.isCustom);
    const vis = visibleRecipes(orderable);
    levelSetRecipes = orderable.filter((r) => r.group === "levelset");
    // 选择菜谱只显示：本关自定义 + 已安装 Web 副本（custom_web）+ Common 核心
    // v0.0.0（web 内置禁用）时不显示任何 Web 内置菜谱
    webInstalled = S.webSyncDisabled
      ? []
      : vis.filter((r) => r.group === "web" && (r.assetPath ?? "").includes("/custom_web/"));
    coreRecipes = vis.filter((r) => r.group !== "levelset" && r.group !== "web");
  };
  recomputeGroups();

  // 勾选判定按「菜谱 id」而非 guid：同 id 的 Import/拷贝视为同一菜谱；保存仍用 guid 集合。
  const selectedIds = new Set<string>();
  /** 由 level 已保存的 guid/id 重建勾选 id 集合（含后端 recipeIds 兜底）。 */
  const syncSelectedIdsFromLevel = () => {
    selectedIds.clear();
    for (const r of recipes) if (selected.has(r.guid)) selectedIds.add(r.id);
    // 兜底：后端返回的菜谱 id 直接加入（已装副本不在当前目录时也能保持勾选）
    for (const id of level?.recipeIds ?? []) if (id) selectedIds.add(id);
  };
  syncSelectedIdsFromLevel();

  const toggleSelect = (guid: string, checked: boolean) => {
    if (checked) {
      selected.add(guid);
      const r = byGuid.get(guid);
      if (r) selectedIds.add(r.id);
    } else {
      selected.delete(guid);
      const r = byGuid.get(guid);
      if (r) {
        const anySame = [...selected].some((g) => byGuid.get(g)?.id === r.id);
        if (!anySame) selectedIds.delete(r.id);
      }
    }
  };

  const catMeta: Record<string, { label: string; emoji: string; color: string }> = {
    levelset: { label: "自定义菜谱", emoji: "🍽️", color: "#3b82f6" },
    web: { label: "已安装 Web 内置", emoji: "🕸️", color: "#7c5cbf" },
    core: { label: "Common", emoji: "📦", color: "#2d6a4f" },
  };

  function recipeCard(r: RecipeEntry): string {
    const checked = selectedIds.has(r.id) ? "checked" : "";
    const cust = r.isCustom ? ` <span class="pc-badge" title="自定义菜谱">🔧</span>` : "";
    const grp =
      r.group && r.group !== "core" && r.group !== "levelset"
        ? ` <span class="pc-badge">${escHtml(foodGroupLabel(r.group))}</span>`
        : "";
    const lsBadge = r.group === "levelset"
      ? ` <span class="pc-badge" style="background:#3b82f6;color:#fff">本关</span>`
      : "";
    const warnBadge = recipeLacksIntermediate(r)
      ? ` <span class="pc-badge" style="background:#b45309;color:#fff" title="该菜谱需要搅拌但缺少对应中间产物（面糊），请勿使用">⚠ 无中间产物</span>`
      : "";
    const chips = (r.ingredients ?? [])
      .map((ingId) => {
        const ing = ingredientEntryById(ingId);
        const name = ing?.nameZh ?? ingId;
        return `<span class="rc-ing" title="${escHtml(ingId)}">${foodIconImg("ingredients", ing?.id, ing?.icon)}${escHtml(name)}</span>`;
      })
      .join("");
    const searchable = `${r.nameZh} ${r.nameEn ?? ""} ${r.id}`.toLowerCase();
    const iconSrc = customRecipeIconUrl(r) ?? (r.id && r.icon !== false ? `/icons/recipes/${encodeURIComponent(r.id)}.png` : "/icons/_placeholder.png");
    return `<label class="pick-card recipe-card" data-name="${escHtml(searchable)}">
      <input type="checkbox" value="${r.guid}" ${checked}>
      <span class="rc-head"><img class="food-icon" loading="lazy" src="${escHtml(iconSrc)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'"><span class="pc-name">${escHtml(r.nameZh)}${grp}${cust}${lsBadge}${warnBadge}</span></span>
      <span class="rc-ings">${chips || '<span class="muted small">无食材</span>'}</span>
    </label>`;
  }

  function typeGroupHtml(cat: string, type: string, items: RecipeEntry[]): string {
    const cards = items.map(recipeCard).join("");
    return `<div class="rw-group" data-cat="${escHtml(cat)}" data-type="${escHtml(type)}">
      <div class="rw-group-header"><span class="rw-group-name">${escHtml(recipeTypeLabel(type))}</span><span class="rw-group-count"><span class="rw-group-sel">0</span>/<span class="rw-group-total">${items.length}</span></span></div>
      <div class="pick-grid recipe-grid">${cards}</div>
    </div>`;
  }

  function catHtml(cat: "levelset" | "web" | "core", items: RecipeEntry[]): string {
    const meta = catMeta[cat];
    // 用传入的 items（已选/已搜索过滤后的子集）分组，而不是预计算的全量 catGroups
    const groups = groupRecipesByType(items).map(([type, arr]) => typeGroupHtml(cat, type, arr)).join("");
    if (!groups) return "";
    return `<div class="rw-cat" data-cat="${cat}">
      <div class="rw-cat-header" style="border-color:${meta.color}">
        <span>${meta.emoji} ${meta.label}</span><span class="rw-cat-count">${items.length}</span>
      </div>
      ${groups}
    </div>`;
  }

  function selectedViewHtml(): string {
    const sel = visibleRecipes(orderable).filter((r) => selectedIds.has(r.id));
    if (sel.length === 0) return '<p class="modal-hint">未选择菜谱，勾选左侧菜谱后显示在这里。</p>';
    const byCat: Record<string, RecipeEntry[]> = { levelset: [], web: [], core: [] };
    for (const r of sel) {
      if (r.group === "levelset") byCat.levelset.push(r);
      else if (r.group === "web") byCat.web.push(r);
      else byCat.core.push(r);
    }
    let html = "";
    for (const cat of ["levelset", "web", "core"] as const) {
      if (byCat[cat].length === 0) continue;
      html += catHtml(cat, byCat[cat]);
    }
    return html;
  }

  function listHtmlFor(q: string): string {
    const lower = q.toLowerCase();
    const vis = (items: RecipeEntry[]) =>
      items.filter((r) => {
        if (!lower) return true;
        const hay = `${r.nameZh} ${r.nameEn ?? ""} ${r.id} ${(r.ingredients ?? []).join(" ")}`.toLowerCase();
        return hay.includes(lower);
      });
    if (activeTab === "selected") return selectedViewHtml();
    const parts: string[] = [];
    if (activeTab === "select") {
      if (vis(levelSetRecipes).length) parts.push(catHtml("levelset", vis(levelSetRecipes)));
      if (vis(webInstalled).length) parts.push(catHtml("web", vis(webInstalled)));
      if (vis(coreRecipes).length) parts.push(catHtml("core", vis(coreRecipes)));
    }
    return parts.join("") || '<p class="modal-hint">没有匹配的菜谱</p>';
  }

  const currentRecipes = (): RecipeEntry[] =>
    [...selected].map((g) => byGuid.get(g)).filter((r): r is RecipeEntry => !!r);

  const existingDispenserIngIds = (): Set<string> => {
    const s = new Set<string>();
    for (const it of S.items) {
      if (it.stubKind === "Dispenser") {
        const id = ingredientIdByGuid(it.dispenser?.spawnerItemPrefabGuid);
        if (id) s.add(id);
      }
    }
    return s;
  };
  const existingPrefabIds = (): Set<string> => {
    const s = new Set<string>();
    for (const it of S.items) {
      const id = prefabIdFromPath(it.prefabAssetPath);
      s.add(id);
      s.add(functionalBaseId(id));
    }
    return s;
  };

  const analysisInfo = () => {
    const recs = currentRecipes();
    const reqIngs = new Set<string>();
    const steps = new Set<string>();
    const platingSteps = new Set<string>();
    for (const r of recs) {
      (r.ingredients ?? []).forEach((i) => reqIngs.add(i));
      if (r.cookingStep) steps.add(r.cookingStep);
      if (r.platingStep) platingSteps.add(r.platingStep);
    }
    const haveDisp = existingDispenserIngIds();
    const havePref = existingPrefabIds();
    const missingIngs = [...reqIngs].filter((i) => !haveDisp.has(i));
    const reqUt = computeRequiredUtensils(reqIngs, steps, platingSteps, recs);
    const missingUt = reqUt.filter((u) => !havePref.has(u));
    const missingIntermediateIds = missingIntermediateRecipes(recs).map((r) => r.id);
    return { reqIngs, steps, platingSteps, missingIngs, reqUt, missingUt, missingIntermediateIds };
  };

  // ---------- 自动填充道具（食材箱 + 锅具/道具） ----------
  function fillMissingDispensers() {
    const cbs = document.querySelectorAll<HTMLInputElement>(".rw-ing-cb:checked");
    const selectedIngs = new Set<string>();
    for (const cb of cbs) selectedIngs.add(cb.value);
    if (S.fillIncludeMainDough) {
      if (selectedIngs.has("DLC05_Dough")) selectedIngs.add("DoughSO");
      if (selectedIngs.has("DLC02_ChoppedBun")) selectedIngs.add("ChoppedBunSO");
    }
    if (selectedIngs.size === 0) return;
    const cat = catalogItemById("Dispenser");
    if (!cat) return;
    const base = placementBase();
    let idx = 0;
    const unresolved: string[] = [];
    for (const ing of selectedIngs) {
      const guid = ingredientGuidById(ing);
      if (!guid) {
        unresolved.push(ing);
        continue;
      }
      const it = addFromCatalog(cat, base.x + idx * CELL, base.z);
      if (it) {
        it.dispenser = { spawnerItemPrefabGuid: guid };
        idx++;
      }
    }
    if (unresolved.length > 0) {
      setStatus(`食材箱未创建（目录中找不到对应食材资产）：${unresolved.join("、")}`, false);
    }
  }

  function fillMissingUtensils(selectedUt: Set<string>) {
    const recs = currentRecipes();
    if (selectedUt.size === 0) return;

    const attachBase = new Map<string, string>();
    for (const ids of Object.values(STEP_UTENSILS)) {
      if (ids.length < 2) continue;
      const base = ids[0];
      for (let i = 1; i < ids.length; i++) attachBase.set(ids[i], base);
    }
    const counterStdRule = "counter_standard";
    const autoBases = new Set<string>();
    for (const cat of S.catalogByGuid.values()) {
      if (cat.stack?.hostRule === counterStdRule && !attachBase.has(cat.id)) {
        attachBase.set(cat.id, "Counter");
        autoBases.add("Counter");
      }
    }
    const clusters = new Map<string, string[]>();
    const standalones: string[] = [];
    const clustered = new Set<string>();
    for (const u of selectedUt) {
      const baseId = attachBase.get(u);
      if (baseId && (selectedUt.has(baseId) || autoBases.has(baseId))) {
        if (!clusters.has(baseId)) clusters.set(baseId, []);
        clusters.get(baseId)!.push(u);
        clustered.add(u);
      }
    }
    for (const u of selectedUt) {
      if (!clustered.has(u) && !clusters.has(u)) standalones.push(u);
    }

    const base = placementBase();
    let idx = 0;
    const placedItemIds: string[] = [];
    for (const [baseId, attachments] of clusters) {
      const baseWx = base.x + idx * CELL;
      const baseWz = base.z - 2 * CELL;
      const baseCat = catalogItemById(baseId);
      if (baseCat) { addFromCatalog(baseCat, baseWx, baseWz); placedItemIds.push(baseId); }
      for (const attId of attachments) {
        const attCat = catalogItemById(attId);
        if (!attCat) continue;
        const item = addFromCatalog(attCat, baseWx, baseWz);
        if (item && attCat.stack?.y) item.localPosition.y = attCat.stack.y;
        placedItemIds.push(attId);
      }
      idx++;
    }
    for (const u of standalones) {
      const cat = catalogItemById(u);
      if (!cat) continue;
      addFromCatalog(cat, base.x + idx * CELL, base.z - 2 * CELL);
      placedItemIds.push(u);
      idx++;
    }

    // 奶油喷罐：绑定发泡奶油食材
    const needCream = recs.some(recipeNeedsCreamSpray);
    const ingByGuid = new Map(S.ingredientsCache.map((i) => [i.id, i.guid]));
    if (needCream) {
      const creamGuids = CREAM_INGREDIENT_IDS.map((iid) => ingByGuid.get(iid)).filter((g): g is string => !!g);
      for (const sprayId of CREAM_SPRAY_IDS) {
        const sprayCat = catalogItemById(sprayId);
        if (!sprayCat) continue;
        for (const it of S.items) {
          if (it.prefabGuid !== sprayCat.guid) continue;
          it.stubKind = "CookingUtensil";
          if (!it.cookingUtensil) it.cookingUtensil = {};
          if (it.cookingUtensil.capacity == null) it.cookingUtensil.capacity = defaultUtensilCapacity(it);
          const existing = new Set(it.cookingUtensil.allowedIngredientGuids ?? []);
          for (const g of creamGuids) existing.add(g);
          it.cookingUtensil.allowedIngredientGuids = [...existing];
        }
      }
    }

    // 自动分配中间产物 + 搅拌杯装填
    const blenderIngs = new Set<string>();
    for (const r of recs) {
      if (r.cookingStep === "Blender") (r.ingredients ?? []).forEach((i) => blenderIngs.add(i));
    }
    if (blenderIngs.size > 0 && placedItemIds.includes("BlenderCup")) {
      const guidsToAdd = [...blenderIngs].map((iid) => ingByGuid.get(iid)).filter((g): g is string => !!g);
      if (guidsToAdd.length > 0) {
        const itCat = catalogItemById("BlenderCup");
        for (const it of S.items) {
          if (!itCat || it.prefabGuid !== itCat.guid) continue;
          it.stubKind = "CookingUtensil";
          if (!it.cookingUtensil) it.cookingUtensil = {};
          if (it.cookingUtensil.capacity == null) it.cookingUtensil.capacity = defaultUtensilCapacity(it);
          const existing = new Set(it.cookingUtensil.allowedIngredientGuids ?? []);
          for (const g of guidsToAdd) existing.add(g);
          it.cookingUtensil.allowedIngredientGuids = [...existing];
        }
      }
    }
    if (S.autoIntermediates) {
      const intermediateMap = computeIntermediatesForUtensils(recs);
      for (const [placedId, intIds] of intermediateMap) {
        if (!intIds || intIds.length === 0) continue;
        const itCat = catalogItemById(placedId);
        if (!itCat) continue;
        const guidsToAdd = intIds.map((iid) => ingByGuid.get(iid)).filter((g): g is string => !!g);
        if (guidsToAdd.length === 0) continue;
        for (const it of S.items) {
          if (it.prefabGuid !== itCat.guid) continue;
          it.stubKind = "CookingUtensil";
          if (!it.cookingUtensil) it.cookingUtensil = {};
          if (it.cookingUtensil.capacity == null) it.cookingUtensil.capacity = defaultUtensilCapacity(it);
          const existing = new Set(it.cookingUtensil.allowedIngredientGuids ?? []);
          for (const g of guidsToAdd) existing.add(g);
          it.cookingUtensil.allowedIngredientGuids = [...existing];
        }
      }
    }
  }

  // ---------- 渲染 ----------
  const contentEl = () => document.getElementById("rw-content")!;
  const footerEl = () => document.getElementById("rw-footer")!;

  const tabsHtml = () => {
    const counts: Record<RecipeTab, number> = {
      select: orderable.length,
      selected: selectedIds.size,
      autofill: currentRecipes().length,
    };
    return (Object.keys(TAB_META) as RecipeTab[])
      .map((k) => {
        const m = TAB_META[k];
        const cnt = ` <span class="rw-tab-cnt">${counts[k]}</span>`;
        const title = m.needScene && !hasScene ? "需要先选择场景" : "";
        return `<button type="button" class="rw-tab${activeTab === k ? " active" : ""}" data-tab="${k}" title="${title}">${m.emoji} ${m.label}${cnt}</button>`;
      })
      .join("");
  };

  const render = () => {
    const tabsEl = document.getElementById("rw-tabs");
    if (tabsEl) tabsEl.innerHTML = tabsHtml();
    wireTabs();
    const el = contentEl();
    if (activeTab === "select" || activeTab === "selected") {
      if (!hasScene) { el.innerHTML = '<p class="modal-hint">请先在关卡编辑器中选择场景，再使用「选择菜谱」/「已选菜谱」。</p>'; renderFooter(); return; }
      const q = (document.getElementById("rw-search") as HTMLInputElement)?.value.trim().toLowerCase() ?? "";
      el.innerHTML = `<div class="rw-toolbar">
          <input type="search" id="rw-search" class="rw-search" value="${escHtml(q)}" placeholder="搜索菜谱 / 食材…" autocomplete="off">
          ${activeTab === "select" ? '<button type="button" class="rw-collapse-all" id="rw-collapse-all">收起全部</button>' : ""}
        </div>
        <div class="rw-list" id="rw-list">${listHtmlFor(q)}</div>`;
      wireList(q);
    } else if (activeTab === "autofill") {
      if (!hasScene) { el.innerHTML = '<p class="modal-hint">请先在关卡编辑器中选择场景，再使用「自动填充道具」。</p>'; renderFooter(); return; }
      el.innerHTML = autofillHtml();
      wireAutofill();
    }
    renderFooter();
  };

  const renderFooter = () => {
    const el = footerEl();
    if (activeTab === "select" || activeTab === "selected") {
      el.innerHTML = `<button type="button" class="modal-btn" data-cancel>取消</button>
        <button type="button" class="modal-btn" id="rw-clear-all">清空已选</button>
        <button type="button" class="modal-btn primary" data-ok>保存菜谱</button>`;
      document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
      document.getElementById("rw-clear-all")?.addEventListener("click", () => {
        selected.clear();
        selectedIds.clear();
        render();
      });
      document.querySelector("[data-ok]")?.addEventListener("click", async () => {
        try {
          const guids = [...selected];
          console.debug(`[recipes] 保存菜谱: levelInfo=${level!.levelInfoAssetPath}, 共 ${guids.length} 个 guid:`);
          for (const g of guids) {
            const r = byGuid.get(g);
            if (r) console.debug(`[recipes]   ${g} -> ${r.id} (${r.group}, ${r.assetPath ?? ""})`);
            else console.warn(`[recipes]   ${g} -> 不在当前目录中（后端可能丢弃该 guid）`);
          }
          await saveLevelRecipes(level!.levelInfoAssetPath, guids);
          closeModal();
          setStatus("菜谱已写入 LevelInfo");
        } catch (e) {
          setStatus((e as Error).message, false);
        }
      });
    } else {
      el.innerHTML = `<button type="button" class="modal-btn" data-cancel>关闭</button>`;
      document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
    }
  };

  const wireTabs = () => {
    document.querySelectorAll<HTMLElement>(".rw-tab").forEach((btn) => {
      btn.addEventListener("click", () => {
        const tab = btn.dataset.tab as RecipeTab;
        if (TAB_META[tab].needScene && !hasScene) {
          setStatus("请先在编辑器中选择场景，再使用该功能", false);
          return;
        }
        activeTab = tab;
        render();
      });
    });
  };

  const wireList = (_q: string) => {
    document.getElementById("rw-search")?.addEventListener("input", () => render());
    const listEl = document.getElementById("rw-list");
    if (!listEl) return;
    listEl.querySelectorAll<HTMLInputElement>("input[type=checkbox]").forEach((cb) => {
      const card = cb.closest(".pick-card");
      if (card) card.classList.toggle("selected", cb.checked);
      cb.addEventListener("change", () => {
        toggleSelect(cb.value, cb.checked);
        card?.classList.toggle("selected", cb.checked);
        updateGroupCounts();
        render();
      });
    });
    listEl.querySelectorAll<HTMLElement>(".rw-group-header").forEach((hdr) => {
      hdr.addEventListener("click", () => hdr.parentElement?.classList.toggle("collapsed"));
    });
    document.getElementById("rw-collapse-all")?.addEventListener("click", () => {
      const groups = [...listEl.querySelectorAll<HTMLElement>(".rw-group")];
      const allCollapsed = groups.every((g) => g.classList.contains("collapsed"));
      groups.forEach((g) => g.classList.toggle("collapsed", !allCollapsed));
      const btn = document.getElementById("rw-collapse-all");
      if (btn) btn.textContent = allCollapsed ? "收起全部" : "展开全部";
    });
  };

  const updateGroupCounts = () => {
    const listEl = document.getElementById("rw-list");
    if (!listEl) return;
    listEl.querySelectorAll<HTMLElement>(".rw-group").forEach((grp) => {
      const selEl = grp.querySelector<HTMLElement>(".rw-group-sel");
      if (!selEl) return;
      let selectedCount = 0;
      grp.querySelectorAll<HTMLInputElement>("input[type=checkbox]").forEach((cb) => {
        if (cb.checked) selectedCount++;
      });
      selEl.textContent = String(selectedCount);
    });
  };

  /** 局部刷新自动填充内容（不动 tabs/footer），补齐后保持弹窗开启。 */
  const refreshAutofill = () => {
    const el = contentEl();
    if (!el) return;
    el.innerHTML = autofillHtml();
    wireAutofill();
  };

  const autofillHtml = (): string => {
    const recs = currentRecipes();
    if (recs.length === 0) return '<p class="modal-hint">未选择菜谱，先勾选菜谱再查看食材清单与自动填充道具。</p>';
    const info = analysisInfo();
    const ingRows = [...info.reqIngs]
      .sort()
      .map((i) => {
        const ok = !info.missingIngs.includes(i);
        let checked = !ok;
        if (i === "DoughSO" && info.reqIngs.has("DLC05_Dough")) checked = false;
        if (i === "ChoppedBunSO" && info.reqIngs.has("DLC02_ChoppedBun")) checked = false;
        return `<label class="rw-row ${ok ? "" : "miss"}"><input type="checkbox" class="rw-ing-cb" value="${i}" ${checked ? "checked" : ""}/> <span>${escHtml(ingredientNameById(i))}</span> <span class="muted">${escHtml(i)}</span></label>`;
      })
      .join("");
    const utRows = info.reqUt
      .map((u) => {
        const ok = !info.missingUt.includes(u);
        const cat = catalogItemById(u);
        const label = cat ? tidyCatalogNameZh(cat.nameZh, cat.id) : u;
        return `<label class="rw-row ${ok ? "" : "miss"}"><input type="checkbox" class="rw-ut-cb" value="${u}" ${ok ? "" : "checked"}/> <span>${escHtml(label)}</span> <span class="muted">${escHtml(u)}</span></label>`;
      })
      .join("");
    return `<div class="rw-analysis-modal">
      ${info.missingIntermediateIds.length
        ? `<div class="rw-warn">⚠ 以下菜谱缺少对应中间产物（面糊），请勿使用：${info.missingIntermediateIds.map((id) => escHtml(id)).join("、")}</div>`
        : ""}
      <p class="modal-hint">食材清单（✓ 已有对应食材箱 · ✗ 缺失）</p>
      <div class="rw-rows">${ingRows || '<p class="muted">无</p>'}</div>
      ${info.missingIngs.length
        ? `<div class="rw-toolbar">
             <button type="button" class="rw-sel-all" id="rw-sel-all-ing">全选缺失</button>
             <button type="button" class="rw-sel-none" id="rw-sel-none-ing">全不选</button>
             <button type="button" class="modal-btn primary rw-fill" id="rw-fill-ing">一键补齐选中食材</button>
           </div>
           <label class="ctx-stub-row" style="display:block;margin-top:6px"><input type="checkbox" id="rw-include-main-dough" ${S.fillIncludeMainDough ? "checked" : ""}/> 同时补齐主线面团/面包皮（DoughSO / ChoppedBunSO）</label>`
        : `<p class="modal-hint ok">食材箱已齐全</p>`}
      <p class="modal-hint" style="margin-top:12px">锅具 / 道具（据烹饪方式推断，✓ 已有 · ✗ 缺失）</p>
      <div class="rw-rows">${utRows || '<p class="muted">无</p>'}</div>
      ${info.missingUt.length
        ? `<label class="ctx-stub-row" style="display:block;margin-top:6px"><input type="checkbox" id="an-auto-intermediates" ${S.autoIntermediates ? "checked" : ""} /> 自动分配中间产物到对应锅具（面糊/煎肉排/炸物等）</label>
           <p class="modal-hint">补齐的物体会放在画布上一排（互不堆叠），可拖动调整位置。</p>`
        : `<p class="modal-hint ok">锅具/道具已齐全</p>`}
      <div class="rw-toolbar" style="margin-top:8px">
        ${info.missingIngs.length ? `<button type="button" class="modal-btn primary" id="an-fill-ing">一键补齐食材 (${info.missingIngs.length})</button>` : ""}
        ${info.missingUt.length ? `<button type="button" class="modal-btn primary" id="an-fill-ut">自动补全道具 (${info.missingUt.length})</button>` : ""}
      </div>
    </div>`;
  };

  const wireAutofill = () => {
    const cbEl = document.getElementById("rw-include-main-dough") as HTMLInputElement | null;
    if (cbEl) cbEl.onchange = () => { S.fillIncludeMainDough = cbEl.checked; };
    const autoCb = document.getElementById("an-auto-intermediates") as HTMLInputElement | null;
    if (autoCb) {
      autoCb.onchange = () => {
        S.autoIntermediates = autoCb.checked;
        const navCb = document.getElementById("chk-auto-intermediates") as HTMLInputElement | null;
        if (navCb) navCb.checked = S.autoIntermediates;
      };
    }
    document.getElementById("rw-sel-all-ing")?.addEventListener("click", () => {
      document.querySelectorAll<HTMLInputElement>(".rw-ing-cb").forEach((c) => { c.checked = true; });
    });
    document.getElementById("rw-sel-none-ing")?.addEventListener("click", () => {
      document.querySelectorAll<HTMLInputElement>(".rw-ing-cb").forEach((c) => { c.checked = false; });
    });
    // 食材补齐：顶部（rw-fill-ing）与底部（an-fill-ing）按钮等价，只刷新自动填充内容，保持弹窗开启
    const doFillIngredients = () => {
      try {
        fillMissingDispensers();
      } catch (e) {
        setStatus(`补齐食材失败：${(e as Error).message}`, false);
      }
      refreshAutofill();
    };
    document.getElementById("rw-fill-ing")?.addEventListener("click", doFillIngredients);
    document.getElementById("an-fill-ing")?.addEventListener("click", doFillIngredients);
    document.getElementById("an-fill-ut")?.addEventListener("click", () => {
      try {
        const selectedUt = new Set<string>();
        document.querySelectorAll<HTMLInputElement>(".rw-ut-cb:checked").forEach((c) => selectedUt.add(c.value));
        fillMissingUtensils(selectedUt);
      } catch (e) {
        setStatus(`补全道具失败：${(e as Error).message}`, false);
      }
      refreshAutofill();
    });
  };

  openModal(
    `菜谱${hasScene && level ? " · " + (level.levelName || "未命名") : ""}`,
    `<div class="rw-tabs" id="rw-tabs">${tabsHtml()}</div>
     <div class="rw-content" id="rw-content"></div>`,
    `<div class="rw-footer" id="rw-footer"></div>`
  );
  document.querySelector(".modal-panel")?.classList.add("wide");

  render();
}
