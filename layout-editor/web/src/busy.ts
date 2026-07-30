let overlay: HTMLDivElement | null = null;
let depth = 0;

function ensure(): HTMLDivElement {
  if (!overlay) {
    overlay = document.createElement("div");
    overlay.id = "busy-overlay";
    overlay.className = "busy-overlay hidden";
    overlay.innerHTML = `
      <div class="busy-box">
        <div class="busy-spinner"></div>
        <div class="busy-msg" id="busy-msg">处理中…</div>
      </div>`;
    document.body.appendChild(overlay);
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

export function hideBusy(): void {
  depth = Math.max(0, depth - 1);
  if (depth === 0 && overlay) overlay.classList.add("hidden");
}

/** Run an async task with a busy indicator. */
export async function withBusy<T>(msg: string, task: () => Promise<T>): Promise<T> {
  showBusy(msg);
  try {
    return await task();
  } finally {
    hideBusy();
  }
}
