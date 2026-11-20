import * as api from "./api";
import type { LevelDetail } from "./types";

/** Four independent drafts; never deduplicate a sequence or replace an untouched player. */
export function createScriptedDrafts(detail: LevelDetail): string[][] {
  return Array.from({ length: 4 }, (_, p) => [...(detail.configs[p]?.scriptedRecipes ?? [])]);
}

export function moveScriptedEntry(entries: string[], from: number, to: number): void {
  if (from < 0 || to < 0 || from >= entries.length || to >= entries.length) return;
  entries.splice(to, 0, entries.splice(from, 1)[0]);
}

export function assertScriptedReadback(expected: string[], actual: string[] | null | undefined): void {
  if (!actual || expected.length !== actual.length || expected.some((id, i) => id.toLowerCase() !== actual[i]?.toLowerCase())) {
    throw new Error("后端已接受保存，但原生序列读回不一致；请重新加载核实。");
  }
}

export async function mountNativeScriptedRecipes(host: HTMLElement, initial: LevelDetail, setName: string): Promise<void> {
  const esc = (value: string): string => value.replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]!));
  let detail = initial;
  const drafts = createScriptedDrafts(detail);
  const dirty = [false, false, false, false];
  let player = 0;
  let busy = false;
  let message = "";
  let webEnabled: boolean | null = null;
  let catalog: Awaited<ReturnType<typeof api.fetchRecipeCatalog>> = [];
  let loaded = false;
  const render = (): void => {
    host.dataset.dirty = dirty.some(Boolean) ? "1" : "0";
    host.dataset.saving = busy ? "1" : "0";
    const editable = detail.gameMode === "Campaign" && detail.configs[player]?.exists && loaded && !busy && host.dataset.parentSaving !== "1";
    const disabled = editable ? "" : "disabled";
    const names = new Map(catalog.map(recipe => [recipe.guid, recipe.nameZh || recipe.id || recipe.guid]));
    host.innerHTML = `<p class="modal-hint">原生 scriptedRecipes：按人数独立保存，允许重复，按列表顺序出单。仅 Campaign 可修改。与启用的 Web OrderSequence 互斥，请先在菜谱面板显式禁用 Web 顺序。</p>
      <p>${[0, 1, 2, 3].map(p => `<button type="button" data-player="${p}" ${busy ? "disabled" : ""}>${p + 1}P${dirty[p] ? " *" : ""}${p === player ? " (当前)" : ""}</button>`).join(" ")}</p>
      <p class="modal-hint">Web OrderSequence：${webEnabled === null ? "尚未确认" : webEnabled ? "已启用" : "未启用"}；当前 ${drafts[player].length} 项</p>
      ${(detail.configs[player]?.scriptedRecipeErrors ?? []).map(error => `<p class="modal-hint err">${esc(error)}</p>`).join("")}
      <ol>${drafts[player].map((guid, i) => `<li>${esc(names.get(guid) || guid || "[缺失引用]")}
        <button type="button" data-up="${i}" ${disabled}>上移</button>
        <button type="button" data-down="${i}" ${disabled}>下移</button>
        <button type="button" data-remove="${i}" ${disabled}>删除</button></li>`).join("")}</ol>
      <label class="m-field">菜谱<select data-recipe ${disabled}>${catalog.map(recipe => `<option value="${esc(recipe.guid)}">${esc(names.get(recipe.guid)!)}</option>`).join("")}</select></label>
      <label class="m-field">或输入菜谱 GUID<input data-guid placeholder="32 位 GUID" ${disabled}></label>
      <p><button type="button" data-add ${disabled}>追加（可重复）</button>
      <button type="button" data-clear ${disabled}>清空当前人数</button>
      <button type="button" data-discard ${busy ? "disabled" : ""}>放弃未保存的序列修改</button>
      <button type="button" data-save ${!loaded || busy || detail.gameMode !== "Campaign" ? "disabled" : ""}>保存原生序列</button></p>
      <p class="modal-hint" role="status">${esc(message)}</p>`;
    host.querySelectorAll<HTMLButtonElement>("[data-player]").forEach(button => button.onclick = () => { player = Number(button.dataset.player); render(); });
    const mutate = (action: () => void): void => {
      if (busy || host.dataset.parentSaving === "1") return;
      action(); dirty[player] = true; message = "草稿尚未保存"; render();
    };
    host.querySelectorAll<HTMLButtonElement>("[data-up]").forEach(button => button.onclick = () => mutate(() => moveScriptedEntry(drafts[player], Number(button.dataset.up), Number(button.dataset.up) - 1)));
    host.querySelectorAll<HTMLButtonElement>("[data-down]").forEach(button => button.onclick = () => mutate(() => moveScriptedEntry(drafts[player], Number(button.dataset.down), Number(button.dataset.down) + 1)));
    host.querySelectorAll<HTMLButtonElement>("[data-remove]").forEach(button => button.onclick = () => mutate(() => drafts[player].splice(Number(button.dataset.remove), 1)));
    host.querySelector<HTMLButtonElement>("[data-add]")!.onclick = () => {
      const id = host.querySelector<HTMLInputElement>("[data-guid]")!.value.trim() || host.querySelector<HTMLSelectElement>("[data-recipe]")!.value;
      if (!/^[a-f0-9]{32}$/i.test(id)) { message = "请输入有效的 32 位菜谱 GUID"; render(); return; }
      mutate(() => drafts[player].push(id));
    };
    host.querySelector<HTMLButtonElement>("[data-clear]")!.onclick = () => mutate(() => { drafts[player] = []; });
    host.querySelector<HTMLButtonElement>("[data-discard]")!.onclick = () => {
      if (busy || host.dataset.parentSaving === "1") return;
      if (dirty.some(Boolean) && !confirm("放弃四个人数所有未保存的原生序列修改？")) return;
      createScriptedDrafts(detail).forEach((entries, p) => { drafts[p] = entries; dirty[p] = false; });
      message = "已放弃原生序列草稿"; render();
    };
    host.querySelector<HTMLButtonElement>("[data-save]")!.onclick = async () => {
      if (busy || host.dataset.parentSaving === "1") return;
      if (!dirty.some(Boolean)) { message = "没有修改"; render(); return; }
      busy = true; message = "预检并保存…"; render();
      try {
        const [fresh, web] = await Promise.all([api.fetchLevelDetail(detail.levelInfoAssetPath), api.fetchLevelOrderSequence(detail.levelInfoAssetPath)]);
        if (fresh.gameMode !== "Campaign") throw new Error("当前模式已变化，仅 Campaign 可修改原生序列。");
        const effective = createScriptedDrafts(fresh).map((entries, p) => dirty[p] ? drafts[p] : entries);
        if (web?.enabled && effective.some(entries => entries.length)) throw new Error("Web OrderSequence 已启用；请先显式禁用，或清空全部原生序列。");
        const request: api.LevelConfigUpdateBody = { assetPath: detail.levelInfoAssetPath };
        const keys = ["config_1p", "config_2p", "config_3p", "config_4p"] as const;
        keys.forEach((key, p) => { if (dirty[p]) request[key] = { ...fresh.configs[p], scriptedRecipes: [...drafts[p]] }; });
        await api.updateLevelConfig(request);
        const saved = await api.fetchLevelDetail(detail.levelInfoAssetPath);
        keys.forEach((_, p) => { if (dirty[p]) assertScriptedReadback(drafts[p], saved.configs[p]?.scriptedRecipes); });
        detail = saved;
        createScriptedDrafts(saved).forEach((entries, p) => { drafts[p] = entries; dirty[p] = false; });
        webEnabled = !!web?.enabled;
        message = "原生序列已保存并逐项读回确认";
      } catch (error) { message = error instanceof Error ? error.message : String(error); }
      finally { busy = false; render(); }
    };
  };
  render();
  try {
    const [recipes, web, fresh] = await Promise.all([api.fetchRecipeCatalog(setName), api.fetchLevelOrderSequence(detail.levelInfoAssetPath), api.fetchLevelDetail(detail.levelInfoAssetPath)]);
    catalog = recipes; webEnabled = !!web?.enabled; detail = fresh;
    createScriptedDrafts(fresh).forEach((entries, p) => { drafts[p] = entries; });
    loaded = true;
  } catch (error) { message = `加载失败，原生序列未改动：${error instanceof Error ? error.message : error}`; }
  render();
}
