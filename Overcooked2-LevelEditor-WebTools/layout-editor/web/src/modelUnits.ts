/** 自定义菜谱 3D 模型的单位体系（实测确定，勿随意改动）：
 *
 *  1 模型单位（Blender OBJ/FBX） = 1 Unity 单位 = 1 m = 100 cm
 *  （Unity ModelImporter useFileScale=1 导入为 1:1；官方 CustomRecipeSO 以 scale=1
 *   直引 Blender 导出的模型文件，已在 dump_bundle 网格 AABB 实测确认）
 *
 *  游戏真实盘子 m_sk_plate_02：直径 1.0 单位 = 100.0000 cm，高 0.163 单位 = 16.3000 cm。
 *  官方参考菜（胡闹厨房素材图/model/虾球和炒饭更新步骤/）：
 *    蛋炒饭.fbx  足迹 ≈ 0.82~0.87 → 82~87 cm（官方 Fried_Rice 直引）
 *    芝士虾球.fbx 足迹 ≈ 0.71~0.77 → 71~77 cm（官方 Cheese_Prawn 直引）
 *  存储格式保持 Unity 单位（scale 相对倍数 + 位置 Unity 单位），cm 仅为显示/输入层。 */

export const CM_PER_UNIT = 100;

/** Unity 单位 → cm。 */
export function u2cm(u: number): number {
  return u * CM_PER_UNIT;
}

/** cm → Unity 单位。 */
export function cm2u(cm: number): number {
  return cm / CM_PER_UNIT;
}

/** cm 数值格式化：默认 4 位小数。 */
export function fmtCm(v: number): string {
  return v.toFixed(4);
}

/** 自动适配目标足迹（cm）：盘子 85（对齐官方蛋炒饭 82~87 cm，游戏盘子 100 cm 留边）；
 *  杯子 37（玻璃杯口径 69 cm，食物足迹 37 cm 放入杯中）。 */
export const PLATE_FIT_CM = 85;
export const CUP_FIT_CM = 37;

/** 原始尺寸（Unity 单位）的水平足迹 = max(X/Z)。后端注释确认旋转只交换 X/Z、max 不变，
 *  因此足迹与旋转无关。 */
export function footprintOf(raw: { x: number; y: number; z: number }): number {
  return Math.max(Math.abs(raw.x), Math.abs(raw.z)) || 1;
}

/** 相对缩放 → 足迹尺寸（cm）。 */
export function footprintCm(raw: { x: number; y: number; z: number }, scale: number): number {
  return u2cm(footprintOf(raw) * Math.max(0.0001, scale));
}

const DEG2RAD = Math.PI / 180;

/** 把「已含配置旋转的世界包围盒」反向旋转回模型内容坐标系（8 角点按逆旋转后重新取 AABB），
 *  得到模型内容的原始尺寸（Unity 单位）。配置旋转为 90° 倍数时精确还原；
 *  用于从摘要的世界包围盒（boundsSize）计算 unitK 显示归一化。
 *  注意：three/Unity 的欧拉 XYZ 正变换为 v' = Rx·Ry·Rz·v（先绕 Z），
 *  逆运算 = 先 -rotX、再 -rotY、再 -rotZ。 */
export function unrotateSize(
  world: { x: number; y: number; z: number },
  rotX: number,
  rotY: number,
  rotZ: number
): { x: number; y: number; z: number } {
  const hx = Math.abs(world.x) / 2;
  const hy = Math.abs(world.y) / 2;
  const hz = Math.abs(world.z) / 2;
  const rx = ((-rotX) * DEG2RAD) % (2 * Math.PI);
  const ry = ((-rotY) * DEG2RAD) % (2 * Math.PI);
  const rz = ((-rotZ) * DEG2RAD) % (2 * Math.PI);
  const cx = Math.cos(rx);
  const sx = Math.sin(rx);
  const cy = Math.cos(ry);
  const sy = Math.sin(ry);
  const cz = Math.cos(rz);
  const sz = Math.sin(rz);
  let maxX = 0;
  let maxY = 0;
  let maxZ = 0;
  for (const a of [-1, 1]) {
    for (const b of [-1, 1]) {
      for (const c of [-1, 1]) {
        // 反向：先绕 X 逆旋，再绕 Y，再绕 Z
        let x = a * hx;
        let y = b * hy;
        let z = c * hz;
        // 绕 X
        const y1 = y * cx - z * sx;
        const z1 = y * sx + z * cx;
        // 绕 Y
        const x2 = x * cy + z1 * sy;
        const z2 = -x * sy + z1 * cy;
        // 绕 Z
        const x3 = x2 * cz - y1 * sz;
        const y3 = x2 * sz + y1 * cz;
        maxX = Math.max(maxX, Math.abs(x3));
        maxY = Math.max(maxY, Math.abs(y3));
        maxZ = Math.max(maxZ, Math.abs(z2));
      }
    }
  }
  return { x: maxX * 2, y: maxY * 2, z: maxZ * 2 };
}

/** 目标足迹（cm）→ 相对缩放。 */
export function scaleForFootprintCm(raw: { x: number; y: number; z: number }, cm: number): number {
  return Math.max(1e-6, cm2u(Math.max(1e-4, cm)) / footprintOf(raw));
}

/** 4 位小数（非 cm 数值，如相对缩放/旋转）。 */
export function fmt4(v: number): string {
  return Math.round(v * 10000) / 10000 + "";
}
