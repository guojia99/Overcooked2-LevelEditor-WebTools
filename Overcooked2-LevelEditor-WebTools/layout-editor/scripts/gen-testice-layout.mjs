#!/usr/bin/env node
/**
 * gen-testice-layout.mjs — 生成 my_test/testice 冰雪三岛布景并写回 Unity（v2）。
 *
 * 布局（格 = 1.2m，行走面统一高度，左中右结构，背景为水）：
 *   左岛 8×12（四角各阶梯切 3 格的斜角）、中岛 4×4、右岛 8×8（同款切角）
 *   中岛与左右岛之间各留 1 格（1.2m）水道，视觉隔水；
 *   中岛下方垫一块隐形 airFloor 行走碰撞（左右各嵌入邻岛碰撞体 0.6m、纵向
 *   外扩 0.9m，学 3-4 浮冰 Ground 碰撞比可视面大一圈的做法），走上去不掉水。
 *   远景聚集上半部分：北/东北/西北三座布景岛（中秋宝塔、DLC13 寺庙、雪树、
 *   企鹅）+ 北带与东侧远处冰山；下半部分水面留空（预留给动态冰岛）。
 *   左右岛西/东侧各伸出 2 格宽走廊连接 5×5 庭院（DLC13 中秋灯柱/围栏/盒灯/
 *   桌椅 + 普通树），水面更多浮冰与中秋漂浮灯。
 *   岛上冰块配隐形碰撞盒（1.2×2×1.2，阻挡玩家穿行）。背景水面东扩。
 *   岛面全部雪地砖（ice_floor 只用于水面浮冰）；边缘贴花盖在地砖/崖顶接缝线上。
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
// 行走层高度：必须为 0！3-4 的 Col_Ice 与编辑器自制关（jia_carnival）的 Col_Floor
// 顶面都在 y=0，角色出生/行走基准也是 0。地砖视觉也放 0 与碰撞对齐；
// 之前放 0.08 会导致胶囊嵌进地板盒侧面 → 到处"空气墙"。
const FLOOR_Y = 0;
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
  // 地板贴花放大 6%（本体 ≈1.2m 与格距相等，原始尺寸有发丝缝漏底；1.06 → 相邻互搭 0.07m）
  snow_tile: P("snow_floor_01", "47ee35a1dc6245e4b8a6da98121b63fc", { rotX: 90, scale: { x: 1.06, y: 1.06, z: 1.06 } }),
  ice_tile: P("ice_floor_01", "46d2e3289d9e97543b43ede1a072ccfc", { rotX: 90 }),
  cliff_s1: P("m_dlc3_icecliff_straight_01", "6abdbf27f38cbb94b8265c4901667159"),
  cliff_s2: P("m_dlc3_icecliff_straight_02", "96188dcc3bafdf64289d423aad495613"),
  cliff_90: P("m_dlc3_icecliff_90", "a219a44b013140f4d9468fd3195ee012"),
  cliff_270: P("m_dlc3_icecliff_270", "c3d40717412382845bee670975a480fc"),
  // 雪盖：wrapper 用小缩放（prefab 本体烘了 100 倍；本体长 0.96m < 格距 1.2m。
  // 0.0125 → 1.44m，与放大后的地板/邻条互搭约 0.12m，消除角部/切角阶梯/边缘拼接缝）
  cap_s1: P("snow_straight_01", "a21b1b02d0c27fc4b997c86bd7dc3e95", { scale: { x: 0.0125, y: 0.0125, z: 0.0125 } }),
  cap_s2: P("snow_straight_02", "070c192a1c96f6245b0424e83ae3a1e3", { scale: { x: 0.0125, y: 0.0125, z: 0.0125 } }),
  // 角点雪地贴花（凸角 snow_270 = cliff_270 yaw+180；凹角 snow_90 = cliff_90 yaw+90）
  cap_270: P("snow_270", "b189d42b8d4bfe84cbc99271b1dda0a4", { scale: { x: 0.0125, y: 0.0125, z: 0.0125 } }),
  cap_90: P("snow_90", "4a1047cc09bcf2a4db77f698d2933f9d", { scale: { x: 0.0125, y: 0.0125, z: 0.0125 } }),
  water: P("Water_01", "a3288048c24aaa44181e6970566891bd", { rotX: 90 }),
  snowfall: P("Snowfall_01", "c8ef56094c25a2746b37e348ab7a46ef"),
  iceblock_01: P("m_dlc3_iceblock_01", "e7edce59c2dcfce4e8961659dc5644a6"),
  iceblock_02: P("m_dlc3_iceblock_02", "aaed9f7a452440a45800d3000d5761aa"),
  iceblock_03: P("m_dlc3_iceblock_03", "89408557682fbe640b7542cc208fb09b"),
};
// 积雪常青树（common03/dlc03）
// 中秋装饰（common03/dlc13）
PREFABS.pagoda = {
  id: "p_dlc13_pagoda_1",
  guid: "6a27c4739efc10de0b072efeec34ccf8",
  assetPath: "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_pagoda_1.prefab",
  rotX: 0,
  scale: { x: 1, y: 1, z: 1 },
  footprint: { cellsX: 6, cellsZ: 6 },
};
// 远景布景（common03 / common01）
PREFABS.temple13 = {
  id: "p_dlc13_temple_1",
  guid: "cd8240bdfa9f2843f88f7e258e863035",
  assetPath: "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_temple_1.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 6, cellsZ: 5 },
};
PREFABS.penguin = {
  id: "NPC_Penguin",
  guid: "c53944d537b0149468a687e9c10db9b0",
  assetPath: "Assets/common01/prefabs/art/npc/NPC_Penguin.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 1 },
};
PREFABS.tree_snow = {
  id: "m_dlc3_evergreen_snow_01", guid: "ab570a00a428bba2774d2639872c30ad",
  assetPath: "Assets/common03/prefabs/dlc03/art/dlc03_christmas/m_dlc3_evergreen_snow_01.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 3, cellsZ: 3 },
};
PREFABS.tree_snow_big = {
  id: "p_dlc09_evergreen_snow_01", guid: "581f3c1871aa368bf360575a8fcecb35",
  assetPath: "Assets/common03/prefabs/dlc09/art/dlc09_wonderland/p_dlc09_evergreen_snow_01.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 4, cellsZ: 4 },
};
// 坐着用餐/喝茶的客人桌（core legacy）
PREFABS.table_guests = {
  id: "table_customers", guid: "2bce6efbb09e7fc34c26ccc555235b92",
  assetPath: "Assets/common03/prefabs/core/art/legacy/table_customers.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 3 },
};
PREFABS.table_guests2 = {
  id: "table_customers_2", guid: "74b63cddabe72798276f6f3d078915b8",
  assetPath: "Assets/common03/prefabs/core/art/legacy/table_customers_2.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 3 },
};
PREFABS.lantern_stand = {
  id: "p_dlc13_lantern_stand_1", guid: "c331c6f0e4c28aab5da0c3478ad25814",
  assetPath: "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_lantern_stand_1.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 1 },
};
PREFABS.box_lantern_01 = {
  id: "p_dlc13_box_lantern_01", guid: "11f2cfbfcef7810ea00842a69983a98e",
  assetPath: "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_box_lantern_01.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 1 },
};
PREFABS.box_lantern_02 = {
  id: "p_dlc13_box_lantern_02", guid: "d403956af5d5f406bf6c0ee3cbc8f22b",
  assetPath: "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_box_lantern_02.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 1 },
};
PREFABS.tablechair = {
  id: "p_dlc13_npctablechair_1", guid: "a59adb9b610c322ad83bce27be00b07c",
  assetPath: "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_npctablechair_1.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 3 },
};
PREFABS.fence = {
  id: "p_dlc13_fence_02", guid: "131d6c48257a781233e1154454cb215f",
  assetPath: "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_fence_02.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 1 },
};
PREFABS.floatingbox = {
  id: "p_floatingboxlantern_01", guid: "ddd0caf219253c051a01ab86c925b1fe",
  assetPath: "Assets/common03/prefabs/dlc13/art/dlc13/p_floatingboxlantern_01.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 1 },
};
PREFABS.air_lantern = {
  id: "p_dlc13_lantern_floating_air_02", guid: "96cc4b04f1a60e4658b2d68ac2d07f78",
  assetPath: "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_lantern_floating_air_02.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 1 },
};
PREFABS.lantern = {
  id: "p_dlc13_lantern_floating_water_02",
  guid: "4159ff454b43fed9331ef9ca63f838d1",
  assetPath: "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_lantern_floating_water_02.prefab",
  rotX: 0,
  scale: { x: 1, y: 1, z: 1 },
  footprint: { cellsX: 1, cellsZ: 1 },
};

// ---------------------------------------------------------------- 新素材（v3 丰富化）
// 冰雪小件（dlc03_christmas / common01+common03）
const C1 = (id, guid, sub) => ({
  id, guid,
  assetPath: `Assets/common01/prefabs/art/dlc03_christmas/${id}.prefab`,
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: sub[0], cellsZ: sub[1] },
});
PREFABS.snowmound_02 = C1("snowmound_02", "b44e0ee71eed03344ac8e1466bd4839f", [1, 1]);
PREFABS.snowmound_04 = C1("snowmound_04", "8452a972465115c47f67270ec476b76", [1, 1]);
PREFABS.snowballpile = C1("snowballpile_01", "7420c01cb0ea9c94ea2a7b75033325eb", [1, 1]);
const C3 = (id, guid, cellsX, cellsZ) => ({
  id, guid,
  assetPath: `Assets/common03/prefabs/dlc03/art/dlc03_christmas/${id}.prefab`,
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX, cellsZ },
});
PREFABS.icicle_01 = C3("icicle_01", "0ae6e03af12bb32b8d3920f4ac4028f9", 1, 1);
PREFABS.icicle_03 = C3("icicle_03", "a779ba953b426d644b95204cc52d8801", 1, 1);
PREFABS.gift_01 = C3("gift_01", "36e862b3505943bcec122b0976060c1b", 1, 1);
PREFABS.gift_02 = C3("gift_02", "06e738f94cca61b10c7978e42a509b42", 1, 1);
PREFABS.candycane = C3("giant_candycane_01", "2721e1b896e8eef33af988c922a3c89e", 1, 1);
PREFABS.tree_decorated = C3("evergreen_decorated_01", "bd8eb5af71618daa169fa9bb35515379", 3, 3);
PREFABS.streetlamp = C3("m_streetlamp_01", "6d32670545baffa35c230e9344dd436c", 1, 1);
// 中秋/灯会（dlc13）
const D13 = (id, guid, cellsX, cellsZ) => ({
  id, guid,
  assetPath: `Assets/common03/prefabs/dlc13/art/dlc13/${id}.prefab`,
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX, cellsZ },
});
PREFABS.lantern_big = D13("p_dlc13_lantern_1", "512172025459cabccfd154454c63871c", 1, 1);
PREFABS.lotus = D13("p_lotuscandle_01", "2502e3aa7f097a507bd8795d25ecf6d0", 1, 1);
PREFABS.archway = D13("p_dlc13_map_archway_01", "35fa7c4b7465bf47f53e1baa258158c3", 1, 3);
PREFABS.dragon_statue = D13("p_dlc13_map_dragon_statue_01", "fc6cf7d7d61cf2844f62dddb0e12b004", 1, 1);
PREFABS.ripple = D13("p_dlc13_ripple_02", "3730693e98869d8f86ea8b65b7e03151", 2, 2);
PREFABS.fishfloat = D13("p_dlc13_fishfloat_01", "d9c5838d8916206ca2f5c77c8368655c", 3, 1);
// NPC（eskimo / 知更鸟 / 圣诞 NPC / 中式侍者）
PREFABS.eskimo = {
  id: "npc_eskimo", guid: "69de55db58333bae20843c07153c05ef",
  assetPath: "Assets/common03/prefabs/core/art/npc/npc_eskimo.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 1 },
};
PREFABS.robin = {
  id: "robinground_01", guid: "75a09f617ccd4fe0b80c28e00a6f19f0",
  assetPath: "Assets/common03/prefabs/dlc03/art/npc/robinground_01.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 1 },
};
PREFABS.npc03 = {
  id: "DLC03_NPC_02", guid: "865bc89e466afcd41ab498be3f2c2457",
  assetPath: "Assets/common01/prefabs/art/npc/DLC03_NPC_02.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 1 },
};
PREFABS.waiter = {
  id: "NPC_Asian_Waiter_01", guid: "2bf53d6647a60464bb4f33df56650fc4",
  assetPath: "Assets/common01/prefabs/art/npc/NPC_Asian_Waiter_01.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 1 },
};
// 雪橇（dlc09 wonderland）
PREFABS.sled = {
  id: "p_dlc09_sled_02", guid: "2d5e2e3c03e6b2d893980f8348761e88",
  assetPath: "Assets/common03/prefabs/dlc09/art/dlc09_wonderland/p_dlc09_sled_02.prefab",
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 2 },
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
// 中岛 4×4（标准半格基准，岛缘 0.0..4.8 / ±2.4）——两侧水道各 1 格（1.2m）
const midIsland = rectCells(0, 3, -2, 1, []);
// 右岛 8×8，西缘 x=6.0
const rightIsland = rectCells(5, 12, -4, 3, cornerBevels(5, 12, -4, 3));
// 走廊+庭院：并入主岛格集，连接处不生成崖（同一 emitIsland 格集）
function addCells(set, i0, i1, j0, j1, remove = []) {
  for (let i = i0; i <= i1; i++) for (let j = j0; j <= j1; j++) set.add(key(i, j));
  for (const [i, j] of remove) set.delete(key(i, j));
}
// 西走廊 2 格宽（左岛西缘 → 西庭院 5×5，切角点）
addCells(leftIsland, -13, -10, -1, 0);
addCells(leftIsland, -18, -14, -3, 1, [[-18, -3], [-14, -3], [-18, 1], [-14, 1]]);
// 东走廊 2 格宽（右岛东缘 → 东庭院 5×5，切角点）
addCells(rightIsland, 13, 16, -1, 0);
addCells(rightIsland, 17, 21, -3, 1, [[17, -3], [21, -3], [17, 1], [21, 1]]);

// 北侧中秋布景岛 5×5（不可行走，纯背景），切四角
const pagodaIsland = rectCells(0, 4, 6, 10, cornerBevels(0, 4, 6, 10).filter(
  ([i, j]) => (i === 0 || i === 4) && (j === 6 || j === 10))); // 5×5 只切角点 1 格

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
 * 铺一座岛：地砖 + 直边冰崖 + 边缘贴花 + 转角。
 * opts: { parent, walkable, ox, oz } —— ox/oz 为格基准（中心 = ox+1.2i）
 * 岛面全部雪地砖（ice_floor 只用于水面浮冰；岛上不用——看着像洞）。
 */
function emitIsland(items, cells, salt, opts = {}) {
  const parent = opts.parent || "Art/MainIsland";
  const walkable = opts.walkable !== false;
  const ox = opts.ox !== undefined ? opts.ox : HALF;
  const oz = opts.oz !== undefined ? opts.oz : HALF;
  const wx = (i) => ox + CELL * i;
  const wz = (j) => oz + CELL * j;
  const has = (i, j) => cells.has(key(i, j));
  // 1) 地砖
  for (const k of cells) {
    const [i, j] = k.split(",").map(Number);
    items.push(makeItem(PREFABS.snow_tile, wx(i), FLOOR_Y, wz(j), 0, parent, walkable));
  }
  // 2) 直边冰崖（水线，y=0）+ 边缘雪贴花（盖缝，y=FLOOR_Y，放边线上，每条外露边都盖）
  for (const k of cells) {
    const [i, j] = k.split(",").map(Number);
    const exposed = EDGE_RULES.filter((r) => !has(i + r.di, j + r.dj));
    for (const r of exposed) {
      const cf = pickVariant(PREFABS.cliff_s1, PREFABS.cliff_s2, i, j, salt + r.yaw);
      items.push(makeItem(cf, wx(i) + r.dx, 0, wz(j) + r.dz, r.yaw, parent, false));
      const cap = pickVariant(PREFABS.cap_s1, PREFABS.cap_s2, i, j, salt + r.yaw + 7);
      items.push(makeItem(cap, wx(i) + r.dx, FLOOR_Y, wz(j) + r.dz, r.yaw, parent, false));
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
        const yaw = CONVEX_YAW[occ[0]];
        items.push(makeItem(PREFABS.cliff_270, ox + CELL * a - HALF, 0, oz + CELL * b - HALF, yaw, parent, false));
        // 凸角雪地贴花：snow_270 同位，yaw = 凸角崖 yaw + 180（3-4 实测规则）
        items.push(makeItem(PREFABS.cap_270, ox + CELL * a - HALF, FLOOR_Y, oz + CELL * b - HALF, (yaw + 180) % 360, parent, false));
      } else if (occ.length === 3) {
        const missing = ["SW", "SE", "NW", "NE"].find((n) => !occ.includes(n));
        const yaw = CONCAVE_YAW[missing];
        items.push(makeItem(PREFABS.cliff_90, ox + CELL * a - HALF, 0, oz + CELL * b - HALF, yaw, parent, false));
        // 凹角雪地贴花：snow_90 同位，yaw = 凹角崖 yaw + 90（3-4 实测规则）
        items.push(makeItem(PREFABS.cap_90, ox + CELL * a - HALF, FLOOR_Y, oz + CELL * b - HALF, (yaw + 90) % 360, parent, false));
      }
    }
  }
}

// ---------------------------------------------------------------- 远景布景（上半部分聚集，下半留空给动态冰岛）
// 东北布景岛 4×4（只切角点 1 格）、西北布景岛 4×4（切 4 角点；v3 扩自 3×3 以放下 4×4 大雪树）
const neIslet = rectCells(14, 17, 8, 11, [[14, 8], [17, 8], [14, 11], [17, 11]]);
const nwIslet = rectCells(-11, -8, 8, 11, [[-11, 8], [-8, 8], [-11, 11], [-8, 11]]);

// 布景岛装饰坐标一律取岛格集内的格心（cx(i), cz(j)）——之前的自由坐标导致
// NPC/桌椅悬在切角格外的水面上方。
function emitBackdrop(items) {
  const B = (pf, i, j, yaw, y = FLOOR_Y) =>
    items.push(makeItem(pf, cx(i), y, cz(j), yaw, "Art/Backdrop", false));
  // 中秋岛（宝塔，i∈[0,4] j∈[6,10] 切 4 角点）：亭内坐桌喝茶客人 ×2 + 企鹅 +
  // 亭周灯柱/盒灯 + 新素材（eskimo 岛民 / 中式侍者 / 龙雕像）
  items.push(makeItem(PREFABS.pagoda, 3.0, FLOOR_Y, 10.2, 0, "Art/Backdrop", false));
  B(PREFABS.table_guests, 1, 7, 0);
  B(PREFABS.table_guests, 3, 7, 180);
  B(PREFABS.penguin, 1, 10, 200);
  B(PREFABS.lantern_stand, 0, 9, 45);
  B(PREFABS.lantern_stand, 4, 9, 315);
  B(PREFABS.box_lantern_01, 1, 6, 135);
  B(PREFABS.box_lantern_01, 3, 6, 225);
  B(PREFABS.eskimo, 0, 7, 300);
  B(PREFABS.waiter, 4, 7, 240);
  B(PREFABS.dragon_statue, 3, 10, 90);
  // 东北岛（寺庙，i∈[14,17] j∈[8,11] 切 4 角点）：坐桌客人 + 企鹅 + 知更鸟 + 亭周灯柱
  items.push(makeItem(PREFABS.temple13, 19.2, FLOOR_Y, 12.0, 200, "Art/Backdrop", false));
  B(PREFABS.table_guests2, 15, 8, 0);
  B(PREFABS.penguin, 17, 10, 140);
  B(PREFABS.robin, 16, 11, 200);
  B(PREFABS.lantern_stand, 15, 10, 45);
  B(PREFABS.lantern_stand, 16, 10, 315);
  B(PREFABS.box_lantern_02, 16, 8, 180);
  // 西北岛（雪树，i∈[-11,-8] j∈[8,11] 切 4 角点）：大雪树居中 + 企鹅/圣诞 NPC/雪堆
  items.push(makeItem(PREFABS.tree_snow_big, -10.8, FLOOR_Y, 12.0, 30, "Art/Backdrop", false));
  B(PREFABS.penguin, -11, 9, 90);
  B(PREFABS.penguin, -8, 10, 250);
  B(PREFABS.npc03, -10, 11, 160);
  B(PREFABS.snowmound_02, -9, 8, 0);
}

// ---------------------------------------------------------------- 走廊+庭院装饰
// 中秋庭院升级：装饰圣诞树（原普通雪树）+ 雪橇/礼物/糖果拐杖/冰晶/雪堆/街灯/大红灯笼，
// 走廊入口加 dlc13 拱门。坐标全部格心（cx/cz）。
function emitCourtyards(items) {
  const C = (pf, i, j, yaw, y = FLOOR_Y) =>
    items.push(makeItem(pf, cx(i), y, cz(j), yaw, "Art/Decoration", false));
  // 西庭院（i∈[-18,-14] j∈[-3,1]，切 4 角点）
  C(PREFABS.tree_decorated, -16, -1, 270);      // 中央：装饰圣诞树
  C(PREFABS.lantern_stand, -17, -2, 135);
  C(PREFABS.lantern_stand, -15, -2, 225);
  C(PREFABS.lantern_stand, -17, 1, 45);
  C(PREFABS.lantern_stand, -15, 1, 315);
  C(PREFABS.tablechair, -16, 1, 180);
  C(PREFABS.snowballpile, -17, -1, 20);
  C(PREFABS.sled, -14, 0, 90);                 // 入口雪橇
  C(PREFABS.gift_01, -15, 0, 30);              // 树下礼物
  C(PREFABS.candycane, -14, -2, 0);            // 糖果拐杖
  C(PREFABS.icicle_01, -18, -2, 0);
  C(PREFABS.icicle_03, -18, 0, 0);
  C(PREFABS.lantern_big, -17, -3, 0);          // 南缘大红灯笼
  C(PREFABS.fence, -15, -3, 0);
  // 西走廊：两端盒灯（北沿格心线）+ 入口拱门
  C(PREFABS.box_lantern_01, -13, 0, 0);
  C(PREFABS.box_lantern_01, -10, 0, 0);
  items.push(makeItem(PREFABS.archway, -16.2, FLOOR_Y, 0, 0, "Art/Decoration", false));
  // 东庭院（i∈[17,21] j∈[-3,1]，切 4 角点）
  C(PREFABS.tree_decorated, 19, -1, 90);
  C(PREFABS.lantern_stand, 18, -2, 135);
  C(PREFABS.lantern_stand, 20, -2, 225);
  C(PREFABS.lantern_stand, 18, 1, 45);
  C(PREFABS.lantern_stand, 20, 1, 315);
  C(PREFABS.tablechair, 19, 1, 180);
  C(PREFABS.snowballpile, 20, -1, 200);
  C(PREFABS.sled, 17, 0, 270);
  C(PREFABS.gift_02, 21, 0, 150);
  C(PREFABS.candycane, 17, -2, 0);
  C(PREFABS.icicle_01, 21, -2, 0);
  C(PREFABS.icicle_03, 21, 0, 0);
  C(PREFABS.lantern_big, 18, -3, 0);
  C(PREFABS.fence, 20, -3, 0);
  // 东走廊：两端盒灯 + 入口拱门
  C(PREFABS.box_lantern_02, 13, 0, 0);
  C(PREFABS.box_lantern_02, 16, 0, 0);
  items.push(makeItem(PREFABS.archway, 20.4, FLOOR_Y, 0, 0, "Art/Decoration", false));
}

// ---------------------------------------------------------------- 碰撞体
//  invisible BoxCollider（1.2×2×1.2，插件 ApplyCollisionItem 机制），给岛上冰块加碰撞
function makeCollisionItem(x, z, displayName) {
  const it = makeItem({ guid: "", assetPath: "", id: displayName, rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 1 } },
    x, FLOOR_Y, z, 0, "Design/Collision", false);
  it.stubKind = "Collision";
  it.airWall = false;
  return it;
}

// ---------------------------------------------------------------- 装饰
function emitDecor(items) {
  const D = (pf, i, j, yaw, y = FLOOR_Y) =>
    items.push(makeItem(pf, cx(i), y, cz(j), yaw, "Art/Decoration", false));
  // 三个主岛完全留白（无雪堆/树/招牌/冰块），仅四角放中秋盒灯作灯光点缀
  // 左岛四角（切角阶梯的内角格）
  D(PREFABS.box_lantern_01, -8, -5, 45);
  D(PREFABS.box_lantern_01, -3, -5, 135);
  D(PREFABS.box_lantern_01, -8, 4, 315);
  D(PREFABS.box_lantern_01, -3, 4, 225);
  // 右岛四角
  D(PREFABS.box_lantern_01, 6, -3, 45);
  D(PREFABS.box_lantern_01, 11, -3, 135);
  D(PREFABS.box_lantern_01, 6, 2, 315);
  D(PREFABS.box_lantern_01, 11, 2, 225);
  // 北侧布景岛装饰已在 emitBackdrop 中（宝塔等）；此处仅水面漂浮物。
  // 水面上半浮的冰块（仿 3-4，y=-1.15；避开水道，不挡过岛路线）
  const W = (pf, x, z, yaw) =>
    items.push(makeItem(pf, x, -1.15, z, yaw, "Art/Decoration", false));
  W(PREFABS.iceblock_01, -11.4, 2.4, 210);
  W(PREFABS.iceblock_02, 18.0, 3.6, 75);
  W(PREFABS.iceblock_03, 2.4, 4.2, 150);
  W(PREFABS.iceblock_01, 12.6, 5.4, 30);
  W(PREFABS.iceblock_02, -13.2, 4.8, 200);
  W(PREFABS.iceblock_01, -8.4, 7.8, 60);
  W(PREFABS.iceblock_03, 16.8, 6.6, 320);
  W(PREFABS.iceblock_01, -1.2, 15.0, 90);
  W(PREFABS.iceblock_02, 12.0, 15.6, 140);
  W(PREFABS.iceblock_03, 22.8, 9.0, 20);
  W(PREFABS.iceblock_02, -16.2, 9.0, 250);
  W(PREFABS.iceblock_01, -6.6, 16.8, 330);
  W(PREFABS.iceblock_03, 2.4, 16.2, 110);
  W(PREFABS.iceblock_01, 7.2, 18.0, 70);
  W(PREFABS.iceblock_02, 16.2, 16.8, 240);
  W(PREFABS.iceblock_03, -10.8, 14.4, 150);
  W(PREFABS.iceblock_01, 24.0, 13.2, 10);
  W(PREFABS.iceblock_02, -19.2, 7.2, 290);
  W(PREFABS.iceblock_03, 0.6, 18.6, 200);
  W(PREFABS.iceblock_01, 9.6, 13.2, 175);
  // 中秋漂浮灯笼（水面 y=-0.81）
  const L = (x, z, yaw) =>
    items.push(makeItem(PREFABS.lantern, x, -0.81, z, yaw, "Art/Decoration", false));
  L(-6.0, 9.6, 15);
  L(10.2, 7.8, 200);
  L(13.8, 6.6, 120);
  L(-3.0, 8.4, 300);
  L(-9.6, 9.0, 45);
  L(6.6, 5.6, 160);
  L(15.0, 13.8, 230);
  // 漂浮盒灯（水面）
  const FB = (x, z, yaw) =>
    items.push(makeItem(PREFABS.floatingbox, x, -0.81, z, yaw, "Art/Decoration", false));
  FB(-16.8, 4.2, 10);
  FB(20.4, 6.0, 190);
  FB(-5.4, 12.6, 280);
  // 空中漂浮灯笼（高处点缀，y=2.5）
  const AL = (x, z) =>
    items.push(makeItem(PREFABS.air_lantern, x, 2.5, z, 0, "Art/Decoration", false));
  AL(3.0, 13.8);
  AL(-18.6, 3.6);
  AL(23.4, 3.6);
  // v3 水面新装饰（y=-0.81 贴水）：莲花烛 / 水波纹 / 花灯鱼 —— 全部布在北侧水面，
  // 避开中岛移动航线（x 0..11.4, z -9.6..2.4）
  const LOT = (x, z, yaw) =>
    items.push(makeItem(PREFABS.lotus, x, -0.81, z, yaw, "Art/Decoration", false));
  LOT(0.6, 5.4, 15);
  LOT(3.0, 5.4, 300);
  LOT(4.2, 5.4, 160);
  LOT(13.8, 6.0, 80);
  const RIP = (x, z, yaw) =>
    items.push(makeItem(PREFABS.ripple, x, -0.81, z, yaw, "Art/Decoration", false));
  RIP(-4.2, 9.0, 0);
  RIP(12.0, 7.8, 30);
  RIP(8.4, 6.6, 200);
  items.push(makeItem(PREFABS.fishfloat, 19.2, -0.81, 7.2, 260, "Art/Decoration", false));
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
  // 中岛单独收集：全部物品（地砖+崖+贴花）进移动组。walkable:true ——
  // 插件 SyncWalkableToFloors 会把移动组成员的行走碰撞挂成物品子物体，随组动画移动。
  const midItems = [];
  emitIsland(midItems, midIsland, 23, { parent: "Art/MovingIsland" });
  emitIsland(gen, rightIsland, 37);
  emitIsland(gen, pagodaIsland, 53, { parent: "Art/Backdrop", walkable: false });
  emitIsland(gen, neIslet, 67, { parent: "Art/Backdrop", walkable: false });
  emitIsland(gen, nwIslet, 71, { parent: "Art/Backdrop", walkable: false });
  emitBackdrop(gen);
  emitCourtyards(gen);
  emitDecor(gen);
  gen.push(...midItems);
  const water = makeItem(PREFABS.water, 3.0, -0.81, 4.0, 0, "Art/Environment", false);
  water.localScale = { x: 54, y: 38, z: 1 }; // x -24..30, z -15..23
  gen.push(water);
  gen.push(makeItem(PREFABS.snowfall, 2.4, 0, 1.2, 0, "Art/Environment", false));

  const doc = {
    sceneAssetPath: SCENE,
    items: players.concat(gen),
    // 中岛隐形过桥：airFloor（Ground 层碰撞，无可见面），横向嵌入左右岛碰撞体
    // 0.6m（零缝隙，走上去绝不掉水），纵向比岛面外扩 0.9m（学 3-4 浮冰 Ground）。
    floors: [{
      instanceId: "new:testice-airfloor-mid",
      hierarchyPath: "Design/Collision/Col_AirFloor",
      parentPath: "Design/Collision",
      displayName: "Col_AirFloor",
      surfaceKind: "solid",
      meshType: "plane",
      meshFileId: 0,
      airFloor: true,
      localPosition: { x: 2.4, y: FLOOR_Y, z: 0 },
      worldPosition: { x: 2.4, y: FLOOR_Y, z: 0 },
      localRotationY: 0,
      localScale: { x: 1, y: 1, z: 1 },
      widthUnits: 8.4,  // x: -1.8 .. 6.6（中岛 0.0..4.8；左岛东缘 -1.2、右岛西缘 6.0，各嵌入 0.6）
      depthUnits: 6.6,  // z: -3.3 .. 3.3（岛面 ±2.4 外扩 0.9）
      widthCells: 7,
      depthCells: 6,
    }],
    // 中岛移动组：中心(2.4,0) → 停 30s → 下行 7.5s 至 (2.4,-7.2) → 右移 7.5s 至
    // 拼接位 (9.0,-7.2)（北缘 z=-4.8 与右岛南缘齐平拼合）→ 停 30s → 原路 15s 返回中心。
    // 两个独立 move 事件 + 组级 loop（周期 90s；不用单事件 pingpong —— 镜像会把
    // C 点 30s 停留重复成 60s）。e2 用独立的 waypoint 副本，避免 wait 在两端重复触发。
    moveControls: {
      groups: [{
        id: "mid-island",
        displayName: "MidIsland",
        itemInstanceIds: midItems.map((it) => it.instanceId),
        floorInstanceIds: [],
        objectInstanceIds: [],
        startDelay: 0,
        loop: true,
        loopDelay: 0,
        waypoints: [
          { id: "wpA", x: 2.4, z: 0, wait: 30 },
          { id: "wpB", x: 2.4, z: -7.2, segmentSeconds: 7.5 },
          { id: "wpC", x: 9.0, z: -7.2, segmentSeconds: 7.5 },
          { id: "wpC2", x: 9.0, z: -7.2, wait: 30, segmentSeconds: 7.5 },
          { id: "wpB2", x: 2.4, z: -7.2, segmentSeconds: 7.5 },
          { id: "wpA2", x: 2.4, z: 0 },
        ],
        events: [
          { id: "e1", type: "move", delay: 0, intervalSeconds: 7.5, waypointIds: ["wpA", "wpB", "wpC"] },
          { id: "e2", type: "move", delay: 0, intervalSeconds: 7.5, waypointIds: ["wpC2", "wpB2", "wpA2"] },
        ],
      }],
    },
    cameraInfo: {
      backgroundColor: "#000000",
      fieldOfView: 45,
      position: { x: 2.4, y: 15, z: -15 },
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
        intensity: 0.35,
        range: 10,
        spotAngle: 30,
        enabled: true,
        eulerAngles: { x: 39.16, y: 350.1, z: 0 },
      },
    ],
  };

  const byId = {};
  for (const it of gen) byId[it.displayName.split(" ")[0]] = (byId[it.displayName.split(" ")[0]] || 0) + 1;
  console.log(`生成物品：${gen.length} 个（另含 4 出生点）`);
  for (const [id, n] of Object.entries(byId).sort()) console.log(`  ${id} × ${n}`);

  if (!APPLY) {
    const fs = await import("node:fs");
    fs.writeFileSync("/var/folders/p8/b3ryxj6s1bd4wgsspxzhfnw40000gn/T/opencode/testice-doc.json", JSON.stringify(doc));
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
  console.log(await api("/api/scene/killplane", "POST", { sceneAssetPath: SCENE, cx: 2.4, cz: 4, sx: 54, sz: 40 }));

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

  console.log("\n全部完成。请在 Unity 中 Play 验证（重点：过岛行走、冰块碰撞、BGM/落水）。");
}

main().catch((e) => {
  console.error("失败：", e.message);
  process.exit(1);
});
