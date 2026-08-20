import * as api from "./api";
import type {
  AudioDirectoryEntry,
  AudioExportManifest,
  AudioExportSfxDir,
  AudioItemRule,
  AudioKnowledge,
  BundleAnalysis,
  CustomRecipeSummary,
  DeathEffectEntry,
  DirectoryEvent,
  IngredientEntry,
  LevelDetail,
  LevelSetInfo,
  LevelSummary,
  MusicEntry,
  PerPlayerConfig,
  RecipeEntry,
  SetExportStatus,
} from "./types";
import { closeModal, openModal } from "./modals";
import { showBusy, hideBusy, setBusyMessage } from "./busy";
import { suspendBridgeWatch, resumeBridgeWatch } from "./editor/sceneIO";
import { navHtml, wireNav } from "./nav";
import { applyRatio, computeAutoScores, round5, RATIO_MAX, RATIO_MIN, RATIO_STEP } from "./autoScore";
import { groupRecipesByType, recipeTypeLabel } from "./recipeTypes";
import { foodGroupLabel } from "./ingredientLabels";
import { computeCardGroups, rlCardHtml, rlSectionHtml, STEP_ICON_SRC, type RecipeWithGroups } from "./recipeCard";
import { exportSummaryPng, type SummaryCard, type SummaryExportData } from "./summaryExport";
import { customRecipeIconUrl } from "./editor/catalog";
import { normalizeCustomRecipeCard } from "./recipeCardCustom";

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
    } else if (target === "recipes") {
      location.href = "/recipes";
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
          <button class="m-btn" data-export="${esc(s.setName)}">导出</button>
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
  content.querySelectorAll<HTMLButtonElement>("[data-export]").forEach((b) =>
    b.addEventListener("click", () => {
      const s = setMap.get(b.dataset.export!);
      if (s) confirmExportSet(app, s);
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

// ==================== Set export（打包 AssetBundle + zip 下载） ====================

/** 导出各阶段的提示（后端 message 已足够描述时优先用后端的）。 */
const EXPORT_PHASE_HINT: Record<string, string> = {
  queued: "任务已排队…",
  prepare: "准备场景（逐个保存并清除临时物体）…",
  clean: "清理旧构建产物…",
  build: "构建 AssetBundle，约需 3-5 分钟，请保持 Unity 打开…",
  package: "清理 manifest / meta 文件…",
  zip: "生成 zip 压缩包…",
};

function fmtElapsed(ms: number): string {
  const sec = Math.floor(ms / 1000);
  const m = Math.floor(sec / 60);
  return `${m}:${(sec % 60).toString().padStart(2, "0")}`;
}

function confirmExportSet(app: HTMLElement, s: LevelSetInfo): void {
  const display = s.levelSetNameZH || s.levelSetName || s.setName;
  const prevVersion = (s.version || "").trim();
  openModal(
    `导出关卡集 · ${esc(display)}`,
    `<p>将打包 <b>${esc(display)}</b>（${s.levelCount} 个关卡）的 AssetBundle 并生成可发布的 zip，<b>约需 3-5 分钟</b>。期间 Unity 会逐个保存场景并构建，请勿操作 Unity 或关闭本页。</p>
     <p class="modal-hint">发布新版本前建议更新版本号（当前 <code>v${esc(prevVersion || "0")}</code>，会写入 LevelSetInfo）：</p>
     <label class="m-field">版本号 version<input type="text" id="exp-set-version" autocomplete="off" placeholder="${esc(prevVersion || "0.1")}" value="${esc(prevVersion)}"></label>
     <p class="modal-hint">zip 内含 <code>${esc(s.setName)}/info_${esc(s.setName)}</code> 与各场景 <code>${esc(s.setName)}/s_*</code>（不含 manifest/meta）。将 zip 解压到游戏 <code>BepInEx/plugins/OC2DIYLevel/levels/</code> 目录即可游玩。</p>`,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>开始导出</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", async () => {
    const okBtn = document.querySelector("[data-ok]") as HTMLButtonElement | null;
    if (okBtn) okBtn.disabled = true;
    const versionInput = document.getElementById("exp-set-version") as HTMLInputElement | null;
    const newVersion = (versionInput?.value ?? "").trim();
    const startAt = Date.now();
    suspendBridgeWatch(); // 构建会阻塞 Unity 主线程泵，健康探测会误报掉线
    showBusy("正在启动导出…");
    try {
      if (newVersion && newVersion !== prevVersion) {
        setBusyMessage("保存版本号…");
        await api.updateSetInfo({
          setName: s.setName,
          levelSetName: s.levelSetName,
          levelSetNameZH: s.levelSetNameZH,
          author: s.author,
          version: newVersion,
        });
      }
      await api.startSetExport(s.setName);
      closeModal();

      // 轮询导出进度（状态端点由桥接监听线程直答，构建期间仍可响应）。
      const deadline = Date.now() + 15 * 60 * 1000;
      for (;;) {
        if (Date.now() > deadline) throw new Error("导出超时（15 分钟），请查看 Unity Console。");
        await new Promise((r) => setTimeout(r, 2000));
        let st: SetExportStatus;
        try {
          st = await api.fetchSetExportStatus();
        } catch {
          continue; // 瞬时网络抖动，继续轮询
        }
        if (st.status === "error") throw new Error(st.error || "导出失败（详见 Unity Console）。");
        if (st.status === "done" && st.setName === s.setName) {
          setBusyMessage("导出完成，正在下载 zip…");
          const res = await api.downloadSetExportZip(s.setName, st.zipFileName);
          const url = URL.createObjectURL(res.blob);
          const a = document.createElement("a");
          a.href = url;
          a.download = res.fileName;
          a.click();
          setTimeout(() => URL.revokeObjectURL(url), 1000);
          setStatus(`已导出 ${res.fileName}（${st.fileCount} 个文件），已开始下载`);
          break;
        }
        const hint = st.message || EXPORT_PHASE_HINT[st.phase] || "导出中…";
        setBusyMessage(`${hint}（已进行 ${fmtElapsed(Date.now() - startAt)}）`);
      }
      await renderSetList(app);
    } catch (e) {
      setStatus((e as Error).message, false);
      if (okBtn) okBtn.disabled = false; // 允许重试（弹窗此时可能仍打开）
    } finally {
      resumeBridgeWatch();
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

  const cards = levels
    .map((lv, idx) => {
      const id = lv.dataDir.split("/").pop() || `level${idx}`;
      const shot = lv.screenshotPath ? api.imageFloorUrl(lv.screenshotPath) : "";
      const title = lv.levelNameZH || lv.levelName || id;
      const enName = lv.levelNameZH && lv.levelName ? lv.levelName : "";
      return `
      <div class="m-card m-level-card">
        <div class="m-level-shot${shot ? "" : " empty"}">
          ${shot ? `<img src="${esc(shot)}" alt="截图" loading="lazy">` : '<span class="muted">无截图</span>'}
        </div>
        <h3 title="${esc(title)}">${esc(title)}${enName ? ` <span class="muted">(${esc(enName)})</span>` : ""}</h3>
        <div class="m-meta">
          ${lv.hasScene ? '<span class="m-badge ok">场景</span>' : '<span class="m-badge warn">缺场景</span>'}
          <span class="muted">${esc(lv.sceneName)} · ${esc(id)}</span>
        </div>
        <div class="m-actions">
          <button class="m-btn primary" data-edit="${esc(lv.assetPath)}">编辑</button>
          <button class="m-btn" data-summary="${esc(lv.assetPath)}">📋 汇总</button>
          <button class="m-btn" data-layout="${esc(lv.sceneAssetPath)}">打开布局</button>
          <button class="m-btn danger" data-del="${esc(id)}">删除</button>
        </div>
      </div>`;
    })
    .join("");

  content.innerHTML = `
    <div class="m-actions-row">
      <button class="m-btn primary" id="new-level">+ 新建关卡</button>
      <span class="muted">当前关卡集：<b>${esc(setName)}</b></span>
    </div>
    <div class="m-section-title">关卡</div>
    <div class="m-grid m-level-grid">${cards || '<p class="muted">暂无关卡</p>'}</div>
  `;

  document.getElementById("new-level")?.addEventListener("click", () => openCreateLevelModal(app, setName));
  content.querySelectorAll<HTMLButtonElement>("[data-edit]").forEach((b) =>
    b.addEventListener("click", () => void renderLevelDetail(app, setName, b.dataset.edit!))
  );
  content.querySelectorAll<HTMLButtonElement>("[data-summary]").forEach((b) =>
    b.addEventListener("click", () => void renderLevelSummary(app, setName, b.dataset.summary!))
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
      <button class="m-btn" id="btn-summary">📋 汇总</button>
    </div>

    <div class="m-block">
      <h3>基础信息 (LevelInfoSO)</h3>
      <div class="m-form">
        <label class="m-field">英文名 levelName<input type="text" id="f-levelName" value="${esc(detail.levelName)}"></label>
        <label class="m-field">中文名 levelNameZH<input type="text" id="f-levelNameZH" value="${esc(detail.levelNameZH)}"></label>
        <label class="m-field">场景名 sceneName<input type="text" id="f-sceneName" value="${esc(detail.sceneName)}"></label>
        <label class="m-field">调试菜谱数 debugRecipeCount<input type="number" id="f-debugRecipeCount" value="${detail.debugRecipeCount}"></label>
        <label class="m-field">最少同时订单 minOrderCount<input type="number" id="f-minOrderCount" min="1" max="10" step="1" value="${detail.minOrderCount}"></label>
        <label class="m-field">最多同时订单 maxOrderCount<input type="number" id="f-maxOrderCount" min="1" max="10" step="1" value="${detail.maxOrderCount}"></label>
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
        minOrderCount: Number((document.getElementById("f-minOrderCount") as HTMLInputElement).value || 2),
        maxOrderCount: Number((document.getElementById("f-maxOrderCount") as HTMLInputElement).value || 5),
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
  document.getElementById("btn-summary")?.addEventListener("click", () => void renderLevelSummary(app, setName, assetPath));

  const ssFile = document.getElementById("ss-file") as HTMLInputElement | null;
  const ssUpload = document.getElementById("ss-upload");
  const ssPreview = document.getElementById("ss-preview");

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

// ==================== Level recipe summary (汇总页) ====================

/** 汇总页：中文名 → 作者 → 关卡截图 → 按菜系分类的菜谱卡片（一个分类一行），
 *  支持按实际渲染大小一键导出 PNG。 */
export async function renderLevelSummary(app: HTMLElement, setName: string, assetPath: string): Promise<void> {
  const content = shell(app, `汇总 · ${setName}`, "返回关卡列表", () => void renderLevelList(app, setName));
  setBusy("加载汇总…");
  let detail: LevelDetail;
  let sets: LevelSetInfo[];
  try {
    [sets, detail] = await Promise.all([api.fetchSets(), api.fetchLevelDetail(assetPath)]);
  } catch (e) {
    showError(e);
    return;
  }
  if (!detail) {
    content.innerHTML = '<p class="muted">未找到该关卡。</p>';
    return;
  }

  let level: { recipeGuids: string[]; recipeIds?: string[] };
  let recipes: RecipeWithGroups[];
  let ingredients: IngredientEntry[];
  let customRecipes: CustomRecipeSummary[] = [];
  try {
    [level, recipes, ingredients, customRecipes] = await Promise.all([
      api.fetchLevelRecipes(detail.sceneAssetPath),
      api.fetchRecipeCatalog(setName),
      api.fetchIngredients().catch(() => [] as IngredientEntry[]),
      api.fetchCustomRecipes(setName).catch(() => [] as CustomRecipeSummary[]),
    ]);
  } catch (e) {
    showError(e);
    return;
  }

  const set = sets.find((s) => s.setName === setName);
  const ingredientName = (id: string): string => ingredients.find((i) => i.id === id)?.nameZh ?? id;
  /** 成品图标：自定义菜谱经专用端点从 CustomRecipeSO.icon 读取；其余用静态菜谱图标。 */
  const recipeIconUrl = (r: RecipeEntry): string =>
    customRecipeIconUrl(r) ?? `/icons/recipes/${encodeURIComponent(r.id)}.png`;

  const byGuid = new Map(recipes.map((r) => [r.guid, r]));
  // 关卡集自定义菜谱：用与自定义菜谱列表一致的形式覆盖目录条目
  // （normalizeCustomRecipeCard：cookingStep=cookingStepId、mixing=type==="Mixed"、
  //  intermediate=false、不携带后端 cookingGroups），保证汇总卡片与列表卡片完全一致。
  const customByGuid = new Map(customRecipes.map((c) => [c.guid, normalizeCustomRecipeCard(c) as RecipeWithGroups]));
  for (const [g, c] of customByGuid) byGuid.set(g, c);
  const selected = (level.recipeGuids ?? [])
    .map((g) => byGuid.get(g))
    // 自定义菜谱（含 Composite/Mixed，score 可能为 0 被标 intermediate）也计入汇总
    .filter((r): r is RecipeEntry => !!r && (!r.intermediate || !!r.isCustom));

  const grouped = groupRecipesByType(selected).map(([type, arr]) => ({
    type,
    typeLabel: recipeTypeLabel(type),
    count: arr.length,
    recipes: arr,
  }));

  const summaryData: SummaryExportData = {
    title: detail.levelNameZH || detail.levelName || "未命名",
    sub: `${detail.levelName} · ${detail.sceneName}`,
    author: `作者：${set?.author || "—"}`,
    screenshotUrl: detail.screenshotPath ? api.imageFloorUrl(detail.screenshotPath) : "",
    sections: grouped.map((g) => ({
      typeLabel: g.typeLabel,
      count: g.count,
      cards: g.recipes.map((r): SummaryCard => {
        const groups = computeCardGroups(r, { allRecipes: recipes });
        const badges: string[] = [];
        if (r.isCustom) badges.push("自定义");
        if (r.group === "levelset") badges.push("本关");
        if (r.group && r.group !== "core" && r.group !== "levelset") badges.push(foodGroupLabel(r.group));
        badges.push(`⭐ ${r.score ?? 0}`);
        return {
          iconUrl: recipeIconUrl(r),
          nameZh: r.nameZh,
          nameEn: r.nameEn || r.id,
          badges,
          groups: groups.map((cg) => ({
            stepIcons: [cg.step, ...(cg.extraSteps ?? []).map((e) => e.step)]
              .filter(Boolean)
              .map((s) => STEP_ICON_SRC[s])
              .filter((s): s is string => !!s),
            ingredientUrls: (cg.ingredients ?? []).map((id) => `/icons/ingredients/${encodeURIComponent(id)}.png`),
            ingredientStepIcons: (cg.ingredients ?? []).map(
              (id) =>
                (cg.ingredientSteps?.[id] ?? [])
                  .map((s) => STEP_ICON_SRC[s])
                  .filter((s): s is string => !!s)
            ),
          })),
        };
      }),
    })),
  };

  const sections = grouped
    .map((g) =>
      rlSectionHtml(
        g.type,
        g.recipes
          .map((r) =>
            rlCardHtml(r, {
              allRecipes: recipes,
              ingredientName,
              extraBadge: r.group === "levelset" ? "本关" : undefined,
              iconSrc: recipeIconUrl,
            })
          )
          .join(""),
        g.count
      )
    )
    .join("");

  const shotSrc = detail.screenshotPath ? api.imageFloorUrl(detail.screenshotPath) : "";
  const shotHtml = shotSrc
    ? `<img class="sum-shot-img" src="${esc(shotSrc)}" alt="关卡截图">`
    : '<div class="sum-shot-empty">（未上传关卡截图）</div>';

  content.innerHTML = `
    <div class="m-actions-row">
      <button class="m-btn primary" id="sum-export">🖼 一键导出图片</button>
      <span class="status" id="sum-status"></span>
    </div>
    <div class="sum-page" id="sum-node">
      <header class="sum-head">
        <h1 class="sum-title">${esc(detail.levelNameZH || detail.levelName || "未命名")}</h1>
        <div class="sum-sub">${esc(detail.levelName)} · ${esc(detail.sceneName)}</div>
        <div class="sum-author">作者：${esc(set?.author || "—")}</div>
      </header>
      <div class="sum-shot">${shotHtml}</div>
      <div class="sum-recipes">
        ${sections || '<p class="muted">该关卡尚未配置菜谱</p>'}
      </div>
    </div>
  `;

  setStatus(`共 ${selected.length} 道菜谱 · 关卡截图${summaryData.screenshotUrl ? "" : "缺失"}`);
  document.getElementById("sum-export")?.addEventListener("click", async () => {
    const btn = document.getElementById("sum-export") as HTMLButtonElement | null;
    const st = document.getElementById("sum-status")!;
    if (btn) btn.disabled = true;
    try {
      const node = document.getElementById("sum-node");
      const width = node ? node.getBoundingClientRect().width : 1200;
      const fileName = `${detail.levelNameZH || detail.levelName || "level"}_汇总.png`;
      await exportSummaryPng(summaryData, width, fileName);
      st.textContent = "已导出 PNG";
    } catch (e) {
      st.textContent = (e as Error).message;
      st.classList.add("err");
    } finally {
      if (btn) btn.disabled = false;
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
     <p class="modal-hint">订单数量（LevelInfoSO）</p>
     <div class="cfg-order-count">
       <label class="m-field">最少同时订单 minOrderCount<input type="number" id="cfg-minOrderCount" min="1" max="10" step="1" value="${detail.minOrderCount ?? 2}"></label>
       <label class="m-field">最多同时订单 maxOrderCount<input type="number" id="cfg-maxOrderCount" min="1" max="10" step="1" value="${detail.maxOrderCount ?? 5}"></label>
     </div>
     <p class="modal-hint">星级分数（按人数）</p>
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
      const minOrderCount = Number((document.getElementById("cfg-minOrderCount") as HTMLInputElement).value || 2);
      const maxOrderCount = Number((document.getElementById("cfg-maxOrderCount") as HTMLInputElement).value || 5);
      await api.updateLevelInfo({
        assetPath: detail.levelInfoAssetPath,
        levelName: detail.levelName,
        levelNameZH: detail.levelNameZH,
        sceneName: detail.sceneName,
        debugRecipeCount: detail.debugRecipeCount,
        disableDynamicParenting: detail.disableDynamicParenting,
        minOrderCount,
        maxOrderCount,
        dependencies: detail.dependencies,
      });
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
  /** catalog ids of items placed on canvas, for item→audio rule matching. */
  itemIds: Set<string>;
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

/** Expand itemAudioRules for the items present on canvas. */
function detectItemAudioRequirements(
  itemIds: Set<string>,
  knowledge: AudioKnowledge
): {
  hits: { rule: AudioItemRule; itemIds: string[] }[];
  dirIds: Set<string>;
  ambiences: Set<string>;
} {
  const hits: { rule: AudioItemRule; itemIds: string[] }[] = [];
  const dirIds = new Set<string>();
  const ambiences = new Set<string>();
  const themeMap = new Map(knowledge.themes.map((t) => [t.key, t]));
  for (const rule of knowledge.itemAudioRules) {
    const matched = rule.items.filter((id) => itemIds.has(id));
    if (matched.length === 0) continue;
    hits.push({ rule, itemIds: matched });
    if (rule.theme) {
      const t = themeMap.get(rule.theme);
      if (t) {
        t.directories.forEach((d) => dirIds.add(d));
        t.ambiences.forEach((a) => ambiences.add(a));
      }
    }
    if (rule.directories) rule.directories.forEach((d) => dirIds.add(d));
    if (rule.ambiences) rule.ambiences.forEach((a) => ambiences.add(a));
  }
  return { hits, dirIds, ambiences };
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
  let exports: AudioExportManifest | null;
  try {
    [music, dirs, ambiances, deaths, knowledge, analysis, bundleGraph, exports] = await Promise.all([
      api.fetchMusicCatalog(),
      api.fetchAudioDirectoryCatalog(),
      api.fetchAmbiences(),
      api.fetchDeathEffects(),
      api.fetchAudioKnowledge(),
      api.fetchBundleAnalysis(detail.levelInfoAssetPath).catch(() => null),
      api.fetchBundleGraph(),
      api.fetchAudioExports(),
    ]);
  } catch (e) {
    showError(e);
    return;
  }

  const sfxById = new Map<string, AudioExportSfxDir>();
  if (exports) {
    for (const d of exports.sfx) sfxById.set(d.id, d);
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

  // ---- ambience validity ----
  // 枚举里有 6 个死值（WashingUp/Sizzling 等）不存在于任何 AudioDirectoryData，
  // 选中后运行时 AudioManager.FindEntry 会对空列表取下标直接越界。仅展示/保存有音频资源的 tag。
  const ambValidSet = (knowledge.availableAmbiences || []).length
    ? new Set<string>(knowledge.availableAmbiences!)
    : null;
  const ambUniverse = ambValidSet ? ambiances.filter((a) => ambValidSet.has(a)) : ambiances;
  const droppedAmb = ambValidSet
    ? (cur.ambiences || []).filter((a) => ambiances.includes(a) && !ambValidSet.has(a))
    : [];

  // ---- mutable state ----
  const state = {
    musicGuid: cur.inLevelMusicGuid || "",
    deathGuid: cur.onDeathEffectGuid || "",
    dirGuids: new Set<string>((cur.audioDirectoryGuids || []).filter((g) => dirByGuid.has(g))),
    ambiences: new Set<string>(
      (cur.ambiences || []).filter((a) => ambiances.includes(a) && (!ambValidSet || ambValidSet.has(a)))
    ),
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
  const itemReq = detectItemAudioRequirements(themeSignals.itemIds, knowledge);

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
  // 当前 BGM 不在曲库时（guid 未匹配）也保留显示，避免静默显示为「(无)」导致误存覆盖
  const bgmInCatalog = musicByGuid.has(state.musicGuid);
  const bgmCurId = cur.inLevelMusicId || state.musicGuid;
  const bgmOptHtml = `<option value="" ${!state.musicGuid ? "selected" : ""}>(无)</option>` +
    (state.musicGuid && !bgmInCatalog
      ? `<option value="${esc(state.musicGuid)}" selected>当前：${esc(bgmCurId)}（不在曲库，保持不变）</option>`
      : "") +
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

  // ---- legend helper ----
  const legendHtml = (id: string): string => {
    const ev = eventsByDir.get(id);
    if (!ev) return "";
    const tag = ev.eventsZh.length ? `<span class="muted">[${ev.eventsZh.map(esc).join("／")}]</span>` : "";
    const desc = ev.desc ? `<span class="muted">${esc(ev.desc)}</span>` : "";
    return ` ${desc}${tag}`;
  };

  // ---- mandatory dirs (locked) ----
  const mandatoryHtml = mandatoryGuids
    .map((g) => {
      const d = dirByGuid.get(g);
      const id = d?.id || "";
      const hasClips = sfxById.has(id) && (sfxById.get(id)!.clips.length > 0);
      const expandBtn = hasClips
        ? ` <button type="button" class="au-expand-btn" data-au-expand="${esc(id)}" title="试听">▶ 试听</button><div class="au-clips" data-au-clips="${esc(id)}" style="display:none"></div>`
        : "";
      return `<label class="modal-check"><input type="checkbox" checked disabled data-mand value="${esc(g)}"> ${esc(d?.nameZh || id)}${legendHtml(id)}${expandBtn}</label>`;
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
  const isDirRecommended = (id: string) => rec.dirIds.has(id) || itemReq.dirIds.has(id);
  const dirGroupsHtml = dirGroupKeys
    .map((tk) => {
      const label = tk === "_other" ? "其他" : themeLabelZh(tk);
      const items = dirGroups
        .get(tk)!
        .map((d) => {
          const checked = state.dirGuids.has(d.guid) || isDirRecommended(d.id) ? "checked" : "";
          const rec2 = isDirRecommended(d.id) ? `<span class="rec-tag">推荐</span>` : "";
          const hasClips = sfxById.has(d.id) && (sfxById.get(d.id)!.clips.length > 0);
          const expandBtn = hasClips
            ? ` <button type="button" class="au-expand-btn" data-au-expand="${esc(d.id)}" title="试听">▶ 试听</button><div class="au-clips" data-au-clips="${esc(d.id)}" style="display:none"></div>`
            : "";
          return `<label class="modal-check"><input type="checkbox" value="${esc(d.guid)}" data-dir ${checked}> ${esc(d.nameZh)}${rec2} <span class="muted">(${esc(d.bundleName || "?")})</span>${legendHtml(d.id)}${expandBtn}</label>`;
        })
        .join("");
      return `<div class="amb-group"><div class="amb-group-title">${esc(label)}</div>${items}</div>`;
    })
    .join("");

  // ---- ambiences grouped ----
  const ambThemeOf = new Map<string, string>();
  for (const t of knowledge.themes) for (const a of t.ambiences) ambThemeOf.set(a, t.key);
  const ambGroups = new Map<string, string[]>();
  for (const a of ambUniverse) {
    const tk = ambThemeOf.get(a) || "_other";
    if (!ambGroups.has(tk)) ambGroups.set(tk, []);
    ambGroups.get(tk)!.push(a);
  }
  const ambGroupKeys = [...ambGroups.keys()].sort((a, b) => (a === "_other" ? 1 : a < b ? -1 : 1));
  const isAmbRecommended = (a: string) => rec.ambiences.has(a) || itemReq.ambiences.has(a);
  const ambGroupsHtml = ambGroupKeys
    .map((tk) => {
      const label = tk === "_other" ? "其他" : themeLabelZh(tk);
      const items = ambGroups
        .get(tk)!
        .map((a) => {
          const checked = state.ambiences.has(a) || isAmbRecommended(a) ? "checked" : "";
          const rec2 = isAmbRecommended(a) ? `<span class="rec-tag">推荐</span>` : "";
          let playBtn = "";
          if (exports) {
            const ambExp = exports.ambiences.find((x) => x.tag === a);
            if (ambExp && ambExp.found) {
              playBtn = ` <button type="button" class="au-play-btn" data-au-play="ambience" data-au-tag="${esc(a)}" title="试听">▶</button>`;
            } else if (ambExp) {
              playBtn = ` <span class="muted" title="未找到音频">🔇</span>`;
            }
          }
          return `<label class="modal-check"><input type="checkbox" value="${esc(a)}" data-amb ${checked}> ${esc(ambZh(a))}${rec2} <span class="muted">(${esc(a)})</span>${playBtn}</label>`;
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

  // ---- item audio gaps ----
  const itemGaps: { labelZh: string; itemIds: string[]; missingDirs: string[]; missingAmb: string[] }[] = [];
  for (const { rule, itemIds: hitIds } of itemReq.hits) {
    const missingDirs: string[] = [];
    const missingAmb: string[] = [];
    const resolveDirs = (ids: string[]) => {
      for (const id of ids) {
        const e = dirById.get(id);
        if (e && !state.dirGuids.has(e.guid) && !curDirIdSet.has(id)) missingDirs.push(id);
      }
    };
    if (rule.theme) {
      const t = knowledge.themes.find((t2) => t2.key === rule.theme);
      if (t) {
        resolveDirs(t.directories);
        for (const a of t.ambiences) {
          if (!state.ambiences.has(a) && !curAmbSet.has(a)) missingAmb.push(a);
        }
      }
    }
    if (rule.directories) resolveDirs(rule.directories);
    if (rule.ambiences) {
      for (const a of rule.ambiences) {
        if (!state.ambiences.has(a) && !curAmbSet.has(a)) missingAmb.push(a);
      }
    }
    if (missingDirs.length || missingAmb.length)
      itemGaps.push({ labelZh: rule.labelZh, itemIds: hitIds, missingDirs, missingAmb });
  }
  const allGapsCount = gaps.length + itemGaps.length;
  const itemGapHtml = itemGaps.length
    ? itemGaps
        .map(
          (g) =>
            `<div class="amb-group"><div class="amb-group-title">🔧 ${esc(g.labelZh)} <span class="muted">(${g.itemIds.map(esc).join("、")})</span></div>` +
            (g.missingDirs.length
              ? `<div class="dep-warn dep-miss">缺音效集：${g.missingDirs.map((id) => esc(dirById.get(id)?.nameZh || id)).join("、")}</div>`
              : "") +
            (g.missingAmb.length
              ? `<div class="dep-warn dep-miss">缺氛围音：${g.missingAmb.map((a) => esc(ambZh(a))).join("、")}</div>`
              : "") +
            `</div>`
        )
        .join("")
    : "";

  // ---- render ----
  const detectedThemes = [...themeSignals.themes, ...(themeSignals.raft ? ["raft"] : "")].filter(Boolean);
  const recHint = detectedThemes.length
    ? `检测到主题：${detectedThemes.map((t) => esc(themeLabelZh(t))).join("、")}`
    : "未检测到明显主题（可手动选择）";
  // 已选 BGM 的关卡默认打开「音乐」tab，直接显示当前 BGM
  const defaultTab = state.musicGuid ? "music" : "check";

  openModal(
    `音频配置 · ${detail.levelName || detail.levelNameZH}`,
    `
    <p class="modal-hint">写入场景的 <code>PseudoPrefabManagerStub</code>。保存时会自动打开/保存场景、Reload，并<b>自动把所选 BGM / 音效集所需 bundle 并入 <code>LevelInfoSO.dependencies</code></b>（只增不删）。</p>
    <div class="cfg-tabs">
      <button type="button" class="cfg-tab-btn ${defaultTab === "check" ? "active" : ""}" data-tab="check">🔍 检查</button>
      <button type="button" class="cfg-tab-btn ${defaultTab === "music" ? "active" : ""}" data-tab="music">🎵 音乐 / 特效</button>
      <button type="button" class="cfg-tab-btn" data-tab="mand">📦 强制音效集</button>
      <button type="button" class="cfg-tab-btn" data-tab="dirs">🔊 音效集</button>
      <button type="button" class="cfg-tab-btn" data-tab="amb">🌬️ 氛围音</button>
    </div>

    <!-- === TAB: 检查 === -->
    <div class="au-pane" data-pane="check" ${defaultTab === "check" ? "" : 'style="display:none"'}>
      <div class="modal-actions"><button type="button" class="m-btn small" id="au-apply-rec">✨ 应用主题推荐</button> <span class="muted small">${recHint}</span></div>

      <p class="modal-hint" style="margin-top:8px">覆盖检查${allGapsCount ? ` · <span class="dep-miss">有 ${allGapsCount} 处缺失</span>` : ""} <button type="button" class="link-btn" id="au-fix-gaps" ${allGapsCount ? "" : "disabled"}>添加所有缺失</button></p>
      <div class="dep-box">${gapHtml || itemGapHtml ? (gapHtml + itemGapHtml) : '<div class="dep-ok">所有主题与物品音频均已覆盖。</div>'}</div>
      ${depsHtml}
    </div>

    <!-- === TAB: 音乐 / 特效 === -->
    <div class="au-pane" data-pane="music" ${defaultTab === "music" ? "" : 'style="display:none"'}>
      <div class="audio-grid">
        <label class="m-field">关卡 BGM (InLevelMusicSO)<select id="au-music">${bgmOptHtml}</select></label>
        <div id="au-music-warn" class="dep-warn dep-miss" style="display:none"></div>
        <label class="m-field">死亡特效 (OnDeathEffectSO)<select id="au-death">${deathOptHtml}</select>
          <span class="muted small">快捷：<button type="button" class="link-btn" data-death-theme="water">水面</button> / <button type="button" class="link-btn" data-death-theme="goo">黏液</button></span>
        </label>
      </div>
    </div>

    <!-- === TAB: 强制音效集 === -->
    <div class="au-pane" data-pane="mand" style="display:none">
      <div class="modal-scroll">
        <div class="mand-heading">强制音效集（5，锁定）—— 全局玩法 / 脚步 / 语音 / 厨房氛围 / UI</div>
        ${mandatoryHtml || '<p class="muted">未找到强制音效集（请检查 audio-knowledge.json 与 common01 目录）</p>'}
      </div>
    </div>

    <!-- === TAB: 音效集 === -->
    <div class="au-pane" data-pane="dirs" style="display:none">
      <div class="modal-scroll">
        <div class="mand-heading">主题音效集 AudioDirectorySOs（叠加在强制 5 之上）</div>
        ${dirGroupsHtml || '<p class="muted">无</p>'}
      </div>
    </div>

    <!-- === TAB: 氛围音 === -->
    <div class="au-pane" data-pane="amb" style="display:none">
      <p class="modal-hint">氛围音 InLevelAmbiences</p>
      ${droppedAmb.length ? `<div class="dep-warn dep-miss">已移除 ${droppedAmb.length} 个无音频资源的无效氛围音（运行时会导致崩溃）：${droppedAmb.map((a) => esc(ambZh(a))).join("、")}，保存后生效。</div>` : ""}
      <div class="modal-scroll">${ambGroupsHtml || '<p class="muted">无</p>'}</div>
    </div>

    ${exports ? "" : `<div class="dep-warn dep-miss" style="margin-top:10px">🎧 试听不可用：尚未导出音频数据。请在 Unity Editor 的 Bridge 窗口（菜单 Layout Editor → Open Bridge）点击「导出音频依赖」，完成后刷新页面即可试听。</div>`}
    <div class="au-player ${exports ? "" : "au-player-hidden"}" id="au-player">
      <button type="button" class="au-play-btn" id="au-player-btn" title="播放/暂停">▶</button>
      <span class="au-player-label" id="au-player-label"></span>
      <span class="au-player-time" id="au-player-time">--:-- / --:--</span>
      <input type="range" class="au-player-progress" id="au-player-progress" min="0" max="100" value="0" step="0.1" />
    </div>
    `,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>保存</button>`
  );
  document.querySelector(".modal-panel")?.classList.add("wide");

  // ---- tab switching ----
  document.querySelectorAll<HTMLButtonElement>(".cfg-tab-btn").forEach((btn) =>
    btn.addEventListener("click", () => {
      document.querySelectorAll(".cfg-tab-btn").forEach((b) => b.classList.toggle("active", b === btn));
      document.querySelectorAll<HTMLElement>(".au-pane").forEach((p) => {
        p.style.display = p.dataset.pane === btn.dataset.tab ? "" : "none";
      });
    })
  );

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
      if (ambUniverse.includes(a)) {
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

  // fix all detected coverage gaps (theme + item gaps)
  document.getElementById("au-fix-gaps")?.addEventListener("click", () => {
    let n = 0;
    const fixDir = (id: string) => {
      const e = dirById.get(id);
      if (!e) return false;
      state.dirGuids.add(e.guid);
      const cb = document.querySelector<HTMLInputElement>(`[data-dir][value="${e.guid}"]`);
      if (cb) cb.checked = true;
      return true;
    };
    const fixAmb = (a: string) => {
      if (!ambUniverse.includes(a)) return false;
      state.ambiences.add(a);
      const cb = document.querySelector<HTMLInputElement>(`[data-amb][value="${a}"]`);
      if (cb) cb.checked = true;
      return true;
    };
    for (const g of gaps) {
      for (const id of g.missingDirs) if (fixDir(id)) n++;
      for (const a of g.missingAmb) if (fixAmb(a)) n++;
    }
    for (const g of itemGaps) {
      for (const id of g.missingDirs) if (fixDir(id)) n++;
      for (const a of g.missingAmb) if (fixAmb(a)) n++;
    }
    setStatus(n ? `已补齐 ${n} 项缺失音效集/氛围音` : "无缺失需要补齐");
  });

  refreshMusicWarn();

  // ---- Audio player ----
  if (exports) {
    const playerEl = document.getElementById("au-player")!;
    const audio = document.createElement("audio");
    audio.preload = "auto";
    audio.style.display = "none";
    playerEl.appendChild(audio); // 挂在弹窗内，随弹窗一起销毁
    // 弹窗关闭（取消/保存/点背景/程序关闭）时停止播放并释放
    const disposeAudio = (): void => {
      try {
        audio.pause();
        audio.removeAttribute("src");
        audio.load();
      } catch {
        /* ignore */
      }
      audio.remove();
    };
    const modalRoot = document.getElementById("modal-root");
    if (modalRoot) {
      const observer = new MutationObserver(() => {
        if (!modalRoot.hasChildNodes()) {
          observer.disconnect();
          disposeAudio();
        }
      });
      observer.observe(modalRoot, { childList: true });
    }
    const playerBtn = document.getElementById("au-player-btn") as HTMLButtonElement;
    const playerLabel = document.getElementById("au-player-label")!;
    const playerTime = document.getElementById("au-player-time")!;
    const playerProgress = document.getElementById("au-player-progress") as HTMLInputElement;
    let currentPlaying: string | null = null;

    const bgmByGuid = new Map<string, string>();
    for (const b of exports.bgm) bgmByGuid.set(b.guid, b.filename);
    const ambByTag = new Map<string, string>();
    for (const a of exports.ambiences) if (a.found && a.filename) ambByTag.set(a.tag, a.filename);

    function formatTime(s: number): string {
      if (!isFinite(s) || s < 0) return "--:--";
      const m = Math.floor(s / 60);
      const sec = Math.floor(s % 60);
      return `${m}:${sec.toString().padStart(2, "0")}`;
    }

    function showPlayer(label: string) {
      playerEl.classList.remove("au-player-hidden");
      playerLabel.textContent = label;
      playerTime.textContent = "--:-- / --:--";
      playerProgress.value = "0";
    }

    let pendingPlayResolve: (() => void) | null = null;

    audio.addEventListener("canplay", () => {
      if (pendingPlayResolve) {
        pendingPlayResolve();
        pendingPlayResolve = null;
      }
    });

    audio.addEventListener("error", () => {
      var code = audio.error ? audio.error.code : -1;
      var msg = code === 4 ? "音频格式不支持" : code === 3 ? "音频解码失败" : code === 2 ? "网络错误" : "加载失败";
      setStatus("音频 " + msg + " (code: " + code + ")", false);
      playerBtn.textContent = "▶";
      currentPlaying = null;
      pendingPlayResolve = null;
    });

    function playUrl(url: string, label: string, key: string) {
      if (currentPlaying === key) {
        if (audio.paused) {
          audio.play().catch(function(e) { setStatus("播放失败: " + e.message, false); });
          playerBtn.textContent = "⏸";
        } else {
          audio.pause();
          playerBtn.textContent = "▶";
        }
        return;
      }
      audio.src = "";
      audio.load();
      currentPlaying = key;
      showPlayer(label);
      playerBtn.textContent = "⏸";
      pendingPlayResolve = null;
      audio.src = url;
      audio.load();
      var playPromise = audio.play();
      if (playPromise !== undefined) {
        playPromise.catch(function() {
          setStatus("自动播放被阻止，请再次点击播放", false);
        });
      }
    }

    audio.addEventListener("timeupdate", () => {
      if (!audio.duration || !isFinite(audio.duration)) return;
      const pct = (audio.currentTime / audio.duration) * 100;
      playerProgress.value = String(Math.min(pct, 100));
      playerTime.textContent = `${formatTime(audio.currentTime)} / ${formatTime(audio.duration)}`;
    });

    audio.addEventListener("ended", () => {
      playerBtn.textContent = "▶";
      currentPlaying = null;
    });

    audio.addEventListener("loadedmetadata", () => {
      playerTime.textContent = `0:00 / ${formatTime(audio.duration)}`;
    });

    playerBtn.addEventListener("click", () => {
      if (!audio.src && !currentPlaying) return;
      if (audio.paused) {
        audio.play().catch(() => {});
        playerBtn.textContent = "⏸";
      } else {
        audio.pause();
        playerBtn.textContent = "▶";
      }
    });

    playerProgress.addEventListener("input", () => {
      if (!audio.duration || !isFinite(audio.duration)) return;
      const t = (Number(playerProgress.value) / 100) * audio.duration;
      audio.currentTime = t;
    });

    // ---- BGM play button ----
    // ---- BGM auto-play on selection change ----
    let lastAutoPlayedGuid = state.musicGuid;
    function tryPlayBgm() {
      const guid = state.musicGuid;
      if (!guid || guid === lastAutoPlayedGuid) return;
      lastAutoPlayedGuid = guid;
      const m = musicByGuid.get(guid);
      const filename = bgmByGuid.get(guid);
      if (!filename || !m) return;
      playUrl(api.getAudioStreamUrl(filename), m.nameZh, `bgm:${guid}`);
    }
    document.getElementById("au-music")?.addEventListener("change", () => tryPlayBgm());

    // Auto-play when switching to music tab
    document.querySelector<HTMLButtonElement>('[data-tab="music"]')?.addEventListener("click", () => {
      setTimeout(() => tryPlayBgm(), 0);
    });

    // ---- SFX expand/collapse ----
    document.querySelectorAll<HTMLElement>("[data-au-expand]").forEach((btn) => {
      btn.addEventListener("click", (e) => {
        e.stopPropagation();
        const id = btn.dataset.auExpand!;
        const container = document.querySelector<HTMLElement>(`[data-au-clips="${id}"]`);
        if (!container) return;
        const sfx = sfxById.get(id);
        if (!sfx) return;

        const isOpen = container.style.display !== "none";
        if (isOpen) {
          container.style.display = "none";
          btn.textContent = "▶ 试听";
          return;
        }

        if (!container.innerHTML) {
          const typeLabel: Record<string, string> = {
            oneshot: "单次",
            looping: "循环",
            looping_start: "开始",
            looping_end: "结尾",
          };
          container.innerHTML = sfx.clips
            .map(
              (c) =>
                `<span class="au-clip-row"><span class="muted">[${typeLabel[c.type] || c.type}]</span> ${esc(c.tag)} <button type="button" class="au-play-btn small" data-au-play="sfx" data-au-path="${esc(c.filename)}" title="试听">▶</button></span>`
            )
            .join("");
          // wire newly created play buttons
          container.querySelectorAll<HTMLElement>("[data-au-play=\"sfx\"]").forEach((pb) => {
            pb.addEventListener("click", (ev) => {
              ev.stopPropagation();
              const path = pb.dataset.auPath!;
              playUrl(api.getAudioStreamUrl(path), path.split("/").pop() || path, `sfx:${path}`);
            });
          });
        }
        container.style.display = "";
        btn.textContent = "▲ 收起";
      });
    });

    // ---- Ambience play buttons ----
    document.querySelectorAll<HTMLElement>("[data-au-play=\"ambience\"]").forEach((btn) => {
      btn.addEventListener("click", (e) => {
        e.stopPropagation();
        const tag = btn.dataset.auTag!;
        const filename = ambByTag.get(tag);
        if (!filename) { setStatus(`未找到 ${tag} 的音频`, false); return; }
        const label = ambZh(tag);
        playUrl(api.getAudioStreamUrl(filename), label, `ambience:${tag}`);
      });
    });
  }

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
          minOrderCount: detail.minOrderCount,
          maxOrderCount: detail.maxOrderCount,
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
