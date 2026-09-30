#!/usr/bin/env node
/**
 * 将果汁 / 冰淇淋 / 冰沙模型目录内 PBR 贴图压缩到 1024（就地覆盖）。
 *
 * 用法：
 *   node recompress-commonw3-drink-textures.mjs           # dry-run 统计
 *   node recompress-commonw3-drink-textures.mjs --apply
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  DEFAULT_TEXTURE_MAX,
  isModelTextureFile,
  patchTextureMetaMaxSize,
  recompressTexturesInDir,
} from "./lib/compress-texture.mjs";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const CATEGORIES = ["smoothie", "ice_cream", "milk_slush"];

function collectTextureFiles() {
  const files = [];
  for (const cat of CATEGORIES) {
    const modelsRoot = path.join(repoRoot, `Assets/commonW3/custom_recipes/${cat}/models`);
    if (!fs.existsSync(modelsRoot)) continue;
    for (const entry of fs.readdirSync(modelsRoot, { withFileTypes: true })) {
      if (!entry.isDirectory() || !entry.name.startsWith("Web_")) continue;
      const dir = path.join(modelsRoot, entry.name);
      for (const name of fs.readdirSync(dir)) {
        const abs = path.join(dir, name);
        if (fs.statSync(abs).isFile() && isModelTextureFile(abs)) files.push(abs);
      }
    }
  }
  return files.sort();
}

function formatKb(n) {
  return `${Math.round(n / 1024)}KB`;
}

const apply = process.argv.includes("--apply");
const files = collectTextureFiles();

if (!files.length) {
  console.log("未找到可压缩贴图。");
  process.exit(0);
}

let totalBefore = 0;
for (const f of files) totalBefore += fs.statSync(f).size;

console.log(`共 ${files.length} 张贴图，当前合计 ${formatKb(totalBefore)}（目标最长边 ${DEFAULT_TEXTURE_MAX}px）`);

if (!apply) {
  console.log("\ndry-run；加 --apply 执行压缩。");
  process.exit(0);
}

let compressed = 0;
let totalAfter = 0;
let metasPatched = 0;

for (const cat of CATEGORIES) {
  const modelsRoot = path.join(repoRoot, `Assets/commonW3/custom_recipes/${cat}/models`);
  if (!fs.existsSync(modelsRoot)) continue;
  for (const entry of fs.readdirSync(modelsRoot, { withFileTypes: true })) {
    if (!entry.isDirectory() || !entry.name.startsWith("Web_")) continue;
    const results = recompressTexturesInDir(path.join(modelsRoot, entry.name));
    for (const r of results) {
      compressed++;
      totalAfter += r.after;
      if (patchTextureMetaMaxSize(`${r.file}.meta`)) metasPatched++;
    }
  }
}

console.log(`\n完成：${compressed} 张贴图 ${formatKb(totalBefore)} → ${formatKb(totalAfter)}（约 -${Math.round((1 - totalAfter / totalBefore) * 100)}%）`);
console.log(`更新 ${metasPatched} 个 .meta maxTextureSize → ${DEFAULT_TEXTURE_MAX}`);
