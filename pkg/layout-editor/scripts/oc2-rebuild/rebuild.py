"""重建关卡场景（阶段 1/2 主驱动）。

    python3 rebuild.py --set dlc                 # 重建 oc2_dlc_story 87 关
    python3 rebuild.py --level OC2_DLC02_1_1     # 单关
    python3 rebuild.py --set dlc --dry           # 只跑抽取+校验，不落盘

校验闸门：每关生成后立刻反解回来与源场景逐件比对，mismatch > 0 视为失败。
"""

import argparse
import hashlib
import json
import os
import re
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from oc2r import emit, extract, floormats, levels as LV, paths, placement, resolve, verify

_GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")


def deterministic_guid(*parts):
    return hashlib.md5(("oc2rebuild:" + "/".join(parts)).encode("utf-8")).hexdigest()


def read_meta_guid(path):
    mp = path + ".meta"
    if not os.path.exists(mp):
        return ""
    try:
        m = _GUID_RE.search(open(mp, encoding="utf-8", errors="replace").read(400))
    except OSError:
        return ""
    return m.group(1) if m else ""


def level_info_guid(set_name, level_id):
    p = os.path.join(paths.LEVELSETS, set_name, "data", level_id,
                     "LevelInfo_%s.asset" % level_id)
    g = read_meta_guid(p)
    return g or ""


def scene_bundle_name(set_name, scene_name):
    return "%s/%s" % (set_name, scene_name)


def project_guids(with_paths=False):
    """工程内全部资产 guid，用于结构自检。with_paths=True 时额外返回 guid->路径。"""
    out = set()
    paths_map = {}
    for root, _dirs, files in os.walk(paths.ASSETS):
        for fn in files:
            if not fn.endswith(".meta"):
                continue
            p = os.path.join(root, fn)
            try:
                m = _GUID_RE.search(open(p, encoding="utf-8", errors="replace").read(300))
            except OSError:
                continue
            if m:
                out.add(m.group(1))
                if with_paths and fn.endswith(".prefab.meta"):
                    paths_map[m.group(1)] = p[: -len(".meta")]
    return (out, paths_map) if with_paths else out


def merge_level_info_deps(set_name, level_id, bundles):
    """把场景物件 SO 的 bundle 并进 LevelInfo.dependencies（只增不删）。"""
    p = os.path.join(paths.LEVELSETS, set_name, "data", level_id,
                     "LevelInfo_%s.asset" % level_id)
    if not os.path.exists(p) or not bundles:
        return []
    txt = open(p, encoding="utf-8").read()
    m = re.search(r"^  dependencies:(?: \[\])?\n((?:  - [^\n]*\n)*)", txt, re.M)
    if not m:
        return []
    have = set(re.findall(r"^  - (\S+)", m.group(1), re.M))
    add = set(bundles) - have
    if not add:
        return []
    allb = have | set(bundles)
    ordered = sorted(allb, key=lambda s: (0, int(s[6:])) if re.fullmatch(r"bundle\d+", s)
                     else (1, s))
    block = "  dependencies:\n" + "".join("  - %s\n" % b for b in ordered)
    txt = txt[: m.start()] + block + txt[m.end():]
    with open(p, "w", encoding="utf-8") as f:
        f.write(txt)
    return sorted(add)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--set", default="dlc", choices=["dlc", "base", "all"])
    ap.add_argument("--level", default="")
    ap.add_argument("--limit", type=int, default=0)
    ap.add_argument("--dry", action="store_true")
    ap.add_argument("--out", default="rebuild.json")
    ap.add_argument("--no-bundle-name", action="store_true",
                    help="不写场景 meta 的 assetBundleName（沿用原值）")
    args = ap.parse_args()

    R = resolve.Resolver()
    P = placement.Placer(R, dry_run=args.dry)
    FM = floormats.FloorMaterials(R, dry_run=args.dry)
    EX = extract.Extractor(R, P, floormats=FM)
    W = emit.SceneWriter(R)

    lvs = []
    if args.set in ("dlc", "all"):
        lvs += LV.dlc_levels()
    if args.set in ("base", "all"):
        lvs += LV.base_levels()
    if args.level:
        lvs = [l for l in lvs if l["id"] == args.level]
    if args.limit:
        lvs = lvs[: args.limit]

    print("重建 %d 关（%s）" % (len(lvs), "演练" if args.dry else "落盘"))
    recs = []
    guids = None
    t0 = time.time()
    failed = 0
    for i, lv in enumerate(lvs, 1):
        src = lv.get("sourceScene")
        if not src or not os.path.exists(src):
            print("[%3d/%3d] %-22s 源场景缺失" % (i, len(lvs), lv["id"]))
            recs.append({"id": lv["id"], "ok": False, "error": "源场景缺失"})
            failed += 1
            continue
        t1 = time.time()
        mdl, _sc = EX.build(lv)
        set_name = lv["set"]
        scene_name = lv["editorSceneName"]
        scene_rel = "Assets/LevelSets/%s/scenes/%s.unity" % (set_name, scene_name)
        scene_abs = os.path.join(paths.REPO, scene_rel)
        info_guid = level_info_guid(set_name, lv["id"])
        text, bundles = W.build(mdl, level_info_guid=info_guid or None)

        rec = {"id": lv["id"], "set": set_name, "scene": scene_name,
               "ok": True, "stats": mdl.stats, "bundles": sorted(bundles),
               "gridHalf": list(mdl.grid_half), "cell": mdl.cell,
               "envRoot": mdl.env_name,
               "seconds": round(time.time() - t1, 2)}

        if args.dry:
            tmp = paths.cache("preview_%s.unity" % scene_name)
            with open(tmp, "w", encoding="utf-8") as f:
                f.write(text)
            rec["verify"] = verify.verify(mdl, tmp)
        else:
            os.makedirs(os.path.dirname(scene_abs), exist_ok=True)
            with open(scene_abs, "w", encoding="utf-8") as f:
                f.write(text)
            guid = read_meta_guid(scene_abs) or deterministic_guid(
                set_name, "scenes", scene_name)
            bundle = "" if args.no_bundle_name else scene_bundle_name(set_name, scene_name)
            if args.no_bundle_name:
                old = _read_bundle_name(scene_abs + ".meta")
                bundle = old
            with open(scene_abs + ".meta", "w", encoding="utf-8") as f:
                f.write(emit.scene_meta(guid, bundle))
            rec["guid"] = guid
            rec["verify"] = verify.verify(mdl, scene_abs)
            rec["depsAdded"] = merge_level_info_deps(set_name, lv["id"], bundles)
            if guids is None:
                guids = project_guids()
            guids |= P.created_guids
            guids |= FM.created_guids
            probs = verify.structural_check(scene_abs, guids)
            if probs:
                rec["structural"] = probs[:20]
                rec["ok"] = False

        v = rec["verify"]
        if v["total"] != 0:
            rec["ok"] = False
            failed += 1
        elif not rec["ok"]:
            failed += 1
        s = mdl.stats
        print("[%3d/%3d] %-22s 物件%5d (台%4d 具%3d 景%5d) 墙%3d 杀%3d 地砖%4d "
              "斜%3d 丢%4d | 校验 %s  %.1fs"
              % (i, len(lvs), lv["id"], s["items"], s["counters"], s["utensils"],
                 s["scenery"], s["walls"], s["killplanes"], s["floors"],
                 s["skewed"], s["drops"],
                 "OK" if rec["ok"] else ("失配 %d" % v["total"]),
                 rec["seconds"]))
        if rec.get("structural"):
            for p in rec["structural"][:5]:
                print("        结构问题: %s" % p)
        if v["total"] != 0:
            for k, d in v.items():
                if k == "total" or not isinstance(d, dict):
                    continue
                if d["missing"] or d["extra"] or d["rotMismatch"] or d["scaleMismatch"]:
                    print("        %-12s 源%5d 生成%5d 缺%4d 多%4d 旋%3d 缩%3d 最差%.4f %s"
                          % (k, d["src"], d["gen"], d["missing"], d["extra"],
                             d["rotMismatch"], d["scaleMismatch"], d["worstDist"],
                             d["worstName"]))
        recs.append(rec)

    payload = {
        "generatedAt": time.strftime("%Y-%m-%d %H:%M:%S"),
        "dry": args.dry,
        "levels": recs,
        "createdAssets": sorted(set(P.created_files)),
        "materials": {
            "created": FM.created,
            "reused": sorted(set(n for n, _ in FM.reused)),
            "mismatched": FM.mismatched,
            "failed": FM.failed,
        },
    }
    p = paths.out(args.out)
    with open(p, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=1)
    print("\n完成 %d 关，失败 %d，用时 %.1fs" % (len(lvs), failed, time.time() - t0))
    print("新建素材: SO/wrapper %d 个，新材质 %d 个，复用材质 %d 个，贴图对不上另建 %d 个"
          % (len(set(P.created_files)), len(FM.created),
             len(set(n for n, _ in FM.reused)), len(FM.mismatched)))
    if FM.mismatched:
        print("  主贴图与原版不一致（已另建干净材质）:",
              sorted(set(n for n, _ in FM.mismatched))[:12])
    if FM.failed:
        print("材质失败:", FM.failed[:10])
    print("报告:", p)
    return 1 if failed else 0


def _read_bundle_name(meta_path):
    if not os.path.exists(meta_path):
        return ""
    try:
        txt = open(meta_path, encoding="utf-8", errors="replace").read()
    except OSError:
        return ""
    m = re.search(r"^  assetBundleName: (.*)$", txt, re.M)
    return (m.group(1).strip() if m else "")


if __name__ == "__main__":
    sys.exit(main())
