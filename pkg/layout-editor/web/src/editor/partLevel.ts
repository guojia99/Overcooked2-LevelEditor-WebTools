/**
 * 分 P（多阶段布局）编辑态核心 —— docs/10-分P关卡功能规格.md（附录 A 对齐决策）。
 *
 * M1 范围：数据模型（partLevel + 物件 partId）+ 编辑态（base/P 切换、视图隔离、
 * 参考叠显）+ 启用向导（存量全归 base，决策 D9）+ 普关兼容（无 partLevel 时一切如旧）。
 * 场景写回结构（slot 根/park 偏移/转场烘焙）在 M2/M3 落地；M1 的 partId 经桥侧
 * part_level~/part_level.json（按 hierarchyPath）往返，场景内无载体。
 */
import { S } from "./state";
import type { PartLevelConfig, PartConfig, PartParkConfig, PartTransitionConfig } from "../types";
import { openModal, closeModal, closeAllModals } from "../modals";
import { draw } from "./render";
import { setStatus } from "./status";
import { pushHistory } from "./historyOps";
import { clearSelection, clearFloorSelection } from "./selection";
import { hideDetail, hideContextMenu } from "./ui/overlay";
import { setLayer } from "./init";
import { modalFooterBtnsHtml, mBtnHtml, dangerBtnHtml } from "../ui/views/button";

export const PART_BAR_ID = "part-bar";
const BASE_PART = "base";

// ---------- 基础判定 ----------

/** 分 P 模式是否启用（未启用 = 普关，一切过滤短路放行）。 */
export function partLevelActive(): boolean {
  return S.partLevel?.enabled === true;
}

/** 物件/地板的 partId 归一化：分 P 模式下缺省 = "base"；普关返回 ""（无概念）。 */
export function partIdOf(obj: { partId?: string }): string {
  if (!partLevelActive()) return "";
  return obj.partId || BASE_PART;
}

export function partById(pid: string): PartConfig | undefined {
  return S.partLevel?.parts.find((p) => p.id === pid);
}

export function partLabel(pid: string): string {
  if (pid === BASE_PART) return "基础层";
  const p = partById(pid);
  return `${pid.toUpperCase()}${p?.label ? ` · ${p.label}` : ""}`;
}

/** 当前编辑的 P（普关返回 ""，调用方据此短路）。 */
export function currentPartId(): string {
  return partLevelActive() ? S.currentPart : "";
}

/** 物件是否属于当前编辑的 P（普关恒 true）。 */
export function itemInCurrentPart(it: { partId?: string }): boolean {
  if (!partLevelActive()) return true;
  return partIdOf(it) === S.currentPart;
}

export function floorInCurrentPart(f: { partId?: string }): boolean {
  return itemInCurrentPart(f);
}

/** 物件是否属于参考叠显层（仅绘制辅助，不可点选）。 */
export function itemInReferencePart(it: { partId?: string }): boolean {
  if (!partLevelActive() || S.partReferences.size === 0) return false;
  const pid = partIdOf(it);
  if (pid === S.currentPart) return false;
  return S.partReferences.has(pid);
}

export function floorInReferencePart(f: { partId?: string }): boolean {
  return itemInReferencePart(f);
}

/** 分 P 模式下物件/地板是否参与画布绘制（当前 P 全量 + 参考层叠显）。 */
export function itemVisibleOnCanvas(it: { partId?: string }): boolean {
  if (!partLevelActive()) return true;
  const pid = partIdOf(it);
  return pid === S.currentPart || (pid !== S.currentPart && S.partReferences.has(pid));
}

/** 是否以「参考层」身份绘制（半透明、不参与交互）。 */
export function itemDrawnAsReference(it: { partId?: string }): boolean {
  return partLevelActive() && itemInReferencePart(it);
}

// ---------- 新建物件盖章 ----------

/** 新建物件：归入当前 P；P 层内默认 partScoped（决策 §3.3）。普关不写字段。 */
export function stampNewItemPart(target: { partId?: string; partScoped?: boolean }): void {
  if (!partLevelActive()) {
    target.partId = undefined;
    target.partScoped = undefined;
    return;
  }
  target.partId = S.currentPart;
  target.partScoped = S.currentPart !== BASE_PART ? true : false;
}

export function stampNewFloorPart(target: { partId?: string }): void {
  if (!partLevelActive()) {
    target.partId = undefined;
    return;
  }
  target.partId = S.currentPart;
}

// ---------- P 管理 ----------

function nextPartId(): string {
  const used = new Set((S.partLevel?.parts ?? []).map((p) => p.id));
  let n = (S.partLevel?.parts.length ?? 0) + 1;
  while (used.has(`p${n}`)) n++;
  return `p${n}`;
}

/** 相邻 P 自动生成默认转场（线性链 N−1 段）。已存在的转场（含作者改过的参数）
 *  原样保留；只剔除引用已删阶段/非相邻对/重复的条目，再补齐缺失的相邻对。 */
export function rebuildDefaultTransitions(pl: PartLevelConfig): void {
  if (pl.parts.length < 2) {
    pl.transitions = [];
    return;
  }
  const idx = new Map<string, number>();
  pl.parts.forEach((p, i) => idx.set(p.id, i));
  const kept: PartTransitionConfig[] = [];
  const seen = new Set<string>();
  for (const t of pl.transitions ?? []) {
    const a = idx.get(t.fromPartId);
    const b = idx.get(t.toPartId);
    // 仅保留相邻对（线性链语义）；引用已删阶段或乱序对一律丢弃。
    if (a == null || b == null || b !== a + 1) continue;
    const key = `${t.fromPartId}>${t.toPartId}`;
    if (seen.has(key)) continue;
    seen.add(key);
    kept.push(t);
  }
  pl.transitions = kept;
  const preset = pl.defaultTransitionPreset ?? "sink_then_rise";
  for (let i = 0; i + 1 < pl.parts.length; i++) {
    const from = pl.parts[i];
    const to = pl.parts[i + 1];
    const key = `${from.id}>${to.id}`;
    if (seen.has(key)) continue;
    const outSeconds = 2;
    const inSeconds = 3;
    pl.transitions.push({
      id: `t_${from.id}_${to.id}`,
      fromPartId: from.id,
      toPartId: to.id,
      kind: "partTransition",
      preset,
      stableSeconds: 60,
      totalSeconds: outSeconds + inSeconds,
      outSeconds,
      inSeconds,
      cleanupContents: true,
      cleanupDelaySeconds: 0,
      killLooseEverywhere: true,
    });
  }
}

/** 开启向导：P 数量 + 存量物件归属（决策 D9 默认全归 base）。 */
export function openEnableWizard(): void {
  if (partLevelActive()) return;
  const body = `
    <div class="part-warn">
      <span class="part-warn-icon">⚠</span>
      <span><b>不可逆操作</b>：开启并<b>写回保存</b>后，本关卡永久成为分 P 关卡，无法再降级为普通单阶段关卡（回退只能靠写回历史或副本关卡）。</span>
    </div>
    <p class="modal-hint" style="margin:0 0 4px">分 P = 同一关卡按时间轴自动切换多套可玩布局（P1 → P2 → …，转场演出期玩家仍可移动）。</p>
    <div class="part-form">
      <label class="m-field">阶段数量
        <input type="number" id="wiz-part-count" min="2" max="8" value="3" style="width:100%" />
      </label>
      <label class="m-field">现有物件归入
        <select id="wiz-assign">
          <option value="base" selected>基础层（推荐：现有物件 = 全程常驻）</option>
          <option value="p1">P1（现有物件 = 开局布局）</option>
        </select>
      </label>
    </div>
    <p class="modal-hint">开启后自动生成默认转场（每段稳定 60s + 下沉再上浮 5s），可在「⚙ 分 P 管理 → 转场链」调整节奏、特效与清理策略——场景结构、转场动画与时间轴由写回自动烘焙。背景层仅基础层可编辑（背景全程常驻，不随 P 替换）。</p>
  `;
  const footer = modalFooterBtnsHtml("取消", "开启分 P");
  openModal("开启分 P 关卡", body, footer, { id: "part-enable-wizard" });
  const root = document.getElementById("modal-root")!;
  root.querySelector("[data-cancel]")?.addEventListener("click", () => closeModal());
  root.querySelector("[data-ok]")?.addEventListener("click", () => {
    const countEl = root.querySelector<HTMLInputElement>("#wiz-part-count");
    const assignEl = root.querySelector<HTMLSelectElement>("#wiz-assign");
    const count = Math.max(2, Math.min(8, Number(countEl?.value ?? 3) || 3));
    const assign = assignEl?.value === "p1" ? "p1" : "base";
    applyPartLevelEnable(count, assign);
    closeModal();
  });
}

/** 从关卡配置或启用向导提交：创建分 P 结构并归属存量物件。 */
export function applyPartLevelEnable(count: number, assign: "base" | "p1"): void {
  const parts: PartConfig[] = [];
  for (let i = 1; i <= count; i++) {
    parts.push({ id: `p${i}`, label: i === 1 ? "开局" : `阶段 ${i}` });
  }
  const pl: PartLevelConfig = { enabled: true, parts, transitions: [] };
  rebuildDefaultTransitions(pl);
  S.partLevel = pl;
  for (const it of S.items) it.partId = assign;
  for (const f of S.floors) f.partId = assign;
  S.currentPart = "p1";
  S.partReferences.clear();
  pushHistory();
  renderPartBar();
  draw();
  setStatus(
    `已开启分 P（${count} 个阶段）：现有物件全部归入${assign === "base" ? "基础层" : "P1"}；` +
      `请把各阶段玩法内容分别移入 P1…P${count}（右键菜单「移到分 P」）。写回保存后不可逆。`,
    false
  );
}

/** 追加一个空 P（自动补默认转场）。 */
export function addPartInteractive(): void {
  if (!partLevelActive()) return;
  const pl = S.partLevel!;
  const id = nextPartId();
  pl.parts.push({ id, label: `阶段 ${pl.parts.length + 1}` });
  rebuildDefaultTransitions(pl);
  pushHistory();
  renderPartBar();
  setStatus(`已添加 ${id.toUpperCase()}（默认转场已自动生成，可在管理面板调整）`, false);
}

function countPartObjects(pid: string): { items: number; floors: number } {
  let items = 0;
  let floors = 0;
  for (const it of S.items) if (partIdOf(it) === pid) items++;
  for (const f of S.floors) if (partIdOf(f) === pid) floors++;
  return { items, floors };
}

/** P 管理面板：改名 / park 方向与偏移 / 删除（须先清空）。 */
export function openManageModal(): void {
  if (!partLevelActive()) return;
  const pl = S.partLevel!;
  const axisLabel: Record<string, string> = { down: "↓ 下", up: "↑ 上", left: "← 左", right: "→ 右" };
  const presetLabel: Record<string, string> = {
    sink_then_rise: "下沉再上浮（先后）",
    slide_cross: "平移接力（并行）",
    wave_wipe: "海浪拍击（并行 + 海浪演出）",
  };
  const rows = pl.parts
    .map((p) => {
      const n = countPartObjects(p.id);
      return `
      <tr data-pid="${p.id}">
        <td class="part-id">${p.id.toUpperCase()}</td>
        <td><input type="text" class="part-label" value="${p.label ?? ""}" placeholder="名称" style="width:130px" /></td>
        <td>
          <span class="part-park-cell">
            <select class="part-axis" style="width:80px">
              ${["down", "up", "left", "right"].map((a) => `<option value="${a}"${(p.park?.axis ?? "down") === a ? " selected" : ""}>${axisLabel[a]}</option>`).join("")}
            </select>
            <input type="number" class="part-offset" min="5" max="200" step="1" value="${p.park?.offset ?? 30}" style="width:62px" title="停放偏移（米，5~200）" />
            <span class="part-offset-unit">m</span>
          </span>
        </td>
        <td class="part-count">${n.items} 物品 / ${n.floors} 地板</td>
        <td>${p.id === "p1" ? '<span class="modal-hint" style="margin:0">开局段</span>' : mBtnHtml("删除", "small", { "data-del": p.id })}</td>
      </tr>`;
    })
    .join("");

  // 转场卡片区（fx 组候选来自动画层的特效组）。
  const fxGroups = S.animControls.filter((g) => g.groupKind === "fx");
  const fxOptions = (sel?: string) =>
    `<option value=""${!sel ? " selected" : ""}>无</option>` +
    fxGroups
      .map((g) => `<option value="${g.id}"${sel === g.id ? " selected" : ""}>${g.displayName || g.id}</option>`)
      .join("");
  const transitionCards = (pl.transitions ?? [])
    .map(
      (t) => `
    <div class="part-trans-card" data-tid="${t.id}">
      <div class="part-trans-head">
        <span class="part-trans-arrow">${t.fromPartId.toUpperCase()} → ${t.toPartId.toUpperCase()}</span>
        <span class="part-trans-preset-tag">${presetLabel[t.preset] ?? t.preset}</span>
        <span class="part-section-hint">线性推进；转场演出期玩家仍可移动</span>
      </div>
      <div class="part-trans-grid">
        <label class="m-field">转场节奏
          <select class="t-preset">
            ${Object.entries(presetLabel).map(([v, lbl]) => `<option value="${v}"${t.preset === v ? " selected" : ""}>${lbl}</option>`).join("")}
          </select>
        </label>
        <label class="m-field">稳定时长（秒）
          <input type="number" class="t-stable" min="1" step="1" value="${t.stableSeconds}" title="fromPart 保持可玩的时长（转场前）" />
        </label>
        <label class="m-field">退场（秒）
          <input type="number" class="t-out" min="0.5" step="0.5" value="${t.outSeconds}" title="旧 P 退场动画时长" />
        </label>
        <label class="m-field">入场（秒）
          <input type="number" class="t-in" min="0.5" step="0.5" value="${t.inSeconds}" title="新 P 入场动画时长" />
        </label>
        <label class="m-field">特效组（shake / flash）
          <select class="t-fx" title="转场期的全屏特效（在动画层创建特效组后可选）">${fxOptions(t.fxGroupId)}</select>
        </label>
        <label class="m-field">清理延迟（秒）
          <input type="number" class="t-delay" min="0" step="1" value="${t.cleanupDelaySeconds ?? 0}" title="新 P 稳定后延迟 N 秒再清理旧 P 内容物" />
        </label>
        <div class="part-check-row">
          <label class="part-check"><input type="checkbox" class="t-cleanup"${t.cleanupContents !== false ? " checked" : ""} />转场后清理旧 P 内容物（盘上菜 / 锅内物 / 散落食材）</label>
          <label class="part-check"><input type="checkbox" class="t-loose"${t.killLooseEverywhere !== false ? " checked" : ""} />全场散落食材大扫除</label>
        </div>
      </div>
    </div>`
    )
    .join("");

  const transitionsSection =
    pl.parts.length < 2
      ? `<div class="part-section-title">转场链<span class="part-section-hint">至少 2 个阶段才会生成转场</span></div>
         <p class="modal-hint">当前只有 1 个阶段：分 P 至少 2 个阶段才有切换玩法，可点顶栏「＋」添加。</p>`
      : `<div class="part-section-title">转场链<span class="part-section-hint">线性推进 P1 → P2 → …；方向由各阶段的停放方向决定</span></div>
         ${transitionCards}`;

  const body = `
    <p class="modal-hint" style="margin:0 0 8px">P1 为开局布局（停放不生效，初始偏移恒 0）；其余 P 的停放方向 = 转场退场后的去向与距离。保存后场景结构（slot 根/停放偏移）、转场动画与时间轴由写回链自动烘焙，无需手动衔接。</p>
    <div class="part-section-title">阶段</div>
    <table class="part-table">
      <thead><tr><th>阶段</th><th>名称</th><th>停放方向</th><th>内容量</th><th></th></tr></thead>
      <tbody>${rows}</tbody>
    </table>
    ${transitionsSection}
  `;
  const footer = modalFooterBtnsHtml("取消", "保存");
  openModal("分 P 管理", body, footer, { id: "part-manage", panelClass: "part-manage-modal" });
  const root = document.getElementById("modal-root")!;
  root.querySelector("[data-cancel]")?.addEventListener("click", () => closeModal());
  root.querySelectorAll<HTMLButtonElement>("[data-del]").forEach((btn) => {
    btn.addEventListener("click", () => openDeleteConfirm(btn.dataset.del!));
  });
  root.querySelector("[data-ok]")?.addEventListener("click", () => {
    root.querySelectorAll<HTMLTableRowElement>("tr[data-pid]").forEach((tr) => {
      const pid = tr.dataset.pid!;
      const p = pl.parts.find((x) => x.id === pid);
      if (!p) return;
      const label = tr.querySelector<HTMLInputElement>(".part-label")?.value.trim();
      p.label = label || undefined;
      const axis = (tr.querySelector<HTMLSelectElement>(".part-axis")?.value ?? "down") as PartParkConfig["axis"];
      const offset = Math.max(5, Math.min(200, Number(tr.querySelector<HTMLInputElement>(".part-offset")?.value ?? 30) || 30));
      if (pid === "p1") p.park = undefined;
      else p.park = { axis, offset };
    });
    // 转场参数回读（钳制到合法区间）。
    root.querySelectorAll<HTMLElement>(".part-trans-card[data-tid]").forEach((card) => {
      const t = (pl.transitions ?? []).find((x) => x.id === card.dataset.tid);
      if (!t) return;
      t.preset = (card.querySelector<HTMLSelectElement>(".t-preset")?.value ?? t.preset) as PartTransitionConfig["preset"];
      t.stableSeconds = Math.max(1, Number(card.querySelector<HTMLInputElement>(".t-stable")?.value ?? 60) || 60);
      t.outSeconds = Math.max(0.5, Number(card.querySelector<HTMLInputElement>(".t-out")?.value ?? 2) || 2);
      t.inSeconds = Math.max(0.5, Number(card.querySelector<HTMLInputElement>(".t-in")?.value ?? 3) || 3);
      t.totalSeconds = t.outSeconds + t.inSeconds;
      const fx = card.querySelector<HTMLSelectElement>(".t-fx")?.value ?? "";
      t.fxGroupId = fx || undefined;
      t.cleanupContents = card.querySelector<HTMLInputElement>(".t-cleanup")?.checked !== false;
      t.killLooseEverywhere = card.querySelector<HTMLInputElement>(".t-loose")?.checked !== false;
      t.cleanupDelaySeconds = Math.max(0, Number(card.querySelector<HTMLInputElement>(".t-delay")?.value ?? 0) || 0);
    });
    pushHistory();
    renderPartBar();
    closeModal();
    setStatus("分 P 配置已更新（含转场参数）");
    draw();
  });
}

/** 阶段删除确认（弹在管理面板上层；带内容的阶段拒绝删除并提示先清空）。 */
function openDeleteConfirm(pid: string): void {
  const n = countPartObjects(pid);
  if (n.items > 0 || n.floors > 0) {
    setStatus(
      `无法删除 ${pid.toUpperCase()}：其中还有 ${n.items} 物品 / ${n.floors} 地板——请先在画布选中后右键「移到分 P」清空（基础层或其他 P）`,
      false
    );
    return;
  }
  const p = partById(pid);
  const body = `
    <div class="part-confirm-body">
      确认删除阶段 <b>${pid.toUpperCase()}${p?.label ? ` · ${p.label}` : ""}</b>？
      <br />其前后两段的转场会自动重新衔接；保存（写回）前可通过 <b>Ctrl+Z</b> 撤销。
    </div>
  `;
  const footer = `
    ${mBtnHtml("取消", "default", { "data-cancel": "" })}
    ${dangerBtnHtml("确认删除", { "data-ok": "" })}
  `;
  openModal("删除阶段", body, footer, { id: "part-delete-confirm" });
  const root = document.getElementById("modal-root")!;
  root.querySelector("[data-cancel]")?.addEventListener("click", () => closeModal());
  root.querySelector("[data-ok]")?.addEventListener("click", () => {
    const pl2 = S.partLevel!;
    pl2.parts = pl2.parts.filter((x) => x.id !== pid);
    rebuildDefaultTransitions(pl2);
    if (S.currentPart === pid) S.currentPart = BASE_PART;
    S.partReferences.delete(pid);
    pushHistory();
    closeAllModals();
    renderPartBar();
    openManageModal();
    setStatus(`已删除 ${pid.toUpperCase()}（Ctrl+Z 可撤销）`);
  });
}

// ---------- 当前 P 切换 ----------

export function setCurrentPart(pid: string): void {
  if (!partLevelActive()) return;
  if (pid !== BASE_PART && !partById(pid)) return;
  if (pid === S.currentPart) return;
  S.currentPart = pid;
  S.partReferences.delete(pid);
  clearSelection();
  clearFloorSelection();
  S.marqueeing = false;
  hideDetail();
  hideContextMenu();
  // 背景层仅基础层可编辑（背景全程常驻，不随 P 替换）：切出基础层时回退到地板层。
  if (pid !== BASE_PART && S.currentLayer === "background") setLayer("floor");
  renderPartBar();
  draw();
}

/** currentPart 指向已删除的 P 时收敛（回 base）。 */
export function ensureValidCurrentPart(): void {
  if (!partLevelActive()) {
    S.currentPart = BASE_PART;
    return;
  }
  if (S.currentPart !== BASE_PART && !partById(S.currentPart)) S.currentPart = BASE_PART;
  for (const ref of [...S.partReferences]) {
    if (ref === S.currentPart) S.partReferences.delete(ref);
    else if (ref !== BASE_PART && !partById(ref)) S.partReferences.delete(ref);
  }
}

// ---------- 顶栏（part bar） ----------

/** 分 P 已开启时显示顶栏「分P管理」；普关隐藏（开启入口在关卡配置 → 环境）。 */
export function updatePartToolbarButton(): void {
  const btn = document.getElementById("btn-part-level");
  if (!btn) return;
  btn.classList.toggle("hidden", !partLevelActive());
}

export function renderPartBar(): void {
  ensureValidCurrentPart();
  updatePartToolbarButton();
  const bar = document.getElementById(PART_BAR_ID);
  if (!bar) return;
  // 背景层仅基础层可编辑：先按当前状态刷新背景层 Tab（含关闭分 P 后恢复）。
  syncBackgroundTabState();
  if (!partLevelActive()) {
    bar.classList.add("hidden");
    bar.innerHTML = "";
    return;
  }
  const pl = S.partLevel!;
  const tabs = [
    `<button type="button" class="part-tab${S.currentPart === BASE_PART ? " active" : ""}" data-part="${BASE_PART}" title="全程常驻的主场景壳（不参与 P 替换）">🏠 基础层</button>`,
    ...pl.parts.map(
      (p) =>
        `<button type="button" class="part-tab${S.currentPart === p.id ? " active" : ""}" data-part="${p.id}" title="${p.label ?? p.id}">${p.id.toUpperCase()}${p.label ? ` · ${p.label}` : ""}</button>`
    ),
    `<button type="button" class="part-tab part-add" id="part-add" title="添加阶段（自动衔接转场）">＋</button>`,
    `<button type="button" class="part-tab part-manage" id="part-manage" title="分 P 管理：阶段 / 停放方向 / 转场节奏与清理">⚙</button>`,
  ].join("");
  const refTargets: { pid: string; label: string }[] = [];
  if (S.currentPart !== BASE_PART) refTargets.push({ pid: BASE_PART, label: "基础层" });
  for (const p of pl.parts) {
    if (p.id === S.currentPart) continue;
    refTargets.push({
      pid: p.id,
      label: `${p.id.toUpperCase()}${p.label ? ` · ${p.label}` : ""}`,
    });
  }
  const refChecks = refTargets
    .map(
      (t) => `
      <label class="part-ref-chip">
        <input type="checkbox" class="part-ref-cb" data-ref-part="${t.pid}"${S.partReferences.has(t.pid) ? " checked" : ""} />
        <span>${t.label}</span>
      </label>`
    )
    .join("");
  bar.innerHTML = `
    <span class="part-bar-label" title="分 P：同一关卡按时间轴切换多套布局">🔀 分 P</span>
    <div class="part-tabs" role="tablist">${tabs}</div>
    <div class="part-ref" title="参考叠显：勾选的阶段以半透明叠在当前视图下，仅供对齐（不可点选、不写入文档）">
      <span class="part-ref-label">叠显</span>
      <div class="part-ref-choices" role="group" aria-label="参考叠显">${refChecks || '<span class="part-ref-empty muted small">无其他阶段</span>'}</div>
    </div>
  `;
  bar.classList.remove("hidden");
  bar.querySelectorAll<HTMLButtonElement>(".part-tab[data-part]").forEach((b) =>
    b.addEventListener("click", () => setCurrentPart(b.dataset.part!))
  );
  bar.querySelector("#part-add")?.addEventListener("click", () => addPartInteractive());
  bar.querySelector("#part-manage")?.addEventListener("click", () => openManageModal());
  bar.querySelectorAll<HTMLInputElement>(".part-ref-cb").forEach((cb) => {
    cb.addEventListener("change", () => {
      const pid = cb.dataset.refPart!;
      if (cb.checked) S.partReferences.add(pid);
      else S.partReferences.delete(pid);
      draw();
    });
  });
}

/** 背景层仅基础层可编辑（背景全程常驻，不随 P 替换）：非基础层时禁用背景层 Tab。 */
function syncBackgroundTabState(): void {
  const bgTab = document.querySelector<HTMLButtonElement>('.layer-tab[data-layer="background"]');
  if (!bgTab) return;
  const blocked = partLevelActive() && S.currentPart !== BASE_PART;
  if (blocked) bgTab.setAttribute("disabled", "");
  else bgTab.removeAttribute("disabled");
  bgTab.title = blocked
    ? "背景层仅基础层可编辑（背景全程常驻，不随 P 替换）——请先切到 🔀 分 P · 基础层"
    : "背景层";
}

// ---------- 右键「移到分 P」 ----------

/** 菜单目标列表：base + 全部 P（排除当前 P）。 */
export function partMoveTargets(): { pid: string; label: string }[] {
  if (!partLevelActive()) return [];
  const out = [{ pid: BASE_PART, label: "🏠 基础层" }];
  for (const p of S.partLevel!.parts) {
    if (p.id === S.currentPart) continue;
    out.push({ pid: p.id, label: `${p.id.toUpperCase()}${p.label ? ` · ${p.label}` : ""}` });
  }
  return out;
}

/** 把当前选中物件/地板移入目标 P（决策 §3.2 严格作用域）。 */
export function moveSelectionToPart(pid: string): void {
  if (!partLevelActive()) return;
  if (pid !== BASE_PART && !partById(pid)) return;
  let n = 0;
  for (const it of S.items) {
    if (!S.selectedKeys.has(it._editorKey)) continue;
    it.partId = pid;
    it.partScoped = pid !== BASE_PART ? true : false;
    n++;
  }
  for (const f of S.floors) {
    if (!S.selectedFloorKeys.has(f._key)) continue;
    f.partId = pid;
    n++;
  }
  if (n > 0) {
    pushHistory();
    setStatus(`已把 ${n} 个物件移入 ${partLabel(pid)}`);
    draw();
  }
}

// ---------- 写回校验 ----------

/** 分 P 相关写回校验：errors 阻断、warnings 提示。 */
export function validatePartLevelForSave(): { errors: string[]; warnings: string[] } {
  const errors: string[] = [];
  const warnings: string[] = [];
  if (!partLevelActive()) return { errors, warnings };
  const pl = S.partLevel!;
  if (pl.parts.length === 0) errors.push("分 P 关卡必须定义至少一个阶段（parts 为空）");
  if (pl.parts.length === 1) warnings.push("只有 1 个阶段：分 P 至少 2 个阶段才有切换玩法（建议添加 P2）");
  const ids = new Set(pl.parts.map((p) => p.id));
  const seen = new Set<string>();
  for (const t of pl.transitions ?? []) {
    if (!ids.has(t.fromPartId) || !ids.has(t.toPartId)) {
      errors.push(`转场 ${t.id} 引用了不存在的阶段（${t.fromPartId} → ${t.toPartId}）`);
      continue;
    }
    const key = `${t.fromPartId}>${t.toPartId}`;
    if (seen.has(key)) errors.push(`转场重复：${key}`);
    seen.add(key);
    if (!(t.stableSeconds > 0)) warnings.push(`转场 ${t.id} 稳定时长为 0（该阶段会被立即切换）`);
  }
  // 连贯性：p1..pN 线性链每相邻对恰一段（决策 D4/Q2）。
  for (let i = 0; i + 1 < pl.parts.length; i++) {
    const key = `${pl.parts[i].id}>${pl.parts[i + 1].id}`;
    if (!seen.has(key)) warnings.push(`缺少转场 ${key}（烘焙前需在分 P 管理中补齐）`);
  }
  return { errors, warnings };
}
