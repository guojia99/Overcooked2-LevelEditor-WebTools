import * as api from "./api";
import type {
  AudioDirectoryEntry,
  DeathEffectEntry,
  LevelDetail,
  LevelSetInfo,
  LevelSummary,
  MusicEntry,
  PerPlayerConfig,
  RecipeEntry,
} from "./types";
import { closeModal, openModal, openRecipePicker } from "./modals";
import { showBusy, hideBusy } from "./busy";

const TARGET_SCENE_KEY = "layoutTargetScene";

export function goLayout(sceneAssetPath?: string): void {
  if (sceneAssetPath) sessionStorage.setItem(TARGET_SCENE_KEY, sceneAssetPath);
  else sessionStorage.removeItem(TARGET_SCENE_KEY);
  location.hash = "#/layout";
  location.reload();
}

export function goManage(): void {
  location.hash = "#/manage";
  location.reload();
}

export function consumeTargetScene(): string | null {
  const v = sessionStorage.getItem(TARGET_SCENE_KEY);
  if (v) sessionStorage.removeItem(TARGET_SCENE_KEY);
  return v;
}

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function setStatus(msg: string, ok = true): void {
  const el = document.getElementById("m-status");
  if (!el) return;
  el.textContent = msg;
  el.classList.toggle("err", !ok);
  el.classList.toggle("ok", ok && msg.length > 0);
}

function setBusy(msg: string): void {
  const el = document.getElementById("manage-content");
  if (el) el.innerHTML = `<p class="muted">${esc(msg)}</p>`;
  setStatus(msg);
}

function showError(e: unknown): void {
  const msg = e instanceof Error ? e.message : String(e);
  setStatus(msg, false);
  const el = document.getElementById("manage-content");
  if (el) el.innerHTML = `<div class="m-block"><h3>出错</h3><p>${esc(msg)}</p></div>`;
}

function shell(app: HTMLElement, title: string, backLabel?: string, onBack?: () => void): HTMLElement {
  app.innerHTML = `
    <div class="manage-bar">
      ${backLabel ? `<button class="m-btn" id="m-back">← ${esc(backLabel)}</button>` : ""}
      <h1 class="m-title">${esc(title)}</h1>
      <span class="status" id="m-status"></span>
      <span style="flex:1"></span>
      <button class="m-btn" id="m-reload" title="触发 Unity Reload Pseudo Assets">↻ Reload</button>
      <button class="m-btn" id="m-layout">布局编辑器</button>
    </div>
    <div class="manage-content" id="manage-content"></div>
  `;
  const back = document.getElementById("m-back");
  if (back && onBack) back.addEventListener("click", onBack);
  document.getElementById("m-reload")?.addEventListener("click", async () => {
    try {
      await api.reloadPseudo();
      setStatus("已触发 Unity Reload");
    } catch (e) {
      setStatus((e as Error).message, false);
    }
  });
  document.getElementById("m-layout")?.addEventListener("click", () => goLayout());
  return document.getElementById("manage-content")!;
}

export async function renderManageView(app: HTMLElement): Promise<void> {
  await renderSetList(app);
}

// ==================== Set list ====================

async function renderSetList(app: HTMLElement): Promise<void> {
  const content = shell(app, "关卡集管理");
  setBusy("加载关卡集…");
  let sets: LevelSetInfo[] = [];
  try {
    sets = await api.fetchSets();
  } catch (e) {
    showError(e);
    return;
  }
  setStatus(`共 ${sets.length} 个关卡集`);

  const cards = sets
    .map(
      (s) => `
      <div class="m-card">
        <h3>${esc(s.levelSetNameZH || s.setName)} <span class="muted">(${esc(s.levelSetName || s.setName)})</span></h3>
        <div class="m-meta">
          作者：${esc(s.author || "—")}<br>
          版本：${esc(s.version || "—")} · 关卡数：${s.levelCount}<br>
          <span class="muted">${esc(s.setName)}</span>
        </div>
        <div class="m-actions">
          <button class="m-btn primary" data-open="${esc(s.setName)}">打开</button>
          <button class="m-btn" data-edit="${esc(s.setName)}">编辑信息</button>
        </div>
      </div>`
    )
    .join("");

  content.innerHTML = `
    <div class="m-actions-row">
      <button class="m-btn primary" id="new-set">+ 新建关卡集</button>
    </div>
    <div class="m-section-title">关卡集列表</div>
    <div class="m-grid">${cards || '<p class="muted">暂无关卡集</p>'}</div>
  `;

  document.getElementById("new-set")?.addEventListener("click", () => openCreateSetModal(app));
  content.querySelectorAll<HTMLButtonElement>("[data-open]").forEach((b) =>
    b.addEventListener("click", () => void renderLevelList(app, b.dataset.open!))
  );
  const setMap = new Map(sets.map((s) => [s.setName, s]));
  content.querySelectorAll<HTMLButtonElement>("[data-edit]").forEach((b) =>
    b.addEventListener("click", () => {
      const s = setMap.get(b.dataset.edit!);
      if (s) openEditSetModal(app, s);
    })
  );
}

function openCreateSetModal(app: HTMLElement): void {
  openModal(
    "新建关卡集",
    `
    <label class="m-field">关卡集标识（目录名，仅字母/数字/下划线）<input type="text" id="set-name" placeholder="my_set"></label>
    <label class="m-field">英文名 levelSetName<input type="text" id="set-en" placeholder="My Set"></label>
    <label class="m-field">中文名 levelSetNameZH<input type="text" id="set-zh" placeholder="我的关卡集"></label>
    <label class="m-field">作者 author<input type="text" id="set-author"></label>
    <p class="modal-hint">将在 Assets/LevelSets/&lt;标识&gt;/ 下创建 data/、scenes/ 目录与 LevelSetInfo.asset。</p>
    `,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>创建</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", async () => {
    try {
      const setName = (document.getElementById("set-name") as HTMLInputElement).value.trim();
      if (!setName) return setStatus("请填写关卡集标识", false);
      showBusy("创建关卡集…");
      await api.createSet({
        setName,
        levelSetName: (document.getElementById("set-en") as HTMLInputElement).value.trim(),
        levelSetNameZH: (document.getElementById("set-zh") as HTMLInputElement).value.trim(),
        author: (document.getElementById("set-author") as HTMLInputElement).value.trim(),
      });
      closeModal();
      setStatus("已创建关卡集");
      await renderSetList(app);
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });
}

function openEditSetModal(app: HTMLElement, s: LevelSetInfo): void {
  openModal(
    `编辑关卡集 · ${s.setName}`,
    `
    <label class="m-field">英文名 levelSetName<input type="text" id="se-en" value="${esc(s.levelSetName)}"></label>
    <label class="m-field">中文名 levelSetNameZH<input type="text" id="se-zh" value="${esc(s.levelSetNameZH)}"></label>
    <label class="m-field">作者 author<input type="text" id="se-author" value="${esc(s.author)}"></label>
    <label class="m-field">版本 version<input type="text" id="se-version" value="${esc(s.version)}"></label>
    <p class="modal-hint">修改 version 会自动重算 uid（街机大厅检索用）。关卡集不可删除。</p>
    `,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>保存</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", async () => {
    try {
      showBusy("保存关卡集信息…");
      await api.updateSetInfo({
        setName: s.setName,
        levelSetName: (document.getElementById("se-en") as HTMLInputElement).value.trim(),
        levelSetNameZH: (document.getElementById("se-zh") as HTMLInputElement).value.trim(),
        author: (document.getElementById("se-author") as HTMLInputElement).value.trim(),
        version: (document.getElementById("se-version") as HTMLInputElement).value.trim(),
      });
      closeModal();
      setStatus("已保存关卡集信息");
      await renderSetList(app);
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });
}

// ==================== Level list ====================

async function renderLevelList(app: HTMLElement, setName: string): Promise<void> {
  const content = shell(app, `关卡列表 · ${setName}`, "返回关卡集", () => void renderSetList(app));
  setBusy(`加载 ${setName} 的关卡…`);
  let levels: LevelSummary[] = [];
  try {
    levels = await api.fetchLevels(setName);
  } catch (e) {
    showError(e);
    return;
  }
  setStatus(`共 ${levels.length} 个关卡`);

  const rows = levels
    .map((lv, idx) => {
      const id = lv.dataDir.split("/").pop() || `level${idx}`;
      return `
      <div class="m-row">
        <div class="m-row-main">
          <div class="m-row-title">${esc(lv.levelNameZH || lv.levelName || id)}</div>
          <div class="m-row-sub">
            ${lv.hasScreenshot ? '<span class="m-badge ok">截图</span>' : '<span class="m-badge">无截图</span>'}
            ${lv.hasScene ? '<span class="m-badge ok">场景</span>' : '<span class="m-badge warn">缺场景</span>'}
            ${esc(lv.sceneName)} · <span class="muted">${esc(id)}</span>
          </div>
        </div>
        <button class="m-btn primary" data-edit="${esc(lv.assetPath)}">编辑</button>
        <button class="m-btn" data-layout="${esc(lv.sceneAssetPath)}">打开布局</button>
        <button class="m-btn danger" data-del="${esc(id)}">删除</button>
      </div>`;
    })
    .join("");

  content.innerHTML = `
    <div class="m-actions-row">
      <button class="m-btn primary" id="new-level">+ 新建关卡</button>
      <span class="muted">当前关卡集：<b>${esc(setName)}</b></span>
    </div>
    <div class="m-section-title">关卡</div>
    <div class="m-list">${rows || '<p class="muted">暂无关卡</p>'}</div>
  `;

  document.getElementById("new-level")?.addEventListener("click", () => openCreateLevelModal(app, setName));
  content.querySelectorAll<HTMLButtonElement>("[data-edit]").forEach((b) =>
    b.addEventListener("click", () => void renderLevelDetail(app, setName, b.dataset.edit!))
  );
  content.querySelectorAll<HTMLButtonElement>("[data-layout]").forEach((b) =>
    b.addEventListener("click", () => goLayout(b.dataset.layout!))
  );
  content.querySelectorAll<HTMLButtonElement>("[data-del]").forEach((b) =>
    b.addEventListener("click", () => confirmDeleteLevel(app, setName, b.dataset.del!))
  );
}

function openCreateLevelModal(app: HTMLElement, setName: string): void {
  openModal(
    `新建关卡 · ${setName}`,
    `
    <label class="m-field">关卡标识（仅字母/数字/下划线，用于目录/场景名 s_&lt;标识&gt;）<input type="text" id="lv-id" placeholder="level_1"></label>
    <label class="m-field">英文名 levelName<input type="text" id="lv-en" placeholder="Level 1"></label>
    <label class="m-field">中文名 levelNameZH<input type="text" id="lv-zh" placeholder="第一关"></label>
    <p class="modal-hint">将自动生成 4 份人数配置（config_1p~4p，复制模板默认值）、LevelInfoSO，并复制模板场景 s_template 到 scenes/。</p>
    `,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>创建</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", async () => {
    try {
      const levelId = (document.getElementById("lv-id") as HTMLInputElement).value.trim();
      if (!levelId) return setStatus("请填写关卡标识", false);
      showBusy("创建关卡（生成配置、复制场景）…");
      await api.createLevel({
        setName,
        levelId,
        levelName: (document.getElementById("lv-en") as HTMLInputElement).value.trim(),
        levelNameZH: (document.getElementById("lv-zh") as HTMLInputElement).value.trim(),
      });
      closeModal();
      setStatus("已创建关卡");
      await renderLevelList(app, setName);
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });
}

async function confirmDeleteLevel(app: HTMLElement, setName: string, levelId: string): Promise<void> {
  showBusy("读取待删文件…");
  let paths: string[] = [];
  try {
    paths = await api.fetchDeletePreview(setName, levelId);
  } catch (e) {
    setStatus((e as Error).message, false);
  } finally {
    hideBusy();
  }

  const fileList = paths.length
    ? paths.map((p) => `<div class="del-file">${esc(p)}</div>`).join("")
    : `<div class="muted">（无文件，可能已不存在）</div>`;

  openModal(
    `删除关卡 · ${esc(levelId)}`,
    `<p>将永久删除以下 <b>${paths.length}</b> 个文件/资源（含场景、LevelInfo、人数配置及关卡目录内自定义菜谱/模型），且<b>不可恢复</b>：</p>
     <div class="del-file-list">${fileList}</div>
     <p class="modal-hint">关卡集本身不会被删除。如该关卡已分配 AssetBundle，删除后请重新构建 AssetBundle。</p>`,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn danger" data-ok>确认删除</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", async () => {
    showBusy("删除中…");
    try {
      await api.deleteLevel(setName, levelId);
      closeModal();
      setStatus("已删除关卡");
      await renderLevelList(app, setName);
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });
}

// ==================== Level detail ====================

async function renderLevelDetail(app: HTMLElement, setName: string, assetPath: string): Promise<void> {
  const content = shell(app, `关卡编辑 · ${setName}`, "返回关卡列表", () => void renderLevelList(app, setName));
  setBusy("加载关卡数据…");
  let detail: LevelDetail;
  try {
    detail = await api.fetchLevelDetail(assetPath);
  } catch (e) {
    showError(e);
    return;
  }
  if (!detail) {
    content.innerHTML = '<p class="muted">未找到该关卡。</p>';
    return;
  }
  setStatus(`已加载：${detail.levelName}`);

  const deps = (detail.dependencies || []).join("\n");
  content.innerHTML = `
    <div class="m-actions-row">
      <button class="m-btn" id="btn-config">人数配置 (1P/2P/3P/4P)</button>
      <button class="m-btn" id="btn-audio">音频配置</button>
      <button class="m-btn" id="btn-recipes">菜谱</button>
      <button class="m-btn" id="btn-layout">打开俯视图编排</button>
    </div>

    <div class="m-block">
      <h3>基础信息 (LevelInfoSO)</h3>
      <div class="m-form">
        <label class="m-field">英文名 levelName<input type="text" id="f-levelName" value="${esc(detail.levelName)}"></label>
        <label class="m-field">中文名 levelNameZH<input type="text" id="f-levelNameZH" value="${esc(detail.levelNameZH)}"></label>
        <label class="m-field">场景名 sceneName<input type="text" id="f-sceneName" value="${esc(detail.sceneName)}"></label>
        <label class="m-field">调试菜谱数 debugRecipeCount<input type="number" id="f-debugRecipeCount" value="${detail.debugRecipeCount}"></label>
        <label class="m-field">截图
          <span class="muted">${detail.hasScreenshot ? "已设置 screenshot" : "未设置（暂不支持上传）"}</span>
        </label>
        <label class="m-field">动态父挂载 disableDynamicParenting
          <label class="modal-check"><input type="checkbox" id="f-disableDynamicParenting" ${detail.disableDynamicParenting ? "checked" : ""}> 勾选=禁用（含移动/升降平台关卡应取消）</label>
        </label>
        <label class="m-field">依赖 bundles（每行一个，如 bundle47 / bundle11）<textarea id="f-dependencies">${esc(deps)}</textarea></label>
      </div>
      <div class="m-actions-row">
        <button class="m-btn primary" id="save-info">保存基础信息</button>
      </div>
    </div>

    <div class="m-block">
      <h3>音频预览 (场景 PseudoPrefabManagerStub)</h3>
      <div class="m-meta" id="audio-preview"></div>
    </div>
  `;

  renderAudioPreview(detail);
  wireDetailActions(app, setName, assetPath, detail);
}

function renderAudioPreview(detail: LevelDetail): void {
  const el = document.getElementById("audio-preview");
  if (!el) return;
  const a = detail.audio;
  el.innerHTML = `
    BGM：${esc(a.inLevelMusicId || "—")}<br>
    氛围音：${a.ambiences?.length ? esc(a.ambiences.join(", ")) : "—"}<br>
    音效集：${a.audioDirectoryIds?.length ? esc(a.audioDirectoryIds.join(", ")) : "—"}<br>
    死亡特效：${esc(a.onDeathEffectId || "—")}
  `;
}

function wireDetailActions(app: HTMLElement, setName: string, assetPath: string, detail: LevelDetail): void {
  document.getElementById("save-info")?.addEventListener("click", async () => {
    try {
      const deps = (document.getElementById("f-dependencies") as HTMLTextAreaElement).value
        .split(/\r?\n/)
        .map((s) => s.trim())
        .filter(Boolean);
      showBusy("保存基础信息…");
      await api.updateLevelInfo({
        assetPath,
        levelName: (document.getElementById("f-levelName") as HTMLInputElement).value.trim(),
        levelNameZH: (document.getElementById("f-levelNameZH") as HTMLInputElement).value.trim(),
        sceneName: (document.getElementById("f-sceneName") as HTMLInputElement).value.trim(),
        debugRecipeCount: Number((document.getElementById("f-debugRecipeCount") as HTMLInputElement).value || 0),
        disableDynamicParenting: (document.getElementById("f-disableDynamicParenting") as HTMLInputElement).checked,
        dependencies: deps,
      });
      setStatus("基础信息已保存（已 reload）");
      await renderLevelDetail(app, setName, assetPath);
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });

  document.getElementById("btn-config")?.addEventListener("click", () =>
    openConfigTabsModal(detail, () => void renderLevelDetail(app, setName, assetPath))
  );
  document.getElementById("btn-audio")?.addEventListener("click", () =>
    void openAudioModal(detail, () => void renderLevelDetail(app, setName, assetPath))
  );
  document.getElementById("btn-recipes")?.addEventListener("click", () =>
    void openRecipesForLevel(setName, detail)
  );
  document.getElementById("btn-layout")?.addEventListener("click", () => goLayout(detail.sceneAssetPath));
}

// ==================== Config tab modal (1P/2P/3P/4P) ====================

const CONFIG_FIELDS: Array<[keyof PerPlayerConfig, string, string]> = [
  ["orderLifeTime", "订单超时(秒)", "1"],
  ["timeBetweenOrders", "订单间隔(秒)", "1"],
  ["plateReturnTime", "回盘间隔(秒)", "1"],
  ["roundTime", "关卡时长(秒)", "1"],
  ["survivalTimeMultiplier", "生存倍率", "0.1"],
  ["oneStarScore", "1星分数", "1"],
  ["twoStarScore", "2星分数", "1"],
  ["threeStarScore", "3星分数", "1"],
  ["fourStarScore", "4星分数", "1"],
];

const PLAYER_TABS = ["1p", "2p", "3p", "4p"] as const;
const PLAYER_LABELS = ["单人 1P", "双人 2P", "三人 3P", "四人 4P"];

function openConfigTabsModal(detail: LevelDetail, onSaved: () => void): void {
  const tabBtns = PLAYER_TABS.map(
    (t, i) => `<button type="button" class="cfg-tab-btn ${i === 0 ? "active" : ""}" data-tab="${t}">${PLAYER_LABELS[i]}</button>`
  ).join("");

  const panes = PLAYER_TABS.map((t, ti) => {
    const cfg = detail.configs[ti] ?? ({ exists: false } as PerPlayerConfig);
    const inputs = CONFIG_FIELDS.map(([key, label, step]) => {
      const val = (cfg[key] as number) ?? 0;
      return `<label class="m-field">${esc(label)}<input type="number" step="${step}" id="cfg-${t}-${key}" value="${val}"></label>`;
    }).join("");
    return `<div class="cfg-pane" data-pane="${t}" ${ti !== 0 ? 'style="display:none"' : ""}>${inputs}</div>`;
  }).join("");

  openModal(
    `人数配置 · ${detail.levelName || detail.levelNameZH}`,
    `<div class="cfg-tabs">${tabBtns}</div>${panes}<p class="modal-hint">切换各 Tab 修改后，点击保存将<b>一次性写入全部 1P/2P/3P/4P 配置</b>。</p>`,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>保存全部</button>`
  );
  document.querySelector(".modal-panel")?.classList.add("wide");

  document.querySelectorAll<HTMLButtonElement>(".cfg-tab-btn").forEach((btn) =>
    btn.addEventListener("click", () => {
      document.querySelectorAll(".cfg-tab-btn").forEach((b) => b.classList.toggle("active", b === btn));
      document.querySelectorAll<HTMLElement>(".cfg-pane").forEach((p) => {
        p.style.display = p.dataset.pane === btn.dataset.tab ? "" : "none";
      });
    })
  );

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", async () => {
    try {
      const build = (t: string): PerPlayerConfig => {
        const getNum = (key: string) =>
          parseInt((document.getElementById(`cfg-${t}-${key}`) as HTMLInputElement).value || "0", 10);
        const getFloat = (key: string) =>
          Number((document.getElementById(`cfg-${t}-${key}`) as HTMLInputElement).value || 0);
        return {
          exists: true,
          orderLifeTime: getNum("orderLifeTime"),
          timeBetweenOrders: getNum("timeBetweenOrders"),
          plateReturnTime: getNum("plateReturnTime"),
          roundTime: getNum("roundTime"),
          survivalTimeMultiplier: getFloat("survivalTimeMultiplier"),
          oneStarScore: getNum("oneStarScore"),
          twoStarScore: getNum("twoStarScore"),
          threeStarScore: getNum("threeStarScore"),
          fourStarScore: getNum("fourStarScore"),
        };
      };
      showBusy("保存人数配置…");
      await api.updateLevelConfig({
        assetPath: detail.levelInfoAssetPath,
        config_1p: build("1p"),
        config_2p: build("2p"),
        config_3p: build("3p"),
        config_4p: build("4p"),
      });
      closeModal();
      setStatus("人数配置已保存（已 reload）");
      onSaved();
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });
}

// ==================== Audio modal ====================

async function openAudioModal(detail: LevelDetail, onSaved: () => void): Promise<void> {
  if (!detail.sceneAssetPath) {
    setStatus("该关卡缺少场景路径，无法编辑音频", false);
    return;
  }
  setStatus("加载音频资源…");
  let music: MusicEntry[];
  let dirs: AudioDirectoryEntry[];
  let ambiances: string[];
  let deaths: DeathEffectEntry[];
  try {
    [music, dirs, ambiances, deaths] = await Promise.all([
      api.fetchMusicCatalog(),
      api.fetchAudioDirectoryCatalog(),
      api.fetchAmbiences(),
      api.fetchDeathEffects(),
    ]);
  } catch (e) {
    showError(e);
    return;
  }

  const cur = detail.audio;
  const musicOpts =
    `<option value="" ${!cur.inLevelMusicGuid ? "selected" : ""}>(无)</option>` +
    music
      .map(
        (m) =>
          `<option value="${esc(m.guid)}" ${m.guid === cur.inLevelMusicGuid ? "selected" : ""}>${esc(m.id)}${m.bundleName ? ` (${esc(m.bundleName)})` : ""}</option>`
      )
      .join("");
  const deathOpts =
    `<option value="" ${!cur.onDeathEffectGuid ? "selected" : ""}>(无)</option>` +
    deaths
      .map(
        (d) =>
          `<option value="${esc(d.guid)}" ${d.guid === cur.onDeathEffectGuid ? "selected" : ""}>${esc(d.id)}</option>`
      )
      .join("");

  const ambSet = new Set(cur.ambiences || []);
  const ambChecks = ambiances
    .map((n) => `<label class="modal-check"><input type="checkbox" value="${esc(n)}" data-amb ${ambSet.has(n) ? "checked" : ""}> ${esc(n)}</label>`)
    .join("");
  const dirSet = new Set(cur.audioDirectoryGuids || []);
  const dirChecks = dirs
    .map(
      (d) =>
        `<label class="modal-check"><input type="checkbox" value="${esc(d.guid)}" data-dir ${dirSet.has(d.guid) ? "checked" : ""}> ${esc(d.id)}${d.bundleName ? ` <span class="muted">(${esc(d.bundleName)})</span>` : ""}</label>`
    )
    .join("");

  openModal(
    `音频配置 · ${detail.levelName || detail.levelNameZH}`,
    `
    <p class="modal-hint">这些字段写入场景的 <code>PseudoPrefabManagerStub</code>。保存后会自动打开/保存该场景并触发 Reload。许多 BGM 所在 bundle 不在默认加载列表，需在基础信息的 dependencies 额外添加（如木筏 BGM 需 bundle11）。</p>
    <label class="m-field">关卡 BGM (InLevelMusicSO)<select id="au-music">${musicOpts}</select></label>
    <label class="m-field">死亡特效 (OnDeathEffectSO)<select id="au-death">${deathOpts}</select></label>
    <p class="modal-hint">氛围音 InLevelAmbiences</p>
    <div class="modal-scroll">${ambChecks || '<p class="muted">无</p>'}</div>
    <p class="modal-hint" style="margin-top:8px">音效集 AudioDirectorySOs</p>
    <div class="modal-scroll">${dirChecks || '<p class="muted">无</p>'}</div>
    `,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>保存</button>`
  );
  document.querySelector(".modal-panel")?.classList.add("wide");

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", async () => {
    try {
      const ambiences: string[] = [];
      document.querySelectorAll<HTMLInputElement>("[data-amb]:checked").forEach((el) => ambiences.push(el.value));
      const audioDirectoryGuids: string[] = [];
      document.querySelectorAll<HTMLInputElement>("[data-dir]:checked").forEach((el) => audioDirectoryGuids.push(el.value));
      showBusy("保存音频配置（打开并保存场景）…");
      await api.updateLevelAudio({
        sceneAssetPath: detail.sceneAssetPath,
        inLevelMusicGuid: (document.getElementById("au-music") as HTMLSelectElement).value,
        ambiences,
        audioDirectoryGuids,
        onDeathEffectGuid: (document.getElementById("au-death") as HTMLSelectElement).value,
      });
      closeModal();
      setStatus("音频配置已保存（已 reload）");
      onSaved();
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });
}

// ==================== Recipes ====================

async function openRecipesForLevel(setName: string, detail: LevelDetail): Promise<void> {
  if (!detail.sceneAssetPath) {
    setStatus("该关卡缺少场景路径", false);
    return;
  }
  try {
    const [recipes, level] = await Promise.all([
      api.fetchRecipeCatalog(setName),
      api.fetchLevelRecipes(detail.sceneAssetPath),
    ]);
    if (!level.levelInfoAssetPath) {
      setStatus("未找到该场景对应的 LevelInfoSO", false);
      return;
    }
    openRecipePicker(recipes as RecipeEntry[], level.recipeGuids ?? [], level.levelName, async (guids) => {
      await api.saveLevelRecipes(level.levelInfoAssetPath, guids);
      await api.reloadPseudo();
      setStatus("菜谱已写入并 reload");
    });
  } catch (e) {
    setStatus((e as Error).message, false);
  }
}
