import * as api from "./api";
import type {
  AudioDirectoryEntry,
  AudioKnowledge,
  BundleAnalysis,
  DeathEffectEntry,
  DirectoryEvent,
  LevelDetail,
  LevelSetInfo,
  LevelSummary,
  MusicEntry,
  PerPlayerConfig,
  RecipeEntry,
} from "./types";
import { closeModal, openModal } from "./modals";
import { showBusy, hideBusy } from "./busy";
import { navHtml, wireNav } from "./nav";
import { applyRatio, computeAutoScores, round5, RATIO_MAX, RATIO_MIN, RATIO_STEP } from "./autoScore";

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

const IDENT_RE = /^[A-Za-z0-9_]+$/;

function wireIdentInput(id: string): void {
  const el = document.getElementById(id) as HTMLInputElement | null;
  el?.addEventListener("input", () => {
    const v = el.value.replace(/[^A-Za-z0-9_]/g, "");
    if (v !== el.value) el.value = v;
  });
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
  document.body.classList.add("manage-bg");
  app.innerHTML = `
    ${navHtml("manage")}
    <div class="manage-bar">
      ${backLabel ? `<button class="m-btn" id="m-back">← ${esc(backLabel)}</button>` : ""}
      <h1 class="m-title">${esc(title)}</h1>
      <span class="status" id="m-status"></span>
      <span style="flex:1"></span>
      <button class="m-btn" id="m-reload" title="触发 Unity Reload Pseudo Assets">↻ Reload</button>
    </div>
    <div class="manage-content" id="manage-content"></div>
  `;
  wireNav((target) => {
    if (target === "layout") goLayout();
    else if (target === "custom-recipes") {
      location.hash = "#/custom-recipes";
      location.reload();
    }
  });
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
          <button class="m-btn danger" data-del="${esc(s.setName)}">删除</button>
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
  content.querySelectorAll<HTMLButtonElement>("[data-del]").forEach((b) =>
    b.addEventListener("click", () => {
      const s = setMap.get(b.dataset.del!);
      if (s) confirmDeleteSet(app, s);
    })
  );
}

function confirmDeleteSet(app: HTMLElement, s: LevelSetInfo): void {
  const setName = s.setName;
  const display = s.levelSetNameZH || s.levelSetName || setName;
  openModal(
    `删除关卡集 · ${esc(display)}`,
    `<p>将永久删除关卡集 <b>${esc(display)}</b>（目录 <code>${esc(setName)}</code>）及其所有关卡、场景、资源与 AssetBundle 引用，且<b>不可恢复</b>。</p>
     <p class="modal-hint">为防止误删，请输入关卡集标识 <b>${esc(setName)}</b> 以确认：</p>
     <label class="m-field">确认标识 <input type="text" id="del-set-confirm" autocomplete="off" placeholder="${esc(setName)}"></label>`,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn danger" data-ok disabled>确认删除</button>`
  );
  const input = document.getElementById("del-set-confirm") as HTMLInputElement | null;
  const okBtn = document.querySelector("[data-ok]") as HTMLButtonElement | null;
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  const sync = () => {
    if (okBtn) okBtn.disabled = (input?.value.trim() ?? "") !== setName;
  };
  input?.addEventListener("input", sync);
  input?.addEventListener("change", sync);
  sync();
  okBtn?.addEventListener("click", async () => {
    if ((input?.value.trim() ?? "") !== setName) return;
    showBusy("删除关卡集…");
    try {
      await api.deleteSet(setName);
      closeModal();
      setStatus("已删除关卡集");
      await renderSetList(app);
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });
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
  wireIdentInput("set-name");
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", async () => {
    try {
      const setName = (document.getElementById("set-name") as HTMLInputElement).value.trim();
      if (!setName) return setStatus("请填写关卡集标识", false);
      if (!IDENT_RE.test(setName)) return setStatus("关卡集标识仅允许英文字母/数字/下划线", false);
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
    <p class="modal-hint">修改 version 会自动重算 uid（街机大厅检索用）。如需删除整个关卡集，请在列表卡片点击「删除」。</p>
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
    <p class="modal-hint">将自动生成 4 份分数配置（config_1p~4p，复制模板默认值）、LevelInfoSO，并复制模板场景 s_template 到 scenes/。</p>
    `,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>创建</button>`
  );
  wireIdentInput("lv-id");
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", async () => {
    try {
      const levelId = (document.getElementById("lv-id") as HTMLInputElement).value.trim();
      if (!levelId) return setStatus("请填写关卡标识", false);
      if (!IDENT_RE.test(levelId)) return setStatus("关卡标识仅允许英文字母/数字/下划线", false);
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
    `<p>将永久删除以下 <b>${paths.length}</b> 个文件/资源（含场景、LevelInfo、分数配置及关卡目录内自定义菜谱/模型），且<b>不可恢复</b>：</p>
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
      <button class="m-btn" id="btn-layout">打开关卡编辑器</button>
    </div>

    <div class="m-block">
      <h3>基础信息 (LevelInfoSO)</h3>
      <div class="m-form">
        <label class="m-field">英文名 levelName<input type="text" id="f-levelName" value="${esc(detail.levelName)}"></label>
        <label class="m-field">中文名 levelNameZH<input type="text" id="f-levelNameZH" value="${esc(detail.levelNameZH)}"></label>
        <label class="m-field">场景名 sceneName<input type="text" id="f-sceneName" value="${esc(detail.sceneName)}"></label>
        <label class="m-field">调试菜谱数 debugRecipeCount<input type="number" id="f-debugRecipeCount" value="${detail.debugRecipeCount}"></label>
        <label class="m-field">截图
          <span class="muted" id="ss-status">${detail.hasScreenshot ? "已设置 screenshot" : "未设置"}</span>
          <div class="m-actions-row" style="margin-top:4px">
            <input type="file" id="ss-file" accept="image/png,image/jpeg" style="display:none">
            <button type="button" class="m-btn small" id="ss-upload">上传截图</button>
            ${detail.hasScreenshot ? '<button type="button" class="m-btn small" id="ss-preview">预览</button>' : ""}
          </div>
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
  `;

  wireDetailActions(app, setName, assetPath, detail);
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

  document.getElementById("btn-layout")?.addEventListener("click", () => goLayout(detail.sceneAssetPath));

  const ssFile = document.getElementById("ss-file") as HTMLInputElement | null;
  const ssUpload = document.getElementById("ss-upload");
  const ssPreview = document.getElementById("ss-preview");
  const ssStatus = document.getElementById("ss-status");

  ssUpload?.addEventListener("click", () => ssFile?.click());

  ssFile?.addEventListener("change", async () => {
    const file = ssFile.files?.[0];
    if (!file) return;
    showBusy("上传截图…");
    try {
      const reader = new FileReader();
      await new Promise<void>((resolve, reject) => {
        reader.onload = () => resolve();
        reader.onerror = () => reject(new Error("读取文件失败"));
        reader.readAsDataURL(file);
      });
      const base64 = (reader.result as string).split(",")[1] || (reader.result as string);
      await api.uploadScreenshot(assetPath, file.name, base64);
      setStatus("截图已上传");
      await renderLevelDetail(app, setName, assetPath);
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });

  ssPreview?.addEventListener("click", async () => {
    try {
      // Fetch the level detail again to get screenshot info, then open in new tab
      // We don't have a direct URL for the sprite, but we can try to look it up
      setStatus("截图已保存在关卡数据目录中，可在 Unity 中查看。");
    } catch (e) {
      setStatus((e as Error).message, false);
    }
  });
}

// ==================== Config tab modal (1P/2P/3P/4P) ====================

const RHYTHM_FIELDS: Array<[keyof PerPlayerConfig, string, string]> = [
  ["orderLifeTime", "订单超时(秒)", "1"],
  ["timeBetweenOrders", "订单间隔(秒)", "1"],
  ["plateReturnTime", "回盘间隔(秒)", "1"],
  ["roundTime", "关卡时长(秒)", "1"],
  ["survivalTimeMultiplier", "生存倍率", "0.1"],
];

const STAR_FIELDS: Array<[keyof PerPlayerConfig, string]> = [
  ["oneStarScore", "1★"],
  ["twoStarScore", "2★"],
  ["threeStarScore", "3★"],
  ["fourStarScore", "4★"],
];

const PLAYER_TABS = ["1p", "2p", "3p", "4p"] as const;
const PLAYER_LABELS = ["单人 1P", "双人 2P", "三人 3P", "四人 4P"];
const PLAYER_ROW_LABELS = ["1P", "2P", "3P", "4P"];

export function openConfigTabsModal(detail: LevelDetail, setName: string, onSaved: () => void): void {
  const starHead = STAR_FIELDS.map(([, label]) => `<th>${label}</th>`).join("");
  const matrixRows = PLAYER_TABS.map((t, ti) => {
    const cfg = detail.configs[ti] ?? ({ exists: false } as PerPlayerConfig);
    const cells = STAR_FIELDS.map(([key]) => {
      const val = (cfg[key] as number) ?? 0;
      return `<td><input type="number" step="5" min="0" class="cfg-star-input" id="cfg-${t}-${key}" value="${val}"></td>`;
    }).join("");
    return `<tr>
      <td class="cfg-row-label">${PLAYER_ROW_LABELS[ti]}</td>${cells}
      <td class="cfg-ratio-cell">
        <input type="range" id="cfg-ratio-${t}" min="${RATIO_MIN}" max="${RATIO_MAX}" step="${RATIO_STEP}" value="1">
        <span class="cfg-ratio-val" id="cfg-ratio-val-${t}">1.0x</span>
      </td>
    </tr>`;
  }).join("");

  const tabBtns = PLAYER_TABS.map(
    (t, i) => `<button type="button" class="cfg-tab-btn ${i === 0 ? "active" : ""}" data-tab="${t}">${PLAYER_LABELS[i]}</button>`
  ).join("");

  const panes = PLAYER_TABS.map((t, ti) => {
    const cfg = detail.configs[ti] ?? ({ exists: false } as PerPlayerConfig);
    const inputs = RHYTHM_FIELDS.map(([key, label, step]) => {
      const val = (cfg[key] as number) ?? 0;
      return `<label class="m-field">${esc(label)}<input type="number" step="${step}" id="cfg-${t}-${key}" value="${val}"></label>`;
    }).join("");
    return `<div class="cfg-pane" data-pane="${t}" ${ti !== 0 ? 'style="display:none"' : ""}>${inputs}</div>`;
  }).join("");

  openModal(
    `分数配置 · ${detail.levelName || detail.levelNameZH}`,
    `<div class="cfg-ai-bar">
        <button type="button" class="m-btn primary" id="cfg-ai-fill">✨ 一键定分</button>
     </div>
     <table class="cfg-matrix">
       <thead><tr><th>人数</th>${starHead}<th>难度系数</th></tr></thead>
       <tbody>${matrixRows}</tbody>
     </table>
     <div id="cfg-ai-detail"></div>
     <p class="modal-hint">节奏参数</p>
     <div class="cfg-tabs">${tabBtns}</div>${panes}`,
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

  const baseStars: number[][] = PLAYER_TABS.map((_t, ti) => {
    const cfg = detail.configs[ti] ?? ({ exists: false } as PerPlayerConfig);
    return STAR_FIELDS.map(([key]) => (cfg[key] as number) ?? 0);
  });

  const starInput = (t: string, j: number) =>
    document.getElementById(`cfg-${t}-${STAR_FIELDS[j][0]}`) as HTMLInputElement;
  const ratioSlider = (t: string) => document.getElementById(`cfg-ratio-${t}`) as HTMLInputElement;
  const ratioLabel = (t: string) => document.getElementById(`cfg-ratio-val-${t}`)!;
  const resetRatio = (t: string) => {
    ratioSlider(t).value = "1";
    ratioLabel(t).textContent = "1.0x";
  };

  PLAYER_TABS.forEach((t, ti) => {
    ratioSlider(t).addEventListener("input", () => {
      const ratio = parseFloat(ratioSlider(t).value);
      ratioLabel(t).textContent = ratio.toFixed(1) + "x";
      applyRatio(baseStars[ti], ratio).forEach((v, j) => {
        starInput(t, j).value = String(v);
      });
    });
    STAR_FIELDS.forEach(([,], j) => {
      starInput(t, j).addEventListener("change", () => {
        const v = round5(parseInt(starInput(t, j).value || "0", 10) || 0);
        starInput(t, j).value = String(v);
        baseStars[ti] = STAR_FIELDS.map((_, k) => parseInt(starInput(t, k).value || "0", 10) || 0);
        resetRatio(t);
      });
    });
  });

  document.getElementById("cfg-ai-fill")?.addEventListener("click", async () => {
    const detailEl = document.getElementById("cfg-ai-detail")!;
    try {
      showBusy("推算星级分数…");
      const [catalog, level] = await Promise.all([
        api.fetchRecipeCatalog(setName),
        api.fetchLevelRecipes(detail.sceneAssetPath),
      ]);
      const byGuid = new Map(catalog.map((r) => [r.guid, r]));
      const selected = (level.recipeGuids ?? [])
        .map((g) => byGuid.get(g))
        .filter((r): r is RecipeEntry => !!r);
      if (!selected.length) {
        detailEl.innerHTML = `<p class="modal-hint err">该关卡尚未配置菜谱，请先在详情页点击「菜谱…」选择菜谱。</p>`;
        return;
      }
      const roundTimes = PLAYER_TABS.map(
        (t) => parseInt((document.getElementById(`cfg-${t}-roundTime`) as HTMLInputElement).value || "240", 10) || 240
      );
      const result = computeAutoScores(selected, roundTimes);
      if (!result) {
        detailEl.innerHTML = `<p class="modal-hint err">所选菜谱缺少价格信息，无法推算。</p>`;
        return;
      }
      PLAYER_TABS.forEach((t, ti) => {
        baseStars[ti] = result.stars[ti].slice();
        result.stars[ti].forEach((v, j) => {
          starInput(t, j).value = String(v);
        });
        resetRatio(t);
      });
      const rows = result.details
        .map(
          (d) =>
            `<tr><td>${esc(d.name)}</td><td>${esc(d.groupLabel)}</td><td>${d.ingredientCount}</td><td>${d.cookingStepCount > 0 ? "✓" : "—"}</td><td>${d.score}</td><td>${d.timeSec.toFixed(0)}s</td></tr>`
        )
        .join("");
      detailEl.innerHTML = `
        <p class="modal-hint ok">已按 ${result.details.length} 道菜谱推算：平均单菜约 ${result.avgTimeSec.toFixed(0)} 秒 · 平均菜价 ${result.avgPrice.toFixed(0)} 分</p>
        <table class="cfg-ai-table">
          <thead><tr><th>菜谱</th><th>来源</th><th>食材数</th><th>需烹饪</th><th>菜价</th><th>估时</th></tr></thead>
          <tbody>${rows}</tbody>
        </table>`;
    } catch (e) {
      detailEl.innerHTML = `<p class="modal-hint err">${esc((e as Error).message)}</p>`;
    } finally {
      hideBusy();
    }
  });

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
          oneStarScore: round5(getNum("oneStarScore")),
          twoStarScore: round5(getNum("twoStarScore")),
          threeStarScore: round5(getNum("threeStarScore")),
          fourStarScore: round5(getNum("fourStarScore")),
        };
      };
      showBusy("保存分数配置…");
      await api.updateLevelConfig({
        assetPath: detail.levelInfoAssetPath,
        config_1p: build("1p"),
        config_2p: build("2p"),
        config_3p: build("3p"),
        config_4p: build("4p"),
      });
      closeModal();
      setStatus("分数配置已保存（已 reload）");
      onSaved();
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });
}

// ==================== Audio modal ====================

export interface ThemeSignals {
  /** art theme keys present in the scene (e.g. "wizard", "space", "raft"). */
  themes: Set<string>;
  /** true when a raft/water floor is present. */
  raft: boolean;
  /** "" | "water" | "goo" — inferred death theme. */
  deathTheme: string;
}

interface AudioRec {
  dirIds: Set<string>;
  ambiences: Set<string>;
  bgmId: string;
  deathTheme: string;
}

function detectAudioRecommendations(
  signals: ThemeSignals,
  knowledge: AudioKnowledge
): AudioRec {
  const dirIds = new Set<string>();
  const ambiences = new Set<string>();
  const bgmCandidates: string[] = [];
  let deathTheme = "";

  const active = new Set<string>(signals.themes);
  if (signals.raft) active.add("raft");

  for (const t of knowledge.themes) {
    if (!active.has(t.key)) continue;
    t.directories.forEach((d) => dirIds.add(d));
    t.ambiences.forEach((a) => ambiences.add(a));
    t.bgm.forEach((b) => bgmCandidates.push(b));
    if (t.deathTheme) deathTheme = t.deathTheme;
  }
  if (signals.deathTheme) deathTheme = signals.deathTheme;
  return { dirIds, ambiences, bgmId: bgmCandidates[0] ?? "", deathTheme };
}

export async function openAudioModal(
  detail: LevelDetail,
  themeSignals: ThemeSignals,
  onSaved: () => void
): Promise<void> {
  if (!detail.sceneAssetPath) {
    setStatus("该关卡缺少场景路径，无法编辑音频", false);
    return;
  }
  setStatus("加载音频资源…");

  let music: MusicEntry[];
  let dirs: AudioDirectoryEntry[];
  let ambiances: string[];
  let deaths: DeathEffectEntry[];
  let knowledge: AudioKnowledge;
  let analysis: BundleAnalysis | null;
  let bundleGraph: Map<string, string[]>;
  try {
    [music, dirs, ambiances, deaths, knowledge, analysis, bundleGraph] = await Promise.all([
      api.fetchMusicCatalog(),
      api.fetchAudioDirectoryCatalog(),
      api.fetchAmbiences(),
      api.fetchDeathEffects(),
      api.fetchAudioKnowledge(),
      api.fetchBundleAnalysis(detail.levelInfoAssetPath).catch(() => null),
      api.fetchBundleGraph(),
    ]);
  } catch (e) {
    showError(e);
    return;
  }

  const cur = detail.audio;
  const alwaysLoaded = new Set(knowledge.alwaysLoadedBundles);
  const baseBundles = new Set(knowledge.baseBundles);
  const mandatoryIds = new Set(knowledge.mandatoryDirectoryIds);

  const dirById = new Map<string, AudioDirectoryEntry>();
  for (const d of dirs) dirById.set(d.id, d);
  const dirByGuid = new Map<string, AudioDirectoryEntry>();
  for (const d of dirs) dirByGuid.set(d.guid, d);
  const musicByGuid = new Map<string, MusicEntry>();
  for (const m of music) musicByGuid.set(m.guid, m);
  const eventsByDir = new Map<string, DirectoryEvent>();
  for (const e of knowledge.directoryEvents) eventsByDir.set(e.id, e);

  // ---- mutable state ----
  const state = {
    musicGuid: cur.inLevelMusicGuid || "",
    deathGuid: cur.onDeathEffectGuid || "",
    dirGuids: new Set<string>((cur.audioDirectoryGuids || []).filter((g) => dirByGuid.has(g))),
    ambiences: new Set<string>((cur.ambiences || []).filter((a) => ambiances.includes(a))),
    cleanExtras: false,
  };

  // always force the mandatory directories in
  const mandatoryGuids: string[] = [];
  for (const id of knowledge.mandatoryDirectoryIds) {
    const e = dirById.get(id);
    if (e) mandatoryGuids.push(e.guid);
  }
  const effectiveDirGuids = (): string[] => {
    const out = new Set<string>(mandatoryGuids);
    state.dirGuids.forEach((g) => out.add(g));
    return [...out];
  };

  const rec = detectAudioRecommendations(themeSignals, knowledge);

  // ---- BGM options grouped by theme ----
  const themeOfMusic = new Map<string, string>();
  for (const t of knowledge.themes)
    for (const b of t.bgm) if (!themeOfMusic.has(b)) themeOfMusic.set(b, t.key);
  const themeLabelZh = (k: string): string =>
    ({
      city_sushi: "城市 / 厨房",
      raft: "木筏 / 急流",
      wizard: "魔法学校",
      space: "太空",
      air_balloon: "热气球",
      mine: "矿洞",
      camping: "露营 (DLC5)",
      dlc03_christmas: "圣诞 (DLC3)",
      throne: "王座 (DLC2)",
      circus: "马戏团 (DLC8)",
      dlc08_circus: "马戏团 (DLC8)",
      dlc07_horde: "部落 (DLC7)",
      dlc09_wonderland: "仙境 (DLC9)",
      graveyard: "墓地",
    } as Record<string, string>)[k] || k;

  const ambLabelZh = new Map<string, string>(
    (knowledge.ambienceLabels || []).map((a) => [a.name, a.zh])
  );
  const ambZh = (name: string): string => ambLabelZh.get(name) || name;

  const bgmGroups = new Map<string, MusicEntry[]>();
  for (const m of music) {
    const tk = themeOfMusic.get(m.id) || "_other";
    if (!bgmGroups.has(tk)) bgmGroups.set(tk, []);
    bgmGroups.get(tk)!.push(m);
  }
  const bgmGroupKeys = [...bgmGroups.keys()].sort((a, b) => (a === "_other" ? 1 : a < b ? -1 : 1));
  const bgmOptHtml = `<option value="" ${!state.musicGuid ? "selected" : ""}>(无)</option>` +
    bgmGroupKeys
      .map((tk) => {
        const label = tk === "_other" ? "其他" : themeLabelZh(tk);
        const opts = bgmGroups
          .get(tk)!
          .map(
            (m) =>
              `<option value="${esc(m.guid)}" ${m.guid === state.musicGuid ? "selected" : ""}>${esc(m.nameZh)}${m.bundleName ? ` (${esc(m.bundleName)})` : ""}</option>`
          )
          .join("");
        return `<optgroup label="${esc(label)}">${opts}</optgroup>`;
      })
      .join("");

  // ---- death options ----
  const deathOptHtml = `<option value="" ${!state.deathGuid ? "selected" : ""}>(无)</option>` +
    deaths
      .map((d) => `<option value="${esc(d.guid)}" ${d.guid === state.deathGuid ? "selected" : ""}>${esc(d.id)}</option>`)
      .join("");

  // ---- mandatory dirs (locked) ----
  const legendHtml = (id: string): string => {
    const ev = eventsByDir.get(id);
    if (!ev) return "";
    const tag = ev.eventsZh.length ? `<span class="muted">[${ev.eventsZh.map(esc).join("／")}]</span>` : "";
    const desc = ev.desc ? `<span class="muted">${esc(ev.desc)}</span>` : "";
    return ` ${desc}${tag}`;
  };
  const mandatoryHtml = mandatoryGuids
    .map((g) => {
      const d = dirByGuid.get(g);
      const id = d?.id || "";
      return `<label class="modal-check"><input type="checkbox" checked disabled data-mand value="${esc(g)}"> ${esc(d?.nameZh || id)}${legendHtml(id)}</label>`;
    })
    .join("");

  // ---- themed dirs grouped ----
  const dirThemeOf = new Map<string, string>();
  for (const t of knowledge.themes) for (const d of t.directories) dirThemeOf.set(d, t.key);
  const nonMandatoryDirs = dirs.filter((d) => !mandatoryIds.has(d.id));
  const dirGroups = new Map<string, AudioDirectoryEntry[]>();
  for (const d of nonMandatoryDirs) {
    const tk = dirThemeOf.get(d.id) || "_other";
    if (!dirGroups.has(tk)) dirGroups.set(tk, []);
    dirGroups.get(tk)!.push(d);
  }
  const dirGroupKeys = [...dirGroups.keys()].sort((a, b) => (a === "_other" ? 1 : a < b ? -1 : 1));
  const isDirRecommended = (id: string) => rec.dirIds.has(id);
  const dirGroupsHtml = dirGroupKeys
    .map((tk) => {
      const label = tk === "_other" ? "其他" : themeLabelZh(tk);
      const items = dirGroups
        .get(tk)!
        .map((d) => {
          const checked = state.dirGuids.has(d.guid) || isDirRecommended(d.id) ? "checked" : "";
          const rec2 = isDirRecommended(d.id) ? `<span class="rec-tag">推荐</span>` : "";
          return `<label class="modal-check"><input type="checkbox" value="${esc(d.guid)}" data-dir ${checked}> ${esc(d.nameZh)}${rec2} <span class="muted">(${esc(d.bundleName || "?")})</span>${legendHtml(d.id)}</label>`;
        })
        .join("");
      return `<div class="amb-group"><div class="amb-group-title">${esc(label)}</div>${items}</div>`;
    })
    .join("");

  // ---- ambiences grouped ----
  const ambThemeOf = new Map<string, string>();
  for (const t of knowledge.themes) for (const a of t.ambiences) ambThemeOf.set(a, t.key);
  const ambGroups = new Map<string, string[]>();
  for (const a of ambiances) {
    const tk = ambThemeOf.get(a) || "_other";
    if (!ambGroups.has(tk)) ambGroups.set(tk, []);
    ambGroups.get(tk)!.push(a);
  }
  const ambGroupKeys = [...ambGroups.keys()].sort((a, b) => (a === "_other" ? 1 : a < b ? -1 : 1));
  const isAmbRecommended = (a: string) => rec.ambiences.has(a);
  const ambGroupsHtml = ambGroupKeys
    .map((tk) => {
      const label = tk === "_other" ? "其他" : themeLabelZh(tk);
      const items = ambGroups
        .get(tk)!
        .map((a) => {
          const checked = state.ambiences.has(a) || isAmbRecommended(a) ? "checked" : "";
          const rec2 = isAmbRecommended(a) ? `<span class="rec-tag">推荐</span>` : "";
          return `<label class="modal-check"><input type="checkbox" value="${esc(a)}" data-amb ${checked}> ${esc(ambZh(a))}${rec2} <span class="muted">(${esc(a)})</span></label>`;
        })
        .join("");
      return `<div class="amb-group"><div class="amb-group-title">${esc(label)}</div>${items}</div>`;
    })
    .join("");

  // ---- coverage check: which themed directories / ambiences are MISSING for placed decor ----
  const curDirIdSet = new Set<string>(cur.audioDirectoryIds || []);
  const curAmbSet = new Set<string>(cur.ambiences || []);
  const detectedThemeKeys = new Set<string>(themeSignals.themes);
  if (themeSignals.raft) detectedThemeKeys.add("raft");
  const gaps: { theme: string; missingDirs: string[]; missingAmb: string[] }[] = [];
  for (const t of knowledge.themes) {
    if (!detectedThemeKeys.has(t.key)) continue;
    const missingDirs = (t.directories || []).filter((id) => {
      const e = dirById.get(id);
      return e && !state.dirGuids.has(e.guid) && !curDirIdSet.has(id);
    });
    const missingAmb = (t.ambiences || []).filter((a) => !state.ambiences.has(a) && !curAmbSet.has(a));
    if (missingDirs.length || missingAmb.length)
      gaps.push({ theme: t.key, missingDirs, missingAmb });
  }
  const gapHtml = gaps.length
    ? gaps
        .map(
          (g) =>
            `<div class="amb-group"><div class="amb-group-title">${esc(themeLabelZh(g.theme))}</div>` +
            (g.missingDirs.length
              ? `<div class="dep-warn dep-miss">缺音效集：${g.missingDirs.map((id) => esc(dirById.get(id)?.nameZh || id)).join("、")}</div>`
              : "") +
            (g.missingAmb.length
              ? `<div class="dep-warn dep-miss">缺氛围音：${g.missingAmb.map((a) => esc(ambZh(a))).join("、")}</div>`
              : "") +
            `</div>`
        )
        .join("")
    : `<div class="dep-ok">已覆盖所有已放置装饰的主题音效集与氛围音。</div>`;

  // ---- dependencies panel ----
  let depsHtml = "";
  if (analysis) {
    const miss = analysis.missing.filter((b) => !alwaysLoaded.has(b));
    const extras = analysis.extras;
    const missHtml = miss.length
      ? `<div class="dep-warn dep-miss">🔴 缺失（保存时自动加入）：<b>${miss.map(esc).join(", ")}</b></div>`
      : `<div class="dep-ok">依赖资源包完整。</div>`;
    const extrasHtml = extras.length
      ? `<div class="dep-warn dep-extra">🟡 检测到未使用 bundle：${extras.map(esc).join(", ")} <label class="modal-check inline"><input type="checkbox" id="au-clean"> 清理未用 bundle</label></div>`
      : "";
    const cur2 = analysis.current.length ? analysis.current.map(esc).join(", ") : "<i class='muted'>（无）</i>";
    depsHtml = `
      <p class="modal-hint" style="margin-top:8px">依赖资源包 <code>LevelInfoSO.dependencies</code></p>
      <div class="dep-box">
        <div class="muted">当前：${cur2}</div>
        ${missHtml}
        ${extrasHtml}
      </div>`;
  }

  // ---- render ----
  const detectedThemes = [...themeSignals.themes, ...(themeSignals.raft ? ["raft"] : [])];
  const recHint = detectedThemes.length
    ? `检测到主题：${detectedThemes.map((t) => esc(themeLabelZh(t))).join("、")}`
    : "未检测到明显主题（可手动选择）";

  openModal(
    `音频配置 · ${detail.levelName || detail.levelNameZH}`,
    `
    <p class="modal-hint">写入场景的 <code>PseudoPrefabManagerStub</code>。保存时会自动打开/保存场景、Reload，并<b>自动把所选 BGM / 音效集所需 bundle 并入 <code>LevelInfoSO.dependencies</code></b>（只增不删）。</p>
    <div class="audio-grid">
      <label class="m-field">关卡 BGM (InLevelMusicSO)<select id="au-music">${bgmOptHtml}</select></label>
      <div id="au-music-warn" class="dep-warn dep-miss" style="display:none"></div>
      <label class="m-field">死亡特效 (OnDeathEffectSO)<select id="au-death">${deathOptHtml}</select>
        <span class="muted small">快捷：<button type="button" class="link-btn" data-death-theme="water">水面</button> / <button type="button" class="link-btn" data-death-theme="goo">黏液</button></span>
      </label>
    </div>
    <div class="modal-actions"><button type="button" class="m-btn small" id="au-apply-rec">✨ 应用主题推荐</button> <span class="muted small">${recHint}</span></div>

    <p class="modal-hint" style="margin-top:8px">覆盖检查（根据已放置装饰的主题）${gaps.length ? ` · <span class="dep-miss">有 ${gaps.length} 个主题存在缺失</span>` : ""} <button type="button" class="link-btn" id="au-fix-gaps" ${gaps.length ? "" : "disabled"}>添加所有缺失</button></p>
    <div class="dep-box">${gapHtml}</div>

    <p class="modal-hint" style="margin-top:8px">强制音效集（5，锁定）—— 全局玩法 / 脚步 / 语音 / 厨房氛围 / UI</p>
    <div class="modal-scroll locked">${mandatoryHtml || '<p class="muted">未找到强制音效集（请检查 audio-knowledge.json 与 common01 目录）</p>'}</div>

    <p class="modal-hint" style="margin-top:8px">主题音效集 AudioDirectorySOs（叠加在强制 5 之上）</p>
    <div class="modal-scroll">${dirGroupsHtml || '<p class="muted">无</p>'}</div>

    <p class="modal-hint" style="margin-top:8px">氛围音 InLevelAmbiences</p>
    <div class="modal-scroll">${ambGroupsHtml || '<p class="muted">无</p>'}</div>
    ${depsHtml}
    `,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>保存</button>`
  );
  document.querySelector(".modal-panel")?.classList.add("wide");

  // ---- helpers to refresh derived UI ----
  const musicBundleMissing = (): string | null => {
    const m = musicByGuid.get(state.musicGuid);
    if (!m || !m.bundleName) return null;
    if (alwaysLoaded.has(m.bundleName) || baseBundles.has(m.bundleName)) return null;
    // accurate check: is the BGM's bundle reachable from the declared dependencies?
    const loaded = api.bundleClosure(bundleGraph, detail.dependencies || []);
    if (loaded.has(m.bundleName)) return null;
    return m.bundleName;
  };
  const refreshMusicWarn = (): void => {
    const el = document.getElementById("au-music-warn");
    if (!el) return;
    const missing = musicBundleMissing();
    if (missing) {
      el.style.display = "";
      el.innerHTML = `⚠️ 该 BGM 需要 <b>${esc(missing)}</b>，当前 dependencies 未包含——保存时会自动加入。`;
    } else {
      el.style.display = "none";
    }
  };

  // ---- wire events ----
  document.getElementById("au-music")?.addEventListener("change", (e) => {
    state.musicGuid = (e.target as HTMLSelectElement).value;
    refreshMusicWarn();
  });
  document.getElementById("au-death")?.addEventListener("change", (e) => {
    state.deathGuid = (e.target as HTMLSelectElement).value;
  });
  document.querySelectorAll<HTMLInputElement>("[data-dir]").forEach((el) =>
    el.addEventListener("change", () => {
      if (el.checked) state.dirGuids.add(el.value);
      else state.dirGuids.delete(el.value);
    })
  );
  document.querySelectorAll<HTMLInputElement>("[data-amb]").forEach((el) =>
    el.addEventListener("change", () => {
      if (el.checked) state.ambiences.add(el.value);
      else state.ambiences.delete(el.value);
    })
  );

  // death theme quick buttons
  document.querySelectorAll<HTMLButtonElement>("[data-death-theme]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const themeKey = btn.dataset.deathTheme || "";
      const dt = knowledge.deathThemes.find((t) => t.key === themeKey);
      if (!dt) return;
      const hit = [...deaths].find((d) => d.id.includes(dt.effectIdHint));
      if (hit) {
        state.deathGuid = hit.guid;
        const sel = document.getElementById("au-death") as HTMLSelectElement | null;
        if (sel) sel.value = hit.guid;
        setStatus(`已选死亡特效：${hit.id}`);
      }
    });
  });

  // apply recommendations
  document.getElementById("au-apply-rec")?.addEventListener("click", () => {
    rec.dirIds.forEach((id) => {
      const e = dirById.get(id);
      if (e) {
        state.dirGuids.add(e.guid);
        const cb = document.querySelector<HTMLInputElement>(`[data-dir][value="${e.guid}"]`);
        if (cb) cb.checked = true;
      }
    });
    rec.ambiences.forEach((a) => {
      if (ambiances.includes(a)) {
        state.ambiences.add(a);
        const cb = document.querySelector<HTMLInputElement>(`[data-amb][value="${a}"]`);
        if (cb) cb.checked = true;
      }
    });
    if (!state.musicGuid && rec.bgmId) {
      const m = music.find((x) => x.id === rec.bgmId);
      if (m) {
        state.musicGuid = m.guid;
        const sel = document.getElementById("au-music") as HTMLSelectElement | null;
        if (sel) sel.value = m.guid;
        refreshMusicWarn();
      }
    }
    if (rec.deathTheme) {
      const dt = knowledge.deathThemes.find((t) => t.key === rec.deathTheme);
      if (dt) {
        const hit = [...deaths].find((d) => d.id.includes(dt.effectIdHint));
        if (hit) {
          state.deathGuid = hit.guid;
          const sel = document.getElementById("au-death") as HTMLSelectElement | null;
          if (sel) sel.value = hit.guid;
        }
      }
    }
    setStatus("已套用主题推荐（可继续手动调整）");
  });

  // fix all detected coverage gaps (check the missing themed dirs + ambiences)
  document.getElementById("au-fix-gaps")?.addEventListener("click", () => {
    let n = 0;
    for (const g of gaps) {
      for (const id of g.missingDirs) {
        const e = dirById.get(id);
        if (!e) continue;
        state.dirGuids.add(e.guid);
        const cb = document.querySelector<HTMLInputElement>(`[data-dir][value="${e.guid}"]`);
        if (cb) cb.checked = true;
        n++;
      }
      for (const a of g.missingAmb) {
        if (!ambiances.includes(a)) continue;
        state.ambiences.add(a);
        const cb = document.querySelector<HTMLInputElement>(`[data-amb][value="${a}"]`);
        if (cb) cb.checked = true;
        n++;
      }
    }
    setStatus(n ? `已补齐 ${n} 项缺失音效集/氛围音` : "无缺失需要补齐");
  });

  refreshMusicWarn();
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", async () => {
    try {
      state.cleanExtras =
        (document.getElementById("au-clean") as HTMLInputElement | null)?.checked ?? false;
      const ambiences = [...state.ambiences];
      const audioDirectoryGuids = effectiveDirGuids();

      // optional cleanup first (trim confirmed-unused bundles), then audio save auto-merges required.
      if (state.cleanExtras && analysis && analysis.extras.length) {
        const cur2 = new Set(analysis.current);
        analysis.extras.forEach((b) => cur2.delete(b));
        const kept = [...cur2];
        showBusy("清理未用 bundle…");
        await api.updateLevelInfo({
          assetPath: detail.levelInfoAssetPath,
          levelName: detail.levelName,
          levelNameZH: detail.levelNameZH,
          sceneName: detail.sceneName,
          debugRecipeCount: detail.debugRecipeCount,
          disableDynamicParenting: detail.disableDynamicParenting,
          dependencies: kept,
        });
      }

      showBusy("保存音频配置（打开并保存场景）…");
      await api.updateLevelAudio({
        sceneAssetPath: detail.sceneAssetPath,
        inLevelMusicGuid: state.musicGuid,
        ambiences,
        audioDirectoryGuids,
        onDeathEffectGuid: state.deathGuid,
      });
      closeModal();
      setStatus("音频配置已保存（已 reload，所需 bundle 已自动补齐）");
      onSaved();
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });
}
