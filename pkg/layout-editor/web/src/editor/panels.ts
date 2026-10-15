import {
  S,
  EditorItem,
  EditorFloor,
  PX_PER_UNIT,
  type SceneListLayer,
} from "./state";
import { dom } from "./dom";
import {
  worldToCanvas,
  prefabIdFromPath,
  escHtml
} from "./coords";
import { itemLabel } from "./labels";
import { itemLayerOfIt, itemCategoryOf, catalogItemForGuidOrPath } from "./catalog";
import { isCollisionItem } from "./stubControls";
import {
  isSelected,
  setSelection,
  setFloorSelection,
  clearSelection
} from "./selection";
import { sceneNpcAnimItems, npcTypesHintHtml } from "./npcAnimations";
import { matchBuiltinAnimDecor, sceneEnvAnimDecorItems, envAnimTypesHintHtml } from "./builtinAnimDecor";
import { clampCanvasScale } from "./canvasZoom";
import { draw } from "./render";
import { renderAnimControlPanel, groupVisibleInLayer, updateAnimPickBar } from "./animControl";
import { renderLightPanel } from "./lightPanel";
import { renderTriggerSourcePanel } from "./triggerOrchestrator";
import { applyPaletteGridCols } from "./palette";
import { itemInCurrentPart, floorInCurrentPart } from "./partLevel";
import {
  coreGroupKey,
  decorGroupKey,
  floorGroupKey,
  hiddenSceneGroupsSig,
  hideAllGroupsOnLayer,
  isGroupHidden,
  showAllGroupsOnLayer,
  surfaceGroupKey,
  toggleGroupHidden,
} from "./layerGroupVisibility";

const SCENE_LIST_LAYERS: SceneListLayer[] = ["decor", "floor", "background", "items"];

function sceneListLayerOfCurrent(): SceneListLayer | null {
  const l = S.currentLayer;
  return SCENE_LIST_LAYERS.includes(l as SceneListLayer) ? (l as SceneListLayer) : null;
}

function layerEyeButtonHtml(layer: SceneListLayer, gk: string, hidden: boolean): string {
  const enc = encodeURIComponent(gk);
  return (
    `<button type="button" class="layer-eye${hidden ? " off" : ""}" data-scene-layer="${layer}" data-scene-group="${enc}" ` +
    `title="${hidden ? "显示该组" : "隐藏该组（画布不显示、不可点选）"}">${hidden ? "👁‍🗨" : "👁"}</button>`
  );
}

function layerGroupDetailsOpen(layer: SceneListLayer, gk: string, title: string, count: number): string {
  const hidden = isGroupHidden(layer, gk);
  return (
    `<details class="layer-group cat-group${hidden ? " layer-group-hidden" : ""}" open>` +
    `<summary class="layer-group-summary">` +
    layerEyeButtonHtml(layer, gk, hidden) +
    `<span class="layer-group-title">${escHtml(title)}</span>` +
    `<span class="layer-group-count">（${count}）</span>` +
    `</summary>`
  );
}

function groupFloorsByKey(floors: EditorFloor[]): Map<string, EditorFloor[]> {
  const map = new Map<string, EditorFloor[]>();
  for (const f of floors) {
    const gk = floorGroupKey(f);
    if (!map.has(gk)) map.set(gk, []);
    map.get(gk)!.push(f);
  }
  for (const list of map.values()) {
    list.sort((a, b) => a._key.localeCompare(b._key));
  }
  return map;
}

function groupSurfaceItemsByKey(items: EditorItem[]): Map<string, EditorItem[]> {
  const map = new Map<string, EditorItem[]>();
  for (const it of items) {
    const gk = surfaceGroupKey(it);
    if (!map.has(gk)) map.set(gk, []);
    map.get(gk)!.push(it);
  }
  for (const list of map.values()) {
    list.sort((a, b) => a._editorKey.localeCompare(b._editorKey));
  }
  return map;
}

function floorGroupTitle(gk: string, list: EditorFloor[]): string {
  const first = list[0];
  if (!first) return gk.replace(/^floor:/, "");
  const kind =
    first.surfaceKind === "raft"
      ? "木筏"
      : first.surfaceKind === "background"
        ? "背景板"
        : first.imageTexturePath
          ? "图片地板"
          : first.materialName ?? "地板";
  return first.displayName || kind;
}

function surfaceGroupTitle(gk: string): string {
  return gk.startsWith("surf:") ? gk.slice(5) : gk;
}

let layerVisBulkBound = false;

function updateLayerVisBulkBar(): void {
  const bar = document.getElementById("layer-vis-bulk");
  if (!bar) return;
  const layer = sceneListLayerOfCurrent();
  const show = layer !== null && S.activeRightTab === "items";
  bar.classList.toggle("hidden", !show);
}

export function initLayerVisBulkButtons(): void {
  if (layerVisBulkBound) return;
  layerVisBulkBound = true;
  document.getElementById("layer-vis-show-all")?.addEventListener("click", () => {
    const layer = sceneListLayerOfCurrent();
    if (!layer) return;
    showAllGroupsOnLayer(layer);
    S.sceneItemListSig = "";
    draw();
    refreshSceneItemList();
  });
  document.getElementById("layer-vis-hide-all")?.addEventListener("click", () => {
    const layer = sceneListLayerOfCurrent();
    if (!layer) return;
    hideAllGroupsOnLayer(layer);
    S.sceneItemListSig = "";
    draw();
    refreshSceneItemList();
  });
}

function wireSceneListGroupInteractions(body: HTMLElement): void {
  body.querySelectorAll<HTMLButtonElement>(".layer-eye[data-scene-layer]").forEach((btn) => {
    btn.addEventListener("click", (ev) => {
      ev.stopPropagation();
      ev.preventDefault();
      const layer = btn.dataset.sceneLayer as SceneListLayer;
      const gk = decodeURIComponent(btn.dataset.sceneGroup ?? "");
      if (!layer || !gk) return;
      toggleGroupHidden(layer, gk);
      S.sceneItemListSig = "";
      draw();
      refreshSceneItemList();
    });
  });

  body.querySelectorAll<HTMLElement>(".layer-group-summary").forEach((sum) => {
    sum.addEventListener("dblclick", (ev) => {
      if ((ev.target as HTMLElement).closest(".layer-eye")) return;
      const eye = sum.querySelector<HTMLButtonElement>(".layer-eye[data-scene-layer]");
      if (!eye) return;
      const layer = eye.dataset.sceneLayer as SceneListLayer;
      const gk = decodeURIComponent(eye.dataset.sceneGroup ?? "");
      if (!layer || !gk) return;

      if (layer === "decor") {
        const keys = S.items
          .filter((it) => itemLayerOfIt(it) === "decor" && itemInCurrentPart(it) && decorGroupKey(it) === gk)
          .map((it) => it._editorKey);
        if (keys.length === 0) return;
        setSelection(keys, keys[0]);
        const first = S.items.find((i) => i._editorKey === keys[0]);
        if (first) ensureItemVisible(first);
        draw();
        return;
      }

      if (layer === "items") {
        const keys = S.items
          .filter((it) => itemLayerOfIt(it) === "items" && itemInCurrentPart(it) && coreGroupKey(it) === gk)
          .map((it) => it._editorKey);
        if (keys.length === 0) return;
        setSelection(keys, keys[0]);
        const first = S.items.find((i) => i._editorKey === keys[0]);
        if (first) ensureItemVisible(first);
        draw();
        return;
      }

      const layerBg = layer === "background";
      if (gk.startsWith("floor:")) {
        const floorKeys = S.floors
          .filter(
            (f) =>
              floorInCurrentPart(f) &&
              (layerBg ? f.surfaceKind === "background" : f.surfaceKind !== "background") &&
              floorGroupKey(f) === gk
          )
          .map((f) => f._key);
        if (floorKeys.length === 0) return;
        setFloorSelection(floorKeys);
        clearSelection();
        const first = S.floors.find((f) => f._key === floorKeys[0]);
        if (first) ensureItemVisible({ _wx: first._wx, _wz: first._wz } as EditorItem);
        draw();
        return;
      }

      if (gk.startsWith("surf:")) {
        const keys = S.items
          .filter((it) => {
            if (!itemInCurrentPart(it) || isCollisionItem(it)) return false;
            const cat = itemCategoryOf(it);
            const onLayer = layerBg ? cat === "background" : cat === "floors";
            return onLayer && surfaceGroupKey(it) === gk;
          })
          .map((it) => it._editorKey);
        if (keys.length === 0) return;
        setSelection(keys, keys[0]);
        const first = S.items.find((i) => i._editorKey === keys[0]);
        if (first) ensureItemVisible(first);
        draw();
      }
    });
  });
}

/** Drag the left palette's right edge to resize it (min 180px, max half the window). */
export function initPaletteResizer(): void {
  const resizer = document.getElementById("palette-resizer");
  const panel = document.getElementById("palette-panel");
  if (!resizer || !panel) return;
  try {
    const saved = parseFloat(localStorage.getItem("paletteWidth") || "0");
    if (saved > 0) {
      panel.style.width = saved + "px";
      applyPaletteGridCols();
    }
  } catch {
    /* ignore */
  }
  resizer.addEventListener("mousedown", (e) => {
    e.preventDefault();
    const startX = e.clientX;
    const startW = panel.getBoundingClientRect().width;
    const onMove = (ev: MouseEvent) => {
      const w = Math.min(window.innerWidth / 2, Math.max(180, startW + ev.clientX - startX));
      panel.style.width = w + "px";
      applyPaletteGridCols();
      try {
        localStorage.setItem("paletteWidth", String(w));
      } catch {
        /* ignore */
      }
    };
    const onUp = () => {
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", onUp);
    };
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);
  });
}

/** Drag the right items panel's left edge to resize it (max half the window). */
export function initPanelResizer(): void {
  const resizer = document.getElementById("panel-resizer");
  const panel = document.getElementById("items-panel");
  if (!resizer || !panel) return;
  try {
    const saved = parseFloat(localStorage.getItem("itemsPanelWidth") || "0");
    if (saved > 0) panel.style.width = saved + "px";
  } catch {
    /* ignore */
  }
  resizer.addEventListener("mousedown", (e) => {
    e.preventDefault();
    const startX = e.clientX;
    const startW = panel.getBoundingClientRect().width;
    const onMove = (ev: MouseEvent) => {
      const w = Math.min(window.innerWidth / 2, Math.max(260, startW + startX - ev.clientX));
      panel.style.width = w + "px";
      try {
        localStorage.setItem("itemsPanelWidth", String(w));
      } catch {
        /* ignore */
      }
    };
    const onUp = () => {
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", onUp);
    };
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);
  });
}

export function applyPanelCollapse(): void {  const palette = document.getElementById("palette-panel");
  const itemsPanel = document.getElementById("items-panel");
  const btnPalette = document.getElementById("btn-collapse-palette");
  const btnItems = document.getElementById("btn-collapse-items");
  const paletteResizer = document.getElementById("palette-resizer");
  if (palette) palette.classList.toggle("hidden", S.paletteCollapsed);
  if (paletteResizer) paletteResizer.classList.toggle("hidden", S.paletteCollapsed);
  if (btnPalette) btnPalette.textContent = S.paletteCollapsed ? "▶" : "◀";
  // Right panel visible on every layer (per-layer item list / move control).
  const onPanel = true;
  if (itemsPanel) itemsPanel.classList.toggle("hidden", !onPanel || S.itemsPanelCollapsed);
  if (btnItems) {
    btnItems.classList.toggle("hidden", !onPanel);
    btnItems.textContent = S.itemsPanelCollapsed ? "◀" : "▶";
  }
  // On the move layer there is no per-layer item list: force the "move" tab.
  if (S.currentLayer === "anim" && S.activeRightTab === "items") {
    S.activeRightTab = "anim";
    updatePanelTabButtons();
  }
  S.sceneItemListSig = ""; // force rebuild on expand/layer change
}

export function updatePanelTabButtons(): void {
  document.querySelectorAll(".panel-tab").forEach((b) => {
    const el = b as HTMLElement;
    const tab = el.dataset.tab;
    el.classList.toggle("active", tab === S.activeRightTab);
    el.style.display = "";
  });
}

export function ensureItemVisible(item: EditorItem): void {
  const w = dom.canvas.clientWidth;
  const h = dom.canvas.clientHeight;
  if (dom.canvas.width !== w || dom.canvas.height !== h) {
    dom.canvas.width = w;
    dom.canvas.height = h;
  }
  const p = worldToCanvas(item._wx, item._wz);
  const margin = 60;
  if (p.x >= margin && p.x <= w - margin && p.y >= margin && p.y <= h - margin) return;
  const k = PX_PER_UNIT * S.scale;
  S.panX = -item._wx * k;
  S.panY = item._wz * k;
}

/** 将 2D 视野对准世界矩形（必要时放大），用于重叠告警等定位。 */
export function focusWorldBounds(
  minX: number,
  maxX: number,
  minZ: number,
  maxZ: number,
  paddingM = 1.2
): void {
  // 与 draw() 一致：先对齐 backing store，避免 pan 按 client 算、worldToCanvas 按 width 算而偏到角落。
  const w = dom.canvas.clientWidth;
  const h = dom.canvas.clientHeight;
  if (dom.canvas.width !== w || dom.canvas.height !== h) {
    dom.canvas.width = w;
    dom.canvas.height = h;
  }

  const cx = (minX + maxX) / 2;
  const cz = (minZ + maxZ) / 2;
  const span = Math.max(maxX - minX, maxZ - minZ, 3) + paddingM * 2;
  const margin = 72;
  const vw = Math.max(120, w - margin * 2);
  const vh = Math.max(120, h - margin * 2);
  const fitScale = Math.min(vw / (span * PX_PER_UNIT), vh / (span * PX_PER_UNIT));
  if (fitScale < S.scale) S.scale = clampCanvasScale(fitScale);

  // worldToCanvas：cx = w/2 + panX + wx·K，cy = h/2 + panY - wz·K → 居中时 panX = -wx·K，panY = wz·K
  const k = PX_PER_UNIT * S.scale;
  S.panX = -cx * k;
  S.panY = cz * k;
}

export function refreshSceneItemList(): void {
  const body = document.getElementById("scene-items-body");
  const countEl = document.getElementById("scene-items-count");
  if (!body) return;

  const layer = S.currentLayer;
  const layerItems = S.items.filter(
    (it) => itemLayerOfIt(it) === layer && itemInCurrentPart(it)
  );
  if (countEl) countEl.textContent = `（${layerItems.length}）`;

  if (layer === "floor" || layer === "background") {
    // Floor-like layers: list the floors themselves + surface items of the layer.
    const layerBg = layer === "background";
    const floors = S.floors.filter((f) =>
      floorInCurrentPart(f) &&
      (layerBg ? f.surfaceKind === "background" : f.surfaceKind !== "background")
    );
    const surfaceItems = S.items.filter((it) => {
      if (!itemInCurrentPart(it)) return false;
      if (isCollisionItem(it)) return false;
      const cat = itemCategoryOf(it);
      return layerBg ? cat === "background" : cat === "floors";
    });
    if (countEl) countEl.textContent = `（${floors.length} 地板 · ${surfaceItems.length} 表面物）`;

    const sceneLayer: SceneListLayer = layer;
    const parts: string[] = [];
    const floorGroups = groupFloorsByKey(floors);
    const sortedFloorKeys = [...floorGroups.keys()].sort((a, b) =>
      floorGroupTitle(a, floorGroups.get(a)!).localeCompare(floorGroupTitle(b, floorGroups.get(b)!), "zh")
    );
    for (const gk of sortedFloorKeys) {
      const list = floorGroups.get(gk)!;
      parts.push(layerGroupDetailsOpen(sceneLayer, gk, floorGroupTitle(gk, list), list.length));
      for (const f of list) {
        const kind =
          f.surfaceKind === "raft"
            ? "木筏"
            : f.surfaceKind === "background"
              ? "背景板"
              : f.imageTexturePath
                ? "图片地板"
                : f.materialName ?? "地板";
        parts.push(
          `<div class="scene-item-row layer-child-row${S.selectedFloorKeys.has(f._key) ? " active" : ""}" data-floor-key="${f._key}">` +
            `<span class="zh">${escHtml(f.displayName || kind)}</span> <span class="id">${escHtml(kind)}</span>` +
            `</div>`
        );
      }
      parts.push("</details>");
    }
    const surfGroups = groupSurfaceItemsByKey(surfaceItems);
    const sortedSurfKeys = [...surfGroups.keys()].sort((a, b) =>
      surfaceGroupTitle(a).localeCompare(surfaceGroupTitle(b), "zh")
    );
    for (const gk of sortedSurfKeys) {
      const list = surfGroups.get(gk)!;
      parts.push(layerGroupDetailsOpen(sceneLayer, gk, surfaceGroupTitle(gk), list.length));
      for (const it of list) {
        parts.push(
          `<div class="scene-item-row layer-child-row${isSelected(it._editorKey) ? " active" : ""}" data-key="${it._editorKey}">` +
            `<span class="zh">${escHtml(itemLabel(it))}</span> <span class="id">${escHtml(prefabIdFromPath(it.prefabAssetPath) || "—")}</span>` +
            `<button type="button" class="scene-item-samename" data-samename="${it._editorKey}" title="选中所有同名物品">⌂</button>` +
            `</div>`
        );
      }
      parts.push("</details>");
    }
    if (parts.length === 0) {
      parts.push(`<div class="muted" style="padding:10px;">本层暂无${layerBg ? "背景内容（水面 / 天空 / 环境）" : "地板"}。可在左侧调色板添加。</div>`);
    }
    body.innerHTML = parts.join("");

    body.querySelectorAll<HTMLElement>("[data-floor-key]").forEach((row) => {
      row.addEventListener("click", () => {
        const key = row.dataset.floorKey!;
        const f = S.floors.find((x) => x._key === key);
        if (!f) return;
        setFloorSelection([key]);
        clearSelection();
        ensureItemVisible({ _wx: f._wx, _wz: f._wz } as EditorItem);
        draw();
      });
    });
  } else if (layer === "decor") {
    const parts: string[] = [];
    const npcs = sceneNpcAnimItems();
    const npcKeys = new Set(npcs.map((it) => it._editorKey));
    parts.push(
      `<details class="cat-group" open><summary>🎞 自带行走动画的 NPC（${npcs.length}）</summary>` +
        (npcs.length > 0
          ? npcs
              .map(
                (it) =>
                  `<div class="scene-item-row" data-key="${it._editorKey}">` +
                  `<span class="zh">${escHtml(itemLabel(it))}</span> <span class="id">🎞</span>` +
                  `</div>`
              )
              .join("")
          : `<div class="npc-types-empty">场景中暂无自带动画的 NPC。${npcTypesHintHtml()}</div>`) +
        `</details>`
    );
    const envAnims = sceneEnvAnimDecorItems();
    const envKeys = new Set(envAnims.map((it) => it._editorKey));
    parts.push(
      `<details class="cat-group" open><summary>✨ 自带环境动画的装饰（${envAnims.length}）</summary>` +
        (envAnims.length > 0
          ? envAnims
              .map((it) => {
                const id = prefabIdFromPath(it.prefabAssetPath) || it.instanceId;
                const match = matchBuiltinAnimDecor(id, it.displayName);
                const badge = match?.emoji ?? "✨";
                return (
                  `<div class="scene-item-row" data-key="${it._editorKey}">` +
                  `<span class="zh">${escHtml(itemLabel(it))}</span> <span class="id">${badge}</span>` +
                  `</div>`
                );
              })
              .join("")
          : `<div class="npc-types-empty">场景中暂无环境动画装饰。${envAnimTypesHintHtml()}</div>`) +
        `</details>`
    );

    const nameGroups = new Map<string, EditorItem[]>();
    for (const it of layerItems) {
      if (npcKeys.has(it._editorKey) || envKeys.has(it._editorKey)) continue;
      const gk = decorGroupKey(it);
      if (!nameGroups.has(gk)) nameGroups.set(gk, []);
      nameGroups.get(gk)!.push(it);
    }
    const sortedGroupNames = [...nameGroups.keys()].sort((a, b) => a.localeCompare(b, "zh"));
    for (const gk of sortedGroupNames) {
      const list = nameGroups.get(gk)!;
      list.sort((a, b) => a._editorKey.localeCompare(b._editorKey));
      parts.push(layerGroupDetailsOpen("decor", gk, gk, list.length));
      for (const it of list) {
        const id = prefabIdFromPath(it.prefabAssetPath) || "—";
        parts.push(
          `<div class="scene-item-row layer-child-row${isSelected(it._editorKey) ? " active" : ""}" data-key="${it._editorKey}">` +
            `<span class="zh">${escHtml(itemLabel(it))}</span> <span class="id">${escHtml(id)}</span>` +
            `<button type="button" class="scene-item-samename" data-samename="${it._editorKey}" title="选中所有同名物品">⌂</button>` +
            `</div>`
        );
      }
      parts.push("</details>");
    }
    if (sortedGroupNames.length === 0 && npcs.length === 0 && envAnims.length === 0) {
      parts.push(`<div class="muted" style="padding:10px;">本层暂无装饰物。可在左侧调色板添加。</div>`);
    }
    body.innerHTML = parts.join("");
  } else {
    // 核心层等：按目录 category 分组。
    const groups = new Map<string, EditorItem[]>();
    for (const it of layerItems) {
      const cat = catalogItemForGuidOrPath(it.prefabGuid, it.prefabAssetPath);
      const key = cat?.category && S.corePaletteGroupMeta.has(cat.category) ? cat.category : "__other";
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key)!.push(it);
    }
    const orderedKeys = [...S.corePaletteGroupMeta.keys()].filter((k) => groups.has(k));
    for (const key of groups.keys()) {
      if (key !== "__other" && !orderedKeys.includes(key)) orderedKeys.push(key);
    }
    if (groups.has("__other")) orderedKeys.push("__other");

    const parts: string[] = [];
    for (const key of orderedKeys) {
      const list = groups.get(key)!;
      const label = S.corePaletteGroupMeta.get(key) ?? (key === "__other" ? "其他" : key);
      parts.push(layerGroupDetailsOpen("items", key, label, list.length));
      for (const it of list) {
        const id = prefabIdFromPath(it.prefabAssetPath) || "—";
        parts.push(
          `<div class="scene-item-row layer-child-row${isSelected(it._editorKey) ? " active" : ""}" data-key="${it._editorKey}">` +
            `<span class="zh">${escHtml(itemLabel(it))}</span> <span class="id">${escHtml(id)}</span>` +
            `<button type="button" class="scene-item-samename" data-samename="${it._editorKey}" title="选中所有同名物品">⌂</button>` +
            `</div>`
        );
      }
      parts.push("</details>");
    }
    body.innerHTML = parts.join("");
  }

  body.querySelectorAll<HTMLElement>(".scene-item-row[data-key]").forEach((row) => {
    row.addEventListener("click", () => {
      const key = row.dataset.key!;
      const it = S.items.find((i) => i._editorKey === key);
      if (!it) return;
      setSelection([key]);
      ensureItemVisible(it);
      draw();
    });
  });

  body.querySelectorAll<HTMLElement>(".scene-item-samename[data-samename]").forEach((btn) => {
    btn.addEventListener("click", (ev) => {
      ev.stopPropagation();
      const key = btn.dataset.samename!;
      const base = S.items.find((i) => i._editorKey === key);
      if (!base) return;
      const name = itemLabel(base);
      const matches = S.items.filter((i) => itemLabel(i) === name);
      const keys = matches.map((i) => i._editorKey);
      setSelection(keys, key);
      ensureItemVisible(base);
      draw();
    });
  });

  wireSceneListGroupInteractions(body);
  updateLayerVisBulkBar();
}

export function maybeRefreshSceneItemList(): void {
  const visSig = hiddenSceneGroupsSig();
  const sig = `${S.currentLayer}|${S.activeRightTab}|${S.activeAnimGroupId}|${S.activeAnimEventIdx}|${S.animMode}|${S.items.length}|${S.selectedKeys.size}|${S.selectedFloorKeys.size}|${S.selectedKey}|${S.selectedLightPath}|${S.lights.length}|${S.animControls.length}|${S.buttonEvents.length}|${S.buttonLinks.length}|${S.switchLinks.length}|${S.dirty}|${visSig}`;
  if (sig === S.sceneItemListSig) return;
  S.sceneItemListSig = sig;
  renderRightPanel();
}

export function renderRightPanel(): void {
  const body = document.getElementById("scene-items-body");
  if (!body) return;
  updateAnimPickBar();

  const countEl = document.getElementById("scene-items-count");
  const animCountEl = document.getElementById("anim-control-count");

  if (S.currentLayer === "lights") {
    renderLightPanel(body);
    if (countEl) countEl.textContent = S.lights.length > 0 ? `(${S.lights.length})` : "";
    if (animCountEl) animCountEl.textContent = "";
    return;
  }

  if (S.currentLayer === "anim") {
    // Move layer: the move panel is always shown (dedicated management layer).
    S.activeRightTab = "anim";
    updatePanelTabButtons();
    renderAnimControlPanel(body);
    if (countEl) countEl.textContent = "";
    const layerGroups = S.animControls.filter((g) => groupVisibleInLayer(g));
    if (animCountEl) animCountEl.textContent = layerGroups.length > 0 ? `(${layerGroups.length})` : "";
    return;
  }

  if (
    (S.currentLayer === "floor" || S.currentLayer === "background") &&
    S.activeAnimGroupId
  ) {
    // Floor/background layer with an active move group: the move panel takes over.
    S.activeRightTab = "anim";
    updatePanelTabButtons();
    renderAnimControlPanel(body);
    if (countEl) countEl.textContent = "";
    const layerGroups = S.animControls.filter((g) => groupVisibleInLayer(g));
    if (animCountEl) animCountEl.textContent = layerGroups.length > 0 ? `(${layerGroups.length})` : "";
    return;
  }

  if (S.activeRightTab === "anim") {
    renderAnimControlPanel(body);
    if (countEl) countEl.textContent = "";
    const layerGroups = S.animControls.filter((g) => groupVisibleInLayer(g));
    if (animCountEl) animCountEl.textContent = layerGroups.length > 0 ? `(${layerGroups.length})` : "";
  } else if (S.activeRightTab === "triggers") {
    renderTriggerSourcePanel(body);
    if (countEl) countEl.textContent = "";
    if (animCountEl) animCountEl.textContent = "";
  } else {
    refreshSceneItemList();
    if (animCountEl) animCountEl.textContent = "";
  }
}
