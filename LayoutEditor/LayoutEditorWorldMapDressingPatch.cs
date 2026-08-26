using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LevelEditor;
using LevelEditorStub;

/// <summary>
/// 世界地图装饰运行时补丁（Play 期）。
///
/// 背景：dlc08 map_* 装饰（如 p_dlc08_map_rope_fence_* 绳栏）的 bundle prefab 内
/// 带 WorldMapSceneryOptimizer（Scenery_Optimizer 节点），其 Awake() 会把整个
/// 可视 Mesh 子树 SetActive(false)；只有在世界地图场景里，WorldMapTileFlip 的
/// 展开流程（StartInstantUnfold / UnfoldFlow → End）才会把 Mesh 重新激活。
/// 在普通关卡场景里 m_startFlipped=0 且 m_flipOwnerData.m_levelMapNode=null，
/// 展开流程永远不会触发，表现为：编辑器里可见，进 Play 后整件消失。
///
/// 本补丁在 Play 后扫描所有伪 prefab 实例，对含 WorldMapSceneryOptimizer 的
/// child 直接调用 End(FlipDirection.Unfold)（等价于展开完成的收尾：Mesh 重新
/// 激活、不挂 Animator），让世界地图专用装饰在关卡里常驻可见。
///
/// 注意：这只是**未重新烘焙的旧场景**的编辑器兜底。新写回的场景由
/// LayoutEditorStubIO.BakeWorldMapDressing 给这类伪根烘焙
/// LayoutRuntimeWorldMapDressing（游戏编译，随场景保存，游戏包内同样生效），
/// 已烘焙的伪根本补丁跳过。
/// </summary>
[InitializeOnLoad]
public static class LayoutEditorWorldMapDressingPatch
{
    private static bool _armed;
    private static double _deadline;
    private static readonly HashSet<int> _patched = new HashSet<int>();

    static LayoutEditorWorldMapDressingPatch()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _armed = true;
            _patched.Clear();
            // 兜底超时：PseudoPrefabManager 缺失/child 一直不就绪时不至于每帧空转
            _deadline = EditorApplication.timeSinceStartup + 10.0;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            Disarm();
        }
    }

    private static void Disarm()
    {
        _armed = false;
        EditorApplication.update -= Tick;
    }

    private static void Tick()
    {
        if (!_armed || !Application.isPlaying ||
            EditorApplication.timeSinceStartup > _deadline)
        {
            Disarm();
            return;
        }
        var manager = PseudoPrefabManager.Instance;
        if (manager == null)
            return;

        bool pending = false;
        foreach (var stub in UnityEngine.Object.FindObjectsOfType<PseudoPrefabStub>())
        {
            // 防御：Play 模式进出 / ResetChild 重建期间，stub 或 child 可能
            // 在空检查之后被宿主销毁，访问其成员会抛 MissingReferenceException
            try
            {
                var pseudo = stub.GetComponent<PseudoPrefab>();
                var child = pseudo != null ? pseudo.childGameObject : null;
                if (child == null)
                {
                    pending = true;
                    continue;
                }
                // 已烘焙 LayoutRuntimeWorldMapDressing（游戏编译，随场景保存）的伪根
                // 由运行时组件自行展开，这里跳过以免重复处理。
                if (stub.GetComponent<LayoutRuntimeWorldMapDressing>() != null)
                    continue;
                if (!_patched.Add(child.GetInstanceID()))
                    continue;
                PatchChild(child);
            }
            catch (Exception)
            {
                // 对象被销毁：下一帧重试；退出 Play 则由 isPlaying 检查终止
                pending = true;
            }
        }
        if (!pending)
            Disarm();
    }

    /// <summary>强制展开 child 内所有世界地图装饰：Mesh 重新激活。
    ///  End(Unfold) 只清理可能存在的 Animator 并 SetActive(true)，无副作用。
    ///  Mesh 激活后 bundle 自带的碰撞盒（如 map_rope_fence 腰高 BoxCollider，
    ///  展开前随 Mesh 未激活而不存在）会变成看不见的空气墙——关卡内阻挡一律由
    ///  场景显式空气墙承担，这里把 child 下所有碰撞体一并关掉。</summary>
    private static void PatchChild(GameObject child)
    {
        var optimizers = child.GetComponentsInChildren<WorldMapSceneryOptimizer>(true);
        if (optimizers == null || optimizers.Length == 0)
            return;
        for (int i = 0; i < optimizers.Length; i++)
        {
            var opt = optimizers[i];
            if (opt == null)
                continue;
            var mesh = opt.Mesh;
            if (mesh != null && !mesh.activeSelf)
            {
                opt.End(FlipDirection.Unfold);
                Debug.Log("[LayoutEditor] world-map dressing force-unfold: " + child.name);
            }
        }
        var colliders = child.GetComponentsInChildren<Collider>(true);
        if (colliders == null)
            return;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled)
                colliders[i].enabled = false;
        }
    }
}
