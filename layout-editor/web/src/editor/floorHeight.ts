import {
  S,
  CELL,
  EditorFloor,
  EditorItem
} from "./state";
import { normalizeRot } from "./coords";

/**
 * 行走面高度（walk-surface height）约定——与后端 SceneLayoutApplier.FloorWalkY 一致：
 * 默认地板的视觉 Y 为 -0.05（实心 Plane）/ 0.01（主题地板）/ 0（空气地板），
 * 它们的角色站立面都是 0；只有明显抬高（y > 0.05）才算高台。
 * 层归属、高度过滤、画布标签全部用这个 h，不用原始 localPosition.y。
 */
export function floorWalkY(f: EditorFloor): number {
  const y = f.localPosition?.y ?? 0;
  return y <= 0.05 ? 0 : y;
}

/** 物品的「脚底高度」：与地板同一约定（y<=0.05 视为地面层 0）。
 *  叠放物品（锅上灶 y≈1）按其实际 Y 归层，不按宿主层。 */
export function itemHeightOf(it: EditorItem): number {
  const y = it.localPosition?.y ?? 0;
  return y <= 0.05 ? 0 : y;
}

/** 高度过滤适用的图层：地板 / 核心 / 装饰（背景层与移动层不过滤）。
 *  过滤面板悬浮在画布左下角，在这三层可用。 */
export function floorHeightFilterLayers(): ReadonlySet<string> {
  return HEIGHT_FILTER_LAYERS;
}
const HEIGHT_FILTER_LAYERS: ReadonlySet<string> = new Set(["floor", "items", "decor"]);

/** 高度过滤是否生效（仅在过滤适用的图层生效，避免其它层「内容凭空消失」）。 */
export function floorHeightFilterActive(): boolean {
  if (!HEIGHT_FILTER_LAYERS.has(S.currentLayer)) return false;
  return S.floorHeight.min != null && S.floorHeight.max != null;
}

/** 高度值是否在过滤区间内（不过滤图层/未设置区间时恒 true）。 */
export function floorHeightInRange(h: number): boolean {
  if (!floorHeightFilterActive()) return true;
  const min = S.floorHeight.min as number;
  const max = S.floorHeight.max as number;
  return h >= min - 1e-6 && h <= max + 1e-6;
}

export function floorInHeightFilter(f: EditorFloor): boolean {
  return floorHeightInRange(floorWalkY(f));
}

export function itemInHeightFilter(it: EditorItem): boolean {
  return floorHeightInRange(itemHeightOf(it));
}

/** 高度 h 所属的层索引（层厚可调，L_n = [n*t, (n+1)*t)）。h 恒 >= 0（见 floorWalkY）。 */
export function floorLayerIndex(h: number): number {
  const t = Math.max(0.05, S.floorHeight.thickness || 0.2);
  return Math.floor((h + 1e-6) / t);
}

/** 放置点 (wx,wz) 处的行走面高度：覆盖该点的非背景地板里最高的 h。
 *  onlyInRange=true 时只考虑当前过滤区间内的地板（用于过滤激活时放置物品）。
 *  返回 -1 表示该点没有任何（符合条件的）地板。 */
export function floorHeightAt(wx: number, wz: number, onlyInRange = false): number {
  let h = -1;
  for (const f of S.floors) {
    if (f.surfaceKind === "background") continue;
    const hw = (f._wCells * CELL) / 2;
    const hh = (f._dCells * CELL) / 2;
    const dx = wx - f._wx;
    const dz = wz - f._wz;
    const rad = (normalizeRot(f.localRotationY) * Math.PI) / 180;
    const cos = Math.cos(rad);
    const sin = Math.sin(rad);
    const lx = dx * cos + dz * sin;
    const lz = -dx * sin + dz * cos;
    if (Math.abs(lx) > hw + 0.01 || Math.abs(lz) > hh + 0.01) continue;
    const fy = floorWalkY(f);
    if (onlyInRange && !floorHeightInRange(fy)) continue;
    h = Math.max(h, fy);
  }
  return h;
}

/** 高度过滤激活时新地板的默认 Y：区间底 + 0.01，使新地板落在当前显示层内。 */
export function defaultNewFloorY(fallback: number): number {
  return S.floorHeight.min != null ? S.floorHeight.min + 0.01 : fallback;
}

/** 把用户设定的行走面高度 h 转成该类型地板的视觉 localPosition.y：
 *  h=0 回落到类型默认（实心 -0.05 / 主题 0.01 / 空气 0），h>0 时视觉=行走面。 */
export function floorVisualYForWalkHeight(
  h: number,
  kind: "plane" | "themed" | "air"
): number {
  if (h <= 0.05) return kind === "themed" ? 0.01 : kind === "air" ? 0 : -0.05;
  return h;
}

/** 层列表数据：按行走面高度聚合现有地板与物品（始终含 L0）。 */
export function floorLayerSummary(): {
  index: number;
  lo: number;
  hi: number;
  count: number;
  itemCount: number;
}[] {
  const t = Math.max(0.05, S.floorHeight.thickness || 0.2);
  const counts = new Map<number, number>();
  const itemCounts = new Map<number, number>();
  let maxIdx = 0;
  for (const f of S.floors) {
    if (f.surfaceKind === "background") continue;
    const idx = floorLayerIndex(floorWalkY(f));
    counts.set(idx, (counts.get(idx) ?? 0) + 1);
    if (idx > maxIdx) maxIdx = idx;
  }
  for (const it of S.items) {
    const idx = floorLayerIndex(itemHeightOf(it));
    itemCounts.set(idx, (itemCounts.get(idx) ?? 0) + 1);
    if (idx > maxIdx) maxIdx = idx;
  }
  const out: { index: number; lo: number; hi: number; count: number; itemCount: number }[] = [];
  for (let i = 0; i <= maxIdx; i++) {
    out.push({
      index: i,
      lo: i * t,
      hi: (i + 1) * t,
      count: counts.get(i) ?? 0,
      itemCount: itemCounts.get(i) ?? 0,
    });
  }
  return out;
}
