using System;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器 Play 下跳过 Steam 成就初始化（CSteamworks 原生库在编辑器不存在）。
///
/// 背景：点击 Play 后宿主链路
///   PseudoPrefabManager.OnEnable → KitchenBootstrapManager.EnsureSetup
///   → PCSaveManager.BootstrapAwake → LoadProfile 协程 → MetaGameProgress.ByteLoad
///   → OvercookedAchievementManager.Init → SteamAchievementManager.Init
///   → SteamUserStats.RequestCurrentStats
/// 在编辑器抛出 DllNotFoundException: CSteamworks，协程首帧中断并刷红字（Play 卡顿）。
///
/// 处置：编辑器侧 Harmony 前缀跳过 OvercookedAchievementManager.Init。宿主文件只读，
/// 真机/导出不经过本守卫（Assets/Editor 只在编辑器编译），因此对成就不产生影响。
/// 与 JiaMapPlaySupport 的同类跳过 patch 兼容：重复前缀无害，退出 Play 各自卸载互不影响。
/// </summary>
[InitializeOnLoad]
internal static class LayoutEditorSteamPlayGuard
{
    private const string HarmonyId = "oc2.layouteditor.steamguard";

    private static bool _loggedSkip;

    static LayoutEditorSteamPlayGuard()
    {
        try
        {
            MethodInfo target = typeof(OvercookedAchievementManager).GetMethod(
                "Init", BindingFlags.Public | BindingFlags.Instance);
            if (target == null)
            {
                LayoutEditorLog.LogWarning("[LayoutEditor] Steam 守卫未命中: OvercookedAchievementManager.Init");
                return;
            }
            MethodInfo prefix = typeof(LayoutEditorSteamPlayGuard).GetMethod(
                "SkipSteamInit", BindingFlags.NonPublic | BindingFlags.Static);
            if (prefix == null)
            {
                LayoutEditorLog.LogWarning("[LayoutEditor] Steam 守卫未命中: SkipSteamInit");
                return;
            }
            Harmony harmony = new Harmony(HarmonyId);
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[LayoutEditor] Steam 守卫安装异常: " + ex);
        }
    }

    /// <summary>Play 中跳过 Steam 成就初始化；编辑模式放行（宿主只在 Play 调用）。</summary>
    private static bool SkipSteamInit()
    {
        if (!Application.isPlaying)
            return true;
        if (!_loggedSkip)
        {
            _loggedSkip = true;
            LayoutEditorLog.Log("[LayoutEditor] 编辑器 Play 跳过 Steam 成就初始化（CSteamworks 原生库不可用）");
        }
        return false;
    }
}
