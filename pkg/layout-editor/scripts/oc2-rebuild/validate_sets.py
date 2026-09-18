"""全量校验：遍历两个关卡集的所有场景做结构自检（引用闭合 / guid 存在）。

    python3 validate_sets.py [--set oc2_dlc_story oc2_story]
"""

import argparse
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from oc2r import paths, verify
from rebuild import project_guids


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--set", nargs="*", default=["oc2_dlc_story", "oc2_story"])
    ap.add_argument("--out", default="validate.json")
    args = ap.parse_args()

    t0 = time.time()
    guids, guid_paths = project_guids(with_paths=True)
    print("工程资产 guid: %d 个（其中预制体 %d 个）" % (len(guids), len(guid_paths)))
    report = {}
    total_bad = 0
    for s in args.set:
        d = os.path.join(paths.LEVELSETS, s, "scenes")
        if not os.path.isdir(d):
            print("跳过（没有该关卡集）:", s)
            continue
        scenes = sorted(f for f in os.listdir(d) if f.endswith(".unity"))
        bad = {}
        for fn in scenes:
            probs = verify.structural_check(os.path.join(d, fn), guids, guid_paths)
            if probs:
                bad[fn] = probs[:10]
                total_bad += len(probs)
        report[s] = {"scenes": len(scenes), "badScenes": len(bad), "problems": bad}
        print("%-16s 场景 %3d  有问题 %d" % (s, len(scenes), len(bad)))
        for fn, probs in list(bad.items())[:5]:
            print("   %s" % fn)
            for p in probs[:3]:
                print("      %s" % p)

    # 关卡集数据完整性
    for s in args.set:
        dd = os.path.join(paths.LEVELSETS, s, "data")
        if not os.path.isdir(dd):
            continue
        levels = [x for x in sorted(os.listdir(dd))
                  if os.path.isdir(os.path.join(dd, x))]
        miss = []
        for lid in levels:
            for need in ["LevelInfo_%s.asset" % lid, "config_1p.asset", "config_2p.asset",
                         "config_3p.asset", "config_4p.asset", "screenshot.png"]:
                if not os.path.exists(os.path.join(dd, lid, need)):
                    miss.append("%s/%s" % (lid, need))
        report.setdefault(s, {})["levels"] = len(levels)
        report[s]["missingFiles"] = miss
        print("%-16s 关卡 %3d  缺文件 %d %s" % (s, len(levels), len(miss), miss[:4]))
        total_bad += len(miss)

    p = paths.out(args.out)
    with open(p, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=1)
    print("\n用时 %.1fs，问题合计 %d，报告: %s" % (time.time() - t0, total_bad, p))
    return 1 if total_bad else 0


if __name__ == "__main__":
    sys.exit(main())
