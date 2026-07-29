using System;
using System.IO;
using UnityEngine;

public static class LayoutEditorPaths
{
    public static string WebDistRoot
    {
        get
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../layout-editor/web/dist"));
        }
    }

    public static bool IsWebDistReady()
    {
        var root = WebDistRoot;
        return File.Exists(Path.Combine(root, "index.html"));
    }

    public static string WebUiUrl
    {
        get { return "http://127.0.0.1:" + LayoutEditorHttpServer.DefaultPort + "/"; }
    }

    public static bool IsPathUnderRoot(string filePath, string rootDirectory)
    {
        var root = Path.GetFullPath(rootDirectory);
        if (!root.EndsWith(Path.DirectorySeparatorChar.ToString()))
            root += Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(filePath);
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
