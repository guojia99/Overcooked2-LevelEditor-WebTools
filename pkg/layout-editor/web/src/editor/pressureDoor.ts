import { S, type DoorMotion, type EditorItem } from "./state";
import type { AnimGroup, AnimGroupDoorMotion, AnimGroupEvent } from "../types";
import { uuid, prefabIdFromPath } from "./coords";
import { catalogItemForGuidOrPath } from "./catalog";

export type DoorDirection = "cw" | "ccw";

/** 门自身保存的旋转参数；默认顺时针 90° / 0.5s。 */
export const DEFAULT_DOOR_MOTION: DoorMotion = {
  direction: "cw",
  angleDegrees: 90,
  durationSeconds: 0.5,
};

function rotateEventsOf(group: AnimGroup): AnimGroupEvent[] {
  return [...group.events]
    .filter((e) => e.type === "rotate")
    .sort((a, b) => (a.startTime ?? 0) - (b.startTime ?? 0));
}

/** 从动画组读出开门参数（第一个 rotate = 开门；缺省顺时针 90°/0.5s）。 */
export function holdGroupMotion(group: AnimGroup): DoorMotion {
  const open = rotateEventsOf(group)[0];
  if (!open) return { ...DEFAULT_DOOR_MOTION };
  return {
    direction: open.rotateDirection === "ccw" ? "ccw" : "cw",
    angleDegrees: open.rotateDegrees && open.rotateDegrees > 0 ? open.rotateDegrees : 90,
    durationSeconds: open.rotateSeconds && open.rotateSeconds > 0 ? open.rotateSeconds : 0.5,
  };
}

/** 把开门参数写进动画组：开门事件用 dir/angle/duration，关门事件反向同角度/时长。 */
export function setHoldGroupMotion(group: AnimGroup, motion: DoorMotion): void {
  const events = rotateEventsOf(group);
  if (events.length === 0) return;
  events[0].rotateDirection = motion.direction;
  events[0].rotateDegrees = motion.angleDegrees;
  events[0].rotateSeconds = motion.durationSeconds;
  for (let i = 1; i < events.length; i++) {
    events[i].rotateDirection = motion.direction === "cw" ? "ccw" : "cw";
    events[i].rotateDegrees = motion.angleDegrees;
    events[i].rotateSeconds = motion.durationSeconds;
  }
  S.dirty = true;
}

/** 门所在的按住式动画组。 */
export function holdGroupsOfDoor(doorId: string): AnimGroup[] {
  return S.animControls.filter(
    (g) => g.advanceMode === "hold" && g.itemInstanceIds.includes(doorId)
  );
}

/** 组内单门覆盖（未配置 = 沿用组事件默认值）。 */
export function doorMotionOverrideOf(
  group: AnimGroup,
  doorId: string
): DoorMotion | null {
  const o = (group.doorMotions ?? []).find((m) => m.instanceId === doorId);
  if (!o) return null;
  return {
    direction: o.direction === "ccw" ? "ccw" : "cw",
    angleDegrees: o.angleDegrees > 0 ? o.angleDegrees : 90,
    durationSeconds: o.durationSeconds > 0 ? o.durationSeconds : 0.5,
  };
}

/** 门当前生效的旋转参数：单门覆盖 > 组事件默认值 > 门自身默认值。 */
export function doorMotionOf(door: EditorItem): DoorMotion {
  const groups = holdGroupsOfDoor(door.instanceId ?? "");
  if (groups.length > 0) {
    const group = groups[0];
    return doorMotionOverrideOf(group, door.instanceId ?? "") ?? holdGroupMotion(group);
  }
  return door.doorMotion ? { ...DEFAULT_DOOR_MOTION, ...door.doorMotion } : { ...DEFAULT_DOOR_MOTION };
}

function motionsEqual(a: DoorMotion, b: DoorMotion): boolean {
  return a.direction === b.direction &&
    Math.abs(a.angleDegrees - b.angleDegrees) < 1e-4 &&
    Math.abs(a.durationSeconds - b.durationSeconds) < 1e-4;
}

/** 单门覆盖：与组默认一致时删掉覆盖（回落到组默认），否则新增/更新。 */
function ensureDoorMotionOverride(group: AnimGroup, doorId: string, motion: DoorMotion): void {
  const groupMotion = holdGroupMotion(group);
  if (!group.doorMotions) group.doorMotions = [];
  const idx = group.doorMotions.findIndex((m) => m.instanceId === doorId);
  if (motionsEqual(groupMotion, motion)) {
    if (idx >= 0) group.doorMotions.splice(idx, 1);
    if (group.doorMotions.length === 0) delete group.doorMotions;
    return;
  }
  const entry: AnimGroupDoorMotion = {
    instanceId: doorId,
    direction: motion.direction,
    angleDegrees: motion.angleDegrees,
    durationSeconds: motion.durationSeconds,
  };
  if (idx >= 0) group.doorMotions[idx] = entry;
  else group.doorMotions.push(entry);
}

/** 更新单扇门参数：只影响该门在各按住式组里的覆盖，不改其它门。 */
export function setDoorMotion(door: EditorItem, motion: DoorMotion): void {
  door.doorMotion = { ...motion };
  const doorId = door.instanceId ?? "";
  for (const group of holdGroupsOfDoor(doorId)) {
    ensureDoorMotionOverride(group, doorId, motion);
  }
  S.dirty = true;
}

/** 场景中的可联动门（DLC5 自动门、城市双开门、主题门等）。 */
export function isDoorItem(it: EditorItem | undefined): boolean {
  if (!it || !it.instanceId) return false;
  const cat = catalogItemForGuidOrPath(it.prefabGuid, it.prefabAssetPath);
  if (cat?.category === "mechanisms/doors") return true;
  const pid = prefabIdFromPath(it.prefabAssetPath) ?? "";
  if (!/door/i.test(pid)) return false;
  // 门框 / 门洞 / 过道不是可旋转门扇。
  if (/doorway|door_?frame|frame_?door/i.test(pid)) return false;
  return true;
}

/** 压力开关已绑定的按住式动画组（同一开关只保留一条 hold 联动）。 */
export function holdGroupsOfSwitch(switchId: string): AnimGroup[] {
  const out: AnimGroup[] = [];
  const seen = new Set<string>();
  for (const l of S.buttonLinks) {
    if (l.sourceId !== switchId) continue;
    for (const name of l.groupNames ?? []) {
      const g = S.animControls.find((x) => x.displayName === name);
      if (!g || g.advanceMode !== "hold" || seen.has(g.id)) continue;
      seen.add(g.id);
      out.push(g);
    }
  }
  return out;
}

/**
 * 为压力开关创建/追加按住式门联动。
 * - 已有 hold 组：把门追加到该组（同一开关可控制多扇门，每扇门独立方向/角度/时长）。
 * - 无 hold 组但有其它按钮联动：返回 null，避免静默覆盖既有配置。
 * - 无联动：创建一个含两段 rotate（站上开 / 离开关）的 hold 组。
 */
export function createPressureDoorHoldLink(
  sw: EditorItem,
  door: EditorItem,
  motion?: DoorMotion
): AnimGroup | null {
  if (!sw.instanceId || !door.instanceId) return null;
  sw.stubKind = "PressureSwitch";

  const existing = S.buttonLinks.find((l) => l.sourceId === sw.instanceId);
  if (existing) {
    if (!existing.groupNames || existing.groupNames.length !== 1) return null;
    if ((existing.sharedSourceIds ?? []).length > 0 || existing.pairId) return null;
    let group: AnimGroup | undefined;
    for (const name of existing.groupNames ?? []) {
      const g = S.animControls.find((x) => x.displayName === name);
      if (g?.advanceMode === "hold") {
        group = g;
        break;
      }
    }
    if (!group) return null;
    if (!group.itemInstanceIds.includes(door.instanceId)) {
      group.itemInstanceIds.push(door.instanceId);
    }
    // 每扇门独立参数：只给新门写自己的覆盖，不影响组内其它门。
    const motion = doorMotionOf(door);
    door.doorMotion = { ...motion };
    ensureDoorMotionOverride(group, door.instanceId, motion);
    S.dirty = true;
    return group;
  }

  let name = `自动门 ${S.animControls.length + 1}`;
  if (S.animControls.some((g) => g.displayName === name)) {
    let n = 1;
    while (S.animControls.some((g) => g.displayName === `自动门 ${S.animControls.length + 1} (${n})`)) n++;
    name = `自动门 ${S.animControls.length + 1} (${n})`;
  }

  const actual = motion ?? doorMotionOf(door);
  door.doorMotion = { ...actual };
  const group: AnimGroup = {
    id: uuid(),
    displayName: name,
    groupKind: "members",
    triggerMode: "button",
    advanceMode: "hold",
    itemInstanceIds: [door.instanceId],
    floorInstanceIds: [],
    objectInstanceIds: [],
    memberOffsets: [],
    memberStatic: [],
    memberGroups: [],
    startDelay: 0,
    loop: false,
    loopDelay: 2,
    waitForFinished: false,
    waypoints: [],
    events: [
      {
        id: uuid(),
        type: "rotate",
        triggerName: "DoorOpen",
        delay: 0,
        startTime: 0,
        rotateDegrees: actual.angleDegrees,
        rotateDirection: actual.direction,
        rotateSeconds: actual.durationSeconds,
      },
      {
        id: uuid(),
        type: "rotate",
        triggerName: "DoorClose",
        delay: 0,
        startTime: 1,
        rotateDegrees: actual.angleDegrees,
        rotateDirection: actual.direction === "cw" ? "ccw" : "cw",
        rotateSeconds: actual.durationSeconds,
      },
    ],
  };
  S.animControls.push(group);
  S.buttonLinks.push({
    id: uuid(),
    sourceId: sw.instanceId,
    groupNames: [name],
    sequenceMode: "loop",
    lockUntilFinished: false,
  });
  S.dirty = true;
  return group;
}

/** 从按住式组移除一扇门；组空后同时移除组与按钮联动。 */
export function removeDoorFromHoldGroup(groupId: string, doorInstanceId: string): void {
  const group = S.animControls.find((g) => g.id === groupId);
  if (!group) return;
  group.itemInstanceIds = group.itemInstanceIds.filter((id) => id !== doorInstanceId);
  if (group.doorMotions) {
    group.doorMotions = group.doorMotions.filter((m) => m.instanceId !== doorInstanceId);
    if (group.doorMotions.length === 0) delete group.doorMotions;
  }
  if (group.itemInstanceIds.length > 0 ||
      group.floorInstanceIds.length > 0 ||
      group.objectInstanceIds.length > 0) {
    S.dirty = true;
    return;
  }
  S.animControls = S.animControls.filter((g) => g !== group);
  for (const l of S.buttonLinks) {
    l.groupNames = (l.groupNames ?? []).filter((n) => n !== group.displayName);
  }
  S.buttonLinks = S.buttonLinks.filter(
    (l) => l.groupNames.length > 0 || (l.sharedSourceIds?.length ?? 0) > 0 || !!l.pairId
  );
  S.dirty = true;
}
