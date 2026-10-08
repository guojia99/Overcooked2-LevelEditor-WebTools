using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
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
    ///     WebManifest。清单为空且无 web 导出集 = 完全休眠（零加载零事件零介入）；
    ///     清单为空但存在 stub_levels.txt 文件（v3.7.0）= 仍激活（commonW 素材 +
    ///     双 stub 自愈——commonW 包只由本加载器加载，空清单休眠会让只引用
    ///     commonW 素材的关卡进图卡加载）；非空 = 激活：挂 AssemblyResolve +
    ///     从自身目录把所有 commonW+数字 bundle LoadFromFile 常驻（幂等）+
    ///     加载统一运行时 bundle webcustomstub_runtime → Assembly.Load
    ///     (WebCustomStubRuntime) → 反射 CustomStub.EntryPoint.Install()
    ///     （EntryPoint 反射读 WebManifest 做逐关卡闸门）。原版 bundle 仍靠 OC2DIYLevel。
    ///  2. 每关卡集 *_custom_runtime 文件（预留：某关卡专属代码）仍支持加载；
    ///     旧版 runtime 文件（含旧每集 Stub_&lt;set&gt; 代码）一律忽略并告警（不识别旧代码）。
    ///  3. 版本门控：关卡集 requires.txt（= 依赖包 SSOT 版本）与自身 PluginVersion semver
    ///     比较——已装依赖 &gt;= requires 才提供 stub 支持，&lt; 则告警跳过（关卡本体仍加载）。
    ///
    /// 场景自愈（RandomCrate| 等 tag）统一收编于 CustomStub.EntryPoint（本 loader 不再
    /// 自行 HealScene），loader 只负责程序集/依赖加载 + 每次场景加载幂等补扫。
    ///
    /// v3.7.0（2026-10-06 空清单休眠→进图卡加载修复·双 stub 自愈随运行时分发）：
    ///  - 根因①（空清单休眠）：v3.5.0 零介入铁律把 commonW 素材包加载耦合在
    ///    「stub 特征清单非空」上——只引用 commonW 素材（MixerBowl/烤盘/web 食材）
    ///    而无 CustomStub 玩法的关卡，导出的 stub_levels.txt 为空 → 加载器休眠
    ///    → commonW 不加载 → 场景 bundle 外部引用（cab）无法解析 → 进图卡加载
    ///    （LaTiao 0.9.1+ 导出实锤）。修复：任一集存在 stub_levels.txt 文件（即使
    ///    0 条目）即完整激活（commonW + 统一运行时）；导出器同步补 "web" 特征
    ///    （场景/LevelInfo 引用 commonW 素材即写入清单行），双保险。
    ///  - 根因②（双 stub，随本版运行时分发）：编辑器 wrapper 升级路径补挂派生
    ///    stub 后残留基础 PseudoPrefabStub（组件序在前），真机 OC2DIYLevel 按
    ///    派生 stub 分类挂派生运行时，PseudoPrefab.Awake 的
    ///    GetComponent&lt;PseudoPrefabStub&gt;() 命中基础 stub → 派生 Setup 强转抛
    ///    InvalidCastException 中断 ResetAllPseudoPrefabs 全链 → 关卡永久卡加载
    ///    （diantang / jia_level_2_1 酱料机实锤）。修复：运行时 EntryPoint 装
    ///    ResetAllPseudoPrefabs 前缀自愈（移除基础 stub），编辑器导出前自动
    ///    修复（LayoutEditorDualStubRepair）。
    ///  - 根因③（进图竞态窗口）：异步依赖队列全程 ~15s（commonW2 解压实测 ~10s），
    ///    快速连按空格/A 跳过菜单可在队列完成前进图——commonW 未就位（场景外部
    ///    引用无法解析）或运行时未装（自愈补丁缺席）→ 同样永久卡加载。修复：
    ///    ①进图闸门——AssetBundleManager.LoadLevelAsync 前缀，队列未完成时同步
    ///    收口 DrainPendingSync（在途/待队 op 阻塞取 assetBundle，解压仍在工作
    ///    线程，主线程仅在加载界面等待；协程见 _syncTakeover 让位防双份）；
    ///    ②运行时 bundle 优先入队——EntryPoint/自愈补丁秒级就位，不等 commonW
    ///    长解压。闸门仅激活态安装，休眠（官方图玩家）零介入不变。
    /// v3.6.0（2026-09-29 联机按钮-传送带偶发丢步修复·版本对齐）：**loader 逻辑
    ///     零变更**，仅与 CustomStub 运行时 SSOT（StubVersion 3.6.0）对齐——本版
    ///     运行时新增 NodeRingSync 节点环步数对账（联机同帧 BLPress 合并丢步 →
    ///     瞬跳终态校正）、ButtonLogicRelay 派发门控（Pending 语义：不边送边转）、
    ///     ConveyorDirectionSync 吸附基准重捕，详见 StubVersion 3.6.0 条目。对齐后
    ///     requires.txt 门控统一为 3.6.0（旧 loader 载入 3.6.0 导出集会告警跳过
    ///     stub 支持，属预期防混装行为）。
    /// v3.5.9（2026-09-29 回退 info bundle 预载）：v3.5.8 的 Plan A 在真机翻车——
    ///     **Unity 2017.4 对同一 bundle 文件的二次 LoadFromFile 不返回缓存实例，
    ///     而是报错 "can't be loaded because another AssetBundle with the same
    ///     files is already loaded" 并返回 null**（真机日志实锤，9 个集全部
    ///     failed loading → 关卡列表全空）。OC2DIYLevel 的同步 LoadFromFile 与
    ///     任何形式的提前加载互斥——该 15s 卡顿只能由上游修（启动时异步预载 /
    ///     先查 GetAllLoadedAssetBundles）。本版整体移除预载逻辑，恢复 v3.5.7
    ///     行为。⚠ 教训入档：**不要尝试替外部模组预热它自己会 LoadFromFile 的
    ///     bundle 文件**。
    /// v3.5.8（2026-09-29 info bundle 启动预载 + 日志减负）：【info 预载已被
    ///     v3.5.9 回退】。保留下来的部分：
    ///  1. **Plan A**：启动扫描收集所有 levels/&lt;set&gt;/info_* bundle，随异步队列
    ///     在启动期预载（排在 commonW*/统一运行时之前——进关卡选择早于进关卡）。
    ///     根因（v3.5.7 资产跟踪实锤）：OC2DIYLevel 进关卡选择时在主线程**同步**
    ///     LoadFromFile + LoadAsset 每个集的 info bundle（LZMA 整包解压，实测 11 条
    ///     慢调用合计 ~15s = 看门狗的 14.3s 冻结）。Unity 对同内部名已加载 bundle 的
    ///     再次 LoadFromFile 返回缓存实例 → 点击路径的同步加载归零。验证方式：
    ///     预载后 [资产跟踪] 不应再出现在 info_* 的 ≥100ms 慢调用行。
    ///     ⚠ 收集不受 requires.txt 版本门控影响（旧格式集同样会被 OC2DIYLevel
    ///     同步加载）；入队仍受 stub 清单激活门控（休眠模式零加载不变）。
    ///  2. 日志减负（用户反馈：日志不得影响正常游戏）：[心跳]/[资产跟踪]10s 汇总
    ///     降为 Verbose；TraceAssetLoads 默认改 false（≥100ms 慢调用明细行本身
    ///     极稀有、开销≈0，排障期间可在 cfg 开启——本机已存的 true 继续生效）。
    /// v3.5.7（2026-09-29 跟踪器去洪水）：v3.5.6 逐调用打日志在真机翻车——实测
    ///     关卡选择界面存在**每帧 30-40 次**的 LoadAsset 高频轮询（OC2DIYLevel/游戏
    ///     侧行为），2 分钟 26.6 万次调用、日志 65.7MB，逐条字符串+文件写直接拖慢
    ///     加载。改为：快调用（&lt;100ms）只原子计数零日志；仅 ≥100ms 慢调用输出
    ///     明细行（真凶正是这类——v3.5.6 抓到 11 条 info_* bundle 同步加载合计
    ///     ~15s，与看门狗的 14.3s 冻结吻合）；另每 10s 一行调用汇总（可见轮询量
    ///     不刷屏）；Stopwatch ThreadStatic 复用去分配。
    /// v3.5.6（2026-09-29 资源加载跟踪）：[Diagnostics] TraceAssetLoads（默认 true）
    ///     给三个**同步阻塞**入口挂 Harmony postfix 计时日志——AssetBundle.
    ///     LoadFromFile / LoadAsset(string,Type) / LoadAllAssets(Type)。动机：
    ///     看门狗实测「进关卡选择界面 14.3s 冻结」「点击关卡→存档弹窗 4-5 次
    ///     300-700ms 抖动」两处卡顿期间**零游戏日志**（不经过 AssetBundleManager
    ///     的带日志路径）——需要逐调用计时才能指认静默的同步资源工作。postfix
    ///     纯日志不改行为；仅激活态（stub 清单非空）安装，休眠模式零介入不变。
    /// v3.5.5（2026-09-29 看门狗配置键改名）：[Logging] FrameStallWatch → StallWatchdog。
    ///     根因：BepInEx 配置文件**已保存的值优先于新默认值**——v3.5.3 首次运行把
    ///     false 写进 cfg 后，v3.5.4 把默认值改 true 完全无效（连续两轮排障看门狗
    ///     均未生效）。改名后新键无历史值，默认 true 直接生效；cfg 里遗留的
    ///     FrameStallWatch=false 为无主旧键，不生效可无视。
    /// v3.5.4（2026-09-29 卡顿定位增强）：①帧卡顿看门狗改为**默认开启**（连续两轮
    ///     排障均因未改配置而看门狗未生效——去掉这个易漏步骤；仍可在
    ///     BepInEx 配置 [Logging] FrameStallWatch=false 关闭）。②异步加载等待期
    ///     每 2s 打一行 [心跳]：心跳间隔远超 2s = 主线程在那个窗口硬卡（冻结期间
    ///     协程不被推进），与 [帧卡顿] 互相印证。
    /// v3.5.3（2026-09-29 卡顿定位工具）：①帧卡顿看门狗（[Logging] FrameStallWatch，
    ///     默认关）：相邻两帧间隔超 100ms 即记录一行 [帧卡顿] 日志（时间戳/帧号/
    ///     场景名/间隔毫秒）——本行时间=卡顿结束，上一条日志时间≈卡顿开始，对照
    ///     两行之间的日志即可判断责任方（loader / 游戏本体 / 其它模组）；纯被动
    ///     计时（读 Time + 打日志），不 hook 不扫描，休眠模式下也可用（排障对照）。
    ///     ②主线程耗时打点（常开、一次性）：Assembly.Load / GetTypes /
    ///     EntryPoint.Install 各自毫秒数随行输出——Install 含 GameApi 反射初始化
    ///     与自检，是异步化后残余的主线程大头，可直接从日志读出。
    /// v3.5.2（2026-09-29 启动读档卡顿修复）：依赖/统一运行时/自定义 runtime 的
    ///     AssetBundle.LoadFromFile 全部改为 LoadFromFileAsync 协程分帧加载。
    ///     旧版在启动首几帧的主线程上同步加载 commonW1+W2+W3（合计约 56MB，且为
    ///     LZMA 压缩——LoadFromFile 对 LZMA 须整包同步解压），与游戏 Bootstrap
    ///     读档窗口（PCSaveManager.BootstrapAwake 的 File.ReadAllBytes+ByteLoad
    ///     同样跑主线程）撞车，实测表现为「读存档卡 2-5 秒后正常」。异步后解压
    ///     在工作线程、主线程逐帧收结果；加载完成点仍在启动期（drain 后数帧），
    ///     远早于任何 web 关卡场景加载，MonoScript 程序集解析时序铁律不受影响。
    ///     *_custom_runtime 预留通道一并收编进同一异步队列。
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
        public const string PluginVersion = "3.7.0";

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
        private static bool _resolveHooked;

        // ---- v3.5.2 依赖/运行时异步加载状态（替代旧版布尔幂等位 _depsLoaded：
        // 协程在途时 sceneLoaded 补扫不得误判「已加载」而跳过等待）----
        /// <summary>异步加载状态：0=未开始，1=队列运行中，2=完成（含失败收场）。</summary>
        private static int _depLoadState;

        /// <summary>队列协程在途标记（与 _depLoadState 分离：队列清空后仍可能有
        /// 后续扫描结果入队需要重启队列）。</summary>
        private static bool _queueRunning;

        /// <summary>插件实例（StartCoroutine 需要；Awake 赋值）。</summary>
        private static LevelRuntimeLoader _instance;

        /// <summary>待加载 bundle 队列（只在工作于主线程的入队点与协程之间传递；
        /// 协程逐个 LoadFromFileAsync 分帧消费）。</summary>
        private static readonly List<LoadOp> _pendingOps = new List<LoadOp>();

        /// <summary>v3.7.0 同步收口（进图闸门）：协程被 DrainPendingSync 接管——
        /// 协程恢复后见此标记直接退出，防双份处理（在途 op 由收口方处理完毕）。</summary>
        private static bool _syncTakeover;

        /// <summary>协程当前在途的 op 与其异步请求（收口时阻塞取回结果；主线程
        /// 单线程，协程与闸门前缀不会并发执行，无竞态）。</summary>
        private static AssetBundleCreateRequest _inflightReq;
        private static LoadOp _inflightOp;

        /// <summary>队列完成统计（协程/同步收口共用；EnsureQueueRunning 起队时清零）。</summary>
        private static int _qBundlesDone;
        private static int _qAssembliesLoaded;

        /// <summary>web stub 关卡清单（v3.5.0，stub 侧 CustomStub.EntryPoint 反射读取，
        /// 与 StubLog 读取 LogFromCrate 同款桥接方向）。每条 "集|关卡|特征1,特征2,..."，
        /// 由启动扫描汇总 levels/&lt;set&gt;/stub_levels.txt 而来。清单为空且无 web 导出集
        /// = 加载器整体休眠；清单为空但装有 web 导出集（v3.7.0）仍激活（commonW 素材 +
        /// 双 stub 自愈）。EntryPoint 据此做逐关卡闸门：清单未命中的场景（官方图/旧集/
        /// 未用 CustomStub 的 web 关卡）一律零介入。</summary>
        public static string[] WebManifest = new string[0];

        /// <summary>v3.7.0：任一已装集存在 stub_levels.txt 文件（即使 0 条目）。
        /// 空清单激活语义的开关（commonW 素材 + 双 stub 自愈，见 ApplyScanResult）。</summary>
        private static bool _anyWebExportedSet;

        #region 日志前缀：时间戳 + 主/客机角色（联机排障区分两台机器）

        private static MethodInfo _miIsInSession;
        private static MethodInfo _miIsHost;
        private static bool _roleProbed;

        /// <summary>诊断日志开关（BepInEx 配置 [Logging] Verbose，默认 false）。
        /// stub 侧 CustomStub.StubLog 会反射读取本属性作为统一开关——改名需同步
        /// （StubLog.ResolveVerbose 找的就是 "VerboseEnabled"）。</summary>
        private static ConfigEntry<bool> _cfgVerbose;

        /// <summary>帧卡顿看门狗开关（[Logging] StallWatchdog，v3.5.5 起默认 true。
        /// v3.5.5 改键名：旧键 FrameStallWatch 在已生成的 cfg 里存了 false 且
        /// BepInEx「已保存值优先于新默认值」会压住改默认——换键绕开历史值）。
        /// 纯被动计时：读 Time + 超阈值打日志，不 hook 不扫描——休眠模式同样可用
        /// （对照实验：拔掉 web 关卡集后仍卡 = 责任不在 loader）。</summary>
        private static ConfigEntry<bool> _cfgFrameWatch;

        /// <summary>资源加载跟踪开关（[Diagnostics] TraceAssetLoads，v3.5.6，默认 true）。
        /// 给 AssetBundle 的三个同步阻塞入口挂 postfix 计时日志——冻结若发生在这类
        /// 调用内部，该行会以巨大耗时直接指认元凶。仅激活态（清单非空）安装。</summary>
        private static ConfigEntry<bool> _cfgTraceAssets;

        public static bool TraceAssetsEnabled
        {
            get { return _cfgTraceAssets != null && _cfgTraceAssets.Value; }
        }

        public static bool VerboseEnabled
        {
            get { return _cfgVerbose != null && _cfgVerbose.Value; }
        }

        public static bool FrameWatchEnabled
        {
            get { return _cfgFrameWatch != null && _cfgFrameWatch.Value; }
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
            _instance = this;
            _log = Logger;
            _cfgVerbose = Config.Bind("Logging", "Verbose", false,
                "输出 CustomStub 运行时与本加载器的诊断级日志（掷骰/取出、逐物体自愈、反射自检、"
                + "目录清单等）。默认关闭——这些日志在稳态游玩期是每秒数条的纯开销。排障时开启。");
            _cfgFrameWatch = Config.Bind("Logging", "StallWatchdog", true,
                "帧卡顿看门狗（默认开启；v3.5.5 起-key-名从 FrameStallWatch 改为本键，旧键已废弃请删除）："
                + "相邻两帧间隔超过 100ms 时记录一行 [帧卡顿] 日志（时间戳/帧号/场景名/间隔毫秒）。"
                + "用于定位卡顿发生的确切时间点：本行时间=卡顿结束，上一条日志时间≈卡顿开始，"
                + "对照两行之间的日志即可判断责任方（loader / 游戏本体 / 其它模组）。正常游玩几乎不产生日志；如嫌噪音可改为 false。");
            _cfgTraceAssets = Config.Bind("Diagnostics", "TraceAssetLoads", false,
                "资源加载跟踪（默认关闭；v3.5.8 起仅排障时开启）：给 AssetBundle.LoadFromFile / "
                + "LoadAsset / LoadAllAssets 挂计时，仅耗时 ≥100ms 的慢调用输出一行 [资产跟踪] 明细"
                + "（快调用零日志零字符串，开销≈0，不影响游戏）。已立功：定位过 OC2DIYLevel 同步加载 "
                + "info_* bundle 合计 ~15s 的进关卡选择冻结。仅在使用 web 导出关卡的环境激活。");
            // v3.5.0 零介入铁律：Awake 不再预载依赖/统一运行时、不挂 AssemblyResolve。
            // 先等启动后台扫描汇总 levels/<set>/stub_levels.txt 清单，只有存在
            // web 导出的 stub 关卡（清单非空）才激活（ApplyScanResult）——未用 web
            // 导出的环境自此一个字节都不加载、一个事件都不挂。
            // 激活时机的时序安全性：扫描在 Update 首帧发起、数帧内 Drain，仍远早于
            // 任何关卡场景加载（主菜单在前）；EntryPoint.Install 只需早于场景里
            // MonoScript 的程序集解析即可。
            LogI("v" + PluginVersion + " ready（清单驱动：仅当存在 web 导出 stub 关卡时才加载依赖/运行时）"
                + (VerboseEnabled ? "｜诊断日志已开启" : "")
                + (FrameWatchEnabled ? "｜帧卡顿看门狗已开启（阈值 100ms）" : "")
                + (TraceAssetsEnabled ? "｜资源加载跟踪待激活安装（TraceAssetLoads）" : ""));
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
        /// v3.5.0：清单为空且无任何 web 导出集（休眠模式）时一个字节都不加载。
        /// v3.7.0：装有 web 导出集（任一集存在 stub_levels.txt 文件，即使空清单）仍激活
        /// ——commonW 素材只由本加载器加载，空清单休眠会让该类关卡进图卡加载。
        /// v3.5.2：入队后交给 LoadQueueCoroutine 异步分帧加载（LoadFromFileAsync，
        /// 解压在工作线程）——同步 LoadFromFile 对 LZMA bundle 须主线程整包解压
        /// （commonW1+W2+W3 约 56MB），曾与游戏 Bootstrap 读档窗口撞车卡 2-5 秒。
        /// 入队判定（已加载/存在性检查）仍同步做——单次目录枚举开销可忽略。</summary>
        private static void LoadDependenciesAndRuntime()
        {
            if (_depLoadState != 0)
                return; // 1=队列运行中 / 2=已完成（幂等：OnSceneLoadedHeal 每次场景都会调）
            if (WebManifest.Length == 0 && !_anyWebExportedSet)
                return; // 零介入休眠：无任何 web 导出关卡集，不加载依赖/运行时
            _depLoadState = 1;
            var dir = OwnDir();
            if (string.IsNullOrEmpty(dir))
            {
                LogW("无法解析 Loader.dll 所在目录，跳过依赖/运行时加载（自愈补扫仍会尝试）");
                EnsureQueueRunning();
                return;
            }
            LogI("依赖包目录: " + dir);
            LogI("[断点:依赖] 开始检查统一运行时与 commonW1、commonW2、commonW3...（异步分帧加载；运行时优先入队—— EntryPoint/双 stub 自愈补丁 ~秒级就位，不等 commonW2/W3 的长解压）");

            // 1. 统一运行时 bundle → *.dll.bytes → Assembly.Load → EntryPoint.Install
            //    （v3.7.0 起优先入队：运行时包小、秒级完成，EntryPoint 与双 stub 自愈
            //    补丁尽早可用；commonW2/W3 解压慢（实测 ~10s）不阻塞运行时就位）。
            try
            {
                var rtPath = Path.Combine(dir, RuntimeBundleFileName);
                if (!File.Exists(rtPath))
                {
                    LogW("未找到统一运行时 " + RuntimeBundleFileName + "（" + rtPath
                        + "）——CustomStub 玩法将无法生效。请安装/更新依赖包 OC2DIYLevelRuntimeWLoader。");
                    LogP("未找到统一运行时文件，随机箱、火锅等自定义玩法无法启用。请更新 OC2DIYLevelRuntimeWLoader 依赖包。");
                }
                else if (LoadedBundles.Contains(rtPath))
                {
                    // 之前已加载过（如二次扫描），跳过
                }
                else
                {
                    _pendingOps.Add(new LoadOp(LoadOpKind.Runtime, RuntimeBundleFileName, rtPath));
                }
            }
            catch (Exception ex)
            {
                LogW("统一运行时入队异常: " + ex);
                LogP("自定义关卡运行组件启动失败，部分自定义玩法可能无法使用。请将 logs 目录发送给开发者。原因: " + ex.Message);
            }

            // 2. 动态发现 commonW1、commonW2、commonW3...（供 stub 组件按名解析；
            //    不把未来新增的依赖包写死在 Loader 中）。
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
                    _pendingOps.Add(new LoadOp(LoadOpKind.Dep, name, path));
                }
                catch (Exception ex)
                {
                    LogW("  " + name + " 入队异常: " + ex.Message);
                }
            }
            EnsureQueueRunning();
        }

        #region 依赖/运行时异步加载队列（v3.5.2：消除启动读档卡顿）

        private enum LoadOpKind
        {
            Dep,     // commonW* 依赖 bundle
            Runtime, // 统一运行时 webcustomstub_runtime
            Custom   // 关卡集 *_custom_runtime（预留通道）
        }

        private sealed class LoadOp
        {
            public readonly LoadOpKind Kind;
            public readonly string Name;
            public readonly string Path;

            public LoadOp(LoadOpKind kind, string name, string path)
            {
                Kind = kind;
                Name = name;
                Path = path;
            }
        }

        /// <summary>启动/重启加载队列协程（幂等）。仅主线程调用。</summary>
        private static void EnsureQueueRunning()
        {
            if (_queueRunning)
                return;
            if (_instance == null)
            {
                // Awake 必先于 Update/sceneLoaded 执行，理论不可达；保守重置状态待重试
                // （同时清空队列——重试时会重新入队，防重复加载同一 bundle）。
                LogW("插件实例未就绪，异步加载队列未启动（将在下次场景加载重试）");
                _pendingOps.Clear();
                _depLoadState = 0;
                return;
            }
            _queueRunning = true;
            _syncTakeover = false; // 新一轮队列：清除上一轮的同步收口接管标记
            _qBundlesDone = 0;
            _qAssembliesLoaded = 0;
            _instance.StartCoroutine(RunSafe(LoadQueueCoroutine()));
        }

        /// <summary>队列消费协程（内层迭代器）：逐个 LoadFromFileAsync → 分帧等待 →
        /// 按 kind 处理。⚠ C#4 铁律：yield 不得出现在带 catch 的 try 块内——本迭代器
        /// 的 yield 全部裸露，各步处理各自的 try/catch 均不含 yield。
        /// v3.7.0：等待结束发现 _syncTakeover（进图闸门同步收口接管）→ 直接退出，
        /// 在途 op 由 DrainPendingSync 处理（防双份）；完成统计与收场日志统一在
        /// OnQueueFinished。</summary>
        private static IEnumerator LoadQueueCoroutine()
        {
            while (_pendingOps.Count > 0)
            {
                var op = _pendingOps[0];
                _pendingOps.RemoveAt(0);
                LogV("异步加载 bundle: " + op.Path);
                AssetBundleCreateRequest req = null;
                try { req = AssetBundle.LoadFromFileAsync(op.Path); }
                catch (Exception ex)
                {
                    LogW("  bundle 异步加载发起异常 " + op.Path + ": " + ex.Message);
                    continue;
                }
                var swWait = System.Diagnostics.Stopwatch.StartNew();
                var nextBeat = 2.0;
                _inflightOp = op;
                _inflightReq = req;
                if (req != null)
                {
                    while (!req.isDone)
                    {
                        yield return req;
                        // v3.5.4 心跳：正常每 2s 一行；间隔远超 2s = 主线程在那个
                        // 窗口硬卡（冻结期间协程不被推进）——与 [帧卡顿] 互相印证。
                        if (swWait.Elapsed.TotalSeconds >= nextBeat)
                        {
                            LogV("[心跳] " + Path.GetFileName(op.Path) + " 异步加载进行中 "
                                + swWait.Elapsed.TotalSeconds.ToString("F0")
                                + "s（心跳持续=主线程活着；长间隔=主线程硬卡）");
                            nextBeat += 2.0;
                        }
                    }
                }
                swWait.Stop();
                if (_syncTakeover)
                    yield break; // 进图闸门已同步收口接管：在途 op 已被处理，防双份直接退出
                var bundle = req == null ? null : req.assetBundle;
                ProcessBundleResult(op, bundle, swWait.ElapsedMilliseconds);
                _inflightOp = null;
                _inflightReq = null;
            }
        }

        /// <summary>单个 bundle 加载结果处理（协程与 v3.7.0 同步收口共用）：
        /// Dep=常驻日志；Runtime=*.dll.bytes → Assembly.Load → EntryPoint.Install；
        /// Custom=逐 DLL 加载。bundle 为 null 走告警分支。统计进 _qBundlesDone/
        /// _qAssembliesLoaded（收场日志在 OnQueueFinished）。</summary>
        private static void ProcessBundleResult(LoadOp op, AssetBundle bundle, long decompressMs)
        {
            try
            {
                if (bundle == null)
                {
                    if (op.Kind == LoadOpKind.Dep)
                    {
                        LogW("  " + op.Name + " 加载失败（LoadFromFileAsync 返回 null）: " + op.Path);
                        LogP("依赖资源 " + op.Name + " 加载失败，相关自定义关卡功能可能无法正常显示。请重新安装依赖包。");
                    }
                    else if (op.Kind == LoadOpKind.Runtime)
                    {
                        LogW("统一运行时 bundle 加载失败（LoadFromFileAsync 返回 null）: " + op.Path);
                        LogP("统一运行时加载失败，自定义玩法无法启用。请重新安装依赖包。");
                    }
                    else
                    {
                        LogW("自定义 runtime 加载失败（LoadFromFileAsync 返回 null）: " + op.Path);
                    }
                    return;
                }
                if (op.Kind == LoadOpKind.Dep)
                {
                    LogI("  " + op.Name + " 已加载进内存并常驻（不 Unload；后台解压 "
                        + decompressMs + " ms——该时长不占主线程，主线程帧间隔看 [帧卡顿]）");
                }
                else if (op.Kind == LoadOpKind.Runtime)
                {
                    LoadedBundles.Add(op.Path);
                    var swProc = System.Diagnostics.Stopwatch.StartNew();
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
                        {
                            loaded++;
                            _qAssembliesLoaded++;
                        }
                    }
                    swProc.Stop();
                    if (loaded == 0)
                    {
                        LogW("统一运行时 bundle 内无 *.dll.bytes（打错包或旧包？）");
                        LogP("统一运行时文件内容不完整，自定义玩法无法启用。请重新导出或安装最新依赖包。");
                    }
                    LogI("[断点:依赖] 统一运行时加载完成，程序集数量=" + loaded
                        + "，主线程处理耗时=" + swProc.ElapsedMilliseconds
                        + " ms（明细见上行 Assembly.Load/Install 分项）");
                }
                else
                {
                    LogI("加载自定义 runtime: " + op.Path);
                    LoadedBundles.Add(op.Path);
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
                            _qAssembliesLoaded++;
                    }
                    if (dllCount == 0)
                        LogW("  自定义 runtime 内没有任何 *.dll.bytes 资产。bundle 内全部资产: "
                            + string.Join(", ", bundle.GetAllAssetNames()));
                }
                _qBundlesDone++;
            }
            catch (Exception ex)
            {
                if (op.Kind == LoadOpKind.Runtime)
                {
                    LogW("统一运行时加载异常: " + ex);
                    LogP("统一运行时加载时发生错误，自定义玩法可能无法使用。请将 logs 目录发送给开发者。原因: " + ex.Message);
                }
                else
                {
                    LogW("自定义 runtime 处理异常 " + op.Path + ": " + ex);
                }
            }
        }

        /// <summary>安全驱动器（外层手动枚举内层协程 + try/catch——C#4 不能在有
        /// catch 的 try 里 yield，故 yield 均在 catch 作用域之外）。内层异常：
        /// 终止队列、置完成态、告警不外抛——等价旧版同步路径的逐环节 catch 收场。</summary>
        private static IEnumerator RunSafe(IEnumerator inner)
        {
            while (inner != null)
            {
                bool moved = false;
                Exception error = null;
                try { moved = inner.MoveNext(); }
                catch (Exception ex) { error = ex; }
                if (error != null)
                {
                    LogW("异步加载协程异常（队列终止）: " + error);
                    LogP("自定义关卡运行组件启动中断，部分自定义玩法可能无法使用。请将 logs 目录发送给开发者。原因: "
                        + error.Message);
                    OnQueueFinished();
                    yield break;
                }
                if (!moved)
                {
                    OnQueueFinished();
                    yield break;
                }
                yield return inner.Current;
            }
        }

        /// <summary>队列收场：清协程在途标记 + 置完成态（失败也置 2——旧版同步路径
        /// 同样是告警后继续，不做无限重试）。v3.7.0：完成统计日志统一在此（协程正常
        /// 结束/异常终止/同步收口三条路径共用；幂等——收口先行收场后协程恢复再触发
        /// 时早退，不重复置态/打日志）。</summary>
        private static void OnQueueFinished()
        {
            if (!_queueRunning && _depLoadState == 2)
                return; // 已收场（同步收口先行，协程恢复后二次触达）
            _queueRunning = false;
            _depLoadState = 2;
            LogI("[断点:依赖] 异步加载队列完成: bundle " + _qBundlesDone
                + " 个，本次新加载程序集 " + _qAssembliesLoaded + " 个");
        }

        /// <summary>v3.7.0 同步收口（进图闸门核心）：接管异步队列剩余 op，把「快速
        /// 跳过菜单进图撞上加载窗口」竞态转化为加载界面里的一次性等待——
        ///  - 在途 op：阻塞取 assetBundle（工作线程照常解压完成，主线程等待；协程
        ///    恢复后见 _syncTakeover 直接退出，防双份处理）；
        ///  - 未开始 op：发起 LoadFromFileAsync 后阻塞等待（解压仍走工作线程，
        ///    不重蹈 v3.5.2 修掉的主线程 LZMA 整包解压）；
        ///  - 逐个 ProcessBundleResult（运行时 Assembly.Load → EntryPoint.Install，
        ///    双 stub 自愈补丁随 Install 就位）→ 收口完成后原方法继续，场景 bundle
        ///    加载时 commonW/运行时全部就位。
        /// 仅主线程调用（Harmony 前缀）；异常不外抛（放行原方法）。</summary>
        private static void DrainPendingSync(string reason)
        {
            _syncTakeover = true; // 协程恢复后直接退出（RunSafe→OnQueueFinished，重复置态无害）
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int drained = 0;
            if (_inflightReq != null && _inflightOp != null)
            {
                var op = _inflightOp;
                AssetBundle bundle = null;
                var swOp = System.Diagnostics.Stopwatch.StartNew();
                try { bundle = _inflightReq.assetBundle; } // 阻塞至工作线程完成
                catch (Exception ex) { LogW("  [收口] 在途 bundle 取回异常 " + op.Path + ": " + ex.Message); }
                _inflightReq = null;
                _inflightOp = null;
                ProcessBundleResult(op, bundle, swOp.ElapsedMilliseconds);
                drained++;
            }
            while (_pendingOps.Count > 0)
            {
                var op = _pendingOps[0];
                _pendingOps.RemoveAt(0);
                AssetBundle bundle = null;
                var swOp = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var req = AssetBundle.LoadFromFileAsync(op.Path);
                    if (req != null)
                        bundle = req.assetBundle; // 发起异步后阻塞等待：解压在工作线程
                }
                catch (Exception ex)
                {
                    LogW("  [收口] bundle 同步等待异常 " + op.Path + ": " + ex.Message);
                }
                ProcessBundleResult(op, bundle, swOp.ElapsedMilliseconds);
                drained++;
            }
            OnQueueFinished();
            LogI("[断点:依赖] 进图收口（" + reason + "）：同步等待 " + drained + " 个依赖完成，耗时 "
                + sw.ElapsedMilliseconds + " ms（解压在工作线程，主线程等待——此后场景加载全程依赖就位）");
        }

        // ---- v3.7.0 进图闸门：AssetBundleManager.LoadLevelAsync 前缀 ----
        // 异步依赖队列未完成时同步收口（DrainPendingSync），杜绝竞态：commonW 未就位
        // （场景外部引用无法解析）或运行时未装（双 stub 自愈补丁缺席）时进 web 关卡
        // → 永久卡加载。队列已完成时首行静态检查零开销放行；休眠态不装补丁（零介入
        // 铁律不变——官方图玩家无感知）。仅激活后安装（EnsureLevelEntryGate）。

        private static bool _levelGatePatched;

        private static void EnsureLevelEntryGate()
        {
            if (_levelGatePatched)
                return;
            _levelGatePatched = true; // 失败不重试：闸门缺席=行为同 v3.6.0 现状，不炸宿主
            try
            {
                var t = Type.GetType("AssetBundleManager, Assembly-CSharp");
                if (t == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try { t = asm.GetType("AssetBundleManager", false); }
                        catch { }
                        if (t != null)
                            break;
                    }
                }
                var m = t != null
                    ? t.GetMethod("LoadLevelAsync", BindingFlags.Public | BindingFlags.Static)
                    : null;
                var prefix = typeof(LevelRuntimeLoader).GetMethod("LevelEntryGatePrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (m == null || prefix == null)
                {
                    LogW("[断点:依赖] 进图闸门未装：未找到 AssetBundleManager.LoadLevelAsync"
                        + "（快速进图竞态无防护——如出现进图卡加载请把日志发给研发）");
                    return;
                }
                var harmony = new Harmony("oc2.loader.levelgate");
                harmony.Patch(m, new HarmonyMethod(prefix));
                LogI("[断点:依赖] 进图闸门已装: AssetBundleManager.LoadLevelAsync"
                    + "（异步队列未完成时进图同步等待收口，v3.7.0）");
            }
            catch (Exception ex)
            {
                LogW("[断点:依赖] 进图闸门安装失败（快速进图竞态无防护）: " + ex.Message);
            }
        }

        /// <summary>LoadLevelAsync 前缀（不拦截原方法）：队列已完成且无待项 → 零开销
        /// 放行；否则同步收口。参数名与宿主方法形参一致（Harmony 按名注入）。</summary>
        private static void LevelEntryGatePrefix(string assetBundleName)
        {
            try
            {
                if (_depLoadState == 2 && _pendingOps.Count == 0)
                    return; // 队列已完成：零开销放行
                if (WebManifest.Length == 0 && !_anyWebExportedSet)
                    return; // 休眠态（理论不可达——闸门仅在激活后安装）
                if (_depLoadState == 0)
                    LoadDependenciesAndRuntime(); // 尚未启动的边界情形：先启动再收口
                DrainPendingSync("LoadLevelAsync " + (assetBundleName ?? "?"));
            }
            catch (Exception ex)
            {
                LogW("[断点:依赖] 进图收口异常（放行原方法）: " + ex.Message);
            }
        }

        /// <summary>关卡集 *_custom_runtime 入队（v3.5.2 随依赖同一队列异步分帧加载，
        /// 消除同类主线程卡顿）。去重两重：已在 LoadedBundles（加载完成）或在待队
        /// 中（异步在途——双重扫描产生两次 ApplyScanResult 时，旧版同步加载即时
        /// 写 LoadedBundles 天然去重，异步后必须显式查待队，否则同 bundle 双载）。</summary>
        private static int EnqueueCustomRuntimeFiles(List<string> files)
        {
            var queued = 0;
            if (files != null)
            {
                for (int i = 0; i < files.Count; i++)
                {
                    var f = files[i];
                    if (LoadedBundles.Contains(f) || IsPendingLoadOp(f))
                        continue;
                    _pendingOps.Add(new LoadOp(LoadOpKind.Custom, Path.GetFileName(f), f));
                    queued++;
                }
            }
            if (queued > 0)
                EnsureQueueRunning();
            return queued;
        }

        /// <summary>路径是否已在待加载队列中（主线程专用；路径来源同一批
        /// Directory.GetFiles，大小写一致，按 ordinal 比较足够）。</summary>
        private static bool IsPendingLoadOp(string path)
        {
            for (int i = 0; i < _pendingOps.Count; i++)
            {
                if (_pendingOps[i].Path == path)
                    return true;
            }
            return false;
        }

        #endregion

        #region 资源加载跟踪（v3.5.6 诊断：同步阻塞入口逐调用计时）

        private static bool _tracerInstalled;

        /// <summary>安装资源加载跟踪（幂等；仅激活态调用）。postfix 纯日志不改行为，
        /// 安装失败只告警不影响游戏。动机：看门狗实测两处卡顿（进关卡选择 14.3s 冻结、
        /// 点击关卡→存档弹窗 4-5 次 300-700ms 抖动）期间零游戏日志——同步资源调用
        /// 不经过 AssetBundleManager 的带日志路径，必须逐调用计时指认。</summary>
        private static void EnsureAssetTracer()
        {
            if (_tracerInstalled || !TraceAssetsEnabled)
                return;
            _tracerInstalled = true;
            try
            {
                var harmony = new Harmony("oc2.loader.traceassets");
                int ok = 0, skip = 0;
                ok += PatchTracer(harmony, "LoadFromFile",
                    new[] { typeof(string), typeof(uint), typeof(ulong) }, "TracePostfixLoadFromFile", ref skip);
                ok += PatchTracer(harmony, "LoadAsset",
                    new[] { typeof(string), typeof(Type) }, "TracePostfixLoadAsset", ref skip);
                ok += PatchTracer(harmony, "LoadAllAssets",
                    new[] { typeof(Type) }, "TracePostfixLoadAllAssets", ref skip);
                LogI("[资产跟踪] 已装 " + ok + " 个同步资源调用计时补丁（LoadFromFile/LoadAsset/LoadAllAssets）"
                    + (skip > 0 ? "，反射缺失跳过 " + skip + " 个（Unity 版本签名变化？）" : "")
                    + "。根因定位后可在配置 [Diagnostics] TraceAssetLoads=false 关闭");
            }
            catch (Exception ex)
            {
                LogW("[资产跟踪] 安装异常（不影响游戏，仅缺这层日志）: " + ex);
            }
        }

        private static int PatchTracer(Harmony harmony, string methodName, Type[] argTypes, string postfixName, ref int skip)
        {
            try
            {
                var target = AccessTools.Method(typeof(AssetBundle), methodName, argTypes);
                if (target == null)
                {
                    skip++;
                    LogW("[资产跟踪] 未找到 AssetBundle." + methodName + "(" + argTypes.Length + " 参)，跳过");
                    return 0;
                }
                var postfix = new HarmonyMethod(AccessTools.Method(typeof(LevelRuntimeLoader), postfixName));
                var prefix = new HarmonyMethod(AccessTools.Method(typeof(LevelRuntimeLoader), "TraceStart"));
                harmony.Patch(target, prefix, postfix);
                return 1;
            }
            catch (Exception ex)
            {
                skip++;
                LogW("[资产跟踪] 单个补丁失败 " + methodName + ": " + ex.Message);
                return 0;
            }
        }

        // 计时（ThreadStatic：prefix→原方法→postfix 同线程成对执行；Stopwatch 复用
        // 不逐调用分配——高频轮询下分配本身也是开销）。
        [ThreadStatic] private static System.Diagnostics.Stopwatch _traceSw;

        /// <summary>慢调用日志阈值（ms）：低于它只计数，达到它才输出明细行。</summary>
        private const long TraceSlowMs = 100;

        private static long _traceFastCount;
        private static long _traceSlowCount;

        private static void TraceStart()
        {
            var sw = _traceSw;
            if (sw == null)
            {
                sw = new System.Diagnostics.Stopwatch();
                _traceSw = sw;
            }
            else
            {
                sw.Reset();
            }
            sw.Start();
        }

        private static long TraceMs()
        {
            var sw = _traceSw;
            return sw != null ? sw.ElapsedMilliseconds : -1L;
        }

        /// <summary>耗时归类：快调用原子计数后返回 false（调用方直接 return，
        /// 零字符串零日志——v3.5.6 逐条打日志在 26.6 万次/2 分钟的轮询下翻车）。</summary>
        private static bool TraceIsSlow()
        {
            if (TraceMs() < TraceSlowMs)
            {
                Interlocked.Increment(ref _traceFastCount);
                return false;
            }
            Interlocked.Increment(ref _traceSlowCount);
            return true;
        }

        private static void TracePostfixLoadFromFile(string path, AssetBundle __result)
        {
            if (!TraceIsSlow())
                return;
            LogI("[资产跟踪] LoadFromFile \"" + path + "\" → " + (__result != null ? "ok" : "null")
                + "，耗时 " + TraceMs() + " ms（同步阻塞主线程）");
        }

        private static void TracePostfixLoadAsset(AssetBundle __instance, string name, Type type, UnityEngine.Object __result)
        {
            if (!TraceIsSlow())
                return;
            LogI("[资产跟踪] " + (__instance != null ? __instance.name : "?") + ".LoadAsset(\"" + name
                + "\", " + (type != null ? type.Name : "?") + ") → " + (__result != null ? "ok" : "null")
                + "，耗时 " + TraceMs() + " ms（同步阻塞主线程）");
        }

        private static void TracePostfixLoadAllAssets(AssetBundle __instance, Type type, UnityEngine.Object[] __result)
        {
            if (!TraceIsSlow())
                return;
            LogI("[资产跟踪] " + (__instance != null ? __instance.name : "?") + ".LoadAllAssets("
                + (type != null ? type.Name : "?") + ") → " + (__result != null ? __result.Length.ToString() : "null")
                + " 个，耗时 " + TraceMs() + " ms（同步阻塞主线程）");
        }

        /// <summary>调用量 10s 汇总（Update 驱动）：让高频轮询可见而不刷屏。</summary>
        private static float _traceSummaryAt = -1f;

        private static void TraceSummaryTick()
        {
            float now = Time.realtimeSinceStartup;
            if (_traceSummaryAt < 0f)
            {
                _traceSummaryAt = now + 10f;
                return;
            }
            if (now < _traceSummaryAt)
                return;
            long total = Interlocked.Exchange(ref _traceFastCount, 0);
            long slow = Interlocked.Exchange(ref _traceSlowCount, 0);
            LogV("[资产跟踪] 近 10s 调用 " + total + " 次，其中慢(≥" + TraceSlowMs + "ms) "
                + slow + " 次（慢调用明细见各行；高频小调用=每帧轮询，属正常噪音）");
            _traceSummaryAt = now + 10f;
        }

        #endregion

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

        /// <summary>看门狗上一帧的 realtimeSinceStartup（-1=尚未采样）。实例字段——
        /// Update 是实例方法，无需考虑跨线程。</summary>
        private float _watchLast = -1f;

        private void Update()
        {
            // v3.5.3 帧卡顿看门狗（[Logging] FrameStallWatch，默认关）：纯被动计时。
            // 卡顿期间 Update 不被调用，恢复后的首次 Update 里 realtimeSinceStartup
            // 差值即为整个冻结时长——本行时间=卡顿结束、上一条日志时间≈卡顿开始。
            if (FrameWatchEnabled)
            {
                float now = Time.realtimeSinceStartup;
                if (_watchLast >= 0f)
                {
                    float delta = now - _watchLast;
                    if (delta > 0.1f)
                        LogW("[帧卡顿] Δ=" + (int)(delta * 1000f) + " ms（frame=" + Time.frameCount
                            + "，场景=" + SceneManager.GetActiveScene().name
                            + "）——卡顿区间≈[上一条日志时间, 本行时间]，查两行之间的日志定位责任方");
                }
                _watchLast = now;
            }
            if (TraceAssetsEnabled && _tracerInstalled)
                TraceSummaryTick();
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
            /// <summary>v3.7.0：任一已装集存在 stub_levels.txt 文件（即使 0 条目）。
            /// web 导出集的 commonW 素材（MixerBowl/烤盘/web 食材等）只由本加载器加载，
            /// 清单为空即休眠会让该类关卡场景外部引用无法解析 → 进图卡加载
            ///（2026-10-06 事故，LaTiao 实锤）。存在即激活（commonW + 运行时）。</summary>
            public bool AnyStubLevelsFile;
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

            // ---- v3.5.0 闸门：清单为空且无任何 web 导出集 = 本机没有 stub 关卡 ----
            // → 加载器整体休眠：不加载 commonW*/统一运行时/自定义 runtime，
            //   不挂 AssemblyResolve（此前未挂过）——对游戏零介入。
            // v3.7.0：存在 stub_levels.txt 文件（即使 0 条目）= 装有 web 导出集——
            // 其场景可能引用 commonW 素材（commonW 只由本加载器加载），空清单休眠
            // 会让这类关卡进图卡加载（2026-10-06 LaTiao 实锤）。此时仍完整激活
            //（commonW + 运行时；EntryPoint 逐关卡闸门对无特征关卡保持零 hook，
            // 双 stub 自愈补丁随 Install 常驻——官方图不经过 OC2DIYLevel 该路径，零调用）。
            WebManifest = result.ManifestEntries.ToArray();
            _anyWebExportedSet = result.AnyStubLevelsFile;
            if (WebManifest.Length == 0 && !_anyWebExportedSet)
            {
                LogI("未发现任何 web 导出的关卡集（levels/ 下无 stub_levels.txt）——加载器休眠，对游戏零介入"
                    + (result.SetCount > 0 ? "（已装关卡集 " + result.SetCount + " 个均非 web 导出或被版本门控跳过）" : ""));
                return;
            }

            // 激活：挂 AssemblyResolve（引用顺序兜底）+ 预载依赖/统一运行时。
            // 预载保持启动期（决策已确认；v3.5.2 起为异步分帧——drain 后数帧内完成，
            // 仍远早于任何 web 关卡场景加载）：程序集必须先于 web 关卡场景加载进
            // AppDomain，否则场景 MonoScript 解析竞态失败；纯数据/程序集驻留不 hook 任何函数。
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
            if (WebManifest.Length > 0)
            {
                LogI("stub 关卡清单命中 " + (WebManifest.Length - legacyCount) + " 关（分布于 "
                    + result.SetsWithManifest + " 个关卡集）"
                    + (legacyCount > 0 ? "，另含 " + legacyCount + " 个旧版导出集（兼容模式：探测+自愈）" : "")
                    + "——激活依赖/运行时加载");
            }
            else
            {
                LogI("已装 " + result.SetCount + " 个 web 导出关卡集但均未使用 CustomStub（清单为空）"
                    + "——仍激活依赖/运行（commonW 素材 + 双 stub 自愈；逐关卡玩法闸门保持零介入，v3.7.0）");
            }
            // v3.5.9：info bundle 预载已整体回退（Unity 2017.4 同文件二次
            // LoadFromFile 返回 null 报错，与 OC2DIYLevel 的加载互斥——见头部
            // 变更记录）。进关卡选择的 ~15s 同步加载卡顿归上游修。
            try
            {
                LoadDependenciesAndRuntime();
            }
            catch (Exception ex)
            {
                LogW("依赖/运行时加载异常: " + ex);
                LogP("自定义关卡运行组件启动失败，部分自定义玩法可能无法使用。请将 logs 目录发送给开发者。原因: " + ex.Message);
            }
            // v3.5.6 资源加载跟踪：仅激活态安装（休眠=零介入不变）。
            // v3.7.0 进图闸门：同样仅激活态安装（官方图玩家零介入）。
            EnsureAssetTracer();
            EnsureLevelEntryGate();

            // v3.5.2：*_custom_runtime 改随依赖队列异步分帧加载（原主线程同步
            // LoadFromFile，与依赖包同一卡顿类别——LZMA 整包解压）。
            var queued = EnqueueCustomRuntimeFiles(result.RuntimeFiles);
            if (result.Verbose || queued > 0)
                LogI("扫描汇总 [" + result.Root + "]: 关卡集 " + result.SetCount
                    + " 个，含自定义 runtime " + result.WithRuntime
                    + " 个，入队异步加载 " + queued + " 个");
            LogI("[断点:扫描] 关卡扫描处理完成，自定义 runtime 入队=" + queued + "（异步分帧）");
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
                    result.AnyStubLevelsFile = true; // v3.7.0：存在即 web 导出集（空清单也需 commonW）
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

        /// <summary>返回 true 表示本次真正完成了程序集加载（重复跳过/失败返回 false）。
        /// v3.5.3：Assembly.Load / GetTypes / EntryPoint.Install 均为不可后台化的
        /// 主线程成本，随行输出毫秒数（常开、一次性）——残余卡顿定位的第一手数据。</summary>
        private static bool LoadFromBytes(string displayName, byte[] raw)
        {
            try
            {
                var swLoad = System.Diagnostics.Stopwatch.StartNew();
                var asm = Assembly.Load(raw);
                swLoad.Stop();
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
                var swTypes = System.Diagnostics.Stopwatch.StartNew();
                try { typeCount = asm.GetTypes().Length; }
                catch { typeCount = -1; }
                swTypes.Stop();
                LogI("已加载关卡程序集: " + name + "（来自 " + displayName + "，类型数 "
                    + (typeCount >= 0 ? typeCount.ToString() : "未知") + "）"
                    + (crateType != null ? "，CustomStub.RandomCrate ✓" : "，⚠ 未找到 CustomStub.RandomCrate（旧版或空程序集）")
                    + "｜主线程耗时: Assembly.Load=" + swLoad.ElapsedMilliseconds
                    + " ms，GetTypes=" + swTypes.ElapsedMilliseconds + " ms");
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
                            var swInstall = System.Diagnostics.Stopwatch.StartNew();
                            var installed = install.Invoke(null, null);
                            swInstall.Stop();
                            // 返回 false 有两种情况：已有其它实例（正常），或 Install
                            // 内部抛异常被自己吞掉（此时上一行必有「安装异常」告警）。
                            // 别把后者也说成「已有实例」——2026-09-15 事故里这句话
                            // 差点把 GameApi 静态构造失败掩盖过去。
                            LogI("CustomStub.EntryPoint.Install: "
                                + ((installed is bool && (bool)installed)
                                    ? "已安装"
                                    : "未由本次调用安装（已有实例，或安装异常——见上一行告警）")
                                + "｜耗时 " + swInstall.ElapsedMilliseconds
                                + " ms（含 GameApi 反射初始化与自检，异步化后的主线程大头）");
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

        /// <summary>每次场景加载：先确保依赖/统一运行时已开始加载（v3.5.2 起为异步
        /// 队列幂等启动，在途/已完成均直接返回；休眠模式下为空操作）。
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
                if (WebManifest.Length == 0 && !_anyWebExportedSet)
                {
                    LogV("场景加载: " + scene.name + "（无 web 导出集=休眠模式，零介入）");
                    return;
                }
                LoadDependenciesAndRuntime();
                EnsureAssetTracer();
                EnsureLevelEntryGate();
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
