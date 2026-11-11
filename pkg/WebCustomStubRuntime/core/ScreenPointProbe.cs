using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace CustomStub
{
    /// <summary>屏幕点相机调用探针（2026-11-08 真机排障，仅 Verbose 诊断）。
    /// 背景：呆喵喵图（Dmm/s_dmm_10，camera 特征）在非 16:9 显示器上真机每帧刷
    /// 原生错误 "Screen position out of view frustum (screen pos x,y[,z]) (Camera
    /// rect 0 144 2332 1312)"（41 秒 29635 条）——原生错误**没有托管堆栈**，
    /// debugLog 抓不到调用方；且已排除本运行时与游戏 Assembly-CSharp（两者都
    /// 不在逐帧路径调用这些 API）。相机 rect 本身是原版 FixedAspectRatioCamera
    /// 的 16:9 letterbox（2332 宽屏 → 上下各 144px 黑边），属正常行为。
    /// 机制：给 Camera 的屏幕/视口点 API 挂 Harmony 前缀，在「点必然/大概率被
    /// 原生拒绝」（严格越界，或贴边 ≤0.5px——实测错误值恰为 rect 边界：
    /// x=0/xMax、y=yMin+0.125）时上报**托管堆栈**，点名调用方（第三方模组/
    /// 上游 OC2DIYLevel）。前缀只观察不拦截（恒放行原方法），行为零变更；
    /// 本程序集之外的合法调用不受影响。同一调用点只详报一次，之后每 30 秒
    /// 汇总计数，绝不刷屏。
    /// 开关：仅 BepInEx 配置 [Logging] Verbose=true 时安装（EntryPoint.Install
    /// 门控）——默认零开销零介入；排障时开 Verbose 复现一次即可定位。</summary>
    internal static class ScreenPointProbe
    {
        private sealed class ReportState
        {
            public int Count;
            public DateTime LastSummaryUtc;
        }

        private const int MaxStackFrames = 12;
        private const int SummaryIntervalSeconds = 30;

        private static bool s_installed;
        private static bool s_failed;
        private static readonly Dictionary<string, ReportState> s_reports =
            new Dictionary<string, ReportState>();

        /// <summary>幂等安装（EntryPoint.Install 在 Verbose 时调用）。失败只记一次
        /// 警告，绝不影响其他补丁。</summary>
        internal static void EnsureInstalled()
        {
            if (s_installed || s_failed)
                return;
            try
            {
                var harmony = new Harmony("oc2.customstub.screenpointprobe");
                PatchScreenApi(harmony, "ScreenPointToRay");
                PatchScreenApi(harmony, "ScreenToWorldPoint");
                PatchViewportApi(harmony, "ViewportPointToRay");
                PatchViewportApi(harmony, "ViewportToWorldPoint");
                s_installed = true;
                StubLog.Log("[CustomStub] 屏幕点探针已装（Verbose 诊断：越界相机调用将带调用方堆栈上报）");
            }
            catch (Exception ex)
            {
                s_failed = true;
                StubLog.LogWarn("[CustomStub] 屏幕点探针安装失败（仅诊断能力缺失，不影响任何功能）: " + ex.Message);
            }
        }

        private static void PatchScreenApi(Harmony harmony, string methodName)
        {
            var target = typeof(Camera).GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Vector3) }, null);
            if (target == null)
            {
                StubLog.LogWarn("[CustomStub] 屏幕点探针: Camera." + methodName + " 反射失败（跳过）");
                return;
            }
            var prefix = typeof(ScreenPointProbe).GetMethod(methodName + "Prefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (prefix == null)
            {
                StubLog.LogWarn("[CustomStub] 屏幕点探针: 前缀缺失 " + methodName + "（跳过）");
                return;
            }
            harmony.Patch(target, new HarmonyMethod(prefix));
        }

        private static void PatchViewportApi(Harmony harmony, string methodName)
        {
            var target = typeof(Camera).GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Vector3) }, null);
            if (target == null)
            {
                StubLog.LogWarn("[CustomStub] 屏幕点探针: Camera." + methodName + " 反射失败（跳过）");
                return;
            }
            var prefix = typeof(ScreenPointProbe).GetMethod(methodName + "Prefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (prefix == null)
            {
                StubLog.LogWarn("[CustomStub] 屏幕点探针: 前缀缺失 " + methodName + "（跳过）");
                return;
            }
            harmony.Patch(target, new HarmonyMethod(prefix));
        }

        /// <summary>屏幕坐标 API 前缀（只观察）。判定 = 严格越界（±0.5px 容差）或
        /// 贴边 ≤0.5px（实测原生错误恰在 rect 边界值上触发：x=0 / x=xMax /
        /// y=yMin+0.125）。命中即上报调用方堆栈。</summary>
        private static void ScreenPointToRayPrefix(Camera __instance, Vector3 __0)
        {
            CheckScreen(__instance, __0, "Camera.ScreenPointToRay");
        }

        private static void ScreenToWorldPointPrefix(Camera __instance, Vector3 __0)
        {
            CheckScreen(__instance, __0, "Camera.ScreenToWorldPoint");
        }

        private static void CheckScreen(Camera cam, Vector3 p, string api)
        {
            try
            {
                if (cam == null)
                    return;
                var r = cam.pixelRect;
                var outX = p.x < r.xMin - 0.5f || p.x > r.xMax + 0.5f;
                var outY = p.y < r.yMin - 0.5f || p.y > r.yMax + 0.5f;
                var edgeX = Math.Abs(p.x - r.xMin) <= 0.5f || Math.Abs(p.x - r.xMax) <= 0.5f;
                var edgeY = Math.Abs(p.y - r.yMin) <= 0.5f || Math.Abs(p.y - r.yMax) <= 0.5f;
                if (!outX && !outY && !edgeX && !edgeY)
                    return;
                Report(api, cam, p, r, "screen", outX || outY);
            }
            catch
            {
            }
        }

        /// <summary>视口坐标 API 前缀（只观察）。视口合法域 [0,1]，容差 0.002。</summary>
        private static void ViewportPointToRayPrefix(Camera __instance, Vector3 __0)
        {
            CheckViewport(__instance, __0, "Camera.ViewportPointToRay");
        }

        private static void ViewportToWorldPointPrefix(Camera __instance, Vector3 __0)
        {
            CheckViewport(__instance, __0, "Camera.ViewportToWorldPoint");
        }

        private static void CheckViewport(Camera cam, Vector3 p, string api)
        {
            try
            {
                if (cam == null)
                    return;
                if (p.x >= -0.002f && p.x <= 1.002f && p.y >= -0.002f && p.y <= 1.002f)
                    return;
                Report(api, cam, p, cam.pixelRect, "viewport", true);
            }
            catch
            {
            }
        }

        /// <summary>节流上报：同一调用点（堆栈指纹）首报全量（含堆栈），之后每
        /// 30 秒一条计数汇总。</summary>
        private static void Report(string api, Camera cam, Vector3 pos, Rect rect,
            string coordKind, bool strictlyOutside)
        {
            var stack = new StackTrace(2, true);
            var key = api + "@" + StackKey(stack);
            ReportState state;
            lock (s_reports)
            {
                if (!s_reports.TryGetValue(key, out state))
                {
                    state = new ReportState();
                    s_reports[key] = state;
                }
                state.Count++;
                var first = state.Count == 1;
                var due = (DateTime.UtcNow - state.LastSummaryUtc).TotalSeconds >= SummaryIntervalSeconds;
                if (!first && !due)
                    return;
                state.LastSummaryUtc = DateTime.UtcNow;
            }
            var sb = new StringBuilder();
            sb.Append("[CustomStub][屏幕点探针] ").Append(api).Append(" 越界")
                .Append(coordKind == "screen" ? "（屏幕坐标不在相机 pixelRect）" : "（视口坐标不在 [0,1]）")
                .Append(strictlyOutside ? " [严格越界]" : " [贴边]")
                .Append(" pos=").Append(pos.x.ToString("0.###")).Append(",").Append(pos.y.ToString("0.###"))
                .Append(",").Append(pos.z.ToString("0.###"))
                .Append(" rect=").Append(rect.xMin.ToString("0")).Append(",").Append(rect.yMin.ToString("0"))
                .Append(",").Append(rect.width.ToString("0")).Append(",").Append(rect.height.ToString("0"))
                .Append(" far=").Append(cam.farClipPlane.ToString("0"))
                .Append(" 相机=").Append(cam.name)
                .Append(" 场景=").Append(cam.gameObject.scene.name)
                .Append(" 第 ").Append(state.Count).Append(" 次")
                .Append("。调用方堆栈：\n");
            var frames = stack.GetFrames();
            var shown = 0;
            if (frames != null)
            {
                for (int i = 0; i < frames.Length && shown < MaxStackFrames; i++)
                {
                    var m = frames[i].GetMethod();
                    if (m == null)
                        continue;
                    var decl = m.DeclaringType == null ? "?" : m.DeclaringType.FullName;
                    if (decl.StartsWith("CustomStub.ScreenPointProbe", StringComparison.Ordinal))
                        continue;
                    sb.Append("    at ").Append(decl).Append(".").Append(m.Name).Append("\n");
                    shown++;
                }
            }
            if (shown == 0)
                sb.Append("    (无托管帧)");
            StubLog.Log(sb.ToString());
        }

        /// <summary>堆栈指纹：前若干帧的类型.方法名拼接（区分调用点，文件行号不可用）。</summary>
        private static string StackKey(StackTrace stack)
        {
            var sb = new StringBuilder();
            var frames = stack.GetFrames();
            if (frames != null)
            {
                var taken = 0;
                for (int i = 0; i < frames.Length && taken < 4; i++)
                {
                    var m = frames[i].GetMethod();
                    if (m == null)
                        continue;
                    var decl = m.DeclaringType == null ? "?" : m.DeclaringType.FullName;
                    if (decl.StartsWith("CustomStub.ScreenPointProbe", StringComparison.Ordinal))
                        continue;
                    sb.Append(decl).Append(".").Append(m.Name).Append(";");
                    taken++;
                }
            }
            return sb.ToString();
        }

        /// <summary>场景切换清场（计数与节流状态归零，新场景重新首报）。</summary>
        internal static void OnSceneChanged()
        {
            lock (s_reports)
                s_reports.Clear();
        }
    }
}
