"""自顶向下的物件识别。

与上一版的根本差异：
  * 匹配对象是**游戏完整 prefab 清单（manifest 4870 个）**，不是编辑器素材库的子集；
  * 同名多候选用**子树结构签名**（子节点名 + mesh guid）消歧，不再取遍历顺序第一个；
  * 未命中的叶子不再静默丢弃，而是进入"模型/裸网格"通道，最后仍未解决的会被逐条记录。
"""

import os
import re
from collections import Counter

from . import scene_graph as SG
from . import yamlscene as Y

# 整棵子树跳过（连同后代）
SKIP_SUBTREE_EXACT = {
    "light probes",
    "lightprobes",
    "reflection probes",
    "reflectionprobes",
    "mesh baker",
    "mesh bakers",
    "meshbaker",
    "navmesh",
}
SKIP_SUBTREE_RE = re.compile(
    r"^(combinedmesh[-_].*|lightmap_-?\d+_baker|.*_lightmapbaker)$", re.I
)

# 这些名字只跳过自身（仍然继续往下走）
SKIP_SELF_EXACT = {
    "attachpoint",
    "attach point",
    "attach",
    "worktop",
    "pivot",
    "transform",
    "group",
}

FX_NAME_RE = re.compile(r"^(pfx[_-]|fx[_-]|vfx[_-])", re.I)

# mesh baker / 静态合批产物：与散件造景重复，整棵丢弃
BAKED_NAME_RE = re.compile(r"^(combined\s+floor\s+tile|combinedmesh|combined mesh)", re.I)

_TOKEN_SPLIT = re.compile(r"[^A-Za-z0-9]+|(?<=[a-z0-9])(?=[A-Z])")

# 这些名字毫无信息量，绝不能只凭名字认 prefab
# （dlc02 里真的有个 gameobject.prefab，227 个 GameObject，会把整片场景吞掉）
GENERIC_NAMES = {
    "gameobject", "game object", "new game object", "cube", "sphere", "plane",
    "quad", "capsule", "cylinder", "empty", "root", "container", "parent",
    "group", "holder", "dummy", "temp", "new prefab",
}


def tokens(name):
    return [t for t in _TOKEN_SPLIT.split(name or "") if t]


def is_npc_like(node):
    """未能匹配到 prefab 的角色 rig（NPC/顾客）：带骨骼与 Animator，
    静态还原只会摆出一个 T-pose，按"不要动画"的口径整棵跳过。"""
    for t in tokens(node.name):
        if t.lower() in ("npc", "npcs", "chr"):
            return True
    has_anim = node.has(SG.CLS_ANIMATOR) or node.has(SG.CLS_ANIMATION)
    if has_anim:
        for c in node.children:
            if c.name.strip().lower() in ("skeleton", "rig", "armature"):
                return True
    return False


def is_collision_only(node):
    """只有碰撞体、没有任何渲染器的节点 —— 属于碰撞通道，不是造景物件。"""
    if node.has(SG.CLS_MESHRENDERER) or node.has(SG.CLS_SKINNEDMESHRENDERER):
        return False
    return (node.has(SG.CLS_BOXCOLLIDER) or node.has(SG.CLS_MESHCOLLIDER)
            or node.has(SG.CLS_SPHERECOLLIDER) or node.has(SG.CLS_CAPSULECOLLIDER))


def is_light_group(node):
    n = node.name.strip().lower()
    return n in ("lights", "spotlights", "light probes", "lightprobes",
                 "reflection probes", "reflectionprobes")

_SUFFIX_PAREN = re.compile(r"\s*\(\d+\)$")
_SUFFIX_NUM = re.compile(r"\s+\d+$")


def base_name(n):
    return _SUFFIX_NUM.sub("", _SUFFIX_PAREN.sub("", n or "")).strip()


class Match(object):
    __slots__ = ("kind", "node", "name_used", "entry", "score", "detail", "candidates")

    def __init__(self, kind, node, name_used="", entry=None, score=None, detail="", candidates=0):
        self.kind = kind            # prefab / model / group / skip / drop
        self.node = node
        self.name_used = name_used
        self.entry = entry
        self.score = score
        self.detail = detail
        self.candidates = candidates

    def __repr__(self):
        return "<Match %s %s %s>" % (self.kind, self.node.name, self.detail)


def is_builtin_guid(g):
    """Unity 内置资源 guid（default/extra resources），不能用来做结构指纹：
    所有用内置 Quad/Cube 的东西 guid 都一样，会造成大面积误匹配。"""
    return not g or g.startswith("0000000000000000")


def node_signature(node):
    """节点子树签名：后代名字 + mesh guid + material guid（均剔除内置资源）。

    材质是区分"同网格不同皮肤"变体的关键（dispenser_crate_01 vs dispenser_crate_firewood
    共用 m_sk_crate_01 网格，只有材质不同）。
    """
    names = Counter()
    meshes = Counter()
    mats = Counter()
    for n in node.walk():
        if n is not node:
            names[n.name] += 1
        for cls in (SG.CLS_MESHFILTER, SG.CLS_SKINNEDMESHRENDERER):
            d = n.comp(cls)
            if d is not None:
                g = Y.ref_guid(d.data.get("m_Mesh", {}))
                if g and not is_builtin_guid(g):
                    meshes[g] += 1
        for cls in (SG.CLS_MESHRENDERER, SG.CLS_SKINNEDMESHRENDERER):
            d = n.comp(cls)
            if d is None:
                continue
            lst = d.data.get("m_Materials")
            if isinstance(lst, list):
                for m in lst:
                    g = Y.ref_guid(m) if isinstance(m, dict) else ""
                    if g and not is_builtin_guid(g):
                        mats[g] += 1
    return names, meshes, mats


def _overlap(a, b):
    if not a and not b:
        return None
    inter = sum((a & b).values())
    tot = max(sum(a.values()), sum(b.values()))
    if tot == 0:
        return None
    return inter / float(tot)


def shape_score(node_names, node_meshes, node_mats, shape):
    """返回 (结构分, 材质吻合度)。

    结构分只看 mesh + 子节点名 —— 场景实例常常覆盖材质，把材质计入主分会误伤。
    材质吻合度单独返回，只用于同分候选之间的取舍
    （dispenser_crate_01 / dispenser_crate_firewood 网格结构完全相同，只有材质不同）。
    """
    pn = Counter(shape.get("names") or [])
    root = shape.get("root") or ""
    if root and pn.get(root):
        pn[root] -= 1
        if pn[root] == 0:
            del pn[root]
    pm = Counter(g for g in (shape.get("meshes") or []) if not is_builtin_guid(g))
    pmat = Counter(g for g in (shape.get("mats") or []) if not is_builtin_guid(g))

    ns = _overlap(node_names, pn)
    msc = _overlap(node_meshes, pm)
    matsc = _overlap(node_mats, pmat)

    if msc is None and ns is None:
        base = None
    elif msc is None:
        base = ns
    elif ns is None:
        base = msc
    else:
        base = 0.7 * msc + 0.3 * ns
    return base, (matsc if matsc is not None else 0.0)


class Matcher(object):
    def __init__(self, resolver, prefer_tokens=None, keep_fx=False):
        self.R = resolver
        self.prefer = [t.lower() for t in (prefer_tokens or [])]
        self.keep_fx = keep_fx
        self.mesh_count = resolver.pi.get("meshCount", {})

    # ------------------------------------------------------------------
    def classify(self, node):
        nm = (node.name or "").strip()
        low = nm.lower()

        if SKIP_SUBTREE_RE.match(low) or low in SKIP_SUBTREE_EXACT:
            return Match("skip", node, detail="subtree:" + low)

        if BAKED_NAME_RE.match(nm):
            return Match("skip", node, detail="baked-combined")

        if is_light_group(node):
            return Match("light", node, detail="light-group")

        if node.has(SG.CLS_LIGHT):
            return Match("light", node, detail="light")

        if not self.keep_fx and FX_NAME_RE.match(nm):
            return Match("skip", node, detail="fx")

        m = self.try_prefab(node)
        if m is not None:
            return m

        m = self.try_signature(node)
        if m is not None:
            return m

        m = self.try_model(node)
        if m is not None:
            return m

        if is_collision_only(node) and not node.children:
            return Match("collision", node, detail="collider")

        if is_npc_like(node):
            return Match("skip", node, detail="npc-rig")

        if node.children:
            return Match("group", node)

        if low in SKIP_SELF_EXACT:
            return Match("skip", node, detail="self:" + low)

        if node.has(SG.CLS_PARTICLESYSTEM):
            return Match("skip", node, detail="particle")

        return Match("drop", node, detail=self._drop_reason(node))

    # ------------------------------------------------------------------
    def try_prefab(self, node):
        if base_name(node.name).lower() in GENERIC_NAMES:
            return None
        cands = self.R.prefab_candidates(node.name)
        if not cands:
            return None
        nn, nmsh, nmat = node_signature(node)
        scored = []
        for name_used, entry in cands:
            shape = self.R.shape_of(entry)
            if shape:
                sc, matsc = shape_score(nn, nmsh, nmat, shape)
            else:
                sc, matsc = None, 0.0
            bonus = self._container_bonus(entry["container"], node.name) + 0.08 * matsc
            scored.append((-(sc if sc is not None else 0.0) - bonus, sc, name_used, entry))
        scored.sort(key=lambda x: x[0])
        _, sc, name_used, entry = scored[0]
        n_cand = len(scored)

        if sc is None:
            # 反编译导出里缺这个 .prefab，只能凭名字；只允许极小的子树，避免吞掉分组
            if sum(1 for _ in node.walk()) <= 3:
                return Match("prefab", node, name_used, entry, None, "no-shape", n_cand)
            return None
        if sc >= 0.55:
            return Match("prefab", node, name_used, entry, sc, "", n_cand)
        # 名字完全一致 + 候选唯一 + 子树规模相当：场景里的网格可能是烘焙/静态合批后的副本，
        # 结构分对不上也应当认这个 prefab（mi_floor_01 / fence_straight_01 等就是这种）。
        # 规模校验必须做，否则像 Art/Scenery/Planks 这种分组会被同名 prefab 整个吞掉。
        if (n_cand == 1
                and name_used.lower() in (node.name.strip().lower(), base_name(node.name).lower())
                and (sc > 0.15 or not node.children)
                and self._size_compatible(node, self.R.shape_of(entry))):
            return Match("prefab", node, name_used, entry, sc, "name-exact", n_cand)
        if n_cand == 1 and sc >= 0.25:
            return Match("prefab", node, name_used, entry, sc, "weak-shape", n_cand)
        if not node.children and sc >= 0.2:
            return Match("prefab", node, name_used, entry, sc, "leaf-weak", n_cand)
        return None

    @staticmethod
    def _size_compatible(node, shape):
        if not shape:
            return False
        pg = int(shape.get("goCount") or 0)
        ng = sum(1 for _ in node.walk())
        if pg <= 0:
            return ng <= 2
        return ng <= max(pg * 2, pg + 2)

    def _container_bonus(self, container, node_name):
        """打分相同时的倾向性：同 DLC 优先、根名与场景名一致优先、非特殊变体优先。"""
        bonus = 0.0
        cont = container.lower()
        for t in self.prefer:
            if t and t in cont:
                bonus += 0.04
                break
        stem = os.path.splitext(os.path.basename(container))[0]
        nm = (node_name or "").strip()
        if stem == nm:
            bonus += 0.05
        elif stem.lower() == base_name(nm).lower():
            bonus += 0.03
        # 名字越短越"通用"，同分时优先通用款（dispenser_crate_01 > dispenser_crate_firewood）
        bonus += max(0.0, 0.02 - 0.0002 * len(stem))
        return bonus

    # ------------------------------------------------------------------
    def try_signature(self, node, _depth=0):
        """名字对不上时，用子树结构（mesh/material/子节点名）反查 prefab。

        场景里大量玩法物件用的是历史遗留命名（DispenserCrate / Bin / Counter…），
        与 bundle 内 prefab 名（dispenser_crate_01 …）完全对不上，
        但结构是一模一样的，可以精确反查。
        """
        best = self._signature_best(node)
        if best is None:
            return None
        sc, ar = best
        if sc < 0.7:
            return None
        # 单子节点的"包装组"：若子节点同样能匹配上，说明真正的实例在更深一层，
        # 在这里匹配会把包装组的变换当成实例变换，容易错位。
        if _depth < 4 and len(node.children) == 1:
            sub = self._signature_best(node.children[0])
            if sub is not None and sub[0] >= sc - 1e-6:
                return None
        entry = self.R.entry_for_ar(ar)
        if entry is None:
            return None
        return Match("prefab", node, base_name(node.name), entry, sc, "by-signature", 1)

    def _signature_best(self, node):
        nn, nmsh, nmat = node_signature(node)
        if not nn and not nmsh and not nmat:
            return None
        cands = self._signature_candidates(nn, nmsh, nmat)
        if not cands:
            return None
        scored = []
        for ar in cands:
            shape = self.R.shapes.get(ar)
            if not shape:
                continue
            sc, matsc = shape_score(nn, nmsh, nmat, shape)
            if sc is None:
                continue
            entry = self.R.entry_for_ar(ar)
            cont = entry["container"] if entry else ar
            bonus = self._container_bonus(cont, node.name) + 0.08 * matsc
            root = (shape.get("root") or "").lower()
            nlow = base_name(node.name).lower()
            if root and nlow and (root in nlow or nlow in root):
                bonus += 0.06
            scored.append((-(sc + bonus), sc, ar))
        if not scored:
            return None
        scored.sort(key=lambda x: x[0])
        return scored[0][1], scored[0][2]

    def _signature_candidates(self, nn, nmsh, nmat, cap=400):
        pools = []
        m2p = self.R.mesh_to_prefabs
        best = None
        for g in nmsh:
            lst = m2p.get(g)
            if not lst:
                continue
            if best is None or len(lst) < len(best):
                best = lst
        if best is not None and len(best) <= cap:
            pools.append(best)
        if not pools:
            # 没有网格（纯逻辑物件）时改用最稀有的子节点名
            c2p = self.R.childname_to_prefabs
            best2 = None
            for n in nn:
                lst = c2p.get(n)
                if not lst:
                    continue
                if best2 is None or len(lst) < len(best2):
                    best2 = lst
            if best2 is not None and len(best2) <= cap:
                pools.append(best2)
        if not pools:
            return None
        out = []
        seen = set()
        for p in pools:
            for ar in p:
                if ar not in seen:
                    seen.add(ar)
                    out.append(ar)
        return out

    # ------------------------------------------------------------------
    def try_model(self, node):
        """节点引用的是 bundle 里的模型（.fbx/.obj）而非 prefab。"""
        guids = []
        for n in node.walk():
            for cls in (SG.CLS_MESHFILTER, SG.CLS_SKINNEDMESHRENDERER):
                d = n.comp(cls)
                if d is not None:
                    g = Y.ref_guid(d.data.get("m_Mesh", {}))
                    if g:
                        guids.append(g)
        if not guids:
            return None

        conts = {}
        for g in guids:
            c = self.R.model_for_mesh_guid(g)
            if c is None:
                return None
            conts[c["container"]] = c
        if len(conts) != 1:
            return None
        cont = next(iter(conts.values()))
        total = self.mesh_count.get(cont["container"], 1)

        if not node.children:
            # 叶子：只有单网格模型才安全，多网格模型要整体在父层还原
            if total <= 1:
                return Match("model", node, base_name(node.name), cont, 1.0, "leaf-model")
            return None

        stem = os.path.splitext(os.path.basename(cont["container"]))[0].lower()
        if base_name(node.name).lower() == stem:
            return Match("model", node, base_name(node.name), cont, 1.0, "group-model")
        # 子树网格数与模型网格总数吻合，也认为是整体模型实例
        if total > 1 and len(guids) == total:
            return Match("model", node, base_name(node.name), cont, 0.8, "group-model-count")
        return None

    # ------------------------------------------------------------------
    def _drop_reason(self, node):
        has_mesh = node.has(SG.CLS_MESHFILTER) or node.has(SG.CLS_SKINNEDMESHRENDERER)
        if not has_mesh:
            if node.has(SG.CLS_BOXCOLLIDER):
                return "collider-only"
            if node.has(SG.CLS_LIGHT):
                return "light"
            if node.has(SG.CLS_PARTICLESYSTEM):
                return "particle"
            if node.has(SG.CLS_MONOBEHAVIOUR):
                return "logic-only"
            return "empty"
        d = node.comp(SG.CLS_MESHFILTER) or node.comp(SG.CLS_SKINNEDMESHRENDERER)
        g = Y.ref_guid(d.data.get("m_Mesh", {})) if d is not None else ""
        if g.startswith("0000000000000000"):
            return "builtin-mesh"
        rel = self.R.ar_path(g)
        if not rel:
            return "mesh-unresolved"
        if rel.startswith("Mesh/"):
            return "scene-embedded-mesh"
        return "mesh-no-model-container"

    # ------------------------------------------------------------------
    def walk(self, root, on_match):
        """自顶向下遍历。命中 prefab/model 后不再下钻。"""
        stack = [root]
        while stack:
            n = stack.pop()
            m = self.classify(n)
            on_match(m)
            if m.kind == "group":
                for c in reversed(n.children):
                    stack.append(c)
