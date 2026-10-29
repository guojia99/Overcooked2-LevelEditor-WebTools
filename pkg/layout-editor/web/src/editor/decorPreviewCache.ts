/**
 * 装饰缩略图缓存中枢。
 *
 * - manifest：guid → { front, back? }（Unity 菜单导出到 exports/decor），带版本号防旧缓存。
 * - 图片经后端 /api/web-assets/decor/ 读取，不再走 layout-editor/web/public 静态资源。
 * - 预热：开启「装饰预览」后后台并发请求全部装饰图，写进浏览器 HTTP 缓存；
 *   不长期持有解码后的 HTMLImageElement（3000+ 张会吃光内存），只记录成功/失败状态。
 * - 卡片 <img> 仍用 loading="lazy"，滚动到视口时直接从 HTTP 缓存命中。
 */

export type DecorView = "front" | "back";

export interface DecorPreviewDetail {
  front?: string;
  back?: string;
}

export interface DecorPreviewModel {
  obj: string;
  mtl?: string;
}

export interface DecorPreviewEntry {
  front: string;
  back?: string;
  /** 高清详情图（1024，个别大件 1536/2048）；仅在打开大图查看器时加载。 */
  detail?: DecorPreviewDetail;
  /** 3D 预览模型（OBJ/MTL + 共享贴图）；仅在打开 3D 查看器时加载。 */
  model?: DecorPreviewModel;
}

export interface DecorModelInfo {
  resourceBase: string;
  modelFileName: string;
  mtlUrl: string;
}

export interface DecorPreviewManifest {
  version: string;
  generatedAt?: string;
  thumbSize?: number;
  views?: DecorView[];
  files: Record<string, DecorPreviewEntry>;
  missing?: Array<{ id: string; guid: string; nameZh?: string }>;
}

const PREVIEW_BASE = "/api/web-assets/decor";
const PRELOAD_CONCURRENCY = 12;

let manifest: DecorPreviewManifest | null = null;
let manifestPromise: Promise<DecorPreviewManifest | null> | null = null;
const loaded = new Set<string>();
const failed = new Set<string>();
const queued = new Set<string>();
const queue: string[] = [];
let preloadRunning = false;
let progressCb: ((done: number, total: number) => void) | null = null;

function validGuid(guid: string): boolean {
  return /^[a-f0-9]{32}$/i.test(guid);
}

export async function loadDecorPreviewManifest(): Promise<DecorPreviewManifest | null> {
  if (manifestPromise) return manifestPromise;
  manifestPromise = fetch(`${PREVIEW_BASE}/manifest.json`, { cache: "no-cache" })
    .then((r) => {
      if (!r.ok) throw new Error(`manifest HTTP ${r.status}`);
      return r.json() as Promise<DecorPreviewManifest>;
    })
    .then((data) => {
      if (!data || typeof data !== "object" || !data.files) throw new Error("manifest 格式不正确");
      manifest = data;
      for (const m of data.missing ?? []) {
        if (m && m.guid) failed.add(failKey(m.guid, "front"));
      }
      return manifest;
    })
    .catch(() => {
      manifest = null;
      return null;
    });
  return manifestPromise;
}

export function decorPreviewVersion(): string {
  return manifest?.version ?? "";
}

function failKey(guid: string, view: DecorView): string {
  return `${guid}:${view}`;
}

export function isDecorPreviewMissing(guid: string, view: DecorView = "front"): boolean {
  return !!guid && failed.has(failKey(guid, view));
}

/** 卡片图片 URL；manifest 明确缺失 / 已知加载失败时返回 null（显示占位）。 */
export function decorPreviewUrl(guid: string, view: DecorView = "front"): string | null {
  if (!guid || !validGuid(guid) || failed.has(failKey(guid, view))) return null;
  const entry = manifest?.files?.[guid];
  if (manifest) {
    if (!entry || !entry.front) return null;
    if (view === "back" && !entry.back) return null;
  }
  const rel = (view === "back" ? entry?.back : entry?.front) || `${view}/${guid}.png`;
  const safeRel = rel
    .split("/")
    .map((part) => encodeURIComponent(part))
    .join("/");
  const base = `${PREVIEW_BASE}/${safeRel}`;
  const version = manifest?.version;
  return version ? `${base}?v=${encodeURIComponent(version)}` : base;
}

/** 3D 模型资源信息；manifest 未提供 model 时返回 null。 */
export function decorModelInfo(guid: string): DecorModelInfo | null {
  if (!guid || !validGuid(guid)) return null;
  const model = manifest?.files?.[guid]?.model;
  if (!model || !model.obj) return null;
  const slash = model.obj.lastIndexOf("/");
  const dir = slash >= 0 ? model.obj.slice(0, slash + 1) : "";
  const fileName = slash >= 0 ? model.obj.slice(slash + 1) : model.obj;
  const mtlRel = model.mtl || dir + "model.mtl";
  const mtlUrl = mtlRel.indexOf(dir) === 0 ? mtlRel.slice(dir.length) : mtlRel;
  return {
    resourceBase: `${PREVIEW_BASE}/${dir}`,
    modelFileName: fileName,
    mtlUrl,
  };
}

/** 高清详情图 URL；manifest 未提供 detail 时返回 null（查看器回退 192 缩略图）。 */
export function decorDetailUrl(guid: string, view: DecorView = "front"): string | null {
  if (!guid || !validGuid(guid)) return null;
  const entry = manifest?.files?.[guid];
  const rel = view === "back" ? entry?.detail?.back : entry?.detail?.front;
  if (!rel) return null;
  const safeRel = rel
    .split("/")
    .map((part) => encodeURIComponent(part))
    .join("/");
  const base = `${PREVIEW_BASE}/${safeRel}`;
  const version = manifest?.version;
  return version ? `${base}?v=${encodeURIComponent(version)}` : base;
}

export function markDecorPreviewLoaded(guid: string, view: DecorView = "front"): void {
  if (!guid) return;
  loaded.add(failKey(guid, view));
  failed.delete(failKey(guid, view));
}

export function markDecorPreviewFailed(guid: string, view: DecorView = "front"): void {
  if (!guid) return;
  failed.add(failKey(guid, view));
  loaded.delete(failKey(guid, view));
}

function pump(): void {
  if (preloadRunning) return;
  preloadRunning = true;
  const run = () => {
    if (queue.length === 0) {
      preloadRunning = false;
      return;
    }
    const batch = queue.splice(0, PRELOAD_CONCURRENCY);
    let remaining = batch.length;
    const doneOne = () => {
      remaining--;
      if (remaining <= 0) {
        if (progressCb) progressCb(loaded.size + failed.size, loaded.size + failed.size + queue.length);
        setTimeout(run, 0);
      }
    };
    for (const guid of batch) {
      queued.delete(guid);
      if (loaded.has(failKey(guid, "front")) || failed.has(failKey(guid, "front"))) {
        doneOne();
        continue;
      }
      const url = decorPreviewUrl(guid);
      if (!url) {
        doneOne();
        continue;
      }
      // 用 fetch 预热浏览器 HTTP 缓存（不保留解码位图，3000+ 张也不会吃光内存）；
      // 卡片 <img> 滚动到视口时从缓存直接取。
      fetch(url, { cache: "force-cache" })
        .then((r) => {
          if (!r.ok) throw new Error(`HTTP ${r.status}`);
          return r.arrayBuffer();
        })
        .then(() => {
          markDecorPreviewLoaded(guid, "front");
          doneOne();
        })
        .catch(() => {
          markDecorPreviewFailed(guid, "front");
          doneOne();
        });
    }
  };
  run();
}

/** 后台预热全部装饰缩略图（可重复调用；新增 guid 会继续排队）。 */
export function preloadDecorPreviews(
  guids: string[],
  onProgress?: (done: number, total: number) => void
): void {
  if (onProgress) progressCb = onProgress;
  for (const guid of guids) {
    if (!guid || !validGuid(guid) || loaded.has(failKey(guid, "front")) || failed.has(failKey(guid, "front")) || queued.has(guid)) continue;
    queued.add(guid);
    queue.push(guid);
  }
  if (queue.length > 0) pump();
}

/** 当前缓存状态（排障/状态栏可用）。 */
export function decorPreviewCacheStats(): { loaded: number; failed: number; pending: number } {
  return { loaded: loaded.size, failed: failed.size, pending: queue.length };
}
