using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 写回后 CustomStub 守卫：每次 web 写回（POST /api/scene/layout）成功且场景实际用到
/// stub 组件时，检查统一运行时 WebCustomStubRuntime 的 DLL 是否新鲜、runtime 包是否已
/// 重打包，异常时自动触发编译打包，杜绝「导出打进过期 runtime」。
///
/// 统一单程序集重构后：不再有「每集副本漂移」概念（母本即唯一运行实体），
/// 只校验统一 runtime 的 DLL/bytes 新鲜度。
///
/// 解耦：经 LayoutEditorHttpServer.CustomStubWriteBackCheck 静态钩子接入
/// （[InitializeOnLoad] 注册），HttpServer 不硬引用 CustomStub 类型；
/// 删除 CustomStub 全部文件（含本文件）后编辑器仍可编译。
///
/// 时序：检查在应答前同步执行（仅文件时间比对，开销极小），编译一律 delayCall 到
/// 应答之后 —— Refresh 引发的域重载不会截断 HTTP 响应；域重载后由 CustomStubAutoBake
/// 自动 StageRuntimeQuiet，无脚本变动时本类补一次 StageRuntime 兜底。
/// </summary>
[InitializeOnLoad]
public static class CustomStubWriteBackGuard
{
    private static bool _scheduled;

    static CustomStubWriteBackGuard()
    {
        LayoutEditorHttpServer.CustomStubWriteBackCheck = Check;
    }

    /// <summary>写回应答前调用（HttpServer apply 分支）。返回告警文案（null = 正常/未用到）。</summary>
    private static string Check(string setName)
    {
        try
        {
            // 仅当场景实际用到 stub 组件才检查（tag 载体前缀 + CustomStub 命名空间组件）。
            if (!LayoutEditorSetExporter.ActiveSceneUsesCustomStub())
                return null;

            var dllState = LayoutStubDllBuilder.GetRuntimeStageState();
            if (dllState == "missing" || dllState == "stale")
            {
                Schedule();
                return Log("统一运行时 DLL "
                    + (dllState == "missing" ? "尚未编译" : "过期（母本源码比 DLL 新）")
                    + "，已触发重新编译打包。");
            }

            if (LayoutStubDllBuilder.StageRuntimeQuiet())
                return Log("统一运行时 DLL 已更新，runtime 包已自动 staging。");

            Debug.Log("[CustomStub] 写回检查: 统一运行时 fresh");
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomStub] 写回检查异常: " + ex.Message);
            return null;
        }
    }

    private static string Log(string warn)
    {
        Debug.Log("[CustomStub] 写回检查: " + warn);
        return warn;
    }

    private static void Schedule()
    {
        if (_scheduled)
            return;
        _scheduled = true;
        EditorApplication.delayCall += delegate
        {
            _scheduled = false;
            Run();
        };
    }

    private static void Run()
    {
        try
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Schedule(); // 编译中再顺延一拍
                return;
            }
            var dllState = LayoutStubDllBuilder.GetRuntimeStageState();
            if (dllState == "missing" || dllState == "stale")
            {
                AssetDatabase.Refresh();
                // 有脚本变动 → 域重载后 CustomStubAutoBake 自动 staging，这里会被丢弃；
                // 无变动（无域重载）→ 补一次 staging 兜底。
                EditorApplication.delayCall += delegate
                {
                    if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                        LayoutStubDllBuilder.StageRuntime(false);
                };
            }
            else
            {
                LayoutStubDllBuilder.StageRuntimeQuiet();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomStub] 写回后编译异常: " + ex.Message);
        }
    }
}
