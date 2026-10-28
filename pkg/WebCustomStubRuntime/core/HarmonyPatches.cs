using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// Harmony 补丁集（CustomStub 版，接替原 ServerRespawnCollider / RespawnColliderMessage
    /// 源码覆盖补丁——stub 程序集无法替换宿主类，只能用 Harmony prefix 拦截）。
    ///
    /// 目标方法在 EntryPoint.Install 里手工绑定（HarmonyPatch 特性无法引用
    /// 宿主类型）。前缀方法签名按参数名注入（_gameObject 与宿主方法形参一致）。
    /// </summary>
    internal static class HarmonyPatches
    {
        // ---- 热路径异常 once-flag（前缀/后缀跑在宿主热路径上：每类异常只报一次，
        // 报完放行原方法=原版行为兜底，绝不让补丁异常炸进宿主逻辑） ----
        private static readonly System.Collections.Generic.HashSet<string> s_warnedOnce =
            new System.Collections.Generic.HashSet<string>();

        private static void WarnOnce(string key, string msg)
        {
            if (!s_warnedOnce.Add(key))
                return;
            StubLog.LogWarn(msg);
        }

        // ---- 快速放行（v5 性能）：这些方法被宿主「每帧×每锅具」调用。全场景没有
        // 任何生效中的 UtensilTiming（burn/overMix>0）时，首行静态字段检查直接
        // 放行原方法，不做 GetComponent/装箱——纳秒级开销。----

        /// <summary>
        /// ServerRespawnCollider.ObjectAdded(GameObject) 前缀：
        ///  - 玩家触 KillPlane → 先强制脱离可移动火锅（否则玩家成为即将隐藏载具的
        ///    子物体，重生协程随父失效 = 「双人双双落水卡死」）；
        ///  - 可移动火锅自身 → 跳过宿主重生（宿主 DestroyEntity 会把实体连同模型
        ///    永久销毁），坠落/重生由 CustomStub.PushableVoidFall 负责。
        /// 返回 false = 跳过原方法。
        /// </summary>
        private static bool RespawnColliderObjectAddedPrefix(GameObject _gameObject)
        {
            if (_gameObject == null)
                return true;
            try
            {
                if (GameApi.GetComponent(_gameObject, GameApi.PlayerControlsType) != null)
                {
                    PushableVoidFall.DetachPlayerFromVoidFallPots(_gameObject);
                    // Web 背包落水清空：穿戴者落水 → 其背上的背包丢失全部存货
                    var wornMarker = _gameObject.GetComponentInChildren<WebBackpackChild>(true);
                    if (wornMarker != null && wornMarker.storage != null)
                        wornMarker.storage.OnFellInWater();
                }
                // 背包本体落水（未穿戴时坠水）
                var marker = _gameObject.GetComponent<WebBackpackChild>();
                if (marker != null && marker.storage != null)
                    marker.storage.OnFellInWater();
                if (PushableVoidFall.ShouldIgnoreKillPlane(_gameObject))
                    return false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("killplane", "[CustomStub.Harmony] KillPlane 前缀异常（放行原方法）: " + ex.Message);
            }
            return true;
        }

        // ============ 空气取出拦截（随机箱空气候，RandomCrate v8） ============
        //
        // 目标：ServerPickupItemSpawner.HandlePickup(ICarrier, Vector2)。
        // 原方法内联 NetworkUtils.ServerSpawnPrefab——m_itemPrefab 为 null 时
        // SpawnableEntityCollection.SpawnEntity 按 ID 取负下标必炸，且null 会外溢到
        // GameUtils.GetIngredientCrates 等读取点。RandomCrate 用哨兵方案（不置空字段），
        // 由本前缀查「空气待取表」拦截：命中=跳过原方法（不生成、不持取、玩家手空——
        // 调用方 ReceivePickUpEvent 在 HandlePickup 返回后无任何后续动作，零副作用）；
        // 未命中/异常=放行原方法（原版行为；异常时空气退化为取出兜底食材一次）。

        // ============ Web 储物背包取出/放入拦截（WebBackpack，3.8.10） ============
        //
        // 取出：ServerBackpackDispenser.HandlePickup 前缀（空手 = 队首食材型节点）。
        // 放入/盘接：ServerPlacementContainer.CanHandlePlacement + HandlePlacement 前缀
        // （CanHandlePlacement 对 Web 背包为条件覆盖——原版 CanCombine 链不认空盘）。

        private static bool ServerBackpackDispenserHandlePickupPrefix(object __instance, object _carrier, object _directionXZ)
        {
            try
            {
                var wb0 = WebBackpackStorage.FindForDispenser(__instance as Component);
                if (wb0 == null)
                {
                    var missName = __instance is Component ? ((Component)__instance).name : "?";
                    StubLog.Log("[WebBackpack] 取出前缀: 未命中背包表（原版行为）→ " + missName);
                    return true;
                }
                if (wb0.TryDispense(_carrier, _directionXZ))
                    return false;
                if (wb0.IsWorn())
                    return false;
                var compName = __instance is Component ? ((Component)__instance).name : "?";
                StubLog.Log("[WebBackpack] 取出前缀: 未背起，放行原版 → " + compName);
            }
            catch (System.Exception ex)
            {
                WarnOnce("wbPickup", "[CustomStub.Harmony] WebBackpack 取出前缀异常（放行原方法）: " + ex.Message);
            }
            return true;
        }

        /// <summary>容器组件 → WebBackpackStorage（层级无关）：先查 child 标记
        /// （背包被背起后 child 挂在厨师身上，父级查找必然落空——2026-10-12
        /// 17:46 场次放置探针实证），再退自身/父级。</summary>
        private static WebBackpackStorage FindWebBackpack(Component comp)
        {
            if (comp == null)
                return null;
            var marker = comp.GetComponent<WebBackpackChild>();
            if (marker != null && marker.storage != null)
                return marker.storage;
            var wb = comp.GetComponent<WebBackpackStorage>();
            if (wb != null)
                return wb;
            return comp.GetComponentInParent<WebBackpackStorage>();
        }

        /// <summary>ServerPlacementContainer.CanHandlePlacement 前缀（3.8.10 起为
        /// 条件覆盖）：Web 背包完全接管放置判定——原版 CanCombine 链不认「空盘
        /// 接取出」，且组合节点的合并语义与离散格位不符。非背包容器照常放行。</summary>
        private static float s_wbPlaceProbeNext;

        private static bool ServerPlacementCanHandlePlacementPrefix(object __instance, object _carrier,
            ref bool __result)
        {
            try
            {
                var comp = __instance as Component;
                if (comp == null)
                    return true;
                var wb = FindWebBackpack(comp);
                if (wb == null)
                {
                    // 交互路由探针：背包正被背着时发生的「非背包」放置交互——
                    // 若玩家本意是放进背包，说明客户端没把背包列为放置目标。
                    if (WebBackpackStorage.AnyWorn && Time.realtimeSinceStartup > s_wbPlaceProbeNext)
                    {
                        s_wbPlaceProbeNext = Time.realtimeSinceStartup + 2f;
                        var probeItem = "";
                        if (_carrier != null)
                        {
                            var inspect = _carrier.GetType().GetMethod("InspectCarriedItem",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                                null, System.Type.EmptyTypes, null);
                            var carried = inspect != null ? inspect.Invoke(_carrier, null) as GameObject : null;
                            probeItem = carried != null ? carried.name : "<空手>";
                        }
                        StubLog.Log("[WebBackpack] 放置探针: 交互→其它容器 " + comp.name
                            + "（携带=" + probeItem + "；若预期目标是背包=客户端未把背包列为放置目标）");
                    }
                    WebBackpackStorage.WireServerPlacementReferences(comp.gameObject);
                    return true;
                }
                WebBackpackStorage.WireServerPlacementReferences(comp.gameObject);
                __result = wb.CanHandlePlacementCarrier(_carrier);
                return false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("wbPlaceCan", "[CustomStub.Harmony] WebBackpack 放置判定前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        /// <summary>ServerPlacementContainer.HandlePlacement 前缀：Web 背包已背起时
        /// 走统一放置入口（空盘接 / 盘装内容搬入 / 食材放入）。</summary>
        private static bool ServerPlacementHandlePlacementPrefix(object __instance, object _carrier)
        {
            try
            {
                var comp = __instance as Component;
                if (comp == null || _carrier == null)
                    return true;
                WebBackpackStorage.WireServerPlacementReferences(comp.gameObject);
                var wb = FindWebBackpack(comp);
                if (wb == null)
                    return true;
                if (!wb.IsWorn())
                    return true;
                if (wb.TryHandlePlacementCarrier(_carrier))
                    return false;
                return true;
            }
            catch (System.Exception ex)
            {
                WarnOnce("wbPlacePre", "[CustomStub.Harmony] WebBackpack 放入前缀异常: " + ex.Message);
            }
            return true;
        }

        private static bool ServerPickupHandlePickupPrefix(object __instance)
        {
            if (!RandomCrate.HasAnyAirPending)
                return true;
            try
            {
                var crate = RandomCrate.FindAirPending(__instance as Component);
                if (crate == null)
                    return true;
                crate.OnAirTaken();
                return false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("airPickup", "[CustomStub.Harmony] 空气取出前缀异常（放行原方法=空气退化为兜底食材）: " + ex.Message);
                return true;
            }
        }

        // ============ 热水器接水门禁（Kettle，2026-10-26） ============
        //
        // 复刻原版 DLC100 ToggleablePlacementItemSpawner 的 Active 门禁 + 单杯冷却：
        //  - 前缀 ServerPlacementItemSpawner.CanHandlePlacement：热水器非 Active
        //    （未烧开水）时拒绝接水（__result=false 跳过原方法）；非热水器/无
        //    热水器场景快速放行，原版酱料机零影响。
        //  - 后缀 ServerPlacementItemSpawner.HandlePlacement：接出一杯 → 冷却
        //    （Kettle.OnServerFilled：动画 Cooldown + 倒水音效 + 复位开关）。
        //  - 后缀 ClientPlacementItemSpawner.ApplyServerEvent：客户端收到接水
        //    消息 → 本地倒水表现（服务端权威，客户端只做表现同步）。
        //  补丁随 "kettle" 特征按需安装（EntryPoint.EnsureKettlePatches）。

        /// <summary>前缀：热水器非 Active 拒绝接水。</summary>
        private static bool ServerPlacementCanHandlePrefix(object __instance, ref bool __result)
        {
            if (!Kettle.HasAny)
                return true;
            try
            {
                var k = Kettle.FindBySpawner(__instance as Component);
                if (k == null)
                    return true;
                if (!k.AllowFill)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
            catch (System.Exception ex)
            {
                WarnOnce("kettleGate", "[CustomStub.Harmony] 热水器接水门禁前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        /// <summary>后缀：服务端接出一杯 → 冷却复位。</summary>
        private static void ServerPlacementHandlePostfix(object __instance)
        {
            if (!Kettle.HasAny)
                return;
            try
            {
                var k = Kettle.FindBySpawner(__instance as Component);
                if (k != null)
                    k.OnServerFilled();
            }
            catch (System.Exception ex)
            {
                WarnOnce("kettleFill", "[CustomStub.Harmony] 热水器接水后缀异常（忽略）: " + ex.Message);
            }
        }

        /// <summary>后缀：客户端接水消息 → 倒水表现。</summary>
        private static void ClientPlacementApplyEventPostfix(object __instance)
        {
            if (!Kettle.HasAny)
                return;
            try
            {
                var k = Kettle.FindBySpawner(__instance as Component);
                if (k != null)
                    k.OnClientFillMessage();
            }
            catch (System.Exception ex)
            {
                WarnOnce("kettleFillCli", "[CustomStub.Harmony] 热水器客户端接水后缀异常（忽略）: " + ex.Message);
            }
        }

        /// <summary>供 EntryPoint 手工绑定用的前缀/后缀 MethodInfo（本程序集内部）。</summary>
        internal static System.Reflection.MethodInfo ServerPlacementCanHandlePrefixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("ServerPlacementCanHandlePrefix",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }

        internal static System.Reflection.MethodInfo ServerPlacementHandlePostfixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("ServerPlacementHandlePostfix",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }

        internal static System.Reflection.MethodInfo ClientPlacementApplyEventPostfixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("ClientPlacementApplyEventPostfix",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }

        // ============ 脏茶杯合并（DirtyTeacup，2026-10-27） ============
        //
        // 单个脏茶杯（web_teacup_dirty，tag DirtyTeacup|）放上脏茶杯堆：
        // 原版 ServerDirtyPlateStack.CanHandlePlacement 只接受携带 DirtyPlateStack
        // 的堆（单杯返回 false），HandlePlacement 对单杯会 RequireComponent
        // <ServerStack> NRE。补丁随 DirtyTeacup| tag 按需安装
        // （EntryPoint.EnsureDirtyTeacupPatches）。
        //  - 前缀 CanHandlePlacement：携带物是本族单杯 → __result=true 跳过原方法；
        //  - 前缀 HandlePlacement：单杯 → 目标堆 AddToStack + DestroyCarriedItem，
        //    跳过原方法。

        /// <summary>前缀：单杯可被脏茶杯堆接收。</summary>
        private static bool DirtyStackCanHandlePrefix(object __instance, object _carrier, ref bool __result)
        {
            try
            {
                if (DirtyTeacupMergeWatcher.MatchSingleCup(__instance as Component, _carrier) == null)
                    return true;
                __result = true;
                return false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("dirtyCupCanHandle", "[CustomStub.Harmony] 脏茶杯接收前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        /// <summary>前缀：单杯合并入堆（AddToStack + 销毁携带物），跳过原方法。</summary>
        private static bool DirtyStackHandlePrefix(object __instance, object _carrier)
        {
            try
            {
                var cup = DirtyTeacupMergeWatcher.MatchSingleCup(__instance as Component, _carrier);
                if (cup == null)
                    return true;
                var add = __instance.GetType().GetMethod("AddToStack",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (add == null)
                    return true;
                add.Invoke(__instance, null);
                var destroy = _carrier.GetType().GetMethod("DestroyCarriedItem",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (destroy != null)
                    destroy.Invoke(_carrier, null);
                StubLog.Dbg("[CustomStub.Harmony] 脏茶杯合并入堆: " + cup.name);
                return false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("dirtyCupHandle", "[CustomStub.Harmony] 脏茶杯合并前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        internal static System.Reflection.MethodInfo DirtyStackCanHandlePrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("DirtyStackCanHandlePrefix", BF); }
        }

        internal static System.Reflection.MethodInfo DirtyStackHandlePrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("DirtyStackHandlePrefix", BF); }
        }

        // ============ 单只脏茶杯直接洗（DirtyTeacup，2026-10-27） ============
        //
        // 原版 WashingStation.CanHandlePlacement 只认「携带 DirtyPlateStack 整堆」：
        // 单只脏茶杯（web_teacup_dirty 是普通可携带物，没有 DirtyPlateStack 组件）
        // 永远放不进洗杯水槽——客户端不亮高亮、服务端直接拒绝（用户反馈
        // 「洗杯台无法使用/脏杯子放不进去」）。
        //  - 前缀 CanHandlePlacement（Server + Client）：携带物是本族单杯且本槽是
        //    茶杯水槽 → __result=true 跳过原方法；
        //  - 前缀 HandlePlacement（Server）：单杯 → DestroyCarriedItem + AddPlates(1)，
        //    跳过原方法（原方法对单杯没有 ServerStack，会走到挂载分支）。
        // 2026-10-28 追加【整堆】分支：原版 HandlePlacement 对整堆先试烘干 attach 站，
        // 烘干位空时整堆被挂到烘干台上卡住（OnItemAdded 消耗逻辑不触发，用户反馈
        // 「放一堆脏茶杯只有一个进去，其他卡在水槽上面」）——整堆在本前缀直接
        // AddPlates(堆大小)+DestroyCarriedItem 消耗，跳过原方法。
        // 只对「烘干位模板是 web_teacup_stack_clean」的水槽生效，不影响其它 sink。

        private static bool WashingCanHandlePrefix(object __instance, object _carrier, ref bool __result)
        {
            try
            {
                var inst = __instance as Component;
                var single = DirtyTeacupMergeWatcher.MatchSingleCupForSink(inst, _carrier);
                var stack = DirtyTeacupMergeWatcher.MatchTeacupStackForSink(inst, _carrier);
                if (single == null && stack == null)
                {
                    // 兜底：只要目标确实是茶杯水槽、携带物确实是本族脏茶杯（单杯
                    // 或整堆），就允许直接洗涤，不依赖堆 prefab 的 washed/plate 字段。
                    var carried = DirtyTeacupMergeWatcher.InspectCarried(_carrier);
                    if (DirtyTeacupMergeWatcher.IsTeacupSink(inst) &&
                        DirtyTeacupMergeWatcher.IsDirtyTeacupItem(carried))
                    {
                        __result = true;
                        return false;
                    }
                    // 匹配失败时输出具体原因（5s 节流）——定位「脏茶杯放不进水槽」
                    DirtyTeacupMergeWatcher.LogSinkMatchFailure(inst, _carrier);
                    return true;
                }
                __result = true;
                return false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("teacupSinkCan", "[CustomStub.Harmony] 洗杯台单杯接受前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        private static bool WashingHandlePrefix(object __instance, object _carrier)
        {
            try
            {
                var inst = __instance as Component;
                if (inst == null)
                    return true;
                var addPlates = inst.GetType().GetMethod("AddPlates",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance);

                // 单杯路径：+1 待洗
                var cup = DirtyTeacupMergeWatcher.MatchSingleCupForSink(inst, _carrier);
                if (cup != null)
                {
                    if (addPlates != null)
                        addPlates.Invoke(inst, new object[] { 1 });
                    DestroyCarrierItem(_carrier);
                    StubLog.Log("[CustomStub.Harmony] 单只脏茶杯放入洗杯水槽（+1 待洗）");
                    return false;
                }

                // 整堆路径：+堆大小 待洗（不走原版烘干 attach 分支）
                var stackGo = DirtyTeacupMergeWatcher.MatchTeacupStackForSink(inst, _carrier);
                if (stackGo != null)
                {
                    int size = DirtyTeacupMergeWatcher.GetCarriedStackSize(stackGo);
                    if (size <= 0)
                        size = 1;
                    if (addPlates != null)
                        addPlates.Invoke(inst, new object[] { size });
                    DestroyCarrierItem(_carrier);
                    StubLog.Log("[CustomStub.Harmony] 整堆脏茶杯放入洗杯水槽（+" + size + " 待洗）");
                    return false;
                }

                // 最终兜底：茶杯水槽 + 本族脏茶杯（单杯/堆）直接洗涤计数，
                // 不依赖堆 prefab 的 washed/plate 字段，避免退回“挂水槽台面”。
                var carried = DirtyTeacupMergeWatcher.InspectCarried(_carrier);
                if (DirtyTeacupMergeWatcher.IsTeacupSink(inst) &&
                    DirtyTeacupMergeWatcher.IsDirtyTeacupItem(carried))
                {
                    int size = DirtyTeacupMergeWatcher.GetCarriedStackSize(carried);
                    if (size <= 0)
                        size = 1;
                    if (addPlates != null)
                        addPlates.Invoke(inst, new object[] { size });
                    DestroyCarrierItem(_carrier);
                    StubLog.Log("[CustomStub.Harmony] 脏茶杯放入洗杯水槽（兜底 +" + size + " 待洗）");
                    return false;
                }
                return true;
            }
            catch (System.Exception ex)
            {
                WarnOnce("teacupSinkHandle", "[CustomStub.Harmony] 洗杯台放置前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        private static void DestroyCarrierItem(object carrier)
        {
            var destroy = carrier.GetType().GetMethod("DestroyCarriedItem",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (destroy != null)
                destroy.Invoke(carrier, null);
        }

        internal static System.Reflection.MethodInfo WashingCanHandlePrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("WashingCanHandlePrefix", BF); }
        }

        internal static System.Reflection.MethodInfo WashingHandlePrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("WashingHandlePrefix", BF); }
        }

        // ============ 禁止脏茶杯放到茶杯水槽台面（DirtyTeacup，2026-10-28） ============
        //
        // 洗杯台台面本身是 AttachStation：原版/部分皮肤路径会让整堆脏茶杯先挂到
        // 水槽上方的 AttachStation，玩家再拿单杯逐个送洗。产品要求脏茶杯不能放到
        // 台面，必须整堆直接进洗涤计数。这里拦 AttachStation 的 CanHandle/Handle，
        // 只针对「茶杯水槽的洗涤台面/烘干位 + 本族脏茶杯」，放行干净杯/其它物品。

        private static bool TeacupAttachCanHandlePrefix(object __instance, object _iCarrier, ref bool __result)
        {
            try
            {
                var station = __instance as Component;
                if (station == null || !DirtyTeacupMergeWatcher.IsTeacupSinkAttachStation(station))
                    return true;
                var carried = DirtyTeacupMergeWatcher.InspectCarried(_iCarrier);
                if (!DirtyTeacupMergeWatcher.IsDirtyTeacupItem(carried))
                    return true;
                __result = false;
                StubLog.Log("[CustomStub.Harmony] 阻止脏茶杯放到茶杯水槽台面（改走洗涤计数）: "
                    + (carried != null ? carried.name : "?"));
                return false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("teacupAttachCan", "[CustomStub.Harmony] 茶杯水槽台面拦截前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        private static bool TeacupAttachHandlePrefix(object __instance, object _iCarrier)
        {
            try
            {
                var station = __instance as Component;
                if (station == null || !DirtyTeacupMergeWatcher.IsTeacupSinkAttachStation(station))
                    return true;
                var carried = DirtyTeacupMergeWatcher.InspectCarried(_iCarrier);
                if (!DirtyTeacupMergeWatcher.IsDirtyTeacupItem(carried))
                    return true;
                return false; // 不挂台面；交给 WashingStation 计数（若未匹配则留手上）
            }
            catch (System.Exception ex)
            {
                WarnOnce("teacupAttachHandle", "[CustomStub.Harmony] 茶杯水槽台面放置前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        internal static System.Reflection.MethodInfo TeacupAttachCanHandlePrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("TeacupAttachCanHandlePrefix", BF); }
        }

        internal static System.Reflection.MethodInfo TeacupAttachHandlePrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("TeacupAttachHandlePrefix", BF); }
        }

        // ============ 玩家放置动作直达茶杯水槽（DirtyTeacup，2026-10-28） ============
        //
        // 不依赖放置目标最终选中 AttachStation 还是 WashingStation：玩家按放置时
        // 只要目标属于茶杯水槽且手里是本族脏茶杯（单杯/整堆），服务端直接
        // AddPlates(全部数量) + DestroyCarriedItem；AddPlates 会广播
        // WashingStationMessage（客户端刷新 m_plateCount/脏杯模型），手里整堆消失。

        private static bool PlaceHeldItemServerPrefix(object _control, object _target)
        {
            try
            {
                var targetGo = _target as GameObject;
                var controlComp = _control as Component;
                if (targetGo == null || controlComp == null)
                    return true;
                var washingType = GameApi.Find("WashingStation");
                var serverWashingType = GameApi.Find("ServerWashingStation");
                if (washingType == null || serverWashingType == null)
                    return true;
                Component washing = targetGo.GetComponentInParent(washingType);
                if (washing == null)
                    washing = targetGo.GetComponentInChildren(washingType);
                if (washing == null)
                {
                    var sinkRoot = FindTeacupSinkRoot(targetGo.transform);
                    if (sinkRoot != null)
                        washing = sinkRoot.GetComponentInChildren(washingType);
                }
                if (washing == null || !DirtyTeacupMergeWatcher.IsTeacupSink(washing))
                    return true;
                var serverWashing = washing.GetComponent(serverWashingType);
                if (serverWashing == null)
                    return true;
                // 载体解析：与原生 PlaceHeldItem_Server 保持一致
                // （_control.gameObject.RequireInterface<ICarrier>() == GetComponent(ICarrier)）。
                // 若挑到「另一侧」的载体，客户端 DestroyCarriedItem 只做本地 Object.Destroy，
                // 服务端实体仍在注册表里，世界里的杯堆会残留（用户反馈现象）。
                string carrierInfo = "";
                Component carrier = null;
                var icarrierType = GameApi.Find("ICarrier");
                if (icarrierType != null)
                {
                    carrier = controlComp.gameObject.GetComponent(icarrierType);
                    if (carrier != null)
                        carrierInfo = carrier.GetType().Name + "(原生接口)";
                }
                if (carrier == null)
                {
                    var serverCarrierType = GameApi.Find("ServerPlayerAttachmentCarrier");
                    if (serverCarrierType != null)
                    {
                        carrier = controlComp.gameObject.GetComponent(serverCarrierType);
                        if (carrier != null)
                            carrierInfo = carrier.GetType().Name + "(服务端指定)";
                    }
                }
                if (carrier == null && icarrierType != null)
                {
                    carrier = controlComp.gameObject.GetComponentInChildren(icarrierType);
                    if (carrier != null)
                        carrierInfo = carrier.GetType().Name + "(子物体)";
                }
                if (carrier == null)
                {
                    var clientCarrierType = GameApi.Find("ClientPlayerAttachmentCarrier");
                    if (clientCarrierType != null)
                    {
                        carrier = controlComp.gameObject.GetComponent(clientCarrierType);
                        if (carrier != null)
                            carrierInfo = carrier.GetType().Name + "(客户端兜底)";
                    }
                }
                if (carrier == null)
                    return true;
                var carried = DirtyTeacupMergeWatcher.InspectCarried(carrier);
                if (!DirtyTeacupMergeWatcher.IsDirtyTeacupItem(carried))
                    return true;
                var addPlates = serverWashing.GetType().GetMethod("AddPlates",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance);
                if (addPlates == null)
                    return true;
                int size = DirtyTeacupMergeWatcher.GetCarriedStackSize(carried);
                if (size <= 0)
                    size = 1;
                addPlates.Invoke(serverWashing, new object[] { size });
                var destroyFlags = System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance;
                var destroy = carrier.GetType().GetMethod("DestroyCarriedItem", destroyFlags);
                if (destroy != null)
                    destroy.Invoke(carrier, null);
                string leftover = "";
                // 编辑器 Play/主机单机：客户端载体只在本地做预测，服务端 DestroyCarriedItem
                // 清的是服务端侧；客户端手里若还挂着同一件，模型会留在世界里
                //（用户反馈「整堆放下去了，其他杯子还在」）。
                try
                {
                    var clientCarrierType = GameApi.Find("ClientPlayerAttachmentCarrier");
                    if (clientCarrierType != null)
                    {
                        var clientCarrier = controlComp.gameObject.GetComponent(clientCarrierType);
                        if (clientCarrier != null && clientCarrier != carrier)
                        {
                            var clientCarried = DirtyTeacupMergeWatcher.InspectCarried(clientCarrier);
                            if (clientCarried != null && clientCarried == carried)
                            {
                                var clientDestroy = clientCarrier.GetType().GetMethod("DestroyCarriedItem", destroyFlags);
                                if (clientDestroy != null)
                                {
                                    clientDestroy.Invoke(clientCarrier, null);
                                    leftover += " 客户端载体已清";
                                }
                            }
                        }
                    }
                }
                catch (System.Exception exClient)
                {
                    WarnOnce("teacupPlaceClientCarrier",
                        "[CustomStub.Harmony] 客户端载体清理异常: " + exClient.Message);
                }
                // 销毁后复核：手里还留着东西 = 载体选错或客户端预测未清，
                // 会表现成「整堆进去了，世界里还留一堆」。
                try
                {
                    var stillCarried = DirtyTeacupMergeWatcher.InspectCarried(carrier);
                    if (stillCarried != null)
                        leftover += " ⚠服务端载体销毁后仍在手上: " + stillCarried.name
                            + "(active=" + stillCarried.activeSelf + ")";
                }
                catch (System.Exception exLeft)
                {
                    leftover += " ⚠复核异常: " + exLeft.Message;
                }
                // 兜底：本地 Object.Destroy ≠ 网络销毁。若销毁后实体仍然存活
                //（客户端载体路径的典型表现），改走 NetworkUtils.DestroyObject
                // 广播 DestroyEntity，世界里的杯堆不会残留。
                try
                {
                    if (carried != null && carried.activeInHierarchy)
                    {
                        var netUtils = GameApi.Find("NetworkUtils");
                        var destroyObject = netUtils != null
                            ? netUtils.GetMethod("DestroyObject",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                            : null;
                        if (destroyObject != null)
                        {
                            destroyObject.Invoke(null, new object[] { carried });
                            leftover += " 已走 DestroyObject 兜底";
                        }
                    }
                }
                catch (System.Exception exFallback)
                {
                    leftover += " ⚠DestroyObject 兜底异常: " + exFallback.Message;
                }
                StubLog.Log("[CustomStub.Harmony] 玩家放置动作：脏茶杯直接进洗杯水槽（+"
                    + size + " 待洗） 载体=" + carrierInfo + leftover
                    + " → " + SinkDirtyPlateVisibility.Describe(washing));
                return false; // 跳过原 PlaceHeldItem_Server（不再走 AttachStation）
            }
            catch (System.Exception ex)
            {
                WarnOnce("teacupPlaceDirect", "[CustomStub.Harmony] 脏茶杯直达洗杯水槽前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        private static GameObject FindTeacupSinkRoot(Transform t)
        {
            while (t != null)
            {
                if (t.GetComponent("TeacupSinkRebuilder") != null)
                    return t.gameObject;
                if (!string.IsNullOrEmpty(t.name) &&
                    t.name.StartsWith("web_sink_teacup", System.StringComparison.Ordinal))
                    return t.gameObject;
                t = t.parent;
            }
            return null;
        }

        internal static System.Reflection.MethodInfo PlaceHeldItemServerPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("PlaceHeldItemServerPrefix", BF); }
        }

        // ============ 脏茶杯同步诊断（2026-10-28） ============
        //
        // 背景：服务端 AddPlates(+5) 日志已确认，但编辑器 Play 里水槽只显示 1 个脏杯
        // （用户反馈）。两侧同时打点即可区分两种病因：
        //   · 服务端 AddPlates 后计数没涨   → 打到了错的 WashingStation 实例；
        //   · 服务端涨了、客户端 ApplyServerEvent 没收到 AddPlates → 同步/镜像问题；
        //   · 两侧都是 5、已显示=3         → 纯显示层（TeacupSinkRebuilder 槽位）问题。
        // 只在茶杯水槽上打点，且 InteractionState（每帧刷新的进度消息）不记，避免刷屏。

        private static System.Reflection.FieldInfo s_wsMsgTypeField;
        private static System.Reflection.FieldInfo s_wsMsgCountField;

        /// <summary>ServerWashingStation.AddPlates(int) 之后：打印权威计数并即时刷新脏杯显隐。</summary>
        private static void AddPlatesTracePostfix(object __instance, int plateCount)
        {
            try
            {
                var washing = ResolveWashingStation(__instance as Component);
                if (washing == null || !DirtyTeacupMergeWatcher.IsTeacupSink(washing))
                    return;
                // 1 -> 1 个、2 -> 2 个、>=3 -> 3 个：服务端权威计数落地后立刻显示，
                // 不等客户端消息（单机 Play 里服务端计数就是最终事实）。
                SinkDirtyPlateVisibility.ApplyNow(washing);
                StubLog.Log("[CustomStub.Harmony] 服务端 AddPlates(+" + plateCount + ") 后 → "
                    + SinkDirtyPlateVisibility.Describe(washing));
            }
            catch (System.Exception ex)
            {
                WarnOnce("teacupAddPlatesTrace", "[CustomStub.Harmony] AddPlates 诊断异常: " + ex.Message);
            }
        }

        /// <summary>ClientWashingStation.ApplyServerEvent 之后：客户端按消息落地再校正一次。</summary>
        private static void ClientWashingEventPostfix(object __instance, object serialisable)
        {
            try
            {
                var washing = ResolveWashingStation(__instance as Component);
                if (washing == null || !DirtyTeacupMergeWatcher.IsTeacupSink(washing))
                    return;
                SinkDirtyPlateVisibility.ApplyNow(washing);
            }
            catch (System.Exception ex)
            {
                WarnOnce("teacupClientMsgPost", "[CustomStub.Harmony] 客户端洗杯消息后校正异常: " + ex.Message);
            }
        }

        /// <summary>ClientWashingStation.ApplyServerEvent：确认 AddPlates/CleanedPlate 是否送达客户端。</summary>
        private static void ClientWashingEventPrefix(object __instance, object serialisable)
        {
            try
            {
                if (serialisable == null)
                    return;
                var msgType = serialisable.GetType();
                if (msgType.Name != "WashingStationMessage")
                    return;
                if (s_wsMsgTypeField == null || s_wsMsgCountField == null)
                {
                    s_wsMsgTypeField = msgType.GetField("m_msgType",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance);
                    s_wsMsgCountField = msgType.GetField("m_plateCount",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance);
                    if (s_wsMsgTypeField == null || s_wsMsgCountField == null)
                        return;
                }
                // InteractionState = 0（交互进度，每帧都发）；只记 AddPlates=1 / CleanedPlate=2。
                int type = System.Convert.ToInt32(s_wsMsgTypeField.GetValue(serialisable),
                    System.Globalization.CultureInfo.InvariantCulture);
                if (type == 0)
                    return;
                var washing = ResolveWashingStation(__instance as Component);
                if (washing == null || !DirtyTeacupMergeWatcher.IsTeacupSink(washing))
                    return;
                int count = System.Convert.ToInt32(s_wsMsgCountField.GetValue(serialisable),
                    System.Globalization.CultureInfo.InvariantCulture);
                StubLog.Log("[CustomStub.Harmony] 客户端收到洗杯消息 "
                    + (type == 1 ? "AddPlates" : "CleanedPlate") + "(计数=" + count + ") → "
                    + SinkDirtyPlateVisibility.Describe(washing));
            }
            catch (System.Exception ex)
            {
                WarnOnce("teacupClientMsgTrace", "[CustomStub.Harmony] 客户端洗杯消息诊断异常: " + ex.Message);
            }
        }

        /// <summary>把 Server/ClientWashingStation 组件解析成同物体的 WashingStation。</summary>
        private static Component ResolveWashingStation(Component comp)
        {
            if (comp == null)
                return null;
            var washingType = GameApi.Find("WashingStation");
            if (washingType == null)
                return null;
            return comp.gameObject.GetComponent(washingType) as Component;
        }

        internal static System.Reflection.MethodInfo AddPlatesTracePostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("AddPlatesTracePostfix", BF); }
        }

        internal static System.Reflection.MethodInfo ClientWashingEventPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ClientWashingEventPrefix", BF); }
        }

        internal static System.Reflection.MethodInfo ClientWashingEventPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ClientWashingEventPostfix", BF); }
        }

        // ============ 堆完整性守卫（2026-10-27 茶杯堆拾取/脏堆渲染崩溃事故） ============
        //
        // 背景：编辑器 Play 下 CleanPlateStackSeeder 的预览杯会被宿主
        // ServerStack.StartSynchronising 按 tag 收进私有 m_stackItems；预览随后被
        // 销毁（seeder 补杯成功后 DestroyPreviews / 物理清理），列表留下失效引用——
        // 宿主 RemoveFromStack 对失效项 RequestInterface<IAttachment>() 直接抛
        // MissingReferenceException（拾取即崩）。DirtyPlateStackCosmetics 在
        // Start/PlateChanged 里缓存子物体 Renderer，预览销毁不走 OnPlateRemoved
        // 回调，Update 里 SetPropertyBlock 打到失效 Renderer 同样崩。
        // Assets/Scripts 不可改，此处用前缀在宿主入口先剔除失效项：
        //   · ServerStack.GetSize/InspectTopOfStack/AddToStack/RemoveFromStack
        //     四入口统一剔除（下游 ServerPlateStackBase/ServerCleanPlateStack 全部
        //     经由 m_stack 这些方法取数，自动受益）；
        //   · ServerCleanPlateStack.HandlePickup：剔除后为空则跳过原方法（否则
        //     override RemoveFromStack 对 null 调 RequestComponent + CarryItem(null)）；
        //   · DirtyPlateStackCosmetics.Update：m_renderers 含失效项先重建缓存。
        // 反射目标缺失/异常 = WarnOnce 后放行原方法（原版行为兜底）。

        private static System.Reflection.FieldInfo s_stackItemsField;
        private static System.Reflection.FieldInfo s_cosRenderersField;
        private static System.Reflection.MethodInfo s_cosUpdateRenderersMethod;

        /// <summary>剔除 ServerStack.m_stackItems 里的失效项并返回剔除后数量；
        /// -1 = 探测/清理失败（调用方放行原方法兜底）。</summary>
        private static int PurgeAndCountStackItems(object serverStack)
        {
            try
            {
                if (s_stackItemsField == null)
                {
                    s_stackItemsField = serverStack.GetType().GetField("m_stackItems",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (s_stackItemsField == null)
                        WarnOnce("stackItemsField", "[CustomStub.Harmony] ServerStack.m_stackItems 反射缺失，堆失效项剔除不可用");
                }
                if (s_stackItemsField == null)
                    return -1;
                var list = s_stackItemsField.GetValue(serverStack)
                    as System.Collections.Generic.List<GameObject>;
                if (list == null)
                    return -1;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i] == null)
                        list.RemoveAt(i);
                }
                return list.Count;
            }
            catch (System.Exception ex)
            {
                WarnOnce("stackPurgeEx", "[CustomStub.Harmony] 堆失效项剔除异常（放行原方法）: " + ex.Message);
                return -1;
            }
        }

        /// <summary>ServerStack 四入口统一前缀：先剔除失效堆内项。</summary>
        private static void ServerStackPurgePrefix(object __instance)
        {
            PurgeAndCountStackItems(__instance);
        }

        /// <summary>ServerCleanPlateStack.HandlePickup 前缀：剔除失效项后堆空则
        /// 跳过原方法（宿主 override RemoveFromStack 会对 null 调 RequestComponent，
        /// 且 CarryItem(null) 无防护）。</summary>
        private static bool CleanStackHandlePickupGuardPrefix(object __instance)
        {
            var comp = __instance as Component;
            if (comp == null)
                return true;
            var stack = comp.GetComponent("ServerStack");
            if (stack == null)
                return true;
            int count = PurgeAndCountStackItems(stack);
            if (count != 0)
                return true;    // 有货放行；-1 = 探测失败同样放行兜底
            WarnOnce("cleanPickupEmpty", "[CustomStub.Harmony] 拦截空净杯堆拾取：失效堆内项已剔除，本次拾取忽略");
            return false;
        }

        /// <summary>DirtyPlateStackCosmetics.Update 前缀：渲染缓存含失效项时先调
        /// 私有 UpdateRenderers() 重建再放行（宿主仅 OnPlateAdded/Removed 刷新缓存，
        /// seeder 预览销毁不走该回调）。</summary>
        private static void DirtyCosmeticsPurgeRenderersPrefix(object __instance)
        {
            try
            {
                if (s_cosRenderersField == null)
                {
                    var t = __instance.GetType();
                    s_cosRenderersField = t.GetField("m_renderers",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    s_cosUpdateRenderersMethod = t.GetMethod("UpdateRenderers",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (s_cosRenderersField == null || s_cosUpdateRenderersMethod == null)
                        WarnOnce("cosFields", "[CustomStub.Harmony] DirtyPlateStackCosmetics 反射缺失，渲染缓存自愈不可用");
                }
                if (s_cosRenderersField == null || s_cosUpdateRenderersMethod == null)
                    return;
                var renderers = s_cosRenderersField.GetValue(__instance) as Renderer[];
                if (renderers == null)
                    return;
                bool dead = false;
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null)
                    {
                        dead = true;
                        break;
                    }
                }
                if (dead)
                    s_cosUpdateRenderersMethod.Invoke(__instance, null);
            }
            catch (System.Exception ex)
            {
                WarnOnce("cosPurgeEx", "[CustomStub.Harmony] 脏杯堆渲染缓存自愈异常（放行原方法）: " + ex.Message);
            }
        }

        internal static System.Reflection.MethodInfo ServerStackPurgePrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerStackPurgePrefix", BF); }
        }

        internal static System.Reflection.MethodInfo CleanStackHandlePickupGuardPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("CleanStackHandlePickupGuardPrefix", BF); }
        }

        internal static System.Reflection.MethodInfo DirtyCosmeticsPurgeRenderersPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("DirtyCosmeticsPurgeRenderersPrefix", BF); }
        }

        // ============ 附着站悬空引用守卫（2026-10-27 放置即崩事故） ============
        //
        // 背景：ServerAttachStation.m_item 指向的物品被销毁（脏杯合并/丢弃/服务端
        // 销毁）后引用残留，CanHandlePlacement 等入口 `m_item.AccessGameObject()`
        // 抛 MissingReferenceException（21:55 事故栈，ServerAttachStation.cs:149）。
        // 宿主侧上一届用源码补丁 HasLiveItem() 解决；Assets/Scripts 禁改后移植为
        // 此前缀：所有读 m_item 的公共入口 + 每帧 UpdateSynchronising 先做假 null
        // 检查，失效则清空引用（与上届补丁语义一致）。ClientAttachStation 同理。

        private static System.Reflection.FieldInfo s_attachItemField;
        private static System.Type s_attachItemFieldType;

        /// <summary>附着站 m_item（IAttachment/IClientAttachment，实现均为 Component）
        /// 假 null 检查，失效则置 null。</summary>
        private static void PurgeAttachStationItem(object station)
        {
            try
            {
                var t = station.GetType();
                if (s_attachItemField == null || s_attachItemFieldType != t)
                {
                    s_attachItemField = t.GetField("m_item",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    s_attachItemFieldType = t;
                    if (s_attachItemField == null)
                        WarnOnce("attachItemField:" + t.Name, "[CustomStub.Harmony] " + t.Name + ".m_item 反射缺失，附着站悬空引用清理不可用");
                }
                if (s_attachItemField == null)
                    return;
                var uo = s_attachItemField.GetValue(station) as UnityEngine.Object;
                if (!ReferenceEquals(uo, null) && uo == null)
                {
                    s_attachItemField.SetValue(station, null);
                    WarnOnce("attachItemPurged:" + t.Name, "[CustomStub.Harmony] " + t.Name + " 引用物品已销毁，悬空引用已清空（防放置/拾取 MissingReferenceException）");
                }
            }
            catch (System.Exception ex)
            {
                WarnOnce("attachPurgeEx", "[CustomStub.Harmony] 附着站悬空引用清理异常（放行原方法）: " + ex.Message);
            }
        }

        /// <summary>Server/ClientAttachStation 各入口统一前缀：先清悬空 m_item。</summary>
        private static void AttachStationPurgePrefix(object __instance)
        {
            PurgeAttachStationItem(__instance);
        }

        internal static System.Reflection.MethodInfo AttachStationPurgePrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("AttachStationPurgePrefix", BF); }
        }

        // ============ 高亮失效引用守卫（2026-10-27 靠近工作台卡死事故） ============
        //
        // 背景：ClientAnticipateInteractionHighlight.m_children / m_renderers 缓存
        // 子物体（堆上杯盘）引用，子物体被销毁（合并/洗杯/服务端销毁）后残留——
        // IncrementHighlight 递归进已销毁子物体 / Highlight 遍历失效 Renderer 即抛
        // MissingReferenceException，并让 PlayerControls.Update 每帧中断（21:39/
        // 21:41 事故栈：FindRenderers←RebuildHighlightMaterials←Highlight←
        // IncrementHighlight 递归）。上届源码补丁逐方法加守卫；禁改后移植为前缀：
        //   · Increment/DecrementHighlight：先剔除 m_children 失效项；
        //   · Highlight/Unhighlight/RebuildHighlightMaterials：平行剔除
        //     m_renderers + m_startingBrightness 失效索引；m_highlight 假 null 时
        //     Highlight/Rebuild 跳过（Unhighlight 不读 m_highlight，剔除后放行）；
        //   · OnItemAttachmentAdded/Removed：attachment 假 null 跳过。
        // OnDestroy 自摘除不移植——后果已被「用时剔除」覆盖。

        private static System.Reflection.FieldInfo s_hlChildrenField;
        private static System.Reflection.FieldInfo s_hlRenderersField;
        private static System.Reflection.FieldInfo s_hlBrightnessField;
        private static System.Reflection.FieldInfo s_hlHighlightField;
        private static System.Reflection.MethodInfo s_hlSaveBrightness;
        private static bool s_hlFieldsMissing;

        private static bool EnsureHighlightFields(object inst)
        {
            if (s_hlFieldsMissing)
                return false;
            if (s_hlChildrenField != null)
                return true;
            var t = inst.GetType();
            System.Reflection.BindingFlags f = System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance;
            s_hlChildrenField = t.GetField("m_children", f);
            s_hlRenderersField = t.GetField("m_renderers", f);
            s_hlBrightnessField = t.GetField("m_startingBrightness", f);
            s_hlHighlightField = t.GetField("m_highlight", f);
            s_hlSaveBrightness = t.GetMethod("SaveRenderersStartingBrightness", f);
            if (s_hlChildrenField == null || s_hlRenderersField == null
                || s_hlBrightnessField == null || s_hlHighlightField == null)
            {
                WarnOnce("hlFields", "[CustomStub.Harmony] 高亮组件字段反射缺失，失效引用剔除不可用");
                s_hlFieldsMissing = true;
                return false;
            }
            return true;
        }

        /// <summary>Increment/DecrementHighlight 前缀：剔除 m_children 失效项
        /// （宿主正序遍历+递归，失效项会让递归目标即崩）。</summary>
        private static void AntHighlightChildPurgePrefix(object __instance)
        {
            try
            {
                if (!EnsureHighlightFields(__instance))
                    return;
                var children = s_hlChildrenField.GetValue(__instance) as System.Collections.IList;
                if (children == null)
                    return;
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    var uo = children[i] as UnityEngine.Object;
                    if (!ReferenceEquals(uo, null) && uo == null)
                        children.RemoveAt(i);
                }
            }
            catch (System.Exception ex)
            {
                WarnOnce("hlChildPurgeEx", "[CustomStub.Harmony] 高亮子列表剔除异常（放行原方法）: " + ex.Message);
            }
        }

        /// <summary>Highlight/Unhighlight/RebuildHighlightMaterials 前缀：平行剔除
        /// m_renderers 与 m_startingBrightness 的失效索引（两结构按下标一一对应，
        /// 必须同步剔除）；亮度数组错位时调私有 SaveRenderersStartingBrightness 重建。
        /// m_highlight 假 null 时 Highlight/Rebuild 跳过（宿主会 deref 它）。</summary>
        private static bool AntHighlightRenderersPurgePrefix(object __instance,
            System.Reflection.MethodBase __originalMethod)
        {
            try
            {
                if (!EnsureHighlightFields(__instance))
                    return true;
                var hl = s_hlHighlightField.GetValue(__instance) as UnityEngine.Object;
                bool hlDead = !ReferenceEquals(hl, null) && hl == null;
                if (hlDead && __originalMethod != null
                    && (__originalMethod.Name == "Highlight" || __originalMethod.Name == "RebuildHighlightMaterials"))
                    return false;
                var renderers = s_hlRenderersField.GetValue(__instance)
                    as System.Collections.Generic.List<Renderer>;
                if (renderers == null || renderers.Count == 0)
                    return true;
                var brightness = s_hlBrightnessField.GetValue(__instance) as float[][];
                bool aligned = brightness != null && brightness.Length == renderers.Count;
                var keptRenderers = new System.Collections.Generic.List<Renderer>(renderers.Count);
                var keptBrightness = aligned ? new System.Collections.Generic.List<float[]>(brightness.Length) : null;
                for (int i = 0; i < renderers.Count; i++)
                {
                    if (renderers[i] == null)
                        continue;
                    keptRenderers.Add(renderers[i]);
                    if (keptBrightness != null)
                        keptBrightness.Add(brightness[i]);
                }
                if (keptRenderers.Count == renderers.Count)
                    return true;    // 无失效项，零开销放行
                renderers.Clear();
                renderers.AddRange(keptRenderers);
                if (keptBrightness != null)
                    s_hlBrightnessField.SetValue(__instance, keptBrightness.ToArray());
                else if (s_hlSaveBrightness != null)
                    s_hlSaveBrightness.Invoke(__instance, null);    // 错位兜底：重新采样
                return true;
            }
            catch (System.Exception ex)
            {
                WarnOnce("hlRendererPurgeEx", "[CustomStub.Harmony] 高亮渲染缓存剔除异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        /// <summary>OnItemAttachmentAdded/Removed 前缀：attachment 假 null 跳过
        /// （宿主会 attachment.AccessGameObject() 直接崩）。参数名须与宿主一致。</summary>
        private static bool AntHighlightAttachmentGuardPrefix(object attachment)
        {
            var uo = attachment as UnityEngine.Object;
            if (!ReferenceEquals(uo, null) && uo == null)
                return false;
            return true;
        }

        internal static System.Reflection.MethodInfo AntHighlightChildPurgePrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("AntHighlightChildPurgePrefix", BF); }
        }

        internal static System.Reflection.MethodInfo AntHighlightRenderersPurgePrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("AntHighlightRenderersPurgePrefix", BF); }
        }

        internal static System.Reflection.MethodInfo AntHighlightAttachmentGuardPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("AntHighlightAttachmentGuardPrefix", BF); }
        }

        // ============ 杯盘半成品回落（2026-10-27，移植自被禁改的 Plate/GameUtils 补丁） ============
        //
        // 原版 Plate.CanPlaceOnPlate 只在「放进去后恰好等于某个配方」时放行，
        // 空杯放牛奶/蜂蜜、热水+牛奶 等半成品组合被弹回。补丁版在配方匹配失败时
        // 回落 GameUtils.CanStillFormOrder（同 step 配方真子集 → 放行）。
        // 移植难点：原方法早退路径（无 TransferBehaviour/不可转移/订单修改特例）
        // 同样返回 false，回落只该在「已转移但配方不匹配」时触发——用前后缀
        // __state 快照内容数：内容数变化 = 走过了 TransferToContainer。

        private static System.Reflection.MethodInfo s_plateGetContents;
        private static System.Type s_plateGetContentsType;
        private static System.Reflection.MethodInfo s_plateGetComposition;
        private static System.Reflection.FieldInfo s_plateStepField;

        /// <summary>IIngredientContents.GetContents() 反射取数，失败 -1。</summary>
        private static int SnapshotContentsCount(object adapter)
        {
            if (adapter == null)
                return -1;
            var t = adapter.GetType();
            if (s_plateGetContents == null || s_plateGetContentsType != t)
            {
                s_plateGetContents = t.GetMethod("GetContents",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                s_plateGetContentsType = t;
                if (s_plateGetContents == null)
                    WarnOnce("plateGetContents", "[CustomStub.Harmony] IIngredientContents.GetContents 反射缺失，半成品回落不可用");
            }
            if (s_plateGetContents == null)
                return -1;
            var arr = s_plateGetContents.Invoke(adapter, null) as System.Array;
            return arr != null ? arr.Length : -1;
        }

        /// <summary>Plate.CanPlaceOnPlate 前缀：快照盘内内容数（__state 传给后缀）。</summary>
        private static void CanPlaceOnPlateFallbackPrefix(object _ingredientContentsAdapter, out int __state)
        {
            __state = -1;
            try
            {
                __state = SnapshotContentsCount(_ingredientContentsAdapter);
            }
            catch (System.Exception ex)
            {
                WarnOnce("plateSnapEx", "[CustomStub.Harmony] 半成品回落快照异常: " + ex.Message);
            }
        }

        /// <summary>Plate.CanPlaceOnPlate 后缀：原判 false 且内容数变化（=已转移、
        /// 仅配方匹配失败）时，用 PlatePartialOrderFallback 复刻补丁回落逻辑。</summary>
        private static void CanPlaceOnPlateFallbackPostfix(object __instance,
            object _ingredientContentsAdapter, int __state, ref bool __result)
        {
            if (__result || __state < 0)
                return;
            try
            {
                int now = SnapshotContentsCount(_ingredientContentsAdapter);
                if (now < 0 || now == __state)
                    return;    // 早退路径（未发生转移）——保持原版拒绝
                var comp = __instance as Component;
                if (comp == null)
                    return;
                if (s_plateGetComposition == null)
                    s_plateGetComposition = comp.GetType().GetMethod("GetOrderComposition",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (s_plateStepField == null)
                    s_plateStepField = comp.GetType().GetField("m_platingStep",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (s_plateGetComposition == null || s_plateStepField == null)
                {
                    WarnOnce("plateMembers", "[CustomStub.Harmony] Plate.GetOrderComposition/m_platingStep 反射缺失，半成品回落不可用");
                    return;
                }
                var contents = s_plateGetContents.Invoke(_ingredientContentsAdapter, null);
                var composition = s_plateGetComposition.Invoke(comp, new object[] { contents });
                var step = s_plateStepField.GetValue(comp);
                if (PlatePartialOrderFallback.CanStillFormOrder(composition, step))
                {
                    __result = true;
                    StubLog.Dbg("[CustomStub.Harmony] 半成品组合按回落判定放行入杯/盘");
                }
            }
            catch (System.Exception ex)
            {
                WarnOnce("plateFallbackEx", "[CustomStub.Harmony] 半成品回落后缀异常（保持原判）: " + ex.Message);
            }
        }

        internal static System.Reflection.MethodInfo CanPlaceOnPlateFallbackPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("CanPlaceOnPlateFallbackPrefix", BF); }
        }

        internal static System.Reflection.MethodInfo CanPlaceOnPlateFallbackPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("CanPlaceOnPlateFallbackPostfix", BF); }
        }

        // ============ 空炮发射拦截（CannonGuard，2026-09-19 真机事故） ============
        //
        // 目标：ServerCannon.OnTrigger(string)。空炮发射（m_loadedObject 为 null
        // 或玩家已脱离 AttachPoint——宿主 Unload 不清该字段）= m_flying 永久 true +
        // 客户端 ClientCannon.LaunchProjectile(null) 在 transform.SetParent 处 NRE，
        // 协程死在 EndCannonRoutine 之前 → ServerCannonSessionInteractable.
        // CanInteract(!IsFlying()) 永远 false，大炮从此拒入（原版软锁）。
        // 命中=跳过原方法（拦截一次无效发射，零副作用）；未命中/异常=放行原方法。

        /// <summary>ServerCannon.OnTrigger 前缀（判定委托 CannonGuard.AllowLaunch）。</summary>
        private static bool ServerCannonOnTriggerPrefix(object __instance, string _trigger)
        {
            try
            {
                return CannonGuard.AllowLaunch(__instance, _trigger);
            }
            catch (System.Exception ex)
            {
                WarnOnce("cannonLaunch", "[CustomStub.Harmony] 大炮发射前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        // ============ 网络实体扫描锚点（v14，联机 ID 错位修复） ============
        //
        // MultiplayerController.ScanEntities 每关只调一次（冷方法，detour 零热路径
        // 开销），是主客机唯一确定性一致的「发令时刻」：两台机器都在收到
        // GameState.ScanNetworkEntities 时把可移动火锅同步装配完，扫描遍历到的层级
        // 才会完全相同、实体 ID 才会对齐。
        //
        // 前缀必须绝对不抛：它跑在关卡加载主流程上，异常会把整关卡加载打断。

        /// <summary>MultiplayerController.ScanEntities 前缀：扫描开始前把所有
        /// 可移动火锅装配完，并记录发令时刻快照。</summary>
        private static void MultiplayerScanEntitiesPrefix()
        {
            try
            {
                int total, assembled;
                PushablePot.FlushPendingAssemblies(out total, out assembled);
                WebBackpackStorage.FlushPendingAssemblies();
                if (total > 0)
                    NetDiagnostics.OnScanEntitiesBegin(total, assembled);
            }
            catch (System.Exception ex)
            {
                WarnOnce("scanPrefix", "[CustomStub.Harmony] 扫描前预装配异常（放行扫描）: " + ex);
            }
        }

        /// <summary>MultiplayerController.StartSynchronisation 前缀：输出实体指纹
        /// 与逐锅注册自检（扫描已结束、StartSynchronisingEntry 尚未跑 = 纯净快照）。</summary>
        private static void MultiplayerStartSynchronisationPrefix()
        {
            try
            {
                NetDiagnostics.OnStartSynchronisation();
            }
            catch (System.Exception ex)
            {
                WarnOnce("startSyncPrefix", "[CustomStub.Harmony] 同步启动诊断异常（忽略）: " + ex.Message);
            }
        }

        /// <summary>ClientKitchenLoader.ScannedEntities 前缀（无参 = 不依赖宿主参数名）。</summary>
        private static void KitchenScannedEntitiesPrefix()
        {
            try
            {
                NetDiagnostics.OnScanCompleted();
            }
            catch (System.Exception ex)
            {
                WarnOnce("kitchenScanned", "[CustomStub.Harmony] 扫描完成打点异常（忽略）: " + ex.Message);
            }
        }

        /// <summary>ClientKitchenLoader.StartEntities 前缀。</summary>
        private static void KitchenStartEntitiesPrefix()
        {
            try
            {
                NetDiagnostics.OnEntitiesStarted();
            }
            catch (System.Exception ex)
            {
                WarnOnce("kitchenStarted", "[CustomStub.Harmony] 实体启动打点异常（忽略）: " + ex.Message);
            }
        }

        /// <summary>供 EntryPoint 手工绑定用的前缀 MethodInfo（本程序集内部）。</summary>
        internal static System.Reflection.MethodInfo RespawnColliderObjectAddedPrefixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("RespawnColliderObjectAddedPrefix",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }

        internal static System.Reflection.MethodInfo MultiplayerScanEntitiesPrefixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("MultiplayerScanEntitiesPrefix",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }

        internal static System.Reflection.MethodInfo MultiplayerStartSynchronisationPrefixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("MultiplayerStartSynchronisationPrefix",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }

        internal static System.Reflection.MethodInfo KitchenScannedEntitiesPrefixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("KitchenScannedEntitiesPrefix",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }

        internal static System.Reflection.MethodInfo KitchenStartEntitiesPrefixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("KitchenStartEntitiesPrefix",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }

        internal static System.Reflection.MethodInfo ServerPickupHandlePickupPrefixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("ServerPickupHandlePickupPrefix",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }

        internal static System.Reflection.MethodInfo ServerBackpackDispenserHandlePickupPrefixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("ServerBackpackDispenserHandlePickupPrefix",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }

        internal static System.Reflection.MethodInfo ServerPlacementCanHandlePlacementPrefixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("ServerPlacementCanHandlePlacementPrefix",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }

        internal static System.Reflection.MethodInfo ServerPlacementHandlePlacementPrefixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("ServerPlacementHandlePlacementPrefix",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }

        internal static System.Reflection.MethodInfo ServerCannonOnTriggerPrefixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("ServerCannonOnTriggerPrefix",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }

        // ============ 锅具时间参数（煮糊/过度混合 ≠ 2× 时的阈值接管） ============
        //
        // 设计：无 UtensilTiming / 对应值 <= 0 一律放行原方法（原版行为，零开销）；
        // 煮熟/混合阈值本身已由 UtensilTiming 直接写进 handler 公有字段，无需拦截。

        /// <summary>CookingHandler.GetCookedOrderState(float) 前缀：煮糊阈值接管。
        /// __result = CookingProgress 枚举（Raw=0/Cooked=1/Burnt=2，按名取值）。</summary>
        private static bool CookingHandlerGetCookedStatePrefix(object __instance, float _cookingProgress, ref object __result)
        {
            if (!UtensilTiming.HasAnyActive)
                return true;
            try
            {
                var handler = __instance as Component;
                var timing = UtensilTiming.Find(handler);
                if (timing == null || timing.m_burnTime <= 0f || GameApi.CookingProgressEnum == null)
                    return true;
                float cook = ReadFloat(handler, GameApi.CookingTimeField);
                __result = System.Enum.ToObject(GameApi.CookingProgressEnum,
                    _cookingProgress > timing.m_burnTime ? 2 : (_cookingProgress > cook ? 1 : 0));
                return false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("cookedState", "[Harmony] GetCookedOrderState 前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        /// <summary>MixingHandler.GetMixedOrderState(float) 前缀：过度混合阈值接管。
        /// __result = MixingProgress 枚举（Unmixed=0/Mixed=1/OverMixed=2）。</summary>
        private static bool MixingHandlerGetMixedStatePrefix(object __instance, float _mixingProgress, ref object __result)
        {
            if (!UtensilTiming.HasAnyActive)
                return true;
            try
            {
                var handler = __instance as Component;
                var timing = UtensilTiming.Find(handler);
                if (timing == null || timing.m_overMixTime <= 0f || GameApi.MixingProgressEnum == null)
                    return true;
                float mix = ReadFloat(handler, GameApi.MixingTimeField);
                __result = System.Enum.ToObject(GameApi.MixingProgressEnum,
                    _mixingProgress > timing.m_overMixTime ? 2 : (_mixingProgress >= mix ? 1 : 0));
                return false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("mixedState", "[Harmony] GetMixedOrderState 前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        /// <summary>ServerCookingHandler.IsBurning() 前缀（火警/停止烹饪判定）。</summary>
        private static bool ServerCookingIsBurningPrefix(object __instance, ref bool __result)
        {
            if (!UtensilTiming.HasAnyActive)
                return true;
            try
            {
                var comp = __instance as Component;
                var timing = UtensilTiming.Find(comp);
                if (timing == null || timing.m_burnTime <= 0f || GameApi.ServerGetCookingProgressMethod == null)
                    return true;
                float progress = InvokeFloat(comp, GameApi.ServerGetCookingProgressMethod);
                __result = progress > timing.m_burnTime;
                return false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("serverBurning", "[Harmony] ServerCooking.IsBurning 前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        /// <summary>ClientCookingHandler.IsBurning() 前缀（客户端烧糊追踪）。</summary>
        private static bool ClientCookingIsBurningPrefix(object __instance, ref bool __result)
        {
            if (!UtensilTiming.HasAnyActive)
                return true;
            try
            {
                var comp = __instance as Component;
                var timing = UtensilTiming.Find(comp);
                if (timing == null || timing.m_burnTime <= 0f || GameApi.ClientGetCookingProgressMethod == null)
                    return true;
                float progress = InvokeFloat(comp, GameApi.ClientGetCookingProgressMethod);
                __result = progress > timing.m_burnTime;
                return false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("clientBurning", "[Harmony] ClientCooking.IsBurning 前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        /// <summary>ServerMixingHandler.IsOverMixed() 前缀。</summary>
        private static bool ServerMixingIsOverMixedPrefix(object __instance, ref bool __result)
        {
            if (!UtensilTiming.HasAnyActive)
                return true;
            try
            {
                var comp = __instance as Component;
                var timing = UtensilTiming.Find(comp);
                if (timing == null || timing.m_overMixTime <= 0f || GameApi.ServerGetMixingProgressMethod == null)
                    return true;
                float progress = InvokeFloat(comp, GameApi.ServerGetMixingProgressMethod);
                __result = progress > timing.m_overMixTime;
                return false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("serverOverMixed", "[Harmony] ServerMixing.IsOverMixed 前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        /// <summary>ClientMixingHandler.IsOverMixed() 前缀。</summary>
        private static bool ClientMixingIsOverMixedPrefix(object __instance, ref bool __result)
        {
            if (!UtensilTiming.HasAnyActive)
                return true;
            try
            {
                var comp = __instance as Component;
                var timing = UtensilTiming.Find(comp);
                if (timing == null || timing.m_overMixTime <= 0f || GameApi.ClientGetMixingProgressMethod == null)
                    return true;
                float progress = InvokeFloat(comp, GameApi.ClientGetMixingProgressMethod);
                __result = progress > timing.m_overMixTime;
                return false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("clientOverMixed", "[Harmony] ClientMixing.IsOverMixed 前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        /// <summary>ServerCookingHandler.SetCookingProgress(float) 后缀：修正视觉状态机。
        /// 原版 OverDoing 预警阈值 1.3×cook 与 Ruined 判定按 2×cook 硬编码；煮糊独立
        /// 配置时按比例重算（预警起点 = cook + 0.3×(burn−cook)，与原版 1.3× 语义对齐：
        /// burn=2×cook 时完全等价），变化时补发状态回调。</summary>
        private static void ServerCookingSetProgressPostfix(object __instance)
        {
            if (!UtensilTiming.HasAnyActive)
                return;
            try
            {
                var comp = __instance as Component;
                var timing = UtensilTiming.Find(comp);
                if (timing == null || timing.m_burnTime <= 0f)
                    return;
                var data = GameApi.ServerCookingDataField != null ? GameApi.ServerCookingDataField.GetValue(comp) : null;
                if (data == null)
                    return;
                float progress = ToFloat(GameApi.CookingMsgProgressField.GetValue(data));
                object state = GameApi.CookingMsgStateField.GetValue(data);
                float cook = ReadTimeOn(comp, GameApi.CookingHandlerType, GameApi.CookingTimeField);
                float burn = timing.m_burnTime;
                float overThreshold = cook + 0.3f * (burn - cook);
                object desired;
                if (progress > burn)
                    desired = UiState(4); // Ruined
                else if (progress >= cook)
                    desired = progress > overThreshold ? UiState(3) /* OverDoing */
                        : (ToInt(state) == 1 ? UiState(2) /* Completed */ : state); // Progressing → Completed
                else
                    desired = UiState(1); // Progressing
                if (desired != null && state != null && ToInt(desired) != ToInt(state))
                {
                    GameApi.CookingMsgStateField.SetValue(data, desired);
                    InvokeCallback(GameApi.ServerCookingStateChangedField.GetValue(comp), desired);
                }
            }
            catch (System.Exception ex)
            {
                WarnOnce("setCookingProgress", "[Harmony] SetCookingProgress 修正异常（放行原状态）: " + ex.Message);
            }
        }

        /// <summary>ServerMixingHandler.SetMixingProgress(float) 后缀：同烹饪侧。</summary>
        private static void ServerMixingSetProgressPostfix(object __instance)
        {
            if (!UtensilTiming.HasAnyActive)
                return;
            try
            {
                var comp = __instance as Component;
                var timing = UtensilTiming.Find(comp);
                if (timing == null || timing.m_overMixTime <= 0f)
                    return;
                var data = GameApi.ServerMixingDataField != null ? GameApi.ServerMixingDataField.GetValue(comp) : null;
                if (data == null)
                    return;
                float progress = ToFloat(GameApi.MixingMsgProgressField.GetValue(data));
                object state = GameApi.MixingMsgStateField.GetValue(data);
                float mix = ReadTimeOn(comp, GameApi.MixingHandlerType, GameApi.MixingTimeField);
                float over = timing.m_overMixTime;
                float overThreshold = mix + 0.3f * (over - mix);
                object desired;
                if (progress > over)
                    desired = UiState(4);
                else if (progress >= mix)
                    desired = progress > overThreshold ? UiState(3)
                        : (ToInt(state) == 1 ? UiState(2) : state);
                else
                    desired = UiState(1);
                if (desired != null && state != null && ToInt(desired) != ToInt(state))
                {
                    GameApi.MixingMsgStateField.SetValue(data, desired);
                    InvokeCallback(GameApi.ServerMixingStateChangedField.GetValue(comp), desired);
                }
            }
            catch (System.Exception ex)
            {
                WarnOnce("setMixingProgress", "[Harmony] SetMixingProgress 修正异常（放行原状态）: " + ex.Message);
            }
        }

        /// <summary>ClientCookingHandler.ApplyServerUpdate 后缀：重发正确的 OverDoing
        /// 进度条换算（原版按 2×cook 收尾，独立煮糊时橙色预警区错位）。</summary>
        private static void ClientCookingApplyUpdatePostfix(object __instance)
        {
            if (!UtensilTiming.HasAnyActive)
                return;
            try
            {
                var comp = __instance as Component;
                var timing = UtensilTiming.Find(comp);
                if (timing == null || timing.m_burnTime <= 0f)
                    return;
                var gui = GameApi.ClientCookingGuiField != null ? GameApi.ClientCookingGuiField.GetValue(comp) : null;
                if (gui == null || GameApi.UiSetOverDoingMethod == null)
                    return;
                float progress = InvokeFloat(comp, GameApi.ClientGetCookingProgressMethod);
                if (progress <= 0.001f)
                    return;
                float cook = ReadTimeOn(comp, GameApi.CookingHandlerType, GameApi.CookingTimeField);
                float burn = timing.m_burnTime;
                float amount = Remap01(progress, cook, Mathf.Clamp(burn - 0.5f, cook, burn));
                GameApi.UiSetOverDoingMethod.Invoke(gui, new object[] { amount });
            }
            catch (System.Exception ex)
            {
                WarnOnce("clientCookingUI", "[Harmony] ClientCooking UI 修正异常: " + ex.Message);
            }
        }

        /// <summary>ClientMixingHandler.ApplyServerUpdate 后缀：同烹饪侧。</summary>
        private static void ClientMixingApplyUpdatePostfix(object __instance)
        {
            if (!UtensilTiming.HasAnyActive)
                return;
            try
            {
                var comp = __instance as Component;
                var timing = UtensilTiming.Find(comp);
                if (timing == null || timing.m_overMixTime <= 0f)
                    return;
                var ui = GameApi.ClientMixingUiField != null ? GameApi.ClientMixingUiField.GetValue(comp) : null;
                if (ui == null || GameApi.UiSetOverDoingMethod == null)
                    return;
                float progress = InvokeFloat(comp, GameApi.ClientGetMixingProgressMethod);
                if (progress <= 0.001f)
                    return;
                float mix = ReadTimeOn(comp, GameApi.MixingHandlerType, GameApi.MixingTimeField);
                float over = timing.m_overMixTime;
                float amount = Remap01(progress, mix, Mathf.Clamp(over - 0.5f, mix, over));
                GameApi.UiSetOverDoingMethod.Invoke(ui, new object[] { amount });
            }
            catch (System.Exception ex)
            {
                WarnOnce("clientMixingUI", "[Harmony] ClientMixing UI 修正异常: " + ex.Message);
            }
        }

        // ---- 小工具 ----

        /// <summary>在同步组件（Server/Client Handler）物体上读真实 handler 的公有字段。
        /// 字段声明在 CookingHandler/MixingHandler 上，不能直接 GetValue 同步组件。</summary>
        private static float ReadTimeOn(Component comp, System.Type handlerType, System.Reflection.FieldInfo field)
        {
            if (comp == null || handlerType == null || field == null)
                return 0f;
            var handler = comp.GetComponent(handlerType);
            if (handler == null)
                return 0f;
            return ToFloat(field.GetValue(handler));
        }

        private static float ReadFloat(Component comp, System.Reflection.FieldInfo field)
        {
            if (comp == null || field == null)
                return 0f;
            return ToFloat(field.GetValue(comp));
        }

        private static float ToFloat(object raw)
        {
            return raw is float ? (float)raw : 0f;
        }

        private static int ToInt(object enumValue)
        {
            return enumValue == null ? -1 : System.Convert.ToInt32(enumValue);
        }

        private static object UiState(int value)
        {
            return GameApi.UIStateEnum != null ? System.Enum.ToObject(GameApi.UIStateEnum, value) : null;
        }

        private static float InvokeFloat(Component comp, System.Reflection.MethodInfo method)
        {
            if (comp == null || method == null)
                return 0f;
            return ToFloat(method.Invoke(comp, null));
        }

        /// <summary>MathUtils.ClampedRemap 反射（失败回落手写钳位线性）。</summary>
        private static float Remap01(float value, float a, float b)
        {
            if (GameApi.ClampedRemapMethod != null)
                return ToFloat(GameApi.ClampedRemapMethod.Invoke(null, new object[] { value, a, b, 0f, 1f }));
            if (b <= a)
                return value >= b ? 1f : 0f;
            float t = (value - a) / (b - a);
            return t < 0f ? 0f : (t > 1f ? 1f : t);
        }

        private static void InvokeCallback(object callback, object state)
        {
            var del = callback as System.Delegate;
            if (del == null)
                return;
            try
            {
                del.DynamicInvoke(state);
            }
            catch (System.Exception ex)
            {
                WarnOnce("stateCallback", "[Harmony] 状态回调补发异常: " + ex.Message);
            }
        }

        // ---- 供 EntryPoint 手工绑定的 MethodInfo 表（名称 → 前缀/后缀方法） ----

        internal static System.Reflection.MethodInfo CookingGetCookedStatePrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("CookingHandlerGetCookedStatePrefix", BF); }
        }

        internal static System.Reflection.MethodInfo MixingGetMixedStatePrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("MixingHandlerGetMixedStatePrefix", BF); }
        }

        internal static System.Reflection.MethodInfo ServerCookingIsBurningPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerCookingIsBurningPrefix", BF); }
        }

        internal static System.Reflection.MethodInfo ClientCookingIsBurningPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ClientCookingIsBurningPrefix", BF); }
        }

        internal static System.Reflection.MethodInfo ServerMixingIsOverMixedPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerMixingIsOverMixedPrefix", BF); }
        }

        internal static System.Reflection.MethodInfo ClientMixingIsOverMixedPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ClientMixingIsOverMixedPrefix", BF); }
        }

        internal static System.Reflection.MethodInfo ServerCookingSetProgressPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerCookingSetProgressPostfix", BF); }
        }

        internal static System.Reflection.MethodInfo ServerMixingSetProgressPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerMixingSetProgressPostfix", BF); }
        }

        internal static System.Reflection.MethodInfo ClientCookingApplyUpdatePostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ClientCookingApplyUpdatePostfix", BF); }
        }

        internal static System.Reflection.MethodInfo ClientMixingApplyUpdatePostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ClientMixingApplyUpdatePostfix", BF); }
        }

        // ============ 节点环步数对账（3.6.0 联机偶发丢步修复） ============
        //
        // 背景：按钮链每一步 = 主机 ServerTimedQueue 广播 TimedQueueMessage(index=0)，
        // 全端 ClientTimedQueue 到点 SetTrigger(BLPress)。网络突刺/时钟追赶把两条消息
        // 挤进同一帧时，两次 SetTrigger 被 Mecanim 单值 Trigger 合并为一次过渡 →
        // 该端永久少转一步（差 90°）。postfix 只把消息/执行计数转发给组根上的
        // NodeRingSync（注册表 O(1) 寻址、零分配），由其对账并瞬跳补步——
        // 非节点环实体（无 NodeRingSync）首行早退，零开销。

        /// <summary>ClientTimedQueue.ApplyServerEvent 后缀：QueueEvent(index==0)
        /// = 权威步数 +1；Cancel = 权威回退对齐本地。</summary>
        private static void ClientTimedQueueApplyServerEventPostfix(object __instance, object serialisable)
        {
            try
            {
                var comp = __instance as Component;
                if (comp == null || serialisable == null)
                    return;
                var ring = NodeRingSync.FindFor(comp);
                if (ring == null)
                    return; // 非节点环实体：零开销早退
                if (GameApi.TimedQueueMsgTypeField == null || GameApi.TimedQueueMsgIndexField == null)
                    return;
                var msgType = GameApi.TimedQueueMsgTypeField.GetValue(serialisable);
                if (msgType != null && msgType.ToString() == "QueueEvent")
                {
                    var boxed = GameApi.TimedQueueMsgIndexField.GetValue(serialisable);
                    ring.OnAuthoritativeEvent(boxed is int ? (int)boxed : -1);
                }
                else
                {
                    ring.OnAuthoritativeCancel();
                }
            }
            catch (System.Exception ex)
            {
                WarnOnce("ringEvent", "[CustomStub.Harmony] 节点环对账事件后缀异常（跳过=不补步）: " + ex.Message);
            }
        }

        /// <summary>ClientTriggerQueue.DoEvent 后缀：SetTrigger 执行计数（诊断——
        /// SetTrigger 次数 &gt; 本地步数 = 同帧合并丢步的直接证据）。</summary>
        private static void ClientTriggerQueueDoEventPostfix(object __instance, int _index)
        {
            try
            {
                var comp = __instance as Component;
                if (comp == null)
                    return;
                var ring = NodeRingSync.FindFor(comp);
                if (ring != null)
                    ring.OnTriggerFired(_index);
            }
            catch (System.Exception ex)
            {
                WarnOnce("ringFire", "[CustomStub.Harmony] 节点环触发计数后缀异常（忽略）: " + ex.Message);
            }
        }

        internal static System.Reflection.MethodInfo ClientTimedQueueApplyServerEventPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ClientTimedQueueApplyServerEventPostfix", BF); }
        }

        internal static System.Reflection.MethodInfo ClientTriggerQueueDoEventPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ClientTriggerQueueDoEventPostfix", BF); }
        }

        // ---- 饮料/酱料机 PseudoPrefabDispenser.Setup 前缀（无 ItemCrateCosmeticDecisions 时跳过） ----
        // 写回路径会给饮料机挂 DispenserStub + 派生运行时；宿主 Setup 按食材箱画箱盖 UV，
        // child 无 PickupItemSpawner / ItemCrateCosmeticDecisions → NRE 中断 ReloadPseudoAssetsFull。

        private static System.Type s_itemCrateCosmeticDecisionsType;

        private static bool PseudoPrefabDispenserSetupPrefix(object __instance)
        {
            try
            {
                if (__instance == null)
                    return true;
                var childField = __instance.GetType().GetField("childGameObject",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (childField == null)
                    return true;
                var child = childField.GetValue(__instance) as GameObject;
                if (child == null)
                    return true;
                if (s_itemCrateCosmeticDecisionsType == null)
                    s_itemCrateCosmeticDecisionsType = GameApi.Find("ItemCrateCosmeticDecisions");
                if (s_itemCrateCosmeticDecisionsType == null)
                    return true;
                if (GameApi.GetComponent(child, s_itemCrateCosmeticDecisionsType) == null)
                    return false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("dispSetup", "[CustomStub.Harmony] PseudoPrefabDispenser.Setup 前缀异常（放行原方法）: " + ex.Message);
            }
            return true;
        }

        internal static System.Reflection.MethodInfo PseudoPrefabDispenserSetupPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("PseudoPrefabDispenserSetupPrefix", BF); }
        }

        // ---- 双 stub 自愈（2026-10-06 真机卡加载事故） ----
        // OC2DIYLevel.PseudoPrefabManager.ResetAllPseudoPrefabs 前缀：在其 AddComponent
        // 循环之前，把「exact 基础 PseudoPrefabStub + 派生 stub 共存」物体上的基础
        // stub 移除——否则 PseudoPrefab.Awake 的 GetComponent<PseudoPrefabStub>() 命中
        // 组件序第一的基础 stub，派生 Setup 开头强转 (派生Stub) 抛 InvalidCastException
        // 中断整个初始化链（关卡永久卡加载）。官方图没有 PseudoPrefabManagerStub，
        // 本前缀零调用零开销；存量坏包（diantang / jia_level_2_1 等）免重导出。

        /// <summary>双 stub 自愈前缀（不拦截原方法）。重复调用幂等；每次进 web 关卡
        /// 都会全场景扫一遍 stub 组件（一次性成本，非每帧）。</summary>
        private static void DualStubHealPrefix()
        {
            try
            {
                var stubs = UnityEngine.Object.FindObjectsOfType<LevelEditorStub.PseudoPrefabStub>();
                var seen = new System.Collections.Generic.HashSet<GameObject>();
                int fixedCount = 0;
                foreach (var stub in stubs)
                {
                    if (stub == null)
                        continue;
                    var go = stub.gameObject;
                    if (!seen.Add(go))
                        continue;
                    if (HealDualStubObject(go))
                        fixedCount++;
                }
                if (fixedCount > 0)
                    StubLog.Log("[CustomStub] 双 stub 自愈: " + fixedCount
                        + " 个道具已移除基础 stub（真机卡加载修复；建议用新版编辑器重导出根治）");
            }
            catch (System.Exception ex)
            {
                WarnOnce("dualStub", "[CustomStub.Harmony] 双 stub 自愈前缀异常（放行原方法）: " + ex.Message);
            }
        }

        /// <summary>物体上存在恰好为基类的 PseudoPrefabStub 且存在派生 stub →
        ///  DestroyImmediate 基础 stub（场景加载窗口内立即生效，先于 AddComponent 循环）；
        ///  分发器生成食材为空时回落 soArray 首项（否则派生 Setup 读空 NRE）。
        ///  返回是否修改。</summary>
        private static bool HealDualStubObject(GameObject go)
        {
            LevelEditorStub.PseudoPrefabStub baseStub = null;
            bool hasDerived = false;
            foreach (var s in go.GetComponents<LevelEditorStub.PseudoPrefabStub>())
            {
                if (s == null)
                    continue;
                if (s.GetType() == typeof(LevelEditorStub.PseudoPrefabStub))
                    baseStub = s;
                else
                    hasDerived = true;
            }
            if (baseStub == null || !hasDerived)
                return false;
            var dispenser = go.GetComponent<LevelEditorStub.PseudoPrefabDispenserStub>();
            if (dispenser != null && dispenser.spawnerItemPrefabSO == null)
            {
                var arr = go.GetComponent<LevelEditorStub.PseudoPrefabSOArray>();
                if (arr != null && arr.pseudoPrefabSOs != null && arr.pseudoPrefabSOs.Length > 0)
                    dispenser.spawnerItemPrefabSO = arr.pseudoPrefabSOs[0];
            }
            // 派生 stub 的 pseudoPrefabSO 为空时先继承基础 SO：旧包（只补了派生
            // stub 没补运行时）在真机由 OC2DIYLevel 按派生分类挂运行时后，Setup
            // 会读空 SO → LoadAsset(null) NRE（茶杯回收台不回流/卡加载）。
            if (baseStub.pseudoPrefabSO != null)
            {
                foreach (var s in go.GetComponents<LevelEditorStub.PseudoPrefabStub>())
                {
                    if (s == null || s == baseStub)
                        continue;
                    if (s.pseudoPrefabSO == null)
                        s.pseudoPrefabSO = baseStub.pseudoPrefabSO;
                }
            }
            UnityEngine.Object.DestroyImmediate(baseStub);
            StubLog.Log("[CustomStub] 双 stub 自愈: " + go.name
                + " 移除基础 stub（派生 stub 保留，OC2DIYLevel 将按派生分类挂运行时）");
            return true;
        }

        internal static System.Reflection.MethodInfo DualStubHealPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("DualStubHealPrefix", BF); }
        }

        // ============ 摇杆遥控地板（AnimPilotFloorDrive，布局 pilot 组） ============

        private static bool ServerPilotMovementUpdatePrefix(object __instance)
        {
            try
            {
                return AnimPilotFloorDrive.ServerUpdatePrefix(__instance);
            }
            catch (System.Exception ex)
            {
                WarnOnce("animPilotUpd", "[CustomStub.Harmony] 摇杆地板 Update 前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        private static void ServerPilotMovementUpdatePostfix(object __instance)
        {
            try
            {
                AnimPilotFloorDrive.ServerUpdatePostfix(__instance);
            }
            catch (System.Exception ex)
            {
                WarnOnce("animPilotUpdPf", "[CustomStub.Harmony] 摇杆地板 Update 后缀异常: " + ex.Message);
            }
        }

        private static void ServerPilotMovementStartPostfix(object __instance)
        {
            try
            {
                AnimPilotFloorDrive.ServerStartPostfix(__instance);
            }
            catch (System.Exception ex)
            {
                WarnOnce("animPilotStart", "[CustomStub.Harmony] 摇杆地板 Start 后缀异常: " + ex.Message);
            }
        }

        private static void ServerPilotMovementAssignPostfix(object __instance, object _controlScheme)
        {
            try
            {
                AnimPilotFloorDrive.ServerAssignPostfix(__instance, _controlScheme);
            }
            catch (System.Exception ex)
            {
                WarnOnce("animPilotAssign", "[CustomStub.Harmony] 摇杆地板 Assign 后缀异常: " + ex.Message);
            }
        }

        private static void ClientPilotMovementAssignAvatarPostfix(object __instance, GameObject _avatar)
        {
            try
            {
                AnimPilotFloorDrive.ClientAssignAvatarPostfix(__instance, _avatar);
            }
            catch (System.Exception ex)
            {
                WarnOnce("animPilotAvatar", "[CustomStub.Harmony] 摇杆地板 AssignAvatar 后缀异常: " + ex.Message);
            }
        }

        internal static System.Reflection.MethodInfo ServerPilotMovementUpdatePrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerPilotMovementUpdatePrefix", BF); }
        }

        internal static System.Reflection.MethodInfo ServerPilotMovementUpdatePostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerPilotMovementUpdatePostfix", BF); }
        }

        internal static System.Reflection.MethodInfo ServerPilotMovementStartPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerPilotMovementStartPostfix", BF); }
        }

        internal static System.Reflection.MethodInfo ServerPilotMovementAssignPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerPilotMovementAssignPostfix", BF); }
        }

        internal static System.Reflection.MethodInfo ClientPilotMovementAssignAvatarPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ClientPilotMovementAssignAvatarPostfix", BF); }
        }

        private static void ClientInteractableCanInteractPostfix(object __instance, object _player, ref bool __result)
        {
            if (__result)
                return;
            try
            {
                AnimPilotJoystickInteract.ApplyProximityCanInteract(__instance as Component, _player as Component,
                    ref __result);
                AnimPilotMemberInteract.ApplyProximityCanInteract(__instance as Component, _player as Component,
                    ref __result);
            }
            catch (System.Exception ex)
            {
                WarnOnce("pilotJoyCan", "[CustomStub.Harmony] 摇杆邻近 CanInteract 后缀异常: " + ex.Message);
            }
        }

        private static void ServerInteractableCanInteractPostfix(object __instance, object _player, ref bool __result)
        {
            if (__result)
                return;
            try
            {
                AnimPilotJoystickInteract.ApplyProximityCanInteract(__instance as Component, _player as Component,
                    ref __result);
                AnimPilotMemberInteract.ApplyProximityCanInteract(__instance as Component, _player as Component,
                    ref __result);
            }
            catch (System.Exception ex)
            {
                WarnOnce("pilotJoyCanSrv", "[CustomStub.Harmony] 摇杆邻近 CanInteract(服) 后缀异常: " + ex.Message);
            }
        }

        private static void ServerSessionInteractableCanInteractPostfix(object __instance, object _interacter,
            ref bool __result)
        {
            if (__result)
                return;
            try
            {
                AnimPilotJoystickInteract.ApplyProximityCanInteract(__instance as Component, _interacter as Component,
                    ref __result);
                AnimPilotMemberInteract.ApplyProximityCanInteract(__instance as Component, _interacter as Component,
                    ref __result);
            }
            catch (System.Exception ex)
            {
                WarnOnce("pilotJoyCanSes", "[CustomStub.Harmony] 摇杆邻近 CanInteract(会话服) 后缀异常: " + ex.Message);
            }
        }

        private static void PlayerControlsSetInteractionObjectsPostfix(object __instance, object _newInteractionObjects)
        {
            try
            {
                AnimPilotJoystickInteract.TryInjectPilotJoystickInteractionObjects(__instance as Component,
                    _newInteractionObjects);
                AnimPilotMemberInteract.TryInjectPilotMemberInteractionObjects(__instance as Component,
                    _newInteractionObjects);
            }
            catch (System.Exception ex)
            {
                WarnOnce("pilotJoyIo", "[CustomStub.Harmony] 摇杆邻近 InteractionObjects 后缀异常: " + ex.Message);
            }
        }

        private static void ClientPlayerControlsUpdateInteractPostfix(object __instance, float _deltaTime,
            bool isUsePressed, bool justPressed)
        {
            try
            {
                AnimPilotJoystickInteract.TryTriggerNearbyPilotJoystickOnUsePressed(__instance as Component,
                    justPressed);
                AnimPilotMemberInteract.TryTriggerNearbyPilotMemberOnUsePressed(__instance as Component,
                    justPressed);
            }
            catch (System.Exception ex)
            {
                WarnOnce("pilotJoyUse", "[CustomStub.Harmony] 摇杆邻近按交互兜底异常: " + ex.Message);
            }
        }

        internal static System.Reflection.MethodInfo ClientInteractableCanInteractPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ClientInteractableCanInteractPostfix", BF); }
        }

        internal static System.Reflection.MethodInfo ServerInteractableCanInteractPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerInteractableCanInteractPostfix", BF); }
        }

        internal static System.Reflection.MethodInfo ServerSessionInteractableCanInteractPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerSessionInteractableCanInteractPostfix", BF); }
        }

        internal static System.Reflection.MethodInfo PlayerControlsSetInteractionObjectsPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("PlayerControlsSetInteractionObjectsPostfix", BF); }
        }

        internal static System.Reflection.MethodInfo ClientPlayerControlsUpdateInteractPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ClientPlayerControlsUpdateInteractPostfix", BF); }
        }

        // ============ 容器转移（ContainerTransferSync，接替 Patch ServerPlate/Prep） ============

        private static bool ServerPlateCanTransferPrefix(object __instance, object _container, ref bool __result)
        {
            return ContainerTransferSync.ServerPlateCanTransferPrefix(__instance, _container, ref __result);
        }

        private static bool ServerPrepCanTransferPrefix(object __instance, object _container, ref bool __result)
        {
            return ContainerTransferSync.ServerPrepCanTransferPrefix(__instance, _container, ref __result);
        }

        private static bool ServerPrepTransferPrefix(object __instance, object _carrier, object _container, bool _dontRemove)
        {
            return ContainerTransferSync.ServerPrepTransferPrefix(__instance, _carrier, _container, _dontRemove);
        }

        internal static System.Reflection.MethodInfo ServerPlateCanTransferPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerPlateCanTransferPrefix", BF); }
        }

        internal static System.Reflection.MethodInfo ServerPrepCanTransferPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerPrepCanTransferPrefix", BF); }
        }

        internal static System.Reflection.MethodInfo ServerPrepTransferPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerPrepTransferPrefix", BF); }
        }

        private static bool PrepGetOrderDefinitionPrefix(object __instance, GameObject _carriedItem,
            object _itemContainer, ref object __result)
        {
            return ContainerTransferSync.PrepGetOrderDefinitionPrefix(__instance, _carriedItem, _itemContainer, ref __result);
        }

        private static void ServerPrepHandlePlacementPostfix(object __instance, object _carrier)
        {
            ContainerTransferSync.ServerPrepHandlePlacementPostfix(__instance, _carrier);
        }

        internal static System.Reflection.MethodInfo PrepGetOrderDefinitionPrefixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("PrepGetOrderDefinitionPrefix", BF); }
        }

        internal static System.Reflection.MethodInfo ServerPrepHandlePlacementPostfixMethod
        {
            get { return typeof(HarmonyPatches).GetMethod("ServerPrepHandlePlacementPostfix", BF); }
        }

        private const System.Reflection.BindingFlags BF =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
    }
}
