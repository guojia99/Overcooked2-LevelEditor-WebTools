"""量化"父级非均匀缩放 + 子级旋转"导致的 TRS 近似误差。

Unity 的 Transform 只能存 TRS，父级非均匀缩放遇上子级旋转时，
真实的 4x4 变换无法用 TRS 精确表达。本脚本对这类物件计算
"真实矩阵" 与 "TRS 近似" 在物体包围盒 8 个角上的最大偏差，
用来判断这批物件是否需要单独处理。

    python3 survey_skew.py [--set all]
"""

import argparse
import json
import os
import sys
from collections import Counter

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from oc2r import levels as LV
from oc2r import matcher as M
from oc2r import paths
from oc2r import resolve
from oc2r import scene_graph as SG
from oc2r import structure as ST


def trs(pos, rot, scale):
    x, y, z, w = rot
    r = np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y)],
        [2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x)],
        [2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y)],
    ])
    m = np.eye(4)
    m[:3, :3] = r * np.array(scale)
    m[:3, 3] = pos
    return m


def true_matrix(node):
    m = node.mat
    out = np.eye(4)
    out[0, :] = m[0:4]
    out[1, :] = m[4:8]
    out[2, :] = m[8:12]
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--set", default="all", choices=["dlc", "base", "all"])
    ap.add_argument("--extent", type=float, default=0.6,
                    help="物件半径假设（米），用于把矩阵误差折算成位移误差")
    args = ap.parse_args()

    R = resolve.Resolver()
    lvs = []
    if args.set in ("dlc", "all"):
        lvs += LV.dlc_levels()
    if args.set in ("base", "all"):
        lvs += LV.base_levels()

    e = args.extent
    corners = np.array([[sx * e, sy * e, sz * e, 1.0]
                        for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]).T

    buckets = Counter()
    worst = []
    total = 0
    skew_total = 0
    for lv in lvs:
        src = lv.get("sourceScene")
        if not src or not os.path.exists(src):
            continue
        sc = SG.Scene(src)
        mt = M.Matcher(R, prefer_tokens=lv.get("preferTokens"))
        items = []

        def on(m):
            if m.kind in ("prefab", "model"):
                items.append(m.node)

        for r in ST.classify_roots(sc)["objects"]:
            wl, _sp = ST.object_subroots(sc, r)
            for w in wl:
                mt.walk(w, on)

        for n in items:
            total += 1
            if not n.skewed:
                continue
            skew_total += 1
            mt_true = true_matrix(n)
            mt_apx = trs(n.wpos, SG.q_norm(n.wrot), n.wscale)
            d = np.abs((mt_true @ corners) - (mt_apx @ corners))[:3]
            err = float(d.max())
            if err < 0.01:
                buckets["<1cm"] += 1
            elif err < 0.05:
                buckets["1~5cm"] += 1
            elif err < 0.2:
                buckets["5~20cm"] += 1
            else:
                buckets[">20cm"] += 1
                worst.append((round(err, 3), lv["id"], n.path))

    print("物件总数 %d，其中 TRS 不可精确表达 %d（%.2f%%）"
          % (total, skew_total, 100.0 * skew_total / max(1, total)))
    print("误差分布（按物件半径 %.2f m 折算）：" % e, dict(buckets))
    worst.sort(reverse=True)
    print("\n误差最大的 20 个：")
    for err, lid, path in worst[:20]:
        print("   %6.3f m  %-18s %s" % (err, lid, path[-70:]))
    with open(paths.out("skew.json"), "w", encoding="utf-8") as f:
        json.dump({"total": total, "skewed": skew_total, "buckets": dict(buckets),
                   "worst": worst[:200]}, f, ensure_ascii=False, indent=1)
    print("\n报告:", paths.out("skew.json"))


if __name__ == "__main__":
    main()
