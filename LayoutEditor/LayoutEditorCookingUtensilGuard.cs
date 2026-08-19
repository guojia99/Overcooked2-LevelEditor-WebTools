using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LevelEditor;
using LevelEditorStub;

/// <summary>
/// PseudoPrefabCookingUtensil stub 字段自愈守卫。
///
/// 背景：common03 厨具变体 wrapper（火锅大锅/烤盘/DLC 锅具）写回时由
/// LayoutEditorStubIO.ApplyStub 补挂「派生 stub + 派生运行时组件」。宿主
/// PseudoPrefab 的 protected stub 字段只在 Awake 里赋值（GetComponent 取
/// 第一个 PseudoPrefabStub）——组件增删/域重载/场景加载的时序边角下
/// （如派生 stub 尚未反序列化完成、或双 stub 竞态）该字段可能为 null 或
/// 指向错误类型，导致 Setup() 第 15 行 cast 得 null、第 18 行
/// `cookingUtensilStub.capacity` 抛 NullReferenceException。
///
/// 处理：编辑器 update 低频轮询（写回后 ~15 秒内 + 每次场景加载后），
/// 对场景里所有 PseudoPrefabCookingUtensil：
///   - 反射读取 stub 字段；为 null 或类型不符时重取/补挂正确的
///     PseudoPrefabCookingUtensilStub（伪证物丢失时从 wrapper 的基础
///     stub / SO 重建），消除 NRE 源头。
/// 宿主文件一律不动。
/// </summary>
[InitializeOnLoad]
static class LayoutEditorCookingUtensilGuard
{
    private static bool _armed;
    private static double _deadline;
    private static FieldInfo _stubField;

    static LayoutEditorCookingUtensilGuard()
    {
        _stubField = typeof(PseudoPrefab).GetField("stub",
            BindingFlags.Instance | BindingFlags.NonPublic);
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += OnLoaded;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode ||
            state == PlayModeStateChange.EnteredEditMode)
        {
            Arm();
        }
    }

    /// <summary>场景加载/域重载后武装一次短程轮询（Start 竞态窗口就在此刻）。</summary>
    private static void OnLoaded()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        EditorApplication.update -= OnLoaded;
        Arm();
    }

    /// <summary>真实 prefab 是否为锅具容器（有 IngredientContainer）。
    ///  判定失败（bundle 未加载/资产缺失/加载异常）一律放行返回 true，
    ///  仅在**确实加载到真实 prefab 且无 IngredientContainer** 时返回 false，
    ///  用于防御宿主 PseudoPrefabCookingUtensil.Setup 对非容器道具 NRE。</summary>
    public static bool RealPrefabHasIngredientContainer(PseudoPrefabSO so)
    {
        if (so == null)
            return true;
        try
        {
            AssetBundle bundle;
            try
            {
                bundle = PseudoPrefabManager.GetAssetBundle(so.bundleName);
            }
            catch
            {
                return true; // bundle 未加载：无法判定，放行
            }
            if (bundle == null)
                return true;
            var prefab = bundle.LoadAsset<GameObject>(so.assetPath);
            if (prefab == null)
                return true; // 加载失败：无法判定，放行
            return prefab.GetComponent("IngredientContainer") != null;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>食材真实 prefab 是否可作为食材箱产出（实现 ISpawnableItem）。
    ///  判定失败一律放行返回 true，仅在确实加载到 prefab 且无任何产出组件时返回 false
    ///  （如 TurkeySO 原始火鸡，无 WorkableItem/IngredientPropertiesComponent/容器组件，
    ///  宿主 PseudoPrefabDispenser.Setup 的 RequireInterface&lt;ISpawnableItem&gt; 得 null → NRE）。</summary>
    public static bool RealPrefabIsSpawnableIngredient(PseudoPrefabSO so)
    {
        if (so == null)
            return false;
        try
        {
            AssetBundle bundle;
            try
            {
                bundle = PseudoPrefabManager.GetAssetBundle(so.bundleName);
            }
            catch
            {
                return true;
            }
            if (bundle == null)
                return true;
            var prefab = bundle.LoadAsset<GameObject>(so.assetPath);
            if (prefab == null)
                return true;
            return prefab.GetComponent("WorkableItem") != null
                || prefab.GetComponent("IngredientPropertiesComponent") != null
                || prefab.GetComponent("IngredientContainer") != null
                || prefab.GetComponent("Plate") != null
                || prefab.GetComponent("Glass") != null;
        }
        catch
        {
            return true;
        }
    }

    private static void Arm()
    {
        _armed = true;
        _deadline = EditorApplication.timeSinceStartup + 15.0;
        if (!_hasTick)
        {
            _hasTick = true;
            EditorApplication.update += Tick;
        }
    }

    private static bool _hasTick;

    private static void Tick()
    {
        if (!_armed || EditorApplication.timeSinceStartup > _deadline)
        {
            _armed = false;
            _hasTick = false;
            EditorApplication.update -= Tick;
            return;
        }

        bool dirty = false;
        foreach (var runtime in UnityEngine.Object.FindObjectsOfType<PseudoPrefabCookingUtensil>())
        {
            try
            {
                var go = runtime.gameObject;
                var stub = _stubField != null ? _stubField.GetValue(runtime) as PseudoPrefabStub : null;

                // ① 降级判定：真实 prefab 不是锅具容器（无 IngredientContainer，如
                //   喷雾/搅拌工作站被误配成锅具）→ 宿主 Setup 会 NRE，降级为普通道具。
                PseudoPrefabSO so = null;
                var existingCuStub = go.GetComponent<PseudoPrefabCookingUtensilStub>();
                if (existingCuStub != null && existingCuStub.pseudoPrefabSO != null)
                    so = existingCuStub.pseudoPrefabSO;
                else if (stub != null)
                    so = stub.pseudoPrefabSO;
                if (so != null && !RealPrefabHasIngredientContainer(so))
                {
                    DowngradeToBase(go, so);
                    dirty = true;
                    continue;
                }

                if (stub is PseudoPrefabCookingUtensilStub)
                    continue;
                var fixedStub = go.GetComponent<PseudoPrefabCookingUtensilStub>();
                if (fixedStub == null)
                {
                    // wrapper 丢 stub：从 SO 重建（SO 优先取基础 stub 的，其次场景残留）
                    PseudoPrefabSO baseSo = null;
                    var baseStub = go.GetComponent<PseudoPrefabStub>();
                    if (baseStub != null && baseStub.pseudoPrefabSO != null)
                        baseSo = baseStub.pseudoPrefabSO;
                    fixedStub = Undo.AddComponent<PseudoPrefabCookingUtensilStub>(go);
                    if (baseSo != null) fixedStub.pseudoPrefabSO = baseSo;
                    if (fixedStub.capacity <= 0)
                        fixedStub.capacity = LayoutEditorStubIO.NativeUtensilCapacityForId(go.name);
                    LayoutEditorLog.LogWarning(
                        "[LayoutEditor] utensil guard: 补挂缺失的 CookingUtensilStub: " + go.name);
                    dirty = true;
                }
                if (_stubField != null)
                {
                    _stubField.SetValue(runtime, fixedStub);
                    dirty = true;
                    LayoutEditorLog.LogWarning(
                        "[LayoutEditor] utensil guard: 已修正 " + go.name +
                        " 的 stub 引用（原为 " + (stub == null ? "null" : stub.GetType().Name) + "）");
                }
            }
            catch (Exception)
            {
                // 对象在场景切换中被销毁：跳过，下一轮终止条件兜底
            }
        }

        // 终端：无 pilotableObject 时宿主 PseudoPrefabTerminal.Setup 抛 UnassignedReferenceException。
        foreach (var runtime in UnityEngine.Object.FindObjectsOfType<PseudoPrefabTerminal>())
        {
            try
            {
                var go = runtime.gameObject;
                var stub = go.GetComponent<PseudoPrefabTerminalStub>();
                if (stub == null || stub.pilotableObject == null)
                {
                    DowngradeToBase(go, stub != null ? stub.pseudoPrefabSO : null);
                    dirty = true;
                }
            }
            catch (Exception)
            {
            }
        }

        // 传送门：无出口时宿主 PseudoPrefabTeleportal.LateSetup 对 null exitPortal 抛 NRE。
        foreach (var runtime in UnityEngine.Object.FindObjectsOfType<PseudoPrefabTeleportal>())
        {
            try
            {
                var go = runtime.gameObject;
                var stub = go.GetComponent<PseudoPrefabTeleportalStub>();
                if (stub == null || stub.exitPortal == null)
                {
                    DowngradeToBase(go, stub != null ? stub.pseudoPrefabSO : null);
                    dirty = true;
                }
            }
            catch (Exception)
            {
            }
        }

        // 食材箱：无生成食材或食材不可产出时宿主 PseudoPrefabDispenser.Setup 会 NRE。
        foreach (var runtime in UnityEngine.Object.FindObjectsOfType<PseudoPrefabDispenser>())
        {
            try
            {
                var go = runtime.gameObject;
                var stub = go.GetComponent<PseudoPrefabDispenserStub>();
                if (stub == null || stub.spawnerItemPrefabSO == null ||
                    !RealPrefabIsSpawnableIngredient(stub.spawnerItemPrefabSO))
                {
                    DowngradeToBase(go, stub != null ? stub.pseudoPrefabSO : null);
                    dirty = true;
                }
            }
            catch (Exception)
            {
            }
        }

        // 容器堆：盘子 SO 缺失或真实 prefab 无 EditorGridSnap 时
        // 宿主 PseudoPrefabCleanPlateStack.Setup 抛 NRE。
        foreach (var runtime in UnityEngine.Object.FindObjectsOfType<PseudoPrefabCleanPlateStack>())
        {
            try
            {
                var go = runtime.gameObject;
                var stub = go.GetComponent<PseudoPrefabCleanPlateStackStub>();
                if (stub == null || stub.platePseudoPrefabSO == null ||
                    !PlatePrefabHasGridSnap(stub.platePseudoPrefabSO))
                {
                    DowngradeToBase(go, stub != null ? stub.pseudoPrefabSO : null);
                    dirty = true;
                }
            }
            catch (Exception)
            {
            }
        }

        if (dirty)
        {
            EditorSceneManager.MarkAllScenesDirty();
            _armed = false;
            _hasTick = false;
            EditorApplication.update -= Tick;
        }
    }

    /// <summary>把被误配/残缺的派生道具降级为普通道具：移除派生 stub 与派生运行时，
    ///  恢复基础 PseudoPrefabStub + 基础 PseudoPrefab（Setup 空操作，不再 NRE）。</summary>
    public static void DowngradeToBase(GameObject go, PseudoPrefabSO so)
    {
        if (go == null)
            return;
        // 派生运行时组件（均为 PseudoPrefab 子类）
        if (go.GetComponent<PseudoPrefabCookingUtensil>() != null)
            Undo.DestroyObjectImmediate(go.GetComponent<PseudoPrefabCookingUtensil>());
        if (go.GetComponent<PseudoPrefabDispenser>() != null)
            Undo.DestroyObjectImmediate(go.GetComponent<PseudoPrefabDispenser>());
        if (go.GetComponent<PseudoPrefabTerminal>() != null)
            Undo.DestroyObjectImmediate(go.GetComponent<PseudoPrefabTerminal>());
        if (go.GetComponent<PseudoPrefabCleanPlateStack>() != null)
            Undo.DestroyObjectImmediate(go.GetComponent<PseudoPrefabCleanPlateStack>());
        if (go.GetComponent<PseudoPrefabTeleportal>() != null)
            Undo.DestroyObjectImmediate(go.GetComponent<PseudoPrefabTeleportal>());
        // 派生 stub（均为 PseudoPrefabStub 子类）
        if (go.GetComponent<PseudoPrefabCookingUtensilStub>() != null)
            Undo.DestroyObjectImmediate(go.GetComponent<PseudoPrefabCookingUtensilStub>());
        if (go.GetComponent<PseudoPrefabDispenserStub>() != null)
            Undo.DestroyObjectImmediate(go.GetComponent<PseudoPrefabDispenserStub>());
        if (go.GetComponent<PseudoPrefabTerminalStub>() != null)
            Undo.DestroyObjectImmediate(go.GetComponent<PseudoPrefabTerminalStub>());
        if (go.GetComponent<PseudoPrefabCleanPlateStackStub>() != null)
            Undo.DestroyObjectImmediate(go.GetComponent<PseudoPrefabCleanPlateStackStub>());
        if (go.GetComponent<PseudoPrefabTeleportalStub>() != null)
            Undo.DestroyObjectImmediate(go.GetComponent<PseudoPrefabTeleportalStub>());
        if (go.GetComponent<PseudoPrefabStub>() == null && so != null)
        {
            var baseStub = Undo.AddComponent<PseudoPrefabStub>(go);
            baseStub.pseudoPrefabSO = so;
        }
        if (go.GetComponent<PseudoPrefab>() == null)
            Undo.AddComponent<PseudoPrefab>(go);
        LayoutEditorLog.LogWarning(
            "[LayoutEditor] guard: 道具派生配置不完整/无效，已降级为普通道具: " + go.name +
            "（" + (so != null ? so.name : "?") + "）");
    }

    /// <summary>容器堆的盘子 prefab 是否带 EditorGridSnap（宿主 Setup 逐盘实例化时要求）。
    ///  判定失败一律放行返回 true，仅在确实加载到 prefab 且缺该组件时返回 false。</summary>
    public static bool PlatePrefabHasGridSnap(PseudoPrefabSO so)
    {
        if (so == null)
            return false;
        try
        {
            AssetBundle bundle;
            try
            {
                bundle = PseudoPrefabManager.GetAssetBundle(so.bundleName);
            }
            catch
            {
                return true;
            }
            if (bundle == null)
                return true;
            var prefab = bundle.LoadAsset<GameObject>(so.assetPath);
            if (prefab == null)
                return true;
            return prefab.GetComponent("EditorGridSnap") != null;
        }
        catch
        {
            return true;
        }
    }
}
