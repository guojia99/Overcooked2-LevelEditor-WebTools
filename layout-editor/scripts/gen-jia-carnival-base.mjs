#!/usr/bin/env node
/**
 * gen-jia-carnival-base.mjs — 为 jia_carnival / s_jia_level_base 生成陆地游乐园造景。
 *
 * 场地：整块 DLC08 马戏团地板 mat_dlc08_ground_1-1（v10 换回；铺满 48×36m，
 * surfaceKind=background 纯视觉无碰撞）；中间 20×30（v9，x -5.8..14.2,
 * z -16.8..13.2）用隐形 airFloor 提供行走碰撞，4 出生点原样保留。四周造马戏团布景：
 *   - 北侧 3 幽宽游园大道（road + road_centre 黄线）通往大帐篷主舞台群：
 *     stage_entrance 大拱门 + 火焰表演者、两侧带观众的看台（面向玩家厨房）、
 *     大帐篷顶 + 高空秋千柱、背后 hanging_cloth / tent_wall 幕墙收边；
 *   - 四角马车群：NW 爆米花车+散落爆米花+立牌、NE 糖果车+气球动物、
 *     W 售票车+马戏道具（火圈/壶铃/杠铃/马戏球）、SW 小马车、SE 汽车；
 *   - 东西侧带串灯排（string_lights 沿本地 Z 延伸，fp cellsX=1 实证）+ 城市树 +
 *     哈哈镜 + 长凳 + 杂耍/大力士 NPC；
 *   - 天空：两个热气球（background_balloon_01，29m 高）+ 云带（cloud_clump）。
 *   - v4：厨房围栏彩旗绳换成 dlc08 立柱绳栏（map_rope_fence）；绳栏外北/东/西
 *     三边市民 NPC 围观（面向厨房）；杂耍艺人 1→6（5 个新表演点，配杂耍棒/小道具）；
 *     新增大炮、大力锤+领奖台、号角、气球串绳/气球束、长凳、观众脚下散落物。
 *   - v5：删东半区四角中号草堆（hay_pile_02），小干草加密为 2.4m 网格；
 *     北绳栏居中开 2.4m 上菜缺口（x 3.0..5.4，对齐游园大道中轴，空气墙不动）。
 *   - v6：移除 keep_plank 三边木板圈（北/南/东），地面修饰全由小干草承担。
 *   - v7：SE 汽车南移出南绳（原车身压绳）；小干草改为绳栏内全域确定性随机散布
 *     （52 件，避开玩法件/出生点，最小间距 1.3m），不再是规律网格。
 *   - v8：隐形 airFloor 行走地板 24×14.4 → 30×20（x -10.8..19.2, z -11.8..8.2，
 *     中心不变；绳栏/空气墙/布景全部不动，玩家实际活动范围仍由空气墙决定）；
 *     移除 SW 装饰大炮（dlc08_cannon）。
 *   - v9：airFloor 方向修正 30×20 → 20×30（x -5.8..14.2, z -16.8..13.2）；
 *     Art/Ground 被意外旋转 200° + 平移 (17.7,0,-8.4) 需手动归位
 *     （Hierarchy 选中 Art/Ground → Transform 齿轮 → Reset）。
 *   - v10：视觉大地板弃用 AI 涂鸦图片，换回 DLC08 马戏团地板
 *     mat_dlc08_ground_1-1（48×36m 不变）。
 *
 * 相机 (5.4, 22, -15.2) pitch60 朝北：装饰集中 z 5..14；南缘 z<-10 基本在画面外，
 * 只放轻道具。全部生成物 walkable:false（行走碰撞只来自 img_floor 的 floors 条目，
 * 该条目从现有布局 verbatim 回传）。
 *
 * 地面：road y=-0.09 / path y=0（s_jia_level1_1 实测同值）。
 * 音频：BGM → DLC_08_FairgroundDay_Theme；保留现有 2 个音频目录 + DLC08AudioDirectory。
 *
 * 用法：
 *   node gen-jia-carnival-base.mjs --base-url http://10.211.55.3:8765 [--apply]
 */

const FLOOR_Y = 0;    // 装饰基面（视觉大地板在 y=-0.05，装饰落 y=0）
const SCENE = "Assets/LevelSets/jia_carnival/scenes/s_jia_level_base.unity";

// v9：平行光兜底——现场景里 day 灯已丢失（cur.lights 返回 []），用 11:32 快照的
// 已知好值重建（ApplyLights 对缺失节点会重新创建）；cur.lights 非空时仍 verbatim。
const DEFAULT_DAY_LIGHT = {
  hierarchyPath: "Art/Lights/day",
  displayName: "day",
  lightType: 1,               // directional
  color: "#FFEECA",
  intensity: 0.7,
  range: 10.0,
  spotAngle: 30.0,
  enabled: true,
  eulerAngles: { x: 67.0, y: 233.0, z: 205.0 },
};

// ---------------------------------------------------------------- prefab 表
// (guid 全部来自 layout-editor/web/public/catalog/items.*.json 实测)
const P = (id, guid, assetPath, fpX, fpZ) => ({
  id, guid, assetPath,
  rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: fpX, cellsZ: fpZ },
});
const C3 = "Assets/common03/prefabs/dlc08/art/dlc08_circus";
const C11 = "Assets/common03/prefabs/dlc11/art/dlc11_summer";
const NPC8 = "Assets/common03/prefabs/dlc08/art/npc";
const NPC1 = "Assets/common01/prefabs/art/npc";
const PREFABS = {
  // ---- 北面主舞台群（dlc08_circus）
  tent_roof: P("m_dlc08_tent_roof_01", "18ef500ce1739b56c2ae0ddc34502a33", `${C3}/m_dlc08_tent_roof_01.prefab`, 6, 4),
  tent_wall: P("p_dlc08_tent_wall_01", "f135bcf1216988ffed2816092b66cc60", `${C3}/p_dlc08_tent_wall_01.prefab`, 6, 2),
  hanging_cloth: P("p_dlc08_hanging_cloth_01", "2ae2617635864edacf81393fb3ff9b24", `${C3}/p_dlc08_hanging_cloth_01.prefab`, 13, 3),
  stage_entrance: P("p_dlc08_stage_entrance_01", "aecd84607bcc1ec1bd292643b04c0fa2", `${C3}/p_dlc08_stage_entrance_01.prefab`, 7, 1),
  trapeze_pillar: P("p_dlc08_pillar_trapeze_01", "0901a89c19c3b81f0d3867ff2e5398bd", `${C3}/p_dlc08_pillar_trapeze_01.prefab`, 2, 1),
  bleachers: P("m_dlc08_bleachers_crowd_01", "7d4e962ab03d9eb952be1bd07698a750", `${C3}/m_dlc08_bleachers_crowd_01.prefab`, 5, 4),
  // ---- 马车
  wagon_candy: P("p_dlc08_wagon_candy_01", "c156e7af1406a1b2ed19f7470c2dda4e", `${C3}/p_dlc08_wagon_candy_01.prefab`, 6, 6),
  wagon_popcorn: P("p_dlc08_wagon_popcorn_01", "4467dd0e42c498e03d1c4d4f199eaa01", `${C3}/p_dlc08_wagon_popcorn_01.prefab`, 6, 3),
  wagon_ticket: P("p_dlc08_wagon_ticket_day_01", "e42aef80165e4a2a87c81a6af3c60130", `${C3}/p_dlc08_wagon_ticket_day_01.prefab`, 7, 4),
  wagon_small: P("p_dlc08_wagon_small_01", "0972d620c687f88b5efacfa8581b1c28", `${C3}/p_dlc08_wagon_small_01.prefab`, 6, 4),
  // ---- 串灯（全部沿本地 Z 延伸，fp cellsX=1）/ 绳旗
  lights_01: P("p_dlc08_string_lights_01", "cd14edc6a6a4d78aba0ea754a78b2845", `${C3}/p_dlc08_string_lights_01.prefab`, 1, 7),
  lights_02: P("p_dlc08_string_lights_02", "a77d546de8e3b74ddd8f891cc814388f", `${C3}/p_dlc08_string_lights_02.prefab`, 1, 4),
  lights_04: P("p_dlc08_string_lights_04", "5cfd1f0dad35f06dd6339db35ff0b469", `${C3}/p_dlc08_string_lights_04.prefab`, 1, 7),
  lights_05: P("p_dlc08_string_lights_05", "f6a438c45308284c8bf102a79505a79a", `${C11}/p_dlc08_string_lights_05.prefab`, 1, 9),
  bunting_07: P("p_dlc5_rope_bunting_07", "f09828baf377e6f9619c324f28b1d740", `${C3}/p_dlc5_rope_bunting_07.prefab`, 1, 7),
  bunting_10: P("p_dlc5_rope_bunting_10", "75f67f2a5e9e2ee76ac24e08f06de337", `${C3}/p_dlc5_rope_bunting_10.prefab`, 1, 4),
  // ---- 气球
  balloon_01: P("p_dlc08_balloon_01", "40ab8a11f25fbf06aa93aeaf4117380c", `${C3}/p_dlc08_balloon_01.prefab`, 1, 1),
  balloon_02: P("p_dlc08_balloon_02", "2a5946d285dc4e74aeaff55bb3873131", `${C3}/p_dlc08_balloon_02.prefab`, 1, 1),
  balloon_03: P("p_dlc08_balloon_03", "b638f2eb3012a69e435f61f4f89cfa15", `${C3}/p_dlc08_balloon_03.prefab`, 1, 1),
  balloons_cluster: P("dlc11_balloons_01", "d8df335db94be81725a39bafac0c5510", `${C11}/dlc11_balloons_01.prefab`, 2, 2),
  balloon_dog: P("p_dlc08_balloon_dog_01", "2fb6e1106c4c6df640bd0fc6bbbc2e89", `${C3}/p_dlc08_balloon_dog_01.prefab`, 2, 1),
  balloon_giraffe: P("p_dlc08_balloon_giraffe_01", "462eb8a34c61a3c5ec80a9e9d9005409", `${C3}/p_dlc08_balloon_giraffe_01.prefab`, 2, 1),
  balloon_elephant: P("p_dlc08_balloon_elephant_01", "51c7281230fd218d178d7ba5ada63cde", `${C3}/p_dlc08_balloon_elephant_01.prefab`, 2, 1),
  hot_air_balloon: P("background_balloon_01", "3c1cdd404e14d9b4e881f05bce8cb120", "Assets/common01/prefabs/art/air_balloon/background_balloon_01.prefab", 12, 12),
  cloud: P("p_dlc08_cloud_clump", "12aab1bac3010ad1c913ed3041610db7", `${C3}/p_dlc08_cloud_clump.prefab`, 16, 4),
  // ---- 游乐小件
  bendy_mirror: P("p_dlc08_bendy_mirror_01", "49bea2cc6a001cea2bc3c1107ef43541", `${C3}/p_dlc08_bendy_mirror_01.prefab`, 1, 1),
  sign: P("m_dlc08_standing_sign_01", "b6d00b6e7b925a937ae55d86b5b5d136", `${C3}/m_dlc08_standing_sign_01.prefab`, 1, 1),
  sconce: P("p_dlc08_sconce_01", "d3af6ff2060d37083acbcb708e1fd0e6", `${C3}/p_dlc08_sconce_01.prefab`, 1, 1),
  city_tree: P("p_dlc08_city_tree_01", "e0a5fcab5cc89a4308fc600dc1d2eb7b", `${C3}/p_dlc08_city_tree_01.prefab`, 3, 3),
  ball_01: P("m_dlc08_circus_ball_01", "a67eee35af7fb55f41edca990a4bc984", `${C3}/m_dlc08_circus_ball_01.prefab`, 1, 1),
  ball_02: P("m_dlc08_circus_ball_02", "6da2813a4e00abbb2a637dbb81626dfe", `${C3}/m_dlc08_circus_ball_02.prefab`, 1, 1),
  ball_03: P("m_dlc08_circus_ball_03", "f6893d798cc3710969eb8e8adcf1ae12", `${C3}/m_dlc08_circus_ball_03.prefab`, 1, 1),
  ball_04: P("m_dlc08_circus_ball_04", "6372948f7647dc5b6f266b274749ce8c", `${C3}/m_dlc08_circus_ball_04.prefab`, 1, 1),
  kettlebell: P("m_dlc08_kettlebell_01", "191f3d8150c4c5f72db637f7601f2aa2", `${C3}/m_dlc08_kettlebell_01.prefab`, 1, 1),
  barbell: P("m_dlc08_barbell_01", "bf86484ebe39232c6162db86ea8b5524", `${C3}/m_dlc08_barbell_01.prefab`, 2, 1),
  hoop: P("p_dlc08_hoop_standing_01", "71bc36789880302e1e94760b4a65ec04", `${C3}/p_dlc08_hoop_standing_01.prefab`, 1, 1),
  drum: P("p_dlc08_drum_01", "48ca02fd7159c7d2a0c09e75e8872bcf", `${C3}/p_dlc08_drum_01.prefab`, 1, 2),
  hay_bale: P("p_dlc08_hay_bale_01", "890d08501c9e113286d6586a3e16d23f", `${C3}/p_dlc08_hay_bale_01.prefab`, 2, 1),
  bench: P("p_dlc08_city_bench_01", "84740bd8f9d138bb32adbb8a618ea62e", `${C3}/p_dlc08_city_bench_01.prefab`, 2, 1),
  popcorn_spill: P("m_dlc08_popcorn_01", "64bbbf64f40b40def978ab0346d768ae", `${C3}/m_dlc08_popcorn_01.prefab`, 1, 1),
  gobstopper: P("m_dlc08_gobstopper_01", "521461df9ce623b1f9bc07dbd0ec0320", `${C3}/m_dlc08_gobstopper_01.prefab`, 1, 1),
  ticketstub: P("m_dlc08_ticketstub_01", "411264842beadaeee94cc3402735e488", `${C3}/m_dlc08_ticketstub_01.prefab`, 1, 1),
  // ---- 地板拼块 / 干草（v3）
  hay_pile_01: P("p_dlc08_hay_pile_01", "eb86a42dff882bceac5cd8fa27f97163", `${C3}/p_dlc08_hay_pile_01.prefab`, 4, 4),
  hay_pile_02: P("p_dlc08_hay_pile_02", "8fd65298f4f4c8cfedd1be3f1a687024", `${C3}/p_dlc08_hay_pile_02.prefab`, 3, 3),
  hay_01: P("p_dlc08_hay_01", "4d76411cf715d84a6f8077376b9f4cb6", `${C3}/p_dlc08_hay_01.prefab`, 1, 1),
  hay_02: P("p_dlc08_hay_02", "5ba5c588599573d6fef3057b5e370459", `${C3}/p_dlc08_hay_02.prefab`, 1, 1),
  hay_03: P("p_dlc08_hay_03", "49d35ff6df3819a5f70e675109ce9781", `${C3}/p_dlc08_hay_03.prefab`, 1, 1),
  keep_plank_14: P("p_dlc08_keep_plank_14", "c6da6960e8ee15c1e6ae09ff2cb08653", `${C3}/p_dlc08_keep_plank_14.prefab`, 1, 4),
  keep_plank_16: P("p_dlc08_keep_plank_16", "07a54f46cc998a8dc19e4799d060aa93", `${C3}/p_dlc08_keep_plank_16.prefab`, 1, 3),
  keep_plank_18: P("p_dlc08_keep_plank_18", "226e93709c90c91d8349372401667d95", `${C3}/p_dlc08_keep_plank_18.prefab`, 1, 4),
  keep_plank_15: P("p_dlc08_keep_plank_15", "16dee961b18885ac1745b208bc8bd2c8", `${C3}/p_dlc08_keep_plank_15.prefab`, 1, 1),
  rope_bunting_12: P("p_dlc5_rope_bunting_12", "0db73291b1a1754a1537069119100c6c", `${C3}/p_dlc5_rope_bunting_12.prefab`, 1, 10),
  rope_bunting_11: P("p_dlc5_rope_bunting_11", "9acab43bbb7d52985bd0a28ff257cd5d", `${C3}/p_dlc5_rope_bunting_11.prefab`, 1, 5),
  accordion: P("p_dlc08_accordion_01", "f1dc534a1c181e5b8cba3908b39ce26e", `${C3}/p_dlc08_accordion_01.prefab`, 2, 1),
  car: P("car", "e5d2ee63bc5bc30d5afd35528ac45736", `${C3}/car.prefab`, 2, 3),
  // ---- 马戏 NPC（common03 副本）
  npc_strongman01: P("dlc08_npc_strongman01", "46ffa5a762206ca1833c6da7a4bc18fe", `${NPC8}/dlc08_npc_strongman01.prefab`, 1, 1),
  npc_strongman02: P("dlc08_npc_strongman02", "44bf08171c368f7d14f8947c8f11710c", `${NPC8}/dlc08_npc_strongman02.prefab`, 1, 1),
  npc_juggler: P("dlc08_npc_juggler01", "77b8c6f6f79e66c4444b181877a61367", `${NPC8}/dlc08_npc_juggler01.prefab`, 1, 1),
  npc_firebreather01: P("dlc08_npc_firebreather01", "f9cf2f2d2f097a04166775371048d49e", `${NPC8}/dlc08_npc_firebreather01.prefab`, 1, 1),
  npc_firebreather02: P("dlc08_npc_firebreather02", "1f0a8f6f4ab7a4d0f0f019339a2664ee", `${NPC8}/dlc08_npc_firebreather02.prefab`, 1, 1),
  // ---- 立柱绳栏（v4：替换厨房围栏的彩旗绳；bunting_11/12 条目保留在表中，
  //      好让旧生成物被差量删除）
  rope_fence_01: P("p_dlc08_map_rope_fence_01", "4d55c6733db7bba23bf9c7efade1e295", `${C3}/p_dlc08_map_rope_fence_01.prefab`, 2, 1),
  rope_fence_02: P("p_dlc08_map_rope_fence_02", "fb18709be8065de910bc1880c86b827b", `${C3}/p_dlc08_map_rope_fence_02.prefab`, 1, 1),
  rope_fence_03: P("p_dlc08_map_rope_fence_03", "263f9f588cc9860e5d3913191062bf8a", `${C3}/p_dlc08_map_rope_fence_03.prefab`, 1, 1),
  // ---- 杂耍/游乐元素（v4）
  juggling_pin: P("p_dlc08_juggling_pin_01", "d947eee7d8aac73d6584c10fe5657214", `${C3}/p_dlc08_juggling_pin_01.prefab`, 1, 1),
  cannon: P("dlc08_cannon", "0fb660155afa5f221a84201b839260b4", "Assets/common03/prefabs/dlc08/mechanisms/dlc08_cannon.prefab", 2, 2), // v8：装饰大炮已移除；条目保留好让旧生成物被差量删除
  mallet: P("p_dlc08_mallet_01", "8b0224cc4274e75b63584737eda19cda", `${C3}/p_dlc08_mallet_01.prefab`, 1, 1),
  podium: P("p_dlc08_podium_01", "3eae5c1c078b2a3ccf53728ecc29df73", `${C3}/p_dlc08_podium_01.prefab`, 2, 2),
  horn: P("p_dlc08_horn_01", "76222e9c96d460d3614cb1c615d20ee2", `${C3}/p_dlc08_horn_01.prefab`, 1, 1),
  balloon_string: P("p_dlc08_balloon_string_01", "085f4dbb4e5007ff2775cd42e0c1a494", `${C3}/p_dlc08_balloon_string_01.prefab`, 1, 1),
  balloons_01: P("p_dlc08_balloons_01", "547bfdfef97c5b75b8e29f6bf197bce0", `${C3}/p_dlc08_balloons_01.prefab`, 2, 2),
  popcorn_02: P("m_dlc08_popcorn_02", "b98b772b60a3b56423278797bc9bc50c", `${C3}/m_dlc08_popcorn_02.prefab`, 1, 1),
  gobstopper_02: P("m_dlc08_gobstopper_02", "a0bbc7d5142663fa43476063a446466c", `${C3}/m_dlc08_gobstopper_02.prefab`, 1, 1),
  gobstopper_03: P("m_dlc08_gobstopper_03", "a102a08004a5bc78a153f46726ef2fcc", `${C3}/m_dlc08_gobstopper_03.prefab`, 1, 1),
  // ---- 市民观众 NPC（common01，v4；绳栏外围观厨房）
  npc_mike_civ: P("NPC_Mike_Civ_01", "89d4cdd4b5ff4a04ab0a45c0c71b0edc", `${NPC1}/NPC_Mike_Civ_01.prefab`, 1, 1),
  npc_dora_civ: P("NPC_Dora_Civ_01", "09d9d1e938ab93e4580d1736cc19f04c", `${NPC1}/NPC_Dora_Civ_01.prefab`, 1, 1),
  npc_dorablonde_civ: P("NPC_DoraBlonde_Civ_01", "60fc1f6cafe280e46b1ee1ce95f1590e", `${NPC1}/NPC_DoraBlonde_Civ_01.prefab`, 1, 1),
  npc_ginger_civ: P("NPC_Ginger_Civ_01", "582d5ab7a1027ea49a3c6dda9dcd451d", `${NPC1}/NPC_Ginger_Civ_01.prefab`, 1, 1),
  npc_specs_civ: P("NPC_Specs_Civ_01", "5b5e1487cd9ce934bb920ee0373bcf3a", `${NPC1}/NPC_Specs_Civ_01.prefab`, 1, 1),
  npc_beard_civ: P("NPC_Beard_Civ_01", "19d00f21d4425dc439f715c602236b09", `${NPC1}/NPC_Beard_Civ_01.prefab`, 1, 1),
  npc_wizboy_civ: P("NPC_WizBoy_Civ_01", "ce20fcf0ed47a7046be4264772d50691", `${NPC1}/NPC_WizBoy_Civ_01.prefab`, 1, 1),
  npc_asian_civ: P("NPC_Asian_Civ_01", "c117eadf3033cc646896cc374061e3d6", `${NPC1}/NPC_Asian_Civ_01.prefab`, 1, 1),
  npc_hispanic_civ: P("NPC_Hispanic_Civ_01", "651d5494d32eb92429e84a38cd58e802", `${NPC1}/NPC_Hispanic_Civ_01.prefab`, 1, 1),
  npc_mideast_civ: P("NPC_MiddleEatern_Civ_01", "3f4c932b995d4b04dba178ecf80edf3d", `${NPC1}/NPC_MiddleEatern_Civ_01.prefab`, 1, 1),
};

// ---------------------------------------------------------------- 空气墙（stubKind=Collision + airWall）
// ApplyCollisionItem：BoxCollider size 1×1.132×1（localScale 生效——沿边拉长、压薄）。
// 这里围厨房四边：每边一块，长 = 边长+2（盖住角），厚 1、高 1（scale.y=1 → 1.132m 高）。
function makeAirWall(items, x, y, z, len, alongX, name) {
  const it = makeItem({ id: name, guid: "", assetPath: "", rotX: 0, scale: { x: 1, y: 1, z: 1 }, footprint: { cellsX: 1, cellsZ: 1 } },
    x, y, z, 0, "Design/Collision");
  it.stubKind = "Collision";
  it.airWall = true;
  it.displayName = name;
  it.localScale = alongX ? { x: len, y: 1, z: 1 } : { x: 1, y: 1, z: len };
  items.push(it);
}

// ---------------------------------------------------------------- 生成骨架（照抄 testice）
let seq = 0;
function makeItem(pf, x, y, z, rotY, parentPath) {
  const id = `new:jia-carnival:${String(seq++).padStart(4, "0")}`;
  return {
    instanceId: id,
    hierarchyPath: id,
    prefabGuid: pf.guid,
    prefabAssetPath: pf.assetPath,
    parentPath,
    displayName: pf.id,
    localPosition: { x, y, z },
    worldPosition: { x, y, z },
    localRotationX: pf.rotX,
    localRotationY: rotY,
    localRotationZ: 0,
    localScale: { ...pf.scale },
    colliderCenter: { x: 0, y: 0, z: 0 },
    footprint: { ...pf.footprint },
    walkable: false,
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

// ---------------------------------------------------------------- 北面主舞台群
function emitNorthStage(items) {
  const D = (pf, x, z, yaw, y = FLOOR_Y) =>
    items.push(makeItem(pf, x, y, z, yaw, "Art/Backdrop"));
  // 大拱门（8.4m 跨度薄面，开口朝南北）：厨房北缘 → 游园大道入口
  D(PREFABS.stage_entrance, 4.2, 6.3, 0);
  D(PREFABS.sconce, -0.9, 6.3, 180); D(PREFABS.sconce, 10.2, 6.3, 180); // 拱门两侧灯柱
  // 火焰表演者一对迎宾
  D(PREFABS.npc_firebreather01, 2.4, 7.8, 170);
  D(PREFABS.npc_firebreather02, 6.0, 7.8, 190);
  // 带观众的看台 ×2（面向南方玩家厨房；朝向待 Play 校准）
  D(PREFABS.bleachers, -4.8, 9.6, 180);
  D(PREFABS.bleachers, 13.2, 9.6, 180);
  // 大力士 + 举重道具（看台东侧空位）
  D(PREFABS.npc_strongman01, -1.2, 7.5, 200);
  D(PREFABS.npc_strongman02, 8.7, 7.5, 160);
  D(PREFABS.kettlebell, 19.2, 7.2, 0);
  D(PREFABS.barbell, 17.7, 9.6, 90);
  // 大帐篷顶 + 高空秋千柱一对
  D(PREFABS.tent_roof, 4.2, 10.2, 0);
  D(PREFABS.trapeze_pillar, -0.6, 10.8, 0);
  D(PREFABS.trapeze_pillar, 9.0, 10.8, 0);
  // 北侧幕布墙收边（挡住远处虚空）
  D(PREFABS.hanging_cloth, 4.2, 14.0, 0);
  D(PREFABS.tent_wall, -10.2, 13.2, 0);
  D(PREFABS.tent_wall, 18.0, 13.2, 0);
  // 游园大道西端串灯（4.8m 短串，端杆 z 5.7/10.5 避开帐篷墙与看台）
  D(PREFABS.lights_02, -8.4, 8.1, 0);
}

// ---------------------------------------------------------------- 四角马车群
function emitWagons(items) {
  const D = (pf, x, z, yaw, y = FLOOR_Y) =>
    items.push(makeItem(pf, x, y, z, yaw, "Art/Backdrop"));
  // NW 爆米花车 + 散落爆米花（车头东南角）+ 立牌
  D(PREFABS.wagon_popcorn, -13.8, 8.4, 0);
  D(PREFABS.popcorn_spill, -9.3, 6.3, 0);
  D(PREFABS.popcorn_spill, -8.7, 5.7, 210);
  D(PREFABS.sign, -9.9, 6.6, 210);
  // NE 糖果车 + 气球动物 + 气球簇
  D(PREFABS.wagon_candy, 23.4, 8.4, 180);
  D(PREFABS.balloon_giraffe, 22.2, 3.9, 30);
  D(PREFABS.balloon_elephant, 16.5, 5.4, 270);
  D(PREFABS.balloons_cluster, 18.9, 3.9, 0);
  // W 售票车（西广场，票口朝东向环路）
  D(PREFABS.wagon_ticket, -12.0, 1.2, 90);
  D(PREFABS.ticketstub, -9.0, 1.8, 0);
  // SW 小马车 + 手风琴
  D(PREFABS.wagon_small, -14.4, -8.4, 20);
  // SE 汽车（路边停车）+ 干草垛
  // v7：原 (12.6,-9.6) 车身（fp 2×3 ≈ 2.4×3.6m）北缘压进 z=-9.3 南绳，
  // 南移到 (13.8,-12.3)（z 跨 -14.1..-10.5，离绳 ≥1.2m）
  D(PREFABS.car, 13.8, -12.3, 15);
  D(PREFABS.hay_bale, 6.6, -11.4, 40);
  D(PREFABS.ball_02, 9.0, -11.1, 0);
}

// ---------------------------------------------------------------- 东西侧带 + 天空
function emitSides(items) {
  const D = (pf, x, z, yaw, y = FLOOR_Y) =>
    items.push(makeItem(pf, x, y, z, yaw, "Art/Backdrop"));
  // 环路边框串灯：西环 / 东环 / 南环（hug 厨房地板边缘，串灯只有两端杆落地）
  D(PREFABS.lights_01, -8.4, -0.9, 0);
  D(PREFABS.lights_02, 16.8, -0.9, 0);
  D(PREFABS.lights_05, 4.2, -9.9, 90, FLOOR_Y); // 南环沿 E-W 排
  // 西侧带：火圈 + 马戏球 + 气球柱 + 灯柱
  D(PREFABS.hoop, -10.5, -3.9, 90);
  D(PREFABS.ball_04, -9.6, -5.1, 0);
  D(PREFABS.balloon_01, -11.7, 6.0, 0);
  D(PREFABS.balloon_02, -9.0, -8.7, 0);
  D(PREFABS.balloon_03, -9.9, -9.9, 0);
  // 西南绳旗（沿 Z，避开小马车）
  D(PREFABS.bunting_07, -9.3, -10.8, 0, FLOOR_Y);
  // 东侧带：哈哈镜 + 杂耍 + 鼓 + 树 + 马戏球
  D(PREFABS.bendy_mirror, 21.0, -5.4, 250);
  D(PREFABS.npc_juggler, 19.8, -7.5, 210);
  D(PREFABS.drum, 18.9, -2.1, 0);
  D(PREFABS.city_tree, 21.6, 0.6, 20);
  D(PREFABS.ball_01, 20.7, -2.7, 0);
  D(PREFABS.ball_03, 17.4, 3.9, 0);
  D(PREFABS.gobstopper, 18.6, -5.1, 0);
  D(PREFABS.balloon_dog, 17.7, -8.4, 200);
  // 南缘轻装饰（基本在画面外）
  D(PREFABS.bunting_10, -6.0, -10.5, 90, FLOOR_Y);
  // 天空：热气球 ×2 + 云带 ×2（北面上空；与幕布墙/马车高度分离已核）
  items.push(makeItem(PREFABS.hot_air_balloon, -21.0, 6.0, 19.0, 30, "Art/Backdrop"));
  items.push(makeItem(PREFABS.hot_air_balloon, 23.5, 7.0, 15.0, 320, "Art/Backdrop"));
  items.push(makeItem(PREFABS.cloud, 4.2, 10.5, 17.0, 0, "Art/Backdrop"));
  items.push(makeItem(PREFABS.cloud, -9.0, 11.0, 13.5, 180, "Art/Backdrop"));
}

// ---------------------------------------------------------------- 厨房绳索围栏 + 空气墙（v4）
// 厨房地板矩形：x -7.8..16.2, z -9.0..5.4（24×14.4）。用户玩法件全在西半区
// （x -7.2..-1.2），围栏全周 + 空气墙四边，玩家活动范围 = 中间厨房不变。
// v4：彩旗绳换成 dlc08 立柱绳栏（map_rope_fence，纯绳+立柱，更像围场隔离绳）。
function emitFence(items) {
  const D = (pf, x, z, yaw) =>
    items.push(makeItem(pf, x, FLOOR_Y, z, yaw, "Art/Fence"));
  // 立柱绳栏（rope_fence_01 长轴暂定沿本地 X：yaw0 → 沿世界 X（北/南边），
  // yaw90 → 沿世界 Z（西/东边）。2.4m/段；朝向与分段手感 Play 校准）：
  for (let k = 0; k <= 10; k++) {
    const x = -7.8 + 2.4 * k;
    // 北段居中留上菜缺口（拆 x=4.2 一段 → 缺口 x 3.0..5.4，对齐游园大道中轴；
    // 空气墙 N 不动，玩家不能走出厨房，仅递菜不被绳挡）
    if (Math.abs(x - 4.2) > 0.01) D(PREFABS.rope_fence_01, x, 5.7, 0);
    D(PREFABS.rope_fence_01, x, -9.3, 0);   // 南段
  }
  for (let k = 0; k <= 5; k++) {
    const z = -8.1 + 2.4 * k;
    D(PREFABS.rope_fence_01, -9.0, z, 90);  // 西段（盖 z -9.3..5.1）
    D(PREFABS.rope_fence_01, 17.2, z, 90);  // 东段
  }
  // 四角 1×1 短件收角
  D(PREFABS.rope_fence_02, -9.0, 5.7, 0);
  D(PREFABS.rope_fence_03, 17.2, 5.7, 0);
  D(PREFABS.rope_fence_03, -9.0, -9.3, 0);
  D(PREFABS.rope_fence_02, 17.2, -9.3, 0);
  // 空气墙四边（比绳圈再外扩 0.6，保证绳后过不去；len = 边长 + 2 盖角）
  makeAirWall(items, 4.2, 0.5, 6.3, 27.6, true, "AirWall_N");
  makeAirWall(items, 4.2, 0.5, -9.9, 27.6, true, "AirWall_S");
  makeAirWall(items, -9.0, 0.5, -1.8, 17.0, false, "AirWall_W");
  makeAirWall(items, 17.4, 0.5, -1.8, 17.0, false, "AirWall_E");
}

// ---------------------------------------------------------------- 围观市民 NPC（v4）
// 绳栏外 ~1.2m，北+东+西三边（南缘基本在相机画面外），全部面向厨房中心 (4.2,-1.8)。
// 站位避开已有道具：北排让开大拱门（x 0..8.4）与灯柱；东列让开鼓/气球簇；
// 西列中段 z -3..5.4 被售票车占，只放南头两个。
const CIVS = [
  PREFABS.npc_mike_civ, PREFABS.npc_dora_civ, PREFABS.npc_dorablonde_civ,
  PREFABS.npc_ginger_civ, PREFABS.npc_specs_civ, PREFABS.npc_beard_civ,
  PREFABS.npc_wizboy_civ, PREFABS.npc_asian_civ, PREFABS.npc_hispanic_civ,
  PREFABS.npc_mideast_civ,
];
function emitSpectators(items) {
  const CX = 4.2, CZ = -1.8; // 厨房中心
  const yawTo = (x, z) => Math.round(Math.atan2(CX - x, CZ - z) * 180 / Math.PI);
  const spots = [
    // 北排（z=6.9）
    [-7.2, 6.9], [-6.0, 6.9], [-4.8, 6.9], [-3.6, 6.9],
    [11.4, 6.9], [12.6, 6.9], [13.8, 6.9], [15.0, 6.9],
    // 东列（x=18.6）
    [18.6, -6.9], [18.6, -3.6], [18.6, 0.3], [18.6, 1.8],
    // 西列南头（x=-10.2）
    [-10.2, -6.6], [-10.2, -7.8],
  ];
  spots.forEach(([x, z], i) => {
    items.push(makeItem(CIVS[i % CIVS.length], x, FLOOR_Y, z, yawTo(x, z), "Art/Spectators"));
  });
}

// ---------------------------------------------------------------- 更多杂耍艺人（v4）
// 现有 1 个（东侧 emitSides）→ 再补 5 个表演点，全部在绳栏外；每点配杂耍棒 + 小道具。
function emitJugglers(items) {
  const D = (pf, x, z, yaw) =>
    items.push(makeItem(pf, x, FLOOR_Y, z, yaw, "Art/Jugglers"));
  // NW 角（售票车北端外，面向厨房）
  D(PREFABS.npc_juggler, -10.2, 6.3, 150);
  D(PREFABS.juggling_pin, -10.9, 5.5, 25);
  D(PREFABS.juggling_pin, -9.6, 7.0, 330);
  D(PREFABS.ball_03, -10.8, 7.2, 0);
  // NE 糖果车前（面向南侧观众）
  D(PREFABS.npc_juggler, 19.8, 6.6, 200);
  D(PREFABS.juggling_pin, 18.9, 5.9, 15);
  D(PREFABS.juggling_pin, 20.7, 5.9, 340);
  D(PREFABS.ball_02, 21.3, 6.9, 0);
  // SW 售票车南（面向东北厨房）
  D(PREFABS.npc_juggler, -11.9, -4.8, 140);
  D(PREFABS.juggling_pin, -12.9, -4.0, 20);
  D(PREFABS.juggling_pin, -12.3, -5.6, 300);
  D(PREFABS.accordion, -13.2, -5.2, 250);
  // 南中（南环灯串以南，画面下缘）
  D(PREFABS.npc_juggler, 2.4, -11.7, 350);
  D(PREFABS.juggling_pin, 1.6, -12.4, 10);
  D(PREFABS.juggling_pin, 3.2, -12.5, 330);
  D(PREFABS.ball_04, 3.6, -11.4, 0);
  // SE 汽车旁
  D(PREFABS.npc_juggler, 10.2, -12.3, 20);
  D(PREFABS.juggling_pin, 9.4, -12.9, 40);
  D(PREFABS.juggling_pin, 11.0, -13.0, 310);
  D(PREFABS.drum, 11.6, -12.1, 0);
}

// ---------------------------------------------------------------- 更多游乐元素（v4）
// 大炮 / 大力锤+领奖台（与大力士呼应的力量游戏角）/ 号角 / 气球串绳 / 气球束 /
// 长凳 / 城市树 / 观众脚下散落爆米花票根拐杖糖。
function emitCarnivalExtras(items) {
  const D = (pf, x, z, yaw) =>
    items.push(makeItem(pf, x, FLOOR_Y, z, yaw, "Art/Extras"));
  D(PREFABS.mallet, 17.4, 7.5, 0);             // 大力锤（东看台北侧力量角）
  D(PREFABS.podium, 19.5, 9.9, 0);             // 领奖台
  D(PREFABS.horn, 18.3, 8.4, 200);             // 号角
  D(PREFABS.balloon_string, -2.7, 8.4, 0);     // 气球串绳 ×2（北路两侧）
  D(PREFABS.balloon_string, 9.0, 8.1, 0);
  D(PREFABS.balloons_01, -0.6, -12.3, 0);      // 气球束 ×2（南带 / 东带南）
  D(PREFABS.balloons_01, 23.4, -6.3, 0);
  D(PREFABS.bench, -16.2, 3.9, 0);             // 长凳 ×2（西售票车广场 / 东带南）
  D(PREFABS.bench, 21.6, -8.7, 0);
  D(PREFABS.city_tree, 25.8, 1.8, 300);        // 城市树 NE 补一棵
  // 观众脚下散落（人群掉落感）
  D(PREFABS.popcorn_02, -4.2, 7.8, 0);
  D(PREFABS.popcorn_02, 13.2, 7.7, 40);
  D(PREFABS.popcorn_02, 19.4, -2.9, 90);
  D(PREFABS.ticketstub, -6.0, 7.6, 15);
  D(PREFABS.ticketstub, 18.2, -6.1, 70);
  D(PREFABS.ticketstub, 2.4, -10.8, 0);
  D(PREFABS.gobstopper_02, 15.9, 7.5, 0);
  D(PREFABS.gobstopper_03, -8.3, 7.4, 0);
}

// ---------------------------------------------------------------- 地板拼块
// v6：按用户反馈移除 keep_plank 三边木板圈（北/南/东）。PREFABS 表中 keep_plank_*
// 条目保留，好让旧生成物被差量删除。厨房内圈地面修饰由 emitHayField 的小干草承担。

// ---------------------------------------------------------------- 干草地面修饰（v7）
// 整个绳栏内圈（厨房全域 x -7.6..16.0, z -8.6..5.0）**随机**散布小干草，
// 不再是规律网格（用户反馈 6×6 太整齐）。hay_01/02/03 全为扁平贴地件，不挡动线。
// 确定性伪随机（同样输入同样结果，幂等）；避开用户玩法件/出生点，件间最小间距 1.3m。
function hashRand(i, salt) {
  let h = (i * 374761393 + salt * 668265263) >>> 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177) >>> 0;
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}
const HAY_AVOID = [
  // 用户玩法件（坐标来自场景导出，r = 避让半径）
  { x: -1.2, z: 2.4, r: 1.1 }, { x: -2.4, z: 2.4, r: 1.1 },   // Dispenser 墙 ×6
  { x: -3.6, z: 2.4, r: 1.1 }, { x: -4.8, z: 2.4, r: 1.1 },
  { x: -6.0, z: 2.4, r: 1.1 }, { x: -7.2, z: 2.4, r: 1.1 },
  { x: -3.6, z: -2.4, r: 1.3 }, { x: -4.8, z: -2.4, r: 1.3 }, // 炸锅+灶台
  { x: -6.0, z: -2.4, r: 1.3 },                               // Counter+cleantraystack
  { x: -5.4, z: -7.2, r: 1.5 }, { x: -3.0, z: -7.2, r: 1.3 }, // tray_sink / ServingStation
  { x: -1.2, z: -7.2, r: 1.3 }, { x: 0.0, z: -7.2, r: 1.2 },  // tray_return / Bin
  { x: 14.4, z: -3.6, r: 1.3 }, { x: 14.4, z: -1.2, r: 1.3 }, // Switch / 饮料机
  { x: 0.0, z: 0.0, r: 1.0 }, { x: 2.4, z: 0.0, r: 1.0 },     // 出生点 ×4
  { x: 4.8, z: 0.0, r: 1.0 }, { x: 7.2, z: 0.0, r: 1.0 },
];
function emitHayField(items) {
  const D = (pf, x, z, yaw) =>
    items.push(makeItem(pf, x, FLOOR_Y, z, yaw, "Art/HayField"));
  const X0 = -7.6, X1 = 16.0, Z0 = -8.6, Z1 = 5.0;
  const TARGET = 52, MIN_GAP = 1.3;
  const placed = [];
  for (let i = 0; placed.length < TARGET && i < 6000; i++) {
    const x = X0 + hashRand(i, 11) * (X1 - X0);
    const z = Z0 + hashRand(i, 37) * (Z1 - Z0);
    if (HAY_AVOID.some((a) => (x - a.x) ** 2 + (z - a.z) ** 2 < a.r * a.r)) continue;
    if (placed.some((p) => (p[0] - x) ** 2 + (p[1] - z) ** 2 < MIN_GAP * MIN_GAP)) continue;
    placed.push([x, z]);
    const pf = [PREFABS.hay_01, PREFABS.hay_02, PREFABS.hay_03][Math.floor(hashRand(i, 71) * 3)];
    D(pf, Math.round(x * 100) / 100, Math.round(z * 100) / 100, Math.floor(hashRand(i, 53) * 180));
  }
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

async function main() {
  // 1) 现有布局：保留 4 出生点 + 用户手摆玩法件 + 开关连线 + 相机/灯光（全部 verbatim）
  const cur = await api(`/api/scene/layout?assetPath=${encodeURIComponent(SCENE)}`);
  const players = (cur.items || []).filter((it) => /Player\.prefab$/.test(it.prefabAssetPath || ""));
  if (players.length !== 4) throw new Error(`出生点数量异常：${players.length}（期望 4）`);
  // 用户手摆的核心玩法件保留：u: 前缀 = 已落场景对象，但要排除我们自己上一轮
  // 写回的装饰（apply 后装饰也拿到 u: id，重复保留会与新装饰叠加/冲突）。
  // 判定：prefab 路径出现在本脚本 PREFABS 表里的 = 我们的装饰，不保留；
  // 空气墙 stub（无 prefab 路径）也由本脚本 emitFence 重新生成，同样不保留
  // （否则每轮 apply 多一份旧空气墙——v4 曾叠成 8 面）；
  // 其余 u: 件（Dispenser/Cooker/Switch/ServingStation 等玩法件）= 用户件，verbatim 保留。
  const ourPaths = new Set(Object.values(PREFABS).map((pf) => pf.assetPath));
  const userItems = (cur.items || []).filter((it) =>
    (it.instanceId || "").startsWith("u:") && !ourPaths.has(it.prefabAssetPath || "")
    && !(it.stubKind === "Collision" && it.airWall));
  console.log(`保留用户件 ${userItems.length} 个（含 4 出生点 + 手摆玩法件）`);
  // 地板全量重建（旧地板被差量删除）：
  //   [0] 视觉大地板（v10）：用回 DLC08 马戏团地板 mat_dlc08_ground_1-1
  //       （弃用 AI 涂鸦图片地板），铺满 48×36m，
  //       surfaceKind=background → SyncWalkableToFloors 跳过（无行走碰撞）。
  //   [1] 中间 20×30 隐形 airFloor（v9：v8 的 30×20 方向反了，应为 x:20 × z:30）：
  //       Col_Floor 行走范围，无可见面。绳栏/空气墙未随之外扩，
  //       玩家活动范围仍由空气墙限定。
  const floors = [
    {
      instanceId: "new:jia-carnival:ground-big",
      hierarchyPath: "Art/Ground/Background",
      parentPath: "Art/Ground",
      displayName: "Background",
      surfaceKind: "background",
      meshType: "plane",
      meshFileId: 10209,
      materialGuid: "ad2d40545b44fadf43d8ff028558f7c8", // mat_dlc08_ground_1-1
      materialAssetPath: "Assets/common03/materials/mat_dlc08_ground_1-1.mat",
      materialName: "mat_dlc08_ground_1-1",
      localPosition: { x: 4.2, y: -0.05, z: 1.5 },
      worldPosition: { x: 4.2, y: -0.05, z: 1.5 },
      localRotationY: 0,
      localScale: { x: 4.8, y: 1, z: 3.6 },  // plane 基准 10×10 → 48×36m
      widthUnits: 48.0, depthUnits: 36.0,     // x -19.8..28.2, z -16.5..19.5
      widthCells: 40, depthCells: 30,
    },
    {
      instanceId: "new:jia-carnival:col-floor-center",
      hierarchyPath: "Design/Collision/Col_Floor",
      parentPath: "Design/Collision",
      displayName: "Col_Floor",
      surfaceKind: "solid",
      meshType: "plane",
      meshFileId: 0,
      airFloor: true,
      localPosition: { x: 4.2, y: FLOOR_Y, z: -1.8 },
      worldPosition: { x: 4.2, y: FLOOR_Y, z: -1.8 },
      localRotationY: 0,
      localScale: { x: 1, y: 1, z: 1 },
      widthUnits: 20.0, depthUnits: 30.0,     // v9：x -5.8..14.2, z -16.8..13.2
      widthCells: 17, depthCells: 25,
    },
  ];

  // 2) 生成
  const gen = [];
  emitFence(gen);
  emitHayField(gen);
  emitNorthStage(gen);
  emitWagons(gen);
  emitSides(gen);
  emitSpectators(gen);      // v4：绳栏外围观市民（北+东+西，面向厨房）
  emitJugglers(gen);        // v4：更多杂耍艺人表演点
  emitCarnivalExtras(gen);  // v4：大炮/大力锤/领奖台/气球/长凳/散落物

  const doc = {
    sceneAssetPath: SCENE,
    items: players.concat(userItems.filter((it) => !players.includes(it)), gen),
    floors,
    // 用户接线（Switch→饮料机）verbatim 保留
    switchLinks: cur.switchLinks || [],
    buttonLinks: cur.buttonLinks || { links: [] },
    buttonEvents: cur.buttonEvents || { links: [] },
    moveControls: cur.moveControls || { groups: [] },                       // AI 贴图地板 verbatim（行走碰撞来源，不动）
    cameraInfo: cur.cameraInfo,   // 现有相机 verbatim
    lights: (cur.lights && cur.lights.length > 0) ? cur.lights : [DEFAULT_DAY_LIGHT], // v9 兜底重建
  };

  const byId = {};
  for (const it of gen) byId[it.displayName] = (byId[it.displayName] || 0) + 1;
  console.log(`生成物品：${gen.length} 个（另含 4 出生点 + 大地板 + 隐形碰撞）`);
  for (const [id, n] of Object.entries(byId).sort()) console.log(`  ${id} × ${n}`);

  if (!APPLY) {
    const fs = await import("node:fs");
    fs.writeFileSync("/var/folders/p8/b3ryxj6s1bd4wgsspxzhfnw40000gn/T/opencode/jia-carnival-doc.json", JSON.stringify(doc));
    console.log("\n（未加 --apply，不写回。文档已写入 jia-carnival-doc.json）");
    return;
  }

  // 3) 写回布局（全量 + 重建行走碰撞 = img_floor 的 Col_Floor）
  console.log("\n写回布局…");
  const r1 = await api(`/api/scene/layout?snap=0.01&syncWalkable=1`, "POST", doc);
  console.log("布局写回：", typeof r1 === "string" ? r1 : JSON.stringify(r1));

  // 4) 游乐园 BGM：DLC_08_FairgroundDay_Theme；保留现有 2 个音频目录 + DLC08AudioDirectory
  console.log("设置游乐园 BGM…");
  console.log(await api("/api/level/audio", "POST", {
    sceneAssetPath: SCENE,
    inLevelMusicGuid: "129785b3a01f3034e963a255012db1c4", // DLC_08_FairgroundDay_Theme
    ambiences: [],
    audioDirectoryGuids: [
      "5dbc266c42f87744fbbd6e0a97b2ad44", // BoatAudioDirectory（现有）
      "318aba72e60547641adf426e9dbf82da", // CutsceneTutorialAudioDirectory（现有）
      "7d84a49d4f2448a498a57c752112b832", // DLC08AudioDirectory
    ],
    onDeathEffectGuid: "",
  }));

  console.log("\n全部完成。请在 Unity 中 Play 验证（重点：看台朝向、马车朝向、串灯走向、帐篷观感）。");
}

main().catch((e) => {
  console.error("失败：", e.message);
  process.exit(1);
});
