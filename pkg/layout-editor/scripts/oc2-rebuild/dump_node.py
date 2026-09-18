"""打印某个节点子树的结构：组件、脚本类名、mesh/material 归属。

    python3 dump_node.py OC2_DLC02_1_1 "DispenserCrate" [--depth 4]
"""

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from oc2r import levels as LV
from oc2r import resolve
from oc2r import scene_graph as SG
from oc2r import yamlscene as Y

CLS_NAME = {
    1: "GameObject", 4: "Transform", 20: "Camera", 23: "MeshRenderer", 33: "MeshFilter",
    54: "Rigidbody", 64: "MeshCollider", 65: "BoxCollider", 108: "Light", 114: "MonoBehaviour",
    135: "SphereCollider", 136: "CapsuleCollider", 137: "SkinnedMeshRenderer",
    198: "ParticleSystem", 199: "ParticleSystemRenderer", 95: "Animator", 111: "Animation",
    212: "SpriteRenderer", 224: "RectTransform",
}


def describe(R, n):
    bits = []
    for cls, d in n.comps:
        if cls == SG.CLS_MONOBEHAVIOUR:
            g = Y.ref_guid(d.data.get("m_Script", {}))
            bits.append("MB:" + (R.script_class(g) or g[:8]))
        elif cls in (SG.CLS_MESHFILTER, SG.CLS_SKINNEDMESHRENDERER):
            g = Y.ref_guid(d.data.get("m_Mesh", {}))
            bits.append("%s(%s)" % (CLS_NAME.get(cls, cls), R.ar_path(g) or g[:8] or "-"))
        elif cls == SG.CLS_MESHRENDERER:
            mats = d.data.get("m_Materials") or []
            names = [R.material_name(Y.ref_guid(m)) or Y.ref_guid(m)[:8] for m in mats if isinstance(m, dict)]
            bits.append("MR[%s]" % ",".join(names))
        elif cls in (SG.CLS_TRANSFORM, SG.CLS_RECTTRANSFORM):
            continue
        else:
            bits.append(CLS_NAME.get(cls, str(cls)))
    return " ".join(bits)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("level")
    ap.add_argument("needle")
    ap.add_argument("--depth", type=int, default=4)
    ap.add_argument("--max", type=int, default=3)
    args = ap.parse_args()

    R = resolve.Resolver()
    lvs = {l["id"]: l for l in LV.dlc_levels() + LV.base_levels()}
    lv = lvs[args.level]
    sc = SG.Scene(lv["sourceScene"])

    found = 0
    for n in sc.iter_nodes():
        if args.needle.lower() not in n.name.lower():
            continue
        found += 1
        if found > args.max:
            break
        print("=" * 100)
        print("路径:", n.path)
        print("world pos=%s  rotY=%.1f  scale=%s  skewed=%s"
              % (fmt(n.wpos), SG.q_to_euler_unity(n.wrot)[1], fmt(n.wscale), n.skewed))
        stack = [(n, 0)]
        while stack:
            cur, d = stack.pop()
            print("   " + "  " * d + ("- %s  | %s" % (cur.name, describe(R, cur))))
            if d < args.depth:
                for c in reversed(cur.children):
                    stack.append((c, d + 1))
    if not found:
        print("没找到含 %r 的节点" % args.needle)


def fmt(v):
    return "(%.2f, %.2f, %.2f)" % v


if __name__ == "__main__":
    main()
