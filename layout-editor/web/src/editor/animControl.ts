import {
  S,
  CELL,
  type AnimMode,
  type EditorItem
} from "./state";
import { dom } from "./dom";
import {
  uuid,
  worldToCanvas,
  escHtml,
  snapValue
} from "./coords";
import { itemLabel } from "./labels";
import { itemLayerOfIt, itemCategoryOf } from "./catalog";
import { pushHistory } from "./historyOps";
import { draw } from "./render";
import {
  setSelection,
  clearSelection,
  clearFloorSelection
} from "./selection";
import { setStatus } from "./status";
import {
  ensureItemVisible,
  renderRightPanel,
  updatePanelTabButtons
} from "./panels";
import { isCollisionItem } from "./stubControls";
import { renameGroupInButtonLinks, linkBindingGroup, cleanOrphanedButtonLinks } from "./buttonLinks";
import { openModal, closeModal } from "../modals";
import { setLayer } from "./init";
import type {
  AnimGroup,
  AnimGroupEvent,
  AnimGroupMemberGroup,
  AnimGroupWaypoint
} from "../types";

// ------------------------------------------------------------------ group colors

const GROUP_COLORS = [
  "#3d6bf3", "#2f9e6e", "#d8703c", "#8a5fd6", "#c23a4a",
  "#1f9ab0", "#b0893c", "#5b7bd6", "#c25bd6", "#6ba83c",
];

/** Stable color per move group (same id ⇒ same color across canvas + panel). */
export function groupColor(id: string): string {
  let h = 7;
  for (let i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) >>> 0;
  return GROUP_COLORS[h % GROUP_COLORS.length];
}

function colorA(hex: string, a: number): string {
  const r = parseInt(hex.slice(1, 3), 16);
  const g = parseInt(hex.slice(3, 5), 16);
  const b = parseInt(hex.slice(5, 7), 16);
  return `rgba(${r},${g},${b},${a})`;
}

const WAYPOINT_COLORS = [
  "#3d6bf3", "#2f9e6e", "#d8703c", "#8a5fd6",
  "#c2394a", "#1f9ab0", "#b0893c", "#c25bd6",
];

/** Stable color per waypoint — the same point keeps the same color everywhere
 *  (canvas + panels), so points reused by several events stay distinguishable. */
export function waypointColor(id: string): string {
  let h = 3;
  for (let i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) >>> 0;
  return WAYPOINT_COLORS[h % WAYPOINT_COLORS.length];
}

/** Color per event type — event cards use these so 移动 / 抬起 / 落下 / 等待 / 旋转
 *  / 全屏抖动 / 闪电 read at a glance in the events list. */
export const EVENT_TYPE_COLORS: Record<AnimGroupEvent["type"], string> = {
  move: "#3d6bf3",
  lift: "#8a5fd6",
  drop: "#c23a4a",
  wait: "#8b93a3",
  rotate: "#e8930c",
  shake: "#c9793c",
  flash: "#e8d94c",
};

/** 时间轴粒度：所有时间（startTime / duration / 间隔）吸附到 0.1 秒。 */
export const TIMELINE_STEP = 0.1;

/** 时间轴 UI 缩放：基准每秒像素数（缩放倍率 1 时）。 */
const TL_PPS = 48;

/** 时间轴缩放倍率（0.2x ~ 10x，Ctrl/⌘+滚轮 或工具栏 ± 调整）。 */
let tlZoom = 1;

/** 当前时间轴每秒像素数。 */
function tlPps(): number {
  return TL_PPS * tlZoom;
}

/** 横向滚动位置（渲染刷新后恢复，缩放时用于锚定光标处的时间点）。 */
let tlSavedScroll = 0;

/** 缩放倍率取值范围。 */
const TL_ZOOM_MIN = 0.2;
const TL_ZOOM_MAX = 10;

/** 标尺刻度步长（秒）：随缩放自适应，保证刻度间距不至于过密。 */
function tlTickStep(pps: number): number {
  if (pps >= 96) return 0.5;
  if (pps >= 30) return 1;
  if (pps >= 15) return 2;
  if (pps >= 8) return 5;
  return 10;
}

export function snapTime(v: number): number {
  // 注意用 *10/10 而非 /0.1*0.1：后者产生 3.4000000000000004 这类浮点残渣，
  // 会污染输入框显示与泳道邻接判断。
  return Math.max(0, Math.round(v * 10) / 10);
}

export function eventTypeColor(type: AnimGroupEvent["type"]): string {
  return EVENT_TYPE_COLORS[type];
}

export function eventTypeLabel(type: AnimGroupEvent["type"]): string {
  return type === "move" ? "移动"
    : type === "lift" ? "抬起"
    : type === "drop" ? "落下"
    : type === "rotate" ? "旋转"
    : type === "shake" ? "全屏抖动"
    : type === "flash" ? "闪电"
    : "等待";
}

/** 全屏特效组判定（groupKind="fx"：无成员，事件仅 shake/flash/wait）。 */
export function isFxGroup(group: AnimGroup): boolean {
  return group.groupKind === "fx";
}

/** 特效组的特效类型（组内首个特效事件；全 wait / 空组返回 null）。 */
export function groupFxType(group: AnimGroup): "shake" | "flash" | null {
  if (!isFxGroup(group)) return null;
  for (const e of group.events) {
    if (e.type === "shake") return "shake";
    if (e.type === "flash") return "flash";
  }
  return null;
}

// ------------------------------------------------------------------ basics

export function activeGroup(): AnimGroup | null {
  if (!S.activeAnimGroupId) return null;
  return S.animControls.find((g) => g.id === S.activeAnimGroupId) ?? null;
}

function clearAnimSelection(): void {
  clearSelection();
  clearFloorSelection();
  S.selectedKey = null;
}

export function groupVisibleInLayer(g: AnimGroup): boolean {
  if (S.currentLayer === "anim" || S.currentLayer === "background") return true;
  if (S.currentLayer === "floor") return true;
  // 全屏特效是场景级的（相机/灯），与物品图层无关：任何图层都可见。
  if (isFxGroup(g)) return true;
  return g.itemInstanceIds.some(
    (id) =>
      S.items.some(
        (it) =>
          it.instanceId === id &&
          itemLayerOfIt(it) === S.currentLayer
      )
  ) || g.objectInstanceIds.length > 0;
}

export function findGroupByItemId(instanceId: string): AnimGroup | null {
  return S.animControls.find((g) => g.itemInstanceIds.includes(instanceId)) ?? null;
}

export function findGroupById(id: string): AnimGroup | null {
  return S.animControls.find((g) => g.id === id) ?? null;
}

/** Waypoint pick tolerance: points within 0.5 格 of each other count as
 *  overlapping (same spirit as the item footprint pick). */
export const WAYPOINT_HIT_RADIUS = CELL * 0.5;

/** All waypoints within the pick radius of a world position (0.5 格). Multiple
 *  hits = overlapping cluster, picked the same way items are (picker list). */
export function hitTestWaypoints(wx: number, wz: number): { id: string }[] {
  const hits: { id: string }[] = [];
  const r2 = WAYPOINT_HIT_RADIUS * WAYPOINT_HIT_RADIUS;
  for (const group of S.animControls) {
    if (group.id !== S.activeAnimGroupId && S.activeAnimGroupId) continue;
    for (const wp of group.waypoints) {
      const dx = wp.x - wx;
      const dz = wp.z - wz;
      if (dx * dx + dz * dz <= r2) {
        hits.push({ id: wp.id });
      }
    }
  }
  return hits;
}

/** Group + waypoint + stable global number (#1-based) for a waypoint id. */
export function waypointInfo(wpId: string): { group: AnimGroup; wp: AnimGroupWaypoint; num: number } | null {
  for (const g of S.animControls) {
    const idx = g.waypoints.findIndex((w) => w.id === wpId);
    if (idx >= 0) return { group: g, wp: g.waypoints[idx], num: idx + 1 };
  }
  return null;
}

/** Delete a waypoint (from the pool and every event route). */
export function deleteWaypoint(group: AnimGroup, wpId: string): void {
  group.waypoints = group.waypoints.filter((w) => w.id !== wpId);
  for (const evt of group.events) {
    if (evt.waypointIds) evt.waypointIds = evt.waypointIds.filter((id) => id !== wpId);
  }
  if (S.selectedWaypointId === wpId) S.selectedWaypointId = null;
  S.dirty = true;
}

export function cleanOrphanedAnimControls(): void {
  const liveIds = new Set(S.items.map((it) => it.instanceId));
  const liveFloorIds = new Set(S.floors.map((f) => f.instanceId));
  S.animControls = S.animControls.filter((g) => {
    g.itemInstanceIds = g.itemInstanceIds.filter(
      (id) => liveIds.has(id) || id.startsWith("new:")
    );
    // 空气地板（airFloor）保留在组里：其碰撞盒对象（Col_AirFloor）是场景对象，
    // Unity 端会被动画组烘焙 reparent + 动画，碰撞随岛移动。
    g.floorInstanceIds = g.floorInstanceIds.filter(
      (id) => liveFloorIds.has(id) || id.startsWith("new:")
    );
    // Drop dangling member-group references (grouping is organizational).
    if (g.memberGroups) {
      const live = new Set([...g.itemInstanceIds, ...g.floorInstanceIds, ...g.objectInstanceIds]);
      for (const mg of g.memberGroups) {
        mg.memberInstanceIds = mg.memberInstanceIds.filter((id) => live.has(id));
      }
      g.memberGroups = g.memberGroups.filter((mg) => mg.memberInstanceIds.length > 0);
    }
    // memberOffsets/memberStatic reference plain scene objects that are not part
    // of S.items/S.floors — keep them untouched (the backend re-captures gaps).
    // 全屏特效组没有成员（宿主是相机 rig / 专用灯，不在物品列表里）：无条件保留。
    if (g.groupKind === "fx") return true;
    return g.itemInstanceIds.length > 0 || g.floorInstanceIds.length > 0 || g.objectInstanceIds.length > 0;
  });
  if (S.activeAnimGroupId && !S.animControls.some((g) => g.id === S.activeAnimGroupId)) {
    S.activeAnimGroupId = null;
    S.activeAnimEventIdx = null;
    S.selectedWaypointId = null;
  }
  for (const g of S.animControls) migrateGroupTimeline(g);
}

/** 旧顺序事件（无 startTime）自动迁移为时间轴绝对时间：startTime[0] =
 *  max(0, delay)，后续按烘焙链规则 nextEventStart 换算，全部 0.1s 对齐。
 *  迁移后 delay 字段保留（旧档兼容），调度一律以 startTime 为准。 */
export function migrateGroupTimeline(group: AnimGroup): boolean {
  if (group.events.every((e) => e.startTime != null)) return false;
  let acc = 0;
  let prevDur = 0;
  let first = true;
  for (const evt of group.events) {
    const start = first
      ? Math.max(0, evt.delay)
      : nextEventStart(acc, prevDur, evt.delay, !!group.waitForFinished);
    evt.startTime = snapTime(start);
    prevDur = eventDuration(evt, group);
    acc = start;
    first = false;
  }
  return true;
}

/** 事件在时间轴上的开始时间（秒，相对组队列启动；未迁移的旧数据按 0 处理）。 */
export function eventStart(evt: AnimGroupEvent): number {
  return snapTime(evt.startTime ?? 0);
}

/** 事件结束时间 = startTime + 时长。 */
export function eventEnd(evt: AnimGroupEvent, group: AnimGroup): number {
  return eventStart(evt) + eventDuration(evt, group);
}

/** 抬起 / 落下事件互相独占时间段（lift 与 drop 之间强制不重叠）；它们与其他
 *  类型事件（移动 / 旋转 / 等待）之间仍可自由重叠并行。 */
function isExclusiveEvt(evt: AnimGroupEvent): boolean {
  return evt.type === "lift" || evt.type === "drop";
}

/** 事件 idx 的禁区区间表 = 其他 lift/drop 事件的区间——仅当本事件也是
 *  lift/drop 时才生效（即只有 lift/drop 彼此之间不允许重叠）。 */
function exclusiveBlockers(group: AnimGroup, idx: number): { s: number; e: number }[] {
  const evt = group.events[idx];
  const out: { s: number; e: number }[] = [];
  if (!isExclusiveEvt(evt)) return out;
  group.events.forEach((o, i) => {
    if (i === idx || !isExclusiveEvt(o)) return;
    out.push({ s: eventStart(o), e: eventEnd(o, group) });
  });
  out.sort((a, b) => a.s - b.s);
  return out;
}

/** 拖动 / 修改开始时间时强制不重叠：start 落入禁区时吸附到最近的可放置边界
 *  （禁区左外沿 / 右外沿 / 0 点）；无处可放则保持原位。 */
function clampExclusiveStart(group: AnimGroup, idx: number, start: number, dur: number): number {
  const blocks = exclusiveBlockers(group, idx);
  start = Math.max(0, start);
  if (!blocks.length) return start;
  const overlaps = (s: number) =>
    blocks.some((b) => s < b.e - 1e-4 && s + dur > b.s + 1e-4);
  if (!overlaps(start)) return start;
  const cands = new Set<number>([0]);
  for (const b of blocks) {
    cands.add(Math.max(0, b.s - dur));
    cands.add(b.e);
  }
  let best: number | null = null;
  for (const c of cands) {
    if (overlaps(c)) continue;
    if (best === null || Math.abs(c - start) < Math.abs(best - start)) best = c;
  }
  return best ?? eventStart(group.events[idx]);
}

/** 右边缘缩放时强制不重叠：结束时间不得超过后方最近禁区的开始时间。 */
function clampExclusiveDuration(group: AnimGroup, idx: number, dur: number): number {
  const start = eventStart(group.events[idx]);
  let maxEnd = Infinity;
  for (const b of exclusiveBlockers(group, idx)) {
    if (b.s >= start - 1e-4) maxEnd = Math.min(maxEnd, b.s);
  }
  if (maxEnd === Infinity) return dur;
  return Math.max(TIMELINE_STEP, Math.min(dur, snapTime(maxEnd - start)));
}

/** 组时间轴总长（不含 startDelay / loopDelay）：最晚事件结束时间。 */
export function groupTimelineDuration(group: AnimGroup): number {
  let end = 0;
  for (const evt of group.events) end = Math.max(end, eventEnd(evt, group));
  return end;
}

function removeMember(group: AnimGroup, instanceId: string): void {
  group.itemInstanceIds = group.itemInstanceIds.filter((id) => id !== instanceId);
  group.floorInstanceIds = group.floorInstanceIds.filter((id) => id !== instanceId);
  group.objectInstanceIds = group.objectInstanceIds.filter((id) => id !== instanceId);
  if (group.memberOffsets)
    group.memberOffsets = group.memberOffsets.filter((o) => o.instanceId !== instanceId);
  if (group.memberStatic)
    group.memberStatic = group.memberStatic.filter((m) => m.instanceId !== instanceId);
  if (group.memberGroups) {
    for (const mg of group.memberGroups) {
      mg.memberInstanceIds = mg.memberInstanceIds.filter((id) => id !== instanceId);
    }
  }
}

// ------------------------------------------------------------------ modes

export function exitAnimMode(): void {
  if (S.animMode === "none") return;
  S.animMode = "none";
  clearAnimSelection();
  updateAnimPickBar();
  renderRightPanel();
  draw();
}

function setAnimMode(mode: AnimMode): void {
  if (S.animMode === mode && S.currentLayer === "anim") {
    renderRightPanel();
    draw();
    return;
  }
  S.animMode = mode;
  if (mode !== "none" && S.currentLayer !== "anim") {
    setLayer("anim");
  }
  if (mode === "members") {
    clearAnimSelection();
  }
  updateAnimPickBar();
  renderRightPanel();
  draw();
}

// ------------------------------------------------------------------ floating pick bar

export function updateAnimPickBar(): void {
  const bar = dom.animPickBar;
  const group = activeGroup();
  const show = S.currentLayer === "anim" && S.animMode === "members" && !!group;
  if (!show) {
    bar.classList.add("hidden");
    return;
  }
  const itemN = S.selectedKeys.size;
  const floorN = S.selectedFloorKeys.size;
  const canAdd = itemN + floorN > 0;
  const tgt = pickTarget(group!);
  const addLabel = tgt ? `＋ 加入「${escHtml(tgt.name)}」` : "＋ 加入分组";
  bar.innerHTML =
    `<span class="mpb-title">🎯 组「${escHtml(group!.displayName)}」· 框选成员</span>` +
    `<span class="mpb-count">已选 物品 <b>${itemN}</b> · 地板 <b>${floorN}</b></span>` +
    `<span class="mpb-actions">` +
    `<button type="button" class="btn-small pick-add" id="mpb-add-members"${canAdd ? "" : " disabled"}>` +
    `${addLabel}${canAdd ? `（${itemN} 物品 · ${floorN} 地板）` : ""}</button>` +
    `<button type="button" class="btn-small" id="mpb-exit">退出模式</button>` +
    `</span>`;
  bar.classList.remove("hidden");
  bar.querySelector("#mpb-add-members")?.addEventListener("click", addSelectedToGroup);
  bar.querySelector("#mpb-exit")?.addEventListener("click", () => exitAnimMode());
}

/** The member group currently targeted by pick additions (null = ungrouped). */
function pickTarget(group: AnimGroup): AnimGroupMemberGroup | null {
  return (group.memberGroups ?? []).find((mg) => mg.id === S.animPickTargetGroupId) ?? null;
}

function addSelectedToGroup(): void {
  const group = activeGroup();
  if (!group || S.selectedKeys.size + S.selectedFloorKeys.size === 0) return;
  pushHistory();
  let addedItems = 0;
  let addedFloors = 0;
  let linkedItems = 0;
  const target = pickTarget(group);
  const targetAdd = (id: string) => {
    if (target && !target.memberInstanceIds.includes(id)) target.memberInstanceIds.push(id);
  };
  const addItem = (it: EditorItem): boolean => {
    if (isBackgroundItem(it) || group.itemInstanceIds.includes(it.instanceId)) return false;
    group.itemInstanceIds.push(it.instanceId);
    targetAdd(it.instanceId);
    return true;
  };
  for (const key of S.selectedKeys) {
    const it = S.items.find((i) => i._editorKey === key);
    if (it && addItem(it)) addedItems++;
  }
  for (const key of S.selectedFloorKeys) {
    const f = S.floors.find((fl) => fl._key === key);
    if (!f || f.surfaceKind === "background" || group.floorInstanceIds.includes(f.instanceId)) continue;
    // 空气地板（仅碰撞盒）允许入组：Unity 端其 Col_AirFloor 碰撞盒对象会被
    // 烘焙 reparent + 动画，行走碰撞随组移动。
    group.floorInstanceIds.push(f.instanceId);
    targetAdd(f.instanceId);
    addedFloors++;
    // 空气地板不做整岛连带：隐形桥常与两侧静态岛边缘重叠（嵌入碰撞用），
    // 连带会把静态岛的地砖/崖错误拉进动画组。
    if (f.airFloor) continue;
    // 整岛语义：地板入组时自动连带其矩形上方的物品（工作站/装饰/地砖）——
    // 只移地板而上面的东西留在原地几乎必然不是想要的结果。
    const hw = (f._wCells * CELL) / 2 + 0.05;
    const hd = (f._dCells * CELL) / 2 + 0.05;
    for (const it of S.items) {
      if (Math.abs(it._wx - f._wx) > hw || Math.abs(it._wz - f._wz) > hd) continue;
      if (addItem(it)) linkedItems++;
    }
  }
  S.dirty = true;
  clearAnimSelection();
  updateAnimPickBar();
  renderRightPanel();
  draw();
  setStatus(
    `已加入 ${addedItems} 个物品 · ${addedFloors} 块地板到组「${group.displayName}」` +
      (linkedItems > 0 ? `（自动连带地板上方 ${linkedItems} 个物品）` : "") +
      (target ? `（成员组「${target.name}」）` : ""),
    addedItems + addedFloors > 0
  );
}

/** Remove currently selected members (items + floors) from the active group. */
export function removeSelectedMembers(): void {
  const group = activeGroup();
  if (!group) return;
  const itemIds = [...S.selectedKeys]
    .map((k) => S.items.find((i) => i._editorKey === k)?.instanceId)
    .filter((x): x is string => !!x);
  const floorKeys = [...S.selectedFloorKeys];
  if (!itemIds.length && !floorKeys.length) return;
  pushHistory();
  let n = 0;
  for (const id of itemIds) {
    if (group.itemInstanceIds.includes(id)) {
      removeMember(group, id);
      n++;
    }
  }
  for (const key of floorKeys) {
    const f = S.floors.find((fl) => fl._key === key);
    if (f && group.floorInstanceIds.includes(f.instanceId)) {
      removeMember(group, f.instanceId);
      n++;
    }
  }
  if (n) S.dirty = true;
  clearAnimSelection();
  updateAnimPickBar();
  renderRightPanel();
  draw();
  if (n > 0) setStatus(`已从组「${group.displayName}」移出 ${n} 个成员`);
}

// ------------------------------------------------------------------ canvas overlay

export function drawAnimControlOverlay(): void {
  const active = activeGroup();
  const groups = active
    ? [active]
    : S.animControls.filter((g) => groupVisibleInLayer(g));

  for (const group of groups) {
    const isActive = group.id === S.activeAnimGroupId;
    const alpha = isActive ? 1.0 : 0.4;
    const col = groupColor(group.id);

    const wpMap = new Map(group.waypoints.map((w) => [w.id, w]));

    // Draw route lines for move events
    for (const evt of group.events) {
      if (evt.type !== "move" || !evt.waypointIds || evt.waypointIds.length < 2) continue;
      const pts: { x: number; z: number }[] = [];
      for (const wpId of evt.waypointIds) {
        const wp = wpMap.get(wpId);
        if (wp) pts.push({ x: wp.x, z: wp.z });
      }
      if (pts.length < 2) continue;

      dom.ctx.save();
      dom.ctx.globalAlpha = alpha;
      dom.ctx.beginPath();
      const p0 = worldToCanvas(pts[0].x, pts[0].z);
      dom.ctx.moveTo(p0.x, p0.y);
      for (let i = 1; i < pts.length; i++) {
        const pi = worldToCanvas(pts[i].x, pts[i].z);
        dom.ctx.lineTo(pi.x, pi.y);
      }
      dom.ctx.strokeStyle = isActive ? colorA(col, 0.85) : colorA(col, 0.45);
      dom.ctx.lineWidth = isActive ? 2 : 1.5;
      dom.ctx.setLineDash([6, 4]);
      dom.ctx.stroke();
      dom.ctx.setLineDash([]);

      // Direction arrows
      for (let i = 0; i < pts.length - 1; i++) {
        const a = worldToCanvas(pts[i].x, pts[i].z);
        const b = worldToCanvas(pts[i + 1].x, pts[i + 1].z);
        const mx = (a.x + b.x) / 2;
        const my = (a.y + b.y) / 2;
        const angle = Math.atan2(b.y - a.y, b.x - a.x);
        const size = 5;
        dom.ctx.beginPath();
        dom.ctx.moveTo(mx + size * Math.cos(angle), my + size * Math.sin(angle));
        dom.ctx.lineTo(mx + size * Math.cos(angle + 2.5), my + size * Math.sin(angle + 2.5));
        dom.ctx.lineTo(mx + size * Math.cos(angle - 2.5), my + size * Math.sin(angle - 2.5));
        dom.ctx.closePath();
        dom.ctx.fillStyle = isActive ? colorA(col, 0.7) : colorA(col, 0.4);
        dom.ctx.fill();
      }

      // Loop visualisation: event-level loop (scrolling, closed arc) vs
      // group-level loop (queue flash-back, dashed return arrow).
      if (isActive) {
        const last = pts[pts.length - 1];
        const first = pts[0];
        const pa = worldToCanvas(last.x, last.z);
        const pb = worldToCanvas(first.x, first.z);
        if (evt.loop) {
          // Closed arc back to the start.
          dom.ctx.strokeStyle = "rgba(216,120,60,0.9)";
          dom.ctx.lineWidth = 2;
          dom.ctx.beginPath();
          const midX = (pa.x + pb.x) / 2;
          const midY = (pa.y + pb.y) / 2;
          const dx = pb.x - pa.x;
          const dy = pb.y - pa.y;
          const len = Math.max(12, Math.hypot(dx, dy));
          const nx = -dy / len;
          const ny = dx / len;
          dom.ctx.moveTo(pa.x + nx * 10, pa.y + ny * 10);
          dom.ctx.quadraticCurveTo(
            midX + nx * 22, midY + ny * 22,
            pb.x + nx * 10, pb.y + ny * 10
          );
          dom.ctx.stroke();
          // Arrow head on the arc end
          const a2 = Math.atan2(ny, nx) + Math.PI / 2;
          dom.ctx.beginPath();
          dom.ctx.moveTo(pb.x + nx * 8, pb.y + ny * 8);
          dom.ctx.lineTo(pb.x + nx * 8 + 8 * Math.cos(a2 + 2.6), pb.y + ny * 8 + 8 * Math.sin(a2 + 2.6));
          dom.ctx.lineTo(pb.x + nx * 8 + 8 * Math.cos(a2 - 2.6), pb.y + ny * 8 + 8 * Math.sin(a2 - 2.6));
          dom.ctx.closePath();
          dom.ctx.fillStyle = "rgba(216,120,60,0.9)";
          dom.ctx.fill();
        } else if (group.loop) {
          dom.ctx.strokeStyle = "rgba(216,120,60,0.7)";
          dom.ctx.lineWidth = 1.5;
          dom.ctx.setLineDash([3, 4]);
          dom.ctx.beginPath();
          dom.ctx.moveTo(pa.x, pa.y);
          dom.ctx.lineTo(pb.x, pb.y);
          dom.ctx.stroke();
          dom.ctx.setLineDash([]);
          const angle = Math.atan2(pb.y - pa.y, pb.x - pa.x);
          dom.ctx.beginPath();
          dom.ctx.moveTo(pb.x, pb.y);
          dom.ctx.lineTo(pb.x - 8 * Math.cos(angle - 0.5), pb.y - 8 * Math.sin(angle - 0.5));
          dom.ctx.lineTo(pb.x - 8 * Math.cos(angle + 0.5), pb.y - 8 * Math.sin(angle + 0.5));
          dom.ctx.closePath();
          dom.ctx.fillStyle = "rgba(216,120,60,0.7)";
          dom.ctx.fill();
        }
        // Loop badge near the route end.
        if (evt.loop || group.loop) {
          const badge = evt.loop ? "⟲ 无限循环" : "⟲ 循环闪回";
          const bx = pa.x + 8;
          const by = pa.y - 10;
          dom.ctx.font = "10px sans-serif";
          const tw = dom.ctx.measureText(badge).width;
          dom.ctx.fillStyle = "rgba(20,20,24,0.8)";
          dom.ctx.fillRect(bx, by - 11, tw + 8, 15);
          dom.ctx.fillStyle = "#e8a05c";
          dom.ctx.fillText(badge, bx + 4, by);
        }
      }
      dom.ctx.restore();
    }

    // Draw waypoints: every point shows its stable global number badge.
    for (const wp of group.waypoints) {
      const p = worldToCanvas(wp.x, wp.z);
      const selected = wp.id === S.selectedWaypointId;
      // Every waypoint carries its stable global number (#N in this group) so
      // the canvas matches the route/pool lists and stacked points are tellable.
      const num = group.waypoints.indexOf(wp) + 1;
      dom.ctx.save();
      dom.ctx.globalAlpha = alpha;
      dom.ctx.beginPath();
      dom.ctx.arc(p.x, p.y, selected ? 10 : 8, 0, Math.PI * 2);
      dom.ctx.fillStyle = selected ? "#ffcc00" : waypointColor(wp.id);
      dom.ctx.fill();
      if (selected) {
        dom.ctx.strokeStyle = "#fff";
        dom.ctx.lineWidth = 2;
        dom.ctx.stroke();
      } else if (isActive) {
        dom.ctx.strokeStyle = "rgba(0,0,0,0.4)";
        dom.ctx.lineWidth = 1;
        dom.ctx.stroke();
      }
      dom.ctx.fillStyle = "#fff";
      dom.ctx.font = "bold 10px sans-serif";
      dom.ctx.textAlign = "center";
      dom.ctx.textBaseline = "middle";
      dom.ctx.fillText(String(num), p.x, p.y + 0.5);
      dom.ctx.textAlign = "start";
      dom.ctx.textBaseline = "alphabetic";
      // Coordinates only on the selected point.
      if (selected) {
        dom.ctx.font = "10px monospace";
        dom.ctx.fillStyle = "#fff";
        dom.ctx.strokeStyle = "rgba(0,0,0,0.7)";
        dom.ctx.lineWidth = 3;
        const text = `(${wp.x.toFixed(2)}, ${wp.z.toFixed(2)})`;
        dom.ctx.strokeText(text, p.x + 12, p.y - 12);
        dom.ctx.fillText(text, p.x + 12, p.y - 12);
      }
      dom.ctx.restore();
    }

    // Member markers: show which items each point/route controls.
    if (isActive) {
      drawMemberMarkers(group);
    }
  }

  if (S.currentLayer === "anim") drawAnimLegend();
  const act = activeGroup();
  if (act && S.animPreview && S.animPreview.groupId === act.id) {
    drawPreviewOverlay(act);
  }
}

function firstRouteFirstWp(group: AnimGroup): { x: number; z: number } | null {
  for (const evt of group.events) {
    if (evt.type !== "move" || !evt.waypointIds || !evt.waypointIds.length) continue;
    for (const wpId of evt.waypointIds) {
      const wp = group.waypoints.find((w) => w.id === wpId);
      if (wp) return { x: wp.x, z: wp.z };
    }
  }
  return null;
}

function routePosAt(evt: AnimGroupEvent, group: AnimGroup, tSec: number): { x: number; z: number } | null {
  if (!evt.waypointIds || !evt.waypointIds.length) return null;
  const wps = evt.waypointIds
    .map((id) => group.waypoints.find((w) => w.id === id))
    .filter(Boolean) as AnimGroupWaypoint[];
  if (!wps.length) return null;
  if (wps.length === 1) return { x: wps[0].x, z: wps[0].z };
  const times = wps.map((w, i) => (w.hasTime ? w.t! : i * (evt.intervalSeconds ?? 2)));
  const L = times[times.length - 1];
  let t = L > 0 ? tSec % L : 0;
  if (t < 0) t += L;
  if (t <= times[0]) return { x: wps[0].x, z: wps[0].z };
  for (let i = 0; i < wps.length - 1; i++) {
    const t0 = times[i];
    const t1 = times[i + 1];
    if (t <= t1) {
      const k = t1 > t0 ? (t - t0) / (t1 - t0) : 0;
      return {
        x: wps[i].x + (wps[i + 1].x - wps[i].x) * k,
        z: wps[i].z + (wps[i + 1].z - wps[i].z) * k,
      };
    }
  }
  return { x: wps[wps.length - 1].x, z: wps[wps.length - 1].z };
}

/** Role badge text for a member: 静止 / 相位 / 跟随 / 移动. */
function memberRoleText(group: AnimGroup, id: string): string {
  if (group.memberStatic?.some((m) => m.instanceId === id)) return "静止";
  const off = group.memberOffsets?.find((o) => o.instanceId === id);
  if (off?.followWaypointId) return "跟随";
  // A route-riding member serializes t:0 (C# float default) — only a *nonzero*
  // phase shift makes it "相位"; t==0 means it rides the route in step ("移动").
  if (off?.t != null && off.t !== 0) return "相位";
  return "移动";
}

function drawMemberMarkers(group: AnimGroup): void {
  const firstWp = firstRouteFirstWp(group);
  const firstEvt = group.events.find((e) => e.type === "move");
  const offById = new Map((group.memberOffsets ?? []).map((o) => [o.instanceId, o]));
  const col = groupColor(group.id);
  const labelOf = (id: string): string | null => {
    const it = S.items.find((i) => i.instanceId === id);
    if (it) return itemLabel(it);
    const f = S.floors.find((fl) => fl.instanceId === id);
    if (f) return `地板:${f.displayName}`;
    const off = offById.get(id);
    if (off?.displayName) return off.displayName;
    return null;
  };
  const posOf = (id: string): { x: number; z: number } | null => {
    const it = S.items.find((i) => i.instanceId === id);
    if (it) return { x: it._wx, z: it._wz };
    const f = S.floors.find((fl) => fl.instanceId === id);
    if (f) return { x: f._wx, z: f._wz };
    const off = offById.get(id);
    if (!off) return null;
    // Follow members: pinned at the followed waypoint (+ offset).
    if (off.followWaypointId) {
      const wp = group.waypoints.find((w) => w.id === off.followWaypointId);
      if (wp) return { x: wp.x + (off.x ?? 0), z: wp.z + (off.z ?? 0) };
    }
    if (off.t) {
      if (firstEvt && firstWp) return routePosAt(firstEvt, group, off.t);
      return firstWp;
    }
    if (firstWp) return { x: firstWp.x + off.x, z: firstWp.z + off.z };
    return null;
  };

  dom.ctx.save();
  const ids = [...group.itemInstanceIds, ...group.floorInstanceIds, ...group.objectInstanceIds];
  for (const id of ids) {
    const label = labelOf(id);
    const pos = posOf(id);
    if (!pos) continue;
    const p = worldToCanvas(pos.x, pos.z);
    const isStatic = group.memberStatic?.some((m) => m.instanceId === id);
    const off = offById.get(id);
    const isPhase = !isStatic && off?.t != null && off.t !== 0;
    const isFollow = !isStatic && !!off?.followWaypointId;
    // Role shapes: 移动 = square (group color), 静止 = circle (gray),
    // 相位 = diamond (purple), 跟随 = triangle (teal, pinned to a waypoint).
    dom.ctx.beginPath();
    if (isStatic) {
      dom.ctx.arc(p.x, p.y, 5, 0, Math.PI * 2);
      dom.ctx.fillStyle = "rgba(130,130,140,0.75)";
      dom.ctx.fill();
    } else if (isPhase) {
      dom.ctx.moveTo(p.x, p.y - 6);
      dom.ctx.lineTo(p.x + 6, p.y);
      dom.ctx.lineTo(p.x, p.y + 6);
      dom.ctx.lineTo(p.x - 6, p.y);
      dom.ctx.closePath();
      dom.ctx.fillStyle = "rgba(160,100,220,0.85)";
      dom.ctx.fill();
    } else if (isFollow) {
      dom.ctx.moveTo(p.x, p.y - 6);
      dom.ctx.lineTo(p.x + 6, p.y + 5);
      dom.ctx.lineTo(p.x - 6, p.y + 5);
      dom.ctx.closePath();
      dom.ctx.fillStyle = "rgba(79,214,192,0.9)";
      dom.ctx.fill();
    } else {
      dom.ctx.fillStyle = colorA(col, 0.85);
      dom.ctx.fillRect(p.x - 5, p.y - 5, 10, 10);
    }
    dom.ctx.strokeStyle = "rgba(255,255,255,0.8)";
    dom.ctx.lineWidth = 1;
    dom.ctx.stroke();
    if (label) {
      dom.ctx.font = "11px sans-serif";
      const tw = dom.ctx.measureText(label).width;
      dom.ctx.fillStyle = "rgba(20,20,24,0.75)";
      dom.ctx.fillRect(p.x + 7, p.y - 5, tw + 5, 14);
      dom.ctx.fillStyle = "#e8ffe8";
      dom.ctx.fillText(label, p.x + 9.5, p.y + 5);
    }
  }
  dom.ctx.restore();
}

/** Legend strip (bottom-left) explaining the anim-layer symbols. */
function drawAnimLegend(): void {
  const legend: [string, string][] = [
    ["■ 移动", "#2f9e6e"],
    ["● 静止", "#8b93a3"],
    ["◆ 相位", "#a06bd6"],
    ["▲ 跟随路点", "#4fd6c0"],
    ["● 路点", "#3d6bf3"],
    ["─→ 路线", "#d0d0d0"],
  ];
  const ctx = dom.ctx;
  ctx.save();
  ctx.font = "11px sans-serif";
  const pad = 8;
  const rowH = 16;
  const w = legend.reduce((m, [t]) => Math.max(m, ctx.measureText(t).width), 0) + pad * 2;
  const h = rowH * legend.length + pad * 1.5;
  const x = 10;
  const y = dom.canvas.height - h - 10;
  ctx.fillStyle = "rgba(16,18,24,0.8)";
  ctx.fillRect(x, y, w, h);
  legend.forEach(([t, c], i) => {
    ctx.fillStyle = c;
    ctx.fillText(t, x + pad, y + pad + 3 + i * rowH);
  });
  ctx.restore();
}

// ------------------------------------------------------------------ route preview (front-end simulation)

let previewRAF = 0;
let previewLastTick = 0;

/** Drop clip-imported key times so intervalSeconds / per-segment edits take effect. */
function clearImportedRouteTiming(group: AnimGroup, evt: AnimGroupEvent): void {
  for (const wpId of evt.waypointIds ?? []) {
    const wp = group.waypoints.find((w) => w.id === wpId);
    if (!wp) continue;
    delete wp.hasTime;
    delete wp.t;
    delete wp.segmentSeconds;
  }
}

function applyEventInterval(group: AnimGroup, evtIdx: number, seconds: number): void {
  const evt = group.events[evtIdx];
  const v = Math.max(0.1, seconds);
  const prev = evt.intervalSeconds ?? 2;
  if (Math.abs(v - prev) < 0.001) return;
  pushHistory();
  evt.intervalSeconds = v;
  clearImportedRouteTiming(group, evt);
  S.dirty = true;
}

/** Keyframe timeline of an event route (arrival keys + dwell keys from waits).
 *  Imported routes keep their original key times unless any timing is edited
 *  (wait / segmentSeconds), in which case the computed timeline takes over. */
/** routeTimeline 结果缓存：预览播放 / scrub 每帧每成员都会取一次，按事件对象 +
 *  解析出的路点引用数组做 identity 校验，命中则复用 times/pts（避免每帧重建数组）。 */
const routeTimelineCache = new WeakMap<
  AnimGroupEvent,
  { sig: string; times: number[]; pts: { x: number; z: number }[] }
>();

function routeTimeline(evt: AnimGroupEvent, group: AnimGroup): { times: number[]; pts: { x: number; z: number }[] } {
  const wps = (evt.waypointIds ?? [])
    .map((id) => group.waypoints.find((w) => w.id === id))
    .filter(Boolean) as AnimGroupWaypoint[];
  if (!wps.length) return { times: [], pts: [] };
  const interval = evt.intervalSeconds ?? 2;
  // 签名覆盖所有影响时间线的字段（路点会原地改坐标 / 停留 / 段时长）。
  let sig = `${interval}`;
  for (const w of wps) {
    sig += `|${w.id}:${w.x},${w.z},${w.wait ?? 0},${w.segmentSeconds ?? 0},${w.hasTime ? 1 : 0},${w.t ?? 0}`;
  }
  const hit = routeTimelineCache.get(evt);
  if (hit && hit.sig === sig) return { times: hit.times, pts: hit.pts };
  let out: { times: number[]; pts: { x: number; z: number }[] };
  if (wps.length === 1) {
    out = { times: [0], pts: [{ x: wps[0].x, z: wps[0].z }] };
  } else {
    const anyEdit = wps.some((w) => (w.wait ?? 0) > 0 || (w.segmentSeconds ?? 0) > 0);
    if (!anyEdit && wps.some((w) => w.hasTime)) {
      out = {
        times: wps.map((w) => w.t ?? 0),
        pts: wps.map((w) => ({ x: w.x, z: w.z })),
      };
    } else {
      const times: number[] = [];
      const pts: { x: number; z: number }[] = [];
      let t = 0;
      for (let i = 0; i < wps.length; i++) {
        const p = { x: wps[i].x, z: wps[i].z };
        times.push(t);
        pts.push(p);
        const wait = wps[i].wait ?? 0;
        if (wait > 0) {
          times.push(t + wait);
          pts.push(p);
        }
        if (i < wps.length - 1) t += (wps[i].segmentSeconds ?? interval) + wait;
      }
      out = { times, pts };
    }
  }
  routeTimelineCache.set(evt, { sig, times: out.times, pts: out.pts });
  return out;
}

function clipDuration(evt: AnimGroupEvent, group: AnimGroup): number {
  const { times } = routeTimeline(evt, group);
  if (!times.length) return 0;
  return times[times.length - 1];
}

function clipPos(evt: AnimGroupEvent, group: AnimGroup, tc: number): { x: number; z: number } | null {
  const { times, pts } = routeTimeline(evt, group);
  if (!pts.length) return null;
  if (pts.length === 1) return { x: pts[0].x, z: pts[0].z };
  const L = times[times.length - 1];
  let t: number;
  if (evt.pingpong) {
    const period = 2 * L;
    let m = period > 0 ? tc % period : 0;
    if (m < 0) m += period;
    t = m <= L ? m : period - m;
  } else if (evt.loop) {
    t = L > 0 ? tc % L : 0;
  } else {
    t = Math.min(tc, L);
  }
  if (t <= times[0]) return { x: pts[0].x, z: pts[0].z };
  for (let i = 0; i < pts.length - 1; i++) {
    const t0 = times[i];
    const t1 = times[i + 1];
    if (t <= t1) {
      const k = t1 > t0 ? (t - t0) / (t1 - t0) : 0;
      return {
        x: pts[i].x + (pts[i + 1].x - pts[i].x) * k,
        z: pts[i].z + (pts[i + 1].z - pts[i].z) * k,
      };
    }
  }
  return { x: pts[pts.length - 1].x, z: pts[pts.length - 1].z };
}

/** Vertical position of a move event at clip time tc (integrated lift profile). */
function clipY(evt: AnimGroupEvent, group: AnimGroup, tc: number): number {
  const h = evt.liftHeight ?? 0;
  if (h <= 0) return 0;
  const up = Math.max(0, evt.liftSeconds ?? 0);
  const down = Math.max(0, evt.dropSeconds ?? 0);
  const total = Math.max(clipDuration(evt, group), up + down);
  if (up > 0 && tc < up) return h * (tc / up);
  if (down > 0 && tc > total - down) {
    const k = Math.min(1, (tc - (total - down)) / down);
    return h * (1 - k);
  }
  return h;
}

/** Duration of one event's clip (wait = duration 秒常量 clip，与烘焙一致；
 *  lift/drop use their liftSeconds; rotate = rotateSeconds; move = route length;
 *  shake/flash = duration（一次抖动 / 闪光的时长）。 */
function eventDuration(evt: AnimGroupEvent, group: AnimGroup): number {
  if (evt.type === "move") return clipDuration(evt, group);
  if (evt.type === "lift" || evt.type === "drop") return Math.max(0.1, evt.liftSeconds ?? 1);
  if (evt.type === "rotate") return Math.max(0.1, evt.rotateSeconds ?? 2);
  if (evt.type === "shake") return Math.max(0.1, evt.duration ?? 2);
  if (evt.type === "flash") return Math.max(0.1, evt.duration ?? 0.8);
  return Math.max(0.1, evt.duration ?? 1);
}

/** Start time of the next event: the bake chains clips (delay <= 0 → direct
 *  transition at the previous clip's end), and waitForFinished paces the trigger
 *  on the previous clip's finished event — so the gap is never shorter than the
 *  previous clip's duration. */
function nextEventStart(prevStart: number, prevDur: number, delay: number, waitForFinished: boolean): number {
  if (waitForFinished) return prevStart + prevDur + Math.max(0, delay);
  return prevStart + Math.max(prevDur, Math.max(0, delay));
}

/** 拖时间轴块右边缘改时长：wait→duration，rotate→rotateSeconds，lift/drop→
 *  liftSeconds，move→按「每步间隔 × (路点数-1) + 停留」推算总时长：
 *  无自定义段时长时直接调 intervalSeconds；有自定义段时长（时间曲线编辑过）
 *  则按比例缩放各段 segmentSeconds 以保留节奏（方案 A）。停留 wait 不动。 */
function resizeEventDuration(group: AnimGroup, evt: AnimGroupEvent, newDur: number): void {
  newDur = snapTime(Math.max(TIMELINE_STEP, newDur));
  if (evt.type === "wait") {
    evt.duration = newDur;
    return;
  }
  if (evt.type === "shake" || evt.type === "flash") {
    evt.duration = newDur;
    return;
  }
  if (evt.type === "rotate") {
    evt.rotateSeconds = newDur;
    return;
  }
  if (evt.type === "lift" || evt.type === "drop") {
    evt.liftSeconds = newDur;
    return;
  }
  if (evt.type !== "move") return;
  const wps = (evt.waypointIds ?? [])
    .map((id) => group.waypoints.find((w) => w.id === id))
    .filter(Boolean) as AnimGroupWaypoint[];
  if (wps.length < 2) {
    // 路线不足 2 个路点：时长是名义值（每步间隔），直接写入 intervalSeconds。
    evt.intervalSeconds = snapTime(Math.max(TIMELINE_STEP, newDur));
    return;
  }
  const segCount = wps.length - 1;
  const waitTotal = wps.reduce((s, w) => s + (w.wait ?? 0), 0);
  const newMovable = newDur - waitTotal;
  // 目标时长 ≤ 总停留（或无可缩放移动段，如全程停留的导入路线）：缩 wait。
  const oldDur0 = clipDuration(evt, group);
  if (newMovable <= 0.001 || oldDur0 - waitTotal <= 0.001) {
    if (waitTotal > 0.001) {
      const f = Math.max(TIMELINE_STEP, newDur) / waitTotal;
      for (const w of wps) if ((w.wait ?? 0) > 0) w.wait = (w.wait ?? 0) * f;
    }
    return;
  }
  const hasCustomSegs = wps.some((w) => (w.segmentSeconds ?? 0) > 0);
  if (!hasCustomSegs) {
    evt.intervalSeconds = snapTime(Math.max(TIMELINE_STEP, newMovable / segCount));
    // 导入路线保留的原始关键时间（hasTime/t）优先于 intervalSeconds——
    // 拖边改时长与手改「每步间隔」同语义：清掉导入时间，让推算时间线生效。
    clearImportedRouteTiming(group, evt);
    return;
  }
  const oldDur = clipDuration(evt, group);
  const movable = oldDur - waitTotal;
  if (movable <= 0.001) return;
  const f = newMovable / movable;
  const interval = evt.intervalSeconds ?? 2;
  for (let i = 0; i < wps.length - 1; i++) {
    wps[i].segmentSeconds = (wps[i].segmentSeconds ?? interval) * f;
  }
}

/** 时间轴块显示用时长：移动事件路线不足 2 个路点时 clipDuration=0，
 *  用「每步间隔」作名义时长（加了第 2 个路点后即为真实时长）。
 *  仅用于 UI 显示 / 拖边；预览与烘焙仍按 eventDuration（0 = 不动）。 */
function eventDisplayDuration(group: AnimGroup, evt: AnimGroupEvent): number {
  const d = eventDuration(evt, group);
  if (evt.type === "move" && d <= 0.001) {
    return Math.max(TIMELINE_STEP, evt.intervalSeconds ?? 2);
  }
  return Math.max(TIMELINE_STEP, d);
}

/** 时间轴泳道分配：按 startTime 排序后贪心装入第一条不重叠的泳道。 */
interface TimelineItem {
  evt: AnimGroupEvent;
  idx: number;
  start: number;
  end: number;
  lane: number;
}

function timelineLanes(group: AnimGroup): TimelineItem[] {
  const items: TimelineItem[] = group.events
    .map((evt, idx) => {
      const start = eventStart(evt);
      return {
        evt,
        idx,
        start,
        end: start + eventDisplayDuration(group, evt),
        lane: 0,
      };
    })
    .sort((a, b) => a.start - b.start || a.idx - b.idx);
  const laneEnds: number[] = [];
  for (const it of items) {
    // 0.01s 容差：吸收时长累加的浮点残渣（如 3.4000000000000004 ≈ 3.4 相接）。
    let lane = laneEnds.findIndex((e) => e <= it.start + 0.01);
    if (lane < 0) {
      lane = laneEnds.length;
      laneEnds.push(0);
    }
    it.lane = lane;
    laneEnds[lane] = it.end;
  }
  return items;
}

/** Total simulated duration of one full sequence (for the preview progress bar):
 *  startDelay + 时间轴最晚事件结束时间（+ loopDelay 当整组循环）。 */
function previewDuration(group: AnimGroup): number {
  let acc = (group.startDelay ?? 0) + groupTimelineDuration(group);
  if (group.loop) acc += group.loopDelay ?? 0;
  return acc;
}

/** 旋转事件在 clip 时间 tc 的角度增量（度）：非循环播完停在满角度，
 *  循环按周期包裹（same rule as the bake 的自转移重放）。 */
function rotateProgress(evt: AnimGroupEvent, group: AnimGroup, tc: number): number {
  const deg = evt.rotateDegrees ?? 180;
  const dir = evt.rotateDirection === "ccw" ? -1 : 1;
  const dur = eventDuration(evt, group);
  if (tc < 0 || dur <= 0) return 0;
  const k = evt.loop ? (tc % dur) / dur : Math.min(1, tc / dur);
  return dir * deg * k;
}

/** Member's authored local Y (decor offsets below ground, etc.). */
function memberBaseY(id: string): number {
  const it = S.items.find((i) => i.instanceId === id);
  if (it) return it.localPosition.y;
  const f = S.floors.find((fl) => fl.instanceId === id);
  if (f) return f.localPosition.y;
  return 0;
}

/** Preview Y：移动保持成员自身高度；lift/drop/fly 一律为相对 Δy（叠加在成员
 *  自身基准高度上，与烘焙的 per-member Δy 语义一致）。 */
function memberPreviewY(id: string, y: number, mode: "base" | "relative"): number {
  if (mode === "base") return memberBaseY(id);
  return memberBaseY(id) + y;
}

/** Simulated positions (with optional height) of every member and the route head.
 *  Move events after a lift fly at the inherited height (same rule as the bake). */
/** Simulated pose (position + optional height + rotation delta) of every member
 *  and the route head. rotY = 旋转动画产生的 Y 轴角度增量（度，叠加到
 *  成员自身 localRotationY 上预览）。
 *  Move events after a lift fly at the inherited height (same rule as the bake). */
export type PreviewPose = { x: number; z: number; y?: number; rotY?: number };

function previewPositions(group: AnimGroup, t: number): Map<string, PreviewPose> {
  const out = new Map<string, PreviewPose>();
  const staticIds = new Set((group.memberStatic ?? []).map((m) => m.instanceId));
  const offById = new Map((group.memberOffsets ?? []).map((o) => [o.instanceId, o]));
  const memberIds = [...group.itemInstanceIds, ...group.floorInstanceIds, ...group.objectInstanceIds];
  const firstMove = group.events.find((e) => e.type === "move");
  const baseWp =
    firstMove?.waypointIds?.map((id) => group.waypoints.find((w) => w.id === id)).filter(Boolean)[0] ??
    group.waypoints[0];
  const basePos = baseWp ? { x: baseWp.x, z: baseWp.z } : { x: 0, z: 0 };

  const place = (id: string, p: { x: number; z: number } | null | undefined, yMode: "base" | "relative" = "base", y = 0) => {
    if (!p) return;
    const off = offById.get(id);
    out.set(id, {
      x: p.x + (off?.x ?? 0),
      z: p.z + (off?.z ?? 0),
      y: memberPreviewY(id, y, yMode),
    });
  };

  /** Hold every non-static member (+ route head) at the given position. */
  const holdAll = (p: { x: number; z: number }, yMode: "base" | "relative" = "base", y = 0) => {
    out.set("__head__", { ...p, y: yMode === "base" ? 0 : y });
    for (const id of memberIds) {
      if (staticIds.has(id)) continue;
      const f = followPosOf(id);
      if (f) out.set(id, { x: f.x, z: f.z, y: memberPreviewY(id, y, yMode) });
      else place(id, p, yMode, y);
    }
  };

  /** Members bound to a specific waypoint hold there (+ their offset) the whole
   *  sequence instead of riding the route ("跟随路点"). */
  const followPosOf = (id: string): { x: number; z: number } | null => {
    const off = offById.get(id);
    const fw = off?.followWaypointId;
    if (!fw) return null;
    const wp = group.waypoints.find((w) => w.id === fw);
    if (!wp) return null;
    return { x: wp.x + (off?.x ?? 0), z: wp.z + (off?.z ?? 0) };
  };

  // Group-level loop: wrap the playback time over the whole sequence so it
  // repeats instead of freezing at the end.
  const total = previewDuration(group);
  if (group.loop && total > 0) t = t % total;

  const startOff = group.startDelay ?? 0;
  let seqLiftY = 0;
  /** Route end of the last move event — lift/drop/wait hold here (not basePos),
   *  so event hand-offs are seamless (no flash-back between events). */
  let lastPos: { x: number; z: number } | null = null;
  // 时间轴模型：事件按 startTime 绝对调度（same rule as the bake 的 ΔstartTime
  // 队列）。XZ/Y 由最晚开始的已生效非旋转事件决定（并行重叠时后者优先，
  // 时间轴 UI 会对移动类重叠给出警告）；旋转事件独立叠加到 rotY。
  let cur: AnimGroupEvent | null = null;
  let tc = 0;
  let rotY = 0;
  let hasRotate = false;
  const sorted = [...group.events].sort((a, b) => eventStart(a) - eventStart(b));
  for (const evt of sorted) {
    const start = startOff + eventStart(evt);
    if (t < start) break;
    if (evt.type === "rotate") {
      hasRotate = true;
      rotY += rotateProgress(evt, group, t - start);
      continue;
    }
    // 全屏特效事件不动成员位姿（包络由 previewFxState 单独计算）。
    if (evt.type === "shake" || evt.type === "flash") continue;
    if (cur !== null) {
      // Fold the state of the event we just fully passed.
      // 高度语义 = 相对位移 Δy（与烘焙一致）：lift 在已抬升高度上继续叠加；
      // drop 默认落回原高度（Δ 清零），yTo>0 时只下落指定量（可部分下落）。
      if (cur.type === "lift") seqLiftY += cur.yTo || 1;
      else if (cur.type === "drop") seqLiftY = Math.max(0, seqLiftY - (cur.yTo || seqLiftY));
      else if (cur.type === "move") {
        const L = clipDuration(cur, group);
        if (L > 0) {
          const end = clipPos(cur, group, L);
          if (end) lastPos = end;
        }
      }
    }
    cur = evt;
    tc = t - start;
  }

  if (cur === null) {
    // Before the first event's start: members hold at the base position.
    holdAll(basePos);
  } else if (cur.type === "wait") {
    holdAll(lastPos ?? basePos);
  } else if (cur.type === "lift" || cur.type === "drop") {
    // Pure-Y event（相对 Δy，可叠加）：成员保持当前 XZ，从已抬升高度继续
    // 上升 / 下降 —— 连续两次抬起 = 高度累加（与烘焙的 accum 串接一致）。
    const secs = Math.max(0.05, cur.liftSeconds ?? 1);
    const k = Math.min(1, Math.max(0, tc / secs));
    const before = seqLiftY;
    const delta = cur.type === "lift" ? (cur.yTo || 1) : (cur.yTo || before);
    const yTarget = cur.type === "drop" ? Math.max(0, before - delta) : before + delta;
    const y = before + (yTarget - before) * k;
    holdAll(lastPos ?? basePos, "relative", y);
    seqLiftY = yTarget;
  } else {
    const L = clipDuration(cur, group);
    if (L <= 0) {
      holdAll(lastPos ?? basePos);
    } else {
      const p = clipPos(cur, group, tc);
      let yMode: "base" | "relative" = "base";
      let flyY = 0;
      if ((cur.liftHeight ?? 0) > 0) {
        yMode = "relative";
        flyY = clipY(cur, group, tc);
      } else if (seqLiftY > 0) {
        // 抬起后的半空移动：Δy 叠加在各成员自身基准高度上（与烘焙一致）。
        yMode = "relative";
        flyY = seqLiftY;
      }
      if (p) {
        out.set("__head__", { ...p, y: yMode === "base" ? 0 : flyY });
        const end = clipPos(cur, group, L);
        if (end) lastPos = end;
      }
      for (const id of memberIds) {
        if (staticIds.has(id)) continue;
        const off = offById.get(id);
        const f = followPosOf(id);
        if (f) {
          out.set(id, { x: f.x, z: f.z, y: memberPreviewY(id, flyY, yMode) });
          continue;
        }
        const tc2 = off?.t != null ? tc + off.t : tc;
        const mp = clipPos(cur, group, tc2);
        if (mp) {
          const offX = off?.x ?? 0;
          const offZ = off?.z ?? 0;
          out.set(id, { x: mp.x + offX, z: mp.z + offZ, y: memberPreviewY(id, flyY, yMode) });
        }
      }
    }
  }

  // Static members hold still at the base waypoint (+ their offset).
  if (baseWp) {
    for (const id of memberIds) {
      if (!staticIds.has(id)) continue;
      place(id, basePos);
    }
  }
  // 旋转动画：所有非静态成员叠加当前 Y 轴角度增量（移动 + 旋转可并行）。
  if (hasRotate) {
    for (const id of memberIds) {
      if (staticIds.has(id)) continue;
      const p = out.get(id);
      if (p) p.rotY = rotY;
    }
  }
  return out;
}

/** Simulated poses of every member of the currently previewed group
 *  (null when no preview exists). 暂停 / 时间轴 scrub 时也返回位姿（AE 风格：
 *  播放头停在哪，画布就显示哪一帧）。 */
export function previewMemberPositions(): Map<string, PreviewPose> | null {
  const act = activeGroup();
  if (!act || !S.animPreview || S.animPreview.groupId !== act.id) return null;
  return previewPositions(act, S.animPreview.t);
}

// ---------------------------------------------------------------- fx preview

/** 全屏特效的预览状态：flash = 0..1 闪光包络（乘峰值强度前的归一值已含峰值），
 *  shakeX/Y = 画布像素抖动偏移。render.ts 消费（画布平移 + 全屏 overlay）。 */
export interface PreviewFxState {
  flash: number;
  flashColor: string;
  shakeX: number;
  shakeY: number;
}

/** 闪光包络关键帧（x = t/dur 归一，y = 峰值归一）—— 与烘焙的 BuildFlashClip
 *  同一形状（实测自 oc1 4-2 的 4_2_Lightning.anim：快攻双闪 + 余晖）。 */
const FLASH_KX = [0, 0.04, 0.16, 0.22, 0.36, 0.6, 0.64, 1];
const FLASH_KY = [0, 1, 0.125, 0.625, 0, 0, 0.375, 0];

function flashEnvelope(k: number): number {
  if (k <= 0 || k >= 1) return 0;
  for (let i = 1; i < FLASH_KX.length; i++) {
    if (k <= FLASH_KX[i]) {
      const span = FLASH_KX[i] - FLASH_KX[i - 1];
      const f = span > 0 ? (k - FLASH_KX[i - 1]) / span : 0;
      return FLASH_KY[i - 1] + (FLASH_KY[i] - FLASH_KY[i - 1]) * f;
    }
  }
  return 0;
}

/** 确定性抖动采样（FNV 哈希 → [-1,1]，下标 i）：预览与烘焙同为「固定种子伪
 *  随机 + 线性插值」的形态（数值不必逐位一致，观感一致即可）。 */
function fxJitter(seedStr: string, i: number): number {
  let h = 2166136261 >>> 0;
  const s = seedStr + ":" + i;
  for (let k = 0; k < s.length; k++) h = Math.imul(h ^ s.charCodeAt(k), 16777619) >>> 0;
  return ((h % 2000) / 1000) - 1;
}

/** 当前预览中的全屏特效状态（无特效 / 无预览返回 null）。 */
export function previewFxState(): PreviewFxState | null {
  const act = activeGroup();
  if (!act || !S.animPreview || S.animPreview.groupId !== act.id) return null;
  if (!isFxGroup(act)) return null;

  let t = S.animPreview.t;
  const total = previewDuration(act);
  if (act.loop && total > 0) t = t % total;
  const startOff = act.startDelay ?? 0;

  // 画布像素/世界单位比例（抖动幅度以米计）。
  const p0 = worldToCanvas(0, 0);
  const p1 = worldToCanvas(1, 0);
  const pxPerUnit = Math.max(1e-6, Math.hypot(p1.x - p0.x, p1.y - p0.y));

  let flash = 0;
  let flashColor = "#DEE1FF";
  let shakeX = 0;
  let shakeY = 0;
  for (const evt of act.events) {
    const start = startOff + eventStart(evt);
    const dur = eventDuration(evt, act);
    if (dur <= 0) continue;
    let tc = t - start;
    if (tc < 0 || (!evt.loop && tc >= dur)) continue;
    if (evt.loop) tc = tc % dur; // 自转移重放（与 BuildController 的 loop 同语义）
    if (evt.type === "flash") {
      const peak = evt.flashIntensity ?? 4;
      const e = flashEnvelope(tc / dur) * peak;
      if (e > flash) {
        flash = e;
        if (evt.flashColor && /^#[0-9a-fA-F]{6}$/.test(evt.flashColor)) flashColor = evt.flashColor;
      }
    } else if (evt.type === "shake") {
      const amp = (evt.shakeAmplitude ?? 0.15) * pxPerUnit;
      const decay = Math.max(0, 1 - tc / dur);
      // 15Hz 关键帧间线性插值（与烘焙曲线同采样密度）。
      const pos = tc * 15;
      const i = Math.floor(pos);
      const f = pos - i;
      const jx = fxJitter(evt.id, i) * (1 - f) + fxJitter(evt.id, i + 1) * f;
      const jy = fxJitter(evt.id + "y", i) * (1 - f) + fxJitter(evt.id + "y", i + 1) * f;
      shakeX += jx * amp * decay;
      shakeY += jy * amp * 0.6 * decay;
    }
  }
  if (flash <= 0.001 && Math.abs(shakeX) < 0.01 && Math.abs(shakeY) < 0.01) return null;
  return { flash, flashColor, shakeX, shakeY };
}

/** 时间轴播放头位置（无预览时用模块级 scrub 时间，播放中 / 暂停时用预览时钟）。 */
let tlScrubT = 0;

/** 当前组播放头时间（秒）。 */
export function timelinePlayheadTime(group: AnimGroup): number {
  return S.animPreview && S.animPreview.groupId === group.id ? S.animPreview.t : tlScrubT;
}

/** 播放中把播放头 DOM 直接移动到当前时间（不整面板重绘）。 */
function updateTimelineDom(group: AnimGroup): void {
  const t = timelinePlayheadTime(group);
  const ph = document.getElementById("anim-tl-playhead");
  if (ph) ph.style.left = `${t * tlPps()}px`;
  const label = document.getElementById("anim-tl-time");
  if (label) label.textContent = `${t.toFixed(1)}s / ${groupTimelineDuration(group).toFixed(1)}s`;
}

function previewLoop(now: number): void {
  previewRAF = 0;
  if (!S.animPreview || !S.animPreview.playing) return;
  if (!previewLastTick) previewLastTick = now;
  const dt = Math.min(0.05, (now - previewLastTick) / 1000);
  previewLastTick = now;
  S.animPreview.t += dt;
  const g = activeGroup();
  if (g && S.animPreview.groupId === g.id) {
    // 播放到序列末尾（启动延迟 + 时间轴全长 + 循环组 loopDelay）即回到 0s
    // 重播 —— 预览是循环观看的编辑辅助；游戏内是否循环由组的 loop 决定。
    const total = previewDuration(g);
    if (total > 0 && S.animPreview.t >= total) S.animPreview.t %= total;
    updateTimelineDom(g);
  }
  draw();
  previewRAF = requestAnimationFrame(previewLoop);
}

export function stopAnimPreview(): void {
  if (previewRAF) cancelAnimationFrame(previewRAF);
  previewRAF = 0;
  previewLastTick = 0;
  if (S.animPreview) tlScrubT = S.animPreview.t;
  S.animPreview = null;
}

/** 播放 / 暂停（暂停保留当前时间，恢复从播放头继续）。 */
export function toggleAnimPreview(): void {
  const group = activeGroup();
  if (!group) return;
  if (S.animPreview && S.animPreview.groupId === group.id) {
    S.animPreview.playing = !S.animPreview.playing;
  } else {
    S.animPreview = { playing: true, t: tlScrubT, groupId: group.id };
  }
  if (S.animPreview.playing) {
    previewLastTick = 0;
    if (!previewRAF) previewRAF = requestAnimationFrame(previewLoop);
  }
  renderRightPanel();
  draw();
}

/** 时间轴 scrub：把播放头移动到 t（秒）并暂停；画布实时显示该帧位姿。 */
export function scrubAnimPreview(t: number): void {
  const group = activeGroup();
  if (!group) return;
  t = Math.max(0, t);
  if (S.animPreview && S.animPreview.groupId === group.id) {
    S.animPreview.t = t;
    S.animPreview.playing = false;
    if (previewRAF) {
      cancelAnimationFrame(previewRAF);
      previewRAF = 0;
    }
    previewLastTick = 0;
  } else {
    tlScrubT = t;
    S.animPreview = { playing: false, t, groupId: group.id };
  }
  updateTimelineDom(group);
  draw();
}

/** 步进 ±0.1s（暂停并吸附到 0.1 网格）。 */
export function stepAnimPreview(delta: number): void {
  const group = activeGroup();
  if (!group) return;
  scrubAnimPreview(snapTime(timelinePlayheadTime(group) + delta));
  renderRightPanel();
}

/** 回开头（t=0，保持当前播放/暂停状态）。 */
export function rewindAnimPreview(): void {
  const group = activeGroup();
  if (!group) return;
  tlScrubT = 0;
  if (S.animPreview && S.animPreview.groupId === group.id) {
    S.animPreview.t = 0;
    previewLastTick = 0;
    updateTimelineDom(group);
    draw();
  } else {
    scrubAnimPreview(0);
  }
  renderRightPanel();
}

function syncPreview(group: AnimGroup): void {
  if (S.animPreview && S.animPreview.groupId !== group.id) stopAnimPreview();
}

function drawPreviewOverlay(group: AnimGroup): void {
  if (!S.animPreview || S.animPreview.groupId !== group.id) return;
  const col = groupColor(group.id);
  // 特效组没有路线头/成员位姿：只画进度条（特效本体由 previewFxState 在
  // render.ts 全屏渲染）。
  const pos = isFxGroup(group) ? null : previewPositions(group, S.animPreview.t);
  const head = pos ? pos.get("__head__") : undefined;
  if (head) {
    const p = worldToCanvas(head.x, head.z);
    dom.ctx.save();
    dom.ctx.beginPath();
    dom.ctx.arc(p.x, p.y, 7, 0, Math.PI * 2);
    dom.ctx.fillStyle = colorA(col, 0.9);
    dom.ctx.fill();
    dom.ctx.strokeStyle = "#fff";
    dom.ctx.lineWidth = 1.5;
    dom.ctx.stroke();
    dom.ctx.restore();
    if ((head.y ?? 0) > 0.01) {
      dom.ctx.save();
      dom.ctx.font = "10px sans-serif";
      const txt = `↑${(head.y ?? 0).toFixed(1)}`;
      dom.ctx.fillStyle = "rgba(20,20,24,0.8)";
      const tw = dom.ctx.measureText(txt).width;
      dom.ctx.fillRect(p.x - tw / 2 - 2, p.y - 20, tw + 4, 13);
      dom.ctx.fillStyle = "#9fd0ff";
      dom.ctx.fillText(txt, p.x - tw / 2, p.y - 10);
      dom.ctx.restore();
    }
  }
  // Progress bar (top-left below the mode hint).
  const total = previewDuration(group);
  if (total > 0) {
    const frac = Math.max(0, Math.min(1, S.animPreview.t / total));
    const bw = 120;
    const bx = 10;
    const by = 40;
    dom.ctx.fillStyle = "rgba(20,20,24,0.8)";
    dom.ctx.fillRect(bx, by, bw + 4, 10);
    dom.ctx.fillStyle = colorA(col, 0.9);
    dom.ctx.fillRect(bx + 2, by + 2, bw * frac, 6);
  }
}

// ------------------------------------------------------------------ panel: list view

function memberSummary(g: AnimGroup): string {
  const names: string[] = [];
  for (const id of g.itemInstanceIds) {
    const it = S.items.find((i) => i.instanceId === id);
    if (it) names.push(itemLabel(it));
  }
  for (const id of g.floorInstanceIds) {
    const f = S.floors.find((fl) => fl.instanceId === id);
    if (f) names.push(`地板:${f.displayName}`);
  }
  for (const id of g.objectInstanceIds) {
    const off = g.memberOffsets?.find((o) => o.instanceId === id);
    const st = g.memberStatic?.find((m) => m.instanceId === id);
    names.push(off?.displayName ?? st?.displayName ?? id.replace("u:", ""));
  }
  return names.join("、");
}

function groupCardHtml(g: AnimGroup): string {
  const col = groupColor(g.id);
  const itemN = g.itemInstanceIds.length;
  const floorN = g.floorInstanceIds.length;
  const objN = g.objectInstanceIds.length;
  const loopMark = g.loop || g.events.some((e) => e.loop) ? " ⟲" : "";
  const emptyRouteEvts = g.events.filter(
    (e) => e.type === "move" && (!e.waypointIds || e.waypointIds.length === 0)
  ).length;
  const warnings: string[] = [];
  const fxT = groupFxType(g);
  if (!isFxGroup(g) && itemN + floorN + objN === 0) warnings.push("⚠ 无成员");
  if (emptyRouteEvts > 0) warnings.push(`⚠ ${emptyRouteEvts} 个事件无路线`);
  if (isFxGroup(g) && !fxT) warnings.push("⚠ 无特效事件");
  const fxChip = fxT === "shake"
    ? `<span class="mgc-chip chip-obj">🌍 全屏抖动</span>`
    : fxT === "flash"
      ? `<span class="mgc-chip chip-item">⚡ 闪电</span>`
      : "";
  return `<div class="anim-group-card" data-group-id="${g.id}">
    <div class="mgc-color" style="background:${col}"></div>
    <div class="mgc-main">
      <div class="mgc-name">${escHtml(g.displayName)}${loopMark}</div>
      <div class="mgc-members">
        ${fxChip}
        ${itemN ? `<span class="mgc-chip chip-item">物品 ${itemN}</span>` : ""}
        ${floorN ? `<span class="mgc-chip chip-floor">地板 ${floorN}</span>` : ""}
        ${objN ? `<span class="mgc-chip chip-obj">其他 ${objN}</span>` : ""}
      </div>
      <div class="mgc-meta">${isFxGroup(g) ? "✨ 特效组" : `📍 路点 ${g.waypoints.length}`} · 🔁 事件 ${g.events.length}${warnings.length ? ` <span class="mgc-warn">${warnings.join(" · ")}</span>` : ""}</div>
      ${memberSummary(g) ? `<div class="mgc-sub">${escHtml(memberSummary(g))}</div>` : ""}
    </div>
  </div>`;
}

function renderGroupList(body: HTMLElement): void {
  if (S.animPreview) stopAnimPreview();
  const groups = S.animControls.filter((g) => groupVisibleInLayer(g));
  const head = `<div class="anim-list-head">
    <span class="anim-list-title">🎬 动画组${groups.length ? ` (${groups.length})` : ""}</span>
    <button type="button" class="btn-small primary" id="btn-new-group">＋ 新增分组</button>
  </div>`;

  if (groups.length === 0) {
    body.innerHTML = head + `<div class="anim-empty">
      <div class="anim-empty-icon">🎬</div>
      <div class="anim-empty-text">还没有动画组</div>
      <div class="anim-empty-sub">创建动画组后，物品 / 地板 / 装饰可沿路线自动移动（如传送带、巡逻的 NPC、漂移的木筏）。</div>
      <button type="button" class="btn-small primary anim-empty-btn" id="btn-new-group">＋ 新增分组</button>
      <div class="anim-empty-sub">快捷方式：在地图上右键一个物品 →「创建动画组」</div>
    </div>`;
  } else {
    body.innerHTML = head + groups.map(groupCardHtml).join("");
  }

  body.querySelectorAll<HTMLElement>("#btn-new-group").forEach((btn) => {
    btn.addEventListener("click", openNewGroupModal);
  });

  body.querySelectorAll<HTMLElement>(".anim-group-card").forEach((row) => {
    row.addEventListener("click", () => {
      S.activeAnimGroupId = row.dataset.groupId!;
      S.activeAnimEventIdx = null;
      S.selectedWaypointId = null;
      S.animMode = "none";
      S.activeAnimTab = "timeline";
      S.animPickTargetGroupId = null;
      clearAnimSelection();
      const group = activeGroup();
      if (group && group.itemInstanceIds.length > 0) {
        const item = S.items.find((it) => it.instanceId === group!.itemInstanceIds[0]);
        if (item) {
          setSelection([item._editorKey]);
          ensureItemVisible(item);
        }
      }
      renderRightPanel();
      draw();
    });
  });
}

// ------------------------------------------------------------------ panel: wizard (new group)

function defaultGroupName(): string {
  return `动画组 ${S.animControls.length + 1}`;
}

function memberSpawnPos(itemIds: string[], floorIds: string[]): { x: number; z: number } | null {
  for (const id of itemIds) {
    const it = S.items.find((i) => i.instanceId === id);
    if (it) return { x: it._wx, z: it._wz };
  }
  for (const id of floorIds) {
    const f = S.floors.find((fl) => fl.instanceId === id);
    if (f) return { x: f._wx, z: f._wz };
  }
  return null;
}

function createGroup(name: string, itemIds: string[], floorIds: string[]): void {
  pushHistory();
  const pos = memberSpawnPos(itemIds, floorIds);
  const wp: AnimGroupWaypoint = { id: uuid(), x: pos?.x ?? 0, z: pos?.z ?? 0 };
  const group: AnimGroup = {
    id: uuid(),
    displayName: name,
    groupKind: "members",
    itemInstanceIds: itemIds,
    floorInstanceIds: floorIds,
    objectInstanceIds: [],
    memberOffsets: [],
    memberStatic: [],
    memberGroups: [],
    startDelay: 0,
    loop: false,
    loopDelay: 2,
    waypoints: [wp],
    events: [{
      id: uuid(),
      type: "move",
      delay: 0,
      startTime: 0,
      intervalSeconds: 2,
      waypointIds: [wp.id],
    }],
  };
  S.animControls.push(group);
  S.activeAnimGroupId = group.id;
  S.activeAnimEventIdx = 0;
  S.activeAnimTab = "timeline";
  S.animPickTargetGroupId = null;
  S.activeRightTab = "anim";
  S.animMode = "none";
  clearAnimSelection();
  updatePanelTabButtons();
  updateAnimPickBar();
  renderRightPanel();
  draw();
  setStatus(
    `已创建动画组「${name}」。可在「🧩 成员」添加成员（📐 地图选点 / 下拉），在「📍 路线」添加路点。`
  );
}

/** 创建全屏特效组（无成员；首个事件即所选特效类型，带默认参数）。 */
function createFxGroup(name: string, fxType: "shake" | "flash"): void {
  pushHistory();
  const group: AnimGroup = {
    id: uuid(),
    displayName: name,
    groupKind: "fx",
    itemInstanceIds: [],
    floorInstanceIds: [],
    objectInstanceIds: [],
    memberOffsets: [],
    memberStatic: [],
    memberGroups: [],
    startDelay: 0,
    loop: true,
    loopDelay: 6,
    waypoints: [],
    events: [fxType === "shake"
      ? { id: uuid(), type: "shake", delay: 0, startTime: 0, shakeAmplitude: 0.15, duration: 2 }
      : { id: uuid(), type: "flash", delay: 0, startTime: 0, flashIntensity: 4, duration: 0.8, flashColor: "#DEE1FF", soundCue: "DynamicStage01Thunder" }],
  };
  S.animControls.push(group);
  S.activeAnimGroupId = group.id;
  S.activeAnimEventIdx = 0;
  S.activeAnimTab = "timeline";
  S.animPickTargetGroupId = null;
  S.activeRightTab = "anim";
  S.animMode = "none";
  clearAnimSelection();
  updatePanelTabButtons();
  updateAnimPickBar();
  renderRightPanel();
  draw();
  setStatus(
    `已创建特效组「${name}」（${fxType === "shake" ? "全屏抖动" : "闪电"}）。在「🎬 时间轴」添加更多事件错开节奏；默认开启整组循环。`
  );
}

export function openNewGroupModal(): void {
  if (S.animMode !== "none") {
    S.animMode = "none";
    clearAnimSelection();
  }
  const modalName = defaultGroupName();
  openModal(
    "＋ 新增移动分组",
    `<p class="modal-hint">动画组驱动物品 / 地板沿路线移动；全屏特效组无成员，驱动相机抖动（🌍 地震）或闪电明暗（⚡ 雷暴）。组名建议用英文（如 Island1 / Earthquake / LightningStorm）。</p>
     <label class="modal-field">组名（英文）<input type="text" id="wizard-name" value="${escHtml(modalName)}" placeholder="如 Island1" /></label>
     <label class="modal-field">组类型
       <label class="check"><input type="radio" name="wizard-kind" value="members" checked /> 动画组（成员 + 路线）</label>
       <label class="check"><input type="radio" name="wizard-kind" value="fx-flash" /> ⚡ 全屏特效 · 闪电（专用灯明暗交替）</label>
       <label class="check"><input type="radio" name="wizard-kind" value="fx-shake" /> 🌍 全屏特效 · 抖动（相机地震）</label>
     </label>`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn primary" id="wizard-create">创建</button>`
  );

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.getElementById("wizard-create")?.addEventListener("click", () => {
    const name =
      ((document.getElementById("wizard-name") as HTMLInputElement | null)?.value ?? "").trim() ||
      modalName;
    const kind = (document.querySelector('input[name="wizard-kind"]:checked') as HTMLInputElement | null)?.value ?? "members";
    closeModal();
    if (kind === "fx-shake") createFxGroup(name, "shake");
    else if (kind === "fx-flash") createFxGroup(name, "flash");
    else createGroup(name, [], []);
  });
  (document.getElementById("wizard-name") as HTMLInputElement | null)?.focus();
}

// ------------------------------------------------------------------ route time curve (F3)

const CURVE_W = 560;
const CURVE_H = 170;

/** Per-waypoint arrival times (without dwell keys). */
function routeArrivalTimes(evt: AnimGroupEvent, group: AnimGroup): number[] {
  const wps = (evt.waypointIds ?? [])
    .map((id) => group.waypoints.find((w) => w.id === id))
    .filter(Boolean) as AnimGroupWaypoint[];
  if (wps.length <= 1) return wps.map(() => 0);
  const anyEdit = wps.some((w) => (w.wait ?? 0) > 0 || (w.segmentSeconds ?? 0) > 0);
  if (!anyEdit && wps.some((w) => w.hasTime)) return wps.map((w) => w.t ?? 0);
  const interval = evt.intervalSeconds ?? 2;
  const out: number[] = [];
  let t = 0;
  for (let i = 0; i < wps.length; i++) {
    out.push(t);
    if (i < wps.length - 1) t += (wps[i].segmentSeconds ?? interval) + (wps[i].wait ?? 0);
  }
  return out;
}

function routeCurveInfoText(group: AnimGroup, evtIdx: number): string {
  const evt = group.events[evtIdx];
  const wps = (evt.waypointIds ?? [])
    .map((id) => group.waypoints.find((w) => w.id === id))
    .filter(Boolean) as AnimGroupWaypoint[];
  const arrival = routeArrivalTimes(evt, group);
  const parts: string[] = [`开始 ${eventStart(evt).toFixed(1)}s`];
  for (let i = 0; i < wps.length; i++) {
    if (i < wps.length - 1) {
      parts.push(`段${i + 1} ${(wps[i].segmentSeconds ?? evt.intervalSeconds ?? 2).toFixed(1)}s`);
    }
    if ((wps[i].wait ?? 0) > 0) parts.push(`停${i + 1} ${wps[i].wait}s`);
  }
  const clipLen = arrival.length > 0 ? arrival[arrival.length - 1] + (wps[wps.length - 1]?.wait ?? 0) : 0;
  parts.push(`总 ${clipLen.toFixed(1)}s`);
  return parts.join(" · ");
}

function drawRouteCurve(canvas: HTMLCanvasElement, group: AnimGroup, evtIdx: number): void {
  const ctx = canvas.getContext("2d");
  if (!ctx) return;
  const W = CURVE_W;
  const H = CURVE_H;
  canvas.width = W;
  canvas.height = H;
  const evt = group.events[evtIdx];
  const wps = (evt.waypointIds ?? [])
    .map((id) => group.waypoints.find((w) => w.id === id))
    .filter(Boolean) as AnimGroupWaypoint[];
  const arrival = routeArrivalTimes(evt, group);
  const n = arrival.length;
  const clipLen = n > 0 ? arrival[n - 1] + (wps[n - 1]?.wait ?? 0) : 0;
  const total = Math.max(0.5, clipLen);
  const padL = 34;
  const padR = 10;
  const padT = 10;
  const padB = 18;
  const xOf = (t: number) => padL + (t / total) * (W - padL - padR);
  const yOf = (p: number) => H - padB - p * (H - padT - padB);

  ctx.clearRect(0, 0, W, H);
  ctx.fillStyle = "#181b21";
  ctx.fillRect(0, 0, W, H);

  // Second grid
  const step = total <= 10 ? 1 : total <= 30 ? 2 : 5;
  ctx.strokeStyle = "rgba(255,255,255,0.07)";
  ctx.lineWidth = 1;
  ctx.font = "9px monospace";
  ctx.textAlign = "center";
  ctx.fillStyle = "rgba(255,255,255,0.4)";
  for (let s = 0; s <= total; s += step) {
    const x = xOf(s);
    ctx.beginPath();
    ctx.moveTo(x, padT);
    ctx.lineTo(x, H - padB);
    ctx.stroke();
    ctx.fillText(String(s), x, H - 5);
  }
  // Progress polyline (arrival keys + dwell plateaus)
  ctx.strokeStyle = "#3d6bf3";
  ctx.lineWidth = 2;
  ctx.beginPath();
  let prevX = xOf(0);
  let prevY = yOf(0);
  ctx.moveTo(prevX, prevY);
  for (let i = 0; i < n; i++) {
    const x = xOf(arrival[i]);
    const y = yOf(n > 1 ? i / (n - 1) : 0);
    ctx.lineTo(x, y);
    prevX = x;
    prevY = y;
  }
  // Dwell plateaus (wait) as dashed horizontal segments
  ctx.setLineDash([3, 3]);
  ctx.strokeStyle = "#7fd6b0";
  ctx.lineWidth = 1.5;
  for (let i = 0; i < n; i++) {
    const w = wps[i]?.wait ?? 0;
    if (w > 0) {
      const x0 = xOf(arrival[i]);
      const y = yOf(n > 1 ? i / (n - 1) : 0);
      ctx.beginPath();
      ctx.moveTo(x0, y);
      ctx.lineTo(x0 + (w / total) * (W - padL - padR), y);
      ctx.stroke();
    }
  }
  ctx.setLineDash([]);
  ctx.stroke();
  // Handles: waypoints (circles), wait ends (squares)
  ctx.fillStyle = "#ffcc00";
  for (let i = 0; i < n; i++) {
    const x = xOf(arrival[i]);
    const y = yOf(n > 1 ? i / (n - 1) : 0);
    ctx.beginPath();
    ctx.arc(x, y, 4, 0, Math.PI * 2);
    ctx.fillStyle = i === 0 ? "#9aa0a6" : "#ffcc00";
    ctx.fill();
    ctx.strokeStyle = "#fff";
    ctx.lineWidth = 1;
    ctx.stroke();
    const w = wps[i]?.wait ?? 0;
    if (w > 0) {
      const wx2 = x + (w / total) * (W - padL - padR);
      ctx.fillStyle = "#7fd6b0";
      ctx.fillRect(wx2 - 3, y - 3, 6, 6);
    }
  }
}

interface CurveDrag {
  kind: "point" | "wait";
  idx: number;
  orig: number;
  changed: boolean;
}

/** Singleton drag context — window handlers are registered exactly once. */
interface CurveDragCtx {
  canvas: HTMLCanvasElement;
  group: AnimGroup;
  evtIdx: number;
  drag: CurveDrag;
}
let curveDragCtx: CurveDragCtx | null = null;
let curveHandlersWired = false;

function applyCurveDrag(ctx: CurveDragCtx, mx: number): void {
  const { canvas, group, evtIdx, drag } = ctx;
  const evt = group.events[evtIdx];
  const wps = (evt.waypointIds ?? [])
    .map((id) => group.waypoints.find((w) => w.id === id))
    .filter(Boolean) as AnimGroupWaypoint[];
  const arrival = routeArrivalTimes(evt, group);
  const total = curveTotal(group, evtIdx);
  const t = Math.max(0, ((mx - 34) / (CURVE_W - 34 - 10)) * total);
  if (drag.kind === "point") {
    const i = drag.idx;
    const base = arrival[i - 1] + (wps[i - 1]?.wait ?? 0);
    const v = Math.max(0.1, t - base);
    if (Math.abs(v - (wps[i - 1].segmentSeconds ?? evt.intervalSeconds ?? 2)) > 0.01) {
      wps[i - 1].segmentSeconds = v;
      drag.changed = true;
    }
  } else {
    const i = drag.idx;
    const base = arrival[i];
    const v = Math.max(0, t - base);
    if (Math.abs(v - (wps[i]?.wait ?? 0)) > 0.01) {
      if (wps[i]) wps[i].wait = v;
      drag.changed = true;
    }
  }
  drawRouteCurve(canvas, group, evtIdx);
  const info = document.getElementById(`route-curve-info-${evtIdx}`);
  if (info) info.textContent = routeCurveInfoText(group, evtIdx);
}

function wireRouteCurve(canvas: HTMLCanvasElement, group: AnimGroup, evtIdx: number): void {
  const evt = group.events[evtIdx];
  const W = CURVE_W;
  const H = CURVE_H;
  const padL = 34;
  const padR = 10;
  const padT = 10;
  const padB = 18;

  const hitTest = (mx: number, my: number): CurveDrag | null => {
    const wps = (evt.waypointIds ?? [])
      .map((id) => group.waypoints.find((w) => w.id === id))
      .filter(Boolean) as AnimGroupWaypoint[];
    const arrival = routeArrivalTimes(evt, group);
    const n = arrival.length;
    const total = curveTotal(group, evtIdx);
    const xOf = (t: number) => padL + (t / total) * (W - padL - padR);
    const yOf = (p: number) => H - padB - p * (H - padT - padB);
    const near = (x: number, y: number) => Math.hypot(x - mx, y - my) <= 12;
    for (let i = 0; i < n; i++) {
      const w = wps[i]?.wait ?? 0;
      if (w > 0) {
        const x = xOf(arrival[i] + w);
        const y = yOf(n > 1 ? i / (n - 1) : 0);
        if (near(x, y)) return { kind: "wait", idx: i, orig: w, changed: false };
      }
    }
    for (let i = 1; i < n; i++) {
      const x = xOf(arrival[i]);
      const y = yOf(i / (n - 1));
      if (near(x, y)) {
        return {
          kind: "point",
          idx: i,
          orig: wps[i - 1].segmentSeconds ?? evt.intervalSeconds ?? 2,
          changed: false,
        };
      }
    }
    return null;
  };

  canvas.addEventListener("mousedown", (e) => {
    const rect = canvas.getBoundingClientRect();
    const mx = ((e.clientX - rect.left) * W) / rect.width;
    const my = ((e.clientY - rect.top) * H) / rect.height;
    const drag = hitTest(mx, my);
    if (drag) {
      e.preventDefault();
      e.stopPropagation();
      curveDragCtx = { canvas, group, evtIdx, drag };
      if (!curveHandlersWired) {
        curveHandlersWired = true;
        window.addEventListener("mousemove", (ev) => {
          if (!curveDragCtx) return;
          const rect = curveDragCtx.canvas.getBoundingClientRect();
          const mx2 = ((ev.clientX - rect.left) * CURVE_W) / rect.width;
          applyCurveDrag(curveDragCtx, mx2);
        });
        window.addEventListener("mouseup", () => {
          const ctx = curveDragCtx;
          curveDragCtx = null;
          if (ctx && ctx.drag.changed) {
            pushHistory();
            S.dirty = true;
            renderRightPanel();
            draw();
          }
        });
      }
    }
  });
}

function curveTotal(group: AnimGroup, evtIdx: number): number {
  const evt = group.events[evtIdx];
  const wps = (evt.waypointIds ?? [])
    .map((id) => group.waypoints.find((w) => w.id === id))
    .filter(Boolean) as AnimGroupWaypoint[];
  const arrival = routeArrivalTimes(evt, group);
  const clipLen = arrival.length > 0 ? arrival[arrival.length - 1] + (wps[wps.length - 1]?.wait ?? 0) : 0;
  return Math.max(0.5, clipLen);
}

// ------------------------------------------------------------------ panel: entry point

export function renderAnimControlPanel(body: HTMLElement): void {
  updateAnimPickBar();
  const group = activeGroup();
  if (group) {
    syncPreview(group);
    renderGroupEditor(body, group);
    return;
  }
  if (S.animPreview) stopAnimPreview();
  renderGroupList(body);
}

// ------------------------------------------------------------------ panel: group editor

function sectionTitle(icon: string, text: string, count: string, color: string): string {
  return `<div class="anim-section-title" style="--sec:${color}">
    <span class="sec-icon">${icon}</span><span class="sec-text">${text}</span>
    ${count ? `<span class="sec-count">${count}</span>` : ""}
  </div>`;
}

function memberRowHtml(group: AnimGroup, name: string, role: string, id: string, kind: "item" | "floor" | "obj"): string {
  const off = group.memberOffsets?.find((o) => o.instanceId === id);
  const followOpts = group.waypoints
    .map((wp, i) => `<option value="${wp.id}"${off?.followWaypointId === wp.id ? " selected" : ""}>#${i + 1}</option>`)
    .join("");
  return `<div class="anim-member-row">
    <span class="zh">${escHtml(name)}</span>
    <span class="wp-badge role-${role}">${role}</span>
    <span class="member-follow-wrap" title="跟随路点：该成员固定在指定路点（可加偏移）不随路线移动；「—」= 沿路线移动">
      跟<select class="member-follow-wp" data-follow-member="${id}">
        <option value="">—</option>${followOpts}
      </select>
    </span>
    <button type="button" class="btn-del" data-del-${kind}="${id}" title="移出组">×</button>
  </div>`;
}

// ------------------------------------------------------------------ member groups

/** Pure background content (water, sky…): never selectable as move members. */
function isBackgroundItem(it: EditorItem): boolean {
  return itemCategoryOf(it) === "background";
}

function flatMemberIds(group: AnimGroup): string[] {
  return [...group.itemInstanceIds, ...group.floorInstanceIds, ...group.objectInstanceIds];
}

function memberPathOf(group: AnimGroup, id: string): string | null {
  const it = S.items.find((i) => i.instanceId === id);
  if (it?.hierarchyPath) return it.hierarchyPath;
  const f = S.floors.find((fl) => fl.instanceId === id);
  if (f?.hierarchyPath) return f.hierarchyPath;
  const off = group.memberOffsets?.find((o) => o.instanceId === id);
  if (off?.hierarchyPath) return off.hierarchyPath;
  const st = group.memberStatic?.find((m) => m.instanceId === id);
  if (st?.hierarchyPath) return st.hierarchyPath;
  return null;
}

function memberNameOf(group: AnimGroup, id: string): string {
  const it = S.items.find((i) => i.instanceId === id);
  if (it) return itemLabel(it);
  const f = S.floors.find((fl) => fl.instanceId === id);
  if (f) return `地板:${f.displayName}`;
  const off = group.memberOffsets?.find((o) => o.instanceId === id);
  if (off?.displayName) return off.displayName;
  const st = group.memberStatic?.find((m) => m.instanceId === id);
  if (st?.displayName) return st.displayName;
  return id.replace("u:", "");
}

function memberKindOf(group: AnimGroup, id: string): "item" | "floor" | "obj" {
  if (group.itemInstanceIds.includes(id)) return "item";
  if (group.floorInstanceIds.includes(id)) return "floor";
  return "obj";
}

interface MemberGroupView {
  /** User group id, or "der:<memberId>" for hierarchy-derived groups. */
  id: string;
  name: string;
  derived: boolean;
  /** Derived groups: the flat member id that is the group root. */
  rootMemberId: string | null;
  /** User groups: flat member ids inside the group. */
  memberIds: string[];
  /** Derived groups: child items to display (read-only). */
  kids: EditorItem[];
}

/** Groups members into named member groups (user-created + hierarchy-derived,
 *  e.g. Island1 roots with child items); the rest are "standalone". */
function buildMemberGroupViews(group: AnimGroup): { views: MemberGroupView[]; standalone: string[] } {
  const flat = flatMemberIds(group);
  const views: MemberGroupView[] = [];
  const covered = new Set<string>();
  for (const mg of group.memberGroups ?? []) {
    const ids = mg.memberInstanceIds.filter((id) => flat.includes(id));
    for (const id of ids) covered.add(id);
    views.push({ id: mg.id, name: mg.name, derived: false, rootMemberId: null, memberIds: ids, kids: [] });
  }
  for (const id of flat) {
    if (covered.has(id)) continue;
    const path = memberPathOf(group, id);
    if (!path) continue;
    const kids = S.items.filter((k) => !!k.hierarchyPath && k.hierarchyPath.startsWith(path + "/"));
    if (kids.length === 0) continue;
    covered.add(id);
    views.push({
      id: "der:" + id,
      name: memberNameOf(group, id),
      derived: true,
      rootMemberId: id,
      memberIds: [id],
      kids,
    });
  }
  const standalone = flat.filter((id) => !covered.has(id));
  return { views, standalone };
}

function groupEventsMeta(group: AnimGroup): string {
  return group.events.length > 0
    ? `驱动事件 ${group.events.map((_, i) => i + 1).join("、")}`
    : "未配置事件";
}

/** Nested tree for an event card: 成员组 → 成员 (indented, expandable). */
function eventMemberTreeHtml(group: AnimGroup): string {
  const { views, standalone } = buildMemberGroupViews(group);
  const parts: string[] = [];
  for (const v of views) {
    const collapsed = S.collapsedGroupIds.has(v.id);
    const n = v.derived ? v.kids.length : v.memberIds.length;
    let kids = "";
    if (!collapsed) {
      if (v.derived) {
        kids = v.kids
          .map((k) => `<div class="mt-member" data-kid="${k._editorKey}">${escHtml(itemLabel(k))}</div>`)
          .join("");
      } else {
        kids = v.memberIds
          .map((id) => {
            const role = memberRoleText(group, id);
            return `<div class="mt-member" data-member-id="${id}"><span class="wp-badge role-${role}">${role}</span>${escHtml(memberNameOf(group, id))}</div>`;
          })
          .join("");
        if (!kids) kids = '<div class="muted anim-member-empty">（空组）</div>';
      }
    }
    parts.push(
      `<div class="mt-group-node">
        <div class="mt-group-row${collapsed ? " collapsed" : ""}" data-member-group="${v.id}">
          <span class="mt-arrow">${collapsed ? "▸" : "▾"}</span>
          <span class="mt-name">${escHtml(v.name)}</span>
          ${v.derived ? `<span class="mt-tag">物品组</span>` : ""}
          <span class="mt-count">${n} 成员</span>
        </div>
        ${kids ? `<div class="mt-kids">${kids}</div>` : ""}
      </div>`
    );
  }
  for (const id of standalone) {
    const role = memberRoleText(group, id);
    parts.push(
      `<div class="mt-member" data-member-id="${id}"><span class="wp-badge role-${role}">${role}</span>${escHtml(memberNameOf(group, id))}</div>`
    );
  }
  if (!parts.length) return '<div class="muted">（无成员）</div>';
  return `<div class="member-tree">${parts.join("")}</div>`;
}

// ------------------------------------------------------------------ group editor tabs

let newMgFormOpen = false;

function createMemberGroup(group: AnimGroup, name: string): void {
  pushHistory();
  if (!group.memberGroups) group.memberGroups = [];
  const mg: AnimGroupMemberGroup = { id: uuid(), name: name.trim(), memberInstanceIds: [] };
  group.memberGroups.push(mg);
  S.animPickTargetGroupId = mg.id;
  S.dirty = true;
}

function deleteMemberGroup(group: AnimGroup, id: string): void {
  pushHistory();
  group.memberGroups = (group.memberGroups ?? []).filter((g) => g.id !== id);
  if (S.animPickTargetGroupId === id) S.animPickTargetGroupId = null;
  S.dirty = true;
}

function renderMembersTab(group: AnimGroup): string {
  const { views, standalone } = buildMemberGroupViews(group);
  let html = "";

  // Pick target selector
  const userGroups = group.memberGroups ?? [];
  html += `<div class="mv-pick-target">
    <span>框选加入目标</span>
    <select id="pick-target" title="框选/点选的成员将加入选中的成员组（未分组 = 直接加入动画组）">
      <option value="">未分组</option>
      ${userGroups
        .map((g) => `<option value="${g.id}"${g.id === S.animPickTargetGroupId ? " selected" : ""}>${escHtml(g.name)}</option>`)
        .join("")}
    </select>
    ${S.animPickTargetGroupId ? `<span class="mv-target-note">新成员将加入该成员组</span>` : ""}
  </div>`;

  // Member groups
  html += `<div class="mv-group-section">
    <div class="mv-group-section-head">
      <span>成员组 (${views.length})</span>
      <button type="button" class="btn-small" id="btn-new-member-group">＋ 新增成员组</button>
    </div>
    <div id="new-mg-form-wrap">${newMgFormOpen
      ? `<div class="mv-new-group-form">
          <input type="text" id="new-mg-name" placeholder="成员组名称，如：木筏A / 岛1" />
          <button type="button" class="btn-small primary" id="btn-mg-create">创建</button>
          <button type="button" class="btn-small" id="btn-mg-cancel">取消</button>
        </div>`
      : ""}</div>`;
  if (views.length === 0) {
    html += '<div class="muted anim-member-empty">（暂无成员组 — 点击「＋ 新增成员组」把一组物品归为同一组）</div>';
  }
  for (const v of views) {
    const collapsed = S.collapsedGroupIds.has(v.id);
    const n = v.derived ? v.kids.length : v.memberIds.length;
    html += `<div class="mv-group-card${v.derived ? " derived" : ""}">
      <div class="mv-group-title" data-toggle-group="${v.id}">
        <span class="subgroup-arrow">${collapsed ? "▸" : "▾"}</span>
        <span class="mv-group-name">${escHtml(v.name)}${v.derived ? ` <span class="mv-group-tag">物品组</span>` : ""}</span>
        <span class="mv-group-meta">${n} 成员 · ${groupEventsMeta(group)}</span>
        ${v.derived
          ? `<button type="button" class="btn-del" data-del-${memberKindOf(group, v.rootMemberId!)}="${v.rootMemberId}" title="移出该物品组（连同其下物品）">×</button>`
          : `<button type="button" class="btn-small" data-target-group="${v.id}" title="设为框选加入目标">🎯</button>
             <button type="button" class="btn-del" data-del-group="${v.id}" title="删除成员组（成员保留在未分组）">×</button>`}
      </div>`;
    if (!collapsed) {
      html += `<div class="mv-group-body">`;
      if (v.derived) {
        html += v.kids.length
          ? v.kids
              .map((k) => `<div class="mv-kid-row" data-kid="${k._editorKey}" title="${escHtml(k.hierarchyPath ?? "")}">${escHtml(itemLabel(k))}</div>`)
              .join("")
          : '<div class="muted anim-member-empty">（该物品组下无独立物品）</div>';
      } else if (v.memberIds.length) {
        html += v.memberIds
          .map((id) => memberRowHtml(group, memberNameOf(group, id), memberRoleText(group, id), id, memberKindOf(group, id)))
          .join("");
      } else {
        html += '<div class="muted anim-member-empty">（空组 — 地图选点框选物品后加入）</div>';
      }
      html += `</div>`;
    }
    html += `</div>`;
  }
  html += `</div>`;

  // Standalone members, split by kind
  const standItem = standalone.filter((id) => memberKindOf(group, id) === "item");
  const standFloor = standalone.filter((id) => memberKindOf(group, id) === "floor");
  const standObj = standalone.filter((id) => memberKindOf(group, id) === "obj");
  html += `<div class="mv-group-section">
    <div class="mv-group-section-head"><span>未分组成员 (${standalone.length})</span></div>`;
  if (standalone.length === 0) {
    html += '<div class="muted anim-member-empty">（所有成员都已分组）</div>';
  }
  const standList = (ids: string[], label: string) => {
    if (ids.length === 0) return "";
    return `<div class="anim-member-group">
      <div class="anim-member-group-head">${label} (${ids.length})</div>
      ${ids.map((id) => memberRowHtml(group, memberNameOf(group, id), memberRoleText(group, id), id, memberKindOf(group, id))).join("")}
    </div>`;
  };
  html += standList(standItem, "物品") + standList(standFloor, "地板") + standList(standObj, "其他物体");
  html += `</div>`;

  // Add controls: 地图选点 / 添加物品 / 添加装饰 / 添加地板
  const wantLayer = S.currentLayer === "anim" ? "items" : S.currentLayer;
  const itemCandidates = S.items.filter(
    (it) =>
      !isBackgroundItem(it) &&
      !isCollisionItem(it) &&
      itemLayerOfIt(it) === wantLayer &&
      !group.itemInstanceIds.includes(it.instanceId)
  );
  const decorCandidates = S.items.filter(
    (it) =>
      !isBackgroundItem(it) &&
      !isCollisionItem(it) &&
      itemLayerOfIt(it) === "decor" &&
      !group.itemInstanceIds.includes(it.instanceId)
  );
  // 空气地板（仅碰撞盒）可入组：Unity 端其 Col_AirFloor 对象会被烘焙动画。
  const floorCandidates = S.floors.filter(
    (f) => f.surfaceKind !== "background" && !group.floorInstanceIds.includes(f.instanceId)
  );
  html += `<div class="anim-add-row">
    <button type="button" class="btn-small pick-add" id="btn-add-members" title="地图选点：点选 / 框选地图上的物品、装饰与地板加入本组（水面等背景不可选）">📐 地图选点</button>
    ${itemCandidates.length > 0
      ? `<select class="group-add-item"><option value="">＋ 添加物品…</option>${itemCandidates
          .map((it) => `<option value="${it.instanceId}">${escHtml(itemLabel(it))}</option>`)
          .join("")}</select>`
      : ""}
    ${decorCandidates.length > 0
      ? `<select class="group-add-decor"><option value="">＋ 添加装饰…</option>${decorCandidates
          .map((it) => `<option value="${it.instanceId}">${escHtml(itemLabel(it))}</option>`)
          .join("")}</select>`
      : ""}
    ${floorCandidates.length > 0
      ? `<select class="group-add-floor"><option value="">＋ 添加地板…</option>${floorCandidates
          .map((f) => `<option value="${f.instanceId}">${escHtml(f.displayName)}</option>`)
          .join("")}</select>`
      : ""}
  </div>`;
  return html;
}

/** 路点池（在事件卡内管理：放置 / 起点 / 微调 / 停留 / 删除）。 */

/** 组内成员（物品 / 地板）的世界坐标中心 — 「＋ 添加起点」使用。 */
function memberCenterPos(group: AnimGroup): { x: number; z: number } | null {
  let sx = 0;
  let sz = 0;
  let n = 0;
  for (const id of group.itemInstanceIds) {
    const it = S.items.find((i) => i.instanceId === id);
    if (it) {
      sx += it._wx;
      sz += it._wz;
      n++;
    }
  }
  for (const id of group.floorInstanceIds) {
    const f = S.floors.find((fl) => fl.instanceId === id);
    if (f) {
      sx += f._wx;
      sz += f._wz;
      n++;
    }
  }
  if (!n) return null;
  return { x: sx / n, z: sz / n };
}

/** 统一「路线」编辑器：单一路线列表（行内停留 / 微调 / 移除）+ 可折叠路点池。
 *  放置的路点默认自动编入当前事件路线，取消「放置自动编入」则进池复用。 */
function routeEditorHtml(
  group: AnimGroup,
  evt: AnimGroupEvent,
  evtIdx: number,
  selWp: { x: number; z: number } | undefined,
  selInRoute: boolean
): string {
  const wpModeOn = S.animMode === "waypoints";
  const step = S.freeSnapStep;
  const wpMap = new Map(group.waypoints.map((w) => [w.id, w]));
  const routeN = evt.waypointIds?.length ?? 0;

  const nudgeBtns = () =>
    `<span class="wp-nudge-inline" title="按全局精度 ${step} 微调">
      <button type="button" class="btn-small" data-wp-nudge="${-step},0" title="左移 ${step}">←</button>
      <button type="button" class="btn-small" data-wp-nudge="0,${step}" title="上移 ${step}">↑</button>
      <button type="button" class="btn-small" data-wp-nudge="0,${-step}" title="下移 ${step}">↓</button>
      <button type="button" class="btn-small" data-wp-nudge="${step},0" title="右移 ${step}">→</button>
    </span>`;

  // 路线行：按顺序移动，行内可设停留 / 微调 / 移除（保留在池中）。
  let routeRows = "";
  if (routeN > 0) {
    evt.waypointIds!.forEach((wpId, ri) => {
      const wp = wpMap.get(wpId);
      const sel = wpId === S.selectedWaypointId;
      const alsoEvts = wp
        ? group.events
            .map((e, ei) => (ei !== evtIdx && e.type === "move" && e.waypointIds?.includes(wpId) ? ei + 1 : 0))
            .filter((n) => n > 0)
        : [];
      const alsoTxt =
        alsoEvts.length > 0
          ? ` <span class="wp-also" title="该路点同时被其他事件复用">复用${alsoEvts.map((n) => `事件${n}`).join("/")}</span>`
          : "";
      routeRows += `<div class="event-route-item${sel ? " active" : ""}" data-wp-id="${wpId}">
        <span class="wp-dot" style="background:${wpId ? waypointColor(wpId) : "#666"}"></span>
        <span class="wp-idx">#${wp ? group.waypoints.indexOf(wp) + 1 : "?"}</span>
        <span class="wp-pos">${wp ? `(${wp.x.toFixed(2)}, ${wp.z.toFixed(2)})` : "?"}</span>
        <span class="wp-wait-inline" title="到达该路点后停留的秒数">停<input type="number" class="wp-wait-input" data-wp-wait="${wpId}" value="${wp?.wait ?? 0}" step="0.5" min="0" />s</span>
        ${alsoTxt}
        ${sel ? nudgeBtns() : ""}
        <button type="button" class="btn-del-sm" data-route-del="${evtIdx}:${ri}" title="从路线移除（路点保留在池中）">×</button>
      </div>`;
    });
    if (routeN === 1) {
      routeRows += `<div class="event-route-empty">⚠ 只有 1 个路点物体不会移动 — 继续放置第 2 个路点</div>`;
    }
  } else {
    routeRows = `<div class="event-route-empty">⚠ 路线为空 — 点「📍 放置路点」后在画布上点击放置</div>`;
  }

  // 路点池（默认折叠）：未编入当前路线的路点，可复用或删除。
  const pooled = group.waypoints.filter((w) => !evt.waypointIds?.includes(w.id));
  const poolOpen = S.openWaypointPool.has(evtIdx);
  let poolHtml = "";
  if (pooled.length > 0) {
    poolHtml = `<div class="anim-wp-pool">
      <div class="anim-wp-pool-head" data-pool-toggle="${evtIdx}">
        <span class="subgroup-arrow">${poolOpen ? "▾" : "▸"}</span>
        🗃 路点池 (${pooled.length})${poolOpen ? "" : " · 点击展开"}
      </div>
      ${poolOpen ? `<div class="anim-wp-pool-body">
        ${pooled
          .map((wp) => {
            const sel = wp.id === S.selectedWaypointId;
            return `<div class="anim-wp-row${sel ? " active" : ""}" data-wp-id="${wp.id}">
              <span class="wp-dot" style="background:${waypointColor(wp.id)}"></span>
              <span class="wp-idx">#${group.waypoints.indexOf(wp) + 1}</span>
              <span class="wp-pos">(${wp.x.toFixed(2)}, ${wp.z.toFixed(2)})</span>
              ${sel ? nudgeBtns() : ""}
              <button type="button" class="btn-small" data-pool-add="${evtIdx}:${wp.id}" title="把该路点加入本事件路线">＋ 编入</button>
              <button type="button" class="btn-del" data-del-wp="${wp.id}" title="删除路点">×</button>
            </div>`;
          })
          .join("")}
      </div>` : ""}
    </div>`;
  }

  return `<div class="anim-routes-toolbar">
      <button type="button" class="mode-btn${wpModeOn ? " on" : ""}" id="btn-mode-waypoints" title="点击空白放置路点；点击 / 拖拽路点选中移动">${wpModeOn ? "✓ 放置中（点击空白放点）" : "📍 放置路点"}</button>
      ${wpModeOn ? `<button type="button" class="btn-small" id="btn-exit-mode">退出</button>` : ""}
      <button type="button" class="btn-small pick-add btn-add-start-wp" data-evt="${evtIdx}" title="在当前成员的中心位置添加一个起点路点，并自动编入本事件路线开头">＋ 起点（成员中心）</button>
      <button type="button" class="btn-small" data-add-selected="${evtIdx}"${selWp && !selInRoute ? "" : " disabled"} title="把画布上选中的路点加入本事件路线">＋ 编入选中</button>
      <label class="check route-auto-wrap" title="放置的路点自动按顺序编入本事件路线（取消勾选则放入路点池）">
        <input type="checkbox" class="route-auto-add" data-idx="${evtIdx}"${S.animRouteAutoAdd ? " checked" : ""} /> 放置自动编入
      </label>
    </div>
    <div class="event-route-list">${routeRows}</div>
    ${poolHtml}
    <div class="anim-wp-hint">放置路点自动按顺序编入路线；点击 / 拖拽路点选中后可用行内方向按钮微调；Del 删除选中路点。</div>`;
}

/** 事件类型参数区（属性面板用，与原事件卡一致）。 */
function eventSettingsHtml(evt: AnimGroupEvent, i: number): string {
  if (evt.type === "move") {
    return `<label>每步间隔 (秒)<input type="number" class="event-interval" data-idx="${i}" value="${evt.intervalSeconds ?? 2}" step="0.1" min="0.1" /></label>
     <label class="check" title="与「往返循环」互斥"><input type="checkbox" class="event-loop" data-idx="${i}"${evt.loop && !evt.pingpong ? " checked" : ""} /> 循环播放本段（到终点跳回起点，仅最后事件生效）</label>
     <label class="check" title="与「循环播放本段」互斥"><input type="checkbox" class="event-pingpong" data-idx="${i}"${evt.pingpong ? " checked" : ""} /> 往返循环（到终点后原路返回，仅最后事件生效）</label>
     <div class="sub anim-wp-hint">位于「抬起」事件之后时，本事件自动按抬起高度半空移动，无需配置高度。</div>`;
  }
  if (evt.type === "lift" || evt.type === "drop") {
    return `<label>${evt.type === "lift" ? "抬升高度 ΔY" : "下降高度 ΔY（0 = 落回原高度）"}<input type="number" class="event-yto" data-idx="${i}" value="${evt.yTo ?? (evt.type === "lift" ? 1 : 0)}" step="0.1" /></label>
     <label>升降用时 (秒)<input type="number" class="event-ysec" data-idx="${i}" value="${evt.liftSeconds ?? 1}" step="0.1" min="0.05" /></label>
     <div class="muted anim-member-empty">${evt.type === "lift" ? "纯抬起事件：每个成员从自身当前高度原地升起 ΔY（台上物品也会升），不移动。" : "纯落下事件：每个成员从自身当前高度落下 ΔY；0 = 落回抬起前的高度。"}抬起 / 落下之间不允许互相重叠（拖动 / 改时间会自动避让）；与移动等其他事件可正常重叠。</div>`;
  }
  if (evt.type === "rotate") {
    return `<label>角度 (0~360°)<input type="number" class="event-rot-deg" data-idx="${i}" value="${evt.rotateDegrees ?? 180}" step="1" min="0" max="360" /></label>
     <label>方向
        <select class="event-rot-dir" data-idx="${i}">
          <option value="cw"${evt.rotateDirection !== "ccw" ? " selected" : ""}>顺时针</option>
          <option value="ccw"${evt.rotateDirection === "ccw" ? " selected" : ""}>逆时针</option>
        </select>
      </label>
     <label>用时 (秒)<input type="number" class="event-rot-sec" data-idx="${i}" value="${evt.rotateSeconds ?? 2}" step="0.1" min="0.1" /></label>
     <label class="check" title="播完后立即重播（仅最后时间簇生效）"><input type="checkbox" class="event-loop" data-idx="${i}"${evt.loop ? " checked" : ""} /> 循环旋转</label>
     <div class="muted anim-member-empty">每个成员绕自身中心 Y 轴自转；与移动事件时间重叠即可「边移动边旋转」。</div>`;
  }
  if (evt.type === "shake") {
    return `<label>抖动幅度 (米)<input type="number" class="event-shake-amp" data-idx="${i}" value="${evt.shakeAmplitude ?? 0.15}" step="0.05" min="0.01" /></label>
     <label>抖动时长 (秒)<input type="number" class="event-duration" data-idx="${i}" value="${evt.duration ?? 2}" step="0.1" min="0.1" /></label>
     <label class="check" title="播完后立即重播（仅最后时间簇生效）—— 持续地震"><input type="checkbox" class="event-loop" data-idx="${i}"${evt.loop ? " checked" : ""} /> 持续抖动（循环）</label>`;
  }
  if (evt.type === "flash") {
    return `<label>峰值强度<input type="number" class="event-flash-intensity" data-idx="${i}" value="${evt.flashIntensity ?? 4}" step="0.5" min="0.1" /></label>
     <label>闪光时长 (秒)<input type="number" class="event-duration" data-idx="${i}" value="${evt.duration ?? 0.8}" step="0.1" min="0.1" /></label>
     <label>闪电颜色<input type="color" class="event-flash-color" data-idx="${i}" value="${/^#[0-9a-fA-F]{6}$/.test(evt.flashColor ?? "") ? evt.flashColor : "#DEE1FF"}" /></label>
     <label>雷声事件名（空 = 无声）<input type="text" class="event-sound-cue" data-idx="${i}" value="${escHtml(evt.soundCue ?? "DynamicStage01Thunder")}" placeholder="如 DynamicStage01Thunder" /></label>
     <label class="check" title="播完后立即重播（仅最后时间簇生效）—— 频闪风暴"><input type="checkbox" class="event-loop" data-idx="${i}"${evt.loop ? " checked" : ""} /> 持续频闪（循环）</label>`;
  }
  return `<label>停顿时长 (秒)<input type="number" class="event-duration" data-idx="${i}" value="${evt.duration ?? 1}" step="0.1" min="0.1" /></label>
     <div class="muted anim-member-empty">等待事件 = 时间轴上的停顿段（成员保持不动）。</div>`;
}

/** 事件属性面板（时间轴下方）：类型 / 开始时间 / 类型参数 / 路线 / 成员组。 */
function renderEventInspector(
  group: AnimGroup,
  i: number,
  selWp: { x: number; z: number } | undefined,
  selInRoute: boolean
): string {
  const evt = group.events[i];
  if (!evt) {
    return `<div class="anim-tl-inspector"><div class="muted anim-member-empty">点击时间轴上的事件块查看 / 编辑属性；拖块改开始时间，拖右边缘改时长。</div></div>`;
  }
  const tcol = EVENT_TYPE_COLORS[evt.type];
  const typeChip = `<span class="evt-chip" style="background:${colorA(tcol, 0.16)};color:${tcol};border-color:${tcol}">${eventTypeLabel(evt.type)}</span>`;
  const tStart = eventStart(evt);
  const tEnd = snapTime(tStart + eventDisplayDuration(group, evt));
  const routeN = evt.type === "move" ? (evt.waypointIds?.length ?? 0) : 0;
  const routeEmpty = evt.type === "move" && routeN === 0;

  let routeHtml = "";
  if (evt.type === "move") {
    routeHtml = `<div class="subgroup-section">
      <div class="subgroup-section-title">📍 路线（按顺序移动）</div>
      ${routeEditorHtml(group, evt, i, selWp, selInRoute)}
    </div>`;
    const curveOpen = S.openCurves.has(i);
    routeHtml += `<div class="subgroup-section">
      <button type="button" class="route-curve-toggle" data-curve-toggle="${i}">
        <span class="subgroup-arrow">${curveOpen ? "▾" : "▸"}</span>
        ⏱ 移动时间曲线（clip 内部时间 · 拖动：圆点=段时长 · 绿块=停留）
      </button>
      ${curveOpen ? `<canvas class="route-curve" data-curve-idx="${i}" width="${CURVE_W}" height="${CURVE_H}"></canvas>
      <div class="route-curve-info" id="route-curve-info-${i}">${routeCurveInfoText(group, i)}</div>` : ""}
    </div>`;
  }

  const loopMark = evt.loop ? " ⟲" : evt.pingpong ? " ⇄" : "";
  // 类型下拉按组类型过滤：特效组只 offer 特效类型，成员组只 offer 成员动画类型。
  const fx = isFxGroup(group);
  const typeOptions = fx
    ? `<option value="flash"${evt.type === "flash" ? " selected" : ""}>闪电</option>
            <option value="shake"${evt.type === "shake" ? " selected" : ""}>全屏抖动</option>
            <option value="wait"${evt.type === "wait" ? " selected" : ""}>等待</option>`
    : `<option value="move"${evt.type === "move" ? " selected" : ""}>移动</option>
            <option value="rotate"${evt.type === "rotate" ? " selected" : ""}>旋转</option>
            <option value="lift"${evt.type === "lift" ? " selected" : ""}>抬起</option>
            <option value="drop"${evt.type === "drop" ? " selected" : ""}>落下</option>
            <option value="wait"${evt.type === "wait" ? " selected" : ""}>等待</option>`;
  return `<div class="anim-tl-inspector" data-event-idx="${i}">
    <div class="anim-tl-insp-head">
      ${typeChip}
      <span class="subgroup-title">事件 ${i + 1}${evt.triggerName ? ` (${escHtml(evt.triggerName)})` : ""}</span>
      <span class="subgroup-meta" style="color:${tcol}">${tStart.toFixed(1)}s~${tEnd.toFixed(1)}s${loopMark}</span>
      ${routeEmpty ? `<span class="anim-tl-warn-inline">⚠ 路线为空</span>` : ""}
      <button type="button" class="btn-del" data-del-event="${i}" title="删除事件">×</button>
    </div>
    <div class="subgroup-section">
      <div class="subgroup-section-title">⚙ 设置</div>
      <div class="event-type-row">
        <label>类型
          <select class="event-type" data-idx="${i}">
            ${typeOptions}
          </select>
        </label>
        <label title="时间轴上的绝对开始时间（0.1s 对齐）；与其他事件时间重叠即并行播放">开始时间 (秒)<input type="number" class="event-start" data-idx="${i}" value="${tStart}" step="0.1" min="0" /></label>
      </div>
      ${eventSettingsHtml(evt, i)}
    </div>
    ${routeHtml}
    ${fx ? "" : `<div class="subgroup-section">
      <div class="subgroup-section-title">🧩 成员组（随事件运动 · 点击展开成员）</div>
      ${eventMemberTreeHtml(group)}
    </div>`}
  </div>`;
}

/** 🎬 时间轴 tab：秒标尺 + 泳道事件块 + 可拖播放头 + 属性面板。 */
function renderTimelineTab(
  group: AnimGroup,
  selEvtIdx: number | null,
  selWp: { x: number; z: number } | undefined,
  selInRoute: boolean
): string {
  const items = timelineLanes(group);
  const laneCount = Math.max(1, items.reduce((m, it) => Math.max(m, it.lane + 1), 0));
  // 时间轴总长 = 全部事件的最晚结束时间 + 2s 余量（最小 10s），缩放只改像素密度。
  const total = Math.max(10, items.reduce((m, it) => Math.max(m, it.end), 0) + 2);
  const pps = tlPps();
  const width = Math.round(total * pps);
  const t = timelinePlayheadTime(group);
  const playing = !!S.animPreview && S.animPreview.groupId === group.id && S.animPreview.playing;

  // 并行语义警告：单占用类事件（移动/升降/全屏特效——同一宿主同一时刻只有
  // 一个位姿/状态生效）时间重叠时后者/前者覆盖；waitForFinished 下并行会被顺延。
  const fxGrp = isFxGroup(group);
  const moveItems = items.filter((it) => it.evt.type !== "rotate" && it.evt.type !== "wait");
  let moveOverlap = false;
  for (let a = 0; a < moveItems.length && !moveOverlap; a++) {
    for (let b = a + 1; b < moveItems.length; b++) {
      if (moveItems[b].start >= moveItems[a].end) break;
      moveOverlap = true;
      break;
    }
  }
  let warnHtml = "";
  if (moveOverlap) {
    warnHtml += fxGrp
      ? `<div class="anim-tl-warn">⚠ 特效事件时间重叠：重叠时只有最先开始的那个生效（其余被忽略），请错开时间 —— 间隔即节奏。</div>`
      : `<div class="anim-tl-warn">⚠ 移动 / 升降类事件时间重叠：并行时后开始的事件会接管成员位置，建议错开或只保留一个。</div>`;
  }
  if (group.waitForFinished && items.some((it, k) => k > 0 && it.start < items[k - 1].end)) {
    warnHtml += `<div class="anim-tl-warn">⚠ 已勾选「等待动画播完再触发下一事件」：游戏内重叠事件会顺延（按上一 clip 播完起算），与编辑器预览不一致。</div>`;
  }
  // 循环 / 往返仅在「最后一个时间簇的唯一事件」上生效（烘焙为 Animator 自转移
  // 重放，与 BuildController 同一规则）：并行簇内或非末尾事件的循环标记在游戏
  // 内会被忽略（编辑器预览仍会循环）。在此显性警告，避免写回后“循环丢失”。
  {
    const clusterEnds: number[] = [];
    const clusterOf: number[] = [];
    for (const it of items) {
      let ci = clusterEnds.length - 1;
      if (ci < 0 || it.start >= clusterEnds[ci] - 0.0001) {
        clusterEnds.push(it.end);
        ci = clusterEnds.length - 1;
      } else if (it.end > clusterEnds[ci]) {
        clusterEnds[ci] = it.end;
      }
      clusterOf[it.idx] = ci;
    }
    const clusterSizes = new Map<number, number>();
    for (const ci of clusterOf) clusterSizes.set(ci, (clusterSizes.get(ci) ?? 0) + 1);
    const lastCluster = clusterEnds.length - 1;
    const bad = items.filter((it) => {
      if (!it.evt.loop && !it.evt.pingpong) return false;
      if (it.evt.type !== "move" && it.evt.type !== "rotate" &&
          it.evt.type !== "shake" && it.evt.type !== "flash") return true;
      return clusterOf[it.idx] !== lastCluster || (clusterSizes.get(clusterOf[it.idx]) ?? 0) > 1;
    });
    if (bad.length > 0) {
      const names = bad.map((it) => `事件${it.idx + 1}`).join("、");
      warnHtml += `<div class="anim-tl-warn">⚠ ${names} 的循环 / 往返只在「最后一个独立事件」上生效：当前位置在游戏内会被忽略（仅编辑器预览循环）。请把它移到时间轴末尾并与其他事件错开。</div>`;
    }
  }

  // 标尺刻度随缩放自适应（缩得太小则按 2s/5s/10s 标注，放得大则 0.5s 标注）。
  const tickStep = tlTickStep(pps);
  let ticks = "";
  for (let sec = 0; sec <= total; sec += tickStep) {
    const label = tickStep < 1 && sec % 1 !== 0 ? `${sec.toFixed(1)}s` : `${Math.round(sec)}s`;
    ticks += `<span class="anim-tl-tick" style="left:${sec * pps}px">${label}</span>`;
  }
  // 网格线随缩放走（CSS 里的 48px 固定网格只对应 100% 缩放，这里内联覆盖）。
  const minorGrid = pps >= 20
    ? `repeating-linear-gradient(to right, rgba(255,255,255,0.04) 0 1px, transparent 1px ${pps * 0.1}px),`
    : "";
  const gridBg = `${minorGrid} repeating-linear-gradient(to right, rgba(255,255,255,0.12) 0 1px, transparent 1px ${pps}px)`;

  let blocks = "";
  for (const it of items) {
    const { evt, idx } = it;
    const col = EVENT_TYPE_COLORS[evt.type];
    const dur = it.end - it.start;
    const meta =
      evt.type === "move"
        ? `${evt.waypointIds?.length ?? 0}点`
        : evt.type === "lift"
          ? `↑${evt.yTo ?? 1}`
          : evt.type === "drop"
            ? `↓${evt.yTo || "回原"}`
            : evt.type === "rotate"
              ? `${evt.rotateDirection === "ccw" ? "↺" : "↻"}${evt.rotateDegrees ?? 180}°`
              : evt.type === "shake"
                ? `±${(evt.shakeAmplitude ?? 0.15).toFixed(2)}m`
                : evt.type === "flash"
                  ? `⚡${evt.flashIntensity ?? 4}`
                  : `停${(evt.duration ?? 1).toFixed(1)}s`;
    const badge = evt.loop ? " ⟲" : evt.pingpong ? " ⇄" : "";
    blocks += `<div class="anim-tl-block${selEvtIdx === idx ? " sel" : ""}" data-tl-evt="${idx}"
      style="left:${it.start * pps}px;top:${it.lane * 30}px;width:${Math.max(18, dur * pps - 3)}px;background:${colorA(col, 0.28)};border-color:${col}"
      title="${idx + 1}·${eventTypeLabel(evt.type)} ${it.start.toFixed(1)}s ~ ${it.end.toFixed(1)}s（拖动=移动 · 右边缘=时长）">
      <span class="anim-tl-block-label">${idx + 1}·${eventTypeLabel(evt.type)}${badge}</span>
      <span class="anim-tl-block-meta">${meta} ${dur.toFixed(1)}s</span>
      <span class="anim-tl-handle r" data-tl-resize="${idx}" title="拖动改时长"></span>
    </div>`;
  }

  return `<div class="anim-section anim-timeline-panel">
    ${sectionTitle("🎬", "时间轴（0.1s 对齐 · 重叠即并行）", `${group.events.length} 个事件`, "#d8703c")}
    <div class="anim-tl-toolbar">
      <button type="button" class="btn-small" id="btn-tl-rewind" title="回到 0s">⏮</button>
      <button type="button" class="btn-small${playing ? " preview-on" : ""}" id="btn-tl-play" title="播放 / 暂停（空格）">${playing ? "⏸" : "▶"}</button>
      <button type="button" class="btn-small" id="btn-tl-step-back" title="后退 0.1s">−0.1</button>
      <button type="button" class="btn-small" id="btn-tl-step-fwd" title="前进 0.1s">＋0.1</button>
      <span class="anim-tl-time" id="anim-tl-time">${t.toFixed(1)}s / ${groupTimelineDuration(group).toFixed(1)}s</span>
      <span class="anim-tl-toolbar-sp"></span>
      <button type="button" class="btn-small" id="btn-tl-zoom-out" title="缩小时间轴（Ctrl/⌘+滚轮）">🔍−</button>
      <span class="anim-tl-zoom" id="anim-tl-zoom" title="当前缩放">${Math.round(tlZoom * 100)}%</span>
      <button type="button" class="btn-small" id="btn-tl-zoom-in" title="放大时间轴（Ctrl/⌘+滚轮）">🔍＋</button>
      <button type="button" class="btn-small" id="btn-tl-fit" title="缩放适配：把全部事件聚焦到可视区">⤢ 聚焦</button>
      <button type="button" class="btn-small primary" id="btn-add-event">＋ 添加事件</button>
    </div>
    ${warnHtml}
    <div class="anim-tl-scroll">
      <div class="anim-tl-canvas" style="width:${width}px">
        <div class="anim-tl-ruler" style="background-image:${gridBg}" title="点击 / 拖动移动播放头（scrub）">${ticks}</div>
        <div class="anim-tl-lanes" style="height:${laneCount * 30 + 4}px;background-image:${gridBg}" title="左右拖动 = 平移视图">${blocks}</div>
        <div class="anim-tl-playhead" id="anim-tl-playhead" style="left:${t * pps}px"></div>
      </div>
    </div>
    <div class="anim-wp-hint">拖动事件块改开始时间；拖右边缘改时长（移动事件按「每步间隔 × 步数」推算总时长，时间曲线自定义过则按比例缩放各段）；点块选中后在下方编辑；点标尺 / 拖播放头逐帧预览；空格播放；标尺下空白区左右拖动平移，Ctrl/⌘+滚轮缩放，⤢ 聚焦全部事件。</div>
    ${renderEventInspector(group, selEvtIdx ?? -1, selWp, selInRoute)}
  </div>`;
}

/** 触发器下拉：选项 = 中文 + 英文；已填的自定义值保留为额外选项。 */
function triggerSelectHtml(
  cls: string,
  value: string | undefined,
  opts: [string, string][],
  emptyLabel: string
): string {
  const known = opts.map(([v]) => v);
  const optHtml = opts
    .map(([v, label]) => `<option value="${escHtml(v)}"${value === v ? " selected" : ""}>${escHtml(label)}（${escHtml(v)}）</option>`)
    .join("");
  const extra = value && !known.includes(value)
    ? `<option value="${escHtml(value)}" selected>自定义 · ${escHtml(value)}</option>`
    : "";
  return `<select class="${cls}"><option value="">${escHtml(emptyLabel)}</option>${optHtml}${extra}</select>`;
}

/** 📍 路点管理：区分已使用（被事件路线 / 成员跟随引用）与未使用路点。 */
function renderWaypointsTab(group: AnimGroup): string {
  interface UseRef {
    evtIdx: number;
    pos: number;
  }
  // 事件路线引用。
  const usage = new Map<string, UseRef[]>();
  group.events.forEach((evt, ei) => {
    if (evt.type !== "move" || !evt.waypointIds) return;
    evt.waypointIds.forEach((wpId, ri) => {
      if (!usage.has(wpId)) usage.set(wpId, []);
      usage.get(wpId)!.push({ evtIdx: ei, pos: ri });
    });
  });
  // 成员「跟随路点」引用。
  const followCount = new Map<string, number>();
  for (const o of group.memberOffsets ?? []) {
    if (!o.followWaypointId) continue;
    followCount.set(o.followWaypointId, (followCount.get(o.followWaypointId) ?? 0) + 1);
  }
  const isUsed = (id: string) => usage.has(id) || followCount.has(id);

  const wpRow = (wp: AnimGroupWaypoint, usedFlag: boolean): string => {
    const sel = wp.id === S.selectedWaypointId;
    const refs = usage.get(wp.id) ?? [];
    const fc = followCount.get(wp.id);
    const num = group.waypoints.indexOf(wp) + 1;
    const useTxt = usedFlag
      ? refs
          .map(
            (r) =>
              `<span class="wp-usage" title="事件 ${r.evtIdx + 1} 路线第 ${r.pos + 1} 个">事件${r.evtIdx + 1}·#${r.pos + 1}</span>`
          )
          .join("") +
        (fc ? `<span class="wp-usage follow" title="${fc} 个成员跟随此路点">跟随×${fc}</span>` : "")
      : `<span class="wp-usage unused">未使用</span>`;
    const addBtn =
      !usedFlag && S.activeAnimEventIdx !== null && group.events[S.activeAnimEventIdx]?.type === "move"
        ? `<button type="button" class="btn-small" data-pool-add="${S.activeAnimEventIdx}:${wp.id}" title="把该路点加入当前事件路线">＋ 编入当前事件</button>`
        : "";
    return `<div class="anim-wp-row${sel ? " active" : ""}" data-wp-id="${wp.id}">
      <span class="wp-dot" style="background:${waypointColor(wp.id)}"></span>
      <span class="wp-idx">#${num}</span>
      <span class="wp-pos">(${wp.x.toFixed(2)}, ${wp.z.toFixed(2)})</span>
      ${useTxt}
      <span class="wp-wait-inline" title="到达该路点后停留的秒数">停<input type="number" class="wp-wait-input" data-wp-wait="${wp.id}" value="${wp.wait ?? 0}" step="0.5" min="0" />s</span>
      ${addBtn}
      <button type="button" class="btn-del" data-del-wp="${wp.id}" title="删除路点">×</button>
    </div>`;
  };

  const used = group.waypoints.filter((w) => isUsed(w.id));
  const unused = group.waypoints.filter((w) => !isUsed(w.id));
  const section = (title: string, rows: string, empty: string) =>
    `<div class="wp-manage-section">
      <div class="wp-manage-head">${title}</div>
      ${rows || `<div class="muted anim-member-empty">${empty}</div>`}
    </div>`;

  return `<div class="anim-section">
    ${sectionTitle("📍", "路点管理", `${group.waypoints.length} 个`, "#3d6bf3")}
    <div class="wp-manage-summary">
      <span class="wp-usage-sum used">已使用 ${used.length}</span>
      <span class="wp-usage-sum unused">未使用 ${unused.length}</span>
    </div>
    ${section("已使用（被事件路线 / 成员跟随引用）", used.map((w) => wpRow(w, true)).join(""), "（无）")}
    ${section("未使用（不会参与移动）", unused.map((w) => wpRow(w, false)).join(""), "（无 — 全部路点都已编入）")}
    <div class="anim-wp-hint">已使用 = 在某个事件的路线里，或被成员「跟随」；未使用的路点不参与移动，可「＋ 编入当前事件」或删除。点击行可选中画布上的路点。</div>
  </div>`;
}

function renderSettingsTab(group: AnimGroup): string {
  const boundLink = linkBindingGroup(group.displayName);
  const boundHint = boundLink
    ? `<div class="sub anim-wp-hint" style="color:#e8b35a">⚠ 该组已被按钮联动绑定：不再自动执行，启动/结束触发器由联动自动管理（在此处手动修改无效，写回时会被联动覆盖）。</div>`
    : "";
  return `<div class="anim-section">
    ${sectionTitle("⚙", "组设置", "", "#8b93a3")}
    <div class="anim-settings-grid">
      <label>启动延迟 (秒)<input type="number" class="group-start-delay" value="${group.startDelay}" step="0.1" min="0" /></label>
      <label class="check"><input type="checkbox" class="group-loop"${group.loop ? " checked" : ""} /> 循环整个序列</label>
      <label class="group-loop-delay-wrap"${group.loop ? "" : ' style="display:none"'}>循环间隔 (秒)<input type="number" class="group-loop-delay" value="${group.loopDelay}" step="0.1" min="0" /></label>
    </div>
  </div>
  <div class="anim-section">
    ${sectionTitle("🔀", "触发器 / 动画（TriggerQueue · Animator）", "", "#4f8fd6")}
    ${boundHint}
    <div class="anim-settings-grid">
      <label class="check"><input type="checkbox" class="group-wait-finished"${group.waitForFinished ? " checked" : ""} /> 等待动画播完再触发下一事件</label>
      <div class="sub anim-wp-hint">勾选后：每个移动事件播完（到达终点）才触发下一事件；循环片段不会发送完成信号。</div>
      <label>启动触发器（空 = 开局自动 / 延迟启动）${triggerSelectHtml("group-start-trigger", group.startTrigger, [["Start", "启动（= 自动启动）"], ["RoundStart", "回合开始"], ["GameStart", "游戏开始"]], "（空 · 开局自动 / 延迟启动）")}</label>
      <label>取消触发器（收到后中止队列）${triggerSelectHtml("group-cancel-trigger", group.cancelTrigger, [["StopMoving", "停止移动"], ["Stop", "停止"], ["Cancel", "取消"]], "（无）")}</label>
      <label>队列结束触发器（全部事件完成后广播）${triggerSelectHtml("group-end-trigger", group.endTrigger, [["OpenDoor", "开门"], ["DoorOpen", "门打开"], ["RoundStart", "回合开始"]], "（无）")}</label>
      <label>完成回调触发器名（默认 AnimationFinished）${triggerSelectHtml("group-finished-trigger", group.finishedTrigger, [["AnimationDone", "动画完成"]], "AnimationFinished（默认）")}</label>
      <label class="check"><input type="checkbox" class="group-root-motion"${group.applyRootMotion ? " checked" : ""} /> 应用根运动 (Animator.applyRootMotion)</label>
    </div>
  </div>
  <div class="anim-danger">
    <button type="button" class="btn-small btn-danger" id="btn-del-move">🗑 删除动画组</button>
    <span class="anim-wp-hint">删除后该组的所有路线与事件一并移除，物品保留在场景中。</span>
  </div>`;
}

function renderGroupEditor(body: HTMLElement, group: AnimGroup): void {
  syncPreview(group);
  newMgFormOpen = false;
  const col = groupColor(group.id);
  const fxGrp = isFxGroup(group);
  // 特效组没有成员/路点概念：强制回到时间轴（旧 UI 状态防御）。
  if (fxGrp && S.activeAnimTab !== "timeline" && S.activeAnimTab !== "settings") {
    S.activeAnimTab = "timeline";
  }
  const selEvtIdx = S.activeAnimEventIdx;
  const activeEvt = selEvtIdx !== null ? group.events[selEvtIdx] : undefined;
  const selWp = S.selectedWaypointId
    ? group.waypoints.find((w) => w.id === S.selectedWaypointId)
    : undefined;
  const selInRoute = !!(
    selWp &&
    activeEvt &&
    activeEvt.waypointIds?.includes(selWp.id)
  );
  const previewOn = !!S.animPreview && S.animPreview.groupId === group.id && S.animPreview.playing;
  const memberCount = flatMemberIds(group).length;
  const fxType = groupFxType(group);
  const fxLabel = fxType === "shake" ? "🌍 全屏抖动" : fxType === "flash" ? "⚡ 闪电" : "✨ 特效";

  let html = `<div class="anim-editor-head">
    <button type="button" class="btn-small" id="btn-anim-back">◀ 返回</button>
    <span class="anim-editor-color" style="background:${col}"></span>
    <input type="text" id="group-name" value="${escHtml(group.displayName)}" title="组名（回车生效）" />
  </div>`;

  html += `<div class="anim-editor-summary">
    <span>${fxGrp
      ? `${fxLabel} · 🔁 ${group.events.length} 事件`
      : `🧩 ${memberCount} 成员 · 📍 ${group.waypoints.length} 路点 · 🔁 ${group.events.length} 事件`}</span>
    <button type="button" class="btn-small${previewOn ? " preview-on" : ""}" id="btn-preview" title="${fxGrp ? "在画布上模拟全屏特效（抖动 / 闪光，纯前端预览）" : "在画布上模拟成员沿路线运动（纯前端预览，写回后以游戏内为准）"}">${previewOn ? "⏸ 暂停预览" : fxGrp ? "▶ 预览特效" : "▶ 预览路线"}</button>
  </div>`;

  html += `<div class="anim-tabs">
    <button type="button" class="anim-tab${S.activeAnimTab === "timeline" ? " active" : ""}" data-mvtab="timeline">🎬 时间轴 (${group.events.length})</button>
    ${fxGrp ? "" : `<button type="button" class="anim-tab${S.activeAnimTab === "members" ? " active" : ""}" data-mvtab="members">🧩 成员 (${memberCount})</button>
    <button type="button" class="anim-tab${S.activeAnimTab === "waypoints" ? " active" : ""}" data-mvtab="waypoints">📍 路点 (${group.waypoints.length})</button>`}
    <button type="button" class="anim-tab${S.activeAnimTab === "settings" ? " active" : ""}" data-mvtab="settings">⚙ 设置</button>
  </div>`;

  html += `<div class="anim-tab-body">`;
  switch (S.activeAnimTab) {
    case "timeline":
      html += renderTimelineTab(group, selEvtIdx, selWp, selInRoute);
      break;
    case "members":
      html += renderMembersTab(group);
      break;
    case "waypoints":
      html += renderWaypointsTab(group);
      break;
    case "settings":
      html += renderSettingsTab(group);
      break;
  }
  html += `</div>`;

  body.innerHTML = html;
  wireGroupEditor(body, group);
}

function wireGroupEditor(body: HTMLElement, group: AnimGroup): void {
  const refresh = () => {
    renderRightPanel();
    draw();
  };

  body.querySelector("#btn-anim-back")?.addEventListener("click", () => {
    S.activeAnimGroupId = null;
    S.activeAnimEventIdx = null;
    S.selectedWaypointId = null;
    S.animMode = "none";
    S.activeAnimTab = "members";
    S.animPickTargetGroupId = null;
    stopAnimPreview();
    clearAnimSelection();
    updateAnimPickBar();
    refresh();
  });

  // ---- tabs
  body.querySelectorAll<HTMLElement>("[data-mvtab]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const tab = (btn as HTMLElement).dataset.mvtab as "timeline" | "members" | "waypoints" | "settings";
      S.activeAnimTab = tab;
      refresh();
      draw();
    });
  });

  body.querySelector("#btn-preview")?.addEventListener("click", toggleAnimPreview);

  // ---- members tab
  body.querySelector<HTMLSelectElement>("#pick-target")?.addEventListener("change", (e) => {
    S.animPickTargetGroupId = (e.target as HTMLSelectElement).value || null;
    refresh();
  });
  body.querySelector("#btn-add-members")?.addEventListener("click", () => setAnimMode("members"));
  body.querySelector("#btn-new-member-group")?.addEventListener("click", () => {
    newMgFormOpen = true;
    refresh();
    const nameInput = document.getElementById("new-mg-name") as HTMLInputElement | null;
    nameInput?.focus();
  });
  body.querySelector("#btn-mg-create")?.addEventListener("click", () => {
    const nameInput = body.querySelector<HTMLInputElement>("#new-mg-name");
    const name = nameInput?.value.trim() ?? "";
    if (!name) {
      setStatus("请输入成员组名称", false);
      return;
    }
    createMemberGroup(group, name);
    newMgFormOpen = false;
    refresh();
  });
  body.querySelector("#btn-mg-cancel")?.addEventListener("click", () => {
    newMgFormOpen = false;
    refresh();
  });
  body.querySelectorAll<HTMLElement>("[data-toggle-group]").forEach((el) => {
    el.addEventListener("click", (e) => {
      if ((e.target as HTMLElement).closest("button")) return;
      const id = (el as HTMLElement).dataset.toggleGroup!;
      if (S.collapsedGroupIds.has(id)) S.collapsedGroupIds.delete(id);
      else S.collapsedGroupIds.add(id);
      refresh();
    });
  });
  body.querySelectorAll<HTMLElement>("[data-target-group]").forEach((btn) => {
    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      S.animPickTargetGroupId = (btn as HTMLElement).dataset.targetGroup!;
      refresh();
      setStatus("框选 / 点选成员将加入该成员组");
    });
  });
  body.querySelectorAll<HTMLElement>("[data-del-group]").forEach((btn) => {
    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      deleteMemberGroup(group, (btn as HTMLElement).dataset.delGroup!);
      refresh();
    });
  });

  // ---- member chips (event cards) + kid rows
  body.querySelectorAll<HTMLElement>("[data-member-group]").forEach((el) => {
    el.addEventListener("click", (e) => {
      e.stopPropagation();
      const id = (el as HTMLElement).dataset.memberGroup!;
      if (S.collapsedGroupIds.has(id)) S.collapsedGroupIds.delete(id);
      else S.collapsedGroupIds.add(id);
      refresh();
    });
  });
  body.querySelectorAll<HTMLElement>("[data-member-id]").forEach((el) => {
    el.addEventListener("click", (e) => {
      e.stopPropagation();
      const id = (el as HTMLElement).dataset.memberId!;
      // Highlight the whole member: the member itself (when it is a web
      // item/floor) plus every child item under its hierarchy path.
      const path = group.memberOffsets?.find((o) => o.instanceId === id)?.hierarchyPath
        ?? group.memberStatic?.find((m) => m.instanceId === id)?.hierarchyPath;
      const keys = S.items
        .filter((it) => {
          if (it.instanceId === id) return true;
          if (path && it.hierarchyPath) return it.hierarchyPath.startsWith(path + "/");
          return false;
        })
        .map((it) => it._editorKey);
      if (keys.length > 0) setSelection(keys);
      refresh();
      draw();
    });
  });
  body.querySelectorAll<HTMLElement>("[data-kid]").forEach((el) => {
    el.addEventListener("click", (e) => {
      e.stopPropagation();
      const it = S.items.find((i) => i._editorKey === (el as HTMLElement).dataset.kid);
      if (it) {
        setSelection([it._editorKey]);
        ensureItemVisible(it);
        draw();
      }
    });
  });

  // ---- routes (inside event cards)
  body.querySelector("#btn-mode-waypoints")?.addEventListener("click", () => setAnimMode("waypoints"));
  body.querySelector("#btn-exit-mode")?.addEventListener("click", () => exitAnimMode());
  body.querySelectorAll<HTMLElement>(".btn-add-start-wp").forEach((btn) => {
    btn.addEventListener("click", () => {
      const evtIdx = parseInt((btn as HTMLElement).dataset.evt!);
      const evt = group.events[evtIdx];
      const pos = memberCenterPos(group);
      if (!pos) {
        setStatus("组内没有可解析位置的成员（物品 / 地板）", false);
        return;
      }
      pushHistory();
      const wp: AnimGroupWaypoint = {
        id: uuid(),
        x: snapValue(pos.x, S.freeSnapStep),
        z: snapValue(pos.z, S.freeSnapStep),
      };
      group.waypoints.push(wp);
      S.selectedWaypointId = wp.id;
      if (evt && evt.type === "move") {
        if (!evt.waypointIds) evt.waypointIds = [];
        evt.waypointIds.unshift(wp.id);
      }
      S.dirty = true;
      refresh();
      draw();
    });
  });
  body.querySelectorAll<HTMLElement>("[data-wp-nudge]").forEach((btn) => {
    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      const wpId = S.selectedWaypointId;
      const wp = wpId ? group.waypoints.find((w) => w.id === wpId) : undefined;
      if (!wp) return;
      const [dx, dz] = (btn as HTMLElement).dataset.wpNudge!.split(",").map(Number);
      pushHistory();
      wp.x = snapValue(wp.x + (dx || 0), S.freeSnapStep);
      wp.z = snapValue(wp.z + (dz || 0), S.freeSnapStep);
      S.dirty = true;
      refresh();
    });
  });
  body.querySelectorAll<HTMLElement>("[data-del-wp]").forEach((btn) => {
    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      const wpId = btn.dataset.delWp!;
      pushHistory();
      deleteWaypoint(group, wpId);
      refresh();
    });
  });
  // Route + pool rows: clicking selects the waypoint (ignoring inner controls).
  body.querySelectorAll<HTMLElement>(".event-route-item[data-wp-id], .anim-wp-row[data-wp-id]").forEach((row) => {
    row.addEventListener("click", (e) => {
      if ((e.target as HTMLElement).closest("button,input,label")) return;
      S.selectedWaypointId = row.dataset.wpId!;
      refresh();
    });
  });
  // 放置自动编入（每事件工具栏）— 全局开关。
  body.querySelectorAll<HTMLInputElement>(".route-auto-add").forEach((cb) => {
    cb.addEventListener("click", (e) => e.stopPropagation());
    cb.addEventListener("change", () => {
      S.animRouteAutoAdd = cb.checked;
    });
  });
  // 路点池折叠开关。
  body.querySelectorAll<HTMLElement>("[data-pool-toggle]").forEach((el) => {
    el.addEventListener("click", () => {
      const idx = parseInt((el as HTMLElement).dataset.poolToggle!);
      if (S.openWaypointPool.has(idx)) S.openWaypointPool.delete(idx);
      else S.openWaypointPool.add(idx);
      refresh();
    });
  });
  // 池中路点 → 加入当前事件路线。
  body.querySelectorAll<HTMLElement>("[data-pool-add]").forEach((btn) => {
    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      const [ei, wpId] = (btn as HTMLElement).dataset.poolAdd!.split(":");
      const evt = group.events[parseInt(ei)];
      if (!evt || !evt.waypointIds || evt.waypointIds.includes(wpId)) return;
      pushHistory();
      evt.waypointIds.push(wpId);
      S.dirty = true;
      refresh();
    });
  });
  // 时间曲线展开/折叠（默认折叠，点击后才显示）。
  body.querySelectorAll<HTMLElement>("[data-curve-toggle]").forEach((el) => {
    el.addEventListener("click", () => {
      const idx = parseInt((el as HTMLElement).dataset.curveToggle!);
      if (S.openCurves.has(idx)) S.openCurves.delete(idx);
      else S.openCurves.add(idx);
      refresh();
    });
  });
  body.querySelectorAll<HTMLInputElement>("[data-wp-wait]").forEach((inp) => {
    inp.addEventListener("change", (e) => {
      e.stopPropagation();
      const wpId = (inp as HTMLInputElement).dataset.wpWait!;
      const wp = group.waypoints.find((w) => w.id === wpId);
      if (!wp) return;
      const v = parseFloat(inp.value) || 0;
      if (v === (wp.wait ?? 0)) return;
      pushHistory();
      wp.wait = Math.max(0, v);
      S.dirty = true;
      refresh();
    });
    inp.addEventListener("click", (e) => e.stopPropagation());
  });

  // ---- members tab: rename / remove / add
  // 成员跟随路点：固定在指定路点（+偏移），不随路线移动。
  body.querySelectorAll<HTMLSelectElement>(".member-follow-wp").forEach((sel) => {
    sel.addEventListener("click", (e) => e.stopPropagation());
    sel.addEventListener("change", () => {
      const id = sel.dataset.followMember!;
      const v = sel.value || undefined;
      const off = group.memberOffsets?.find((o) => o.instanceId === id);
      if ((off?.followWaypointId ?? undefined) === v) return;
      pushHistory();
      if (!group.memberOffsets) group.memberOffsets = [];
      let o = group.memberOffsets.find((x) => x.instanceId === id);
      if (!o) {
        o = { instanceId: id, x: 0, z: 0 };
        group.memberOffsets.push(o);
      }
      if (v) o.followWaypointId = v;
      else delete o.followWaypointId;
      S.dirty = true;
      refresh();
    });
  });
  const nameInput = body.querySelector<HTMLInputElement>("#group-name");
  nameInput?.addEventListener("change", () => {
    if (nameInput.value.trim() && nameInput.value !== group.displayName) {
      pushHistory();
      renameGroupInButtonLinks(group.displayName, nameInput.value.trim());
      group.displayName = nameInput.value.trim();
      S.dirty = true;
    }
    nameInput.value = group.displayName;
    refresh();
  });
  body.querySelectorAll<HTMLElement>("[data-del-item]").forEach((btn) => {
    btn.addEventListener("click", () => {
      pushHistory();
      removeMember(group, btn.dataset.delItem!);
      S.dirty = true;
      refresh();
    });
  });
  body.querySelectorAll<HTMLElement>("[data-del-floor]").forEach((btn) => {
    btn.addEventListener("click", () => {
      pushHistory();
      removeMember(group, btn.dataset.delFloor!);
      S.dirty = true;
      refresh();
    });
  });
  body.querySelectorAll<HTMLElement>("[data-del-obj]").forEach((btn) => {
    btn.addEventListener("click", () => {
      pushHistory();
      removeMember(group, btn.dataset.delObj!);
      S.dirty = true;
      refresh();
    });
  });
  body.querySelector<HTMLSelectElement>(".group-add-item")?.addEventListener("change", (e) => {
    const id = (e.target as HTMLSelectElement).value;
    if (!id) return;
    pushHistory();
    group.itemInstanceIds.push(id);
    const tgt = pickTarget(group);
    if (tgt && !tgt.memberInstanceIds.includes(id)) tgt.memberInstanceIds.push(id);
    S.dirty = true;
    refresh();
  });
  body.querySelector<HTMLSelectElement>(".group-add-decor")?.addEventListener("change", (e) => {
    const id = (e.target as HTMLSelectElement).value;
    if (!id) return;
    pushHistory();
    group.itemInstanceIds.push(id);
    const tgt = pickTarget(group);
    if (tgt && !tgt.memberInstanceIds.includes(id)) tgt.memberInstanceIds.push(id);
    S.dirty = true;
    refresh();
  });
  body.querySelector<HTMLSelectElement>(".group-add-floor")?.addEventListener("change", (e) => {
    const id = (e.target as HTMLSelectElement).value;
    if (!id) return;
    pushHistory();
    group.floorInstanceIds.push(id);
    // 整岛语义：地板入组时自动连带其矩形上方的物品（工作站/装饰/地砖）。
    const f = S.floors.find((fl) => fl.instanceId === id);
    let linked = 0;
    if (f) {
      const hw = (f._wCells * CELL) / 2 + 0.05;
      const hd = (f._dCells * CELL) / 2 + 0.05;
      for (const it of S.items) {
        if (isBackgroundItem(it) || group.itemInstanceIds.includes(it.instanceId)) continue;
        if (Math.abs(it._wx - f._wx) > hw || Math.abs(it._wz - f._wz) > hd) continue;
        group.itemInstanceIds.push(it.instanceId);
        const tgt2 = pickTarget(group);
        if (tgt2 && !tgt2.memberInstanceIds.includes(it.instanceId)) tgt2.memberInstanceIds.push(it.instanceId);
        linked++;
      }
    }
    const tgt = pickTarget(group);
    if (tgt && !tgt.memberInstanceIds.includes(id)) tgt.memberInstanceIds.push(id);
    S.dirty = true;
    refresh();
    if (linked > 0) setStatus(`地板已入组，自动连带其上方 ${linked} 个物品`);
  });

  // ---- timeline tab: transport + 块拖动 / 时长缩放 / 标尺 scrub / 平移 / 视图缩放
  body.querySelector("#btn-tl-play")?.addEventListener("click", toggleAnimPreview);
  body.querySelector("#btn-tl-rewind")?.addEventListener("click", rewindAnimPreview);
  body.querySelector("#btn-tl-step-back")?.addEventListener("click", () => stepAnimPreview(-TIMELINE_STEP));
  body.querySelector("#btn-tl-step-fwd")?.addEventListener("click", () => stepAnimPreview(TIMELINE_STEP));

  const tlScroller = body.querySelector<HTMLElement>(".anim-tl-scroll");
  if (tlScroller) {
    // 渲染重建后恢复横向滚动位置（缩放 / 平移不打断浏览上下文）。
    tlScroller.scrollLeft = tlSavedScroll;
    tlScroller.addEventListener("scroll", () => {
      tlSavedScroll = tlScroller.scrollLeft;
    });
    // Ctrl/⌘+滚轮 = 以光标处时间点为锚缩放；横向滚轮 / Shift+滚轮 = 平移。
    tlScroller.addEventListener("wheel", (e) => {
      if (e.ctrlKey || e.metaKey) {
        e.preventDefault();
        const rect = tlScroller.getBoundingClientRect();
        const offX = e.clientX - rect.left;
        const anchorT = (tlScroller.scrollLeft + offX) / tlPps();
        tlZoom = Math.min(TL_ZOOM_MAX, Math.max(TL_ZOOM_MIN, tlZoom * (e.deltaY < 0 ? 1.25 : 0.8)));
        tlSavedScroll = Math.max(0, anchorT * tlPps() - offX);
        refresh();
      } else if (e.shiftKey || Math.abs(e.deltaX) > Math.abs(e.deltaY)) {
        e.preventDefault();
        tlScroller.scrollLeft += e.shiftKey ? e.deltaY : e.deltaX;
      }
    }, { passive: false });
    // 工具栏缩放：以可视区中央时间点为锚；聚焦 = 全部事件适配到可视区。
    const zoomBy = (factor: number) => {
      const anchorT = (tlScroller.scrollLeft + tlScroller.clientWidth / 2) / tlPps();
      tlZoom = Math.min(TL_ZOOM_MAX, Math.max(TL_ZOOM_MIN, tlZoom * factor));
      tlSavedScroll = Math.max(0, anchorT * tlPps() - tlScroller.clientWidth / 2);
      refresh();
    };
    body.querySelector("#btn-tl-zoom-in")?.addEventListener("click", () => zoomBy(1.5));
    body.querySelector("#btn-tl-zoom-out")?.addEventListener("click", () => zoomBy(1 / 1.5));
    body.querySelector("#btn-tl-fit")?.addEventListener("click", () => {
      const dur = Math.max(1, groupTimelineDuration(group));
      const fitPps = (tlScroller.clientWidth - 24) / (dur + 1);
      tlZoom = Math.min(TL_ZOOM_MAX, Math.max(TL_ZOOM_MIN, fitPps / TL_PPS));
      tlSavedScroll = 0;
      refresh();
    });
  }

  const tlCanvas = body.querySelector<HTMLElement>(".anim-tl-canvas");
  if (tlCanvas) {
    const timeFromClientX = (clientX: number) => {
      const rect = tlCanvas.getBoundingClientRect();
      return Math.max(0, (clientX - rect.left) / tlPps());
    };
    // 标尺按下 = scrub 播放头（拖动连续更新）；泳道空白区 / 中键 = 左右平移视图。
    tlCanvas.addEventListener("pointerdown", (e) => {
      if ((e.target as HTMLElement).closest(".anim-tl-block")) return;
      const onRuler = !!(e.target as HTMLElement).closest(".anim-tl-ruler");
      if (e.button === 0 && onRuler) {
        e.preventDefault();
        const onMove = (ev: PointerEvent) => scrubAnimPreview(timeFromClientX(ev.clientX));
        const onUp = () => {
          window.removeEventListener("pointermove", onMove);
          window.removeEventListener("pointerup", onUp);
          renderRightPanel();
        };
        window.addEventListener("pointermove", onMove);
        window.addEventListener("pointerup", onUp);
        scrubAnimPreview(timeFromClientX(e.clientX));
        return;
      }
      if ((e.button === 0 && !onRuler) || e.button === 1) {
        if (!tlScroller) return;
        e.preventDefault();
        const startX = e.clientX;
        const startScroll = tlScroller.scrollLeft;
        const onMove = (ev: PointerEvent) => {
          tlScroller.scrollLeft = startScroll - (ev.clientX - startX);
        };
        const onUp = () => {
          window.removeEventListener("pointermove", onMove);
          window.removeEventListener("pointerup", onUp);
        };
        window.addEventListener("pointermove", onMove);
        window.addEventListener("pointerup", onUp);
      }
    });
    // 事件块：拖主体改 startTime，拖右边缘改时长；点击（位移 < 3px）选中。
    tlCanvas.querySelectorAll<HTMLElement>(".anim-tl-block").forEach((block) => {
      block.addEventListener("pointerdown", (e) => {
        if (e.button !== 0) return;
        e.preventDefault();
        e.stopPropagation();
        const idx = parseInt(block.dataset.tlEvt!);
        const evt = group.events[idx];
        if (!evt) return;
        const resizing = !!(e.target as HTMLElement).closest(".anim-tl-handle");
        const origStart = eventStart(evt);
        const origDur = eventDuration(evt, group);
        const pps = tlPps();
        const startX = e.clientX;
        let moved = false;
        let histPushed = false;
        const onMove = (ev: PointerEvent) => {
          const dx = ev.clientX - startX;
          if (!moved && Math.abs(dx) < 3) return;
          if (!histPushed) {
            pushHistory();
            histPushed = true;
          }
          moved = true;
          if (resizing) {
            // 右边缘缩放：lift/drop 之间互不重叠，结束不超过后方最近 lift/drop。
            resizeEventDuration(group, evt, clampExclusiveDuration(group, idx, origDur + dx / pps));
          } else {
            // 拖动：lift/drop 之间强制不重叠（吸附到最近可放置边界）。
            evt.startTime = clampExclusiveStart(group, idx, snapTime(origStart + dx / pps), origDur);
          }
          S.dirty = true;
          const ns = eventStart(evt);
          const nd = eventDisplayDuration(group, evt);
          block.style.left = `${ns * pps}px`;
          block.style.width = `${Math.max(18, nd * pps - 3)}px`;
          draw();
        };
        const onUp = () => {
          window.removeEventListener("pointermove", onMove);
          window.removeEventListener("pointerup", onUp);
          if (!moved) {
            S.activeAnimEventIdx = idx;
          }
          refresh();
          draw();
        };
        window.addEventListener("pointermove", onMove);
        window.addEventListener("pointerup", onUp);
      });
    });
  }

  body.querySelectorAll<HTMLSelectElement>(".event-type").forEach((sel) => {
    sel.addEventListener("change", () => {
      const idx = parseInt(sel.dataset.idx!);
      pushHistory();
      group.events[idx].type = sel.value as AnimGroupEvent["type"];
      group.events[idx].triggerName = undefined;
      const t = group.events[idx].type;
      if (t !== "move" && t !== "rotate" && t !== "shake" && t !== "flash") {
        group.events[idx].loop = false;
        group.events[idx].pingpong = false;
      }
      if (t === "lift" && !group.events[idx].yTo) {
        group.events[idx].yTo = 1;
      }
      if (t === "rotate") {
        if (!group.events[idx].rotateDegrees) group.events[idx].rotateDegrees = 180;
        if (!group.events[idx].rotateSeconds) group.events[idx].rotateSeconds = 2;
      }
      if (t === "shake") {
        if (!group.events[idx].shakeAmplitude) group.events[idx].shakeAmplitude = 0.15;
        if (!group.events[idx].duration) group.events[idx].duration = 2;
      }
      if (t === "flash") {
        if (!group.events[idx].flashIntensity) group.events[idx].flashIntensity = 4;
        if (!group.events[idx].duration) group.events[idx].duration = 0.8;
        if (!group.events[idx].flashColor) group.events[idx].flashColor = "#DEE1FF";
        if (group.events[idx].soundCue == null) group.events[idx].soundCue = "DynamicStage01Thunder";
      }
      // 切换为抬起/落下后：lift 与 drop 之间强制不重叠，落入禁区时吸附到
      // 最近可放置边界（与其他类型事件的重叠不受影响）。
      group.events[idx].startTime = clampExclusiveStart(
        group, idx, eventStart(group.events[idx]), eventDuration(group.events[idx], group));
      S.dirty = true;
      refresh();
    });
  });
  const wireNumInput = (
    cls: string,
    get: (e: AnimGroupEvent) => number,
    set: (e: AnimGroupEvent, v: number) => void
  ) => {
    body.querySelectorAll<HTMLInputElement>(cls).forEach((inp) => {
      inp.addEventListener("change", () => {
        const idx = parseInt(inp.dataset.idx!);
        const v = parseFloat(inp.value);
        if (!isFinite(v)) return;
        const ev = group.events[idx];
        if (v === get(ev)) return;
        pushHistory();
        set(ev, v);
        S.dirty = true;
        refresh();
      });
    });
  };
  wireNumInput(".event-yto", (e) => e.yTo ?? 0, (e, v) => (e.yTo = v));
  wireNumInput(".event-ysec", (e) => e.liftSeconds ?? 1, (e, v) => (e.liftSeconds = Math.max(0.1, v)));
  wireNumInput(".event-duration", (e) => e.duration ?? 1, (e, v) => (e.duration = snapTime(Math.max(0.1, v))));
  wireNumInput(".event-rot-deg", (e) => e.rotateDegrees ?? 180, (e, v) => (e.rotateDegrees = Math.min(360, Math.max(0, v))));
  wireNumInput(".event-rot-sec", (e) => e.rotateSeconds ?? 2, (e, v) => (e.rotateSeconds = snapTime(Math.max(0.1, v))));
  wireNumInput(".event-shake-amp", (e) => e.shakeAmplitude ?? 0.15, (e, v) => (e.shakeAmplitude = Math.max(0.01, v)));
  wireNumInput(".event-flash-intensity", (e) => e.flashIntensity ?? 4, (e, v) => (e.flashIntensity = Math.max(0.1, v)));
  body.querySelectorAll<HTMLInputElement>(".event-flash-color").forEach((inp) => {
    inp.addEventListener("change", () => {
      const idx = parseInt(inp.dataset.idx!);
      const v = inp.value.trim();
      if (!/^#[0-9a-fA-F]{6}$/.test(v) || v === (group.events[idx].flashColor ?? "#DEE1FF")) return;
      pushHistory();
      group.events[idx].flashColor = v.toLowerCase();
      S.dirty = true;
      refresh();
    });
  });
  body.querySelectorAll<HTMLInputElement>(".event-sound-cue").forEach((inp) => {
    inp.addEventListener("change", () => {
      const idx = parseInt(inp.dataset.idx!);
      const v = inp.value.trim();
      if (v === (group.events[idx].soundCue ?? "")) return;
      pushHistory();
      if (v) group.events[idx].soundCue = v;
      else delete group.events[idx].soundCue;
      S.dirty = true;
      refresh();
    });
  });
  body.querySelectorAll<HTMLSelectElement>(".event-rot-dir").forEach((sel) => {
    sel.addEventListener("change", () => {
      const idx = parseInt(sel.dataset.idx!);
      pushHistory();
      group.events[idx].rotateDirection = sel.value === "ccw" ? "ccw" : "cw";
      S.dirty = true;
      refresh();
      draw();
    });
  });
  body.querySelectorAll<HTMLInputElement>(".event-start").forEach((inp) => {
    inp.addEventListener("change", () => {
      const idx = parseInt(inp.dataset.idx!);
      const v = snapTime(parseFloat(inp.value) || 0);
      if (v === eventStart(group.events[idx])) return;
      pushHistory();
      // lift/drop 之间互不重叠：与 lift/drop 重叠时吸附到最近可放置边界。
      group.events[idx].startTime = clampExclusiveStart(group, idx, v, eventDuration(group.events[idx], group));
      S.dirty = true;
      refresh();
      draw();
    });
  });
  body.querySelectorAll<HTMLInputElement>(".event-interval").forEach((inp) => {
    const commit = () => {
      const idx = parseInt(inp.dataset.idx!);
      const v = parseFloat(inp.value);
      if (!isFinite(v)) return;
      applyEventInterval(group, idx, v);
      refresh();
      draw();
    };
    inp.addEventListener("change", commit);
    inp.addEventListener("keydown", (e) => {
      if (e.key === "Enter") {
        e.preventDefault();
        commit();
      }
    });
  });
  body.querySelectorAll<HTMLInputElement>(".event-loop").forEach((cb) => {
    cb.addEventListener("change", () => {
      const idx = parseInt(cb.dataset.idx!);
      pushHistory();
      const evt = group.events[idx];
      evt.loop = cb.checked;
      if (cb.checked) {
        evt.pingpong = false;
        // 互斥：同步清除「往返循环」勾选
        body.querySelectorAll<HTMLInputElement>(".event-pingpong").forEach((p) => {
          if (parseInt(p.dataset.idx!) === idx) p.checked = false;
        });
      }
      S.dirty = true;
      refresh();
      draw();
    });
  });
  body.querySelectorAll<HTMLInputElement>(".event-pingpong").forEach((cb) => {
    cb.addEventListener("change", () => {
      const idx = parseInt(cb.dataset.idx!);
      pushHistory();
      const evt = group.events[idx];
      evt.pingpong = cb.checked;
      if (cb.checked) {
        evt.loop = false;
        // 互斥：同步清除「循环播放本段」勾选
        body.querySelectorAll<HTMLInputElement>(".event-loop").forEach((p) => {
          if (parseInt(p.dataset.idx!) === idx) p.checked = false;
        });
      }
      S.dirty = true;
      refresh();
      draw();
    });
  });
  body.querySelectorAll<HTMLElement>("[data-add-selected]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const idx = parseInt((btn as HTMLElement).dataset.addSelected!);
      const wpId = S.selectedWaypointId;
      if (!wpId) return;
      const evt = group.events[idx];
      if (!evt.waypointIds) evt.waypointIds = [];
      if (evt.waypointIds.includes(wpId)) return;
      pushHistory();
      evt.waypointIds.push(wpId);
      S.dirty = true;
      refresh();
    });
  });
  body.querySelectorAll<HTMLElement>("[data-route-del]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const [ei, ri] = (btn as HTMLElement).dataset.routeDel!.split(":").map(Number);
      pushHistory();
      group.events[ei].waypointIds?.splice(ri, 1);
      S.dirty = true;
      refresh();
    });
  });
  body.querySelectorAll<HTMLElement>("[data-del-event]").forEach((btn) => {
    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      const idx = parseInt((btn as HTMLElement).dataset.delEvent!);
      pushHistory();
      group.events.splice(idx, 1);
      if (S.activeAnimEventIdx !== null) {
        if (S.activeAnimEventIdx === idx) S.activeAnimEventIdx = null;
        else if (S.activeAnimEventIdx > idx) S.activeAnimEventIdx--;
      }
      S.dirty = true;
      refresh();
    });
  });

  body.querySelector("#btn-add-event")?.addEventListener("click", () => {
    pushHistory();
    // 特效组：沿用本组特效类型（全 wait 的空组默认 flash）；成员组默认 move。
    const fxT = groupFxType(group);
    if (isFxGroup(group)) {
      group.events.push({
        id: uuid(),
        type: fxT ?? "flash",
        delay: 0,
        startTime: snapTime(groupTimelineDuration(group)),
        ...(fxT === "shake"
          ? { shakeAmplitude: 0.15, duration: 2 }
          : { flashIntensity: 4, duration: 0.8, flashColor: "#DEE1FF", soundCue: "DynamicStage01Thunder" }),
      });
    } else {
      group.events.push({
        id: uuid(),
        type: "move",
        delay: 0,
        // 新事件追加到时间轴末尾（最晚结束时间之后）。
        startTime: snapTime(groupTimelineDuration(group)),
        intervalSeconds: 2,
        waypointIds: [],
      });
    }
    S.activeAnimEventIdx = group.events.length - 1;
    S.dirty = true;
    refresh();
  });
  body.querySelectorAll<HTMLCanvasElement>(".route-curve").forEach((canvas) => {
    const idx = parseInt(canvas.dataset.curveIdx!);
    drawRouteCurve(canvas, group, idx);
    wireRouteCurve(canvas, group, idx);
  });

  // ---- settings tab
  body.querySelector<HTMLInputElement>(".group-start-delay")?.addEventListener("change", (e) => {
    const v = parseFloat((e.target as HTMLInputElement).value) || 0;
    if (v === group.startDelay) return;
    pushHistory();
    group.startDelay = v;
    S.dirty = true;
  });
  body.querySelector<HTMLInputElement>(".group-loop")?.addEventListener("change", (e) => {
    const v = (e.target as HTMLInputElement).checked;
    if (v === group.loop) return;
    pushHistory();
    group.loop = v;
    S.dirty = true;
    refresh();
  });
  body.querySelector<HTMLInputElement>(".group-loop-delay")?.addEventListener("change", (e) => {
    const v = parseFloat((e.target as HTMLInputElement).value) || 0;
    if (v === group.loopDelay) return;
    pushHistory();
    group.loopDelay = v;
    S.dirty = true;
  });

  // ---- trigger / animator fields (TriggerQueue · TriggerTimer · Animator)
  body.querySelector<HTMLInputElement>(".group-wait-finished")?.addEventListener("change", (e) => {
    const v = (e.target as HTMLInputElement).checked;
    if (v === !!group.waitForFinished) return;
    pushHistory();
    group.waitForFinished = v;
    S.dirty = true;
  });
  const wireTriggerInput = (cls: string, key: "startTrigger" | "cancelTrigger" | "endTrigger" | "finishedTrigger") => {
    body.querySelector<HTMLInputElement>(cls)?.addEventListener("change", (e) => {
      const v = (e.target as HTMLInputElement).value.trim();
      if (v === (group[key] ?? "")) return;
      pushHistory();
      if (v) group[key] = v;
      else delete group[key];
      S.dirty = true;
    });
  };
  wireTriggerInput(".group-start-trigger", "startTrigger");
  wireTriggerInput(".group-cancel-trigger", "cancelTrigger");
  wireTriggerInput(".group-end-trigger", "endTrigger");
  wireTriggerInput(".group-finished-trigger", "finishedTrigger");
  body.querySelector<HTMLInputElement>(".group-root-motion")?.addEventListener("change", (e) => {
    const v = (e.target as HTMLInputElement).checked;
    if (v === !!group.applyRootMotion) return;
    pushHistory();
    group.applyRootMotion = v;
    S.dirty = true;
  });
  body.querySelector("#btn-del-move")?.addEventListener("click", () => {
    if (!confirm(`确定删除动画组「${group.displayName}」及其 ${group.events.length} 个事件？`)) return;
    pushHistory();
    S.animControls = S.animControls.filter((g) => g.id !== S.activeAnimGroupId);
    cleanOrphanedButtonLinks();
    S.activeAnimGroupId = null;
    S.activeAnimEventIdx = null;
    S.selectedWaypointId = null;
    S.animMode = "none";
    S.activeAnimTab = "members";
    S.animPickTargetGroupId = null;
    stopAnimPreview();
    clearAnimSelection();
    S.dirty = true;
    refresh();
  });
}
