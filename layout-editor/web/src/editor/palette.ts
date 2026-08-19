import { S } from "./state";
import { dom } from "./dom";
import { tidyCatalogNameZh } from "../displayLabels";
import { hostRuleLabelZh } from "../stacking";
import { isAmbientBackgroundCat, isWaterBackgroundCat } from "./catalog";
import { isNpcAnimItem } from "./npcAnimations";
import { buildComboPaletteGroup } from "./combos";
import { variantBaseId, VARIANT_TO_BASE } from "./itemVariants";
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

export function buildPalette(catalog: Catalog, filter: string) {
  dom.paletteCats.innerHTML = "";
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
      const npcAnim = isNpcAnimItem(it.id, it.nameZh);
      const sub = it.stack
        ? `<div class="sub">配套${hostRuleLabelZh(it.stack.hostRule)} · 高 ${it.stack.y}m</div>`
        : it.layoutTier === "decor"
          ? `<div class="sub">装饰${npcAnim ? " · 🎞 自带移动动画" : ""}</div>`
          : "";
      const skins = skinCounts.get(it.id) ?? 1;
      const skinBadge =
        skins > 1 ? ` <span class="variant-badge" title="同功能换肤 ${skins} 种：放置后右键可切换">×${skins}</span>` : "";
      row.innerHTML = `<div class="zh">${tidyCatalogNameZh(it.nameZh, it.id)}${skinBadge}</div><div class="id">${cardSubId(it)}</div>${sub}`;
      if (npcAnim) {
        row.title = "自带移动动画（行走循环 / 路径动画）";
        const badgeEl = document.createElement("span");
        badgeEl.className = "npc-anim-badge";
        badgeEl.textContent = "🎞";
        row.querySelector(".zh")?.appendChild(badgeEl);
      }
      row.addEventListener("dragstart", (e) => {
        S.dragCatalog = it;
        e.dataTransfer?.setData("text/plain", it.guid);
      });
      row.addEventListener("dragend", () => {
        S.dragCatalog = null;
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
}
