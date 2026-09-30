#!/usr/bin/env node
/**
 * Scaffolds names-dictionary.json entries for assets that lack a translation.
 * Reads the generated catalogs, drafts Chinese names from curated tables +
 * token rules, merges into scripts/data/names-dictionary.json (existing
 * entries always win), and prints untranslated ids for manual completion.
 *
 * Usage:
 *   node layout-editor/scripts/scaffold-dictionary.mjs           # add missing ids
 *   node layout-editor/scripts/scaffold-dictionary.mjs --refresh # re-draft poor zh
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PUBLIC_DIR = path.resolve(__dirname, "../web/public");
const DICT_PATH = path.resolve(__dirname, "data/names-dictionary.json");

// ---------------------------------------------------------------------------
// Hand-curated translations (core gameplay + surfaces + specials)
// ---------------------------------------------------------------------------

const CURATED = {
  // counters / workstations
  Barbeque: "烧烤架", Bin: "垃圾箱", Blender: "搅拌机", Campfire: "篝火",
  ChoppingCounter: "切菜台", ConveyorStation: "传送带站", Cooker: "灶台",
  Counter: "工作台", CounterCorner: "转角工作台", Dispenser: "食材箱",
  FryingStation: "煎炸台", GlassReturn: "脏杯台", Mixer: "搅拌台",
  Oven: "烤箱", PlateReturn: "脏盘台", ServingStation: "上菜台",
  Sink: "水槽", SinkGlass: "洗杯槽",
  // utensils
  Bellows: "风箱", BlenderCup: "搅拌杯", CleanGlassStack: "干净杯堆",
  CleanPlateStack: "干净盘堆", FireExtinguisher: "灭火器", Flamethrower: "喷火器",
  FrierBasket: "炸篮", FryPan: "煎锅", Glass: "玻璃杯", GriddlePan: "煎烤盘",
  MixerBowl: "搅拌碗", Plate: "盘子", Pot: "汤锅", Skewer: "烤串签",
  Steamer: "蒸笼", ToastingFork: "烘烤叉", WaterGun: "水枪",
  // mechanisms
  AttachingFoodSpawner: "传送带食材生成器", Backpack: "背包", Burner: "燃烧弹射器",
  ControlTerminal_Marker: "控制终端标记", MultiControlTerminal: "多路控制终端",
  PressureSwitch: "压力开关", Switch: "开关", Teleportal: "传送门",
  // chef spawn
  Player: "厨师出生点",
  // odd game assets
  beachthronecollision: "海滩王座碰撞", rename_me: "未命名",
  // 新增 DLC NPC 装饰（gen-decor-entries 2026-08）
  city_kevin_02: "城市凯文 2", kevin_01: "凯文", kevin_01_dlc5: "凯文（露营）",
  dlc08_npc_firebreather01: "马戏团·吐火者 1", dlc08_npc_firebreather02: "马戏团·吐火者 2",
  dlc08_npc_juggler01: "马戏团·杂耍者 1", dlc08_npc_strongman01: "马戏团·大力士 1",
  dlc08_npc_strongman02: "马戏团·大力士 2",
  npc_buck: "顾客·巴克", npc_business: "顾客·商务男", npc_constructionworker_01: "顾客·建筑工 1",
  npc_diners_01: "顾客·食客 1", npc_eskimo: "顾客·爱斯基摩人", npc_glasses: "顾客·眼镜男",
  npc_lifering: "顾客·救生圈", npc_mel: "顾客·梅尔", npc_mike: "顾客·迈克",
  npc_penguin_1: "企鹅", npc_waiter: "顾客·服务员", npc_waiters_01: "顾客·服务员 1",
  robinflight: "知更鸟（飞）", robinground_01: "知更鸟（地面）",
  // floors / backgrounds
  floor_corner_01: "地板转角", floor_edge_01: "地板边缘", sand_floor_01: "沙地地板",
  walkway_floor_01: "栈道地板", walkway_floor_03_alt_01: "栈道地板（变体）",
  walkway_pillar_01: "栈道立柱", walkway_pillars_01: "栈道立柱群",
  walkway_roof_01: "栈道顶棚", walkway_roof_alt_01: "栈道顶棚（变体）",
  walkway_rope_02: "栈道绳索", background_balloon_01: "背景热气球",
  Sky: "天空背景", alien_floor_tile_01: "外星地砖", alien_gue: "外星黏液",
  PFX_background_wizardshool_01: "魔法学校背景特效", floor_carpet_purple: "紫色地毯",
  "Red Carpet Entrance": "红地毯入口",
  p_floor_section_01: "地板区块 1", p_floor_section_02: "地板区块 2",
  p_floor_section_03: "地板区块 3", p_floor_section_04: "地板区块 4",
  p_floor_section_05: "地板区块 5",
  ice_floor_01: "冰面地板", snow_floor_01: "雪地地板",
  raft_raft_back_01: "木筏后端", raft_raft_front_01: "木筏前端",
  raft_raft_middle_01: "木筏中段 1", raft_raft_middle_02: "木筏中段 2",
  raft_raft_middle_03: "木筏中段 3", raft_water: "木筏水面",
  Travelator: "自动步道",
  // animals / special characters
  Kevin_Hawaiian_01: "凯文（夏威夷）", StoryKevin_Hawaiian: "剧情凯文（夏威夷）",
  StoryOnionKing_Hawaiian: "洋葱国王（夏威夷）", Kevin_01_DLC5: "凯文（露营）",
  StoryOnionKing_DLC5: "洋葱国王（露营）", Hover_Moth: "飞蛾",
  TropicalBirds: "热带鸟群", "TropicalBirds(noAudio)": "热带鸟群（无音效）",
  Raft_Fish_Group: "鱼群", NPC_Penguin: "企鹅顾客",
  DLC03_NPC_02: "圣诞顾客", DLC07_NPCs_Ghost: "幽灵顾客", DLC07_NPCs_Keep: "城堡顾客",
  NPC_DLC_04: "DLC 顾客 04", NPC_DLC_05: "DLC 顾客 05", NPC_DLC_06: "DLC 顾客 06",
  dlc08_NPC_FireBreather01: "喷火艺人 1", dlc08_NPC_FireBreather02: "喷火艺人 2",
  dlc08_NPC_Juggler01: "杂耍艺人", dlc08_NPC_Strongman01: "大力士 1",
  dlc08_NPC_Strongman02: "大力士 2",
  Beach_AirParrot_Blue: "海滩飞鹦鹉（蓝）", Beach_AirParrot_Purple: "海滩飞鹦鹉（紫）",
  Beach_AirParrot_Red: "海滩飞鹦鹉（红）", Beach_GroundParrot_Blue: "海滩鹦鹉（蓝）",
  Beach_GroundParrot_Purple: "海滩鹦鹉（紫）", Beach_GroundParrot_Red: "海滩鹦鹉（红）",
  City_kevin_02: "凯文雕像", m_city_kevin_01: "凯文雕像 1", m_city_kevin_02: "凯文雕像 2",
  m_city_pigeon_01: "鸽子", m_dlc5_owl_01: "猫头鹰", seagull_01: "海鸥",
  shark_01: "鲨鱼", sharkfin_01: "鲨鱼鳍", m_dlc7_raven_01: "渡鸦",
  Alien_Tentacle_01: "外星触手",
  // misc curated (from web displayLabels + common cases)
  barrier_rope_2unit_01: "绳索护栏（2格）", barrier_rope_1unit_01: "绳索护栏（1格）",
  m_raft_barrier_rope_01: "木筏绳索护栏", beachdoodads_pebbles_01: "海滩鹅卵石 1",
  beachdoodads_pebbles_02: "海滩鹅卵石 2", beachdoodads_pebbles_03: "海滩鹅卵石 3",
  beachdoodads_starfish_01: "海星 1", beachdoodads_starfish_02: "海星 2",
  beachdoodads_clamshell_01: "蛤蜊壳",
  City_path_side_01: "城市路缘", "City_restaurant_table&chairs_01": "餐厅桌椅",
  city_wall_straight_05: "城市直墙 05", Citywall_brick_01a: "城市砖墙 01a",
  Citywall_brick_01c: "城市砖墙 01c", Citywall_wallpaper_01: "城市墙纸墙",
  Citywall_wallpaper_corner_in_01: "城市墙纸内角墙",
  crate_raft_01: "木筏板条箱", crate_raft_x2_01: "木筏板条箱（双格）",
  Crate_raft_x3_01: "木筏板条箱（三格）", Crate_raft_x10_01: "木筏板条箱（十格）",
  exterior_tree_pink_01: "粉花树", exterior_tree_green_01: "绿树",
  m_sp_cliff_corner_out_01: "悬崖外转角", m_sp_cliff_edge_01: "悬崖边缘 1",
  m_sp_cliff_edge_02: "悬崖边缘 2", m_sp_cliff_edge_05: "悬崖边缘 5",
  m_sp_cliff_edge_06: "悬崖边缘 6",
  noripple_m_dlc3_icecliff_270: "冰崖转角 270°", noripple_m_dlc3_icecliff_90: "冰崖转角 90°",
  noripple_m_dlc3_icecliff_straight_01: "冰崖直边 1", noripple_m_dlc3_icecliff_straight_02: "冰崖直边 2",
  p_dlc07_shield_fork_spoon_01: "刀叉勺盾饰", p_dlc5_cabin_roof_01: "小屋屋顶",
  p_dlc5_log_stairs_01: "原木楼梯", "p_dlc5_moonshaft_01 (4)": "月光井",
  p_dlc5_twig_pile_01: "树枝堆", pillar_middle_01: "中段立柱", pillar_end_01: "端头立柱",
  pool_corner_01: "泳池转角 1", "pool_corner_01 1": "泳池转角 1'",
  pool_corner_02: "泳池转角 2", "pool_corner_02 1": "泳池转角 2'",
  pool_straight_01: "泳池直边", "pool_straight_01 1": "泳池直边'",
  pool_wall_straight_01: "泳池直墙",
  raft_leaves_01: "木筏树叶 1", raft_leave_01: "木筏树叶 1", raft_leave_02: "木筏树叶 2",
  raft_leave_03: "木筏树叶 3", raft_leave_04: "木筏树叶 4", raft_leave_05: "木筏树叶 5",
  Resort_wall_end_01: "度假村墙尽头", Resort_wall_out_01: "度假村墙外角",
  Resort_wall_straight_01: "度假村直墙", Resort_wall_window_01: "度假村窗墙",
  rooftiles_straight_01: "直瓦屋顶", SeaShore_PFX_02: "海岸特效", SeaShore_PFX: "海岸特效",
  Space_Door_Airlock_Bool_Open: "太空气闸门", sushi_sign_01: "寿司招牌 1",
  sushi_sign_02: "寿司招牌 2", sushi_sign_03: "寿司招牌 3",
  umbrella_open_01: "遮阳伞", umbrella_closed_01: "遮阳伞（合拢）",
  walkway_pillars_01: "栈道立柱", walkway_rope_02: "栈道绳索",
  wall_270_01: "270° 转角墙", wall_brick_01a: "砖墙 01a", wall_brick_01b: "砖墙 01b",
  wall_brick_01d: "砖墙 01d", wall_brick_out_01: "砖墙外角",
  wall_brick_window_03: "砖墙窗 03", Wall_straight_top_02: "直墙顶沿 02",
  dlc5_ground_camp: "露营地地面", p_dlc5_camp_fire_01: "篝火 1", p_dlc5_camp_fire_02: "篝火 2",
  p_dlc5_camp_fire_02_nopfx: "篝火 2（无特效）", p_dlc5_throne_01: "王座",
  throne_armor_01: "王座盔甲", throne_banner_01: "王座旗帜",
  throne_kevinschair_01: "凯文王座椅", throne_picture_01: "王座挂画", throne_torch: "王座火把",
  Boat_Cannon: "船炮", DogSled: "狗拉雪橇", DogSled_Luggage: "雪橇行李",
  Dinghy: "小艇", Stair: "楼梯", Stairs: "楼梯", Step: "台阶",
  Candle1Position: "蜡烛位 1", Candle2Position: "蜡烛位 2", Candle3Position: "蜡烛位 3",
  fire_hazard: "火灾隐患点", NPC_Walk_Anticlockwise_47s: "NPC 逆行巡逻路径 47s",
  NPC_Walk_Anticlockwise_67s: "NPC 逆行巡逻路径 67s",
  car_position_01: "车位 1", car_position_02: "车位 2", car_position_03: "车位 3",
  mi_lantern_02: "矿洞灯笼", m_lorry_van_back_01: "餐车尾部 1", m_lorry_van_back_02: "餐车尾部 2",
  m_lorry_van_front_01: "餐车前部", m_lorry_van_exhaust_01: "餐车排气管",
  "traffic_light green": "绿灯", "traffic_light red": "红灯", "traffic_light off": "信号灯（灭）",
  "decoration_wall_light 1": "装饰壁灯 1", "p_dlc5_grass_card_a 1": "草叶卡片 A",
  "p_dlc5_grass_card_b 1": "草叶卡片 B", "p_dlc5_grass_card_c 1": "草叶卡片 C",
  // music (proper nouns, descriptive zh)
  ASparklingWonder: "闪耀仙境（BGM）", AnOldSchoolHouse: "古老校舍（BGM）",
  ChrismasTunes: "圣诞旋律（BGM）", DLC2_EnchantedForest: "魔法森林（DLC2 BGM）",
  DLC_02_Generic: "DLC2 通用（BGM）", DLC_02_HiddenTrack: "DLC2 隐藏曲目",
  DLC_02_WorldMap: "DLC2 世界地图（BGM）", DLC_03_MapScreen_Music: "DLC3 地图界面音乐",
  DLC_04_Map_Screen: "DLC4 地图界面音乐", DLC_05_Level: "DLC5 关卡（BGM）",
  DLC_05_WorldMap: "DLC5 世界地图（BGM）", DLC_07_Battlements: "DLC7 城垛（BGM）",
  DLC_07_Keep: "DLC7 城堡（BGM）", DLC_07_WorldMap: "DLC7 世界地图（BGM）",
  DLC_08_WorldMap_Theme: "DLC8 世界地图主题曲", DLC_09_Battlements_Music: "DLC9 城垛音乐",
  DLC_09_Camping_Music: "DLC9 露营音乐", DLC_09_Fairground_Music: "DLC9 游乐场音乐",
  DLC_10_Map_Screen: "DLC10 地图界面音乐", DLC_11_Summer_Levels: "DLC11 夏日关卡（BGM）",
  DLC_11_Summer_WorldMap: "DLC11 夏日世界地图（BGM）", DLC_13_WorldMap_Music: "DLC13 世界地图音乐",
  DLC_WorldMap: "DLC 世界地图（BGM）", DynamicStage01: "动态关卡 1（BGM）",
  DynamicStage02: "动态关卡 2（BGM）", DynamicStage03: "动态关卡 3（BGM）",
  DynamicStage04: "动态关卡 4（BGM）", Earth: "地球（BGM）",
  "Festive-Medley": "节日组曲（BGM）", Snowfall_01: "落雪（BGM）",
  SpellboundSO: "魔咒（BGM）", TheMineSO: "矿洞（BGM）",
  TheWalkingBreadSO: "行走的面包（BGM）", "Up&AwaySO": "飞屋环游（BGM）",
  Overcooked_02_DLC_05_Level_SO: "DLC5 关卡（BGM）", DLC11_Summer_Levels_SO: "DLC11 夏日关卡（BGM）",
  // step icons
  BlenderIcon: "搅拌机图标", CampfireIcon: "篝火图标", DeepFatFryerIcon: "油炸锅图标",
  FryingPanIcon: "煎锅图标", GrillIcon: "烤架图标", MixerIcon: "搅拌碗图标",
  OvenIcon: "烤箱图标", PotIcon: "汤锅图标", SplitPanIcon: "分隔煎锅图标",
  SteamerIcon: "蒸笼图标",
  // misc leftovers
  aboard_01: "登船板", dlc2_Planter_01: "海滩花盆", Barrelraft_02: "木桶筏 2",
  m_dlc2_barrelraft_01: "木桶筏", m_sp_countertop_01: "台面", p_dlc7_cobweb_01: "蛛网",
  plants_01: "植物丛 1", plants_02: "植物丛 2", plants_03: "植物丛 3",
  raft_lilypad_01: "睡莲 1", raft_lilypad_02: "睡莲 2", rocket_01: "火箭",
  snowballpile_01: "雪球堆", snowmound_01: "雪堆 1", snowmound_02: "雪堆 2",
  snowmound_03: "雪堆 3", snowmound_04: "雪堆 4", snowmound_05: "雪堆 5",
  snowmound_06: "雪堆 6", snowmound_07: "雪堆 7", snowmound_08: "雪堆 8",
  woodenwall_01: "木墙", PFX_RunningPuff: "奔跑扬尘特效",
  PeckoningAudioDirectory: "禽鸟音效集",
  exterior_road_01: "马路", exterior_road_centre_01: "马路中央",
  exterior_road_crossing_01: "人行横道", exterior_road_stop_01: "停车标线",
  exterior_road_yellow_box_01: "黄色禁停区", exterior_path_01: "人行道",
  exterior_path_corner_01: "人行道转角", exterior_path_side_01: "人行道边缘",
  exterior_man_hole_01: "井盖 1", exterior_man_hole_02: "井盖 2",
  exterior_grass_01: "草坪", CityLivingSO: "城市生活（BGM）",
  DownTheRiverSO: "顺流而下（BGM）", OuterSpaceSO: "外太空（BGM）",
  // water / shore / swamp backgrounds
  m_dlc2_beach_sea_plane_wet_sand: "DLC2海滩湿沙海面",
  m_dlc2_sanddecal_05: "DLC2沙地贴花 5",
  m_dlc2_sanddecal_06: "DLC2沙地贴花 6",
  m_dlc5_river_01: "DLC5河流 1",
  m_mapwatercurve120_01: "地图弯曲水面 120°",
  m_mapwaterroad_01: "地图水路",
  m_mapwaterroadshort_01: "地图短水路",
  p_dlc4_water_01: "DLC4水面 1",
  river_edge_foam_01: "河流边缘浪花 1",
  sea_shore_simple_01: "海岸（简约）",
  seashore_pfx: "海岸特效",
  seashore_pfx_02: "海岸特效 2",
  sm_dlc13_water_edge: "DLC13水面边缘",
  sm_dlc13_waterbase_1to2: "DLC13水面底座（1转2）",
  SM_DLC13_WaterBase_1to2: "DLC13水面底座（1转2）",
  sm_dlc13_sun: "DLC13太阳",
  sm_dlc13_waterbase_1: "DLC13水面底座 1",
  swamp_water: "沼泽水面",
  watersplash_particle_001: "水花粒子 1",
  watersplash_particle_003: "水花粒子 3",
  countertop_01_standard_corner_swamp: "台面标准转角（沼泽）",
  grave_sky_plane_01: "墓地天空平面",
  // leftovers that token rules miss
  "1_5_hazardspawner_01": "1.5 危险生成器",
  backgroundbirdparent: "背景鸟群父节点",
  camershakegroup: "镜头抖动组",
  castsshadow_misc: "投射阴影（杂项）",
  conveyorstationprop: "传送带站道具",
  depth_blend_far: "远景深度混合",
  equipment_steamer_01: "蒸锅设备",
  firehazard: "火灾危险点",
  "pfx_lighting_transition1": "光照过渡特效 1",
  props: "道具组",
  pudding: "布丁",
  raft_dark: "木筏（暗色）",
  ratscurry: "老鼠窜动",
  scenery: "布景",
  "splahes background": "溅水背景",
  splash_raft: "木筏溅水",
  steps: "台阶",
  stonewalls_01: "石墙 1",
  stonewalls_02: "石墙 2",
  storyonionking_hawaiian: "剧情洋葱王（夏威夷）",
  swamp_pickaxe_01: "沼泽镐",
  swamp_railbroken_01: "沼泽断裂轨道",
  swamp_rails_01: "沼泽轨道",
  swamp_stoneblock_01: "沼泽石块 1",
  swamp_stoneblock_02: "沼泽石块 2",
  swamp_truss_01: "沼泽桁架",
  swamp_trusslarge_01: "沼泽大桁架 1",
  swamp_trusslarge_02: "沼泽大桁架 2",
  timeline_dynamicstage_04: "时间轴动态关卡 4",
  timeline_tutorial_01: "时间轴教程 1",
  turbinewind: "涡轮风",
  unbread_a: "未眠面包 A",
  walkingbread_01a: "行走面包 1a",
  walkingbread_01a_walking: "行走面包 1a（行走）",
  walkingbread_01b: "行走面包 1b",
  walkingbread_01b_walking: "行走面包 1b（行走）",
  walkingbread_02a: "行走面包 2a",
  walkingbread_02a_walking: "行走面包 2a（行走）",
  walkingbread_02b: "行走面包 2b",
  walkingbread_02b_walking: "行走面包 2b（行走）",
  walkingbreadwalkingpath: "行走面包路径",
  wallblock: "墙块",
  optionalhotchocolate: "可选热可可",
  flyingpigeon: "飞鸽",
  gameobject: "游戏对象",
  hazardspawner_a: "危险生成器 A",
  hazardspawner_b: "危险生成器 B",
  "hazardspawner_central (high)": "危险生成器（中央·高）",
  island_01: "岛屿 1",
  island_02: "岛屿 2",
  island_03: "岛屿 3",
  killplanes: "死亡平面组",
  lift: "升降台",
  m_airballoon_floorboard_broken_01: "热气球破损地板 1",
  m_airballoon_floorboard_broken_02: "热气球破损地板 2",
  m_airballoon_floorboard_broken_03: "热气球破损地板 3",
  m_airballoon_ropehighres_01: "热气球高清绳 1",
  m_bamboofloat_01: "竹筏 1",
  m_dlc6_drawbridge_ledge_01: "DLC6吊桥檐口 1",
  m_dlc6_drawbridge_ledge_02: "DLC6吊桥檐口 2",
  m_dogbowl_01: "狗碗 1",
  m_dynamic03_vortex_torus_01: "动态漩涡环面",
  m_floormesh_level_01: "地板网格",
  m_sk_cooker_01: "灶台 SK",
  magic_countertopa: "魔法台面 A",
  mi_cart_01: "矿洞矿车 1",
  mi_crystal_02: "矿洞水晶 2",
  mi_crystals_01: "矿洞水晶 1",
  mi_crystals_02: "矿洞水晶 2",
  mi_dark_01: "矿洞暗区 1",
  mi_elevatedrails_01: "矿洞高架轨道",
  mi_gold_01: "矿洞金矿",
  mi_minecart_crystals: "矿车（水晶）",
  mi_minecart_gold: "矿车（金矿）",
  mi_mudwalls_01: "矿洞泥墙",
  mi_mudwalls_01_outside: "矿洞泥墙（外侧）",
  mi_pickaxe_01: "矿洞镐",
  mi_railbroken_01: "矿洞断裂轨道",
  mi_rails_01: "矿洞轨道",
  mi_ropecoil_01: "矿洞绳卷",
  mi_truss_01: "矿洞桁架 1",
  mi_truss_02: "矿洞桁架 2",
  mi_trusslarge_01: "矿洞大桁架 1",
  mi_trusslarge_02: "矿洞大桁架 2",
  minecartparent_01: "矿车父节点",
  movingplatform: "移动平台",
  ms_pfx_teleport_1: "传送特效 1",
  multiplayergamecamera: "多人游戏相机",
  noncastsshadow_misc: "无投射阴影（杂项）",
  onionking_01: "洋葱王",
  air_balloon_sandbag_01: "热气球沙袋",
  air_balloon_wing_01: "热气球机翼",
  airballoon_frontspike_01: "热气球前刺",
  beachdoodads_clamshell_02: "海滩蛤蜊壳 2",
  beachdoodads_starfish_03: "海滩海星 3",
};

const CURATED_IDS = new Set(Object.keys(CURATED));

const NPC_PERSONA_ZH = {
  Alien: "外星人", Asian: "亚裔", Beard: "大胡子", Dora: "朵拉", DoraBlonde: "金发朵拉",
  Ginger: "姜发", Hispanic: "拉丁裔", MiddleEastern: "中东", MiddleEatern: "中东",
  Mike: "迈克", Specs: "眼镜", WizBoy: "巫师男孩",
};

const NPC_VARIANT_ZH = {
  Blue: "蓝", Brown: "棕", Green: "绿", Orange: "橙", Yellow: "黄", Pink: "粉",
  Civ: "便装", Construction: "施工", HardHat: "安全帽", Waiter: "服务生",
  Wizard: "巫师", Hawaiian: "夏威夷",
};

// ---------------------------------------------------------------------------
// Token rules for drafting
// ---------------------------------------------------------------------------

const STRIP_PREFIXES = ["noripple_", "PFX_", "p_", "m_", "ui_", "t_", "mat_"];

const TOKEN_ZH = {
  // structures
  wall: "墙", straight: "直", corner: "转角", end: "尽头", out: "外", in: "内",
  brick: "砖", window: "窗", wallpaper: "墙纸", floor: "地板", roof: "屋顶",
  rooftiles: "屋顶瓦", tiles: "瓦", tile: "瓦片", ridge: "脊", section: "段",
  vbeam: "V形梁", door: "门", double: "双开", stair: "楼梯", stairs: "楼梯",
  step: "台阶", side: "侧边", ladder: "梯子", scaffold: "脚手架",
  pillar: "立柱", arch: "拱门", fence: "栅栏", barrier: "护栏", rope: "绳",
  bridge: "桥", ropebridge: "绳桥", hut: "小屋", cabin: "小屋", building: "建筑",
  tent: "帐篷", treehouse: "树屋", wallbit: "墙块", rooftop: "屋顶",
  // furniture / props
  table: "桌", chair: "椅", bench: "长椅", stool: "凳", sunbed: "日光浴床",
  bedroll: "铺盖", box: "箱子", lid: "盖", crate: "板条箱", barrel: "桶",
  urn: "瓮", bucket: "水桶", spade: "沙铲", mug: "杯子", lantern: "灯笼",
  torch: "火把", candle: "蜡烛", book: "书", picture: "画", banner: "横幅",
  flag: "旗帜", bunting: "彩旗", sign: "招牌", signpost: "指示牌",
  rack: "架", ticket: "票据", spice: "香料", vent: "通风口", light: "灯",
  plate: "盘子", drink: "饮料", trash: "垃圾", rubbishbin: "垃圾桶", bin: "垃圾桶",
  hydrant: "消防栓", cone: "锥桶", scooter: "小摩托", lorry: "卡车", van: "货车",
  exhaust: "排气管", dinghy: "小艇", canoe: "独木舟", boat: "船", cannon: "炮",
  dogsled: "狗拉雪橇", luggage: "行李", backpack: "背包", surfboard: "冲浪板",
  towel: "毛巾", umbrella: "遮阳伞", sunglasses: "太阳镜", beachball: "沙滩球",
  divingboard: "跳水板", airbed: "充气床", buoy: "浮标", lifebuoypillar: "救生圈柱",
  rug: "地毯", carpet: "地毯", masonjar: "玻璃罐", hatchet: "斧头", hotchoc: "热可可",
  nail: "钉子", plywood: "胶合板", plank: "木板", planks: "木板", woodplank: "木板",
  wood: "木", wooden: "木制", log: "原木", twig: "树枝", stump: "树桩",
  armor: "盔甲", throne: "王座", shield: "盾", fork: "叉", spoon: "勺",
  skewer: "签", floats: "浮筒", support: "支撑", base: "底座",
  marketstall: "市集摊位", marksetstall: "市集摊位", stall: "摊位", canopy: "遮阳棚",
  decoration: "装饰", dressing: "装饰", blank: "空白", plane: "平面", curved: "弧形",
  // nature
  tree: "树", palm: "棕榈", palmtree: "棕榈树", coconut: "椰子", pine: "松",
  branch: "枝", trunk: "树干", leaf: "叶", leaves: "树叶", vines: "藤蔓",
  grass: "草", fern: "蕨", reed: "芦苇", flower: "花", flowers: "花", bush: "灌木",
  plant: "植物", rubber: "橡胶", mushroom: "蘑菇", pineapple: "菠萝",
  rock: "岩石", rockwall: "岩壁", stone: "石", stones: "石块", slab: "石板",
  small: "小", large: "大", pile: "堆", cliff: "悬崖", edge: "边缘",
  sand: "沙", sandcastle: "沙堡", sanddecal: "沙地贴花", beach: "海滩",
  sea: "海", shore: "岸", seashore: "海岸", ocean: "海洋", water: "水",
  pool: "泳池", poolwater: "泳池水", river: "河", ripple: "涟漪", wake: "尾流",
  moonshaft: "月光井", fog: "雾", forestfog: "森林雾", shadowcast: "投影",
  grave: "墓地", gravestone: "墓碑", ice: "冰", iceblock: "冰块", icecliff: "冰崖",
  snow: "雪", ground: "地面", path: "小路", cobble: "卵石", flagstone: "石板",
  courtyard: "庭院", walkway: "栈道", sparkles: "闪光",
  // city / road
  city: "城市", sushi: "寿司", restaurant: "餐厅", exterior: "室外", road: "路",
  crossing: "路口", centre: "中心", stop: "停靠", car: "车", traffic: "交通",
  kitchen: "厨房", firepit: "火坑", bamboo: "竹", market: "市集",
  sumi: "墨绘", airlock: "气闸", space: "太空", debris: "残骸",
  brokentiles: "碎砖", tentacle: "触手", alien: "外星",
  // characters / animals
  npc: "顾客", kevin: "凯文", pigeon: "鸽子", owl: "猫头鹰", seagull: "海鸥",
  shark: "鲨鱼", fin: "鳍", raven: "渡鸦", duck: "鸭", flamingo: "火烈鸟",
  parrot: "鹦鹉", moth: "蛾", fish: "鱼", crab: "蟹",
  // modifiers
  open: "打开", closed: "合拢", short: "短", long: "长", alt: "变体",
  night: "夜晚", day: "白天", crack: "裂缝", ornate: "华丽", orange: "橙",
  blue: "蓝", green: "绿", red: "红", yellow: "黄", purple: "紫", pink: "粉",
  brown: "棕", front: "前端", back: "后端", middle: "中段", top: "顶部",
  inside: "内侧", right: "右", angle: "角", air: "飞", hover: "悬停",
  swing: "秋千", group: "组", position: "位置", walk: "步行",
  anticlockwise: "逆时针", nopfx: "无特效", noaudio: "无音效",
  camp: "营地", campsite: "营地", campfire: "篝火", resort: "度假村",
  wizard: "魔法", grave_fence: "墓园栅栏",
  "1unit": "1格", "2unit": "2格", x2: "双格", x3: "三格", x5: "五格", x10: "十格",
  smoothie: "冰沙", strawberry: "草莓", clamshell: "蛤蜊壳", pebbles: "鹅卵石",
  starfish: "海星", doodads: "小物件", stonewall: "石墙", king: "国王",
  onion: "洋葱", hover_moth: "飞蛾",
  balloon: "气球", burner: "燃烧器", standard: "标准", group: "组",
  citywall: "城市墙", living: "生活", planter: "花盆", lilypad: "睡莲",
  rocket: "火箭", snowballpile: "雪球堆", snowmound: "雪堆", cobweb: "蛛网",
  countertop: "台面", aboard: "登船板", barrelraft: "木桶筏",
  dlc2: "DLC2", dlc02: "DLC2", dlc3: "DLC3", dlc03: "DLC3",
  dlc5: "DLC5", dlc05: "DLC5", dlc7: "DLC7", dlc07: "DLC7",
  dlc8: "DLC8", dlc08: "DLC8", dlc9: "DLC9", dlc09: "DLC9",
  sp: "太空", keep: "城堡", horde: "部落", circus: "马戏团",
  wonderland: "仙境", christmas: "圣诞", graveyard: "墓地", mine: "矿洞",
  theme: "主题曲", medley: "组曲", wonder: "仙境", sparkling: "闪耀",
  // ---- DLC 装饰（gen-decor-entries 新增，2026-08）----
  candycane: "拐杖糖", toy: "玩具", cracker: "拉炮", gingerbreadman: "姜饼人",
  horse: "木马", letterblock: "字母积木", nutcracker: "胡桃夹子", pretzel: "椒盐卷饼",
  snowglobe: "雪景球", train: "火车", gift: "礼物", bigsack: "大麻袋", bow: "蝴蝶结",
  evergreen: "常青树", decorated: "装饰", mince: "肉馅", pies: "派",
  hotchocolate: "热可可", iceberg: "冰山", icicle: "冰柱", snowpile: "雪堆",
  sled: "雪橇", sack: "麻袋", northpole: "北极点", treestump: "树桩",
  longbench: "长椅", longtable: "长桌", streetlamp: "路灯", marketcrate: "市集板条箱",
  butterflies: "蝴蝶", flightpath: "飞行轨迹", flyinglanterns: "飞天灯笼",
  flyinglanternsgroup: "飞天灯笼组", lilypods: "睡莲叶", incense: "香炉",
  crazypaving: "碎拼石板", flooredge: "地板边缘", floortile: "地板砖",
  mud: "泥地", mudmound: "泥堆", moss: "苔藓", pond: "池塘", pathedge: "路边",
  squarestone: "方石", stonebase: "石基", temple: "寺庙", vase: "花瓶",
  pagoda: "宝塔", npctablechair: "顾客桌椅", archway: "牌坊", dragon: "龙",
  statue: "雕像", fishfloat: "鱼漂", seafloat: "海漂", wavefloat: "波浪浮筒",
  genericfloat: "通用浮筒", boxlantern: "盒灯", lotus: "莲花", candle: "烛台",
  seaweedfloat: "海草浮筒", waterwheel: "水车", woodenfloor: "木地板",
  woodenwall: "木墙", confetti: "彩纸屑", fireworkbox: "烟花箱", firework: "烟花",
  pinata: "皮纳塔", banana: "香蕉", broccoli: "西兰花", carrot: "胡萝卜",
  eggplant: "茄子", grapes: "葡萄", tomato: "番茄", balloons: "气球串",
  baloon: "气球", playable: "可玩", scene: "场景", lights: "灯串", giant: "巨型",
  map: "地图", snowsparkle: "雪闪", mid: "中段", wide: "宽", wind: "风",
  float: "浮筒", collision: "碰撞", beachthrone: "海滩王座", tropicalbirds: "热带鸟",
  merge: "拼块", merger: "拼块", diners: "小餐馆", vanbunting: "货车彩旗",
  dragonstatue: "龙雕像", mincepie: "肉馅派",
  dlc4: "DLC4", dlc10: "DLC10", dlc11: "DLC11", dlc13: "DLC13",
  icebergs: "冰山", floatingboxlantern: "漂浮盒灯", flyingboxlantern: "飞天盒灯",
  lotuscandle: "莲花烛", shovel: "铲子", mi: "",
  mooncake: "月饼", chocolate: "巧克力", watermelon: "西瓜",
  plants: "植物", incensepot: "香炉", plantpot: "花盆", seawavefloat: "海浪浮筒",
  // misc tokens (decor / tech / water)
  broken: "破损", ani: "动画", sandbag: "沙袋", rotor: "旋翼", wings: "机翼", wing: "机翼",
  spike: "尖刺", frontspike: "前刺", pickaxe: "镐", rails: "轨道", rail: "轨道",
  railbroken: "断裂轨道", truss: "桁架", trusslarge: "大桁架", stoneblock: "石块",
  stonewalls: "石墙", simple: "简约", pfx: "特效", foam: "浪花", wet: "湿",
  splash: "溅水", splahes: "溅水", particle: "粒子", particles: "粒子",
  background: "背景", bird: "鸟", parent: "父节点", hazardspawner: "危险生成器",
  manhole: "井盖", hole: "孔", firework: "烟花", fireworks: "烟花", neon: "霓虹",
  animated: "动画", floating: "漂浮", counter: "柜台", street: "街",
  fire: "火", hydrant: "消防栓", swamp: "沼泽", trusses: "桁架",
  wet: "湿", sun: "太阳", waterbase: "水面底座", wateredge: "水面边缘",
  beachdoodads: "海滩小物件", doodads: "小物件", robin: "知更鸟",
  sky: "天空", decal: "贴花", offshore: "近海", extended: "延伸",
  foam: "浪花", edge: "边缘", foamy: "泡沫", wet: "湿",
  standard: "标准", turquoise: "青绿", rootbeerfloat: "沙士浮冰",
  orangefloat: "橙味浮冰", surf: "冲浪", island: "岛屿", block: "块",
  props: "道具", scenery: "布景", pudding: "布丁", steps: "台阶",
  lift: "升降", platform: "平台", camera: "相机", game: "游戏",
  object: "对象", optional: "可选", flying: "飞", pigeon: "鸽子",
  walking: "行走", path: "路径", bread: "面包", unbread: "未眠面包",
  timeline: "时间轴", tutorial: "教程", dynamicstage: "动态关卡",
  turbinewind: "涡轮风", turbine: "涡轮", wind: "风",
  castsshadow: "投射阴影", noncastsshadow: "无投射阴影", misc: "杂项",
  depth: "深度", blend: "混合", far: "远", lighting: "光照",
  transition: "过渡", equipment: "设备", steamer: "蒸锅",
  conveyorstation: "传送带站", prop: "道具", camershake: "镜头抖动",
  shake: "抖动", floorboard: "地板板", ropehighres: "高清绳",
  drawbridge: "吊桥", ledge: "檐口", dogbowl: "狗碗", bamboofloat: "竹筏",
  vortex: "漩涡", torus: "环面", floormesh: "地板网格", magic: "魔法",
  countertopa: "台面 A", cart: "矿车", crystal: "水晶", crystals: "水晶",
  dark: "暗", elevatedrails: "高架轨道", gold: "金", minecart: "矿车",
  mudwalls: "泥墙", outside: "外侧", ropecoil: "绳卷", onionking: "洋葱王",
  storyonionking: "剧情洋葱王", killplanes: "死亡平面", killplane: "死亡平面",
  sm: "", highres: "高清", low: "低", v2: "变体2", off: "关闭",
  burner: "燃烧器", spike: "尖刺", rotor: "旋翼", banner: "横幅",
  flag: "旗帜", nail: "钉", standard: "标准", blue: "蓝",
  building: "建筑", walls: "墙", roof: "屋顶", wallblock: "墙块",
  dlc6: "DLC6", dlc06: "DLC6", dlc12: "DLC12", dlc14: "DLC14",
  dlc15: "DLC15", dlc16: "DLC16", dlc17: "DLC17",
  hawaiian: "夏威夷", central: "中央", high: "高",
  robin: "知更鸟", ground: "地面",
  airparrot: "飞鹦鹉", groundparrot: "地面鹦鹉", prefab: "",
  boiledfrankfurter: "煮热狗肠", frankfurter: "热狗肠", boiled: "煮",
  anim: "动画", round: "圆形", floating: "漂浮", lantern: "灯笼",
  seafloat: "海漂", map: "地图", camp: "营地",
  waterbase: "水面底座", water: "水", wet: "湿", sand: "沙",
};

const COMPOUND_TOKEN_ZH = {
  beachdoodads: "海滩小物件",
  waterbase: "水面底座",
  hazardspawner: "危险生成器",
  floorboard: "地板板",
  ropehighres: "高清绳",
  mapwatercurve120: "地图弯曲水面120°",
  mapwaterroad: "地图水路",
  mapwaterroadshort: "地图短水路",
  stonewalls: "石墙",
  frontspike: "前刺",
  walkingbread: "行走面包",
  walkingbreadwalkingpath: "行走面包路径",
  optionalhotchocolate: "可选热可可",
  flyingpigeon: "飞鸽",
  gameobject: "游戏对象",
  killplanes: "死亡平面组",
  movingplatform: "移动平台",
  multiplayergamecamera: "多人游戏相机",
  minecartparent: "矿车父节点",
  conveyorstationprop: "传送带站道具",
  camershakegroup: "镜头抖动组",
  backgroundbirdparent: "背景鸟群父节点",
  castsshadow_misc: "投射阴影（杂项）",
  noncastsshadow_misc: "无投射阴影（杂项）",
  depth_blend_far: "远景深度混合",
  airballoon_burner_standard_blue: "热气球燃烧器（标准蓝）",
  "airballoon_burner off": "热气球燃烧器（关闭）",
};

// ---------------------------------------------------------------------------

const LATIN_WORD_RE = /[a-zA-Z]{3,}/g;

function latinWordCount(zh) {
  const cleaned = zh
    .replace(/DLC\d+/gi, "")
    .replace(/BGM/gi, "")
    .replace(/NPC/gi, "")
    .replace(/PFX/gi, "")
    .replace(/\d+°/g, "");
  const m = cleaned.match(LATIN_WORD_RE);
  return m ? m.length : 0;
}

function normalizeNumber(tok) {
  const m = /^0*(\d+[a-z]?)$/.exec(tok);
  return m ? m[1] : tok;
}

const CJK_RE = /[一-鿿]/;

/** Join drafted parts: CJK tokens run together, latin/numbers stay spaced. */
function joinParts(parts) {
  let out = "";
  for (const part of parts) {
    if (!out) {
      out = part;
      continue;
    }
    const prevCjk = CJK_RE.test(out[out.length - 1]);
    const curCjk = CJK_RE.test(part[0]);
    out += prevCjk && curCjk ? part : " " + part;
  }
  return out.trim();
}

function splitTokens(id) {
  let s = id;
  for (const p of STRIP_PREFIXES) {
    if (s.toLowerCase().startsWith(p.toLowerCase())) {
      s = s.slice(p.length);
      break;
    }
  }
  const raw = s.split(/[_\s]+/).filter(Boolean);
  const tokens = [];
  for (const part of raw) {
    // Whole-part hits (dlc2, 1x28, …) stay atomic.
    if (TOKEN_ZH[part.toLowerCase()] || /^\d+x\d+$/.test(part)) {
      tokens.push(part);
      continue;
    }
    const camel = part
      .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
      .replace(/([0-9])([A-Z])/g, "$1 $2")
      .replace(/([A-Za-z])([0-9])/g, "$1 $2")
      .split(" ");
    tokens.push(...camel);
  }
  // Drop a trailing "SO" token (ScriptableObject suffix carries no meaning).
  if (tokens.length > 1 && tokens[tokens.length - 1] === "SO") tokens.pop();
  return tokens;
}

/** Suffix pattern rules applied before token drafting. */
const SUFFIX_RULES = [
  { re: /^(.*?)AudioDirectory(?:SO)?$/, suffix: "音效集" },
];

/** Theme prefix rules: strip from the id before tokenizing, prepend prefixZh. */
const PREFIX_RULES = [
  { re: /^air_balloon_/, prefixZh: "热气球·" },
  { re: /^airballoon_/, prefixZh: "热气球·" },
  { re: /^m_airballoon_/, prefixZh: "热气球·" },
  { re: /^swamp_/, prefixZh: "沼泽·" },
  { re: /^sm_dlc13_/i, prefixZh: "DLC13·" },
  { re: /^m_dlc2_beach_/, prefixZh: "DLC2海滩·" },
  { re: /^m_dlc5_/, prefixZh: "DLC5·" },
  { re: /^m_dlc6_/, prefixZh: "DLC6·" },
  { re: /^m_mapwater/, prefixZh: "地图水面·" },
  { re: /^mi_/, prefixZh: "矿洞·" },
  { re: /^watersplash_/, prefixZh: "水花·" },
  { re: /^beachdoodads_/, prefixZh: "海滩小物件·" },
];

function draftZh(id) {
  if (COMPOUND_TOKEN_ZH[id]) return COMPOUND_TOKEN_ZH[id];
  if (CURATED[id]) return CURATED[id];

  const npc = /^NPC_([A-Za-z]+)_([A-Za-z]+)_(\d+)$/.exec(id);
  if (npc) {
    const persona = NPC_PERSONA_ZH[npc[1]] || npc[1];
    const variant = NPC_VARIANT_ZH[npc[2]] || npc[2];
    return `顾客·${persona}（${variant}）`;
  }

  for (const rule of SUFFIX_RULES) {
    const m = rule.re.exec(id);
    if (m) {
      const prefix = m[1]
        .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
        .replace(/_/g, " ")
        .trim();
      return `${prefix} ${rule.suffix}`;
    }
  }

  let prefixZh = "";
  let rest = id;
  for (const rule of PREFIX_RULES) {
    if (rule.re.test(rest)) {
      prefixZh = rule.prefixZh;
      rest = rest.replace(rule.re, "");
      break;
    }
  }
  const drafted = draftTokens(rest);
  if (!drafted) return null;
  return prefixZh + drafted;
}

function draftTokens(id) {

  const tokens = splitTokens(id);
  const parts = [];
  let translated = 0;
  for (const tok of tokens) {
    const num = normalizeNumber(tok);
    if (num !== tok) {
      parts.push(num);
      continue;
    }
    const lower = tok.toLowerCase();
    const compound = COMPOUND_TOKEN_ZH[lower] ?? COMPOUND_TOKEN_ZH[tok];
    if (compound) {
      parts.push(compound);
      translated++;
      continue;
    }
    const dlc = /^dlc(\d+)$/i.exec(lower);
    if (dlc) {
      parts.push(`DLC${dlc[1]}`);
      translated++;
      continue;
    }
    const toRange = /^(\d+)to(\d+)$/i.exec(lower);
    if (toRange) {
      parts.push(`${toRange[1]}转${toRange[2]}`);
      translated++;
      continue;
    }
    const zh = TOKEN_ZH[lower];
    if (zh !== undefined) {
      if (zh) parts.push(zh);
      translated++;
    } else {
      parts.push(tok);
    }
  }
  if (translated === 0) return null;
  return joinParts(parts);
}

function draftEn(id) {
  const tokens = splitTokens(id).map((t) =>
    /^0*\d+$/.test(t) ? String(Number(t)) : t
  );
  return tokens.join(" ").replace(/\s+/g, " ").trim();
}

function collectIds() {
  const ids = new Set();
  const read = (name) => JSON.parse(fs.readFileSync(path.join(PUBLIC_DIR, name), "utf8"));
  // catalog.json / recipes.json 已切分为索引 + 分块/分组文件，需聚合。
  const catalog = read("catalog.json");
  for (const chunkFile of catalog.itemChunks || []) {
    for (const it of read(chunkFile).items || []) ids.add(it.id);
  }
  for (const it of read("ingredients.json").ingredients || []) ids.add(it.id);
  const recipesIndex = read("recipes.json");
  for (const groupFile of Object.values(recipesIndex.groupFiles || {})) {
    for (const it of read(groupFile).recipes || []) ids.add(it.id);
  }
  for (const it of read("cooking-steps.json").cookingSteps || []) ids.add(it.id);
  const audio = read("audio-catalog.json");
  for (const it of audio.music || []) ids.add(it.id);
  for (const it of audio.audioDirectories || []) ids.add(it.id);
  for (const it of audio.deathEffects || []) ids.add(it.id);
  return ids;
}

function main() {
  const refresh = process.argv.includes("--refresh");
  const doc = JSON.parse(fs.readFileSync(DICT_PATH, "utf8"));
  const names = doc.names || [];
  const have = new Set(names.map((n) => n.id));

  let refreshed = 0;
  if (refresh) {
    for (const entry of names) {
      const curated = CURATED[entry.id];
      if (curated && entry.zh !== curated) {
        entry.zh = curated;
        entry.en = draftEn(entry.id);
        refreshed++;
        continue;
      }
      if (CURATED_IDS.has(entry.id)) continue;
      if (latinWordCount(entry.zh) === 0) continue;
      const newZh = draftZh(entry.id);
      if (!newZh || newZh === entry.zh) continue;
      const oldScore = latinWordCount(entry.zh);
      const newScore = latinWordCount(newZh);
      if (newScore < oldScore || (newScore === 0 && oldScore > 0)) {
        entry.zh = newZh;
        entry.en = draftEn(entry.id);
        refreshed++;
      }
    }
    console.log(`Refreshed ${refreshed} draft entries with leftover English.`);
  }

  const ids = collectIds();
  const untranslated = [];
  let added = 0;
  for (const id of [...ids].sort()) {
    if (have.has(id)) continue;
    const zh = draftZh(id);
    if (!zh) {
      untranslated.push(id);
      continue;
    }
    names.push({ id, zh, en: draftEn(id) });
    added++;
  }

  names.sort((a, b) => a.id.localeCompare(b.id));
  doc.names = names;
  fs.writeFileSync(DICT_PATH, JSON.stringify(doc, null, 2), "utf8");

  console.log(`Added ${added} draft entries (${names.length} total).`);
  if (untranslated.length > 0) {
    console.log(`Untranslated ${untranslated.length} ids (add them manually to names-dictionary.json):`);
    for (const id of untranslated) console.log(`  ${id}`);
  }
}

main();
