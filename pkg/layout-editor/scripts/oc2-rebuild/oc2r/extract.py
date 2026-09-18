"""从源场景抽取重建所需的全部数据（世界坐标系）。

与上一版的根本差别：一切几何都用 **世界变换**（scene_graph 已算好），
生成端把物件挂在单位变换的分组下，因此 local == world，不会再出现父变换丢失。
"""

import os
import re

from . import matcher as M
from . import scene_graph as SG
from . import structure as ST
from . import stubs as STUBS
from . import yamlscene as Y

RENDER_KEYS = (
    "m_Fog", "m_FogColor", "m_FogMode", "m_FogDensity",
    "m_LinearFogStart", "m_LinearFogEnd",
    "m_AmbientSkyColor", "m_AmbientEquatorColor", "m_AmbientGroundColor",
    "m_AmbientIntensity", "m_AmbientMode", "m_SubtractiveShadowColor",
    "m_HaloStrength", "m_FlareStrength", "m_FlareFadeSpeed",
    "m_IndirectSpecularColor",
)

BUILTIN_MESH_IDS = {
    "10202": "Cube", "10206": "Cylinder", "10207": "Capsule",
    "10208": "Sphere", "10209": "Plane", "10210": "Quad",
}

DEFAULT_GRID_HALF = (9, 5)
DEFAULT_CELL = 1.2


class Item(object):
    __slots__ = ("name", "pos", "rot", "scale", "plan", "group", "skewed",
                 "src_path", "node", "container", "stub")

    def __init__(self, name, node, plan, group, container):
        self.name = name
        self.node = node
        self.pos = node.wpos
        self.rot = SG.q_norm(node.wrot)
        self.scale = node.wscale
        self.plan = plan
        self.group = group
        self.skewed = node.skewed
        self.src_path = node.path
        self.container = container
        self.stub = None


class Box(object):
    __slots__ = ("name", "pos", "rot", "scale", "size", "center", "layer",
                 "trigger", "respawn")

    def __init__(self, name, pos, rot, scale, size, center, layer=0,
                 trigger=0, respawn=None):
        self.name = name
        self.pos = pos
        self.rot = rot
        self.scale = scale
        self.size = size
        self.center = center
        self.layer = layer
        self.trigger = trigger
        self.respawn = respawn


class FloorTile(object):
    __slots__ = ("name", "pos", "rot", "scale", "mesh_fid", "mat_refs", "mat_names")

    def __init__(self, name, pos, rot, scale, mesh_fid, mat_refs, mat_names):
        self.name = name
        self.pos = pos
        self.rot = rot
        self.scale = scale
        self.mesh_fid = mesh_fid
        self.mat_refs = mat_refs
        self.mat_names = mat_names


class LevelModel(object):
    def __init__(self):
        self.render = {}
        self.grid_pos = (0.0, 0.0, 0.0)
        self.grid_half = DEFAULT_GRID_HALF
        self.cell = DEFAULT_CELL
        self.kp_pos = (0.0, -1.0, 0.0)
        self.kp_scale = (7.0, 1.0, 7.0)
        self.kp_respawn = 1
        self.cam_pos = None
        self.dir_light = None
        self.items = []
        self.walls = []
        self.floor_box = None
        self.killplanes = []
        self.players = {}
        self.floors = []
        self.stats = {}
        self.env_name = ""


def raw_field(doc, key):
    m = re.search(r"^  " + re.escape(key) + r": (.*)$", doc.body, re.M)
    return m.group(1).strip() if m else None


def _mb_by_class(node, resolver, want):
    for cls, d in node.comps:
        if cls != SG.CLS_MONOBEHAVIOUR:
            continue
        g = Y.ref_guid(d.data.get("m_Script", {}))
        if resolver.script_class(g) == want:
            return d
    return None


def _first_box(node):
    for cls, d in node.comps:
        if cls == SG.CLS_BOXCOLLIDER:
            return d
    return None


def _mesh_ref(node):
    d = node.comp(SG.CLS_MESHFILTER) or node.comp(SG.CLS_SKINNEDMESHRENDERER)
    if d is None:
        return None, None
    m = d.data.get("m_Mesh", {})
    return str(Y.ref_fid(m)), Y.ref_guid(m)


def _materials(node):
    d = node.comp(SG.CLS_MESHRENDERER) or node.comp(SG.CLS_SKINNEDMESHRENDERER)
    if d is None:
        return []
    lst = d.data.get("m_Materials")
    if not isinstance(lst, list):
        return []
    return [Y.ref_guid(x) for x in lst if isinstance(x, dict) and Y.ref_guid(x)]


class Extractor(object):
    def __init__(self, resolver, placer, floormats=None):
        self.R = resolver
        self.P = placer
        # 源材质 guid（AssetRipper）-> 工程材质 guid 的按需解析器
        self.fm = floormats

    # ------------------------------------------------------------------
    def build(self, level):
        sc = SG.Scene(level["sourceScene"])
        mdl = LevelModel()
        mt = M.Matcher(self.R, prefer_tokens=level.get("preferTokens"))
        chans = ST.classify_roots(sc)

        self._render(sc, mdl)
        self._env(sc, chans, mdl)
        self._camera(chans, mdl)
        self._players(sc, chans, mdl)

        drops = []
        skips = []
        lights = []
        collisions = []

        def on_match(m):
            if m.kind in ("prefab", "model"):
                self._add_item(mdl, m)
            elif m.kind == "collision":
                collisions.append(m.node)
            elif m.kind == "light":
                lights.append(m.node)
            elif m.kind == "drop":
                if m.detail == "builtin-mesh":
                    self._add_floor(mdl, m.node)
                else:
                    drops.append((m.detail, m.node.path))
            elif m.kind == "skip":
                skips.append((m.detail, m.node.path))

        special = {"collision": [], "killplane": [], "lights": []}
        for r in chans["objects"]:
            wl, sp = ST.object_subroots(sc, r)
            for k in special:
                special[k].extend(sp[k])
            for w in wl:
                mt.walk(w, on_match)

        self._collision(sc, special["collision"], collisions, mdl)
        self._killplanes(special["killplane"], mdl)
        self._lights(special["lights"] + lights, mdl)

        # 静态玩法参数（含跨物件引用）
        node_to_item = {}
        for it in mdl.items:
            node_to_item[id(it.node)] = it
        stub_n = 0
        for it in mdl.items:
            s = STUBS.collect(it, sc, self.R)
            if not s:
                continue
            # 把引用目标从"源节点"换成"已生成的物件"
            resolved = {}
            for field, (mode, target) in s["refs"].items():
                if isinstance(target, list):
                    got = [node_to_item.get(id(_owner_item_node(t, node_to_item)))
                           for t in target]
                    got = [g for g in got if g is not None]
                    if got:
                        resolved[field] = (mode, got)
                else:
                    owner = _owner_item_node(target, node_to_item)
                    g = node_to_item.get(id(owner)) if owner is not None else None
                    if g is not None:
                        resolved[field] = (mode, g)
            s["refs"] = resolved
            if s["so"] or s["refs"] or s["values"]:
                it.stub = s
                stub_n += 1

        mdl.stats = {
            "items": len(mdl.items),
            "counters": sum(1 for i in mdl.items if i.group == "counters"),
            "utensils": sum(1 for i in mdl.items if i.group == "utensils"),
            "scenery": sum(1 for i in mdl.items if i.group == "scenery"),
            "skewed": sum(1 for i in mdl.items if i.skewed),
            "walls": len(mdl.walls),
            "killplanes": len(mdl.killplanes),
            "floors": len(mdl.floors),
            "players": len(mdl.players),
            "lights": 1 if mdl.dir_light else 0,
            "stubs": stub_n,
            "drops": len(drops),
            "skips": len(skips),
            "dropDetail": _count(d for d, _ in drops),
            "skipDetail": _count(d for d, _ in skips),
            "dropPaths": [p for _, p in drops[:40]],
            "floorMaterialsMissing": sorted(set(
                n for f in mdl.floors for n in f.mat_names if not f.mat_refs)),
        }
        return mdl, sc

    # ------------------------------------------------------------------
    def _render(self, sc, mdl):
        for d in sc.docs:
            if d.cls == SG.CLS_RENDERSETTINGS:
                for k in RENDER_KEYS:
                    v = raw_field(d, k)
                    if v is not None:
                        mdl.render[k] = v
                return

    def _env(self, sc, chans, mdl):
        env = chans["env"][0] if chans["env"] else None
        if env is None:
            return
        mdl.env_name = env.name
        grid = None
        kp = None
        for n in env.walk():
            nm = n.name.strip().lower()
            if grid is None and nm == "gridmanager":
                grid = n
            elif kp is None and nm == "killplane":
                kp = n
        if grid is not None:
            mdl.grid_pos = grid.wpos
            d = _mb_by_class(grid, self.R, "QuadGridManager")
            if d is None:
                for cls, dd in grid.comps:
                    if cls == SG.CLS_MONOBEHAVIOUR and "m_gridHalfSize" in dd.data:
                        d = dd
                        break
            if d is not None:
                hs = d.data.get("m_gridHalfSize") or {}
                mdl.grid_half = (int(Y.num(hs.get("X"), 9)), int(Y.num(hs.get("Z"), 5)))
                sz = d.data.get("m_size") or {}
                mdl.cell = Y.num(sz.get("x"), DEFAULT_CELL) or DEFAULT_CELL
        if kp is not None:
            mdl.kp_pos = kp.wpos
            mdl.kp_scale = kp.wscale
            d = _mb_by_class(kp, self.R, "RespawnCollider")
            if d is None:
                for cls, dd in kp.comps:
                    if cls == SG.CLS_MONOBEHAVIOUR and "m_respawnType" in dd.data:
                        d = dd
                        break
            if d is not None:
                mdl.kp_respawn = int(Y.num(d.data.get("m_respawnType"), 1))

    def _camera(self, chans, mdl):
        cam = chans["camera"][0] if chans["camera"] else None
        if cam is not None:
            mdl.cam_pos = cam.wpos

    def _players(self, sc, chans, mdl):
        for n, node in ST.player_nodes(sc, chans["chefs"]):
            rot = SG.q_norm(node.wrot)
            mdl.players[n] = (node.wpos, rot, SG.q_to_euler_unity(rot)[1])

    # ------------------------------------------------------------------
    def _add_item(self, mdl, m):
        plan = self.P.resolve(m.entry)
        if not plan.wrapper:
            return
        parent = plan.default_parent or "Art"
        if parent.startswith("Design/Counters"):
            group = "counters"
        elif parent.startswith("Design/Utensils"):
            group = "utensils"
        else:
            group = "scenery"
        mdl.items.append(Item(m.node.name, m.node, plan, group, m.entry["container"]))

    def _add_floor(self, mdl, node):
        fid, guid = _mesh_ref(node)
        if fid not in BUILTIN_MESH_IDS:
            return
        src_mats = _materials(node)
        names = [self.R.material_name(g) or g for g in src_mats]
        mapped = []
        if self.fm is not None:
            for g in src_mats:
                pg = self.fm.ensure(g)
                if pg:
                    mapped.append(pg)
        mdl.floors.append(FloorTile(node.name, node.wpos, SG.q_norm(node.wrot),
                                    node.wscale, fid, mapped, names))

    # ------------------------------------------------------------------
    def _collision(self, sc, groups, extra_nodes, mdl):
        seen = set()
        cands = []
        for g in groups:
            for n in g.walk():
                cands.append(n)
        cands.extend(extra_nodes)
        for n in cands:
            if id(n) in seen:
                continue
            seen.add(id(n))
            box = _first_box(n)
            if box is None:
                continue
            nm = n.name.strip().lower()
            if nm.startswith("killplane"):
                mdl.killplanes.append(self._box_of(n, box, layer=n.layer, trigger=1,
                                                   respawn=self._respawn_of(n)))
                continue
            b = self._box_of(n, box, layer=n.layer)
            if nm.startswith("ground") or nm.startswith("floor") or nm.startswith("col_floor"):
                if mdl.floor_box is None:
                    b.name = "Col_Floor"
                    mdl.floor_box = b
                else:
                    b.name = "Col_Floor"
                    mdl.walls.append(b)
            else:
                b.name = "Col_Wall"
                mdl.walls.append(b)
        if mdl.floor_box is None:
            mdl.floor_box = Box(
                "Col_Floor", (mdl.grid_pos[0], 0.0, mdl.grid_pos[2]),
                SG.IDENT_Q, (1.0, 1.0, 1.0),
                (2 * mdl.grid_half[0] * mdl.cell, 0.4, 2 * mdl.grid_half[1] * mdl.cell),
                (0.0, -0.2, 0.0), layer=9)

    def _killplanes(self, groups, mdl):
        for g in groups:
            for n in g.walk():
                box = _first_box(n)
                if box is None:
                    continue
                mdl.killplanes.append(self._box_of(
                    n, box, layer=n.layer, trigger=1, respawn=self._respawn_of(n)))

    def _respawn_of(self, node):
        d = _mb_by_class(node, self.R, "RespawnCollider")
        if d is None:
            for cls, dd in node.comps:
                if cls == SG.CLS_MONOBEHAVIOUR and "m_respawnType" in dd.data:
                    d = dd
                    break
        if d is None:
            return 1
        return int(Y.num(d.data.get("m_respawnType"), 1))

    @staticmethod
    def _box_of(node, box, layer=0, trigger=0, respawn=None):
        size = SG.v3(box.data.get("m_Size"), SG.ONE3)
        center = SG.v3(box.data.get("m_Center"))
        return Box(node.name, node.wpos, SG.q_norm(node.wrot), node.wscale,
                   size, center, layer=layer, trigger=trigger, respawn=respawn)

    # ------------------------------------------------------------------
    def _lights(self, groups, mdl):
        best = None
        for g in groups:
            for n in g.walk():
                d = n.comp(SG.CLS_LIGHT)
                if d is None:
                    continue
                if str(Y.num(d.data.get("m_Type"), 0)) not in ("1", "1.0"):
                    continue
                best = (n, d)
                break
            if best:
                break
        if not best:
            return
        n, d = best
        mdl.dir_light = {
            "rot": SG.q_norm(n.wrot),
            "color": raw_field(d, "m_Color") or "{r: 1, g: 0.93333334, b: 0.7921569, a: 1}",
            "intensity": raw_field(d, "m_Intensity") or "0.7",
            "shadow": "0.629",
        }
        m = re.search(r"m_Strength: ([^\n]*)", d.body)
        if m:
            mdl.dir_light["shadow"] = m.group(1).strip()


def _count(it):
    out = {}
    for x in it:
        out[x] = out.get(x, 0) + 1
    return out


def _owner_item_node(node, node_to_item):
    """引用可能指到某个物件的子节点，往上找到被生成成实例的那个节点。"""
    n = node
    while n is not None:
        if id(n) in node_to_item:
            return n
        n = n.parent
    return None
