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
}
