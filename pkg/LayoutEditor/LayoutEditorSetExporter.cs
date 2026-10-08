using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
/// zip 下载同样不依赖主线程泵。
///
/// 统一运行时自动编译闭环（RunExport 闸口）：导出启动时若母本源码比
/// WebCustomStubRuntime.dll 新（外部改码尚未编译），经 RuntimeReadyGate 钩子自动
/// 触发编译，任务参数持久化到 SessionState 挂起；编译引发的域重载后由本类
/// [InitializeOnLoad] 续跑器自动重启导出——不再报「请等 Unity 编译结束后重试」。
///
/// 逐关卡选择性导出（v1 仅单集）：selectedLevels = 场景名清单，null/空 = 全量。
/// 实现要点：① prepare 只打开入选场景（被排除场景不打开不准备；其落盘场景由
/// 写回链路保证无临时伪 prefab 实例，磁盘增量构建安全，且绝不进 zip）；② 构建前把
/// LevelSetInfoSO.levelInfos 临时过滤为入选关卡（玩家侧 OC2DIYLevel 按 levelInfos
/// 枚举关卡，只从 zip 剔除 s_* 会留下打不开的死关），构建后立即还原；③ 崩溃自愈：
/// 改写前先做磁盘级备份（LayoutEditorExports/._setinfo_backup_&lt;set&gt;），域加载 /
/// 下次导出前检测残留备份即还原；④ 被排除场景保留 bundle 名照常增量构建（近零
/// 成本），仅 zip 组装时过滤 s_* 文件——构建产物目录始终保持全量，无需临时目录。</summary>
[InitializeOnLoad]
public static class LayoutEditorSetExporter
{
    private const string LevelSetsRoot = "Assets/LevelSets";
    private const string BundlesRoot = "Assets/AssetBundles";
    /** zip 输出目录（项目根、Assets 外，避免 Unity 生成 .meta）。 */
    public const string ExportRootDir = "LayoutEditorExports";

    /// <summary>导出前置扩展钩子（参数 = 关卡集名）。解耦点：无订阅者时行为不变，
    /// 例如 CustomStub 的 Stub DLL staging（LayoutStubDllBuilder）经此接入。</summary>
    public static Action<string> BeforeBuild;

    /// <summary>统一运行时就绪闸口钩子（导出启动时调用；LayoutStubDllBuilder 注册）。
    /// 返回 null=就绪；"compiling"=已触发自动编译（或正在编译），导出任务经
    /// SessionState 挂起、域重载后由本类自动续跑；其他=不可自动恢复的原因（导出
    /// 置 error 展示）。无订阅者（CustomStub 未安装）时视为就绪。</summary>
    public static Func<string> RuntimeReadyGate;

    /// <summary>导出前孤儿脚本引用自动清理钩子（参数 = 待导出场景路径列表）。
    /// 返回清理汇总文案（进导出日志；null/空=没有发现）。CustomStubOrphanRepair
    /// 注册；安全策略：删僵尸/空组件、复活合法载体，未知签名与缺失 prefab 实例
    /// 只报告不阻断导出。无订阅者时导出行为不变。</summary>
    public static Func<List<string>, string> OrphanCleanHook;

    /// <summary>双 stub 道具自动修复钩子（2026-10-06 真机卡加载事故）：导出 prepare 前
    ///  由 LayoutEditorDualStubRepair 订阅（打开场景→PromoteDerivedStub→备份+保存，幂等）。
    ///  无订阅者时导出行为不变。</summary>
    public static Func<List<string>, string> DualStubCleanHook;

    /// <summary>统一运行时 DLL 编译状态钩子（deps 清单用；LayoutStubDllBuilder 注册，
    /// 返回 missing|stale|fresh）。无订阅者（CustomStub 未安装）返回 noStub。</summary>
    public static Func<string> RuntimeStageState;

    /// <summary>统一运行时 bundle 文件名（与 LayoutStubDllBuilder.RuntimeBundleName、
    /// Loader 的固定加载名三方同步的文件名契约——解耦故不硬引用，改动时三处同改）。</summary>
    private const string RuntimeBundleName = "webcustomstub_runtime";

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
    /** 本趟依赖包打包版本（deps 模式；独立于运行时 SSOT 版本，默认 1.0.0）。 */
    private static string _depsVersion = "1.0.0";
    /** 逐关卡选择性导出（v1 仅单集；元素 = 场景名；null = 全量导出）。 */
    private static List<string> _selectedLevels;

    // ---- 逐关卡导出：LevelSetInfoSO.levelInfos 临时改写 / 还原 / 崩溃自愈 ----
    /** 当前被改写的 LevelSetInfoSO 资产路径（null = 本趟无改写）。 */
    private static string _setInfoMutatedAssetPath;
    /** 被改写的关卡集名（备份文件名后缀）。 */
    private static string _setInfoMutatedSetName;
    /** 改写前的原始 levelInfos 引用（数组本体未动，仅替换字段，可直接写回）。 */
    private static LevelInfoSO[] _setInfoSavedLevelInfos;

    // ---- 挂起导出续跑（自动编译闭环：RunExport 闸口挂起 → 域重载后续跑） ----
    /** SessionState：挂起任务参数（setNames '\t' mode '\t' depsVersion '\t' sel=逗号连接场景名；
     *  sel= 段为逐关卡导出新增，旧格式无此段，解析端双格式兼容）。 */
    private const string PendingKey = "LayoutEditor.SetExport.Pending";
    /** SessionState：挂起时刻 ticks（超时判定用）。 */
    private const string PendingAtKey = "LayoutEditor.SetExport.PendingAt";
    /** 续跑轮询的下次检查时刻（EditorApplication.timeSinceStartup）。 */
    private static double _resumeNextCheck;
    /** 当前 running 状态是「闸口挂起等编译」而非真实导出（区分新任务接管与自身挂起态，
     *  防续跑轮询误清挂起任务）。 */
    private static bool _deferredCompile;

    static LayoutEditorSetExporter()
    {
        // 域重载后自动续跑挂起的导出（统一运行时自动编译闭环的收尾）
        EditorApplication.delayCall += ResumePendingExport;
        // 逐关卡导出崩溃自愈：上次导出异常中断（Unity 被杀/断电）时还原被临时
        // 过滤的 LevelSetInfo（备份在 LayoutEditorExports/ 下持久保存）。
        EditorApplication.delayCall += RecoverSetInfoBackups;
    }

    /// <summary>CustomStub tag 载体前缀（SpecificPseudoPrefabTag.prefabTag）。
    ///  与 CustomStub/EntryPoint.HealObject + loader 的解析保持同步；
    ///  新增 stub 类型时此处必须补前缀。</summary>
    private static readonly string[] CustomStubTagPrefixes =
    {
        "RandomCrate|", "TimedSwitch|", "PushablePot|", "SwitchReenable|", "WorldMapDressing|",
        "UtensilTiming|", "CameraOffset|", "TravelatorReverse|", "TeleportalExitOnly|", "RatHeist|",
        "ConveyorDirectionSync|", "BLRelay|", "Coaxial|",
    };

    /// <summary>tag 前缀 → 清单特征（v3.5.0 stub_levels.txt 数据源之一）。
    ///  新增 stub 类型时四处同步：① CustomStubTagPrefixes、② 本表、
    ///  ③ StubComponentFeatureMap、④ 运行时特征消费方（EntryPoint 逐关卡闸门）。</summary>
    private static readonly Dictionary<string, string> StubTagFeatureMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "RandomCrate|", "crate" },
        { "TimedSwitch|", "switch" },
        { "PushablePot|", "pushable" },
        { "SwitchReenable|", "switch" },
        { "WorldMapDressing|", "worldmap" },
        { "UtensilTiming|", "timing" },
        { "CameraOffset|", "camera" },
        { "TravelatorReverse|", "travelator" },
        { "TeleportalExitOnly|", "teleportal" },
        { "RatHeist|", "rat" },
        { "ConveyorDirectionSync|", "conveyor" },
        { "BLRelay|", "blrelay" },
        { "Coaxial|", "coaxial" },
    };

    /// <summary>CustomStub 命名空间组件名 → 清单特征（双通道兜底：tag 缺失/旧格式时
    ///  场景里烘焙的组件仍能标出该关卡用 stub）。</summary>
    private static readonly Dictionary<string, string> StubComponentFeatureMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "RandomCrate", "crate" },
        { "TimedCookingSwitch", "switch" },
        { "PushablePot", "pushable" },
        { "SwitchReenable", "switch" },
        { "WorldMapDressing", "worldmap" },
        { "UtensilTimingConfig", "timing" },
        { "TravelatorReverser", "travelator" },
        { "TeleportalExitOnly", "teleportal" },
        { "RatHeist", "rat" },
        { "ConveyorDirectionSync", "conveyor" },
        { "ButtonLogicRelay", "blrelay" },
        { "CoaxialButtonGroup", "coaxial" },
        { "AnimGridMemberSync", "animgrid" },
        { "SwitchStartVisual", "startvisual" },
    };

    /// <summary>扫描当前打开的场景是否用到 CustomStub（= 逐场景特征收集的非空判定）。
    ///  导出 prepare 阶段逐场景调用（场景此时已打开）；写回守卫
    ///  （CustomStubWriteBackGuard）在 Apply 完成后也复用本方法判定是否检查 stub。</summary>
    public static bool ActiveSceneUsesCustomStub()
    {
        return CollectActiveSceneStubFeatures().Count > 0;
    }

    /// <summary>逐场景收集 CustomStub 清单特征（v3.5.0，stub_levels.txt 数据源；
    ///  运行时 EntryPoint 据此做逐关卡零介入闸门与按特征挂载）。三通道：
    ///  ① stub tag 前缀（StubTagFeatureMap）；
    ///  ② 命名空间 CustomStub 的组件（StubComponentFeatureMap；未知类型按 "stub"
    ///     兜底 + 告警——防止新增组件漏映射导致该关卡被闸门误杀）；
    ///  ③ 无 tag 内容：web 大锅/可移动火锅（名字子串，镜像运行时 HotPot.IsLargePot
    ///     → hotpot/pushable）、web 大炮（SetupCannonStub 根 → cannon）、未绑定终端
    ///     （Terminal.m_pilotableObject==null → terminal）、初始关闭开关
    ///     （PseudoPrefabSwitchStub.startEnabled==false → startvisual）、动画组成员
    ///     （Design/Animated Objects 有组根 → animgrid）。
    ///  场景必须处于打开状态（导出 prepare 阶段逐场景调用）。</summary>
    public static List<string> CollectActiveSceneStubFeatures()
    {
        var feats = new HashSet<string>(StringComparer.Ordinal);

        // ① tag 通道
        foreach (var tag in UnityEngine.Object.FindObjectsOfType<LevelEditorStub.SpecificPseudoPrefabTag>())
        {
            var t = tag.prefabTag;
            if (string.IsNullOrEmpty(t))
                continue;
            foreach (var kv in StubTagFeatureMap)
            {
                if (t.StartsWith(kv.Key, StringComparison.Ordinal))
                {
                    feats.Add(kv.Value);
                    break;
                }
            }
        }

        // ② 组件通道 + 未绑定终端探测（Terminal.m_pilotableObject==null）
        foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
        {
            if (mb == null)
                continue; // missing script
            var type = mb.GetType();
            if (type.Namespace == "CustomStub")
            {
                string f;
                if (StubComponentFeatureMap.TryGetValue(type.Name, out f))
                    feats.Add(f);
                else
                {
                    feats.Add("stub");
                    Debug.LogWarning("[SetExporter] 未知 CustomStub 组件未进特征表（按 stub 兜底，运行时激活核心但不确定子系统）: "
                        + type.Name + " @ " + mb.gameObject.name + "——请在 StubComponentFeatureMap 补映射");
                }
                continue;
            }
            if (type.Name == "Terminal")
            {
                var pilotable = type.GetField("m_pilotableObject",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (pilotable != null && pilotable.GetValue(mb) == null)
                    feats.Add("terminal");
            }
        }

        // ③ 无 tag 通道（续）：web 大锅/可移动火锅（名字子串镜像 HotPot.IsLargePot）
        foreach (var tr in UnityEngine.Object.FindObjectsOfType<Transform>())
        {
            var n = tr.name;
            if (n.IndexOf("pot_01_pushable", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("pushable_object", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                feats.Add("pushable");
                feats.Add("hotpot");
            }
            else if (n.IndexOf("large_pot", StringComparison.OrdinalIgnoreCase) >= 0)
                feats.Add("hotpot");
        }
        // web 大炮（SetupCannonStub 根）
        if (UnityEngine.Object.FindObjectsOfType<LevelEditorStub.SetupCannonStub>().Length > 0)
            feats.Add("cannon");
        // 初始关闭开关（开局关闭色补挂目标；startEnabled 为公开序列化字段）
        foreach (var sw in UnityEngine.Object.FindObjectsOfType<LevelEditorStub.PseudoPrefabSwitchStub>())
        {
            if (sw != null && !sw.startEnabled)
            {
                feats.Add("startvisual");
                break;
            }
        }
        // 动画组成员（Design/Animated Objects 组根 → AnimGridMemberSync 换装目标）
        var animatedRoot = GameObject.Find("Design/Animated Objects");
        if (animatedRoot != null && animatedRoot.transform.childCount > 0)
            feats.Add("animgrid");

        // ④ commonW 素材引用通道（2026-10-06 空清单休眠→进图卡加载事故）：场景引用了
        //    commonW1/W2/W3... 素材时标 "web" 特征——commonW 包只由 Loader 激活时加载，
        //    无 CustomStub 玩法的关卡不标特征会让清单为空 → 加载器休眠 → commonW 不加载
        //    → 场景外部引用（cab）无法解析 → 进图卡加载。web 特征仅作激活信号（运行时
        //    无消费方，EntryPoint 对未知特征安全忽略）。
        try
        {
            var activeScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            if (AssetTextReferencesCommonW(activeScenePath))
                feats.Add("web");
        }
        catch
        {
        }

        return new List<string>(feats);
    }

    /// <summary>资产文本 guid 扫描：任一 guid 解析进 Assets/commonW*（素材/材质/网格/SO）
    ///  即认为该资产需要 commonW 依赖包（stub_levels.txt "web" 特征数据源）。
    ///  场景与 LevelInfoSO 通用。</summary>
    private static bool AssetTextReferencesCommonW(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;
        string text;
        try { text = File.ReadAllText(AbsPath(assetPath)); }
        catch { return false; }
        if (string.IsNullOrEmpty(text))
            return false;
        foreach (Match m in Regex.Matches(text, @"guid:\s*([a-f0-9]{32})"))
        {
            var path = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
            if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/commonW", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>该关 LevelInfoSO（recipes/allIngredients/matchlists）引用了 commonW 素材
    ///  （commonW3 自定义菜谱可能只出现在订单里，场景文本扫不到，需补此通道）。</summary>
    private static bool LevelInfoReferencesCommonW(string sceneAssetPath)
    {
        try
        {
            var info = LayoutEditorLevelInfoResolver.ResolveForScene(sceneAssetPath);
            if (info == null)
                return false;
            return AssetTextReferencesCommonW(AssetDatabase.GetAssetPath(info));
        }
        catch
        {
            return false;
        }
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
        return StartExport(setNames, mode, null);
    }

    /// <summary>deps 模式可带依赖包打包版本（depsVersion，独立于运行时/关卡集版本，
    /// 规范化见 NormalizeDepsVersion）；其余模式忽略该参数。</summary>
    public static string StartExport(List<string> setNames, string mode, string depsVersion)
    {
        return StartExport(setNames, mode, depsVersion, null);
    }

    /// <summary>逐关卡选择性导出（v1 仅单集）：selectedLevels = 场景名清单
    /// （LevelInfoSO.sceneName / scenes/ 文件名去扩展），null/空 = 全量导出。
    /// 部分导出时 info_&lt;set&gt; 内 levelInfos 临时过滤为入选关卡（构建后还原，
    /// 见 ApplyLevelSelectionFilter），zip 只携带入选 s_* bundle。</summary>
    public static string StartExport(List<string> setNames, string mode, string depsVersion,
        List<string> selectedLevels)
    {
        if (setNames == null || setNames.Count == 0)
            return "缺少关卡集标识。";
        lock (_lock)
        {
            if (_status == "running")
                return "已有导出任务正在进行（" + _setName + "），请等待完成后再试。";
        }
        // 上次导出异常中断的 LevelSetInfo 备份先还原（此刻无导出在跑，安全）
        RecoverSetInfoBackups();
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

        // 选择清单校验/规范化：仅 levels/all 模式且单集时有效；全选 = 等价全量（置 null）。
        List<string> sel;
        var selErr = NormalizeSelectedLevels(safeList, m, selectedLevels, out sel);
        if (!string.IsNullOrEmpty(selErr))
            return selErr;

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
            _depsVersion = NormalizeDepsVersion(m == "deps" ? depsVersion : null);
            _deferredCompile = false;
            _selectedLevels = sel;
            // 逐关卡导出改写登记清零（此前已 RecoverSetInfoBackups，磁盘已自愈）
            _setInfoMutatedAssetPath = null;
            _setInfoMutatedSetName = null;
            _setInfoSavedLevelInfos = null;
        }
        EditorApplication.delayCall += RunExport;
        return null;
    }

    /// <summary>校验并规范化逐关卡选择清单。返回 null 表示通过（outSelLevels =
    /// null 全量导出 / 非空部分导出）；返回非空字符串 = 错误信息（导出不启动）。</summary>
    private static string NormalizeSelectedLevels(List<string> safeList, string mode,
        List<string> selectedLevels, out List<string> outSelLevels)
    {
        outSelLevels = null;
        var normalized = new List<string>();
        if ((mode == "levels" || mode == "all")
            && selectedLevels != null && selectedLevels.Count > 0)
        {
            if (safeList.Count > 1)
                return "逐关卡选择性导出暂不支持多集合并导出（请单集导出；多集导出始终包含全部关卡）。";
            var sceneFiles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in CollectScenePaths(LevelSetsRoot + "/" + safeList[0]))
                sceneFiles.Add(Path.GetFileNameWithoutExtension(p));
            foreach (var raw in selectedLevels)
            {
                if (string.IsNullOrEmpty(raw))
                    continue;
                var s = raw.Trim();
                if (s.Length == 0 || normalized.Contains(s))
                    continue;
                if (s.IndexOf('/') >= 0 || s.IndexOf('\\') >= 0 || s == "." || s == "..")
                    return "所选关卡名非法：" + s;
                if (!sceneFiles.Contains(s))
                    return "所选关卡不存在：" + s + "（关卡集 " + safeList[0]
                        + " 的 scenes/ 下未找到对应场景）";
                normalized.Add(s);
            }
            if (normalized.Count == 0)
                return "所选关卡清单为空。";
            if (normalized.Count < sceneFiles.Count)
                outSelLevels = normalized; // 部分导出；全选 = null 全量
        }
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
        // 统一运行时就绪闸口：母本源码比 DLL 新（外部改码尚未编译）→ 自动触发编译，
        // 任务挂起（SessionState），域重载后 ResumePendingExport 自动续跑——导出不再
        // 因「尚未编译完成」报错中断。批处理（LayoutEditorBatchExport）同路径：挂起时
        // 状态非 done、exit 3，外层流水线等编译完成后再跑一遍即可。
        if (!TryGateRuntimeReady())
            return;
        var setNames = new List<string>();
        List<string> selectedLevels = null;
        lock (_lock)
        {
            setNames.AddRange(_setNames);
            if (_selectedLevels != null)
                selectedLevels = new List<string>(_selectedLevels);
        }
        var prevActive = EditorSceneManager.GetActiveScene().path;
        try
        {
            RunExportCore(setNames, selectedLevels);
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
            // 逐关卡导出兜底还原（正常路径 RunExportCore 内已还原；此处防异常逃逸）
            RestoreSetInfoMutation();
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

    /// <summary>导出前统一运行时就绪闸口。true=就绪继续导出；false=已挂起（编译已
    /// 触发，域重载后自动续跑）或已置 error。闸口自身异常一律按就绪放行，由后续
    /// BeforeBuild 硬校验兜底，绝不因闸口故障卡死导出。</summary>
    private static bool TryGateRuntimeReady()
    {
        var gate = RuntimeReadyGate;
        if (gate == null)
            return true;
        string verdict;
        try
        {
            verdict = gate();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SetExporter] 运行时就绪闸口异常（按就绪继续，BeforeBuild 兜底）: " + ex.Message);
            return true;
        }
        if (verdict == null)
            return true;
        if (verdict != "compiling")
        {
            lock (_lock)
            {
                _status = "error";
                _error = "统一运行时未就绪：" + verdict;
            }
            Debug.LogWarning("[SetExporter] 导出未启动：" + _error);
            return false;
        }
        List<string> pendingSets;
        string mode;
        string depsVersion;
        List<string> pendingSelection;
        lock (_lock)
        {
            pendingSets = new List<string>(_setNames);
            mode = _mode;
            depsVersion = _depsVersion;
            pendingSelection = _selectedLevels != null
                ? new List<string>(_selectedLevels) : null;
        }
        SessionState.SetString(PendingKey,
            string.Join("\t", pendingSets.ToArray()) + "\t" + mode + "\t" + (depsVersion ?? "")
            + "\t" + "sel=" + (pendingSelection != null ? string.Join(",", pendingSelection.ToArray()) : ""));
        SessionState.SetString(PendingAtKey, DateTime.UtcNow.Ticks.ToString());
        lock (_lock) { _deferredCompile = true; }
        SetPhase("compile", "统一运行时源码已更新，自动编译中（完成后自动继续导出）…");
        return false;
    }

    private static void ClearPendingExport()
    {
        SessionState.SetString(PendingKey, "");
        SessionState.SetString(PendingAtKey, "");
    }

    /// <summary>域重载后自动续跑挂起的导出（编译完成 → 运行时就绪 → 重新走完整
    /// StartExport 链）。~1s 节流轮询；超时（5 分钟）或闸口报失败（编译错误）才置
    /// error 收场。</summary>
    private static void ResumePendingExport()
    {
        var pending = SessionState.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(pending))
            return;
        lock (_lock)
        {
            // 已有新的导出任务接管（running 且非本任务挂起态），旧挂起任务作废
            if (_status == "running" && !_deferredCompile)
            {
                ClearPendingExport();
                return;
            }
        }
        var gate = RuntimeReadyGate;
        string verdict = null;
        if (gate != null)
        {
            try { verdict = gate(); }
            catch (Exception ex) { Debug.LogWarning("[SetExporter] 续跑闸口异常: " + ex.Message); }
        }
        if (verdict == null)
        {
            // 就绪：解包参数重新走完整启动链（全部状态字段重新初始化）
            ClearPendingExport();
            var parts = pending.Split('\t');
            if (parts.Length < 3)
                return;
            // 新格式末段 "sel=<逗号连接的场景名>"（逐关卡导出）；旧格式无此段。
            List<string> selectedLevels = null;
            var setCount = parts.Length - 2;
            var mode = parts[parts.Length - 2];
            var depsVersion = parts[parts.Length - 1];
            if (parts[parts.Length - 1].StartsWith("sel=", StringComparison.Ordinal))
            {
                var selRaw = parts[parts.Length - 1].Substring("sel=".Length);
                selectedLevels = new List<string>();
                foreach (var s in selRaw.Split(','))
                {
                    if (!string.IsNullOrEmpty(s))
                        selectedLevels.Add(s);
                }
                if (selectedLevels.Count == 0)
                    selectedLevels = null;
                setCount = parts.Length - 3;
                mode = parts[parts.Length - 3];
                depsVersion = parts[parts.Length - 2];
            }
            var setNames = new List<string>();
            for (var i = 0; i < setCount; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]))
                    setNames.Add(parts[i]);
            }
            var err = StartExport(setNames, mode,
                string.IsNullOrEmpty(depsVersion) ? null : depsVersion, selectedLevels);
            if (!string.IsNullOrEmpty(err))
            {
                lock (_lock)
                {
                    _status = "error";
                    _error = "自动续跑导出失败：" + err;
                }
                Debug.LogWarning("[SetExporter] " + _error);
            }
            else
                Debug.Log("[SetExporter] 统一运行时编译完成，已自动续跑导出（"
                    + string.Join("+", setNames.ToArray()) + "）");
            return;
        }
        if (verdict == "compiling")
        {
            long startedAt;
            long.TryParse(SessionState.GetString(PendingAtKey, "0"), out startedAt);
            if (startedAt > 0 && DateTime.UtcNow.Ticks - startedAt > TimeSpan.TicksPerMinute * 5)
            {
                ClearPendingExport();
                lock (_lock)
                {
                    _status = "error";
                    _error = "等待统一运行时自动编译超时（5 分钟），请查看 Unity Console 后重试导出。";
                }
                Debug.LogWarning("[SetExporter] " + _error);
                return;
            }
            ScheduleResumeRetry();
            return;
        }
        ClearPendingExport();
        lock (_lock)
        {
            _status = "error";
            _error = "统一运行时自动编译未完成：" + verdict;
        }
        Debug.LogWarning("[SetExporter] " + _error);
    }

    private static void ScheduleResumeRetry()
    {
        _resumeNextCheck = EditorApplication.timeSinceStartup + 1.0;
        EditorApplication.update -= ResumeRetryTick;
        EditorApplication.update += ResumeRetryTick;
    }

    private static void ResumeRetryTick()
    {
        if (EditorApplication.timeSinceStartup < _resumeNextCheck)
            return;
        EditorApplication.update -= ResumeRetryTick;
        ResumePendingExport();
    }

    private static void RunExportCore(List<string> setNames, List<string> selectedLevels)
    {
        var mode = _mode;
        var joinedName = string.Join("+", setNames.ToArray());

        // 逐关卡选择（v1 仅单集；StartExport 已校验）：null = 全量导出。
        HashSet<string> includedSceneNames = null;
        if (selectedLevels != null && selectedLevels.Count > 0)
        {
            includedSceneNames = new HashSet<string>(selectedLevels, StringComparer.Ordinal);
            Debug.Log("[SetExporter] 逐关卡选择性导出：" + selectedLevels.Count + "/"
                + CollectScenePaths(LevelSetsRoot + "/" + setNames[0]).Count + " 关（"
                + string.Join(",", selectedLevels.ToArray()) + "）。");
        }

        // ---- 依赖包模式（deps）：不碰关卡场景，仅打包 OC2DIYLevelRuntimeWLoader/ 依赖 ----
        if (mode == "deps")
        {
            ExportDepsOnly(setNames[0], _depsVersion);
            return;
        }

        // ---- 1. prepare：逐集逐场景 Open → 清临时物体 → 重打 stub tag → Save ----
        var perSetScenes = new List<KeyValuePair<string, List<string>>>();
        // v3.5.0 逐关卡清单：每条 "集|关卡|特征1,特征2,..."（stub_levels.txt 数据源）。
        var stubManifestLines = new List<string>();
        var totalScenes = 0;
        foreach (var setName in setNames)
        {
            var scenes = CollectScenePaths(LevelSetsRoot + "/" + setName);
            if (scenes.Count == 0)
                throw new Exception("关卡集没有可用场景（" + LevelSetsRoot + "/" + setName
                    + "/scenes/ 为空）。");
            // 逐关卡导出：只准备/构建入选场景——被排除场景不打开不烘焙（落盘场景由
            // 写回链路保证无临时伪 prefab 实例，不打开即无内存态，构建安全），其
            // s_* 产物（若有历史 bundle 名）绝不进 zip。选择 ⇒ 单集（StartExport 已校验）。
            if (includedSceneNames != null)
            {
                scenes.RemoveAll(p => !includedSceneNames.Contains(Path.GetFileNameWithoutExtension(p)));
                if (scenes.Count == 0)
                    throw new Exception("逐关卡导出：选择清单未命中任何场景（" + LevelSetsRoot
                        + "/" + setName + "）。");
            }
            perSetScenes.Add(new KeyValuePair<string, List<string>>(setName, scenes));
            totalScenes += scenes.Count;
        }

        // ---- 0. orphan-clean：导出前自动清理场景孤儿脚本引用（磁盘文本级，此时场景
        //      尚未打开；安全策略经 OrphanCleanHook 钩子由 CustomStubOrphanRepair 提供，
        //      仅报告项不阻断导出）----
        if (OrphanCleanHook != null)
        {
            var allScenes = new List<string>();
            foreach (var pair in perSetScenes)
                allScenes.AddRange(pair.Value);
            try
            {
                var orphanSummary = OrphanCleanHook(allScenes);
                if (!string.IsNullOrEmpty(orphanSummary))
                    Debug.Log("[SetExporter] 导出前孤儿脚本清理:\n" + orphanSummary);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SetExporter] 孤儿脚本清理异常（继续导出）: " + ex.Message);
            }
        }

        // ---- 0.5 dual-stub-clean：双 stub 道具自动修复（经 DualStubCleanHook 钩子由
        //      LayoutEditorDualStubRepair 提供：打开场景→清理基础 stub 组件对→保存）----
        if (DualStubCleanHook != null)
        {
            var allScenesDual = new List<string>();
            foreach (var pair in perSetScenes)
                allScenesDual.AddRange(pair.Value);
            try
            {
                var dualStubSummary = DualStubCleanHook(allScenesDual);
                if (!string.IsNullOrEmpty(dualStubSummary))
                    Debug.Log("[SetExporter] 导出前双 stub 修复:\n" + dualStubSummary);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SetExporter] 双 stub 修复异常（继续导出）: " + ex.Message);
            }
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
                // v3.5.0 逐场景特征收集（stub_levels.txt 数据源；tag 重打之后扫，
                // 与运行时自愈将看到的内容一致）。逐关卡导出时循环体只含入选场景
                //（上方已过滤），被排除关卡自然不进清单、不进 zip。
                var feats = CollectActiveSceneStubFeatures();
                // web 特征补充：LevelInfoSO 引用 commonW 素材（commonW3 菜谱可能只进
                // 订单不经场景，见 ④ 通道注释）。
                if (!feats.Contains("web") && LevelInfoReferencesCommonW(scenePath))
                    feats.Add("web");
                if (feats.Count > 0)
                {
                    stubManifestLines.Add(pair.Key + "|" + Path.GetFileNameWithoutExtension(scenePath)
                        + "|" + string.Join(",", feats.ToArray()));
                    if (!_usesCustomStub)
                    {
                        _usesCustomStub = true;
                        Debug.Log("[SetExporter] 检测到 CustomStub 用法（tag/组件/无 tag 内容）：" + scenePath);
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                foreach (var w in ValidateSceneForPlayerBuild(scenePath))
                    Debug.LogWarning("[SetExporter] Player 构建校验: " + w);
                var commonW1Info = DescribeCommonW1PrefabRefs(scenePath);
                if (!string.IsNullOrEmpty(commonW1Info))
                    Debug.Log("[SetExporter] " + commonW1Info);
            }
        }
        AssetDatabase.SaveAssets();

        // ---- 1.5 逐关卡导出：levelInfos 临时过滤（info_<set> 构建产物随之为部分集；
        //      构建完成后在 finally 还原源资产，崩溃自愈见 RecoverSetInfoBackups）----
        if (includedSceneNames != null)
            ApplyLevelSelectionFilter(setNames[0], includedSceneNames);

        try
        {
        // ---- 2. clean：逐集仅删除本集旧产物目录（其他目录不动）----
        foreach (var setName in setNames)
        {
            foreach (var outDir in CollectSetBundleOutputDirs(setName))
            {
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
        }
        AssetDatabase.Refresh();

        // ---- 3. build：构建 AssetBundle（阻塞，约 3-5 分钟；一次全量构建覆盖所有集）----
        SetPhase("build", "构建 AssetBundle（约 3-5 分钟"
            + (includedSceneNames != null ? "，本次打包 " + includedSceneNames.Count + " 关" : "") + "）…");
        foreach (var pair in perSetScenes)
        {
            LayoutEditorLevelAdminApi.EnsureSetInfoBundle(pair.Key);
            EnsureSceneBundleNames(pair.Key, pair.Value);
            foreach (var w in ValidateSetBundleNaming(pair.Key))
                Debug.LogWarning("[SetExporter] " + w);
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
            if (ResolveBuiltSetBundleDir(setName) == null)
                throw new Exception("构建完成但没有输出目录 " + BundlesRoot + "/" + setName
                    + "（bundle 名可能未设置或与关卡集文件夹名不一致，如 latiao/* vs LaTiao/）。");
        }

        // ---- 4. package：逐集删除 .manifest / *.meta 等带后缀文件 ----
        SetPhase("package", "清理 manifest 与 meta 文件…");
        foreach (var setName in setNames)
        {
            var builtDir = ResolveBuiltSetBundleDir(setName);
            if (builtDir == null)
                continue;
            foreach (var f in Directory.GetFiles(builtDir))
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

        // 逐关卡导出：入选场景的构建产物文件名集合（产物文件名 = assetBundleName 末段；
        // 改名关卡可能保留历史 bundle 名，故从 importer 解析而非想当然用场景文件名）。
        // perSetScenes 已在头部过滤为仅入选场景。
        HashSet<string> selectedBundleFiles = null;
        if (includedSceneNames != null)
        {
            selectedBundleFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var scenePath in perSetScenes[0].Value)
            {
                var imp = AssetImporter.GetAtPath(scenePath);
                var bn = imp != null ? imp.assetBundleName : null;
                if (!string.IsNullOrEmpty(bn))
                    selectedBundleFiles.Add(bn.Substring(bn.LastIndexOf('/') + 1));
            }
        }

        var entries = new List<LayoutEditorZipWriter.ZipEntrySource>();
        foreach (var setName in setNames)
        {
            var absOutDir = ResolveBuiltSetBundleDir(setName);
            if (absOutDir == null)
                throw new Exception("没有可打包的 bundle 目录（" + setName + "）。");
            var payloads = new List<string>(Directory.GetFiles(absOutDir));
            payloads.RemoveAll(HasJunkExtension);
            // 旧体系 per-set runtime bundle 已废除，若产物里残留 runtime 文件一律剔除
            // （统一运行时改由依赖包 OC2DIYLevelRuntimeWLoader/webcustomstub_runtime 分发）。
            payloads.RemoveAll(p =>
                string.Equals(Path.GetFileName(p), "runtime", StringComparison.OrdinalIgnoreCase));
            // 逐关卡导出：只保留入选关卡的场景 bundle（info_<set> 恒保留；多集导出
            // 无选择，不进此分支）。
            if (selectedBundleFiles != null)
            {
                var infoFileSel = "info_" + setName;
                payloads.RemoveAll(p =>
                {
                    var fn = Path.GetFileName(p);
                    return !string.Equals(fn, infoFileSel, StringComparison.OrdinalIgnoreCase)
                        && !selectedBundleFiles.Contains(fn);
                });
            }
            if (payloads.Count == 0)
                throw new Exception("清理后没有可打包的 bundle 文件（" + setName + "）。");
            var infoFile = "info_" + setName;
            if (!payloads.Exists(p => string.Equals(Path.GetFileName(p), infoFile, StringComparison.OrdinalIgnoreCase)))
                Debug.LogWarning("[SetExporter] zip 内未找到 " + infoFile
                    + "——OC2DIYLevel 将无法加载关卡集 info bundle（LevelInfo/地板材质等会缺失）。"
                    + " 请确认关卡集根目录 AssetBundle = " + setName + "/info_" + setName);

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

            // stub_levels.txt（v3.5.0 逐关卡清单）：loader/EntryPoint 据此做逐关卡
            // 零介入闸门（清单未命中的场景不扫描/不探测/不装补丁）与按特征挂载。
            // 每个集都写（含空清单）——文件缺失会被 loader 按「旧版导出」处理。
            var prefix = setName + "|";
            var levelLines = stubManifestLines.FindAll(l => l.StartsWith(prefix, StringComparison.Ordinal));
            var manifestAbs = WriteStubLevelsFile(setName, levelLines);
            if (!string.IsNullOrEmpty(manifestAbs))
                entries.Add(new LayoutEditorZipWriter.ZipEntrySource(
                    "OC2DIYLevel/levels/" + setName + "/stub_levels.txt", manifestAbs));
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
        }
        finally
        {
            // 逐关卡导出：还原 LevelSetInfoSO.levelInfos（源资产零残留）。
            RestoreSetInfoMutation();
        }
        AssetDatabase.Refresh();
    }

    // ==================== 逐关卡导出：LevelSetInfo 临时过滤 / 还原 / 崩溃自愈 ====================

    /// <summary>关卡集 LevelSetInfoSO 资产路径（LevelSets/&lt;set&gt;/data/ 下第一个；null=未找到）。</summary>
    private static string SetInfoAssetPath(string setName)
    {
        var dataDir = LevelSetsRoot + "/" + setName + "/data";
        if (!string.IsNullOrEmpty(setName) && AssetDatabase.IsValidFolder(dataDir))
        {
            foreach (var guid in AssetDatabase.FindAssets("t:LevelSetInfoSO", new[] { dataDir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(p))
                    return p;
            }
        }
        return null;
    }

    /// <summary>LevelSetInfo 磁盘备份绝对路径。放 LayoutEditorExports/（Assets 外、
    ///  持久保存）——Unity 崩溃重启会清 Temp/，放临时目录会丢自愈数据。</summary>
    private static string SetInfoBackupAbsPath(string setName)
    {
        return ExportRootAbsPath() + "/._setinfo_backup_" + setName;
    }

    /// <summary>构建前把 levelInfos 过滤为入选关卡（按 LevelInfoSO.sceneName 匹配）。
    ///  顺序：磁盘备份 → 登记改写（还原依据）→ 过滤 + SaveAssets。备份失败直接中止
    ///  （源资产零改动）；登记后任何失败由 RestoreSetInfoMutation 兜底。</summary>
    private static void ApplyLevelSelectionFilter(string setName, HashSet<string> includedSceneNames)
    {
        var assetPath = SetInfoAssetPath(setName);
        if (string.IsNullOrEmpty(assetPath))
            throw new Exception("逐关卡导出：未找到 LevelSetInfoSO（" + LevelSetsRoot
                + "/" + setName + "/data/ 下应有 LevelSetInfo.asset）。");
        var so = AssetDatabase.LoadAssetAtPath<LevelSetInfoSO>(assetPath);
        if (so == null)
            throw new Exception("逐关卡导出：LevelSetInfoSO 加载失败（" + assetPath + "）。");

        var backupAbs = SetInfoBackupAbsPath(setName);
        try
        {
            var dir = ExportRootAbsPath();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.Copy(AbsPath(assetPath), backupAbs, true);
        }
        catch (Exception ex)
        {
            throw new Exception("逐关卡导出：备份 LevelSetInfo 失败，已中止（源数据未改动）: " + ex.Message);
        }

        _setInfoMutatedAssetPath = assetPath;
        _setInfoMutatedSetName = setName;
        _setInfoSavedLevelInfos = so.levelInfos;

        var keep = new List<LevelInfoSO>();
        var total = 0;
        if (so.levelInfos != null)
        {
            total = so.levelInfos.Length;
            foreach (var li in so.levelInfos)
            {
                if (li != null && includedSceneNames.Contains(li.sceneName ?? ""))
                    keep.Add(li);
            }
        }
        if (keep.Count == 0)
            throw new Exception("逐关卡导出：选择清单与 levelInfos 不匹配（0 命中）——场景名与 "
                + "LevelInfoSO.sceneName 不一致，请检查改名关卡。");
        so.levelInfos = keep.ToArray();
        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetExporter] 逐关卡导出：levelInfos 临时过滤为 " + keep.Count + "/" + total
            + " 关（构建后自动还原；崩溃备份 " + backupAbs + "）。");
    }

    /// <summary>还原被临时过滤的 LevelSetInfoSO：优先内存写回（原数组引用直接复赋值），
    ///  失败回落磁盘备份复制 + ImportAsset。幂等；成功后删备份并清登记。</summary>
    private static void RestoreSetInfoMutation()
    {
        var assetPath = _setInfoMutatedAssetPath;
        if (string.IsNullOrEmpty(assetPath))
            return;
        var setName = _setInfoMutatedSetName;
        var backupAbs = SetInfoBackupAbsPath(setName);
        try
        {
            var so = AssetDatabase.LoadAssetAtPath<LevelSetInfoSO>(assetPath);
            var restored = false;
            if (so != null && _setInfoSavedLevelInfos != null)
            {
                so.levelInfos = _setInfoSavedLevelInfos;
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssets();
                restored = true;
            }
            if (!restored && File.Exists(backupAbs))
            {
                File.Copy(backupAbs, AbsPath(assetPath), true);
                AssetDatabase.ImportAsset(assetPath);
                restored = true;
            }
            if (!restored)
                Debug.LogWarning("[SetExporter] LevelSetInfo 还原跳过（SO 与备份均不可用）: " + assetPath);
            else
                Debug.Log("[SetExporter] 逐关卡导出：LevelSetInfo 已还原（" + setName + "）。");
        }
        catch (Exception ex)
        {
            // 保留备份与登记：下次 StartExport / 域加载时 RecoverSetInfoBackups 再试
            Debug.LogError("[SetExporter] LevelSetInfo 还原失败！请手动用备份还原（" + backupAbs
                + " → " + assetPath + "）: " + ex.Message);
            return;
        }
        try
        {
            if (File.Exists(backupAbs))
                File.Delete(backupAbs);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SetExporter] 删除 LevelSetInfo 备份失败（不影响导出结果）: " + ex.Message);
        }
        _setInfoMutatedAssetPath = null;
        _setInfoMutatedSetName = null;
        _setInfoSavedLevelInfos = null;
    }

    /// <summary>崩溃自愈：Unity 被杀/断电导致 finally 未跑时，按残留备份还原
    ///  LevelSetInfo（域加载 delayCall 与每次 StartExport 都会调用；导出进行中跳过）。</summary>
    private static void RecoverSetInfoBackups()
    {
        try
        {
            lock (_lock)
            {
                if (_status == "running")
                    return; // 备份属于进行中的导出，不动
            }
            var dir = ExportRootAbsPath();
            if (!Directory.Exists(dir))
                return;
            foreach (var backup in Directory.GetFiles(dir, "._setinfo_backup_*"))
            {
                var setName = Path.GetFileName(backup).Substring("._setinfo_backup_".Length);
                var assetPath = SetInfoAssetPath(setName);
                if (string.IsNullOrEmpty(assetPath) || !File.Exists(AbsPath(assetPath)))
                {
                    File.Delete(backup); // 关卡集已删除，备份无意义
                    continue;
                }
                File.Copy(backup, AbsPath(assetPath), true);
                AssetDatabase.ImportAsset(assetPath);
                File.Delete(backup);
                Debug.LogWarning("[SetExporter] 检测到上次导出异常中断，已还原关卡集关卡列表（"
                    + setName + "）。若该集此后有编辑丢失，可从写回历史恢复。");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SetExporter] LevelSetInfo 备份自愈检查失败: " + ex.Message);
        }
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
    /// bundle + web/public/Loader.dll，无需 BuildAssetBundles，快速导出。
    /// depsVersion = 依赖包打包版本（独立于运行时 SSOT 版本，用于 zip 命名与
    /// package_version.txt；装一次即可长期复用，版本变更即提示玩家更新）。</summary>
    private static void ExportDepsOnly(string setName, string depsVersion)
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
        // 2026-10-06 加固：删产物前先补回丢失的根 bundle 名（.meta 误动后 BuildAssetBundles
        // 会静默不产出）；重建后校验产物齐全，缺失即阻断导出（此前仅 LogWarning，
        // 打出的依赖包缺 commonW3 会让老鼠模型/新菜谱等素材失效）。
        var fixedWNames = EnsureCommonWBundleNames();
        if (fixedWNames != null)
            Debug.Log("[SetExporter] 导出前已补回 commonW 根目录 bundle 名: " + fixedWNames);
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
        var missingW = ValidateCommonWProducts();
        if (missingW.Count > 0)
            throw new Exception("commonW 构建产物缺失: " + string.Join("、", missingW.ToArray())
                + "（源目录存在但 BuildAssetBundles 未产出）——请先用菜单「Layout Editor/CustomStub（关卡代码分发）"
                + "/重新打包 commonW 素材包 (Rebuild commonW Bundles)」修复后再导出。");

        var entries = new List<LayoutEditorZipWriter.ZipEntrySource>();
        AddDependencyEntries(entries, new List<string> { setName }, true); // deps 模式：commonW1/W2 均无条件携带
        if (entries.Count == 0)
            throw new Exception("依赖包为空：未找到 Loader.dll / webcustomstub_runtime / commonW1，"
                + "请先执行菜单「Layout Editor/CustomStub（关卡代码分发）/构建 AssetBundles（含 Runtime 打包）」。");

        // 构建清单 package_version.txt：打包版本/时间/环境 + 包内全部文件 MD5（排查用），
        // 作为最后一个条目进 zip（自身不参与 MD5，见 WritePackageVersionFile）。
        var pkgVerAbs = WritePackageVersionFile(depsVersion, entries);
        if (!string.IsNullOrEmpty(pkgVerAbs))
            entries.Add(new LayoutEditorZipWriter.ZipEntrySource(
                "OC2DIYLevelRuntimeWLoader/package_version.txt", pkgVerAbs));

        var zipFileName = "OC2DIYLevelRuntimeWLoader_v" + depsVersion
            + "_" + DateTime.Now.ToString("yyyyMMdd") + ".zip";
        var zipAbsPath = ExportRootAbsPath() + "/" + zipFileName;
        SetPhase("zip", "生成 zip：" + zipFileName + "（" + entries.Count + " 个文件）…");
        LayoutEditorZipWriter.WriteZip(zipAbsPath, entries);
        SaveLastDepsVersion(depsVersion);
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

    // ==================== commonW 素材包强制重打（2026-10-06） ====================
    // 背景：依赖包/关卡集导出虽会删旧重打 commonW*，但没有独立入口（只想重打素材包
    // 必须跑完整导出）；且源目录 assetBundleName 丢失（.meta 误动）时 BuildAssetBundles
    // 静默不产出 → 依赖包缺 commonW3 等 → 真机老鼠模型/新菜谱失效，此前仅 LogWarning。

    /// <summary>枚举存在的 commonW 源目录索引（Assets/commonW&lt;N&gt;，N≥1）。</summary>
    private static List<int> CollectCommonWSourceIndices()
    {
        var list = new List<int>();
        for (int i = 1; i <= 32; i++)
        {
            if (AssetDatabase.IsValidFolder("Assets/commonW" + i))
                list.Add(i);
        }
        return list;
    }

    /// <summary>确保每个 commonW 源目录的根 assetBundleName 已设置（空值时补
    ///  “commonw&lt;N&gt;”）。.meta 被误动/清空后 BuildAssetBundles 不会产出对应包，
    ///  依赖包将静默缺失——此处前置修复。返回补回的 bundle 名清单（null=无需修复）。</summary>
    internal static string EnsureCommonWBundleNames()
    {
        var fixedList = new List<string>();
        foreach (var index in CollectCommonWSourceIndices())
        {
            var dir = "Assets/commonW" + index;
            var importer = AssetImporter.GetAtPath(dir);
            if (importer == null)
                continue;
            var expected = "commonw" + index;
            if (string.IsNullOrEmpty(importer.assetBundleName))
            {
                importer.assetBundleName = expected;
                importer.SaveAndReimport();
                fixedList.Add(expected);
            }
            else if (!string.Equals(importer.assetBundleName, expected, StringComparison.Ordinal))
            {
                Debug.LogWarning("[SetExporter] commonW 源目录 " + dir + " 的 bundle 名为 "
                    + importer.assetBundleName + "（≠ " + expected + "），保持不改——如非有意请手动核对。");
            }
        }
        return fixedList.Count > 0 ? string.Join("、", fixedList.ToArray()) : null;
    }

    /// <summary>校验 commonW 构建产物：源目录存在的每个 commonW&lt;N&gt; 必须有
    ///  Assets/AssetBundles/commonw&lt;N&gt; 产物。返回缺失清单（空=全部就绪）。</summary>
    private static List<string> ValidateCommonWProducts()
    {
        var missing = new List<string>();
        foreach (var index in CollectCommonWSourceIndices())
        {
            var abs = AbsPath(BundlesRoot) + "/commonw" + index;
            if (!File.Exists(abs))
                missing.Add("commonw" + index);
        }
        return missing;
    }

    [MenuItem("Layout Editor/CustomStub（关卡代码分发）/重新打包 commonW 素材包 (Rebuild commonW Bundles)", false, 13)]
    public static void RebuildCommonWBundlesMenu()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorUtility.DisplayDialog("重新打包 commonW", "Unity 正在编译/导入脚本，请等待完成后再构建。", "确定");
            return;
        }
        var indices = CollectCommonWSourceIndices();
        if (indices.Count == 0)
        {
            EditorUtility.DisplayDialog("重新打包 commonW", "未找到任何 Assets/commonW<N> 源目录。", "确定");
            return;
        }
        var names = new List<string>();
        foreach (var index in indices)
            names.Add("commonw" + index);
        if (!EditorUtility.DisplayDialog("重新打包 commonW",
            "将删除并重建 " + indices.Count + " 个 commonW 素材包（"
            + string.Join("、", names.ToArray()) + "）的构建产物。\n"
            + "BuildAssetBundles 为增量构建：其余 bundle（关卡集/统一运行时等）不受影响。\n继续？",
            "开始", "取消"))
            return;
        try
        {
            var fixedNames = EnsureCommonWBundleNames();
            if (fixedNames != null)
                Debug.Log("[SetExporter] 已补回 commonW 根目录 bundle 名: " + fixedNames);
            LayoutEditorPseudoReload.EnsurePrepareForBuilding();
            foreach (var name in names)
                DeleteBundleProduct(name);
            EditorUtility.DisplayProgressBar("重新打包 commonW", "BuildAssetBundles 增量构建…", 0.5f);
            if (!Directory.Exists(AbsPath(BundlesRoot)))
                Directory.CreateDirectory(AbsPath(BundlesRoot));
            var manifest = BuildPipeline.BuildAssetBundles(
                BundlesRoot, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);
            if (manifest == null)
                throw new Exception("BuildPipeline.BuildAssetBundles 返回 null（详见 Console）。");
            var missing = ValidateCommonWProducts();
            if (missing.Count > 0)
                throw new Exception("以下 commonW 源目录存在但构建产物缺失: "
                    + string.Join("、", missing.ToArray())
                    + "\n（常见原因：目录内无可打包内容，或 bundle 名异常——请查看 Console 的构建告警）");
            var sb = new System.Text.StringBuilder();
            foreach (var name in names)
            {
                var abs = AbsPath(BundlesRoot) + "/" + name;
                sb.Append(name).Append(": ")
                    .Append((new FileInfo(abs).Length / 1024f / 1024f).ToString("F2")).Append(" MB（")
                    .Append(File.GetLastWriteTime(abs).ToString("MM-dd HH:mm")).Append("）\n");
            }
            EditorUtility.DisplayDialog("重新打包 commonW", "完成：\n" + sb, "确定");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("重新打包 commonW", "失败:\n" + ex.Message, "确定");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
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
        var runtimeAbs = AbsPath(BundlesRoot) + "/" + RuntimeBundleName;
        if (File.Exists(runtimeAbs))
            entries.Add(new LayoutEditorZipWriter.ZipEntrySource(
                depDir + RuntimeBundleName, runtimeAbs));
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

    // ---- 依赖包清单（deps manifest）与构建信息（package_version.txt） ----

    /// <summary>依赖包清单（GET /api/set/export/deps-manifest）：逐条镜像 AddDependencyEntries
    ///  的条目来源与判定，供导出弹窗展示真实打包内容。只读文件系统 + mtime 比对，
    ///  不触碰 Unity 资产 API。commonW3+ 按源目录判定（导出会删产物重建，按产物判会漏报）。</summary>
    public static DepsManifestDto BuildDepsManifest()
    {
        const string depDir = "OC2DIYLevelRuntimeWLoader/";
        var dto = new DepsManifestDto();
        dto.ok = true;
        dto.runtimeState = RuntimeStageState != null ? RuntimeStageState() : "noStub";
        dto.runtimeVersion = StubVersionValue();
        dto.lastDepsVersion = GetLastDepsVersion();
        var entries = new List<DepsManifestEntryDto>();

        AddManifestFileEntry(entries, depDir + "Loader.dll",
            ProjectRootAbsPath() + "/layout-editor/web/public/Loader.dll",
            "BepInEx 插件：加载依赖与统一运行时（web/public 手动维护副本）");
        AddManifestFileEntry(entries, depDir + "version.txt",
            ProjectRootAbsPath() + "/Assets/WebCustomStubRuntime/Loader~/bin/Release/version.txt",
            "Loader/debugLog 构建版本记录（build.sh 生成）");
        AddManifestFileEntry(entries, depDir + "debugLog.dll",
            ProjectRootAbsPath() + "/layout-editor/web/public/debugLog.dll",
            "会话日志插件（可选，缺失不阻断）");
        AddManifestFileEntry(entries, depDir + "log_config.txt",
            ProjectRootAbsPath() + "/layout-editor/web/public/log_config.txt",
            "debugLog 配置（可选，缺失用内置默认）");
        AddManifestFileEntry(entries, depDir + "readme.txt",
            ProjectRootAbsPath() + "/layout-editor/web/public/readme.txt",
            "安装与使用说明");

        // 统一运行时 bundle：状态 = DLL 编译状态映射到条目契约 ok|stale|missing
        // （fresh→ok；DLL missing 但旧产物仍在 → missing 提示先编译）。
        var runtimeAbs = AbsPath(BundlesRoot) + "/" + RuntimeBundleName;
        var runtimeExists = File.Exists(runtimeAbs);
        var runtimeEntryState = "missing";
        if (runtimeExists && dto.runtimeState == "fresh")
            runtimeEntryState = "ok";
        else if (runtimeExists && dto.runtimeState == "stale")
            runtimeEntryState = "stale";
        AddManifestEntry(entries, depDir + RuntimeBundleName,
            "统一运行时 bundle（CustomStub 关卡代码）；未就绪时先「编译 Runtime DLL」",
            runtimeEntryState,
            runtimeExists ? new FileInfo(runtimeAbs).Length : 0);

        // commonW1/W2：deps 模式无条件携带，导出时删旧产物重建（源 .meta 已标 bundle 名，
        // 必然重新产出）——状态恒为 ok，大小按当前产物展示（仅供参考）。
        var w1Abs = AbsPath(BundlesRoot) + "/commonw1";
        AddManifestEntry(entries, depDir + "commonW1",
            "编辑器增量素材（必备；导出时删旧重打，此处大小为当前产物仅供参考）",
            "ok", File.Exists(w1Abs) ? new FileInfo(w1Abs).Length : 0);
        var w2Abs = AbsPath(BundlesRoot) + "/commonw2";
        AddManifestEntry(entries, depDir + "commonW2",
            "汉堡菜谱素材（依赖包通用，无条件携带；导出时删旧重打）",
            "ok", File.Exists(w2Abs) ? new FileInfo(w2Abs).Length : 0);

        // commonW3+：按源目录 Assets/commonW<数字> 判定（存在即导出时重建并携带）。
        try
        {
            var addedCommonW = new HashSet<int>();
            var assetDirs = Directory.GetDirectories(Application.dataPath.Replace('\\', '/'));
            for (int i = 0; i < assetDirs.Length; i++)
            {
                int index;
                if (!TryGetCommonWIndex(Path.GetFileName(assetDirs[i]), out index) || index < 3)
                    continue;
                if (!addedCommonW.Add(index))
                    continue;
                AddManifestEntry(entries, depDir + "commonW" + index,
                    "扩展素材包（源目录存在即导出时删旧重打并携带；导出前无产物故大小显示 —，" +
                    "可随时用菜单「重新打包 commonW 素材包」单独重打）", "ok", 0);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SetExporter] 清单扫描 commonW3+ 失败：" + ex.Message);
        }

        AddManifestEntry(entries, depDir + "package_version.txt",
            "本次导出自动生成：打包版本/时间/环境 + 全部文件 MD5（排查用）", "ok", 0);

        dto.entries = entries;
        return dto;
    }

    private static void AddManifestFileEntry(List<DepsManifestEntryDto> entries,
        string zipPath, string sourceAbs, string note)
    {
        var exists = File.Exists(sourceAbs);
        AddManifestEntry(entries, zipPath, note, exists ? "ok" : "missing",
            exists ? new FileInfo(sourceAbs).Length : 0);
    }

    private static void AddManifestEntry(List<DepsManifestEntryDto> entries,
        string zipPath, string note, string state, long sizeBytes)
    {
        var e = new DepsManifestEntryDto();
        e.zipPath = zipPath;
        e.label = zipPath.Substring(zipPath.LastIndexOf('/') + 1);
        e.state = state;
        e.sizeBytes = sizeBytes;
        e.note = note;
        entries.Add(e);
    }

    /// <summary>依赖包打包版本规范化：trim → 去前导 v/V → SanitizeVersion → 空/无效回落
    ///  "1.0.0"。独立于运行时 SSOT 版本（requires.txt 门控）与关卡集版本。</summary>
    private static string NormalizeDepsVersion(string version)
    {
        var v = (version ?? "").Trim();
        if (v.Length > 1 && (v[0] == 'v' || v[0] == 'V'))
            v = v.Substring(1).Trim();
        v = SanitizeVersion(v);
        return v == "0" ? "1.0.0" : v;
    }

    private const string DepsVersionFile = "deps_version.txt";

    /// <summary>持久化上次依赖包导出版本（LayoutEditorExports/deps_version.txt），
    ///  清单端点返回给导出弹窗预填（跨浏览器一致）。</summary>
    private static void SaveLastDepsVersion(string depsVersion)
    {
        try
        {
            File.WriteAllText(ExportRootAbsPath() + "/" + DepsVersionFile, depsVersion ?? "");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SetExporter] 记录依赖包版本失败（不影响导出结果）: " + ex.Message);
        }
    }

    /// <summary>上次依赖包导出版本（无记录返回 null，前端回落默认 v1.0.0）。</summary>
    public static string GetLastDepsVersion()
    {
        try
        {
            var path = ExportRootAbsPath() + "/" + DepsVersionFile;
            if (!File.Exists(path))
                return null;
            var v = (File.ReadAllText(path) ?? "").Trim();
            return v.Length > 0 ? v : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>写依赖包构建清单（package_version.txt）到临时目录并返回绝对路径。
    ///  内容 key=value 机器可解析：打包版本/时间（含 UTC 偏移）/Unity 与 OS 环境/
    ///  运行时与 Loader 版本 + 包内全部条目源文件 MD5（store-only zip 源字节=解压后
    ///  字节，校验等价；不含本文件自身——自指无法哈希）。</summary>
    private static string WritePackageVersionFile(string depsVersion,
        List<LayoutEditorZipWriter.ZipEntrySource> entries)
    {
        try
        {
            var dir = ExportRootAbsPath();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var path = dir + "/._deps_pkgver.txt";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# OC2DIYLevelRuntimeWLoader dependency package build info (auto-generated).");
            sb.AppendLine("# Verify installed files against md5 below when troubleshooting.");
            sb.AppendLine("package_version=" + depsVersion);
            sb.AppendLine("build_time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("utc_offset=" + FormatUtcOffset());
            sb.AppendLine("unity_version=" + Application.unityVersion);
            sb.AppendLine("build_os=" + Application.platform + " (" + System.Environment.OSVersion + ")");
            sb.AppendLine("runtime_version=" + StubVersionValue());
            sb.AppendLine("loader_version=" + LoaderBuildVersion("Loader"));
            sb.AppendLine("debuglog_version=" + LoaderBuildVersion("debugLog"));
            sb.AppendLine();
            sb.AppendLine("# file md5 (all package entries, excluding this file)");
            for (int i = 0; i < entries.Count; i++)
            {
                var source = entries[i].SourcePath;
                if (string.IsNullOrEmpty(source) || !File.Exists(source))
                    continue;
                var md5 = ComputeFileMd5(source);
                if (md5 != null)
                    sb.AppendLine(entries[i].FileName + "=" + md5);
            }
            File.WriteAllText(path, sb.ToString());
            return path;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SetExporter] 写 package_version.txt 失败（不影响其余依赖打包）: " + ex.Message);
            return null;
        }
    }

    /// <summary>解析 Loader 构建输出 version.txt 的 key=值（如 Loader=3.6.0）。缺失返回 ""。</summary>
    private static string LoaderBuildVersion(string key)
    {
        try
        {
            var path = ProjectRootAbsPath() + "/Assets/WebCustomStubRuntime/Loader~/bin/Release/version.txt";
            if (!File.Exists(path))
                return "";
            var lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = (lines[i] ?? "").Trim();
                if (line.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                    return line.Substring(key.Length + 1).Trim();
            }
        }
        catch { }
        return "";
    }

    /// <summary>本地时区 UTC 偏移（+08:00 形式；取值失败回落 +00:00）。</summary>
    private static string FormatUtcOffset()
    {
        try
        {
            var offset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);
            var sign = offset < TimeSpan.Zero ? "-" : "+";
            return sign + Math.Abs(offset.Hours).ToString("00") + ":" + Math.Abs(offset.Minutes).ToString("00");
        }
        catch
        {
            return "+00:00";
        }
    }

    /// <summary>计算文件 MD5（小写十六进制）。失败返回 null（单文件失败不阻断清单）。</summary>
    private static string ComputeFileMd5(string absPath)
    {
        try
        {
            using (var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider())
            using (var fs = File.OpenRead(absPath))
            {
                var hash = md5.ComputeHash(fs);
                var sb = new System.Text.StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SetExporter] 计算 MD5 失败（" + absPath + "）: " + ex.Message);
            return null;
        }
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

    /// <summary>v3.5.0 写 stub_levels.txt（逐关卡清单）到临时目录并返回绝对路径。
    /// 内容：'#' 头注释 + 每行 "&lt;关卡&gt;|&lt;特征1,特征2,...&gt;"（levelLines 为
    /// "集|关卡|特征" 前缀匹配后的关卡段）。空清单也写文件（文件缺失会被 loader
    /// 按「旧版导出」处理，导致该集 stub 运行时休眠）。</summary>
    private static string WriteStubLevelsFile(string setName, List<string> levelLines)
    {
        try
        {
            var dir = ExportRootAbsPath();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var path = dir + "/._stublevals_" + setName + ".txt";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# web stub level manifest (v3.5.0+). Format: <level>|<feature,feature,...>");
            sb.AppendLine("# Written by the level-set exporter; consumed by OC2LevelRuntimeLoader/");
            sb.AppendLine("# CustomStub.EntryPoint for per-level gating. Do not edit by hand.");
            for (int i = 0; i < levelLines.Count; i++)
            {
                var line = levelLines[i];
                var firstBar = line.IndexOf('|');
                if (firstBar >= 0 && firstBar < line.Length - 1)
                    sb.AppendLine(line.Substring(firstBar + 1));
            }
            File.WriteAllText(path, sb.ToString());
            return path;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SetExporter] 写 stub_levels.txt 失败（该集将被 loader 按旧版处理，运行时休眠）: " + ex.Message);
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

    /// <summary>Unity 按 bundle 名首段建子目录；收集与关卡集文件夹同名（及历史大小写变体）的产物目录。</summary>
    private static List<string> CollectSetBundleOutputDirs(string setName)
    {
        var dirs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Action<string> add = rel =>
        {
            if (string.IsNullOrEmpty(rel) || !seen.Add(rel))
                return;
            dirs.Add(rel);
        };
        add(BundlesRoot + "/" + setName);
        var setDir = LevelSetsRoot + "/" + setName;
        var importer = AssetImporter.GetAtPath(setDir);
        if (importer != null && !string.IsNullOrEmpty(importer.assetBundleName))
        {
            var slash = importer.assetBundleName.IndexOf('/');
            if (slash > 0)
                add(BundlesRoot + "/" + importer.assetBundleName.Substring(0, slash));
        }
        return dirs;
    }

    private static string ResolveBuiltSetBundleDir(string setName)
    {
        foreach (var rel in CollectSetBundleOutputDirs(setName))
        {
            var abs = AbsPath(rel);
            if (Directory.Exists(abs) && Directory.GetFiles(abs).Length > 0)
                return abs;
        }
        return null;
    }

    /// <summary>关卡集 bundle 前缀应与文件夹名一致（LaTiao/info_LaTiao），否则 OC2DIYLevel
    ///  找不到 info_* 文件、地板材质/LevelInfo 不会加载。</summary>
    private static List<string> ValidateSetBundleNaming(string setName)
    {
        var warnings = new List<string>();
        if (string.IsNullOrEmpty(setName))
            return warnings;
        var expectedInfo = setName + "/info_" + setName;
        var setDir = LevelSetsRoot + "/" + setName;
        var setImporter = AssetImporter.GetAtPath(setDir);
        if (setImporter != null && !string.IsNullOrEmpty(setImporter.assetBundleName)
            && !string.Equals(setImporter.assetBundleName, expectedInfo, StringComparison.Ordinal))
        {
            warnings.Add("关卡集根目录 AssetBundle「" + setImporter.assetBundleName
                + "」与约定「" + expectedInfo + "」不一致——真机可能无法 LoadFromFile info bundle。");
        }
        var scenesDir = setDir + "/scenes";
        if (!AssetDatabase.IsValidFolder(scenesDir))
            return warnings;
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { scenesDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var sceneImporter = AssetImporter.GetAtPath(path);
            if (sceneImporter == null || string.IsNullOrEmpty(sceneImporter.assetBundleName))
                continue;
            var prefix = setName + "/";
            if (!sceneImporter.assetBundleName.StartsWith(prefix, StringComparison.Ordinal))
            {
                warnings.Add("场景「" + path + "」bundle「" + sceneImporter.assetBundleName
                    + "」前缀不是「" + prefix + "」。");
            }
        }
        return warnings;
    }

    private const string EmbeddedBuiltInStandardShader =
        "m_Shader: {fileID: 7, guid: 0000000000000000f000000000000000, type: 0}";

    /// <summary>导出前扫描场景 YAML：内嵌 built-in Standard 材质在 Player 中会粉紫，
    ///  应持久化为 LevelSets/&lt;set&gt;/materials/ 资产。</summary>
    private static List<string> ValidateSceneForPlayerBuild(string sceneAssetPath)
    {
        var warnings = new List<string>();
        var abs = AbsPath(sceneAssetPath);
        if (string.IsNullOrEmpty(abs) || !File.Exists(abs))
            return warnings;

        var text = File.ReadAllText(abs);
        var sections = text.Split(new[] { "--- !u!21 &" }, StringSplitOptions.None);
        for (int i = 1; i < sections.Length; i++)
        {
            var block = sections[i];
            if (!block.StartsWith("Material:", StringComparison.Ordinal)
                && block.IndexOf("\nMaterial:", StringComparison.Ordinal) < 0)
                continue;
            if (!block.Contains("m_PrefabInternal: {fileID: 0}"))
                continue;
            if (!block.Contains(EmbeddedBuiltInStandardShader))
                continue;

            var nameMatch = Regex.Match(block, @"m_Name:\s*(.+)");
            var matName = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "(unknown)";
            warnings.Add(sceneAssetPath + ": 内嵌 Standard Shader 材质「" + matName
                + "」在 Player 中会粉紫；请持久化到 LevelSets/<set>/materials/ 资产。");
            if (Regex.IsMatch(matName, @"_tiling\d+x\d+", RegexOptions.IgnoreCase))
            {
                warnings.Add(sceneAssetPath + ": 烘焙地板材质「" + matName
                    + "」应持久化为工程 .mat 资产而非场景内嵌。");
            }
        }

        var sceneImporter = AssetImporter.GetAtPath(sceneAssetPath);
        if (sceneImporter != null && !string.IsNullOrEmpty(sceneImporter.assetBundleName))
        {
            var sceneBundle = sceneImporter.assetBundleName;
            foreach (Match m in Regex.Matches(text, @"guid:\s*([a-f0-9]{32})", RegexOptions.IgnoreCase))
            {
                var refPath = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                if (string.IsNullOrEmpty(refPath) || refPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    continue;
                var refImporter = AssetImporter.GetAtPath(refPath);
                if (refImporter == null || string.IsNullOrEmpty(refImporter.assetBundleName))
                    continue;
                if (refImporter.assetBundleName != sceneBundle)
                    continue;
                warnings.Add(sceneAssetPath + ": 资产「" + refPath + "」与场景共用 bundle「"
                    + sceneBundle + "」——Unity 禁止 scene+asset 同包，请改为 info bundle。");
            }
        }
        return warnings;
    }

    /// <summary>信息级：场景引用的 commonW1 资产（由 Loader 常驻加载，非 stub）。</summary>
    private static string DescribeCommonW1PrefabRefs(string sceneAssetPath)
    {
        var abs = AbsPath(sceneAssetPath);
        if (string.IsNullOrEmpty(abs) || !File.Exists(abs))
            return null;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var text = File.ReadAllText(abs);
        foreach (Match m in Regex.Matches(text, @"guid:\s*([a-f0-9]{32})", RegexOptions.IgnoreCase))
        {
            var path = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
            if (string.IsNullOrEmpty(path))
                continue;
            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/commonW1/", StringComparison.OrdinalIgnoreCase))
                continue;
            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                seen.Add(path);
        }
        if (seen.Count == 0)
            return null;
        return sceneAssetPath + ": 引用 commonW1 预制体 " + seen.Count + " 个（Loader 常驻，非 CustomStub）："
            + string.Join(", ", new List<string>(seen).ToArray());
    }
}
