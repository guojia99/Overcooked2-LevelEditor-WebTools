import {
  S,
  EditorItem
} from "./state";
import type { AnimGroup, ButtonLink, CoaxialLink } from "../types";
import { stubKindOf } from "./stubControls";
import { itemLabel } from "./labels";
import { uuid, escHtml } from "./coords";
import { pushHistory } from "./historyOps";
import { setStatus } from "./status";
import { draw } from "./render";

/** 触发源类型：Switch 按钮 / PressureSwitch 压力开关。 */
export function isButtonLinkSource(it: EditorItem): boolean {
  const kind = stubKindOf(it);
  return kind === "Switch" || kind === "PressureSwitch";
}

export function linkOfSource(sourceId: string): ButtonLink | undefined {
  return (
    S.buttonLinks.find((l) => l.sourceId === sourceId) ??
    S.buttonLinks.find((l) => (l.sharedSourceIds ?? []).includes(sourceId))
  );
}

export function ensureLink(sourceId: string): ButtonLink {
  const existing = linkOfSource(sourceId);
  if (existing) return existing;
  const link: ButtonLink = {
    id: uuid(),
    sourceId,
    groupNames: [],
    lockUntilFinished: true,
  };
  S.buttonLinks.push(link);
  return link;
}

/** 互锁对中的另一方（同 pairId 的另一条 link）。 */
export function partnerOf(link: ButtonLink): ButtonLink | undefined {
  if (!link.pairId) return undefined;
  return S.buttonLinks.find((l) => l !== link && l.pairId === link.pairId);
}

/** 某动画组是否已被任一联动绑定（一个组至多属于一条联动）。 */
export function linkBindingGroup(groupName: string): ButtonLink | undefined {
  return S.buttonLinks.find((l) => l.groupNames.includes(groupName));
}

/** 互锁模式下单个按钮最多绑定的动画组数（两组同时启动、全部完成后翻转）。 */
export const PAIR_GROUP_LIMIT = 2;

/**
 * 清理失效联动：源物品被删、动画组被删/改名、配对另一方缺失。
 * 动画组以 displayName 引用（跨保存稳定），改名由 animControl 的改名处同步。
 * 共轭按钮：逐个剔除已删 id；主源被删但有存活共轭按钮时提升首个共轭为主源。
 */
function animGroupHasMembers(g: AnimGroup): boolean {
  return (
    (g.itemInstanceIds?.length ?? 0) > 0 ||
    (g.floorInstanceIds?.length ?? 0) > 0 ||
    (g.objectInstanceIds?.length ?? 0) > 0
  );
}

/** 去掉共轭/互锁自动创建的零成员占位动画组（动画应在触发编排里单独配置）。 */
export function pruneEmptyPartnerPlaceholderGroups(): void {
  const placeholder =
    /^(共轭双按钮 |互锁双按钮 |共轭 · |互锁 · )/;
  S.animControls = S.animControls.filter((g) => {
    if (!placeholder.test(g.displayName)) return true;
    if (animGroupHasMembers(g)) return true;
    return false;
  });
}

function linkHasPurpose(l: ButtonLink): boolean {
  if (l.groupNames.length > 0) return true;
  if ((l.sharedSourceIds ?? []).length > 0) return true;
  if (l.pairId) return true;
  return false;
}

export function cleanOrphanedButtonLinks(): void {
  pruneEmptyPartnerPlaceholderGroups();
  const itemIds = new Set(S.items.map((i) => i.instanceId).filter(Boolean));
  const groupNames = new Set(S.animControls.map((g) => g.displayName));
  for (const l of S.buttonLinks) {
    l.groupNames = l.groupNames.filter((n) => groupNames.has(n));
    l.sharedSourceIds = (l.sharedSourceIds ?? []).filter((id) => itemIds.has(id) && id !== l.sourceId);
    if (!itemIds.has(l.sourceId) && (l.sharedSourceIds ?? []).length > 0) {
      l.sourceId = l.sharedSourceIds.shift()!;
    }
  }
  S.buttonLinks = S.buttonLinks.filter(
    (l) => itemIds.has(l.sourceId) && linkHasPurpose(l)
  );
  // 配对完整性：partner 缺失时解除配对。
  for (const l of S.buttonLinks) {
    if (l.pairId && !partnerOf(l)) {
      l.pairId = undefined;
      l.pairStartsUp = undefined;
    }
  }
}

// ---------- 同轴按钮组 ----------

/** 某按钮所在的同轴组（一只按钮只属一组）。 */
export function coaxialGroupOf(sourceId: string): CoaxialLink | undefined {
  return S.coaxialLinks.find((g) => g.sourceIds.includes(sourceId));
}

/** 清理失效同轴组：成员/目标被删时剔除引用；成员 <2 的组整体移除；
 *  目标与触发名平行（triggers 按剩余目标截齐）。 */
export function cleanOrphanedCoaxialLinks(): void {
  const itemIds = new Set(S.items.map((i) => i.instanceId).filter(Boolean));
  for (const g of S.coaxialLinks) {
    g.sourceIds = g.sourceIds.filter((id) => itemIds.has(id));
    const kept: string[] = [];
    const keptTriggers: string[] = [];
    (g.targetIds ?? []).forEach((id, i) => {
      if (!itemIds.has(id)) return;
      kept.push(id);
      keptTriggers.push(g.triggers?.[i] ?? "");
    });
    g.targetIds = kept;
    g.triggers = keptTriggers;
    g.windowSeconds = Math.max(0.35, Math.min(5, g.windowSeconds || 1));
  }
  S.coaxialLinks = S.coaxialLinks.filter((g) => g.sourceIds.length >= 2);
}

/** 移除与任一指定按钮相关的同轴组（重套组合/换绑前清旧）。 */
export function clearCoaxialGroupsForSwitches(...ids: (string | undefined)[]): void {
  const idSet = new Set(ids.filter(Boolean) as string[]);
  S.coaxialLinks = S.coaxialLinks.filter((g) => !g.sourceIds.some((id) => idSet.has(id)));
}

/** 动画组改名时同步联动里的引用（displayName 是引用键）。 */
export function renameGroupInButtonLinks(oldName: string, newName: string): void {
  if (!oldName || oldName === newName) return;
  for (const l of S.buttonLinks) {
    l.groupNames = l.groupNames.map((n) => (n === oldName ? newName : n));
  }
}

/** 为开关联动创建占位动画组（waypoint 落在开关位置，供 combos / 编排台复用）。 */
export function createSwitchAnimGroup(source: EditorItem, displayName?: string): AnimGroup {
  const x = source._wx;
  const z = source._wz;
  const waypointId = uuid();
  return {
    id: uuid(),
    displayName: displayName ?? `开关动画组 ${S.animControls.length + 1}`,
    groupKind: "members",
    triggerMode: "button",
    itemInstanceIds: [],
    floorInstanceIds: [],
    objectInstanceIds: [],
    memberOffsets: [],
    memberStatic: [],
    memberGroups: [],
    startDelay: 0,
    loop: false,
    loopDelay: 2,
    // 按钮组的完成回报必须等 clip 播完（BLDone 由 AnimationFinished 驱动），
    // 否则「完成后才可再按」的锁定立即解锁。写回时 PrepareGroups 也会强制。
    waitForFinished: true,
    waypoints: [{ id: waypointId, x, z }],
    events: [{
      id: uuid(),
      type: "move",
      delay: 0,
      startTime: 0,
      intervalSeconds: 2,
      waypointIds: [waypointId],
    }],
  };
}

// ---------------------------------------------------------------- UI

/** 触发编排「③ 动画组序列」分区渲染选项。 */
export interface ButtonLinkSectionOpts {
  /** 「编辑」某动画组：切到单组编排（模式 A）。 */
  onEditGroup: (groupId: string) => void;
  /** 结构性变更后重绘整个编排台（跨分区联动）。 */
  rerender: () => void;
}

/** 联动摘要文案（右键菜单/触发源列表复用）。 */
export function buttonLinkSummaryText(item: EditorItem): string {
  const link = linkOfSource(item.instanceId ?? "");
  if (!link) return "— 未配置 —";
  const mode = buttonPartnerMode(item);
  const n = link.groupNames.length;
  const parts: string[] = [];
  if (mode === "conjugate") parts.push("共轭配对");
  else if (mode === "interlock") parts.push("互锁配对");
  if (n > 0) {
    parts.push(
      `已绑 ${n} 组 · ${link.simultaneous ? "同按" : link.sequenceMode === "pingpong" ? "往返" : "循环"}`
    );
    if (link.lockUntilFinished !== false) parts.push("完成后才可再按");
  } else if (mode !== "none") {
    parts.push("动画在触发编排中单独配置");
  } else {
    return "— 未绑定动画组 —";
  }
  return parts.join(" · ");
}

function pairHintText(partner: ButtonLink | undefined): string {
  if (!partner)
    return "不配对时每次按压启动下一组；可选择循环或往返模式。";
  const partnerItem = S.items.find((i) => i.instanceId === partner.sourceId);
  return `互锁模式：与「${partnerItem ? itemLabel(partnerItem) : "?"}」互斥，每个按钮需各绑至多 ${PAIR_GROUP_LIMIT} 个动画组；按下时本侧各组同时启动，全部完成后对方抬起。`;
}

function buttonLinkSectionHtml(item: EditorItem): string {
  const link = linkOfSource(item.instanceId ?? "");
  const partner = link ? partnerOf(link) : undefined;

  // 配对候选：其他 Switch / PressureSwitch 物品
  const pairOpts = ['<option value="">— 不配对 —</option>']
    .concat(
      S.items
        .filter((i) => i.instanceId && i.instanceId !== item.instanceId && isButtonLinkSource(i))
        .map((i) => {
          const sel = partner && partner.sourceId === i.instanceId ? "selected" : "";
          return `<option value="${escHtml(i.instanceId)}" ${sel}>${escHtml(itemLabel(i))}</option>`;
        })
    )
    .join("");

  const mode = link?.sequenceMode ?? "loop";
  const paired = !!partner;
  return `<p class="trig-hint">每次按压触发下一步动画组；「逐节点」的组每按一次只推进一个事件节点（相同开始时间的事件并行，末尾环回）。启动 / 结束触发器由联动自动管理（无需在动画组里手动设置）。</p>
    <div id="blm-groups" class="trig-list"></div>
     <div class="trig-addrow"><select id="blm-groupadd" class="trig-select"></select>
       <button type="button" class="btn-small" id="blm-add">添加已有组</button>
       <button type="button" class="btn-small primary" id="blm-new-group">＋ 新建并编辑</button></div>
     ${paired ? "" : `<div class="trig-subhead">共轭按钮（同时开/关，任一按压等同）</div>
     <div id="blm-shared" class="trig-list"></div>
     <div class="trig-addrow"><select id="blm-shared-add" class="trig-select"></select>
       <button type="button" class="btn-small" id="blm-shared-btn">＋ 添加共轭按钮</button></div>`}
     <label class="trig-check"><input type="checkbox" id="blm-lock" ${!link || link.lockUntilFinished !== false ? "checked" : ""}/> 动画组完成后才可再按（运行期忽略按压）</label>
     <label class="trig-check"><input type="checkbox" id="blm-simul" ${link?.simultaneous ? "checked" : ""}/> 同按模式：一次按压同时启动全部组（各组独立推进，最快组完成即解锁）</label>
    <label class="trig-field">播放模式 <select id="blm-mode" class="trig-select">
      <option value="loop" ${mode === "loop" ? "selected" : ""}>循环：A → B → C → A</option>
      <option value="pingpong" ${mode === "pingpong" ? "selected" : ""}>往返：A → B → C → B → A</option>
    </select></label>
    <div class="trig-subhead">互锁按钮（一对一）</div>
    <label class="trig-field">配对按钮 <select id="blm-pair" class="trig-select">${pairOpts}</select></label>
    <label class="trig-check"><input type="checkbox" id="blm-startup" ${link?.pairStartsUp !== false ? "checked" : ""} ${partner ? "" : "disabled"}/> 初始为抬起（可按）状态</label>
    <p class="trig-hint" id="blm-pair-hint">${escHtml(pairHintText(partner))}</p>`;
}

/** 在给定容器内渲染 + 接线「③ 动画组序列」分区（供触发编排台调用）。 */
export function renderButtonLinkSection(host: HTMLElement, item: EditorItem, opts: ButtonLinkSectionOpts): void {
  const myId = item.instanceId ?? "";
  if (!myId) {
    host.innerHTML = '<p class="trig-hint">该物件无实例 id，无法配置。</p>';
    return;
  }
  host.innerHTML = buttonLinkSectionHtml(item);

  const groupsEl = host.querySelector<HTMLElement>("#blm-groups");
  const addSel = host.querySelector<HTMLSelectElement>("#blm-groupadd");
  if (!groupsEl || !addSel) return;

  const link = () => linkOfSource(myId);

  const refresh = () => {
    const l = link();
    if (!l || l.groupNames.length === 0) {
      groupsEl.innerHTML = '<p class="trig-hint">未绑定动画组</p>';
    } else {
      const sharedBanner = l.sourceId !== myId
        ? `<div class="trig-hint" style="color:#7ec8a9">🔗 该按钮与「${escHtml(S.items.find((i) => i.instanceId === l.sourceId) ? itemLabel(S.items.find((i) => i.instanceId === l.sourceId)!) : l.sourceId)}」共轭联动（同时开/关，任一按压等同）</div>`
        : "";
      groupsEl.innerHTML = sharedBanner + l.groupNames
        .map((n, i) => {
          const g = S.animControls.find((gr) => gr.displayName === n);
          const press = g?.advanceMode === "press";
          const advSel = g
            ? `<select class="trig-select blm-adv" data-bl-adv="${escHtml(n)}" title="整组连播：一按播完整组（组内事件按时间轴连播）&#10;逐节点：一按只推进一个事件节点，相同开始时间的事件并行，末尾环回第一个节点&#10;单个旋转事件自动往返：按一次转过去、再按转回">
                <option value="timeline"${press ? "" : " selected"}>▶ 整组连播</option>
                <option value="press"${press ? " selected" : ""}>⏭ 逐节点</option>
              </select>`
            : "";
          return `<div class="trig-step"><span class="trig-step-idx">${i + 1}</span>
            <span class="trig-step-label">${escHtml(n)}</span>
            ${advSel}
            <button type="button" class="btn-small" data-bl-edit="${escHtml(n)}" title="编辑该动画组的成员与时间轴">✎ 编辑</button>
            <button type="button" class="btn-small blm-mini" data-bl-up="${i}" ${i === 0 ? "disabled" : ""}>↑</button>
            <button type="button" class="btn-small blm-mini" data-bl-down="${i}" ${i === l.groupNames.length - 1 ? "disabled" : ""}>↓</button>
            <button type="button" class="btn-small blm-mini" data-bl-del="${i}">移除</button></div>`;
        })
        .join("");
      groupsEl.querySelectorAll<HTMLSelectElement>(".blm-adv").forEach((sel) => {
        sel.addEventListener("change", () => {
          const g = S.animControls.find((gr) => gr.displayName === sel.dataset.blAdv);
          if (!g) return;
          const v = sel.value === "press" ? "press" : "timeline";
          if (g.advanceMode === v || (v === "timeline" && !g.advanceMode)) return;
          pushHistory();
          g.advanceMode = v;
          if (v === "press") {
            // 节点必须终止才能回报完成：剥离整组循环与事件循环（与组设置同款约束）。
            g.loop = false;
            for (const evt of g.events) {
              evt.loop = false;
              evt.pingpong = false;
            }
            setStatus(`「${g.displayName}」已切换为逐节点：每按一次推进一个事件节点（相同开始时间并行）`);
          } else {
            setStatus(`「${g.displayName}」已切换为整组连播：每按一次播完整组`);
          }
          S.dirty = true;
          refresh();
          refreshAddSel();
        });
      });
      groupsEl.querySelectorAll<HTMLButtonElement>("[data-bl-edit]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const g = S.animControls.find((gr) => gr.displayName === btn.dataset.blEdit);
          if (g) opts.onEditGroup(g.id);
        });
      });
      groupsEl.querySelectorAll<HTMLButtonElement>("[data-bl-up]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const i = parseInt(btn.dataset.blUp!, 10);
          const ll = link();
          if (!ll || i <= 0) return;
          pushHistory();
          [ll.groupNames[i - 1], ll.groupNames[i]] = [ll.groupNames[i], ll.groupNames[i - 1]];
          refresh();
        });
      });
      groupsEl.querySelectorAll<HTMLButtonElement>("[data-bl-down]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const i = parseInt(btn.dataset.blDown!, 10);
          const ll = link();
          if (!ll || i >= ll.groupNames.length - 1) return;
          pushHistory();
          [ll.groupNames[i], ll.groupNames[i + 1]] = [ll.groupNames[i + 1], ll.groupNames[i]];
          refresh();
        });
      });
      groupsEl.querySelectorAll<HTMLButtonElement>("[data-bl-del]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const i = parseInt(btn.dataset.blDel!, 10);
          const ll = link();
          if (!ll) return;
          pushHistory();
          ll.groupNames.splice(i, 1);
          if (ll.groupNames.length === 0 && !ll.pairId) {
            S.buttonLinks = S.buttonLinks.filter((x) => x !== ll);
          }
          setStatus("已移除联动动画组（写回后生效）");
          refresh();
          refreshAddSel();
        });
      });
    }
  };

  const refreshAddSel = () => {
    const l = link();
    const mine = new Set(l?.groupNames ?? []);
    const paired = !!(l && partnerOf(l));
    const limitReached = paired && mine.size >= PAIR_GROUP_LIMIT;
    // 候选包含全部成员组（不限 triggerMode——自动组绑定后即转为按钮触发，
    // 添加处理里已自动打标）；特效组（shake/flash）宿主在相机/灯、不在
    // Design/Animated Objects 下，无法走 ButtonLink 绑定，排除。
    const opt = S.animControls
      .filter((g) => g.groupKind !== "fx")
      .filter((g) => !mine.has(g.displayName))
      .map((g) => {
        const boundBy = linkBindingGroup(g.displayName);
        // 互锁共享环：组被【本对 partner】绑定时允许本侧同绑（ARun/BRun 分发
        // 同一组、交替推进，如「传送带阵 ×4 + 互锁双按钮」）。
        const partnerShared = !!(l?.pairId && boundBy && boundBy.pairId === l.pairId);
        const disabled = (boundBy && !partnerShared) || limitReached ? "disabled" : "";
        const suffix = boundBy
          ? partnerShared
            ? "（互锁共享）"
            : "（已被其他按钮绑定）"
          : limitReached
            ? `（互锁模式最多 ${PAIR_GROUP_LIMIT} 组）`
            : g.triggerMode === "button"
              ? ""
              : "（自动组 · 绑定后转为按钮触发）";
        return `<option value="${escHtml(g.displayName)}" ${disabled}>${escHtml(g.displayName)}${suffix}</option>`;
      })
      .join("");
    addSel.innerHTML = opt || '<option value="">— 无可绑定的动画组（场景中还没有动画组，可「＋ 新建并编辑」） —</option>';
  };

  refresh();
  refreshAddSel();

  // ---- 共轭按钮（同时开/关，任一按压等同）----
  const sharedEl = host.querySelector<HTMLElement>("#blm-shared");
  const sharedAddSel = host.querySelector<HTMLSelectElement>("#blm-shared-add");
  const refreshShared = () => {
    if (!sharedEl || !sharedAddSel) return;
    const l = link();
    const sharedIds = l?.sharedSourceIds ?? [];
    if (!l || l.groupNames.length === 0) {
      sharedEl.innerHTML = '<p class="trig-hint">先绑定动画组，再添加共轭按钮</p>';
      sharedAddSel.innerHTML = "";
      return;
    }
    sharedEl.innerHTML = sharedIds.length
      ? sharedIds
          .map((id) => {
            const it = S.items.find((i) => i.instanceId === id);
            return `<div class="trig-step"><span class="trig-step-idx">🔗</span>
              <span class="trig-step-label">${escHtml(it ? itemLabel(it) : id)}</span>
              <button type="button" class="btn-small blm-mini" data-unshare="${escHtml(id)}">移除</button></div>`;
          })
          .join("")
      : '<p class="trig-hint">无共轭按钮（只有本按钮触发）</p>';
    sharedEl.querySelectorAll<HTMLButtonElement>("[data-unshare]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const l2 = link();
        if (!l2) return;
        pushHistory();
        l2.sharedSourceIds = (l2.sharedSourceIds ?? []).filter((id) => id !== btn.dataset.unshare);
        setStatus("已移除共轭按钮（写回后生效）");
        refreshShared();
        opts.rerender();
      });
    });
    const candidates = S.items.filter(
      (i) =>
        i.instanceId &&
        i.instanceId !== myId &&
        i.instanceId !== l.sourceId &&
        !sharedIds.includes(i.instanceId) &&
        isButtonLinkSource(i)
    );
    sharedAddSel.innerHTML =
      candidates
        .map((i) => `<option value="${escHtml(i.instanceId!)}">${escHtml(itemLabel(i))}</option>`)
        .join("") || '<option value="">— 场景中没有其他开关 —</option>';
  };
  refreshShared();
  host.querySelector("#blm-shared-btn")?.addEventListener("click", () => {
    const id = sharedAddSel?.value ?? "";
    if (!id) return;
    const l = link();
    if (!l || l.groupNames.length === 0) {
      setStatus("请先绑定动画组，再添加共轭按钮", false);
      return;
    }
    pushHistory();
    if (l.pairId) {
      const p = partnerOf(l);
      l.pairId = undefined;
      l.pairStartsUp = undefined;
      if (p) {
        p.pairId = undefined;
        p.pairStartsUp = undefined;
      }
    }
    l.sharedSourceIds = [...(l.sharedSourceIds ?? []), id];
    const it = S.items.find((i) => i.instanceId === id);
    setStatus(`已添加共轭按钮「${it ? itemLabel(it) : id}」（同时开/关，任一按压等同，写回后生效）`);
    refreshShared();
    opts.rerender();
  });

  host.querySelector("#blm-add")?.addEventListener("click", () => {
    const name = addSel.value;
    if (!name) return;
    const binder = linkBindingGroup(name);
    if (binder) {
      // 互锁共享环：partner 绑定的组允许本侧同绑（同组交替分发）。
      const cur = linkOfSource(myId);
      const partnerShared = !!(cur?.pairId && binder.pairId && cur.pairId === binder.pairId);
      if (!partnerShared) {
        setStatus("该动画组已被其他按钮绑定", false);
        return;
      }
    }
    pushHistory();
    const l = ensureLink(myId);
    if (partnerOf(l) && l.groupNames.length >= PAIR_GROUP_LIMIT) {
      setStatus(`互锁模式每个按钮最多绑定 ${PAIR_GROUP_LIMIT} 个动画组`, false);
      return;
    }
    l.groupNames.push(name);
    const g = S.animControls.find((gr) => gr.displayName === name);
    if (g) g.triggerMode = "button";
    if (g && g.loop) {
      setStatus(`已绑定「${name}」（写回后生效）⚠ 该组循环执行不会结束，开启锁定时按钮将无法再按`, true);
    } else {
      setStatus(`已绑定动画组「${name}」（写回后生效）`);
    }
    refresh();
    refreshAddSel();
    draw();
  });

  host.querySelector("#blm-new-group")?.addEventListener("click", () => {
    const group = createSwitchAnimGroup(item);
    pushHistory();
    S.animControls.push(group);
    ensureLink(myId).groupNames.push(group.displayName);
    setStatus(`已创建开关动画组「${group.displayName}」，正在打开单组编排…（写回后生效）`);
    opts.onEditGroup(group.id);
  });

  const lockEl = host.querySelector<HTMLInputElement>("#blm-lock");
  lockEl?.addEventListener("change", () => {
    pushHistory();
    ensureLink(myId).lockUntilFinished = lockEl.checked;
    setStatus(`已${lockEl.checked ? "开启" : "关闭"}运行期锁定（写回后生效）`);
  });

  const simulEl = host.querySelector<HTMLInputElement>("#blm-simul");
  simulEl?.addEventListener("change", () => {
    const l = linkOfSource(myId);
    if (!l || l.groupNames.length === 0) {
      setStatus("请先绑定动画组，再切换同按模式", false);
      simulEl.checked = false;
      return;
    }
    if (l.groupNames.length < 2) {
      setStatus("只绑定 1 组时同按与整组等价，无需开启", false);
    }
    pushHistory();
    l.simultaneous = simulEl.checked;
    setStatus(
      simulEl.checked
        ? "已开启同按模式：一次按压同时启动全部组（写回后生效）"
        : "已关闭同按模式：恢复逐组轮转（写回后生效）"
    );
    opts.rerender();
  });

  const modeEl = host.querySelector<HTMLSelectElement>("#blm-mode");
  modeEl?.addEventListener("change", () => {
    pushHistory();
    ensureLink(myId).sequenceMode = modeEl.value === "pingpong" ? "pingpong" : "loop";
    setStatus(`按钮动画序列已切换为${modeEl.value === "pingpong" ? "往返" : "循环"}模式（写回后生效）`);
  });

  const pairSel = host.querySelector<HTMLSelectElement>("#blm-pair");
  const startupEl = host.querySelector<HTMLInputElement>("#blm-startup");
  const pairHintEl = host.querySelector<HTMLElement>("#blm-pair-hint");
  pairSel?.addEventListener("change", () => {
    pushHistory();
    const l = ensureLink(myId);
    const old = partnerOf(l);
    if (old) {
      old.pairId = undefined;
      old.pairStartsUp = undefined;
    }
    const pid = pairSel.value;
    if (!pid) {
      l.pairId = undefined;
      l.pairStartsUp = undefined;
      setStatus("已解除互锁配对（写回后生效）");
    } else {
      const shared = uuid();
      const other = ensureLink(pid);
      l.sharedSourceIds = undefined;
      other.sharedSourceIds = undefined;
      l.pairId = shared;
      other.pairId = shared;
      l.pairStartsUp = startupEl?.checked ?? true;
      other.pairStartsUp = !l.pairStartsUp;
      const otherItem = S.items.find((i) => i.instanceId === pid);
      const meItem = S.items.find((i) => i.instanceId === myId);
      if (meItem && otherItem) applyInterlockSwitchVisuals(meItem, otherItem, l.pairStartsUp);
      draw(); // 初始关闭侧的画布红底/「关」角标即时刷新
      setStatus(`已与「${otherItem ? itemLabel(otherItem) : pid}」结为互锁按钮（写回后生效）`);
    }
    refreshAddSel();
    if (pairHintEl) pairHintEl.textContent = pairHintText(partnerOf(l));
    if (startupEl) {
      startupEl.disabled = !l.pairId;
      startupEl.checked = !!l.pairStartsUp;
    }
  });
  startupEl?.addEventListener("change", () => {
    const l = link();
    if (!l || !l.pairId) return;
    pushHistory();
    l.pairStartsUp = startupEl.checked;
    const other = partnerOf(l);
    if (other) other.pairStartsUp = !startupEl.checked;
    const meItem = S.items.find((i) => i.instanceId === myId);
    const otherItem = other ? S.items.find((i) => i.instanceId === other.sourceId) : undefined;
    if (meItem && otherItem) applyInterlockSwitchVisuals(meItem, otherItem, startupEl.checked);
    draw(); // 初始关闭侧的画布红底/「关」角标即时刷新
    setStatus(`已设为初始${startupEl.checked ? "抬起（可按）" : "按下（锁定）"}（写回后生效）`);
  });
}

// ---------------------------------------------------------------- 右键菜单 · 共轭/互锁

export type ButtonPartnerMode = "none" | "conjugate" | "interlock";

/** 当前开关与其它按钮的共轭/互锁关系（无动画组时仍可能为 none）。 */
export function buttonPartnerMode(item: EditorItem): ButtonPartnerMode {
  const id = item.instanceId ?? "";
  if (!id) return "none";
  const link = linkOfSource(id);
  if (!link) return "none";
  if (link.pairId && partnerOf(link)) return "interlock";
  if ((link.sharedSourceIds ?? []).length > 0 || link.sourceId !== id) return "conjugate";
  return "none";
}

export function buttonPartnerId(item: EditorItem): string {
  const id = item.instanceId ?? "";
  const mode = buttonPartnerMode(item);
  if (mode === "none") return "";
  const link = linkOfSource(id);
  if (!link) return "";
  if (mode === "conjugate") {
    return link.sourceId === id ? (link.sharedSourceIds?.[0] ?? "") : link.sourceId;
  }
  const mine = S.buttonLinks.find((l) => l.sourceId === id && l.pairId);
  return mine ? (partnerOf(mine)?.sourceId ?? "") : "";
}

export function clearButtonPartnerBinding(instanceId: string): void {
  if (!instanceId) return;
  const mine = S.buttonLinks.find((l) => l.sourceId === instanceId);
  if (mine?.pairId) {
    const other = partnerOf(mine);
    if (other) {
      other.pairId = undefined;
      other.pairStartsUp = undefined;
    }
    mine.pairId = undefined;
    mine.pairStartsUp = undefined;
  }
  if (mine?.sharedSourceIds?.length) mine.sharedSourceIds = undefined;
  for (const l of S.buttonLinks) {
    if ((l.sharedSourceIds ?? []).includes(instanceId)) {
      l.sharedSourceIds = l.sharedSourceIds!.filter((id) => id !== instanceId);
    }
  }
}

/** 应用共轭/互锁配对（会清除双方旧配对；不创建占位动画组）。 */
export function applyButtonPartnerBinding(
  item: EditorItem,
  mode: ButtonPartnerMode,
  partnerId: string,
  pairStartsUp: boolean
): void {
  const myId = item.instanceId ?? "";
  if (!myId) return;
  clearButtonPartnerBinding(myId);
  if (partnerId) clearButtonPartnerBinding(partnerId);
  if (mode === "none" || !partnerId) return;

  const partner = S.items.find((i) => i.instanceId === partnerId);
  if (!partner || !isButtonLinkSource(partner)) return;

  if (mode === "conjugate") {
    const l = ensureLink(myId);
    l.sharedSourceIds = [partnerId];
    l.pairId = undefined;
    l.pairStartsUp = undefined;
    const other = ensureLink(partnerId);
    other.pairId = undefined;
    other.pairStartsUp = undefined;
    other.sharedSourceIds = undefined;
    for (const sw of [item, partner]) {
      if (!sw.switchStub) sw.switchStub = {};
      sw.switchStub.startEnabled = true;
    }
    return;
  }

  const pairId = uuid();
  const linkA = ensureLink(myId);
  const linkB = ensureLink(partnerId);
  linkA.sharedSourceIds = undefined;
  linkB.sharedSourceIds = undefined;
  linkA.pairId = pairId;
  linkB.pairId = pairId;
  linkA.pairStartsUp = pairStartsUp;
  linkB.pairStartsUp = !pairStartsUp;
  applyInterlockSwitchVisuals(item, partner, pairStartsUp);
}

/** 互锁初始外观：一侧抬起（可按）、另一侧按下（锁定）。 */
export function applyInterlockSwitchVisuals(
  swA: EditorItem,
  swB: EditorItem,
  aStartsUp: boolean
): void {
  if (!swA.switchStub) swA.switchStub = {};
  if (!swB.switchStub) swB.switchStub = {};
  swA.switchStub.startEnabled = aStartsUp;
  swB.switchStub.startEnabled = !aStartsUp;
}

function partnerCandidateOptions(myId: string, selectedId: string): string {
  const opts = ['<option value="">— 选择按钮 —</option>'];
  for (const i of S.items) {
    if (!i.instanceId || i.instanceId === myId || !isButtonLinkSource(i)) continue;
    const sel = i.instanceId === selectedId ? "selected" : "";
    opts.push(`<option value="${escHtml(i.instanceId)}" ${sel}>${escHtml(itemLabel(i))}</option>`);
  }
  return opts.join("");
}

/** 右键菜单「特殊按钮」Tab：共轭 / 互锁配对。 */
export function buttonPartnerCtxHtml(item: EditorItem): string {
  const mode = buttonPartnerMode(item);
  const pid = buttonPartnerId(item);
  const link = linkOfSource(item.instanceId ?? "");
  const startsUp = link?.pairStartsUp !== false;
  const showPartner = mode !== "none";
  const hintText =
    mode === "conjugate"
      ? "仅配置按钮配对；动画在「事件」页编排。场景中以青色双箭头连线。"
      : mode === "interlock"
        ? "仅配置互锁配对；各侧动画在「事件」页单独配置。场景中以琥珀色双箭头连线。"
        : "选择模式并指定配对按钮。";
  return `<div class="ctx-stub-row ctx-bl-mode-row">
      <select id="ctx-bl-mode" class="ctx-input" title="共轭=同时开/关；互锁=轮流可按、各绑独立动画组">
        <option value="none" ${mode === "none" ? "selected" : ""}>无配对</option>
        <option value="conjugate" ${mode === "conjugate" ? "selected" : ""}>共轭（同时开/关）</option>
        <option value="interlock" ${mode === "interlock" ? "selected" : ""}>互锁（轮流）</option>
      </select>
    </div>
    <div id="ctx-bl-partner-wrap" class="ctx-bl-partner-wrap" style="display:${showPartner ? "block" : "none"}">
      <label class="ctx-stub-row">配对按钮
        <select id="ctx-bl-partner" class="ctx-input">${partnerCandidateOptions(item.instanceId ?? "", pid)}</select>
      </label>
      <label class="ctx-stub-row ctx-bl-interlock-only" style="display:${mode === "interlock" ? "flex" : "none"}">
        <input type="checkbox" id="ctx-bl-startup" ${startsUp ? "checked" : ""}/> 初始抬起（可按）
      </label>
      <p class="ctx-stub-hint" id="ctx-bl-hint">${hintText}</p>
    </div>`;
}

/** 右键菜单「事件」Tab：动画 / 事件组摘要 + 编排入口。 */
export function buttonEventsOrchestrateCtxHtml(item: EditorItem, eventHint?: string): string {
  const emptyHint =
    '<p class="ctx-stub-hint">未配置事件组；按压后按「机器 → 事件组 → 动画组」顺序执行，点下方按钮打开编排台。</p>';
  return `<p class="ctx-stub-hint">🎬 ${escHtml(buttonLinkSummaryText(item))}</p>
    ${eventHint ? `<p class="ctx-stub-hint">${eventHint}</p>` : emptyHint}
    <button type="button" class="ctx-btn ctx-btn-block" id="ctx-trig-config">🎛 打开触发编排…</button>`;
}

/** 右键菜单：共轭/互锁配置 + 动画摘要 + 编排入口（未分 Tab 时整段使用）。 */
export function buttonLinkCtxHtml(item: EditorItem, eventHint?: string): string {
  return `<div class="ctx-stub-block">
    <div class="ctx-stub-title">按钮联动</div>
    ${buttonPartnerCtxHtml(item)}
    ${buttonEventsOrchestrateCtxHtml(item, eventHint)}
  </div>`;
}

/** 接线右键菜单中的共轭/互锁控件。 */
export function wireButtonLinkCtx(item: EditorItem): void {
  const myId = item.instanceId ?? "";
  const modeSel = document.getElementById("ctx-bl-mode") as HTMLSelectElement | null;
  const partnerWrap = document.getElementById("ctx-bl-partner-wrap");
  const partnerSel = document.getElementById("ctx-bl-partner") as HTMLSelectElement | null;
  const startupRow = document.querySelector<HTMLElement>(".ctx-bl-interlock-only");
  const startupEl = document.getElementById("ctx-bl-startup") as HTMLInputElement | null;

  const refreshUi = () => {
    const mode = buttonPartnerMode(item);
    if (modeSel) modeSel.value = mode;
    const pid = buttonPartnerId(item);
    if (partnerSel) partnerSel.innerHTML = partnerCandidateOptions(myId, pid);
    if (partnerWrap) partnerWrap.style.display = mode === "none" ? "none" : "block";
    if (startupRow) startupRow.style.display = mode === "interlock" ? "flex" : "none";
    if (startupEl && mode === "interlock") {
      const l = S.buttonLinks.find((x) => x.sourceId === myId);
      startupEl.checked = l?.pairStartsUp !== false;
    }
    const hint = document.getElementById("ctx-bl-hint");
    if (hint) {
      hint.textContent =
        mode === "conjugate"
          ? "仅配置按钮配对；动画在「事件」页编排。场景中以青色双箭头连线。"
          : mode === "interlock"
            ? "仅配置互锁配对；各侧动画在「事件」页单独配置。场景中以琥珀色双箭头连线。"
            : "选择模式并指定配对按钮。";
    }
  };

  const apply = () => {
    const mode = (modeSel?.value ?? "none") as ButtonPartnerMode;
    const pid = partnerSel?.value ?? "";
    if (mode !== "none" && !pid) {
      setStatus("请选择配对按钮", false);
      refreshUi();
      return;
    }
    pushHistory();
    applyButtonPartnerBinding(item, mode, pid, startupEl?.checked ?? true);
    S.dirty = true;
    const partner = S.items.find((i) => i.instanceId === pid);
    if (mode === "none") setStatus("已解除按钮配对（写回后生效）");
    else if (mode === "conjugate")
      setStatus(`已与「${partner ? itemLabel(partner) : pid}」设为共轭（写回后生效）`);
    else setStatus(`已与「${partner ? itemLabel(partner) : pid}」设为互锁（写回后生效）`);
    refreshUi();
    draw();
  };

  modeSel?.addEventListener("change", apply);
  partnerSel?.addEventListener("change", apply);
  startupEl?.addEventListener("change", () => {
    if (buttonPartnerMode(item) !== "interlock") return;
    const pid = partnerSel?.value ?? buttonPartnerId(item);
    if (!pid) return;
    pushHistory();
    applyButtonPartnerBinding(item, "interlock", pid, startupEl.checked);
    setStatus(`已设为初始${startupEl.checked ? "抬起（可按）" : "按下（锁定）"}（写回后生效）`);
    S.dirty = true;
    draw();
  });
}
