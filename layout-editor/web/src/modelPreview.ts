import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import { FBXLoader } from "three/examples/jsm/loaders/FBXLoader.js";
import { OBJLoader } from "three/examples/jsm/loaders/OBJLoader.js";
import { openModal, closeModal } from "./modals";

/** 3D 模型在线预览（three.js）：FBX 会尝试按 FBX 内嵌的相对路径经
 *  resourceBase 加载贴图（PNG/JPG）；OBJ 仅展示几何体。 */

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

let active: { raf: number; renderer: THREE.WebGLRenderer } | null = null;

export interface ModelPreviewOptions {
  title: string;
  /** 模型文件访问基地址（目录式，FBX 贴图按相对路径拼接）。 */
  resourceBase: string;
  /** 模型文件名（.fbx / .obj）。 */
  modelFileName: string;
  /** 初始缩放（应用到预览对象，默认 1）。 */
  scale?: number;
  /** 初始 Y 轴旋转（度，默认 0）。 */
  rotationY?: number;
  /** 调整回调：用户在弹窗内修改缩放/旋转时触发，用于写回菜谱表单。 */
  onAdjust?: (scale: number, rotationY: number) => void;
  /** 本地模型（选择文件后未保存）：直接解析 ArrayBuffer，不走服务器。 */
  localBuffer?: ArrayBuffer;
  /** 本地贴图（File 列表）：FBX 内嵌纹理无法加载时注入到全部材质。 */
  localTextures?: File[];
  /** 服务端贴图 URL 列表：FBX 无 Video 节点（three 无法解析内嵌引用）时注入。 */
  remoteTextures?: string[];
  /** 参考模型（盘子/披萨装盘），并排显示用于对比实际大小。 */
  referenceUrl?: string;
  /** 参考模型格式（带 query 的 URL 无法按扩展名判断）。 */
  referenceFormat?: "obj" | "fbx";
  /** 参考模型实际缩放（prefab 根 scale，OBJ 源文件为建模坐标）。 */
  referenceScale?: number;
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
  const initialScale = opts.scale ?? 1;
  const initialRotY = opts.rotationY ?? 0;
  const controlsHtml = `<div class="mp-controls">
    <label>缩放 <input type="number" id="mp-scale" value="${initialScale}" step="0.05" min="0.01"></label>
    <label>Y 旋转（度）<input type="number" id="mp-rot" value="${initialRotY}" step="5"></label>
    ${opts.onAdjust ? `<button type="button" class="m-btn primary" id="mp-apply">✅ 应用方向/大小到菜谱</button>` : ""}
  </div>`;
  openModal(
    `3D 模型预览 · ${esc(opts.title)}`,
    `<div class="mp-wrap"><canvas id="mp-canvas"></canvas></div>
     <p class="mp-status" id="mp-status"><span class="busy-spinner mp-inline-spinner" id="mp-spinner"></span> 加载中…</p>
     ${controlsHtml}
     <p class="modal-hint">左键旋转 · 右键平移 · 滚轮缩放 · 调整「缩放 / Y 旋转」修正模型在游戏中的大小与朝向${opts.referenceUrl ? " · 右侧半透明为参考盘子，对比调整实际大小" : ""}</p>`,
    `<button type="button" class="m-btn" data-cancel>关闭</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  const panel = document.querySelector(".modal-panel");
  if (panel) panel.classList.add("wide", "mp-panel");

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
    grid = new THREE.GridHelper(size * 2, 20, 0x3d6bf3, 0x333a4a);
    grid.position.y = box.min.y;
    scene.add(grid);
  }

  let previewObj: THREE.Object3D | null = null;
  let refObj: THREE.Object3D | null = null;

  function applyTransform(scale: number, rotY: number): void {
    if (!previewObj) return;
    previewObj.scale.setScalar(Math.max(0.001, scale));
    previewObj.rotation.y = rotY * DEG2RAD;
  }

  function onLoaded(obj: THREE.Object3D): void {
    previewObj = obj;
    scene.add(obj);
    applyTransform(initialScale, initialRotY);
    fitObjects([obj, refObj]);
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

  // 参考模型（盘子/披萨装盘）：并排显示，用于对比实际大小
  if (opts.referenceUrl) {
    const loader = opts.referenceFormat === "fbx" ? new FBXLoader() : new OBJLoader();
    loader.load(
      opts.referenceUrl,
      (ref) => {
        ref.traverse((child) => {
          const mesh = child as THREE.Mesh;
          if (!mesh.isMesh) return;
          const mat = mesh.material as THREE.MeshStandardMaterial | THREE.MeshPhongMaterial | undefined;
          if (mat) {
            mat.transparent = true;
            mat.opacity = 0.65;
            mat.color = new THREE.Color(0x9ab8d8);
          }
        });
        // OBJ 源文件为建模坐标，应用 prefab 根缩放才是游戏内尺寸
        const refScale = opts.referenceScale ?? 1;
        ref.scale.setScalar(refScale);
        const box = new THREE.Box3().setFromObject(ref);
        const size = box.getSize(new THREE.Vector3());
        const half = Math.max(size.x, size.z) / 2;
        ref.position.x = half + 0.15;
        refObj = ref;
        scene.add(ref);
        if (previewObj) fitObjects([previewObj, ref]);
      },
      undefined,
      () => {
        // 参考模型加载失败不影响主预览
      }
    );
  }

  // 调整控件（总是显示）：实时应用到预览对象，点「应用」（有回调时）写回菜谱表单
  {
    const scaleInput = document.getElementById("mp-scale") as HTMLInputElement | null;
    const rotInput = document.getElementById("mp-rot") as HTMLInputElement | null;
    const applyBtn = document.getElementById("mp-apply");
    const read = (): [number, number] => [
      Number(scaleInput?.value) || 1,
      Number(rotInput?.value) || 0,
    ];
    scaleInput?.addEventListener("input", () => {
      const [s, r] = read();
      applyTransform(s, r);
      opts.onAdjust?.(s, r);
    });
    rotInput?.addEventListener("input", () => {
      const [s, r] = read();
      applyTransform(s, r);
      opts.onAdjust?.(s, r);
    });
    applyBtn?.addEventListener("click", () => {
      const [s, r] = read();
      opts.onAdjust!(s, r);
      status.textContent = `已应用：缩放 ${s} · Y 旋转 ${r}°（在编辑页再次保存生效）`;
      status.classList.remove("err");
    });
  }

  const ext = opts.modelFileName.split(".").pop()?.toLowerCase() ?? "";
  if (opts.localBuffer) {
    // 本地预览：直接解析 ArrayBuffer（未保存），贴图手动注入
    try {
      const loader = new FBXLoader();
      const group = loader.parse(opts.localBuffer, "");
      if (opts.localTextures && opts.localTextures.length > 0) {
        injectLocalTextures(group, opts.localTextures);
      }
      onLoaded(group);
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
    new OBJLoader().load(opts.resourceBase + encodeURIComponent(opts.modelFileName), onLoaded, undefined, onError);
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
  });
}
