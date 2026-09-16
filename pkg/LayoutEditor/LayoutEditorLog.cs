using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Writes a persistent debug log next to the layout-editor folder (repo root /logs),
/// so the move-control import/bake data flow can be inspected without the Unity
/// console. Every line is also mirrored to Debug.Log when possible.
/// </summary>
public static class LayoutEditorLog
{
    private static string _path;

    private static string Path_
    {
        get
        {
            if (_path != null) return _path;
            try
            {
                // Application.dataPath == <repo>/Assets -> sibling dir of layout-editor.
                var root = Path.Combine(Application.dataPath, "..");
                var dir = Path.Combine(root, "logs");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                _path = Path.Combine(dir, "layout_editor.log");
            }
            catch (Exception)
            {
                _path = "layout_editor.log";
            }
            return _path;
        }
    }

    public static void Log(string message)
    {
        try
        {
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message;
            File.AppendAllText(Path_, line + "\n");
        }
        catch (Exception)
        {
            // Logging must never break the editor flow.
        }
        Debug.Log("[LayoutEditorLog] " + message);
    }

    public static void LogWarning(string message)
    {
        try
        {
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " WARN " + message;
            File.AppendAllText(Path_, line + "\n");
        }
        catch (Exception)
        {
        }
        Debug.LogWarning("[LayoutEditorLog] " + message);
    }

    // ---- 写回告警收集（/api/set/layout 响应透传给 web 状态栏）----
    // 请求都在主线程泵执行（HttpServer.EnqueueMain），无需加锁。
    // 之前绑定丢弃只进 Unity 日志，用户在 web 端无感知，直到游戏里才发现
    // 脏盘台/饮料机失效。Apply 开始时清空，响应构造时 Drain。
    private static readonly System.Collections.Generic.List<string> _applyWarnings =
        new System.Collections.Generic.List<string>();

    public static void BeginApply()
    {
        _applyWarnings.Clear();
    }

    public static void RecordApplyWarning(string message)
    {
        if (_applyWarnings.Count < 50)
            _applyWarnings.Add(message);
    }

    public static string[] DrainApplyWarnings()
    {
        var arr = _applyWarnings.ToArray();
        _applyWarnings.Clear();
        return arr;
    }
}
