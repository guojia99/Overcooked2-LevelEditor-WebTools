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

/// <summary>导出准备失败（没有 PseudoPrefabManager，或 DeInit 抛错）。导出必须中止，
/// 不能在临时物体还在的情况下保存或构建。</summary>
public class LayoutEditorExportPrepareException : Exception
{
    public LayoutEditorExportPrepareException(string message) : base(message)
    {
    }

    public LayoutEditorExportPrepareException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public static class LayoutEditorPseudoReload
{
    /// <summary>导出正在 BuildAssetBundles。期间 LayoutEditor 的场景打开/延迟回调
    /// 不得再改场景或挂临时物体。</summary>
    public static bool ExportBuildInProgress;
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
        PrepareSceneForWrite(SceneManager.GetActiveScene(), false);
    }

    public static void PrepareSceneForWrite(Scene scene, bool requireManager)
    {
        var block = LayoutEditorSafety.PlayModeWriteBlockReason();
        if (!string.IsNullOrEmpty(block))
            throw new LayoutEditorExportPrepareException(block);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new LayoutEditorExportPrepareException("Cannot prepare an unloaded scene.");
        // Host DeInit clears pseudo children globally, even when called on one manager.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var other = SceneManager.GetSceneAt(i);
            if (other == scene || !other.IsValid() || !other.isLoaded)
                continue;
            foreach (var root in other.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<PseudoPrefabManager>(true) != null
                    || root.GetComponentInChildren<PseudoPrefab>(true) != null
                    || root.GetComponentInChildren<SetupCustomPrefab>(true) != null)
                    throw new LayoutEditorExportPrepareException(
                        "请先关闭其他已加载的伪预制体场景再写回，避免全局 DeInit 清理其他场景：" + other.path);
            }
        }
        ClearCleanPlateStackPreviews();
        int prepared = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var manager in root.GetComponentsInChildren<PseudoPrefabManager>(true))
            {
                ForcePrepareManagerStrict(manager, scene.path);
                prepared++;
            }
        }
        if (prepared == 0 && requireManager)
            throw new LayoutEditorExportPrepareException("No PseudoPrefabManager in scene: " + scene.path);
    }

    public static void SaveScenePrepared(Scene scene, bool restorePreview)
    {
        try
        {
            PrepareSceneForWrite(scene, false);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new LayoutEditorExportPrepareException("SaveScene failed: " + scene.path);
        }
        catch (LayoutEditorExportPrepareException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LayoutEditorExportPrepareException("Scene save failed: " + scene.path, ex);
        }
        finally
        {
            if (restorePreview)
                TryRestoreWritePreview();
        }
    }

    public static void TryRestoreWritePreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || ExportBuildInProgress)
            return;
        try
        {
            ReloadPseudoAssetsFull();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LayoutEditor] Restore preview failed: " + ex);
        }
    }

    /// <summary>导出 prepare 专用：始终执行 DeInit 剥临时伪 prefab（等同 Toggle ON），
    /// 不因 prepareForBuilding 已为 true 而跳过。OpenScene 后 OnEnable 常会 Init
    /// 再挂上临时子物体，须在 HUD/tag 烘焙与 SaveScene 之前清理。</summary>
    public static void ForcePrepareForBuildingForExport()
    {
        PrepareSceneForWrite(SceneManager.GetActiveScene(), false);
    }

    /// <summary>导出专用：强制 Toggle Prepare For Building 的 ON 侧（标志置 true 并 DeInit）。
    /// 不因标志已经是 true 而跳过。requireManager 为 true 且场景没有管理器、或 DeInit 失败时抛
    /// <see cref="LayoutEditorExportPrepareException"/>，调用方不得继续保存或构建。</summary>
    public static void ForcePrepareForExportStrict(bool requireManager)
    {
        PrepareSceneForWrite(SceneManager.GetActiveScene(), requireManager);
    }

    private static void ForcePrepareManagerStrict(PseudoPrefabManager manager, string scenePath)
    {
        manager.prepareForBuilding = true;
        try
        {
            manager.DeInit();
        }
        catch (Exception ex)
        {
            manager.prepareForBuilding = false;
            throw new LayoutEditorExportPrepareException(
                "Prepare For Building DeInit 失败（" + scenePath + "）：" + ex.Message, ex);
        }
        if (!manager.prepareForBuilding)
            throw new LayoutEditorExportPrepareException(
                "Prepare For Building 未能保持开启：" + scenePath);
    }

    /// <summary>Refresh scene placeholders after layout write-back (avoids full recipe SetupConfig).</summary>
    public static void ReloadPseudoAssets()
    {
        var manager = PseudoPrefabManager.Instance;
        if (manager == null)
            return;

        EnsureCustomBundleDependency(manager);
        var pancakeManager = manager.GetComponent<PseudoPrefabManagerStub>();
        LayoutEditorPancakeFill.SyncForScene(manager.gameObject.scene,
            pancakeManager != null ? pancakeManager.levelInfo : null);
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
        var pancakeManager = manager.GetComponent<PseudoPrefabManagerStub>();
        LayoutEditorPancakeFill.SyncForScene(manager.gameObject.scene,
            pancakeManager != null ? pancakeManager.levelInfo : null);
        // 写回主路径：先把存量派生 stub 提升为派生运行时，再 DeInit/Init。
        LayoutEditorStubIO.PromoteAllDerivedStubsInScene();
        // 外观材质 SO 先校验：非 .mat 的 SO 会让宿主导入重入并在 Setup 中销毁 child。
        LayoutEditorSwitchMaterialRepair.SanitizeAllInScene();
        LayoutEditorLog.Log("[dispenser-icon] ReloadPseudoAssetsFull: begin（写回主路径）");
        SafeReinit(manager);
        // 茶杯完整 prefab 误挂 PseudoPrefabCleanPlateStack 会 NRE，换成 seeder；
        // 包装堆被误挂 seeder 时在这里拆掉并补回派生运行时。须在逐对象 ResetChild 之前。
        LayoutEditorStubIO.RepairFullCleanPlateStackRuntimes();
        // 宿主 ResetAllPseudoPrefabs 一旦某个伪 prefab 抛异常，后续对象会被跳过
        // （典型：核心 Switch child 保留旧 bundle 材质 → Scene 视图洋红）。这里
        // 再做一遍逐对象 try/catch 重建，保证写回后所有 child 都拿到新 bundle 资产。
        RepairPseudoPrefabChildren();
        // 压力开关 m_buttonBit 失效兜底（host Setup 首行 MissingReference）。
        RepairBrokenPressureSwitchChildren();
        var previews = UnityEngine.Object.FindObjectsOfType<PseudoPrefab>();
        int missingChildren = 0;
        for (int i = 0; i < previews.Length; i++)
        {
            if (previews[i].childGameObject == null)
            {
                missingChildren++;
                LayoutEditorLog.LogWarning("[pseudo-reload] Missing child: " + previews[i].name);
            }
        }
        LayoutEditorLog.Log("[pseudo-reload] Preview children: total=" + previews.Length
            + " present=" + (previews.Length - missingChildren) + " missing=" + missingChildren);
        // 写回主路径：载具由 DeInit/Init 整体重建，预览锅随旧载具销毁；域重载/开场景
        // /退 Play 三时机覆盖不到不触发编译的纯 HTTP 写回，须在此重新布防补挂。
        LayoutEditorPushablePotPreview.Arm();
    }

    /// <summary>
    /// 逐个重建场景内 PseudoPrefab child。宿主 ResetAllPseudoPrefabs 无 try/catch，
    /// 一个对象失败会中断后续对象；本方法隔离失败，专门保证 bundle 材质伪 prefab
    /// （Switch / 压力开关 / 其他 common01 包装件）在 ReInit 后不残留已卸载材质。
    /// </summary>
    /// <summary>压力开关 child 的 m_buttonBit 在 bundle 重载时可能变成已销毁引用
    /// （Setup 首行 MissingReferenceException）。把引用重新指到新 child 的 Pad
    /// MeshRenderer 并补跑 Setup；无问题返回 false。host 文件只读，自愈落编辑器侧。</summary>
    public static bool RepairBrokenPressureSwitchChild(PseudoPrefab pseudo)
    {
        if (pseudo == null)
            return false;
        if (pseudo.GetComponent<PseudoPrefabPressureSwitchStub>() == null)
            return false;

        // 只修已实例化的 child；不在这里主动 ResetChild（避免再次触发 host 的
        // 重入/半销毁时序，把一次错误变成两次）。
        var child = pseudo.childGameObject;
        if (child == null)
            return false;

        var cos = child.GetComponent("PressureSwitchCosmeticDecisions");
        if (cos == null)
            return false;
        var t = cos.GetType();
        var bitField = t.GetField("m_buttonBit",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (bitField == null)
            return false;
        var bit = bitField.GetValue(cos) as MeshRenderer;
        if (bit != null)
            return false; // 引用正常

        MeshRenderer pad = null;
        var renderers = child.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null)
                continue;
            if (string.Equals(r.gameObject.name, "Pad", StringComparison.Ordinal))
            {
                pad = r;
                break;
            }
        }
        if (pad == null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null)
                    continue;
                if (r.gameObject.name.IndexOf("Pad", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pad = r;
                    break;
                }
            }
        }
        if (pad == null)
            return false;

        bitField.SetValue(cos, pad);
        try
        {
            pseudo.Setup();
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[pseudo-repair] 压力开关补跑 Setup 失败: "
                + pseudo.name + " — " + ex.Message);
        }
        return true;
    }

    /// <summary>扫描场景内全部压力开关，修复 m_buttonBit 指向已销毁 Pad 的 child
    /// （场景打开/域重载后的兜底；正常对象零操作）。</summary>
    public static int RepairBrokenPressureSwitchChildren()
    {
        PseudoPrefab[] all = UnityEngine.Object.FindObjectsOfType<PseudoPrefab>();
        int repaired = 0;
        for (int i = 0; i < all.Length; i++)
        {
            try
            {
                if (RepairBrokenPressureSwitchChild(all[i]))
                    repaired++;
            }
            catch (Exception ex)
            {
                LayoutEditorLog.LogWarning("[pseudo-repair] 压力开关自愈异常: "
                    + (all[i] != null ? all[i].name : "?") + " — " + ex.Message);
            }
        }
        if (repaired > 0)
            LayoutEditorLog.Log("[pseudo-repair] 压力开关 child 自愈: " + repaired + " 个");
        return repaired;
    }

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
                // 压力开关宿主的 PressureSwitchCosmeticDecisions.m_buttonBit 在
                // bundle 重载/重入时可能指向已销毁的 Pad MeshRenderer，Setup 首行
                // 即抛 MissingReferenceException。这里把 m_buttonBit 重新指到新
                // child 的 Pad 并补跑 Setup（host 文件不可改，编辑器侧自愈）。
                if (RepairBrokenPressureSwitchChild(pseudo))
                {
                    rebuilt++;
                    LayoutEditorLog.LogWarning("[pseudo-repair] ResetChild 失败后已修复压力开关 child: "
                        + pseudo.name + " — " + ex.Message);
                }
                else
                {
                    failed++;
                    LayoutEditorLog.LogWarning("[pseudo-repair] ResetChild failed: "
                        + pseudo.name + " — " + ex);
                }
            }
        }
        LayoutEditorStubIO.ApplySwitchStartVisualsInActiveScene();
        LayoutEditorGridSnapGuard.RelaxGridSnapOnScene();
        if (rebuilt > 0 || failed > 0)
            LayoutEditorLog.Log("[pseudo-repair] child 重建: ok=" + rebuilt + " failed=" + failed);
        return rebuilt;
    }

    private static GameObject GetSelfReferencingCustomPreview(PseudoPrefab pseudo)
    {
        var manager = PseudoPrefabManager.Instance;
        var info = manager != null && manager.stub != null ? manager.stub.levelInfo : null;
        if (info == null)
            return null;
        string setName = GetLevelSetName(info);
        if (string.IsNullOrEmpty(setName))
            return null;
        var stub = pseudo.GetComponent<PseudoPrefabStub>();
        var source = stub != null && stub.pseudoPrefabSO != null ? stub.pseudoPrefabSO.asset as GameObject : null;
        if (source == null || !AssetDatabase.GetAssetPath(source).StartsWith(
            "Assets/LevelSets/" + setName + "/custom_content/", StringComparison.Ordinal))
            return null;
        var inner = source.GetComponent<PseudoPrefabStub>();
        return inner != null && inner.pseudoPrefabSO == stub.pseudoPrefabSO
            && source.GetComponent<PseudoPrefab>() != null ? source : null;
    }

    internal static bool TryResetCustomModelPreview(PseudoPrefab pseudo)
    {
        if (Application.isPlaying || pseudo == null)
            return false;
        var source = GetSelfReferencingCustomPreview(pseudo);
        if (source == null)
            return false;
        // Clone inactive so wrapper Start cannot recursively instantiate itself.
        var staging = new GameObject("Custom model preview staging");
        staging.hideFlags = HideFlags.HideAndDontSave;
        staging.SetActive(false);
        try
        {
            var preview = UnityEngine.Object.Instantiate(source, staging.transform, false);
            preview.name = source.name + " (model preview)";
            foreach (var inner in preview.GetComponentsInChildren<PseudoPrefab>(true))
                UnityEngine.Object.DestroyImmediate(inner);
            foreach (var t in preview.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave;
            pseudo.ClearChild();
            preview.transform.SetParent(pseudo.transform, false);
            preview.transform.localPosition = Vector3.zero;
            preview.transform.localRotation = Quaternion.identity;
            pseudo.childGameObject = preview;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(staging);
        }
        return true;
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
        manager.prepareForBuilding = false;
        SafeInit(manager);
    }

    internal static bool CallingOriginalInit;

    internal static void SafeInit(PseudoPrefabManager manager)
    {
        if (manager.stub == null)
            manager.stub = manager.GetComponent<PseudoPrefabManagerStub>();
        var info = manager.stub != null ? manager.stub.levelInfo : null;
        // Run before taking the dependency snapshot and before native Init/preload.
        LayoutEditorPancakeFill.SyncForScene(manager.gameObject.scene, info);
        string[] persistentDependencies = info != null ? info.dependencies : null;
        bool filteredDependencies = false;
        bool previousCallingOriginal = CallingOriginalInit;
        try
        {
            CallingOriginalInit = true;
            if (info != null && persistentDependencies != null)
            {
                string setName = GetLevelSetName(info);
                if (!string.IsNullOrEmpty(setName))
                {
                    var runtimeDependencies = new List<string>();
                    var skippedDependencies = new List<string>();
                    bool skipCommon04 = Array.IndexOf(persistentDependencies, "common04") >= 0
                        && AssetDatabase.IsValidFolder("Assets/LevelSets/" + setName + "/custom_content")
                        && !File.Exists(Path.Combine(Application.streamingAssetsPath, "Windows/common04"))
                        && CanPreviewWithoutCommon04(manager, info);
                    for (int i = 0; i < persistentDependencies.Length; i++)
                    {
                        string dependency = persistentDependencies[i];
                        if (IsUnbuiltLocalCustomDependency(setName, dependency)
                            || (dependency == "common04" && skipCommon04))
                            skippedDependencies.Add(dependency);
                        else
                            runtimeDependencies.Add(dependency);
                    }
                    if (skippedDependencies.Count > 0)
                    {
                        info.dependencies = runtimeDependencies.ToArray();
                        filteredDependencies = true;
                        LayoutEditorLog.LogWarning("[pseudo-reload] Init 临时跳过未构建的本地自定义 bundle："
                            + string.Join(", ", skippedDependencies.ToArray())
                            + "；LevelInfoSO.dependencies 将在 Init 后恢复");
                    }
                }
            }

            // PreloadAndSeed also traverses dependencies; filter before it as well as Init.
            var bundleField = manager.GetType().GetField("bundleDict", BindingFlags.Instance | BindingFlags.NonPublic);
            var bundles = bundleField != null
                ? bundleField.GetValue(manager) as Dictionary<string, AssetBundle> : null;
            if (bundles != null)
            {
                foreach (var loaded in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (loaded != null && loaded.name.StartsWith("bundle", StringComparison.Ordinal)
                        && !bundles.ContainsKey(loaded.name))
                    {
                        bundles.Add(loaded.name, loaded);
                        LayoutEditorLog.Log("[pseudo-reload] Adopt loaded bundle: " + loaded.name);
                    }
                }
            }
            if (!ManagerHasManifest(manager) && !LayoutEditorDispenserIconFix.PreloadAndSeed(manager))
            {
                LayoutEditorLog.Log("[dispenser-icon] SafeReinit: 预载不可用，退回仅对已加载 bundle 补种子");
                LayoutEditorDispenserIconFix.SeedSceneDispenserPrefabs();
            }
            else if (ManagerHasManifest(manager))
                LayoutEditorLog.Log("[preview-init] Existing manifest/bundle set; skip duplicate preload");
            HealSpecialDispenserRuntimesInScene();
            // Local cloned nodes can retain atlas references from bundles unloaded by DeInit.
            foreach (var dispenser in UnityEngine.Object.FindObjectsOfType<PseudoPrefabDispenserStub>())
            {
                if (dispenser.spawnerItemPrefabSO == null)
                    continue;
                var node = RecipeHelper.GetIngredientOrderNode(dispenser.spawnerItemPrefabSO);
                if (node == null || node.m_crateLid == null || node.m_crateLid.m_atlasTexture != null)
                    continue;
                foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
                {
                    foreach (var original in bundle.LoadAllAssets<IngredientOrderNode>())
                    {
                        if (original.name == node.name && original.m_crateLid != null
                            && original.m_crateLid.m_atlasTexture != null)
                        {
                            node.m_crateLid = original.m_crateLid.DeepClone();
                            LayoutEditorLog.Log("[pseudo-reload] Restored ingredient atlas: " + node.name);
                            break;
                        }
                    }
                    if (node.m_crateLid.m_atlasTexture != null)
                        break;
                }
            }
            manager.Init();
            var previews = UnityEngine.Object.FindObjectsOfType<PseudoPrefab>();
            int present = previews.Count(p => p.childGameObject != null);
            Debug.Log("[preview-init] " + manager.gameObject.scene.path + " children: total="
                + previews.Length + " present=" + present + " missing=" + (previews.Length - present));
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
        finally
        {
            CallingOriginalInit = previousCallingOriginal;
            if (filteredDependencies && info != null)
            {
                info.dependencies = persistentDependencies;
                LayoutEditorLog.Log("[pseudo-reload] Restored dependencies: "
                    + string.Join(", ", info.dependencies));
            }
        }
        LayoutEditorDispenserIconFix.SyncSeededIcons();
        LayoutEditorLog.Log("[dispenser-icon] SafeReinit: end");
        LayoutEditorGridSnapGuard.RelaxGridSnapOnScene();
    }

    private static string GetLevelSetName(LevelInfoSO info)
    {
        string path = AssetDatabase.GetAssetPath(info);
        string[] parts = (path ?? "").Replace('\\', '/').Split('/');
        return parts.Length > 2 && parts[0] == "Assets" && parts[1] == "LevelSets"
            ? parts[2]
            : "";
    }

    private static bool ManagerHasManifest(PseudoPrefabManager manager)
    {
        var field = typeof(PseudoPrefabManager).GetField("assetBundleManifest",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null && field.GetValue(manager) != null;
    }

    private static bool CanPreviewWithoutCommon04(PseudoPrefabManager manager, LevelInfoSO info)
    {
        var roots = new List<UnityEngine.Object>();
        roots.Add(info);
        // OnEnable runs before Scene.isLoaded, when GetRootGameObjects throws.
        var scene = manager.gameObject.scene;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.scene == scene && go.transform.parent == null)
                roots.Add(go);
        // Include disk references too: restoration may not have created every root yet.
        if (!string.IsNullOrEmpty(scene.path))
        foreach (var path in AssetDatabase.GetDependencies(scene.path, true))
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != null)
                roots.Add(asset);
        }
        var dependencies = EditorUtility.CollectDependencies(roots.ToArray());
        int inspected = 0;
        bool safe = true;
        foreach (var dependency in dependencies)
        {
            var so = dependency as PseudoPrefabSO;
            if (so == null)
                continue;
            string path = AssetDatabase.GetAssetPath(so);
            if (!path.StartsWith("Assets/common04/", StringComparison.Ordinal)
                && so.bundleName != "common04")
                continue;
            inspected++;
            LayoutEditorLog.Log("[pseudo-reload] common04 SO: " + path + " name=" + so.name
                + " direct=" + (so.asset != null) + " bundle=" + so.bundleName);
            if (so.bundleName == "common04")
                safe = false;
        }
        LayoutEditorLog.Log("[pseudo-reload] common04 audit: inspected=" + inspected
            + " requiresBundle=" + !safe);
        return safe;
    }

    private static bool IsUnbuiltLocalCustomDependency(string setName, string dependency)
    {
        if (string.IsNullOrEmpty(dependency))
            return false;
        string prefix = setName + "/";
        if (!dependency.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        string bundleName = dependency.Substring(prefix.Length);
        if (bundleName != "custom_content" && bundleName != "custom_recipes")
            return false;
        string bundlePath = Path.Combine(Application.streamingAssetsPath, "Windows/" + dependency)
            .Replace('\\', '/');
        return !File.Exists(bundlePath)
            && AssetDatabase.IsValidFolder("Assets/LevelSets/" + setName + "/" + bundleName);
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
        if (!LayoutEditorOptionalBundlePreflight.NeedsDedicatedBundle(parts[2], "custom_recipes"))
            return; // An old product is not proof that source content still exists.
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
        if (Application.isPlaying || LayoutEditorPseudoReload.ExportBuildInProgress)
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
        if (LayoutEditorSwitchMaterialRepair.SanitizeAllInScene() > 0 && activeScene.IsValid())
            EditorSceneManager.MarkSceneDirty(activeScene);
        LayoutEditorStubIO.RepairFullCleanPlateStackRuntimes();
        if (activeScene.IsValid() && !string.IsNullOrEmpty(activeScene.path))
        {
            var levelInfo = LayoutEditorLevelInfoResolver.ResolveForScene(activeScene.path);
            if (levelInfo != null)
                LayoutEditorSteamerFill.SyncSteamerIngredients(levelInfo);
            LayoutEditorPancakeFill.SyncForScene(activeScene, levelInfo);
        }
        LayoutEditorPseudoReload.RepairMissingPseudoMaterials();
        LayoutEditorPseudoReload.RepairMissingSwitchChildren();
        LayoutEditorPseudoReload.RepairBrokenPressureSwitchChildren();
        LayoutEditorStubIO.ApplySwitchStartVisualsInActiveScene();
    }
}
