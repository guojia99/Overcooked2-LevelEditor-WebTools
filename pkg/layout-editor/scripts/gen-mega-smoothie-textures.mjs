#!/usr/bin/env node
/**
 * 从什锦果汁母图 + atlas-index masks 生成 12 款 Web 混果汁漫反射贴图。
 *
 * 输入：
 *   backup_rebuild/Models/什锦果汁/atlas-index.json
 *   layout-editor/scripts/data/mixed-smoothie-palettes.json
 *
 * 输出：
 *   backup_rebuild/Models/什锦果汁/variants/<id>/<id>_base_color.png
 *
 * 用法：
 *   node gen-mega-smoothie-textures.mjs              # dry-run
 *   node gen-mega-smoothie-textures.mjs --apply
 *   node gen-mega-smoothie-textures.mjs --apply --only Web_Smoothie_Mix_Red
 */
import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, "../..");
const workspaceRoot = path.resolve(repoRoot, "..");
const MEGA_DIR = path.join(workspaceRoot, "backup_rebuild_20260917_132624/Models/什锦果汁");
const ATLAS_INDEX = path.join(MEGA_DIR, "atlas-index.json");
const PALETTES_PATH = path.join(here, "data/mixed-smoothie-palettes.json");

const apply = process.argv.includes("--apply");
const onlyArg = process.argv.find((a, i) => process.argv[i - 1] === "--only");
const palettes = JSON.parse(fs.readFileSync(PALETTES_PATH, "utf8")).variants;
const atlas = JSON.parse(fs.readFileSync(ATLAS_INDEX, "utf8"));

const targets = onlyArg ? palettes.filter((p) => p.id === onlyArg) : palettes;
if (onlyArg && targets.length === 0) {
  console.error(`未找到色板: ${onlyArg}`);
  process.exit(1);
}

const pyHelper = path.join(here, ".gen-mega-smoothie-textures-tmp.py");
const py = String.raw`
import json, sys, colorsys
from pathlib import Path
from PIL import Image

def hex_rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i+2], 16) for i in (0, 2, 4))

def rgb_hsv(r, g, b):
    return colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0)

def hsv_rgb(h, s, v):
    r, g, b = colorsys.hsv_to_rgb(h, s, v)
    return (int(round(r * 255)), int(round(g * 255)), int(round(b * 255)))

def recolor_channel(img, mask, target_rgb, sat_boost=1.3, v_floor=0.2):
    """HSV 换色：目标色相/饱和度 + 母图明暗层次，避免亮度乘算导致的灰蒙感。"""
    src = img.convert('RGBA')
    out = src.copy()
    spx = src.load()
    mpx = mask.load()
    opx = out.load()
    w, h = src.size
    th, ts, _ = rgb_hsv(*target_rgb)
    ts = min(1.0, max(0.55, ts * sat_boost))

    lums = []
    for y in range(h):
        for x in range(w):
            if mpx[x, y] < 16:
                continue
            r, g, b, a = spx[x, y]
            if a < 8:
                continue
            lums.append(rgb_hsv(r, g, b)[2])
    if not lums:
        return out
    lo, hi = min(lums), max(lums)
    span = max(hi - lo, 0.06)

    for y in range(h):
        for x in range(w):
            if mpx[x, y] < 16:
                continue
            r, g, b, a = spx[x, y]
            if a < 8:
                continue
            _, os, ov = rgb_hsv(r, g, b)
            nv = (ov - lo) / span
            nv = max(0.0, min(1.0, nv))
            nv = v_floor + (1.0 - v_floor) * (nv ** 0.82)
            use_s = min(1.0, ts * 0.95 + os * 0.05)
            nr, ng, nb = hsv_rgb(th, use_s, nv)
            opx[x, y] = (nr, ng, nb, a)
    return out

payload = json.loads(sys.argv[1])
mega_dir = Path(payload['megaDir'])
atlas = json.loads(Path(payload['atlasIndex']).read_text(encoding='utf-8'))
master = mega_dir / atlas['masterTexture']
img = Image.open(master).convert('RGBA')
region_by_key = {r['recolor']: r for r in atlas['regions'] if r.get('recolor') and r['recolor'] != 'none'}

for v in payload['variants']:
    vid = v['id']
    out_dir = mega_dir / 'variants' / vid
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / f"{vid}_base_color.png"
    result = img.copy()
    sat_boost = float(v.get('satBoost', 1.3))
    drink_boost = float(v.get('drinkSatBoost', 1.45))
    for key in ('umbrella', 'drink', 'straw', 'glass'):
        reg = region_by_key.get(key)
        if not reg or key not in v:
            continue
        mask_path = mega_dir / reg['mask']
        mask = Image.open(mask_path).convert('L')
        boost = drink_boost if key == 'drink' else sat_boost
        result = recolor_channel(result, mask, hex_rgb(v[key]), sat_boost=boost)
    result.save(out_path)
    print(f"WROTE {out_path}")
`;

function runPython(variants) {
  fs.writeFileSync(pyHelper, py, "utf8");
  try {
    const arg = JSON.stringify({
      megaDir: MEGA_DIR,
      atlasIndex: ATLAS_INDEX,
      variants,
    });
    execSync(`python3 "${pyHelper}" ${JSON.stringify(arg)}`, {
      encoding: "utf8",
      stdio: "inherit",
    });
  } finally {
    try {
      fs.unlinkSync(pyHelper);
    } catch {
      /* ignore */
    }
  }
}

for (const v of targets) {
  const out = path.join(MEGA_DIR, "variants", v.id, `${v.id}_base_color.png`);
  if (!apply) {
    console.log(`[dry] ${out}  umbrella=${v.umbrella} drink=${v.drink} straw=${v.straw} glass=${v.glass}`);
    continue;
  }
}

if (apply) {
  runPython(targets);
  console.log(`\n完成：${targets.length} 张贴图已写入 variants/`);
} else {
  console.log(`\ndry-run：${targets.length} 张贴图待生成。加 --apply 执行。`);
}
