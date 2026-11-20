import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createServer } from "node:http";
import { createRequire } from "node:module";
import { test } from "node:test";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";
import { handleSettingsTool, settingsToolDefinitions } from "../dist/level-settings.js";
import { handleModeTool } from "../dist/level-mode.js";

const path = "Assets/LevelSets/test/data/one/LevelInfo_one.asset";
const a = "a".repeat(32), b = "b".repeat(32);
const respawn = "oc2_save_level_respawn_bounds", sequence = "oc2_save_native_scripted_recipes";
const player = () => ({ exists: true, orderLifeTime: 99, timeBetweenOrders: 7, plateReturnTime: 4, survivalTimeMultiplier: 1.25, roundTime: 300, oneStarScore: 100, twoStarScore: 200, threeStarScore: 300, fourStarScore: 400, targetRepairCostMax: 123, scriptedRecipes: [a, "", b], scriptedRecipeErrors: ["2: missing"] });
const detail = (gameMode = "Campaign") => ({ levelInfoAssetPath: path, gameMode, levelName: "One", levelNameZH: "一", debugRecipeCount: 6, disableDynamicParenting: true, enableRespawnBounds: true, minOrderCount: 2, maxOrderCount: 4, gridHalfSizeX: 12, gridHalfSizeY: 8, gridHalfSizeZ: 13, dependencies: ["bundle1"], configs: Array.from({ length: 4 }, player) });
function bridgeFor(before = detail(), after, failure) {
  const calls = [];
  let stored = structuredClone(before);
  return {
    calls,
    async get(url) { calls.push(["GET", url]); return structuredClone(calls.length === 1 ? before : after === undefined ? stored : after); },
    async post(url, body) {
      calls.push(["POST", url, structuredClone(body)]);
      if (failure) throw new Error("lost response");
      if (url === "/api/level/info") stored = { ...stored, ...body };
      else for (let p = 1; p <= 4; p++) if (body[`config_${p}p`]) stored.configs[p - 1] = { ...stored.configs[p - 1], ...body[`config_${p}p`], scriptedRecipeErrors: [] };
      return { ok: true };
    },
  };
}

test("respawn saves both booleans in every mode with full latest info scalars", async () => {
  for (const mode of ["Campaign", "Dynamic", "Horde"]) for (const flag of [true, false]) {
    const before = detail(mode), bridge = bridgeFor(before);
    assert.equal((await handleSettingsTool(respawn, { assetPath: path, enableRespawnBounds: flag, confirm: true }, bridge)).verified, true);
    assert.deepEqual(bridge.calls.map((c) => c[0]), ["GET", "POST", "GET"]);
    const { levelInfoAssetPath, gameMode, configs, enableRespawnBounds, ...scalars } = before;
    assert.deepEqual(bridge.calls[1], ["POST", "/api/level/info", { assetPath: path, ...scalars, updateEnableRespawnBounds: true, enableRespawnBounds: flag }]);
    assert.equal(before.enableRespawnBounds, true);
  }
});

test("native sequences merge only selected player scalars and preserve duplicates, order and explicit clear", async () => {
  for (let p = 1; p <= 4; p++) for (const guids of [[b.toUpperCase(), a, b], []]) {
    const before = detail(), bridge = bridgeFor(before);
    const result = await handleSettingsTool(sequence, { assetPath: path, playerCount: p, scriptedRecipes: guids, confirm: true }, bridge);
    assert.equal(result.verified, true);
    const { exists, scriptedRecipes, scriptedRecipeErrors, ...scalars } = before.configs[p - 1];
    assert.deepEqual(bridge.calls[1], ["POST", "/api/level/config", { assetPath: path, [`config_${p}p`]: { ...scalars, scriptedRecipes: guids } }]);
    assert.deepEqual(before.configs[p - 1].scriptedRecipes, [a, "", b]);
    assert.deepEqual(bridge.calls.map((c) => c[0]), ["GET", "POST", "GET"]);
  }
});

test("strict argument validation rejects unknown keys, absent/null arrays, coercions and missing confirmation before HTTP", async () => {
  const good = { assetPath: path, playerCount: 1, scriptedRecipes: [a], confirm: true };
  const cases = [
    ...[null, undefined, {}, "abc", [""], [null], [1], ["g".repeat(32)]].map((scriptedRecipes) => [sequence, { ...good, scriptedRecipes }]),
    ...[null, "1", true, 0, 5, 1.5].map((playerCount) => [sequence, { ...good, playerCount }]),
    ...[null, undefined, "false", 0, 1, []].map((enableRespawnBounds) => [respawn, { assetPath: path, enableRespawnBounds, confirm: true }]),
    ...[false, "true", 1, undefined].map((confirm) => [sequence, { ...good, confirm }]),
    [sequence, { ...good, scriptedRecipeErrors: [] }], [sequence, { ...good, extra: true }],
    [sequence, { ...good, assetPath: "Assets/scene.unity" }], [respawn, { ...good, enableRespawnBounds: false }],
  ];
  for (const [name, input] of cases) {
    const bridge = bridgeFor();
    await assert.rejects(handleSettingsTool(name, input, bridge), (e) => e.code === "INVALID_ARGUMENT" && e.writeOutcome === "not_started");
    assert.equal(bridge.calls.length, 0);
  }
  for (const name of ["oc2_get_level_mode_config", "oc2_change_level_mode", "oc2_create_level", "oc2_save_level_mode_config"]) {
    const bridge = bridgeFor();
    await assert.rejects(handleModeTool(name, { ...good, typo: true }, bridge), (e) => e.code === "INVALID_ARGUMENT");
    assert.equal(bridge.calls.length, 0);
  }
});

test("non Campaign and missing player/config scalars never POST or echo dormant arrays", async () => {
  for (const mode of ["Dynamic", "Horde"]) for (const guids of [[], [a]]) {
    const bridge = bridgeFor(detail(mode));
    await assert.rejects(handleSettingsTool(sequence, { assetPath: path, playerCount: 1, scriptedRecipes: guids, confirm: true }, bridge), (e) => e.code === "INVALID_ARGUMENT");
    assert.equal(bridge.calls.length, 1);
  }
  for (const mutate of [(d) => { d.configs[0].exists = false; }, (d) => { delete d.configs[0].roundTime; }, (d) => { d.levelInfoAssetPath = "other"; }]) {
    const value = detail(); mutate(value);
    const bridge = bridgeFor(value);
    await assert.rejects(handleSettingsTool(sequence, { assetPath: path, playerCount: 1, scriptedRecipes: [], confirm: true }, bridge), (e) => e.writeOutcome === "not_started");
    assert.equal(bridge.calls.filter((c) => c[0] === "POST").length, 0);
  }
});

test("unconfirmed writes and mismatched/broken readbacks report outcome and never retry POST", async () => {
  const input = { assetPath: path, playerCount: 2, scriptedRecipes: [b, a, b], confirm: true };
  const failed = bridgeFor(detail(), undefined, true);
  await assert.rejects(handleSettingsTool(sequence, input, failed), (e) => e.code === "WRITE_UNCONFIRMED" && e.writeOutcome === "unknown");
  assert.deepEqual(failed.calls.map((c) => c[0]), ["GET", "POST"]);
  for (const guids of [[a, b, b], [b, a], [b, "", b], [b, a, b]]) {
    const after = detail(); after.configs[1].scriptedRecipes = guids;
    // Even matching GUIDs must not conceal reference diagnostics.
    const bridge = bridgeFor(detail(), after);
    await assert.rejects(handleSettingsTool(sequence, input, bridge), (e) => e.code === "READBACK_FAILED" && e.writeOutcome === "acknowledged");
    assert.equal(bridge.calls.filter((c) => c[0] === "POST").length, 1);
  }
  const bridge = bridgeFor(detail(), detail());
  await assert.rejects(handleSettingsTool(respawn, { assetPath: path, enableRespawnBounds: false, confirm: true }, bridge), (e) => e.code === "READBACK_FAILED");
});

test("settings schemas and manifest expose identical required types and confirmation", () => {
  const require = createRequire(import.meta.url);
  const sdkRequire = createRequire(require.resolve("@modelcontextprotocol/sdk/server/index.js"));
  const Ajv = sdkRequire("ajv/dist/2020.js").default;
  const ajv = new Ajv({ strict: false });
  const manifest = JSON.parse(readFileSync(new URL("../tool-manifest.json", import.meta.url), "utf8"));
  for (const tool of settingsToolDefinitions) {
    const valid = tool.name === respawn ? { assetPath: path, enableRespawnBounds: false, confirm: true } : { assetPath: path, playerCount: 4, scriptedRecipes: [a, a], confirm: true };
    const validate = ajv.compile(tool.inputSchema);
    assert.equal(validate(valid), true, JSON.stringify(validate.errors));
    assert.equal(validate({ ...valid, confirm: "true" }), false);
    assert.equal(validate({ ...valid, unknown: true }), false);
    const entry = manifest.tools.find((t) => t.name === tool.name);
    assert.equal(entry.sideEffect, "write_asset");
    assert.equal(entry.requiresConfirmation, true);
    assert.equal(entry.safeToRetry, false);
    assert.notEqual(entry.stdioOnly, true);
  }
});

test("real stdio exposes both settings tools, executes GET POST GET, and returns nonretryable readback failures", async (t) => {
  let stored = detail(), corrupt = false, failAfterSave = false;
  const calls = [];
  const server = createServer(async (req, res) => {
    let text = ""; for await (const chunk of req) text += chunk;
    calls.push([req.method, req.url]); res.setHeader("Content-Type", "application/json");
    if (req.method === "POST") {
      const body = JSON.parse(text);
      if (req.url === "/api/level/info") stored.enableRespawnBounds = body.enableRespawnBounds;
      else stored.configs[2] = { ...stored.configs[2], ...body.config_3p, scriptedRecipeErrors: [] };
      if (failAfterSave) corrupt = true;
      res.end('{"ok":true}');
    } else res.end(JSON.stringify(corrupt ? null : stored));
  });
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  t.after(() => { server.closeAllConnections(); server.close(); });
  const transport = new StdioClientTransport({ command: process.execPath, args: [new URL("../dist/server.js", import.meta.url).pathname], env: { ...process.env, OC2_EDITOR_BRIDGE_URL: `http://127.0.0.1:${server.address().port}` }, stderr: "pipe" });
  const client = new Client({ name: "settings-test", version: "1" });
  t.after(() => client.close()); await client.connect(transport);
  const tools = (await client.listTools()).tools;
  for (const definition of settingsToolDefinitions) assert.deepEqual(tools.find((tool) => tool.name === definition.name).inputSchema, definition.inputSchema);
  for (const [name, input] of [[respawn, { enableRespawnBounds: false }], [sequence, { playerCount: 3, scriptedRecipes: [b, a, b] }]]) {
    const result = await client.callTool({ name, arguments: { assetPath: path, ...input, confirm: true } });
    assert.equal(result.structuredContent.ok, true);
    assert.equal(result.structuredContent.data.verified, true);
    assert.equal(result.structuredContent.resource.assetPath, path);
  }
  assert.deepEqual(calls.map((c) => c[0]), ["GET", "POST", "GET", "GET", "POST", "GET"]);
  // Fail the read after an acknowledged save, using the same actual stdio handler.
  failAfterSave = true;
  const failure = await client.callTool({ name: respawn, arguments: { assetPath: path, enableRespawnBounds: true, confirm: true } });
  assert.equal(failure.isError, true);
  assert.equal(failure.structuredContent.error.code, "READBACK_FAILED");
  assert.equal(failure.structuredContent.error.retryable, false);
  assert.equal(failure.structuredContent.operation.sideEffect, "write_asset");
  assert.equal(failure.structuredContent.data.writeOutcome, "acknowledged");
  assert.equal(calls.filter((c) => c[0] === "POST").length, 3);
});
