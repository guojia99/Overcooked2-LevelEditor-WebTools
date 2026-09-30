#!/usr/bin/env python3
"""
从冰淇淋合图按连通域裁切 icon（与牛奶冰沙相同思路：边缘背景泛洪 + 4 邻域聚合）。

3×4 网格口味顺序（行优先）：
  香草、巧克力、草莓、葡萄
  橙子、桃子、蓝莓、黑莓
  覆盆子、香蕉、西瓜、菠萝

草莓已有 FBX/icon，跳过写入。

用法:
  python3 layout-editor/scripts/extract-ice-cream-icons.py [--apply]
"""
from __future__ import annotations

import argparse
import sys
from collections import deque
from pathlib import Path

from PIL import Image

WORKSPACE = Path(__file__).resolve().parents[3]
ICE_DIR = WORKSPACE / "backup_rebuild_20260917_132624/冰淇淋模型"

GRID_FLAVORS: list[list[str]] = [
    ["香草", "巧克力", "草莓", "葡萄"],
    ["橙子", "桃子", "蓝莓", "黑莓"],
    ["覆盆子", "香蕉", "西瓜", "菠萝"],
]

SKIP_FLAVORS = {"草莓"}

SOURCES = [
    (
        ICE_DIR / "所有冰淇淋-带杯子.png",
        ICE_DIR / "icon",
        "冰淇淋带杯子",
        "png",
        "global",  # 12 个独立连通域
    ),
    (
        ICE_DIR / "所有冰淇淋-不带杯子.png",
        ICE_DIR / "模型",
        "冰淇淋不带杯子",
        "png",
        "global",
    ),
]

ROWS, COLS = 3, 4
MIN_AREA = 50_000
BG_DARK_MAX = 12  # 带杯子 PNG：纯黑底
BG_LIGHT_MIN = 245  # 不带杯子 JPEG：近白底
PADDING = 4


def detect_bg_mode(rgba: Image.Image) -> str:
    """根据四边采样判断黑底或白底。"""
    w, h = rgba.size
    px = rgba.load()
    samples: list[tuple[int, int, int, int]] = []
    for x in range(0, w, max(1, w // 32)):
        samples.append(px[x, 0])
        samples.append(px[x, h - 1])
    for y in range(0, h, max(1, h // 32)):
        samples.append(px[0, y])
        samples.append(px[w - 1, y])
    bright = sum(1 for r, g, b, a in samples if a >= 16 and min(r, g, b) >= BG_LIGHT_MIN)
    return "light" if bright > len(samples) // 2 else "dark"


def is_background(r: int, g: int, b: int, a: int = 255, mode: str = "dark") -> bool:
    if a < 16:
        return True
    if mode == "light":
        return min(r, g, b) >= BG_LIGHT_MIN
    return max(r, g, b) <= BG_DARK_MAX


def mark_background(rgba: Image.Image, mode: str | None = None) -> list[list[bool]]:
    if mode is None:
        mode = detect_bg_mode(rgba)
    w, h = rgba.size
    px = rgba.load()
    bg = [[False] * w for _ in range(h)]

    q: deque[tuple[int, int]] = deque()
    for x in range(w):
        for y in (0, h - 1):
            if not bg[y][x] and is_background(*px[x, y], mode=mode):
                bg[y][x] = True
                q.append((x, y))
    for y in range(1, h - 1):
        for x in (0, w - 1):
            if not bg[y][x] and is_background(*px[x, y], mode=mode):
                bg[y][x] = True
                q.append((x, y))

    while q:
        x, y = q.popleft()
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < w and 0 <= ny < h and not bg[ny][nx]:
                if is_background(*px[nx, ny], mode=mode):
                    bg[ny][nx] = True
                    q.append((nx, ny))
    return bg


def grid_cell(cx: float, cy: float, w: int, h: int) -> tuple[int, int]:
    col = min(COLS - 1, max(0, int(cx / (w / COLS))))
    row = min(ROWS - 1, max(0, int(cy / (h / ROWS))))
    return row, col


def find_global_components(rgba: Image.Image, bg: list[list[bool]]) -> list[dict]:
    w, h = rgba.size
    seen = [[False] * w for _ in range(h)]
    components: list[dict] = []

    for y0 in range(h):
        for x0 in range(w):
            if bg[y0][x0] or seen[y0][x0]:
                continue
            stack = [(x0, y0)]
            seen[y0][x0] = True
            min_x = max_x = x0
            min_y = max_y = y0
            sx = sy = 0
            area = 0
            coords: list[tuple[int, int]] = []

            while stack:
                x, y = stack.pop()
                coords.append((x, y))
                area += 1
                sx += x
                sy += y
                min_x = min(min_x, x)
                max_x = max(max_x, x)
                min_y = min(min_y, y)
                max_y = max(max_y, y)
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if 0 <= nx < w and 0 <= ny < h and not bg[ny][nx] and not seen[ny][nx]:
                        seen[ny][nx] = True
                        stack.append((nx, ny))

            if area < MIN_AREA:
                continue
            cx, cy = sx / area, sy / area
            row, col = grid_cell(cx, cy, w, h)
            components.append(
                {
                    "area": area,
                    "cx": cx,
                    "cy": cy,
                    "row": row,
                    "col": col,
                    "min_x": min_x,
                    "min_y": min_y,
                    "max_x": max_x,
                    "max_y": max_y,
                    "coords": coords,
                }
            )

    return components


def find_grid_cell_component(
    rgba: Image.Image, bg: list[list[bool]], row: int, col: int
) -> dict | None:
    w, h = rgba.size
    x0 = int(col * w / COLS)
    y0 = int(row * h / ROWS)
    x1 = int((col + 1) * w / COLS)
    y1 = int((row + 1) * h / ROWS)

    seen = [[False] * w for _ in range(h)]
    best: dict | None = None

    for sy in range(y0, y1):
        for sx in range(x0, x1):
            if bg[sy][sx] or seen[sy][sx]:
                continue
            stack = [(sx, sy)]
            seen[sy][sx] = True
            min_x = max_x = sx
            min_y = max_y = sy
            area = 0
            coords: list[tuple[int, int]] = []

            while stack:
                x, y = stack.pop()
                if not (x0 <= x < x1 and y0 <= y < y1):
                    continue
                coords.append((x, y))
                area += 1
                min_x = min(min_x, x)
                max_x = max(max_x, x)
                min_y = min(min_y, y)
                max_y = max(max_y, y)
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if (
                        x0 <= nx < x1
                        and y0 <= ny < y1
                        and not bg[ny][nx]
                        and not seen[ny][nx]
                    ):
                        seen[ny][nx] = True
                        stack.append((nx, ny))

            if area < MIN_AREA // 4:
                continue
            if best is None or area > best["area"]:
                best = {
                    "area": area,
                    "row": row,
                    "col": col,
                    "min_x": min_x,
                    "min_y": min_y,
                    "max_x": max_x,
                    "max_y": max_y,
                    "coords": coords,
                }
    return best


def crop_component(rgba: Image.Image, comp: dict) -> Image.Image:
    w, h = rgba.size
    px = rgba.load()
    min_x = max(0, comp["min_x"] - PADDING)
    min_y = max(0, comp["min_y"] - PADDING)
    max_x = min(w - 1, comp["max_x"] + PADDING)
    max_y = min(h - 1, comp["max_y"] + PADDING)
    out_w = max_x - min_x + 1
    out_h = max_y - min_y + 1
    out = Image.new("RGBA", (out_w, out_h), (0, 0, 0, 0))
    out_px = out.load()
    for x, y in comp["coords"]:
        out_px[x - min_x, y - min_y] = px[x, y]
    return out


def process_sheet(
    src: Path, out_dir: Path, suffix: str, ext: str, mode: str, apply: bool
) -> None:
    if not src.exists():
        print(f"MISSING {src}", file=sys.stderr)
        return

    rgba = Image.open(src).convert("RGBA")
    w, h = rgba.size
    bg_mode = detect_bg_mode(rgba)
    bg = mark_background(rgba, bg_mode)
    print(f"\n{src.name} ({mode}, bg={bg_mode}):")

    if mode == "global":
        comps = find_global_components(rgba, bg)
        by_cell: dict[tuple[int, int], dict] = {}
        for c in comps:
            key = (c["row"], c["col"])
            if key in by_cell:
                print(f"WARN duplicate cell {key}", file=sys.stderr)
            by_cell[key] = c
        if len(by_cell) != ROWS * COLS:
            print(f"  {len(by_cell)} cells filled (expect {ROWS * COLS})")
            for c in comps:
                print(
                    f"  r{c['row']}c{c['col']} cy={c['cy']:.0f} cx={c['cx']:.0f} area={c['area']}"
                )
            raise SystemExit(f"Grid assignment failed for {src.name}")
    else:
        by_cell = {}
        for row in range(ROWS):
            for col in range(COLS):
                comp = find_grid_cell_component(rgba, bg, row, col)
                if comp is None:
                    raise SystemExit(f"No component in cell r{row}c{col} for {src.name}")
                by_cell[(row, col)] = comp

    out_dir.mkdir(parents=True, exist_ok=True)
    written = 0
    for row in range(ROWS):
        for col in range(COLS):
            flavor = GRID_FLAVORS[row][col]
            comp = by_cell[(row, col)]
            out_name = f"{flavor}{suffix}.{ext}"
            out_path = out_dir / out_name
            skip = flavor in SKIP_FLAVORS
            print(
                f"  r{row}c{col} {flavor:4s}  area={comp['area']:7d}  -> {out_path.name}"
                + ("  [skip]" if skip else "")
            )
            if skip:
                continue
            if apply:
                crop_component(rgba, comp).save(out_path)
                written += 1
    print(f"  {'Would write' if not apply else 'Wrote'} {written} files -> {out_dir}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="写入文件（默认 dry-run）")
    args = parser.parse_args()

    for src, out_dir, suffix, ext, mode in SOURCES:
        process_sheet(src, out_dir, suffix, ext, mode, args.apply)

    if not args.apply:
        print("\nDry-run 完成。加 --apply 写入。")


if __name__ == "__main__":
    main()
