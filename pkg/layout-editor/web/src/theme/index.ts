import { DEFAULT_THEME_ID, getThemeById, isThemeId, THEMES } from "./registry";
import type { ThemeDefinition, ThemeId } from "./types";
import { THEME_STORAGE_KEY } from "./types";

export type { ThemeDefinition, ThemeId } from "./types";
export { THEME_STORAGE_KEY } from "./types";
export { THEMES, DEFAULT_THEME_ID } from "./registry";

let activeTheme: ThemeDefinition = getThemeById(DEFAULT_THEME_ID);

export function getActiveTheme(): ThemeDefinition {
  return activeTheme;
}

export function getActiveThemeId(): ThemeId {
  return activeTheme.id;
}

export function listThemes(): readonly ThemeDefinition[] {
  return THEMES;
}

export function readStoredThemeId(): ThemeId {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (isThemeId(stored)) return stored;
  } catch {
    /* private mode / blocked storage */
  }
  return DEFAULT_THEME_ID;
}

export function applyTheme(id: ThemeId): void {
  activeTheme = getThemeById(id);
  document.documentElement.dataset.theme = activeTheme.cssDataTheme;
  try {
    localStorage.setItem(THEME_STORAGE_KEY, id);
  } catch {
    /* ignore */
  }
  document.dispatchEvent(
    new CustomEvent("oc2-theme-change", { detail: { id } })
  );
}

export function initTheme(): void {
  applyTheme(readStoredThemeId());
}

/** 2D 画布空白区底色（仅 void 背景，不影响物品/地板配色）。 */
export function getCanvasVoidBg(): string {
  const v = getComputedStyle(document.documentElement)
    .getPropertyValue("--ui-canvas-void-bg")
    .trim();
  return v || "#1a1d23";
}

/** Inline script body for index.html — must stay in sync with readStoredThemeId/applyTheme. */
export const THEME_BOOTSTRAP_SCRIPT = `(function(){try{var k="oc2-ui-theme";var v=localStorage.getItem(k);var t=v==="pink"||v==="sky-blue"||v==="white"?v:"black-gold";document.documentElement.dataset.theme=t;}catch(e){document.documentElement.dataset.theme="black-gold";}})();`;
