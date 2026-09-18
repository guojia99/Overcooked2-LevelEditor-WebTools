/**
 * 2D 主包 ↔ 3D 视口的解耦桥。
 *
 * 为什么需要它：three.js 约 600KB，必须通过动态 import 隔离在独立 chunk 里
 * （沿用 recipeModelPreview.ts 的分割策略）。但 render.ts / input.ts 这类主包
 * 模块需要在状态变更时通知 3D 重建——它们不能静态 import scene3d，否则 three
 * 会被拖进首屏 bundle。本模块零依赖，只持有一个由 scene3d 注册的回调句柄。
 */

export interface Scene3DHandle {
  /** 标脏：下一帧重新同步场景图（内部做增量 diff，不是全量重建）。 */
  invalidate(): void;
  /** 卸载并释放 GPU 资源。 */
  dispose(): void;
  /** 相机对准某个世界坐标（切换视图时保持视野中心）。 */
  lookAtWorld(wx: number, wz: number): void;
  /** 当前相机注视点的世界坐标（切回 2D 时用来同步 panX/panY）。 */
  focusWorld(): { x: number; z: number };
  /** 视口尺寸变化。 */
  resize(): void;
}

let handle: Scene3DHandle | null = null;

export function setScene3DHandle(h: Scene3DHandle | null): void {
  handle = h;
}

export function scene3dHandle(): Scene3DHandle | null {
  return handle;
}

/** 3D 已挂载时标脏。draw() 在 3D 模式下调用它替代 2D 重绘。 */
export function scene3dInvalidate(): void {
  if (handle) handle.invalidate();
}

export function scene3dResize(): void {
  if (handle) handle.resize();
}
