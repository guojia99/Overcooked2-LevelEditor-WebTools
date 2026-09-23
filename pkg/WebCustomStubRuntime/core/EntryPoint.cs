using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using LevelEditorStub;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomStub
{
    /// <summary>
    /// CustomStub 运行时入口（新增 stub 组件的统一安装器）。
    ///
    /// 两条安装路径：
    ///  - 游戏侧：OC2LevelRuntimeLoader 在 Assembly.Load(Stub_&lt;set&gt;) 后反射调用
    ///    CustomStub.EntryPoint.Install()；
    ///  - 编辑器 Play：[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] 自动安装
    ///    （Stub_&lt;set&gt; 程序集在编辑器内由 Unity 编译，进入 Play 时触发）。
    ///
    /// 幂等性：多个关卡集各有一份同名程序集（CustomStub.* 类重复定义），用
    /// 哨兵 GameObject（"CustomStub.Runtime"，DontDestroyOnLoad）保证全套
    /// ticker / Harmony 补丁 / 场景自愈只装一次。
    ///
    /// 职责：
    ///  1. Harmony 补丁（KillPlane 跳过 + 玩家脱离，HarmonyPatches；
    ///     触发区占用联机同步 + fallPad 清理，TriggerZoneOccupancySync）；
    ///  2. HotPot / PushableVoidFall / UtensilTiming（锅具时间）/ TerminalGuard
    ///     （未绑定终端防线）常驻 ticker；
    ///  3. sceneLoaded 场景自愈：按 SpecificPseudoPrefabTag 载体还原组件——
    ///     TimedSwitch| / PushablePot| / SwitchReenable| / WorldMapDressing|
    ///     / UtensilTiming| / TravelatorReverse| / TeleportalExitOnly|
    ///     / CameraOffset|（相机偏移注册+按需补丁；
    ///     RandomCrate| 由 loader 自愈，此处不重复）。
    /// </summary>
    public static class EntryPoint
    {
        /// <summary>版本金丝雀：真机日志确认 bundle 内 DLL 新鲜度看这一行。
        ///  v2：TimedSwitch 自愈竞态修复（AddComponent 先禁用→Parse→再启用，
        ///  startOn=false 不再被 OnEnable 默认相位吃掉）。
        ///  v3：防御性日志体系（StubLog 统一 [Stub:程序集] 前缀 + GameApi 反射自检
        ///  汇总 + 安装/自愈明细；RandomCrate v6）。
        ///  v4：EntryPoint ticker 降频 + HotPot region 缓存 + UtensilTiming 早停。
        ///  v5：性能优化——4 ticker 合并为 1；FindObjectsOfType 全场景扫描改为
        /// ~1s 缓存/探测（无对应内容的关卡零扫描）；锅具时间 Harmony 补丁按需
        /// 安装 + 前缀快速放行；TimedSwitch 逐帧压相降为相位边界+每 10 帧再压。
        ///  v6：空气候（RandomCrate v8）——HandlePickup 前缀按需安装 +
        /// HasAnyAirPending 快速放行；ResetSceneTickers 挂 RandomCrate.OnSceneChanged。
        ///  v7：相机出发点偏移（CameraAuthoredOffset）——接替 loader v1.7.0 全局补丁
        /// （拖全场景帧率，已移除）；CameraOffset|<x>,<z> tag 自愈时按需安装
        /// GetIdealLocation postfix + s_activeCount 首行快速放行。
        ///  v8：自愈跨程序集去重（2026-09-12 双装配事故：编辑器多关卡集 stub 程序集
        /// 共存，安装权归先到程序集，其 HealObject 的 GetComponent&lt;本程序集类型&gt;
        /// 判不到场景里其它程序集烘焙的同名组件 → 重复挂载 → 可移动火锅双锅叠装、
        /// 食材被两口锅分流；统一按 FullName 判定）+ PushablePot 装配竞态收口
        /// （LoadPotPrefab 耗时期间晚到者复查标记）。
        ///  v9：相机偏移编辑器 Play 也改由 CameraAuthoredOffset 承担（2026-09-12）——
        /// 删除 Assembly-CSharp-Patch/MultiplayerCamera.cs 整文件补丁镜像（违反
        /// 「新功能归 CustomStub」条例），EnsurePatches 不再跳过编辑器。
        ///  v10：触发区占用联机同步 + fallPad 清理（TriggerZoneOccupancySync，
        /// 2026-09-12）——接替 Assembly-CSharp-Patch ServerTriggerZone/ClientTriggerZone
        /// 源码覆盖补丁（同条例移除，文件已还原原版）：OnTriggerEnter/Exit 后缀广播
        /// TriggerZoneMessage（接通原版死通道，联机踏板外观）、基类 ApplyServerEvent
        /// 后缀写 m_occupied、UpdateSynchronising 前缀先正确清理 fallPad 占用列表
        /// （规避原版正序 Remove 跳元素/销毁 collider 抛异常）。
        ///  v11（2.0.0）：统一单程序集 WebCustomStubRuntime（母本搬出 Editor 目录、
        /// 全平台编译，取代每集 Stub_&lt;set&gt; 副本 + GUID 无损迁移）；版本号收敛到
        /// StubVersion.Value 单锚点；关卡级 tag 门控（HealScene 无 web stub tag 时
        /// 跳过安装 ticker/补丁——普通关卡零副作用）；退关运行时状态全量清零。
        ///  v12：ticker 三态门控（2026-09-14 性能审查）——v11 的 tag 门控是
        /// 「一次激活、永不复位」的静态锁，导致 ①同 session 进过一张 web 图后，
        /// 之后所有官方图/世界地图继续跑全套扫描，且 HotPot 会改写官方图灶台的
        /// m_gridIndex.y 与触发盒尺寸；②只放静态 web 火锅（无任何 stub tag）的关卡
        /// 永不激活 → 抬格层/扩触发盒/直驱烹饪全失效 → 静态火锅不加热。
        /// 现改为按场景重评估的 Dormant/Probing/Active 三态：
        ///  - 每次 sceneLoaded 进入 Probing（~30s 窗口，每 2s 只做 1 次单类型扫描）；
        ///  - 扫到 stub tag（HealScene）或探测命中「web 大锅 / 未绑定终端」→ Active；
        ///  - 窗口内无命中 → Dormant（ticker.enabled=false，官方图零开销）。
        /// 哨兵与已装 Harmony 补丁保持常驻（卸载补丁风险大于收益），只切 ticker 开关。
        ///  v13：传送门单向（TeleportalExitOnly，2026-09-15）——游戏侧传送门本就是
        /// 单向（Teleportal.m_exitPortal 唯一方向源），但编辑器无法表达「出口为空」
        /// （宿主/mod 的 LateSetup 对 null exitPortal 无判空即 NRE），出口门的 stub
        /// 保留回指占位、由 TeleportalExitOnly| tag 自愈挂组件在运行时清回 null。
        /// 不挂 ticker（组件自带低频协程），无该 tag 的图零开销。
        ///  v14：可移动火锅装配时机锚定 + 联机实体诊断（2026-09-15 联机 ID 错位事故）。
        /// 根因：实体 ID 是 EntitySerialisationRegistry 按【扫描命中顺序】发放的自增
        /// FIFO，而 LinkAllEntitiesToSynchronisationScripts 每 0.1s yield 一帧、
        /// 逐类型现场 GetComponentsInChildren——PushablePot 在扫描窗口期 Instantiate
        /// 大锅，主客机插入点不同 ⇒ 从该点起全部实体 ID 整体错位 ⇒ 客机完全不能
        /// 操作、双方厨师原地不动、生成物无模型（全程零报错）；单机侧则表现为
        /// 「其中一口锅开局就有汤且丢不进食材」（该锅漏挂同步组件）。
        /// 三件套修复：① EnsurePotAssemblyPatches 给 ScanEntities 打前缀，在发令
        /// 时刻同步装配全部大锅（两机唯一确定性锚点）；② PushablePot 协程加
        /// 「扫描期硬闸」（IsEntityScanActive 时绝不 Instantiate）；③ NetDiagnostics
        /// 在 StartSynchronisation 前缀输出【实体指纹 + 分段指纹 + 逐锅注册自检】，
        /// 主客机两行一比即可判定 ID 是否对齐。
        /// 顺带修好三处常年落空的反射（GameApi.Find 加命名空间候选+兜底扫描）：
        /// ClientSynchroniserBase/ServerSynchroniserBase/Serialisable 命名空间缺失
        /// （=触发区占用联机同步的收发两端补丁从未真正装上）、ContainerCapacityField
        /// 读错组件（汤面重摆从未生效）、IsLocallyControlled 反射了不存在的接口。
        ///  v15（2.4.0）：大炮防线（CannonGuard，2026-09-19 真机事故）——①空炮发射
        /// 拦截：ServerCannon.OnTrigger 前缀（m_loadedObject 为空或玩家已脱离
        /// AttachPoint 即跳过原方法；原版此路径会让 m_flying 永久 true + 客户端
        /// LaunchProjectile(null) NRE，大炮从此拒入）；②按钮占用门控：StubTicker
        /// 按「炮内是否有人」迁移时发 Disable/Reset（走原版 ServerTriggerDisableScript
        /// 网络通道，双端变灰/点亮），空炮不再常亮可按；③SwitchReenable 复查跳过
        /// 大炮按钮 + 烘焙侧不再给 CannonSwitch 烤自动复位（存量场景写回摘除）。
        /// 只认 SetupCannonStub 根的 web 大炮，官方 dlc08/09 图零影响。</summary>
        public const string Version = "v15(" + StubVersion.Value + ")";

        private const string SentinelName = "CustomStub.Runtime";
        private const string HarmonyId = "oc2.customstub";

        // Ticker 帧间隔（@60fps 调参集中在此）。
        // 相位偏移（v12）：原实现全部用 frame % N == 0，而 60 能整除 3/4/15/30/60
        // ——每 60 帧 7 个分支在同一帧集中爆发（每秒一次固定尖峰）。改为
        // (frame + 偏移) % N == 0，偏移取互不相同的常数把各分支摊到不同帧上。
        private const int HotPotCookIntervalFrames = 3;
        private const int HotPotCookPhase = 0;
        private const int HotPotMaintenanceIntervalFrames = 30;
        private const int HotPotMaintenancePhase = 7;
        private const int HotPotFlameIntervalFrames = 15;
        private const int HotPotFlamePhase = 2;
        private const int VoidFallIntervalFrames = 4;
        private const int VoidFallPhase = 1;
        private const int UtensilTimingIntervalFrames = 30;
        private const int UtensilTimingPhase = 19;
        private const int TerminalGuardDiscoverIntervalFrames = 60;
        private const int TerminalGuardDiscoverPhase = 29;
        private const int TerminalGuardRefreshIntervalFrames = 30;
        private const int TerminalGuardRefreshPhase = 11;
        private const int CannonGuardDiscoverIntervalFrames = 60;
        private const int CannonGuardDiscoverPhase = 41;
        private const int CannonGuardRefreshIntervalFrames = 30;
        private const int CannonGuardRefreshPhase = 23;

        private static bool s_installedThisAssembly;
        private static GameObject s_host;

        // ---- ticker 三态门控（v12） ----
        private const int StateDormant = 0;
        private const int StateProbing = 1;
        private const int StateActive = 2;

        /// <summary>无 tag 探测窗口（秒）：宿主/模组的伪 prefab 子物体在 sceneLoaded
        /// 之后才实例化，单次判断会漏，需要一段有界的探测期。</summary>
        private const float ProbeWindowSeconds = 30f;

        /// <summary>探测间隔（秒）：每次只做 1 种类型的 FindObjectsOfType，4 个槽位轮转。
        /// 开局前 ProbeFastCount 次用 ProbeFastSeconds 快速轮转——宿主的伪 prefab 子物体
        /// 在 sceneLoaded 之后才实例化，静态 web 火锅要尽快命中（否则开局若干秒不加热）；
        /// 快速期后放慢到 ProbeSlowSeconds 直到窗口结束。整个探测期的均摊开销远低于
        /// v11 的 7 次全场景扫描/秒。</summary>
        private const float ProbeFastSeconds = 0.5f;
        private const float ProbeSlowSeconds = 2f;
        private const int ProbeFastCount = 8;

        private static int s_state = StateDormant;
        private static float s_probeDeadline;
        private static float s_nextProbeAt;
        private static int s_probeCursor;
        private static StubTicker s_ticker;
        private static bool s_corePatchesInstalled;
        private static bool s_activationLogged;

        /// <summary>核心激活：安装常驻 Harmony 补丁（一次）并启用 ticker。幂等。</summary>
        private static void ActivateCore(string reason)
        {
            if (s_state == StateActive)
                return;
            s_state = StateActive;
            if (!s_corePatchesInstalled)
            {
                s_corePatchesInstalled = true;
                InstallHarmony();
            }
            // 联机诊断打点随核心一起装（冷方法，官方图因不激活而零影响）
            EnsureNetDiagPatches();
            SetTickerEnabled(true);
            if (!s_activationLogged)
            {
                s_activationLogged = true;
                StubLog.Log("[CustomStub " + Version + "] 核心激活（" + reason + "）"
                    + "：Harmony=" + (s_harmonyInstalled ? "OK" : "失败，依赖无前缀安全网")
                    + "，Stub ticker（HotPot/VoidFall/UtensilTiming/TerminalGuard 合一）已启用");
            }
            else
            {
                StubLog.Dbg("[CustomStub] 核心激活（" + reason + "）");
            }
        }

        /// <summary>进入探测态（每次场景加载的起点）。本场景若有 stub tag，
        /// 紧随其后的 HealScene 会立即提升为 Active。</summary>
        private static void BeginProbe()
        {
            s_state = StateProbing;
            s_probeDeadline = Time.unscaledTime + ProbeWindowSeconds;
            s_nextProbeAt = 0f;
            s_probeCursor = 0;
            SetTickerEnabled(true);
        }

        /// <summary>休眠：本场景确认不含任何 web stub 内容——ticker 停跑，
        /// 官方图/主菜单/世界地图零开销（哨兵与已装补丁保留）。</summary>
        private static void GoDormant()
        {
            if (s_state == StateDormant)
                return;
            s_state = StateDormant;
            SetTickerEnabled(false);
            StubLog.Dbg("[CustomStub] 本场景无 web stub 内容，ticker 已休眠");
        }

        private static void SetTickerEnabled(bool enabled)
        {
            if (s_ticker == null)
                return;
            if (s_ticker.enabled != enabled)
                s_ticker.enabled = enabled;
        }

    /// <summary>无 tag 通道探测（每次只做 1 次单类型全场景扫描，5 槽轮转）：
    ///  - 槽 0~2：web 大锅（ServerCookingHandler / WokEffects / ContentsCosmetic，
    ///    三者轮转——客机没有 Server* 同步器，只认单一类型会漏判）；
    ///  - 槽 3：未绑定可操控对象的 Terminal（写回降级残留，同样无 tag）；
    ///  - 槽 4：web 大炮（CannonGuard——SetupCannonStub 根，无专属 tag）。
    /// 窗口结束仍无命中 ⇒ Dormant。</summary>
        private static void TickProbe()
        {
            var now = Time.unscaledTime;
            if (now < s_nextProbeAt)
                return;
            var cursor = s_probeCursor++;
            s_nextProbeAt = now + (cursor < ProbeFastCount ? ProbeFastSeconds : ProbeSlowSeconds);
            // 5 槽轮转（v15）：0~2 web 大锅（三者轮转——客机没有 Server* 同步器）；
            // 3 未绑定可操控对象的 Terminal；4 web 大炮（CannonGuard，无 tag 通道）。
            var slot = cursor % 5;
            bool hit;
            if (slot < 3)
                hit = HotPot.ProbeHasLargePot(slot);
            else if (slot == 3)
                hit = TerminalGuard.ProbeHasUnboundTerminal();
            else
                hit = CannonGuard.ProbeHasCannon();
            if (hit)
            {
                ActivateCore(slot < 3 ? "探测到 web 大锅（无 tag 通道）"
                    : (slot == 3 ? "探测到未绑定可操控对象的终端" : "探测到 web 大炮"));
                return;
            }
            if (now >= s_probeDeadline)
                GoDormant();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            // 统一单程序集 WebCustomStubRuntime：编辑器 Play 与真机同一份程序集，
            // AutoInstall 一律执行（真机 loader 反射再调 Install 冗余但无害，哨兵幂等）。
            StubLog.Dbg("[CustomStub " + Version + "] AutoInstall（编辑器 Play/场景启动自动安装路径）");
            Install();
        }

        /// <summary>安装入口（loader 反射调用；幂等）。返回是否由本次调用完成安装。</summary>
        public static bool Install()
        {
            if (s_installedThisAssembly)
                return false;
            s_installedThisAssembly = true;

            try
            {
                // 跨程序集幂等：另一关卡集的程序集已装过全套（同名类重复定义，
                // 重复装 = 双 ticker + 双 Harmony 前缀）
                var existing = GameObject.Find(SentinelName);
                if (existing != null)
                {
                    StubLog.Log("[CustomStub " + Version + "] 已由其他程序集安装，跳过（sentinel 存在）");
                    return false;
                }

                var host = new GameObject(SentinelName);
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<CustomStubMarker>();
                s_host = host;

                // ticker 随哨兵一次性挂载，但初始禁用——由三态门控（BeginProbe /
                // ActivateCore / GoDormant）逐场景开关，避免反复 AddComponent/Destroy。
                s_ticker = AddTicker<StubTicker>(host, "Stub");
                SetTickerEnabled(false);

                SceneManager.sceneLoaded += OnSceneLoadedHeal;
                // 每场景重评估的起点：先进探测态，HealScene 扫到 tag 会立刻提升为 Active
                BeginProbe();
                HealScene(SceneManager.GetActiveScene());
                ResetSceneTickers();

                // 反射自检汇总：列出游戏 AppDomain 里未命中的反射目标（直接定位
                // 「反射了游戏侧不存在的类型」类事故）。
                GameApi.DumpReflectionSelfCheck();

                StubLog.Log("[CustomStub " + Version + "] EntryPoint 安装完成"
                    + "（哨兵已就位，ticker/Harmony 由三态门控按场景内容惰性启停）");
                return true;
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub " + Version + "] 安装异常: " + ex);
                return false;
            }
        }

        /// <summary>逐个挂 ticker 并报明细——某一类 stub 功能整体失效时，从这里
        /// 能看出是「没挂上」还是「挂上但没扫到目标」。</summary>
        private static T AddTicker<T>(GameObject host, string label) where T : Component
        {
            try
            {
                var c = host.AddComponent<T>();
                StubLog.Dbg("[CustomStub] ticker 已挂: " + label);
                return c;
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] ticker 挂载失败 " + label + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Harmony 是否装上（2026-09-04 事故：编辑器 Play 里 HarmonyLib.Harmony
        /// 静态构造抛异常 → KillPlane 前缀缺失 → 玩家挂锅落水被宿主原生重生卡死。
        /// 无前缀时的安全网见 PushableVoidFall.BeginPotFall/松手路径的主动脱离。）</summary>
        private static bool s_harmonyInstalled;

        private static void InstallHarmony()
        {
            s_harmonyInstalled = false;
            var target = GameApi.RespawnObjectAddedMethod;
            if (target == null)
            {
                StubLog.LogWarn("[CustomStub] ServerRespawnCollider.ObjectAdded 反射失败，KillPlane 补丁未装（无前缀安全网生效）");
            }
            else
            {
                try
                {
                    var harmony = new Harmony(HarmonyId);
                    var prefixMethod = HarmonyPatches.RespawnColliderObjectAddedPrefixMethod;
                    if (prefixMethod == null)
                    {
                        StubLog.LogWarn("[CustomStub] 前缀方法缺失，KillPlane 补丁未装（无前缀安全网生效）");
                    }
                    else
                    {
                        var prefix = new HarmonyMethod(prefixMethod);
                        harmony.Patch(target, prefix);
                        s_harmonyInstalled = true;
                        StubLog.Log("[CustomStub] KillPlane 补丁已装: " + target.DeclaringType.Name + "." + target.Name);
                    }
                }
                catch (Exception ex)
                {
                    // ex.Message 只有外层一句话（TypeInitializationException 不含真因），
                    // 必须打全量（含 InnerException）才能定位（HarmonyLib.Harmony 静态构造
                    // 在 Unity 2017.4 编辑器 Mono 上初始化失败，2026-09-04 待查真因）。
                    StubLog.LogWarn("[CustomStub] Harmony 安装失败（可移动火锅 KillPlane 行为走无前缀安全网）: " + ex);
                }
            }
            // 锅具时间补丁不再随安装无条件打：目标方法是宿主「每帧×每锅具」的热方法，
            // 无 UtensilTiming 配置的关卡连 detour 开销都不该有。改由
            // EnsureUtensilTimingPatches() 按需触发（场景自愈扫到 UtensilTiming| tag /
            // UtensilTiming.ApplyValues 遇到 burn/over>0）。

            InstallTriggerZoneSyncPatches();
        }

        /// <summary>触发区占用联机同步 + fallPad 清理（TriggerZoneOccupancySync）：
        /// 事件驱动（OnTriggerEnter/Exit）+ 基类消息分发后缀（GetType 首行门控）+
        /// UpdateSynchronising 前缀（字段门控），均非重热路径，随装不按需。
        /// 与 KillPlane/锅具时间/相机补丁相互独立——任一失败不影响其他组。</summary>
        private static bool s_triggerZoneSyncPatched;

        private static void InstallTriggerZoneSyncPatches()
        {
            if (s_triggerZoneSyncPatched)
                return;
            try
            {
                var harmony = new Harmony(HarmonyId + ".triggerzone");
                int ok = 0, skip = 0;
                ok += PatchPair(harmony, GameApi.ServerTriggerZoneOnTriggerEnterMethod,
                    null, TriggerZoneOccupancySync.SyncOccupiedPostfixMethod, ref skip);
                ok += PatchPair(harmony, GameApi.ServerTriggerZoneOnTriggerExitMethod,
                    null, TriggerZoneOccupancySync.SyncOccupiedPostfixMethod, ref skip);
                ok += PatchPair(harmony, GameApi.ServerTriggerZoneUpdateSynchronisingMethod,
                    TriggerZoneOccupancySync.FallPadPrunePrefixMethod, null, ref skip);
                ok += PatchPair(harmony, GameApi.ClientApplyServerEventMethod,
                    null, TriggerZoneOccupancySync.ApplyServerEventPostfixMethod, ref skip);
                s_triggerZoneSyncPatched = ok > 0;
                StubLog.Log("[CustomStub] 触发区占用同步补丁: 已装 " + ok + " 个"
                    + (skip > 0 ? "，反射缺失跳过 " + skip + " 个" : ""));
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] 触发区占用同步补丁安装失败（踏板联机外观/fallPad 清理退化为原版行为）: " + ex);
            }
        }

        /// <summary>锅具时间补丁（煮糊/过度混合 ≠ 2× 时的阈值接管）：与 KillPlane
        /// 相互独立——任一失败不影响另一组。按需安装（v5 起），幂等；安装后前缀
        /// 自身还有 UtensilTiming.HasAnyActive 静态字段快速放行双保险。</summary>
        private static bool s_utensilTimingPatched;

        internal static void EnsureUtensilTimingPatches()
        {
            if (s_utensilTimingPatched)
                return;
            try
            {
                var harmony = new Harmony(HarmonyId + ".utensiltiming");
                int ok = 0, skip = 0;
                ok += PatchPair(harmony, GameApi.CookingHandlerGetCookedStateMethod,
                    HarmonyPatches.CookingGetCookedStatePrefixMethod, null, ref skip);
                ok += PatchPair(harmony, GameApi.MixingHandlerGetMixedStateMethod,
                    HarmonyPatches.MixingGetMixedStatePrefixMethod, null, ref skip);
                ok += PatchPair(harmony, GameApi.HandlerIsBurningMethod,
                    HarmonyPatches.ServerCookingIsBurningPrefixMethod, null, ref skip);
                ok += PatchPair(harmony, GameApi.ClientCookingIsBurningMethod,
                    HarmonyPatches.ClientCookingIsBurningPrefixMethod, null, ref skip);
                ok += PatchPair(harmony, GameApi.ServerMixingIsOverMixedMethod,
                    HarmonyPatches.ServerMixingIsOverMixedPrefixMethod, null, ref skip);
                ok += PatchPair(harmony, GameApi.ClientMixingIsOverMixedMethod,
                    HarmonyPatches.ClientMixingIsOverMixedPrefixMethod, null, ref skip);
                ok += PatchPair(harmony, GameApi.ServerCookingSetProgressMethod,
                    null, HarmonyPatches.ServerCookingSetProgressPostfixMethod, ref skip);
                ok += PatchPair(harmony, GameApi.ServerMixingSetProgressMethod,
                    null, HarmonyPatches.ServerMixingSetProgressPostfixMethod, ref skip);
                ok += PatchPair(harmony, GameApi.ClientCookingApplyUpdateMethod,
                    null, HarmonyPatches.ClientCookingApplyUpdatePostfixMethod, ref skip);
                ok += PatchPair(harmony, GameApi.ClientMixingApplyUpdateMethod,
                    null, HarmonyPatches.ClientMixingApplyUpdatePostfixMethod, ref skip);
                s_utensilTimingPatched = ok > 0;
                StubLog.Log("[CustomStub] 锅具时间补丁: 已装 " + ok + " 个（按需安装）" + (skip > 0 ? "，反射缺失跳过 " + skip + " 个" : ""));
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] 锅具时间补丁安装失败（未配置煮糊/过混的锅具不受影响）: " + ex);
            }
        }

        private static int PatchPair(Harmony harmony, MethodInfo target, MethodInfo prefix, MethodInfo postfix, ref int skip)
        {
            if (target == null || (prefix == null && postfix == null))
            {
                skip++;
                return 0;
            }
            try
            {
                if (prefix != null)
                    harmony.Patch(target, new HarmonyMethod(prefix));
                if (postfix != null)
                    harmony.Patch(target, null, new HarmonyMethod(postfix));
                return 1;
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] 时间补丁单个绑定失败 " + target.DeclaringType.Name + "." + target.Name + ": " + ex.Message);
                skip++;
                return 0;
            }
        }

        /// <summary>可移动火锅「扫描前预装配」补丁（v14，2026-09-15 联机 ID 错位事故）：
        /// 给 MultiplayerController.ScanEntities 打前缀，在网络实体扫描发令时刻把
        /// 场景里所有大锅【同步】装配完；顺带给 StartSynchronisation 打前缀输出
        /// 实体指纹与逐锅注册自检。两者都是每关一次的冷方法，无热路径开销。
        ///
        /// 为什么必须有：实体 ID 是按扫描命中顺序发放的自增 FIFO，扫描窗口期内
        /// Instantiate 出的对象会插进序列的随机位置——主客机插入点不同就会让
        /// 【从该点起的所有实体 ID 整体错位】，表现为客机完全不能动、双方厨师原地
        /// 不动、生成物没模型，而日志里一条报错都没有（2026-09-15 实测）。
        /// 按需安装（扫到 PushablePot| tag 或 PushablePot.Start），幂等；
        /// 独立 Harmony id，与其它补丁组互不影响。</summary>
        private static bool s_potAssemblyPatched;
        private static bool s_potAssemblyPatchFailed;

        internal static void EnsurePotAssemblyPatches()
        {
            if (s_potAssemblyPatched || s_potAssemblyPatchFailed)
                return;
            try
            {
                var harmony = new Harmony(HarmonyId + ".potassembly");
                int ok = 0, skip = 0;
                ok += PatchPair(harmony, GameApi.ScanEntitiesMethod,
                    HarmonyPatches.MultiplayerScanEntitiesPrefixMethod, null, ref skip);
                s_potAssemblyPatched = ok > 0;
                s_potAssemblyPatchFailed = ok == 0;
                if (ok > 0)
                    StubLog.Log("[CustomStub] 实体扫描锚点补丁: 已装 " + ok + " 个"
                        + (skip > 0 ? "，反射缺失跳过 " + skip + " 个" : "")
                        + "（可移动火锅将在扫描前完成装配）");
                else
                    StubLog.LogWarn("[CustomStub] 实体扫描锚点补丁未装（反射缺失）——"
                        + "可移动火锅退化为协程装配 + 扫描期硬闸，联机请核对实体指纹");
            }
            catch (Exception ex)
            {
                s_potAssemblyPatchFailed = true;
                StubLog.LogWarn("[CustomStub] 实体扫描锚点补丁安装失败（退化为协程装配 + 扫描期硬闸）: " + ex);
            }
            // 诊断补丁与锚点补丁解耦：锚点装不上时更需要诊断日志
            EnsureNetDiagPatches();
        }

        /// <summary>联机诊断补丁（v14）：实体指纹 + 关卡网络时序打点。
        /// 三个目标都是每关一次的冷方法（StartSynchronisation / ClientKitchenLoader
        /// 的 ScannedEntities、StartEntities），无热路径开销。
        /// 随 ActivateCore 安装 = 只有 web stub 关卡才打点，官方图零影响。
        ///
        /// 为什么值得常开：联机 ID 错位这类事故【全程零报错】，此前唯一的线索只有
        /// 玩家口述。有了 `[Net] 实体扫描结果 ... 指纹=XXXXXXXX`，主客机两行一比
        /// 就能当场定性，不必再靠猜。</summary>
        private static bool s_netDiagPatched;
        private static bool s_netDiagPatchFailed;

        internal static void EnsureNetDiagPatches()
        {
            if (s_netDiagPatched || s_netDiagPatchFailed)
                return;
            try
            {
                var harmony = new Harmony(HarmonyId + ".netdiag");
                int ok = 0, skip = 0;
                ok += PatchPair(harmony, GameApi.StartSynchronisationMethod,
                    HarmonyPatches.MultiplayerStartSynchronisationPrefixMethod, null, ref skip);
                ok += PatchPair(harmony, GameApi.KitchenScannedEntitiesMethod,
                    HarmonyPatches.KitchenScannedEntitiesPrefixMethod, null, ref skip);
                ok += PatchPair(harmony, GameApi.KitchenStartEntitiesMethod,
                    HarmonyPatches.KitchenStartEntitiesPrefixMethod, null, ref skip);
                s_netDiagPatched = ok > 0;
                s_netDiagPatchFailed = ok == 0;
                if (ok > 0)
                    StubLog.Log("[CustomStub] 联机诊断补丁: 已装 " + ok + " 个"
                        + (skip > 0 ? "，反射缺失跳过 " + skip + " 个" : "")
                        + "（实体指纹 + 关卡网络时序打点）");
                else
                    StubLog.LogWarn("[CustomStub] 联机诊断补丁全部未装（反射缺失）——联机排障将缺少实体指纹");
            }
            catch (Exception ex)
            {
                s_netDiagPatchFailed = true;
                StubLog.LogWarn("[CustomStub] 联机诊断补丁安装失败（不影响玩法，仅少日志）: " + ex.Message);
            }
        }

        /// <summary>空气取出补丁（ServerPickupItemSpawner.HandlePickup 前缀，见
        /// HarmonyPatches.ServerPickupHandlePickupPrefix）：与 KillPlane/锅具时间补丁
        /// 相互独立。按需安装（首个空气候掷出时由 RandomCrate.SetAirPending 触发），
        /// 幂等；安装后前缀自身还有 RandomCrate.HasAnyAirPending 静态快速放行双保险
        /// ——无空气待取的原版箱子只多一次静态 bool 读取。</summary>
        private static bool s_airPickupPatched;
        private static bool s_airPickupPatchFailed;

        internal static void EnsureAirPickupPatches()
        {
            if (s_airPickupPatched || s_airPickupPatchFailed)
                return;
            try
            {
                var target = GameApi.ServerPickupHandlePickupMethod;
                var prefixMethod = HarmonyPatches.ServerPickupHandlePickupPrefixMethod;
                if (target == null || prefixMethod == null)
                {
                    s_airPickupPatchFailed = true;
                    StubLog.LogWarn("[CustomStub] ServerPickupItemSpawner.HandlePickup 反射/前缀缺失，"
                        + "空气取出补丁未装（空气箱将退化为取出兜底食材）");
                    return;
                }
                var harmony = new Harmony(HarmonyId + ".airpickup");
                harmony.Patch(target, new HarmonyMethod(prefixMethod));
                s_airPickupPatched = true;
                StubLog.Dbg("[CustomStub] 空气取出补丁已装（按需）: "
                    + target.DeclaringType.Name + "." + target.Name);
            }
            catch (Exception ex)
            {
                s_airPickupPatchFailed = true;
                StubLog.LogWarn("[CustomStub] 空气取出补丁安装失败（空气箱将退化为取出兜底食材）: " + ex);
            }
        }

        /// <summary>大炮空炮发射拦截（ServerCannon.OnTrigger 前缀，v15，见
        /// HarmonyPatches.ServerCannonOnTriggerPrefix / CannonGuard.AllowLaunch）：
        /// 目标是按钮按压/联动触发才走的冷方法，无热路径开销。按需安装
        /// （CannonGuard 发现 web 大炮时触发），幂等；失败仅损失拦截防线
        /// （按钮门控不受影响），独立 Harmony id 与其它补丁组互不影响。</summary>
        private static bool s_cannonPatched;
        private static bool s_cannonPatchFailed;

        internal static void EnsureCannonPatches()
        {
            if (s_cannonPatched || s_cannonPatchFailed)
                return;
            try
            {
                var target = GameApi.ServerCannonOnTriggerMethod;
                var prefixMethod = HarmonyPatches.ServerCannonOnTriggerPrefixMethod;
                if (target == null || prefixMethod == null)
                {
                    s_cannonPatchFailed = true;
                    StubLog.LogWarn("[CustomStub] ServerCannon.OnTrigger 反射/前缀缺失，"
                        + "空炮发射拦截未装（真机空炮发射仍会拒入软锁）");
                    return;
                }
                var harmony = new Harmony(HarmonyId + ".cannon");
                harmony.Patch(target, new HarmonyMethod(prefixMethod));
                s_cannonPatched = true;
                StubLog.Log("[CustomStub] 大炮空炮发射拦截补丁已装（按需）: "
                    + target.DeclaringType.Name + "." + target.Name);
            }
            catch (Exception ex)
            {
                s_cannonPatchFailed = true;
                StubLog.LogWarn("[CustomStub] 大炮空炮发射拦截补丁安装失败: " + ex);
            }
        }

        private static void OnSceneLoadedHeal(Scene scene, LoadSceneMode mode)
        {
            // v12：每次场景加载重评估门控——先回到探测态（ticker 暂以低频探测跑），
            // HealScene 扫到 stub tag 会立刻提升为 Active；都没有则 ~30s 后自动休眠。
            // 这条是「玩过 web 图后官方图仍被扫描/灶台被改写」的修复点。
            BeginProbe();
            s_tickWarned.Clear();
            HealScene(scene);
            ResetSceneTickers();
        }

        private static void ResetSceneTickers()
        {
            HotPot.OnSceneChanged();
            PushableVoidFall.OnSceneChanged();
            PushablePot.OnSceneChanged();
            UtensilTiming.OnSceneChanged();
            TerminalGuard.OnSceneChanged();
            CannonGuard.OnSceneChanged();
            RandomCrate.OnSceneChanged();
            RatHeist.OnSceneChanged();
            NetDiagnostics.OnSceneChanged();
        }

        /// <summary>场景自愈：按 tag 载体补挂缺失组件并还原参数（组件为权威，
        /// 已存在的不动）。</summary>
        internal static void HealScene(Scene scene)
        {
            if (!scene.isLoaded)
                return;
            try
            {
                // 相机偏移注册表随场景清场：清掉上一场注册（本场景若配置相机，下方
                // 扫描会重新注册）。不挂 ResetSceneTickers——该链在 HealScene 之后
                // 跑，会把刚注册的相机清掉。
                CameraAuthoredOffset.OnSceneChanged();
                var tags = UnityEngine.Object.FindObjectsOfType<SpecificPseudoPrefabTag>();
                var stubTags = 0;
                var tickerTags = 0;
                var healed = 0;
                var alreadyOk = 0;
                var sawUtensilTiming = false;
                var sawPushablePot = false;
                for (int i = 0; i < tags.Length; i++)
                {
                    var tag = tags[i];
                    if (tag == null || string.IsNullOrEmpty(tag.prefabTag))
                        continue;
                    if (!IsStubTag(tag.prefabTag))
                        continue; // 普通伪 prefab 的载体 tag 与 stub 无关，不计入汇总
                    stubTags++;
                    // 传送门单向是「自带低频协程、不用 ticker」的自治组件：不计入
                    // 激活票数，否则只放单向门的关卡会白跑全套 HotPot/VoidFall 扫描
                    // （v12 三态门控的同一条性能约定）。
                    if (!tag.prefabTag.StartsWith(TeleportalExitOnly.TagPrefix, StringComparison.Ordinal))
                        tickerTags++;
                    if (tag.prefabTag.StartsWith(UtensilTimingConfig.TagPrefix, StringComparison.Ordinal))
                        sawUtensilTiming = true;
                    if (tag.prefabTag.StartsWith(PushablePot.TagPrefix, StringComparison.Ordinal))
                        sawPushablePot = true;
                    try
                    {
                        var before = CountStubComponents(tag.gameObject);
                        HealObject(tag.gameObject, tag.prefabTag);
                        if (CountStubComponents(tag.gameObject) > before)
                            healed++;
                        else
                            alreadyOk++;
                    }
                    catch (Exception ex)
                    {
                        StubLog.LogWarn("[CustomStub] 自愈单对象失败 " + tag.gameObject.name + ": " + ex.Message);
                    }
                }
                // 锅具时间补丁按需安装：扫到 UtensilTiming| tag 才 patch 宿主热方法
                if (sawUtensilTiming)
                    EnsureUtensilTimingPatches();
                // 实体扫描锚点按需安装：本场景有可移动火锅才 patch（v14）。
                // 时序上安全——HealScene 跑在 sceneLoaded，而 ScanEntities 要等
                // ClientKitchenLoader 收到 GameState.ScanNetworkEntities（更晚）。
                if (sawPushablePot)
                    EnsurePotAssemblyPatches();
                // 关卡级门控：本场景确实用到需要 ticker 的 web stub 才激活核心 ticker/Harmony。
                // 注意：无 tag 的 web 内容（静态 web 火锅 / 写回降级残留的未绑定终端）
                // 由 TickProbe 的探测通道兜底激活，不在此处判定。
                if (tickerTags > 0)
                    ActivateCore("扫到 web stub tag × " + tickerTags);
                if (stubTags > 0)
                {
                    // 汇总：只在有 stub tag 的场景打（普通场景不打，避免刷屏）
                    StubLog.Log("[CustomStub] 场景自愈汇总 [" + scene.name + "]: stub tag " + stubTags
                        + " 个，补挂 " + healed + " 个，已就位/非本类 " + alreadyOk + " 个");
                }
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] 场景自愈失败 [" + scene.name + "]: " + ex.Message);
            }
        }

        /// <summary>本类负责自愈的 tag 前缀（统一单程序集后 RandomCrate| 亦收编于此，
        /// 不再由 loader 自愈）。</summary>
        private static bool IsStubTag(string prefabTag)
        {
            return prefabTag.StartsWith(RandomCrate.TagPrefix, StringComparison.Ordinal)
                || prefabTag.StartsWith(TimedCookingSwitch.TagPrefix, StringComparison.Ordinal)
                || prefabTag.StartsWith(PushablePot.TagPrefix, StringComparison.Ordinal)
                || prefabTag.StartsWith(SwitchReenable.TagPrefix, StringComparison.Ordinal)
                || prefabTag.StartsWith(WorldMapDressing.TagPrefix, StringComparison.Ordinal)
                || prefabTag.StartsWith(UtensilTimingConfig.TagPrefix, StringComparison.Ordinal)
                || prefabTag.StartsWith(CameraAuthoredOffset.TagPrefix, StringComparison.Ordinal)
                || prefabTag.StartsWith(TravelatorReverser.TagPrefix, StringComparison.Ordinal)
                || prefabTag.StartsWith(TeleportalExitOnly.TagPrefix, StringComparison.Ordinal)
                 || prefabTag.StartsWith(RatHeist.TagPrefix, StringComparison.Ordinal)
                 || prefabTag.StartsWith(ConveyorDirectionSync.TagPrefix, StringComparison.Ordinal);
        }

        /// <summary>统计对象上 CustomStub 命名空间组件数（自愈前后对比用）。</summary>
        private static int CountStubComponents(GameObject go)
        {
            var count = 0;
            foreach (var c in go.GetComponents<Component>())
            {
                if (c != null && c.GetType().Namespace == "CustomStub")
                    count++;
            }
            return count;
        }

        /// <summary>对象上是否已有任一程序集的同名 CustomStub 组件。编辑器里多个
        /// 关卡集 stub 程序集共存（Stub_a/Stub_b 同名类不同类型）：安装权归先到程序集，
        /// 其 HealObject 用 GetComponent&lt;本程序集类型&gt; 判不到场景里其它程序集
        /// 烘焙的同名组件 → 重复挂载（2026-09-12 可移动火锅双锅叠装事故）。
        /// 按 GetType().FullName 判定，跨程序集幂等。</summary>
        private static bool HasStubComponentNamed(GameObject go, string className)
        {
            if (go == null)
                return false;
            var full = "CustomStub." + className;
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c != null && c.GetType().FullName == full)
                    return true;
            }
            return false;
        }

        private static void HealObject(GameObject go, string prefabTag)
        {
            if (prefabTag.StartsWith(RandomCrate.TagPrefix, StringComparison.Ordinal))
            {
                if (HasStubComponentNamed(go, "RandomCrate"))
                    return;
                HealRandomCrate(go, prefabTag);
                return;
            }
            if (prefabTag.StartsWith(TimedCookingSwitch.TagPrefix, StringComparison.Ordinal))
            {
                if (HasStubComponentNamed(go, "TimedCookingSwitch"))
                    return;
                // 竞态修复（2026-09-07 真机日志实证）：AddComponent 在 active 物体上会
                // 同帧执行 OnEnable——TimedCookingSwitch.OnEnable 以【默认值】固化
                // m_phaseOn 并立即开火，之后 Parse 补写的 m_startOn 无法回滚相位
                // （现象：startOn=false 配置开局仍为开启）。统一先禁用、写完配置再
                // 启用，让 OnEnable 以最终配置启动。
                var sw = go.AddComponent<TimedCookingSwitch>();
                sw.enabled = false;
                ParseTimedSwitch(prefabTag.Substring(TimedCookingSwitch.TagPrefix.Length), sw);
                sw.enabled = true;
                StubLog.Dbg("[CustomStub] 自愈 TimedSwitch: " + go.name);
            }
            else if (prefabTag.StartsWith(PushablePot.TagPrefix, StringComparison.Ordinal))
            {
                if (HasStubComponentNamed(go, "PushablePot"))
                    return;
                var pot = go.AddComponent<PushablePot>();
                ParsePushablePot(prefabTag.Substring(PushablePot.TagPrefix.Length), pot);
                StubLog.Dbg("[CustomStub] 自愈 PushablePot: " + go.name);
            }
            else if (prefabTag.StartsWith(SwitchReenable.TagPrefix, StringComparison.Ordinal))
            {
                if (HasStubComponentNamed(go, "SwitchReenable"))
                    return;
                var re = go.AddComponent<SwitchReenable>();
                ParseSwitchReenable(prefabTag.Substring(SwitchReenable.TagPrefix.Length), re);
                StubLog.Dbg("[CustomStub] 自愈 SwitchReenable: " + go.name);
            }
            else if (prefabTag.StartsWith(WorldMapDressing.TagPrefix, StringComparison.Ordinal))
            {
                if (HasStubComponentNamed(go, "WorldMapDressing"))
                    return;
                go.AddComponent<WorldMapDressing>();
                StubLog.Dbg("[CustomStub] 自愈 WorldMapDressing: " + go.name);
            }
            else if (prefabTag.StartsWith(UtensilTimingConfig.TagPrefix, StringComparison.Ordinal))
            {
                if (HasStubComponentNamed(go, "UtensilTimingConfig"))
                    return;
                var cfg = go.AddComponent<UtensilTimingConfig>();
                ParseUtensilTiming(prefabTag.Substring(UtensilTimingConfig.TagPrefix.Length), cfg);
                StubLog.Dbg("[CustomStub] 自愈 UtensilTimingConfig: " + go.name);
            }
            else if (prefabTag.StartsWith(TravelatorReverser.TagPrefix, StringComparison.Ordinal))
            {
                if (HasStubComponentNamed(go, "TravelatorReverser"))
                    return;
                // AddComponent 竞态修复同 TimedSwitch：OnEnable 以默认值固化初始相位，
                // 统一先禁用、Parse 写完配置再启用。
                var rev = go.AddComponent<TravelatorReverser>();
                rev.enabled = false;
                ParseTravelatorReverse(prefabTag.Substring(TravelatorReverser.TagPrefix.Length), rev);
                rev.enabled = true;
                StubLog.Dbg("[CustomStub] 自愈 TravelatorReverse: " + go.name);
            }
            else if (prefabTag.StartsWith(TeleportalExitOnly.TagPrefix, StringComparison.Ordinal))
            {
                if (HasStubComponentNamed(go, "TeleportalExitOnly"))
                    return;
                // AddComponent 竞态同 TimedSwitch/TravelatorReverse：OnEnable 会以默认值
                // 立刻启动压制协程，先禁用、Parse 写完配置再启用（m_enabled=0 的
                // 「配置保留但不生效」才不会被默认 true 吃掉）。
                var exitOnly = go.AddComponent<TeleportalExitOnly>();
                exitOnly.enabled = false;
                ParseTeleportalExitOnly(prefabTag.Substring(TeleportalExitOnly.TagPrefix.Length), exitOnly);
                exitOnly.enabled = true;
                StubLog.Dbg("[CustomStub] 自愈 TeleportalExitOnly: " + go.name);
            }
            else if (prefabTag.StartsWith(RatHeist.TagPrefix, StringComparison.Ordinal))
            {
                if (HasStubComponentNamed(go, "RatHeist"))
                    return;
                // 老鼠状态机在 Start 协程里自行等待同步，OnEnable 无副作用，无需
                // 先禁用再启用。
                var rat = go.AddComponent<RatHeist>();
                ParseRatHeist(prefabTag.Substring(RatHeist.TagPrefix.Length), rat);
                StubLog.Dbg("[CustomStub] 自愈 RatHeist: " + go.name);
            }
            else if (prefabTag.StartsWith(ConveyorDirectionSync.TagPrefix, StringComparison.Ordinal))
            {
                if (HasStubComponentNamed(go, "ConveyorDirectionSync"))
                    return;
                var sync = go.AddComponent<ConveyorDirectionSync>();
                sync.enabled = false;
                sync.m_enabled = true;
                sync.enabled = true;
                StubLog.Dbg("[CustomStub] 自愈 ConveyorDirectionSync: " + go.name);
            }
            else if (prefabTag.StartsWith(CameraAuthoredOffset.TagPrefix, StringComparison.Ordinal))
            {
                // CameraOffset|<x>,<z>（invariant 浮点 = 相机根节点摆放位世界 XZ，
                // tag payload 即权威通道）。不挂组件：注册进静态表 + 按需装补丁即可。
                var payload = prefabTag.Substring(CameraAuthoredOffset.TagPrefix.Length);
                var parts = payload.Split(',');
                if (parts.Length < 2)
                {
                    StubLog.LogWarn("[CustomStub] CameraOffset tag 格式非法（应为 CameraOffset|<x>,<z>）: "
                        + go.name);
                    return;
                }
                CameraAuthoredOffset.RegisterFromTag(go,
                    ParseFloat(parts[0], 0f), ParseFloat(parts[1], 0f));
            }
        }

        /// <summary>RandomCrate 自愈（统一单程序集后由 EntryPoint 承担，取代 loader
        /// 反射版）：组件缺失时挂载 + 从同物体 PseudoPrefabSOArray 回填候选 + 从 tag
        /// 解析权重（v3: RandomCrate|&lt;iconGuid&gt;|&lt;w1,w2,...&gt;|air=&lt;w&gt;；兼容 v2/旧两段式）。
        /// 问号贴图无法从 guid 反查（游戏侧无 AssetDatabase），留空→回落原版图标。</summary>
        private static void HealRandomCrate(GameObject go, string prefabTag)
        {
            RandomCrate crate;
            try
            {
                crate = go.AddComponent<RandomCrate>();
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] 自愈 RandomCrate AddComponent 失败 " + go.name + ": " + ex.Message);
                return;
            }

            // 候选：同物体 PseudoPrefabSOArray.pseudoPrefabSOs 直接回填
            var carrier = go.GetComponent<PseudoPrefabSOArray>();
            if (carrier != null && carrier.pseudoPrefabSOs != null)
                crate.m_itemSOs = carrier.pseudoPrefabSOs;

            // 权重 + 空气：从 tag 解析
            var payload = prefabTag.Substring(RandomCrate.TagPrefix.Length);
            var csv = payload;
            var airWeight = 0f;
            if (payload.StartsWith("|", StringComparison.Ordinal))
            {
                var seg = payload.Substring(1).Split('|');
                csv = seg.Length > 0 ? seg[0] : "";
            }
            else if (payload.IndexOf('|') >= 0)
            {
                var seg = payload.Split('|');
                csv = seg.Length > 1 ? seg[1] : "";
            }
            // air=<w> 段（任意位置）
            var allSegs = payload.Split('|');
            for (int i = 0; i < allSegs.Length; i++)
            {
                var s = allSegs[i].Trim();
                if (s.StartsWith("air=", StringComparison.Ordinal))
                    airWeight = ParseFloat(s.Substring(4), 0f);
            }
            crate.m_airWeight = airWeight >= 1f ? airWeight : 0f;

            var parts = csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var count = crate.m_itemSOs != null ? crate.m_itemSOs.Length : parts.Length;
            var weights = new float[count];
            for (int i = 0; i < count; i++)
            {
                weights[i] = 5f;
                if (i < parts.Length)
                {
                    float w;
                    if (float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out w) && w >= 1f)
                        weights[i] = w;
                }
            }
            crate.m_weights = weights;
            crate.m_questionMarkTexture = null;
            StubLog.Dbg("[CustomStub] 自愈 RandomCrate: " + go.name
                + "（候选 " + count + (crate.m_airWeight >= 1f ? "，空气=" + crate.m_airWeight : "") + "）");
        }

        /// <summary>UtensilTiming|&lt;cook&gt;,&lt;burn&gt;,&lt;mix&gt;,&lt;over&gt;（invariant 浮点，
        /// 0 = 未配置）。组件为权威，已存在的不动。</summary>
        private static void ParseUtensilTiming(string payload, UtensilTimingConfig cfg)
        {
            if (string.IsNullOrEmpty(payload))
                return;
            var parts = payload.Split(',');
            if (parts.Length < 4)
                return;
            cfg.m_cookTime = ParseFloat(parts[0], 0f);
            cfg.m_burnTime = ParseFloat(parts[1], 0f);
            cfg.m_mixTime = ParseFloat(parts[2], 0f);
            cfg.m_overMixTime = ParseFloat(parts[3], 0f);
        }

        /// <summary>TimedSwitch|&lt;1|0&gt;,&lt;on&gt;,&lt;off&gt;,&lt;1|0&gt;</summary>
        private static void ParseTimedSwitch(string payload, TimedCookingSwitch sw)
        {
            if (string.IsNullOrEmpty(payload))
                return;
            var parts = payload.Split(',');
            if (parts.Length < 4)
                return;
            sw.m_enabled = parts[0].Trim() == "1";
            sw.m_onSeconds = ParseFloat(parts[1], 30f);
            sw.m_offSeconds = ParseFloat(parts[2], 30f);
            sw.m_startOn = parts[3].Trim() == "1";
        }

        /// <summary>TravelatorReverse|&lt;1|0&gt;,&lt;fwd&gt;,&lt;back&gt;,&lt;1|0&gt;[,&lt;angle&gt;]
        /// （angle 缺省 180，兼容 4 段旧载体）</summary>
        private static void ParseTravelatorReverse(string payload, TravelatorReverser rev)
        {
            if (string.IsNullOrEmpty(payload))
                return;
            var parts = payload.Split(',');
            if (parts.Length < 4)
                return;
            rev.m_enabled = parts[0].Trim() == "1";
            rev.m_forwardSeconds = ParseFloat(parts[1], 10f);
            rev.m_backwardSeconds = ParseFloat(parts[2], 10f);
            rev.m_startReversed = parts[3].Trim() == "1";
            if (parts.Length >= 5)
                rev.m_turnAngle = ParseFloat(parts[4], 180f);
        }

        /// <summary>PushablePot|&lt;bundle&gt;:&lt;path&gt;;&lt;bundle&gt;:&lt;path&gt;;...
        /// 第一项=大锅 prefab，其余=食材 OrderDefinitionNode。</summary>
        private static void ParsePushablePot(string payload, PushablePot pot)
        {
            if (string.IsNullOrEmpty(payload))
                return;
            var entries = payload.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var bundles = new System.Collections.Generic.List<string>();
            var paths = new System.Collections.Generic.List<string>();
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i].Trim();
                var sep = entry.IndexOf(':');
                if (sep <= 0 || sep >= entry.Length - 1)
                    continue;
                var bundle = entry.Substring(0, sep);
                var path = entry.Substring(sep + 1);
                if (i == 0)
                {
                    pot.m_potBundle = bundle;
                    pot.m_potPath = path;
                }
                else
                {
                    bundles.Add(bundle);
                    paths.Add(path);
                }
            }
            pot.m_extraIngredientBundles = bundles.ToArray();
            pot.m_extraIngredientPaths = paths.ToArray();
        }

        /// <summary>SwitchReenable|&lt;delay&gt;（可空）</summary>
        private static void ParseSwitchReenable(string payload, SwitchReenable re)
        {
            if (string.IsNullOrEmpty(payload))
                return;
            re.m_resetDelay = ParseFloat(payload, 0.35f);
        }

        /// <summary>TeleportalExitOnly|&lt;1|0&gt;（1 = 仅作为出口，0 = 配置保留但不生效）。
        /// 空 payload 按启用处理（向前兼容只写前缀的载体）。</summary>
        private static void ParseTeleportalExitOnly(string payload, TeleportalExitOnly exitOnly)
        {
            if (string.IsNullOrEmpty(payload))
                return;
            exitOnly.m_enabled = payload.Trim() != "0";
        }

        /// <summary>RatHeist|&lt;interval&gt;,&lt;radius&gt;,&lt;speed&gt;,&lt;skin&gt;,&lt;raw 1|0&gt;,&lt;plated 1|0&gt;,&lt;utensil 1|0&gt;
        /// （interval 秒 / radius 格 0=全图 / speed 倍率 / skin retro|dlc08；
        /// 段缺省回落组件默认值）。</summary>
        private static void ParseRatHeist(string payload, RatHeist rat)
        {
            if (string.IsNullOrEmpty(payload))
                return;
            var parts = payload.Split(',');
            if (parts.Length < 1)
                return;
            if (parts.Length >= 1)
                rat.m_interval = Mathf.Max(2f, ParseFloat(parts[0], rat.m_interval));
            if (parts.Length >= 2)
                rat.m_radius = Mathf.Max(0f, ParseFloat(parts[1], rat.m_radius));
            if (parts.Length >= 3)
                rat.m_speed = Mathf.Max(0.1f, ParseFloat(parts[2], rat.m_speed));
            if (parts.Length >= 4 && !string.IsNullOrEmpty(parts[3].Trim()))
                rat.m_skin = parts[3].Trim();
            if (parts.Length >= 5)
                rat.m_stealRaw = parts[4].Trim() != "0";
            if (parts.Length >= 6)
                rat.m_stealPlated = parts[5].Trim() == "1";
            if (parts.Length >= 7)
                rat.m_stealUtensil = parts[6].Trim() == "1";
        }

        private static float ParseFloat(string s, float fallback)
        {
            float v;
            if (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;
            return fallback;
        }

        // ============ 哨兵与 ticker 宿主 ============

        /// <summary>安装标记（供排查：场景里的常驻对象是什么）。</summary>
        public class CustomStubMarker : MonoBehaviour
        {
        }

        /// <summary>统一 ticker（v5 起 4 合 1：减少 Unity Update 回调次数，空场景每帧
        /// 只剩 1 次 Update + 帧取模判断）。各子系统内部另有对象缓存/按需探测，
        /// 无对应内容的关卡实际开销≈0：
        ///  - HotPot Cook 每 3 帧累积 delta；火焰每 15 帧；维护每 30 帧；
        ///  - VoidFall 每 4 帧；
        ///  - UtensilTiming 每 30 帧（全部应用后早停）；
        ///  - TerminalGuard 发现每 60 帧，已 guard 刷新每 30 帧。
        /// v12：① 三态门控——Probing 态只跑低频探测，Dormant 态组件直接 disable
        /// （本 Update 根本不会被调用）；② 各分支加互异相位偏移，消除 v11 里
        /// 「每 60 帧 7 个分支同帧爆发」的每秒固定尖峰。</summary>
        private class StubTicker : MonoBehaviour
        {
            private float m_cookAccum;

            /// <summary>休眠→再启用时清空累积（跨场景/跨门控状态不得把上一段的
            /// deltaTime 补给新场景的锅）。</summary>
            private void OnEnable()
            {
                m_cookAccum = 0f;
            }

            private void Update()
            {
                try
                {
                    if (s_state != StateActive)
                    {
                        if (s_state == StateProbing)
                            TickProbe();
                        return;
                    }
                    var frame = Time.frameCount;
                    m_cookAccum += Time.deltaTime;
                    if ((frame + HotPotCookPhase) % HotPotCookIntervalFrames == 0)
                    {
                        if (m_cookAccum > 0f)
                        {
                            HotPot.CookPotsOverBurner(m_cookAccum);
                            m_cookAccum = 0f;
                        }
                    }
                    if ((frame + HotPotFlamePhase) % HotPotFlameIntervalFrames == 0)
                        HotPot.TickWokFlame();
                    if ((frame + HotPotMaintenancePhase) % HotPotMaintenanceIntervalFrames == 0)
                        HotPot.TickMaintenance();
                    if ((frame + VoidFallPhase) % VoidFallIntervalFrames == 0)
                        PushableVoidFall.Tick();
                    if ((frame + UtensilTimingPhase) % UtensilTimingIntervalFrames == 0
                        && !UtensilTiming.IsScanComplete())
                        UtensilTiming.Tick();
                    if ((frame + TerminalGuardDiscoverPhase) % TerminalGuardDiscoverIntervalFrames == 0)
                        TerminalGuard.TickDiscover();
                    if ((frame + TerminalGuardRefreshPhase) % TerminalGuardRefreshIntervalFrames == 0)
                        TerminalGuard.TickRefreshGuarded();
                    if ((frame + CannonGuardDiscoverPhase) % CannonGuardDiscoverIntervalFrames == 0)
                        CannonGuard.TickDiscover();
                    if ((frame + CannonGuardRefreshPhase) % CannonGuardRefreshIntervalFrames == 0)
                        CannonGuard.TickRefresh();
                }
                catch (Exception ex)
                {
                    WarnTickOnce(ex);
                }
            }
        }

        // ---- tick 异常去重（v12）：原实现在 catch 里无条件 LogWarn，持续性异常
        // = 每帧一条日志（60 行/秒 × 反射 Invoke × 磁盘写），本身就会把帧率打垮。
        // 现按「异常类型 + 消息」去重，最多报 TickWarnLimit 条不同的，之后静默。
        private const int TickWarnLimit = 3;
        private static readonly System.Collections.Generic.HashSet<string> s_tickWarned =
            new System.Collections.Generic.HashSet<string>();

        private static void WarnTickOnce(Exception ex)
        {
            if (s_tickWarned.Count >= TickWarnLimit)
                return;
            var key = ex.GetType().Name + "|" + ex.Message;
            if (!s_tickWarned.Add(key))
                return;
            StubLog.LogWarn("[CustomStub.StubTicker] tick skipped: " + ex.Message
                + (s_tickWarned.Count >= TickWarnLimit ? "（已达上报上限，同类异常后续静默）" : ""));
        }
    }
}
