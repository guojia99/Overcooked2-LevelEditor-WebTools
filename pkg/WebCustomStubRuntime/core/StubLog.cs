using System;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// CustomStub 共享日志桥（全部 stub 组件统一出口）。
    ///
    /// 日志格式（排查三要素：时间戳 / 是否 loader 通道 / 哪个 stub）：
    ///  - 游戏侧（有 loader）：消息加 [Stub:&lt;程序集名&gt;] 前缀后经 loader 的
    ///    LogFromCrate 桥转发，loader 再补 [HH:mm:ss.fff][主机|客机|单机] 前缀——
    ///    最终行形如：
    ///    [Info: OC2 LevelRuntime Loader] [22:31:04.512][客机] [Stub:Stub_tianxin][RandomCrate] 消息
    ///    （有 [Stub:...] 段 = stub 桥接日志；没有 = loader 原生日志）
    ///  - 编辑器宿主（无 loader）：回落 Debug.Log，自带 [HH:mm:ss.fff] 时间戳
    ///    （程序集名=CustomStub）。
    ///
    /// 刷屏约定：热路径（Update/ticker/Harmony 前缀）只允许「一次性初始化」、
    /// 「状态迁移」、「异常（once-flag/限量）」三类日志。
    ///
    /// 日志分级（v12，2026-09-14 性能审查）：
    ///  - <see cref="Log"/> / <see cref="LogWarn"/>：常开。只放「安装与补丁装载结果、
    ///    版本金丝雀、场景自愈汇总、玩法级状态迁移、全部告警」。
    ///  - <see cref="Dbg"/>：诊断级，默认【关闭】。反射自检、逐物体自愈明细、
    ///    掷骰/取出、装配明细、扫描摘要等验证期才需要的内容一律走这里。
    ///    开启方式（任一）：
    ///      · 环境变量 OC2_STUB_VERBOSE=1（编辑器与真机通用）；
    ///      · loader 的 BepInEx 配置 [Logging] Verbose = true
    ///        （反射读 OC2LevelRuntimeLoader.LevelRuntimeLoader.VerboseEnabled，
    ///         stub 侧零编译依赖）。
    ///    热路径调用点应先用 <c>if (StubLog.Verbose)</c> 包住，避免字符串拼接开销。
    /// </summary>
    internal static class StubLog
    {
        private static MethodInfo s_bridge;
        private static bool s_searched;
        private static bool s_bridgeFailReported;
        private static string s_stubId;

        /// <summary>-1 = 未决；0 = 关；1 = 开。首次使用时解析一次（BepInEx 配置在
        /// 插件 Awake 就已就绪，早于本程序集加载）。</summary>
        private static int s_verbose = -1;

        /// <summary>诊断日志是否开启（默认关）。</summary>
        internal static bool Verbose
        {
            get
            {
                if (s_verbose < 0)
                    s_verbose = ResolveVerbose() ? 1 : 0;
                return s_verbose == 1;
            }
        }

        private static bool ResolveVerbose()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("OC2_STUB_VERBOSE");
                if (!string.IsNullOrEmpty(env))
                {
                    env = env.Trim();
                    if (env == "1" || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (env == "0" || string.Equals(env, "false", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch (Exception)
            {
                // 沙箱/权限受限环境读不到环境变量：按 loader 配置继续判定
            }
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm == null)
                        continue;
                    var t = asm.GetType("OC2LevelRuntimeLoader.LevelRuntimeLoader", false);
                    if (t == null)
                        continue;
                    var p = t.GetProperty("VerboseEnabled", BindingFlags.Public | BindingFlags.Static);
                    if (p == null)
                        return false;
                    var v = p.GetValue(null, null);
                    return v is bool && (bool)v;
                }
            }
            catch (Exception)
            {
                // loader 缺失/旧版（无该属性）：保持默认关闭
            }
            return false;
        }

        /// <summary>当前 stub 程序集标识（Stub_&lt;set&gt;；编辑器宿主=CustomStub）。
        /// 多关卡集并存时区分日志来源的唯一依据。</summary>
        internal static string StubId
        {
            get
            {
                if (s_stubId == null)
                {
                    try { s_stubId = typeof(StubLog).Assembly.GetName().Name; }
                    catch { s_stubId = "?"; }
                    if (string.IsNullOrEmpty(s_stubId))
                        s_stubId = "?";
                }
                return s_stubId;
            }
        }

        internal static void Log(string msg)
        {
            Bridge(msg, false);
        }

        internal static void LogWarn(string msg)
        {
            Bridge(msg, true);
        }

        internal static void Player(string msg)
        {
            Bridge("[PLAYER] " + msg, true);
        }

        /// <summary>诊断日志（默认关闭，见类头注释）。</summary>
        internal static void Dbg(string msg)
        {
            if (Verbose)
                Bridge(msg, false);
        }

        /// <summary>桥路径前缀：只标 stub 身份（时间戳+角色由 loader 统一补，
        /// 避免双重前缀）。</summary>
        private static string Stamp(string msg)
        {
            return "[Stub:" + StubId + "] " + msg;
        }

        /// <summary>回落路径（编辑器/桥失败）：自带时间戳。</summary>
        private static string FallbackStamp(string msg)
        {
            return "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "]" + Stamp(msg);
        }

        private static void Bridge(string msg, bool warn)
        {
            if (!s_searched)
            {
                s_searched = true;
                try
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm == null)
                            continue;
                        var t = asm.GetType("OC2LevelRuntimeLoader.LevelRuntimeLoader", false);
                        if (t == null)
                            continue;
                        s_bridge = t.GetMethod("LogFromCrate", BindingFlags.Public | BindingFlags.Static);
                        break;
                    }
                }
                catch (Exception)
                {
                    s_bridge = null;
                }
            }
            if (s_bridge != null)
            {
                try
                {
                    s_bridge.Invoke(null, new object[] { Stamp(msg), warn });
                    return;
                }
                catch (Exception)
                {
                    // 桥失败回落 Debug；只报一次，避免每条日志都翻倍
                    if (!s_bridgeFailReported)
                    {
                        s_bridgeFailReported = true;
                        Debug.LogWarning(FallbackStamp("[StubLog] loader 日志桥调用失败，后续日志回落 Unity Console"));
                    }
                }
            }
            if (warn)
                Debug.LogWarning(FallbackStamp(msg));
            else
                Debug.Log(FallbackStamp(msg));
        }
    }
}
