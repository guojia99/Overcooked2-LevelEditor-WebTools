using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 写回后 CustomStub 守卫：每次 web 写回（POST /api/scene/layout）成功且场景实际用到
/// stub 组件时，检查关卡集 stub 副本是否与母本同步（漂移比对）、DLL 是否新鲜，
/// 异常时自动同步母本并触发编译（及时打包 runtime bundle），杜绝「写回用的是旧逻辑 /
/// 导出打进过期 DLL」。
///
/// 解耦：经 LayoutEditorHttpServer.CustomStubWriteBackCheck 静态钩子接入
/// （[InitializeOnLoad] 注册），HttpServer 不硬引用 CustomStub 类型；
/// 删除 CustomStub 全部文件（含本文件）后编辑器仍可编译。
///
/// 时序（与 CustomStubHttpApi compile 按钮同款铁律）：检查在应答前同步执行（仅文本比对，
/// 开销极小），写盘/编译一律 delayCall 到应答之后 —— Refresh 引发的域重载不会截断
/// HTTP 响应。域重载后由 CustomStubAutoBake 自动完成 staging；无脚本变动（无域重载）
/// 时由本类的嵌套 delayCall 补一次 StageSet 兜底。
/// </summary>
[InitializeOnLoad]
public static class CustomStubWriteBackGuard
{
    /// <summary>连续写回去抖：待处理的关卡集合并到一次延迟执行里统一重新求值。</summary>
    private static readonly HashSet<string> _pendingSets = new HashSet<string>();
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
            if (string.IsNullOrEmpty(setName))
                return null;
            // 仅当场景实际用到 stub 组件才检查（tag 载体前缀 + CustomStub 命名空间组件）。
            if (!LayoutEditorSetExporter.ActiveSceneUsesCustomStub())
                return null;

            var configured = CustomStubCopyTool.IsConfigured(setName);
            if (!configured)
            {
                // StubIO 烘焙时类型缺失会经 CustomStubCopyRequested 钩子自动拷贝，
                // 这里只提示，不重复触发。
                return Log(setName, "场景使用了 CustomStub 组件，但关卡集尚未拷贝 stub"
                    + "（已由写回链路自动触发拷贝，稍后编译）。");
            }

            if (CustomStubCopyTool.IsDrifted(setName))
            {
                Schedule(setName);
                return Log(setName, "stub 副本与母本漂移，已自动同步母本并重新编译 DLL。");
            }

            var dllState = LayoutStubDllBuilder.GetStageState(setName);
            if (dllState == "missing" || dllState == "stale")
            {
                Schedule(setName);
                return Log(setName, "Stub DLL "
                    + (dllState == "missing" ? "尚未编译" : "过期（源码比 DLL 新）")
                    + "，已触发重新编译+打包。");
            }

            if (BytesStale(setName))
            {
                Schedule(setName);
                return Log(setName, "Stub DLL 已更新但 runtime 包未重打包，已自动 staging。");
            }

            Debug.Log("[CustomStub] 写回检查 " + setName + ": 副本已同步，DLL fresh");
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomStub] 写回检查 " + setName + " 异常: " + ex.Message);
            return null;
        }
    }

    private static string Log(string setName, string warn)
    {
        Debug.Log("[CustomStub] 写回检查 " + setName + ": " + warn);
        return warn;
    }

    /// <summary>runtime 包（.dll.bytes）是否落后于 Library DLL（镜像 StageAllSetsQuiet 的判定）。</summary>
    private static bool BytesStale(string setName)
    {
        var asmName = CustomStubCopyTool.StubAssemblyName(setName);
        var projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
        var dllAbs = projectRoot + "/Library/ScriptAssemblies/" + asmName + ".dll";
        if (!File.Exists(dllAbs))
            return false; // DLL 未编译由 GetStageState=missing 覆盖
        var bytesPath = "Assets/LevelSets/" + setName + "/stub/" + asmName + ".dll.bytes";
        var bytesAbs = Application.dataPath.Replace("Assets", "") + bytesPath;
        if (!File.Exists(bytesAbs))
            return true;
        if (File.GetLastWriteTime(dllAbs) > File.GetLastWriteTime(bytesAbs).AddSeconds(2))
            return true;
        var importer = AssetImporter.GetAtPath(bytesPath);
        return importer != null && importer.assetBundleName != setName + "/runtime";
    }

    private static void Schedule(string setName)
    {
        _pendingSets.Add(setName);
        if (_scheduled)
            return;
        _scheduled = true;
        EditorApplication.delayCall += delegate
        {
            _scheduled = false;
            var sets = new List<string>(_pendingSets);
            _pendingSets.Clear();
            foreach (var set in sets)
                Run(set);
        };
    }

    /// <summary>应答后执行：重新求值（连续写回只跑一趟），漂移则同步母本，
    /// 需要编译则 Refresh（域重载后 AutoBake 自动 staging），否则直接补 staging。</summary>
    private static void Run(string setName)
    {
        try
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                // 编译中再顺延一拍
                Schedule(setName);
                return;
            }
            if (!CustomStubCopyTool.IsConfigured(setName))
                return;

            var needRefresh = false;
            if (CustomStubCopyTool.IsDrifted(setName))
            {
                var result = CustomStubCopyTool.CopyToSet(setName, false);
                Debug.Log("[CustomStub] 写回后自动同步 " + setName + ": " + result);
                needRefresh = true;
            }
            var dllState = LayoutStubDllBuilder.GetStageState(setName);
            if (dllState == "missing" || dllState == "stale")
                needRefresh = true;

            if (needRefresh)
            {
                AssetDatabase.Refresh();
                // 有脚本变动 → 域重载后 CustomStubAutoBake 自动 staging，这里会被丢弃；
                // 无变动（无域重载）→ 补一次 staging 兜底。
                EditorApplication.delayCall += delegate
                {
                    if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                        LayoutStubDllBuilder.StageSet(setName, false);
                };
            }
            else if (BytesStale(setName))
            {
                LayoutStubDllBuilder.StageSet(setName, false);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomStub] 写回后同步/编译 " + setName + " 异常: " + ex.Message);
        }
    }
}
