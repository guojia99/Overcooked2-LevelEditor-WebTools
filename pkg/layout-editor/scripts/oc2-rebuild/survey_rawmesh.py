"""阶段 0：裸网格 / 材质盘点。

统计三类"不是 prefab 也不是模型实例"的渲染节点，给出还原方案与提取预算：
  A. 内置网格（Quad/Plane/Cube…）+ bundle 材质  -> 走编辑器地板体系，需要把材质提取进工程
  B. bundle 模型网格但没有可加载的 GameObject 容器
  C. 场景内嵌网格（AssetRipper 的 Mesh/ 目录）—— 多为烘焙/合批产物

用法：
    python3 survey_rawmesh.py [--set all]
"""

import argparse
import json
import os
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from oc2r import levels as LV
from oc2r import matcher as M
from oc2r import paths
from oc2r import resolve
from oc2r import scene_graph as SG
from oc2r import structure as ST
from oc2r import yamlscene as Y

BUILTIN_MESH = {
    "10202": "Cube", "10206": "Cylinder", "10207": "Capsule",
    "10208": "Sphere", "10209": "Plane", "10210": "Quad",
    "10211": "Plane?", "10205": "Cone",
}


def mesh_ref(node):
    d = node.comp(SG.CLS_MESHFILTER) or node.comp(SG.CLS_SKINNEDMESHRENDERER)
    if d is None:
        return None, None
    m = d.data.get("m_Mesh", {})
    return str(Y.ref_fid(m)), Y.ref_guid(m)


def mats_of(node):
    d = node.comp(SG.CLS_MESHRENDERER) or node.comp(SG.CLS_SKINNEDMESHRENDERER)
    if d is None:
        return []
    lst = d.data.get("m_Materials")
    if not isinstance(lst, list):
        return []
    return [Y.ref_guid(x) for x in lst if isinstance(x, dict) and Y.ref_guid(x)]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--set", default="all", choices=["dlc", "base", "all"])
    args = ap.parse_args()

    R = resolve.Resolver()
    lvs = []
    if args.set in ("dlc", "all"):
        lvs += LV.dlc_levels()
    if args.set in ("base", "all"):
        lvs += LV.base_levels()

    builtin_by_type = Counter()
    builtin_mat = Counter()
    builtin_mat_bundle = {}
    builtin_levels = defaultdict(set)
    nomodel = Counter()
    nomodel_sample = {}
    embedded = Counter()
    embedded_sample = {}

    for lv in lvs:
        src = lv.get("sourceScene")
        if not src or not os.path.exists(src):
            continue
        sc = SG.Scene(src)
        mt = M.Matcher(R, prefer_tokens=lv.get("preferTokens"))

        def on(m, lv=lv):
            if m.kind != "drop":
                return
            n = m.node
            fid, guid = mesh_ref(n)
            if m.detail == "builtin-mesh":
                builtin_by_type[BUILTIN_MESH.get(fid, "builtin:" + str(fid))] += 1
                for g in mats_of(n):
                    nm = R.material_name(g) or g
                    builtin_mat[nm] += 1
                    builtin_levels[nm].add(lv["id"])
                    if nm not in builtin_mat_bundle:
                        c = R.material_for_guid(g)
                        builtin_mat_bundle[nm] = c["bundle"] + " :: " + c["container"] if c else ""
            elif m.detail == "mesh-no-model-container":
                rel = R.ar_path(guid)
                nomodel[rel] += 1
                nomodel_sample.setdefault(rel, n.path)
            elif m.detail in ("scene-embedded-mesh", "mesh-unresolved"):
                rel = R.ar_path(guid) or ("guid:" + (guid or ""))
                embedded[rel] += 1
                embedded_sample.setdefault(rel, n.path)

        for r in ST.classify_roots(sc)["objects"]:
            wl, _sp = ST.object_subroots(sc, r)
            for w in wl:
                mt.walk(w, on)

    print("=== A. 内置网格节点（走地板体系） ===")
    print("总数:", sum(builtin_by_type.values()), dict(builtin_by_type))
    print("需要提取进工程的材质: %d 种" % len(builtin_mat))
    for nm, cnt in builtin_mat.most_common():
        print("   %-44s %6d 实例  %2d 关  %s"
              % (nm[:44], cnt, len(builtin_levels[nm]), builtin_mat_bundle.get(nm, "")[:60]))

    print("\n=== B. bundle 模型网格但无 GameObject 容器 ===")
    print("总数:", sum(nomodel.values()), " 不同网格:", len(nomodel))
    for rel, cnt in nomodel.most_common(30):
        print("   %-58s %5d  %s" % ((rel or "?")[-58:], cnt, nomodel_sample.get(rel, "")[-50:]))

    print("\n=== C. 场景内嵌网格（烘焙/合批产物） ===")
    print("总数:", sum(embedded.values()), " 不同网格:", len(embedded))
    for rel, cnt in embedded.most_common(20):
        print("   %-58s %5d  %s" % ((rel or "?")[-58:], cnt, embedded_sample.get(rel, "")[-50:]))

    out = {
        "builtinByType": dict(builtin_by_type),
        "builtinMaterials": [
            {"name": nm, "count": c, "levels": sorted(builtin_levels[nm]),
             "source": builtin_mat_bundle.get(nm, "")}
            for nm, c in builtin_mat.most_common()
        ],
        "noModelContainer": [{"mesh": k, "count": v, "sample": nomodel_sample.get(k, "")}
                             for k, v in nomodel.most_common()],
        "sceneEmbedded": [{"mesh": k, "count": v, "sample": embedded_sample.get(k, "")}
                          for k, v in embedded.most_common()],
    }
    p = paths.out("rawmesh.json")
    with open(p, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    print("\n报告:", p)


if __name__ == "__main__":
    main()
