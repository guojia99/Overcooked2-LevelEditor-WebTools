// Execute production Web export polling with transport doubles; never contacts Unity.
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createRequire } from "node:module";
import vm from "node:vm";
const require = createRequire(new URL("../web/package.json", import.meta.url));
const ts = require("typescript");
const source = await readFile(new URL("../web/src/levels.ts", import.meta.url), "utf8");
const ast = ts.createSourceFile("levels.ts", source, ts.ScriptTarget.Latest, true);
const names = ["updateExportStages", "awaitExportAndDownload"];
const methods = ast.statements.filter(node => ts.isFunctionDeclaration(node) && names.includes(node.name?.text));
assert.equal(methods.length, 2);
const js = ts.transpileModule(methods.map(node => node.getText(ast)).join("\n"), {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.None },
}).outputText;
let ticks = 0;
let reads = 0;
let downloads = 0;
let stages = 0;
let clicks = 0;
const snapshots = [new Error("offline"), { status: "idle" },
  { status: "running", exportId: "task", setName: "fixture", mode: "levels", phase: "restore" },
  { status: "done", exportId: "task", setName: "fixture", mode: "levels", zipFileName: "test.zip" }];
const context = vm.createContext({
  activeExportId: "task", EXPORT_PHASE_HINT: {},
  Date: { now: () => ticks },
  setTimeout: (callback, ms) => { ticks += ms; callback(); },
  setBusyMessage() {}, setBusyResult() {}, setStatus() {},
  setBusyStages() { stages++; }, fmtElapsed: String, queueFocusNote: () => "",
  URL: { createObjectURL: () => "blob:test", revokeObjectURL() {} },
  document: { createElement: () => ({ click() { clicks++; } }) },
  api: {
    async fetchSetExportStatus() { reads++; const value = snapshots.shift(); if (value instanceof Error) throw value; return value; },
    async downloadSetExportZip() { downloads++; return { blob: {}, fileName: "test.zip" }; },
  },
});
vm.runInContext(js, context);
await vm.runInContext('awaitExportAndDownload("fixture", 0)', context);
assert.equal(reads, 4);
assert.equal(stages, 2);
assert.equal(downloads, 1);
assert.equal(clicks, 1);
assert.throws(() => vm.runInContext('updateExportStages({status:"done",exportId:"other"},"fixture","levels")', context), /替换/);
assert.equal(vm.runInContext('updateExportStages({status:"done",setName:"fixture",mode:"levels"},"fixture","levels")', context), false);
context.api.fetchSetExportStatus = async () => { throw new Error("offline"); };
ticks = 0;
await assert.rejects(vm.runInContext('awaitExportAndDownload("fixture", 0)', context), /30 分钟/);
console.log("PASS Web production polling: reconnect, idle, restore, matching ID, replacement, missing ID, deadline; no Unity requests.");
