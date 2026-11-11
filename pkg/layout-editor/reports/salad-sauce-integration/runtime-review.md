# Salad Sauce Runtime Review

Date: 2026-11-10

## Confirmed Defects And Fixes

- Play feature gate: `RefreshFeatureFlags` previously used `HasFeature("saladsauce")`; an absent manifest makes that method return true for every scene. The new salad-specific gate reads selected `LevelInfoSO.recipes` in Play, and uses the manifest feature list exclusively when present. Switching to a scene without sauce recipes disables the guard.
- Malformed composite: a null `m_composition` was accepted by `Leaves`, then dereferenced by `Normalize`. A live regression reproduced `NullReferenceException` at the former `original.Clone()` call. Null children, missing/destroyed ingredient definitions and missing reflection fields now bypass salad normalization; runtime patch boundaries contain exceptions and preserve the incoming reference. Failed patch installation rolls back this patch group instead of interrupting EntryPoint.
- Visual lookup publication: `m_lookup` was assigned before all sauce entries loaded. With a missing mustard reference, a live regression demonstrated that a failed build left a non-null incomplete cache and blocked retry. The lookup is now published only after successful construction; failed clones are released. References restored on the temporary validation carrier successfully rebuild its lookup.

## Investigated Without Production Changes

- Plate fallback did not revive a rejection in the live Unity/Harmony environment. Validation installs the existing production fallback prefix/postfix and a highest-priority rejecting prefix. It first confirms that the unchanged lettuce fixture could otherwise pass `CanStillFormOrder`, then verifies the patched Plate still rejects without changing contents. Actual empty-plate and duplicate-sauce rejection also pass with fallback installed. `HarmonyPatches.cs` was not changed.
- Existing UID mapping remains DLC08 ketchup 158482 -> 890084 and mustard 17094 -> 890076. Both temporary and non-temporary transfers normalize sauce aliases when salad is present; source definitions and canonical-composite identity remain intact.
- Existing 24 optional anchors and dependency registration support the 78 final recipes over 26 bases. No recipe, model, icon or dependency asset rebuild was needed for these fixes.

## Verification

- `node layout-editor/scripts/check-salad-sauces.mjs`: passed; 26 bases, 78 finals, 24 anchors, 102 unique generated UIDs, 84 original files unchanged.
- `dotnet build layout-editor/offline-check/WebCustomStubRuntime.Check.csproj --no-restore --nologo -v:q -clp:ErrorsOnly`: passed; net35 / C# 4, 0 errors, 5 warnings.
- `dotnet build layout-editor/offline-check/LayoutEditor.Standalone.Check.csproj --no-restore --nologo -v:q -clp:ErrorsOnly`: passed; 0 errors, 41 warnings.
- `dotnet build layout-editor/offline-check/LayoutEditor.Check.csproj --no-restore --nologo -v:q -clp:ErrorsOnly`: passed; 0 errors, 43 warnings.
- `python3 layout-editor/scripts/check-gameapi-init-order.py`: passed; 377 fields, 12 initialization-reachable methods.
- Unity was refreshed via `POST /api/compile`. Two consecutive `GET /api/recipes/salad-sauce` requests returned identical successful results: 78 finals, 24 anchors, nativeChecks=2884. Ketchup/mustard mesh triangle counts remain 4999/5000.
- Live checks include every enumerated nonempty vegetable/sauce subset, empty and duplicate rejection, either sauce order, adding vegetables after sauce, carried-plate rejection, native hotdog bypass, canonical/alias visuals, failure/retry lookup construction, gate transitions, native BitStream serialization/deserialization and matching the same partial-salad carrier after deserialization.
- `git diff --check`: passed. `git status --short -- Assets/LevelSets`: empty.

## Scope And Limits

Live checks ran inside the connected Unity editor using disposable hidden objects and temporary Harmony validation patches; they did not enter a gameplay session or perform a Windows host/client session. Native serialization roundtrip is covered, but actual two-machine transport and final game-bundle loading are not verified.

No scene writeback, model-factory POST, export, release, commit, frontend build, icon/model edits, CustomContentApi edits or docs08 edits were performed. Unity's existing domain-reload tooling may update its automatically staged runtime DLL artifact. Existing unrelated worktree changes were preserved.
