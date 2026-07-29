using System.Reflection;
using LevelEditor;
using UnityEditor;

public static class LayoutEditorPseudoReload
{
    /// <summary>Same as Tools → Toggle Prepare For Building when entering cleared state.</summary>
    public static void EnsurePrepareForBuilding()
    {
        var manager = PseudoPrefabManager.Instance;
        if (manager == null)
            return;

        if (!manager.prepareForBuilding)
        {
            manager.prepareForBuilding = true;
            manager.DeInit();
        }
    }

    /// <summary>Refresh scene placeholders after layout write-back (avoids full recipe SetupConfig).</summary>
    public static void ReloadPseudoAssets()
    {
        var manager = PseudoPrefabManager.Instance;
        if (manager == null)
            return;

        // ReloadSceneLayoutPrefabs is added by the project patch; call it via
        // reflection so this compiles against an unpatched decompiled PseudoPrefabManager.
        var mi = manager.GetType().GetMethod(
            "ReloadSceneLayoutPrefabs",
            BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            mi.Invoke(manager, null);
            return;
        }

        // Fallback for unpatched environments: full reload.
        manager.DeInit();
        manager.prepareForBuilding = false;
        manager.Init();
    }

    /// <summary>Full Tools → Reload Pseudo Assets (bootstrap + recipes + scene).</summary>
    public static void ReloadPseudoAssetsFull()
    {
        var manager = PseudoPrefabManager.Instance;
        if (manager == null)
            return;

        manager.DeInit();
        manager.prepareForBuilding = false;
        manager.Init();
    }
}
