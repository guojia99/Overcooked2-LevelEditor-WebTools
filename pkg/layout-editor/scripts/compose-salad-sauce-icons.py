#!/usr/bin/env python3
"""Compose the manifest's 78 icons using Pillow; never redraw a salad base.

python3 layout-editor/scripts/compose-salad-sauce-icons.py --apply
python3 layout-editor/scripts/compose-salad-sauce-icons.py --check
One-time source capture: --import-sauces /path/to/original/png/directory --apply
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageStat

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "layout-editor/scripts/data/salad-sauce-icons"
REPORT = ROOT / "layout-editor/reports/salad-sauce-icons"
ICONS = ROOT / "Assets/commonW3/custom_recipes/salad/icons"
MANIFEST = ROOT / "layout-editor/scripts/data/salad-sauce-recipes.json"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def png(image):
    stream = io.BytesIO()
    image.save(stream, format="PNG", optimize=False, compress_level=9)
    return stream.getvalue()


def write_changed(path, data, apply):
    if path.exists() and path.read_bytes() == data:
        return 0
    if apply:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    return 1


def color_count(image, color, mask=None):
    return sum(1 for i, (r, g, b, a) in enumerate(image.get_flattened_data())
               if a > 80 and (mask is None or mask[i] > 40)
               and (r > 135 and r > g * 1.45 and r > b * 1.8 if color == "Ketchup"
                    else r > 145 and g > 95 and r < g * 1.9 and g > b * 1.65))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--import-sauces", type=Path)
    parser.add_argument("--sync-web", action="store_true", help="Also refresh only these 78 public/dist recipe icons")
    args = parser.parse_args()
    manifest = json.loads(MANIFEST.read_text())
    assert len(manifest["bases"]) == 26 and len(manifest["recipes"]) == 78
    lock_path = DATA / "inputs.json"
    if args.import_sauces:
        assert args.apply and not lock_path.exists(), "Source capture is one-time only"
        protected = {}
        originals = sorted(p for p in ICONS.glob("*.png") if not p.name.startswith("Web_SauceSalad_"))
        assert len(originals) == 21
        for p in originals:
            for item in (p, Path(str(p) + ".meta")):
                rel = item.relative_to(ROOT).as_posix()
                head = subprocess.check_output(["git", "show", "HEAD:" + rel], cwd=ROOT)
                assert item.read_bytes() == head, "Original differs from HEAD: " + rel
                protected[rel] = digest(head)
        inputs = {}
        for name, source_name in (("Ketchup", "\u7ea2\u756a\u8304.png"), ("Mustard", "\u9ec4\u82a5\u672b.png")):
            data = (args.import_sauces / source_name).read_bytes()
            write_changed(DATA / (name + ".png"), data, True)
            inputs[name + ".png"] = digest(data)
        bases = {}
        for base in manifest["bases"]:
            candidates = [ICONS / (sid + ".png") for sid in [base["id"], *base["aliases"]]]
            source = next((p for p in candidates if p.exists()), None)
            if source is None:
                source = ROOT / "layout-editor/web/public/icons/recipes" / (base["id"] + ".png")
                assert source.exists(), "Official icon missing: " + str(source)
                target = DATA / "official-bases" / source.name
                write_changed(target, source.read_bytes(), True)
                inputs[target.relative_to(DATA).as_posix()] = digest(source.read_bytes())
                origin = "official extracted PNG: " + source.relative_to(ROOT).as_posix()
                source = target
            else:
                origin = "original 21 icon / ingredient-equivalent alias"
            bases[base["id"]] = {"path": source.relative_to(ROOT).as_posix(), "sha256": digest(source.read_bytes()), "origin": origin}
        metas = {str(p.relative_to(ROOT)): digest(p.read_bytes()) for r in manifest["recipes"]
                 for p in [ROOT / (r["iconPath"] + ".meta")] if p.exists()}
        assert sum("official-bases" in b["path"] for b in bases.values()) == 5
        lock = {"protectedOriginals": protected, "sources": inputs, "bases": bases, "existingNewMetas": metas}
        write_changed(lock_path, (json.dumps(lock, indent=2) + "\n").encode(), True)
    lock = json.loads(lock_path.read_text())
    for rel, expected in {**lock["protectedOriginals"], **lock["existingNewMetas"]}.items():
        assert digest((ROOT / rel).read_bytes()) == expected, "Protected file changed: " + rel
    sources, source_stats = {}, {}
    for name in ("Ketchup", "Mustard"):
        p = DATA / (name + ".png")
        assert digest(p.read_bytes()) == lock["sources"][p.name]
        image = Image.open(p).convert("RGBA")
        alpha = image.getchannel("A")
        assert alpha.getextrema() == (0, 255)
        assert color_count(image, name) > 1000, "PNG color does not match sauce"
        source_stats[name] = {"size": image.size, "alpha": alpha.getextrema(), "crop": alpha.getbbox(),
                              "alphaWeightedRGBA": ImageStat.Stat(image, alpha).mean, "sha256": digest(p.read_bytes())}
        image = image.crop(alpha.getbbox())
        # Keep a transparent cutout, with opaque sauce and darker, saturated color.
        r, g, b, a = image.split()
        image = Image.merge("RGBA", (r.point(lambda v: round(v * .90)),
                                     g.point(lambda v: round(v * .88)),
                                     b.point(lambda v: round(v * .84)),
                                     a.point(lambda v: 255 if v else 0)))
        sources[name] = image
    old_report_path = REPORT / "validation.json"
    previous_outputs = json.loads(old_report_path.read_text()).get("outputs", {}) if old_report_path.exists() else {}
    changed, records, outputs, base_images = 0, [], {}, {}
    template = (ICONS / "Web_LSalad_Lettuce.png.meta").read_text()
    for base in manifest["bases"]:
        entry = lock["bases"][base["id"]]
        path = ROOT / entry["path"]
        assert digest(path.read_bytes()) == entry["sha256"], "Base modified: " + str(path)
        image = Image.open(path).convert("RGBA")
        assert image.width >= 98 and image.height >= 98, (base["id"], image.size)
        assert image.getchannel("A").getextrema() == (0, 255), "Base is not transparent"
        base_images[base["id"]] = image
    for recipe in manifest["recipes"]:
        base = base_images[recipe["baseId"]]
        w, h = base.size
        # Saturated food pixels exclude the white plate; dilation keeps drizzles continuous.
        food = Image.new("L", base.size)
        food.putdata([255 if a > 200 and max(r, g, b) - min(r, g, b) > 32 else 0
                      for r, g, b, a in base.get_flattened_data()])
        dilation = max(3, round(w * .05) | 1)
        food = food.filter(ImageFilter.MaxFilter(dilation))
        ellipse = Image.new("L", base.size)
        ImageDraw.Draw(ellipse).ellipse((w * .17, h * .12, w * .83, h * .73), fill=255)
        food = ImageChops.multiply(food, ellipse)
        food = ImageChops.multiply(food, base.getchannel("A").point(lambda a: 255 if a == 255 else 0))
        result = base.copy()
        layers, visible = [], {}
        dual = recipe["sauce"] == "KetchupMustard"
        names = ["Ketchup", "Mustard"] if dual else [recipe["sauce"]]
        for name in names:
            # Enlarge around the old centers; dual drizzles may overlap centrally.
            if dual:
                box = (.22, .23, .49, .58) if name == "Ketchup" else (.51, .28, .78, .61)
            else:
                box = (.29, .22, .71, .63)
            scale = 1.30 if dual else 1.20
            center_x = (box[0] + box[2]) / 2
            center_y = (box[1] + box[3]) / 2
            width = round(w * (box[2] - box[0]) * scale)
            height = round(h * (box[3] - box[1]) * scale)
            x = round(w * center_x - width / 2)
            y = round(h * center_y - height / 2)
            sauce = sources[name].resize((width, height), Image.Resampling.LANCZOS)
            layer = Image.new("RGBA", base.size)
            layer.paste(sauce, (x, y))
            # No opacity attenuation; only the cutout edge and food boundary mask.
            a = ImageChops.multiply(layer.getchannel("A"), food)
            layer.putalpha(a)
            result = Image.alpha_composite(result, layer)
            layers.append(layer)
        assert result.getchannel("A").tobytes() == base.getchannel("A").tobytes()
        masks = [list(layer.getchannel("A").get_flattened_data()) for layer in layers]
        overlap_pixels = sum(bool(a and b) for a, b in zip(*masks)) if dual else 0
        for name, mask in zip(names, masks):
            visible[name] = color_count(result, name, mask)
            assert visible[name] >= 25, (recipe["id"], visible)
        changed_pixels = 0
        for i, (before, after) in enumerate(zip(base.get_flattened_data(), result.get_flattened_data())):
            if before != after:
                assert any(mask[i] for mask in masks), "Base changed outside sauce"
                changed_pixels += 1
        assert 100 < changed_pixels < w * h * .35, (recipe["id"], changed_pixels)
        assert recipe["iconGuid"] == hashlib.md5(("commonw3-icon:" + recipe["id"]).encode()).hexdigest()
        asset = (ROOT / recipe["assetPath"]).read_text(encoding="utf-8-sig")
        assert re.search(r"icon: \{fileID: 21300000, guid: " + recipe["iconGuid"] + r", type: 3\}", asset)
        path = ROOT / recipe["iconPath"]
        encoded = png(result)
        if path.exists() and path.read_bytes() != encoded:
            current = Image.open(path).convert("RGBA")
            placeholder = len(current.getcolors(16) or []) in range(1, 5) and all(r == g == b for r, g, b, a in current.get_flattened_data())
            assert placeholder or digest(path.read_bytes()) == previous_outputs.get(recipe["id"]), "Unrecognized completed icon; refusing overwrite: " + str(path)
        changed += write_changed(path, encoded, args.apply)
        meta_path = Path(str(path) + ".meta")
        if meta_path.exists():
            assert re.search(r"^guid: " + recipe["iconGuid"] + r"$", meta_path.read_text(), re.M)
        else:
            meta = re.sub(r"^guid: \w+", "guid: " + recipe["iconGuid"], template, flags=re.M)
            changed += write_changed(meta_path, meta.encode(), args.apply)
        if args.apply or args.check:
            saved = Image.open(path)
            assert saved.mode == "RGBA" and saved.size == base.size and saved.tobytes() == result.tobytes()
        outputs[recipe["id"]] = digest(encoded)
        records.append({"id": recipe["id"], "base": recipe["baseId"], "source": lock["bases"][recipe["baseId"]],
                        "size": base.size, "alphaUnchanged": True, "baseOutsideSauceUnchanged": True,
                         "changedPixels": changed_pixels, "visibleSauceColorPixels": visible,
                         "dualOverlapPixels": overlap_pixels, "guid": recipe["iconGuid"], "sha256": digest(encoded)})
        if args.sync_web:
            for folder in ("public", "dist"):
                target = ROOT / "layout-editor/web" / folder / "icons/recipes" / (recipe["id"] + ".png")
                changed += write_changed(target, encoded, args.apply)
                if args.apply or args.check:
                    assert target.read_bytes() == encoded
    # Two readable contact sheets: original base, red, yellow, both, at native resolution.
    for part, start in enumerate((0, 13), 1):
        sheet = Image.new("RGB", (760, 13 * 145 + 36), "#e8e9ed")
        draw = ImageDraw.Draw(sheet)
        for column, label in enumerate(("BASE", "KETCHUP", "MUSTARD", "BOTH")):
            draw.text((190 + column * 140, 10), label, fill="#20232b")
        for row, base in enumerate(manifest["bases"][start:start + 13]):
            y = 36 + row * 145
            label = base["id"].replace("DLC11_Salad_", "D11_").replace("Web_", "")
            for n in range(0, len(label), 24):
                draw.text((8, y + 20 + n // 24 * 14), label[n:n + 24], fill="#20232b")
            group = [r for r in manifest["recipes"] if r["baseId"] == base["id"]]
            images = [base_images[base["id"]]] + [Image.open(ROOT / r["iconPath"]).convert("RGBA") for r in group] if args.apply or args.check else [base_images[base["id"]]]
            for col, image in enumerate(images):
                image = image.copy()
                image.thumbnail((128, 120), Image.Resampling.LANCZOS)
                tile = Image.new("RGBA", (128, 120), "#555b65")
                tile.alpha_composite(image, ((128 - image.width) // 2, (120 - image.height) // 2))
                sheet.paste(tile.convert("RGB"), (180 + col * 140, y))
        if args.apply:
            write_changed(REPORT / ("contact-sheet-%d.png" % part), png(sheet), True)
    report = {"ok": True, "count": len(records), "protectedOriginalFiles": len(lock["protectedOriginals"]),
              "officialAdditionalBases": 5, "sourceStats": source_stats, "outputs": outputs, "icons": records,
              "layout": {"sourceAlphaMultiplier": 1.0, "opaqueSauceInterior": True,
                         "singleScale": 1.20, "dualScale": 1.30, "rgbMultipliers": [.90, .88, .84],
                         "foodMask": "saturated food + dilation, intersect inner ellipse and base alpha",
                         "dualOverlapPixels": sum(r["dualOverlapPixels"] for r in records)}}
    if args.apply:
        write_changed(old_report_path, (json.dumps(report, indent=2) + "\n").encode(), True)
    print(json.dumps({"ok": True, "icons": 78, "changed": changed, "applied": args.apply, "originalFilesUnchanged": 42}))
    if args.check:
        assert changed == 0, "Run with --apply first"


if __name__ == "__main__":
    main()
