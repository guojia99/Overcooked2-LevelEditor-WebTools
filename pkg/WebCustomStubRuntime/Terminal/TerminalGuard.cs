using System.Collections.Generic;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 未绑定可操控对象的终端防线（降级残留防 NRE）。
    ///
    /// 背景：web 布局里 Terminal 未配置 pilotableObject 时，写回按守卫降级为普通
    /// 道具（DowngradeToBase，LayoutEditorStubIO 日志「终端未配置可操控对象」）。
    /// 降级后基础 PseudoPrefab.Setup 为空操作，真实 bundle prefab
    /// （如 MultiControlTerminal_02）原样实例化——其 Terminal.m_pilotableObject
    /// 为 null（bundle 模板默认）。Play 后两处空引用：
    ///  1) ClientTerminalCosmeticDecisions.Update 每帧
    ///     GetTerminal().m_pilotableObject.HasMoved() → NRE 刷屏；
    ///  2) 玩家交互 → ClientTerminal.CreateSession 里 m_pilotableObject 传参 → NRE。
    ///
    /// 防线（StubTicker 低频扫描，编辑器 Play 与游戏统一）：
    ///  - Terminal.m_pilotableObject == null 的终端 → 禁用同物体 Interactable
    ///    （挡住交互路径 2）、子树 ForwardTriggerToTarget（null target 转发风险；
    ///    宿主 PseudoPrefabTerminal.Setup 正常路径本会删除它们）；
    ///  - ClientTerminalCosmeticDecisions（NRE 1）由同步系统握手后才动态挂载——
    ///    对已记忆终端每轮重复禁用（幂等，覆盖晚挂载）；
    ///  - Terminal 本体保持 enabled（SessionInteractable 无帧逻辑，且避免扰动
    ///    同步系统的注册流程）。
    /// 关卡重开后记忆表残留项自动清理（Unity null 语义）。
    /// </summary>
    internal static class TerminalGuard
    {
        private static readonly List<UnityEngine.Object> s_guarded = new List<UnityEngine.Object>();
        private static bool s_loggedSelfCheck;
        private static bool s_pilotableReadWarned;
        private static bool s_disableFailWarned;

        /// <summary>发现窗口（秒，v12）：终端是场景静态物体，只在关卡加载后的一段
        /// 时间内可能陆续出现（宿主伪 prefab 实例化）。原实现无条件每 ~1s
        /// FindObjectsOfType(Terminal) 跑满整局，纯浪费。窗口结束即停止发现，
        /// 已 guard 的终端仍由 TickRefreshGuarded 持续维护（覆盖晚挂载的外观组件）。</summary>
        private const float DiscoverWindowSeconds = 20f;

        private static float s_discoverUntil = -1f;
        private static bool s_discoverDone;

        internal static void OnSceneChanged()
        {
            s_guarded.Clear();
            // 重新武装发现窗口（下一次 TickDiscover 才取 Time，避免这里依赖调用时机）
            s_discoverUntil = -1f;
            s_discoverDone = false;
        }

        /// <summary>EntryPoint 三态门控的无 tag 探测通道：场景里是否存在
        /// 「未绑定可操控对象」的终端（写回降级残留，不带任何 stub tag——
        /// 只靠 tag 门控会让本防线整体失效，NRE 刷屏回归）。</summary>
        internal static bool ProbeHasUnboundTerminal()
        {
            if (GameApi.TerminalType == null || GameApi.TerminalPilotableField == null)
                return false;
            var terminals = GameApi.FindAll(GameApi.TerminalType);
            for (int i = 0; i < terminals.Length; i++)
            {
                var terminal = terminals[i];
                if (terminal == null)
                    continue;
                try
                {
                    if (GameApi.TerminalPilotableField.GetValue(terminal) == null)
                        return true;
                }
                catch (System.Exception)
                {
                    // 读不到就当没有（真正的防线由 TickDiscover 负责并会报告异常）
                }
            }
            return false;
        }

        /// <summary>发现未绑定 pilotable 的新终端（每 ~1s，EntryPoint 调度；
        /// v12：仅在场景加载后的 DiscoverWindowSeconds 窗口内执行）。</summary>
        internal static void TickDiscover()
        {
            if (s_discoverDone || GameApi.TerminalType == null || GameApi.TerminalPilotableField == null)
                return;
            var now = Time.unscaledTime;
            if (s_discoverUntil < 0f)
                s_discoverUntil = now + DiscoverWindowSeconds;
            if (!s_loggedSelfCheck)
            {
                s_loggedSelfCheck = true;
                StubLog.Dbg("[TerminalGuard] 反射自检: Terminal=" + (GameApi.TerminalType != null)
                    + " PilotableField=" + (GameApi.TerminalPilotableField != null)
                    + " ClientCosmetic=" + (GameApi.ClientTerminalCosmeticType != null)
                    + " ForwardTrigger=" + (GameApi.ForwardTriggerToTargetType != null));
            }
            var terminals = GameApi.FindAll(GameApi.TerminalType);
            for (int i = 0; i < terminals.Length; i++)
            {
                var terminal = terminals[i];
                if (terminal == null)
                    continue;
                var behaviour = terminal as Behaviour;
                if (behaviour == null)
                    continue;
                if (s_guarded.Contains(terminal))
                    continue;
                object pilotable;
                try
                {
                    pilotable = GameApi.TerminalPilotableField.GetValue(terminal);
                }
                catch (System.Exception ex)
                {
                    if (!s_pilotableReadWarned)
                    {
                        s_pilotableReadWarned = true;
                        StubLog.LogWarn("[TerminalGuard] 读取 m_pilotableObject 异常（该终端跳过防线）: " + ex.Message);
                    }
                    continue;
                }
                if (pilotable != null)
                    continue;
                s_guarded.Add(terminal);
                DisableComponents(behaviour.gameObject, GameApi.InteractableType);
                DisableComponents(behaviour.gameObject, GameApi.ForwardTriggerToTargetType);
                StubLog.LogWarn("[TerminalGuard] 终端未绑定可操控对象（写回降级残留），已禁用交互/外观防 NRE: "
                    + behaviour.gameObject.name);
            }
            s_guarded.RemoveAll(IsDead);
            if (now >= s_discoverUntil)
            {
                s_discoverDone = true;
                StubLog.Dbg("[TerminalGuard] 发现窗口结束，停止扫描（已 guard " + s_guarded.Count + " 个）");
            }
        }

        /// <summary>对已 guard 终端刷新 ClientTerminalCosmeticDecisions 禁用（每 ~0.5s）。
        /// 无 guard 目标时立即返回（绝大多数关卡的常态）。</summary>
        internal static void TickRefreshGuarded()
        {
            if (s_guarded.Count == 0 || GameApi.ClientTerminalCosmeticType == null)
                return;
            for (int i = 0; i < s_guarded.Count; i++)
            {
                var terminal = s_guarded[i];
                if (terminal == null)
                    continue;
                var behaviour = terminal as Behaviour;
                if (behaviour == null)
                    continue;
                DisableComponents(behaviour.gameObject, GameApi.ClientTerminalCosmeticType);
            }
            s_guarded.RemoveAll(IsDead);
        }

        /// <summary>缓存的谓词委托（匿名方法会被编译器缓存，这里显式化更直观）。</summary>
        private static bool IsDead(UnityEngine.Object o)
        {
            return o == null;
        }

        /// <summary>禁用物体子树内指定类型的全部 Behaviour（失败静默——类型缺失/无组件）。
        /// v12：异常加 once-flag——本方法跑在 2Hz 周期路径上，原实现每次失败都打一条。</summary>
        private static void DisableComponents(GameObject go, System.Type type)
        {
            if (go == null || type == null)
                return;
            try
            {
                var comps = go.GetComponentsInChildren(type, true);
                for (int i = 0; i < comps.Length; i++)
                {
                    var b = comps[i] as Behaviour;
                    if (b != null && b.enabled)
                        b.enabled = false;
                }
            }
            catch (System.Exception ex)
            {
                if (!s_disableFailWarned)
                {
                    s_disableFailWarned = true;
                    StubLog.LogWarn("[TerminalGuard] 禁用组件异常 " + go.name + ": " + ex.Message);
                }
            }
        }
    }
}
