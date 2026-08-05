#!/usr/bin/env python3
"""
extract-icons.py — batch-extract ingredient & recipe icons from the game's AssetBundles
into layout-editor/web/public/icons/ for the web editor.

DATA-DRIVEN & MAINTAINABLE
  - Ingredients: resolved by sprite NAME. Precedence:
      icon-overrides.json[ingredients]  >  ingredient-icons.json  >
      <base>_Icon (case-insensitive)    >  strip "DLC\\d+_" prefix -> <rest>_Icon
    (ingredient-icons.json holds the curated non-obvious mappings, e.g. SushiRiceSO -> Rice_Icon.)
  - Recipes: the recipe data asset does NOT reference its plated icon, so a hand-curated
      recipe-icons.json maps recipe id -> ui_* sprite name (null = skip).
      icon-overrides.json[recipes] takes precedence.
  - Catalog (core items: 锅具/道具/桌台): catalog-icons.json maps catalog id -> sprite name.
      Most core items have no 2D icon (3D models); only the mapped ones get an icon.
      icon-overrides.json[catalog] takes precedence.
  - Custom items: add an entry to icon-overrides.json (sprite name or null).
  - Local PNG fallback: when a recipe sprite is not found in bundles, the script also
    searches Assets/*/food/CustomRecipes/**/models/ for matching PNG files:
      * `<recipe_id>_Icon.png` (exact match)
      * `<sprite_name>.png` (match by the curated sprite name from recipe-icons.json)
    Found local PNGs are copied directly to the web icons directory.

RE-RUN AFTER A GAME UPDATE
  - Re-run `python3 extract-icons.py`. New ingredients matching <base>_Icon are picked up
    automatically; report at the end lists any unmapped ids to curate.

USAGE
  python3 extract-icons.py                # extract both ingredients + recipes
  python3 extract-icons.py --check        # dry-run: report coverage, write nothing
  python3 extract-icons.py --ingredients  # only ingredients
  python3 extract-icons.py --recipes      # only recipes

Requires: UnityPy, Pillow  (pip install UnityPy Pillow)
"""
import os, re, sys, json, glob, shutil

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
WIN = os.path.join(ROOT, "Assets/StreamingAssets/Windows")
DATA = os.path.join(os.path.dirname(__file__), "data")
OUT = os.path.join(ROOT, "layout-editor/web/public/icons")

try:
    import UnityPy
except ImportError:
    sys.exit("ERROR: UnityPy not installed. Run: pip install UnityPy Pillow")
try:
    from PIL import Image  # noqa: F401
except ImportError:
    sys.exit("ERROR: Pillow not installed. Run: pip install Pillow")


def load_json(name, default):
    p = os.path.join(DATA, name)
    if not os.path.exists(p):
        return default
    raw = json.load(open(p, encoding="utf-8"))
    return {k: v for k, v in raw.items() if not k.startswith("_")}


def collect_ids(sub):
    ids = []
    for f in glob.glob(os.path.join(ROOT, "Assets", sub, "**/*.asset"), recursive=True):
        ids.append(os.path.splitext(os.path.basename(f))[0])
    return sorted(set(ids))


def base_name(sid):
    if sid.endswith("SO"):
        b = sid[:-2]
        if b.endswith("_"):
            b = b[:-1]
        return b
    return sid


def ingredient_sprite_name(sid, overrides, curated):
    """Resolve an ingredient id to its icon sprite name (or None)."""
    if sid in overrides:
        return overrides[sid]
    if sid in curated:
        return curated[sid]
    b = base_name(sid)
    return b + "_Icon"


def build_sprite_index(env):
    """name(lower) -> object, across all loaded bundles. First sprite wins; Texture2D only if no sprite."""
    idx = {}
    tex = {}
    for o in env.objects:
        if o.type.name == "Sprite":
            try:
                nm = o.read().m_Name
            except Exception:
                continue
            if nm:
                idx.setdefault(nm.lower(), o)
        elif o.type.name == "Texture2D":
            try:
                nm = o.read().m_Name
            except Exception:
                continue
            if nm:
                tex.setdefault(nm.lower(), o)
    for k, v in tex.items():
        idx.setdefault(k, v)
    return idx


def build_local_png_index():
    """Scan Assets/*/food/CustomRecipes/**/models/ for local PNG files.
    Returns dict: {lowercase_filename_without_ext: full_abspath}."""
    idx = {}
    patterns = [
        "Assets/common01/food/CustomRecipes/**/models/*.png",
        "Assets/common02/food/CustomRecipes/**/models/*.png",
    ]
    for pat in patterns:
        for f in glob.glob(os.path.join(ROOT, pat), recursive=True):
            key = os.path.splitext(os.path.basename(f))[0].lower()
            idx[key] = f
    return idx


def extract(obj, dest):
    try:
        img = obj.read().image
        img.save(dest)
        return True
    except Exception as e:
        print(f"   WARN: failed to extract {dest}: {e}")
        return False


def copy_local_png(src_path, dest):
    """Copy a local PNG file to destination."""
    try:
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.copy2(src_path, dest)
        return True
    except Exception as e:
        print(f"   WARN: failed to copy {src_path} -> {dest}: {e}")
        return False


def try_local_png(local_idx, recipe_id, sprite_name, out_dir, check):
    """Try to find a matching local PNG for a recipe.
    Returns True if a local file was found and processed."""
    dest = os.path.join(out_dir, recipe_id + ".png")

    # 1) Direct <id>_Icon match: Soup_TomatoEgg_SO -> souptomatoegg_so_icon.png
    key = (recipe_id + "_Icon").lower()
    if key in local_idx:
        if not check:
            copy_local_png(local_idx[key], dest)
        return True

    # 1b) Strip _SO suffix: Soup_TomatoEgg_SO -> Soup_TomatoEgg -> Souptomatoegg_icon.png
    base = base_name(recipe_id)
    if base != recipe_id:
        key_so = (base + "_Icon").lower()
        if key_so in local_idx:
            if not check:
                copy_local_png(local_idx[key_so], dest)
            return True

    # 2) Match by sprite name: ui_soup_tomato_01 -> ui_soup_tomato_01.png
    if sprite_name:
        key2 = sprite_name.lower()
        if key2 in local_idx:
            if not check:
                copy_local_png(local_idx[key2], dest)
            return True

    return False


def main():
    args = [a.lower() for a in sys.argv[1:]]
    check = "--check" in args
    do_ing = "--recipes" not in args and "--catalog" not in args
    do_rec = "--ingredients" not in args and "--catalog" not in args
    do_cat = "--ingredients" not in args and "--recipes" not in args

    overrides_all = load_json("icon-overrides.json", {})
    ing_overrides = overrides_all.get("ingredients", {})
    rec_overrides = overrides_all.get("recipes", {})
    cat_overrides = overrides_all.get("catalog", {})
    ing_curated = load_json("ingredient-icons.json", {})
    rec_curated = load_json("recipe-icons.json", {})
    cat_curated = load_json("catalog-icons.json", {})

    ing_ids = collect_ids("common01/food/Ingredients") + collect_ids("common02/food/Ingredients")
    rec_ids = (
        collect_ids("common01/food/Recipes")
        + collect_ids("common02/food/Recipes")
        + collect_ids("common01/food/CustomRecipes")
    )

    # ---- AssetBundle sprites ----
    print(f"loading bundles from {WIN} ...")
    env = UnityPy.load(WIN)
    idx = build_sprite_index(env)
    print(f"sprite name index: {len(idx)} entries")

    # ---- Local PNG index ----
    local_idx = build_local_png_index()
    if local_idx:
        print(f"local PNG index: {len(local_idx)} files from CustomRecipes/models/")

    stats = {"ing_ok": 0, "ing_miss": [], "rec_ok": 0, "rec_miss": [],
             "rec_local": 0, "cat_ok": 0, "cat_miss": []}

    if do_ing:
        out_dir = os.path.join(OUT, "ingredients")
        if not check:
            os.makedirs(out_dir, exist_ok=True)
        for sid in ing_ids:
            nm = ingredient_sprite_name(sid, ing_overrides, ing_curated)
            obj = idx.get((nm or "").lower()) if nm else None
            if obj is None:
                stats["ing_miss"].append(sid)
                continue
            stats["ing_ok"] += 1
            if not check:
                extract(obj, os.path.join(out_dir, sid + ".png"))

    if do_rec:
        out_dir = os.path.join(OUT, "recipes")
        if not check:
            os.makedirs(out_dir, exist_ok=True)
        for sid in rec_ids:
            nm = rec_overrides.get(sid, rec_curated.get(sid))
            if nm is None:
                # explicit null or unmapped – try local PNG fallback
                if try_local_png(local_idx, sid, None, out_dir, check):
                    stats["rec_ok"] += 1
                    stats["rec_local"] += 1
                elif sid not in rec_curated and sid not in rec_overrides:
                    stats["rec_miss"].append(sid)
                continue

            # Try local PNG first (higher priority than bundle)
            if try_local_png(local_idx, sid, nm, out_dir, check):
                stats["rec_ok"] += 1
                stats["rec_local"] += 1
                continue

            # Fallback: try bundle
            obj = idx.get(nm.lower())
            if obj is not None:
                stats["rec_ok"] += 1
                if not check:
                    extract(obj, os.path.join(out_dir, sid + ".png"))
                continue

            stats["rec_miss"].append(f"{sid} (sprite {nm!r} not found)")

    if do_cat:
        out_dir = os.path.join(OUT, "catalog")
        if not check:
            os.makedirs(out_dir, exist_ok=True)
        for sid, nm in cat_curated.items():
            obj = idx.get((nm or "").lower()) if nm else None
            if obj is None:
                stats["cat_miss"].append(f"{sid} (sprite {nm!r} not found)")
                continue
            stats["cat_ok"] += 1
            if not check:
                extract(obj, os.path.join(out_dir, sid + ".png"))

    mode = "CHECK" if check else "EXTRACT"
    print(f"\n=== {mode} results ===")
    print(f"ingredients: {stats['ing_ok']}/{len(ing_ids)} ok, {len(stats['ing_miss'])} unmapped")
    if stats["ing_miss"]:
        print("  unmapped ingredients (add to ingredient-icons.json or icon-overrides.json):")
        for s in stats["ing_miss"]:
            print(f"    {s}")
    print(f"recipes:     {stats['rec_ok']}/{len(rec_ids)} ok, {len(stats['rec_miss'])} unmapped"
          + (f" ({stats['rec_local']} from local PNGs)" if stats.get("rec_local") else ""))
    if stats["rec_miss"]:
        print("  unmapped recipes (curate in recipe-icons.json):")
        for s in stats["rec_miss"]:
            print(f"    {s}")
    print(f"catalog:     {stats['cat_ok']}/{len(cat_curated)} ok, {len(stats['cat_miss'])} unmapped")
    if stats["cat_miss"]:
        print("  unmapped catalog icons:")
        for s in stats["cat_miss"]:
            print(f"    {s}")
    if not check:
        print(f"\nWrote icons to {OUT}/{{ingredients,recipes,catalog}}/")


if __name__ == "__main__":
    main()
