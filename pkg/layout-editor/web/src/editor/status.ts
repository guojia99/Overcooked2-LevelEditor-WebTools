import { dom } from "./dom";

export function setStatus(text: string, ok = true) {
  dom.statusEl.textContent = text;
  dom.statusEl.className = "status " + (ok ? "ok" : "err");
}
