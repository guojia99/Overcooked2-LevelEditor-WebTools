/** 2D 画布缩放范围（各编辑层共用；与 3D 切换时 writeBackTo2D 一致）。 */
export const CANVAS_SCALE_MIN = 0.125;
export const CANVAS_SCALE_MAX = 8;

export function clampCanvasScale(scale: number): number {
  return Math.min(CANVAS_SCALE_MAX, Math.max(CANVAS_SCALE_MIN, scale));
}
