"""关卡清单：DLC 87 关 + 本体 45 关。

DLC 清单沿用上一版 scan-levels.py 的产物（它是从 bundle 的 coopgamescenedirectory
里读出来的，关卡编号/星级/配置指针这部分没有问题），但源场景路径统一重新解析。
本体清单直接从 dump_bundle 解出的 coopgamescenedirectory JSON 读。
"""

import json
import os
import re

from . import paths

_OLD_LEVELS = paths.repo("layout-editor/scripts/oc2-import/out/levels.json")
_BASE_DIR_JSON = os.path.join(
    paths.DUMP, "Assets", "data", "datafile", "coopgamescenedirectory.asset.json"
)

_scene_index = None


def scene_index():
    """AssetRipper 导出里所有 .unity 的 basename(小写) -> [绝对路径]。"""
    global _scene_index
    if _scene_index is not None:
        return _scene_index
    idx = {}
    for root, _dirs, files in os.walk(paths.AR_ASSETS):
        for fn in files:
            if fn.endswith(".unity"):
                idx.setdefault(fn[: -len(".unity")].lower(), []).append(os.path.join(root, fn))
    _scene_index = idx
    return idx


def find_scene(scene_name):
    lst = scene_index().get((scene_name or "").lower())
    if not lst:
        return None
    # 同名多份时优先非 test / 非 backup 的
    lst = sorted(lst, key=lambda p: ("test" in p.lower(), len(p)))
    return lst[0]


def dlc_levels():
    with open(_OLD_LEVELS, encoding="utf-8") as f:
        data = json.load(f)
    out = []
    for lv in data["levels"]:
        src = paths.normalize_source_scene(lv.get("sourceScene") or "")
        if not src:
            src = find_scene(lv.get("sceneName") or "")
        out.append({
            "set": "oc2_dlc_story",
            "id": lv["id"],
            "dlc": lv.get("dlc"),
            "label": lv.get("label"),
            "world": lv.get("world"),
            "order": lv.get("order"),
            "hidden": lv.get("hidden", False),
            "sceneName": lv.get("sceneName"),
            "editorSceneName": lv.get("editorSceneName"),
            "theme": lv.get("theme"),
            "sourceScene": src,
            "variants": lv.get("variants") or {},
            "preferTokens": [lv.get("dlc") or ""],
        })
    return out


_KEVIN_RE = re.compile(r"KevinLevel(\d+)$")
_LEVEL_RE = re.compile(r"Level(\d+)$")


def base_levels(include_throne=False):
    with open(_BASE_DIR_JSON, encoding="utf-8") as f:
        d = json.load(f)
    out = []
    order = 0
    for e in d.get("Scenes", []):
        label = e.get("Label") or ""
        if "ThroneRoom" in label and not include_throne:
            continue
        variants = e.get("SceneVarients") or []
        names = [v.get("SceneName") for v in variants if v.get("SceneName")]
        if not names:
            continue
        scene_name = names[0]
        src = find_scene(scene_name)
        short = label.rsplit(".", 1)[-1]
        m = _KEVIN_RE.search(short)
        if m:
            lid = "OC2_Story_H%s" % m.group(1)
        else:
            m = _LEVEL_RE.search(short)
            if m:
                lid = "OC2_Story_%02d" % int(m.group(1))
            elif "Tutorial" in short:
                lid = "OC2_Story_00_Tutorial"
            else:
                lid = "OC2_Story_" + re.sub(r"[^A-Za-z0-9_]", "_", short)
        order += 1
        out.append({
            "set": "oc2_story",
            "id": lid,
            "dlc": "base",
            "label": label,
            "world": e.get("World"),
            "order": order,
            "hidden": bool(int(e.get("IsHidden") or 0)),
            "sceneName": scene_name,
            "editorSceneName": "s_" + lid.lower(),
            "theme": _theme_of(scene_name),
            "sourceScene": src,
            "variants": _base_variants(variants),
            "preferTokens": [],
        })
    return out


def _theme_of(scene_name):
    s = (scene_name or "").lower()
    for t in ("sushi", "wizard", "balloon", "city", "mine", "rapids", "space",
              "dynamic", "movingplatform", "tutorial"):
        if t in s:
            return t
    return ""


def _base_variants(variants):
    out = {}
    for v in variants:
        pc = str(v.get("PlayerCount") or "")
        if not pc:
            continue
        sb = v.get("m_PCStarBoundaries") or {}
        out[pc] = {
            "sceneName": v.get("SceneName"),
            "stars": {
                "one": sb.get("m_OneStarScore"),
                "two": sb.get("m_TwoStarScore"),
                "three": sb.get("m_ThreeStarScore"),
                "four": sb.get("m_FourStarScore"),
            },
            "levelConfigPid": (v.get("LevelConfig") or {}).get("m_PathID"),
            "levelConfigFid": (v.get("LevelConfig") or {}).get("m_FileID"),
        }
    return out


def all_levels():
    return dlc_levels() + base_levels()
