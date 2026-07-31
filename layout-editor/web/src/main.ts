import "./style.css";
import {
  fetchFloorMaterials,
  fetchGrid,
  fetchHealth,
  fetchHealthInfo,
  fetchIngredients,
  STALE_BRIDGE_MSG,
  fetchLayout,
  fetchLevelDetail,
  fetchLevelRecipes,
  fetchLevelSets,
  fetchRecipeCatalog,
  loadCatalog,
  saveLayout,
  saveLevelRecipes,
  setDeathTheme,
  setKillPlaneBounds,
} from "./api";
import { renderManageView, goManage, consumeTargetScene, openConfigTabsModal, openAudioModal } from "./levels";
import { showBusy, hideBusy } from "./busy";
import { navHtml, wireNav } from "./nav";
import { HistoryStack } from "./history";
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
import { raftPiecesForRect } from "./raft";
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
  LevelDetail,
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
let selectedFloorKeys = new Set<string>();
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
let dragFloorGroupKeys: string[] = [];
let dragFloorMode: "move" | "resize" = "move";
let dragFloorEdge: string = "";
let dragFloorAnchorX = 0;
let dragFloorAnchorZ = 0;

let ingredientsCache: import("./types").IngredientEntry[] = [];
let currentLevelSet = "";
let sceneListCache: import("./types").LevelSetScene[] = [];

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
  Player: "Player",
  ServingStation: "ServingStation",
  PlateReturn: "PlateReturn",
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
        <button type="button" class="ctx-btn" id="ctx-fs-ings">食材列表…</button></div>`;
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
    case "Player": {
      return `<div class="ctx-stub"><div class="ctx-stub-title">玩家</div>
        <div class="ctx-stub-row">玩家固定在场景中，仅可拖动调整位置</div></div>`;
    }
    case "ServingStation": {
      const ss = item.servingStation ?? {};
      plateReturnLabels = computePlateReturnLabels();
      const prs = plateReturns();
      const opts = ['<option value="">— 未绑定 —</option>']
        .concat(
          prs.map(
            (t) =>
              `<option value="${t.instanceId}" ${ss.plateReturnInstanceId === t.instanceId ? "selected" : ""}>脏盘台 ${plateReturnLabels.get(t.instanceId) ?? "?"}（${escHtml(itemLabel(t))}）</option>`
          )
        )
        .join("");
      return `<div class="ctx-stub"><div class="ctx-stub-title">上菜口参数</div>
        <label class="ctx-stub-row">脏盘台 <select id="ctx-ss-pr" class="ctx-input">${opts}</select></label></div>`;
    }
    case "PlateReturn": {
      plateReturnLabels = computePlateReturnLabels();
      const bound = servingStations().filter((s) => s.servingStation?.plateReturnInstanceId === item.instanceId);
      const rows = servingStations()
        .map((s) => {
          const checked = s.servingStation?.plateReturnInstanceId === item.instanceId ? "checked" : "";
          return `<label class="ctx-stub-row"><input type="checkbox" class="ctx-pr-sv" value="${s.instanceId}" ${checked}/> ${escHtml(itemLabel(s))}</label>`;
        })
        .join("");
      return `<div class="ctx-stub"><div class="ctx-stub-title">脏盘台 · 绑定的上菜口 (${bound.length})</div>
        ${rows || '<div class="ctx-stub-row">场景中无上菜口</div>'}</div>`;
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
        pushHistory();
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
        pushHistory();
        ensure().spawnInOrder = (e.target as HTMLInputElement).checked;
      });
      num("ctx-fs-start")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().triggerAtStart = (e.target as HTMLInputElement).checked;
      });
      num("ctx-fs-time")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (isFinite(v) && v >= 0) {
          pushHistory();
          ensure().triggerTime = v;
        }
      });
      document.getElementById("ctx-fs-ings")?.addEventListener("click", () => {
        ensure();
        hideContextMenu();
        openFoodSpawnerEditor(item, ingredientsCache, (patch) => {
          pushHistory();
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
          pushHistory();
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
            pushHistory();
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
        pushHistory();
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
        pushHistory();
        ensure().portalColor = parseInt((e.target as HTMLSelectElement).value, 10) || 0;
        draw();
      });
      num("ctx-tp-ds")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().doubleSided = (e.target as HTMLInputElement).checked;
        draw();
      });
      num("ctx-tp-exit")?.addEventListener("change", (e) => {
        pushHistory();
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
        pushHistory();
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
        pushHistory();
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
        pushHistory();
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
        pushHistory();
        ensure().fireMode = parseInt((e.target as HTMLSelectElement).value, 10) || 0;
      });
      num("ctx-bn-air")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (isFinite(v) && v >= 0) {
          pushHistory();
          ensure().airTime = v;
        }
      });
      num("ctx-bn-rand")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().randomTargetOrder = (e.target as HTMLInputElement).checked;
      });
      num("ctx-bn-hide")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().hideVisual = (e.target as HTMLInputElement).checked;
      });
      break;
    }
    case "Player": {
      break;
    }
    case "ServingStation": {
      num("ctx-ss-pr")?.addEventListener("change", (e) => {
        pushHistory();
        item.stubKind = "ServingStation";
        item.servingStation = { plateReturnInstanceId: (e.target as HTMLSelectElement).value };
        draw();
        setStatus("已更新上菜口的脏盘台绑定（写回后生效）");
      });
      break;
    }
    case "PlateReturn": {
      document.querySelectorAll<HTMLInputElement>(".ctx-pr-sv").forEach((cb) =>
        cb.addEventListener("change", () => {
          const sv = items.find((i) => i.instanceId === cb.value);
          if (!sv) return;
          pushHistory();
          sv.stubKind = "ServingStation";
          sv.servingStation = { plateReturnInstanceId: cb.checked ? item.instanceId : "" };
          hideContextMenu();
          draw();
          setStatus(
            cb.checked
              ? `已把「${itemLabel(sv)}」绑定到该脏盘台（写回后生效）`
              : `已解绑「${itemLabel(sv)}」（写回后生效）`
          );
        })
      );
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
        const label = cat ? tidyCatalogNameZh(cat.nameZh, cat.id) : u;
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
      if (it) {
        it.dispenser = { spawnerItemPrefabGuid: ingredientGuidById(ing) ?? "" };
        idx++;
      }
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
if (!MANAGE_ACTIVE) document.body.classList.remove("manage-bg");

if (!MANAGE_ACTIVE) {
app.innerHTML = `
  ${navHtml("layout")}
  <div class="toolbar">
    <label>场景</label>
    <select id="scene-select"><option value="">加载中…</option></select>
    <button id="btn-reload">重新加载</button>
    <button id="btn-save" class="primary">写回 Unity</button>
    <button id="btn-recipes" type="button">菜谱</button>
    <button id="btn-level-config" type="button">人数配置</button>
    <button id="btn-level-audio" type="button">音频</button>
    <button id="btn-sync" type="button" title="从其他关卡复制道具、地板与背景主题（仅前端数据，写回后生效）">同步布局…</button>
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
      <div id="pick-tip" class="pick-tip hidden" role="dialog"></div>
      <div id="floor-bar" class="floor-bar hidden"></div>
      <div class="hint">拖拽空白框选 · Shift 加选 · Ctrl+C/V/X 复制/粘贴/裁切 · Ctrl+Z 撤回 · Ctrl+Shift+Z 重做 · 重叠点击弹出选择 · 空格+拖动平移 · 右键微移/旋转/改参数 · Del 删除 · R/Shift+R 旋转90° · 滚轮缩放</div>
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
const pickTipEl = document.getElementById("pick-tip")!;
const floorBar = document.getElementById("floor-bar")!;

interface EditorSnapshot {
  items: EditorItem[];
  floors: EditorFloor[];
  bgThemeKey: string;
}

const history = new HistoryStack<EditorSnapshot>(20);
let dirty = false;
let dragSnapshot: EditorSnapshot | null = null;

function snapshotState(): EditorSnapshot {
  return JSON.parse(JSON.stringify({ items, floors, bgThemeKey })) as EditorSnapshot;
}

function updateSaveIndicator(): void {
  const btn = document.getElementById("btn-save");
  if (btn) btn.textContent = dirty ? "写回 Unity *" : "写回 Unity";
}

function markDirty(): void {
  dirty = true;
  updateSaveIndicator();
}

function clearDirty(): void {
  dirty = false;
  updateSaveIndicator();
}

function pushHistory(): void {
  history.push(snapshotState());
  markDirty();
}

function applySnapshot(snap: EditorSnapshot): void {
  items = snap.items;
  floors = snap.floors;
  bgThemeKey = snap.bgThemeKey;
  clearSelection();
  clearFloorSelection();
  hideDetail();
  hideContextMenu();
  hidePickTip();
  markDirty();
  draw();
}

function undo(): void {
  const snap = history.undo(snapshotState());
  if (!snap) {
    setStatus("没有可撤回的操作", false);
    return;
  }
  applySnapshot(snap);
  setStatus(`已撤回（还可撤回 ${history.size} 步，写回 Unity 后生效）`);
}

function redo(): void {
  const snap = history.redo(snapshotState());
  if (!snap) {
    setStatus("没有可重做的操作", false);
    return;
  }
  applySnapshot(snap);
  setStatus(`已重做（还可重做 ${history.redoSize} 步，写回 Unity 后生效）`);
}

function commitDragSnapshot(): void {
  if (!dragSnapshot) return;
  const snap = dragSnapshot;
  dragSnapshot = null;
  if (JSON.stringify(snapshotState()) !== JSON.stringify(snap)) {
    history.push(snap);
    markDirty();
  }
}

function confirmLeaveIfDirty(action: () => void): void {
  if (!dirty) {
    action();
    return;
  }
  openModal(
    "有未保存的修改",
    `<p>当前关卡的布局修改尚未写回 Unity，离开后修改将丢失。</p>`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn danger" data-leave>直接离开</button>
     <button type="button" class="modal-btn primary" data-save>写回并离开</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", () => {
    closeModal();
    sceneSelect.value = scenePath;
  });
  document.querySelector("[data-leave]")?.addEventListener("click", () => {
    closeModal();
    action();
  });
  document.querySelector("[data-save]")?.addEventListener("click", () => {
    closeModal();
    void saveToUnity().then((ok) => {
      if (ok) action();
      else sceneSelect.value = scenePath;
    });
  });
}

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

  if (item.stubKind === "Player" || id === "Player") {
    const pid = item.player?.playerID ?? 11;
    if (pid !== 11) return `玩家${pid + 1}`;
  }

  const cat = catalogByGuid.get(item.prefabGuid);
  if (cat?.nameZh) return tidyCatalogNameZh(cat.nameZh, cat.id);
  if (item.displayName) return tidyCatalogNameZh(item.displayName, item.displayName);
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
    plateReturnLabels = computePlateReturnLabels();
    for (const item of sorted) {
      drawItem(item, isSelected(item._editorKey));
    }
    drawTeleportalLinks();
    drawServingLinks();
  }

  if (marqueeing) drawMarquee();

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
  const inRect = (wx: number, wz: number) => {
    const p = worldToCanvas(wx, wz);
    return p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY;
  };
  if (currentLayer === "floor") {
    const nextFloors = marqueeAdd ? new Set(selectedFloorKeys) : new Set<string>();
    for (const f of floors) {
      if (inRect(f._wx, f._wz)) nextFloors.add(f._key);
    }
    setFloorSelection([...nextFloors]);
    const next = marqueeAdd ? new Set(selectedKeys) : new Set<string>();
    for (const it of items) {
      if (!isSurfaceItem(catalogByGuid.get(it.prefabGuid))) continue;
      if (inRect(it._wx, it._wz)) next.add(it._editorKey);
    }
    selectedKeys = next;
    const keys = selectionKeys();
    selectedKey = keys.length ? keys[keys.length - 1] : null;
    return;
  }
  const next = marqueeAdd ? new Set(selectedKeys) : new Set<string>();
  for (const it of items) {
    if (isSurfaceItem(catalogByGuid.get(it.prefabGuid))) continue;
    if (inRect(it._wx, it._wz)) next.add(it._editorKey);
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
    const selected = highlight && selectedFloorKeys.has(f._key);
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

  // Resize handles when exactly one floor is selected.
  if (selected && !ghost && selectedFloorKeys.size === 1) {
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

interface FloorHit {
  floor: EditorFloor;
  mode: "move" | "resize";
  edge: string;
  anchorX: number;
  anchorZ: number;
}

function hitTestFloorsAll(wx: number, wz: number): FloorHit[] {  const hits: FloorHit[] = [];
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
      hits.push({ floor: f, mode: "resize", edge, anchorX, anchorZ });
      continue;
    }
    hits.push({ floor: f, mode: "move", edge: "", anchorX: 0, anchorZ: 0 });
  }
  return hits;
}

function dragFloor(f: EditorFloor, mx: number, my: number) {
  const { x: wx, z: wz } = canvasToWorld(mx, my);
  if (dragFloorMode === "move") {
    if (f.surfaceKind === "raft") {
      f._wx = wx;
      f._wz = wz;
      snapRaftCenterToGrid(f);
    } else {
      const snapped = snapFootprintCenter(wx, wz, f._wCells, f._dCells, f.localRotationY ?? 0, CELL, snapStep);
      f._wx = snapped.x;
      f._wz = snapped.z;
    }
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

/** Snap a raft floor so its plank lattice sits on the global CELL grid (grid origin when known),
 *  keeping rafts aligned with each other and with placed items across save/reload round trips. */
function snapRaftCenterToGrid(f: EditorFloor): void {
  if (f.surfaceKind !== "raft") return;
  const ox = gridInfo?.found ? gridInfo.worldPosition.x : 0;
  const oz = gridInfo?.found ? gridInfo.worldPosition.z : 0;
  const minX = f._wx - ((f._wCells - 1) / 2) * CELL;
  const minZ = f._wz - ((f._dCells - 1) / 2) * CELL;
  const sMinX = ox + Math.round((minX - ox) / CELL) * CELL;
  const sMinZ = oz + Math.round((minZ - oz) / CELL) * CELL;
  f._wx = sMinX + ((f._wCells - 1) / 2) * CELL;
  f._wz = sMinZ + ((f._dCells - 1) / 2) * CELL;
}

function finalizeFloor(f: EditorFloor) {
  if (f.surfaceKind === "raft") {
    // Raft floors snap their plank lattice to the global CELL grid instead of the half-cell step.
    snapRaftCenterToGrid(f);
  } else {
    // Snap by footprint edges (same as items) so adjacent floors share a common grid edge
    // instead of independently rounding centers, which can create half-cell overlaps/gaps.
    const snapped = snapFootprintCenter(
      f._wx,
      f._wz,
      f._wCells,
      f._dCells,
      f.localRotationY ?? 0,
      CELL,
      snapStep
    );
    f._wx = snapped.x;
    f._wz = snapped.z;
  }
  f.localPosition.x = f._wx;
  f.localPosition.z = f._wz;
  f.worldPosition.x = f._wx;
  f.worldPosition.z = f._wz;
  f.widthUnits = f._wCells * CELL;
  f.depthUnits = f._dCells * CELL;
  // Smart material match by size tag.
  if (f.surfaceKind !== "background" && f.surfaceKind !== "raft") tryMatchFloorMaterialBySize(f);
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

function addFloorAt(wx: number, wz: number) {
  pushHistory();
  const id = `new:floor:${crypto.randomUUID()}`;
  const key = newEditorKey();
  const w = 4;
  const d = 4;
  const snapped = snapFootprintCenter(wx, wz, w, d, 0, CELL, snapStep);
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
    localPosition: { x: snapped.x, y: -0.05, z: snapped.z },
    worldPosition: { x: snapped.x, y: -0.05, z: snapped.z },
    localRotationY: 0,
    localScale: { x: (w * CELL) / 10, y: 1, z: (d * CELL) / 10 },
    widthUnits: w * CELL,
    depthUnits: d * CELL,
    widthCells: w,
    depthCells: d,
    _wx: snapped.x,
    _wz: snapped.z,
    _wCells: w,
    _dCells: d,
  };
  floors.push(floor);
  setFloorSelection([key]);
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
      <dt>材质</dt><dd id="fe-mat">${f.surfaceKind === "raft" ? "木筏拼块（写回时生成）" : matchedMat?.nameZh ?? f.materialName ?? "无"}</dd>
      <dt>坐标</dt><dd>x ${f._wx.toFixed(2)}, z ${f._wz.toFixed(2)}</dd>
      <dt>旋转</dt><dd>${normalizeRot(f.localRotationY)}°</dd>
      <dt>死亡类型</dt><dd>${deathLabelZh(deathInfo)}（只读）</dd>
    </dl>
    <div class="floor-edit-row">
      <label>宽(格) <input type="number" min="1" id="fe-w" value="${f._wCells}" /></label>
      <label>高(格) <input type="number" min="1" id="fe-d" value="${f._dCells}" /></label>
      <span class="muted" style="align-self:center;font-size:11px">自动应用（仅页面）</span>
    </div>
    <div class="mat-pick-title">特殊材质</div>
    <div class="mat-pick-list">
      <button type="button" class="mat-pick${f.surfaceKind === "raft" ? " active" : ""}" id="fe-to-raft"><span class="mat-id">🪵 木筏段</span><span class="mat-sub">作为木筏地板（写回时按尺寸铺满拼块）</span></button>
    </div>
    <div class="mat-pick-title">切换材质（点击应用）</div>
    <div class="mat-pick-list">${matRows || '<div class="mat-pick-empty">当前关卡集无材质</div>'}</div>
    <p class="close-hint">拖四角缩放 · 宽高输入即时生效 · 左键拖动移动 · 右键此面板切换材质 · Esc 关闭</p>
  `;
  detailEl.classList.remove("hidden");
  positionDetail(clientX, clientY);

  const feW = document.getElementById("fe-w") as HTMLInputElement;
  const feD = document.getElementById("fe-d") as HTMLInputElement;
  let sizePushed = false;
  const applyFloorSizeLive = () => {
    const wv = parseInt(feW.value, 10);
    const dv = parseInt(feD.value, 10);
    if (!(wv > 0) && !(dv > 0)) return;
    if (!sizePushed) {
      pushHistory();
      sizePushed = true;
    }
    if (wv > 0) f._wCells = wv;
    if (dv > 0) f._dCells = dv;
    finalizeFloor(f);
    draw();
    const sz = document.getElementById("fe-size");
    if (sz)
      sz.textContent = `${f._wCells} × ${f._dCells} 格 (${(f._wCells * CELL).toFixed(1)} × ${(f._dCells * CELL).toFixed(1)} m，${f._wCells * f._dCells} 格)`;
    const m = floorMaterials.find((x) => x.guid === f.materialGuid);
    const matEl = document.getElementById("fe-mat");
    if (matEl)
      matEl.textContent =
        f.surfaceKind === "raft" ? "木筏拼块（写回时生成）" : m?.nameZh ?? f.materialName ?? "无";
    detailEl.querySelectorAll<HTMLButtonElement>(".mat-pick").forEach((b) =>
      b.classList.toggle("active", b.dataset.guid === f.materialGuid)
    );
  };
  feW.addEventListener("input", applyFloorSizeLive);
  feD.addEventListener("input", applyFloorSizeLive);
  feW.addEventListener("change", applyFloorSizeLive);
  feD.addEventListener("change", applyFloorSizeLive);

  detailEl.querySelectorAll<HTMLButtonElement>(".mat-pick[data-guid]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const m = floorMaterials.find((x) => x.guid === btn.dataset.guid);
      if (!m) return;
      pushHistory();
      f.materialGuid = m.guid;
      f.materialAssetPath = m.assetPath;
      f.materialName = m.id;
      if (f.surfaceKind === "raft") f.surfaceKind = "solid";
      draw();
      setStatus(`已切换材质：${m.nameZh}（写回后生效）`);
      showFloorDetail(f, clientX, clientY);
    });
  });
  document.getElementById("fe-to-raft")?.addEventListener("click", () => {
    if (f.surfaceKind !== "raft") {
      pushHistory();
      f.surfaceKind = "raft";
      snapRaftCenterToGrid(f);
      finalizeFloor(f);
      draw();
      setStatus(`已设为木筏地板 ${f._wCells}×${f._dCells}（写回时按尺寸铺满木筏拼块，可 Ctrl+Z 撤回）`);
    }
    showFloorDetail(f, clientX, clientY);
  });
}

function positionFloating(el: HTMLElement, clientX: number, clientY: number) {
  const margin = 8;
  let left = clientX + margin;
  let top = clientY + margin;
  el.style.left = `${left}px`;
  el.style.top = `${top}px`;
  requestAnimationFrame(() => {
    const rect = el.getBoundingClientRect();
    if (rect.right > window.innerWidth - margin) {
      left = Math.max(margin, clientX - rect.width - margin);
      el.style.left = `${left}px`;
    }
    if (rect.bottom > window.innerHeight - margin) {
      top = Math.max(margin, clientY - rect.height - margin);
      el.style.top = `${top}px`;
    }
  });
}

function positionDetail(clientX: number, clientY: number) {
  positionFloating(detailEl, clientX, clientY);
}

interface PickCandidate {
  title: string;
  sub: string;
  onPick: () => void;
}

function showPickTip(candidates: PickCandidate[], clientX: number, clientY: number) {
  pickTipEl.innerHTML =
    `<div class="pick-tip-head">此处有 ${candidates.length} 个重叠对象，请选择要操作的对象：</div>` +
    candidates
      .map(
        (c, i) =>
          `<button type="button" class="pick-tip-item" data-idx="${i}"><span class="pick-title">${escHtml(c.title)}</span><span class="pick-sub">${escHtml(c.sub)}</span></button>`
      )
      .join("") +
    `<p class="close-hint">点击外部或 Esc 关闭</p>`;
  pickTipEl.classList.remove("hidden");
  positionFloating(pickTipEl, clientX, clientY);
  pickTipEl.querySelectorAll<HTMLButtonElement>(".pick-tip-item").forEach((btn) => {
    btn.addEventListener("click", () => {
      hidePickTip();
      candidates[Number(btn.dataset.idx)]?.onPick();
    });
  });
}

function hidePickTip() {
  pickTipEl.classList.add("hidden");
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

function isPlayerItem(item: EditorItem): boolean {
  return item.stubKind === "Player" || prefabIdFromPath(item.prefabAssetPath) === "Player";
}

function isServingStationItem(item: EditorItem): boolean {
  return item.stubKind === "ServingStation" || prefabIdFromPath(item.prefabAssetPath) === "ServingStation";
}

function isPlateReturnItem(item: EditorItem): boolean {
  return item.stubKind === "PlateReturn" || prefabIdFromPath(item.prefabAssetPath) === "PlateReturn";
}

function isFoodSpawnerItem(item: EditorItem): boolean {
  return item.stubKind === "AttachingFoodSpawner" || prefabIdFromPath(item.prefabAssetPath) === "AttachingFoodSpawner";
}

function servingStations(): EditorItem[] {
  return items.filter(isServingStationItem);
}

function plateReturns(): EditorItem[] {
  return items.filter(isPlateReturnItem);
}

let plateReturnLabels = new Map<string, string>();

function computePlateReturnLabels(): Map<string, string> {
  const label = new Map<string, string>();
  let n = 0;
  for (const t of plateReturns()) label.set(t.instanceId, (++n).toString());
  return label;
}

function itemHalfExtents(item: EditorItem): { hx: number; hz: number } {
  const fp = resolveFootprint(item);
  return {
    hx: ((fp.cellsX * CELL) / 2) * itemScaleX(item),
    hz: ((fp.cellsZ * CELL) / 2) * itemScaleZ(item),
  };
}

function rectsOverlap(
  ax: number, az: number, ahx: number, ahz: number,
  bx: number, bz: number, bhx: number, bhz: number
): boolean {
  return Math.abs(ax - bx) < ahx + bhx - 1e-6 && Math.abs(az - bz) < ahz + bhz - 1e-6;
}

/** 玩家最高优先级：物品不允许与玩家重叠，玩家也不允许与任何非地板物品/其他玩家重叠。 */
function moveBlockedAt(item: EditorItem, wx: number, wz: number, ignoreKeys?: Set<string>): boolean {
  const { hx, hz } = itemHalfExtents(item);
  const isPlayer = isPlayerItem(item);
  for (const o of items) {
    if (o._editorKey === item._editorKey) continue;
    if (ignoreKeys?.has(o._editorKey)) continue;
    if (isPlayer) {
      if (isSurfaceItem(catalogByGuid.get(o.prefabGuid))) continue;
    } else if (!isPlayerItem(o)) {
      continue;
    }
    const oh = itemHalfExtents(o);
    if (rectsOverlap(wx, wz, hx, hz, o._wx, o._wz, oh.hx, oh.hz)) return true;
  }
  return false;
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

function drawConveyorArrow(center: { x: number; y: number }, rot: number, cellPx: number, speed: number, color = "#ffe49a") {
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
  ctx.strokeStyle = color;
  ctx.fillStyle = color;
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

function drawServingLinks() {
  const byInst = new Map(items.map((i) => [i.instanceId, i]));
  for (const s of servingStations()) {
    const prId = s.servingStation?.plateReturnInstanceId;
    if (!prId) continue;
    const p = byInst.get(prId);
    if (!p) continue;
    const a = worldToCanvas(s._wx, s._wz);
    const b = worldToCanvas(p._wx, p._wz);
    ctx.save();
    ctx.strokeStyle = "#7bd889";
    ctx.globalAlpha = 0.5;
    ctx.lineWidth = 1.5;
    ctx.setLineDash([6, 4]);
    ctx.beginPath();
    ctx.moveTo(a.x, a.y);
    ctx.lineTo(b.x, b.y);
    ctx.stroke();
    ctx.setLineDash([]);
    const rad = Math.atan2(b.y - a.y, b.x - a.x);
    const ah = 8 * Math.max(0.6, scale);
    ctx.fillStyle = "#7bd889";
    ctx.beginPath();
    ctx.moveTo(b.x, b.y);
    ctx.lineTo(b.x - Math.cos(rad - 0.45) * ah, b.y - Math.sin(rad - 0.45) * ah);
    ctx.lineTo(b.x - Math.cos(rad + 0.45) * ah, b.y - Math.sin(rad + 0.45) * ah);
    ctx.closePath();
    ctx.fill();
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
  const isPlayer = isPlayerItem(item);
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

  if (!isPlayer) {
    ctx.beginPath();
    ctx.rect(-bw / 2 + 2, -bh / 2 + 2, bw - 4, bh - 4);
    ctx.clip();

    ctx.fillStyle = paint.label;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    drawLabelInBox(ctx, itemLabel(item), bw - 4, bh - 4);
  }

  ctx.restore();

  if (isPlayer) {
    ctx.save();
    ctx.beginPath();
    ctx.rect(center.x - bw / 2 + 2, center.y - bh / 2 + 2, bw - 4, bh - 4);
    ctx.clip();
    ctx.fillStyle = paint.label;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.translate(center.x, center.y);
    drawLabelInBox(ctx, itemLabel(item), bw - 4, bh - 4);
    ctx.restore();
  }

  if (isConveyorItem(item)) {
    drawConveyorArrow(center, rot, cellPx, item.conveyor?.conveySpeed ?? 0.5);
  } else if (isTeleportalItem(item)) {
    drawTeleportalBadge(item, center, cellPx);
  } else if (isFoodSpawnerItem(item)) {
    drawConveyorArrow(center, rot, cellPx, 1, "#7bd889");
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

function hitTestAll(wx: number, wz: number): EditorItem[] {
  const includeSurface = currentLayer === "floor";
  const sorted = [...items]
    .filter((it) => includeSurface || !isSurfaceItem(catalogByGuid.get(it.prefabGuid)))
    .sort((a, b) => drawLayerForItem(b, catalogByGuid) - drawLayerForItem(a, catalogByGuid));
  return sorted.filter((item) => {
    const fp = resolveFootprint(item);
    const { lx, lz } = worldToItemLocal(item, wx, wz);
    const hw = ((fp.cellsX * CELL) / 2) * itemScaleX(item);
    const hh = ((fp.cellsZ * CELL) / 2) * itemScaleZ(item);
    return Math.abs(lx) <= hw && Math.abs(lz) <= hh;
  });
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
    case "Player": {
      const pid = item.player?.playerID ?? 11;
      return `<dt>玩家</dt><dd>${pid === 11 ? "自动（按加入顺序）" : `玩家 ${pid + 1}`} · 固定出生点，仅可拖动位置</dd>`;
    }
    case "ServingStation": {
      const prId = item.servingStation?.plateReturnInstanceId;
      const pr = prId ? items.find((i) => i.instanceId === prId) : undefined;
      const txt = pr
        ? `已绑定 → 脏盘台 ${plateReturnLabels.get(pr.instanceId) ?? "?"}（${itemLabel(pr)}）`
        : prId
          ? "绑定的脏盘台不在当前场景"
          : "未绑定脏盘台";
      return `<dt>上菜口</dt><dd>${txt}（右键直接修改）</dd>`;
    }
    case "PlateReturn": {
      const bound = servingStations().filter((s) => s.servingStation?.plateReturnInstanceId === item.instanceId);
      const txt = bound.length
        ? `被 ${bound.length} 个上菜口绑定：${bound.map((s) => itemLabel(s)).join("、")}`
        : "未被任何上菜口绑定";
      return `<dt>脏盘台</dt><dd>${txt}（右键管理绑定）</dd>`;
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
  let scalePushed = false;
  const applyScale = () => {
    const v = parseFloat(scaleInput.value);
    if (!isFinite(v) || v < 0.5) return;
    if (!scalePushed) {
      pushHistory();
      scalePushed = true;
    }
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
      <dt>中文名</dt><dd>${cat?.nameZh ? tidyCatalogNameZh(cat.nameZh, cat.id) : "—"}</dd>
      <dt>资源路径</dt><dd>${item.prefabAssetPath || "—"}</dd>
      <dt>层级路径</dt><dd>${item.hierarchyPath}</dd>
      <dt>父节点</dt><dd>${item.parentPath || "—"}</dd>
      <dt>占地</dt><dd>${fp.cellsX} × ${fp.cellsZ} 格 (${(fp.cellsX * CELL).toFixed(1)} × ${(fp.cellsZ * CELL).toFixed(1)} m)</dd>
      <dt>本地坐标</dt><dd>x ${item.localPosition.x.toFixed(2)}, y ${item.localPosition.y.toFixed(2)}, z ${item.localPosition.z.toFixed(2)}</dd>
      <dt>旋转 Y</dt><dd>${normalizeRot(item.localRotationY)}°</dd>
      ${isSurfaceItem(cat) ? `<dt>缩放</dt><dd>${itemUniformScale(item).toFixed(2)}×（右键菜单可调整大小）</dd>` : ""}
      <dt>分类</dt><dd>${isSurfaceItem(cat) ? surfaceKindLabelZh(cat?.surfaceKind) + "（地板层）" : cat?.layoutTier === "decor" ? "装饰道具" : "核心玩法"} · ${cat?.nameZh ? tidyCatalogNameZh(cat.nameZh, cat.id) : cat?.category ?? "—"}</dd>
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

function setFloorSelection(keys: string[], primary?: string): void {
  selectedFloorKeys = new Set(keys);
  selectedFloorKey = keys.length ? primary ?? keys[keys.length - 1] : null;
}

function clearFloorSelection(): void {
  selectedFloorKeys = new Set<string>();
  selectedFloorKey = null;
}

function nudgeItem(item: EditorItem, dx: number, dz: number) {
  if (moveBlockedAt(item, item._wx + dx, item._wz + dz)) {
    setStatus("目标位置与玩家重叠，不可放置", false);
    return;
  }
  pushHistory();
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
  for (const k of [...kill]) {
    const it = items.find((i) => i._editorKey === k);
    if (it && isPlayerItem(it)) kill.delete(k);
  }
  if (kill.size === 0) {
    setStatus("玩家不可删除", false);
    return;
  }
  pushHistory();
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
    .filter((i): i is EditorItem => !!i && !isPlayerItem(i))
    .map((i) => JSON.parse(JSON.stringify(i)) as EditorItem);
  pasteRound = 0;
  if (!clipboard.length) {
    setStatus("玩家不可复制", false);
    return;
  }
  setStatus(`已复制 ${clipboard.length} 个物品（Ctrl/Cmd+V 粘贴）`);
}

function cutSelection() {
  const keys = selectionKeys();
  if (!keys.length) {
    setStatus("没有选中物品可裁切", false);
    return;
  }
  copySelection();
  deleteSelected();
  setStatus(`已裁切 ${clipboard.length} 个物品（Ctrl/Cmd+V 粘贴，Ctrl/Cmd+Z 撤回）`);
}

function pasteClipboard() {
  if (!clipboard.length) {
    setStatus("剪贴板为空（先 Ctrl/Cmd+V 复制）", false);
    return;
  }
  pushHistory();
  pasteRound++;
  const off = CELL * pasteRound;
  const pasted: string[] = [];
  let skipped = 0;
  for (const src of clipboard) {
    if (isPlayerItem(src)) {
      skipped++;
      continue;
    }
    const nx = src._wx + off;
    const nz = src._wz + off;
    if (moveBlockedAt(src, nx, nz)) {
      skipped++;
      continue;
    }
    const editorKey = newEditorKey();
    const copy = JSON.parse(JSON.stringify(src)) as EditorItem;
    copy._editorKey = editorKey;
    copy.instanceId = `new:copy:${crypto.randomUUID()}`;
    copy.hierarchyPath = copy.instanceId;
    copy._wx = nx;
    copy._wz = nz;
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
  setStatus(`已粘贴 ${pasted.length} 个物品${skipped ? `（${skipped} 个因与玩家重叠被跳过）` : ""}`);
}

function showContextMenu(item: EditorItem, clientX: number, clientY: number) {
  const cat = catalogByGuid.get(item.prefabGuid);
  const isSurface = isSurfaceItem(cat);
  const isPlayer = isPlayerItem(item);
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
    ${
      isPlayer
        ? ""
        : `<div class="ctx-nudge-row">
      <span class="ctx-label">旋转 <span id="ctx-rot" class="ctx-scale-val">${rot}°</span></span>
      <div class="ctx-nudge">
        <button type="button" data-rot="-90" title="逆时针 90°">−90°</button>
        <input type="number" id="ctx-rot-input" class="ctx-input ctx-rot-input" min="0" max="359" step="1" value="${rot}" title="任意角度 (0~359)" />
        <button type="button" data-rot="90" title="顺时针 90°">+90°</button>
      </div>
    </div>`
    }
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
      ${isPlayer ? "" : `<button type="button" class="ctx-btn" data-act="copy">复制 (Ctrl+C)</button>
      <button type="button" class="ctx-btn" data-act="cut">裁切 (Ctrl+X)</button>`}
      <button type="button" class="ctx-btn" data-act="paste">粘贴 (Ctrl+V)</button>
      ${isPlayer ? "" : `<button type="button" class="ctx-btn danger" data-act="delete">删除</button>`}
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
      pushHistory();
      setItemUniformScale(item, next);
      const scaleEl = document.getElementById("ctx-scale");
      if (scaleEl) scaleEl.textContent = `${next.toFixed(1)}×`;
      draw();
    });
  });
  const applyRotation = (deg: number) => {
    if (!isFinite(deg)) return;
    pushHistory();
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
  ctxMenuEl.querySelector('[data-act="cut"]')?.addEventListener("click", () => {
    hideContextMenu();
    cutSelection();
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
  const alive = new Set(floors.map((f) => f._key));
  if ([...selectedFloorKeys].some((k) => !alive.has(k))) {
    setFloorSelection([...selectedFloorKeys].filter((k) => alive.has(k)));
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
  const wz = (bounds?.cz ?? 0) + 2 * CELL;
  const cat = catalogItemById(pid);
  if (!cat) return;
  addFromCatalog(cat, wx, wz, false);
  setStatus(`已切换背景环境：${tidyCatalogNameZh(cat.nameZh, cat.id)}（${pid}）`);
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
    row.innerHTML = `<div class="zh">${tidyCatalogNameZh(it.nameZh, it.id)}</div><div class="id">${it.id}</div>${sub}`;
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
  // Re-finalize so write-back coords match the active snap step (avoids drift vs Unity apply).
  for (const f of floors) finalizeFloor(f);

  const raftItems: LayoutItem[] = [];
  const missingIds = new Set<string>();
  for (const f of floors) {
    if (f.surfaceKind !== "raft") continue;
    for (const p of raftPiecesForRect(f._wCells, f._dCells)) {
      const cat = catalogItemById(p.id);
      if (!cat) {
        missingIds.add(p.id);
        continue;
      }
      const id = `new:raft:${crypto.randomUUID()}`;
      const px = f._wx + p.dx;
      const pz = f._wz + p.dz;
      raftItems.push({
        instanceId: id,
        hierarchyPath: id,
        prefabGuid: cat.guid,
        prefabAssetPath: cat.assetPath,
        parentPath: cat.defaultParent,
        displayName: cat.id,
        localPosition: { x: px, y: 0, z: pz },
        worldPosition: { x: px, y: 0, z: pz },
        localRotationY: p.rotY,
        footprint: cat.footprint,
        // Walkability comes from the retained raft floor rect (one Col_Floor),
        // not per-plank — dual lattice would otherwise stack overlapping colliders.
        walkable: false,
      });
    }
  }
  if (missingIds.size > 0) {
    throw new Error(
      `木筏拼块目录缺失：${[...missingIds].join(", ")}（请重新生成 catalog.json）`
    );
  }
  return {
    sceneAssetPath: scenePath,
    items: items
      .map(({ _editorKey, _wx, _wz, _parentWx, _parentWz, ...rest }): LayoutItem => {
        const fp = resolveFootprint(rest);
        const cat = catalogByGuid.get(rest.prefabGuid);
        // Raft planks already expanded above are walkable:false; other floor prefabs stay walkable.
        const isRaftPlank = cat?.surfaceKind === "raft";
        return {
          ...rest,
          footprint: fp,
          worldPosition: { x: _wx, y: rest.localPosition?.y ?? 0, z: _wz },
          walkable: !isRaftPlank && !!(cat && cat.surfaceTier === "floor"),
        };
      })
      .concat(raftItems),
    // Keep raft floors in floors[] so Unity SyncWalkableToFloors builds one Col_Floor
    // per raft rect. ApplyFloors skips surfaceKind=="raft" (no Plane mesh).
    floors: floors.map(({ _key, _wx, _wz, _wCells, _dCells, ...rest }) => ({
      ...rest,
      widthCells: _wCells,
      depthCells: _dCells,
      widthUnits: _wCells * CELL,
      depthUnits: _dCells * CELL,
      worldPosition: { x: _wx, y: rest.localPosition?.y ?? -0.05, z: _wz },
      localPosition: { x: _wx, y: rest.localPosition?.y ?? -0.05, z: _wz },
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
    if (group.key === "Player") continue;
    const list = (catalog.byCategory[group.key] ?? []).filter((it) => {
      if (it.surfaceTier === "floor" || it.surfaceTier === "background") return false;
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

    for (const it of list) {
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
      row.innerHTML = `<div class="zh">${tidyCatalogNameZh(it.nameZh, it.id)}</div><div class="id">${it.nameEn} · ${it.id}</div>${sub}`;
      row.addEventListener("dragstart", (e) => {
        dragCatalog = it;
        e.dataTransfer?.setData("text/plain", it.guid);
      });
      row.addEventListener("dragend", () => {
        dragCatalog = null;
      });
      details.appendChild(row);
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
    {
      key: "background",
      labelZh: "背景 / 环境",
      match: (it) => it.surfaceTier === "background" && !isThemeBackgroundPrefabId(it.id),
    },
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
  if (selectedFloorKeys.size > 1) {
    info = `<span class="fb-info">已选 ${selectedFloorKeys.size} 块地板（拖动整体移动 · Del 删除）</span>`;
  } else if (f) {
    info = `<span class="fb-info"><b>${surfaceKindLabelZh(f.surfaceKind)}</b> · ${f._wCells}×${f._dCells}格 · ${f.surfaceKind === "raft" ? "木筏拼块（写回时生成）" : f.materialName ?? "无材质"}</span>`;
  } else if (selItem && isSurfaceItem(selCat)) {
    info = `<span class="fb-info"><b>${surfaceKindLabelZh(selCat?.surfaceKind)}</b> · ${itemLabel(selItem)}</span>`;
  } else {
    info = `<span class="fb-info">${deathLabelZh(deathInfo)} · 共 ${floors.length} 块地板</span>`;
  }
  floorBar.innerHTML = `${themeRow}${killToggle}${walkToggle}${info}<span class="fb-hint">背景为坠落死亡区 · 拖拽空白框选 · 拖动移动 · 拖角点缩放 · 右键详情</span>`;

  floorBar.querySelectorAll<HTMLButtonElement>(".fb-theme-btn").forEach((btn) => {
    btn.addEventListener("click", () => {
      const nextTheme = btn.dataset.bg ?? "void";
      if (nextTheme === bgThemeKey) return;
      pushHistory();
      bgThemeKey = nextTheme;
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
  sceneListCache = scenes;
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
    const target = sceneSelect.value;
    if (!target || target === scenePath) return;
    confirmLeaveIfDirty(() => void loadScene(target));
  });

  document.getElementById("btn-reload")!.addEventListener("click", () => {
    if (scenePath) confirmLeaveIfDirty(() => void loadScene(scenePath));
  });

  document.getElementById("btn-save")!.addEventListener("click", () => void saveToUnity());

  document.getElementById("btn-recipes")!.addEventListener("click", () => void openRecipesDialog());

  const withLevelDetail = async (fn: (detail: LevelDetail) => void | Promise<void>) => {
    if (!scenePath) {
      setStatus("请先选择场景", false);
      return;
    }
    try {
      showBusy("加载关卡信息…");
      const level = await fetchLevelRecipes(scenePath);
      if (!level.levelInfoAssetPath) {
        setStatus("未找到该场景对应的 LevelInfoSO", false);
        return;
      }
      const detail = await fetchLevelDetail(level.levelInfoAssetPath);
      await fn(detail);
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  };
  document.getElementById("btn-level-config")!.addEventListener("click", () =>
    void withLevelDetail((detail) => openConfigTabsModal(detail, currentLevelSet, () => {}))
  );
  document.getElementById("btn-level-audio")!.addEventListener("click", () =>
    void withLevelDetail((detail) => openAudioModal(detail, () => {}))
  );

  document.getElementById("btn-sync")!.addEventListener("click", () => openSyncLayoutDialog());

  wireNav((target) => {
    if (target === "manage") confirmLeaveIfDirty(() => goManage());
  });

  window.addEventListener("beforeunload", (e) => {
    if (!dirty) return;
    e.preventDefault();
    e.returnValue = "";
  });

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
      clearFloorSelection();
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
    const urlScene = new URLSearchParams(location.search).get("scene") ?? "";
    const match =
      (target ? scenes.find((s) => s.assetPath === target) : null) ??
      (urlScene ? scenes.find((s) => s.assetPath === urlScene) : null);
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

/** Merge raft plank items that form solid rectangles into raft floors,
 *  so web-side raft editing uses the same logic as floor planes (write-back re-expands them).
 *
 *  Dual-lattice planks: middle_01 sits on the CELL primary lattice; back/front/middle_02/03
 *  sit on half-cell diagonals. Clustering links both lattices; dimensions come from middle_01
 *  when possible, with a fallback for older single-lattice saves. */
function mergeRaftItemsIntoFloors(): void {
  const raftOf = (it: EditorItem) => catalogByGuid.get(it.prefabGuid)?.surfaceKind === "raft";
  const catalogId = (it: EditorItem) => catalogByGuid.get(it.prefabGuid)?.id ?? "";
  const isPrimary = (it: EditorItem) => catalogId(it) === "raft_raft_middle_01";
  const rafts = items.filter(raftOf);
  if (rafts.length === 0) return;

  const TOL = 0.05;
  /** Secondary↔primary nearest distance in official scenes is ~0.82–0.86. */
  const SECONDARY_LINK = 0.92;
  const parent = rafts.map((_, i) => i);
  const find = (a: number): number => {
    while (parent[a] !== a) {
      parent[a] = parent[parent[a]];
      a = parent[a];
    }
    return a;
  };
  const union = (a: number, b: number) => {
    parent[find(a)] = find(b);
  };
  for (let i = 0; i < rafts.length; i++) {
    for (let j = i + 1; j < rafts.length; j++) {
      const dx = Math.abs(rafts[i]._wx - rafts[j]._wx);
      const dz = Math.abs(rafts[i]._wz - rafts[j]._wz);
      if (Math.abs(dx - CELL) < TOL && dz < TOL) union(i, j);
      else if (Math.abs(dz - CELL) < TOL && dx < TOL) union(i, j);
      else if (Math.hypot(dx, dz) < SECONDARY_LINK) union(i, j);
    }
  }
  const clusters = new Map<number, EditorItem[]>();
  for (let i = 0; i < rafts.length; i++) {
    const root = find(i);
    const arr = clusters.get(root) ?? [];
    arr.push(rafts[i]);
    clusters.set(root, arr);
  }

  const trySolidRect = (
    lattice: EditorItem[]
  ): { w: number; d: number; cx: number; cz: number } | null => {
    if (lattice.length < 1) return null;
    const minPosX = Math.min(...lattice.map((it) => it._wx));
    const maxPosX = Math.max(...lattice.map((it) => it._wx));
    const minPosZ = Math.min(...lattice.map((it) => it._wz));
    const maxPosZ = Math.max(...lattice.map((it) => it._wz));
    const cells = new Set<string>();
    for (const it of lattice) {
      const fi = (it._wx - minPosX) / CELL;
      const fj = (it._wz - minPosZ) / CELL;
      if (Math.abs(fi - Math.round(fi)) > TOL / CELL || Math.abs(fj - Math.round(fj)) > TOL / CELL) {
        return null;
      }
      cells.add(`${Math.round(fi)},${Math.round(fj)}`);
    }
    const w = Math.round((maxPosX - minPosX) / CELL) + 1;
    const d = Math.round((maxPosZ - minPosZ) / CELL) + 1;
    if (cells.size !== lattice.length || cells.size !== w * d) return null;
    return {
      w,
      d,
      cx: (minPosX + maxPosX) / 2,
      cz: (minPosZ + maxPosZ) / 2,
    };
  };

  for (const cluster of clusters.values()) {
    if (cluster.length < 2) continue;

    const primaries = cluster.filter(isPrimary);
    let rect = trySolidRect(primaries);
    // Fallback: older single-lattice saves (back/front/middle all on one CELL grid).
    if (!rect) rect = trySolidRect(cluster);
    if (!rect) continue;
    // Require at least 2 lattice cells, or a dual-lattice cluster with secondaries.
    if (rect.w * rect.d < 2 && cluster.length < 2) continue;

    const removeKeys = new Set(cluster.map((it) => it._editorKey));
    items = items.filter((it) => !removeKeys.has(it._editorKey));

    const { w, d, cx: cxW, cz: czW } = rect;
    const key = newEditorKey();
    floors.push({
      instanceId: `new:raft:${crypto.randomUUID()}`,
      _key: key,
      hierarchyPath: `raft:${key}`,
      parentPath: "Art",
      displayName: "Raft",
      surfaceKind: "raft",
      meshType: "plane",
      meshFileId: 10209,
      localPosition: { x: cxW, y: -0.05, z: czW },
      worldPosition: { x: cxW, y: -0.05, z: czW },
      localRotationY: 0,
      localScale: { x: (w * CELL) / 10, y: 1, z: (d * CELL) / 10 },
      widthUnits: w * CELL,
      depthUnits: d * CELL,
      widthCells: w,
      depthCells: d,
      _wx: cxW,
      _wz: czW,
      _wCells: w,
      _dCells: d,
    });
  }
}

async function loadScene(assetPath: string) {
  showBusy("加载场景…");
  try {
    setStatus("加载场景…");
    scenePath = assetPath;
    currentLevelSet = levelSetFromScenePath(assetPath);
    window.history.replaceState(
      null,
      "",
      `${location.pathname}?scene=${encodeURIComponent(assetPath)}${location.hash}`
    );
    const doc = await fetchLayout(assetPath);
    const dupIds = countDuplicateInstanceIds(doc.items);
    items = doc.items.map((raw, index) => enrichItem(raw, `i${index}`));
    floors = (doc.floors ?? []).map((raw, index) => enrichFloor(raw, `f${index}`));
    mergeRaftItemsIntoFloors();
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
    clearFloorSelection();
    hideDetail();
    history.clear();
    clearDirty();
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

async function saveToUnity(): Promise<boolean> {
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
    history.clear();
    clearDirty();
    await loadScene(scenePath);
    return true;
  } catch (e) {
    setStatus((e as Error).message, false);
    return false;
  } finally {
    hideBusy();
  }
}

function openSyncLayoutDialog(): void {
  if (!scenePath) {
    setStatus("请先选择场景", false);
    return;
  }
  const others = sceneListCache.filter((s) => s.assetPath !== scenePath);
  if (!others.length) {
    setStatus("没有其他可同步的场景", false);
    return;
  }
  const opts = others
    .map(
      (s) =>
        `<option value="${escHtml(s.assetPath)}">${escHtml(s.levelSet)} / ${escHtml(s.sceneName)}</option>`
    )
    .join("");
  openModal(
    "同步其他关卡的布局",
    `<label class="m-field">来源关卡<select id="sync-src">${opts}</select></label>
     <p class="modal-hint" style="color:#f28b82">将把来源关卡的<b>道具、地板与背景主题</b>复制到当前图，<b>覆盖当前图的全部内容</b>。仅修改前端数据（写回 Unity 后才落盘），可用 Ctrl+Z 撤回一次。</p>`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn danger" data-ok>覆盖并同步</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", () => {
    const sel = document.getElementById("sync-src") as HTMLSelectElement;
    closeModal();
    void syncLayoutFromScene(sel.value);
  });
}

async function syncLayoutFromScene(otherPath: string): Promise<void> {
  showBusy("读取来源场景布局…");
  try {
    const doc = await fetchLayout(otherPath);
    pushHistory();
    const keepPlayers = items.filter(isPlayerItem);
    const idMap = new Map<string, string>();
    items = doc.items
      .filter((raw) => prefabIdFromPath(raw.prefabAssetPath) !== "Player")
      .map((raw) => {
        const it = enrichItem(JSON.parse(JSON.stringify(raw)) as LayoutItem, newEditorKey());
        const nextId = `new:sync:${crypto.randomUUID()}`;
        if (raw.instanceId) idMap.set(raw.instanceId, nextId);
        it.instanceId = nextId;
        it.hierarchyPath = nextId;
        return it;
      });
    items.push(...keepPlayers);
    for (const it of items) {
      const exitId = it.teleportal?.exitPortalInstanceId;
      if (exitId && idMap.has(exitId) && it.teleportal) {
        it.teleportal.exitPortalInstanceId = idMap.get(exitId)!;
      }
      const prId = it.servingStation?.plateReturnInstanceId;
      if (prId && idMap.has(prId) && it.servingStation) {
        it.servingStation.plateReturnInstanceId = idMap.get(prId)!;
      }
    }
    floors = (doc.floors ?? []).map((raw) => {
      const f = enrichFloor(JSON.parse(JSON.stringify(raw)) as FloorObject, newEditorKey());
      f.instanceId = `new:floor:${crypto.randomUUID()}`;
      f.hierarchyPath = f.instanceId;
      return f;
    });
    mergeRaftItemsIntoFloors();
    const theme = inferBgThemeFromItems(items);
    if (theme) {
      bgThemeKey = theme;
      bgThemeDirty = true;
      localStorage.setItem("bgTheme:" + scenePath, bgThemeKey);
    }
    clearSelection();
    clearFloorSelection();
    hideDetail();
    hideContextMenu();
    markDirty();
    draw();
    setStatus(
      `已同步 ${items.length} 个道具、${floors.length} 块地板（覆盖当前图，写回 Unity 后生效，可 Ctrl+Z 撤回）`
    );
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

function addFromCatalog(cat: CatalogItem, wx: number, wz: number, recordHistory = true): EditorItem | null {
  if (cat.id === "Player") {
    setStatus("玩家固定在场景中，不可添加", false);
    return null;
  }
  const snapped = snapFootprintCenter(wx, wz, cat.footprint.cellsX, cat.footprint.cellsZ, 0, CELL, snapStep);
  const probe = {
    _editorKey: "",
    _wx: snapped.x,
    _wz: snapped.z,
    prefabGuid: cat.guid,
    prefabAssetPath: cat.assetPath,
    footprint: cat.footprint,
    stubKind: "",
  } as unknown as EditorItem;
  if (moveBlockedAt(probe, snapped.x, snapped.z)) {
    setStatus("该位置与玩家重叠，无法放置", false);
    return null;
  }
  if (recordHistory) pushHistory();
  const id = `new:${cat.guid}:${crypto.randomUUID()}`;
  const editorKey = newEditorKey();
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

    hidePickTip();

    if (currentLayer === "floor") {
      if (pendingNewFloor) {
        pendingNewFloor = false;
        canvas.style.cursor = "";
        addFloorAt(wx, wz);
        return;
      }
      const fHits = hitTestFloorsAll(wx, wz);
      const surfaceHits = hitTestAll(wx, wz).filter((it) =>
        isSurfaceItem(catalogByGuid.get(it.prefabGuid))
      );
      const keepFloor = selectedFloorKey
        ? fHits.find((fh) => fh.floor._key === selectedFloorKey)
        : undefined;
      const keepItem = selectedKey
        ? surfaceHits.find((it) => it._editorKey === selectedKey)
        : undefined;
      if (fHits.length + surfaceHits.length > 1 && !keepFloor && !keepItem) {
        hideDetail();
        hideContextMenu();
        const candidates: PickCandidate[] = [];
        for (const fh of fHits) {
          candidates.push({
            title: `${surfaceKindLabelZh(fh.floor.surfaceKind)} ${fh.floor._wCells}×${fh.floor._dCells}格`,
            sub: `地板 · ${fh.floor.materialName ?? "无材质"}`,
            onPick: () => {
              setFloorSelection([fh.floor._key]);
              clearSelection();
              draw();
            },
          });
        }
        for (const it of surfaceHits) {
          candidates.push({
            title: itemLabel(it),
            sub: `${surfaceKindLabelZh(catalogByGuid.get(it.prefabGuid)?.surfaceKind)} · ${prefabIdFromPath(it.prefabAssetPath)}`,
            onPick: () => {
              clearFloorSelection();
              setSelection([it._editorKey]);
              draw();
            },
          });
        }
        showPickTip(candidates, e.clientX, e.clientY);
        draw();
        return;
      }
      const fHit = keepFloor ?? fHits[0] ?? null;
      if (fHit) {
        dragSnapshot = snapshotState();
        clearSelection();
        if (e.shiftKey) {
          const next = new Set(selectedFloorKeys);
          if (next.has(fHit.floor._key)) next.delete(fHit.floor._key);
          else next.add(fHit.floor._key);
          setFloorSelection([...next], fHit.floor._key);
        } else if (selectedFloorKeys.size > 1 && selectedFloorKeys.has(fHit.floor._key)) {
          selectedFloorKey = fHit.floor._key;
          dragFloorKey = fHit.floor._key;
          dragFloorMode = "move";
          dragFloorEdge = "";
          dragFloorGroupKeys = [...selectedFloorKeys];
          dragLastWx = wx;
          dragLastWz = wz;
        } else {
          setFloorSelection([fHit.floor._key]);
          dragFloorKey = fHit.floor._key;
          dragFloorMode = fHit.mode;
          dragFloorEdge = fHit.edge;
          dragFloorAnchorX = fHit.anchorX;
          dragFloorAnchorZ = fHit.anchorZ;
          dragFloorGroupKeys = [];
        }
      } else {
        clearFloorSelection();
        const itemHit = keepItem ?? surfaceHits[0] ?? null;
        if (itemHit) {
          dragSnapshot = snapshotState();
          setSelection([itemHit._editorKey]);
          dragItemKey = itemHit._editorKey;
          dragGroupKeys = [];
          dragOffsetX = wx - itemHit._wx;
          dragOffsetZ = wz - itemHit._wz;
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
      }
      draw();
      return;
    }

    const hits = hitTestAll(wx, wz);
    const already = selectedKey ? hits.find((it) => it._editorKey === selectedKey) : undefined;
    if (hits.length > 1 && !e.shiftKey && !already) {
      hideDetail();
      hideContextMenu();
      showPickTip(
        hits.map((it) => ({
          title: itemLabel(it),
          sub: prefabIdFromPath(it.prefabAssetPath) || "—",
          onPick: () => {
            setSelection([it._editorKey]);
            draw();
          },
        })),
        e.clientX,
        e.clientY
      );
      draw();
      return;
    }
    const hit = already ?? hits[0] ?? null;
    if (hit) {
      dragSnapshot = snapshotState();
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
      const fHits = hitTestFloorsAll(wx, wz);
      const surfaceHits = hitTestAll(wx, wz).filter((it) =>
        isSurfaceItem(catalogByGuid.get(it.prefabGuid))
      );
      const keepFloor = selectedFloorKey
        ? fHits.find((fh) => fh.floor._key === selectedFloorKey)
        : undefined;
      const keepItem = selectedKey
        ? surfaceHits.find((it) => it._editorKey === selectedKey)
        : undefined;
      if (fHits.length + surfaceHits.length > 1 && !keepFloor && !keepItem) {
        hideDetail();
        hideContextMenu();
        const candidates: PickCandidate[] = [];
        for (const fh of fHits) {
          candidates.push({
            title: `${surfaceKindLabelZh(fh.floor.surfaceKind)} ${fh.floor._wCells}×${fh.floor._dCells}格`,
            sub: `地板 · ${fh.floor.materialName ?? "无材质"}`,
            onPick: () => {
              setFloorSelection([fh.floor._key]);
              clearSelection();
              showFloorDetail(fh.floor, e.clientX, e.clientY);
              draw();
            },
          });
        }
        for (const it of surfaceHits) {
          candidates.push({
            title: itemLabel(it),
            sub: `${surfaceKindLabelZh(catalogByGuid.get(it.prefabGuid)?.surfaceKind)} · ${prefabIdFromPath(it.prefabAssetPath)}`,
            onPick: () => {
              clearFloorSelection();
              setSelection([it._editorKey]);
              hideDetail();
              showContextMenu(it, e.clientX, e.clientY);
              draw();
            },
          });
        }
        showPickTip(candidates, e.clientX, e.clientY);
        draw();
        return;
      }
      const fHit = keepFloor ?? fHits[0] ?? null;
      if (fHit) {
        setFloorSelection([fHit.floor._key]);
        showFloorDetail(fHit.floor, e.clientX, e.clientY);
        draw();
        return;
      }
      const itemHit = keepItem ?? surfaceHits[0] ?? null;
      if (itemHit) {
        setSelection([itemHit._editorKey]);
        hideDetail();
        showContextMenu(itemHit, e.clientX, e.clientY);
        draw();
        return;
      }
      hideDetail();
      hideContextMenu();
      hidePickTip();
      return;
    }
    const hits = hitTestAll(wx, wz);
    const already = selectedKey ? hits.find((it) => it._editorKey === selectedKey) : undefined;
    if (hits.length > 1 && !already) {
      hideDetail();
      hideContextMenu();
      showPickTip(
        hits.map((it) => ({
          title: itemLabel(it),
          sub: prefabIdFromPath(it.prefabAssetPath) || "—",
          onPick: () => {
            setSelection([it._editorKey]);
            hideDetail();
            showContextMenu(it, e.clientX, e.clientY);
            draw();
          },
        })),
        e.clientX,
        e.clientY
      );
      draw();
      return;
    }
    const hit = already ?? hits[0] ?? null;
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
      hidePickTip();
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
      if (dragFloorGroupKeys.length > 1) {
        const { x: wx, z: wz } = canvasToWorld(mx, my);
        const dx = wx - dragLastWx;
        const dz = wz - dragLastWz;
        for (const k of dragFloorGroupKeys) {
          const f = floors.find((x) => x._key === k);
          if (f) {
            f._wx += dx;
            f._wz += dz;
          }
        }
        dragLastWx = wx;
        dragLastWz = wz;
      } else {
        const f = floors.find((x) => x._key === dragFloorKey);
        if (f) dragFloor(f, mx, my);
      }
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
      const groupSet = new Set(dragGroupKeys);
      for (const k of dragGroupKeys) {
        const it = items.find((i) => i._editorKey === k);
        if (it) {
          const nx = it._wx + dx;
          const nz = it._wz + dz;
          if (moveBlockedAt(it, nx, nz, groupSet)) continue;
          it._wx = nx;
          it._wz = nz;
        }
      }
      dragLastWx = newWx;
      dragLastWz = newWz;
    } else {
      const nx = wx - dragOffsetX;
      const nz = wz - dragOffsetZ;
      if (!moveBlockedAt(item, nx, nz)) {
        item._wx = nx;
        item._wz = nz;
        syncLocalFromWorld(item);
      }
    }
    draw();
  });

  window.addEventListener("mouseup", () => {
    if (panning) {
      panning = false;
      updateCanvasCursor();
    }
    if (dragFloorKey) {
      const keys = dragFloorGroupKeys.length > 1 ? dragFloorGroupKeys : [dragFloorKey];
      for (const k of keys) {
        const f = floors.find((x) => x._key === k);
        if (f) finalizeFloor(f);
      }
    }
    dragFloorKey = null;
    dragFloorGroupKeys = [];
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
    commitDragSnapshot();
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
      hidePickTip();
      closeModal();
      marqueeing = false;
      pendingNewFloor = false;
      canvas.style.cursor = "";
    }

    if ((e.ctrlKey || e.metaKey) && (e.key === "z" || e.key === "Z") && !isTypingTarget(e.target)) {
      if (e.shiftKey) redo();
      else undo();
      e.preventDefault();
      return;
    }
    if ((e.ctrlKey || e.metaKey) && (e.key === "x" || e.key === "X") && !isTypingTarget(e.target)) {
      cutSelection();
      e.preventDefault();
      return;
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
      if (e.key === "Delete" || e.key === "Backspace") {
        const killItems = new Set(
          selectionKeys().filter((k) => {
            const it = items.find((i) => i._editorKey === k);
            return it && isSurfaceItem(catalogByGuid.get(it.prefabGuid));
          })
        );
        if (selectedFloorKeys.size === 0 && killItems.size === 0) return;
        pushHistory();
        floors = floors.filter((x) => !selectedFloorKeys.has(x._key));
        items = items.filter((i) => !killItems.has(i._editorKey));
        clearFloorSelection();
        clearSelection();
        hideDetail();
        draw();
        return;
      }
      if (e.key === "r" || e.key === "R") {
        if (selectedFloorKeys.size === 1 && selectedFloorKey) {
          const f = floors.find((x) => x._key === selectedFloorKey);
          if (f) {
            pushHistory();
            f.localRotationY = normalizeRot(f.localRotationY + (e.shiftKey ? -90 : 90));
            draw();
          }
          return;
        }
        if (selectedFloorKeys.size === 0 && selectedKey) {
          const item = items.find((i) => i._editorKey === selectedKey);
          if (item && isSurfaceItem(catalogByGuid.get(item.prefabGuid))) {
            pushHistory();
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
    if ((e.key === "r" || e.key === "R") && !isPlayerItem(item)) {
      pushHistory();
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
    if (
      e.target !== canvas &&
      !pickTipEl.classList.contains("hidden") &&
      !pickTipEl.contains(e.target as Node)
    ) {
      hidePickTip();
    }
  });

  window.addEventListener("resize", () => draw());
}

if (MANAGE_ACTIVE) {
  void renderManageView(app);
} else {
  void init();
}
