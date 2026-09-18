"""阶段 0：地板材质提取预算。

对 survey_rawmesh 列出的 46 种材质，算出：
  * 反编译导出里对应的 .mat 与贴图文件、体积
  * 工程里（commonW2/materials 等）是否已经有同名材质
用法：python3 survey_floormat_budget.py
"""

import json
import os
import re
import sys
from collections import OrderedDict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from oc2r import indices, paths, resolve
from oc2r import yamlscene as Y

TEX_PROPS = ("_MainTex", "_BaseMap", "_Albedo", "_AlbedoMap", "_Diffuse", "_Tex")


def mat_textures(mat_path):
    """返回 .mat 引用的贴图 guid 列表（去重，保持顺序）。"""
    try:
        txt = open(mat_path, encoding="utf-8", errors="replace").read()
    except OSError:
        return []
    out = OrderedDict()
    for g in re.findall(r"m_Texture: \{fileID: \d+, guid: ([0-9a-f]{32})", txt):
        out[g] = True
    return list(out)


def main():
    R = resolve.Resolver()
    with open(paths.out("rawmesh.json"), encoding="utf-8") as f:
        raw = json.load(f)

    existing = {}
    for root, _dirs, files in os.walk(paths.ASSETS):
        for fn in files:
            if fn.endswith(".mat"):
                existing.setdefault(fn[:-4].lower(), os.path.join(root, fn))

    total_mat = 0
    total_tex = 0
    tex_seen = {}
    rows = []
    for m in raw["builtinMaterials"]:
        nm = m["name"]
        if len(nm) == 32 and all(c in "0123456789abcdef" for c in nm):
            rows.append((nm, m["count"], "Unity 内置材质，无需提取", 0, 0))
            continue
        src = m.get("source") or ""
        cont = src.split(" :: ", 1)[1] if " :: " in src else ""
        ar_rel = ""
        if cont:
            # bundle 容器 -> 反编译导出路径
            cand = cont[len("assets/"):] if cont.lower().startswith("assets/") else cont
            full = os.path.join(paths.AR_ASSETS, cand)
            if os.path.exists(full):
                ar_rel = cand
            else:
                # 大小写不同，按目录扫描
                d = os.path.join(paths.AR_ASSETS, os.path.dirname(cand))
                base = os.path.basename(cand).lower()
                if os.path.isdir(d):
                    for fn in os.listdir(d):
                        if fn.lower() == base:
                            ar_rel = os.path.join(os.path.dirname(cand), fn)
                            break
        note = ""
        msize = 0
        tsize = 0
        if ar_rel:
            mp = os.path.join(paths.AR_ASSETS, ar_rel)
            msize = os.path.getsize(mp)
            total_mat += msize
            for g in mat_textures(mp):
                rel = R.ar_path(g)
                if not rel or g in tex_seen:
                    continue
                tex_seen[g] = rel
                p = os.path.join(paths.AR_ASSETS, rel)
                if os.path.exists(p):
                    s = os.path.getsize(p)
                    tsize += s
                    total_tex += s
        else:
            note = "反编译导出里找不到 .mat"
        if nm.lower() in existing:
            note = (note + " / " if note else "") + "工程已有: " + os.path.relpath(
                existing[nm.lower()], paths.REPO)
        rows.append((nm, m["count"], note, msize, tsize))

    print("%-44s %7s %10s %10s  %s" % ("材质", "实例", "mat", "新增贴图", "备注"))
    for nm, cnt, note, msize, tsize in rows:
        print("%-44s %7d %9.1fK %9.1fK  %s" % (nm[:44], cnt, msize / 1024.0, tsize / 1024.0, note))
    print()
    print("合计: %d 种材质，%.1f MB 材质 + %.1f MB 贴图（去重后 %d 张）"
          % (len(rows), total_mat / 1e6, total_tex / 1e6, len(tex_seen)))


if __name__ == "__main__":
    main()
