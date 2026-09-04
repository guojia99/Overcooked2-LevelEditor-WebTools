using System;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// CustomStub 共享日志桥（RandomCrate 之外的新 stub 组件共用）。
    ///
    /// stub 程序集（Stub_&lt;set&gt;）不能引用 BepInEx，Debug.Log 在游戏侧可能被
    /// BepInEx 日志级别过滤——优先反射转发到 OC2LevelRuntimeLoader 的
    /// LogFromCrate(string, bool)（与 loader 同一来源输出到 LogOutput.log），
    /// 无加载器（编辑器宿主 Play）时回落 Unity Console。
    /// </summary>
    internal static class StubLog
    {
        private static MethodInfo s_bridge;
        private static bool s_searched;

        internal static void Log(string msg)
        {
            Bridge(msg, false);
        }

        internal static void LogWarn(string msg)
        {
            Bridge(msg, true);
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
                    s_bridge.Invoke(null, new object[] { msg, warn });
                    return;
                }
                catch (Exception)
                {
                    // 桥失败回落 Debug
                }
            }
            if (warn)
                Debug.LogWarning(msg);
            else
                Debug.Log(msg);
        }
    }
}
