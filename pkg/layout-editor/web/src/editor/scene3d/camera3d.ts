/**
 * 3D 轨道相机 + 与 2D 画布视野的双向换算。
 *
 * 2D 用 (panX, panY, scale) 描述视野；3D 用 (target, distance, yaw, pitch)。
 * 切换视图时通过 worldFocus 互转，保证「切过去还是同一个地方」。
 */

import * as THREE from "three";
import { S, PX_PER_UNIT } from "../state";
import { CAMERA_NEAR, CAMERA_FAR } from "./constants";
import { toSceneZ, toWorldZ } from "./space";

/** 2D 的 1 像素 ≈ 多少世界单位（scale=1 时 48px = 1m）。 */
function worldPerPixel(): number {
  return 1 / (PX_PER_UNIT * Math.max(0.0001, S.scale));
}

export class OrbitCam {
  readonly camera: THREE.PerspectiveCamera;
  /** 注视点（three 场景坐标，Z 已镜像；Y 通常贴地）。 */
  readonly target = new THREE.Vector3(0, 0, 0);
  /** 相机到注视点的距离。 */
  distance = 18;
  /**
   * 方位角（弧度，绕 Y）。默认 0 = 相机在 +Z 侧俯视。
   * 配合 space.ts 的 Z 镜像，这个方位下 +X 朝右、编辑器 +Z 朝上，
   * 与 2D 画布的 worldToCanvas 完全一致（切换视图不会左右翻转）。
   */
  yaw = 0;
  /** 俯仰角（弧度，0 = 水平，PI/2 = 正俯视）。 */
  pitch = 0.92;

  private minPitch = 0.05;
  private maxPitch = Math.PI / 2 - 0.001;
  private minDistance = 1.5;
  private maxDistance = 220;

  constructor(aspect: number) {
    this.camera = new THREE.PerspectiveCamera(50, aspect, CAMERA_NEAR, CAMERA_FAR);
    this.apply();
  }

  setAspect(aspect: number): void {
    this.camera.aspect = aspect;
    this.camera.updateProjectionMatrix();
  }

  /** 把球坐标写进相机 transform。 */
  apply(): void {
    this.pitch = Math.min(this.maxPitch, Math.max(this.minPitch, this.pitch));
    this.distance = Math.min(this.maxDistance, Math.max(this.minDistance, this.distance));
    const cp = Math.cos(this.pitch);
    const sp = Math.sin(this.pitch);
    this.camera.position.set(
      this.target.x + this.distance * cp * Math.sin(this.yaw),
      this.target.y + this.distance * sp,
      this.target.z + this.distance * cp * Math.cos(this.yaw)
    );
    this.camera.up.set(0, 1, 0);
    this.camera.lookAt(this.target);
    this.camera.updateMatrixWorld();
  }

  orbit(dx: number, dy: number): void {
    this.yaw -= dx * 0.008;
    this.pitch += dy * 0.008;
    this.apply();
  }

  /** 屏幕拖动 → 沿相机右向量 / 前向水平分量平移注视点。
   *  语义是「抓住世界拖」：向右拖，内容跟着向右走（注视点反向移动）。 */
  pan(dx: number, dy: number, viewportHeight: number): void {
    // 把屏幕位移换算成注视点平面上的世界位移（透视投影下与距离成正比）。
    const vFov = (this.camera.fov * Math.PI) / 180;
    const worldPerPx = (2 * Math.tan(vFov / 2) * this.distance) / Math.max(1, viewportHeight);
    const right = new THREE.Vector3();
    right.setFromMatrixColumn(this.camera.matrixWorld, 0);
    right.y = 0;
    right.normalize();
    const fwd = new THREE.Vector3();
    fwd.setFromMatrixColumn(this.camera.matrixWorld, 2);
    fwd.y = 0;
    fwd.normalize();
    this.target.addScaledVector(right, -dx * worldPerPx);
    this.target.addScaledVector(fwd, -dy * worldPerPx);
    this.apply();
  }

  /**
   * 方向按钮用的平移：语义是「移动视角本身」——按 ▲ 视角上移（内容相对下移），
   * 与拖拽的「抓住世界拖」正好相反。两者共用同一套换算，只差一个符号。
   */
  panView(dx: number, dy: number, viewportHeight: number): void {
    this.pan(-dx, -dy, viewportHeight);
  }

  zoom(deltaY: number): void {
    this.distance *= deltaY > 0 ? 1.1 : 0.9;
    this.apply();
  }

  /**
   * 以某个世界锚点为中心缩放（滚轮对准鼠标所指位置推进／拉远）。
   *
   * 做法：先按比例改视距，再把注视点朝锚点做同比例收缩 ——
   *   target' = pivot + (target - pivot) * k
   * 这样锚点在屏幕上的位置基本不动，手感就是「往鼠标指的地方钻进去」。
   * pivot 传 null 时退化为绕注视点缩放（与旧行为一致）。
   */
  zoomAt(deltaY: number, pivot: THREE.Vector3 | null): void {
    const before = this.distance;
    const raw = before * (deltaY > 0 ? 1.1 : 0.9);
    const next = Math.min(this.maxDistance, Math.max(this.minDistance, raw));
    // 已经顶到远近限位时不再平移注视点，否则会「钻不动却一直漂」。
    if (Math.abs(next - before) < 1e-6) return;
    this.distance = next;
    if (pivot) {
      const k = next / before;
      this.target.set(
        pivot.x + (this.target.x - pivot.x) * k,
        pivot.y + (this.target.y - pivot.y) * k,
        pivot.z + (this.target.z - pivot.z) * k
      );
    }
    this.apply();
  }

  /** 视图切换：把 2D 的 pan/scale 视野搬进 3D。 */
  adoptFrom2D(viewportW: number, viewportH: number): void {
    const wpp = worldPerPixel();
    // 2D 画布中心对应的编辑器世界坐标（worldToCanvas 的逆运算取中心点）：
    //   cx = W/2 + panX + wx*K = W/2  ⇒ wx = -panX/K
    //   cy = H/2 + panY - wz*K = H/2  ⇒ wz = +panY/K
    // 再按 space.ts 的约定镜像 Z 写进 three。
    this.target.set(-S.panX * wpp, 0, toSceneZ(S.panY * wpp));
    // 让 3D 的可视高度与 2D 当前缩放下的可视高度大致相等。
    const visibleWorldH = viewportH * wpp;
    const vFov = (this.camera.fov * Math.PI) / 180;
    this.distance = visibleWorldH / (2 * Math.tan(vFov / 2));
    this.setAspect(viewportW / Math.max(1, viewportH));
    this.apply();
  }

  /** 视图切换回 2D：把注视点与视距还原成 pan/scale。 */
  writeBackTo2D(viewportH: number): void {
    const vFov = (this.camera.fov * Math.PI) / 180;
    const visibleWorldH = 2 * Math.tan(vFov / 2) * this.distance;
    const scale = viewportH / Math.max(0.0001, visibleWorldH) / PX_PER_UNIT;
    S.scale = Math.min(4, Math.max(0.25, scale));
    const wpp = 1 / (PX_PER_UNIT * S.scale);
    S.panX = -this.target.x / wpp;
    S.panY = toWorldZ(this.target.z) / wpp;
  }

  /** 框住一组编辑器世界坐标点。 */
  fit(points: { x: number; z: number }[], viewportH: number): void {
    if (!points.length) return;
    let minX = Infinity;
    let maxX = -Infinity;
    let minZ = Infinity;
    let maxZ = -Infinity;
    for (const p of points) {
      if (p.x < minX) minX = p.x;
      if (p.x > maxX) maxX = p.x;
      if (p.z < minZ) minZ = p.z;
      if (p.z > maxZ) maxZ = p.z;
    }
    this.target.set((minX + maxX) / 2, 0, toSceneZ((minZ + maxZ) / 2));
    const span = Math.max(maxX - minX, maxZ - minZ, 4);
    const vFov = (this.camera.fov * Math.PI) / 180;
    this.distance = (span * 0.75) / Math.tan(vFov / 2);
    void viewportH;
    this.apply();
  }
}
