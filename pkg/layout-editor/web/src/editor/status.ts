import { notify, type NotifyOptions } from "../notification";

export function setStatus(text: string, ok = true): void {
  notify(text, ok);
}

/** 桥接连接等需 sticky + tag 的场景 */
export function setStatusTagged(text: string, ok: boolean, tag: string): void {
  const opts: NotifyOptions = { ok, tag, sticky: !ok };
  notify(text, opts);
}
