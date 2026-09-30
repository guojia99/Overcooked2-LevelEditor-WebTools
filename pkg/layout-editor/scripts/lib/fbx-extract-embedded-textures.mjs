/**
 * 从二进制 FBX 提取内嵌 Video/Content 贴图（Meshy / Tripo 等导出常见）。
 */
import { parseBinary } from "fbx-parser";

/** @typedef {{ cls: string; fileName: string; bytes: Uint8Array }} ExtractedTexture */

/**
 * @param {string} fileName
 * @returns {string | null}
 */
export function classifyTextureName(fileName) {
  const lower = fileName.toLowerCase();
  if (lower.includes("normal")) return "normal";
  if (lower.includes("roughness") || lower.includes("_rough")) return "roughness";
  if (lower.includes("metallic") || lower.includes("_metal")) return "metallic";
  if (lower.includes("basecolor") || lower.includes("base_color") || lower.includes("albedo")) return "base_color";
  if (/\.(png|jpg|jpeg|tga|bmp)$/i.test(lower) && !lower.includes("normal") && !lower.includes("rough") && !lower.includes("metal")) {
    return "base_color";
  }
  return null;
}

/**
 * @param {Uint8Array|Buffer} fbxBytes
 * @returns {ExtractedTexture[]}
 */
export function extractFbxEmbeddedTextures(fbxBytes) {
  const fbx = parseBinary(fbxBytes);
  const videos = [];

  function collectVideos(node) {
    if (!node) return;
    if (Array.isArray(node)) return node.forEach(collectVideos);
    if (typeof node !== "object") return;
    if (node.name === "Video" && node.props?.[1]) videos.push(node);
    for (const v of Object.values(node)) collectVideos(v);
  }
  collectVideos(fbx);

  const out = [];
  for (const video of videos) {
    const fileName = String(video.props[1]).replace(/^Video::/, "");
    const cls = classifyTextureName(fileName);
    if (!cls) continue;

    let content = null;
    function findContent(node) {
      if (!node || content) return;
      if (Array.isArray(node)) return node.forEach(findContent);
      if (typeof node !== "object") return;
      if (node.name === "Content" && node.props?.[0]) content = node.props[0];
      for (const v of Object.values(node)) findContent(v);
    }
    findContent(video);
    if (!content) continue;

    const bytes = content instanceof Uint8Array ? content : Uint8Array.from(content);
    if (bytes.length < 64) continue;
    out.push({ cls, fileName, bytes });
  }

  const byCls = new Map();
  for (const tex of out) {
    if (!byCls.has(tex.cls)) byCls.set(tex.cls, tex);
  }
  return [...byCls.values()];
}
