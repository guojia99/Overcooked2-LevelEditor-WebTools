import { S } from "./state";
import { cancelBtnHtml, modalBtnHtml } from "../ui/views/button";
import { escHtml } from "./coords";
import { pushHistory } from "./historyOps";
import { draw } from "./render";
import { openModal, closeModal } from "../modals";
import { setStatus } from "./status";

/**
 * 打开「相机」编辑弹窗：背景色、FOV、出发点位置（X/Z）。
 * 灯光请在「💡 灯光层」编辑。
 */
export function openCameraLightModal(): void {
  if (!S.scenePath) {
    setStatus("请先加载场景", false);
    return;
  }

  const cam = S.cameraInfo;
  const camSection = cam
    ? `
      <div class="modal-field cl-cam-color">
        <span>背景色</span>
        <span class="cl-color-row">
          <input type="color" id="cl-cam-bg" value="${normalizeHex(cam.backgroundColor, "#000000")}" />
          <code>${escHtml(cam.backgroundColor ?? "")}</code>
        </span>
      </div>
      <div class="modal-field">
        <span>视野角度</span>
        <span class="cl-fov-row">
          <input type="range" id="cl-cam-fov-range" min="20" max="80" step="1" value="${clampNum(cam.fieldOfView, 20, 80)}" />
          <input type="number" id="cl-cam-fov-num" min="1" max="179" step="1" value="${cam.fieldOfView}" />
        </span>
      </div>
      <div class="modal-field cl-cam-pos-field">
        <span>出发点位置</span>
        <span class="cl-cam-pos">
          <span class="cl-pos-pad">
            <button type="button" class="cl-pos-btn cl-pos-up" data-cam-pos="up" title="Z +0.1（长按连续调整）">↑</button>
            <button type="button" class="cl-pos-btn cl-pos-left" data-cam-pos="left" title="X −0.1（长按连续调整）">←</button>
            <span class="cl-pos-val">
              <label>X <input type="number" id="cl-cam-pos-x" step="0.1" title="相机 X 世界坐标（回车生效）" /></label>
              <label>Z <input type="number" id="cl-cam-pos-z" step="0.1" title="相机 Z 世界坐标（回车生效）" /></label>
            </span>
            <button type="button" class="cl-pos-btn cl-pos-right" data-cam-pos="right" title="X +0.1（长按连续调整）">→</button>
            <button type="button" class="cl-pos-btn cl-pos-down" data-cam-pos="down" title="Z −0.1（长按连续调整）">↓</button>
          </span>
          ${modalBtnHtml("重置位置", "modal-btn", { id: "cl-cam-pos-reset", ...(S.cameraPosOrigin ? {} : { disabled: "" }), title: "恢复场景导出时的相机位置" })}
        </span>
      </div>
      <p class="modal-hint cl-cam-snap"></p>`
    : `<p class="modal-hint">当前场景未导出相机信息（场景中找不到相机），相机设置不可用。</p>`;

  const body = `
    <h3 class="cl-section-title">🎥 相机</h3>
    ${camSection}
    <p class="modal-hint">场景灯光（位置、强度、阴影等）请在左侧「💡 灯光层」编辑。</p>`;

  openModal("相机", body, `${cancelBtnHtml("关闭")}`);
  document.querySelector("#modal-root [data-cancel]")?.addEventListener("click", closeModal);

  const bgInput = document.getElementById("cl-cam-bg") as HTMLInputElement | null;
  const fovRange = document.getElementById("cl-cam-fov-range") as HTMLInputElement | null;
  const fovNum = document.getElementById("cl-cam-fov-num") as HTMLInputElement | null;

  let camBgPushed = false;
  let camFovPushed = false;

  if (bgInput) {
    bgInput.addEventListener("input", () => {
      if (!S.cameraInfo) return;
      if (!camBgPushed) {
        pushHistory();
        camBgPushed = true;
      }
      S.cameraInfo.backgroundColor = bgInput.value;
      const code = bgInput.parentElement?.querySelector("code");
      if (code) code.textContent = bgInput.value;
      draw();
    });
    bgInput.addEventListener("change", () => {
      camBgPushed = false;
    });
  }

  const applyFov = (fov: number, src: "range" | "num") => {
    if (!S.cameraInfo) return;
    const v = clampNum(fov, 1, 179);
    if (!camFovPushed) {
      pushHistory();
      camFovPushed = true;
    }
    S.cameraInfo.fieldOfView = v;
    if (src === "range" && fovNum) fovNum.value = String(v);
    if (src === "num" && fovRange) fovRange.value = String(clampNum(v, 20, 80));
    draw();
  };
  if (fovRange) {
    fovRange.addEventListener("input", () => applyFov(Number(fovRange.value), "range"));
    fovRange.addEventListener("change", () => {
      camFovPushed = false;
    });
  }
  if (fovNum) {
    fovNum.addEventListener("change", () => applyFov(Number(fovNum.value), "num"));
  }

  const CAM_POS_STEP = 0.1;
  const posXInput = document.getElementById("cl-cam-pos-x") as HTMLInputElement | null;
  const posZInput = document.getElementById("cl-cam-pos-z") as HTMLInputElement | null;
  const snapEl = document.querySelector<HTMLElement>("#modal-root .cl-cam-snap");

  const stepPos = (v: number, d: number): number => Math.round((v + d) * 1000) / 1000;

  const updatePosReadout = () => {
    const c = S.cameraInfo;
    if (!c) return;
    if (posXInput && document.activeElement !== posXInput) posXInput.value = fmt(c.position?.x);
    if (posZInput && document.activeElement !== posZInput) posZInput.value = fmt(c.position?.z);
    if (snapEl) {
      snapEl.textContent =
        `相机位置 (${fmt(c.position?.x)}, ${fmt(c.position?.y)}, ${fmt(c.position?.z)})，可用上方按钮微调（单击/长按，每次 0.1，Y 不变）或直接输入 X/Z` +
        ` · 俯角 ${fmt(c.pitch)}° / 朝向 ${fmt(c.yaw)}°` +
        ` · 裁面 ${fmt(c.nearClip)}–${fmt(c.farClip)}` +
        ` · Play 时以此位置为跟随中心（保留开场运镜与跟随微调）`;
    }
  };

  const stepOnce = (dir: string, push: boolean) => {
    if (!S.cameraInfo) return;
    if (push) pushHistory();
    if (!S.cameraInfo.position) S.cameraInfo.position = { x: 0, y: 0, z: 0 };
    const p = S.cameraInfo.position;
    if (dir === "up") p.z = stepPos(p.z, CAM_POS_STEP);
    else if (dir === "down") p.z = stepPos(p.z, -CAM_POS_STEP);
    else if (dir === "left") p.x = stepPos(p.x, -CAM_POS_STEP);
    else if (dir === "right") p.x = stepPos(p.x, CAM_POS_STEP);
    S.cameraInfo.positionEdited = true;
    updatePosReadout();
    draw();
  };

  document.querySelectorAll<HTMLButtonElement>("#modal-root [data-cam-pos]").forEach((btn) => {
    const dir = btn.dataset.camPos ?? "";
    let repeatDelay: number | undefined;
    let repeatTimer: number | undefined;
    let suppressClick = false;
    const stopRepeat = () => {
      window.clearTimeout(repeatDelay);
      window.clearInterval(repeatTimer);
      repeatDelay = undefined;
      repeatTimer = undefined;
    };
    btn.addEventListener("pointerdown", (e) => {
      e.preventDefault();
      stopRepeat();
      suppressClick = true;
      try {
        btn.setPointerCapture(e.pointerId);
      } catch {
        /* no pointer capture */
      }
      stepOnce(dir, true);
      repeatDelay = window.setTimeout(() => {
        repeatTimer = window.setInterval(() => stepOnce(dir, false), 70);
      }, 400);
    });
    btn.addEventListener("pointerup", stopRepeat);
    btn.addEventListener("pointercancel", stopRepeat);
    btn.addEventListener("pointerleave", stopRepeat);
    btn.addEventListener("click", () => {
      if (suppressClick) {
        suppressClick = false;
        return;
      }
      stepOnce(dir, true);
    });
  });

  const applyPosInput = (axis: "x" | "z", input: HTMLInputElement) => {
    if (!S.cameraInfo) return;
    const v = parseFloat(input.value);
    if (!isFinite(v)) {
      updatePosReadout();
      return;
    }
    if (!S.cameraInfo.position) S.cameraInfo.position = { x: 0, y: 0, z: 0 };
    const p = S.cameraInfo.position;
    const next = Math.round(v * 1000) / 1000;
    if (p[axis] === next) return;
    pushHistory();
    p[axis] = next;
    S.cameraInfo.positionEdited = true;
    updatePosReadout();
    draw();
  };
  posXInput?.addEventListener("change", () => applyPosInput("x", posXInput));
  posZInput?.addEventListener("change", () => applyPosInput("z", posZInput));

  const posReset = document.getElementById("cl-cam-pos-reset");
  posReset?.addEventListener("click", () => {
    const c = S.cameraInfo;
    const o = S.cameraPosOrigin;
    if (!c || !o) return;
    if (c.position && c.position.x === o.x && c.position.y === o.y && c.position.z === o.z) return;
    pushHistory();
    c.position = { x: o.x, y: o.y, z: o.z };
    c.positionEdited = true;
    updatePosReadout();
    draw();
  });

  updatePosReadout();
}

function normalizeHex(hex: string | undefined, fallback: string): string {
  return hex && /^#[0-9a-fA-F]{6}$/.test(hex) ? hex : fallback;
}

function clampNum(v: number, min: number, max: number): number {
  const n = Number(v);
  if (!Number.isFinite(n)) return min;
  return Math.min(max, Math.max(min, n));
}

function fmt(v: number | undefined): string {
  return v === undefined || v === null || !Number.isFinite(v) ? "?" : String(Math.round(v * 100) / 100);
}
