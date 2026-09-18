"""诊断单关的匹配决策明细。

    python3 probe_level.py OC2_DLC02_1_1 [--kind drop] [--reason mesh-no-model-container] [--n 40]
"""

import argparse
import os
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from oc2r import levels as LV
from oc2r import matcher as M
from oc2r import resolve
from oc2r import scene_graph as SG
from oc2r import structure as ST
from oc2r import yamlscene as Y


def mesh_info(R, node):
    d = node.comp(SG.CLS_MESHFILTER) or node.comp(SG.CLS_SKINNEDMESHRENDERER)
    if d is None:
        return ""
    g = Y.ref_guid(d.data.get("m_Mesh", {}))
    if not g:
        return ""
    return R.ar_path(g) or ("guid:" + g)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("level")
    ap.add_argument("--kind", default="drop")
    ap.add_argument("--reason", default="")
    ap.add_argument("--n", type=int, default=40)
    args = ap.parse_args()

    R = resolve.Resolver()
    lvs = {l["id"]: l for l in LV.dlc_levels() + LV.base_levels()}
    lv = lvs[args.level]
    sc = SG.Scene(lv["sourceScene"])
    mt = M.Matcher(R, prefer_tokens=lv.get("preferTokens"))

    hits = []
    byreason = Counter()

    def on_match(m):
        if m.kind != args.kind:
            return
        byreason[m.detail] += 1
        if args.reason and m.detail != args.reason:
            return
        hits.append(m)

    for r in ST.classify_roots(sc)["objects"]:
        wl, _sp = ST.object_subroots(sc, r)
        for w in wl:
            mt.walk(w, on_match)

    print("kind=%s 合计 %d" % (args.kind, sum(byreason.values())))
    for k, v in byreason.most_common():
        print("   %-28s %d" % (k, v))
    print()
    names = Counter(m.node.name for m in hits)
    print("按名字聚合 Top:")
    for k, v in names.most_common(25):
        print("   %-44s %d" % (k, v))
    print()
    seen = set()
    shown = 0
    for m in hits:
        key = M.base_name(m.node.name)
        if key in seen:
            continue
        seen.add(key)
        shown += 1
        if shown > args.n:
            break
        if m.kind in ("prefab", "model"):
            print("%-46s -> %-58s score=%s n=%d %s"
                  % (m.node.name[:46], m.entry["container"][-58:],
                     ("%.2f" % m.score) if m.score is not None else "  - ",
                     m.candidates, m.detail))
        else:
            print("%-60s | %-22s | mesh=%s"
                  % (m.node.path[-60:], m.detail, mesh_info(R, m.node)))


if __name__ == "__main__":
    main()
