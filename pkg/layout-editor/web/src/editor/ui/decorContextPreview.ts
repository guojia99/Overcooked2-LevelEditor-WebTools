import type { CatalogItem } from "../../types";
import { tidyCatalogNameZh } from "../../displayLabels";
import { catalogItemForGuidOrPath, itemCategoryOf } from "../catalog";
import { escHtml, prefabIdFromPath } from "../coords";
import { decorPreviewUrl, markDecorPreviewFailed } from "../decorPreviewCache";
import { openDecorPreviewLightbox } from "./decorPreviewLightbox";
import { dom } from "../dom";
import { S, type EditorItem } from "../state";

const MARGIN = 12;
const GAP = 10;

let observersBound = false;
let shownKey: string | null = null;

function previewEl(): HTMLElement | null {
  return dom.decorCtxPreviewEl ?? null;
}

function previewLabel(item: EditorItem, cat: CatalogItem | undefined): string {
  if (cat?.nameZh) return tidyCatalogNameZh(cat.nameZh, cat.id);
  if (item.displayName) return tidyCatalogNameZh(item.displayName, item.displayName);
  return prefabIdFromPath(item.prefabAssetPath) || "装饰";
}

/** 食材 / 成品菜装饰优先用彩色图标，和左侧调色板一致。 */
function foodPreviewSrc(cat: CatalogItem | undefined): string {
  if (!cat?.icon) return "";
  if (cat.ingredientDecor) return `/icons/ingredients/${encodeURIComponent(cat.id)}.png`;
  if (cat.recipeDecor) return `/icons/recipes/${encodeURIComponent(cat.id)}.png`;
  return "";
}

function ensureObservers(): void {
  if (observersBound) return;
  observersBound = true;
  window.addEventListener("resize", () => syncDecorContextPreview());
  const stage = document.getElementById("canvas-stage");
  if (stage && typeof ResizeObserver !== "undefined") {
    const ro = new ResizeObserver(() => syncDecorContextPreview());
    ro.observe(stage);
  }
}

function overlapsMenu(menu: DOMRect, x: number, y: number, w: number, h: number): boolean {
  return !(
    x + w <= menu.left - GAP ||
    x >= menu.right + GAP ||
    y + h <= menu.top - GAP ||
    y >= menu.bottom + GAP
  );
}

/** 把预览卡放在画布右上角；右键菜单开着且挡住该角时，改放到仍靠右上、且不压住菜单的空位。 */
export function syncDecorContextPreview(): void {
  const el = previewEl();
  if (!el || el.classList.contains("hidden")) return;
  const stage = document.getElementById("canvas-stage");
  if (!stage) {
    hideDecorContextPreview();
    return;
  }
  const sr = stage.getBoundingClientRect();
  const menuOpen = !dom.ctxMenuEl.classList.contains("hidden");
  const menu = menuOpen ? dom.ctxMenuEl.getBoundingClientRect() : null;
  const w = el.offsetWidth;
  const h = el.offsetHeight;
  if (w < 1 || h < 1) return;

  const candidates: Array<[number, number]> = [
    [sr.right - MARGIN - w, sr.top + MARGIN],
  ];
  if (menu) {
    candidates.push(
      [menu.left - GAP - w, sr.top + MARGIN],
      [sr.right - MARGIN - w, menu.bottom + GAP],
      [menu.right + GAP, sr.top + MARGIN],
      [sr.left + MARGIN, sr.top + MARGIN]
    );
  }
  const inside = (x: number, y: number) =>
    x >= sr.left + MARGIN - 0.5 &&
    y >= sr.top + MARGIN - 0.5 &&
    x + w <= sr.right - MARGIN + 0.5 &&
    y + h <= sr.bottom - MARGIN + 0.5;

  let chosen: [number, number] | null = null;
  for (const spot of candidates) {
    if (inside(spot[0], spot[1]) && !(menu && overlapsMenu(menu, spot[0], spot[1], w, h))) {
      chosen = spot;
      break;
    }
  }
  if (!chosen) {
    const maxX = Math.max(sr.left + MARGIN, sr.right - MARGIN - w);
    const maxY = Math.max(sr.top + MARGIN, sr.bottom - MARGIN - h);
    chosen = [
      Math.min(Math.max(sr.right - MARGIN - w, sr.left + MARGIN), maxX),
      Math.min(sr.top + MARGIN, maxY),
    ];
  }
  el.style.left = `${Math.round(chosen[0])}px`;
  el.style.top = `${Math.round(chosen[1])}px`;
}

export function hideDecorContextPreview(): void {
  shownKey = null;
  const el = previewEl();
  if (!el) return;
  el.classList.add("hidden");
  el.setAttribute("aria-hidden", "true");
}

/** 只聚焦一个装饰物时显示预览；多选、选中非装饰或清空选中时收起。 */
export function syncDecorPreviewForSelection(): void {
  if (!previewEl()) return;
  if (S.selectedKeys.size !== 1 || !S.selectedKey) {
    hideDecorContextPreview();
    return;
  }
  const item = S.items.find((it) => it._editorKey === S.selectedKey);
  if (!item || itemCategoryOf(item) !== "decor") {
    hideDecorContextPreview();
    return;
  }
  showDecorContextPreview(item);
}

/** 在画布右上角显示该装饰物的缩略图。同一件已在显示时只重新摆位置。 */
export function showDecorContextPreview(item: EditorItem): void {
  const el = previewEl();
  if (!el) return;
  if (shownKey === item._editorKey && !el.classList.contains("hidden")) {
    requestAnimationFrame(() => syncDecorContextPreview());
    return;
  }
  shownKey = item._editorKey;
  const cat = catalogItemForGuidOrPath(item.prefabGuid, item.prefabAssetPath);
  const guid = cat?.guid || item.prefabGuid;
  const label = previewLabel(item, cat);
  const food = foodPreviewSrc(cat);
  const renderFront = decorPreviewUrl(guid, "front");
  const renderBack = decorPreviewUrl(guid, "back");
  const isDual = !!renderBack;
  const front: string = isDual ? (renderFront || food) : (food || renderFront || "");
  const frontFallback: string = front === food ? (renderFront || "") : (food || "");
  const ch = escHtml(Array.from(label || "?")[0] || "?");
  el.classList.toggle("is-dual", isDual);

  const imgHtml = (src: string, fallbackSrc: string, extraClass = ""): string =>
    src
      ? `<img class="decor-ctx-preview-img${extraClass}" alt="" src="${escHtml(src)}"${
          fallbackSrc ? ` data-fallback-src="${escHtml(fallbackSrc)}"` : ""
        } />`
      : "";

  if (isDual) {
    el.innerHTML = `<div class="decor-ctx-preview-views">
      <div class="decor-ctx-preview-view">
        <div class="decor-ctx-preview-thumb">${imgHtml(front, frontFallback, front === food ? " is-icon" : "")}<span class="decor-ctx-preview-fallback"${
          front ? "" : ' style="display:flex"'
        }>${ch}</span></div>
        <div class="decor-ctx-preview-view-label">正面</div>
      </div>
      <div class="decor-ctx-preview-view">
        <div class="decor-ctx-preview-thumb">${imgHtml(renderBack || "", "")}<span class="decor-ctx-preview-fallback" style="display:none">无</span></div>
        <div class="decor-ctx-preview-view-label">背面</div>
      </div>
    </div><div class="decor-ctx-preview-name">${escHtml(label)}</div>`;
  } else {
    el.innerHTML = `<div class="decor-ctx-preview-thumb">${imgHtml(front, frontFallback, front === food ? " is-icon" : "")}<span class="decor-ctx-preview-fallback"${
      front ? "" : ' style="display:flex"'
    }>${ch}</span></div><div class="decor-ctx-preview-name">${escHtml(label)}</div>`;
  }
  el.setAttribute("aria-label", label);
  el.querySelectorAll<HTMLElement>(".decor-ctx-preview-thumb").forEach((thumb, idx) => {
    thumb.title = "点击放大查看";
    if (!thumb.querySelector(".decor-ctx-preview-zoom")) {
      const badge = document.createElement("span");
      badge.className = "decor-ctx-preview-zoom";
      badge.textContent = "🔍";
      thumb.appendChild(badge);
    }
    thumb.addEventListener("click", (e) => {
      e.stopPropagation();
      openDecorPreviewLightbox(guid, label, isDual && idx === 1 ? "back" : "front");
    });
  });
  el.querySelectorAll<HTMLImageElement>("img.decor-ctx-preview-img").forEach((img) => {
    img.addEventListener("error", () => {
      const fb = img.dataset.fallbackSrc;
      if (fb && img.dataset.fallbackUsed !== "1") {
        img.dataset.fallbackUsed = "1";
        img.classList.remove("is-icon");
        img.src = fb;
        return;
      }
      // 背面失败只隐藏背面图；正面失败回退食物图标/首字。
      const isBack = !!img.closest(".decor-ctx-preview-view:last-child");
      if (guid) markDecorPreviewFailed(guid, isBack ? "back" : "front");
      img.remove();
      const thumb = img.parentElement;
      const letter = thumb?.querySelector<HTMLElement>(".decor-ctx-preview-fallback");
      if (letter) {
        letter.textContent = isBack ? "无" : letter.textContent;
        letter.style.display = "flex";
      }
    });
  });
  el.style.visibility = "hidden";
  el.classList.remove("hidden");
  el.setAttribute("aria-hidden", "false");
  ensureObservers();
  requestAnimationFrame(() => {
    syncDecorContextPreview();
    if (!el.classList.contains("hidden")) el.style.visibility = "";
  });
}
