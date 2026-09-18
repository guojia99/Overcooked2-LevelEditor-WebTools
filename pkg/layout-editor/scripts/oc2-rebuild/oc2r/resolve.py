"""索引之上的查询层：名字 -> prefab 容器、mesh guid -> 模型容器、material guid -> 材质容器。"""

import os
import re

from . import indices

_SUFFIX_PAREN = re.compile(r"\s*\(\d+\)$")
_SUFFIX_NUM = re.compile(r"\s+\d+$")


def name_variants(name):
    """按优先级给出候选名。精确名永远排第一：
    游戏里确实存在 `p_dlc07_keep_flagstone_01 (11).prefab` 这种把副本存成独立预制体的做法，
    上一版无条件剥掉 " (N)" 正是素材错配的根源之一。
    """
    out = []
    n = (name or "").strip()
    if not n:
        return out

    def add(x):
        x = x.strip()
        if x and x not in out:
            out.append(x)

    add(n)
    a = _SUFFIX_PAREN.sub("", n)
    add(a)
    b = _SUFFIX_NUM.sub("", a)
    add(b)
    c = _SUFFIX_NUM.sub("", n)
    add(c)
    return out


class Resolver(object):
    def __init__(self, force=False):
        self.pi, self.si, self.sc, self.li, self.ag = indices.build_all(force)
        self.prefabs = self.pi["byLowerName"]
        self.models = self.pi["byModelKey"]
        self.materials = self.pi["byMaterialKey"]
        self.meshes = self.pi["byMeshKey"]
        self.shapes = self.si["shapes"]
        self.scripts = self.sc["byGuid"]
        self.ar_guid = self.ag["byGuid"]
        self.so = self.li["so"]
        self.recipe_so = self.li.get("recipeSo") or {}
        self.so_by_name = self.li["byPrefabName"]
        self.wrappers = self.li["wrappers"]
        self.wrapper_by_so = self.li["wrapperBySo"]
        self.catalog_by_path = {}
        self.catalog_by_id = {}
        for it in self.li["catalog"]:
            if it.get("assetPath"):
                self.catalog_by_path[it["assetPath"]] = it
            self.catalog_by_id.setdefault(it["id"], it)
        self.appearances = self.li.get("appearances") or {}
        # ar 路径 -> prefab 容器 entry；容器（小写）-> ar 路径
        self.by_ar = {}
        self.ar_by_container = {}
        for lst in self.prefabs.values():
            for e in lst:
                if e.get("ar"):
                    self.by_ar[e["ar"]] = e
                    self.ar_by_container.setdefault(e["container"].lower(), e["ar"])
        self._mesh_to_prefabs = None
        self._childname_to_prefabs = None

    # -- 结构反查（名字对不上时用） ----------------------------------
    @property
    def mesh_to_prefabs(self):
        if self._mesh_to_prefabs is None:
            idx = {}
            for ar, sh in self.shapes.items():
                for g in set(sh.get("meshes") or []):
                    if g and not g.startswith("0000000000000000"):
                        idx.setdefault(g, []).append(ar)
            self._mesh_to_prefabs = idx
        return self._mesh_to_prefabs

    @property
    def childname_to_prefabs(self):
        if self._childname_to_prefabs is None:
            idx = {}
            for ar, sh in self.shapes.items():
                root = sh.get("root") or ""
                for n in set(sh.get("names") or []):
                    if n and n != root:
                        idx.setdefault(n, []).append(ar)
            self._childname_to_prefabs = idx
        return self._childname_to_prefabs

    def entry_for_ar(self, ar):
        return self.by_ar.get(ar)

    # -- prefab ------------------------------------------------------
    def prefab_candidates(self, name):
        """返回 [(用到的候选名, 容器 entry)]。"""
        res = []
        for v in name_variants(name):
            lst = self.prefabs.get(v.lower())
            if lst:
                for e in lst:
                    res.append((v, e))
                # 命中即停：更长的名字优先级更高
                break
        return res

    def shape_of(self, entry):
        return self.shapes.get(entry.get("ar") or "")

    # -- 模型 / 网格 / 材质 -------------------------------------------
    def ar_path(self, guid):
        return self.ar_guid.get(guid, "")

    @staticmethod
    def _key_of_ar(rel):
        if not rel:
            return ""
        d = os.path.dirname(rel).lower()
        stem = os.path.splitext(os.path.basename(rel))[0].lower()
        return ("assets/" + d + "/" + stem) if d else ("assets/" + stem)

    def model_for_mesh_guid(self, mesh_guid):
        """场景里的 mesh 引用 -> bundle 里可 LoadAsset<GameObject> 的模型容器。"""
        rel = self.ar_path(mesh_guid)
        if not rel:
            return None
        key = self._key_of_ar(rel)
        lst = self.models.get(key)
        if lst:
            return lst[0]
        return None

    def mesh_container_for_guid(self, mesh_guid):
        rel = self.ar_path(mesh_guid)
        if not rel:
            return None
        lst = self.meshes.get(self._key_of_ar(rel))
        return lst[0] if lst else None

    def material_for_guid(self, mat_guid):
        rel = self.ar_path(mat_guid)
        if not rel:
            return None
        lst = self.materials.get(self._key_of_ar(rel))
        return lst[0] if lst else None

    def material_name(self, mat_guid):
        rel = self.ar_path(mat_guid)
        if not rel:
            return ""
        return os.path.splitext(os.path.basename(rel))[0]

    # -- 脚本 --------------------------------------------------------
    def script_class(self, guid):
        e = self.scripts.get(guid)
        return e["cls"] if e else ""

    # -- 素材库 ------------------------------------------------------
    def so_guids_for_prefab_name(self, name):
        return self.so_by_name.get((name or "").lower(), [])

    def so_info(self, guid):
        return self.so.get(guid)

    def wrappers_for_so(self, so_guid):
        return self.wrapper_by_so.get(so_guid, [])

    def catalog(self, asset_path):
        return self.catalog_by_path.get(asset_path)
