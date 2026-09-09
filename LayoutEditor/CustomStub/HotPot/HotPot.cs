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
    ///
    /// 全部游戏类型经 GameApi 反射（游戏侧无 LevelEditor.* 类型）。
    /// Ticker 由 EntryPoint.Install 创建（编辑器 Play 与游戏 loader 两条路径统一）。
    /// </summary>
    internal static class HotPot
    {
        private static readonly Dictionary<UnityEngine.Object, bool> s_cookedState = new Dictionary<UnityEngine.Object, bool>();
        private static readonly HashSet<UnityEngine.Object> s_liftCompleted = new HashSet<UnityEngine.Object>();
        private static Component[] s_cachedRegions;
        private static bool s_regionCacheValid;
        private static bool s_loggedSelfCheck;
        private static bool s_loggedFirstPot;
        private static bool s_loggedFirstCook;

        // ---- 对象缓存（v5 性能优化）----
        // 原实现每个 tick（20Hz/4Hz/2Hz）都 FindObjectsOfType 全场景扫描——Unity 2017
        // 下这是 O(全场景对象)+数组分配，是帧率腰斩的主因。现改为 ~1s 周期重建的
        // 缓存列表（均已按 IsLargePot 过滤一次）：tick 只遍历缓存；空缓存时探测
        // 退避到 2s（无火锅内容的关卡每秒仅数次 FindAll，近零开销）。大锅在运行时
        // 才装配（PushablePot），晚出现最多 1~2s 被纳入——锅开局为空锅，无影响。
        private static readonly List<Component> s_potHandlers = new List<Component>();
        private static readonly List<Component> s_woks = new List<Component>();
        private static readonly List<Component> s_ccds = new List<Component>();
        private static float s_nextCacheRefresh = -1f;
        private const float CacheRefreshSeconds = 1f;
        private const float CacheRefreshIdleSeconds = 2f;

        /// <summary>场景切换时由 EntryPoint 调用：失效全部缓存与一次性记录
        /// （含 s_triggerExpanded/s_soupFixed——v5 前这两个 HashSet 跨场景不清，
        /// 每换一关累积一批假 null 键）。</summary>
        internal static void OnSceneChanged()
        {
            s_regionCacheValid = false;
            s_cachedRegions = null;
            s_liftCompleted.Clear();
            s_triggerExpanded.Clear();
            s_soupFixed.Clear();
            s_potHandlers.Clear();
            s_woks.Clear();
            s_ccds.Clear();
            s_nextCacheRefresh = -1f;
            PruneCookedStates();
        }

        /// <summary>刷新 CookingRegion 缓存（IsOverBurner / ExpandBurner 共用，避免嵌套 FindAll）。</summary>
        internal static void RefreshRegionCache()
        {
            s_regionCacheValid = true;
            if (GameApi.CookingRegionType == null)
            {
                s_cachedRegions = new Component[0];
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
        }

        private static void EnsureRegionCache()
        {
            if (!s_regionCacheValid)
                RefreshRegionCache();
        }

        /// <summary>按 IsLargePot 过滤重建某个游戏类型的场景实例缓存。</summary>
        private static void RebuildLargePotCache(Type type, List<Component> list)
        {
            list.Clear();
            if (type == null)
                return;
            var raw = GameApi.FindAll(type);
            for (int i = 0; i < raw.Length; i++)
            {
                var c = raw[i] as Component;
                if (c != null && IsLargePot(c.transform))
                    list.Add(c);
            }
        }

        /// <summary>~1s 周期的缓存重建（全部 tick 的唯一 FindAll 汇聚点）。
        /// 一次性维护项（抬格/扩触发盒）也挪进本周期——原 30 帧维护 tick 的
        /// FindAll 全部收敛至此。空场景（无任何火锅内容）退避到 2s 一探。</summary>
        private static void EnsurePotCaches()
        {
            if (Time.unscaledTime < s_nextCacheRefresh)
                return;
            RebuildLargePotCache(GameApi.ServerCookingHandlerType, s_potHandlers);
            RebuildLargePotCache(GameApi.WokEffectsType, s_woks);
            RebuildLargePotCache(GameApi.ContentsCosmeticType, s_ccds);
            RefreshRegionCache();
            var any = s_potHandlers.Count > 0 || s_woks.Count > 0
                || s_ccds.Count > 0 || s_cachedRegions.Length > 0;
            s_nextCacheRefresh = Time.unscaledTime
                + (any ? CacheRefreshSeconds : CacheRefreshIdleSeconds);
            if (!any)
                return;
            LiftCookingRegionGrid();
            ExpandBurnerTriggers();
        }

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
                    StubLog.Log("[HotPot] 发现大锅: " + handler.name);
                }
                if (!IsOverBurner(handler.transform))
                    continue;
                if (InvokeBool(handler, GameApi.HandlerIsBurningMethod) ||
                    InvokeBool(handler, GameApi.HandlerIsCookedMethod))
                    continue;
                // 锅内无内容物不加热（与宿主 ServerCookingRegion 行为一致；
                // 不用 GetOrderComposition：锅刚实例化时容器未同步会 NRE）。
                var container = GameApi.GetComponentInChildren(handler.gameObject, GameApi.ServerIngredientContainerType);
                if (container == null || !InvokeBool(container, GameApi.HasContentsMethod))
                    continue;
                if (GameApi.HandlerCookMethod != null)
                {
                    GameApi.HandlerCookMethod.Invoke(handler, new object[] { deltaTime });
                    if (!s_loggedFirstCook)
                    {
                        // 一次性：直驱烹饪链路已活的证据
                        s_loggedFirstCook = true;
                        StubLog.Log("[HotPot] 直驱烹饪已生效: " + handler.name);
                    }
                }
            }
        }

        /// <summary>低频维护（汤面/煮熟提示），由 StubTicker 每 ~30 帧调用。
        /// 抬格/扩触发盒为一次性维护项，已挪进 EnsurePotCaches 的 ~1s 刷新周期。</summary>
        internal static void TickMaintenance()
        {
            LogSelfCheckOnce();
            EnsurePotCaches();
            FixSoupLevel();
            AlertWhenCooked();
        }

        /// <summary>锅底火焰 Enter/Exit，由 StubTicker 每 ~15 帧调用。</summary>
        internal static void TickWokFlame()
        {
            LogSelfCheckOnce();
            EnsurePotCaches();
            KeepWokFlameOn();
        }

        private static void LogSelfCheckOnce()
        {
            if (s_loggedSelfCheck)
                return;
            s_loggedSelfCheck = true;
            StubLog.Log("[HotPot] 反射自检: ServerCookingRegion=" + (GameApi.ServerCookingRegionType != null)
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
        /// 即绕过宿主格子路径、由运行时直驱，扩触发盒是等效且零副作用的落点）。</summary>
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
                    var col = GameApi.RegionTriggerAreaField.GetValue(region) as Collider;
                    var box = col as BoxCollider;
                    if (box == null)
                        continue;
                    var size = box.size;
                    box.size = new Vector3(size.x + BurnerTriggerExpandXZ, size.y, size.z + BurnerTriggerExpandXZ);
                    StubLog.Log("[HotPot] 灶台触发范围已加宽: " + region.name
                        + " " + size.x.ToString("0.##") + "→" + box.size.x.ToString("0.##"));
                }
                catch (System.Exception ex)
                {
                    StubLog.LogWarn("[HotPot] 灶台触发范围加宽失败: " + ex.Message);
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
            try { capacity = (int)GameApi.ContainerCapacityField.GetValue(container); }
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

        /// <summary>把每个 CookingRegion 的 Server/Client 同步器缓存格层抬到 y=1（操作面）。</summary>
        private static void LiftCookingRegionGrid()
        {
            LiftIndexFor(GameApi.ServerCookingRegionType, GameApi.ServerGridIndexField);
            LiftIndexFor(GameApi.ClientCookingRegionType, GameApi.ClientGridIndexField);
        }

        /// <summary>GridIndex 是不可变 struct（m_y 私有字段），装箱改写后整体写回。</summary>
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
                    if (IsOverBurner(wok.transform))
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
        /// 局部 (-0.6, ·, +0.6)，用原点会差半格）。</summary>
        private static bool IsOverBurner(Transform t)
        {
            if (t == null || GameApi.CookingRegionType == null)
                return false;
            EnsureRegionCache();
            var col = t.GetComponentInChildren<Collider>();
            var center = col != null ? col.bounds.center : t.position;
            for (int i = 0; i < s_cachedRegions.Length; i++)
            {
                var region = s_cachedRegions[i] as Behaviour;
                if (region == null || !region.enabled)
                    continue;
                if (GameApi.RegionTriggerAreaField == null)
                    continue;
                var area = GameApi.RegionTriggerAreaField.GetValue(region) as Collider;
                if (area == null)
                    continue;
                if (!TimedCookingSwitch.IsHeatingAt(region.transform))
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
    }
}
