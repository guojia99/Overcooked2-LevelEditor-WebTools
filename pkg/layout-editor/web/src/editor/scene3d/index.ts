/**
 * 3D 视口入口：挂载 / 卸载 / 渲染循环 / 与 2D 的视野互转。
 *
 * 本模块（连同整个 scene3d/ 目录与 three.js）只通过动态 import 加载，
 * 保证首屏 bundle 不被 600KB 的 three 拖累——与 recipeModelPreview.ts 的策略一致。
 *
 * 渲染节奏：draw() 只标脏（dirty=true），真正的场景同步与绘制在 rAF 里合并执行。
 * 拖动时 draw() 每帧被调用多次，若每次都同步重建场景图会直接掉帧。
 */

import * as THREE from "three";
import { dom } from "../dom";
import { setScene3DHandle, Scene3DHandle } from "../scene3dBridge";
import { CAMERA_FAR } from "./constants";
import { OrbitCam } from "./camera3d";
import { Scene3DCtx } from "./ctx";
import { syncScene, clearScene, scenePoints } from "./sceneBuild";
import { attachInput3D, detachInput3D } from "./input3d";
import { ensureMarqueeEl, disposeMarquee } from "./marquee3d";
import { mountHud3D, unmountHud3D } from "./hud3d";
import { disposeSharedTextures } from "./materials";
import { resetOverlayCache } from "./overlays3d";
import { toSceneZ, toWorldZ } from "./space";

let ctx: Scene3DCtx | null = null;
let raf = 0;
let dirty = true;
let resizeObserver: ResizeObserver | null = null;
/** 上次同步给 renderer 的 CSS 像素尺寸。不能拿 canvas.width 比较——
 *  setPixelRatio 会把它乘上 dpr，在高分屏上会导致每帧都重设尺寸。 */
let lastW = 0;
let lastH = 0;

function buildScene(canvas: HTMLCanvasElement): Scene3DCtx {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
  renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
  renderer.setClearColor(0x14171c, 1);
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.sortObjects = true;

  const scene = new THREE.Scene();
  scene.fog = new THREE.Fog(0x14171c, CAMERA_FAR * 0.55, CAMERA_FAR);

  // 柔和三点光：盒体需要明暗面才能读出体积，但不能有硬阴影干扰读图。
  scene.add(new THREE.HemisphereLight(0xffffff, 0x2a2f36, 1.05));
  const key = new THREE.DirectionalLight(0xffffff, 0.95);
  key.position.set(6, 14, 8);
  scene.add(key);
  const fill = new THREE.DirectionalLight(0xbcd4ff, 0.35);
  fill.position.set(-8, 6, -6);
  scene.add(fill);

  const width = Math.max(1, canvas.clientWidth);
  const height = Math.max(1, canvas.clientHeight);
  const cam = new OrbitCam(width / height);

  const overlayRoot = new THREE.Group();
  overlayRoot.name = "overlays";
  scene.add(overlayRoot);
  const animRoot = new THREE.Group();
  animRoot.name = "anim";
  scene.add(animRoot);
  const handleRoot = new THREE.Group();
  handleRoot.name = "handles";
  scene.add(handleRoot);

  return {
    scene,
    cam,
    renderer,
    canvas,
    items: new Map(),
    floors: new Map(),
    overlayRoot,
    animRoot,
    handleRoot,
    invalidate: () => {
      dirty = true;
    },
  };
}

function resize(): void {
  if (!ctx) return;
  const w = Math.max(1, ctx.canvas.clientWidth);
  const h = Math.max(1, ctx.canvas.clientHeight);
  if (w === lastW && h === lastH) return;
  lastW = w;
  lastH = h;
  ctx.renderer.setSize(w, h, false);
  ctx.cam.setAspect(w / h);
  dirty = true;
}

function loop(): void {
  raf = requestAnimationFrame(loop);
  if (!ctx) return;
  resize();
  if (!dirty) return;
  dirty = false;
  syncScene(ctx);
  ctx.renderer.render(ctx.scene, ctx.cam.camera);
}

/** 挂载 3D 视口（由 init.setViewMode 动态调用）。 */
export function mountScene3D(): Scene3DHandle {
  if (ctx) {
    dirty = true;
    return handle();
  }
  const canvas = dom.canvas3d;
  ctx = buildScene(canvas);

  // 视野承接：把 2D 的 pan/scale 搬成轨道相机参数，切过去还是同一个地方。
  ctx.cam.adoptFrom2D(Math.max(1, canvas.clientWidth), Math.max(1, canvas.clientHeight));

  const host = canvas.parentElement;
  if (host) ensureMarqueeEl(host as HTMLElement);
  if (host) mountHud3D(host as HTMLElement, ctx);

  attachInput3D(ctx);

  if (typeof ResizeObserver !== "undefined" && host) {
    resizeObserver = new ResizeObserver(() => {
      dirty = true;
    });
    resizeObserver.observe(host);
  }

  dirty = true;
  if (!raf) raf = requestAnimationFrame(loop);

  const h = handle();
  setScene3DHandle(h);
  return h;
}

export function unmountScene3D(): void {
  if (!ctx) return;
  // 切回 2D 前把相机视野写回 pan/scale，保证来回切换视角连续。
  ctx.cam.writeBackTo2D(Math.max(1, ctx.canvas.clientHeight));

  detachInput3D();
  if (resizeObserver) resizeObserver.disconnect();
  resizeObserver = null;
  if (raf) cancelAnimationFrame(raf);
  raf = 0;

  clearScene(ctx);
  resetOverlayCache();
  disposeMarquee();
  unmountHud3D();
  disposeSharedTextures();
  ctx.renderer.dispose();
  ctx = null;
  lastW = 0;
  lastH = 0;
  setScene3DHandle(null);
}

function handle(): Scene3DHandle {
  return {
    invalidate: () => {
      dirty = true;
    },
    dispose: unmountScene3D,
    lookAtWorld: (wx: number, wz: number) => {
      if (!ctx) return;
      ctx.cam.target.set(wx, ctx.cam.target.y, toSceneZ(wz));
      ctx.cam.apply();
      dirty = true;
    },
    focusWorld: () => (ctx ? { x: ctx.cam.target.x, z: toWorldZ(ctx.cam.target.z) } : { x: 0, z: 0 }),
    resize: () => {
      dirty = true;
    },
  };
}

/** 框住整关（工具栏「居中」等入口可调用）。 */
export function fitScene3D(): void {
  if (!ctx) return;
  ctx.cam.fit(scenePoints(), Math.max(1, ctx.canvas.clientHeight));
  dirty = true;
}
