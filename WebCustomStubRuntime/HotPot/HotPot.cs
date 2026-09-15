using System;
using System.Collections.Generic;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 火锅大锅/灶台运行时管理器（CustomStub 版，接替原 LayoutRuntimeHotPot）。
    ///
    /// 处理火锅链路的四个断点：
    ///  1) 灶台 stub 在地板块层（grid y=0），锅在操作面层（y=1）：宿主 Server/Client
    ///     CookingRegion 缓存的 m_gridIndex 要求「锅灶同格」才加热 → 抬格层到 y=1；
    ///  2) 锅底 wok_flame 环形火只在「锅占据 CookingRegion 格子」时点亮 → 按触发区
    ///     直接驱动 Enter/ExitCookingRegion；
    ///  3) 煮熟「嘀嘀」提示链路易断 → 监听 IsCooked 翻转补发 ImCooked；
    ///  4) 可移动火锅（动态格子）常被宿主「同格」判定挡在烹饪链路外 → 按触发区
    ///     直接推进 ServerCookingHandler.Cook(deltaTime)；
    ///  5) 汤面高度兜底（2026-09-03 从旧 LayoutRuntimeHotPot 移植）：宿主
    ///     PseudoPrefabCookingUtensil 仍会对「child 根无 WokEffectsCosmeticDecisions」
    ///     的锅写 -0.2（汤面沉底）。条件兜底——只修被覆写成负值的（&lt;0），修一次
    ///     即移交宿主回调；上游守卫生效时零影响。
    ///  6) 锅内视觉残留兜底（2026-09-14）：汤勺接走整锅后锅数据已空（第二勺接不到），
    ///     但锅内漂浮食材模型（WokCosmeticDecisions）/汤面（ContentsCosmeticDecisions）
    ///     的 vanilla 清空链在本环境失效（编辑器 Play 与真机同现）→ 稳态幂等清理：
    ///     客户端内容数==0 而视觉仍在时才动手；vanilla 链正常时恒为无操作。
    ///
    /// 全部游戏类型经 GameApi 反射（游戏侧无 LevelEditor.* 类型）。
    /// Ticker 由 EntryPoint.Install 创建（编辑器 Play 与游戏 loader 两条路径统一）。
    /// </summary>
    internal static class HotPot
    {
        private static readonly Dictionary<UnityEngine.Object, bool> s_cookedState = new Dictionary<UnityEngine.Object, bool>();
        private static readonly HashSet<UnityEngine.Object> s_liftCompleted = new HashSet<UnityEngine.Object>();
        private static bool s_loggedSelfCheck;
        private static bool s_loggedFirstPot;
        private static bool s_loggedFirstCook;

        private static readonly Component[] EmptyComponents = new Component[0];
        private static readonly Collider[] EmptyColliders = new Collider[0];
        private static readonly TimedCookingSwitch[] EmptySwitches = new TimedCookingSwitch[0];

        // ---- 灶台缓存（IsOverBurner / ExpandBurnerTriggers 共用）----
        // v12：除了 CookingRegion 组件本身，把「触发盒 Collider」与「定时开关引用」
        // 也一并缓存成同下标的并行数组。原实现每次 IsOverBurner 都对每个灶台做
        //   ① RegionTriggerAreaField.GetValue(region)（反射 + 装箱）
        //   ② TimedCookingSwitch.IsHeatingAt(region.transform)
        //      → GetComponentsInChildren<TimedCookingSwitch>(true)【每次分配一个数组】
        // 而 IsOverBurner 跑在 20Hz × 每口锅 × 每个灶台的热路径上——2 锅 6 灶台
        // ≈ 240 次数组分配/秒，是 Boehm GC 周期性卡顿的主因。
        private static Component[] s_cachedRegions = EmptyComponents;
        private static Collider[] s_regionAreas = EmptyColliders;
        private static TimedCookingSwitch[][] s_regionSwitchesDown;
        private static TimedCookingSwitch[] s_regionSwitchUp = EmptySwitches;
        private static bool s_regionCacheValid;

        // ---- 对象缓存（v5 性能优化；v12 分帧轮转 + 大锅门控）----
        // 原实现每个 tick（20Hz/4Hz/2Hz）都 FindObjectsOfType 全场景扫描——Unity 2017
        // 下这是 O(全场景对象)+数组分配，是帧率腰斩的主因。v5 改为 ~1s 周期重建的
        // 缓存列表（均已按 IsLargePot 过滤一次）：tick 只遍历缓存。
        //
        // v12 两处修正：
        //  1) 【分帧轮转】原来一次刷新要连打 7 次 FindObjectsOfType（4 类锅 + 灶台
        //     + Server/Client 格层），集中在同一帧 = 每秒一个尖峰。现拆成 7 个 slot，
        //     每 CacheSliceSeconds 只做 1 次扫描，整轮周期仍 ≈1s。
        //  2) 【大锅门控】原来的「是否有内容」判据里含 s_cachedRegions.Length > 0
        //     ——官方图只要有灶台就恒为 true，于是 LiftCookingRegionGrid（改写所有
        //     CookingRegion 的 m_gridIndex.y）与 ExpandBurnerTriggers（加宽所有灶台
        //     触发盒）会作用到官方图上，既是性能浪费更是业务副作用。现改为只认
        //     「场景里确有 web 大锅」，无大锅时整轮跳过灶台相关的一切读写并退避 5s。
        private static readonly List<Component> s_potHandlers = new List<Component>();
        private static readonly List<Collider> s_potColliders = new List<Collider>();
        private static readonly List<Component> s_clientCookHandlers = new List<Component>();
        private static readonly List<Collider> s_clientCookColliders = new List<Collider>();
        private static readonly List<Component> s_woks = new List<Component>();
        private static readonly List<Collider> s_wokColliders = new List<Collider>();
        private static readonly List<Component> s_ccds = new List<Component>();
        private static readonly List<Component> s_wokCosmetics = new List<Component>();
        private static float s_nextSliceAt = -1f;
        private static int s_cacheSlice;
        private static bool s_hasPots;
        private const float CacheSliceSeconds = 0.15f;
        private const float IdleRoundSeconds = 5f;
        private const int CacheSliceCount = 8;

        /// <summary>上一次由本模块直驱后记录的烹饪进度（判定「宿主是否也在推进这口锅」）。
        /// 见 CookPotsOverBurner 的让位逻辑。</summary>
        private static readonly Dictionary<UnityEngine.Object, float> s_drivenProgress =
            new Dictionary<UnityEngine.Object, float>();

        /// <summary>判定「进度自行增长过」的阈值。宿主 ServerCookingRegion 每 FixedUpdate
        /// 推进一个 deltaTime（≥1/60s），远大于本值；而 Cook 内部是精确浮点加法，
        /// 无累积误差，取 1e-4 足以区分「没被别人推过」与「被推过」。</summary>
        private const float ExternalDriveEpsilon = 0.0001f;

        /// <summary>场景切换时由 EntryPoint 调用：失效全部缓存与一次性记录
        /// （含 s_triggerExpanded/s_soupFixed——v5 前这两个 HashSet 跨场景不清，
        /// 每换一关累积一批假 null 键）。</summary>
        internal static void OnSceneChanged()
        {
            s_regionCacheValid = false;
            s_cachedRegions = EmptyComponents;
            s_regionAreas = EmptyColliders;
            s_regionSwitchesDown = null;
            s_regionSwitchUp = EmptySwitches;
            s_liftCompleted.Clear();
            s_triggerExpanded.Clear();
            s_soupFixed.Clear();
            s_potHandlers.Clear();
            s_potColliders.Clear();
            s_clientCookHandlers.Clear();
            s_clientCookColliders.Clear();
            s_woks.Clear();
            s_wokColliders.Clear();
            s_ccds.Clear();
            s_wokCosmetics.Clear();
            s_drivenProgress.Clear();
            s_nextSliceAt = -1f;
            s_cacheSlice = 0;
            s_hasPots = false;
            PruneCookedStates();
        }

        /// <summary>EntryPoint 三态门控的无 tag 探测通道（每次只做 1 次全场景扫描）：
        /// 场景里是否存在 web 大锅。slot 轮转三种游戏类型——客机上不存在
        /// Server* 同步器，只认单一类型会在联机客机侧漏判（火焰/汤面视觉会失效）。</summary>
        internal static bool ProbeHasLargePot(int slot)
        {
            Type type;
            switch (slot)
            {
                case 0: type = GameApi.ServerCookingHandlerType; break;
                case 1: type = GameApi.WokEffectsType; break;
                default: type = GameApi.ContentsCosmeticType; break;
            }
            if (type == null)
                return false;
            var raw = GameApi.FindAll(type);
            for (int i = 0; i < raw.Length; i++)
            {
                var c = raw[i] as Component;
                if (c != null && IsLargePot(c.transform))
                    return true;
            }
            return false;
        }

        /// <summary>刷新 CookingRegion 缓存（含触发盒与定时开关的同下标并行数组）。</summary>
        internal static void RefreshRegionCache()
        {
            s_regionCacheValid = true;
            if (GameApi.CookingRegionType == null)
            {
                s_cachedRegions = EmptyComponents;
                s_regionAreas = EmptyColliders;
                s_regionSwitchesDown = null;
                s_regionSwitchUp = EmptySwitches;
                return;
            }
            var raw = GameApi.FindAll(GameApi.CookingRegionType);
            var list = new List<Component>();
            for (int i = 0; i < raw.Length; i++)
            {
                var c = raw[i] as Component;
                if (c != null)
                    list.Add(c);
            }
            s_cachedRegions = list.ToArray();

            var n = s_cachedRegions.Length;
            s_regionAreas = n > 0 ? new Collider[n] : EmptyColliders;
            s_regionSwitchesDown = n > 0 ? new TimedCookingSwitch[n][] : null;
            s_regionSwitchUp = n > 0 ? new TimedCookingSwitch[n] : EmptySwitches;
            for (int i = 0; i < n; i++)
            {
                var region = s_cachedRegions[i];
                if (region == null)
                {
                    s_regionSwitchesDown[i] = EmptySwitches;
                    continue;
                }
                if (GameApi.RegionTriggerAreaField != null)
                {
                    try { s_regionAreas[i] = GameApi.RegionTriggerAreaField.GetValue(region) as Collider; }
                    catch (System.Exception ex)
                    {
                        WarnOnce(ref s_regionAreaFailLogged, "[HotPot] 读取灶台触发盒失败: " + ex.Message);
                    }
                }
                // 定时开关引用缓存（相位仍是每次实时读字段——相位会随时间翻转，
                // 不能缓存结果，只缓存「去哪读」）。语义与 TimedCookingSwitch.IsHeatingAt
                // 逐行等价：子树任一开关在加热相位即为加热，否则回落祖先链开关，
                // 都没有则视为常开。
                s_regionSwitchesDown[i] = region.GetComponentsInChildren<TimedCookingSwitch>(true);
                s_regionSwitchUp[i] = region.GetComponentInParent<TimedCookingSwitch>();
            }
        }

        private static void EnsureRegionCache()
        {
            if (!s_regionCacheValid)
                RefreshRegionCache();
        }

        /// <summary>缓存化的加热相位判定（等价 TimedCookingSwitch.IsHeatingAt，零分配）。</summary>
        private static bool IsRegionHeating(int index)
        {
            if (s_regionSwitchesDown == null || index >= s_regionSwitchesDown.Length)
                return true;
            var downs = s_regionSwitchesDown[index];
            if (downs != null)
            {
                for (int i = 0; i < downs.Length; i++)
                {
                    if (downs[i] != null && downs[i].IsHeatingPhase())
                        return true;
                }
            }
            var up = index < s_regionSwitchUp.Length ? s_regionSwitchUp[index] : null;
            if (up != null)
                return up.IsHeatingPhase();
            return true; // 无定时开关 = 常开
        }

        /// <summary>按 IsLargePot 过滤重建某个游戏类型的场景实例缓存。
        /// cols 非空时同步缓存每个实例的首个子树 Collider（IsOverBurner 用，
        /// 避免 20Hz 热路径上反复 GetComponentInChildren 做树遍历）。</summary>
        private static void RebuildLargePotCache(Type type, List<Component> list, List<Collider> cols)
        {
            list.Clear();
            if (cols != null)
                cols.Clear();
            if (type == null)
                return;
            var raw = GameApi.FindAll(type);
            for (int i = 0; i < raw.Length; i++)
            {
                var c = raw[i] as Component;
                if (c == null || !IsLargePot(c.transform))
                    continue;
                list.Add(c);
                if (cols != null)
                    cols.Add(c.GetComponentInChildren<Collider>());
            }
        }

        /// <summary>缓存维护的分帧轮转（全部 tick 的唯一 FindAll 汇聚点，v12）。
        /// 8 个 slot × CacheSliceSeconds ≈ 1.2s 一整轮，与 v5 的刷新周期同量级，
        /// 但每次只做 1 次全场景扫描，消除周期性尖峰。
        /// slot 0~4 重建五类大锅缓存；slot 4 结束时评估「是否真有大锅」——
        /// 没有就整轮跳过 slot 5~7（灶台缓存 / 扩触发盒 / 抬格层，全部是对
        /// CookingRegion 的读写），并退避 IdleRoundSeconds。这保证了官方图、
        /// 主菜单、世界地图的灶台绝不会被本模块触碰。</summary>
        private static void EnsurePotCaches()
        {
            var now = Time.unscaledTime;
            if (now < s_nextSliceAt)
                return;
            s_nextSliceAt = now + CacheSliceSeconds;
            switch (s_cacheSlice)
            {
                case 0:
                    RebuildLargePotCache(GameApi.ServerCookingHandlerType, s_potHandlers, s_potColliders);
                    break;
                case 1:
                    RebuildLargePotCache(GameApi.WokEffectsType, s_woks, s_wokColliders);
                    break;
                case 2:
                    RebuildLargePotCache(GameApi.ContentsCosmeticType, s_ccds, null);
                    break;
                case 3:
                    // 客户端烹饪同步器：驱动「锅在灶台上」标志用（主机/客机都有；
                    // 联机客机上没有 Server* 同步器，缺了它客机侧看不到烧糊预警）。
                    RebuildLargePotCache(GameApi.ClientCookingHandlerType, s_clientCookHandlers, s_clientCookColliders);
                    break;
                case 4:
                    RebuildLargePotCache(GameApi.WokCosmeticType, s_wokCosmetics, null);
                    s_hasPots = s_potHandlers.Count > 0 || s_woks.Count > 0
                        || s_ccds.Count > 0 || s_wokCosmetics.Count > 0 || s_clientCookHandlers.Count > 0;
                    if (!s_hasPots)
                    {
                        s_cacheSlice = 0;
                        s_nextSliceAt = now + IdleRoundSeconds;
                        s_regionCacheValid = false;
                        s_cachedRegions = EmptyComponents;
                        s_regionAreas = EmptyColliders;
                        s_regionSwitchesDown = null;
                        s_regionSwitchUp = EmptySwitches;
                        return;
                    }
                    break;
                case 5:
                    RefreshRegionCache();
                    ExpandBurnerTriggers();
                    break;
                case 6:
                    LiftIndexFor(GameApi.ServerCookingRegionType, GameApi.ServerGridIndexField);
                    break;
                default:
                    LiftIndexFor(GameApi.ClientCookingRegionType, GameApi.ClientGridIndexField);
                    break;
            }
            s_cacheSlice = (s_cacheSlice + 1) % CacheSliceCount;
        }

        /// <summary>直驱大锅烹饪（由 StubTicker 每 ~3 帧调用，传入累积 deltaTime）。
        ///
        /// 【为什么需要直驱】宿主 ServerCookingRegion.UpdateSynchronising 推进烹饪要求
        /// 「锅与灶台同格」（GetGridLocationFromPos(锅) == 灶台启动时缓存的 m_gridIndex）。
        /// 可移动火锅的格子是动态的，这道判定基本不成立，所以必须由本方法按触发区直驱。
        ///
        /// 【烧糊修复，2026-09-14】原实现在 IsCooked() 为真时就 continue——而
        /// ServerCookingHandler.IsCooked() 是 progress &gt;= m_cookingtime，于是进度被永久
        /// 冻结在「刚熟」那一刻：到不了 1.3×（即将烧糊的橙色预警）更到不了 2×（烧糊），
        /// 配了 burnTime 也没用。现改为一路推进到 IsBurning() 为止（该方法已被
        /// HarmonyPatches 按 UtensilTiming.m_burnTime 接管，未配置时仍是 vanilla 的 2×）。
        ///
        /// 【双驱动让位】去掉 IsCooked 早退后，静态大锅（同格判定成立、宿主也在推进）
        /// 会被推两遍 = 烹饪速度翻倍。用「观测进度是否自行增长过」来判定宿主是否在推：
        /// 增长过就本轮让位（只更新记录），没增长才由我们推。宿主停推（锅被推离灶台）
        /// 时下一轮自然接管，无需额外状态机。</summary>
        internal static void CookPotsOverBurner(float deltaTime)
        {
            if (deltaTime <= 0f || GameApi.ServerCookingHandlerType == null)
                return;
            EnsurePotCaches();
            for (int i = 0; i < s_potHandlers.Count; i++)
            {
                var handler = s_potHandlers[i];
                if (handler == null) // 已销毁（假 null），下次刷新剔除
                    continue;
                if (!s_loggedFirstPot)
                {
                    // 一次性：证明「发现大锅」这一环通了（没这行=识别/扫描环节断）
                    s_loggedFirstPot = true;
                    StubLog.Dbg("[HotPot] 发现大锅: " + handler.name);
                }
                if (!IsOverBurner(handler.transform, i < s_potColliders.Count ? s_potColliders[i] : null))
                    continue;
                // 已烧糊：宿主 Cook() 内部同样会拒绝推进，这里提前跳过省一次反射调用
                if (InvokeBool(handler, GameApi.HandlerIsBurningMethod))
                    continue;
                // 锅内无内容物不加热（与宿主 ServerCookingRegion 行为一致；
                // 不用 GetOrderComposition：锅刚实例化时容器未同步会 NRE）。
                var container = GameApi.GetComponentInChildren(handler.gameObject, GameApi.ServerIngredientContainerType);
                if (container == null || !InvokeBool(container, GameApi.HasContentsMethod))
                    continue;
                if (GameApi.HandlerCookMethod == null)
                    continue;

                // 宿主是否也在推进这口锅？（见方法注释「双驱动让位」）
                var before = InvokeFloat(handler, GameApi.ServerGetCookingProgressMethod);
                float lastDriven;
                if (s_drivenProgress.TryGetValue(handler, out lastDriven)
                    && before > lastDriven + ExternalDriveEpsilon)
                {
                    s_drivenProgress[handler] = before;
                    continue;
                }

                GameApi.HandlerCookMethod.Invoke(handler, new object[] { deltaTime });
                s_drivenProgress[handler] = InvokeFloat(handler, GameApi.ServerGetCookingProgressMethod);
                if (!s_loggedFirstCook)
                {
                    // 一次性：直驱烹饪链路已活的证据
                    s_loggedFirstCook = true;
                    StubLog.Dbg("[HotPot] 直驱烹饪已生效: " + handler.name);
                }
            }
            if (s_drivenProgress.Count > 64)
                PruneDrivenProgress();
        }

        private static void PruneDrivenProgress()
        {
            var dead = new List<UnityEngine.Object>();
            foreach (var pair in s_drivenProgress)
            {
                if (pair.Key == null)
                    dead.Add(pair.Key);
            }
            for (int i = 0; i < dead.Count; i++)
                s_drivenProgress.Remove(dead[i]);
        }

        /// <summary>低频维护（汤面/煮熟提示），由 StubTicker 每 ~30 帧调用。
        /// 抬格/扩触发盒为一次性维护项，已挪进 EnsurePotCaches 的 ~1s 刷新周期。</summary>
        internal static void TickMaintenance()
        {
            LogSelfCheckOnce();
            EnsurePotCaches();
            FixSoupLevel();
            AlertWhenCooked();
            ClearStalePotVisuals();
        }

        /// <summary>锅底火焰 Enter/Exit + 客户端「锅在灶台上」标志，由 StubTicker 每 ~15 帧调用。</summary>
        internal static void TickWokFlame()
        {
            LogSelfCheckOnce();
            EnsurePotCaches();
            KeepWokFlameOn();
            SyncClientCookingRegionFlag();
        }

        private static void LogSelfCheckOnce()
        {
            if (s_loggedSelfCheck)
                return;
            s_loggedSelfCheck = true;
            StubLog.Dbg("[HotPot] 反射自检: ServerCookingRegion=" + (GameApi.ServerCookingRegionType != null)
                + " ClientCookingRegion=" + (GameApi.ClientCookingRegionType != null)
                + " WokEffects=" + (GameApi.WokEffectsType != null)
                + " CookHandler=" + (GameApi.ServerCookingHandlerType != null)
                + " TriggerAudio=" + (GameApi.TriggerAudioMethod != null && GameApi.ImCookedTag != null));
        }

        /// <summary>灶台触发范围加宽（2026-09-03 用户实测：碰撞触发检测半径偏小）。
        /// 宿主判定链 = TriggerRecorder 采集（m_TriggerArea 触发盒）+ 同格校验；
        /// 本类的全部分支（IsOverBurner / 烹饪驱动 / 火焰常燃 / 提示音）也以
        /// m_TriggerArea.bounds 为准——运行时把触发盒 XZ 各加宽
        /// BurnerTriggerExpandXZ（默认 2.0→2.7，中心不动，占地对齐判定依赖中心）。
        /// 一次性、幂等（按实例记录）。不动全局 QuadGridManager.m_size：那会波及
        /// 推车/驾驶的网格占用语义，且火锅锅枢轴与灶台枢轴天然错一整格（旧系统
        /// 即绕过宿主格子路径、由运行时直驱，扩触发盒是等效且零副作用的落点）。
        ///
        /// v12：只在「场景确有 web 大锅」时才由 EnsurePotCaches 的 slot 4 调用
        /// （见该方法注释）——官方图/世界地图的灶台绝不触碰。</summary>
        private const float BurnerTriggerExpandXZ = 0.7f;
        private static readonly HashSet<UnityEngine.Object> s_triggerExpanded = new HashSet<UnityEngine.Object>();

        private static void ExpandBurnerTriggers()
        {
            if (GameApi.CookingRegionType == null || GameApi.RegionTriggerAreaField == null)
                return;
            EnsureRegionCache();
            for (int i = 0; i < s_cachedRegions.Length; i++)
            {
                var region = s_cachedRegions[i];
                if (region == null || !s_triggerExpanded.Add(region))
                    continue;
                try
                {
                    var box = (i < s_regionAreas.Length ? s_regionAreas[i] : null) as BoxCollider;
                    if (box == null)
                        continue;
                    var size = box.size;
                    box.size = new Vector3(size.x + BurnerTriggerExpandXZ, size.y, size.z + BurnerTriggerExpandXZ);
                    StubLog.Dbg("[HotPot] 灶台触发范围已加宽: " + region.name
                        + " " + size.x.ToString("0.##") + "→" + box.size.x.ToString("0.##"));
                }
                catch (System.Exception ex)
                {
                    WarnOnce(ref s_expandFailLogged, "[HotPot] 灶台触发范围加宽失败: " + ex.Message);
                }
            }
        }

        /// <summary>5) 大锅汤面兜底：恢复原版 WhenEmpty=0.25，并按当前内容数即时重摆
        ///（OnContentChanged 公式）。只处理被宿主覆写成负值的（&lt;0），修一次后跳过
        ///（后续重摆交给宿主回调）——与旧 LayoutRuntimeHotPot.FixSoupLevel 一致。</summary>
        private static readonly HashSet<UnityEngine.Object> s_soupFixed = new HashSet<UnityEngine.Object>();

        private static void FixSoupLevel()
        {
            if (GameApi.ContentsCosmeticType == null || GameApi.ContentsYEmptyField == null)
                return;
            for (int i = 0; i < s_ccds.Count; i++)
            {
                var ccd = s_ccds[i];
                if (ccd == null)
                    continue;
                var contentsObj = GameApi.ContentsObjectField != null
                    ? GameApi.ContentsObjectField.GetValue(ccd) as GameObject : null;
                if (contentsObj == null)
                    continue;
                float yEmpty;
                try { yEmpty = (float)GameApi.ContentsYEmptyField.GetValue(ccd); }
                catch (System.Exception ex)
                {
                    WarnOnce(ref s_soupYEmptyFailLogged, "[HotPot] 读取汤面 WhenEmpty 失败: " + ex.Message);
                    continue;
                }
                if (yEmpty >= 0f || !s_soupFixed.Add(ccd))
                    continue;
                GameApi.ContentsYEmptyField.SetValue(ccd, 0.25f);
                RelayoutContents(ccd, contentsObj);
            }
        }

        private static void RelayoutContents(Component ccd, GameObject contentsObj)
        {
            if (GameApi.ServerIngredientContainerType == null || GameApi.ClientIngredientContainerType == null)
                return;
            // 容器优先挂在 ccd.m_gameObject（绑定的工作站/锅体）上，回落自身
            var go = GameApi.ContentsGameObjectField != null
                ? GameApi.ContentsGameObjectField.GetValue(ccd) as GameObject : null;
            if (go == null)
                go = ccd.gameObject;
            var container = GameApi.GetComponent(go, GameApi.ServerIngredientContainerType);
            var clientContainer = GameApi.GetComponent(go, GameApi.ClientIngredientContainerType);
            if (container == null || clientContainer == null)
                return;
            var contents = GameApi.GetContentsMethod != null
                ? GameApi.GetContentsMethod.Invoke(clientContainer, null) as Array : null;
            if (contents == null || contents.Length == 0)
                return;
            int capacity;
            // 容量在 IngredientContainer（数据组件）上，不在 ServerIngredientContainer
            // （同步器只是持有它的引用）——v14 前这里读的是同步器上不存在的字段，
            // 反射常年为 null、每次都从这里早退（汤面高度重摆从未生效）。
            var capacityHolder = GameApi.GetComponent(go, GameApi.IngredientContainerType);
            if (capacityHolder == null || GameApi.IngredientCapacityField == null)
                return;
            try { capacity = (int)GameApi.IngredientCapacityField.GetValue(capacityHolder); }
            catch (System.Exception ex)
            {
                WarnOnce(ref s_soupCapacityFailLogged, "[HotPot] 读取容器容量失败（汤面重摆跳过）: " + ex.Message);
                return;
            }
            if (capacity <= 0)
                return;
            float yEmpty;
            float yFull;
            try
            {
                yEmpty = (float)GameApi.ContentsYEmptyField.GetValue(ccd);
                yFull = GameApi.ContentsYFullField != null ? (float)GameApi.ContentsYFullField.GetValue(ccd) : 0.2f;
            }
            catch (System.Exception ex)
            {
                WarnOnce(ref s_soupYReadFailLogged, "[HotPot] 读取汤面高度字段失败（汤面重摆跳过）: " + ex.Message);
                return;
            }
            float fill = Mathf.Clamp01((float)contents.Length / (float)capacity);
            float y = fill * (yFull - yEmpty) + yEmpty;
            var tr = contentsObj.transform;
            var lp = tr.localPosition;
            lp.y = y;
            tr.localPosition = lp;
        }

        /// <summary>把 CookingRegion 的 Server/Client 同步器缓存格层抬到 y=1（操作面）。
        /// v12：由 EnsurePotCaches 的 slot 5/6 分帧调用（各自 1 次 FindAll），
        /// 且只在场景确有 web 大锅时执行。</summary>
        /// <remarks>GridIndex 是不可变 struct（m_y 私有字段），装箱改写后整体写回。</remarks>
        private static void LiftIndexFor(Type type, System.Reflection.FieldInfo field)
        {
            if (type == null || field == null)
                return;
            var syncs = GameApi.FindAll(type);
            for (int i = 0; i < syncs.Length; i++)
            {
                var sync = syncs[i] as Component;
                if (sync == null || s_liftCompleted.Contains(sync))
                    continue;
                try
                {
                    var boxed = field.GetValue(sync);
                    if (boxed == null)
                        continue;
                    var yField = boxed.GetType().GetField("m_y",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (yField == null)
                        continue;
                    int y = (int)yField.GetValue(boxed);
                    if (y >= 1)
                    {
                        s_liftCompleted.Add(sync);
                        continue;
                    }
                    yField.SetValue(boxed, 1);
                    field.SetValue(sync, boxed);
                    s_liftCompleted.Add(sync);
                }
                catch (System.Exception ex)
                {
                    // 单个同步器失败不影响其余
                    WarnOnce(ref s_liftFailLogged, "[HotPot] 灶台格层抬升单点失败: " + ex.Message);
                }
            }
        }

        /// <summary>锅底 wok_flame 常燃：锅在启用的 CookingRegion 上则 Enter，离开则 Exit
        ///  （可移动火锅离开灶台后熄火）。</summary>
        private static void KeepWokFlameOn()
        {
            if (GameApi.WokEffectsType == null)
                return;
            for (int i = 0; i < s_woks.Count; i++)
            {
                var wok = s_woks[i];
                if (wok == null)
                    continue;
                try
                {
                    if (IsOverBurner(wok.transform, i < s_wokColliders.Count ? s_wokColliders[i] : null))
                        GameApi.WokEnterMethod.Invoke(wok, null);
                    else
                        GameApi.WokExitMethod.Invoke(wok, null);
                }
                catch (System.Exception ex)
                {
                    WarnOnce(ref s_flameFailLogged, "[HotPot] 锅底火焰驱动失败: " + ex.Message);
                }
            }
        }

        /// <summary>客户端「锅在灶台上」标志（ClientCookingHandler.m_isInCookingRegion）
        /// 按触发区直驱（2026-09-14，与烧糊修复配套）。
        ///
        /// vanilla 只在 ClientCookingRegion.UpdateSynchronising 里驱动它，判定是
        /// GridManager.GetGridOccupant(m_gridIndex) 再做一次同格复核——可移动火锅的
        /// 动态格子过不了，标志恒为 false。后果不是「少个特效」而是**预警图标被吞**：
        /// ClientCookingHandler.ApplyServerUpdate 在标志为 false 时，把一切非
        /// Progressing 的状态强制显示为 Idle，于是「即将烧糊」(OverDoing) 的橙色
        /// 警告图标与烧糊状态都看不见。这里按与烹饪直驱同一套触发区判定补上该标志，
        /// 幂等（只是写一个 bool），与 vanilla 同时驱动时结论一致。</summary>
        private static void SyncClientCookingRegionFlag()
        {
            if (GameApi.ClientCookingEnterRegionMethod == null || GameApi.ClientCookingExitRegionMethod == null)
                return;
            for (int i = 0; i < s_clientCookHandlers.Count; i++)
            {
                var handler = s_clientCookHandlers[i];
                if (handler == null)
                    continue;
                try
                {
                    var inRegion = IsOverBurner(handler.transform,
                        i < s_clientCookColliders.Count ? s_clientCookColliders[i] : null);
                    if (inRegion)
                        GameApi.ClientCookingEnterRegionMethod.Invoke(handler, null);
                    else
                        GameApi.ClientCookingExitRegionMethod.Invoke(handler, null);
                }
                catch (System.Exception ex)
                {
                    WarnOnce(ref s_cookRegionFlagFailLogged,
                        "[HotPot] 客户端烹饪区标志驱动失败（烧糊预警图标可能不显示）: " + ex.Message);
                }
            }
        }

        /// <summary>6) 锅内视觉残留兜底（2026-09-14）：汤勺接走整锅后锅数据已空，
        /// 但锅内漂浮食材模型/汤面的 vanilla 清空链在本环境失效（编辑器与真机同现，
        /// 具体断点未定位——可能回调链被中途异常吞掉）。稳态幂等判定：客户端内容数==0
        /// 而视觉仍在时才清理；vanilla 链正常工作时本方法恒为无操作（不闪烁、不打架）。</summary>
        private static void ClearStalePotVisuals()
        {
            if (GameApi.ClientIngredientContainerType == null || GameApi.GetContentsMethod == null)
                return;
            ClearStaleWokContents();
            ClearStaleSoupSurface();
        }

        /// <summary>漂浮食材模型：WokCosmeticDecisions.m_container 子物体全销毁
        /// （等价 vanilla DestroyContents；锅已空时 vanilla 不会再重建）。</summary>
        private static void ClearStaleWokContents()
        {
            if (GameApi.MealContainerField == null || GameApi.ClientOrderDefinitionType == null)
                return;
            for (int i = 0; i < s_wokCosmetics.Count; i++)
            {
                var wok = s_wokCosmetics[i];
                if (wok == null) // 已销毁（假 null），下次刷新剔除
                    continue;
                if (!ClientContentsEmpty(wok.transform))
                    continue;
                GameObject containerGO;
                try { containerGO = GameApi.MealContainerField.GetValue(wok) as GameObject; }
                catch (System.Exception ex)
                {
                    WarnOnce(ref s_visualClearFailLogged, "[HotPot] 读取锅内视觉容器失败: " + ex.Message);
                    continue;
                }
                if (containerGO == null)
                    continue;
                var tr = containerGO.transform;
                if (tr.childCount == 0)
                    continue;
                for (int j = tr.childCount - 1; j >= 0; j--)
                    UnityEngine.Object.Destroy(tr.GetChild(j).gameObject);
                if (!s_loggedVisualClear)
                {
                    s_loggedVisualClear = true;
                    StubLog.Dbg("[HotPot] 锅内漂浮食材视觉残留已清理: " + wok.name);
                }
            }
        }

        /// <summary>汤面：锅已空而 ContentsCosmeticDecisions.m_contentsObject 仍显示时隐藏
        /// （等价 vanilla OnContentChanged 的空内容分支；下次投菜 vanilla 会重新显示）。</summary>
        private static void ClearStaleSoupSurface()
        {
            if (GameApi.ContentsObjectField == null)
                return;
            for (int i = 0; i < s_ccds.Count; i++)
            {
                var ccd = s_ccds[i];
                if (ccd == null)
                    continue;
                GameObject contentsObj;
                try { contentsObj = GameApi.ContentsObjectField.GetValue(ccd) as GameObject; }
                catch (System.Exception ex)
                {
                    WarnOnce(ref s_visualClearFailLogged, "[HotPot] 读取汤面对象失败: " + ex.Message);
                    continue;
                }
                if (contentsObj == null || !contentsObj.activeSelf)
                    continue;
                // 容器优先挂在 ccd.m_gameObject（绑定的锅体）上，回落自身（同 RelayoutContents）
                var go = GameApi.ContentsGameObjectField != null
                    ? GameApi.ContentsGameObjectField.GetValue(ccd) as GameObject : null;
                if (go == null)
                    go = ccd.gameObject;
                if (!ClientContentsEmpty(go.transform))
                    continue;
                contentsObj.SetActive(false);
                if (!s_loggedVisualClear)
                {
                    s_loggedVisualClear = true;
                    StubLog.Dbg("[HotPot] 汤面视觉残留已隐藏: " + ccd.name);
                }
            }
        }

        /// <summary>从 t 沿父级向上找首个 IClientOrderDefinition 所在 GameObject
        /// （镜像 WokCosmeticDecisions.FindOrderDefinition 的上溯，建立「外观实例 → 所属锅」
        /// 关联），读其 ClientIngredientContainer 内容数；==0 返回 true。
        /// 找不到关联/容器/读数失败一律 false（不动视觉，宁可漏清不可错清）。</summary>
        private static bool ClientContentsEmpty(Transform t)
        {
            GameObject owner = null;
            for (var cur = t; cur != null; cur = cur.parent)
            {
                if (cur.GetComponent(GameApi.ClientOrderDefinitionType) != null)
                {
                    owner = cur.gameObject;
                    break;
                }
            }
            if (owner == null)
                return false;
            var container = owner.GetComponent(GameApi.ClientIngredientContainerType);
            if (container == null)
                return false;
            try
            {
                var contents = GameApi.GetContentsMethod.Invoke(container, null) as System.Array;
                return contents != null && contents.Length == 0;
            }
            catch (System.Exception ex)
            {
                WarnOnce(ref s_visualClearFailLogged, "[HotPot] 读取锅内容数失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>监听大锅 ServerCookingHandler.IsCooked() 翻转，补发 ImCooked 提示音。</summary>
        private static void AlertWhenCooked()
        {
            if (GameApi.ServerCookingHandlerType == null)
                return;
            for (int i = 0; i < s_potHandlers.Count; i++)
            {
                var handler = s_potHandlers[i];
                if (handler == null)
                    continue;
                bool cooked;
                if (!s_cookedState.TryGetValue(handler, out cooked))
                    cooked = false;
                bool now = InvokeBool(handler, GameApi.HandlerIsCookedMethod);
                if (now && !cooked && GameApi.TriggerAudioMethod != null && GameApi.ImCookedTag != null)
                {
                    try
                    {
                        GameApi.TriggerAudioMethod.Invoke(null,
                            new object[] { GameApi.ImCookedTag, handler.gameObject.layer });
                    }
                    catch (System.Exception ex)
                    {
                        WarnOnce(ref s_audioFailLogged, "[HotPot] 煮熟提示音触发失败: " + ex.Message);
                    }
                }
                if (now != cooked)
                    s_cookedState[handler] = now;
            }
            // 场景重载后清理已销毁的键
            if (s_cookedState.Count > 64)
                PruneCookedStates();
        }

        private static void PruneCookedStates()
        {
            var dead = new List<UnityEngine.Object>();
            foreach (var pair in s_cookedState)
            {
                if (pair.Key == null)
                    dead.Add(pair.Key);
            }
            for (int i = 0; i < dead.Count; i++)
                s_cookedState.Remove(dead[i]);
        }

        /// <summary>锅根（含可移动火锅 wrapper / 载具）是否落在某个启用 CookingRegion 的
        /// 触发区内（XZ 判定，用碰撞体中心而非 transform 原点——锅体碰撞中心在自身
        /// 局部 (-0.6, ·, +0.6)，用原点会差半格）。
        ///
        /// v12：跑在 20Hz × 每口锅 × 每个灶台的热路径上，全部查找已前移到 ~1s 的
        /// 缓存刷新——锅的 Collider 由调用方从 s_potColliders/s_wokColliders 传入；
        /// 灶台触发盒与定时开关引用走 s_regionAreas/IsRegionHeating（零反射、零分配）。
        /// cachedCollider 为 null 时回落即时查找，保持旧行为。</summary>
        private static bool IsOverBurner(Transform t, Collider cachedCollider)
        {
            if (t == null || GameApi.CookingRegionType == null)
                return false;
            EnsureRegionCache();
            var col = cachedCollider != null ? cachedCollider : t.GetComponentInChildren<Collider>();
            var center = col != null ? col.bounds.center : t.position;
            for (int i = 0; i < s_cachedRegions.Length; i++)
            {
                var region = s_cachedRegions[i] as Behaviour;
                if (region == null || !region.enabled)
                    continue;
                var area = i < s_regionAreas.Length ? s_regionAreas[i] : null;
                if (area == null)
                    continue;
                if (!IsRegionHeating(i))
                    continue;
                var b = area.bounds;
                if (center.x >= b.min.x && center.x <= b.max.x && center.z >= b.min.z && center.z <= b.max.z)
                    return true;
            }
            return false;
        }

        /// <summary>按对象名识别大锅（large_pot / pot_01_pushable / pushable_object 载具）。</summary>
        internal static bool IsLargePot(Transform t)
        {
            while (t != null)
            {
                var n = t.name;
                if (n.IndexOf("large_pot", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (n.IndexOf("pot_01_pushable", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("pushable_object", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                t = t.parent;
            }
            return false;
        }

        // ---- 热路径异常 once-flag（用户约定：异常只报一次，不刷屏） ----
        private static bool s_soupYEmptyFailLogged;
        private static bool s_soupCapacityFailLogged;
        private static bool s_soupYReadFailLogged;
        private static bool s_liftFailLogged;
        private static bool s_flameFailLogged;
        private static bool s_audioFailLogged;
        private static bool s_invokeBoolFailLogged;
        private static bool s_visualClearFailLogged;
        private static bool s_loggedVisualClear;
        private static bool s_regionAreaFailLogged;
        private static bool s_expandFailLogged;
        private static bool s_cookRegionFlagFailLogged;

        private static void WarnOnce(ref bool flag, string msg)
        {
            if (flag)
                return;
            flag = true;
            StubLog.LogWarn(msg);
        }

        private static bool InvokeBool(Component c, System.Reflection.MethodInfo m)
        {
            if (c == null || m == null)
                return false;
            try
            {
                return (bool)m.Invoke(c, null);
            }
            catch (System.Exception ex)
            {
                WarnOnce(ref s_invokeBoolFailLogged,
                    "[HotPot] 状态查询调用失败 " + m.DeclaringType.Name + "." + m.Name + ": " + ex.Message);
                return false;
            }
        }

        private static bool s_invokeFloatFailLogged;

        private static float InvokeFloat(Component c, System.Reflection.MethodInfo m)
        {
            if (c == null || m == null)
                return 0f;
            try
            {
                var raw = m.Invoke(c, null);
                return raw is float ? (float)raw : 0f;
            }
            catch (System.Exception ex)
            {
                WarnOnce(ref s_invokeFloatFailLogged,
                    "[HotPot] 进度查询调用失败 " + m.DeclaringType.Name + "." + m.Name + ": " + ex.Message);
                return 0f;
            }
        }
    }
}
