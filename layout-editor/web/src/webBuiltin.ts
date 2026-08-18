/**
 * Web 内置内容（Assets/common_w）前端门槛：/api/env/status 的 commonW 段
 * （后端实测存在性 + id 清单，envStatus.ts 启动时拉取一次）+ web-allowlist.json
 * （逐步放开白名单，手工维护）。
 *
 * 规则：
 *  - common_w 未装配（用户项目无 Assets/common_w 目录，commonW.exists=false）
 *    → 一切 web 内置内容禁用（白名单不生效）。
 *  - common_w 已装配 → 按白名单放开：菜谱按 type 组/单项 id；放开的菜谱自动带出
 *    所需食材、搅拌中间产物、烹饪/装盘步骤与工作站道具（依赖闭包）。
 *  - id 清单过滤（api.ts 数据层 + UI 选择层）：条目必须在后端扫描到的清单中 ——
 *    common_w 被 common2/commonN 更新移除部分内容时前端自动容忍缺失。
 */
import { STEP_UTENSILS } from "./recipeGroups";
import type { RecipeEntry } from "./types";
import { commonWStatus, initEnvStatus } from "./envStatus";

/** /api/env/status 的 commonW 段（后端从用户项目的 Assets/common_w 实测扫描）。 */
export interface CommonWManifest {
  exists: boolean;
  version: string;
  recipes: string[];
  ingredients: string[];
  prefabs: string[];
  cookingSteps: string[];
}

/** web-allowlist.json（手工维护，默认全关）。 */
export interface WebAllowlist {
  /** 菜谱 type 组开关（如 hotdog/hotpot/moonpie/mealdeal）。 */
  recipeGroups: Record<string, boolean>;
  recipes: string[];
  ingredients: string[];
  prefabs: string[];
  cookingSteps: string[];
}

const EMPTY_ALLOWLIST: WebAllowlist = {
  recipeGroups: {},
  recipes: [],
  ingredients: [],
  prefabs: [],
  cookingSteps: [],
};

let _manifest: CommonWManifest | null = null;
let _allowlist: WebAllowlist = EMPTY_ALLOWLIST;

/** 依赖闭包：已放开菜谱自动带出的食材/中间产物/步骤/工作站道具。 */
const _closure = {
  ingredients: new Set<string>(),
  intermediates: new Set<string>(),
  steps: new Set<string>(),
  prefabs: new Set<string>(),
};

/** node 型食材（匹配节点，无实体 prefab）→ 食材箱可生成的整食材；
 *  以及建箱等价替换（同 uID 节点的核心版/dlc04 版优先）。
 *  - 沙拉洋葱：原版 dlc11 关卡食材箱给整个沙拉洋葱（dlc11_onion_salad，prefab 型，
 *    切 8 刀变 ChoppedOnion_Salad 匹配 dlc11onion_salad 节点）；节点本体不能进食材箱
 *    （运行时 LoadAsset<GameObject> 为 null → PseudoPrefabDispenser.Setup NRE）。
 *  - DLC8 汉堡胚 → 核心汉堡面包（ChoppedBunSO）：二者携带的 IngredientOrderNode
 *    uID 同为 16088（bundle 实测），订单匹配完全等价；套餐统一用核心面包，
 *    不再依赖 bundle359 的 dlc08_choppedbun。
 *  - DLC10 火锅/拼盘食材 → DLC04 版（uID 718144~718150 一一相等，换皮复用）：
 *    dlc10_* 建箱统一映射到无前缀/dlc04 版（bundle226），少一个 bundle 依赖；
 *    混选两套换皮菜谱时也只建一套箱（uID 等价，一箱即匹配两套订单）。 */
export const NODE_INGREDIENT_SOURCES: Record<string, string> = {
  dlc11onion_salad: "dlc11_onion_salad",
  dlc08_bun: "ChoppedBunSO",
  dlc10_bokchoy: "bokchoy",
  dlc10_meat: "dlc04_meat",
  dlc10_orange: "dlc04_orange",
  dlc10_prawn: "dlc04_prawn",
  dlc10_grapes: "grapes",
  dlc10_noodles: "noodles",
  dlc10_peach: "peach",
};

async function fetchStatic<T>(fileName: string): Promise<T | null> {
  try {
    const r = await fetch(`/${fileName}`);
    if (!r.ok) return null;
    return (await r.json()) as T;
  } catch {
    return null;
  }
}

/** 启动时加载环境自检（commonW 段）+ 白名单（失败时按"完全禁用"处理；幂等）。 */
export async function initWebBuiltin(): Promise<void> {
  if (_loaded) return;
  _loaded = true;
  const [, allowlist] = await Promise.all([
    initEnvStatus(),
    fetchStatic<WebAllowlist>("web-allowlist.json"),
  ]);
  _manifest = commonWStatus();
  _allowlist = allowlist ?? EMPTY_ALLOWLIST;
}
let _loaded = false;

/** 环境自检刷新后同步 commonW 段（依赖状态检查弹窗"重新检查"）。
 *  注意：仅更新清单缓存；依赖闭包与已加载的食材/菜谱列表在下次拉取目录时生效。 */
export function syncWebBuiltinFromEnv(): void {
  _manifest = commonWStatus();
}

/** common_w 是否已装配（用户项目存在 Assets/common_w，由后端实测）。 */
export function commonWAvailable(): boolean {
  return !!_manifest && _manifest.exists;
}

type ManifestKind = "recipes" | "ingredients" | "prefabs" | "cookingSteps";

/** manifest id 清单成员判定（common_w 部分内容被移除时兜底为 false）。 */
export function webManifestHas(kind: ManifestKind, id: string): boolean {
  if (!commonWAvailable() || !_manifest) return false;
  return (_manifest[kind] ?? []).includes(id);
}

/** api.ts 数据层过滤：非 web 组原样保留；web 组必须命中 manifest 清单。 */
export function filterWebEntries<T extends { id: string; group?: string }>(
  kind: ManifestKind,
  entries: T[]
): T[] {
  return entries.filter((e) => e.group !== "web" || webManifestHas(kind, e.id));
}

/** 白名单直接判定（不含闭包）：菜谱按 type 组或单项 id 放开。 */
function isRecipeAllowlisted(r: { id: string; type?: string }): boolean {
  if (_allowlist.recipes.includes(r.id)) return true;
  return !!r.type && _allowlist.recipeGroups[r.type] === true;
}

/** 由当前菜谱目录计算依赖闭包（fetchRecipeCatalog 之后调用一次）。 */
export function computeWebClosure(recipes: RecipeEntry[]): void {
  _closure.ingredients.clear();
  _closure.intermediates.clear();
  _closure.steps.clear();
  _closure.prefabs.clear();
  if (!commonWAvailable()) return;

  const byId = new Map(recipes.map((r) => [r.id, r]));
  const queue: RecipeEntry[] = recipes.filter(
    (r) => r.group === "web" && webManifestHas("recipes", r.id) && isRecipeAllowlisted(r)
  );
  const seen = new Set<string>();
  const addStep = (step: string | undefined) => {
    if (!step) return;
    _closure.steps.add(step);
    for (const station of STEP_UTENSILS[step] ?? []) {
      if (webManifestHas("prefabs", station)) _closure.prefabs.add(station);
    }
  };
  while (queue.length) {
    const r = queue.pop()!;
    if (!seen.add(r.id)) continue;
    addStep(r.cookingStep);
    addStep(r.platingStep);
    for (const ing of r.ingredients ?? []) {
      if (!ing) continue;
      _closure.ingredients.add(ing);
      // node 型匹配节点同时带出其整食材（食材箱/加工的实际来源）
      const source = NODE_INGREDIENT_SOURCES[ing];
      if (source) _closure.ingredients.add(source);
    }
    for (const cid of r.compositionIds ?? []) {
      const sub = byId.get(cid);
      if (sub && sub.group === "web" && webManifestHas("recipes", sub.id) && !seen.has(sub.id)) {
        _closure.intermediates.add(sub.id);
        queue.push(sub);
      } else if (!sub) {
        // 组成项不是菜谱（直接是食材）时按食材带出
        _closure.ingredients.add(cid);
      }
    }
  }
}

/** UI 选择层：web 内置菜谱是否已放开（白名单或作为已放开菜谱的中间产物）。 */
export function isWebRecipeEnabled(r: { id: string; type?: string; group?: string }): boolean {
  if (r.group !== "web") return true;
  if (!webManifestHas("recipes", r.id)) return false;
  return isRecipeAllowlisted(r) || _closure.intermediates.has(r.id);
}

/** UI 选择层：web 内置食材是否已放开（白名单或依赖闭包）。 */
export function isWebIngredientEnabled(ing: { id: string; group?: string }): boolean {
  if (ing.group !== "web") return true;
  if (!webManifestHas("ingredients", ing.id)) return false;
  return _allowlist.ingredients.includes(ing.id) || _closure.ingredients.has(ing.id);
}

/** UI 选择层：web 内置烹饪/装盘步骤是否已放开。 */
export function isWebCookingStepEnabled(step: { id: string; group?: string }): boolean {
  if (step.group !== "web") return true;
  if (!webManifestHas("cookingSteps", step.id)) return false;
  return _allowlist.cookingSteps.includes(step.id) || _closure.steps.has(step.id);
}

/** UI 选择层：web 内置道具（common_w prefab）是否已放开。
 *  核心层道具已全量放开：不再按 allowlist / 依赖闭包过滤，仅保留 manifest 门槛
 *  （common_w 未装配 / 条目被内容更新移除时禁用；消费方必须容忍条目缺失）。 */
export function isWebPrefabEnabled(id: string, assetPath?: string): boolean {
  const isWeb = (assetPath ?? "").includes("/common_w/") || webManifestHas("prefabs", id);
  if (!isWeb) return true;
  return webManifestHas("prefabs", id);
}

/** web 内置道具的禁用原因（null = 可用或非 web 道具；调色板/组合判断用）。 */
export function webPrefabDisabledReason(id: string, assetPath?: string): string | null {
  const isWeb = (assetPath ?? "").includes("/common_w/") || webManifestHas("prefabs", id);
  if (!isWeb) return null;
  if (webManifestHas("prefabs", id)) return null;
  if (!commonWAvailable()) return "common_w 未装配（Assets/common_w 不存在），见「🩺 依赖检查」";
  return "common_w 中无此道具（可能已被内容更新移除）";
}

/** web 内置菜谱的禁用原因（null = 可用或非 web 菜谱；清单页置灰展示用）。 */
export function webRecipeDisabledReason(r: { id: string; type?: string; group?: string }): string | null {
  if (r.group !== "web") return null;
  if (isWebRecipeEnabled(r)) return null;
  if (!commonWAvailable()) return "common_w 未装配（Assets/common_w 不存在），见「🩺 依赖检查」";
  if (!webManifestHas("recipes", r.id)) return "common_w 中无此菜谱（可能已被内容更新移除）";
  return "未在 web-allowlist.json 中放开";
}

/** web 内置食材的禁用原因（null = 可用或非 web 食材）。 */
export function webIngredientDisabledReason(ing: { id: string; group?: string }): string | null {
  if (ing.group !== "web") return null;
  if (isWebIngredientEnabled(ing)) return null;
  if (!commonWAvailable()) return "common_w 未装配（Assets/common_w 不存在），见「🩺 依赖检查」";
  if (!webManifestHas("ingredients", ing.id)) return "common_w 中无此食材（可能已被内容更新移除）";
  return "未被已放开的菜谱带出（或 web-allowlist.json 未列出）";
}

/** 当前白名单（调试/管理页展示用）。 */
export function webAllowlist(): WebAllowlist {
  return _allowlist;
}
