/** 关卡列表「一键导出关卡截图」PNG 导出（纯前端）。
 *
 *  与 summaryExport.ts 同思路：纯 SVG 组合（rect + <image> data URL + <text>）
 *  绘入 canvas 后导出 PNG，避免 foreignObject 导致的 canvas 污染。
 *  导出内容：页头为关卡集中/英文名，卡片仅含每关截图 + 中文名 + 英文名；
 *  不含 s_* 场景/关卡标识等元信息，也不含任何操作按钮。 */

export interface LevelShotCard {
  /** 关卡截图 URL（"" 或加载失败 = 「无截图」占位）。 */
  screenshotUrl: string;
  nameZh: string;
  nameEn: string;
}

export interface LevelShotExportData {
  /** 页头：关卡集中文名。 */
  title: string;
  /** 页头副标题：关卡集英文名（可含关卡数）。 */
  sub: string;
  cards: LevelShotCard[];
}

// ---- layout constants（镜像 style.css .m-level-card / .m-level-grid）----

const PAGE_PAD = 24;
const HEAD_MB = 22;
const TITLE_FS = 44;
const SUB_FS = 14;
const SUB_GAP = 8;
const GRID_GAP = 12;
const MIN_CARD_W = 250;
const MAX_COLS = 4;
const CARD_RADIUS = 8;
const CARD_BG = "#232834";
const CARD_BORDER = "#333a4a";
const SHOT_RATIO = 0.6;
const SHOT_EMPTY_BG = "#1a1d23";
const TEXT_PAD_X = 12;
const TEXT_MT = 10;
const TEXT_MB = 12;
const NAME_FS = 14;
const NAME_LH = 18;
const EN_FS = 11;
const EN_LH = 14;
const PAGE_BG = "#22262e";
const TITLE_COLOR = "#fdf3dd";
const SUB_COLOR = "#9aa0a6";
const NAME_COLOR = "#fdf3dd";
const EN_COLOR = "rgba(253,243,221,0.66)";

interface LoadedImg {
  dataUrl: string;
  w: number;
  h: number;
}

function escXml(s: string): string {
  return s
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&apos;");
}

const measureCtx = document.createElement("canvas").getContext("2d")!;
let pageFont = "sans-serif";

function textWidth(text: string, size: number, weight: number | string): number {
  measureCtx.font = `${weight} ${size}px ${pageFont}`;
  return measureCtx.measureText(text).width;
}

/** Greedy line wrap (CJK-friendly: char by char). */
function wrapLines(text: string, size: number, weight: number | string, maxW: number): string[] {
  if (!text) return [];
  if (maxW <= 0) return [text];
  if (textWidth(text, size, weight) <= maxW) return [text];
  const lines: string[] = [];
  let line = "";
  for (const ch of text) {
    if (textWidth(line + ch, size, weight) > maxW && line) {
      lines.push(line);
      line = ch;
    } else {
      line += ch;
    }
  }
  if (line) lines.push(line);
  return lines.length ? lines : [text];
}

async function fetchAsDataUrl(url: string): Promise<string | null> {
  try {
    const resp = await fetch(url);
    if (!resp.ok) return null;
    const blob = await resp.blob();
    return await new Promise<string | null>((resolve) => {
      const fr = new FileReader();
      fr.onload = () => resolve(fr.result as string);
      fr.onerror = () => resolve(null);
      fr.readAsDataURL(blob);
    });
  } catch {
    return null;
  }
}

async function loadImages(urls: string[]): Promise<Map<string, LoadedImg>> {
  const map = new Map<string, LoadedImg>();
  const seen = new Set<string>();
  const uniq = urls.filter((u) => u && !seen.has(u) && seen.add(u));
  await Promise.all(
    uniq.map(async (u) => {
      try {
        const dataUrl = await fetchAsDataUrl(u);
        if (!dataUrl) return;
        const img = new Image();
        await new Promise<void>((resolve, reject) => {
          img.onload = () => resolve();
          img.onerror = () => reject(new Error("decode failed"));
          img.src = dataUrl;
        });
        map.set(u, { dataUrl, w: img.naturalWidth, h: img.naturalHeight });
      } catch {
        /* skip missing images */
      }
    })
  );
  return map;
}

/** 仅上侧圆角的路径（作为截图区域的 clip，等价 border-radius: 8px 8px 0 0）。 */
function roundedTopPath(x: number, y: number, w: number, h: number, r: number): string {
  const rr = Math.min(r, w / 2, h / 2);
  return (
    `M${x},${y + h} L${x},${y + rr} Q${x},${y} ${x + rr},${y}` +
    ` L${x + w - rr},${y} Q${x + w},${y} ${x + w},${y + rr} L${x + w},${y + h} Z`
  );
}

interface CardLayout {
  x: number;
  y: number;
  w: number;
  h: number;
  shotH: number;
  nameLines: string[];
  hasEn: boolean;
  hasShot: boolean;
}

interface Layout {
  width: number;
  height: number;
  cards: CardLayout[];
}

function computeLayout(data: LevelShotExportData, width: number, imgs: Map<string, LoadedImg>): Layout {
  const contentW = width - PAGE_PAD * 2;
  const cols = Math.min(MAX_COLS, Math.max(1, Math.floor((contentW + GRID_GAP) / (MIN_CARD_W + GRID_GAP))));
  const cardW = Math.floor((contentW - (cols - 1) * GRID_GAP) / cols);
  const textW = cardW - TEXT_PAD_X * 2;

  const cards: CardLayout[] = [];
  let rowY = PAGE_PAD + Math.round(TITLE_FS * 1.15) + SUB_GAP + Math.round(SUB_FS * 1.3) + HEAD_MB;
  for (let i = 0; i < data.cards.length; i++) {
    const c = data.cards[i];
    const col = i % cols;
    if (col === 0 && i > 0) rowY += cards[i - 1].h + GRID_GAP;
    const nameLines = wrapLines(c.nameZh, NAME_FS, 600, textW);
    const lineCount = Math.max(1, nameLines.length);
    const hasEn = !!c.nameEn;
    const shotH = Math.round(cardW * SHOT_RATIO);
    const h = shotH + TEXT_MT + lineCount * NAME_LH + (hasEn ? EN_LH : 0) + TEXT_MB;
    cards.push({
      x: PAGE_PAD + col * (cardW + GRID_GAP),
      y: rowY,
      w: cardW,
      h,
      shotH,
      nameLines: nameLines.length ? nameLines : [""],
      hasEn,
      hasShot: !!c.screenshotUrl && imgs.has(c.screenshotUrl),
    });
  }

  const bottom = cards.length ? cards[cards.length - 1].y + cards[cards.length - 1].h : rowY - HEAD_MB;
  return { width, height: bottom + PAGE_PAD, cards };
}

function buildSvg(data: LevelShotExportData, layout: Layout, imgs: Map<string, LoadedImg>): string {
  const parts: string[] = [];
  parts.push(
    `<svg xmlns="http://www.w3.org/2000/svg" width="${layout.width}" height="${layout.height}" viewBox="0 0 ${layout.width} ${layout.height}">`
  );
  parts.push(`<rect width="${layout.width}" height="${layout.height}" fill="${PAGE_BG}"/>`);

  const cx = layout.width / 2;
  parts.push(
    `<text x="${cx}" y="${PAGE_PAD + Math.round(TITLE_FS * 0.8)}" text-anchor="middle" font-family="${escXml(pageFont)}" font-size="${TITLE_FS}" font-weight="700" fill="${TITLE_COLOR}">${escXml(data.title)}</text>`
  );
  parts.push(
    `<text x="${cx}" y="${PAGE_PAD + Math.round(TITLE_FS * 1.15) + SUB_GAP + Math.round(SUB_FS * 0.95)}" text-anchor="middle" font-family="${escXml(pageFont)}" font-size="${SUB_FS}" fill="${SUB_COLOR}">${escXml(data.sub)}</text>`
  );

  for (let i = 0; i < layout.cards.length; i++) {
    const card = layout.cards[i];
    parts.push(
      `<rect x="${card.x}" y="${card.y}" width="${card.w}" height="${card.h}" rx="${CARD_RADIUS}" fill="${CARD_BG}" stroke="${CARD_BORDER}" stroke-width="1"/>`
    );
    parts.push(`<clipPath id="shot-${i}"><path d="${roundedTopPath(card.x, card.y, card.w, card.shotH, CARD_RADIUS)}"/></clipPath>`);
    const shot = card.hasShot ? imgs.get(data.cards[i].screenshotUrl) : undefined;
    if (shot) {
      // object-fit: cover —— slice 等比裁切填满整个截图区
      parts.push(
        `<image href="${shot.dataUrl}" x="${card.x}" y="${card.y}" width="${card.w}" height="${card.shotH}" preserveAspectRatio="xMidYMid slice" clip-path="url(#shot-${i})"/>`
      );
    } else {
      parts.push(
        `<path d="${roundedTopPath(card.x, card.y, card.w, card.shotH, CARD_RADIUS)}" fill="${SHOT_EMPTY_BG}"/>`
      );
      parts.push(
        `<text x="${cx}" y="${card.y + card.shotH / 2 + 4}" text-anchor="middle" font-family="${escXml(pageFont)}" font-size="12" fill="#7c8390">无截图</text>`
      );
    }

    let ty = card.y + card.shotH + TEXT_MT;
    for (const line of card.nameLines) {
      if (line) {
        parts.push(
          `<text x="${card.x + card.w / 2}" y="${ty + 13}" text-anchor="middle" font-family="${escXml(pageFont)}" font-size="${NAME_FS}" font-weight="600" fill="${NAME_COLOR}">${escXml(line)}</text>`
        );
      }
      ty += NAME_LH;
    }
    if (card.hasEn) {
      parts.push(
        `<text x="${card.x + card.w / 2}" y="${ty + EN_LH - 3}" text-anchor="middle" font-family="${escXml(pageFont)}" font-size="${EN_FS}" fill="${EN_COLOR}">${escXml(data.cards[i].nameEn)}</text>`
      );
    }
  }

  parts.push("</svg>");
  return parts.join("\n");
}

/** 一键导出全部关卡截图：按给定页宽（列表实际渲染宽度）合成单张 PNG 下载。 */
export async function exportLevelShotsPng(data: LevelShotExportData, widthPx: number, fileName: string): Promise<void> {
  pageFont = getComputedStyle(document.body).fontFamily || "sans-serif";
  const width = Math.max(320, Math.round(widthPx));

  const imgs = await loadImages(data.cards.map((c) => c.screenshotUrl));
  const layout = computeLayout(data, width, imgs);
  const svg = buildSvg(data, layout, imgs);
  const url = URL.createObjectURL(new Blob([svg], { type: "image/svg+xml;charset=utf-8" }));
  try {
    const img = new Image();
    await new Promise<void>((resolve, reject) => {
      img.onload = () => resolve();
      img.onerror = () => reject(new Error("导出渲染失败"));
      img.src = url;
    });
    const canvas = document.createElement("canvas");
    canvas.width = layout.width;
    canvas.height = layout.height;
    const ctx = canvas.getContext("2d");
    if (!ctx) throw new Error("无法创建画布");
    ctx.fillStyle = PAGE_BG;
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
    const a = document.createElement("a");
    a.href = canvas.toDataURL("image/png");
    a.download = fileName;
    a.click();
  } finally {
    URL.revokeObjectURL(url);
  }
}
