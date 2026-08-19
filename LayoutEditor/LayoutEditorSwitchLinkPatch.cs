using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LevelEditor;
using LevelEditorStub;

/// <summary>
/// 开关运行时补丁（Play 期，仿 LayoutEditorItemSwitcherPatch）。
///
/// 背景：宿主 Switch（common01 bundle47）按压后触发链为
///   交互 impulse "Switch" → TriggerAdapter(Switch→Disable)
///   → TriggerDisableScript 禁用 Interactable（按钮变红、无法再按）；
///   收到 "Reset"（m_enableTrigger）才重新启用（变绿）。
/// 本补丁解决三个运行时问题：
///   1) 开关可反复按：按压周期结束后自动补发 "Reset"（短暂红色反馈后回绿）。
///   2) 触发名同步：switchLinks 的自定义触发名（switch_{目标id}_{N}）写入目标机器
///      PickupItemSwitcher/PlacementItemSwitcher.m_switchTrigger 与断头台
///      AutoWorkstation.m_workTrigger（这些字段都在 OnTrigger 中实时读取）；
///      大炮联动（CannonSwitch，Unity 星形发射按钮）写入 Cannon.m_launchTrigger，
///      并把按钮 child 挂到大炮 Cannon.m_button（宿主 ServerCannon 收发 Reset/Disable）。
///   3) 伪根转发：宿主 TriggerOnObject/按钮逻辑 helper 把触发消息发到伪根
///      GameObject，而监听者都在 bundle child 上（SendTrigger 只查同物体组件），
///      本补丁在伪根挂转发器把消息转给 child；事件组的 done 中继同理挂到 child。
/// </summary>
[InitializeOnLoad]
public static class LayoutEditorSwitchLinkPatch
{
    private static bool _armed;
    private static double _deadline;

    static LayoutEditorSwitchLinkPatch()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _armed = true;
            // 兜底超时：PseudoPrefabManager 缺失/初始化失败时不至于每帧空转
            _deadline = EditorApplication.timeSinceStartup + 10.0;
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
        if (!_armed || !Application.isPlaying ||
            EditorApplication.timeSinceStartup > _deadline)
        {
            _armed = false;
            EditorApplication.update -= Tick;
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

                var sw = stub.GetComponent<PseudoPrefabSwitchStub>();
                var doneRelays = stub.GetComponents<LayoutEditorEventDoneRelay>();
                var hasRelays = doneRelays != null && doneRelays.Length > 0;
                if (sw == null && !hasRelays)
                    continue;

                if (child == null)
                {
                    pending = true;
                    continue;
                }

                if (sw != null && PatchSwitchRoot(stub.gameObject, sw, child))
                    pending = true;

                // 事件组 done 中继：烘焙期写在伪根上，child 就绪后挂到 child
                // （开关也可作为事件目标，转发器与中继照常处理）
                if (hasRelays && PatchDoneRelays(stub.gameObject, child, doneRelays))
                    pending = true;
            }
            catch (Exception)
            {
                // 对象被销毁：若是 ResetChild 重建中则下一帧重试；
                // 若是退出 Play，则下一帧会被 isPlaying 检查终止
                pending = true;
            }
        }
        if (!pending)
        {
            _armed = false;
            EditorApplication.update -= Tick;
        }
    }

    /// <summary>开关伪根：根转发器（child 方向）+ 按压恢复中继 + 目标机器触发名同步。
    ///  返回 true 表示还有 child 未就绪，需要下一帧重试。</summary>
    private static bool PatchSwitchRoot(GameObject root, PseudoPrefabSwitchStub sw, GameObject switchChild)
    {
        bool pending = false;

        // 新场景：写回时已烘焙 LayoutRuntimeSwitchLink（游戏编译，随场景保存），
        // 转发/监听字段/大炮接线/回绿全部由它在编辑器与游戏包内完成。
        // 这里跳过以免与 Linker 重复触发（同一按压目标收到两次消息 → 机器跳一档）。
        if (root.GetComponent<LayoutRuntimeSwitchLink>() != null)
            return pending;

        if (switchChild.GetComponent<TriggerDisableScript>() != null)
        {
            var reenable = switchChild.GetComponent<LayoutEditorSwitchReenableRelay>();
            if (reenable == null)
                switchChild.AddComponent<LayoutEditorSwitchReenableRelay>();
        }

        if (sw.objectToTrigger == null || sw.objectToTrigger.Length == 0)
            return pending;

        var trigger = string.IsNullOrEmpty(sw.triggerOnObject) ? "Switch" : sw.triggerOnObject;
        for (int i = 0; i < sw.objectToTrigger.Length; i++)
        {
            var target = sw.objectToTrigger[i];
            if (target == null)
                continue;
            var targetChild = GetChildOf(target);
            if (targetChild == null)
            {
                pending = true;
                continue;
            }
            // 伪根 → child 转发（宿主 TriggerOnObject 把消息发到伪根）
            EnsureForwarder(target, targetChild);
            PatchTargetListeners(targetChild, trigger);
            PatchCannonLink(switchChild, targetChild, trigger);
        }
        return pending;
    }

    /// <summary>大炮联动（1:1）：把按钮 child 挂到大炮 Cannon.m_button，并把联动
    ///  触发名写入 Cannon.m_launchTrigger。宿主 ServerCannon.StartSynchronising
    ///  需 m_button.RequireComponent&lt;ServerInteractable&gt; 收发 Reset/Disable
    ///  （m_button 为空会 NRE 使大炮失效）；m_launchTrigger 在 OnTrigger 实时读取，
    ///  同步后任意联动触发名（含默认 switch_{id}_{N} 或 Launch）都能发射。</summary>
    private static void PatchCannonLink(GameObject switchChild, GameObject targetChild, string trigger)
    {
        if (switchChild == null || targetChild == null)
            return;
        var cannon = targetChild.GetComponent<Cannon>();
        if (cannon == null)
            return;
        if (!string.IsNullOrEmpty(trigger))
            cannon.m_launchTrigger = trigger;
        cannon.m_button = switchChild;
        Debug.Log("[LayoutEditor] cannon link: " + targetChild.name +
            " m_launchTrigger = " + cannon.m_launchTrigger + " m_button = " + switchChild.name);
    }

    /// <summary>事件组目标伪根：转发器 + done 中继挂到 child（每个根中继对应一个
    ///  child 中继，同一目标可被多条事件引用）。返回 true 表示有 child 未就绪。</summary>
    private static bool PatchDoneRelays(GameObject root, GameObject child, LayoutEditorEventDoneRelay[] relays)
    {
        bool pending = false;
        if (child == null)
            return true;
        EnsureForwarder(root, child);
        var existing = child.GetComponents<LayoutEditorEventDoneRelay>();
        for (int i = 0; i < relays.Length; i++)
        {
            var relay = relays[i];
            if (relay == null)
                continue;
            LayoutEditorEventDoneRelay childRelay = null;
            if (existing != null)
            {
                for (int r = 0; r < existing.Length; r++)
                {
                    if (existing[r] != null &&
                        existing[r].m_listenTrigger == relay.m_listenTrigger &&
                        existing[r].m_forwardTrigger == relay.m_forwardTrigger &&
                        existing[r].m_forwardTo == relay.m_forwardTo)
                    {
                        childRelay = existing[r];
                        break;
                    }
                }
            }
            if (childRelay == null)
                childRelay = child.AddComponent<LayoutEditorEventDoneRelay>();
            childRelay.m_listenTrigger = relay.m_listenTrigger;
            childRelay.m_forwardTrigger = relay.m_forwardTrigger;
            childRelay.m_forwardTo = relay.m_forwardTo;
            UnityEngine.Object.Destroy(relay);
        }
        return pending;
    }

    private static GameObject GetChildOf(GameObject root)
    {
        if (root == null)
            return null;
        var pseudo = root.GetComponent<PseudoPrefab>();
        return pseudo != null ? pseudo.childGameObject : null;
    }

    /// <summary>在伪根上挂转发器：把发到伪根的所有触发消息转给 bundle child。</summary>
    private static void EnsureForwarder(GameObject root, GameObject child)
    {
        if (root == null || child == null)
            return;
        var fwd = root.GetComponent<LayoutEditorRootToChildForwarder>();
        if (fwd == null)
            fwd = root.AddComponent<LayoutEditorRootToChildForwarder>();
        fwd.m_childTarget = child;
    }

    /// <summary>把自定义触发名写入目标 child 的实时读取字段：
    ///  饮料机/酱料机（PickupItemSwitcher/PlacementItemSwitcher.m_switchTrigger）、
    ///  断头台（AutoWorkstation.m_workTrigger，默认 "Chop"）。</summary>
    private static void PatchTargetListeners(GameObject child, string trigger)
    {
        if (child == null || string.IsNullOrEmpty(trigger))
            return;
        var switcher = child.GetComponent<PickupItemSwitcher>();
        if (switcher != null)
        {
            switcher.m_switchTrigger = trigger;
            Debug.Log("[LayoutEditor] switch link: " + child.name +
                " PickupItemSwitcher.m_switchTrigger = " + trigger);
        }
        var placementSwitcher = child.GetComponent<PlacementItemSwitcher>();
        if (placementSwitcher != null)
            placementSwitcher.m_switchTrigger = trigger;
        var workstation = child.GetComponent<AutoWorkstation>();
        if (workstation != null)
        {
            workstation.m_workTrigger = trigger;
            Debug.Log("[LayoutEditor] switch link: " + child.name +
                " AutoWorkstation.m_workTrigger = " + trigger);
        }
    }
}

/// <summary>按压周期结束后补发 Reset，让开关可反复按（短暂红色反馈后回绿）。</summary>
public class LayoutEditorSwitchReenableRelay : MonoBehaviour, ITriggerReceiver
{
    /// <summary>按压后的禁用窗口（秒）：期间按钮呈红色且不可再按。</summary>
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

/// <summary>伪根 → bundle child 触发转发（宿主 TriggerOnObject / 按钮逻辑 helper
///  把消息发到伪根 GameObject，监听者都在 child 上）。</summary>
public class LayoutEditorRootToChildForwarder : MonoBehaviour, ITriggerReceiver
{
    public GameObject m_childTarget;

    public void OnTrigger(string _trigger)
    {
        if (m_childTarget != null)
            m_childTarget.SendTrigger(_trigger);
    }
}

/// <summary>事件组 done 中继：目标 child 广播完成触发器（如断头台 "Reset"）时，
///  转发到按钮逻辑 helper（helper 上的 TriggerOnAnimator 中继转为状态机触发器）。
///  烘焙期写在目标伪根上（场景保存），Play 期由补丁转移到 child。</summary>
public class LayoutEditorEventDoneRelay : MonoBehaviour, ITriggerReceiver
{
    public string m_listenTrigger = "";
    public string m_forwardTrigger = "";
    public GameObject m_forwardTo;

    public void OnTrigger(string _trigger)
    {
        if (!string.IsNullOrEmpty(m_listenTrigger) && _trigger == m_listenTrigger &&
            m_forwardTo != null && !string.IsNullOrEmpty(m_forwardTrigger))
        {
            m_forwardTo.SendTrigger(m_forwardTrigger);
        }
    }
}
