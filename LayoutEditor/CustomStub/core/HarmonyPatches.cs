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

        /// <summary>供 EntryPoint 手工绑定用的前缀MethodInfo（本程序集内部）。</summary>
        internal static System.Reflection.MethodInfo RespawnColliderObjectAddedPrefixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("RespawnColliderObjectAddedPrefix",
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

        private const System.Reflection.BindingFlags BF =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
    }
}
