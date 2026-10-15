/**
 * 随机触发器（RandomTrigger）编辑：右侧「触发源」Tab 底部节点列表 + 编辑弹窗。
 *
 * 语义：一个信号输入（公有 OnTrigger，事件组/BLRelay/SMB/其他节点级联可达）或
 * 任一源按钮按下 → 按权重从候选列表随机选一个发出；最终信号 = 信号名 + 分隔符
 * （默认 ":"）+ 可编程参数值（固定/候选/随机字符串/随机 N 位整数/数值区间）；
 * 冷却限流 N 秒内只触发一次。数据存 S.randomTriggers（文档级，仅全量保存携带，
 * 由 RandomTriggerBakery 烘焙为 Design/RandomTrigger Logic，运行时
 * CustomStub.RandomTrigger 服务端权威掷骰）。
 */
import { S } from "./state";
import type { RandomTriggerLink, RandomTriggerEntry, RandomTriggerParam } from "../types";
import { itemLabel } from "./labels";
import { uuid, escHtml } from "./coords";
import { pushHistory } from "./historyOps";
import { setStatus } from "./status";
import { draw } from "./render";
import { renderRightPanel } from "./panels";
import { openModal, closeModal } from "../modals";
import { smallBtnHtml, modalBtnHtml, primaryBtnHtml, cancelBtnHtml } from "../ui/views/button";
import { nativeLinkTrigger } from "./stubControls";
import { isButtonLinkSource } from "./buttonLinks";

// ---------------------------------------------------------------- data

/** 节点显示名：回导 id（scene:RT:RT_xxx）取末段；uuid 用输入信号名/短 id。 */
export function rtNodeLabel(link: RandomTriggerLink): string {
  const id = link.id ?? "";
  if (id.startsWith("scene:RT:")) return id.slice("scene:RT:".length);
  const trig = (link.inputTrigger ?? "").trim();
  return trig ? `信号「${trig}」` : `节点 ${id.slice(0, 6)}`;
}

/** 清理失效随机触发器：源按钮/目标物品被删、rt: 目标节点被删（rt 引用保留——
 *  指向场景既有节点，删掉的按悬空清理）；目标为空的候选（= 自身）合法保留。 */
export function cleanOrphanedRandomTriggers(): void {
  const itemIds = new Set(S.items.map((i) => i.instanceId).filter(Boolean));
  const linkIds = new Set(S.randomTriggers.map((l) => l.id));
  for (const rt of S.randomTriggers) {
    rt.sourceIds = (rt.sourceIds ?? []).filter((id) => itemIds.has(id));
    for (const e of rt.entries ?? []) {
      const ref = e.targetRef ?? "";
      if (ref.startsWith("rt:")) {
        const targetId = ref.slice(3);
        if (!linkIds.has(targetId)) e.targetRef = "";
      } else if (ref && ref !== "" && !ref.startsWith("new:")) {
        if (!ref.startsWith("u:")) e.targetRef = "";
        else if (!itemIds.has(ref)) e.targetRef = "";
      }
    }
  }
  S.randomTriggers = S.randomTriggers.filter((rt) => (rt.entries ?? []).length > 0);
}

// ---------------------------------------------------------------- 面板区块（触发源 Tab 底部）

export function randomTriggerSectionHtml(): string {
  const nodes = S.randomTriggers;
  const cards = nodes
    .map((rt) => {
      const srcN = (rt.sourceIds ?? []).length;
      const entN = (rt.entries ?? []).length;
      const cooldown = rt.cooldownSeconds ?? 0;
      const input = (rt.inputTrigger ?? "").trim();
      return `<div class="trig-src-card" data-rtsrc="${escHtml(rt.id)}">
        <div class="trig-src-main">
          <div class="trig-src-name">🎲 ${escHtml(rtNodeLabel(rt))}</div>
          <div class="trig-src-sub">📥 输入 ${escHtml(input || "<任意>")}｜按钮直连 ${srcN}｜冷却 ${cooldown}s</div>
          <div class="trig-src-sub">📤 候选 ${entN} 个｜分隔符「${escHtml(rt.separator || ":")}」+ 参数</div>
        </div>
        ${smallBtnHtml("编辑…", "primary", { "data-rtcfg": rt.id })}
        ${smallBtnHtml("删除", "default", { "data-rtdel": rt.id })}
      </div>`;
    })
    .join("");
  return `<div class="anim-list-head"><span class="anim-list-title">🎲 随机触发器${nodes.length ? ` (${nodes.length})` : ""}</span></div>
    <div class="trig-panel-note">一个信号（或源按钮按下）进来 → 按权重随机选一个候选信号发出（信号名:参数，参数可编程随机）；冷却 N 秒内只触发一次。</div>
    ${cards || '<div class="anim-empty"><div class="anim-empty-icon">🎲</div><div class="anim-empty-text">暂无随机触发器</div></div>'}
    <div style="padding:6px 8px">${smallBtnHtml("＋ 新建随机触发器", "primary", { id: "rt-new" })}</div>`;
}

export function wireRandomTriggerSection(): void {
  document.getElementById("rt-new")?.addEventListener("click", () => {
    openRandomTriggerEditor(null);
  });
  document.querySelectorAll<HTMLButtonElement>("[data-rtcfg]").forEach((btn) => {
    btn.addEventListener("click", (ev) => {
      ev.stopPropagation();
      const link = S.randomTriggers.find((l) => l.id === btn.dataset.rtcfg);
      if (link) openRandomTriggerEditor(link);
    });
  });
  document.querySelectorAll<HTMLButtonElement>("[data-rtdel]").forEach((btn) => {
    btn.addEventListener("click", (ev) => {
      ev.stopPropagation();
      const id = btn.dataset.rtdel;
      const link = S.randomTriggers.find((l) => l.id === id);
      if (!id || !link) return;
      pushHistory();
      S.randomTriggers = S.randomTriggers.filter((l) => l.id !== id);
      // 联动清理：其它节点指向本节点的 rt: 引用退回「自身」（与载入期
      // cleanOrphanedRandomTriggers 同款语义，防止写回悬空引用报错）。
      for (const rt of S.randomTriggers) {
        for (const e of rt.entries ?? []) {
          if (e.targetRef === "rt:" + id) e.targetRef = "";
        }
      }
      setStatus(`已删除随机触发器「${rtNodeLabel(link)}」（可 Ctrl+Z 撤回；写回后生效）`);
      renderRightPanel();
      draw();
    });
  });
}

// ---------------------------------------------------------------- 编辑弹窗

interface DraftEntry {
  trigger: string;
  weight: number;
  targetRef: string;
  param: RandomTriggerParam | null;
}

interface Draft {
  id: string;
  inputTrigger: string;
  cooldownSeconds: number;
  separator: string;
  sourceIds: string[];
  entries: DraftEntry[];
}

const PARAM_MODES: Array<{ v: number; label: string }> = [
  { v: 0, label: "无参数（只发信号名）" },
  { v: 1, label: "固定值" },
  { v: 2, label: "候选列表（随机选一）" },
  { v: 3, label: "随机字符串" },
  { v: 4, label: "随机整数（N 位）" },
  { v: 5, label: "数值区间" },
];

function defaultInputTrigger(): string {
  return `rt_go_${uuid().slice(0, 6)}`;
}

function targetLabelOf(ref: string): string {
  if (!ref) return "自身";
  if (ref.startsWith("rt:")) {
    const link = S.randomTriggers.find((l) => l.id === ref.slice(3));
    return `🎲 ${link ? rtNodeLabel(link) : "?"}`;
  }
  const it = S.items.find((i) => i.instanceId === ref);
  return it ? itemLabel(it) : ref;
}


/** 打开随机触发器编辑弹窗（link=null 新建）。 */
export function openRandomTriggerEditor(link: RandomTriggerLink | null): void {
  const isNew = link == null;
  const draft: Draft = link
    ? {
        id: link.id,
        inputTrigger: link.inputTrigger ?? "",
        cooldownSeconds: Math.max(0, link.cooldownSeconds ?? 0),
        separator: link.separator || ":",
        sourceIds: [...(link.sourceIds ?? [])],
        entries: (link.entries ?? []).map((e) => ({
          trigger: e.trigger ?? "",
          weight: e.weight ?? 1,
          targetRef: e.targetRef ?? "",
          param: e.param ? { ...e.param, candidates: [...(e.param.candidates ?? [])] } : null,
        })),
      }
    : {
        id: uuid(),
        inputTrigger: defaultInputTrigger(),
        cooldownSeconds: 0,
        separator: ":",
        sourceIds: [],
        entries: [
          { trigger: "", weight: 1, targetRef: "", param: null },
          { trigger: "", weight: 1, targetRef: "", param: null },
        ],
      };

  openModal(
    isNew ? "🎲 新建随机触发器" : `🎲 编辑随机触发器 · ${escHtml(rtNodeLabel(link))}`,
    `<div id="rt-editor"></div>`,
    `${cancelBtnHtml("取消")} ${primaryBtnHtml("确定", { id: "rt-ok" })}`,
    { id: "random-trigger-editor", panelClass: "rt-trigger-modal", closeOnBackdrop: false }
  );
  const host = document.getElementById("rt-editor");
  if (!host) return;
  const editorHost: HTMLElement = host;
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  // footer 的确定按钮不在重绘的 host 内：只绑一次（wireEditor 随结构变化重跑，
  // 若在里面绑定会累积 commit 监听 → 一次点击多次 pushHistory 污染 undo 栈）。
  document.getElementById("rt-ok")?.addEventListener("click", commit);
  renderEditor();

  function candidateButtons(exclude: Set<string>): string {
    const opts = S.items
      .filter((i) => i.instanceId && i.instanceId !== draft.id && !exclude.has(i.instanceId) && isButtonLinkSource(i))
      .map((i) => `<option value="${escHtml(i.instanceId)}">${escHtml(itemLabel(i))}</option>`)
      .join("");
    return opts || '<option value="">— 场景中没有按钮/开关 —</option>';
  }

  function targetOptions(sel: string, selfId: string): string {
    const itemOpts = S.items
      .filter((i) => i.instanceId)
      .map(
        (i) =>
          `<option value="${escHtml(i.instanceId)}" ${sel === i.instanceId ? "selected" : ""}>${escHtml(itemLabel(i))}</option>`
      )
      .join("");
    const rtOpts = S.randomTriggers
      .filter((l) => l.id !== selfId)
      .map(
        (l) =>
          `<option value="rt:${escHtml(l.id)}" ${sel === "rt:" + l.id ? "selected" : ""}>🎲 ${escHtml(rtNodeLabel(l))}</option>`
      )
      .join("");
    const cur = sel;
    const known =
      cur === "" ||
      S.items.some((i) => i.instanceId === cur) ||
      (cur.startsWith("rt:") && S.randomTriggers.some((l) => "rt:" + l.id === cur && l.id !== selfId));
    const keep =
      cur && !known
        ? `<option value="${escHtml(cur)}" selected>${escHtml(targetLabelOf(cur))}（已失联）</option>`
        : "";
    return `<option value="" ${cur === "" ? "selected" : ""}>→ 自身物体</option>${keep}${rtOpts}${itemOpts}`;
  }

  function paramHtml(p: RandomTriggerParam | null): string {
    const mode = p?.mode ?? 0;
    const modeOpts = PARAM_MODES.map(
      (m) => `<option value="${m.v}" ${m.v === mode ? "selected" : ""}>${m.label}</option>`
    ).join("");
    let fields = "";
    if (mode === 1) {
      fields = `<label class="rt-mini">参数值<input type="text" class="trig-input rt-p-fixed" placeholder="固定参数值" value="${escHtml(p?.fixedValue ?? "")}"/></label>`;
    } else if (mode === 2) {
      const cands = (p?.candidates ?? []).join(" | ");
      fields = `<label class="rt-mini rt-grow">候选（| 分隔）<input type="text" class="trig-input rt-p-cands" placeholder="如 开|关|待机" value="${escHtml(cands)}"/></label>`;
    } else if (mode === 3) {
      fields = `<label class="rt-mini">长度<input type="number" class="trig-input rt-p-len" min="1" max="64" value="${p?.stringLength ?? 4}"/></label>
        <label class="rt-mini rt-grow">字符集<input type="text" class="trig-input rt-p-charset" placeholder="默认：大小写字母 + 数字" value="${escHtml(p?.charset ?? "")}"/></label>`;
    } else if (mode === 4) {
      fields = `<label class="rt-mini">位数（1~9）<input type="number" class="trig-input rt-p-digits" min="1" max="9" value="${p?.digits ?? 1}"/></label>`;
    } else if (mode === 5) {
      fields = `<label class="rt-mini">最小<input type="number" class="trig-input rt-p-min" step="any" value="${p?.minValue ?? 0}"/></label>
        <label class="rt-mini">最大<input type="number" class="trig-input rt-p-max" step="any" value="${p?.maxValue ?? 1}"/></label>
        <label class="rt-mini">小数位<input type="number" class="trig-input rt-p-dec" min="0" max="6" value="${p?.decimals ?? 0}"/></label>`;
    }
    const fieldsHtml = fields
      ? `<div class="rt-param-fields">${fields}</div>`
      : `<span class="rt-param-none">只发信号名，无参数附加</span>`;
    return `<div class="rt-param-block">
      <span class="rt-param-label">参数</span>
      <div class="rt-param-row"><select class="trig-select rt-p-mode">${modeOpts}</select>${fieldsHtml}</div>
    </div>`;
  }

  function entryRows(): string {
    return draft.entries
      .map((e, i) => {
        return `<div class="rt-entry" data-idx="${i}">
          <div class="rt-entry-head">
            <span class="rt-entry-idx" title="候选 #${i + 1}">#${i + 1}</span>
            <span class="rt-entry-head-title">候选结果</span>
            ${modalBtnHtml("删除", "btn-small rt-entry-del", { "data-rt-del": String(i) })}
          </div>
          <div class="rt-entry-main">
            <label class="rt-cell">权重<input type="number" class="trig-input rt-e-weight" min="0" step="any" value="${e.weight}"/></label>
            <label class="rt-cell rt-cell--grow">信号名<input type="text" class="trig-input rt-e-trigger" placeholder="如 Chop / Launch / 自定义" value="${escHtml(e.trigger)}"/></label>
            <label class="rt-cell rt-cell--target">目标<select class="trig-select rt-e-target">${targetOptions(e.targetRef, draft.id)}</select></label>
          </div>
          ${paramHtml(e.param)}
        </div>`;
      })
      .join("");
  }

  function renderEditor(): void {
    collectFromDom();
    const srcRows = draft.sourceIds
      .map((id) => {
        const it = S.items.find((i) => i.instanceId === id);
        return `<div class="rt-src-chip"><span class="rt-src-chip-icon" aria-hidden="true">🔘</span>
          <span class="rt-src-chip-label">${escHtml(it ? itemLabel(it) : id)}</span>
          ${modalBtnHtml("移除", "btn-small rt-src-chip-del", { "data-rt-srcdel": id })}</div>`;
      })
      .join("");
    editorHost.innerHTML = `
      <div class="rt-editor-shell">
        <section class="rt-section rt-section--global">
          <header class="rt-section-head">
            <span class="rt-section-icon" aria-hidden="true">📥</span>
            <div class="rt-section-titles">
              <h3 class="rt-section-title">触发入口</h3>
              <p class="rt-section-desc">信号名匹配或源按钮按下时掷骰；冷却期内忽略重复输入</p>
            </div>
          </header>
          <div class="rt-grid">
            <label class="rt-field rt-field--primary">输入信号名<input type="text" class="trig-input" id="rt-input-trigger" value="${escHtml(draft.inputTrigger)}" placeholder="留空 = 匹配任意输入信号"/></label>
            <label class="rt-field">冷却（秒）<input type="number" class="trig-input" id="rt-cooldown" min="0" step="any" value="${draft.cooldownSeconds}"/></label>
            <label class="rt-field">参数分隔符<input type="text" class="trig-input" id="rt-separator" value="${escHtml(draft.separator)}"/></label>
          </div>
          <div class="rt-callout">
            <span class="rt-callout-icon" aria-hidden="true">ℹ️</span>
            <p>事件组、BLRelay、动画 SMB 或其它随机触发器用「输入信号名」向本节点发消息；下方源按钮按下也会触发。</p>
          </div>
        </section>

        <section class="rt-section rt-section--sources">
          <header class="rt-section-head">
            <span class="rt-section-icon" aria-hidden="true">🔘</span>
            <div class="rt-section-titles">
              <h3 class="rt-section-title">源按钮</h3>
              <p class="rt-section-desc">按下即掷骰，可不选（纯信号模式）</p>
            </div>
          </header>
          <div class="rt-src-list">${srcRows || '<div class="rt-empty">未选源按钮 — 仅通过信号触发</div>'}</div>
          <div class="rt-toolbar-row">
            <select id="rt-src-add" class="trig-select rt-toolbar-select">${candidateButtons(new Set(draft.sourceIds))}</select>
            ${smallBtnHtml("＋ 加按钮", "default", { id: "rt-src-addbtn" })}
          </div>
        </section>

        <section class="rt-section rt-section--entries">
          <header class="rt-section-head rt-section-head--split">
            <div class="rt-section-head-main">
              <span class="rt-section-icon" aria-hidden="true">🎲</span>
              <div class="rt-section-titles">
                <h3 class="rt-section-title">候选列表</h3>
                <p class="rt-section-desc">按权重随机选一，至少 2 项</p>
              </div>
            </div>
            ${smallBtnHtml("＋ 加候选", "primary", { id: "rt-entry-add" })}
          </header>
          <div class="rt-entry-list">${entryRows()}</div>
          <div class="rt-callout rt-callout--subtle">
            <span class="rt-callout-icon" aria-hidden="true">📤</span>
            <p>最终信号 = <strong>信号名</strong> + 分隔符 + 参数（例 <code>Door:Open</code>）。选「无参数」则只发信号名；机器目标可留默认原生触发名。</p>
          </div>
        </section>
      </div>`;
    wireEditor();
  }

  /** 结构变化（加/删/模式切换）前，先把 DOM 里的输入收进 draft。 */
  function collectFromDom(): void {
    if (!host) return;
    const input = document.getElementById("rt-input-trigger") as HTMLInputElement | null;
    if (input) draft.inputTrigger = input.value.trim();
    const cooldown = document.getElementById("rt-cooldown") as HTMLInputElement | null;
    if (cooldown) draft.cooldownSeconds = Math.max(0, Number(cooldown.value) || 0);
    const sep = document.getElementById("rt-separator") as HTMLInputElement | null;
    if (sep) draft.separator = sep.value || ":";
    editorHost.querySelectorAll<HTMLElement>(".rt-entry").forEach((row) => {
      const idx = Number(row.dataset.idx);
      const e = draft.entries[idx];
      if (!e) return;
      const w = row.querySelector<HTMLInputElement>(".rt-e-weight");
      if (w) e.weight = Math.max(0, Number(w.value) || 0);
      const t = row.querySelector<HTMLInputElement>(".rt-e-trigger");
      if (t) e.trigger = t.value;
      const sel = row.querySelector<HTMLSelectElement>(".rt-e-target");
      if (sel) e.targetRef = sel.value;
      // 参数（无 .rt-p-mode 行 = 无参数行）
      const modeSel = row.querySelector<HTMLSelectElement>(".rt-p-mode");
      if (!modeSel) {
        e.param = null;
        return;
      }
      const mode = Number(modeSel.value);
      const param: RandomTriggerParam = e.param && e.param.mode === mode ? e.param : { mode };
      param.mode = mode;
      const fixed = row.querySelector<HTMLInputElement>(".rt-p-fixed");
      if (fixed) param.fixedValue = fixed.value;
      const cands = row.querySelector<HTMLInputElement>(".rt-p-cands");
      if (cands)
        param.candidates = cands.value
          .split("|")
          .map((s) => s.trim())
          .filter((s) => s.length > 0);
      const len = row.querySelector<HTMLInputElement>(".rt-p-len");
      if (len) param.stringLength = Math.max(1, Math.min(64, Math.round(Number(len.value) || 4)));
      const charset = row.querySelector<HTMLInputElement>(".rt-p-charset");
      if (charset) param.charset = charset.value.trim();
      const digits = row.querySelector<HTMLInputElement>(".rt-p-digits");
      if (digits) param.digits = Math.max(1, Math.min(9, Math.round(Number(digits.value) || 1)));
      const min = row.querySelector<HTMLInputElement>(".rt-p-min");
      if (min) param.minValue = Number(min.value) || 0;
      const max = row.querySelector<HTMLInputElement>(".rt-p-max");
      if (max) param.maxValue = Number(max.value) || 0;
      const dec = row.querySelector<HTMLInputElement>(".rt-p-dec");
      if (dec) param.decimals = Math.max(0, Math.min(6, Math.round(Number(dec.value) || 0)));
      e.param = param;
    });
  }

  function wireEditor(): void {
    document.getElementById("rt-src-addbtn")?.addEventListener("click", () => {
      const sel = document.getElementById("rt-src-add") as HTMLSelectElement | null;
      const id = sel?.value ?? "";
      if (!id) {
        setStatus("没有可加入的按钮（先在画布放一个按钮/开关）", false);
        return;
      }
      if (!draft.sourceIds.includes(id)) draft.sourceIds.push(id);
      renderEditor();
    });
    editorHost.querySelectorAll<HTMLButtonElement>("[data-rt-srcdel]").forEach((btn) => {
      btn.addEventListener("click", () => {
        // dataset 键名：data-rt-srcdel → rtSrcdel（连字符转驼峰）
        draft.sourceIds = draft.sourceIds.filter((x) => x !== btn.dataset.rtSrcdel);
        renderEditor();
      });
    });
    document.getElementById("rt-entry-add")?.addEventListener("click", () => {
      collectFromDom();
      draft.entries.push({ trigger: "", weight: 1, targetRef: "", param: null });
      renderEditor();
    });
    editorHost.querySelectorAll<HTMLButtonElement>("[data-rt-del]").forEach((btn) => {
      btn.addEventListener("click", () => {
        collectFromDom();
        const idx = Number(btn.dataset.rtDel);
        draft.entries.splice(idx, 1);
        renderEditor();
      });
    });
    editorHost.querySelectorAll<HTMLSelectElement>(".rt-p-mode").forEach((sel) => {
      sel.addEventListener("change", () => {
        collectFromDom();
        renderEditor();
      });
    });
    // 目标选择便捷：选机器目标且信号名为空 → 预填原生触发名
    editorHost.querySelectorAll<HTMLSelectElement>(".rt-e-target").forEach((sel) => {
      sel.addEventListener("change", () => {
        const row = sel.closest<HTMLElement>(".rt-entry");
        if (!row) return;
        const idx = Number(row.dataset.idx);
        const e = draft.entries[idx];
        if (!e) return;
        e.targetRef = sel.value;
        const trigInput = row.querySelector<HTMLInputElement>(".rt-e-trigger");
        if (!e.trigger && trigInput && !sel.value.startsWith("rt:") && sel.value !== "") {
          const target = S.items.find((i) => i.instanceId === sel.value);
          const native = target ? nativeLinkTrigger(target) : null;
          if (native) {
            e.trigger = native;
            trigInput.value = native;
          }
        }
      });
    });
  }

  function commit(): void {
    collectFromDom();
    const errs: string[] = [];
    if (draft.entries.length < 2) errs.push("候选至少 2 个");
    if (draft.entries.some((e) => !e.trigger.trim())) errs.push("存在信号名为空的候选");
    for (const [i, e] of draft.entries.entries()) {
      if (e.targetRef === "rt:" + draft.id) errs.push(`候选 #${i + 1} 指向自身节点`);
      const p = e.param;
      if (p && p.mode === 2 && !(p.candidates ?? []).length) errs.push(`候选 #${i + 1} 参数候选列表为空`);
      if (p && p.mode === 1 && !p.fixedValue?.trim()) errs.push(`候选 #${i + 1} 固定参数值为空`);
      if (p && p.mode === 5 && p.minValue === p.maxValue) errs.push(`候选 #${i + 1} 区间最小=最大`);
    }
    if (errs.length) {
      setStatus(`随机触发器未保存：${errs.join("；")}`, false);
      return;
    }
    pushHistory();
    const entry: RandomTriggerEntry[] = draft.entries.map((e) => ({
      trigger: e.trigger.trim(),
      weight: e.weight,
      targetRef: e.targetRef,
      param: e.param && e.param.mode !== 0 ? e.param : null,
    }));
    const link: RandomTriggerLink = {
      id: draft.id,
      sourceIds: draft.sourceIds,
      inputTrigger: draft.inputTrigger,
      cooldownSeconds: draft.cooldownSeconds,
      separator: draft.separator || ":",
      entries: entry,
    };
    const idx = S.randomTriggers.findIndex((l) => l.id === draft.id);
    if (idx >= 0) S.randomTriggers[idx] = link;
    else S.randomTriggers.push(link);
    closeModal();
    // 权重全 0 与后端/运行时同语义（视为等权），仅提示不阻断。
    if (draft.entries.every((e) => e.weight <= 0))
      setStatus("已保存随机触发器（权重全 0 = 等权随机；写回后生效）");
    else setStatus(isNew ? "已新建随机触发器（写回后生效）" : "已更新随机触发器（写回后生效）");
    renderRightPanel();
    draw();
  }
}
