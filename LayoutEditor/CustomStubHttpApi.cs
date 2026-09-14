using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CustomStub 的 web API 桥：导出弹窗的「编译 Runtime DLL」按钮 + 状态查询。
///
/// 统一单程序集重构后：不再有「拷贝到关卡集/同步更新」（每集副本链已废除）；
/// 只保留全局统一运行时 WebCustomStubRuntime 的状态查询与编译打包。
///
/// 解耦：经 LayoutEditorHttpServer.CustomStubApi 静态钩子接入（[InitializeOnLoad] 注册），
/// HttpServer 不硬引用 CustomStub 类型；删除 CustomStub 全部文件（含本文件）后编辑器仍可编译。
///
/// 时序要点：compile 写盘后不立即 AssetDatabase.Refresh()（脚本变动会引发域重载，
/// 截断 HTTP 响应）——先返回 JSON，再 delayCall 触发 Refresh；域重载后由
/// CustomStubAutoBake 自动 StageRuntimeQuiet，无脚本变动时本类嵌套 delayCall 补一次
/// StageRuntime 兜底。
/// </summary>
[InitializeOnLoad]
public static class CustomStubHttpApi
{
    static CustomStubHttpApi()
    {
        LayoutEditorHttpServer.CustomStubApi = Handle;
    }

    /// <summary>action: status / compile（copy 已废除，兼容旧调用返回提示）。返回 JSON。</summary>
    private static string Handle(string action, string setName)
    {
        try
        {
            switch (action)
            {
                case "status":
                    return StatusJson();
                case "compile":
                    return CompileJson();
                case "copy":
                    // 每集副本链已废除：拷贝语义不再存在，直接回落为编译统一 runtime。
                    return CompileJson();
                default:
                    return "{\"ok\":false,\"error\":\"unknown action: " + Esc(action) + "\"}";
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return "{\"ok\":false,\"error\":\"" + Esc(ex.Message) + "\"}";
        }
    }

    private static string StatusJson()
    {
        var dllState = LayoutStubDllBuilder.GetRuntimeStageState();
        // configured/drifted 语义在单程序集下恒定：runtime 母本始终存在、无每集漂移。
        return "{\"ok\":true"
            + ",\"configured\":true"
            + ",\"drifted\":false"
            + ",\"dllState\":\"" + dllState + "\""
            + ",\"asmName\":\"" + LayoutStubDllBuilder.RuntimeAsmName + "\""
            + "}";
    }

    /// <summary>编译打包统一运行时（一键：先应答 HTTP，再 delayCall Refresh 触发编译；
    /// 无脚本变动时补一次 StageRuntime 兜底）。</summary>
    private static string CompileJson()
    {
        EditorApplication.delayCall += delegate
        {
            AssetDatabase.Refresh();
            EditorApplication.delayCall += delegate
            {
                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                    LayoutStubDllBuilder.StageRuntime(false);
            };
        };
        return "{\"ok\":true,\"reloading\":true,\"message\":\"正在编译打包统一运行时 WebCustomStubRuntime\"}";
    }

    private static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }
}
