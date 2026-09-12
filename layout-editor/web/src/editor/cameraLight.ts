import { S } from "./state";
import { escHtml } from "./coords";
import { pushHistory } from "./historyOps";
import { draw } from "./render";
import { openModal, closeModal } from "../modals";
import { setStatus } from "./status";

/** 灯光类型标签（LightType 枚举：0=Spot 1=Directional 2=Point 3=Area）。 */
const LIGHT_TYPE_LABELS: Record<number, string> = {
  0: "聚光灯",
  1: "方向光",
  2: "点光源",
  3: "面光源",
};

function lightTypeLabel(t: number): string {
  return LIGHT_TYPE_LABELS[t] ?? `类型${t}`;
}

/**
 * 打开「相机 / 灯光」编辑弹窗。
 * - 相机：背景色（空洞主题下即游戏背景色）+ FOV + 出发点位置（X/Z 方向键，每次 0.1，Y 不变）。
 * - 灯光：Art/Lights 非 prefab 灯光的颜色 / 强度 / 范围 / 角度 / 启用，可增删。
 * 修改即时反映到画布（视野范围、空洞底色），写回 Unity 时随全量保存携带。
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
          <button type="button" class="modal-btn" id="cl-cam-pos-reset" ${S.cameraPosOrigin ? "" : "disabled"} title="恢复场景导出时的相机位置">重置位置</button>
        </span>
      </div>
      <p class="modal-hint cl-cam-snap"></p>`
    : `<p class="modal-hint">当前场景未导出相机信息（场景中找不到相机），相机设置不可用。</p>`;

  const body = `
    <h3 class="cl-section-title">🎥 相机</h3>
    ${camSection}
    <h3 class="cl-section-title">💡 灯光（Art/Lights）</h3>
    <div class="modal-scroll cl-light-list" id="cl-light-list"></div>
    <div class="cl-add-row">
      <button type="button" class="modal-btn" id="cl-add-light">＋ 新建灯光</button>
    </div>`;

  openModal(
    "相机 / 灯光",
    body,
    `<button type="button" class="modal-btn" data-cancel>关闭</button>`
  );
  document.querySelector("#modal-root [data-cancel]")?.addEventListener("click", closeModal);

  // —— 相机控件 ——
  const bgInput = document.getElementById("cl-cam-bg") as HTMLInputElement | null;
  const fovRange = document.getElementById("cl-cam-fov-range") as HTMLInputElement | null;
  const fovNum = document.getElementById("cl-cam-fov-num") as HTMLInputElement | null;

  /** 交互级撤销：一次拖动/取色只压一条历史（首个 input 前压入改前快照）。 */
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

  // —— 出发点位置方向键（单击步进 ±0.1，长按连续调整；X/Z 也可直接输入；Y 永不改动） ——
  const CAM_POS_STEP = 0.1;
  const posXInput = document.getElementById("cl-cam-pos-x") as HTMLInputElement | null;
  const posZInput = document.getElementById("cl-cam-pos-z") as HTMLInputElement | null;
  const snapEl = document.querySelector<HTMLElement>("#modal-root .cl-cam-snap");

  /** 步进后 round 到 0.001，消除 0.1 累加的浮点噪声。 */
  const stepPos = (v: number, d: number): number => Math.round((v + d) * 1000) / 1000;

  const updatePosReadout = () => {
    const c = S.cameraInfo;
    if (!c) return;
    // 输入框聚焦时不回写，避免打断正在输入的数字
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

  /** 方向步进一次；push=true 压一条撤销历史（一次按压会话只在首步压入）。 */
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
        // 老旧浏览器无 pointer capture，pointerleave 兜底停止
      }
      stepOnce(dir, true);
      // 长按：400ms 后进入 70ms 连发
      repeatDelay = window.setTimeout(() => {
        repeatTimer = window.setInterval(() => stepOnce(dir, false), 70);
      }, 400);
    });
    btn.addEventListener("pointerup", stopRepeat);
    btn.addEventListener("pointercancel", stopRepeat);
    btn.addEventListener("pointerleave", stopRepeat);
    // 键盘激活（Enter/Space 只触发 click 而无 pointerdown）兜底单步；
    // 鼠标/触摸的 click 已在 pointerdown 步进过，抑制避免重复。
    btn.addEventListener("click", () => {
      if (suppressClick) {
        suppressClick = false;
        return;
      }
      stepOnce(dir, true);
    });
  });

  // —— X/Z 直接输入（change = 回车/失焦提交，每次提交一条撤销历史） ——
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

  // —— 灯光列表 ——
  const listEl = document.getElementById("cl-light-list");
  if (listEl) {
    renderLightList(listEl);
    const addBtn = document.getElementById("cl-add-light");
    addBtn?.addEventListener("click", () => {
      addNewLight();
      renderLightList(listEl);
    });
  }
}

function renderLightList(listEl: HTMLElement): void {
  if (S.lights.length === 0) {
    listEl.innerHTML = `<p class="modal-hint">场景没有非 prefab 灯光，可点「新建灯光」添加。</p>`;
    return;
  }

  listEl.innerHTML = S.lights
    .map((l, i) => {
      const isSpot = l.lightType === 0;
      const isPoint = l.lightType === 2;
      const typeSel = [0, 1, 2]
        .map(
          (t) =>
            `<option value="${t}" ${l.lightType === t ? "selected" : ""}>${lightTypeLabel(t)}</option>`
        )
        .join("");
      return `
      <div class="cl-light-row" data-idx="${i}">
        <div class="cl-light-head">
          <span class="cl-light-name" title="${escHtml(l.hierarchyPath)}">${escHtml(l.displayName || l.hierarchyPath)}</span>
          <code class="muted">${escHtml(l.hierarchyPath)}</code>
          <label class="modal-check inline"><input type="checkbox" data-l-enable ${l.enabled ? "checked" : ""} /> 启用</label>
          <button type="button" class="modal-btn cl-light-del" data-l-del title="删除该灯光（写回后从场景移除）">🗑</button>
        </div>
        <div class="cl-light-ctrl">
          <label class="cl-ctrl">颜色<input type="color" data-l-color value="${normalizeHex(l.color, "#ffffff")}" /></label>
          <label class="cl-ctrl">类型<select data-l-type>${typeSel}</select></label>
          <label class="cl-ctrl">强度<input type="number" data-l-intensity min="0" max="8" step="0.05" value="${l.intensity}" /></label>
          ${isSpot || isPoint ? `<label class="cl-ctrl">范围<input type="number" data-l-range min="0.01" step="0.5" value="${l.range}" /></label>` : ""}
          ${isSpot ? `<label class="cl-ctrl">角度<input type="number" data-l-spot min="1" max="179" step="1" value="${l.spotAngle}" /></label>` : ""}
        </div>
      </div>`;
    })
    .join("");

  listEl.querySelectorAll<HTMLElement>(".cl-light-row").forEach((row) => {
    const idx = Number(row.dataset.idx);
    const light = () => S.lights[idx];

    // 颜色取色器：input 实时预览，首次 input 前压一条历史（change 即关闭取色时复位）。
    row.querySelector<HTMLInputElement>("[data-l-color]")?.addEventListener("input", (e) => {
      if (row.dataset.colorPushed !== "1") {
        pushHistory();
        row.dataset.colorPushed = "1";
      }
      light().color = (e.target as HTMLInputElement).value;
      draw();
    });
    row.querySelector<HTMLInputElement>("[data-l-color]")?.addEventListener("change", () => {
      row.dataset.colorPushed = "";
    });

    // change 提交型控件：每次提交前压历史再变更。
    row.querySelector<HTMLInputElement>("[data-l-enable]")?.addEventListener("change", (e) => {
      pushHistory();
      light().enabled = (e.target as HTMLInputElement).checked;
      draw();
    });
    row.querySelector<HTMLSelectElement>("[data-l-type]")?.addEventListener("change", (e) => {
      pushHistory();
      light().lightType = Number((e.target as HTMLSelectElement).value);
      renderLightList(listEl);
      draw();
    });
    row.querySelector<HTMLInputElement>("[data-l-intensity]")?.addEventListener("change", (e) => {
      pushHistory();
      light().intensity = Math.max(0, Number((e.target as HTMLInputElement).value) || 0);
      draw();
    });
    row.querySelector<HTMLInputElement>("[data-l-range]")?.addEventListener("change", (e) => {
      pushHistory();
      light().range = Math.max(0.01, Number((e.target as HTMLInputElement).value) || 0.01);
      draw();
    });
    row.querySelector<HTMLInputElement>("[data-l-spot]")?.addEventListener("change", (e) => {
      pushHistory();
      light().spotAngle = clampNum(Number((e.target as HTMLInputElement).value) || 30, 1, 179);
      draw();
    });
    row.querySelector<HTMLButtonElement>("[data-l-del]")?.addEventListener("click", () => {
      pushHistory();
      S.lights.splice(idx, 1);
      renderLightList(listEl);
      draw();
    });
  });
}

function addNewLight(): void {
  const used = new Set(S.lights.map((l) => l.hierarchyPath));
  let n = S.lights.length + 1;
  let path = `Art/Lights/light${n}`;
  while (used.has(path)) {
    n++;
    path = `Art/Lights/light${n}`;
  }
  pushHistory();
  S.lights.push({
    hierarchyPath: path,
    displayName: `light${n}`,
    lightType: 2,
    color: "#ffffff",
    intensity: 1,
    range: 10,
    spotAngle: 30,
    enabled: true,
    eulerAngles: { x: 0, y: 0, z: 0 },
  });
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
