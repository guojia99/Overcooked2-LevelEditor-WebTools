import type { LayoutDocument, LayoutItem, PerPlayerConfig, RecipeEntry, WorkloadMode } from "../types";
import {
  CHOP_COUNTER_RULE,
  PICKUP_SEC,
  PLATE_TAKE_SEC,
  QUEUE_PENALTY,
  SERVE_ACTION_SEC,
  STATION_CLASS_BY_ID,
  STATION_ZONE,
  STEP_TO_UTENSIL,
  UTENSIL_KIND_BY_ID,
  UTENSIL_RULES,
  WALK_CELLS_PER_SEC,
  WASH_PER_ITEM_SEC,
  ingredientNeedsChop,
  type KitchenZone,
  type StationClass,
  type UtensilKind,
} from "../autoScoreKnowledge";
import {
  analyzeKitchen,
  effectiveChopPerItemSec,
  hazardMultiplier,
  kitchenWarnings,
  type KitchenStats,
} from "../kitchenAnalysis";
/** 与 editor/state CELL 同值（1.2m/格）。 */
const CELL = 1.2;

export const WORKLOAD_ALGORITHM_VERSION = 3;
/** 玩家涂抹色：刻意避开画布物件色（工作台 counters≈#76a8ff、装饰橙、器具粉等）。 */
export const WORKLOAD_COLORS = ["#FF5C7A", "#00D9C8", "#FFC940", "#C77DFF"];
/** 填充 / 描边透明度（8 位 hex alpha，叠加在场景之上）。 */
export const WORKLOAD_FILL_ALPHA = "99";
export const WORKLOAD_STROKE_ALPHA = "EE";
/** @deprecated v3 使用 WASH_PER_ITEM_SEC；保留供快照兼容 */
export const DEFAULT_WASH_SEC = WASH_PER_ITEM_SEC;
/** @deprecated v3 使用 WALK_CELLS_PER_SEC；保留供快照兼容 */
export const DEFAULT_WALK_SPEED = WALK_CELLS_PER_SEC * CELL;

export type WorkloadPhase = "fetch" | "prep" | "cook" | "plate" | "serve" | "wash";

export interface WorkloadCell { x: number; z: number; }

export interface PlayerWorkloadBreakdown {
  fetch: number;
  prep: number;
  cook: number;
  plate: number;
  serve: number;
  wash: number;
  walk: number;
}

export interface WorkloadResultView {
  percentages: number[];
  seconds: number[];
  breakdown: PlayerWorkloadBreakdown[];
  overlap: Array<{ key: string; seconds: number; share: number }>;
  ignored: number;
  warning: boolean;
  warningGap: number;
  warnings: string[];
  algorithmVersion: typeof WORKLOAD_ALGORITHM_VERSION;
}

interface Pt { x: number; z: number; }

export interface WorkloadStep {
  phase: WorkloadPhase;
  pos: Pt;
  actionSec: number;
  cell: string;
}

interface StationSite {
  item: LayoutItem;
  pos: Pt;
  zone: KitchenZone;
  stationClass?: StationClass;
  utensilKind?: UtensilKind;
}

interface StationIndex {
  byZone: Record<KitchenZone, StationSite[]>;
  byClass: Partial<Record<StationClass, StationSite[]>>;
  byUtensil: Partial<Record<UtensilKind, StationSite[]>>;
  centroids: Record<KitchenZone, Pt>;
}

function emptyBreakdown(): PlayerWorkloadBreakdown {
  return { fetch: 0, prep: 0, cook: 0, plate: 0, serve: 0, wash: 0, walk: 0 };
}

/** 工作量格子键（世界坐标 → 整格索引，与画布涂抹一致）。 */
export function workloadCellKey(x: number, z: number): string {
  return `${Math.round(x / CELL)},${Math.round(z / CELL)}`;
}

function itemCatalogId(it: LayoutItem): string {
  const p = it.prefabAssetPath || "";
  const base = p.slice(Math.max(p.lastIndexOf("/"), p.lastIndexOf("\\")) + 1).replace(/\.prefab$/, "");
  if (base && STATION_CLASS_BY_ID[base] !== undefined) return base;
  const name = (it.displayName || "").replace(/\(Clone\)$/, "").trim();
  if (name && (STATION_CLASS_BY_ID[name] !== undefined || UTENSIL_KIND_BY_ID[name] !== undefined)) return name;
  return base || name;
}

function itemPos(it: LayoutItem): Pt | null {
  const p = it.worldPosition ?? it.localPosition;
  if (!p || typeof p.x !== "number" || typeof p.z !== "number") return null;
  return { x: p.x, z: p.z };
}

function distMeters(a: Pt, b: Pt): number {
  return Math.hypot(a.x - b.x, a.z - b.z);
}

function zoneCentroid(sites: StationSite[]): Pt {
  if (!sites.length) return { x: 0, z: 0 };
  let x = 0;
  let z = 0;
  for (const s of sites) {
    x += s.pos.x;
    z += s.pos.z;
  }
  return { x: x / sites.length, z: z / sites.length };
}

function emptyZones(): Record<KitchenZone, StationSite[]> {
  return { source: [], prep: [], cook: [], serve: [], wash: [], plate: [] };
}

/** 解析场景站点并按功能分区索引（与 kitchenAnalysis / 定分同源）。 */
export function indexStations(doc: LayoutDocument): StationIndex {
  const byZone = emptyZones();
  const byClass: Partial<Record<StationClass, StationSite[]>> = {};
  const byUtensil: Partial<Record<UtensilKind, StationSite[]>> = {};

  for (const it of doc.items ?? []) {
    if (it.stubKind === "Player") continue;
    const pos = itemPos(it);
    if (!pos) continue;
    const id = itemCatalogId(it);
    const utensilKind = UTENSIL_KIND_BY_ID[id];
    if (utensilKind) {
      const site: StationSite = { item: it, pos, zone: "cook", utensilKind };
      byUtensil[utensilKind] = byUtensil[utensilKind] ?? [];
      byUtensil[utensilKind]!.push(site);
      byZone.cook.push(site);
      continue;
    }
    const cls = STATION_CLASS_BY_ID[id];
    if (!cls) continue;
    const zone = STATION_ZONE[cls];
    if (!zone) continue;
    const site: StationSite = { item: it, pos, zone, stationClass: cls };
    byZone[zone].push(site);
    byClass[cls] = byClass[cls] ?? [];
    byClass[cls]!.push(site);
  }

  const centroids: Record<KitchenZone, Pt> = {
    source: zoneCentroid(byZone.source),
    prep: zoneCentroid(byZone.prep),
    cook: zoneCentroid(byZone.cook),
    serve: zoneCentroid(byZone.serve),
    wash: zoneCentroid(byZone.wash),
    plate: zoneCentroid(byZone.plate),
  };
  return { byZone, byClass, byUtensil, centroids };
}

/** 相对参考点选欧氏最近站点。 */
export function nearestStation(sites: StationSite[], from: Pt): StationSite | null {
  if (!sites.length) return null;
  let best = sites[0];
  let min = distMeters(from, best.pos);
  for (let i = 1; i < sites.length; i++) {
    const d = distMeters(from, sites[i].pos);
    if (d < min) {
      min = d;
      best = sites[i];
    }
  }
  return best;
}

function sitesInPlayerArea(sites: StationSite[], playerArea: Set<string> | null): StationSite[] {
  if (!playerArea) return sites;
  return sites.filter((s) => playerArea.has(workloadCellKey(s.pos.x, s.pos.z)));
}

function pickZone(
  index: StationIndex,
  zone: KitchenZone,
  from: Pt,
  warnings: string[],
  label: string,
  playerArea: Set<string> | null
): StationSite | null {
  const pool = sitesInPlayerArea(index.byZone[zone], playerArea);
  const site = nearestStation(pool, from);
  if (site) return site;
  if (playerArea) return null;
  warnings.push(`场景缺少${label}，估时位置回退到${zone}区质心`);
  return { item: {} as LayoutItem, pos: index.centroids[zone], zone };
}

function pickClass(
  index: StationIndex,
  cls: StationClass,
  from: Pt,
  warnings: string[],
  label: string,
  playerArea: Set<string> | null
): StationSite | null {
  const pool = sitesInPlayerArea(index.byClass[cls] ?? [], playerArea);
  const site = nearestStation(pool, from);
  if (site) return site;
  const zone = STATION_ZONE[cls];
  if (zone) return pickZone(index, zone, from, warnings, label, playerArea);
  if (playerArea) return null;
  warnings.push(`场景缺少${label}，估时位置回退到场景原点`);
  return { item: {} as LayoutItem, pos: { x: 0, z: 0 }, zone: "cook" };
}

function utensilQueueMult(kitchen: KitchenStats, kind: UtensilKind, players: number): number {
  const info = kitchen.utensils[kind];
  const supply = info?.count ?? 0;
  if (supply >= players) return 1;
  const demand = Math.max(1, players);
  return 1 + QUEUE_PENALTY * ((demand - supply) / demand);
}

function recipeUtensilKinds(r: RecipeEntry): UtensilKind[] {
  const groups = r.cookingGroups ?? [];
  const fromGroups = groups.map((g) => g.step).filter((s) => STEP_TO_UTENSIL[s] !== undefined);
  if (fromGroups.length) return fromGroups.map((s) => STEP_TO_UTENSIL[s]);
  const step = r.cookingStep ?? "";
  return STEP_TO_UTENSIL[step] !== undefined ? [STEP_TO_UTENSIL[step]] : [];
}

function pickUtensilSite(
  index: StationIndex,
  kind: UtensilKind,
  from: Pt,
  warnings: string[],
  playerArea: Set<string> | null
): StationSite | null {
  const utensilSites = sitesInPlayerArea(index.byUtensil[kind] ?? [], playerArea);
  const fromUtensil = nearestStation(utensilSites, from);
  if (fromUtensil) return fromUtensil;
  const rule = UTENSIL_RULES[kind];
  if (rule.station) {
    return pickClass(index, rule.station, from, warnings, kind, playerArea);
  }
  return pickZone(index, "cook", from, warnings, kind, playerArea);
}

function makeStep(phase: WorkloadPhase, site: StationSite, actionSec: number): WorkloadStep {
  return {
    phase,
    pos: site.pos,
    actionSec,
    cell: workloadCellKey(site.pos.x, site.pos.z),
  };
}

/** 将菜谱展开为有序工序链。
 *  playerArea 非空时只使用落在该玩家涂抹格内的站点（两侧各有切菜台则各自计入切配）。 */
export function buildRecipeSteps(
  doc: LayoutDocument,
  recipe: RecipeEntry,
  kitchen: KitchenStats,
  playerCount: number,
  warnings: string[],
  playerArea: Set<string> | null = null
): WorkloadStep[] {
  const index = indexStations(doc);
  const steps: WorkloadStep[] = [];
  let carryPos: Pt | null = null;
  const ref = (): Pt => carryPos ?? index.centroids.source;

  const ingredients = recipe.ingredients ?? [];
  const ingIds = ingredients.length
    ? ingredients
    : Array.from({ length: Math.max(1, recipe.ingredientCount ?? 1) }, () => "unknown");

  const chopPerItem = effectiveChopPerItemSec(kitchen) ?? CHOP_COUNTER_RULE.perItemSec;
  const needsPlate = !!recipe.platingStep || !!recipe.type;

  const pushStep = (phase: WorkloadPhase, site: StationSite | null, actionSec: number): boolean => {
    if (!site) return false;
    steps.push(makeStep(phase, site, actionSec));
    carryPos = site.pos;
    return true;
  };

  for (const ingId of ingIds) {
    const source = pickZone(index, "source", ref(), warnings, "食材箱", playerArea);
    pushStep("fetch", source, PICKUP_SEC);

    if (ingredientNeedsChop(ingId)) {
      const prep = pickZone(index, "prep", ref(), warnings, "切配台", playerArea);
      pushStep("prep", prep, chopPerItem);
    }
  }

  for (const kind of recipeUtensilKinds(recipe)) {
    const site = pickUtensilSite(index, kind, ref(), warnings, playerArea);
    const base = kitchen.utensils[kind]?.cookSec ?? UTENSIL_RULES[kind].cookSec;
    const actionSec = base * utensilQueueMult(kitchen, kind, playerCount);
    pushStep("cook", site, actionSec);
  }

  if (needsPlate) {
    const platePool = sitesInPlayerArea(
      index.byZone.plate.length ? index.byZone.plate : index.byZone.wash,
      playerArea
    );
    const plateZone: KitchenZone = index.byZone.plate.length ? "plate" : "wash";
    const plateLabel = index.byZone.plate.length ? "干净餐具堆" : "餐具区";
    const plate = nearestStation(platePool, ref()) ?? pickZone(index, plateZone, ref(), warnings, plateLabel, playerArea);
    if (!pushStep("plate", plate, PLATE_TAKE_SEC)) {
      // 装盘失败不阻断上菜估时
    }
  }

  const serve = pickZone(index, "serve", ref(), warnings, "上菜台", playerArea);
  pushStep("serve", serve, SERVE_ACTION_SEC);

  if (needsPlate) {
    const wash = pickZone(index, "wash", ref(), warnings, "水槽", playerArea);
    pushStep("wash", wash, WASH_PER_ITEM_SEC);
  }

  return steps;
}

function areaPlayers(cell: string, areas: string[][]): number[] {
  const out: number[] = [];
  for (let i = 0; i < areas.length; i++) {
    if (areas[i].includes(cell)) out.push(i);
  }
  return out;
}

function walkSecBetween(from: Pt, to: Pt): number {
  return distMeters(from, to) / CELL / WALK_CELLS_PER_SEC;
}

export function analyzeWorkload(
  doc: LayoutDocument,
  recipes: RecipeEntry[],
  config: PerPlayerConfig | undefined,
  mode: WorkloadMode,
  areas: string[][],
  _washSec = WASH_PER_ITEM_SEC,
  _walkSpeed = DEFAULT_WALK_SPEED
): WorkloadResultView {
  const n = Number(mode[0]);
  const kitchen = analyzeKitchen(doc);
  const hazardMult = hazardMultiplier(kitchen);
  const stepWarnings: string[] = [];
  const layoutWarnings = kitchenWarnings(kitchen, recipes);

  const weights = recipes.map((r) => Math.max(0, r.weight ?? 1));
  const weightTotal = weights.reduce((a, b) => a + b, 0) || recipes.length || 1;
  const orderCount = Math.max(
    1,
    Math.round(((config?.roundTime || 240) / Math.max(1, config?.timeBetweenOrders || 10)) * 10) / 10
  );

  const seconds = new Array(n).fill(0);
  const breakdown: PlayerWorkloadBreakdown[] = Array.from({ length: n }, () => emptyBreakdown());
  const overlap = new Map<string, number>();
  let ignored = 0;

  const playerAreas = areas.map((cells) => new Set(cells));

  for (let ri = 0; ri < recipes.length; ri++) {
    const recipe = recipes[ri];
    const expected = orderCount * (weights[ri] || 1) / weightTotal;

    for (let p = 0; p < n; p++) {
      const steps = buildRecipeSteps(doc, recipe, kitchen, n, stepWarnings, playerAreas[p]);
      let carryPos: Pt | null = null;

      for (const step of steps) {
        const walkSec = carryPos ? walkSecBetween(carryPos, step.pos) : 0;
        carryPos = step.pos;

        const covered = areaPlayers(step.cell, areas);
        if (!covered.length) {
          ignored++;
          continue;
        }

        const share = 1 / covered.length;
        const fullStepSec = (step.actionSec + walkSec) * hazardMult * expected;
        const actionWeighted = step.actionSec * hazardMult * expected * share;
        const walkWeighted = walkSec * hazardMult * expected * share;

        if (covered.length > 1) {
          const overlapKey = covered.map((i) => i + 1).join("/");
          overlap.set(overlapKey, (overlap.get(overlapKey) ?? 0) + fullStepSec);
        }

        // 工序链按玩家区域内站点独立展开；重叠格仍均分
        if (!covered.includes(p)) continue;

        seconds[p] += actionWeighted + walkWeighted;
        breakdown[p][step.phase] += actionWeighted;
        breakdown[p].walk += walkWeighted;
      }
    }
  }

  const total = seconds.reduce((a, b) => a + b, 0);
  const percentages = total > 0 ? seconds.map((s) => s / total * 100) : seconds.map(() => 0);
  const values = percentages.filter((v) => v > 0);
  const warningGap = values.length > 1 ? Math.max(...values) - Math.min(...values) : 0;

  const warnings = [...new Set([...layoutWarnings, ...stepWarnings])];

  return {
    percentages,
    seconds,
    breakdown,
    overlap: [...overlap.entries()].map(([key, sec]) => ({
      key,
      seconds: sec,
      share: total ? sec / total * 100 : 0,
    })),
    ignored,
    warning: warningGap >= 5,
    warningGap,
    warnings,
    algorithmVersion: WORKLOAD_ALGORITHM_VERSION,
  };
}
