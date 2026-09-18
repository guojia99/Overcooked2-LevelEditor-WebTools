/**
 * 3D 视口全局渲染约定（方案 §3 R1–R8）。
 *
 * 近零高度几何是本视口最大的技术难点：地板在数据里几乎没有厚度，且「地面」
 * 这一层的 localPosition.y 有三个互不相同的历史值（实心 -0.05 / 主题 +0.01 /
 * 空气 0），行走面却都是 0。2D 画布是 Y 盲的、靠画家算法排序所以无所谓；3D 一
 * 开深度缓冲就会 z-fighting 闪烁，零厚度平面还会在掠射角整片消失。
 */

import { S } from "../state";

/** R2：真实档板厚 = Unity Col_Floor 的 0.4（SceneLayoutApplier.CreateColFloor）。 */
export const FLOOR_SLAB_REAL = 0.4;
/** R2：自适应档的上下限。 */
export const FLOOR_SLAB_AUTO_MAX = 0.4;
export const FLOOR_SLAB_AUTO_MIN = 0.06;
/** R2：薄片档（最接近 2D 平面观感）。 */
export const FLOOR_SLAB_THIN = 0.05;

/** R5：薄物件（表面物件 / 主题 quad / 水面 / 背景面）的最小可视厚度。 */
export const MIN_THIN_HEIGHT = 0.04;
/** 没有实测高度时的兜底盒高（核心玩法物件）。 */
export const FALLBACK_ITEM_HEIGHT = 1.0;
/**
 * 装饰类的兜底盒高。测不到网格的装饰（贝壳、卵石、地图植物，以及
 * killplanes / lights / 相机这类非视觉标记）绝大多数是小件或扁平件，
 * 给 1 米方块会在场景里竖起一片突兀的柱子，所以单独给一个矮兜底。
 */
export const FALLBACK_DECOR_HEIGHT = 0.3;
/** 最小可视盒高，避免零高度物件消失。 */
export const MIN_ITEM_HEIGHT = 0.08;
/**
 * 最大可视盒高。实测数据里天空盒 1398m、动态舞台 724m、冰崖 30m 都是真实值，
 * 但照实拉成盒子会把整个场景吞掉、挡住所有编辑目标。这里只钳制【显示】，
 * 不动数据本身（写回用的仍是原始 transform）。
 */
export const MAX_ITEM_HEIGHT = 12;
/**
 * pivot 偏移（catalog.baseY）的绝对上限。
 *
 * 合法的偏移可以远大于物件自身高度：墙顶盖板 h=0.215 / baseY=+1.79，
 * 悬挂绳索 h=0.216 / baseY=+0.90，石板路 h=0.204 / baseY=-2.57 —— 一旦按
 * 「|baseY| > 自身高度」丢弃，这些全会被拍到 pivot 平面上，肉眼可见地错位。
 * 真正该拦的是天空盒/舞台背景那一类（|baseY| 12~309），故用绝对阈值分界。
 */
export const MAX_PIVOT_OFFSET = 8;

/**
 * R6：renderOrder 阶梯。半透明贴花层共面时靠它决定谁盖谁，
 * 配合 depthWrite:false 避免互相遮挡写深度。
 */
export const ORDER = {
  grid: -10,
  floor: 0,
  floorSeam: 1,
  surfaceItem: 2,
  walkable: 3,
  killPlane: 4,
  voidHatch: 5,
  item: 10,
  airWall: 11,
  link: 20,
  waypoint: 22,
  route: 23,
  handle: 30,
  label: 40,
} as const;

/** R3：共面重叠地板的 polygonOffset 基准步进（按 2D 绘制序号递增）。 */
export const POLYGON_OFFSET_STEP = 1;

/** R8：相机近远平面。默认的 0.1/5000 撑不住 5 毫米级的分层。 */
export const CAMERA_NEAR = 0.1;
export const CAMERA_FAR = 300;

/** 当前档位下的地板板厚。auto 档由调用方传入「下方最近一层的间距」。 */
export function floorSlabThickness(gapBelow?: number): number {
  const mode = S.floorSlabMode;
  if (mode === "thin") return FLOOR_SLAB_THIN;
  if (mode === "real") return FLOOR_SLAB_REAL;
  // auto：贴着下层收缩，让台地读感清晰，不至于互相穿插成一坨。
  if (gapBelow == null || !Number.isFinite(gapBelow)) return FLOOR_SLAB_AUTO_MAX;
  return Math.min(FLOOR_SLAB_AUTO_MAX, Math.max(FLOOR_SLAB_AUTO_MIN, gapBelow));
}
