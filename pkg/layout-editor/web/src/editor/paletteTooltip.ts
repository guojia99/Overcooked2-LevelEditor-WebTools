import { escHtml } from "./coords";
import type { ComboDef } from "./state";
import { catalogItemById } from "./catalog";
import { tidyCatalogNameZh } from "../displayLabels";
import { hostRuleLabelZh } from "../stacking";
import type { CatalogItem } from "../types";
import type { BuiltinAnimDecorMatch } from "./builtinAnimDecor";

export interface PaletteTooltipSection {
  label: string;
  text: string;
}

export interface PaletteTooltipContent {
  title: string;
  sections?: PaletteTooltipSection[];
  footer?: string;
}

const tipByEl = new WeakMap<HTMLElement, PaletteTooltipContent>();
let tipEl: HTMLDivElement | null = null;
let activeEl: HTMLElement | null = null;

function ensureTipEl(): HTMLDivElement {
  if (!tipEl) {
    tipEl = document.createElement("div");
    tipEl.className = "palette-hover-tip hidden";
    tipEl.setAttribute("role", "tooltip");
    document.body.appendChild(tipEl);
    window.addEventListener("scroll", hidePaletteTooltip, true);
    window.addEventListener("resize", hidePaletteTooltip);
  }
  return tipEl;
}

export function hidePaletteTooltip(): void {
  activeEl = null;
  tipEl?.classList.add("hidden");
}

function positionTip(anchor: HTMLElement): void {
  const tip = ensureTipEl();
  tip.classList.remove("hidden");
  const ar = anchor.getBoundingClientRect();
  const margin = 8;
  const gap = 10;
  let left = ar.right + gap;
  let top = ar.top;
  const tw = tip.offsetWidth;
  const th = tip.offsetHeight;
  if (left + tw > window.innerWidth - margin) {
    left = Math.max(margin, ar.left - gap - tw);
  }
  if (top + th > window.innerHeight - margin) {
    top = Math.max(margin, window.innerHeight - margin - th);
  }
  top = Math.max(margin, top);
  tip.style.left = `${left}px`;
  tip.style.top = `${top}px`;
}

function renderTip(content: PaletteTooltipContent): void {
  const tip = ensureTipEl();
  const sections =
    content.sections
      ?.filter((s) => s.text.trim())
      .map(
        (s) =>
          `<div class="palette-hover-tip-section"><div class="palette-hover-tip-label">${escHtml(s.label)}</div><div class="palette-hover-tip-text">${escHtml(s.text)}</div></div>`
      )
      .join("") ?? "";
  const footer = content.footer?.trim()
    ? `<div class="palette-hover-tip-footer">${escHtml(content.footer)}</div>`
    : "";
  tip.innerHTML = `<div class="palette-hover-tip-title">${escHtml(content.title)}</div>${sections}${footer}`;
}

function showFor(el: HTMLElement): void {
  const content = tipByEl.get(el);
  if (!content) return;
  activeEl = el;
  renderTip(content);
  positionTip(el);
}

/** 悬停即时显示完整说明（无原生 title 延迟）；拖放开始时自动隐藏。 */
export function bindPaletteCardTooltip(el: HTMLElement, content: PaletteTooltipContent): void {
  tipByEl.set(el, content);
  el.setAttribute("title", "");
  if (el.dataset.paletteTipBound === "1") return;
  el.dataset.paletteTipBound = "1";
  el.addEventListener("mouseenter", () => showFor(el));
  el.addEventListener("mouseleave", hidePaletteTooltip);
  el.addEventListener("dragstart", hidePaletteTooltip);
}

export function comboPaletteTooltipContent(def: ComboDef, disabledReason?: string | null): PaletteTooltipContent {
  const orderedParts = def.parts.map((p) => catalogItemById(p.id)?.nameZh || p.id);
  const counts = new Map<string, number>();
  for (const name of orderedParts) counts.set(name, (counts.get(name) ?? 0) + 1);
  const partsText = Array.from(counts.entries())
    .map(([name, n]) => (n > 1 ? `${name} ×${n}` : name))
    .join("、");
  const placement =
    def.parts.length > 1
      ? def.parts
          .map((p, i) => {
            const name = catalogItemById(p.id)?.nameZh || p.id;
            const off =
              p.dx === 0 && p.dz === 0 ? "基准位" : `偏移 ${p.dx}×${p.dz} 格`;
            return `${i + 1}. ${name}（${off}）`;
          })
          .join("\n")
      : "";
  return {
    title: def.nameZh,
    sections: [
      { label: "包含组件", text: partsText },
      ...(placement ? [{ label: "相对摆放", text: placement }] : []),
      { label: "联动说明", text: def.hint },
      ...(disabledReason ? [{ label: "不可用", text: disabledReason }] : []),
    ],
    footer: `组合 ID：${def.id} · 拖到画布一次放置并自动配置联动`,
  };
}

export function catalogPaletteTooltipContent(
  it: CatalogItem,
  extras?: { animDecor?: BuiltinAnimDecorMatch | null; skinCount?: number }
): PaletteTooltipContent {
  const en = it.nameEn?.trim() ?? "";
  const norm = (s: string) => s.toLowerCase().replace(/[\s_\-]+/g, "");
  const sections: PaletteTooltipSection[] = [];
  if (en && norm(en) !== norm(it.id)) sections.push({ label: "英文名", text: en });
  sections.push({ label: "Prefab ID", text: it.id });
  if (it.assetPath) sections.push({ label: "资源路径", text: it.assetPath });
  if (it.stack) {
    sections.push({
      label: "堆叠配套",
      text: `需${hostRuleLabelZh(it.stack.hostRule)} · 高度 ${it.stack.y}m`,
    });
  }
  if (it.layoutTier === "decor") {
    const bits: string[] = ["装饰物"];
    if (it.ingredientDecor) bits.push("食材展示");
    if (it.recipeDecor) bits.push("成品菜展示");
    sections.push({ label: "类型", text: bits.join(" · ") });
  }
  if (extras?.skinCount && extras.skinCount > 1) {
    sections.push({ label: "换肤", text: `同功能皮肤 ${extras.skinCount} 种（放置后右键可切换）` });
  }
  if (it.needsStub) {
    sections.push({
      label: "CustomStub",
      text: "需关卡集携带 CustomStub runtime；导出 zip 会按需打包",
    });
  }
  if (extras?.animDecor) {
    sections.push({ label: "内置动画", text: extras.animDecor.title });
  }
  return {
    title: tidyCatalogNameZh(it.nameZh, it.id),
    sections,
    footer: "拖到画布放置；单击卡片可累加批量数量",
  };
}
