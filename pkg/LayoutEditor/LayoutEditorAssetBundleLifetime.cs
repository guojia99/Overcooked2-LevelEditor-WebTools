using System;
using System.Collections.Generic;
using System.Reflection;
using LevelEditor;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

// Based on the community PreviewInitAssetBundleFix: validate native objects,
// clear stale manager state, then reload through the existing preview pipeline.
[InitializeOnLoad]
internal static class LayoutEditorAssetBundleLifetime
{
    private static readonly FieldInfo BundleField = typeof(PseudoPrefabManager).GetField(
        "bundleDict", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ManifestField = typeof(PseudoPrefabManager).GetField(
        "assetBundleManifest", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo LoadBundlesMethod = typeof(PseudoPrefabManager).GetMethod(
        "EnsureLoadAllAssetBundles", BindingFlags.Instance | BindingFlags.NonPublic);
    private static bool _recovering;
    private static bool _rebuildPending;
    private static double _deadline;
    private static double _nextCheck;
    private static int _attempts;

    static LayoutEditorAssetBundleLifetime()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        QueueRecovery();
    }

    internal static bool CanRepair(PseudoPrefabManager manager)
    {
        return manager != null && !Application.isPlaying
            && !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling && !EditorApplication.isUpdating
            && !BuildPipeline.isBuildingPlayer && !manager.prepareForBuilding
            && !LayoutEditorPseudoReload.ExportBuildInProgress;
    }

    internal static bool HasManifest(PseudoPrefabManager manager)
    {
        return ManifestField != null
            && ManifestField.GetValue(manager) as AssetBundleManifest != null;
    }

    internal static string InvalidStateReason(PseudoPrefabManager manager, string[] dependencies)
    {
        if (BundleField == null || ManifestField == null)
            throw new InvalidOperationException("Preview bundle cache fields were not found.");
        var bundles = BundleField.GetValue(manager) as Dictionary<string, AssetBundle>;
        if (bundles == null)
            return "bundle dictionary is null";
        foreach (var pair in bundles)
            if (pair.Value == null)
                return "destroyed bundle: " + pair.Key;

        object reference = ManifestField.GetValue(manager);
        var manifest = reference as AssetBundleManifest;
        if (reference != null && manifest == null)
            return "destroyed manifest";
        // A fresh cache (or the dispenser preloader) has no manifest yet.
        if (manifest == null)
            return null;
        if (!bundles.ContainsKey("Windows"))
            return "manifest bundle Windows is missing";
        if (dependencies == null)
            return null;
        foreach (string dependency in dependencies)
        {
            if (string.IsNullOrEmpty(dependency))
                continue;
            if (!bundles.ContainsKey(dependency))
                return "required bundle is missing: " + dependency;
            foreach (string transitive in manifest.GetAllDependencies(dependency))
                if (!bundles.ContainsKey(transitive))
                    return "transitive bundle is missing: " + transitive;
        }
        return null;
    }

    internal static bool ClearStaleState(PseudoPrefabManager manager, string[] dependencies)
    {
        if (!CanRepair(manager))
            return false;
        string reason = InvalidStateReason(manager, dependencies);
        if (reason == null)
            return false;
        var bundles = BundleField.GetValue(manager) as Dictionary<string, AssetBundle>;
        var unloaded = new HashSet<AssetBundle>();
        if (bundles != null)
        {
            foreach (var bundle in new List<AssetBundle>(bundles.Values))
            {
                if (bundle == null || !unloaded.Add(bundle))
                    continue;
                bundle.Unload(true);
            }
            bundles.Clear();
        }
        else
            BundleField.SetValue(manager, new Dictionary<string, AssetBundle>());
        ManifestField.SetValue(manager, null);
        if (manager.editedMaterials != null)
            manager.editedMaterials.Clear();
        LayoutEditorLog.Log("[bundle-lifetime] Cleared stale preview state: " + reason
            + "; unloaded=" + unloaded.Count);
        _rebuildPending = true;
        QueueRecovery();
        return true;
    }

    internal static void EnsureBundlesLoaded(PseudoPrefabManager manager)
    {
        if (LoadBundlesMethod == null)
            throw new InvalidOperationException("Preview bundle loader was not found.");
        LoadBundlesMethod.Invoke(manager, null);
    }

    // Never nest a full Init inside ResetChild; the deferred pass rebuilds all
    // previews once the native Play/domain-reload teardown has finished.
    internal static bool CanResetChild(PseudoPrefab pseudo)
    {
        if (Application.isPlaying)
            return true;
        var manager = PseudoPrefabManager.Instance;
        // A blocked repair is not a ready cache. Start can run while an import
        // or Play transition has deferred SafeInit and left the dictionary empty.
        if (!CanRepair(manager))
        {
            QueueRecovery();
            return false;
        }
        var stub = pseudo.GetComponent<PseudoPrefabStub>();
        var so = stub != null ? stub.pseudoPrefabSO : null;
        string[] required = so != null && so.asset == null && !string.IsNullOrEmpty(so.bundleName)
            ? new string[] { so.bundleName } : null;
        if (HasManifest(manager) && InvalidStateReason(manager, required) == null)
            return true;
        QueueRecovery();
        return false;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            _deadline = 0;
            _attempts = 0;
            QueueRecovery();
        }
        else
            EditorApplication.update -= Recover;
    }

    internal static void QueueRecovery()
    {
        // Repeated Start/ResetChild calls must not reset the retry budget.
        if (_attempts >= 2)
            return;
        EditorApplication.update -= Recover;
        EditorApplication.update += Recover;
        if (_deadline > EditorApplication.timeSinceStartup)
            return;
        _deadline = EditorApplication.timeSinceStartup + 15.0;
        _nextCheck = 0;
        _attempts = 0;
    }

    private static void Recover()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now > _deadline)
        {
            EditorApplication.update -= Recover;
            return;
        }
        if (now < _nextCheck)
            return;
        _nextCheck = now + 0.5;
        var manager = PseudoPrefabManager.Instance;
        if (!CanRepair(manager) || LayoutEditorPseudoReload.CallingOriginalInit || _recovering)
            return;
        _recovering = true;
        try
        {
            if (manager.stub == null)
                manager.stub = manager.GetComponent<PseudoPrefabManagerStub>();
            var info = manager.stub != null ? manager.stub.levelInfo : null;
            if (info == null)
                return;
            string[] dependencies = LayoutEditorPseudoReload.GetPreviewDependencies(manager, info);
            bool needsInit = !HasManifest(manager) || InvalidStateReason(manager, dependencies) != null;
            if (needsInit)
            {
                _attempts++;
                LayoutEditorPseudoReload.SafeInit(manager);
                if (!HasManifest(manager) || InvalidStateReason(manager, dependencies) != null)
                {
                    if (_attempts >= 2)
                    {
                        LayoutEditorLog.LogWarning("[bundle-lifetime] Preview recovery failed twice; stopped.");
                        EditorApplication.update -= Recover;
                    }
                    return;
                }
                _rebuildPending = true;
            }
            bool rebuilt = _rebuildPending;
            if (rebuilt)
            {
                _rebuildPending = false;
                LayoutEditorPseudoReload.RepairPseudoPrefabChildren();
                LayoutEditorPseudoReload.RepairMissingPseudoMaterials();
                LayoutEditorPseudoReload.RepairBrokenPressureSwitchChildren();
                // ResetChild replaces objects referenced by other previews. Restore
                // cross-object links only after every replacement has completed.
                foreach (var pseudo in UnityEngine.Object.FindObjectsOfType<PseudoPrefab>())
                {
                    try { pseudo.LateSetup(); }
                    catch (Exception ex)
                    {
                        LayoutEditorLog.LogWarning("[bundle-lifetime] LateSetup failed: " + pseudo.name + ": " + ex.Message);
                    }
                }
                foreach (var custom in UnityEngine.Object.FindObjectsOfType<SetupCustomPrefab>())
                {
                    try { custom.LateSetup(); }
                    catch (Exception ex)
                    {
                        LayoutEditorLog.LogWarning("[bundle-lifetime] Custom LateSetup failed: " + custom.name + ": " + ex.Message);
                    }
                }
                LayoutEditorPushablePotPreview.Arm();
                LayoutEditorLog.Log("[bundle-lifetime] Preview bundle recovery completed.");
            }
            if (!rebuilt)
            {
                LayoutEditorPseudoReload.RepairMissingPseudoMaterials();
                LayoutEditorPseudoReload.RepairBrokenPressureSwitchChildren();
            }
            EditorApplication.update -= Recover;
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[bundle-lifetime] Preview recovery failed: " + ex);
            EditorApplication.update -= Recover;
        }
        finally
        {
            _recovering = false;
        }
    }
}
