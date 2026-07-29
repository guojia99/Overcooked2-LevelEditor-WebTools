export function snapValue(v: number, step: number): number {
  if (step <= 0) return v;
  return Math.round(v / step) * step;
}

export function snapVec(x: number, z: number, step: number): { x: number; z: number } {
  return { x: snapValue(x, step), z: snapValue(z, step) };
}

/** Snap so footprint edges align to the grid (multi-cell objects can sit flush). */
export function snapFootprintCenter(
  wx: number,
  wz: number,
  cellsX: number,
  cellsZ: number,
  rotationY: number,
  cellSize: number,
  step: number
): { x: number; z: number } {
  const rot = ((rotationY % 360) + 360) % 360;
  const swap = rot === 90 || rot === 270;
  const spanX = (swap ? cellsZ : cellsX) * cellSize;
  const spanZ = (swap ? cellsX : cellsZ) * cellSize;
  const minX = wx - spanX / 2;
  const minZ = wz - spanZ / 2;
  return {
    x: snapValue(minX, step) + spanX / 2,
    z: snapValue(minZ, step) + spanZ / 2,
  };
}
