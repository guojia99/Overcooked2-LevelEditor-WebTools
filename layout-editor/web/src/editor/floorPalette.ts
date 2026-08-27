import { S } from "./state";
import type { CatalogItem } from "../types";
import { dom } from "./dom";
import { normalizeRot } from "./coords";
import { setStatus } from "./status";
import { draw } from "./render";
import { pushHistory } from "./historyOps";
import { batchRandomRotateLotusPressureSwitches } from "./items";
import { setFloorSelection } from "./selection";
import {
  isThemedFloor,
  themedFloorPrefabs,
  syncBackgroundForTheme,
  effectiveMaterialTiling
} from "./floors";
import { itemLabel } from "./labels";
import {
  BG_THEMES,
  bgTheme,
  bgThemeTooltip,
  deathLabelZh,
  isSurfaceItem,
  isThemeBackgroundPrefabId,
  surfaceKindLabelZh,
  themeBackgroundPrefabIds
} from "../floorColors";
import { tidyCatalogNameZh } from "../displayLabels";
import { isAmbientBackgroundCat, isWaterBackgroundCat } from "./catalog";
import { floorLayerSummary } from "./floorHeight";
import {
  selectionHeightRowHtml,
  selectionHeightTargetCount,
  wireSelectionHeightRow
} from "./selectionHeight";

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
  // 地板层按地板计数，核心/装饰层按物品计数。
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

/** 高度过滤面板（悬浮）整体刷新：层列表 + 滑块位置。 */
export function refreshFloorHeightPanel(): void {
  renderFloorHeightLayers();
  syncFloorHeightSliderUI();
}

/** 高度区间变化后的统一刷新：面板 + 物品面板（地板层）+ 画布。 */
export function refreshAfterHeightFilterChange(): void {
  refreshFloorHeightPanel();
  if (S.currentLayer === "floor") {
    const q = (document.getElementById("palette-search") as HTMLInputElement | null)?.value ?? "";
    buildFloorPalette(q, "floor");
  }
  draw();
}

/** 特殊地板：压力开关（含莲花压力开关），在地板层放置。 */
const PRESSURE_SWITCH_SURFACE_IDS = new Set([
  "PressureSwitch",
  "dlc13_lotuspressureswitch_large",
  "dlc13_lotuspressureswitch_small",
]);

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
      setStatus("在画布上点击以放置新地板");
      dom.canvas.style.cursor = "crosshair";
    });
    dom.paletteCats.appendChild(addBtn);

    // 新增主题地板: pick a themed prefab, then click the canvas to place a floor
    // that tiles it on write-back (a standalone floor type, not a solid plane).
    const themedList = themedFloorPrefabs().filter((it) => matchesFloorPaletteFilter(it, q) && matchesFloorHeightFilter(it));
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
        setStatus(`在画布上点击以放置主题地板：${tidyCatalogNameZh(cat.nameZh, cat.id)}`);
        dom.canvas.style.cursor = "crosshair";
      });
      themedRow.appendChild(sel);
      themedRow.appendChild(addThemedBtn);
      dom.paletteCats.appendChild(themedRow);
    }

    // 新增空气地板: only a walkable Col_AirFloor collider, no visible plane.
    const airBtn = document.createElement("button");
    airBtn.className = "palette-add-floor";
    airBtn.textContent = "+ 新增空气地板";
    airBtn.title = "仅有可行走碰撞盒（Col_AirFloor），无可见地板，写回后生效";
    airBtn.addEventListener("click", () => {
      S.pendingNewAirFloor = true;
      setStatus("在画布上点击以放置空气地板（仅可行走，无可见地板）");
      dom.canvas.style.cursor = "crosshair";
    });
    dom.paletteCats.appendChild(airBtn);
  }

  const groups: { key: string; labelZh: string; match: (it: CatalogItem) => boolean }[] =
    mode === "background"
      ? [
          {
            key: "water",
            labelZh: "水面 / 海洋",
            match: (it) => isWaterBackgroundCat(it),
          },
          {
            key: "ambient",
            labelZh: "环境特效（落雪 / BGM）",
            match: (it) => isAmbientBackgroundCat(it),
          },
          {
            key: "background",
            labelZh: "背景 / 环境",
            match: (it) => it.surfaceTier === "background" && !isThemeBackgroundPrefabId(it.id),
          },
        ]
      : [
          {
            key: "snowice",
            labelZh: "❄ 雪地 / 冰面（含冰崖围边）",
            // 雪地板/冰面砖 + 冰崖围边（装饰层条目，搭高台时围边用）+ 雪堆
            match: (it) =>
              it.surfaceKind === "snow" ||
              it.surfaceKind === "ice" ||
              /icecliff|snowmound|snowpile|snowball|iceblock/i.test(it.id),
          },
          { key: "conveyor", labelZh: "传送带地面", match: (it) => it.surfaceKind === "conveyor" },
          { key: "ground", labelZh: "大型地面", match: (it) => it.surfaceKind === "ground" },
          { key: "pressure", labelZh: "压力开关（特殊地板）", match: (it) => PRESSURE_SWITCH_SURFACE_IDS.has(it.id) },
        ];
  // Background palette also lists ambient effects (they are not surface items).
  // Floor mode uses the full catalog too so the snow group can include ice-cliff
  // decor pieces (placed as regular items); conveyor/ground/pressure groups are
  // surfaceKind/ID-scoped and unaffected.
  const pool = [...S.catalogByGuid.values()];

  let anyGroup = false;
  for (const group of groups) {
    const list = pool
      .filter((it) => group.match(it))
      .filter((it) => matchesFloorPaletteFilter(it, q))
      .filter((it) => mode === "background" || matchesFloorHeightFilter(it))
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

  if (!anyGroup && q) {
    const empty = document.createElement("div");
    empty.className = "palette-empty";
    empty.textContent = "无匹配项";
    dom.paletteCats.appendChild(empty);
  }
}

export function updateFloorBar() {
  if (S.currentLayer !== "floor" && S.currentLayer !== "background") {
    dom.floorBar.classList.add("hidden");
    return;
  }
  dom.floorBar.classList.remove("hidden");
  const themeOpts = BG_THEMES.map(
    (t) =>
      `<option value="${t.key}"${t.key === S.bgThemeKey ? " selected" : ""}>${t.emoji} ${t.labelZh}</option>`
  ).join("");
  const themeRow = `<label class="fb-theme" title="${bgThemeTooltip(bgTheme(S.bgThemeKey), S.currentLevelSet)}">背景：<select id="fb-bg-theme">${themeOpts}</select></label>`;
  const killToggle = `<label class="fb-check" title="回写 Unity 时把坠落区(KillPlane)扩大到覆盖整关，使所有非地板区域都会坠落"><input type="checkbox" id="fb-autokill" ${S.autoKillPlane ? "checked" : ""}/> 回写扩大坠落区</label>`;
  const walkToggle = `<label class="fb-check" title="回写时按可见地板重新生成可行走碰撞体(Col_Floor)：可行走=地板，地板间空隙=坠落坑"><input type="checkbox" id="fb-autowalk" ${S.autoWalkable ? "checked" : ""}/> 同步可行走到地板</label>`;
  const bgEditToggle = `<label class="fb-check" title="默认锁定：背景板只显示，不参与点选/框选/拖动/缩放。勾选后可像普通地板一样操作背景"><input type="checkbox" id="fb-bgedit" ${S.backgroundEditable ? "checked" : ""}/> 解锁背景操作</label>`;
  const f = S.floors.find((x) => x._key === S.selectedFloorKey);
  const selItem = S.selectedKey ? S.items.find((i) => i._editorKey === S.selectedKey) : null;
  const selCat = selItem ? S.catalogByGuid.get(selItem.prefabGuid) : undefined;
  let info: string;
  if (S.selectedFloorKeys.size > 1) {
    info = `<span class="fb-info">已选 ${S.selectedFloorKeys.size} 块地板（拖动整体移动 · 方向键微移 · R 旋转 · Del 删除）</span>`;
  } else if (S.selectedFloorKeys.size > 0 && S.selectedKeys.size > 0) {
    info = `<span class="fb-info">已选 ${S.selectedFloorKeys.size} 块地板 · ${S.selectedKeys.size} 个表面物品（拖动任一选中项整体移动 · 方向键/R 批量变换 · Del 删除）</span>`;
  } else if (S.selectedKeys.size > 1 && S.selectedFloorKeys.size === 0) {
    info = `<span class="fb-info">已选 ${S.selectedKeys.size} 个表面物品（拖动整体移动 · 方向键微移 · R 旋转 · Del 删除）</span>`;
  } else if (f) {
    const matFloorDetail = (() => {
      const { w: tw, d: td } = effectiveMaterialTiling(f);
      const tilingMismatch = tw !== f._wCells || td !== f._dCells;
      const tilingTxt = tilingMismatch
        ? `<b>平铺 ${tw}×${td}</b>（地板 ${f._wCells}×${f._dCells}）`
        : `平铺 ${tw}×${td}`;
      return `${f.materialName ?? "无材质"} · ${tilingTxt}`;
    })();
    info = `<span class="fb-info"><b>${f.airFloor ? "空气地板" : f.surfaceKind === "raft" ? "木筏地板" : isThemedFloor(f) ? "主题地板" : f.imageTexturePath ? "图片地板" : f.tintEnabled ? "染色地板" : surfaceKindLabelZh(f.surfaceKind)}</b> · ${f._wCells}×${f._dCells}格 · ${f.airFloor ? "仅可行走（无可见地板）" : f.surfaceKind === "raft" ? "木筏拼块（写回时生成）" : isThemedFloor(f) ? `${tidyCatalogNameZh(S.catalogByGuid.get(f.prefabGuid!)?.nameZh ?? f.displayName, f.displayName)}（写回时生成单个缩放实例）` : f.imageTexturePath ? `${f.imageMode === "tile" ? "一格平铺" : f.imageMode === "warp" ? "透视贴合" : "全部铺开"}${normalizeRot(f.imageRotation ?? 0) ? ` · 旋转${normalizeRot(f.imageRotation ?? 0)}°` : ""} · ${f.imageTexturePath.split("/").pop() ?? ""}` : f.tintEnabled ? `颜色 ${f.tintColor ?? "#ffffff"}` : matFloorDetail}</span>`;
  } else if (selItem && isSurfaceItem(selCat)) {
    info = `<span class="fb-info"><b>${surfaceKindLabelZh(selCat?.surfaceKind)}</b> · ${itemLabel(selItem)}</span>`;
  } else {
    info = `<span class="fb-info">${deathLabelZh(S.deathInfo)} · 共 ${S.floors.length} 块地板</span>`;
  }
  const lotusRotBtn =
    S.currentLayer === "floor"
      ? `<button type="button" id="fb-lotus-rand-rot" class="fb-btn" title="将场景中所有莲花压力开关随机设为 0° / 90° / 180° / 270°（写回后生效）">🪷 莲花随机旋转</button>`
      : "";
  const batchHRow =
    selectionHeightTargetCount() >= 2 ? selectionHeightRowHtml() : "";
  const html = `${themeRow}${killToggle}${walkToggle}${bgEditToggle}${lotusRotBtn}${batchHRow}${info}<span class="fb-hint">背景为坠落死亡区 · 拖拽空白框选 · 拖动移动 · 拖角点缩放 · 右键详情</span>`;
  const active = document.activeElement;
  const editing =
    !!active && dom.floorBar.contains(active) && (active.tagName === "SELECT" || active.tagName === "INPUT");
  if (editing || dom.floorBar.innerHTML === html) return;
  dom.floorBar.innerHTML = html;

  document.getElementById("fb-bg-theme")?.addEventListener("change", (e) => {
    const nextTheme = (e.target as HTMLSelectElement).value || "void";
    if (nextTheme === S.bgThemeKey) return;
    pushHistory();
    S.bgThemeKey = nextTheme;
    S.bgThemeDirty = true;
    localStorage.setItem("bgTheme:" + S.scenePath, S.bgThemeKey);
    syncBackgroundForTheme(S.bgThemeKey);
    updateFloorBar();
    setStatus(`背景主题：${bgTheme(S.bgThemeKey).labelZh}（写回 Unity 后生效）`);
  });
  document.getElementById("fb-autokill")?.addEventListener("change", (e) => {
    S.autoKillPlane = (e.target as HTMLInputElement).checked;
  });
  document.getElementById("fb-autowalk")?.addEventListener("change", (e) => {
    S.autoWalkable = (e.target as HTMLInputElement).checked;
  });
  document.getElementById("fb-bgedit")?.addEventListener("change", (e) => {
    S.backgroundEditable = (e.target as HTMLInputElement).checked;
    if (!S.backgroundEditable) {
      const alive = new Set(
        S.floors.filter((x) => x.surfaceKind !== "background").map((x) => x._key)
      );
      if ([...S.selectedFloorKeys].some((k) => !alive.has(k))) {
        setFloorSelection([...S.selectedFloorKeys].filter((k) => alive.has(k)));
      }
    }
    draw();
  });
  document.getElementById("fb-lotus-rand-rot")?.addEventListener("click", () => {
    const n = batchRandomRotateLotusPressureSwitches();
    if (n === 0) {
      setStatus("场景中没有莲花压力开关", false);
      return;
    }
    setStatus(`已随机旋转 ${n} 个莲花压力开关（0° / 90° / 180° / 270°）`);
  });
  wireSelectionHeightRow(dom.floorBar, () => draw());
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
    const sub =
      it.surfaceTier === "background" && themeBackgroundPrefabIds("sky").includes(it.id)
        ? `<div class="sub">天空主题自动补齐</div>`
        : it.id === "Water_01"
          ? `<div class="sub">水主题自动补齐</div>`
          : it.id === "alien_gue"
            ? `<div class="sub">黏液主题自动补齐</div>`
            : "";
    // 固有模型高度徽标（实测 bounds Y）：平板 ~0.1、冰崖等高件 1+。
    const hBadge =
      it.height != null && it.height > 0.01
        ? `<div class="sub fhf-badge">h=${it.height.toFixed(2)}</div>`
        : "";
    row.innerHTML = `<div class="zh">${tidyCatalogNameZh(it.nameZh, it.id)}</div><div class="id">${it.id}</div>${sub}${hBadge}`;
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
