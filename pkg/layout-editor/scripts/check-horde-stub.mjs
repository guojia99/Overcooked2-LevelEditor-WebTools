// Read-only production checks; generated C# and build outputs stay in the OS temp directory.
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import vm from "node:vm";
import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import ts from "../web/node_modules/typescript/lib/typescript.js";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const read = (p) => fs.readFileSync(path.join(root, p), "utf8");

// --update-catalog snapshots in memory and runs only the Horde incremental generator.
const catalogDirs = ["public", "dist"].map((dir) => `layout-editor/web/${dir}`);
function catalogSnapshot(dir) {
  const manifest = JSON.parse(read(`${dir}/catalog.json`));
  const chunks = manifest.itemChunks.map((name) => JSON.parse(read(`${dir}/${name}`)));
  const hashes = {};
  function visit(relative) {
    for (const entry of fs.readdirSync(path.join(root, relative), { withFileTypes: true })) {
      const name = `${relative}/${entry.name}`;
      if (entry.isDirectory()) visit(name);
      else if (entry.name.endsWith(".json")) hashes[name.slice(dir.length + 1)] = createHash("sha256").update(fs.readFileSync(path.join(root, name))).digest("hex");
    }
  }
  visit(dir);
  return { manifest, chunks, items: chunks.flatMap((chunk) => chunk.items), hashes };
}
const nativeRoot = "Assets/common04/game_environment/horde/prefabs";
const nativeGuids = new Set(["23525467a89357149ac283bdbeb11d1e", "9082ea11251999449830958b76c907cd"]);
const affectedCategories = new Set(["counters/service", "mechanisms/doors", "horde"]);
const generatorText = read("layout-editor/scripts/build-catalog.mjs");
const generatorSource = ts.createSourceFile("build-catalog.mjs", generatorText, ts.ScriptTarget.Latest, true);
const hordeUpdater = generatorSource.statements.find((node) => ts.isFunctionDeclaration(node) && node.name?.text === "updateHordeOnly").getText(generatorSource);
const generatorConstants = generatorSource.statements.filter((node) => ts.isVariableStatement(node)
  && node.declarationList.declarations.some((decl) => ["HORDE_PREFABS", "CORE_PALETTE", "DEFAULT_PARENT"].includes(decl.name.getText(generatorSource))))
  .map((node) => node.getText(generatorSource)).join("\n");
for (const duplicateId of [false, true]) {
  // Exercise real delta logic with groups that become empty, duplicate groups,
  // and another source retaining the same ID in the old category.
  const native = [
    { id: "Service_Window", guid: "23525467a89357149ac283bdbeb11d1e", layoutTier: "core", category: "horde" },
    { id: "gate", guid: "9082ea11251999449830958b76c907cd", layoutTier: "core", category: "horde" },
  ];
  const oldItems = native.map((item, i) => ({ ...item, category: i ? "mechanisms/doors" : "counters/service", icon: false }));
  if (duplicateId) oldItems.push({ id: "gate", guid: "unrelated", category: "mechanisms/doors", icon: false });
  const files = new Map([
    ["/public/catalog.json", JSON.stringify({ schemaVersion: 12, itemCount: oldItems.length, itemChunks: ["catalog/items.0.json"], paletteGroups: [
      { key: "counters/service", itemCount: 1 }, { key: "counters/service", itemCount: 1 },
      { key: "mechanisms/doors", itemCount: 1 }, { key: "mechanisms/doors", itemCount: 1 },
    ] })],
    ["/public/catalog/items.0.json", JSON.stringify({ schemaVersion: 12, index: 0, itemCount: oldItems.length, items: oldItems })],
  ]);
  let writes = 0;
  vm.runInNewContext(`${generatorConstants}\n${hordeUpdater}\nupdateHordeOnly();`, {
    path, repoRoot: "/", HORDE_PREFAB_ROOT: nativeRoot, OUT_DIR: "/public", DIST_DIR: "/dist", SCHEMA_VERSION: 12, DRY_RUN: false,
    walkPrefabs: (_dir, _base, out) => out.push(...native.map((item) => ({ ...item }))),
    console: { log() {} },
    fs: { existsSync: (name) => files.has(name), readFileSync: (name) => { assert(files.has(name), name); return files.get(name); },
      writeFileSync: (name, text) => { files.set(name, text); writes++; } },
  });
  const groups = JSON.parse(files.get("/public/catalog.json")).paletteGroups;
  assert.deepEqual(groups.map(({ key, itemCount }) => [key, itemCount]), duplicateId ? [["mechanisms/doors", 1], ["horde", 2]] : [["horde", 2]]);
  assert.equal(writes, 2);
}
console.log("PASS catalog delta fixtures: emptied groups removed, duplicate groups collapsed, shared IDs counted once.");
const beforeCatalogs = process.argv.includes("--update-catalog") ? catalogDirs.map(catalogSnapshot) : null;
if (beforeCatalogs) {
  const result = spawnSync(process.execPath, [path.join(root, "layout-editor/scripts/build-catalog.mjs"), "--horde-only"], { encoding: "utf8" });
  process.stdout.write(result.stdout ?? "");
  process.stderr.write(result.stderr ?? "");
  assert.equal(result.status, 0, "Horde incremental catalog generation failed");
}
const catalogs = catalogDirs.map(catalogSnapshot);
for (const [index, { manifest, chunks, items, hashes }] of catalogs.entries()) {
  assert.equal(manifest.schemaVersion, 12);
  assert.equal(manifest.itemCount, 3708);
  assert.equal(items.length, 3708);
  assert.equal(new Set(manifest.paletteGroups.map((group) => group.key)).size, manifest.paletteGroups.length);
  assert(manifest.paletteGroups.every((group) => group.itemCount > 0));
  assert.deepEqual(manifest.paletteGroups.find((group) => group.key === "horde"), {
    key: "horde", labelZh: "核心 · 敌群", labelEn: "Horde", layoutTier: "core", itemCount: 2,
  });
  for (const [id, guid, kind] of [["Service_Window", "23525467a89357149ac283bdbeb11d1e", "ServingStation"], ["gate", "9082ea11251999449830958b76c907cd", "Lockable"]]) {
    const matches = items.filter((item) => item.guid === guid);
    assert.equal(matches.length, 1);
    const item = matches[0];
    assert.equal(item.id, id);
    assert.equal(item.assetPath, `${nativeRoot}/${id}.prefab`);
    assert.equal(item.category, "horde");
    assert.equal(item.defaultParent, "Design/Counters");
    assert.equal(item.layoutTier, "core");
    assert.equal(item.customStubKind, kind);
    assert.deepEqual(item.footprint, { cellsX: 2, cellsZ: 1 });
    assert(!item.needsStub);
  }
  assert.equal(items.filter((item) => item.category === "horde").length, 2);
  chunks.forEach((chunk, chunkIndex) => {
    assert.equal(chunk.schemaVersion, 12);
    assert.equal(chunk.index, chunkIndex);
    assert.equal(chunk.itemCount, chunk.items.length);
  });
  const before = beforeCatalogs?.[index];
  if (before) {
    assert.equal(before.items.filter((item) => !nativeGuids.has(item.guid)).length, 3706);
    assert.deepEqual(items, before.items.map((item) => nativeGuids.has(item.guid) ? { ...item, category: "horde" } : item));
    assert.deepEqual({ ...manifest, paletteGroups: undefined }, { ...before.manifest, paletteGroups: undefined });
    assert.deepEqual(manifest.paletteGroups.filter((group) => !affectedCategories.has(group.key)), before.manifest.paletteGroups.filter((group) => !affectedCategories.has(group.key)));
    for (const category of affectedCategories) {
      const count = (list) => new Set(list.filter((item) => item.category === category).map((item) => item.id)).size;
      const oldCount = before.manifest.paletteGroups.find((group) => group.key === category)?.itemCount ?? 0;
      const nextCount = manifest.paletteGroups.find((group) => group.key === category)?.itemCount ?? 0;
      assert.equal(nextCount, oldCount + count(items) - count(before.items));
      console.log(`PASS ${catalogDirs[index]}: ${category} ${oldCount} -> ${nextCount}`);
    }
    assert.deepEqual(Object.keys(hashes), Object.keys(before.hashes));
    const allowedFiles = new Set(["catalog.json", ...before.manifest.itemChunks.filter((_, i) => before.chunks[i].items.some((item) => nativeGuids.has(item.guid)))]);
    for (const name of Object.keys(hashes)) if (!allowedFiles.has(name)) assert.equal(hashes[name], before.hashes[name], `Unrelated JSON changed: ${name}`);
    chunks.forEach((chunk, i) => assert.deepEqual({ ...chunk, items: undefined }, { ...before.chunks[i], items: undefined }));
    console.log(`PASS ${catalogDirs[index]}: 3706 unrelated entries unchanged; native entries changed only category; unrelated JSON unchanged.`);
  }
}
for (const name of ["catalog.json", ...catalogs[0].manifest.itemChunks]) assert.equal(catalogs[0].hashes[name], catalogs[1].hashes[name], `public/dist mismatch: ${name}`);
console.log("PASS catalogs: schema 12, 3708 entries, standalone Horde group, unique nonempty groups, public/dist byte-identical.");
if (process.argv.includes("--catalog-only")) process.exit(0);

const ctx = { exports: {}, Number, Math };
vm.runInNewContext(ts.transpileModule(read("layout-editor/web/src/editor/hordeStub.ts"), {
  compilerOptions: { module: ts.ModuleKind.CommonJS },
}).outputText, ctx);
const { normalizeHordeStub, normalizeUnlockCost, nativeHordeStubKind } = ctx.exports;
const item = { prefabAssetPath: "Assets/common04/game_environment/horde/prefabs/Service_Window.prefab", prefabGuid: "", servingStation: {
  plateReturnInstanceIds: ["a", "b"], orderUIOffset: { x: 1.25, y: -3, z: 4.5 }, healthUIOffset: { x: 7, y: 8, z: -9 },
} };
const expected = JSON.stringify(item.servingStation);
for (let i = 0; i < 20; i++) { normalizeHordeStub(item); Object.assign(item, JSON.parse(JSON.stringify(item))); }
assert.equal(JSON.stringify(item.servingStation), expected);
const ordinary = { prefabAssetPath: "Assets/common01/prefabs/counters/ServingStation.prefab", prefabGuid: "", stubKind: "ServingStation", servingStation: { plateReturnInstanceIds: ["a"] } };
const before = JSON.stringify(ordinary);
normalizeHordeStub(ordinary);
assert.equal(JSON.stringify(ordinary), before);
assert.equal(nativeHordeStubKind({ prefabAssetPath: "Assets/commonW2/prefabs/art/gate.prefab", prefabGuid: "old" }), "");
assert.equal(nativeHordeStubKind({ ...ordinary, prefabGuid: "23525467a89357149ac283bdbeb11d1e" }), "", "ordinary path wins over stale Horde GUID");
const converted = { ...item, prefabGuid: "old-normal-guid" };
normalizeHordeStub(converted);
assert.equal(converted.prefabGuid, "23525467a89357149ac283bdbeb11d1e", "Horde path repairs old ordinary GUID");
assert.equal(JSON.stringify(converted.servingStation), expected, "identity repair preserves offsets and bindings");
for (const [value, cost] of [[undefined, 100], [NaN, 100], [-9, 0], [0, 0], [12.7, 13], [2147483647, 2147483647], [1e30, 2147483647], [Infinity, 2147483647], [-Infinity, 0]]) {
  assert.equal(normalizeUnlockCost(value), cost);
}
const defaults = { prefabGuid: "9082ea11251999449830958b76c907cd", prefabAssetPath: "" };
normalizeHordeStub(defaults);
assert.equal(defaults.lockable.unlockCost, 100);
const empty = { prefabGuid: "23525467a89357149ac283bdbeb11d1e", prefabAssetPath: "", servingStation: { orderUIOffset: null } };
normalizeHordeStub(empty);
assert.equal(JSON.stringify(empty.servingStation.orderUIOffset), '{"x":0,"y":0,"z":0}');

function functionsFrom(file, names) {
  const text = read(file);
  const source = ts.createSourceFile(file, text, ts.ScriptTarget.Latest, true);
  return source.statements.filter((n) => ts.isFunctionDeclaration(n) && names.includes(n.name?.text)).map((n) => n.getText(source)).join("\n");
}
const inputs = new Map();
const ui = { exports: {}, nativeHordeStubKind, normalizeHordeStub, normalizeUnlockCost,
  S: { paramLabels: new Map(), items: [] }, STUB_KIND_BY_PREFAB_ID: {},
  computeParamLabels: () => {}, computeReturnLabels: () => new Map(),
  servingPlateReturn: () => "", servingGlassReturn: () => "", servingMugReturn: () => "", servingTrayReturn: () => "", servingTeacupReturn: () => "",
  plateReturns: () => [], glassReturns: () => [], mugReturns: () => [], trayReturns: () => [], teacupReturns: () => [],
  isHotpotBurnerItem: () => false, isButtonLinkSource: () => false, wireCounterAppearance: () => {},
  pushHistory: () => {}, setStatus: () => {}, draw: () => {},
  document: { getElementById: (id) => inputs.get(id) ?? null },
};
vm.runInNewContext(ts.transpileModule(functionsFrom("layout-editor/web/src/editor/stubControls.ts", ["stubKindOf", "stubControlsHtml", "wireStubControls"]), {
  compilerOptions: { module: ts.ModuleKind.CommonJS },
}).outputText, ui);
const html = ui.exports.stubControlsHtml(item);
for (const field of ["orderUIOffset", "healthUIOffset"]) for (const axis of ["x", "y", "z"]) {
  const id = `ctx-ss-${field}-${axis}`;
  assert(html.includes(`id="${id}"`));
  inputs.set(id, { addEventListener: (_event, handler) => inputs.get(id).handler = handler });
}
ui.exports.wireStubControls(item);
inputs.get("ctx-ss-healthUIOffset-z").handler({ target: { valueAsNumber: -12.5 } });
assert.equal(item.servingStation.healthUIOffset.z, -12.5);
assert(ui.exports.stubControlsHtml(defaults).includes('id="ctx-lockable-cost"'));
inputs.clear();
inputs.set("ctx-lockable-cost", { addEventListener: (_event, handler) => inputs.get("ctx-lockable-cost").handler = handler });
ui.exports.wireStubControls(defaults);
inputs.get("ctx-lockable-cost").handler({ target: { valueAsNumber: 1e30 } });
assert.equal(defaults.lockable.unlockCost, 2147483647);
const links = { exports: {} };
vm.runInNewContext(ts.transpileModule(functionsFrom("layout-editor/web/src/editor/servingLinks.ts", ["setServingReturns"]), {
  compilerOptions: { module: ts.ModuleKind.CommonJS },
}).outputText, links);
links.exports.setServingReturns(item, ["c"]);
assert.equal(item.servingStation.healthUIOffset.z, -12.5);
// Restore the fixture before checking serializer round trips.
item.servingStation = JSON.parse(expected);
console.log("PASS controls: six offset inputs, actual change handlers, clamped cost, binding edits preserve offsets.");

const serialize = read("layout-editor/web/src/editor/serialize.ts");
const source = ts.createSourceFile("serialize.ts", serialize, ts.ScriptTarget.Latest, true);
const serializeFunction = source.statements.find((n) => ts.isFunctionDeclaration(n) && n.name?.text === "serializeItemForDoc");
const scope = { exports: {}, normalizeHordeStub, S: { catalogByGuid: new Map() }, isResizableBackgroundItem: () => false,
  resolveFootprint: () => ({ cellsX: 2, cellsZ: 1 }), STUB_KIND_BY_PREFAB_ID: {}, prefabIdFromPath: (s) => s.split("/").pop()?.replace(/\.prefab$/, ""),
  stripMisplacedSpeedControl: () => {}, stripUnconfiguredSpeedControl: () => {}, partLevelActive: () => false,
  editorItemUnityWorldXZ: (it) => ({ x: it._wx, z: it._wz }),
};
vm.runInNewContext(ts.transpileModule(serializeFunction.getText(source), { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, scope);
let saved = { ...item, _wx: 1, _wz: 2, _parentWx: 0, _parentWz: 0, localPosition: { x: 1, y: 0, z: 2 } };
for (let i = 0; i < 20; i++) {
  saved = { ...saved, ...JSON.parse(JSON.stringify(scope.exports.serializeItemForDoc(saved))) };
  assert.equal(JSON.stringify(saved.servingStation), expected);
  assert.equal(saved.prefabGuid, "23525467a89357149ac283bdbeb11d1e");
  assert.equal(saved.prefabAssetPath, `${nativeRoot}/Service_Window.prefab`);
}
const convertedNormal = { ...saved, prefabAssetPath: ordinary.prefabAssetPath, prefabGuid: "fb4883750ab23964fa3d000c2b455b38" };
const normalSaved = scope.exports.serializeItemForDoc(convertedNormal);
assert.equal(normalSaved.prefabAssetPath, ordinary.prefabAssetPath);
assert.equal(normalSaved.prefabGuid, convertedNormal.prefabGuid);
assert.equal(JSON.stringify(normalSaved.servingStation), expected, "normal serialization does not strip station fields");
console.log("PASS frontend: native defaults, ordinary/decor isolation, Int32 boundaries, 20 actual serialize/JSON round trips.");

const src = read("Assets/Editor/LayoutEditor/LayoutEditorStubIO.cs");
function blockAt(text, start) {
  const tokens = /@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|\/\/[^\n]*|\/\*[\s\S]*?\*\/|[{}]/g;
  tokens.lastIndex = text.indexOf("{", start);
  let depth = 0;
  for (let m; (m = tokens.exec(text));) {
    if (m[0] === "{") depth++;
    if (m[0] === "}" && --depth === 0) return text.slice(start, tokens.lastIndex);
  }
  throw Error("Unbalanced block");
}
const methods = ["EnsureServingStationStub", "PromoteDerivedStub", "GetDerivedStubs", "EnsureDerivedStubData", "ResolvePrefabBasePseudoPrefabSO", "FindRuntimeType", "EnsureDerivedRuntimeForStubs", "FindExactBaseStub", "FindExactBaseRuntime"];
const extracted = methods.map((name) => {
  const m = new RegExp("    (?:private|internal) static [^\\n]+ " + name + "\\(").exec(src);
  assert(m, name);
  return blockAt(src, m.index);
}).join("\n");
const apply = ["ServingStation", "Lockable"].map((kind) => blockAt(src, src.indexOf('        if (item.stubKind == "' + kind + '")', src.indexOf("public static void ApplyStub")))).join("\n");
const exportStart = src.indexOf("        var hordeServing =", src.indexOf("public static void ExportStub"));
const exportEnd = src.indexOf("        if (ExportWebBackpackIfPresent", exportStart);
const harness = read("layout-editor/scripts/horde-stub-harness.txt").replace("__METHODS__", extracted).replace("__APPLY__", apply).replace("__EXPORT__", src.slice(exportStart, exportEnd));
const dir = fs.mkdtempSync(path.join(os.tmpdir(), "horde-stub-check-"));
try {
  fs.writeFileSync(path.join(dir, "Program.cs"), harness);
  fs.writeFileSync(path.join(dir, "check.csproj"), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup></Project>');
  const result = spawnSync("dotnet", ["run", "--project", path.join(dir, "check.csproj"), "--verbosity", "quiet"], { encoding: "utf8", timeout: 120000 });
  process.stdout.write(result.stdout ?? "");
  process.stderr.write(result.stderr ?? "");
  assert.equal(result.status, 0, "C# production method harness failed");
} finally {
  fs.rmSync(dir, { recursive: true, force: true });
}
