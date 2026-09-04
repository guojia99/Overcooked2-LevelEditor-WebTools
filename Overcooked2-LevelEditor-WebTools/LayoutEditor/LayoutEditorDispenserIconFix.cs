using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using LevelEditor;
using LevelEditorStub;

/// <summary>
/// 食材箱「木纹·中秋」等新结构皮肤的箱盖图标兼容补丁（编辑器侧，宿主文件不动）。
///
/// 背景（bundle 实测）：
/// - 基础食材箱（bundle47 dispenser_crate_01）盖子网格 m_sk_crate_01 是 SkinnedMeshRenderer；
/// - 木纹·中秋外观（DispenserWood13SO → bundle449 dlc13_dispenser_crate_camping_new）
///   的盖子 m_sk_crate_01_Image 是普通 MeshRenderer，且真实 prefab 根上没有任何渲染器。
/// 宿主 PseudoPrefabDispenser.Setup 的图标逻辑是：
///   盖子子物体 GetComponent&lt;SkinnedMeshRenderer&gt;() → 为空再查【根】GetComponent&lt;MeshRenderer&gt;()
/// 木纹·中秋两者皆空 → MissingComponentException 直接打断 PseudoPrefabManager.Init()，
/// 写回后其余道具全部停在半初始化状态（连带 ClientOvenCosmeticDecisions OnDestroy 的
/// Animator 噪音）。游戏运行时（ClientItemCrateCosmeticDecisions）两种渲染器都会查，无此问题。
///
/// 对策：在宿主 Init 使用真实 prefab 之前，给这类 prefab 的根补一个 MeshRenderer
/// （材质复制自盖子渲染器），宿主图标逻辑照常走通并把图标材质写到根渲染器上；
/// Init 之后再把这个图标材质数组同步回真正的盖子渲染器（编辑器内同样显示食材图标）。
/// 种子写在 bundle 已加载的缓存资产上：同一次会话内每次 Instantiate 都生效；
/// 场景里保存的实例虽带种子渲染器，游戏运行时 Init 会 ClearChild 后从原始 bundle
/// 重建（且游戏侧图标代码本就正确），不会泄漏到成品。
///
/// 全链路诊断日志写入 logs/layout_editor.log（LayoutEditorLog），标签 [dispenser-icon]。
/// </summary>
public static class LayoutEditorDispenserIconFix
{
    private const string Tag = "[dispenser-icon] ";

    /// <summary>图标同步完成后的扩展钩子（SyncSeededIcons 末尾调用；SafeReinit /
    ///  ReloadPseudoAssets / 自愈守卫等全部初始化路径都汇聚于此）。解耦：无订阅者
    /// 行为不变——CustomStubAutoBake 订阅它来同步随机食材箱的问号箱盖。</summary>
    public static Action AfterRandomCrateSync;

    private static FieldInfo _bundleDictField;

    private static FieldInfo BundleDictField
    {
        get
        {
            if (_bundleDictField == null)
                _bundleDictField = typeof(PseudoPrefabManager).GetField("bundleDict",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            return _bundleDictField;
        }
    }

    /// <summary>DeInit 之后、Init 之前调用：场景有食材箱时预载依赖 bundle 并注入
    ///  manager 的 bundleDict（宿主 EnsureLoadAllAssetBundles 命中缓存即不再重复加载），
    ///  再给新结构食材箱的真实 prefab 补种子渲染器，使 Init 一次走通。
    ///  返回 false 表示预载不可用（调用方退回：Init 后由重试/自愈守卫兜底）。</summary>
    public static bool PreloadAndSeed(PseudoPrefabManager manager)
    {
        if (manager == null)
        {
            LayoutEditorLog.Log(Tag + "PreloadAndSeed: manager == null");
            return false;
        }
        var stubs = UnityEngine.Object.FindObjectsOfType<PseudoPrefabDispenserStub>();
        if (stubs == null || stubs.Length == 0)
        {
            LayoutEditorLog.Log(Tag + "PreloadAndSeed: 场景没有食材箱 stub，跳过预载");
            return false;
        }
        LogSceneDispensers(stubs, "PreloadAndSeed");
        var info = manager.stub != null ? manager.stub.levelInfo : null;
        if (info == null || info.dependencies == null || info.dependencies.Length == 0)
        {
            LayoutEditorLog.LogWarning(Tag + "PreloadAndSeed: levelInfo/dependencies 为空，无法预载");
            return false;
        }
        var field = BundleDictField;
        if (field == null)
        {
            LayoutEditorLog.LogWarning(Tag + "PreloadAndSeed: 反射拿不到 PseudoPrefabManager.bundleDict 字段");
            return false;
        }
        var dict = field.GetValue(manager) as Dictionary<string, AssetBundle>;
        if (dict == null)
        {
            LayoutEditorLog.LogWarning(Tag + "PreloadAndSeed: bundleDict 实例为 null");
            return false;
        }
        try
        {
            LayoutEditorLog.Log(Tag + "PreloadAndSeed: 预载 Windows manifest");
            var manifestBundle = AssetBundle.LoadFromFile(BundlePath("Windows"));
            if (manifestBundle == null)
            {
                LayoutEditorLog.LogWarning(Tag + "PreloadAndSeed: Windows manifest bundle 加载失败: " + BundlePath("Windows"));
                return false;
            }
            var manifest = manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            if (manifest == null)
            {
                LayoutEditorLog.LogWarning(Tag + "PreloadAndSeed: AssetBundleManifest 读取失败");
                return false;
            }
            if (!dict.ContainsKey("Windows"))
                dict.Add("Windows", manifestBundle);
            var queue = new List<string>(info.dependencies);
            LayoutEditorLog.Log(Tag + "PreloadAndSeed: levelInfo.dependencies = [" +
                string.Join(", ", info.dependencies) + "]");
            for (int i = 0; i < queue.Count; i++)
            {
                var name = queue[i];
                if (string.IsNullOrEmpty(name) || dict.ContainsKey(name))
                    continue;
                var bundle = AssetBundle.LoadFromFile(BundlePath(name));
                if (bundle == null)
                {
                    LayoutEditorLog.LogWarning(Tag + "PreloadAndSeed: bundle 加载失败: " + name +
                        "（" + BundlePath(name) + "）");
                    return false;
                }
                dict.Add(name, bundle);
                LayoutEditorLog.Log(Tag + "PreloadAndSeed: 已注入 bundle " + name);
                var deps = manifest.GetAllDependencies(name);
                for (int d = 0; d < deps.Length; d++)
                {
                    if (!dict.ContainsKey(deps[d]) && !queue.Contains(deps[d]))
                        queue.Add(deps[d]);
                }
            }
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning(Tag + "PreloadAndSeed 异常: " + ex);
            return false;
        }
        bool seeded = SeedSceneDispenserPrefabs();
        LayoutEditorLog.Log(Tag + "PreloadAndSeed 完成，本次补种子 " + (seeded ? "≥1 个" : "0 个（无需或失败，见上）"));
        return true;
    }

    /// <summary>给场景里所有食材箱 stub 指向的真实 prefab 补种子渲染器（bundle 已加载时）。
    ///  返回是否至少补了一个。</summary>
    public static bool SeedSceneDispenserPrefabs()
    {
        bool any = false;
        var stubs = UnityEngine.Object.FindObjectsOfType<PseudoPrefabDispenserStub>();
        if (stubs == null)
            return false;
        for (int i = 0; i < stubs.Length; i++)
        {
            var s = stubs[i];
            if (s == null)
                continue;
            try
            {
                if (SeedPrefab(s.pseudoPrefabSO))
                    any = true;
            }
            catch (Exception ex)
            {
                LayoutEditorLog.LogWarning(Tag + "SeedSceneDispenserPrefabs: " +
                    (s != null && s.pseudoPrefabSO != null ? s.pseudoPrefabSO.name : "?") + " 异常: " + ex.Message);
            }
        }
        return any;
    }

    /// <summary>manager 当前是否已加载该 SO 的 bundle 且其真实 prefab 仍需补种子
    ///  （自愈守卫的重触发判据；幂等，补过即返回 false）。</summary>
    public static bool PrefabNeedsSeedViaManager(PseudoPrefabSO so)
    {
        if (so == null)
            return false;
        AssetBundle bundle;
        try
        {
            bundle = PseudoPrefabManager.GetAssetBundle(so.bundleName);
        }
        catch
        {
            return false; // bundle 未加载：无法判定
        }
        if (bundle == null)
            return false;
        var prefab = bundle.LoadAsset<GameObject>(so.assetPath);
        string reason;
        return NeedsSeed(prefab, out reason);
    }

    /// <summary>Init 完成后调用：把宿主写到（种子）根渲染器上的图标材质数组同步回
    ///  真正的盖子渲染器，编辑器内也能看到食材图标。幂等。</summary>
    public static void SyncSeededIcons()
    {
        var dispensers = UnityEngine.Object.FindObjectsOfType<PseudoPrefabDispenser>();
        if (dispensers == null)
            return;
        int synced = 0, seededSeen = 0;
        for (int i = 0; i < dispensers.Length; i++)
        {
            try
            {
                var d = dispensers[i];
                if (d == null)
                    continue;
                var child = d.childGameObject;
                if (child == null)
                {
                    LayoutEditorLog.Log(Tag + "Sync: " + SafeName(d) + " childGameObject == null（跳过）");
                    continue;
                }
                // 种子根渲染器：有 MeshRenderer 且根上无 MeshFilter（原生根渲染器都带 filter）
                var root = child.GetComponent<MeshRenderer>();
                if (root == null || child.GetComponent<MeshFilter>() != null)
                    continue;
                seededSeen++;
                var cosmetic = child.GetComponent<ItemCrateCosmeticDecisions>();
                if (cosmetic == null || string.IsNullOrEmpty(cosmetic.m_crateLidMeshName))
                {
                    LayoutEditorLog.LogWarning(Tag + "Sync: " + child.name + " 有种子渲染器但无 ItemCrateCosmeticDecisions/lid 名");
                    continue;
                }
                var lid = child.transform.FindChildRecursive(cosmetic.m_crateLidMeshName);
                if (lid == null)
                {
                    LayoutEditorLog.LogWarning(Tag + "Sync: " + child.name + " 找不到盖子物体 " + cosmetic.m_crateLidMeshName);
                    continue;
                }
                var lidRenderer = lid.GetComponent<MeshRenderer>();
                if (lidRenderer == null)
                {
                    LayoutEditorLog.LogWarning(Tag + "Sync: " + child.name + " 盖子 " + cosmetic.m_crateLidMeshName + " 无 MeshRenderer");
                    continue;
                }
                lidRenderer.sharedMaterials = root.sharedMaterials;
                synced++;
                LayoutEditorLog.Log(Tag + "Sync: " + child.name + " 图标材质已同步到盖子 " +
                    cosmetic.m_crateLidMeshName + "（materials=" + (root.sharedMaterials != null ? root.sharedMaterials.Length : 0) + "）");
            }
            catch (Exception ex)
            {
                LayoutEditorLog.LogWarning(Tag + "Sync 异常: " + ex.Message);
            }
        }
        if (seededSeen > 0)
            LayoutEditorLog.Log(Tag + "Sync 完成: 种子箱 " + seededSeen + " 个，同步成功 " + synced + " 个");
        if (AfterRandomCrateSync != null)
        {
            try
            {
                AfterRandomCrateSync();
            }
            catch (Exception ex)
            {
                LayoutEditorLog.LogWarning(Tag + "AfterRandomCrateSync 扩展钩子异常: " + ex.Message);
            }
        }
    }

    private static bool SeedPrefab(PseudoPrefabSO so)
    {
        if (so == null || string.IsNullOrEmpty(so.assetPath))
        {
            LayoutEditorLog.Log(Tag + "Seed: stub 的 pseudoPrefabSO 为空（跳过）");
            return false;
        }
        AssetBundle bundle;
        try
        {
            bundle = PseudoPrefabManager.GetAssetBundle(so.bundleName);
        }
        catch
        {
            LayoutEditorLog.Log(Tag + "Seed: " + so.name + " 的 bundle 未加载（" + so.bundleName + "），本次不补种子");
            return false; // bundle 未加载
        }
        if (bundle == null)
        {
            LayoutEditorLog.Log(Tag + "Seed: " + so.name + " 的 bundle 为 null（" + so.bundleName + "）");
            return false;
        }
        var prefab = bundle.LoadAsset<GameObject>(so.assetPath);
        string reason;
        if (!NeedsSeed(prefab, out reason))
        {
            LayoutEditorLog.Log(Tag + "Seed: " + so.name + "（" + so.bundleName + "）无需种子: " + reason);
            return false;
        }
        var cosmetic = prefab.GetComponent<ItemCrateCosmeticDecisions>();
        var lid = prefab.transform.FindChildRecursive(cosmetic.m_crateLidMeshName);
        var seeded = prefab.AddComponent<MeshRenderer>();
        seeded.sharedMaterials = lid.GetComponent<Renderer>().sharedMaterials;
        LayoutEditorLog.Log(Tag + "Seed: " + prefab.name + "（" + so.bundleName + "）已补种子: 根加 MeshRenderer，材质取自 " +
            cosmetic.m_crateLidMeshName + "（materials=" + (seeded.sharedMaterials != null ? seeded.sharedMaterials.Length : 0) +
            ", m_materialNumber=" + cosmetic.m_materialNumber + "）");
        return true;
    }

    /// <summary>真实 prefab 是否为「宿主图标逻辑查不到渲染器」的新结构：
    ///  根无 MeshRenderer、非背包箱、盖子子物体存在且是普通 Renderer（非蒙皮）、
    ///  材质下标有效。判定失败一律返回 false（不种子、不干预），reason 记录原因。</summary>
    private static bool NeedsSeed(GameObject prefab, out string reason)
    {
        reason = "";
        if (prefab == null)
        {
            reason = "prefab 未加载到";
            return false;
        }
        if (prefab.GetComponent<MeshRenderer>() != null)
        {
            reason = "根已有 MeshRenderer";
            return false;
        }
        if (prefab.GetComponent<Backpack>() != null)
        {
            reason = "背包箱（宿主 Setup 提前 return，无图标逻辑）";
            return false;
        }
        var cosmetic = prefab.GetComponent<ItemCrateCosmeticDecisions>();
        if (cosmetic == null || string.IsNullOrEmpty(cosmetic.m_crateLidMeshName))
        {
            reason = "无 ItemCrateCosmeticDecisions/lid 名";
            return false;
        }
        var lid = prefab.transform.FindChildRecursive(cosmetic.m_crateLidMeshName);
        if (lid == null)
        {
            reason = "找不到盖子物体 " + cosmetic.m_crateLidMeshName;
            return false;
        }
        var lidRenderer = lid.GetComponent<Renderer>();
        if (lidRenderer == null || lidRenderer is SkinnedMeshRenderer)
        {
            reason = "盖子渲染器为 " + (lidRenderer == null ? "null" : lidRenderer.GetType().Name) + "（宿主原逻辑可处理）";
            return false;
        }
        var materials = lidRenderer.sharedMaterials;
        if (materials == null || cosmetic.m_materialNumber >= materials.Length)
        {
            reason = "盖子材质数组无效（" + (materials != null ? materials.Length.ToString() : "null") +
                " ≤ m_materialNumber=" + cosmetic.m_materialNumber + "）";
            return false;
        }
        reason = "新结构（盖子为普通 MeshRenderer 且根无渲染器）";
        return true;
    }

    /// <summary>列出场景食材箱的配置（go 名 / SO / spawner SO），供日志排查。</summary>
    private static void LogSceneDispensers(PseudoPrefabDispenserStub[] stubs, string phase)
    {
        var sb = new StringBuilder();
        sb.Append(phase).Append(": 场景食材箱 ").Append(stubs.Length).Append(" 个:");
        for (int i = 0; i < stubs.Length && i < 10; i++)
        {
            var s = stubs[i];
            if (s == null)
                continue;
            var so = s.pseudoPrefabSO;
            var dso = s.spawnerItemPrefabSO;
            sb.Append(" [").Append(s.name)
              .Append(" SO=").Append(so != null ? so.name + "@" + so.bundleName : "<null>")
              .Append(" spawner=").Append(dso != null ? dso.name : "<null>")
              .Append("]");
        }
        LayoutEditorLog.Log(Tag + sb.ToString());
    }

    private static string SafeName(PseudoPrefabDispenser d)
    {
        try
        {
            return d != null && d.gameObject != null ? d.gameObject.name : "<destroyed>";
        }
        catch (Exception)
        {
            return "<destroyed>";
        }
    }

    private static string BundlePath(string bundleName)
    {
        return Path.Combine(Application.streamingAssetsPath, "Windows/" + bundleName).Replace("\\", "/");
    }
}

/// <summary>
/// 新结构食材箱自愈守卫：场景加载/进入 Play 后，若 manager 的 bundle 已加载而真实
/// prefab 仍缺种子（说明宿主 Init 曾在图标逻辑处被打断，或按原样 Init 必被打断），
/// 补种子后重跑一次 manager.Init() 并同步图标材质。短程轮询 + 次数上限，防循环。
/// （编辑期写回的主路径由 LayoutEditorPseudoReload 的预载/重试覆盖，这里兜底
///  「直接打开已保存场景」「进入 Play」两条宿主自跑 Init 的路径。）
/// </summary>
[InitializeOnLoad]
static class LayoutEditorDispenserIconHeal
{
    private const int MaxAttempts = 2;
    private static bool _armed;
    private static double _deadline;
    private static int _attempts;
    private static bool _warned;

    static LayoutEditorDispenserIconHeal()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += OnLoaded;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode ||
            state == PlayModeStateChange.EnteredPlayMode ||
            state == PlayModeStateChange.EnteredEditMode)
        {
            LayoutEditorLog.Log("[dispenser-icon] heal: arm on " + state);
            Arm();
        }
    }

    /// <summary>域重载后一次性武装（场景加载/Play 进入的 Init 就在此刻附近跑）。</summary>
    private static void OnLoaded()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        EditorApplication.update -= OnLoaded;
        LayoutEditorLog.Log("[dispenser-icon] heal: arm after domain reload");
        Arm();
    }

    private static void Arm()
    {
        _armed = true;
        _attempts = 0;
        _deadline = EditorApplication.timeSinceStartup + 15.0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!_armed)
        {
            EditorApplication.update -= Tick;
            return;
        }
        if (EditorApplication.timeSinceStartup > _deadline)
        {
            Disarm();
            return;
        }
        try
        {
            var manager = PseudoPrefabManager.Instance;
            if (manager == null)
                return;
            if (!AnyStubNeedsSeed())
            {
                Disarm();
                return;
            }
            if (_attempts >= MaxAttempts)
            {
                if (!_warned)
                {
                    _warned = true;
                    LayoutEditorLog.LogWarning("[dispenser-icon] heal: 补种子后仍检测到" +
                        "新结构食材箱，放弃本轮自愈（详见此前 re-init 警告）");
                }
                Disarm();
                return;
            }
            _attempts++;
            LayoutEditorLog.Log("[dispenser-icon] heal: 检测到缺种子的食材箱，第 " + _attempts + " 次补种子 + re-init");
            LayoutEditorDispenserIconFix.SeedSceneDispenserPrefabs();
            try
            {
                manager.Init();
                LayoutEditorDispenserIconFix.SyncSeededIcons();
                LayoutEditorLog.Log("[dispenser-icon] heal: re-init ok（第 " + _attempts + " 次）");
            }
            catch (Exception ex)
            {
                LayoutEditorLog.LogWarning("[dispenser-icon] heal re-init failed: " + ex);
            }
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[dispenser-icon] heal tick 异常: " + ex.Message);
        }
    }

    private static bool AnyStubNeedsSeed()
    {
        var stubs = UnityEngine.Object.FindObjectsOfType<PseudoPrefabDispenserStub>();
        if (stubs == null)
            return false;
        for (int i = 0; i < stubs.Length; i++)
        {
            var s = stubs[i];
            if (s == null)
                continue;
            try
            {
                if (LayoutEditorDispenserIconFix.PrefabNeedsSeedViaManager(s.pseudoPrefabSO))
                    return true;
            }
            catch (Exception)
            {
            }
        }
        return false;
    }

    private static void Disarm()
    {
        _armed = false;
        EditorApplication.update -= Tick;
    }
}
