"""阶段 1 自检：对全量用到的 prefab 容器做落地方案演练（dry-run，不写文件）。

    python3 survey_placement.py
"""

import json
import os
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from oc2r import paths, placement, resolve


def main():
    R = resolve.Resolver()
    P = placement.Placer(R, dry_run=True)
    print("学习到的 脚本类集合->wrapper 映射: %d 条" % len(P.learned))
    print("外观 SO: %d 个，共享 wrapper 类型: %d 种" % (len(P.so_type), len(P.type_wrapper)))

    sv = json.load(open(paths.out("survey.json"), encoding="utf-8"))
    used = Counter()
    for r in sv["levels"]:
        used.update(r.get("prefabContainers") or {})
        used.update(r.get("modelContainers") or {})
    print("用到的不同容器: %d（实例 %d）" % (len(used), sum(used.values())))

    kinds = Counter()
    cats = Counter()
    no_wrapper = []
    new_so = Counter()
    new_wrapper = Counter()
    by_wrapper = Counter()
    for cont, n in used.items():
        bundle = ""
        e = None
        lst = R.prefabs.get(os.path.splitext(os.path.basename(cont))[0].lower())
        if lst:
            for x in lst:
                if x["container"] == cont:
                    e = x
                    break
        if e is None:
            # 模型容器
            key = os.path.dirname(cont).lower() + "/" + os.path.splitext(os.path.basename(cont))[0].lower()
            ml = R.models.get(key)
            e = {"bundle": ml[0]["bundle"], "container": cont, "ar": ""} if ml else None
        if e is None:
            no_wrapper.append((cont, n, "容器索引缺失"))
            continue
        before_files = len(P.created_files)
        plan = P.resolve(e)
        kinds[plan.kind] += n
        cats[plan.category or "?"] += n
        by_wrapper[plan.wrapper] += n
        for f in P.created_files[before_files:]:
            if f.endswith(".asset"):
                new_so[f] += 1
            else:
                new_wrapper[f] += 1
        if not plan.wrapper:
            no_wrapper.append((cont, n, "没有可用 wrapper"))

    print("\n落地方式: ", dict(kinds))
    print("分类分布: ", dict(cats.most_common(14)))
    print("需新建 SO: %d 个；需新建 wrapper: %d 个" % (len(new_so), len(new_wrapper)))
    print("无法落地的容器: %d" % len(no_wrapper))
    for cont, n, why in sorted(no_wrapper, key=lambda x: -x[1])[:25]:
        print("   %-70s %5d  %s" % (cont[-70:], n, why))

    print("\n共享 wrapper 使用 Top:")
    for w, n in by_wrapper.most_common(18):
        if w and (P.cat_by_wrapper.get(w) or {}).get("category", "") not in placement.DECOR_CATEGORIES:
            print("   %-62s %6d" % (w[-62:], n))

    out = {
        "kinds": dict(kinds),
        "categories": dict(cats),
        "newSo": sorted(new_so),
        "newWrapper": sorted(new_wrapper),
        "unplaceable": [{"container": c, "count": n, "why": w} for c, n, w in no_wrapper],
    }
    with open(paths.out("placement.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    print("\n报告:", paths.out("placement.json"))


if __name__ == "__main__":
    main()
