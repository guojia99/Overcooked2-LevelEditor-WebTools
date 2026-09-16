import { S, EditorFloor, EditorItem } from "./state";
import {
  normalizeRot,
  itemPrefabId,
  itemVisualCenterXZ,
  visualCenterToStubXZ,
  largePotUnityFromVisualCenter,
  syncItemLocalFromEditor,
  stepDecimals,
  snapValue,
  isSwCornerPivotPrefabId,
  isLargePotPrefabId,
} from "./coords";
import { selectionKeys } from "./selection";
import { isPlayerItem } from "./renderItems";
import { pushHistory } from "./historyOps";
import { setStatus } from "./status";
import { rotateItemByDelta, moveBlockedAt } from "./items";
import { trySnapUtensilToHost } from "../stacking";

export type BatchRotateMode = "group" | "individual";

export function batchTransformTargets(): { items: EditorItem[]; floors: EditorFloor[] } {
  const items = selectionKeys()
    .map((k) => S.items.find((i) => i._editorKey === k))
    .filter((it): it is EditorItem => !!it && !isPlayerItem(it));
  const floors = [...S.selectedFloorKeys]
    .map((k) => S.floors.find((f) => f._key === k))
    .filter((f): f is EditorFloor => !!f);
  return { items, floors };
}

export function batchTransformCount(): number {
  const { items, floors } = batchTransformTargets();
  return items.length + floors.length;
}

export function isBatchTransform(): boolean {
  return batchTransformCount() >= 2;
}

function primarySelectedItem(): EditorItem | null {
  if (S.selectedKey) {
    const it = S.items.find((i) => i._editorKey === S.selectedKey);
    if (it && S.selectedKeys.has(it._editorKey) && !isPlayerItem(it)) return it;
  }
  const keys = selectionKeys();
  if (!keys.length) return null;
  return S.items.find((i) => i._editorKey === keys[0]) ?? null;
}

/** 绕世界 Y 轴顺时针旋转平面坐标（与 localRotationY +90° 一致）。 */
function rotateXZAround(
  cx: number,
  cz: number,
  x: number,
  z: number,
  deltaDeg: number
): { x: number; z: number } {
  const rad = (-deltaDeg * Math.PI) / 180;
  const dx = x - cx;
  const dz = z - cz;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  return {
    x: cx + dx * cos - dz * sin,
    z: cz + dx * sin + dz * cos,
  };
}

function groupCenterXZ(items: EditorItem[], floors: EditorFloor[]): { x: number; z: number } {
  let sx = 0;
  let sz = 0;
  let n = 0;
  for (const it of items) {
    const c = itemVisualCenterXZ(it);
    sx += c.x;
    sz += c.z;
    n++;
  }
  for (const f of floors) {
    sx += f._wx;
    sz += f._wz;
    n++;
  }
  if (!n) return { x: 0, z: 0 };
  return { x: sx / n, z: sz / n };
}

function setItemWorldXZ(item: EditorItem, wx: number, wz: number, rotY: number): void {
  const pid = itemPrefabId(item);
  item.localRotationY = normalizeRot(rotY);
  if (isSwCornerPivotPrefabId(pid)) {
    const stub = visualCenterToStubXZ(pid, wx, wz, item.localRotationY);
    item._wx = stub.x;
    item._wz = stub.z;
  } else if (isLargePotPrefabId(pid)) {
    const u = largePotUnityFromVisualCenter(wx, wz, item.localRotationY);
    item._wx = u.x;
    item._wz = u.z;
  } else {
    item._wx = wx;
    item._wz = wz;
  }
  syncItemLocalFromEditor(item);
  const cat = S.catalogByGuid.get(item.prefabGuid);
  if (cat?.stack) trySnapUtensilToHost(item, cat, S.items, S.catalogByGuid);
}

function rotateItemInGroup(
  item: EditorItem,
  cx: number,
  cz: number,
  deltaDeg: number
): void {
  const vc = itemVisualCenterXZ(item);
  const next = rotateXZAround(cx, cz, vc.x, vc.z, deltaDeg);
  const rotY = normalizeRot(item.localRotationY + deltaDeg);
  setItemWorldXZ(item, next.x, next.z, rotY);
}

export function batchRotateSelected(deltaDeg: number, opts?: { skipHistory?: boolean }): number {
  if (!deltaDeg || !isFinite(deltaDeg)) return 0;
  const { items, floors } = batchTransformTargets();
  if (items.length + floors.length < 2) return 0;

  if (!opts?.skipHistory) pushHistory();
  const mode = S.batchRotateMode;
  const center = groupCenterXZ(items, floors);
  let changed = 0;

  if (mode === "group") {
    for (const it of items) {
      rotateItemInGroup(it, center.x, center.z, deltaDeg);
      changed++;
    }
    for (const f of floors) {
      const next = rotateXZAround(center.x, center.z, f._wx, f._wz, deltaDeg);
      f._wx = snapValue(next.x, S.freeSnapStep);
      f._wz = snapValue(next.z, S.freeSnapStep);
      f.localRotationY = normalizeRot(f.localRotationY + deltaDeg);
      changed++;
    }
  } else {
    for (const it of items) {
      rotateItemByDelta(it, deltaDeg);
      changed++;
    }
    for (const f of floors) {
      f.localRotationY = normalizeRot(f.localRotationY + deltaDeg);
      changed++;
    }
  }

  if (changed > 0) S.dirty = true;
  return changed;
}

/** 将选中项统一设为绝对旋转角。整体模式以主选项为基准旋转整组；各自模式逐项设为目标角。 */
export function batchSetSelectionRotation(deg: number, opts?: { skipHistory?: boolean }): number {
  const { items, floors } = batchTransformTargets();
  if (items.length + floors.length < 2) return 0;
  const target = normalizeRot(deg);

  if (S.batchRotateMode === "individual") {
    if (!opts?.skipHistory) pushHistory();
    let changed = 0;
    for (const it of items) {
      const cur = normalizeRot(it.localRotationY);
      if (cur === target) continue;
      rotateItemByDelta(it, normalizeRot(target - cur));
      changed++;
    }
    for (const f of floors) {
      if (normalizeRot(f.localRotationY) === target) continue;
      f.localRotationY = target;
      changed++;
    }
    if (changed > 0) S.dirty = true;
    return changed;
  }

  const primary = primarySelectedItem();
  const refRot = primary
    ? normalizeRot(primary.localRotationY)
    : floors[0]
      ? normalizeRot(floors[0].localRotationY)
      : 0;
  const delta = normalizeRot(target - refRot);
  if (delta === 0) return 0;
  return batchRotateSelected(delta, opts);
}

export function batchRotationSummary(): {
  count: number;
  mixed: boolean;
  displayRot: number | null;
  mode: BatchRotateMode;
} {
  const { items, floors } = batchTransformTargets();
  const count = items.length + floors.length;
  const rots: number[] = [];
  for (const it of items) rots.push(normalizeRot(it.localRotationY));
  for (const f of floors) rots.push(normalizeRot(f.localRotationY));
  const mixed = rots.length > 1 && rots.some((r) => r !== rots[0]);
  const primary = primarySelectedItem();
  let displayRot: number | null = null;
  if (primary) displayRot = normalizeRot(primary.localRotationY);
  else if (floors.length && S.selectedFloorKey && S.selectedFloorKeys.has(S.selectedFloorKey)) {
    const f = S.floors.find((x) => x._key === S.selectedFloorKey);
    if (f) displayRot = normalizeRot(f.localRotationY);
  } else if (rots.length) displayRot = rots[0];
  return { count, mixed, displayRot, mode: S.batchRotateMode };
}

export type BatchRandomRotationKind = "cardinal" | "free";

function shuffleArray<T>(arr: T[]): T[] {
  const a = [...arr];
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j]!, a[i]!];
  }
  return a;
}

/** 为 count 个目标生成尽量不重复的随机角度（90° 系列最多 4 种，超出后循环洗牌）。 */
function distinctRandomRotations(kind: BatchRandomRotationKind, count: number): number[] {
  if (count <= 0) return [];
  if (kind === "cardinal") {
    const pool = [0, 90, 180, 270];
    const out: number[] = [];
    while (out.length < count) {
      for (const a of shuffleArray(pool)) {
        if (out.length >= count) break;
        out.push(a);
      }
    }
    return out;
  }
  if (count > 360) {
    const used = new Set<number>();
    const out: number[] = [];
    while (out.length < count) {
      let a = Math.floor(Math.random() * 360);
      if (used.has(a)) {
        let found = -1;
        for (let d = 0; d < 360; d++) {
          if (!used.has(d)) {
            found = d;
            break;
          }
        }
        if (found < 0) break;
        a = found;
      }
      used.add(a);
      out.push(a);
    }
    return out;
  }
  return shuffleArray(Array.from({ length: 360 }, (_, i) => i)).slice(0, count);
}

/** 一键随机角度：每项分配尽量不重复的角度（忽略整体/各自模式）。 */
export function batchRandomizeSelectionRotation(kind: BatchRandomRotationKind): number {
  const { items, floors } = batchTransformTargets();
  const targets = [...items, ...floors];
  if (targets.length < 2) return 0;

  const angles = distinctRandomRotations(kind, targets.length);
  pushHistory();
  let changed = 0;
  let ai = 0;

  for (const it of items) {
    const target = angles[ai++]!;
    const cur = normalizeRot(it.localRotationY);
    if (cur === target) continue;
    rotateItemByDelta(it, normalizeRot(target - cur));
    changed++;
  }
  for (const f of floors) {
    const target = angles[ai++]!;
    if (normalizeRot(f.localRotationY) === target) continue;
    f.localRotationY = target;
    changed++;
  }

  if (changed > 0) S.dirty = true;
  return changed;
}

type DisperseTarget =
  | { kind: "item"; it: EditorItem; x: number; z: number }
  | { kind: "floor"; f: EditorFloor; x: number; z: number };

function itemStubXZAtVisual(it: EditorItem, vx: number, vz: number): { x: number; z: number } {
  const pid = itemPrefabId(it);
  const rot = it.localRotationY;
  if (isSwCornerPivotPrefabId(pid)) return visualCenterToStubXZ(pid, vx, vz, rot);
  if (isLargePotPrefabId(pid)) return largePotUnityFromVisualCenter(vx, vz, rot);
  return { x: vx, z: vz };
}

/** 以选中集中心为基准，沿径向移动每项（out=向外分散，in=向心收缩至重叠）。 */
function batchRadialMoveSelected(amount: number, direction: "out" | "in"): number {
  if (!amount || !isFinite(amount) || amount <= 0) return 0;
  const { items, floors } = batchTransformTargets();
  if (items.length + floors.length < 2) return 0;

  const center = groupCenterXZ(items, floors);
  const targets: DisperseTarget[] = [];
  for (const it of items) {
    const c = itemVisualCenterXZ(it);
    targets.push({ kind: "item", it, x: c.x, z: c.z });
  }
  for (const f of floors) {
    targets.push({ kind: "floor", f, x: f._wx, z: f._wz });
  }

  const atCenter: DisperseTarget[] = [];
  const moves: Array<{ target: DisperseTarget; nx: number; nz: number }> = [];
  for (const t of targets) {
    const dx = t.x - center.x;
    const dz = t.z - center.z;
    const dist = Math.hypot(dx, dz);
    if (direction === "in") {
      if (dist < 0.001) continue;
      const newDist = Math.max(0, dist - amount);
      if (newDist < 0.001) {
        moves.push({ target: t, nx: center.x, nz: center.z });
      } else {
        const scale = newDist / dist;
        moves.push({
          target: t,
          nx: center.x + dx * scale,
          nz: center.z + dz * scale,
        });
      }
      continue;
    }
    if (dist < 0.001) {
      atCenter.push(t);
      continue;
    }
    const scale = (dist + amount) / dist;
    moves.push({
      target: t,
      nx: center.x + dx * scale,
      nz: center.z + dz * scale,
    });
  }
  if (direction === "out" && atCenter.length > 0) {
    const n = atCenter.length;
    for (let i = 0; i < n; i++) {
      const ang = (i / n) * Math.PI * 2;
      moves.push({
        target: atCenter[i]!,
        nx: center.x + Math.cos(ang) * amount,
        nz: center.z + Math.sin(ang) * amount,
      });
    }
  }

  const ignore = new Set(selectionKeys());
  let blocked = false;
  for (const m of moves) {
    if (m.target.kind !== "item") continue;
    const stub = itemStubXZAtVisual(m.target.it, m.nx, m.nz);
    if (moveBlockedAt(m.target.it, stub.x, stub.z, ignore)) blocked = true;
  }

  pushHistory();
  let changed = 0;
  for (const m of moves) {
    if (m.target.kind === "item") {
      const it = m.target.it;
      const stub = itemStubXZAtVisual(it, m.nx, m.nz);
      if (moveBlockedAt(it, stub.x, stub.z, ignore)) continue;
      setItemWorldXZ(it, m.nx, m.nz, it.localRotationY);
      changed++;
    } else {
      const f = m.target.f;
      f._wx = snapValue(m.nx, S.freeSnapStep);
      f._wz = snapValue(m.nz, S.freeSnapStep);
      changed++;
    }
  }

  if (blocked) setStatus("部分目标位置与玩家重叠，已跳过", false);
  if (changed > 0) S.dirty = true;
  return changed;
}

/** 以选中集中心为基准，沿径向将每项向外推开 amount（米）。 */
export function batchDisperseSelected(amount: number): number {
  return batchRadialMoveSelected(amount, "out");
}

/** 以选中集中心为基准，沿径向将每项向内收缩 amount（米），可直至中心重叠。 */
export function batchContractSelected(amount: number): number {
  return batchRadialMoveSelected(amount, "in");
}

export function batchNudgeSelected(dx: number, dz: number, dy = 0): void {
  const { items, floors } = batchTransformTargets();
  if (items.length + floors.length < 2) return;

  const ignore = new Set(selectionKeys());
  let blocked = false;
  for (const it of items) {
    if (moveBlockedAt(it, it._wx + dx, it._wz + dz, ignore)) {
      blocked = true;
      continue;
    }
  }
  pushHistory();
  for (const f of floors) {
    f._wx = snapValue(f._wx + dx, S.freeSnapStep);
    f._wz = snapValue(f._wz + dz, S.freeSnapStep);
  }
  for (const it of items) {
    if (moveBlockedAt(it, it._wx + dx, it._wz + dz, ignore)) continue;
    it._wx += dx;
    it._wz += dz;
    syncItemLocalFromEditor(it);
    if (dy !== 0) {
      it.localPosition.y = snapValue(it.localPosition.y + dy, S.freeSnapStep);
      if (it.worldPosition) it.worldPosition.y = it.localPosition.y;
    }
    const cat = S.catalogByGuid.get(it.prefabGuid);
    if (cat?.stack) trySnapUtensilToHost(it, cat, S.items, S.catalogByGuid);
  }
  if (blocked) setStatus("部分目标位置与玩家重叠，已跳过", false);
  S.dirty = true;
}

export function batchDisperseRowHtml(): string {
  const n = batchTransformCount();
  if (n < 2) return "";
  return `
    <div class="ctx-nudge-row ctx-batch-disperse">
      <span class="ctx-label">分散 / 收缩 <span class="ctx-scale-val">${n} 项 · 以中心为基准</span></span>
      <div class="ctx-radial-row">
        <span class="ctx-radial-tag">外</span>
        <div class="ctx-random-rot">
          <button type="button" data-sel-disperse="0.1" title="沿径向向外推开 0.1 m">0.1</button>
          <button type="button" data-sel-disperse="0.5" title="沿径向向外推开 0.5 m">0.5</button>
          <button type="button" data-sel-disperse="1" title="沿径向向外推开 1 m">1</button>
        </div>
      </div>
      <div class="ctx-radial-row">
        <span class="ctx-radial-tag">内</span>
        <div class="ctx-random-rot">
          <button type="button" data-sel-contract="0.1" title="沿径向向心收缩 0.1 m">0.1</button>
          <button type="button" data-sel-contract="0.5" title="沿径向向心收缩 0.5 m">0.5</button>
          <button type="button" data-sel-contract="1" title="沿径向向心收缩 1 m（可直至中心重叠）">1</button>
        </div>
      </div>
    </div>`;
}

export function wireBatchDisperseRow(root: HTMLElement, onApplied?: () => void): void {
  root.querySelectorAll<HTMLButtonElement>("[data-sel-disperse]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const amount = parseFloat(btn.dataset.selDisperse ?? "0");
      const n = batchDisperseSelected(amount);
      if (n === 0) return;
      onApplied?.();
      setStatus(`已将 ${n} 项从中心向外分散 ${amount} m`);
    });
  });
  root.querySelectorAll<HTMLButtonElement>("[data-sel-contract]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const amount = parseFloat(btn.dataset.selContract ?? "0");
      const n = batchContractSelected(amount);
      if (n === 0) return;
      onApplied?.();
      setStatus(`已将 ${n} 项向中心收缩 ${amount} m`);
    });
  });
}

export function batchRotationRowHtml(): string {
  const { count, mixed, displayRot, mode } = batchRotationSummary();
  if (count < 2) return "";
  const rotVal = mixed || displayRot == null ? "混合" : `${displayRot}°`;
  const rotInput = mixed || displayRot == null ? "" : String(displayRot);
  return `
    <div class="ctx-nudge-row ctx-batch-rot">
      <span class="ctx-label">批量旋转 <span class="ctx-scale-val" id="sel-rot-count">${count} 项 · ${rotVal}</span></span>
      <div class="ctx-rotate-mode" title="整体：绕选中集中心旋转；各自：每项原地旋转">
        <label class="ctx-mode-opt"><input type="radio" name="sel-rot-mode" value="group"${mode === "group" ? " checked" : ""} /> 整体</label>
        <label class="ctx-mode-opt"><input type="radio" name="sel-rot-mode" value="individual"${mode === "individual" ? " checked" : ""} /> 各自</label>
      </div>
      <div class="ctx-nudge">
        <button type="button" data-sel-rot="-90" title="逆时针 90°">−90°</button>
        <input type="number" id="sel-rot-input" class="ctx-input ctx-rot-input" min="0" max="359" step="1" value="${rotInput}" placeholder="${mixed ? "混合" : "°"}" title="统一设为该角度（回车生效，以主选项为基准）" />
        <button type="button" data-sel-rot="90" title="顺时针 90°">+90°</button>
      </div>
      <div class="ctx-random-rot" title="每项分配尽量不重复的随机角度">
        <button type="button" data-sel-rand="cardinal" title="0°/90°/180°/270° 洗牌分配，尽量不重复">随机 90°</button>
        <button type="button" data-sel-rand="free" title="0°–359° 洗牌分配，尽量不重复">随机 360°</button>
      </div>
    </div>`;
}

export function wireBatchRotationRow(
  root: HTMLElement,
  onApplied?: () => void
): void {
  const input = root.querySelector<HTMLInputElement>("#sel-rot-input");
  if (!input) return;

  let rotPushed = false;
  const applyDeg = (deg: number, announce = false) => {
    if (!isFinite(deg)) return;
    if (!rotPushed) {
      pushHistory();
      rotPushed = true;
    }
    const n = batchSetSelectionRotation(deg, { skipHistory: true });
    if (n === 0 && !announce) return;
    refreshVal();
    onApplied?.();
    if (announce) setStatus(`已将选中项旋转统一为 ${normalizeRot(deg)}°`);
  };

  const refreshVal = () => {
    const { mixed, displayRot } = batchRotationSummary();
    if (document.activeElement === input) return;
    input.value = mixed || displayRot == null ? "" : String(displayRot);
    input.placeholder = mixed ? "混合" : "°";
    const lbl = root.querySelector("#sel-rot-count");
    if (lbl) {
      const { count } = batchRotationSummary();
      const rv = mixed || displayRot == null ? "混合" : `${displayRot}°`;
      lbl.textContent = `${count} 项 · ${rv}`;
    }
  };

  root.querySelectorAll<HTMLInputElement>('input[name="sel-rot-mode"]').forEach((radio) => {
    radio.addEventListener("change", () => {
      if (!radio.checked) return;
      S.batchRotateMode = radio.value === "individual" ? "individual" : "group";
    });
  });

  root.querySelectorAll<HTMLButtonElement>("[data-sel-rot]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const delta = parseFloat(btn.dataset.selRot ?? "0");
      const n = batchRotateSelected(delta);
      if (n === 0) return;
      refreshVal();
      onApplied?.();
      const modeZh = S.batchRotateMode === "group" ? "整体" : "各自";
      setStatus(`已${modeZh}旋转 ${n} 项（${delta > 0 ? "+" : ""}${delta}°）`);
    });
  });

  root.querySelectorAll<HTMLButtonElement>("[data-sel-rand]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const kind = btn.dataset.selRand === "free" ? "free" : "cardinal";
      const n = batchRandomizeSelectionRotation(kind);
      if (n === 0) return;
      refreshVal();
      onApplied?.();
      const kindZh = kind === "cardinal" ? "90° 系列" : "360°";
      setStatus(`已为 ${n} 项分配尽量不重复的随机角度（${kindZh}）`);
    });
  });

  input.addEventListener("input", () => {
    const raw = input.value.trim();
    if (!raw) return;
    applyDeg(parseFloat(raw));
  });
  input.addEventListener("change", () => {
    const raw = input.value.trim();
    if (!raw) return;
    applyDeg(parseFloat(raw), true);
  });
}

export function batchNudgeRowLabel(): string {
  const n = batchTransformCount();
  return n >= 2 ? `微移 ${S.freeSnapStep.toFixed(stepDecimals(S.freeSnapStep))}（${n} 项）` : "";
}
