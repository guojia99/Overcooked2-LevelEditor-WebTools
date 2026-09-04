using System;
using System.Collections.Generic;
using System.IO;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>关卡集导出（打包 AssetBundle → 清理产物 → 生成 zip）。
/// Docs/zh/tutorial.md「构建和导出关卡」：场景 bundle = &lt;set&gt;/&lt;sceneName&gt;，
/// 关卡集根目录 bundle = &lt;set&gt;/info_&lt;set&gt;，Build 后产物在 Assets/AssetBundles/&lt;set&gt;/。
///
/// BuildPipeline.BuildAssetBundles 会在主线程阻塞数分钟，期间 HTTP 主线程泵
/// （PumpMainThread）无法执行——所以 /api/set/export 只负责启动任务（delayCall），
/// 状态查询由监听线程 fast-path 直答（见 LayoutEditorHttpServer.ListenLoop），
/// zip 下载同样不依赖主线程泵。</summary>
public static class LayoutEditorSetExporter
{
    private const string LevelSetsRoot = "Assets/LevelSets";
    private const string BundlesRoot = "Assets/AssetBundles";
    /** zip 输出目录（项目根、Assets 外，避免 Unity 生成 .meta）。 */
    public const string ExportRootDir = "LayoutEditorExports";

    /// <summary>导出前置扩展钩子（参数 = 关卡集名）。解耦点：无订阅者时行为不变，
    /// 例如 CustomStub 的 Stub DLL staging（LayoutStubDllBuilder）经此接入。</summary>
    public static Action<string> BeforeBuild;

    private static readonly object _lock = new object();
    private static string _status = "idle"; // idle | running | done | error
    private static string _setName = "";
    private static string _phase = "";
    private static string _message = "";
    private static string _error = "";
    private static string _zipFileName = "";
    private static string _zipAbsPath = "";
    private static int _fileCount;
    /** 本趟导出是否有场景用到 CustomStub（决定 zip 是否携带 runtime bundle）。 */
    private static bool _usesCustomStub;

    /// <summary>CustomStub tag 载体前缀（SpecificPseudoPrefabTag.prefabTag）。
    ///  与 CustomStub/EntryPoint.HealObject + loader 的 RandomCrate 解析保持同步；
    ///  新增 stub 类型时此处必须补前缀。</summary>
    private static readonly string[] CustomStubTagPrefixes =
    {
        "RandomCrate|", "TimedSwitch|", "PushablePot|", "SwitchReenable|", "WorldMapDressing|"
    };

    /// <summary>扫描当前打开的场景是否用到 CustomStub：tag 载体（含 prefab 自带的
    ///  RandomCrate|）或命名空间 CustomStub 的组件（Stub_<set> 程序集，双通道兜底）。
    ///  导出 prepare 阶段逐场景调用（场景此时已打开）。</summary>
    private static bool ActiveSceneUsesCustomStub()
    {
        foreach (var tag in UnityEngine.Object.FindObjectsOfType<LevelEditorStub.SpecificPseudoPrefabTag>())
        {
            var t = tag.prefabTag;
            if (string.IsNullOrEmpty(t))
                continue;
            for (int i = 0; i < CustomStubTagPrefixes.Length; i++)
            {
                if (t.StartsWith(CustomStubTagPrefixes[i], StringComparison.Ordinal))
                    return true;
            }
        }
        foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
        {
            if (mb == null)
                continue; // missing script
            var ns = mb.GetType().Namespace;
            if (ns == "CustomStub")
                return true;
        }
        return false;
    }

    public static string ExportRootAbsPath()
    {
        var dataPath = Application.dataPath.Replace('\\', '/');
        var root = Path.GetDirectoryName(dataPath);
        return (root ?? "").Replace('\\', '/') + "/" + ExportRootDir;
    }

    /// <summary>状态快照（监听线程安全读取）。</summary>
    public static SetExportStatusDto GetStatus()
    {
        lock (_lock)
        {
            return new SetExportStatusDto
            {
                status = _status,
                setName = _setName,
                phase = _phase,
                message = _message,
                error = _error,
                zipFileName = _zipFileName,
                fileCount = _fileCount
            };
        }
    }

    /// <summary>启动导出任务（主线程调用）。返回 null 表示已启动，否则为错误信息。</summary>
    public static string StartExport(string setName)
    {
        if (string.IsNullOrEmpty(setName))
            return "缺少关卡集标识。";
        var safe = setName.Trim();
        if (safe.IndexOf('/') >= 0 || safe.IndexOf('\\') >= 0 || safe == "." || safe == "..")
            return "关卡集标识非法。";
        var setDir = LevelSetsRoot + "/" + safe;
        if (!AssetDatabase.IsValidFolder(setDir))
            return "关卡集不存在：" + safe;

        lock (_lock)
        {
            if (_status == "running")
                return "已有导出任务正在进行（" + _setName + "），请等待完成后再试。";
            _status = "running";
            _setName = safe;
            _phase = "queued";
            _message = "任务已排队…";
            _error = "";
            _zipFileName = "";
            _zipAbsPath = "";
            _fileCount = 0;
            _usesCustomStub = false;
        }
        EditorApplication.delayCall += RunExport;
        return null;
    }

    /// <summary>解析 zip 下载请求为绝对路径（监听线程调用，只做纯路径拼接 + 存在性检查，
    ///  不触碰 Unity API）。优先返回刚导出完成的产物，其次按文件名回退。</summary>
    public static string ResolveDownloadPath(string setName, string fileName)
    {
        lock (_lock)
        {
            if (_status == "done" && _setName == setName && !string.IsNullOrEmpty(_zipAbsPath)
                && File.Exists(_zipAbsPath))
                return _zipAbsPath;
        }
        if (string.IsNullOrEmpty(fileName) || fileName.IndexOf('/') >= 0 || fileName.IndexOf('\\') >= 0
            || fileName == "." || fileName == "..")
            return null;
        var candidate = ExportRootAbsPath() + "/" + fileName;
        return File.Exists(candidate) ? candidate : null;
    }

    private static void SetPhase(string phase, string message)
    {
        lock (_lock)
        {
            _phase = phase;
            _message = message;
        }
        Debug.Log("[SetExporter] " + phase + " · " + message);
    }

    private static void RunExport()
    {
        var setName = "";
        lock (_lock) { setName = _setName; }
        var prevActive = EditorSceneManager.GetActiveScene().path;
        try
        {
            RunExportCore(setName);
            lock (_lock) { _status = "done"; }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            lock (_lock)
            {
                _status = "error";
                _error = ex.Message;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            // 回到导出前的活动场景并重载伪 prefab（失败不中断导出结果）。
            try
            {
                if (!string.IsNullOrEmpty(prevActive) && File.Exists(AbsPath(prevActive))
                    && EditorSceneManager.GetActiveScene().path != prevActive)
                    EditorSceneManager.OpenScene(prevActive);
                LayoutEditorPseudoReload.ReloadPseudoAssetsFull();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SetExporter] 恢复场景失败：" + ex.Message);
            }
        }
    }

    private static void RunExportCore(string setName)
    {
        var setDir = LevelSetsRoot + "/" + setName;
        var outDir = BundlesRoot + "/" + setName;
        var absOutDir = AbsPath(outDir);

        // ---- 1. prepare：逐场景 Open → 清临时物体（Prepare For Building）→ Save ----
        var scenes = CollectScenePaths(setDir);
        if (scenes.Count == 0)
            throw new Exception("关卡集没有可用场景（" + setDir + "/scenes/ 为空）。");
        var i = 0;
        foreach (var scenePath in scenes)
        {
            i++;
            SetPhase("prepare", "准备场景 " + i + "/" + scenes.Count + "：" + Path.GetFileName(scenePath));
            EditorUtility.DisplayProgressBar("导出关卡集 " + setName,
                "准备场景 " + i + "/" + scenes.Count + "…", (float)i / (scenes.Count + 1));
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            LayoutEditorPseudoReload.EnsurePrepareForBuilding();
            if (!_usesCustomStub && ActiveSceneUsesCustomStub())
            {
                _usesCustomStub = true;
                Debug.Log("[SetExporter] 检测到 CustomStub 用法（tag/组件）：" + scenePath
                    + "，本集 zip 将携带 runtime bundle");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();

        // ---- 2. clean：仅删除本关卡集的旧产物目录（其他目录不动）----
        SetPhase("clean", "清理旧构建产物：" + outDir);
        if (AssetDatabase.IsValidFolder(outDir))
            AssetDatabase.DeleteAsset(outDir);
        else if (Directory.Exists(absOutDir))
            Directory.Delete(absOutDir, true);
        var absDirMeta = absOutDir + ".meta";
        if (File.Exists(absDirMeta))
            File.Delete(absDirMeta);
        AssetDatabase.Refresh();

        // ---- 3. build：构建 AssetBundle（阻塞，约 3-5 分钟）----
        SetPhase("build", "构建 AssetBundle（约 3-5 分钟）…");
        LayoutEditorLevelAdminApi.EnsureSetInfoBundle(setName);
        EnsureSceneBundleNames(setName, scenes);
        if (BeforeBuild != null)
            BeforeBuild(setName);
        if (!Directory.Exists(AbsPath(BundlesRoot)))
            Directory.CreateDirectory(AbsPath(BundlesRoot));
        var manifest = BuildPipeline.BuildAssetBundles(
            BundlesRoot, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);
        if (manifest == null)
            throw new Exception("BuildPipeline.BuildAssetBundles 返回 null，构建失败（详见 Console）。");
        if (!Directory.Exists(absOutDir))
            throw new Exception("构建完成但没有输出目录 " + outDir + "（bundle 名可能未设置，请检查关卡集根目录与场景的 AssetBundle）。");

        // ---- 4. package：删除 .manifest / *.meta 等带后缀文件 ----
        SetPhase("package", "清理 manifest 与 meta 文件…");
        foreach (var f in Directory.GetFiles(absOutDir))
        {
            var lower = f.ToLower();
            if (lower.EndsWith(".manifest") || lower.EndsWith(".meta"))
                File.Delete(f);
        }

        // ---- 5. zip：打包含顶层 setName/ 文件夹的 zip ----
        var payloads = new List<string>(Directory.GetFiles(absOutDir));
        payloads.RemoveAll(HasJunkExtension);
        // CustomStub 按需携带：本集无场景使用 tag/组件时，zip 不含 runtime bundle
        // （loader 扫描不到 runtime 自然跳过，不注入 CustomStub 程序集）。
        if (!_usesCustomStub)
        {
            int removed = payloads.RemoveAll(p =>
                string.Equals(Path.GetFileName(p), "runtime", StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
                Debug.Log("[SetExporter] 本集未使用 CustomStub 道具，zip 不携带 runtime bundle");
        }
        if (payloads.Count == 0)
            throw new Exception("清理后没有可打包的 bundle 文件。");
        if (File.Exists(absOutDir + "/info_" + setName) == false)
            Debug.LogWarning("[SetExporter] 未找到 info_" + setName
                + "（关卡集根目录 AssetBundle 可能用了历史命名），将按实际产物打包。");

        var version = SanitizeVersion(FindSetVersion(setName));
        var zipFileName = setName + "_v" + version + "_" + DateTime.Now.ToString("yyyyMMdd") + ".zip";
        var zipAbsPath = ExportRootAbsPath() + "/" + zipFileName;
        SetPhase("zip", "生成 zip：" + zipFileName + "（" + payloads.Count + " 个文件）…");
        var entries = new List<LayoutEditorZipWriter.ZipEntrySource>();
        foreach (var p in payloads)
            entries.Add(new LayoutEditorZipWriter.ZipEntrySource(setName + "/" + Path.GetFileName(p), p));
        LayoutEditorZipWriter.WriteZip(zipAbsPath, entries);

        lock (_lock)
        {
            _zipFileName = zipFileName;
            _zipAbsPath = zipAbsPath;
            _fileCount = payloads.Count;
            _message = "导出完成：" + zipFileName;
        }
        AssetDatabase.Refresh();
    }

    private static List<string> CollectScenePaths(string setDir)
    {
        var result = new List<string>();
        var scenesDir = setDir + "/scenes";
        if (!AssetDatabase.IsValidFolder(scenesDir))
            return result;
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { scenesDir }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(p) && File.Exists(AbsPath(p)))
                result.Add(p);
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>场景 bundle 名 = &lt;set&gt;/&lt;sceneName&gt;（Docs/zh 构建步骤 2）。
    ///  保守补齐：只补空值，历史命名（改名过的关卡）不动。</summary>
    private static void EnsureSceneBundleNames(string setName, List<string> scenePaths)
    {
        foreach (var scenePath in scenePaths)
        {
            var importer = AssetImporter.GetAtPath(scenePath);
            if (importer == null || !string.IsNullOrEmpty(importer.assetBundleName))
                continue;
            var sceneName = Path.GetFileNameWithoutExtension(scenePath);
            importer.assetBundleName = setName + "/" + sceneName;
            importer.SaveAndReimport();
            Debug.Log("[SetExporter] 场景 AssetBundle 已设为 " + setName + "/" + sceneName);
        }
    }

    private static string FindSetVersion(string setName)
    {
        var dataDir = LevelSetsRoot + "/" + setName + "/data";
        if (!AssetDatabase.IsValidFolder(dataDir))
            return "";
        foreach (var guid in AssetDatabase.FindAssets("t:LevelSetInfoSO", new[] { dataDir }))
        {
            var so = AssetDatabase.LoadAssetAtPath<LevelSetInfoSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (so != null)
                return so.version ?? "";
        }
        return "";
    }

    private static string SanitizeVersion(string version)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ch in (version ?? "").Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' || ch == '_')
                sb.Append(ch);
            else
                sb.Append('-');
        }
        var s = sb.ToString();
        return s.Length > 0 ? s : "0";
    }

    private static bool HasJunkExtension(string path)
    {
        var lower = path.ToLower();
        return lower.EndsWith(".manifest") || lower.EndsWith(".meta");
    }

    private static string AbsPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "";
        var dataPath = Application.dataPath.Replace('\\', '/');
        if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            return dataPath + assetPath.Substring("Assets".Length);
        return assetPath;
    }
}
