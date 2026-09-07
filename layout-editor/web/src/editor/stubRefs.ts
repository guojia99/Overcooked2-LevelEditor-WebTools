/**
 * 物品 stub 内部绑定引用的统一重映射与孤儿清理。
 *
 * 覆盖的引用种类（文档级 + 物品级）：
 *  - S.switchLinks                开关 → 饮料机/酱料机/断头台联动
 *  - it.servingStation            上菜台 → 脏盘台/脏杯台（1 对多 + legacy 单绑定镜像）
 *  - it.teleportal                传送门 → 出口传送门
 *  - it.terminal                  终端 → 可操控对象（大炮等）
 *  - it.heatedOven                石炉台 → 热源工作台
 *  - S.buttonLinks / S.buttonEvents   按钮联动（sourceId/events.targetId 为 instanceId）
 *  - S.animControls               动画组成员 id
 *
 * 历史问题：这些引用分散在各处维护——删除物品只清了开关/按钮联动，
 * loadScene 自动去重只清了动画组/按钮联动，复制粘贴完全不重映射，
 * 变体切换的重映射漏了加热炉热源。指向已删/已换 id 的引用会在写回时
 * 被 Unity 侧丢弃（绑定失效要重新绑的根因之一），必须在这里统一收口。
 */
import { S, EditorItem } from "./state";

/** 单个物品内的引用按映射表改写（m 不在表内的原样返回）。 */
function remapItemRefs(it: EditorItem, m: (x: string) => string): void {
  if (it.teleportal?.exitPortalInstanceId)
    it.teleportal.exitPortalInstanceId = m(it.teleportal.exitPortalInstanceId);
  if (it.terminal?.pilotableObjectInstanceId)
    it.terminal.pilotableObjectInstanceId = m(it.terminal.pilotableObjectInstanceId);
  if (it.heatedOven?.heatedStationInstanceId)
    it.heatedOven.heatedStationInstanceId = m(it.heatedOven.heatedStationInstanceId);
  if (it.servingStation) {
    if (it.servingStation.plateReturnInstanceId)
      it.servingStation.plateReturnInstanceId = m(it.servingStation.plateReturnInstanceId);
    if (it.servingStation.plateReturnInstanceIds)
      it.servingStation.plateReturnInstanceIds = it.servingStation.plateReturnInstanceIds.map(m);
  }
}

/** 所有文档级/物品级 instanceId 引用按映射表整体改写。
 *  变体切换（单条映射）与成套复制粘贴（多条映射）共用；不在表内的 id 原样保留。 */
export function remapAllInstanceRefs(map: Map<string, string>): void {
  if (map.size === 0) return;
  const m = (x: string) => map.get(x) ?? x;
  for (const l of S.switchLinks) {
    l.switchId = m(l.switchId);
    l.targetId = m(l.targetId);
  }
  for (const l of S.buttonLinks) {
    l.sourceId = m(l.sourceId);
  }
  for (const l of S.buttonEvents) {
    l.sourceId = m(l.sourceId);
    for (const g of l.groups) {
      for (const ev of g.events) {
        ev.targetId = m(ev.targetId);
      }
    }
  }
  for (const mg of S.animControls) {
    mg.itemInstanceIds = mg.itemInstanceIds.map(m);
    if (mg.memberStatic) {
      for (const mm of mg.memberStatic) mm.instanceId = m(mm.instanceId);
    }
    if (mg.memberGroups) {
      for (const g of mg.memberGroups) g.memberInstanceIds = g.memberInstanceIds.map(m);
    }
  }
  for (const it of S.items) remapItemRefs(it, m);
}

/** 只重映射给定物品集合内部的引用（复制粘贴用）：一起复制的物品之间
 *  绑定跟随副本；引用不在本次复制集合内的原 id 保持不变（原物体仍存活，
 *  引用继续有效；若指向的物体后续被删，由 cleanOrphanedStubRefs 收口）。
 *  注意不能在这里用 remapAllInstanceRefs——那会把场景里其他物品对"被复制
 *  原物体"的绑定也错误地改指到副本上。 */
export function remapRefsWithinItems(items: EditorItem[], map: Map<string, string>): void {
  if (map.size === 0) return;
  const m = (x: string) => map.get(x) ?? x;
  for (const it of items) remapItemRefs(it, m);
}

/** 清理指向当前物品列表之外的所有绑定引用，返回清理条数（供状态栏汇报）。
 *  空引用（""/undefined）视为未绑定，不算孤儿。 */
export function cleanOrphanedStubRefs(): number {
  const live = new Set(S.items.map((i) => i.instanceId).filter(Boolean));
  let removed = 0;

  const before = S.switchLinks.length;
  S.switchLinks = S.switchLinks.filter(
    (l) => live.has(l.switchId) && live.has(l.targetId)
  );
  removed += before - S.switchLinks.length;

  for (const it of S.items) {
    const t = it.teleportal;
    if (t?.exitPortalInstanceId && !live.has(t.exitPortalInstanceId)) {
      t.exitPortalInstanceId = "";
      removed++;
    }
    const term = it.terminal;
    if (term?.pilotableObjectInstanceId && !live.has(term.pilotableObjectInstanceId)) {
      term.pilotableObjectInstanceId = "";
      removed++;
    }
    const ho = it.heatedOven;
    if (ho?.heatedStationInstanceId && !live.has(ho.heatedStationInstanceId)) {
      ho.heatedStationInstanceId = "";
      removed++;
    }
    const ss = it.servingStation;
    if (ss) {
      if (ss.plateReturnInstanceIds?.length) {
        const kept = ss.plateReturnInstanceIds.filter((id) => live.has(id));
        removed += ss.plateReturnInstanceIds.length - kept.length;
        ss.plateReturnInstanceIds = kept;
        // 与 plateReturnInstanceIds 保持镜像（sceneIO 同步导出时同款约定）。
        ss.plateReturnInstanceId = kept[0] ?? "";
      } else if (ss.plateReturnInstanceId && !live.has(ss.plateReturnInstanceId)) {
        ss.plateReturnInstanceId = "";
        removed++;
      }
    }
  }
  return removed;
}
