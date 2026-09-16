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
        {
            // Assets/LevelSets/<set>/scenes/<scene>.unity → 严格路由 /layout/<set>/<scene>
            var parts = path.Replace('\\', '/').Split('/');
            if (parts.Length >= 5 && parts[parts.Length - 4] == "LevelSets" && parts[parts.Length - 2] == "scenes")
            {
                var setName = parts[parts.Length - 3];
                var sceneName = parts[parts.Length - 1];
                sceneName = sceneName.Substring(0, sceneName.Length - ".unity".Length);
                if (!string.IsNullOrEmpty(setName) && !string.IsNullOrEmpty(sceneName))
                    return baseUrl + "/layout/" + Uri.EscapeDataString(setName) + "/" + Uri.EscapeDataString(sceneName);
            }
        }
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
