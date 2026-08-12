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

/** 模型变换：缩放 + 三轴旋转（度）+ 三轴位置（单位，Y 为底面高度）。 */
export interface ModelTransformValues {
  scale: number;
  rotationX: number;
  rotationY: number;
  rotationZ: number;
  positionX: number;
  positionY: number;
  positionZ: number;
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
  /** 调整回调：用户在弹窗内修改缩放/旋转/位置时触发，用于写回菜谱表单。 */
  onAdjust?: (t: ModelTransformValues) => void;
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
  /** 初始参照物类型：plate（盘子，默认）/ cup（杯子示意，程序化生成）。 */
  referenceType?: "plate" | "cup";
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
  };
  const controlsHtml = `<div class="mp-controls">
    <label>缩放 <input type="number" id="mp-scale" value="${initial.scale}" step="0.001" min="0.0001"></label>
    <label>旋转X（度）<input type="number" id="mp-rot-x" value="${initial.rotationX}" step="5"></label>
    <label>旋转Y（度）<input type="number" id="mp-rot-y" value="${initial.rotationY}" step="5"></label>
    <label>旋转Z（度）<input type="number" id="mp-rot-z" value="${initial.rotationZ}" step="5"></label>
    <label>位置X<input type="number" id="mp-pos-x" value="${initial.positionX}" step="0.05"></label>
    <label>位置Y（底面高度）<input type="number" id="mp-pos-y" value="${initial.positionY}" step="0.05"></label>
    <label>位置Z<input type="number" id="mp-pos-z" value="${initial.positionZ}" step="0.05"></label>
    <label>参照物<select id="mp-ref-type">
      <option value="plate" ${(opts.referenceType ?? "plate") === "plate" ? "selected" : ""}>盘子</option>
      <option value="cup" ${opts.referenceType === "cup" ? "selected" : ""}>杯子（示意）</option>
    </select></label>
    <button type="button" class="m-btn" id="mp-rot-x90">↻ 绕 X 转 90°</button>
    <button type="button" class="m-btn" id="mp-rot-z90">↻ 绕 Z 转 90°</button>
    <button type="button" class="m-btn" id="mp-fit">✨ 自动适配盘子</button>
    ${opts.onAdjust ? `<button type="button" class="m-btn primary" id="mp-apply">✅ 应用方向/大小到菜谱</button>` : ""}
  </div>`;
  openModal(
    `3D 模型预览 · ${esc(opts.title)}`,
    `<div class="mp-wrap"><canvas id="mp-canvas"></canvas></div>
     <p class="mp-status" id="mp-status"><span class="busy-spinner mp-inline-spinner" id="mp-spinner"></span> 加载中…</p>
     ${controlsHtml}
     <p class="modal-hint">左键旋转 · 右键平移 · 滚轮缩放 · 「自动适配盘子」一键摆平竖立模型并缩放到盘子大小（与正常装盘模型一致）；也可手动调整缩放/旋转/位置修正游戏内大小、朝向与底面高度 · 左侧半透明为参照物（可切换盘子/杯子示意），对比调整实际大小</p>`,
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

  /** 把参考模型放到主模型左侧（相机默认在 +x 侧，避免被主模型遮挡）。
   *  fit 后主模型尺寸变化，需要重新放置。 */
  function positionRefBeside(): void {
    if (!refObj) return;
    const rb = new THREE.Box3().setFromObject(refObj);
    const rsize = rb.getSize(new THREE.Vector3());
    let gap = Math.max(rsize.x, rsize.z) / 2 + 0.15;
    if (previewObj) {
      const mb = new THREE.Box3().setFromObject(previewObj);
      const msize = mb.getSize(new THREE.Vector3());
      gap = Math.max(msize.x, msize.z) / 2 + Math.max(rsize.x, rsize.z) / 2 + 0.15;
    }
    refObj.position.x = -gap;
  }

  function disposeObject(obj: THREE.Object3D): void {
    obj.traverse((child) => {
      const mesh = child as THREE.Mesh;
      if (mesh.isMesh) {
        mesh.geometry?.dispose();
        const mat = mesh.material as THREE.Material | THREE.Material[] | undefined;
        if (Array.isArray(mat)) mat.forEach((m) => m.dispose());
        else mat?.dispose();
      }
    });
  }

  /** 加载/切换参照物：plate 从服务器加载盘子模型；cup 为程序化生成的示意杯。 */
  function loadReference(kind: "plate" | "cup"): void {
    if (refObj) {
      scene.remove(refObj);
      disposeObject(refObj);
      refObj = null;
    }
    if (kind === "cup") {
      // 示意杯（非游戏模型）：口径 0.44 / 底 0.30 / 高 0.70，半透明
      const geo = new THREE.CylinderGeometry(0.22, 0.15, 0.7, 24, 1, true);
      const mat = new THREE.MeshStandardMaterial({
        color: 0x9ab8d8,
        transparent: true,
        opacity: 0.65,
        side: THREE.DoubleSide,
        depthWrite: false,
      });
      const cup = new THREE.Mesh(geo, mat);
      cup.position.y = 0.35;
      refObj = cup;
      scene.add(cup);
      positionRefBeside();
      if (previewObj) fitObjects([previewObj, refObj]);
      return;
    }
    if (!opts.referenceUrl) return;
    const loader = opts.referenceFormat === "fbx" ? new FBXLoader() : new OBJLoader();
    loader.load(
      opts.referenceUrl,
      (ref) => {
        if (refObj) return; // 已切换类型，忽略迟到的加载
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
        ref.scale.setScalar(opts.referenceScale ?? 1);
        refObj = ref;
        scene.add(ref);
        positionRefBeside();
        if (previewObj) fitObjects([previewObj, ref]);
      },
      undefined,
      () => {
        // 参考模型加载失败不影响主预览
      }
    );
  }

  function applyTransform(t: ModelTransformValues): void {
    if (!previewObj) return;
    previewObj.scale.setScalar(Math.max(0.001, t.scale));
    previewObj.rotation.set(t.rotationX * DEG2RAD, t.rotationY * DEG2RAD, t.rotationZ * DEG2RAD);
    previewObj.position.set(t.positionX, t.positionY, t.positionZ);
  }

  function onLoaded(obj: THREE.Object3D): void {
    previewObj = obj;
    scene.add(obj);
    applyTransform(initial);
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

  // 参考模型（盘子/杯子示意）：并排显示，用于对比实际大小
  loadReference(opts.referenceType ?? "plate");

  // 调整控件（总是显示）：实时应用到预览对象，点「应用」（有回调时）写回菜谱表单
  {
    const ids = ["mp-scale", "mp-rot-x", "mp-rot-y", "mp-rot-z", "mp-pos-x", "mp-pos-y", "mp-pos-z"] as const;
    const els = ids.map((id) => document.getElementById(id) as HTMLInputElement | null);
    const applyBtn = document.getElementById("mp-apply");
    const fitBtn = document.getElementById("mp-fit");
    const rotX90Btn = document.getElementById("mp-rot-x90");
    const rotZ90Btn = document.getElementById("mp-rot-z90");
    const refTypeSel = document.getElementById("mp-ref-type") as HTMLSelectElement | null;
    refTypeSel?.addEventListener("change", () => {
      loadReference(refTypeSel.value === "cup" ? "cup" : "plate");
      status.textContent = refTypeSel.value === "cup" ? "参照物：杯子（示意，非游戏模型）" : "参照物：盘子";
      status.classList.remove("err");
    });

    const read = (): ModelTransformValues => {
      const num = (el: HTMLInputElement | null, fallback: number): number => {
        const n = Number(el?.value);
        return Number.isFinite(n) ? n : fallback;
      };
      return {
        scale: num(els[0], 1),
        rotationX: num(els[1], 0),
        rotationY: num(els[2], 0),
        rotationZ: num(els[3], 0),
        positionX: num(els[4], 0),
        positionY: num(els[5], 0),
        positionZ: num(els[6], 0),
      };
    };

    const write = (t: ModelTransformValues): void => {
      els[0] && (els[0].value = String(Math.round(t.scale * 10000) / 10000));
      els[1] && (els[1].value = String(Math.round(t.rotationX)));
      els[2] && (els[2].value = String(Math.round(t.rotationY)));
      els[3] && (els[3].value = String(Math.round(t.rotationZ)));
      els[4] && (els[4].value = String(Math.round(t.positionX * 100) / 100));
      els[5] && (els[5].value = String(Math.round(t.positionY * 100) / 100));
      els[6] && (els[6].value = String(Math.round(t.positionZ * 100) / 100));
    };

    const notify = (): void => {
      opts.onAdjust?.(read());
    };

    for (const el of els) {
      el?.addEventListener("input", () => {
        applyTransform(read());
        notify();
      });
    }

    applyBtn?.addEventListener("click", () => {
      try {
        const t = read();
        if (opts.onAdjust) opts.onAdjust(t);
        status.textContent = `已应用：缩放 ${t.scale} · 旋转 ${t.rotationX}°/${t.rotationY}°/${t.rotationZ}° · 位置 ${t.positionX}/${t.positionY}/${t.positionZ}（在编辑页再次保存生效）`;
        status.classList.remove("err");
      } catch (e) {
        status.textContent = `应用失败：${e instanceof Error ? e.message : String(e)}`;
        status.classList.add("err");
      }
    });

    /** 快捷旋转 90°（竖立模型摆平）：在当前旋转值上加 90°，立即应用并写回。 */
    const rotate90 = (axis: "x" | "z"): void => {
      const t = read();
      if (axis === "x") t.rotationX = (t.rotationX + 90) % 360;
      else t.rotationZ = (t.rotationZ + 90) % 360;
      write(t);
      applyTransform(t);
      opts.onAdjust?.(t);
      status.textContent = `已旋转：${axis.toUpperCase()} +90°（旋转 X ${t.rotationX}° · Z ${t.rotationZ}°，保存后游戏内生效）`;
      status.classList.remove("err");
    };
    rotX90Btn?.addEventListener("click", () => rotate90("x"));
    rotZ90Btn?.addEventListener("click", () => rotate90("z"));

    /** 自动适配：摆平竖立模型 → 缩放到参照物尺寸 → 放入容器（盘子贴底 / 杯子抬升到杯中）。
     *  目标尺寸按参照物类型：盘子 0.75（线上正常装盘模型足迹），杯子 0.37（示意杯内径 0.44×0.85）。
     *  优先使用 Unity 实际导入尺寸（unitySize），避免 three.js 与 Unity 的单位/朝向差异。 */
    fitBtn?.addEventListener("click", () => {
      if (!previewObj) return;
      const cur = read();
      const refKind = refTypeSel?.value === "cup" ? "cup" : "plate";
      const target = refKind === "cup" ? 0.37 : 0.75;
      // 杯子：把模型底抬到杯内 0.3 高度，顶部露出杯口；盘子：底面贴盘面（y=0）
      const cupLift = 0.3;
      let rotX = 0;
      let rotZ = 0;
      let scale: number;
      let posY: number;
      if (opts.unitySize) {
        // 按 Unity 实际尺寸：薄轴判定 + 缩放 + 位置
        const us = opts.unitySize;
        const h = Math.abs(us.y);
        const w = Math.abs(us.x);
        const d = Math.abs(us.z);
        const minHor = Math.min(w, d);
        const maxHor = Math.max(w, d);
        if (minHor < h / 2 && minHor < maxHor / 2) {
          if (w <= d) rotX = 90;
          else rotZ = 90;
        }
        const footprint = Math.max(us.x, us.z) || 1;
        scale = Math.max(1e-6, target / Math.abs(footprint));
        posY = refKind === "cup" ? cupLift - us.minY * scale : -us.minY * scale;
      } else {
        // 无 Unity 尺寸（首次上传未保存）：用预览测量，结果可能受单位差异影响
        previewObj.scale.setScalar(1);
        previewObj.rotation.set(0, 0, 0);
        previewObj.position.set(0, 0, 0);
        const box = new THREE.Box3().setFromObject(previewObj);
        const size = box.getSize(new THREE.Vector3());
        const h = size.y;
        const w = size.x;
        const d = size.z;
        const minHor = Math.min(w, d);
        const maxHor = Math.max(w, d);
        if (minHor < h / 2 && minHor < maxHor / 2) {
          if (w <= d) rotX = 90;
          else rotZ = 90;
        }
        previewObj.rotation.set(rotX * DEG2RAD, 0, rotZ * DEG2RAD);
        const box2 = new THREE.Box3().setFromObject(previewObj);
        const size2 = box2.getSize(new THREE.Vector3());
        const footprint = Math.max(size2.x, size2.z) || 1;
        scale = Math.max(1e-6, target / footprint);
        previewObj.scale.setScalar(scale);
        const box3 = new THREE.Box3().setFromObject(previewObj);
        posY = refKind === "cup" ? cupLift - box3.min.y : -box3.min.y;
      }
      previewObj.scale.setScalar(scale);
      previewObj.rotation.set(rotX * DEG2RAD, 0, rotZ * DEG2RAD);
      previewObj.position.set(0, posY, 0);
      const fitted: ModelTransformValues = {
        scale,
        rotationX: rotX,
        rotationY: cur.rotationY,
        rotationZ: rotZ,
        positionX: 0,
        positionY: posY,
        positionZ: 0,
      };
      write(fitted);
      opts.onAdjust?.(fitted);
      status.textContent = `已自动适配（${refKind === "cup" ? "杯子参照：按杯内径缩放并抬升到杯中" : "盘子参照"}${opts.unitySize ? " · Unity 实际尺寸" : " · 预览尺寸，保存后可再次校准"}）：缩放 ${scale.toFixed(4)} · 旋转 X/Z ${rotX}°/${rotZ}° · 位置Y ${posY.toFixed(3)}（保存后游戏内生效）`;
      status.classList.remove("err");
      positionRefBeside();
      fitObjects([previewObj, refObj]);
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
