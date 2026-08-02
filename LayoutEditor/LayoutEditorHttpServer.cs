using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class LayoutEditorHttpServer
{
    public const int DefaultPort = 8765;

    private HttpListener _listener;
    private Thread _listenerThread;
    private volatile bool _running;
    private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
    private readonly object _queueLock = new object();

    public bool IsRunning
    {
        get { return _running; }
    }

    /// <summary>True when running and the listener thread is alive (false = unexpected interruption).</summary>
    public bool IsHealthy
    {
        get
        {
            return _running
                && _listener != null
                && _listener.IsListening
                && _listenerThread != null
                && _listenerThread.IsAlive;
        }
    }

    public int Port { get; private set; }

    public static bool HasBundledWebUi()
    {
        return LayoutEditorPaths.IsWebDistReady();
    }

    public void Start(int port = DefaultPort)
    {
        if (_running)
            return;

        Port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
        _listener.Prefixes.Add("http://localhost:" + port + "/");
        _listener.Start();
        _running = true;

        if (!LayoutEditorPaths.IsWebDistReady())
        {
            Debug.LogWarning(
                "Layout Editor: 未找到静态前端 layout-editor/web/dist/index.html。" +
                "请使用仓库内已提交的 dist，或由维护者执行 npm run build 后提交。");
        }
        else
        {
            Debug.Log("Layout Editor: 静态前端 " + LayoutEditorPaths.WebUiUrl);
        }

        _listenerThread = new Thread(ListenLoop) { IsBackground = true };
        _listenerThread.Start();

        EditorApplication.update -= PumpMainThread;
        EditorApplication.update += PumpMainThread;
    }

    public void Stop()
    {
        if (!_running)
            return;

        _running = false;
        try
        {
            if (_listener != null)
            {
                _listener.Stop();
                _listener.Close();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Layout editor server stop: " + ex.Message);
        }

        _listener = null;
        EditorApplication.update -= PumpMainThread;
    }

    private void ListenLoop()
    {
        while (_running)
        {
            try
            {
                var context = _listener.GetContext();
                var captured = context;
                EnqueueMain(delegate { HandleRequest(captured); });
            }
            catch (HttpListenerException)
            {
                if (!_running)
                    break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (_running)
                    Debug.LogWarning("Layout editor HTTP: " + ex.Message);
            }
        }
    }

    private void EnqueueMain(Action action)
    {
        lock (_queueLock)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }

    private void PumpMainThread()
    {
        while (true)
        {
            Action action = null;
            lock (_queueLock)
            {
                if (_mainThreadQueue.Count == 0)
                    break;
                action = _mainThreadQueue.Dequeue();
            }

            try
            {
                if (action != null)
                    action();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (request.HttpMethod == "OPTIONS")
        {
            WriteText(response, 204, "");
            return;
        }

        try
        {
            var path = request.Url.AbsolutePath;
            if (path == "/api/health")
            {
                var staticReady = LayoutEditorPaths.IsWebDistReady();
                WriteJson(response, 200,
                    "{\"ok\":true,\"port\":" + Port + ",\"static\":" + (staticReady ? "true" : "false") +
                    ",\"recipeApi\":true" +
                    ",\"schemaVersion\":" + LayoutEditorRecipeKnowledge.BridgeSchemaVersion +
                    ",\"knowledgeLoaded\":" + (LayoutEditorRecipeKnowledge.KnowledgeFileLoaded ? "true" : "false") +
                    ",\"dictionaryLoaded\":" + (LayoutEditorManualLookup.DictionaryLoaded ? "true" : "false") +
                    "}");
                return;
            }

            if (path == "/api/level-sets" && request.HttpMethod == "GET")
            {
                WriteJson(response, 200, LayoutEditorJson.ToJson(ScanLevelSetScenes()));
                return;
            }

            if (path == "/api/grid" && request.HttpMethod == "GET")
            {
                WriteJson(response, 200, LayoutEditorJson.ToJson(LayoutEditorGridReader.ReadFromActiveScene()));
                return;
            }

            if (path == "/api/catalog/ingredients" && request.HttpMethod == "GET")
            {
                WriteJson(response, 200, LayoutEditorJson.ToJson(LayoutEditorCatalogApi.ScanIngredients()));
                return;
            }

            if (path == "/api/catalog/floor-materials" && request.HttpMethod == "GET")
            {
                var levelSet = request.QueryString["levelSet"] ?? string.Empty;
                WriteJson(response, 200, LayoutEditorJson.ToJson(LayoutEditorFloorMaterialsApi.Scan(levelSet)));
                return;
            }

            if (path == "/api/recipes" && request.HttpMethod == "GET")
            {
                var levelSet = request.QueryString["levelSet"] ?? string.Empty;
                WriteJson(response, 200, LayoutEditorJson.ToJson(LayoutEditorCatalogApi.ScanRecipes(levelSet)));
                return;
            }

            if (path == "/api/level-recipes" && request.HttpMethod == "GET")
            {
                var assetPath = request.QueryString["assetPath"];
                if (!string.IsNullOrEmpty(assetPath))
                    OpenSceneIfNeeded(assetPath);
                WriteJson(response, 200, LayoutEditorJson.ToJson(LayoutEditorCatalogApi.GetLevelRecipes(assetPath)));
                return;
            }

            if (path == "/api/level-recipes" && request.HttpMethod == "POST")
            {
                var body = ReadBody(request);
                var update = JsonUtility.FromJson<LevelRecipesUpdateDto>(body);
                var recipeErr = LayoutEditorCatalogApi.SetLevelRecipes(update);
                if (!string.IsNullOrEmpty(recipeErr))
                {
                    WriteJson(response, 400, LayoutEditorJson.ToJson(new ApiErrorDto { error = recipeErr }));
                    return;
                }

                WriteJson(response, 200, "{\"ok\":true}");
                return;
            }

            if (path == "/api/scene/layout" && request.HttpMethod == "GET")
            {
                var assetPath = request.QueryString["assetPath"];
                if (!string.IsNullOrEmpty(assetPath))
                    OpenSceneIfNeeded(assetPath);

                var doc = SceneLayoutExporter.ExportActiveScene();
                WriteJson(response, 200, LayoutEditorJson.ToJson(doc));
                return;
            }

            if (path == "/api/scene/layout" && request.HttpMethod == "POST")
            {
                var body = ReadBody(request);
                var doc = LayoutEditorJson.ParseLayoutDocument(body);
                var snap = 1.2f;
                var snapStr = request.QueryString["snap"];
                if (!string.IsNullOrEmpty(snapStr))
                    float.TryParse(snapStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out snap);

                bool syncWalkable = request.QueryString["syncWalkable"] == "1";
                var err = SceneLayoutApplier.Apply(doc, snap, syncWalkable);
                if (!string.IsNullOrEmpty(err))
                {
                    WriteJson(response, 400, LayoutEditorJson.ToJson(new ApiErrorDto { error = err }));
                    return;
                }

                WriteJson(response, 200, "{\"ok\":true}");
                return;
            }

            if (path == "/api/catalog/music" && request.HttpMethod == "GET")
            {
                WriteJson(response, 200, LayoutEditorJson.ToJson(LayoutEditorLevelAdminApi.ScanMusic()));
                return;
            }

            if (path == "/api/catalog/audio-directories" && request.HttpMethod == "GET")
            {
                WriteJson(response, 200, LayoutEditorJson.ToJson(LayoutEditorLevelAdminApi.ScanAudioDirectories()));
                return;
            }

            if (path == "/api/catalog/ambiences" && request.HttpMethod == "GET")
            {
                WriteJson(response, 200, LayoutEditorJson.ToJson(LayoutEditorLevelAdminApi.ScanAmbiences()));
                return;
            }

            if (path == "/api/catalog/death-effects" && request.HttpMethod == "GET")
            {
                WriteJson(response, 200, LayoutEditorJson.ToJson(LayoutEditorLevelAdminApi.ScanDeathEffects()));
                return;
            }

            if (path == "/api/sets" && request.HttpMethod == "GET")
            {
                WriteJson(response, 200, LayoutEditorJson.ToJson(LayoutEditorLevelAdminApi.ScanSets()));
                return;
            }

            if (path.StartsWith("/api/sets/", StringComparison.Ordinal) && path.EndsWith("/levels", StringComparison.Ordinal) && request.HttpMethod == "GET")
            {
                var rest = path.Substring("/api/sets/".Length);
                var setName = Uri.UnescapeDataString(rest.Substring(0, rest.Length - "/levels".Length));
                WriteJson(response, 200, LayoutEditorJson.ToJson(LayoutEditorLevelAdminApi.ScanLevels(setName)));
                return;
            }

            if (path == "/api/set/create" && request.HttpMethod == "POST")
            {
                var body = ReadBody(request);
                var dto = JsonUtility.FromJson<LevelSetCreateDto>(body);
                var err = LayoutEditorLevelAdminApi.CreateSet(dto);
                WriteAdminResult(response, err);
                return;
            }

            if (path == "/api/set/info" && request.HttpMethod == "POST")
            {
                var body = ReadBody(request);
                var dto = JsonUtility.FromJson<LevelSetInfoUpdateDto>(body);
                var err = LayoutEditorLevelAdminApi.UpdateSetInfo(dto);
                WriteAdminResult(response, err);
                return;
            }

            if (path == "/api/set/delete" && request.HttpMethod == "POST")
            {
                var body = ReadBody(request);
                var dto = JsonUtility.FromJson<LevelSetCreateDto>(body);
                var err = LayoutEditorLevelAdminApi.DeleteSet(dto != null ? dto.setName : null);
                WriteAdminResult(response, err);
                return;
            }

            if (path == "/api/level" && request.HttpMethod == "GET")
            {
                var assetPath = request.QueryString["assetPath"];
                WriteJson(response, 200, LayoutEditorJson.ToJson(LayoutEditorLevelAdminApi.GetLevel(assetPath)));
                return;
            }

            if (path == "/api/level/bundles" && request.HttpMethod == "GET")
            {
                var assetPath = request.QueryString["assetPath"];
                WriteJson(response, 200, LayoutEditorJson.ToJson(LayoutEditorLevelAdminApi.AnalyzeBundles(assetPath)));
                return;
            }

            if (path == "/api/level/delete-preview" && request.HttpMethod == "GET")
            {
                var setName = request.QueryString["setName"];
                var levelId = request.QueryString["levelId"];
                WriteJson(response, 200, LayoutEditorJson.ToJson(LayoutEditorLevelAdminApi.PreviewDeleteLevel(setName, levelId)));
                return;
            }

            if (path == "/api/level/create" && request.HttpMethod == "POST")
            {
                var body = ReadBody(request);
                var dto = JsonUtility.FromJson<LevelCreateDto>(body);
                var err = LayoutEditorLevelAdminApi.CreateLevel(dto);
                WriteAdminResult(response, err);
                return;
            }

            if (path == "/api/level/info" && request.HttpMethod == "POST")
            {
                var body = ReadBody(request);
                var dto = JsonUtility.FromJson<LevelInfoUpdateDto>(body);
                var err = LayoutEditorLevelAdminApi.UpdateLevelInfo(dto);
                WriteAdminResult(response, err);
                return;
            }

            if (path == "/api/level/config" && request.HttpMethod == "POST")
            {
                var body = ReadBody(request);
                var dto = JsonUtility.FromJson<LevelConfigUpdateDto>(body);
                var err = LayoutEditorLevelAdminApi.UpdateLevelConfig(dto);
                WriteAdminResult(response, err);
                return;
            }

            if (path == "/api/level/audio" && request.HttpMethod == "POST")
            {
                var body = ReadBody(request);
                var dto = JsonUtility.FromJson<LevelAudioUpdateDto>(body);
                var err = LayoutEditorLevelAdminApi.UpdateLevelAudio(dto);
                WriteAdminResult(response, err);
                return;
            }

            if (path == "/api/level/delete" && request.HttpMethod == "POST")
            {
                var body = ReadBody(request);
                var dto = JsonUtility.FromJson<LevelDeleteDto>(body);
                var err = LayoutEditorLevelAdminApi.DeleteLevel(dto);
                WriteAdminResult(response, err);
                return;
            }

            if (path == "/api/reload" && request.HttpMethod == "POST")
            {
                LayoutEditorLevelAdminApi.Reload();
                WriteJson(response, 200, "{\"ok\":true}");
                return;
            }

            if (path == "/api/scene/death" && request.HttpMethod == "POST")
            {
                var body = ReadBody(request);
                var dto = JsonUtility.FromJson<DeathThemeDto>(body);
                var err = LayoutEditorLevelAdminApi.SetDeathTheme(dto != null ? dto.sceneAssetPath : null, dto != null ? dto.theme : null);
                WriteAdminResult(response, err);
                return;
            }

            if (path == "/api/scene/killplane" && request.HttpMethod == "POST")
            {
                var body = ReadBody(request);
                var dto = JsonUtility.FromJson<KillPlaneBoundsDto>(body);
                var err = dto == null
                    ? "缺少参数。"
                    : LayoutEditorLevelAdminApi.SetKillPlaneBounds(dto.sceneAssetPath, dto.cx, dto.cz, dto.sx, dto.sz);
                WriteAdminResult(response, err);
                return;
            }

            if (path == "/api/level/image-upload" && request.HttpMethod == "POST")
            {
                var body = ReadBody(request);
                var dto = JsonUtility.FromJson<ImageUploadDto>(body);
                string texturePath;
                var err = LayoutEditorLevelAdminApi.UploadImageFloor(
                    dto != null ? dto.setName : null,
                    dto != null ? dto.fileName : null,
                    dto != null ? dto.base64 : null,
                    out texturePath);
                if (!string.IsNullOrEmpty(err))
                    WriteJson(response, 400, LayoutEditorJson.ToJson(new ApiErrorDto { error = err }));
                else
                    WriteJson(response, 200, LayoutEditorJson.ToJson(new ImageUploadResultDto { texturePath = texturePath }));
                return;
            }

            if (path == "/api/level/data-file" && request.HttpMethod == "GET")
            {
                // Serve a raw file from the project (used to preview image-floor
                // textures stored in a level set's data dir).
                var rel = request.QueryString["path"];
                string abs;
                string contentType;
                var err = LayoutEditorLevelAdminApi.ResolveProjectFile(rel, out abs, out contentType);
                if (!string.IsNullOrEmpty(err) || !File.Exists(abs))
                {
                    WriteJson(response, 404, LayoutEditorJson.ToJson(new ApiErrorDto { error = err ?? "文件不存在。" }));
                    return;
                }
                var bytes = File.ReadAllBytes(abs);
                response.ContentType = contentType;
                response.StatusCode = 200;
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
                response.OutputStream.Close();
                return;
            }

            if (TryServeStatic(path, response))
                return;

            WriteJson(response, 404, LayoutEditorJson.ToJson(new ApiErrorDto { error = "Not found: " + path }));
        }
        catch (Exception ex)
        {
            WriteJson(response, 500, LayoutEditorJson.ToJson(new ApiErrorDto { error = ex.Message }));
        }
    }

    private static void OpenSceneIfNeeded(string assetPath)
    {
        assetPath = assetPath.Replace('\\', '/');
        var active = EditorSceneManager.GetActiveScene();
        if (active.path == assetPath)
            return;

        if (File.Exists(assetPath))
            EditorSceneManager.OpenScene(assetPath);
    }

    private static LevelSetSceneListDto ScanLevelSetScenes()
    {
        var root = Path.Combine(Application.dataPath, "LevelSets");
        var scenes = new List<LevelSetSceneDto>();
        if (!Directory.Exists(root))
            return new LevelSetSceneListDto { scenes = scenes.ToArray() };

        foreach (var file in Directory.GetFiles(root, "*.unity", SearchOption.AllDirectories))
        {
            if (!file.Replace('\\', '/').Contains("/scenes/"))
                continue;

            var assetPath = "Assets" + file.Replace(Application.dataPath, "").Replace('\\', '/');
            var parts = assetPath.Split('/');
            string levelSet = parts.Length > 2 ? parts[2] : "";
            scenes.Add(new LevelSetSceneDto
            {
                assetPath = assetPath,
                levelSet = levelSet,
                sceneName = Path.GetFileNameWithoutExtension(file)
            });
        }

        scenes.Sort((a, b) => string.Compare(a.assetPath, b.assetPath, StringComparison.Ordinal));
        return new LevelSetSceneListDto { scenes = scenes.ToArray() };
    }

    private static bool TryServeStatic(string path, HttpListenerResponse response)
    {
        if (path.StartsWith("/api/", StringComparison.Ordinal))
            return false;

        if (path == "/")
            path = "/index.html";

        var webRoot = LayoutEditorPaths.WebDistRoot;
        if (!Directory.Exists(webRoot))
            return false;

        var filePath = Path.GetFullPath(Path.Combine(webRoot, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (!LayoutEditorPaths.IsPathUnderRoot(filePath, webRoot))
            return false;

        if (!File.Exists(filePath))
        {
            if (path == "/index.html" || (!path.Contains(".") && !path.StartsWith("/api/", StringComparison.Ordinal)))
            {
                filePath = Path.Combine(webRoot, "index.html");
                if (!File.Exists(filePath))
                    return false;
            }
            else
            {
                return false;
            }
        }

        var bytes = File.ReadAllBytes(filePath);
        response.ContentType = GetContentType(filePath);
        response.StatusCode = 200;
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.OutputStream.Close();
        return true;
    }

    private static string GetContentType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        switch (ext)
        {
            case ".html": return "text/html; charset=utf-8";
            case ".js": return "application/javascript; charset=utf-8";
            case ".css": return "text/css; charset=utf-8";
            case ".json": return "application/json; charset=utf-8";
            case ".svg": return "image/svg+xml";
            case ".png": return "image/png";
            default: return "application/octet-stream";
        }
    }

    private static string ReadBody(HttpListenerRequest request)
    {
        if (!request.HasEntityBody)
            return "";

        using (var reader = new StreamReader(request.InputStream, new UTF8Encoding(false)))
            return reader.ReadToEnd();
    }

    private static void WriteJson(HttpListenerResponse response, int status, string json)
    {
        WriteText(response, status, json, "application/json; charset=utf-8");
    }

    private static void WriteText(HttpListenerResponse response, int status, string text, string contentType = "text/plain; charset=utf-8")
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? "");
        response.StatusCode = status;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.OutputStream.Close();
    }

    private static void WriteAdminResult(HttpListenerResponse response, string error)
    {
        if (!string.IsNullOrEmpty(error))
            WriteJson(response, 400, LayoutEditorJson.ToJson(new ApiErrorDto { error = error }));
        else
            WriteJson(response, 200, "{\"ok\":true}");
    }
}
