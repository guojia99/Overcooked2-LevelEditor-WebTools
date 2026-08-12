import { S } from "./state";
import { dom } from "./dom";
import { tidyCatalogNameZh } from "../displayLabels";
import { hostRuleLabelZh } from "../stacking";
import { isAmbientBackgroundCat, isWaterBackgroundCat } from "./catalog";
import { isNpcAnimItem } from "./npcAnimations";
import type { Catalog } from "../types";

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

    for (const it of list) {
      const row = document.createElement("div");
      row.className = "palette-item";
      if (it.layoutTier === "decor") row.classList.add("palette-decor");
      row.draggable = true;
      row.dataset.guid = it.guid;
      const npcAnim = isNpcAnimItem(it.id, it.nameZh);
      const sub = it.stack
        ? `<div class="sub">配套${hostRuleLabelZh(it.stack.hostRule)} · 高度 ${it.stack.y}m</div>`
        : it.layoutTier === "decor"
          ? `<div class="sub">装饰${npcAnim ? " · 🎞 自带移动动画" : ""}</div>`
          : "";
      row.innerHTML = `<div class="zh">${tidyCatalogNameZh(it.nameZh, it.id)}</div><div class="id">${it.nameEn} · ${it.id}</div>${sub}`;
      if (npcAnim) {
        row.title = "自带移动动画（行走循环 / 路径动画）";
        const badge = document.createElement("span");
        badge.className = "npc-anim-badge";
        badge.textContent = "🎞";
        row.querySelector(".zh")?.appendChild(badge);
      }
      row.addEventListener("dragstart", (e) => {
        S.dragCatalog = it;
        e.dataTransfer?.setData("text/plain", it.guid);
      });
      row.addEventListener("dragend", () => {
        S.dragCatalog = null;
      });
      details.appendChild(row);
    }
    dom.paletteCats.appendChild(details);
  }
}
