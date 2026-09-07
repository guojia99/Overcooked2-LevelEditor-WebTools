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
    /// 防线（TerminalGuardTicker 低频扫描，编辑器 Play 与游戏统一）：
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

        internal static void Tick()
        {
            if (GameApi.TerminalType == null || GameApi.TerminalPilotableField == null)
                return;
            if (!s_loggedSelfCheck)
            {
                s_loggedSelfCheck = true;
                StubLog.Log("[TerminalGuard] 反射自检: Terminal=" + (GameApi.TerminalType != null)
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
                if (!s_guarded.Contains(terminal))
                {
                    object pilotable;
                    try
                    {
                        pilotable = GameApi.TerminalPilotableField.GetValue(terminal);
                    }
                    catch (System.Exception)
                    {
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
                // 握手后动态挂载的同步脚本——每轮重复禁用（幂等，覆盖晚挂载）
                DisableComponents(behaviour.gameObject, GameApi.ClientTerminalCosmeticType);
            }
            // 关卡重开/重载后清掉已销毁表项
            s_guarded.RemoveAll(delegate(UnityEngine.Object o) { return o == null; });
        }

        /// <summary>禁用物体子树内指定类型的全部 Behaviour（失败静默——类型缺失/无组件）。</summary>
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
                StubLog.LogWarn("[TerminalGuard] 禁用组件异常 " + go.name + ": " + ex.Message);
            }
        }
    }
}
