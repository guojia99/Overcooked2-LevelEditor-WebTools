/**
 * 同轴按钮组的右键菜单编辑块（挂在 Switch 参数面板「联动目标」之后）。
 *
 * 语义：≥2 按钮组成一组，从第一个按下起在时间窗内（0.35~5s，默认 1s）全部
 * 按下才向目标机器广播触发消息（断头台 Chop / 饮料酱料机 Next / 大炮 Launch）；
 * 超时已按按钮自动弹回且不触发。一只按钮只属一组；数据存 S.coaxialLinks
 * （文档级，仅全量保存携带，由 CoaxialButtonBakery 烘焙为 Design/Coaxial Logic）。
 */
import { S, EditorItem } from "./state";
import { coaxialGroupOf } from "./buttonLinks";
import { stubKindOf, isSwitchLinkTarget, nativeLinkTrigger } from "./stubControls";
import { itemLabel } from "./labels";
import { uuid, escHtml } from "./coords";
import { pushHistory } from "./historyOps";
import { setStatus } from "./status";

export const COAXIAL_WINDOW_MIN = 0.35;
export const COAXIAL_WINDOW_MAX = 5;
export const COAXIAL_WINDOW_DEFAULT = 1;

export function clampCoaxialWindow(v: number): number {
  const n = Number(v);
  if (!Number.isFinite(n)) return COAXIAL_WINDOW_DEFAULT;
  return Math.max(COAXIAL_WINDOW_MIN, Math.min(COAXIAL_WINDOW_MAX, Math.round(n * 100) / 100));
}

/** 候选成员：Switch 按钮（未入任何同轴组；压力开关按压语义不同，不参与）。 */
function candidateSwitches(excludeId: string, extraExclude: Set<string>): EditorItem[] {
  return S.items.filter(
    (i) =>
      i.instanceId &&
      i.instanceId !== excludeId &&
      !extraExclude.has(i.instanceId) &&
      !coaxialGroupOf(i.instanceId) &&
      stubKindOf(i) === "Switch"
  );
}

/** 右键菜单「同轴组」区块 HTML（内容动态渲染在 #ctx-coax-body）。 */
export function coaxialCtxHtml(item: EditorItem): string {
  void item;
  return `<div class="ctx-stub-title" style="margin-top:6px">同轴组（时间窗内集齐才触发）</div>
    <div id="ctx-coax-body"></div>`;
}

/** 渲染 + 接线 #ctx-coax-body（stubControls 的 Switch case 末尾调用）。 */
export function wireCoaxialCtx(item: EditorItem): void {
  const myId = item.instanceId ?? "";
  if (!myId) return;
  refresh();

  function partnerOptsHtml(exclude: Set<string>): string {
    const opts = candidateSwitches(myId, exclude)
      .map((i) => `<option value="${escHtml(i.instanceId)}">${escHtml(itemLabel(i))}</option>`)
      .join("");
    return opts || '<option value="">— 无可用按钮（其余均已入组或非按钮） —</option>';
  }

  function targetOptsHtml(group: NonNullable<ReturnType<typeof coaxialGroupOf>>): string {
    const linked = new Set(group.targetIds ?? []);
    const opts = S.items
      .filter(
        (i) => i.instanceId && !linked.has(i.instanceId) && isSwitchLinkTarget(i)
      )
      .map((i) => `<option value="${escHtml(i.instanceId)}">${escHtml(itemLabel(i))}</option>`)
      .join("");
    return opts || '<option value="">— 无可联动目标（仅断头台/饮料机/酱料机/大炮） —</option>';
  }

  function bodyHtml(): string {
    const group = coaxialGroupOf(myId);
    if (!group) {
      return `<div class="ctx-stub-row" style="font-size:11px;color:#8a909a">未加入同轴组：≥2 按钮在时间窗内先后按下（首个按下起计时）才触发，超时自动弹回不触发</div>
        <label class="ctx-stub-row"><select id="ctx-coax-partner" class="ctx-input"></select>
          <button type="button" class="ctx-btn" id="ctx-coax-create">配对成组</button></label>`;
    }
    const memberRows = group.sourceIds
      .map((id) => {
        const it = S.items.find((i) => i.instanceId === id);
        const self = id === myId ? "（本按钮）" : "";
        return `<div class="ctx-stub-row">🔘 ${escHtml(it ? itemLabel(it) : id)}${self}
          <button type="button" class="ctx-btn" data-coax-member="${escHtml(id)}">移除</button></div>`;
      })
      .join("");
    const targetRows = (group.targetIds ?? [])
      .map((id, i) => {
        const it = S.items.find((it2) => it2.instanceId === id);
        const trig = group.triggers?.[i] || (it ? nativeLinkTrigger(it) ?? "Chop" : "Chop");
        return `<div class="ctx-stub-row">→ ${escHtml(it ? itemLabel(it) : id)}
          <span style="color:#8a909a">[${escHtml(trig)}]</span>
          <button type="button" class="ctx-btn" data-coax-target="${escHtml(id)}">移除</button></div>`;
      })
      .join("");
    return `${memberRows}
      <label class="ctx-stub-row"><select id="ctx-coax-addmember" class="ctx-input"></select>
        <button type="button" class="ctx-btn" id="ctx-coax-memberadd">加按钮</button></label>
      <label class="ctx-stub-row">时间窗 <input type="number" id="ctx-coax-window" class="ctx-input" style="width:64px"
        min="${COAXIAL_WINDOW_MIN}" max="${COAXIAL_WINDOW_MAX}" step="0.05" value="${group.windowSeconds}"/> 秒（${COAXIAL_WINDOW_MIN}~${COAXIAL_WINDOW_MAX}）</label>
      ${targetRows || '<div class="ctx-stub-row" style="font-size:11px;color:#8a909a">未接目标机器：纯同轴组（触发无效果），在下方添加目标</div>'}
      <label class="ctx-stub-row"><select id="ctx-coax-addtarget" class="ctx-input"></select>
        <button type="button" class="ctx-btn" id="ctx-coax-targetadd">加目标</button></label>
      <label class="ctx-stub-row"><button type="button" class="ctx-btn" id="ctx-coax-disband">解散本组</button></label>`;
  }

  function refresh(): void {
    const body = document.getElementById("ctx-coax-body");
    if (!body) return;
    body.innerHTML = bodyHtml();
    const group = coaxialGroupOf(myId);

    if (!group) {
      const sel = document.getElementById("ctx-coax-partner") as HTMLSelectElement | null;
      if (sel) sel.innerHTML = partnerOptsHtml(new Set());
      document.getElementById("ctx-coax-create")?.addEventListener("click", () => {
        const partner = (document.getElementById("ctx-coax-partner") as HTMLSelectElement | null)?.value ?? "";
        if (!partner) {
          setStatus("没有可配对的按钮（其余开关都已入组？）", false);
          return;
        }
        pushHistory();
        S.coaxialLinks.push({
          id: uuid(),
          sourceIds: [myId, partner],
          windowSeconds: COAXIAL_WINDOW_DEFAULT,
          targetIds: [],
          triggers: [],
        });
        setStatus("已创建同轴组（时间窗默认 1s，可调 0.35~5s；写回后生效）");
        refresh();
      });
      return;
    }

    const memberSel = document.getElementById("ctx-coax-addmember") as HTMLSelectElement | null;
    if (memberSel) memberSel.innerHTML = partnerOptsHtml(new Set(group.sourceIds));
    const targetSel = document.getElementById("ctx-coax-addtarget") as HTMLSelectElement | null;
    if (targetSel) targetSel.innerHTML = targetOptsHtml(group);

    body.querySelectorAll<HTMLButtonElement>("[data-coax-member]").forEach((btn) => {
      btn.addEventListener("click", () => {
        pushHistory();
        const id = btn.dataset.coaxMember!;
        const g = coaxialGroupOf(myId);
        if (!g) return;
        g.sourceIds = g.sourceIds.filter((x) => x !== id);
        if (g.sourceIds.length < 2) {
          S.coaxialLinks = S.coaxialLinks.filter((x) => x !== g);
          setStatus("成员不足 2 个，同轴组已解散（写回后生效）");
        } else {
          setStatus(id === myId ? "已退出同轴组（写回后生效）" : "已移除成员（写回后生效）");
        }
        refresh();
      });
    });
    body.querySelectorAll<HTMLButtonElement>("[data-coax-target]").forEach((btn) => {
      btn.addEventListener("click", () => {
        pushHistory();
        const id = btn.dataset.coaxTarget!;
        const g = coaxialGroupOf(myId);
        if (!g) return;
        const idx = (g.targetIds ?? []).indexOf(id);
        if (idx >= 0) {
          g.targetIds = (g.targetIds ?? []).filter((x) => x !== id);
          g.triggers = (g.triggers ?? []).filter((_, i) => i !== idx);
        }
        setStatus("已移除同轴目标（写回后生效）");
        refresh();
      });
    });
    document.getElementById("ctx-coax-memberadd")?.addEventListener("click", () => {
      const id = (document.getElementById("ctx-coax-addmember") as HTMLSelectElement | null)?.value ?? "";
      if (!id) {
        setStatus("没有可加入的按钮", false);
        return;
      }
      pushHistory();
      const g = coaxialGroupOf(myId);
      if (g && !g.sourceIds.includes(id)) g.sourceIds.push(id);
      setStatus("已加入同轴组（写回后生效）");
      refresh();
    });
    document.getElementById("ctx-coax-targetadd")?.addEventListener("click", () => {
      const id = (document.getElementById("ctx-coax-addtarget") as HTMLSelectElement | null)?.value ?? "";
      if (!id) {
        setStatus("没有可添加的目标", false);
        return;
      }
      pushHistory();
      const g = coaxialGroupOf(myId);
      const target = S.items.find((i) => i.instanceId === id);
      if (g) {
        g.targetIds = [...(g.targetIds ?? []), id];
        g.triggers = [...(g.triggers ?? []), (target ? nativeLinkTrigger(target) ?? "" : "")];
      }
      setStatus("已添加同轴目标（写回后生效）");
      refresh();
    });
    const win = document.getElementById("ctx-coax-window") as HTMLInputElement | null;
    win?.addEventListener("change", () => {
      const g = coaxialGroupOf(myId);
      if (!g) return;
      pushHistory();
      g.windowSeconds = clampCoaxialWindow(parseFloat(win.value));
      win.value = String(g.windowSeconds);
      setStatus(`已更新时间窗为 ${g.windowSeconds}s（写回后生效）`);
    });
    document.getElementById("ctx-coax-disband")?.addEventListener("click", () => {
      pushHistory();
      S.coaxialLinks = S.coaxialLinks.filter((g) => !g.sourceIds.includes(myId));
      setStatus("已解散同轴组（写回后生效）");
      refresh();
    });
  }
}
