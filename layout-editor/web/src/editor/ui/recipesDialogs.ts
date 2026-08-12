import {
  S,
  CELL
} from "../state";
import {
  computeRequiredUtensils,
  STEP_UTENSILS,
  computeIntermediatesForUtensils
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
import { foodGroupLabel } from "../../ingredientLabels";
import {
  groupRecipesByType,
  recipeTypeLabel
} from "../../recipeTypes";
import {
  rlCardHtml,
  rlSectionHtml,
  RlCardOptions,
  RecipeWithGroups
} from "../../recipeCard";
import { tidyCatalogNameZh } from "../../displayLabels";
import {
  fetchHealthInfo,
  STALE_BRIDGE_MSG,
  fetchRecipeCatalog,
  fetchLevelRecipes,
  saveLevelRecipes
} from "../../api";
import type { RecipeEntry } from "../../types";

export async function openRecipesDialog() {
  if (!S.scenePath) {
    setStatus("请先选择场景", false);
    return;
  }
  const health = await fetchHealthInfo();
  if (!health.recipeApi) {
    setStatus(STALE_BRIDGE_MSG, false);
    return;
  }
  let recipes: RecipeEntry[];
  let level: { levelInfoAssetPath: string; levelName: string; recipeGuids: string[] };
  try {
    [recipes, level] = await Promise.all([
      fetchRecipeCatalog(S.currentLevelSet),
      fetchLevelRecipes(S.scenePath),
    ]);
  } catch (e) {
    setStatus((e as Error).message, false);
    return;
  }
  if (!level.levelInfoAssetPath) {
    setStatus("未找到该场景对应的 LevelInfoSO", false);
    return;
  }

  const selected = new Set<string>(level.recipeGuids ?? []);
  const orderable = recipes.filter((r) => !r.intermediate);
  S.intermediatesCache = recipes.filter((r) => r.intermediate || r.isCustom);
  const byGuid = new Map(recipes.map((r) => [r.guid, r]));

  const levelSetRecipes = orderable.filter((r) => r.group === "levelset");
  const coreRecipes = orderable.filter((r) => r.group !== "levelset");

  function recipeCard(r: RecipeEntry): string {
    const checked = selected.has(r.guid) ? "checked" : "";
    const cust = r.isCustom ? ` <span class="pc-badge" title="自定义菜谱">🔧</span>` : "";
    const grp =
      r.group && r.group !== "core" && r.group !== "levelset"
        ? ` <span class="pc-badge">${escHtml(foodGroupLabel(r.group))}</span>`
        : "";
    const lsBadge = r.group === "levelset"
      ? ` <span class="pc-badge" style="background:#3b82f6;color:#fff">本关</span>`
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
      <span class="rc-head"><img class="food-icon" loading="lazy" src="${escHtml(iconSrc)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'"><span class="pc-name">${escHtml(r.nameZh)}${grp}${cust}${lsBadge}</span></span>
      <span class="rc-ings">${chips || '<span class="muted small">无食材</span>'}</span>
    </label>`;
  }

  let listHtml = "";

  if (levelSetRecipes.length > 0) {
    const cards = levelSetRecipes.map(recipeCard).join("");
    listHtml += `<div class="rw-group rw-group-levelset" data-type="levelset">
      <div class="rw-group-header" style="background:#3b82f6;color:#fff">
        <span class="rw-group-name">🍽️ 自定义菜单</span>
        <span class="rw-group-count"><span class="rw-group-sel">0</span>/<span class="rw-group-total">${levelSetRecipes.length}</span></span>
      </div>
      <div class="pick-grid recipe-grid">${cards}</div>
    </div>`;
  }

  const coreHtml = groupRecipesByType(coreRecipes)
    .map(([type, recItems]) => {
      const cards = recItems.map(recipeCard).join("");
      return `<div class="rw-group" data-type="${escHtml(type)}"><div class="rw-group-header"><span class="rw-group-name">${escHtml(recipeTypeLabel(type))}</span><span class="rw-group-count"><span class="rw-group-sel">0</span>/<span class="rw-group-total">${S.items.length}</span></span></div><div class="pick-grid recipe-grid">${cards}</div></div>`;
    })
    .join("");
  listHtml += coreHtml;

  openModal(
    `关卡菜谱 · ${level.levelName || "未命名"}`,
    `<div class="rw">
       <div class="rw-col">
         <input type="search" id="rw-search" class="rw-search" placeholder="搜索菜谱…">
         <button type="button" class="rw-collapse-all" id="rw-collapse-all">收起全部</button>
         <div class="modal-scroll rw-list" id="rw-list">${listHtml}</div>
       </div>
       <div class="rw-col">
         <div class="rw-analysis" id="rw-analysis"></div>
       </div>
     </div>`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn" id="rw-view-selected" title="查看当前已选菜谱（分类卡片）">✅ 已选菜谱</button>
     <button type="button" class="modal-btn" id="rw-clear-all">清空已选</button>
     <button type="button" class="modal-btn primary" data-ok>保存菜谱</button>`
  );
  document.querySelector(".modal-panel")?.classList.add("wide");

  const listEl = document.getElementById("rw-list")!;
  const analysisEl = document.getElementById("rw-analysis")!;
  const searchEl = document.getElementById("rw-search") as HTMLInputElement;

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
  const existingPrefabIds = (): Set<string> => new Set(S.items.map((it) => prefabIdFromPath(it.prefabAssetPath)));

  const analysisHtml = (): string => {
    const recs = currentRecipes();
    if (recs.length === 0) return `<p class="modal-hint">未选择菜谱，勾选左侧菜谱后查看所需食材与锅具。</p>`;

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
    const reqUt = computeRequiredUtensils(reqIngs, steps, platingSteps);
    const missingUt = reqUt.filter((u) => !havePref.has(u));

    const ingRows = [...reqIngs]
      .sort()
      .map((i) => {
        const ok = !missingIngs.includes(i);
        let checked = !ok;
        // Main story dough/bun: default unchecked when DLC counterpart also present
        if (i === "DoughSO" && reqIngs.has("DLC05_Dough")) checked = false;
        if (i === "ChoppedBunSO" && reqIngs.has("DLC02_ChoppedBun")) checked = false;
        return `<label class="rw-row ${ok ? "" : "miss"}"><input type="checkbox" class="rw-ing-cb" value="${i}" ${checked ? "checked" : ""}/> <span>${escHtml(ingredientNameById(i))}</span> <span class="muted">${escHtml(i)}</span></label>`;
      })
      .join("");

    const utRows = reqUt
      .map((u) => {
        const ok = !missingUt.includes(u);
        const cat = catalogItemById(u);
        const label = cat ? tidyCatalogNameZh(cat.nameZh, cat.id) : u;
        return `<label class="rw-row ${ok ? "" : "miss"}"><input type="checkbox" class="rw-ut-cb" value="${u}" ${ok ? "" : "checked"}/> <span>${escHtml(label)}</span> <span class="muted">${escHtml(u)}</span></label>`;
      })
      .join("");

    const ingFill = missingIngs.length
      ? `<div class="rw-toolbar">
           <button type="button" class="rw-sel-all" id="rw-sel-all-ing">全选缺失</button>
           <button type="button" class="rw-sel-none" id="rw-sel-none-ing">全不选</button>
           <button type="button" class="modal-btn primary rw-fill" id="rw-fill-ing">一键补齐选中食材</button>
         </div>
         <label class="ctx-stub-row" style="display:block;margin-top:6px"><input type="checkbox" id="rw-include-main-dough" ${S.fillIncludeMainDough ? "checked" : ""}/> 同时补齐主线面团/面包皮（DoughSO / ChoppedBunSO）</label>`
      : `<p class="modal-hint ok">食材箱已齐全</p>`;
    const utFill = missingUt.length
      ? `<button type="button" class="modal-btn primary rw-fill" id="rw-fill-ut">一键补齐缺失锅具/道具 (${missingUt.length})</button>
         <label class="ctx-stub-row" style="display:block;margin-top:6px"><input type="checkbox" id="rw-auto-intermediates" ${S.autoIntermediates ? "checked" : ""} /> 自动分配中间产物到对应锅具</label>`
      : `<p class="modal-hint ok">锅具/道具已齐全</p>`;

    return `
      <p class="modal-hint">食材清单（✓ 已有对应食材箱 · ✗ 缺失）</p>
      <div class="rw-rows">${ingRows || '<p class="muted">无</p>'}</div>
      ${ingFill}
      <p class="modal-hint" style="margin-top:10px">锅具 / 道具（据烹饪方式推断，✓ 已有 · ✗ 缺失）</p>
      <div class="rw-rows">${utRows || '<p class="muted">无</p>'}</div>
      ${utFill}
      <p class="modal-hint" style="margin-top:10px">补齐的物体会放在画布上一排（互不堆叠），可拖动调整位置。</p>
    `;
  };

  const rerender = () => {
    analysisEl.innerHTML = analysisHtml();
    const fillBtn = document.getElementById("rw-fill-ing");
    if (fillBtn) {
      fillBtn.onclick = () => {
        const cb = document.getElementById("rw-include-main-dough") as HTMLInputElement | null;
        S.fillIncludeMainDough = cb?.checked ?? false;
        fillMissingDispensers();
        rerender();
      };
    }
    const cbEl = document.getElementById("rw-include-main-dough") as HTMLInputElement | null;
    if (cbEl) {
      cbEl.onchange = () => { S.fillIncludeMainDough = cbEl.checked; };
    }
    document.getElementById("rw-sel-all-ing")?.addEventListener("click", () => {
      document.querySelectorAll<HTMLInputElement>(".rw-ing-cb").forEach(cb => { cb.checked = true; });
    });
    document.getElementById("rw-sel-none-ing")?.addEventListener("click", () => {
      document.querySelectorAll<HTMLInputElement>(".rw-ing-cb").forEach(cb => { cb.checked = false; });
    });
    const utBtn = document.getElementById("rw-fill-ut");
    if (utBtn) {
      utBtn.onclick = () => {
        const cbs = document.querySelectorAll<HTMLInputElement>(".rw-ut-cb:checked");
        const selected = new Set<string>();
        for (const cb of cbs) selected.add(cb.value);
        if (selected.size === 0) return;

        // attachment -> base id (from STEP_UTENSILS)
        const attachBase = new Map<string, string>();
        for (const ids of Object.values(STEP_UTENSILS)) {
          if (ids.length < 2) continue;
          const base = ids[0];
          for (let i = 1; i < ids.length; i++) attachBase.set(ids[i], base);
        }

        // items with counter_standard or other host rules auto-include their Counter base
        const counterStdRule = "counter_standard";
        const autoBases = new Set<string>();
        for (const cat of S.catalogByGuid.values()) {
          if (cat.stack?.hostRule === counterStdRule && !attachBase.has(cat.id)) {
            attachBase.set(cat.id, "Counter");
            autoBases.add("Counter");
          }
        }

        // group selected items into clusters: base -> [attachments]; standalone items
        const clusters = new Map<string, string[]>(); // baseId -> attachmentIds
        const standalones: string[] = [];
        const clustered = new Set<string>();

        for (const u of selected) {
          const baseId = attachBase.get(u);
          if (baseId && (selected.has(baseId) || autoBases.has(baseId))) {
            if (!clusters.has(baseId)) clusters.set(baseId, []);
            clusters.get(baseId)!.push(u);
            clustered.add(u);
          }
        }

        for (const u of selected) {
          if (!clustered.has(u) && !clusters.has(u)) standalones.push(u);
        }

        const base = placementBase();
        let idx = 0;
        const placedItemIds: string[] = []; // catalog ids of placed items

        // place clusters
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

        // place standalones
        for (const u of standalones) {
          const cat = catalogItemById(u);
          if (!cat) continue;
          addFromCatalog(cat, base.x + idx * CELL, base.z - 2 * CELL);
          placedItemIds.push(u);
          idx++;
        }

        // auto-fill intermediates (use global flag; sync analysis panel checkbox back)
        const autoIntCb = document.getElementById("rw-auto-intermediates") as HTMLInputElement | null;
        if (autoIntCb) {
          S.autoIntermediates = autoIntCb.checked;
          const navCb = document.getElementById("chk-auto-intermediates") as HTMLInputElement | null;
          if (navCb) navCb.checked = S.autoIntermediates;
        }
        const recs = currentRecipes();
        // 锅具 allowedIngredientSOs 是食材（PseudoPrefabSO），用食材 guid 映射
        const ingByGuid = new Map(S.ingredientsCache.map((i) => [i.id, i.guid]));

        // 搅拌杯：把已选菜谱中 Blender 步骤菜谱的食材（如冰沙的草莓/香蕉）装填为额外可搅拌食物
        const blenderIngs = new Set<string>();
        for (const r of recs) {
          if (r.cookingStep === "Blender") (r.ingredients ?? []).forEach((i) => blenderIngs.add(i));
        }
        if (blenderIngs.size > 0 && placedItemIds.includes("BlenderCup")) {
          const guidsToAdd = [...blenderIngs]
            .map((iid) => ingByGuid.get(iid))
            .filter((g): g is string => !!g);
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

          for (const placedId of placedItemIds) {
            const intIds = intermediateMap.get(placedId);
            if (!intIds || intIds.length === 0) continue;
            const itCat = catalogItemById(placedId);
            if (!itCat) continue;
            const guidsToAdd = intIds
              .map((iid) => ingByGuid.get(iid))
              .filter((g): g is string => !!g);
            if (guidsToAdd.length === 0) continue;
            for (const it of S.items) {
              if (it.prefabGuid !== itCat.guid) continue;
              it.stubKind = "CookingUtensil";
              if (!it.cookingUtensil) it.cookingUtensil = {};
              if (it.cookingUtensil.capacity == null)
                it.cookingUtensil.capacity = defaultUtensilCapacity(it);
              const existing = new Set(it.cookingUtensil.allowedIngredientGuids ?? []);
              for (const g of guidsToAdd) existing.add(g);
              it.cookingUtensil.allowedIngredientGuids = [...existing];
            }
          }
        }

        rerender();
      };
    }
  };

  listEl.querySelectorAll<HTMLInputElement>("input[type=checkbox]").forEach((cb) => {
    const card = cb.closest(".pick-card");
    if (card) card.classList.toggle("selected", cb.checked); // init
    cb.addEventListener("change", () => {
      if (cb.checked) selected.add(cb.value);
      else selected.delete(cb.value);
      card?.classList.toggle("selected", cb.checked);
      updateGroupCounts();
      rerender();
    });
  });

  function updateGroupCounts(): void {
    listEl.querySelectorAll<HTMLElement>(".rw-group").forEach((grp) => {
      const selEl = grp.querySelector<HTMLElement>(".rw-group-sel");
      if (!selEl) return;
      let selectedCount = 0;
      grp.querySelectorAll<HTMLInputElement>("input[type=checkbox]").forEach((cb) => {
        if (cb.checked) selectedCount++;
      });
      selEl.textContent = String(selectedCount);
    });
  }
  updateGroupCounts();
  searchEl.addEventListener("input", () => {
    const q = searchEl.value.trim().toLowerCase();
    listEl.querySelectorAll<HTMLDivElement>(".rw-group").forEach((grp) => {
      let visible = 0;
      grp.querySelectorAll<HTMLElement>(".recipe-card").forEach((card) => {
        const txt = card.dataset.name ?? "";
        const show = !q || txt.includes(q);
        card.style.display = show ? "" : "none";
        if (show) visible++;
      });
      grp.style.display = visible > 0 ? "" : "none";
    });
  });

  listEl.querySelectorAll<HTMLElement>(".rw-group-header").forEach((hdr) => {
    hdr.addEventListener("click", () => {
      hdr.parentElement?.classList.toggle("collapsed");
    });
  });

  document.getElementById("rw-collapse-all")?.addEventListener("click", () => {
    const allCollapsed = [...listEl.querySelectorAll<HTMLElement>(".rw-group")].every((g) => g.classList.contains("collapsed"));
    listEl.querySelectorAll<HTMLElement>(".rw-group").forEach((g) => {
      g.classList.toggle("collapsed", !allCollapsed);
    });
    const btn = document.getElementById("rw-collapse-all");
    if (btn) btn.textContent = allCollapsed ? "收起全部" : "展开全部";
  });

  rerender();

  document.getElementById("rw-view-selected")?.addEventListener("click", () => {
    closeModal();
    void openSelectedRecipesDialog();
  });

  document.getElementById("rw-clear-all")?.addEventListener("click", () => {
    selected.clear();
    listEl.querySelectorAll<HTMLInputElement>("input[type=checkbox]").forEach((cb) => {
      cb.checked = false;
      const card = cb.closest(".pick-card");
      card?.classList.remove("selected");
    });
    updateGroupCounts();
    rerender();
  });

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", async () => {
    try {
      await saveLevelRecipes(level.levelInfoAssetPath, [...selected]);
      closeModal();
      setStatus("菜谱已写入 LevelInfo（请在 Unity 保存资源）");
    } catch (e) {
      setStatus((e as Error).message, false);
    }
  });

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
    for (const ing of selectedIngs) {
      const it = addFromCatalog(cat, base.x + idx * CELL, base.z);
      if (it) {
        it.dispenser = { spawnerItemPrefabGuid: ingredientGuidById(ing) ?? "" };
        idx++;
      }
    }
  }
}

export async function openSelectedRecipesDialog() {
  if (!S.scenePath) {
    setStatus("请先选择场景", false);
    return;
  }
  const health = await fetchHealthInfo();
  if (!health.recipeApi) {
    setStatus(STALE_BRIDGE_MSG, false);
    return;
  }
  let recipes: RecipeEntry[];
  let level: { levelInfoAssetPath: string; levelName: string; recipeGuids: string[] };
  try {
    [recipes, level] = await Promise.all([
      fetchRecipeCatalog(S.currentLevelSet),
      fetchLevelRecipes(S.scenePath),
    ]);
  } catch (e) {
    setStatus((e as Error).message, false);
    return;
  }
  if (!level.levelInfoAssetPath) {
    setStatus("未找到该场景对应的 LevelInfoSO", false);
    return;
  }

  const byGuid = new Map(recipes.map((r) => [r.guid, r]));
  S.intermediatesCache = recipes.filter((r) => r.intermediate || r.isCustom);
  const selected: RecipeWithGroups[] = (level.recipeGuids ?? [])
    .map((g) => byGuid.get(g))
    .filter((r): r is RecipeEntry => !!r && !r.intermediate);

  // "菜谱清单列表" card UI, grouped by recipe family (汉堡 / 卷饼 / …)
  const sections = groupRecipesByType(selected)
    .map(([type, arr]) =>
      rlSectionHtml(
        type,
        arr
          .map((r) => {
            const opts: RlCardOptions = {
              allRecipes: recipes as RecipeWithGroups[],
              ingredientName: (id) => ingredientEntryById(id)?.nameZh ?? id,
              extraBadge: r.group === "levelset" ? "本关" : undefined,
            };
            const iconUrl = customRecipeIconUrl(r);
            if (iconUrl) opts.iconSrc = () => iconUrl;
            return rlCardHtml(r, opts);
          })
          .join(""),
        arr.length
      )
    )
    .join("");

  openModal(
    `已选菜谱 · ${level.levelName || "未命名"}（${selected.length}）`,
    `<p class="modal-hint">当前订单菜谱（写入 LevelInfoSO.recipes）</p>
     <div class="modal-scroll">${sections || '<p class="muted">未选择任何菜谱</p>'}</div>`,
    `<button type="button" class="modal-btn primary" data-close>关闭</button>`
  );
  document.querySelector(".modal-panel")?.classList.add("wide", "sel-recipes");
  document.querySelector("[data-close]")?.addEventListener("click", closeModal);
}
