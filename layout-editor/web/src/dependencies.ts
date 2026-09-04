import * as api from "./api";
import type { AudioKnowledge, BundleAnalysis, LevelDetail, LevelSetInfo, LevelSummary } from "./types";
import { showBusy, hideBusy } from "./busy";
import { navHtml, wireNav } from "./nav";
import { goLayout, goManage } from "./levels";

const DEPS_TARGET_KEY = "depsTargetLevel";

export function goDependencies(setName?: string, levelInfoAssetPath?: string): void {
  if (setName && levelInfoAssetPath) {
    sessionStorage.setItem(DEPS_TARGET_KEY, JSON.stringify({ setName, assetPath: levelInfoAssetPath }));
  } else {
    sessionStorage.removeItem(DEPS_TARGET_KEY);
  }
  location.hash = "#/dependencies";
  location.reload();
}

function consumeDepsTarget(): { setName: string; assetPath: string } | null {
  const raw = sessionStorage.getItem(DEPS_TARGET_KEY);
  if (!raw) return null;
  sessionStorage.removeItem(DEPS_TARGET_KEY);
  try {
    const v = JSON.parse(raw) as { setName?: string; assetPath?: string };
    if (v.setName && v.assetPath) return { setName: v.setName, assetPath: v.assetPath };
  } catch {
    /* ignore */
  }
  return null;
}

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function setStatus(msg: string, ok = true): void {
  const el = document.getElementById("dep-status");
  if (!el) return;
  el.textContent = msg;
  el.classList.toggle("err", !ok);
  el.classList.toggle("ok", ok && msg.length > 0);
}

function setBusy(msg: string): void {
  const el = document.getElementById("dep-content");
  if (el) el.innerHTML = `<p class="muted">${esc(msg)}</p>`;
  setStatus(msg);
}

function showError(e: unknown): void {
  const msg = e instanceof Error ? e.message : String(e);
  setStatus(msg, false);
  const el = document.getElementById("dep-content");
  if (el) el.innerHTML = `<div class="m-block"><h3>出错</h3><p>${esc(msg)}</p></div>`;
}

function wireDepsNav(): void {
  wireNav((target) => {
    if (target === "layout") {
      location.hash = "#/layout";
      location.reload();
    } else if (target === "manage") goManage();
    else if (target === "custom-recipes") {
      location.hash = "#/custom-recipes";
      location.reload();
    } else if (target === "recipes") {
      location.href = "/recipes";
    } else if (target === "guide") {
      location.hash = "#/guide";
      location.reload();
    } else if (target === "changelog") {
      location.hash = "#/changelog";
      location.reload();
    } else if (target === "dependencies") {
      location.hash = "#/dependencies";
      location.reload();
    }
  });
}

function shell(app: HTMLElement, title: string, backLabel?: string, onBack?: () => void): HTMLElement {
  document.body.classList.add("manage-bg");
  app.innerHTML = `
    ${navHtml("dependencies")}
    <div class="manage-bar">
      ${backLabel ? `<button class="m-btn" id="dep-back">← ${esc(backLabel)}</button>` : ""}
      <h1 class="m-title">${esc(title)}</h1>
      <span class="status" id="dep-status"></span>
      <span style="flex:1"></span>
      <button class="m-btn" id="dep-reload" title="触发 Unity Reload Pseudo Assets">↻ Reload</button>
    </div>
    <div class="manage-content" id="dep-content"></div>
  `;
  wireDepsNav();
  document.getElementById("dep-back")?.addEventListener("click", () => onBack?.());
  document.getElementById("dep-reload")?.addEventListener("click", async () => {
    try {
      await api.reloadPseudo();
      setStatus("已触发 Unity Reload");
    } catch (e) {
      setStatus((e as Error).message, false);
    }
  });
  return document.getElementById("dep-content")!;
}

function renderAnalysisBox(analysis: BundleAnalysis, alwaysLoaded: Set<string>): string {
  const miss = analysis.missing.filter((b) => !alwaysLoaded.has(b));
  const extras = analysis.extras;
  const missHtml = miss.length
    ? `<div class="dep-warn dep-miss">🔴 缺失（场景/菜谱引用但 dependencies 未覆盖）：<b>${miss.map(esc).join(", ")}</b></div>`
    : `<div class="dep-ok">依赖资源包完整（相对当前关卡引用）。</div>`;
  const extrasHtml = extras.length
    ? `<div class="dep-warn dep-extra">🟡 检测到未使用 bundle：${extras.map(esc).join(", ")} <label class="modal-check inline"><input type="checkbox" id="dep-clean"> 保存时清理未用 bundle</label></div>`
    : "";
  const cur = analysis.current.length ? analysis.current.map(esc).join(", ") : "<i class='muted'>（无）</i>";
  return `
    <div class="dep-box">
      <div class="muted">分析结果（当前 dependencies）：${cur}</div>
      ${missHtml}
      ${extrasHtml}
    </div>`;
}

export async function renderDependenciesView(app: HTMLElement): Promise<void> {
  const target = consumeDepsTarget();
  if (target) {
    await renderDepsDetail(app, target.setName, target.assetPath);
    return;
  }
  await renderSetList(app);
}

async function renderSetList(app: HTMLElement): Promise<void> {
  const content = shell(app, "依赖管理 · 选择关卡集");
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
        <h3>${esc(s.levelSetNameZH || s.setName)}</h3>
        <div class="m-meta muted">${esc(s.levelSetName || "")} · ${esc(s.setName)}</div>
        <div class="m-actions">
          <button class="m-btn primary" data-set="${esc(s.setName)}">选择关卡</button>
        </div>
      </div>`
    )
    .join("");

  content.innerHTML = `
    <p class="modal-hint">管理 <code>LevelInfoSO.dependencies</code>：查看 bundle 分析、手动编辑依赖列表。场景写回与菜谱保存后会自动重建 dependencies。</p>
    <div class="m-grid">${cards || '<p class="muted">暂无关卡集</p>'}</div>
  `;

  content.querySelectorAll<HTMLButtonElement>("[data-set]").forEach((b) =>
    b.addEventListener("click", () => void renderLevelList(app, b.dataset.set!))
  );
}

async function renderLevelList(app: HTMLElement, setName: string): Promise<void> {
  const content = shell(app, `依赖管理 · ${setName}`, "返回关卡集", () => void renderSetList(app));
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
      const title = lv.levelNameZH || lv.levelName || id;
      return `
      <div class="m-card">
        <h3>${esc(title)}</h3>
        <div class="m-meta muted">${esc(lv.sceneName)} · ${esc(id)}</div>
        <div class="m-actions">
          <button class="m-btn primary" data-deps="${esc(lv.assetPath)}">管理依赖</button>
          ${lv.sceneAssetPath ? `<button class="m-btn" data-layout="${esc(lv.sceneAssetPath)}">打开布局</button>` : ""}
        </div>
      </div>`;
    })
    .join("");

  content.innerHTML = `<div class="m-grid">${cards || '<p class="muted">暂无关卡</p>'}</div>`;

  content.querySelectorAll<HTMLButtonElement>("[data-deps]").forEach((b) =>
    b.addEventListener("click", () => void renderDepsDetail(app, setName, b.dataset.deps!))
  );
  content.querySelectorAll<HTMLButtonElement>("[data-layout]").forEach((b) =>
    b.addEventListener("click", () => goLayout(b.dataset.layout!))
  );
}

async function renderDepsDetail(app: HTMLElement, setName: string, assetPath: string): Promise<void> {
  const content = shell(
    app,
    `依赖管理 · ${setName}`,
    "返回关卡列表",
    () => void renderLevelList(app, setName)
  );
  setBusy("加载关卡依赖…");

  let detail: LevelDetail;
  let analysis: BundleAnalysis | null = null;
  let knowledge: AudioKnowledge;
  try {
    [detail, analysis, knowledge] = await Promise.all([
      api.fetchLevelDetail(assetPath),
      api.fetchBundleAnalysis(assetPath).catch(() => null),
      api.fetchAudioKnowledge(),
    ]);
  } catch (e) {
    showError(e);
    return;
  }
  if (!detail) {
    content.innerHTML = '<p class="muted">未找到该关卡。</p>';
    return;
  }

  const alwaysLoaded = new Set(knowledge.alwaysLoadedBundles);
  const depsText = (detail.dependencies || []).join("\n");
  const title = detail.levelNameZH || detail.levelName || detail.sceneName;

  setStatus(`已加载：${title}`);

  content.innerHTML = `
    <div class="m-actions-row">
      <button class="m-btn" id="dep-refresh">↻ 刷新分析</button>
      <button class="m-btn" id="dep-open-layout" ${detail.sceneAssetPath ? "" : "disabled"}>打开关卡编辑器</button>
    </div>
    <div class="m-block">
      <h3>Bundle 分析 · ${esc(title)}</h3>
      <p class="modal-hint">对比关卡实际引用与 <code>LevelInfoSO.dependencies</code>。场景写回后会<b>覆盖重建</b> dependencies，再合并音频所需 bundle。</p>
      <div id="dep-analysis">${analysis ? renderAnalysisBox(analysis, alwaysLoaded) : '<p class="muted">分析不可用（请确认 Unity Bridge 已连接）。</p>'}</div>
    </div>
    <div class="m-block">
      <h3>依赖 bundles 列表</h3>
      <label class="m-field">每行一个 bundle 名（如 bundle47 / bundle11 / myset/custom_recipes）
        <textarea id="dep-textarea" rows="12">${esc(depsText)}</textarea>
      </label>
      <div class="m-actions-row">
        <button class="m-btn primary" id="dep-save">保存依赖</button>
      </div>
    </div>
  `;

  document.getElementById("dep-open-layout")?.addEventListener("click", () => {
    if (detail.sceneAssetPath) goLayout(detail.sceneAssetPath);
  });

  document.getElementById("dep-refresh")?.addEventListener("click", async () => {
    try {
      showBusy("刷新分析…");
      analysis = await api.fetchBundleAnalysis(assetPath);
      const box = document.getElementById("dep-analysis");
      if (box) {
        box.innerHTML = analysis
          ? renderAnalysisBox(analysis, alwaysLoaded)
          : '<p class="muted">分析不可用。</p>';
      }
      setStatus("分析已刷新");
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });

  document.getElementById("dep-save")?.addEventListener("click", async () => {
    try {
      let deps = (document.getElementById("dep-textarea") as HTMLTextAreaElement).value
        .split(/\r?\n/)
        .map((s) => s.trim())
        .filter(Boolean);

      const clean =
        (document.getElementById("dep-clean") as HTMLInputElement | null)?.checked ?? false;
      if (clean && analysis && analysis.extras.length) {
        const kept = new Set(deps);
        analysis.extras.forEach((b) => kept.delete(b));
        deps = [...kept];
        (document.getElementById("dep-textarea") as HTMLTextAreaElement).value = deps.join("\n");
      }

      showBusy("保存依赖…");
      await api.updateLevelInfo({
        assetPath,
        levelName: detail.levelName,
        levelNameZH: detail.levelNameZH,
        sceneName: detail.sceneName,
        debugRecipeCount: detail.debugRecipeCount,
        disableDynamicParenting: detail.disableDynamicParenting,
        minOrderCount: detail.minOrderCount,
        maxOrderCount: detail.maxOrderCount,
        dependencies: deps,
      });
      setStatus("依赖已保存（已 reload）");
      await renderDepsDetail(app, setName, assetPath);
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });
}
