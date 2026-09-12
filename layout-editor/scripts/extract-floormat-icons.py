#!/usr/bin/env python3
"""
extract-floormat-icons.py — 为地板材质选择器批量生成贴图缩略图。

数据链路（每材质）：
  扫描 FloorMaterialsApi 同范围目录（Assets/LevelSets/*/materials →
  common01/02/W1/W2 materials，同 id 先到先得）
    → 解析 .mat YAML 的 _DiffuseMap/_MainTex 纹理 guid
    → 在纹理目录（LevelSets/*/textures + common01/02/W1/W2 textures）定位 PNG
    → PIL 128×128 中心裁切 → layout-editor/web/public/icons/floor-materials/<matId>.png

无贴图 / 贴图缺失的材质跳过（选择器回退纯文字），报告统计。
幂等可重跑；--check 干跑只出报告不写文件。

用法：
  python3 layout-editor/scripts/extract-floormat-icons.py [--check]
"""
import os
import re
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("ERROR: Pillow not installed. Run: pip install Pillow")

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "layout-editor", "web", "public", "icons", "floor-materials")
SIZE = 128

TEX_SLOT_RE = re.compile(
    r"- _(?:DiffuseMap|MainTex):\s*\n\s*m_Texture: \{fileID: 2800000, guid: ([0-9a-f]{32}), type: 3\}"
)
GUID_META_RE = re.compile(r"guid: ([0-9a-f]{32})")


def material_roots():
    """与 LayoutEditorFloorMaterialsApi.Scan / build-catalog scanFloorMaterials 同序。"""
    roots = []
    sets = os.path.join(ROOT, "Assets", "LevelSets")
    if os.path.isdir(sets):
        for name in sorted(os.listdir(sets)):
            d = os.path.join(sets, name, "materials")
            if os.path.isdir(d):
                roots.append(d)
    for lib in ("common01", "common02", "commonW1", "commonW2"):
        d = os.path.join(ROOT, "Assets", lib, "materials")
        if os.path.isdir(d):
            roots.append(d)
    return roots


def build_tex_guid_index():
    """纹理 guid → PNG 路径（LevelSets/*/textures + 各库 textures）。"""
    idx = {}
    roots = []
    sets = os.path.join(ROOT, "Assets", "LevelSets")
    if os.path.isdir(sets):
        for name in sorted(os.listdir(sets)):
            d = os.path.join(sets, name, "textures")
            if os.path.isdir(d):
                roots.append(d)
    for lib in ("common01", "common02", "commonW1", "commonW2"):
        d = os.path.join(ROOT, "Assets", lib, "textures")
        if os.path.isdir(d):
            roots.append(d)
    for base in roots:
        for dp, _ds, fs in os.walk(base):
            for f in fs:
                if not f.endswith(".png.meta"):
                    continue
                meta = os.path.join(dp, f)
                try:
                    m = GUID_META_RE.search(open(meta, errors="ignore").read(300))
                except OSError:
                    continue
                if m:
                    idx.setdefault(m.group(1), meta[:-5])
    return idx


def main():
    check_only = "--check" in sys.argv
    if not check_only:
        os.makedirs(OUT, exist_ok=True)

    tex_index = build_tex_guid_index()
    done, skipped, no_tex, written = [], [], [], 0
    seen_ids = set()
    for root in material_roots():
        for f in sorted(os.listdir(root)):
            if not f.endswith(".mat"):
                continue
            mat_id = f[:-4]
            if mat_id in seen_ids:
                continue
            seen_ids.add(mat_id)
            txt = open(os.path.join(root, f), errors="ignore").read()
            m = TEX_SLOT_RE.search(txt)
            png = tex_index.get(m.group(1)) if m else None
            if not png or not os.path.exists(png):
                no_tex.append(mat_id)
                continue
            out_path = os.path.join(OUT, mat_id + ".png")
            if os.path.exists(out_path):
                done.append(mat_id)
                continue
            if check_only:
                done.append(mat_id)
                continue
            try:
                img = Image.open(png).convert("RGBA")
                w, h = img.size
                side = min(w, h)
                img = img.crop(((w - side) // 2, (h - side) // 2,
                                (w + side) // 2, (h + side) // 2))
                img = img.resize((SIZE, SIZE), Image.LANCZOS)
                img.save(out_path)
                written += 1
                done.append(mat_id)
            except Exception as ex:
                skipped.append((mat_id, repr(ex)[:80]))

    total = len(seen_ids)
    print(f"材质总数: {total}  可出缩略图: {len(done)}  无贴图跳过: {len(no_tex)}  失败: {len(skipped)}")
    if not check_only:
        print(f"本次写入: {written}（其余已存在）→ {os.path.relpath(OUT, ROOT)}")
    if no_tex:
        print("无贴图（选择器回退纯文字）:", ", ".join(no_tex[:12]) + ("…" if len(no_tex) > 12 else ""))
    for mid, why in skipped:
        print("  FAIL", mid, why)


if __name__ == "__main__":
    main()
