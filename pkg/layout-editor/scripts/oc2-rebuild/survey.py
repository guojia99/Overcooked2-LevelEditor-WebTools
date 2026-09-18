"""阶段 0：全量 dry-run 盘点。

只读源场景，不写工程。输出每关的识别/跳过/丢弃统计与明细，
用于在动手重建前确认覆盖率，并暴露所有"确实还原不了"的东西。

用法：
    python3 survey.py [--set dlc|base|all] [--limit N] [--level ID]
"""

import argparse
import json
import os
import sys
import time
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from oc2r import levels as LV
from oc2r import matcher as M
from oc2r import paths
from oc2r import resolve
from oc2r import scene_graph as SG
from oc2r import structure as ST
from oc2r import yamlscene as Y


def survey_level(lv, R):
    src = lv.get("sourceScene")
    rec = {
        "id": lv["id"],
        "set": lv["set"],
        "sceneName": lv.get("sceneName"),
        "source": os.path.relpath(src, paths.AR) if src else None,
        "ok": False,
    }
    if not src or not os.path.exists(src):
        rec["error"] = "源场景缺失"
        return rec

    t0 = time.time()
    sc = SG.Scene(src)
    chans = ST.classify_roots(sc)
    mt = M.Matcher(R, prefer_tokens=lv.get("preferTokens"))

    counts = Counter()
    drops = Counter()
    drop_names = Counter()
    drop_paths = {}
    skips = Counter()
    prefab_names = Counter()
    model_names = Counter()
    weak = []
    ambiguous = []
    missing_so = Counter()
    builtin_mats = Counter()
    skewed = 0

    def on_match(m):
        counts[m.kind] += 1
        if m.kind == "prefab":
            prefab_names[m.entry["container"]] += 1
            if m.detail:
                counts["prefab:" + m.detail] += 1
            if m.detail in ("weak-shape", "leaf-weak", "no-shape"):
                weak.append({"node": m.node.path, "cont": m.entry["container"],
                             "score": m.score, "why": m.detail})
            if m.candidates > 1:
                ambiguous.append({"node": m.node.path, "cont": m.entry["container"],
                                  "score": m.score, "n": m.candidates})
            stem = os.path.splitext(os.path.basename(m.entry["container"]))[0]
            if not R.so_guids_for_prefab_name(stem):
                missing_so[stem] += 1
        elif m.kind == "model":
            model_names[m.entry["container"]] += 1
            stem = os.path.splitext(os.path.basename(m.entry["container"]))[0]
            if not R.so_guids_for_prefab_name(stem):
                missing_so[stem] += 1
        elif m.kind == "drop":
            drops[m.detail] += 1
            key = m.detail + "|" + M.base_name(m.node.name)
            drop_names[key] += 1
            if key not in drop_paths:
                drop_paths[key] = m.node.path
            if m.detail == "builtin-mesh":
                for g in _materials_of(m.node):
                    builtin_mats[R.material_name(g) or g] += 1
        elif m.kind == "skip":
            skips[m.detail] += 1
        elif m.kind in ("light", "collision"):
            counts[m.kind + ":" + m.detail] += 1
        if m.kind in ("prefab", "model") and m.node.skewed:
            pass

    walked = []
    specials = {"collision": [], "killplane": [], "lights": []}
    for r in chans["objects"]:
        wl, sp = ST.object_subroots(sc, r)
        walked.extend(wl)
        for k in specials:
            specials[k].extend(sp[k])
    for w in walked:
        mt.walk(w, on_match)

    players = ST.player_nodes(sc, chans["chefs"])

    for n in sc.iter_nodes():
        if n.skewed:
            skewed += 1

    baker_nodes = sum(len(list(b.walk())) for b in chans["bakers"])

    rec.update({
        "ok": True,
        "seconds": round(time.time() - t0, 2),
        "nodes": len(sc.nodes),
        "roots": [r.name for r in sc.roots],
        "envRoot": chans["env"][0].name if chans["env"] else None,
        "cameraRoot": chans["camera"][0].name if chans["camera"] else None,
        "counts": dict(counts),
        "drops": dict(drops),
        "dropNames": dict(drop_names.most_common(80)),
        "dropPaths": {k: drop_paths[k] for k, _ in drop_names.most_common(20)},
        "skips": dict(skips),
        "bakerNodes": baker_nodes,
        "skewedNodes": skewed,
        "players": len(players),
        "collisionGroups": len(specials["collision"]),
        "killplaneGroups": len(specials["killplane"]),
        "lightGroups": len(specials["lights"]),
        "missingSo": dict(missing_so),
        "builtinMaterials": dict(builtin_mats),
        "weakSample": weak[:25],
        "ambiguousSample": ambiguous[:25],
        "prefabContainers": dict(prefab_names),
        "modelContainers": dict(model_names),
    })
    return rec


def _materials_of(node):
    out = []
    d = node.comp(SG.CLS_MESHRENDERER) or node.comp(SG.CLS_SKINNEDMESHRENDERER)
    if d is None:
        return out
    mats = d.data.get("m_Materials")
    if isinstance(mats, list):
        for m in mats:
            g = Y.ref_guid(m)
            if g:
                out.append(g)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--set", default="all", choices=["dlc", "base", "all"])
    ap.add_argument("--limit", type=int, default=0)
    ap.add_argument("--level", default="")
    ap.add_argument("--out", default="survey.json")
    args = ap.parse_args()

    R = resolve.Resolver()
    lvs = []
    if args.set in ("dlc", "all"):
        lvs += LV.dlc_levels()
    if args.set in ("base", "all"):
        lvs += LV.base_levels()
    if args.level:
        lvs = [l for l in lvs if l["id"] == args.level]
    if args.limit:
        lvs = lvs[: args.limit]

    print("关卡数:", len(lvs))
    recs = []
    t0 = time.time()
    for i, lv in enumerate(lvs, 1):
        r = survey_level(lv, R)
        recs.append(r)
        if r.get("ok"):
            c = r["counts"]
            print("[%3d/%3d] %-22s 物件 %5d (prefab %4d / model %3d) 组 %4d 丢 %4d 跳 %4d  %.1fs"
                  % (i, len(lvs), r["id"],
                     c.get("prefab", 0) + c.get("model", 0), c.get("prefab", 0),
                     c.get("model", 0), c.get("group", 0), c.get("drop", 0),
                     c.get("skip", 0), r["seconds"]))
        else:
            print("[%3d/%3d] %-22s 失败: %s" % (i, len(lvs), r["id"], r.get("error")))

    agg = aggregate(recs)
    payload = {"generatedAt": time.strftime("%Y-%m-%d %H:%M:%S"),
               "levels": recs, "aggregate": agg}
    p = paths.out(args.out)
    with open(p, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=1)
    print("\n总耗时 %.1fs，报告: %s" % (time.time() - t0, p))
    print_summary(agg)


def aggregate(recs):
    counts = Counter()
    drops = Counter()
    drop_names = Counter()
    drop_paths = {}
    skips = Counter()
    missing_so = Counter()
    builtin_mats = Counter()
    prefab_cont = Counter()
    model_cont = Counter()
    env_missing = []
    ambiguous = 0
    weak = 0
    skewed = 0
    baker = 0
    for r in recs:
        if not r.get("ok"):
            continue
        counts.update(r["counts"])
        drops.update(r["drops"])
        drop_names.update(r.get("dropNames") or {})
        for k, v in (r.get("dropPaths") or {}).items():
            drop_paths.setdefault(k, v)
        skips.update(r["skips"])
        missing_so.update(r["missingSo"])
        builtin_mats.update(r["builtinMaterials"])
        prefab_cont.update(r["prefabContainers"])
        model_cont.update(r["modelContainers"])
        skewed += r["skewedNodes"]
        baker += r["bakerNodes"]
        ambiguous += len(r["ambiguousSample"])
        weak += len(r["weakSample"])
        if not r["envRoot"]:
            env_missing.append(r["id"])
    return {
        "levels": len(recs),
        "failed": [r["id"] for r in recs if not r.get("ok")],
        "counts": dict(counts),
        "drops": dict(drops),
        "dropTop": [[k, v, drop_paths.get(k, "")] for k, v in drop_names.most_common(120)],
        "skips": dict(skips),
        "distinctPrefabContainers": len(prefab_cont),
        "distinctModelContainers": len(model_cont),
        "missingSoCount": len(missing_so),
        "missingSoTop": missing_so.most_common(60),
        "builtinMaterialCount": len(builtin_mats),
        "builtinMaterials": builtin_mats.most_common(200),
        "skewedNodes": skewed,
        "bakerNodes": baker,
        "envMissing": env_missing,
    }


def print_summary(a):
    print("\n================ 汇总 ================")
    print("关卡 %d，失败 %s" % (a["levels"], a["failed"] or "无"))
    c = a["counts"]
    total_obj = c.get("prefab", 0) + c.get("model", 0)
    print("识别物件 %d（prefab %d + model %d）" % (total_obj, c.get("prefab", 0), c.get("model", 0)))
    print("  其中弱匹配 prefab:  weak-shape %d / leaf-weak %d / no-shape %d"
          % (c.get("prefab:weak-shape", 0), c.get("prefab:leaf-weak", 0), c.get("prefab:no-shape", 0)))
    print("分组下钻 %d，跳过 %d，丢弃 %d" % (c.get("group", 0), c.get("skip", 0), c.get("drop", 0)))
    print("丢弃明细:", dict(sorted(a["drops"].items(), key=lambda x: -x[1])))
    print("跳过明细:", dict(sorted(a["skips"].items(), key=lambda x: -x[1])))
    print("mesh baker 合并网格节点（整棵丢弃）:", a["bakerNodes"])
    print("TRS 不可精确表达（父非均匀缩放+子旋转）节点:", a["skewedNodes"])
    print("用到的不同 prefab 容器 %d / 模型容器 %d" % (a["distinctPrefabContainers"], a["distinctModelContainers"]))
    print("素材库缺 SO 的名字数:", a["missingSoCount"])
    print("内置网格需要的工程材质数:", a["builtinMaterialCount"])
    if a["envMissing"]:
        print("!! 找不到环境根的关卡:", a["envMissing"])


if __name__ == "__main__":
    main()
