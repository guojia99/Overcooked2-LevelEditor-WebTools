#!/usr/bin/env node
/**
 * gen-jia-level1_5-layout.mjs — 桃灼春坞（Blushing Peach Hollow）v1
 *
 * 场景第 6 关 · s_jia_level_1_5 · jia_level_1_5（挂在 jia_carnival 关卡集下）
 *
 * 设计（承 level1_4 的墙壳方法论，主题换春天桃花 / 中国风 / 中秋）：
 * - 主体墙参考官方 4-3（s_oc1_story_4_3）：4-3 厨房外壳用的正是 city_sushi 砖墙家族
 *   （wall_brick_01a / wall_wallpaper_01 / wall_brick_window_01），与 level1_4 同源 →
 *   直接沿用 level1_4 已标定 pivot 的砖墙壳（南门 = 入口，北墙三连窗 = 上菜窗）。
 * - 另在北/东/西外圈补一圈 4-3 招牌的高石墙 city_wall_straight_05 作背景（walkable:false，
 *   不放相机侧南墙以免挡视线），强化「坞」的围合感。
 * - 地板换成 4-3 同款喜庆红毯 Red Carpet Entrance。
 * - 装饰全部换成 dlc13 中秋 / 中国风 + 春天桃花：桃花树、绣球、竹、龙雕、香炉、拱门、
 *   石灯 / 盒灯 / 大红灯笼、孔明灯群、蝴蝶、坐着用餐的客人桌（NPC 在墙外吃饭）。
 */

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const CELL = 1.2;
const HALF = 0.6;
const FLOOR_Y = 0;

const SCENE = "Assets/LevelSets/jia_carnival/scenes/s_jia_level_1_5.unity";
const LEVEL_INFO = "Assets/LevelSets/jia_carnival/data/jia_level_1_5/LevelInfo_jia_level_1_5.asset";

const cx = (i) => HALF + CELL * i;
const cz = (j) => HALF + CELL * j;
const key = (i, j) => `${i},${j}`;

const pf = (id, guid, assetPath, extra = {}) => ({
  id, guid, assetPath, rotX: 0, scale: { x: 1, y: 1, z: 1 },
  footprint: { cellsX: 1, cellsZ: 1 }, ...extra,
});

const PREFABS = {
  // ---- 地板：4-3 同款喜庆红毯 ----
  carpet: pf("Red Carpet Entrance", "208fb099fac50a242b03678a23b3244f",
    "Assets/common01/prefabs/art/city_sushi/Red Carpet Entrance.prefab",
    { rotX: 90, scale: { x: 1.06, y: 1.06, z: 1.06 } }),

  // ---- 砖墙壳（city_sushi，4-3 厨房同源，pivot 承 level1_4）----
  wall_wp: pf("wall_wallpaper_01", "daa9bb76a58af124b9c64a3437295168",
    "Assets/common01/prefabs/art/city_sushi/wall_wallpaper_01.prefab"),
  wall_brick: pf("wall_brick_01a", "342d2709b7155cd428e5ac1aab0a39c4",
    "Assets/common01/prefabs/art/city_sushi/wall_brick_01a.prefab"),
  wall_win: pf("wall_brick_window_03", "375e6a079af0ff34786e8fe1ab7ec7d8",
    "Assets/common01/prefabs/art/city_sushi/wall_brick_window_03.prefab"),
  wall_win2: pf("wall_brick_window_01", "fd5b060d5e41f5f408956918cb0ba834",
    "Assets/common01/prefabs/art/city_sushi/wall_brick_window_01.prefab"),
  wall_out: pf("wall_brick_out_01", "f991727a681f84b4ebdbbf19ce440d58",
    "Assets/common01/prefabs/art/city_sushi/wall_brick_out_01.prefab"),
  wall_wp_in: pf("wall_wallpaper_in_01", "ad92d11839b2f8049a77cdb62645ca27",
    "Assets/common01/prefabs/art/city_sushi/wall_wallpaper_in_01.prefab"),
  door_double: pf("city_door_double_01", "d3941656b8aeaae45838ff4e1ab8844a",
    "Assets/common01/prefabs/art/city_sushi/city_door_double_01.prefab",
    { footprint: { cellsX: 2, cellsZ: 1 } }),
  wall_light: pf("decoration_wall_light 1", "d6f8268d61f82a748aa67e5a6e260edc",
    "Assets/common01/prefabs/art/city_sushi/decoration_wall_light 1.prefab"),

  // ---- 分区水渠（春天庭院水景，左中右结构） ----
  water: pf("Water_01", "a3288048c24aaa44181e6970566891bd",
    "Assets/common01/prefabs/art/dlc03_christmas/Water_01.prefab",
    { rotX: 90 }),

  // ---- 春天桃花 ----
  tree_pink: pf("exterior_tree_pink_01", "43aaaad2a82edc24ca7df3af785f44f6",
    "Assets/common01/prefabs/art/city_sushi/exterior_tree_pink_01.prefab",
    { footprint: { cellsX: 3, cellsZ: 3 } }),
  tree_pink_big: pf("p_dlc13_tree_pink_1", "baa3bfa48fe9a9ffd4adff7169bf6756",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_tree_pink_1.prefab",
    { footprint: { cellsX: 12, cellsZ: 12 } }),
  bush_pink_l: pf("p_dlc13_bush_large_pink_1", "f0334f118950f7d522e696f3a45d4c7f",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_bush_large_pink_1.prefab",
    { footprint: { cellsX: 3, cellsZ: 2 } }),
  bush_pink_s: pf("p_dlc13_bush_small_pink_1", "8573b8da464a3b287ef44a1c554dfc1d",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_bush_small_pink_1.prefab",
    { footprint: { cellsX: 2, cellsZ: 1 } }),
  bush_red_s: pf("p_dlc13_bush_small_red_1", "aea20f5a342b968ba8f488ae159c28c5",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_bush_small_red_1.prefab",
    { footprint: { cellsX: 2, cellsZ: 1 } }),
  bush_green_l: pf("p_dlc13_bush_large_green_1", "39cf91d8f72f0c499ba0c21de1146044",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_bush_large_green_1.prefab",
    { footprint: { cellsX: 4, cellsZ: 4 } }),
  grass: pf("p_dlc13_grass_02", "ae981fd0e1f0af11088c8f64d7358c1f",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_grass_02.prefab"),

  // ---- 中国风 / 中秋 ----
  bamboo: pf("p_dlc13_bamboo_1", "68030afac8a38f03058e8cb32e0658fa",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_bamboo_1.prefab"),
  bamboo_frame: pf("p_dlc13_bamboo_frame_01", "5164514d02bbf157b22057c16bc5ba06",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_bamboo_frame_01.prefab",
    { footprint: { cellsX: 2, cellsZ: 1 } }),
  dragon: pf("p_dlc13_dragon_statue_02", "4a7a8a5aab9ab12a5f683e05ef293a73",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_dragon_statue_02.prefab",
    { footprint: { cellsX: 2, cellsZ: 3 } }),
  incense: pf("p_dlc13_incense_pot_1", "e5a8c11a6c0ebc00ac1a9f2b44a73edd",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_incense_pot_1.prefab",
    { footprint: { cellsX: 2, cellsZ: 2 } }),
  arch: pf("p_dlc13_arch_1", "9ee19a5c44d508d30feb6d3af78c436f",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_arch_1.prefab",
    { footprint: { cellsX: 2, cellsZ: 6 } }),
  rock_a: pf("p_dlc13_rock_a_1", "e259e9b8283d958c634ff00aa6983d4c",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_rock_a_1.prefab"),
  rock_b: pf("p_dlc13_rock_b_1", "33e709fc4199e707d48296d0fbd97168",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_rock_b_1.prefab",
    { footprint: { cellsX: 3, cellsZ: 2 } }),

  // ---- 灯 ----
  lantern_stand: pf("p_dlc13_lantern_stand_1", "c331c6f0e4c28aab5da0c3478ad25814",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_lantern_stand_1.prefab"),
  box_lantern: pf("p_dlc13_box_lantern_01", "11f2cfbfcef7810ea00842a69983a98e",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_box_lantern_01.prefab"),
  box_lantern2: pf("p_dlc13_box_lantern_02", "d403956af5d5f406bf6c0ee3cbc8f22b",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_box_lantern_02.prefab"),
  big_lantern: pf("p_dlc13_lantern_1", "512172025459cabccfd154454c63871c",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_lantern_1.prefab"),
  flying_lanterns: pf("p_dlc13_flyinglanternsgroup_1", "8874b07991c4976ed88312d63df3aed2",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_flyinglanternsgroup_1.prefab",
    { footprint: { cellsX: 21, cellsZ: 9 } }),
  butterfly: pf("p_dlc13_hover_butterfly_1", "40782a9b1333833f5aeb8421cf5fbcd5",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_hover_butterfly_1.prefab"),

  // ---- 用餐客人（NPC 在墙外吃饭，自带坐姿客人） ----
  table_guests: pf("table_customers", "2bce6efbb09e7fc34c26ccc555235b92",
    "Assets/common03/prefabs/core/art/legacy/table_customers.prefab",
    { footprint: { cellsX: 1, cellsZ: 3 } }),
  table_guests2: pf("table_customers_2", "74b63cddabe72798276f6f3d078915b8",
    "Assets/common03/prefabs/core/art/legacy/table_customers_2.prefab",
    { footprint: { cellsX: 1, cellsZ: 3 } }),
  npc_table13: pf("p_dlc13_npctablechair_1", "a59adb9b610c322ad83bce27be00b07c",
    "Assets/common03/prefabs/dlc13/art/dlc13/p_dlc13_npctablechair_1.prefab",
    { footprint: { cellsX: 1, cellsZ: 3 } }),

  // ---- 站立 NPC（亚洲居民 / 侍者） ----
  npc_waiter: pf("NPC_Asian_Waiter_01", "2bf53d6647a60464bb4f33df56650fc4",
    "Assets/common01/prefabs/art/npc/NPC_Asian_Waiter_01.prefab"),
  npc_blue: pf("NPC_Asian_Blue_01", "89c693e0d16578642bb8a9908eb3649f",
    "Assets/common01/prefabs/art/npc/NPC_Asian_Blue_01.prefab"),
  npc_green: pf("NPC_Asian_Green_01", "e09b087817394b44cad01a661bb7115f",
    "Assets/common01/prefabs/art/npc/NPC_Asian_Green_01.prefab"),
  npc_orange: pf("NPC_Asian_Orange_01", "3d67bc4cccd773f40bf88cda3604f2aa",
    "Assets/common01/prefabs/art/npc/NPC_Asian_Orange_01.prefab"),
  npc_yellow: pf("NPC_Asian_Yellow_01", "8fa24eaea3135e141a99dc1d86259657",
    "Assets/common01/prefabs/art/npc/NPC_Asian_Yellow_01.prefab"),
};

/** 抬升类装饰（空中/悬浮）；其余贴地 y=0 */
const DECOR_Y = {
  p_dlc13_flyinglanternsgroup_1: 3.2,
  p_dlc13_hover_butterfly_1: 1.4,
  "decoration_wall_light 1": FLOOR_Y,
};
const decorY = (pfDef) => DECOR_Y[pfDef.id] ?? FLOOR_Y;

function rectCells(i0, i1, j0, j1, remove = []) {
  const s = new Set();
  for (let i = i0; i <= i1; i++) for (let j = j0; j <= j1; j++) s.add(key(i, j));
  for (const [i, j] of remove) s.delete(key(i, j));
  return s;
}

function cornerBevels(i0, i1, j0, j1) {
  return [
    [i0, j0], [i0 + 1, j0], [i0, j0 + 1],
    [i1, j0], [i1 - 1, j0], [i1, j0 + 1],
    [i0, j1], [i0 + 1, j1], [i0, j1 - 1],
    [i1, j1], [i1 - 1, j1], [i1, j1 - 1],
  ];
}

// ===== 几何（墙壳/floor bbox 承 level1_4，pivot 已标定，勿动尺寸）=====
const FLOOR_I0 = -10;
const FLOOR_I1 = 6;
const FLOOR_J0 = -6;
const FLOOR_J1 = 4;

// 厨房分左中右三区：i=-5 / i=1 两道纵向水渠隔开（非行走），
// 仅在南 j∈[-6,-5]、北 j∈[3,4] 留通道把三区连起来（可绕行，不掉水）。
const CHANNEL_I = [-5, 1];
const CHANNEL_J0 = -4;
const CHANNEL_J1 = 2;
const channelCells = new Set();
for (const ci of CHANNEL_I) for (let j = CHANNEL_J0; j <= CHANNEL_J1; j++) channelCells.add(key(ci, j));

const outerBevels = cornerBevels(FLOOR_I0, FLOOR_I1, FLOOR_J0, FLOOR_J1);
const shopFloorWalk = rectCells(FLOOR_I0, FLOOR_I1, FLOOR_J0, FLOOR_J1, outerBevels);
for (const k of channelCells) shopFloorWalk.delete(k); // 挖掉水渠
const shopFloorCornerPad = new Set(outerBevels.map(([i, j]) => key(i, j)));

// 三区格集（仅用于打印/校验）
const leftCells = [...shopFloorWalk].filter((k) => Number(k.split(",")[0]) <= -6);
const centerCells = [...shopFloorWalk].filter((k) => { const i = Number(k.split(",")[0]); return i >= -4 && i <= 0; });
const rightCells = [...shopFloorWalk].filter((k) => Number(k.split(",")[0]) >= 2);

const WALL_I0 = -11;
const WALL_I1 = 7;
const WALL_J0 = -7;
const WALL_J1 = 5;

// 南墙正中留门（入口）
const SOUTH_DOOR = new Set([key(-1, WALL_J0), key(0, WALL_J0), key(1, WALL_J0)]);
// 北墙正中三连窗（上菜窗，对北侧用餐岛）
const NORTH_SERVE = new Set([key(-1, WALL_J1), key(0, WALL_J1), key(1, WALL_J1)]);

// 墙外北侧用餐布景岛（NPC 在外面吃饭）
const northDiningIslet = rectCells(-5, 3, 7, 9, cornerBevels(-5, 3, 7, 9));

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
  const id = `new:jia15:${String(seq++).padStart(4, "0")}`;
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

/** city_sushi 墙 pivot 相对格心偏移（s_oc1_story_2_2 Art/Walls 实测，承 level1_4） */
function wallPivotOffset(pfDef, yaw, i, j) {
  const y = ((yaw % 360) + 360) % 360;
  const id = pfDef.id;
  if (id === "wall_brick_out_01") {
    if (y === 180) return { dx: -0.32, dz: -0.378 };
    if (y === 90) return { dx: 0.316, dz: -0.378 };
  }
  if (id === "wall_wallpaper_in_01") {
    if (y === 90) return { dx: -0.17, dz: 0.153 };
    if (y === 180) return { dx: 0.169, dz: 0.15 };
  }
  if (/wall_brick_window/.test(id)) {
    if (y === 0) return { dx: 0, dz: 0.3 };
    if (y === 90) return { dx: 0.316, dz: 0 };
    if (y === 180) return { dx: 0, dz: -0.378 };
  }
  if (y === 180) return { dx: -0.6, dz: -0.378 };
  if (y === 0) return { dx: -0.6, dz: 0.3 };
  if (y === 90) {
    const dz = j === WALL_J1 - 1 ? -0.6 : ((j - WALL_J0) % 2 === 1 ? -0.6 : 0.6);
    return { dx: 0.316, dz };
  }
  if (y === 270) {
    const dz = j >= WALL_J1 - 2 ? 0.6 : -0.6;
    return { dx: -0.32, dz };
  }
  return { dx: 0, dz: 0 };
}

function wallWorldPos(i, j, pfDef, yaw) {
  const { dx, dz } = wallPivotOffset(pfDef, yaw, i, j);
  return { x: cx(i) + dx, y: FLOOR_Y, z: cz(j) + dz };
}

function buildWallShell() {
  const walls = [];
  const add = (i, j, pfDef, yaw) => walls.push({ i, j, pf: pfDef, yaw });

  add(WALL_I0, WALL_J0, PREFABS.wall_out, 180);   // SW
  add(WALL_I1, WALL_J0, PREFABS.wall_out, 90);    // SE
  add(WALL_I0, WALL_J1, PREFABS.wall_wp_in, 90);  // NW
  add(WALL_I1, WALL_J1, PREFABS.wall_wp_in, 180); // NE

  const pickNorth = (i) => {
    if (NORTH_SERVE.has(key(i, WALL_J1))) return PREFABS.wall_win; // 上菜窗
    if (i === -2 || i === 2) return PREFABS.wall_win2;
    if (i % 4 === 0) return PREFABS.wall_win;
    return PREFABS.wall_wp;
  };
  const pickSouth = (i) => {
    if (SOUTH_DOOR.has(key(i, WALL_J0))) return null;
    if (i === -2 || i === 2) return PREFABS.wall_win2;
    return PREFABS.wall_brick;
  };
  const pickWest = (j) => (j % 4 === 0 ? PREFABS.wall_win2 : PREFABS.wall_brick);
  const pickEast = (j) => (j % 4 === 2 ? PREFABS.wall_win : PREFABS.wall_brick);

  for (let i = WALL_I0 + 1; i <= WALL_I1 - 1; i++) {
    const south = pickSouth(i);
    if (south) add(i, WALL_J0, south, 180);
    add(i, WALL_J1, pickNorth(i), 0);
  }
  for (let j = WALL_J0 + 1; j <= WALL_J1 - 1; j++) {
    add(WALL_I0, j, pickWest(j), 270);
    add(WALL_I1, j, pickEast(j), 90);
  }
  return walls;
}

const wallShell = buildWallShell();

function emitShopFloor(items) {
  for (const k of shopFloorWalk) {
    const [i, j] = k.split(",").map(Number);
    items.push(makeItem(PREFABS.carpet, cx(i), FLOOR_Y, cz(j), 0, "Art/MainIsland", true));
  }
  for (const k of shopFloorCornerPad) {
    const [i, j] = k.split(",").map(Number);
    items.push(makeItem(PREFABS.carpet, cx(i), FLOOR_Y, cz(j), 0, "Art/MainIsland/Corners", false));
  }
}

function emitWallShell(items) {
  for (const w of wallShell) {
    const p = wallWorldPos(w.i, w.j, w.pf, w.yaw);
    items.push(makeItem(w.pf, p.x, p.y, p.z, w.yaw, "Art/Structure", false));
  }
  // 双扇门 pivot（story_2_2 南墙 dz≈-0.498）= 入口
  items.push(makeItem(PREFABS.door_double, cx(0), FLOOR_Y, cz(WALL_J0) - 0.498, 180, "Art/Structure", false));
  // 上菜窗两侧壁灯
  items.push(makeItem(PREFABS.wall_light, cx(-2) - 0.2, FLOOR_Y, cz(WALL_J1) + 0.3 - 0.03, 0, "Art/Structure", false));
  items.push(makeItem(PREFABS.wall_light, cx(2) + 0.2, FLOOR_Y, cz(WALL_J1) + 0.3 - 0.03, 0, "Art/Structure", false));
}

/** 分区水渠：单块 Water_01 水面（Water_01 被后端当唯一场景水，只能一块），
 * 铺在两道水渠所跨的矩形下方 y=-0.81；中间可走区地毯不透明会把水盖住，
 * 只有 i=-5 / i=1 两列「没铺地板」的水渠格露出水面 → 视觉水渠 + 掉水判定。 */
function emitWaterChannels(items) {
  const x0 = cx(CHANNEL_I[0]) - HALF;               // 左渠外缘
  const x1 = cx(CHANNEL_I[CHANNEL_I.length - 1]) + HALF; // 右渠外缘
  const z0 = cz(CHANNEL_J0) - HALF;
  const z1 = cz(CHANNEL_J1) + HALF;
  const w = { ...PREFABS.water, scale: { x: x1 - x0, y: z1 - z0, z: 1 } };
  items.push(makeItem(w, (x0 + x1) / 2, -0.81, (z0 + z1) / 2, 0, "Art/Water", false));
}

/** 墙外北侧用餐岛：客人在外面吃饭 + 桃花 / 灯 / 蝴蝶 */
function emitNorthDiningIslet(items) {
  for (const k of northDiningIslet) {
    const [i, j] = k.split(",").map(Number);
    items.push(makeItem(PREFABS.carpet, cx(i), FLOOR_Y, cz(j), 0, "Art/Backdrop/Dining", false));
  }
  const B = (pf, i, j, yaw, y = decorY(pf)) =>
    items.push(makeItem(pf, cx(i), y, cz(j), yaw, "Art/Backdrop/Dining", false));
  // 坐着用餐的客人（1×3，锚 j=7 → 占 j7-9，在岛内）
  B(PREFABS.table_guests, -4, 7, 0);
  B(PREFABS.table_guests2, 0, 7, 0);
  B(PREFABS.npc_table13, 2, 7, 0);
  // 侍者 + 排队/闲逛的居民
  B(PREFABS.npc_waiter, -2, 8, 180);
  B(PREFABS.npc_blue, -5, 9, 120);
  B(PREFABS.npc_yellow, 3, 9, 240);
  // 桃花树 + 绣球 + 灯
  B(PREFABS.tree_pink, -5, 8, 20);
  B(PREFABS.tree_pink, 3, 8, 340);
  B(PREFABS.lantern_stand, -5, 7, 45);
  B(PREFABS.lantern_stand, 3, 7, 315);
  B(PREFABS.bush_pink_s, -1, 9, 0);
  B(PREFABS.bush_red_s, 1, 9, 0);
  B(PREFABS.butterfly, -3, 8, 0);
  B(PREFABS.butterfly, 1, 8, 0);
}

/** 厨房外墙一圈春天桃花 / 中国风布景（不侵入可走主岛） */
function emitExteriorDecor(items) {
  const X = (sub, pf, i, j, yaw) =>
    items.push(makeItem(pf, cx(i), decorY(pf), cz(j), yaw, `Art/Backdrop/Exterior/${sub}`, false));

  // 南墙外（入口 j=-8~-10）：龙拱门当门楼（对齐门中轴 x=0.6，纵深朝南延伸）+ 石灯 + 桃花夹道
  X("South", PREFABS.arch, 0, -9, 0);                 // 门楼（2×6 沿 z，正对南门）
  X("South", PREFABS.big_lantern, -2, -8, 0);         // 门两侧大红灯笼
  X("South", PREFABS.big_lantern, 2, -8, 0);
  X("South", PREFABS.tree_pink, -6, -9, 90);          // 桃花朝入口
  X("South", PREFABS.tree_pink, 5, -9, 270);
  X("South", PREFABS.bush_pink_l, -9, -8, 0);
  X("South", PREFABS.bush_pink_l, 6, -8, 0);
  X("South", PREFABS.rock_b, -4, -10, 60);
  X("South", PREFABS.rock_a, 3, -10, 300);
  X("South", PREFABS.grass, -8, -10, 0);
  X("South", PREFABS.grass, 5, -10, 0);

  // 西墙外 i≈-13：桃花 + 竹 + 绣球 + 石景 + 石灯
  X("West", PREFABS.tree_pink, -13, -3, 90);
  X("West", PREFABS.tree_pink, -13, 3, 90);
  X("West", PREFABS.bamboo, -12, -5, 60);
  X("West", PREFABS.bamboo, -12, 1, 60);
  X("West", PREFABS.bush_pink_s, -12, -1, 90);
  X("West", PREFABS.bush_green_l, -13, 6, 90);
  X("West", PREFABS.rock_a, -12, 4, 20);
  X("West", PREFABS.box_lantern, -12, -6, 90);
  X("West", PREFABS.dragon, -13, -6, 90);   // 龙首朝东，望向入口

  // 东墙外 i≈9：镜像
  X("East", PREFABS.tree_pink, 9, -3, 270);
  X("East", PREFABS.tree_pink, 9, 3, 270);
  X("East", PREFABS.bamboo, 8, -5, 300);
  X("East", PREFABS.bamboo, 8, 1, 300);
  X("East", PREFABS.bush_red_s, 8, -1, 270);
  X("East", PREFABS.bush_green_l, 9, 6, 270);
  X("East", PREFABS.rock_a, 8, 4, 340);
  X("East", PREFABS.box_lantern2, 8, -6, 270);
  X("East", PREFABS.dragon, 9, -6, 270);   // 龙首朝西，望向入口

  // 北墙外 j=6（上菜窗对面，连用餐岛）：石灯夹道 + 绣球 + 竹框
  X("North", PREFABS.lantern_stand, -4, 6, 90);
  X("North", PREFABS.lantern_stand, 2, 6, 270);
  X("North", PREFABS.bamboo_frame, 0, 6, 0);
  X("North", PREFABS.bush_pink_s, -2, 6, 0);
  X("North", PREFABS.bush_red_s, 1, 6, 0);

  // 远景大桃花树（北面背景，坐 12×12，压在石墙外）
  X("Far", PREFABS.tree_pink_big, -9, 13, 0);
  X("Far", PREFABS.tree_pink_big, 7, 13, 0);
  // 孔明灯群 + 蝴蝶（空中春意 / 中秋）
  X("Far", PREFABS.flying_lanterns, -1, 12, 0);
  X("Far", PREFABS.butterfly, -6, 10, 0);
  X("Far", PREFABS.butterfly, 5, 10, 0);
}

const args = process.argv.slice(2);
const argVal = (name, dflt) => { const idx = args.indexOf(name); return idx >= 0 ? args[idx + 1] : dflt; };
const BASE = argVal("--base-url", "http://127.0.0.1:8765");
const APPLY = args.includes("--apply");
const OFFLINE = args.includes("--offline");

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

// 出生点在留白中心南侧
const SPAWN_POS = {
  "Player 1": { x: cx(-3), z: cz(-2) },
  "Player 2": { x: cx(-2), z: cz(-2) },
  "Player 3": { x: cx(-3), z: cz(-1) },
  "Player 4": { x: cx(-2), z: cz(-1) },
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
      p.parentPath = "Design/Chefs";
      p.walkable = false;
    }
  }

  const gen = [];
  emitShopFloor(gen);
  emitWallShell(gen);
  emitWaterChannels(gen);
  emitNorthDiningIslet(gen);
  emitExteriorDecor(gen);

  const doc = {
    sceneAssetPath: SCENE,
    items: players.concat(gen),
    floors: [],
    cameraInfo: {
      backgroundColor: "#101018", fieldOfView: 45,
      position: { x: -2.0, y: 22, z: -16.0 }, pitch: 60, yaw: 0, roll: 0,
      nearClip: 0.3, farClip: 1000,
    },
    lights: [{
      hierarchyPath: "Art/Lights/day", displayName: "day", lightType: 1,
      color: "#FFE0C8", intensity: 0.5, range: 10, spotAngle: 30, enabled: true,
      eulerAngles: { x: 39.16, y: 350.1, z: 0 },
    }],
  };

  const byId = {};
  for (const it of gen) { const id = it.displayName.split(" ")[0]; byId[id] = (byId[id] || 0) + 1; }
  console.log(`生成物品：${gen.length} 个（另含 4 出生点）`);
  console.log(`  可走地板 ${shopFloorWalk.size} 格（左 ${leftCells.length} / 中 ${centerCells.length} / 右 ${rightCells.length}）| 水渠 ${channelCells.size} 格 | 切角补毯 ${shopFloorCornerPad.size} 格 | 外墙 ${wallShell.length} 段 | 用餐岛 ${northDiningIslet.size} 格`);
  for (const [id, n] of Object.entries(byId).sort()) console.log(`  ${id} × ${n}`);

  const out = path.join(__dirname, "../../.opencode/jia15-doc.json");
  fs.writeFileSync(out, JSON.stringify(doc));
  console.log("\n文档 →", out);
  if (!APPLY) { console.log("（未加 --apply，不写回 Unity）"); return; }

  console.log("\n写回布局…");
  console.log(await api(`/api/scene/layout?snap=0.01&syncWalkable=1`, "POST", doc));
  console.log("设置 KillPlane…");
  console.log(await api("/api/scene/killplane", "POST", {
    sceneAssetPath: SCENE, cx: -1.8, cz: 0.6, sx: 24, sz: 22,
  }));
  const info = await api(`/api/level?assetPath=${encodeURIComponent(LEVEL_INFO)}`);
  const deps = new Set(info.dependencies || []);
  for (const b of ["bundle47", "bundle449"]) deps.add(b);
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
