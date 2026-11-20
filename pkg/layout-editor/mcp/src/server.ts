import { randomUUID } from "node:crypto";
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  type CallToolRequest,
} from "@modelcontextprotocol/sdk/types.js";
import { BridgeClient, BridgeError } from "./bridge-client.js";
import { handleModeTool, ModeToolError, modeToolDefinitions } from "./level-mode.js";
import { handleSettingsTool, settingsToolDefinitions } from "./level-settings.js";
import type { Draft, JsonObject, LayoutDocument, LayoutItem, LevelSet, LevelSummary, ToolResult } from "./types.js";

const bridge = new BridgeClient();
const drafts = new Map<string, Draft>();
let lastExportId = "";

const toolDefinitions = [
  ...settingsToolDefinitions,
  ...modeToolDefinitions,
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
    description: "列出指定关卡集中的关卡（含分组名 group 字段，分组用于归类，组顺序按关卡顺序推导）。使用 setName，不要传仓库绝对路径。只读。",
    inputSchema: { type: "object", properties: { setName: { type: "string", minLength: 1 } }, required: ["setName"], additionalProperties: false },
  },
  {
    name: "oc2_get_level_detail",
    description: "读取关卡配置、原生 bundle 依赖、音频和场景路径；自动清理并保存历史非法 dependencies。",
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
    description: "读取关卡绑定菜谱和完整菜谱目录，供 AI 规划订单和设备。目录排除 Web_SauceSalad_Optional_ 内部锚点，保留 optional 登记；saladSubgroups 的 non_dlc11_official 独占剧情食材沙拉及其酱扩展，其余按互斥酱料组和可交叉的 official 来源组，按菜谱 id 去重计数。只读，无菜谱写工具；现有 HTTP/Web 保存原版 Cake_Plain_SO 时安全追加煎锅 Web_MixedFlourEgg，混选输入未核验则保留空表原生 lookup。完整混选用前端显式自动填充；另用 oc2_get_scene_layout 读回 cookingUtensil.allowedIngredientGuids，场景仍需写回落盘。",
    inputSchema: { type: "object", properties: { assetPath: { type: "string", minLength: 1 }, levelSet: { type: "string" } }, required: ["assetPath"], additionalProperties: false },
  },
  {
    name: "oc2_analyze_level_dependencies",
    description: "分析关卡原生 bundle 依赖、缺失和多余项，仅返回 ^bundle[0-9]+$。读取时自动清理并保存历史非法 dependencies；共享包分发清单独立管理。",
    inputSchema: { type: "object", properties: { assetPath: { type: "string", minLength: 1 } }, required: ["assetPath"], additionalProperties: false },
  },
  {
    name: "oc2_list_level_resource_usage",
    description: "统计关卡集内每关引用的素材来源库（common01/commonW1/custom 等）及去重资源数，含整集汇总。只读。",
    inputSchema: { type: "object", properties: { setName: { type: "string", minLength: 1 } }, required: ["setName"], additionalProperties: false },
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
    description: "将经过验证的场景草稿写回 Unity 场景。会保存场景并可能触发 Reload；必须显式 confirm=true。Unity 处于 Play 模式时整次拒绝，不改场景。",
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
    name: "oc2_list_custom_content",
    description: "列出关卡集的自定义食材/锅具/工作台/水槽/托盘/备餐容器/分料机条目。只读。",
    inputSchema: { type: "object", properties: { setName: { type: "string", minLength: 1 }, kind: { type: "string" } }, required: ["setName"], additionalProperties: false },
  },
  {
    name: "oc2_get_custom_content_templates",
    description: "列出自定义内容可用模板（common04 / common03 / commonW1 / Modules）。只读。",
    inputSchema: { type: "object", properties: { kind: { type: "string" } }, additionalProperties: false },
  },
  {
    name: "oc2_get_custom_content_catalog",
    description: "读取关卡集自定义内容的动态素材面板条目。只读。",
    inputSchema: { type: "object", properties: { setName: { type: "string", minLength: 1 } }, required: ["setName"], additionalProperties: false },
  },
  {
    name: "oc2_salad_sauce_bundles",
    description: "仅构建 commonw3 和 webcustomstub_runtime 的 Windows bundle，输出 Library/SaladSauceBundleBuild/Windows；不保存场景/LevelInfo，不发布。build 需 confirm=true；先返回 queued，随后用 status 读取状态及产物验证。构建阻塞 Unity 主线程时 HTTP 可能超时，先读 Library/SaladSauceBundleBuild/result.json，禁止盲目重试 build。",
    inputSchema: { type: "object", properties: { operation: { type: "string", enum: ["build", "status"] }, confirm: { type: "boolean" } }, required: ["operation"], additionalProperties: false },
  },
  {
    name: "oc2_start_export",
    description: "启动增量导出，自动预检空可选包。levels 仅交付关卡且不 staging 运行时；all/deps 携带依赖闭包。构建 map 保留可达共享资源归属。单集/多集/deps 共用 mode 与 stages 快照，deps 不适用阶段为 skipped。立即返回后轮询至 restore 收尾及 status=done/error，失败不重复启动；需要 confirm=true。",
    inputSchema: { type: "object", properties: { setNames: { type: "array", minItems: 1, items: { type: "string", minLength: 1 } }, mode: { type: "string", enum: ["all", "levels", "deps"] }, depsVersion: { type: "string" }, selectedLevels: { type: "array", items: { type: "string" } }, confirm: { type: "boolean", const: true } }, required: ["setNames", "confirm"], additionalProperties: false },
  },
  {
    name: "oc2_get_export_status",
    description: "查询 SetExportStatusDto 线程安全深拷贝快照：mode；stages[] 每行 phase/state(waiting|running|done|error|skipped)/elapsedMs/completed/total/message。固定行序 queued、starting、compile、preflight、orphan-clean、dual-stub-clean、prepare、build、package、zip、restore。只按后端快照展示进度/耗时，total=0 用不确定条；prepare 为真实已完成 n/N，非当前处理索引，不按历史耗时估预算。失败原阶段保持 error，restore 独立运行后终态 error，面板保留可关闭。缺 stages 的旧后端仅文字；idle/断网保留已知行，不推断完成。compile 域重载由 SessionState 恢复累计，仅重载缺口用 UTC 墙钟，其余用 Stopwatch。每 2 秒轮询至 restore 收尾及 status=done/error；zip 不代表完成。",
    inputSchema: { type: "object", properties: {}, additionalProperties: false },
  },
  {
    name: "oc2_wait_export",
    description: "等待导出终态，单次状态请求最多10秒，总等待默认30分钟。断线/HTML暂态自动重连；exportId可显式传入，默认核对最近启动的任务，任务替换明确失败。idle或zip不代表完成，不重复启动导出。",
    inputSchema: { type: "object", properties: { exportId: { type: "string" }, timeoutMs: { type: "integer", minimum: 1000, maximum: 1800000 } }, additionalProperties: false },
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
  const modeTool = modeToolDefinitions.some((tool) => tool.name === name);
  const settingsWrite = settingsToolDefinitions.some((tool) => tool.name === name);
  const modeWrite = settingsWrite || (modeTool && name !== "oc2_get_level_mode_config");
  const exportWrite = name === "oc2_start_export";
  const sideEffect = exportWrite ? "build" : modeWrite ? (settingsWrite || name === "oc2_save_level_mode_config" ? "write_asset" : "write_scene") : "read";
  const e = error instanceof BridgeError ? error : new Error(error instanceof Error ? error.message : String(error));
  return {
    ok: false,
    error: { code: error instanceof ModeToolError ? error.code : error instanceof BridgeError && error.status === 404 ? "NOT_FOUND" : "BRIDGE_ERROR", message: e.message, retryable: modeWrite || exportWrite ? false : error instanceof BridgeError ? error.retryable : false },
    ...(error instanceof ModeToolError ? { data: { writeOutcome: error.writeOutcome }, warnings: error.writeOutcome === "not_started" ? [] : ["先执行只读核对，禁止自动重试写操作。"] } : {}),
    operation: { name, kind: sideEffect, sideEffect },
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
  if (settingsToolDefinitions.some((tool) => tool.name === name)) {
    const data = await handleSettingsTool(name, input, bridge);
    const response = result(name, data, "write_asset", "write_asset", 30000, { assetPath: String(input.assetPath) });
    response.timing.deadlineMs = 180000;
    return response;
  }
  if (modeToolDefinitions.some((tool) => tool.name === name)) {
    const data = await handleModeTool(name, input, bridge);
    const sideEffect = name === "oc2_get_level_mode_config" ? "read" : name === "oc2_save_level_mode_config" ? "write_asset" : "write_scene";
    const readback = (data.readback || data) as JsonObject;
    const response = result(name, data, sideEffect, sideEffect, sideEffect === "read" ? 5000 : 30000, { assetPath: String(readback.assetPath), ...(typeof input.setName === "string" ? { setName: input.setName, levelId: String(input.levelId) } : {}) });
    if (sideEffect !== "read") response.timing.deadlineMs = 180000;
    return response;
  }
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
    const [recipes, catalog, orderSequence] = await Promise.all([
      bridge.get(`/api/level-recipes?assetPath=${encodeURIComponent(assetPath)}`),
      bridge.get(`/api/recipes?levelSet=${encodeURIComponent(levelSet)}`),
      // 订单顺序（4.0.0）：只读回显（order_seq~/order_seq.json）；旧桥无该端点时为 null。
      bridge.get(`/api/level/order-sequence?assetPath=${encodeURIComponent(assetPath)}`).catch(() => null),
    ]);
    return result(name, { levelRecipes: recipes, catalog, orderSequence }, "read", "read", 10000, { sceneAssetPath: assetPath });
  }
  if (name === "oc2_analyze_level_dependencies") {
    const assetPath = stringArg(input, "assetPath");
    return result(name, await bridge.get(`/api/level/bundles?assetPath=${encodeURIComponent(assetPath)}`), "compute", "read", 10000, { sceneAssetPath: assetPath });
  }
  if (name === "oc2_list_level_resource_usage") {
    const setName = stringArg(input, "setName");
    return result(name, await bridge.get(`/api/sets/${encodeURIComponent(setName)}/resource-stats`), "compute", "read", 8000, { setName });
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
  if (name === "oc2_list_custom_content") {
    const setName = stringArg(input, "setName");
    const kind = typeof input.kind === "string" ? input.kind : "";
    return result(name, await bridge.get(`/api/custom-content/list?setName=${encodeURIComponent(setName)}&kind=${encodeURIComponent(kind)}`), "read", "read", 5000);
  }
  if (name === "oc2_get_custom_content_templates") {
    const kind = typeof input.kind === "string" ? input.kind : "";
    return result(name, await bridge.get(`/api/custom-content/templates?kind=${encodeURIComponent(kind)}`), "read", "read", 2000);
  }
  if (name === "oc2_get_custom_content_catalog") {
    const setName = stringArg(input, "setName");
    return result(name, await bridge.get(`/api/custom-content/catalog?setName=${encodeURIComponent(setName)}`), "read", "read", 5000);
  }
  if (name === "oc2_salad_sauce_bundles") {
    const operation = stringArg(input, "operation");
    if (operation !== "build" && operation !== "status") throw new Error("operation 必须为 build/status");
    if (operation === "build" && input.confirm !== true) throw new Error("构建必须传 confirm=true");
    const endpoint = `/api/recipes/salad-sauce?operation=${operation}`;
    const data = operation === "build" ? await bridge.post(endpoint, {}) : await bridge.get(endpoint);
    return result(name, data, operation === "build" ? "build" : "read", operation === "build" ? "build" : "read", operation === "build" ? 300000 : 1000);
  }
  if (name === "oc2_start_export") {
    if (input.confirm !== true) throw new Error("启动导出必须传 confirm=true");
    if (!Array.isArray(input.setNames) || input.setNames.length === 0) throw new Error("setNames 不能为空");
    const body: JsonObject = { setNames: input.setNames, mode: input.mode || "all" };
    if (typeof input.depsVersion === "string") body.depsVersion = input.depsVersion;
    if (Array.isArray(input.selectedLevels)) body.selectedLevels = input.selectedLevels;
    const accepted = await bridge.post<JsonObject>("/api/set/export", body, 30000);
    lastExportId = String(accepted.exportId || "");
    return result(name, { accepted, exportId: lastExportId, taskId: lastExportId, setNames: input.setNames, pollAfterMs: 2000, deadlineMs: 1800000 }, "build", "build", 300000);
  }
  if (name === "oc2_get_export_status") return result(name, await bridge.get("/api/set/export/status", 10000), "read", "read", 1000);
  if (name === "oc2_wait_export") {
    const timeoutMs = typeof input.timeoutMs === "number" ? Math.min(Math.max(input.timeoutMs, 1000), 1800000) : 1800000;
    const started = Date.now();
    let status: JsonObject = {};
    let expectedId = typeof input.exportId === "string" ? input.exportId : lastExportId;
    while (Date.now() - started < timeoutMs) {
      try {
        status = await bridge.get<JsonObject>("/api/set/export/status", Math.min(10000, timeoutMs - (Date.now() - started)));
      } catch (error) {
        if (!(error instanceof BridgeError) || !error.retryable) throw error;
        await new Promise((resolve) => setTimeout(resolve, Math.min(2000, Math.max(0, timeoutMs - (Date.now() - started)))));
        continue;
      }
      if (expectedId && status.exportId && status.exportId !== expectedId)
        throw new BridgeError("导出任务已被替换，请核对 exportId；未重新启动导出。", 409, false);
      if (!expectedId && status.exportId) expectedId = String(status.exportId);
      const state = String(status.status || "");
      if ((!expectedId || status.exportId === expectedId) && (state === "done" || state === "error")) return result(name, { status, elapsedMs: Date.now() - started }, "build", "build", 300000);
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
