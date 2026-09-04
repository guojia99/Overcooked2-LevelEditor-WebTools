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

/** 中心 pivot 道具专用吸附：长轴（偶数格 span）根吸到半格奇偶位
 *  ((k+0.5)·cellSize，即 0.6 mod 1.2），短轴（奇数格 span）根吸到整格点。
 *  这样两个工作位（根 ±半格）恰好落在格子中心，不会一半概率吸到错位格。 */
export function snapCenterPivot(
  wx: number,
  wz: number,
  cellsX: number,
  cellsZ: number,
  rotationY: number,
  cellSize: number
): { x: number; z: number } {
  const rot = ((rotationY % 360) + 360) % 360;
  const swap = rot === 90 || rot === 270;
  const spanX = (swap ? cellsZ : cellsX) * cellSize;
  const spanZ = (swap ? cellsX : cellsZ) * cellSize;
  return {
    x: snapCenterPivotAxis(wx, spanX, cellSize),
    z: snapCenterPivotAxis(wz, spanZ, cellSize),
  };
}

function snapCenterPivotAxis(v: number, span: number, cellSize: number): number {
  const cells = Math.round(span / cellSize);
  if (cells > 0 && cells % 2 === 0) {
    // 偶数格轴：根在 (k + 0.5) · cellSize
    return (Math.round(v / cellSize - 0.5) + 0.5) * cellSize;
  }
  // 奇数格轴：根在整格点 k · cellSize
  return snapValue(v, cellSize);
}

/** Unity EditorGridSnap / QuadGridManager 格子中心格点：(k + 0.5) · cellSize（0.6 mod 1.2）。 */
export function snapOddLatticeAxis(v: number, cellSize: number): number {
  return (Math.round(v / cellSize - 0.5) + 0.5) * cellSize;
}

export function snapOddLattice(
  wx: number,
  wz: number,
  cellSize: number
): { x: number; z: number } {
  return { x: snapOddLatticeAxis(wx, cellSize), z: snapOddLatticeAxis(wz, cellSize) };
}
