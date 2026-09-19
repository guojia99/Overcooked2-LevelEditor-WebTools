using System.Collections.Generic;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 大炮防线（编辑器 Play 与真机统一路径，2026-09-19 真机事故）。
    ///
    /// 症状①（空炮时按钮高亮可按）：烘焙链曾给大炮发射按钮（CannonSwitch）也烤了
    /// SwitchReenable|0.35（LayoutEditorStubIO 只排除 ToggleSwitch）——同步启动把
    /// 按钮 Interactable 按startEnabled=0 置灰后，复位组件看到下降沿 0.35s 自动补发
    /// "Reset" 又把空炮按钮点亮。烘焙侧已改为大炮联动按钮不烤 SwitchReenable
    /// （存量场景重写回时摘除），本类再做运行时门控。
    /// 症状②（按一次空炮后谁都进不去炮）：ServerCannon.OnTrigger 对空炮零防护——
    /// m_flying=true + 广播 Launched(m_loadedObject=null)，客户端
    /// ClientCannon.LaunchProjectile(null) 在 _objectToLaunch.transform.SetParent
    /// 处 NRE，协程死在 EndCannonRoutine 之前 → m_flying 永久 true，而
    /// ServerCannonSessionInteractable.CanInteract = !IsFlying() → 大炮永久拒入
    /// （原版就有的软锁，官方关卡靠场景把按钮开局置灰掩盖）。
    ///
    /// 防线（两条，只认 SetupCannonStub 根的 web 大炮，官方 dlc08/09 图零影响）：
    ///  1) Harmony 前缀拦 ServerCannon.OnTrigger（HarmonyPatches.
    ///     ServerCannonOnTriggerPrefix → AllowLaunch）：非发射触发名放行；
    ///     m_loadedObject 为 null 或玩家已不在 AttachPoint 下（Unload 后字段残留
    ///     引用，防把站外面的人误发射）→ 跳过原方法 + WarnOnce。旁路触发
    ///     （定时开关等对空炮发 Launch）同样被拦。
    ///  2) StubTicker 低频门控（TickRefresh，~0.5s）：按「炮内是否有人（玩家挂在
    ///     AttachPoint 下）」做状态迁移时才发触发——空/飞行中 → m_disableTrigger
    ///     （"Disable"，经 ServerTriggerDisableScript 网络同步：所有客户端变灰且
    ///     不可按）；有人 → m_enableTrigger（"Reset"）点亮。走原版网络通道而非
    ///     直写 Interactable.enabled（那只改本机，联机客机外观不同步）；按钮缺
    ///     TriggerDisableScript 时回落直写（告警一次，仅单机正确）。
    /// </summary>
    internal static class CannonGuard
    {
        private class Entry
        {
            internal Component Cannon;
            internal bool Initialized;
            internal bool LastOccupied;
            internal bool TriggerWarned;
        }

        private static readonly List<Entry> s_watched = new List<Entry>();
        private static bool s_loggedSelfCheck;
        private static bool s_noDisableScriptWarned;

        /// <summary>发现窗口（秒，同 TerminalGuard）：大炮是场景静态物体（伪 prefab
        /// child），只在关卡加载后一段时间内可能陆续出现，窗口结束停止全场景扫描。</summary>
        private const float DiscoverWindowSeconds = 20f;

        private static float s_discoverUntil = -1f;
        private static bool s_discoverDone;

        internal static void OnSceneChanged()
        {
            s_watched.Clear();
            s_discoverUntil = -1f;
            s_discoverDone = false;
        }

        /// <summary>本类反射是否可用（缺 Cannon/ServerCannon/SetupCannonStub 任一即
        /// 整体退化为原版行为，由自检日志暴露）。</summary>
        private static bool ReflectionReady
        {
            get
            {
                return GameApi.CannonType != null
                    && GameApi.ServerCannonType != null
                    && GameApi.SetupCannonStubType != null
                    && GameApi.ServerCannonLoadedObjectField != null;
            }
        }

        /// <summary>EntryPoint 三态门控的无 tag 探测通道：场景里是否存在 web 大炮
        /// （Cannon 且祖先带 SetupCannonStub——编辑器宿主与游戏模组同构）。</summary>
        internal static bool ProbeHasCannon()
        {
            if (!ReflectionReady)
                return false;
            var cannons = GameApi.FindAll(GameApi.CannonType);
            for (int i = 0; i < cannons.Length; i++)
            {
                var cannon = cannons[i];
                if (cannon == null)
                    continue;
                if (GameApi.GetComponentInParent(cannon as Component, GameApi.SetupCannonStubType) != null)
                    return true;
            }
            return false;
        }

        /// <summary>发现 web 大炮并接管（每 ~1s，仅发现窗口内；首个命中按需装
        /// 发射拦截补丁）。</summary>
        internal static void TickDiscover()
        {
            if (s_discoverDone || !ReflectionReady)
                return;
            var now = Time.unscaledTime;
            if (s_discoverUntil < 0f)
                s_discoverUntil = now + DiscoverWindowSeconds;
            if (!s_loggedSelfCheck)
            {
                s_loggedSelfCheck = true;
                StubLog.Dbg("[CannonGuard] 反射自检: Cannon=" + (GameApi.CannonType != null)
                    + " ServerCannon=" + (GameApi.ServerCannonType != null)
                    + " SetupCannonStub=" + (GameApi.SetupCannonStubType != null)
                    + " LoadedObjectField=" + (GameApi.ServerCannonLoadedObjectField != null));
            }
            var cannons = GameApi.FindAll(GameApi.CannonType);
            for (int i = 0; i < cannons.Length; i++)
            {
                var cannon = cannons[i];
                if (cannon == null || IsWatched(cannon))
                    continue;
                if (GameApi.GetComponentInParent(cannon as Component, GameApi.SetupCannonStubType) == null)
                    continue; // 官方图大炮：不接管，零影响
                var entry = new Entry();
                entry.Cannon = cannon as Component;
                s_watched.Add(entry);
                StubLog.Log("[CannonGuard] 已接管 web 大炮: " + entry.Cannon.gameObject.name
                    + "（空炮发射拦截 + 按钮占用门控）");
            }
            s_watched.RemoveAll(IsDead);
            if (s_watched.Count > 0)
                EntryPoint.EnsureCannonPatches();
            if (now >= s_discoverUntil)
            {
                s_discoverDone = true;
                StubLog.Dbg("[CannonGuard] 发现窗口结束，停止扫描（已接管 " + s_watched.Count + " 门）");
            }
        }

        /// <summary>按钮占用门控（每 ~0.5s，仅服务端侧发送；无目标立即返回）。</summary>
        internal static void TickRefresh()
        {
            if (s_watched.Count == 0)
                return;
            if (!GameApi.IsServerMachine())
                return;
            for (int i = 0; i < s_watched.Count; i++)
            {
                var entry = s_watched[i];
                if (entry == null || entry.Cannon == null)
                    continue;
                try
                {
                    TickEntry(entry);
                }
                catch (System.Exception ex)
                {
                    if (!entry.TriggerWarned)
                    {
                        entry.TriggerWarned = true;
                        StubLog.LogWarn("[CannonGuard] 门控异常（该炮跳过一轮）: "
                            + entry.Cannon.gameObject.name + " " + ex.Message);
                    }
                }
            }
            s_watched.RemoveAll(IsDead);
        }

        private static void TickEntry(Entry entry)
        {
            var cannonGo = entry.Cannon.gameObject;
            var serverCannon = GameApi.GetComponent(cannonGo, GameApi.ServerCannonType);
            if (serverCannon == null)
                return; // 同步未启动（ServerCannon 由实体扫描后挂载），此阶段按钮本就不可按
            bool occupied;
            if (!TryReadOccupied(serverCannon, entry.Cannon, out occupied))
                return;
            if (entry.Initialized && occupied == entry.LastOccupied)
                return; // 状态未迁移不发触发（避免网络消息刷屏）
            entry.Initialized = true;
            entry.LastOccupied = occupied;
            ApplyButtonState(entry, occupied);
        }

        /// <summary>炮内是否有人：m_loadedObject 非空 且 玩家仍挂在 AttachPoint 下
        /// （Load 时 SetParent(AttachPoint)、Unload/发射后脱离；宿主 Unload 不清
        /// m_loadedObject，仅凭字段判会误把退出后的人当「有人」）。</summary>
        private static bool TryReadOccupied(Component serverCannon, Component cannon, out bool occupied)
        {
            occupied = false;
            var loaded = GameApi.ServerCannonLoadedObjectField.GetValue(serverCannon) as GameObject;
            if (loaded == null)
                return true;
            var attachPoint = GameApi.CannonAttachPointField != null
                ? GameApi.CannonAttachPointField.GetValue(cannon) as Transform
                : null;
            if (attachPoint == null)
            {
                // 读不到 AttachPoint（反射缺失/结构异常）：宁可放行原版行为
                return false;
            }
            occupied = loaded.transform != null && loaded.transform.parent == attachPoint;
            return true;
        }

        private static void ApplyButtonState(Entry entry, bool occupied)
        {
            var cannon = entry.Cannon;
            var button = GameApi.CannonButtonField != null
                ? GameApi.CannonButtonField.GetValue(cannon) as GameObject
                : null;
            if (button == null)
                return; // 无发射按钮（摆设炮），无按钮可门控
            var triggerName = ReadStateTrigger(cannon, occupied);
            if (!string.IsNullOrEmpty(triggerName))
            {
                GameApi.SendTrigger(button, triggerName);
                return;
            }
            // 触发名缺失（异常配置）：回落直写本机 Interactable（联机仅服务端正确）
            if (!s_noDisableScriptWarned)
            {
                s_noDisableScriptWarned = true;
                StubLog.LogWarn("[CannonGuard] 大炮 enable/disable 触发名为空，回落直写 Interactable（联机客机外观可能不同步）: "
                    + cannon.gameObject.name);
            }
            var interactable = GameApi.GetComponent(button, GameApi.InteractableType) as Behaviour;
            if (interactable != null && interactable.enabled != occupied)
                interactable.enabled = occupied;
        }

        private static string ReadStateTrigger(Component cannon, bool occupied)
        {
            var field = occupied ? GameApi.CannonEnableTriggerField : GameApi.CannonDisableTriggerField;
            return field != null ? field.GetValue(cannon) as string : null;
        }

        /// <summary>发射放行判定（Harmony 前缀逻辑）。非发射触发名/反射不可用 =
        /// 放行原方法；发射触发名但炮内无人（含玩家已脱离 AttachPoint）= 拦截。</summary>
        internal static bool AllowLaunch(object serverCannonInstance, string trigger)
        {
            var serverCannon = serverCannonInstance as Component;
            if (serverCannon == null || !ReflectionReady)
                return true;
            if (GameApi.ServerCannonCannonField == null || GameApi.CannonLaunchTriggerField == null)
                return true;
            var cannon = GameApi.ServerCannonCannonField.GetValue(serverCannon) as Component;
            if (cannon == null)
                return true;
            var launchTrigger = GameApi.CannonLaunchTriggerField.GetValue(cannon) as string;
            if (string.IsNullOrEmpty(launchTrigger) || trigger != launchTrigger)
                return true; // 非发射触发：原样放行（原方法自身也会过滤）
            bool occupied;
            if (!TryReadOccupied(serverCannon, cannon, out occupied))
                return true; // 状态读不出来：放行（原版行为兜底）
            if (occupied)
                return true;
            WarnBlockedOnce(serverCannon);
            return false;
        }

        private static readonly HashSet<string> s_blockWarned = new HashSet<string>();

        private static void WarnBlockedOnce(Component serverCannon)
        {
            var key = serverCannon != null && serverCannon.gameObject != null
                ? serverCannon.gameObject.GetInstanceID().ToString()
                : "?";
            if (!s_blockWarned.Add(key))
                return;
            StubLog.LogWarn("[CannonGuard] 已拦截空炮发射（原版此路径会让 m_flying 永久为 true、"
                + "大炮从此无法进入——详见 ServerCannon.OnTrigger/ClientCannon.LaunchProjectile）: "
                + (serverCannon != null && serverCannon.gameObject != null ? serverCannon.gameObject.name : "?"));
        }

        /// <summary>SwitchReenable 复位跳过判定：go（按钮伪根）子树里是否含本守卫
        /// 接管的大炮发射按钮（m_button 指向按钮 child，IsChildOf 含自身）。
        /// 时序兜底：SwitchReenable 绑定可能早于大炮发现，故其在每次复位前都会
        /// 复查本方法。</summary>
        internal static bool IsCannonButton(GameObject buttonRoot)
        {
            if (buttonRoot == null || s_watched.Count == 0)
                return false;
            var rootT = buttonRoot.transform;
            for (int i = 0; i < s_watched.Count; i++)
            {
                var entry = s_watched[i];
                if (entry == null || entry.Cannon == null)
                    continue;
                var button = GameApi.CannonButtonField != null
                    ? GameApi.CannonButtonField.GetValue(entry.Cannon) as GameObject
                    : null;
                if (button == null || button.transform == null)
                    continue;
                if (button.transform.IsChildOf(rootT))
                    return true;
            }
            return false;
        }

        private static bool IsWatched(UnityEngine.Object cannon)
        {
            for (int i = 0; i < s_watched.Count; i++)
            {
                var entry = s_watched[i];
                if (entry != null && entry.Cannon == cannon)
                    return true;
            }
            return false;
        }

        private static bool IsDead(Entry entry)
        {
            return entry == null || entry.Cannon == null;
        }
    }
}
