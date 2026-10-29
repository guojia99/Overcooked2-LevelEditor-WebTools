/**
 * 装饰物统一预览：双视图（正面/背面）+ 3D 模型，同一个弹窗。
 *
 * - 默认打开正面；背面按需切换。
 * - 3D 只有点击「3D」页签时才请求 OBJ/MTL 与贴图，并显示加载动画/进度条。
 * - 2D 支持滚轮缩放、拖拽平移、双击 1×/2.5×；3D 使用 OrbitControls。
 */
import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import { OBJLoader } from "three/examples/jsm/loaders/OBJLoader.js";
import { MTLLoader } from "three/examples/jsm/loaders/MTLLoader.js";
import { escHtml } from "../coords";
import { decorDetailUrl, decorModelInfo, decorPreviewUrl, type DecorView } from "../decorPreviewCache";

type DecorPreviewTab = DecorView | "3d";
type ModelState = "idle" | "loading" | "ready" | "error";

interface ThreeState {
  renderer: THREE.WebGLRenderer;
  scene: THREE.Scene;
  camera: THREE.PerspectiveCamera;
  controls: OrbitControls;
  object: THREE.Object3D;
  grid: THREE.GridHelper;
  raf: number;
  resizeObserver: ResizeObserver | null;
  fit: () => void;
}

interface LightboxState {
  root: HTMLElement;
  guid: string;
  label: string;
  tab: DecorPreviewTab;
  img: HTMLImageElement;
  empty: HTMLElement;
  pane2d: HTMLElement;
  pane3d: HTMLElement;
  loading: HTMLElement;
  loadingText: HTMLElement;
  loadingBar: HTMLElement;
  loadingBarInner: HTMLElement;
  loadingRetry: HTMLButtonElement;
  zoomLabel: HTMLElement;
  sizeLabel: HTMLElement;
  frontBtn: HTMLButtonElement;
  backBtn: HTMLButtonElement;
  threeDBtn: HTMLButtonElement | null;
  foot2d: HTMLElement;
  foot3d: HTMLElement;
  zoom: number;
  tx: number;
  ty: number;
  dragging: boolean;
  dragStartX: number;
  dragStartY: number;
  startTx: number;
  startTy: number;
  imageRequestId: number;
  modelState: ModelState;
  three: ThreeState | null;
  keyHandler: (e: KeyboardEvent) => void;
}

let active: LightboxState | null = null;

function applyTransform(st: LightboxState): void {
  st.img.style.transform = `translate3d(${st.tx}px, ${st.ty}px, 0) scale(${st.zoom})`;
  st.zoomLabel.textContent = `${Math.round(st.zoom * 100)}%`;
}

function resetView(st: LightboxState): void {
  st.zoom = 1;
  st.tx = 0;
  st.ty = 0;
  applyTransform(st);
}

function showLoading(st: LightboxState, text: string, progress?: number): void {
  st.loading.style.display = "flex";
  st.loadingText.textContent = text;
  st.loadingRetry.style.display = "none";
  if (typeof progress === "number" && isFinite(progress)) {
    st.loadingBar.classList.remove("indeterminate");
    st.loadingBarInner.style.width = `${Math.max(0, Math.min(100, Math.round(progress * 100)))}%`;
  } else {
    st.loadingBar.classList.add("indeterminate");
    st.loadingBarInner.style.width = "";
  }
}

function hideLoading(st: LightboxState): void {
  st.loading.style.display = "none";
  st.loadingBar.classList.remove("indeterminate");
}

function showLoadError(st: LightboxState, text: string, retry: boolean): void {
  st.loading.style.display = "flex";
  st.loadingText.textContent = text;
  st.loadingBar.classList.remove("indeterminate");
  st.loadingBarInner.style.width = "0";
  st.loadingRetry.style.display = retry ? "" : "none";
}

function set2dView(st: LightboxState, view: DecorView): void {
  const thumb = decorPreviewUrl(st.guid, view);
  const detail = decorDetailUrl(st.guid, view);
  const url = detail || thumb;
  st.tab = view;
  st.frontBtn.classList.toggle("active", view === "front");
  st.backBtn.classList.toggle("active", view === "back");
  st.threeDBtn?.classList.remove("active");
  st.pane2d.classList.remove("hidden");
  st.pane3d.classList.add("hidden");
  st.foot2d.classList.remove("hidden");
  st.foot3d.classList.add("hidden");
  st.empty.style.display = "none";
  st.sizeLabel.textContent = "";
  st.img.dataset.fallbackUsed = "";
  st.img.dataset.fallbackSrc = detail && thumb && detail !== thumb ? thumb : "";
  const reqId = ++st.imageRequestId;

  if (!url) {
    st.img.removeAttribute("src");
    st.img.style.visibility = "hidden";
    st.empty.textContent = view === "back" ? "无背面图" : "无正面图";
    st.empty.style.display = "flex";
    hideLoading(st);
    return;
  }

  showLoading(st, "正在加载图片…");
  st.img.style.visibility = "";
  st.img.onload = () => {
    if (reqId !== st.imageRequestId || st.tab === "3d") return;
    st.empty.style.display = "none";
    st.sizeLabel.textContent =
      st.img.naturalWidth > 0 ? `${st.img.naturalWidth} × ${st.img.naturalHeight}` : "";
    hideLoading(st);
  };
  st.img.onerror = () => {
    if (reqId !== st.imageRequestId || st.tab === "3d") return;
    const fallback = st.img.dataset.fallbackSrc;
    if (fallback && st.img.dataset.fallbackUsed !== "1") {
      st.img.dataset.fallbackUsed = "1";
      st.img.src = fallback;
      return;
    }
    st.img.style.visibility = "hidden";
    st.empty.textContent = view === "back" ? "背面图加载失败" : "正面图加载失败";
    st.empty.style.display = "flex";
    hideLoading(st);
  };
  st.img.src = url;
  resetView(st);
}

function disposeThree(st: LightboxState): void {
  const t = st.three;
  if (!t) return;
  cancelAnimationFrame(t.raf);
  t.resizeObserver?.disconnect();
  t.controls.dispose();
  t.object.traverse((o) => {
    const mesh = o as THREE.Mesh;
    if (mesh.geometry) mesh.geometry.dispose();
    const mat = mesh.material as THREE.Material | THREE.Material[] | undefined;
    if (Array.isArray(mat)) mat.forEach((m) => m.dispose());
    else if (mat) mat.dispose();
  });
  if (t.grid) {
    t.grid.geometry.dispose();
    const gm = t.grid.material as THREE.Material | THREE.Material[];
    if (Array.isArray(gm)) gm.forEach((m) => m.dispose());
    else if (gm) gm.dispose();
  }
  t.renderer.dispose();
  try {
    t.renderer.forceContextLoss();
  } catch {
    /* ignore */
  }
  if (t.renderer.domElement.parentElement)
    t.renderer.domElement.parentElement.removeChild(t.renderer.domElement);
  st.three = null;
  st.modelState = "idle";
}

async function load3d(st: LightboxState): Promise<void> {
  if (st.modelState === "loading" || st.modelState === "ready") return;
  const info = decorModelInfo(st.guid);
  if (!info) {
    st.modelState = "error";
    showLoadError(st, "该装饰物没有导出 3D 模型。", false);
    return;
  }

  st.modelState = "loading";
  showLoading(st, "正在加载 3D 模型…");
  let manager: THREE.LoadingManager | null = null;
  try {
    manager = new THREE.LoadingManager();
    manager.onProgress = (_url, loaded, total) => {
      // OBJ 已就绪后贴图仍可能在后台加载；不要再把已隐藏的 loading 覆盖层弹回来。
      if (active !== st || st.tab !== "3d" || st.modelState !== "loading") return;
      showLoading(st, "正在加载 3D 模型…", total > 0 ? loaded / total : undefined);
    };
    const mtlLoader = new MTLLoader(manager);
    mtlLoader.setPath(info.resourceBase);
    const materials = await mtlLoader.loadAsync(info.mtlUrl);
    materials.preload();
    const obj = await new OBJLoader(manager).setMaterials(materials).loadAsync(
      info.resourceBase + encodeURIComponent(info.modelFileName)
    );

    if (active !== st) return;
    const pane = st.pane3d;
    const width = Math.max(1, pane.clientWidth || 800);
    const height = Math.max(1, pane.clientHeight || 520);
    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.setSize(width, height);
    renderer.domElement.className = "decor-preview-3d-canvas";
    pane.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x1d2026);
    scene.add(new THREE.AmbientLight(0xffffff, 1.8));
    const dir = new THREE.DirectionalLight(0xffffff, 2.4);
    dir.position.set(4, 7, 5);
    scene.add(dir);
    const fill = new THREE.DirectionalLight(0xffffff, 0.8);
    fill.position.set(-5, 3, -4);
    scene.add(fill);

    const box = new THREE.Box3().setFromObject(obj);
    const center = box.getCenter(new THREE.Vector3());
    obj.position.sub(center);
    scene.add(obj);

    const size = box.getSize(new THREE.Vector3());
    const maxDim = Math.max(size.x, size.y, size.z, 0.2);
    const grid = new THREE.GridHelper(maxDim * 4, 20, 0x3d6bf3, 0x333a4a);
    grid.position.y = box.min.y - center.y;
    scene.add(grid);

    const camera = new THREE.PerspectiveCamera(45, width / height, Math.max(maxDim / 1000, 0.001), maxDim * 100);
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.08;

    const fit = (): void => {
      const b = new THREE.Box3().setFromObject(obj);
      if (b.isEmpty()) return;
      const c = b.getCenter(new THREE.Vector3());
      const s = b.getSize(new THREE.Vector3());
      const m = Math.max(s.x, s.y, s.z, 0.2);
      controls.target.copy(c);
      camera.position.copy(c).add(new THREE.Vector3(m * 1.6, m * 1.2, m * 1.6));
      camera.near = Math.max(m / 1000, 0.001);
      camera.far = m * 100;
      camera.updateProjectionMatrix();
      controls.update();
    };

    const three: ThreeState = { renderer, scene, camera, controls, object: obj, grid, raf: 0, resizeObserver: null, fit };
    three.fit();

    const resize = (): void => {
      const w = Math.max(1, pane.clientWidth || 1);
      const h = Math.max(1, pane.clientHeight || 1);
      renderer.setSize(w, h);
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
    };
    if (typeof ResizeObserver !== "undefined") {
      three.resizeObserver = new ResizeObserver(resize);
      three.resizeObserver.observe(pane);
    }

    const tick = (): void => {
      three.raf = requestAnimationFrame(tick);
      if (active !== st || !st.three) return;
      if (st.tab !== "3d") return;
      controls.update();
      renderer.render(scene, camera);
    };
    three.raf = requestAnimationFrame(tick);

    st.three = three;
    st.modelState = "ready";
    if (st.tab === "3d") hideLoading(st);
  } catch (e) {
    if (active !== st) return;
    st.modelState = "error";
    if (st.tab === "3d")
      showLoadError(st, `3D 模型加载失败：${e instanceof Error ? e.message : String(e)}`, true);
  }
}

function switchTab(st: LightboxState, tab: DecorPreviewTab): void {
  if (tab === "back" && !decorPreviewUrl(st.guid, "back") && !decorDetailUrl(st.guid, "back")) return;
  if (tab === "3d" && !decorModelInfo(st.guid)) return;
  if (tab === "3d") {
    st.tab = "3d";
    st.frontBtn.classList.remove("active");
    st.backBtn.classList.remove("active");
    st.threeDBtn?.classList.add("active");
    st.pane2d.classList.add("hidden");
    st.pane3d.classList.remove("hidden");
    st.foot2d.classList.add("hidden");
    st.foot3d.classList.remove("hidden");
    if (st.modelState === "ready") {
      hideLoading(st);
      if (st.three) st.three.fit();
    } else if (st.modelState === "idle" || st.modelState === "error") {
      void load3d(st);
    } else {
      showLoading(st, "正在加载 3D 模型…");
    }
    return;
  }
  set2dView(st, tab);
}

function closeLightbox(): void {
  if (!active) return;
  disposeThree(active);
  window.removeEventListener("keydown", active.keyHandler, true);
  active.root.remove();
  active = null;
}

export function openDecorPreviewLightbox(
  guid: string,
  label: string,
  initialTab: DecorPreviewTab = "front"
): void {
  closeLightbox();

  const frontUrl = decorPreviewUrl(guid, "front") || decorDetailUrl(guid, "front");
  const backUrl = decorPreviewUrl(guid, "back") || decorDetailUrl(guid, "back");
  const hasModel = !!decorModelInfo(guid);
  if (!frontUrl && !backUrl && !hasModel) return;
  const initial: DecorPreviewTab =
    initialTab === "3d" && hasModel
      ? "3d"
      : initialTab === "back" && backUrl
        ? "back"
        : !frontUrl && backUrl
          ? "back"
          : "front";

  const root = document.createElement("div");
  root.className = "decor-lightbox";
  root.setAttribute("role", "dialog");
  root.setAttribute("aria-modal", "true");
  root.setAttribute("aria-label", `${label} 装饰预览`);
  root.innerHTML = `
    <div class="decor-lightbox-backdrop" data-decor-lb-close></div>
    <div class="decor-lightbox-card">
      <div class="decor-lightbox-head">
        <div class="decor-lightbox-title">${escHtml(label)}</div>
        <div class="decor-lightbox-tabs" role="tablist">
          <button type="button" class="decor-lightbox-tab" data-decor-lb-view="front">正面</button>
          <button type="button" class="decor-lightbox-tab" data-decor-lb-view="back"${backUrl ? "" : ' disabled title="无背面图"'}>背面</button>
          ${hasModel ? '<button type="button" class="decor-lightbox-tab" data-decor-lb-view="3d" title="按需加载 3D 模型">🧊 3D</button>' : ""}
        </div>
        <button type="button" class="decor-lightbox-close" data-decor-lb-close title="关闭（Esc）" aria-label="关闭">×</button>
      </div>
      <div class="decor-lightbox-stage">
        <div class="decor-lightbox-pane decor-lightbox-pane-2d">
          <img class="decor-lightbox-img" alt="${escHtml(label)}" draggable="false" />
          <div class="decor-lightbox-empty" style="display:none">无图</div>
        </div>
        <div class="decor-lightbox-pane decor-lightbox-pane-3d hidden"></div>
        <div class="decor-lightbox-loading">
          <span class="busy-spinner"></span>
          <div class="decor-lightbox-loading-text">正在加载…</div>
          <div class="decor-lightbox-progress"><div class="decor-lightbox-progress-inner"></div></div>
          <button type="button" class="decor-lightbox-retry" data-decor-lb-retry style="display:none">重试</button>
        </div>
      </div>
      <div class="decor-lightbox-foot decor-lightbox-foot-2d">
        <button type="button" class="decor-lightbox-zoom-btn" data-decor-lb-zoom-out title="缩小">−</button>
        <span class="decor-lightbox-zoom-label">100%</span>
        <button type="button" class="decor-lightbox-zoom-btn" data-decor-lb-zoom-in title="放大">＋</button>
        <button type="button" class="decor-lightbox-reset" data-decor-lb-reset>重置</button>
        <span class="decor-lightbox-size"></span>
        <span class="decor-lightbox-hint">滚轮缩放 · 拖拽平移 · 双击切换 1×/2.5× · Esc 关闭</span>
      </div>
      <div class="decor-lightbox-foot decor-lightbox-foot-3d hidden">
        <span class="decor-lightbox-hint">左键旋转 · 右键平移 · 滚轮缩放</span>
        <button type="button" class="decor-lightbox-reset" data-decor-lb-3d-reset>重置视角</button>
        <span class="decor-lightbox-hint">Esc 关闭</span>
      </div>
    </div>`;
  document.body.appendChild(root);

  const st: LightboxState = {
    root,
    guid,
    label,
    tab: initial,
    img: root.querySelector<HTMLImageElement>(".decor-lightbox-img")!,
    empty: root.querySelector<HTMLElement>(".decor-lightbox-empty")!,
    pane2d: root.querySelector<HTMLElement>(".decor-lightbox-pane-2d")!,
    pane3d: root.querySelector<HTMLElement>(".decor-lightbox-pane-3d")!,
    loading: root.querySelector<HTMLElement>(".decor-lightbox-loading")!,
    loadingText: root.querySelector<HTMLElement>(".decor-lightbox-loading-text")!,
    loadingBar: root.querySelector<HTMLElement>(".decor-lightbox-progress")!,
    loadingBarInner: root.querySelector<HTMLElement>(".decor-lightbox-progress-inner")!,
    loadingRetry: root.querySelector<HTMLButtonElement>("[data-decor-lb-retry]")!,
    zoomLabel: root.querySelector<HTMLElement>(".decor-lightbox-zoom-label")!,
    sizeLabel: root.querySelector<HTMLElement>(".decor-lightbox-size")!,
    frontBtn: root.querySelector<HTMLButtonElement>('[data-decor-lb-view="front"]')!,
    backBtn: root.querySelector<HTMLButtonElement>('[data-decor-lb-view="back"]')!,
    threeDBtn: root.querySelector<HTMLButtonElement>('[data-decor-lb-view="3d"]'),
    foot2d: root.querySelector<HTMLElement>(".decor-lightbox-foot-2d")!,
    foot3d: root.querySelector<HTMLElement>(".decor-lightbox-foot-3d")!,
    zoom: 1,
    tx: 0,
    ty: 0,
    dragging: false,
    dragStartX: 0,
    dragStartY: 0,
    startTx: 0,
    startTy: 0,
    imageRequestId: 0,
    modelState: "idle",
    three: null,
    keyHandler: () => undefined,
  };

  const zoomBy = (factor: number): void => {
    st.zoom = Math.max(0.5, Math.min(8, st.zoom * factor));
    applyTransform(st);
  };

  root.querySelectorAll("[data-decor-lb-close]").forEach((el) => el.addEventListener("click", closeLightbox));
  st.frontBtn.addEventListener("click", () => switchTab(st, "front"));
  st.backBtn.addEventListener("click", () => switchTab(st, "back"));
  st.threeDBtn?.addEventListener("click", () => switchTab(st, "3d"));
  st.loadingRetry.addEventListener("click", () => {
    st.modelState = "idle";
    void load3d(st);
  });
  root.querySelector("[data-decor-lb-zoom-in]")?.addEventListener("click", () => zoomBy(1.25));
  root.querySelector("[data-decor-lb-zoom-out]")?.addEventListener("click", () => zoomBy(1 / 1.25));
  root.querySelector("[data-decor-lb-reset]")?.addEventListener("click", () => resetView(st));
  root.querySelector("[data-decor-lb-3d-reset]")?.addEventListener("click", () => {
    if (st.three) st.three.fit();
  });

  st.pane2d.addEventListener(
    "wheel",
    (e) => {
      if (st.tab === "3d") return;
      e.preventDefault();
      const factor = Math.exp(-e.deltaY * 0.0015);
      zoomBy(factor);
    },
    { passive: false }
  );
  st.pane2d.addEventListener("pointerdown", (e) => {
    if (st.tab === "3d" || e.button !== 0) return;
    st.dragging = true;
    st.dragStartX = e.clientX;
    st.dragStartY = e.clientY;
    st.startTx = st.tx;
    st.startTy = st.ty;
    st.pane2d.setPointerCapture(e.pointerId);
    st.pane2d.classList.add("dragging");
  });
  st.pane2d.addEventListener("pointermove", (e) => {
    if (!st.dragging) return;
    st.tx = st.startTx + (e.clientX - st.dragStartX);
    st.ty = st.startTy + (e.clientY - st.dragStartY);
    applyTransform(st);
  });
  const endDrag = (e: PointerEvent): void => {
    if (!st.dragging) return;
    st.dragging = false;
    try {
      st.pane2d.releasePointerCapture(e.pointerId);
    } catch {
      /* ignore */
    }
    st.pane2d.classList.remove("dragging");
  };
  st.pane2d.addEventListener("pointerup", endDrag);
  st.pane2d.addEventListener("pointercancel", endDrag);
  st.pane2d.addEventListener("dblclick", () => {
    if (st.tab === "3d") return;
    st.zoom = st.zoom > 1.05 ? 1 : 2.5;
    st.tx = 0;
    st.ty = 0;
    applyTransform(st);
  });

  st.keyHandler = (e: KeyboardEvent): void => {
    if (e.key === "Escape") {
      e.preventDefault();
      closeLightbox();
      return;
    }
    if (e.key === "1" || e.key === "ArrowLeft") switchTab(st, "front");
    else if (e.key === "2" || e.key === "ArrowRight") switchTab(st, "back");
    else if ((e.key === "3" || e.key.toLowerCase() === "d") && hasModel) switchTab(st, "3d");
    else if (e.key === "+" || e.key === "=") zoomBy(1.25);
    else if (e.key === "-") zoomBy(1 / 1.25);
    else if (e.key === "0") {
      if (st.tab === "3d") st.three?.fit();
      else resetView(st);
    }
  };
  window.addEventListener("keydown", st.keyHandler, true);

  active = st;
  if (initial === "3d") switchTab(st, "3d");
  else set2dView(st, initial);
}
