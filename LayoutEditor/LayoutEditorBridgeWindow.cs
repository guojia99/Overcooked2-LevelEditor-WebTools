using UnityEditor;
using UnityEngine;

public class LayoutEditorBridgeWindow : EditorWindow
{
    private static LayoutEditorHttpServer _server;
    private static bool _watchdogEnabled;
    private static double _nextWatchdogCheck;
    private static readonly System.Collections.Generic.List<System.DateTime> _restartTimes =
        new System.Collections.Generic.List<System.DateTime>();

    [MenuItem("Tools/Layout Editor/Open Bridge", false, 200)]
    public static void OpenWindow()
    {
        var w = GetWindow<LayoutEditorBridgeWindow>(false, "Layout Editor", true);
        w.minSize = new Vector2(360, 320);
        w.Show();
    }

    [MenuItem("Tools/Layout Editor/Open Web UI", false, 203)]
    public static void OpenWebUi()
    {
        if (!LayoutEditorHttpServer.HasBundledWebUi())
        {
            EditorUtility.DisplayDialog(
                "Layout Editor",
                "未找到 layout-editor/web/dist/index.html。\n请使用仓库内已提交的静态前端，或由维护者执行 npm run build。",
                "确定");
            return;
        }

        var server = EnsureServer();
        if (!server.IsRunning)
            StartServer(server);
        Application.OpenURL(LayoutEditorPaths.WebUiUrlForActiveScene());
    }

    [MenuItem("Tools/Layout Editor/Start Server", false, 201)]
    public static void StartServerMenu()
    {
        StartServer(EnsureServer());
    }

    [MenuItem("Tools/Layout Editor/Stop Server", false, 202)]
    public static void StopServerMenu()
    {
        StopServer();
    }

    private static LayoutEditorHttpServer EnsureServer()
    {
        if (_server == null)
            _server = new LayoutEditorHttpServer();
        return _server;
    }

    /// <summary>Manually start the server and enable the crash watchdog (auto-restart, max 3 per minute).</summary>
    private static void StartServer(LayoutEditorHttpServer server)
    {
        server.Start();
        if (server.IsRunning)
        {
            _watchdogEnabled = true;
            _restartTimes.Clear();
            EditorApplication.update -= WatchdogTick;
            EditorApplication.update += WatchdogTick;
        }
    }

    private static void StopServer()
    {
        _watchdogEnabled = false;
        EditorApplication.update -= WatchdogTick;
        if (_server != null)
            _server.Stop();
    }

    private static void WatchdogTick()
    {
        if (!_watchdogEnabled || _server == null)
            return;
        if (EditorApplication.timeSinceStartup < _nextWatchdogCheck)
            return;
        _nextWatchdogCheck = EditorApplication.timeSinceStartup + 2.0;

        if (!_server.IsRunning)
            return; // stopped intentionally (e.g. port released), not a crash
        if (_server.IsHealthy)
            return;

        var now = System.DateTime.Now;
        _restartTimes.RemoveAll(t => (now - t).TotalSeconds > 60.0);
        if (_restartTimes.Count >= 3)
        {
            _watchdogEnabled = false;
            EditorApplication.update -= WatchdogTick;
            Debug.LogWarning("Layout Editor: 服务异常中断，1 分钟内已自动重启 3 次，停止自动重启。请手动重启服务。");
            return;
        }

        _restartTimes.Add(now);
        Debug.LogWarning("Layout Editor: 检测到服务中断，正在自动重启（" + _restartTimes.Count + "/3）…");
        try
        {
            _server.Stop();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Layout editor watchdog stop: " + ex.Message);
        }
        _server.Start();
        if (!_server.IsRunning)
            Debug.LogWarning("Layout Editor: 自动重启失败。");
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
            Application.OpenURL(LayoutEditorPaths.WebUiUrlForActiveScene());
        if (GUILayout.Button("打开主页", GUILayout.Height(24)))
            Application.OpenURL(LayoutEditorPaths.WebUiUrl + "#/manage");
        GUI.enabled = true;

        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        GUILayout.Space(8f);
        EditorGUILayout.LabelField("当前场景", scene.path);

        GUILayout.Space(8f);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        GUILayout.Space(4f);
        if (GUILayout.Button("导出装饰实测尺寸 (measured-footprints.json)", GUILayout.Height(26)))
            LayoutEditorFootprintDump.Dump();
        if (GUILayout.Button("Export Audio for Web", GUILayout.Height(26)))
            LayoutEditorAudioExporter.ExportAudioForWeb();
    }
}
