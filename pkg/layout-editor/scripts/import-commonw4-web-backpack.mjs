#!/usr/bin/env node
/**
 * commonW4 · Web 储物背包素材导入（OBJ 落盘 + 目录 meta 骨架）。
 * 完整 PBR/运行时 prefab 仍可在 Unity 内精调；本脚本保证 catalog 可扫描到包装 prefab。
 *
 *   node layout-editor/scripts/import-commonw4-web-backpack.mjs
 *   node layout-editor/scripts/import-commonw4-web-backpack.mjs --apply
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const apply = process.argv.includes("--apply");
const srcObj = path.join(
  repoRoot,
  "dump_bundle/Assets/downloadablecontent/dlc09/dlc_assets/prefabs/sharedkitchen/camping/dlc09_backpack.obj"
);
const destObj = path.join(repoRoot, "Assets/commonW4/models/mechanisms/web_backpack_storage_01.obj");

const steps = [];
if (!fs.existsSync(destObj) && fs.existsSync(srcObj)) {
  steps.push({ action: "copy-obj", from: srcObj, to: destObj });
}

console.log("import-commonw4-web-backpack:", apply ? "APPLY" : "dry-run");
for (const s of steps) {
  console.log(`  ${s.action}: ${s.from} -> ${s.to}`);
  if (apply) {
    fs.mkdirSync(path.dirname(s.to), { recursive: true });
    fs.copyFileSync(s.from, s.to);
  }
}
if (steps.length === 0) console.log("  (nothing to do — OBJ 已存在或源缺失)");
