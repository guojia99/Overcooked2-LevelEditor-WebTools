using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OC2LevelRuntimeLoader
{
    /// <summary>
    /// OC2DIYLevelRuntimeWLoader —— 统一运行时加载器（OC2DIYLevel 的配套辅助插件）。
    ///
    /// 部署（独立文件夹）：BepInEx/plugins/OC2DIYLevelRuntimeWLoader/
    ///   Loader.dll + webcustomstub_runtime（统一运行时 bundle） + commonW1 [+ commonW2]。
    /// 关卡集仍在 BepInEx/plugins/OC2DIYLevel/levels/&lt;set&gt;/（模组固定读取路径）。
    ///
    /// 职责（统一单程序集重构后）：
    ///  1. Awake 最早时机：从自身目录（Loader.dll 同级）把 commonW1/commonW2 bundle
    ///     LoadFromFile 进内存并常驻（幂等：已加载则跳过），供所有关卡的 stub 组件
    ///     经 AssetBundle.GetAllLoadedAssetBundles 按名解析；再从自身目录加载统一运行时
    ///     bundle webcustomstub_runtime → Assembly.Load(WebCustomStubRuntime) →
    ///     反射 CustomStub.EntryPoint.Install()。只接管 W1/W2，原版 bundle 仍靠 OC2DIYLevel。
    ///  2. 每关卡集 *_custom_runtime 文件（预留：某关卡专属代码）仍支持加载；
    ///     旧版 runtime 文件（含旧每集 Stub_&lt;set&gt; 代码）一律忽略并告警（不识别旧代码）。
    ///  3. 版本门控：关卡集 requires.txt（= 依赖包 SSOT 版本）与自身 PluginVersion semver
    ///     比较——已装依赖 &gt;= requires 才提供 stub 支持，&lt; 则告警跳过（关卡本体仍加载）。
    ///
    /// 场景自愈（RandomCrate| 等 tag）统一收编于 CustomStub.EntryPoint（本 loader 不再
    /// 自行 HealScene），loader 只负责程序集/依赖加载 + 每次场景加载幂等补扫。
    ///
    /// v2.2.1（2026-09-15）：仅随统一运行时同步版本号（可移动火锅联机实体 ID 错位
    ///     修复 + 联机实体指纹诊断，全部在 CustomStub 侧，loader 无改动）。
    ///     ⚠ 该版本是联机致命修复，requires.txt 门控会挡下 &lt; 2.2.1 的旧依赖包。
    /// v2.2.0（2026-09-15）：仅随统一运行时同步版本号（新增传送门单向
    ///     TeleportalExitOnly，自愈与压制全在 CustomStub.EntryPoint 侧，loader 无改动）。
    /// v2.1.0（2026-09-14 性能审查）：
    ///  1. 关卡目录扫描（Directory/File 枚举 + requires.txt 读取）挪到 ThreadPool
    ///     后台线程——原实现在【每次场景加载】的主线程上做这些磁盘 I/O，关卡集多时
    ///     直接加长进关卡的卡顿。主线程只保留必须的 AssetBundle.LoadFromFile /
    ///     Assembly.Load（Unity API 与程序集加载不可跨线程）。
    ///  2. 日志治理：新增 [Logging] Verbose 配置项（默认 false）——环境目录清单、
    ///     逐文件明细等诊断日志归入 verbose；角色前缀 RoleTag() 按帧缓存
    ///     （原本每条日志 2 次反射 Invoke）；场景加载的程序集清单只在数量变化时打。
    ///     该配置同时被 stub 侧 StubLog 反射读取（统一开关）。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class LevelRuntimeLoader : BaseUnityPlugin
    {
        public const string PluginGuid = "oc2.oc2diylevelruntimewloader";
        public const string PluginName = "OC2DIYLevelRuntimeWLoader";
        public const string PluginVersion = "2.2.1";

        /// <summary>统一运行时 bundle 文件名（依赖包内，固定；不与关卡目录下的
        /// *_custom_runtime 混淆，也绝不叫裸 runtime）。</summary>
        private const string RuntimeBundleFileName = "webcustomstub_runtime";

        /// <summary>依赖 bundle（由本 loader 从自身目录加载并常驻，不 Unload）。</summary>
        private static readonly string[] DependencyBundleNames = { "commonW1", "commonW2" };

        /// <summary>每关卡自定义 runtime 文件后缀（预留通道；不识别旧版 runtime）。</summary>
        private const string CustomRuntimeSuffix = "_custom_runtime";

        private static ManualLogSource _log;
        private static readonly List<byte[]> PendingRaw = new List<byte[]>();
        private static readonly HashSet<string> LoadedNames = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> LoadedBundles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _startupScanDone;
        private static bool _depsLoaded;

        #region 日志前缀：时间戳 + 主/客机角色（联机排障区分两台机器）

        private static MethodInfo _miIsInSession;
        private static MethodInfo _miIsHost;
        private static bool _roleProbed;

        /// <summary>诊断日志开关（BepInEx 配置 [Logging] Verbose，默认 false）。
        /// stub 侧 CustomStub.StubLog 会反射读取本属性作为统一开关——改名需同步
        /// （StubLog.ResolveVerbose 找的就是 "VerboseEnabled"）。</summary>
        private static ConfigEntry<bool> _cfgVerbose;

        public static bool VerboseEnabled
        {
            get { return _cfgVerbose != null && _cfgVerbose.Value; }
        }

        /// <summary>每行日志统一前缀：[HH:mm:ss.fff][主机|客机|单机|未知]。</summary>
        private static string Prefix()
        {
            return "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "][" + RoleTag() + "] ";
        }

        // 角色按帧缓存（v2.1）：RoleTag 原本每条日志做 2 次静态方法反射 Invoke，
        // 一帧内多条日志重复求值纯属浪费。角色只随进/出房间变化，帧内恒定。
        private static int _roleFrame = -1;
        private static string _roleCached;

        /// <summary>角色判定镜像 RandomCrate 的游戏标准写法：ConnectionStatus（internal，
        /// 反射）IsInSession()/IsHost()。</summary>
        private static string RoleTag()
        {
            int frame;
            try { frame = Time.frameCount; }
            catch { frame = -1; }
            if (frame >= 0 && frame == _roleFrame && _roleCached != null)
                return _roleCached;
            var tag = ComputeRoleTag();
            _roleFrame = frame;
            _roleCached = tag;
            return tag;
        }

        private static string ComputeRoleTag()
        {
            try
            {
                if (!_roleProbed)
                {
                    _roleProbed = true;
                    var t = FindLoadedType("ConnectionStatus");
                    if (t != null)
                    {
                        const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                        _miIsInSession = t.GetMethod("IsInSession", F, null, Type.EmptyTypes, null);
                        _miIsHost = t.GetMethod("IsHost", F, null, Type.EmptyTypes, null);
                    }
                }
                if (_miIsInSession != null && _miIsHost != null)
                {
                    var inSession = _miIsInSession.Invoke(null, null);
                    if (!(inSession is bool) || !(bool)inSession)
                        return "单机";
                    var host = _miIsHost.Invoke(null, null);
                    return (host is bool && (bool)host) ? "主机" : "客机";
                }
            }
            catch { }
            return "未知";
        }

        private static void LogI(string message)
        {
            if (_log != null)
                _log.LogInfo(Prefix() + message);
        }

        /// <summary>诊断级日志（[Logging] Verbose=true 时才输出）。</summary>
        private static void LogV(string message)
        {
            if (_log != null && VerboseEnabled)
                _log.LogInfo(Prefix() + message);
        }

        private static void LogW(string message)
        {
            if (_log != null)
                _log.LogWarning(Prefix() + message);
        }

        #endregion

        private void Awake()
        {
            _log = Logger;
            _cfgVerbose = Config.Bind("Logging", "Verbose", false,
                "输出 CustomStub 运行时与本加载器的诊断级日志（掷骰/取出、逐物体自愈、反射自检、"
                + "目录清单等）。默认关闭——这些日志在稳态游玩期是每秒数条的纯开销。排障时开启。");
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            LogI("v" + PluginVersion + " ready（自身目录加载 commonW1/W2 + 统一运行时；关卡目录只认 *_custom_runtime）"
                + (VerboseEnabled ? "｜诊断日志已开启" : ""));
            // 最早时机：加载依赖（commonW1/W2）+ 统一运行时，先于任何关卡场景，
            // 避免首帧问号/大锅贴图竞态；异常不致命。
            // 注意：本条链路【刻意保持同步】——EntryPoint.Install 必须早于场景里
            // MonoScript 的程序集解析，异步化会引入脚本引用解析失败的竞态。
            try
            {
                LoadDependenciesAndRuntime();
            }
            catch (Exception ex)
            {
                LogW("Awake 依赖/运行时加载异常: " + ex);
            }
        }

        /// <summary>Loader.dll 所在目录（= OC2DIYLevelRuntimeWLoader 文件夹）。</summary>
        private static string OwnDir()
        {
            try
            {
                var loc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    var dir = Path.GetDirectoryName(loc);
                    if (!string.IsNullOrEmpty(dir))
                        return dir;
                }
            }
            catch { }
            return null;
        }

        /// <summary>从自身目录加载 commonW1/commonW2（幂等：已在全局表则跳过，不 Unload）
        /// + 统一运行时 webcustomstub_runtime → Assembly.Load → EntryPoint.Install。</summary>
        private static void LoadDependenciesAndRuntime()
        {
            if (_depsLoaded)
                return;
            _depsLoaded = true;
            var dir = OwnDir();
            if (string.IsNullOrEmpty(dir))
            {
                LogW("无法解析 Loader.dll 所在目录，跳过依赖/运行时加载（自愈补扫仍会尝试）");
                return;
            }
            LogI("依赖包目录: " + dir);

            // 1. commonW1 / commonW2（供 stub 组件按名解析；一开始就加载进内存被所有关卡使用）
            foreach (var name in DependencyBundleNames)
            {
                try
                {
                    if (IsBundleLoaded(name))
                    {
                        LogI("  " + name + " 已加载（其它插件已加载），跳过");
                        continue;
                    }
                    var path = Path.Combine(dir, name);
                    if (!File.Exists(path))
                    {
                        LogI("  依赖包未含 " + name + "（可选），跳过");
                        continue;
                    }
                    var b = AssetBundle.LoadFromFile(path);
                    if (b == null)
                        LogW("  " + name + " 加载失败（LoadFromFile 返回 null）: " + path);
                    else
                        LogI("  " + name + " 已加载进内存并常驻（不 Unload）");
                }
                catch (Exception ex)
                {
                    LogW("  " + name + " 加载异常: " + ex.Message);
                }
            }

            // 2. 统一运行时 bundle → *.dll.bytes → Assembly.Load → EntryPoint.Install
            try
            {
                var rtPath = Path.Combine(dir, RuntimeBundleFileName);
                if (!File.Exists(rtPath))
                {
                    LogW("未找到统一运行时 " + RuntimeBundleFileName + "（" + rtPath
                        + "）——CustomStub 玩法将无法生效。请安装/更新依赖包 OC2DIYLevelRuntimeWLoader。");
                    return;
                }
                if (LoadedBundles.Contains(rtPath))
                    return;
                var bundle = AssetBundle.LoadFromFile(rtPath);
                if (bundle == null)
                {
                    LogW("统一运行时 bundle 加载失败（LoadFromFile 返回 null）: " + rtPath);
                    return;
                }
                LoadedBundles.Add(rtPath);
                var loaded = 0;
                foreach (var assetPath in bundle.GetAllAssetNames())
                {
                    if (!assetPath.EndsWith(".dll.bytes", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var asset = bundle.LoadAsset<TextAsset>(assetPath);
                    if (asset == null || asset.bytes == null || asset.bytes.Length == 0)
                    {
                        LogW("统一运行时 DLL 资产读取失败: " + assetPath);
                        continue;
                    }
                    LogI("统一运行时 DLL: " + assetPath + "（" + asset.bytes.Length + " 字节）");
                    if (LoadFromBytes(assetPath, asset.bytes))
                        loaded++;
                }
                if (loaded == 0)
                    LogW("统一运行时 bundle 内无 *.dll.bytes（打错包或旧包？）");
            }
            catch (Exception ex)
            {
                LogW("统一运行时加载异常: " + ex);
            }
        }

        /// <summary>bundle 是否已在 Unity 全局表（按内部 name 匹配，大小写不敏感）。</summary>
        private static bool IsBundleLoaded(string bundleName)
        {
            try
            {
                foreach (var b in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (b != null && string.Equals(b.name, bundleName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private void Update()
        {
            if (!_startupScanDone)
            {
                _startupScanDone = true;
                try
                {
                    DumpEnvironment();
                    BeginScan(true);
                }
                catch (Exception ex)
                {
                    LogW("启动扫描异常: " + ex);
                }
            }
            DrainScanResults();
        }

        #region 环境探测（联机/装机排障：levels 到底在哪、mod 是否就位）

        /// <summary>启动时打一份环境清单：路径解析结果、OC2DIYLevel 目录两级内容、
        /// StreamingAssets 候选位置、AppDomain 里相关程序集。全部只读，异常不致命。
        /// v2.1：目录清单（大量 Directory/FileInfo 磁盘 I/O）改为仅 Verbose 时输出，
        /// 默认只打路径与程序集两项摘要。</summary>
        private static void DumpEnvironment()
        {
            try
            {
                LogI("[环境] PluginPath=" + Paths.PluginPath);
                LogI("[环境] GameRootPath=" + Paths.GameRootPath);
                string dataPath = null;
                try { dataPath = Application.dataPath; } catch { }
                LogI("[环境] Application.dataPath=" + (dataPath ?? "(不可用)"));
                try { LogI("[环境] streamingAssetsPath=" + Application.streamingAssetsPath); }
                catch { }

                if (VerboseEnabled)
                {
                    var pluginDir = Path.Combine(Paths.PluginPath, "OC2DIYLevel");
                    DumpDir("[环境] plugins/OC2DIYLevel", pluginDir);

                    if (dataPath != null)
                    {
                        DumpDir("[环境] StreamingAssets/OC2DIYLevel",
                            Path.Combine(Path.Combine(dataPath, "StreamingAssets"), "OC2DIYLevel"));
                    }
                }
                else
                {
                    LogI("[环境] 目录清单已省略（排障时把 BepInEx 配置 [Logging] Verbose 设为 true）");
                }

                // 相关程序集清单：确认 OC2DIYLevel / LevelEditorStub / Stub_* 是否已加载及版本
                var related = new List<string>();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var n = asm.GetName().Name;
                    if (n.IndexOf("OC2DIY", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("LevelEditorStub", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.StartsWith("Stub_", StringComparison.OrdinalIgnoreCase)
                        || n.IndexOf("CustomStub", StringComparison.OrdinalIgnoreCase) >= 0)
                        related.Add(n + " " + asm.GetName().Version);
                }
                LogI("[环境] AppDomain 相关程序集（OC2DIY*/LevelEditorStub/Stub_*/CustomStub*）: "
                    + (related.Count > 0 ? string.Join(", ", related.ToArray()) : "（无）"));
            }
            catch (Exception ex)
            {
                LogW("[环境] 环境探测异常（不影响后续扫描）: " + ex.Message);
            }
        }

        /// <summary>列出目录本身及一级子项（目录再展开一层），每项带大小/类型。
        /// 不存在时明确打"不存在"——这是判断 levels 装没装、装哪了的直接证据。</summary>
        private static void DumpDir(string label, string dir)
        {
            if (!Directory.Exists(dir))
            {
                LogI(label + ": 不存在（" + dir + "）");
                return;
            }
            LogI(label + ": 存在（" + dir + "），内容如下");
            var count = 0;
            foreach (var entry in Directory.GetFileSystemEntries(dir))
            {
                if (count++ >= 50)
                {
                    LogI(label + "  …（超过 50 项，截断）");
                    break;
                }
                var name = Path.GetFileName(entry);
                if (Directory.Exists(entry))
                {
                    var sub = Directory.GetFileSystemEntries(entry);
                    var names = new List<string>();
                    for (int i = 0; i < sub.Length && i < 20; i++)
                        names.Add(Path.GetFileName(sub[i]));
                    LogI(label + "  [目录] " + name + "/（" + sub.Length + " 项: "
                        + string.Join(", ", names.ToArray()) + (sub.Length > 20 ? ", …" : "") + "）");
                }
                else
                {
                    LogI(label + "  [文件] " + name + "（" + new FileInfo(entry).Length + " 字节）");
                }
            }
        }

        /// <summary>levels 候选根目录（按优先级）。第一个存在的用作主扫描；
        /// 其余存在的也会补扫（幂等去重），全部不存在则逐个打"不存在"便于定位装机问题。</summary>
        private static List<string> GetLevelsRoots()
        {
            var roots = new List<string>();
            roots.Add(Path.Combine(Path.Combine(Paths.PluginPath, "OC2DIYLevel"), "levels"));
            try
            {
                var sa = Path.Combine(Application.streamingAssetsPath, "OC2DIYLevel");
                roots.Add(Path.Combine(sa, "levels"));
                roots.Add(sa);
            }
            catch { }
            return roots;
        }

        #endregion

        // ============ 关卡目录扫描（v2.1：磁盘 I/O 移出主线程） ============
        //
        // 原实现 ScanOnce/ScanRoot 在【每次场景加载】的主线程上做
        // Directory.GetDirectories + 每个关卡集 File.Exists ×2 + Directory.GetFiles
        // + File.ReadAllText(requires.txt)，关卡集多时直接加长进关卡的卡顿。
        // 现在拆成两段：
        //   ① 后台线程（ThreadPool）：纯 System.IO 的目录枚举与文本读取，
        //      日志行也只是收集进结果对象（BepInEx 日志不跨线程调用）；
        //   ② 主线程（Update.DrainScanResults）：输出日志 + AssetBundle.LoadFromFile
        //      + Assembly.Load —— Unity API 与程序集加载必须留在主线程。
        // levels 候选根目录要读 Application.streamingAssetsPath（Unity API），
        // 因此在主线程算好后作为参数传进后台线程。

        private sealed class ScanResult
        {
            public bool Verbose;
            public string Root;
            public int SetCount;
            public int WithRuntime;
            public readonly List<string> Infos = new List<string>();
            public readonly List<string> Warns = new List<string>();
            /// <summary>待主线程加载的 *_custom_runtime 文件（已按存在性筛过）。</summary>
            public readonly List<string> RuntimeFiles = new List<string>();
            public bool NoRootFound;
        }

        private static readonly Queue<ScanResult> _scanResults = new Queue<ScanResult>();
        private static readonly object _scanLock = new object();
        private static int _scanInFlight;

        /// <summary>发起一次后台扫描（幂等：已有扫描在途则跳过——补扫本就是拾漏，
        /// 丢一次不影响正确性，下次场景加载还会再扫）。</summary>
        private static void BeginScan(bool verbose)
        {
            if (Interlocked.CompareExchange(ref _scanInFlight, 1, 0) != 0)
                return;
            List<string> roots;
            try
            {
                roots = GetLevelsRoots(); // 含 Unity API，必须主线程
            }
            catch (Exception ex)
            {
                _scanInFlight = 0;
                LogW("levels 根目录解析异常: " + ex.Message);
                return;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                ScanResult result;
                try
                {
                    result = ScanFilesystem(roots, verbose);
                }
                catch (Exception ex)
                {
                    result = new ScanResult();
                    result.Verbose = verbose;
                    result.Warns.Add("后台扫描异常: " + ex);
                }
                lock (_scanLock)
                    _scanResults.Enqueue(result);
                _scanInFlight = 0;
            });
        }

        /// <summary>主线程消费扫描结果：输出日志 + 加载新发现的 runtime bundle。</summary>
        private static void DrainScanResults()
        {
            while (true)
            {
                ScanResult result;
                lock (_scanLock)
                {
                    if (_scanResults.Count == 0)
                        return;
                    result = _scanResults.Dequeue();
                }
                try
                {
                    ApplyScanResult(result);
                }
                catch (Exception ex)
                {
                    LogW("扫描结果处理异常: " + ex);
                }
            }
        }

        private static void ApplyScanResult(ScanResult result)
        {
            for (int i = 0; i < result.Warns.Count; i++)
                LogW(result.Warns[i]);
            if (result.Verbose)
            {
                for (int i = 0; i < result.Infos.Count; i++)
                    LogI(result.Infos[i]);
            }
            if (result.NoRootFound)
                return;

            var newlyLoaded = 0;
            for (int i = 0; i < result.RuntimeFiles.Count; i++)
            {
                var f = result.RuntimeFiles[i];
                if (LoadedBundles.Contains(f))
                    continue;
                try
                {
                    LogI("加载自定义 runtime: " + f);
                    var bundle = AssetBundle.LoadFromFile(f);
                    if (bundle == null)
                    {
                        LogW("自定义 runtime 加载失败（LoadFromFile 返回 null）: " + f);
                        continue;
                    }
                    LoadedBundles.Add(f);
                    var dllCount = 0;
                    foreach (var assetPath in bundle.GetAllAssetNames())
                    {
                        if (!assetPath.EndsWith(".dll.bytes", StringComparison.OrdinalIgnoreCase))
                            continue;
                        var asset = bundle.LoadAsset<TextAsset>(assetPath);
                        if (asset == null || asset.bytes == null || asset.bytes.Length == 0)
                        {
                            LogW("  DLL 资产读取失败（非 TextAsset 或空）: " + assetPath);
                            continue;
                        }
                        dllCount++;
                        LogI("  发现 " + assetPath + "（" + asset.bytes.Length + " 字节）");
                        if (LoadFromBytes(assetPath, asset.bytes))
                            newlyLoaded++;
                    }
                    if (dllCount == 0)
                        LogW("  自定义 runtime 内没有任何 *.dll.bytes 资产。bundle 内全部资产: "
                            + string.Join(", ", bundle.GetAllAssetNames()));
                }
                catch (Exception ex)
                {
                    LogW("自定义 runtime 处理异常 " + f + ": " + ex);
                }
            }

            if (result.Verbose || newlyLoaded > 0)
                LogI("扫描汇总 [" + result.Root + "]: 关卡集 " + result.SetCount
                    + " 个，含自定义 runtime " + result.WithRuntime
                    + " 个，本次新加载程序集 " + newlyLoaded + " 个");
        }

        /// <summary>后台线程执行体：纯 System.IO，绝不触碰 Unity API 与 BepInEx 日志。</summary>
        private static ScanResult ScanFilesystem(List<string> roots, bool verbose)
        {
            var result = new ScanResult();
            result.Verbose = verbose;

            string existingRoot = null;
            for (int i = 0; i < roots.Count; i++)
            {
                if (Directory.Exists(roots[i]))
                {
                    existingRoot = roots[i];
                    break;
                }
            }
            if (existingRoot == null)
            {
                result.NoRootFound = true;
                if (verbose)
                {
                    result.Warns.Add("所有 levels 候选目录均不存在（未装 OC2DIYLevel 关卡或目录被挪走），逐个列出供排查:");
                    for (int i = 0; i < roots.Count; i++)
                        result.Warns.Add("  不存在: " + roots[i]);
                }
                return result;
            }
            result.Root = existingRoot;
            if (verbose)
            {
                result.Infos.Add("扫描关卡集目录: " + existingRoot
                    + (existingRoot == roots[0]
                        ? "（主路径）"
                        : "（⚠ 备选路径，主路径 " + roots[0] + " 不存在——若 OC2DIYLevel 改版挪了 levels 位置请告知研发同步本 loader）"));
            }

            ScanRootOnWorker(existingRoot, result);

            // 备选根也存在时补扫（幂等：主线程侧 LoadedBundles 去重），拾漏混合安装
            for (int i = 0; i < roots.Count; i++)
            {
                if (roots[i] == existingRoot || !Directory.Exists(roots[i]))
                    continue;
                ScanRootOnWorker(roots[i], result);
            }
            return result;
        }

        private static void ScanRootOnWorker(string levelsRoot, ScanResult result)
        {
            string[] setDirs;
            try { setDirs = Directory.GetDirectories(levelsRoot); }
            catch (Exception ex)
            {
                result.Warns.Add("枚举关卡集目录失败 " + levelsRoot + ": " + ex.Message);
                return;
            }
            foreach (var setDir in setDirs)
            {
                result.SetCount++;
                var setName = Path.GetFileName(setDir);

                // 旧版 runtime 文件（含旧每集 Stub_<set> 代码）——统一单程序集重构后
                // 不再识别，显式告警提示重新导出（新代码走依赖包统一运行时）。
                if (File.Exists(Path.Combine(setDir, "runtime")))
                    result.Warns.Add("  [" + setName + "] 检测到旧版 runtime 文件，已忽略（新版由依赖包统一运行时提供，"
                        + "请用新编辑器重新导出该关卡集一次）");

                // 版本门控：requires.txt（= 关卡集要求的依赖包 SSOT 版本）
                var requires = ReadRequires(setDir);
                if (!string.IsNullOrEmpty(requires) && CompareSemver(PluginVersion, requires) < 0)
                {
                    result.Warns.Add("  [" + setName + "] 依赖包版本过旧：已装 " + PluginVersion + " < 关卡要求 " + requires
                        + "，跳过该集 stub 支持（关卡本体仍可加载）。请更新 OC2DIYLevelRuntimeWLoader 依赖包。");
                    continue;
                }

                // 每关卡自定义 runtime（预留通道）：只认 *_custom_runtime。
                string[] files;
                try { files = Directory.GetFiles(setDir); }
                catch { continue; }
                foreach (var f in files)
                {
                    if (!Path.GetFileName(f).EndsWith(CustomRuntimeSuffix, StringComparison.OrdinalIgnoreCase))
                        continue;
                    result.WithRuntime++;
                    result.RuntimeFiles.Add(f);
                }
            }
        }

        /// <summary>读关卡集 requires.txt（依赖版本门控）。不存在/读失败返回 null（不门控）。</summary>
        private static string ReadRequires(string setDir)
        {
            try
            {
                var p = Path.Combine(setDir, "requires.txt");
                if (!File.Exists(p))
                    return null;
                var s = File.ReadAllText(p);
                return s != null ? s.Trim() : null;
            }
            catch { return null; }
        }

        /// <summary>semver 比较（a &lt; b 返回 &lt;0；相等 0；a &gt; b 返回 &gt;0）。仅比较数字段，
        /// 缺段按 0，非数字段忽略——门控只需大小关系，足够稳健。</summary>
        private static int CompareSemver(string a, string b)
        {
            var pa = SplitVer(a);
            var pb = SplitVer(b);
            var n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                var va = i < pa.Length ? pa[i] : 0;
                var vb = i < pb.Length ? pb[i] : 0;
                if (va != vb)
                    return va < vb ? -1 : 1;
            }
            return 0;
        }

        private static int[] SplitVer(string v)
        {
            if (string.IsNullOrEmpty(v))
                return new int[0];
            var segs = v.Split('.');
            var r = new int[segs.Length];
            for (int i = 0; i < segs.Length; i++)
            {
                int x;
                var digits = "";
                foreach (var ch in segs[i])
                {
                    if (ch >= '0' && ch <= '9') digits += ch;
                    else break;
                }
                int.TryParse(digits, out x);
                r[i] = x;
            }
            return r;
        }

        /// <summary>返回 true 表示本次真正完成了程序集加载（重复跳过/失败返回 false）。</summary>
        private static bool LoadFromBytes(string displayName, byte[] raw)
        {
            try
            {
                var asm = Assembly.Load(raw);
                var name = asm.GetName().Name;
                if (LoadedNames.Contains(name))
                {
                    LogI("程序集已加载过，跳过重复加载: " + name);
                    return false;
                }
                LoadedNames.Add(name);
                PendingRaw.Remove(raw);
                // 内容自检：确认关卡程序集里确有 CustomStub.RandomCrate
                var crateType = asm.GetType("CustomStub.RandomCrate", false);
                int typeCount;
                try { typeCount = asm.GetTypes().Length; }
                catch { typeCount = -1; }
                LogI("已加载关卡程序集: " + name + "（来自 " + displayName + "，类型数 "
                    + (typeCount >= 0 ? typeCount.ToString() : "未知") + "）"
                    + (crateType != null ? "，CustomStub.RandomCrate ✓" : "，⚠ 未找到 CustomStub.RandomCrate（旧版或空程序集）"));
                // CustomStub EntryPoint 引导（v1.4.0+）：新 stub 组件（TimedSwitch /
                // PushablePot / VoidFall / SwitchReenable / WorldMapDressing /
                // UtensilTiming（锅具时间）/ TerminalGuard（未绑定终端防线）/
                // Harmony KillPlane 补丁）的统一安装器。
                // 纯反射约定调用，loader 与 stub 零编译依赖；
                // EntryPoint 内部自带哨兵幂等（多关卡集同名程序集只装一次）。
                var entryPoint = asm.GetType("CustomStub.EntryPoint", false);
                if (entryPoint != null)
                {
                    try
                    {
                        var install = entryPoint.GetMethod("Install", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                        if (install != null)
                        {
                            var installed = install.Invoke(null, null);
                            // 返回 false 有两种情况：已有其它实例（正常），或 Install
                            // 内部抛异常被自己吞掉（此时上一行必有「安装异常」告警）。
                            // 别把后者也说成「已有实例」——2026-09-15 事故里这句话
                            // 差点把 GameApi 静态构造失败掩盖过去。
                            LogI("CustomStub.EntryPoint.Install: "
                                + ((installed is bool && (bool)installed)
                                    ? "已安装"
                                    : "未由本次调用安装（已有实例，或安装异常——见上一行告警）"));
                        }
                    }
                    catch (Exception ex)
                    {
                        LogW("CustomStub.EntryPoint.Install 调用失败: " + ex.Message);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                PendingRaw.Add(raw);
                LogW("Assembly.Load 失败（保留字节供 AssemblyResolve 兜底）" + displayName + ": " + ex);
                return false;
            }
        }

        private static readonly HashSet<string> ReportedResolveMisses = new HashSet<string>(StringComparer.Ordinal);

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var simpleName = args.Name.Split(',')[0].Trim();
            foreach (var raw in PendingRaw)
            {
                try
                {
                    var asm = Assembly.Load(raw);
                    if (asm.GetName().Name == simpleName)
                    {
                        PendingRaw.Remove(raw);
                        LoadedNames.Add(simpleName);
                        if (_log != null)
                            LogI("AssemblyResolve 兜底加载: " + simpleName);
                        return asm;
                    }
                }
                catch
                {
                    // 尝试下一个
                }
            }
            // 关卡程序集相关的解析失败只报一次（缺引用会让组件静默失效，必须可见）
            if ((simpleName.StartsWith("Stub_", StringComparison.OrdinalIgnoreCase)
                    || simpleName.IndexOf("CustomStub", StringComparison.OrdinalIgnoreCase) >= 0
                    || simpleName.IndexOf("LevelEditorStub", StringComparison.OrdinalIgnoreCase) >= 0)
                && ReportedResolveMisses.Add(simpleName) && _log != null)
            {
                LogW("AssemblyResolve 未命中: " + simpleName
                    + "（程序集未加载且无兜底字节——若这是关卡程序集，检查 levels/<set>/runtime 是否随包安装）");
            }
            return null;
        }

        #region 场景加载补扫（MonoScript 解析失败的保险；自愈已收编 CustomStub.EntryPoint）

        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoadedHeal;
        }

        /// <summary>每次场景加载：先确保依赖/统一运行时已加载，再幂等补扫关卡目录的
        /// *_custom_runtime（拾漏：启动后才安装/更新的关卡集）。场景自愈（RandomCrate|
        /// 等 tag → 挂组件）统一由 CustomStub.EntryPoint 承担，loader 不再自行 HealScene。
        /// v2.1：补扫改为后台线程（BeginScan），程序集清单只在数量变化时打。</summary>
        private static int _lastReportedAsmCount = -1;

        private static void OnSceneLoadedHeal(Scene scene, LoadSceneMode mode)
        {
            try
            {
                LoadDependenciesAndRuntime();
                if (LoadedNames.Count != _lastReportedAsmCount)
                {
                    _lastReportedAsmCount = LoadedNames.Count;
                    LogI("场景加载: " + scene.name + "（mode=" + mode + "，已加载程序集 " + LoadedNames.Count + " 个"
                        + (LoadedNames.Count > 0 ? ": " + string.Join(", ", ToArray(LoadedNames)) : "") + "）");
                }
                else
                {
                    LogV("场景加载: " + scene.name + "（mode=" + mode + "，已加载程序集 " + LoadedNames.Count + " 个）");
                }
                BeginScan(false);
            }
            catch (Exception ex)
            {
                if (_log != null)
                    LogW("场景补扫异常 [" + scene.name + "]: " + ex);
            }
        }

        private static string[] ToArray(HashSet<string> set)
        {
            var arr = new string[set.Count];
            set.CopyTo(arr);
            return arr;
        }

        /// <summary>关卡程序集（RandomCrate 等）的日志桥：它们编不进 BepInEx 插件
        /// 程序集，Debug.Log 又可能被过滤——反射调用本方法转发到插件日志通道，
        /// 与 loader 日志在同一 [OC2 LevelRuntime Loader] 来源下可见。
        /// v1.5.2：桥接消息统一保证带 [Stub:...] 段（旧版 stub 没带就补 [Stub]），
        /// 与 loader 原生日志（无此段）一眼区分。</summary>
        public static void LogFromCrate(string message, bool warn)
        {
            if (_log == null || message == null)
                return;
            if (message.IndexOf("[Stub:", StringComparison.Ordinal) < 0)
                message = "[Stub] " + message;
            if (warn)
                LogW(message);
            else
                LogI(message);
        }

        private static Type FindLoadedType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null)
                    return t;
            }
            return null;
        }

        #endregion
    }
}
