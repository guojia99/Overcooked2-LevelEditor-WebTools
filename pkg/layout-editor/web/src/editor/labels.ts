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

export function drawWebBackpackGrid(
  ctx: CanvasRenderingContext2D,
  item: EditorItem,
  bw: number,
  bh: number
): boolean {
  if (item.stubKind !== "WebBackpack") return false;
  const wb = item.webBackpack ?? {};
  const guids = wb.initialFillGuids ?? [];
  const hasFill = guids.some((g) => !!g);
  // 无预设填充：与原版背包相同——走画布默认文字标签（itemLabel →「Web背包」）
  if (!hasFill) return false;

  const cap = wb.capacity ?? 4;
  const valid = [1, 2, 4, 6, 9, 12, 16, 20, 25, 30];
  const c = valid.includes(cap) ? cap : 4;
  // 列多行少：1→1、2/4→2、6/9→3、12/16→4、20/25→5、36→6
  const dim =
    c === 1 ? 1 : c === 2 || c === 4 ? 2 : c === 6 || c === 9 ? 3 : c === 12 || c === 16 ? 4 : c === 20 || c === 25 ? 5 : 6;
  const area = Math.min(bw * 0.78, bh * 0.5);
  const pad = 2;
  // 按行列较大值收缩格宽，非方阵（6=3×2 等）不超出标签区
  const rows = Math.ceil(c / dim);
  const span = Math.max(dim, rows);
  const gw = (area - pad * 2) / span;
  const gh = gw;
  const ox = -((dim - 1) * gw) / 2;
  const oy = -bh * 0.22 - ((rows - 1) * gh) / 2;
  for (let i = 0; i < c; i++) {
    const row = Math.floor(i / dim);
    const col = i % dim;
    const x = ox + col * gw;
    const y = oy + row * gh;
    ctx.fillStyle = guids[i] ? "rgba(255,255,255,0.12)" : "rgba(255,255,255,0.05)";
    ctx.strokeStyle = "rgba(255,255,255,0.2)";
    ctx.fillRect(x, y, gw - 1, gh - 1);
    ctx.strokeRect(x, y, gw - 1, gh - 1);
    const g = guids[i];
    if (g) {
      const ingId = ingredientIdByGuid(g);
      if (ingId) {
        const img = getIngredientIcon(ingId);
        if (img && img.complete) {
          const s = Math.min(gw - 3, gh - 3);
          ctx.drawImage(img, x + (gw - s) / 2, y + (gh - s) / 2, s, s);
        }
      } else {
        // 菜谱/中间产物填充（无食材图标）：实心圆点示意该格已占
        ctx.beginPath();
        ctx.fillStyle = "rgba(255,255,255,0.55)";
        ctx.arc(x + gw / 2 - 0.5, y + gh / 2 - 0.5, Math.min(gw, gh) * 0.18, 0, Math.PI * 2);
        ctx.fill();
      }
    }
  }
  ctx.save();
  ctx.translate(0, bh * 0.36 + 2);
  drawLabelInBox(ctx, itemLabel(item), bw - 4, bh * 0.38);
  ctx.restore();
  return true;
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

export type InstanceOrdinal = { index: number; total: number };

/** 同 prefab 实例在场景内的阅读顺序序号（先 Z 后 X；仅 total>1 时展示 #n）。 */
export function instanceOrdinalByEditorKey(items: EditorItem[]): Map<string, InstanceOrdinal> {
  const groups = new Map<string, EditorItem[]>();
  for (const it of items) {
    const k = it.prefabGuid ?? it.prefabAssetPath ?? it._editorKey;
    const list = groups.get(k);
    if (list) list.push(it);
    else groups.set(k, [it]);
  }
  const out = new Map<string, InstanceOrdinal>();
  for (const list of groups.values()) {
    list.sort((a, b) => {
      const az = a._wz ?? a.worldPosition?.z ?? a.localPosition?.z ?? 0;
      const bz = b._wz ?? b.worldPosition?.z ?? b.localPosition?.z ?? 0;
      if (Math.abs(az - bz) > 1e-4) return az - bz;
      const ax = a._wx ?? a.worldPosition?.x ?? a.localPosition?.x ?? 0;
      const bx = b._wx ?? b.worldPosition?.x ?? b.localPosition?.x ?? 0;
      if (Math.abs(ax - bx) > 1e-4) return ax - bx;
      return a._editorKey.localeCompare(b._editorKey);
    });
    const total = list.length;
    for (let i = 0; i < total; i++) {
      out.set(list[i]._editorKey, { index: i + 1, total });
    }
  }
  return out;
}

let cachedOrdinalItemsSig = "";
let cachedOrdinalMap = new Map<string, InstanceOrdinal>();

function ordinalMapForScene(): Map<string, InstanceOrdinal> {
  const sig = `${S.items.length}|${S.items.map((i) => i._editorKey).join(",")}`;
  if (sig !== cachedOrdinalItemsSig) {
    cachedOrdinalItemsSig = sig;
    cachedOrdinalMap = instanceOrdinalByEditorKey(S.items);
  }
  return cachedOrdinalMap;
}

export function itemLabelDisambiguated(
  item: EditorItem,
  ordMap?: Map<string, InstanceOrdinal>
): string {
  const base = itemLabel(item);
  const ord = (ordMap ?? ordinalMapForScene()).get(item._editorKey);
  if (!ord || ord.total <= 1) return base;
  return `${base} #${ord.index}`;
}

/** 联动列表副标题：prefab id + 短 instanceId。 */
export function switchLinkTargetMeta(item: EditorItem): string {
  const pid = prefabIdFromPath(item.prefabAssetPath) || "—";
  const iid = item.instanceId ?? "";
  const shortId = iid.startsWith("u:") ? iid : iid ? `u:${iid}` : "—";
  return `${pid} · ${shortId}`;
}

/** 联动列表主标题（歧义实例名；联动序号由列表左侧序号列展示）。 */
export function switchLinkTargetCaption(
  item: EditorItem,
  _linkIndex?: number,
  ordMap?: Map<string, InstanceOrdinal>
): string {
  return itemLabelDisambiguated(item, ordMap);
}
