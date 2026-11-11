# Salad sauce icons

78 icons are composed from 26 unchanged salad bases. 21 original commonW3 PNGs
and their 21 meta files are byte-for-byte protected against HEAD. Four deduplicated
DLC11 bases use their ingredient-equivalent original commonW3 aliases. The five
remaining bases use official extracted recipe PNGs, captured under
`scripts/data/salad-sauce-icons/official-bases/`. No base is redrawn or resampled
in the delivered icon; output dimensions match each base's native dimensions.

The original red/yellow PNGs are captured byte-for-byte as `Ketchup.png` and
`Mustard.png` under `scripts/data/salad-sauce-icons/`. Their alpha-weighted RGB
means are approximately (236,34,17) and (250,182,16). FBX materials are not used.
Input hashes, provenance, protected original hashes and any pre-existing new meta
hashes are recorded in `scripts/data/salad-sauce-icons/inputs.json`.

## Reproduce and check

Requires Python 3 and Pillow 12.1 or newer (verified with existing Pillow 12.3.0;
no dependency was installed). Run from repository root:

```sh
python3 layout-editor/scripts/compose-salad-sauce-icons.py --apply --sync-web
python3 layout-editor/scripts/compose-salad-sauce-icons.py --check --sync-web
node layout-editor/scripts/gen-commonw3-salad-sauces.mjs --check
```

The recipe generator invokes the compositor on `--apply` / `--check`. Missing
icons are generated; grayscale placeholders can be replaced; recognized previous
outputs can be updated. Unrecognized completed icons cause a refusal before
overwriting that icon. Existing metas are verified and never rewritten. This
checkout initially had no new icon PNGs/metas, so 78 missing Sprite metas were
created by copying an original TextureImporter and changing only its GUID.

The compositor alpha-crops each sauce, makes the sauce interior opaque, and
darkens RGB by factors 0.90 / 0.88 / 0.84. Relative to the initial layout, single
sauces are 20% larger in both dimensions and dual sauces are 30% larger.
No additional opacity attenuation is applied; resampling retains smooth cutout
edges and a food mask keeps the sauce inside the plate.
The mask is saturated food pixels with a small dilation, intersected with an
inner-plate ellipse and fully opaque base pixels. Dual sauces occupy offset
rectangles with unequal vertical dimensions and may partially overlap.
Outside the sauce support every base pixel is unchanged; the entire
output alpha channel is identical to the original base.

`validation.json` records every output SHA256, native dimensions, GUID,
ingredient-equivalent base source, changed pixel count, and visible sauce color
counts. The check recomputes all pixels, verifies saved PNG mode/size/data,
checks recipe icon references and deterministic GUIDs, and requires `changed=0`.

## Preview and limitations

`contact-sheet-1.png` and `contact-sheet-2.png` contain all 26 rows, with base,
ketchup, mustard, and both sauces. Dark preview tile backgrounds are only in
the sheets; delivered PNG backgrounds retain the original transparent alpha.

Direct image reading is unsupported by this session's model. A separate image
tool confirmed the source red drizzle and inspected one original salad, but
both final contact-sheet analysis calls timed out. **Final visual acceptance
is therefore incomplete.** Pixel containment, sauce colors, alpha preservation
and original-byte protection passed; these do not replace manual aesthetic
review at native size or in-game rendering. No Unity/game visual check was run.

## Main-agent build coordination

Only the 78 PNGs in each of `web/public/icons/recipes/` and
`web/dist/icons/recipes/` are refreshed by `--sync-web`; no full build is run by
this icon task to avoid colliding with ongoing runtime/backend/subgroup work.
After all agents finish, the main agent should run from repository root:

```sh
node layout-editor/scripts/build-catalog.mjs
npm --prefix layout-editor/web run build
python3 layout-editor/scripts/compose-salad-sauce-icons.py --check --sync-web
```

Catalog generation must follow the icon sync so the generated recipe rows see
the real PNGs. Vite copies public icons into dist. No git commit was made.
