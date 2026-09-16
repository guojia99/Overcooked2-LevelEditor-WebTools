/**
 * 厨房分析 —— 解析场景布局（/api/scene/layout 的 LayoutDocument），
 * 为一键定分提供当前图核心层的设备清单、分区步行耗时与告警。
 *
 * 分类依据 autoScoreKnowledge.ts 的站点/锅具知识表；
 * 距离用 worldPosition（米）÷1.2 折算格数，再按步行速度折秒。
 * 分区：source 食材箱 → prep 切配 → cook 烹饪 → plate 取餐具 / serve 上菜 / wash 洗盘。
 */

import type { LayoutDocument, LayoutItem, RecipeEntry } from "./types";
import {
  CHOP_COUNTER_RULE,
  HAZARD_PER_UNIT,
  HAZARD_MULT_MAX,
  HAZARD_MULT_MIN,
  STATION_CLASS_BY_ID,
  STATION_CLASS_ZH,
  STATION_ZONE,
  STEP_TO_UTENSIL,
  UTENSIL_KIND_BY_ID,
  UTENSIL_KIND_ZH,
  UTENSIL_RULES,
  WALK_CELLS_PER_SEC,
  guillotinePerItemSec,
  ingredientNeedsChop,
  type KitchenZone,
  type StationClass,
  type UtensilKind,
} from "./autoScoreKnowledge";

const CELL_METERS = 1.2;

interface Pt {
  x: number;
  z: number;
}

export interface KitchenUtensilInfo {
  count: number;
  capacity: number;
  cookSec: number;
  station: StationClass | null;
}

export interface KitchenStats {
  /** 核心层物件总数（参与分类的）。 */
  coreItemCount: number;
  /** 站点分类计数。 */
  stations: Partial<Record<StationClass, number>>;
  /** 锅具聚合（容量/时长为场景 stub 覆盖后的值）。 */
  utensils: Partial<Record<UtensilKind, KitchenUtensilInfo>>;
  /** 断头台总数 / 已接按钮数。 */
  guillotineTotal: number;
  guillotineWithButton: number;
  /** 分区步行秒数。 */
  walk: {
    srcToPrepSec: number;
    srcToCookSec: number;
    prepToCookSec: number;
    cookToServeSec: number;
    cookToPlateSec: number;
  };
  /** 干扰/机制计数（风险乘数用）。 */
  hazards: { burner: number; wind: number; travelator: number; conveyor: number; teleportal: number };
}

/** prefabAssetPath 基名（无 .prefab）→ 分类 id；回退 displayName（去 (Clone)）。 */
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

/** from 区每点到 to 区最近点的平均步行秒数（任一区为空返回 0）。 */
function avgMinWalkSec(from: Pt[], to: Pt[]): number {
  if (!from.length || !to.length) return 0;
  let total = 0;
  for (const a of from) {
    let min = Infinity;
    for (const b of to) min = Math.min(min, distMeters(a, b));
    total += min;
  }
  return total / from.length / CELL_METERS / WALK_CELLS_PER_SEC;
}

function emptyStats(): KitchenStats {
  return {
    coreItemCount: 0,
    stations: {},
    utensils: {},
    guillotineTotal: 0,
    guillotineWithButton: 0,
    walk: { srcToPrepSec: 0, srcToCookSec: 0, prepToCookSec: 0, cookToServeSec: 0, cookToPlateSec: 0 },
    hazards: { burner: 0, wind: 0, travelator: 0, conveyor: 0, teleportal: 0 },
  };
}

/** 解析场景布局 → 厨房统计。doc 为空（取不到布局）时返回全零统计。 */
export function analyzeKitchen(doc: LayoutDocument | null | undefined): KitchenStats {
  const stats = emptyStats();
  if (!doc || !Array.isArray(doc.items)) return stats;

  const zones: Record<KitchenZone, Pt[]> = { source: [], prep: [], cook: [], serve: [], wash: [], plate: [] };
  const utensilItems = new Map<string, LayoutItem[]>();

  for (const it of doc.items) {
    const id = itemCatalogId(it);
    const utensilKind = UTENSIL_KIND_BY_ID[id];
    if (utensilKind) {
      const arr = utensilItems.get(utensilKind) ?? [];
      arr.push(it);
      utensilItems.set(utensilKind, arr);
      stats.coreItemCount++;
    }
    const cls = STATION_CLASS_BY_ID[id];
    if (!cls) continue;
    stats.stations[cls] = (stats.stations[cls] ?? 0) + 1;
    stats.coreItemCount++;
    const zone = STATION_ZONE[cls];
    const pos = itemPos(it);
    if (zone && pos) zones[zone].push(pos);
    if (cls === "burner") stats.hazards.burner++;
    else if (cls === "wind") stats.hazards.wind++;
    else if (cls === "travelator") stats.hazards.travelator++;
    else if (cls === "conveyor") stats.hazards.conveyor++;
    else if (cls === "teleportal") stats.hazards.teleportal++;
  }

  // 锅具聚合：容量/时长优先读场景 cookingUtensil stub（UtensilTiming 自定值）
  for (const [kind, arr] of utensilItems) {
    const rule = UTENSIL_RULES[kind as UtensilKind];
    let capacity = rule.capacity;
    let cookSec = rule.cookSec;
    for (const it of arr) {
      const stub = it.cookingUtensil;
      if (!stub) continue;
      if (stub.capacity && stub.capacity > 0) capacity = Math.max(capacity, stub.capacity);
      if (stub.cookTime && stub.cookTime > 0) cookSec = stub.cookTime;
      else if (stub.mixTime && stub.mixTime > 0) cookSec = stub.mixTime;
    }
    stats.utensils[kind as UtensilKind] = { count: arr.length, capacity, cookSec, station: rule.station };
  }

  // 断头台按钮：switchLinks 的目标是断头台 → 记为“带按钮”（严格按联动数据判定，
  // 未接按钮的断头台无法落刀，由 kitchenWarnings 提示并按切菜台 3s/个回退估时）
  stats.guillotineTotal = stats.stations.guillotine ?? 0;
  const byInstance = new Map(doc.items.map((it) => [it.instanceId, it]));
  const linkedGuillotines = new Set<string>();
  for (const link of doc.switchLinks ?? []) {
    const target = byInstance.get(link.targetId);
    if (target && STATION_CLASS_BY_ID[itemCatalogId(target)] === "guillotine") {
      linkedGuillotines.add(link.targetId);
    }
  }
  stats.guillotineWithButton = Math.min(stats.guillotineTotal, linkedGuillotines.size);

  stats.walk = {
    srcToPrepSec: avgMinWalkSec(zones.source, zones.prep),
    srcToCookSec: avgMinWalkSec(zones.source, zones.cook),
    prepToCookSec: avgMinWalkSec(zones.prep, zones.cook),
    cookToServeSec: avgMinWalkSec(zones.cook, zones.serve),
    // 无干净餐具堆时退化到洗盘区取具
    cookToPlateSec: zones.plate.length ? avgMinWalkSec(zones.cook, zones.plate) : avgMinWalkSec(zones.cook, zones.wash),
  };
  return stats;
}

/** 风险乘数（明火/风雪/步道/传送带拖慢，传送门提速；夹在 [0.95, 1.25]）。 */
export function hazardMultiplier(stats: KitchenStats): number {
  const h = stats.hazards;
  const raw =
    1 +
    h.burner * HAZARD_PER_UNIT.burner +
    h.wind * HAZARD_PER_UNIT.wind +
    h.travelator * HAZARD_PER_UNIT.travelator +
    h.conveyor * HAZARD_PER_UNIT.conveyor +
    h.teleportal * HAZARD_PER_UNIT.teleportal;
  return Math.min(HAZARD_MULT_MAX, Math.max(HAZARD_MULT_MIN, raw));
}

/** 有效单件切时：断头台（带按钮）与切菜台按供给量加权；
 *  无任何可用切配台（含断头台全未接按钮）返回 null（调用方告警并回退 3s/个）。 */
export function effectiveChopPerItemSec(stats: KitchenStats): number | null {
  const chopCount = stats.stations.chop ?? 0;
  const guillotine = stats.guillotineWithButton;
  if (chopCount + guillotine === 0) return null;
  const total = chopCount + guillotine;
  return (guillotine * guillotinePerItemSec() + chopCount * CHOP_COUNTER_RULE.perItemSec) / total;
}

/** 摘要 chips：站点计数 + 断头台按钮 + 步行概览。 */
export function kitchenChips(stats: KitchenStats): string[] {
  const chips: string[] = [];
  const order: StationClass[] = [
    "dispenser",
    "randomDispenser",
    "chop",
    "guillotine",
    "hob",
    "fryStation",
    "oven",
    "barbeque",
    "campfire",
    "stoneFurnace",
    "hotpotBurner",
    "mixer",
    "blender",
    "sink",
    "plateReturn",
    "plateStack",
    "serving",
    "burner",
    "conveyor",
    "teleportal",
    "travelator",
    "wind",
    "switch",
    "pressureSwitch",
  ];
  for (const cls of order) {
    const n = stats.stations[cls];
    if (!n) continue;
    let label = `${STATION_CLASS_ZH[cls]}×${n}`;
    if (cls === "guillotine") label += stats.guillotineWithButton > 0 ? `（带按钮×${stats.guillotineWithButton}）` : "（未接按钮）";
    chips.push(label);
  }
  for (const [kind, info] of Object.entries(stats.utensils)) {
    if (info && info.count > 0) {
      chips.push(`${UTENSIL_KIND_ZH[kind as UtensilKind]}×${info.count}（${info.cookSec}s·容${info.capacity}）`);
    }
  }
  return chips;
}

/** 菜谱步骤收集（cookingGroups 优先，回退 cookingStep）。 */
function recipeSteps(r: RecipeEntry): string[] {
  const groups = (r as { cookingGroups?: Array<{ step: string }> }).cookingGroups;
  const fromGroups = (groups ?? []).map((g) => g.step).filter((s) => STEP_TO_UTENSIL[s] !== undefined);
  if (fromGroups.length) return fromGroups;
  const step = r.cookingStep ?? "";
  return STEP_TO_UTENSIL[step] !== undefined ? [step] : [];
}

/** 场景 × 菜谱交叉校验告警。 */
export function kitchenWarnings(stats: KitchenStats, recipes: RecipeEntry[]): string[] {
  const warns: string[] = [];
  const needUtensils = new Set<UtensilKind>();
  let needChop = false;
  let needPlating = false;
  for (const r of recipes) {
    for (const step of recipeSteps(r)) needUtensils.add(STEP_TO_UTENSIL[step]);
    if ((r.ingredients ?? []).some((i) => ingredientNeedsChop(i))) needChop = true;
    if (r.platingStep) needPlating = true;
  }

  for (const kind of needUtensils) {
    const info = stats.utensils[kind];
    const zh = UTENSIL_KIND_ZH[kind];
    if (!info || info.count === 0) {
      warns.push(`菜谱需要${zh}，但场景未放置（估时按基准 ${UTENSIL_RULES[kind].cookSec}s）`);
      continue;
    }
    const station = UTENSIL_RULES[kind].station;
    if (station && (stats.stations[station] ?? 0) === 0) {
      warns.push(`${zh}×${info.count} 没有可用的${STATION_CLASS_ZH[station]}`);
    }
  }

  if (needChop) {
    const chopCount = stats.stations.chop ?? 0;
    if (chopCount + stats.guillotineTotal === 0) {
      warns.push("菜谱含需切食材，但场景没有切菜台/断头台");
    } else if (stats.guillotineTotal > stats.guillotineWithButton) {
      warns.push(`断头台×${stats.guillotineTotal} 未接按钮无法落刀（该部分按切菜台 3s/个估时）`);
    }
  }

  if (needPlating) {
    const stacks = stats.stations.plateStack ?? 0;
    const returns = stats.stations.plateReturn ?? 0;
    const sinks = stats.stations.sink ?? 0;
    if (stacks === 0 && (returns === 0 || sinks === 0)) {
      warns.push("无干净餐具堆，且缺少「回收台 + 水槽」组合，餐具循环不成立");
    } else if (stacks === 0 && sinks === 0) {
      warns.push("脏盘会回盘但没有水槽，无法清洗");
    }
  }

  if ((stats.stations.serving ?? 0) === 0) {
    warns.push("场景没有上菜台");
  }
  return warns;
}
