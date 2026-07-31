using UnityEditor;
using UnityEngine;

public class LayoutEditorBridgeWindow : EditorWindow
{
    private static LayoutEditorHttpServer _server;

    [MenuItem("Tools/Layout Editor/Open Bridge", false, 200)]
    public static void OpenWindow()
    {
        var w = GetWindow<LayoutEditorBridgeWindow>(false, "Layout Editor", true);
        w.minSize = new Vector2(360, 220);
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
            server.Start();
        Application.OpenURL(LayoutEditorPaths.WebUiUrlForActiveScene());
    }

    [MenuItem("Tools/Layout Editor/Start Server", false, 201)]
    public static void StartServerMenu()
    {
        EnsureServer().Start();
    }

    [MenuItem("Tools/Layout Editor/Stop Server", false, 202)]
    public static void StopServerMenu()
    {
        if (_server != null)
            _server.Stop();
    }

    private static LayoutEditorHttpServer EnsureServer()
    {
        if (_server == null)
            _server = new LayoutEditorHttpServer();
        return _server;
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
            server.Start();
        GUI.enabled = server.IsRunning;
        if (GUILayout.Button("停止服务", GUILayout.Height(28)))
            server.Stop();
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        GUI.enabled = server.IsRunning && hasStatic;
        if (GUILayout.Button("在浏览器中打开编排页", GUILayout.Height(32)))
            Application.OpenURL(LayoutEditorPaths.WebUiUrlForActiveScene());
        GUI.enabled = true;

        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        GUILayout.Space(8f);
        EditorGUILayout.LabelField("当前场景", scene.path);
    }
}
