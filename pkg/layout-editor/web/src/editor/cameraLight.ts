import { S } from "./state";
import { cancelBtnHtml, modalBtnHtml } from "../ui/views/button";
import { escHtml } from "./coords";
import { pushHistory } from "./historyOps";
import { draw } from "./render";
import { openModal, closeModal } from "../modals";
import { setStatus } from "./status";
import type { SplitScreenCameraSetting } from "../types";

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
  const ensureSplitCameras = (): SplitScreenCameraSetting[] => {
    if (!S.cameraInfo) return [];
    const info = S.cameraInfo;
    if (!info.splitScreenCameras) info.splitScreenCameras = [];
    const defOffset = { x: 0, y: 0, z: 0 };
    for (let i = 0; i < 4; i++) {
      if (!info.splitScreenCameras[i]) {
        info.splitScreenCameras[i] = {
          visible: true,
          offset: { ...defOffset },
          fieldOfView: info.fieldOfView ?? 0,
          size: 1,
        };
      } else if (!info.splitScreenCameras[i].offset) {
        info.splitScreenCameras[i].offset = { ...defOffset };
      }
    }
    return info.splitScreenCameras;
  };
  const splitCameras = ensureSplitCameras();
  const splitPalette = ["#e74c3c", "#3498db", "#2ecc71", "#f1c40f"];
  const fmtNum = (v: number | undefined): string =>
    Number.isFinite(v) ? String(Math.round((v as number) * 1000) / 1000) : "0";
  const splitTabs = [0, 1, 2, 3]
    .map(
      (i) => `<button type="button" class="cl-split-tab${i === 0 ? " active" : ""}" data-split-tab="${i}">
        <span class="cl-split-dot" style="background:${splitPalette[i]}"></span>镜头 ${i + 1}
      </button>`
    )
    .join("");
  const splitPanels = [0, 1, 2, 3]
    .map((i) => {
      const c = splitCameras[i];
      const off = c.offset ?? { x: 0, y: 0, z: 0 };
      const size = Number.isFinite(c.size) && c.size > 0 ? c.size : 1;
      return `
      <div class="cl-split-panel${i === 0 ? " active" : ""}" data-split-panel="${i}">
        <div class="cl-split-panel-head">
          <label class="modal-check" title="取消勾选 = 编辑器不画该参考，且游戏内该玩家画面不生成（保留黑框）"><input type="checkbox" id="cl-cam-vis-${i}" ${c.visible === false ? "" : "checked"}/> 启用镜头</label>
          <button type="button" class="cl-split-reset" data-split-reset="${i}" title="恢复默认偏移 / FOV / 大小">↺ 默认</button>
        </div>
        <div class="cl-split-grid">
          <label>X 偏移<input type="number" id="cl-cam-offx-${i}" step="0.1" value="${fmtNum(off.x)}" /></label>
          <label>Y 高度<input type="number" id="cl-cam-offy-${i}" step="0.1" value="${fmtNum(off.y)}" /></label>
          <label>Z 偏移<input type="number" id="cl-cam-offz-${i}" step="0.1" value="${fmtNum(off.z)}" /></label>
          <label>FOV<span class="cl-split-inline"><input type="number" id="cl-cam-fov-${i}" min="0" max="179" step="1" value="${fmtNum(c.fieldOfView)}" /><span class="muted">0=主</span></span></label>
          <label class="cl-split-size">大小<span class="cl-split-inline"><input type="range" id="cl-cam-size-${i}" min="0.25" max="4" step="0.05" value="${size}" /><code id="cl-cam-size-${i}-val">${size.toFixed(2)}</code></span></label>
        </div>
      </div>`;
    })
    .join("");
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
      <div class="modal-field">
        <span>多镜头分屏</span>
        <span>
          <label class="modal-check"><input type="checkbox" id="cl-cam-split" ${cam.splitScreen ? "checked" : ""}/> 按厨师数量自动分屏</label>
        </span>
      </div>
      <p class="modal-hint">分屏按场景内本地厨师数量自动决定：单人双厨师 = 2 镜头，2/3/4 人 = 2/3/4 镜头。三人为上方 60% 宽居中 + 下方左右各 50%（左右留黑边）。开启后运行时为单向模式：原版镜头屏蔽、忽略「出发点位置」偏移，且不会切回单镜头。</p>
      <div id="cl-split-cams" style="${cam.splitScreen ? "" : "display:none"}">
        <p class="modal-hint">分屏镜头：每个厨师一台独立相机，可单独启用/隐藏（隐藏=编辑器不画参考且游戏内不出该画面）、在“主相机单目标默认跟随”基础上调 X/Y/Z 额外偏移、FOV 与大小。画布会用对应颜色标出全部已启用镜头的范围；主镜头参考隐藏。</p>
        <div class="cl-split-tabs" role="tablist">${splitTabs}</div>
        <div class="cl-split-panels">${splitPanels}</div>
      </div>
      <div class="modal-field cl-cam-pos-field" id="cl-cam-main-pos" style="${cam.splitScreen ? "display:none" : ""}">
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

  const splitInput = document.getElementById("cl-cam-split") as HTMLInputElement | null;
  const splitSizesEl = document.getElementById("cl-split-cams");
  const mainPosEl = document.getElementById("cl-cam-main-pos");
  if (splitInput) {
    splitInput.addEventListener("change", () => {
      if (!S.cameraInfo) return;
      pushHistory();
      S.cameraInfo.splitScreen = splitInput.checked;
      if (splitInput.checked) ensureSplitCameras();
      if (splitSizesEl) splitSizesEl.style.display = splitInput.checked ? "" : "none";
      if (mainPosEl) mainPosEl.style.display = splitInput.checked ? "none" : "";
      setStatus(
        splitInput.checked
          ? "已开启多镜头分屏（写回后生效；运行时单向，原版镜头将被屏蔽）"
          : "已关闭多镜头分屏"
      );
      draw();
    });
  }

  document.querySelectorAll<HTMLButtonElement>("#modal-root [data-split-tab]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const idx = Number(btn.dataset.splitTab);
      document.querySelectorAll<HTMLButtonElement>("#modal-root [data-split-tab]").forEach((b) => {
        b.classList.toggle("active", b === btn);
      });
      document.querySelectorAll<HTMLElement>("#modal-root [data-split-panel]").forEach((p) => {
        p.classList.toggle("active", Number(p.dataset.splitPanel) === idx);
      });
    });
  });

  document.querySelectorAll<HTMLButtonElement>("#modal-root [data-split-reset]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const i = Number(btn.dataset.splitReset);
      pushHistory();
      const c = ensureSplitCameras()[i];
      c.offset = { x: 0, y: 0, z: 0 };
      c.fieldOfView = S.cameraInfo?.fieldOfView ?? 0;
      c.size = 1;
      c.visible = true;
      const setVal = (id: string, v: number) => {
        const el = document.getElementById(id) as HTMLInputElement | null;
        if (el) el.value = String(v);
      };
      setVal(`cl-cam-offx-${i}`, 0);
      setVal(`cl-cam-offy-${i}`, 0);
      setVal(`cl-cam-offz-${i}`, 0);
      setVal(`cl-cam-fov-${i}`, Math.round(c.fieldOfView));
      const sizeEl = document.getElementById(`cl-cam-size-${i}`) as HTMLInputElement | null;
      if (sizeEl) sizeEl.value = "1";
      const readout = document.getElementById(`cl-cam-size-${i}-val`);
      if (readout) readout.textContent = "1.00";
      const vis = document.getElementById(`cl-cam-vis-${i}`) as HTMLInputElement | null;
      if (vis) vis.checked = true;
      draw();
    });
  });

  for (let i = 0; i < 4; i++) {
    const vis = document.getElementById(`cl-cam-vis-${i}`) as HTMLInputElement | null;
    if (vis) {
      vis.addEventListener("change", () => {
        pushHistory();
        ensureSplitCameras()[i].visible = vis.checked;
        draw();
      });
    }

    for (const axis of ["x", "y", "z"] as const) {
      const input = document.getElementById(`cl-cam-off${axis}-${i}`) as HTMLInputElement | null;
      if (!input) continue;
      input.addEventListener("change", () => {
        const v = Number(input.value);
        if (!Number.isFinite(v)) return;
        pushHistory();
        const c = ensureSplitCameras()[i];
        if (!c.offset) c.offset = { x: 0, y: 0, z: 0 };
        c.offset[axis] = Math.round(v * 1000) / 1000;
        draw();
      });
    }

    const fov = document.getElementById(`cl-cam-fov-${i}`) as HTMLInputElement | null;
    if (fov) {
      fov.addEventListener("change", () => {
        const v = clampNum(Number(fov.value), 0, 179);
        pushHistory();
        ensureSplitCameras()[i].fieldOfView = Math.round(v);
        fov.value = String(Math.round(v));
        draw();
      });
    }

    const size = document.getElementById(`cl-cam-size-${i}`) as HTMLInputElement | null;
    const readout = document.getElementById(`cl-cam-size-${i}-val`);
    if (size) {
      let pushed = false;
      size.addEventListener("input", () => {
        if (!pushed) {
          pushHistory();
          pushed = true;
        }
        const v = clampNum(Number(size.value), 0.25, 4);
        ensureSplitCameras()[i].size = v;
        if (readout) readout.textContent = v.toFixed(2);
        draw();
      });
      size.addEventListener("change", () => {
        pushed = false;
      });
    }
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
