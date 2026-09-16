import { dom } from "../dom";
import { positionFloating } from "./detailPanel";
import { hidePickTip } from "./overlay";
import { escHtml } from "../coords";
import type { PickCandidate, PickTipHeaderAction } from "../state";

export interface PickTipOptions {
  headerAction?: PickTipHeaderAction;
  /** Shift 多选：点击切换选中，弹窗保持打开并刷新标记 */
  toggleMode?: boolean;
  headText?: string;
  /** toggle 后重建候选（刷新 selected 状态） */
  rebuild?: () => PickCandidate[];
}

function normalizeOptions(arg?: PickTipOptions | PickTipHeaderAction): PickTipOptions {
  if (!arg) return {};
  if ("onClick" in arg && "label" in arg) return { headerAction: arg };
  return arg;
}

function renderPickTip(
  candidates: PickCandidate[],
  clientX: number,
  clientY: number,
  options: PickTipOptions
): void {
  const headerAction = options.headerAction;
  const toggleMode = !!options.toggleMode;
  const headerHtml = headerAction
    ? `<button type="button" class="pick-tip-item pick-tip-batch" data-pick-batch="1">
        <span class="pick-title">${escHtml(headerAction.label)}</span>
        ${headerAction.sub ? `<span class="pick-sub">${escHtml(headerAction.sub)}</span>` : ""}
      </button>`
    : "";
  const defaultHead = toggleMode
    ? `此处 ${candidates.length} 个重叠 · Shift 点击切换选中 · Shift 拖动框选加选`
    : headerAction
      ? "重叠对象 · 可选批量微调或单独操作："
      : `此处有 ${candidates.length} 个重叠对象，请选择要操作的对象：`;
  const headText = options.headText ?? defaultHead;
  const footHint = toggleMode ? "点击列表项切换选中 · Shift 拖动可框选加选 · 点击外部或 Esc 关闭" : "点击外部或 Esc 关闭";

  dom.pickTipEl.innerHTML =
    `<div class="pick-tip-head">${escHtml(headText)}</div>` +
    headerHtml +
    candidates
      .map((c, i) => {
        const sel = !!c.selected;
        return `<button type="button" class="pick-tip-item${sel ? " is-selected" : ""}" data-idx="${i}">
        <span class="pick-title">${sel ? '<span class="pick-sel-tag" aria-hidden="true">✓</span> ' : ""}${escHtml(c.title)}</span>
        <span class="pick-sub">${sel && toggleMode ? '<span class="pick-sel-hint">已选 · </span>' : ""}${escHtml(c.sub)}</span>
      </button>`;
      })
      .join("") +
    `<p class="close-hint">${footHint}</p>`;

  dom.pickTipEl.classList.remove("hidden");
  positionFloating(dom.pickTipEl, clientX, clientY);

  dom.pickTipEl.querySelector<HTMLButtonElement>("[data-pick-batch]")?.addEventListener("click", () => {
    hidePickTip();
    headerAction?.onClick();
  });
  dom.pickTipEl.querySelectorAll<HTMLButtonElement>(".pick-tip-item[data-idx]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const c = candidates[Number(btn.dataset.idx)];
      if (!c) return;
      c.onPick();
      if (toggleMode && options.rebuild) {
        showPickTip(options.rebuild(), clientX, clientY, options);
      } else {
        hidePickTip();
      }
    });
  });
}

export function showPickTip(
  candidates: PickCandidate[],
  clientX: number,
  clientY: number,
  options?: PickTipOptions | PickTipHeaderAction
) {
  renderPickTip(candidates, clientX, clientY, normalizeOptions(options));
}
