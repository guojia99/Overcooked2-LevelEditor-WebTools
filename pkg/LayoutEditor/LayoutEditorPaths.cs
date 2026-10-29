using System;
using System.IO;
using UnityEngine;

public static class LayoutEditorPaths
{
    // Application.dataPath 只能在 Unity 主线程调用；HTTP 监听线程 fast-path 只读缓存。
    private static volatile string s_webDistRoot;
    private static volatile string s_webPublicRoot;
    private static volatile string s_exportsRoot;

    /// <summary>主线程预计算前端根目录（Start/菜单/导出器调用）。监听线程随后只读缓存。</summary>
    public static void PrecomputeRoots()
    {
        if (string.IsNullOrEmpty(s_webDistRoot))
            s_webDistRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../layout-editor/web/dist"));
        if (string.IsNullOrEmpty(s_webPublicRoot))
            s_webPublicRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../layout-editor/web/public"));
        if (string.IsNullOrEmpty(s_exportsRoot))
            s_exportsRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../exports"));
    }

    /// <summary>监听线程安全读取：未预计算时返回 false，让请求回主线程处理。</summary>
    public static bool TryGetCachedRoots(out string webDistRoot, out string webPublicRoot)
    {
        webDistRoot = s_webDistRoot;
        webPublicRoot = s_webPublicRoot;
        return !string.IsNullOrEmpty(webDistRoot) && !string.IsNullOrEmpty(webPublicRoot);
    }

    /// <summary>监听线程安全读取 exports 根目录（音频/桌台/装饰导出产物，git 忽略）。</summary>
    public static bool TryGetCachedExportsRoot(out string exportsRoot)
    {
        exportsRoot = s_exportsRoot;
        return !string.IsNullOrEmpty(exportsRoot);
    }

    public static string WebDistRoot
    {
        get
        {
            PrecomputeRoots();
            return s_webDistRoot;
        }
    }

    /// <summary>布局编辑器前端源码资源根（web/public）：仅剩构建期目录/图标，导出产物不再写这里。</summary>
    public static string WebPublicRoot
    {
        get
        {
            PrecomputeRoots();
            return s_webPublicRoot;
        }
    }

    /// <summary>Web 前端导出产物根目录（&lt;repo&gt;/exports，git 忽略）：audio / counter-skins / decor。</summary>
    public static string ExportsRoot
    {
        get
        {
            PrecomputeRoots();
            return s_exportsRoot;
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
