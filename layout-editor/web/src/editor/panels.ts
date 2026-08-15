import {
  S,
  EditorItem,
  PX_PER_UNIT
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
import { sceneNpcAnimItems, NPC_ANIM_TYPES } from "./npcAnimations";
import { draw } from "./render";
import { renderMoveControlPanel, groupVisibleInLayer, updateMovePickBar } from "./moveControl";
import { applyPaletteGridCols } from "./palette";

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
  if (S.currentLayer === "move" && S.activeRightTab === "items") {
    S.activeRightTab = "move";
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
  const p = worldToCanvas(item._wx, item._wz);
  const margin = 60;
  if (p.x >= margin && p.x <= dom.canvas.width - margin && p.y >= margin && p.y <= dom.canvas.height - margin) return;
  S.panX = dom.canvas.width / 2 - item._wx * PX_PER_UNIT * S.scale;
  S.panY = dom.canvas.height / 2 + item._wz * PX_PER_UNIT * S.scale;
}

export function refreshSceneItemList(): void {
  const body = document.getElementById("scene-items-body");
  const countEl = document.getElementById("scene-items-count");
  if (!body) return;

  const layer = S.currentLayer;
  const layerItems = S.items.filter(
    (it) => itemLayerOfIt(it) === layer
  );
  if (countEl) countEl.textContent = `（${layerItems.length}）`;

  if (layer === "floor" || layer === "background") {
    // Floor-like layers: list the floors themselves + surface items of the layer.
    const layerBg = layer === "background";
    const floors = S.floors.filter((f) =>
      layerBg ? f.surfaceKind === "background" : f.surfaceKind !== "background"
    );
    const surfaceItems = S.items.filter((it) => {
      if (isCollisionItem(it)) return false;
      const cat = itemCategoryOf(it);
      return layerBg ? cat === "background" : cat === "floors";
    });
    if (countEl) countEl.textContent = `（${floors.length} 地板 · ${surfaceItems.length} 表面物）`;

    const parts: string[] = [];
    if (floors.length > 0) {
      parts.push(`<details class="cat-group" open><summary>${layerBg ? "🌊 背景板" : "🗺️ 地板"}（${floors.length}）</summary>`);
      for (const f of floors) {
        const kind =
          f.surfaceKind === "raft"
            ? "木筏"
            : f.surfaceKind === "background"
              ? "背景板"
              : f.imageTexturePath
                ? "图片地板"
                : f.materialName ?? "地板";
        parts.push(
          `<div class="scene-item-row${S.selectedFloorKeys.has(f._key) ? " active" : ""}" data-floor-key="${f._key}">` +
            `<span class="zh">${escHtml(f.displayName || kind)}</span> <span class="id">${escHtml(kind)}</span>` +
            `</div>`
        );
      }
      parts.push("</details>");
    }
    if (surfaceItems.length > 0) {
      parts.push(`<details class="cat-group" open><summary>${layerBg ? "🌊 背景 / 环境特效" : "🧱 表面物品"}（${surfaceItems.length}）</summary>`);
      for (const it of surfaceItems) {
        parts.push(
          `<div class="scene-item-row${isSelected(it._editorKey) ? " active" : ""}" data-key="${it._editorKey}">` +
            `<span class="zh">${escHtml(itemLabel(it))}</span> <span class="id">${escHtml(prefabIdFromPath(it.prefabAssetPath) || "—")}</span>` +
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
  } else {
    // items / decor / move: group by catalog category, per-layer.
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
    if (layer === "decor") {
      // 自带移动动画的 NPC 清单
      const npcs = sceneNpcAnimItems();
      const known = NPC_ANIM_TYPES.map((t) => `${t.labelZh}（${t.evidence}）`).join("；");
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
            : `<div class="muted" style="padding:6px 4px;">场景中暂无自带动画的 NPC。已知类型：${escHtml(known)}。服务生（NPC_*_Waiter_01）为目录内可直接放置的自带动画 NPC；月亮 / 面包人 / 行人为关卡专属物体。</div>`) +
          `</details>`
      );
    }
    for (const key of orderedKeys) {
      const list = groups.get(key)!;
      const label = S.corePaletteGroupMeta.get(key) ?? (key === "__other" ? "其他" : key);
      parts.push(`<details class="cat-group" open><summary>${escHtml(label)}（${list.length}）</summary>`);
      for (const it of list) {
        const id = prefabIdFromPath(it.prefabAssetPath) || "—";
        parts.push(
          `<div class="scene-item-row${isSelected(it._editorKey) ? " active" : ""}" data-key="${it._editorKey}">` +
            `<span class="zh">${escHtml(itemLabel(it))}</span> <span class="id">${escHtml(id)}</span>` +
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
}

export function maybeRefreshSceneItemList(): void {
  const sig = `${S.currentLayer}|${S.activeRightTab}|${S.activeMoveGroupId}|${S.activeMoveEventIdx}|${S.moveMode}|${S.items.length}|${S.selectedKeys.size}|${S.selectedFloorKeys.size}|${S.selectedKey}|${S.moveControls.length}|${S.dirty}`;
  if (sig === S.sceneItemListSig) return;
  S.sceneItemListSig = sig;
  renderRightPanel();
}

export function renderRightPanel(): void {
  const body = document.getElementById("scene-items-body");
  if (!body) return;
  updateMovePickBar();

  const countEl = document.getElementById("scene-items-count");
  const moveCountEl = document.getElementById("move-control-count");

  if (S.currentLayer === "move") {
    // Move layer: the move panel is always shown (dedicated management layer).
    S.activeRightTab = "move";
    updatePanelTabButtons();
    renderMoveControlPanel(body);
    if (countEl) countEl.textContent = "";
    const layerGroups = S.moveControls.filter((g) => groupVisibleInLayer(g));
    if (moveCountEl) moveCountEl.textContent = layerGroups.length > 0 ? `(${layerGroups.length})` : "";
    return;
  }

  if (
    (S.currentLayer === "floor" || S.currentLayer === "background") &&
    S.activeMoveGroupId
  ) {
    // Floor/background layer with an active move group: the move panel takes over.
    S.activeRightTab = "move";
    updatePanelTabButtons();
    renderMoveControlPanel(body);
    if (countEl) countEl.textContent = "";
    const layerGroups = S.moveControls.filter((g) => groupVisibleInLayer(g));
    if (moveCountEl) moveCountEl.textContent = layerGroups.length > 0 ? `(${layerGroups.length})` : "";
    return;
  }

  if (S.activeRightTab === "move") {
    renderMoveControlPanel(body);
    if (countEl) countEl.textContent = "";
    const layerGroups = S.moveControls.filter((g) => groupVisibleInLayer(g));
    if (moveCountEl) moveCountEl.textContent = layerGroups.length > 0 ? `(${layerGroups.length})` : "";
  } else {
    refreshSceneItemList();
    if (moveCountEl) moveCountEl.textContent = "";
  }
}
