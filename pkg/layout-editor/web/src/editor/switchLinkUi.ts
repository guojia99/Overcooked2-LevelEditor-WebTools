import { S } from "./state";
import type { EditorItem } from "./state";
import { escHtml } from "./coords";
import {
  instanceOrdinalByEditorKey,
  itemLabelDisambiguated,
  switchLinkTargetCaption,
  switchLinkTargetMeta,
} from "./labels";
import { isSwitchLinkTarget } from "./stubControls";
import { cleanOrphanedButtonEvents } from "./buttonEvents";
import { pushHistory } from "./historyOps";
import { setStatus } from "./status";
import { draw } from "./render";
import { setSelection, selectionKeys } from "./selection";
import { ensureItemVisible } from "./panels";
import { modalBtnHtml } from "../ui/views/button";

export type SwitchLinkListVariant = "ctx" | "dock";

export type SwitchLinkListOptions = {
  switchId: string;
  variant: SwitchLinkListVariant;
  /** 增删联动后回调（刷新下拉、触发名等） */
  onChange?: () => void;
};

function myLinks(switchId: string) {
  return S.switchLinks.filter((l) => l.switchId === switchId);
}

function activeTargetInstanceId(): string | undefined {
  const keys = selectionKeys();
  if (keys.length !== 1) return undefined;
  const it = S.items.find((i) => i._editorKey === keys[0]);
  return it?.instanceId;
}

function removeBtnHtml(targetId: string, variant: SwitchLinkListVariant): string {
  if (variant === "dock") {
    return modalBtnHtml("移除", "btn-small blm-mini", { "data-unlink": targetId });
  }
  return `<button type="button" class="ctx-xbtn switch-link-unlink" data-unlink="${escHtml(targetId)}" title="移除联动">✕</button>`;
}

function renderListHtml(switchId: string, variant: SwitchLinkListVariant): string {
  const links = myLinks(switchId);
  const ordMap = instanceOrdinalByEditorKey(S.items);
  const highlightId = activeTargetInstanceId();

  if (!links.length) {
    const empty =
      variant === "dock"
        ? '<p class="trig-hint switch-link-empty">未设置机器联动目标</p>'
        : '<div class="ctx-stub-row switch-link-empty" style="font-size:11px;color:#8a909a">未设置联动目标</div>';
    return `<div class="switch-link-panel switch-link-panel--${variant}">
      <div class="switch-link-summary">共 0 个目标 · 点击行可在画布定位</div>
      <div class="switch-link-scroll">${empty}</div>
    </div>`;
  }

  const rows = links
    .map((l, i) => {
      const target = S.items.find((it) => it.instanceId === l.targetId);
      const linkNo = i + 1;
      const active =
        highlightId && l.targetId === highlightId ? " switch-link-row--active" : "";
      if (!target) {
        return `<div class="switch-link-row switch-link-row--missing${active}" role="listitem" data-target-id="${escHtml(l.targetId)}">
          <span class="switch-link-idx">${linkNo}</span>
          <div class="switch-link-body">
            <span class="switch-link-title">（已删除）${escHtml(l.targetId)}</span>
          </div>
          ${removeBtnHtml(l.targetId, variant)}
        </div>`;
      }
      return `<div class="switch-link-row${active}" role="listitem" tabindex="0" data-target-id="${escHtml(l.targetId)}" data-editor-key="${escHtml(target._editorKey)}">
        <span class="switch-link-idx">${linkNo}</span>
        <div class="switch-link-body">
          <span class="switch-link-title">${escHtml(switchLinkTargetCaption(target, linkNo, ordMap))}</span>
          <span class="switch-link-meta">${escHtml(switchLinkTargetMeta(target))}</span>
        </div>
        ${removeBtnHtml(l.targetId, variant)}
      </div>`;
    })
    .join("");

  return `<div class="switch-link-panel switch-link-panel--${variant}">
    <div class="switch-link-summary">共 ${links.length} 个目标 · 点击行选中并定位</div>
    <div class="switch-link-scroll" role="list">${rows}</div>
  </div>`;
}

function wireList(host: HTMLElement, options: SwitchLinkListOptions): void {
  const { switchId, onChange } = options;

  host.querySelectorAll<HTMLElement>("[data-unlink]").forEach((btn) => {
    btn.addEventListener("click", (ev) => {
      ev.stopPropagation();
      ev.preventDefault();
      const tid = btn.getAttribute("data-unlink");
      if (!tid) return;
      pushHistory();
      S.switchLinks = S.switchLinks.filter((l) => !(l.switchId === switchId && l.targetId === tid));
      cleanOrphanedButtonEvents();
      setStatus("已移除开关联动（写回后生效）");
      mountSwitchLinkList(host, options);
      onChange?.();
    });
  });

  const selectRow = (row: HTMLElement) => {
    const key = row.getAttribute("data-editor-key");
    if (!key) return;
    const it = S.items.find((i) => i._editorKey === key);
    if (!it) return;
    setSelection([key]);
    ensureItemVisible(it);
    draw();
    host.querySelectorAll(".switch-link-row").forEach((r) => r.classList.remove("switch-link-row--active"));
    row.classList.add("switch-link-row--active");
  };

  host.querySelectorAll<HTMLElement>(".switch-link-row[data-editor-key]").forEach((row) => {
    row.addEventListener("click", (ev) => {
      if ((ev.target as HTMLElement).closest("[data-unlink]")) return;
      selectRow(row);
    });
    row.addEventListener("keydown", (ev) => {
      if (ev.key === "Enter" || ev.key === " ") {
        ev.preventDefault();
        selectRow(row);
      }
    });
  });
}

/** 渲染并接线开关联动目标列表（右键联动 Tab / 编排台 ① 区共用）。 */
export function mountSwitchLinkList(host: HTMLElement, options: SwitchLinkListOptions): void {
  host.innerHTML = renderListHtml(options.switchId, options.variant);
  wireList(host, options);
}

/** 添加目标下拉选项 HTML（已联动目标排除 + 歧义标签）。 */
export function switchLinkTargetOptionsHtml(switchId: string, excludeSelfId: string): string {
  const linked = new Set(myLinks(switchId).map((l) => l.targetId));
  const ordMap = instanceOrdinalByEditorKey(S.items);
  const opts = S.items
    .filter(
      (i) =>
        i.instanceId &&
        i.instanceId !== excludeSelfId &&
        !linked.has(i.instanceId) &&
        isSwitchLinkTarget(i)
    )
    .map(
      (i) =>
        `<option value="${escHtml(i.instanceId)}">${escHtml(itemLabelDisambiguated(i, ordMap))}</option>`
    )
    .join("");
  return opts || '<option value="">— 无可联动目标（机器 / 步带·传送带控速 / 生成器批次） —</option>';
}
