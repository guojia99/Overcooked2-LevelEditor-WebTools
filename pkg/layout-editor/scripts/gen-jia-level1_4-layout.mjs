#!/usr/bin/env node
/**
 * gen-jia-level1_4-layout.mjs — 枫醉秋亭 v9
 *
 * v9: city_sushi 墙件 story_2_2 pivot 偏移（四角 + 直边），修复墙角缺口
 * v7: 北墙上菜窗（j=5 正中三连窗）+ 墙外一圈 city_sushi / 林缘小件
 */

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const CELL = 1.2;
const HALF = 0.6;
const FLOOR_Y = 0;

/** 外围装饰 Y（story_2_2 实测；贴地类略抬避免 z-fight/穿地） */
const EXTERIOR_Y = {
  exterior_grass_01: 0.187,
  exterior_path_01: 0,
  exterior_street_bench: 0.021,
  exterior_man_hole_02: 0.01,
  m_city_canopy: 0,
  restaurant_lantern: 0.608,
  restaurant_plant_01: 0.751,
  decoration_table_plant: 0.751,
  decoration_spice_rack: 1.453,
  decoration_ticket_rack: 1.468,
  sushi_sign_01: 0.02,
  p_dlc5_twig_01: 0.01,
  p_dlc5_twig_02: 0.01,
  decoration_bamboo_01: 0,
  decoration_bamboo_02: 0,
};
const decorY = (pfDef) => EXTERIOR_Y[pfDef.id] ?? 0.01;
const SCENE = "Assets/LevelSets/jia_carnival/scenes/s_jia_level1_4.unity";
const LEVEL_INFO = "Assets/LevelSets/jia_carnival/data/jia_level1_4/LevelInfo_jia_level1_4.asset";

const cx = (i) => HALF + CELL * i;
const cz = (j) => HALF + CELL * j;
const key = (i, j) => `${i},${j}`;

const pf = (id, guid, assetPath, extra = {}) => ({
  id, guid, assetPath, rotX: 0, scale: { x: 1, y: 1, z: 1 },
  footprint: { cellsX: 1, cellsZ: 1 }, ...extra,
});

const PREFABS = {
  carpet: pf("floor_carpet_purple", "9f67dfb56cc0a4842b2d6a568e278de1",
    "Assets/common01/prefabs/art/city_sushi/floor_carpet_purple.prefab",
    { rotX: 90, scale: { x: 1.06, y: 1.06, z: 1.06 } }),
  bamboo_a: pf("decoration_bamboo_01", "a71e9b4552cd0a94a86bb425b0f94a35",
    "Assets/common01/prefabs/art/city_sushi/decoration_bamboo_01.prefab"),
  bamboo_b: pf("decoration_bamboo_02", "6432c72fd2c4bfe4196215559098ad86",
    "Assets/common01/prefabs/art/city_sushi/decoration_bamboo_02.prefab"),
  rest_lantern: pf("restaurant_lantern", "30b3a50f2e1c7994ea50508649f69ad7",
    "Assets/common01/prefabs/art/city_sushi/restaurant_lantern.prefab"),
  rest_plant: pf("restaurant_plant_01", "8ed8b9d9ff59a3148962039d17f614cb",
    "Assets/common01/prefabs/art/city_sushi/restaurant_plant_01.prefab"),
  table_guests: pf("table_customers", "2bce6efbb09e7fc34c26ccc555235b92",
    "Assets/common03/prefabs/core/art/legacy/table_customers.prefab",
    { footprint: { cellsX: 1, cellsZ: 3 } }),
  wall_wp: pf("wall_wallpaper_01", "daa9bb76a58af124b9c64a3437295168",
    "Assets/common01/prefabs/art/city_sushi/wall_wallpaper_01.prefab"),
  wall_brick: pf("wall_brick_01a", "342d2709b7155cd428e5ac1aab0a39c4",
    "Assets/common01/prefabs/art/city_sushi/wall_brick_01a.prefab"),
  wall_win: pf("wall_brick_window_03", "375e6a079af0ff34786e8fe1ab7ec7d8",
    "Assets/common01/prefabs/art/city_sushi/wall_brick_window_03.prefab"),
  wall_win2: pf("wall_brick_window_01", "fd5b060d5e41f5f408956918cb0ba834",
    "Assets/common01/prefabs/art/city_sushi/wall_brick_window_01.prefab"),
  wall_short: pf("wall_brick_short_01", "e79fb59a8c19da34ebebd9f871a12ad3",
    "Assets/common01/prefabs/art/city_sushi/wall_brick_short_01.prefab"),
  wall_out: pf("wall_brick_out_01", "f991727a681f84b4ebdbbf19ce440d58",
    "Assets/common01/prefabs/art/city_sushi/wall_brick_out_01.prefab"),
  wall_wp_in: pf("wall_wallpaper_in_01", "ad92d11839b2f8049a77cdb62645ca27",
    "Assets/common01/prefabs/art/city_sushi/wall_wallpaper_in_01.prefab"),
  door_double: pf("city_door_double_01", "d3941656b8aeaae45838ff4e1ab8844a",
    "Assets/common01/prefabs/art/city_sushi/city_door_double_01.prefab",
    { footprint: { cellsX: 2, cellsZ: 1 } }),
  sushi_sign: pf("sushi_sign_01", "55c4db2eaa6107b4984d54bada16ae2d",
    "Assets/common01/prefabs/art/city_sushi/sushi_sign_01.prefab"),
  wall_light: pf("decoration_wall_light 1", "d6f8268d61f82a748aa67e5a6e260edc",
    "Assets/common01/prefabs/art/city_sushi/decoration_wall_light 1.prefab"),
  spice_rack: pf("decoration_spice_rack", "495ca44a967155b41be64386eb2b1191",
    "Assets/common01/prefabs/art/city_sushi/decoration_spice_rack.prefab"),
  table_plant: pf("decoration_table_plant", "dbc5461e3bd144e48b3589cc65c16e60",
    "Assets/common01/prefabs/art/city_sushi/decoration_table_plant.prefab"),
  ticket_rack: pf("decoration_ticket_rack", "4a8627f298e416d44b03b86cd65a10a9",
    "Assets/common01/prefabs/art/city_sushi/decoration_ticket_rack.prefab"),
  grass: pf("exterior_grass_01", "98737bae21178e34296db9071fb499d7",
    "Assets/common01/prefabs/art/city_sushi/exterior_grass_01.prefab"),
  path: pf("exterior_path_01", "9526cc63a99513d4688e7e420903cfb8",
    "Assets/common01/prefabs/art/city_sushi/exterior_path_01.prefab"),
  bench: pf("exterior_street_bench", "c6875c7771c813e43b068aaeac3a0b05",
    "Assets/common01/prefabs/art/city_sushi/exterior_street_bench.prefab"),
  manhole: pf("exterior_man_hole_02", "d3006785ed913b44d8360d2cc1703a1e",
    "Assets/common01/prefabs/art/city_sushi/exterior_man_hole_02.prefab"),
  canopy: pf("m_city_canopy", "9787949914b4da54ea6e6c7914d7dc49",
    "Assets/common01/prefabs/art/city_sushi/m_city_canopy.prefab"),
  twig: pf("p_dlc5_twig_01", "55d99ddf96676b34ca8d070358c33067",
    "Assets/common02/prefabs/art/dlc05_camping/p_dlc5_twig_01.prefab"),
  twig2: pf("p_dlc5_twig_02", "2d81f6720d5877a4a8ba0050adb045ad",
    "Assets/common02/prefabs/art/dlc05_camping/p_dlc5_twig_02.prefab"),
};

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

const FLOOR_I0 = -10;
const FLOOR_I1 = 6;
const FLOOR_J0 = -6;
const FLOOR_J1 = 4;

// 可行走八角岛（学 level1_2/1_3：rect − cornerBevels）
const shopFloorWalk = rectCells(FLOOR_I0, FLOOR_I1, FLOOR_J0, FLOOR_J1,
  cornerBevels(FLOOR_I0, FLOOR_I1, FLOOR_J0, FLOOR_J1));
// 切角 12 格：视觉补地毯，不可行走（填墙内黑三角；level1_2 用冰崖贴边代替）
const shopFloorCornerPad = new Set(
  cornerBevels(FLOOR_I0, FLOOR_I1, FLOOR_J0, FLOOR_J1).map(([i, j]) => key(i, j)),
);
// 厨房留白（仍有地毯，仅不放墙/杂饰；四竹居中）
const centerBlank = rectCells(-2, 2, -3, -1);

// 外墙矩形壳：地板 bbox 外扩 1 格（学 story_2_2：墙在地板外侧独立成行/列，不跟切角锯齿）
const WALL_I0 = -11;
const WALL_I1 = 7;
const WALL_J0 = -7;
const WALL_J1 = 5;

// 南墙正中留门
const SOUTH_DOOR = new Set([key(-1, WALL_J0), key(0, WALL_J0), key(1, WALL_J0)]);
// 北墙正中上菜窗（三连窗格，对北侧用餐岛）
const NORTH_SERVE = new Set([key(-1, WALL_J1), key(0, WALL_J1), key(1, WALL_J1)]);

// 墙外北侧用餐布景岛（NPC 不进厨房）
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
  const id = `new:jia14:${String(seq++).padStart(4, "0")}`;
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

/** city_sushi 墙 pivot 相对格心偏移（s_oc1_story_2_2 Art/Walls 实测） */
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

/** story_2_2 外墙语法：四角角件 + 直边分段（角格不重复放直墙，避免 mesh 重叠） */
function buildWallShell() {
  const walls = [];
  const add = (i, j, pfDef, yaw) => walls.push({ i, j, pf: pfDef, yaw });

  // 四角角件（story_2_2：SW/SE 用 wall_brick_out，NW/NE 用 wall_wallpaper_in）
  add(WALL_I0, WALL_J0, PREFABS.wall_out, 180);   // SW
  add(WALL_I1, WALL_J0, PREFABS.wall_out, 90);    // SE
  add(WALL_I0, WALL_J1, PREFABS.wall_wp_in, 90);  // NW（yaw 90，非 270）
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

  // 南/北直边（跳过角格）
  for (let i = WALL_I0 + 1; i <= WALL_I1 - 1; i++) {
    const south = pickSouth(i);
    if (south) add(i, WALL_J0, south, 180);
    add(i, WALL_J1, pickNorth(i), 0);
  }
  // 东/西直边（跳过角格）
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
  // 双扇门 pivot（story_2_2 南墙 dz≈-0.498）
  items.push(makeItem(PREFABS.door_double, cx(0), FLOOR_Y, cz(WALL_J0) - 0.498, 180, "Art/Structure", false));
  // 上菜窗两侧壁灯（story_2_2 北墙 dz≈-0.03）
  items.push(makeItem(PREFABS.wall_light, cx(-2) - 0.2, FLOOR_Y, cz(WALL_J1) + 0.3 - 0.03, 0, "Art/Structure", false));
  items.push(makeItem(PREFABS.wall_light, cx(2) + 0.2, FLOOR_Y, cz(WALL_J1) + 0.3 - 0.03, 0, "Art/Structure", false));
}

function emitCenterBamboo(items) {
  // 四竹一字排开（留白正中 j=-2）
  const row = [-2, -1, 0, 1];
  const bamboos = [PREFABS.bamboo_a, PREFABS.bamboo_b, PREFABS.bamboo_a, PREFABS.bamboo_b];
  row.forEach((i, n) => {
    items.push(makeItem(bamboos[n], cx(i), FLOOR_Y, cz(-2), 0, "Art/Decoration", false));
  });
}

function emitNorthDiningIslet(items) {
  for (const k of northDiningIslet) {
    const [i, j] = k.split(",").map(Number);
    items.push(makeItem(PREFABS.carpet, cx(i), FLOOR_Y, cz(j), 0, "Art/Backdrop/Dining", false));
  }
  const B = (pf, i, j, yaw, y = decorY(pf)) =>
    items.push(makeItem(pf, cx(i), y, cz(j), yaw, "Art/Backdrop/Dining", false));
  // table_customers 1×3：锚 j=7 yaw0 → 占用 j=7,8,9（在岛 j∈[7,9] 内）
  B(PREFABS.table_guests, -4, 7, 0);
  B(PREFABS.table_guests, 1, 7, 0);
  B(PREFABS.rest_lantern, -5, 8, 90);
  B(PREFABS.rest_lantern, 2, 8, 270);
  B(PREFABS.rest_plant, -5, 9, 45);
  B(PREFABS.rest_plant, 2, 9, 315);
}

/** 厨房外墙一圈布景（不侵入可走主岛 / centerBlank） */
function emitExteriorDecor(items) {
  const X = (sub, pf, i, j, yaw) => {
    const y = decorY(pf);
    items.push(makeItem(pf, cx(i), y, cz(j), yaw, `Art/Backdrop/Exterior/${sub}`, false));
  };

  // 南墙外 j=-8：门前径 + 入口标识
  for (const i of [-2, -1, 0, 1, 2]) X("South", PREFABS.path, i, -8, 0);
  X("South", PREFABS.sushi_sign, -11, -8, 90);
  X("South", PREFABS.bench, -9, -8, 90);
  X("South", PREFABS.grass, 5, -8, 270);
  X("South", PREFABS.manhole, 4, -8, 0);
  X("South", PREFABS.rest_lantern, -8, -8, 45);
  X("South", PREFABS.twig, -7, -8, 30);
  X("South", PREFABS.twig2, 3, -8, 330);

  // 西/东墙外 i=±12
  for (const j of [-5, -2, 1, 4]) {
    X("West", PREFABS.grass, -12, j, 90);
    X("East", PREFABS.grass, 8, j, 270);
  }
  X("West", PREFABS.bamboo_a, -12, -3, 90);
  X("West", PREFABS.bamboo_b, -12, 0, 45);
  X("East", PREFABS.bamboo_a, 8, -3, 270);
  X("East", PREFABS.bamboo_b, 8, 0, 315);
  X("West", PREFABS.spice_rack, -12, -6, 90);
  X("East", PREFABS.ticket_rack, 8, -6, 270);

  // 北墙外 j=6（上菜窗对面，连用餐岛）
  for (let i = -5; i <= 3; i += 2) X("North", PREFABS.path, i, 6, 0);
  X("North", PREFABS.table_plant, -4, 6, 0);
  X("North", PREFABS.table_plant, 2, 6, 180);
  X("North", PREFABS.spice_rack, 0, 6, 0);
  X("North", PREFABS.rest_lantern, -5, 6, 90);
  X("North", PREFABS.rest_lantern, 3, 6, 270);
  X("North", PREFABS.twig, -6, 6, 60);
  X("North", PREFABS.twig2, 4, 6, 300);
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

// 出生点在留白南侧，避开四竹
const SPAWN_POS = {
  "Player 1": { x: cx(-3), z: cz(-3) },
  "Player 2": { x: cx(-2), z: cz(-3) },
  "Player 3": { x: cx(-3), z: cz(-2) },
  "Player 4": { x: cx(-2), z: cz(-2) },
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
  emitCenterBamboo(gen);
  emitNorthDiningIslet(gen);
  emitExteriorDecor(gen);

  const doc = {
    sceneAssetPath: SCENE,
    items: players.concat(gen),
    floors: [],
    cameraInfo: {
      backgroundColor: "#000000", fieldOfView: 45,
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
  console.log(`  可走地板 ${shopFloorWalk.size} 格 | 切角补地毯 ${shopFloorCornerPad.size} 格 | 留白 ${centerBlank.size} 格 | 外墙 ${wallShell.length} 段 | 用餐岛 ${northDiningIslet.size} 格`);
  for (const [id, n] of Object.entries(byId).sort()) console.log(`  ${id} × ${n}`);

  const out = path.join(__dirname, "../../.opencode/jia14-doc.json");
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
  for (const b of ["bundle47", "bundle226", "bundle225", "bundle448"]) deps.add(b);
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
