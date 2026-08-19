using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LevelEditor;
using LevelEditorStub;
using UnityEngine;

/// 开关联动运行时接线（游戏编译，写回时烘焙到开关伪根上，随场景保存）。
///
/// 背景：宿主 `PseudoPrefabSwitch.Setup` 只在包装 prefab 带 `PseudoPrefabSwitch`
/// 组件时运行（common01 的 Switch 有，common03 的 DLC8 按钮没有）；且伪根→child 的
/// 触发转发、机器监听字段（m_switchTrigger / m_workTrigger）、大炮 `Cannon.m_button`
/// （宿主 ServerCannon.StartSynchronising 需要，否则 NRE）原版都是关卡场景手工接线，
/// 编辑器产生的伪 prefab 关卡没有。
///
/// 本组件在运行时（child 就绪后，每帧轻量校正）负责：
///   1) 按钮 child 挂 LayoutRuntimeSwitchRelay（监听 "Switch" → 向目标 child
///      广播自定义触发名），替代 Setup 缺失时缺失的 TriggerOnObject；
///   2) 目标伪根挂 LayoutRuntimeForwarder（伪根→child 转发，服务 Setup 生成的
///      TriggerOnObject 路径）；
///   3) 写目标 child 监听字段（PickupItemSwitcher / PlacementItemSwitcher
///      .m_switchTrigger、AutoWorkstation.m_workTrigger）；
///   4) 大炮：写 Cannon.m_launchTrigger 并把按钮 child 挂到 Cannon.m_button；
///      炮内玩家摇杆分给 ServerPilotRotation（瞄准）；发射按钮门控（炮内有人才可点）；
///   5) 按钮按压后 0.35s 自动补发 Reset 回绿（LayoutRuntimeSwitchReenable）。
public class LayoutRuntimeSwitchLink : MonoBehaviour
{
    public GameObject[] m_targetRoots;
    public string m_trigger = "Switch";

    private GameObject m_buttonChild;
    private bool m_ready;
    private bool m_useSetup; // 按钮包装带 PseudoPrefabSwitch（宿主 Setup 会生成 TriggerOnObject）

    private void Update()
    {
        var pseudo = GetComponent<PseudoPrefab>();
        if (pseudo == null)
            return;
        m_buttonChild = pseudo.childGameObject;
        if (m_buttonChild == null)
            return; // child 尚未生成（ResetChild 未执行），下帧再试

        if (!m_ready)
        {
            m_ready = true;
            m_useSetup = GetComponent<PseudoPrefabSwitch>() != null;
            AddReenableRelay();
            AddTriggerRelay();
        }

        if (m_targetRoots != null)
        {
            for (int i = 0; i < m_targetRoots.Length; i++)
            {
                var target = m_targetRoots[i];
                if (target == null)
                    continue;
                var tp = target.GetComponent<PseudoPrefab>();
                if (tp == null || tp.childGameObject == null)
                    continue;
                var child = tp.childGameObject;
                EnsureForwarder(target);
                WriteListenerFields(child);
                var cannon = child.GetComponent<Cannon>();
                if (cannon != null)
                {
                    if (!string.IsNullOrEmpty(m_trigger))
                        cannon.m_launchTrigger = m_trigger;
                    cannon.m_button = m_buttonChild;
                    PatchCannon(cannon);
                }
            }
        }
    }

    private void AddReenableRelay()
    {
        if (m_buttonChild.GetComponent<TriggerDisableScript>() == null)
            return;
        if (m_buttonChild.GetComponent<LayoutRuntimeSwitchReenable>() == null)
            m_buttonChild.AddComponent<LayoutRuntimeSwitchReenable>();
    }

    private void AddTriggerRelay()
    {
        // 宿主 Setup 路径（包装带 PseudoPrefabSwitch）：Setup 已在 child 生成
        // TriggerOnObject（发到目标伪根 → forwarder 转发），再加 relay 会双重触发
        // （机器跳档）。仅非 Setup 的按钮（common03 DLC8 按钮等）走 relay。
        if (m_useSetup)
            return;
        var relay = m_buttonChild.GetComponent<LayoutRuntimeSwitchRelay>();
        if (relay == null)
            relay = m_buttonChild.AddComponent<LayoutRuntimeSwitchRelay>();
        relay.m_triggerToFire = m_trigger;
        relay.m_targetRoots = m_targetRoots;
    }

    private void EnsureForwarder(GameObject target)
    {
        // 仅 Setup 路径需要 forwarder（Setup 的 TriggerOnObject 发到目标伪根 → 转发到 child）；
        // relay 路径直达 child，不需要。
        if (!m_useSetup)
            return;
        if (target.GetComponent<LayoutRuntimeForwarder>() == null)
            target.AddComponent<LayoutRuntimeForwarder>();
    }

    private void WriteListenerFields(GameObject child)
    {
        if (string.IsNullOrEmpty(m_trigger))
            return;
        var switcher = child.GetComponent<PickupItemSwitcher>();
        if (switcher != null)
            switcher.m_switchTrigger = m_trigger;
        var placement = child.GetComponent<PlacementItemSwitcher>();
        if (placement != null)
            placement.m_switchTrigger = m_trigger;
        var workstation = child.GetComponent<AutoWorkstation>();
        if (workstation != null)
            workstation.m_workTrigger = m_trigger;
    }

    /// <summary>大炮：炮内玩家摇杆分给 ServerPilotRotation（瞄准）；发射按钮门控
    ///  （炮内有人才可点，空炮置灰）。loaded 用 AttachPoint 下是否挂着玩家判定
    ///  （Load 时玩家 SetParent(AttachPoint)，Unload/发射后脱离）。</summary>
    private void PatchCannon(Cannon cannon)
    {
        var serverCannon = cannon.GetComponent<ServerCannon>();
        if (serverCannon == null)
            return; // 同步尚未启动，下帧再试
        bool loaded = cannon.m_attachPoint != null && cannon.m_attachPoint.childCount > 0;
        GameObject player = loaded ? cannon.m_attachPoint.GetChild(0).gameObject : null;

        var pilot = cannon.GetComponent<ServerPilotRotation>();
        if (pilot != null)
        {
            if (loaded && player != null)
            {
                var pc = player.GetComponent<PlayerControls>();
                if (pc != null)
                    pilot.AssignPlayer(pc.ControlScheme);
            }
            else
            {
                pilot.AssignPlayer(null);
            }
        }

        var button = cannon.m_button;
        if (button != null)
        {
            var interactable = button.GetComponent<Interactable>();
            if (interactable != null && interactable.enabled != loaded)
                interactable.enabled = loaded;
        }
    }
}

/// <summary>目标伪根上的触发转发（游戏编译）：把发到伪根的触发消息转给 bundle child。
///  宿主 TriggerOnObject / SendMessage 把消息发到伪根，而监听者都在 child 上。</summary>
public class LayoutRuntimeForwarder : MonoBehaviour, ITriggerReceiver
{
    public void OnTrigger(string _trigger)
    {
        if (string.IsNullOrEmpty(_trigger))
            return;
        var pseudo = GetComponent<PseudoPrefab>();
        if (pseudo == null || pseudo.childGameObject == null)
            return;
        pseudo.childGameObject.SendTrigger(_trigger);
    }
}

/// <summary>按钮 child 上的触发中继（游戏编译）：收到按钮按压消息 "Switch" 时，
///  向每个目标伪根的 child 广播自定义触发名。替代 Setup 缺失时缺失的 TriggerOnObject
///  （直接用 ITriggerReceiver，无需网络同步器配对，运行时挂载也可用）。</summary>
public class LayoutRuntimeSwitchRelay : MonoBehaviour, ITriggerReceiver
{
    public string m_triggerToFire = "Switch";
    public GameObject[] m_targetRoots;

    public void OnTrigger(string _trigger)
    {
        if (_trigger != "Switch" || string.IsNullOrEmpty(m_triggerToFire) || m_targetRoots == null)
            return;
        for (int i = 0; i < m_targetRoots.Length; i++)
        {
            var target = m_targetRoots[i];
            if (target == null)
                continue;
            var pseudo = target.GetComponent<PseudoPrefab>();
            if (pseudo == null || pseudo.childGameObject == null)
                continue;
            pseudo.childGameObject.SendTrigger(m_triggerToFire);
        }
    }
}

/// <summary>按钮按压周期结束后补发 Reset（宿主 TriggerDisableScript 收到 "Disable"
///  会禁用 Interactable；本组件挂在按钮 child 上 0.35s 后回绿，使开关可反复按）。</summary>
public class LayoutRuntimeSwitchReenable : MonoBehaviour, ITriggerReceiver
{
    public float m_resetDelay = 0.35f;

    public void OnTrigger(string _trigger)
    {
        if (_trigger == "Disable")
        {
            StopAllCoroutines();
            StartCoroutine(ReenableLater());
        }
    }

    private IEnumerator ReenableLater()
    {
        yield return new WaitForSeconds(m_resetDelay);
        if (this != null && gameObject != null)
            gameObject.SendTrigger("Reset");
    }
}
