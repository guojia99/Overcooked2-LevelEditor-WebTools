# Salad Sauce Height 0.200 m Verification

PASS for the requested height rebuild and isolated Windows bundle readback. Factory constants remain KetchupY=0.20f and MustardY=KetchupY+0.006f; CheckVisual lift uses KetchupY.

## Unity Rebuild and Readback

- POST /api/compile with an empty body returned reloading=true; subsequent health polling succeeded. The initial request without Content-Length was rejected by HTTP with Length Required.
- POST /api/recipes/salad-sauce rebuilt the actual assets. Independent GET before and after bundle construction passed: 78 final recipes, 24 optional anchors, 2888 native checks.
- Story, DLC11 and asset readbacks all reported the following Sauce local positions and renderer bounds, relative to isolated identity carriers.

| Visual | Sauce local Y (m) | Renderer bounds Y (m) | Lift (m) |
| --- | ---: | --- | ---: |
| Ketchup | 0.200000 | 0.200000..0.253496 | 0.200000 |
| Mustard | 0.206000 | 0.206000..0.254803 | 0.200000 |

Local prefab YAML also reads y=0.2 and y=0.206. X/Z remain zero, scale remains one and rotation remains identity. Unity generated these prefabs; no fallback manual prefab edit was needed. Meshes remain 4999 / 5000 triangles.

## Isolated Windows Bundles

Existing operation=status was read before submitting one operation=build. While the build blocked HTTP status, Library/SaladSauceBundleBuild/result.json reported building; no build was resubmitted. Final HTTP status reported ok=true, state=completed, runtimeStageState=fresh and scenesUnchanged=true.

Unity-recorded start: 2026-11-10T17:07:08.1775682Z. Completion: 2026-11-10T17:08:08.4077373Z. These are the remote Unity clock timestamps.

Output: Library/SaladSauceBundleBuild/Windows/, target StandaloneWindows.

| Bundle | Bytes | SHA-256 |
| --- | ---: | --- |
| commonw3 | 45231452 | 59fed2e2084100d21c427699a2ee00a39e0840de1fbdfd41f0f8b9e4abfcfc2f |
| webcustomstub_runtime | 176328 | bffc1897993713ff88b35a24810aa1155138c33236ae0ee6474ab2dd04a26f47 |

UnityPy independently read both actual output files and matched their hashes to the Unity result. Serialized Sauce heights were 0.20000000298023224 m and 0.20600000023841858 m. Serialized mesh bounds plus transforms were 0.20000000298023224..0.25349587202072144 m and 0.20600000023841858..0.2548025995492935 m. Scale/rotation/X/Z assertions passed.

Unity bundle checks passed: 78 icons, 78 final recipes, 24 optional anchors and two carriers. Existing icon checks verified all 78 source/public/dist PNG copies against the bundled textures without regenerating them. Runtime source, staged and bundled DLL hashes match: 991a584bd7bb09afbae3ddd09cd7404c49eaae84b740d599458731c40817406d.

## Verification Limits

- The complete report generator stopped at its existing protected baseline: Assets/LevelSets/tester_web/data/test_salad_108/LevelInfo_test_salad_108.asset differs from height-baseline.json. A separate comparison found this is the only mismatch among 5217 entries. The baseline and LevelInfo were preserved; the time or origin of that difference is not established by this run. Previous bundle-validation.json/md remain historical reports.
- C#4/net35 standalone compilation was attempted, including after the bundle build completed. It did not pass: local Library/ScriptAssemblies/Assembly-CSharp.dll is absent, causing 80 unresolved-type errors and two reference warnings. No project or source changes were made to repair this separate local compilation environment. Unity successfully executed the updated factory and completed its guarded bundle build.
- git diff --check passed. The factory, validator and prefab files are currently untracked, so a normal git diff does not display their changes. The only manual code edit in this verification was synchronizing the existing validator's height assertions and height report text from 0.120/0.126 to 0.200/0.206 m.
- No scene/LevelInfo save endpoint, production manifest replacement, publication or commit was performed. The build reported unchanged loaded scene paths/handles/dirty flags. No gameplay, network session or final appearance approval was performed.
