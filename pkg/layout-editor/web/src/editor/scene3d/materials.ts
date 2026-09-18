/**
 * 共享材质 / 纹理工厂与缓存。
 *
 * 性能前提：一个关卡可能有上千件装饰，若每件都新建 canvas 纹理会瞬间打爆显存。
 * 文字纹理按文本内容缓存（同名物件共用一张），图标纹理按 HTMLImageElement 缓存
 * （复用 iconCaches 已有的图片池，加载完成回调经 setRedraw → draw() → 3D 标脏）。
 */

import * as THREE from "three";

const labelCache = new Map<string, THREE.CanvasTexture>();
const iconCache = new WeakMap<HTMLImageElement, THREE.Texture>();

/** rgba()/rgb()/#hex → { color, alpha }，用于把 2D 的配色照搬到 3D 材质。
 *
 *  必须用 setRGB(..., SRGBColorSpace)：2D canvas 的颜色字符串是 sRGB，而 three
 *  的工作色彩空间是线性 sRGB。直接 new THREE.Color(r/255, g/255, b/255) 会把
 *  sRGB 值当成线性值塞进去，渲染出来整体发白，与 2D 对不上。 */
export function parseCssColor(css: string): { color: THREE.Color; alpha: number } {
  const m = /^rgba?\(([^)]+)\)$/i.exec(css.trim());
  if (m) {
    const parts = m[1].split(",").map((s) => parseFloat(s.trim()));
    const c = new THREE.Color().setRGB(
      Math.max(0, Math.min(1, (parts[0] || 0) / 255)),
      Math.max(0, Math.min(1, (parts[1] || 0) / 255)),
      Math.max(0, Math.min(1, (parts[2] || 0) / 255)),
      THREE.SRGBColorSpace
    );
    const a = parts.length > 3 && Number.isFinite(parts[3]) ? parts[3] : 1;
    return { color: c, alpha: Math.max(0, Math.min(1, a)) };
  }
  try {
    // setStyle 自带 sRGB → 工作空间转换。
    return { color: new THREE.Color().setStyle(css, THREE.SRGBColorSpace), alpha: 1 };
  } catch {
    return { color: new THREE.Color().setStyle("#8a8f98", THREE.SRGBColorSpace), alpha: 1 };
  }
}

/** 文字 billboard 纹理（白字描边，深底上也读得清）。 */
export function labelTexture(text: string): THREE.CanvasTexture | null {
  const key = text || "";
  if (!key) return null;
  const hit = labelCache.get(key);
  if (hit) return hit;

  const font = "bold 44px system-ui, -apple-system, 'PingFang SC', sans-serif";
  const probe = document.createElement("canvas").getContext("2d");
  if (!probe) return null;
  probe.font = font;
  const textW = Math.min(620, Math.ceil(probe.measureText(key).width) + 24);

  const canvas = document.createElement("canvas");
  canvas.width = Math.max(64, textW);
  canvas.height = 64;
  const ctx = canvas.getContext("2d");
  if (!ctx) return null;
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.font = font;
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.lineWidth = 6;
  ctx.strokeStyle = "#11141a";
  ctx.strokeText(key, canvas.width / 2, canvas.height / 2, canvas.width - 8);
  ctx.fillStyle = "#ffffff";
  ctx.fillText(key, canvas.width / 2, canvas.height / 2, canvas.width - 8);

  const tex = new THREE.CanvasTexture(canvas);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.minFilter = THREE.LinearFilter;
  tex.generateMipmaps = false;
  // 共享标记：disposeObject 见到它就不释放（多个物件共用同一张）。
  tex.userData.shared = true;
  labelCache.set(key, tex);
  return tex;
}

/** 图标纹理：包装 iconCaches 已加载完成的 HTMLImageElement。 */
export function iconTexture(img: HTMLImageElement | null): THREE.Texture | null {
  if (!img || !img.complete || img.naturalWidth === 0) return null;
  const hit = iconCache.get(img);
  if (hit) return hit;
  const tex = new THREE.Texture(img);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.minFilter = THREE.LinearFilter;
  tex.generateMipmaps = false;
  tex.needsUpdate = true;
  tex.userData.shared = true;
  iconCache.set(img, tex);
  return tex;
}

/** 半透明贴花材质（走道 / 坠落区 / void / 接缝等叠加层，R6）。 */
export function decalMaterial(color: THREE.ColorRepresentation, opacity: number, order: number): THREE.MeshBasicMaterial {
  const m = new THREE.MeshBasicMaterial({
    color,
    transparent: true,
    opacity,
    side: THREE.DoubleSide,
    depthWrite: false,
    polygonOffset: true,
    polygonOffsetFactor: -2,
    polygonOffsetUnits: -(order + 2),
  });
  return m;
}

/** 释放文字纹理池（卸载 3D 视图时调用）。 */
export function disposeSharedTextures(): void {
  for (const tex of labelCache.values()) tex.dispose();
  labelCache.clear();
}
