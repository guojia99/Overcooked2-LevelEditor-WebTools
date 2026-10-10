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
                    PushableVoidFall.DetachPlayerFromVoidFallPots(_gameObject);
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

        /// <summary>ServerPickupItemSpawner.HandlePickup 前缀。</summary>
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

        private const System.Reflection.BindingFlags BF =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
    }
}
