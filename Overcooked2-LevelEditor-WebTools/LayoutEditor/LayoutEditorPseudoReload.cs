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
        RuntimePrefabManager.ClearAllRuntimePrefabs(true);
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

        // 木纹·中秋等新结构食材箱：宿主图标逻辑查不到渲染器会打断 Init，
        // 先给已加载 bundle 里的真实 prefab 根补种子渲染器（见 LayoutEditorDispenserIconFix）。
        LayoutEditorLog.Log("[dispenser-icon] ReloadPseudoAssets: light path begin（seed → ReloadSceneLayoutPrefabs）");
        LayoutEditorDispenserIconFix.SeedSceneDispenserPrefabs();

        // ReloadSceneLayoutPrefabs is added by the project patch; call it via
        // reflection so this compiles against an unpatched decompiled PseudoPrefabManager.
        var mi = manager.GetType().GetMethod(
            "ReloadSceneLayoutPrefabs",
            BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            try
            {
                mi.Invoke(manager, null);
                LayoutEditorLog.Log("[dispenser-icon] ReloadPseudoAssets: ReloadSceneLayoutPrefabs ok");
            }
            catch (Exception ex)
            {
                LayoutEditorLog.LogWarning("[dispenser-icon] ReloadSceneLayoutPrefabs failed: " + ex);
            }
            LayoutEditorDispenserIconFix.SyncSeededIcons();
            LayoutEditorGridSnapGuard.RelaxGridSnapOnScene();
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
        LayoutEditorLog.Log("[dispenser-icon] ReloadPseudoAssetsFull: begin（写回主路径）");
        SafeReinit(manager);
    }

    /// <summary>DeInit + Init，带 try/catch：宿主原版 PseudoPrefabManager 对缺失 bundle
    ///  会抛 KeyNotFoundException，插件侧不能因此中断（缺失的应已被依赖写入守卫过滤，
    ///  这里仅作最后防线）。DeInit 会卸载全部 bundle（种子随缓存资产一并销毁），故
    ///  Init 前先预载依赖 bundle 注入 manager 并补种子（见 LayoutEditorDispenserIconFix）；
    ///  Init 仍失败时补种子重试一次（自愈守卫兜底 Play/开场景路径）。</summary>
    private static void SafeReinit(PseudoPrefabManager manager)
    {
        LayoutEditorLog.Log("[dispenser-icon] SafeReinit: DeInit begin");
        try
        {
            // 旧关卡的 LevelInfoSO 可能缺音频数组字段，宿主 Init 遍历时会是 null。
            if (manager.stub != null)
                LayoutEditorLevelInfoSanitizer.Sanitize(manager.stub.levelInfo);
            manager.DeInit();
            manager.prepareForBuilding = false;
            LayoutEditorLog.Log("[dispenser-icon] SafeReinit: DeInit ok（bundle 全部卸载，种子随之销毁）");
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[dispenser-icon] PseudoPrefabManager de-init failed: " + ex);
        }
        if (!LayoutEditorDispenserIconFix.PreloadAndSeed(manager))
        {
            LayoutEditorLog.Log("[dispenser-icon] SafeReinit: 预载不可用，退回仅对已加载 bundle 补种子");
            LayoutEditorDispenserIconFix.SeedSceneDispenserPrefabs();
        }
        try
        {
            manager.Init();
            LayoutEditorLog.Log("[dispenser-icon] SafeReinit: Init ok");
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[dispenser-icon] PseudoPrefabManager re-init failed: " + ex);
            // 新结构食材箱（木纹·中秋等）图标逻辑的 MissingComponentException 会在此出现：
            // 此时 bundle 已由失败的 Init 加载完毕，补种子后重试一次完整 Init。
            if (LayoutEditorDispenserIconFix.SeedSceneDispenserPrefabs())
            {
                try
                {
                    manager.Init();
                    LayoutEditorLog.Log("[dispenser-icon] PseudoPrefabManager re-init (seeded) ok");
                }
                catch (Exception ex2)
                {
                    LayoutEditorLog.LogWarning("[dispenser-icon] PseudoPrefabManager re-init retry failed: " + ex2);
                }
            }
            else
            {
                LayoutEditorLog.Log("[dispenser-icon] SafeReinit: 重试前补种子为 0 个（无需或失败，见上方 Seed 日志）");
            }
        }
        LayoutEditorDispenserIconFix.SyncSeededIcons();
        LayoutEditorLog.Log("[dispenser-icon] SafeReinit: end");
        LayoutEditorGridSnapGuard.RelaxGridSnapOnScene();
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
