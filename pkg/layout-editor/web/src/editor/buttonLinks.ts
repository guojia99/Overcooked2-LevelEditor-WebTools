import {
  S,
  EditorItem
} from "./state";
import type { AnimGroup, ButtonLink } from "../types";
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
  return S.buttonLinks.find((l) => l.sourceId === sourceId);
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

/** 共轭对中的另一方（同 pairId 的另一条 link）。 */
export function partnerOf(link: ButtonLink): ButtonLink | undefined {
  if (!link.pairId) return undefined;
  return S.buttonLinks.find((l) => l !== link && l.pairId === link.pairId);
}

/** 某动画组是否已被任一联动绑定（一个组至多属于一条联动）。 */
export function linkBindingGroup(groupName: string): ButtonLink | undefined {
  return S.buttonLinks.find((l) => l.groupNames.includes(groupName));
}

/** 共轭模式下单个按钮最多绑定的动画组数（两组同时启动、全部完成后翻转）。 */
export const PAIR_GROUP_LIMIT = 2;

/**
 * 清理失效联动：源物品被删、动画组被删/改名、配对另一方缺失。
 * 动画组以 displayName 引用（跨保存稳定），改名由 animControl 的改名处同步。
 */
export function cleanOrphanedButtonLinks(): void {
  const itemIds = new Set(S.items.map((i) => i.instanceId).filter(Boolean));
  const groupNames = new Set(S.animControls.map((g) => g.displayName));
  for (const l of S.buttonLinks) {
    l.groupNames = l.groupNames.filter((n) => groupNames.has(n));
  }
  S.buttonLinks = S.buttonLinks.filter(
    (l) => itemIds.has(l.sourceId) && l.groupNames.length > 0
  );
  // 配对完整性：partner 缺失时解除配对。
  for (const l of S.buttonLinks) {
    if (l.pairId && !partnerOf(l)) {
      l.pairId = undefined;
      l.pairStartsUp = undefined;
    }
  }
}

/** 动画组改名时同步联动里的引用（displayName 是引用键）。 */
export function renameGroupInButtonLinks(oldName: string, newName: string): void {
  if (!oldName || oldName === newName) return;
  for (const l of S.buttonLinks) {
    l.groupNames = l.groupNames.map((n) => (n === oldName ? newName : n));
  }
}

function groupLabel(name: string): string {
  const g = S.animControls.find((gr) => gr.displayName === name);
  const members = g ? g.itemInstanceIds.length + g.floorInstanceIds.length + g.objectInstanceIds.length : 0;
  return g ? `${escHtml(name)}（${members} 成员/${g.events.length} 事件）` : escHtml(name);
}

function createSwitchAnimGroup(source: EditorItem): AnimGroup {
  const x = source._wx;
  const z = source._wz;
  const waypointId = uuid();
  return {
    id: uuid(),
    displayName: `开关动画组 ${S.animControls.length + 1}`,
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
  const n = link?.groupNames.length ?? 0;
  const partner = link ? partnerOf(link) : undefined;
  return !link || n === 0
    ? "— 未绑定动画组 —"
    : `已绑 ${n} 组 · ${link.sequenceMode === "pingpong" ? "往返" : "循环"}${link.lockUntilFinished !== false ? " · 完成后才可再按" : ""}${partner ? " · 共轭配对" : ""}`;
}

function pairHintText(partner: ButtonLink | undefined): string {
  if (!partner)
    return "不配对时每次按压启动下一组；可选择循环或往返模式。";
  const partnerItem = S.items.find((i) => i.instanceId === partner.sourceId);
  return `共轭模式：与「${partnerItem ? itemLabel(partnerItem) : "?"}」互斥，每个按钮需各绑 ${PAIR_GROUP_LIMIT} 个动画组；按下时两组同时启动，全部完成后对方抬起。`;
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
  return `<p class="trig-hint">每次按压进入下一步动画组；动画完成后才进入下一次。启动 / 结束触发器由联动自动管理（无需在动画组里手动设置）。</p>
    <div id="blm-groups" class="trig-list"></div>
     <div class="trig-addrow"><select id="blm-groupadd" class="trig-select"></select>
       <button type="button" class="btn-small" id="blm-add">添加已有组</button>
       <button type="button" class="btn-small primary" id="blm-new-group">＋ 新建并编辑</button></div>
    <label class="trig-check"><input type="checkbox" id="blm-lock" ${!link || link.lockUntilFinished !== false ? "checked" : ""}/> 动画组完成后才可再按（运行期忽略按压）</label>
    <label class="trig-field">播放模式 <select id="blm-mode" class="trig-select">
      <option value="loop" ${mode === "loop" ? "selected" : ""}>循环：A → B → C → A</option>
      <option value="pingpong" ${mode === "pingpong" ? "selected" : ""}>往返：A → B → C → B → A</option>
    </select></label>
    <div class="trig-subhead">共轭按钮（一对一）</div>
    <label class="trig-field">配对按钮 <select id="blm-pair" class="trig-select">${pairOpts}</select></label>
    <label class="trig-check"><input type="checkbox" id="blm-startup" ${link?.pairStartsUp ? "checked" : ""} ${partner ? "" : "disabled"}/> 初始为抬起（可按）状态</label>
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
      groupsEl.innerHTML = l.groupNames
        .map(
          (n, i) => `<div class="trig-step"><span class="trig-step-idx">${i + 1}</span>
            <span class="trig-step-label">${groupLabel(n)}</span>
            <button type="button" class="btn-small" data-bl-edit="${escHtml(n)}" title="编辑该动画组的成员与时间轴">✎ 编辑</button>
            <button type="button" class="btn-small blm-mini" data-bl-up="${i}" ${i === 0 ? "disabled" : ""}>↑</button>
            <button type="button" class="btn-small blm-mini" data-bl-down="${i}" ${i === l.groupNames.length - 1 ? "disabled" : ""}>↓</button>
            <button type="button" class="btn-small blm-mini" data-bl-del="${i}">移除</button></div>`
        )
        .join("");
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
    const opt = S.animControls
      .filter((g) => g.triggerMode === "button")
      .filter((g) => !mine.has(g.displayName))
      .map((g) => {
        const boundBy = linkBindingGroup(g.displayName);
        const disabled = boundBy || limitReached ? "disabled" : "";
        const suffix = boundBy ? "（已被其他按钮绑定）" : limitReached ? `（共轭模式最多 ${PAIR_GROUP_LIMIT} 组）` : "";
        return `<option value="${escHtml(g.displayName)}" ${disabled}>${escHtml(g.displayName)}${suffix}</option>`;
      })
      .join("");
    addSel.innerHTML = opt || '<option value="">— 无可绑定的动画组 —</option>';
  };

  refresh();
  refreshAddSel();

  host.querySelector("#blm-add")?.addEventListener("click", () => {
    const name = addSel.value;
    if (!name) return;
    if (linkBindingGroup(name)) {
      setStatus("该动画组已被其他按钮绑定", false);
      return;
    }
    pushHistory();
    const l = ensureLink(myId);
    if (partnerOf(l) && l.groupNames.length >= PAIR_GROUP_LIMIT) {
      setStatus(`共轭模式每个按钮最多绑定 ${PAIR_GROUP_LIMIT} 个动画组`, false);
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
      setStatus("已解除共轭配对（写回后生效）");
    } else {
      const shared = uuid();
      const other = ensureLink(pid);
      l.pairId = shared;
      other.pairId = shared;
      l.pairStartsUp = startupEl?.checked ?? true;
      other.pairStartsUp = !l.pairStartsUp;
      const otherItem = S.items.find((i) => i.instanceId === pid);
      setStatus(`已与「${otherItem ? itemLabel(otherItem) : pid}」结为共轭按钮（写回后生效）`);
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
    setStatus(`已设为初始${startupEl.checked ? "抬起（可按）" : "按下（锁定）"}（写回后生效）`);
  });
}
