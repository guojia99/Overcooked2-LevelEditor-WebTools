"""把"匹配到的游戏 prefab 容器"落成"编辑器可用的实例"。

编辑器素材库有两种组织方式：
  * 玩法件（counters / utensils / mechanisms / Player）：**共享 wrapper 预制体**
    （Counter.prefab / Sink.prefab / Pot.prefab …），外观由实例上覆写的
    PseudoPrefabStub.pseudoPrefabSO 决定。所有 stub 都继承 PseudoPrefabStub，
    所以任何玩法 wrapper 都能换皮。
  * 造景件（art / decor）：**一物一 wrapper**，wrapper 自带唯一的 SO。

因此：
  玩法件 -> 复用共享 wrapper + （必要时新建）外观 SO
  造景件 -> 复用已有专属 wrapper；没有就新建 wrapper + SO

wrapper/SO 的选择不写死成手工表，而是从现有素材库**学**出来：
统计"已知 SO 指向的游戏 prefab 的脚本类集合 -> 该 SO 用的 wrapper"，
新容器按脚本类集合做最近邻匹配。
"""

import hashlib
import os
import re
from collections import Counter, defaultdict

from . import indices
from . import paths

# 与 import-dlc-content.mjs 保持一致的脚本 guid
GUID_PSEUDO_SO = "0cff7c13895ab9e47a5e02d4619cc3b9"
GUID_PSEUDO_STUB = "0f66cc8b36034eb4c8eec31e1994e471"
GUID_PSEUDO_PREFAB = "d58b99f9c4313714e9c4b11f1534ae6f"

# 生成的 wrapper 预制体内部 fileID（沿用素材库模板，保持与既有资产一致）
GEN_GO = 1554132891853676
GEN_TR = 4341917854781290
GEN_STUB = 114096038827637246
GEN_PSEUDO = 114967938430519454

NEW_LIB_ROOT = "Assets/commonW1"
NEW_SUBDIR = "oc2rebuild"

DECOR_CATEGORIES = ("art", "other", "decor/food", "decor/recipes")

# 这些"装饰性"脚本不参与玩法类型判定
NOISE_SCRIPTS = {
    "RendererInfo", "RendererSceneInfo", "EditorGridSnap", "AnimatorCommunications",
    "AnimatorAudioComponent", "ForwardTriggersToParent", "AnticipateInteractionHighlight",
    "Flammable", "StaticGridLocation", "DynamicGridLocation", "InheritFromModelPrefab",
    "LabelGUI", "InstanceMaterialScroller", "TabletopConveyenceReceiver",
    "TabletopConveyenceWindReceiver", "HandlePickupReferral", "HandlePlacementReferral",
    "PlacementLayerSwapper", "PlacementCollisionSwapper", "Interactable",
}


def guid_for(kind, key):
    return hashlib.md5(("oc2rebuild:%s:%s" % (kind, key)).encode("utf-8")).hexdigest()


def _dlc_seg(container):
    m = re.search(r"/(dlc\d+)/", container.lower())
    return m.group(1) if m else "core"


def _theme_seg(container):
    """从容器目录里取一个主题段，尽量沿用素材库既有主题目录名。"""
    parts = [p for p in os.path.dirname(container).lower().split("/") if p]
    skip = {"assets", "downloadablecontent", "dlc_assets", "assets", "prefabs", "dlc_assets"}
    cand = [p for p in parts if p not in skip and not re.fullmatch(r"dlc\d+", p)]
    if not cand:
        return "misc"
    seg = cand[-1]
    return re.sub(r"[^a-z0-9_]+", "_", seg).strip("_") or "misc"


class Plan(object):
    __slots__ = ("kind", "wrapper", "wrapper_info", "so_guid", "category",
                 "default_parent", "created", "note")

    def __init__(self):
        self.kind = ""            # shared / dedicated
        self.wrapper = ""         # wrapper 预制体的工程路径
        self.wrapper_info = None  # indices.parse_wrapper_prefab 的结果
        self.so_guid = ""         # 实例上要覆写的 SO（shared 时必填）
        self.category = ""
        self.default_parent = "Art"
        self.created = []
        self.note = ""


class Placer(object):
    def __init__(self, resolver, dry_run=False):
        self.R = resolver
        self.dry_run = dry_run
        self.created_files = []
        self.created_guids = set()
        self._build_tables()
        self._cache = {}

    # ------------------------------------------------------------------
    def _build_tables(self):
        R = self.R
        # SO 按 bundle 内容器路径索引（精确到大小写不敏感的完整路径）
        self.so_by_container = defaultdict(list)
        for g, info in R.so.items():
            ap = (info.get("assetPath") or "").replace("\\", "/").lower()
            if ap:
                self.so_by_container[ap].append(g)

        # 外观 SO -> 类型 -> 共享 wrapper
        self.so_type = {}
        by_type = (R.appearances or {}).get("byType") or {}
        for t, lst in by_type.items():
            for e in lst:
                self.so_type[e["guid"]] = t
        self.type_wrapper = {}
        for t in by_type:
            p = "Assets/common01/prefabs/counters/%s.prefab" % t
            if p in R.wrappers:
                self.type_wrapper[t] = p

        # catalog：wrapper 路径 -> 分类 / 默认父节点
        self.cat_by_wrapper = {}
        for it in R.li["catalog"]:
            ap = it.get("assetPath")
            if ap:
                self.cat_by_wrapper[ap] = it

        # 学习样本：每个"已知 SO -> wrapper"都是一条样本，
        # 新容器按 (脚本类集合, 名字词元) 做最近邻。
        # 不能只按脚本类集合投票 —— countertop_01 与 countertop_corner_01 的脚本完全一样，
        # 只有名字能区分普通桌台和角落桌台。
        self.examples = []
        for g, info in R.so.items():
            wrappers = R.wrappers_for_so(g)
            t = self.so_type.get(g)
            target = None
            if t and t in self.type_wrapper:
                target = self.type_wrapper[t]
            elif wrappers:
                target = wrappers[0]
            if not target:
                continue
            cont = (info.get("assetPath") or "").lower()
            key = self._class_key_for_container(cont)
            if key is None:
                continue
            self.examples.append((key, _tokens(os.path.basename(cont)), target))
        self.learned = defaultdict(Counter)
        for key, _tok, target in self.examples:
            self.learned[key][target] += 1

    @staticmethod
    def _jaccard(a, b):
        if not a and not b:
            return 1.0
        if not a or not b:
            return 0.0
        return len(a & b) / float(len(a | b))

    def _class_key_for_container(self, container):
        ar = self._ar_of_container(container)
        if not ar:
            return None
        sh = self.R.shapes.get(ar)
        if not sh:
            return None
        cls = set()
        for g in sh.get("scripts") or []:
            c = self.R.script_class(g)
            if c and c not in NOISE_SCRIPTS:
                cls.add(c)
        return frozenset(cls)

    def _ar_of_container(self, container):
        return self.R.ar_by_container.get((container or "").lower())

    # ------------------------------------------------------------------
    def resolve(self, entry, node_hint=""):
        """entry = {'bundle':..., 'container':..., 'ar':...}"""
        container = entry["container"]
        key = container.lower()
        if key in self._cache:
            return self._cache[key]
        plan = self._resolve_uncached(entry)
        self._cache[key] = plan
        return plan

    def _resolve_uncached(self, entry):
        R = self.R
        container = entry["container"]
        low = container.lower()
        plan = Plan()

        so_guid = ""
        cands = self.so_by_container.get(low) or []
        if cands:
            so_guid = self._pick_so(cands)

        wrapper = ""
        if so_guid:
            t = self.so_type.get(so_guid)
            if t and t in self.type_wrapper:
                wrapper = self.type_wrapper[t]
            else:
                ws = R.wrappers_for_so(so_guid)
                if ws:
                    wrapper = self._pick_wrapper(ws)

        if not wrapper:
            wrapper = self._guess_wrapper(entry)

        cat = self.cat_by_wrapper.get(wrapper) or {}
        category = cat.get("category") or ""
        shared = bool(category) and category not in DECOR_CATEGORIES

        if shared:
            if not so_guid:
                so_guid = self._ensure_so(entry, category)
            plan.kind = "shared"
            plan.wrapper = wrapper
            plan.so_guid = so_guid
        else:
            # 造景件：必须有指向本容器的专属 wrapper
            dedicated = ""
            if so_guid:
                for w in R.wrappers_for_so(so_guid):
                    info = R.wrappers.get(w)
                    if info and info.get("so") == so_guid:
                        dedicated = w
                        break
            if not dedicated:
                so_guid = so_guid or self._ensure_so(entry, "art")
                dedicated = self._ensure_wrapper(entry, so_guid)
            plan.kind = "dedicated"
            plan.wrapper = dedicated
            plan.so_guid = ""     # wrapper 自带正确的 SO，无需覆写
            cat = self.cat_by_wrapper.get(dedicated) or cat
            category = cat.get("category") or "art"

        plan.wrapper_info = R.wrappers.get(plan.wrapper)
        plan.category = category
        plan.default_parent = cat.get("defaultParent") or self._default_parent(category)
        return plan

    @staticmethod
    def _default_parent(category):
        if category.startswith("counters") or category == "mechanisms" or category.startswith("workstation"):
            return "Design/Counters"
        if category.startswith("utensils") or category.startswith("equipment"):
            return "Design/Utensils"
        if category == "Player":
            return "Design/Chefs"
        return "Art"

    def _pick_so(self, cands):
        """同容器多 SO 时，优先新式库（commonW1/W2/common03）里 m_Name==prefabName 的那个。"""
        def rank(g):
            info = self.R.so_info(g) or {}
            p = info.get("path", "")
            score = 0
            if "/commonW1/" in p:
                score -= 3
            elif "/common03/" in p:
                score -= 2
            elif "/commonW2/" in p:
                score -= 1
            if self.so_type.get(g):
                score -= 5      # 外观 SO 最优先
            return (score, len(p))
        return sorted(cands, key=rank)[0]

    @staticmethod
    def _pick_wrapper(ws):
        return sorted(ws, key=lambda p: (0 if "/commonW1/" in p else 1, len(p)))[0]

    def _guess_wrapper(self, entry):
        """最近邻：脚本类集合权重 0.65，名字词元权重 0.35。

        没有任何玩法脚本的容器一律判为造景件 —— 共享 wrapper 的存在理由就是玩法脚本，
        否则像 wallblock 这种纯墙块会被拉去匹配某个同样"无脚本"的机关 wrapper。
        """
        cont = entry["container"].lower()
        key = self._class_key_for_container(cont)
        if not key:
            return ""
        tok = _tokens(os.path.basename(cont))
        best = ""
        best_score = 0.0
        for ekey, etok, target in self.examples:
            if not ekey:
                continue
            s = 0.65 * self._jaccard(key, ekey) + 0.35 * self._jaccard(tok, etok)
            if s > best_score:
                best_score = s
                best = target
        if best_score >= 0.30:
            return best
        return ""

    # ------------------------------------------------------------------
    def _ensure_so(self, entry, category):
        container = entry["container"]
        name = os.path.splitext(os.path.basename(container))[0]
        g = guid_for("so", container.lower())
        if g in self.R.so:
            return g
        dlc = _dlc_seg(container)
        if category.startswith("counters") or category == "mechanisms":
            sub = "counters"
        elif category.startswith("utensils"):
            sub = "utensils"
        else:
            sub = "art/" + _theme_seg(container)
        rel = "%s/pseudo_prefab_so/%s/%s/%s/%s.asset" % (
            NEW_LIB_ROOT, NEW_SUBDIR, dlc, sub, _safe(name))
        self._write(rel, _so_yaml(name, name, entry["bundle"], container), g)
        # 立刻并入内存索引，后续查询可见
        self.R.so[g] = {"prefabName": name, "bundleName": entry["bundle"],
                        "assetPath": container, "path": rel}
        self.so_by_container[container.lower()].append(g)
        self.R.so_by_name.setdefault(name.lower(), []).append(g)
        return g

    def _ensure_wrapper(self, entry, so_guid):
        container = entry["container"]
        name = os.path.splitext(os.path.basename(container))[0]
        g = guid_for("prefab", container.lower())
        dlc = _dlc_seg(container)
        # 路径必须沿用素材库既有约定 prefabs/<dlc|core>/art/<theme>/，
        # build-catalog.mjs 的 categorize() 靠它判定 category=art / theme，
        # 多插一层目录会全部掉进 category=other。
        rel = "%s/prefabs/%s/art/%s/%s.prefab" % (
            NEW_LIB_ROOT, dlc, _theme_seg(container), _safe(name))
        existing = self.R.wrapper_by_so.get(so_guid)
        if existing:
            return existing[0]
        self._write(rel, _wrapper_yaml(name, so_guid), g, meta_kind="prefab")
        info = {"rootGo": GEN_GO, "rootTr": GEN_TR, "name": name,
                "comps": {GUID_PSEUDO_STUB: GEN_STUB, GUID_PSEUDO_PREFAB: GEN_PSEUDO},
                "so": so_guid, "soArray": [], "soArrayFid": 0, "soFid": GEN_STUB,
                "guid": g, "id": name}
        self.R.wrappers[rel] = info
        self.R.wrapper_by_so.setdefault(so_guid, []).append(rel)
        self.cat_by_wrapper.setdefault(rel, {"category": "art", "defaultParent": "Art"})
        return rel

    # ------------------------------------------------------------------
    def _write(self, rel, text, guid, meta_kind="asset"):
        self.created_files.append(rel)
        self.created_guids.add(guid)
        if self.dry_run:
            return
        full = os.path.join(paths.REPO, rel)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        _ensure_folder_metas(os.path.dirname(rel))
        with open(full, "w", encoding="utf-8") as f:
            f.write(text)
        with open(full + ".meta", "w", encoding="utf-8") as f:
            f.write(_meta_yaml(guid, meta_kind))


def _safe(name):
    return re.sub(r'[\\/:*?"<>|]+', "_", name).strip()


_TOKEN_RE = re.compile(r"[^a-z0-9]+")


def _tokens(name):
    n = os.path.splitext(name or "")[0].lower()
    return frozenset(t for t in _TOKEN_RE.split(n) if t and not t.isdigit())


def _ensure_folder_metas(rel_dir):
    """为新建目录补 .meta（继承 commonW1 的 bundle 名，与既有目录一致）。"""
    parts = rel_dir.split("/")
    for i in range(len(parts), 1, -1):
        d = "/".join(parts[:i])
        if not d.startswith(NEW_LIB_ROOT + "/"):
            break
        full = os.path.join(paths.REPO, d)
        meta = full + ".meta"
        if os.path.isdir(full) and not os.path.exists(meta):
            g = guid_for("folder", d)
            with open(meta, "w", encoding="utf-8") as f:
                f.write(
                    "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\n"
                    "DefaultImporter:\n  externalObjects: {}\n  userData: \n"
                    "  assetBundleName: commonW1\n  assetBundleVariant: \n" % g)


def _meta_yaml(guid, kind):
    if kind == "prefab":
        return ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n"
                "  externalObjects: {}\n  mainObjectFileID: 100100000\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid)
    return ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n"
            "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid)


def _so_yaml(asset_name, prefab_name, bundle, asset_path):
    return (
        "%%YAML 1.1\n"
        "%%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_PrefabParentObject: {fileID: 0}\n"
        "  m_PrefabInternal: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
        "  m_Name: %s\n"
        "  m_EditorClassIdentifier: \n"
        "  prefabName: %s\n"
        "  bundleName: %s\n"
        "  assetPath: %s\n" % (GUID_PSEUDO_SO, asset_name, prefab_name, bundle, asset_path)
    )


def _wrapper_yaml(name, so_guid):
    return (
        "%%YAML 1.1\n"
        "%%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!1001 &100100000\n"
        "Prefab:\n"
        "  m_ObjectHideFlags: 1\n"
        "  serializedVersion: 2\n"
        "  m_Modification:\n"
        "    m_TransformParent: {fileID: 0}\n"
        "    m_Modifications: []\n"
        "    m_RemovedComponents: []\n"
        "  m_ParentPrefab: {fileID: 0}\n"
        "  m_RootGameObject: {fileID: %d}\n"
        "  m_IsPrefabParent: 1\n"
        "--- !u!1 &%d\n"
        "GameObject:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_PrefabParentObject: {fileID: 0}\n"
        "  m_PrefabInternal: {fileID: 100100000}\n"
        "  serializedVersion: 5\n"
        "  m_Component:\n"
        "  - component: {fileID: %d}\n"
        "  - component: {fileID: %d}\n"
        "  - component: {fileID: %d}\n"
        "  m_Layer: 0\n"
        "  m_Name: %s\n"
        "  m_TagString: Untagged\n"
        "  m_Icon: {fileID: 0}\n"
        "  m_NavMeshLayer: 0\n"
        "  m_StaticEditorFlags: 0\n"
        "  m_IsActive: 1\n"
        "--- !u!4 &%d\n"
        "Transform:\n"
        "  m_ObjectHideFlags: 1\n"
        "  m_PrefabParentObject: {fileID: 0}\n"
        "  m_PrefabInternal: {fileID: 100100000}\n"
        "  m_GameObject: {fileID: %d}\n"
        "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
        "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
        "  m_LocalScale: {x: 1, y: 1, z: 1}\n"
        "  m_Children: []\n"
        "  m_Father: {fileID: 0}\n"
        "  m_RootOrder: 0\n"
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n"
        "--- !u!114 &%d\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 1\n"
        "  m_PrefabParentObject: {fileID: 0}\n"
        "  m_PrefabInternal: {fileID: 100100000}\n"
        "  m_GameObject: {fileID: %d}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
        "  m_Name: \n"
        "  m_EditorClassIdentifier: \n"
        "  pseudoPrefabSO: {fileID: 11400000, guid: %s, type: 2}\n"
        "--- !u!114 &%d\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 1\n"
        "  m_PrefabParentObject: {fileID: 0}\n"
        "  m_PrefabInternal: {fileID: 100100000}\n"
        "  m_GameObject: {fileID: %d}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
        "  m_Name: \n"
        "  m_EditorClassIdentifier: \n"
        "  childGameObject: {fileID: 0}\n"
        % (GEN_GO, GEN_GO, GEN_TR, GEN_STUB, GEN_PSEUDO, name,
           GEN_TR, GEN_GO, GEN_STUB, GEN_GO, GUID_PSEUDO_STUB, so_guid,
           GEN_PSEUDO, GEN_GO, GUID_PSEUDO_PREFAB)
    )
