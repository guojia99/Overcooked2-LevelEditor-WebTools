import { S } from "./state";
import { dom } from "./dom";
import { tidyCatalogNameZh } from "../displayLabels";
import { hostRuleLabelZh } from "../stacking";
import { isAmbientBackgroundCat, isWaterBackgroundCat } from "./catalog";
import { isNpcAnimItem } from "./npcAnimations";
import { escHtml } from "./coords";
import type { Catalog } from "../types";

/** 卡片按目录大类着色（与画布配色 itemColors.CATEGORY_RGB 一致）。 */
export function paletteCardCategory(category: string): string {
  const top = (category ?? "").split("/")[0];
  return top || "other";
}

/** 卡片副标题：英文名与 prefab id 相同（归一化后）时只显示 id，避免重复。 */
function cardSubId(it: { nameEn?: string; id: string }): string {
  const en = it.nameEn ?? "";
  const norm = (s: string) => s.toLowerCase().replace(/[\s_\-]+/g, "");
  if (en && norm(en) !== norm(it.id)) return `${escHtml(en)} · ${escHtml(it.id)}`;
  return escHtml(it.id);
}

/** 依据左侧面板当前宽度设置每行卡片列数（自动适配，最多 5 列）。 */
export function applyPaletteGridCols(): void {
  const palette = document.getElementById("palette-panel");
  const cats = document.getElementById("palette-cats");
  if (!palette || !cats) return;
  const w = palette.getBoundingClientRect().width || parseFloat(palette.style.width || "") || 280;
  const cols = Math.max(1, Math.min(5, Math.floor(w / 92)));
  cats.style.setProperty("--palette-cols", String(cols));
}

function syncVariantsButton(): void {
  const btn = document.getElementById("btn-palette-variants");
  const state = document.getElementById("palette-variants-state");
  if (btn) btn.classList.toggle("active", S.showPaletteVariants);
  if (state) state.textContent = S.showPaletteVariants ? "●" : "○";
}

/** 功能重复的 DLC 变体（换皮/同名），默认折叠在「显示 DLC 变体」按钮之后。 */
export const CORE_PALETTE_VARIANTS = new Set([
  // 烤箱 / 烤制工作站（换皮）
  "dlc08_oven_02",
  "dlc09_oven",
  "oven_medieval",
  "oven_furnace_medieval",
  "workstation_furnace_01",
  "dlc13_workstation_cooker_01",
  // 汤锅 / 煎锅 / 炸篮 / 烤盘
  "dlc03_utensil_pot",
  "dlc07_utensil_pot_01",
  "dlc08_utensil_pot_01",
  "dlc09_utensil_pot",
  "dlc07_utensil_frying_pan_01",
  "dlc08_utensil_frying_pan",
  "dlc09_utensil_frying_pan",
  "dlc08_frierbasket",
  "dlc09_utensil_roasting_tray",
  // 火锅
  "utensil_dlc10_large_pot_01",
  "dlc10_cooking_region_floorburner",
  // 搅拌器具 / 搅拌台
  "dlc03_utensil_mixer",
  "dlc07_utensil_mixer_01",
  "dlc08_utensil_mixer_01",
  "dlc09_utensil_mixer",
  "dlc13_utensil_mixer_01",
  "workstation_mixer_01",
  "workstation_mixer_03",
  "dlc10_workstation_mixer",
  "dlc08_workstation_mixer",
  "dlc13_workstation_mixer_01",
  // 杯子 / 马克杯（换皮）
  "dlc11_cleanglassstack",
  "dlc11_equipment_glass_01",
  "dlc09_cleanmugstack",
  "dlc09_dirtymug",
  "dlc09_dirtymugstack",
  "dlc09_equipment_mug_01",
  // 水槽 / 回收台（换皮）
  "dlc13_workstation_sink_01_wood",
  "workstation_sink_01_summer",
  "dlc13_workstation_bin_01",
  "dlc13_workstation_plate_return",
  "dlc11_workstation_glass_return_01",
  "dlc09_workstation_sink_mug_01_wood",
  "dlc09_workstation_mug_return_winter",
  "dlc13_workstation_plate_station",
  // 工具
  "dlc09_utensil_ingredient_spray",
  "dlc10_pushable_object",
  "utensil_dlc10_big_ol_spoon",
  "dlc08_utensil_fire_extinguisher",
]);

export function buildPalette(catalog: Catalog, filter: string) {
  dom.paletteCats.innerHTML = "";
  dom.paletteCats.classList.toggle("show-variants", S.showPaletteVariants);
  syncVariantsButton();
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

  S.corePaletteGroupMeta.clear();
  for (const g of groups) {
    if (g.layoutTier === "core") S.corePaletteGroupMeta.set(g.key, g.labelZh);
  }

  for (const group of groups) {
    if (group.key === "Player") continue;
    // 核心层只显示核心玩法物品（layoutTier=core），装饰层只显示装饰物，
    // 其余一律归入装饰层（地板/背景在地板/背景层调色板）。
    if (S.currentLayer === "decor" ? group.layoutTier !== "decor" : group.layoutTier !== "core") continue;
    const list = (catalog.byCategory[group.key] ?? []).filter((it) => {
      if (it.surfaceTier === "floor" || it.surfaceTier === "background") return false;
      // Ambient / weather effects (落雪 BGM…) and water surfaces live on the
      // background layer.
      if (isAmbientBackgroundCat(it)) return false;
      if (isWaterBackgroundCat(it)) return false;
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

    const grid = document.createElement("div");
    grid.className = "palette-grid";
    details.appendChild(grid);

    for (const it of list) {
      const row = document.createElement("div");
      const catName = paletteCardCategory(it.category);
      row.className = `palette-card palette-cat-${catName}`;
      if (it.layoutTier === "decor") row.classList.add("palette-decor");
      const isVariant = CORE_PALETTE_VARIANTS.has(it.id);
      if (isVariant) row.classList.add("palette-card-variant");
      // 暂时屏蔽这波新增的 DLC 道具（Import 源库 / custom_web 副本）：可见但置灰禁用
      const isDlcProp = /(\/Import\/prefabs\/|\/custom_web\/prefabs\/)/.test(it.assetPath);
      if (isDlcProp) row.classList.add("palette-card-disabled");
      row.draggable = !isDlcProp;
      row.dataset.guid = it.guid;
      const npcAnim = isNpcAnimItem(it.id, it.nameZh);
      const sub = it.stack
        ? `<div class="sub">配套${hostRuleLabelZh(it.stack.hostRule)} · 高 ${it.stack.y}m</div>`
        : it.layoutTier === "decor"
          ? `<div class="sub">装饰${npcAnim ? " · 🎞 自带移动动画" : ""}</div>`
          : "";
      const badge = isDlcProp ? ' <span class="disabled-badge" title="暂时屏蔽的 DLC 道具">⛔ 禁用</span>' : "";
      row.innerHTML = `<div class="zh">${tidyCatalogNameZh(it.nameZh, it.id)}${isVariant ? ' <span class="variant-badge">DLC</span>' : ""}${badge}</div><div class="id">${cardSubId(it)}</div>${sub}`;
      if (npcAnim) {
        row.title = "自带移动动画（行走循环 / 路径动画）";
        const badgeEl = document.createElement("span");
        badgeEl.className = "npc-anim-badge";
        badgeEl.textContent = "🎞";
        row.querySelector(".zh")?.appendChild(badgeEl);
      }
      if (!isDlcProp) {
        row.addEventListener("dragstart", (e) => {
          S.dragCatalog = it;
          e.dataTransfer?.setData("text/plain", it.guid);
        });
        row.addEventListener("dragend", () => {
          S.dragCatalog = null;
        });
      }
      grid.appendChild(row);
    }
    dom.paletteCats.appendChild(details);
  }
  applyPaletteGridCols();
}
