#!/usr/bin/env python3
"""
dump-bundle-all.py — 将所有 AssetBundle 中的每个对象按 bundle 内路径（container）
完整导出到 <项目根>/dump_bundle/。

用法:
  python3 dump-bundle-all.py            # 导出全部
  python3 dump-bundle-all.py --only bundle18 bundle23   # 只导出指定 bundle
  python3 dump-bundle-all.py --skip-png                  # 贴图也导出为 JSON(typetree) 而不是解码 PNG

输出结构（按 container 路径）:
  <项目根>/dump_bundle/
    ├── manifest.json                      # 清单：每个对象 -> 导出文件、类型、来源 bundle
    └── Assets/data/orderdefinitions/recipeitems/sushi/sushi_plainprawn.asset
        └── (同名 container 有多个对象时追加 _2, _3 …)

导出格式:
  Texture2D / Sprite  -> .png (解码后的图片)
  TextAsset           -> .txt (utf-8 文本)
  其他所有类型         -> .json (Unity 对象的 typetree，字节数据转 base64)
                         typetree 读取失败的 -> .bin (原始字节)
  无 container 路径的对象 -> _unassigned/<bundle>/<type>/<name>
"""

import base64, json, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
WIN = os.path.join(ROOT, "Assets/StreamingAssets/Windows")
OUT = os.path.join(ROOT, "dump_bundle")

try:
    import UnityPy
except ImportError:
    sys.exit("ERROR: UnityPy not installed. Run: pip install UnityPy Pillow")

INVALID_CHARS = '<>:"/\\|?*'


def sanitize_segment(name):
    if not name:
        return "_unnamed"
    return "".join("_" if ch in INVALID_CHARS else ch for ch in name).strip() or "_unnamed"


def clean_path(container):
    """container -> 文件系统安全路径，assets/ 前缀规范化为 Assets/。"""
    if not container:
        return None
    parts = [sanitize_segment(p) for p in container.replace("\\", "/").split("/")]
    if parts and parts[0].lower() == "assets":
        parts[0] = "Assets"
    return os.path.join(*parts)


def dedupe_path(out_dir, rel, used):
    """同一 container 有多个对象时追加 _2, _3 …，保证不互相覆盖。"""
    key = rel.lower()
    base, ext = os.path.splitext(rel)
    candidate, counter = rel, 1
    while key in used:
        counter += 1
        candidate = f"{base}_{counter}{ext}"
        key = candidate.lower()
    used.add(key)
    return os.path.join(out_dir, candidate)


def to_jsonable(v):
    """递归把 typetree 中的 bytes/元组等转成可 JSON 序列化的内容。"""
    if isinstance(v, bytes):
        return {"__base64__": base64.b64encode(v).decode("ascii")}
    if isinstance(v, dict):
        return {str(k): to_jsonable(x) for k, x in v.items()}
    if isinstance(v, (list, tuple)):
        return [to_jsonable(x) for x in v]
    return v


def dump_object(obj, bundle_name, out_dir, used, want_png):
    """导出单个对象，返回 (相对路径, 格式)。"""
    t = obj.type.name
    name = ""
    try:
        name = getattr(obj.read(), "m_Name", "") or ""
    except Exception:
        pass

    rel = clean_path(obj.container)
    if rel is None:
        rel = os.path.join("_unassigned", sanitize_segment(bundle_name),
                           t, sanitize_segment(name))

    if t in ("Texture2D", "Sprite") and want_png:
        try:
            img = obj.read().image
            dest = dedupe_path(out_dir, rel + ".png", used)
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            img.save(dest)
            return os.path.relpath(dest, out_dir), "png"
        except Exception as e:
            print(f"  PNG FAIL ({t}): {rel} ({e})")

    if t == "TextAsset":
        try:
            raw = obj.get_raw_data()
            try:
                text = raw.decode("utf-8")
            except UnicodeDecodeError:
                text = raw.decode("latin-1")
            dest = dedupe_path(out_dir, rel + ".txt", used)
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            with open(dest, "w", encoding="utf-8") as f:
                f.write(text)
            return os.path.relpath(dest, out_dir), "txt"
        except Exception as e:
            print(f"  TEXT FAIL ({t}): {rel} ({e})")

    try:
        tree = obj.read_typetree()
        dest = dedupe_path(out_dir, rel + ".json", used)
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        with open(dest, "w", encoding="utf-8") as f:
            json.dump(to_jsonable(tree), f, ensure_ascii=False, indent=1)
        return os.path.relpath(dest, out_dir), "json"
    except Exception as e:
        print(f"  TYPETREE FAIL ({t}): {rel} ({e})")

    try:
        raw = obj.get_raw_data()
        dest = dedupe_path(out_dir, rel + ".bin", used)
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        with open(dest, "wb") as f:
            f.write(raw)
        return os.path.relpath(dest, out_dir), "bin"
    except Exception as e:
        print(f"  RAW FAIL ({t}): {rel} ({e})")

    return None, None


def main():
    argv = sys.argv[1:]
    only = set()
    skip_png = False
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--only":
            i += 1
            while i < len(argv) and not argv[i].startswith("--"):
                only.add(argv[i]); i += 1
            continue
        if a == "--skip-png":
            skip_png = True
        i += 1

    files = sorted(f for f in os.listdir(WIN)
                   if not f.startswith(".") and not f.endswith(".meta") and not f.endswith(".manifest"))
    if only:
        files = [f for f in files if f in only]
        missing = only - set(files)
        if missing:
            sys.exit(f"ERROR: bundle not found: {sorted(missing)}")

    os.makedirs(OUT, exist_ok=True)
    used = set()
    manifest = []
    stats = {}
    ok = fail = 0

    for fi, fname in enumerate(files):
        fpath = os.path.join(WIN, fname)
        print(f"[{fi + 1}/{len(files)}] {fname} ...", flush=True)
        try:
            env = UnityPy.load(fpath)
        except Exception as e:
            print(f"  LOAD FAIL: {e}")
            fail += 1
            continue

        objects = list(env.objects)
        for oi, obj in enumerate(objects):
            try:
                rel, fmt = dump_object(obj, fname, OUT, used, not skip_png)
            except Exception as e:
                rel, fmt = None, None
                print(f"  OBJECT FAIL ({obj.type.name}): {e}")
            if rel is None:
                fail += 1
                continue
            ok += 1
            stats[fmt] = stats.get(fmt, 0) + 1
            manifest.append({
                "bundle": fname,
                "type": obj.type.name,
                "container": obj.container,
                "file": rel.replace(os.sep, "/"),
                "format": fmt,
            })

        print(f"  -> so far: {ok} ok, {fail} failed", flush=True)

    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump({
            "total": ok,
            "failed": fail,
            "formats": stats,
            "bundles": len(files),
            "objects": manifest,
        }, f, ensure_ascii=False, indent=1)

    print(f"\nDone: {ok} objects exported, {fail} failed -> {OUT}/")
    print(f"Formats: {stats}")
    print(f"Manifest: {os.path.join(OUT, 'manifest.json')}")


if __name__ == "__main__":
    main()
