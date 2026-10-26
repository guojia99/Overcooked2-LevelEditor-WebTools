import { S } from "./state";
import { dom } from "./dom";
import { tidyCatalogNameZh } from "../displayLabels";
import { hostRuleLabelZh } from "../stacking";
import { isAmbientBackgroundCat, isWaterBackgroundCat, foodIconImg } from "./catalog";
import { matchBuiltinAnimDecor } from "./builtinAnimDecor";
import { buildComboPaletteGroup } from "./combos";
import { variantBaseId, VARIANT_TO_BASE } from "./itemVariants";
import { escHtml } from "./coords";
import type { Catalog, CatalogItem } from "../types";
import { bindPaletteCardTooltip, catalogPaletteTooltipContent } from "./paletteTooltip";
import {
  decorPreviewUrl,
  isDecorPreviewMissing,
  markDecorPreviewFailed,
} from "./decorPreviewCache";

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

/** 占格文案：整数不补小数，半格（0.9）保留一位。 */
function footprintLabelZh(it: { footprint?: { cellsX?: number; cellsZ?: number } }): string {
  const x = Number(it.footprint?.cellsX);
  const z = Number(it.footprint?.cellsZ);
  if (!isFinite(x) || !isFinite(z) || x <= 0 || z <= 0) return "";
  const fmt = (n: number) => (Number.isInteger(n) ? String(n) : n.toFixed(1));
  return `${fmt(x)}×${fmt(z)} 格`;
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
  const mode = S.decorPreviewSize;
  const previewOn = S.currentLayer === "decor" && mode !== "none";
  const w = palette.getBoundingClientRect().width || parseFloat(palette.style.width || "") || 280;
  // 三档按「最小卡片宽度」自适应：默认 280px → 小 3 列 / 中 2 列 / 大 1 列；
  // 面板拉到 50% 屏宽（约 800px）时大档为 4 列 ≈ 当前旧版的大图尺寸。
  let cols: number;
  if (previewOn) {
    const divisor = mode === "small" ? 72 : mode === "medium" ? 112 : 180;
    const cap = mode === "small" ? 12 : mode === "medium" ? 10 : 6;
    cols = Math.max(1, Math.min(cap, Math.floor(w / divisor)));
  } else {
    cols = Math.max(1, Math.min(5, Math.floor(w / 92)));
  }
  cats.style.setProperty("--palette-cols", String(cols));
  cats.classList.toggle("decor-preview-on", previewOn);
  cats.dataset.decorPreviewSize = previewOn ? mode : "";
}

/** 装饰层「按类型 / 按用途」分组：按条目 typeKind/usage 字段动态聚合，
 *  标签与顺序来自 catalog.taxonomy（build-catalog.mjs 关键词规则打标）。 */
function deriveTaxonomyGroups(
  catalog: Catalog,
  mode: "type" | "usage"
): { groups: NonNullable<Catalog["paletteGroups"]>; byKey: Record<string, CatalogItem[]> } {
  const tax = catalog.taxonomy?.[mode === "type" ? "typeKind" : "usage"] ?? [];
  const lists = new Map<string, CatalogItem[]>();
  for (const it of catalog.items) {
    if (it.layoutTier !== "decor") continue;
    const key = (mode === "type" ? it.typeKind : it.usage) || "other";
    if (!lists.has(key)) lists.set(key, []);
    lists.get(key)!.push(it);
  }
  const groups: NonNullable<Catalog["paletteGroups"]> = [];
  const byKey: Record<string, CatalogItem[]> = {};
  for (const t of tax) {
    const list = lists.get(t.key);
    if (!list || list.length === 0) continue;
    const gk = `tax/${t.key}`;
    groups.push({ key: gk, labelZh: t.zh, labelEn: t.zh, layoutTier: "decor", itemCount: list.length });
    byKey[gk] = list;
  }
  return { groups, byKey };
}

const paletteOpenState = new Map<string, Set<string>>();
let paletteGroupActionsBound = false;

export function buildPalette(catalog: Catalog, filter: string) {
  const layer = S.currentLayer;
  const firstBuildForLayer = !paletteOpenState.has(layer);
  const prevScroll = firstBuildForLayer ? 0 : dom.paletteCats.scrollTop;
  dom.paletteCats.innerHTML = "";
  let openState = paletteOpenState.get(layer);
  if (!openState) {
    openState = new Set<string>();
    paletteOpenState.set(layer, openState);
  }
  const q = filter.trim().toLowerCase();
  const sizeFilter: DecorSizeFilter =
    S.currentLayer === "decor" ? (S.decorSizeFilter ?? "all") : "all";

  const themeMode = S.currentLayer !== "decor" || S.paletteGroupMode === "theme";
  let byKey = catalog.byCategory;
  let groups: NonNullable<Catalog["paletteGroups"]>;
  if (themeMode) {
    groups =
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
  } else {
    const derived = deriveTaxonomyGroups(catalog, S.paletteGroupMode as "type" | "usage");
    groups = derived.groups;
    byKey = derived.byKey;
  }

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
    const list = (byKey[group.key] ?? []).filter((it) => {
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
    details.dataset.groupKey = group.key;
    details.open = firstBuildForLayer ? group.layoutTier === "core" : openState.has(group.key);
    if (firstBuildForLayer && details.open) openState.add(group.key);
    details.addEventListener("toggle", () => {
      if (details.open) openState.add(group.key);
      else openState.delete(group.key);
    });
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
      const decorPreviewOn = it.layoutTier === "decor" && S.decorPreviewSize !== "none";
      if (it.layoutTier === "decor") row.classList.add("palette-decor");
      if (decorPreviewOn) {
        row.classList.add("palette-card-preview", `palette-card-preview-${S.decorPreviewSize}`);
      }
      row.draggable = true;
      row.dataset.guid = it.guid;
      const animDecor = it.layoutTier === "decor" ? matchBuiltinAnimDecor(it.id, it.nameZh) : null;
      const foodDecor = it.ingredientDecor === true;
      const recipeDecor = it.recipeDecor === true;
      const decorSize =
        it.layoutTier === "decor"
          ? ` <span class="decor-size-badge" title="尺寸分级：按实测占位（footprint）最大边判定">${DECOR_SIZE_LABEL_ZH[decorSizeTier(it)]}</span>`
          : "";
      const footprint = footprintLabelZh(it);
      const heightText =
        it.layoutTier === "decor" && typeof it.height === "number" && it.height > 0
          ? ` · 高 ${it.height.toFixed(2)}m`
          : "";
      const sub = it.stack
        ? `<div class="sub">配套${hostRuleLabelZh(it.stack.hostRule)} · 高 ${it.stack.y}m</div>`
        : it.layoutTier === "decor"
          ? `<div class="sub">装饰${!decorPreviewOn && footprint ? ` · 占 ${footprint}` : ""}${heightText}${foodDecor ? " · 食材" : recipeDecor ? " · 成品菜" : ""}${decorSize}${animDecor ? ` · ${animDecor.emoji} ${animDecor.badgeZh}` : ""}</div>`
          : "";
      const footprintRow = decorPreviewOn && footprint ? `<div class="fp">占地 ${footprint}</div>` : "";
      const skins = skinCounts.get(it.id) ?? 1;
      const skinBadge =
        skins > 1 ? ` <span class="variant-badge" title="同功能换肤 ${skins} 种：放置后右键可切换">×${skins}</span>` : "";
      const stubBadge = it.needsStub
        ? ` <span class="stub-badge" title="CustomStub 道具：游戏侧需携带关卡集 runtime（CustomStub）才能完整运行；web 导出 zip 会自动按需携带">⚙</span>`
        : "";
      const foodIcon = it.ingredientDecor
        ? foodIconImg("ingredients", it.id, true)
        : it.recipeDecor
          ? foodIconImg("recipes", it.id, true)
          : "";
      const previewUrl = decorPreviewOn ? decorPreviewUrl(it.guid) : null;
      const fallbackChar = escHtml((tidyCatalogNameZh(it.nameZh, it.id) || it.id).slice(0, 1));
      const thumb = previewUrl
        ? `<div class="pc-thumb-wrap"><img class="pc-thumb" loading="lazy" decoding="async" src="${escHtml(previewUrl)}" alt="" data-preview-guid="${escHtml(it.guid)}" /><span class="pc-thumb-fallback">${fallbackChar}</span></div>`
        : decorPreviewOn && isDecorPreviewMissing(it.guid) && foodIcon
          ? foodIcon
          : decorPreviewOn
            ? `<div class="pc-thumb-wrap"><span class="pc-thumb-fallback" style="display:flex">${fallbackChar}</span></div>`
            : foodIcon;
      const idLine = decorPreviewOn ? escHtml(it.id) : cardSubId(it);
      row.innerHTML = `${thumb}<div class="zh">${tidyCatalogNameZh(it.nameZh, it.id)}${skinBadge}${stubBadge}</div><div class="id" title="${escHtml(it.id)}">${idLine}</div>${footprintRow}${sub}`;
      const previewImg = row.querySelector<HTMLImageElement>("img.pc-thumb");
      if (previewImg) {
        previewImg.addEventListener("error", () => {
          markDecorPreviewFailed(it.guid);
          const wrap = previewImg.parentElement;
          previewImg.remove();
          if (wrap) {
            const fb = wrap.querySelector<HTMLElement>(".pc-thumb-fallback");
            if (fb) fb.style.display = "flex";
          }
          row.classList.add("preview-missing");
        });
      }
      bindPaletteCardTooltip(
        row,
        catalogPaletteTooltipContent(it, { animDecor, skinCount: skins })
      );
      if (animDecor) {
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
  dom.paletteCats.scrollTop = prevScroll;
}


/** 「展开全部 / 收起全部」按钮：作用于左侧调色板当前全部分组，并同步记忆状态。 */
function setAllPaletteGroups(open: boolean): void {
  let state = paletteOpenState.get(S.currentLayer);
  if (!state) {
    state = new Set<string>();
    paletteOpenState.set(S.currentLayer, state);
  }
  dom.paletteCats.querySelectorAll<HTMLDetailsElement>("details.cat-group[data-group-key]").forEach((d) => {
    d.open = open;
    const key = d.dataset.groupKey;
    if (!key) return;
    if (open) state.add(key);
    else state.delete(key);
  });
}

export function initPaletteGroupActions(): void {
  if (paletteGroupActionsBound) return;
  paletteGroupActionsBound = true;
  document.getElementById("palette-expand-all")?.addEventListener("click", () => setAllPaletteGroups(true));
  document.getElementById("palette-collapse-all")?.addEventListener("click", () => setAllPaletteGroups(false));
}
