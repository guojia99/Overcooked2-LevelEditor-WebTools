#!/usr/bin/env python3
"""
extract-recipes-from-pdf.py
从 Overcooked_AYCE_Recipes_by_Level.pdf 提取所有菜谱小票图片（不去重，全部输出）。

用法:
    python3 extract-recipes-from-pdf.py

输出:
    layout-editor/data/recipe-tickets/  (平铺所有菜谱小票图片 + manifest.json)

依赖:
    pip install pymupdf Pillow
"""

import fitz
import os
import re
import io
import json
from PIL import Image as PILImage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
PDF = os.path.join(ROOT, "..", "Overcooked_AYCE_Recipes_by_Level.pdf")
OUT = os.path.join(ROOT, "data", "recipe-tickets")

MIN_IMAGE_DIM = 80
MAX_IMAGE_DIM = 400


def extract_with_mask(doc, xref, smask_xref):
    base = doc.extract_image(xref)
    rgb = PILImage.open(io.BytesIO(base["image"])).convert("RGBA")

    if smask_xref and smask_xref > 0:
        try:
            mask_info = doc.extract_image(smask_xref)
            mask = PILImage.open(io.BytesIO(mask_info["image"])).convert("L")
            rgb.putalpha(mask)
        except Exception:
            pass

    return rgb


def extract_recipe_lines(text):
    LINE_PREFIXES = "\u2013\u2014\u2217- "
    lines = []
    for line in text.split("\n"):
        stripped = line.strip()
        if not stripped:
            continue
        ch = stripped[0]
        if ch in ("\u2013", "\u2014", "\u2217", "-"):
            ingredient = stripped.lstrip(LINE_PREFIXES).strip()
            if ingredient and not ingredient.isdigit():
                lines.append(ingredient)
    return lines


def sanitize_filename(name):
    name = re.sub(r"[^\w\s,]", "", name)
    name = name.replace(", ", "_").replace(" ", "_").replace(",", "_")
    name = re.sub(r"_+", "_", name)
    name = name.strip("_")
    if len(name) > 120:
        name = name[:120]
    if not name:
        name = "unknown"
    return name


def main():
    if not os.path.exists(PDF):
        print(f"ERROR: PDF not found at {PDF}")
        return

    doc = fitz.open(PDF)
    os.makedirs(OUT, exist_ok=True)

    total_images = 0
    skipped = 0
    manifest = []

    print(f"Processing {doc.page_count} pages...")

    for page_num in range(doc.page_count):
        page = doc[page_num]
        images = page.get_images(full=True)
        text = page.get_text()
        recipe_lines = extract_recipe_lines(text)

        for i, img in enumerate(images):
            xref = img[0]
            smask = img[1]
            w = img[2]
            h = img[3]

            if w < MIN_IMAGE_DIM or h < MIN_IMAGE_DIM:
                skipped += 1
                continue
            if w > MAX_IMAGE_DIM or h > MAX_IMAGE_DIM:
                skipped += 1
                continue

            total_images += 1

            try:
                pil_img = extract_with_mask(doc, xref, smask)
            except Exception as e:
                print(f"  WARN: page {page_num + 1} img {i + 1}: {e}")
                skipped += 1
                continue

            recipe_name = recipe_lines[i] if i < len(recipe_lines) else ""

            filename = f"page{page_num + 1:03d}_{i + 1}.png"
            filepath = os.path.join(OUT, filename)
            pil_img.save(filepath, format="PNG")

            manifest.append(
                {
                    "filename": filename,
                    "page": page_num + 1,
                    "index_on_page": i + 1,
                    "recipe_name": recipe_name,
                    "dimensions": f"{pil_img.width}x{pil_img.height}",
                }
            )

    manifest_path = os.path.join(OUT, "manifest.json")
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)

    named_count = sum(1 for m in manifest if m["recipe_name"])

    print(f"\n=== Extraction complete ===")
    print(f"  total images:       {total_images}")
    print(f"  skipped:            {skipped}")
    print(f"  named (from text):  {named_count}")
    print(f"  output directory:   {OUT}")
    print(f"  manifest:           {manifest_path}")

    doc.close()


if __name__ == "__main__":
    main()
