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
using LevelEditorStub;

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
    /// <summary>写回 SaveScene 前清掉 CleanPlateStackSeeder 的 DontSave 预览（防实例修改落盘）。</summary>
    public static void ClearCleanPlateStackPreviews()
    {
        MonoBehaviour[] all = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
        for (int i = 0; i < all.Length; i++)
        {
            MonoBehaviour mb = all[i];
            if (mb != null && mb.GetType().Name == "CleanPlateStackSeeder")
                mb.SendMessage("ClearPreview", SendMessageOptions.DontRequireReceiver);
        }
    }

    /// <summary>Same as Tools → Toggle Prepare For Building when entering cleared state.</summary>
    public static void EnsurePrepareForBuilding()
    {
        ClearCleanPlateStackPreviews();
        var manager = PseudoPrefabManager.Instance;
        if (manager == null)
            return;

        if (!manager.prepareForBuilding)
        {
            manager.prepareForBuilding = true;
            manager.DeInit();
        }
    }

    /// <summary>导出 prepare 专用：始终执行 DeInit 剥临时伪 prefab（等同 Toggle ON），
    /// 不因 prepareForBuilding 已为 true 而跳过。OpenScene 后 OnEnable 常会 Init
    /// 再挂上临时子物体，须在 HUD/tag 烘焙与 SaveScene 之前清理。</summary>
    public static void ForcePrepareForBuildingForExport()
    {
        var manager = PseudoPrefabManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[SetExporter] Prepare For Building：场景无 PseudoPrefabManager，跳过 DeInit");
            return;
        }
        var wasAlready = manager.prepareForBuilding;
        ClearCleanPlateStackPreviews();
        manager.prepareForBuilding = true;
        manager.DeInit();
        Debug.Log("[SetExporter] Prepare For Building：已 DeInit 清除临时物体（进入前 prepareForBuilding="
            + (wasAlready ? "true" : "false") + "）");
    }

    /// <summary>Refresh scene placeholders after layout write-back (avoids full recipe SetupConfig).</summary>
    public static void ReloadPseudoAssets()
    {
        var manager = PseudoPrefabManager.Instance;
        if (manager == null)
            return;

        EnsureCustomBundleDependency(manager);
        // 旧场景半损状态先提升派生运行时（缺运行时的回收台会让 Init/LateSetup NRE）。
        LayoutEditorStubIO.PromoteAllDerivedStubsInScene();

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
            LayoutEditorStubIO.RepairFullCleanPlateStackRuntimes();
            RepairMissingPseudoMaterials();
            // 载具子树重建会连带销毁可移动火锅的编辑模式预览锅，重新布防补挂
            LayoutEditorPushablePotPreview.Arm();
            return;
        }

        // Fallback for unpatched environments: full reload.
        SafeReinit(manager);
        LayoutEditorStubIO.RepairFullCleanPlateStackRuntimes();
        RepairMissingPseudoMaterials();
        LayoutEditorPushablePotPreview.Arm();
    }

    /// <summary>Full Tools → Reload Pseudo Assets (bootstrap + recipes + scene).</summary>
    public static void ReloadPseudoAssetsFull()
    {
        var manager = PseudoPrefabManager.Instance;
        if (manager == null)
            return;

        EnsureCustomBundleDependency(manager);
        // 写回主路径：先把存量派生 stub 提升为派生运行时，再 DeInit/Init。
        LayoutEditorStubIO.PromoteAllDerivedStubsInScene();
        LayoutEditorLog.Log("[dispenser-icon] ReloadPseudoAssetsFull: begin（写回主路径）");
        SafeReinit(manager);
        // 完整 prefab 的 CleanPlateStack 误挂 PseudoPrefabCleanPlateStack 会 NRE；
        // 必须在逐对象 ResetChild 之前清理/替换为 CustomStub.CleanPlateStackSeeder。
        LayoutEditorStubIO.RepairFullCleanPlateStackRuntimes();
        // 宿主 ResetAllPseudoPrefabs 一旦某个伪 prefab 抛异常，后续对象会被跳过
        // （典型：核心 Switch child 保留旧 bundle 材质 → Scene 视图洋红）。这里
        // 再做一遍逐对象 try/catch 重建，保证写回后所有 child 都拿到新 bundle 资产。
        RepairPseudoPrefabChildren();
        // 写回主路径：载具由 DeInit/Init 整体重建，预览锅随旧载具销毁；域重载/开场景
        // /退 Play 三时机覆盖不到不触发编译的纯 HTTP 写回，须在此重新布防补挂。
        LayoutEditorPushablePotPreview.Arm();
    }

    /// <summary>
    /// 逐个重建场景内 PseudoPrefab child。宿主 ResetAllPseudoPrefabs 无 try/catch，
    /// 一个对象失败会中断后续对象；本方法隔离失败，专门保证 bundle 材质伪 prefab
    /// （Switch / 压力开关 / 其他 common01 包装件）在 ReInit 后不残留已卸载材质。
    /// </summary>
    public static int RepairPseudoPrefabChildren()
    {
        // 先提升半损派生 stub，否则逐对象 ResetChild 仍会初始化基类空 Setup。
        LayoutEditorStubIO.PromoteAllDerivedStubsInScene();
        PseudoPrefab[] all = UnityEngine.Object.FindObjectsOfType<PseudoPrefab>();
        int rebuilt = 0;
        int failed = 0;
        for (int i = 0; i < all.Length; i++)
        {
            PseudoPrefab pseudo = all[i];
            if (pseudo == null)
                continue;
            try
            {
                pseudo.ResetChild();
                rebuilt++;
            }
            catch (Exception ex)
            {
                failed++;
                LayoutEditorLog.LogWarning("[pseudo-repair] ResetChild failed: "
                    + pseudo.name + " — " + ex.Message);
            }
        }
        LayoutEditorStubIO.ApplySwitchStartVisualsInActiveScene();
        LayoutEditorGridSnapGuard.RelaxGridSnapOnScene();
        if (rebuilt > 0 || failed > 0)
            LayoutEditorLog.Log("[pseudo-repair] child 重建: ok=" + rebuilt + " failed=" + failed);
        return rebuilt;
    }

    /// <summary>
    /// 只重建材质/Shader 已丢失或不受支持的 child（启动/开场景后调用，开销小）。
    /// 避免每次域重载把全部伪 prefab 都重建一遍。
    /// </summary>
    public static int RepairMissingPseudoMaterials()
    {
        PseudoPrefab[] all = UnityEngine.Object.FindObjectsOfType<PseudoPrefab>();
        int rebuilt = 0;
        int failed = 0;
        for (int i = 0; i < all.Length; i++)
        {
            PseudoPrefab pseudo = all[i];
            if (pseudo == null || pseudo.childGameObject == null)
                continue;
            if (!HasMissingPseudoMaterial(pseudo.childGameObject))
                continue;
            try
            {
                pseudo.ResetChild();
                rebuilt++;
            }
            catch (Exception ex)
            {
                failed++;
                LayoutEditorLog.LogWarning("[pseudo-repair] material ResetChild failed: "
                    + pseudo.name + " — " + ex.Message);
            }
        }
        if (rebuilt > 0)
        {
            LayoutEditorStubIO.ApplySwitchStartVisualsInActiveScene();
            LayoutEditorGridSnapGuard.RelaxGridSnapOnScene();
            LayoutEditorLog.Log("[pseudo-repair] 洋红/缺失材质 child 重建: " + rebuilt
                + "（失败 " + failed + "）");
        }
        return rebuilt;
    }

    /// <summary>场景打开后若 Switch 包装件的 child 尚未生成（宿主 Init 抛异常/竞态），
    /// 逐个 try/catch 重建；只针对 Switch，避免每次开场景重置全部伪 prefab。</summary>
    public static int RepairMissingSwitchChildren()
    {
        PseudoPrefab[] all = UnityEngine.Object.FindObjectsOfType<PseudoPrefab>();
        bool broken = false;
        for (int i = 0; i < all.Length; i++)
        {
            PseudoPrefab pseudo = all[i];
            if (pseudo == null)
                continue;
            PseudoPrefabSwitchStub stub = pseudo.GetComponent<PseudoPrefabSwitchStub>();
            if (stub != null && pseudo.childGameObject == null)
            {
                broken = true;
                break;
            }
        }
        if (!broken)
            return 0;
        return RepairPseudoPrefabChildren();
    }

    private static bool HasMissingPseudoMaterial(GameObject child)
    {
        if (child == null)
            return false;
        Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;
            Material[] mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0)
                continue;
            for (int j = 0; j < mats.Length; j++)
            {
                Material m = mats[j];
                if (m == null)
                    return true;
                Shader sh = m.shader;
                if (sh == null || !sh.isSupported)
                {
                    LayoutEditorLog.Log("[pseudo-repair] 材质异常: " + r.gameObject.name
                        + " mat=" + (m != null ? m.name : "<null>")
                        + " shader=" + (sh != null ? sh.name : "<null>")
                        + " supported=" + (sh != null && sh.isSupported));
                    return true;
                }
            }
        }
        return false;
    }

    [MenuItem("Layout Editor/诊断/重建伪 prefab child（修复洋红开关）")]
    public static void RepairPseudoPrefabChildrenMenu()
    {
        int rebuilt = RepairPseudoPrefabChildren();
        Debug.Log("[pseudo-repair] 手动重建完成: " + rebuilt);
    }

    /// <summary>写回 Reload 前：饮料/酱料机不得保留 PseudoPrefabDispenser 运行时（宿主 Setup NRE）。</summary>
    private static void HealSpecialDispenserRuntimesInScene()
    {
        var stubs = UnityEngine.Object.FindObjectsOfType<PseudoPrefabDispenserStub>();
        if (stubs == null || stubs.Length == 0)
            return;
        for (int i = 0; i < stubs.Length; i++)
        {
            var stub = stubs[i];
            if (stub == null)
                continue;
            string pid;
            if (!LayoutEditorStubIO.TryGetSpecialDispenserPrefabId(stub.gameObject, out pid))
                continue;
            LayoutEditorStubIO.PromoteDerivedStub(stub.gameObject);
        }
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
        HealSpecialDispenserRuntimesInScene();
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
        // commonW6 已切换为 bundle 引用：Init 前补齐 SO 指向的游戏 bundle。
        LayoutEditorCommonW6Dependencies.SyncForScene(SceneManager.GetActiveScene(), info);
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

/// <summary>场景打开/域重载后的一次性自愈：仅当伪 prefab child 的材质/Shader
/// 丢失时重建（核心 Switch 的 bundle 材质在 ReInit 竞态下会变洋红）。</summary>
[InitializeOnLoad]
static class LayoutEditorPseudoMaterialRepairHook
{
    static LayoutEditorPseudoMaterialRepairHook()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += OnDelay;
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += OnDelay;
    }

    static void OnDelay()
    {
        if (Application.isPlaying)
            return;
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            // 无迁移基线的旧关卡：茶杯回收台 returnClean=true -> false。
            // 新关卡在 CreateLevel 时已写基线，不受影响。标记在 Scene 保存/写回后落盘。
            if (LayoutEditorLayoutMigrations.ApplyToOpenScene(activeScene) > 0)
                EditorSceneManager.MarkSceneDirty(activeScene);
        }
        if (LayoutEditorStubIO.PromoteAllDerivedStubsInScene() > 0 && activeScene.IsValid())
            EditorSceneManager.MarkSceneDirty(activeScene);
        LayoutEditorStubIO.RepairFullCleanPlateStackRuntimes();
        LayoutEditorPseudoReload.RepairMissingPseudoMaterials();
        LayoutEditorPseudoReload.RepairMissingSwitchChildren();
        LayoutEditorStubIO.ApplySwitchStartVisualsInActiveScene();
    }
}
