import { readFileSync } from "node:fs";
import { BridgeClient, BridgeError } from "./bridge-client.js";
import { ModeToolError } from "./level-mode.js";
import type { JsonObject, LevelDetail, PerPlayerConfig } from "./types.js";

const schema = JSON.parse(readFileSync(new URL("../schemas/level-settings.schema.json", import.meta.url), "utf8"));
export const settingsToolDefinitions = [
  {
    name: "oc2_save_level_respawn_bounds",
    description: "保存重生边界布尔值。先 GET 详情保留完整基础标量，再 POST /api/level/info（updateEnableRespawnBounds=true），最后 GET 核对。confirm=true；失败禁止自动重试。",
    inputSchema: { ...schema.$defs.respawnBounds, $defs: schema.$defs },
  },
  {
    name: "oc2_save_native_scripted_recipes",
    description: "保存 Campaign 指定 playerCount=1..4 的原生序列。scriptedRecipes 为 GUID 有序数组，保留重复，[] 清空；不接受缺省/null。先 GET 详情合并该人数现有全部标量，仅 POST 此人数，再 GET 逐项核对。与启用的 Web OrderSequence 互斥，不自动清除。confirm=true；失败禁止自动重试。",
    inputSchema: { ...schema.$defs.nativeSequence, $defs: schema.$defs },
  },
];

const infoFields = ["levelName", "levelNameZH", "debugRecipeCount", "disableDynamicParenting", "minOrderCount", "maxOrderCount", "gridHalfSizeX", "gridHalfSizeY", "gridHalfSizeZ", "dependencies"] as const;
const playerFields = ["orderLifeTime", "timeBetweenOrders", "plateReturnTime", "survivalTimeMultiplier", "roundTime", "oneStarScore", "twoStarScore", "threeStarScore", "fourStarScore", "targetRepairCostMax"] as const;

function requireValue(condition: unknown, message: string): asserts condition {
  if (!condition) throw new ModeToolError(message, "INVALID_ARGUMENT");
}

function pick(source: JsonObject, fields: readonly string[]): JsonObject {
  return Object.fromEntries(fields.map((field) => {
    const value = source[field];
    const valid = field === "dependencies" ? Array.isArray(value) && value.every((v) => typeof v === "string")
      : field === "levelName" || field === "levelNameZH" ? typeof value === "string"
      : field === "disableDynamicParenting" ? typeof value === "boolean"
      : typeof value === "number" && Number.isFinite(value) && (field === "survivalTimeMultiplier" || Number.isInteger(value));
    if (!valid) throw new Error(`详情缺少有效 ${field}，未构造不完整保存请求`);
    return [field, value];
  }));
}

async function readDetail(bridge: BridgeClient, path: string): Promise<LevelDetail> {
  const detail = await bridge.get<LevelDetail>(`/api/level?assetPath=${encodeURIComponent(path)}`, 30000);
  if (!detail || detail.levelInfoAssetPath !== path || !["Campaign", "Dynamic", "Horde"].includes(detail.gameMode)) throw new Error("详情 GET 路径或模式无效");
  return detail;
}

export async function handleSettingsTool(name: string, input: JsonObject, bridge: BridgeClient): Promise<JsonObject> {
  const definition = settingsToolDefinitions.find((tool) => tool.name === name);
  requireValue(definition && input && typeof input === "object" && !Array.isArray(input), "未知工具或无效参数对象");
  const shape = definition.inputSchema;
  requireValue(Object.keys(input).every((key) => Object.hasOwn(shape.properties, key)), "包含未知参数");
  requireValue(input.confirm === true, "保存必须传 confirm=true");
  requireValue(typeof input.assetPath === "string" && input.assetPath.length <= 1024 && /^Assets\/LevelSets\/[^/\\]+\/data\/[^/\\]+\/[^/\\]+\.asset$/.test(input.assetPath), "assetPath 必须为后端返回的本关 LevelInfoSO 路径");
  const respawn = name === "oc2_save_level_respawn_bounds";
  if (respawn) requireValue(typeof input.enableRespawnBounds === "boolean", "enableRespawnBounds 必须为布尔值");
  else {
    requireValue(typeof input.playerCount === "number" && Number.isInteger(input.playerCount) && input.playerCount >= 1 && input.playerCount <= 4, "playerCount 必须为 1 到 4 的整数");
    requireValue(Array.isArray(input.scriptedRecipes) && input.scriptedRecipes.every((guid) => typeof guid === "string" && /^[0-9a-fA-F]{32}$/.test(guid)), "scriptedRecipes 必须为 GUID 数组；[] 清空，缺省/null 不可保存");
  }
  const path = input.assetPath;
  let body: JsonObject;
  try {
    const detail = await readDetail(bridge, path);
    if (respawn) body = { assetPath: path, ...pick(detail, infoFields), updateEnableRespawnBounds: true, enableRespawnBounds: input.enableRespawnBounds };
    else {
      requireValue(detail.gameMode === "Campaign", "仅 Campaign 可修改 scriptedRecipes（包括清空）");
      const config = detail.configs?.[Number(input.playerCount) - 1];
      requireValue(config?.exists === true, "缺少指定人数配置");
      body = { assetPath: path, [`config_${input.playerCount}p`]: { ...pick(config, playerFields), scriptedRecipes: [...input.scriptedRecipes as string[]] } };
    }
  } catch (error) {
    if (error instanceof ModeToolError) throw error;
    throw new ModeToolError(`保存前详情读取失败。${error instanceof Error ? error.message : String(error)}`, "BRIDGE_ERROR");
  }
  let saved: JsonObject;
  try {
    saved = await bridge.post<JsonObject>(respawn ? "/api/level/info" : "/api/level/config", body, 60000);
    if (!saved || saved.ok !== true) throw new Error("写响应未确认 ok=true");
  } catch (error) {
    throw new ModeToolError(`写结果未确认；先 GET 详情，禁止自动重试。${error instanceof Error ? error.message : String(error)}`, "WRITE_UNCONFIRMED", "unknown", error instanceof BridgeError ? error.status : 0);
  }
  try {
    const readback = await readDetail(bridge, path);
    if (respawn) {
      if (readback.enableRespawnBounds !== input.enableRespawnBounds) throw new Error("重生边界读回不一致");
    } else {
      const config: PerPlayerConfig | undefined = readback.configs?.[Number(input.playerCount) - 1];
      const wanted = input.scriptedRecipes as string[];
      if (readback.gameMode !== "Campaign" || config?.exists !== true || !Array.isArray(config.scriptedRecipes) || config.scriptedRecipes.length !== wanted.length || config.scriptedRecipes.some((guid, i) => typeof guid !== "string" || guid.toLowerCase() !== wanted[i].toLowerCase()) || !Array.isArray(config.scriptedRecipeErrors) || config.scriptedRecipeErrors.length !== 0) throw new Error("原生序列读回长度、顺序、重复或引用诊断不一致");
    }
    return { saved, readback, verified: true };
  } catch (error) {
    throw new ModeToolError(`保存已确认但读回失败；只 GET 详情核对，禁止再次 POST。${error instanceof Error ? error.message : String(error)}`, "READBACK_FAILED", "acknowledged", error instanceof BridgeError ? error.status : 0);
  }
}
