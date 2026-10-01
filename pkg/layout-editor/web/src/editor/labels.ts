import { prefabIdFromPath } from "./coords";
import { S } from "./state";
import {
  getIngredientIcon,
  getCatalogIcon,
  getRecipeIcon,
  getQuestionMarkIcon
} from "./iconCaches";
import { ingredientIdByGuid, catalogItemForGuidOrPath } from "./catalog";
import { ingredientNameZh } from "../ingredientLabels";
import { tidyCatalogNameZh } from "../displayLabels";
import { isCannonSwitch } from "./stubControls";
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

/** 俯视渲染图上的底片标签：半透明黑底 + 白字（任何皮肤底图可读），
 *  字号随格子/缩放自适应、超宽自动截断；过小格不绘制。 */
export function drawTopViewBadgeLabel(
  ctx: CanvasRenderingContext2D,
  text: string,
  boxW: number,
  boxH: number
) {
  if (boxW < 16 || boxH < 12 || !text) return;
  const t = text.length > 6 ? text.slice(0, 5) + "…" : text;
  let fontSize = Math.max(9, Math.min(13, Math.min(boxW, boxH) * 0.34, 11 * S.scale));
  ctx.font = `${fontSize}px system-ui, sans-serif`;
  let tw = ctx.measureText(t).width;
  const maxW = boxW - 8;
  if (tw > maxW) {
    fontSize = Math.max(8, Math.floor((fontSize * maxW) / tw));
    if (fontSize < 8) return;
    ctx.font = `${fontSize}px system-ui, sans-serif`;
    tw = ctx.measureText(t).width;
  }
  const h = fontSize + 5;
  const w = Math.min(boxW - 2, tw + 7);
  const r = Math.min(4, h / 2);
  ctx.fillStyle = "rgba(0,0,0,0.55)";
  ctx.beginPath();
  ctx.moveTo(-w / 2 + r, -h / 2);
  ctx.lineTo(w / 2 - r, -h / 2);
  ctx.arcTo(w / 2, -h / 2, w / 2, 0, r);
  ctx.lineTo(w / 2, h / 2 - r);
  ctx.arcTo(w / 2, h / 2, 0, h / 2, r);
  ctx.lineTo(-w / 2 + r, h / 2);
  ctx.arcTo(-w / 2, h / 2, -w / 2, 0, r);
  ctx.lineTo(-w / 2, -h / 2 + r);
  ctx.arcTo(-w / 2, -h / 2, 0, -h / 2, r);
  ctx.closePath();
  ctx.fill();
  ctx.fillStyle = "#fff";
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillText(t, 0, 0.5);
}

export function drawIconWithLabel(
  ctx: CanvasRenderingContext2D,
  icon: HTMLImageElement | null,
  label: string,
  bw: number,
  bh: number
): void {
  // 与食材箱俯视图预留圆对齐：图标居中放大盖住圆环，名称下移到盒体下半区
  const iconSize = Math.min(bw * 0.82, bh * 0.64, 36 * S.scale);
  const iconCy = -bh * 0.04;
  if (icon) {
    ctx.drawImage(icon, -iconSize / 2, iconCy - iconSize / 2, iconSize, iconSize);
  } else {
    // subtle placeholder ring while the icon loads
    ctx.strokeStyle = "rgba(255,255,255,0.25)";
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.arc(0, iconCy, iconSize * 0.48, 0, Math.PI * 2);
    ctx.stroke();
  }
  ctx.save();
  ctx.translate(0, bh * 0.36 + 2);
  drawLabelInBox(ctx, label, bw - 4, bh * 0.38);
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
  // 随机食材箱：显示所选问号图标样式（设了食材也显示）+ 候选数
  const rndCount = item.dispenser?.randomItemGuids?.length ?? 0;
  if (rndCount > 0) {
    drawIconWithLabel(
      ctx,
      getQuestionMarkIcon(item.dispenser?.questionMarkGuid ?? ""),
      `随机${rndCount}种`,
      bw,
      bh
    );
    return true;
  }
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
  if (!cat) return false;
  if (cat.ingredientDecor) {
    drawIconWithLabel(ctx, getIngredientIcon(cat.id), itemLabel(item), bw, bh);
    return true;
  }
  if (cat.recipeDecor) {
    drawIconWithLabel(ctx, getRecipeIcon(cat.id), itemLabel(item), bw, bh);
    return true;
  }
  if (!cat.icon) return false;
  drawIconWithLabel(ctx, getCatalogIcon(cat.id), itemLabel(item), bw, bh);
  return true;
}

/** 大炮开关（p_dlc0{8,9}_button_cannon）特殊图标：金色五角星
 *  （对应 Unity 里带五角星标志的大炮发射按钮）+ 名称；非大炮开关返回 false。 */
export function drawCannonSwitchStarIcon(
  ctx: CanvasRenderingContext2D,
  item: EditorItem,
  bw: number,
  bh: number
): boolean {
  if (!isCannonSwitch(item)) return false;
  const starSize = Math.min(bw * 0.8, bh * 0.46, 30 * S.scale);
  const starCy = -bh / 4 - 1;
  const outer = starSize * 0.5;
  const inner = outer * 0.42;
  ctx.save();
  ctx.shadowColor = "rgba(255,255,255,0.4)";
  ctx.shadowBlur = 3 * S.scale;
  ctx.fillStyle = "#ffd54a";
  ctx.strokeStyle = "rgba(120,80,0,0.85)";
  ctx.lineWidth = Math.max(1, 1.4 * S.scale);
  ctx.beginPath();
  for (let i = 0; i < 10; i++) {
    const r = i % 2 === 0 ? outer : inner;
    const a = -Math.PI / 2 + (i * Math.PI) / 5;
    const x = Math.cos(a) * r;
    const y = starCy + Math.sin(a) * r;
    if (i === 0) ctx.moveTo(x, y);
    else ctx.lineTo(x, y);
  }
  ctx.closePath();
  ctx.fill();
  ctx.stroke();
  ctx.restore();
  ctx.save();
  ctx.translate(0, bh / 4 + 1);
  drawLabelInBox(ctx, itemLabel(item), bw - 4, bh / 2);
  ctx.restore();
  return true;
}

export function itemLabel(item: EditorItem): string {
  const id = prefabIdFromPath(item.prefabAssetPath);
  if (item.stubKind === "Collision") {
    if (item.displayName === "AirWall" || item.airWall) return "空气墙（隐形碰撞）";
    if (item.displayName === "AirSlope" || item.airSlope) {
      const s = item.slope;
      return s ? `空气斜坡 ${s.angleDeg.toFixed(0)}°` : "空气斜坡（可行走）";
    }
    return item.displayName || "碰撞块";
  }
  const isDispenser =
    (item.stubKind === "Dispenser" || id === "Dispenser") && id !== "Backpack";
  if (isDispenser) {
    const rnd = item.dispenser?.randomItemGuids?.length ?? 0;
    if (rnd > 0) return `随机${rnd}种（?）`;
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
