#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate Chinese display names for the common03 floor materials and write
them back into:
  - layout-editor/web/public/floor-materials.json  (nameZh / nameEn)
  - layout-editor/scripts/data/names-dictionary.json (append missing ids)

Re-runnable; only touches entries whose source is common03.
"""
import json
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
CATALOG = os.path.join(ROOT, 'layout-editor', 'web', 'public', 'floor-materials.json')
NAMES = os.path.join(ROOT, 'layout-editor', 'scripts', 'data', 'names-dictionary.json')

# multi-token phrases (greedy, longest window first) -> zh
PHRASES = {
    # theme + object combos
    ('sk', 'floor', 'tile', 'sushiblue'): '共享厨房·蓝色寿司地砖',
    ('sk', 'floor', 'tile', 'sushired'): '共享厨房·红色寿司地砖',
    ('sk', 'floor', 'tile', 'sushi'): '共享厨房·寿司地砖',
    ('sk', 'floor', 'city'): '共享厨房·城市地台',
    ('sk', 'floor'): '共享厨房地板',
    ('city', 'path'): '城市小径',
    ('city', 'blacktiles'): '城市黑砖',
    ('city', 'road', 'yellow', 'box'): '城市道路黄框区',
    ('city', 'road', 'crossing'): '城市道路人行横道',
    ('city', 'road', 'centre'): '城市道路中央',
    ('city', 'road', 'crack'): '城市道路裂纹',
    ('city', 'road', 'stop'): '城市道路停止线',
    ('city', 'road'): '城市道路',
    ('city', 'rooftiles'): '城市屋顶砖',
    ('city', 'pavementdeco'): '城市人行道花纹',
    ('city', 'pavement'): '城市人行道',
    ('city', 'woodenslats'): '城市木板条',
    ('city', 'h18', 'broken', 'floor', 'cliff'): '破碎悬崖地面',
    ('city', 'wake'): '城市船迹水花',
    ('airballoon', 'floorboards'): '热气球木板地面',
    ('airballoon', 'floortile'): '热气球地砖',
    ('airballoon', 'floor', 'tile'): '热气球地砖',
    ('airballoon', 'floor'): '热气球地板',
    ('wizard', 'floorboards'): '魔法学校木板地面',
    ('wizard', 'floortile'): '魔法学校地砖',
    ('wizard', 'stonefloor'): '魔法学校石地板',
    ('wizard', 'woodfloor'): '魔法学校木地板',
    ('wizard', 'stonetile'): '魔法学校石砖',
    ('mine', 'floorboards'): '矿洞木板地面',
    ('mine', 'brokentiles'): '矿洞碎砖',
    ('mine', 'brokentiles', 'vortex'): '矿洞碎砖漩涡',
    ('mi', 'floor'): '矿洞地面',
    ('mi', 'floorfix'): '矿洞地面修补',
    ('sp', 'blacktiles'): '太空黑砖',
    ('sp', 'brokentiles', 'vortex'): '太空碎砖漩涡',
    ('sp', 'brokentiles'): '太空碎砖',
    ('sp', 'floor'): '太空地板',
    ('sp', 'tiles'): '太空瓷砖',
    ('grave', 'grass', 'floor'): '墓地草地',
    ('grave', 'kitchen', 'floor'): '墓地厨房地板',
    ('grave', 'brokentiles'): '墓地碎砖',
    ('grave', 'floor'): '墓地地板',
    ('swamp', 'grass', 'floor'): '沼泽草地',
    ('swamp', 'kitchen', 'floor'): '沼泽厨房地板',
    ('swamp', 'wake'): '沼泽水花',
    ('raft', 'floor', 'fill'): '木筏地面填充',
    ('raft', 'wake'): '木筏水花',
    ('dlc2', 'citypath'): '海滩小径',
    ('dlc2', 'cityroad'): '海滩道路',
    ('dlc2', 'city', 'path'): '海滩小径',
    ('dlc2', 'floortile'): '海滩地砖',
    ('dlc2', 'tilefloor'): '海滩瓷砖地板',
    ('dlc2', 'woodenfloor'): '海滩木地板',
    ('dlc2', 'pooltiles'): '海滩泳池砖',
    ('dlc2', 'road', 'double', 'yellow'): '海滩双黄线道路',
    ('dlc3', 'ice', 'floor'): '冬季冰面',
    ('dlc4', 'floortile'): '石径地砖',
    ('dlc4', 'stonetileback'): '石径石砖背',
    ('dlc4', 'stonetile'): '石径石砖',
    ('dlc4', 'mossfloor'): '石径苔藓地面',
    ('dlc4', 'pathedging'): '石径边缘',
    ('dlc5', 'forestfloor'): '森林地面',
    ('dlc5', 'forest', 'floor'): '森林地面',
    ('dlc5', 'ground', 'camp'): '森林营地地面',
    ('dlc5', 'ground'): '森林营地地面',
    ('dlc07', 'hiddencity', 'floor'): '隐秘之城地面',
    ('dlc07', 'ground'): '隐秘之城地面',
    ('dlc08', 'ground'): '马戏团地面',
    ('dlc11', 'pooltiles', 'marking'): '嘉年华泳池砖标线',
    ('dlc11', 'pooltiles'): '嘉年华泳池砖',
    ('dlc13', 'wooden', 'floor'): 'DLC13 木地板',
    ('dlc13', 'flloortile'): 'DLC13 地砖',
    ('lorry', 'van', 'floor'): '货车车厢地板',
    ('lorry', 'van', 'tiles'): '货车车厢瓷砖',
    ('floortiles', 'kitchen'): '厨房地砖',
    ('floortile', 'kitchen'): '厨房地砖',
    ('floortiles', 'darkpavement'): '深色人行道',
    ('floortiles', 'pavement'): '人行道地砖',
    ('floortiles', 'road', 'yellowbox'): '道路黄框区',
    ('floortiles', 'road'): '原版道路',
    ('floortiles', 'poshrestaurant'): '高档餐厅地砖',
    ('floortile', 'poshrestaurant'): '高档餐厅地砖',
    ('floortiles', 'restaurant', 'saladbar'): '餐厅沙拉吧地砖',
    ('floortiles', 'snowroad'): '雪地道路',
    ('floortiles', 'desertroad'): '沙漠道路',
    ('floortiles', 'desert'): '沙漠地砖',
    ('floortiles', 'chequered'): '格纹地砖',
    ('floortile', 'stone'): '石质地砖',
    ('hell', 'lavafloor'): '地狱岩浆地面',
    ('hell', 'kitchentile'): '地狱厨房瓷砖',
    ('magic', 'carpetfloor'): '魔法地毯地面',
    ('magic', 'stonefloor'): '魔法石地面',
    ('testchamber', 'outsidefloor'): '测试舱外地面',
    ('testchamber', 'floor'): '测试舱地面',
    ('space', 'floor'): '太空地板',
    ('onionhouse', 'floor'): '洋葱屋地板',
    ('floortiles',): '原版地砖',
    ('floortile',): '原版地砖',
    ('floorboards',): '木板地面',
    ('stonefloor',): '石地面',
    ('snowpath',): '雪径',
    ('mossfloor',): '苔藓地面',
    ('pathedging',): '小径边缘',
    ('woodenfloor',): '木地板',
    ('wooden', 'floor'): '木地板',
    ('blacktiles',): '黑砖',
    ('woodslats',): '木板条',
    ('rooftiles',): '屋顶砖',
    ('pavement',): '人行道',
    ('stonetile',): '石砖',
    ('kitchentile',): '厨房瓷砖',
    ('carpetfloor',): '地毯地面',
    ('lavafloor',): '岩浆地面',
    ('outsidefloor',): '户外地面',
    ('wake',): '水花',
    ('path',): '小径',
    ('road',): '道路',
    ('floor',): '地板',
    ('tiles',): '地砖',
    ('tile',): '地砖',
    ('ground',): '地面',
    # standalone theme names (e.g. DLC2_FloorTiles_Kitchen)
    ('dlc2',): '海滩',
    ('dlc',): 'DLC',
    ('onionhouse',): '洋葱屋',
    # modifiers
    ('corner', 'in'): '内角',
    ('corner', 'out'): '外角',
    ('yellow', 'box'): '黄框',
    ('yellowbox',): '黄框',
    ('double', 'yellow'): '双黄线',
    ('no', 'decal'): '无贴花',
    ('nodecal',): '无贴花',
    ('objectspace',): '物体空间',
    ('highrenderqueue',): '高渲染',
    ('broken',): '破碎',
    ('outside',): '户外',
    ('water',): '积水',
    ('alpha',): '透明',
    ('centre',): '中央',
    ('corner',): '转角',
    ('straight',): '直段',
    ('side',): '侧边',
    ('edge',): '边条',
    ('crossing',): '横道线',
    ('white',): '白色',
    ('rainbow',): '彩虹',
    ('stopline',): '停止线',
    ('roadcracked',): '裂纹',
    ('crack',): '裂纹',
    ('saladbar',): '沙拉吧',
    ('restaurant',): '餐厅',
    ('kitchen',): '厨房',
    ('mini', 'offset'): '小型错位',
    ('mini',): '小型',
    ('offset',): '错位',
    ('blue',): '蓝色',
    ('red',): '红色',
    ('pink',): '粉色',
    ('dark',): '深色',
    ('light',): '浅色',
    ('concrete',): '混凝土',
    ('grass',): '草地',
    ('forest',): '森林',
    ('still',): '静态',
    ('fix',): '修补',
    ('fill',): '填充',
    ('vortex',): '漩涡',
    ('outer',): '外沿',
    ('off',): '关',
    ('copy',): '副本',
    ('special',): '特殊',
    ('vs',): '对抗',
    ('a',): 'A',
    ('b',): 'B',
    ('sushi',): '寿司',
    ('camp',): '营地',
    ('desert',): '沙漠',
    ('snow',): '雪',
    ('stone',): '石质',
    ('posh',): '高档',
    ('cliff',): '悬崖',
    ('stop',): '停止线',
    ('stopline',): '停止线',
    ('throne',): '王座',
    ('dlc08',): '马戏团',
    ('pavement1',): '人行道',
}

MAX_PHRASE = max(len(k) for k in PHRASES)
NUM_RE = re.compile(r'^0*(\d+)$')
SIZETAG_RE = re.compile(r'^(\d+)x(\d+)([ab]?)$')
PAIR_RE = re.compile(r'^(\d+)[\-](\d+)$')
NUMAB_RE = re.compile(r'^0*(\d+)([ab])$')
XNUM_RE = re.compile(r'^x(\d+)$')
HVAR_RE = re.compile(r'^h(\d+)$')


def classify_token(tok):
    """-> ('num', display) for number-ish tokens, else None."""
    m = SIZETAG_RE.match(tok)
    if m:
        return 'num', m.group(1) + '×' + m.group(2) + m.group(3).upper()
    m = NUMAB_RE.match(tok)
    if m:
        return 'num', m.group(1) + m.group(2).upper()
    m = XNUM_RE.match(tok)
    if m:
        return 'num', '×' + m.group(1)
    m = PAIR_RE.match(tok)
    if m:
        return 'num', m.group(1) + '-' + m.group(2)
    m = NUM_RE.match(tok)
    if m:
        return 'num', m.group(1)
    return None


def unknown_token(tok):
    m = HVAR_RE.match(tok)
    if m:
        return '变体H' + m.group(1)
    if 'highrenderqueue' in tok:
        num = re.match(r'^0*(\d+)', tok)
        return ('%s 高渲染' % num.group(1)) if num else '高渲染'
    return tok


def translate(mid):
    n = mid.lower()
    if n.startswith('mat_'):
        n = n[4:]
    if n.startswith('dlc11_mat_'):
        n = n[10:]
    # split into tokens, drop empties / stray dashes from " - _ Copy"
    tokens = [t for t in re.split(r'_+', n) if t and t != '-']
    pieces = []  # ('zh', s) or ('num', s)
    i = 0
    while i < len(tokens):
        # try numeric first (keeps consecutive digits mergeable)
        num = classify_token(tokens[i])
        # greedy phrase window
        hit = None
        for w in range(min(MAX_PHRASE, len(tokens) - i), 0, -1):
            if tuple(tokens[i:i + w]) in PHRASES:
                hit = w
                break
        if hit and not (num and w == 1):
            pieces.append(('zh', PHRASES[tuple(tokens[i:i + hit])]))
            i += hit
            continue
        if num:
            pieces.append(num)
            i += 1
            continue
        # unknown token: heuristic fallback
        pieces.append(('zh', unknown_token(tokens[i])))
        i += 1
    # merge consecutive number pieces: "5","1" -> "5-1";  size already has ×
    merged = []
    for kind, s in pieces:
        if kind == 'num' and merged and merged[-1][0] == 'num':
            prev = merged[-1][1]
            if '×' not in prev and '×' not in s and '-' not in prev and '-' not in s:
                merged[-1] = ('num', prev + '-' + s)
            else:
                merged.append((kind, s))
        else:
            merged.append((kind, s))
    # render: zh pieces concatenated; num pieces appended with a space
    zh = ''
    trailing = []
    for kind, s in merged:
        if kind == 'zh':
            zh += s
        else:
            trailing.append(s)
    if trailing:
        zh = (zh + ' ' + ' '.join(trailing)) if zh else ' '.join(trailing)
    return zh or mid


def en_name(mid):
    return mid[4:] if mid.startswith('mat_') else mid


def main():
    with open(CATALOG, 'r', encoding='utf-8') as f:
        cat = json.load(f)
    changed = 0
    for m in cat['materials']:
        if m.get('source') != 'common03':
            continue
        zh = translate(m['id'])
        if zh != m.get('nameZh'):
            m['nameZh'] = zh
            changed += 1
        m.setdefault('nameEn', en_name(m['id']))
    with open(CATALOG, 'w', encoding='utf-8') as f:
        json.dump(cat, f, ensure_ascii=False, indent=2)
    print('catalog updated:', changed)

    with open(NAMES, 'r', encoding='utf-8') as f:
        names = json.load(f)
    have = {e['id'] for e in names['names']}
    added = 0
    for m in cat['materials']:
        if m.get('source') != 'common03':
            continue
        if m['id'] not in have:
            names['names'].append({'id': m['id'], 'zh': m['nameZh'], 'en': m['nameEn']})
            added += 1
        else:
            for e in names['names']:
                if e['id'] == m['id']:
                    e['zh'] = m['nameZh']
                    e['en'] = m['nameEn']
                    break
    names['names'].sort(key=lambda e: e['id'])
    with open(NAMES, 'w', encoding='utf-8') as f:
        json.dump(names, f, ensure_ascii=False, indent=2)
    print('names-dictionary added:', added)


if __name__ == '__main__':
    main()
