import { randomUUID } from "node:crypto";
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  type CallToolRequest,
} from "@modelcontextprotocol/sdk/types.js";
import { BridgeClient, BridgeError } from "./bridge-client.js";
import type { Draft, JsonObject, LayoutDocument, LayoutItem, LevelSet, LevelSummary, ToolResult } from "./types.js";

const bridge = new BridgeClient();
const drafts = new Map<string, Draft>();

const toolDefinitions = [
  {
    name: "oc2_editor_health",
    description: "检查 Overcooked2 Unity 关卡编辑器 Bridge 是否在线。只读。",
    inputSchema: { type: "object", properties: {}, additionalProperties: false },
  },
  {
    name: "oc2_list_level_sets",
    description: "列出关卡集及其数量、版本和作者。只读。",
    inputSchema: { type: "object", properties: {}, additionalProperties: false },
  },
  {
    name: "oc2_list_levels",
    description: "列出指定关卡集中的关卡。使用 setName，不要传仓库绝对路径。只读。",
    inputSchema: { type: "object", properties: { setName: { type: "string", minLength: 1 } }, required: ["setName"], additionalProperties: false },
  },
  {
    name: "oc2_get_level_detail",
    description: "读取关卡配置、菜谱依赖、音频和场景路径。只读。",
    inputSchema: { type: "object", properties: { assetPath: { type: "string", minLength: 1 } }, required: ["assetPath"], additionalProperties: false },
  },
  {
    name: "oc2_get_scene_layout",
    description: "读取完整场景布局，包括物品、地板、动画、联动、相机和灯光。只读，通常需要 2-15 秒。",
    inputSchema: { type: "object", properties: { assetPath: { type: "string", minLength: 1 } }, required: ["assetPath"], additionalProperties: false },
  },
  {
    name: "oc2_get_grid",
    description: "读取当前 Unity 场景网格信息。只读。",
    inputSchema: { type: "object", properties: {}, additionalProperties: false },
  },
  {
    name: "oc2_get_level_recipe_context",
    description: "读取关卡绑定菜谱和完整菜谱目录，供 AI 规划订单和设备。只读。",
    inputSchema: { type: "object", properties: { assetPath: { type: "string", minLength: 1 }, levelSet: { type: "string" } }, required: ["assetPath"], additionalProperties: false },
  },
  {
    name: "oc2_analyze_level_dependencies",
    description: "分析关卡所需 AssetBundle、缺失依赖和多余依赖。只读。",
    inputSchema: { type: "object", properties: { assetPath: { type: "string", minLength: 1 } }, required: ["assetPath"], additionalProperties: false },
  },
  {
    name: "oc2_validate_scene",
    description: "读取场景并执行 MCP 层安全校验，不写入 Unity。",
    inputSchema: { type: "object", properties: { assetPath: { type: "string", minLength: 1 } }, required: ["assetPath"], additionalProperties: false },
  },
  {
    name: "oc2_create_scene_draft",
    description: "读取场景并创建内存草稿。不会修改 Unity。后续使用 draftId。",
    inputSchema: { type: "object", properties: { setName: { type: "string", minLength: 1 }, levelId: { type: "string", minLength: 1 }, assetPath: { type: "string", minLength: 1 } }, required: ["setName", "levelId", "assetPath"], additionalProperties: false },
  },
  {
    name: "oc2_apply_scene_patch",
    description: "对场景草稿应用移动、旋转、删除或新增物品操作。不会修改 Unity。",
    inputSchema: { type: "object", properties: { draftId: { type: "string", minLength: 1 }, operations: { type: "array", minItems: 1, items: { type: "object" } } }, required: ["draftId", "operations"], additionalProperties: false },
  },
  {
    name: "oc2_validate_scene_draft",
    description: "验证场景草稿，不写入 Unity。",
    inputSchema: { type: "object", properties: { draftId: { type: "string", minLength: 1 } }, required: ["draftId"], additionalProperties: false },
  },
  {
    name: "oc2_diff_scene_draft",
    description: "返回场景草稿相对原场景的结构化差异。",
    inputSchema: { type: "object", properties: { draftId: { type: "string", minLength: 1 } }, required: ["draftId"], additionalProperties: false },
  },
  {
    name: "oc2_commit_scene_draft",
    description: "将经过验证的场景草稿写回 Unity 场景。会保存场景并可能触发 Reload；必须显式 confirm=true。",
    inputSchema: { type: "object", properties: { draftId: { type: "string", minLength: 1 }, confirm: { type: "boolean", const: true }, snap: { type: "number", minimum: 0 }, syncWalkable: { type: "boolean" }, only: { type: "string", enum: ["", "items", "decor", "floors"] } }, required: ["draftId", "confirm"], additionalProperties: false },
  },
  {
    name: "oc2_list_writeback_history",
    description: "列出指定关卡最近的写回历史。只读。",
    inputSchema: { type: "object", properties: { assetPath: { type: "string", minLength: 1 } }, required: ["assetPath"], additionalProperties: false },
  },
  {
    name: "oc2_get_writeback_diff",
    description: "读取指定写回记录的结构化差异。只读。",
    inputSchema: { type: "object", properties: { assetPath: { type: "string", minLength: 1 }, record: { type: "string", minLength: 1 } }, required: ["assetPath", "record"], additionalProperties: false },
  },
  {
    name: "oc2_start_export",
    description: "启动关卡集导出。立即返回，AI 必须使用 taskId 继续等待；通常 3-30 分钟。需要 confirm=true。",
    inputSchema: { type: "object", properties: { setNames: { type: "array", minItems: 1, items: { type: "string", minLength: 1 } }, mode: { type: "string", enum: ["all", "levels", "deps"] }, depsVersion: { type: "string" }, selectedLevels: { type: "array", items: { type: "string" } }, confirm: { type: "boolean", const: true } }, required: ["setNames", "confirm"], additionalProperties: false },
  },
  {
    name: "oc2_get_export_status",
    description: "查询当前 Unity 导出任务状态。建议每 2 秒调用一次。",
    inputSchema: { type: "object", properties: {}, additionalProperties: false },
  },
  {
    name: "oc2_wait_export",
    description: "等待导出任务完成，内部每 2 秒轮询，最长默认 30 分钟。",
    inputSchema: { type: "object", properties: { timeoutMs: { type: "integer", minimum: 1000, maximum: 1800000 } }, additionalProperties: false },
  },
];

function now(): string { return new Date().toISOString(); }

function result(name: string, data: unknown, kind: ToolResult["operation"]["kind"], sideEffect: ToolResult["operation"]["sideEffect"], estimate: number, resource?: ToolResult["resource"]): ToolResult {
  return {
    ok: true,
    data,
    operation: { name, kind, sideEffect },
    resource,
    bridge: { online: true, url: bridge.baseUrl },
    timing: { estimatedMs: estimate, pollAfterMs: null, deadlineMs: Math.max(estimate * 3, 30000) },
  };
}

function errorResult(name: string, error: unknown, resource?: ToolResult["resource"]): ToolResult {
  const e = error instanceof BridgeError ? error : new Error(error instanceof Error ? error.message : String(error));
  return {
    ok: false,
    error: { code: error instanceof BridgeError && error.status === 404 ? "NOT_FOUND" : "BRIDGE_ERROR", message: e.message, retryable: error instanceof BridgeError ? error.retryable : false },
    operation: { name, kind: "read", sideEffect: "read" },
    resource,
    bridge: { online: false, url: bridge.baseUrl },
    timing: { estimatedMs: 1000, pollAfterMs: 3000, deadlineMs: 30000 },
  };
}

function textContent(value: ToolResult): { content: [{ type: "text"; text: string }]; structuredContent: ToolResult; isError?: boolean } {
  return { content: [{ type: "text", text: JSON.stringify(value, null, 2) }], structuredContent: value, ...(value.ok ? {} : { isError: true }) };
}

function args(request: CallToolRequest): JsonObject { return (request.params.arguments || {}) as JsonObject; }
function stringArg(input: JsonObject, key: string): string { const value = input[key]; if (typeof value !== "string" || !value) throw new Error(`缺少参数 ${key}`); return value; }

async function listSets(name: string): Promise<ToolResult> {
  const data = await bridge.get<{ sets?: LevelSet[] }>("/api/sets");
  return result(name, data.sets || [], "read", "read", 5000);
}

async function listLevels(name: string, input: JsonObject): Promise<ToolResult> {
  const setName = stringArg(input, "setName");
  const data = await bridge.get<{ levels?: LevelSummary[] }>(`/api/sets/${encodeURIComponent(setName)}/levels`);
  return result(name, data.levels || [], "read", "read", 5000, { setName });
}

function validateDocument(document: LayoutDocument): { valid: boolean; errors: string[]; warnings: string[] } {
  const errors: string[] = [];
  const warnings: string[] = [];
  const ids = new Set<string>();
  for (const item of document.items || []) {
    if (!item.instanceId) errors.push("存在没有 instanceId 的物品");
    if (ids.has(item.instanceId)) errors.push(`重复 instanceId: ${item.instanceId}`);
    ids.add(item.instanceId);
    if (!item.prefabGuid && !item.prefabAssetPath) warnings.push(`物品 ${item.instanceId} 缺少 prefab 引用`);
    if (!item.localPosition || [item.localPosition.x, item.localPosition.y, item.localPosition.z].some((v) => !Number.isFinite(v))) errors.push(`物品 ${item.instanceId} 坐标无效`);
    const dispenser = item.dispenser as { randomItemGuids?: unknown } | undefined;
    if (dispenser && Array.isArray(dispenser.randomItemGuids) && dispenser.randomItemGuids.length === 1) warnings.push(`随机食材箱 ${item.instanceId} 只有一个候选`);
  }
  return { valid: errors.length === 0, errors, warnings };
}

function cloneDocument(document: LayoutDocument): LayoutDocument { return JSON.parse(JSON.stringify(document)) as LayoutDocument; }

function diffDocuments(before: LayoutDocument, after: LayoutDocument): JsonObject {
  const oldItems = new Map(before.items.map((item) => [item.instanceId, item]));
  const newItems = new Map(after.items.map((item) => [item.instanceId, item]));
  const added = [...newItems.keys()].filter((id) => !oldItems.has(id));
  const removed = [...oldItems.keys()].filter((id) => !newItems.has(id));
  const changed = [...newItems.keys()].filter((id) => oldItems.has(id) && JSON.stringify(oldItems.get(id)) !== JSON.stringify(newItems.get(id)));
  return { added, removed, changed, itemCountBefore: before.items.length, itemCountAfter: after.items.length, floorsChanged: JSON.stringify(before.floors || []) !== JSON.stringify(after.floors || []) };
}

function applyOperations(document: LayoutDocument, operations: unknown[]): LayoutDocument {
  const next = cloneDocument(document);
  for (const raw of operations) {
    if (!raw || typeof raw !== "object") throw new Error("每个 operation 必须是对象");
    const operation = raw as JsonObject;
    const op = String(operation.op || "");
    if (op === "move_item") {
      const item = next.items.find((candidate) => candidate.instanceId === operation.instanceId);
      if (!item) throw new Error(`找不到物品 ${String(operation.instanceId)}`);
      const position = operation.position as Partial<{ x: number; y: number; z: number }>;
      if (![position.x, position.y, position.z].every((value) => typeof value === "number" && Number.isFinite(value))) throw new Error("move_item.position 必须包含有效 x/y/z");
      item.localPosition = { x: position.x as number, y: position.y as number, z: position.z as number };
    } else if (op === "rotate_item") {
      const item = next.items.find((candidate) => candidate.instanceId === operation.instanceId);
      if (!item || typeof operation.y !== "number") throw new Error("rotate_item 需要有效 instanceId 和 y");
      item.localRotationY = operation.y;
    } else if (op === "delete_item") {
      const index = next.items.findIndex((candidate) => candidate.instanceId === operation.instanceId);
      if (index < 0) throw new Error(`找不到物品 ${String(operation.instanceId)}`);
      next.items.splice(index, 1);
    } else if (op === "add_item") {
      const item = operation.item;
      if (!item || typeof item !== "object") throw new Error("add_item 需要完整 item 对象；请先从目录查询 prefab 数据");
      const candidate = item as LayoutItem;
      if (!candidate.instanceId || !candidate.localPosition) throw new Error("新增物品必须包含 instanceId 和 localPosition");
      if (next.items.some((existing) => existing.instanceId === candidate.instanceId)) throw new Error(`重复 instanceId: ${candidate.instanceId}`);
      next.items.push(candidate);
    } else if (op === "replace_document") {
      if (!operation.document || typeof operation.document !== "object") throw new Error("replace_document 需要 document");
      return cloneDocument(operation.document as LayoutDocument);
    } else {
      throw new Error(`不支持的场景操作: ${op}`);
    }
  }
  return next;
}

async function handle(name: string, input: JsonObject): Promise<ToolResult> {
  if (name === "oc2_editor_health") return result(name, await bridge.get("/api/health"), "read", "read", 1000);
  if (name === "oc2_list_level_sets") return listSets(name);
  if (name === "oc2_list_levels") return listLevels(name, input);
  if (name === "oc2_get_level_detail") {
    const assetPath = stringArg(input, "assetPath");
    return result(name, await bridge.get(`/api/level?assetPath=${encodeURIComponent(assetPath)}`), "read", "read", 5000, { sceneAssetPath: assetPath });
  }
  if (name === "oc2_get_scene_layout") {
    const assetPath = stringArg(input, "assetPath");
    return result(name, await bridge.get(`/api/scene/layout?assetPath=${encodeURIComponent(assetPath)}`), "read", "read", 10000, { sceneAssetPath: assetPath });
  }
  if (name === "oc2_get_grid") return result(name, await bridge.get("/api/grid"), "read", "read", 2000);
  if (name === "oc2_get_level_recipe_context") {
    const assetPath = stringArg(input, "assetPath");
    const levelSet = typeof input.levelSet === "string" ? input.levelSet : "";
    const [recipes, catalog] = await Promise.all([
      bridge.get(`/api/level-recipes?assetPath=${encodeURIComponent(assetPath)}`),
      bridge.get(`/api/recipes?levelSet=${encodeURIComponent(levelSet)}`),
    ]);
    return result(name, { levelRecipes: recipes, catalog }, "read", "read", 10000, { sceneAssetPath: assetPath });
  }
  if (name === "oc2_analyze_level_dependencies") {
    const assetPath = stringArg(input, "assetPath");
    return result(name, await bridge.get(`/api/level/bundles?assetPath=${encodeURIComponent(assetPath)}`), "compute", "read", 10000, { sceneAssetPath: assetPath });
  }
  if (name === "oc2_validate_scene") {
    const assetPath = stringArg(input, "assetPath");
    const document = await bridge.get<LayoutDocument>(`/api/scene/layout?assetPath=${encodeURIComponent(assetPath)}`);
    return result(name, validateDocument(document), "compute", "read", 10000, { sceneAssetPath: assetPath });
  }
  if (name === "oc2_create_scene_draft") {
    const setName = stringArg(input, "setName");
    const levelId = stringArg(input, "levelId");
    const assetPath = stringArg(input, "assetPath");
    const document = await bridge.get<LayoutDocument>(`/api/scene/layout?assetPath=${encodeURIComponent(assetPath)}`);
    const id = `draft-${randomUUID()}`;
    drafts.set(id, { id, setName, levelId, sceneAssetPath: assetPath, document: cloneDocument(document), baseDocument: cloneDocument(document), createdAt: now(), updatedAt: now() });
    return result(name, { draftId: id, sceneAssetPath: assetPath, itemCount: document.items.length, createdAt: now() }, "draft", "draft", 10000, { setName, levelId, sceneAssetPath: assetPath });
  }
  if (name === "oc2_apply_scene_patch") {
    const draftId = stringArg(input, "draftId");
    const draft = drafts.get(draftId);
    if (!draft) throw new Error(`找不到草稿 ${draftId}`);
    const operations = input.operations;
    if (!Array.isArray(operations)) throw new Error("operations 必须是数组");
    draft.document = applyOperations(draft.document, operations);
    draft.updatedAt = now();
    return result(name, { draftId, diff: diffDocuments(draft.baseDocument, draft.document), validation: validateDocument(draft.document) }, "draft", "draft", 1000, { setName: draft.setName, levelId: draft.levelId, sceneAssetPath: draft.sceneAssetPath });
  }
  if (name === "oc2_validate_scene_draft" || name === "oc2_diff_scene_draft" || name === "oc2_commit_scene_draft") {
    const draftId = stringArg(input, "draftId");
    const draft = drafts.get(draftId);
    if (!draft) throw new Error(`找不到草稿 ${draftId}`);
    if (name === "oc2_validate_scene_draft") return result(name, validateDocument(draft.document), "compute", "draft", 1000, { setName: draft.setName, levelId: draft.levelId, sceneAssetPath: draft.sceneAssetPath });
    if (name === "oc2_diff_scene_draft") return result(name, diffDocuments(draft.baseDocument, draft.document), "compute", "draft", 1000, { setName: draft.setName, levelId: draft.levelId, sceneAssetPath: draft.sceneAssetPath });
    if (input.confirm !== true) throw new Error("写回场景必须传 confirm=true");
    const validation = validateDocument(draft.document);
    if (!validation.valid) throw new Error(`场景校验失败: ${validation.errors.join("；")}`);
    const query = new URLSearchParams({ snap: String(typeof input.snap === "number" ? input.snap : 0.01) });
    if (input.syncWalkable === true) query.set("syncWalkable", "1");
    if (typeof input.only === "string" && input.only) query.set("only", input.only);
    const previousDocument = cloneDocument(draft.baseDocument);
    const saved = await bridge.post(`/api/scene/layout?${query.toString()}`, draft.document as unknown as JsonObject);
    draft.baseDocument = cloneDocument(draft.document);
    draft.updatedAt = now();
    return result(name, { draftId, saved, diff: diffDocuments(previousDocument, draft.document), warnings: validation.warnings }, "write_scene", "write_scene", 30000, { setName: draft.setName, levelId: draft.levelId, sceneAssetPath: draft.sceneAssetPath });
  }
  if (name === "oc2_list_writeback_history") {
    const assetPath = stringArg(input, "assetPath");
    return result(name, await bridge.get(`/api/writeback/history?assetPath=${encodeURIComponent(assetPath)}`), "read", "read", 5000, { sceneAssetPath: assetPath });
  }
  if (name === "oc2_get_writeback_diff") {
    const assetPath = stringArg(input, "assetPath");
    const record = stringArg(input, "record");
    return result(name, await bridge.get(`/api/writeback/history/detail?assetPath=${encodeURIComponent(assetPath)}&record=${encodeURIComponent(record)}`), "read", "read", 5000, { sceneAssetPath: assetPath });
  }
  if (name === "oc2_start_export") {
    if (input.confirm !== true) throw new Error("启动导出必须传 confirm=true");
    if (!Array.isArray(input.setNames) || input.setNames.length === 0) throw new Error("setNames 不能为空");
    const body: JsonObject = { setNames: input.setNames, mode: input.mode || "all" };
    if (typeof input.depsVersion === "string") body.depsVersion = input.depsVersion;
    if (Array.isArray(input.selectedLevels)) body.selectedLevels = input.selectedLevels;
    const accepted = await bridge.post("/api/set/export", body);
    return result(name, { accepted, taskId: `export-${randomUUID()}`, setNames: input.setNames, pollAfterMs: 2000, deadlineMs: 1800000 }, "build", "build", 300000);
  }
  if (name === "oc2_get_export_status") return result(name, await bridge.get("/api/set/export/status"), "read", "read", 1000);
  if (name === "oc2_wait_export") {
    const timeoutMs = typeof input.timeoutMs === "number" ? Math.min(Math.max(input.timeoutMs, 1000), 1800000) : 1800000;
    const started = Date.now();
    let status: JsonObject = {};
    while (Date.now() - started < timeoutMs) {
      status = await bridge.get<JsonObject>("/api/set/export/status");
      const state = String(status.status || "");
      if (state === "done" || state === "error") return result(name, { status, elapsedMs: Date.now() - started }, "build", "build", 300000);
      await new Promise((resolve) => setTimeout(resolve, 2000));
    }
    throw new BridgeError(`导出等待超过 ${timeoutMs}ms`, 408, true);
  }
  throw new Error(`未知工具 ${name}`);
}

const server = new Server({ name: "oc2-level-editor", version: "0.1.0" }, { capabilities: { tools: {} } });
server.setRequestHandler(ListToolsRequestSchema, async () => ({ tools: toolDefinitions }));
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const name = request.params.name;
  try {
    return textContent(await handle(name, args(request)));
  } catch (error) {
    return textContent(errorResult(name, error));
  }
});

await server.connect(new StdioServerTransport());
