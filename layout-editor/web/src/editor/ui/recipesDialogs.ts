import {
  S,
  CELL,
  EditorItem
} from "../state";
import {
  computeRequiredUtensils,
  STEP_UTENSILS,
  computeIntermediatesForUtensils,
  computeUtensilIngredientFill,
  recipeNeedsCreamSpray,
  recipeNeedsSodaMachine,
  recipeNeedsDrinkMachine,
  recipeNeedsCondimentMachine,
  condimentMachineForIngredient,
  SODA_MACHINE_INGREDIENT_IDS,
  DRINK_MACHINE_INGREDIENT_IDS,
  recipeLacksIntermediate,
  missingIntermediateRecipes,
  functionalBaseId,
  leafIngredientIds,
  CREAM_SPRAY_IDS,
  CREAM_SPRAY_DEFAULT_ID,
  CREAM_INGREDIENT_IDS,
  isRecipeDlcBlocked
} from "../recipeKnowledge";
import { comboById, addCombo } from "../combos";
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
  ingredientIdByGuid
} from "../catalog";
import {
  openModal,
  closeModal
} from "../../modals";
import { setStatus } from "../status";
import {
  addFromCatalog,
  placementBase,
  itemWorldAABB
} from "../items";
import { itemLayerOfIt } from "../catalog";
import { utensilCapacityOrFix } from "../stubControls";
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
  fetchOptionalPresets,
  saveOptionalItems,
  saveMatchlists,
  saveLevelRecipes
} from "../../api";
import { NODE_INGREDIENT_SOURCES } from "../../recipeGroups";
import type { RecipeEntry, LevelOptionalItem, LevelRecipes, OptionalPresets } from "../../types";

export type RecipeTab = "select" | "selected" | "autofill" | "optional" | "matchlist";

export interface RecipesDialogOptions {
  openTab?: RecipeTab;
  /** 关卡集（缺省用当前场景关卡集）。 */
  setName?: string;
}

const TAB_META: Record<RecipeTab, { label: string; emoji: string; needScene: boolean }> = {
  select: { label: "选择菜谱", emoji: "🍽️", needScene: true },
  selected: { label: "已选菜谱", emoji: "✅", needScene: true },
  autofill: { label: "自动填充道具", emoji: "🧺", needScene: true },
  optional: { label: "Optional 参数", emoji: "🧩", needScene: true },
  matchlist: { label: "Matchlist", emoji: "📋", needScene: true },
};

export async function openRecipesDialog(opts: RecipesDialogOptions = {}) {
  const health = await fetchHealthInfo();
  if (!health.recipeApi) {
    setStatus(STALE_BRIDGE_MSG, false);
    return;
  }
  const hasScene = !!S.scenePath;

  let activeTab: RecipeTab = opts.openTab ?? "select";

  // ---------- 选择/已选 数据 ----------
  let recipes: RecipeEntry[] = [];
  let level: LevelRecipes | null = null;
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
  // ---------- Optional / Matchlist 手动管理状态 ----------
  // optionalRecipeMatchListItems / includeRecipeMatchLists 不再随「保存菜谱」自动
  // 重建，由这两个 tab 单独配置、单独写回（覆盖式；取消勾选/删除只影响对应数组）。
  let optionalItems: LevelOptionalItem[] = (level?.optionalItems ?? []).map((i) => ({ ...i }));
  let optionalDirty = false;
  const matchlistKeys = new Set<string>((level?.matchlists ?? []).map((m) => m.key));
  let matchlistDirty = false;
  // 一键填充候选（hotdog 两套 / 披萨部件）懒加载：进 optional tab 前预取。
  let presets: OptionalPresets | null = null;
  void fetchOptionalPresets()
    .then((p) => {
      presets = p;
      // 弹窗已关闭时跳过重渲染（rw-content 已不在 DOM）。
      if (activeTab === "optional" && document.getElementById("rw-content")) render();
    })
    .catch(() => {
      // 候选不可用（旧桥）时 tab 内提示，已保存条目仍可查看/删除。
    });
  // 默认屏蔽重复 DLC 换皮（同一道菜的多个 DLC 皮肤只保留首选一版，已选的不隐藏）。
  let blockDupDlc = true;
  let orderable: RecipeEntry[] = [];
  let byGuid = new Map<string, RecipeEntry>();
  /** id → 菜谱/中间产物条目：套餐等组成项里查不到食材表的（FriedMeat 等
   *  中间产物）用它回退取中文名与成品贴图（icons/recipes）。 */
  let byRecipeId = new Map<string, RecipeEntry>();
  let levelSetRecipes: RecipeEntry[] = [];
  let coreRecipes: RecipeEntry[] = [];
  S.intermediatesCache = recipes.filter((r) => r.intermediate || r.isCustom);

  /** 从 recipes 重建派生集合（加载或安装/移除后刷新）。 */
  const recomputeGroups = () => {
    byGuid = new Map(recipes.map((r) => [r.guid, r]));
    byRecipeId = new Map(recipes.map((r) => [r.id, r]));
    // 自定义菜谱（含 Composite/Mixed，score 可能为 0 被标 intermediate）一律可作为订单菜谱；
    // 只有非自定义的中间产物（如 score<=0 的匹配规则）排除在可点单之外。
    orderable = recipes.filter((r) => !r.intermediate || r.isCustom);
    S.intermediatesCache = recipes.filter((r) => r.intermediate || r.isCustom);
    const vis = visibleRecipes(orderable);
    levelSetRecipes = orderable.filter((r) => r.group === "levelset");
    // 选择菜谱：本关自定义（levelset）+ 其余全部（core / dlcXX 通用内容），全量可用。
    coreRecipes = vis.filter((r) => r.group !== "levelset");
  };
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
  // 去重过滤依赖 selectedIds（已选变体保留可见），必须在 syncSelectedIdsFromLevel 之后
  recomputeGroups();

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

  /** 按当前勾选的菜谱 id 解析 guid（覆盖写回，不保留未勾选项）。 */
  function guidsForSave(): string[] {
    const out: string[] = [];
    const seenId = new Set<string>();
    for (const id of selectedIds) {
      if (!id || seenId.has(id)) continue;
      seenId.add(id);
      const r =
        recipes.find((x) => x.id === id && x.group === "levelset") ??
        recipes.find((x) => x.id === id);
      if (r) out.push(r.guid);
    }
    return out;
  }

  const catMeta: Record<string, { label: string; emoji: string; color: string }> = {
    levelset: { label: "自定义菜谱", emoji: "🍽️", color: "#3b82f6" },
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
        // 建箱等价替换（如套餐面包 dlc08_bun 显示为核心汉堡面包）优先
        const ing = ingredientEntryById(ingId) ?? ingredientEntryById(NODE_INGREDIENT_SOURCES[ingId] ?? "");
        if (ing) {
          return `<span class="rc-ing" title="${escHtml(ingId)}">${foodIconImg("ingredients", ing.id, ing.icon)}${escHtml(ing.nameZh)}</span>`;
        }
        // 食材表查不到：多为中间产物（煎肉排/炸洋葱圈等），按菜谱条目取名与成品贴图
        const mid = byRecipeId.get(ingId);
        if (mid) {
          return `<span class="rc-ing" title="${escHtml(ingId)}">${foodIconImg("recipes", mid.id, mid.icon)}${escHtml(mid.nameZh)}</span>`;
        }
        return `<span class="rc-ing" title="${escHtml(ingId)}">${foodIconImg("ingredients", undefined, false)}${escHtml(ingId)}</span>`;
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

  function catHtml(cat: "levelset" | "core", items: RecipeEntry[]): string {
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
    const byCat: Record<string, RecipeEntry[]> = { levelset: [], core: [] };
    for (const r of sel) {
      if (r.group === "levelset") byCat.levelset.push(r);
      else byCat.core.push(r);
    }
    let html = "";
    for (const cat of ["levelset", "core"] as const) {
      if (byCat[cat].length === 0) continue;
      html += catHtml(cat, byCat[cat]);
    }
    return html;
  }

  function listHtmlFor(q: string): string {
    const lower = q.toLowerCase();
    const vis = (items: RecipeEntry[]) =>
      items.filter((r) => {
        // 默认屏蔽重复 DLC 换皮（已勾选的不隐藏，便于查看/取消）
        if (blockDupDlc && isRecipeDlcBlocked(r) && !selectedIds.has(r.id)) return false;
        if (!lower) return true;
        const hay = `${r.nameZh} ${r.nameEn ?? ""} ${r.id} ${(r.ingredients ?? []).join(" ")}`.toLowerCase();
        return hay.includes(lower);
      });
    if (activeTab === "selected") return selectedViewHtml();
    const parts: string[] = [];
    if (activeTab === "select") {
      if (vis(levelSetRecipes).length) parts.push(catHtml("levelset", vis(levelSetRecipes)));
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
        if (!id) continue;
        s.add(id);
        // 等价形态互通（uID 相同的换皮/核心版）：dlc10 箱算 dlc04 需求已满足
        // （反之亦然），dlc08 面包箱算核心面包——避免误报缺失、重复建箱。
        const equiv = NODE_INGREDIENT_SOURCES[id];
        if (equiv) s.add(equiv);
        for (const [from, to] of Object.entries(NODE_INGREDIENT_SOURCES)) {
          if (to === id) s.add(from);
        }
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
      // 组成项里的中间产物（煎肉排/炸洋葱圈等）展开为叶食材（MeatSO/dlc08_onion_ring）
      // 进食材箱清单——半成品本身无实体食材条目，不能建箱（此前报「目录中找不到
      // 对应食材资产」），改由锅具装填（computeUtensilIngredientFill）分配到对应锅具。
      // NODE_INGREDIENT_SOURCES 同时做建箱等价替换（套餐面包 dlc08_bun → 核心
      // 汉堡面包 ChoppedBunSO，uID 等价、订单匹配不受影响）。
      (r.ingredients ?? []).forEach((i) =>
        leafIngredientIds(i).forEach((leaf) => reqIngs.add(NODE_INGREDIENT_SOURCES[leaf] ?? leaf))
      );
      if (r.cookingStep) steps.add(r.cookingStep);
      // 套餐等无主步骤的菜谱：加工步骤在 cookingGroups 里（FryingPan 煎肉 /
      // DeepFatFryer 炸薯条洋葱圈芝士条），据此要求煎锅/炸锅工作台
      for (const g of r.cookingGroups ?? []) if (g.step) steps.add(g.step);
      if (r.platingStep) platingSteps.add(r.platingStep);
    }
    const haveDisp = existingDispenserIngIds();
    const havePref = existingPrefabIds();
    // node 型匹配节点（如沙拉洋葱 dlc11onion_salad）由其整食材食材箱覆盖
    const crateIngId = (i: string) => NODE_INGREDIENT_SOURCES[i] ?? i;
    // 汽水是 node 型，由汽水机产出（需求体现在「锅具/道具」区），不进食材箱清单
    const fromSodaMachine = (i: string) => SODA_MACHINE_INGREDIENT_IDS.includes(i);
    // 套餐饮料由饮料机产出（机器 + 开关联动），不建普通食材箱
    const fromDrinkMachine = (i: string) => DRINK_MACHINE_INGREDIENT_IDS.includes(i);
    // 发泡奶油由奶油喷罐喷出（道具绑定），不建食材箱
    const fromCreamSpray = (i: string) => CREAM_INGREDIENT_IDS.includes(i);
    const fromTool = (i: string) => fromSodaMachine(i) || fromDrinkMachine(i) || fromCreamSpray(i);
    const missingIngs = [...reqIngs].filter(
      (i) => !fromTool(i) && !haveDisp.has(i) && !haveDisp.has(crateIngId(i))
    );
    // 奶油喷罐：需求只含默认 DLC3 款（不自动填充 dlc09 版）；
    // 场景里已有任一款（dlc03/dlc09）即视为满足，不误报缺失。
    const sprayGuids = CREAM_SPRAY_IDS.map((id) => catalogItemById(id)?.guid).filter((g): g is string => !!g);
    const hasAnySpray = S.items.some((it) => !!it.prefabGuid && sprayGuids.includes(it.prefabGuid));
    const reqUt0 = computeRequiredUtensils(
      new Set([...reqIngs].filter((i) => !fromTool(i))),
      steps,
      platingSteps,
      recs
    );
    // 目录校验闸门：需求 id 必须在目录中真实存在且有中文翻译——幽灵 id
    // （对应资产被删/未引入，如历史 common03 对齐上游时丢失的马克杯套装）
    // 不再出现在清单/不再尝试放置，聚合提示而非静默忽略。
    const ignoredUtIds = new Set<string>();
    const catalogValidUt = (list: string[]) =>
      list.filter((u) => {
        const cat = catalogItemById(u);
        if (cat && cat.nameZh) return true;
        ignoredUtIds.add(u);
        return false;
      });
    const reqUt = catalogValidUt(hasAnySpray ? reqUt0.filter((u) => u !== CREAM_SPRAY_DEFAULT_ID) : reqUt0);
    const missingUt = reqUt.filter((u) => !havePref.has(u));
    const missingIntermediateIds = missingIntermediateRecipes(recs).map((r) => r.id);
    return {
      reqIngs: new Set([...reqIngs].filter((i) => !fromTool(i))),
      steps,
      platingSteps,
      missingIngs,
      reqUt,
      missingUt,
      missingIntermediateIds,
      ignoredUtIds: [...ignoredUtIds],
    };
  };

  // ---------- 自动填充道具（食材箱 + 锅具/道具） ----------

  /** 自动填充行空位放置：从游标起逐格右移，用落位后的真实 AABB 与场景现有
   *  物品（含本批已放）求交（只查核心物品层），被占则撤掉重试；成功后游标
   *  推进到已放物品右缘。此前按 idx*CELL 直线排开不检查占用——自动填充与
   *  场景已有道具重叠的根因。 */
  const placeAtFreeCell = (
    catId: string,
    cursor: { x: number },
    rowZ: number,
    recordHistory = true
  ): EditorItem | null => {
    const cat = catalogItemById(catId);
    if (!cat) return null;
    for (let step = 0; step < 96; step++) {
      const it = addFromCatalog(cat, cursor.x + step * CELL, rowZ, recordHistory);
      if (!it) return null;
      const box = itemWorldAABB(it);
      const EPS = 0.02;
      const clash = S.items.some((o) => {
        if (o === it || itemLayerOfIt(o) !== "items") return false;
        const b2 = itemWorldAABB(o);
        return (
          Math.min(box.maxX, b2.maxX) - Math.max(box.minX, b2.minX) > EPS &&
          Math.min(box.maxZ, b2.maxZ) - Math.max(box.minZ, b2.minZ) > EPS
        );
      });
      if (!clash) {
        cursor.x = Math.max(cursor.x + step * CELL, box.maxX + CELL);
        return it;
      }
      const i = S.items.indexOf(it);
      if (i >= 0) S.items.splice(i, 1);
    }
    setStatus(`画布空位不足，「${tidyCatalogNameZh(cat.nameZh, cat.id)}」未能放置（请先腾出一排空位）`, false);
    return null;
  };

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
    const cursor = { x: base.x };
    const unresolved: string[] = [];
    for (const ing of selectedIngs) {
      // 汽水是 node 型（由汽水机产出）、发泡奶油由喷罐喷出：都不建食材箱
      if (SODA_MACHINE_INGREDIENT_IDS.includes(ing) || CREAM_INGREDIENT_IDS.includes(ing)) continue;
      // node 型匹配节点 → 食材箱生成其整食材（如沙拉洋葱节点 → 整个沙拉洋葱）
      const crateId = NODE_INGREDIENT_SOURCES[ing] ?? ing;
      const guid = ingredientGuidById(crateId);
      if (!guid) {
        // 中间产物（煎肉排等）无实体食材条目、不建箱（叶食材展开后不应出现，防御）；
        // 仅对真正的未知食材报缺失
        if (!byRecipeId.has(ing)) unresolved.push(ing);
        continue;
      }
      const it = placeAtFreeCell("Dispenser", cursor, base.z);
      if (it) {
        it.dispenser = { spawnerItemPrefabGuid: guid };
      }
    }
    if (unresolved.length > 0) {
      setStatus(`食材箱未创建（目录中找不到对应食材资产）：${unresolved.join("、")}`, false);
    }
  }

  function fillMissingUtensils(selectedUt: Set<string>) {
    const recs = currentRecipes();

    // ---------- 放置规则 ----------
    // 1. 防重叠：placeAtFreeCell 从游标起逐格右移，用落位后的真实 AABB 与场景
    //    现有物品（含本批已放）求交，被占则撤掉试下一格——不再与已有道具叠位。
    // 2. 一个工作台只放一种锅具：按目录 stack 数据（stack.y+hostRule）判定堆叠对，
    //    第 i 个锅配第 i 个灶台、各占独立空位（此前同族锅全部叠在同一坐标）；
    //    落地件（Oven/Cooker 等 stack=None）独立放置，作为堆叠 base 被锅具消耗后
    //    （锅数 ≥1）不再重复落地一份。
    const catalogValid = (id: string) => {
      const cat = catalogItemById(id);
      return cat && cat.nameZh ? cat : null;
    };
    const isStackItem = (id: string) => catalogValid(id)?.stack?.y != null;
    const stackIds = [...selectedUt].filter((u) => isStackItem(u));
    const groundIds = [...selectedUt].filter((u) => !isStackItem(u));

    // 堆叠件 → 其 base（STEP_UTENSILS 首位；hostRule=counter_standard → Counter）
    const attachBase = new Map<string, string>();
    for (const ids of Object.values(STEP_UTENSILS)) {
      if (ids.length < 2) continue;
      const b = ids[0];
      for (let i = 1; i < ids.length; i++) attachBase.set(ids[i], b);
    }
    const counterStdRule = "counter_standard";
    for (const cat of S.catalogByGuid.values()) {
      if (cat.stack?.hostRule === counterStdRule && !attachBase.has(cat.id)) {
        attachBase.set(cat.id, "Counter");
      }
    }

    const base = placementBase();
    const fillRowZ = base.z - 2 * CELL;
    // placeAtFreeCell 为弹窗级共享 helper（见 fillMissingDispensers 上方），
    // 此处统一传锅具/道具行 z；堆叠件（锅）落在自己灶台正上方不参与查占。
    const cursor = { x: base.x };
    const placedItemIds: string[] = [];
    const baseConsumed = new Map<string, number>();
    for (const attId of stackIds) {
      const attCat = catalogValid(attId);
      if (!attCat) continue;
      const baseId = attachBase.get(attId);
      if (!baseId) {
        // 无 base 映射的堆叠件按落地件处理
        if (placeAtFreeCell(attId, cursor, fillRowZ)) placedItemIds.push(attId);
        continue;
      }
      const baseItem = placeAtFreeCell(baseId, cursor, fillRowZ);
      if (!baseItem) continue;
      placedItemIds.push(baseId);
      baseConsumed.set(baseId, (baseConsumed.get(baseId) ?? 0) + 1);
      // 锅具叠在自己灶台正上方（合法堆叠；不参与防重叠检查）
      const att = addFromCatalog(attCat, baseItem._wx ?? 0, baseItem._wz ?? 0);
      if (att && attCat.stack?.y != null) att.localPosition.y = attCat.stack.y;
      placedItemIds.push(attId);
    }
    for (const u of groundIds) {
      const consumed = baseConsumed.get(u) ?? 0;
      if (consumed > 0) {
        baseConsumed.set(u, consumed - 1);
        continue;
      }
      if (placeAtFreeCell(u, cursor, fillRowZ)) placedItemIds.push(u);
    }

    // 奶油喷罐：发泡奶油由喷罐喷出（不建食材箱）。缺失时自动放置一个并绑定奶油食材。
    const needCream = recs.some(recipeNeedsCreamSpray);
    const ingByGuid = new Map(S.ingredientsCache.map((i) => [i.id, i.guid]));
    const recipeById = new Map(recipes.map((r) => [r.id, r.guid]));
    if (needCream) {
      const creamGuids = CREAM_INGREDIENT_IDS.map((iid) => ingByGuid.get(iid)).filter((g): g is string => !!g);
      if (creamGuids.length === 0) {
        setStatus("⚠️ 选中菜谱需要奶油喷罐，但目录中找不到发泡奶油食材（whippedcream），喷罐未绑定", false);
      }
      let sprayFound = false;
      const bindSpray = (it: EditorItem) => {
        sprayFound = true;
        it.stubKind = "CookingUtensil";
        if (!it.cookingUtensil) it.cookingUtensil = {};
        it.cookingUtensil.capacity = utensilCapacityOrFix(it);
        it.cookingUtensil.allowedIngredientGuids = [...creamGuids];
      };
      for (const sprayId of CREAM_SPRAY_IDS) {
        const sprayCat = catalogItemById(sprayId);
        if (!sprayCat) continue;
        for (const it of S.items) {
          if (it.prefabGuid === sprayCat.guid) bindSpray(it);
        }
      }
      if (!sprayFound) {
        const sprayCat = catalogItemById("utensil_ingredient_spray_01") ?? catalogItemById(CREAM_SPRAY_IDS[0]);
        const it = sprayCat ? placeAtFreeCell(sprayCat.id, cursor, fillRowZ, false) : null;
        if (it) {
          bindSpray(it);
        } else {
          setStatus("⚠️ 选中菜谱需要奶油喷罐，但未能自动放置（utensil_ingredient_spray_01）", false);
        }
      }
    }

    // 汽水机 + 联动开关（冰淇淋汽水：汽水为 node 型，只能由汽水机产出）。
    // 无机器 → 放置「汽水饮料机 + 开关」组合（自动联动）；机器可输出列表
    // 收窄为本关所需汽水；已有机器但缺开关联动 → 只补开关 + switchLink。
    {
      const sodaRecs = recs.filter(recipeNeedsSodaMachine);
      if (sodaRecs.length > 0) {
        const sodas = new Set<string>();
        for (const r of sodaRecs)
          for (const i of r.ingredients ?? [])
            if (SODA_MACHINE_INGREDIENT_IDS.includes(i)) sodas.add(i);
        const machineOf = () =>
          S.items.find((it) => prefabIdFromPath(it.prefabAssetPath ?? "") === "dlc11_drink_dispenser");
        let machine = machineOf();
        if (!machine) {
          const def = comboById("drink_switch_icecream");
          if (def) {
            addCombo(def, cursor.x, base.z - 3 * CELL);
            cursor.x += 4 * CELL;
            machine = machineOf();
          }
        }
        if (machine) {
          const guids = [...sodas].map((id) => ingByGuid.get(id)).filter((g): g is string => !!g);
          if (guids.length > 0) {
            machine.stubKind = "Dispenser";
            machine.soArray = { pseudoPrefabGuids: guids };
            machine.dispenser = { spawnerItemPrefabGuid: guids[0] };
          }
          const linked = S.switchLinks.some((l) => l.targetId === machine.instanceId);
          if (!linked) {
            const swCat = catalogItemById("Switch");
            const sw = swCat
              ? addFromCatalog(swCat, (machine._wx ?? 0) + 2 * CELL, machine._wz ?? 0)
              : null;
            if (sw) {
              S.switchLinks.push({
                switchId: sw.instanceId,
                targetId: machine.instanceId,
                trigger: "Next",
              });
            }
          }
        } else {
          setStatus("⚠️ 选中菜谱需要汽水机（dlc11_drink_dispenser），但未能放置", false);
        }
      }
    }

    // 饮料机 + 联动开关（套餐：饮料 drink01/02/03 由 DLC8 饮料机产出，不建普通食材箱）。
    // 逻辑同汽水机：无机器 → 放置「饮料机 + 开关」组合（自动联动）；可输出列表
    // 收窄为本关所需饮料；已有机器但缺开关联动 → 只补开关 + switchLink。
    {
      const drinkRecs = recs.filter(recipeNeedsDrinkMachine);
      if (drinkRecs.length > 0) {
        const drinks = new Set<string>();
        for (const r of drinkRecs)
          for (const i of r.ingredients ?? [])
            if (DRINK_MACHINE_INGREDIENT_IDS.includes(i)) drinks.add(i);
        const machineOf = () =>
          S.items.find((it) => prefabIdFromPath(it.prefabAssetPath ?? "") === "dlc08_drink_machine");
        let machine = machineOf();
        if (!machine) {
          const def = comboById("drink_switch");
          if (def) {
            addCombo(def, cursor.x, base.z - 3 * CELL);
            cursor.x += 4 * CELL;
            machine = machineOf();
          }
        }
        if (machine) {
          const guids = [...drinks].map((id) => ingByGuid.get(id)).filter((g): g is string => !!g);
          if (guids.length > 0) {
            machine.stubKind = "Dispenser";
            machine.soArray = { pseudoPrefabGuids: guids };
            machine.dispenser = { spawnerItemPrefabGuid: guids[0] };
          }
          const linked = S.switchLinks.some((l) => l.targetId === machine.instanceId);
          if (!linked) {
            const swCat = catalogItemById("Switch");
            const sw = swCat
              ? addFromCatalog(swCat, (machine._wx ?? 0) + 2 * CELL, machine._wz ?? 0)
              : null;
            if (sw) {
              S.switchLinks.push({
                switchId: sw.instanceId,
                targetId: machine.instanceId,
                trigger: "Next",
              });
            }
          }
        } else {
          setStatus("⚠️ 选中菜谱需要饮料机（dlc08_drink_machine），但未能放置", false);
        }
      }
    }

    // 酱料机 + 联动开关（热狗的番茄酱/芥末酱：node 型食材，只能由酱料机产出）。
    // 逻辑同饮料机：无机器 → 放置「酱料机 + 开关」组合（自动联动，dlc08）；可输出列表
    // 收窄为本关所需酱料；已有机器但缺开关联动 → 只补开关 + switchLink。
    // dlc11 换皮酱料（dlc11_ketchup/mustard）只认 dlc11 酱料机（无组合，手动摆放 + 接线）。
    {
      const condRecs = recs.filter(recipeNeedsCondimentMachine);
      if (condRecs.length > 0) {
        // 按机器家族分组收集所需酱料
        const needByMachine = new Map<string, Set<string>>();
        for (const r of condRecs)
          for (const i of r.ingredients ?? []) {
            const mid = condimentMachineForIngredient(i);
            if (!mid) continue;
            if (!needByMachine.has(mid)) needByMachine.set(mid, new Set<string>());
            needByMachine.get(mid)!.add(i);
          }
        for (const [machinePid, sauceIds] of needByMachine) {
          const machineOf = () =>
            S.items.find((it) => prefabIdFromPath(it.prefabAssetPath ?? "") === machinePid);
          let machine: EditorItem | null | undefined = machineOf();
          if (!machine) {
            const def = machinePid === "dlc08_condiment_dispenser" ? comboById("condiment_switch") : undefined;
            if (def) {
              addCombo(def, cursor.x, base.z - 3 * CELL);
              cursor.x += 4 * CELL;
              machine = machineOf();
            } else {
              const mCat = catalogItemById(machinePid);
              machine = mCat ? placeAtFreeCell(machinePid, cursor, base.z - 3 * CELL, false) : null;
            }
          }
          if (machine) {
            const guids = [...sauceIds].map((id) => ingByGuid.get(id)).filter((g): g is string => !!g);
            if (guids.length > 0) {
              machine.stubKind = "Dispenser";
              machine.soArray = { pseudoPrefabGuids: guids };
              machine.dispenser = { spawnerItemPrefabGuid: guids[0] };
            }
            const linked = S.switchLinks.some((l) => l.targetId === machine.instanceId);
            if (!linked) {
              const swCat = catalogItemById("Switch");
              const sw = swCat
                ? addFromCatalog(swCat, (machine._wx ?? 0) + 2 * CELL, machine._wz ?? 0)
                : null;
              if (sw) {
                S.switchLinks.push({
                  switchId: sw.instanceId,
                  targetId: machine.instanceId,
                  trigger: "Next",
                });
              }
            }
          } else {
            setStatus(`⚠️ 选中菜谱需要酱料机（${machinePid}），但未能放置`, false);
          }
        }
      }
    }

    // 锅具食材自动装填（数据驱动，镜像游戏 OrderDefinitionNode 组成）：
    // 汤类食材→汤锅、热狗肠→汤锅、洋葱→煎锅、搅拌类食材→搅拌杯、面糊食材→搅拌碗、
    // 面糊中间产物→炸篮等。按功能基础 id 匹配场景锅具（含 DLC 变体），覆盖写入 allowedIngredientSOs。
    {
      const vesselFill = computeUtensilIngredientFill(recs);
      const ingGuid = new Map(S.ingredientsCache.map((i) => [i.id, i.guid]));
      const vesselOfItem = (it: { prefabGuid?: string; prefabAssetPath?: string }): string => {
        const id = S.catalogByGuid.get(it.prefabGuid ?? "")?.id ?? prefabIdFromPath(it.prefabAssetPath ?? "");
        return functionalBaseId(id ?? "");
      };
      for (const [vessel, fill] of vesselFill) {
        const guids: string[] = [];
        for (const iid of fill.ings) {
          const g = ingGuid.get(iid);
          if (g) guids.push(g);
        }
        for (const iid of fill.intermediates) {
          const g = recipeById.get(iid);
          if (g) guids.push(g);
        }
        if (guids.length === 0) continue;
        for (const it of S.items) {
          if (vesselOfItem(it) !== vessel) continue;
          it.stubKind = "CookingUtensil";
          if (!it.cookingUtensil) it.cookingUtensil = {};
          it.cookingUtensil.capacity = utensilCapacityOrFix(it);
          it.cookingUtensil.allowedIngredientGuids = [...new Set(guids)];
        }
      }
    }

    // 自动分配中间产物 + 搅拌杯装填
    if (S.autoIntermediates) {
      const intermediateMap = computeIntermediatesForUtensils(recs);
      for (const [placedId, intIds] of intermediateMap) {
        if (!intIds || intIds.length === 0) continue;
        const itCat = catalogItemById(placedId);
        if (!itCat) continue;
        const guidsToAdd = intIds
          .map((iid) => ingByGuid.get(iid) ?? recipeById.get(iid))
          .filter((g): g is string => !!g);
        if (guidsToAdd.length === 0) continue;
        for (const it of S.items) {
          if (it.prefabGuid !== itCat.guid) continue;
          it.stubKind = "CookingUtensil";
          if (!it.cookingUtensil) it.cookingUtensil = {};
          it.cookingUtensil.capacity = utensilCapacityOrFix(it);
          it.cookingUtensil.allowedIngredientGuids = [...new Set(guidsToAdd)];
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
      optional: optionalItems.length,
      matchlist: matchlistKeys.size,
    };
    return (Object.keys(TAB_META) as RecipeTab[])
      .map((k) => {
        const m = TAB_META[k];
        const cnt = ` <span class="rw-tab-cnt">${counts[k]}${k === "optional" && optionalDirty ? "*" : ""}${k === "matchlist" && matchlistDirty ? "*" : ""}</span>`;
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
          ${activeTab === "select" ? `<button type="button" class="rw-collapse-all" id="rw-block-dup" title="${blockDupDlc ? "同一道菜的多个 DLC 皮肤只保留首选一版（热狗保留 DLC8、热可可保留 DLC3、火锅保留 DLC10、烤菜/布丁保留 DLC7/DLC3、水果拼盘保留 DLC4）" : "显示所有 DLC 皮肤变体"}">${blockDupDlc ? "屏蔽重复DLC ✓" : "显示重复DLC"}</button>` : ""}
          ${activeTab === "select" ? '<button type="button" class="rw-collapse-all" id="rw-collapse-all">收起全部</button>' : ""}
        </div>
        <div class="rw-list" id="rw-list">${listHtmlFor(q)}</div>`;
      wireToolbarButtons();
      wireListCards();
    } else if (activeTab === "autofill") {
      if (!hasScene) { el.innerHTML = '<p class="modal-hint">请先在关卡编辑器中选择场景，再使用「自动填充道具」。</p>'; renderFooter(); return; }
      el.innerHTML = autofillHtml();
      wireAutofill();
    } else if (activeTab === "optional") {
      if (!hasScene) { el.innerHTML = '<p class="modal-hint">请先在关卡编辑器中选择场景，再使用「Optional 参数管理」。</p>'; renderFooter(); return; }
      el.innerHTML = optionalTabHtml();
      wireOptional();
    } else if (activeTab === "matchlist") {
      if (!hasScene) { el.innerHTML = '<p class="modal-hint">请先在关卡编辑器中选择场景，再使用「Matchlist 管理」。</p>'; renderFooter(); return; }
      el.innerHTML = matchlistTabHtml();
      wireMatchlist();
    }
    renderFooter();
  };

  /** 搜索输入：只重建列表区（不触碰工具栏/搜索框），避免焦点丢失导致只能输入一个字。 */
  const renderList = () => {
    const q = (document.getElementById("rw-search") as HTMLInputElement)?.value.trim().toLowerCase() ?? "";
    const listEl = document.getElementById("rw-list");
    if (!listEl) return;
    listEl.innerHTML = listHtmlFor(q);
    wireListCards();
  };

  /** 工具栏（搜索框 / 屏蔽重复DLC / 收起全部）接线 —— 仅整页 render 时调用。 */
  const wireToolbarButtons = () => {
    document.getElementById("rw-search")?.addEventListener("input", renderList);
    document.getElementById("rw-block-dup")?.addEventListener("click", () => {
      blockDupDlc = !blockDupDlc;
      render();
    });
    document.getElementById("rw-collapse-all")?.addEventListener("click", () => {
      const listEl = document.getElementById("rw-list");
      if (!listEl) return;
      const groups = [...listEl.querySelectorAll<HTMLElement>(".rw-group")];
      const allCollapsed = groups.every((g) => g.classList.contains("collapsed"));
      groups.forEach((g) => g.classList.toggle("collapsed", !allCollapsed));
      const btn = document.getElementById("rw-collapse-all");
      if (btn) btn.textContent = allCollapsed ? "收起全部" : "展开全部";
    });
  };

  /** 列表卡片接线：勾选 / 分组折叠（重建 #rw-list 后需重挂）。 */
  const wireListCards = () => {
    const listEl = document.getElementById("rw-list");
    if (!listEl) return;
    listEl.querySelectorAll<HTMLInputElement>("input[type=checkbox]").forEach((cb) => {
      const card = cb.closest(".pick-card");
      if (card) card.classList.toggle("selected", cb.checked);
      cb.addEventListener("change", () => {
        toggleSelect(cb.value, cb.checked);
        card?.classList.toggle("selected", cb.checked);
        updateGroupCounts();
        // 轻量刷新：只更新 tab 计数，不重建列表（保持滚动位置与搜索框焦点）
        const tabsEl = document.getElementById("rw-tabs");
        if (tabsEl) { tabsEl.innerHTML = tabsHtml(); wireTabs(); }
      });
    });
    listEl.querySelectorAll<HTMLElement>(".rw-group-header").forEach((hdr) => {
      hdr.addEventListener("click", () => hdr.parentElement?.classList.toggle("collapsed"));
    });
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
          const guids = guidsForSave();
          console.debug(`[recipes] 保存菜谱（覆盖）: levelInfo=${level!.levelInfoAssetPath}, 共 ${guids.length} 个 guid:`);
          for (const g of guids) {
            const r = byGuid.get(g);
            if (r) console.debug(`[recipes]   ${g} -> ${r.id} (${r.group}, ${r.assetPath ?? ""})`);
            else console.warn(`[recipes]   ${g} -> 不在当前目录中（后端可能丢弃该 guid）`);
          }
          await saveLevelRecipes(level!.levelInfoAssetPath, guids);
          selected.clear();
          for (const g of guids) selected.add(g);
          closeModal();
          setStatus(`菜谱已覆盖写入 LevelInfo（${guids.length} 道）`);
        } catch (e) {
          setStatus((e as Error).message, false);
        }
      });
    } else if (activeTab === "optional") {
      el.innerHTML = `<button type="button" class="modal-btn" data-cancel>关闭</button>
        <button type="button" class="modal-btn primary" id="rw-save-optional">写回 Optional${optionalDirty ? `（${optionalItems.length} 条*）` : `（${optionalItems.length} 条）`}</button>`;
      document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
      document.getElementById("rw-save-optional")?.addEventListener("click", doSaveOptional);
    } else if (activeTab === "matchlist") {
      el.innerHTML = `<button type="button" class="modal-btn" data-cancel>关闭</button>
        <button type="button" class="modal-btn primary" id="rw-save-matchlist">写回 Matchlist${matchlistDirty ? `（${matchlistKeys.size} 个*）` : `（${matchlistKeys.size} 个）`}</button>`;
      document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
      document.getElementById("rw-save-matchlist")?.addEventListener("click", doSaveMatchlist);
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
        const iName = ingredientEntryById(i)?.nameZh ?? byRecipeId.get(i)?.nameZh ?? i;
        return `<label class="rw-row ${ok ? "" : "miss"}"><input type="checkbox" class="rw-ing-cb" value="${i}" ${checked ? "checked" : ""}/> <span>${escHtml(iName)}</span> <span class="muted">${escHtml(i)}</span></label>`;
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
      ${info.ignoredUtIds?.length
        ? `<div class="rw-warn">⚠ 以下需求 id 不在道具目录中（资产缺失或未翻译），已忽略：${info.ignoredUtIds.map((u) => escHtml(u)).join("、")}</div>`
        : ""}
      <p class="modal-hint" style="margin-top:12px">锅具 / 道具（据烹饪方式推断，✓ 已有 · ✗ 缺失）</p>
      <div class="rw-rows">${utRows || '<p class="muted">无</p>'}</div>
      ${info.missingUt.length
        ? `<label class="ctx-stub-row" style="display:block;margin-top:6px"><input type="checkbox" id="an-auto-intermediates" ${S.autoIntermediates ? "checked" : ""} /> 自动分配中间产物到对应锅具（面糊/煎肉排/炸物等）</label>
           <p class="modal-hint">补齐的物体会放在画布上一排（互不堆叠），可拖动调整位置。</p>`
        : `<p class="modal-hint ok">锅具/道具已齐全</p>`}
      <div class="rw-toolbar" style="margin-top:8px">
        ${info.missingIngs.length ? `<button type="button" class="modal-btn primary" id="an-fill-ing">一键补齐食材 (${info.missingIngs.length})</button>` : ""}
        <button type="button" class="modal-btn primary" id="an-fill-ut" title="放置缺失锅具/机具并装填：锅具按已选菜谱分配可处理食材（煎锅←肉、炸篮←薯条/洋葱圈/芝士条等），饮料机/汽水机/酱料机/奶油喷罐自动放置并联动；锅具已齐时仅执行装填与联动（覆盖写入食材）">${info.missingUt.length ? `自动补全道具 (${info.missingUt.length})` : "🔄 锅具装填 / 机具联动"}</button>
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

  // ---------- 🧩 Optional 参数管理 tab ----------
  // optionalRecipeMatchListItems 手动配置：DLC 原始菜谱 / hotdog 可选部件与酱料 /
  // 披萨部件等注册进关卡匹配表。保存菜谱不再自动重建此数组（旧关卡内容保留，
  // 可在此增删）。是否使用一键填充由用户决定，填充后仍需「写回 Optional」。
  let optPickerOpen = false;
  let optFilter = "all";
  const OPT_FILTERS = () => {
    const groups = [...new Set(optionalCandidates().map((c) => c.group).filter(Boolean))].sort();
    return ["all", "preset", ...groups];
  };

  const KIND_META: Record<string, { label: string; cls: string }> = {
    "recipe": { label: "DLC菜谱", cls: "kind-recipe" },
    "hotdog-optional": { label: "热狗可选", cls: "kind-hotdog" },
    "condiment": { label: "酱料", cls: "kind-condiment" },
    "boiledfrankfurter": { label: "煮肠", cls: "kind-condiment" },
    "pizza-optional": { label: "披萨部件", cls: "kind-pizza" },
    "custom-recipe": { label: "自定义", cls: "kind-custom" },
    "node": { label: "节点", cls: "kind-node" },
  };
  const kindMetaOf = (k?: string) => KIND_META[k ?? ""] ?? KIND_META.node;

  /** 候选 = 一键填充特例（presets）+ 目录中的 DLC 原始菜谱（按 guid 去重）。 */
  const optionalCandidates = (): { guid: string; id: string; nameZh: string; group: string; kind: string }[] => {
    const list: { guid: string; id: string; nameZh: string; group: string; kind: string }[] = [];
    const seen = new Set<string>();
    for (const p of presets?.items ?? []) {
      if (!p.guid || seen.has(p.guid)) continue;
      seen.add(p.guid);
      list.push({ guid: p.guid, id: p.id, nameZh: p.nameZh ?? "", group: p.group ?? "", kind: p.kind ?? "node" });
    }
    for (const r of recipes) {
      if (!r.group || !r.group.startsWith("dlc") || seen.has(r.guid)) continue;
      seen.add(r.guid);
      list.push({ guid: r.guid, id: r.id, nameZh: r.nameZh ?? "", group: r.group, kind: "recipe" });
    }
    return list;
  };

  const optionalDisplayName = (it: LevelOptionalItem): string => {
    const cand = optionalCandidates().find((c) => c.guid === it.guid);
    if (cand?.nameZh) return cand.nameZh;
    const r = byGuid.get(it.guid) ?? byRecipeId.get(it.id);
    if (r?.nameZh) return r.nameZh;
    return it.id;
  };

  const addOptionalGuids = (guids: string[] | undefined, doneMsg: string) => {
    if (!guids || guids.length === 0) return;
    const cand = new Map(optionalCandidates().map((c) => [c.guid, c]));
    const known = new Set(optionalItems.map((i) => i.guid));
    let added = 0;
    for (const g of guids) {
      if (known.has(g)) continue;
      const c = cand.get(g);
      optionalItems.push({ guid: g, id: c?.id ?? g, group: c?.group, kind: c?.kind });
      known.add(g);
      added++;
    }
    optionalDirty = true;
    setStatus(added ? `${doneMsg}（新增 ${added} 条，待写回）` : "候选条目已全部在列表中");
    render();
  };

  const fillPizzaOptionals = () => {
    if (!presets) { setStatus("候选尚未加载完成，稍后再试", false); return; }
    const guids = [...presets.pizzaFillGuids];
    // 蘑菇披萨变体：与旧自动填充一致，仅当所选菜谱包含蘑菇披萨时追加。
    const hasMushroom = [...selected].some((g) => {
      const r = byGuid.get(g);
      return !!r && r.type === "pizza" && /mushroom/i.test(r.id);
    });
    if (hasMushroom) guids.push(...presets.pizzaMushroomGuids);
    addOptionalGuids(guids, "已加入自选披萨部件");
  };

  const fillHotdogOptionals = () => {
    if (!presets) { setStatus("候选尚未加载完成，稍后再试", false); return; }
    // 与旧自动填充一致：所选热狗属 dlc11 时用 dlc11 套件，否则 dlc08。
    const isDlc11 = [...selectedIds].some((id) => /dlc11/i.test(id) && /hotdog|frankfurter/i.test(id));
    addOptionalGuids(
      isDlc11 ? presets.hotdogFillGuidsDlc11 : presets.hotdogFillGuidsDlc08,
      isDlc11 ? "已加入 Hotdog（dlc11）可选部件/酱料/煮肠" : "已加入 Hotdog（dlc08）可选部件/酱料/煮肠"
    );
  };

  const optionalFilterLabel = (f: string) =>
    f === "all" ? "全部"
    : f === "preset" ? "特例(Hotdog/披萨)"
    : foodGroupLabel(f as never) || f;

  /** 候选卡片（picker 整块与局部刷新共用）。 */
  const optCardsHtml = () => {
    const q = (document.getElementById("rw-opt-search") as HTMLInputElement)?.value.trim().toLowerCase() ?? "";
    const known = new Set(optionalItems.map((i) => i.guid));
    return optionalCandidates()
      .filter((c) => (optFilter === "all" ? true : optFilter === "preset" ? c.kind !== "recipe" : c.group === optFilter))
      .filter((c) => !q || c.id.toLowerCase().includes(q) || c.nameZh.toLowerCase().includes(q))
      .map((c) => {
        const km = kindMetaOf(c.kind);
        const has = known.has(c.guid);
        return `<button type="button" class="pick-card rw-opt-cand${has ? " disabled" : ""}" data-oguid="${escHtml(c.guid)}"${has ? " disabled" : ""}>
          <span class="rw-badge ${km.cls}">${km.label}</span>
          <span class="rw-opt-name">${escHtml(c.nameZh || c.id)}</span>
          <span class="muted rw-opt-id">${escHtml(c.id)}</span>
        </button>`;
      })
      .join("");
  };

  const optPickerHtml = () => {
    const chips = OPT_FILTERS()
      .map((f) => `<button type="button" class="rw-chip${optFilter === f ? " active" : ""}" data-ofilter="${escHtml(f)}">${escHtml(optionalFilterLabel(f))}</button>`)
      .join("");
    return `<div class="rw-opt-picker">
      <div class="rw-chips">${chips}</div>
      <input type="search" id="rw-opt-search" class="rw-search" placeholder="搜索候选（id / 名称）…" autocomplete="off">
      <div class="rw-opt-cand-list" id="rw-opt-cand-list">${optCardsHtml() || '<p class="muted">无匹配候选</p>'}</div>
    </div>`;
  };

  /** 候选搜索/筛选只重建候选列表区（保持输入焦点）。 */
  const refreshOptPickerList = () => {
    const el = document.getElementById("rw-opt-cand-list");
    if (!el) return;
    el.innerHTML = optCardsHtml() || '<p class="muted">无匹配候选</p>';
    wireOptCandCards();
  };

  const wireOptCandCards = () => {
    document.querySelectorAll<HTMLElement>(".rw-opt-cand:not([disabled])").forEach((card) => {
      card.addEventListener("click", () => {
        const g = card.dataset.oguid;
        if (!g || optionalItems.some((i) => i.guid === g)) return;
        const c = optionalCandidates().find((x) => x.guid === g);
        optionalItems.push({ guid: g, id: c?.id ?? g, group: c?.group, kind: c?.kind });
        optionalDirty = true;
        render();
      });
    });
  };

  const optionalTabHtml = () => {
    const rows = optionalItems
      .map((it, idx) => {
        const km = kindMetaOf(it.kind);
        return `<div class="rw-opt-row">
          <span class="rw-badge ${km.cls}">${km.label}</span>
          <span class="rw-opt-name">${escHtml(optionalDisplayName(it))}</span>
          <span class="muted rw-opt-id">${escHtml(it.id)}${it.group ? ` · ${escHtml(foodGroupLabel(it.group as never) || it.group)}` : ""}</span>
          <button type="button" class="rw-opt-del" data-odel="${idx}" title="删除该条目">✕</button>
        </div>`;
      })
      .join("");
    return `<p class="modal-hint">optionalRecipeMatchListItems：注册进关卡匹配表的额外节点（DLC 原始菜谱、Hotdog 可选部件/酱料/煮肠、自选披萨部件…）。<b>保存菜谱不再自动填充</b>，此处手动增删后单独写回；一键填充仅把候选加入列表，是否写回由你决定。</p>
      ${presets ? "" : '<div class="rw-warn">⚠ 一键填充候选加载失败或旧桥不支持——已保存条目仍可查看/删除/写回。</div>'}
      <div class="rw-toolbar">
        <button type="button" class="modal-btn" id="rw-opt-fill-pizza">🍕 披萨一键填充</button>
        <button type="button" class="modal-btn" id="rw-opt-fill-hotdog">🌭 Hotdog 一键填充</button>
        <button type="button" class="modal-btn" id="rw-opt-toggle-picker">${optPickerOpen ? "收起添加面板" : "＋ 添加条目"}</button>
      </div>
      ${optPickerOpen ? optPickerHtml() : ""}
      <div class="rw-rows rw-opt-list">${rows || '<p class="muted">暂无条目。DLC 原始菜谱/热狗酱料等不再自动注册——需要时用上方按钮或添加面板配置。</p>'}</div>`;
  };

  const wireOptional = () => {
    document.getElementById("rw-opt-fill-pizza")?.addEventListener("click", fillPizzaOptionals);
    document.getElementById("rw-opt-fill-hotdog")?.addEventListener("click", fillHotdogOptionals);
    document.getElementById("rw-opt-toggle-picker")?.addEventListener("click", () => {
      optPickerOpen = !optPickerOpen;
      render();
    });
    document.querySelectorAll<HTMLElement>("[data-ofilter]").forEach((chip) => {
      chip.addEventListener("click", () => {
        optFilter = chip.dataset.ofilter ?? "all";
        document.querySelectorAll<HTMLElement>("[data-ofilter]").forEach((c) => c.classList.toggle("active", c === chip));
        refreshOptPickerList();
      });
    });
    document.getElementById("rw-opt-search")?.addEventListener("input", refreshOptPickerList);
    wireOptCandCards();
    document.querySelectorAll<HTMLElement>(".rw-opt-del").forEach((btn) => {
      btn.addEventListener("click", () => {
        const idx = Number(btn.dataset.odel);
        if (Number.isInteger(idx) && idx >= 0 && idx < optionalItems.length) {
          optionalItems.splice(idx, 1);
          optionalDirty = true;
          render();
        }
      });
    });
  };

  const doSaveOptional = async () => {
    if (!level?.levelInfoAssetPath) return;
    try {
      await saveOptionalItems(level.levelInfoAssetPath, optionalItems.map((i) => i.guid));
      level.optionalItems = optionalItems.map((i) => ({ ...i }));
      optionalDirty = false;
      setStatus(`Optional 已写回（${optionalItems.length} 条）`);
      render();
    } catch (e) {
      setStatus((e as Error).message, false);
    }
  };

  // ---------- 📋 Matchlist 管理 tab ----------
  // includeRecipeMatchLists 手动勾选（commonW1 包装白名单）。保存菜谱不再按 DLC
  // 自动并入；「按已选菜谱一键填充」仅勾选，写回由用户决定。story 匹配表由
  // 运行时无条件并入（bundle18），无需也不可在此配置。
  const ML_KEYS = ["dlc02", "dlc03", "dlc04", "dlc05", "dlc07", "dlc08", "dlc09", "dlc10", "dlc11", "dlc13", "combineddlc"];

  const matchlistTabHtml = () => {
    const cards = ML_KEYS.map((k) => {
      const on = matchlistKeys.has(k);
      return `<label class="rw-ml-card${on ? " on" : ""}">
        <input type="checkbox" data-mlkey="${escHtml(k)}" ${on ? "checked" : ""}/>
        <span class="rw-ml-key">${escHtml(k === "combineddlc" ? "combineddlc（组合包）" : (foodGroupLabel(k as never) || k))}</span>
      </label>`;
    }).join("");
    return `<p class="modal-hint">includeRecipeMatchLists：并入对应 DLC 的整套匹配节点（食材/订单/可选自由拼接/套餐）。<b>保存菜谱不再自动并入</b>——选择了 DLC 菜谱时建议在此开启对应项；story 匹配表运行时始终并入，无需配置。</p>
      <div class="rw-toolbar">
        <button type="button" class="modal-btn" id="rw-ml-fill-sel">按已选菜谱一键填充</button>
      </div>
      <div class="rw-ml-grid">${cards}</div>`;
  };

  const wireMatchlist = () => {
    document.querySelectorAll<HTMLInputElement>("[data-mlkey]").forEach((cb) => {
      cb.addEventListener("change", () => {
        const k = cb.dataset.mlkey ?? "";
        if (cb.checked) matchlistKeys.add(k);
        else matchlistKeys.delete(k);
        matchlistDirty = true;
        cb.closest(".rw-ml-card")?.classList.toggle("on", cb.checked);
        const tabsEl = document.getElementById("rw-tabs");
        if (tabsEl) { tabsEl.innerHTML = tabsHtml(); wireTabs(); }
        renderFooter();
      });
    });
    document.getElementById("rw-ml-fill-sel")?.addEventListener("click", () => {
      const dlcs = new Set<string>();
      for (const g of selected) {
        const r = byGuid.get(g);
        if (r?.group && r.group.startsWith("dlc")) dlcs.add(r.group);
      }
      let added = 0;
      for (const k of dlcs) {
        if (ML_KEYS.includes(k) && !matchlistKeys.has(k)) { matchlistKeys.add(k); added++; }
      }
      matchlistDirty = true;
      setStatus(added ? `已勾选 ${added} 个 matchlist（待写回）` : "已选菜谱未涉及新的 DLC");
      render();
    });
  };

  const doSaveMatchlist = async () => {
    if (!level?.levelInfoAssetPath) return;
    try {
      const keys = ML_KEYS.filter((k) => matchlistKeys.has(k));
      await saveMatchlists(level.levelInfoAssetPath, keys);
      const prev = level.matchlists ?? [];
      level.matchlists = keys.map((key) => ({ key, guid: prev.find((m) => m.key === key)?.guid ?? "" }));
      matchlistDirty = false;
      setStatus(`Matchlist 已写回（${keys.length} 个）`);
      render();
    } catch (e) {
      setStatus((e as Error).message, false);
    }
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
