import { changeLevelMode, fetchLevelModeConfig, fetchRecipeCatalog, updateLevelModeConfig } from "./api";
import type { HordeWave, LevelDetail, LevelGameMode, LevelModeConfig, RecipeEntry } from "./types";
import { mBtnHtml } from "./ui/views/button";

function esc(value: unknown): string {
  return String(value ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

/** Native mode authoring only. Web layout parts keep their own data and controls. */
export async function mountLevelModeEditor(
  host: HTMLElement, setName: string, assetPath: string, detail: LevelDetail,
  refreshDetail: () => Promise<void>,
  options?: { isLayoutDirty?: () => boolean }
): Promise<void> {
  const modeNames = { Campaign: "普通 Campaign", Dynamic: "原生 Dynamic", Horde: "敌群 Horde" };
  let dirty = false;
  let saving = false;
  let switching = false;
  let loading = detail.gameMode !== "Campaign";
  const intro = `<h3>关卡模式：${esc(modeNames[detail.gameMode] ?? detail.gameMode)}</h3>
    <p class="modal-hint">Web 分 P 仅控制布局，与原生 Dynamic 阶段及 Horde 的 1P–4P 配置独立。</p>
    <div class="m-form"><label class="m-field">目标模式<select data-mode-target>
      ${(Object.keys(modeNames) as LevelGameMode[]).map(mode => `<option value="${mode}" ${mode === detail.gameMode ? "selected" : ""}>${modeNames[mode]}</option>`).join("")}
    </select></label></div>
    <p class="modal-hint">切换会修改原生控制器、UI 绑定和模式配置。请先保存本页及 Web 布局修改，在 Unity 打开并激活目标场景，保存所有场景并关闭其他伪预制体场景。</p>
    <ul class="modal-hint"><li>Web 分 P 启用时，后端拒绝转为 Dynamic 或 Horde。</li>
      <li>转为 Horde：须有激活的 Horde 服务门；可勾选下方选项同时转换普通交付窗。</li>
      <li>离开 Horde：先删除金币门并保存；勾选下方选项可在切换时转换交付窗，无需预删最后一个服务门。</li>
      <li>转为 Dynamic：N 个原生阶段须有 N−1 个有效 native transition；缺失时切换失败。</li></ul>
    <label class="modal-check"><input type="checkbox" data-mode-convert> 同时转换受支持的普通/Horde 交付窗（改变外观和组件，保留位置、名称及回收绑定；不转换金币门或未知物件）</label>
    <p class="modal-hint">也可按目标模式新建模板关卡。</p>
    <div class="m-actions-row">${mBtnHtml("确认切换模式", "default", { "data-mode-switch": "" })}
      ${mBtnHtml("放弃模式编辑并重新加载", "default", { "data-mode-reload": "" })}</div>
    <p data-switch-status role="status" aria-live="polite"></p>`;
  host.innerHTML = `${intro}<div data-mode-body></div>`;
  const body = host.querySelector<HTMLElement>("[data-mode-body]")!;
  const target = host.querySelector<HTMLSelectElement>("[data-mode-target]")!;
  const switchButton = host.querySelector<HTMLButtonElement>("[data-mode-switch]")!;
  const reloadButton = host.querySelector<HTMLButtonElement>("[data-mode-reload]")!;
  const switchStatus = host.querySelector<HTMLElement>("[data-switch-status]")!;
  const page = host.closest<HTMLElement>(".modal-body") ?? host.parentElement!;
  const pageInputs = Array.from(page.querySelectorAll<HTMLInputElement | HTMLSelectElement>("input, select"))
    .filter(input => !host.contains(input));
  const inputValue = (input: HTMLInputElement | HTMLSelectElement): string =>
    input instanceof HTMLInputElement && input.type === "checkbox" ? String(input.checked) : input.value;
  const initialValues = new Map(pageInputs.map(input => [input, inputValue(input)]));
  const pageIsDirty = (): boolean => {
    // Audio controls are mounted asynchronously after this editor. Include them
    // when checking other tabs instead of relying only on the initial snapshot.
    for (const input of page.querySelectorAll<HTMLInputElement | HTMLSelectElement>("input, select")) {
      if (host.contains(input) || initialValues.has(input)) continue;
      const initial = input instanceof HTMLInputElement
        ? input.type === "checkbox" ? String(input.defaultChecked ?? input.hasAttribute("checked"))
          : input.defaultValue ?? input.getAttribute("value") ?? ""
        : Array.from(input.options).find(option => option.defaultSelected)?.value ?? input.options[0]?.value ?? "";
      initialValues.set(input, initial);
      pageInputs.push(input);
    }
    return pageInputs.some(input => input.isConnected && inputValue(input) !== initialValues.get(input));
  };
  const switchMessage = (message: string, error = false): void => {
    switchStatus.textContent = message;
    switchStatus.classList.toggle("err", error);
    switchStatus.setAttribute("role", error ? "alert" : "status");
  };
  const updateSwitch = (): void => {
    switchButton.disabled = loading || saving || switching || target.value === detail.gameMode;
    target.disabled = saving || switching;
    reloadButton.disabled = saving || switching;
  };
  target.addEventListener("change", updateSwitch);
  reloadButton.addEventListener("click", () => {
    if ((dirty || pageIsDirty()) && !confirm("重新加载会放弃本页所有未保存修改，确定继续？")) return;
    void refreshDetail().catch(error => switchMessage(`重新加载失败：${error instanceof Error ? error.message : error}`, true));
  });
  switchButton.addEventListener("click", () => void (async () => {
    if (loading || saving || switching || target.value === detail.gameMode) return;
    if (dirty || pageIsDirty() || options?.isLayoutDirty?.()) {
      switchMessage("有未保存修改，已阻止切换。请先保存模式配置和基础信息，或点击「放弃模式编辑并重新加载」后再切换。", true);
      return;
    }
    const mode = target.value as LevelGameMode;
    const convertHordeObjects = host.querySelector<HTMLInputElement>("[data-mode-convert]")!.checked;
    if (!confirm(`将「${modeNames[detail.gameMode]}」切换为「${modeNames[mode]}」？\n\n切换会保存原生控制器、UI 绑定及模式资产。${convertHordeObjects ? "同时转换交付窗外观和组件，保留位置、名称和回收绑定；不转换金币门或未知物件。" : "不转换场景道具。"}请确认已阅读上述场景、分 P、服务门和 native transition 限制。`)) return;
    switching = true;
    updateSwitch();
    const editor = body.querySelector<HTMLFieldSetElement>("[data-mode-fields]");
    if (editor) editor.disabled = true;
    const inputDisabled = new Map(pageInputs.map(input => [input, input.disabled]));
    pageInputs.forEach(input => input.disabled = true);
    const originalDisabled = new Map<HTMLButtonElement, boolean>();
    page.querySelectorAll<HTMLButtonElement>("button").forEach(button => {
      if (!host.contains(button)) { originalDisabled.set(button, button.disabled); button.disabled = true; }
    });
    switchMessage("正在切换原生模式并回读详情…");
    try {
      await changeLevelMode(assetPath, mode, convertHordeObjects);
      switchMessage("模式切换已回读确认，正在刷新关卡详情…");
      if (host.isConnected) await refreshDetail();
    } catch (error) {
      switchMessage(`切换未完成：${error instanceof Error ? error.message : error}。请按后端提示处理后重试；如请求中断，请先重新加载核实当前模式。`, true);
    } finally {
      switching = false;
      if (editor) editor.disabled = false;
      inputDisabled.forEach((disabled, input) => input.disabled = disabled);
      originalDisabled.forEach((disabled, button) => button.disabled = disabled);
      updateSwitch();
    }
  })());
  updateSwitch();
  if (detail.gameMode === "Campaign") {
    body.innerHTML = '<p class="muted">普通模式继续使用现有菜谱、订单及人数配置。</p>';
    return;
  }
  body.innerHTML = '<p role="status">正在加载模式配置与菜谱目录…</p>';
  let config: LevelModeConfig;
  let recipes: RecipeEntry[];
  try {
    [config, recipes] = await Promise.all([fetchLevelModeConfig(assetPath), fetchRecipeCatalog(setName)]);
    if (!config || config.gameMode !== detail.gameMode) throw new Error("模式配置与关卡类型不一致，请重新加载关卡。");
    if (config.gameMode === "Horde" && config.hordeConfigs?.length !== 4) throw new Error("Horde 配置必须包含独立的 1P 到 4P 四份数据。");
  } catch (error) {
    if (!host.isConnected) return;
    body.innerHTML = `<p role="alert">加载失败：${esc(error instanceof Error ? error.message : error)}</p>
      ${mBtnHtml("重新加载模式配置", "default", { "data-mode-retry": "" })}`;
    loading = false;
    updateSwitch();
    body.querySelector("[data-mode-retry]")?.addEventListener("click", () => {
      reloadButton.click();
    });
    return;
  }
  if (!host.isConnected) return;
  const catalog = new Map(recipes.filter(r => r.guid && !r.intermediate).map(r => [r.guid, r]));
  const enemies = config.enemies ?? [];
  const enemyCatalog = new Map(enemies.map(e => [e.guid, e]));
  const searches = new Map<string, string>();
  let playerIndex = 0;
  loading = false;
  updateSwitch();
  body.innerHTML = `<form data-mode-form novalidate>
    <fieldset data-mode-fields style="border:0;padding:0;min-width:0"></fieldset>
    <p data-mode-status role="status" aria-live="polite"></p></form>`;
  const form = host.querySelector<HTMLFormElement>("[data-mode-form]")!;
  const fields = form.querySelector<HTMLFieldSetElement>("[data-mode-fields]")!;
  const status = form.querySelector<HTMLElement>("[data-mode-status]")!;
  const say = (message: string, error = false): void => {
    status.textContent = message;
    status.classList.toggle("err", error);
    status.setAttribute("role", error ? "alert" : "status");
  };
  const markDirty = (): void => {
    dirty = true;
    say("有未保存的模式修改；保存将提交全部人数配置。");
  };
  const numberField = (label: string, key: string, value: number, min = 0, max = 86400, step = "1"): string =>
    `<label class="m-field">${esc(label)}<input type="number" data-number="${esc(key)}" value="${Number.isFinite(value) ? value : ""}"
      min="${min}" max="${max}" step="${step}" required></label>`;
  const action = (text: string, command: string, attrs: Record<string, string> = {}, disabled = false): string =>
    mBtnHtml(text, "default", { "data-action": command, ...attrs, ...(disabled ? { disabled: "" } : {}) });
  const moveButtons = (kind: string, index: number, length: number, extra: Record<string, string> = {}): string =>
    `${action("上移", `${kind}-up`, { "data-index": String(index), ...extra }, index === 0)}
     ${action("下移", `${kind}-down`, { "data-index": String(index), ...extra }, index === length - 1)}`;

  function menuHtml(guids: string[], key: string): string {
    const selected = new Set(guids);
    const entries = [...new Set([...guids, ...catalog.keys()])];
    return `<details data-menu="${esc(key)}" open style="margin:12px 0">
      <summary>菜谱菜单（已选 <span data-menu-count>${guids.length}</span> 道）</summary>
      <label class="m-field">搜索菜谱
        <input type="search" data-recipe-search="${esc(key)}" value="${esc(searches.get(key) ?? "")}" placeholder="中文名 / 英文名 / ID / GUID" autocomplete="off">
      </label>
      <div class="m-actions-row">${action("选择搜索结果", "recipes-select", { "data-menu-key": key })}
        ${action("取消搜索结果", "recipes-clear", { "data-menu-key": key })}</div>
      <div style="max-height:240px;overflow:auto;display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,220px),1fr));gap:6px">
        ${entries.map(guid => {
          const r = catalog.get(guid);
          const name = r?.nameZh || r?.nameEn || r?.id || `目录未收录：${guid}`;
          const search = `${name} ${r?.nameEn ?? ""} ${r?.id ?? ""} ${guid}`.toLowerCase();
          return `<label class="modal-check" data-recipe-row data-search="${esc(search)}" title="${esc(`${r?.id ?? "未收录"} · ${guid}`)}">
            <input type="checkbox" data-recipe value="${esc(guid)}" ${selected.has(guid) ? "checked" : ""}> ${esc(name)}</label>`;
        }).join("")}
      </div><p class="muted small" data-recipe-result></p>
    </details>`;
  }

  function eventHtml(wave: HordeWave, w: number): string {
    const count = Math.max(wave.enemyGuids.length, wave.spawnTimes.length);
    return `<div><h4>有序生成事件（${count} 条）</h4>
      <p class="modal-hint">按列表顺序保存敌人及对应生成时间（秒）。可重复添加同种敌人，也可使用相同时间。</p>
      ${count ? Array.from({ length: count }, (_, e) => {
        const guid = wave.enemyGuids[e] ?? "";
        const known = enemyCatalog.has(guid);
        return `<div data-event="${e}" class="m-form" style="margin-bottom:10px">
          <label class="m-field">事件 ${e + 1} · 敌人<select data-enemy required>
            <option value="">请选择敌人</option>
            ${!known && guid ? `<option value="${esc(guid)}" selected>目录未收录：${esc(guid)}</option>` : ""}
            ${enemies.map(enemy => `<option value="${esc(enemy.guid)}" ${guid === enemy.guid ? "selected" : ""}>${esc(enemy.nameZh || enemy.nameEn || enemy.id)}</option>`).join("")}
          </select></label>
          ${numberField("生成时间（秒）", `spawn-${w}-${e}`, wave.spawnTimes[e])}
          <div class="m-actions-row">${moveButtons("event", e, count, { "data-wave": String(w) })}
            ${action("删除事件", "event-remove", { "data-index": String(e), "data-wave": String(w) })}</div>
        </div>`;
      }).join("") : '<p class="muted">尚无事件，请添加至少一条生成事件。</p>'}
      ${action("添加生成事件", "event-add", { "data-wave": String(w) }, count >= 256 || !enemies.length)}
    </div>`;
  }

  function draw(): void {
    if (config.gameMode === "Dynamic") {
      fields.innerHTML = `<p class="modal-hint">原生阶段按顺序运行，每阶段使用自己的时长与菜单。至少 2 阶段，最多 64 阶段。</p>
        ${config.phases.map((phase, i) => `<fieldset data-phase="${i}" style="margin:12px 0;min-width:0">
          <legend>阶段 ${i + 1}</legend><p class="muted small">ID：${esc(phase.id)}</p>
          ${numberField("阶段时长（秒）", `phase-${i}`, phase.durationSeconds, 1)}
          ${menuHtml(phase.recipeGuids, phase.id)}
          <div class="m-actions-row">${moveButtons("phase", i, config.phases.length)}
            ${action("删除阶段", "phase-remove", { "data-index": String(i) }, config.phases.length <= 2)}</div>
        </fieldset>`).join("")}
        <div class="m-actions-row">${action("添加阶段", "phase-add", {}, config.phases.length >= 64)}</div>`;
    } else {
      const player = config.hordeConfigs[playerIndex];
      fields.innerHTML = `<h4>全人数共用参数</h4><div class="m-form">
        ${numberField("目标血量", "targetHealth", config.targetHealth, 1, 1000000)}
        ${numberField("修门速度", "targetRepairSpeed", config.targetRepairSpeed, 0, 1000000, "any")}
        ${numberField("单次修门阈值", "targetRepairThreshold", config.targetRepairThreshold, 0, 1000000, "any")}
        ${numberField("厨房血量", "kitchenHealth", config.kitchenHealth, 1, 1000000)}
      </div>
      <div class="m-actions-row" role="tablist" aria-label="Horde 人数配置">
        ${config.hordeConfigs.map((p, i) => action(`${i + 1}P · ${p.waves.length} 波`, "player", {
          "data-player": String(i), role: "tab", "aria-selected": String(i === playerIndex),
          "aria-controls": "horde-player-panel", id: `horde-player-tab-${i}`,
        })).join("")}
      </div><section id="horde-player-panel" role="tabpanel" aria-labelledby="horde-player-tab-${playerIndex}">
        <h4>${playerIndex + 1}P 独立配置</h4>
        ${numberField("修门花费上限", "repairCost", player.targetRepairCostMax, 0, 1000000)}
        <div class="m-actions-row"><span>复制当前人数到：</span>
          ${config.hordeConfigs.map((_, i) => i === playerIndex ? "" : `<label class="modal-check"><input type="checkbox" data-copy-player="${i}"> ${i + 1}P</label>`).join("")}
          ${action("复制并覆盖所选人数", "player-copy")}</div>
        <p class="modal-hint">复制包含修门花费、全部波次、菜单及生成事件。其他人数配置独立保存。</p>
        ${!enemies.length ? '<p role="alert">后端未返回 common04 敌人目录，无法新增或保存敌人，请重新加载关卡。</p>' : ""}
        ${player.waves.map((wave, w) => `<fieldset data-wave-row="${w}" style="margin:12px 0;min-width:0">
          <legend>第 ${w + 1} 波</legend><p class="muted small">ID：${esc(wave.id)}</p>
          ${numberField("波次间隔（秒）", `wave-${w}`, wave.intervalSeconds)}
          ${menuHtml(wave.recipeGuids, `${playerIndex}:${wave.id}`)}${eventHtml(wave, w)}
          <div class="m-actions-row">${moveButtons("wave", w, player.waves.length)}
            ${action("删除波次", "wave-remove", { "data-index": String(w) }, player.waves.length <= 1)}</div>
        </fieldset>`).join("")}
        ${action("添加波次", "wave-add", {}, player.waves.length >= 128)}
      </section>`;
    }
    fields.insertAdjacentHTML("beforeend", `<p class="modal-hint">同时订单上下限使用本页「基础信息」中的当前值，一并保存。每份菜单须选 1–256 道菜谱。</p>
      <div class="m-actions-row">${mBtnHtml("保存模式配置", "primary", { "data-action": "save" })}</div>`);
    fields.querySelectorAll<HTMLElement>("[data-menu]").forEach(filterMenu);
  }

  function filterMenu(menu: HTMLElement): void {
    const query = menu.querySelector<HTMLInputElement>("[data-recipe-search]")!.value.trim().toLowerCase();
    let visible = 0;
    menu.querySelectorAll<HTMLElement>("[data-recipe-row]").forEach(row => {
      row.hidden = !row.dataset.search!.includes(query);
      if (!row.hidden) visible++;
    });
    menu.querySelector("[data-recipe-result]")!.textContent = `匹配 ${visible} 道；搜索不会取消已选菜谱。`;
    menu.querySelector("[data-menu-count]")!.textContent = String(menu.querySelectorAll("[data-recipe]:checked").length);
  }

  // Flush every visible form before changing indexes or replacing DOM nodes.
  function collect(): void {
    const number = (key: string): number => fields.querySelector<HTMLInputElement>(`[data-number="${key}"]`)!.valueAsNumber;
    const menu = (row: Element): string[] => Array.from(row.querySelectorAll<HTMLInputElement>("[data-recipe]:checked"), input => input.value);
    fields.querySelectorAll<HTMLInputElement>("[data-recipe-search]").forEach(input => searches.set(input.dataset.recipeSearch!, input.value));
    if (config.gameMode === "Dynamic") {
      fields.querySelectorAll<HTMLElement>("[data-phase]").forEach(row => {
        const i = Number(row.dataset.phase);
        config.phases[i].durationSeconds = number(`phase-${i}`);
        config.phases[i].recipeGuids = menu(row);
      });
    } else {
      config.targetHealth = number("targetHealth");
      config.targetRepairSpeed = number("targetRepairSpeed");
      config.targetRepairThreshold = number("targetRepairThreshold");
      config.kitchenHealth = number("kitchenHealth");
      const player = config.hordeConfigs[playerIndex];
      player.targetRepairCostMax = number("repairCost");
      fields.querySelectorAll<HTMLElement>("[data-wave-row]").forEach(row => {
        const w = Number(row.dataset.waveRow);
        const wave = player.waves[w];
        wave.intervalSeconds = number(`wave-${w}`);
        wave.recipeGuids = menu(row);
        const events = Array.from(row.querySelectorAll<HTMLElement>("[data-event]"));
        wave.enemyGuids = events.map(event => event.querySelector<HTMLSelectElement>("[data-enemy]")!.value);
        wave.spawnTimes = events.map(event => number(`spawn-${w}-${event.dataset.event}`));
      });
    }
  }

  function validate(): void {
    const integer = (value: number, min: number, max: number): boolean => Number.isInteger(value) && value >= min && value <= max;
    if (!integer(config.minOrderCount, 1, 10) || !integer(config.maxOrderCount, config.minOrderCount, 10)) {
      throw new Error("基础信息的订单数量须满足 1 ≤ 最少订单 ≤ 最多订单 ≤ 10。");
    }
    for (const [name, value] of [["目标血量", config.targetHealth], ["厨房血量", config.kitchenHealth]] as const) {
      if (!integer(value, 1, 1000000)) throw new Error(`${name}须为 1–1000000 的整数。`);
    }
    for (const value of [config.targetRepairSpeed, config.targetRepairThreshold]) {
      if (!Number.isFinite(value) || value < 0 || value > 1000000) throw new Error("修门参数须在 0–1000000 之间。");
    }
    let total = 0;
    const checkMenu = (guids: string[], label: string): void => {
      if (!guids.length || guids.length > 256) throw new Error(`${label}：请选择 1–256 道菜谱。`);
      total += guids.length;
    };
    if (config.gameMode === "Dynamic") {
      if (config.phases.length < 2 || config.phases.length > 64) throw new Error("Dynamic 须有 2–64 个阶段。");
      config.phases.forEach((phase, i) => {
        if (!integer(phase.durationSeconds, 1, 86400)) throw new Error(`阶段 ${i + 1}：时长须为 1–86400 秒的整数。`);
        checkMenu(phase.recipeGuids, `阶段 ${i + 1}`);
      });
    } else {
      config.hordeConfigs.forEach((player, p) => {
        if (!integer(player.targetRepairCostMax, 0, 1000000)) throw new Error(`${p + 1}P：修门花费上限须为 0–1000000 的整数。`);
        if (!player.waves.length || player.waves.length > 128) throw new Error(`${p + 1}P：须有 1–128 波。`);
        player.waves.forEach((wave, w) => {
          const label = `${p + 1}P 第 ${w + 1} 波`;
          if (!integer(wave.intervalSeconds, 0, 86400)) throw new Error(`${label}：间隔须为 0–86400 秒的整数。`);
          checkMenu(wave.recipeGuids, label);
          if (!wave.enemyGuids.length || wave.enemyGuids.length > 256 || wave.enemyGuids.length !== wave.spawnTimes.length) throw new Error(`${label}：须有 1–256 条完整生成事件。`);
          wave.enemyGuids.forEach((guid, e) => {
            if (!enemyCatalog.has(guid)) throw new Error(`${label} 事件 ${e + 1}：请选择 common04 目录中的敌人。`);
            if (!integer(wave.spawnTimes[e], 0, 86400)) throw new Error(`${label} 事件 ${e + 1}：生成时间须为 0–86400 秒的整数。`);
          });
          total += wave.enemyGuids.length;
        });
      });
    }
    if (total > 32768) throw new Error("全部菜谱与敌人条目合计不能超过 32768 条。");
  }

  const newId = (prefix: string, rows: { id: string }[]): string => {
    let n = 1;
    while (rows.some(row => row.id === `${prefix}_${n}`)) n++;
    return `${prefix}_${n}`;
  };
  const move = <T,>(values: T[], index: number, next: number): void => {
    [values[index], values[next]] = [values[next], values[index]];
  };
  form.addEventListener("input", event => {
    const input = event.target;
    if (input instanceof HTMLInputElement && input.matches("[data-recipe-search]")) {
      searches.set(input.dataset.recipeSearch!, input.value);
      filterMenu(input.closest<HTMLElement>("[data-menu]")!);
    } else if (!(input instanceof HTMLInputElement && input.matches("[data-copy-player]"))) {
      markDirty();
      if (input instanceof HTMLInputElement && input.matches("[data-recipe]")) filterMenu(input.closest<HTMLElement>("[data-menu]")!);
    }
  });
  form.addEventListener("change", event => {
    if (event.target instanceof HTMLSelectElement) markDirty();
  });
  form.addEventListener("submit", event => event.preventDefault());
  fields.addEventListener("click", event => {
    const button = event.target instanceof Element ? event.target.closest<HTMLButtonElement>("[data-action]") : null;
    if (!button || button.disabled || saving) return;
    const command = button.dataset.action!;
    if (command.startsWith("recipes-")) {
      const menu = button.closest<HTMLElement>("[data-menu]")!;
      menu.querySelectorAll<HTMLInputElement>("[data-recipe-row]:not([hidden]) [data-recipe]").forEach(input => input.checked = command === "recipes-select");
      filterMenu(menu);
      markDirty();
      return;
    }
    collect();
    if (command === "save") {
      void save();
      return;
    }
    if (!form.reportValidity()) return;
    const index = Number(button.dataset.index);
    const waveIndex = Number(button.dataset.wave);
    if (command === "player") {
      playerIndex = Number(button.dataset.player);
      draw();
      return;
    }
    if (command === "player-copy") {
      const targets = Array.from(fields.querySelectorAll<HTMLInputElement>("[data-copy-player]:checked"), input => Number(input.dataset.copyPlayer));
      if (!targets.length) return say("请先选择要覆盖的目标人数。", true);
      if (!confirm(`将当前 ${playerIndex + 1}P 的完整配置覆盖到 ${targets.map(p => `${p + 1}P`).join("、")}？`)) return;
      targets.forEach(p => config.hordeConfigs[p] = JSON.parse(JSON.stringify(config.hordeConfigs[playerIndex])));
    } else if (command === "phase-add") {
      config.phases.push({ id: newId("phase", config.phases), durationSeconds: 240, recipeGuids: [] });
    } else if (command === "wave-add") {
      const waves = config.hordeConfigs[playerIndex].waves;
      waves.push({ id: newId("wave", waves), intervalSeconds: 5, recipeGuids: [], enemyGuids: [], spawnTimes: [] });
    } else if (command === "event-add") {
      const wave = config.hordeConfigs[playerIndex].waves[waveIndex];
      wave.enemyGuids.push(enemies[0].guid);
      wave.spawnTimes.push(wave.spawnTimes[wave.spawnTimes.length - 1] ?? 0);
    } else {
      const rows = command.startsWith("phase-") ? config.phases : config.hordeConfigs[playerIndex].waves;
      if (command.startsWith("event-")) {
        const wave = config.hordeConfigs[playerIndex].waves[waveIndex];
        if (command.endsWith("remove")) {
          wave.enemyGuids.splice(index, 1);
          wave.spawnTimes.splice(index, 1);
        } else {
          const next = index + (command.endsWith("up") ? -1 : 1);
          move(wave.enemyGuids, index, next);
          move(wave.spawnTimes, index, next);
        }
      } else if (command.endsWith("remove")) rows.splice(index, 1);
      else {
        const next = index + (command.endsWith("up") ? -1 : 1);
        if (command.startsWith("phase-")) move(config.phases, index, next);
        else move(config.hordeConfigs[playerIndex].waves, index, next);
      }
    }
    markDirty();
    draw();
  });

  async function save(): Promise<void> {
    if (saving || !form.reportValidity()) return;
    try {
      const min = page.querySelector<HTMLInputElement>("#cfg-minOrderCount, #f-minOrderCount");
      const max = page.querySelector<HTMLInputElement>("#cfg-maxOrderCount, #f-maxOrderCount");
      config.minOrderCount = min ? min.valueAsNumber : detail.minOrderCount;
      config.maxOrderCount = max ? max.valueAsNumber : detail.maxOrderCount;
      validate();
      saving = true;
      updateSwitch();
      fields.disabled = true;
      say("正在保存全部模式配置并同步原生资产…");
      await updateLevelModeConfig(config);
      const saved = await fetchLevelModeConfig(assetPath);
      for (const key of ["gameMode", "phases", "hordeConfigs", "targetHealth", "targetRepairSpeed", "targetRepairThreshold", "kitchenHealth", "minOrderCount", "maxOrderCount"] as const) {
        if (key === "targetRepairSpeed" || key === "targetRepairThreshold") {
          // Unity stores float32 and may emit only seven significant JSON digits.
          const actual = saved[key], expected = config[key];
          const expectedFloat = Math.fround(expected);
          if (Number.isFinite(actual) && Number.isFinite(expected)
            && Math.abs(actual - expectedFloat) <= Math.max(2 ** -149, Math.abs(expectedFloat) * 1e-6)) continue;
        }
        if (JSON.stringify(saved[key]) !== JSON.stringify(config[key])) throw new Error("保存已应答，但模式配置回读不一致，请重新保存或重新加载核实。");
      }
      detail.minOrderCount = config.minOrderCount;
      detail.maxOrderCount = config.maxOrderCount;
      detail.targetHealth = config.targetHealth;
      detail.targetRepairSpeed = config.targetRepairSpeed;
      detail.targetRepairThreshold = config.targetRepairThreshold;
      detail.kitchenHealth = config.kitchenHealth;
      if (config.gameMode === "Horde") detail.configs.forEach((p, i) => p.targetRepairCostMax = config.hordeConfigs[i].targetRepairCostMax);
      for (const input of [min, max]) if (input) initialValues.set(input, inputValue(input));
      dirty = false;
      say("模式配置已保存并回读确认，原生资产已同步。可继续编辑并再次保存。");
    } catch (error) {
      say(`保存失败：${error instanceof Error ? error.message : error}。当前编辑已保留，可修正后再次保存。`, true);
    } finally {
      saving = false;
      fields.disabled = false;
      updateSwitch();
    }
  }
  draw();
}
