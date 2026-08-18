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
/// 背景：common_w 厨具变体 wrapper（火锅大锅/烤盘/DLC 锅具）写回时由
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
                var stub = _stubField != null ? _stubField.GetValue(runtime) as PseudoPrefabStub : null;
                if (stub is PseudoPrefabCookingUtensilStub)
                    continue;
                var go = runtime.gameObject;
                var fixedStub = go.GetComponent<PseudoPrefabCookingUtensilStub>();
                if (fixedStub == null)
                {
                    // wrapper 丢 stub：从 SO 重建（SO 优先取基础 stub 的，其次场景残留）
                    PseudoPrefabSO so = null;
                    var baseStub = go.GetComponent<PseudoPrefabStub>();
                    if (baseStub != null && baseStub.pseudoPrefabSO != null)
                        so = baseStub.pseudoPrefabSO;
                    fixedStub = Undo.AddComponent<PseudoPrefabCookingUtensilStub>(go);
                    if (so != null) fixedStub.pseudoPrefabSO = so;
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
        if (dirty)
        {
            EditorSceneManager.MarkAllScenesDirty();
            _armed = false;
            _hasTick = false;
            EditorApplication.update -= Tick;
        }
    }
}
