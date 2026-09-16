/**
 * recipeModelPreview.ts —— 自定义菜谱 3D 模型预览的共享实现。
 *
 * 原先这两个函数是 customRecipes.ts 的模块私有函数，汉堡工作台拿不到。
 * 抽出来供三处复用：
 *   - 菜谱管理列表/表单（customRecipes.ts）
 *   - 汉堡工作台的候选卡片与堆叠层（burgerMaker.ts）
 *   - 贴图编辑器的实时预览（走 previewLocalModel，不落盘）
 */
import * as api from "./api";
import type { ModelTransformValues } from "./modelPreview";

/** 菜谱 models 目录的 3D 资源访问基地址（目录式，FBX 贴图按相对路径拼接）。
 *  目录结构：custom_recipes/<分类>/models/<菜谱id>/（每个菜谱一个文件夹）。 */
export function modelResourceBase(recipeAssetPath: string): string {
  const id = recipeAssetPath.split("/").pop()?.replace(/\.asset$/, "") ?? "model";
  const dir = recipeAssetPath.replace(/\/[^/]+\.asset$/, "") + "/models/" + encodeURIComponent(id);
  return dirResourceBase(dir);
}

/** 任意 Assets 目录 → 文件服务基地址（后端按 base64url 解码目录名）。 */
export function dirResourceBase(assetDir: string): string {
  const b64 = btoa(unescape(encodeURIComponent(assetDir)))
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/, "");
  return `/api/custom-recipes/model-files/${b64}/`;
}

export interface RecipePreviewExtra extends Partial<ModelTransformValues> {
  onAdjust?: (t: ModelTransformValues) => void;
  unitySize?: { x: number; y: number; z: number; minY: number };
  fitTarget?: "plate" | "cup";
  /** 预览加载失败时的提示方式（默认 alert）。 */
  onError?: (msg: string) => void;
  /** 强制只读（缺省：commonW2 共享库自动只读）。 */
  readonly?: boolean;
}

/** 共享只读素材库：commonW2 的模型变换不归任何单个关卡集管理，预览一律只读。
 *  放在这里做**单一收口** —— 任何调用方都不会忘记传 readonly。 */
export function isSharedReadonlyAsset(assetPath: string): boolean {
  return assetPath.replace(/\\/g, "/").includes("/commonW2/");
}

/** 打开菜谱 3D 模型在线预览（自动从 models 目录找 .fbx/.obj；three.js 按需加载）。
 *  预览显示参考容器标的物（盘子/玻璃杯，半透明、无碰撞），容器中心 = 原点 (0,0,0)，
 *  「自动适配」按「适配目标」（fitTarget）缩放并把选中面放到容器承物面。
 *  返回 true = 已打开；false = 该菜谱没有可预览的本地模型。 */
export async function openRecipeModelPreview(
  recipeAssetPath: string,
  title: string,
  extra?: RecipePreviewExtra
): Promise<boolean> {
  const fail = (msg: string): boolean => {
    if (extra?.onError) extra.onError(msg);
    else alert(msg);
    return false;
  };
  try {
    // 先问「网格来源」端点：它同时覆盖①上传约定目录 ②model 引用的共享 prefab
    //（commonW2 夹心的四件套在扁平的 burger/models/ 下，只查约定目录会永远报「尚未上传模型」）。
    // 旧桥没有该端点时回退到原来的目录列举。
    let dir = "";
    let model = "";
    let files: string[] = [];
    try {
      const src = await api.fetchCustomRecipeModelSource(recipeAssetPath);
      dir = src.dirAssetPath;
      model = src.modelFile;
      files = src.files;
    } catch {
      dir = "";
    }
    if (!model) {
      files = await api.fetchCustomRecipeModelFiles(recipeAssetPath);
      const found = files.find((f) => /\.(fbx|obj)$/i.test(f));
      if (!found) return fail("该菜谱没有可预览的本地模型（只有指向游戏 bundle 的模型指针时，网页无法预览）。");
      model = found;
      dir = "";
    }
    const { openModelPreview } = await import("./modelPreview");
    const isReadonly = extra?.readonly ?? isSharedReadonlyAsset(recipeAssetPath);
    const base = dir ? dirResourceBase(dir) : modelResourceBase(recipeAssetPath);
    const texUrls = files
      .filter((f) => /\.(png|jpg|jpeg)$/i.test(f))
      .map((f) => base + encodeURIComponent(f));
    const mtl = files.find((f) => /\.mtl$/i.test(f));
    openModelPreview({
      title,
      resourceBase: base,
      modelFileName: model,
      mtlUrl: mtl ? base + encodeURIComponent(mtl) : undefined,
      fitTarget: extra?.fitTarget,
      scale: extra?.scale,
      rotationX: extra?.rotationX,
      rotationY: extra?.rotationY,
      rotationZ: extra?.rotationZ,
      positionX: extra?.positionX,
      positionY: extra?.positionY,
      positionZ: extra?.positionZ,
      pivotX: extra?.pivotX,
      pivotY: extra?.pivotY,
      pivotZ: extra?.pivotZ,
      unitySize: extra?.unitySize,
      // 只读时不接调整回调，彻底断掉写回路径
      onAdjust: isReadonly ? undefined : extra?.onAdjust,
      readonly: isReadonly,
      readonlyReason: isReadonly
        ? "该模型来自 commonW2「Burger大全」共享素材库，所有关卡集共用，变换参数不随单个关卡集保存。"
        : undefined,
      remoteTextures: texUrls,
    });
    return true;
  } catch (e) {
    return fail((e as Error).message || "模型预览加载失败。");
  }
}

// ---------------------------------------------------------------- 模板网格

const TEMPLATE_MESH_DIR = "Assets/commonW2/custom_recipes/burger/models";

/** 模板 FBX 字节缓存：贴图编辑器每次改色都要重新渲染，不能每次都重新下载。 */
const templateBufferCache = new Map<string, ArrayBuffer>();

/** 取模板网格的 FBX 字节（供 openModelPreview 的 localBuffer 分支用）。 */
export async function fetchTemplateMeshBuffer(templateId: string): Promise<ArrayBuffer> {
  const cached = templateBufferCache.get(templateId);
  if (cached) return cached;
  const url = dirResourceBase(TEMPLATE_MESH_DIR) + encodeURIComponent(templateId + ".fbx");
  const r = await fetch(url);
  if (!r.ok) throw new Error(`模板网格加载失败（${templateId}.fbx，HTTP ${r.status}）`);
  const buf = await r.arrayBuffer();
  templateBufferCache.set(templateId, buf);
  return buf;
}

/** 未落盘的本地预览：模板/上传的 FBX 字节 + 本地贴图 File，直接在浏览器里渲染。
 *  贴图编辑器据此做到「改一次颜色立刻看到上色后的模型」，零服务器往返。 */
export async function previewLocalModel(opts: {
  title: string;
  buffer: ArrayBuffer;
  fileName: string;
  textures?: File[];
  fitTarget?: "plate" | "cup";
  scale?: number;
}): Promise<void> {
  const { openModelPreview } = await import("./modelPreview");
  openModelPreview({
    title: opts.title,
    resourceBase: "",
    modelFileName: opts.fileName,
    localBuffer: opts.buffer,
    localTextures: opts.textures,
    fitTarget: opts.fitTarget ?? "plate",
    scale: opts.scale,
  });
}
