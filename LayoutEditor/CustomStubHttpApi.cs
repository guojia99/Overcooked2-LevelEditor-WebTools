using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CustomStub 的 web API 桥：导出弹窗的「拷贝到关卡集 / 同步更新 / 编译 Stub DLL」按钮。
///
/// 解耦：经 LayoutEditorHttpServer.CustomStubApi 静态钩子接入（[InitializeOnLoad] 注册），
/// HttpServer 不硬引用 CustomStub 类型；删除 CustomStub 全部文件（含本文件）后编辑器仍可编译。
///
/// 时序要点：copy/compile 写盘后不立即 AssetDatabase.Refresh()（脚本变动会引发域重载，
/// 截断 HTTP 响应）——先返回 JSON，再 delayCall 触发 Refresh。域重载后由 CustomStubAutoBake
/// 自动完成 SyncAllDrifted + StageAllSetsQuiet（无需本类再做编排）；无脚本变动（无域重载）
/// 时由本类的嵌套 delayCall 补一次 StageSet 兜底。
/// </summary>
[InitializeOnLoad]
public static class CustomStubHttpApi
{
    static CustomStubHttpApi()
    {
        LayoutEditorHttpServer.CustomStubApi = Handle;
    }

    /// <summary>action: status / copy / compile。返回 JSON 字符串（HTTP 200 的 body）。</summary>
    private static string Handle(string action, string setName)
    {
        if (string.IsNullOrEmpty(setName))
            return "{\"ok\":false,\"error\":\"缺少 setName\"}";
        try
        {
            switch (action)
            {
                case "status":
                    return StatusJson(setName);
                case "copy":
                    return CopyJson(setName, false);
                case "compile":
                    return CopyJson(setName, true);
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

    private static string StatusJson(string setName)
    {
        var configured = CustomStubCopyTool.IsConfigured(setName);
        var drifted = configured && CustomStubCopyTool.IsDrifted(setName);
        var dllState = LayoutStubDllBuilder.GetStageState(setName);
        return "{\"ok\":true"
            + ",\"configured\":" + (configured ? "true" : "false")
            + ",\"drifted\":" + (drifted ? "true" : "false")
            + ",\"dllState\":\"" + dllState + "\""
            + ",\"asmName\":\"" + CustomStubCopyTool.StubAssemblyName(setName) + "\""
            + "}";
    }

    /// <summary>拷贝/同步母本（幂等）；withCompile=true 时随后触发编译+staging（一键全链路）。</summary>
    private static string CopyJson(string setName, bool withCompile)
    {
        // refresh=false：先应答 HTTP，Refresh 由 delayCall 触发（域重载不截断响应）。
        var result = CustomStubCopyTool.CopyToSet(setName, false);
        Debug.Log("[CustomStub] web " + (withCompile ? "编译" : "拷贝/同步") + " " + setName + ": " + result);
        var captured = setName;
        EditorApplication.delayCall += delegate
        {
            AssetDatabase.Refresh();
            if (!withCompile)
                return;
            // 有脚本变动 → 域重载后 CustomStubAutoBake 自动 staging，这里会被丢弃；
            // 无变动（无域重载）→ 补一次 staging，保证「编译 Stub DLL」按钮幂等可重按。
            EditorApplication.delayCall += delegate
            {
                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                    LayoutStubDllBuilder.StageSet(captured, false);
            };
        };
        return "{\"ok\":true,\"reloading\":true,\"message\":\"" + Esc(result) + "\"}";
    }

    private static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }
}
