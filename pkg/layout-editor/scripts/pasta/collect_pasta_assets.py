#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""collect_pasta_assets.py — 意面组合模型资产采集（Phase 0 数据准备）。

从 AssetRipper 导出目录（备份盘，只读源）采集 4 个原版 plated 意面模型：
  plated_pasta_01 / plated_pasta_fish_01 / plated_pasta_prawn_01 /
  recipe_seafood_pasta_01
产出（写入本目录 assets/，供 gen-commonw5-pasta.mjs 消费，离线可重跑）：
  meshes/*.obj      — mesh_to_obj.py 转换（镜像 X + 倒排面序，仓库既有约定）
  textures/*.png    — 漫反射贴图原件拷贝
  materials.json    — 4 个材质的属性解析（shader 源 guid / 贴图 guid / floats / colors）
  prefabs.json      — 4 个 prefab 的组件摘要（mesh guid / 材质 guid 序 / 子网格数）
  uids.json         — 原版节点/步骤 uID（extract_pasta.py 的提炼，匹配用克隆资产的数据源）
"""
import json
import os
import re
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.dirname(HERE)
REPO = os.path.dirname(SCRIPTS)
sys.path.insert(0, SCRIPTS)
import mesh_to_obj  # noqa: E402  仓库既有转换器（含坐标约定）

RIPPED = os.path.join(
    os.path.expanduser("~"),
    "Desktop/oc2_editor/backup_rebuild_20260917_132624/Ripped/"
    "UnityProject_AssetsOnly/ExportedProject/Assets")

PREFABS = {
    "plated_pasta_01": "Prefabs/Plated/plated_pasta_01.prefab",
    "plated_pasta_fish_01": "Prefabs/Plated/plated_pasta_fish_01.prefab",
    "plated_pasta_prawn_01": "Prefabs/Plated/plated_pasta_prawn_01.prefab",
    "recipe_seafood_pasta_01": "Prefabs/Meals/recipe_seafood_pasta_01.prefab",
}

# prefab 名 → (mesh 源 guid, [材质源 guid...])；由 Ripped prefab YAML 解析而来
GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")


def guid_of(rel_no_ext):
    meta = os.path.join(RIPPED, rel_no_ext + ".meta")
    m = GUID_RE.search(open(meta, encoding="utf-8", errors="replace").read(200))
    if not m:
        raise SystemExit("no guid: " + meta)
    return m.group(1)


def find_by_guid(guid):
    """全 Ripped 目录 guid → 相对路径（不含 .meta）。"""
    for root, _dirs, files in os.walk(RIPPED):
        for fn in files:
            if not fn.endswith(".meta"):
                continue
            p = os.path.join(root, fn)
            try:
                head = open(p, errors="replace").read(200)
            except OSError:
                continue
            m = GUID_RE.search(head)
            if m and m.group(1) == guid:
                return os.path.relpath(p, RIPPED)[:-5]
    raise SystemExit("guid not found: " + guid)


def parse_prefab(rel):
    """提取 MeshFilter mesh guid + MeshRenderer 材质 guid 序 + RendererInfo 字段。"""
    text = open(os.path.join(RIPPED, rel), encoding="utf-8", errors="replace").read()
    mesh = re.search(r"m_Mesh: \{fileID: \d+, guid: ([0-9a-f]{32})", text)
    mats = re.findall(r"- \{fileID: 2100000, guid: ([0-9a-f]{32})", text)
    info = re.search(
        r"m_Script: \{fileID: 11500000, guid: 34856dccebb76b09e2df91366afead1a\}.*?"
        r"lightmapIndex: (-?\d+)\n\s+lightmapScaleOffset: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}",
        text, re.S)
    return {
        "meshGuid": mesh.group(1) if mesh else None,
        "materialGuids": mats,
        "rendererInfo": {
            "lightmapIndex": int(info.group(1)) if info else -1,
            "lightmapScaleOffset": [float(g) for g in info.groups()[1:]] if info else [0, 0, 0, 0],
        },
    }


def parse_material(rel):
    """材质 → {shaderGuid, texEnvs, floats, colors}（保留原值，gen 阶段仅换引用）。"""
    text = open(os.path.join(RIPPED, rel), encoding="utf-8", errors="replace").read()
    shader = re.search(r"m_Shader: \{fileID: 4800000, guid: ([0-9a-f]{32})", text)
    texenvs = {}
    for m in re.finditer(
            r"(_\w+):\n\s+m_Texture: \{fileID: 2800000, guid: ([0-9a-f]{32})", text):
        texenvs[m.group(1)] = m.group(2)
    floats = {}
    colors = {}
    fm = re.search(r"m_Floats:\n((?:\s+-?\w+: .*\n)+)", text)
    if fm:
        for line in fm.group(1).strip().splitlines():
            k, _, v = line.strip().partition(": ")
            floats[k] = float(v)
    cm = re.search(r"m_Colors:\n((?:\s+-?\w+: \{[^}]*\}\n)+)", text)
    if cm:
        for line in cm.group(1).strip().splitlines():
            k, _, v = line.strip().partition(": ")
            rgba = [float(x) for x in re.findall(r"-?[\d.e+-]+", v)]
            colors[k] = rgba
    name = re.search(r"m_Name: (\S+)", text)
    return {
        "name": name.group(1),
        "shaderGuid": shader.group(1),
        "texEnvs": texenvs,
        "floats": floats,
        "colors": colors,
    }


def main():
    out = os.path.join(HERE, "assets")
    for sub in ("meshes", "textures"):
        os.makedirs(os.path.join(out, sub), exist_ok=True)

    materials = {}
    prefabs = {}
    mesh_files = {}
    tex_files = {}

    for name, rel in sorted(PREFABS.items()):
        pf = parse_prefab(rel)
        prefabs[name] = pf
        # mesh
        if pf["meshGuid"]:
            mrel = find_by_guid(pf["meshGuid"])
            mesh_files[name] = mrel
        # materials
        for mg in pf["materialGuids"]:
            if mg in materials:
                continue
            mrel = find_by_guid(mg)
            mat = parse_material(mrel)
            mat["sourceRel"] = mrel
            materials[mg] = mat
            # textures
            for prop, tg in mat["texEnvs"].items():
                if tg in tex_files:
                    continue
                trel = find_by_guid(tg)
                tex_files[tg] = trel

    # 1) meshes → obj（每个子网格 g 后补 usemtl —— Unity OBJ 导入器按材质分子网格）
    for name, mrel in sorted(mesh_files.items()):
        dst = os.path.join(out, "meshes", os.path.basename(mrel)[:-len(".asset")] + ".obj")
        mesh = mesh_to_obj.parse_mesh_asset(os.path.join(RIPPED, mrel))
        text = mesh_to_obj.convert(mesh)
        lines = []
        sub = -1
        for line in text.splitlines():
            if line.startswith("g "):
                sub += 1
                lines.append(line)
                if sub >= 0:
                    lines.append("usemtl mat_%d" % sub)
                continue
            lines.append(line)
        open(dst, "w").write("\n".join(lines) + "\n")
        print("mesh  %-28s %6d verts %d submeshes" %
              (os.path.basename(dst), mesh["vertex_count"], len(mesh["submeshes"])))
        prefabs[name]["meshSubmeshCount"] = len(mesh["submeshes"])
        prefabs[name]["meshSourceName"] = os.path.basename(mrel)[:-len(".asset")]

    # 2) textures → png
    for tg, trel in sorted(tex_files.items()):
        dst = os.path.join(out, "textures", os.path.basename(trel))
        shutil.copyfile(os.path.join(RIPPED, trel), dst)
        print("tex   %-28s %6d KB" % (os.path.basename(dst),
                                      os.path.getsize(dst) // 1024))

    # 3) summaries
    json.dump(materials, open(os.path.join(out, "materials.json"), "w"), indent=1)
    json.dump(prefabs, open(os.path.join(out, "prefabs.json"), "w"), indent=1)
    json.dump({tg: os.path.basename(trel)[:-len(".png")]
               for tg, trel in tex_files.items()},
              open(os.path.join(out, "texmap.json"), "w"), indent=1)

    # 4) uids（来自 extract_report.json 提炼；若无则跑 extract_pasta.py）
    uids = {
        "pastaNode": 26686, "sushiFishNode": 23600, "sushiPrawnNode": 21875,
        "potStep": 20068, "fryingPanStep": 20294,
        "boiledPastaNode": 14906,
    }
    json.dump(uids, open(os.path.join(out, "uids.json"), "w"), indent=1)
    print("OK collect →", out)


if __name__ == "__main__":
    main()
