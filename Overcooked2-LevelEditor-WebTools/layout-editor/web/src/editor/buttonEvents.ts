import {
  S,
  EditorItem
} from "./state";
import type { ButtonEventLink, ButtonEventGroup } from "../types";
import { isButtonLinkSource } from "./buttonLinks";
import { stubKindOf } from "./stubControls";
import { itemLabel } from "./labels";
import { uuid, escHtml } from "./coords";
import { closeModal, openModal } from "../modals";
import { pushHistory } from "./historyOps";
import { setStatus } from "./status";
import { draw } from "./render";
import { setSelection } from "./selection";
import { ensureItemVisible } from "./panels";

// ---------------------------------------------------------------- data

/** 按钮事件组的触发源：Switch 按钮 / PressureSwitch 压力开关（与动画组联动同口径）。 */
export function isButtonEventSource(it: EditorItem): boolean {
  return isButtonLinkSource(it);
}

export function eventLinkOfSource(sourceId: string): ButtonEventLink | undefined {
  return S.buttonEvents.find((l) => l.sourceId === sourceId);
}

export function ensureEventLink(sourceId: string): ButtonEventLink {
  const existing = eventLinkOfSource(sourceId);
  if (existing) return existing;
  const link: ButtonEventLink = {
    id: uuid(),
    sourceId,
    groups: [],
  };
  S.buttonEvents.push(link);
  return link;
}

/** 清理失效事件组：源物品被删、目标物品被删、组内事件清空、目标不再是联动目标。
 *  同时归一事件触发名 = 联动的共享触发名（运行时监听字段只认这个名字；
 *  历史文档里自造的 switch_{prefabId}_{N} 会被自动修复）。 */
export function cleanOrphanedButtonEvents(): void {
  const itemIds = new Set(S.items.map((i) => i.instanceId).filter(Boolean));
  for (const l of S.buttonEvents) {
    const linked = new Set(
      S.switchLinks.filter((sl) => sl.switchId === l.sourceId).map((sl) => sl.targetId)
    );
    for (const g of l.groups) {
      g.events = g.events.filter((e) => itemIds.has(e.targetId) && linked.has(e.targetId));
      for (const e of g.events) {
        const lt = S.switchLinks.find(
          (sl) => sl.switchId === l.sourceId && sl.targetId === e.targetId
        )?.trigger;
        if (lt) e.trigger = lt;
      }
    }
    l.groups = l.groups.filter((g) => g.events.length > 0);
  }
  S.buttonEvents = S.buttonEvents.filter(
    (l) => itemIds.has(l.sourceId) && l.groups.length > 0
  );
}

/** 目标的联动共享触发名（未联动返回 undefined）。 */
export function linkTriggerOf(sourceId: string, targetId?: string): string | undefined {
  return S.switchLinks.find(
    (l) => l.switchId === sourceId && (targetId === undefined || l.targetId === targetId)
  )?.trigger;
}

/** 事件触发名：固定取联动的共享触发名（大炮为 Launch）。监听字段
 *  （m_workTrigger/m_switchTrigger）是每台机器单值，由联动路径写入，
 *  自造其它名字只会无人监听。 */
function defaultEventTrigger(sourceId: string, target: EditorItem): string {
  return linkTriggerOf(sourceId, target.instanceId) ?? "Switch";
}

// ---------------------------------------------------------------- 右侧面板

function summary(link?: ButtonEventLink): string {
  const groups = link?.groups ?? [];
  if (groups.length === 0) return "— 未配置事件组 —";
  const total = groups.reduce((sum, g) => sum + g.events.length, 0);
  const gated = groups.reduce(
    (sum, g) => sum + g.events.filter((e) => e.doneTrigger && e.doneTrigger.trim()).length,
    0
  );
  return `已绑 ${groups.length} 组 / ${total} 条事件${gated > 0 ? `（${gated} 条带完成触发器）` : ""} · 按顺序广播，组内完成后才可再按`;
}

/** 右侧面板「按钮事件组」Tab。 */
export function renderButtonEventPanel(body: HTMLElement): void {
  const sources = S.items.filter((i) => i.instanceId && isButtonEventSource(i));
  const countEl = document.getElementById("bevents-count");
  if (countEl) countEl.textContent = sources.length > 0 ? `(${sources.length})` : "";

  const parts: string[] = [];
  parts.push(
    `<div class="muted" style="padding:8px 10px;">一个按钮可绑定多个事件组：每次按压按顺序向下一组广播全部事件（循环）；组内全部事件完成（完成触发器）后才可再按。事件目标仅限开关右键菜单「联动目标」中的物件。配置在按钮右键菜单或下方列表中打开。</div>`
  );
  for (const src of sources) {
    const link = eventLinkOfSource(src.instanceId ?? "");
    const n = link?.groups.length ?? 0;
    parts.push(
      `<div class="scene-item-row" data-bevsrc="${escHtml(src.instanceId ?? "")}">` +
        `<span class="zh">${escHtml(itemLabel(src))}</span> ` +
        `<span class="id">${escHtml(summary(n > 0 ? link : undefined))}</span>` +
        `<button type="button" class="ctx-btn" style="margin-left:auto" data-bevcfg="${escHtml(src.instanceId ?? "")}">配置…</button>` +
        `</div>`
    );
  }
  if (sources.length === 0) {
    parts.push(`<div class="muted" style="padding:10px;">场景中暂无开关/压力开关。先放置一个按钮（核心层 · 机制 → 开关）。</div>`);
  }
  body.innerHTML = parts.join("");

  body.querySelectorAll<HTMLButtonElement>("[data-bevcfg]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const src = S.items.find((i) => i.instanceId === btn.dataset.bevcfg);
      if (src) openButtonEventModal(src);
    });
  });
  body.querySelectorAll<HTMLElement>("[data-bevsrc]").forEach((row) => {
    row.addEventListener("click", () => {
      const src = S.items.find((i) => i.instanceId === row.dataset.bevsrc);
      if (!src) return;
      setSelection([src._editorKey]);
      ensureItemVisible(src);
      draw();
    });
  });
}

/** 事件组 Tab 激活时即时重绘（组内容编辑后调用）。 */
export function refreshButtonEventPanelIfActive(): void {
  if (S.activeRightTab !== "bevents") return;
  const body = document.getElementById("scene-items-body");
  if (body) renderButtonEventPanel(body);
}

// ---------------------------------------------------------------- 右键菜单

/** 右键菜单中的事件组摘要 + 「配置…」入口。 */
export function buttonEventSummaryHtml(item: EditorItem): string {
  const link = eventLinkOfSource(item.instanceId ?? "");
  const n = link?.groups.length ?? 0;
  return `<div class="ctx-stub-title" style="margin-top:6px">联动事件组（按顺序广播）</div>
    <label class="ctx-stub-row"><span class="ctx-input" style="opacity:${n > 0 ? 1 : 0.6}">${escHtml(summary(n > 0 ? link : undefined))}</span>
    <button type="button" class="ctx-btn" id="ctx-bev-config">配置…</button></label>`;
}

// ---------------------------------------------------------------- 配置弹窗

/** 事件目标下拉：仅限本开关的联动目标（运行时监听字段只由联动路径接线，
 *  未联动的目标收了消息也不会响应）。 */
function targetOptionsHtml(excludeIds: Set<string>, allowedTargets: Set<string>): string {
  return S.items
    .filter(
      (i) =>
        i.instanceId &&
        !excludeIds.has(i.instanceId) &&
        allowedTargets.has(i.instanceId) &&
        stubKindOf(i) !== "Player"
    )
    .map((i) => `<option value="${escHtml(i.instanceId)}">${escHtml(itemLabel(i))}</option>`)
    .join("");
}

function modalBodyHtml(link: ButtonEventLink | undefined): string {
  const n = link?.groups.length ?? 0;
  return `<p class="modal-hint">每次按压按顺序向下一事件组广播全部事件（最后一组后循环回第一组）。事件目标仅限开关右键菜单「联动目标」里的物件；触发消息固定取联动的共享触发名（在开关右键菜单修改）。事件可配「完成触发器」：目标完成事件时广播该触发名，组内全部完成后按钮才可再按；未配完成触发器的事件视为立即完成。</p>
    <div class="blm-sec">事件组（共 ${n} 组，按下时顺序切换）</div>
    <div id="bev-groups"></div>
    <div class="blm-addrow"><button type="button" class="modal-btn" id="bev-addgroup">＋ 添加事件组</button></div>`;
}

export function openButtonEventModal(item: EditorItem): void {
  const myId = item.instanceId ?? "";
  if (!myId) return;
  const kindName = stubKindOf(item) === "PressureSwitch" ? "压力开关" : "按钮";
  const link = eventLinkOfSource(myId);
  openModal(
    `${kindName}事件组配置 · ${itemLabel(item)}`,
    modalBodyHtml(link),
    `<button type="button" class="modal-btn primary" data-close>关闭</button>`
  );
  document.querySelector("[data-close]")?.addEventListener("click", closeModal);
  wireButtonEventModal(item);
}

function wireButtonEventModal(item: EditorItem): void {
  const myId = item.instanceId ?? "";
  if (!myId) return;

  const groupsEl = document.getElementById("bev-groups");
  if (!groupsEl) return;

  const link = () => eventLinkOfSource(myId);
  const labels = new Map(S.items.map((i) => [i.instanceId, itemLabel(i)]));
  const labelOf = (id: string) => labels.get(id) ?? id;

  const refresh = () => {
    const l = link();
    const allowedTargets = new Set(
      S.switchLinks.filter((sl) => sl.switchId === myId).map((sl) => sl.targetId)
    );
    if (!l || l.groups.length === 0) {
      groupsEl.innerHTML =
        allowedTargets.size === 0
          ? '<p class="modal-hint">未配置事件组，且本开关还没有联动目标：先在开关右键菜单「联动目标」添加（如断头台/饮料机），再回到这里编排事件。</p>'
          : '<p class="modal-hint">未配置事件组</p>';
    } else {
      groupsEl.innerHTML = l.groups
        .map((g, gi) => {
          const eventRows = g.events
            .map(
              (e, ei) => `<div class="blm-row">
                <span class="blm-row-label">${gi + 1}.${ei + 1} → ${escHtml(labelOf(e.targetId))}</span>
                <span class="modal-input" style="display:inline-flex;align-items:center;min-width:120px" title="触发消息 = 联动的共享触发名（在开关右键菜单修改）">${escHtml(e.trigger)}</span>
                <input class="modal-input bev-done" data-bev-g="${gi}" data-bev-i="${ei}" value="${escHtml(e.doneTrigger ?? "")}" placeholder="完成触发器（可选）" title="目标完成事件时广播的触发名"/>
                <button type="button" class="modal-btn blm-mini" data-bev-del="${gi}:${ei}">移除</button>
              </div>`
            )
            .join("");
          const opts = targetOptionsHtml(new Set(g.events.map((e) => e.targetId)), allowedTargets);
          return `<div class="bev-group">
            <div class="blm-sec">事件组 ${gi + 1}（${g.events.length} 条事件）
              <button type="button" class="modal-btn blm-mini" data-bevg-up="${gi}" ${gi === 0 ? "disabled" : ""}>↑</button>
              <button type="button" class="modal-btn blm-mini" data-bevg-down="${gi}" ${gi === l.groups.length - 1 ? "disabled" : ""}>↓</button>
              <button type="button" class="modal-btn blm-mini" data-bevg-del="${gi}">删除组</button>
            </div>
            ${eventRows || '<p class="modal-hint">空事件组（按下时直接跳过）</p>'}
            <div class="blm-addrow"><select id="bev-target-${gi}" class="modal-select">${opts || '<option value="">— 无联动目标可添加（先在开关右键菜单添加联动） —</option>'}</select>
              <button type="button" class="modal-btn" data-bev-add="${gi}">＋ 添加事件</button></div>
          </div>`;
        })
        .join("");
    }

    groupsEl.querySelectorAll<HTMLInputElement>(".bev-done").forEach((inp) => {
      inp.addEventListener("change", () => {
        const l = link();
        const g = l?.groups[parseInt(inp.dataset.bevG!, 10)];
        const e = g?.events[parseInt(inp.dataset.bevI!, 10)];
        if (!l || !g || !e) return;
        pushHistory();
        const v = inp.value.trim();
        if (v) e.doneTrigger = v;
        else delete e.doneTrigger;
        setStatus(v ? `完成触发器已设为 ${v}（写回后生效）` : "已清除完成触发器（该事件立即视为完成）");
      });
    });
    groupsEl.querySelectorAll<HTMLButtonElement>("[data-bev-del]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const [gi, ei] = btn.dataset.bevDel!.split(":").map((x) => parseInt(x, 10));
        const l = link();
        const g = l?.groups[gi];
        if (!l || !g) return;
        pushHistory();
        g.events.splice(ei, 1);
        if (g.events.length === 0) {
          l.groups.splice(gi, 1);
          if (l.groups.length === 0) S.buttonEvents = S.buttonEvents.filter((x) => x !== l);
        }
        setStatus("已移除事件（写回后生效）");
        refresh();
        refreshButtonEventPanelIfActive();
      });
    });
    groupsEl.querySelectorAll<HTMLButtonElement>("[data-bevg-up]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const gi = parseInt(btn.dataset.bevgUp!, 10);
        const l = link();
        if (!l || gi <= 0) return;
        pushHistory();
        [l.groups[gi - 1], l.groups[gi]] = [l.groups[gi], l.groups[gi - 1]];
        refresh();
      });
    });
    groupsEl.querySelectorAll<HTMLButtonElement>("[data-bevg-down]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const gi = parseInt(btn.dataset.bevgDown!, 10);
        const l = link();
        if (!l || gi >= l.groups.length - 1) return;
        pushHistory();
        [l.groups[gi], l.groups[gi + 1]] = [l.groups[gi + 1], l.groups[gi]];
        refresh();
      });
    });
    groupsEl.querySelectorAll<HTMLButtonElement>("[data-bevg-del]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const gi = parseInt(btn.dataset.bevgDel!, 10);
        const l = link();
        if (!l) return;
        pushHistory();
        l.groups.splice(gi, 1);
        if (l.groups.length === 0) S.buttonEvents = S.buttonEvents.filter((x) => x !== l);
        setStatus("已删除事件组（写回后生效）");
        refresh();
        refreshButtonEventPanelIfActive();
      });
    });
    groupsEl.querySelectorAll<HTMLButtonElement>("[data-bev-add]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const gi = parseInt(btn.dataset.bevAdd!, 10);
        const sel = document.getElementById(`bev-target-${gi}`) as HTMLSelectElement | null;
        const tid = sel?.value ?? "";
        if (!tid) return;
        const target = S.items.find((i) => i.instanceId === tid);
        const l = ensureEventLink(myId);
        const g = l.groups[gi];
        if (!l || !g || !target) return;
        pushHistory();
        g.events.push({ targetId: tid, trigger: defaultEventTrigger(myId, target) });
        setStatus(`已向事件组 ${gi + 1} 添加事件 → ${itemLabel(target)}（写回后生效）`);
        refresh();
        refreshButtonEventPanelIfActive();
      });
    });
  };

  refresh();

  document.getElementById("bev-addgroup")?.addEventListener("click", () => {
    pushHistory();
    const l = ensureEventLink(myId);
    l.groups.push({ id: uuid(), events: [] } as ButtonEventGroup);
    setStatus(`已添加事件组 ${l.groups.length}（写回后生效）`);
    refresh();
    refreshButtonEventPanelIfActive();
  });
}
