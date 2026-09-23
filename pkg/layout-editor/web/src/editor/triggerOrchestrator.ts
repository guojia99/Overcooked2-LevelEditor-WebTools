import { S, EditorItem } from "./state";
import { escHtml, prefabIdFromPath } from "./coords";
import { itemLabel } from "./labels";
import {
  stubKindOf,
  isSwitchLinkTarget,
  nativeLinkTrigger
} from "./stubControls";
import {
  isButtonLinkSource,
  buttonLinkSummaryText,
  renderButtonLinkSection
} from "./buttonLinks";
import {
  eventLinkOfSource,
  buttonEventSummaryText,
  renderButtonEventSection,
  cleanOrphanedButtonEvents
} from "./buttonEvents";
import { openAnimDock, closeAnimDock } from "./animDock";
import { openAnimGroupEditorById, detachGroupEditorHost } from "./animControl";
import { pushHistory } from "./historyOps";
import { setStatus } from "./status";
import { draw } from "./render";
import { setSelection } from "./selection";
import { ensureItemVisible } from "./panels";

/** 当前编排台正在编排的触发源实例 id（模式 B）。 */
let currentSourceId: string | null = null;

/** 编排台正处于「触发编排」模式（用于面板刷新时判断）。 */
export function isTriggerOrchestratorOpen(): boolean {
  return currentSourceId !== null;
}

/** 清除当前触发源标记（切到单组编排 / 关闭时调用）。 */
export function clearTriggerSource(): void {
  currentSourceId = null;
}

// ---------------------------------------------------------------- 打开 / 关闭

/** 在底部编排台中打开某按钮/开关的「触发编排」（模式 B）。 */
export function openTriggerOrchestrator(item: EditorItem): void {
  const id = item.instanceId ?? "";
  if (!id) return;
  currentSourceId = id;
  const body = openAnimDock();
  detachGroupEditorHost();
  renderOrchestrator(body, item);
}

export function closeTriggerOrchestrator(): void {
  currentSourceId = null;
  closeAnimDock();
}

// ---------------------------------------------------------------- 编排台主体（模式 B）

function renderOrchestrator(body: HTMLElement, item: EditorItem): void {
  const kind = stubKindOf(item);
  const kindName = kind === "PressureSwitch" ? "压力开关" : "按钮 / 开关";
  const showMachines = kind === "Switch";

  body.innerHTML = `
    <div class="trig-head">
      <button type="button" class="btn-small" id="trig-close">✕ 关闭</button>
      <span class="trig-title">🔘 触发编排 · ${escHtml(itemLabel(item))}</span>
      <span class="trig-kind">${kindName}</span>
      <span class="trig-flowhint">按下 → ${showMachines ? "① 机器 → " : ""}② 事件组 → ③ 动画组</span>
    </div>
    <div class="trig-flow">
      ${showMachines ? `<div class="trig-col" id="trig-col-machines">
        <div class="trig-col-head"><span class="trig-badge">①</span> 直连机器目标</div>
        <div class="trig-col-body" id="trig-sec-machines"></div>
      </div>
      <div class="trig-arrow">▶</div>` : ""}
      <div class="trig-col" id="trig-col-events">
        <div class="trig-col-head"><span class="trig-badge">②</span> 事件组序列</div>
        <div class="trig-col-body" id="trig-sec-events"></div>
      </div>
      <div class="trig-arrow">▶</div>
      <div class="trig-col" id="trig-col-anim">
        <div class="trig-col-head"><span class="trig-badge">③</span> 动画组序列</div>
        <div class="trig-col-body" id="trig-sec-anim"></div>
      </div>
    </div>`;

  body.querySelector("#trig-close")?.addEventListener("click", closeTriggerOrchestrator);

  const rerender = () => {
    const it = S.items.find((i) => i.instanceId === currentSourceId);
    if (it) renderOrchestrator(body, it);
    draw();
  };

  if (showMachines) {
    const secMachines = body.querySelector<HTMLElement>("#trig-sec-machines");
    if (secMachines) renderSwitchTargetSection(secMachines, item, rerender);
  }
  const secEvents = body.querySelector<HTMLElement>("#trig-sec-events");
  if (secEvents) renderButtonEventSection(secEvents, item);
  const secAnim = body.querySelector<HTMLElement>("#trig-sec-anim");
  if (secAnim)
    renderButtonLinkSection(secAnim, item, {
      onEditGroup: (gid) => openAnimGroupEditorById(gid),
      rerender,
    });
}

// ---------------------------------------------------------------- ① 直连机器目标（switchLinks）

/** 渲染 + 接线「① 直连机器目标」分区。移除目标会同步清理失效事件（rerender）。
 *  传送带方向切换已统一为「开关动画组」（③ 动画组序列，节点环），不再走
 *  switchLink 直连；后端加载场景时会把遗留 Animate 联动自动迁移掉。 */
function renderSwitchTargetSection(host: HTMLElement, item: EditorItem, rerender: () => void): void {
  const myId = item.instanceId ?? "";
  const myLinks = () => S.switchLinks.filter((l) => l.switchId === myId);

  const linkTargetOptsHtml = () => {
    const linked = new Set(myLinks().map((l) => l.targetId));
    const opts = S.items
      .filter(
        (i) =>
          i.instanceId &&
          i.instanceId !== myId &&
          !linked.has(i.instanceId) &&
          isSwitchLinkTarget(i)
      )
      .map((i) => `<option value="${escHtml(i.instanceId)}">${escHtml(itemLabel(i))}</option>`)
      .join("");
    return opts || '<option value="">— 无可联动目标（仅断头台/饮料机/酱料机/大炮） —</option>';
  };

  host.innerHTML = `<p class="trig-hint">按下按钮时向这些机器广播触发消息（默认取机器原生触发名；同一开关的所有机器联动共享一个触发名）。事件组的目标只能从这里选。</p>
    <div id="trig-sw-links" class="trig-list"></div>
    <div class="trig-addrow"><select id="trig-sw-target" class="trig-select">${linkTargetOptsHtml()}</select>
      <button type="button" class="btn-small primary" id="trig-sw-add">＋ 添加目标</button></div>
    <label class="trig-field" id="trig-sw-trigger-field">触发消息 <input id="trig-sw-trigger" class="trig-input" value="${escHtml(myLinks()[0]?.trigger ?? "Switch")}" placeholder="Switch"/></label>`;

  const linksEl = host.querySelector<HTMLElement>("#trig-sw-links");

  const bindUnlink = (container: HTMLElement) => {
    container.querySelectorAll<HTMLButtonElement>("[data-unlink]").forEach((btn) => {
      btn.addEventListener("click", () => {
        pushHistory();
        const tid = btn.dataset.unlink!;
        S.switchLinks = S.switchLinks.filter((l) => !(l.switchId === myId && l.targetId === tid));
        // 事件组目标仅限联动目标：联动被移除时同步丢弃对应事件后整体重绘
        cleanOrphanedButtonEvents();
        setStatus("已移除开关联动（写回后生效）");
        rerender();
      });
    });
  };

  const renderRows = () => {
    if (!linksEl) return;
    const links = myLinks();
    if (!links.length) {
      linksEl.innerHTML = '<p class="trig-hint">未设置机器联动目标</p>';
    } else {
      linksEl.innerHTML = links
        .map((l) => {
          const target = S.items.find((i) => i.instanceId === l.targetId);
          return `<div class="trig-step"><span class="trig-step-idx">→</span>
            <span class="trig-step-label">${escHtml(target ? itemLabel(target) : l.targetId)}</span>
            <button type="button" class="btn-small blm-mini" data-unlink="${escHtml(l.targetId)}">移除</button></div>`;
        })
        .join("");
      bindUnlink(linksEl);
    }
  };
  renderRows();

  host.querySelector("#trig-sw-add")?.addEventListener("click", () => {
    const sel = host.querySelector<HTMLSelectElement>("#trig-sw-target");
    const tid = sel?.value ?? "";
    if (!tid || !myId) return;
    pushHistory();
    const trigInput = host.querySelector<HTMLInputElement>("#trig-sw-trigger");
    let trigger = trigInput?.value.trim() || "";
    if (!trigger || trigger === "Switch") {
      const target = S.items.find((i) => i.instanceId === tid);
      const native = target ? nativeLinkTrigger(target) : null;
      if (native) {
        trigger = native;
      } else if (myLinks()[0]?.trigger) {
        trigger = myLinks()[0]!.trigger ?? "Switch";
      } else {
        const prefabId = target ? prefabIdFromPath(target.prefabAssetPath) ?? "item" : "item";
        trigger = `switch_${prefabId}_1`;
      }
    }
    S.switchLinks.push({ switchId: myId, targetId: tid, trigger });
    setStatus(`已添加开关联动（${trigger}，写回后生效）`);
    // 目标变化会影响事件组可选目标：整体重绘
    rerender();
  });

  host.querySelector("#trig-sw-trigger")?.addEventListener("change", () => {
    const inp = host.querySelector<HTMLInputElement>("#trig-sw-trigger");
    const trig = inp?.value.trim() || "Switch";
    const links = myLinks();
    if (!links.length) return;
    pushHistory();
    for (const l of links) l.trigger = trig;
    // 事件组触发名固定取联动共享触发名：联动改名时同步事件
    for (const bl of S.buttonEvents) {
      if (bl.sourceId !== myId) continue;
      for (const g of bl.groups) for (const e of g.events) e.trigger = trig;
    }
    setStatus(`已更新触发消息为 ${trig}（写回后生效）`);
  });
}

// ---------------------------------------------------------------- 右侧「触发源」面板

/** 右侧面板「🔘 触发源」Tab：列出全部按钮/开关，点「编排…」在底部编排台打开模式 B。 */
export function renderTriggerSourcePanel(body: HTMLElement): void {
  const sources = S.items.filter((i) => i.instanceId && isButtonLinkSource(i));
  const countEl = document.getElementById("triggers-count");
  if (countEl) countEl.textContent = sources.length > 0 ? `(${sources.length})` : "";

  const parts: string[] = [];
  parts.push(
    `<div class="anim-list-head"><span class="anim-list-title">🔘 触发源${sources.length ? ` (${sources.length})` : ""}</span></div>`
  );
  parts.push(
    `<div class="trig-panel-note">按钮 / 开关 / 压力开关的联动都在此统一编排：按下 →（机器）→ 事件组 → 动画组。点「编排…」在底部面板打开，预览不被遮挡。</div>`
  );
  for (const src of sources) {
    const id = src.instanceId ?? "";
    const evLink = eventLinkOfSource(id);
    const evN = evLink?.groups.length ?? 0;
    const active = currentSourceId === id ? " active" : "";
    parts.push(
      `<div class="trig-src-card${active}" data-trigsrc="${escHtml(id)}">
        <div class="trig-src-main">
          <div class="trig-src-name">${escHtml(itemLabel(src))}</div>
          <div class="trig-src-sub">🔁 ${escHtml(buttonEventSummaryText(evN > 0 ? evLink : undefined))}</div>
          <div class="trig-src-sub">🎬 ${escHtml(buttonLinkSummaryText(src))}</div>
        </div>
        <button type="button" class="btn-small primary" data-trigcfg="${escHtml(id)}">编排…</button>
      </div>`
    );
  }
  if (sources.length === 0) {
    parts.push(
      `<div class="anim-empty"><div class="anim-empty-icon">🔘</div>
        <div class="anim-empty-text">场景中暂无按钮 / 开关</div>
        <div class="anim-empty-sub">先在核心层 · 机制中放置一个开关或压力开关，再回到这里编排它触发的事件与动画。</div></div>`
    );
  }
  body.innerHTML = parts.join("");

  body.querySelectorAll<HTMLButtonElement>("[data-trigcfg]").forEach((btn) => {
    btn.addEventListener("click", (ev) => {
      ev.stopPropagation();
      const src = S.items.find((i) => i.instanceId === btn.dataset.trigcfg);
      if (src) openTriggerOrchestrator(src);
    });
  });
  body.querySelectorAll<HTMLElement>("[data-trigsrc]").forEach((row) => {
    row.addEventListener("click", () => {
      const src = S.items.find((i) => i.instanceId === row.dataset.trigsrc);
      if (!src) return;
      setSelection([src._editorKey]);
      ensureItemVisible(src);
      draw();
    });
  });
}
