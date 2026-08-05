export type NavPage = "layout" | "manage" | "custom-recipes" | "recipes";

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
    <button type="button" class="topnav-link${active === "custom-recipes" ? " active" : ""}" data-nav="custom-recipes">🍽️ 自定义菜谱</button>
    <button type="button" class="topnav-link${active === "recipes" ? " active" : ""}" data-nav="recipes">📖 菜谱清单列表</button>
    ${sceneControls}
  </nav>`;
}

export function wireNav(onNavigate: (target: NavPage) => void): void {
  document.querySelectorAll<HTMLButtonElement>(".topnav-link").forEach((btn) => {
    btn.addEventListener("click", () => {
      const target = btn.dataset.nav as NavPage | undefined;
      if (target) onNavigate(target);
    });
  });
}
