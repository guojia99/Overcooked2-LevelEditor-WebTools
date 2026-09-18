"""阶段 0：关卡截图指针审计。

上一版脚本优先取 SceneVarients[].Screenshot，而 DLC09 五关的该指针指向同一张通用图，
导致 87 张截图只有 83 张唯一。本脚本把每关的两个指针都列出来，
统计哪个能做到"一关一图"，作为重建时的取图依据。

数据源是 dump_bundle 解出的 *coopgamescenedirectory JSON，不需要 UnityPy。
用法：python3 survey_screenshots.py
"""

import glob
import json
import os
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from oc2r import levels as LV
from oc2r import paths

EXCLUDE = ("throneroom", "vs", "competitive")


def ptr(d):
    """两种 dump 外形的指针归一化：
      A) {"m_FileID":0,"m_PathID":123}
      B) {"fileID":123,"guid":"000..0","type":0}
    """
    if not isinstance(d, dict):
        return None
    if "m_PathID" in d:
        p = d.get("m_PathID")
        if not p:
            return None
        return "%s:%s" % (int(d.get("m_FileID") or 0), int(p))
    if "fileID" in d:
        f = d.get("fileID")
        try:
            f = int(f)
        except (TypeError, ValueError):
            return None
        if not f:
            return None
        g = (d.get("guid") or "").strip("0")
        return "%s:%s" % (g or 0, f)
    return None


def load_dirs():
    """所有 coop 场景目录 JSON -> {路径: 解析后的 entries}"""
    out = {}
    pats = [
        os.path.join(paths.DUMP, "Assets", "data", "datafile", "coopgamescenedirectory*.json"),
        os.path.join(paths.DUMP, "Assets", "downloadablecontent", "*", "dlc_assets", "data",
                     "*coopgamescenedirectory*.json"),
        os.path.join(paths.DUMP, "Assets", "downloadablecontent", "*", "assets", "data",
                     "*coopgamescenedirectory*.json"),
        os.path.join(paths.DUMP, "Assets", "data", "**", "*coopgamescenedirectory*.json"),
    ]
    seen = set()
    for pat in pats:
        for p in glob.glob(pat, recursive=True):
            if p in seen:
                continue
            seen.add(p)
            try:
                with open(p, encoding="utf-8") as f:
                    d = json.load(f)
            except (OSError, ValueError, UnicodeDecodeError):
                continue
            # dump 有两种外形：直接是 MonoBehaviour 字段，或再包一层 {"MonoBehaviour": {...}}
            if isinstance(d, dict) and isinstance(d.get("MonoBehaviour"), dict):
                d = d["MonoBehaviour"]
            if isinstance(d, dict) and isinstance(d.get("Scenes"), list):
                out[p] = d["Scenes"]
    return out


def main():
    dirs = load_dirs()
    print("找到场景目录 JSON: %d 份" % len(dirs))

    # 场景名 -> [(目录文件, entry)]
    by_scene = defaultdict(list)
    for p, scenes in dirs.items():
        for e in scenes:
            for v in (e.get("SceneVarients") or []):
                sn = v.get("SceneName")
                if sn:
                    by_scene[sn].append((p, e))

    lvs = LV.dlc_levels() + LV.base_levels()
    rows = []
    for lv in lvs:
        sn = lv.get("sceneName")
        hits = by_scene.get(sn) or []
        # 同名多 entry 时优先来自同 DLC 目录的
        dlc = (lv.get("dlc") or "").lower()
        hits_sorted = sorted(hits, key=lambda x: (dlc not in x[0].lower(), len(x[0])))
        if not hits_sorted:
            rows.append({"id": lv["id"], "set": lv["set"], "scene": sn,
                         "dirs": 0, "shot": None, "lso": None})
            continue
        p, e = hits_sorted[0]
        shot = None
        for v in (e.get("SceneVarients") or []):
            q = ptr(v.get("Screenshot"))
            if q:
                shot = q
                break
        rows.append({
            "id": lv["id"], "set": lv["set"], "scene": sn,
            "dirs": len(hits), "dir": os.path.relpath(p, paths.DUMP),
            "label": e.get("Label"),
            "shot": shot,
            "lso": ptr(e.get("LoadScreenOverride")),
        })

    for setname in ("oc2_dlc_story", "oc2_story"):
        sub = [r for r in rows if r["set"] == setname]
        if not sub:
            continue
        print("\n===== %s（%d 关）=====" % (setname, len(sub)))
        for key in ("shot", "lso"):
            vals = [r[key] for r in sub]
            none_n = sum(1 for v in vals if not v)
            c = Counter(v for v in vals if v)
            dup = {k: n for k, n in c.items() if n > 1}
            print("  %-4s 唯一 %3d / 非空 %3d / 空 %d；重复指针 %d 组，涉及 %d 关"
                  % (key, len(c), len(vals) - none_n, none_n, len(dup), sum(dup.values())))
            for k, n in sorted(dup.items(), key=lambda x: -x[1])[:5]:
                ids = [r["id"] for r in sub if r[key] == k]
                print("        %s x%d -> %s" % (k, n, ids))
        # 推荐策略：优先 lso，lso 空则 shot
        best = []
        for r in sub:
            best.append(r["lso"] or r["shot"])
        c = Counter(v for v in best if v)
        dup = {k: n for k, n in c.items() if n > 1}
        print("  推荐(lso 优先, 缺则 shot): 唯一 %d / 非空 %d，重复 %d 组"
              % (len(c), sum(1 for v in best if v), len(dup)))
        for k, n in sorted(dup.items(), key=lambda x: -x[1])[:6]:
            ids = [sub[i]["id"] for i, v in enumerate(best) if v == k]
            print("        %s x%d -> %s" % (k, n, ids))
        multi = [r["id"] for r in sub if r["dirs"] > 1]
        if multi:
            print("  场景名在多份目录里出现（需按 DLC 归属消歧）: %d 关 %s" % (len(multi), multi[:8]))
        missing = [r["id"] for r in sub if not r["shot"] and not r["lso"]]
        if missing:
            print("  两个指针都空: %s" % missing)

    p = paths.out("screenshot_audit.json")
    with open(p, "w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1, default=str)
    print("\n报告:", p)


if __name__ == "__main__":
    main()
