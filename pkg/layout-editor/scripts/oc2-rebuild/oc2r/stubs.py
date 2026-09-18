"""静态玩法参数还原（stub 参数）。

按用户口径：只还原**静态**参数与必要的对象引用，不做动画组与开关联动。
重点是三类会在 Unity 里直接抛 UnassignedReferenceException 的引用：
    PseudoPrefabTerminalStub.pilotableObject
    PseudoPrefabHeatedOvenStub.heatedStation
    PseudoPrefabServingStationStub.plateReturn / plateReturns
以及玩法上最关键的食材箱内容物与传送门配对。
"""

import os

from . import scene_graph as SG
from . import yamlscene as Y

# 编辑器 stub 脚本 guid（Assets/Scripts/LevelEditorStub/*.cs.meta）
STUB_GUIDS = {
    "dispenser": "156b7334933469e49b4f74ff333f27cd",
    "terminal": "0ab2c6b4771509c42ba9ef97f95664c0",
    "heatedOven": "01487a485ceff894cad0d4445f15e859",
    "servingStation": "81b11dbf31f57bc4eb58ea3f734067e1",
    "plateReturn": "93fbcad70067334429bcc6434660422d",
    "teleportal": "cbca2d2955415d4469d5c349ef447d21",
    "cookingUtensil": "90647d85fb3e54e45b1ef93c4153b59b",
    "attachingFoodSpawner": "309630079e39a35408f2263b2b5d1b8a",
}
GUID_TO_KIND = dict((v, k) for k, v in STUB_GUIDS.items())


def stub_kind_of(plan):
    """看 wrapper 上挂了哪个 stub 组件，返回 (kind, 组件 fileID)。"""
    info = plan.wrapper_info or {}
    comps = info.get("comps") or {}
    for g, fid in comps.items():
        k = GUID_TO_KIND.get(g)
        if k:
            return k, fid
    return "", 0


def _mb(node, resolver, names, deep=True):
    """在节点（可选含后代）上找指定类名的 MonoBehaviour。"""
    pool = node.walk() if deep else [node]
    for n in pool:
        for cls, d in n.comps:
            if cls != SG.CLS_MONOBEHAVIOUR:
                continue
            c = resolver.script_class(Y.ref_guid(d.data.get("m_Script", {})))
            if c in names:
                return n, d
    return None, None


def _ref_node(scene, ref):
    """把场景内的 fileID 引用解析成节点（引用可能指向 GameObject 或组件）。"""
    fid = Y.ref_fid(ref) if isinstance(ref, dict) else 0
    if not fid:
        return None
    n = scene.nodes.get(fid)
    if n is not None:
        return n
    n = scene.by_tr.get(fid)
    if n is not None:
        return n
    d = scene.by_anchor.get(fid)
    if d is None:
        return None
    gof = Y.ref_fid(d.data.get("m_GameObject"))
    return scene.nodes.get(gof)


def collect(item, scene, resolver):
    """返回该物件要还原的 stub 数据：
    {"kind":…, "fid":…, "so": {字段名: SO guid}, "refs": {字段名: 目标源节点}}
    """
    kind, fid = stub_kind_of(item.plan)
    if not kind:
        return None
    out = {"kind": kind, "fid": fid, "so": {}, "refs": {}, "values": {}}
    node = item.node

    if kind in ("dispenser", "attachingFoodSpawner"):
        _n, d = _mb(node, resolver, ("PickupItemSpawner", "AttachingFoodSpawner"))
        if d is not None:
            g = Y.ref_guid(d.data.get("m_itemPrefab") or {})
            so = _ingredient_so(resolver, g)
            if so:
                out["so"]["spawnerItemPrefabSO"] = so
    elif kind == "terminal":
        _n, d = _mb(node, resolver, ("Terminal",))
        if d is not None:
            t = _ref_node(scene, d.data.get("m_pilotableObject"))
            if t is not None:
                out["refs"]["pilotableObject"] = ("go", t)
    elif kind == "heatedOven":
        _n, d = _mb(node, resolver, ("HeatedOven", "OvenStation", "HeatedStation"))
        if d is not None:
            for key in ("m_heatedStation", "m_station", "m_cookingStation"):
                t = _ref_node(scene, d.data.get(key))
                if t is not None:
                    out["refs"]["heatedStation"] = ("go", t)
                    break
    elif kind == "servingStation":
        _n, d = _mb(node, resolver, ("PlateStation", "ServingStation", "OrderStation"))
        if d is not None:
            for key in ("m_plateReturnStation", "m_plateReturn", "m_returnStation"):
                t = _ref_node(scene, d.data.get(key))
                if t is not None:
                    out["refs"]["plateReturn"] = ("comp:plateReturn", t)
                    break
            lst = d.data.get("m_plateReturnStations") or d.data.get("m_plateReturns")
            if isinstance(lst, list):
                targets = []
                for r in lst:
                    t = _ref_node(scene, r)
                    if t is not None:
                        targets.append(t)
                if targets:
                    out["refs"]["plateReturns"] = ("comp-array:plateReturn", targets)
    elif kind == "teleportal":
        _n, d = _mb(node, resolver, ("Teleportal", "Portal", "TeleportalStation"))
        if d is not None:
            for key in ("m_exitPortal", "m_linkedPortal", "m_target"):
                t = _ref_node(scene, d.data.get(key))
                if t is not None:
                    out["refs"]["exitPortal"] = ("comp:teleportal", t)
                    break
    elif kind == "cookingUtensil":
        _n, d = _mb(node, resolver, ("IngredientContainer", "CookableContainer",
                                     "MixableContainer"))
        if d is not None:
            cap = d.data.get("m_capacity")
            if cap is not None:
                out["values"]["capacity"] = int(Y.num(cap, 0))

    if not out["so"] and not out["refs"] and not out["values"]:
        return None
    return out


def _ingredient_so(resolver, prefab_guid):
    """游戏食材 prefab -> 编辑器食材 SO guid。"""
    if not prefab_guid:
        return ""
    rel = resolver.ar_path(prefab_guid)
    if not rel:
        return ""
    name = os.path.splitext(os.path.basename(rel))[0]
    cands = resolver.so_guids_for_prefab_name(name)
    if not cands:
        return ""
    # 优先 assetPath 就指向这个 prefab 的
    want = ("assets/" + rel).lower().replace("\\", "/")
    for g in cands:
        info = resolver.so_info(g) or {}
        if (info.get("assetPath") or "").lower().replace("\\", "/") == want:
            return g
    return cands[0]
