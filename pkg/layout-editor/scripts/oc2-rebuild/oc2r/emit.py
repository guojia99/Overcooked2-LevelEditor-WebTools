"""把 LevelModel 组装成编辑器 .unity 场景文本。

骨架沿用 Assets/Template/s_template.unity 的固定 fileID 结构：
  Design/{Collision,Utensils,Counters,KillPlanes} · Chefs · Art/{Lights,Ground,Scenery}
  · MultiplayerGameCamera · Debug · PseudoPrefabManager · 环境 prefab 实例
所有物件挂在单位变换的分组下，local == world。
"""

import os
import re

from . import paths
from . import scene_graph as SG
from .sceneyaml import (FID, FidGen, SCENE_META_TMPL, box_yaml, env_instance, fnum,
                        go_yaml, light_yaml, mesh_filter_yaml, mesh_renderer_yaml,
                        mod_line, player_instance, prefab_instance, respawn_yaml,
                        tr_yaml)
from .stubs import STUB_GUIDS

TEMPLATE = os.path.join(paths.ASSETS, "Template", "s_template.unity")
FLOOR_MAT_GUID = "8a4202dd5921a734291b1222e0725671"

_tmpl_cache = [None]


def template():
    if _tmpl_cache[0] is None:
        with open(TEMPLATE, encoding="utf-8") as f:
            _tmpl_cache[0] = f.read()
    return _tmpl_cache[0]


def _grab(tmpl, start_marker, end_marker):
    i = tmpl.find(start_marker)
    j = tmpl.find(end_marker, i)
    return tmpl[i:j].rstrip("\n")


class SceneWriter(object):
    def __init__(self, resolver):
        self.R = resolver

    # ------------------------------------------------------------------
    def build(self, mdl, level_info_guid=None, manager_patch=None):
        tmpl = template()
        fg = FidGen()
        out = []

        # 1) 头部（RenderSettings 打源场景的补丁）
        head = tmpl[:tmpl.find("--- !u!1 &55104702")]
        for k, v in mdl.render.items():
            head = re.sub(re.escape("\n  " + k) + r": [^\n]*",
                          "\n  %s: %s" % (k, v), head, count=1)
        out.append(head.rstrip("\n"))

        # 2) 环境 prefab 实例（网格 / 表杀）
        out.extend(env_instance(mdl.grid_pos, mdl.grid_half, mdl.kp_pos,
                                mdl.kp_scale, mdl.kp_respawn))

        # 3) 物件（Scenery 分组 fileID 先分配，物件才知道父节点）
        scenery_go, scenery_tr = fg.next(), fg.next()
        parent_of = {"counters": FID["counters_tr"], "utensils": FID["utensils_tr"],
                     "scenery": scenery_tr}
        counters, utensils, scenery = [], [], []
        item_docs = []
        bundles = set()
        # 先给每个物件分配实例 id / transform id，交叉引用才有得可指
        slots = {}
        for it in mdl.items:
            slots[id(it)] = {"iid": fg.next(), "tid": fg.next(), "go": 0, "comp": {}}
        stripped_docs = []
        for it in mdl.items:
            for field, (mode, target) in ((it.stub or {}).get("refs") or {}).items():
                targets = target if isinstance(target, list) else [target]
                for t in targets:
                    s = slots.get(id(t))
                    if s is None:
                        continue
                    if mode == "go":
                        if not s["go"]:
                            s["go"] = fg.next()
                    else:
                        key = mode.split(":", 1)[1]
                        if key not in s["comp"]:
                            s["comp"][key] = fg.next()
        for it in mdl.items:
            bucket = {"counters": counters, "utensils": utensils}.get(it.group, scenery)
            doc, extra = self._item(it, slots, parent_of[it.group], len(bucket))
            item_docs.append(doc)
            stripped_docs.extend(extra)
            bucket.append(slots[id(it)]["tid"])
            so = self.R.so_info(it.plan.so_guid) if it.plan.so_guid else None
            if so is None:
                info = self.R.wrappers.get(it.plan.wrapper) or {}
                so = self.R.so_info(info.get("so") or "")
            if so and so.get("bundleName"):
                bundles.add(so["bundleName"])
        item_docs.extend(stripped_docs)

        # 4) 碰撞
        col_children = [FID["floor_col_tr"]]
        wall_docs = []
        fb = mdl.floor_box
        floor_col_docs = [
            go_yaml(FID["floor_col_go"], "Col_Floor",
                    [FID["floor_col_tr"], FID["floor_col_box"]], layer=9),
            tr_yaml(FID["floor_col_tr"], FID["floor_col_go"], FID["collision_tr"], 0,
                    fb.pos, fb.rot, fb.scale, []),
            box_yaml(FID["floor_col_box"], FID["floor_col_go"], fb.size, fb.center),
        ]
        for i, w in enumerate(mdl.walls):
            g, t, b = fg.next(), fg.next(), fg.next()
            nm = w.name if i == 0 else "%s (%d)" % (w.name, i)
            wall_docs.append(go_yaml(g, nm, [t, b], layer=w.layer or 10))
            wall_docs.append(tr_yaml(t, g, FID["collision_tr"], len(col_children),
                                     w.pos, w.rot, w.scale, []))
            wall_docs.append(box_yaml(b, g, w.size, w.center))
            col_children.append(t)

        # 5) 表杀组
        kp_docs = []
        kp_children = []
        design_children = [FID["collision_tr"], FID["utensils_tr"], FID["counters_tr"]]
        kp_tr = 0
        if mdl.killplanes:
            kp_go, kp_tr = fg.next(), fg.next()
            for i, kp in enumerate(mdl.killplanes):
                g, t, b, r = fg.next(), fg.next(), fg.next(), fg.next()
                nm = "KillPlane" if i == 0 else "KillPlane (%d)" % i
                kp_docs.append(go_yaml(g, nm, [t, b, r], layer=kp.layer))
                kp_docs.append(tr_yaml(t, g, kp_tr, i, kp.pos, kp.rot, kp.scale, []))
                kp_docs.append(box_yaml(b, g, kp.size, kp.center, trigger=1))
                kp_docs.append(respawn_yaml(r, g, kp.respawn if kp.respawn is not None else 1))
                kp_children.append(t)
            kp_docs.insert(0, go_yaml(kp_go, "KillPlanes", [kp_tr]))
            kp_docs.insert(1, tr_yaml(kp_tr, kp_go, FID["design_tr"], 3,
                                      (0, 0, 0), SG.IDENT_Q, (1, 1, 1), kp_children))
            design_children.append(kp_tr)

        # 6) 玩家
        player_docs = []
        player_children = []
        for n in sorted(mdl.players):
            pos, rot, hint = mdl.players[n]
            doc, tid = player_instance(fg, n, pos, rot, hint, FID["chefs_tr"])
            player_docs.append(doc)
            player_children.append(tid)

        # 7) 地板瓦片（内置网格 + 工程材质）
        floor_docs = []
        ground_children = [FID["floor_tr"]]
        for f in mdl.floors:
            if not f.mat_refs:
                continue
            g, t, mr, mf = fg.next(), fg.next(), fg.next(), fg.next()
            floor_docs.append(go_yaml(g, f.name, [t, mr, mf]))
            floor_docs.append(tr_yaml(t, g, FID["ground_tr"], len(ground_children),
                                      f.pos, f.rot, f.scale, [],
                                      hint=SG.q_to_euler_unity(f.rot)))
            floor_docs.append(mesh_renderer_yaml(mr, g, f.mat_refs))
            floor_docs.append(mesh_filter_yaml(mf, g, f.mesh_fid))
            ground_children.append(t)

        # 8) 骨架
        skeleton = [
            go_yaml(FID["design_go"], "Design", [FID["design_tr"]]),
            tr_yaml(FID["design_tr"], FID["design_go"], 0, 1, (0, 0, 0),
                    SG.IDENT_Q, (1, 1, 1), design_children),
            go_yaml(FID["collision_go"], "Collision", [FID["collision_tr"]]),
            tr_yaml(FID["collision_tr"], FID["collision_go"], FID["design_tr"], 0,
                    (0, 0, 0), SG.IDENT_Q, (1, 1, 1), col_children),
            go_yaml(FID["utensils_go"], "Utensils", [FID["utensils_tr"]]),
            tr_yaml(FID["utensils_tr"], FID["utensils_go"], FID["design_tr"], 1,
                    (0, 0, 0), SG.IDENT_Q, (1, 1, 1), utensils),
            go_yaml(FID["counters_go"], "Counters", [FID["counters_tr"]]),
            tr_yaml(FID["counters_tr"], FID["counters_go"], FID["design_tr"], 2,
                    (0, 0, 0), SG.IDENT_Q, (1, 1, 1), counters),
            go_yaml(FID["chefs_go"], "Chefs", [FID["chefs_tr"]]),
            tr_yaml(FID["chefs_tr"], FID["chefs_go"], 0, 3, (0, 0, 0),
                    SG.IDENT_Q, (1, 1, 1), player_children),
            go_yaml(FID["art_go"], "Art", [FID["art_tr"]]),
            tr_yaml(FID["art_tr"], FID["art_go"], 0, 4, (0, 0, 0), SG.IDENT_Q,
                    (1, 1, 1), [FID["lights_tr"], FID["ground_tr"], scenery_tr]),
            go_yaml(FID["lights_go"], "Lights", [FID["lights_tr"]]),
            tr_yaml(FID["lights_tr"], FID["lights_go"], FID["art_tr"], 0,
                    (0, 0, 0), SG.IDENT_Q, (1, 1, 1), [FID["day_tr"]]),
            go_yaml(scenery_go, "Scenery", [scenery_tr]),
            tr_yaml(scenery_tr, scenery_go, FID["art_tr"], 2, (0, 0, 0),
                    SG.IDENT_Q, (1, 1, 1), scenery),
            go_yaml(FID["ground_go"], "Ground", [FID["ground_tr"]]),
            tr_yaml(FID["ground_tr"], FID["ground_go"], FID["art_tr"], 1,
                    (0, 0, 0), SG.IDENT_Q, (1, 1, 1), ground_children),
        ]

        # 9) 平行光
        dl = mdl.dir_light or {}
        light = light_yaml(
            FID["day_go"], FID["day_tr"], FID["day_light"], FID["lights_tr"],
            dl.get("rot", (0.78188676, 0.07891235, -0.2563485, 0.5627713)),
            dl.get("color", "{r: 1, g: 0.93333334, b: 0.7921569, a: 1}"),
            dl.get("intensity", "0.7"), dl.get("shadow", "0.629"))

        # 10) 缺省地板 quad（铺满网格，被真实地砖覆盖时仍保留作底）
        fw = 2 * mdl.grid_half[0] * mdl.cell / 10.0
        fh = 2 * mdl.grid_half[1] * mdl.cell / 10.0
        base_floor = [
            go_yaml(FID["floor_go"], "Floor",
                    [FID["floor_tr"], FID["floor_mr"], FID["floor_mf"]]),
            tr_yaml(FID["floor_tr"], FID["floor_go"], FID["ground_tr"], 0,
                    (mdl.grid_pos[0], -0.05, mdl.grid_pos[2]), SG.IDENT_Q,
                    (round(fw, 4), 1, round(fh, 4)), []),
            mesh_renderer_yaml(FID["floor_mr"], FID["floor_go"],
                               ["{fileID: 2100000, guid: %s, type: 2}" % FLOOR_MAT_GUID]),
            mesh_filter_yaml(FID["floor_mf"], FID["floor_go"], "10209"),
        ]

        # 11) 模板固定块：相机 / Debug / PseudoPrefabManager
        cam_block = _grab(tmpl, "--- !u!1 &420415744", "--- !u!1 &426971518")
        if mdl.cam_pos:
            cam_block = _replace_cam(cam_block, mdl.cam_pos)
        elif mdl.grid_pos:
            cam_block = _replace_cam(cam_block, (mdl.grid_pos[0], 22.0,
                                                 mdl.grid_pos[2] - 11.6))
        cam_child = _grab(tmpl, "--- !u!1 &2005137850", "--- !u!1001 &2031069185")
        debug_block = _grab(tmpl, "--- !u!1 &409915802", "--- !u!1 &420415744")
        mgr_block = _grab(tmpl, "--- !u!1 &975597275", "--- !u!1001 &1056758579")
        if level_info_guid:
            mgr_block = re.sub(
                r"levelInfo: \{fileID: 11400000, guid: [0-9a-f]+, type: 2\}",
                "levelInfo: {fileID: 11400000, guid: %s, type: 2}" % level_info_guid,
                mgr_block)
        if manager_patch:
            mgr_block = manager_patch(mgr_block)

        docs = out + skeleton + floor_col_docs + wall_docs + kp_docs
        docs += item_docs + player_docs
        docs.append(light)
        docs += base_floor + floor_docs
        docs += [cam_block, cam_child, debug_block, mgr_block]
        text = "\n".join(d.rstrip("\n") for d in docs if d) + "\n"
        return text, bundles

    # ------------------------------------------------------------------
    def _item(self, it, slots, parent_fid, order=0):
        info = it.plan.wrapper_info or {}
        pguid = info.get("guid") or ""
        tr = info.get("rootTr") or 0
        go = info.get("rootGo") or 0
        slot = slots[id(it)]
        iid, tid = slot["iid"], slot["tid"]
        rot = it.rot
        mods = [
            mod_line(tr, pguid, "m_LocalPosition.x", fnum(it.pos[0])),
            mod_line(tr, pguid, "m_LocalPosition.y", fnum(it.pos[1])),
            mod_line(tr, pguid, "m_LocalPosition.z", fnum(it.pos[2])),
            mod_line(tr, pguid, "m_LocalRotation.x", fnum(rot[0])),
            mod_line(tr, pguid, "m_LocalRotation.y", fnum(rot[1])),
            mod_line(tr, pguid, "m_LocalRotation.z", fnum(rot[2])),
            mod_line(tr, pguid, "m_LocalRotation.w", fnum(rot[3])),
            mod_line(tr, pguid, "m_RootOrder", str(order)),
            mod_line(go, pguid, "m_Name", it.name),
        ]
        if it.plan.so_guid and info.get("soFid"):
            mods.append(mod_line(info["soFid"], pguid, "pseudoPrefabSO", "",
                                 "{fileID: 11400000, guid: %s, type: 2}" % it.plan.so_guid))
        if not _is_one(it.scale):
            for i, ax in enumerate("xyz"):
                mods.append(mod_line(tr, pguid, "m_LocalScale.%s" % ax, fnum(it.scale[i])))
        ex, ey, ez = SG.q_to_euler_unity(rot)
        mods.append(mod_line(tr, pguid, "m_LocalEulerAnglesHint.x", fnum(round(ex, 4))))
        mods.append(mod_line(tr, pguid, "m_LocalEulerAnglesHint.y", fnum(round(ey, 4))))
        mods.append(mod_line(tr, pguid, "m_LocalEulerAnglesHint.z", fnum(round(ez, 4))))

        mods.extend(self._stub_mods(it, slots, pguid))

        doc = prefab_instance(iid, tid, pguid, tr, parent_fid, mods)
        extra = self._stripped_docs(it, slots)
        return doc, extra

    # ------------------------------------------------------------------
    def _stub_mods(self, it, slots, pguid):
        """静态玩法参数的 override。"""
        s = it.stub
        if not s:
            return []
        fid = s.get("fid") or 0
        if not fid:
            return []
        out = []
        for field, guid in (s.get("so") or {}).items():
            out.append(mod_line(fid, pguid, field, "",
                                "{fileID: 11400000, guid: %s, type: 2}" % guid))
        for field, val in (s.get("values") or {}).items():
            out.append(mod_line(fid, pguid, field, fnum(val)))
        for field, (mode, target) in (s.get("refs") or {}).items():
            if isinstance(target, list):
                out.append(mod_line(fid, pguid, field + ".Array.size", str(len(target))))
                for i, t in enumerate(target):
                    ref = self._ref_fid(t, slots, mode)
                    if ref:
                        out.append(mod_line(fid, pguid, "%s.Array.data[%d]" % (field, i),
                                            "", "{fileID: %d}" % ref))
            else:
                ref = self._ref_fid(target, slots, mode)
                if ref:
                    out.append(mod_line(fid, pguid, field, "", "{fileID: %d}" % ref))
        return out

    @staticmethod
    def _ref_fid(target, slots, mode):
        s = slots.get(id(target))
        if s is None:
            return 0
        if mode == "go":
            return s["go"]
        key = mode.split(":", 1)[1]
        return s["comp"].get(key, 0)

    @staticmethod
    def _stripped_docs(it, slots):
        """被别人引用的实例需要额外的 stripped GameObject / 组件文档。"""
        s = slots[id(it)]
        info = it.plan.wrapper_info or {}
        pguid = info.get("guid") or ""
        out = []
        if s["go"]:
            out.append("\n".join([
                "--- !u!1 &%d stripped" % s["go"], "GameObject:",
                "  m_PrefabParentObject: {fileID: %s, guid: %s, type: 2}"
                % (info.get("rootGo") or 0, pguid),
                "  m_PrefabInternal: {fileID: %d}" % s["iid"]]))
        for key, fid in s["comp"].items():
            script = STUB_GUIDS.get(key, "")
            comp_fid = (info.get("comps") or {}).get(script)
            if not comp_fid:
                continue
            out.append("\n".join([
                "--- !u!114 &%d stripped" % fid, "MonoBehaviour:",
                "  m_PrefabParentObject: {fileID: %s, guid: %s, type: 2}"
                % (comp_fid, pguid),
                "  m_PrefabInternal: {fileID: %d}" % s["iid"],
                "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % script]))
        return out


def _is_one(s, eps=1e-5):
    return all(abs(v - 1.0) < eps for v in s)


_CAM_RE = re.compile(r"m_LocalPosition: \{x: 5\.4, y: 22, z: -15\.2\}")


def _replace_cam(block, pos):
    rep = "m_LocalPosition: {x: %s, y: %s, z: %s}" % (
        fnum(round(pos[0], 4)), fnum(round(pos[1], 4)), fnum(round(pos[2], 4)))
    new, n = _CAM_RE.subn(rep, block, count=1)
    if n == 0:
        # 模板被改过：退回按第一处 m_LocalPosition 替换
        new = re.sub(r"m_LocalPosition: \{[^}]*\}", rep, block, count=1)
    return new


def scene_meta(guid, bundle=""):
    return SCENE_META_TMPL.format(guid=guid, bundle=bundle)
