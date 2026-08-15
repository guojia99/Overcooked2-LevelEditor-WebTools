using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LayoutEditorBridgeWindow : EditorWindow
{
    private static LayoutEditorHttpServer _server;
    private static double _nextWatchdogCheck;
    private static readonly List<DateTime> _restartTimes = new List<DateTime>();

    private const string AutoStartKey = "LayoutEditor.ServerAutoStart";
    private const int MaxRestartsPerMinute = 5;
    private const double WatchdogIntervalSeconds = 2.0;
    private const double MinRestartIntervalSeconds = 2.0;

    [MenuItem("Layout Editor/Open Bridge", false, 200)]
    public static void OpenWindow()
    {
        var w = GetWindow<LayoutEditorBridgeWindow>(false, "Layout Editor", true);
        w.minSize = new Vector2(360, 320);
        w.Show();
    }

    [MenuItem("Layout Editor/Start Server", false, 201)]
    public static void StartServerMenu()
    {
        StartServer(EnsureServer());
    }

    [MenuItem("Layout Editor/Stop Server", false, 202)]
    public static void StopServerMenu()
    {
        StopServer();
    }

    [MenuItem("Layout Editor/清理损坏的预制件实例", false, 203)]
    public static void CleanBrokenPrefabInstancesMenu()
    {
        var n = LayoutEditorSceneRepair.RemoveBrokenPrefabInstances();
        Debug.Log("[LayoutEditor] 清理损坏的预制件实例：" + n + " 个");
    }

    private static LayoutEditorHttpServer EnsureServer()
    {
        if (_server == null)
            _server = new LayoutEditorHttpServer();
        return _server;
    }

    /// <summary>记录"服务应保持开启"的意图（EditorPrefs，跨 domain reload / Editor 重启存活），并立即启动。</summary>
    private static void StartServer(LayoutEditorHttpServer server)
    {
        EditorPrefs.SetBool(AutoStartKey, true);
        _restartTimes.Clear();
        _nextWatchdogCheck = 0;
        if (server.Start())
        {
            Debug.Log("Layout Editor: 服务已启动，自动保活已开启。");
        }
        else
        {
            Debug.LogWarning("Layout Editor: 服务启动失败，看门狗将自动重试。");
        }
    }

    private static void StopServer()
    {
        EditorPrefs.DeleteKey(AutoStartKey);
        _restartTimes.Clear();
        if (_server != null)
            _server.Stop();
    }

    /// <summary>
    /// 看门狗：只要"自动保活"开启，就保证服务一直运行。
    /// 中断时自动重启，限流：60 秒窗口内最多 5 次、相邻重启间隔 ≥2 秒；超限后暂停，窗口滑动后自动恢复重试，不会永久放弃。
    /// </summary>
    internal static void WatchdogTick()
    {
        if (EditorApplication.timeSinceStartup < _nextWatchdogCheck)
            return;
        _nextWatchdogCheck = EditorApplication.timeSinceStartup + WatchdogIntervalSeconds;

        if (!EditorPrefs.GetBool(AutoStartKey, false))
            return;

        var server = EnsureServer();
        if (server.IsRunning && server.IsHealthy)
            return;

        var now = DateTime.Now;
        _restartTimes.RemoveAll(t => (now - t).TotalSeconds > 60.0);
        if (_restartTimes.Count >= MaxRestartsPerMinute)
            return;
        if (_restartTimes.Count > 0 &&
            (now - _restartTimes[_restartTimes.Count - 1]).TotalSeconds < MinRestartIntervalSeconds)
            return;

        _restartTimes.Add(now);
        Debug.LogWarning("Layout Editor: 检测到服务中断，正在自动重启（" + _restartTimes.Count + "/" + MaxRestartsPerMinute + " 每分钟）…");
        try
        {
            if (server.IsRunning)
                server.Stop();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Layout editor watchdog stop: " + ex.Message);
        }
        if (!server.Start())
            Debug.LogWarning("Layout Editor: 自动重启失败，稍后自动重试。");
    }

    private void OnEnable()
    {
        EnsureServer();
    }

    private void OnGUI()
    {
        var server = EnsureServer();
        var hasStatic = LayoutEditorPaths.IsWebDistReady();
        var uiUrl = LayoutEditorPaths.WebUiUrl;

        EditorGUILayout.LabelField(server.IsRunning ? uiUrl : "已停止");
        EditorGUILayout.LabelField(EditorPrefs.GetBool(AutoStartKey, false) ? "自动保活：已开启" : "自动保活：已停止");

        GUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = !server.IsRunning;
        if (GUILayout.Button("启动服务", GUILayout.Height(28)))
            StartServer(server);
        GUI.enabled = server.IsRunning;
        if (GUILayout.Button("停止服务", GUILayout.Height(28)))
            StopServer();
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        GUI.enabled = server.IsRunning && hasStatic;
        if (GUILayout.Button("在浏览器中打开编排页", GUILayout.Height(32)))
        {
            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            var url2 = LayoutEditorPaths.WebUiUrl;
            if (!string.IsNullOrEmpty(activeScene.path) && activeScene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                url2 += "?scene=" + Uri.EscapeDataString(activeScene.path);
            else
                url2 += "#/manage";
            Application.OpenURL(url2);
        }
        if (GUILayout.Button("打开主页", GUILayout.Height(24)))
            Application.OpenURL(LayoutEditorPaths.WebUiUrl + "#/manage");
        GUI.enabled = true;

        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        GUILayout.Space(8f);
        EditorGUILayout.LabelField("当前场景", scene.path);

        GUILayout.Space(8f);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        GUILayout.Space(4f);
        if (GUILayout.Button("导出装饰实测尺寸", GUILayout.Height(26)))
            LayoutEditorFootprintDump.Dump();
        if (GUILayout.Button("导出音频依赖", GUILayout.Height(26)))
            LayoutEditorAudioExporter.ExportAudioForWeb();
        if (GUILayout.Button("导出 Bundle 全部内容", GUILayout.Height(26)))
            LayoutEditorBundleDumper.Dump();
        if (GUILayout.Button("导出全部素材图", GUILayout.Height(26)))
            LayoutEditorImageExporter.Export();
    }
}

/// <summary>
/// 每次 domain reload（脚本重编译、进入播放模式、Unity 启动时编译）后自动重新注册看门狗，
/// 保证"自动保活"不因静态状态被清空而失效。
/// </summary>
[InitializeOnLoad]
internal static class LayoutEditorServerLifecycle
{
    static LayoutEditorServerLifecycle()
    {
        EditorApplication.update += LayoutEditorBridgeWindow.WatchdogTick;
    }
}
