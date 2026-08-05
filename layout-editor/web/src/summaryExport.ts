/** 汇总页 PNG 导出。
 *
 *  Uses pure SVG composition (rects + <image> data URLs + <text>) instead of
 *  SVG <foreignObject>: Chrome taints the canvas when drawing an SVG image that
 *  contains foreignObject content ("Tainted canvases may not be exported"),
 *  even when the SVG carries zero external references. Pure SVG is origin-clean,
 *  so the canvas can be exported as PNG.
 *
 *  Layout constants mirror the .sum-recipes CSS in recipeList.css so the exported
 *  image matches the on-screen page. The output width = the actual rendered width
 *  of the summary node; height is computed from the same layout rules. */

export interface SummaryCardGroup {
  /** URL strings for the step/utensil icons ("" = no cooking step). */
  stepIcons: string[];
  /** URL strings for ingredient icons. */
  ingredientUrls: string[];
}

export interface SummaryCard {
  iconUrl: string;
  nameZh: string;
  nameEn: string;
  badges: string[];
  groups: SummaryCardGroup[];
}

export interface SummarySection {
  typeLabel: string;
  count: number;
  cards: SummaryCard[];
}

export interface SummaryExportData {
  title: string;
  sub: string;
  author: string;
  screenshotUrl: string;
  sections: SummarySection[];
}

// ---- layout constants (mirror recipeList.css .sum-* + .rl-* at summary scale) ----

const PAGE_PAD = 24;
const HEAD_MB = 18;
const TITLE_FS = 52;
const SUB_FS = 13;
const AUTHOR_FS = 32;
const SHOT_MAX_H = 480;
const SHOT_MB = 20;
const SECTION_MB = 18;
const GRID_GAP = 14;
const CARD_MIN_W = 220;
const CARD_RADIUS = 14;
const CARD_BG = "#232834";
const CARD_BORDER = "#333a4a";
const PROD_PAD_TOP = 12;
const ICON = 72;
const NAME_FS = 14;
const NAME_LH = 17.5;
const EN_FS = 10;
const EN_LH = 12;
const BADGE_MT = 8;
const BADGE_H = 18;
const GROUP_GAP = 8;
const GROUP_PAD_X = 8;
const CHIP = 34;
const CHIP_GAP = 10;
const STEP_ICON = 22;
const STEP_GAP = 6;
const STEP_MT = 2;
const STEP_PAD_B = 8;
const ACCENT_COLOR = "#e8b04b";
const TITLE_COLOR = "#fdf3dd";
const SUB_COLOR = "#9aa0a6";
const AUTHOR_COLOR = "#e8eaed";
const SECTION_LABEL_COLOR = "#f0d9a8";
const SECTION_COUNT_COLOR = "#8a909a";
const SECTION_COUNT_BG = "#262b36";
const PAGE_BG = "#22262e";

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

/** Contain-fit rectangle (like object-fit: contain). */
function fitRect(imgW: number, imgH: number, boxW: number, boxH: number): { w: number; h: number } {
  if (imgW <= 0 || imgH <= 0) return { w: 0, h: 0 };
  const scale = Math.min(boxW / imgW, boxH / imgH);
  return { w: imgW * scale, h: imgH * scale };
}

interface CardLayout {
  x: number;
  y: number;
  w: number;
  h: number;
  nameLines: string[];
}

interface GroupLayout {
  x: number;
  y: number;
  w: number;
  h: number;
  hasStep: boolean;
}

interface SectionLayout {
  titleY: number;
  cards: CardLayout[];
  /** groups per card, positioned within the card */
  groupLayouts: GroupLayout[][];
  h: number;
}

interface Layout {
  width: number;
  height: number;
  shotY: number;
  shotW: number;
  shotH: number;
  headBottom: number;
  sections: SectionLayout[];
}

function groupWidth(card: SummaryCard, gi: number): number {
  const n = card.groups[gi].ingredientUrls.length;
  return n * CHIP + Math.max(0, n - 1) * CHIP_GAP + GROUP_PAD_X * 2;
}

function cardWidth(card: SummaryCard, availW: number): number {
  const total = card.groups.reduce((acc, _g, i) => acc + groupWidth(card, i), 0) +
    Math.max(0, card.groups.length - 1) * GROUP_GAP;
  return Math.min(Math.max(CARD_MIN_W, total), availW);
}

function prodHeight(card: SummaryCard, cardW: number): number {
  const zhLines = wrapLines(card.nameZh, NAME_FS, 600, cardW - PROD_PAD_TOP * 2).length;
  const enH = card.nameEn ? EN_LH : 0;
  const badgeH = card.badges.length ? BADGE_H : 0;
  return Math.max(132, PROD_PAD_TOP + ICON + 8 + zhLines * NAME_LH + enH + BADGE_MT + badgeH);
}

/** Pack groups into centered lines within cardW; returns lines of group indices. */
function packGroupLines(card: SummaryCard, cardW: number): number[][] {
  const lines: number[][] = [];
  let line: number[] = [];
  let lineW = 0;
  for (let i = 0; i < card.groups.length; i++) {
    const w = groupWidth(card, i);
    if (line.length && lineW + GROUP_GAP + w > cardW) {
      lines.push(line);
      line = [];
      lineW = 0;
    }
    line.push(i);
    lineW += (lineW ? GROUP_GAP : 0) + w;
  }
  if (line.length) lines.push(line);
  return lines;
}

function groupHeight(hasStep: boolean): number {
  return hasStep ? CHIP + STEP_MT + STEP_ICON + STEP_PAD_B : CHIP;
}

function computeLayout(data: SummaryExportData, width: number, imgs: Map<string, LoadedImg>): Layout {
  const contentW = width - PAGE_PAD * 2;
  const out: Layout = {
    width,
    height: 0,
    shotY: 0,
    shotW: 0,
    shotH: 0,
    headBottom: 0,
    sections: [],
  };

  let y = PAGE_PAD;
  out.headBottom = y + Math.round(TITLE_FS * 1.15) + 6 + Math.round(SUB_FS * 1.2) + 6 + Math.round(AUTHOR_FS * 1.2);
  y = out.headBottom + HEAD_MB;

  if (data.screenshotUrl) {
    const shot = imgs.get(data.screenshotUrl);
    if (shot) {
      const fit = fitRect(shot.w, shot.h, contentW, SHOT_MAX_H);
      out.shotY = y;
      out.shotW = contentW;
      out.shotH = Math.max(1, Math.round(fit.h));
      y += out.shotH + SHOT_MB;
    }
  }

  for (const sec of data.sections) {
    const titleH = 18;
    const secY = y + titleH + 8;
    let rowY = secY;
    let rowH = 0;
    let rowX = 0;
    const cards: CardLayout[] = [];
    const groupLayouts: GroupLayout[][] = [];

    for (const card of sec.cards) {
      const w = cardWidth(card, contentW);
      if (rowX > 0 && rowX + GRID_GAP + w > contentW) {
        rowY += rowH + GRID_GAP;
        rowX = 0;
        rowH = 0;
      }
      const cx = PAGE_PAD + rowX;
      const h = prodHeight(card, w) + groupHeightOfCard(card, w);
      if (h > rowH) rowH = h;
      cards.push({ x: cx, y: rowY, w, h, nameLines: wrapLines(card.nameZh, NAME_FS, 600, w - PROD_PAD_TOP * 2) });
      groupLayouts.push(layoutGroups(card, w, cx, rowY + prodHeight(card, w)));
      rowX += w + GRID_GAP;
    }
    const secH = rowY + rowH - secY;
    out.sections.push({ titleY: y, cards, groupLayouts, h: secH });
    y += titleH + 8 + secH + SECTION_MB;
  }

  out.height = y - SECTION_MB + PAGE_PAD;
  return out;
}

function groupHeightOfCard(card: SummaryCard, w: number): number {
  const lines = packGroupLines(card, w);
  return lines.reduce((acc, line) => acc + Math.max(...line.map((i) => groupHeight(card.groups[i].stepIcons.length > 0))), 0) +
    Math.max(0, lines.length - 1) * GROUP_GAP;
}

function layoutGroups(card: SummaryCard, w: number, cardX: number, cardY: number): GroupLayout[] {
  const lines = packGroupLines(card, w);
  const out: GroupLayout[] = [];
  let y = cardY;
  for (const line of lines) {
    const lineW = line.reduce((acc, i) => acc + groupWidth(card, i), 0) + (line.length - 1) * GROUP_GAP;
    let x = cardX + Math.round((w - lineW) / 2);
    const lineH = Math.max(...line.map((i) => groupHeight(card.groups[i].stepIcons.length > 0)));
    for (const i of line) {
      const gw = groupWidth(card, i);
      out.push({ x, y, w: gw, h: lineH, hasStep: card.groups[i].stepIcons.length > 0 });
      x += gw + GROUP_GAP;
    }
    y += lineH + GROUP_GAP;
  }
  return out;
}

/** Compose the export SVG. All images must already be present in `imgs`.
 *  Layout coordinates stay in CSS px; `scale` multiplies the output raster size
 *  (the viewBox keeps the CSS-px coordinate system, so everything is re-rasterized
 *  at the higher resolution — crisp text and icons). */
function buildSvg(data: SummaryExportData, layout: Layout, imgs: Map<string, LoadedImg>, scale = 1): string {
  const parts: string[] = [];
  const W = Math.round(layout.width * scale);
  const H = Math.round(layout.height * scale);
  parts.push(`<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${layout.width} ${layout.height}">`);
  parts.push(`<rect width="${layout.width}" height="${layout.height}" fill="${PAGE_BG}"/>`);

  const cx = W / 2;

  // ---- header ----
  parts.push(`<text x="${cx}" y="${PAGE_PAD + Math.round(TITLE_FS * 0.8)}" text-anchor="middle" font-family="${escXml(pageFont)}" font-size="${TITLE_FS}" font-weight="700" fill="${TITLE_COLOR}">${escXml(data.title)}</text>`);
  parts.push(`<text x="${cx}" y="${layout.headBottom - Math.round(AUTHOR_FS * 1.2) - 26}" text-anchor="middle" font-family="${escXml(pageFont)}" font-size="${SUB_FS}" fill="${SUB_COLOR}">${escXml(data.sub)}</text>`);
  parts.push(`<text x="${cx}" y="${layout.headBottom - 10}" text-anchor="middle" font-family="${escXml(pageFont)}" font-size="${AUTHOR_FS}" font-weight="600" fill="${AUTHOR_COLOR}">${escXml(data.author)}</text>`);

  // ---- screenshot ----
  if (data.screenshotUrl && layout.shotH > 0) {
    const shot = imgs.get(data.screenshotUrl);
    if (shot) {
      const fit = fitRect(shot.w, shot.h, layout.shotW, layout.shotH);
      const sx = Math.round(cx - fit.w / 2);
      const sy = Math.round(layout.shotY + (layout.shotH - fit.h) / 2);
      parts.push(`<image href="${shot.dataUrl}" x="${sx}" y="${sy}" width="${Math.round(fit.w)}" height="${Math.round(fit.h)}"/>`);
    }
  }

  // ---- sections ----
  for (let si = 0; si < layout.sections.length; si++) {
    const sec = layout.sections[si];
    const secData = data.sections[si];
    const accentW = 4;
    parts.push(`<rect x="${PAGE_PAD}" y="${sec.titleY + 2}" width="${accentW}" height="15" rx="2" fill="${ACCENT_COLOR}"/>`);
    const labelX = PAGE_PAD + accentW + 8;
    parts.push(`<text x="${labelX}" y="${sec.titleY + 13}" font-family="${escXml(pageFont)}" font-size="14" font-weight="600" fill="${SECTION_LABEL_COLOR}">${escXml(secData.typeLabel)}</text>`);
    const countW = textWidth(String(secData.count), 11, 400) + 16;
    const countX = labelX + textWidth(secData.typeLabel, 14, 600) + 8;
    parts.push(`<rect x="${countX}" y="${sec.titleY + 1}" width="${Math.round(countW)}" height="14" rx="7" fill="${SECTION_COUNT_BG}"/>`);
    parts.push(`<text x="${countX + Math.round(countW / 2)}" y="${sec.titleY + 12}" text-anchor="middle" font-family="${escXml(pageFont)}" font-size="11" fill="${SECTION_COUNT_COLOR}">${secData.count}</text>`);

    for (let ci = 0; ci < sec.cards.length; ci++) {
      const card = sec.cards[ci];
      const c = secData.cards[ci];
      parts.push(`<rect x="${card.x}" y="${card.y}" width="${card.w}" height="${card.h}" rx="${CARD_RADIUS}" fill="${CARD_BG}" stroke="${CARD_BORDER}" stroke-width="1"/>`);

      const prodH = prodHeight(c, card.w);
      const prodImg = imgs.get("/recipe-ui/recipe-bg-main.png");
      if (prodImg) {
        parts.push(`<image href="${prodImg.dataUrl}" x="${card.x}" y="${card.y}" width="${card.w}" height="${prodH}" preserveAspectRatio="none"/>`);
      }
      // recipe icon (contain, centered)
      const icon = imgs.get(c.iconUrl);
      if (icon) {
        const fit = fitRect(icon.w, icon.h, ICON, ICON);
        parts.push(`<image href="${icon.dataUrl}" x="${Math.round(card.x + (card.w - fit.w) / 2)}" y="${card.y + PROD_PAD_TOP}" width="${Math.round(fit.w)}" height="${Math.round(fit.h)}"/>`);
      }

      // name + en
      let ty = card.y + PROD_PAD_TOP + ICON + 8;
      for (const line of card.nameLines) {
        parts.push(`<text x="${card.x + card.w / 2}" y="${ty + 12}" text-anchor="middle" font-family="${escXml(pageFont)}" font-size="${NAME_FS}" font-weight="600" fill="${TITLE_COLOR}">${escXml(line)}</text>`);
        ty += NAME_LH;
      }
      let by = ty + EN_LH;
      if (c.nameEn) {
        parts.push(`<text x="${card.x + card.w / 2}" y="${by - 2}" text-anchor="middle" font-family="${escXml(pageFont)}" font-size="${EN_FS}" fill="rgba(253,243,221,0.72)">${escXml(c.nameEn)}</text>`);
      }
      if (c.badges.length) {
        by += BADGE_MT;
        const badgeY = by;
        let bx = card.x + (card.w - badgeRowWidth(c.badges)) / 2;
        for (const b of c.badges) {
          const bw = textWidth(b, 10, 400) + 16;
          parts.push(`<rect x="${Math.round(bx)}" y="${badgeY}" width="${Math.round(bw)}" height="16" rx="8" fill="rgba(10,12,16,0.72)" stroke="rgba(255,217,138,0.4)" stroke-width="1"/>`);
          parts.push(`<text x="${Math.round(bx + bw / 2)}" y="${badgeY + 11}" text-anchor="middle" font-family="${escXml(pageFont)}" font-size="10" fill="#ffd98a">${escXml(b)}</text>`);
          bx += bw + 4;
        }
      }

      // cooking groups
      const groupLayouts = sec.groupLayouts[ci];
      for (let gi = 0; gi < groupLayouts.length; gi++) {
        const g = groupLayouts[gi];
        const group = c.groups[gi];
        const bgImg = imgs.get("/recipe-ui/recipe-bg-group.png");
        if (bgImg) {
          parts.push(`<image href="${bgImg.dataUrl}" x="${g.x}" y="${g.y}" width="${g.w}" height="${g.h}" preserveAspectRatio="none"/>`);
        }
        const n = group.ingredientUrls.length;
        const totalW = n * CHIP + Math.max(0, n - 1) * CHIP_GAP;
        let ix = g.x + (g.w - totalW) / 2;
        const iy = g.y + (g.hasStep ? 0 : Math.max(0, (g.h - CHIP) / 2));
        for (const u of group.ingredientUrls) {
          const img = imgs.get(u);
          if (img) {
            const fit = fitRect(img.w, img.h, CHIP, CHIP);
            parts.push(`<image href="${img.dataUrl}" x="${Math.round(ix + (CHIP - fit.w) / 2)}" y="${Math.round(iy + (CHIP - fit.h) / 2)}" width="${Math.round(fit.w)}" height="${Math.round(fit.h)}"/>`);
          }
          ix += CHIP + CHIP_GAP;
        }
        if (g.hasStep) {
          const steps = group.stepIcons;
          const stW = steps.length * STEP_ICON + Math.max(0, steps.length - 1) * STEP_GAP;
          let sx = g.x + (g.w - stW) / 2;
          const sy = g.y + CHIP + STEP_MT;
          for (const u of steps) {
            const img = imgs.get(u);
            if (img) {
              const fit = fitRect(img.w, img.h, STEP_ICON, STEP_ICON);
              parts.push(`<image href="${img.dataUrl}" x="${Math.round(sx + (STEP_ICON - fit.w) / 2)}" y="${Math.round(sy + (STEP_ICON - fit.h) / 2)}" width="${Math.round(fit.w)}" height="${Math.round(fit.h)}"/>`);
            }
            sx += STEP_ICON + STEP_GAP;
          }
        }
      }
    }
  }

  parts.push("</svg>");
  return parts.join("\n");
}

function badgeRowWidth(badges: string[]): number {
  return badges.reduce((acc, b) => acc + textWidth(b, 10, 400) + 16, 0) + Math.max(0, badges.length - 1) * 4;
}

/** Supersample factor for the export: layout is authored in CSS px, the output
 *  raster is this many times larger. 1 = actual rendered size. */
const EXPORT_SCALE = 1;

/** Export the summary as a PNG (lossless, size unbounded) sized to the given width
 *  (the actual rendered width of the on-screen summary node), rendered at
 *  EXPORT_SCALE× resolution. */
export async function exportSummaryPng(data: SummaryExportData, widthPx: number, fileName: string): Promise<void> {
  pageFont = getComputedStyle(document.body).fontFamily || "sans-serif";
  const width = Math.max(320, Math.round(widthPx));

  const urls: string[] = ["/recipe-ui/recipe-bg-main.png", "/recipe-ui/recipe-bg-group.png"];
  if (data.screenshotUrl) urls.push(data.screenshotUrl);
  for (const sec of data.sections) {
    for (const c of sec.cards) {
      urls.push(c.iconUrl);
      for (const g of c.groups) {
        for (const u of g.ingredientUrls) urls.push(u);
        for (const u of g.stepIcons) urls.push(u);
      }
    }
  }
  const imgs = await loadImages(urls);

  const layout = computeLayout(data, width, imgs);
  const svg = buildSvg(data, layout, imgs, EXPORT_SCALE);
  const url = URL.createObjectURL(new Blob([svg], { type: "image/svg+xml;charset=utf-8" }));
  try {
    const img = new Image();
    await new Promise<void>((resolve, reject) => {
      img.onload = () => resolve();
      img.onerror = () => reject(new Error("导出渲染失败"));
      img.src = url;
    });
    const outW = Math.round(layout.width * EXPORT_SCALE);
    const outH = Math.round(layout.height * EXPORT_SCALE);
    const canvas = document.createElement("canvas");
    canvas.width = outW;
    canvas.height = outH;
    const ctx = canvas.getContext("2d");
    if (!ctx) throw new Error("无法创建画布");
    ctx.fillStyle = PAGE_BG;
    ctx.fillRect(0, 0, outW, outH);
    ctx.drawImage(img, 0, 0, outW, outH);
    const a = document.createElement("a");
    a.href = canvas.toDataURL("image/png");
    a.download = fileName;
    a.click();
  } finally {
    URL.revokeObjectURL(url);
  }
}
