/** OBJ/MTL 贴图引用改名（纯文本）：主模型统一命名为 <recipeId>.obj、MTL 命名为
 *  <recipeId>.mtl 后，OBJ 内的 mtllib 与 MTL 内的 map_* 贴图引用名对不上，
 *  Unity 会丢失贴图。这里把引用名改写为落盘后的最终文件名。 */

import { classifyTextureName, type TextureDiskNames } from "./fbxTextureRename";

export interface MtlRenameResult {
  mtl: string;
  renamed: number;
}

/** MTL 贴图通道 → 贴图用途（map_* 关键字；无关键字的按文件名关键字兜底）。 */
function channelClass(kw: string): import("./fbxTextureRename").TextureClass | null {
  const k = kw.toLowerCase();
  if (k === "map_ke") return "emissive";
  if (k === "map_kd" || k === "map_ka" || k === "map_kk") return "base_color";
  if (k === "map_ks" || k === "map_pm" || k === "map_reflection") return "metallic";
  if (k === "map_pr" || k === "map_ns" || k === "map_ps" || k === "map_glossiness") return "roughness";
  if (k === "map_bump" || k === "norm" || k === "bump" || k === "disp") return "normal";
  return null;
}

const IMAGE_EXT_RE = /\.(png|jpg|jpeg|tga|bmp|tif|tiff)$/i;

function isMapLine(line: string): boolean {
  return /^(map_[a-z0-9_]+|bump|norm|disp)\s/i.test(line.trim());
}

/** 改写 MTL 贴图引用：
 *  @param diskNames  类别 → 落盘文件名（<recipeId>_base_color.png 等，由贴图格子选择）
 *  @param extraNames 原文件名 → 落盘文件名（OBJ 附带图片文件按 sanitizeUploadFileName 落盘）
 *  优先级：精确文件名匹配（extraNames）→ 通道/文件名关键字分类（diskNames）。 */
export function renameMtlTextureRefs(
  mtlText: string,
  diskNames: TextureDiskNames,
  extraNames?: Record<string, string>
): MtlRenameResult {
  const lowerMap = new Map<string, string>();
  for (const [k, v] of Object.entries(extraNames ?? {})) {
    if (v) lowerMap.set(k.toLowerCase(), v);
  }
  let renamed = 0;
  const out = mtlText.split("\n").map((line) => {
    const trimmed = line.trim();
    if (!isMapLine(trimmed)) return line;
    const parts = line.split(/\s+/);
    if (parts.length < 2) return line;
    const kw = parts[0];
    const cls = channelClass(kw);
    // 从行尾找带图片扩展名的文件名；无扩展名时取最后一个 token
    let idx = -1;
    for (let i = parts.length - 1; i >= 1; i--) {
      if (IMAGE_EXT_RE.test(parts[i])) {
        idx = i;
        break;
      }
    }
    if (idx < 0) idx = parts.length - 1;
    const file = parts[idx];
    const exact = lowerMap.get(file.toLowerCase());
    const disk =
      exact ??
      (cls
        ? (diskNames[cls] ?? (diskNames[classifyTextureName(file)] ?? undefined))
        : (diskNames[classifyTextureName(file)] ?? undefined));
    if (!disk || disk === file) return line;
    parts[idx] = disk;
    renamed++;
    return parts.join(" ");
  });
  return { mtl: out.join("\n"), renamed };
}

/** 把 OBJ 文本的 mtllib 行改写为目标 MTL 文件名；无 mtllib 行时在文件头补一行
 *  （Unity OBJ 导入按 mtllib 关联材质，缺行则模型灰色）。返回改写后文本。 */
export function ensureObjMtllib(objText: string, newMtlName: string): string {
  if (!newMtlName) return objText;
  let replaced = false;
  const out = objText.split("\n").map((line) => {
    if (/^mtllib\s/i.test(line.trim())) {
      replaced = true;
      const name = line.trim().split(/\s+/)[1];
      return name === newMtlName ? line : line.replace(name, newMtlName);
    }
    return line;
  });
  if (!replaced) out.unshift(`mtllib ${newMtlName}`);
  return out.join("\n");
}
