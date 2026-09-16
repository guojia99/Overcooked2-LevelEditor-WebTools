let overlay: HTMLDivElement | null = null;
let depth = 0;
let iconFailed = false;

/**
 * 全局忙碌遮罩。加载动画为「烹饪」主题：番茄 / 芝士 / 土豆三张食材图标
 * （来自 public/icons/ingredients/）依次弹跳，所有长耗时操作共用
 * （写回、加载关卡、删除、导出关卡集……）。图标缺失时回退为圆环 spinner。
 */
function ensure(): HTMLDivElement {
  if (!overlay) {
    overlay = document.createElement("div");
    overlay.id = "busy-overlay";
    overlay.className = "busy-overlay hidden";
    overlay.innerHTML = `
      <div class="busy-box">
        <div class="busy-foods" aria-hidden="true">
          <img class="busy-food f1" src="/icons/ingredients/TomatoSO.png" alt="">
          <img class="busy-food f2" src="/icons/ingredients/CheeseSO.png" alt="">
          <img class="busy-food f3" src="/icons/ingredients/PotatoSO.png" alt="">
        </div>
        <div class="busy-fallback" aria-hidden="true"><div class="busy-spinner"></div></div>
        <div class="busy-msg" id="busy-msg">处理中…</div>
      </div>`;
    document.body.appendChild(overlay);
    // dist 未重建（无 icons 目录）等情况下回退到圆环 spinner。
    overlay.querySelectorAll<HTMLImageElement>(".busy-food").forEach((img) => {
      img.addEventListener("error", () => {
        if (!iconFailed) {
          iconFailed = true;
          overlay?.classList.add("no-food-icons");
        }
      });
    });
  }
  return overlay;
}

export function showBusy(msg = "处理中…"): void {
  const o = ensure();
  const m = o.querySelector("#busy-msg");
  if (m) m.textContent = msg;
  o.classList.remove("hidden");
  depth++;
}

/** 任务进行中只更新文案（不动引用计数）。用于长时间任务的阶段提示。 */
export function setBusyMessage(msg: string): void {
  const m = ensure().querySelector("#busy-msg");
  if (m) m.textContent = msg;
}

export function hideBusy(): void {
  depth = Math.max(0, depth - 1);
  if (depth === 0 && overlay) overlay.classList.add("hidden");
}

export async function withBusy<T>(msg: string, task: () => Promise<T>): Promise<T> {
  showBusy(msg);
  try {
    return await task();
  } finally {
    hideBusy();
  }
}
