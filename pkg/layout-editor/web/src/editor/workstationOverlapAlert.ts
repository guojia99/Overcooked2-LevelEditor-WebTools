/**
 * 写回时工作台重叠阻断：高亮涉事物件、聚焦重叠区域，直到用户进行其他编辑操作。
 */

import { S } from "./state";
import { dom } from "./dom";
import { setSelection } from "./selection";
import { focusWorldBounds } from "./panels";
import { scene3dHandle, scene3dInvalidate } from "./scene3dBridge";
import type { WorkstationCollisionPair } from "./items";
import { itemWorldAABB } from "./items";

let animFrame = 0;

function requestPaint(): void {
  if (S.viewMode === "3d") scene3dInvalidate();
  else void import("./render").then((m) => m.draw());
}

export function workstationOverlapAlertActive(): boolean {
  return (S.workstationOverlapAlertKeys?.size ?? 0) > 0;
}

export function clearWorkstationOverlapAlert(): void {
  if (!S.workstationOverlapAlertKeys?.size) return;
  S.workstationOverlapAlertKeys = null;
  if (animFrame) {
    cancelAnimationFrame(animFrame);
    animFrame = 0;
  }
  requestPaint();
}

function boundsForPair(pair: WorkstationCollisionPair): {
  minX: number;
  maxX: number;
  minZ: number;
  maxZ: number;
} | null {
  const a = S.items.find((it) => it._editorKey === pair.keyA);
  const b = S.items.find((it) => it._editorKey === pair.keyB);
  if (!a || !b) return null;
  const xzA = itemWorldAABB(a);
  const xzB = itemWorldAABB(b);
  const minX = Math.max(xzA.minX, xzB.minX);
  const maxX = Math.min(xzA.maxX, xzB.maxX);
  const minZ = Math.max(xzA.minZ, xzB.minZ);
  const maxZ = Math.min(xzA.maxZ, xzB.maxZ);
  if (minX >= maxX || minZ >= maxZ) {
    const cx = (a._wx + b._wx) / 2;
    const cz = (a._wz + b._wz) / 2;
    const pad = 2.4;
    return { minX: cx - pad, maxX: cx + pad, minZ: cz - pad, maxZ: cz + pad };
  }
  return { minX, maxX, minZ, maxZ };
}

function focusPairs(pairs: WorkstationCollisionPair[]): void {
  let minX = Infinity;
  let maxX = -Infinity;
  let minZ = Infinity;
  let maxZ = -Infinity;
  for (const p of pairs) {
    const b = boundsForPair(p);
    if (!b) continue;
    minX = Math.min(minX, b.minX);
    maxX = Math.max(maxX, b.maxX);
    minZ = Math.min(minZ, b.minZ);
    maxZ = Math.max(maxZ, b.maxZ);
  }
  if (!Number.isFinite(minX)) return;
  const cx = (minX + maxX) / 2;
  const cz = (minZ + maxZ) / 2;
  focusWorldBounds(minX, maxX, minZ, maxZ, 1.2);
  const h = scene3dHandle();
  if (h && S.viewMode === "3d") h.lookAtWorld(cx, cz);
}

function startPulseLoop(): void {
  if (animFrame) return;
  const tick = () => {
    if (!workstationOverlapAlertActive()) {
      animFrame = 0;
      return;
    }
    requestPaint();
    animFrame = requestAnimationFrame(tick);
  };
  animFrame = requestAnimationFrame(tick);
}

/** 激活重叠告警（选中涉事物件、聚焦、画布高亮）。 */
export function showWorkstationOverlapAlert(pairs: WorkstationCollisionPair[]): void {
  const keys = new Set<string>();
  for (const p of pairs) {
    keys.add(p.keyA);
    keys.add(p.keyB);
  }
  if (keys.size === 0) return;
  S.workstationOverlapAlertKeys = keys;
  setSelection([...keys], pairs[0]?.keyA);
  focusPairs(pairs);
  startPulseLoop();
  requestPaint();
}

/** 2D 画布：在物品绘制之后描红框脉冲。 */
export function overlapAlertPulse(): number {
  return 0.45 + 0.55 * (0.5 + 0.5 * Math.sin(performance.now() / 220));
}
