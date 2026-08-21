import {
  S,
  CELL,
  type MoveMode,
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
import { isAirFloor } from "./floors";
import { openModal, closeModal } from "../modals";
import { setLayer } from "./init";
import type {
  MoveGroup,
  MoveGroupEvent,
  MoveGroupMemberGroup,
  MoveGroupWaypoint
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

/** Color per event type — event cards use these so 移动 / 抬起 / 落下 / 等待
 *  read at a glance in the events list. */
export const EVENT_TYPE_COLORS: Record<MoveGroupEvent["type"], string> = {
  move: "#3d6bf3",
  lift: "#8a5fd6",
  drop: "#c23a4a",
  wait: "#8b93a3",
};

export function eventTypeColor(type: MoveGroupEvent["type"]): string {
  return EVENT_TYPE_COLORS[type];
}

// ------------------------------------------------------------------ basics

export function activeGroup(): MoveGroup | null {
  if (!S.activeMoveGroupId) return null;
  return S.moveControls.find((g) => g.id === S.activeMoveGroupId) ?? null;
}

function clearMoveSelection(): void {
  clearSelection();
  clearFloorSelection();
  S.selectedKey = null;
}

export function groupVisibleInLayer(g: MoveGroup): boolean {
  if (S.currentLayer === "move" || S.currentLayer === "background") return true;
  if (S.currentLayer === "floor") return true;
  return g.itemInstanceIds.some(
    (id) =>
      S.items.some(
        (it) =>
          it.instanceId === id &&
          itemLayerOfIt(it) === S.currentLayer
      )
  ) || g.objectInstanceIds.length > 0;
}

export function findGroupByItemId(instanceId: string): MoveGroup | null {
  return S.moveControls.find((g) => g.itemInstanceIds.includes(instanceId)) ?? null;
}

export function findGroupById(id: string): MoveGroup | null {
  return S.moveControls.find((g) => g.id === id) ?? null;
}

/** Waypoint pick tolerance: points within 0.5 格 of each other count as
 *  overlapping (same spirit as the item footprint pick). */
export const WAYPOINT_HIT_RADIUS = CELL * 0.5;

/** All waypoints within the pick radius of a world position (0.5 格). Multiple
 *  hits = overlapping cluster, picked the same way items are (picker list). */
export function hitTestWaypoints(wx: number, wz: number): { id: string }[] {
  const hits: { id: string }[] = [];
  const r2 = WAYPOINT_HIT_RADIUS * WAYPOINT_HIT_RADIUS;
  for (const group of S.moveControls) {
    if (group.id !== S.activeMoveGroupId && S.activeMoveGroupId) continue;
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
export function waypointInfo(wpId: string): { group: MoveGroup; wp: MoveGroupWaypoint; num: number } | null {
  for (const g of S.moveControls) {
    const idx = g.waypoints.findIndex((w) => w.id === wpId);
    if (idx >= 0) return { group: g, wp: g.waypoints[idx], num: idx + 1 };
  }
  return null;
}

/** Delete a waypoint (from the pool and every event route). */
export function deleteWaypoint(group: MoveGroup, wpId: string): void {
  group.waypoints = group.waypoints.filter((w) => w.id !== wpId);
  for (const evt of group.events) {
    if (evt.waypointIds) evt.waypointIds = evt.waypointIds.filter((id) => id !== wpId);
  }
  if (S.selectedWaypointId === wpId) S.selectedWaypointId = null;
  S.dirty = true;
}

export function cleanOrphanedMoveControls(): void {
  const liveIds = new Set(S.items.map((it) => it.instanceId));
  const liveFloorIds = new Set(S.floors.map((f) => f.instanceId));
  // 空气地板（仅碰撞盒）随写回重建，无法参与移动组烘焙 → 从组中剥离。
  const airFloorIds = new Set(S.floors.filter((f) => isAirFloor(f)).map((f) => f.instanceId));
  S.moveControls = S.moveControls.filter((g) => {
    g.itemInstanceIds = g.itemInstanceIds.filter(
      (id) => liveIds.has(id) || id.startsWith("new:")
    );
    g.floorInstanceIds = g.floorInstanceIds.filter(
      (id) => (liveFloorIds.has(id) || id.startsWith("new:")) && !airFloorIds.has(id)
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
    return g.itemInstanceIds.length > 0 || g.floorInstanceIds.length > 0 || g.objectInstanceIds.length > 0;
  });
  if (S.activeMoveGroupId && !S.moveControls.some((g) => g.id === S.activeMoveGroupId)) {
    S.activeMoveGroupId = null;
    S.activeMoveEventIdx = null;
    S.selectedWaypointId = null;
  }
}

function removeMember(group: MoveGroup, instanceId: string): void {
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

export function exitMoveMode(): void {
  if (S.moveMode === "none") return;
  S.moveMode = "none";
  clearMoveSelection();
  updateMovePickBar();
  renderRightPanel();
  draw();
}

function setMoveMode(mode: MoveMode): void {
  if (S.moveMode === mode && S.currentLayer === "move") {
    renderRightPanel();
    draw();
    return;
  }
  S.moveMode = mode;
  if (mode !== "none" && S.currentLayer !== "move") {
    setLayer("move");
  }
  if (mode === "members") {
    clearMoveSelection();
  }
  updateMovePickBar();
  renderRightPanel();
  draw();
}

// ------------------------------------------------------------------ floating pick bar

export function updateMovePickBar(): void {
  const bar = dom.movePickBar;
  const group = activeGroup();
  const show = S.currentLayer === "move" && S.moveMode === "members" && !!group;
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
  bar.querySelector("#mpb-exit")?.addEventListener("click", () => exitMoveMode());
}

/** The member group currently targeted by pick additions (null = ungrouped). */
function pickTarget(group: MoveGroup): MoveGroupMemberGroup | null {
  return (group.memberGroups ?? []).find((mg) => mg.id === S.movePickTargetGroupId) ?? null;
}

function addSelectedToGroup(): void {
  const group = activeGroup();
  if (!group || S.selectedKeys.size + S.selectedFloorKeys.size === 0) return;
  pushHistory();
  let addedItems = 0;
  let addedFloors = 0;
  const target = pickTarget(group);
  const targetAdd = (id: string) => {
    if (target && !target.memberInstanceIds.includes(id)) target.memberInstanceIds.push(id);
  };
  for (const key of S.selectedKeys) {
    const it = S.items.find((i) => i._editorKey === key);
    if (it && !isBackgroundItem(it) && !group.itemInstanceIds.includes(it.instanceId)) {
      group.itemInstanceIds.push(it.instanceId);
      targetAdd(it.instanceId);
      addedItems++;
    }
  }
  for (const key of S.selectedFloorKeys) {
    const f = S.floors.find((fl) => fl._key === key);
    // 空气地板（仅碰撞盒）随写回重建，加入移动组会在写回时失效 → 排除。
    if (f && !isAirFloor(f) && f.surfaceKind !== "background" && !group.floorInstanceIds.includes(f.instanceId)) {
      group.floorInstanceIds.push(f.instanceId);
      targetAdd(f.instanceId);
      addedFloors++;
    }
  }
  S.dirty = true;
  clearMoveSelection();
  updateMovePickBar();
  renderRightPanel();
  draw();
  setStatus(
    `已加入 ${addedItems} 个物品 · ${addedFloors} 块地板到组「${group.displayName}」` +
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
  clearMoveSelection();
  updateMovePickBar();
  renderRightPanel();
  draw();
  if (n > 0) setStatus(`已从组「${group.displayName}」移出 ${n} 个成员`);
}

// ------------------------------------------------------------------ canvas overlay

export function drawMoveControlOverlay(): void {
  const active = activeGroup();
  const groups = active
    ? [active]
    : S.moveControls.filter((g) => groupVisibleInLayer(g));

  for (const group of groups) {
    const isActive = group.id === S.activeMoveGroupId;
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

  if (S.currentLayer === "move") drawMoveLegend();
  const act = activeGroup();
  if (act && S.movePreview && S.movePreview.groupId === act.id) {
    drawPreviewOverlay(act);
  }
}

function firstRouteFirstWp(group: MoveGroup): { x: number; z: number } | null {
  for (const evt of group.events) {
    if (evt.type !== "move" || !evt.waypointIds || !evt.waypointIds.length) continue;
    for (const wpId of evt.waypointIds) {
      const wp = group.waypoints.find((w) => w.id === wpId);
      if (wp) return { x: wp.x, z: wp.z };
    }
  }
  return null;
}

function routePosAt(evt: MoveGroupEvent, group: MoveGroup, tSec: number): { x: number; z: number } | null {
  if (!evt.waypointIds || !evt.waypointIds.length) return null;
  const wps = evt.waypointIds
    .map((id) => group.waypoints.find((w) => w.id === id))
    .filter(Boolean) as MoveGroupWaypoint[];
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
function memberRoleText(group: MoveGroup, id: string): string {
  if (group.memberStatic?.some((m) => m.instanceId === id)) return "静止";
  const off = group.memberOffsets?.find((o) => o.instanceId === id);
  if (off?.followWaypointId) return "跟随";
  if (off?.t != null) return "相位";
  return "移动";
}

function drawMemberMarkers(group: MoveGroup): void {
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
    const isPhase = !isStatic && off?.t != null;
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

/** Legend strip (bottom-left) explaining the move-layer symbols. */
function drawMoveLegend(): void {
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

/** Keyframe timeline of an event route (arrival keys + dwell keys from waits).
 *  Imported routes keep their original key times unless any timing is edited
 *  (wait / segmentSeconds), in which case the computed timeline takes over. */
function routeTimeline(evt: MoveGroupEvent, group: MoveGroup): { times: number[]; pts: { x: number; z: number }[] } {
  const wps = (evt.waypointIds ?? [])
    .map((id) => group.waypoints.find((w) => w.id === id))
    .filter(Boolean) as MoveGroupWaypoint[];
  if (!wps.length) return { times: [], pts: [] };
  if (wps.length === 1) return { times: [0], pts: [{ x: wps[0].x, z: wps[0].z }] };
  const anyEdit = wps.some((w) => (w.wait ?? 0) > 0 || (w.segmentSeconds ?? 0) > 0);
  if (!anyEdit && wps.some((w) => w.hasTime)) {
    return {
      times: wps.map((w) => w.t ?? 0),
      pts: wps.map((w) => ({ x: w.x, z: w.z })),
    };
  }
  const times: number[] = [];
  const pts: { x: number; z: number }[] = [];
  const interval = evt.intervalSeconds ?? 2;
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
  return { times, pts };
}

function clipDuration(evt: MoveGroupEvent, group: MoveGroup): number {
  const { times } = routeTimeline(evt, group);
  if (!times.length) return 0;
  return times[times.length - 1];
}

function clipPos(evt: MoveGroupEvent, group: MoveGroup, tc: number): { x: number; z: number } | null {
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
function clipY(evt: MoveGroupEvent, group: MoveGroup, tc: number): number {
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

/** Duration of one event's clip (wait clips are 1s constants so chaining timing
 *  matches the bake; lift/drop use their liftSeconds; move = route length). */
function eventDuration(evt: MoveGroupEvent, group: MoveGroup): number {
  if (evt.type === "move") return clipDuration(evt, group);
  if (evt.type === "lift" || evt.type === "drop") return Math.max(0.05, evt.liftSeconds ?? 1);
  return 1;
}

/** Start time of the next event: the bake chains clips (delay <= 0 → direct
 *  transition at the previous clip's end), and waitForFinished paces the trigger
 *  on the previous clip's finished event — so the gap is never shorter than the
 *  previous clip's duration. */
function nextEventStart(prevStart: number, prevDur: number, delay: number, waitForFinished: boolean): number {
  if (waitForFinished) return prevStart + prevDur + Math.max(0, delay);
  return prevStart + Math.max(prevDur, Math.max(0, delay));
}

/** Total simulated duration of one full sequence (for the preview progress bar). */
function previewDuration(group: MoveGroup): number {
  let acc = group.startDelay ?? 0;
  let prevDur = 0;
  let first = true;
  for (const evt of group.events) {
    if (first) {
      acc += Math.max(0, evt.delay);
      first = false;
    } else {
      acc = nextEventStart(acc, prevDur, evt.delay, !!group.waitForFinished);
    }
    prevDur = eventDuration(evt, group);
  }
  if (group.events.length > 0) acc += prevDur;
  if (group.loop) acc += group.loopDelay ?? 0;
  return acc;
}

/** Simulated positions (with optional height) of every member and the route head.
 *  Move events after a lift fly at the inherited height (same rule as the bake). */
function previewPositions(group: MoveGroup, t: number): Map<string, { x: number; z: number; y?: number }> {
  const out = new Map<string, { x: number; z: number; y?: number }>();
  const staticIds = new Set((group.memberStatic ?? []).map((m) => m.instanceId));
  const offById = new Map((group.memberOffsets ?? []).map((o) => [o.instanceId, o]));
  const memberIds = [...group.itemInstanceIds, ...group.floorInstanceIds, ...group.objectInstanceIds];
  const firstMove = group.events.find((e) => e.type === "move");
  const baseWp =
    firstMove?.waypointIds?.map((id) => group.waypoints.find((w) => w.id === id)).filter(Boolean)[0] ??
    group.waypoints[0];
  const basePos = baseWp ? { x: baseWp.x, z: baseWp.z } : { x: 0, z: 0 };

  const place = (id: string, p: { x: number; z: number } | null | undefined, y = 0) => {
    if (!p) return;
    const off = offById.get(id);
    out.set(id, {
      x: p.x + (off?.x ?? 0),
      z: p.z + (off?.z ?? 0),
      y: y + (off?.t != null ? 0 : 0),
    });
  };

  /** Hold every non-static member (+ route head) at the given position. */
  const holdAll = (p: { x: number; z: number }, y = 0) => {
    out.set("__head__", { ...p, y });
    for (const id of memberIds) {
      if (staticIds.has(id)) continue;
      const f = followPosOf(id);
      if (f) out.set(id, { x: f.x, z: f.z, y });
      else place(id, p, y);
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

  let acc = group.startDelay ?? 0;
  let seqLiftY = 0;
  /** Route end of the last move event — lift/drop/wait hold here (not basePos),
   *  so event hand-offs are seamless (no flash-back between events). */
  let lastPos: { x: number; z: number } | null = null;
  // Find the LAST event whose start time has passed — that one is playing.
  // Event starts follow the bake's chaining model (next start = previous start
  // + max(previous duration, delay); waitForFinished adds the delay on top).
  let cur: MoveGroupEvent | null = null;
  let tc = 0;
  let first = true;
  let prevDur = 0;
  for (const evt of group.events) {
    const start = first
      ? acc + Math.max(0, evt.delay)
      : nextEventStart(acc, prevDur, evt.delay, !!group.waitForFinished);
    if (t < start) break;
    acc = start;
    if (cur !== null) {
      // Fold the state of the event we just fully passed.
      if (cur.type === "lift") seqLiftY = cur.yTo !== 0 ? cur.yTo! : 1;
      else if (cur.type === "drop") seqLiftY = 0;
      else if (cur.type === "move") {
        const L = clipDuration(cur, group);
        if (L > 0) {
          const end = clipPos(cur, group, L);
          if (end) lastPos = end;
        }
      }
    }
    prevDur = eventDuration(evt, group);
    first = false;
    cur = evt;
    tc = t - start;
  }

  if (cur === null) {
    // Before the first event's start: members hold at the base position.
    holdAll(basePos);
  } else if (cur.type === "wait") {
    holdAll(lastPos ?? basePos);
  } else if (cur.type === "lift" || cur.type === "drop") {
    // Pure-Y event: members hold their current XZ and rise/fall to yTo.
    const secs = Math.max(0.05, cur.liftSeconds ?? 1);
    const k = Math.min(1, Math.max(0, tc / secs));
    const yFrom = cur.type === "drop" ? seqLiftY : 0;
    const yTo = cur.yTo ?? (cur.type === "lift" ? 1 : 0);
    const y = yFrom + (yTo - yFrom) * k;
    holdAll(lastPos ?? basePos, y);
    seqLiftY = yTo;
  } else {
    const L = clipDuration(cur, group);
    if (L <= 0) {
      holdAll(lastPos ?? basePos);
    } else {
      const p = clipPos(cur, group, tc);
      const flyY = (cur.liftHeight ?? 0) > 0 ? clipY(cur, group, tc) : seqLiftY;
      if (p) {
        out.set("__head__", { ...p, y: flyY });
        const end = clipPos(cur, group, L);
        if (end) lastPos = end;
      }
      for (const id of memberIds) {
        if (staticIds.has(id)) continue;
        const off = offById.get(id);
        const f = followPosOf(id);
        if (f) {
          out.set(id, { x: f.x, z: f.z, y: flyY });
          continue;
        }
        const tc2 = off?.t != null ? tc + off.t : tc;
        const mp = clipPos(cur, group, tc2);
        if (mp) {
          const offX = off?.x ?? 0;
          const offZ = off?.z ?? 0;
          out.set(id, { x: mp.x + offX, z: mp.z + offZ, y: flyY });
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
  return out;
}

/** Simulated positions of every member of the currently previewed group
 *  (null when no preview is playing). The renderer uses this to move the
 *  actual components along the route during playback. */
export function previewMemberPositions(): Map<string, { x: number; z: number; y?: number }> | null {
  const act = activeGroup();
  if (!act || !S.movePreview || S.movePreview.groupId !== act.id || !S.movePreview.playing) return null;
  return previewPositions(act, S.movePreview.t);
}

function previewLoop(now: number): void {
  previewRAF = 0;
  if (!S.movePreview || !S.movePreview.playing) return;
  if (!previewLastTick) previewLastTick = now;
  const dt = Math.min(0.05, (now - previewLastTick) / 1000);
  previewLastTick = now;
  S.movePreview.t += dt;
  draw();
  previewRAF = requestAnimationFrame(previewLoop);
}

export function stopMovePreview(): void {
  if (previewRAF) cancelAnimationFrame(previewRAF);
  previewRAF = 0;
  previewLastTick = 0;
  S.movePreview = null;
}

export function toggleMovePreview(): void {
  const group = activeGroup();
  if (!group) return;
  if (S.movePreview && S.movePreview.groupId === group.id) {
    if (S.movePreview.playing) {
      S.movePreview.playing = false;
      renderRightPanel();
      draw();
      return;
    }
    S.movePreview.t = 0;
    S.movePreview.playing = true;
  } else {
    S.movePreview = { playing: true, t: 0, groupId: group.id };
  }
  previewLastTick = 0;
  if (!previewRAF) previewRAF = requestAnimationFrame(previewLoop);
  renderRightPanel();
  draw();
}

function syncPreview(group: MoveGroup): void {
  if (S.movePreview && S.movePreview.groupId !== group.id) stopMovePreview();
}

function drawPreviewOverlay(group: MoveGroup): void {
  if (!S.movePreview || S.movePreview.groupId !== group.id) return;
  const col = groupColor(group.id);
  const pos = previewPositions(group, S.movePreview.t);
  const head = pos.get("__head__");
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
    const frac = Math.max(0, Math.min(1, S.movePreview.t / total));
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

function memberSummary(g: MoveGroup): string {
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

function groupCardHtml(g: MoveGroup): string {
  const col = groupColor(g.id);
  const itemN = g.itemInstanceIds.length;
  const floorN = g.floorInstanceIds.length;
  const objN = g.objectInstanceIds.length;
  const loopMark = g.loop || g.events.some((e) => e.loop) ? " ⟲" : "";
  const emptyRouteEvts = g.events.filter(
    (e) => e.type === "move" && (!e.waypointIds || e.waypointIds.length === 0)
  ).length;
  const warnings: string[] = [];
  if (itemN + floorN + objN === 0) warnings.push("⚠ 无成员");
  if (emptyRouteEvts > 0) warnings.push(`⚠ ${emptyRouteEvts} 个事件无路线`);
  return `<div class="move-group-card" data-group-id="${g.id}">
    <div class="mgc-color" style="background:${col}"></div>
    <div class="mgc-main">
      <div class="mgc-name">${escHtml(g.displayName)}${loopMark}</div>
      <div class="mgc-members">
        ${itemN ? `<span class="mgc-chip chip-item">物品 ${itemN}</span>` : ""}
        ${floorN ? `<span class="mgc-chip chip-floor">地板 ${floorN}</span>` : ""}
        ${objN ? `<span class="mgc-chip chip-obj">其他 ${objN}</span>` : ""}
      </div>
      <div class="mgc-meta">📍 路点 ${g.waypoints.length} · 🔁 事件 ${g.events.length}${warnings.length ? ` <span class="mgc-warn">${warnings.join(" · ")}</span>` : ""}</div>
      ${memberSummary(g) ? `<div class="mgc-sub">${escHtml(memberSummary(g))}</div>` : ""}
    </div>
  </div>`;
}

function renderGroupList(body: HTMLElement): void {
  if (S.movePreview) stopMovePreview();
  const groups = S.moveControls.filter((g) => groupVisibleInLayer(g));
  const head = `<div class="move-list-head">
    <span class="move-list-title">🎬 移动组${groups.length ? ` (${groups.length})` : ""}</span>
    <button type="button" class="btn-small primary" id="btn-new-group">＋ 新增分组</button>
  </div>`;

  if (groups.length === 0) {
    body.innerHTML = head + `<div class="move-empty">
      <div class="move-empty-icon">🎬</div>
      <div class="move-empty-text">还没有移动组</div>
      <div class="move-empty-sub">创建移动组后，物品 / 地板 / 装饰可沿路线自动移动（如传送带、巡逻的 NPC、漂移的木筏）。</div>
      <button type="button" class="btn-small primary move-empty-btn" id="btn-new-group">＋ 新增分组</button>
      <div class="move-empty-sub">快捷方式：在地图上右键一个物品 →「创建移动组」</div>
    </div>`;
  } else {
    body.innerHTML = head + groups.map(groupCardHtml).join("");
  }

  body.querySelectorAll<HTMLElement>("#btn-new-group").forEach((btn) => {
    btn.addEventListener("click", openNewGroupModal);
  });

  body.querySelectorAll<HTMLElement>(".move-group-card").forEach((row) => {
    row.addEventListener("click", () => {
      S.activeMoveGroupId = row.dataset.groupId!;
      S.activeMoveEventIdx = null;
      S.selectedWaypointId = null;
      S.moveMode = "none";
      S.activeMoveTab = "members";
      S.movePickTargetGroupId = null;
      clearMoveSelection();
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
  return `移动组 ${S.moveControls.length + 1}`;
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
  const wp: MoveGroupWaypoint = { id: uuid(), x: pos?.x ?? 0, z: pos?.z ?? 0 };
  const group: MoveGroup = {
    id: uuid(),
    displayName: name,
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
      intervalSeconds: 2,
      waypointIds: [wp.id],
    }],
  };
  S.moveControls.push(group);
  S.activeMoveGroupId = group.id;
  S.activeMoveEventIdx = 0;
  S.activeMoveTab = "members";
  S.movePickTargetGroupId = null;
  S.activeRightTab = "move";
  S.moveMode = "none";
  clearMoveSelection();
  updatePanelTabButtons();
  updateMovePickBar();
  renderRightPanel();
  draw();
  setStatus(
    `已创建移动组「${name}」。可在「🧩 成员」添加成员（📐 地图选点 / 下拉），在「📍 路线」添加路点。`
  );
}

export function openNewGroupModal(): void {
  if (S.moveMode !== "none") {
    S.moveMode = "none";
    clearMoveSelection();
  }
  const modalName = defaultGroupName();
  openModal(
    "＋ 新增移动分组",
    `<p class="modal-hint">直接创建移动组，创建后可在编辑器中添加成员（物品 / 装饰 / 地板）与路点。组名建议用英文（如 Island1 / MovingBridge）。</p>
     <label class="modal-field">组名（英文）<input type="text" id="wizard-name" value="${escHtml(modalName)}" placeholder="如 Island1" /></label>`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn primary" id="wizard-create">创建移动组</button>`
  );

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.getElementById("wizard-create")?.addEventListener("click", () => {
    const name =
      ((document.getElementById("wizard-name") as HTMLInputElement | null)?.value ?? "").trim() ||
      modalName;
    closeModal();
    createGroup(name, [], []);
  });
  (document.getElementById("wizard-name") as HTMLInputElement | null)?.focus();
}

// ------------------------------------------------------------------ route time curve (F3)

const CURVE_W = 560;
const CURVE_H = 170;

/** Per-waypoint arrival times (without dwell keys). */
function routeArrivalTimes(evt: MoveGroupEvent, group: MoveGroup): number[] {
  const wps = (evt.waypointIds ?? [])
    .map((id) => group.waypoints.find((w) => w.id === id))
    .filter(Boolean) as MoveGroupWaypoint[];
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

function routeCurveInfoText(group: MoveGroup, evtIdx: number): string {
  const evt = group.events[evtIdx];
  const wps = (evt.waypointIds ?? [])
    .map((id) => group.waypoints.find((w) => w.id === id))
    .filter(Boolean) as MoveGroupWaypoint[];
  const arrival = routeArrivalTimes(evt, group);
  const parts: string[] = [`启动 ${evt.delay}s`];
  for (let i = 0; i < wps.length; i++) {
    if (i < wps.length - 1) {
      parts.push(`段${i + 1} ${(wps[i].segmentSeconds ?? evt.intervalSeconds ?? 2).toFixed(1)}s`);
    }
    if ((wps[i].wait ?? 0) > 0) parts.push(`停${i + 1} ${wps[i].wait}s`);
  }
  const clipLen = arrival.length > 0 ? arrival[arrival.length - 1] + (wps[wps.length - 1]?.wait ?? 0) : 0;
  parts.push(`总 ${(evt.delay + clipLen).toFixed(1)}s`);
  return parts.join(" · ");
}

function drawRouteCurve(canvas: HTMLCanvasElement, group: MoveGroup, evtIdx: number): void {
  const ctx = canvas.getContext("2d");
  if (!ctx) return;
  const W = CURVE_W;
  const H = CURVE_H;
  canvas.width = W;
  canvas.height = H;
  const evt = group.events[evtIdx];
  const wps = (evt.waypointIds ?? [])
    .map((id) => group.waypoints.find((w) => w.id === id))
    .filter(Boolean) as MoveGroupWaypoint[];
  const arrival = routeArrivalTimes(evt, group);
  const n = arrival.length;
  const clipLen = n > 0 ? arrival[n - 1] + (wps[n - 1]?.wait ?? 0) : 0;
  const total = Math.max(0.5, evt.delay + clipLen);
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
  // Delay zone
  if (evt.delay > 0) {
    ctx.fillStyle = "rgba(61,107,243,0.08)";
    ctx.fillRect(padL, padT, xOf(evt.delay) - padL, H - padT - padB);
  }
  // Progress polyline (arrival keys + dwell plateaus)
  ctx.strokeStyle = "#3d6bf3";
  ctx.lineWidth = 2;
  ctx.beginPath();
  let prevX = xOf(evt.delay);
  let prevY = yOf(0);
  ctx.moveTo(prevX, prevY);
  for (let i = 0; i < n; i++) {
    const x = xOf(evt.delay + arrival[i]);
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
      const x0 = xOf(evt.delay + arrival[i]);
      const y = yOf(n > 1 ? i / (n - 1) : 0);
      ctx.beginPath();
      ctx.moveTo(x0, y);
      ctx.lineTo(x0 + (w / total) * (W - padL - padR), y);
      ctx.stroke();
    }
  }
  ctx.setLineDash([]);
  ctx.stroke();
  // Handles: delay (triangle), waypoints (circles), wait ends (squares)
  ctx.fillStyle = "#ffcc00";
  ctx.beginPath();
  const dhx = xOf(evt.delay);
  const dhy = yOf(0);
  ctx.moveTo(dhx, dhy - 6);
  ctx.lineTo(dhx - 4, dhy + 3);
  ctx.lineTo(dhx + 4, dhy + 3);
  ctx.closePath();
  ctx.fill();
  for (let i = 0; i < n; i++) {
    const x = xOf(evt.delay + arrival[i]);
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
  kind: "delay" | "point" | "wait";
  idx: number;
  orig: number;
  changed: boolean;
}

/** Singleton drag context — window handlers are registered exactly once. */
interface CurveDragCtx {
  canvas: HTMLCanvasElement;
  group: MoveGroup;
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
    .filter(Boolean) as MoveGroupWaypoint[];
  const arrival = routeArrivalTimes(evt, group);
  const total = curveTotal(group, evtIdx);
  const t = Math.max(0, ((mx - 34) / (CURVE_W - 34 - 10)) * total);
  if (drag.kind === "delay") {
    const v = Math.max(0, t);
    if (Math.abs(v - evt.delay) > 0.01) {
      evt.delay = v;
      drag.changed = true;
    }
  } else if (drag.kind === "point") {
    const i = drag.idx;
    const base = evt.delay + arrival[i - 1] + (wps[i - 1]?.wait ?? 0);
    const v = Math.max(0.1, t - base);
    if (Math.abs(v - (wps[i - 1].segmentSeconds ?? evt.intervalSeconds ?? 2)) > 0.01) {
      wps[i - 1].segmentSeconds = v;
      drag.changed = true;
    }
  } else {
    const i = drag.idx;
    const base = evt.delay + arrival[i];
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

function wireRouteCurve(canvas: HTMLCanvasElement, group: MoveGroup, evtIdx: number): void {
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
      .filter(Boolean) as MoveGroupWaypoint[];
    const arrival = routeArrivalTimes(evt, group);
    const n = arrival.length;
    const total = curveTotal(group, evtIdx);
    const xOf = (t: number) => padL + (t / total) * (W - padL - padR);
    const yOf = (p: number) => H - padB - p * (H - padT - padB);
    const near = (x: number, y: number) => Math.hypot(x - mx, y - my) <= 12;
    for (let i = 0; i < n; i++) {
      const w = wps[i]?.wait ?? 0;
      if (w > 0) {
        const x = xOf(evt.delay + arrival[i] + w);
        const y = yOf(n > 1 ? i / (n - 1) : 0);
        if (near(x, y)) return { kind: "wait", idx: i, orig: w, changed: false };
      }
    }
    const dhx = xOf(evt.delay);
    const dhy = yOf(0);
    if (near(dhx, dhy)) return { kind: "delay", idx: 0, orig: evt.delay, changed: false };
    for (let i = 1; i < n; i++) {
      const x = xOf(evt.delay + arrival[i]);
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

function curveTotal(group: MoveGroup, evtIdx: number): number {
  const evt = group.events[evtIdx];
  const wps = (evt.waypointIds ?? [])
    .map((id) => group.waypoints.find((w) => w.id === id))
    .filter(Boolean) as MoveGroupWaypoint[];
  const arrival = routeArrivalTimes(evt, group);
  const clipLen = arrival.length > 0 ? arrival[arrival.length - 1] + (wps[wps.length - 1]?.wait ?? 0) : 0;
  return Math.max(0.5, evt.delay + clipLen);
}

// ------------------------------------------------------------------ panel: entry point

export function renderMoveControlPanel(body: HTMLElement): void {
  updateMovePickBar();
  const group = activeGroup();
  if (group) {
    syncPreview(group);
    renderGroupEditor(body, group);
    return;
  }
  if (S.movePreview) stopMovePreview();
  renderGroupList(body);
}

// ------------------------------------------------------------------ panel: group editor

function sectionTitle(icon: string, text: string, count: string, color: string): string {
  return `<div class="move-section-title" style="--sec:${color}">
    <span class="sec-icon">${icon}</span><span class="sec-text">${text}</span>
    ${count ? `<span class="sec-count">${count}</span>` : ""}
  </div>`;
}

function memberRowHtml(group: MoveGroup, name: string, role: string, id: string, kind: "item" | "floor" | "obj"): string {
  const off = group.memberOffsets?.find((o) => o.instanceId === id);
  const followOpts = group.waypoints
    .map((wp, i) => `<option value="${wp.id}"${off?.followWaypointId === wp.id ? " selected" : ""}>#${i + 1}</option>`)
    .join("");
  return `<div class="move-member-row">
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

function flatMemberIds(group: MoveGroup): string[] {
  return [...group.itemInstanceIds, ...group.floorInstanceIds, ...group.objectInstanceIds];
}

function memberPathOf(group: MoveGroup, id: string): string | null {
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

function memberNameOf(group: MoveGroup, id: string): string {
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

function memberKindOf(group: MoveGroup, id: string): "item" | "floor" | "obj" {
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
function buildMemberGroupViews(group: MoveGroup): { views: MemberGroupView[]; standalone: string[] } {
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

function groupEventsMeta(group: MoveGroup): string {
  return group.events.length > 0
    ? `驱动事件 ${group.events.map((_, i) => i + 1).join("、")}`
    : "未配置事件";
}

/** Nested tree for an event card: 成员组 → 成员 (indented, expandable). */
function eventMemberTreeHtml(group: MoveGroup): string {
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
        if (!kids) kids = '<div class="muted move-member-empty">（空组）</div>';
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

function createMemberGroup(group: MoveGroup, name: string): void {
  pushHistory();
  if (!group.memberGroups) group.memberGroups = [];
  const mg: MoveGroupMemberGroup = { id: uuid(), name: name.trim(), memberInstanceIds: [] };
  group.memberGroups.push(mg);
  S.movePickTargetGroupId = mg.id;
  S.dirty = true;
}

function deleteMemberGroup(group: MoveGroup, id: string): void {
  pushHistory();
  group.memberGroups = (group.memberGroups ?? []).filter((g) => g.id !== id);
  if (S.movePickTargetGroupId === id) S.movePickTargetGroupId = null;
  S.dirty = true;
}

function renderMembersTab(group: MoveGroup): string {
  const { views, standalone } = buildMemberGroupViews(group);
  let html = "";

  // Pick target selector
  const userGroups = group.memberGroups ?? [];
  html += `<div class="mv-pick-target">
    <span>框选加入目标</span>
    <select id="pick-target" title="框选/点选的成员将加入选中的成员组（未分组 = 直接加入移动组）">
      <option value="">未分组</option>
      ${userGroups
        .map((g) => `<option value="${g.id}"${g.id === S.movePickTargetGroupId ? " selected" : ""}>${escHtml(g.name)}</option>`)
        .join("")}
    </select>
    ${S.movePickTargetGroupId ? `<span class="mv-target-note">新成员将加入该成员组</span>` : ""}
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
    html += '<div class="muted move-member-empty">（暂无成员组 — 点击「＋ 新增成员组」把一组物品归为同一组）</div>';
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
          : '<div class="muted move-member-empty">（该物品组下无独立物品）</div>';
      } else if (v.memberIds.length) {
        html += v.memberIds
          .map((id) => memberRowHtml(group, memberNameOf(group, id), memberRoleText(group, id), id, memberKindOf(group, id)))
          .join("");
      } else {
        html += '<div class="muted move-member-empty">（空组 — 地图选点框选物品后加入）</div>';
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
    html += '<div class="muted move-member-empty">（所有成员都已分组）</div>';
  }
  const standList = (ids: string[], label: string) => {
    if (ids.length === 0) return "";
    return `<div class="move-member-group">
      <div class="move-member-group-head">${label} (${ids.length})</div>
      ${ids.map((id) => memberRowHtml(group, memberNameOf(group, id), memberRoleText(group, id), id, memberKindOf(group, id))).join("")}
    </div>`;
  };
  html += standList(standItem, "物品") + standList(standFloor, "地板") + standList(standObj, "其他物体");
  html += `</div>`;

  // Add controls: 地图选点 / 添加物品 / 添加装饰 / 添加地板
  const wantLayer = S.currentLayer === "move" ? "items" : S.currentLayer;
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
  // 空气地板（仅碰撞盒）随写回重建，无法参与移动组烘焙 → 不出现在添加候选里。
  const floorCandidates = S.floors.filter(
    (f) => !isAirFloor(f) && f.surfaceKind !== "background" && !group.floorInstanceIds.includes(f.instanceId)
  );
  html += `<div class="move-add-row">
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
function memberCenterPos(group: MoveGroup): { x: number; z: number } | null {
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
  group: MoveGroup,
  evt: MoveGroupEvent,
  evtIdx: number,
  selWp: { x: number; z: number } | undefined,
  selInRoute: boolean
): string {
  const wpModeOn = S.moveMode === "waypoints";
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
    poolHtml = `<div class="move-wp-pool">
      <div class="move-wp-pool-head" data-pool-toggle="${evtIdx}">
        <span class="subgroup-arrow">${poolOpen ? "▾" : "▸"}</span>
        🗃 路点池 (${pooled.length})${poolOpen ? "" : " · 点击展开"}
      </div>
      ${poolOpen ? `<div class="move-wp-pool-body">
        ${pooled
          .map((wp) => {
            const sel = wp.id === S.selectedWaypointId;
            return `<div class="move-wp-row${sel ? " active" : ""}" data-wp-id="${wp.id}">
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

  return `<div class="move-routes-toolbar">
      <button type="button" class="mode-btn${wpModeOn ? " on" : ""}" id="btn-mode-waypoints" title="点击空白放置路点；点击 / 拖拽路点选中移动">${wpModeOn ? "✓ 放置中（点击空白放点）" : "📍 放置路点"}</button>
      ${wpModeOn ? `<button type="button" class="btn-small" id="btn-exit-mode">退出</button>` : ""}
      <button type="button" class="btn-small pick-add btn-add-start-wp" data-evt="${evtIdx}" title="在当前成员的中心位置添加一个起点路点，并自动编入本事件路线开头">＋ 起点（成员中心）</button>
      <button type="button" class="btn-small" data-add-selected="${evtIdx}"${selWp && !selInRoute ? "" : " disabled"} title="把画布上选中的路点加入本事件路线">＋ 编入选中</button>
      <label class="check route-auto-wrap" title="放置的路点自动按顺序编入本事件路线（取消勾选则放入路点池）">
        <input type="checkbox" class="route-auto-add" data-idx="${evtIdx}"${S.moveRouteAutoAdd ? " checked" : ""} /> 放置自动编入
      </label>
    </div>
    <div class="event-route-list">${routeRows}</div>
    ${poolHtml}
    <div class="move-wp-hint">放置路点自动按顺序编入路线；点击 / 拖拽路点选中后可用行内方向按钮微调；Del 删除选中路点。</div>`;
}

function renderEventsTab(
  group: MoveGroup,
  selEvtIdx: number | null,
  selWp: { x: number; z: number } | undefined,
  selInRoute: boolean
): string {
  let html = `<div class="move-section">
    ${sectionTitle("🔁", "事件（移动 / 等待序列）", `${group.events.length} 个`, "#d8703c")}`;
  for (let i = 0; i < group.events.length; i++) {
    const evt = group.events[i];
    const isSel = selEvtIdx === i;
    const tcol = EVENT_TYPE_COLORS[evt.type];
    const typeLabel =
      evt.type === "move" ? "移动" : evt.type === "lift" ? "抬起" : evt.type === "drop" ? "落下" : "等待";
    const typeChip = `<span class="evt-chip" style="background:${colorA(tcol, 0.16)};color:${tcol};border-color:${tcol}">${typeLabel}</span>`;
    const routeN = evt.type === "move" ? (evt.waypointIds?.length ?? 0) : 0;
    const routeEmpty = evt.type === "move" && routeN === 0;
    const liftOn = (evt.liftHeight ?? 0) > 0;

    let routeHtml = "";
    let settingsHtml = "";
    if (evt.type === "move") {
      settingsHtml =
        `<label>每步间隔 (秒)<input type="number" class="event-interval" data-idx="${i}" value="${evt.intervalSeconds ?? 2}" step="0.1" min="0.1" /></label>
         <label class="check" title="与「往返循环」互斥"><input type="checkbox" class="event-loop" data-idx="${i}"${evt.loop && !evt.pingpong ? " checked" : ""} /> 循环播放本段（到终点跳回起点，仅最后事件生效）</label>
         <label class="check" title="与「循环播放本段」互斥"><input type="checkbox" class="event-pingpong" data-idx="${i}"${evt.pingpong ? " checked" : ""} /> 往返循环（到终点后原路返回，仅最后事件生效）</label>
         <div class="sub move-wp-hint">位于「抬起」事件之后时，本事件自动按抬起高度半空移动，无需配置高度。</div>`;
      routeHtml = `<div class="subgroup-section">
        <div class="subgroup-section-title">📍 路线（按顺序移动）</div>
        ${routeEditorHtml(group, evt, i, selWp, selInRoute)}
      </div>`;
      const curveOpen = S.openCurves.has(i);
      routeHtml += `<div class="subgroup-section">
        <button type="button" class="route-curve-toggle" data-curve-toggle="${i}">
          <span class="subgroup-arrow">${curveOpen ? "▾" : "▸"}</span>
          ⏱ 移动时间曲线（拖动：起点=启动延迟 · 圆点=段时长 · 绿块=停留）
        </button>
        ${curveOpen ? `<canvas class="route-curve" data-curve-idx="${i}" width="${CURVE_W}" height="${CURVE_H}"></canvas>
        <div class="route-curve-info" id="route-curve-info-${i}">${routeCurveInfoText(group, i)}</div>` : ""}
      </div>`;
    } else if (evt.type === "lift" || evt.type === "drop") {
      settingsHtml =
        `<label>目标高度 Y<input type="number" class="event-yto" data-idx="${i}" value="${evt.yTo ?? (evt.type === "lift" ? 1 : 0)}" step="0.1" /></label>
         <label>升降用时 (秒)<input type="number" class="event-ysec" data-idx="${i}" value="${evt.liftSeconds ?? 1}" step="0.1" min="0.05" /></label>
         <div class="muted move-member-empty">${evt.type === "lift" ? "纯抬起事件：成员原地升起至目标高度，不移动。" : "纯落下事件：成员原地落回至目标高度。"}</div>`;
    } else {
      settingsHtml = '<div class="muted move-member-empty">等待事件 = 纯间隔延迟（不产生移动，下一事件在其后触发）</div>';
    }

    const yMeta =
      evt.type === "move"
        ? (liftOn ? `↑${evt.liftHeight} · ` : "") + `路线 ${routeN} 点`
        : evt.type === "lift"
          ? `↑ ${evt.yTo ?? 0}`
          : evt.type === "drop"
            ? `↓ ${evt.yTo ?? 0}`
            : "延迟段";
    const loopMark = evt.loop ? " ⟲" : evt.pingpong ? " ⇄" : group.loop ? " ⟲" : "";
    html += `<div class="subgroup-card${isSel ? " open" : ""}${routeEmpty ? " route-empty" : ""}" data-event-idx="${i}" data-evtype="${evt.type}">
      <div class="subgroup-head" data-subgroup-head="${i}">
        <span class="event-drag-handle" draggable="true" data-drag-event="${i}" title="拖动调整事件顺序">⠿</span>
        <span class="subgroup-arrow">${isSel ? "▾" : "▸"}</span>
        <span class="subgroup-title">🔁 事件 ${i + 1} · ${typeChip}${evt.triggerName ? ` (${escHtml(evt.triggerName)})` : ""}</span>
        <span class="subgroup-meta" style="color:${tcol}">${yMeta}${loopMark}</span>
        <button type="button" class="btn-del" data-del-event="${i}" title="删除事件">×</button>
      </div>
      ${isSel
        ? `<div class="subgroup-body">
            <div class="subgroup-section">
              <div class="subgroup-section-title">⚙ 设置</div>
              <div class="event-type-row">
                <label>类型
                  <select class="event-type" data-idx="${i}">
                    <option value="move"${evt.type === "move" ? " selected" : ""}>移动</option>
                    <option value="lift"${evt.type === "lift" ? " selected" : ""}>抬起</option>
                    <option value="drop"${evt.type === "drop" ? " selected" : ""}>落下</option>
                    <option value="wait"${evt.type === "wait" ? " selected" : ""}>等待</option>
                  </select>
                </label>
                <label>触发间隔 (秒)<input type="number" class="event-delay" data-idx="${i}" value="${evt.delay}" step="0.1" min="0" /></label>
              </div>
              ${settingsHtml}
            </div>
            ${routeHtml}
            <div class="subgroup-section">
              <div class="subgroup-section-title">🧩 成员组（随事件运动 · 点击展开成员）</div>
              ${eventMemberTreeHtml(group)}
            </div>
          </div>`
        : ""}
    </div>`;
  }
  html += `<div class="move-event-actions">
    <button type="button" class="btn-small primary" id="btn-add-event">＋ 添加事件</button>
  </div></div>`;
  return html;
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
function renderWaypointsTab(group: MoveGroup): string {
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

  const wpRow = (wp: MoveGroupWaypoint, usedFlag: boolean): string => {
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
      !usedFlag && S.activeMoveEventIdx !== null && group.events[S.activeMoveEventIdx]?.type === "move"
        ? `<button type="button" class="btn-small" data-pool-add="${S.activeMoveEventIdx}:${wp.id}" title="把该路点加入当前事件路线">＋ 编入当前事件</button>`
        : "";
    return `<div class="move-wp-row${sel ? " active" : ""}" data-wp-id="${wp.id}">
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
      ${rows || `<div class="muted move-member-empty">${empty}</div>`}
    </div>`;

  return `<div class="move-section">
    ${sectionTitle("📍", "路点管理", `${group.waypoints.length} 个`, "#3d6bf3")}
    <div class="wp-manage-summary">
      <span class="wp-usage-sum used">已使用 ${used.length}</span>
      <span class="wp-usage-sum unused">未使用 ${unused.length}</span>
    </div>
    ${section("已使用（被事件路线 / 成员跟随引用）", used.map((w) => wpRow(w, true)).join(""), "（无）")}
    ${section("未使用（不会参与移动）", unused.map((w) => wpRow(w, false)).join(""), "（无 — 全部路点都已编入）")}
    <div class="move-wp-hint">已使用 = 在某个事件的路线里，或被成员「跟随」；未使用的路点不参与移动，可「＋ 编入当前事件」或删除。点击行可选中画布上的路点。</div>
  </div>`;
}

function renderSettingsTab(group: MoveGroup): string {
  const boundLink = linkBindingGroup(group.displayName);
  const boundHint = boundLink
    ? `<div class="sub move-wp-hint" style="color:#e8b35a">⚠ 该组已被按钮联动绑定：不再自动执行，启动/结束触发器由联动自动管理（在此处手动修改无效，写回时会被联动覆盖）。</div>`
    : "";
  return `<div class="move-section">
    ${sectionTitle("⚙", "组设置", "", "#8b93a3")}
    <div class="move-settings-grid">
      <label>启动延迟 (秒)<input type="number" class="group-start-delay" value="${group.startDelay}" step="0.1" min="0" /></label>
      <label class="check"><input type="checkbox" class="group-loop"${group.loop ? " checked" : ""} /> 循环整个序列</label>
      <label class="group-loop-delay-wrap"${group.loop ? "" : ' style="display:none"'}>循环间隔 (秒)<input type="number" class="group-loop-delay" value="${group.loopDelay}" step="0.1" min="0" /></label>
    </div>
  </div>
  <div class="move-section">
    ${sectionTitle("🔀", "触发器 / 动画（TriggerQueue · Animator）", "", "#4f8fd6")}
    ${boundHint}
    <div class="move-settings-grid">
      <label class="check"><input type="checkbox" class="group-wait-finished"${group.waitForFinished ? " checked" : ""} /> 等待动画播完再触发下一事件</label>
      <div class="sub move-wp-hint">勾选后：每个移动事件播完（到达终点）才触发下一事件；循环片段不会发送完成信号。</div>
      <label>启动触发器（空 = 开局自动 / 延迟启动）${triggerSelectHtml("group-start-trigger", group.startTrigger, [["Start", "启动"], ["RoundStart", "回合开始"], ["GameStart", "游戏开始"]], "（空 · 开局自动 / 延迟启动）")}</label>
      <label>取消触发器（收到后中止队列）${triggerSelectHtml("group-cancel-trigger", group.cancelTrigger, [["StopMoving", "停止移动"], ["Stop", "停止"], ["Cancel", "取消"]], "（无）")}</label>
      <label>队列结束触发器（全部事件完成后广播）${triggerSelectHtml("group-end-trigger", group.endTrigger, [["OpenDoor", "开门"], ["DoorOpen", "门打开"], ["RoundStart", "回合开始"]], "（无）")}</label>
      <label>完成回调触发器名（默认 AnimationFinished）${triggerSelectHtml("group-finished-trigger", group.finishedTrigger, [["AnimationDone", "动画完成"]], "AnimationFinished（默认）")}</label>
      <label class="check"><input type="checkbox" class="group-root-motion"${group.applyRootMotion ? " checked" : ""} /> 应用根运动 (Animator.applyRootMotion)</label>
    </div>
  </div>
  <div class="move-danger">
    <button type="button" class="btn-small btn-danger" id="btn-del-move">🗑 删除移动组</button>
    <span class="move-wp-hint">删除后该组的所有路线与事件一并移除，物品保留在场景中。</span>
  </div>`;
}

function renderGroupEditor(body: HTMLElement, group: MoveGroup): void {
  syncPreview(group);
  newMgFormOpen = false;
  const col = groupColor(group.id);
  const selEvtIdx = S.activeMoveEventIdx;
  const activeEvt = selEvtIdx !== null ? group.events[selEvtIdx] : undefined;
  const selWp = S.selectedWaypointId
    ? group.waypoints.find((w) => w.id === S.selectedWaypointId)
    : undefined;
  const selInRoute = !!(
    selWp &&
    activeEvt &&
    activeEvt.waypointIds?.includes(selWp.id)
  );
  const previewOn = !!S.movePreview && S.movePreview.groupId === group.id && S.movePreview.playing;
  const memberCount = flatMemberIds(group).length;

  let html = `<div class="move-editor-head">
    <button type="button" class="btn-small" id="btn-move-back">◀ 返回</button>
    <span class="move-editor-color" style="background:${col}"></span>
    <input type="text" id="group-name" value="${escHtml(group.displayName)}" title="组名（回车生效）" />
  </div>`;

  html += `<div class="move-editor-summary">
    <span>🧩 ${memberCount} 成员 · 📍 ${group.waypoints.length} 路点 · 🔁 ${group.events.length} 事件</span>
    <button type="button" class="btn-small${previewOn ? " preview-on" : ""}" id="btn-preview" title="在画布上模拟成员沿路线运动（纯前端预览，写回后以游戏内为准）">${previewOn ? "⏸ 暂停预览" : "▶ 预览路线"}</button>
  </div>`;

  html += `<div class="move-tabs">
    <button type="button" class="move-tab${S.activeMoveTab === "members" ? " active" : ""}" data-mvtab="members">🧩 成员 (${memberCount})</button>
    <button type="button" class="move-tab${S.activeMoveTab === "events" ? " active" : ""}" data-mvtab="events">🔁 事件 (${group.events.length})</button>
    <button type="button" class="move-tab${S.activeMoveTab === "waypoints" ? " active" : ""}" data-mvtab="waypoints">📍 路点 (${group.waypoints.length})</button>
    <button type="button" class="move-tab${S.activeMoveTab === "settings" ? " active" : ""}" data-mvtab="settings">⚙ 设置</button>
  </div>`;

  html += `<div class="move-tab-body">`;
  switch (S.activeMoveTab) {
    case "members":
      html += renderMembersTab(group);
      break;
    case "events":
      html += renderEventsTab(group, selEvtIdx, selWp, selInRoute);
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

function wireGroupEditor(body: HTMLElement, group: MoveGroup): void {
  const refresh = () => {
    renderRightPanel();
    draw();
  };

  body.querySelector("#btn-move-back")?.addEventListener("click", () => {
    S.activeMoveGroupId = null;
    S.activeMoveEventIdx = null;
    S.selectedWaypointId = null;
    S.moveMode = "none";
    S.activeMoveTab = "members";
    S.movePickTargetGroupId = null;
    stopMovePreview();
    clearMoveSelection();
    updateMovePickBar();
    refresh();
  });

  // ---- tabs
  body.querySelectorAll<HTMLElement>("[data-mvtab]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const tab = (btn as HTMLElement).dataset.mvtab as "members" | "events" | "waypoints" | "settings";
      S.activeMoveTab = tab;
      refresh();
      draw();
    });
  });

  body.querySelector("#btn-preview")?.addEventListener("click", toggleMovePreview);

  // ---- members tab
  body.querySelector<HTMLSelectElement>("#pick-target")?.addEventListener("change", (e) => {
    S.movePickTargetGroupId = (e.target as HTMLSelectElement).value || null;
    refresh();
  });
  body.querySelector("#btn-add-members")?.addEventListener("click", () => setMoveMode("members"));
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
      S.movePickTargetGroupId = (btn as HTMLElement).dataset.targetGroup!;
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
  body.querySelector("#btn-mode-waypoints")?.addEventListener("click", () => setMoveMode("waypoints"));
  body.querySelector("#btn-exit-mode")?.addEventListener("click", () => exitMoveMode());
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
      const wp: MoveGroupWaypoint = {
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
  body.querySelectorAll<HTMLElement>(".event-route-item[data-wp-id], .move-wp-row[data-wp-id]").forEach((row) => {
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
      S.moveRouteAutoAdd = cb.checked;
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
    // 防御：空气地板（仅碰撞盒）不参与移动组烘焙。
    const f = S.floors.find((fl) => fl.instanceId === id);
    if (f && isAirFloor(f)) {
      setStatus("空气地板（仅碰撞盒）无法加入移动组", false);
      refresh();
      return;
    }
    pushHistory();
    group.floorInstanceIds.push(id);
    const tgt = pickTarget(group);
    if (tgt && !tgt.memberInstanceIds.includes(id)) tgt.memberInstanceIds.push(id);
    S.dirty = true;
    refresh();
  });

  // ---- events tab
  body.querySelectorAll<HTMLElement>("[data-subgroup-head]").forEach((head) => {
    head.addEventListener("click", (e) => {
      if ((e.target as HTMLElement).closest("button")) return;
      const idx = parseInt((head as HTMLElement).dataset.subgroupHead!);
      S.activeMoveEventIdx = S.activeMoveEventIdx === idx ? null : idx;
      refresh();
      draw();
    });
  });
  body.querySelectorAll<HTMLSelectElement>(".event-type").forEach((sel) => {
    sel.addEventListener("change", () => {
      const idx = parseInt(sel.dataset.idx!);
      pushHistory();
      group.events[idx].type = sel.value as MoveGroupEvent["type"];
      group.events[idx].triggerName = undefined;
      if (group.events[idx].type !== "move") {
        group.events[idx].loop = false;
        group.events[idx].pingpong = false;
      }
      if (group.events[idx].type === "lift" && !group.events[idx].yTo) {
        group.events[idx].yTo = 1;
      }
      S.dirty = true;
      refresh();
    });
  });
  const wireNumInput = (
    cls: string,
    get: (e: MoveGroupEvent) => number,
    set: (e: MoveGroupEvent, v: number) => void
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
  wireNumInput(".event-ysec", (e) => e.liftSeconds ?? 1, (e, v) => (e.liftSeconds = Math.max(0.05, v)));
  body.querySelectorAll<HTMLInputElement>(".event-delay").forEach((inp) => {
    inp.addEventListener("change", () => {
      const idx = parseInt(inp.dataset.idx!);
      const v = parseFloat(inp.value) || 0;
      if (v === group.events[idx].delay) return;
      pushHistory();
      group.events[idx].delay = v;
      S.dirty = true;
    });
  });
  body.querySelectorAll<HTMLInputElement>(".event-interval").forEach((inp) => {
    inp.addEventListener("change", () => {
      const idx = parseInt(inp.dataset.idx!);
      const v = parseFloat(inp.value) || 2;
      if (v === group.events[idx].intervalSeconds) return;
      pushHistory();
      group.events[idx].intervalSeconds = v;
      S.dirty = true;
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
      if (S.activeMoveEventIdx !== null) {
        if (S.activeMoveEventIdx === idx) S.activeMoveEventIdx = null;
        else if (S.activeMoveEventIdx > idx) S.activeMoveEventIdx--;
      }
      S.dirty = true;
      refresh();
    });
  });

  // ---- event drag reorder (with lift + insertion-line feedback)
  let dragEvtFrom: number | null = null;
  body.querySelectorAll<HTMLElement>("[data-drag-event]").forEach((handle) => {
    handle.addEventListener("dragstart", (e) => {
      dragEvtFrom = parseInt((handle as HTMLElement).dataset.dragEvent!);
      e.dataTransfer?.setData("text/plain", String(dragEvtFrom));
      if (e.dataTransfer) e.dataTransfer.effectAllowed = "move";
      handle.classList.add("dragging");
      // 拿起反馈：被拖卡片浮起
      const card = handle.closest(".subgroup-card") as HTMLElement | null;
      card?.classList.add("ev-dragging");
    });
    handle.addEventListener("dragend", () => {
      dragEvtFrom = null;
      handle.classList.remove("dragging");
      body.querySelectorAll(".subgroup-card.ev-dragging, .subgroup-card.drop-before, .subgroup-card.drop-after")
        .forEach((c) => c.classList.remove("ev-dragging", "drop-before", "drop-after"));
    });
  });
  body.querySelectorAll<HTMLElement>(".subgroup-card").forEach((card) => {
    card.addEventListener("dragover", (e) => {
      if (dragEvtFrom === null) return;
      e.preventDefault();
      e.dataTransfer!.dropEffect = "move";
      const rect = card.getBoundingClientRect();
      const above = e.clientY - rect.top < rect.height / 2;
      card.classList.toggle("drop-before", above);
      card.classList.toggle("drop-after", !above);
    });
    card.addEventListener("dragleave", () => card.classList.remove("drop-before", "drop-after"));
    card.addEventListener("drop", (e) => {
      if (dragEvtFrom === null) return;
      e.preventDefault();
      e.stopPropagation();
      const rect = card.getBoundingClientRect();
      const above = e.clientY - rect.top < rect.height / 2;
      card.classList.remove("drop-before", "drop-after");
      const to = parseInt((card as HTMLElement).dataset.eventIdx!);
      if (to >= 0 && to < group.events.length && to !== dragEvtFrom) {
        const from = dragEvtFrom;
        const activeEvt = S.activeMoveEventIdx !== null ? group.events[S.activeMoveEventIdx] : null;
        pushHistory();
        const moved = group.events.splice(from, 1)[0];
        let insertAt = to;
        if (from < to) insertAt = to - 1;
        if (!above) insertAt = insertAt + 1;
        group.events.splice(Math.max(0, Math.min(group.events.length, insertAt)), 0, moved);
        if (activeEvt) S.activeMoveEventIdx = group.events.indexOf(activeEvt);
        else S.activeMoveEventIdx = null;
        S.dirty = true;
        refresh();
        draw();
      }
      dragEvtFrom = null;
    });
  });
  body.querySelector("#btn-add-event")?.addEventListener("click", () => {
    pushHistory();
    group.events.push({
      id: uuid(),
      type: "move",
      delay: 0,
      intervalSeconds: 2,
      waypointIds: [],
    });
    S.activeMoveEventIdx = group.events.length - 1;
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
    if (!confirm(`确定删除移动组「${group.displayName}」及其 ${group.events.length} 个事件？`)) return;
    pushHistory();
    S.moveControls = S.moveControls.filter((g) => g.id !== S.activeMoveGroupId);
    cleanOrphanedButtonLinks();
    S.activeMoveGroupId = null;
    S.activeMoveEventIdx = null;
    S.selectedWaypointId = null;
    S.moveMode = "none";
    S.activeMoveTab = "members";
    S.movePickTargetGroupId = null;
    stopMovePreview();
    clearMoveSelection();
    S.dirty = true;
    refresh();
  });
}
