/**
 * textureEditor.ts —— 夹心模型贴图编辑器（1024×1024）。
 *
 * 三种产出方式统一成一张 1024×1024 PNG：
 *   1. 纯色填充（取色器）—— 模板 FriedFishCake.png 实测就是这种纯色贴图；
 *   2. 上传图片（自动等比缩放铺满 1024×1024）；
 *   3. 画笔涂抹（可在前两者之上继续画；支持粗细/颜色/橡皮/撤销/清空）。
 *
 * 配合 recipeModelPreview.previewLocalModel：模板 FBX 字节 + 当前 canvas 导出的
 * PNG File 直接在浏览器里渲染，改一次颜色立刻看到上色后的 3D 模型，零服务器往返。
 */
import { openModal, closeModal } from "./modals";
import { fetchTemplateMeshBuffer, previewLocalModel } from "./recipeModelPreview";

/** 与模板贴图一致的尺寸（Unity TextureImporter maxTextureSize 2048，实际用 1024）。 */
export const TEXTURE_SIZE = 1024;

/** 模板贴图的主色（#C06205），作为默认起始色。 */
const DEFAULT_COLOR = "#c06205";

export interface TextureEditorResult {
  /** PNG base64（不含 data: 前缀），可直接塞进 upload-model 的 files[]。 */
  base64: string;
  /** 同一张图的 File 形态，供本地预览注入。 */
  file: File;
  /** 主色（便于调用方记录/回显）。 */
  color: string;
}

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function canvasToBase64(canvas: HTMLCanvasElement): string {
  const url = canvas.toDataURL("image/png");
  const comma = url.indexOf(",");
  return comma >= 0 ? url.slice(comma + 1) : url;
}

function canvasToFile(canvas: HTMLCanvasElement, fileName: string): Promise<File> {
  return new Promise((resolve, reject) => {
    canvas.toBlob((blob) => {
      if (!blob) {
        reject(new Error("贴图导出失败。"));
        return;
      }
      resolve(new File([blob], fileName, { type: "image/png" }));
    }, "image/png");
  });
}

/**
 * 打开贴图编辑器。
 * @param opts.fileName  产出 PNG 的文件名（建议 <RecipeId>_Tex.png）
 * @param opts.templateId 用于实时 3D 预览的模板网格 id（默认 FriedFishCake）
 * @param opts.initialColor 初始纯色
 */
export function openTextureEditor(opts: {
  fileName: string;
  templateId?: string;
  initialColor?: string;
  onDone: (result: TextureEditorResult) => void;
}): void {
  const templateId = opts.templateId ?? "FriedFishCake";
  let color = opts.initialColor ?? DEFAULT_COLOR;

  openModal(
    "🎨 夹心贴图编辑器",
    `<p class="modal-hint">产出一张 ${TEXTURE_SIZE}×${TEXTURE_SIZE} PNG 贴图，贴到模板网格
      <code>${esc(templateId)}.fbx</code> 上。纯色即可用（官方夹心贴图本身就是纯色）；
      也可上传图片或用画笔涂改。「🔍 预览模型」直接在浏览器渲染当前贴图效果，不写盘。</p>
     <div class="tex-editor">
       <div class="tex-canvas-wrap">
         <canvas id="tex-canvas" width="${TEXTURE_SIZE}" height="${TEXTURE_SIZE}"></canvas>
       </div>
       <div class="tex-tools">
         <div class="tex-row"><span class="muted">主色</span>
           <input type="color" id="tex-color" value="${esc(color)}">
           <button type="button" class="m-btn small" id="tex-fill">填充整张</button>
         </div>
         <div class="tex-row"><span class="muted">上传图片</span>
           <input type="file" id="tex-upload" accept="image/png,image/jpeg,image/webp" class="rl-select">
         </div>
         <div class="tex-row"><span class="muted">画笔</span>
           <input type="range" id="tex-size" min="4" max="256" value="48">
           <span class="muted small" id="tex-size-val">48 px</span>
         </div>
         <div class="tex-row">
           <button type="button" class="m-btn small" id="tex-undo" title="撤销上一笔（最多 20 步）">↩ 撤销</button>
           <button type="button" class="m-btn small" id="tex-clear">清空为主色</button>
           <button type="button" class="m-btn small" id="tex-preview">🔍 预览模型</button>
         </div>
         <p class="muted small">提示：在左侧画布上按住拖动即可涂抹；橡皮 = 把画笔色设成主色再涂。</p>
       </div>
     </div>`,
    `<button type="button" class="m-btn" data-cancel>取消</button>
     <button type="button" class="m-btn primary" id="tex-ok">使用这张贴图</button>`
  );
  document.querySelector(".modal-panel")?.classList.add("wide");

  const canvas = document.getElementById("tex-canvas") as HTMLCanvasElement;
  const ctx = canvas.getContext("2d")!;
  const sizeInput = document.getElementById("tex-size") as HTMLInputElement;
  const sizeVal = document.getElementById("tex-size-val")!;
  const colorInput = document.getElementById("tex-color") as HTMLInputElement;

  // 撤销栈：只存最近 20 步（1024² RGBA ≈ 4MB/步，再多会吃满内存）
  const undoStack: ImageData[] = [];
  const UNDO_LIMIT = 20;
  const pushUndo = (): void => {
    undoStack.push(ctx.getImageData(0, 0, TEXTURE_SIZE, TEXTURE_SIZE));
    if (undoStack.length > UNDO_LIMIT) undoStack.shift();
  };

  const fillAll = (c: string): void => {
    ctx.fillStyle = c;
    ctx.fillRect(0, 0, TEXTURE_SIZE, TEXTURE_SIZE);
  };
  fillAll(color);

  colorInput.addEventListener("input", () => {
    color = colorInput.value;
  });
  document.getElementById("tex-fill")?.addEventListener("click", () => {
    pushUndo();
    fillAll(color);
  });
  document.getElementById("tex-clear")?.addEventListener("click", () => {
    pushUndo();
    fillAll(color);
  });
  document.getElementById("tex-undo")?.addEventListener("click", () => {
    const prev = undoStack.pop();
    if (prev) ctx.putImageData(prev, 0, 0);
  });
  sizeInput.addEventListener("input", () => {
    sizeVal.textContent = `${sizeInput.value} px`;
  });

  // ---- 上传图片：等比缩放后居中铺满（cover），空白处用主色兜底 ----
  document.getElementById("tex-upload")?.addEventListener("change", (e) => {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      URL.revokeObjectURL(url);
      pushUndo();
      fillAll(color);
      const scale = Math.max(TEXTURE_SIZE / img.width, TEXTURE_SIZE / img.height);
      const w = img.width * scale;
      const h = img.height * scale;
      ctx.drawImage(img, (TEXTURE_SIZE - w) / 2, (TEXTURE_SIZE - h) / 2, w, h);
    };
    img.onerror = () => {
      URL.revokeObjectURL(url);
      alert("图片解码失败，请换一张 PNG/JPG。");
    };
    img.src = url;
  });

  // ---- 画笔 ----
  let drawing = false;
  const toCanvasXY = (ev: PointerEvent): { x: number; y: number } => {
    const rect = canvas.getBoundingClientRect();
    return {
      x: ((ev.clientX - rect.left) / rect.width) * TEXTURE_SIZE,
      y: ((ev.clientY - rect.top) / rect.height) * TEXTURE_SIZE,
    };
  };
  const strokeTo = (ev: PointerEvent): void => {
    const { x, y } = toCanvasXY(ev);
    ctx.lineTo(x, y);
    ctx.stroke();
  };
  canvas.addEventListener("pointerdown", (ev) => {
    pushUndo();
    drawing = true;
    canvas.setPointerCapture(ev.pointerId);
    ctx.strokeStyle = color;
    ctx.lineWidth = Number(sizeInput.value);
    ctx.lineCap = "round";
    ctx.lineJoin = "round";
    ctx.beginPath();
    const { x, y } = toCanvasXY(ev);
    ctx.moveTo(x, y);
    ctx.lineTo(x, y);
    ctx.stroke();
  });
  canvas.addEventListener("pointermove", (ev) => {
    if (drawing) strokeTo(ev);
  });
  canvas.addEventListener("pointerup", (ev) => {
    drawing = false;
    canvas.releasePointerCapture(ev.pointerId);
  });

  // ---- 实时 3D 预览（模板 FBX 字节 + 当前贴图 File，不落盘）----
  document.getElementById("tex-preview")?.addEventListener("click", () => {
    void (async () => {
      const btn = document.getElementById("tex-preview") as HTMLButtonElement | null;
      if (btn) {
        btn.disabled = true;
        btn.textContent = "加载中…";
      }
      try {
        const buffer = await fetchTemplateMeshBuffer(templateId);
        const file = await canvasToFile(canvas, opts.fileName);
        // 预览弹窗会替换当前弹窗内容，关闭后需要用户重新打开编辑器 —— 先把贴图状态
        // 交回调用方缓存，避免用户以为白画了。
        await previewLocalModel({
          title: `夹心模型预览 · ${templateId}`,
          buffer,
          fileName: `${templateId}.fbx`,
          textures: [file],
        });
      } catch (err) {
        alert((err as Error).message || "预览失败。");
      } finally {
        if (btn) {
          btn.disabled = false;
          btn.textContent = "🔍 预览模型";
        }
      }
    })();
  });

  document.querySelector("[data-cancel]")?.addEventListener("click", () => closeModal());
  document.getElementById("tex-ok")?.addEventListener("click", () => {
    void (async () => {
      try {
        const file = await canvasToFile(canvas, opts.fileName);
        opts.onDone({ base64: canvasToBase64(canvas), file, color });
        closeModal();
      } catch (err) {
        alert((err as Error).message || "贴图导出失败。");
      }
    })();
  });
}
