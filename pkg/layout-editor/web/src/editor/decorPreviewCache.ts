/**
 * 装饰缩略图缓存中枢。
 *
 * - manifest：guid → png 文件（Unity 菜单导出，Node 脚本生成），带版本号防旧缓存。
 * - 预热：开启「装饰预览」后后台并发请求全部装饰图，写进浏览器 HTTP 缓存；
 *   不长期持有解码后的 HTMLImageElement（3000+ 张会吃光内存），只记录成功/失败状态。
 * - 卡片 <img> 仍用 loading="lazy"，滚动到视口时直接从 HTTP 缓存命中。
 */

export interface DecorPreviewManifest {
  version: string;
  generatedAt?: string;
  thumbSize?: number;
  files: Record<string, string>;
  missing?: Array<{ id: string; guid: string; nameZh?: string }>;
}

const PREVIEW_DIR = "/icons/decor-preview";
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
  manifestPromise = fetch(`${PREVIEW_DIR}/manifest.json`, { cache: "no-cache" })
    .then((r) => {
      if (!r.ok) throw new Error(`manifest HTTP ${r.status}`);
      return r.json() as Promise<DecorPreviewManifest>;
    })
    .then((data) => {
      if (!data || typeof data !== "object" || !data.files) throw new Error("manifest 格式不正确");
      manifest = data;
      for (const m of data.missing ?? []) {
        if (m && m.guid) failed.add(m.guid);
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

export function isDecorPreviewMissing(guid: string): boolean {
  return !!guid && failed.has(guid);
}

/** 卡片图片 URL；manifest 明确缺失 / 已知加载失败时返回 null（显示占位）。 */
export function decorPreviewUrl(guid: string): string | null {
  if (!guid || !validGuid(guid) || failed.has(guid)) return null;
  const file = manifest?.files?.[guid];
  if (manifest && !file) return null;
  const name = file || `${guid}.png`;
  const base = `${PREVIEW_DIR}/${encodeURIComponent(name)}`;
  const version = manifest?.version;
  return version ? `${base}?v=${encodeURIComponent(version)}` : base;
}

export function markDecorPreviewLoaded(guid: string): void {
  if (!guid) return;
  loaded.add(guid);
  failed.delete(guid);
}

export function markDecorPreviewFailed(guid: string): void {
  if (!guid) return;
  failed.add(guid);
  loaded.delete(guid);
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
      if (loaded.has(guid) || failed.has(guid)) {
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
          markDecorPreviewLoaded(guid);
          doneOne();
        })
        .catch(() => {
          markDecorPreviewFailed(guid);
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
    if (!guid || !validGuid(guid) || loaded.has(guid) || failed.has(guid) || queued.has(guid)) continue;
    queued.add(guid);
    queue.push(guid);
  }
  if (queue.length > 0) pump();
}

/** 当前缓存状态（排障/状态栏可用）。 */
export function decorPreviewCacheStats(): { loaded: number; failed: number; pending: number } {
  return { loaded: loaded.size, failed: failed.size, pending: queue.length };
}
