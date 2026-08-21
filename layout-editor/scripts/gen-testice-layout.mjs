#!/usr/bin/env node
/**
 * gen-testice-layout.mjs — 生成 my_test/testice 冰雪三岛布景并写回 Unity（v2）。
 *
 * 布局（格 = 1.2m，行走面统一高度，左中右结构，背景为水）：
 *   左岛 8×12（四角各阶梯切 3 格的斜角）、中岛 4×4、右岛 8×8（同款切角）
 *   中岛与左右岛之间各留 1.8m 水道（同 3-4 水道宽度），视觉上隔水；
 *   中岛下方垫一块隐形 airFloor 行走碰撞（四周比岛面外扩 0.9m，学 3-4 浮冰
 *   Ground 碰撞 5.4×4.2 比可视面大一圈的做法），两侧各留 0.9m 碰撞缝隙，
 *   厨师可以直接走过（和 3-4 浮冰间隙同款手感）。
 *   周围 3 块漂浮小冰岛（moveControls 小环线漂动动画）+ 北侧中秋宝塔布景岛
 *   + 水面漂浮灯笼点缀。
 *
 * 素材：地砖/冰崖/雪盖/水面/降雪复用 oc1_story_3_4（DLC03 / bundle210），
 * 宝塔与漂浮灯笼来自 DLC13 中秋主题（common03）。
 *
 * 用法：
 *   node gen-testice-layout.mjs --base-url http://<虚拟机IP>:8765            # 只生成 JSON，不写回
 *   node gen-testice-layout.mjs --base-url http://<虚拟机IP>:8765 --apply    # 生成并写回 Unity
 */

const CELL = 1.2;
const HALF = 0.6;
const SCENE = "Assets/LevelSets/my_test/scenes/s_testice.unity";

// ---------------------------------------------------------------- prefab 表
const P = (id, guid, extra = {}) => ({
  id,
  guid,
  assetPath: `Assets/common01/prefabs/art/dlc03_christmas/${id}.prefab`,
  rotX: 0,
  scale: { x: 1, y: 1, z: 1 },
  footprint: { cellsX: 1, cellsZ: 1 },
  ...extra,
});
const PREFABS = {
  snow_tile: P("snow_floor_01", "47ee35a1dc6245e4b8a6da98121b63fc", { rotX: 90 }),
  ice_tile: P("ice_floor_01", "46d2e3289d9e97543b43ede1a072ccfc", { rotX: 90 }),
  cliff_s1: P("m_dlc3_icecliff_straight_01", "6abdbf27f38cbb94b8265c4901667159"),
  cliff_s2: P("m_dlc3_icecliff_straight_02", "96188dcc3bafdf64289d423aad495613"),
  cliff_90: P("m_dlc3_icecliff_90", "a219a44b013140f4d9468fd3195ee012"),
  cliff_270: P("m_dlc3_icecliff_270", "c3d40717412382845bee670975a480fc"),
  // 雪盖：wrapper 用 0.01 缩放（prefab 本体烘了 100 倍），盖在崖顶边缘
  cap_s1: P("snow_straight_01", "a21b1b02d0c27fc4b997c86bd7dc3e95", { scale: { x: 0.01, y: 0.01, z: 0.01 } }),
  cap_s2: P("snow_straight_02", "070c192a1c96f6245b0424e83ae3a1e3", { scale: { x: 0.01, y: 0.01, z: 0.01 } }),
  water: P("Water_01", "a3288048c24aaa44181e6970566891bd", { rotX: 90 }),
  snowfall: P("Snowfall_01", "c8ef56094c25a2746b37e348ab7a46ef"),
  snowmound_01: P("snowmound_01", "251b8d7198c395b4f80906bbe9173649"),
  snowmound_02: P("snowmound_02", "b44e0ee71eed03344ac8e1466bd4839f"),
  snowmound_03: P("snowmound_03", "8cfd1bb644e580641b6aed3c36a375f9", { footprint: { cellsX: 2, cellsZ: 1 } }),
  snowmound_04: P("snowmound_04", "8452a972465115c47af67270ec476b76"),
  snowmound_05: P("snowmound_05", "5b5df672891cfb242a6398be5d9dc04b", { footprint: { cellsX: 2, cellsZ: 2 } }),
  snowmound_06: P("snowmound_06", "af2a980af02aa0b4a9026d81a89f39ed"),
  snowmound_08: P("snowmound_08", "4afcaf13be108f3498d332e3545418e3"),
  iceblock_01: P("m_dlc3_iceblock_01", "e7edce59c2dcfce4e8961659dc5644a6"),
  iceblock_02: P("m_dlc3_iceblock_02", "aaed9f7a452440a45800d3000d5761aa"),
  iceblock_03: P("m_dlc3_iceblock_03", "89408557682fbe640b7542cc208fb09b"),
  snowballpile: P("snowballpile_01", "7420c01cb0ea9c94ea2a7b75033325eb"),
  sign_02: P("sign_02", "70bd51e4781df3e4fa56d74605d4f03f"),
};
// 积雪常青树（common03/dlc03）
PREFABS.evergreen = {
  id: "m_dlc3_evergreen_snow_01",
  guid: "ab570a00a428bba2774d2639872c30ad",
  assetPath: "Assets/common03/prefabs/dlc03/art/dlc03_christmas/m_dlc3_evergreen_snow_01.prefab",
  rotX: 0,
  scale: { x: 1, y: 1, z: 1 },
  footprint: { cellsX: 3, cellsZ: 3 },
};
// 中秋装饰（common03/dlc13）
PREFABS.pagoda = {
  id: "p_dlc13_pagoda_1",
  guid: "6a27c4739efc10de0b072efeec34ccf8",
  assetPath: "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_pagoda_1.prefab",
  rotX: 0,
  scale: { x: 1, y: 1, z: 1 },
  footprint: { cellsX: 6, cellsZ: 6 },
};
PREFABS.lantern = {
  id: "p_dlc13_lantern_floating_water_02",
  guid: "4159ff454b43fed9331ef9ca63f838d1",
  assetPath: "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_lantern_floating_water_02.prefab",
  rotX: 0,
  scale: { x: 1, y: 1, z: 1 },
  footprint: { cellsX: 1, cellsZ: 1 },
};

// ---------------------------------------------------------------- 岛位图
// 格 (i,j) → 地砖中心世界坐标 (ox+1.2i, y, oz+1.2j)；主岛用半格基准 0.6
const cx = (i) => HALF + CELL * i;
const cz = (j) => HALF + CELL * j;
const key = (i, j) => `${i},${j}`;

function rectCells(i0, i1, j0, j1, remove = []) {
  const s = new Set();
  for (let i = i0; i <= i1; i++) for (let j = j0; j <= j1; j++) s.add(key(i, j));
  for (const [i, j] of remove) s.delete(key(i, j));
  return s;
}
// 四角阶梯切角（每角缺 3 格，呈 45° 斜角，视觉平滑）
function cornerBevels(i0, i1, j0, j1) {
  return [
    [i0, j0], [i0 + 1, j0], [i0, j0 + 1],     // SW
    [i1, j0], [i1 - 1, j0], [i1, j0 + 1],     // SE
    [i0, j1], [i0 + 1, j1], [i0, j1 - 1],     // NW
    [i1, j1], [i1 - 1, j1], [i1, j1 - 1],     // NE
  ];
}

// 左岛 8×12（横 8 竖 12），东缘 x=-1.2
const leftIsland = rectCells(-9, -2, -6, 5, cornerBevels(-9, -2, -6, 5));
// 中岛 4×4（整数格基准：中心 1.2..4.8 × -1.8..1.8，岛缘 0.6..5.4 / ±2.4）
const midIsland = rectCells(1, 4, -1, 2, []);
// 右岛 8×8，西缘 x=7.2 → 两侧水道各 1.8m（同 3-4）
const rightIsland = rectCells(6, 13, -4, 3, cornerBevels(6, 13, -4, 3));
// 北侧中秋布景岛 5×5（不可行走，纯背景），切四角
const pagodaIsland = rectCells(0, 4, 6, 10, cornerBevels(0, 4, 6, 10).filter(
  ([i, j]) => (i === 0 || i === 4) && (j === 6 || j === 10))); // 5×5 只切角点 1 格

// 冰面点缀：全部放在岛的边缘格（落在缺格里的自动跳过）
const iceAccents = new Map([
  // 左岛：西缘/东缘/南缘/北缘
  [key(-9, -2), 1], [key(-9, 2), 1], [key(-2, -4), 1], [key(-2, 0), 1], [key(-2, 3), 1],
  [key(-6, -6), 1], [key(-4, -6), 1], [key(-7, 5), 1], [key(-4, 5), 1],
  // 中岛
  [key(1, 0), 1], [key(4, -1), 1], [key(2, 2), 1],
  // 右岛
  [key(6, -1), 1], [key(6, 1), 1], [key(13, -1), 1], [key(12, 2), 1],
  [key(9, -4), 1], [key(11, -4), 1], [key(9, 3), 1], [key(11, 3), 1],
]);

// ---------------------------------------------------------------- 围边语法（学自 3-4 实测）
// 直边朝向：W→0, N→90, E→180, S→270
const EDGE_RULES = [
  { di: 0, dj: 1, dx: 0, dz: HALF, yaw: 90 },  // N 边
  { di: 1, dj: 0, dx: HALF, dz: 0, yaw: 180 }, // E 边
  { di: 0, dj: -1, dx: 0, dz: -HALF, yaw: 270 }, // S 边
  { di: -1, dj: 0, dx: -HALF, dz: 0, yaw: 0 },  // W 边
];
// 凸角（角点周围 4 格只占 1 格）：cliff_270；key=被占格方位
const CONVEX_YAW = { NE: 270, NW: 180, SW: 90, SE: 0 };
// 凹角（占 3 格）：cliff_90；key=缺失格方位
const CONCAVE_YAW = { NW: 0, SW: 270, NE: 90, SE: 180 };

let seq = 0;
function makeItem(pf, x, y, z, rotY, parentPath, walkable, displaySuffix) {
  const id = `new:testice:${String(seq++).padStart(4, "0")}`;
  return {
    instanceId: id,
    hierarchyPath: id,
    prefabGuid: pf.guid,
    prefabAssetPath: pf.assetPath,
    parentPath,
    displayName: displaySuffix ? `${pf.id} ${displaySuffix}` : pf.id,
    localPosition: { x, y, z },
    worldPosition: { x, y, z },
    localRotationX: pf.rotX,
    localRotationY: rotY,
    localRotationZ: 0,
    localScale: { ...pf.scale },
    colliderCenter: { x: 0, y: 0, z: 0 },
    footprint: { ...pf.footprint },
    walkable: !!walkable,
    airWall: false,
    stubKind: "",
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

// 确定性伪随机（同位置多次运行结果一致），用于直边 01/02 变体交替
function pickVariant(a, b, i, j, salt) {
  const h = Math.abs((i * 73856093) ^ (j * 19349663) ^ (salt * 83492791));
  return h % 2 === 0 ? a : b;
}

/**
 * 铺一座岛：地砖 + 直边冰崖 + 崖顶雪盖 + 转角。
 * opts: { parent, walkable, ox, oz, accents } —— ox/oz 为格基准（中心 = ox+1.2i）
 */
function emitIsland(items, cells, salt, opts = {}) {
  const parent = opts.parent || "Art/MainIsland";
  const walkable = opts.walkable !== false;
  const ox = opts.ox !== undefined ? opts.ox : HALF;
  const oz = opts.oz !== undefined ? opts.oz : HALF;
  const accents = opts.accents !== undefined ? opts.accents : iceAccents;
  const wx = (i) => ox + CELL * i;
  const wz = (j) => oz + CELL * j;
  const has = (i, j) => cells.has(key(i, j));
  // 1) 地砖
  for (const k of cells) {
    const [i, j] = k.split(",").map(Number);
    const pf = accents && accents.has(k) ? PREFABS.ice_tile : PREFABS.snow_tile;
    items.push(makeItem(pf, wx(i), 0.08, wz(j), 0, parent, walkable));
  }
  // 2) 直边冰崖 + 崖顶雪盖
  for (const k of cells) {
    const [i, j] = k.split(",").map(Number);
    const exposed = EDGE_RULES.filter((r) => !has(i + r.di, j + r.dj));
    for (const r of exposed) {
      const cf = pickVariant(PREFABS.cliff_s1, PREFABS.cliff_s2, i, j, salt + r.yaw);
      items.push(makeItem(cf, wx(i) + r.dx, 0, wz(j) + r.dz, r.yaw, parent, false));
    }
    // 雪盖放在边砖中心；凸角砖（≥2 面外露）只盖第一面，避免同位堆叠
    const capEdge = exposed[0];
    if (capEdge) {
      const cap = pickVariant(PREFABS.cap_s1, PREFABS.cap_s2, i, j, salt + 7);
      items.push(makeItem(cap, wx(i), 0.08, wz(j), capEdge.yaw, parent, false));
    }
  }
  // 3) 转角（凸角 cliff_270 / 凹角 cliff_90），角点 = 相邻砖中心±半格
  let minI = 1e9, maxI = -1e9, minJ = 1e9, maxJ = -1e9;
  for (const k of cells) {
    const [i, j] = k.split(",").map(Number);
    minI = Math.min(minI, i); maxI = Math.max(maxI, i);
    minJ = Math.min(minJ, j); maxJ = Math.max(maxJ, j);
  }
  for (let a = minI; a <= maxI + 1; a++) {
    for (let b = minJ; b <= maxJ + 1; b++) {
      // 角点 (ox+1.2a-0.6, oz+1.2b-0.6) 周围 4 格：SW(a-1,b-1) SE(a,b-1) NW(a-1,b) NE(a,b)
      const occ = [
        ["SW", has(a - 1, b - 1)],
        ["SE", has(a, b - 1)],
        ["NW", has(a - 1, b)],
        ["NE", has(a, b)],
      ].filter(([, v]) => v).map(([n]) => n);
      if (occ.length === 1) {
        items.push(makeItem(PREFABS.cliff_270, ox + CELL * a - HALF, 0, oz + CELL * b - HALF, CONVEX_YAW[occ[0]], parent, false));
      } else if (occ.length === 3) {
        const missing = ["SW", "SE", "NW", "NE"].find((n) => !occ.includes(n));
        items.push(makeItem(PREFABS.cliff_90, ox + CELL * a - HALF, 0, oz + CELL * b - HALF, CONCAVE_YAW[missing], parent, false));
      }
    }
  }
}

// ---------------------------------------------------------------- 漂浮小冰岛（move group 漂动）
// 每块 = 1~2 格冰面 + 侧面半浮冰块，整体由 moveControls 驱动小环线漂移
const floeGroups = []; // { name, memberIds, waypoints, interval }
function emitFloe(items, name, tileCells, decor, waypoints, interval) {
  const memberIds = [];
  for (const [i, j] of tileCells) {
    const t = makeItem(PREFABS.ice_tile, cx(i), 0.08, cz(j), 0, "Art/Floes", false);
    items.push(t);
    memberIds.push(t.instanceId);
  }
  for (const [pf, x, y, z, yaw] of decor) {
    const d = makeItem(pf, x, y, z, yaw, "Art/Floes", false);
    items.push(d);
    memberIds.push(d.instanceId);
  }
  floeGroups.push({ name, memberIds, waypoints, interval });
}

function emitFloes(items) {
  // floe1：左岛南侧 2×2（中心 -7.2,-9.6），0.6m 小方环线 20s/圈
  emitFloe(items, "Floe1",
    [[-7, -9], [-6, -9], [-7, -8], [-6, -8]],
    [[PREFABS.iceblock_03, -7.2, -1.15, -11.4, 40], [PREFABS.snowmound_02, -7.8, 0.08, -9.0, 70]],
    [[-7.2, -9.6], [-6.6, -9.6], [-6.6, -9.0], [-7.2, -9.0], [-7.2, -9.6]], 5);
  // floe2：右岛南侧 1×2（中心 14.4,-7.8），0.6m 小方环线 24s/圈
  emitFloe(items, "Floe2",
    [[11, -7], [12, -7]],
    [[PREFABS.iceblock_01, 16.2, -1.15, -7.8, 160]],
    [[14.4, -7.8], [15.0, -7.8], [15.0, -7.2], [14.4, -7.2], [14.4, -7.8]], 6);
  // floe3：西北水面 1×2 竖条（中心 -0.6,10.8），0.6m 小方环线 20s/圈
  // （别用共线往返路线：会被烘焙器简化成 pingpong 并丢失最远端）
  emitFloe(items, "Floe3",
    [[-1, 8], [-1, 9]],
    [[PREFABS.snowballpile, -0.6, 0.08, 10.2, 0]],
    [[-0.6, 10.8], [0.0, 10.8], [0.0, 11.4], [-0.6, 11.4], [-0.6, 10.8]], 5);
}

function buildMoveControls() {
  return {
    groups: floeGroups.map((g, gi) => ({
      id: `testice-${g.name}`,
      displayName: g.name,
      itemInstanceIds: g.memberIds,
      floorInstanceIds: [],
      objectInstanceIds: [],
      memberOffsets: [], // 留空：烘焙时按成员当前位置自动捕获
      memberStatic: [],
      memberGroups: [],
      startDelay: 0,
      loop: false,
      loopDelay: 0,
      waitForFinished: false,
      startTrigger: "", // 空 = 回合开始自动启动（同 3-4 Islands）
      cancelTrigger: "",
      endTrigger: "",
      finishedTrigger: "",
      applyRootMotion: false,
      waypoints: g.waypoints.map(([x, z], wi) => ({
        id: `${g.name}-w${wi}`, x, z, hasTime: false, t: 0, wait: 0, segmentSeconds: 0,
      })),
      events: [{
        id: `${g.name}-ev0`,
        type: "move",
        triggerName: "StartScrolling",
        delay: 0,
        intervalSeconds: g.interval,
        waypointIds: g.waypoints.map((_, wi) => `${g.name}-w${wi}`),
        loop: true, // 片段自身循环（loopTime=1，无出口过渡，同 3-4 Islands 滚动）
        pingpong: false,
        yTo: 0,
        liftSeconds: 0,
        liftHeight: 0,
        dropSeconds: 0,
      }],
      groupHierarchyPath: `Design/Animated Objects/${g.name}`,
    })),
  };
}

// ---------------------------------------------------------------- 装饰
function emitDecor(items) {
  const D = (pf, i, j, yaw, y = 0.08) =>
    items.push(makeItem(pf, cx(i), y, cz(j), yaw, "Art/Decoration", false));
  // 左岛（避开出生点 (-7,-2)/(-6,0)）
  D(PREFABS.snowmound_01, -8, -5, 20);
  D(PREFABS.snowballpile, -5, -4, 0);
  D(PREFABS.iceblock_01, -3, 3, 15);
  D(PREFABS.snowmound_05, -6, 3, 130);
  D(PREFABS.evergreen, -8, 1, 0);
  D(PREFABS.snowmound_03, -4, -3, 260);
  // 中岛（i∈[1,4]，中心 x=1.2+1.2(i-1)... 此处用世界坐标直接摆）
  items.push(makeItem(PREFABS.snowmound_04, 1.2, 0.08, 1.8, 0, "Art/Decoration", false));
  items.push(makeItem(PREFABS.iceblock_01, 4.8, 0.08, -1.8, 60, "Art/Decoration", false));
  items.push(makeItem(PREFABS.sign_02, 3.6, 0.08, 1.8, 180, "Art/Decoration", false));
  // 右岛（避开出生点 (8,-2)/(9,0)）
  D(PREFABS.snowmound_02, 11, -3, 45);
  D(PREFABS.iceblock_02, 7, 1, 80);
  D(PREFABS.snowmound_08, 9, 2, 200);
  D(PREFABS.snowmound_06, 8, -3, 310);
  D(PREFABS.iceblock_03, 11, 1, 25);
  // 北侧布景岛：中秋宝塔（亭）
  items.push(makeItem(PREFABS.pagoda, 3.0, 0.08, 10.2, 0, "Art/Backdrop", false));
  // 水面上半浮的冰块（仿 3-4，y=-1.15；避开水道，不挡过岛路线）
  const W = (pf, x, z, yaw) =>
    items.push(makeItem(pf, x, -1.15, z, yaw, "Art/Decoration", false));
  W(PREFABS.iceblock_01, -11.4, 2.4, 210);
  W(PREFABS.iceblock_02, 18.0, -2.4, 75);
  W(PREFABS.iceblock_03, 2.4, -4.2, 150);
  W(PREFABS.iceblock_01, 12.6, 5.4, 30);
  // 中秋漂浮灯笼（水面 y=-0.81）
  const L = (x, z, yaw) =>
    items.push(makeItem(PREFABS.lantern, x, -0.81, z, yaw, "Art/Decoration", false));
  L(-4.2, -8.4, 15);
  L(9.0, -6.6, 200);
  L(13.8, 6.6, 120);
  L(-3.0, 8.4, 300);
}

// ---------------------------------------------------------------- API
const args = process.argv.slice(2);
const argVal = (name, dflt) => {
  const idx = args.indexOf(name);
  return idx >= 0 ? args[idx + 1] : dflt;
};
const BASE = argVal("--base-url", "http://127.0.0.1:8765");
const APPLY = args.includes("--apply");

async function api(path, method, body) {
  const r = await fetch(`${BASE}${path}`, {
    method: method || "GET",
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await r.text();
  if (!r.ok) throw new Error(`${method || "GET"} ${path} -> ${r.status}: ${text.slice(0, 300)}`);
  try { return JSON.parse(text); } catch { return text; }
}

// 出生点：Player1/2 左岛，Player3/4 右岛（仿 3-4 的 2+2）
const SPAWN_POS = {
  "Player 1": { x: cx(-7), z: cz(-2) },
  "Player 2": { x: cx(-6), z: cz(0) },
  "Player 3": { x: cx(8), z: cz(-2) },
  "Player 4": { x: cx(9), z: cz(0) },
};

async function main() {
  // 1) 现有布局：保留 4 个出生点（改坐标），其余内容全量重建（差量删除旧生成物）
  const cur = await api(`/api/scene/layout?assetPath=${encodeURIComponent(SCENE)}`);
  const players = (cur.items || []).filter((it) => /Player\.prefab$/.test(it.prefabAssetPath || ""));
  if (players.length !== 4) throw new Error(`出生点数量异常：${players.length}（期望 4）`);
  for (const p of players) {
    const np = SPAWN_POS[p.displayName];
    if (!np) throw new Error(`未知出生点：${p.displayName}`);
    p.localPosition = { x: np.x, y: 0, z: np.z };
    p.worldPosition = { x: np.x, y: 0, z: np.z };
    p.walkable = false;
  }

  // 2) 生成三岛 + 布景岛 + 浮冰 + 装饰 + 水面 + 落雪
  const gen = [];
  emitIsland(gen, leftIsland, 11);
  emitIsland(gen, midIsland, 23, { ox: 0, oz: -HALF });
  emitIsland(gen, rightIsland, 37);
  emitIsland(gen, pagodaIsland, 53, { parent: "Art/Backdrop", walkable: false, accents: new Map() });
  emitFloes(gen);
  emitDecor(gen);
  const water = makeItem(PREFABS.water, 3.0, -0.81, 1.2, 0, "Art/Environment", false);
  water.localScale = { x: 34, y: 27, z: 1 };
  gen.push(water);
  gen.push(makeItem(PREFABS.snowfall, 3.0, 0, 1.2, 0, "Art/Environment", false));

  const doc = {
    sceneAssetPath: SCENE,
    items: players.concat(gen),
    // 中岛隐形过桥：airFloor（Ground 层碰撞，无可见面），比岛面外扩 0.9m，
    // 两侧各留 0.9m 碰撞缝隙（可跨过）——和 3-4 浮冰 Ground 碰撞完全同款。
    floors: [{
      instanceId: "new:testice-airfloor-mid",
      hierarchyPath: "Design/Collision/Col_AirFloor",
      parentPath: "Design/Collision",
      displayName: "Col_AirFloor",
      surfaceKind: "solid",
      meshType: "plane",
      meshFileId: 0,
      airFloor: true,
      localPosition: { x: 3.0, y: 0.08, z: 0 },
      worldPosition: { x: 3.0, y: 0.08, z: 0 },
      localRotationY: 0,
      localScale: { x: 1, y: 1, z: 1 },
      widthUnits: 6.6,  // x: -0.3 .. 6.3（岛面 0.6..5.4 外扩 0.9）
      depthUnits: 6.6,  // z: -3.3 .. 3.3（岛面 ±2.4 外扩 0.9）
      widthCells: 6,
      depthCells: 6,
    }],
    moveControls: buildMoveControls(),
    cameraInfo: {
      backgroundColor: "#000000",
      fieldOfView: 45,
      position: { x: 3.0, y: 15, z: -15 },
      pitch: 54,
      yaw: 0,
      roll: 0,
      nearClip: 0.3,
      farClip: 1000,
    },
    lights: [
      {
        hierarchyPath: "Art/Lights/day",
        displayName: "day",
        lightType: 1,
        color: "#636BFF", // 3-4 同款冷蓝平行光
        intensity: 0.7,
        range: 10,
        spotAngle: 30,
        enabled: true,
        eulerAngles: { x: 39.16, y: 350.1, z: 0 },
      },
    ],
  };

  const byId = {};
  for (const it of gen) byId[it.displayName.split(" ")[0]] = (byId[it.displayName.split(" ")[0]] || 0) + 1;
  console.log(`生成物品：${gen.length} 个（另含 4 出生点）+ ${floeGroups.length} 个浮冰移动组`);
  for (const [id, n] of Object.entries(byId).sort()) console.log(`  ${id} × ${n}`);

  if (!APPLY) {
    console.log("\n（未加 --apply，不写回。文档大小：", JSON.stringify(doc).length, "字节）");
    return;
  }

  // 3) 写回布局（全量 + 重建行走碰撞 + 烘焙移动组动画）
  console.log("\n写回布局…");
  const r1 = await api(`/api/scene/layout?snap=0.01&syncWalkable=1`, "POST", doc);
  console.log("布局写回：", typeof r1 === "string" ? r1 : JSON.stringify(r1));

  // 4) 落水死亡特效（水背景）
  console.log("设置死亡主题为 water…");
  console.log(await api("/api/scene/death", "POST", { sceneAssetPath: SCENE, theme: "water" }));

  // 5) KillPlane 覆盖整个布局范围（落水重生）
  console.log("设置 KillPlane 范围…");
  console.log(await api("/api/scene/killplane", "POST", { sceneAssetPath: SCENE, cx: 3.0, cz: 1.2, sx: 36, sz: 30 }));

  // 6) 冰雪 BGM + 环境声 + 音频目录（保留 testice 现有 5 个目录 + DLC03）
  console.log("设置冰雪 BGM…");
  console.log(await api("/api/level/audio", "POST", {
    sceneAssetPath: SCENE,
    inLevelMusicGuid: "e7478bf2c298f4543868f5f42a972711", // DLC_03_InGame_Music_SO
    ambiences: ["DLC_03_Amb"],
    audioDirectoryGuids: [
      "45abe9378aa684144b655a71d85d422a",
      "b298262cebf31cc41ad9e3d8eef6104a",
      "4c50268f8f711f0478ed2afe1fe04148",
      "c3d1d7d9880429247949bfc85cb265cd",
      "193b2f9bf726f474bbcad5946bf20392",
      "1e0a31a7d9dc8ec419e69701fef59706", // DLC03AudioDirectory
    ],
    onDeathEffectGuid: "66a94ec69fed59240b93b7a666dfc2be", // WaterSplash_Particle_003_SO
  }));

  console.log("\n全部完成。请在 Unity 中 Play 验证（重点：中岛两侧水道能否直接走过、浮冰漂动、BGM/落水）。");
}

main().catch((e) => {
  console.error("失败：", e.message);
  process.exit(1);
});
