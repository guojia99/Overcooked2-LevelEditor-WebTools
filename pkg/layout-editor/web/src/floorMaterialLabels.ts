/** 实心地板材质显示名：优先 catalog 中文名，否则按 id 规则翻译（与 translate_floor_materials.py 同源）。 */

const MAT_PHRASES: Record<string, string> = {
  sk_floor_tile_sushiblue: "共享厨房·蓝色寿司地砖",
  sk_floor_tile_sushired: "共享厨房·红色寿司地砖",
  sk_floor_tile_sushi: "共享厨房·寿司地砖",
  sk_floor_city: "共享厨房·城市地台",
  sk_floor: "共享厨房地板",
  city_path: "城市小径",
  city_blacktiles: "城市黑砖",
  city_road_yellow_box: "城市道路黄框区",
  city_road_crossing: "城市道路人行横道",
  city_road_centre: "城市道路中央",
  city_road_crack: "城市道路裂纹",
  city_road_stop: "城市道路停止线",
  city_road: "城市道路",
  city_rooftiles: "城市屋顶砖",
  city_pavementdeco: "城市人行道花纹",
  city_pavement: "城市人行道",
  city_woodenslats: "城市木板条",
  city_h18_broken_floor_cliff: "破碎悬崖地面",
  city_wake: "城市船迹水花",
  airballoon_floorboards: "热气球木板地面",
  airballoon_floortile: "热气球地砖",
  airballoon_floor_tile: "热气球地砖",
  airballoon_floor: "热气球地板",
  wizard_floorboards: "魔法学校木板地面",
  wizard_floortile: "魔法学校地砖",
  wizard_stonefloor: "魔法学校石地板",
  wizard_woodfloor: "魔法学校木地板",
  wizard_stonetile: "魔法学校石砖",
  mine_floorboards: "矿洞木板地面",
  mine_brokentiles: "矿洞碎砖",
  mine_brokentiles_vortex: "矿洞碎砖漩涡",
  mi_floor: "矿洞地面",
  mi_floorfix: "矿洞地面修补",
  sp_blacktiles: "太空黑砖",
  sp_brokentiles_vortex: "太空碎砖漩涡",
  sp_brokentiles: "太空碎砖",
  sp_floor: "太空地板",
  sp_tiles: "太空瓷砖",
  grave_grass_floor: "墓地草地",
  grave_kitchen_floor: "墓地厨房地板",
  grave_brokentiles: "墓地碎砖",
  grave_floor: "墓地地板",
  swamp_grass_floor: "沼泽草地",
  swamp_kitchen_floor: "沼泽厨房地板",
  swamp_wake: "沼泽水花",
  raft_floor_fill: "木筏地面填充",
  raft_wake: "木筏水花",
  dlc2_citypath: "海滩小径",
  dlc2_cityroad: "海滩道路",
  dlc2_city_path: "海滩小径",
  dlc2_floortile: "海滩地砖",
  dlc2_tilefloor: "海滩瓷砖地板",
  dlc2_woodenfloor: "海滩木地板",
  dlc2_pooltiles: "海滩泳池砖",
  dlc2_road_double_yellow: "海滩双黄线道路",
  dlc3_ice_floor: "冬季冰面",
  dlc4_floortile: "石径地砖",
  dlc4_stonetileback: "石径石砖背",
  dlc4_stonetile: "石径石砖",
  dlc4_mossfloor: "石径苔藓地面",
  dlc4_pathedging: "石径边缘",
  dlc5_forestfloor: "森林地面",
  dlc5_forest_floor: "森林地面",
  dlc5_ground_camp: "森林营地地面",
  dlc5_ground: "森林营地地面",
  dlc07_hiddencity_floor: "隐秘之城地面",
  dlc07_ground: "隐秘之城地面",
  dlc08_ground: "马戏团地面",
  dlc11_pooltiles_marking: "嘉年华泳池砖标线",
  dlc11_pooltiles: "嘉年华泳池砖",
  dlc13_wooden_floor: "DLC13 木地板",
  dlc13_flloortile: "DLC13 地砖",
  lorry_van_floor: "货车车厢地板",
  lorry_van_tiles: "货车车厢瓷砖",
  floortiles_kitchen: "厨房地砖",
  floortile_kitchen: "厨房地砖",
  floortiles_darkpavement: "深色人行道",
  floortiles_pavement: "人行道地砖",
  floortiles_road_yellowbox: "道路黄框区",
  floortiles_road: "原版道路",
  floortiles_poshrestaurant: "高档餐厅地砖",
  floortile_poshrestaurant: "高档餐厅地砖",
  floortiles_restaurant_saladbar: "餐厅沙拉吧地砖",
  floortiles_snowroad: "雪地道路",
  floortiles_desertroad: "沙漠道路",
  floortiles_desert: "沙漠地砖",
  floortiles_chequered: "格纹地砖",
  floortile_stone: "石质地砖",
  hell_lavafloor: "地狱岩浆地面",
  hell_kitchentile: "地狱厨房瓷砖",
  magic_carpetfloor: "魔法地毯地面",
  magic_stonefloor: "魔法石地面",
  testchamber_outsidefloor: "测试舱外地面",
  testchamber_floor: "测试舱地面",
  space_floor: "太空地板",
  onionhouse_floor: "洋葱屋地板",
  floortiles: "原版地砖",
  floortile: "原版地砖",
  floorboards: "木板地面",
  stonefloor: "石地面",
  snowpath: "雪径",
  mossfloor: "苔藓地面",
  pathedging: "小径边缘",
  woodenfloor: "木地板",
  wooden_floor: "木地板",
  blacktiles: "黑砖",
  woodslats: "木板条",
  rooftiles: "屋顶砖",
  pavement: "人行道",
  stonetile: "石砖",
  kitchentile: "厨房瓷砖",
  carpetfloor: "地毯地面",
  lavafloor: "岩浆地面",
  outsidefloor: "户外地面",
  wake: "水花",
  path: "小径",
  road: "道路",
  floor: "地板",
  tiles: "地砖",
  tile: "地砖",
  ground: "地面",
  kevin_floor: "凯文地板",
  kevin: "凯文",
};

const MAT_TOKEN_ZH: Record<string, string> = {
  kevin: "凯文",
  raft: "木筏",
  airballoon: "热气球",
  city: "城市",
  path: "小径",
  sp: "太空",
  blacktiles: "黑砖",
  wizard: "魔法学校",
  stonefloor: "石地板",
  woodfloor: "木地板",
  floor: "地板",
  tile: "地砖",
  carpet: "地毯",
  sky: "天空",
  wood: "木",
  stone: "石",
  snow: "雪",
  ice: "冰",
  sand: "沙",
  alien: "外星",
  dark: "深色",
  old: "复古",
  dlc2: "海滩",
  dlc3: "冬季",
  dlc4: "石径",
  dlc5: "森林",
  dlc07: "隐秘之城",
  dlc08: "马戏团",
  dlc11: "嘉年华",
  dlc13: "DLC13",
  mi: "矿洞",
  mine: "矿洞",
  sk: "共享厨房",
  grave: "墓地",
  swamp: "沼泽",
  hell: "地狱",
  magic: "魔法",
  kitchen: "厨房",
  restaurant: "餐厅",
  brokentiles: "碎砖",
  floortiles: "地砖",
  floortile: "地砖",
  wooden: "木",
  concrete: "混凝土",
  grass: "草地",
  forest: "森林",
  desert: "沙漠",
  hiddencity: "隐秘之城",
  onionhouse: "洋葱屋",
  circus: "马戏团",
  carnival: "嘉年华",
  ni: "无贴花",
  nodecal: "无贴花",
  pfx: "粒子",
  glow: "发光",
  starscape: "星空",
  skybox: "天空盒",
};

function hasChinese(text: string): boolean {
  return /[\u4e00-\u9fff]/.test(text);
}

function normalizeMaterialId(id: string): string {
  let n = (id ?? "").toLowerCase();
  if (n.startsWith("mat_")) n = n.slice(4);
  if (n.startsWith("dlc11_mat_")) n = n.slice(10);
  return n.replace(/_tiling\d+x\d+$/i, "").replace(/_?\d+x\d+$/i, "");
}

export function isLikelyUntranslatedMaterialName(nameZh: string, id: string): boolean {
  const n = nameZh.trim();
  if (!n) return true;
  if (!hasChinese(n)) return true;
  const normId = normalizeMaterialId(id).replace(/_/g, "");
  const normZh = n.toLowerCase().replace(/\s+/g, "").replace(/×/g, "x");
  if (normZh === normId || n.toLowerCase() === id.toLowerCase()) return true;
  return false;
}

function classifyNumToken(tok: string): string | null {
  const size = tok.match(/^(\d+)x(\d+)([ab]?)$/i);
  if (size) return `${size[1]}×${size[2]}${size[3].toUpperCase()}`;
  const numAb = tok.match(/^0*(\d+)([ab])$/i);
  if (numAb) return `${numAb[1]}${numAb[2].toUpperCase()}`;
  const xNum = tok.match(/^x(\d+)$/i);
  if (xNum) return `×${xNum[1]}`;
  const pair = tok.match(/^(\d+)-(\d+)$/);
  if (pair) return `${pair[1]}-${pair[2]}`;
  const num = tok.match(/^0*(\d+)$/);
  if (num) return num[1];
  return null;
}

function tokenizeMaterialId(id: string): string[] {
  const n = normalizeMaterialId(id);
  return n.split("_").filter((t) => t.length > 0 && t !== "-");
}

/** 由材质 id 推导中文显示名（catalog 无翻译时的回退）。 */
export function translateMaterialId(id: string): { zh: string; en: string } {
  const tokens = tokenizeMaterialId(id);
  const zhPieces: string[] = [];
  const enPieces: string[] = [];
  let i = 0;
  const maxPhrase = 6;
  while (i < tokens.length) {
    let matched = 0;
    for (let w = Math.min(maxPhrase, tokens.length - i); w >= 1; w--) {
      const key = tokens.slice(i, i + w).join("_");
      if (MAT_PHRASES[key]) {
        matched = w;
        zhPieces.push(MAT_PHRASES[key]);
        enPieces.push(key.replace(/_/g, " "));
        break;
      }
    }
    if (matched) {
      i += matched;
      continue;
    }
    const num = classifyNumToken(tokens[i]);
    if (num) {
      zhPieces.push(num);
      enPieces.push(num.replace(/×/g, "x"));
      i++;
      continue;
    }
    const lower = tokens[i].toLowerCase();
    const hVar = lower.match(/^h(\d+)$/);
    if (hVar) {
      zhPieces.push(`变体H${hVar[1]}`);
      enPieces.push(lower);
    } else {
      zhPieces.push(MAT_TOKEN_ZH[lower] ?? tokens[i]);
      enPieces.push(tokens[i]);
    }
    i++;
  }
  const zh = zhPieces.join("");
  const en = enPieces.join(" ").trim() || normalizeMaterialId(id).replace(/_/g, " ");
  return { zh: zh || id, en };
}

export function materialDisplayLabel(m: {
  id: string;
  nameZh?: string;
  nameEn?: string;
}): { zh: string; en: string } {
  const fromId = translateMaterialId(m.id);
  const zh =
    m.nameZh && !isLikelyUntranslatedMaterialName(m.nameZh, m.id) ? m.nameZh.trim() : fromId.zh;
  const en = m.nameEn?.trim() || fromId.en;
  return { zh, en };
}

export function materialSearchText(m: {
  id: string;
  nameZh?: string;
  nameEn?: string;
  sizeTag?: string;
}): string {
  const { zh, en } = materialDisplayLabel(m);
  return [m.id, zh, en, m.sizeTag ?? ""].join(" ").toLowerCase();
}

/** 材质筛选：含中文时只匹配中文名；纯英文/ id 时匹配 id + 中英文。 */
export function materialMatchesSearchQuery(
  m: {
    id: string;
    nameZh?: string;
    nameEn?: string;
    sizeTag?: string;
  },
  query: string
): boolean {
  const q = query.trim();
  if (!q) return true;

  const { zh, en } = materialDisplayLabel(m);
  const id = m.id.toLowerCase();
  const enL = en.toLowerCase();
  const sizeL = (m.sizeTag ?? "").toLowerCase();
  const hasCjk = /[\u4e00-\u9fff]/.test(q);

  if (hasCjk) {
    const zhSources = [zh, m.nameZh?.trim() ?? ""].filter((s) => s.length > 0);
    const phrase = q.replace(/\s+/g, "");
    if (phrase && zhSources.some((z) => z.includes(phrase))) return true;
    const tokens = q.split(/\s+/).filter(Boolean);
    return tokens.every((token) => {
      if (/[\u4e00-\u9fff]/.test(token)) {
        return zhSources.some((z) => z.includes(token));
      }
      const t = token.toLowerCase();
      return id.includes(t) || enL.includes(t) || sizeL.includes(t);
    });
  }

  const tokens = q.toLowerCase().split(/\s+/).filter(Boolean);
  const blob = materialSearchText(m);
  return tokens.every((t) => blob.includes(t));
}
