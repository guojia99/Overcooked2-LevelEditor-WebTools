/**
 * 空气斜坡参数面板（v9）：坡角 / 长度 / 宽度 + 起止高度实时显示。
 * 交互模式与 selectionAirWallHeight 一致（±步进按钮 + 数字输入，写回后生效）。
 *
 * 参数模型 = 高度 + 长度 + 手动角度：
 *   起点 = 物件 Y（与地板对齐摆放），终点 = 起点 + tan(angle)×长度，UI 只读展示。
 */

import { CELL, S, EditorItem } from "./state";
import {
  AIR_SLOPE_MAX_ANGLE,
  AIR_SLOPE_WARN_ANGLE,
  airSlopeEndY,
  airSlopeRiseMeters,
  setAirSlopeParams,
} from "./items";
import { pushHistory } from "./historyOps";
import { setStatus } from "./status";
import { draw } from "./render";

export function airSlopeRowHtml(item: EditorItem): string {
  const s = item.slope;
  if (!s) return "";
  const startY = item.localPosition?.y ?? 0;
  const endY = airSlopeEndY(item);
  const rise = airSlopeRiseMeters(item);
  const steep = s.angleDeg > AIR_SLOPE_WARN_ANGLE;
  return `
    <div class="ctx-nudge-row">
      <span class="ctx-label">坡角(度) <span class="ctx-scale-val${steep ? " ctx-steep-warn" : ""}" id="ctx-as-angle-val">${s.angleDeg.toFixed(0)}°</span></span>
      <div class="ctx-nudge">
        <button type="button" data-as-angle-dy="-5" title="坡角 −5°">−5°</button>
        <input type="number" id="ctx-as-angle-input" class="ctx-input ctx-pos-input" min="0.5" max="${AIR_SLOPE_MAX_ANGLE}" step="1" value="${s.angleDeg}" title="坡角 0.5~${AIR_SLOPE_MAX_ANGLE}°（GroundCast 上限 58°；原版 40~46.5°）" />
        <button type="button" data-as-angle-dy="5" title="坡角 +5°">+5°</button>
      </div>
    </div>
    <div class="ctx-nudge-row">
      <span class="ctx-label">坡长(格) <span class="ctx-scale-val">水平${(s.lengthCells * CELL).toFixed(1)}m</span></span>
      <div class="ctx-nudge">
        <button type="button" data-as-len-dy="-1" title="长度 −1 格">−1</button>
        <input type="number" id="ctx-as-len-input" class="ctx-input ctx-pos-input" min="0.5" step="1" value="${s.lengthCells}" title="坡道水平投影长度（格，1格=${CELL}m，沿朝向）" />
        <button type="button" data-as-len-dy="1" title="长度 +1 格">+1</button>
      </div>
    </div>
    <div class="ctx-nudge-row">
      <span class="ctx-label">坡宽(格) <span class="ctx-scale-val">${(s.widthCells * CELL).toFixed(1)}m</span></span>
      <div class="ctx-nudge">
        <input type="number" id="ctx-as-width-input" class="ctx-input ctx-pos-input" min="0.5" step="1" value="${s.widthCells}" title="坡道宽度（格，垂直朝向）" />
      </div>
    </div>
    <div class="ctx-nudge-row">
      <span class="ctx-label">高度 <span class="ctx-scale-val" id="ctx-as-height-val">${startY.toFixed(1)}m → ${endY.toFixed(1)}m（升${rise.toFixed(2)}m）</span></span>
      <span class="muted" style="align-self:center;font-size:11px">起点=物件Y · R 键转向</span>
    </div>
    <div class="ctx-nudge-row">
      <span class="ctx-label">调试显示 <span class="ctx-scale-val" id="ctx-as-dbg-val">${s.debugColor ? "游戏内可见" : "关"}</span></span>
      <div class="ctx-nudge">
        <input type="color" id="ctx-as-dbg-color" class="ctx-color-input" value="${/^#[0-9a-fA-F]{6}$/.test(s.debugColor ?? "") ? s.debugColor : "#7fe8c8"}" title="选色后写回会在游戏内生成半透明薄板（淡显），用于排查坡向/衔接；正式导出前建议关闭" />
        <button type="button" id="ctx-as-dbg-off" title="关闭调试显示（游戏内不再可见）">关</button>
      </div>
    </div>`;
}

export function wireAirSlopeRow(root: HTMLElement, item: EditorItem): void {
  const angleInput = root.querySelector<HTMLInputElement>("#ctx-as-angle-input");
  if (!angleInput) return;

  let pushed = false;
  const ensureHistory = () => {
    if (!pushed) {
      pushHistory();
      pushed = true;
    }
  };

  const refresh = () => {
    const s = item.slope;
    if (!s) return;
    if (document.activeElement !== angleInput) angleInput.value = s.angleDeg.toFixed(0);
    const lenInp = root.querySelector<HTMLInputElement>("#ctx-as-len-input");
    const wInp = root.querySelector<HTMLInputElement>("#ctx-as-width-input");
    if (lenInp && document.activeElement !== lenInp) lenInp.value = String(s.lengthCells);
    if (wInp && document.activeElement !== wInp) wInp.value = String(s.widthCells);
    const hEl = root.querySelector("#ctx-as-height-val");
    if (hEl) {
      const startY = item.localPosition?.y ?? 0;
      hEl.textContent = `${startY.toFixed(1)}m → ${airSlopeEndY(item).toFixed(1)}m（升${airSlopeRiseMeters(item).toFixed(2)}m）`;
    }
    const aEl = root.querySelector("#ctx-as-angle-val");
    if (aEl) {
      aEl.textContent = `${s.angleDeg.toFixed(0)}°`;
      aEl.classList.toggle("ctx-steep-warn", s.angleDeg > AIR_SLOPE_WARN_ANGLE);
    }
    const dbgEl = root.querySelector("#ctx-as-dbg-val");
    if (dbgEl) dbgEl.textContent = s.debugColor ? "游戏内可见" : "关";
  };

  const apply = (patch: Parameters<typeof setAirSlopeParams>[1]) => {
    ensureHistory();
    setAirSlopeParams(item, patch);
    S.dirty = true;
    refresh();
    draw();
  };

  root.querySelectorAll<HTMLButtonElement>("[data-as-angle-dy]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const dy = parseFloat(btn.dataset.asAngleDy ?? "0");
      apply({ angleDeg: (item.slope?.angleDeg ?? 30) + dy });
    });
  });
  root.querySelectorAll<HTMLButtonElement>("[data-as-len-dy]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const dy = parseFloat(btn.dataset.asLenDy ?? "0");
      apply({ lengthCells: (item.slope?.lengthCells ?? 3) + dy });
    });
  });

  angleInput.addEventListener("input", () => {
    const v = parseFloat(angleInput.value);
    if (!isFinite(v)) return;
    apply({ angleDeg: v });
    if (v > AIR_SLOPE_MAX_ANGLE) {
      setStatus(`坡角不能超过 ${AIR_SLOPE_MAX_ANGLE}°（GroundCast 上限，玩家走不上去），已自动钳制`, false);
    } else if (v > AIR_SLOPE_WARN_ANGLE) {
      setStatus(`坡角 ${v}° 偏陡（原版斜坡 40~46.5°），上坡手感可能卡顿`, true);
    }
  });
  angleInput.addEventListener("blur", () => {
    pushed = false;
  });

  root.querySelector<HTMLInputElement>("#ctx-as-len-input")?.addEventListener("input", (e) => {
    const v = parseFloat((e.target as HTMLInputElement).value);
    if (isFinite(v)) apply({ lengthCells: v });
  });
  root.querySelector<HTMLInputElement>("#ctx-as-width-input")?.addEventListener("input", (e) => {
    const v = parseFloat((e.target as HTMLInputElement).value);
    if (isFinite(v)) apply({ widthCells: v });
  });

  // 调试显示色：选色即启用（写回后游戏内半透明可见）；「关」清除。
  root.querySelector<HTMLInputElement>("#ctx-as-dbg-color")?.addEventListener("input", (e) => {
    const v = (e.target as HTMLInputElement).value;
    if (/^#[0-9a-fA-F]{6}$/.test(v)) apply({ debugColor: v.toLowerCase() });
  });
  root.querySelector<HTMLButtonElement>("#ctx-as-dbg-off")?.addEventListener("click", () => {
    apply({ debugColor: "" });
    const lbl = root.querySelector("#ctx-as-dbg-val");
    if (lbl) lbl.textContent = "关";
  });
}
