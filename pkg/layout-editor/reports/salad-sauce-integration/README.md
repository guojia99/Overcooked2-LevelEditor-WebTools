# commonW3 Salad Sauce Integration

Implemented November 10, 2026. Release version remains 4.0.0; internal revision 4.0.0.21.

Latest colour reversal repair: [colour-reversal-bug.md](colour-reversal-bug.md). Native UIDs and embedded model colours were correct; configured machine list indices were incorrectly treated as native cosmetic slots. Editor/runtime fixes passed 3088 native checks and isolated Windows bundle colour/hash readback. The historical height baseline drift and the separate final-cycle protection baseline are documented there.

## Assets and Mapping

- Authoritative generator/icon input: `layout-editor/scripts/data/salad-sauce-recipes.json`.
- Full 78 recipe ID / UID / base mapping: `recipe-mapping.md`.
- 30 source recipes deduplicate by ingredient UID to 26 bases: 3 story, 6 DLC11, 17 additional Web compositions. Four Web aliases remain attached to the equivalent DLC11 base.
- 78 final CustomRecipeSO assets, UID 58322401 through 58322478, with unique composition signatures.
- 24 score-zero optional nodes: 3 story or 5 DLC11 ingredient anchors times 3 sauce states. Each requires a vegetable and sauce, so an empty/sauce-only plate cannot match. These are not added to order recipes.
- Four native prefabs and two direct model SO assets under `Assets/commonW3/custom_recipes/salad/models/`. Native carrier behaviours are recreated with persistent project MonoScripts; original lookup content is privately cloned at Awake and extended with sauce visuals before Start.
- Actual Unity FBX mesh fileID: both 4300000; ketchup 4999 triangles, mustard 5000. Height repair: visual prefab Sauce child ketchup y=0.200 m, mustard y=0.206 m (0.006 m separation); both lifted 0.200 m. FBX, X/Z, scale and orientation are unchanged. Factory constants are authoritative. Latest verification and its baseline/offline-compilation limitations: `height-020-validation.md`.
- Generator preserves existing 21 recipe UID/GUIDs, PNGs and icon metas. New icon refs use md5(`commonw3-icon:<full recipe id>`). All 78 PNGs have been composed and synchronized to public/dist; repeat validation reports changed=0.

## Runtime and Integration

`SaladSauceRuntime` installs on the `saladsauce` feature and deactivates on unrelated manifest scenes. Plate temporary checks reject empty/duplicate sauce before transfer; server CanHandlePlacement and HandlePlacement repeat that guard. Transfer normalization copies only changed ingredient/composite nodes, mapping DLC08 ketchup 158482 to DLC11 890084 and mustard 17094 to 890076 only for plain salad trees. Hotdog/cooked/mixed structures bypass this logic. No Update loop, scene scans or new network entities are introduced.

Saving recipes and rebuilding dependencies synchronize family-specific optional anchors plus native leaf nodes required for network UID deserialization. Original optional items are retained. The exporter detects selected sauce recipes through LevelInfo and emits `saladsauce`. Explicit native bundle dependencies are bundle18/47/427/428/354, subject to the existing bundle-exists guard.

`GameApi.ICarrierInspectCarriedMethod` now resolves InspectCarriedItem from its declaring interface ICarrierPlacement. Inherited-interface GetMethod returned null in the actual server placement test.

No user LevelInfo, scene or level export was saved. No git commit was created. The existing subgroup/frontend/MCP field implementations and CustomContentApi/docs08 edits were preserved. The existing catalog generator was executed without editing its source.

## Verification

| Check | Result |
|---|---|
| `node layout-editor/scripts/gen-commonw3-salad-sauces.mjs --apply` followed by `--check` | 26 bases / 78 finals / 24 anchors; changed=0 |
| `node layout-editor/scripts/check-salad-sauces.mjs --live --report` | PASS; 102 unique generated UIDs; 84 original asset/meta/icon files byte-identical |
| Two consecutive Unity POST rebuilds and GET readback | PASS; SHA-256 unchanged for 12 factory files |
| Latest Unity native checks | 2888 passed; includes Story/DLC11 native rendering callbacks with one of each sauce at the raised height, prefab transforms and renderer bounds, plus existing recipe/placement checks. Latest readback is in bundle-validation.json; live-validation.json records the earlier integration run. |
| `dotnet build layout-editor/offline-check/WebCustomStubRuntime.Check.csproj --no-restore -v:q -clp:ErrorsOnly` | PASS net35 / C#4; 0 errors; existing warnings and cached NU1900 audit warning |
| `dotnet build layout-editor/offline-check/LayoutEditor.Check.csproj --no-restore -v:q -clp:ErrorsOnly` | PASS net35 / C#4; 0 errors; existing and test DTO field warnings |
| `dotnet build layout-editor/offline-check/LayoutEditor.Standalone.Check.csproj -v:q -clp:ErrorsOnly -p:NuGetAudit=false --ignore-failed-sources` | PASS; only LayoutEditor subtree, no root Editor sources or Runtime DLL reference; net35 / C#4; 0 errors. Subdirectory gitignore exception makes this check project reviewable. |
| `python3 layout-editor/scripts/check-gameapi-init-order.py` | PASS: 377 static fields, 12 initialization-reachable methods |
| `sh Assets/WebCustomStubRuntime/Loader~/build.sh --no-restore -v:q -clp:ErrorsOnly` | PASS; 0 warnings / errors; public/dist Loader.dll synced; version 4.0.0 |
| Unity `/api/set/stub/status` | dllState=fresh after compile/staging |
| `node layout-editor/scripts/build-catalog.mjs` | PASS; new finals in commonw3; 34 existing DLC08 knowledge warnings; ingredient-id gaps=0 |
| `node layout-editor/scripts/check-salad-groups.cjs` | PASS; 24 sauce/provenance cases and actual static/frontend mirror |
| New final catalog groups | ketchup_only=26 / mustard_only=26 / both_sauces=26 |

Rebuild API: `POST /api/recipes/salad-sauce` (empty body). Validation: GET same path. Compile: POST `/api/compile`, then poll health across domain reload. Use 90-second timeout for factory requests and read back GET after any uncertain POST.

## Remaining Validation

All 78 icons, the Web production build, and an isolated Windows commonW3 / webcustomstub_runtime bundle build are complete. See `bundle-validation.md` for actual bundle readback and `runtime-review.md` for the subsequent review results. The final online, management, public and dist catalogs agree: 108 selectable salads, 26 in each sauce subgroup, 30 without sauce, and 9 official. All 24 internal anchors remain indexed for preparation but are excluded from selection lists.

Windows host/client gameplay and final visual approval were not performed. Fixed-machine and IngredientSpray paths were confirmed against their shared native placement chain and its actual patched methods, but no player interaction or two-machine network session was run. Icon pixel/alpha tests and model geometry/material tests passed; they do not replace visual review in the game. The isolated bundles are not a full dependency distribution or production replacement manifest. No release/export is implied by these tests.

## Sauce Height Repair

The following 0.120/0.126 m results are historical. The latest requested 0.200/0.206 m rebuild, 2888 native checks and actual isolated Windows bundle readback passed; see `height-020-validation.md` for current bounds and verification limitations.

Unity POST rebuilt actual prefabs, GET confirmed 2888 checks, and the dedicated build/status API completed one isolated StandaloneWindows commonw3/runtime build. Actual bundle transforms and mesh bounds were independently read using UnityPy. Story and DLC11 callbacks both report ketchup bounds Y=0.120000..0.173496 m and mustard Y=0.126000..0.174803 m relative to the isolated identity carrier. The 0.120 m lift uses the requested default; no measurement against an actual plated dish surface or gameplay screenshot was performed, so final clearance/appearance still needs visual approval.

All 78 latest source/public/dist PNG hashes agree; the bundled compressed textures were compared with these sources and their payload/decoded hashes recorded. C#4/net35 standalone build passed with 0 errors / 41 existing warnings. Check-salad-sauces passed (102 unique UIDs / 84 original files unchanged). The post-rebuild protection baseline verifies 5217 files unchanged across build: scene/LevelInfo .unity/.asset files, FBX/meta, icon compositor and 234 icon PNG copies. It does not claim a pre-turn hash baseline. No scene/LevelInfo write, production manifest replacement, commit or publication was performed.
