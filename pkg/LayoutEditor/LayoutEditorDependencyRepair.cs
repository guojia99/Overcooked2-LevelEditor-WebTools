using System;
using System.Reflection;
using HarmonyLib;
using LevelEditor;
using UnityEditor;
using UnityEngine;

// Install before scene restoration, including the first OnEnable and later Start calls.
[InitializeOnLoad]
static class LayoutEditorDependencyRepair
{
    static LayoutEditorDependencyRepair()
    {
        try
        {
            var harmony = new Harmony("oc2.layouteditor.previewinit");
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            harmony.Patch(typeof(PseudoPrefabManager).GetMethod("Init"),
                prefix: new HarmonyMethod(typeof(LayoutEditorDependencyRepair).GetMethod("InitPrefix", flags)));
            harmony.Patch(typeof(PseudoPrefabManager).GetMethod("EnsureLoadAllAssetBundles", BindingFlags.Instance | BindingFlags.NonPublic),
                postfix: new HarmonyMethod(typeof(LayoutEditorDependencyRepair).GetMethod("LoadSharedBundlesPostfix", flags)));
            harmony.Patch(typeof(PseudoPrefab).GetMethod("ResetChild"),
                prefix: new HarmonyMethod(typeof(LayoutEditorDependencyRepair).GetMethod("ResetChildPrefix", flags)));
            harmony.Patch(typeof(PseudoPrefabReplaceMaterial).GetMethod("ResetChild"),
                prefix: new HarmonyMethod(typeof(LayoutEditorDependencyRepair).GetMethod("ResetChildPrefix", flags)));
            Debug.Log("[preview-init] Installed Init and ResetChild guards (OnEnable/Start)");
        }
        catch (Exception ex)
        {
            Debug.LogError("[preview-init] Guard installation failed: " + ex);
        }
    }

    private static bool InitPrefix(PseudoPrefabManager __instance)
    {
        if (__instance.stub == null) __instance.stub = __instance.GetComponent<LevelEditorStub.PseudoPrefabManagerStub>();
        if (__instance.stub != null) LayoutEditorNativeDependencies.Clean(__instance.stub.levelInfo);
        if (Application.isPlaying || LayoutEditorPseudoReload.CallingOriginalInit)
            return true;
        LayoutEditorPseudoReload.SafeInit(__instance);
        return false;
    }

    // Runs after the native manifest is ready, before SetAssetRef, in Edit and Play.
    private static void LoadSharedBundlesPostfix(PseudoPrefabManager __instance)
    {
        if (__instance.prepareForBuilding || BuildPipeline.isBuildingPlayer
            || !LayoutEditorAssetBundleLifetime.HasManifest(__instance)) return;
        var roots = new System.Collections.Generic.List<UnityEngine.Object>();
        if (__instance.stub != null && __instance.stub.levelInfo != null) roots.Add(__instance.stub.levelInfo);
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.scene == __instance.gameObject.scene && go.transform.parent == null) roots.Add(go);
        var names = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in EditorUtility.CollectDependencies(roots.ToArray()))
        {
            var so = asset as LevelEditorStub.PseudoPrefabSO;
            if (so != null && !string.IsNullOrEmpty(so.bundleName)) names.Add(so.bundleName);
        }
        var load = typeof(PseudoPrefabManager).GetMethod("LoadAssetBundle", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var name in names)
            if (!string.IsNullOrEmpty(name) && name.StartsWith("common", StringComparison.OrdinalIgnoreCase)
                && LayoutEditorCatalogApi.BundleFileExists(name))
                load.Invoke(__instance, new object[] { name, false });
    }

    private static bool ResetChildPrefix(PseudoPrefab __instance)
    {
        if (!LayoutEditorAssetBundleLifetime.CanResetChild(__instance))
            return false;
        return !LayoutEditorPseudoReload.TryResetCustomModelPreview(__instance);
    }
}
