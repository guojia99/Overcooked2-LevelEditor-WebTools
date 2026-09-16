const FALLBACK_ZH: Record<string, string> = {
  barrier_rope_2unit_01: "绳索护栏（2格）",
  m_raft_barrier_rope_01: "木筏绳索护栏",
  Beach_GroundParrot_Purple: "海滩鹦鹉（紫）",
  Beach_GroundParrot_Red: "海滩鹦鹉（红）",
  beachdoodads_pebbles_01: "海滩鹅卵石 1",
  beachdoodads_pebbles_02: "海滩鹅卵石 2",
  beachdoodads_pebbles_03: "海滩鹅卵石 3",
  beachdoodads_starfish_01: "海星 1",
  beachdoodads_starfish_02: "海星 2",
  City_path_side_01: "城市路缘",
  "City_restaurant_table&chairs_01": "餐厅桌椅",
  city_wall_straight_05: "城市直墙 05",
  Citywall_brick_01c: "城市砖墙 01c",
  Citywall_wallpaper_01: "城市墙纸墙",
  Citywall_wallpaper_corner_in_01: "城市墙纸内角墙",
  crate_raft_01: "木筏板条箱",
  crate_raft_x2_01: "木筏板条箱（双格）",
  exterior_tree_pink_01: "粉花树",
  m_sp_cliff_corner_out_01: "悬崖外转角",
  m_sp_cliff_edge_01: "悬崖边缘 1",
  m_sp_cliff_edge_02: "悬崖边缘 2",
  m_sp_cliff_edge_05: "悬崖边缘 5",
  m_sp_cliff_edge_06: "悬崖边缘 6",
  noripple_m_dlc3_icecliff_270: "冰崖转角 270°",
  noripple_m_dlc3_icecliff_90: "冰崖转角 90°",
  noripple_m_dlc3_icecliff_straight_01: "冰崖直边 1",
  noripple_m_dlc3_icecliff_straight_02: "冰崖直边 2",
  p_dlc07_shield_fork_spoon_01: "刀叉勺盾饰",
  p_dlc5_cabin_roof_01: "小屋屋顶",
  p_dlc5_log_stairs_01: "原木楼梯",
  "p_dlc5_moonshaft_01 (4)": "月光井",
  p_dlc5_twig_pile_01: "树枝堆",
  pillar_middle_01: "中段立柱",
  pool_corner_01: "泳池转角 1",
  "pool_corner_01 1": "泳池转角 1'",
  pool_corner_02: "泳池转角 2",
  "pool_corner_02 1": "泳池转角 2'",
  pool_straight_01: "泳池直边",
  "pool_straight_01 1": "泳池直边'",
  pool_wall_straight_01: "泳池直墙",
  raft_leaves_01: "木筏树叶 1",
  raft_leaves_02: "木筏树叶 2",
  raft_leaves_03: "木筏树叶 3",
  raft_leaves_04: "木筏树叶 4",
  raft_leaves_05: "木筏树叶 5",
  raft_raft_front_01: "木筏前端",
  raft_raft_middle_01: "木筏中段 1",
  raft_raft_middle_02: "木筏中段 2",
  raft_raft_middle_03: "木筏中段 3",
  Resort_wall_end_01: "度假村墙尽头",
  Resort_wall_out_01: "度假村墙外角",
  Resort_wall_straight_01: "度假村直墙",
  Resort_wall_window_01: "度假村窗墙",
  rooftiles_straight_01: "直瓦屋顶",
  SeaShore_PFX_02: "海岸特效",
  Space_Door_Airlock_Bool_Open: "太空气闸门",
  sushi_sign_02: "寿司招牌 2",
  sushi_sign_03: "寿司招牌 3",
  "TropicalBirds(noAudio)": "热带鸟群（无音效）",
  umbrella_open_01: "遮阳伞",
  walkway_pillars_01: "栈道立柱",
  walkway_rope_02: "栈道绳索",
  wall_270_01: "270° 转角墙",
  wall_brick_01a: "砖墙 01a",
  wall_brick_01b: "砖墙 01b",
  wall_brick_01d: "砖墙 01d",
  wall_brick_out_01: "砖墙外角",
  wall_brick_window_03: "砖墙窗 03",
  Wall_straight_top_02: "直墙顶沿 02",
};

const NPC_PERSONA_ZH: Record<string, string> = {
  Alien: "外星人",
  Asian: "亚裔",
  Beard: "大胡子",
  Dora: "朵拉",
  DoraBlonde: "金发朵拉",
  Ginger: "姜发",
  Hispanic: "拉丁裔",
  MiddleEastern: "中东",
  MiddleEatern: "中东",
  Mike: "迈克",
  Specs: "眼镜",
  WizBoy: "巫师男孩",
};

const NPC_VARIANT_ZH: Record<string, string> = {
  Blue: "蓝",
  Brown: "棕",
  Green: "绿",
  Orange: "橙",
  Yellow: "黄",
  Pink: "粉",
  Civ: "便装",
  Construction: "施工",
  HardHat: "安全帽",
  Waiter: "服务生",
  Wizard: "巫师",
};

function fallbackNameZh(id: string): string | null {
  const direct = FALLBACK_ZH[id];
  if (direct) return direct;
  const m = /^NPC_([A-Za-z]+)_([A-Za-z]+)_\d+$/.exec(id);
  if (m) {
    const persona = NPC_PERSONA_ZH[m[1]] ?? m[1];
    const variant = NPC_VARIANT_ZH[m[2]] ?? m[2];
    return `顾客·${persona}（${variant}）`;
  }
  return null;
}

/** Strip manual-table variant counts: "城市砖墙 ×6" → "城市砖墙".
 *  When the name is untranslated (=== id), fall back to the built-in zh table. */
export function tidyCatalogNameZh(name: string, id?: string): string {
  if (id && name === id) {
    const zh = fallbackNameZh(id);
    if (zh) return zh;
  }
  return name.replace(/\s*[×xX]\d+\s*$/u, "").trim();
}
