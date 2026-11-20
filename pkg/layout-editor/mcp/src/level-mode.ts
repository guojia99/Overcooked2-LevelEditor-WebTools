import { readFileSync } from "node:fs";
import { isDeepStrictEqual } from "node:util";
import { BridgeClient, BridgeError } from "./bridge-client.js";
import type { GameMode, JsonObject, LevelModeChangeDto, LevelModeConfig } from "./types.js";

const schema = JSON.parse(readFileSync(new URL("../schemas/level-mode.schema.json", import.meta.url), "utf8"));
const assetPathSchema = schema.$defs.assetPath;
const gameModeSchema = schema.$defs.gameMode;
export const modeToolDefinitions = [
  {
    name: "oc2_get_level_mode_config",
    description: "读取 LevelModeConfigDto、Dynamic 原生阶段、Horde 1P-4P 波次及 common04 六种敌人目录。assetPath 是 LevelInfoSO 路径；Web partLevel 与原生模式分开。只读。",
    inputSchema: { type: "object", properties: { assetPath: assetPathSchema }, required: ["assetPath"], additionalProperties: false },
  },
  {
    name: "oc2_save_level_mode_config",
    description: "保存完整 Dynamic/Horde 配置到 2 MiB sidecar 并同步本关专属 SO；不切换模式。config 从 GET 修改后传入，enemies 只读不发送。需 confirm=true；自动 GET 读回核对，写入或读回失败禁止自动重试。Dynamic 阶段数须与 N-1 原生有效转场对应。",
    inputSchema: { type: "object", $defs: schema.$defs, properties: { config: { allOf: [{ $ref: "#/$defs/modeConfig" }, { properties: { gameMode: { enum: ["Dynamic", "Horde"] } } }] }, confirm: { type: "boolean", const: true } }, required: ["config", "confirm"], additionalProperties: false },
  },
  {
    name: "oc2_change_level_mode",
    description: "切换 Campaign/Dynamic/Horde 原生控制器。需场景打开激活、所有场景干净、唯一有效 Manager。convertHordeObjects 可选布尔默认 false；用户明确同意 true 才在事务内双向转换受支持普通 ServingStation/Horde Service_Window wrapper，保留原 GO/位置/名称/回收绑定，改变外观和组件；不可自动开启。金币 gate 不转换，切出前先删除；Horde 可无金币 gate，仍需激活交付窗且无普通口。切出时用 true 转换最后 Horde 窗，勿预删。Dynamic 需 N-1 原生有效转场；partLevel.enabled 拒切 Dynamic/Horde。confirm=true，自动读回详情及配置；禁止自动重试。",
    inputSchema: { type: "object", properties: { assetPath: assetPathSchema, gameMode: gameModeSchema, convertHordeObjects: schema.$defs.convertHordeObjects, confirm: { type: "boolean", const: true } }, required: ["assetPath", "gameMode", "confirm"], additionalProperties: false },
  },
  {
    name: "oc2_create_level",
    description: "按原生模式模板新建关卡，gameMode 缺省 Campaign，可选 Dynamic/Horde。保存已打开场景后调用，confirm=true；自动从列表发现真实资产路径并 GET 读回核对。失败先查列表，不可盲重试。",
    inputSchema: { type: "object", properties: { setName: { type: "string", pattern: "^[a-z0-9_]+$" }, levelId: { type: "string", pattern: "^[a-z0-9_]+$" }, levelName: { type: "string" }, levelNameZH: { type: "string" }, gameMode: { ...gameModeSchema, default: "Campaign" }, confirm: { type: "boolean", const: true } }, required: ["setName", "levelId", "confirm"], additionalProperties: false },
  },
];

export class ModeToolError extends Error {
  constructor(message: string, readonly code: string, readonly writeOutcome: "not_started" | "unknown" | "acknowledged" = "not_started", readonly status = 0) {
    super(message);
  }
}

function requireValue(condition: unknown, message: string): asserts condition {
  if (!condition) throw new ModeToolError(message, "INVALID_ARGUMENT");
}

function gameMode(value: unknown): GameMode {
  requireValue(value === "Campaign" || value === "Dynamic" || value === "Horde", "gameMode 必须为 Campaign/Dynamic/Horde");
  return value;
}

function assetPath(value: unknown): string {
  requireValue(typeof value === "string" && value.length <= 1024 && /^Assets\/LevelSets\/[^/\\]+\/data\/[^/\\]+\/[^/\\]+\.asset$/.test(value), "assetPath 必须为后端返回的本关 LevelInfoSO 路径");
  return value;
}

function validateConfig(value: unknown): LevelModeConfig {
  requireValue(value && typeof value === "object" && !Array.isArray(value), "config 必须为完整 LevelModeConfigDto");
  const config = value as LevelModeConfig;
  const allowed = Object.keys(schema.$defs.modeConfig.properties);
  requireValue(Object.keys(config).every((key) => allowed.includes(key)), "config 包含未知字段（partLevel 不属于原生模式配置）");
  assetPath(config.assetPath);
  gameMode(config.gameMode);
  requireValue(config.gameMode !== "Campaign", "Campaign 菜谱使用原有菜谱 API，不能保存模式 sidecar");
  const number = (v: unknown, min: number, max: number, integer = true) => typeof v === "number" && Number.isFinite(v) && v >= min && v <= max && (!integer || Number.isInteger(v));
  requireValue(number(config.targetHealth, 1, 1000000) && number(config.kitchenHealth, 1, 1000000), "生命值须为 1 到 1000000 的整数");
  requireValue(number(config.targetRepairSpeed, 0, 1000000, false) && number(config.targetRepairThreshold, 0, 1000000, false), "修复参数须为 0 到 1000000 的有限数");
  requireValue(number(config.minOrderCount, 1, 10) && number(config.maxOrderCount, config.minOrderCount, 10), "订单数须满足 1 <= minOrderCount <= maxOrderCount <= 10");
  let total = 0;
  const guids = (values: unknown): values is string[] => Array.isArray(values) && values.length >= 1 && values.length <= 256 && values.every((v) => typeof v === "string" && /^[0-9a-fA-F]{32}$/.test(v));
  const ids = (entries: { id: string }[]) => entries.every((v) => v && typeof v.id === "string" && v.id.trim() && v.id.length <= 128 && !/[\p{Cc}]/u.test(v.id)) && new Set(entries.map((v) => v.id)).size === entries.length;
  if (config.gameMode === "Dynamic") {
    requireValue(config.hordeConfigs == null || (Array.isArray(config.hordeConfigs) && config.hordeConfigs.length === 0), "Dynamic 不能包含 Horde 配置");
    requireValue(Array.isArray(config.phases) && config.phases.length >= 2 && config.phases.length <= 64 && ids(config.phases), "Dynamic 需 2 到 64 个唯一非空阶段 ID");
    for (const phase of config.phases) {
      requireValue(Object.keys(phase).every((key) => ["id", "durationSeconds", "recipeGuids"].includes(key)) && number(phase.durationSeconds, 1, 86400) && guids(phase.recipeGuids), "阶段时长须为 1 到 86400 秒，每份菜单需 1 到 256 个菜谱 GUID");
      total += phase.recipeGuids.length;
    }
  } else {
    requireValue(config.phases == null || (Array.isArray(config.phases) && config.phases.length === 0), "Horde 不能包含 Dynamic 阶段");
    requireValue(Array.isArray(config.hordeConfigs) && config.hordeConfigs.length === 4, "Horde 必须按 1P 到 4P 提供四份配置");
    for (const player of config.hordeConfigs) {
      requireValue(player && Object.keys(player).every((key) => ["targetRepairCostMax", "waves"].includes(key)) && number(player.targetRepairCostMax, 0, 1000000) && Array.isArray(player.waves) && player.waves.length >= 1 && player.waves.length <= 128 && ids(player.waves), "每个人数配置需有效修复费用、1 到 128 个唯一波次 ID");
      for (const wave of player.waves) {
        requireValue(Object.keys(wave).every((key) => ["id", "intervalSeconds", "recipeGuids", "enemyGuids", "spawnTimes"].includes(key)) && number(wave.intervalSeconds, 0, 86400) && guids(wave.recipeGuids) && guids(wave.enemyGuids), "每波需有效间隔、菜谱及敌人 GUID（各 1 到 256 个）");
        requireValue(Array.isArray(wave.spawnTimes) && wave.spawnTimes.length === wave.enemyGuids.length && wave.spawnTimes.every((v) => number(v, 0, 86400)), "spawnTimes 必须与 enemyGuids 等长且为 0 到 86400 的整数；保留重复敌人和同时生成");
        total += wave.recipeGuids.length + wave.enemyGuids.length;
      }
    }
  }
  requireValue(total <= 32768, "菜谱与敌人总条目数不能超过 32768");
  return config;
}

function writableConfig(config: LevelModeConfig): JsonObject {
  const { enemies: _enemies, ...saved } = config;
  return { ...saved, phases: config.gameMode === "Dynamic" ? config.phases : null, hordeConfigs: config.gameMode === "Horde" ? config.hordeConfigs : null };
}

async function readConfig(bridge: BridgeClient, path: string): Promise<LevelModeConfig> {
  const config = await bridge.get<LevelModeConfig>(`/api/level/mode-config?assetPath=${encodeURIComponent(path)}`, 30000);
  if (!config || config.assetPath !== path || !["Campaign", "Dynamic", "Horde"].includes(config.gameMode)) throw new Error("模式 GET 未返回匹配的 LevelModeConfigDto");
  if (config.gameMode !== "Campaign") validateConfig(config);
  return config;
}

export async function handleModeTool(name: string, input: JsonObject, bridge: BridgeClient): Promise<JsonObject> {
  const definition = modeToolDefinitions.find((tool) => tool.name === name);
  requireValue(definition && input && typeof input === "object" && !Array.isArray(input), "未知工具或无效参数对象");
  requireValue(Object.keys(input).every((key) => Object.hasOwn(definition.inputSchema.properties, key)), "包含未知参数");
  if (name === "oc2_get_level_mode_config") return await readConfig(bridge, assetPath(input.assetPath)) as unknown as JsonObject;
  requireValue(input.confirm === true, "模式写操作必须传 confirm=true");
  let endpoint: string;
  let body: JsonObject;
  let path: string | undefined;
  let wanted: LevelModeConfig | undefined;
  let mode: GameMode;
  if (name === "oc2_save_level_mode_config") {
    wanted = validateConfig(input.config);
    path = wanted.assetPath;
    mode = wanted.gameMode;
    body = writableConfig(wanted);
    requireValue(Buffer.byteLength(JSON.stringify(body), "utf8") <= 2097152, "模式 POST JSON 超过 2 MiB 上限");
    endpoint = "/api/level/mode-config";
  } else if (name === "oc2_change_level_mode") {
    path = assetPath(input.assetPath);
    mode = gameMode(input.gameMode);
    requireValue(input.convertHordeObjects === undefined || typeof input.convertHordeObjects === "boolean", "convertHordeObjects 必须为布尔值；仅用户明确同意后才能设为 true");
    body = { assetPath: path, gameMode: mode, convertHordeObjects: input.convertHordeObjects === true } satisfies LevelModeChangeDto;
    endpoint = "/api/level/mode";
  } else {
    requireValue(name === "oc2_create_level", "未知模式工具");
    requireValue(typeof input.setName === "string" && /^[a-z0-9_]+$/.test(input.setName) && typeof input.levelId === "string" && /^[a-z0-9_]+$/.test(input.levelId), "setName/levelId 只允许小写字母、数字和下划线");
    mode = input.gameMode === undefined ? "Campaign" : gameMode(input.gameMode);
    body = { setName: input.setName, levelId: input.levelId, gameMode: mode };
    for (const key of ["levelName", "levelNameZH"]) {
      requireValue(input[key] === undefined || typeof input[key] === "string", `${key} 必须为字符串`);
      if (input[key] !== undefined) body[key] = input[key];
    }
    endpoint = "/api/level/create";
  }

  // No retries: a lost response can follow a successful Unity save.
  let saved: unknown;
  try {
    saved = await bridge.post(endpoint, body, 60000);
    if (!saved || typeof saved !== "object" || (saved as JsonObject).ok !== true) throw new Error("写响应未确认 ok=true");
  } catch (error) {
    throw new ModeToolError(`写操作失败或结果未确认；先 GET 模式/详情（创建先查关卡列表），不要自动重试。${error instanceof Error ? error.message : String(error)}`, "WRITE_UNCONFIRMED", "unknown", error instanceof BridgeError ? error.status : 0);
  }
  try {
    if (!path) {
      const levels = await bridge.get<{ levels?: { assetPath: string; sceneName: string }[] }>(`/api/sets/${encodeURIComponent(String(input.setName))}/levels`, 30000);
      const found = levels.levels?.filter((level) => level.sceneName === input.levelId);
      if (found?.length !== 1) throw new Error("创建后未能从列表唯一解析新关卡");
      path = assetPath(found[0].assetPath);
    }
    const readback = await readConfig(bridge, path);
    if (readback.gameMode !== mode) throw new Error("模式读回与写入目标不一致");
    if (wanted) {
      const expected = writableConfig(wanted);
      const actual = writableConfig(readback);
      // Unity's float JSON formatting may retain only seven significant digits.
      for (const key of ["targetRepairSpeed", "targetRepairThreshold"] as const) {
        const expectedFloat = Math.fround(wanted[key]);
        if (Math.abs(readback[key] - expectedFloat) > Math.max(2 ** -149, Math.abs(expectedFloat) * 1e-6)) throw new Error(`${key} 读回不一致`);
        actual[key] = expected[key];
      }
      if (!isDeepStrictEqual(actual, expected)) throw new Error("模式配置读回与写入不一致");
      return { saved, readback, verified: true };
    }
    const detail = await bridge.get<JsonObject>(`/api/level?assetPath=${encodeURIComponent(path)}`, 30000);
    if (!detail || detail.levelInfoAssetPath !== path || detail.gameMode !== mode) throw new Error("详情读回路径或原生模式不一致");
    return { saved, readback, detail, verified: true };
  } catch (error) {
    throw new ModeToolError(`写入已获确认，但读回验证失败；先 GET 模式/详情（创建先查列表），不要再次 POST。${error instanceof Error ? error.message : String(error)}`, "READBACK_FAILED", "acknowledged", error instanceof BridgeError ? error.status : 0);
  }
}
