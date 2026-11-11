# Salad Sauce Bundle Validation

## Windows Bundle Validation 2026-11-10T14:40:29.2180587Z

PASS. Built with an explicit two-entry AssetBundleBuild[] and reloaded both actual outputs. No scene/LevelInfo save, publish archive, version bump or commit.

Output: `Library/SaladSauceBundleBuild/Windows/` (isolated from the main AssetBundles manifest).

| Bundle | Bytes | MiB | SHA-256 |
| --- | ---: | ---: | --- |
| commonw3 | 45377636 | 43.275 | `0b9641fa0cda8185110506b630a5ea06bdbd790986f1168c15f605fad6779098` |
| webcustomstub_runtime | 176328 | 0.168 | `bffc1897993713ff88b35a24810aa1155138c33236ae0ee6474ab2dd04a26f47` |

Verified 78 new icons, 78 final recipes, 24 optional anchors, two carrier prefabs with native/runtime scripts and all seven lookup/visual references. Serialized native lookup is intentionally null; runtime Awake clones the original lookup.

Meshes: SaladKetchup:tri=4999:material=SaladKetchup:texture=SaladKetchup_Albedo; SaladMustard:tri=5000:material=SaladMustard:texture=SaladMustard_Albedo. Six sauce textures are present; each material's _MainTex, _BumpMap and _MetallicGlossMap resolves to its corresponding albedo, normal and metallic/smoothness texture.

Runtime source DLL, staged bytes and bundled TextAsset SHA-256 all match: `991a584bd7bb09afbae3ddd09cd7404c49eaae84b740d599458731c40817406d`. Runtime state: fresh. Scene handles/paths/dirty flags unchanged across build.

Post-build factory GET passed 2884 native checks (78 finals / 24 anchors), including original lookup cloning and sauce behavior.

Unity CPU texture data was unavailable for non-readable textures; duplication below uses UnityPy serialized payloads, not empty Unity readback hashes.

- commonw3: 302 Texture2D objects; 3 duplicate groups; 46080 redundant serialized pixel bytes (before bundle compression).

Duplicate: Web_SauceSalad_DLC11_Salad_tomato_cucumber_onion_Mustard, Web_SauceSalad_DLC11_Salad_corn_onion_Mustard.


Duplicate: Web_SauceSalad_DLC11_Salad_tomato_cucumber_onion_Ketchup, Web_SauceSalad_DLC11_Salad_corn_onion_Ketchup.


Duplicate: Web_SauceSalad_DLC11_Salad_tomato_cucumber_onion_KetchupMustard, Web_SauceSalad_DLC11_Salad_corn_onion_KetchupMustard.

- webcustomstub_runtime: 0 Texture2D objects; 0 duplicate groups; 0 redundant serialized pixel bytes (before bundle compression).

Sauce model texture duplicate groups: 0. Full texture hashes, embedded script identities, asset lists and errors are recorded in JSON.

Limitations:

- Isolated two-bundle map embeds references to other libraries; not a full dependency distribution or production replacement manifest.
- No game launch/network validation; carrier scripts/references verified from bundles, native lookup behavior verified separately by the Unity factory GET.
- Texture duplicates compare serialized pixel payload plus dimensions/format within these outputs; existing external bundle duplication is not included.
- Existing commonW3 CustomRecipeConfigSO references Assembly-CSharp-Editor.dll; this editor config is unrelated to the two verified carrier scripts and was not modified.

Validation: C# 4 / net35 standalone build passed (0 errors, 41 existing warnings); MCP TypeScript build passed; check-salad-sauces.mjs passed (26 bases, 78 finals, 24 anchors, 102 unique UIDs, 84 original files unchanged). git diff found no tracked changes in Assets/LevelSets, ProjectSettings or Assets/StreamingAssets.

Recovery: the first build completed but its post-build icon assertion counted 21 existing salad icons as well. The prefix filter was corrected; compilation was checked before the second targeted build. Empty CPU texture hashes were excluded and replaced by serialized readback.

## Windows Bundle Validation 2026-11-10T16:52:29.9542095Z

PASS. Built with an explicit two-entry AssetBundleBuild[] and reloaded both actual outputs. No scene/LevelInfo save, publish archive, version bump or commit.

Output: `Library/SaladSauceBundleBuild/Windows/` (isolated from the main AssetBundles manifest).

| Bundle | Bytes | MiB | SHA-256 |
| --- | ---: | ---: | --- |
| commonw3 | 45233649 | 43.138 | `fb14530c8a6a465e49d609602d66e6757ffefaa227211d954734560a3658b8c3` |
| webcustomstub_runtime | 176328 | 0.168 | `bffc1897993713ff88b35a24810aa1155138c33236ae0ee6474ab2dd04a26f47` |

Verified 78 new icons, 78 final recipes, 24 optional anchors, two carrier prefabs with native/runtime scripts and all seven lookup/visual references. Serialized native lookup is intentionally null; runtime Awake clones the original lookup.

Meshes: SaladKetchup:tri=4999:material=SaladKetchup:texture=SaladKetchup_Albedo; SaladMustard:tri=5000:material=SaladMustard:texture=SaladMustard_Albedo. Six sauce textures are present; each material's _MainTex, _BumpMap and _MetallicGlossMap resolves to its corresponding albedo, normal and metallic/smoothness texture.

Runtime source DLL, staged bytes and bundled TextAsset SHA-256 all match: `991a584bd7bb09afbae3ddd09cd7404c49eaae84b740d599458731c40817406d`. Runtime state: fresh. Scene handles/paths/dirty flags unchanged across build.

Post-build factory GET passed 2888 native checks (78 finals / 24 anchors), including original lookup cloning and sauce behavior.

Unity CPU texture data was unavailable for non-readable textures; duplication below uses UnityPy serialized payloads, not empty Unity readback hashes.

- commonw3: 302 Texture2D objects; 3 duplicate groups; 46080 redundant serialized pixel bytes (before bundle compression).

Duplicate: Web_SauceSalad_DLC11_Salad_tomato_cucumber_onion_Mustard, Web_SauceSalad_DLC11_Salad_corn_onion_Mustard.


Duplicate: Web_SauceSalad_DLC11_Salad_tomato_cucumber_onion_Ketchup, Web_SauceSalad_DLC11_Salad_corn_onion_Ketchup.


Duplicate: Web_SauceSalad_DLC11_Salad_tomato_cucumber_onion_KetchupMustard, Web_SauceSalad_DLC11_Salad_corn_onion_KetchupMustard.

- webcustomstub_runtime: 0 Texture2D objects; 0 duplicate groups; 0 redundant serialized pixel bytes (before bundle compression).

Sauce model texture duplicate groups: 0. Full texture hashes, embedded script identities, asset lists and errors are recorded in JSON.

Limitations:

- The requested 0.120/0.126 m defaults were used; bounds are relative to isolated identity carriers, not measured against an actual plated dish surface. Final visual clearance remains unapproved.
- Isolated two-bundle map embeds references to other libraries; not a full dependency distribution or production replacement manifest.
- No game launch/network validation; carrier scripts/references verified from bundles, native lookup behavior verified separately by the Unity factory GET.
- Texture duplicates compare serialized pixel payload plus dimensions/format within these outputs; existing external bundle duplication is not included.
- Existing commonW3 CustomRecipeConfigSO references Assembly-CSharp-Editor.dll; this editor config is unrelated to the two verified carrier scripts and was not modified.

Height repair: ketchup Sauce local y=0.120 m; mustard y=0.126 m; preserved 0.006 m separation, X/Z, scale, rotation and FBX geometry. Both serialized bundle transforms/bounds and Story/DLC11 native rendering callbacks passed.

- Story:SaladKetchup:y=0.120000:boundsY=0.120000..0.173496:lift=0.120000:previousBoundsY=0.000000..0.053496
- Story:SaladMustard:y=0.126000:boundsY=0.126000..0.174803:lift=0.120000:previousBoundsY=0.006000..0.054803
- DLC11:SaladKetchup:y=0.120000:boundsY=0.120000..0.173496:lift=0.120000:previousBoundsY=0.000000..0.053496
- DLC11:SaladMustard:y=0.126000:boundsY=0.126000..0.174803:lift=0.120000:previousBoundsY=0.006000..0.054803
- asset:SaladKetchup:y=0.120000:boundsY=0.120000..0.173496:lift=0.120000:previousBoundsY=0.000000..0.053496
- asset:SaladMustard:y=0.126000:boundsY=0.126000..0.174803:lift=0.120000:previousBoundsY=0.006000..0.054803

All 78 latest icon source/public/dist PNG SHA-256 values agree. Bundled DXT payload and decoded RGBA hashes are recorded individually; decoded premultiplied RGB mean error is below 5/255 per channel and alpha mean error below 2/255 against the latest sources. PNG file hashes and DXT payload hashes intentionally describe different encodings.

Protected baseline files unchanged: 5217 (all user scene/LevelInfo .unity/.asset files, FBX files/metas, icon compositor and 234 source/public/dist icons). Baseline was captured after the initial height-only prefab rebuild and before this bundle build.

C# 4 / net35 standalone build passed: 0 errors, 41 existing warnings. Unity compiled the new height assertions; POST rebuild and GET readback passed. No Windows gameplay or visual approval was performed.

## Windows Bundle Validation 2026-11-10T18:13:07.9777053Z

PASS. Built with an explicit two-entry AssetBundleBuild[] and reloaded both actual outputs. No scene/LevelInfo save, publish archive, version bump or commit.

Output: `Library/SaladSauceBundleBuild/Windows/` (isolated from the main AssetBundles manifest).

| Bundle | Bytes | MiB | SHA-256 |
| --- | ---: | ---: | --- |
| commonw3 | 45231452 | 43.136 | `59fed2e2084100d21c427699a2ee00a39e0840de1fbdfd41f0f8b9e4abfcfc2f` |
| webcustomstub_runtime | 176639 | 0.168 | `effbf8b1f56f1476515eca857c78e0a96ba989d2338a4410da7817e42c46f787` |

Verified 78 new icons, 78 final recipes, 24 optional anchors, two carrier prefabs with native/runtime scripts and all seven lookup/visual references. Serialized native lookup is intentionally null; runtime Awake clones the original lookup.

Meshes: SaladKetchup:tri=4999:material=SaladKetchup:texture=SaladKetchup_Albedo; SaladMustard:tri=5000:material=SaladMustard:texture=SaladMustard_Albedo. Six sauce textures are present; each material's _MainTex, _BumpMap and _MetallicGlossMap resolves to its corresponding albedo, normal and metallic/smoothness texture.

Runtime source DLL, staged bytes and bundled TextAsset SHA-256 all match: `81c4e24bd22eebe6b173aac512e4af521af4aa7fadac7bad63b74465e78f6ccf`. Runtime state: fresh. Scene handles/paths/dirty flags unchanged across build.

Post-build factory GET passed 3088 native checks (78 finals / 24 anchors), including original lookup cloning and sauce behavior.

Unity CPU texture data was unavailable for non-readable textures; duplication below uses UnityPy serialized payloads, not empty Unity readback hashes.

- commonw3: 302 Texture2D objects; 3 duplicate groups; 46080 redundant serialized pixel bytes (before bundle compression).

Duplicate: Web_SauceSalad_DLC11_Salad_tomato_cucumber_onion_Mustard, Web_SauceSalad_DLC11_Salad_corn_onion_Mustard.


Duplicate: Web_SauceSalad_DLC11_Salad_tomato_cucumber_onion_Ketchup, Web_SauceSalad_DLC11_Salad_corn_onion_Ketchup.


Duplicate: Web_SauceSalad_DLC11_Salad_tomato_cucumber_onion_KetchupMustard, Web_SauceSalad_DLC11_Salad_corn_onion_KetchupMustard.

- webcustomstub_runtime: 0 Texture2D objects; 0 duplicate groups; 0 redundant serialized pixel bytes (before bundle compression).

Sauce model texture duplicate groups: 0. Full texture hashes, embedded script identities, asset lists and errors are recorded in JSON.

Limitations:

- The requested 0.200/0.206 m heights were used; bounds are relative to isolated identity carriers, not measured against an actual plated dish surface. Final visual clearance remains unapproved.
- Isolated two-bundle map embeds references to other libraries; not a full dependency distribution or production replacement manifest.
- No game launch/network validation; carrier scripts/references verified from bundles, native lookup behavior verified separately by the Unity factory GET.
- Texture duplicates compare serialized pixel payload plus dimensions/format within these outputs; existing external bundle duplication is not included.
- Existing commonW3 CustomRecipeConfigSO references Assembly-CSharp-Editor.dll; this editor config is unrelated to the two verified carrier scripts and was not modified.

Height repair: ketchup Sauce local y=0.200 m; mustard y=0.206 m; preserved 0.006 m separation, X/Z, scale, rotation and FBX geometry. Both serialized bundle transforms/bounds and Story/DLC11 native rendering callbacks passed.

- Story:SaladKetchup:y=0.200000:boundsY=0.200000..0.253496:lift=0.200000:previousBoundsY=0.000000..0.053496
- Story:SaladMustard:y=0.206000:boundsY=0.206000..0.254803:lift=0.200000:previousBoundsY=0.006000..0.054803
- DLC11:SaladKetchup:y=0.200000:boundsY=0.200000..0.253496:lift=0.200000:previousBoundsY=0.000000..0.053496
- DLC11:SaladMustard:y=0.206000:boundsY=0.206000..0.254803:lift=0.200000:previousBoundsY=0.006000..0.054803
- asset:SaladKetchup:y=0.200000:boundsY=0.200000..0.253496:lift=0.200000:previousBoundsY=0.000000..0.053496
- asset:SaladMustard:y=0.206000:boundsY=0.206000..0.254803:lift=0.200000:previousBoundsY=0.006000..0.054803

All 78 latest icon source/public/dist PNG SHA-256 values agree. Bundled DXT payload and decoded RGBA hashes are recorded individually; decoded premultiplied RGB mean error is below 5/255 per channel and alpha mean error below 2/255 against the latest sources. PNG file hashes and DXT payload hashes intentionally describe different encodings.

Protected baseline files unchanged: 5217 (all user scene/LevelInfo .unity/.asset files, FBX files/metas, icon compositor and 234 source/public/dist icons). Baseline: `colour-baseline.json`; this verifies changes since that explicit capture, not a pre-turn state.

C# 4 / net35 standalone build passed: 0 errors, 41 existing warnings. Unity compiled the new height assertions; POST rebuild and GET readback passed. No Windows gameplay or visual approval was performed.

