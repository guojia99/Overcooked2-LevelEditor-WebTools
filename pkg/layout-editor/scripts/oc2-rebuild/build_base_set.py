"""新建本体关卡集 oc2_story（45 关）：关卡资产 + 场景 + 集信息。

    python3 build_base_set.py [--dry] [--limit N]
"""

import argparse
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from oc2r import baseassets as BA
from oc2r import emit, extract, floormats, levels as LV, paths, placement, resolve, verify
from rebuild import project_guids, read_meta_guid

SET_NAME = "oc2_story"
SET_UID = "3f6c1d28-5a47-4f2b-9d0e-7c1a6b3e42f5"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry", action="store_true")
    ap.add_argument("--limit", type=int, default=0)
    ap.add_argument("--level", default="")
    ap.add_argument("--out", default="base_set.json")
    args = ap.parse_args()

    R = resolve.Resolver()
    P = placement.Placer(R, dry_run=args.dry)
    FM = floormats.FloorMaterials(R, dry_run=args.dry)
    EX = extract.Extractor(R, P, floormats=FM)
    W = emit.SceneWriter(R)
    pidmaps = BA.PidMaps()

    lvs = LV.base_levels()
    if args.level:
        lvs = [l for l in lvs if l["id"] == args.level]
    if args.limit:
        lvs = lvs[: args.limit]

    if not args.dry:
        BA.ensure_set_folders(SET_NAME)

    print("新建关卡集 %s：%d 关（%s）" % (SET_NAME, len(lvs), "演练" if args.dry else "落盘"))
    recs = []
    info_guids = []
    guids = None
    failed = 0
    t0 = time.time()
    for i, lv in enumerate(lvs, 1):
        src = lv.get("sourceScene")
        if not src or not os.path.exists(src):
            print("[%3d/%3d] %-22s 源场景缺失" % (i, len(lvs), lv["id"]))
            recs.append({"id": lv["id"], "ok": False, "error": "源场景缺失"})
            failed += 1
            continue
        t1 = time.time()
        mdl, sc = EX.build(lv)
        if not args.dry:
            BA.ensure_level_folder_meta(SET_NAME, lv["id"])
        scene_rel = "Assets/LevelSets/%s/scenes/%s.unity" % (SET_NAME, lv["editorSceneName"])
        scene_abs = os.path.join(paths.REPO, scene_rel)
        text, scene_bundles = W.build(mdl, level_info_guid=None)
        info_guid, warns, deps, recipes = BA.build_level_assets(
            lv, sc, R, pidmaps, scene_bundles, dry=args.dry)
        # LevelInfo guid 已知，重新出一次场景（PseudoPrefabManager 要指向它）
        text, _ = W.build(mdl, level_info_guid=info_guid)
        info_guids.append(info_guid)

        rec = {"id": lv["id"], "scene": lv["editorSceneName"], "ok": True,
               "stats": mdl.stats, "warns": warns, "deps": sorted(deps),
               "recipes": len(recipes),
               "seconds": round(time.time() - t1, 2)}
        if args.dry:
            tmp = paths.cache("preview_%s.unity" % lv["editorSceneName"])
            with open(tmp, "w", encoding="utf-8") as f:
                f.write(text)
            rec["verify"] = verify.verify(mdl, tmp)
        else:
            os.makedirs(os.path.dirname(scene_abs), exist_ok=True)
            with open(scene_abs, "w", encoding="utf-8") as f:
                f.write(text)
            guid = read_meta_guid(scene_abs) or BA.det_guid(
                SET_NAME, "scenes/%s.unity" % lv["editorSceneName"])
            with open(scene_abs + ".meta", "w", encoding="utf-8") as f:
                f.write(emit.scene_meta(guid, "%s/%s" % (SET_NAME, lv["editorSceneName"])))
            rec["guid"] = guid
            rec["verify"] = verify.verify(mdl, scene_abs)
            if guids is None:
                guids = project_guids()
            guids |= P.created_guids | FM.created_guids
            probs = verify.structural_check(scene_abs, guids)
            if probs:
                rec["structural"] = probs[:20]
                rec["ok"] = False

        if rec["verify"]["total"] != 0:
            rec["ok"] = False
        if not rec["ok"]:
            failed += 1
        s = mdl.stats
        print("[%3d/%3d] %-22s 物件%5d (台%4d 具%3d 景%5d) 墙%3d 杀%3d 地砖%4d 丢%4d "
              "| 菜谱%2d 校验 %s %s %.1fs"
              % (i, len(lvs), lv["id"], s["items"], s["counters"], s["utensils"],
                 s["scenery"], s["walls"], s["killplanes"], s["floors"], s["drops"],
                 len(recipes),
                 "OK" if rec["ok"] else "失败",
                 ("警告%d" % len(warns)) if warns else "", rec["seconds"]))
        if warns:
            for w in warns[:3]:
                print("        警告: %s" % w)
        if rec.get("structural"):
            for p in rec["structural"][:3]:
                print("        结构问题: %s" % p)
        recs.append(rec)

    if not args.dry and info_guids:
        BA.write_set_info(SET_NAME, "Overcooked 2 - Story",
                          "\\u80E1\\u95F9\\u53A8\\u623F 2 - \\u4E3B\\u7EBF\\u5173\\u5361",
                          SET_UID, info_guids)

    payload = {"generatedAt": time.strftime("%Y-%m-%d %H:%M:%S"), "dry": args.dry,
               "levels": recs, "createdAssets": sorted(set(P.created_files)),
               "materials": {"created": FM.created,
                             "reused": sorted(set(n for n, _ in FM.reused))}}
    p = paths.out(args.out)
    with open(p, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=1)
    print("\n完成 %d 关，失败 %d，用时 %.1fs" % (len(lvs), failed, time.time() - t0))
    print("报告:", p)
    return 1 if failed else 0


def _recipe_count(rec):
    return 0


if __name__ == "__main__":
    sys.exit(main())
