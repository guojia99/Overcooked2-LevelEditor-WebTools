import { openModal, closeModal } from "./modals";

export type NavPage = "layout" | "manage" | "custom-recipes" | "recipes" | "guide" | "dependencies" | "changelog";

const GUIDE_FEATURES: [string, string, string][] = [
  ["🗺️", "关卡编辑器", "俯视图编排物品 / 装饰 / 地板层，叠放吸附、半格对齐、旋转删除，一键写回 Unity 场景"],
  ["📋", "关卡管理", "关卡集与关卡增删改，1P~4P 人数配置、音频与菜谱绑定"],
  ["📦", "依赖管理", "Bundle 依赖分析、手动编辑 LevelInfoSO.dependencies，清理未用 bundle"],
  ["📖", "菜谱清单列表", "全部菜谱按类型分组陈列，搜索筛选，按烹饪步骤展示食材与锅具"],
  ["🍽️", "自定义菜谱", "可视化组装配方、上传 3D 模型（FBX/OBJ），浏览器内在线预览"],
  ["⚙️", "与 Unity 无缝协作", "HTTP 桥接自动 Prepare For Building，资源目录一键生成"],
];

export function guideFeatureListHtml(): string {
  return GUIDE_FEATURES.map(
    ([icon, name, desc]) =>
      `<li><span>${icon}</span><b>${name}</b><span class="af-desc">${desc}</span></li>`
  ).join("");
}

const GITHUB_URL = "https://github.com/guojia99/Overcooked2-LevelEditor-WebTools";

const GITHUB_SVG = `<svg viewBox="0 0 16 16" aria-hidden="true"><path fill-rule="evenodd" d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27s1.36.09 2 .27c1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.01 8.01 0 0 0 16 8c0-4.42-3.58-8-8-8z"/></svg>`;

export function navHtml(active: NavPage): string {
  let sceneControls = "";
  if (active === "layout") {
    sceneControls = `
    <div class="nav-scene-controls">
      <label class="nav-label" for="set-select">关卡集</label>
      <select id="set-select" class="nav-select"><option value="">加载中…</option></select>
      <label class="nav-label" for="scene-select">关卡</label>
      <select id="scene-select" class="nav-select"><option value="">请先选关卡集</option></select>
    </div>`;
  }
  return `
  <nav class="topnav">
    <span class="topnav-brand">Overcooked!2 关卡工具</span>
    <button type="button" class="topnav-link${active === "layout" ? " active" : ""}" data-nav="layout">🗺️ 关卡编辑器</button>
    <button type="button" class="topnav-link${active === "manage" ? " active" : ""}" data-nav="manage">📋 关卡管理</button>
    <button type="button" class="topnav-link${active === "dependencies" ? " active" : ""}" data-nav="dependencies">📦 依赖管理</button>
    <button type="button" class="topnav-link${active === "custom-recipes" ? " active" : ""}" data-nav="custom-recipes">🍽️ 自定义菜谱</button>
    <button type="button" class="topnav-link${active === "recipes" ? " active" : ""}" data-nav="recipes">📖 菜谱清单列表</button>
    <button type="button" class="topnav-link${active === "guide" ? " active" : ""}" data-nav="guide">📘 功能说明</button>
    <button type="button" class="topnav-link${active === "changelog" ? " active" : ""}" data-nav="changelog">📜 更新日志</button>
    <span class="topnav-spacer"></span>
    ${sceneControls}
    <button type="button" class="topnav-github" data-nav-github title="作者介绍 · guojia99">${GITHUB_SVG}<span>关于</span></button>
  </nav>`;
}

function openAboutModal(): void {
  openModal(
    "关于 · 作者介绍",
    `
    <div class="about-hero">
      <span class="about-avatar">${GITHUB_SVG}</span>
      <div class="about-meta">
        <b>guojia99</b>
        <span class="muted">Overcooked!2 关卡编辑工具 · 作者</span>
      </div>
    </div>
    <p class="about-desc">一个把 Overcooked!2 关卡制作搬进浏览器的工具：俯视图编排、关卡与菜谱管理，通过 Unity 桥接直接写回工程场景。更多源码、使用说明与更新，欢迎前往 GitHub 仓库。</p>
    <a class="about-link" href="${GITHUB_URL}" target="_blank" rel="noopener">${GITHUB_SVG} GitHub 仓库</a>
    <h3 class="about-sub">工具功能</h3>
    <ul class="about-features">${guideFeatureListHtml()}</ul>
    `,
    `<button type="button" class="modal-btn primary" data-cancel>关闭</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
}

export function wireNav(onNavigate: (target: NavPage) => void): void {
  document.querySelectorAll<HTMLButtonElement>(".topnav-link").forEach((btn) => {
    btn.addEventListener("click", () => {
      const target = btn.dataset.nav as NavPage | undefined;
      if (target) onNavigate(target);
    });
  });
  document.querySelector<HTMLButtonElement>("[data-nav-github]")?.addEventListener("click", openAboutModal);
}
