#!/usr/bin/env python3
"""
probe-mapping.py — 对单个源场景做名称映射覆盖测试（不生成任何文件）。

用法：python3 probe-mapping.py [source_scene_path]
默认：dlc02 s_beach_1_1
"""
import os
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from oc2_common import ROOT, RIPPER, Scene, EditorIndex, base_name, map_item

DEFAULT = os.path.join(
    RIPPER, "Assets", "downloadablecontent", "dlc02", "dlc_assets",
    "Scenes", "01_Beach", "s_beach_1_1.unity")

# 需要遍历的源场景子树（Design 下映射为玩法物件；Art 下为装饰）
DESIGN_GROUPS = ["Work Surfaces", "Gameplay Stations", "Cooking Stations",
                 "Utensils", "Glasses", "Plates", "Mugs", "Trays",
                 "Animated Objects", "Walls", "Floors", "Environment Objects"]
ART_GROUPS = ["Scenery", "Animated Objects"]


def collect(scene, group_root, out, skip_colliders=True):
    for tr in scene.children.get(group_root, []):
        go = scene.tr2go.get(tr)
        nm = scene.go_name(go)
        comps = [scene.docs[c].cls for c in scene.components(go) if c in scene.docs]
        # 跳过纯碰撞/骨骼节点
        if skip_colliders and nm and nm.lower() in (
                "ground", "wall", "tableblock", "table", "killplane", "block"):
            continue
        out.append((nm, tr, comps))


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT
    scene = Scene(path)
    idx = EditorIndex()
    print(f"场景: {os.path.basename(path)}  SO 总数: {len(idx.so_by_guid)}  目录项: {len(idx.catalog)}")

    design = scene.find_root("Design")
    art = scene.find_root("Art")
    items = []
    for g in DESIGN_GROUPS:
        sub = scene.find_child(design, g) if design else None
        if sub:
            collect(scene, sub, items)
    for g in ART_GROUPS:
        sub = scene.find_child(art, g) if art else None
        if sub:
            collect(scene, sub, items)

    hit, miss, skipped = [], [], []
    stack = list(items)
    while stack:
        nm, tr, comps = stack.pop()
        kind, b, *rest = map_item(scene, idx, tr)
        if kind == "place":
            hit.append((b, nm, rest[0]))
        elif kind == "group":
            sub = []
            collect(scene, tr, sub)
            stack.extend(sub)
        elif kind == "skip":
            skipped.append(b)
        else:
            miss.append((b, nm, None))

    print(f"\n顶层物件: {len(items)}  命中: {len(hit)}  未命中: {len(miss)}  忽略: {len(skipped)}")
    miss_names = Counter(b for b, _, _ in miss)
    print("\n=== 未命中（按基名聚合）===")
    for b, n in miss_names.most_common():
        print(f"  {n:3d}x  {b}")
    print("\n=== 命中样例 ===")
    seen = set()
    for b, nm, r in hit:
        if b in seen:
            continue
        seen.add(b)
        print(f"  {b:40s} -> {r['kind']:10s} {os.path.basename(r['placeholder']):35s} so={r['soName']}")


if __name__ == "__main__":
    main()
