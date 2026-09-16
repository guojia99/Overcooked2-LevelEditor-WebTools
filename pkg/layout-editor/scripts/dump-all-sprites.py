#!/usr/bin/env python3
"""
dump-all-sprites.py — 将全部 AssetBundle 中的所有 Sprite/Texture2D 平铺导出为 PNG，
输出到 layout-editor/ 同级目录 sprite-dump/。

用法:
  python3 dump-all-sprites.py

输出:
  <项目根>/sprite-dump/
    ├── all-sprites.json     # 清单：每个精灵的名称、来源 bundle、文件路径
    └── <sprite_name>.png    # 平铺 PNG（重名时追加 _2, _3 …）
"""

import os, sys, json

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
WIN = os.path.join(ROOT, "Assets/StreamingAssets/Windows")
OUT = os.path.join(ROOT, "sprite-dump")

try:
    import UnityPy
except ImportError:
    sys.exit("ERROR: UnityPy not installed. Run: pip install UnityPy Pillow")
try:
    from PIL import Image
except ImportError:
    sys.exit("ERROR: Pillow not installed. Run: pip install Pillow")


def sanitize_filename(name):
    """Replace filesystem-unfriendly characters."""
    if not name:
        return "_unnamed"
    invalid = '<>:"/\\|?*'
    result = []
    for ch in name:
        if ch in invalid:
            result.append("_")
        else:
            result.append(ch)
    return "".join(result).strip()


def safe_path(out_dir, name, used):
    """Resolve a unique filename in out_dir, appending _2, _3 ... for collisions."""
    base = sanitize_filename(name)
    stem, ext = os.path.splitext(base)
    if not ext:
        ext = ".png"
    candidate = stem + ext
    counter = 1
    while candidate.lower() in used:
        counter += 1
        candidate = f"{stem}_{counter}{ext}"
    used.add(candidate.lower())
    return os.path.join(out_dir, candidate), candidate


def main():
    print(f"Loading bundles from {WIN} ...")
    env = UnityPy.load(WIN)

    os.makedirs(OUT, exist_ok=True)

    sprites = []
    textures = {}
    bundle_sprites = {}

    for obj in env.objects:
        try:
            data = obj.read()
        except Exception:
            continue
        name = getattr(data, "m_Name", None) or ""
        container = getattr(obj, "container", None) or ""

        if obj.type.name == "Sprite":
            sprites.append((obj, name, container))
            bundle_sprites.setdefault(container, 0)
            bundle_sprites[container] += 1
        elif obj.type.name == "Texture2D":
            key = name.lower()
            if key not in textures:
                textures[key] = (obj, name, container)

    # Merge: Sprite first, Texture2D as fallback for sprites not already covered
    covered = {s[1].lower() for s in sprites}
    extra = [(o, n, c) for k, (o, n, c) in textures.items() if k not in covered]
    all_items = sprites + extra

    print(f"Total objects: {len(all_items)} (Sprites: {len(sprites)}, Texture2D: {len(extra)})")
    print(f"Output: {OUT}/")

    used_names = set()
    manifest = []
    ok, fail = 0, 0
    total = len(all_items)

    for i, (obj, name, container) in enumerate(all_items):
        if (i + 1) % 500 == 0 or i == 0:
            print(f"  [{i + 1}/{total}] {name or '(unnamed)'} ...")

        dest_path, dest_name = safe_path(OUT, name or "unnamed", used_names)

        try:
            img = obj.read().image
            img.save(dest_path)
            ok += 1
        except Exception as e:
            print(f"  FAIL: {name} ({e})")
            fail += 1
            continue

        manifest.append({
            "name": name,
            "file": dest_name,
            "bundle": container,
            "type": obj.type.name,
            "width": img.width,
            "height": img.height,
        })

    manifest_path = os.path.join(OUT, "all-sprites.json")
    with open(manifest_path, "w", encoding="utf8") as f:
        json.dump({
            "total": ok,
            "failed": fail,
            "sprites": manifest,
            "bundles": {k: v for k, v in sorted(bundle_sprites.items(), key=lambda x: -x[1])}
        }, f, ensure_ascii=False, indent=2)

    print(f"\nDone: {ok} exported, {fail} failed → {OUT}/")
    print(f"Manifest: {manifest_path}")


if __name__ == "__main__":
    main()
