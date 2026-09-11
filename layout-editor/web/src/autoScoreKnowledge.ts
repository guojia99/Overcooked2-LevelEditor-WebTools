/**
 * 定分知识库 —— 一键定分（autoScore.ts）的规则数据源。
 *
 * 数据来源：
 *  - Docs/定分/胡闹厨房机制总结.txt（小费/连击/超时/各设备基准时间/燃料/火焰）
 *  - Docs/定分/一些规则表.jpg（官方规则表截图）
 *  - 设备 id 与分类核对自 web/public/catalog.json 核心层目录（灶台/切配/水槽/机制…）
 *  - 断头台（workstation_guillotine_01）：需按钮触发、一次可切 2 个、切速 1 秒
 *
 * 四块内容：① 站点分类表  ② 工序时长表（锅具/切配/洗盘）  ③ 需切食材识别  ④ 步行与风险参数。
 * 本文件只放数据与纯函数；估时模型见 autoScore.ts，场景解析见 kitchenAnalysis.ts。
 */

// ==================== ① 站点分类表 ====================
// key = 场景物件 prefab id（prefabAssetPath 基名，无 .prefab 后缀）。

export type StationClass =
  | "hob" // 灶台（放煎锅/汤锅/蒸笼）
  | "fryStation" // 炸台（放炸篮）
  | "oven" // 烤箱
  | "barbeque" // 烧烤架（烤串）
  | "campfire" // 篝火（煎烤盘/烘烤叉）
  | "stoneFurnace" // 石炉/烤炉（需手动看火）
  | "hotpotBurner" // 火锅灶台（2×2 加热区）
  | "burner" // 明火灶（着火风险）
  | "chop" // 切菜台
  | "guillotine" // 断头台
  | "mixer" // 搅拌台（放搅拌碗）
  | "blender" // 搅拌机（放搅拌杯）
  | "sink" // 水槽（洗餐具）
  | "bin" // 垃圾箱
  | "plateReturn" // 脏餐具回收台
  | "serving" // 上菜台
  | "plateStack" // 干净餐具堆
  | "dispenser" // 固定食材箱
  | "randomDispenser" // 随机食材箱
  | "conveyor" // 传送带站
  | "teleportal" // 传送门
  | "travelator" // 自动步道
  | "wind" // 风雪（推移干扰）
  | "cannon" // 大炮
  | "switch" // 按钮/拨动开关
  | "pressureSwitch"; // 压力开关

export const STATION_CLASS_BY_ID: Record<string, StationClass> = {
  // counters/cooking
  Cooker: "hob",
  FryingStation: "fryStation",
  Oven: "oven",
  Barbeque: "barbeque",
  Campfire: "campfire",
  oven_furnace_medieval: "stoneFurnace",
  workstation_furnace_01: "stoneFurnace",
  // counters/prep
  ChoppingCounter: "chop",
  workstation_guillotine_01: "guillotine",
  // counters/service
  ServingStation: "serving",
  Dispenser: "dispenser",
  dispenser_coal_01: "dispenser",
  AttachingFoodSpawner: "dispenser",
  RandomDispenser: "randomDispenser",
  Mixer: "mixer",
  dlc08_workstation_mixer: "mixer",
  Blender: "blender",
  workstation_blender_01: "blender",
  // counters/sinks
  Sink: "sink",
  SinkGlass: "sink",
  SinkMug: "sink",
  SinkTray: "sink",
  dlc08_workstation_01_tray_sink_circus: "sink",
  workstation_sink_mug_01_wood: "sink",
  Bin: "bin",
  dlc13_workstation_bin_01: "bin",
  PlateReturn: "plateReturn",
  GlassReturn: "plateReturn",
  workstation_tray_return: "plateReturn",
  workstation_mug_return: "plateReturn",
  // utensils/plates（干净餐具堆：盘/杯/马克杯/餐盘）
  CleanPlateStack: "plateStack",
  CleanGlassStack: "plateStack",
  cleanmugstack: "plateStack",
  dlc08_cleantraystack: "plateStack",
  // hotpot/web + mechanisms（火锅灶台）
  cooking_region_floorburner: "hotpotBurner",
  web_cooking_region_floorburner: "hotpotBurner",
  web_dlc10_cooking_region_floorburner: "hotpotBurner",
  // mechanisms
  Burner: "burner",
  ConveyorStation: "conveyor",
  Teleportal: "teleportal",
  Travelator: "travelator",
  wind_snow_left: "wind",
  wind_snow_right: "wind",
  dlc08_cannon: "cannon",
  dlc09_cannon: "cannon",
  Switch: "switch",
  ToggleSwitch: "switch",
  PressureSwitch: "pressureSwitch",
  dlc13_lotuspressureswitch_large: "pressureSwitch",
  dlc13_lotuspressureswitch_small: "pressureSwitch",
  dlc13_lotuspressureswitch_small_2: "pressureSwitch",
};

export const STATION_CLASS_ZH: Record<StationClass, string> = {
  hob: "灶台",
  fryStation: "炸台",
  oven: "烤箱",
  barbeque: "烧烤架",
  campfire: "篝火",
  stoneFurnace: "石炉",
  hotpotBurner: "火锅灶",
  burner: "明火灶",
  chop: "切菜台",
  guillotine: "断头台",
  mixer: "搅拌台",
  blender: "搅拌机",
  sink: "水槽",
  bin: "垃圾箱",
  plateReturn: "脏盘回收",
  serving: "上菜台",
  plateStack: "干净餐具堆",
  dispenser: "食材箱",
  randomDispenser: "随机食材箱",
  conveyor: "传送带",
  teleportal: "传送门",
  travelator: "步道",
  wind: "风雪",
  cannon: "大炮",
  switch: "按钮",
  pressureSwitch: "压力开关",
};

/** 站点 → 功能分区（步行距离计算用）。 */
export type KitchenZone = "source" | "prep" | "cook" | "serve" | "wash" | "plate";

export const STATION_ZONE: Partial<Record<StationClass, KitchenZone>> = {
  dispenser: "source",
  randomDispenser: "source",
  chop: "prep",
  guillotine: "prep",
  hob: "cook",
  fryStation: "cook",
  oven: "cook",
  barbeque: "cook",
  campfire: "cook",
  stoneFurnace: "cook",
  hotpotBurner: "cook",
  burner: "cook",
  mixer: "cook",
  blender: "cook",
  serving: "serve",
  plateStack: "plate",
  sink: "wash",
  plateReturn: "wash",
};

// ==================== ② 工序时长表 ====================

/** 锅具种类（场景 utensil 目录 id + 菜谱 cookingStep 归一后的种类）。 */
export type UtensilKind =
  | "FryPan"
  | "Pot"
  | "Steamer"
  | "FrierBasket"
  | "OvenTray"
  | "RoastingTray"
  | "OvenCakeTin"
  | "MixerBowl"
  | "BlenderCup"
  | "Skewer"
  | "GriddlePan"
  | "ToastingFork"
  | "HotPotPot";

/** 场景锅具 prefab id → 锅具种类（utensils/* 目录）。 */
export const UTENSIL_KIND_BY_ID: Record<string, UtensilKind> = {
  FryPan: "FryPan",
  Pot: "Pot",
  Steamer: "Steamer",
  FrierBasket: "FrierBasket",
  OvenTray: "OvenTray",
  utensil_roasting_tray: "RoastingTray",
  utensil_cake_tin_01: "OvenCakeTin",
  MixerBowl: "MixerBowl",
  BlenderCup: "BlenderCup",
  utensil_blender_01: "BlenderCup",
  Skewer: "Skewer",
  GriddlePan: "GriddlePan",
  ToastingFork: "ToastingFork",
  utensil_large_pot_01: "HotPotPot",
  web_utensil_large_pot_01: "HotPotPot",
  web_utensil_large_pot_01_pushable: "HotPotPot",
  web_utensil_dlc10_large_pot_01: "HotPotPot",
  web_dlc10_pushable_object: "HotPotPot",
};

export interface UtensilRule {
  /** 需要的台面种类（null = 自带加热，如火锅大锅直接坐灶）。 */
  station: StationClass | null;
  /** 基准制作秒数（Docs/定分：煎/煮/蒸/烤/搅 12s，炸 10s）。 */
  cookSec: number;
  /** 单锅容量（份数）。场景 cookingUtensil stub 的 capacity 优先于此默认值。 */
  capacity: number;
  /** 火力敏感（煎锅：中火 18s、无火 120s）→ 灶台不足时罚项更重。 */
  heatSensitive?: boolean;
}

export const UTENSIL_RULES: Record<UtensilKind, UtensilRule> = {
  FryPan: { station: "hob", cookSec: 12, capacity: 1, heatSensitive: true },
  Pot: { station: "hob", cookSec: 12, capacity: 3 },
  Steamer: { station: "hob", cookSec: 12, capacity: 3 },
  FrierBasket: { station: "fryStation", cookSec: 10, capacity: 3 },
  OvenTray: { station: "oven", cookSec: 12, capacity: 4 },
  RoastingTray: { station: "oven", cookSec: 12, capacity: 4 },
  OvenCakeTin: { station: "oven", cookSec: 12, capacity: 2 },
  MixerBowl: { station: "mixer", cookSec: 12, capacity: 3 },
  BlenderCup: { station: "blender", cookSec: 12, capacity: 3 },
  Skewer: { station: "barbeque", cookSec: 12, capacity: 3 },
  GriddlePan: { station: "campfire", cookSec: 12, capacity: 2, heatSensitive: true },
  ToastingFork: { station: "campfire", cookSec: 12, capacity: 2, heatSensitive: true },
  HotPotPot: { station: "hotpotBurner", cookSec: 12, capacity: 6 },
};

/** 菜谱 cookingStep id（Pot/FryingPan/…）→ 锅具种类。 */
export const STEP_TO_UTENSIL: Record<string, UtensilKind> = {
  Pot: "Pot",
  FryingPan: "FryPan",
  DeepFatFryer: "FrierBasket",
  OvenTray: "OvenTray",
  Steamer: "Steamer",
  Mixer: "MixerBowl",
  MixingBowl: "MixerBowl",
  Blender: "BlenderCup",
  GriddlePan: "GriddlePan",
  KebabSkewer: "Skewer",
  ToastingFork: "ToastingFork",
  HotPot: "HotPotPot",
  RoastingTray: "RoastingTray",
  OvenCakeTin: "OvenCakeTin",
};

/** 锅具种类 → 中文（UI 摘要用）。 */
export const UTENSIL_KIND_ZH: Record<UtensilKind, string> = {
  FryPan: "煎锅",
  Pot: "汤锅",
  Steamer: "蒸笼",
  FrierBasket: "炸篮",
  OvenTray: "烤箱盘",
  RoastingTray: "烤盘",
  OvenCakeTin: "蛋糕模",
  MixerBowl: "搅拌碗",
  BlenderCup: "搅拌杯",
  Skewer: "烤串签",
  GriddlePan: "煎烤盘",
  ToastingFork: "烘烤叉",
  HotPotPot: "火锅大锅",
};

// ---------- 切配规则（断头台 vs 切菜台） ----------

/** 切菜台：交互按住切完一个食材（Docs/定分：切菜 3 秒）。 */
export const CHOP_COUNTER_RULE = {
  perItemSec: 3,
  capacity: 1,
};

/** 断头台：需按钮触发落刀；一次可切 2 个、切速 1 秒。
 *  有效单件耗时 = (perCycleSec + buttonOverheadSec) / capacity ≈ 1.75s/个。 */
export const GUILLOTINE_RULE = {
  perCycleSec: 1,
  capacity: 2,
  /** 每批按钮开销：跑去按/协调一名玩家按压的固定时间。 */
  buttonOverheadSec: 2.5,
};

/** 断头台有效单件切时（含按钮开销摊派）。 */
export function guillotinePerItemSec(): number {
  return (GUILLOTINE_RULE.perCycleSec + GUILLOTINE_RULE.buttonOverheadSec) / GUILLOTINE_RULE.capacity;
}

// ---------- 洗盘 / 餐具 ----------

/** Docs/定分：洗盘子 3 秒；脏盘 10 秒周期自动回盘（节奏参数仍用官方拟合 PLATE_RETURN_SEC=7）。 */
export const WASH_PER_ITEM_SEC = 3;
export const DIRTY_RETURN_SEC = 10;
/** 从餐具堆取一只盘/杯的耗时。 */
export const PLATE_TAKE_SEC = 1.5;
/** 放上上菜台的动作耗时。 */
export const SERVE_ACTION_SEC = 1;
/** 从食材箱取出一份食材的耗时。 */
export const PICKUP_SEC = 0.5;

// ==================== ③ 需切食材识别 ====================

/** 显式 chopped 变体：菜谱直接引用切好的食材（如 DLC02_ChoppedBun）。 */
export const CHOP_INGREDIENT_PATTERN = /chopp?ed/i;

/** 整料 → 需先切再用的精选表（依据官方玩法；可在 UI 参数面板核对后增删）。
 *  - 生菜/番茄/洋葱/蘑菇：沙拉、披萨、汤的底料，先切后用
 *  - 黄瓜/寿司鱼：寿司配菜切条
 *  - 胡萝卜/土豆（含 dlc07）：蛋糕丝/薯条需切
 *  - dlc11_onion_salad：repo 文档确证「切 8 刀变 ChoppedOnion_Salad」
 *  注意 MeatSO（肉饼直接下锅）、DLC05_Dough、香蕉/西瓜（搅拌机整投）不切。 */
export const CHOP_WHOLE_IDS = new Set([
  "LettuceSO",
  "TomatoSO",
  "OnionSO",
  "MushroomSO",
  "CucumberSO",
  "SushiFishSO",
  "CarrotSO",
  "PotatoSO",
  "dlc07_potato",
  "dlc11_onion_salad",
]);

/** 食材是否需要切配（chopped 变体或精选整料）。 */
export function ingredientNeedsChop(id: string): boolean {
  return CHOP_INGREDIENT_PATTERN.test(id) || CHOP_WHOLE_IDS.has(id);
}

// ==================== ④ 步行与风险参数 ====================

/** 玩家步行速度（格/秒；1 格 = 1.2m ≈ 官方快跑 3m/s 量级）。 */
export const WALK_CELLS_PER_SEC = 3.0;

/** 锅具排队能力：同类锅具数量少于并发需求（≈人数）时的罚项斜率。 */
export const QUEUE_PENALTY = 0.15;

/** 风险乘数（作用于单菜总耗时）：
 *  明火灶/风雪/步道/传送带拖慢节奏，传送门略提速。整体夹在 [0.95, 1.25]。 */
export const HAZARD_PER_UNIT = {
  burner: 0.02,
  wind: 0.02,
  travelator: 0.01,
  conveyor: 0.01,
  teleportal: -0.02,
};
export const HAZARD_MULT_MIN = 0.95;
export const HAZARD_MULT_MAX = 1.25;

/** 总校准：把分解模型锚定到旧拟合模型（食材×9.2s+步数×10.6s+5.1s，官方 28 图拟合）
 *  在标准官方厨房（分区步行 3~5 格）下的量级。调低 → 分数整体偏高。 */
export const MODEL_CALIBRATION = 1.6;

/** 模型参数只读摘要（弹窗「模型参数」折叠区展示）。 */
export function modelParamsSummary(): Array<{ key: string; value: string }> {
  return [
    { key: "取材", value: `${PICKUP_SEC}s/份 + 分区步行 ÷ ${WALK_CELLS_PER_SEC}格/s` },
    { key: "切菜台", value: `${CHOP_COUNTER_RULE.perItemSec}s/个 ×${CHOP_COUNTER_RULE.capacity}` },
    {
      key: "断头台",
      value: `${GUILLOTINE_RULE.perCycleSec}s/批 ÷${GUILLOTINE_RULE.capacity}个 + 按钮 ${GUILLOTINE_RULE.buttonOverheadSec}s（有效 ${guillotinePerItemSec().toFixed(2)}s/个）`,
    },
    { key: "锅具基准", value: "煎/煮/蒸/烤/搅 12s · 炸 10s（场景 stub 配置优先）" },
    { key: "洗盘", value: `${WASH_PER_ITEM_SEC}s/件 · 脏盘回 ${DIRTY_RETURN_SEC}s · 取盘 ${PLATE_TAKE_SEC}s` },
    { key: "排队罚项", value: `同类锅具不足并发需求时 ×(1+${QUEUE_PENALTY}×缺口比)` },
    {
      key: "风险乘数",
 value: `明火+${HAZARD_PER_UNIT.burner} 风雪+${HAZARD_PER_UNIT.wind} 步道+${HAZARD_PER_UNIT.travelator} 传送带+${HAZARD_PER_UNIT.conveyor} 传送门${HAZARD_PER_UNIT.teleportal}，夹 [${HAZARD_MULT_MIN}, ${HAZARD_MULT_MAX}]`,
    },
    { key: "总校准", value: String(MODEL_CALIBRATION) },
  ];
}
