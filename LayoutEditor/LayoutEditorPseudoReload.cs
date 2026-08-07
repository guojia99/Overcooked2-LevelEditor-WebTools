using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LevelEditor;

[InitializeOnLoad]
static class LayoutEditorSceneCleanup
{
    static LayoutEditorSceneCleanup()
    {
        EditorSceneManager.sceneClosing += OnSceneClosing;
    }

    static void OnSceneClosing(Scene scene, bool removingScene)
    {
        if (!removingScene)
            return;
        RuntimePrefabManager.ClearAllRuntimePrefabs();
    }
}

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

        EnsureCustomBundleDependency(manager);

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
        SafeReinit(manager);
    }

    /// <summary>Full Tools → Reload Pseudo Assets (bootstrap + recipes + scene).</summary>
    public static void ReloadPseudoAssetsFull()
    {
        var manager = PseudoPrefabManager.Instance;
        if (manager == null)
            return;

        EnsureCustomBundleDependency(manager);
        SafeReinit(manager);
    }

    /// <summary>DeInit + Init，带 try/catch：宿主原版 PseudoPrefabManager 对缺失 bundle
    ///  会抛 KeyNotFoundException，插件侧不能因此中断（缺失的应已被依赖写入守卫过滤，
    ///  这里仅作最后防线）。</summary>
    private static void SafeReinit(PseudoPrefabManager manager)
    {
        try
        {
            // 旧关卡的 LevelInfoSO 可能缺音频数组字段，宿主 Init 遍历时会是 null。
            if (manager.stub != null)
                LayoutEditorLevelInfoSanitizer.Sanitize(manager.stub.levelInfo);
            manager.DeInit();
            manager.prepareForBuilding = false;
            manager.Init();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LayoutEditor] PseudoPrefabManager re-init failed: " + ex.Message);
        }
    }

    /// <summary>若本关卡集的自定义菜谱 bundle 已构建，则补入 levelInfo.dependencies，
    ///  保证宿主原版 PseudoPrefabManager 在 Init 时能加载它；未构建时不写入（避免异常）。</summary>
    private static void EnsureCustomBundleDependency(PseudoPrefabManager manager)
    {
        var info = manager.stub != null ? manager.stub.levelInfo : null;
        if (info == null)
            return;
        var assetPath = AssetDatabase.GetAssetPath(info);
        var parts = (assetPath ?? "").Replace('\\', '/').Split('/');
        if (parts.Length <= 2 || parts[1] != "LevelSets")
            return;
        var customBundle = parts[2] + "/custom_recipes";
        var deps = info.dependencies != null ? new List<string>(info.dependencies) : new List<string>();
        if (deps.Contains(customBundle))
            return;
        var bundlePath = Path.Combine(Application.streamingAssetsPath, "Windows/" + customBundle).Replace('\\', '/');
        if (!File.Exists(bundlePath))
            return;
        Undo.RecordObject(info, "Layout Editor Ensure Custom Bundle");
        deps.Add(customBundle);
        info.dependencies = deps.ToArray();
        EditorUtility.SetDirty(info);
        AssetDatabase.SaveAssets();
    }
}
