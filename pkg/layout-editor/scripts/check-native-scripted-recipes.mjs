// Execute production draft editor/API functions with small DOM and HTTP doubles.
import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import { test } from "node:test";
import ts from "../web/node_modules/typescript/lib/typescript.js";

const A = "a".repeat(32), B = "b".repeat(32);
const fixture = () => ({ levelInfoAssetPath: "info", gameMode: "Campaign", configs: Array.from({ length: 4 }, () => ({ exists: true, roundTime: 240, scriptedRecipes: [] })) });
function load(file, api = {}, names) {
  let text = fs.readFileSync(new URL(`../web/src/${file}`, import.meta.url), "utf8");
  if (names) {
    const tree = ts.createSourceFile(file, text, ts.ScriptTarget.Latest, true);
    text = names.map(name => {
      const node = tree.statements.find(node => (ts.isFunctionDeclaration(node) && node.name?.text === name) ||
        (ts.isVariableStatement(node) && node.declarationList.declarations.some(declaration => declaration.name.getText(tree) === name)));
      assert(node, name);
      return node.getText(tree);
    }).join("\n");
  }
  const context = { exports: {}, require: () => api, URLSearchParams, confirm: () => true, ...api };
  vm.runInNewContext(ts.transpileModule(text, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText, context);
  return context.exports;
}
const plain = value => JSON.parse(JSON.stringify(value));

// Only selectors and elements the production editor uses; innerHTML rebuild discards old nodes.
class Host {
  dataset = {};
  set innerHTML(html) {
    this.html = html;
    this.nodes = [];
    for (const match of html.matchAll(/<(button|input|select)\b([^>]*)>/g)) {
      const attrs = match[2];
      const data = /\bdata-([\w-]+)(?:="([^"]*)")?/.exec(attrs);
      if (!data) continue;
      const option = match[1] === "select" ? /<option value="([^"]*)"/.exec(html.slice(match.index))?.[1] : "";
      this.nodes.push({ key: data[1], value: option || "", disabled: /\bdisabled\b/.test(attrs), dataset: { [data[1]]: data[2] ?? "" } });
    }
  }
  querySelectorAll(selector) {
    const [, key, value] = /^\[data-([\w-]+)(?:="([^"]*)")?\]$/.exec(selector) ?? [];
    assert(key, selector);
    return this.nodes.filter(node => node.key === key && (value === undefined || node.dataset[key] === value));
  }
  querySelector(selector) { return this.querySelectorAll(selector)[0]; }
  async click(selector) {
    const node = this.querySelector(selector);
    assert(node && !node.disabled, selector);
    await node.onclick();
  }
}

test("four drafts copy source arrays, preserve duplicate entries and reorder boundaries", () => {
  const { createScriptedDrafts, moveScriptedEntry, assertScriptedReadback } = load("nativeScriptedRecipes.ts");
  const detail = fixture(); detail.configs[0].scriptedRecipes = [A, B, A];
  const drafts = createScriptedDrafts(detail);
  drafts[1].push(B); moveScriptedEntry(drafts[0], 1, 0);
  assert.deepEqual(plain(drafts[0]), [B, A, A]);
  assert.deepEqual(detail.configs[0].scriptedRecipes, [A, B, A]);
  assert.deepEqual(plain(drafts[2]), []);
  moveScriptedEntry(drafts[0], 0, -1);
  assert.deepEqual(plain(drafts[0]), [B, A, A]);
  assert.throws(() => assertScriptedReadback([A, B, A], [A, A, B]), /读回不一致/);
  assert.throws(() => assertScriptedReadback([], null), /读回不一致/);
  assertScriptedReadback([A, A], [A.toUpperCase(), A]);
});

test("actual editor appends duplicates, switches independent players, moves/deletes/clears and reads back", async () => {
  let detail = fixture(); const writes = []; let reads = 0;
  const api = {
    fetchRecipeCatalog: async () => [{ guid: A, nameZh: "A" }, { guid: B, nameZh: "B" }],
    fetchLevelDetail: async () => { reads++; return structuredClone(detail); },
    fetchLevelOrderSequence: async () => null,
    updateLevelConfig: async request => {
      writes.push(plain(request));
      ["config_1p", "config_2p", "config_3p", "config_4p"].forEach((key, p) => { if (request[key]) detail.configs[p] = plain(request[key]); });
    },
  };
  const host = new Host();
  await load("nativeScriptedRecipes.ts", api).mountNativeScriptedRecipes(host, detail, "test");
  await host.click("[data-add]"); await host.click("[data-add]");
  assert.equal(host.dataset.dirty, "1");
  host.querySelector("[data-guid]").value = B; await host.click("[data-add]");
  await host.click('[data-up="2"]'); await host.click('[data-down="0"]');
  await host.click('[data-remove="2"]');
  await host.click('[data-player="1"]');
  host.querySelector("[data-guid]").value = B; await host.click("[data-add]");
  await host.click('[data-player="0"]');
  await host.click("[data-save]");
  assert.deepEqual(writes[0].config_1p.scriptedRecipes, [B, A]);
  assert.deepEqual(writes[0].config_2p.scriptedRecipes, [B]);
  assert(!("config_3p" in writes[0]) && !("config_4p" in writes[0]));
  assert(reads >= 3);
  assert.match(host.html, /逐项读回确认/);
  assert.equal(host.dataset.dirty, "0");
  await host.click("[data-clear]"); await host.click("[data-save]");
  assert.deepEqual(writes[1].config_1p.scriptedRecipes, []);
  assert(!("config_2p" in writes[1]));
  await host.click("[data-add]"); assert.equal(host.dataset.dirty, "1");
  await host.click("[data-discard]"); assert.equal(host.dataset.dirty, "0");
  host.dataset.parentSaving = "1";
  await host.click("[data-add]");assert.equal(host.dataset.dirty, "0", "score save blocks concurrent sequence mutation");
});

test("editor preserves dirty drafts after conflict, stale mode, or readback mismatch", async () => {
  const detail = fixture(); const host = new Host(); let web = null, mode = "Campaign", writes = 0;
  const api = {
    fetchRecipeCatalog: async () => [{ guid: A, nameZh: "A" }],
    fetchLevelDetail: async () => ({ ...structuredClone(detail), gameMode: mode }),
    fetchLevelOrderSequence: async () => web,
    updateLevelConfig: async () => { writes++; },
  };
  await load("nativeScriptedRecipes.ts", api).mountNativeScriptedRecipes(host, detail, "test");
  await host.click("[data-add]");
  web = { enabled: true }; await host.click("[data-save]");
  assert.equal(writes, 0); assert.match(host.html, /Web OrderSequence 已启用/);
  web = null; mode = "Dynamic"; await host.click("[data-save]");
  assert.equal(writes, 0); assert.match(host.html, /当前模式已变化/);
  mode = "Campaign"; await host.click("[data-save]");
  assert.equal(writes, 1); assert.match(host.html, /读回不一致/); assert.match(host.html, /1P \*/);
});

test("Web API rejects inverse native conflict before POST and verifies saves with no-cache GET", async () => {
  const requests = []; let detail = fixture(); detail.configs[3].scriptedRecipes = [A];
  const data = { enabled: true, endBehavior: "loop", groups: [{ mode: 0, batchSize: 0, recipeGuids: [A, A] }] };
  let persisted = null;
  const api = load("api.ts", {
    fetchLevelDetail: async () => detail,
    fetch: async (url, options) => {
      requests.push({ url, options });
      if (options?.method === "POST") persisted = JSON.parse(JSON.parse(options.body).json);
      return { exists: true, json: JSON.stringify(persisted) };
    },
    readApiJson: async response => response,
  }, ["saveLevelOrderSequence", "fetchLevelOrderSequence"]);
  await assert.rejects(api.saveLevelOrderSequence("info", data), /原生 scriptedRecipes 非空/);
  assert.equal(requests.length, 0);
  detail = fixture(); await api.saveLevelOrderSequence("info", data);
  assert.equal(requests[0].options.method, "POST"); assert.equal(requests[1].options.cache, "no-store");
  assert.deepEqual(persisted.groups[0].recipeGuids, [A, A]);
});

test("respawn API verifies explicit updates and leaves legacy requests without extra reads", async () => {
  let reads = 0, stored = true;
  const api = load("api.ts", {
    fetch: async () => ({}), readApiJson: async response => response,
    fetchLevelDetail: async () => { reads++; return { enableRespawnBounds: stored }; },
  }, ["updateLevelInfo"]);
  await api.updateLevelInfo({ assetPath: "info" });
  assert.equal(reads, 0);
  await assert.rejects(api.updateLevelInfo({ assetPath: "info", updateEnableRespawnBounds: true, enableRespawnBounds: false }), /读回不一致/);
  stored = false;
  await api.updateLevelInfo({ assetPath: "info", updateEnableRespawnBounds: true, enableRespawnBounds: false });
  assert.equal(reads, 2);
});

test("audio resource failure rejects with original error instead of returning an undefined save handle", async () => {
  const failure = new Error("audio catalog unavailable");
  const shown = [];
  const names = ["fetchMusicCatalog", "fetchAudioDirectoryCatalog", "fetchAmbiences", "fetchDeathEffects", "fetchAudioKnowledge", "fetchBundleGraph", "fetchAudioExports"];
  const api = Object.fromEntries(names.map(name => [name, async () => {
    if (name === "fetchMusicCatalog") throw failure;
    return [];
  }]));
  const tree = ts.createSourceFile("levels.ts", fs.readFileSync(new URL("../web/src/levels.ts", import.meta.url), "utf8"), ts.ScriptTarget.Latest, true);
  const declaration = tree.statements.find(node => ts.isFunctionDeclaration(node) && node.name?.text === "wireAudioEditor");
  assert(declaration);
  const context = { exports: {}, api, setStatus() {}, showError: error => shown.push(error) };
  vm.runInNewContext(ts.transpileModule(`${declaration.getText(tree)}\nexports.wireAudioEditor = wireAudioEditor;`, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  }).outputText, context);
  await assert.rejects(context.exports.wireAudioEditor({}, { sceneAssetPath: "scene" }, {}, () => {}), error => error === failure);
  assert.deepEqual(shown, [failure]);
});

test("levels.ts type-checks under project settings without changing unrelated diagnostics", () => {
  const configPath = new URL("../web/tsconfig.json", import.meta.url);
  const config = ts.readConfigFile(configPath.pathname, ts.sys.readFile);
  assert.equal(config.error, undefined);
  const parsed = ts.parseJsonConfigFileContent(config.config, ts.sys, new URL("../web/", import.meta.url).pathname);
  const program = ts.createProgram(parsed.fileNames, { ...parsed.options, noEmit: true, incremental: false });
  const diagnostics = ts.getPreEmitDiagnostics(program).filter(diagnostic =>
    diagnostic.file?.fileName.replaceAll("\\", "/").endsWith("/web/src/levels.ts"));
  assert.equal(diagnostics.length, 0, ts.formatDiagnosticsWithColorAndContext(diagnostics, {
    getCurrentDirectory: ts.sys.getCurrentDirectory,
    getCanonicalFileName: file => file,
    getNewLine: () => "\n",
  }));
});

test("real modal close and close-all honor dirty guard, including stacked parent drafts", () => {
  let active = null; const attached = [];
  const root = {
    querySelector: () => active,
    appendChild: parent => { active = parent;attached.push(parent); },
    set innerHTML(value) { assert.equal(value, "");active = null; },
  };
  const source = `\nexport function fixtureGuard(node, guard) { modalCloseGuards.set(node, guard); }
    export function fixtureStack(node) { modalStack.push(node); }`;
  const names = ["modalStack", "modalCloseGuards", "closeModal", "closeAllModals"];
  const text = fs.readFileSync(new URL("../web/src/modals.ts", import.meta.url), "utf8");
  const tree = ts.createSourceFile("modals.ts", text, ts.ScriptTarget.Latest, true);
  const declarations = names.map(name => tree.statements.find(node =>
    (ts.isFunctionDeclaration(node) && node.name?.text === name) ||
    (ts.isVariableStatement(node) && node.declarationList.declarations.some(declaration => declaration.name.getText(tree) === name))).getText(tree)).join("\n");
  const context = { exports: {}, document: { getElementById: () => root } };
  vm.runInNewContext(ts.transpileModule(declarations + source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText, context);
  const api = context.exports;
  let dirty = true;
  const parent = { remove() { active = null; } }, child = { remove() { active = null; } };
  active = parent;api.fixtureGuard(parent, () => !dirty);
  api.closeModal();assert.equal(active, parent);
  api.closeAllModals();assert.equal(active, parent);
  active = child;api.fixtureStack(parent);
  api.closeAllModals();assert.equal(active, child, "parent guard prevents destructive close-all");
  api.closeModal();assert.equal(active, parent, "child closes and restores draft parent");
  dirty = false;api.closeModal();assert.equal(active, null);
});
