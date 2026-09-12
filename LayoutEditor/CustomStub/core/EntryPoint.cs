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
    ///     / UtensilTiming| / CameraOffset|（相机偏移注册+按需补丁；
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
        /// （规避原版正序 Remove 跳元素/销毁 collider 抛异常）。</summary>
        public const string Version = "v10";

        private const string SentinelName = "CustomStub.Runtime";
        private const string HarmonyId = "oc2.customstub";

        // Ticker 帧间隔（@60fps 调参集中在此）
        private const int HotPotCookIntervalFrames = 3;
        private const int HotPotMaintenanceIntervalFrames = 30;
        private const int HotPotFlameIntervalFrames = 15;
        private const int VoidFallIntervalFrames = 4;
        private const int UtensilTimingIntervalFrames = 30;
        private const int TerminalGuardDiscoverIntervalFrames = 60;
        private const int TerminalGuardRefreshIntervalFrames = 30;

        private static bool s_installedThisAssembly;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            // 母本程序集（CustomStub，Editor 平台）只是模板/语法校验载体，
            // 运行实体是各关卡集的 Stub_<set>（全平台编译）——编辑器 Play 只装后者。
            if (typeof(EntryPoint).Assembly.GetName().Name == "CustomStub")
                return;
            StubLog.Log("[CustomStub " + Version + "] AutoInstall（编辑器 Play/场景启动自动安装路径）");
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

                InstallHarmony();
                AddTicker<StubTicker>(host, "Stub");
                SceneManager.sceneLoaded += OnSceneLoadedHeal;
                HealScene(SceneManager.GetActiveScene());
                ResetSceneTickers();

                // 反射自检汇总：列出游戏 AppDomain 里未命中的反射目标（直接定位
                // 「反射了游戏侧不存在的类型」类事故）。
                GameApi.DumpReflectionSelfCheck();

                StubLog.Log("[CustomStub " + Version + "] EntryPoint 安装完成"
                    + "（Harmony=" + (s_harmonyInstalled ? "OK" : "失败，已依赖无前缀安全网")
                    + "，Stub ticker（HotPot/VoidFall/UtensilTiming/TerminalGuard 合一）+ 场景自愈）");
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
        private static void AddTicker<T>(GameObject host, string label) where T : Component
        {
            try
            {
                host.AddComponent<T>();
                StubLog.Log("[CustomStub] ticker 已挂: " + label);
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] ticker 挂载失败 " + label + ": " + ex.Message);
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
                StubLog.Log("[CustomStub] 空气取出补丁已装（按需）: "
                    + target.DeclaringType.Name + "." + target.Name);
            }
            catch (Exception ex)
            {
                s_airPickupPatchFailed = true;
                StubLog.LogWarn("[CustomStub] 空气取出补丁安装失败（空气箱将退化为取出兜底食材）: " + ex);
            }
        }

        private static void OnSceneLoadedHeal(Scene scene, LoadSceneMode mode)
        {
            HealScene(scene);
            ResetSceneTickers();
        }

        private static void ResetSceneTickers()
        {
            HotPot.OnSceneChanged();
            PushableVoidFall.OnSceneChanged();
            UtensilTiming.OnSceneChanged();
            TerminalGuard.OnSceneChanged();
            RandomCrate.OnSceneChanged();
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
                var healed = 0;
                var alreadyOk = 0;
                var sawUtensilTiming = false;
                for (int i = 0; i < tags.Length; i++)
                {
                    var tag = tags[i];
                    if (tag == null || string.IsNullOrEmpty(tag.prefabTag))
                        continue;
                    if (!IsStubTag(tag.prefabTag))
                        continue; // 普通伪 prefab 的载体 tag 与 stub 无关，不计入汇总
                    stubTags++;
                    if (tag.prefabTag.StartsWith(UtensilTimingConfig.TagPrefix, StringComparison.Ordinal))
                        sawUtensilTiming = true;
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
                // 汇总：只在有 stub tag 的场景打（普通场景不打，避免刷屏）
                if (stubTags > 0)
                    StubLog.Log("[CustomStub] 场景自愈汇总 [" + scene.name + "]: stub tag " + stubTags
                        + " 个，补挂 " + healed + " 个，已就位/非本类 " + alreadyOk + " 个");
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] 场景自愈失败 [" + scene.name + "]: " + ex.Message);
            }
        }

        /// <summary>本类负责自愈的 tag 前缀（RandomCrate| 由 loader 自愈，不在此列）。</summary>
        private static bool IsStubTag(string prefabTag)
        {
            return prefabTag.StartsWith(TimedCookingSwitch.TagPrefix, StringComparison.Ordinal)
                || prefabTag.StartsWith(PushablePot.TagPrefix, StringComparison.Ordinal)
                || prefabTag.StartsWith(SwitchReenable.TagPrefix, StringComparison.Ordinal)
                || prefabTag.StartsWith(WorldMapDressing.TagPrefix, StringComparison.Ordinal)
                || prefabTag.StartsWith(UtensilTimingConfig.TagPrefix, StringComparison.Ordinal)
                || prefabTag.StartsWith(CameraAuthoredOffset.TagPrefix, StringComparison.Ordinal);
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
                StubLog.Log("[CustomStub] 自愈 TimedSwitch: " + go.name);
            }
            else if (prefabTag.StartsWith(PushablePot.TagPrefix, StringComparison.Ordinal))
            {
                if (HasStubComponentNamed(go, "PushablePot"))
                    return;
                var pot = go.AddComponent<PushablePot>();
                ParsePushablePot(prefabTag.Substring(PushablePot.TagPrefix.Length), pot);
                StubLog.Log("[CustomStub] 自愈 PushablePot: " + go.name);
            }
            else if (prefabTag.StartsWith(SwitchReenable.TagPrefix, StringComparison.Ordinal))
            {
                if (HasStubComponentNamed(go, "SwitchReenable"))
                    return;
                var re = go.AddComponent<SwitchReenable>();
                ParseSwitchReenable(prefabTag.Substring(SwitchReenable.TagPrefix.Length), re);
                StubLog.Log("[CustomStub] 自愈 SwitchReenable: " + go.name);
            }
            else if (prefabTag.StartsWith(WorldMapDressing.TagPrefix, StringComparison.Ordinal))
            {
                if (HasStubComponentNamed(go, "WorldMapDressing"))
                    return;
                go.AddComponent<WorldMapDressing>();
                StubLog.Log("[CustomStub] 自愈 WorldMapDressing: " + go.name);
            }
            else if (prefabTag.StartsWith(UtensilTimingConfig.TagPrefix, StringComparison.Ordinal))
            {
                if (HasStubComponentNamed(go, "UtensilTimingConfig"))
                    return;
                var cfg = go.AddComponent<UtensilTimingConfig>();
                ParseUtensilTiming(prefabTag.Substring(UtensilTimingConfig.TagPrefix.Length), cfg);
                StubLog.Log("[CustomStub] 自愈 UtensilTimingConfig: " + go.name);
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
        ///  - TerminalGuard 发现每 60 帧，已 guard 刷新每 30 帧。</summary>
        private class StubTicker : MonoBehaviour
        {
            private float m_cookAccum;

            private void Update()
            {
                try
                {
                    var frame = Time.frameCount;
                    m_cookAccum += Time.deltaTime;
                    if (frame % HotPotCookIntervalFrames == 0)
                    {
                        if (m_cookAccum > 0f)
                        {
                            HotPot.CookPotsOverBurner(m_cookAccum);
                            m_cookAccum = 0f;
                        }
                    }
                    if (frame % HotPotFlameIntervalFrames == 0)
                        HotPot.TickWokFlame();
                    if (frame % HotPotMaintenanceIntervalFrames == 0)
                        HotPot.TickMaintenance();
                    if (frame % VoidFallIntervalFrames == 0)
                        PushableVoidFall.Tick();
                    if (frame % UtensilTimingIntervalFrames == 0
                        && !UtensilTiming.IsScanComplete())
                        UtensilTiming.Tick();
                    if (frame % TerminalGuardDiscoverIntervalFrames == 0)
                        TerminalGuard.TickDiscover();
                    if (frame % TerminalGuardRefreshIntervalFrames == 0)
                        TerminalGuard.TickRefreshGuarded();
                }
                catch (Exception ex)
                {
                    StubLog.LogWarn("[CustomStub.StubTicker] tick skipped: " + ex.Message);
                }
            }
        }
    }
}
