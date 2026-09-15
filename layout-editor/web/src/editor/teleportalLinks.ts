/**
 * 传送门方向模型（入口 / 出口 / 双向）与配对改写的唯一收口。
 *
 * 游戏侧语义（与引擎 1:1）：Teleportal.m_exitPortal 是方向的唯一来源——
 * 「A 能传到 B」当且仅当 A 的出口 = B；双向就是两扇门互指，引擎里没有
 * 「双向开关」这种东西。出口为空的门只收不发。
 *
 * 编辑器表达方式（2026-09-15）：
 *  - 入口门：exitPortalInstanceId = 出口门；
 *  - 单向的出口门：exitOnly = true，且 exitPortalInstanceId 回指入口门——
 *    这个回指是【占位】：宿主 LevelEditor.PseudoPrefabTeleportal.LateSetup 与真机
 *    mod 的同源逻辑都对空出口无判空即 NRE（写回链会把该门降级成装饰件）。
 *    真正的单向由运行时 CustomStub.TeleportalExitOnly 把 m_exitPortal 清回 null 实现；
 *  - 双向：两扇门互指且都不是 exitOnly。
 *
 * 注意：`doubleSided` 是【双面外观】（宿主只克隆了一份门框视觉），与方向无关——
 * 历史上它在 UI 里被错标成「双向」，是「取消双向仍然双向传送」的直接原因。
 */
import { prefabIdFromPath } from "./coords";
import { S, EditorItem } from "./state";

export function isTeleportalItem(item: EditorItem): boolean {
  return item.stubKind === "Teleportal" || prefabIdFromPath(item.prefabAssetPath) === "Teleportal";
}

export function teleportals(): EditorItem[] {
  return S.items.filter(isTeleportalItem);
}

/** 传送门方向：
 *  - "two"      双向（互指且两侧都能进）
 *  - "entrance" 单向入口（本门发送到出口门）
 *  - "exit"     仅作为出口（本门不发送）
 *  - "unbound"  未绑定出口（写回会被阻断） */
export type TeleportalRole = "two" | "entrance" | "exit" | "unbound";

export function teleportalRole(item: EditorItem, byInst?: Map<string, EditorItem>): TeleportalRole {
  const tp = item.teleportal;
  if (tp?.exitOnly) return "exit";
  const exitId = tp?.exitPortalInstanceId;
  if (!exitId || exitId === item.instanceId) return "unbound";
  const lookup = byInst ?? new Map(teleportals().map((i) => [i.instanceId, i]));
  const partner = lookup.get(exitId);
  if (!partner) return "unbound";
  const back = partner.teleportal;
  if (!back?.exitOnly && back?.exitPortalInstanceId === item.instanceId) return "two";
  return "entrance";
}

/** 指向本门的入口门（exitOnly 的回指占位不算入口）。 */
export function teleportalEntrancesOf(item: EditorItem): EditorItem[] {
  return teleportals().filter(
    (t) =>
      t.instanceId !== item.instanceId &&
      !t.teleportal?.exitOnly &&
      t.teleportal?.exitPortalInstanceId === item.instanceId
  );
}

export function teleportalById(instanceId: string | undefined): EditorItem | undefined {
  if (!instanceId) return undefined;
  return teleportals().find((t) => t.instanceId === instanceId);
}

function ensureStub(item: EditorItem) {
  item.stubKind = "Teleportal";
  if (!item.teleportal)
    item.teleportal = { exitPortalInstanceId: "", portalColor: 0, doubleSided: false, exitOnly: false };
  return item.teleportal;
}

/**
 * 设置「入口门 → 出口门」这一对的方向（对级语义，UI 的「双向传送」开关即此）。
 *  - twoWay=true ：出口门 exitOnly=false 且回指入口（互指）——会覆盖它原有的出口；
 *  - twoWay=false：
 *      · 出口门未绑定或正回指本门 → exitOnly=true + 回指占位（占位防宿主 NRE，
 *        运行时把 m_exitPortal 清回 null）；
 *      · 出口门已指向第三扇门（链式 A→B→C）→ 保持不动：它本来就不会传回 A，
 *        A→B 已经是单向，不该把 B→C 一并掐断。
 * 入口门本身的 exitOnly 一律清掉（它要发送）。
 */
export function setTeleportalPairDirection(entrance: EditorItem, twoWay: boolean): void {
  const tp = ensureStub(entrance);
  tp.exitOnly = false;
  const partner = teleportalById(tp.exitPortalInstanceId);
  if (!partner) return;
  const back = ensureStub(partner);
  if (twoWay) {
    back.exitOnly = false;
    back.exitPortalInstanceId = entrance.instanceId;
    return;
  }
  const backExit = back.exitPortalInstanceId;
  if (!backExit || backExit === entrance.instanceId) {
    back.exitPortalInstanceId = entrance.instanceId;
    back.exitOnly = true;
  }
  // 链式（backExit 指向第三扇门）：保持原样，A→B 已是单向
}

/**
 * 解绑前的清理：若 prevExitId 指向的门是本门的专属出口（exitOnly 且只被本门指向），
 * 把它复位成普通未绑定门——否则会留下「没有入口指向的仅出口门」，写回被校验阻断。
 * 返回被复位的门（供状态栏提示；未复位则返回 undefined）。
 */
export function releaseExitOnlyPartner(entrance: EditorItem, prevExitId: string): EditorItem | undefined {
  const partner = teleportalById(prevExitId);
  if (!partner?.teleportal?.exitOnly) return undefined;
  const others = teleportalEntrancesOf(partner).filter((t) => t.instanceId !== entrance.instanceId);
  if (others.length > 0) return undefined; // 还有别的入口指向它，保持仅出口
  partner.teleportal.exitOnly = false;
  if (partner.teleportal.exitPortalInstanceId === entrance.instanceId)
    partner.teleportal.exitPortalInstanceId = "";
  return partner;
}

/** 配对组号：一对/一组互相引用的门共用同一个数字（方向另由徽标区分）。 */
export function computeTeleportalLabels(): Map<string, string> {
  const tp = teleportals();
  const byInst = new Map(tp.map((i) => [i.instanceId, i]));
  const label = new Map<string, string>();
  let n = 0;
  for (const t of tp) {
    if (label.has(t.instanceId)) continue;
    const lab = (++n).toString();
    label.set(t.instanceId, lab);
    const exitId = t.teleportal?.exitPortalInstanceId;
    if (exitId && byInst.has(exitId) && !label.has(exitId)) label.set(exitId, lab);
  }
  for (const t of tp) if (!label.has(t.instanceId)) label.set(t.instanceId, "?");
  return label;
}
