export type NavPage = "layout" | "manage";

export function navHtml(active: NavPage): string {
  return `
  <nav class="topnav">
    <span class="topnav-brand">Overcooked!2 关卡工具</span>
    <button type="button" class="topnav-link${active === "layout" ? " active" : ""}" data-nav="layout">🗺️ 关卡编辑器</button>
    <button type="button" class="topnav-link${active === "manage" ? " active" : ""}" data-nav="manage">📋 关卡管理</button>
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
