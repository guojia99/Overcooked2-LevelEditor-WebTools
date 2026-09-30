import { S } from "./state";
import type { CatalogItem } from "../types";
import { dom } from "./dom";
import { escHtml, normalizeRot } from "./coords";
import { setStatus } from "./status";
import { draw } from "./render";
import { batchRandomRotateLotusPressureSwitches } from "./items";
import { isThemedFloor, themedFloorPrefabs, effectiveMaterialTiling } from "./floors";
import { itemLabel } from "./labels";
import {
  commonBackgroundPrefabIds,
  deathLabelZh,
  isSurfaceItem,
  surfaceKindLabelZh,
  themeWaterPrefabId,
} from "../floorColors";
import { tidyCatalogNameZh } from "../displayLabels";
import { defaultBackgroundPlaneCells, isAmbientBackgroundCat, isWaterBackgroundCat } from "./catalog";
import { floorLayerSummary } from "./floorHeight";
import {
  applyPaletteGridCols,
  bumpPalettePick,
  palettePlaceCountFor,
  clearPalettePick,
  refreshPaletteQtyBadges,
} from "./palette";

/** 面板高度过滤：按 prefab 固有模型高度（catalog 的 height 字段，未实测按 0）
 *  是否落在当前高度区间内。未激活（全部）时恒通过。 */
export function matchesFloorHeightFilter(it: CatalogItem): boolean {
  if (S.floorHeight.min == null || S.floorHeight.max == null) return true;
  const h = it.height ?? 0;
  return h >= S.floorHeight.min - 1e-6 && h <= S.floorHeight.max + 1e-6;
}

/** 同步滑块位置与数值标签到 S.floorHeight（null = 全部 = 整个滑块域）。 */
export function syncFloorHeightSliderUI(): void {
  const minEl = document.getElementById("fhf-min") as HTMLInputElement | null;
  const maxEl = document.getElementById("fhf-max") as HTMLInputElement | null;
  const minVal = document.getElementById("fhf-min-val");
  const maxVal = document.getElementById("fhf-max-val");
  if (!minEl || !maxEl) return;
  const lo = S.floorHeight.min ?? parseFloat(minEl.min);
  const hi = S.floorHeight.max ?? parseFloat(maxEl.max);
  minEl.value = String(lo);
  maxEl.value = String(hi);
  if (minVal) minVal.textContent = lo.toFixed(2);
  if (maxVal) maxVal.textContent = hi.toFixed(2);
}

/** 渲染高度层列表（全部 + L0…LN，含每层地板/物品计数），点击 = 设为该层区间。 */
export function renderFloorHeightLayers(): void {
  const box = document.getElementById("fhf-layers");
  if (!box) return;
  box.innerHTML = "";
  const isAll = S.floorHeight.min == null || S.floorHeight.max == null;
  const byItems = S.currentLayer === "items" || S.currentLayer === "decor";
  const addBtn = (label: string, active: boolean, title: string, onClick: () => void) => {
    const b = document.createElement("button");
    b.type = "button";
    b.className = "fhf-layer" + (active ? " active" : "");
    b.textContent = label;
    b.title = title;
    b.addEventListener("click", onClick);
    box.appendChild(b);
  };
  const applyRange = (min: number | null, max: number | null) => {
    S.floorHeight.min = min;
    S.floorHeight.max = max;
    refreshAfterHeightFilterChange();
  };
  addBtn(`全部高度`, isAll, "显示所有高度", () => applyRange(null, null));
  for (const l of floorLayerSummary()) {
    const active =
      !isAll &&
      Math.abs((S.floorHeight.min as number) - l.lo) < 1e-6 &&
      Math.abs((S.floorHeight.max as number) - l.hi) < 1e-6;
    const n = byItems ? `${l.itemCount}件` : `${l.count}块`;
    addBtn(
      `L${l.index} · ${l.lo.toFixed(2)}~${l.hi.toFixed(2)} · ${n}`,
      active,
      `只显示高度在 ${l.lo.toFixed(2)}~${l.hi.toFixed(2)} 的${byItems ? "物品" : "地板"}`,
      () => applyRange(l.lo, l.hi)
    );
  }
}

export function refreshFloorHeightPanel(): void {
  renderFloorHeightLayers();
  syncFloorHeightSliderUI();
}

export function refreshAfterHeightFilterChange(): void {
  refreshFloorHeightPanel();
  if (S.currentLayer === "floor") {
    const q = (document.getElementById("palette-search") as HTMLInputElement | null)?.value ?? "";
    buildFloorPalette(q, "floor");
  }
  draw();
}

const PRESSURE_SWITCH_SURFACE_IDS = new Set([
  "PressureSwitch",
  "dlc13_lotuspressureswitch_large",
  "dlc13_lotuspressureswitch_small",
]);

function isSandAlienBackground(it: CatalogItem): boolean {
  const id = it.id.toLowerCase();
  const path = (it.assetPath ?? "").toLowerCase();
  return (
    /sand|alien|goo|gue/.test(id) ||
    /sand|alien|goo/.test(path) ||
    it.surfaceKind === "sand" ||
    it.surfaceKind === "alien"
  );
}

function isSkyBackground(it: CatalogItem): boolean {
  const id = it.id.toLowerCase();
  const path = (it.assetPath ?? "").toLowerCase();
  const zh = `${it.nameZh} ${it.nameEn ?? ""}`;
  return id.includes("sky") || path.includes("/sky") || /天空|远景/.test(zh);
}

function backgroundPaletteGroups(pool: CatalogItem[]): { key: string; labelZh: string; items: CatalogItem[] }[] {
  const assigned = new Set<string>();
  const take = (list: CatalogItem[]) => {
    const out: CatalogItem[] = [];
    for (const it of list) {
      if (assigned.has(it.guid)) continue;
      assigned.add(it.guid);
      out.push(it);
    }
    return out;
  };

  const commonIds = new Set(commonBackgroundPrefabIds(S.currentLevelSet));
  const dlcWater = themeWaterPrefabId(S.currentLevelSet);
  const common = take(
    pool.filter((it) => commonIds.has(it.id)).sort((a, b) => {
      if (a.id === dlcWater) return -1;
      if (b.id === dlcWater) return 1;
      return a.id.localeCompare(b.id);
    })
  );

  const water = take(pool.filter((it) => isWaterBackgroundCat(it)));
  const sky = take(
    pool.filter(
      (it) =>
        it.surfaceTier === "background" &&
        isSkyBackground(it) &&
        !isWaterBackgroundCat(it) &&
        !isAmbientBackgroundCat(it)
    )
  );
  const sand = take(
    pool.filter(
      (it) =>
        (it.surfaceTier === "background" || isSandAlienBackground(it)) &&
        isSandAlienBackground(it) &&
        !isWaterBackgroundCat(it) &&
        !isAmbientBackgroundCat(it)
    )
  );
  const ambient = take(pool.filter((it) => isAmbientBackgroundCat(it)));
  const other = take(
    pool.filter(
      (it) =>
        it.surfaceTier === "background" ||
        isWaterBackgroundCat(it) ||
        isAmbientBackgroundCat(it) ||
        looksLikeLooseBackground(it)
    )
  );

  const groups: { key: string; labelZh: string; items: CatalogItem[] }[] = [];
  if (common.length) groups.push({ key: "common", labelZh: "⭐ 常用背景", items: common });
  if (water.length) groups.push({ key: "water", labelZh: "💧 水面 / 海洋", items: water });
  if (sky.length) groups.push({ key: "sky", labelZh: "☁️ 天空 / 远景", items: sky });
  if (sand.length) groups.push({ key: "sand", labelZh: "🏜️ 沙地 / 外星", items: sand });
  if (ambient.length) groups.push({ key: "ambient", labelZh: "🌨️ 环境特效", items: ambient });
  if (other.length) groups.push({ key: "other", labelZh: "🌊 其他背景", items: other });
  return groups;
}

function looksLikeLooseBackground(it: CatalogItem): boolean {
  if (it.surfaceTier === "background") return true;
  const path = (it.assetPath ?? "").toLowerCase();
  return path.includes("/background/") || path.includes("/environment/");
}

function cardSubId(it: CatalogItem): string {
  const en = it.nameEn ?? "";
  const norm = (s: string) => s.toLowerCase().replace(/[\s_\-]+/g, "");
  if (en && norm(en) !== norm(it.id)) return `${escHtml(en)} · ${escHtml(it.id)}`;
  return escHtml(it.id);
}

function appendBackgroundCardGrid(parent: HTMLElement, list: CatalogItem[]) {
  const grid = document.createElement("div");
  grid.className = "palette-grid";
  const dlcWater = themeWaterPrefabId(S.currentLevelSet);

  for (const it of list) {
    const row = document.createElement("div");
    row.className = "palette-card palette-bg palette-cat-background";
    row.draggable = true;
    row.dataset.guid = it.guid;
    const { wCells, dCells } = defaultBackgroundPlaneCells(it);
    const sizeBadge =
      wCells !== 6 || dCells !== 6 ? `<div class="sub">默认 ${wCells}×${dCells} 格</div>` : "";
    const recBadge =
      it.id === dlcWater
        ? `<div class="sub">推荐 · 本关 DLC</div>`
        : it.height != null && it.height > 0.01
          ? `<div class="sub fhf-badge">h=${it.height.toFixed(2)}</div>`
          : "";
    row.innerHTML =
      `<div class="zh">${tidyCatalogNameZh(it.nameZh, it.id)}</div>` +
      `<div class="id">${cardSubId(it)}</div>${sizeBadge}${recBadge}`;

    let skipClick = false;
    row.addEventListener("dragstart", (e) => {
      skipClick = true;
      S.dragCatalog = it;
      S.dragCatalogBatch = palettePlaceCountFor(it.guid);
      e.dataTransfer?.setData("text/plain", it.guid);
    });
    row.addEventListener("dragend", () => {
      S.dragCatalog = null;
      S.dragCatalogBatch = 1;
      setTimeout(() => {
        skipClick = false;
      }, 0);
    });
    row.addEventListener("click", (e) => {
      if (skipClick) return;
      if ((e.target as HTMLElement).closest(".palette-qty-reset")) return;
      e.preventDefault();
      e.stopPropagation();
      bumpPalettePick(it);
    });
    row.addEventListener("contextmenu", (e) => {
      if (S.palettePick?.guid !== it.guid) return;
      e.preventDefault();
      e.stopPropagation();
      clearPalettePick();
    });
    grid.appendChild(row);
  }
  parent.appendChild(grid);
}

export function buildFloorPalette(filter = "", mode: "floor" | "background" = "floor") {
  dom.paletteCats.innerHTML = "";
  const q = filter.trim().toLowerCase();

  if (mode === "floor") {
    refreshFloorHeightPanel();
    const addBtn = document.createElement("button");
    addBtn.className = "palette-add-floor";
    addBtn.textContent = "+ 新增地板（在画布点击放置）";
    addBtn.addEventListener("click", () => {
      S.pendingNewFloor = true;
      S.pendingNewFloorCat = null;
      setStatus("在画布上点击以放置新地板（Esc 取消）");
      dom.canvas.style.cursor = "crosshair";
      updateFloorBar();
    });
    dom.paletteCats.appendChild(addBtn);

    const themedList = themedFloorPrefabs().filter(
      (it) => matchesFloorPaletteFilter(it, q) && matchesFloorHeightFilter(it)
    );
    if (themedList.length > 0) {
      const themedRow = document.createElement("div");
      themedRow.className = "palette-add-themed";
      const sel = document.createElement("select");
      for (const it of themedList) {
        const opt = document.createElement("option");
        opt.value = it.guid;
        opt.textContent = `${tidyCatalogNameZh(it.nameZh, it.id)}（${surfaceKindLabelZh(it.surfaceKind)}）`;
        sel.appendChild(opt);
      }
      const addThemedBtn = document.createElement("button");
      addThemedBtn.className = "palette-add-floor";
      addThemedBtn.textContent = "+ 新增主题地板";
      addThemedBtn.title = "写回时整块区域生成一个拉伸的 prefab 实例";
      addThemedBtn.addEventListener("click", () => {
        const cat = S.catalogByGuid.get(sel.value);
        if (!cat) {
          setStatus("请先选择主题地板 prefab", false);
          return;
        }
        S.pendingNewFloor = true;
        S.pendingNewFloorCat = cat;
        setStatus(`在画布上点击以放置主题地板：${tidyCatalogNameZh(cat.nameZh, cat.id)}（Esc 取消）`);
        dom.canvas.style.cursor = "crosshair";
        updateFloorBar();
      });
      themedRow.appendChild(sel);
      themedRow.appendChild(addThemedBtn);
      dom.paletteCats.appendChild(themedRow);
    }

    const airBtn = document.createElement("button");
    airBtn.className = "palette-add-floor";
    airBtn.textContent = "+ 新增空气地板";
    airBtn.title = "仅有可行走碰撞盒（Col_AirFloor），无可见地板，写回后生效";
    airBtn.addEventListener("click", () => {
      S.pendingNewAirFloor = true;
      setStatus("在画布上点击以放置空气地板（Esc 取消）");
      dom.canvas.style.cursor = "crosshair";
      updateFloorBar();
    });
    dom.paletteCats.appendChild(airBtn);
  }

  const pool = [...S.catalogByGuid.values()];
  let anyGroup = false;

  if (mode === "background") {
    const filtered = pool
      .filter((it) => matchesFloorPaletteFilter(it, q))
      .sort((a, b) => a.id.localeCompare(b.id));
    for (const group of backgroundPaletteGroups(filtered)) {
      if (group.items.length === 0) continue;
      anyGroup = true;
      const details = document.createElement("details");
      details.className = "cat-group";
      details.open = group.key === "common";
      const summary = document.createElement("summary");
      summary.textContent = `${group.labelZh} (${group.items.length})`;
      details.appendChild(summary);
      appendBackgroundCardGrid(details, group.items);
      dom.paletteCats.appendChild(details);
    }
    applyPaletteGridCols();
    refreshPaletteQtyBadges();
  } else {
    const groups: { key: string; labelZh: string; match: (it: CatalogItem) => boolean }[] = [
      {
        key: "snowice",
        labelZh: "❄ 雪地 / 冰面（含冰崖围边）",
        match: (it) =>
          it.surfaceKind === "snow" ||
          it.surfaceKind === "ice" ||
          /icecliff|snowmound|snowpile|snowball|iceblock/i.test(it.id),
      },
      { key: "conveyor", labelZh: "传送带地面", match: (it) => it.surfaceKind === "conveyor" },
      { key: "ground", labelZh: "大型地面", match: (it) => it.surfaceKind === "ground" },
      { key: "pressure", labelZh: "压力开关（特殊地板）", match: (it) => PRESSURE_SWITCH_SURFACE_IDS.has(it.id) },
    ];

    for (const group of groups) {
      const list = pool
        .filter((it) => group.match(it))
        .filter((it) => matchesFloorPaletteFilter(it, q))
        .filter((it) => matchesFloorHeightFilter(it))
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
      dom.paletteCats.appendChild(details);
    }
  }

  if (!anyGroup && q) {
    const empty = document.createElement("div");
    empty.className = "palette-empty";
    empty.textContent = "无匹配项";
    dom.paletteCats.appendChild(empty);
  }
}

function cancelPendingPlacement(): void {
  S.pendingNewFloor = false;
  S.pendingNewFloorCat = null;
  S.pendingNewAirFloor = false;
  dom.canvas.style.cursor = "";
  setStatus("已取消放置");
  updateFloorBar();
}

function buildSelectionInfo(): string {
  const f = S.floors.find((x) => x._key === S.selectedFloorKey);
  const selItem = S.selectedKey ? S.items.find((i) => i._editorKey === S.selectedKey) : null;
  const selCat = selItem ? S.catalogByGuid.get(selItem.prefabGuid) : undefined;

  if (S.selectedFloorKeys.size > 1) {
    return `<span class="fb-info">已选 ${S.selectedFloorKeys.size} 块地板（拖动整体移动 · 方向键微移 · R 旋转 · Del 删除）</span>`;
  }
  if (S.selectedFloorKeys.size > 0 && S.selectedKeys.size > 0) {
    return `<span class="fb-info">已选 ${S.selectedFloorKeys.size} 块地板 · ${S.selectedKeys.size} 个表面物品（拖动整体移动 · Del 删除）</span>`;
  }
  if (S.selectedKeys.size > 1 && S.selectedFloorKeys.size === 0) {
    return `<span class="fb-info">已选 ${S.selectedKeys.size} 个表面物品（拖动整体移动 · 方向键微移 · R 旋转 · Del 删除）</span>`;
  }
  if (f) {
    const matFloorDetail = (() => {
      const { w: tw, d: td } = effectiveMaterialTiling(f);
      const tilingMismatch = tw !== f._wCells || td !== f._dCells;
      const tilingTxt = tilingMismatch
        ? `<b>平铺 ${tw}×${td}</b>（地板 ${f._wCells}×${f._dCells}）`
        : `平铺 ${tw}×${td}`;
      return `${f.materialName ?? "无材质"} · ${tilingTxt}`;
    })();
    return `<span class="fb-info"><b>${f.airFloor ? "空气地板" : f.surfaceKind === "raft" ? "木筏地板" : isThemedFloor(f) ? "主题地板" : f.imageTexturePath ? "图片地板" : f.tintEnabled ? "染色地板" : surfaceKindLabelZh(f.surfaceKind)}</b> · ${f._wCells}×${f._dCells}格 · ${f.airFloor ? "仅可行走（无可见地板）" : f.surfaceKind === "raft" ? "木筏拼块（写回时生成）" : isThemedFloor(f) ? `${tidyCatalogNameZh(S.catalogByGuid.get(f.prefabGuid!)?.nameZh ?? f.displayName, f.displayName)}（写回时生成单个缩放实例）` : f.imageTexturePath ? `${f.imageMode === "tile" ? "一格平铺" : f.imageMode === "warp" ? "透视贴合" : "全部铺开"}${normalizeRot(f.imageRotation ?? 0) ? ` · 旋转${normalizeRot(f.imageRotation ?? 0)}°` : ""} · ${f.imageTexturePath.split("/").pop() ?? ""}` : f.tintEnabled ? `颜色 ${f.tintColor ?? "#ffffff"}` : matFloorDetail}</span>`;
  }
  if (selItem && isSurfaceItem(selCat)) {
    return `<span class="fb-info"><b>${surfaceKindLabelZh(selCat?.surfaceKind)}</b> · ${itemLabel(selItem)}</span>`;
  }
  if (S.currentLayer === "background") {
    const bgFloors = S.floors.filter((x) => x.surfaceKind === "background").length;
    const bgItems = S.items.filter((it) => {
      const cat = S.catalogByGuid.get(it.prefabGuid);
      return cat?.surfaceTier === "background" || isWaterBackgroundCat(cat) || isAmbientBackgroundCat(cat);
    }).length;
    return `<span class="fb-info">${deathLabelZh(S.deathInfo)} · ${bgFloors} 背景板 · ${bgItems} 背景物</span>`;
  }
  return `<span class="fb-info">${deathLabelZh(S.deathInfo)} · 共 ${S.floors.filter((x) => x.surfaceKind !== "background").length} 块地板</span>`;
}

export function updateFloorBar() {
  if (S.currentLayer !== "floor" && S.currentLayer !== "background") {
    dom.floorBar.classList.add("hidden");
    return;
  }
  dom.floorBar.classList.remove("hidden");

  const pending =
    S.pendingNewFloor || S.pendingNewAirFloor
      ? `<span class="fb-pending">${
          S.pendingNewAirFloor
            ? "正在放置空气地板"
            : S.pendingNewFloorCat
              ? `正在放置主题地板：${tidyCatalogNameZh(S.pendingNewFloorCat.nameZh, S.pendingNewFloorCat.id)}`
              : "正在放置新地板"
        }… <button type="button" id="fb-cancel-place" class="fb-btn">取消</button></span>`
      : "";

  const info = buildSelectionInfo();
  let actions = "";
  let hint = "";

  if (S.currentLayer === "floor") {
    const killToggle = `<label class="fb-check" title="回写 Unity 时把坠落区(KillPlane)扩大到覆盖整关"><input type="checkbox" id="fb-autokill" ${S.autoKillPlane ? "checked" : ""}/> 回写扩大坠落区</label>`;
    const walkToggle = `<label class="fb-check" title="回写时按可见地板重新生成可行走碰撞体"><input type="checkbox" id="fb-autowalk" ${S.autoWalkable ? "checked" : ""}/> 同步可行走到地板</label>`;
    const lotusRotBtn = `<button type="button" id="fb-lotus-rand-rot" class="fb-btn" title="莲花压力开关随机 0/90/180/270°">🪷 莲花随机旋转</button>`;
    actions = `${pending}${killToggle}${walkToggle}${lotusRotBtn}`;
    hint = `<span class="fb-hint">框选 · 拖动 · 拖角缩放 · 右键地板编辑器 · Del 删除 · Esc 取消放置</span>`;
  } else {
    actions = `${pending}<span class="fb-death-readonly" title="坠落类型由 Unity 场景 KillPlane 决定，在「相机/灯光」中查看相机背景色">坠落类型：<b>${deathLabelZh(S.deathInfo)}</b></span>`;
    hint = `<span class="fb-hint">拖入背景卡片放置 · 单击卡片累加数量 · 拖角缩放 · 右键铺满关卡 · Del 删除</span>`;
  }

  const html = `<div class="fb-row fb-row-actions">${actions}</div><div class="fb-row fb-row-info">${info}${hint}</div>`;
  const active = document.activeElement;
  const editing =
    !!active && dom.floorBar.contains(active) && (active.tagName === "SELECT" || active.tagName === "INPUT");
  if (editing || dom.floorBar.innerHTML === html) return;
  dom.floorBar.innerHTML = html;

  document.getElementById("fb-cancel-place")?.addEventListener("click", cancelPendingPlacement);
  document.getElementById("fb-autokill")?.addEventListener("change", (e) => {
    S.autoKillPlane = (e.target as HTMLInputElement).checked;
  });
  document.getElementById("fb-autowalk")?.addEventListener("change", (e) => {
    S.autoWalkable = (e.target as HTMLInputElement).checked;
  });
  document.getElementById("fb-lotus-rand-rot")?.addEventListener("click", () => {
    const n = batchRandomRotateLotusPressureSwitches();
    if (n === 0) {
      setStatus("场景中没有莲花压力开关", false);
      return;
    }
    setStatus(`已随机旋转 ${n} 个莲花压力开关（0° / 90° / 180° / 270°）`);
  });
}

export function matchesFloorPaletteFilter(it: CatalogItem, q: string): boolean {
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

export function appendPaletteTileGrid(parent: HTMLElement, list: CatalogItem[]) {
  const tileGrid = document.createElement("div");
  tileGrid.className = "palette-tile-grid";
  for (const it of list) {
    const row = document.createElement("div");
    row.className = "palette-item palette-tile";
    row.draggable = true;
    row.dataset.guid = it.guid;
    const hBadge =
      it.height != null && it.height > 0.01
        ? `<div class="sub fhf-badge">h=${it.height.toFixed(2)}</div>`
        : "";
    row.innerHTML = `<div class="zh">${tidyCatalogNameZh(it.nameZh, it.id)}</div><div class="id">${it.id}</div>${hBadge}`;
    row.addEventListener("dragstart", (e) => {
      S.dragCatalog = it;
      e.dataTransfer?.setData("text/plain", it.guid);
    });
    row.addEventListener("dragend", () => {
      S.dragCatalog = null;
    });
    tileGrid.appendChild(row);
  }
  parent.appendChild(tileGrid);
}
