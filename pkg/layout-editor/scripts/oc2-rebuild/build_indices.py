"""构建四张权威索引。

用法：
    python3 build_indices.py [--force]
"""

import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from oc2r import indices, paths


def main():
    force = "--force" in sys.argv
    print("仓库:", paths.REPO)
    print("反编译导出:", paths.AR)
    print("dump_bundle:", paths.DUMP)
    print()

    t0 = time.time()
    pi = indices.build_prefab_index(force)
    print("1/4 prefab 索引: %d 个唯一名 / %d 条容器  (%.1fs)"
          % (pi["names"], pi["count"], time.time() - t0))
    amb = sum(1 for v in pi["byLowerName"].values() if len(v) > 1)
    no_ar = sum(1 for v in pi["byLowerName"].values() for e in v if not e["ar"])
    print("     同名多容器: %d 个键；manifest 有但反编译导出里找不到 .prefab: %d 条" % (amb, no_ar))

    t0 = time.time()
    si = indices.build_shape_index(pi, force)
    print("2/4 结构签名索引: %d 个 prefab  (%.1fs)" % (si["count"], time.time() - t0))

    t0 = time.time()
    sc = indices.build_script_index(force)
    print("3/4 脚本索引: %d 个 guid->类名  (%.1fs)" % (sc["count"], time.time() - t0))

    t0 = time.time()
    li = indices.build_lib_index(force)
    print("4/4 素材库索引: %d 个 PseudoPrefabSO / %d 个 wrapper 预制体 / %d 条 catalog  (%.1fs)"
          % (li["soCount"], len(li["wrappers"]), len(li["catalog"]), time.time() - t0))
    multi = sum(1 for v in li["byPrefabName"].values() if len(v) > 1)
    print("     同 prefabName 多 SO: %d 个键" % multi)

    t0 = time.time()
    ag = indices.build_ar_guid_index(force)
    print("5/5 反编译 guid->路径: %d 条  (%.1fs)" % (ag["count"], time.time() - t0))
    print("     模型容器键 %d / 材质容器键 %d / 网格容器键 %d"
          % (len(pi["byModelKey"]), len(pi["byMaterialKey"]), len(pi["byMeshKey"])))

    print("\n索引输出目录:", indices.INDEX_DIR)
    for fn in sorted(os.listdir(indices.INDEX_DIR)):
        p = os.path.join(indices.INDEX_DIR, fn)
        print("   %-16s %.1f MB" % (fn, os.path.getsize(p) / 1e6))


if __name__ == "__main__":
    main()
