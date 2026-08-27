import { S } from "./state";
import { dom } from "./dom";
import { tidyCatalogNameZh } from "../displayLabels";
import { hostRuleLabelZh } from "../stacking";
import { isAmbientBackgroundCat, isWaterBackgroundCat } from "./catalog";
import { matchBuiltinAnimDecor } from "./builtinAnimDecor";
import { buildComboPaletteGroup } from "./combos";
import { variantBaseId, VARIANT_TO_BASE } from "./itemVariants";
import { escHtml } from "./coords";
import type { Catalog, CatalogItem } from "../types";

function clampPaletteCount(n: number): number {
  if (!isFinite(n)) return 1;
  return Math.max(1, Math.min(99, Math.round(n)));
}

/** 单击武装并累加数量；在卡片角标显示，拖到画布按数量放置。 */
export function bumpPalettePick(cat: CatalogItem): void {
  if (S.palettePick?.guid === cat.guid) {
    S.palettePick.count = clampPaletteCount(S.palettePick.count + 1);
  } else {
    S.palettePick = { guid: cat.guid, count: 1 };
  }
  refreshPaletteQtyBadges();
}

export function clearPalettePick(): void {
  if (!S.palettePick) return;
  S.palettePick = null;
  refreshPaletteQtyBadges();
}

export function refreshPaletteQtyBadges(): void {
  document.querySelectorAll<HTMLElement>(".palette-card[data-guid]").forEach((el) => {
    const guid = el.dataset.guid!;
    const armed = !!S.palettePick && S.palettePick.guid === guid;
    el.classList.toggle("palette-card-armed", armed);
    let badge = el.querySelector<HTMLElement>(".palette-qty-badge");
    if (armed && S.palettePick) {
      if (!badge) {
        badge = document.createElement("span");
        badge.className = "palette-qty-badge";
        badge.title = "点击 × 清除数量";
        badge.innerHTML =
          '<span class="palette-qty-num"></span><button type="button" class="palette-qty-reset" title="清除数量">×</button>';
        badge.querySelector(".palette-qty-reset")?.addEventListener("click", (e) => {
          e.preventDefault();
          e.stopPropagation();
          clearPalettePick();
        });
        el.appendChild(badge);
      }
      const num = badge.querySelector(".palette-qty-num");
      if (num) num.textContent = String(S.palettePick.count);
    } else if (badge) {
      badge.remove();
    }
  });
}

export function palettePlaceCountFor(guid: string): number {
  if (S.palettePick?.guid === guid) return S.palettePick.count;
  return 1;
}

/** 卡片按目录大类着色（与画布配色 itemColors.CATEGORY_RGB 一致）。 */
export function paletteCardCategory(category: string): string {
  const top = (category ?? "").split("/")[0];
  return top || "other";
}

/** 装饰物尺寸分级：按 footprint 最大边（格）判定。 */
export type DecorSizeFilter = "all" | "small" | "medium" | "large" | "xl";

export const DECOR_SIZE_LABEL_ZH: Record<Exclude<DecorSizeFilter, "all">, string> = {
  small: "小",
  medium: "中",
  large: "大",
  xl: "特大",
};

export function decorSizeTier(it: { footprint?: { cellsX?: number; cellsZ?: number } }): Exclude<DecorSizeFilter, "all"> {
  const f = it.footprint;
  const max = Math.max(f?.cellsX ?? 1, f?.cellsZ ?? 1);
  if (max <= 1) return "small";
  if (max <= 2) return "medium";
  if (max <= 3) return "large";
  return "xl";
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

export function buildPalette(catalog: Catalog, filter: string) {
  dom.paletteCats.innerHTML = "";
  const q = filter.trim().toLowerCase();
  const sizeFilter: DecorSizeFilter =
    S.currentLayer === "decor" ? (S.decorSizeFilter ?? "all") : "all";

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

  // 家族皮肤计数（基础版卡片显示 ×N 徽标）：baseId → 家族内目录条目数。
  const skinCounts = new Map<string, number>();
  for (const it of S.catalogById.values()) {
    const b = variantBaseId(it.id);
    skinCounts.set(b, (skinCounts.get(b) ?? 0) + 1);
  }

  // 「联合组合」分类：核心层置顶（一次放置多个物品并自动完成联动配置）。
  if (S.currentLayer !== "decor") {
    const comboEl = buildComboPaletteGroup(filter);
    if (comboEl) dom.paletteCats.appendChild(comboEl);
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
      if (sizeFilter !== "all" && decorSizeTier(it) !== sizeFilter) return false;
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
    summary.title = group.labelEn;
    details.appendChild(summary);

    const grid = document.createElement("div");
    grid.className = "palette-grid";
    details.appendChild(grid);

    let shownCount = 0;
    for (const it of list) {
      // DLC 变体合并：变体不单独出卡（家族只留基础版一张卡，右键切换皮肤）；
      // 基础版条目缺失时保留变体卡（容忍 common03 内容更新移除条目）。
      const baseId = VARIANT_TO_BASE[it.id];
      if (baseId && baseId !== it.id && S.catalogById.has(baseId)) continue;
      // 不可用条目（旧拷贝）静默隐藏
      const p = it.assetPath ?? "";
      const isLegacyWeb = p.includes("/Import/prefabs/") || p.includes("/custom_web/prefabs/");
      if (isLegacyWeb) continue;
      const row = document.createElement("div");
      const catName = paletteCardCategory(it.category);
      row.className = `palette-card palette-cat-${catName}`;
      if (it.layoutTier === "decor") row.classList.add("palette-decor");
      row.draggable = true;
      row.dataset.guid = it.guid;
      const animDecor = it.layoutTier === "decor" ? matchBuiltinAnimDecor(it.id, it.nameZh) : null;
      const decorSize =
        it.layoutTier === "decor"
          ? ` <span class="decor-size-badge" title="尺寸分级：按实测占位（footprint）最大边判定">${DECOR_SIZE_LABEL_ZH[decorSizeTier(it)]}</span>`
          : "";
      const sub = it.stack
        ? `<div class="sub">配套${hostRuleLabelZh(it.stack.hostRule)} · 高 ${it.stack.y}m</div>`
        : it.layoutTier === "decor"
          ? `<div class="sub">装饰${decorSize}${animDecor ? ` · ${animDecor.emoji} ${animDecor.badgeZh}` : ""}</div>`
          : "";
      const skins = skinCounts.get(it.id) ?? 1;
      const skinBadge =
        skins > 1 ? ` <span class="variant-badge" title="同功能换肤 ${skins} 种：放置后右键可切换">×${skins}</span>` : "";
      row.innerHTML = `<div class="zh">${tidyCatalogNameZh(it.nameZh, it.id)}${skinBadge}</div><div class="id">${cardSubId(it)}</div>${sub}`;
      if (animDecor) {
        row.title = animDecor.title;
        const badgeEl = document.createElement("span");
        badgeEl.className = animDecor.kind === "npc" ? "npc-anim-badge" : "env-anim-badge";
        badgeEl.textContent = animDecor.emoji;
        row.querySelector(".zh")?.appendChild(badgeEl);
      }
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
      shownCount++;
    }
    // 全部条目都被隐藏（家族合并/不可用）时收起整个分组
    if (shownCount === 0) continue;
    summary.textContent = `${group.labelZh} (${shownCount})`;
    dom.paletteCats.appendChild(details);
  }
  applyPaletteGridCols();
  refreshPaletteQtyBadges();
}
