import type { CatalogItem } from "./types";
import { isStackHostCatalog, isStackUtensilCatalog } from "./stacking";

export interface ItemPaintStyle {
  fill: string;
  stroke: string;
  label: string;
}

type Rgb = { r: number; g: number; b: number };

/** Pale tints per catalog category (top-down canvas). */
const CATEGORY_RGB: Record<string, Rgb> = {
  counters: { r: 118, g: 168, b: 255 },
  utensils: { r: 255, g: 214, b: 128 },
  mechanisms: { r: 196, g: 148, b: 255 },
  Player: { r: 255, g: 168, b: 188 },
  art: { r: 128, g: 212, b: 168 },
  other: { r: 178, g: 186, b: 200 },
};

function rgba(rgb: Rgb, alpha: number): string {
  return `rgba(${rgb.r}, ${rgb.g}, ${rgb.b}, ${alpha})`;
}

function resolveCategory(cat: CatalogItem | undefined, parentPath: string): string {
  if (cat?.category) return cat.category;
  if (parentPath.includes("Counters")) return "counters";
  if (parentPath.includes("Utensils")) return "utensils";
  if (parentPath.includes("Mechanisms")) return "mechanisms";
  if (parentPath.startsWith("Art/")) return "art";
  return "other";
}

/** Stacked utensils: translucent; carriers (tables, stations): opaque. */
export function isTransparentStackItem(
  cat: CatalogItem | undefined,
  parentPath: string
): boolean {
  if (isStackUtensilCatalog(cat)) return true;
  if ((cat?.category === "utensils" || cat?.category?.startsWith("utensils/")) && cat.stack) return true;
  if (parentPath.includes("Utensils") && cat?.stack) return true;
  return false;
}

export function isSolidCarrier(
  cat: CatalogItem | undefined,
  parentPath: string
): boolean {
  if (isTransparentStackItem(cat, parentPath)) return false;
  if (isStackHostCatalog(cat)) return true;
  if (parentPath.includes("Counters") || parentPath.includes("Mechanisms")) return true;
  if (cat?.category === "counters" || cat?.category === "mechanisms") return true;
  return false;
}

export function paintStyleForItem(
  cat: CatalogItem | undefined,
  parentPath: string,
  selected: boolean
): ItemPaintStyle {
  const category = resolveCategory(cat, parentPath);
  const rgb = cat?.id === "Dispenser" ? CATEGORY_RGB.mechanisms : CATEGORY_RGB[category] ?? CATEGORY_RGB.other;
  const transparent = isTransparentStackItem(cat, parentPath);
  const solid = isSolidCarrier(cat, parentPath);

  if (selected) {
    return {
      fill: rgba({ r: 249, g: 171, b: 0 }, transparent ? 0.62 : 0.88),
      stroke: "#f9ab00",
      label: transparent ? "#1a1d23" : "#fff",
    };
  }

  let fillAlpha: number;
  if (transparent) fillAlpha = 0.36;
  else if (solid) fillAlpha = 0.88;
  else if (cat?.layoutTier === "decor" || category === "art") fillAlpha = 0.5;
  else fillAlpha = 0.62;

  const strokeRgb: Rgb = {
    r: Math.round(rgb.r * 0.72),
    g: Math.round(rgb.g * 0.72),
    b: Math.round(rgb.b * 0.72),
  };

  return {
    fill: rgba(rgb, fillAlpha),
    stroke: rgba(strokeRgb, transparent ? 0.7 : 0.95),
    label: transparent || fillAlpha < 0.55 ? "#1a1d23" : "#fff",
  };
}
