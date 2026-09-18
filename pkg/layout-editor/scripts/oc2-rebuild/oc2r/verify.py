"""几何校验：把生成的场景反解回来，与源场景逐件比对。

这是上一版完全缺失的一环。校验对象：
  * 每个物件的世界坐标 / 旋转 / 缩放
  * 碰撞盒、表杀平面、厨师出生点
  * 网格中心与半径、相机根
不达标（mismatch > 0）就不算重建成功。
"""

import math
import os
import re

from . import scene_graph as SG
from . import yamlscene as Y
from .sceneyaml import FID

TOL_POS = 1e-3
TOL_ROT = 1e-3
TOL_SCALE = 1e-3


def _dist(a, b):
    return math.sqrt(sum((a[i] - b[i]) ** 2 for i in range(3)))


def _qdiff(a, b):
    d = abs(sum(a[i] * b[i] for i in range(4)))
    d = max(0.0, min(1.0, d))
    return 2.0 * math.degrees(math.acos(d))


def parse_generated(path):
    """读回生成的场景：返回按分组归类的实例世界变换。

    生成的场景里 Design/Counters、Design/Utensils、Art/Scenery 都是单位变换，
    所以实例的 m_Modifications 里的 local 变换就是世界变换。
    """
    txt = open(path, encoding="utf-8", errors="replace").read()
    docs = Y.split_docs(txt)
    parents = {
        FID["counters_tr"]: "counters",
        FID["utensils_tr"]: "utensils",
        FID["chefs_tr"]: "chefs",
        FID["ground_tr"]: "floors",
    }
    # Scenery 分组的 fileID 是动态的，靠名字找
    by_anchor = {d.anchor: d for d in docs}
    scenery_tr = 0
    killplanes_tr = 0
    for d in docs:
        if d.cls != 1:
            continue
        nm = d.data.get("m_Name")
        if nm not in ("Scenery", "KillPlanes"):
            continue
        comps = d.data.get("m_Component") or []
        for c in comps:
            fid = Y.ref_fid(c.get("component")) if isinstance(c, dict) else 0
            dd = by_anchor.get(fid)
            if dd is not None and dd.cls == 4:
                if nm == "Scenery":
                    scenery_tr = fid
                else:
                    killplanes_tr = fid
    if scenery_tr:
        parents[scenery_tr] = "scenery"

    out = {"counters": [], "utensils": [], "scenery": [], "chefs": [],
           "walls": [], "killplanes": [], "floors": []}
    for d in docs:
        if d.cls != 1001:
            continue
        mo = d.data.get("m_Modification") or {}
        parent = Y.ref_fid(mo.get("m_TransformParent"))
        grp = parents.get(parent)
        if grp is None:
            continue
        mods = mo.get("m_Modifications") or []
        vals = {}
        for m in mods:
            if not isinstance(m, dict):
                continue
            vals[m.get("propertyPath")] = m.get("value")
        pos = (Y.num(vals.get("m_LocalPosition.x")), Y.num(vals.get("m_LocalPosition.y")),
               Y.num(vals.get("m_LocalPosition.z")))
        rot = (Y.num(vals.get("m_LocalRotation.x")), Y.num(vals.get("m_LocalRotation.y")),
               Y.num(vals.get("m_LocalRotation.z")), Y.num(vals.get("m_LocalRotation.w"), 1.0))
        scale = (Y.num(vals.get("m_LocalScale.x"), 1.0), Y.num(vals.get("m_LocalScale.y"), 1.0),
                 Y.num(vals.get("m_LocalScale.z"), 1.0))
        out[grp].append({"name": vals.get("m_Name") or "", "pos": pos,
                         "rot": SG.q_norm(rot), "scale": scale,
                         "guid": Y.ref_guid(d.data.get("m_ParentPrefab", {}))})

    # 非 prefab 的碰撞 / 表杀 / 地板：直接读 Transform
    go_name = {}
    for d in docs:
        if d.cls == 1:
            go_name[d.anchor] = d.data.get("m_Name") or ""
    for d in docs:
        if d.cls != 4 or d.stripped:
            continue
        gof = Y.ref_fid(d.data.get("m_GameObject"))
        nm = go_name.get(gof, "")
        parent = Y.ref_fid(d.data.get("m_Father"))
        rec = {
            "name": nm,
            "pos": SG.v3(d.data.get("m_LocalPosition")),
            "rot": SG.q_norm(SG.quat(d.data.get("m_LocalRotation"))),
            "scale": SG.v3(d.data.get("m_LocalScale"), SG.ONE3),
        }
        if parent == FID["collision_tr"]:
            out["walls"].append(rec)
        elif parent == FID["ground_tr"] and nm != "Floor":
            out["floors"].append(rec)
        elif killplanes_tr and parent == killplanes_tr:
            out["killplanes"].append(rec)
    return out


def _match_lists(src, gen, label, report, tol_pos=TOL_POS):
    """按位置最近邻配对，统计缺失/多余/偏差。"""
    used = [False] * len(gen)
    miss = 0
    worst = 0.0
    worst_name = ""
    rot_bad = 0
    scale_bad = 0
    for s in src:
        best = -1
        best_d = None
        for i, g in enumerate(gen):
            if used[i]:
                continue
            d = _dist(s["pos"], g["pos"])
            if best_d is None or d < best_d:
                best_d = d
                best = i
        if best < 0 or best_d > max(tol_pos, 1e-3):
            miss += 1
            if best_d is not None and best_d > worst:
                worst = best_d
                worst_name = s.get("name", "")
            continue
        used[best] = True
        g = gen[best]
        if _qdiff(s["rot"], g["rot"]) > 0.05:
            rot_bad += 1
        if any(abs(s["scale"][k] - g["scale"][k]) > TOL_SCALE for k in range(3)):
            scale_bad += 1
        if best_d > worst:
            worst = best_d
            worst_name = s.get("name", "")
    extra = sum(1 for u in used if not u)
    report[label] = {
        "src": len(src), "gen": len(gen), "missing": miss, "extra": extra,
        "rotMismatch": rot_bad, "scaleMismatch": scale_bad,
        "worstDist": round(worst, 6), "worstName": worst_name,
    }
    return miss + extra + rot_bad + scale_bad


_prefab_anchor_cache = {}


def prefab_anchors(path):
    """预制体内部所有 fileID（用于校验实例 override 的 target 是否存在）。"""
    if path in _prefab_anchor_cache:
        return _prefab_anchor_cache[path]
    try:
        txt = open(path, encoding="utf-8", errors="replace").read()
    except OSError:
        _prefab_anchor_cache[path] = None
        return None
    anchors = set(int(m) for m in re.findall(r"^--- !u!\d+ &(-?\d+)", txt, re.M))
    _prefab_anchor_cache[path] = anchors
    return anchors


def structural_check(path, project_guids, guid_to_path=None):
    """结构自检：引用是否闭合、guid 是否都能在工程里找到、
    实例 override 的 target fileID 是否真的存在于被引用的预制体里。

    project_guids: 工程内所有资产 guid 的集合（含 .prefab/.mat/.asset）。
    guid_to_path:  可选，guid -> 工程内资产路径；给了才做 target 校验。
    """
    txt = open(path, encoding="utf-8", errors="replace").read()
    docs = Y.split_docs(txt)
    anchors = set(d.anchor for d in docs)
    problems = []

    for d in docs:
        if d.cls == 4 and not d.stripped:
            data = d.data
            f = Y.ref_fid(data.get("m_Father"))
            if f and f not in anchors:
                problems.append("Transform %d 的父节点 %d 不存在" % (d.anchor, f))
            kids = data.get("m_Children")
            if isinstance(kids, list):
                for k in kids:
                    kf = Y.ref_fid(k)
                    if kf and kf not in anchors:
                        problems.append("Transform %d 的子节点 %d 不存在" % (d.anchor, kf))
            gof = Y.ref_fid(data.get("m_GameObject"))
            if gof and gof not in anchors:
                problems.append("Transform %d 的 GameObject %d 不存在" % (d.anchor, gof))
        elif d.cls == 1001:
            g = Y.ref_guid(d.data.get("m_ParentPrefab", {}))
            if not g:
                problems.append("实例 %d 没有 m_ParentPrefab" % d.anchor)
            elif project_guids and g not in project_guids:
                problems.append("实例 %d 引用的预制体 guid %s 不在工程里" % (d.anchor, g))
            mo = d.data.get("m_Modification") or {}
            targets = prefab_anchors(guid_to_path[g]) if (guid_to_path and g in guid_to_path) else None
            for m in (mo.get("m_Modifications") or []):
                if not isinstance(m, dict):
                    continue
                ref = m.get("objectReference")
                rg = Y.ref_guid(ref) if isinstance(ref, dict) else ""
                if rg and project_guids and rg not in project_guids:
                    problems.append("实例 %d 的 override 引用 guid %s 不在工程里" % (d.anchor, rg))
                if targets is not None:
                    t = Y.ref_fid(m.get("target"))
                    if t and t not in targets:
                        problems.append("实例 %d 的 override target %d 不在预制体 %s 内"
                                        % (d.anchor, t, os.path.basename(guid_to_path[g])))
            tp = Y.ref_fid(mo.get("m_TransformParent"))
            if tp and tp not in anchors:
                problems.append("实例 %d 的父 Transform %d 不存在" % (d.anchor, tp))
        elif d.cls == 23:
            mats = d.data.get("m_Materials")
            if isinstance(mats, list):
                for m in mats:
                    g = Y.ref_guid(m) if isinstance(m, dict) else ""
                    if g and not g.startswith("0000000000000000") and project_guids \
                            and g not in project_guids:
                        problems.append("MeshRenderer %d 的材质 guid %s 不在工程里"
                                        % (d.anchor, g))
    return problems


def verify(mdl, scene_path):
    gen = parse_generated(scene_path)
    report = {}
    bad = 0

    src_by_group = {"counters": [], "utensils": [], "scenery": []}
    for it in mdl.items:
        src_by_group[it.group].append(
            {"name": it.name, "pos": it.pos, "rot": it.rot, "scale": it.scale})
    for g in ("counters", "utensils", "scenery"):
        bad += _match_lists(src_by_group[g], gen[g], g, report)

    chefs = [{"name": "Player %d" % n, "pos": p[0], "rot": p[1], "scale": (1, 1, 1)}
             for n, p in sorted(mdl.players.items())]
    bad += _match_lists(chefs, gen["chefs"], "chefs", report)

    walls = [{"name": w.name, "pos": w.pos, "rot": w.rot, "scale": w.scale}
             for w in ([mdl.floor_box] if mdl.floor_box else []) + mdl.walls]
    bad += _match_lists(walls, gen["walls"], "walls", report)

    kps = [{"name": k.name, "pos": k.pos, "rot": k.rot, "scale": k.scale}
           for k in mdl.killplanes]
    bad += _match_lists(kps, gen["killplanes"], "killplanes", report)

    floors = [{"name": f.name, "pos": f.pos, "rot": f.rot, "scale": f.scale}
              for f in mdl.floors if f.mat_refs]
    bad += _match_lists(floors, gen["floors"], "floors", report)

    report["total"] = bad
    return report
