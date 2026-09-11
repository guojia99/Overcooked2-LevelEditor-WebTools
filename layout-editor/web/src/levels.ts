import * as api from "./api";
import type {
  AudioDirectoryEntry,
  AudioExportManifest,
  AudioExportSfxDir,
  AudioItemRule,
  AudioKnowledge,
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
  WriteBackHistoryDetail,
  WriteBackDiffEntry,
  WriteBackHistoryItem,
} from "./types";
import { closeModal, openModal } from "./modals";
import { openDepsCheckModal } from "./editor/ui/depsCheck";
import { showBusy, hideBusy, setBusyMessage } from "./busy";
import { suspendBridgeWatch, resumeBridgeWatch } from "./editor/sceneIO";
import { navHtml, wireNav } from "./nav";
import { navigateTo } from "./route";

const DEPS_TARGET_KEY = "depsTargetLevel";

function goDependenciesPage(setName?: string, levelInfoAssetPath?: string): void {
  if (setName && levelInfoAssetPath) {
    sessionStorage.setItem(DEPS_TARGET_KEY, JSON.stringify({ setName, assetPath: levelInfoAssetPath }));
  }
  location.assign("/dependencies");
}
import { applyRatio, computeAutoScores, computeOrderLifeTimes, ORDER_INTERVAL_SEC, PLATE_RETURN_SEC, round5, RATIO_MAX, RATIO_MIN, RATIO_STEP } from "./autoScore";
import { analyzeKitchen, kitchenChips, kitchenWarnings } from "./kitchenAnalysis";
import { modelParamsSummary } from "./autoScoreKnowledge";
import { groupRecipesByType, recipeTypeLabel } from "./recipeTypes";
import { foodGroupLabel } from "./ingredientLabels";
import { computeCardGroups, rlCardHtml, rlSectionHtml, STEP_ICON_SRC, type RecipeWithGroups } from "./recipeCard";
import { exportSummaryPng, type SummaryCard, type SummaryExportData } from "./summaryExport";
import { exportLevelShotsPng, type LevelShotExportData } from "./levelShotExport";
import { customRecipeIconUrl } from "./editor/catalog";
import { normalizeCustomRecipeCard } from "./recipeCardCustom";
import { screenshotPaneHtml, wireScreenshotPane } from "./editor/ui/screenshotModal";

const TARGET_SCENE_KEY = "layoutTargetScene";

export function goLayout(sceneAssetPath?: string): void {
  if (sceneAssetPath) sessionStorage.setItem(TARGET_SCENE_KEY, sceneAssetPath);
  else sessionStorage.removeItem(TARGET_SCENE_KEY);
  location.assign("/layout");
}

export function goManage(): void {
  location.assign("/manage");
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
    else if (target === "dependencies") goDependenciesPage();
    else navigateTo(target);
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
        <h3 title="${esc((s.levelSetNameZH || "") + " " + (s.levelSetName || s.setName))}">${esc(s.levelSetNameZH || s.setName)} <span class="muted">(${esc(s.levelSetName || s.setName)})</span></h3>
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
     <div class="m-section-title">CustomStub（随机食材箱等关卡代码）</div>
     <p class="modal-hint" id="exp-stub-status">正在查询状态…</p>
     <div class="m-actions-row">
       <button type="button" class="m-btn" id="exp-stub-copy">拷贝到关卡集</button>
       <button type="button" class="m-btn" id="exp-stub-compile">编译 Stub DLL</button>
     </div>
     <p class="modal-hint">zip 内含 <code>levels/${esc(s.setName)}/</code>（关卡 bundle，按需含 runtime）、<code>commonW1</code>（问号图标库等）与 <code>OC2LevelRuntimeLoader.dll</code>（运行时注入插件）。将整个 zip <b>解压到游戏 <code>BepInEx/plugins/OC2DIYLevel/</code> 目录</b>即完成全部安装。</p>`,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>开始导出</button>`
  );
  wireExportStubTools(s.setName);
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

/** 导出弹窗内的 CustomStub 工具（拷贝/同步/编译 DLL；每个关卡集首次需拷贝一次）。
 *  写盘会触发 Unity 重编译与域重载（HTTP 连接重置属预期），故先容忍断连、
 *  再轮询 health 恢复，编译按钮进一步等 DLL 新鲜。 */
function wireExportStubTools(setName: string): void {
  const statusEl = document.getElementById("exp-stub-status");
  const copyBtn = document.getElementById("exp-stub-copy") as HTMLButtonElement | null;
  const compileBtn = document.getElementById("exp-stub-compile") as HTMLButtonElement | null;
  if (!statusEl || !copyBtn || !compileBtn) return;

  const setBusyState = (busy: boolean): void => {
    copyBtn.disabled = busy;
    compileBtn.disabled = busy;
  };

  const refreshStatus = async (): Promise<void> => {
    try {
      const st = await api.fetchSetStubStatus(setName);
      copyBtn.textContent = st.configured ? "同步更新" : "拷贝到关卡集";
      if (!st.configured) {
        statusEl.textContent = "未拷贝 —— 关卡含随机食材箱等玩法时需先「拷贝到关卡集」（每集仅首次需要），普通关卡可直接导出。";
        return;
      }
      const parts = [`已配置 ${st.asmName}`];
      parts.push(st.drifted ? "副本与母本有漂移，建议「同步更新」" : "副本与母本一致");
      parts.push(
        st.dllState === "fresh" ? "DLL 已就绪"
        : st.dllState === "stale" ? "DLL 过期（源码已修改，需重新编译）"
        : st.dllState === "missing" ? "DLL 未编译（点「编译 Stub DLL」）"
        : "无 stub 源码"
      );
      statusEl.textContent = parts.join(" · ");
    } catch (e) {
      statusEl.textContent = `CustomStub 工具不可用：${(e as Error).message}（不影响普通关卡导出）`;
      copyBtn.disabled = true;
      compileBtn.disabled = true;
    }
  };

  /** 等域重载结束（health 恢复）；waitFreshDll=true 时再等 DLL 变 fresh。 */
  const waitSettled = async (waitFreshDll: boolean): Promise<void> => {
    const deadline = Date.now() + 3 * 60 * 1000;
    for (;;) {
      if (Date.now() > deadline) throw new Error("等待 Unity 编译超时（3 分钟），请查看 Unity Console。");
      await new Promise((r) => setTimeout(r, 1500));
      if (!(await api.fetchHealth())) continue; // 域重载中，连接重置属预期
      if (!waitFreshDll) return;
      try {
        const st = await api.fetchSetStubStatus(setName);
        if (st.dllState === "fresh" || st.dllState === "noStub") return;
      } catch {
        // 服务尚未完全就绪，继续等
      }
    }
  };

  const run = async (compile: boolean): Promise<void> => {
    setBusyState(true);
    suspendBridgeWatch(); // 域重载期间健康探测会误报掉线
    statusEl.textContent = compile
      ? "编译 Stub DLL 中（同步母本 → Unity 编译 → 打包 runtime，稍候自动刷新）…"
      : "拷贝/同步中（可能触发 Unity 重编译，稍候自动刷新）…";
    try {
      let msg = "";
      try {
        msg = compile ? await api.stubCompileDll(setName) : await api.stubCopyToSet(setName);
      } catch (e) {
        if (!(e instanceof TypeError)) throw e; // 仅容忍域重载造成的网络中断
      }
      await waitSettled(compile);
      statusEl.textContent = msg ? `完成：${msg}` : "完成";
    } catch (e) {
      statusEl.textContent = `失败：${(e as Error).message}`;
    } finally {
      resumeBridgeWatch();
      setBusyState(false);
      await refreshStatus();
    }
  };

  copyBtn.addEventListener("click", () => void run(false));
  compileBtn.addEventListener("click", () => void run(true));
  void refreshStatus();
}

function openCreateSetModal(app: HTMLElement): void {  openModal(
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

  let setInfo: LevelSetInfo | undefined;
  try {
    setInfo = (await api.fetchSets()).find((s) => s.setName === setName);
  } catch {
    /* 忽略：回退为仅显示 ID */
  }
  let setDisplay = esc(setName);
  if (setInfo?.levelSetNameZH) setDisplay = esc(setInfo.levelSetNameZH);
  if (setInfo?.levelSetName) setDisplay += ` <span class="muted">(${esc(setInfo.levelSetName)})</span>`;
  setDisplay += ` <span class="muted">[${esc(setName)}]</span>`;

  const levelIds = levels.map((lv, idx) => lv.dataDir.split("/").pop() || `level${idx}`);

  const cards = levels
    .map((lv, idx) => {
      const id = levelIds[idx];
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
          <span class="muted">第 ${idx + 1} 关 · ${esc(lv.sceneName)} · ${esc(id)}</span>
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
      ${levels.length > 1 ? '<button class="m-btn" id="reorder-levels">⇅ 调整顺序</button>' : ""}
      ${levels.length > 0 ? '<button class="m-btn" id="shots-export">🖼 一键导出关卡截图</button>' : ""}
      <span class="muted">当前关卡集：<b>${setDisplay}</b></span>
    </div>
    <div class="m-section-title">关卡</div>
    <div class="m-grid m-level-grid">${cards || '<p class="muted">暂无关卡</p>'}</div>
  `;

  document.getElementById("new-level")?.addEventListener("click", () => openCreateLevelModal(app, setName));
  document.getElementById("reorder-levels")?.addEventListener("click", () => openReorderModal(app, setName, levels));
  document.getElementById("shots-export")?.addEventListener("click", async () => {
    const btn = document.getElementById("shots-export") as HTMLButtonElement | null;
    if (btn) btn.disabled = true;
    setStatus("正在合成关卡截图…");
    try {
      const grid = content.querySelector<HTMLElement>(".m-level-grid");
      const exportData: LevelShotExportData = {
        title: setInfo?.levelSetNameZH || setInfo?.levelSetName || setName,
        sub: `${setInfo?.levelSetName || setName} · 共 ${levels.length} 关`,
        // 仅中英文名 + 截图：不带 s_* 标识/序号元信息与按钮（无名的关卡回退为「第 N 关」）
        cards: levels.map((lv, idx) => ({
          screenshotUrl: lv.screenshotPath ? api.imageFloorUrl(lv.screenshotPath) : "",
          nameZh: lv.levelNameZH || lv.levelName || `第 ${idx + 1} 关`,
          nameEn: lv.levelNameZH && lv.levelName ? lv.levelName : "",
        })),
      };
      const width = grid ? grid.getBoundingClientRect().width : 1200;
      const fileName = `${exportData.title}_关卡截图.png`;
      await exportLevelShotsPng(exportData, width, fileName);
      setStatus(`已导出 ${fileName}（${levels.length} 关）`);
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      if (btn) btn.disabled = false;
    }
  });

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

function openReorderModal(app: HTMLElement, setName: string, levels: LevelSummary[]): void {
  const initialIds = levels.map((lv, idx) => lv.dataDir.split("/").pop() || `level${idx}`);

  const rows = levels
    .map((lv, idx) => {
      const id = initialIds[idx];
      const title = lv.levelNameZH || lv.levelName || id;
      const shot = lv.screenshotPath ? api.imageFloorUrl(lv.screenshotPath) : "";
      return `
      <div class="m-reorder-row" draggable="true" data-id="${esc(id)}">
        <span class="m-reorder-handle" title="上下拖拽调整顺序">⠿</span>
        <span class="m-reorder-num">${idx + 1}</span>
        ${shot ? `<img class="m-reorder-shot" src="${esc(shot)}" alt="" loading="lazy">` : '<span class="m-reorder-shot empty"></span>'}
        <div class="m-row-main">
          <div class="m-row-title">${esc(title)}</div>
          <div class="m-row-sub">${esc(lv.sceneName)} · ${esc(id)}</div>
        </div>
        <span class="m-reorder-btns">
          <button type="button" class="m-btn" data-rup title="上移一位">↑</button>
          <button type="button" class="m-btn" data-rdown title="下移一位">↓</button>
        </span>
      </div>`;
    })
    .join("");

  openModal(
    `调整关卡顺序 · ${esc(setName)}`,
    `
    <p class="modal-hint">按住 ⠿ 或整行上下拖拽，调整到满意后点「保存顺序」一次性写入（列表顶部为第 1 关）。</p>
    <div class="modal-scroll"><div class="m-reorder-list" id="reorder-list">${rows}</div></div>
    `,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn primary" data-ok>保存顺序</button>`
  );

  const list = document.getElementById("reorder-list")!;

  function renumber(): void {
    list.querySelectorAll(".m-reorder-row").forEach((row, i) => {
      const num = row.querySelector(".m-reorder-num");
      if (num) num.textContent = String(i + 1);
    });
  }

  function moveRow(row: Element, delta: number): void {
    const rows = Array.from(list.querySelectorAll(".m-reorder-row"));
    const idx = rows.indexOf(row);
    const to = idx + delta;
    if (idx < 0 || to < 0 || to >= rows.length) return;
    if (delta < 0) list.insertBefore(row, rows[to]);
    else list.insertBefore(row, rows[to].nextSibling);
    renumber();
  }

  list.querySelectorAll<HTMLButtonElement>("[data-rup]").forEach((b) =>
    b.addEventListener("click", () => moveRow(b.closest(".m-reorder-row")!, -1))
  );
  list.querySelectorAll<HTMLButtonElement>("[data-rdown]").forEach((b) =>
    b.addEventListener("click", () => moveRow(b.closest(".m-reorder-row")!, 1))
  );

  // HTML5 拖拽：拖动行经过目标行时按鼠标相对其中线的位置实时换位。
  let dragRow: Element | null = null;
  list.querySelectorAll<HTMLElement>(".m-reorder-row").forEach((row) => {
    row.addEventListener("dragstart", (e) => {
      dragRow = row;
      row.classList.add("dragging");
      if (e.dataTransfer) {
        e.dataTransfer.effectAllowed = "move";
        e.dataTransfer.setData("text/plain", row.dataset.id ?? "");
      }
    });
    row.addEventListener("dragend", () => {
      row.classList.remove("dragging");
      dragRow = null;
      renumber();
    });
    row.addEventListener("dragover", (e) => {
      e.preventDefault();
      if (!dragRow || dragRow === row) return;
      const rect = row.getBoundingClientRect();
      const before = e.clientY < rect.top + rect.height / 2;
      if (before) list.insertBefore(dragRow, row);
      else list.insertBefore(dragRow, row.nextSibling);
    });
  });

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", async () => {
    const ids = Array.from(list.querySelectorAll(".m-reorder-row")).map((r) => (r as HTMLElement).dataset.id ?? "");
    if (ids.join("\n") === initialIds.join("\n")) {
      closeModal();
      setStatus("顺序未变化");
      return;
    }
    const okBtn = document.querySelector<HTMLButtonElement>("[data-ok]");
    if (okBtn) okBtn.disabled = true;
    try {
      showBusy("保存关卡顺序…");
      await api.reorderLevels(setName, ids);
      closeModal();
      setStatus("已保存关卡顺序");
      await renderLevelList(app, setName);
    } catch (e) {
      setStatus((e as Error).message, false);
      if (okBtn) okBtn.disabled = false;
    } finally {
      hideBusy();
    }
  });
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

  content.innerHTML = `
    <div class="m-actions-row">
      <button class="m-btn" id="btn-layout">打开关卡编辑器</button>
      <button class="m-btn" id="btn-level-config">📊 关卡配置</button>
      <button class="m-btn" id="btn-deps">📦 依赖管理</button>
      <button class="m-btn" id="btn-summary">📋 汇总</button>
      <button class="m-btn" id="btn-tools-history" title="修复损坏 / 依赖检查 / 测试布局 / 同步布局 + 写回历史对比">🧰 工具与历史</button>
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
        <label class="m-field">动态父挂载 disableDynamicParenting
          <label class="modal-check"><input type="checkbox" id="f-disableDynamicParenting" ${detail.disableDynamicParenting ? "checked" : ""}> 勾选=禁用（含移动/升降平台、可移动火锅的关卡应取消；写回时检测到可移动火锅会自动取消）</label>
        </label>
        <p class="modal-hint">Bundle 依赖（<code>dependencies</code>）请在顶栏 <b>📦 依赖管理</b> 中编辑。当前共 ${(detail.dependencies || []).length} 项。</p>
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
        dependencies: detail.dependencies || [],
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
  document.getElementById("btn-deps")?.addEventListener("click", () => goDependenciesPage(setName, assetPath));
  document.getElementById("btn-summary")?.addEventListener("click", () => void renderLevelSummary(app, setName, assetPath));
  document.getElementById("btn-tools-history")?.addEventListener("click", () => openToolsHistoryModal(detail));
  document.getElementById("btn-level-config")?.addEventListener("click", () =>
    void openConfigTabsModal(detail, setName, () => {
      void renderLevelDetail(app, setName, assetPath);
    })
  );
}

// ==================== Level recipe summary (汇总页) ====================

// ==================== Level tools & write-back history (🧰 工具与历史) ====================

const AUTO_ACTION_KEY = "layoutAutoAction";

/** 从关卡管理跳转布局编辑器时携带的自动动作（编辑器加载场景完成后触发一次）。 */
export function setLayoutAutoAction(action: string): void {
  sessionStorage.setItem(AUTO_ACTION_KEY, action);
}

export function consumeLayoutAutoAction(): string | null {
  const v = sessionStorage.getItem(AUTO_ACTION_KEY);
  if (v) sessionStorage.removeItem(AUTO_ACTION_KEY);
  return v;
}

/** 与后端 LayoutEditorWriteBackHistory.LevelDirFor 同口径：场景文件名去掉 s_ 前缀。 */
function writeBackLevelId(sceneAssetPath: string): string {
  const fileName = (sceneAssetPath.split("/").pop() ?? "").replace(/\.unity$/, "");
  return fileName.startsWith("s_") && fileName.length > 2 ? fileName.slice(2) : fileName || "_unknown";
}

function writeBackSet(sceneAssetPath: string): string {
  const parts = sceneAssetPath.replace(/\\/g, "/").split("/");
  return parts.length > 2 && parts[1] === "LevelSets" ? parts[2] : "_misc";
}

function fmtBytes(n: number): string {
  return n > 0 ? `${(n / 1024).toFixed(0)}KB` : "—";
}

function endpointBadge(ep: string): string {
  const zh: Record<string, string> = {
    layout: "布局",
    death: "死亡主题",
    killplane: "坠落区",
    repair: "修复损坏",
    "level-info": "基础信息",
    "level-config": "关卡配置",
    "level-audio": "音频",
    "level-recipes": "菜谱",
    "optional-items": "可选清单",
    matchlists: "匹配表",
    screenshot: "截图",
  };
  return `<span class="wb-badge">${zh[ep] ?? ep}</span>`;
}

/** 工具与历史弹窗的可选上下文：编辑器内打开时直接执行（关弹窗后原地触发）；
 *  关卡管理页打开时缺省 —— 测试布局/同步布局退化为跳转编辑器 + 自动动作，
 *  恢复快照仅在编辑器内提供（关卡管理页显示提示）。 */
export interface ToolsHistoryOptions {
  onTestLayout?: () => void;
  onSyncLayout?: () => void;
  /** 编辑器内修复成功后的后续（重载场景等）；n = 移除数量。 */
  onRepaired?: (n: number) => void;
  /** 把一条历史快照恢复到编辑器画布（仅前端状态，用户手动写回生效）。 */
  onRestore?: (record: string, side: "before" | "after", label?: string) => void;
}

export function openToolsHistoryModal(detail: LevelDetail, opts?: ToolsHistoryOptions): void {
  const wbSet = writeBackSet(detail.sceneAssetPath);
  const levelId = writeBackLevelId(detail.sceneAssetPath);
  const title = detail.levelNameZH || detail.levelName || levelId;
  openModal(
    `🧰 工具与历史 · ${esc(title)}`,
    `
    <div class="wb-tabs">
      <button type="button" class="wb-tab active" data-wb-tab="tools">🧰 工具</button>
      <button type="button" class="wb-tab" data-wb-tab="history">🕘 写回历史</button>
    </div>
    <div id="wb-tools" class="wb-pane">
      <div class="wb-tool-row">
        <button type="button" class="m-btn" id="wb-repair">🔧 修复损坏</button>
        <div class="muted">移除该关卡场景中源预制件缺失的损坏实例（pseudoPrefabSO 空引用报错）。</div>
      </div>
      <div class="wb-tool-row">
        <button type="button" class="m-btn" id="wb-deps">🩺 依赖检查</button>
        <div class="muted">检查后端服务 / Web 构建 / 菜谱库 / bundle / 音频等环境依赖是否就绪。</div>
      </div>
      <div class="wb-tool-row">
        <button type="button" class="m-btn" id="wb-test">🧪 测试布局</button>
        <div class="muted">打开布局编辑器并一键生成 30×16 测试沙盘（全部食材箱 + 核心层道具，写回后生效）。</div>
      </div>
      <div class="wb-tool-row">
        <button type="button" class="m-btn" id="wb-sync">📥 同步布局</button>
        <div class="muted">打开布局编辑器，从其他关卡复制道具、地板与背景主题（写回后生效）。</div>
      </div>
      <p class="modal-hint" id="wb-tool-status"></p>
    </div>
    <div id="wb-history" class="wb-pane" style="display:none">
      <div class="wb-hist-toolbar">
        <button type="button" class="m-btn small" id="wb-refresh">↻ 刷新</button>
        <span class="muted">每次写回自动缓存最近 15 条记录（场景/Info 前后快照 + 变动差异）</span>
      </div>
      <div id="wb-hist-body" class="modal-scroll"><p class="muted">加载中…</p></div>
    </div>`,
    `<button type="button" class="modal-btn" data-cancel>关闭</button>`
  );
  // 大弹窗：宽幅 + 高占满（modal-body 自身滚动）。
  document.querySelector("#modal-root .modal-panel")?.classList.add("wide", "wb-xl");
  // data-cancel 无全局委托，须在此绑定关闭（与项目其他弹窗一致）。
  document.querySelector("#modal-root [data-cancel]")?.addEventListener("click", closeModal);

  document.querySelectorAll<HTMLButtonElement>("[data-wb-tab]").forEach((btn) => {
    btn.addEventListener("click", () => {
      document.querySelectorAll<HTMLButtonElement>("[data-wb-tab]").forEach((b) => b.classList.toggle("active", b === btn));
      const tools = document.getElementById("wb-tools");
      const hist = document.getElementById("wb-history");
      if (tools && hist) {
        tools.style.display = btn.dataset.wbTab === "tools" ? "" : "none";
        hist.style.display = btn.dataset.wbTab === "history" ? "" : "none";
      }
      if (btn.dataset.wbTab === "history") void loadHistoryList(wbSet, levelId, opts);
    });
  });

  document.getElementById("wb-repair")?.addEventListener("click", async () => {
    const status = document.getElementById("wb-tool-status");
    try {
      status!.textContent = "修复中…";
      const n = await api.repairBrokenPrefabs(detail.sceneAssetPath);
      if (n > 0 && opts?.onRepaired) {
        opts.onRepaired(n);
        closeModal();
        return;
      }
      status!.textContent =
        n > 0 ? `已移除 ${n} 个损坏的预制件实例（重新加载场景后生效）。` : "未发现损坏的预制件实例。";
    } catch (e) {
      status!.textContent = (e as Error).message;
    }
  });

  document.getElementById("wb-deps")?.addEventListener("click", () => openDepsCheckModal());

  document.getElementById("wb-test")?.addEventListener("click", () => {
    if (opts?.onTestLayout) {
      closeModal();
      opts.onTestLayout();
      return;
    }
    setLayoutAutoAction("test-layout");
    goLayout(detail.sceneAssetPath);
  });

  document.getElementById("wb-sync")?.addEventListener("click", () => {
    if (opts?.onSyncLayout) {
      closeModal();
      opts.onSyncLayout();
      return;
    }
    setLayoutAutoAction("sync-layout");
    goLayout(detail.sceneAssetPath);
  });

  document.getElementById("wb-refresh")?.addEventListener("click", () => void loadHistoryList(wbSet, levelId, opts));
}

async function loadHistoryList(wbSet: string, levelId: string, opts?: ToolsHistoryOptions): Promise<void> {
  const body = document.getElementById("wb-hist-body");
  if (!body) return;
  body.innerHTML = '<p class="muted">加载中…</p>';
  let items: WriteBackHistoryItem[];
  try {
    items = await api.fetchWriteBackHistory(wbSet, levelId);
  } catch (e) {
    body.innerHTML = `<p class="modal-hint err">${esc((e as Error).message)}</p>`;
    return;
  }
  if (items.length === 0) {
    body.innerHTML = '<p class="muted">暂无写回历史 —— 在布局编辑器中对本关写回一次即可生成。</p>';
    return;
  }
  body.innerHTML = items
    .map((it) => {
      const time = (it.beginTime || it.record).slice(0, 19);
      return `
    <div class="wb-rec" data-record="${esc(it.record)}" title="${esc(it.record)}">
      <div class="wb-rec-head">
        <b>${esc(time)}</b>
        ${(it.endpoints ?? []).map(endpointBadge).join("")}
        ${it.semanticAvailable ? "" : '<span class="wb-badge wb-badge-warn">仅快照</span>'}
      </div>
      <div class="wb-rec-stats">
        物品 <span class="wb-add">+${it.itemsAdded}</span> <span class="wb-del">-${it.itemsRemoved}</span>
        <span class="wb-mov">↔${it.itemsMoved}</span> <span class="wb-chg">✎${it.itemsChanged}</span>
        · 地板 +${it.floorsAdded}/-${it.floorsRemoved}
        · 快照 ${fmtBytes(it.sceneBytesBefore)} → ${fmtBytes(it.sceneBytesAfter)}
      </div>
    </div>`;
    })
    .join("");
  body.querySelectorAll<HTMLElement>(".wb-rec").forEach((row) => {
    row.addEventListener("click", () => void showHistoryDetail(wbSet, levelId, row.dataset.record ?? "", opts));
  });
}

/** 变更分类 → 徽章（图标/中文标签/颜色类）。 */
const WB_KINDS: Record<string, { icon: string; label: string; cls: string }> = {
  moved: { icon: "↔", label: "移动", cls: "wb-mov" },
  rotated: { icon: "⟳", label: "旋转", cls: "wb-rot" },
  scaled: { icon: "⤢", label: "缩放", cls: "wb-scale" },
  config: { icon: "✎", label: "参数", cls: "wb-chg" },
  prefab: { icon: "🎨", label: "外观", cls: "wb-pfb" },
  hierarchy: { icon: "⇄", label: "层级", cls: "wb-hier" },
  reid: { icon: "🆔", label: "重编号", cls: "wb-reid" },
};

function kindBadges(kinds: string[] | null | undefined): string {
  return (kinds ?? [])
    .map((k) => {
      const m = WB_KINDS[k];
      return m ? `<span class="wb-kind ${m.cls}">${m.icon} ${m.label}</span>` : "";
    })
    .join("");
}

function wbLegend(): string {
  return `
    <div class="wb-legend">
      <span class="wb-kind wb-add">＋ 新增</span>
      <span class="wb-kind wb-del">－ 删除</span>
      ${kindBadges(["moved"])}
      ${kindBadges(["rotated"])}
      ${kindBadges(["scaled"])}
      ${kindBadges(["config"])}
      ${kindBadges(["prefab"])}
      ${kindBadges(["hierarchy"])}
      <span class="muted">（明细仅列前 3 条字段变化）</span>
    </div>`;
}

function diffEntryRows(entries: { id: string; name: string }[] | null | undefined, sign: string, cls: string): string {
  if (!entries || entries.length === 0) return "";
  return entries
    .map(
      (e) => `<div class="wb-diff-row"><span class="wb-sign ${cls}">${sign}</span><span class="wb-name">${esc(
        e.name || e.id
      )}</span></div>`
    )
    .join("");
}

/** 变更行（✎ 参数 / 🎨 外观 / ⇄ 层级）：kinds 多色徽章 + 前 3 条字段明细。 */
function changedEntryRows(entries: WriteBackDiffEntry[] | null | undefined, sign: string, cls: string): string {
  if (!entries || entries.length === 0) return "";
  return entries
    .map((e) => {
      const detail = e.detail ? `<span class="wb-val muted">${esc(e.detail)}</span>` : "";
      return `<div class="wb-diff-row"><span class="wb-sign ${cls}">${sign}</span><span class="wb-name">${esc(
        e.name || e.id
      )}</span>${kindBadges(e.kinds)}${detail}</div>`;
    })
    .join("");
}

function stringDiffRows(label: string, added: string[] | null | undefined, removed: string[] | null | undefined): string {
  const a = added ?? [];
  const r = removed ?? [];
  if (a.length === 0 && r.length === 0) return "";
  const parts: string[] = [];
  if (a.length) parts.push(`<span class="wb-add">+${a.map(esc).join("、")}</span>`);
  if (r.length) parts.push(`<span class="wb-del">-${r.map(esc).join("、")}</span>`);
  return `<div class="wb-diff-row"><span class="wb-name">${label}</span><span class="wb-val">${parts.join(
    " "
  )}</span></div>`;
}

function renderHistoryDetail(detail: WriteBackHistoryDetail): string {
  const d = detail.diff;
  const m = detail.meta;
  if (!d) {
    return '<p class="modal-hint err">该记录缺少 diff 数据（可能来自早期版本记录）。</p>';
  }
  const reid = d.itemsReid ?? [];
  const itemSection = d.semanticAvailable
    ? `
      ${wbLegend()}
      <h4>物品（${d.itemsUnchanged} 个未变${reid.length ? `、${reid.length} 个重编号` : ""}）</h4>
      ${diffEntryRows(d.itemsAdded, "＋", "wb-add") || '<p class="muted">无新增</p>'}
      ${diffEntryRows(d.itemsRemoved, "－", "wb-del") || ""}
      ${(d.itemsMoved ?? [])
        .map((mv) => {
          const kind = WB_KINDS[mv.kind ?? "moved"] ?? WB_KINDS.moved;
          return `<div class="wb-diff-row"><span class="wb-sign ${kind.cls}">${kind.icon}</span><span class="wb-name">${esc(
            mv.name || mv.id
          )}</span><span class="wb-kind ${kind.cls}">${kind.icon} ${kind.label}</span><span class="wb-val muted">${esc(
            mv.fromPosition
          )} → ${esc(mv.toPosition)}</span></div>`;
        })
        .join("")}
      ${changedEntryRows(d.itemsChanged, "✎", "wb-chg") || ""}
      ${
        reid.length
          ? `<div class="wb-reid-block">${reid
              .map(
                (e) =>
                  `<span class="wb-reid-chip">${kindBadges(["reid"])}${esc(e.name || e.id)}</span>`
              )
              .join("")}</div>
             <p class="modal-hint muted">🆔 重编号 = 仅实例 id 变化（如「恢复到画布→写回」后重新盖章），内容未变，不计入变动。</p>`
          : ""
      }
      <h4>地板（${d.floorsUnchanged} 个未变）</h4>
      ${diffEntryRows(d.floorsAdded, "＋", "wb-add") || '<p class="muted">无新增</p>'}
      ${diffEntryRows(d.floorsRemoved, "－", "wb-del") || ""}
      ${changedEntryRows(d.floorsChanged, "✎", "wb-chg") || ""}
      <h4>相机 / 灯光</h4>
      <div class="wb-diff-row"><span class="wb-name">相机</span><span class="wb-val">${
        d.cameraChanged
          ? `<span class="wb-chg">有变化</span>${d.cameraDetail ? `<span class="muted"> ${esc(d.cameraDetail)}</span>` : ""}`
          : '<span class="muted">未变</span>'
      }</span></div>
      <div class="wb-diff-row"><span class="wb-name">灯光</span><span class="wb-val"><span class="wb-add">+${
        (d.lightsAdded ?? []).length
      }</span> <span class="wb-del">-${(d.lightsRemoved ?? []).length}</span> <span class="wb-chg">✎${
        (d.lightsChanged ?? []).length
      }</span>（${d.lightsUnchanged} 个未变）</span></div>
      ${changedEntryRows(d.lightsChanged, "✎", "wb-chg") || ""}`
    : '<p class="modal-hint">该记录无语义对比数据（semanticAvailable=false，仅文件快照）。</p>';

  const info = d.info;
  const infoSection = !info
    ? ""
    : info.missing
      ? '<h4>关卡信息 (LevelInfoSO)</h4><p class="muted">该记录无 info 摘要（关卡可能未绑定 LevelInfo）。</p>'
      :       `
      <h4>关卡信息 (LevelInfoSO)</h4>
      ${stringDiffRows("菜谱", info.recipesAdded, info.recipesRemoved)}
      ${stringDiffRows("食材", info.ingredientsAdded, info.ingredientsRemoved)}
      ${stringDiffRows("依赖", info.dependenciesAdded, info.dependenciesRemoved)}
      ${stringDiffRows("音频目录", info.audioAdded, info.audioRemoved)}
      ${stringDiffRows("环境音", info.ambiencesAdded, info.ambiencesRemoved)}
      ${stringDiffRows("可选清单", info.optionalItemsAdded, info.optionalItemsRemoved)}
      ${stringDiffRows("匹配表", info.matchlistsAdded, info.matchlistsRemoved)}
      ${
        (info.configsBefore ?? []).some((c, i) => c !== (info.configsAfter ?? [])[i])
          ? (info.configsBefore ?? [])
              .map((c, i) =>
                c !== (info.configsAfter ?? [])[i]
                  ? `<div class="wb-diff-row"><span class="wb-name">配置 ${i + 1}p</span><span class="wb-val">${esc(
                      c || "未配置"
                    )} → <b>${esc((info.configsAfter ?? [])[i] || "未配置")}</b></span></div>`
                  : ""
              )
              .join("")
          : ""
      }
      ${
        info.screenshotBefore !== info.screenshotAfter
          ? `<div class="wb-diff-row"><span class="wb-name">截图</span><span class="wb-val">${esc(
              info.screenshotBefore || "无"
            )} → <b>${esc(info.screenshotAfter || "无")}</b></span></div>`
          : ""
      }
      ${
        info.deathEffectBefore !== info.deathEffectAfter
          ? `<div class="wb-diff-row"><span class="wb-name">死亡特效</span><span class="wb-val">${esc(
              info.deathEffectBefore || "无"
            )} → <b>${esc(info.deathEffectAfter || "无")}</b></span></div>`
          : ""
      }
      ${
        info.minMaxOrdersBefore !== info.minMaxOrdersAfter
          ? `<div class="wb-diff-row"><span class="wb-name">同单订单数</span><span class="wb-val">${esc(
              info.minMaxOrdersBefore
            )} → <b>${esc(info.minMaxOrdersAfter)}</b></span></div>`
          : ""
      }`;

  return `
    <div class="wb-detail-head">
      <b>${esc((d.recordedAt || detail.record).slice(0, 19))}</b>
      ${(m?.endpoints ?? []).map(endpointBadge).join("")}
      <span class="muted">定稿 ${esc((m?.finalizeTime ?? "").slice(0, 19))}</span>
    </div>
    <p class="modal-hint muted">${esc(d.scenePath)}</p>
    ${
      m
        ? `<p class="modal-hint">物品数 ${m.itemsBefore} → ${m.itemsAfter} · 地板数 ${m.floorsBefore} → ${m.floorsAfter} · 参与写回：${(
            m.endpoints ?? []
          ).join(" + ")}</p>`
        : ""
    }
    ${itemSection}
    ${infoSection}
    <h4>文件快照</h4>
    <div class="wb-diff-row"><span class="wb-name">场景 .unity</span><span class="wb-val">${fmtBytes(
      d.sceneBytesBefore
    )} → ${fmtBytes(d.sceneBytesAfter)}</span></div>
    <div class="wb-diff-row"><span class="wb-name">关卡 Info .asset</span><span class="wb-val">${fmtBytes(
      d.infoBytesBefore
    )} → ${fmtBytes(d.infoBytesAfter)}</span></div>`;
}

async function showHistoryDetail(
  wbSet: string,
  levelId: string,
  record: string,
  opts?: ToolsHistoryOptions
): Promise<void> {
  const body = document.getElementById("wb-hist-body");
  if (!body || !record) return;
  body.innerHTML = '<p class="muted">加载记录…</p>';
  let detail: WriteBackHistoryDetail;
  try {
    detail = await api.fetchWriteBackHistoryDetail(wbSet, levelId, record);
  } catch (e) {
    body.innerHTML = `<p class="modal-hint err">${esc((e as Error).message)}</p>`;
    return;
  }
  // 恢复操作条：仅编辑器入口（opts.onRestore）提供；无完整快照的侧位禁用（旧记录/仅信息变更）。
  const restoreBar = opts?.onRestore
    ? `
    <div class="wb-restore-bar">
      <button type="button" class="m-btn small" data-restore="before" ${
        detail.canRestoreBefore
          ? 'title="回到这次操作开始之前的状态（相当于撤销这次修改）"'
          : 'disabled title="这条记录没有这一侧的完整布局（旧版记录或纯信息修改），无法恢复"'
      }>↩️ 撤销这次操作</button>
      <button type="button" class="m-btn small" data-restore="after" ${
        detail.canRestoreAfter
          ? 'title="回到这次操作刚完成时的样子（之后又改过的话，可用来找回当时的状态）"'
          : 'disabled title="这条记录没有这一侧的完整布局（旧版记录或纯信息修改），无法恢复"'
      }>🕘 回到操作完成时</button>
      <span class="muted">恢复 = 把当时的关卡布局放回画布（属于未保存修改，Ctrl+Z 可撤回）；再点「💾 写回 Unity」才会写入场景</span>
    </div>`
    : "";
  const manageHint = !opts?.onRestore
    ? '<p class="modal-hint muted">如需把该快照恢复到画布，请在布局编辑器工具栏的「🧰 工具与历史」中操作。</p>'
    : "";
  body.innerHTML = `
    <button type="button" class="m-btn small" id="wb-back">← 返回列表</button>
    ${restoreBar}
    ${renderHistoryDetail(detail)}
    ${manageHint}`;
  document.getElementById("wb-back")?.addEventListener("click", () => void loadHistoryList(wbSet, levelId, opts));
  body.querySelectorAll<HTMLButtonElement>("[data-restore]").forEach((btn) => {
    if (btn.disabled) return;
    let armed = false;
    btn.addEventListener("click", () => {
      if (!armed) {
        // 两步确认：首点布防防误触，再点执行。
        armed = true;
        btn.classList.add("danger");
        btn.dataset.label = btn.textContent ?? "";
        btn.textContent = "确认覆盖画布？";
        window.setTimeout(() => {
          if (armed && btn.isConnected) {
            armed = false;
            btn.classList.remove("danger");
            btn.textContent = btn.dataset.label ?? btn.textContent ?? "";
          }
        }, 4000);
        return;
      }
      const side = btn.dataset.restore === "before" ? "before" : "after";
      const label = (detail.meta?.beginTime ?? detail.diff?.recordedAt ?? record).slice(0, 19);
      closeModal();
      opts?.onRestore?.(record, side, label);
    });
  });
}

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
const PLAYER_ROW_LABELS = ["1P", "2P", "3P", "4P"];

export async function openConfigTabsModal(detail: LevelDetail, setName: string, onSaved: () => void): Promise<void> {
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

  const rhythmHead = RHYTHM_FIELDS.map(([, label]) => `<th>${label}</th>`).join("");
  const rhythmRows = PLAYER_TABS.map((t, ti) => {
    const cfg = detail.configs[ti] ?? ({ exists: false } as PerPlayerConfig);
    const cells = RHYTHM_FIELDS.map(([key, , step]) => {
      const val = (cfg[key] as number) ?? 0;
      return `<td><input type="number" step="${step}" class="cfg-star-input" id="cfg-${t}-${key}" value="${val}"></td>`;
    }).join("");
    return `<tr><td class="cfg-row-label">${PLAYER_ROW_LABELS[ti]}</td>${cells}</tr>`;
  }).join("");
  const defaultRoundTime = detail.configs[0]?.roundTime || 240;

  openModal(
    `关卡配置 · ${detail.levelName || detail.levelNameZH}`,
    `<div class="cfg-ltabs">
        <button type="button" class="cfg-ltab-btn active" data-ltab="score">📊 分数</button>
        <button type="button" class="cfg-ltab-btn" data-ltab="shot">📷 截图</button>
     </div>
     <div data-lpane="score">
       <div class="cfg-ai-bar">
          <button type="button" class="m-btn primary" id="cfg-ai-fill">✨ 一键定分</button>
          <label class="m-field cfg-round-all">关卡时长(秒)<input type="number" id="cfg-roundTime-all" step="10" min="30" value="${defaultRoundTime}"></label>
          <span class="muted small">修改时长后点击「一键定分」重新修订；定分会同步修正订单超时 / 间隔 / 回盘</span>
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
       <p class="modal-hint">节奏参数（按人数）</p>
       <table class="cfg-matrix">
         <thead><tr><th>人数</th>${rhythmHead}</tr></thead>
         <tbody>${rhythmRows}</tbody>
       </table>
     </div>
     <div data-lpane="shot" style="display:none">
       ${screenshotPaneHtml(detail)}
     </div>`,
    `<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>保存全部</button>`
  );
  document.querySelector(".modal-panel")?.classList.add("wide");

  // 顶层 tab：分数（参与「保存全部」）/ 截图（即时上传，切到截图时隐藏保存按钮）
  document.querySelectorAll<HTMLButtonElement>(".cfg-ltab-btn").forEach((btn) =>
    btn.addEventListener("click", () => {
      document.querySelectorAll(".cfg-ltab-btn").forEach((b) => b.classList.toggle("active", b === btn));
      const isShot = btn.dataset.ltab === "shot";
      document.querySelectorAll<HTMLElement>("[data-lpane]").forEach((p) => {
        p.style.display = p.dataset.lpane === btn.dataset.ltab ? "" : "none";
      });
      document.querySelector<HTMLButtonElement>("[data-ok]")?.style.setProperty("display", isShot ? "none" : "");
    })
  );
  wireScreenshotPane(detail);

  // 顶部统一关卡时长：修改后广播到 1P~4P 四行（行内仍可单独微调）
  document.getElementById("cfg-roundTime-all")?.addEventListener("change", (e) => {
    const v = (e.target as HTMLInputElement).value;
    PLAYER_TABS.forEach((t) => {
      (document.getElementById(`cfg-${t}-roundTime`) as HTMLInputElement).value = v;
    });
  });

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
      const [catalog, level, layoutDoc] = await Promise.all([
        api.fetchRecipeCatalog(setName),
        api.fetchLevelRecipes(detail.sceneAssetPath),
        api.fetchLayout(detail.sceneAssetPath).catch(() => null),
      ]);
      const byGuid = new Map(catalog.map((r) => [r.guid, r]));
      const selected = (level.recipeGuids ?? [])
        .map((g) => byGuid.get(g))
        .filter((r): r is RecipeEntry => !!r);
      if (!selected.length) {
        detailEl.innerHTML = `<p class="modal-hint err">该关卡尚未配置菜谱，请先在详情页点击「菜谱…」选择菜谱。</p>`;
        return;
      }
      const topRoundTime =
        parseInt((document.getElementById("cfg-roundTime-all") as HTMLInputElement).value || "240", 10) || 240;
      const kitchen = analyzeKitchen(layoutDoc);
      const result = computeAutoScores(selected, PLAYER_TABS.map(() => topRoundTime), kitchen);
      if (!result) {
        detailEl.innerHTML = `<p class="modal-hint err">所选菜谱缺少价格信息，无法推算。</p>`;
        return;
      }
      const lifeTimes = computeOrderLifeTimes(result.maxTimeSec);
      PLAYER_TABS.forEach((t, ti) => {
        baseStars[ti] = result.stars[ti].slice();
        result.stars[ti].forEach((v, j) => {
          starInput(t, j).value = String(v);
        });
        resetRatio(t);
        const setVal = (key: string, v: number) => {
          (document.getElementById(`cfg-${t}-${key}`) as HTMLInputElement).value = String(v);
        };
        setVal("roundTime", topRoundTime);
        setVal("orderLifeTime", lifeTimes[ti]);
        setVal("timeBetweenOrders", ORDER_INTERVAL_SEC[ti]);
        setVal("plateReturnTime", PLATE_RETURN_SEC);
      });
      const rows = result.details
        .map(
          (d) =>
            `<tr><td>${esc(d.name)}</td><td>${esc(d.groupLabel)}</td><td>${d.ingredientCount}</td><td>${
              d.cookingStepCount > 0 ? "✓" : "—"
            }</td><td>${d.fetchSec > 0 ? d.fetchSec.toFixed(1) : "—"}</td><td>${
              d.prepSec > 0 ? d.prepSec.toFixed(1) : "—"
            }</td><td>${d.cookSec > 0 ? d.cookSec.toFixed(1) : "—"}</td><td>${
              d.serveSec > 0 ? d.serveSec.toFixed(1) : "—"
            }</td><td>${d.score}</td><td>${d.timeSec.toFixed(0)}s</td></tr>`
        )
        .join("");
      const modelTag =
        result.model === "kitchen"
          ? `按图定分（设备感知 · 风险×${result.hazardMult.toFixed(2)}）`
          : "通用线性模型（未读取到场景布局）";
      const chips = kitchenChips(kitchen);
      const chipsHtml = chips.length
        ? `<div class="cfg-kitchen-chips">${chips.map((c) => `<span class="cfg-chip">${esc(c)}</span>`).join("")}</div>`
        : "";
      const warns = kitchenWarnings(kitchen, selected);
      const warnsHtml = warns.length
        ? `<div class="cfg-kitchen-warns">${warns.map((w) => `<p class="modal-hint err">⚠ ${esc(w)}</p>`).join("")}</div>`
        : "";
      const paramsHtml = `
        <details class="cfg-params">
          <summary>模型参数（只读）</summary>
          <table class="cfg-ai-table">
            <tbody>${modelParamsSummary()
              .map((p) => `<tr><td>${esc(p.key)}</td><td>${esc(p.value)}</td></tr>`)
              .join("")}</tbody>
          </table>
        </details>`;
      detailEl.innerHTML = `
        <p class="modal-hint ok">已按 ${result.details.length} 道菜谱推算（${esc(modelTag)}）：平均单菜约 ${result.avgTimeSec.toFixed(
        0
      )} 秒 · 平均菜价 ${result.avgPrice.toFixed(0)} 分 · 已同步修正节奏（订单超时 1P~4P：${lifeTimes.join(
        " / "
      )} 秒，关卡时长 ${topRoundTime} 秒）</p>
        ${chipsHtml}
        ${warnsHtml}
        <table class="cfg-ai-table">
          <thead><tr><th>菜谱</th><th>来源</th><th>食材数</th><th>需烹饪</th><th>取材s</th><th>切配s</th><th>烹饪s</th><th>装上s</th><th>菜价</th><th>估时</th></tr></thead>
          <tbody>${rows}</tbody>
        </table>
        ${paramsHtml}`;
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
      showBusy("保存关卡配置…");
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
      setStatus("关卡配置已保存（已 reload）");
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
  let bundleGraph: Map<string, string[]>;
  let exports: AudioExportManifest | null;
  try {
    [music, dirs, ambiances, deaths, knowledge, bundleGraph, exports] = await Promise.all([
      api.fetchMusicCatalog(),
      api.fetchAudioDirectoryCatalog(),
      api.fetchAmbiences(),
      api.fetchDeathEffects(),
      api.fetchAudioKnowledge(),
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
    <p class="modal-hint">写入场景的 <code>PseudoPrefabManagerStub</code>。保存时会自动打开/保存场景、Reload，并<b>把所选 BGM / 音效集所需 bundle 并入 <code>LevelInfoSO.dependencies</code></b>。Bundle 分析与清理请使用顶栏 <b>📦 依赖管理</b>。</p>
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
      const ambiences = [...state.ambiences];
      const audioDirectoryGuids = effectiveDirGuids();

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
