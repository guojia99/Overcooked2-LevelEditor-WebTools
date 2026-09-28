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
 * Unity/编辑器欧拉角（度）→ three 旋转（四元数）。
 *
 * 推导（纯 yaw 时必须与 toSceneRotY 完全一致，这是回归锚点；任意三轴组合下
 * 前向/上向的镜像保真已数值验证）：
 *   Unity Euler(x,y,z) 的矩阵为 R_u = R_y(y)·R_x(x)·R_z(z)（Unity 语义 z→x→y 依次
 *   应用；其左手系矩阵与右手系标准形式数值相同）。镜像共轭 R↦M·R·M（M=diag(1,1,-1)）
 *   对各轴给出 R_y(-y)、R_x(-x)、R_z(+z)。
 *
 *   纯旋转无法精确表达镜像（行列式 -1）。本约定取「前向(+Z)与上向(+Y) 经 M 精确
 *   映射、左右(+X) 手性翻转（网格顶点原样导入的既定妥协）」：
 *
 *     R_t = M·R_u·diag(-1,1,1) = R_y(-y)·R_x(-x)·R_z(z)·R_y(π)
 *
 *   x=z=0 时退化为 R_y(π−y) = toSceneRotY ✓。倾斜（俯仰/滚转）方向与 Unity 严格
 *   一致：坡道/立式 quad 的抬升方向在 3D 视口所见即游戏所得。
 *
 * 用四元数组合实现（three 的 Euler 'YXZ' 无法表达末尾的局部 R_y(π)）。
 */
export interface QuaternionLike {
  x: number;
  y: number;
  z: number;
  w: number;
}

/** 绕轴 (ax,ay,az)（单位向量）旋转 rad 弧度的四元数。 */
function axisAngleQuat(ax: number, ay: number, az: number, rad: number): QuaternionLike {
  const h = rad / 2;
  const s = Math.sin(h);
  return { x: ax * s, y: ay * s, z: az * s, w: Math.cos(h) };
}

/** 四元数 Hamilton 积 a ⊗ b（b 先作用于局部，与矩阵乘法 A·B 同效）。 */
function mulQuat(a: QuaternionLike, b: QuaternionLike): QuaternionLike {
  return {
    x: a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
    y: a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
    z: a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
    w: a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z,
  };
}

export function toSceneQuaternion(rx: number, ry: number, rz: number): QuaternionLike {
  const DEG2RAD = Math.PI / 180;
  const qy = axisAngleQuat(0, 1, 0, -ry * DEG2RAD);
  const qx = axisAngleQuat(1, 0, 0, -rx * DEG2RAD);
  const qz = axisAngleQuat(0, 0, 1, rz * DEG2RAD);
  const qpi = axisAngleQuat(0, 1, 0, Math.PI);
  // q = qy ⊗ qx ⊗ qz ⊗ qπ
  return mulQuat(mulQuat(mulQuat(qy, qx), qz), qpi);
}

/** 把 toSceneQuaternion 的结果一次性写入 Object3D.quaternion（结构化类型，避免引入 three 依赖）。 */
export function applySceneEuler(
  target: { quaternion: { set: (x: number, y: number, z: number, w: number) => unknown } },
  rx: number,
  ry: number,
  rz: number
): void {
  const q = toSceneQuaternion(rx, ry, rz);
  target.quaternion.set(q.x, q.y, q.z, q.w);
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
