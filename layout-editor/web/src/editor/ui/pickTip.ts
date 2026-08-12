import { dom } from "../dom";
import { positionFloating } from "./detailPanel";
import { hidePickTip } from "./overlay";
import { escHtml } from "../coords";
import type { PickCandidate } from "../state";

export function showPickTip(candidates: PickCandidate[], clientX: number, clientY: number) {
  dom.pickTipEl.innerHTML =
    `<div class="pick-tip-head">此处有 ${candidates.length} 个重叠对象，请选择要操作的对象：</div>` +
    candidates
      .map(
        (c, i) =>
          `<button type="button" class="pick-tip-item" data-idx="${i}"><span class="pick-title">${escHtml(c.title)}</span><span class="pick-sub">${escHtml(c.sub)}</span></button>`
      )
      .join("") +
    `<p class="close-hint">点击外部或 Esc 关闭</p>`;
  dom.pickTipEl.classList.remove("hidden");
  positionFloating(dom.pickTipEl, clientX, clientY);
  dom.pickTipEl.querySelectorAll<HTMLButtonElement>(".pick-tip-item").forEach((btn) => {
    btn.addEventListener("click", () => {
      hidePickTip();
      candidates[Number(btn.dataset.idx)]?.onPick();
    });
  });
}
