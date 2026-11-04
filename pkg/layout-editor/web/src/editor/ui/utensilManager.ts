import {
  S,
  EditorItem
} from "../state";
import {
  prefabIdFromPath,
  escHtml
} from "../coords";
import { itemLabel } from "../labels";
import { pushHistory } from "../historyOps";
import { draw } from "../render";
import { setStatus } from "../status";
import {
  openModal,
  closeModal
} from "../../modals";
import { cancelBtnHtml, modalBtnHtml, primaryBtnHtml } from "../../ui/views/button";
import { openIngredientMultiPicker } from "../../modals";
import {
  stubKindOf,
  defaultUtensilCapacity,
  utensilTimingInputsHtml,
  UTENSIL_TIME_KEYS,
  readUtensilTimeInput
} from "../stubControls";
import {
  computeUtensilIngredientFill,
  functionalBaseId,
  utensilIntermediateRecipes,
  type UtensilFillEntry
} from "../recipeKnowledge";
import {
  fetchRecipeCatalog,
  fetchLevelRecipes
} from "../../api";
import {
  UTENSIL_KIND_BY_ID,
  UTENSIL_KIND_ZH
} from "../../autoScoreKnowledge";
import type { RecipeEntry } from "../../types";
import { visibleIngredients } from "../../ingredientLabels";
import { itemInCurrentPart, partLevelActive } from "../partLevel";
import {
  emptyUtensilRecipeFilters,
  collectUtensilCookStepChips,
  matchesUtensilRecipeFilters,
  utensilRecipeFilterBarHtml,
  bindUtensilRecipeFilterBar,
} from "../../utensilRecipeFilters";
import { showBusy, hideBusy } from "../../busy";

export type UtensilIngredientFill = Map<string, UtensilFillEntry>;

/** 锅具管理弹窗与额外食材选择器共用的菜谱筛选（reopen 时保留）。 */
const utmSharedRecipeFilters = emptyUtensilRecipeFilters();

// ---------- Web 意面家族（commonW5/pasta）：煎锅中间产物 + 锅内熟物模型 ----------
/** 煎寿司鱼/虾中间产物（CustomRecipeSO type=Cooked）资产 id。 */
const PASTA_FISH_NODE_ID = "Web_Seafood_Pasta_FishNode";
const PASTA_PRAWN_NODE_ID = "Web_Seafood_Pasta_PrawnNode";
/** 意面成品菜谱 id（compositionIds 不可用时的兜底判定）。 */
const PASTA_FINAL_IDS = new Set(["Web_Seafood_Pasta", "Web_Fish_Pasta", "Web_Prawn_Pasta"]);
/** 锅内熟虾模型：commonW2 炸虾饼（原版锅内为圆形煎制物；prawn_fried_01 是接盘
 *  造型，留给中间产物装盘展示）。 */
const PASTA_PRAWN_PAN_MODEL_GUID = "949a48257d6a82246918e3af3e81524e";
/** 锅内熟鱼模型：commonW5 Web_Model_PrepFishSliced（PseudoPrefabSO → bundle47
 *  原版煎鱼 prep_fish_sliced_01，懒加载不直连 bundle guid）。 */
const PASTA_FISH_PAN_MODEL_SO_GUID = "f099b5194b8af4747789845c829644e5";

/** 所选菜谱是否含意面家族（组成引用煎寿司鱼/虾中间产物）。 */
export function pastaFamilySelected(recipes: RecipeEntry[]): boolean {
  return recipes.some((r) => {
    const comps = r.compositionIds ?? [];
    return (
      PASTA_FINAL_IDS.has(r.id) ||
      comps.includes(PASTA_FISH_NODE_ID) ||
      comps.includes(PASTA_PRAWN_NODE_ID)
    );
  });
}

/** 意面家族选中时：给场景煎锅（含 DLC 变体）补挂煎寿司鱼/虾中间产物，并对齐写入
 *  锅内熟物模型（models/modelSOs 与 allowedIngredientSOs 按下标对齐——
 *  PseudoPrefabCookingUtensil.Setup 逐条取 models[i]/modelSOs[i]；空串槽位写回为
 *  null = 无专属模型，运行时回落原版/兜底）。真机经场景序列化引用加载模型。
 *  返回写入的煎锅数。 */
export function applyPastaPanModels(
  recipes: RecipeEntry[],
  recipeGuid: Map<string, string>
): number {
  if (!pastaFamilySelected(recipes)) return 0;
  const fishGuid = recipeGuid.get(PASTA_FISH_NODE_ID);
  const prawnGuid = recipeGuid.get(PASTA_PRAWN_NODE_ID);
  if (!fishGuid && !prawnGuid) return 0;
  // [allowed guid, modelGuid, modelSoGuid]
  const additions: Array<[string, string, string]> = [];
  if (fishGuid) additions.push([fishGuid, "", PASTA_FISH_PAN_MODEL_SO_GUID]);
  if (prawnGuid) additions.push([prawnGuid, PASTA_PRAWN_PAN_MODEL_GUID, ""]);
  let touched = 0;
  for (const it of S.items) {
    if (!itemInCurrentPart(it)) continue;
    if (stubKindOf(it) !== "CookingUtensil") continue;
    const id =
      S.catalogByGuid.get(it.prefabGuid ?? "")?.id ?? prefabIdFromPath(it.prefabAssetPath ?? "");
    if (functionalBaseId(id ?? "") !== "FryPan") continue;
    if (!it.cookingUtensil) it.cookingUtensil = {};
    const cu = it.cookingUtensil;
    const allowed = cu.allowedIngredientGuids ?? [];
    const models = [...(cu.modelGuids ?? [])];
    const soGuids = [...(cu.modelSoGuids ?? [])];
    while (models.length < allowed.length) models.push("");
    while (soGuids.length < allowed.length) soGuids.push("");
    let changed = false;
    for (const [g, modelGuid, soGuid] of additions) {
      const idx = allowed.indexOf(g);
      if (idx >= 0) {
        // 已在列表：仅补空模型槽
        if (modelGuid && !models[idx]) { models[idx] = modelGuid; changed = true; }
        if (soGuid && !soGuids[idx]) { soGuids[idx] = soGuid; changed = true; }
        continue;
      }
      allowed.push(g);
      models.push(modelGuid);
      soGuids.push(soGuid);
      changed = true;
    }
    if (!changed) continue;
    cu.allowedIngredientGuids = allowed;
    cu.modelGuids = models;
    cu.modelSoGuids = soGuids;
    if (cu.capacity == null || cu.capacity <= 0) cu.capacity = defaultUtensilCapacity(it);
    touched++;
  }
  return touched;
}

/** allowed 列表变化时按 guid 迁移模型槽位，保持 modelGuids/modelSoGuids 下标对齐
 *  （额外食材选择器删项 / 按菜谱自动填充覆盖写入都会改变 allowed；模型数组不迁移
 *  会让槽位错位到别的食材上）。从未配置过模型则不动。 */
function realignUtensilModels(
  cu: NonNullable<EditorItem["cookingUtensil"]>,
  nextAllowed: string[]
): void {
  const prevAllowed = cu.allowedIngredientGuids ?? [];
  const prevModels = cu.modelGuids ?? [];
  const prevSoGuids = cu.modelSoGuids ?? [];
  if (prevModels.length === 0 && prevSoGuids.length === 0) return;
  const modelByGuid = new Map<string, { m: string; s: string }>();
  prevAllowed.forEach((g, i) => {
    if (g) modelByGuid.set(g, { m: prevModels[i] ?? "", s: prevSoGuids[i] ?? "" });
  });
  const nextModels = nextAllowed.map((g) => modelByGuid.get(g)?.m ?? "");
  const nextSoGuids = nextAllowed.map((g) => modelByGuid.get(g)?.s ?? "");
  if (nextModels.some((x) => x) || nextSoGuids.some((x) => x)) {
    cu.modelGuids = nextModels;
    cu.modelSoGuids = nextSoGuids;
  } else {
    delete cu.modelGuids;
    delete cu.modelSoGuids;
  }
}

/** 把 computeUtensilIngredientFill 的结果写进场景锅具的 allowedIngredientSOs。
 *  两个入口共用：锅具管理页「按菜谱自动填充」与菜谱弹窗「安装缺失」。
 *  ings 按 id→食材 guid、intermediates 按 id→菜谱 guid 解析；返回写入的锅具数。
 *  可移动火锅（pushable）只写食材配置、不挂 CookingUtensil stub（挂 stub 会触发
 *  宿主 Setup NRE，载体组装在 LayoutRuntimePushablePot）。
 *
 *  ⚠ `clear` 条目 = **强制清空**（不是「跳过」）：烧麦/核心松饼这类「搅拌后整锅下
 *  终锅」的菜谱，终锅必须保持空列表 —— PseudoPrefabCookingUtensil.Setup 只在
 *  allowedIngredientSOs 非空时才整表替换原版 lookup，而原版 SteamerPrefabLookup /
 *  FryableObjectsLookup 本就同时含面团与生食材。存量关卡里被旧版 autofill 写进蒸笼
 *  的生胡萝卜也靠这条洗掉。
 *  容量一律取 fill 算出的「单份用量」（蒸笼 1 / 搅拌碗 2~4 / 汤锅 3），与原版一致。 */
export function applyUtensilIngredientFill(
  fill: UtensilIngredientFill,
  ingGuid: Map<string, string>,
  recipeGuid: Map<string, string>
): number {
  const vesselOfItem = (it: EditorItem): string => {
    const id = S.catalogByGuid.get(it.prefabGuid ?? "")?.id ?? prefabIdFromPath(it.prefabAssetPath ?? "");
    return functionalBaseId(id ?? "");
  };
  let touched = 0;
  for (const it of S.items) {
    // 分 P：填充只作用于当前编辑的阶段（各 P 锅具独立配置）。
    if (!itemInCurrentPart(it)) continue;
    const isPushablePot = prefabIdFromPath(it.prefabAssetPath) === "web_utensil_large_pot_01_pushable";
    if (stubKindOf(it) !== "CookingUtensil" && !isPushablePot) continue;
    const f = fill.get(vesselOfItem(it));
    if (!f) continue;
    const add: string[] = [];
    if (!f.clear) {
      for (const iid of f.ings) {
        const g = ingGuid.get(iid);
        if (g) add.push(g);
      }
      for (const iid of f.intermediates) {
        const g = recipeGuid.get(iid);
        if (g) add.push(g);
      }
      // guid 全部解析失败（目录缺项）时不动既有配置
      if (!add.length) continue;
    }
    if (!isPushablePot) it.stubKind = "CookingUtensil";
    if (!it.cookingUtensil) it.cookingUtensil = {};
    it.cookingUtensil.capacity = Math.max(1, f.capacity);
    // 覆盖写入 allowed 前按 guid 迁移模型槽位（意面煎锅模型等），防下标错位
    const nextAllowed = [...new Set(add)];
    realignUtensilModels(it.cookingUtensil, nextAllowed);
    it.cookingUtensil.allowedIngredientGuids = nextAllowed;
    touched++;
  }
  return touched;
}

/** 打开前需拉中间产物目录，用全局忙碌遮罩过渡（与菜谱弹窗一致）。 */
export function openUtensilManager() {
  showBusy("加载锅具数据…");
  void showUtensilManagerModal().finally(() => hideBusy());
}

type UtensilManagerModalOpts = {
  /** 仅「按菜谱自动填充」等批量重绘时跳过目录请求。 */
  skipCatalogRefresh?: boolean;
  /** 整页重绘后恢复列表滚动位置（自动填充用）。 */
  restoreScrollTop?: number;
};

function utmModalScrollEl(): HTMLElement | null {
  return document.querySelector('[data-modal-id="utensil-manager"] .modal-scroll');
}

function captureUtmModalScroll(): number {
  const el = utmModalScrollEl();
  return el ? el.scrollTop : 0;
}

function restoreUtmModalScroll(top: number): void {
  requestAnimationFrame(() => {
    const el = utmModalScrollEl();
    if (el) el.scrollTop = top;
  });
}

function extraIngredientsBtnLabel(count: number): string {
  return count > 0 ? `额外食材：${count} 种…` : "额外食材：无（处理所有主线食材）…";
}

/** 单行就地刷新（避免整弹窗 reopen 丢滚动条位置）。 */
function patchUtensilRowDom(it: EditorItem): void {
  const row = document.querySelector(`.utm-row[data-key="${it._editorKey}"]`);
  if (!row) return;
  const cu = it.cookingUtensil ?? {};
  const cap = (cu.capacity ?? 0) > 0 ? cu.capacity! : defaultUtensilCapacity(it);
  const capInput = row.querySelector<HTMLInputElement>(".utm-cap");
  if (capInput) capInput.value = String(cap);
  const allowed = cu.allowedIngredientGuids ?? [];
  const ingsBtn = row.querySelector<HTMLButtonElement>(".utm-ings");
  if (ingsBtn) ingsBtn.textContent = extraIngredientsBtnLabel(allowed.length);
  row.querySelectorAll<HTMLInputElement>(".utm-time").forEach((input) => {
    const key = UTENSIL_TIME_KEYS[input.dataset.utime ?? ""];
    if (!key) return;
    const raw = cu[key];
    input.value = raw != null && raw > 0 ? String(raw) : "";
  });
}

async function showUtensilManagerModal(opts?: UtensilManagerModalOpts) {
  if (S.currentLevelSet && !opts?.skipCatalogRefresh) {
    try {
      const catalog = await fetchRecipeCatalog(S.currentLevelSet);
      S.intermediatesCache = utensilIntermediateRecipes(catalog);
    } catch (e) {
      setStatus(`刷新中间产物目录失败，使用缓存：${(e as Error).message}`, false);
    }
  }

  // 锅具 = CookingUtensil stub + 可移动火锅（含锅 child，有 IngredientContainer，
  //  食材配置同样有效，但 stubKind 保持空由后端载体组装，不挂 CookingUtensil stub）
  //  食材配置同样有效，但 stubKind 保持空由后端载体组装，不挂 CookingUtensil stub）
  //  分 P：只列出当前阶段的锅具（各 P 独立填充/同步）。
  const utensils = S.items.filter(
    (it) =>
      itemInCurrentPart(it) &&
      (stubKindOf(it) === "CookingUtensil" ||
        prefabIdFromPath(it.prefabAssetPath) === "web_utensil_large_pot_01_pushable")
  );
  if (!utensils.length) {
    setStatus(partLevelActive() ? "当前阶段没有锅具（其他阶段的锅具不参与填充）" : "当前关卡没有锅具", false);
    return;
  }

  const groups = new Map<string, EditorItem[]>();
  for (const it of utensils) {
    const pid = prefabIdFromPath(it.prefabAssetPath);
    const arr = groups.get(pid) ?? [];
    arr.push(it);
    groups.set(pid, arr);
  }
  const sortedGroups = [...groups.entries()].sort((a, b) => a[0].localeCompare(b[0]));

  const body = sortedGroups
    .map(([pid, arr]) => {
      arr.sort((a, b) => a._wz - b._wz || a._wx - b._wx);
      const rows = arr
        .map((it, idx) => {
          const cu = it.cookingUtensil ?? {};
          const cap = (cu.capacity ?? 0) > 0 ? cu.capacity! : defaultUtensilCapacity(it);
          const allowed = cu.allowedIngredientGuids ?? [];
          const allowedTxt = allowed.length > 0 ? `额外食材：${allowed.length} 种` : "额外食材：无（处理所有主线食材）";
          const dis = arr.length < 2 ? "disabled" : "";
          return `<div class="utm-row" data-key="${it._editorKey}">
            <span class="utm-name">${escHtml(itemLabel(it))}${idx + 1}</span>
            <label class="utm-cap-label">容量 <input type="number" class="utm-cap" data-key="${it._editorKey}" min="0" step="1" value="${cap}"/></label>
            ${utensilTimingInputsHtml(it, "", "utm")}
            ${modalBtnHtml(`${allowedTxt}…`, "modal-btn utm-ings", { "data-key": it._editorKey })}
            ${modalBtnHtml(`同步给其他 ${arr.length - 1} 个`, "modal-btn utm-sync", { "data-key": it._editorKey, ...(dis ? { disabled: "" } : {}) })}
          </div>`;
        })
        .join("");
      return `<div class="utm-group"><div class="utm-group-title">${escHtml(itemLabel(arr[0]))}（${escHtml(pid)}）× ${arr.length}</div>${rows}</div>`;
    })
    .join("");

  const autofillCookSteps = collectUtensilCookStepChips(S.intermediatesCache);
  const autofillFilterBar = utensilRecipeFilterBarHtml(utmSharedRecipeFilters, autofillCookSteps, {
    barId: "utm-autofill-recipe-filters",
    hint: "仅影响「按菜谱自动填充」：在关卡已选菜谱中筛选参与推导的条目（与额外食材选择器共用筛选状态）",
  });

  openModal(
    "锅具管理 · 参数同步",
    `<p class="modal-hint">可直接修改每个锅具的容量、时间与额外食材（不选额外食材时可处理所有主线食材，选中后可额外煮这些食材），或一键把它的参数同步给所有相同类型的锅具。额外食材选择器含「中间产物 / 自定义菜谱」分组与细筛（0 分、搅拌等）。时间留空 = 原版默认（输入框内灰色占位显示该锅具真实原版时间，煮糊/过混默认 2× 煮熟/混合）；特殊煮糊时间随关卡包分发。仅修改前端数据，写回 Unity 后生效。</p>
     ${autofillFilterBar}
     <div class="modal-scroll">${body}</div>`,
    `${primaryBtnHtml("🧺 按菜谱自动填充", { id: "utm-auto-fill" })}
     ${cancelBtnHtml("关闭")}`,
    { id: "utensil-manager", panelClass: "wide" }
  );
  const panel = document.querySelector(".modal-panel");
  if (panel) bindUtensilRecipeFilterBar(panel, utmSharedRecipeFilters, () => {});
  if (opts?.restoreScrollTop != null) restoreUtmModalScroll(opts.restoreScrollTop);

  const utensilByKey = (key: string | undefined) => S.items.find((i) => i._editorKey === key);
  const ensureUtensil = (it: EditorItem) => {
    it.stubKind = "CookingUtensil";
    if (!it.cookingUtensil) it.cookingUtensil = {};
    return it.cookingUtensil;
  };

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);

  // 按菜谱自动填充：读取当前关卡已选菜谱 → 数据驱动计算各锅具应装的食材
  // （汤料→汤锅、香肠→汤锅、洋葱→煎锅、面糊食材→搅拌碗、面糊节点→炸篮、
  //  搅拌类→搅拌杯，含 DLC 食材如 dlc07 土豆/西芹；两阶段子菜谱前置锅放生食材
  //  （汤锅←生米）、终锅填中间产物节点（煎锅←煮米），不把生米写进煎锅；
  //  烧麦/核心松饼这类「搅拌后整锅下终锅」的菜谱，终锅清空、保留原版 lookup），
  //  按功能基础 id 匹配场景锅具（含 DLC 变体），容量按菜谱单份用量；覆盖写入食材列表。
  document.getElementById("utm-auto-fill")?.addEventListener("click", async () => {
    if (!S.scenePath) {
      setStatus("未选择场景，无法读取关卡菜谱", false);
      return;
    }
    showBusy("按菜谱自动填充…");
    try {
      let recipes: RecipeEntry[] | null = null;
      let guids: string[] = [];
      try {
        const [catalog, level] = await Promise.all([
          fetchRecipeCatalog(S.currentLevelSet),
          fetchLevelRecipes(S.scenePath),
        ]);
        recipes = catalog;
        guids = level?.recipeGuids ?? [];
      } catch (e) {
        setStatus(`读取关卡菜谱失败：${(e as Error).message}`, false);
        return;
      }
      const byGuid = new Map(recipes.map((r) => [r.guid, r]));
      const allSelected = guids.map((g) => byGuid.get(g)).filter((r): r is RecipeEntry => !!r);
      if (!allSelected.length) {
        setStatus("当前关卡未选择菜谱，先在「选择菜谱」里勾选", false);
        return;
      }
      S.intermediatesCache = utensilIntermediateRecipes(recipes);

      const recs = allSelected.filter((r) =>
        matchesUtensilRecipeFilters(r, utmSharedRecipeFilters, allSelected)
      );
      if (!recs.length) {
        setStatus("当前筛选下没有参与自动填充的关卡菜谱，请调整「菜谱筛选」或先在「选择菜谱」里勾选", false);
        return;
      }

      const fill = computeUtensilIngredientFill(recs);
      if (!fill.size) {
        setStatus("所选菜谱无需锅具装填（沙拉/拼盘等组装类）", false);
        return;
      }
      const ingGuid = new Map(S.ingredientsCache.map((i) => [i.id, i.guid]));
      const recipeGuid = new Map(recipes.map((r) => [r.id, r.guid]));

      pushHistory();
      const touched = applyUtensilIngredientFill(fill, ingGuid, recipeGuid);
      // Web 意面家族：煎锅补挂煎寿司鱼/虾中间产物 + 锅内熟物模型（炸虾饼/原版煎鱼）
      const pastaPans = applyPastaPanModels(recs, recipeGuid);
      draw();
      const vesselZh = (v: string) => {
        const kind = UTENSIL_KIND_BY_ID[v];
        return (kind && UTENSIL_KIND_ZH[kind]) || v;
      };
      const parts = [...fill.entries()].map(([v, f]) => {
        const n = f.ings.length + f.intermediates.length;
        return f.clear || n === 0
          ? `${vesselZh(v)} 沿用原版（容量 ${f.capacity}）`
          : `${vesselZh(v)}×${n} 种食材（容量 ${f.capacity}）`;
      });
      setStatus(
        touched
          ? `已按 ${recs.length} 道菜谱填充 ${touched} 个锅具（${parts.join("、")}${pastaPans > 0 ? `；${pastaPans} 个煎锅已挂煎寿司鱼/虾中间产物与锅内模型` : ""}；写回后生效）`
          : `场景中没有匹配的锅具（需要：${parts.join("、")}）——可先用菜谱弹窗「自动补全道具」放置缺失锅具`,
        touched > 0
      );
      const scrollTop = captureUtmModalScroll();
      void showUtensilManagerModal({ skipCatalogRefresh: true, restoreScrollTop: scrollTop });
    } finally {
      hideBusy();
    }
  });

  document.querySelectorAll<HTMLInputElement>(".utm-cap").forEach((input) => {
    input.addEventListener("change", () => {
      const it = utensilByKey(input.dataset.key);
      const v = parseInt(input.value, 10);
      if (!it || !isFinite(v) || v < 0) return;
      pushHistory();
      ensureUtensil(it).capacity = v;
      draw();
    });
  });

  // 时间参数（煮熟/煮糊 · 混合/过度混合，按锅具类型分组显示）：
  // 空串 = 清除回原版默认（糊/过混回退 2× 联动），数值 > 0 生效。
  // Unity JsonUtility 会把未配置序列化成 0——0 一律按未配置处理。
  document.querySelectorAll<HTMLInputElement>(".utm-time").forEach((input) => {
    input.addEventListener("change", () => {
      const row = input.closest(".utm-row") as HTMLElement | null;
      const it = utensilByKey(row?.dataset.key ?? input.dataset.key);
      if (!it) return;
      const key = UTENSIL_TIME_KEYS[input.dataset.utime ?? ""];
      if (!key) return;
      const curRaw = it.cookingUtensil?.[key];
      const cur = curRaw != null && curRaw > 0 ? curRaw : undefined;
      const next = readUtensilTimeInput(input.value);
      if (cur === next) return;
      pushHistory();
      ensureUtensil(it);
      if (next == null) delete it.cookingUtensil?.[key];
      else it.cookingUtensil![key] = next;
      draw();
    });
  });

  document.querySelectorAll<HTMLButtonElement>(".utm-ings").forEach((btn) => {
    btn.addEventListener("click", () => {
      const it = utensilByKey(btn.dataset.key);
      if (!it) return;
      const baseId = functionalBaseId(prefabIdFromPath(it.prefabAssetPath) ?? "");
      const hint =
        baseId === "BlenderCup"
          ? "allowedIngredientSOs（不选 = 处理所有主线食材；选中的作为额外可煮食材）。Web 果汁多用 DLC4 水果（葡萄/橙子/桃子）与莓果，请用「全部」或「水果」分类查找，勿只筛 DLC2（原版果汁节点仅草莓/菠萝碎等少数几种）。"
          : "allowedIngredientSOs（不选 = 处理所有主线食材；选中的作为额外可煮食材）";
      openIngredientMultiPicker(
        `锅具 · 额外食材（${itemLabel(it)}）`,
        hint,
        visibleIngredients(S.ingredientsCache),
        it.cookingUtensil?.allowedIngredientGuids ?? [],
        (guids) => {
          pushHistory();
          const cu = ensureUtensil(it);
          // allowed 增删后按 guid 迁移模型槽位（意面煎锅模型），防下标错位
          realignUtensilModels(cu, guids);
          cu.allowedIngredientGuids = guids;
          // capacity 缺省会写回 0（后端 int）——兜底原版默认
          if (cu.capacity == null) cu.capacity = defaultUtensilCapacity(it);
          draw();
          patchUtensilRowDom(it);
        },
        S.intermediatesCache,
        {
          utensilRecipeFilters: true,
          sharedUtensilRecipeFilters: utmSharedRecipeFilters,
        }
      );
    });
  });

  document.querySelectorAll<HTMLButtonElement>(".utm-sync").forEach((btn) => {
    btn.addEventListener("click", () => {
      const src = utensilByKey(btn.dataset.key);
      if (!src) return;
      const pid = prefabIdFromPath(src.prefabAssetPath);
      const cu = src.cookingUtensil ?? {};
      const cap = (cu.capacity ?? 0) > 0 ? cu.capacity! : defaultUtensilCapacity(src);
      const allowed = cu.allowedIngredientGuids ?? [];
      pushHistory();
      const patched: EditorItem[] = [src];
      for (const it of S.items) {
        if (it === src || stubKindOf(it) !== "CookingUtensil") continue;
        if (!itemInCurrentPart(it)) continue;
        if (prefabIdFromPath(it.prefabAssetPath) !== pid) continue;
        it.stubKind = "CookingUtensil";
        it.cookingUtensil = {
          capacity: cap,
          allowedIngredientGuids: [...allowed],
          // 锅内模型随 allowed 一起同步（下标对齐；意面煎锅炸虾饼/煎鱼模型）
          ...(cu.modelGuids?.length ? { modelGuids: [...cu.modelGuids] } : {}),
          ...(cu.modelSoGuids?.length ? { modelSoGuids: [...cu.modelSoGuids] } : {}),
          ...(cu.cookTime != null && cu.cookTime > 0 ? { cookTime: cu.cookTime } : {}),
          ...(cu.burnTime != null && cu.burnTime > 0 ? { burnTime: cu.burnTime } : {}),
          ...(cu.mixTime != null && cu.mixTime > 0 ? { mixTime: cu.mixTime } : {}),
          ...(cu.overMixTime != null && cu.overMixTime > 0 ? { overMixTime: cu.overMixTime } : {})
        };
        patched.push(it);
      }
      draw();
      for (let i = 0; i < patched.length; i++) patchUtensilRowDom(patched[i]);
    });
  });
}
