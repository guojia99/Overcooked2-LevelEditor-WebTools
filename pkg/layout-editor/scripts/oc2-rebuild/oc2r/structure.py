"""场景结构分层：把根节点/子树分成"物件通道"与各专用通道。

专用通道（由后续独立模块处理，不走通用物件匹配）：
  env        CampaignGameEnvironment / DynamicCampaignGameEnvironment / HordeGameEnvironment
  camera     MultiplayerGameCamera
  collision  Design/Collision
  killplane  Design/KillPlanes
  lights     */Lights, */Spotlights
  bakers     Lightmap_*_Baker（mesh baker 合并网格，是造景的渲染优化重复体，整棵丢弃）
  chefs      Chefs
"""

import re

from . import scene_graph as SG

BAKER_RE = re.compile(r"^(lightmap_-?\d+_baker|combinedmesh[-_].*)$", re.I)
ENV_NAMES_RE = re.compile(r"gameenvironment$", re.I)
LIGHT_GROUP_NAMES = {"lights", "spotlights", "light probes", "reflection probes"}


def find_env_root(scene):
    """按结构找环境根：带 GridManager 子节点的根，而不是按硬编码名字。

    上一版写死 find_root("CampaignGameEnvironment")，导致 4 关（两个 SP、两个 Horde）
    整关退化成默认网格 9x5。
    """
    # 1) 结构特征：子节点里有 GridManager
    for r in scene.roots:
        for c in r.children:
            if c.name.strip().lower() == "gridmanager":
                return r
    # 2) 名字兜底
    for r in scene.roots:
        if ENV_NAMES_RE.search(r.name.strip()):
            return r
    # 3) 整个场景里搜 GridManager
    for n in scene.iter_nodes():
        if n.name.strip().lower() == "gridmanager":
            return n.parent or n
    return None


def find_camera_root(scene):
    for r in scene.roots:
        if "camera" in r.name.lower():
            return r
    for n in scene.iter_nodes():
        if n.has(SG.CLS_CAMERA):
            return n
    return None


def classify_roots(scene):
    """返回 {通道: [节点]}。"""
    env = find_env_root(scene)
    cam = find_camera_root(scene)
    out = {
        "env": [env] if env else [],
        "camera": [cam] if cam else [],
        "bakers": [],
        "objects": [],
        "chefs": [],
    }
    for r in scene.roots:
        if r is env or r is cam:
            continue
        if BAKER_RE.match(r.name.strip()):
            out["bakers"].append(r)
            continue
        if r.name.strip().lower() == "chefs":
            out["chefs"].append(r)
            continue
        out["objects"].append(r)
    return out


def object_subroots(scene, root):
    """物件通道要遍历的子树集合；专用子树（碰撞/表杀/灯光）被剔除并单列。"""
    special = {"collision": [], "killplane": [], "lights": []}
    walk_list = []
    name = root.name.strip().lower()
    if name == "design":
        for c in root.children:
            n = c.name.strip().lower()
            if n == "collision":
                special["collision"].append(c)
            elif n in ("killplanes", "killplane"):
                special["killplane"].append(c)
            elif n in LIGHT_GROUP_NAMES:
                special["lights"].append(c)
            else:
                walk_list.append(c)
    elif name == "art":
        for c in root.children:
            n = c.name.strip().lower()
            if n in LIGHT_GROUP_NAMES:
                special["lights"].append(c)
            elif BAKER_RE.match(n):
                pass
            else:
                walk_list.append(c)
    else:
        walk_list.append(root)
    return walk_list, special


PLAYER_RE = re.compile(r"^player\s*(\d+)$", re.I)


def player_nodes(scene, chefs_roots):
    """厨师出生点：Chefs/Player N。整棵 Avatar 子树不进通用匹配。"""
    out = []
    for r in chefs_roots:
        for c in r.children:
            m = PLAYER_RE.match(c.name.strip())
            if m:
                out.append((int(m.group(1)), c))
    if not out:
        for n in scene.iter_nodes():
            m = PLAYER_RE.match(n.name.strip())
            if m:
                out.append((int(m.group(1)), n))
    out.sort(key=lambda x: x[0])
    return out
