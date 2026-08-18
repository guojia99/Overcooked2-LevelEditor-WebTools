/** 应用版本号（发版时手动更新）。 */
export const APP_VERSION = "v0.5.3"

/** 由 vite.config.ts 的 define 注入的构建时间（ISO 字符串）；未注入时回退到加载时间。 */
declare const __APP_BUILD_TIME__: string | undefined;
const BUILD_TIME: string =
  typeof __APP_BUILD_TIME__ !== "undefined" && __APP_BUILD_TIME__
    ? __APP_BUILD_TIME__
    : new Date().toISOString();

/** 将 ISO 时间格式化为 "YYYY-MM-DD HH:mm"。 */
function fmtBuildTime(iso: string): string {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return iso;
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

const FORMATTED = fmtBuildTime(BUILD_TIME);

/** 在页面右下角挂载始终可见的版本号徽标。幂等：重复调用不会重复创建。 */
export function mountVersionBadge(): void {
  if (document.getElementById("version-badge")) return;
  const el = document.createElement("div");
  el.id = "version-badge";
  el.className = "version-badge";
  el.textContent = `${APP_VERSION} · ${FORMATTED}`;
  el.title = `版本 ${APP_VERSION}\n构建 ${FORMATTED}`;
  document.body.appendChild(el);
}
