import { S } from "./state";
import { cancelBtnHtml, modalBtnHtml } from "../ui/views/button";
import { escHtml } from "./coords";
import { pushHistory } from "./historyOps";
import { draw } from "./render";
import { openModal, closeModal } from "../modals";
import { setStatus } from "./status";
import { dom } from "./dom";
import type { CameraIntroSetting, SplitScreenCameraSetting } from "../types";

/** 相机弹窗顶层 tab：主视野 / 进场动画 / 多镜头分屏。 */
export type CameraModalTab = "view" | "intro" | "split";

const CAMERA_TAB_LABELS: Record<CameraModalTab, string> = {
  view: "📷 相机视野",
  intro: "🎬 进场动画",
  split: "🔲 多镜头分屏",
};

/**
 * 打开「相机」编辑弹窗（三个 tab：相机视野 / 进场动画 / 多镜头分屏）。
 * 灯光请在「💡 灯光层」编辑。initialTab：初始打开的 tab（画布选点返回时回
 * 「进场动画」tab）。
 */
export function openCameraLightModal(initialTab: CameraModalTab = "view"): void {
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
  const introVal: Partial<CameraIntroSetting> = cam?.intro ?? {};
  const introMode = cam?.intro?.mode ?? "vanilla";
  const splitPalette = ["#e74c3c", "#3498db", "#2ecc71", "#f1c40f"];
  const fmtNum = (v: number | undefined): string =>
    Number.isFinite(v) ? String(Math.round((v as number) * 1000) / 1000) : "0";
  const splitLayoutDiagram = `
    <div class="cl-split-diagram">
      <div class="cl-split-diagram-row">
        <div class="cl-split-diagram-item">
          <div class="cl-split-diagram-stage two">
            <div class="cl-split-pane" style="background:#e74c3c">1</div>
            <div class="cl-split-pane" style="background:#3498db">2</div>
          </div>
          <div class="cl-split-diagram-label">2 人 · 左右各 50%</div>
        </div>
        <div class="cl-split-diagram-item">
          <div class="cl-split-diagram-stage three">
            <div class="cl-split-pane pane1" style="background:#e74c3c">1</div>
            <div class="cl-split-pane" style="background:#3498db">2</div>
            <div class="cl-split-pane" style="background:#2ecc71">3</div>
          </div>
          <div class="cl-split-diagram-label">3 人 · 上 60% / 下各 50%</div>
        </div>
        <div class="cl-split-diagram-item">
          <div class="cl-split-diagram-stage four">
            <div class="cl-split-pane" style="background:#e74c3c">1</div>
            <div class="cl-split-pane" style="background:#3498db">2</div>
            <div class="cl-split-pane" style="background:#2ecc71">3</div>
            <div class="cl-split-pane" style="background:#f1c40f">4</div>
          </div>
          <div class="cl-split-diagram-label">4 人 · 2×2</div>
        </div>
      </div>
      <p class="modal-hint">左右分屏每格宽 50%、高 100%，水平视野约为主镜头的 50%；3 人上方镜头约 120%、下方约 100%；4 人每格约 100%。画布上的彩色视锥就是各镜头实际保留范围。</p>
    </div>`;
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

  const topTabs = (Object.keys(CAMERA_TAB_LABELS) as CameraModalTab[])
    .map(
      (t) =>
        `<button type="button" class="cl-split-tab cl-top-tab${t === initialTab ? " active" : ""}" data-cl-tab="${t}">${CAMERA_TAB_LABELS[t]}</button>`
    )
    .join("");

  // ---- tab 1：相机视野（背景色 / FOV / 出发点位置）
  const viewPanelHtml = (): string => {
    if (!cam) return "";
    return `
      <div class="cl-cols">
        <div class="cl-col">
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
        </div>
        <div class="cl-col">
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
          <p class="modal-hint cl-cam-snap"></p>
        </div>
      </div>`;
  };

  // ---- tab 2：进场动画
  const introPanelHtml = (): string => {
    if (!cam) return "";
    return `
      <div class="cl-cols">
        <div class="cl-col">
          <div class="modal-field" id="cl-cam-intro-field">
            <span>进场模式</span>
            <span>
              <select id="cl-cam-intro-mode" class="cl-intro-select" title="关卡开场相机运镜（写回后生效）；无论何种模式，结束画面恒为最终镜头">
                <option value="vanilla"${introMode === "vanilla" ? " selected" : ""}>原版拉近（默认）</option>
                <option value="none"${introMode === "none" ? " selected" : ""}>无进场 · 直接最终画面</option>
                <option value="pullBack"${introMode === "pullBack" ? " selected" : ""}>拉近进场（可调）</option>
                <option value="rotateLeft"${introMode === "rotateLeft" ? " selected" : ""}>左转进场</option>
                <option value="rotateRight"${introMode === "rotateRight" ? " selected" : ""}>右转进场</option>
                <option value="focus"${introMode === "focus" ? " selected" : ""}>聚焦某点后拉回</option>
                <option value="tour"${introMode === "tour" ? " selected" : ""}>巡回进场（A → B → 归位）</option>
                <option value="dollyIn"${introMode === "dollyIn" ? " selected" : ""}>推近进场（远端滑入）</option>
                <option value="dive"${introMode === "dive" ? " selected" : ""}>俯冲进场（高处俯冲）</option>
                <option value="orbitLeft"${introMode === "orbit" && (cam.intro?.orbitDirection ?? "right") === "left" ? " selected" : ""}>左环绕进场</option>
                <option value="orbitRight"${introMode === "orbit" && (cam.intro?.orbitDirection ?? "right") !== "left" ? " selected" : ""}>右环绕进场</option>
                <option value="raft"${introMode === "raft" ? " selected" : ""}>木筏主题（官方变体）</option>
                <option value="space"${introMode === "space" ? " selected" : ""}>太空主题（官方变体）</option>
              </select>
            </span>
          </div>
          <div id="cl-cam-intro-params">
            <div class="cl-split-grid cl-intro-grid">
              <label id="cl-cam-intro-dur-w">时长（秒）<input type="number" id="cl-cam-intro-dur" min="0.5" max="6" step="0.1" value="${fmtNum(introVal.duration ?? 2)}" /></label>
              <label id="cl-cam-intro-dist-w">起始距离<span class="cl-split-inline"><input type="number" id="cl-cam-intro-dist" min="0" max="15" step="0.5" value="${fmtNum(introVal.startDistance ?? 5)}" /><span class="muted">米</span></span></label>
              <label id="cl-cam-intro-angle-w">旋转幅度<span class="cl-split-inline"><input type="number" id="cl-cam-intro-angle" min="5" max="90" step="1" value="${fmtNum(introVal.rotateAngle ?? 30)}" /><span class="muted">°</span></span></label>
              <label id="cl-cam-intro-diveh-w">俯冲高度<span class="cl-split-inline"><input type="number" id="cl-cam-intro-diveh" min="2" max="40" step="1" value="${fmtNum(introVal.diveHeight ?? 10)}" /><span class="muted">米</span></span></label>
              <label id="cl-cam-intro-orbitang-w">环绕角度<span class="cl-split-inline"><input type="number" id="cl-cam-intro-orbitang" min="90" max="360" step="10" value="${fmtNum(introVal.orbitAngle ?? 180)}" /><span class="muted">°</span></span></label>
            </div>
            <div class="cl-intro-focus-row" id="cl-cam-intro-focus-w">
              <span class="cl-intro-focus-label">聚焦目标</span>
              <span class="cl-split-inline">
                <code id="cl-cam-intro-focus-val">(${fmtNum(introVal.focusTarget?.x ?? 0)}, ${fmtNum(introVal.focusTarget?.z ?? 0)})</code>
                <button type="button" class="cl-split-reset" id="cl-cam-intro-pick-focus" title="关闭弹窗后在画布上点击选择（X/Z 均生效；远处也可选，超画面范围会提示；Esc 取消）">📍 从画布选点</button>
              </span>
            </div>
            <div class="cl-intro-focus-row" id="cl-cam-intro-tourA-w">
              <span class="cl-intro-focus-label">巡回 A · X</span>
              <span class="cl-split-inline">
                <code id="cl-cam-intro-tourA-val">(${fmtNum(introVal.tourA?.x ?? 0)}, ${fmtNum(introVal.tourA?.z ?? 0)})</code>
                <button type="button" class="cl-split-reset" id="cl-cam-intro-pick-tourA" title="巡回起点：关闭弹窗后在画布上点击选择（只取 X，Z 恒跟随主相机；远处也可选，超画面范围会提示；Esc 取消）">📍 画布选点</button>
              </span>
            </div>
            <div class="cl-intro-focus-row" id="cl-cam-intro-tourB-w">
              <span class="cl-intro-focus-label">巡回 B · X</span>
              <span class="cl-split-inline">
                <code id="cl-cam-intro-tourB-val">(${fmtNum(introVal.tourB?.x ?? 0)}, ${fmtNum(introVal.tourB?.z ?? 0)})</code>
                <button type="button" class="cl-split-reset" id="cl-cam-intro-pick-tourB" title="巡回途经点：关闭弹窗后在画布上点击选择（只取 X，Z 恒跟随主相机；远处也可选，超画面范围会提示；Esc 取消）">📍 画布选点</button>
              </span>
            </div>
          </div>
        </div>
        <div class="cl-col cl-intro-side">
          <div class="cl-intro-demo-wrap">
            <div class="cl-intro-demo-title">动画演示（俯视示意，与烘焙同公式循环播放）</div>
            <canvas id="cl-intro-demo" class="cl-intro-demo"></canvas>
          </div>
          <p class="modal-hint" id="cl-cam-intro-hint"></p>
          <div class="cl-mode-cheat">
            <div class="cl-mode-cheat-title">模式速览</div>
            <ul>
              <li><b>原版拉近</b>贴近厨房特写 → 2 秒滑回全景（PullBackIntro）</li>
              <li><b>拉近进场</b>同原版，时长与起始距离可调</li>
              <li><b>左转 / 右转</b>侧向偏航旋入正位，可叠加距离成弧形掠入</li>
              <li><b>聚焦某点</b>水平平移到目标点特写 → 滑回（X/Z 全向，同高度构图，无黑屏）</li>
              <li><b>巡回进场</b>A → B → 归位水平巡航（仅 X 轴，Z 跟随主相机）</li>
              <li><b>推近进场</b>从远端沿视轴滑入（原版反向）</li>
              <li><b>俯冲进场</b>更高更远处俯冲滑入（航拍感）</li>
              <li><b>左/右环绕</b>绕最终位圆弧环绕滑入（90°–360°）</li>
              <li><b>木筏 / 太空</b>官方 DLC 变体，待机行为一并切换</li>
              <li><b>无进场</b>开场即最终画面</li>
            </ul>
          </div>
        </div>
      </div>`;
  };

  // ---- tab 3：多镜头分屏
  const splitPanelHtml = (): string => {
    if (!cam) return "";
    return `
      <div class="cl-cols">
        <div class="cl-col">
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
        </div>
        <div class="cl-col cl-split-side">
          ${splitLayoutDiagram}
        </div>
      </div>`;
  };

  const camSection = cam
    ? `
      <div class="cl-split-tabs cl-top-tabs" role="tablist">${topTabs}</div>
      <div data-cl-panel="view" class="cl-top-panel${initialTab === "view" ? "" : " cl-panel-hidden"}">${viewPanelHtml()}</div>
      <div data-cl-panel="intro" class="cl-top-panel${initialTab === "intro" ? "" : " cl-panel-hidden"}">${introPanelHtml()}</div>
      <div data-cl-panel="split" class="cl-top-panel${initialTab === "split" ? "" : " cl-panel-hidden"}">${splitPanelHtml()}</div>`
    : `<p class="modal-hint">当前场景未导出相机信息（场景中找不到相机），相机设置不可用。</p>`;

  const body = `
    <h3 class="cl-section-title">🎥 相机</h3>
    ${camSection}
    <p class="modal-hint">场景灯光（位置、强度、阴影等）请在左侧「💡 灯光层」编辑。</p>`;

  // 大面板（camera-wide）只用于进场动画 / 多镜头分屏 tab；「相机视野」tab 保持
  // 原始小宽度（控制项少，宽面板空旷）。随顶层 tab 切换动态增删类。
  openModal("相机", body, `${cancelBtnHtml("关闭")}`, {
    panelClass: initialTab === "view" ? "" : "camera-wide",
    id: "camera",
  });
  document.querySelector("#modal-root [data-cancel]")?.addEventListener("click", closeModal);

  // ---- 顶层 tab 切换（相机视野 / 进场动画 / 多镜头分屏）+ 面板尺寸联动
  document.querySelectorAll<HTMLButtonElement>("#modal-root [data-cl-tab]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const tab = btn.dataset.clTab as CameraModalTab;
      document.querySelectorAll<HTMLButtonElement>("#modal-root [data-cl-tab]").forEach((b) => {
        b.classList.toggle("active", b === btn);
      });
      document.querySelectorAll<HTMLElement>("#modal-root [data-cl-panel]").forEach((p) => {
        if (p.dataset.clPanel) p.classList.toggle("cl-panel-hidden", p.dataset.clPanel !== tab);
      });
      const panel = document.querySelector<HTMLElement>("#modal-root .modal-panel");
      if (panel) panel.classList.toggle("camera-wide", tab !== "view");
    });
  });

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
      syncIntroAvail();
      setStatus(
        splitInput.checked
          ? "已开启多镜头分屏（写回后生效；运行时单向，原版镜头将被屏蔽）"
          : "已关闭多镜头分屏"
      );
      draw();
    });
  }

  // ---------------------------------------------------------------- 进场动画
  const introModeSel = document.getElementById("cl-cam-intro-mode") as HTMLSelectElement | null;
  const introParamsEl = document.getElementById("cl-cam-intro-params");
  const introFieldEl = document.getElementById("cl-cam-intro-field");
  const introHintEl = document.getElementById("cl-cam-intro-hint");
  const introDurW = document.getElementById("cl-cam-intro-dur-w");
  const introDistW = document.getElementById("cl-cam-intro-dist-w");
  const introAngleW = document.getElementById("cl-cam-intro-angle-w");
  const introFocusW = document.getElementById("cl-cam-intro-focus-w");
  const introFocusVal = document.getElementById("cl-cam-intro-focus-val");
  const introTourAW = document.getElementById("cl-cam-intro-tourA-w");
  const introTourAVal = document.getElementById("cl-cam-intro-tourA-val");
  const introTourBW = document.getElementById("cl-cam-intro-tourB-w");
  const introTourBVal = document.getElementById("cl-cam-intro-tourB-val");
  const introPickFocusBtn = document.getElementById("cl-cam-intro-pick-focus") as HTMLButtonElement | null;
  const introPickTourABtn = document.getElementById("cl-cam-intro-pick-tourA") as HTMLButtonElement | null;
  const introPickTourBBtn = document.getElementById("cl-cam-intro-pick-tourB") as HTMLButtonElement | null;
  const introDur = document.getElementById("cl-cam-intro-dur") as HTMLInputElement | null;
  const introDist = document.getElementById("cl-cam-intro-dist") as HTMLInputElement | null;
  const introAngle = document.getElementById("cl-cam-intro-angle") as HTMLInputElement | null;
  const introDiveHW = document.getElementById("cl-cam-intro-diveh-w");
  const introDiveH = document.getElementById("cl-cam-intro-diveh") as HTMLInputElement | null;
  const introOrbitAngW = document.getElementById("cl-cam-intro-orbitang-w");
  const introOrbitAng = document.getElementById("cl-cam-intro-orbitang") as HTMLInputElement | null;

  const CUSTOM_INTRO_MODES = new Set([
    "pullBack",
    "rotateLeft",
    "rotateRight",
    "focus",
    "tour",
    "dollyIn",
    "dive",
    "orbit",
  ]);
  const isCustomIntro = (m: string | undefined): boolean => !!m && CUSTOM_INTRO_MODES.has(m);

  const ensureIntro = (): CameraIntroSetting | null => {
    if (!S.cameraInfo) return null;
    if (!S.cameraInfo.intro) S.cameraInfo.intro = { mode: "vanilla" };
    return S.cameraInfo.intro;
  };

  const INTRO_HINTS: Record<CameraIntroSetting["mode"], string> = {
    vanilla: "原版进场：镜头先贴近厨房（约 5 米）再 2 秒滑回全景（PullBackIntro）。",
    none: "禁用开场运镜：进入关卡即最终画面。",
    pullBack: "参数化原版拉近：起始沿视轴贴近厨房 → 拉回全景；时长与起始距离可调。",
    rotateLeft: "开场相机自右侧偏航旋入正位（向左旋回）；可叠加起始距离形成弧形掠入。",
    rotateRight: "开场相机自左侧偏航旋入正位（向右旋回）；可叠加起始距离形成弧形掠入。",
    focus: "镜头水平平移到目标点上方特写（X/Z 均生效，全向平移）→ 滑回最终画面。高度/朝向与主相机完全一致（画面= 最终画面平移，无贴地黑屏）；远处也可选（超出画面范围会提示），点完自动回到本页。",
    tour: "巡回进场：镜头在 A、B 两点的 X 轴上方水平巡航（A → B → 归位）。高度/朝向/Z 与主相机完全一致（画面= 最终画面平移，无贴地黑屏）；选点只取 X（Z 恒跟随主相机），大地图可巡到远处区域——超出主相机画面范围时状态栏提示（途中可能经过空旷区域）。",
    dollyIn: "推近进场：起始在最终镜头的远端（沿视轴后退「起始距离」米）→ 平滑滑入最终画面（原版拉近的反向运镜，「远镜头进场」经典款）。",
    dive: "俯冲进场：起始比最终镜头更高（「俯冲高度」米）且沿视轴更远 → 俯冲滑入最终画面（航拍俯冲感）。",
    orbit: "环绕进场：绕最终镜头位置竖直轴圆弧环绕滑入（角度 90°–360°，可转整圈；「起始距离」= 环绕半径）；朝向恒为最终俯角，构图连续。",
    raft: "官方木筏主题变体：进场 + 待机持续摇曳（原版 DLC 同款，待机行为一并切换）。",
    space: "官方太空主题变体：进场 + 待机 roll 微旋循环（原版 DLC 同款，待机行为一并切换）。",
  };

  /** 起始距离参与的模式（orbit 语义为环绕半径；focus/tour 水平平移不使用）。 */
  const usesStartDistance = (m: string): boolean =>
    m === "pullBack" ||
    m === "rotateLeft" ||
    m === "rotateRight" ||
    m === "dollyIn" ||
    m === "dive" ||
    m === "orbit";

  const syncIntroUI = (): void => {
    const it = S.cameraInfo?.intro;
    const mode = it?.mode ?? "vanilla";
    const custom = isCustomIntro(mode);
    if (introParamsEl) introParamsEl.style.display = custom ? "" : "none";
    if (introDurW) introDurW.style.display = custom ? "" : "none";
    if (introDistW) {
      introDistW.style.display = custom && usesStartDistance(mode) ? "" : "none";
      const labelEl = introDistW.firstChild as HTMLElement | null;
      if (labelEl) labelEl.textContent = mode === "orbit" ? "环绕半径" : "起始距离";
    }
    if (introAngleW) introAngleW.style.display = mode === "rotateLeft" || mode === "rotateRight" ? "" : "none";
    if (introDiveHW) introDiveHW.style.display = mode === "dive" ? "" : "none";
    if (introOrbitAngW) introOrbitAngW.style.display = mode === "orbit" ? "" : "none";
    if (introFocusW) introFocusW.style.display = mode === "focus" ? "" : "none";
    if (introTourAW) introTourAW.style.display = mode === "tour" ? "" : "none";
    if (introTourBW) introTourBW.style.display = mode === "tour" ? "" : "none";
    syncIntroAvail();
  };

  /** 分屏开启时禁用进场配置（配置保留但运行时不可见）。 */
  const syncIntroAvail = (): void => {
    const disabled = !!S.cameraInfo?.splitScreen;
    if (introModeSel) introModeSel.disabled = disabled;
    [introDur, introDist, introAngle, introDiveH, introOrbitAng].forEach((el) => {
      if (el) el.disabled = disabled;
    });
    [introPickFocusBtn, introPickTourABtn, introPickTourBBtn].forEach((el) => {
      if (el) el.disabled = disabled;
    });
    if (introFieldEl) introFieldEl.classList.toggle("cl-disabled", disabled);
    if (introParamsEl) introParamsEl.classList.toggle("cl-disabled", disabled);
    const it = S.cameraInfo?.intro;
    if (introFocusVal && it?.focusTarget)
      introFocusVal.textContent = `(${fmtNum(it.focusTarget.x)}, ${fmtNum(it.focusTarget.z)})`;
    if (introTourAVal && it?.tourA)
      introTourAVal.textContent = `(${fmtNum(it.tourA.x)}, ${fmtNum(it.tourA.z)})`;
    if (introTourBVal && it?.tourB)
      introTourBVal.textContent = `(${fmtNum(it.tourB.x)}, ${fmtNum(it.tourB.z)})`;
    if (introHintEl) {
      const mode = it?.mode ?? "vanilla";
      const split = !!S.cameraInfo?.splitScreen;
      const splitNote = split ? "⚠ 分屏模式已开启：运行时原版镜头被屏蔽，进场动画不可见。" : "";
      introHintEl.textContent = (INTRO_HINTS[mode] ?? "") + (splitNote ? " " + splitNote : "");
    }
  };

  if (introModeSel) {
    introModeSel.addEventListener("change", () => {
      const it = ensureIntro();
      if (!it) return;
      pushHistory();
      const v = introModeSel.value;
      if (v === "orbitLeft" || v === "orbitRight") {
        it.mode = "orbit";
        it.orbitDirection = v === "orbitLeft" ? "left" : "right";
      } else {
        it.mode = v as CameraIntroSetting["mode"];
      }
      if (isCustomIntro(it.mode)) {
        if (!Number.isFinite(it.duration)) it.duration = 2;
        if (usesStartDistance(it.mode) && !Number.isFinite(it.startDistance))
          it.startDistance = it.mode === "orbit" ? 8 : 5;
        if ((it.mode === "rotateLeft" || it.mode === "rotateRight") && !Number.isFinite(it.rotateAngle))
          it.rotateAngle = 30;
        if (it.mode === "dive" && !Number.isFinite(it.diveHeight)) it.diveHeight = 10;
        if (it.mode === "orbit") {
          if (!Number.isFinite(it.orbitAngle)) it.orbitAngle = 180;
          if (it.orbitDirection !== "left" && it.orbitDirection !== "right") it.orbitDirection = "right";
        }
        if (it.mode === "focus" && !it.focusTarget) it.focusTarget = { x: 0, z: 0 };
        if (it.mode === "tour") {
          if (!it.tourA) it.tourA = { x: 0, z: 0 };
          if (!it.tourB) it.tourB = { x: 0, z: 0 };
        }
      }
      syncIntroUI();
      draw();
      setStatus(`进场动画：${introModeSel.selectedOptions[0]?.textContent ?? it.mode}（写回后生效）`);
    });
  }

  const bindIntroNum = (
    input: HTMLInputElement | null,
    min: number,
    max: number,
    apply: (it: CameraIntroSetting, v: number) => void
  ): void => {
    if (!input) return;
    input.addEventListener("change", () => {
      const it = ensureIntro();
      if (!it) return;
      const v = clampNum(Number(input.value), min, max);
      input.value = String(v);
      pushHistory();
      apply(it, Math.round(v * 100) / 100);
    });
  };
  bindIntroNum(introDur, 0.5, 6, (it, v) => {
    it.duration = v;
  });
  bindIntroNum(introDist, 0, 20, (it, v) => {
    it.startDistance = v;
  });
  bindIntroNum(introAngle, 5, 90, (it, v) => {
    it.rotateAngle = v;
  });
  bindIntroNum(introDiveH, 2, 40, (it, v) => {
    it.diveHeight = v;
  });
  bindIntroNum(introOrbitAng, 90, 360, (it, v) => {
    it.orbitAngle = v;
  });

  // 目标选点（聚焦点 / 巡回 A / 巡回 B）：关闭弹窗 → 画布点击 →
  // applyCameraTargetPick 写入并回到本 tab。
  type IntroPickWhich = "focus" | "tourA" | "tourB";
  const beginTargetPick = (btn: HTMLButtonElement | null, which: IntroPickWhich): void => {
    btn?.addEventListener("click", () => {
      if (!S.cameraInfo || S.cameraInfo.splitScreen) return;
      ensureIntro();
      S.pendingCameraTargetPick = which;
      closeModal();
      dom.canvas.style.cursor = "crosshair";
      setStatus(`在画布上点击选择${PICK_TARGET_LABELS[which]}（Esc 取消）`);
    });
  };
  beginTargetPick(introPickFocusBtn, "focus");
  beginTargetPick(introPickTourABtn, "tourA");
  beginTargetPick(introPickTourBBtn, "tourB");

  syncIntroUI();
  // 进场动画演示框（rAF 循环，弹窗关闭自动停）。
  startIntroDemo();

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

/** 画布选点落地（input.ts 一次性消耗点击后调用）：写入目标（聚焦点 / 巡回 A/B，
 *  tour 选点自动切到 tour 模式）、刷新画布标记，并重开相机弹窗回到「进场动画」
 *  tab。选点坐标由调用方按吸附步长取整。focus 的 X/Z 均生效（全向平移）；
 *  tour 仅取 X（Z 恒落为主相机 Z）。X/Z 均不钳制（大地图可选远处）；超出主相机
 *  画面范围（0.75×水平半宽）时仅状态栏提示，不阻止选点。 */
export function applyCameraTargetPick(which: "focus" | "tourA" | "tourB", x: number, z: number): void {
  const cam = S.cameraInfo;
  if (!cam) return;
  if (!cam.intro) cam.intro = { mode: which === "focus" ? "focus" : "tour" };
  const it = cam.intro;
  pushHistory();
  if (which === "focus") it.mode = "focus";
  else it.mode = "tour";
  const camX = Number.isFinite(cam.position?.x) ? (cam.position as { x: number }).x : 0;
  const camY = Number.isFinite(cam.position?.y) ? (cam.position as { y: number }).y : 22;
  const camZ = Number.isFinite(cam.position?.z) ? (cam.position as { z: number }).z : z;
  const fov = Number.isFinite(cam.fieldOfView) && cam.fieldOfView > 0 ? cam.fieldOfView : 45;
  const halfW = Math.max(2, camY * Math.tan(((fov / 2) * Math.PI) / 180) * (16 / 9));
  const px = Math.round(x * 100) / 100;
  const pz = Math.round((which === "focus" ? z : camZ) * 100) / 100;
  const pt = { x: px, z: pz };
  if (which === "focus") it.focusTarget = pt;
  else if (which === "tourA") it.tourA = pt;
  else it.tourB = pt;
  if (!Number.isFinite(it.duration)) it.duration = 2;
  if (it.mode === "tour") {
    if (!it.tourA) it.tourA = { x: px, z: pz };
    if (!it.tourB) it.tourB = { x: px, z: pz };
  }
  const distX = Math.abs(px - camX);
  const farNote =
    distX > halfW * 0.75
      ? `（距主相机 ${distX.toFixed(1)}m，超出画面范围 ±${(halfW * 0.75).toFixed(1)}m，途中可能经过空旷区域）`
      : "";
  const zNote = which === "focus" ? "" : `（Z 跟随主相机 ${pt.z}）`;
  setStatus(`${PICK_TARGET_LABELS[which]}：(${pt.x}, ${pt.z})${zNote}${farNote}（写回后生效）`);
  draw();
  openCameraLightModal("intro");
}

/** 取消目标选点（Esc）：清标记并回到相机弹窗「进场动画」tab。 */
export function cancelCameraTargetPick(): void {
  S.pendingCameraTargetPick = "";
  dom.canvas.style.cursor = "";
  setStatus("已取消选点");
  openCameraLightModal("intro");
}

const PICK_TARGET_LABELS: Record<"focus" | "tourA" | "tourB", string> = {
  focus: "进场动画聚焦目标",
  tourA: "巡回 A 点",
  tourB: "巡回 B 点",
};

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

// ---------------------------------------------------------------- 进场动画演示框

/** 与后端 AnimGroupBakery.EaseValue("easeInOut") 同公式。 */
function demoEaseInOut(k: number): number {
  const c = Math.max(0, Math.min(1, k));
  return c < 0.5 ? 2 * c * c : 1 - Math.pow(-2 * c + 2, 2) / 2;
}

/** 演示框：16:9 横向俯视示意（与游戏画面同比例；厨房矩形位于相机视轴地面落点
 *  + 相机球 + 目标球 + 路径虚线），运动公式与 CameraIntroBakery.BuildPathClip
 *  一致（折线路点 = 段距比例 + 每段 easeInOut；orbit 圆弧 = 段内 linear + 全局
 *  easeInOut 重映射；raft/space = 原版拉近进场 + 待机摆动循环示意），按真实
 *  duration 循环播放（末尾停 0.8s）；canvas 从 DOM 摘除（弹窗关闭）自动停。
 *  每帧实时读 S.cameraInfo —— 模式/参数改动即时反映。 */
function startIntroDemo(): void {
  const canvas = document.getElementById("cl-intro-demo") as HTMLCanvasElement | null;
  if (!canvas) return;
  const dpr = Math.min(2, window.devicePixelRatio || 1);
  const W = 224;
  const H = 126; // 16:9 横向（与游戏画面同比例）
  canvas.width = W * dpr;
  canvas.height = H * dpr;
  canvas.style.width = `${W}px`;
  canvas.style.height = `${H}px`;
  const ctx = canvas.getContext("2d");
  if (!ctx) return;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

  const KITCHEN_W = 16;
  const KITCHEN_H = 10;

  /** 相机视轴水平投影单位向量 + 视轴地面落点距离（h/tan(pitch)）。 */
  const viewAxis = (): { ax: { x: number; z: number }; ground: number } => {
    const cam = S.cameraInfo;
    const yaw = ((cam?.yaw ?? 0) * Math.PI) / 180;
    const pitchDeg = cam?.pitch ?? 60;
    const pitch = (pitchDeg * Math.PI) / 180;
    const h = Math.max(4, cam?.position?.y ?? 22);
    const ground = pitch > 0.05 ? h / Math.tan(pitch) : 12;
    const cp = Math.cos(pitch);
    const len = Math.hypot(Math.sin(yaw) * cp, Math.cos(yaw) * cp) || 1;
    return {
      ax: { x: (Math.sin(yaw) * cp) / len, z: (Math.cos(yaw) * cp) / len },
      ground,
    };
  };

  /** 当前模式的演示路点（世界 XZ；首=起始，末=相机最终位），公式对齐后端烘焙。 */
  const waypoints = (): { x: number; z: number }[] => {
    const cam = S.cameraInfo;
    const it = cam?.intro;
    const mode = it?.mode ?? "vanilla";
    const camPt = { x: cam?.position?.x ?? 0, z: cam?.position?.z ?? 0 };
    const { ax } = viewAxis();
    // vanilla/raft/space 走原版固定拉近（不受 startDistance 残留值影响）。
    const d = mode === "pullBack" || mode === "dollyIn" || mode === "dive"
      ? it?.startDistance ?? 5
      : 5;
    if (mode === "dollyIn" || mode === "dive")
      return [{ x: camPt.x - ax.x * d, z: camPt.z - ax.z * d }, camPt];
    if (mode === "orbit") {
      const r = it?.startDistance && it.startDistance >= 2 ? it.startDistance : 8;
      const th0 = ((it?.orbitDirection === "left" ? -1 : 1) * (it?.orbitAngle ?? 180) * Math.PI) / 180;
      const pts: { x: number; z: number }[] = [];
      const n = 32;
      for (let i = 0; i < n; i++) {
        const th = th0 * (1 - i / n);
        pts.push({ x: camPt.x + Math.sin(th) * r, z: camPt.z + (Math.cos(th) - 1) * r });
      }
      pts.push(camPt);
      return pts;
    }
    if (mode === "focus" && it?.focusTarget)
      return [{ x: it.focusTarget.x, z: it.focusTarget.z }, camPt];
    if (mode === "tour" && it?.tourA && it?.tourB)
      return [{ x: it.tourA.x, z: camPt.z }, { x: it.tourB.x, z: camPt.z }, camPt];
    // vanilla / pullBack / rotate* / raft / space：沿视轴贴近起点（原版拉近同款）。
    return [{ x: camPt.x + ax.x * d, z: camPt.z + ax.z * d }, camPt];
  };

  /** 路点插值（BuildPathClip 同公式）：折线 = 段距比例 + 每段 easeInOut；
   *  smoothPath（orbit 弧）= 段内 linear + 全程 easeInOut 重映射（密集段逐段
   *  缓动会脉冲式蠕动）。 */
  const posAt = (pts: { x: number; z: number }[], p: number, smoothPath: boolean): { x: number; z: number } => {
    if (pts.length < 2) return pts[0] ?? { x: 0, z: 0 };
    const raw = Math.max(0, Math.min(1, p));
    const time = smoothPath ? demoEaseInOut(raw) : raw;
    const segs = pts.length - 1;
    const lens: number[] = [];
    let total = 0;
    for (let i = 0; i < segs; i++) {
      const l = Math.hypot(pts[i + 1].x - pts[i].x, pts[i + 1].z - pts[i].z);
      lens.push(l);
      total += l;
    }
    const cum: number[] = [0];
    let acc = 0;
    for (let i = 0; i < segs; i++) {
      acc += lens[i];
      cum.push(total > 1e-4 ? acc / total : (i + 1) / segs);
    }
    cum[segs] = 1;
    let seg = 0;
    while (seg < segs - 1 && time > cum[seg + 1]) seg++;
    const t0 = cum[seg];
    const t1 = cum[seg + 1];
    const e = smoothPath
      ? Math.max(0, Math.min(1, t1 > t0 ? (time - t0) / (t1 - t0) : 1))
      : demoEaseInOut(t1 > t0 ? (time - t0) / (t1 - t0) : 1);
    return {
      x: pts[seg].x + (pts[seg + 1].x - pts[seg].x) * e,
      z: pts[seg].z + (pts[seg + 1].z - pts[seg].z) * e,
    };
  };

  const frame = (now: number): void => {
    if (!canvas.isConnected) return; // 弹窗已关 → 停
    const cam = S.cameraInfo;
    const it = cam?.intro;
    const mode = it?.mode ?? "vanilla";
    const duration = Math.max(0.5, it?.duration ?? 2);
    const cycle = duration + 0.8;
    const t = (now / 1000) % cycle;
    const p = Math.min(1, t / duration);

    const pts = waypoints();
    const camPt = pts[pts.length - 1];
    // 厨房中心 = 相机沿视轴的地面落点（相机俯瞰的前下方），非相机正下方。
    const { ax, ground } = viewAxis();
    const kC = { x: camPt.x + ax.x * ground, z: camPt.z + ax.z * ground };

    // 包围盒（含厨房示意矩形）→ 框内等比映射。
    let minX = kC.x - KITCHEN_W / 2;
    let maxX = kC.x + KITCHEN_W / 2;
    let minZ = kC.z - KITCHEN_H / 2;
    let maxZ = kC.z + KITCHEN_H / 2;
    for (const q of pts) {
      minX = Math.min(minX, q.x);
      maxX = Math.max(maxX, q.x);
      minZ = Math.min(minZ, q.z);
      maxZ = Math.max(maxZ, q.z);
    }
    const pad = 12;
    const barH = 14;
    const availW = W - pad * 2;
    const availH = H - pad * 2 - barH;
    const sc = Math.min(availW / Math.max(4, maxX - minX), availH / Math.max(4, maxZ - minZ));
    const ox = W / 2 - ((minX + maxX) / 2) * sc;
    const oz = (H - barH) / 2 - ((minZ + maxZ) / 2) * sc;
    const P = (x: number, z: number): { x: number; y: number } => ({ x: ox + x * sc, y: oz + z * sc });

    ctx.clearRect(0, 0, W, H);

    // 厨房示意矩形（视轴落点为中心）。
    const kA = P(kC.x - KITCHEN_W / 2, kC.z - KITCHEN_H / 2);
    const kB = P(kC.x + KITCHEN_W / 2, kC.z + KITCHEN_H / 2);
    ctx.strokeStyle = "rgba(148,163,184,0.5)";
    ctx.lineWidth = 1;
    ctx.setLineDash([]);
    ctx.beginPath();
    ctx.rect(Math.min(kA.x, kB.x), Math.min(kA.y, kB.y), Math.abs(kB.x - kA.x), Math.abs(kB.y - kA.y));
    ctx.stroke();

    if (mode === "none") {
      const c = P(camPt.x, camPt.z);
      ctx.fillStyle = "rgba(226,232,240,0.95)";
      ctx.beginPath();
      ctx.arc(c.x, c.y, 5, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "rgba(148,163,184,0.9)";
      ctx.font = "10px sans-serif";
      ctx.textAlign = "center";
      ctx.textBaseline = "top";
      ctx.fillText("无进场 · 直接最终画面", W / 2, H - barH + 1);
      requestAnimationFrame(frame);
      return;
    }

    // 路径虚线 + 起点。
    if (pts.length >= 2) {
      ctx.strokeStyle = "rgba(236,72,153,0.75)";
      ctx.setLineDash([4, 3]);
      ctx.lineWidth = 1.2;
      ctx.beginPath();
      const p0 = P(pts[0].x, pts[0].z);
      ctx.moveTo(p0.x, p0.y);
      for (let i = 1; i < pts.length; i++) {
        const q = P(pts[i].x, pts[i].z);
        ctx.lineTo(q.x, q.y);
      }
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.strokeStyle = "rgba(236,72,153,0.9)";
      ctx.beginPath();
      ctx.arc(p0.x, p0.y, 3.5, 0, Math.PI * 2);
      ctx.stroke();
    }

    // 目标球（focus / tour A·B）。
    const ball = (wx: number, wz: number, label: string): void => {
      const c = P(wx, wz);
      ctx.fillStyle = "rgba(236,72,153,0.85)";
      ctx.beginPath();
      ctx.arc(c.x, c.y, 3, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "rgba(236,72,153,0.95)";
      ctx.font = "9px sans-serif";
      ctx.textAlign = "left";
      ctx.textBaseline = "bottom";
      ctx.fillText(label, c.x + 5, c.y - 3);
    };
    if (mode === "focus" && it?.focusTarget) ball(it.focusTarget.x, it.focusTarget.z, "聚焦");
    if (mode === "tour" && it?.tourA && it?.tourB) {
      ball(it.tourA.x, camPt.z, "A");
      ball(it.tourB.x, camPt.z, "B");
    }

    // 相机球当前位置（orbit 弧线路径用 smooth 插值防蠕动）。
    const isRaftSpace = mode === "raft" || mode === "space";
    const cur = mode === "orbit" ? posAt(pts, p, true) : posAt(pts, p, false);
    let curX = cur.x;
    let curZ = cur.z;
    let ballR = 4.5;
    if (isRaftSpace) {
      // 官方变体：拉近进场（前 duration）后持续待机摆动示意。
      const w = Math.sin((now / 1000) * 2.2);
      const settle = Math.min(1, demoEaseInOut(p));
      curX += w * 0.9 * settle;
      curZ += Math.cos((now / 1000) * 1.7) * 0.6 * settle;
    }
    if (mode === "dive") ballR += (1 - demoEaseInOut(p)) * ((it?.diveHeight ?? 10) / 40) * 11;
    const c = P(curX, curZ);

    // 朝向线：基准 = 视轴水平方向（世界 (sin yaw, cos yaw) → 画布同向）；
    // rotate 偏航角随进度归零；space roll 微摆。
    let heading = ((S.cameraInfo?.yaw ?? 0) * Math.PI) / 180;
    if (mode === "rotateLeft" || mode === "rotateRight") {
      const sign = mode === "rotateLeft" ? 1 : -1;
      heading += ((sign * (it?.rotateAngle ?? 30) * (1 - demoEaseInOut(p))) * Math.PI) / 180;
    }
    if (mode === "space") heading += Math.sin((now / 1000) * 0.9) * 0.12;
    ctx.strokeStyle = "rgba(226,232,240,0.9)";
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.moveTo(c.x, c.y);
    ctx.lineTo(c.x + Math.sin(heading) * 11, c.y + Math.cos(heading) * 11);
    ctx.stroke();
    ctx.fillStyle = "rgba(246,248,250,0.95)";
    ctx.beginPath();
    ctx.arc(c.x, c.y, ballR, 0, Math.PI * 2);
    ctx.fill();

    // 最终位标记（相机归位点）。
    const f = P(camPt.x, camPt.z);
    ctx.strokeStyle = "rgba(94,234,212,0.8)";
    ctx.beginPath();
    ctx.arc(f.x, f.y, 6, 0, Math.PI * 2);
    ctx.stroke();

    // 底部进度条。
    ctx.fillStyle = "rgba(148,163,184,0.35)";
    ctx.fillRect(pad, H - 7, availW, 3);
    ctx.fillStyle = "rgba(236,72,153,0.9)";
    ctx.fillRect(pad, H - 7, availW * p, 3);

    requestAnimationFrame(frame);
  };
  requestAnimationFrame(frame);
}

const CAM_POS_STEP = 0.1;
