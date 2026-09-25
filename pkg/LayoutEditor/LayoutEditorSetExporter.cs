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
    /** 本趟导出的关卡集列表（多集合并导出；单集 = 1 个元素）。 */
    private static List<string> _setNames = new List<string>();
    private static string _phase = "";
    private static string _message = "";
    private static string _error = "";
    private static string _zipFileName = "";
    private static string _zipAbsPath = "";
    private static int _fileCount;
    /** 本趟导出是否有场景用到 CustomStub（决定 zip 是否携带 runtime bundle）。 */
    private static bool _usesCustomStub;
    /** 导出模式：levels | deps | all。 */
    private static string _mode = "all";

    /// <summary>CustomStub tag 载体前缀（SpecificPseudoPrefabTag.prefabTag）。
    ///  与 CustomStub/EntryPoint.HealObject + loader 的解析保持同步；
    ///  新增 stub 类型时此处必须补前缀。</summary>
    private static readonly string[] CustomStubTagPrefixes =
    {
        "RandomCrate|", "TimedSwitch|", "PushablePot|", "SwitchReenable|", "WorldMapDressing|",
        "UtensilTiming|", "CameraOffset|", "TravelatorReverse|", "TeleportalExitOnly|", "RatHeist|",
        "ConveyorDirectionSync|", "BLRelay|",
    };

    /// <summary>扫描当前打开的场景是否用到 CustomStub：tag 载体（含 prefab 自带的
    ///  RandomCrate|）或命名空间 CustomStub 的组件（Stub_<set> 程序集，双通道兜底）。
    ///  导出 prepare 阶段逐场景调用（场景此时已打开）；写回守卫
    ///  （CustomStubWriteBackGuard）在 Apply 完成后也复用本方法判定是否检查 stub。</summary>
    public static bool ActiveSceneUsesCustomStub()
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
            // 锅具时间参数（cookTime/burnTime/mixTime/overMixTime 任一 > 0）：
            // 应用逻辑在 Stub_<set> 程序集（CustomStub.UtensilTiming + Harmony 阈值接管），
            // 必须随关卡包分发。写回时按 tag "UtensilTiming|" 烘焙（上方前缀已覆盖）；
            // 此处再按命名空间 CustomStub 的组件兜底（含烘焙好的 UtensilTimingConfig）。
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

    /// <summary>启动导出任务（主线程调用）。返回 null 表示已启动，否则为错误信息。
    /// mode：levels（仅关卡集）| deps（仅依赖包）| all（全部一起，默认/未知回落 all）。</summary>
    public static string StartExport(string setName)
    {
        return StartExport(setName, "all");
    }

    public static string StartExport(string setName, string mode)
    {
        var list = new List<string>();
        if (!string.IsNullOrEmpty(setName))
            list.Add(setName);
        return StartExport(list, mode);
    }

    /// <summary>多集合并导出：一次打包多个关卡集到同一 zip（OC2DIYLevel/levels/&lt;set1&gt;/、
    /// levels/&lt;set2&gt;/… 并列，依赖包只带一份）。单集时行为与旧接口完全一致。
    /// deps 模式与集无关（统一运行时依赖包），多于一个集时只取第一个。</summary>
    public static string StartExport(List<string> setNames, string mode)
    {
        if (setNames == null || setNames.Count == 0)
            return "缺少关卡集标识。";
        var safeList = new List<string>();
        foreach (var raw in setNames)
        {
            if (string.IsNullOrEmpty(raw))
                continue;
            var safe = raw.Trim();
            if (safeList.Contains(safe))
                continue; // 去重（保序）
            safeList.Add(safe);
        }
        if (safeList.Count == 0)
            return "缺少关卡集标识。";
        var m = (mode ?? "all").Trim().ToLower();
        if (m != "levels" && m != "deps" && m != "all")
            m = "all";
        if (m == "deps" && safeList.Count > 1)
            safeList.RemoveRange(1, safeList.Count - 1); // 依赖包与集无关，取第一个

        foreach (var safe in safeList)
        {
            if (safe.IndexOf('/') >= 0 || safe.IndexOf('\\') >= 0 || safe == "." || safe == "..")
                return "关卡集标识非法：" + safe;
            if (!AssetDatabase.IsValidFolder(LevelSetsRoot + "/" + safe))
                return "关卡集不存在：" + safe;
        }

        lock (_lock)
        {
            if (_status == "running")
                return "已有导出任务正在进行（" + _setName + "），请等待完成后再试。";
            _status = "running";
            _setName = string.Join("+", safeList.ToArray());
            _setNames = safeList;
            _phase = "queued";
            _message = "任务已排队…";
            _error = "";
            _zipFileName = "";
            _zipAbsPath = "";
            _fileCount = 0;
            _usesCustomStub = false;
            _mode = m;
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
        var setNames = new List<string>();
        lock (_lock) { setNames.AddRange(_setNames); }
        var prevActive = EditorSceneManager.GetActiveScene().path;
        try
        {
            RunExportCore(setNames);
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

    private static void RunExportCore(List<string> setNames)
    {
        var mode = _mode;
        var joinedName = string.Join("+", setNames.ToArray());

        // ---- 依赖包模式（deps）：不碰关卡场景，仅打包 OC2DIYLevelRuntimeWLoader/ 依赖 ----
        if (mode == "deps")
        {
            ExportDepsOnly(setNames[0]);
            return;
        }

        // ---- 1. prepare：逐集逐场景 Open → 清临时物体 → 重打 stub tag → Save ----
        var perSetScenes = new List<KeyValuePair<string, List<string>>>();
        var totalScenes = 0;
        foreach (var setName in setNames)
        {
            var scenes = CollectScenePaths(LevelSetsRoot + "/" + setName);
            if (scenes.Count == 0)
                throw new Exception("关卡集没有可用场景（" + LevelSetsRoot + "/" + setName
                    + "/scenes/ 为空）。");
            perSetScenes.Add(new KeyValuePair<string, List<string>>(setName, scenes));
            totalScenes += scenes.Count;
        }
        var i = 0;
        foreach (var pair in perSetScenes)
        {
            foreach (var scenePath in pair.Value)
            {
                i++;
                SetPhase("prepare", "准备场景 " + i + "/" + totalScenes + "（" + pair.Key + "）："
                    + Path.GetFileName(scenePath));
                EditorUtility.DisplayProgressBar("导出关卡集 " + joinedName,
                    "准备场景 " + i + "/" + totalScenes + "…", (float)i / (totalScenes + 1));
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var hudWarn = LayoutEditorHudOrderLimits.BakeActiveScene();
                if (!string.IsNullOrEmpty(hudWarn))
                    Debug.LogWarning("[SetExporter] " + hudWarn);
                // 打包时自动给 web CustomStub 道具重打 tag（组件→最新格式 tag，覆盖旧格式/
                // 补齐缺失），保证 loader 场景自愈可靠、无陈旧/缺失 tag。
                var retagged = LayoutEditorStubIO.RefreshStubTagsInActiveScene();
                if (retagged > 0)
                    Debug.Log("[SetExporter] 已重打 " + retagged + " 个 stub tag：" + scenePath);
                LayoutEditorPseudoReload.EnsurePrepareForBuilding();
                if (!_usesCustomStub && ActiveSceneUsesCustomStub())
                {
                    _usesCustomStub = true;
                    Debug.Log("[SetExporter] 检测到 CustomStub 用法（tag/组件）：" + scenePath);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        AssetDatabase.SaveAssets();

        // ---- 2. clean：逐集仅删除本集旧产物目录（其他目录不动）----
        foreach (var setName in setNames)
        {
            var outDir = BundlesRoot + "/" + setName;
            var absOutDir = AbsPath(outDir);
            SetPhase("clean", "清理旧构建产物：" + outDir);
            if (AssetDatabase.IsValidFolder(outDir))
                AssetDatabase.DeleteAsset(outDir);
            else if (Directory.Exists(absOutDir))
                Directory.Delete(absOutDir, true);
            var absDirMeta = absOutDir + ".meta";
            if (File.Exists(absDirMeta))
                File.Delete(absDirMeta);
        }
        AssetDatabase.Refresh();

        // ---- 3. build：构建 AssetBundle（阻塞，约 3-5 分钟；一次全量构建覆盖所有集）----
        SetPhase("build", "构建 AssetBundle（约 3-5 分钟）…");
        foreach (var pair in perSetScenes)
        {
            LayoutEditorLevelAdminApi.EnsureSetInfoBundle(pair.Key);
            EnsureSceneBundleNames(pair.Key, pair.Value);
        }
        if (BeforeBuild != null)
            BeforeBuild(setNames[0]); // 钩子为统一运行时 staging，与集名无关，只调一次
        if (!Directory.Exists(AbsPath(BundlesRoot)))
            Directory.CreateDirectory(AbsPath(BundlesRoot));
        var manifest = BuildPipeline.BuildAssetBundles(
            BundlesRoot, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);
        if (manifest == null)
            throw new Exception("BuildPipeline.BuildAssetBundles 返回 null，构建失败（详见 Console）。");
        foreach (var setName in setNames)
        {
            if (!Directory.Exists(AbsPath(BundlesRoot + "/" + setName)))
                throw new Exception("构建完成但没有输出目录 " + BundlesRoot + "/" + setName
                    + "（bundle 名可能未设置，请检查关卡集根目录与场景的 AssetBundle）。");
        }

        // ---- 4. package：逐集删除 .manifest / *.meta 等带后缀文件 ----
        SetPhase("package", "清理 manifest 与 meta 文件…");
        foreach (var setName in setNames)
        {
            foreach (var f in Directory.GetFiles(AbsPath(BundlesRoot + "/" + setName)))
            {
                var lower = f.ToLower();
                if (lower.EndsWith(".manifest") || lower.EndsWith(".meta"))
                    File.Delete(f);
            }
        }

        // ---- 5. zip：新结构（整体解压到 BepInEx/plugins/ 即全部就位）----
        //   OC2DIYLevel/levels/<set1>/… <set2>/…   各集关卡 bundle（info_<set> / s_*）+ requires.txt（逐集）
        //   OC2DIYLevelRuntimeWLoader/…         仅 all 模式：Loader.dll + debugLog.dll + 配置
        //                                     + webcustomstub_runtime
        //                                     + commonW1/commonW2/...（依赖包只带一份）
        var entries = new List<LayoutEditorZipWriter.ZipEntrySource>();
        foreach (var setName in setNames)
        {
            var absOutDir = AbsPath(BundlesRoot + "/" + setName);
            var payloads = new List<string>(Directory.GetFiles(absOutDir));
            payloads.RemoveAll(HasJunkExtension);
            // 旧体系 per-set runtime bundle 已废除，若产物里残留 runtime 文件一律剔除
            // （统一运行时改由依赖包 OC2DIYLevelRuntimeWLoader/webcustomstub_runtime 分发）。
            payloads.RemoveAll(p =>
                string.Equals(Path.GetFileName(p), "runtime", StringComparison.OrdinalIgnoreCase));
            if (payloads.Count == 0)
                throw new Exception("清理后没有可打包的 bundle 文件（" + setName + "）。");
            if (!File.Exists(absOutDir + "/info_" + setName))
                Debug.LogWarning("[SetExporter] 未找到 info_" + setName
                    + "（关卡集根目录 AssetBundle 可能用了历史命名），将按实际产物打包。");

            // 关卡 bundle → OC2DIYLevel/levels/<set>/（模组从此固定路径读关卡）
            foreach (var p in payloads)
                entries.Add(new LayoutEditorZipWriter.ZipEntrySource(
                    "OC2DIYLevel/levels/" + setName + "/" + Path.GetFileName(p), p));

            // requires.txt：本关卡集要求的依赖包版本（= 统一运行时 SSOT 版本）；
            // Loader 用自身 PluginVersion semver 比较，< 时警告跳过 stub 支持。
            var requiresAbs = WriteRequiresFile(setName);
            if (!string.IsNullOrEmpty(requiresAbs))
                entries.Add(new LayoutEditorZipWriter.ZipEntrySource(
                    "OC2DIYLevel/levels/" + setName + "/requires.txt", requiresAbs));
        }

        // all 模式：附带依赖包 OC2DIYLevelRuntimeWLoader/（commonW2 随任一集引用按需）
        if (mode == "all")
            AddDependencyEntries(entries, setNames, false);

        var zipFileName = BuildZipFileName(setNames, mode);
        var zipAbsPath = ExportRootAbsPath() + "/" + zipFileName;
        SetPhase("zip", "生成 zip：" + zipFileName + "（" + entries.Count + " 个文件）…");
        LayoutEditorZipWriter.WriteZip(zipAbsPath, entries);

        lock (_lock)
        {
            _zipFileName = zipFileName;
            _zipAbsPath = zipAbsPath;
            _fileCount = entries.Count;
            _message = "导出完成：" + zipFileName;
        }
        AssetDatabase.Refresh();
    }

    /// <summary>导出 zip 文件名：单集 = &lt;set&gt;_v&lt;ver&gt;[_levels]_&lt;yyyyMMdd&gt;.zip（与历史
    /// 完全一致）；多集 = 各集 name_v&lt;ver&gt; 用 + 连接（超长时回落 multi&lt;N&gt;sets）。</summary>
    private static string BuildZipFileName(List<string> setNames, string mode)
    {
        var parts = new List<string>();
        foreach (var setName in setNames)
            parts.Add(setName + "_v" + SanitizeVersion(FindSetVersion(setName)));
        var modeSuffix = mode == "levels" ? "_levels" : "";
        var baseName = string.Join("+", parts.ToArray());
        if (baseName.Length > 120)
            baseName = "multi" + setNames.Count + "sets";
        return baseName + modeSuffix + "_" + DateTime.Now.ToString("yyyyMMdd") + ".zip";
    }

    /// <summary>依赖包（deps）模式：不构建关卡场景，仅打包 OC2DIYLevelRuntimeWLoader/
    /// （Loader.dll + webcustomstub_runtime + commonW1 + 按需 commonW2）。产物为预构建
    /// bundle + web/public/Loader.dll，无需 BuildAssetBundles，快速导出。</summary>
    private static void ExportDepsOnly(string setName)
    {
        SetPhase("build", "打包依赖 OC2DIYLevelRuntimeWLoader…");
        // 统一运行时新鲜度校验 + staging（BeforeBuild = StageRuntime(throwOnStale)）
        if (BeforeBuild != null)
            BeforeBuild(setName);

        // 当前打开的场景若仍有临时伪 prefab 实例（未 Prepare For Building），
        // BuildAssetBundles 会把这些临时实例拉进构建并中途丢失 instanceID
        // （"Asset has disappeared while building player" 崩溃）。构建前先对活动场景
        // Prepare For Building（DeInit 清临时物体），与 all 模式逐场景准备同理。
        try
        {
            LayoutEditorPseudoReload.EnsurePrepareForBuilding();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SetExporter] 依赖导出前 Prepare For Building 失败（继续尝试构建）: " + ex.Message);
        }

        // 删除 commonW1/commonW2/commonW3 旧构建产物（+ .manifest），随后重新打包，
        // 保证依赖包里的 commonW* 是最新的、不含遗留。
        SetPhase("clean", "清理 commonW1/commonW2/commonW3 旧产物…");
        DeleteBundleProduct("commonw1");
        DeleteBundleProduct("commonw2");
        DeleteBundleProduct("commonw3");

        // 重新构建 AssetBundle（增量：仅重建被删/变更的 commonW1/W2 与统一运行时）。
        SetPhase("build", "重新打包 commonW1 / commonW2 / 统一运行时…");
        if (!Directory.Exists(AbsPath(BundlesRoot)))
            Directory.CreateDirectory(AbsPath(BundlesRoot));
        var depManifest = BuildPipeline.BuildAssetBundles(
            BundlesRoot, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);
        if (depManifest == null)
            throw new Exception("BuildPipeline.BuildAssetBundles 返回 null，commonW1/W2 重新打包失败（详见 Console）。");

        var entries = new List<LayoutEditorZipWriter.ZipEntrySource>();
        AddDependencyEntries(entries, new List<string> { setName }, true); // deps 模式：commonW1/W2 均无条件携带
        if (entries.Count == 0)
            throw new Exception("依赖包为空：未找到 Loader.dll / webcustomstub_runtime / commonW1，"
                + "请先执行 Layout Editor/CustomStub/Build AssetBundles（含 Runtime staging）。");

        var zipFileName = "OC2DIYLevelRuntimeWLoader_v" + StubVersionValue()
            + "_" + DateTime.Now.ToString("yyyyMMdd") + ".zip";
        var zipAbsPath = ExportRootAbsPath() + "/" + zipFileName;
        SetPhase("zip", "生成 zip：" + zipFileName + "（" + entries.Count + " 个文件）…");
        LayoutEditorZipWriter.WriteZip(zipAbsPath, entries);
        lock (_lock)
        {
            _zipFileName = zipFileName;
            _zipAbsPath = zipAbsPath;
            _fileCount = entries.Count;
            _message = "依赖包导出完成：" + zipFileName;
        }
        AssetDatabase.Refresh();
    }

    /// <summary>删除 Assets/AssetBundles 下某个 bundle 产物（+ .manifest）。用于依赖包
    /// 导出前清理 commonW1/W2 旧产物再重建，杜绝遗留。仅删构建产物本身，不碰源资产
    /// 的 .meta（源目录 assetBundleName 已在源侧标记，勿动）。</summary>
    private static void DeleteBundleProduct(string bundleFileName)
    {
        try
        {
            var abs = AbsPath(BundlesRoot) + "/" + bundleFileName;
            if (File.Exists(abs))
            {
                File.Delete(abs);
                Debug.Log("[SetExporter] 已删除旧产物: " + BundlesRoot + "/" + bundleFileName);
            }
            var manifest = abs + ".manifest";
            if (File.Exists(manifest))
                File.Delete(manifest);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SetExporter] 删除 " + bundleFileName + " 旧产物失败: " + ex.Message);
        }
    }

    /// <summary>把依赖包内容加入 zip 条目：OC2DIYLevelRuntimeWLoader/{Loader.dll,
    /// webcustomstub_runtime, commonW1, commonW2, commonW3...}。多集合并导出时整包
    /// 只带一份（调用一次）。commonW1 必须携带；commonW2 在 deps 模式无条件携带，
    /// all 模式任一集引用即携带；commonW3 及以后自动携带，
    /// 避免新增公共资源包后旧的导出逻辑漏分发。</summary>
    private static void AddDependencyEntries(List<LayoutEditorZipWriter.ZipEntrySource> entries,
        List<string> setNames, bool alwaysCommonW2)
    {
        const string depDir = "OC2DIYLevelRuntimeWLoader/";

        // Loader.dll（研发手动维护：layout-editor/web/public/Loader.dll，
        // 更新时先编译 Assets/WebCustomStubRuntime/Loader~/ 再拷贝覆盖）
        var loaderDllAbs = ProjectRootAbsPath() + "/layout-editor/web/public/Loader.dll";
        if (File.Exists(loaderDllAbs))
        {
            entries.Add(new LayoutEditorZipWriter.ZipEntrySource(depDir + "Loader.dll", loaderDllAbs));
            Debug.Log("[SetExporter] 附带 Loader.dll（"
                + File.GetLastWriteTime(loaderDllAbs).ToString("yyyy-MM-dd HH:mm:ss") + "）");
        }
        else
        {
            Debug.LogWarning("[SetExporter] 未找到 " + loaderDllAbs
                + "，依赖包不含 Loader.dll —— CustomStub 玩法将无法生效。");
        }

        // version.txt 与 Loader/debugLog 的构建输出保持一致，随依赖包一起分发，
        // 方便玩家和开发者确认 DLL 是否来自同一套构建产物。
        var versionAbs = ProjectRootAbsPath() + "/Assets/WebCustomStubRuntime/Loader~/bin/Release/version.txt";
        if (File.Exists(versionAbs))
        {
            entries.Add(new LayoutEditorZipWriter.ZipEntrySource(depDir + "version.txt", versionAbs));
            Debug.Log("[SetExporter] 附带 version.txt（"
                + File.GetLastWriteTime(versionAbs).ToString("yyyy-MM-dd HH:mm:ss") + "）");
        }
        else
        {
            Debug.LogWarning("[SetExporter] 未找到 " + versionAbs
                + "，依赖包不含 version.txt —— 请先构建 Loader。");
        }

        // debugLog.dll 与 Loader 同级分发。日志默认 info 级别，缺失时不阻断关卡包导出。
        var debugDllAbs = ProjectRootAbsPath() + "/layout-editor/web/public/debugLog.dll";
        if (File.Exists(debugDllAbs))
        {
            entries.Add(new LayoutEditorZipWriter.ZipEntrySource(depDir + "debugLog.dll", debugDllAbs));
            Debug.Log("[SetExporter] 附带 debugLog.dll（v2.0.0，会话日志/BepInEx 捕捉）");
        }
        else
        {
            Debug.LogWarning("[SetExporter] 未找到 " + debugDllAbs + "，依赖包不含 debugLog.dll（不影响游戏运行）。");
        }

        var debugConfigAbs = ProjectRootAbsPath() + "/layout-editor/web/public/log_config.txt";
        if (File.Exists(debugConfigAbs))
        {
            entries.Add(new LayoutEditorZipWriter.ZipEntrySource(depDir + "log_config.txt", debugConfigAbs));
        }
        else
        {
            Debug.LogWarning("[SetExporter] 未找到 " + debugConfigAbs + "，debugLog.dll 将使用内置默认配置。");
        }

        var readmeAbs = ProjectRootAbsPath() + "/layout-editor/web/public/readme.txt";
        if (File.Exists(readmeAbs))
        {
            entries.Add(new LayoutEditorZipWriter.ZipEntrySource(depDir + "readme.txt", readmeAbs));
        }
        else
        {
            Debug.LogWarning("[SetExporter] 未找到 " + readmeAbs + "，依赖包不含使用说明。");
        }

        // 统一运行时 bundle（webcustomstub_runtime）
        var runtimeAbs = AbsPath(BundlesRoot) + "/" + LayoutStubDllBuilder.RuntimeBundleName;
        if (File.Exists(runtimeAbs))
            entries.Add(new LayoutEditorZipWriter.ZipEntrySource(
                depDir + LayoutStubDllBuilder.RuntimeBundleName, runtimeAbs));
        else
            Debug.LogWarning("[SetExporter] 未找到统一运行时 bundle（" + runtimeAbs
                + "），依赖包不含 webcustomstub_runtime —— 请先 Build AssetBundles（含 Runtime staging）。");

        // commonW1（问号图标库 / RandomDispenser / web 火锅等；由 Loader 从依赖包加载）。
        AddCommonWEntry(entries, depDir, 1, true);

        // commonW2：deps 模式无条件携带（依赖包通用）；all 模式任一集引用时带。
        var commonW2Abs = AbsPath(BundlesRoot) + "/commonw2";
        var wantCommonW2 = alwaysCommonW2;
        if (!wantCommonW2)
        {
            foreach (var setName in setNames)
            {
                if (LayoutEditorCustomIngredients.SetNeedsCommonW2Bundle(setName))
                {
                    wantCommonW2 = true;
                    break;
                }
            }
        }
        if (wantCommonW2)
        {
            if (File.Exists(commonW2Abs))
                entries.Add(new LayoutEditorZipWriter.ZipEntrySource(depDir + "commonW2", commonW2Abs));
            else
                Debug.LogWarning("[SetExporter] 未找到 commonw2 bundle（" + commonW2Abs + "）。");
        }

        // commonW3 及以后为可选扩展包：只要构建产物存在就自动分发，Loader 会按数字顺序读取。
        try
        {
            var bundleFiles = Directory.GetFiles(AbsPath(BundlesRoot));
            var addedCommonW = new HashSet<int>();
            for (int i = 0; i < bundleFiles.Length; i++)
            {
                var fileName = Path.GetFileName(bundleFiles[i]);
                int index;
                if (!TryGetCommonWIndex(fileName, out index) || index < 3)
                    continue;
                if (!addedCommonW.Add(index))
                    continue;
                entries.Add(new LayoutEditorZipWriter.ZipEntrySource(
                    depDir + "commonW" + index, bundleFiles[i]));
                Debug.Log("[SetExporter] 自动附带扩展依赖 commonW" + index + "（" + fileName + "）。");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SetExporter] 扫描 commonW3+ 扩展依赖失败（不影响已识别依赖）：" + ex.Message);
        }
    }

    private static bool TryGetCommonWIndex(string fileName, out int index)
    {
        index = 0;
        if (string.IsNullOrEmpty(fileName)
            || !fileName.StartsWith("commonw", StringComparison.OrdinalIgnoreCase))
            return false;
        var suffix = fileName.Substring("commonw".Length);
        return suffix.Length > 0 && int.TryParse(suffix, out index) && index > 0;
    }

    private static void AddCommonWEntry(List<LayoutEditorZipWriter.ZipEntrySource> entries,
        string depDir, int index, bool required)
    {
        var fileName = "commonw" + index;
        var abs = AbsPath(BundlesRoot) + "/" + fileName;
        if (File.Exists(abs))
            entries.Add(new LayoutEditorZipWriter.ZipEntrySource(depDir + "commonW" + index, abs));
        else if (required)
            Debug.LogWarning("[SetExporter] 未找到 " + fileName + " bundle（" + abs + "）。");
    }

    /// <summary>统一运行时 SSOT 版本号（反射 CustomStub.StubVersion.Value，缺失回落）。</summary>
    private static string StubVersionValue()
    {
        try
        {
            var t = LayoutEditorStubIO.FindCustomStubType("", "StubVersion");
            if (t != null)
            {
                var f = t.GetField("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (f != null)
                {
                    var v = f.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(v))
                        return SanitizeVersion(v);
                }
            }
        }
        catch { }
        return "0";
    }

    /// <summary>写 requires.txt 到临时目录并返回绝对路径（内容 = SSOT 版本号单行）。</summary>
    private static string WriteRequiresFile(string setName)
    {
        try
        {
            var dir = ExportRootAbsPath();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var path = dir + "/._requires_" + setName + ".txt";
            File.WriteAllText(path, StubVersionValue());
            return path;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SetExporter] 写 requires.txt 失败: " + ex.Message);
            return null;
        }
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

    /// <summary>工程根目录绝对路径（Assets 的父目录，正斜杠）。</summary>
    private static string ProjectRootAbsPath()
    {
        var root = Path.GetDirectoryName(Application.dataPath.Replace('\\', '/'));
        return (root ?? "").Replace('\\', '/');
    }
}
