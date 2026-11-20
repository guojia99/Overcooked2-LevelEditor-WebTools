import assert from "node:assert/strict";
import { createServer } from "node:http";
import { test } from "node:test";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";
import { BridgeClient, BridgeError } from "../dist/bridge-client.js";

test("export wait recovers disconnect and idle, rejects another task without POST", async () => {
  let reads = 0;
  let writes = 0;
  let replaced = false;
  const server = createServer((req, res) => {
    if (req.method === "POST") writes++;
    if (++reads === 1) { req.socket.destroy(); return; }
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify(replaced ? { status: "done", exportId: "other" }
      : reads === 2 ? { status: "idle" } : { status: "done", exportId: "expected", mode: "levels" }));
  });
  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  const transport = new StdioClientTransport({ command: process.execPath,
    args: [new URL("../dist/server.js", import.meta.url).pathname],
    env: { ...process.env, OC2_EDITOR_BRIDGE_URL: `http://127.0.0.1:${server.address().port}` } });
  const client = new Client({ name: "export-recovery-test", version: "1" });
  try {
    await client.connect(transport);
    const recovered = await client.callTool({ name: "oc2_wait_export", arguments: { exportId: "expected", timeoutMs: 12000 } });
    const value = JSON.parse(recovered.content[0].text);
    assert.equal(value.ok, true);
    assert.equal(value.data.status.exportId, "expected");
    replaced = true;
    const mismatch = await client.callTool({ name: "oc2_wait_export", arguments: { exportId: "expected", timeoutMs: 1000 } });
    assert.equal(JSON.parse(mismatch.content[0].text).ok, false);
    assert.equal(writes, 0);
    const bridge = new BridgeClient(`http://127.0.0.1:${server.address().port}`);
    server.removeAllListeners("request");
    server.on("request", (_req, res) => { res.writeHead(200); res.write("{"); });
    await assert.rejects(bridge.get("/api/set/export/status", 40), error => error instanceof BridgeError && error.retryable);
  } finally {
    await client.close();
    server.closeAllConnections();
    await new Promise(resolve => server.close(resolve));
  }
});
