"""场景图自检：世界变换、层级、性能。"""

import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from oc2r import paths
from oc2r.scene_graph import Scene


def main():
    p = paths.ar("downloadablecontent/dlc02/dlc_assets/Scenes/01_Beach/s_beach_1_1.unity")
    t0 = time.time()
    sc = Scene(p)
    dt = time.time() - t0
    print("%s  解析耗时 %.2fs" % (sc, dt))
    print("roots:", [r.name for r in sc.roots])

    kp = sc.find_path("Design/KillPlanes")
    if kp:
        print("\nDesign/KillPlanes 组自身 local=%s world=%s" % (fmt(kp.lpos), fmt(kp.wpos)))
        for c in kp.children[:6]:
            print("   %-24s local=%-28s world=%-28s delta=%.3f"
                  % (c.name, fmt(c.lpos), fmt(c.wpos), dist(c.lpos, c.wpos)))

    sn = sc.find_path("Art/Scenery")
    if sn:
        print("\nArt/Scenery 子组抽样（验证父变换是否被计入）")
        for c in sn.children[:4]:
            print("   组 %-20s local=%-26s children=%d" % (c.name, fmt(c.lpos), len(c.children)))
            for g in c.children[:3]:
                print("       %-30s local=%-26s world=%-26s delta=%.3f"
                      % (g.name, fmt(g.lpos), fmt(g.wpos), dist(g.lpos, g.wpos)))

    # 统计偏移
    n_total = 0
    n_off = 0
    maxd = 0.0
    for n in sc.iter_nodes():
        n_total += 1
        d = dist(n.lpos, n.wpos)
        if d > 1e-4:
            n_off += 1
        maxd = max(maxd, d)
    print("\n节点总数 %d，local!=world 的 %d（%.1f%%），最大差 %.2f m"
          % (n_total, n_off, 100.0 * n_off / max(1, n_total), maxd))

    skew = sum(1 for n in sc.iter_nodes() if n.skewed)
    print("父级非均匀缩放+子级旋转（TRS 不可精确表达）节点数：", skew)


def fmt(v):
    return "(%.3f, %.3f, %.3f)" % v


def dist(a, b):
    return ((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2) ** 0.5


if __name__ == "__main__":
    main()
