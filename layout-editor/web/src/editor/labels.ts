import { prefabIdFromPath } from "./coords";
import { S } from "./state";
import {
  getIngredientIcon,
  getCatalogIcon
} from "./iconCaches";
import { ingredientIdByGuid, catalogItemForGuidOrPath } from "./catalog";
import { ingredientNameZh } from "../ingredientLabels";
import { tidyCatalogNameZh } from "../displayLabels";
import type { CatalogItem } from "../types";
import type { EditorItem } from "./state";

export function wrapTextLines(ctx: CanvasRenderingContext2D, text: string, maxWidth: number): string[] {
  if (maxWidth <= 4) return [text.slice(0, 1)];
  const chars = Array.from(text);
  const lines: string[] = [];
  let line = "";
  for (const ch of chars) {
    const test = line + ch;
    if (ctx.measureText(test).width > maxWidth && line.length > 0) {
      lines.push(line);
      line = ch;
    } else {
      line = test;
    }
  }
  if (line) lines.push(line);
  return lines.length ? lines : [""];
}

export function drawLabelInBox(
  ctx: CanvasRenderingContext2D,
  text: string,
  boxW: number,
  boxH: number
) {
  const pad = 3;
  const innerW = Math.max(4, boxW - pad * 2);
  const innerH = Math.max(4, boxH - pad * 2);
  let fontSize = Math.max(8, Math.min(12, innerH * 0.32, 11 * S.scale));

  for (let attempt = 0; attempt < 6; attempt++) {
    ctx.font = `${fontSize}px system-ui, sans-serif`;
    const lines = wrapTextLines(ctx, text, innerW);
    const lineHeight = fontSize * 1.12;
    if (lines.length * lineHeight <= innerH || fontSize <= 8) {
      const startY = -((lines.length - 1) * lineHeight) / 2;
      for (let i = 0; i < lines.length; i++) {
        ctx.fillText(lines[i], 0, startY + i * lineHeight);
      }
      return;
    }
    fontSize -= 1;
  }
}

export function drawIconWithLabel(
  ctx: CanvasRenderingContext2D,
  icon: HTMLImageElement | null,
  label: string,
  bw: number,
  bh: number
): void {
  const iconSize = Math.min(bw * 0.8, bh * 0.46, 30 * S.scale);
  const iconCy = -bh / 4 - 1;
  if (icon) {
    ctx.drawImage(icon, -iconSize / 2, iconCy - iconSize / 2, iconSize, iconSize);
  } else {
    // subtle placeholder ring while the icon loads
    ctx.strokeStyle = "rgba(255,255,255,0.25)";
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.arc(0, iconCy, iconSize * 0.32, 0, Math.PI * 2);
    ctx.stroke();
  }
  ctx.save();
  ctx.translate(0, bh / 4 + 1);
  drawLabelInBox(ctx, label, bw - 4, bh / 2);
  ctx.restore();
}

export function drawDispenserIngredient(
  ctx: CanvasRenderingContext2D,
  item: EditorItem,
  bw: number,
  bh: number
): boolean {
  // Draw the 食材箱 (Dispenser) selected ingredient: icon on top + name below. Returns false when
  // the item isn't a dispenser with a set ingredient (caller falls back to the plain label).
  const id = prefabIdFromPath(item.prefabAssetPath);
  if (item.stubKind !== "Dispenser" && id !== "Dispenser" && id !== "Backpack") return false;
  const guid = item.dispenser?.spawnerItemPrefabGuid;
  if (!guid) return false;
  const ingId = ingredientIdByGuid(guid);
  const name = ingredientNameZh(S.ingredientsCache, guid);
  if (!ingId || name === "未设置") return false;
  drawIconWithLabel(ctx, getIngredientIcon(ingId), name, bw, bh);
  return true;
}

export function drawCatalogItemIcon(
  ctx: CanvasRenderingContext2D,
  cat: CatalogItem | undefined,
  item: EditorItem,
  bw: number,
  bh: number
): boolean {
  if (!cat?.icon) return false;
  drawIconWithLabel(ctx, getCatalogIcon(cat.id), itemLabel(item), bw, bh);
  return true;
}

export function itemLabel(item: EditorItem): string {
  const id = prefabIdFromPath(item.prefabAssetPath);
  if (item.stubKind === "Collision") {
    return item.displayName === "AirWall" ? "空气墙（隐形碰撞）" : item.displayName || "碰撞块";
  }
  const isDispenser =
    (item.stubKind === "Dispenser" || id === "Dispenser") && id !== "Backpack";
  if (isDispenser) {
    const ingZh = ingredientNameZh(S.ingredientsCache, item.dispenser?.spawnerItemPrefabGuid);
    if (ingZh !== "未设置") return ingZh;
  }

  if (item.stubKind === "Player" || id === "Player") {
    const pid = item.player?.playerID ?? 11;
    if (pid !== 11) return `玩家${pid + 1}`;
  }

  const cat = catalogItemForGuidOrPath(item.prefabGuid, item.prefabAssetPath);
  if (cat?.nameZh) return tidyCatalogNameZh(cat.nameZh, cat.id);
  if (item.displayName) return tidyCatalogNameZh(item.displayName, item.displayName);
  return id || "?";
}
