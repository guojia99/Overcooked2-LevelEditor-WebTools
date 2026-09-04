import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import { FBXLoader } from "three/examples/jsm/loaders/FBXLoader.js";
import { OBJLoader } from "three/examples/jsm/loaders/OBJLoader.js";
import { MTLLoader } from "three/examples/jsm/loaders/MTLLoader.js";
import { openModal, closeModal } from "./modals";
import {
  CM_PER_UNIT,
  CUP_FIT_CM,
  PLATE_FIT_CM,
  cm2u,
  fmt4,
  fmtCm,
  footprintOf,
  u2cm,
  unrotateSize,
} from "./modelUnits";

/** 3D 模型在线预览（three.js）：FBX 会尝试按 FBX 内嵌的相对路径经
 *  resourceBase 加载贴图（PNG/JPG）；OBJ 支持 MTL 材质贴图。
 *  模型带虚拟包围盒（橙色线框 + 6 个面中心点），可选择箱面中心为原点或让某面朝下。 */

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

let active: { raf: number; renderer: THREE.WebGLRenderer } | null = null;

/** 模型变换：缩放 + 三轴旋转（度）+ 三轴位置（单位，Y 为底面高度）。 */
export interface ModelTransformValues {
  scale: number;
  rotationX: number;
  rotationY: number;
  rotationZ: number;
  positionX: number;
  positionY: number;
  positionZ: number;
  /** 模型原点偏移（模型节点 localPosition，Unity 单位）：旋转/缩放绕偏移后的原点。 */
  pivotX: number;
  pivotY: number;
  pivotZ: number;
}

export interface ModelPreviewOptions {
  title: string;
  /** 模型文件访问基地址（目录式，FBX 贴图按相对路径拼接）。 */
  resourceBase: string;
  /** 模型文件名（.fbx / .obj）。 */
  modelFileName: string;
  /** 初始缩放（应用到预览对象，默认 1）。 */
  scale?: number;
  /** 初始三轴旋转（度，默认 0）。 */
  rotationX?: number;
  /** 初始 Y 轴旋转（度，默认 0）。 */
  rotationY?: number;
  rotationZ?: number;
  /** 初始位置偏移（单位，Y 为底面高度）。 */
  positionX?: number;
  positionY?: number;
  positionZ?: number;
  /** 初始模型原点偏移（Unity 单位）。 */
  pivotX?: number;
  pivotY?: number;
  pivotZ?: number;
  /** 调整回调：用户在弹窗内修改缩放/旋转/位置/原点时触发，用于写回菜谱表单。 */
  onAdjust?: (t: ModelTransformValues) => void;
  /** 本地模型（选择文件后未保存）：直接解析 ArrayBuffer，不走服务器。 */
  localBuffer?: ArrayBuffer;
  /** 本地贴图（File 列表）：FBX 内嵌纹理无法加载时注入到全部材质。 */
  localTextures?: File[];
  /** 服务端贴图 URL 列表：FBX 无 Video 节点（three 无法解析内嵌引用）时注入。 */
  remoteTextures?: string[];
  /** 服务端 MTL 文件 URL（OBJ 预览）：与 .obj 同目录，加载材质贴图。 */
  mtlUrl?: string;
  /** 本地 MTL 文本（本地预览 OBJ 时提供）。 */
  localMtl?: string;
  /** 自动适配目标：plate（盘子，足迹 85 cm，默认）/ cup（杯子，足迹 37 cm 并抬升到杯中 30 cm）。 */
  fitTarget?: "plate" | "cup";
  /** Unity 导入后的模型原始尺寸（不含配置变换，由后端从 prefab 包围盒反推），
   *  自动适配以 Unity 实际尺寸为准（three.js 尺寸可能与 Unity 不一致）。 */
  unitySize?: { x: number; y: number; z: number; minY: number };
}

function disposeActive(): void {
  if (active) {
    cancelAnimationFrame(active.raf);
    active.renderer.dispose();
    active = null;
  }
}

const DEG2RAD = Math.PI / 180;

/** 本地贴图注入：把用户选择的图片应用到模型全部材质的 map（FBX 相对纹理路径本地不可用）。 */
function injectLocalTextures(obj: THREE.Object3D, files: File[]): void {
  const loader = new THREE.TextureLoader();
  const mats: THREE.MeshStandardMaterial[] = [];
  obj.traverse((child) => {
    const mesh = child as THREE.Mesh;
    if (!mesh.isMesh) return;
    const m = mesh.material as THREE.MeshStandardMaterial | THREE.MeshPhongMaterial | undefined;
    if (m && !mats.includes(m as THREE.MeshStandardMaterial)) mats.push(m as THREE.MeshStandardMaterial);
  });
  if (mats.length === 0) return;
  for (const f of files) {
    const url = URL.createObjectURL(f);
    loader.load(
      url,
      (tex) => {
        URL.revokeObjectURL(url);
        for (const m of mats) {
          m.map = tex;
          m.color = new THREE.Color(1, 1, 1);
          m.needsUpdate = true;
        }
      },
      undefined,
      () => URL.revokeObjectURL(url)
    );
  }
}

/** 服务端贴图注入：FBX 无 Video 节点（three 无法按内嵌引用取贴图）时，
 *  用 models 目录的贴图 URL 覆盖全部材质 map。 */
function injectRemoteTextures(obj: THREE.Object3D, urls: string[]): void {
  const loader = new THREE.TextureLoader();
  const mats: THREE.MeshStandardMaterial[] = [];
  obj.traverse((child) => {
    const mesh = child as THREE.Mesh;
    if (!mesh.isMesh) return;
    const m = mesh.material as THREE.MeshStandardMaterial | THREE.MeshPhongMaterial | undefined;
    if (m && !mats.includes(m as THREE.MeshStandardMaterial)) mats.push(m as THREE.MeshStandardMaterial);
  });
  if (mats.length === 0) return;
  for (const url of urls) {
    loader.load(
      url,
      (tex) => {
        for (const m of mats) {
          m.map = tex;
          m.color = new THREE.Color(1, 1, 1);
          m.needsUpdate = true;
        }
      },
      undefined,
      () => {
        // 单张贴图失败不影响
      }
    );
  }
}

export function openModelPreview(opts: ModelPreviewOptions): void {
  disposeActive();
  const initial: ModelTransformValues = {
    scale: opts.scale ?? 1,
    rotationX: opts.rotationX ?? 0,
    rotationY: opts.rotationY ?? 0,
    rotationZ: opts.rotationZ ?? 0,
    positionX: opts.positionX ?? 0,
    positionY: opts.positionY ?? 0,
    positionZ: opts.positionZ ?? 0,
    pivotX: opts.pivotX ?? 0,
    pivotY: opts.pivotY ?? 0,
    pivotZ: opts.pivotZ ?? 0,
  };
  const controlsHtml = `<div class="mp-controls">
    <label>尺寸（水平足迹, cm）<input type="number" id="mp-size-cm" step="0.0001" min="0.0001" value="" disabled>
      <span class="muted small">模型最长水平足迹；保存并上传后按 Unity 实际尺寸精确校准</span></label>
    <label>缩放（相对倍数）<input type="number" id="mp-scale" value="${fmt4(initial.scale)}" step="0.0001" min="0.0001"></label>
    <label>旋转X（度）<input type="number" id="mp-rot-x" value="${initial.rotationX}" step="5"></label>
    <label>旋转Y（度）<input type="number" id="mp-rot-y" value="${initial.rotationY}" step="5"></label>
    <label>旋转Z（度）<input type="number" id="mp-rot-z" value="${initial.rotationZ}" step="5"></label>
    <label>位置X（cm）<input type="number" id="mp-pos-x" value="${fmtCm(u2cm(initial.positionX))}" step="0.0001"></label>
    <label>位置Y（底面高度, cm）<input type="number" id="mp-pos-y" value="${fmtCm(u2cm(initial.positionY))}" step="0.0001"></label>
    <label>位置Z（cm）<input type="number" id="mp-pos-z" value="${fmtCm(u2cm(initial.positionZ))}" step="0.0001"></label>
    <p class="modal-hint" id="mp-size-ro">当前尺寸：—（模型加载后显示；1 模型单位 = 1 Unity 单位 = 100 cm）</p>
    <label>适配目标<select id="mp-fit-target">
      <option value="plate" ${(opts.fitTarget ?? "plate") === "plate" ? "selected" : ""}>盘子（足迹 85 cm）</option>
      <option value="cup" ${opts.fitTarget === "cup" ? "selected" : ""}>杯子（足迹 37 cm）</option>
    </select>
      <span class="muted small">半透明标的物 = 参考容器（盘子/玻璃杯，无碰撞）；「自动适配」按该目标缩放，位置 Y 保持 0（不自动下沉，高度手动调）</span></label>
    <div class="mp-face-row">
      <label>虚拟箱面<select id="mp-face">
        <option value="bottom">下（-Y）</option>
        <option value="top">上（+Y）</option>
        <option value="front">前（+Z）</option>
        <option value="back">后（-Z）</option>
        <option value="right">右（+X）</option>
        <option value="left">左（-X）</option>
      </select></label>
      <button type="button" class="m-btn" id="mp-face-origin" title="旋转/缩放将绕该面中心">🎯 面中心设为原点</button>
      <button type="button" class="m-btn" id="mp-face-down" title="旋转模型使该面贴地">⬇ 该面朝下</button>
    </div>
    <button type="button" class="m-btn" id="mp-rot-x90">↻ 绕 X 转 90°</button>
    <button type="button" class="m-btn" id="mp-rot-y90">↻ 绕 Y 转 90°</button>
    <button type="button" class="m-btn" id="mp-rot-z90">↻ 绕 Z 转 90°</button>
    <button type="button" class="m-btn" id="mp-pick-origin">🎯 重选原点</button>
    <button type="button" class="m-btn" id="mp-fit">✨ 自动适配</button>
    ${opts.onAdjust ? `<button type="button" class="m-btn primary" id="mp-apply">✅ 应用方向/大小到菜谱</button>` : ""}
  </div>`;
  openModal(
    `3D 模型预览 · ${esc(opts.title)}`,
    `<div class="mp-layout">
       <div class="mp-stage">
         <div class="mp-wrap"><canvas id="mp-canvas"></canvas></div>
         <p class="mp-status" id="mp-status"><span class="busy-spinner mp-inline-spinner" id="mp-spinner"></span> 加载中…</p>
       </div>
       <div class="mp-side">
         ${controlsHtml}
          <p class="modal-hint">左键旋转视角 · 右键平移 · 滚轮缩放 · <b>半透明标的物 = 参考容器（盘子直径 100 cm / 玻璃杯口径 69 cm，纯视觉无碰撞）</b>，其<b>包围盒中心 = 原点 (0,0,0)</b>（红/绿/蓝轴 X/Y/Z，黄色点为原点，网格按 cm 标注格距，1 单位 = 100 cm）· <b>橙色线框 = 模型虚拟包围盒</b>（尺寸见右侧读数，6 个面各有一个中心点；<b>下拉选中的面会微微高亮</b>）· 「🎯 面中心设为原点」旋转/缩放绕该面中心 · 「⬇ 该面朝下」旋转模型让该面贴地 · 「重选原点」后可点击模型上任意点 · 「自动适配」按目标（盘子 85 cm / 杯子 37 cm）缩放，<b>位置 Y 保持 0（不自动下沉，高度手动调）</b> · 尺寸/位置单位为 cm（4 位小数精度）</p>
       </div>
     </div>`,
    `<button type="button" class="m-btn" data-cancel>关闭</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  const panel = document.querySelector(".modal-panel");
  if (panel) panel.classList.add("mp-panel");

  const status = document.getElementById("mp-status")!;
  const canvas = document.getElementById("mp-canvas") as HTMLCanvasElement;

  const wrap = canvas.parentElement!;
  const width = wrap.clientWidth || 640;
  const height = wrap.clientHeight || 420;

  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
  renderer.setSize(width, height);
  renderer.setClearColor(0x1a1d23, 1);
  renderer.outputColorSpace = THREE.SRGBColorSpace;

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(50, width / height, 0.1, 5000);
  camera.position.set(2, 1.6, 2.4);

  const controls = new OrbitControls(camera, canvas);
  controls.enableDamping = true;
  controls.dampingFactor = 0.08;

  scene.add(new THREE.HemisphereLight(0xffffff, 0x404060, 1.2));
  const dir = new THREE.DirectionalLight(0xffffff, 1.4);
  dir.position.set(2, 3, 1.5);
  scene.add(dir);

  let grid: THREE.GridHelper | null = null;
  let axisGizmo: THREE.Group | null = null;
  let gridLabel: THREE.Sprite | null = null;

  /** 生成带文字的标签精灵（Canvas 文字 → 纹理），用于轴与格距标注。 */
  function makeTextSprite(text: string, color: string, height: number): THREE.Sprite {
    const canvas = document.createElement("canvas");
    canvas.width = 256;
    canvas.height = 64;
    const ctx = canvas.getContext("2d");
    if (ctx) {
      ctx.clearRect(0, 0, 256, 64);
      ctx.font = "bold 46px system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.fillStyle = color;
      ctx.fillText(text, 128, 32);
    }
    const tex = new THREE.CanvasTexture(canvas);
    tex.colorSpace = THREE.SRGBColorSpace;
    const sprite = new THREE.Sprite(
      new THREE.SpriteMaterial({ map: tex, transparent: true, depthTest: false, sizeAttenuation: true })
    );
    sprite.scale.set(height * 4, height, 1);
    return sprite;
  }

  function disposeGroup(root: THREE.Object3D): void {
    root.traverse((child) => {
      const asMesh = child as THREE.Mesh;
      if (asMesh.isMesh && asMesh.geometry) asMesh.geometry.dispose();
      const asLine = child as THREE.LineSegments;
      if (asLine.isLineSegments && asLine.geometry) asLine.geometry.dispose();
      const asSprite = child as THREE.Sprite;
      if (asSprite.isSprite) {
        asSprite.material.map?.dispose();
        asSprite.material.dispose();
      }
    });
  }

  /** 坐标系辅助：原点标记 + X/Y/Z 三轴（带颜色与文字标签）+ 厘米格距标注。
   *  轴与标注随 fit 重建，位于模型底部平面（y = 模型底面，单位与 Unity 一致，1 单位 = 100 cm）。 */
  function setupCoordSys(originY: number, span: number, cellCm: number): void {
    if (axisGizmo) {
      scene.remove(axisGizmo);
      disposeGroup(axisGizmo);
      axisGizmo = null;
    }
    if (gridLabel) {
      scene.remove(gridLabel);
      gridLabel.material.map?.dispose();
      gridLabel.material.dispose();
      gridLabel = null;
    }

    const gizmo = new THREE.Group();
    axisGizmo = gizmo;
    const axisLen = Math.max(span * 0.45, 0.3);
    const axes = new THREE.AxesHelper(axisLen);
    axes.position.y = originY;
    gizmo.add(axes);
    const labelH = Math.max(axisLen * 0.07, 0.03);
    const tip = axisLen * 1.12;
    const xl = makeTextSprite("X", "#ff6b6b", labelH);
    xl.position.set(tip, originY, 0);
    gizmo.add(xl);
    const yl = makeTextSprite("Y", "#69db7c", labelH);
    yl.position.set(0, originY + tip, 0);
    gizmo.add(yl);
    const zl = makeTextSprite("Z", "#5c9dff", labelH);
    zl.position.set(0, originY, tip);
    gizmo.add(zl);
    // 原点标记（0,0,0）
    const originDot = new THREE.Mesh(
      new THREE.SphereGeometry(Math.max(labelH * 0.45, 0.012), 12, 12),
      new THREE.MeshBasicMaterial({ color: 0xffd43b })
    );
    originDot.position.set(0, originY, 0);
    gizmo.add(originDot);
    scene.add(gizmo);

    // 网格格距标注（cm）
    const gl = makeTextSprite(`${cellCm} cm/格`, "#b6bcc6", Math.max(labelH * 1.1, 0.04));
    gl.position.set(span * 0.2, originY, -span * 0.4);
    scene.add(gl);
    gridLabel = gl;
  }

  function fitObjects(objects: (THREE.Object3D | null)[]): void {
    const box = new THREE.Box3();
    for (const o of objects) {
      if (o) box.expandByObject(o);
    }
    if (box.isEmpty()) return;
    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3()).length() || 1;
    controls.target.copy(center);
    camera.position.copy(center).add(new THREE.Vector3(1, 0.7, 1).multiplyScalar(size * 1.1));
    camera.near = Math.max(size / 1000, 0.001);
    camera.far = size * 100;
    camera.updateProjectionMatrix();
    controls.update();
    if (grid) {
      scene.remove(grid);
      grid.geometry.dispose();
    }
    // 网格：格距按 cm（5/10/25/50 cm），保证格数在 [8, 50] 内
    const span = size * 2;
    const cellCandidates = [0.05, 0.1, 0.25, 0.5]; // 5 / 10 / 25 / 50 cm
    let cell = 0.1;
    for (const c of cellCandidates) {
      if (span / c <= 50) {
        cell = c;
        break;
      }
    }
    const divisions = Math.max(8, Math.round(span / cell));
    grid = new THREE.GridHelper(span, divisions, 0x3d6bf3, 0x333a4a);
    grid.position.y = box.min.y;
    scene.add(grid);
    setupCoordSys(box.min.y, span, Math.round(cell * 100));
  }

  let previewObj: THREE.Object3D | null = null;
  let modelObj: THREE.Object3D | null = null;
  /** 模型原始几何包围盒（加载时测量，不含任何变换），用于原点按钮与测量。 */
  let rawBox: THREE.Box3 | null = null;
  /** 预览几何单位 → Unity 单位换算系数（预览几何相对 Unity 导入可能放大百倍）。 */
  let unitK = 1;
  /** 预览基准缩放：已知 Unity 尺寸时把模型显示归一化到 Unity 实际大小（1/unitK）。 */
  let unitBase = 1;
  /** 参考容器（盘子/杯子，纯视觉标的物，无碰撞）：包围盒中心 = 原点 (0,0,0)。 */
  let refObj: THREE.Object3D | null = null;
  let refLabel: THREE.Sprite | null = null;

  /** 参考容器模型（web/public/models/，Unity 静态服务随 dist 提供）。 */
  const REF_MODELS = {
    plate: { url: "models/ref_plate.obj", centerY: -0.0643 },
    cup: { url: "models/ref_glass.obj", centerY: -0.398 },
  } as const;

  /** 加载/切换参考容器：半透明显示，包围盒中心对齐原点（0,0,0）；
   *  仅作视觉标的物（不参与射线/碰撞），盘子用于盘子目标、杯子用于杯子目标。
   *  容器顶部带尺寸标注（cm），可直接在 web 验证容器实际大小。 */
  function loadReference(kind: "plate" | "cup"): void {
    if (refObj) {
      scene.remove(refObj);
      disposeGroup(refObj);
      refObj = null;
    }
    if (refLabel) {
      scene.remove(refLabel);
      refLabel.material.map?.dispose();
      refLabel.material.dispose();
      refLabel = null;
    }
    const spec = REF_MODELS[kind];
    new OBJLoader().load(
      spec.url,
      (ref) => {
        if (refObj) return; // 已切换，忽略迟到的加载
        ref.traverse((child) => {
          const mesh = child as THREE.Mesh;
          if (!mesh.isMesh) return;
          const mat = mesh.material as THREE.MeshStandardMaterial | THREE.MeshPhongMaterial | undefined;
          if (mat) {
            mat.transparent = true;
            mat.opacity = 0.35;
            mat.color = new THREE.Color(0x8fd3ff);
          }
        });
        // 包围盒中心 = 原点（0,0,0）：原点 = 容器中心，高度/大小都以它为准
        ref.position.y = spec.centerY;
        refObj = ref;
        scene.add(ref);
        // 容器顶部尺寸标注（cm），便于直接核对容器大小
        const b = new THREE.Box3().setFromObject(ref);
        const bsize = b.getSize(new THREE.Vector3());
        const dia = Math.max(bsize.x, bsize.z);
        const labelText =
          kind === "plate"
            ? `盘子 φ${fmtCm(u2cm(dia))} cm`
            : `玻璃杯 φ${fmtCm(u2cm(dia))} cm · 高 ${fmtCm(u2cm(bsize.y))} cm`;
        console.log(`[ModelPreview] 参考${kind === "plate" ? "盘子" : "玻璃杯"}：φ${(dia * 100).toFixed(1)} cm × 高 ${(bsize.y * 100).toFixed(1)} cm`);
        const label = makeTextSprite(labelText, "#8fd3ff", Math.max(dia * 0.05, 0.04));
        label.position.set(0, b.max.y + Math.max(dia * 0.1, 0.06), 0);
        scene.add(label);
        refLabel = label;
        if (previewObj) fitObjects([previewObj, ref]);
      },
      undefined,
      () => {
        // 参考模型加载失败不影响主预览
      }
    );
  }

  /** 虚拟包围盒的 6 个面（模型局部坐标轴方向）。 */
  const FACE_DEFS = [
    { id: "bottom", axis: [0, -1, 0], label: "下（-Y）" },
    { id: "top", axis: [0, 1, 0], label: "上（+Y）" },
    { id: "front", axis: [0, 0, 1], label: "前（+Z）" },
    { id: "back", axis: [0, 0, -1], label: "后（-Z）" },
    { id: "right", axis: [1, 0, 0], label: "右（+X）" },
    { id: "left", axis: [-1, 0, 0], label: "左（-X）" },
  ] as const;
  type FaceId = (typeof FACE_DEFS)[number]["id"];

  /** 虚拟包围盒：模型局部 AABB 的橙色线框 + 6 个面中心点。
   *  作为模型节点的子对象，随缩放/旋转/原点偏移自动跟随。 */
  let boxLines: THREE.LineSegments | null = null;
  const faceMarks = new Map<FaceId, THREE.Mesh>();
  /** 当前选中箱面。 */
  let currentFace: FaceId = "bottom";
  /** 选中面的高亮面片（半透明，随模型变换跟随）。 */
  let faceHighlight: THREE.Mesh | null = null;

  /** 高亮选中箱面：在该面外侧放一个半透明面片（微微高亮）。 */
  function updateFaceHighlight(id: FaceId): void {
    currentFace = id;
    if (!modelObj || !rawBox) return;
    if (faceHighlight) {
      modelObj.remove(faceHighlight);
      faceHighlight.geometry.dispose();
      (faceHighlight.material as THREE.Material).dispose();
      faceHighlight = null;
    }
    const f = FACE_DEFS.find((x) => x.id === id);
    if (!f) return;
    const size = rawBox.getSize(new THREE.Vector3());
    const center = rawBox.getCenter(new THREE.Vector3());
    const axis = new THREE.Vector3(f.axis[0], f.axis[1], f.axis[2]);
    // 面片尺寸 = 垂直于法线的另外两轴跨度（略内缩）
    const w = Math.abs(axis.x) > 0.5 ? size.z : size.x;
    const h = Math.abs(axis.y) > 0.5 ? size.z : size.y;
    faceHighlight = new THREE.Mesh(
      new THREE.PlaneGeometry(w * 0.98, h * 0.98),
      new THREE.MeshBasicMaterial({
        color: 0xffd54a,
        transparent: true,
        opacity: 0.3,
        side: THREE.DoubleSide,
        depthWrite: false,
      })
    );
    faceHighlight.quaternion.setFromUnitVectors(new THREE.Vector3(0, 0, 1), axis);
    // 沿法线向外微移，避免与线框/模型面 z-fighting
    const offset = Math.max(w, h) * 0.004 + 0.001;
    faceHighlight.position.set(
      center.x + axis.x * (Math.abs(axis.x) > 0.5 ? size.x / 2 + offset : 0),
      center.y + axis.y * (Math.abs(axis.y) > 0.5 ? size.y / 2 + offset : 0),
      center.z + axis.z * (Math.abs(axis.z) > 0.5 ? size.z / 2 + offset : 0)
    );
    modelObj.add(faceHighlight);
  }

  function setupVirtualBox(obj: THREE.Object3D): void {
    if (boxLines) {
      obj.remove(boxLines);
      boxLines.geometry.dispose();
      (boxLines.material as THREE.Material).dispose();
      boxLines = null;
    }
    for (const m of faceMarks.values()) {
      m.geometry.dispose();
      (m.material as THREE.Material).dispose();
      obj.remove(m);
    }
    faceMarks.clear();
    if (!rawBox) return;
    const size = rawBox.getSize(new THREE.Vector3());
    const center = rawBox.getCenter(new THREE.Vector3());
    boxLines = new THREE.LineSegments(
      new THREE.EdgesGeometry(new THREE.BoxGeometry(1, 1, 1)),
      new THREE.LineBasicMaterial({ color: 0xffa94d, transparent: true, opacity: 0.9 })
    );
    boxLines.scale.copy(size);
    boxLines.position.copy(center);
    obj.add(boxLines);
    for (const f of FACE_DEFS) {
      const mark = new THREE.Mesh(
        new THREE.SphereGeometry(0.008, 12, 12),
        new THREE.MeshBasicMaterial({ color: 0x4dd2ff })
      );
      mark.position.set(
        center.x + (f.axis[0] * size.x) / 2,
        center.y + (f.axis[1] * size.y) / 2,
        center.z + (f.axis[2] * size.z) / 2
      );
      obj.add(mark);
      faceMarks.set(f.id as FaceId, mark);
    }
    updateFaceHighlight(currentFace);
  }

  /** 选中面的世界坐标中心点（面中心点标记当前所在位置）。 */
  function faceWorldCenter(id: FaceId): THREE.Vector3 | null {
    scene.updateMatrixWorld(true);
    const mark = faceMarks.get(id);
    if (!mark) return null;
    const v = new THREE.Vector3();
    mark.getWorldPosition(v);
    return v;
  }

  function applyTransform(t: ModelTransformValues, refit = true): void {
    if (!previewObj || !modelObj) return;
    previewObj.rotation.set(t.rotationX * DEG2RAD, t.rotationY * DEG2RAD, t.rotationZ * DEG2RAD);
    previewObj.position.set(t.positionX, t.positionY, t.positionZ);
    // 模型原点偏移（Unity 单位 → 预览单位）
    modelObj.position.set(t.pivotX * unitK, t.pivotY * unitK, t.pivotZ * unitK);
    // 模型显示尺寸 = Unity 实际尺寸 × 用户 scale（unitBase 归一化 three.js 与 Unity 的单位差异）
    previewObj.scale.setScalar(Math.max(0.0001, t.scale) * unitBase);
    updateSizeReadout(t.scale);
    // 视野跟随：交互结束后重新框住模型与参考容器（拖动/连续输入期间不跳相机，避免"飘"）
    if (refit) fitObjects([previewObj, refObj]);
  }

  /** 视野重新框住模型（拖动/输入结束后调用一次）。 */
  function refitView(): void {
    if (!previewObj) return;
    fitObjects([previewObj, refObj]);
  }

  function onLoaded(obj: THREE.Object3D): void {
    // 模型原始几何（无变换）测量
    rawBox = new THREE.Box3().setFromObject(obj);
    const rawSize = rawBox.getSize(new THREE.Vector3());
    if (opts.unitySize && Math.abs(opts.unitySize.y) > 0.0001 && Math.abs(rawSize.y) > 0.0001) {
      rawSizeU = { x: opts.unitySize.x, y: opts.unitySize.y, z: opts.unitySize.z };
      // 内容尺寸 = 世界包围盒反向旋转还原（摘要的 boundsSize 已含配置旋转；
      // 直接用世界包围盒的 Y 比值会因旋转轴交换而错得离谱，如 -90X 时 119/0.31=383x 而非 100x）。
      const content = unrotateSize(opts.unitySize, opts.rotationX ?? 0, opts.rotationY ?? 0, opts.rotationZ ?? 0);
      if (Math.abs(content.y) > 0.0001) {
        unitK = Math.abs(rawSize.y) / Math.abs(content.y);
        // 归一化显示：模型在预览里按 Unity 内容实际尺寸显示（与游戏一致）
        unitBase = 1 / unitK;
      }
    } else {
      // 无 Unity 实测尺寸（首次上传未保存）：用预览几何测量（异常单位模型保存后可再次校准）
      rawSizeU = { x: rawSize.x, y: rawSize.y, z: rawSize.z };
    }
    // 「变换节点（pivotGroup）」+「模型」结构：旋转/缩放绕原点（pivot 偏移在模型节点上）
    modelObj = obj;
    const pivotGroup = new THREE.Group();
    pivotGroup.name = "pivot";
    pivotGroup.add(obj);
    scene.add(pivotGroup);
    previewObj = pivotGroup;
    setupVirtualBox(obj);
    applyTransform(initial);
    if (opts.unitySize) {
      const db = new THREE.Box3().setFromObject(previewObj);
      const ds = db.getSize(new THREE.Vector3());
      console.log(
        `[ModelPreview] 预览显示尺寸（Unity 单位，应与游戏一致）：${ds.x.toFixed(3)} × ${ds.y.toFixed(3)} × ${ds.z.toFixed(3)} · 足迹 ${fmtCm(u2cm(Math.max(ds.x, ds.z)))} cm · 盘子 φ99.8 cm`
      );
    }
    fitObjects([pivotGroup, refObj]);
    status.textContent = "加载完成";
    status.classList.remove("err");
    const spinner = document.getElementById("mp-spinner");
    if (spinner) spinner.style.display = "none";
  }

  function onError(e: unknown): void {
    status.textContent = `加载失败：${e instanceof Error ? e.message : String(e)}`;
    status.classList.add("err");
    const spinner = document.getElementById("mp-spinner");
    if (spinner) spinner.style.display = "none";
  }

  // 参考容器（盘子/玻璃杯，纯视觉标的物，无碰撞）：包围盒中心 = 原点，随适配目标切换
  loadReference(opts.fitTarget ?? "plate");

  // 变换控件 DOM 与读写（顶层作用域：控件块与点击选原点共用）
  const mpIds = ["mp-scale", "mp-rot-x", "mp-rot-y", "mp-rot-z", "mp-pos-x", "mp-pos-y", "mp-pos-z"] as const;
  const mpEls = mpIds.map((id) => document.getElementById(id) as HTMLInputElement | null);
  /** 模型原点偏移（Unity 单位）：不再提供输入/按钮，由「点击模型上的点」设置。 */
  const curPivot = { x: initial.pivotX, y: initial.pivotY, z: initial.pivotZ };
  /** 模型原始尺寸（Unity 单位，不含任何变换）：加载时确定（优先 Unity 实测，
   *  否则用预览几何测量——异常单位模型保存后可再次校准）。 */
  let rawSizeU: { x: number; y: number; z: number } | null = null;

  const readTransform = (): ModelTransformValues => {
    const num = (el: HTMLInputElement | null, fallback: number): number => {
      const n = Number(el?.value);
      return Number.isFinite(n) ? n : fallback;
    };
    return {
      scale: num(mpEls[0], 1),
      rotationX: num(mpEls[1], 0),
      rotationY: num(mpEls[2], 0),
      rotationZ: num(mpEls[3], 0),
      positionX: cm2u(num(mpEls[4], 0)),
      positionY: cm2u(num(mpEls[5], 0)),
      positionZ: cm2u(num(mpEls[6], 0)),
      pivotX: curPivot.x,
      pivotY: curPivot.y,
      pivotZ: curPivot.z,
    };
  };

  /** 刷新「尺寸（足迹 cm）」输入与「当前足迹/长宽高 + 原点」读数（依赖模型原始尺寸）。
   *  足迹 = 世界包围盒（rawSizeU，已含配置旋转）的水平足迹 × scale = 模型实际水平尺寸，
   *  与预览显示和游戏内尺寸一致；
   *  尺寸输入正在被编辑（聚焦）时不回写，避免打断输入（如输入 7 时被改为 7.0000）。 */
  const updateSizeReadout = (scale: number): void => {
    const sizeEl = document.getElementById("mp-size-cm") as HTMLInputElement | null;
    const roEl = document.getElementById("mp-size-ro");
    if (!rawSizeU) {
      if (sizeEl) sizeEl.disabled = true;
      if (roEl) roEl.textContent = "当前尺寸：—（保存并上传后可精确校准；1 单位 = 100 cm）";
      return;
    }
    const foot = footprintOf(rawSizeU) || 1;
    if (sizeEl) {
      sizeEl.disabled = false;
      if (sizeEl !== document.activeElement) {
        sizeEl.value = fmtCm(u2cm(foot * scale));
      }
    }
    if (roEl) {
      roEl.textContent =
        `当前尺寸：足迹 ${fmtCm(u2cm(foot * scale))} cm · 长 ${fmtCm(u2cm(rawSizeU.x * scale))} × 宽 ${fmtCm(u2cm(rawSizeU.z * scale))} × ` +
        `高 ${fmtCm(u2cm(rawSizeU.y * scale))} cm（世界包围盒） · 原点 ${fmtCm(u2cm(curPivot.x))}/${fmtCm(u2cm(curPivot.y))}/${fmtCm(u2cm(curPivot.z))} cm（1 模型单位 = 100 cm）`;
    }
  };

  const writeTransform = (t: ModelTransformValues): void => {
    mpEls[0] && (mpEls[0].value = fmt4(t.scale));
    mpEls[1] && (mpEls[1].value = String(Math.round(t.rotationX)));
    mpEls[2] && (mpEls[2].value = String(Math.round(t.rotationY)));
    mpEls[3] && (mpEls[3].value = String(Math.round(t.rotationZ)));
    mpEls[4] && (mpEls[4].value = fmtCm(u2cm(t.positionX)));
    mpEls[5] && (mpEls[5].value = fmtCm(u2cm(t.positionY)));
    mpEls[6] && (mpEls[6].value = fmtCm(u2cm(t.positionZ)));
    curPivot.x = t.pivotX;
    curPivot.y = t.pivotY;
    curPivot.z = t.pivotZ;
    updateSizeReadout(t.scale);
  };

  // 调整控件（总是显示）：实时应用到预览对象，点「应用」（有回调时）写回菜谱表单
  {
    const applyBtn = document.getElementById("mp-apply");
    const fitBtn = document.getElementById("mp-fit");
    const rotX90Btn = document.getElementById("mp-rot-x90");
    const rotY90Btn = document.getElementById("mp-rot-y90");
    const rotZ90Btn = document.getElementById("mp-rot-z90");
    const fitSel = document.getElementById("mp-fit-target") as HTMLSelectElement | null;
    const faceSel = document.getElementById("mp-face") as HTMLSelectElement | null;
    fitSel?.addEventListener("change", () => {
      loadReference(fitSel.value === "cup" ? "cup" : "plate");
      status.textContent = fitSel.value === "cup" ? "参考容器：玻璃杯（口径 69 cm，食物放入杯中）" : "参考容器：盘子（直径 100 cm，食物放到盘面）";
      status.classList.remove("err");
    });
    faceSel?.addEventListener("change", () => {
      updateFaceHighlight((faceSel.value as FaceId) ?? "bottom");
    });

    const notify = (): void => {
      opts.onAdjust?.(readTransform());
    };

    for (const el of mpEls) {
      el?.addEventListener("input", () => {
        applyTransform(readTransform(), false);
        notify();
      });
      el?.addEventListener("change", () => refitView());
    }

    // 尺寸（足迹 cm）→ 反算相对缩放（需模型原始尺寸已测量；足迹 = 世界包围盒水平足迹）。
    // 输入过程中只更新缩放输入与预览，不回写尺寸输入本身（避免打断输入）。
    const sizeCmEl = document.getElementById("mp-size-cm") as HTMLInputElement | null;
    sizeCmEl?.addEventListener("input", () => {
      if (!rawSizeU) return;
      const cmVal = Number(sizeCmEl.value);
      if (!Number.isFinite(cmVal) || cmVal <= 0) return;
      const t = readTransform();
      t.scale = Math.max(1e-6, cm2u(cmVal) / (footprintOf(rawSizeU) || 1));
      if (mpEls[0]) mpEls[0].value = fmt4(t.scale);
      applyTransform(t, false);
      notify();
    });
    sizeCmEl?.addEventListener("change", () => {
      // 失焦/回车：按当前缩放回写 4 位小数并框住视野
      if (!rawSizeU) return;
      const t = readTransform();
      sizeCmEl.value = fmtCm(u2cm((footprintOf(rawSizeU) || 1) * t.scale));
      refitView();
    });

    applyBtn?.addEventListener("click", () => {
      try {
        const t = readTransform();
        if (opts.onAdjust) opts.onAdjust(t);
        status.textContent =
          `已应用：足迹 ${fmtCm(u2cm((footprintOf(rawSizeU ?? { x: 1, y: 1, z: 1 }) || 1) * t.scale))} cm（缩放 ${fmt4(t.scale)}）· ` +
          `旋转 ${t.rotationX}°/${t.rotationY}°/${t.rotationZ}° · 位置 ${fmtCm(u2cm(t.positionX))}/${fmtCm(u2cm(t.positionY))}/${fmtCm(u2cm(t.positionZ))} cm（在编辑页再次保存生效）`;
        status.classList.remove("err");
      } catch (e) {
        status.textContent = `应用失败：${e instanceof Error ? e.message : String(e)}`;
        status.classList.add("err");
      }
    });

    /** 快捷旋转 90°（竖立模型摆平/转向）：在当前旋转值上加 90°，立即应用并写回。 */
    const rotate90 = (axis: "x" | "y" | "z"): void => {
      const t = readTransform();
      if (axis === "x") t.rotationX = (t.rotationX + 90) % 360;
      else if (axis === "y") t.rotationY = (t.rotationY + 90) % 360;
      else t.rotationZ = (t.rotationZ + 90) % 360;
      writeTransform(t);
      applyTransform(t);
      opts.onAdjust?.(t);
      status.textContent = `已旋转：${axis.toUpperCase()} +90°（旋转 X ${t.rotationX}° · Y ${t.rotationY}° · Z ${t.rotationZ}°，保存后游戏内生效）`;
      status.classList.remove("err");
    };
    rotX90Btn?.addEventListener("click", () => rotate90("x"));
    rotY90Btn?.addEventListener("click", () => rotate90("y"));
    rotZ90Btn?.addEventListener("click", () => rotate90("z"));

    // ---- 虚拟箱面操作：面中心设为原点 / 该面朝下 ----
    // 优先用 Unity 实测（后端按模型内容包围盒计算，避免 three.js 与 Unity 解析差异）；
    // 后端不可用（未保存的本地模型）时回退本地计算。

    /** 面中心设为原点：旋转/缩放绕该面中心（与「点击模型选点」一致，取面中心更精确）。 */
    document.getElementById("mp-face-origin")?.addEventListener("click", () => {
      const id = (faceSel?.value as FaceId) ?? "bottom";
      const label = FACE_DEFS.find((f) => f.id === id)?.label ?? id;
      const applyPivot = (px: number, py: number, pz: number, source: string): void => {
        const t = readTransform();
        t.pivotX = px;
        t.pivotY = py;
        t.pivotZ = pz;
        writeTransform(t);
        applyTransform(t);
        opts.onAdjust?.(t);
        refitView();
        status.textContent = `原点已设为「${label}」面中心（${source}；旋转/缩放将绕该面中心，可在右侧读数查看原点坐标）`;
        status.classList.remove("err");
      };
      // 本地计算：面中心点 → 模型节点局部坐标（预览单位 → Unity 单位）
      if (!modelObj) return;
      const world = faceWorldCenter(id);
      if (!world) return;
      const local = modelObj.worldToLocal(world.clone());
      console.log(`[ModelPreview] 面原点（本地）face=${id} local=${local.x.toFixed(4)},${local.y.toFixed(4)},${local.z.toFixed(4)} unitK=${unitK.toFixed(4)}`);
      applyPivot(-local.x / unitK, -local.y / unitK, -local.z / unitK, "本地预览计算，保存后可再校准");
    });

    /** 该面朝下：旋转模型使选中面的法线指向 -Y（底面贴地）。 */
    document.getElementById("mp-face-down")?.addEventListener("click", () => {
      const id = (faceSel?.value as FaceId) ?? "bottom";
      const f = FACE_DEFS.find((x) => x.id === id);
      if (!f || !modelObj) return;
      const applyRot = (x: number, y: number, z: number, source: string): void => {
        const t = readTransform();
        t.rotationX = x;
        t.rotationY = y;
        t.rotationZ = z;
        writeTransform(t);
        applyTransform(t);
        opts.onAdjust?.(t);
        status.textContent = `已旋转使「${f.label}」面朝下（${source}：旋转 X/Y/Z = ${x.toFixed(2)}°/${y.toFixed(2)}°/${z.toFixed(2)}°，保存后游戏内生效）`;
        status.classList.remove("err");
      };
      // 本地计算：面法线世界方向（模型节点当前朝向）→ 对齐到 -Y
      scene.updateMatrixWorld(true);
      const q = new THREE.Quaternion();
      modelObj.getWorldQuaternion(q);
      const worldDir = new THREE.Vector3(f.axis[0], f.axis[1], f.axis[2]).applyQuaternion(q).normalize();
      const down = new THREE.Vector3(0, -1, 0);
      if (worldDir.dot(down) > 0.999999) {
        status.textContent = `「${f.label}」已朝下，无需旋转`;
        status.classList.remove("err");
        return;
      }
      const qAlign = new THREE.Quaternion().setFromUnitVectors(worldDir, down);
      const e = new THREE.Euler().setFromQuaternion(qAlign.multiply(q), "XYZ");
      console.log(`[ModelPreview] 面朝下（本地）face=${id} worldDir=${worldDir.x.toFixed(3)},${worldDir.y.toFixed(3)},${worldDir.z.toFixed(3)} euler=${(e.x / DEG2RAD).toFixed(2)},${(e.y / DEG2RAD).toFixed(2)},${(e.z / DEG2RAD).toFixed(2)}`);
      applyRot(e.x / DEG2RAD, e.y / DEG2RAD, e.z / DEG2RAD, "本地预览计算，保存后可再校准");
    });

    /** 自动适配：按目标足迹缩放，位置归零（X/Y/Z 均为 0，不自动下沉，高度手动调整）。
     *  目标足迹：盘子 85 cm（官方蛋炒饭参考 82~87 cm，游戏盘子直径 100 cm）、
     *  杯子 37 cm（玻璃杯口径 69 cm，食物放入杯中）。
     *  足迹基准 = 世界包围盒水平足迹（rawSizeU 已含配置旋转，旋转后即为游戏内实际尺寸），
     *  不再重复旋转。优先使用 Unity 实际导入尺寸（unitySize）。 */
    fitBtn?.addEventListener("click", () => {
      if (!previewObj) return;
      const cur = readTransform();
      const kind = fitSel?.value === "cup" ? "cup" : "plate";
      const target = kind === "cup" ? CUP_FIT_CM / CM_PER_UNIT : PLATE_FIT_CM / CM_PER_UNIT;
      let scale: number;
      if (opts.unitySize) {
        // 按 Unity 实际尺寸（世界包围盒足迹 = 游戏内实际水平尺寸）
        const footprint = footprintOf(opts.unitySize) || 1;
        scale = Math.max(1e-6, target / footprint);
      } else {
        // 无 Unity 尺寸（首次上传未保存）：用预览测量，结果可能受单位差异影响
        previewObj.scale.setScalar(1);
        previewObj.rotation.set(0, 0, 0);
        previewObj.position.set(0, 0, 0);
        if (modelObj) modelObj.position.set(0, 0, 0); // 测量原始几何，排除原点偏移
        previewObj.scale.setScalar(unitBase); // 归一化到 Unity 尺寸后测量
        const box2 = new THREE.Box3().setFromObject(previewObj);
        const size2 = box2.getSize(new THREE.Vector3());
        const footprint = Math.max(size2.x, size2.z) || 1;
        scale = Math.max(1e-6, target / footprint);
      }
      previewObj.scale.setScalar(scale * unitBase);
      previewObj.rotation.set(cur.rotationX * DEG2RAD, cur.rotationY * DEG2RAD, cur.rotationZ * DEG2RAD);
      // 位置归零：Y 始终为 0（不自动下沉），高度用「位置 Y」手动调整
      previewObj.position.set(0, 0, 0);
      if (modelObj) modelObj.position.set(cur.pivotX * unitK, cur.pivotY * unitK, cur.pivotZ * unitK);
      const fitted: ModelTransformValues = {
        scale,
        rotationX: cur.rotationX,
        rotationY: cur.rotationY,
        rotationZ: cur.rotationZ,
        positionX: 0,
        positionY: 0,
        positionZ: 0,
        pivotX: cur.pivotX,
        pivotY: cur.pivotY,
        pivotZ: cur.pivotZ,
      };
      writeTransform(fitted);
      opts.onAdjust?.(fitted);
      status.textContent = `已自动适配（${kind === "cup" ? "玻璃杯目标：足迹 37 cm，食物放入杯中" : "盘子目标：足迹 85 cm，食物放到盘面"} · 位置 Y = 0（不自动下沉，高度请手动调「位置 Y」） · ${opts.unitySize ? "Unity 实际尺寸" : "预览尺寸，保存后可再次校准"}）：足迹 ${fmtCm(u2cm((footprintOf(rawSizeU ?? { x: 1, y: 1, z: 1 }) || 1) * scale))} cm（保存后游戏内生效）`;
      status.classList.remove("err");
      fitObjects([previewObj, refObj]);
    });
  }

  // 「重选原点」点击选点模式
  {
    const raycaster = new THREE.Raycaster();
    const pointer = new THREE.Vector2();
    /** 选点模式：点「重选原点」后进入，点击模型上的点设置原点；悬停模型显示十字光标。 */
    let pickingOrigin = false;
    const setPointer = (e: PointerEvent): void => {
      const rect = canvas.getBoundingClientRect();
      pointer.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
      pointer.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
    };
    /** 选点模式退出：恢复默认光标与提示。 */
    const exitPicking = (msg: string): void => {
      pickingOrigin = false;
      canvas.style.cursor = "";
      status.textContent = msg;
      status.classList.remove("err");
    };
    document.getElementById("mp-pick-origin")?.addEventListener("click", () => {
      pickingOrigin = true;
      canvas.style.cursor = "crosshair";
      status.textContent = "选点模式：点击模型上的点设置原点（旋转/缩放将绕该点；也可用「虚拟箱面 → 面中心设为原点」）";
      status.classList.remove("err");
    });
    canvas.addEventListener("pointermove", (e) => {
      // 选点模式：悬停模型显示十字光标
      if (!pickingOrigin || !modelObj) return;
      setPointer(e);
      raycaster.setFromCamera(pointer, camera);
      canvas.style.cursor = raycaster.intersectObject(modelObj, true).length > 0 ? "crosshair" : "default";
    });

    // 选点模式：点击模型上的任意点 → 该点成为原点（旋转/缩放绕该点；预览单位 → Unity 单位）
    canvas.addEventListener("click", (e) => {
      if (!pickingOrigin || !modelObj) return;
      setPointer(e);
      raycaster.setFromCamera(pointer, camera);
      const hits = raycaster.intersectObject(modelObj, true);
      if (hits.length === 0) return;
      const local = modelObj.worldToLocal(hits[0].point.clone());
      const t = readTransform();
      t.pivotX = -local.x / unitK;
      t.pivotY = -local.y / unitK;
      t.pivotZ = -local.z / unitK;
      console.log(
        `[ModelPreview] 点击设原点 point=${hits[0].point.x.toFixed(4)},${hits[0].point.y.toFixed(4)},${hits[0].point.z.toFixed(4)} local=${local.x.toFixed(4)},${local.y.toFixed(4)},${local.z.toFixed(4)} unitK=${unitK.toFixed(4)} pivot=${t.pivotX.toFixed(4)},${t.pivotY.toFixed(4)},${t.pivotZ.toFixed(4)}`
      );
      writeTransform(t);
      applyTransform(t);
      opts.onAdjust?.(t);
      refitView();
      exitPicking(`原点已设为点击位置（旋转/缩放将绕该点；如需重选再点「重选原点」）`);
    });
  }

  const ext = opts.modelFileName.split(".").pop()?.toLowerCase() ?? "";
  if (opts.localBuffer) {
    // 本地预览：直接解析 ArrayBuffer（未保存），贴图手动注入
    try {
      if (ext === "obj") {
        const text = new TextDecoder("utf-8").decode(opts.localBuffer);
        if (opts.localMtl) {
          // MTL + OBJ：MTLLoader 解析材质（贴图引用由 MTL 指定），OBJLoader 组合
          const materials = new MTLLoader().parse(opts.localMtl, "");
          const loader = new OBJLoader();
          loader.setMaterials(materials.materials as never);
          const group = loader.parse(text);
          if (opts.localTextures && opts.localTextures.length > 0) {
            injectLocalTextures(group, opts.localTextures);
          }
          onLoaded(group);
        } else {
          onLoaded(new OBJLoader().parse(text));
        }
      } else {
        const loader = new FBXLoader();
        const group = loader.parse(opts.localBuffer, "");
        if (opts.localTextures && opts.localTextures.length > 0) {
          injectLocalTextures(group, opts.localTextures);
        }
        onLoaded(group);
      }
    } catch (e) {
      onError(e);
    }
  } else if (ext === "fbx") {
    const loader = new FBXLoader();
    loader.resourcePath = opts.resourceBase;
    loader.load(
      opts.resourceBase + encodeURIComponent(opts.modelFileName),
      (group) => {
        // 无 Video 节点的 FBX（three 无法按内嵌引用取贴图）→ 注入服务端贴图
        if (opts.remoteTextures && opts.remoteTextures.length > 0) {
          injectRemoteTextures(group, opts.remoteTextures);
        }
        onLoaded(group);
      },
      undefined,
      onError
    );
  } else if (ext === "obj") {
    // OBJ：优先 MTL + 贴图（目录式服务），MTL 缺失时仅几何体
    if (opts.mtlUrl) {
      new MTLLoader().load(
        opts.mtlUrl,
        (materials) => {
          const loader = new OBJLoader();
          loader.setMaterials(materials.materials as never);
          loader.load(opts.resourceBase + encodeURIComponent(opts.modelFileName), onLoaded, undefined, onError);
        },
        undefined,
        () => new OBJLoader().load(opts.resourceBase + encodeURIComponent(opts.modelFileName), onLoaded, undefined, onError)
      );
    } else {
      new OBJLoader().load(opts.resourceBase + encodeURIComponent(opts.modelFileName), onLoaded, undefined, onError);
    }
  } else {
    status.textContent = "仅支持 FBX / OBJ 预览";
    status.classList.add("err");
  }

  const clock = new THREE.Clock();
  function animate(): void {
    if (!canvas.isConnected) {
      disposeActive();
      return;
    }
    controls.update();
    renderer.render(scene, camera);
    active!.raf = requestAnimationFrame(animate);
    clock.getDelta();
  }
  active = { raf: 0, renderer };
  active.raf = requestAnimationFrame(animate);

  // 弹窗打开后重算尺寸（wide 类可能延迟生效）
  requestAnimationFrame(() => {
    const w = wrap.clientWidth || width;
    const h = wrap.clientHeight || height;
    renderer.setSize(w, h);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    // 布局稳定后再校正一次（全屏 modal 的 flex 布局可能延迟生效）
    setTimeout(() => {
      const w2 = wrap.clientWidth || w;
      const h2 = wrap.clientHeight || h;
      if (Math.abs(w2 - w) > 2 || Math.abs(h2 - h) > 2) {
        renderer.setSize(w2, h2);
        camera.aspect = w2 / h2;
        camera.updateProjectionMatrix();
        fitObjects([previewObj, refObj]);
      }
    }, 150);
  });
}
