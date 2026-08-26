#!/usr/bin/env node
/**
 * gen-jia-level1_3-layout.mjs — 荷风夏榭 v2
 * 左 6×10 留白 | 最小布景岛 | 右上下仅火锅轨道 | 砖块地板 | 荷叶桥
 */

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const CELL = 1.2;
const HALF = 0.6;
const FLOOR_Y = 0;
const SCENE = "Assets/LevelSets/jia_carnival/scenes/s_jia_level1_3.unity";
const LEVEL_INFO = "Assets/LevelSets/jia_carnival/data/jia_level1_3/LevelInfo_jia_level1_3.asset";

const cx = (i) => HALF + CELL * i;
const cz = (j) => HALF + CELL * j;
const key = (i, j) => `${i},${j}`;

const pf = (id, guid, assetPath, extra = {}) => ({
  id, guid, assetPath, rotX: 0, scale: { x: 1, y: 1, z: 1 },
  footprint: { cellsX: 1, cellsZ: 1 }, ...extra,
});

const PREFABS = {
  brick_tile: pf("p_dlc13_floortile_02", "2c870228be2a7a54432c444a8549c614",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_floortile_02.prefab",
    { scale: { x: 1.06, y: 1.06, z: 1.06 } }),
  edge_s1: pf("p_dlc13_flooredge_03", "0873895b21dac8ac558a32eb48074030",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_flooredge_03.prefab"),
  edge_s2: pf("p_dlc13_flooredge_04", "5dc5c7ba6ff77a0d035b795a6c0857ca",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_flooredge_04.prefab"),
  edge_270: pf("p_dlc13_flooredge_corner_02", "c6e4df78de81def0a9ef452f940e76e9",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_flooredge_corner_02.prefab"),
  lotus_sm: pf("dlc13_lotuspressureswitch_small", "e143bb9379494b03e347c0adfc7bc603",
    "Assets/common03/prefabs/dlc13/mechanisms/dlc13_lotuspressureswitch_small.prefab"),
  lotus_lg: pf("dlc13_lotuspressureswitch_large", "2deb54ae1eac651e1f3968d688fa13df",
    "Assets/common03/prefabs/dlc13/mechanisms/dlc13_lotuspressureswitch_large.prefab",
    { footprint: { cellsX: 2, cellsZ: 2 } }),
  floorburner: pf("cooking_region_floorburner", "a815348f43cd4b3905fb2ffaec777548",
    "Assets/common03/prefabs/core/counters/cooking_region_floorburner.prefab",
    { footprint: { cellsX: 2, cellsZ: 2 } }),
  large_pot: pf("utensil_large_pot_01", "0ad9bcbb047c81fc503749b2897b5e86",
    "Assets/common03/prefabs/core/utensils/utensil_large_pot_01.prefab"),
  water: pf("Water_01", "a3288048c24aaa44181e6970566891bd",
    "Assets/common01/prefabs/art/dlc03_christmas/Water_01.prefab", { rotX: 90 }),
  table_guests: pf("table_customers", "2bce6efbb09e7fc34c26ccc555235b92",
    "Assets/common03/prefabs/core/art/legacy/table_customers.prefab",
    { footprint: { cellsX: 1, cellsZ: 3 } }),
  sushi_sign_01: pf("sushi_sign_01", "55c4db2eaa6107b4984d54bada16ae2d",
    "Assets/common01/prefabs/art/city_sushi/sushi_sign_01.prefab"),
  bamboo: pf("decoration_bamboo_01", "a71e9b4552cd0a94a86bb425b0f94a35",
    "Assets/common01/prefabs/art/city_sushi/decoration_bamboo_01.prefab"),
  npc_blue: pf("NPC_Asian_Blue_01", "89c693e0d16578642bb8a9908eb3649f",
    "Assets/common01/prefabs/art/npc/NPC_Asian_Blue_01.prefab"),
  npc_waiter: pf("NPC_Asian_Waiter_01", "2bf53d6647a60464bb4f33df56650fc4",
    "Assets/common01/prefabs/art/npc/NPC_Asian_Waiter_01.prefab"),
  pagoda13: pf("p_dlc13_pagoda_1", "6a27c4739efc10de0b072efeec34ccf8",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_pagoda_1.prefab",
    { footprint: { cellsX: 6, cellsZ: 6 } }),
  pagoda4: pf("p_dlc4_pagoda", "be5e022028ee8d2de27647c9e4b53fc0",
    "Assets/common03/prefabs/dlc04/art/dlc04/p_dlc4_pagoda.prefab",
    { footprint: { cellsX: 6, cellsZ: 5 } }),
  lantern_stand: pf("p_dlc13_lantern_stand_1", "c331c6f0e4c28aab5da0c3478ad25814",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_lantern_stand_1.prefab"),
  lantern4: pf("p_dlc4_lantern", "4ea03e951dc0e33afe95f652b22bc2de",
    "Assets/common03/prefabs/dlc04/art/dlc04/p_dlc4_lantern.prefab"),
  box_lantern: pf("p_dlc13_box_lantern_01", "11f2cfbfcef7810ea00842a69983a98e",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_box_lantern_01.prefab"),
  lotus_candle: pf("p_lotuscandle_01", "2502e3aa7f097a507bd8795d25ecf6d0",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_lotuscandle_01.prefab"),
  ripple: pf("p_dlc13_ripple_02", "3730693e98869d8f86ea8b65b7e03151",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_ripple_02.prefab"),
};

function rectCells(i0, i1, j0, j1, remove = []) {
  const s = new Set();
  for (let i = i0; i <= i1; i++) for (let j = j0; j <= j1; j++) s.add(key(i, j));
  for (const [i, j] of remove) s.delete(key(i, j));
  return s;
}

// 四角阶梯切角（学自 level1_2 / oc1_story_3-4）
function cornerBevels(i0, i1, j0, j1) {
  return [
    [i0, j0], [i0 + 1, j0], [i0, j0 + 1],
    [i1, j0], [i1 - 1, j0], [i1, j0 + 1],
    [i0, j1], [i0 + 1, j1], [i0, j1 - 1],
    [i1, j1], [i1 - 1, j1], [i1, j1 - 1],
  ];
}

// 左 6×10 厨房留白
const leftBlank = rectCells(-7, -2, -4, 5);
// 右上下火锅轨道（右中间 j∈[-5,5] 无地板，全水）
const trackTop = rectCells(4, 14, 6, 8);
const trackBottom = rectCells(4, 14, -8, -6);

// 最小荷叶桥：blank 东缘 → 右轨西缘
const lotusCells = new Set();
for (const j of [-4, -2, 0, 2, 4]) {
  for (let i = -1; i <= 2; i++) lotusCells.add(key(i, j));
}
for (const j of [5, 6]) { lotusCells.add(key(4, j)); lotusCells.add(key(5, j)); }
for (const j of [7, 8]) lotusCells.add(key(5, j));
for (const j of [-5, -6]) { lotusCells.add(key(4, j)); lotusCells.add(key(5, j)); }
for (const j of [-7, -8]) lotusCells.add(key(5, j));

// 3 个切角布景岛（不可行走，仅承台）
const nwSushiIslet = rectCells(-10, -8, 7, 9, [[-10, 7], [-8, 7], [-10, 9], [-8, 9]]);
const midAutumnIslet = rectCells(11, 15, 9, 11, [[11, 9], [15, 9], [11, 11], [15, 11]]);
const springIslet = rectCells(11, 15, -11, -9, [[11, -11], [15, -11], [11, -9], [15, -9]]);

const solidWalkable = new Set([...leftBlank, ...trackTop, ...trackBottom]);
const backdropFloor = new Set([...nwSushiIslet, ...midAutumnIslet, ...springIslet]);

let seq = 0;
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

function makeItem(pfDef, x, y, z, rotY, parentPath, walkable, displaySuffix) {
  const id = `new:jia13:${String(seq++).padStart(4, "0")}`;
  return {
    instanceId: id, hierarchyPath: id,
    prefabGuid: pfDef.guid, prefabAssetPath: pfDef.assetPath,
    parentPath,
    displayName: displaySuffix ? `${pfDef.id} ${displaySuffix}` : pfDef.id,
    localPosition: { x, y, z }, worldPosition: { x, y, z },
    localRotationX: pfDef.rotX ?? 0, localRotationY: rotY, localRotationZ: 0,
    localScale: { ...(pfDef.scale ?? { x: 1, y: 1, z: 1 }) },
    colliderCenter: { x: 0, y: 0, z: 0 },
    footprint: { ...(pfDef.footprint ?? { cellsX: 1, cellsZ: 1 }) },
    walkable: !!walkable, airWall: false, stubKind: "", ...stubDefaults(),
  };
}

function pickVariant(a, b, i, j, salt) {
  const h = Math.abs((i * 73856093) ^ (j * 19349663) ^ (salt * 83492791));
  return h % 2 === 0 ? a : b;
}

const EDGE_RULES = [
  { di: 0, dj: 1, dx: 0, dz: HALF, yaw: 90 },
  { di: 1, dj: 0, dx: HALF, dz: 0, yaw: 180 },
  { di: 0, dj: -1, dx: 0, dz: -HALF, yaw: 270 },
  { di: -1, dj: 0, dx: -HALF, dz: 0, yaw: 0 },
];
const CONVEX_YAW = { NE: 270, NW: 180, SW: 90, SE: 0 };

function emitIsland(items, cells, tilePf, salt, opts = {}) {
  const parent = opts.parent || "Art/MainIsland";
  const walkable = opts.walkable !== false;
  const skipEdges = opts.skipEdges === true;
  const wx = (i) => HALF + CELL * i;
  const wz = (j) => HALF + CELL * j;
  const has = (i, j) => cells.has(key(i, j));
  for (const k of cells) {
    const [i, j] = k.split(",").map(Number);
    items.push(makeItem(tilePf, wx(i), FLOOR_Y, wz(j), 0, parent, walkable));
  }
  if (skipEdges) return;
  for (const k of cells) {
    const [i, j] = k.split(",").map(Number);
    for (const r of EDGE_RULES.filter((e) => !has(i + e.di, j + e.dj))) {
      const ef = pickVariant(PREFABS.edge_s1, PREFABS.edge_s2, i, j, salt + r.yaw);
      items.push(makeItem(ef, wx(i) + r.dx, FLOOR_Y, wz(j) + r.dz, r.yaw, parent, false));
    }
  }
  let minI = 1e9, maxI = -1e9, minJ = 1e9, maxJ = -1e9;
  for (const k of cells) {
    const [i, j] = k.split(",").map(Number);
    minI = Math.min(minI, i); maxI = Math.max(maxI, i);
    minJ = Math.min(minJ, j); maxJ = Math.max(maxJ, j);
  }
  for (let a = minI; a <= maxI + 1; a++) {
    for (let b = minJ; b <= maxJ + 1; b++) {
      const occ = [
        ["SW", has(a - 1, b - 1)], ["SE", has(a, b - 1)],
        ["NW", has(a - 1, b)], ["NE", has(a, b)],
      ].filter(([, v]) => v).map(([n]) => n);
      if (occ.length === 1) {
        items.push(makeItem(PREFABS.edge_270, HALF + CELL * a - HALF, FLOOR_Y, HALF + CELL * b - HALF,
          CONVEX_YAW[occ[0]], parent, false));
      }
    }
  }
}

function emitLotusBridges(items, cells) {
  const placed = new Set();
  for (const k of [...cells].sort()) {
    if (placed.has(k)) continue;
    const [i, j] = k.split(",").map(Number);
    const quad = [[i, j], [i + 1, j], [i, j + 1], [i + 1, j + 1]];
    if (quad.every(([ii, jj]) => cells.has(key(ii, jj))) && quad.every(([ii, jj]) => !placed.has(key(ii, jj)))) {
      const it = makeItem(PREFABS.lotus_lg, (cx(i) + cx(i + 1)) / 2, FLOOR_Y, (cz(j) + cz(j + 1)) / 2, 0, "Art/MainIsland", true);
      it.stubKind = "PressureSwitch"; items.push(it);
      for (const [ii, jj] of quad) placed.add(key(ii, jj));
      continue;
    }
    const it = makeItem(PREFABS.lotus_sm, cx(i), FLOOR_Y, cz(j), 0, "Art/MainIsland", true);
    it.stubKind = "PressureSwitch"; items.push(it);
    placed.add(k);
  }
}

function makeAirWall(items, x, y, z, len, alongX, name) {
  const it = makeItem({ id: name, guid: "", assetPath: "", rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 1 } },
    x, y, z, 0, "Design/Collision", false);
  it.stubKind = "Collision"; it.airWall = true; it.displayName = name;
  it.localScale = alongX ? { x: len, y: 1, z: 1 } : { x: 1, y: 1, z: len };
  items.push(it);
}

function emitHotpotTrack(items, { iStart, iEnd, jLo, jHi, tag }) {
  const placeBurnerEnd = (i0, j0, yaw) => {
    items.push(makeItem(PREFABS.floorburner, cx(i0), FLOOR_Y, cz(j0), yaw, "Design/Counters", true));
    const pot = makeItem(PREFABS.large_pot, (cx(i0) + cx(i0 + 1)) / 2, FLOOR_Y, (cz(j0) + cz(j0 + 1)) / 2, yaw, "Design/Utensils", false);
    pot.stubKind = "CookingUtensil"; pot.cookingUtensil = { capacity: 4, allowedIngredientGuids: [] };
    items.push(pot);
  };
  placeBurnerEnd(iStart, jLo, 90);
  placeBurnerEnd(iEnd - 1, jLo, 270);
  const lenI = (iEnd - iStart + 1) * CELL + 1.2;
  const midX = (cx(iStart) + cx(iEnd)) / 2;
  makeAirWall(items, midX, FLOOR_Y, cz(jHi) + HALF, lenI, true, `AirWall_${tag}_N`);
  makeAirWall(items, midX, FLOOR_Y, cz(jLo) - HALF, lenI, true, `AirWall_${tag}_S`);
  const midZ = (cz(jLo) + cz(jHi)) / 2;
  const lenJ = (jHi - jLo + 1) * CELL + 0.6;
  makeAirWall(items, cx(iStart) - HALF, FLOOR_Y, midZ, lenJ, false, `AirWall_${tag}_W`);
  makeAirWall(items, cx(iEnd) + HALF, FLOOR_Y, midZ, lenJ, false, `AirWall_${tag}_E`);
}

// 布景岛装饰坐标一律取岛格集内的格心（学自 level1_2 emitBackdrop）
function emitBackdrops(items) {
  const B = (pfDef, i, j, yaw, parent = "Art/Backdrop", y = FLOOR_Y) =>
    items.push(makeItem(pfDef, cx(i), y, cz(j), yaw, parent, false));

  // 西北寿司岛 i∈[-10,-8] j∈[7,9] 切 4 角点
  B(PREFABS.sushi_sign_01, -10, 8, 90, "Art/Backdrop/Sushi");
  B(PREFABS.bamboo, -9, 9, 0, "Art/Backdrop/Sushi");
  B(PREFABS.bamboo, -10, 9, 180, "Art/Backdrop/Sushi");
  B(PREFABS.table_guests, -9, 8, 0, "Art/Backdrop/Sushi");
  B(PREFABS.table_guests, -10, 8, 180, "Art/Backdrop/Sushi");
  B(PREFABS.npc_blue, -10, 7, 110, "Art/Backdrop/Sushi");
  B(PREFABS.lantern_stand, -10, 9, 45, "Art/Backdrop/Sushi");
  B(PREFABS.lantern_stand, -8, 7, 315, "Art/Backdrop/Sushi");
  B(PREFABS.box_lantern, -9, 7, 135, "Art/Backdrop/Sushi");
  B(PREFABS.box_lantern, -8, 9, 225, "Art/Backdrop/Sushi");

  // 东北中秋岛 i∈[11,15] j∈[9,11]
  items.push(makeItem(PREFABS.pagoda13, 16.2, FLOOR_Y, 12.6, 0, "Art/Backdrop/MidAutumn", false));
  B(PREFABS.table_guests, 12, 9, 0, "Art/Backdrop/MidAutumn");
  B(PREFABS.table_guests, 14, 9, 180, "Art/Backdrop/MidAutumn");
  B(PREFABS.lantern_stand, 11, 10, 45, "Art/Backdrop/MidAutumn");
  B(PREFABS.lantern_stand, 14, 10, 315, "Art/Backdrop/MidAutumn");
  B(PREFABS.box_lantern, 12, 11, 180, "Art/Backdrop/MidAutumn");
  B(PREFABS.box_lantern, 14, 11, 0, "Art/Backdrop/MidAutumn");
  B(PREFABS.npc_waiter, 13, 10, 270, "Art/Backdrop/MidAutumn");

  // 东南春节岛 i∈[11,15] j∈[-11,-9]
  items.push(makeItem(PREFABS.pagoda4, 16.2, FLOOR_Y, -12.6, 180, "Art/Backdrop/SpringFest", false));
  B(PREFABS.lantern4, 12, -10, 0, "Art/Backdrop/SpringFest");
  B(PREFABS.lantern4, 14, -10, 180, "Art/Backdrop/SpringFest");
  B(PREFABS.table_guests, 13, -9, 180, "Art/Backdrop/SpringFest");
  B(PREFABS.box_lantern, 11, -10, 90, "Art/Backdrop/SpringFest");
  B(PREFABS.box_lantern, 15, -10, 270, "Art/Backdrop/SpringFest");

  // 左 blank 四角盒灯（朝内），留白内不放 NPC/桌椅
  const L = (i, j, yaw) => B(PREFABS.box_lantern, i, j, yaw, "Art/MainIsland");
  L(-7, -4, 45);
  L(-2, -4, 135);
  L(-7, 5, 315);
  L(-2, 5, 225);
}

function emitWaterDecor(items) {
  const W = (pfDef, x, z, yaw, y = -0.81) => items.push(makeItem(pfDef, x, y, z, yaw, "Art/Decoration", false));
  W(PREFABS.lotus_candle, 7.2, 10.8, 0);
  W(PREFABS.lotus_candle, 16.8, -10.8, 180);
  W(PREFABS.ripple, 2.4, 0, 0);
  W(PREFABS.ripple, 10.8, 0, 90);
}

const args = process.argv.slice(2);
const argVal = (name, dflt) => { const idx = args.indexOf(name); return idx >= 0 ? args[idx + 1] : dflt; };
const BASE = argVal("--base-url", "http://127.0.0.1:8765");
const APPLY = args.includes("--apply");
const OFFLINE = args.includes("--offline");

async function api(path, method, body) {
  const r = await fetch(`${BASE}${path}`, {
    method: method || "GET",
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await r.text();
  if (!r.ok) throw new Error(`${method || "GET"} ${path} -> ${r.status}: ${text.slice(0, 400)}`);
  try { return JSON.parse(text); } catch { return text; }
}

const SPAWN_POS = {
  "Player 1": { x: cx(-6), z: cz(-2) },
  "Player 2": { x: cx(-5), z: cz(0) },
  "Player 3": { x: cx(-4), z: cz(-2) },
  "Player 4": { x: cx(-3), z: cz(0) },
};

function makeSpawnPlayers() {
  return ["Player 1", "Player 2", "Player 3", "Player 4"].map((name) => {
    const np = SPAWN_POS[name];
    const it = makeItem({ id: "Player", guid: "78d1be00b5b01df4ca974d31ced391b8", assetPath: "Assets/common01/prefabs/Player.prefab" },
      np.x, 0, np.z, 0, "Design/Chefs", false);
    it.displayName = name; it.stubKind = "Player";
    it.player = { playerID: name === "Player 1" ? 0 : name === "Player 2" ? 1 : name === "Player 3" ? 2 : 3 };
    return it;
  });
}

async function main() {
  let players;
  if (OFFLINE) {
    players = makeSpawnPlayers();
  } else {
    const cur = await api(`/api/scene/layout?assetPath=${encodeURIComponent(SCENE)}`);
    players = (cur.items || []).filter((it) => /Player\.prefab$/.test(it.prefabAssetPath || ""));
    if (players.length !== 4) throw new Error(`出生点数量异常：${players.length}（期望 4）`);
    for (const p of players) {
      const np = SPAWN_POS[p.displayName];
      if (!np) throw new Error(`未知出生点：${p.displayName}`);
      p.localPosition = { x: np.x, y: 0, z: np.z };
      p.worldPosition = { x: np.x, y: 0, z: np.z };
      p.walkable = false;
    }
  }

  const gen = [];
  emitIsland(gen, solidWalkable, PREFABS.brick_tile, 23, { parent: "Art/MainIsland", walkable: true });
  emitIsland(gen, backdropFloor, PREFABS.brick_tile, 41, { parent: "Art/Backdrop", walkable: false, skipEdges: true });
  emitLotusBridges(gen, lotusCells);
  emitHotpotTrack(gen, { iStart: 4, iEnd: 14, jLo: 6, jHi: 8, tag: "Top" });
  emitHotpotTrack(gen, { iStart: 4, iEnd: 14, jLo: -8, jHi: -6, tag: "Bot" });
  emitBackdrops(gen);
  emitWaterDecor(gen);
  const water = makeItem(PREFABS.water, 2.4, -0.81, 0, 0, "Art/Environment", false);
  water.localScale = { x: 36, y: 28, z: 1 };
  gen.push(water);

  const doc = {
    sceneAssetPath: SCENE,
    items: players.concat(gen),
    floors: [],
    cameraInfo: {
      backgroundColor: "#000000", fieldOfView: 48,
      position: { x: 5.4, y: 22, z: -15.2 }, pitch: 60, yaw: 0, roll: 0,
      nearClip: 0.3, farClip: 1000,
    },
    lights: [{
      hierarchyPath: "Art/Lights/day", displayName: "day", lightType: 1,
      color: "#FFE8A0", intensity: 0.45, range: 10, spotAngle: 30, enabled: true,
      eulerAngles: { x: 39.16, y: 350.1, z: 0 },
    }],
  };

  const byId = {};
  for (const it of gen) { const id = it.displayName.split(" ")[0]; byId[id] = (byId[id] || 0) + 1; }
  console.log(`生成物品：${gen.length} 个（另含 4 出生点）`);
  for (const [id, n] of Object.entries(byId).sort()) console.log(`  ${id} × ${n}`);

  const out = path.join(__dirname, "../../.opencode/jia13-doc.json");
  fs.writeFileSync(out, JSON.stringify(doc));
  console.log("\n文档 →", out);
  if (!APPLY) { console.log("（未加 --apply，不写回 Unity）"); return; }

  console.log("\n写回布局…");
  console.log(await api(`/api/scene/layout?snap=0.01&syncWalkable=1`, "POST", doc));
  console.log("设置死亡主题为 water…");
  console.log(await api("/api/scene/death", "POST", { sceneAssetPath: SCENE, theme: "water" }));
  console.log("设置 KillPlane…");
  console.log(await api("/api/scene/killplane", "POST", { sceneAssetPath: SCENE, cx: 2.4, cz: 0, sx: 36, sz: 28 }));
  const info = await api(`/api/level?assetPath=${encodeURIComponent(LEVEL_INFO)}`);
  const deps = new Set(info.dependencies || []);
  for (const b of ["bundle47", "bundle449", "bundle226", "bundle225"]) deps.add(b);
  console.log(await api("/api/level/info", "POST", {
    assetPath: LEVEL_INFO, levelName: info.levelName, levelNameZH: info.levelNameZH,
    sceneName: info.sceneName, debugRecipeCount: info.debugRecipeCount ?? 0,
    disableDynamicParenting: info.disableDynamicParenting ?? true,
    minOrderCount: info.minOrderCount ?? 2, maxOrderCount: info.maxOrderCount ?? 5,
    dependencies: [...deps],
  }));
  console.log("\n全部完成。请在 Unity Play 验证。");
}

main().catch((e) => { console.error("失败：", e.message); process.exit(1); });
