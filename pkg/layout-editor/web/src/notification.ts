/** 全局 Toast 通知（非阻塞；与 busy 全屏遮罩互补）。 */

export type NotifyOptions = {
  ok?: boolean;
  durationMs?: number;
  /** 不自动关闭，需手动点关闭或同 tag 覆盖 */
  sticky?: boolean;
  /** 同 tag 只保留一条（桥接连接状态等） */
  tag?: string;
};

const MAX_VISIBLE = 4;
const DEFAULT_OK_MS = 4000;
const DEFAULT_ERR_MS = 8000;

let root: HTMLElement | null = null;
const byTag = new Map<string, HTMLElement>();
const timers = new WeakMap<HTMLElement, number>();

function ensureRoot(): HTMLElement {
  if (!root) {
    root = document.createElement("div");
    root.id = "notification-root";
    root.className = "notification-root";
    root.setAttribute("aria-live", "polite");
    document.body.appendChild(root);
  }
  return root;
}

function removeItem(el: HTMLElement): void {
  const t = timers.get(el);
  if (t != null) window.clearTimeout(t);
  timers.delete(el);
  for (const [tag, node] of byTag) {
    if (node === el) byTag.delete(tag);
  }
  el.remove();
  trimStack();
}

function trimStack(): void {
  if (!root) return;
  const items = [...root.querySelectorAll<HTMLElement>(".notification-item")];
  while (items.length > MAX_VISIBLE) {
    const last = items.pop();
    if (last) removeItem(last);
  }
}

function scheduleAutoClose(el: HTMLElement, ms: number): void {
  const t = window.setTimeout(() => removeItem(el), ms);
  timers.set(el, t);
}

function createItem(message: string, ok: boolean, sticky: boolean): HTMLElement {
  const el = document.createElement("div");
  el.className = `notification-item ${ok ? "ok" : "err"}`;
  const body = document.createElement("span");
  body.className = "notification-body";
  body.textContent = message;
  el.appendChild(body);
  if (sticky) {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "notification-close";
    btn.setAttribute("aria-label", "关闭");
    btn.textContent = "×";
    btn.addEventListener("click", () => removeItem(el));
    el.appendChild(btn);
  }
  return el;
}

/** 显示一条通知。空字符串忽略。 */
export function notify(message: string, okOrOpts: boolean | NotifyOptions = true): void {
  const text = message.trim();
  if (!text) return;

  const opts: NotifyOptions =
    typeof okOrOpts === "boolean" ? { ok: okOrOpts } : { ok: true, ...okOrOpts };
  const ok = opts.ok !== false;
  const sticky = !!opts.sticky;
  const durationMs = opts.durationMs ?? (ok ? DEFAULT_OK_MS : DEFAULT_ERR_MS);

  const container = ensureRoot();

  if (opts.tag) {
    const prev = byTag.get(opts.tag);
    if (prev) removeItem(prev);
  }

  const el = createItem(text, ok, sticky);
  if (opts.tag) byTag.set(opts.tag, el);

  container.prepend(el);
  trimStack();

  if (!sticky) scheduleAutoClose(el, durationMs);
}
