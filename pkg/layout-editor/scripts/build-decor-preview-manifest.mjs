#!/usr/bin/env node
/**
 * build-decor-preview-manifest.mjs — 装饰缩略图清单/缓存版本生成。
 *
 * 输入：
 *   layout-editor/web/public/catalog.json + catalog/items.*.json
 *   layout-editor/web/public/icons/decor-preview/<guid>.png
 * 输出：
 *   layout-editor/web/public/icons/decor-preview/manifest.json
 *
 * manifest 结构：
 *   {
 *     version: "<文件列表+大小的短哈希>",
 *     generatedAt: "<ISO>",
 *     thumbSize: 192,
 *     files: { "<guid>": "<guid>.png", ... },
 *     missing: [{ id, guid, nameZh }, ...]
 *   }
 *
 * 前端用 version 拼 ?v= 做 immutable 缓存；missing 用于卡片占位，不发无谓请求。
 *
 * Usage:
 *   node layout-editor/scripts/build-decor-preview-manifest.mjs [--check]
 *   --check 只报告覆盖率，不写 manifest，存在缺失时退出码 1。
 */
import crypto from "crypto";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PUBLIC_DIR = path.resolve(__dirname, "../web/public");
const PREVIEW_DIR = path.join(PUBLIC_DIR, "icons/decor-preview");
const DIST_PREVIEW_DIR = path.resolve(__dirname, "../web/dist/icons/decor-preview");
const MANIFEST_NAME = "manifest.json";
const THUMB_SIZE = 192;

function readJson(file) {
  return JSON.parse(fs.readFileSync(file, "utf8"));
}

function loadCatalogItems() {
  const catalogPath = path.join(PUBLIC_DIR, "catalog.json");
  if (!fs.existsSync(catalogPath)) {
    throw new Error(`缺少 ${catalogPath}，请先运行 build-catalog.mjs`);
  }
  const index = readJson(catalogPath);
  const items = [];
  for (const chunk of index.itemChunks || []) {
    const file = path.join(PUBLIC_DIR, chunk);
    if (!fs.existsSync(file)) continue;
    for (const item of readJson(file).items || []) items.push(item);
  }
  return items;
}

function main() {
  const checkOnly = process.argv.includes("--check");
  const items = loadCatalogItems().filter((it) => it && it.layoutTier === "decor" && it.guid);
  const unique = new Map();
  for (const it of items) {
    if (!unique.has(it.guid)) unique.set(it.guid, it);
  }

  if (!fs.existsSync(PREVIEW_DIR)) {
    fs.mkdirSync(PREVIEW_DIR, { recursive: true });
  }

  const files = {};
  const missing = [];
  const hash = crypto.createHash("sha1");
  let bytes = 0;
  for (const [guid, it] of [...unique.entries()].sort((a, b) => a[0].localeCompare(b[0]))) {
    const file = `${guid}.png`;
    const abs = path.join(PREVIEW_DIR, file);
    if (!fs.existsSync(abs)) {
      missing.push({ id: it.id, guid, nameZh: it.nameZh || it.id });
      continue;
    }
    const stat = fs.statSync(abs);
    if (stat.size < 64) {
      missing.push({ id: it.id, guid, nameZh: it.nameZh || it.id });
      continue;
    }
    files[guid] = file;
    bytes += stat.size;
    hash.update(guid);
    hash.update(String(stat.size));
    hash.update(String(Math.round(stat.mtimeMs)));
  }

  const manifest = {
    version: hash.digest("hex").slice(0, 16),
    generatedAt: new Date().toISOString(),
    thumbSize: THUMB_SIZE,
    files,
    missing,
  };

  const total = unique.size;
  const ok = Object.keys(files).length;
  const pct = total > 0 ? ((ok / total) * 100).toFixed(1) : "0.0";
  console.log(
    `decor preview: ${ok}/${total} (${pct}%), ${(bytes / 1024 / 1024).toFixed(1)} MB, ` +
      `missing=${missing.length}, version=${manifest.version}`
  );
  if (missing.length > 0) {
    const print = Math.min(missing.length, 80);
    console.log(`missing previews (first ${print}/${missing.length}):`);
    for (const m of missing.slice(0, print)) console.log(`  ${m.id} | ${m.guid} | ${m.nameZh}`);
  }

  if (checkOnly) {
    if (missing.length > 0) process.exitCode = 1;
    return;
  }

  const out = path.join(PREVIEW_DIR, MANIFEST_NAME);
  fs.writeFileSync(out, JSON.stringify(manifest, null, 2) + "\n", "utf8");
  console.log(`wrote ${out}`);
  if (fs.existsSync(DIST_PREVIEW_DIR)) {
    fs.mkdirSync(DIST_PREVIEW_DIR, { recursive: true });
    fs.writeFileSync(path.join(DIST_PREVIEW_DIR, MANIFEST_NAME), JSON.stringify(manifest, null, 2) + "\n", "utf8");
    console.log(`wrote ${path.join(DIST_PREVIEW_DIR, MANIFEST_NAME)}`);
  }
}

main();
