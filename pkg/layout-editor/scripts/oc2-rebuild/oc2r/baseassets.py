"""本体关卡集 oc2_story 的关卡资产生成（LevelInfo / config_1p~4p / LevelSetInfo）。

数据来源全部是权威值：
  * 关卡配置   dump_bundle/Assets/resources/datafile/levelconfigs/**/<场景名>_<N>p.json
  * 星级       coopgamescenedirectory 的 m_PCStarBoundaries
  * 菜谱       配置里的 RecipeList -> OrderDefinition -> 编辑器菜谱 SO
  * 音乐       源场景 AudioManager.m_inLevelMusic 指向的 wav 名 -> audio-catalog
  * 环境音     源场景 AudioManager.m_inLevelAmbiences 原样搬运
"""

import glob
import hashlib
import json
import os
import re

from . import paths
from . import scene_graph as SG
from . import structure as ST
from . import yamlscene as Y

GUID_LEVEL_INFO = "9613355741a1a7e429f1ad97a816f6de"
GUID_LEVEL_CONFIG = "29722fa34ea4be545b2b4160dc3b3f12"
GUID_LEVEL_SET_INFO = "f6eb6fcdbbf220346afc607651d1ab93"

CONFIG_DIRS = [
    os.path.join(paths.DUMP, "Assets", "resources", "datafile", "levelconfigs"),
    os.path.join(paths.DUMP, "Assets", "data", "datafile", "levelconfigs"),
]

META_TMPL = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

CONFIG_TMPL = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {{fileID: 0}}
  m_PrefabInternal: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: {script}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier: 
  orderLifeTime: {order_life}
  timeBetweenOrders: {time_between}
  plateReturnTime: {plate_return}
  survivalTimeMultiplier: 1
  roundTime: {round_time}
  m_OneStarScore: {one}
  m_TwoStarScore: {two}
  m_ThreeStarScore: {three}
  m_FourStarScore: {four}
"""


def det_guid(set_name, rel):
    return hashlib.md5(("oc2rebuild:%s/%s" % (set_name, rel)).encode("utf-8")).hexdigest()


def fmt_num(v):
    if isinstance(v, float) and v == int(v):
        return str(int(v))
    return str(v)


_config_index = None


def config_index():
    """配置 json 的 basename(小写) -> 路径。"""
    global _config_index
    if _config_index is None:
        idx = {}
        for d in CONFIG_DIRS:
            for p in glob.glob(os.path.join(d, "**", "*.json"), recursive=True):
                idx.setdefault(os.path.basename(p)[:-5].lower(), p)
        _config_index = idx
    return _config_index


def find_config(scene_name, pc):
    """按名字猜配置文件（兜底用；优先走 pathID 精确解析）。"""
    stem = re.sub(r"^s_", "", scene_name or "", flags=re.I).lower()
    cand = ["%s_%dp" % (stem, pc), "%s_%dp" % (stem.replace("-", "_"), pc),
            "%dp_%s" % (pc, stem), "%ss_%dp" % (stem, pc)]
    idx = config_index()
    for c in cand:
        if c in idx:
            return idx[c]
    for k, v in idx.items():
        if k.endswith("_%dp" % pc) and k[: -len("_%dp" % pc)].rstrip("s_") == stem.rstrip("s_"):
            return v
    return None


def config_by_pid(pid, pidmaps):
    """按 LevelConfig 的 pathID 精确定位 dump 出来的配置 json。"""
    if not pid:
        return None
    _b, c = pidmaps.lookup(pid, prefer=level_config_bundles(),
                           accept=lambda x: "levelconfig" in x.lower())
    if not c:
        return None
    return _dump_path(c)


_lc_bundles = None


def level_config_bundles():
    global _lc_bundles
    if _lc_bundles is None:
        with open(paths.DUMP_MANIFEST, encoding="utf-8") as f:
            m = json.load(f)
        out = []
        for o in m["objects"]:
            c = (o.get("container") or "").lower()
            if "levelconfig" in c and "/downloadablecontent/" not in c:
                if o["bundle"] not in out:
                    out.append(o["bundle"])
        _lc_bundles = out
    return _lc_bundles


def load_config(path):
    with open(path, encoding="utf-8") as f:
        d = json.load(f)
    if isinstance(d, dict) and isinstance(d.get("MonoBehaviour"), dict):
        return d["MonoBehaviour"]
    return d


def round_time_of(cfg):
    rt = sum(r.get("m_roundTimer", 0) or 0 for r in (cfg.get("m_rounds") or []))
    if not rt:
        data = cfg.get("m_data")
        if isinstance(data, dict):
            rt = data.get("m_roundTimer", 0) or 0
    if not rt:
        waves = cfg.get("m_waves")
        if isinstance(waves, dict):
            rt = waves.get("m_totalTime", 0) or 0
    return rt


# ---------------------------------------------------------------------------
# 音频
# ---------------------------------------------------------------------------

_audio = None


def audio_catalog():
    global _audio
    if _audio is None:
        with open(paths.repo("layout-editor/web/public/audio-catalog.json"),
                  encoding="utf-8") as f:
            _audio = json.load(f)
    return _audio


# 本体主题 -> 关卡音乐 SO（动态关卡的 dynamic_stage_XX_phase_XX 没有独立 SO，按主题兜底）
MUSIC_BY_THEME = {
    "sushi": "TheNeonCitySO",
    "city": "CityLivingSO",
    "wizard": "SpellboundSO",
    "balloon": "Up&AwaySO",
    "mine": "TheMineSO",
    "rapids": "DownTheRiverSO",
    "space": "OuterSpaceSO",
    "dynamic": "Up&AwaySO",
    "movingplatform": "CityLivingSO",
    "tutorial": "TheNeonCitySO",
}


def audio_for_scene(sc, resolver, theme=""):
    """从源场景的 AudioManager 读音乐与环境音，落到编辑器音频 SO。"""
    ac = audio_catalog()
    music = {}
    for x in ac["music"]:
        mid = x["id"].lower()
        music.setdefault(mid, x)
        if mid.endswith("so"):
            music.setdefault(mid[:-2], x)
    dirs = {x["id"]: x for x in ac["audioDirectories"]}
    music_guid = ""
    amb = ""
    bundles = set()
    warns = []

    mgr = None
    for n in sc.iter_nodes():
        if n.name.strip().lower() == "audiomanager":
            mgr = n
            break
    if mgr is not None:
        for cls, d in mgr.comps:
            if cls != SG.CLS_MONOBEHAVIOUR:
                continue
            data = d.data
            if "m_inLevelMusic" not in data:
                continue
            g = Y.ref_guid(data.get("m_inLevelMusic") or {})
            rel = resolver.ar_path(g) if g else ""
            if rel:
                stem = os.path.splitext(os.path.basename(rel))[0].lower()
                hit = music.get(stem)
                if hit:
                    music_guid = hit["guid"]
                    bundles.add(hit["bundleName"])
                else:
                    warns.append("音乐 %s 没有编辑器 SO" % stem)
            a = data.get("m_inLevelAmbiences")
            if isinstance(a, str):
                amb = a
            break
    if not music_guid:
        fid = MUSIC_BY_THEME.get((theme or "").lower())
        hit = music.get((fid or "").lower())
        if hit:
            music_guid = hit["guid"]
            bundles.add(hit["bundleName"])
            warns.append("音乐按主题兜底为 %s" % fid)
        else:
            warns.append("未解析到关卡音乐")

    dir_guids = []
    for did in ac["mandatoryDirectoryIds"]:
        x = dirs.get(did)
        if x and x["guid"] not in dir_guids:
            dir_guids.append(x["guid"])
            bundles.add(x["bundleName"])
    return music_guid, dir_guids, amb, bundles, warns


# ---------------------------------------------------------------------------
# 菜谱
# ---------------------------------------------------------------------------

_recipe_index = None


def recipe_index(resolver):
    """游戏内 order/recipe 容器 basename -> 编辑器菜谱 SO {guid,bundleName}。

    两类来源：
      * DLC：pseudo_prefab_so 里 assetPath 带 /orderdefinitions/ 的 SO
      * 本体：Assets/common01/food/Recipes/*.asset（PseudoPrefabSORecipe），
        id 形如 Sushi_Fish_SO，对应游戏容器 sushi_fish
    """
    global _recipe_index
    if _recipe_index is not None:
        return _recipe_index
    idx = {}
    for g, info in resolver.so.items():
        ap = (info.get("assetPath") or "").replace("\\", "/").lower()
        if "/orderdefinitions/" not in ap:
            continue
        idx.setdefault(os.path.splitext(os.path.basename(ap))[0], {
            "guid": g, "bundleName": info.get("bundleName", "")})

    # 本体菜谱：PseudoPrefabSORecipe 的 assetPath 就指向游戏的 orderdefinition 资产，
    # 按它的 basename 索引才是权威做法（editor 名 Burger_Plain_SO 对应游戏 BeefBurger）
    for g, info in (resolver.recipe_so or {}).items():
        ap = (info.get("assetPath") or "").replace("\\", "/").lower()
        if not ap:
            continue
        idx.setdefault(os.path.splitext(os.path.basename(ap))[0], {
            "guid": g, "bundleName": info.get("bundleName", "")})

    pub = paths.repo("layout-editor/web/public")
    try:
        with open(os.path.join(pub, "recipes.json"), encoding="utf-8") as f:
            top = json.load(f)
        files = list((top.get("groupFiles") or {}).values())
    except (OSError, ValueError):
        files = []
    for rel in files:
        p = os.path.join(pub, rel)
        if not os.path.exists(p):
            continue
        with open(p, encoding="utf-8") as f:
            d = json.load(f)
        for r in d.get("recipes") or []:
            rid = (r.get("id") or "").lower()
            keys = {rid}
            if rid.endswith("_so"):
                keys.add(rid[:-3])
            ap = (r.get("assetPath") or "").lower()
            if ap:
                base = os.path.splitext(os.path.basename(ap))[0]
                keys.add(base)
                if base.endswith("_so"):
                    keys.add(base[:-3])
            for k in keys:
                idx.setdefault(k, {"guid": r["guid"], "bundleName": ""})
    _recipe_index = idx
    return _recipe_index


class PidMaps(object):
    """pathID -> container 的按 bundle 缓存（复用 oc2-import 已建好的缓存）。"""

    def __init__(self):
        self.dirs = [paths.cache(), paths.repo("layout-editor/scripts/oc2-import/.cache")]
        self.maps = {}
        self._cached_bundles = None

    def cached_bundles(self):
        """磁盘上已有 pidmap 缓存的 bundle 列表（免费可搜）。"""
        if self._cached_bundles is None:
            names = []
            for d in self.dirs:
                if not os.path.isdir(d):
                    continue
                for fn in os.listdir(d):
                    if fn.startswith("pidmap_") and fn.endswith(".json"):
                        b = fn[len("pidmap_"):-len(".json")]
                        if b not in names:
                            names.append(b)
            names.sort(key=lambda s: int(s[6:]) if s[6:].isdigit() else 0)
            self._cached_bundles = names
        return self._cached_bundles

    def lookup(self, pid, prefer=(), accept=None):
        """在候选 bundle 里找 pid；prefer 先查，再查所有已缓存的 bundle。

        accept(container) 可选过滤；返回 (bundle, container) 或 (None, None)。
        """
        order = list(prefer) + [b for b in self.cached_bundles() if b not in prefer]
        for b in order:
            c = self.get(b).get(pid)
            if c and (accept is None or accept(c)):
                return b, c
        return None, None

    def get(self, bundle):
        if bundle in self.maps:
            return self.maps[bundle]
        for d in self.dirs:
            p = os.path.join(d, "pidmap_%s.json" % bundle)
            if os.path.exists(p):
                with open(p, encoding="utf-8") as f:
                    m = json.load(f)
                self.maps[bundle] = {int(k): v for k, v in m.items()}
                return self.maps[bundle]
        m = self._build(bundle)
        self.maps[bundle] = m
        return m

    def _build(self, bundle):
        try:
            import UnityPy
        except ImportError:
            return {}
        p = os.path.join(paths.STREAMING, bundle)
        if not os.path.exists(p):
            return {}
        out = {}
        try:
            env = UnityPy.load(p)
            for obj in env.objects:
                c = getattr(obj, "container", None)
                pid = getattr(obj, "path_id", None)
                if c and pid is not None:
                    out[pid] = c
        except Exception:  # noqa: BLE001
            return {}
        with open(paths.cache("pidmap_%s.json" % bundle), "w", encoding="utf-8") as f:
            json.dump({str(k): v for k, v in out.items()}, f)
        return out


_base_bundles = None


def base_bundles():
    """本体（非 DLC）内容所在 bundle。"""
    global _base_bundles
    if _base_bundles is None:
        with open(paths.DUMP_MANIFEST, encoding="utf-8") as f:
            m = json.load(f)
        out = []
        for o in m["objects"]:
            c = o.get("container") or ""
            if "/downloadablecontent/" in c:
                continue
            if re.search(r"recipes?lists?|orderdefinitions|recipeitems", c, re.I):
                if o["bundle"] not in out:
                    out.append(o["bundle"])
        _base_bundles = out
    return _base_bundles


def resolve_recipes(cfg, resolver, pidmaps):
    """配置 -> 菜谱 SO guid 列表。"""
    warns = []
    bundles = set()
    guids = []
    ridx = recipe_index(resolver)

    rl_pids = []
    for rnd in cfg.get("m_rounds") or []:
        pid = (rnd.get("m_recipes") or {}).get("fileID", 0)
        if pid:
            rl_pids.append(pid)
    data = cfg.get("m_data")
    if isinstance(data, dict):
        pid = (data.get("m_recipes") or {}).get("fileID", 0)
        if pid:
            rl_pids.append(pid)
        for ph in data.get("Phases", []) or []:
            pid = (ph.get("Recipes") or {}).get("fileID", 0)
            if pid:
                rl_pids.append(pid)
    waves = cfg.get("m_waves")
    if isinstance(waves, dict):
        for w in waves.get("m_waves", []) or []:
            pid = (w.get("m_recipes") or {}).get("fileID", 0)
            if pid:
                rl_pids.append(pid)

    search = base_bundles()
    seen = set()
    for pid in rl_pids:
        # RecipeList 不一定放在名字带 recipelist 的容器里
        # （本体最终关的分阶段菜谱表就存在 levelconfigs/bosslevel/ 下），
        # 因此先按名字找，找不到再放宽成"dump 出来的 json 里有 m_recipes"。
        bundle, container = pidmaps.lookup(
            pid, prefer=search,
            accept=lambda c: bool(re.search(r"recipes?lists?", c, re.I)))
        if not container:
            bundle, container = pidmaps.lookup(
                pid, prefer=search, accept=lambda c: _has_recipes(c))
        if not container:
            warns.append("RecipeList pid %s 未解析" % pid)
            continue
        if container in seen:
            continue
        seen.add(container)
        rl = _load_dump(container)
        if rl is None:
            warns.append("缺 RecipeList dump %s" % container)
            continue
        order_search = [bundle] + [b for b in search if b != bundle]
        for entry in rl.get("m_recipes", []) or []:
            opid = (entry.get("m_order") or {}).get("fileID", 0)
            _ob, oc = pidmaps.lookup(opid, prefer=order_search)
            if not oc:
                warns.append("order pid %s 未解析" % opid)
                continue
            base = os.path.splitext(os.path.basename(oc))[0]
            r = ridx.get(base.lower())
            if not r:
                warns.append("菜谱 %s 无编辑器 SO" % base)
                continue
            if r["guid"] not in guids:
                guids.append(r["guid"])
                if r["bundleName"]:
                    bundles.add(r["bundleName"])
    return guids, bundles, warns


def _dump_path(container):
    rel = "Assets/" + container.replace("assets/", "", 1)
    p = os.path.splitext(os.path.join(paths.DUMP, rel))[0] + ".json"
    if os.path.exists(p):
        return p
    p2 = os.path.join(paths.DUMP, rel) + ".json"
    return p2 if os.path.exists(p2) else None


def _load_dump(container):
    p = _dump_path(container)
    if not p:
        return None
    try:
        return load_config(p)
    except (OSError, ValueError):
        return None


def _has_recipes(container):
    d = _load_dump(container)
    if not isinstance(d, dict):
        return False
    r = d.get("m_recipes")
    return isinstance(r, list) and bool(r) and isinstance(r[0], dict) and "m_order" in r[0]


# ---------------------------------------------------------------------------


def write_asset(path, content, guid):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)
    if not os.path.exists(path + ".meta"):
        with open(path + ".meta", "w", encoding="utf-8") as f:
            f.write(META_TMPL.format(guid=guid))


def level_display_name(level):
    lid = level["id"]
    if lid.endswith("_Tutorial"):
        return "0-0"
    m = re.search(r"_H(\d+)$", lid)
    if m:
        return "K-%d" % int(m.group(1))
    m = re.search(r"_(\d+)$", lid)
    if m:
        n = int(m.group(1))
        world = (n - 1) // 6 + 1
        idx = (n - 1) % 6 + 1
        return "%d-%d" % (world, idx)
    return lid


def build_level_assets(level, sc, resolver, pidmaps, bundles_from_scene, dry=False):
    set_name = level["set"]
    lid = level["id"]
    data_dir = os.path.join(paths.LEVELSETS, set_name, "data", lid)
    warns = []
    deps = set(bundles_from_scene)

    cfg_guids = {}
    cfg2 = None
    for pc in (1, 2, 3, 4):
        v = (level.get("variants") or {}).get(str(pc)) or {}
        p = config_by_pid(v.get("levelConfigPid"), pidmaps)
        if not p:
            p = find_config(v.get("sceneName") or level["sceneName"], pc)
        stars = v.get("stars") or {}
        if p:
            cfg = load_config(p)
        else:
            cfg = {}
            warns.append("缺 %dp 配置" % pc)
        if pc == 2:
            cfg2 = cfg
        g = det_guid(set_name, "%s/config_%dp.asset" % (lid, pc))
        cfg_guids[pc] = g
        text = CONFIG_TMPL.format(
            script="11500000, guid: " + GUID_LEVEL_CONFIG,
            name="config_%dp" % pc,
            order_life=fmt_num(cfg.get("m_orderLifetime", 100)),
            time_between=fmt_num(cfg.get("m_timeBetweenOrders", 10)),
            plate_return=fmt_num(cfg.get("m_plateReturnTime", 7)),
            round_time=fmt_num(round_time_of(cfg)),
            one=stars.get("one", 0) or 0, two=stars.get("two", 0) or 0,
            three=stars.get("three", 0) or 0, four=stars.get("four", 0) or 0)
        if not dry:
            write_asset(os.path.join(data_dir, "config_%dp.asset" % pc), text, g)

    recipes, rb, rw = resolve_recipes(cfg2 or {}, resolver, pidmaps)
    warns += rw
    deps |= rb

    music_guid, dir_guids, amb, ab, aw = audio_for_scene(sc, resolver,
                                                         theme=level.get("theme"))
    warns += aw
    deps |= ab

    ddp = 1
    if isinstance(cfg2, dict) and cfg2.get("m_disableDynamicParenting") is not None:
        ddp = 1 if cfg2.get("m_disableDynamicParenting") else 0

    info_guid = det_guid(set_name, "%s/LevelInfo_%s.asset" % (lid, lid))
    name = level_display_name(level)
    # 已有截图引用要保留（截图是另一条流水线灌进来的，重跑本脚本不能把它冲掉）
    screenshot = "{fileID: 0}"
    old_info = os.path.join(data_dir, "LevelInfo_%s.asset" % lid)
    if os.path.exists(old_info):
        try:
            m = re.search(r"^  screenshot: (\{[^}]*\})",
                          open(old_info, encoding="utf-8").read(), re.M)
            if m and "fileID: 0}" not in m.group(1):
                screenshot = m.group(1)
        except OSError:
            pass
    lines = [
        "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000",
        "MonoBehaviour:", "  m_ObjectHideFlags: 0",
        "  m_PrefabParentObject: {fileID: 0}", "  m_PrefabInternal: {fileID: 0}",
        "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % GUID_LEVEL_INFO,
        "  m_Name: LevelInfo_%s" % lid, "  m_EditorClassIdentifier: ",
        "  levelName: %s" % name, "  levelNameZH: %s" % name,
        "  screenshot: %s" % screenshot,
        "  sceneName: %s" % level["editorSceneName"],
    ]
    if recipes:
        lines.append("  recipes:")
        lines += ["  - {fileID: 11400000, guid: %s, type: 2}" % g for g in recipes]
    else:
        lines.append("  recipes: []")
    lines += [
        "  debugRecipeCount: 0", "  excludeStoryRecipeMatchList: 0",
        "  includeRecipeMatchLists: []", "  allIngredients: []",
        "  optionalRecipeMatchListItems: []", "  allCookingSteps: []",
        ("  inLevelMusicSO: {fileID: 11400000, guid: %s, type: 2}" % music_guid)
        if music_guid else "  inLevelMusicSO: {fileID: 0}",
        "  inLevelAmbiences: %s" % (amb or ""),
    ]
    if dir_guids:
        lines.append("  audioDirectorySOs:")
        lines += ["  - {fileID: 11400000, guid: %s, type: 2}" % g for g in dir_guids]
    else:
        lines.append("  audioDirectorySOs: []")
    lines += [
        "  disableDynamicParenting: %d" % ddp,
        "  OnDeathEffectSO: {fileID: 0}", "  onDeathEffectSO: {fileID: 0}",
    ]
    for pc in (1, 2, 3, 4):
        lines.append("  config_%dp: {fileID: 11400000, guid: %s, type: 2}"
                     % (pc, cfg_guids[pc]))
    ordered = sorted(deps, key=lambda s: (0, int(s[6:])) if re.fullmatch(r"bundle\d+", s)
                     else (1, 0))
    if ordered:
        lines.append("  dependencies:")
        lines += ["  - %s" % b for b in ordered]
    else:
        lines.append("  dependencies: []")
    text = "\n".join(lines) + "\n"
    if not dry:
        write_asset(os.path.join(data_dir, "LevelInfo_%s.asset" % lid), text, info_guid)
    return info_guid, warns, deps, recipes


SET_INFO_TMPL = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {{fileID: 0}}
  m_PrefabInternal: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name: LevelSetInfo
  m_EditorClassIdentifier: 
  levelSetName: {name_en}
  levelSetNameZH: "{name_zh}"
  author: 
  uid: {uid}
  version: 1.0.0
  levelInfos:
{infos}
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: {bundle}
  assetBundleVariant: 
"""


def write_set_info(set_name, name_en, name_zh, uid, info_guids):
    d = os.path.join(paths.LEVELSETS, set_name, "data")
    os.makedirs(d, exist_ok=True)
    infos = "\n".join("  - {fileID: 11400000, guid: %s, type: 2}" % g for g in info_guids)
    text = SET_INFO_TMPL.format(script=GUID_LEVEL_SET_INFO, name_en=name_en,
                                name_zh=name_zh, uid=uid, infos=infos)
    write_asset(os.path.join(d, "LevelSetInfo.asset"),
                text, det_guid(set_name, "data/LevelSetInfo.asset"))


def ensure_set_folders(set_name):
    root = os.path.join(paths.LEVELSETS, set_name)
    for rel, bundle in (("", "%s/info_%s" % (set_name, set_name)),
                        ("data", ""), ("scenes", "")):
        d = os.path.join(root, rel) if rel else root
        os.makedirs(d, exist_ok=True)
        meta = d + ".meta"
        if not os.path.exists(meta):
            with open(meta, "w", encoding="utf-8") as f:
                f.write(FOLDER_META.format(
                    guid=det_guid(set_name, "folder/" + (rel or ".")), bundle=bundle))


def ensure_level_folder_meta(set_name, level_id):
    d = os.path.join(paths.LEVELSETS, set_name, "data", level_id)
    os.makedirs(d, exist_ok=True)
    meta = d + ".meta"
    if not os.path.exists(meta):
        with open(meta, "w", encoding="utf-8") as f:
            f.write(FOLDER_META.format(
                guid=det_guid(set_name, "folder/data/" + level_id), bundle=""))
