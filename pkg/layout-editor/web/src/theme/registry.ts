import type { ThemeDefinition, ThemeId } from "./types";
import { blackGoldTheme } from "./themes/blackGold";
import { pinkTheme } from "./themes/pink";
import { skyBlueTheme } from "./themes/skyBlue";
import { whiteTheme } from "./themes/white";

export const THEMES: ThemeDefinition[] = [
  blackGoldTheme,
  pinkTheme,
  skyBlueTheme,
  whiteTheme,
];

export const DEFAULT_THEME_ID: ThemeId = "black-gold";

const themeById = new Map<ThemeId, ThemeDefinition>(
  THEMES.map((t) => [t.id, t])
);

export function getThemeById(id: ThemeId): ThemeDefinition {
  return themeById.get(id) ?? blackGoldTheme;
}

export function isThemeId(value: string | null | undefined): value is ThemeId {
  return value != null && themeById.has(value as ThemeId);
}
