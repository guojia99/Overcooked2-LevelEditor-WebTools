/** DOM → SVG 快照导出（PNG 一键导出的统一引擎）。
 *
 *  设计要点：
 *  1. **导出即所见**：不再手抄 CSS 常量重排版（旧 summaryExport.ts 的做法，
 *     会与真实卡片样式持续跑偏，典型症状是「无步骤的食材分组框被同行有步骤的
 *     分组框带高到 2 格」）。这里直接遍历**实时渲染后的 DOM**，用
 *     getBoundingClientRect / getComputedStyle / Range.getClientRects 读取真实
 *     几何，样式改了导出自动跟随。**本模块对页面只读**，不改任何卡片样式/结构。
 *  2. **纯 SVG，不用 foreignObject**：Chrome 在绘制含 foreignObject 的 SVG 图片时
 *     会污染 canvas（"Tainted canvases may not be exported"），即使 SVG 零外部引用。
 *     纯 SVG（rect + image(dataURL) + text）是 origin-clean 的，可以 toDataURL。
 *  3. 所有位图（<img> 与 CSS background-image）先 fetch 成 dataURL 内联，避免污染。
 *
 *  已知取舍：
 *  - 导出瞬间鼠标若悬停在卡片上，快照会带上 hover 位移（按钮在页顶，概率低）。
 *  - 伪元素仅支持「固定尺寸装饰块」（如 .rl-section-title::before 琥珀竖条）。
 *  - box-shadow / filter: drop-shadow 不渲染；text-shadow 用一层暗色偏移文本近似。 */

// ---------------------------------------------------------------- utils

function escXml(s: string): string {
  return s
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&apos;");
}

interface LoadedImg {
  dataUrl: string;
  w: number;
  h: number;
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
        /* 缺图静默跳过，与页面 onerror 占位行为一致 */
      }
    })
  );
  return map;
}

/** 颜色是否完全透明（computed 值形如 rgba(0, 0, 0, 0) / transparent）。 */
function isTransparent(color: string): boolean {
  if (!color) return true;
  const c = color.trim().toLowerCase();
  if (c === "transparent" || c === "none") return true;
  const m = c.match(/^rgba?\(([^)]+)\)$/);
  if (!m) return false;
  const parts = m[1].split(",").map((p) => parseFloat(p));
  return parts.length >= 4 && parts[3] === 0;
}

function num(v: string): number {
  const n = parseFloat(v);
  return isNaN(n) ? 0 : n;
}

/** 顶层逗号切分（不切 rgb()/url() 括号内的逗号）。 */
function splitTopLevel(s: string): string[] {
  const out: string[] = [];
  let depth = 0;
  let cur = "";
  for (const ch of s) {
    if (ch === "(") depth++;
    else if (ch === ")") depth--;
    if (ch === "," && depth === 0) {
      out.push(cur.trim());
      cur = "";
    } else {
      cur += ch;
    }
  }
  if (cur.trim()) out.push(cur.trim());
  return out;
}

function urlOf(layer: string): string | null {
  const m = layer.match(/^url\(\s*(['"]?)(.*?)\1\s*\)$/);
  return m ? m[2] : null;
}

/** contain / cover 适配框。 */
function fitRect(iw: number, ih: number, bw: number, bh: number, cover: boolean): { w: number; h: number } {
  if (iw <= 0 || ih <= 0) return { w: bw, h: bh };
  const s = cover ? Math.max(bw / iw, bh / ih) : Math.min(bw / iw, bh / ih);
  return { w: iw * s, h: ih * s };
}

function r2(n: number): number {
  return Math.round(n * 100) / 100;
}

// ---------------------------------------------------------------- 字体度量

const metricsCtx = document.createElement("canvas").getContext("2d")!;
const metricsCache = new Map<string, { ascent: number; descent: number }>();

function fontShorthand(cs: CSSStyleDeclaration): string {
  // canvas font 简写：style weight size/lineHeight family
  return `${cs.fontStyle} ${cs.fontWeight} ${cs.fontSize} ${cs.fontFamily}`;
}

function fontMetrics(font: string, fontSize: number): { ascent: number; descent: number } {
  const hit = metricsCache.get(font);
  if (hit) return hit;
  let m = { ascent: fontSize * 0.8, descent: fontSize * 0.2 };
  try {
    metricsCtx.font = font;
    const tm = metricsCtx.measureText("Hg汉");
    const a = (tm as TextMetrics & { fontBoundingBoxAscent?: number }).fontBoundingBoxAscent;
    const d = (tm as TextMetrics & { fontBoundingBoxDescent?: number }).fontBoundingBoxDescent;
    if (typeof a === "number" && typeof d === "number" && a > 0) m = { ascent: a, descent: d };
  } catch {
    /* 退回估算值 */
  }
  metricsCache.set(font, m);
  return m;
}

// ---------------------------------------------------------------- 绘制上下文

interface Ctx {
  parts: string[];
  defs: string[];
  /** 根节点视口坐标原点（所有输出坐标 = 视口坐标 - 原点）。 */
  ox: number;
  oy: number;
  imgs: Map<string, LoadedImg>;
  clipSeq: number;
}

function pushRect(
  ctx: Ctx,
  x: number,
  y: number,
  w: number,
  h: number,
  fill: string,
  radius: number
): void {
  if (w <= 0 || h <= 0) return;
  const rx = radius > 0 ? ` rx="${r2(Math.min(radius, w / 2, h / 2))}"` : "";
  ctx.parts.push(
    `<rect x="${r2(x)}" y="${r2(y)}" width="${r2(w)}" height="${r2(h)}"${rx} fill="${escXml(fill)}"/>`
  );
}

/** linear-gradient(<angle>, c1 [pos], c2 [pos], ...) → <linearGradient>，返回 fill 引用。
 *  只支持线性渐变的角度/to-* 写法，够覆盖卡片底色与遮罩层。 */
function gradientFill(ctx: Ctx, layer: string): string | null {
  const m = layer.match(/^(repeating-)?linear-gradient\((.*)\)$/i);
  if (!m) return null;
  const args = splitTopLevel(m[2]);
  if (args.length < 2) return null;
  let angle = 180; // CSS 默认 to bottom
  let i = 0;
  const head = args[0].trim().toLowerCase();
  if (/^-?[\d.]+deg$/.test(head)) {
    angle = parseFloat(head);
    i = 1;
  } else if (head.startsWith("to ")) {
    const dir = head.slice(3).trim();
    const map: Record<string, number> = {
      top: 0,
      right: 90,
      bottom: 180,
      left: 270,
      "top right": 45,
      "right top": 45,
      "bottom right": 135,
      "right bottom": 135,
      "bottom left": 225,
      "left bottom": 225,
      "top left": 315,
      "left top": 315,
    };
    angle = map[dir] ?? 180;
    i = 1;
  } else if (/^(rgba?\(|#|[a-z]+$)/.test(head) === false) {
    return null;
  }
  const stops = args.slice(i);
  if (stops.length < 2) return null;
  // CSS 角度：0deg 向上，顺时针。换算为单位向量（SVG y 轴向下）。
  const rad = ((angle - 90) * Math.PI) / 180;
  const dx = Math.cos(rad);
  const dy = Math.sin(rad);
  const x1 = r2(0.5 - dx / 2);
  const y1 = r2(0.5 - dy / 2);
  const x2 = r2(0.5 + dx / 2);
  const y2 = r2(0.5 + dy / 2);
  const id = `g${ctx.clipSeq++}`;
  const stopXml = stops
    .map((s, idx) => {
      const parts = s.trim().split(/\s+(?=[\d.]+%?$)/);
      const color = parts[0];
      const posRaw = parts[1];
      const offset = posRaw && posRaw.endsWith("%")
        ? parseFloat(posRaw) / 100
        : idx / (stops.length - 1);
      return `<stop offset="${r2(Math.max(0, Math.min(1, offset)))}" stop-color="${escXml(color)}"/>`;
    })
    .join("");
  ctx.defs.push(
    `<linearGradient id="${id}" x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}">${stopXml}</linearGradient>`
  );
  return `url(#${id})`;
}

/** 背景图层（url / linear-gradient），CSS 顺序为「先写的在最上」，故倒序绘制。 */
function paintBackgroundLayers(ctx: Ctx, cs: CSSStyleDeclaration, x: number, y: number, w: number, h: number, radius: number): void {
  const bgImage = cs.backgroundImage;
  if (!bgImage || bgImage === "none") return;
  const layers = splitTopLevel(bgImage);
  const sizes = splitTopLevel(cs.backgroundSize || "auto");
  const positions = splitTopLevel(cs.backgroundPosition || "0% 0%");
  const clipId = radius > 0 ? `c${ctx.clipSeq++}` : "";
  if (clipId) {
    ctx.defs.push(
      `<clipPath id="${clipId}"><rect x="${r2(x)}" y="${r2(y)}" width="${r2(w)}" height="${r2(h)}" rx="${r2(Math.min(radius, w / 2, h / 2))}"/></clipPath>`
    );
    ctx.parts.push(`<g clip-path="url(#${clipId})">`);
  }
  for (let i = layers.length - 1; i >= 0; i--) {
    const layer = layers[i];
    const size = (sizes[i] ?? sizes[sizes.length - 1] ?? "auto").trim().toLowerCase();
    const pos = (positions[i] ?? positions[positions.length - 1] ?? "50% 50%").trim().toLowerCase();
    const url = urlOf(layer);
    if (url) {
      const img = ctx.imgs.get(url);
      if (!img) continue;
      let dw = w;
      let dh = h;
      let dx = x;
      let dy = y;
      if (size === "cover" || size === "contain") {
        const fit = fitRect(img.w, img.h, w, h, size === "cover");
        dw = fit.w;
        dh = fit.h;
      } else if (size !== "100% 100%" && size !== "auto" && size !== "") {
        const sp = size.split(/\s+/);
        const sw = sp[0];
        const sh = sp[1] ?? "auto";
        dw = sw.endsWith("%") ? (parseFloat(sw) / 100) * w : sw === "auto" ? img.w : num(sw);
        dh = sh.endsWith("%") ? (parseFloat(sh) / 100) * h : sh === "auto" ? (img.h * dw) / Math.max(1, img.w) : num(sh);
      } else if (size === "auto") {
        dw = img.w;
        dh = img.h;
      }
      if (dw !== w || dh !== h) {
        // background-position（只支持 center / 百分比 / 像素两值写法）
        const pp = pos.split(/\s+/);
        const px = pp[0] ?? "50%";
        const py = pp[1] ?? "50%";
        const fx = px === "left" ? 0 : px === "right" ? 1 : px === "center" ? 0.5 : px.endsWith("%") ? parseFloat(px) / 100 : null;
        const fy = py === "top" ? 0 : py === "bottom" ? 1 : py === "center" ? 0.5 : py.endsWith("%") ? parseFloat(py) / 100 : null;
        dx = fx === null ? x + num(px) : x + (w - dw) * fx;
        dy = fy === null ? y + num(py) : y + (h - dh) * fy;
      }
      ctx.parts.push(
        `<image href="${img.dataUrl}" x="${r2(dx)}" y="${r2(dy)}" width="${r2(dw)}" height="${r2(dh)}" preserveAspectRatio="none"/>`
      );
      continue;
    }
    const grad = gradientFill(ctx, layer);
    if (grad) pushRect(ctx, x, y, w, h, grad, radius);
  }
  if (clipId) ctx.parts.push("</g>");
}

/** 边框：四边同宽同色走单个 stroke 矩形（含圆角），否则逐边实心矩形近似。 */
function paintBorder(ctx: Ctx, cs: CSSStyleDeclaration, x: number, y: number, w: number, h: number, radius: number): void {
  const wt = num(cs.borderTopWidth);
  const wr = num(cs.borderRightWidth);
  const wb = num(cs.borderBottomWidth);
  const wl = num(cs.borderLeftWidth);
  if (wt <= 0 && wr <= 0 && wb <= 0 && wl <= 0) return;
  const ct = cs.borderTopColor;
  const same = wt === wr && wr === wb && wb === wl &&
    ct === cs.borderRightColor && ct === cs.borderBottomColor && ct === cs.borderLeftColor;
  if (same) {
    if (isTransparent(ct) || cs.borderTopStyle === "none") return;
    const inset = wt / 2;
    const rx = radius > 0 ? ` rx="${r2(Math.max(0, radius - inset))}"` : "";
    ctx.parts.push(
      `<rect x="${r2(x + inset)}" y="${r2(y + inset)}" width="${r2(Math.max(0, w - wt))}" height="${r2(Math.max(0, h - wt))}"${rx} fill="none" stroke="${escXml(ct)}" stroke-width="${r2(wt)}"/>`
    );
    return;
  }
  if (wt > 0 && !isTransparent(cs.borderTopColor)) pushRect(ctx, x, y, w, wt, cs.borderTopColor, 0);
  if (wb > 0 && !isTransparent(cs.borderBottomColor)) pushRect(ctx, x, y + h - wb, w, wb, cs.borderBottomColor, 0);
  if (wl > 0 && !isTransparent(cs.borderLeftColor)) pushRect(ctx, x, y, wl, h, cs.borderLeftColor, 0);
  if (wr > 0 && !isTransparent(cs.borderRightColor)) pushRect(ctx, x + w - wr, y, wr, h, cs.borderRightColor, 0);
}

// ---------------------------------------------------------------- 文本

interface TextLine {
  text: string;
  x: number;
  y: number;
  h: number;
  /** 行的实测宽度（用于 textLength 钉宽）。 */
  w: number;
}

/** 把一个文本节点拆成「行」：单行直出；多行才逐字符定位（性能保护，
 *  菜谱清单整页有数百张卡片，绝大多数文本都是单行）。 */
function textLines(node: Text): TextLine[] {
  const raw = node.nodeValue ?? "";
  if (!raw.trim()) return [];
  const range = document.createRange();
  // 只量「首个非空白 ~ 末个非空白」这一段：折叠掉的首尾空白也会被算进 client rect，
  // 直接用整节点的 rect 会让 x 左移一个空格宽、textLength 也被拉长。
  const start = raw.search(/\S/);
  let end = raw.length;
  while (end > start && /\s/.test(raw.charAt(end - 1))) end--;
  range.setStart(node, start);
  range.setEnd(node, end);
  const rects = Array.from(range.getClientRects()).filter((r) => r.width > 0 && r.height > 0);
  if (rects.length === 0) return [];
  if (rects.length === 1) {
    const r = rects[0]!;
    return [
      { text: raw.slice(start, end).replace(/\s+/g, " "), x: r.left, y: r.top, h: r.height, w: r.width },
    ];
  }
  const lines: TextLine[] = [];
  let cur: { chars: string[]; top: number; left: number; right: number; h: number } | null = null;
  const flush = (): void => {
    if (!cur) return;
    const text = cur.chars.join("").replace(/\s+/g, " ").replace(/\s+$/, "");
    if (text.length > 0) {
      lines.push({ text, x: cur.left, y: cur.top, h: cur.h, w: Math.max(0, cur.right - cur.left) });
    }
  };
  for (let i = start; i < end; i++) {
    const ch = raw.charAt(i);
    range.setStart(node, i);
    range.setEnd(node, i + 1);
    const rs = range.getClientRects();
    const r = rs.length > 0 ? rs[0] : null;
    if (!r || r.width === 0) {
      // 行尾被折掉的空格
      if (cur) cur.chars.push(ch);
      continue;
    }
    if (!cur || Math.abs(r.top - cur.top) > 1) {
      flush();
      // 行首空格不参与定位（SVG 文本会丢弃前导空白）
      if (/\s/.test(ch)) {
        cur = null;
        continue;
      }
      cur = { chars: [ch], top: r.top, left: r.left, right: r.right, h: r.height };
    } else {
      cur.chars.push(ch);
      cur.right = Math.max(cur.right, r.right);
    }
  }
  flush();
  return lines;
}

function paintText(ctx: Ctx, node: Text, cs: CSSStyleDeclaration): void {
  const lines = textLines(node);
  if (lines.length === 0) return;
  const fontSize = num(cs.fontSize);
  const font = fontShorthand(cs);
  const fm = fontMetrics(font, fontSize);
  const ls = cs.letterSpacing && cs.letterSpacing !== "normal" ? ` letter-spacing="${r2(num(cs.letterSpacing))}"` : "";
  const weight = cs.fontWeight && cs.fontWeight !== "400" ? ` font-weight="${escXml(cs.fontWeight)}"` : "";
  const style = cs.fontStyle && cs.fontStyle !== "normal" ? ` font-style="${escXml(cs.fontStyle)}"` : "";
  // text-shadow 用 feDropShadow 还原（含模糊半径），比「复制一层硬边暗色文字」精确得多
  const shadow = textShadowFilter(ctx, cs.textShadow);
  for (const line of lines) {
    // 基线：行盒内按字体 ascent/descent 垂直居中
    const baseline = line.y + (line.h - (fm.ascent + fm.descent)) / 2 + fm.ascent;
    const x = line.x - ctx.ox;
    const y = baseline - ctx.oy;
    // textLength + lengthAdjust="spacing"：把整行宽度钉死成 DOM 实测宽度，
    // 只调字距不缩字形 —— 消除 SVG 与 DOM 字形步进差导致的「越往右越偏」。
    const fit = line.w > 0 ? ` textLength="${r2(line.w)}" lengthAdjust="spacing"` : "";
    const common = `font-family="${escXml(cs.fontFamily)}" font-size="${r2(fontSize)}"${weight}${style}${ls}`;
    ctx.parts.push(
      `<text x="${r2(x)}" y="${r2(y)}" ${common} fill="${escXml(cs.color)}"${fit}${shadow ? ` filter="${shadow}"` : ""}>${escXml(line.text)}</text>`
    );
  }
}

/** text-shadow → feDropShadow（只取第一层；CSS 模糊半径 b ↔ stdDeviation = b/2）。 */
function textShadowFilter(ctx: Ctx, v: string): string | null {
  if (!v || v === "none") return null;
  const first = splitTopLevel(v)[0];
  if (!first) return null;
  const colorMatch = first.match(/(rgba?\([^)]*\)|#[0-9a-f]{3,8})/i);
  const color = colorMatch ? colorMatch[0] : "rgba(0,0,0,0.6)";
  if (isTransparent(color)) return null;
  const nums = first.replace(/rgba?\([^)]*\)/gi, "").match(/-?[\d.]+px/g) ?? [];
  if (nums.length < 2) return null;
  const dx = num(nums[0] ?? "0");
  const dy = num(nums[1] ?? "0");
  const blur = nums.length > 2 ? num(nums[2] ?? "0") : 0;
  if (dx === 0 && dy === 0 && blur === 0) return null;
  const id = `f${ctx.clipSeq++}`;
  ctx.defs.push(
    `<filter id="${id}" x="-50%" y="-50%" width="200%" height="200%">` +
      `<feDropShadow dx="${r2(dx)}" dy="${r2(dy)}" stdDeviation="${r2(blur / 2)}" flood-color="${escXml(color)}"/>` +
      "</filter>"
  );
  return `url(#${id})`;
}

// ---------------------------------------------------------------- 伪元素

/** 固定尺寸装饰伪元素（content:""、有背景色/图、宽高确定）。
 *  典型：.rl-section-title::before 的琥珀竖条。伪元素拿不到 rect，
 *  按父级内容盒 + flex 对齐推断。 */
function paintFixedPseudo(ctx: Ctx, el: Element, which: "::before" | "::after", parentRect: DOMRect): void {
  let cs: CSSStyleDeclaration;
  try {
    cs = getComputedStyle(el, which);
  } catch {
    return;
  }
  const content = cs.content;
  if (!content || content === "none" || content === "normal") return;
  // 仅处理空内容装饰块（带文字的伪元素交给浏览器渲染语义，这里不还原）
  if (!/^["']\s*["']$/.test(content)) return;
  const w = num(cs.width);
  const h = num(cs.height);
  if (w <= 0 || h <= 0) return;
  if (cs.visibility === "hidden" || num(cs.opacity) === 0) return;
  // 绝对定位伪元素的位置无法从父级推断，跳过（页面上这类都是悬浮标签，导出本就不该有）
  if (cs.position === "absolute" || cs.position === "fixed") return;
  const pcs = getComputedStyle(el);
  const padL = num(pcs.paddingLeft);
  const padR = num(pcs.paddingRight);
  const padT = num(pcs.paddingTop);
  const padB = num(pcs.paddingBottom);
  const borderL = num(pcs.borderLeftWidth);
  const borderR = num(pcs.borderRightWidth);
  const borderT = num(pcs.borderTopWidth);
  const contentTop = parentRect.top + borderT + padT;
  const contentH = parentRect.height - borderT - padT - padB - num(pcs.borderBottomWidth);
  // ::before = 内容盒最左的行内/flex 项；::after = 最右
  const x =
    (which === "::before"
      ? parentRect.left + borderL + padL
      : parentRect.right - borderR - padR - w) - ctx.ox;
  const align = pcs.alignItems;
  const y =
    (align === "center" ? contentTop + (contentH - h) / 2 : contentTop) - ctx.oy;
  const radius = num(cs.borderTopLeftRadius);
  if (!isTransparent(cs.backgroundColor)) pushRect(ctx, x, y, w, h, cs.backgroundColor, radius);
  paintBackgroundLayers(ctx, cs, x, y, w, h, radius);
}

// ---------------------------------------------------------------- 遍历

function collectUrls(root: HTMLElement, out: string[]): void {
  const push = (u: string | null | undefined): void => {
    if (u && !u.startsWith("data:")) out.push(u);
  };
  const walk = (el: Element): void => {
    const cs = getComputedStyle(el);
    if (cs.display === "none") return;
    for (const layer of splitTopLevel(cs.backgroundImage === "none" ? "" : cs.backgroundImage)) {
      push(urlOf(layer));
    }
    for (const pseudo of ["::before", "::after"] as const) {
      try {
        const pcs = getComputedStyle(el, pseudo);
        if (pcs.backgroundImage && pcs.backgroundImage !== "none") {
          for (const layer of splitTopLevel(pcs.backgroundImage)) push(urlOf(layer));
        }
      } catch {
        /* ignore */
      }
    }
    if (el instanceof HTMLImageElement) push(el.currentSrc || el.src);
    for (const child of Array.from(el.children)) walk(child);
  };
  walk(root);
}

function paintElement(ctx: Ctx, el: Element): void {
  const cs = getComputedStyle(el);
  if (cs.display === "none" || cs.visibility === "hidden") return;
  const opacity = num(cs.opacity);
  if (opacity === 0) return;
  const rect = el.getBoundingClientRect();
  const x = rect.left - ctx.ox;
  const y = rect.top - ctx.oy;
  const w = rect.width;
  const h = rect.height;
  const radius = num(cs.borderTopLeftRadius);

  const grouped = opacity < 1;
  if (grouped) ctx.parts.push(`<g opacity="${r2(opacity)}">`);

  if (w > 0 && h > 0) {
    if (!isTransparent(cs.backgroundColor)) pushRect(ctx, x, y, w, h, cs.backgroundColor, radius);
    paintBackgroundLayers(ctx, cs, x, y, w, h, radius);
  }

  if (el instanceof HTMLImageElement) {
    paintImg(ctx, el, cs, rect);
    if (w > 0 && h > 0) paintBorder(ctx, cs, x, y, w, h, radius);
    if (grouped) ctx.parts.push("</g>");
    return;
  }

  paintFixedPseudo(ctx, el, "::before", rect);

  // 子树裁剪（overflow:hidden + 圆角，如 .rl-card）
  const clipped = (cs.overflow === "hidden" || cs.overflowX === "hidden" || cs.overflowY === "hidden") && w > 0 && h > 0;
  let clipId = "";
  if (clipped) {
    clipId = `c${ctx.clipSeq++}`;
    const rx = radius > 0 ? ` rx="${r2(Math.min(radius, w / 2, h / 2))}"` : "";
    ctx.defs.push(
      `<clipPath id="${clipId}"><rect x="${r2(x)}" y="${r2(y)}" width="${r2(w)}" height="${r2(h)}"${rx}/></clipPath>`
    );
    ctx.parts.push(`<g clip-path="url(#${clipId})">`);
  }

  for (const node of Array.from(el.childNodes)) {
    if (node.nodeType === Node.TEXT_NODE) {
      paintText(ctx, node as Text, cs);
    } else if (node.nodeType === Node.ELEMENT_NODE) {
      paintElement(ctx, node as Element);
    }
  }

  if (clipped) ctx.parts.push("</g>");
  paintFixedPseudo(ctx, el, "::after", rect);
  // 边框最后画：overflow:hidden 的子内容（如 .rl-product 背景图）按 CSS 只裁到
  // padding box，不应盖住 .rl-card 的 1px 描边。
  if (w > 0 && h > 0) paintBorder(ctx, cs, x, y, w, h, radius);
  if (grouped) ctx.parts.push("</g>");
}

/** filter: drop-shadow(dx dy blur color) → SVG feDropShadow（成品图标的投影）。
 *  只取第一层 drop-shadow，其余 filter 函数忽略。返回 filter 引用或 null。 */
function dropShadowFilter(ctx: Ctx, filterValue: string): string | null {
  if (!filterValue || filterValue === "none") return null;
  const m = filterValue.match(/drop-shadow\(([^()]*(?:\([^()]*\)[^()]*)*)\)/i);
  if (!m) return null;
  const inner = m[1] ?? "";
  const colorMatch = inner.match(/(rgba?\([^)]*\)|#[0-9a-f]{3,8})/i);
  const color = colorMatch ? colorMatch[0] : "rgba(0,0,0,0.5)";
  const nums = inner.replace(/rgba?\([^)]*\)/gi, "").match(/-?[\d.]+px/g) ?? [];
  if (nums.length < 2) return null;
  const dx = num(nums[0] ?? "0");
  const dy = num(nums[1] ?? "0");
  const blur = nums.length > 2 ? num(nums[2] ?? "0") : 0;
  const id = `f${ctx.clipSeq++}`;
  ctx.defs.push(
    `<filter id="${id}" x="-50%" y="-50%" width="200%" height="200%">` +
      `<feDropShadow dx="${r2(dx)}" dy="${r2(dy)}" stdDeviation="${r2(blur / 2)}" flood-color="${escXml(color)}"/>` +
      "</filter>"
  );
  return `url(#${id})`;
}

function paintImg(ctx: Ctx, el: HTMLImageElement, cs: CSSStyleDeclaration, rect: DOMRect): void {
  const src = el.currentSrc || el.src;
  const img = ctx.imgs.get(src);
  if (!img) return;
  // content box
  const bl = num(cs.borderLeftWidth) + num(cs.paddingLeft);
  const bt = num(cs.borderTopWidth) + num(cs.paddingTop);
  const br = num(cs.borderRightWidth) + num(cs.paddingRight);
  const bb = num(cs.borderBottomWidth) + num(cs.paddingBottom);
  const boxX = rect.left - ctx.ox + bl;
  const boxY = rect.top - ctx.oy + bt;
  const boxW = Math.max(0, rect.width - bl - br);
  const boxH = Math.max(0, rect.height - bt - bb);
  if (boxW <= 0 || boxH <= 0) return;
  const fitMode = cs.objectFit || "fill";
  let dw = boxW;
  let dh = boxH;
  if (fitMode === "contain" || fitMode === "cover" || fitMode === "scale-down") {
    const cover = fitMode === "cover";
    const f = fitRect(img.w, img.h, boxW, boxH, cover);
    dw = fitMode === "scale-down" ? Math.min(f.w, img.w) : f.w;
    dh = fitMode === "scale-down" ? Math.min(f.h, img.h) : f.h;
  } else if (fitMode === "none") {
    dw = img.w;
    dh = img.h;
  }
  const dx = boxX + (boxW - dw) / 2;
  const dy = boxY + (boxH - dh) / 2;
  const radius = num(cs.borderTopLeftRadius);
  let clipId = "";
  if (radius > 0 || dw > boxW || dh > boxH) {
    clipId = `c${ctx.clipSeq++}`;
    const rx = radius > 0 ? ` rx="${r2(Math.min(radius, boxW / 2, boxH / 2))}"` : "";
    ctx.defs.push(
      `<clipPath id="${clipId}"><rect x="${r2(boxX)}" y="${r2(boxY)}" width="${r2(boxW)}" height="${r2(boxH)}"${rx}/></clipPath>`
    );
    ctx.parts.push(`<g clip-path="url(#${clipId})">`);
  }
  const filter = dropShadowFilter(ctx, cs.filter);
  ctx.parts.push(
    `<image href="${img.dataUrl}" x="${r2(dx)}" y="${r2(dy)}" width="${r2(dw)}" height="${r2(dh)}" preserveAspectRatio="none"${filter ? ` filter="${filter}"` : ""}/>`
  );
  if (clipId) ctx.parts.push("</g>");
}

// ---------------------------------------------------------------- 出口

export interface ExportNodeOptions {
  /** 超采样倍率（1 = 与页面等大）。 */
  scale?: number;
  /** 画布底色（节点自身背景透明时兜底）。 */
  background?: string;
}

export interface RenderedSvg {
  svg: string;
  /** CSS px 尺寸（= 节点渲染尺寸）。 */
  width: number;
  height: number;
  /** 输出光栅尺寸（= CSS px × scale）。 */
  outWidth: number;
  outHeight: number;
  /** 节点自身背景色（光栅化时的兜底底色）。 */
  background: string;
}

/** 把一个已渲染的 DOM 节点转成等价 SVG（不落地，供导出与自检共用）。 */
export async function renderNodeToSvg(node: HTMLElement, scale = 1): Promise<RenderedSvg> {
  const urls: string[] = [];
  collectUrls(node, urls);
  const imgs = await loadImages(urls);

  const rect = node.getBoundingClientRect();
  const width = Math.max(1, Math.round(rect.width));
  const height = Math.max(1, Math.round(rect.height));
  const ctx: Ctx = { parts: [], defs: [], ox: rect.left, oy: rect.top, imgs, clipSeq: 0 };
  paintElement(ctx, node);

  const outWidth = Math.round(width * scale);
  const outHeight = Math.round(height * scale);
  const svg =
    `<svg xmlns="http://www.w3.org/2000/svg" width="${outWidth}" height="${outHeight}" viewBox="0 0 ${width} ${height}">` +
    (ctx.defs.length ? `<defs>${ctx.defs.join("")}</defs>` : "") +
    ctx.parts.join("") +
    "</svg>";
  return {
    svg,
    width,
    height,
    outWidth,
    outHeight,
    background: getComputedStyle(node).backgroundColor,
  };
}

/** 把一个已渲染的 DOM 节点导出为 PNG（无损，尺寸不设上限）。 */
export async function exportNodePng(
  node: HTMLElement,
  fileName: string,
  opts: ExportNodeOptions = {}
): Promise<void> {
  const scale = opts.scale && opts.scale > 0 ? opts.scale : 1;
  const rendered = await renderNodeToSvg(node, scale);
  const bg = opts.background || rendered.background;
  const outW = rendered.outWidth;
  const outH = rendered.outHeight;

  const url = URL.createObjectURL(new Blob([rendered.svg], { type: "image/svg+xml;charset=utf-8" }));
  try {
    const img = new Image();
    await new Promise<void>((resolve, reject) => {
      img.onload = () => resolve();
      img.onerror = () => reject(new Error("导出渲染失败"));
      img.src = url;
    });
    const canvas = document.createElement("canvas");
    canvas.width = outW;
    canvas.height = outH;
    const c2d = canvas.getContext("2d");
    if (!c2d) throw new Error("无法创建画布");
    if (bg && !isTransparent(bg)) {
      c2d.fillStyle = bg;
      c2d.fillRect(0, 0, outW, outH);
    }
    c2d.drawImage(img, 0, 0, outW, outH);
    const a = document.createElement("a");
    a.href = canvas.toDataURL("image/png");
    a.download = fileName;
    a.click();
  } finally {
    URL.revokeObjectURL(url);
  }
}

/** 离屏舞台：把已有 DOM 克隆进一个不可见容器（可附加页头等页面上没有的元素），
 *  用于「页面结构 ≠ 导出结构」的场景（如 /recipes 整页导出要补标题）。
 *  复用页面既有 CSS 类，布局与页面完全一致。 */
export function createOffscreenStage(width: number, className = ""): HTMLDivElement {
  const stage = document.createElement("div");
  if (className) stage.className = className;
  stage.style.position = "fixed";
  stage.style.left = "-100000px";
  stage.style.top = "0";
  stage.style.width = `${Math.round(width)}px`;
  stage.style.boxSizing = "border-box";
  stage.style.pointerEvents = "none";
  document.body.appendChild(stage);
  return stage;
}
