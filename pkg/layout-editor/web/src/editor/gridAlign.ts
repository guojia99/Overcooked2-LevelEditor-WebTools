import {
  S,
  CELL,
  EditorItem,
  EditorFloor,
  LayerKey
} from "./state";
import { itemLayerOfIt } from "./catalog";
import { itemInCurrentPart, floorInCurrentPart } from "./partLevel";
import { itemWorldAABB, moveBlockedAt } from "./items";
import {
  itemVisualCenterXZ,
  syncItemLocalFromEditor,
  normalizeRot
} from "./coords";
import { trySnapUtensilToHost } from "../stacking";
import { pushHistory, markDirty } from "./historyOps";
import { draw } from "./render";
import { setStatus } from "./status";

const EPS = 1e-9;

/** 单轴目标中心（以场景格原点为锚，无 GridInfo 时用世界 0）：
 *  直接对齐到最近的格子、填满整格——角贴可见网格线角 ((k+0.5)·CELL 点阵)：
 *  奇数跨距中心落整格点（如 6.00/-1.20），偶数跨距中心落两格之间（与游戏
 *  摆法一致）；<1 格（0.9 格锅/盘/杯）中心直接贴最近整格点。 */
function alignedCenterAxis(
  minEdge: number,
  span: number,
  centerCoord: number,
  origin: number
): number {
  const clean = (v: number) => Math.round(v * 1e6) / 1e6;
  if (span < CELL - EPS) {
    return clean(origin + Math.round((centerCoord - origin) / CELL) * CELL);
  }
  const snapped = (Math.round((minEdge - origin) / CELL - 0.5) + 0.5) * CELL;
  return clean(origin + snapped + span / 2);
}

/** 对齐点阵锚点：场景 QuadGridManager 原点（可见网格线由它起算），无 GridInfo 时世界 0。 */
function alignOriginXZ(): { x: number; z: number } {
  if (S.gridInfo?.found) {
    return { x: S.gridInfo.worldPosition.x, z: S.gridInfo.worldPosition.z };
  }
  return { x: 0, z: 0 };
}

/** 地板轴对齐占地矩形（旋转 90/270 换轴；木筏中心 = min + (w-1)/2·格，
 *  与写回拼板、snapRaftCenterAligned 同一约定）。 */
function floorRect(f: EditorFloor): { minX: number; maxX: number; minZ: number; maxZ: number } {
  const rot = normalizeRot(f.localRotationY ?? 0);
  const swap = rot === 90 || rot === 270;
  const cw = swap ? f._dCells : f._wCells;
  const cd = swap ? f._wCells : f._dCells;
  const spanX = cw * CELL;
  const spanZ = cd * CELL;
  if (f.surfaceKind === "raft") {
    const minX = f._wx - ((cw - 1) / 2) * CELL;
    const minZ = f._wz - ((cd - 1) / 2) * CELL;
    return { minX, maxX: minX + spanX, minZ, maxZ: minZ + spanZ };
  }
  return {
    minX: f._wx - spanX / 2,
    maxX: f._wx + spanX / 2,
    minZ: f._wz - spanZ / 2,
    maxZ: f._wz + spanZ / 2,
  };
}

interface ItemMove { it: EditorItem; dx: number; dz: number; }
interface FloorMove { f: EditorFloor; dx: number; dz: number; }

/** 一键把当前层全部物件对齐到最近的格子、填满整格（无视选中状态）。
 *  奇数跨距中心落整格点（如 6.00/-1.20），偶数跨距中心落两格之间，
 *  <1 格小件中心直接贴整格点；一次 pushHistory，可 Ctrl+Z 整体撤回。 */
export function alignCurrentLayerToGrid(): void {
  if (S.currentLayer !== "items" && S.currentLayer !== "floor") {
    setStatus("一键网格对齐：仅核心层 / 地板层可用", false);
    return;
  }
  const origin = alignOriginXZ();

  // ── 规划阶段（不改状态）：先算好每个物件的平移量，避免无变化时白写一条 undo ──
  const itemMoves: ItemMove[] = [];
  const floorMoves: FloorMove[] = [];
  let kept = 0;
  let blocked = 0;

  if (S.currentLayer === "items") {
    // 核心层：当前 P 全部核心物品（含玩家），无视选中状态。
    const targets = S.items.filter(
      (it) => itemLayerOfIt(it) === "items" && itemInCurrentPart(it)
    );
    for (const it of targets) {
      const box = itemWorldAABB(it);
      const vc = itemVisualCenterXZ(it);
      const nx = alignedCenterAxis(box.minX, box.maxX - box.minX, vc.x, origin.x);
      const nz = alignedCenterAxis(box.minZ, box.maxZ - box.minZ, vc.z, origin.z);
      const dx = nx - vc.x;
      const dz = nz - vc.z;
      if (Math.hypot(dx, dz) < 0.001) {
        kept++;
        continue;
      }
      if (moveBlockedAt(it, it._wx + dx, it._wz + dz)) {
        blocked++;
        continue;
      }
      itemMoves.push({ it, dx, dz });
    }
  } else {
    // 地板层：当前 P 全部非背景地板（含主题/图片/空气/木筏），无视选中状态。
    const targets = S.floors.filter(
      (f) => f.surfaceKind !== "background" && floorInCurrentPart(f)
    );
    for (const f of targets) {
      const r = floorRect(f);
      const cx = (r.minX + r.maxX) / 2;
      const cz = (r.minZ + r.maxZ) / 2;
      let nx: number;
      let nz: number;
      if (f.surfaceKind === "raft") {
        // 木筏：矩形 minX = 最左拼板（raftPiecesForRect 主格点阵 center±(w-1)/2·CELL）。
        // 拼板须落在整格点（每块板填满一个可见格，覆盖范围才铺满整格），
        // 与非木筏地板的「边贴网格线角」恰差半格。
        const clean = (v: number) => Math.round(v * 1e6) / 1e6;
        const sx = origin.x + Math.round((r.minX - origin.x) / CELL) * CELL;
        const sz = origin.z + Math.round((r.minZ - origin.z) / CELL) * CELL;
        nx = clean(sx + (r.maxX - r.minX) / 2);
        nz = clean(sz + (r.maxZ - r.minZ) / 2);
      } else {
        nx = alignedCenterAxis(r.minX, r.maxX - r.minX, cx, origin.x);
        nz = alignedCenterAxis(r.minZ, r.maxZ - r.minZ, cz, origin.z);
      }
      const dx = nx - cx;
      const dz = nz - cz;
      if (Math.hypot(dx, dz) < 0.001) {
        kept++;
        continue;
      }
      floorMoves.push({ f, dx, dz });
    }
  }

  if (itemMoves.length === 0 && floorMoves.length === 0) {
    const what = S.currentLayer === "items" ? "物件" : "地板";
    const notes: string[] = [];
    if (kept > 0) notes.push(`${kept} 个${what}已在网格上`);
    if (blocked > 0) notes.push(`${blocked} 个${what}因与玩家重叠跳过`);
    setStatus(`🧭 网格对齐：${notes.join("，") || "无可对齐物件"}`);
    return;
  }

  // ── 应用阶段：一条 undo 记录覆盖整批 ──
  pushHistory();
  for (const { it, dx, dz } of itemMoves) {
    it._wx += dx;
    it._wz += dz;
    syncItemLocalFromEditor(it);
    const cat = S.catalogByGuid.get(it.prefabGuid);
    if (cat?.stack) trySnapUtensilToHost(it, cat, S.items, S.catalogByGuid);
  }
  for (const { f, dx, dz } of floorMoves) {
    f._wx += dx;
    f._wz += dz;
    f.localPosition.x = f._wx;
    f.localPosition.z = f._wz;
    if (f.worldPosition) {
      f.worldPosition.x = f._wx;
      f.worldPosition.z = f._wz;
    }
  }
  markDirty();
  draw();

  const what = S.currentLayer === "items" ? "物件" : "地板";
  const parts = [`移动 ${itemMoves.length + floorMoves.length} 个${what}`];
  if (kept > 0) parts.push(`已在网格 ${kept}`);
  if (blocked > 0) parts.push(`跳过 ${blocked}（与玩家重叠）`);
  setStatus(`🧭 网格对齐：${parts.join(" · ")} · Ctrl+Z 可撤回，写回 Unity 后生效`);
}

/** 工具栏「🧭 一键对齐」按层启用/置灰（仅核心层 / 地板层）。 */
export function syncGridAlignUI(layer: LayerKey = S.currentLayer): void {
  const applies = layer === "items" || layer === "floor";
  const btn = document.getElementById("btn-grid-align") as HTMLButtonElement | null;
  if (btn) {
    btn.disabled = !applies;
    btn.title = applies
      ? "一键把当前层全部物件对齐到最近的格子、填满整格（如 1×1 中心落 6.00/-1.20 整格点而非 6.60/-1.80 半格位；可 Ctrl+Z 撤回）"
      : "一键网格对齐：仅核心层 / 地板层可用";
  }
}

/** 绑定按钮（点击立即对齐当前层）。 */
export function wireGridAlign(): void {
  document.getElementById("btn-grid-align")?.addEventListener("click", () => {
    alignCurrentLayerToGrid();
  });
  syncGridAlignUI();
}
