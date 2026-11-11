/**
 * 🎮 Unity 实时画面（画布右下角悬浮的游戏相机预览）。
 *
 * 数据来源：复用 /api/level/screenshot-capture（Unity 游戏相机离屏渲染 → JPEG base64），
 * 每次统一抓 1920×1080 —— 右下角缩略与点击放大共用同一张，放大不重抓。
 *
 * 触发点（事件驱动，无轮询）：
 * - loadScene 成功后延迟一拍自动抓（sceneIO 调 scheduleUnityPreviewAutoRefresh）——
 *   覆盖首次进关、切场景、🔄 重新加载、写回后（写回成功本身就会 loadScene 重载场景）；
 * - 面板 🔄 按钮随时手动重抓。
 *
 * 超时与重试（关键）：后端请求在 Unity 主线程队列串行执行，场景刚打开 /
 *  导入编译 / 域重载期间队列可能长时间不泵甚至丢请求（响应永不写出，浏览器会
 *  一直等）。因此每个请求带超时中止，首败自动重试一次，仍失败才显示错误——
 *  绝不让右下角永远转圈；刷新期间保留上一张画面（loading 以角标覆盖）。
 *
 * 开关与收起：「显示配置 → 🎮 Unity 实时画面」（默认开，localStorage unityPreview）；
 * 收起只剩小药丸（localStorage unityPreviewCollapsed）。均见 state.ts。
 */

import { S } from "../state";
import { escHtml } from "../coords";
import type { LevelRecipes } from "../../types";

/** 抓取分辨率（16:9 游戏相机；缩略/放大共用）。 */
const CAP_WIDTH = 1920;
const CAP_HEIGHT = 1080;
const CAP_QUALITY = 85;
/** 单步请求超时（毫秒）：到点中止 → 重试/报错，避免永远转圈。 */
const FETCH_TIMEOUT_MS = 15000;
/** 首次失败后的自动重试间隔（Unity 主线程通常很快空出来）。 */
const RETRY_DELAY_MS = 2000;
/** loadScene 完成后自动抓取的延后（毫秒）：避开场景刚打开时 OnSceneOpened /
 *  伪资产刷新占用主线程的高压窗口。 */
const AUTO_DELAY_MS = 900;

/** levelInfoAssetPath 会话级缓存：同一场景反复刷新不重复请求。 */
const levelInfoCache = new Map<string, string>();

let inFlight = false;
let queued = false;
let autoTimer = 0;

const sleep = (ms: number): Promise<void> => new Promise((r) => window.setTimeout(r, ms));

function panelEl(): HTMLElement | null {
  return document.getElementById("unity-preview");
}

function restoreEl(): HTMLElement | null {
  return document.getElementById("unity-preview-restore");
}

function imgEl(): HTMLImageElement | null {
  return document.getElementById("unity-preview-img") as HTMLImageElement | null;
}

function emptyEl(): HTMLElement | null {
  return document.getElementById("unity-preview-empty");
}

/** 按 S.showUnityPreview / S.unityPreviewCollapsed 同步面板与药丸的显隐。 */
export function applyUnityPreviewVisibility(): void {
  const panel = panelEl();
  const restore = restoreEl();
  if (!panel || !restore) return;
  const show = S.showUnityPreview;
  panel.classList.toggle("hidden", !show || S.unityPreviewCollapsed);
  restore.classList.toggle("hidden", !show || !S.unityPreviewCollapsed);
}

type PanelMode = "loading" | "idle" | "error" | "image";

function setPanelState(mode: PanelMode, message = ""): void {
  const img = imgEl();
  const empty = emptyEl();
  if (!img || !empty) return;
  empty.classList.remove("unity-preview-error", "unity-preview-overlay-mode");
  empty.title = "";
  if (mode === "image") {
    img.classList.remove("hidden");
    empty.classList.add("hidden");
    return;
  }
  img.classList.add("hidden");
  empty.classList.remove("hidden");
  if (mode === "loading") {
    if (img.src) {
      // 已有画面：保留上一张，loading 以左下角小条覆盖（刷新/重试期间不失画面）
      img.classList.remove("hidden");
      empty.classList.add("unity-preview-overlay-mode");
      empty.innerHTML =
        '<span class="unity-preview-err-chip unity-preview-loading-chip"><span class="unity-preview-spin"></span>正在获取…</span>';
    } else {
      empty.innerHTML = '<span class="unity-preview-spin"></span>正在获取 Unity 游戏相机画面…';
    }
  } else if (mode === "idle") {
    empty.textContent = "尚未获取画面，点 🔄 抓取";
  } else if (mode === "error") {
    if (img.src) {
      // 已有画面时保留上一张，错误以左下角小条覆盖（不整面板顶掉）
      img.classList.remove("hidden");
      empty.classList.add("unity-preview-overlay-mode");
      empty.innerHTML = `<span class="unity-preview-err-chip" title="${escHtml(message)}">⚠ ${escHtml(message)}</span>`;
    } else {
      empty.classList.add("unity-preview-error");
      empty.title = message;
      empty.textContent = `⚠ ${message}`;
    }
  }
}

/** 带超时的 JSON 请求（readApiJson 同款错误语义 + AbortController 兜底）。 */
async function fetchJson<T>(url: string, init: RequestInit): Promise<T> {
  const ctl = new AbortController();
  const timer = window.setTimeout(() => ctl.abort(), FETCH_TIMEOUT_MS);
  let r: Response;
  try {
    r = await fetch(url, { ...init, signal: ctl.signal });
  } catch (e) {
    if ((e as Error).name === "AbortError")
      throw new Error("获取超时（Unity 主线程忙或未响应）");
    throw e;
  } finally {
    window.clearTimeout(timer);
  }
  const text = await r.text();
  if (text.trimStart().startsWith("<"))
    throw new Error("Bridge 返回异常页面，请重启 Unity 端服务");
  let data: unknown = {};
  try {
    data = text ? JSON.parse(text) : {};
  } catch {
    throw new Error(`API 响应不是合法 JSON：${text.slice(0, 120)}`);
  }
  if (!r.ok) {
    const err = data as { error?: string };
    throw new Error(err.error ?? `请求失败 (${r.status})`);
  }
  return data as T;
}

/** 单次完整抓取：解析 LevelInfoSO 路径 → 截图 → 换图。 */
async function captureOnce(): Promise<void> {
  const scenePath = S.scenePath;
  let levelInfoPath = levelInfoCache.get(scenePath) ?? "";
  if (!levelInfoPath) {
    const q = new URLSearchParams({ assetPath: scenePath });
    const level = await fetchJson<LevelRecipes>(`/api/level-recipes?${q}`, { method: "GET" });
    levelInfoPath = level.levelInfoAssetPath ?? "";
    if (levelInfoPath) levelInfoCache.set(scenePath, levelInfoPath);
  }
  if (!levelInfoPath) throw new Error("未找到该场景对应的 LevelInfoSO");

  const shot = await fetchJson<{ base64?: string }>("/api/level/screenshot-capture", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ assetPath: levelInfoPath, width: CAP_WIDTH, height: CAP_HEIGHT, quality: CAP_QUALITY }),
  });
  if (!shot.base64) throw new Error("Unity 未返回画面");
  const img = imgEl();
  if (!img) return;
  img.src = `data:image/jpeg;base64,${shot.base64}`;
  setPanelState("image");
}

/**
 * 抓取一帧 Unity 游戏相机画面并更新面板（失败自动重试一次再报错）。
 * 事件驱动入口与 🔄 手动刷新共用；进行中重复调用会合并（结束后补抓一次最新请求）。
 */
export async function refreshUnityPreview(): Promise<void> {
  if (!S.showUnityPreview || !S.scenePath) return;
  if (inFlight) {
    queued = true;
    return;
  }
  inFlight = true;
  setPanelState("loading");
  let lastErr: Error | null = null;
  try {
    for (let attempt = 0; attempt < 2; attempt++) {
      try {
        await captureOnce();
        return;
      } catch (e) {
        lastErr = e as Error;
        console.warn(`[unity-preview] 第 ${attempt + 1} 次抓取失败：${lastErr.message}`);
        if (attempt === 0) await sleep(RETRY_DELAY_MS);
      }
    }
    setPanelState("error", lastErr?.message || "获取失败");
  } finally {
    inFlight = false;
    if (queued) {
      queued = false;
      void refreshUnityPreview();
    }
  }
}

/** loadScene 完成后的自动抓取入口：延后一拍再抓（见文件头「超时与重试」说明）。 */
export function scheduleUnityPreviewAutoRefresh(delayMs = AUTO_DELAY_MS): void {
  if (!S.showUnityPreview) return;
  if (autoTimer) window.clearTimeout(autoTimer);
  autoTimer = window.setTimeout(() => {
    autoTimer = 0;
    void refreshUnityPreview();
  }, delayMs);
}

/** 点击缩略图 → 全屏放大（复用已抓的同一张图，不重抓）。 */
function openZoom(): void {
  const img = imgEl();
  if (!img || !img.src || img.classList.contains("hidden")) return;
  if (document.getElementById("unity-preview-zoom")) return;
  const zoom = document.createElement("div");
  zoom.id = "unity-preview-zoom";
  zoom.className = "unity-preview-zoom";
  zoom.title = "点击任意处关闭（Esc 同样可关闭）";
  const big = document.createElement("img");
  big.src = img.src;
  big.alt = "Unity 游戏相机画面（放大）";
  zoom.appendChild(big);
  const hint = document.createElement("div");
  hint.className = "unity-preview-zoom-hint";
  hint.textContent = "🎮 Unity 实时画面 · 点击关闭";
  zoom.appendChild(hint);
  zoom.addEventListener("click", () => closeZoom());
  document.body.appendChild(zoom);
}

function closeZoom(): void {
  document.getElementById("unity-preview-zoom")?.remove();
}

/** 一次性挂接面板全部交互（init 在 buildLayoutDom 之后调用）。 */
export function initUnityPreview(): void {
  applyUnityPreviewVisibility();
  // 初始态：无图且不在加载中 → idle 占位（首次抓取由 loadScene 钩子触发）
  const img = imgEl();
  if (img && !img.src) setPanelState("idle");

  document.getElementById("unity-preview-refresh")?.addEventListener("click", () => {
    void refreshUnityPreview();
  });
  document.getElementById("unity-preview-collapse")?.addEventListener("click", () => {
    S.unityPreviewCollapsed = true;
    localStorage.setItem("unityPreviewCollapsed", "1");
    applyUnityPreviewVisibility();
  });
  restoreEl()?.addEventListener("click", () => {
    S.unityPreviewCollapsed = false;
    localStorage.setItem("unityPreviewCollapsed", "0");
    applyUnityPreviewVisibility();
  });
  imgEl()?.addEventListener("click", openZoom);
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape") closeZoom();
  });
}
