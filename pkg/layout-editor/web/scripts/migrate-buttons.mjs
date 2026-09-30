#!/usr/bin/env node
/**
 * One-shot codemod: replace common inline button HTML with ui/views helpers.
 * Run from layout-editor/web: node scripts/migrate-buttons.mjs
 */
import { readFileSync, writeFileSync } from "fs";
import { globSync } from "fs";
import path from "path";

const SRC = path.join(import.meta.dirname, "../src");

const FILES = globSync("**/*.ts", { cwd: SRC })
  .map((f) => path.join(SRC, f))
  .filter((f) => !f.includes("/ui/views/") && !f.includes("/theme/"));

const REPLACEMENTS = [
  // modal footer pairs
  [
    /`<button type="button" class="modal-btn" data-cancel>取消<\/button>\s*<button type="button" class="modal-btn primary" data-ok>确定<\/button>`/g,
    "`${modalFooterBtnsHtml()}`",
  ],
  [
    /`<button type="button" class="modal-btn" data-cancel>取消<\/button>\n\s*<button type="button" class="modal-btn primary" data-ok>确定<\/button>`/g,
    "`${modalFooterBtnsHtml()}`",
  ],
  [
    /`<button type="button" class="modal-btn" data-cancel>取消<\/button>\n\s*<button type="button" class="modal-btn primary" data-ok>保存<\/button>`/g,
    "`${cancelBtnHtml()} ${primaryBtnHtml(\"保存\")}`",
  ],
  [
    /`<button type="button" class="modal-btn" data-cancel>关闭<\/button>`/g,
    "`${cancelBtnHtml(\"关闭\")}`",
  ],
  // m-btn pairs
  [
    /`<button type="button" class="m-btn" data-cancel>取消<\/button><button type="button" class="m-btn primary" data-ok>确定<\/button>`/g,
    "`${mCancelBtnHtml()}${mPrimaryBtnHtml()}`",
  ],
  [
    /`<button type="button" class="m-btn" data-cancel>取消<\/button><button type="button" class="m-btn primary" data-ok>保存<\/button>`/g,
    "`${mCancelBtnHtml()}${mPrimaryBtnHtml(\"保存\")}`",
  ],
  [
    /`<button type="button" class="m-btn" data-cancel>取消<\/button><button type="button" class="m-btn primary" data-ok>创建<\/button>`/g,
    "`${mCancelBtnHtml()}${mPrimaryBtnHtml(\"创建\")}`",
  ],
  [
    /`<button type="button" class="m-btn" data-cancel>取消<\/button><button type="button" class="m-btn primary" data-ok>开始导出<\/button>`/g,
    "`${mCancelBtnHtml()}${mPrimaryBtnHtml(\"开始导出\")}`",
  ],
  [
    /`<button type="button" class="m-btn" data-cancel>取消<\/button><button type="button" class="m-btn primary" data-ok>导出依赖包<\/button>`/g,
    "`${mCancelBtnHtml()}${mPrimaryBtnHtml(\"导出依赖包\")}`",
  ],
  [
    /`<button type="button" class="m-btn" data-cancel>取消<\/button><button type="button" class="m-btn primary" data-ok>保存全部<\/button>`/g,
    "`${mCancelBtnHtml()}${mPrimaryBtnHtml(\"保存全部\")}`",
  ],
  [
    /`<button type="button" class="m-btn" data-cancel>取消<\/button><button type="button" class="m-btn primary" data-ok>确认重命名<\/button>`/g,
    "`${mCancelBtnHtml()}${mPrimaryBtnHtml(\"确认重命名\")}`",
  ],
  [
    /`<button type="button" class="m-btn" data-cancel>取消<\/button><button type="button" class="m-btn danger" data-ok>确认删除<\/button>`/g,
    "`${mCancelBtnHtml()}${dangerBtnHtml(\"确认删除\", { \"data-ok\": \"\" })}`",
  ],
  [
    /`<button type="button" class="modal-btn" data-cancel>取消<\/button>\n\s*<button type="button" class="modal-btn primary" data-ok>保存顺序<\/button>`/g,
    "`${cancelBtnHtml()} ${primaryBtnHtml(\"保存顺序\")}`",
  ],
];

const IMPORT_LINE =
  'import { cancelBtnHtml, primaryBtnHtml, modalFooterBtnsHtml, mCancelBtnHtml, mPrimaryBtnHtml, dangerBtnHtml, mBtnHtml, smallBtnHtml } from "./ui/views/button";';

const IMPORT_LINE_EDITOR =
  'import { cancelBtnHtml, primaryBtnHtml, modalFooterBtnsHtml, mCancelBtnHtml, mPrimaryBtnHtml, dangerBtnHtml, mBtnHtml, smallBtnHtml } from "../ui/views/button";';

function neededImports(content) {
  const names = [];
  if (/\bcancelBtnHtml\b/.test(content)) names.push("cancelBtnHtml");
  if (/\bprimaryBtnHtml\b/.test(content)) names.push("primaryBtnHtml");
  if (/\bmodalFooterBtnsHtml\b/.test(content)) names.push("modalFooterBtnsHtml");
  if (/\bmCancelBtnHtml\b/.test(content)) names.push("mCancelBtnHtml");
  if (/\bmPrimaryBtnHtml\b/.test(content)) names.push("mPrimaryBtnHtml");
  if (/\bdangerBtnHtml\b/.test(content)) names.push("dangerBtnHtml");
  if (/\bmBtnHtml\b/.test(content)) names.push("mBtnHtml");
  if (/\bsmallBtnHtml\b/.test(content)) names.push("smallBtnHtml");
  return names;
}

function buildImport(names, relPath) {
  return `import { ${names.join(", ")} } from "${relPath}";`;
}

function relImportPath(filePath) {
  const dir = path.dirname(filePath);
  const rel = path.relative(dir, path.join(SRC, "ui/views/button")).replace(/\\/g, "/");
  return rel.startsWith(".") ? rel : `./${rel}`;
}

let changed = 0;
for (const file of FILES) {
  let content = readFileSync(file, "utf8");
  if (file.endsWith("modals.ts") || file.endsWith("nav.ts")) continue;
  let modified = false;
  for (const [re, rep] of REPLACEMENTS) {
    const next = content.replace(re, rep);
    if (next !== content) {
      content = next;
      modified = true;
    }
  }
  if (!modified) continue;

  const names = neededImports(content);
  if (names.length && !content.includes('from "./ui/views/button"') && !content.includes('from "../ui/views/button"')) {
    const importPath = relImportPath(file);
    const importStmt = buildImport(names, importPath);
    const firstImport = content.match(/^import .+$/m);
    if (firstImport) {
      content = content.replace(firstImport[0], `${firstImport[0]}\n${importStmt}`);
    } else {
      content = `${importStmt}\n${content}`;
    }
  }
  writeFileSync(file, content);
  changed++;
  console.log("updated:", path.relative(SRC, file));
}
console.log(`Done. ${changed} files updated.`);
