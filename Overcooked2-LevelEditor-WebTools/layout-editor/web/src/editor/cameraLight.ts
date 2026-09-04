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
 * - 相机：背景色（空洞主题下即游戏背景色）+ FOV；transform 为只读快照。
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
      <p class="modal-hint cl-cam-snap">
        相机快照（只读）：位置 (${fmt(cam.position?.x)}, ${fmt(cam.position?.y)}, ${fmt(cam.position?.z)})
        · 俯角 ${fmt(cam.pitch)}° / 朝向 ${fmt(cam.yaw)}°
        · 裁面 ${fmt(cam.nearClip)}–${fmt(cam.farClip)}
      </p>`
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
