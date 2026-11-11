# Salad Sauce Machine Colour Reversal

Repair verified November 11, 2026. The VM build clock reports completion at `2026-11-10T18:13:07.9777053Z`; this is the recorded build timestamp, not a release date. No release version change, commit, publish or user scene/LevelInfo save.

## Root Cause and Evidence

The four ingredient UIDs were identified correctly. Their original bundle nodes, icon pixels, native ingredient colours and original hotdog model UV colours agree:

| Native family | Mustard / yellow | Ketchup / red | Native cosmetic slot order |
| --- | --- | --- | --- |
| DLC08 | 17094, Mustard, Mustard_Icon | 158482, Ketchup, Ketchup_Icon | [Mustard, Ketchup] |
| DLC11 | 890076, DLC11_Mustard, Mustard_Icon | 890084, DLC11_Ketchup, Ketchup_Icon | [Mustard, Ketchup] |

`ClientCondimentDispenserCosmeticDecisions.OnItemSwitched` chooses Condiment1/2 by the index in `PlacementItemSwitcher.m_ingredients`; `OnPickupItem` chooses the squirt effect by that same index. Original Condiment_1 is yellow and Condiment_2 is red, verified by the actual mesh UVs and `mat_dlc08_gameplayobjects_01` / `t_dlc08_gameplayobjects_01_d`. Original particle slots are `[MustardSquirt, KetchupSquirt]`. Native animation clips enable the matching bottle and hide the other bottle.

The active test scene's configured SO list is `[dlc11_ketchup, dlc11_mustard]`. Read-only live factory evidence: `node=DLC11_Ketchup:uid=890084:configuredIndex=0:nativeVisualSlot=1`. The upstream `PseudoPrefab.cs` DLC11 branch overwrites the child's array from this configured list before `LayoutEditorItemSwitcherPatch.Patch`. The patch previously sorted against that already-overwritten child array, so it could not restore the native order. Red node at configured index zero triggers the yellow native bottle; yellow at index one triggers the red bottle. A one-sauce list also collapses every sauce to index zero.

The salad chain itself is correct: 158482 -> 890084 -> SaladKetchupVisual -> SaladKetchup.mat -> SaladKetchup_Albedo (mesh UV RGB about 229/27/7); 17094 -> 890076 -> SaladMustardVisual -> SaladMustard.mat -> SaladMustard_Albedo (about 251/147/7). Direct alias lookup gives the same respective colours.

Blender independently reimported both original source FBXs without exporting or editing them. `番茄.fbx` contains yellow/orange albedo (decoded RGB 0.99558/0.61162/0.03803); `黄芥末.fbx` contains red albedo (0.97087/0.12299/0.03024). The previous model agent's filename/embedded-colour discrepancy was real. No FBX, material, texture or UID swap was needed. `colour-native-evidence.json` records source hashes, native icon pixel means, machine UV means and four native hotdog recipe/model checks.

## Fix

- `Assets/Editor/LayoutEditor/LayoutEditorItemSwitcherPatch.cs`: pass the machine SO into Patch and read cosmetic order from the untouched bundle prefab, rather than the mutable child. Existing cycling membership remains intact.
- `Assets/WebCustomStubRuntime/Salad/SaladSauceRuntime.cs`: under the existing saladsauce feature gate, select bottle triggers and squirt effects from the current native ingredient identity. Native yellow slot=0, red slot=1 remains independent of configured order or list length. No machine ingredient arrays, UIDs, transfer content or hotdog semantics are rewritten by these cosmetic callbacks. Unknown ingredients and inactive features retain native callbacks.
- `Assets/WebCustomStubRuntime/Salad/SaladSauceVisuals.cs`: fail before publishing a lookup if a referenced node does not match its expected native colour identity. Canonical alias mapping is unchanged.
- `layout-editor/scripts/gen-commonw3-salad-sauces.mjs` and `scripts/data/salad-sauce-recipes.json`: record the native visual-slot policy alongside the unchanged canonical and alias policy; future generation checks this mapping.
- `Assets/Editor/LayoutEditor/LayoutEditorSaladSauceFactory.cs`: extend the existing validation/build response with colour evidence. Use isolated native machine clones, original node names/icons/colours, real animator callbacks and sampled native clips, real transfer and lookup calls, and GPU UV colour readback. No new global diagnostic API.
- `scripts/check-salad-sauce-colours.py`, `check-salad-sauces.mjs`, `validate-salad-sauce-bundles.py`: independent native-bundle/image regression, live factory regression and serialized Windows output UV/colour/hash readback.

## Verification

- Unity compile/domain reload succeeded. Factory POST rebuild twice and GET readback passed **3088 native checks**; twelve factory files are byte-idempotent. Both DLC08/DLC11 machines feed Story/DLC11 salad carriers; each sauce is checked alone, in reversed configured order and together. Both aliases and canonical direct lookups render the proper colours. Native hotdog transfer preserves source identity and bypasses salad rejection.
- Regression explicitly disables the cosmetic fix and proves the original index-based callback selects the erroneous trigger for reversed/single lists, then enables it and checks the correct trigger and squirt effect. Native animation clips enable the selected bottle and hide the other. Only audio is suppressed in isolated edit-mode pickup validation; production audio uses the original GameUtils.TriggerAudio signature.
- `python3 layout-editor/scripts/check-salad-sauce-colours.py`: PASS for four actual native ingredient nodes/icon pixel identities, four machine slots, four native hotdog model colours, both source hashes and both output albedos.
- `node layout-editor/scripts/gen-commonw3-salad-sauces.mjs --check`: PASS, changed=0; 78 icons unchanged. `check-salad-sauces.mjs --live --report`: PASS, 102 generated UIDs, 84 original asset/meta/icon files unchanged. `check-salad-groups.cjs`: PASS (168 existing sauce/provenance cases); concurrent frontend changes preserved.
- C#4/net35 standalone editor build: PASS, 0 errors / 41 existing warnings. Runtime offline build: PASS, 0 errors / 5 existing warnings. An attempted standalone check during the Windows build temporarily lacked Assembly-CSharp types while Unity replaced Library assemblies; repeating after completion passed.
- Two isolated StandaloneWindows builds completed with the same output hashes. Latest build/status reports 78 icons, 78 final recipes, 24 anchors, two carriers, runtimeStageState=fresh and scenesUnchanged=true. Original y=0.200/0.206, scales, rotations, X/Z, FBX geometry and icon layout are unchanged.
- `python3 layout-editor/scripts/validate-salad-sauce-bundles.py --baseline=colour-baseline.json`: PASS. Actual serialized bundle UV colours agree with native identity. All 78 source/public/dist icons agree, no duplicate sauce texture payload groups. 5217 protected files are unchanged against the explicitly captured colour baseline across the final compile/build/validation.

| Output | Bytes | SHA-256 |
| --- | ---: | --- |
| commonw3 | 45231452 | `59fed2e2084100d21c427699a2ee00a39e0840de1fbdfd41f0f8b9e4abfcfc2f` |
| webcustomstub_runtime | 176639 | `effbf8b1f56f1476515eca857c78e0a96ba989d2338a4410da7817e42c46f787` |
| runtime DLL (Library / staging / bundled TextAsset) | identical | `81c4e24bd22eebe6b173aac512e4af521af4aa7fadac7bad63b74465e78f6ccf` |

Outputs are in `Library/SaladSauceBundleBuild/Windows/`; complete readback is in `bundle-validation.json` and `live-validation.json`. No production manifest or distribution archive was replaced.

## Limits and Preserved Work

The historical `height-baseline.json` failed on six test_salad_108 files: the scene, LevelInfo and config_1p/2p/3p/4p. This older baseline was retained verbatim. These differences were not reverted, and no claim is made that this task has a pre-turn hash baseline. The new `colour-baseline.json` was captured before the final compile/build cycle and verifies that cycle only. The factory records unchanged scene paths/handles/dirty flags; its validation uses temporary clones and does not save user scenes or LevelInfo.

Validation ran in the Windows Unity editor with actual native assets and patched callbacks, plus independent serialized bundle inspection. No player-driven Windows game session, host/client network session, final visual approval or published package installation was performed. Existing published packages still require the normal authorized export/distribution workflow to receive the rebuilt runtime; this task did not publish them.
