/**
 * 关卡截图上传弹窗（关卡编辑器「📷 截图」按钮）。
 *
 * 功能：
 *  - 查看已上传的截图（imageFloorUrl 预览）。
 *  - 选择本地图片 → 画布上自由矩形拖拽裁剪选区（可重选）。
 *  - 画质压缩滑杆（JPEG quality 50–100%）。
 *  - 「裁剪并上传」→ 按源图坐标裁切 → JPEG base64 → /api/level/screenshot-upload
 *    （后端写入关卡 data 目录并赋给 LevelInfoSO.screenshot）。
 */
import type { LevelDetail } from "../../types";
import { openModal, closeModal } from "../../modals";
import { showBusy, hideBusy } from "../../busy";
import { setStatus } from "../status";
import { uploadScreenshot, imageFloorUrl } from "../../api";

/** 裁剪预览画布固定尺寸（不随源图大小变化，避免弹窗缩放/出现滚动条）。 */
const CANVAS_W = 640;
const CANVAS_H = 400;

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

export function openScreenshotModal(detail: LevelDetail, onSaved?: () => void): void {
  let img: HTMLImageElement | null = null;
  let fit: FitRect = { scale: 1, dx: 0, dy: 0, dw: 0, dh: 0 };
  // 裁剪选区（画布显示坐标）
  let crop: CropRect | null = null;
  // 拖拽状态
  let dragStart: { x: number; y: number } | null = null;
  let moving = false;
  let moveOffset: { x: number; y: number } | null = null;

  const currentShot = detail.screenshotPath
    ? `<div class="ss-current">
        <div class="ss-label">已上传截图</div>
        <img src="${imageFloorUrl(detail.screenshotPath)}" alt="关卡截图" class="ss-current-img">
      </div>`
    : '<div class="ss-current ss-empty"><div class="ss-label">尚未上传截图</div></div>';

  const bodyHtml = `
    ${currentShot}
    <div class="ss-upload-row">
      <input type="file" id="ss-file" accept="image/png,image/jpeg" style="display:none">
      <button type="button" class="modal-btn" id="ss-choose">选择图片</button>
      <span class="muted ss-file-name" id="ss-file-name"></span>
    </div>
    <div id="ss-crop-wrap" class="ss-crop-wrap" style="display:none">
      <canvas id="ss-canvas" class="ss-canvas"></canvas>
      <div class="muted ss-hint">在图片上拖拽画一个矩形选区，可再次拖拽调整位置与大小</div>
    </div>
    <div id="ss-quality-row" class="ss-quality-row" style="display:none">
      <label class="modal-check">画质压缩（JPEG）
        <input type="range" id="ss-quality" min="50" max="100" step="1" value="85">
        <span class="muted" id="ss-quality-val">85%</span>
      </label>
    </div>
    <div class="modal-hint err" id="ss-err" style="display:none"></div>
  `;

  const footerHtml = `
    <button type="button" class="modal-btn" data-cancel>取消</button>
    <button type="button" class="modal-btn primary" id="ss-upload" disabled>裁剪并上传</button>
  `;

  openModal("关卡截图 · " + (detail.levelNameZH || detail.levelName || "未命名"), bodyHtml, footerHtml);
  document.querySelector(".modal-panel")?.classList.add("wide");

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

  qualityInput?.addEventListener("input", () => {
    if (qualityVal) qualityVal.textContent = qualityInput.value + "%";
  });

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
    }
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
    crop.x = Math.max(minX, Math.min(crop.x, maxX - 4));
    crop.y = Math.max(minY, Math.min(crop.y, maxY - 4));
    crop.w = Math.max(8, Math.min(crop.w, maxX - crop.x));
    crop.h = Math.max(8, Math.min(crop.h, maxY - crop.y));
  };

  const setUploadEnabled = () => {
    if (uploadBtn) uploadBtn.disabled = !img || !crop;
  };

  const canvasEl = () => document.getElementById("ss-canvas") as HTMLCanvasElement | null;

  const wireCanvas = () => {
    const canvas = canvasEl();
    if (!canvas) return;
    canvas.addEventListener("mousedown", (e) => {
      const p = toCanvasPos(e);
      // 点击已存在选区内部 → 进入「移动选区」模式
      if (crop && p.x >= crop.x && p.x <= crop.x + crop.w && p.y >= crop.y && p.y <= crop.y + crop.h) {
        moving = true;
        moveOffset = { x: p.x - crop.x, y: p.y - crop.y };
        return;
      }
      dragStart = p;
      crop = { x: p.x, y: p.y, w: 0, h: 0 };
      setUploadEnabled();
    });
    canvas.addEventListener("mousemove", (e) => {
      if (dragStart) {
        const p = toCanvasPos(e);
        crop = {
          x: Math.min(dragStart.x, p.x),
          y: Math.min(dragStart.y, p.y),
          w: Math.abs(p.x - dragStart.x),
          h: Math.abs(p.y - dragStart.y),
        };
        drawCanvas();
      } else if (moving && crop && moveOffset) {
        const p = toCanvasPos(e);
        crop.x = p.x - moveOffset.x;
        crop.y = p.y - moveOffset.y;
        clampCrop();
        drawCanvas();
      }
    });
    const end = () => {
      if (dragStart || moving) {
        dragStart = null;
        moving = false;
        moveOffset = null;
        if (crop) clampCrop();
        setUploadEnabled();
        drawCanvas();
      }
    };
    canvas.addEventListener("mouseup", end);
    canvas.addEventListener("mouseleave", end);
  };

  document.getElementById("ss-choose")?.addEventListener("click", () => fileInput?.click());

  fileInput?.addEventListener("change", () => {
    const file = fileInput.files?.[0];
    if (!file) return;
    if (fileNameEl) fileNameEl.textContent = file.name;
    const reader = new FileReader();
    reader.onload = () => {
      const url = reader.result as string;
      const image = new Image();
      image.onload = () => {
        img = image;
        // contain 适配到固定画布：缩放 + 居中偏移，裁剪坐标按此换算回源图
        const s = Math.min(CANVAS_W / image.naturalWidth, CANVAS_H / image.naturalHeight);
        const dw = image.naturalWidth * s;
        const dh = image.naturalHeight * s;
        fit = { scale: s, dx: (CANVAS_W - dw) / 2, dy: (CANVAS_H - dh) / 2, dw, dh };
        crop = null;
        const wrap = document.getElementById("ss-crop-wrap");
        if (wrap) wrap.style.display = "";
        const qr = document.getElementById("ss-quality-row");
        if (qr) qr.style.display = "";
        err("");
        setUploadEnabled();
        drawCanvas();
        wireCanvas();
      };
      image.onerror = () => err("图片加载失败，请换一张重试");
      image.src = url;
    };
    reader.onerror = () => err("读取文件失败");
    reader.readAsDataURL(file);
  });

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);

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
      closeModal();
      setStatus("关卡截图已上传");
      onSaved?.();
      // 重开弹窗展示新截图
      openScreenshotModal(detail, onSaved);
    } catch (e) {
      err((e as Error).message);
    } finally {
      hideBusy();
    }
  });
}
