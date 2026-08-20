import {
  S,
  LayerKey,
  LayerVisibility
} from "./state";
import { dom } from "./dom";
import { setStatus } from "./status";
import {
  loadCatalog,
  fetchHealth,
  fetchHealthInfo,
  fetchIngredients,
  fetchRecipeCatalog,
  fetchCounterAppearances,
  fetchSwitchMaterials,
  fetchLevelSets,
  fetchLevelRecipes,
  fetchLevelDetail,
  repairBrokenPrefabs
} from "../api";
import {
  warnIfBridgeOutdated,
  saveToUnity,
  loadScene,
  confirmLeaveIfDirty,
  openSyncLayoutDialog,
  startBridgeWatch,
  selectSceneInDropdowns
} from "./sceneIO";
import { buildPalette } from "./palette";
import { buildFloorPalette } from "./floorPalette";
import { refreshScopedSaveButton } from "./serialize";
import { scopedSaveMeta } from "./serialize";
import { openRecipesDialog } from "./ui/recipesDialogs";
import { openDepsCheckModal } from "./ui/depsCheck";
import { requestTestLayout } from "./testLayout";
import { openUtensilManager } from "./ui/utensilManager";
import { openCameraLightModal } from "./cameraLight";
import {
  applyPanelCollapse,
  updatePanelTabButtons,
  renderRightPanel,
  initPanelResizer,
  initPaletteResizer
} from "./panels";
import {
  clearSelection,
  clearFloorSelection
} from "./selection";
import {
  hideDetail,
  hideContextMenu
} from "./ui/overlay";
import { draw } from "./render";
import { setupCanvas } from "./input";
import {
  goManage,
  renderLevelSummary,
  openConfigTabsModal,
  openAudioModal,
  consumeTargetScene
} from "../levels";
import {
  showBusy,
  hideBusy
} from "../busy";
import { wireNav } from "../nav";
import { prefabIdFromPath } from "./coords";
import type { Catalog, LevelDetail } from "../types";

/** Catalog cache so `setLayer` can rebuild the palette without re-fetching. */
let layoutCatalog: Catalog = {
  generatedAt: "",
  gridCellSize: 1,
  itemCount: 0,
  items: [],
  byCategory: {},
};

const LAYER_LABEL: Record<LayerKey, string> = {
  items: "核心层",
  decor: "装饰层",
  floor: "地板层",
  background: "背景层",
  move: "移动层",
};

/** Reflect S.layerVisibility of the current layer into the popover checkboxes.
 *  Background content (water/sky) is only operable on the floor / background
 *  layers — elsewhere the toggle is locked off. */
export function syncVisibilityPopover(): void {
  const vis = S.layerVisibility[S.currentLayer];
  const bgLocked = S.currentLayer !== "floor" && S.currentLayer !== "background";
  if (bgLocked) vis.background = false;
  document.querySelectorAll<HTMLInputElement>("#vis-popover input[data-vcat]").forEach((cb) => {
    const cat = cb.dataset.vcat as keyof LayerVisibility;
    if (cat === "background" && bgLocked) {
      cb.disabled = true;
      cb.checked = false;
    } else {
      cb.disabled = false;
      cb.checked = vis[cat];
    }
  });
  const title = document.querySelector(".vis-title");
  if (title) title.textContent = `${LAYER_LABEL[S.currentLayer]} · 显示内容：`;
  const note = document.querySelector(".vis-note");
  if (note) {
    note.textContent = bgLocked
      ? "背景 / 水面仅在 🌊 背景层与 🗺️ 地板层显示与操作；关闭的类别不显示、也不可点选"
      : "关闭的类别将不显示、也不可点选（仅影响当前层）";
  }
}

function wireVisibilityPopover(): void {
  const btn = document.getElementById("btn-visibility");
  const pop = document.getElementById("vis-popover");
  btn?.addEventListener("click", (e) => {
    e.stopPropagation();
    pop?.classList.toggle("hidden");
    syncVisibilityPopover();
  });
  document.addEventListener("mousedown", (e) => {
    if (pop && !pop.classList.contains("hidden") && !pop.contains(e.target as Node) && e.target !== btn) {
      pop.classList.add("hidden");
    }
  });
  pop?.querySelectorAll<HTMLInputElement>("input[data-vcat]").forEach((cb) => {
    cb.addEventListener("change", () => {
      if (cb.disabled) return;
      const cat = cb.dataset.vcat as keyof LayerVisibility;
      S.layerVisibility[S.currentLayer][cat] = cb.checked;
      draw();
    });
  });
}

/** Programmatic layer switch (used by the layer tabs and the move-layer wizards). */
export function setLayer(layer: LayerKey): void {
  if (layer === S.currentLayer) return;
  S.currentLayer = layer;
  document.querySelectorAll(".layer-tab").forEach((b) =>
    b.classList.toggle("active", (b as HTMLElement).dataset.layer === layer)
  );
  refreshScopedSaveButton();
  applyPanelCollapse();
  clearSelection();
  clearFloorSelection();
  S.marqueeing = false;
  hideDetail();
  hideContextMenu();
  S.pendingNewFloor = false;
  S.pendingNewFloorCat = null;
  S.pendingNewAirFloor = false;
  dom.canvas.style.cursor = "";
  S.selectedWaypointId = null;
  S.expandedMemberId = null;
  // Leaving the move layer cancels any member/waypoint pick mode.
  if (layer !== "move") {
    S.moveMode = "none";
  }
  if (layer === "move") S.activeRightTab = "move";
  updatePanelTabButtons();
  syncVisibilityPopover();
  const searchEl = document.getElementById("palette-search") as HTMLInputElement;
  const sizeEl = document.getElementById("decor-size-filter") as HTMLSelectElement;
  if (sizeEl) sizeEl.classList.toggle("hidden", layer !== "decor");
  if (layer === "floor") {
    searchEl.placeholder = "搜索木筏 / 地板…";
    buildFloorPalette(searchEl.value, "floor");
  } else if (layer === "background") {
    searchEl.placeholder = "搜索背景 / 水面 / 环境…";
    buildFloorPalette(searchEl.value, "background");
  } else if (layer === "move") {
    searchEl.placeholder = "移动层：管理可移动物品组";
    dom.paletteCats.innerHTML = "";
  } else if (layer === "decor") {
    searchEl.placeholder = "搜索装饰…";
    buildPalette(layoutCatalog, searchEl.value);
  } else {
    searchEl.placeholder = "搜索核心物品…";
    buildPalette(layoutCatalog, searchEl.value);
  }
  draw();
}

export async function init() {
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
  warnIfBridgeOutdated(healthInfo, catalog.schemaVersion ?? 1);
  layoutCatalog = catalog;
  for (const it of catalog.items) S.catalogByGuid.set(it.guid, it);
  // id → 目录条目（跳过 custom_web/custom_ingredients 拷贝路径），供按 prefab id 回退解析。
  for (const it of catalog.items) {
    if (/\/custom_(web|ingredients)\//.test(it.assetPath)) continue;
    if (!S.catalogById.has(it.id)) S.catalogById.set(it.id, it);
  }
  S.ingredientsCache = await fetchIngredients().catch(() => []);
  S.intermediatesCache = await fetchRecipeCatalog("")
    .then((r) => r.filter((x) => x.intermediate || x.isCustom))
    .catch(() => []);
  S.counterAppearances = await fetchCounterAppearances().catch(() => null);
  S.switchMaterialsCache = await fetchSwitchMaterials().catch(() => []);
  buildPalette(catalog, "");

  const scenes = await fetchLevelSets().catch(() => []);
  S.sceneListCache = scenes;
  const setSelect = document.getElementById("set-select") as HTMLSelectElement;

  const setNames = [...new Set(scenes.map((s) => s.levelSet))].sort();
  setSelect.innerHTML = '<option value="">— 选择关卡集 —</option>';
  for (const name of setNames) {
    const opt = document.createElement("option");
    opt.value = name;
    opt.textContent = name;
    setSelect.appendChild(opt);
  }

  function populateSceneSelect(setName: string): void {
    const filtered = scenes.filter((s) => s.levelSet === setName);
    dom.sceneSelect.innerHTML = '<option value="">— 选择关卡 —</option>';
    for (const s of filtered) {
      const opt = document.createElement("option");
      opt.value = s.assetPath;
      opt.textContent = s.sceneName;
      dom.sceneSelect.appendChild(opt);
    }
  }

  setSelect.addEventListener("change", () => {
    const setName = setSelect.value;
    populateSceneSelect(setName);
    dom.sceneSelect.value = "";
  });

  dom.sceneSelect.addEventListener("change", () => {
    const target = dom.sceneSelect.value;
    if (!target || target === S.scenePath) return;
    confirmLeaveIfDirty(() => void loadScene(target));
  });

  document.getElementById("palette-search")!.addEventListener("input", (e) => {
    const q = (e.target as HTMLInputElement).value;
    if (S.currentLayer === "move") return;
    if (S.currentLayer === "floor") buildFloorPalette(q, "floor");
    else if (S.currentLayer === "background") buildFloorPalette(q, "background");
    else buildPalette(catalog, q);
  });

  document.getElementById("decor-size-filter")?.addEventListener("change", (e) => {
    const v = (e.target as HTMLSelectElement).value;
    S.decorSizeFilter = v as typeof S.decorSizeFilter;
    if (S.currentLayer === "decor") {
      buildPalette(catalog, (document.getElementById("palette-search") as HTMLInputElement).value);
    }
  });

  document.getElementById("btn-reload")!.addEventListener("click", () => {
    if (S.scenePath) confirmLeaveIfDirty(() => void loadScene(S.scenePath));
  });

  document.getElementById("btn-save")!.addEventListener("click", () => void saveToUnity(""));
  document.getElementById("btn-save-items")!.addEventListener("click", () => void saveToUnity(scopedSaveMeta().scope));
  document.getElementById("btn-deps-check")?.addEventListener("click", () => openDepsCheckModal());
  document.getElementById("btn-test-layout")?.addEventListener("click", () => requestTestLayout());
  document.getElementById("btn-repair-broken")?.addEventListener("click", async () => {    try {
      const n = await repairBrokenPrefabs(S.scenePath);
      if (n > 0) {
        setStatus(`已移除 ${n} 个损坏的预制件实例，请重新加载场景`, false);
        await loadScene(S.scenePath ?? "");
      } else {
        setStatus("未发现损坏的预制件实例", false);
      }
    } catch (e) {
      setStatus((e as Error).message, false);
    }
  });
  refreshScopedSaveButton();

  document.getElementById("btn-collapse-palette")!.addEventListener("click", () => {
    S.paletteCollapsed = !S.paletteCollapsed;
    localStorage.setItem("paletteCollapsed", S.paletteCollapsed ? "1" : "0");
    applyPanelCollapse();
    draw();
  });
  document.getElementById("btn-collapse-items")!.addEventListener("click", () => {
    S.itemsPanelCollapsed = !S.itemsPanelCollapsed;
    localStorage.setItem("itemsPanelCollapsed", S.itemsPanelCollapsed ? "1" : "0");
    applyPanelCollapse();
    draw();
  });
  applyPanelCollapse();

  document.getElementById("btn-recipes")!.addEventListener("click", () => void openRecipesDialog());
  document.getElementById("btn-utensils")!.addEventListener("click", () => openUtensilManager());
  document.getElementById("btn-camera-light")!.addEventListener("click", () => openCameraLightModal());
  document.getElementById("chk-auto-intermediates")!.addEventListener("change", (e) => {
    S.autoIntermediates = (e.target as HTMLInputElement).checked;
  });

  const withLevelDetail = async (fn: (detail: LevelDetail) => void | Promise<void>) => {
    if (!S.scenePath) {
      setStatus("请先选择场景", false);
      return;
    }
    try {
      showBusy("加载关卡信息…");
      const level = await fetchLevelRecipes(S.scenePath);
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
    void withLevelDetail((detail) => openConfigTabsModal(detail, S.currentLevelSet, () => {}))
  );
  document.getElementById("btn-level-audio")!.addEventListener("click", () =>
    void withLevelDetail((detail) => {
      const themes = new Set<string>();
      const itemIds = new Set<string>();
      for (const it of S.items) {
        const cat = S.catalogByGuid.get(it.prefabGuid);
        if (cat?.theme) themes.add(cat.theme);
        const id = cat?.id ?? prefabIdFromPath(it.prefabAssetPath);
        if (id) itemIds.add(id);
      }
      const raft = S.floors.some((f) => f.surfaceKind === "raft");
      const dt = S.deathInfo?.deathType;
      const deathTheme = dt === "water" ? "water" : dt === "goo" ? "goo" : "";
      openAudioModal(detail, { themes, raft, deathTheme, itemIds }, () => {});
    })
  );

  document.getElementById("btn-summary")!.addEventListener("click", () =>
    confirmLeaveIfDirty(() =>
      void withLevelDetail((detail) => renderLevelSummary(dom.app, S.currentLevelSet, detail.levelInfoAssetPath))
    )
  );

  document.getElementById("btn-sync")!.addEventListener("click", () => openSyncLayoutDialog());

  wireNav((target) => {
    if (target === "manage") confirmLeaveIfDirty(() => goManage());
    else if (target === "custom-recipes") {
      location.hash = "#/custom-recipes";
      location.reload();
    } else if (target === "recipes") {
      confirmLeaveIfDirty(() => {
        location.href = "/recipes";
      });
    }
  });

  window.addEventListener("beforeunload", (e) => {
    if (!S.dirty) return;
    e.preventDefault();
    e.returnValue = "";
  });

  document.getElementById("snap-grid")!.addEventListener("change", (e) => {
    S.snapEnabled = (e.target as HTMLInputElement).checked;
  });

  document.getElementById("snap-free-step")!.addEventListener("change", (e) => {
    S.freeSnapStep = parseFloat((e.target as HTMLSelectElement).value) || 0.01;
  });

  document.getElementById("show-grid")!.addEventListener("change", (e) => {
    S.showGrid = (e.target as HTMLInputElement).checked;
    draw();
  });

  document.getElementById("show-camera-fov")!.addEventListener("change", (e) => {
    S.showCameraFov = (e.target as HTMLInputElement).checked;
    localStorage.setItem("showCameraFov", S.showCameraFov ? "1" : "0");
    draw();
  });

  document.getElementById("show-coords")!.addEventListener("change", (e) => {
    S.showCoords = (e.target as HTMLInputElement).checked;
    if (!S.showCoords) {
      S.hoverCx = -1;
      S.hoverCy = -1;
    }
    draw();
  });

  document.getElementById("allow-ws-overlap")!.addEventListener("change", (e) => {
    S.allowWorkstationOverlap = (e.target as HTMLInputElement).checked;
  });

  document.querySelectorAll<HTMLButtonElement>(".layer-tab").forEach((btn) => {
    btn.addEventListener("click", () => {
      const layer = btn.dataset.layer as LayerKey;
      setLayer(layer);
    });
  });

  document.querySelectorAll<HTMLButtonElement>(".panel-tab").forEach((btn) => {
    btn.addEventListener("click", () => {
      const tab = btn.dataset.tab as "items" | "move" | "bevents";
      if (tab === S.activeRightTab) return;
      S.activeRightTab = tab;
      updatePanelTabButtons();
      S.activeMoveGroupId = null;
    S.activeMoveEventIdx = null;
      S.selectedWaypointId = null;
      renderRightPanel();
      draw();
    });
  });

  initPanelResizer();
  initPaletteResizer();
  wireVisibilityPopover();
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
    selectSceneInDropdowns(pick.assetPath);
    await loadScene(pick.assetPath);
  }

  startBridgeWatch();
}
