/**
 * 3D 拾取：射线 → 场景对象 → EditorItem / EditorFloor / 路点 / 操作柄。
 *
 * 排序对等：2D 的 hitTestAll 用 itemDrawCompare 反序（画得最上面的优先命中）。
 * 3D 天然按射线距离排序（离相机最近的优先），二者在俯视角下等价；同距并列时
 * 再用 itemDrawCompare 兜底，保证「2D 里先选中谁，3D 里也先选中谁」。
 */

import * as THREE from "three";
import { S, EditorItem, EditorFloor, CELL } from "../state";
import { Scene3DCtx, Pickable, itemByKey, floorByKey } from "./ctx";
import { OrbitCam } from "./camera3d";
import { itemBoxOf } from "./meshItems";
import { toWorld, toSceneZ } from "./space";

export interface PickResult {
  pick: Pickable;
  point: THREE.Vector3;
  distance: number;
}

const raycaster = new THREE.Raycaster();
const ndc = new THREE.Vector2();

/** 画布像素坐标 → NDC。 */
function toNdc(canvas: HTMLCanvasElement, px: number, py: number): THREE.Vector2 {
  const rect = canvas.getBoundingClientRect();
  ndc.set(
    ((px - rect.left) / Math.max(1, rect.width)) * 2 - 1,
    -(((py - rect.top) / Math.max(1, rect.height)) * 2 - 1)
  );
  return ndc;
}

export function setupRay(ctx: Scene3DCtx, clientX: number, clientY: number): THREE.Raycaster {
  // 保险：raycast 依赖 matrixWorld，而它平时由 renderer.render() 更新。
  // 若在「同步完场景但还没渲染」的间隙收到指针事件，矩阵会是陈旧的，
  // 命中结果就会落在错误的物件上（无头测试里已复现）。
  ctx.scene.updateMatrixWorld();
  raycaster.setFromCamera(toNdc(ctx.canvas, clientX, clientY), ctx.cam.camera);
  return raycaster;
}

/** 全部命中（近 → 远，已按可拾取性过滤、按对象去重）。 */
export function pickAll(ctx: Scene3DCtx, clientX: number, clientY: number): PickResult[] {
  const ray = setupRay(ctx, clientX, clientY);
  const hits = ray.intersectObjects(ctx.scene.children, true);
  const seen = new Set<string>();
  const out: PickResult[] = [];
  for (const h of hits) {
    let node: THREE.Object3D | null = h.object;
    let pick: Pickable | undefined;
    let blocked = false;
    while (node) {
      if (node.userData.noPick) blocked = true;
      if (!pick && node.userData.pick) pick = node.userData.pick as Pickable;
      node = node.parent;
    }
    if (blocked || !pick) continue;
    const id = pick.kind + ":" + pick.key;
    if (seen.has(id)) continue;
    seen.add(id);
    out.push({ pick, point: h.point.clone(), distance: h.distance });
  }
  return out;
}

/** 最近一次命中中的操作柄（柄永远优先于其它对象）。 */
export function pickHandle(ctx: Scene3DCtx, clientX: number, clientY: number): PickResult | null {
  const all = pickAll(ctx, clientX, clientY);
  for (const r of all) {
    if (r.pick.kind === "handle") return r;
  }
  return null;
}

/**
 * 命中的物件，按【射线距离】近→远，但空气墙/碰撞体降级到最后。
 *
 * 不套用 2D 的 itemDrawCompare 排序：2D 是平面投影，重叠时只能靠画家序猜用户
 * 想选谁；3D 有真实深度，点到哪个像素就是哪个物体。
 *
 * 空气墙要特殊处理：它在 2D 只是个占地方框，点它必须点进它的占格里；但在 3D
 * 它是一面几米高的竖直体块，会挡在相机与柜台之间——俯视时你以为点的是柜台，
 * 射线却先穿过了那面墙，于是「点了这里，选中的却是别处那面墙」。所以把这类
 * 半透明体块降级为兜底：只有在没点到任何实体时才轮到它。
 */
export function pickItems(ctx: Scene3DCtx, clientX: number, clientY: number): EditorItem[] {
  const all = pickAll(ctx, clientX, clientY).filter((r) => r.pick.kind === "item");
  const solid: EditorItem[] = [];
  const ghost: EditorItem[] = [];
  for (const r of all) {
    const it = itemByKey(r.pick.key);
    if (!it) continue;
    const kind = itemBoxOf(it).kind;
    if (kind === "airwall" || kind === "collision") ghost.push(it);
    else solid.push(it);
  }
  return solid.concat(ghost);
}

export function pickFloors(ctx: Scene3DCtx, clientX: number, clientY: number): EditorFloor[] {
  const all = pickAll(ctx, clientX, clientY).filter((r) => r.pick.kind === "floor");
  const out: EditorFloor[] = [];
  for (const r of all) {
    const f = floorByKey(r.pick.key);
    if (f) out.push(f);
  }
  return out;
}

export function pickWaypoints(ctx: Scene3DCtx, clientX: number, clientY: number): string[] {
  return pickAll(ctx, clientX, clientY)
    .filter((r) => r.pick.kind === "waypoint")
    .map((r) => r.pick.key);
}

/**
 * 射线与水平面 y=planeY 的交点，返回【编辑器世界坐标】（已反镜像 Z）。
 * 拖动、放置、hover 坐标读出都用它。
 * 与视线近乎平行时返回 null，避免交点飞到无穷远。
 */
export function rayOnPlane(
  ctx: Scene3DCtx,
  clientX: number,
  clientY: number,
  planeY: number
): { x: number; z: number } | null {
  const ray = setupRay(ctx, clientX, clientY);
  const dir = ray.ray.direction;
  if (Math.abs(dir.y) < 1e-4) return null;
  const t = (planeY - ray.ray.origin.y) / dir.y;
  if (t <= 0) return null;
  const p = ray.ray.origin.clone().addScaledVector(dir, t);
  return toWorld(p.x, p.z);
}

/**
 * 射线与「过某点、面向相机的竖直平面」的交点 Y —— Y 轴拖动用。
 * 平面法线取相机前向的水平分量，保证拖动手感与视角无关。
 * 入参 at 是编辑器世界坐标。
 */
export function rayVerticalY(
  ctx: Scene3DCtx,
  clientX: number,
  clientY: number,
  at: { x: number; z: number }
): number | null {
  const ray = setupRay(ctx, clientX, clientY);
  const n = new THREE.Vector3();
  ctx.cam.camera.getWorldDirection(n);
  n.y = 0;
  if (n.lengthSq() < 1e-6) return null;
  n.normalize();
  const planePoint = new THREE.Vector3(at.x, 0, toSceneZ(at.z));
  const denom = n.dot(ray.ray.direction);
  if (Math.abs(denom) < 1e-6) return null;
  const t = n.dot(planePoint.clone().sub(ray.ray.origin)) / denom;
  if (t <= 0) return null;
  return ray.ray.origin.clone().addScaledVector(ray.ray.direction, t).y;
}

/** 编辑器世界坐标 → 屏幕像素（框选投影判定用）。 */
export function worldToScreen(
  cam: OrbitCam,
  canvas: HTMLCanvasElement,
  x: number,
  y: number,
  z: number
): { x: number; y: number } {
  const v = new THREE.Vector3(x, y, toSceneZ(z)).project(cam.camera);
  const rect = canvas.getBoundingClientRect();
  return {
    x: ((v.x + 1) / 2) * rect.width,
    y: ((1 - v.y) / 2) * rect.height,
  };
}

/** 用于「点放地板 / 拖放新建」的落点高度：优先命中地板顶面，否则当前高度层。 */
export function dropPlaneY(): number {
  if (S.floorHeight.min != null) return S.floorHeight.min;
  return 0;
}

export const PICK_NEAR_TOL = CELL * 0.5;
