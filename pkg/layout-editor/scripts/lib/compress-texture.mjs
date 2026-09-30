/**
 * commonW3 手持饮品模型贴图压缩（sips 缩边 + PNG 落盘）。
 * 1024 → 512 进一步减体积，端盘手持尺度足够。
 */
import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

/** Web 端盘展示推荐最长边（像素）。 */
export const DEFAULT_TEXTURE_MAX = 512;

const TEX_EXT = new Set([".png", ".jpg", ".jpeg"]);

/**
 * @param {string} src
 * @param {string} dest
 * @param {number} [maxSize]
 */
export function compressTexture(src, dest, maxSize = DEFAULT_TEXTURE_MAX) {
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  const ext = path.extname(dest).toLowerCase();
  try {
    if (ext === ".png") {
      execSync(`sips -Z ${maxSize} -s format png "${src}" --out "${dest}"`, { stdio: "pipe" });
    } else if (ext === ".jpg" || ext === ".jpeg") {
      execSync(`sips -Z ${maxSize} -s format jpeg -s formatOptions 85 "${src}" --out "${dest}"`, {
        stdio: "pipe",
      });
    } else {
      execSync(`sips -Z ${maxSize} "${src}" --out "${dest}"`, { stdio: "pipe" });
    }
  } catch {
    fs.copyFileSync(src, dest);
  }
}

/** @param {string} filePath */
export function isModelTextureFile(filePath) {
  const base = path.basename(filePath);
  const ext = path.extname(base).toLowerCase();
  if (!TEX_EXT.has(ext)) return false;
  return /_(base_color|normal|roughness|metallic)\./i.test(base);
}

/**
 * 就地压缩目录下所有 PBR 贴图。
 * @param {string} dir
 * @param {number} [maxSize]
 * @returns {{ file: string; before: number; after: number }[]}
 */
export function recompressTexturesInDir(dir, maxSize = DEFAULT_TEXTURE_MAX) {
  if (!fs.existsSync(dir)) return [];
  const out = [];
  for (const name of fs.readdirSync(dir)) {
    const abs = path.join(dir, name);
    if (!fs.statSync(abs).isFile() || !isModelTextureFile(abs)) continue;
    const before = fs.statSync(abs).size;
    const tmp = `${abs}.recompress.tmp`;
    compressTexture(abs, tmp, maxSize);
    fs.renameSync(tmp, abs);
    const after = fs.statSync(abs).size;
    out.push({ file: abs, before, after });
  }
  return out;
}

/**
 * 将 TextureImporter .meta 的 maxTextureSize 同步为指定值。
 * @param {string} metaPath
 * @param {number} maxSize
 */
export function patchTextureMetaMaxSize(metaPath, maxSize = DEFAULT_TEXTURE_MAX) {
  if (!fs.existsSync(metaPath)) return false;
  let raw = fs.readFileSync(metaPath, "utf8");
  const next = raw.replace(/maxTextureSize: \d+/g, `maxTextureSize: ${maxSize}`);
  if (next === raw) return false;
  fs.writeFileSync(metaPath, next, "utf8");
  return true;
}
