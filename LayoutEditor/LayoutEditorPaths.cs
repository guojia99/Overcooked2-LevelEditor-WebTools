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
        get { return "http://127.0.0.1:" + LayoutEditorHttpServer.DefaultPort + "/manage"; }
    }

    public static string WebUiUrlForActiveScene()
    {
        var baseUrl = "http://127.0.0.1:" + LayoutEditorHttpServer.DefaultPort;
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        var path = scene.path;
        if (!string.IsNullOrEmpty(path) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            return baseUrl + "/layout?scene=" + Uri.EscapeDataString(path);
        return baseUrl + "/manage";
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
