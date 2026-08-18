import {
  S,
  EditorItem
} from "./state";
import type { ButtonLink } from "../types";
import { stubKindOf } from "./stubControls";
import { itemLabel } from "./labels";
import { escHtml } from "./coords";
import { closeModal, openModal } from "../modals";
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
    id: crypto.randomUUID(),
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

/** 某移动组是否已被任一联动绑定（一个组至多属于一条联动）。 */
export function linkBindingGroup(groupName: string): ButtonLink | undefined {
  return S.buttonLinks.find((l) => l.groupNames.includes(groupName));
}

/** 共轭模式下单个按钮最多绑定的移动组数（两组同时启动、全部完成后翻转）。 */
export const PAIR_GROUP_LIMIT = 2;

/**
 * 清理失效联动：源物品被删、移动组被删/改名、配对另一方缺失。
 * 移动组以 displayName 引用（跨保存稳定），改名由 moveControl 的改名处同步。
 */
export function cleanOrphanedButtonLinks(): void {
  const itemIds = new Set(S.items.map((i) => i.instanceId).filter(Boolean));
  const groupNames = new Set(S.moveControls.map((g) => g.displayName));
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

/** 移动组改名时同步联动里的引用（displayName 是引用键）。 */
export function renameGroupInButtonLinks(oldName: string, newName: string): void {
  if (!oldName || oldName === newName) return;
  for (const l of S.buttonLinks) {
    l.groupNames = l.groupNames.map((n) => (n === oldName ? newName : n));
  }
}

function groupLabel(name: string): string {
  const g = S.moveControls.find((gr) => gr.displayName === name);
  const members = g ? g.itemInstanceIds.length + g.floorInstanceIds.length + g.objectInstanceIds.length : 0;
  return g ? `${escHtml(name)}（${members} 成员/${g.events.length} 事件）` : escHtml(name);
}

// ---------------------------------------------------------------- UI

/** 右键菜单中的联动摘要 + 「配置…」按钮（详细配置在独立弹窗中进行）。 */
export function buttonLinkSummaryHtml(item: EditorItem): string {
  const link = linkOfSource(item.instanceId ?? "");
  const n = link?.groupNames.length ?? 0;
  const partner = link ? partnerOf(link) : undefined;
  const summary =
    !link || n === 0
      ? "— 未绑定移动组 —"
      : `已绑 ${n} 组${link.lockUntilFinished !== false ? " · 完成后才可再按" : ""}${partner ? " · 共轭配对" : ""}`;
  return `<div class="ctx-stub-title" style="margin-top:6px">联动移动组（按顺序触发）</div>
    <label class="ctx-stub-row"><span class="ctx-input" style="opacity:${n > 0 ? 1 : 0.6}">${escHtml(summary)}</span>
    <button type="button" class="ctx-btn" id="ctx-bl-config">配置…</button></label>`;
}

function pairHintText(partner: ButtonLink | undefined): string {
  if (!partner)
    return "不配对时为顺序触发：每次按压启动下一组（最后一组后循环回第一组）。";
  const partnerItem = S.items.find((i) => i.instanceId === partner.sourceId);
  return `共轭模式：与「${partnerItem ? itemLabel(partnerItem) : "?"}」互斥，每个按钮需各绑 ${PAIR_GROUP_LIMIT} 个移动组；按下时两组同时启动，全部完成后对方抬起。`;
}

function buttonLinkModalBodyHtml(item: EditorItem): string {
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

  return `<p class="modal-hint">绑定后每次按压按顺序触发下一组（循环）；绑定的组不再自动执行，启动/结束触发器由联动自动管理。</p>
    <div class="blm-sec">联动移动组（按顺序触发）</div>
    <div id="blm-groups"></div>
    <div class="blm-addrow"><select id="blm-groupadd" class="modal-select"></select>
      <button type="button" class="modal-btn" id="blm-add">添加</button></div>
    <label class="modal-check"><input type="checkbox" id="blm-lock" ${!link || link.lockUntilFinished !== false ? "checked" : ""}/> 移动组完成后才可再按（运行期忽略按压）</label>
    <div class="blm-sec">共轭按钮（一对一）</div>
    <label class="modal-field">配对按钮 <select id="blm-pair" class="modal-select">${pairOpts}</select></label>
    <label class="modal-check"><input type="checkbox" id="blm-startup" ${link?.pairStartsUp ? "checked" : ""} ${partner ? "" : "disabled"}/> 初始为抬起（可按）状态</label>
    <p class="modal-hint" id="blm-pair-hint">${escHtml(pairHintText(partner))}</p>`;
}

export function openButtonLinkModal(item: EditorItem): void {
  const myId = item.instanceId ?? "";
  if (!myId) return;
  const kindName = stubKindOf(item) === "PressureSwitch" ? "压力开关" : "按钮";
  openModal(
    `${kindName}联动配置 · ${itemLabel(item)}`,
    buttonLinkModalBodyHtml(item),
    `<button type="button" class="modal-btn primary" data-close>关闭</button>`
  );
  document.querySelector("[data-close]")?.addEventListener("click", closeModal);
  wireButtonLinkModal(item);
}

function wireButtonLinkModal(item: EditorItem): void {
  const myId = item.instanceId ?? "";
  if (!myId) return;

  const groupsEl = document.getElementById("blm-groups");
  const addSel = document.getElementById("blm-groupadd") as HTMLSelectElement | null;
  if (!groupsEl || !addSel) return;

  const link = () => linkOfSource(myId);

  const refresh = () => {
    const l = link();
    if (!l || l.groupNames.length === 0) {
      groupsEl.innerHTML = '<p class="modal-hint">未绑定移动组</p>';
    } else {
      groupsEl.innerHTML = l.groupNames
        .map(
          (n, i) => `<div class="blm-row"><span class="blm-row-label">${i + 1}. ${groupLabel(n)}</span>
            <button type="button" class="modal-btn blm-mini" data-bl-up="${i}" ${i === 0 ? "disabled" : ""}>↑</button>
            <button type="button" class="modal-btn blm-mini" data-bl-down="${i}" ${i === l.groupNames.length - 1 ? "disabled" : ""}>↓</button>
            <button type="button" class="modal-btn blm-mini" data-bl-del="${i}">移除</button></div>`
        )
        .join("");
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
          setStatus("已移除联动移动组（写回后生效）");
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
    const opts = S.moveControls
      .filter((g) => !mine.has(g.displayName))
      .map((g) => {
        const boundBy = linkBindingGroup(g.displayName);
        const disabled = boundBy || limitReached ? "disabled" : "";
        const suffix = boundBy ? "（已被其他按钮绑定）" : limitReached ? `（共轭模式最多 ${PAIR_GROUP_LIMIT} 组）` : "";
        return `<option value="${escHtml(g.displayName)}" ${disabled}>${escHtml(g.displayName)}${suffix}</option>`;
      })
      .join("");
    addSel.innerHTML = opts || '<option value="">— 无可绑定的移动组 —</option>';
  };

  refresh();
  refreshAddSel();

  document.getElementById("blm-add")?.addEventListener("click", () => {
    const name = addSel.value;
    if (!name) return;
    if (linkBindingGroup(name)) {
      setStatus("该移动组已被其他按钮绑定", false);
      return;
    }
    pushHistory();
    const l = ensureLink(myId);
    if (partnerOf(l) && l.groupNames.length >= PAIR_GROUP_LIMIT) {
      setStatus(`共轭模式每个按钮最多绑定 ${PAIR_GROUP_LIMIT} 个移动组`, false);
      return;
    }
    l.groupNames.push(name);
    const g = S.moveControls.find((gr) => gr.displayName === name);
    if (g && g.loop) {
      setStatus(`已绑定「${name}」（写回后生效）⚠ 该组循环执行不会结束，开启锁定时按钮将无法再按`, true);
    } else {
      setStatus(`已绑定移动组「${name}」（写回后生效）`);
    }
    refresh();
    refreshAddSel();
    draw();
  });

  const lockEl = document.getElementById("blm-lock") as HTMLInputElement | null;
  lockEl?.addEventListener("change", () => {
    pushHistory();
    ensureLink(myId).lockUntilFinished = lockEl.checked;
    setStatus(`已${lockEl.checked ? "开启" : "关闭"}运行期锁定（写回后生效）`);
  });

  const pairSel = document.getElementById("blm-pair") as HTMLSelectElement | null;
  const startupEl = document.getElementById("blm-startup") as HTMLInputElement | null;
  const pairHintEl = document.getElementById("blm-pair-hint");
  pairSel?.addEventListener("change", () => {
    pushHistory();
    const l = ensureLink(myId);
    // 解除旧配对
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
      const shared = crypto.randomUUID();
      const other = ensureLink(pid);
      l.pairId = shared;
      other.pairId = shared;
      // 默认本按钮抬起、对方按下
      l.pairStartsUp = startupEl?.checked ?? true;
      other.pairStartsUp = !l.pairStartsUp;
      const otherItem = S.items.find((i) => i.instanceId === pid);
      setStatus(`已与「${otherItem ? itemLabel(otherItem) : pid}」结为共轭按钮（写回后生效）`);
    }
    refreshAddSel();
    if (pairHintEl) pairHintEl.textContent = pairHintText(partnerOf(l));
    // 更新初始状态复选框可用性
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
