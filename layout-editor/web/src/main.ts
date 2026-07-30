import "./style.css";
import {
  fetchFloorMaterials,
  fetchGrid,
  fetchHealth,
  fetchHealthInfo,
  fetchIngredients,
  STALE_BRIDGE_MSG,
  fetchLayout,
  fetchLevelRecipes,
  fetchLevelSets,
  fetchRecipeCatalog,
  loadCatalog,
  saveLayout,
  saveLevelRecipes,
  setDeathTheme,
  setKillPlaneBounds,
} from "./api";
import { renderManageView, goManage, consumeTargetScene } from "./levels";
import { showBusy, hideBusy } from "./busy";
import {
  closeModal,
  openFoodSpawnerEditor,
  openIngredientMultiPicker,
  openModal,
} from "./modals";
import { ingredientNameZh, ingredientOptionLabel } from "./ingredientLabels";
import { tidyCatalogNameZh } from "./displayLabels";
import { paintStyleForItem } from "./itemColors";
import {
  BG_THEMES,
  bgTheme,
  bgThemeKeyForDeathType,
  bgThemeTooltip,
  deathLabelZh,
  inferBgThemeFromItems,
  isSurfaceItem,
  isThemeBackgroundPrefabId,
  materialBilingual,
  surfaceKindLabelZh,
  surfacePaint,
  themeBackgroundPrefabIds,
} from "./floorColors";
import { snapFootprintCenter, snapValue } from "./snap";
import {
  drawLayerForItem,
  findStackHost,
  hostRuleLabelZh,
  isStackUtensilCatalog,
  trySnapUtensilToHost,
} from "./stacking";
import type {
  CatalogItem,
  DeathInfo,
  FloorMaterial,
  FloorObject,
  GridInfo,
  LayoutItem,
  LayoutDocument,
  RecipeEntry,
  WalkableRect,
} from "./types";

const CELL = 1.2;
const PX_PER_UNIT = 48;

const FOOTPRINT_BY_ID: Record<string, { cellsX: number; cellsZ: number }> = {
  ServingStation: { cellsX: 2, cellsZ: 1 },
  Sink: { cellsX: 2, cellsZ: 1 },
};

type EditorItem = LayoutItem & {
  _editorKey: string;
  _wx: number;
  _wz: number;
  _parentWx: number;
  _parentWz: number;
};

type EditorFloor = FloorObject & {
  _key: string;
  _wx: number;
  _wz: number;
  _wCells: number;
  _dCells: number;
};

let catalogByGuid = new Map<string, CatalogItem>();
let items: EditorItem[] = [];
let floors: EditorFloor[] = [];
let walkable: WalkableRect[] = [];
let deathInfo: DeathInfo | null = null;
let bgThemeKey = "void";
let bgThemeDirty = false;
let autoKillPlane = false;
let autoWalkable = true;
let floorMaterials: FloorMaterial[] = [];
let selectedKey: string | null = null;
let selectedKeys = new Set<string>();
let selectedFloorKey: string | null = null;
let currentLayer: "items" | "floor" = "items";
let scenePath = "";
let snapStep = 0.6;
let showGrid = true;
let gridInfo: GridInfo | null = null;

let scale = 1;
let panX = 0;
let panY = 0;

let dragCatalog: CatalogItem | null = null;
let dragItemKey: string | null = null;
let dragOffsetX = 0;
let dragOffsetZ = 0;
let dragGroupKeys: string[] = [];
let dragLastWx = 0;
let dragLastWz = 0;
let marqueeing = false;
let marqueeAdd = false;
let marqueeStartX = 0;
let marqueeStartY = 0;
let marqueeCurX = 0;
let marqueeCurY = 0;
let spaceHeld = false;
let panning = false;
let lastMx = 0;
let lastMy = 0;

let dragFloorKey: string | null = null;
let dragFloorMode: "move" | "resize" = "move";
let dragFloorEdge: string = "";
let dragFloorAnchorX = 0;
let dragFloorAnchorZ = 0;

let ingredientsCache: import("./types").IngredientEntry[] = [];
let currentLevelSet = "";

function levelSetFromScenePath(assetPath: string): string {
  const parts = assetPath.replace(/\\/g, "/").split("/");
  const i = parts.indexOf("LevelSets");
  return i >= 0 && parts.length > i + 1 ? parts[i + 1] : "";
}

const STUB_KIND_BY_PREFAB_ID: Record<string, string> = {
  Dispenser: "Dispenser",
  AttachingFoodSpawner: "AttachingFoodSpawner",
  ConveyorStation: "Conveyor",
  Teleportal: "Teleportal",
  Pot: "CookingUtensil",
  FryPan: "CookingUtensil",
  Steamer: "CookingUtensil",
  FrierBasket: "CookingUtensil",
  MixerBowl: "CookingUtensil",
  CleanPlateStack: "CleanPlateStack",
  Travelator: "Travelator",
  Flamethrower: "Flamethrower",
  Burner: "Burner",
};

function stubKindOf(item: EditorItem): string {
  if (item.stubKind) return item.stubKind;
  const prefabId = prefabIdFromPath(item.prefabAssetPath);
  return STUB_KIND_BY_PREFAB_ID[prefabId] ?? "";
}

/** Prefab-serialized defaults (from common01/common02 prefabs) used when export data isn't available yet. */
function defaultUtensilCapacity(item: EditorItem): number {
  return prefabIdFromPath(item.prefabAssetPath) === "MixerBowl" ? 4 : 1;
}

const BURNER_FIRE_MODES = ["Direct（直射）", "Parabolic（抛物线）"];

function stubControlsHtml(item: EditorItem): string {
  const kind = stubKindOf(item);
  switch (kind) {
    case "Dispenser": {
      const cur = item.dispenser?.spawnerItemPrefabGuid ?? "";
      const opts = ['<option value="">— 未设置 —</option>']
        .concat(
          ingredientsCache.map(
            (ing) =>
              `<option value="${ing.guid}" ${ing.guid === cur ? "selected" : ""}>${escHtml(ingredientOptionLabel(ing))}</option>`
          )
        )
        .join("");
      return `<div class="ctx-stub"><div class="ctx-stub-title">食材箱参数</div>
        <label class="ctx-stub-row">食材 <select id="ctx-stub-ing" class="ctx-input">${opts}</select></label></div>`;
    }
    case "AttachingFoodSpawner": {
      const fs = item.foodSpawner ?? {};
      return `<div class="ctx-stub"><div class="ctx-stub-title">食材生成器参数</div>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-fs-order" ${fs.spawnInOrder !== false ? "checked" : ""}/> 按顺序生成</label>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-fs-start" ${fs.triggerAtStart !== false ? "checked" : ""}/> 开局触发</label>
        <label class="ctx-stub-row">触发间隔 <input type="number" id="ctx-fs-time" class="ctx-input" step="0.5" min="0" value="${fs.triggerTime ?? 5}"/> 秒</label>
        <button type="button" class="ctx-btn" id="ctx-fs-ings">食材列表 (${(fs.attachmentPrefabGuids ?? []).length})…</button></div>`;
    }
    case "CookingUtensil": {
      const cu = item.cookingUtensil ?? {};
      const allowed = (cu.allowedIngredientGuids ?? []).length;
      return `<div class="ctx-stub"><div class="ctx-stub-title">锅具参数</div>
        <label class="ctx-stub-row">最多食材数 <input type="number" id="ctx-cu-cap" class="ctx-input" min="0" step="1" value="${cu.capacity ?? defaultUtensilCapacity(item)}"/></label>
        <button type="button" class="ctx-btn" id="ctx-cu-ings">允许的食材 (${allowed > 0 ? allowed : "全部"})…</button></div>`;
    }
    case "Conveyor": {
      const sp = item.conveyor?.conveySpeed ?? 0.5;
      return `<div class="ctx-stub"><div class="ctx-stub-title">传送带参数</div>
        <label class="ctx-stub-row">速度 <input type="number" id="ctx-cv-speed" class="ctx-input" step="0.1" value="${sp}"/>（负值反向）</label></div>`;
    }
    case "Teleportal": {
      const tp = item.teleportal ?? {};
      teleportalLabels = computeTeleportalLabels();
      const colorOpts = PORTAL_COLOR_NAMES.map(
        (n, i) => `<option value="${i}" ${(tp.portalColor ?? 0) === i ? "selected" : ""}>${n}</option>`
      ).join("");
      const others = teleportals().filter((t) => t._editorKey !== item._editorKey);
      const exitOpts = ['<option value="">— 未绑定 —</option>']
        .concat(
          others.map(
            (t) =>
              `<option value="${t.instanceId}" ${tp.exitPortalInstanceId === t.instanceId ? "selected" : ""}>传送门 ${teleportalLabels.get(t.instanceId) ?? "?"}（${escHtml(itemLabel(t))}）</option>`
          )
        )
        .join("");
      return `<div class="ctx-stub"><div class="ctx-stub-title">传送门参数</div>
        <label class="ctx-stub-row">颜色 <select id="ctx-tp-color" class="ctx-input">${colorOpts}</select></label>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-tp-ds" ${tp.doubleSided ? "checked" : ""}/> 双向</label>
        <label class="ctx-stub-row">出口 <select id="ctx-tp-exit" class="ctx-input">${exitOpts}</select></label></div>`;
    }
    case "Travelator": {
      const sp = item.travelator?.speed ?? 2.5;
      return `<div class="ctx-stub"><div class="ctx-stub-title">移动地板参数</div>
        <label class="ctx-stub-row">速度 <input type="number" id="ctx-tv-speed" class="ctx-input" step="0.1" min="0" value="${sp}"/></label></div>`;
    }
    case "Flamethrower": {
      const rate = item.flamethrower?.cookingRate ?? 4;
      return `<div class="ctx-stub"><div class="ctx-stub-title">喷火器参数</div>
        <label class="ctx-stub-row">烹饪速率 <input type="number" id="ctx-ft-rate" class="ctx-input" step="0.5" min="0" value="${rate}"/></label></div>`;
    }
    case "CleanPlateStack": {
      const ps = item.cleanPlateStack ?? {};
      return `<div class="ctx-stub"><div class="ctx-stub-title">盘子堆参数</div>
        <label class="ctx-stub-row">盘子数量 <input type="number" id="ctx-ps-count" class="ctx-input" min="0" step="1" value="${ps.plateCount ?? 5}"/></label></div>`;
    }
    case "Burner": {
      const b = item.burner ?? {};
      const modeOpts = BURNER_FIRE_MODES.map(
        (n, i) => `<option value="${i}" ${(b.fireMode ?? 1) === i ? "selected" : ""}>${n}</option>`
      ).join("");
      return `<div class="ctx-stub"><div class="ctx-stub-title">火焰喷射器参数</div>
        <label class="ctx-stub-row">开火模式 <select id="ctx-bn-mode" class="ctx-input">${modeOpts}</select></label>
        <label class="ctx-stub-row">空中时间 <input type="number" id="ctx-bn-air" class="ctx-input" step="0.1" min="0" value="${b.airTime ?? 2}"/> 秒</label>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-bn-rand" ${b.randomTargetOrder ? "checked" : ""}/> 随机目标顺序</label>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-bn-hide" ${b.hideVisual ? "checked" : ""}/> 隐藏模型</label></div>`;
    }
    default:
      return "";
  }
}

function wireStubControls(item: EditorItem) {
  const kind = stubKindOf(item);
  if (!kind) return;

  const num = (id: string): HTMLInputElement | null =>
    document.getElementById(id) as HTMLInputElement | null;

  switch (kind) {
    case "Dispenser": {
      num("ctx-stub-ing")?.addEventListener("change", (e) => {
        item.stubKind = "Dispenser";
        item.dispenser = { spawnerItemPrefabGuid: (e.target as HTMLSelectElement).value };
        draw();
        setStatus("已设置食材箱食材（写回后生效）");
      });
      break;
    }
    case "AttachingFoodSpawner": {
      const ensure = () => {
        item.stubKind = "AttachingFoodSpawner";
        if (!item.foodSpawner) item.foodSpawner = {};
        return item.foodSpawner;
      };
      num("ctx-fs-order")?.addEventListener("change", (e) => {
        ensure().spawnInOrder = (e.target as HTMLInputElement).checked;
      });
      num("ctx-fs-start")?.addEventListener("change", (e) => {
        ensure().triggerAtStart = (e.target as HTMLInputElement).checked;
      });
      num("ctx-fs-time")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (isFinite(v) && v >= 0) ensure().triggerTime = v;
      });
      document.getElementById("ctx-fs-ings")?.addEventListener("click", () => {
        ensure();
        hideContextMenu();
        openFoodSpawnerEditor(item, ingredientsCache, (patch) => {
          item.foodSpawner = patch;
          draw();
          setStatus("已更新食材生成器参数（写回后生效）");
        });
      });
      break;
    }
    case "CookingUtensil": {
      const ensure = () => {
        item.stubKind = "CookingUtensil";
        if (!item.cookingUtensil) item.cookingUtensil = {};
        if (item.cookingUtensil.capacity == null)
          item.cookingUtensil.capacity = defaultUtensilCapacity(item);
        return item.cookingUtensil;
      };
      num("ctx-cu-cap")?.addEventListener("change", (e) => {
        const v = parseInt((e.target as HTMLInputElement).value, 10);
        if (isFinite(v) && v >= 0) {
          ensure().capacity = v;
          setStatus(`锅具容量已设为 ${v}（写回后生效）`);
        }
      });
      document.getElementById("ctx-cu-ings")?.addEventListener("click", () => {
        ensure();
        hideContextMenu();
        openIngredientMultiPicker(
          "锅具 · 允许的食材",
          "allowedIngredientSOs（不勾选任何项 = 允许全部）",
          ingredientsCache,
          item.cookingUtensil?.allowedIngredientGuids ?? [],
          (guids) => {
            ensure().allowedIngredientGuids = guids;
            draw();
            setStatus("已更新锅具允许食材（写回后生效）");
          }
        );
      });
      break;
    }
    case "Conveyor": {
      num("ctx-cv-speed")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (!isFinite(v)) return;
        item.stubKind = "Conveyor";
        item.conveyor = { conveySpeed: v };
        draw();
        setStatus(`传送带速度已设为 ${v}（写回后生效）`);
      });
      break;
    }
    case "Teleportal": {
      const ensure = () => {
        item.stubKind = "Teleportal";
        if (!item.teleportal) item.teleportal = { exitPortalInstanceId: "", portalColor: 0, doubleSided: false };
        return item.teleportal;
      };
      num("ctx-tp-color")?.addEventListener("change", (e) => {
        ensure().portalColor = parseInt((e.target as HTMLSelectElement).value, 10) || 0;
        draw();
      });
      num("ctx-tp-ds")?.addEventListener("change", (e) => {
        ensure().doubleSided = (e.target as HTMLInputElement).checked;
        draw();
      });
      num("ctx-tp-exit")?.addEventListener("change", (e) => {
        ensure().exitPortalInstanceId = (e.target as HTMLSelectElement).value;
        draw();
        setStatus("已更新传送门配对（写回后生效）");
      });
      break;
    }
    case "Travelator": {
      num("ctx-tv-speed")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (!isFinite(v) || v < 0) return;
        item.stubKind = "Travelator";
        item.travelator = { speed: v };
        setStatus(`移动地板速度已设为 ${v}（写回后生效）`);
      });
      break;
    }
    case "Flamethrower": {
      num("ctx-ft-rate")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (!isFinite(v) || v < 0) return;
        item.stubKind = "Flamethrower";
        item.flamethrower = { cookingRate: v };
        setStatus(`喷火器烹饪速率已设为 ${v}（写回后生效）`);
      });
      break;
    }
    case "CleanPlateStack": {
      num("ctx-ps-count")?.addEventListener("change", (e) => {
        const v = parseInt((e.target as HTMLInputElement).value, 10);
        if (!isFinite(v) || v < 0) return;
        item.stubKind = "CleanPlateStack";
        if (!item.cleanPlateStack) item.cleanPlateStack = {};
        item.cleanPlateStack.plateCount = v;
        setStatus(`盘子堆数量已设为 ${v}（写回后生效）`);
      });
      break;
    }
    case "Burner": {
      const ensure = () => {
        item.stubKind = "Burner";
        if (!item.burner) item.burner = {};
        if (item.burner.fireMode == null) item.burner.fireMode = 1;
        if (item.burner.airTime == null) item.burner.airTime = 2;
        if (item.burner.randomTargetOrder == null) item.burner.randomTargetOrder = false;
        if (item.burner.hideVisual == null) item.burner.hideVisual = false;
        return item.burner;
      };
      num("ctx-bn-mode")?.addEventListener("change", (e) => {
        ensure().fireMode = parseInt((e.target as HTMLSelectElement).value, 10) || 0;
      });
      num("ctx-bn-air")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (isFinite(v) && v >= 0) ensure().airTime = v;
      });
      num("ctx-bn-rand")?.addEventListener("change", (e) => {
        ensure().randomTargetOrder = (e.target as HTMLInputElement).checked;
      });
      num("ctx-bn-hide")?.addEventListener("change", (e) => {
        ensure().hideVisual = (e.target as HTMLInputElement).checked;
      });
      break;
    }
  }
}

async function openRecipesDialog() {
  if (!scenePath) {
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
      fetchRecipeCatalog(currentLevelSet),
      fetchLevelRecipes(scenePath),
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
  const byGuid = new Map(recipes.map((r) => [r.guid, r]));

  const listHtml = recipes
    .map((r) => {
      const checked = selected.has(r.guid) ? "checked" : "";
      const en = r.nameEn ? ` <span class="muted">${escHtml(r.nameEn)}</span>` : "";
      const cust = r.isCustom ? ' <span class="muted" title="自定义菜谱">🔧</span>' : "";
      return `<label class="modal-check"><input type="checkbox" value="${r.guid}" ${checked}> ${escHtml(r.nameZh)}${en}${cust}</label>`;
    })
    .join("");

  openModal(
    `关卡菜谱 · ${level.levelName || "未命名"}`,
    `<div class="rw">
       <div class="rw-col">
         <input type="search" id="rw-search" class="rw-search" placeholder="搜索菜谱…">
         <div class="modal-scroll rw-list" id="rw-list">${listHtml}</div>
       </div>
       <div class="rw-col">
         <div class="rw-analysis" id="rw-analysis"></div>
       </div>
     </div>`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
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
    for (const it of items) {
      if (it.stubKind === "Dispenser") {
        const id = ingredientIdByGuid(it.dispenser?.spawnerItemPrefabGuid);
        if (id) s.add(id);
      }
    }
    return s;
  };
  const existingPrefabIds = (): Set<string> => new Set(items.map((it) => prefabIdFromPath(it.prefabAssetPath)));

  const analysisHtml = (): string => {
    const recs = currentRecipes();
    if (recs.length === 0) return `<p class="modal-hint">未选择菜谱，勾选左侧菜谱后查看所需食材与锅具。</p>`;

    const reqIngs = new Set<string>();
    const steps = new Set<string>();
    for (const r of recs) {
      (r.ingredients ?? []).forEach((i) => reqIngs.add(i));
      if (r.cookingStep) steps.add(r.cookingStep);
    }
    const haveDisp = existingDispenserIngIds();
    const havePref = existingPrefabIds();
    const missingIngs = [...reqIngs].filter((i) => !haveDisp.has(i));
    const reqUt = computeRequiredUtensils(reqIngs, steps);
    const missingUt = reqUt.filter((u) => !havePref.has(u));

    const ingRows = [...reqIngs]
      .sort()
      .map((i) => {
        const ok = !missingIngs.includes(i);
        return `<div class="rw-row ${ok ? "" : "miss"}"><span class="rw-mark">${ok ? "✓" : "✗"}</span>${escHtml(ingredientNameById(i))} <span class="muted">${escHtml(i)}</span></div>`;
      })
      .join("");
    const utRows = reqUt
      .map((u) => {
        const ok = !missingUt.includes(u);
        const cat = catalogItemById(u);
        const label = cat ? tidyCatalogNameZh(cat.nameZh) : u;
        return `<div class="rw-row ${ok ? "" : "miss"}"><span class="rw-mark">${ok ? "✓" : "✗"}</span>${escHtml(label)} <span class="muted">${escHtml(u)}</span></div>`;
      })
      .join("");

    const ingFill = missingIngs.length
      ? `<button type="button" class="modal-btn primary rw-fill" id="rw-fill-ing">一键补齐缺失食材箱 (${missingIngs.length})</button>`
      : `<p class="modal-hint ok">食材箱已齐全</p>`;
    const utFill = missingUt.length
      ? `<button type="button" class="modal-btn primary rw-fill" id="rw-fill-ut">一键补齐缺失锅具/道具 (${missingUt.length})</button>`
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
    document.getElementById("rw-fill-ing")?.addEventListener("click", () => {
      fillMissingDispensers();
      rerender();
    });
    document.getElementById("rw-fill-ut")?.addEventListener("click", () => {
      fillMissingUtensils();
      rerender();
    });
  };

  listEl.querySelectorAll<HTMLInputElement>("input[type=checkbox]").forEach((cb) =>
    cb.addEventListener("change", () => {
      if (cb.checked) selected.add(cb.value);
      else selected.delete(cb.value);
      rerender();
    })
  );
  searchEl.addEventListener("input", () => {
    const q = searchEl.value.trim().toLowerCase();
    listEl.querySelectorAll<HTMLLabelElement>("label.modal-check").forEach((lb) => {
      const txt = lb.textContent?.toLowerCase() ?? "";
      lb.style.display = !q || txt.includes(q) ? "" : "none";
    });
  });

  rerender();

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
    const reqIngs = new Set<string>();
    for (const r of currentRecipes()) (r.ingredients ?? []).forEach((i) => reqIngs.add(i));
    const have = existingDispenserIngIds();
    const missing = [...reqIngs].filter((i) => !have.has(i));
    if (!missing.length) return;
    const cat = catalogItemById("Dispenser");
    if (!cat) return;
    const base = placementBase();
    let idx = 0;
    for (const ing of missing) {
      const it = addFromCatalog(cat, base.x + idx * CELL, base.z);
      it.dispenser = { spawnerItemPrefabGuid: ingredientGuidById(ing) ?? "" };
      idx++;
    }
  }

  function fillMissingUtensils() {
    const reqIngs = new Set<string>();
    const steps = new Set<string>();
    for (const r of currentRecipes()) {
      (r.ingredients ?? []).forEach((i) => reqIngs.add(i));
      if (r.cookingStep) steps.add(r.cookingStep);
    }
    const have = existingPrefabIds();
    const missing = computeRequiredUtensils(reqIngs, steps).filter((u) => !have.has(u));
    if (!missing.length) return;
    const base = placementBase();
    let idx = 0;
    for (const u of missing) {
      const cat = catalogItemById(u);
      if (!cat) continue;
      addFromCatalog(cat, base.x + idx * CELL, base.z - 2 * CELL);
      idx++;
    }
  }
}

const app = document.getElementById("app")!;
const MANAGE_ACTIVE = /^#\/manage/.test(location.hash);

if (!MANAGE_ACTIVE) {
app.innerHTML = `
  <div class="toolbar">
    <label>场景</label>
    <select id="scene-select"><option value="">加载中…</option></select>
    <button id="btn-reload">重新加载</button>
    <button id="btn-save" class="primary">写回 Unity</button>
    <button id="btn-recipes" type="button">菜谱…</button>
    <button id="btn-manage" type="button">关卡管理…</button>
    <div class="layer-tabs" id="layer-tabs">
      <button type="button" data-layer="items" class="layer-tab active">📦 物品层</button>
      <button type="button" data-layer="floor" class="layer-tab">🗺️ 地板 / 背景层</button>
    </div>
    <label><input type="checkbox" id="snap-half" checked /> 半格 (0.6)</label>
    <label><input type="checkbox" id="show-grid" checked /> 显示网格</label>
    <span id="status" class="status">连接中…</span>
  </div>
  <div class="main">
    <aside class="palette">
      <div class="palette-header">
        <input type="search" id="palette-search" placeholder="搜索 prefab…" />
      </div>
      <div class="palette-cats" id="palette-cats"></div>
    </aside>
    <div class="canvas-wrap">
      <canvas id="canvas"></canvas>
      <div id="item-detail" class="item-detail hidden" role="dialog"></div>
      <div id="ctx-menu" class="ctx-menu hidden" role="dialog"></div>
      <div id="floor-bar" class="floor-bar hidden"></div>
      <div class="hint">拖拽空白框选 · Shift 加选 · Ctrl+C/V 复制粘贴 · 空格+拖动平移 · 右键微移/旋转/改参数 · Del 删除 · R/Shift+R 旋转90° · 滚轮缩放</div>
    </div>
  </div>
`;
}

const sceneSelect = document.getElementById("scene-select") as HTMLSelectElement;
const statusEl = document.getElementById("status")!;
const paletteCats = document.getElementById("palette-cats")!;
const canvas = document.getElementById("canvas") as HTMLCanvasElement;
const ctx = (canvas && canvas.getContext("2d")) as CanvasRenderingContext2D;
const detailEl = document.getElementById("item-detail")!;
const ctxMenuEl = document.getElementById("ctx-menu")!;
const floorBar = document.getElementById("floor-bar")!;

function setStatus(text: string, ok = true) {
  statusEl.textContent = text;
  statusEl.className = "status " + (ok ? "ok" : "err");
}

function normalizeRot(deg: number): number {
  return ((deg % 360) + 360) % 360;
}

function prefabIdFromPath(assetPath: string | undefined): string {
  if (!assetPath) return "";
  const name = assetPath.split("/").pop() ?? "";
  return name.replace(/\.prefab$/i, "");
}

function resolveFootprint(item: LayoutItem): { cellsX: number; cellsZ: number } {
  const cx = item.footprint?.cellsX ?? 0;
  const cz = item.footprint?.cellsZ ?? 0;
  if (cx > 0 && cz > 0) return { cellsX: cx, cellsZ: cz };

  const cat = catalogByGuid.get(item.prefabGuid);
  if (cat?.footprint && cat.footprint.cellsX > 0 && cat.footprint.cellsZ > 0) {
    return { cellsX: cat.footprint.cellsX, cellsZ: cat.footprint.cellsZ };
  }

  const id = prefabIdFromPath(item.prefabAssetPath) || cat?.id || "";
  const known = FOOTPRINT_BY_ID[id];
  if (known) return known;

  return { cellsX: 1, cellsZ: 1 };
}

function itemScaleX(item: LayoutItem): number {
  const s = item.localScale?.x;
  return s && s > 0 ? s : 1;
}

function itemScaleZ(item: LayoutItem): number {
  const s = item.localScale?.z;
  return s && s > 0 ? s : 1;
}

function itemUniformScale(item: LayoutItem): number {
  return itemScaleX(item);
}

function setItemUniformScale(item: EditorItem, n: number) {
  const y = item.localScale?.y ?? 1;
  item.localScale = { x: n, y, z: n };
}

function worldToCanvas(wx: number, wz: number): { x: number; y: number } {
  const cx = canvas.width / 2 + panX + wx * PX_PER_UNIT * scale;
  const cy = canvas.height / 2 + panY - wz * PX_PER_UNIT * scale;
  return { x: cx, y: cy };
}

function canvasToWorld(cx: number, cy: number): { x: number; z: number } {
  const wx = (cx - canvas.width / 2 - panX) / (PX_PER_UNIT * scale);
  const wz = -(cy - canvas.height / 2 - panY) / (PX_PER_UNIT * scale);
  return { x: wx, z: wz };
}

function newEditorKey(): string {
  return crypto.randomUUID();
}

function enrichItem(raw: LayoutItem, editorKey: string): EditorItem {
  const wp = raw.worldPosition ?? raw.localPosition;
  const fp = resolveFootprint(raw);
  return {
    ...raw,
    _editorKey: editorKey,
    footprint: fp,
    _wx: wp.x,
    _wz: wp.z,
    _parentWx: wp.x - raw.localPosition.x,
    _parentWz: wp.z - raw.localPosition.z,
  };
}

function enrichFloor(raw: FloorObject, key: string): EditorFloor {
  const wp = raw.worldPosition ?? raw.localPosition;
  const w = raw.widthCells > 0 ? raw.widthCells : Math.max(1, Math.round(raw.widthUnits / CELL));
  const d = raw.depthCells > 0 ? raw.depthCells : Math.max(1, Math.round(raw.depthUnits / CELL));
  return {
    ...raw,
    _key: key,
    _wx: wp.x,
    _wz: wp.z,
    _wCells: w,
    _dCells: d,
  };
}

function itemLabel(item: EditorItem): string {
  const id = prefabIdFromPath(item.prefabAssetPath);
  const isDispenser = item.stubKind === "Dispenser" || id === "Dispenser";
  if (isDispenser) {
    const ingZh = ingredientNameZh(ingredientsCache, item.dispenser?.spawnerItemPrefabGuid);
    if (ingZh !== "未设置") return ingZh;
  }

  const cat = catalogByGuid.get(item.prefabGuid);
  if (cat?.nameZh) return tidyCatalogNameZh(cat.nameZh);
  if (item.displayName) return tidyCatalogNameZh(item.displayName);
  return id || "?";
}

function updateCanvasCursor() {
  canvas.classList.remove("pan-ready", "pan-active");
  if (panning) canvas.classList.add("pan-active");
  else if (spaceHeld) canvas.classList.add("pan-ready");
}

function wrapTextLines(ctx: CanvasRenderingContext2D, text: string, maxWidth: number): string[] {
  if (maxWidth <= 4) return [text.slice(0, 1)];
  const chars = Array.from(text);
  const lines: string[] = [];
  let line = "";
  for (const ch of chars) {
    const test = line + ch;
    if (ctx.measureText(test).width > maxWidth && line.length > 0) {
      lines.push(line);
      line = ch;
    } else {
      line = test;
    }
  }
  if (line) lines.push(line);
  return lines.length ? lines : [""];
}

function drawLabelInBox(
  ctx: CanvasRenderingContext2D,
  text: string,
  boxW: number,
  boxH: number
) {
  const pad = 3;
  const innerW = Math.max(4, boxW - pad * 2);
  const innerH = Math.max(4, boxH - pad * 2);
  let fontSize = Math.max(8, Math.min(12, innerH * 0.32, 11 * scale));

  for (let attempt = 0; attempt < 6; attempt++) {
    ctx.font = `${fontSize}px system-ui, sans-serif`;
    const lines = wrapTextLines(ctx, text, innerW);
    const lineHeight = fontSize * 1.12;
    if (lines.length * lineHeight <= innerH || fontSize <= 8) {
      const startY = -((lines.length - 1) * lineHeight) / 2;
      for (let i = 0; i < lines.length; i++) {
        ctx.fillText(lines[i], 0, startY + i * lineHeight);
      }
      return;
    }
    fontSize -= 1;
  }
}

function drawVoidHatch(color: string) {
  const w = canvas.clientWidth;
  const h = canvas.clientHeight;
  ctx.save();
  ctx.strokeStyle = color;
  ctx.lineWidth = 1;
  const step = 26;
  ctx.beginPath();
  for (let x = -h; x < w; x += step) {
    ctx.moveTo(x, 0);
    ctx.lineTo(x + h, h);
  }
  ctx.stroke();
  ctx.restore();
}

function drawKillPlanes() {
  const planes = deathInfo?.killPlanes ?? [];
  for (const kp of planes) {
    if ((kp.sx ?? 0) <= 0.001 || (kp.sz ?? 0) <= 0.001) continue;
    const a = worldToCanvas(kp.cx - kp.sx / 2, kp.cz + kp.sz / 2);
    const b = worldToCanvas(kp.cx + kp.sx / 2, kp.cz - kp.sz / 2);
    const x = Math.min(a.x, b.x);
    const y = Math.min(a.y, b.y);
    const w = Math.abs(b.x - a.x);
    const h = Math.abs(b.y - a.y);
    ctx.save();
    ctx.fillStyle = "rgba(242,139,130,0.10)";
    ctx.fillRect(x, y, w, h);
    ctx.strokeStyle = "rgba(242,139,130,0.95)";
    ctx.lineWidth = 2;
    ctx.setLineDash([8, 5]);
    ctx.strokeRect(x, y, w, h);
    ctx.setLineDash([]);
    ctx.fillStyle = "rgba(242,139,130,0.98)";
    ctx.font = "bold 12px sans-serif";
    ctx.textAlign = "left";
    ctx.textBaseline = "top";
    ctx.fillText(`坠落区 · ${kp.respawnType || "死亡"}`, x + 5, y + 5);
    ctx.restore();
  }
}

function computeLevelBounds(): { cx: number; cz: number; sx: number; sz: number } | null {
  let minX = Infinity;
  let maxX = -Infinity;
  let minZ = Infinity;
  let maxZ = -Infinity;
  const consider = (x: number, z: number, hx = 0, hz = 0) => {
    minX = Math.min(minX, x - hx);
    maxX = Math.max(maxX, x + hx);
    minZ = Math.min(minZ, z - hz);
    maxZ = Math.max(maxZ, z + hz);
  };
  for (const f of floors) {
    if (f.surfaceKind === "background") continue;
    consider(f._wx, f._wz, (f._wCells * CELL) / 2, (f._dCells * CELL) / 2);
  }
  for (const it of items) {
    const cat = catalogByGuid.get(it.prefabGuid);
    if (cat?.surfaceTier === "background") continue;
    if (cat?.surfaceTier !== "floor" && cat?.layoutTier !== "core") continue;
    const fp = resolveFootprint(it);
    consider(it._wx, it._wz, (fp.cellsX * CELL) / 2 * itemScaleX(it), (fp.cellsZ * CELL) / 2 * itemScaleZ(it));
  }
  for (const r of walkable) consider(r.cx, r.cz, r.sx / 2, r.sz / 2);
  if (!isFinite(minX)) return null;
  const margin = CELL;
  minX -= margin;
  minZ -= margin;
  maxX += margin;
  maxZ += margin;
  return { cx: (minX + maxX) / 2, cz: (minZ + maxZ) / 2, sx: maxX - minX, sz: maxZ - minZ };
}

function draw() {
  const w = canvas.clientWidth;
  const h = canvas.clientHeight;
  if (canvas.width !== w || canvas.height !== h) {
    canvas.width = w;
    canvas.height = h;
  }

  const onFloor = currentLayer === "floor";
  const theme = bgTheme(bgThemeKey);
  ctx.fillStyle = onFloor ? theme.fill : "#1a1d23";
  ctx.fillRect(0, 0, w, h);

  if (onFloor) drawVoidHatch(theme.hatch);

  if (showGrid) drawGrid();

  if (onFloor) {
    drawWalkable();
    drawFloorPlanes(false);
    drawFloorAdjacentSeams();
    drawFloorPlanes(true);
    drawSurfaceItems(false);
    const ghostItems = items.filter((it) => !isSurfaceItem(catalogByGuid.get(it.prefabGuid)));
    if (ghostItems.length > 0) {
      teleportalLabels = computeTeleportalLabels();
      const sortedGhost = [...ghostItems].sort(
        (a, b) => drawLayerForItem(a, catalogByGuid) - drawLayerForItem(b, catalogByGuid)
      );
      ctx.save();
      ctx.globalAlpha = 0.32;
      for (const it of sortedGhost) drawItem(it, false);
      ctx.restore();
    }
    drawKillPlanes();
  } else {
    if (floors.length > 0) {
      ctx.save();
      ctx.globalAlpha = 0.5;
      drawFloorPlanes(false);
      ctx.restore();
    }
    const ghostSurface = items.filter((it) => isSurfaceItem(catalogByGuid.get(it.prefabGuid)));
    if (ghostSurface.length > 0) {
      ctx.save();
      ctx.globalAlpha = 0.32;
      for (const it of ghostSurface) drawItem(it, false);
      ctx.restore();
    }
    const sorted = [...items]
      .filter((it) => !isSurfaceItem(catalogByGuid.get(it.prefabGuid)))
      .sort((a, b) => drawLayerForItem(a, catalogByGuid) - drawLayerForItem(b, catalogByGuid));
    teleportalLabels = computeTeleportalLabels();
    for (const item of sorted) {
      drawItem(item, isSelected(item._editorKey));
    }
    drawTeleportalLinks();
    if (marqueeing) drawMarquee();
  }

  updateFloorBar();
}

function drawMarquee() {
  const x = Math.min(marqueeStartX, marqueeCurX);
  const y = Math.min(marqueeStartY, marqueeCurY);
  const w = Math.abs(marqueeCurX - marqueeStartX);
  const h = Math.abs(marqueeCurY - marqueeStartY);
  ctx.save();
  ctx.fillStyle = "rgba(61,107,243,0.12)";
  ctx.fillRect(x, y, w, h);
  ctx.strokeStyle = "rgba(61,107,243,0.9)";
  ctx.lineWidth = 1;
  ctx.setLineDash([4, 3]);
  ctx.strokeRect(x, y, w, h);
  ctx.setLineDash([]);
  ctx.restore();
}

function updateMarqueeSelection() {
  const minX = Math.min(marqueeStartX, marqueeCurX);
  const maxX = Math.max(marqueeStartX, marqueeCurX);
  const minY = Math.min(marqueeStartY, marqueeCurY);
  const maxY = Math.max(marqueeStartY, marqueeCurY);
  const next = marqueeAdd ? new Set(selectedKeys) : new Set<string>();
  for (const it of items) {
    if (currentLayer !== "floor" && isSurfaceItem(catalogByGuid.get(it.prefabGuid))) continue;
    const p = worldToCanvas(it._wx, it._wz);
    if (p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY) next.add(it._editorKey);
  }
  selectedKeys = next;
  const keys = selectionKeys();
  selectedKey = keys.length ? keys[keys.length - 1] : null;
}

/** Union bbox of visible floor planes, or null if there are none. */
function floorUnionBBox(): { x0: number; z0: number; x1: number; z1: number } | null {
  let x0 = Infinity;
  let z0 = Infinity;
  let x1 = -Infinity;
  let z1 = -Infinity;
  for (const f of floors) {
    if (f.surfaceKind === "background") continue;
    const hw = (f._wCells * CELL) / 2;
    const hh = (f._dCells * CELL) / 2;
    x0 = Math.min(x0, f._wx - hw);
    x1 = Math.max(x1, f._wx + hw);
    z0 = Math.min(z0, f._wz - hh);
    z1 = Math.max(z1, f._wz + hh);
  }
  if (!isFinite(x0)) return null;
  return { x0, z0, x1, z1 };
}

/** Sub-rectangles of W that lie outside F (W \ F), as world XZ rects. */
function rectMinus(
  wx0: number,
  wz0: number,
  wx1: number,
  wz1: number,
  f: { x0: number; z0: number; x1: number; z1: number }
): Array<{ x0: number; z0: number; x1: number; z1: number }> {
  const ox0 = Math.max(wx0, f.x0);
  const ox1 = Math.min(wx1, f.x1);
  const oz0 = Math.max(wz0, f.z0);
  const oz1 = Math.min(wz1, f.z1);
  if (ox0 >= ox1 || oz0 >= oz1) return [{ x0: wx0, z0: wz0, x1: wx1, z1: wz1 }];
  const out: Array<{ x0: number; z0: number; x1: number; z1: number }> = [];
  if (wx0 < ox0) out.push({ x0: wx0, z0: wz0, x1: ox0, z1: wz1 });
  if (ox1 < wx1) out.push({ x0: ox1, z0: wz0, x1: wx1, z1: wz1 });
  if (wz0 < oz0) out.push({ x0: ox0, z0: wz0, x1: ox1, z1: oz0 });
  if (oz1 < wz1) out.push({ x0: ox0, z0: oz1, x1: ox1, z1: wz1 });
  return out;
}

function drawWalkable() {
  const floorBox = floorUnionBBox();
  for (const r of walkable) {
    const a = worldToCanvas(r.cx - r.sx / 2, r.cz + r.sz / 2);
    const b = worldToCanvas(r.cx + r.sx / 2, r.cz - r.sz / 2);
    const x = Math.min(a.x, b.x);
    const y = Math.min(a.y, b.y);
    const bw = Math.abs(b.x - a.x);
    const bh = Math.abs(b.y - a.y);
    const ice = r.surfaceType === "ice";
    ctx.fillStyle = ice ? "rgba(120,200,235,0.16)" : "rgba(150,140,120,0.14)";
    ctx.fillRect(x, y, bw, bh);
    ctx.strokeStyle = ice ? "rgba(90,170,210,0.4)" : "rgba(160,150,130,0.3)";
    ctx.setLineDash([4, 3]);
    ctx.lineWidth = 1;
    ctx.strokeRect(x, y, bw, bh);
    ctx.setLineDash([]);

    if (floorBox) {
      const air = rectMinus(
        r.cx - r.sx / 2,
        r.cz - r.sz / 2,
        r.cx + r.sx / 2,
        r.cz + r.sz / 2,
        floorBox
      );
      let labeled = false;
      for (const seg of air) {
        const sa = worldToCanvas(seg.x0, seg.z1);
        const sb = worldToCanvas(seg.x1, seg.z0);
        const sx = Math.min(sa.x, sb.x);
        const sy = Math.min(sa.y, sb.y);
        const sw = Math.abs(sb.x - sa.x);
        const sh = Math.abs(sb.y - sa.y);
        ctx.save();
        ctx.fillStyle = "rgba(249,171,0,0.16)";
        ctx.fillRect(sx, sy, sw, sh);
        ctx.strokeStyle = "rgba(249,171,0,0.7)";
        ctx.setLineDash([6, 4]);
        ctx.lineWidth = 1.5;
        ctx.strokeRect(sx, sy, sw, sh);
        ctx.setLineDash([]);
        if (!labeled && sw > 24 && sh > 14) {
          ctx.fillStyle = "rgba(249,171,0,0.98)";
          ctx.font = "bold 11px sans-serif";
          ctx.textAlign = "left";
          ctx.textBaseline = "top";
          ctx.fillText("空气地板（可行走但无可见地板）", sx + 4, sy + 4);
          labeled = true;
        }
        ctx.restore();
      }
    }
  }
}

function drawSurfaceItems(highlight: boolean) {
  const sorted = [...items]
    .filter((it) => isSurfaceItem(catalogByGuid.get(it.prefabGuid)))
    .sort((a, b) => drawLayerForItem(a, catalogByGuid) - drawLayerForItem(b, catalogByGuid));
  for (const item of sorted) {
    const selected = highlight && item._editorKey === selectedKey;
    drawItem(item, selected);
  }
}

function drawFloorPlanes(highlight: boolean) {
  for (const f of floors) {
    // Backdrop planes (often huge, default-white in Unity) — theme fill shows the void color.
    if (f.surfaceKind === "background") continue;
    const selected = highlight && f._key === selectedFloorKey;
    drawFloorPlane(f, selected, false);
  }
}

function floorRectPx(f: EditorFloor) {
  const center = worldToCanvas(f._wx, f._wz);
  const cellPx = CELL * PX_PER_UNIT * scale;
  const bw = f._wCells * cellPx;
  const bh = f._dCells * cellPx;
  const rot = normalizeRot(f.localRotationY);
  return { center, bw, bh, rot };
}

function drawFloorPlane(f: EditorFloor, selected: boolean, ghost: boolean) {
  const { center, bw, bh, rot } = floorRectPx(f);
  const paint = surfacePaint(f.surfaceKind, selected);

  ctx.save();
  ctx.translate(center.x, center.y);
  ctx.rotate((-rot * Math.PI) / 180);

  ctx.fillStyle = paint.fill;
  ctx.fillRect(-bw / 2, -bh / 2, bw, bh);

  // Dashed outer border to convey the floor surface.
  ctx.strokeStyle = paint.stroke;
  ctx.lineWidth = selected ? 2.5 : ghost ? 1 : 1.5;
  ctx.setLineDash(selected ? [] : [7, 4]);
  ctx.strokeRect(-bw / 2, -bh / 2, bw, bh);
  ctx.setLineDash([]);

  // Faint dashed internal cell grid → "perspective" tiling.
  const cellPx = CELL * PX_PER_UNIT * scale;
  if (!ghost && cellPx > 6) {
    ctx.strokeStyle = "rgba(255,255,255,0.10)";
    ctx.lineWidth = 1;
    ctx.setLineDash([3, 4]);
    for (let i = 1; i < f._wCells; i++) {
      const x = -bw / 2 + i * cellPx;
      ctx.beginPath();
      ctx.moveTo(x, -bh / 2);
      ctx.lineTo(x, bh / 2);
      ctx.stroke();
    }
    for (let j = 1; j < f._dCells; j++) {
      const y = -bh / 2 + j * cellPx;
      ctx.beginPath();
      ctx.moveTo(-bw / 2, y);
      ctx.lineTo(bw / 2, y);
      ctx.stroke();
    }
    ctx.setLineDash([]);
  }

  // Resize handles when selected.
  if (selected && !ghost) {
    ctx.fillStyle = "#f9ab00";
    for (const hx of [-bw / 2, bw / 2]) {
      for (const hy of [-bh / 2, bh / 2]) {
        ctx.fillRect(hx - 3, hy - 3, 6, 6);
      }
    }
  }

  // Label: size in cells + kind emoji.
  ctx.beginPath();
  ctx.rect(-bw / 2 + 2, -bh / 2 + 2, bw - 4, bh - 4);
  ctx.clip();
  ctx.fillStyle = paint.label;
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  const emoji = paint.emoji ? paint.emoji + " " : "";
  drawLabelInBox(ctx, `${emoji}${f._wCells}×${f._dCells}格`, bw - 6, bh - 6);
  ctx.restore();
}

/** Draw a brighter dashed seam wherever two floor rectangles share an edge. */
function drawFloorAdjacentSeams() {
  if (floors.length < 2) return;
  const tol = CELL * 0.35;
  for (let i = 0; i < floors.length; i++) {
    for (let j = i + 1; j < floors.length; j++) {
      const a = floors[i];
      const b = floors[j];
      const aL = a._wx - (a._wCells * CELL) / 2;
      const aR = a._wx + (a._wCells * CELL) / 2;
      const aT = a._wz + (a._dCells * CELL) / 2;
      const aB = a._wz - (a._dCells * CELL) / 2;
      const bL = b._wx - (b._wCells * CELL) / 2;
      const bR = b._wx + (b._wCells * CELL) / 2;
      const bT = b._wz + (b._dCells * CELL) / 2;
      const bB = b._wz - (b._dCells * CELL) / 2;

      // Vertical shared edge (left/right touch, z ranges overlap).
      const zLo = Math.max(aB, bB);
      const zHi = Math.min(aT, bT);
      if (zHi - zLo > tol) {
        if (Math.abs(aR - bL) <= tol) drawSeam(aR, zLo, aR, zHi);
        else if (Math.abs(aL - bR) <= tol) drawSeam(aL, zLo, aL, zHi);
      }
      // Horizontal shared edge (top/bottom touch, x ranges overlap).
      const xLo = Math.max(aL, bL);
      const xHi = Math.min(aR, bR);
      if (xHi - xLo > tol) {
        if (Math.abs(aT - bB) <= tol) drawSeam(xLo, aT, xHi, aT);
        else if (Math.abs(aB - bT) <= tol) drawSeam(xLo, aB, xHi, aB);
      }
    }
  }
  ctx.setLineDash([]);
}

function drawSeam(wx1: number, wz1: number, wx2: number, wz2: number) {
  const p1 = worldToCanvas(wx1, wz1);
  const p2 = worldToCanvas(wx2, wz2);
  ctx.strokeStyle = "rgba(255,235,170,0.5)";
  ctx.lineWidth = 1.5;
  ctx.setLineDash([5, 4]);
  ctx.beginPath();
  ctx.moveTo(p1.x, p1.y);
  ctx.lineTo(p2.x, p2.y);
  ctx.stroke();
}

function floorLocalPoint(f: EditorFloor, wx: number, wz: number): { lx: number; lz: number } {
  const dx = wx - f._wx;
  const dz = wz - f._wz;
  const rad = (normalizeRot(f.localRotationY) * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  return { lx: dx * cos + dz * sin, lz: -dx * sin + dz * cos };
}

function hitTestFloor(wx: number, wz: number): {
  floor: EditorFloor;
  mode: "move" | "resize";
  edge: string;
  anchorX: number;
  anchorZ: number;
} | null {
  for (let i = floors.length - 1; i >= 0; i--) {
    const f = floors[i];
    const hw = (f._wCells * CELL) / 2;
    const hh = (f._dCells * CELL) / 2;
    const { lx, lz } = floorLocalPoint(f, wx, wz);
    if (Math.abs(lx) > hw || Math.abs(lz) > hh) continue;

    // Corner handle hit → resize.
    const handleTol = Math.max(CELL * 0.5, 0.9);
    const nearLeft = lx < -hw + handleTol;
    const nearRight = lx > hw - handleTol;
    const nearBottom = lz < -hh + handleTol;
    const nearTop = lz > hh - handleTol;
    if ((nearLeft || nearRight) && (nearBottom || nearTop)) {
      const edge = `${nearRight ? "R" : "L"}${nearTop ? "T" : "B"}`;
      // Anchor = opposite corner in world space.
      const ax = nearRight ? -hw : hw;
      const az = nearTop ? -hh : hh;
      const rad = (normalizeRot(f.localRotationY) * Math.PI) / 180;
      const cos = Math.cos(rad);
      const sin = Math.sin(rad);
      const anchorX = f._wx + ax * cos - az * sin;
      const anchorZ = f._wz + ax * sin + az * cos;
      return { floor: f, mode: "resize", edge, anchorX, anchorZ };
    }
    return { floor: f, mode: "move", edge: "", anchorX: 0, anchorZ: 0 };
  }
  return null;
}

function dragFloor(f: EditorFloor, mx: number, my: number) {
  const { x: wx, z: wz } = canvasToWorld(mx, my);
  if (dragFloorMode === "move") {
    f._wx = snapValue(wx, snapStep);
    f._wz = snapValue(wz, snapStep);
  } else {
    // Resize: opposite corner (anchor) stays fixed; compute new width/depth.
    const rad = (normalizeRot(f.localRotationY) * Math.PI) / 180;
    const cos = Math.cos(rad);
    const sin = Math.sin(rad);
    const dx = wx - dragFloorAnchorX;
    const dz = wz - dragFloorAnchorZ;
    const lx = dx * cos + dz * sin;
    const lz = -dx * sin + dz * cos;
    const newW = Math.max(1, Math.round((Math.abs(lx) / CELL) || 1));
    const newD = Math.max(1, Math.round((Math.abs(lz) / CELL) || 1));
    // Recenter so the anchor corner stays put.
    const signX = dragFloorEdge.includes("R") ? 1 : -1;
    const signZ = dragFloorEdge.includes("T") ? 1 : -1;
    const newCxLocal = signX * (newW * CELL) / 2;
    const newCzLocal = signZ * (newD * CELL) / 2;
    f._wx = dragFloorAnchorX + newCxLocal * cos - newCzLocal * sin;
    f._wz = dragFloorAnchorZ + newCxLocal * sin + newCzLocal * cos;
    f._wCells = newW;
    f._dCells = newD;
  }
}

function finalizeFloor(f: EditorFloor) {
  f._wx = snapValue(f._wx, snapStep);
  f._wz = snapValue(f._wz, snapStep);
  f.localPosition.x = f._wx;
  f.localPosition.z = f._wz;
  f.worldPosition.x = f._wx;
  f.worldPosition.z = f._wz;
  f.widthUnits = f._wCells * CELL;
  f.depthUnits = f._dCells * CELL;
  // Smart material match by size tag.
  if (f.surfaceKind !== "background") tryMatchFloorMaterialBySize(f);
}

function tryMatchFloorMaterialBySize(f: EditorFloor) {
  const tag = `${f._wCells}x${f._dCells}`;
  const match = floorMaterials.find((m) => m.sizeTag === tag);
  if (match && match.guid !== f.materialGuid) {
    f.materialGuid = match.guid;
    f.materialAssetPath = match.assetPath;
    f.materialName = match.id;
    setStatus(`已按尺寸 ${tag} 自动匹配材质：${match.nameZh}`);
  }
}

function pointInWalkable(wx: number, wz: number): boolean {
  for (const r of walkable) {
    if (Math.abs(wx - r.cx) <= r.sx / 2 + 0.01 && Math.abs(wz - r.cz) <= r.sz / 2 + 0.01)
      return true;
  }
  return false;
}

/** Returns a warning string if an item sits over a void/water zone, else null. */
function itemVoidWarning(item: EditorItem): string | null {
  if (walkable.length === 0) return null;
  if (pointInWalkable(item._wx, item._wz)) return null;
  const name = itemLabel(item);
  switch (deathInfo?.deathType) {
    case "water":
      return `⚠「${name}」位于水面/空洞上方（玩家会落水），已保留但请确认`;
    case "goo":
      return `⚠「${name}」位于黏液/空洞上方（玩家会坠入），已保留但请确认`;
    default:
      return `⚠「${name}」位于空洞上方（会坠落），已保留但请确认`;
  }
}

function warnItemVoid(item: EditorItem) {
  const w = itemVoidWarning(item);
  if (w) setStatus(w, false);
}

function addFloorAt(wx: number, wz: number) {  const snapped = snapValue(wx, snapStep);
  const snappedZ = snapValue(wz, snapStep);
  const id = `new:floor:${crypto.randomUUID()}`;
  const key = newEditorKey();
  const w = 4;
  const d = 4;
  const defaultMat = floorMaterials.find((m) => /floor|blacktiles|path/i.test(m.id));
  const floor: EditorFloor = {
    instanceId: id,
    _key: key,
    hierarchyPath: id,
    parentPath: "Art/Ground",
    displayName: "Floor",
    surfaceKind: "solid",
    meshType: "plane",
    meshFileId: 10209,
    materialGuid: defaultMat?.guid,
    materialAssetPath: defaultMat?.assetPath,
    materialName: defaultMat?.id,
    localPosition: { x: snapped, y: -0.05, z: snappedZ },
    worldPosition: { x: snapped, y: -0.05, z: snappedZ },
    localRotationY: 0,
    localScale: { x: (w * CELL) / 10, y: 1, z: (d * CELL) / 10 },
    widthUnits: w * CELL,
    depthUnits: d * CELL,
    widthCells: w,
    depthCells: d,
    _wx: snapped,
    _wz: snappedZ,
    _wCells: w,
    _dCells: d,
  };
  floors.push(floor);
  selectedFloorKey = key;
  draw();
  setStatus("已新增地板（写回后生效）");
}

function showFloorDetail(f: EditorFloor, clientX: number, clientY: number) {
  const matchedMat = floorMaterials.find((m) => m.guid === f.materialGuid);
  const areaCells = f._wCells * f._dCells;
  const matRows = floorMaterials
    .map((m) => {
      const bl = materialBilingual(m.id);
      return `<button type="button" class="mat-pick${m.guid === f.materialGuid ? " active" : ""}" data-guid="${m.guid}"><span class="mat-id">${bl.zh}</span><span class="mat-sub">${bl.en}</span></button>`;
    })
    .join("");
  detailEl.innerHTML = `
    <h3>${surfaceKindLabelZh(f.surfaceKind)} · ${f.displayName}</h3>
    <dl>
      <dt>类型</dt><dd>${surfaceKindLabelZh(f.surfaceKind)}${f.meshType === "plane" ? "（Plane 平面）" : f.meshType === "quad" ? "（Quad）" : ""}</dd>
      <dt>尺寸</dt><dd id="fe-size">${f._wCells} × ${f._dCells} 格 (${(f._wCells * CELL).toFixed(1)} × ${(f._dCells * CELL).toFixed(1)} m，${areaCells} 格)</dd>
      <dt>材质</dt><dd id="fe-mat">${matchedMat?.nameZh ?? f.materialName ?? "无"}</dd>
      <dt>坐标</dt><dd>x ${f._wx.toFixed(2)}, z ${f._wz.toFixed(2)}</dd>
      <dt>旋转</dt><dd>${normalizeRot(f.localRotationY)}°</dd>
      <dt>死亡类型</dt><dd>${deathLabelZh(deathInfo)}（只读）</dd>
    </dl>
    <div class="floor-edit-row">
      <label>宽(格) <input type="number" min="1" id="fe-w" value="${f._wCells}" /></label>
      <label>高(格) <input type="number" min="1" id="fe-d" value="${f._dCells}" /></label>
      <span class="muted" style="align-self:center;font-size:11px">自动应用（仅页面）</span>
    </div>
    <div class="mat-pick-title">切换材质（点击应用）</div>
    <div class="mat-pick-list">${matRows || '<div class="mat-pick-empty">当前关卡集无材质</div>'}</div>
    <p class="close-hint">拖四角缩放 · 宽高输入即时生效 · 左键拖动移动 · 右键此面板切换材质 · Esc 关闭</p>
  `;
  detailEl.classList.remove("hidden");
  positionDetail(clientX, clientY);

  const feW = document.getElementById("fe-w") as HTMLInputElement;
  const feD = document.getElementById("fe-d") as HTMLInputElement;
  const applyFloorSizeLive = () => {
    const wv = parseInt(feW.value, 10);
    const dv = parseInt(feD.value, 10);
    if (wv > 0) f._wCells = wv;
    if (dv > 0) f._dCells = dv;
    finalizeFloor(f);
    draw();
    const sz = document.getElementById("fe-size");
    if (sz)
      sz.textContent = `${f._wCells} × ${f._dCells} 格 (${(f._wCells * CELL).toFixed(1)} × ${(f._dCells * CELL).toFixed(1)} m，${f._wCells * f._dCells} 格)`;
    const m = floorMaterials.find((x) => x.guid === f.materialGuid);
    const matEl = document.getElementById("fe-mat");
    if (matEl) matEl.textContent = m?.nameZh ?? f.materialName ?? "无";
    detailEl.querySelectorAll<HTMLButtonElement>(".mat-pick").forEach((b) =>
      b.classList.toggle("active", b.dataset.guid === f.materialGuid)
    );
  };
  feW.addEventListener("input", applyFloorSizeLive);
  feD.addEventListener("input", applyFloorSizeLive);
  feW.addEventListener("change", applyFloorSizeLive);
  feD.addEventListener("change", applyFloorSizeLive);

  detailEl.querySelectorAll<HTMLButtonElement>(".mat-pick").forEach((btn) => {
    btn.addEventListener("click", () => {
      const m = floorMaterials.find((x) => x.guid === btn.dataset.guid);
      if (!m) return;
      f.materialGuid = m.guid;
      f.materialAssetPath = m.assetPath;
      f.materialName = m.id;
      draw();
      setStatus(`已切换材质：${m.nameZh}（写回后生效）`);
      showFloorDetail(f, clientX, clientY);
    });
  });
}

function positionDetail(clientX: number, clientY: number) {
  const margin = 8;
  let left = clientX + margin;
  let top = clientY + margin;
  detailEl.style.left = `${left}px`;
  detailEl.style.top = `${top}px`;
  requestAnimationFrame(() => {
    const rect = detailEl.getBoundingClientRect();
    if (rect.right > window.innerWidth - margin) {
      left = Math.max(margin, clientX - rect.width - margin);
      detailEl.style.left = `${left}px`;
    }
    if (rect.bottom > window.innerHeight - margin) {
      top = Math.max(margin, clientY - rect.height - margin);
      detailEl.style.top = `${top}px`;
    }
  });
}

function drawGrid() {
  const w = canvas.width;
  const h = canvas.height;
  const step = CELL * PX_PER_UNIT * scale;
  if (step < 4) return;

  let ox = w / 2 + panX;
  let oy = h / 2 + panY;
  let halfX = 20;
  let halfZ = 20;

  if (gridInfo?.found) {
    const c = worldToCanvas(gridInfo.worldPosition.x, gridInfo.worldPosition.z);
    ox = c.x;
    oy = c.y;
    const cs = gridInfo.cellSize?.x ?? CELL;
    halfX = gridInfo.gridHalfSizeX || 10;
    halfZ = gridInfo.gridHalfSizeZ || 10;
    const gw = cs * PX_PER_UNIT * scale;
    ctx.strokeStyle = "rgba(61, 107, 243, 0.35)";
    ctx.lineWidth = 1;
    ctx.strokeRect(ox - halfX * gw, oy - halfZ * gw, halfX * 2 * gw, halfZ * 2 * gw);
  }

  ctx.strokeStyle = "rgba(255,255,255,0.06)";
  ctx.lineWidth = 1;
  const startX = (ox % step) - step;
  for (let x = startX; x < w; x += step) {
    ctx.beginPath();
    ctx.moveTo(x, 0);
    ctx.lineTo(x, h);
    ctx.stroke();
  }
  const startY = (oy % step) - step;
  for (let y = startY; y < h; y += step) {
    ctx.beginPath();
    ctx.moveTo(0, y);
    ctx.lineTo(w, y);
    ctx.stroke();
  }
}

const PORTAL_COLORS = [
  "#9ad7ff",
  "#5b8def",
  "#ef6f6f",
  "#f0a847",
  "#7bd889",
  "#9c8a5a",
  "#c792ea",
  "#b15bd9",
  "#e8945a",
];

let teleportalLabels = new Map<string, string>();

function isConveyorItem(item: EditorItem): boolean {
  return item.stubKind === "Conveyor" || prefabIdFromPath(item.prefabAssetPath) === "ConveyorStation";
}

function isTeleportalItem(item: EditorItem): boolean {
  return item.stubKind === "Teleportal" || prefabIdFromPath(item.prefabAssetPath) === "Teleportal";
}

function teleportals(): EditorItem[] {
  return items.filter(isTeleportalItem);
}

function computeTeleportalLabels(): Map<string, string> {
  const tp = teleportals();
  const byInst = new Map(tp.map((i) => [i.instanceId, i]));
  const label = new Map<string, string>();
  let n = 0;
  for (const t of tp) {
    if (label.has(t.instanceId)) continue;
    const lab = (++n).toString();
    label.set(t.instanceId, lab);
    const exitId = t.teleportal?.exitPortalInstanceId;
    if (exitId && byInst.has(exitId) && !label.has(exitId)) label.set(exitId, lab);
  }
  for (const t of tp) if (!label.has(t.instanceId)) label.set(t.instanceId, "?");
  return label;
}

function drawConveyorArrow(center: { x: number; y: number }, rot: number, cellPx: number, speed: number) {
  const rad = (rot * Math.PI) / 180;
  let dx = Math.sin(rad);
  let dy = -Math.cos(rad);
  if (speed < 0) {
    dx = -dx;
    dy = -dy;
  }
  const L = cellPx * 0.42;
  const x0 = center.x - dx * L;
  const y0 = center.y - dy * L;
  const x1 = center.x + dx * L;
  const y1 = center.y + dy * L;
  ctx.save();
  ctx.strokeStyle = "#ffe49a";
  ctx.fillStyle = "#ffe49a";
  ctx.lineWidth = Math.max(2, cellPx * 0.12);
  ctx.lineCap = "round";
  ctx.beginPath();
  ctx.moveTo(x0, y0);
  ctx.lineTo(x1, y1);
  ctx.stroke();
  const ah = cellPx * 0.22;
  const px = -dy;
  const py = dx;
  const bx = x1 - dx * ah;
  const by = y1 - dy * ah;
  ctx.beginPath();
  ctx.moveTo(x1, y1);
  ctx.lineTo(bx + px * ah * 0.65, by + py * ah * 0.65);
  ctx.lineTo(bx - px * ah * 0.65, by - py * ah * 0.65);
  ctx.closePath();
  ctx.fill();
  ctx.restore();
}

function drawTeleportalBadge(item: EditorItem, center: { x: number; y: number }, cellPx: number) {
  const color = PORTAL_COLORS[item.teleportal?.portalColor ?? 0] ?? "#c792ea";
  const label = teleportalLabels.get(item.instanceId) ?? "?";
  const r = cellPx * 0.46;
  ctx.save();
  ctx.strokeStyle = color;
  ctx.lineWidth = Math.max(2.5, cellPx * 0.1);
  ctx.beginPath();
  ctx.arc(center.x, center.y, r, 0, Math.PI * 2);
  ctx.stroke();
  if (item.teleportal?.doubleSided) {
    ctx.setLineDash([4, 3]);
    ctx.beginPath();
    ctx.arc(center.x, center.y, r * 0.7, 0, Math.PI * 2);
    ctx.stroke();
    ctx.setLineDash([]);
  }
  const bx = center.x + r * 0.72;
  const by = center.y - r * 0.72;
  ctx.fillStyle = color;
  ctx.beginPath();
  ctx.arc(bx, by, cellPx * 0.26, 0, Math.PI * 2);
  ctx.fill();
  ctx.fillStyle = "#1a1d23";
  ctx.font = `bold ${Math.max(10, Math.round(cellPx * 0.3))}px sans-serif`;
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillText(label, bx, by);
  ctx.restore();
}

function drawTeleportalLinks() {
  const tp = teleportals();
  const byInst = new Map(tp.map((i) => [i.instanceId, i]));
  for (const t of tp) {
    const exitId = t.teleportal?.exitPortalInstanceId;
    if (!exitId || exitId === t.instanceId) continue;
    const p = byInst.get(exitId);
    if (!p) continue;
    const a = worldToCanvas(t._wx, t._wz);
    const b = worldToCanvas(p._wx, p._wz);
    const color = PORTAL_COLORS[t.teleportal?.portalColor ?? 0] ?? "#c792ea";
    ctx.save();
    ctx.strokeStyle = color;
    ctx.globalAlpha = 0.45;
    ctx.lineWidth = 1.5;
    ctx.setLineDash([6, 4]);
    ctx.beginPath();
    ctx.moveTo(a.x, a.y);
    ctx.lineTo(b.x, b.y);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.restore();
  }
}

function drawSurfaceItem(item: EditorItem, selected: boolean) {
  const cat = catalogByGuid.get(item.prefabGuid);
  const fp = resolveFootprint(item);
  const rot = normalizeRot(item.localRotationY);
  const center = worldToCanvas(item._wx, item._wz);
  const cellPx = CELL * PX_PER_UNIT * scale;
  const sx = itemScaleX(item);
  const sz = itemScaleZ(item);
  const w = fp.cellsX * cellPx * sx;
  const h = fp.cellsZ * cellPx * sz;
  const paint = surfacePaint(cat?.surfaceKind, selected);

  ctx.save();
  ctx.translate(center.x, center.y);
  ctx.rotate((-rot * Math.PI) / 180);

  const bw = Math.max(4, w);
  const bh = Math.max(4, h);
  ctx.fillStyle = paint.fill;
  ctx.fillRect(-bw / 2, -bh / 2, bw, bh);
  ctx.strokeStyle = paint.stroke;
  ctx.lineWidth = selected ? 2 : 1;
  ctx.setLineDash([5, 4]);
  ctx.strokeRect(-bw / 2, -bh / 2, bw, bh);
  ctx.setLineDash([]);

  if (paint.emoji) {
    ctx.font = `${Math.min(14, bh * 0.4)}px system-ui`;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillStyle = paint.label;
    ctx.fillText(paint.emoji, 0, 0);
  } else {
    ctx.beginPath();
    ctx.rect(-bw / 2 + 2, -bh / 2 + 2, bw - 4, bh - 4);
    ctx.clip();
    ctx.fillStyle = paint.label;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    drawLabelInBox(ctx, itemLabel(item), bw - 4, bh - 4);
  }

  ctx.restore();
}

function drawItem(item: EditorItem, selected: boolean) {
  const cat = catalogByGuid.get(item.prefabGuid);
  if (isSurfaceItem(cat)) {
    drawSurfaceItem(item, selected);
    return;
  }
  const fp = resolveFootprint(item);
  const rot = normalizeRot(item.localRotationY);
  const center = worldToCanvas(item._wx, item._wz);
  const cellPx = CELL * PX_PER_UNIT * scale;
  const sx = itemScaleX(item);
  const sz = itemScaleZ(item);
  const w = fp.cellsX * cellPx * sx;
  const h = fp.cellsZ * cellPx * sz;
  const isUtensil = isStackUtensilCatalog(cat);
  const paint = paintStyleForItem(cat, item.parentPath, selected);

  ctx.save();
  ctx.translate(center.x, center.y);
  ctx.rotate((-rot * Math.PI) / 180);

  const inset = isUtensil ? Math.min(cellPx * 0.22, 10) : 0;
  const bw = Math.max(4, w - inset * 2);
  const bh = Math.max(4, h - inset * 2);

  ctx.fillStyle = paint.fill;
  ctx.fillRect(-bw / 2, -bh / 2, bw, bh);

  ctx.strokeStyle = paint.stroke;
  ctx.lineWidth = selected ? 2 : 1;
  ctx.strokeRect(-bw / 2, -bh / 2, bw, bh);

  if (!isUtensil && (fp.cellsX > 1 || fp.cellsZ > 1)) {
    ctx.strokeStyle = "rgba(255,255,255,0.15)";
    ctx.lineWidth = 1;
    for (let i = 1; i < fp.cellsX; i++) {
      const x = -w / 2 + i * cellPx;
      ctx.beginPath();
      ctx.moveTo(x, -h / 2);
      ctx.lineTo(x, h / 2);
      ctx.stroke();
    }
    for (let j = 1; j < fp.cellsZ; j++) {
      const y = -h / 2 + j * cellPx;
      ctx.beginPath();
      ctx.moveTo(-w / 2, y);
      ctx.lineTo(w / 2, y);
      ctx.stroke();
    }
  }

  ctx.beginPath();
  ctx.rect(-bw / 2 + 2, -bh / 2 + 2, bw - 4, bh - 4);
  ctx.clip();

  ctx.fillStyle = paint.label;
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  drawLabelInBox(ctx, itemLabel(item), bw - 4, bh - 4);

  ctx.restore();

  if (isConveyorItem(item)) {
    drawConveyorArrow(center, rot, cellPx, item.conveyor?.conveySpeed ?? 0.5);
  } else if (isTeleportalItem(item)) {
    drawTeleportalBadge(item, center, cellPx);
  }
}

function worldToItemLocal(item: EditorItem, wx: number, wz: number): { lx: number; lz: number } {
  const dx = wx - item._wx;
  const dz = wz - item._wz;
  const rad = (normalizeRot(item.localRotationY) * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  return {
    lx: dx * cos + dz * sin,
    lz: -dx * sin + dz * cos,
  };
}

function hitTest(wx: number, wz: number): EditorItem | null {
  const includeSurface = currentLayer === "floor";
  const sorted = [...items]
    .filter((it) => includeSurface || !isSurfaceItem(catalogByGuid.get(it.prefabGuid)))
    .sort((a, b) => drawLayerForItem(b, catalogByGuid) - drawLayerForItem(a, catalogByGuid));
  for (const item of sorted) {
    const fp = resolveFootprint(item);
    const { lx, lz } = worldToItemLocal(item, wx, wz);
    const hw = ((fp.cellsX * CELL) / 2) * itemScaleX(item);
    const hh = ((fp.cellsZ * CELL) / 2) * itemScaleZ(item);
    if (Math.abs(lx) <= hw && Math.abs(lz) <= hh) return item;
  }
  return null;
}

function hideDetail() {
  detailEl.classList.add("hidden");
}

function stackDetailHtml(item: EditorItem, cat: CatalogItem | undefined): string {
  if (!cat?.stack) return "";
  const host = findStackHost(item, cat, item._wx, item._wz, items, catalogByGuid);
  const ruleLabel = hostRuleLabelZh(cat.stack.hostRule);
  if (host) {
    const hostCat = catalogByGuid.get(host.prefabGuid);
    const hostLabel = itemLabel(host);
    const hostId = prefabIdFromPath(host.prefabAssetPath) || hostCat?.id || "—";
    return `<dt>叠放</dt><dd>堆叠于「${hostLabel}」（${hostId}）之上；规则：${ruleLabel}；本地高度 Y=${cat.stack.y}</dd>`;
  }
  return `<dt>叠放</dt><dd>应堆叠在${ruleLabel}上（当前未对齐到有效载体）；本地高度 Y=${cat.stack.y}</dd>`;
}

const PORTAL_COLOR_NAMES = ["天空", "蓝", "红", "橙", "绿", "树", "紫", "彩紫", "彩橙"];

function extraStubDetailHtml(item: EditorItem): string {
  if (isConveyorItem(item)) {
    const sp = item.conveyor?.conveySpeed ?? 0.5;
    return `<dt>传送带</dt><dd>速度 ${sp.toFixed(2)}（${sp < 0 ? "反向" : "正向"}，箭头指示传送方向）</dd>`;
  }
  if (isTeleportalItem(item)) {
    const exitId = item.teleportal?.exitPortalInstanceId;
    const partner = exitId ? items.find((i) => i.instanceId === exitId) : undefined;
    const myLabel = teleportalLabels.get(item.instanceId) ?? "?";
    const colorName = PORTAL_COLOR_NAMES[item.teleportal?.portalColor ?? 0] ?? String(item.teleportal?.portalColor ?? 0);
    const pairTxt = partner
      ? `已绑定 →「${itemLabel(partner)}」（同组 ${myLabel}）`
      : exitId
        ? "绑定目标不在当前场景"
        : "未绑定";
    const ds = item.teleportal?.doubleSided ? " · 双向" : "";
    return `<dt>传送门</dt><dd>${pairTxt} · 颜色 ${colorName}${ds}</dd>`;
  }
  switch (stubKindOf(item)) {
    case "AttachingFoodSpawner": {
      const fs = item.foodSpawner ?? {};
      const n = (fs.attachmentPrefabGuids ?? []).length;
      return `<dt>食材生成器</dt><dd>${fs.spawnInOrder !== false ? "按顺序" : "随机"}生成 · ${fs.triggerAtStart !== false ? "开局触发" : "不开局触发"} · 间隔 ${fs.triggerTime ?? 5}s · ${n} 种食材（右键直接修改）</dd>`;
    }
    case "CookingUtensil": {
      const cu = item.cookingUtensil ?? {};
      const allowed = (cu.allowedIngredientGuids ?? []).length;
      return `<dt>锅具</dt><dd>最多 ${cu.capacity ?? defaultUtensilCapacity(item)} 个食材 · 允许食材：${allowed > 0 ? `${allowed} 种` : "全部"}（右键直接修改）</dd>`;
    }
    case "Travelator":
      return `<dt>移动地板</dt><dd>速度 ${(item.travelator?.speed ?? 2.5).toFixed(2)}（右键直接修改）</dd>`;
    case "Flamethrower":
      return `<dt>喷火器</dt><dd>烹饪速率 ${(item.flamethrower?.cookingRate ?? 4).toFixed(1)}（右键直接修改）</dd>`;
    case "CleanPlateStack":
      return `<dt>盘子堆</dt><dd>${item.cleanPlateStack?.plateCount ?? 5} 个盘子（右键直接修改）</dd>`;
    case "Burner": {
      const b = item.burner ?? {};
      return `<dt>火焰喷射器</dt><dd>${BURNER_FIRE_MODES[b.fireMode ?? 1]} · 空中时间 ${b.airTime ?? 2}s${b.randomTargetOrder ? " · 随机目标" : ""}${b.hideVisual ? " · 隐藏模型" : ""}（右键直接修改）</dd>`;
    }
    default:
      return "";
  }
}

function showSurfaceItemDetail(item: EditorItem, clientX: number, clientY: number) {
  const cat = catalogByGuid.get(item.prefabGuid);
  const fp = resolveFootprint(item);
  const scale = itemUniformScale(item);
  detailEl.innerHTML = `
    <h3>${surfaceKindLabelZh(cat?.surfaceKind)} · ${itemLabel(item)}</h3>
    <dl>
      <dt>类型</dt><dd>${surfaceKindLabelZh(cat?.surfaceKind)}（地板层 prefab）</dd>
      <dt>占地</dt><dd>${fp.cellsX} × ${fp.cellsZ} 格</dd>
      <dt>坐标</dt><dd>x ${item._wx.toFixed(2)}, z ${item._wz.toFixed(2)}</dd>
      <dt>旋转</dt><dd>${normalizeRot(item.localRotationY)}°</dd>
      <dt>缩放</dt><dd id="si-scale-val">${scale.toFixed(2)}×</dd>
    </dl>
    <div class="floor-edit-row">
      <label>缩放 <input type="number" min="0.5" step="0.1" id="si-scale" value="${scale.toFixed(2)}" /></label>
      <span class="muted" style="align-self:center;font-size:11px">即时生效</span>
    </div>
    <p class="close-hint">右键菜单可微移/旋转 · R/Shift+R 旋转90° · Del 删除 · Esc 关闭</p>
  `;
  detailEl.classList.remove("hidden");
  positionDetail(clientX, clientY);

  const scaleInput = document.getElementById("si-scale") as HTMLInputElement;
  const applyScale = () => {
    const v = parseFloat(scaleInput.value);
    if (!isFinite(v) || v < 0.5) return;
    setItemUniformScale(item, v);
    const el = document.getElementById("si-scale-val");
    if (el) el.textContent = `${v.toFixed(2)}×`;
    draw();
    updateFloorBar();
  };
  scaleInput.addEventListener("input", applyScale);
  scaleInput.addEventListener("change", applyScale);
}

function showDetail(item: EditorItem, clientX: number, clientY: number) {
  const cat = catalogByGuid.get(item.prefabGuid);
  const fp = resolveFootprint(item);
  const id = prefabIdFromPath(item.prefabAssetPath) || cat?.id || "—";

  detailEl.innerHTML = `
    <h3>${itemLabel(item)}</h3>
    <dl>
      <dt>Prefab ID</dt><dd>${id}</dd>
      <dt>中文名</dt><dd>${cat?.nameZh ? tidyCatalogNameZh(cat.nameZh) : "—"}</dd>
      <dt>资源路径</dt><dd>${item.prefabAssetPath || "—"}</dd>
      <dt>层级路径</dt><dd>${item.hierarchyPath}</dd>
      <dt>父节点</dt><dd>${item.parentPath || "—"}</dd>
      <dt>占地</dt><dd>${fp.cellsX} × ${fp.cellsZ} 格 (${(fp.cellsX * CELL).toFixed(1)} × ${(fp.cellsZ * CELL).toFixed(1)} m)</dd>
      <dt>本地坐标</dt><dd>x ${item.localPosition.x.toFixed(2)}, y ${item.localPosition.y.toFixed(2)}, z ${item.localPosition.z.toFixed(2)}</dd>
      <dt>旋转 Y</dt><dd>${normalizeRot(item.localRotationY)}°</dd>
      ${isSurfaceItem(cat) ? `<dt>缩放</dt><dd>${itemUniformScale(item).toFixed(2)}×（右键菜单可调整大小）</dd>` : ""}
      <dt>分类</dt><dd>${isSurfaceItem(cat) ? surfaceKindLabelZh(cat?.surfaceKind) + "（地板层）" : cat?.layoutTier === "decor" ? "装饰道具" : "核心玩法"} · ${cat?.nameZh ? tidyCatalogNameZh(cat.nameZh) : cat?.category ?? "—"}</dd>
      ${stackDetailHtml(item, cat)}
      ${item.stubKind === "Dispenser" ? `<dt>食材</dt><dd>${ingredientNameZh(ingredientsCache, item.dispenser?.spawnerItemPrefabGuid)}</dd>` : ""}
      ${extraStubDetailHtml(item)}
    </dl>
    <p class="close-hint">Esc 关闭</p>
  `;

  const margin = 8;
  let left = clientX + margin;
  let top = clientY + margin;
  detailEl.classList.remove("hidden");
  detailEl.style.left = `${left}px`;
  detailEl.style.top = `${top}px`;

  requestAnimationFrame(() => {
    const rect = detailEl.getBoundingClientRect();
    if (rect.right > window.innerWidth - margin) {
      left = Math.max(margin, clientX - rect.width - margin);
      detailEl.style.left = `${left}px`;
    }
    if (rect.bottom > window.innerHeight - margin) {
      top = Math.max(margin, clientY - rect.height - margin);
      detailEl.style.top = `${top}px`;
    }
  });
}

function snapItemWorld(item: EditorItem, wx: number, wz: number): { x: number; z: number } {
  const fp = resolveFootprint(item);
  return snapFootprintCenter(wx, wz, fp.cellsX, fp.cellsZ, item.localRotationY, CELL, snapStep);
}

function refreshUtensilStacks() {
  for (const item of items) {
    const cat = catalogByGuid.get(item.prefabGuid);
    trySnapUtensilToHost(item, cat, items, catalogByGuid);
  }
}

function syncLocalFromWorld(item: EditorItem) {
  const snapped = snapItemWorld(item, item._wx, item._wz);
  item._wx = snapped.x;
  item._wz = snapped.z;
  const cat = catalogByGuid.get(item.prefabGuid);
  if (cat?.stack) {
    trySnapUtensilToHost(item, cat, items, catalogByGuid);
  }
  item.localPosition.x = snapValue(item._wx - item._parentWx, snapStep);
  item.localPosition.z = snapValue(item._wz - item._parentWz, snapStep);
  if (cat?.stack) {
    item.localPosition.y = cat.stack.y;
  }
}

function isSelected(key: string): boolean {
  return selectedKeys.has(key);
}

function selectionKeys(): string[] {
  return Array.from(selectedKeys);
}

function setSelection(keys: string[], primary?: string): void {
  selectedKeys = new Set(keys);
  selectedKey = keys.length ? primary ?? keys[keys.length - 1] : null;
}

function clearSelection(): void {
  selectedKeys = new Set<string>();
  selectedKey = null;
}

function nudgeItem(item: EditorItem, dx: number, dz: number) {
  item._wx += dx;
  item._wz += dz;
  item.localPosition.x = item._wx - item._parentWx;
  item.localPosition.z = item._wz - item._parentWz;
  const cat = catalogByGuid.get(item.prefabGuid);
  if (cat?.stack) trySnapUtensilToHost(item, cat, items, catalogByGuid);
  updateCtxCoord(item);
  draw();
}

function deleteSelected() {
  const keys = selectionKeys();
  if (keys.length === 0 && !selectedKey) return;
  const kill = new Set(keys.length ? keys : selectedKey ? [selectedKey] : []);
  if (kill.size === 0) return;
  items = items.filter((i) => !kill.has(i._editorKey));
  clearSelection();
  hideDetail();
  hideContextMenu();
  draw();
}

let clipboard: EditorItem[] = [];
let pasteRound = 0;

function copySelection() {
  const keys = selectionKeys();
  if (!keys.length) {
    setStatus("没有选中物品可复制");
    return;
  }
  clipboard = keys
    .map((k) => items.find((i) => i._editorKey === k))
    .filter((i): i is EditorItem => !!i)
    .map((i) => JSON.parse(JSON.stringify(i)) as EditorItem);
  pasteRound = 0;
  setStatus(`已复制 ${clipboard.length} 个物品（Ctrl/Cmd+V 粘贴）`);
}

function pasteClipboard() {
  if (!clipboard.length) {
    setStatus("剪贴板为空（先 Ctrl/Cmd+V 复制）", false);
    return;
  }
  pasteRound++;
  const off = CELL * pasteRound;
  const pasted: string[] = [];
  for (const src of clipboard) {
    const editorKey = newEditorKey();
    const copy = JSON.parse(JSON.stringify(src)) as EditorItem;
    copy._editorKey = editorKey;
    copy.instanceId = `new:copy:${crypto.randomUUID()}`;
    copy.hierarchyPath = copy.instanceId;
    copy._wx = src._wx + off;
    copy._wz = src._wz + off;
    copy.localPosition = {
      x: copy._wx - copy._parentWx,
      y: copy.localPosition.y,
      z: copy._wz - copy._parentWz,
    };
    copy.worldPosition = { x: copy._wx, y: copy.localPosition.y, z: copy._wz };
    items.push(copy);
    pasted.push(editorKey);
  }
  setSelection(pasted);
  hideDetail();
  hideContextMenu();
  draw();
  setStatus(`已粘贴 ${pasted.length} 个物品`);
}

function showContextMenu(item: EditorItem, clientX: number, clientY: number) {
  const cat = catalogByGuid.get(item.prefabGuid);
  const isSurface = isSurfaceItem(cat);
  const stubHtml = stubControlsHtml(item);
  const rot = normalizeRot(item.localRotationY);

  ctxMenuEl.innerHTML = `
    <div class="ctx-head">${itemLabel(item)}</div>
    <div class="ctx-coord" id="ctx-coord">x ${item.localPosition.x.toFixed(2)} · z ${item.localPosition.z.toFixed(2)}</div>
    <div class="ctx-nudge-row">
      <span class="ctx-label">微移 0.1</span>
      <div class="ctx-nudge">
        <button type="button" data-nudge="-0.1,0" title="左移 0.1">←</button>
        <button type="button" data-nudge="0,0.1" title="上移 0.1">↑</button>
        <button type="button" data-nudge="0,-0.1" title="下移 0.1">↓</button>
        <button type="button" data-nudge="0.1,0" title="右移 0.1">→</button>
      </div>
    </div>
    <div class="ctx-nudge-row">
      <span class="ctx-label">旋转 <span id="ctx-rot" class="ctx-scale-val">${rot}°</span></span>
      <div class="ctx-nudge">
        <button type="button" data-rot="-90" title="逆时针 90°">−90°</button>
        <input type="number" id="ctx-rot-input" class="ctx-input ctx-rot-input" min="0" max="359" step="1" value="${rot}" title="任意角度 (0~359)" />
        <button type="button" data-rot="90" title="顺时针 90°">+90°</button>
      </div>
    </div>
    ${
      isSurface
        ? `<div class="ctx-nudge-row">
      <span class="ctx-label">缩放</span>
      <div class="ctx-nudge">
        <button type="button" data-scale="-0.5" title="缩小">−</button>
        <span id="ctx-scale" class="ctx-scale-val">${itemUniformScale(item).toFixed(1)}×</span>
        <button type="button" data-scale="0.5" title="放大">+</button>
      </div>
    </div>`
        : ""
    }
    ${stubHtml}
    <div class="ctx-actions">
      <button type="button" class="ctx-btn" data-act="detail">详情…</button>
      <button type="button" class="ctx-btn" data-act="copy">复制 (Ctrl+C)</button>
      <button type="button" class="ctx-btn" data-act="paste">粘贴 (Ctrl+V)</button>
      <button type="button" class="ctx-btn danger" data-act="delete">删除</button>
    </div>
    <p class="close-hint">点击外部或 Esc 关闭</p>
  `;

  const margin = 8;
  let left = clientX + margin;
  let top = clientY + margin;
  ctxMenuEl.classList.remove("hidden");
  ctxMenuEl.style.left = `${left}px`;
  ctxMenuEl.style.top = `${top}px`;
  requestAnimationFrame(() => {
    const rect = ctxMenuEl.getBoundingClientRect();
    if (rect.right > window.innerWidth - margin) {
      left = Math.max(margin, clientX - rect.width - margin);
      ctxMenuEl.style.left = `${left}px`;
    }
    if (rect.bottom > window.innerHeight - margin) {
      top = Math.max(margin, clientY - rect.height - margin);
      ctxMenuEl.style.top = `${top}px`;
    }
  });

  ctxMenuEl.querySelectorAll<HTMLButtonElement>("[data-nudge]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const parts = btn.dataset.nudge!.split(",").map(Number);
      nudgeItem(item, parts[0], parts[1]);
    });
  });
  ctxMenuEl.querySelectorAll<HTMLButtonElement>("[data-scale]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const delta = parseFloat(btn.dataset.scale!);
      const next = Math.max(0.5, +(itemUniformScale(item) + delta).toFixed(2));
      setItemUniformScale(item, next);
      const scaleEl = document.getElementById("ctx-scale");
      if (scaleEl) scaleEl.textContent = `${next.toFixed(1)}×`;
      draw();
    });
  });
  const applyRotation = (deg: number) => {
    if (!isFinite(deg)) return;
    item.localRotationY = normalizeRot(deg);
    syncLocalFromWorld(item);
    const lbl = document.getElementById("ctx-rot");
    if (lbl) lbl.textContent = `${normalizeRot(item.localRotationY)}°`;
    const inp = document.getElementById("ctx-rot-input") as HTMLInputElement | null;
    if (inp && document.activeElement !== inp) inp.value = String(normalizeRot(item.localRotationY));
    draw();
  };
  ctxMenuEl.querySelectorAll<HTMLButtonElement>("[data-rot]").forEach((btn) => {
    btn.addEventListener("click", () => {
      applyRotation(item.localRotationY + parseFloat(btn.dataset.rot!));
    });
  });
  const rotInput = document.getElementById("ctx-rot-input") as HTMLInputElement | null;
  rotInput?.addEventListener("change", () => applyRotation(parseFloat(rotInput.value)));
  wireStubControls(item);
  ctxMenuEl.querySelector('[data-act="detail"]')?.addEventListener("click", () => {
    if (currentLayer === "floor" && isSurface) {
      showSurfaceItemDetail(item, clientX, clientY);
    } else {
      showDetail(item, clientX, clientY);
    }
    hideContextMenu();
  });
  ctxMenuEl.querySelector('[data-act="delete"]')?.addEventListener("click", () => deleteSelected());
  ctxMenuEl.querySelector('[data-act="copy"]')?.addEventListener("click", () => {
    copySelection();
  });
  ctxMenuEl.querySelector('[data-act="paste"]')?.addEventListener("click", () => {
    hideContextMenu();
    pasteClipboard();
  });
}

function updateCtxCoord(item: EditorItem) {
  const el = document.getElementById("ctx-coord");
  if (el) el.textContent = `x ${item.localPosition.x.toFixed(2)} · z ${item.localPosition.z.toFixed(2)}`;
}

function hideContextMenu() {
  ctxMenuEl.classList.add("hidden");
}

function countDuplicateInstanceIds(list: LayoutItem[]): number {
  const seen = new Set<string>();
  let dup = 0;
  for (const it of list) {
    const id = it.instanceId;
    if (!id || id.startsWith("new:")) continue;
    if (seen.has(id)) dup++;
    else seen.add(id);
  }
  return dup;
}

function removeBackgroundFloors(): number {
  const before = floors.length;
  floors = floors.filter((f) => f.surfaceKind !== "background");
  if (selectedFloorKey && !floors.some((f) => f._key === selectedFloorKey)) {
    selectedFloorKey = null;
  }
  return before - floors.length;
}

function syncBackgroundForTheme(themeKey: string) {
  const wanted = themeBackgroundPrefabIds(themeKey);
  const wantedSet = new Set(wanted);

  // Drop theme-managed prefabs that don't match the selected theme.
  const removedItemKeys: string[] = [];
  items = items.filter((it) => {
    const pid = prefabIdFromPath(it.prefabAssetPath);
    if (!isThemeBackgroundPrefabId(pid)) return true;
    if (wantedSet.has(pid)) return true;
    removedItemKeys.push(it._editorKey);
    return false;
  });
  if (removedItemKeys.some((k) => selectedKeys.has(k))) {
    clearSelection();
    hideDetail();
  }

  // Environment backdrop planes (e.g. Art/raft_water/sky) are replaced by theme prefabs.
  const removedFloors = removeBackgroundFloors();

  if (wanted.length === 0) {
    if (removedFloors > 0) draw();
    return;
  }

  const pid = wanted[0];
  const exists = items.some((it) => prefabIdFromPath(it.prefabAssetPath) === pid);
  if (exists) {
    if (removedFloors > 0) draw();
    return;
  }

  const bounds = computeLevelBounds();
  const wx = bounds?.cx ?? 0;
  const wz = bounds?.cz ?? 0;
  const cat = catalogItemById(pid);
  if (!cat) return;
  addFromCatalog(cat, wx, wz);
  setStatus(`已切换背景环境：${tidyCatalogNameZh(cat.nameZh)}（${pid}）`);
  draw();
}

function matchesFloorPaletteFilter(it: CatalogItem, q: string): boolean {
  if (!q) return true;
  const kindZh = surfaceKindLabelZh(it.surfaceKind);
  return (
    it.id.toLowerCase().includes(q) ||
    it.nameZh.toLowerCase().includes(q) ||
    it.nameEn.toLowerCase().includes(q) ||
    kindZh.includes(q) ||
    (it.theme ?? "").toLowerCase().includes(q)
  );
}

function appendPaletteTileGrid(parent: HTMLElement, list: CatalogItem[]) {
  const tileGrid = document.createElement("div");
  tileGrid.className = "palette-tile-grid";
  for (const it of list) {
    const row = document.createElement("div");
    row.className = "palette-item palette-tile";
    row.draggable = true;
    row.dataset.guid = it.guid;
    const sub =
      it.surfaceTier === "background" && themeBackgroundPrefabIds("sky").includes(it.id)
        ? `<div class="sub">天空主题自动补齐</div>`
        : it.id === "raft_water"
          ? `<div class="sub">水主题自动补齐</div>`
          : it.id === "alien_gue"
            ? `<div class="sub">黏液主题自动补齐</div>`
            : "";
    row.innerHTML = `<div class="zh">${tidyCatalogNameZh(it.nameZh)}</div><div class="id">${it.id}</div>${sub}`;
    row.addEventListener("dragstart", (e) => {
      dragCatalog = it;
      e.dataTransfer?.setData("text/plain", it.guid);
    });
    row.addEventListener("dragend", () => {
      dragCatalog = null;
    });
    tileGrid.appendChild(row);
  }
  parent.appendChild(tileGrid);
}

function buildDocument(): LayoutDocument {
  return {
    sceneAssetPath: scenePath,
    items: items.map(({ _editorKey, _wx, _wz, _parentWx, _parentWz, ...rest }) => {
      const fp = resolveFootprint(rest);
      const cat = catalogByGuid.get(rest.prefabGuid);
      return {
        ...rest,
        footprint: fp,
        worldPosition: { x: _wx, y: rest.localPosition?.y ?? 0, z: _wz },
        walkable: !!(cat && cat.surfaceTier === "floor"),
      };
    }),
    floors: floors.map(({ _key, _wx, _wz, _wCells, _dCells, ...rest }) => ({
      ...rest,
      widthCells: _wCells,
      depthCells: _dCells,
      widthUnits: _wCells * CELL,
      depthUnits: _dCells * CELL,
    })),
  };
}

function buildPalette(catalog: import("./types").Catalog, filter: string) {
  paletteCats.innerHTML = "";
  const q = filter.trim().toLowerCase();

  const groups =
    catalog.paletteGroups ??
    Object.keys(catalog.byCategory)
      .sort()
      .map((key) => ({
        key,
        labelZh: key,
        labelEn: key,
        layoutTier: key.startsWith("art/") ? ("decor" as const) : ("core" as const),
        itemCount: catalog.byCategory[key].length,
      }));

  for (const group of groups) {
    const list = (catalog.byCategory[group.key] ?? []).filter((it) => {
      if (!q) return true;
      return (
        it.id.toLowerCase().includes(q) ||
        it.nameZh.toLowerCase().includes(q) ||
        it.nameEn.toLowerCase().includes(q) ||
        it.assetPath.toLowerCase().includes(q)
      );
    });
    if (list.length === 0) continue;

    const details = document.createElement("details");
    details.className = "cat-group";
    details.dataset.tier = group.layoutTier;
    details.open = group.layoutTier === "core";
    const summary = document.createElement("summary");
    summary.textContent = `${group.labelZh} (${list.length})`;
    summary.title = group.labelEn;
    details.appendChild(summary);

    const max = group.layoutTier === "decor" ? 80 : list.length;
    for (let i = 0; i < Math.min(max, list.length); i++) {
      const it = list[i];
      const row = document.createElement("div");
      row.className = "palette-item";
      if (it.layoutTier === "decor") row.classList.add("palette-decor");
      row.draggable = true;
      row.dataset.guid = it.guid;
      const sub = it.stack
        ? `<div class="sub">叠放 · 高度 ${it.stack.y}m</div>`
        : it.layoutTier === "decor"
          ? `<div class="sub">装饰</div>`
          : "";
      row.innerHTML = `<div class="zh">${tidyCatalogNameZh(it.nameZh)}</div><div class="id">${it.nameEn} · ${it.id}</div>${sub}`;
      row.addEventListener("dragstart", (e) => {
        dragCatalog = it;
        e.dataTransfer?.setData("text/plain", it.guid);
      });
      row.addEventListener("dragend", () => {
        dragCatalog = null;
      });
      details.appendChild(row);
    }
    if (list.length > max) {
      const more = document.createElement("div");
      more.className = "palette-item";
      more.style.color = "#9aa0a6";
      more.textContent = `… 还有 ${list.length - max} 项，请搜索缩小范围`;
      details.appendChild(more);
    }
    paletteCats.appendChild(details);
  }
}

function buildFloorPalette(filter = "") {
  paletteCats.innerHTML = "";
  const q = filter.trim().toLowerCase();

  const addBtn = document.createElement("button");
  addBtn.className = "palette-add-floor";
  addBtn.textContent = "+ 新增地板（在画布点击放置）";
  addBtn.addEventListener("click", () => {
    pendingNewFloor = true;
    setStatus("在画布上点击以放置新地板");
    canvas.style.cursor = "crosshair";
  });
  paletteCats.appendChild(addBtn);

  const surfaceItems: CatalogItem[] = [];
  for (const it of catalogByGuid.values()) if (isSurfaceItem(it)) surfaceItems.push(it);

  const groups: { key: string; labelZh: string; match: (it: CatalogItem) => boolean }[] = [
    { key: "raft", labelZh: "木筏拼块", match: (it) => it.surfaceKind === "raft" },
    {
      key: "themed",
      labelZh: "主题地板",
      match: (it) =>
        it.surfaceTier === "floor" && it.surfaceKind !== "raft" && it.surfaceKind !== "conveyor",
    },
    { key: "conveyor", labelZh: "传送带地面", match: (it) => it.surfaceKind === "conveyor" },
    { key: "background", labelZh: "背景 / 环境", match: (it) => it.surfaceTier === "background" },
  ];

  let anyGroup = false;
  for (const group of groups) {
    const list = surfaceItems
      .filter((it) => group.match(it))
      .filter((it) => matchesFloorPaletteFilter(it, q))
      .sort((a, b) => a.id.localeCompare(b.id));
    if (list.length === 0) continue;
    anyGroup = true;

    const details = document.createElement("details");
    details.className = "cat-group";
    details.open = true;
    const summary = document.createElement("summary");
    summary.textContent = `${group.labelZh} (${list.length})`;
    details.appendChild(summary);
    appendPaletteTileGrid(details, list);
    paletteCats.appendChild(details);
  }

  if (!anyGroup && q) {
    const empty = document.createElement("div");
    empty.className = "palette-empty";
    empty.textContent = "无匹配的地板 / 木筏 / 背景项";
    paletteCats.appendChild(empty);
  }
}

let pendingNewFloor = false;

function updateFloorBar() {
  if (currentLayer !== "floor") {
    floorBar.classList.add("hidden");
    return;
  }
  floorBar.classList.remove("hidden");
  const themeBtns = BG_THEMES.map(
    (t) =>
      `<button type="button" class="fb-theme-btn${t.key === bgThemeKey ? " active" : ""}" data-bg="${t.key}" title="${bgThemeTooltip(t)}">${t.emoji} ${t.labelZh}</button>`
  ).join("");
  const themeRow = `<span class="fb-theme">背景主题：${themeBtns}</span>`;
  const killToggle = `<label class="fb-check" title="回写 Unity 时把坠落区(KillPlane)扩大到覆盖整关，使所有非地板区域都会坠落"><input type="checkbox" id="fb-autokill" ${autoKillPlane ? "checked" : ""}/> 回写扩大坠落区</label>`;
  const walkToggle = `<label class="fb-check" title="回写时按可见地板重新生成可行走碰撞体(Col_Floor)：可行走=地板，地板间空隙=坠落坑"><input type="checkbox" id="fb-autowalk" ${autoWalkable ? "checked" : ""}/> 同步可行走到地板</label>`;
  const f = floors.find((x) => x._key === selectedFloorKey);
  const selItem = selectedKey ? items.find((i) => i._editorKey === selectedKey) : null;
  const selCat = selItem ? catalogByGuid.get(selItem.prefabGuid) : undefined;
  let info: string;
  if (f) {
    info = `<span class="fb-info"><b>${surfaceKindLabelZh(f.surfaceKind)}</b> · ${f._wCells}×${f._dCells}格 · ${f.materialName ?? "无材质"}</span>`;
  } else if (selItem && isSurfaceItem(selCat)) {
    info = `<span class="fb-info"><b>${surfaceKindLabelZh(selCat?.surfaceKind)}</b> · ${itemLabel(selItem)}</span>`;
  } else {
    info = `<span class="fb-info">${deathLabelZh(deathInfo)} · 共 ${floors.length} 块地板</span>`;
  }
  floorBar.innerHTML = `${themeRow}${killToggle}${walkToggle}${info}<span class="fb-hint">背景为坠落死亡区 · 拖动移动 · 拖角点缩放 · 右键详情</span>`;

  floorBar.querySelectorAll<HTMLButtonElement>(".fb-theme-btn").forEach((btn) => {
    btn.addEventListener("click", () => {
      bgThemeKey = btn.dataset.bg ?? "void";
      bgThemeDirty = true;
      localStorage.setItem("bgTheme:" + scenePath, bgThemeKey);
      syncBackgroundForTheme(bgThemeKey);
      updateFloorBar();
      setStatus(`背景主题：${bgTheme(bgThemeKey).labelZh}（写回 Unity 后生效）`);
    });
  });
  document.getElementById("fb-autokill")?.addEventListener("change", (e) => {
    autoKillPlane = (e.target as HTMLInputElement).checked;
  });
  document.getElementById("fb-autowalk")?.addEventListener("change", (e) => {
    autoWalkable = (e.target as HTMLInputElement).checked;
  });
}

function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  return tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT" || target.isContentEditable;
}

async function init() {
  const ok = await fetchHealth();
  const healthInfo = await fetchHealthInfo().catch(() => ({ ok: false, recipeApi: false }));
  setStatus(
    ok
      ? healthInfo.recipeApi
        ? "已连接 Unity（含菜谱 API）"
        : "已连接 Unity（请重启 Bridge 以使用菜谱）"
      : "未连接 Unity（请先启动 Bridge）",
    ok
  );

  const catalog = await loadCatalog();
  for (const it of catalog.items) catalogByGuid.set(it.guid, it);
  ingredientsCache = await fetchIngredients().catch(() => []);
  buildPalette(catalog, "");

  const scenes = await fetchLevelSets().catch(() => []);
  sceneSelect.innerHTML = '<option value="">— 选择场景 —</option>';
  for (const s of scenes) {
    const opt = document.createElement("option");
    opt.value = s.assetPath;
    opt.textContent = `${s.levelSet} / ${s.sceneName}`;
    sceneSelect.appendChild(opt);
  }

  document.getElementById("palette-search")!.addEventListener("input", (e) => {
    const q = (e.target as HTMLInputElement).value;
    if (currentLayer === "floor") buildFloorPalette(q);
    else buildPalette(catalog, q);
  });

  sceneSelect.addEventListener("change", () => {
    if (sceneSelect.value) void loadScene(sceneSelect.value);
  });

  document.getElementById("btn-reload")!.addEventListener("click", () => {
    if (scenePath) void loadScene(scenePath);
  });

  document.getElementById("btn-save")!.addEventListener("click", () => void saveToUnity());

  document.getElementById("btn-recipes")!.addEventListener("click", () => void openRecipesDialog());

  document.getElementById("btn-manage")!.addEventListener("click", () => goManage());

  document.getElementById("snap-half")!.addEventListener("change", (e) => {
    snapStep = (e.target as HTMLInputElement).checked ? 0.6 : CELL;
  });

  document.getElementById("show-grid")!.addEventListener("change", (e) => {
    showGrid = (e.target as HTMLInputElement).checked;
    draw();
  });

  document.querySelectorAll<HTMLButtonElement>(".layer-tab").forEach((btn) => {
    btn.addEventListener("click", () => {
      const layer = btn.dataset.layer as "items" | "floor";
      if (layer === currentLayer) return;
      currentLayer = layer;
      document.querySelectorAll(".layer-tab").forEach((b) => b.classList.toggle("active", b === btn));
      clearSelection();
      marqueeing = false;
      selectedFloorKey = null;
      hideDetail();
      hideContextMenu();
      pendingNewFloor = false;
      canvas.style.cursor = "";
      const searchEl = document.getElementById("palette-search") as HTMLInputElement;
      if (layer === "floor") {
        searchEl.placeholder = "搜索木筏 / 地板 / 背景…";
        buildFloorPalette(searchEl.value);
      } else {
        searchEl.placeholder = "搜索 prefab…";
        buildPalette(catalog, searchEl.value);
      }
      draw();
    });
  });

  setupCanvas();
  requestAnimationFrame(draw);

  if (scenes.length > 0) {
    const target = consumeTargetScene();
    const match = target ? scenes.find((s) => s.assetPath === target) : null;
    const guojia = scenes.find((s) => s.assetPath.includes("guojia"));
    const pick = match ?? guojia ?? scenes[0];
    sceneSelect.value = pick.assetPath;
    await loadScene(pick.assetPath);
  }

  startBridgeWatch();
}

let bridgeWasUp = false;
let bridgeStopAlerted = false;

function startBridgeWatch() {
  bridgeWasUp = true;
  bridgeStopAlerted = false;
  window.setInterval(async () => {
    const up = await fetchHealth();
    if (!up && bridgeWasUp && !bridgeStopAlerted) {
      bridgeStopAlerted = true;
      showBridgeStoppedModal();
      setStatus("未连接 Unity（后台服务已停止）", false);
    } else if (up) {
      bridgeStopAlerted = false;
    }
    bridgeWasUp = up;
  }, 3000);
}

function showBridgeStoppedModal() {
  openModal(
    "后台服务已停止",
    `<p>Layout Editor 的后台 Bridge 服务已断开。</p>
     <p>最常见的原因是 <b>Unity 进入了 Play 模式</b>（Play 时编辑器服务会暂停），也可能是服务被手动停止。</p>
     <p>请退出 Play 模式后，在 Unity <b>Tools → Layout Editor → Start Server</b> 重新启动，然后刷新本页。</p>`,
    `<button type="button" class="modal-btn primary" data-ok>知道了</button>`
  );
  document.querySelector("[data-ok]")?.addEventListener("click", closeModal);
}

async function loadScene(assetPath: string) {
  showBusy("加载场景…");
  try {
    setStatus("加载场景…");
    scenePath = assetPath;
    currentLevelSet = levelSetFromScenePath(assetPath);
    const doc = await fetchLayout(assetPath);
    const dupIds = countDuplicateInstanceIds(doc.items);
    items = doc.items.map((raw, index) => enrichItem(raw, `i${index}`));
    floors = (doc.floors ?? []).map((raw, index) => enrichFloor(raw, `f${index}`));
    walkable = doc.walkable ?? [];
    deathInfo = doc.deathInfo ?? null;
    const itemTheme = inferBgThemeFromItems(items);
    const deathThemeKey = bgThemeKeyForDeathType(deathInfo?.deathType);
    const sceneThemeKey = itemTheme ?? deathThemeKey;
    const savedTheme = localStorage.getItem("bgTheme:" + scenePath);
    const normalizedSaved =
      savedTheme === "lava" ? "void" : savedTheme;
    if (normalizedSaved && BG_THEMES.some((t) => t.key === normalizedSaved)) {
      bgThemeKey = normalizedSaved;
    } else {
      bgThemeKey = sceneThemeKey;
    }
    if (bgThemeKey === "lava") bgThemeKey = "void";
    bgThemeDirty = bgThemeKey !== sceneThemeKey;
    refreshUtensilStacks();
    gridInfo = await fetchGrid();
    floorMaterials = await fetchFloorMaterials(currentLevelSet).catch(() => []);
    if (currentLayer === "floor") {
      buildFloorPalette((document.getElementById("palette-search") as HTMLInputElement)?.value ?? "");
    }
    clearSelection();
    marqueeing = false;
    selectedFloorKey = null;
    hideDetail();
    draw();
    const floorNote = floors.length > 0 ? `、${floors.length} 块地板` : "";
    if (dupIds > 0) {
      setStatus(
        `已加载 ${items.length} 个物体（有 ${dupIds} 个重复 ID，请重新编译 Unity 后点「重新加载」）`,
        false
      );
    } else {
      setStatus(`已加载 ${items.length} 个物体${floorNote}`);
    }
  } catch (e) {
    setStatus((e as Error).message, false);
  } finally {
    hideBusy();
  }
}

async function saveToUnity() {
  showBusy("写回 Unity…");
  try {
    setStatus("写回中…");
    const itemTheme = inferBgThemeFromItems(items);
    const deathThemeKey = bgThemeKeyForDeathType(deathInfo?.deathType);
    const sceneThemeKey = itemTheme ?? deathThemeKey;
    const expectedDeathType = bgTheme(bgThemeKey).deathType;
    const needsDeathWrite = deathInfo?.deathType !== expectedDeathType;
    const needsThemeWrite = bgThemeDirty || bgThemeKey !== sceneThemeKey || needsDeathWrite;

    const itemsBeforeSync = items.length;
    syncBackgroundForTheme(bgThemeKey);
    const addedBg = items.length > itemsBeforeSync;

    await saveLayout(buildDocument(), snapStep, autoWalkable);
    if (needsThemeWrite) {
      await setDeathTheme(scenePath, bgThemeKey);
    }
    const bounds = computeLevelBounds();
    if (bounds && autoKillPlane) {
      try {
        await setKillPlaneBounds(scenePath, bounds.cx, bounds.cz, bounds.sx, bounds.sz);
      } catch (kpErr) {
        setStatus(`坠落区配置失败：${(kpErr as Error).message}`, false);
      }
    }
    const themeNote = needsThemeWrite
      ? `，背景死亡效果已应用（${bgTheme(bgThemeKey).labelZh}）`
      : addedBg
        ? `，已补齐背景环境 prefab（${bgTheme(bgThemeKey).labelZh}）`
        : "";
    const walkNote = autoWalkable ? "，可行走碰撞体已按地板重新生成（地板间空隙=坠落坑）" : "";
    const killNote = bounds && autoKillPlane ? "：坠落区已覆盖整关，" : "：";
    setStatus(
      `写回成功${themeNote}${walkNote}${killNote}请在 Unity Ctrl+S 保存场景`
    );
    bgThemeDirty = false;
    await loadScene(scenePath);
  } catch (e) {
    setStatus((e as Error).message, false);
  } finally {
    hideBusy();
  }
}

function escHtml(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

const STEP_UTENSILS: Record<string, string[]> = {
  Pot: ["Cooker", "Pot"],
  FryingPan: ["Cooker", "FryPan"],
  DeepFatFryer: ["FryingStation", "FrierBasket"],
  OvenTray: ["Oven"],
  Steamer: ["Cooker", "Steamer"],
  Mixer: ["Mixer", "MixerBowl"],
};

const CHOPPABLE_INGREDIENTS = new Set([
  "LettuceSO",
  "TomatoSO",
  "OnionSO",
  "CarrotSO",
  "CucumberSO",
  "MushroomSO",
]);

const BASE_UTENSILS = ["ServingStation", "Bin", "CleanPlateStack"];

function catalogItemById(id: string): CatalogItem | undefined {
  for (const it of catalogByGuid.values()) if (it.id === id) return it;
  return undefined;
}

function ingredientEntryById(id: string) {
  return ingredientsCache.find((i) => i.id === id);
}

function ingredientGuidById(id: string): string | undefined {
  return ingredientEntryById(id)?.guid;
}

function ingredientIdByGuid(guid: string | undefined): string | undefined {
  if (!guid) return undefined;
  return ingredientsCache.find((i) => i.guid === guid)?.id;
}

function ingredientNameById(id: string): string {
  return ingredientEntryById(id)?.nameZh ?? id;
}

function computeRequiredUtensils(ingredientIds: Set<string>, steps: Set<string>): string[] {
  const set = new Set<string>(BASE_UTENSILS);
  for (const s of steps) (STEP_UTENSILS[s] ?? []).forEach((u) => set.add(u));
  for (const ing of ingredientIds) {
    if (CHOPPABLE_INGREDIENTS.has(ing)) set.add("ChoppingCounter");
    if (ing === "FlourSO") {
      set.add("Mixer");
      set.add("MixerBowl");
    }
  }
  return [...set];
}

function placementBase(): { x: number; z: number } {
  if (items.length === 0) return { x: 0, z: 0 };
  let minZ = Infinity;
  let minX = Infinity;
  for (const it of items) {
    minZ = Math.min(minZ, it._wz);
    minX = Math.min(minX, it._wx);
  }
  return { x: minX, z: minZ - 2 * CELL };
}

function addFromCatalog(cat: CatalogItem, wx: number, wz: number): EditorItem {
  const id = `new:${cat.guid}:${crypto.randomUUID()}`;
  const editorKey = newEditorKey();
  const snapped = snapFootprintCenter(wx, wz, cat.footprint.cellsX, cat.footprint.cellsZ, 0, CELL, snapStep);
  const item: EditorItem = {
    instanceId: id,
    _editorKey: editorKey,
    hierarchyPath: id,
    prefabGuid: cat.guid,
    prefabAssetPath: cat.assetPath,
    parentPath: cat.defaultParent,
    displayName: cat.id,
    localPosition: { x: snapped.x, y: 0, z: snapped.z },
    worldPosition: { x: snapped.x, y: 0, z: snapped.z },
    localRotationY: 0,
    footprint: cat.footprint,
    _wx: snapped.x,
    _wz: snapped.z,
    _parentWx: 0,
    _parentWz: 0,
  };
  if (cat.id === "Dispenser") {
    item.stubKind = "Dispenser";
    item.dispenser = {};
  }
  if (cat.id === "AttachingFoodSpawner") {
    item.stubKind = "AttachingFoodSpawner";
    item.foodSpawner = {
      spawnInOrder: true,
      triggerAtStart: true,
      triggerTime: 5,
      attachmentPrefabGuids: [],
      weights: [],
    };
  }
  if (cat.id === "ConveyorStation") {
    item.stubKind = "Conveyor";
    item.conveyor = { conveySpeed: 0.5 };
  }
  if (cat.id === "Teleportal") {
    item.stubKind = "Teleportal";
    item.teleportal = { exitPortalInstanceId: "", portalColor: 0, doubleSided: false };
  }
  items.push(item);
  trySnapUtensilToHost(item, cat, items, catalogByGuid);
  setSelection([editorKey]);
  draw();
  warnItemVoid(item);
  return item;
}

function setupCanvas() {
  canvas.addEventListener("wheel", (e) => {
    e.preventDefault();
    const factor = e.deltaY > 0 ? 0.9 : 1.1;
    scale = Math.min(4, Math.max(0.25, scale * factor));
    draw();
  });

  canvas.addEventListener("mousedown", (e) => {
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;
    const { x: wx, z: wz } = canvasToWorld(mx, my);

    if (e.button === 1 || (e.button === 0 && e.altKey) || (e.button === 0 && spaceHeld)) {
      panning = true;
      lastMx = mx;
      lastMy = my;
      hideDetail();
      updateCanvasCursor();
      return;
    }

    if (e.button === 2) return;
    if (e.button !== 0) return;

    if (currentLayer === "floor") {
      if (pendingNewFloor) {
        pendingNewFloor = false;
        canvas.style.cursor = "";
        addFloorAt(wx, wz);
        return;
      }
      const fHit = hitTestFloor(wx, wz);
      if (fHit) {
        selectedFloorKey = fHit.floor._key;
        clearSelection();
        dragFloorKey = fHit.floor._key;
        dragFloorMode = fHit.mode;
        dragFloorEdge = fHit.edge;
        dragFloorAnchorX = fHit.anchorX;
        dragFloorAnchorZ = fHit.anchorZ;
      } else {
        selectedFloorKey = null;
        const itemHit = hitTest(wx, wz);
        if (itemHit && isSurfaceItem(catalogByGuid.get(itemHit.prefabGuid))) {
          setSelection([itemHit._editorKey]);
          dragItemKey = itemHit._editorKey;
          dragGroupKeys = [];
          dragOffsetX = wx - itemHit._wx;
          dragOffsetZ = wz - itemHit._wz;
        } else {
          clearSelection();
          hideDetail();
        }
      }
      draw();
      return;
    }

    const hit = hitTest(wx, wz);
    if (hit) {
      hideDetail();
      hideContextMenu();
      if (e.shiftKey) {
        if (isSelected(hit._editorKey)) selectedKeys.delete(hit._editorKey);
        else selectedKeys.add(hit._editorKey);
        selectedKey = hit._editorKey;
        dragItemKey = isSelected(hit._editorKey) ? hit._editorKey : null;
        dragGroupKeys = selectedKeys.size > 1 && dragItemKey ? selectionKeys() : [];
        if (dragItemKey) {
          dragOffsetX = wx - hit._wx;
          dragOffsetZ = wz - hit._wz;
          dragLastWx = hit._wx;
          dragLastWz = hit._wz;
        }
      } else if (selectedKeys.size > 1 && isSelected(hit._editorKey)) {
        selectedKey = hit._editorKey;
        dragItemKey = hit._editorKey;
        dragGroupKeys = selectionKeys();
        dragOffsetX = wx - hit._wx;
        dragOffsetZ = wz - hit._wz;
        dragLastWx = hit._wx;
        dragLastWz = hit._wz;
      } else {
        setSelection([hit._editorKey]);
        dragItemKey = hit._editorKey;
        dragGroupKeys = [];
        dragOffsetX = wx - hit._wx;
        dragOffsetZ = wz - hit._wz;
      }
    } else {
      marqueeing = true;
      marqueeAdd = e.shiftKey;
      marqueeStartX = mx;
      marqueeStartY = my;
      marqueeCurX = mx;
      marqueeCurY = my;
      if (!marqueeAdd) {
        clearSelection();
        hideDetail();
        hideContextMenu();
      }
    }
    draw();
  });

  canvas.addEventListener("contextmenu", (e) => {
    e.preventDefault();
    const rect = canvas.getBoundingClientRect();
    const { x: wx, z: wz } = canvasToWorld(e.clientX - rect.left, e.clientY - rect.top);
    if (currentLayer === "floor") {
      const fHit = hitTestFloor(wx, wz);
      if (fHit) {
        selectedFloorKey = fHit.floor._key;
        showFloorDetail(fHit.floor, e.clientX, e.clientY);
        draw();
        return;
      }
      const itemHit = hitTest(wx, wz);
      if (itemHit && isSurfaceItem(catalogByGuid.get(itemHit.prefabGuid))) {
        setSelection([itemHit._editorKey]);
        hideDetail();
        showContextMenu(itemHit, e.clientX, e.clientY);
        draw();
        return;
      }
      hideDetail();
      hideContextMenu();
      return;
    }
    const hit = hitTest(wx, wz);
    if (hit) {
      if (!(selectedKeys.size > 1 && isSelected(hit._editorKey))) {
        setSelection([hit._editorKey]);
      } else {
        selectedKey = hit._editorKey;
      }
      hideDetail();
      showContextMenu(hit, e.clientX, e.clientY);
      draw();
    } else {
      hideDetail();
      hideContextMenu();
    }
  });

  window.addEventListener("mousemove", (e) => {
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;

    if (marqueeing) {
      marqueeCurX = mx;
      marqueeCurY = my;
      updateMarqueeSelection();
      draw();
      return;
    }

    if (panning) {
      panX += mx - lastMx;
      panY += my - lastMy;
      lastMx = mx;
      lastMy = my;
      draw();
      return;
    }

    if (dragFloorKey) {
      const f = floors.find((x) => x._key === dragFloorKey);
      if (f) dragFloor(f, mx, my);
      draw();
      return;
    }

    if (!dragItemKey) return;
    const item = items.find((i) => i._editorKey === dragItemKey);
    if (!item) return;
    const { x: wx, z: wz } = canvasToWorld(mx, my);
    if (dragGroupKeys.length > 1) {
      const newWx = wx - dragOffsetX;
      const newWz = wz - dragOffsetZ;
      const dx = newWx - dragLastWx;
      const dz = newWz - dragLastWz;
      for (const k of dragGroupKeys) {
        const it = items.find((i) => i._editorKey === k);
        if (it) {
          it._wx += dx;
          it._wz += dz;
        }
      }
      dragLastWx = newWx;
      dragLastWz = newWz;
    } else {
      item._wx = wx - dragOffsetX;
      item._wz = wz - dragOffsetZ;
      syncLocalFromWorld(item);
    }
    draw();
  });

  window.addEventListener("mouseup", () => {
    if (panning) {
      panning = false;
      updateCanvasCursor();
    }
    if (dragFloorKey) {
      const f = floors.find((x) => x._key === dragFloorKey);
      if (f) finalizeFloor(f);
    }
    dragFloorKey = null;
    if (marqueeing) {
      marqueeing = false;
      const keys = selectionKeys();
      selectedKey = keys.length ? keys[keys.length - 1] : null;
      draw();
    }
    if (dragGroupKeys.length > 1) {
      for (const k of dragGroupKeys) {
        const it = items.find((i) => i._editorKey === k);
        if (it) {
          syncLocalFromWorld(it);
          warnItemVoid(it);
        }
      }
      dragGroupKeys = [];
    } else if (dragItemKey) {
      const item = items.find((i) => i._editorKey === dragItemKey);
      if (item) {
        syncLocalFromWorld(item);
        warnItemVoid(item);
      }
    }
    dragItemKey = null;
  });

  canvas.addEventListener("dragover", (e) => {
    e.preventDefault();
  });

  canvas.addEventListener("drop", (e) => {
    e.preventDefault();
    const guid = e.dataTransfer?.getData("text/plain");
    const cat = guid ? catalogByGuid.get(guid) : dragCatalog;
    if (!cat) return;
    const rect = canvas.getBoundingClientRect();
    const { x: wx, z: wz } = canvasToWorld(e.clientX - rect.left, e.clientY - rect.top);
    addFromCatalog(cat, wx, wz);
  });

  window.addEventListener("keydown", (e) => {
    if (e.code === "Space" && !isTypingTarget(e.target)) {
      e.preventDefault();
      if (!spaceHeld) {
        spaceHeld = true;
        updateCanvasCursor();
      }
    }

    if (e.key === "Escape") {
      hideDetail();
      hideContextMenu();
      closeModal();
      marqueeing = false;
      pendingNewFloor = false;
      canvas.style.cursor = "";
    }

    if ((e.ctrlKey || e.metaKey) && (e.key === "c" || e.key === "C") && !isTypingTarget(e.target)) {
      copySelection();
      e.preventDefault();
      return;
    }
    if ((e.ctrlKey || e.metaKey) && (e.key === "v" || e.key === "V") && !isTypingTarget(e.target)) {
      pasteClipboard();
      e.preventDefault();
      return;
    }

    if (currentLayer === "floor") {
      if (isTypingTarget(e.target)) return;
      if (selectedFloorKey) {
        const f = floors.find((x) => x._key === selectedFloorKey);
        if (f) {
          if (e.key === "Delete" || e.key === "Backspace") {
            floors = floors.filter((x) => x._key !== selectedFloorKey);
            selectedFloorKey = null;
            hideDetail();
            draw();
          }
          if (e.key === "r" || e.key === "R") {
            f.localRotationY = normalizeRot(f.localRotationY + (e.shiftKey ? -90 : 90));
            draw();
          }
        }
        return;
      }
      if (selectedKey) {
        const item = items.find((i) => i._editorKey === selectedKey);
        if (item && isSurfaceItem(catalogByGuid.get(item.prefabGuid))) {
          if (e.key === "Delete" || e.key === "Backspace") {
            items = items.filter((i) => i._editorKey !== selectedKey);
            selectedKey = null;
            hideDetail();
            draw();
          }
          if (e.key === "r" || e.key === "R") {
            item.localRotationY = normalizeRot(item.localRotationY + (e.shiftKey ? -90 : 90));
            syncLocalFromWorld(item);
            draw();
          }
        }
      }
      return;
    }

    if (!selectedKey) return;
    const item = items.find((i) => i._editorKey === selectedKey);
    if (!item) return;

    if (isTypingTarget(e.target)) return;

    if (e.key === "Delete" || e.key === "Backspace") {
      deleteSelected();
    }
    if (e.key === "r" || e.key === "R") {
      item.localRotationY = normalizeRot(item.localRotationY + (e.shiftKey ? -90 : 90));
      syncLocalFromWorld(item);
      draw();
    }
  });

  window.addEventListener("keyup", (e) => {
    if (e.code === "Space") {
      spaceHeld = false;
      panning = false;
      updateCanvasCursor();
    }
  });

  window.addEventListener("blur", () => {
    spaceHeld = false;
    panning = false;
    updateCanvasCursor();
  });

  document.addEventListener("mousedown", (e) => {
    if (!detailEl.classList.contains("hidden") && !detailEl.contains(e.target as Node)) {
      hideDetail();
    }
    if (!ctxMenuEl.classList.contains("hidden") && !ctxMenuEl.contains(e.target as Node)) {
      hideContextMenu();
    }
  });

  window.addEventListener("resize", () => draw());
}

if (MANAGE_ACTIVE) {
  void renderManageView(app);
} else {
  void init();
}
