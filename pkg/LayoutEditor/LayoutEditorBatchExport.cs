using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 批处理导出入口（命令行 -executeMethod 用，无人工值守流水线）：
///   Unity -batchmode -quit -projectPath &lt;root&gt; -executeMethod LayoutEditorBatchExport.Run -logFile &lt;path&gt;
/// 同步驱动 LayoutEditorSetExporter.StartExport + 反射直调 RunExport（绕过
/// delayCall——批处理下 delayCall 在 executeMethod 返回后才触发，配合 -quit
/// 会被跳过），完成/失败后 EditorApplication.Exit(code)：
///   0=导出 done；1=异常；3=导出结束但状态非 done（看 log 内 [SetExporter] 段）。
/// 用途：runtime DLL 母本更新后的自动「staging→bundle→zip」热修重导
/// （2026-09-25 3.3.3 真机按钮永红残留修复首次使用）。
/// </summary>
public static class LayoutEditorBatchExport
{
    /// <summary>默认导出集（tester_web）。可带命令行参数 -set=&lt;name&gt; 覆盖。</summary>
    public static void Run()
    {
        var exitCode = 0;
        try
        {
            var setName = "tester_web";
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("-set=", StringComparison.Ordinal))
                    setName = arg.Substring("-set=".Length);
            }
            var err = LayoutEditorSetExporter.StartExport(setName, "all");
            if (!string.IsNullOrEmpty(err))
                throw new Exception(err);
            var runExport = typeof(LayoutEditorSetExporter).GetMethod("RunExport",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (runExport == null)
                throw new Exception("未找到 LayoutEditorSetExporter.RunExport（导出器版本不匹配？）");
            runExport.Invoke(null, null);
            var st = LayoutEditorSetExporter.GetStatus();
            Debug.Log("[BatchExport] 结果 set=" + st.setName + " status=" + st.status
                + " zip=" + st.zipFileName + " files=" + st.fileCount
                + (string.IsNullOrEmpty(st.error) ? "" : " error=" + st.error));
            if (st.status != "done")
                exitCode = 3;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            exitCode = 1;
        }
        EditorApplication.Exit(exitCode);
    }
}
