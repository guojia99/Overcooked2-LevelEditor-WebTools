/**
 * 启动环境一次性自检：/api/env/status（后端实测聚合）。
 *
 * 前端启动时通过 initEnvStatus() 拉取一次并缓存，各功能据此判断依赖可用性：
 *  - commonW：common_w 源库是否已装配（含实测 id 清单，webBuiltin 消费）
 *  - audioExports / audioExportClips：音频导出是否存在（音乐/音效试听）
 *  - gameBundles / gameBundleCount：游戏 bundle 目录是否存在
 *  - dumpManifest：dump_bundle 清单是否存在（bundle 分析）
 *  - staticDist / knowledgeLoaded / dictionaryLoaded：静态页/知识库/词典
 *
 * 拉取失败（后端未启动）时 envStatus() 返回 null，消费方一律按"不可用"处理。
 */

import type { CommonWManifest } from "./webBuiltin";

export interface EnvStatus {
  ok: boolean;
  port?: number;
  /** web 静态页面（dist）就绪。 */
  staticDist?: boolean;
  schemaVersion?: number;
  /** recipe-knowledge 已加载。 */
  knowledgeLoaded?: boolean;
  /** 手册词典已加载。 */
  dictionaryLoaded?: boolean;
  /** common_w 源库（exists=false 或缺失 → 一切 web 内置内容禁用）。 */
  commonW?: CommonWManifest;
  /** 音频导出清单存在。 */
  audioExports?: boolean;
  /** 已导出的 ogg 数量。 */
  audioExportClips?: number;
  /** 游戏 bundle 目录存在。 */
  gameBundles?: boolean;
  /** 游戏 bundle 数量。 */
  gameBundleCount?: number;
  /** dump_bundle/manifest.json 存在。 */
  dumpManifest?: boolean;
}

let _env: EnvStatus | null = null;
let _loaded = false;

/** 启动自检（幂等：重复调用直接返回；失败按 null = 全部不可用处理）。 */
export async function initEnvStatus(): Promise<void> {
  if (_loaded) return;
  _loaded = true;
  try {
    const r = await fetch("/api/env/status");
    _env = r.ok ? ((await r.json()) as EnvStatus) : null;
  } catch {
    _env = null;
  }
}

/** 缓存的环境状态（未初始化/拉取失败时为 null）。 */
export function envStatus(): EnvStatus | null {
  return _env;
}

/** 强制重新拉取（"依赖状态检查"弹窗的"重新检查"按钮）。 */
export async function refreshEnvStatus(): Promise<EnvStatus | null> {
  try {
    const r = await fetch("/api/env/status");
    _env = r.ok ? ((await r.json()) as EnvStatus) : null;
  } catch {
    _env = null;
  }
  return _env;
}

/** common_w 段（不存在/未装配时为 null）。 */
export function commonWStatus(): CommonWManifest | null {
  const cw = _env?.commonW;
  return cw && cw.exists ? cw : null;
}
