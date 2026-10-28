using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 茶杯水槽脏杯模型显隐校正（对齐原版水槽 UpdateCosmetics 逻辑）。
    ///
    /// 背景：原版 ClientWashingStation.Awake() 会把 WashingStation.m_dirtyPlates
    /// 全部隐藏，之后靠服务端 AddPlates/CleanedPlate 事件的 m_plateCount 显示前 N 个。
    /// 但 ClientWashingStation 是同步器【镜像后加】的组件，Awake 跑得比伪预制体
    /// 加载晚，且茶杯水槽 m_progressUIPrefab 在 Awake 时点尚未接线会抛 NRE——
    /// 结果 web_sink_teacup 的 3 个脏杯模型开局常显、不随数量变化。
    ///
    /// 这里周期直接按权威状态校正显隐（0 → 全隐藏；N → 显示前 min(N, 模型数) 个），
    /// 与 Awake/事件路径无关，自愈任意时序。只对「烘干位杯堆模板是
    /// web_teacup_stack_clean」的茶杯水槽生效，不影响其它水槽。
    ///
    /// 2026-10-28 追加【权威计数】：编辑器 Play（单机）里 ServerWashingStation 与
    /// ClientWashingStation 同时在本地，客户端事件偶发丢失/延迟时只读客户端计数会
    /// 让水槽少显示脏杯（用户反馈「一堆放进去只显示 1 个」）。现在优先读服务端
    /// m_plateCount（权威），没有服务端组件（真机客户端）才回落到客户端计数；
    /// 同时把每次变化（含两侧计数、槽位数、已显示数）打进日志，便于定位同步问题。
    /// </summary>
    public static class SinkDirtyPlateVisibility
    {
        private static Type s_washingStationType;
        private static Type s_clientWashingStationType;
        private static Type s_serverWashingStationType;
        private static FieldInfo s_dirtyPlatesField;        // WashingStation.m_dirtyPlates (public GameObject[])
        private static FieldInfo s_dryingField;             // WashingStation.m_dryingStation (public PlateReturnStation)
        private static FieldInfo s_stackPrefabField;        // PlateReturnStation.m_stackPrefab (public GameObject)
        private static FieldInfo s_plateCountField;         // ClientWashingStation.m_plateCount (private int)
        private static FieldInfo s_serverPlateCountField;   // ServerWashingStation.m_plateCount (private int)
        private static bool s_init;
        private static bool s_loggedReflectMiss;

        /// <summary>每个水槽上一次打印的状态签名（只在变化时打一行，避免刷屏）。</summary>
        private static readonly Dictionary<int, string> s_lastSignature = new Dictionary<int, string>();

        public static void Tick()
        {
            if (!EnsureInit())
                return;
            var stations = UnityEngine.Object.FindObjectsOfType(s_washingStationType);
            for (int i = 0; i < stations.Length; i++)
            {
                var station = stations[i] as Component;
                if (station == null)
                    continue;
                try
                {
                    Apply(station, false);
                }
                catch (Exception ex)
                {
                    StubLog.LogWarn("[SinkDirtyPlateVisibility] 校正异常: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 事件驱动即时校正：洗杯消息（AddPlates/CleanedPlate）落地后立刻按权威计数
        /// 显示 min(待洗, 槽位) 个脏杯。原版 ClientWashingStation.UpdateCosmetics
        /// 用的是客户端 m_plateCount，消息与显示之间会有一拍延迟/丢包，这里补齐。
        /// </summary>
        public static void ApplyNow(Component washing)
        {
            if (washing == null || !EnsureInit())
                return;
            try
            {
                Apply(washing, true);
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[SinkDirtyPlateVisibility] 即时校正异常: " + ex.Message);
            }
        }

        /// <summary>每个水槽第一次成功接管时打一行（证明本子系统在跑，且槽位数正确）。</summary>
        private static readonly HashSet<int> s_handled = new HashSet<int>();

        private static void Apply(Component station, bool logWhenUnchanged)
        {
            var plates = s_dirtyPlatesField.GetValue(station) as GameObject[];
            if (plates == null || plates.Length == 0)
                return;
            // 只处理茶杯水槽（烘干位杯堆模板是 web_teacup_stack_clean）
            var drying = s_dryingField.GetValue(station) as Component;
            if (drying == null)
                return;
            var stackPrefab = s_stackPrefabField.GetValue(drying) as GameObject;
            if (!IsTeacupStackPrefab(stackPrefab))
                return;

            // 确认真茶杯水槽后再兜底重试一次补丁安装（早期 Install 反射未就绪时
            // 5s 一次的 retry 路径；成功安装后本调用是纯静态字段检查）。
            EntryPoint.EnsureDirtyTeacupPatches();

            int clientCount;
            int serverCount;
            int count = ReadCount(station, out clientCount, out serverCount);
            // 1 -> 1 个、2 -> 2 个、>=3 -> 3 个（槽位数）。
            int visible = Mathf.Clamp(count, 0, plates.Length);
            int active = 0;
            bool changed = false;
            for (int i = 0; i < plates.Length; i++)
            {
                var p = plates[i];
                if (p == null)
                    continue;
                bool want = i < visible;
                if (p.activeSelf != want)
                {
                    p.SetActive(want);
                    changed = true;
                }
                if (want)
                    active++;
            }
            string reason = null;
            if (changed)
                reason = "显隐校正";
            else if (s_handled.Add(station.GetInstanceID()))
                reason = "首次接管";
            else if (logWhenUnchanged)
                reason = "显隐复核";
            if (reason != null)
                LogState(station, reason, clientCount, serverCount, count, plates.Length, active);
        }

        /// <summary>茶杯水槽脏杯状态一行摘要（供 Harmony 补丁在放置/事件时打点）。</summary>
        public static string Describe(Component washing)
        {
            try
            {
                if (washing == null)
                    return "sink=null";
                if (!EnsureInit())
                    return "sink=" + washing.name + "(反射未就绪)";
                var plates = s_dirtyPlatesField.GetValue(washing) as GameObject[];
                int clientCount;
                int serverCount;
                int count = ReadCount(washing, out clientCount, out serverCount);
                int slots = plates == null ? 0 : plates.Length;
                int active = 0;
                if (plates != null)
                {
                    for (int i = 0; i < plates.Length; i++)
                    {
                        var p = plates[i];
                        if (p != null && p.activeSelf)
                            active++;
                    }
                }
                return "sink=" + washing.name + "@" + FormatPos(washing.transform.position)
                    + " 待洗=" + count + "(服务端=" + FormatCount(serverCount)
                    + ",客户端=" + clientCount + ")"
                    + " 槽位=" + slots + " 已显示=" + active;
            }
            catch (Exception ex)
            {
                return "sink描述异常:" + ex.Message;
            }
        }

        private static string FormatCount(int serverCount)
        {
            return serverCount < 0 ? "无组件" : serverCount.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatPos(Vector3 pos)
        {
            return pos.x.ToString("0.0", CultureInfo.InvariantCulture) + ","
                + pos.y.ToString("0.0", CultureInfo.InvariantCulture) + ","
                + pos.z.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static void LogState(Component station, string reason, int clientCount,
            int serverCount, int count, int slots, int active)
        {
            string sig = reason + "|" + count + "|" + clientCount + "|" + serverCount
                + "|" + slots + "|" + active;
            int id = station.GetInstanceID();
            string last;
            if (s_lastSignature.TryGetValue(id, out last) && last == sig)
                return;
            s_lastSignature[id] = sig;
            StubLog.Log("[SinkDirtyPlateVisibility] " + station.name + "@" + FormatPos(station.transform.position)
                + " " + reason + "：待洗=" + count + "（服务端=" + FormatCount(serverCount)
                + ",客户端=" + clientCount + "） 槽位=" + slots + " 已显示=" + active);
            if (serverCount >= 0 && serverCount != clientCount)
            {
                StubLog.LogWarn("[SinkDirtyPlateVisibility] " + station.name
                    + " 服务端/客户端待洗数不一致：服务端=" + serverCount + " 客户端=" + clientCount
                    + "（服务端权威，已按服务端显示；若反复不一致说明 WashingStation 事件未送达客户端）");
            }
        }

        /// <summary>
        /// 权威待洗数：编辑器 Play/主机 = ServerWashingStation.m_plateCount；
        /// 真机客户端没有服务端组件 → 回落 ClientWashingStation.m_plateCount。
        /// </summary>
        private static int ReadCount(Component station, out int clientCount, out int serverCount)
        {
            clientCount = 0;
            if (s_clientWashingStationType != null && s_plateCountField != null)
            {
                var client = station.GetComponent(s_clientWashingStationType);
                if (client != null)
                {
                    var v = s_plateCountField.GetValue(client);
                    if (v is int)
                        clientCount = (int)v;
                }
            }
            serverCount = -1;
            if (s_serverWashingStationType != null && s_serverPlateCountField != null)
            {
                var server = station.GetComponent(s_serverWashingStationType);
                if (server != null)
                {
                    var v = s_serverPlateCountField.GetValue(server);
                    if (v is int)
                        serverCount = (int)v;
                }
            }
            return serverCount >= 0 ? serverCount : clientCount;
        }

        private static bool IsTeacupStackPrefab(GameObject stackPrefab)
        {
            return stackPrefab != null
                && !string.IsNullOrEmpty(stackPrefab.name)
                && stackPrefab.name.StartsWith("web_teacup_stack_clean", StringComparison.Ordinal);
        }

        private static bool EnsureInit()
        {
            if (s_init)
                return s_washingStationType != null && s_dirtyPlatesField != null;
            s_init = true;
            try
            {
                s_washingStationType = GameApi.Find("WashingStation");
                s_clientWashingStationType = GameApi.Find("ClientWashingStation");
                s_serverWashingStationType = GameApi.Find("ServerWashingStation");
                if (s_washingStationType != null)
                {
                    s_dirtyPlatesField = s_washingStationType.GetField("m_dirtyPlates",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    s_dryingField = s_washingStationType.GetField("m_dryingStation",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (s_clientWashingStationType != null)
                {
                    s_plateCountField = s_clientWashingStationType.GetField("m_plateCount",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (s_serverWashingStationType != null)
                {
                    s_serverPlateCountField = s_serverWashingStationType.GetField("m_plateCount",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                // PlateReturnStation.m_stackPrefab 在宿主侧是 public
                var prs = GameApi.Find("PlateReturnStation");
                if (prs != null)
                {
                    s_stackPrefabField = prs.GetField("m_stackPrefab",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                bool ok = s_washingStationType != null
                    && s_dirtyPlatesField != null
                    && s_dryingField != null
                    && s_stackPrefabField != null
                    && s_clientWashingStationType != null
                    && s_plateCountField != null;
                if (!ok && !s_loggedReflectMiss)
                {
                    s_loggedReflectMiss = true;
                    StubLog.LogWarn("[SinkDirtyPlateVisibility] 反射初始化不完整，显隐校正停用"
                        + "（washing=" + (s_washingStationType != null)
                        + " client=" + (s_clientWashingStationType != null)
                        + " server=" + (s_serverWashingStationType != null)
                        + " serverCount=" + (s_serverPlateCountField != null) + "）");
                }
                // 服务端计数是可选增强：缺失不影响显隐校正主链路。
                return ok;
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[SinkDirtyPlateVisibility] 初始化异常: " + ex.Message);
                return false;
            }
        }
    }
}
