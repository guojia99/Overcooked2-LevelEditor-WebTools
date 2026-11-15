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
            harmony.Patch(typeof(PseudoPrefab).GetMethod("ResetChild"),
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
        if (Application.isPlaying || LayoutEditorPseudoReload.CallingOriginalInit)
            return true;
        LayoutEditorPseudoReload.SafeInit(__instance);
        return false;
    }

    private static bool ResetChildPrefix(PseudoPrefab __instance)
    {
        return !LayoutEditorPseudoReload.TryResetCustomModelPreview(__instance);
    }
}
