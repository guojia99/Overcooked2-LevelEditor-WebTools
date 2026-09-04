#!/usr/bin/env node
/**
 * gen-backup-level1_4-layout.mjs — backup/level1_4「枫醉秋亭」
 *
 * 厨房（地板 + Structure 墙体 + 房内装饰 + 空气墙 + 4 出生点 + 相机/灯光）：
 *   逐字复制**手改版** s_jia_level1_4（GET 实时读取当前场景，不用旧脚本数学）。
 *   厨房内部不新增任何元素。
 *   手改版结构：264 块 floor_carpet_purple 直接挂 Art/（非 Art/MainIsland），
 *   房内竹景在 Art/Decoration（沿用）；Art/Backdrop 旧外围件不复制（外围重建）。
 *   地板可走：导出器恒报 item.walkable=false，真正可走信息在 doc.walkable
 *   （264 面与地板 1:1）——写回时地板置 walkable=true 由 syncWalkable 重建。
 *   手改版北墙外 j∈[wj1+2..wj1+4] 有悬空地毯露台，北侧园林整体北移避让。
 * 外围布景：dlc04 + dlc13 中式园林 ——
 *   南：石板路 + 落地灯笼 + 红叶树入口；
 *   东西两翼：竹林 / 红叶树 / 假山 / 红灌木；
 *   北侧：露台夹景树 + 水塘睡莲 + 莲烛 + 小桥 + 宝塔(亭) + 孔明灯 + 龙拱门。
 * 依赖桥：Unity → Tools → Layout Editor → 启动服务（http://127.0.0.1:8765）。
 *
 * 用法：
 *   node gen-backup-level1_4-layout.mjs            # 只生成 + 打印摘要 + 写 .opencode/bk14-doc.json
 *   node gen-backup-level1_4-layout.mjs --apply    # 建档(若缺) + 写回布局 + KillPlane + LevelInfo + 校验
 */

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const CELL = 1.2;
const HALF = 0.6;
const FLOOR_Y = 0;

const SET = "backup";
const LEVEL_ID = "level1_4";
const LEVEL_NAME = "Level 1-4 Maple Drunken Pavilion";
const LEVEL_NAME_ZH = "枫醉秋亭";

const SRC_SCENE = "Assets/LevelSets/jia_carnival/scenes/s_jia_level1_4.unity";
const SRC_INFO = "Assets/LevelSets/jia_carnival/data/jia_level1_4/LevelInfo_jia_level1_4.asset";
const DST_SCENE = "Assets/LevelSets/backup/scenes/s_level1_4.unity";
const DST_INFO = "Assets/LevelSets/backup/data/level1_4/LevelInfo_level1_4.asset";

const cx = (i) => +(HALF + CELL * i).toFixed(4);
const cz = (j) => +(HALF + CELL * j).toFixed(4);
const cellOf = (v) => Math.round((v - HALF) / CELL);
const key = (i, j) => `${i},${j}`;

const pf = (id, guid, rel, extra = {}) => ({
  id, guid, assetPath: `Assets/common03/prefabs/${rel}`,
  rotX: 0, scale: { x: 1, y: 1, z: 1 },
  footprint: { cellsX: 1, cellsZ: 1 }, y: 0, ...extra,
});

const PREFABS = {
  // —— dlc04 中式园林 ——
  sq1: pf("p_dlc4_squarestone_single_01", "2ac903a35fe77095ffbf739dab0e3cba", "dlc04/art/dlc04/p_dlc4_squarestone_single_01.prefab", { y: 0.02 }),
  sq2: pf("p_dlc4_squarestone_single_02", "ae7e92d9ba026d60f002b1d3af0b19c8", "dlc04/art/dlc04/p_dlc4_squarestone_single_02.prefab", { y: 0.02 }),
  sq3: pf("p_dlc4_squarestone_single_03", "8ce9a6e810cacf546213fb8af79b4985", "dlc04/art/dlc04/p_dlc4_squarestone_single_03.prefab", { y: 0.02 }),
  sq4: pf("p_dlc4_squarestone_single_04", "02631de126315414260ba7268a7cadb1", "dlc04/art/dlc04/p_dlc4_squarestone_single_04.prefab", { y: 0.02 }),
  sq5: pf("p_dlc4_squarestone_single_05", "1bcfb9bc2dc600696d32ba9f64823aad", "dlc04/art/dlc04/p_dlc4_squarestone_single_05.prefab", { y: 0.02 }),
  lanternStand: pf("p_dlc4_lantern_stand", "60d43a138e587afc0945c8ea7b356eee", "dlc04/art/dlc04/p_dlc4_lantern_stand.prefab"),
  treeRed: pf("p_dlc4_tree_red", "14f3e8f196fab71d0d8fb3ecee0e47cd", "dlc04/art/dlc04/p_dlc4_tree_red.prefab"),
  treeGreen: pf("p_dlc4_tree_green", "2137bb9d7942afa4fc77228de853ba12", "dlc04/art/dlc04/p_dlc4_tree_green.prefab"),
  bushLargeRed: pf("p_dlc4_bush_large_red", "38a39d1b5bb908a4ac297312352ba602", "dlc04/art/dlc04/p_dlc4_bush_large_red.prefab"),
  bushSmallRed: pf("p_dlc4_bush_small_red", "c2a7b4436b148d7394d48a73a80ae982", "dlc04/art/dlc04/p_dlc4_bush_small_red.prefab"),
  rockA: pf("p_dlc4_rock_a", "254617885c5174c7073704aa7a3f7ef7", "dlc04/art/dlc04/p_dlc4_rock_a.prefab"),
  rockB: pf("p_dlc4_rock_b", "dc1d67fd9510fc665ea6369cfdc816bb", "dlc04/art/dlc04/p_dlc4_rock_b.prefab"),
  rockC: pf("p_dlc4_rock_c", "bd0c63cbcf16bafce3ad084f0386ef75", "dlc04/art/dlc04/p_dlc4_rock_c.prefab"),
  rockE: pf("p_dlc4_rock_e", "9bc9d7243f7d3459170fc6946a91dc8e", "dlc04/art/dlc04/p_dlc4_rock_e.prefab"),
  grassCardA: pf("p_dlc4_grass_card_a", "2e3480a2d65b332d0777fd7173461f16", "dlc04/art/dlc04/p_dlc4_grass_card_a.prefab", { y: 0.01 }),
  grassCardB: pf("p_dlc4_grass_card_b", "4734f8c7e0fbd8efeb997d6806d9d156", "dlc04/art/dlc04/p_dlc4_grass_card_b.prefab", { y: 0.01 }),
  grassCardC: pf("p_dlc4_grass_card_c", "d74b17090919c4841716311d4141398a", "dlc04/art/dlc04/p_dlc4_grass_card_c.prefab", { y: 0.01 }),
  pagoda: pf("p_dlc4_pagoda", "be5e022028ee8d2de27647c9e4b53fc0", "dlc04/art/dlc04/p_dlc4_pagoda.prefab"),
  water: pf("p_dlc4_water_01", "218a9d2ef9ed8d5c6038c05d773c0503", "dlc04/art/dlc04/p_dlc4_water_01.prefab", { y: 0.02 }),
  lilypods: pf("p_dlc4_lilypods_01", "c723f1d9b0ef2c27b3b1dceeaec68cd6", "dlc04/art/dlc04/p_dlc4_lilypods_01.prefab", { y: 0.06 }),
  bridgeMid: pf("p_dlc4_bridge_middle", "4a5ba75d3a8e313bddd815ff11e57b97", "dlc04/art/dlc04/p_dlc4_bridge_middle.prefab"),
  bridgeEnd: pf("p_dlc4_bridge_end", "28ca5e6845f0d479dab83d73e2cb447e", "dlc04/art/dlc04/p_dlc4_bridge_end.prefab"),
  plantPot: pf("p_dlc4_plant_pot", "4014112da786e9acf9e004ff6069e91b", "dlc04/art/dlc04/p_dlc4_plant_pot.prefab"),
  vase: pf("p_dlc4_vase", "f8faedc9817aaeeaad5bad69ceb38fd3", "dlc04/art/dlc04/p_dlc4_vase.prefab"),
  incensePot: pf("p_dlc4_incense_pot", "e460aca7a1a359ecb8c3dfa72278f2ab", "dlc04/art/dlc04/p_dlc4_incense_pot.prefab"),
  // —— dlc13 中秋灯会 ——
  bamboo1: pf("p_dlc13_bamboo_1", "68030afac8a38f03058e8cb32e0658fa", "dlc13/art/dlc13/p_dlc13_bamboo_1.prefab"),
  bamboo02: pf("p_dlc13_bamboo_02", "27274c347bd2a1c9892febacc777e30a", "dlc13/art/dlc13/p_dlc13_bamboo_02.prefab"),
  archDragon: pf("p_dlc13_archway_dragon_02", "8c519e273b55cf06ce62ff1291e040ff", "dlc13/art/dlc13/p_dlc13_archway_dragon_02.prefab"),
  flyingLanterns: pf("p_dlc13_flyinglanternsgroup_1", "8874b07991c4976ed88312d63df3aed2", "dlc13/art/dlc13/p_dlc13_flyinglanternsgroup_1.prefab", { y: 4.2 }),
  boxLantern1: pf("p_dlc13_box_lantern_01", "11f2cfbfcef7810ea00842a69983a98e", "dlc13/art/dlc13/p_dlc13_box_lantern_01.prefab"),
  boxLantern2: pf("p_dlc13_box_lantern_02", "d403956af5d5f406bf6c0ee3cbc8f22b", "dlc13/art/dlc13/p_dlc13_box_lantern_02.prefab"),
  lotusCandle: pf("p_lotuscandle_01", "2502e3aa7f097a507bd8795d25ecf6d0", "dlc13/art/dlc13/p_lotuscandle_01.prefab", { y: 0.06 }),
  vase13: pf("p_dlc13_vase_1", "9044ef80130a60855c398e5926c38f22", "dlc13/art/dlc13/p_dlc13_vase_1.prefab"),
  // 注：不用 p_dlc13_tree_red_1（dlc13 大树体量过大，用户明确要求禁用），红叶树统一用 dlc04 tree_red。
};

function stubDefaults() {
  return {
    dispenser: { spawnerItemPrefabGuid: "" },
    conveyor: { conveySpeed: 0.5 },
    teleportal: { exitPortalInstanceId: "", portalColor: 0, doubleSided: false },
    foodSpawner: { spawnInOrder: true, attachmentPrefabGuids: [], weights: [], triggerTime: 5, triggerAtStart: true },
    cookingUtensil: { capacity: 0, allowedIngredientGuids: [] },
    travelator: { speed: 2.5 },
    flamethrower: { cookingRate: 4 },
    cleanPlateStack: { plateCount: 0, platePrefabGuid: "" },
    burner: { fireMode: 0, airTime: 0, randomTargetOrder: false, hideVisual: false },
    player: { playerID: 11 },
    servingStation: { plateReturnInstanceId: "", plateReturnInstanceIds: [] },
    plateReturn: { returnClean: false },
    switchStub: { startEnabled: true, activeMaterialGuid: "", inactiveMaterialGuid: "" },
    pressureSwitch: { occupiedMaterialGuid: "", unoccupiedMaterialGuid: "" },
    terminal: { pilotableObjectInstanceId: "" },
    meshWithMaterial: { pseudoPrefabGuid: "", materialGuid: "" },
    soArray: { pseudoPrefabGuids: [] },
  };
}

let seq = 0;
const newId = () => `new:bk14:${String(seq++).padStart(4, "0")}`;

function makeItem(pfDef, x, y, z, rotY, parentPath) {
  const id = newId();
  return {
    instanceId: id, hierarchyPath: id,
    prefabGuid: pfDef.guid, prefabAssetPath: pfDef.assetPath,
    parentPath,
    displayName: pfDef.id,
    localPosition: { x, y, z }, worldPosition: { x, y, z },
    localRotationX: pfDef.rotX ?? 0, localRotationY: rotY, localRotationZ: 0,
    localScale: { ...(pfDef.scale ?? { x: 1, y: 1, z: 1 }) },
    colliderCenter: { x: 0, y: 0, z: 0 },
    footprint: { ...(pfDef.footprint ?? { cellsX: 1, cellsZ: 1 }) },
    walkable: false, airWall: false, stubKind: "", ...stubDefaults(),
  };
}

/** 逐字克隆源物品（仅换 instanceId/hierarchyPath，其余含世界坐标原样保留） */
function cloneItem(it) {
  const out = JSON.parse(JSON.stringify(it));
  const id = newId();
  out.instanceId = id;
  out.hierarchyPath = id;
  return out;
}

const args = process.argv.slice(2);
const argVal = (name, dflt) => { const idx = args.indexOf(name); return idx >= 0 ? args[idx + 1] : dflt; };
const BASE = argVal("--base-url", "http://127.0.0.1:8765");
const APPLY = args.includes("--apply");

async function api(p, method, body) {
  const r = await fetch(`${BASE}${p}`, {
    method: method || "GET",
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await r.text();
  if (!r.ok) throw new Error(`${method || "GET"} ${p} -> ${r.status}: ${text.slice(0, 400)}`);
  try { return JSON.parse(text); } catch { return text; }
}

async function health() {
  const r = await fetch(`${BASE}/api/health`).catch(() => null);
  if (!r || !r.ok) {
    throw new Error("Unity 桥未启动。请在 Unity：Tools → Layout Editor → Open Bridge → 启动服务，再重跑。");
  }
}

/** backup/level1_4 不存在则经 /api/level/create 建档 */
async function ensureLevel() {
  const err = await api("/api/level/create", "POST", {
    setName: SET, levelId: LEVEL_ID, levelName: LEVEL_NAME, levelNameZH: LEVEL_NAME_ZH,
  }).then((r) => (typeof r === "object" && r && r.error ? r.error : (typeof r === "string" ? r : null)))
    .catch((e) => e.message);
  if (err && !/已存在|存在/.test(err)) throw new Error(`建档失败：${err}`);
  console.log(err ? `关卡已存在，复用：${SET}/${LEVEL_ID}` : `已建档：${SET}/${LEVEL_ID}`);
}

const isFloor = (it) =>
  (it.parentPath || "") === "Art" || // 手改版地板直接挂 Art/
  /^Art\/MainIsland(\/|$)/.test(it.parentPath || "");
const isKitchen = (it) =>
  isFloor(it) ||
  /^Art\/Decoration(\/|$)/.test(it.parentPath || "") || // 房内竹景（沿用）
  /^Art\/Structure(\/|$)/.test(it.parentPath || "") ||
  it.stubKind === "Collision";
// Art/Backdrop 旧外围件（ticket_rack）不复制 —— 外围按枫醉秋亭重建
const isPlayer = (it) => /Player\.prefab$/.test(it.prefabAssetPath || "");

/** 由实测墙/地板 bbox 生成枫醉秋亭外围布景 */
function emitExterior(items, wi0, wi1, wj0, wj1) {
  const occ = new Set();
  const X = (sub, pfDef, i, j, yaw, y) => {
    occ.add(key(i, j));
    items.push(makeItem(pfDef, cx(i), y ?? pfDef.y ?? 0, cz(j), yaw, `Art/Backdrop/${sub}`));
  };
  const axis = Math.round((wi0 + wi1) / 2);

  // —— 南入口（j = wj0-1：石板路 + 灯笼对 + 红叶树镇角 + 香炉迎客） ——
  const sj = wj0 - 1;
  const stones = [PREFABS.sq1, PREFABS.sq2, PREFABS.sq3, PREFABS.sq4, PREFABS.sq5];
  for (let n = 0; n < 5; n++) X("South", stones[n], axis - 2 + n, sj, 0);
  X("South", PREFABS.lanternStand, axis - 3, sj, 90);
  X("South", PREFABS.lanternStand, axis + 3, sj, 270);
  X("South", PREFABS.treeRed, wi0 + 1, sj, 30);
  X("South", PREFABS.treeRed, wi1 - 1, sj, 330);
  X("South", PREFABS.bushSmallRed, wi0 + 3, sj, 0);
  X("South", PREFABS.bushSmallRed, wi1 - 3, sj, 0);
  X("South", PREFABS.incensePot, axis, wj0 - 2, 0);
  X("South", PREFABS.boxLantern1, axis - 2, wj0 - 2, 15);
  X("South", PREFABS.boxLantern2, axis + 2, wj0 - 2, 345);

  // —— 东西两翼（i = wi0-1 / wi1+1：竹林 / 红叶 / 假山 交替） ——
  const jMid = Math.round((wj0 + wj1) / 2);
  for (const side of ["West", "East"]) {
    const i = side === "West" ? wi0 - 1 : wi1 + 1;
    const dir = side === "West" ? 90 : 270;
    X(side, PREFABS.treeRed, i, wj0, dir);
    X(side, PREFABS.bamboo1, i, wj0 + 2, dir);
    X(side, side === "West" ? PREFABS.rockB : PREFABS.rockC, i, jMid, dir);
    X(side, PREFABS.bamboo02, i, wj1 - 2, dir);
    X(side, side === "West" ? PREFABS.treeGreen : PREFABS.treeRed, i, wj1, dir);
  }
  // 四角点缀
  X("West", PREFABS.rockA, wi0 - 1, wj0 - 1, 45);
  X("East", PREFABS.rockE, wi1 + 1, wj0 - 1, 315);
  X("West", PREFABS.bushLargeRed, wi0 - 1, wj1 + 1, 90);
  X("East", PREFABS.bushLargeRed, wi1 + 1, wj1 + 1, 270);

  // —— 北侧园林观景区 ——
  // 手改版北墙外 j∈[wj1+2..wj1+4] 有悬空地毯露台，水塘/桥/宝塔整体北移避让，
  // 露台保持干净留白（不留白地毯上叠加任何布景）。
  const nd = wj1 + 1; // 观览带（灯 + 花盆）
  const t0 = wj1 + 3; // 露台中行（两侧夹景树）
  const b0 = wj1 + 5; // 桥南端
  const n1 = wj1 + 6; // 水塘行 1（桥身）
  const n2 = wj1 + 7; // 水塘行 2（桥身）
  const b1 = wj1 + 8; // 桥北端 / 后景树
  const n5 = wj1 + 10; // 宝塔(亭)
  const n7 = wj1 + 12; // 龙拱门（远景）

  // 露台两侧夹景红叶
  X("North/Terrace", PREFABS.treeRed, wi0 + 2, t0, 60);
  X("North/Terrace", PREFABS.treeRed, wi1 - 2, t0, 300);

  // 小桥横跨水塘（南端→桥身×2→北端）
  X("North/Bridge", PREFABS.bridgeEnd, axis, b0, 0);
  X("North/Bridge", PREFABS.bridgeMid, axis, n1, 0);
  X("North/Bridge", PREFABS.bridgeMid, axis, n2, 0);
  X("North/Bridge", PREFABS.bridgeEnd, axis, b1, 180);
  // 水塘（避开桥列）
  for (let j = n1; j <= n2; j++) {
    for (let i = axis - 3; i <= axis + 3; i++) {
      if (i !== axis) X("North/Pond", PREFABS.water, i, j, 0);
    }
  }
  X("North/Pond", PREFABS.lilypods, axis - 3, n1, 0);
  X("North/Pond", PREFABS.lilypods, axis + 3, n2, 0);
  X("North/Pond", PREFABS.lotusCandle, axis - 2, n2, 0);
  X("North/Pond", PREFABS.lotusCandle, axis + 2, n1, 0);
  items.push(makeItem(PREFABS.flyingLanterns, cx(axis), PREFABS.flyingLanterns.y, cz(n2), 0, "Art/Backdrop/North/Pond"));
  // 观览带
  X("North/Deck", PREFABS.boxLantern1, axis - 3, nd, 0);
  X("North/Deck", PREFABS.boxLantern2, axis + 3, nd, 0);
  X("North/Deck", PREFABS.plantPot, wi0 + 2, nd, 0);
  X("North/Deck", PREFABS.plantPot, wi1 - 2, nd, 180);
  // 宝塔(亭) 居中轴 + 两侧灯/红叶树
  X("North/Pagoda", PREFABS.pagoda, axis, n5, 0);
  X("North/Pagoda", PREFABS.boxLantern2, axis - 2, n5, 0);
  X("North/Pagoda", PREFABS.boxLantern1, axis + 2, n5, 0);
  X("North/Pagoda", PREFABS.treeRed, axis - 4, n5, 60);
  X("North/Pagoda", PREFABS.treeRed, axis + 4, n5, 300);
  X("North/Pagoda", PREFABS.vase, wi0 + 3, n5, 0);
  X("North/Pagoda", PREFABS.vase13, wi1 - 3, n5, 0);
  X("North/Pagoda", PREFABS.treeGreen, wi0 + 2, b1, 30);
  X("North/Pagoda", PREFABS.treeGreen, wi1 - 2, b1, 330);
  // 龙拱门（远景收尾）
  X("North/Arch", PREFABS.archDragon, axis, n7, 0);

  // —— 外圈草皮补空（隔一格，避让已占位） ——
  const cards = [PREFABS.grassCardA, PREFABS.grassCardB, PREFABS.grassCardC];
  const ring = [];
  for (let i = wi0 - 1; i <= wi1 + 1; i++) ring.push([i, sj], [i, nd]);
  for (let j = wj0; j <= wj1; j++) ring.push([wi0 - 1, j], [wi1 + 1, j]);
  let n = 0;
  for (const [i, j] of ring) {
    if (occ.has(key(i, j))) continue;
    if ((i + j) % 2 !== 0) continue;
    X("Ring", cards[n++ % 3], i, j, 0);
  }
}

/** 读回校验：源厨房集 vs 新场景读回集（坐标/朝向多重集 1:1） */
function keyOf(it) {
  const p = it.worldPosition || it.localPosition || { x: 0, y: 0, z: 0 };
  const r = (v) => (+v).toFixed(2);
  return `${it.prefabGuid}|${r(p.x)},${r(p.y)},${r(p.z)}|${(+it.localRotationY).toFixed(1)}`;
}
function diffMultiset(expected, actual) {
  const bag = new Map();
  for (const k of actual) bag.set(k, (bag.get(k) || 0) + 1);
  const missing = [];
  for (const k of expected) {
    const c = bag.get(k) || 0;
    if (c > 0) bag.set(k, c - 1); else missing.push(k);
  }
  const extra = [...bag.entries()].flatMap(([k, c]) => Array(c).fill(k));
  return { missing, extra };
}

async function main() {
  await health();

  // 1. 读源（手改版 s_jia_level1_4）
  const src = await api(`/api/scene/layout?assetPath=${encodeURIComponent(SRC_SCENE)}`);
  const srcItems = src.items || [];
  const kitchenSrc = srcItems.filter(isKitchen);
  const playersSrc = srcItems.filter(isPlayer);
  if (kitchenSrc.length === 0) throw new Error("源场景未读到厨房物品（Art|Art/Decoration|Art/Structure|Collision）。");
  if (playersSrc.length !== 4) throw new Error(`源场景出生点数量异常：${playersSrc.length}（期望 4）`);

  // 2. 逐字克隆（地板置 walkable=true：导出器恒报 false，真正可走信息在
  //    doc.walkable，写回时由 syncWalkable 重建 Col_Floor 碰撞）
  const kitchen = kitchenSrc.map((it) => {
    const c = cloneItem(it);
    if (isFloor(it)) c.walkable = true;
    return c;
  });
  const players = playersSrc.map(cloneItem);

  // 3. 实测 bbox（以手改场景为准）
  const floors = kitchen.filter(isFloor);
  const walls = kitchen.filter((it) => /^Art\/Structure(\/|$)/.test(it.parentPath || ""));
  if (floors.length === 0) throw new Error("源场景 Art/ 下无地板。");
  const is = (arr, f) => arr.map((it) => cellOf(f(it)));
  const fx = is(floors, (it) => it.worldPosition.x);
  const fz = is(floors, (it) => it.worldPosition.z);
  let wi0, wi1, wj0, wj1;
  if (walls.length > 0) {
    const wx = is(walls, (it) => it.worldPosition.x);
    const wz = is(walls, (it) => it.worldPosition.z);
    wi0 = Math.min(...wx); wi1 = Math.max(...wx); wj0 = Math.min(...wz); wj1 = Math.max(...wz);
  } else {
    wi0 = Math.min(...fx) - 1; wi1 = Math.max(...fx) + 1; wj0 = Math.min(...fz) - 1; wj1 = Math.max(...fz) + 1;
  }
  console.log(`厨房 bbox：地板 i∈[${Math.min(...fx)},${Math.max(...fx)}] j∈[${Math.min(...fz)},${Math.max(...fz)}]，墙 i∈[${wi0},${wi1}] j∈[${wj0},${wj1}]`);

  // 4. 外围布景
  const gen = [];
  emitExterior(gen, wi0, wi1, wj0, wj1);

  // 5. KillPlane（实测地板 bbox + 1.5 格余量）
  const minX = Math.min(...floors.map((it) => it.worldPosition.x));
  const maxX = Math.max(...floors.map((it) => it.worldPosition.x));
  const minZ = Math.min(...floors.map((it) => it.worldPosition.z));
  const maxZ = Math.max(...floors.map((it) => it.worldPosition.z));
  const kill = {
    cx: +((minX + maxX) / 2).toFixed(4), cz: +((minZ + maxZ) / 2).toFixed(4),
    sx: +(maxX - minX + 3.6).toFixed(4), sz: +(maxZ - minZ + 3.6).toFixed(4),
  };

  const doc = {
    sceneAssetPath: DST_SCENE,
    items: players.concat(kitchen, gen),
    floors: [],
    cameraInfo: src.cameraInfo,
    lights: src.lights,
  };

  const byId = {};
  for (const it of gen) { const id = it.displayName; byId[id] = (byId[id] || 0) + 1; }
  const decor = kitchen.filter((it) => /^Art\/Decoration(\/|$)/.test(it.parentPath || ""));
  const coll = kitchen.length - floors.length - walls.length - decor.length;
  console.log(`\n物品：厨房复制 ${kitchen.length}（地板 ${floors.length} / 墙件 ${walls.length} / 房内装饰 ${decor.length} / 碰撞 ${coll}）+ 出生点 ${players.length} + 外围布景 ${gen.length}`);
  console.log(`  可走地板 ${floors.length} / ${floors.length} 格（全部置 walkable）`);
  for (const [id, n] of Object.entries(byId).sort()) console.log(`  ${id} × ${n}`);

  const out = path.join(__dirname, "../../.opencode/bk14-doc.json");
  fs.writeFileSync(out, JSON.stringify(doc));
  console.log("\n文档 →", out);
  if (!APPLY) { console.log("（未加 --apply，不写回 Unity）"); return; }

  // 6. 建档（幂等）→ 写回布局 → KillPlane → LevelInfo
  await ensureLevel();
  console.log("\n写回布局…");
  console.log(await api(`/api/scene/layout?snap=0.001&syncWalkable=1`, "POST", doc));
  console.log("设置 KillPlane…");
  console.log(await api("/api/scene/killplane", "POST", { sceneAssetPath: DST_SCENE, ...kill }));

  const srcInfo = await api(`/api/level?assetPath=${encodeURIComponent(SRC_INFO)}`);
  const dstInfo = await api(`/api/level?assetPath=${encodeURIComponent(DST_INFO)}`);
  const deps = new Set(dstInfo.dependencies || []);
  for (const b of srcInfo.dependencies || []) deps.add(b);
  console.log("更新 LevelInfo…");
  console.log(await api("/api/level/info", "POST", {
    assetPath: DST_INFO,
    levelName: LEVEL_NAME, levelNameZH: LEVEL_NAME_ZH, sceneName: "s_level1_4",
    debugRecipeCount: srcInfo.debugRecipeCount ?? 0,
    disableDynamicParenting: srcInfo.disableDynamicParenting ?? true,
    minOrderCount: srcInfo.minOrderCount ?? 2,
    maxOrderCount: srcInfo.maxOrderCount ?? 5,
    dependencies: [...deps],
  }));

  // 7. 读回校验
  console.log("\n读回校验…");
  const back = await api(`/api/scene/layout?assetPath=${encodeURIComponent(DST_SCENE)}`);
  const backItems = back.items || [];
  const backKitchen = backItems.filter(isKitchen);
  const backPlayers = backItems.filter(isPlayer);
  const { missing, extra } = diffMultiset(
    kitchenSrc.map(keyOf), backKitchen.map(keyOf),
  );
  const walkBack = (back.walkable || []).length;
  let pass = missing.length === 0 && extra.length === 0 && backPlayers.length === 4 && walkBack === floors.length;
  console.log(`  厨房物品：源 ${kitchenSrc.length} → 新场景 ${backKitchen.length}`);
  console.log(`  可走面：${walkBack} / 期望 ${floors.length}`);
  console.log(`  出生点：${backPlayers.length}/4`);
  if (missing.length || extra.length) {
    console.log(`  ✗ 厨房多重集不一致：缺 ${missing.length}，多 ${extra.length}`);
    for (const k of missing.slice(0, 5)) console.log(`    缺：${k}`);
    for (const k of extra.slice(0, 5)) console.log(`    多：${k}`);
    pass = false;
  }
  console.log(pass ? "\n✓ 校验通过。请在 Unity Play 验证视觉（外围素材 Y 偏移为估值，可在网页装饰层微调）。" : "\n✗ 校验失败，请检查上方差异。");
  if (!pass) process.exitCode = 1;
}

main().catch((e) => { console.error("失败：", e.message); process.exit(1); });
