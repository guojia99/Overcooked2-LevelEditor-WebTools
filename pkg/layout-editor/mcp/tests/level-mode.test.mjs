import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createServer } from "node:http";
import { createRequire } from "node:module";
import { test } from "node:test";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";
import { BridgeClient, BridgeError } from "../dist/bridge-client.js";
import { handleModeTool, ModeToolError, modeToolDefinitions } from "../dist/level-mode.js";

const path = "Assets/LevelSets/test/data/one/LevelInfo_one.asset";
const recipe = "a".repeat(32);
const enemy = "b".repeat(32);
const config = (gameMode = "Dynamic") => ({
  assetPath: path, gameMode, targetHealth: 100, targetRepairSpeed: 0.1,
  targetRepairThreshold: 25, kitchenHealth: 1000, minOrderCount: 1, maxOrderCount: 3,
  phases: gameMode === "Dynamic" ? [1, 2].map((n) => ({ id: `phase_${n}`, durationSeconds: 60, recipeGuids: [recipe] })) : null,
  hordeConfigs: gameMode === "Horde" ? [1, 2, 3, 4].map((n) => ({ targetRepairCostMax: n * 100, waves: [{ id: "wave_1", intervalSeconds: 5, recipeGuids: [recipe], enemyGuids: [enemy, enemy], spawnTimes: [0, 0] }] })) : null,
  enemies: [{ guid: enemy, id: "unbread_a", nameZh: "面包", nameEn: "Unbread", assetPath: "Assets/common04/game_environment/horde/enemies/unbread_a.asset" }],
});

function fakeBridge(getValue, postValue = { ok: true }) {
  const calls = [];
  return {
    calls,
    async get(url) { calls.push(["GET", url]); return typeof getValue === "function" ? getValue(url) : structuredClone(getValue); },
    async post(url, body) { calls.push(["POST", url, body]); return typeof postValue === "function" ? postValue(url, body) : postValue; },
  };
}

test("mode read uses encoded LevelInfo path and never writes", async () => {
  const bridge = fakeBridge(config());
  assert.deepEqual(await handleModeTool("oc2_get_level_mode_config", { assetPath: path }, bridge), config());
  assert.deepEqual(bridge.calls, [["GET", `/api/level/mode-config?assetPath=${encodeURIComponent(path)}`]]);
});

for (const mode of ["Dynamic", "Horde"]) {
  test(`${mode} save strips catalog, preserves IDs/duplicates/player order and verifies readback`, async () => {
    const expected = config(mode);
    const before = structuredClone(expected);
    const actual = structuredClone(expected);
    actual.targetRepairSpeed = Math.fround(actual.targetRepairSpeed);
    const bridge = fakeBridge(actual);
    const data = await handleModeTool("oc2_save_level_mode_config", { config: expected, confirm: true }, bridge);
    assert.equal(data.verified, true);
    assert.equal(bridge.calls.length, 2);
    assert.equal(bridge.calls[0][0], "POST");
    assert.equal(bridge.calls[0][1], "/api/level/mode-config");
    assert.equal("enemies" in bridge.calls[0][2], false);
    assert.deepEqual(expected, before);
    if (mode === "Horde") {
      assert.deepEqual(bridge.calls[0][2].hordeConfigs.map((p) => p.targetRepairCostMax), [100, 200, 300, 400]);
      assert.deepEqual(bridge.calls[0][2].hordeConfigs[0].waves[0].enemyGuids, [enemy, enemy]);
    }
  });
}

test("invalid configurations fail before HTTP", async () => {
  const cases = [
    { ...config(), gameMode: "Campaign" },
    { ...config(), partLevel: { enabled: true } },
    { ...config(), minOrderCount: 4 },
    { ...config(), targetRepairSpeed: Infinity },
    { ...config(), targetHealth: 1.5 },
    { ...config(), phases: [config().phases[0]] },
    { ...config(), phases: [config().phases[0], config().phases[0]] },
    { ...config(), phases: [{ ...config().phases[0], durationSeconds: 0 }, config().phases[1]] },
    { ...config(), phases: [{ ...config().phases[0], recipeGuids: ["invalid"] }, config().phases[1]] },
    { ...config("Horde"), hordeConfigs: config("Horde").hordeConfigs.slice(0, 3) },
    { ...config("Horde"), phases: config().phases },
  ];
  const unequal = config("Horde");
  unequal.hordeConfigs[0].waves[0].spawnTimes = [0];
  cases.push(unequal);
  const excessive = config("Horde");
  for (const player of excessive.hordeConfigs) player.waves = Array.from({ length: 128 }, (_, i) => ({ ...player.waves[0], id: `w${i}`, recipeGuids: Array(256).fill(recipe) }));
  cases.push(excessive);
  for (const value of cases) {
    const bridge = fakeBridge(null);
    await assert.rejects(handleModeTool("oc2_save_level_mode_config", { config: value, confirm: true }, bridge), (e) => e instanceof ModeToolError && e.code === "INVALID_ARGUMENT");
    assert.equal(bridge.calls.length, 0);
  }
});

test("large valid Horde data preserves UTF-8 IDs and stays within the byte budget", async () => {
  const value = config("Horde");
  for (const player of value.hordeConfigs) player.waves = Array.from({ length: 128 }, (_, i) => ({ ...player.waves[0], id: `${i}${"中".repeat(120)}`, recipeGuids: Array(110).fill(recipe) }));
  // Exercise the total-entry boundary with multibyte IDs.
  for (const player of value.hordeConfigs) player.waves.splice(64);
  for (const player of value.hordeConfigs) for (const wave of player.waves) wave.recipeGuids = Array(125).fill(recipe);
  value.enemies = null;
  const bridge = fakeBridge(value);
  assert.equal((await handleModeTool("oc2_save_level_mode_config", { config: value, confirm: true }, bridge)).verified, true);
  assert.ok(Buffer.byteLength(JSON.stringify(value), "utf8") > JSON.stringify(value).length);
  assert.ok(Buffer.byteLength(JSON.stringify(bridge.calls[0][2]), "utf8") <= 2097152);
});

test("manifest and mode input schemas validate actual DTOs and reject mixed modes", () => {
  const require = createRequire(import.meta.url);
  const sdkRequire = createRequire(require.resolve("@modelcontextprotocol/sdk/server/index.js"));
  const Ajv = sdkRequire("ajv/dist/2020.js").default;
  const ajv = new Ajv({ strict: false });
  const schema = JSON.parse(readFileSync(new URL("../schemas/level-mode.schema.json", import.meta.url), "utf8"));
  const validate = ajv.compile(schema);
  assert.equal(validate(config()), true, JSON.stringify(validate.errors));
  assert.equal(validate(config("Horde")), true, JSON.stringify(validate.errors));
  assert.equal(validate({ ...config(), phases: null }), false);
  assert.equal(validate({ ...config("Horde"), hordeConfigs: [] }), false);
  assert.equal(validate({ ...config(), hordeConfigs: config("Horde").hordeConfigs }), false);
  const manifest = JSON.parse(readFileSync(new URL("../tool-manifest.json", import.meta.url), "utf8"));
  const manifestSchema = JSON.parse(readFileSync(new URL("../schemas/tool-manifest.schema.json", import.meta.url), "utf8"));
  const validateManifest = ajv.compile(manifestSchema);
  assert.equal(validateManifest(manifest), true, JSON.stringify(validateManifest.errors));
  for (const tool of modeToolDefinitions) ajv.compile(tool.inputSchema);
  const save = ajv.compile(modeToolDefinitions.find((t) => t.name === "oc2_save_level_mode_config").inputSchema);
  assert.equal(save({ config: config("Campaign"), confirm: true }), false);
  const changeTool = modeToolDefinitions.find((t) => t.name === "oc2_change_level_mode");
  assert.equal(changeTool.inputSchema.properties.convertHordeObjects.default, false);
  const change = ajv.compile(changeTool.inputSchema);
  for (const flag of [undefined, false, true]) {
    assert.equal(change({ assetPath: path, gameMode: "Horde", confirm: true, ...(flag === undefined ? {} : { convertHordeObjects: flag }) }), true);
  }
  for (const flag of [null, "true", "false", 0, 1]) {
    assert.equal(change({ assetPath: path, gameMode: "Horde", confirm: true, convertHordeObjects: flag }), false);
  }
  assert.equal(change({ assetPath: path, gameMode: "Horde", convertHordeObjects: true }), false);
});

test("all writes require confirmation and reject unknown game modes", async () => {
  for (const name of modeToolDefinitions.map((t) => t.name).filter((n) => !n.startsWith("oc2_get"))) {
    const bridge = fakeBridge(null);
    await assert.rejects(handleModeTool(name, {}, bridge), /confirm=true/);
    assert.equal(bridge.calls.length, 0);
  }
  await assert.rejects(handleModeTool("oc2_change_level_mode", { assetPath: path, gameMode: "partLevel", confirm: true }, fakeBridge(null)), /gameMode/);
});

test("mode change sends only native DTO and reads mode plus detail", async () => {
  const bridge = fakeBridge((url) => url.startsWith("/api/level?") ? { levelInfoAssetPath: path, gameMode: "Horde" } : config("Horde"));
  const data = await handleModeTool("oc2_change_level_mode", { assetPath: path, gameMode: "Horde", confirm: true }, bridge);
  assert.equal(data.verified, true);
  assert.deepEqual(bridge.calls[0], ["POST", "/api/level/mode", { assetPath: path, gameMode: "Horde", convertHordeObjects: false }]);
  assert.deepEqual(bridge.calls.map((c) => c[0]), ["POST", "GET", "GET"]);
});

test("Horde conversion requires an exact boolean and confirm=true before any HTTP", async () => {
  for (const flag of [null, "true", "false", 0, 1, [], {}]) {
    const bridge = fakeBridge(null);
    await assert.rejects(handleModeTool("oc2_change_level_mode", { assetPath: path, gameMode: "Horde", convertHordeObjects: flag, confirm: true }, bridge), (e) => e.code === "INVALID_ARGUMENT");
    assert.equal(bridge.calls.length, 0);
  }
  for (const confirm of [undefined, false]) {
    const bridge = fakeBridge(null);
    await assert.rejects(handleModeTool("oc2_change_level_mode", { assetPath: path, gameMode: "Campaign", convertHordeObjects: true, confirm }, bridge), /confirm=true/);
    assert.equal(bridge.calls.length, 0);
  }
});

test("stdio mode change sends default/false/true booleans over HTTP in both directions", async (t) => {
  let mode = "Campaign";
  const requests = [];
  const server = createServer(async (req, res) => {
    let text = "";
    for await (const chunk of req) text += chunk;
    res.setHeader("Content-Type", "application/json");
    if (req.method === "POST") {
      const body = JSON.parse(text);
      requests.push({ url: req.url, body });
      mode = body.gameMode;
      res.end('{"ok":true}');
    } else if (req.url.startsWith("/api/level?")) {
      res.end(JSON.stringify({ levelInfoAssetPath: path, gameMode: mode }));
    } else res.end(JSON.stringify(config(mode)));
  });
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  t.after(() => { server.closeAllConnections(); server.close(); });
  const transport = new StdioClientTransport({ command: process.execPath, args: [new URL("../dist/server.js", import.meta.url).pathname], env: { ...process.env, OC2_EDITOR_BRIDGE_URL: `http://127.0.0.1:${server.address().port}` }, stderr: "pipe" });
  const client = new Client({ name: "mode-conversion-test", version: "1" });
  t.after(() => client.close());
  await client.connect(transport);
  for (const gameMode of ["Horde", "Campaign", "Dynamic"]) {
    for (const flag of [undefined, false, true]) {
      const response = await client.callTool({ name: "oc2_change_level_mode", arguments: { assetPath: path, gameMode, confirm: true, ...(flag === undefined ? {} : { convertHordeObjects: flag }) } });
      assert.equal(response.structuredContent.ok, true);
      assert.equal(response.structuredContent.data.verified, true);
      assert.deepEqual(requests.at(-1), { url: "/api/level/mode", body: { assetPath: path, gameMode, convertHordeObjects: flag === true } });
    }
  }
  assert.equal(requests.length, 9);
});

for (const mode of [undefined, "Dynamic", "Horde"]) {
  test(`create ${mode || "default Campaign"} discovers actual path and verifies detail`, async () => {
    const wantedMode = mode || "Campaign";
    const bridge = fakeBridge((url) => url.includes("/sets/") ? { levels: [{ sceneName: "one", assetPath: path }] } : url.startsWith("/api/level?") ? { gameMode: wantedMode, levelInfoAssetPath: path } : config(wantedMode));
    const data = await handleModeTool("oc2_create_level", { setName: "test", levelId: "one", ...(mode ? { gameMode: mode } : {}), confirm: true }, bridge);
    assert.equal(data.verified, true);
    assert.deepEqual(bridge.calls[0], ["POST", "/api/level/create", { setName: "test", levelId: "one", gameMode: wantedMode }]);
    assert.deepEqual(bridge.calls.map((c) => c[0]), ["POST", "GET", "GET", "GET"]);
  });
}

test("write/readback failures are explicit and never repeat POST", async () => {
  for (const post of [() => { throw new BridgeError("offline"); }, { error: "not saved" }, { ok: false }]) {
    const bridge = fakeBridge(config(), post);
    await assert.rejects(handleModeTool("oc2_save_level_mode_config", { config: config(), confirm: true }, bridge), (e) => e.code === "WRITE_UNCONFIRMED" && e.writeOutcome === "unknown");
    assert.equal(bridge.calls.length, 1);
  }
  for (const actual of [null, { ...config(), targetHealth: 99 }, { ...config(), targetRepairSpeed: 0 }, { ...config(), gameMode: "Horde" }, { ...config(), assetPath: "other" }]) {
    const bridge = fakeBridge(actual);
    await assert.rejects(handleModeTool("oc2_save_level_mode_config", { config: config(), confirm: true }, bridge), (e) => e.code === "READBACK_FAILED" && e.writeOutcome === "acknowledged");
    assert.equal(bridge.calls.filter((c) => c[0] === "POST").length, 1);
  }
});

test("readback accepts Unity float JSON precision but rejects a changed repair setting", async () => {
  const value = { ...config(), targetRepairSpeed: 0.123456789 };
  assert.equal((await handleModeTool("oc2_save_level_mode_config", { config: value, confirm: true }, fakeBridge({ ...value, targetRepairSpeed: 0.1234568 }))).verified, true);
  await assert.rejects(handleModeTool("oc2_save_level_mode_config", { config: value, confirm: true }, fakeBridge({ ...value, targetRepairSpeed: 0.12345 })), (e) => e.code === "READBACK_FAILED");
});

test("BridgeClient REST encoding and bounded timeout", async (t) => {
  const requests = [];
  const server = createServer(async (req, res) => {
    let body = "";
    for await (const chunk of req) body += chunk;
    requests.push({ url: req.url, method: req.method, body });
    if (req.url === "/timeout") return;
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({ ok: true }));
  });
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  t.after(() => { server.closeAllConnections(); server.close(); });
  const bridge = new BridgeClient(`http://127.0.0.1:${server.address().port}`);
  assert.deepEqual(await bridge.post("/api/level/mode", { assetPath: path, gameMode: "Horde" }, 1000), { ok: true });
  assert.deepEqual(JSON.parse(requests[0].body), { assetPath: path, gameMode: "Horde" });
  await assert.rejects(bridge.get("/timeout", 25), /无法连接/);
});

test("stdio initialize/list/call expose tools, manifest and write error semantics", async (t) => {
  const transport = new StdioClientTransport({ command: process.execPath, args: [new URL("../dist/server.js", import.meta.url).pathname], stderr: "pipe" });
  const client = new Client({ name: "mode-contract-test", version: "1" });
  t.after(() => client.close());
  await client.connect(transport);
  const { tools } = await client.listTools();
  const manifest = JSON.parse(readFileSync(new URL("../tool-manifest.json", import.meta.url), "utf8"));
  for (const definition of modeToolDefinitions) {
    const tool = tools.find((candidate) => candidate.name === definition.name);
    assert.ok(tool);
    assert.deepEqual(tool.inputSchema, definition.inputSchema);
    const entry = manifest.tools.find((candidate) => candidate.name === definition.name);
    assert.notEqual(entry.stdioOnly, true);
    assert.equal(entry.safeToRetry, definition.name.startsWith("oc2_get"));
  }
  assert.ok(tools.some((tool) => tool.name === "oc2_get_scene_layout"));
  assert.ok(tools.some((tool) => tool.name === "oc2_start_export"));
  const response = await client.callTool({ name: "oc2_save_level_mode_config", arguments: { config: config(), confirm: false } });
  assert.equal(response.isError, true);
  assert.equal(response.structuredContent.operation.sideEffect, "write_asset");
  assert.equal(response.structuredContent.error.retryable, false);
  assert.equal(response.structuredContent.data.writeOutcome, "not_started");
});

test("stdio write calls real REST mock and returns verified readback or non-retryable failure", async (t) => {
  let stored = config();
  let failRead = false;
  const calls = [];
  const server = createServer(async (req, res) => {
    let text = "";
    for await (const chunk of req) text += chunk;
    calls.push([req.method, req.url]);
    res.setHeader("Content-Type", "application/json");
    if (req.method === "POST") { stored = JSON.parse(text); res.end('{"ok":true}'); }
    else if (failRead) { res.statusCode = 500; res.end('{"error":"read failed"}'); }
    else res.end(JSON.stringify({ ...stored, enemies: config().enemies }));
  });
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  t.after(() => { server.closeAllConnections(); server.close(); });
  const transport = new StdioClientTransport({ command: process.execPath, args: [new URL("../dist/server.js", import.meta.url).pathname], env: { ...process.env, OC2_EDITOR_BRIDGE_URL: `http://127.0.0.1:${server.address().port}` }, stderr: "pipe" });
  const client = new Client({ name: "mode-rest-test", version: "1" });
  t.after(() => client.close());
  await client.connect(transport);
  const success = await client.callTool({ name: "oc2_save_level_mode_config", arguments: { config: config(), confirm: true } });
  assert.equal(success.structuredContent.ok, true);
  assert.equal(success.structuredContent.data.verified, true);
  assert.equal(success.structuredContent.resource.assetPath, path);
  failRead = true;
  const failure = await client.callTool({ name: "oc2_save_level_mode_config", arguments: { config: config(), confirm: true } });
  assert.equal(failure.isError, true);
  assert.equal(failure.structuredContent.error.code, "READBACK_FAILED");
  assert.equal(failure.structuredContent.error.retryable, false);
  assert.equal(failure.structuredContent.data.writeOutcome, "acknowledged");
  assert.equal(calls.filter((call) => call[0] === "POST").length, 2);
});
