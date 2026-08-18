/**
 * 依赖状态检查弹窗：读取 /api/env/status（envStatus.ts 启动自检缓存），
 * 逐项展示依赖是否就绪，缺失时给出操作指引。布局编辑器工具栏「🩺 依赖检查」打开。
 */
import { envStatus, refreshEnvStatus, type EnvStatus } from "../../envStatus";
import { syncWebBuiltinFromEnv } from "../../webBuiltin";
import { openModal, closeModal } from "../../modals";

interface DepRow {
  name: string;
  ok: boolean;
  /** 就绪时的附加信息（如条目数量/版本）。 */
  detail?: string;
  /** 缺失时的操作指引。 */
  fix?: string;
}

function buildRows(env: EnvStatus | null): DepRow[] {
  if (!env) {
    return [
      {
        name: "后端服务",
        ok: false,
        fix: "无法连接 Unity 后端。请在 Unity 菜单选择「Layout Editor / Start Server」（或 Open Bridge 后启动），然后刷新本页面。",
      },
    ];
  }

  const cw = env.commonW;
  const rows: DepRow[] = [
    {
      name: "后端服务",
      ok: !!env.ok,
      detail: env.port ? `端口 ${env.port}` : undefined,
      fix: "服务异常。请在 Unity 菜单选择「Layout Editor / Start Server」后刷新页面。",
    },
    {
      name: "Web 静态页面",
      ok: !!env.staticDist,
      fix: "Web 编辑器未构建。开发者：cd layout-editor/web && npm run build，然后刷新页面。",
    },
    {
      name: "菜谱知识库",
      ok: !!env.knowledgeLoaded,
      fix: "recipe-knowledge.json 未加载。确认 layout-editor/scripts/data/recipe-knowledge.json 存在，然后在 Unity 中触发一次脚本重编译（或重启编辑器）强制重载。",
    },
    {
      name: "手册词典",
      ok: !!env.dictionaryLoaded,
      fix: "names-dictionary.json 未加载。确认 layout-editor/scripts/data/names-dictionary.json 存在，然后在 Unity 中触发一次脚本重编译强制重载。",
    },
    {
      name: "common_w（Web 内置内容）",
      ok: !!cw?.exists,
      detail: cw?.exists
        ? `版本 ${cw.version} · 菜谱 ${cw.recipes.length} · 食材 ${cw.ingredients.length} · 道具 ${cw.prefabs.length}`
        : undefined,
      fix: "common_w 为开发者提供的外置文件夹。请将开发者分发的 common_w 文件夹整体放入 Unity 项目的 Assets/ 目录下（即 Assets/common_w，内含 version.txt），等待 Unity 导入完成后点击「重新检查」。未装配时全部 Web 内置菜谱/食材/道具不可用。",
    },
    {
      name: "游戏 Bundle",
      ok: !!env.gameBundles,
      detail: env.gameBundles ? `${env.gameBundleCount ?? 0} 个 bundle` : undefined,
      fix: "游戏 AssetBundle 目录缺失。请将游戏的 bundle 文件放入 Assets/StreamingAssets/Windows/（来自游戏安装目录），缺失时场景内游戏道具无法正常加载。",
    },
    {
      name: "音频导出",
      ok: !!env.audioExports,
      detail: env.audioExports ? `${env.audioExportClips ?? 0} 个音频` : undefined,
      fix: "音频尚未导出。请在 Unity 菜单「Layout Editor / Open Bridge」窗口中点击「导出音频依赖」，完成后点击「重新检查」。缺失时音乐/音效试听不可用（不影响布局编辑）。",
    },
    {
      name: "Bundle 清单（dump）",
      ok: !!env.dumpManifest,
      fix: "dump_bundle/manifest.json 缺失。请在「Layout Editor / Open Bridge」窗口点击「导出 Bundle 全部内容」。缺失时 bundle 依赖分析不可用（不影响布局编辑）。",
    },
  ];
  return rows;
}

function rowsHtml(rows: DepRow[]): string {
  return rows
    .map(
      (r) => `
    <div class="dep-row ${r.ok ? "dep-ok" : "dep-missing"}">
      <div class="dep-head">${r.ok ? "✅" : "❌"} <b>${r.name}</b>${
        r.detail ? ` <span class="muted">${r.detail}</span>` : ""
      }</div>
      ${!r.ok && r.fix ? `<div class="dep-fix">${r.fix}</div>` : ""}
    </div>`
    )
    .join("");
}

function renderBody(): string {
  const rows = buildRows(envStatus());
  const missing = rows.filter((r) => !r.ok).length;
  const summary =
    missing === 0
      ? '<p class="modal-hint">全部依赖就绪。</p>'
      : `<p class="modal-hint err">${missing} 项依赖缺失/异常，请按下方指引处理。</p>`;
  return `${summary}${rowsHtml(rows)}
    <p class="modal-hint">状态变更后，部分已打开的列表/面板需刷新页面才会重新加载数据。</p>`;
}

/** 打开依赖状态检查弹窗。 */
export function openDepsCheckModal(): void {
  const panel = openModal("🩺 依赖状态检查", renderBody(), `
    <button type="button" class="modal-btn" id="deps-recheck">🔄 重新检查</button>
    <button type="button" class="modal-btn primary" id="deps-close">关闭</button>
  `);
  panel.querySelector("#deps-close")?.addEventListener("click", () => closeModal());
  panel.querySelector("#deps-recheck")?.addEventListener("click", async (e) => {
    const btn = e.currentTarget as HTMLButtonElement;
    btn.disabled = true;
    btn.textContent = "检查中…";
    await refreshEnvStatus();
    // 同步 common_w 清单缓存（白名单/闭包逻辑下次取数时自动跟随）
    syncWebBuiltinFromEnv();
    const body = panel.querySelector(".modal-body");
    if (body) body.innerHTML = renderBody();
    btn.disabled = false;
    btn.textContent = "🔄 重新检查";
  });
}
