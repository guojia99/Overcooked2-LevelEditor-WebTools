/**
 * 编辑器世界坐标 ↔ three 场景坐标的手性换算（唯一真源）。
 *
 * 为什么必须做镜像：
 *   Unity 是左手系，从上方俯视时 +X 朝右、+Z 朝上；2D 画布忠实还原了这一点
 *   （worldToCanvas: screen_x = +wx, screen_y = -wz）。
 *   three 是右手系，同样俯视、同样让 +Z 朝上时，+X 必然落在【左边】。
 *   若把 (x, y, z) 原样塞进 three，整个关卡相对 2D 就是左右镜像的。
 *
 * 所以统一约定：three_z = -editor_z（标准手性转换），相机默认方位角取 0。
 * 镜像会翻转旋向，故朝向不是简单取负，而是 rotation.y = π - θ：
 *   物件前向 forward = (sinθ, 0, cosθ)（Unity 语义）
 *   局部 +Z 经 R_y(π-θ) 后 = (sinθ, 0, -cosθ) = 镜像空间里的 forward ✓
 *   （θ=0/90/180/270 四个刻度均已数值验证，见 orientation 测试）
 *
 * 纪律：任何把世界坐标写进 Object3D.position 的地方都必须过 toScene*，
 * 任何把 three 坐标交回编辑器状态的地方都必须过 toWorldZ。
 */

/** 编辑器世界 Z → three 场景 Z。 */
export function toSceneZ(z: number): number {
  return -z;
}

/** three 场景 Z → 编辑器世界 Z（射线交点回写状态时用）。 */
export function toWorldZ(z: number): number {
  return -z;
}

/** 编辑器世界 XZ → three 场景 XZ。 */
export function toScene(x: number, z: number): { x: number; z: number } {
  return { x, z: -z };
}

/** three 场景 XZ → 编辑器世界 XZ。 */
export function toWorld(x: number, z: number): { x: number; z: number } {
  return { x, z: -z };
}

/** Unity/编辑器 yaw（度）→ three rotation.y（弧度）。 */
export function toSceneRotY(deg: number): number {
  return Math.PI - (deg * Math.PI) / 180;
}

/**
 * 编辑器世界方向向量 → three 场景方向向量（只镜像 Z，不含平移）。
 * 用于箭头、连线方向这类矢量。
 */
export function toSceneDir(dx: number, dz: number): { x: number; z: number } {
  return { x: dx, z: -dz };
}

/** 物件前向（编辑器世界系），Unity 语义 forward = (sinθ, 0, cosθ)。 */
export function forwardOf(rotDeg: number): { x: number; z: number } {
  const r = (rotDeg * Math.PI) / 180;
  return { x: Math.sin(r), z: Math.cos(r) };
}
