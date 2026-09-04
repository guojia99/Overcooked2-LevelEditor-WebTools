using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 大炮运行时补丁（Play 期）。
///
/// 背景（bundle 实测）：发射按钮链路（踩按钮 → "Switch" → TriggerOnObject →
/// "Launch" → ServerCannon.OnTrigger）已由 LayoutEditorSwitchLinkPatch 接线
/// （cannon.m_button / m_launchTrigger 已写入）。但宿主 ServerCannonSessionInteractable
/// 不会把玩家摇杆分配给大炮的 ServerPilotRotation（对比 ServerTerminal 会分配），
/// 导致进炮后无法旋转炮管瞄准；且发射按钮空炮时也可点击（TriggerDisableScript
/// startEnabled=1），发射空炮时 ClientCannon 对 null 目标会异常。
///
/// 本补丁在 Play 期持续校正每个已接线的大炮：
///   1) 瞄准：玩家 Load 时把 PlayerControls.ControlScheme 分配给 ServerPilotRotation
///      （左摇杆旋转 RotatingPart → 挂在它下面的 Target（落点）随之移动，玩家即可
///      控制落点）；Unload 时清空。
///   2) 按钮门控：只有炮内有人才允许按发射按钮（直接控制按钮 Interactable.enabled，
///      空炮置灰、加载点亮），并移除按钮上的 LayoutEditorSwitchReenableRelay
///      （避免空炮时 0.35s 自动回绿与门控打架）。
/// </summary>
[InitializeOnLoad]
public static class LayoutEditorCannonPatch
{
    private static bool _armed;
    private static FieldInfo _loadedObjectField;

    static LayoutEditorCannonPatch()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _armed = true;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            _armed = false;
            EditorApplication.update -= Tick;
        }
    }

    private static void Tick()
    {
        if (!_armed || !Application.isPlaying)
        {
            _armed = false;
            EditorApplication.update -= Tick;
            return;
        }
        // 场景里没有大炮（伪 prefab child 未生成/未使用）即停止空转；
        // 有大炮但 m_button 尚未接线时保持轮询（等开关补丁写入）。
        var cannons = UnityEngine.Object.FindObjectsOfType<Cannon>();
        if (cannons == null || cannons.Length == 0)
        {
            _armed = false;
            EditorApplication.update -= Tick;
            return;
        }
        for (int i = 0; i < cannons.Length; i++)
        {
            var cannon = cannons[i];
            if (cannon == null)
                continue;
            if (cannon.m_button == null)
            {
                // 未配发射按钮的大炮：宿主 ServerCannon.StartSynchronising 会
                // m_button.RequireComponent<ServerInteractable>() 空引用，且异常被
                // PeerBase/ ExceptionManager 静默吞掉（本 stub 工程 LogLastException
                // 为空实现）→ 客户端永远发不出 StartedSyncronising → 整局卡死。
                // 这里在同步启动前用空按钮兜底（大炮变为纯摆设，不可发射）。
                EnsureFallbackButton(cannon);
                continue;
            }
            try
            {
                PatchCannon(cannon);
            }
            catch (Exception)
            {
                // 对象销毁/同步未完成，下一帧再试
            }
        }
    }

    private const string FallbackButtonName = "__leCannonFallbackButton";

    /// <summary>给未接线的大炮挂一个空按钮对象（含 ServerInteractable，宿主
    /// RequireComponent 为「取不到则返回 null」语义，必须提前挂好）。</summary>
    private static void EnsureFallbackButton(Cannon cannon)
    {
        var existing = cannon.transform.Find(FallbackButtonName);
        GameObject go = existing != null ? existing.gameObject : null;
        if (go == null)
        {
            go = new GameObject(FallbackButtonName);
            go.transform.SetParent(cannon.transform, false);
            go.AddComponent<ServerInteractable>();
            Debug.LogWarning("[LayoutEditor] 大炮未配置发射按钮（无开关链接），已用空按钮兜底: "
                + cannon.name + "。若要可发射，请在 web 编辑器给大炮配置开关。");
        }
        cannon.m_button = go;
    }

    private static void PatchCannon(Cannon cannon)
    {
        var serverCannon = cannon.GetComponent<ServerCannon>();
        if (serverCannon == null)
            return; // 同步尚未启动
        var loadedObject = ReadLoadedObject(serverCannon);
        bool loaded = loadedObject != null;
        // 炮内有人才算 loaded：宿主 Unload 后 m_loadedObject 仍残留引用，玩家已不再
        // 挂在 AttachPoint 下（Load 时 SetParent(AttachPoint)、Unload/发射后脱离），
        // 以父子关系判定，避免退出后按钮仍可点、把外面的人误发射出去。
        if (loaded && cannon.m_attachPoint != null && loadedObject.transform.parent != cannon.m_attachPoint)
            loaded = false;

        // 1) 瞄准：把炮内玩家的摇杆分给 ServerPilotRotation（空炮时清空）。
        //    大炮被控制终端（MultiControlTerminal，m_pilotableObject 指向本炮的
        //    PilotMovement）绑定时跳过：瞄准完全交给终端玩家（宿主 ServerTerminal
        //    会 AssignPlayer），座位赋值/清空会与终端打架。
        var pilot = cannon.GetComponent<ServerPilotRotation>();
        if (pilot != null && !IsTerminalAimed(cannon))
        {
            if (loaded)
            {
                var pc = loadedObject.GetComponent<PlayerControls>();
                if (pc != null)
                    pilot.AssignPlayer(pc.ControlScheme);
            }
            else
            {
                pilot.AssignPlayer(null);
            }
        }

        // 2) 按钮门控：只有炮内有人才允许按发射按钮。
        var button = cannon.m_button;
        if (button != null)
        {
            var relay = button.GetComponent<LayoutEditorSwitchReenableRelay>();
            if (relay != null)
                UnityEngine.Object.Destroy(relay);
            var interactable = button.GetComponent<Interactable>();
            if (interactable != null && interactable.enabled != loaded)
                interactable.enabled = loaded;
        }
    }

    /// <summary>本炮是否被控制终端绑定为可操控目标（Terminal.m_pilotableObject
    ///  == 本炮的 PilotMovement）。绑定后瞄准权归终端玩家。
    ///  绑定关系运行期只增不减，命中即缓存（避免每帧 FindObjectsOfType）。</summary>
    private static readonly System.Collections.Generic.Dictionary<Cannon, bool> _terminalAimed =
        new System.Collections.Generic.Dictionary<Cannon, bool>();

    private static bool IsTerminalAimed(Cannon cannon)
    {
        bool cached;
        if (_terminalAimed.TryGetValue(cannon, out cached))
            return cached;
        bool result = false;
        var pilotMovement = cannon.GetComponent<PilotMovement>();
        if (pilotMovement != null)
        {
            var terminals = UnityEngine.Object.FindObjectsOfType<Terminal>();
            for (int i = 0; i < terminals.Length; i++)
            {
                var t = terminals[i];
                if (t != null && t.m_pilotableObject == pilotMovement)
                {
                    result = true;
                    break;
                }
            }
        }
        if (result)
            _terminalAimed[cannon] = true;
        return result;
    }

    private static GameObject ReadLoadedObject(ServerCannon serverCannon)
    {
        if (_loadedObjectField == null)
        {
            _loadedObjectField = typeof(ServerCannon)
                .GetField("m_loadedObject", BindingFlags.Instance | BindingFlags.NonPublic);
        }
        if (_loadedObjectField == null)
            return null;
        return _loadedObjectField.GetValue(serverCannon) as GameObject;
    }
}
