using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LevelEditor;

/// <summary>
/// 网格吸附守卫：解除场景内所有 PseudoPrefab child 的 EditorGridSnap X/Z 约束。
///
/// 背景：EditorGridSnap（[ExecuteInEditMode]）在编辑模式每帧把 child 的 X/Z 吸到
/// 1.2 格点（QuadGridManager 格点 = 格子中心），宿主 PseudoPrefab.Update 再按 child
/// 位置反向同步根节点（PseudoPrefab.cs 的 transform.position = child.position）→
/// Web 端写回的自由摆放/半格坐标被拉回整格，写回-读回循环中持续漂移，直到恰好
/// 落格才停。宿主 ResetChild 每次场景/域重载都重新启用该组件（只放行 m_constrainY），
/// 且宿主仅豁免 Teleportal/PlateStation/WashingStation/TriggerZone/PhysicalAttachment
/// 与 "Animated Objects" 下的成员。
///
/// 覆盖范围实证（72 个 bundle 全量 UnityPy 扫描）：约 250 个玩法 prefab 的 child 带
/// 此组件（countertop/oven/mixer/dispenser/raft/movingplatform/pfx 风效等，分布在
/// 14 个玩法 bundle），美术 decor bundle 均不带 → 本守卫对 decor 天然是空操作。
///
/// 处理：armed-tick 轮询（场景加载 + 进入编辑模式 + 写回后），对场景内所有 child 的
/// EditorGridSnap 反射写 m_constrainX=m_constrainZ=false（仅当当前为 true 才写，
/// 避免无谓标脏）。整格摆放时格点吸附本是空操作，解除零行为变化；自由摆放/半格
/// 坐标则原样生效。宿主文件一律不动。
/// </summary>
[InitializeOnLoad]
static class LayoutEditorGridSnapGuard
{
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
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        RelaxGridSnapOnScene();
        EditorApplication.delayCall += () => RelaxGridSnapOnScene();
        Arm();
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

    /// <summary>写回后立即调用：对场景内所有 child 解除 EditorGridSnap 的 X/Z 网格约束。</summary>
    public static void RelaxGridSnapOnScene()
    {
        int relaxed = 0;
        string sample = null;
        foreach (var pseudo in UnityEngine.Object.FindObjectsOfType<PseudoPrefab>())
        {
            try
            {
                var child = pseudo.childGameObject;
                if (child == null)
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
                // 约束解除后仍可能因 enabled 在 ResetChild 后首帧吸附；压力开关
                // 占地中心若落在 n×1.2 格点会被吸到 0.6 mod 1.2 并带动父 stub 偏移。
                if (snap.enabled)
                {
                    snap.enabled = false;
                    changed = true;
                }
                if (changed)
                {
                    relaxed++;
                    if (sample == null)
                        sample = child.name;
                }
            }
            catch (Exception)
            {
                // 对象在场景切换中被销毁：跳过，下一轮终止条件兜底
            }
        }
        if (relaxed > 0)
        {
            // child 是临时加载对象（保存前被 EnsurePrepareForBuilding 剥离、不进存档），
            // 无需标脏/重存；场景每次加载/重载后由本守卫重新解除。
            LayoutEditorLog.Log(
                "[LayoutEditor] GridSnap guard: 已解除 " + relaxed + " 个 child 的 EditorGridSnap X/Z 约束（如 " +
                sample + "），web 坐标原样生效");
        }
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
