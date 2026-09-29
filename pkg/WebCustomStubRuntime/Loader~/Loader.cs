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
    ///   Loader.dll + webcustomstub_runtime（统一运行时 bundle） + commonW1/commonW2/...。
    /// 关卡集仍在 BepInEx/plugins/OC2DIYLevel/levels/&lt;set&gt;/（模组固定读取路径）。
    ///
    /// 职责（统一单程序集重构后）：
    ///  1. 启动扫描（后台线程）汇总 levels/&lt;set&gt;/stub_levels.txt 逐关卡清单 →
    ///     WebManifest。清单为空 = 完全休眠（零加载零事件零介入）；非空 = 激活：
    ///     挂 AssemblyResolve + 从自身目录把所有 commonW+数字 bundle LoadFromFile
    ///     常驻（幂等）+ 加载统一运行时 bundle webcustomstub_runtime →
    ///     Assembly.Load(WebCustomStubRuntime) → 反射 CustomStub.EntryPoint.Install()
    ///     （EntryPoint 反射读 WebManifest 做逐关卡闸门）。原版 bundle 仍靠 OC2DIYLevel。
    ///  2. 每关卡集 *_custom_runtime 文件（预留：某关卡专属代码）仍支持加载；
    ///     旧版 runtime 文件（含旧每集 Stub_&lt;set&gt; 代码）一律忽略并告警（不识别旧代码）。
    ///  3. 版本门控：关卡集 requires.txt（= 依赖包 SSOT 版本）与自身 PluginVersion semver
    ///     比较——已装依赖 &gt;= requires 才提供 stub 支持，&lt; 则告警跳过（关卡本体仍加载）。
    ///
    /// 场景自愈（RandomCrate| 等 tag）统一收编于 CustomStub.EntryPoint（本 loader 不再
    /// 自行 HealScene），loader 只负责程序集/依赖加载 + 每次场景加载幂等补扫。
    ///
    /// v3.5.0（2026-09-28 零介入铁律 · 清单驱动的逐关卡按需挂载）：
    ///  - 铁律：未使用 web 导出的关卡（官方图/旧导出集/无自定义关卡），本 loader 及其
    ///    注入的全部运行时对游戏零介入——不 hook 任何函数、不装任何补丁、不做任何
    ///    场景扫描/探测。已装 web 集但当前玩非 web 场景时同样零介入。
    ///  - 机制：导出器逐场景扫描写出 levels/&lt;set&gt;/stub_levels.txt（每行
    ///    "&lt;关卡&gt;|&lt;特征,...&gt;"）；loader 启动扫描汇总为 WebManifest
    ///    （"集|关卡|特征,..."）注入 CustomStub.EntryPoint（反射读 WebManifest 字段，
    ///    镜像 StubLog→LogFromCrate 桥模式）。EntryPoint 按 scene.path 解析 (集,关卡)
    ///    查清单：未命中直接休眠归零；命中按特征挂载（KillPlane 仅 pushable、ticker
    ///    子系统轮询各按特征）。编辑器 Play（无 loader）= 无约束模式，行为不变。
    ///  - 启动重排：Awake 不再预载依赖/运行时、不挂 AssemblyResolve——先等后台扫描
    ///    汇总清单；清单为空（无任何 web stub 关卡）= 完全休眠，一个字节都不加载。
    ///  - 删除 v3.1.0/v3.2.1 的 RecipeHelper 兼容护栏（按铁律「不主动 hook 非 web
    ///    关卡路径的函数」；旧导出集混装崩溃回归 = 用最新编辑器重新导出解决）。
    ///  - 兼容期（3.5.0 ≤ PluginVersion &lt; 4.0.0，2026-09-28 用户决策）：旧版导出集
    ///    （有 requires.txt、无 stub_levels.txt，如 3.2.1 导出）生成 "集|*|legacy"
    ///    兼容条目，EntryPoint 维持 v3.4 行为（探测+tag 自愈）——已分发旧集无需
    ///    重导出。v4.0.0 起严格按 stub_levels.txt 清单（届时旧集需重新导出一次）。
    /// v3.3.2（2026-09-25 真机按钮永红修复）：仅随统一运行时同步版本号（relay 接管
    ///     按钮 Disable/Reset 生命周期 + 共轭双锁语义，全在 CustomStub 侧）。
    /// v3.3.1（2026-09-25 真机按钮锁死修复）：仅随统一运行时同步版本号
    ///     （BLRelay/AnimGridMemberSync 改 tag/扫描自愈——烘焙组件在真机是
    ///     Missing Script 死件；自愈全在 CustomStub 侧，loader 无改动）。
    ///     ⚠ 按钮联动关卡必须用 &gt;= 本版依赖包重新导出。
    /// v3.3.0（2026-09-24 按钮动画联动 v3）：仅随统一运行时同步版本号（新增
    ///     ButtonLogicRelay 分发器 / AnimGridMemberSync 格子换装 / ConveyorDirectionSync
    ///     停稳+空闲刷新时序，自愈与分发全在 CustomStub 侧，loader 无改动）。
    ///     ⚠ 按钮触发动画组的关卡需要 &gt;= 本版依赖包（旧版按钮链路的 SMB 分发
    ///     不持久化，运行期按压无响应）。
    /// v3.0.0（2026-09-21）：目录扫描简化为启动时一次，场景切换不再重复遍历所有地图集；
    /// 只加载实际存在的 *_custom_runtime，普通地图不产生额外扫描开销；新增同名程序集
    /// 来源/版本冲突诊断。
    /// v3.2.1（2026-09-21 兼容护栏修正）：v3.1.0 的护栏在本环境 HarmonyX（BepInEx
    ///     5.4.22）上「触发了但没拦住」——前缀改 __args 不回写，null 仍进原方法照崩
    ///     （debug_20260921 23:53 实测）。改为：检出 null 节点即跳过原方法，反射重建
    ///     滤空查找表（Equals 去重 + m_amountAllowed 累加 + m_lookupArray 语义原样）；
    ///     重建异常退空查找表保会话。另增关卡集/关卡定位上下文（SetupConfig /
    ///     SetupSceneDirectoryData(LevelSetInfoSO) 前缀），告警与玩家提示明确指出
    ///     哪个关卡集的哪一关哪些菜谱失效；同（关卡,菜谱）组合每会话只完整告警一次。
    /// v3.1.0（2026-09-21 旧版关卡包向下兼容）：OC2DIYLevel.RecipeHelper 的菜谱食材
    ///     查找表（GetOrderToPrefabLookup）遇到无法解析的候选（旧版编辑器导出的
    ///     关卡集与新版依赖包混装时，跨 bundle 引用可能落空 → OrderDefinitionNode
    ///     为 null）会在 Dictionary.set_Item 抛 ArgumentNullException，把整个
    ///     StartEmptySession 炸成「严重错误」弹窗。本版给该方法装 Harmony 前缀护栏：
    ///     过滤 null 节点 + 显式告警（提示重导出），关卡仍可进入——降级不致命。
    /// v2.3.0（2026-09-19）：仅随统一运行时同步版本号（新增老鼠偷食材 RatHeist，
    ///     自愈与状态机全在 CustomStub 侧，loader 无改动）。
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
        public const string PluginVersion = "3.5.1";

        /// <summary>统一运行时 bundle 文件名（依赖包内，固定；不与关卡目录下的
        /// *_custom_runtime 混淆，也绝不叫裸 runtime）。</summary>
        private const string RuntimeBundleFileName = "webcustomstub_runtime";

        /// <summary>每关卡自定义 runtime 文件后缀（预留通道；不识别旧版 runtime）。</summary>
        private const string CustomRuntimeSuffix = "_custom_runtime";

        /// <summary>清单兼容截止版本（2026-09-28 用户决策）：PluginVersion &lt; 本值时，
        /// 旧版导出集（有 requires.txt、无 stub_levels.txt）生成兼容条目 "集|*|legacy"，
        /// EntryPoint 对其维持 v3.4 行为（探测 + tag 自愈）——**3.2.1 等已分发关卡集
        /// 无需重导出即可继续工作**；到达 4.0.0 后不再生成兼容条目，严格按
        /// stub_levels.txt 逐关卡清单（届时旧集需重新导出一次）。
        /// ⚠ 长期准则（v3.5.0 起，详见 .opencode/skills/oc2-customstub「Loader 版本
        /// 兼容规范」）：**任一版本必须保留对上一版导出格式的兼容**——本常量推移时，
        /// 被越过的「上一版格式」仍必须兼容（如 4.0.0 移除 ≤3.4 legacy 时，3.5.0 的
        /// stub_levels.txt 清单格式继续支持）；不允许当版直接砍掉上一版格式。</summary>
        private const string LegacyCompatCutoff = "4.0.0";

        private static ManualLogSource _log;
        private static readonly List<byte[]> PendingRaw = new List<byte[]>();
        private static readonly HashSet<string> LoadedNames = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> LoadedBundles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> LoadedSources = new Dictionary<string, string>(StringComparer.Ordinal);
        private static bool _startupScanDone;
        private static bool _filesystemScanDone;
        private static bool _depsLoaded;
        private static bool _resolveHooked;

        /// <summary>web stub 关卡清单（v3.5.0，stub 侧 CustomStub.EntryPoint 反射读取，
        /// 与 StubLog 读取 LogFromCrate 同款桥接方向）。每条 "集|关卡|特征1,特征2,..."，
        /// 由启动扫描汇总 levels/&lt;set&gt;/stub_levels.txt 而来。清单为空 = 本机无任何
        /// web 导出 stub 关卡 = 加载器整体休眠。EntryPoint 据此做逐关卡闸门：清单未命中
        /// 的场景（官方图/旧集/未用 CustomStub 的 web 关卡）一律零介入。</summary>
        public static string[] WebManifest = new string[0];

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

        private static void LogP(string message)
        {
            if (_log != null)
                _log.LogWarning(Prefix() + "[PLAYER] " + message);
        }

        #endregion

        private void Awake()
        {
            _log = Logger;
            _cfgVerbose = Config.Bind("Logging", "Verbose", false,
                "输出 CustomStub 运行时与本加载器的诊断级日志（掷骰/取出、逐物体自愈、反射自检、"
                + "目录清单等）。默认关闭——这些日志在稳态游玩期是每秒数条的纯开销。排障时开启。");
            // v3.5.0 零介入铁律：Awake 不再预载依赖/统一运行时、不挂 AssemblyResolve。
            // 先等启动后台扫描汇总 levels/<set>/stub_levels.txt 清单，只有存在
            // web 导出的 stub 关卡（清单非空）才激活（ApplyScanResult）——未用 web
            // 导出的环境自此一个字节都不加载、一个事件都不挂。
            // 激活时机的时序安全性：扫描在 Update 首帧发起、数帧内 Drain，仍远早于
            // 任何关卡场景加载（主菜单在前）；EntryPoint.Install 只需早于场景里
            // MonoScript 的程序集解析即可。
            LogI("v" + PluginVersion + " ready（清单驱动：仅当存在 web 导出 stub 关卡时才加载依赖/运行时）"
                + (VerboseEnabled ? "｜诊断日志已开启" : ""));
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

        /// <summary>从自身目录加载所有 commonW+数字 bundle（幂等：已在全局表则跳过，不 Unload）
        /// + 统一运行时 webcustomstub_runtime → Assembly.Load → EntryPoint.Install。
        /// v3.5.0：清单为空（休眠模式）时一个字节都不加载。</summary>
        private static void LoadDependenciesAndRuntime()
        {
            if (_depsLoaded)
                return;
            if (WebManifest.Length == 0)
                return; // 零介入休眠：无任何 web stub 关卡，不加载依赖/运行时
            _depsLoaded = true;
            var dir = OwnDir();
            if (string.IsNullOrEmpty(dir))
            {
                LogW("无法解析 Loader.dll 所在目录，跳过依赖/运行时加载（自愈补扫仍会尝试）");
                return;
            }
            LogI("依赖包目录: " + dir);
            LogI("[断点:依赖] 开始检查 commonW1、commonW2、commonW3... 和统一运行时");

            // 1. 动态发现 commonW1、commonW2、commonW3...（供 stub 组件按名解析；
            // 不把未来新增的依赖包写死在 Loader 中）。
            var dependencyNames = GetDependencyBundleNames(dir);
            if (dependencyNames.Count == 0)
                LogI("  未找到 commonW* 依赖包（可选），跳过");
            foreach (var name in dependencyNames)
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
                    {
                        LogW("  " + name + " 加载失败（LoadFromFile 返回 null）: " + path);
                        LogP("依赖资源 " + name + " 加载失败，相关自定义关卡功能可能无法正常显示。请重新安装依赖包。");
                    }
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
                    LogP("未找到统一运行时文件，随机箱、火锅等自定义玩法无法启用。请更新 OC2DIYLevelRuntimeWLoader 依赖包。");
                    return;
                }
                if (LoadedBundles.Contains(rtPath))
                    return;
                var bundle = AssetBundle.LoadFromFile(rtPath);
                if (bundle == null)
                {
                    LogW("统一运行时 bundle 加载失败（LoadFromFile 返回 null）: " + rtPath);
                    LogP("统一运行时加载失败，自定义玩法无法启用。请重新安装依赖包。");
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
                {
                    LogW("统一运行时 bundle 内无 *.dll.bytes（打错包或旧包？）");
                    LogP("统一运行时文件内容不完整，自定义玩法无法启用。请重新导出或安装最新依赖包。");
                }
                LogI("[断点:依赖] 统一运行时加载完成，程序集数量=" + loaded);
            }
            catch (Exception ex)
            {
                LogW("统一运行时加载异常: " + ex);
                LogP("统一运行时加载时发生错误，自定义玩法可能无法使用。请将 logs 目录发送给开发者。原因: " + ex.Message);
            }
        }

        /// <summary>发现自身目录下所有严格匹配 commonW+数字的 bundle。
        /// 采用数字排序，确保 commonW10 排在 commonW9 之后；扩展名文件不会被误加载。</summary>
        private static List<string> GetDependencyBundleNames(string dir)
        {
            var result = new List<string>();
            try
            {
                var files = Directory.GetFiles(dir);
                for (int i = 0; i < files.Length; i++)
                {
                    var name = Path.GetFileName(files[i]);
                    if (string.IsNullOrEmpty(name) || !name.StartsWith("commonW", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var suffix = name.Substring("commonW".Length);
                    if (suffix.Length == 0)
                        continue;
                    int number;
                    if (!int.TryParse(suffix, out number) || number < 1)
                        continue;
                    result.Add("commonW" + number);
                }
            }
            catch (Exception ex)
            {
                LogW("  扫描 commonW* 依赖包失败: " + ex.Message);
                return result;
            }

            result.Sort(delegate(string left, string right)
            {
                return ParseCommonWIndex(left).CompareTo(ParseCommonWIndex(right));
            });
            var unique = new List<string>();
            for (int i = 0; i < result.Count; i++)
            {
                if (i == 0 || !string.Equals(result[i], result[i - 1], StringComparison.OrdinalIgnoreCase))
                    unique.Add(result[i]);
            }
            return unique;
        }

        private static int ParseCommonWIndex(string name)
        {
            int number;
            return int.TryParse(name.Substring("commonW".Length), out number) ? number : int.MaxValue;
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
                    LogP("扫描自定义关卡时发生错误，请将 logs 目录发送给开发者。原因: " + ex.Message);
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
            // v3：目录扫描只在启动阶段做一次。普通地图没有 custom runtime，场景切换不需要
            // 反复枚举所有地图集；加载器的职责是提前加载已安装的 runtime，而不是监视磁盘。
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
            /// <summary>v3.5.0 web stub 关卡清单：每条 "集|关卡|特征1,特征2,..."
            /// （来自 levels/&lt;set&gt;/stub_levels.txt）。为空 = 休眠模式。</summary>
            public readonly List<string> ManifestEntries = new List<string>();
            /// <summary>清单命中的关卡集数（诊断用）。</summary>
            public int SetsWithManifest;
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
            _filesystemScanDone = true;
            for (int i = 0; i < result.Warns.Count; i++)
                LogW(result.Warns[i]);
            if (result.Verbose)
            {
                for (int i = 0; i < result.Infos.Count; i++)
                    LogI(result.Infos[i]);
            }
            if (result.NoRootFound)
            {
                LogP("未找到自定义关卡目录。若您安装了自定义关卡，请确认它位于 BepInEx/plugins/OC2DIYLevel/levels/。");
                return;
            }

            // ---- v3.5.0 闸门：清单为空 = 本机没有任何 web 导出的 stub 关卡 ----
            // → 加载器整体休眠：不加载 commonW*/统一运行时/自定义 runtime，
            //   不挂 AssemblyResolve（此前未挂过）——对游戏零介入。
            WebManifest = result.ManifestEntries.ToArray();
            if (WebManifest.Length == 0)
            {
                LogI("未发现任何 web 导出的 stub 关卡（stub_levels.txt 无条目）——加载器休眠，对游戏零介入"
                    + (result.SetCount > 0 ? "（已装关卡集 " + result.SetCount + " 个均未使用 CustomStub）" : ""));
                return;
            }

            // 清单非空 → 激活：挂 AssemblyResolve（引用顺序兜底）+ 预载依赖/统一运行时。
            // 预载保持启动期（决策已确认）：程序集必须先于 web 关卡场景加载进 AppDomain，
            // 否则场景 MonoScript 解析竞态失败；纯数据/程序集驻留不 hook 任何函数。
            if (!_resolveHooked)
            {
                _resolveHooked = true;
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            }
            var legacyCount = 0;
            for (int i = 0; i < WebManifest.Length; i++)
            {
                if (WebManifest[i] != null && WebManifest[i].EndsWith("|*|legacy", StringComparison.Ordinal))
                    legacyCount++;
            }
            LogI("stub 关卡清单命中 " + (WebManifest.Length - legacyCount) + " 关（分布于 "
                + result.SetsWithManifest + " 个关卡集）"
                + (legacyCount > 0 ? "，另含 " + legacyCount + " 个旧版导出集（兼容模式：探测+自愈）" : "")
                + "——激活依赖/运行时加载");
            try
            {
                LoadDependenciesAndRuntime();
            }
            catch (Exception ex)
            {
                LogW("依赖/运行时加载异常: " + ex);
                LogP("自定义关卡运行组件启动失败，部分自定义玩法可能无法使用。请将 logs 目录发送给开发者。原因: " + ex.Message);
            }

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
            LogI("[断点:扫描] 关卡扫描处理完成，本次加载程序集=" + newlyLoaded);
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

                // v3.5.0 逐关卡清单：web 导出（=有 requires.txt）且门控通过的集必有
                // stub_levels.txt（每行 "<关卡>|<特征,...>"，'#' 注释/空行忽略）。
                // 旧版导出（无 stub_levels.txt）：兼容期（< LegacyCompatCutoff=4.0.0）
                // 生成 "集|*|legacy" 兼容条目——EntryPoint 对该集维持 v3.4 行为
                // （探测+自愈），旧集无需重导出；4.0.0 起不再生成 → 零介入 + 提示重导出。
                // 非 web 集（无 requires.txt）本就不归本加载器管。
                var manifestPath = Path.Combine(setDir, "stub_levels.txt");
                if (File.Exists(manifestPath))
                {
                    var added = ReadStubLevels(setName, manifestPath, result);
                    if (added > 0)
                        result.SetsWithManifest++;
                    else if (result.Verbose)
                        result.Infos.Add("  [" + setName + "] stub 清单存在但无条目（该集未用 CustomStub）");
                }
                else if (!string.IsNullOrEmpty(requires))
                {
                    if (CompareSemver(PluginVersion, LegacyCompatCutoff) < 0)
                    {
                        result.ManifestEntries.Add(setName + "|*|legacy");
                        result.SetsWithManifest++;
                        result.Infos.Add("  [" + setName + "] 旧版导出（无 stub_levels.txt）→ 兼容模式（探测+自愈，同 v3.4 行为，无需重导出）；"
                            + "v4.0.0 起将严格按清单，届时请重新导出");
                    }
                    else
                    {
                        result.Warns.Add("  [" + setName + "] 旧版导出（无 stub_levels.txt），CustomStub 运行时对该集零介入；"
                            + "请用最新编辑器重新导出该关卡集以启用自定义玩法运行时支持");
                    }
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

        /// <summary>v3.5.0 读关卡集 stub_levels.txt → 汇总进扫描结果。
        /// 每行 "&lt;关卡&gt;|&lt;特征1,特征2,...&gt;"（特征段可为空 = 仅自愈无 ticker/补丁需求）；
        /// '#' 开头注释与空行忽略；格式非法的行跳过并告警。返回有效条目数。</summary>
        private static int ReadStubLevels(string setName, string manifestPath, ScanResult result)
        {
            int added = 0;
            try
            {
                var lines = File.ReadAllLines(manifestPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                        continue;
                    var sep = line.IndexOf('|');
                    if (sep <= 0 || sep >= line.Length - 1)
                    {
                        result.Warns.Add("  [" + setName + "] stub_levels.txt 第 " + (i + 1) + " 行格式非法（应为 关卡|特征,...）: "
                            + line + "，已跳过");
                        continue;
                    }
                    var level = line.Substring(0, sep);
                    if (level.IndexOf('|') >= 0 || level.IndexOf('/') >= 0)
                    {
                        result.Warns.Add("  [" + setName + "] stub_levels.txt 第 " + (i + 1) + " 行关卡名非法: " + level + "，已跳过");
                        continue;
                    }
                    result.ManifestEntries.Add(setName + "|" + line);
                    added++;
                }
            }
            catch (Exception ex)
            {
                result.Warns.Add("  [" + setName + "] stub_levels.txt 读取失败（该集按旧版处理，零介入）: " + ex.Message);
            }
            return added;
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
                    string previous;
                    LoadedSources.TryGetValue(name, out previous);
                    var previousVersion = "未知";
                    try
                    {
                        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            if (loaded.GetName().Name == name)
                            {
                                previousVersion = loaded.GetName().Version != null
                                    ? loaded.GetName().Version.ToString() : "未知";
                                break;
                            }
                        }
                    }
                    catch { }
                    LogW("程序集冲突，保留已加载版本: " + name
                        + "，已加载来源=" + (previous ?? "未知")
                        + "，候选来源=" + displayName
                        + "，已加载版本=" + previousVersion
                        + "，候选版本=" + (asm.GetName().Version != null
                            ? asm.GetName().Version.ToString() : "未知"));
                    return false;
                }
                LoadedNames.Add(name);
                LoadedSources[name] = displayName;
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

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
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
                LogP("关卡代码程序集加载失败，相关自定义玩法可能退化。请确认导出的关卡包和运行时依赖包均为最新版本。程序集: " + simpleName);
            }
            return null;
        }

        #region 场景加载补扫（MonoScript 解析失败的保险；自愈已收编 CustomStub.EntryPoint）

        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoadedHeal;
        }

        /// <summary>每次场景加载：先确保依赖/统一运行时已加载（休眠模式下为空操作），
        /// 再幂等补扫关卡目录的 *_custom_runtime（拾漏：启动后才安装/更新的关卡集）。
        /// 场景自愈（RandomCrate| 等 tag → 挂组件）与逐关卡闸门统一由
        /// CustomStub.EntryPoint 承担（反射读 WebManifest）。v3.5.0：休眠模式
        /// （清单为空）下只保留补扫重试，日志降级 verbose。</summary>
        private static int _lastReportedAsmCount = -1;

        private static void OnSceneLoadedHeal(Scene scene, LoadSceneMode mode)
        {
            try
            {
                if (!_filesystemScanDone)
                    BeginScan(false);
                if (WebManifest.Length == 0)
                {
                    LogV("场景加载: " + scene.name + "（清单为空=休眠模式，零介入）");
                    return;
                }
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
                // v3：启动扫描已经覆盖所有关卡集。场景切换只确保统一依赖已加载，
                // 不再次触发全量磁盘扫描；避免地图集合变多后进场景变慢。
            }
            catch (Exception ex)
            {
                if (_log != null)
                    LogW("场景补扫异常 [" + scene.name + "]: " + ex);
                LogP("进入关卡时发生自定义组件加载错误。关卡: " + scene.name + "，原因: " + ex.Message);
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
