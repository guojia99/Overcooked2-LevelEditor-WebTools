#!/usr/bin/env node
/**
 * gen-jia-level1_2-layout.mjs — 把 my_test/testice 的冰雪布局同步到
 * jia_carnival / s_jia_level1_2（Level1-2 Snowy Plum Loft 梅雪凇阁），并带两项修复：
 *
 *   1) 三个主岛（左/中/右，含走廊庭院）的边缘贴花向岛心内缩 EDGE_INSET=0.15m
 *      （直边沿法线、凸角沿对角线、凹角不动；scale 保持 0.0125 —— 缩小会重蹈
 *      testice 0.0115 漏缝的覆辙）。布景岛不内缩。
 *   2) 防 Play 闪烁（z-fighting）：地砖 y=0 不动（行走铁律）；所有边缘/转角
 *      贴花抬到地砖之上并做确定性高度分层 —— 直边 y=0.010+eps、转角
 *      y=0.020+eps（eps 按格坐标哈希 0~0.0028，相邻条必然不同高，转角稳定
 *      压直边一头）。1~2cm 抬升在游戏相机下不可见，搭接量不变、不露缝。
 *
 * 与 testice 生成器的差异：
 *   - 中岛铺成**静态**可行走岛（parent Art/MainIsland），不带移动组；
 *     doc 不传 moveControls —— applier 对 null 完全不碰现有移动配置。
 *   - doc 不传 cameraInfo —— 保留 level1_2 自己的相机 (5.4,22,-15.2)；
 *     也不调用 /api/level/audio（保留 LevelInfo 已配的音频）。
 *   - 灯光改用 testice 实证值（冷蓝 #636BFF @ 0.35）：原暖白 #FFEECA 照白雪
 *     贴花惨白刺眼（"贴花太白"，用户降 intensity 到 0.35 仍白亮——是色温问题
 *     不是亮度问题，雪地必须冷光）。
 *   - 出生点重映射为 testice 位（旧 P1/P2 在 x≈17，同步后会落水）。
 *   - --apply 后重发死亡主题 water + KillPlane (2.4, 4, 54×40)。
 *
 * 用法：
 *   node gen-jia-level1_2-layout.mjs --base-url http://<虚拟机IP>:8765            # dry-run
 *   node gen-jia-level1_2-layout.mjs --base-url http://<虚拟机IP>:8765 --apply    # 写回 Unity
 */

const CELL = 1.2;
const HALF = 0.6;
// 行走层高度：必须为 0（FLOOR_Y 铁律，见 testice 生成器注释）。
const FLOOR_Y = 0;
const SCENE = "Assets/LevelSets/jia_carnival/scenes/s_jia_level1_2.unity";

// 边缘贴花内缩量（米）：三个主岛用；约 1/8 格。Play 后如需调整改这一个数重跑。
const EDGE_INSET = 0.15;
// 防闪烁高度分层：直边贴花 / 转角贴花的基础 y（地砖 y=0，转角压直边）。
const CAP_Y_EDGE = 0.010;
const CAP_Y_CORNER = 0.020;
// 确定性微抖动（0~0.0028）：相邻格 i±1 → ±31≡±7 (mod 8)、j±1 → ±17≡±1 (mod 8)，
// 取模后必然不同 → 相邻贴花永不同高；同位置多次运行结果一致。
const capEps = (a, b, salt) => ((((a * 31) ^ (b * 17) ^ salt) % 8) + 8) % 8 * 0.0004;

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
// 凸角被占格 → 岛心方向（内缩方向 = 被占格方位的反方向... 即岛内侧）。
// 角点在格角上，被占格在象限 q，则岛心朝 q 方向——内缩 = 往被占格一侧移。
const QUAD_DIR = { NE: [1, 1], NW: [-1, 1], SW: [-1, -1], SE: [1, -1] };

let seq = 0;
function makeItem(pf, x, y, z, rotY, parentPath, walkable, displaySuffix) {
  const id = `new:jia12:${String(seq++).padStart(4, "0")}`;
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
 * opts: { parent, walkable, ox, oz, edgeInset } —— ox/oz 为格基准（中心 = ox+1.2i）
 * edgeInset（米）：边缘贴花向岛心平移量（直边沿法线、凸角沿对角线朝被占格一侧、
 * 凹角不动）。scale 不变，搭接量不变，不露缝。
 * 防闪烁：地砖 y=0；直边贴花 y=CAP_Y_EDGE+eps(i,j)、转角贴花 y=CAP_Y_CORNER+eps(a,b)，
 * 相邻异高，彻底消除共面 z-fighting。
 */
function emitIsland(items, cells, salt, opts = {}) {
  const parent = opts.parent || "Art/MainIsland";
  const walkable = opts.walkable !== false;
  const ox = opts.ox !== undefined ? opts.ox : HALF;
  const oz = opts.oz !== undefined ? opts.oz : HALF;
  const inset = opts.edgeInset || 0;
  const wx = (i) => ox + CELL * i;
  const wz = (j) => oz + CELL * j;
  const has = (i, j) => cells.has(key(i, j));
  // 1) 地砖
  for (const k of cells) {
    const [i, j] = k.split(",").map(Number);
    items.push(makeItem(PREFABS.snow_tile, wx(i), FLOOR_Y, wz(j), 0, parent, walkable));
  }
  // 2) 直边冰崖（水线，y=0）+ 边缘雪贴花（盖缝，放边线上；内缩 inset，抬高分层）
  for (const k of cells) {
    const [i, j] = k.split(",").map(Number);
    const exposed = EDGE_RULES.filter((r) => !has(i + r.di, j + r.dj));
    for (const r of exposed) {
      const cf = pickVariant(PREFABS.cliff_s1, PREFABS.cliff_s2, i, j, salt + r.yaw);
      items.push(makeItem(cf, wx(i) + r.dx, 0, wz(j) + r.dz, r.yaw, parent, false));
      const cap = pickVariant(PREFABS.cap_s1, PREFABS.cap_s2, i, j, salt + r.yaw + 7);
      // 内缩方向 = 边法线的反方向（朝岛心）：r.dx/dz 指向岛外
      const ix = inset ? -(r.dx / HALF) * inset : 0;
      const iz = inset ? -(r.dz / HALF) * inset : 0;
      items.push(makeItem(cap, wx(i) + r.dx + ix, CAP_Y_EDGE + capEps(i, j, salt + r.yaw),
        wz(j) + r.dz + iz, r.yaw, parent, false));
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
        // 内缩：沿对角线朝被占格（岛心）一侧两轴各移 inset，与相邻直边对齐
        const [qx, qz] = QUAD_DIR[occ[0]];
        items.push(makeItem(PREFABS.cap_270,
          ox + CELL * a - HALF + qx * inset, CAP_Y_CORNER + capEps(a, b, salt),
          oz + CELL * b - HALF + qz * inset, (yaw + 180) % 360, parent, false));
      } else if (occ.length === 3) {
        const missing = ["SW", "SE", "NW", "NE"].find((n) => !occ.includes(n));
        const yaw = CONCAVE_YAW[missing];
        items.push(makeItem(PREFABS.cliff_90, ox + CELL * a - HALF, 0, oz + CELL * b - HALF, yaw, parent, false));
        // 凹角雪地贴花：snow_90 同位，yaw = 凹角崖 yaw + 90（3-4 实测规则）
        // 凹角已陷在岛内、无外悬，v1 不平移，只抬高分层
        items.push(makeItem(PREFABS.cap_90,
          ox + CELL * a - HALF, CAP_Y_CORNER + capEps(a, b, salt + 3),
          oz + CELL * b - HALF, (yaw + 90) % 360, parent, false));
      }
    }
  }
}

// ---------------------------------------------------------------- 远景布景（上半部分聚集，下半留空）
// 东北布景岛 4×4（只切角点 1 格）、西北布景岛 4×4（切 4 角点）
const neIslet = rectCells(14, 17, 8, 11, [[14, 8], [17, 8], [14, 11], [17, 11]]);
const nwIslet = rectCells(-11, -8, 8, 11, [[-11, 8], [-8, 8], [-11, 11], [-8, 11]]);

// 布景岛装饰坐标一律取岛格集内的格心（cx(i), cz(j)）
function emitBackdrop(items) {
  const B = (pf, i, j, yaw, y = FLOOR_Y) =>
    items.push(makeItem(pf, cx(i), y, cz(j), yaw, "Art/Backdrop", false));
  // 中秋岛（宝塔，i∈[0,4] j∈[6,10] 切 4 角点）：亭内坐桌喝茶客人 ×2 + 企鹅 +
  // 亭周灯柱/盒灯 + eskimo 岛民 / 中式侍者 / 龙雕像
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

// ---------------------------------------------------------------- 碰撞体（备用机制，本场景暂不使用）
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
  // 水面新装饰（y=-0.81 贴水）：莲花烛 / 水波纹 / 花灯鱼 —— 全部布在北侧水面
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
// 注意：level1_2 旧出生点 P1/P2 在 x≈17（同步几何后在右岛东侧水里），必须重映射。
const SPAWN_POS = {
  "Player 1": { x: cx(-7), z: cz(-2) },
  "Player 2": { x: cx(-6), z: cz(0) },
  "Player 3": { x: cx(8), z: cz(-2) },
  "Player 4": { x: cx(9), z: cz(0) },
};

async function main() {
  // 1) 现有布局：保留 4 个出生点（改坐标），其余内容全量重建（差量删除旧生成物：
  //    旧 2 大冰面 + 2 雪面 + 水/落雪被新内容替换；旧 4 个 Col_Floor 由
  //    SyncWalkableToFloors 清除并从 walkable 地砖自动重建）
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
  //    三个主岛（含走廊庭院）边缘贴花内缩 EDGE_INSET；布景岛不内缩。
  //    中岛静态（parent Art/MainIsland），不进移动组。
  const gen = [];
  emitIsland(gen, leftIsland, 11, { edgeInset: EDGE_INSET });
  emitIsland(gen, midIsland, 23, { edgeInset: EDGE_INSET });
  emitIsland(gen, rightIsland, 37, { edgeInset: EDGE_INSET });
  emitIsland(gen, pagodaIsland, 53, { parent: "Art/Backdrop", walkable: false });
  emitIsland(gen, neIslet, 67, { parent: "Art/Backdrop", walkable: false });
  emitIsland(gen, nwIslet, 71, { parent: "Art/Backdrop", walkable: false });
  emitBackdrop(gen);
  emitCourtyards(gen);
  emitDecor(gen);
  const water = makeItem(PREFABS.water, 3.0, -0.81, 4.0, 0, "Art/Environment", false);
  water.localScale = { x: 54, y: 38, z: 1 }; // x -24..30, z -15..23
  gen.push(water);
  gen.push(makeItem(PREFABS.snowfall, 2.4, 0, 1.2, 0, "Art/Environment", false));

  const doc = {
    sceneAssetPath: SCENE,
    items: players.concat(gen),
    // 中岛两侧水道的隐形过桥：airFloor（Ground 层碰撞，无可见面），横向嵌入左右岛
    // 碰撞体 0.6m（零缝隙，走上去绝不掉水），纵向比岛面外扩 0.9m（学 3-4 浮冰）。
    floors: [{
      instanceId: "new:jia12-airfloor-mid",
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
    // 不传 moveControls / cameraInfo —— applier 对 null 完全不碰：
    // 保留 level1_2 现有移动配置与相机 (5.4,22,-15.2) pitch60。
    // 灯光：换 testice 实证冷蓝 #636BFF @ 0.35 —— 原暖白 #FFEECA 照白雪贴花
    // 惨白刺眼（色温问题，非亮度问题；用户降 intensity 无效）。
    lights: [
      {
        hierarchyPath: "Art/Lights/day",
        displayName: "day",
        lightType: 1,
        color: "#636BFF", // 3-4 / testice 同款冷蓝平行光
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
    fs.writeFileSync("/var/folders/p8/b3ryxj6s1bd4wgsspxzhfnw40000gn/T/opencode/jia12-doc.json", JSON.stringify(doc));
    console.log("\n（未加 --apply，不写回。文档大小：", JSON.stringify(doc).length, "字节）");
    return;
  }

    // 3) 写回布局（全量 + 重建行走碰撞）。
    // 注意：本脚本不传 moveControls，不会重烘焙移动组。若关卡含移动组/空气地板过桥，
    // 写回后请在 layout-editor 网页对移动组再点一次「写回」，以清理 Col_AirFloor 副本
    // 并烘焙 AirFloor/Ground 岛式层级（对齐 oc1_story 3-4）。
  console.log("\n写回布局…");
  const r1 = await api(`/api/scene/layout?snap=0.01&syncWalkable=1`, "POST", doc);
  console.log("布局写回：", typeof r1 === "string" ? r1 : JSON.stringify(r1));

  // 4) 落水死亡特效（水背景）
  console.log("设置死亡主题为 water…");
  console.log(await api("/api/scene/death", "POST", { sceneAssetPath: SCENE, theme: "water" }));

  // 5) KillPlane 覆盖整个布局范围（落水重生）
  console.log("设置 KillPlane 范围…");
  console.log(await api("/api/scene/killplane", "POST", { sceneAssetPath: SCENE, cx: 2.4, cz: 4, sx: 54, sz: 40 }));

  // 注意：不调用 /api/level/audio —— level1_2 的 LevelInfo 已配好 BGM 与 30 个音频目录。

  console.log("\n全部完成。请在 Unity 中 Play 验证（重点：过岛行走、边缘贴花内缩效果、拼接缝闪烁是否消失）。");
}

main().catch((e) => {
  console.error("失败：", e.message);
  process.exit(1);
});
