/**
 * 关卡截图 pane（关卡配置弹窗「📷 截图」tab）。
 *
 * 功能：
 *  - 查看已上传的截图（imageFloorUrl 预览）。
 *  - 选择本地图片或读取 Unity 游戏相机画面 → 自动生成裁剪选区。
 *  - 支持自由比例、524:308、2:3、9:16 等比例，选区可移动并通过边框/四角调整。
 *  - 画质压缩滑杆（JPEG quality 50–100%）。
 *  - 「裁剪并上传」→ 按源图坐标裁切 → JPEG base64 → /api/level/screenshot-upload
 *    （后端写入关卡 data 目录并赋给 LevelInfoSO.screenshot），上传成功后就地刷新预览。
 */
import type { LevelDetail } from "../../types";
import { showBusy, hideBusy } from "../../busy";
import { setStatus } from "../status";
import { captureUnityScreenshot, uploadScreenshot, imageFloorUrl } from "../../api";
import { modalBtnHtml, mBtnHtml } from "../../ui/views/button";

/** 裁剪预览画布固定尺寸（不随源图大小变化，避免弹窗缩放/出现滚动条）。 */
const CANVAS_W = 640;
const CANVAS_H = 400;

const ASPECT_PRESETS = [
  { value: "524:308", label: "524:308（推荐）", ratio: 524 / 308 },
  { value: "16:9", label: "16:9", ratio: 16 / 9 },
  { value: "3:2", label: "3:2", ratio: 3 / 2 },
  { value: "1:1", label: "1:1", ratio: 1 },
  { value: "2:3", label: "2:3", ratio: 2 / 3 },
  { value: "9:16", label: "9:16", ratio: 9 / 16 },
  { value: "free", label: "自由比例", ratio: null },
] as const;

interface CropRect {
  x: number;
  y: number;
  w: number;
  h: number;
}

/** 源图在固定画布中的「contain 适配」信息：scale = 显示缩放，dx/dy = 绘制偏移。 */
interface FitRect {
  scale: number;
  dx: number;
  dy: number;
  dw: number;
  dh: number;
}

type CropHandle = "move" | "n" | "s" | "e" | "w" | "nw" | "ne" | "sw" | "se";

/** 截图 tab 的 pane HTML（由关卡配置弹窗嵌入；配对调用 wireScreenshotPane）。 */
export function screenshotPaneHtml(detail: LevelDetail): string {
  const currentShot = detail.screenshotPath
    ? `<div class="ss-current" id="ss-current-wrap">
        <div class="ss-label">已上传截图</div>
        <img src="${imageFloorUrl(detail.screenshotPath)}" alt="关卡截图" class="ss-current-img">
      </div>`
    : '<div class="ss-current ss-empty" id="ss-current-wrap"><div class="ss-label">尚未上传截图</div></div>';

  return `
    ${currentShot}
    <div class="ss-source-card">
      <div class="ss-section-title">截图来源</div>
      <input type="file" id="ss-file" accept="image/png,image/jpeg" style="display:none">
      <div class="ss-upload-row">
        ${modalBtnHtml("上传本地图片", "modal-btn primary", { id: "ss-choose" })}
        ${modalBtnHtml("读取 Unity 游戏画面", "modal-btn", { id: "ss-capture" })}
      </div>
      <span class="muted ss-file-name" id="ss-file-name"></span>
    </div>
    <div id="ss-crop-wrap" class="ss-crop-wrap" style="display:none">
      <div class="ss-section-title">裁剪与构图</div>
      <div class="ss-crop-toolbar">
        <label class="ss-aspect-label">输出比例
          <select id="ss-aspect">
            ${ASPECT_PRESETS.map((p) => `<option value="${p.value}"${p.value === "524:308" ? " selected" : ""}>${p.label}</option>`).join("")}
          </select>
        </label>
        ${mBtnHtml("重新选择区域", "default", { id: "ss-reset-crop" })}
        <span class="muted ss-output-size" id="ss-output-size"></span>
      </div>
      <canvas id="ss-canvas" class="ss-canvas"></canvas>
      <div class="muted ss-hint">拖动框内移动选区；拖动边框或四角调整大小。切换比例会自动保持选区在图片范围内。</div>
    </div>
    <div id="ss-quality-row" class="ss-quality-row" style="display:none">
      <label class="modal-check">画质压缩（JPEG）
        <input type="range" id="ss-quality" min="50" max="100" step="1" value="85">
        <span class="muted" id="ss-quality-val">85%</span>
      </label>
    </div>
    <div class="ss-actions-row">
      ${mBtnHtml("裁剪并上传", "primary", { id: "ss-upload", disabled: "" })}
    </div>
    <div class="modal-hint err" id="ss-err" style="display:none"></div>
  `;
}

/** 挂接截图 pane 的全部交互（渲染 screenshotPaneHtml 之后调用一次）。 */
export function wireScreenshotPane(detail: LevelDetail): void {
  let img: HTMLImageElement | null = null;
  let fit: FitRect = { scale: 1, dx: 0, dy: 0, dw: 0, dh: 0 };
  // 裁剪选区（画布显示坐标）
  let crop: CropRect | null = null;
  let aspect: number | null = 524 / 308;
  let pointerAction: { handle: CropHandle; start: { x: number; y: number }; crop: CropRect } | null = null;
  let sourceLabel = "";

  const err = (msg: string) => {
    const el = document.getElementById("ss-err");
    if (el) {
      el.textContent = msg;
      el.style.display = msg ? "" : "none";
    }
  };

  const fileInput = document.getElementById("ss-file") as HTMLInputElement | null;
  const fileNameEl = document.getElementById("ss-file-name");
  const uploadBtn = document.getElementById("ss-upload") as HTMLButtonElement | null;
  const qualityInput = document.getElementById("ss-quality") as HTMLInputElement | null;
  const qualityVal = document.getElementById("ss-quality-val");
  const aspectInput = document.getElementById("ss-aspect") as HTMLSelectElement | null;
  const outputSize = document.getElementById("ss-output-size");
  const captureBtn = document.getElementById("ss-capture") as HTMLButtonElement | null;

  qualityInput?.addEventListener("input", () => {
    if (qualityVal) qualityVal.textContent = qualityInput.value + "%";
  });

  const getHandlePoints = (r: CropRect) => [
    { x: r.x, y: r.y, handle: "nw" as CropHandle },
    { x: r.x + r.w / 2, y: r.y, handle: "n" as CropHandle },
    { x: r.x + r.w, y: r.y, handle: "ne" as CropHandle },
    { x: r.x + r.w, y: r.y + r.h / 2, handle: "e" as CropHandle },
    { x: r.x + r.w, y: r.y + r.h, handle: "se" as CropHandle },
    { x: r.x + r.w / 2, y: r.y + r.h, handle: "s" as CropHandle },
    { x: r.x, y: r.y + r.h, handle: "sw" as CropHandle },
    { x: r.x, y: r.y + r.h / 2, handle: "w" as CropHandle },
  ];

  const updateOutputSize = () => {
    if (!outputSize || !img || !crop) return;
    const w = Math.max(1, Math.round(crop.w / fit.scale));
    const h = Math.max(1, Math.round(crop.h / fit.scale));
    outputSize.textContent = `输出约 ${w} × ${h}px${sourceLabel ? ` · ${sourceLabel}` : ""}`;
  };

  const drawCanvas = () => {
    const canvas = document.getElementById("ss-canvas") as HTMLCanvasElement | null;
    if (!canvas || !img) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    // 画布始终固定尺寸；源图按 contain 适配居中绘制
    canvas.width = CANVAS_W;
    canvas.height = CANVAS_H;
    ctx.fillStyle = "#1a1d23";
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(img, fit.dx, fit.dy, fit.dw, fit.dh);

    if (crop) {
      ctx.fillStyle = "rgba(0,0,0,0.55)";
      // 四周遮罩
      ctx.fillRect(0, 0, canvas.width, crop.y);
      ctx.fillRect(0, crop.y + crop.h, canvas.width, canvas.height - crop.y - crop.h);
      ctx.fillRect(0, crop.y, crop.x, crop.h);
      ctx.fillRect(crop.x + crop.w, crop.y, canvas.width - crop.x - crop.w, crop.h);
      ctx.strokeStyle = "#4fd8eb";
      ctx.lineWidth = 2;
      ctx.strokeRect(crop.x, crop.y, crop.w, crop.h);
      ctx.strokeStyle = "rgba(255,255,255,0.5)";
      ctx.lineWidth = 1;
      // 三等分参考线
      for (let i = 1; i < 3; i++) {
        ctx.beginPath();
        ctx.moveTo(crop.x + (crop.w * i) / 3, crop.y);
        ctx.lineTo(crop.x + (crop.w * i) / 3, crop.y + crop.h);
        ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(crop.x, crop.y + (crop.h * i) / 3);
        ctx.lineTo(crop.x + crop.w, crop.y + (crop.h * i) / 3);
        ctx.stroke();
      }
      const handles = getHandlePoints(crop);
      ctx.fillStyle = "#ffffff";
      ctx.strokeStyle = "#12313a";
      ctx.lineWidth = 1;
      for (const p of handles) {
        ctx.beginPath();
        ctx.rect(p.x - 5, p.y - 5, 10, 10);
        ctx.fill();
        ctx.stroke();
      }
    }
    updateOutputSize();
  };

  const toCanvasPos = (e: MouseEvent): { x: number; y: number } => {
    const canvas = document.getElementById("ss-canvas") as HTMLCanvasElement | null;
    if (!canvas) return { x: 0, y: 0 };
    const r = canvas.getBoundingClientRect();
    // 画布内部固定 640×400；按 CSS 显示尺寸换算（防被面板缩放时坐标偏移）
    const sx = canvas.width > 0 ? r.width / canvas.width : 1;
    const sy = canvas.height > 0 ? r.height / canvas.height : 1;
    return {
      x: (e.clientX - r.left) / sx,
      y: (e.clientY - r.top) / sy,
    };
  };

  const clampCrop = () => {
    if (!crop || !img) return;
    // 仅允许在图片绘制区域内选择（避免裁到 contain 适配的黑边）
    const minX = fit.dx;
    const minY = fit.dy;
    const maxX = fit.dx + fit.dw;
    const maxY = fit.dy + fit.dh;
    crop.w = Math.max(8, Math.min(crop.w, maxX - minX));
    crop.h = Math.max(8, Math.min(crop.h, maxY - minY));
    crop.x = Math.max(minX, Math.min(crop.x, maxX - crop.w));
    crop.y = Math.max(minY, Math.min(crop.y, maxY - crop.h));
  };

  const getPresetRatio = (value: string): number | null =>
    ASPECT_PRESETS.find((p) => p.value === value)?.ratio ?? null;

  const imageBounds = (): CropRect => ({ x: fit.dx, y: fit.dy, w: fit.dw, h: fit.dh });

  const createCrop = (): CropRect | null => {
    if (!img || fit.dw <= 0 || fit.dh <= 0) return null;
    const bounds = imageBounds();
    if (!aspect) return bounds;
    let w = bounds.w;
    let h = w / aspect;
    if (h > bounds.h) {
      h = bounds.h;
      w = h * aspect;
    }
    return { x: bounds.x + (bounds.w - w) / 2, y: bounds.y + (bounds.h - h) / 2, w, h };
  };

  const resizeToAspect = (nextAspect: number | null) => {
    aspect = nextAspect;
    if (!crop) {
      crop = createCrop();
    } else if (aspect) {
      const cx = crop.x + crop.w / 2;
      const cy = crop.y + crop.h / 2;
      let w = crop.w;
      let h = w / aspect;
      const bounds = imageBounds();
      if (h > bounds.h) {
        h = bounds.h;
        w = h * aspect;
      }
      if (w > bounds.w) {
        w = bounds.w;
        h = w / aspect;
      }
      crop = { x: cx - w / 2, y: cy - h / 2, w, h };
    }
    clampCrop();
    drawCanvas();
    setUploadEnabled();
  };

  const getHandleAt = (p: { x: number; y: number }): CropHandle | null => {
    if (!crop) return null;
    for (const h of getHandlePoints(crop)) {
      if (Math.abs(p.x - h.x) <= 12 && Math.abs(p.y - h.y) <= 12) return h.handle;
    }
    if (p.x >= crop.x && p.x <= crop.x + crop.w && p.y >= crop.y && p.y <= crop.y + crop.h) return "move";
    return null;
  };

  const cursorForHandle = (handle: CropHandle | null): string => {
    if (handle === "move") return "move";
    if (handle === "n" || handle === "s") return "ns-resize";
    if (handle === "e" || handle === "w") return "ew-resize";
    if (handle === "nw" || handle === "se") return "nwse-resize";
    if (handle === "ne" || handle === "sw") return "nesw-resize";
    return "crosshair";
  };

  const setUploadEnabled = () => {
    if (uploadBtn) uploadBtn.disabled = !img || !crop;
  };

  const canvasEl = () => document.getElementById("ss-canvas") as HTMLCanvasElement | null;

  const resizeCrop = (handle: CropHandle, p: { x: number; y: number }, start: { x: number; y: number }, original: CropRect) => {
    if (!crop || !img) return;
    const bounds = imageBounds();
    if (handle === "move") {
      crop.x = original.x + p.x - start.x;
      crop.y = original.y + p.y - start.y;
      clampCrop();
      return;
    }
    const right = original.x + original.w;
    const bottom = original.y + original.h;
    let left = original.x;
    let top = original.y;
    let nextRight = right;
    let nextBottom = bottom;
    if (handle.indexOf("w") >= 0) left = Math.min(p.x, right - 8);
    if (handle.indexOf("e") >= 0) nextRight = Math.max(p.x, left + 8);
    if (handle.indexOf("n") >= 0) top = Math.min(p.y, bottom - 8);
    if (handle.indexOf("s") >= 0) nextBottom = Math.max(p.y, top + 8);
    if (aspect) {
      const isHorizontal = handle === "e" || handle === "w";
      const isVertical = handle === "n" || handle === "s";
      if (isHorizontal) {
        const w = Math.abs(nextRight - left);
        const h = w / aspect;
        top = original.y + (original.h - h) / 2;
        nextBottom = top + h;
      } else if (isVertical) {
        const h = Math.abs(nextBottom - top);
        const w = h * aspect;
        left = original.x + (original.w - w) / 2;
        nextRight = left + w;
      } else {
        const anchorX = handle.indexOf("w") >= 0 ? right : original.x;
        const anchorY = handle.indexOf("n") >= 0 ? bottom : original.y;
        let w = Math.abs(p.x - anchorX);
        let h = Math.abs(p.y - anchorY);
        if (w / Math.max(h, 1) > aspect) h = w / aspect;
        else w = h * aspect;
        left = handle.indexOf("w") >= 0 ? anchorX - w : anchorX;
        nextRight = handle.indexOf("w") >= 0 ? anchorX : anchorX + w;
        top = handle.indexOf("n") >= 0 ? anchorY - h : anchorY;
        nextBottom = handle.indexOf("n") >= 0 ? anchorY : anchorY + h;
      }
    }
    crop = { x: left, y: top, w: Math.max(8, nextRight - left), h: Math.max(8, nextBottom - top) };
    if (crop.w > bounds.w || crop.h > bounds.h) {
      const scale = Math.min(bounds.w / crop.w, bounds.h / crop.h);
      crop.w *= scale;
      crop.h *= scale;
    }
    clampCrop();
  };

  const wireCanvas = () => {
    const canvas = canvasEl();
    if (!canvas) return;
    canvas.addEventListener("pointerdown", (e) => {
      const p = toCanvasPos(e);
      const handle = getHandleAt(p);
      if (handle) {
        e.preventDefault();
        canvas.setPointerCapture(e.pointerId);
        pointerAction = { handle, start: p, crop: crop ? { ...crop } : { x: p.x, y: p.y, w: 8, h: 8 } };
      } else {
        crop = createCrop();
        drawCanvas();
      }
    });
    canvas.addEventListener("pointermove", (e) => {
      const p = toCanvasPos(e);
      if (pointerAction) {
        resizeCrop(pointerAction.handle, p, pointerAction.start, pointerAction.crop);
        drawCanvas();
      } else {
        canvas.style.cursor = cursorForHandle(getHandleAt(p));
      }
    });
    const end = (e: PointerEvent) => {
      if (!pointerAction) return;
      pointerAction = null;
      if (canvas.hasPointerCapture(e.pointerId)) canvas.releasePointerCapture(e.pointerId);
      clampCrop();
      setUploadEnabled();
      drawCanvas();
    };
    canvas.addEventListener("pointerup", end);
    canvas.addEventListener("pointercancel", end);
  };

  document.getElementById("ss-choose")?.addEventListener("click", () => fileInput?.click());

  aspectInput?.addEventListener("change", () => resizeToAspect(getPresetRatio(aspectInput.value)));
  document.getElementById("ss-reset-crop")?.addEventListener("click", () => {
    crop = createCrop();
    drawCanvas();
    setUploadEnabled();
  });

  const loadImage = (image: HTMLImageElement, label: string) => {
    img = image;
    sourceLabel = label;
    const s = Math.min(CANVAS_W / image.naturalWidth, CANVAS_H / image.naturalHeight);
    const dw = image.naturalWidth * s;
    const dh = image.naturalHeight * s;
    fit = { scale: s, dx: (CANVAS_W - dw) / 2, dy: (CANVAS_H - dh) / 2, dw, dh };
    crop = createCrop();
    const wrap = document.getElementById("ss-crop-wrap");
    if (wrap) wrap.style.display = "";
    const qr = document.getElementById("ss-quality-row");
    if (qr) qr.style.display = "";
    err("");
    setUploadEnabled();
    drawCanvas();
  };

  captureBtn?.addEventListener("click", async () => {
    if (!detail.levelInfoAssetPath) {
      err("缺少关卡 LevelInfoSO 路径，无法读取 Unity 画面");
      return;
    }
    const ratio = aspect || 524 / 308;
    const width = 1048;
    const height = Math.max(1, Math.round(width / ratio));
    captureBtn.disabled = true;
    showBusy("读取 Unity 游戏画面…");
    try {
      const captured = await captureUnityScreenshot(detail.levelInfoAssetPath, width, height, 90);
      if (!captured.base64) throw new Error("Unity 未返回画面");
      const image = new Image();
      image.onload = () => loadImage(image, "Unity 游戏相机");
      image.onerror = () => err("Unity 画面加载失败");
      image.src = `data:image/jpeg;base64,${captured.base64}`;
      if (fileNameEl) fileNameEl.textContent = "Unity 游戏相机画面";
    } catch (e) {
      err((e as Error).message);
    } finally {
      captureBtn.disabled = false;
      hideBusy();
    }
  });

  fileInput?.addEventListener("change", () => {
    const file = fileInput.files?.[0];
    if (!file) return;
    if (fileNameEl) fileNameEl.textContent = file.name;
    const reader = new FileReader();
    reader.onload = () => {
      const url = reader.result as string;
      const image = new Image();
      image.onload = () => {
        loadImage(image, file.name);
      };
      image.onerror = () => err("图片加载失败，请换一张重试");
      image.src = url;
    };
    reader.onerror = () => err("读取文件失败");
    reader.readAsDataURL(file);
  });

  wireCanvas();

  uploadBtn?.addEventListener("click", async () => {
    if (!img || !crop) return;
    if (!detail.levelInfoAssetPath) {
      err("缺少关卡 LevelInfoSO 路径，无法上传");
      return;
    }
    const canvas = canvasEl();
    if (!canvas) return;
    // 选区（画布坐标）→ 源图坐标（减去居中偏移、除以显示缩放）
    const sx = Math.max(0, Math.round((crop.x - fit.dx) / fit.scale));
    const sy = Math.max(0, Math.round((crop.y - fit.dy) / fit.scale));
    const sw = Math.min(img.naturalWidth - sx, Math.round(crop.w / fit.scale));
    const sh = Math.min(img.naturalHeight - sy, Math.round(crop.h / fit.scale));
    const out = document.createElement("canvas");
    out.width = Math.max(1, sw);
    out.height = Math.max(1, sh);
    const ctx = out.getContext("2d");
    if (!ctx) {
      err("无法创建裁剪画布");
      return;
    }
    ctx.drawImage(img, sx, sy, out.width, out.height, 0, 0, out.width, out.height);
    const quality = (qualityInput ? parseInt(qualityInput.value, 10) : 85) / 100;
    const dataUrl = out.toDataURL("image/jpeg", quality);
    const base64 = dataUrl.split(",")[1] ?? "";
    showBusy("上传截图…");
    try {
      const texturePath = await uploadScreenshot(detail.levelInfoAssetPath, "screenshot.jpg", base64);
      if (!texturePath) {
        err("上传失败（请确认 Bridge 已连接）");
        return;
      }
      detail.hasScreenshot = true;
      detail.screenshotPath = texturePath;
      // 就地刷新预览（不重开弹窗）
      const wrap = document.getElementById("ss-current-wrap");
      if (wrap) {
        wrap.classList.remove("ss-empty");
        wrap.innerHTML = `
          <div class="ss-label">已上传截图</div>
          <img src="${imageFloorUrl(texturePath)}" alt="关卡截图" class="ss-current-img">`;
      }
      setStatus("关卡截图已上传");
    } catch (e) {
      err((e as Error).message);
    } finally {
      hideBusy();
    }
  });
}
