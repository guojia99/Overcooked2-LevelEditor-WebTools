using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LevelEditor;

/// <summary>
/// 半格摆放守卫：解除多格/偏轴道具的 EditorGridSnap X/Z 约束。
///
/// 背景：断头台（workstation_guillotine_01，2×1、pivot 在两格正中，counter 位于
/// ±0.53）与石炉台（workstation_furnace_01，DLC7 烧煤炉）的 bundle child 都带
/// EditorGridSnap（m_constrainX=m_constrainZ=1，实测 bundle297）。该组件在编辑模式
/// 每帧把 child 的 X/Z 吸到 1.2 格点（QuadGridManager 格点 = 格子中心），把断头台根
/// 拉回整格 → 两个 counter 落在格子边界上 → 左边格子拿不到。用户在 Inspector 手动
/// 取消 m_constrainX/Z 后可以摆到半格（根落 0.6 mod 1.2），但宿主 PseudoPrefab.
/// ResetChild() 每次重置都重新启用该组件（只放行 m_constrainY，不动 X/Z 字段），
/// 场景重载/域重载时 child 从 bundle 重建、字段恢复 1,1,1 → 又自动修正回去。
///
/// 处理：armed-tick 轮询（场景加载 + 进入编辑模式 + 写回后），对目标 prefab 的 child
/// EditorGridSnap 反射写 m_constrainX=m_constrainZ=false（仅当当前为 true 才写，
/// 避免无谓标脏）。解除后 child 停在 Web 端写入的半格位置，PseudoPrefab.Update 按
/// child 位置同步根节点，行为稳定。宿主文件一律不动。
/// </summary>
[InitializeOnLoad]
static class LayoutEditorGridSnapGuard
{
    /// <summary>需要半格自由摆放的道具（bundle child 的 prefabName）。</summary>
    private static readonly string[] TargetPrefabNames =
    {
        "workstation_guillotine_01",
        "workstation_furnace_01",
        // 火锅全家（灶台 + 静态/可移动大锅）：child 带 EditorGridSnap 会吸附整格
        // （1.2），与 web 摆放的半格（0.6）坐标互相拉扯，写回-读回循环中每次偏半格。
        "cooking_region_floorburner",
        "dlc10_cooking_region_floorburner",
        "pushable_object",
        "utensil_large_pot_01",
        "utensil_dlc10_large_pot_01",
    };

    private static readonly FieldInfo _constrainXField;
    private static readonly FieldInfo _constrainZField;

    private static bool _armed;
    private static double _deadline;
    private static bool _hasTick;

    static LayoutEditorGridSnapGuard()
    {
        _constrainXField = typeof(EditorGridSnap).GetField("m_constrainX",
            BindingFlags.Instance | BindingFlags.NonPublic);
        _constrainZField = typeof(EditorGridSnap).GetField("m_constrainZ",
            BindingFlags.Instance | BindingFlags.NonPublic);

        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += OnLoaded;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode ||
            state == PlayModeStateChange.ExitingEditMode)
        {
            Arm();
        }
    }

    /// <summary>场景加载/域重载后武装一次短程轮询（child 就在此刻重建）。</summary>
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

    private static void Tick()
    {
        if (!_armed || EditorApplication.timeSinceStartup > _deadline)
        {
            _armed = false;
            _hasTick = false;
            EditorApplication.update -= Tick;
            return;
        }
        RelaxGridSnapOnScene();
    }

    /// <summary>写回后立即调用：对场景内所有目标 prefab 解除 child 的 X/Z 网格约束。</summary>
    public static void RelaxGridSnapOnScene()
    {
        foreach (var pseudo in UnityEngine.Object.FindObjectsOfType<PseudoPrefab>())
        {
            try
            {
                var child = pseudo.childGameObject;
                if (child == null || !IsTarget(child.name))
                    continue;
                var snap = child.GetComponent<EditorGridSnap>();
                if (snap == null)
                    continue;
                bool changed = false;
                if (ReadConstrain(_constrainXField, snap))
                {
                    WriteConstrain(_constrainXField, snap, false);
                    changed = true;
                }
                if (ReadConstrain(_constrainZField, snap))
                {
                    WriteConstrain(_constrainZField, snap, false);
                    changed = true;
                }
                if (changed)
                {
                    // child 是临时加载对象（保存前被 EnsurePrepareForBuilding 剥离、不进存档），
                    // 无需标脏/重存；场景每次加载/重载后由本守卫重新解除。
                    LayoutEditorLog.Log(
                        "[LayoutEditor] GridSnap guard: 已解除 " + child.name +
                        " 的 EditorGridSnap X/Z 约束（半格摆放生效）");
                }
            }
            catch (Exception)
            {
                // 对象在场景切换中被销毁：跳过，下一轮终止条件兜底
            }
        }
    }

    private static bool IsTarget(string childName)
    {
        if (string.IsNullOrEmpty(childName))
            return false;
        for (int i = 0; i < TargetPrefabNames.Length; i++)
        {
            if (childName == TargetPrefabNames[i])
                return true;
        }
        return false;
    }

    private static bool ReadConstrain(FieldInfo field, EditorGridSnap snap)
    {
        if (field == null || snap == null)
            return false;
        try
        {
            return (bool)field.GetValue(snap);
        }
        catch
        {
            return false;
        }
    }

    private static void WriteConstrain(FieldInfo field, EditorGridSnap snap, bool value)
    {
        if (field == null || snap == null)
            return;
        try
        {
            field.SetValue(snap, value);
        }
        catch
        {
        }
    }
}
