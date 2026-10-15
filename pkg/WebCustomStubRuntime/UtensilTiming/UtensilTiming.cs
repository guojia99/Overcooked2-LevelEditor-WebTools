using System;
using System.Globalization;
using LevelEditorStub;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 锅具时间配置载体·运行时标记（挂在真实锅具实例上）。
    ///
    /// 数据源（权威）：伪 prefab 包装上的 tag "UtensilTiming|cook,burn,mix,over"
    /// （SpecificPseudoPrefabTag.prefabTag，写回时由 LayoutEditorStubIO 写入）。
    /// ——不用 UtensilTimingConfig 组件做查找：多个关卡集程序集各有一份同名类，
    /// GetComponent&lt;T&gt; 按程序集类型匹配会 miss（跨集场景实测教训）；
    /// SpecificPseudoPrefabTag 来自 LevelEditorStub，全 AppDomain 唯一类型。
    /// 组件仍会烘焙/自愈（Inspector 可见性），但不参与运行时查找。
    ///
    /// 应用方式（StubTicker 每 ~0.5s 扫一次，配置完成后早停；见 EntryPoint.StubTicker）：
    ///  1. 扫场景全部 CookingHandler/MixingHandler（游戏原生类型，反射）；
    ///  2. 沿祖先链找 "UtensilTiming|" tag——锅具实例在宿主实例化后挂在包装下；
    ///  3. cook/mix > 0 → 直接写 CookingHandler.m_cookingtime /
    ///     MixingHandler.m_mixingTime（public 字段，零补丁生效）；
    ///  4. burn/overMix > 0 → 在锅具上挂本组件，Harmony 前缀（HarmonyPatches）
    ///     拦截阈值判定方法改写「煮糊 = 2×煮熟」硬编码；未配置的锅具零开销放行。
    ///
    /// 阈值语义（与原版一致）：progress &gt; burn → Burnt/Ruined；
    /// progress &gt; cook → Cooked/Mixed。
    /// </summary>
    public class UtensilTiming : MonoBehaviour
    {
        /// <summary>煮熟秒数（0 = 未配置；生效值以 CookingHandler.m_cookingtime 为准）。</summary>
        public float m_cookTime;

        /// <summary>煮糊秒数（0 = 默认 2× 煮熟）。&gt;0 时由 Harmony 前缀接管。</summary>
        public float m_burnTime;

        /// <summary>混合完成秒数（0 = 未配置）。</summary>
        public float m_mixTime;

        /// <summary>过度混合秒数（0 = 默认 2× 混合）。</summary>
        public float m_overMixTime;

        private static bool s_loggedSelfCheck;
        private static bool s_scanComplete;
        private static int s_ticksSinceScene;
        private const int MinTicksBeforeEarlyStop = 3;

        /// <summary>扫描绝对上限（tick 数；ticker 30 帧一次 ⇒ ~2Hz ⇒ 约 8s）。</summary>
        private const int MaxTicksBeforeForceStop = 16;

        /// <summary>连续无新应用（applied==0）达此 tick 数则放弃未匹配配置并早停。</summary>
        private const int MaxTicksNoProgressBeforeAbandon = 6;

        private const string CloneSuffix = "(Clone)";

        /// <summary>全场景是否存在生效中的阈值接管配置（burn/overMix > 0）。
        /// HarmonyPatches 十个热路径前缀/后缀的首行快速放行依据（v5 性能优化）：
        /// false = 前缀直接放行原方法（零 GetComponent/装箱）。置位路径：
        /// 场景组件 OnEnable（烘焙残留）、Tick 扫到 burn/over>0 的 tag、ApplyValues。
        /// 场景切换时清零重估（见 OnSceneChanged；重估必然早于任何一次有效阈值
        /// 判定——煮糊/过混是秒级过程，Tick 0.5s 内即置位）。</summary>
        private static bool s_hasAnyActive;

        private static System.Collections.Generic.List<WrapperConfig> s_cachedWrappers;
        private static Component[] s_cachedCookingHandlers;
        private static Component[] s_cachedMixingHandlers;
        private static int s_ticksWithZeroProgress;
        private static int s_lastTickApplied;

        internal static bool HasAnyActive
        {
            get { return s_hasAnyActive; }
        }

        private void OnEnable()
        {
            if (m_burnTime > 0f || m_overMixTime > 0f)
                s_hasAnyActive = true;
        }

        internal static void OnSceneChanged()
        {
            s_scanComplete = false;
            s_ticksSinceScene = 0;
            s_ticksWithZeroProgress = 0;
            s_lastTickApplied = 0;
            s_prevPrinted = "";
            s_hasAnyActive = false;
            s_applyFailWarned.Clear();
            s_cachedWrappers = null;
            s_cachedCookingHandlers = null;
            s_cachedMixingHandlers = null;
        }

        internal static bool IsScanComplete()
        {
            return s_scanComplete;
        }

        /// <summary>组件查找缓存入口：游戏组件所在物体上的 UtensilTiming（无则 null）。</summary>
        internal static UtensilTiming Find(Component gameComponent)
        {
            if (gameComponent == null)
                return null;
            return gameComponent.GetComponent<UtensilTiming>();
        }

        /// <summary>按显式数值应用（PushablePot 装配 / 通用入口）。
        /// cook/mix 直接写 handler 公有字段；burn/overMix &gt; 0 时挂本组件。</summary>
        internal static void ApplyValues(GameObject utensilRoot, float cook, float burn, float mix, float overMix)
        {
            if (utensilRoot == null)
                return;
            try
            {
                if (cook > 0f)
                    WriteHandlerField(utensilRoot, GameApi.CookingHandlerType, GameApi.CookingTimeField, cook);
                if (mix > 0f)
                    WriteHandlerField(utensilRoot, GameApi.MixingHandlerType, GameApi.MixingTimeField, mix);

                var timing = utensilRoot.GetComponent<UtensilTiming>();
                if (timing == null)
                {
                    timing = utensilRoot.AddComponent<UtensilTiming>();
                    if (burn > 0f || overMix > 0f)
                        StubLog.Dbg("[UtensilTiming] 已挂载: " + utensilRoot.name
                            + "（cook=" + cook + " burn=" + burn + " mix=" + mix + " over=" + overMix + "）");
                }
                timing.m_cookTime = cook;
                timing.m_burnTime = burn;
                timing.m_mixTime = mix;
                timing.m_overMixTime = overMix;

                if (burn > 0f || overMix > 0f)
                {
                    s_hasAnyActive = true;
                    EntryPoint.EnsureUtensilTimingPatches();
                }
            }
            catch (Exception ex)
            {
                if (s_applyFailWarned.Add(utensilRoot.name))
                    StubLog.LogWarn("[UtensilTiming] 应用失败 " + utensilRoot.name + ": " + ex.Message);
            }
        }

        private static readonly System.Collections.Generic.HashSet<string> s_applyFailWarned =
            new System.Collections.Generic.HashSet<string>();
        private static bool s_writeFieldReflectionWarned;
        private static bool s_handlerMissingWarned;

        /// <summary>写 handler 公有时间字段（幂等：值相同跳过）。</summary>
        private static void WriteHandlerField(GameObject utensilRoot, Type handlerType, System.Reflection.FieldInfo field, float value)
        {
            if (handlerType == null || field == null)
            {
                if (!s_writeFieldReflectionWarned)
                {
                    s_writeFieldReflectionWarned = true;
                    StubLog.LogWarn("[UtensilTiming] handler 时间字段反射缺失，cook/mix 直写不生效（harmony 阈值接管不受影响）");
                }
                return;
            }
            var handler = utensilRoot.GetComponent(handlerType);
            if (handler == null)
            {
                if (!s_handlerMissingWarned)
                {
                    s_handlerMissingWarned = true;
                    StubLog.LogWarn("[UtensilTiming] 锅具上找不到 " + handlerType.Name + " 组件，时间直写跳过: " + utensilRoot.name);
                }
                return;
            }
            object raw = field.GetValue(handler);
            float current = raw is float ? (float)raw : 0f;
            if (Mathf.Approximately(current, value))
                return;
            field.SetValue(handler, value);
            StubLog.Dbg("[UtensilTiming] " + handlerType.Name + "." + field.Name + " "
                + current + " → " + value + " (" + utensilRoot.name + ")");
        }

        /// <summary>Ticker 入口，两级匹配：
        ///  1) 子树（主路径）：从 "UtensilTiming|" tag 包装向下扫 CookingHandler/
        ///     MixingHandler（编辑器与游戏侧 hotpot 家族走这里）；
        ///  2) 按名兜底：子树一个 handler 都没消费的配置，按包装 PseudoPrefabStub 的
        ///     真实 prefab 名（宿主实例化子物体名 = prefabName）全局匹配首个未标记
        ///     handler——游戏侧普通锅具实例不挂在 wrapper 子树下（s_test8 实测：
        ///     汤锅 tag 3,22 子树扫不到，火锅大锅 tag 4,12 子树命中）。
        /// 未配置的锅具零改动；已应用（标记组件已挂）幂等跳过。
        /// 诊断：计数变化时打一条摘要（排查"没生效"的第一落点）。</summary>
        internal static void Tick()
        {
            if (!s_loggedSelfCheck)
            {
                s_loggedSelfCheck = true;
                StubLog.Dbg("[UtensilTiming] 反射自检: CookingHandler=" + (GameApi.CookingHandlerType != null)
                    + " MixingHandler=" + (GameApi.MixingHandlerType != null)
                    + " CookTimeField=" + (GameApi.CookingTimeField != null)
                    + " MixTimeField=" + (GameApi.MixingTimeField != null));
            }
            EnsureWrapperCache();
            var wrappers = s_cachedWrappers;
            if (wrappers == null)
                wrappers = new System.Collections.Generic.List<WrapperConfig>();

            RefreshHandlerCachesIfNeeded();

            int tagHits = 0;
            int applied = 0;
            for (int i = 0; i < wrappers.Count; i++)
                wrappers[i].Consumed = 0;

            for (int i = 0; i < wrappers.Count; i++)
            {
                var cfg = wrappers[i];
                if (!cfg.HasValue || cfg.Abandoned)
                    continue;
                tagHits++;
                int n = ApplyToSubtree(cfg.Tag, GameApi.CookingHandlerType, cfg.Cook, cfg.Burn, cfg.Mix, cfg.Over);
                n += ApplyToSubtree(cfg.Tag, GameApi.MixingHandlerType, cfg.Cook, cfg.Burn, cfg.Mix, cfg.Over);
                cfg.Consumed = n;
                applied += n;
            }
            int fallback = 0;
            for (int i = 0; i < wrappers.Count; i++)
            {
                var cfg = wrappers[i];
                if (!cfg.HasValue || cfg.Abandoned || cfg.Consumed > 0 || string.IsNullOrEmpty(cfg.PrefabName))
                    continue;
                int n = ApplyByName(cfg, wrappers);
                fallback += n;
                applied += n;
                if (n > 0)
                    cfg.Consumed = n;
            }
            var summary = "tag" + tagHits + "/apply" + applied + (fallback > 0 ? "(兜底" + fallback + ")" : "");
            if (summary != s_prevPrinted)
            {
                s_prevPrinted = summary;
                StubLog.Dbg("[UtensilTiming] 扫描: " + summary);
            }
            s_lastTickApplied = applied;
            EvaluateScanComplete(wrappers, applied);
        }

        private static void EnsureWrapperCache()
        {
            if (s_cachedWrappers != null)
                return;
            s_cachedWrappers = new System.Collections.Generic.List<WrapperConfig>();
            var tags = UnityEngine.Object.FindObjectsOfType<SpecificPseudoPrefabTag>();
            for (int i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];
                if (tag == null || string.IsNullOrEmpty(tag.prefabTag)
                    || !tag.prefabTag.StartsWith(UtensilTimingConfig.TagPrefix, StringComparison.Ordinal))
                    continue;
                float cook, burn, mix, over;
                Parse(tag.prefabTag.Substring(UtensilTimingConfig.TagPrefix.Length),
                    out cook, out burn, out mix, out over);
                var cfg = new WrapperConfig();
                cfg.Tag = tag;
                cfg.Cook = cook;
                cfg.Burn = burn;
                cfg.Mix = mix;
                cfg.Over = over;
                cfg.HasValue = cook > 0f || burn > 0f || mix > 0f || over > 0f;
                if (burn > 0f || over > 0f)
                {
                    s_hasAnyActive = true;
                    EntryPoint.EnsureUtensilTimingPatches();
                }
                var baseStub = tag.GetComponent<PseudoPrefabStub>();
                cfg.PrefabName = (baseStub != null && baseStub.pseudoPrefabSO != null)
                    ? baseStub.pseudoPrefabSO.prefabName : null;
                s_cachedWrappers.Add(cfg);
            }
        }

        private static void RefreshHandlerCachesIfNeeded()
        {
            bool refresh = s_ticksSinceScene <= 2 || s_lastTickApplied > 0
                || s_cachedCookingHandlers == null || s_cachedMixingHandlers == null;
            if (!refresh)
                return;
            s_cachedCookingHandlers = ToComponentArray(GameApi.FindAll(GameApi.CookingHandlerType));
            s_cachedMixingHandlers = ToComponentArray(GameApi.FindAll(GameApi.MixingHandlerType));
        }

        private static Component[] ToComponentArray(UnityEngine.Object[] raw)
        {
            if (raw == null || raw.Length == 0)
                return new Component[0];
            var list = new System.Collections.Generic.List<Component>();
            for (int i = 0; i < raw.Length; i++)
            {
                var c = raw[i] as Component;
                if (c != null)
                    list.Add(c);
            }
            return list.ToArray();
        }

        private static void EvaluateScanComplete(System.Collections.Generic.List<WrapperConfig> wrappers, int applied)
        {
            s_ticksSinceScene++;
            if (s_ticksSinceScene < MinTicksBeforeEarlyStop)
                return;
            if (wrappers.Count == 0)
            {
                s_scanComplete = true;
                return;
            }
            if (AllConfigsResolved(wrappers))
            {
                s_scanComplete = true;
                return;
            }
            if (applied <= 0)
                s_ticksWithZeroProgress++;
            else
                s_ticksWithZeroProgress = 0;

            if (s_ticksWithZeroProgress >= MaxTicksNoProgressBeforeAbandon)
            {
                AbandonUnresolved(wrappers, false);
                s_scanComplete = true;
                return;
            }
            if (s_ticksSinceScene < MaxTicksBeforeForceStop)
                return;
            AbandonUnresolved(wrappers, true);
            s_scanComplete = true;
        }

        private static bool AllConfigsResolved(System.Collections.Generic.List<WrapperConfig> wrappers)
        {
            for (int i = 0; i < wrappers.Count; i++)
            {
                var cfg = wrappers[i];
                if (cfg.HasValue && !cfg.Abandoned && cfg.Consumed <= 0)
                    return false;
            }
            return true;
        }

        private static void AbandonUnresolved(System.Collections.Generic.List<WrapperConfig> wrappers, bool forceCap)
        {
            var unmatched = 0;
            for (int i = 0; i < wrappers.Count; i++)
            {
                var cfg = wrappers[i];
                if (!cfg.HasValue || cfg.Abandoned || cfg.Consumed > 0)
                    continue;
                cfg.Abandoned = true;
                unmatched++;
            }
            if (unmatched <= 0)
                return;
            var detail = BuildUnmatchedDetail(wrappers, 3);
            if (forceCap)
            {
                StubLog.LogWarn("[UtensilTiming] 扫描已达上限（约 " + (MaxTicksBeforeForceStop / 2)
                    + "s）强制停止：仍有 " + unmatched + " 个锅具时间配置未匹配到 handler"
                    + "（这些锅具走原版时间）。常见原因：prefab 名与实例名不符 / handler 类型反射失败。"
                    + detail);
            }
            else
            {
                StubLog.LogWarn("[UtensilTiming] 连续 " + MaxTicksNoProgressBeforeAbandon
                    + " 次扫描无新匹配，放弃 " + unmatched + " 个未匹配锅具时间配置（走原版时间）。"
                    + detail);
            }
        }

        private static string BuildUnmatchedDetail(System.Collections.Generic.List<WrapperConfig> wrappers, int maxNames)
        {
            var sb = new System.Text.StringBuilder();
            int shown = 0;
            for (int i = 0; i < wrappers.Count && shown < maxNames; i++)
            {
                var cfg = wrappers[i];
                if (!cfg.HasValue || !cfg.Abandoned)
                    continue;
                if (string.IsNullOrEmpty(cfg.PrefabName))
                    continue;
                if (sb.Length > 0)
                    sb.Append("; ");
                sb.Append(cfg.PrefabName);
                sb.Append("(handler×");
                sb.Append(CountHandlersMatchingPrefab(cfg.PrefabName));
                sb.Append(")");
                shown++;
            }
            if (sb.Length == 0)
                return "";
            return " 示例: " + sb.ToString();
        }

        private static int CountHandlersMatchingPrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
                return 0;
            int n = 0;
            n += CountMatchingInArray(s_cachedCookingHandlers, prefabName);
            n += CountMatchingInArray(s_cachedMixingHandlers, prefabName);
            return n;
        }

        private static int CountMatchingInArray(Component[] handlers, string prefabName)
        {
            if (handlers == null)
                return 0;
            int n = 0;
            for (int i = 0; i < handlers.Length; i++)
            {
                var handler = handlers[i];
                if (handler == null)
                    continue;
                if (NamesMatch(handler.gameObject.name, prefabName))
                    n++;
            }
            return n;
        }

        private static string s_prevPrinted = "";

        /// <summary>包装配置（tag 解析结果 + 兜底匹配状态）。</summary>
        private class WrapperConfig
        {
            public SpecificPseudoPrefabTag Tag;
            public float Cook;
            public float Burn;
            public float Mix;
            public float Over;
            public bool HasValue;
            public int Consumed;
            public bool Abandoned;
            public string PrefabName;
        }

        /// <summary>把时间应用到 tag 包装子树内的全部指定类型 handler（幂等跳过已挂标记）。</summary>
        private static int ApplyToSubtree(SpecificPseudoPrefabTag tag, Type handlerType, float cook, float burn, float mix, float over)
        {
            if (handlerType == null)
                return 0;
            var handlers = tag.GetComponentsInChildren(handlerType, true);
            int applied = 0;
            for (int i = 0; i < handlers.Length; i++)
            {
                var handler = handlers[i];
                if (handler == null)
                    continue;
                if (handler.GetComponent<UtensilTiming>() != null)
                    continue;
                ApplyValues(handler.gameObject, cook, burn, mix, over);
                applied++;
            }
            return applied;
        }

        /// <summary>按名兜底：全局找 gameObject.name == 真实 prefab 名、未挂标记、且不在
        /// 任何带时间 tag 的包装子树内（受管领地不越界）的 handler，应用到首个。
        /// 烹饪/混合按配置值有无分别尝试。</summary>
        private static int ApplyByName(WrapperConfig cfg, System.Collections.Generic.List<WrapperConfig> wrappers)
        {
            int applied = 0;
            if (cfg.Cook > 0f || cfg.Burn > 0f)
                applied += ApplyByNameOfKind(GameApi.CookingHandlerType, cfg, wrappers, s_cachedCookingHandlers);
            if (cfg.Mix > 0f || cfg.Over > 0f)
                applied += ApplyByNameOfKind(GameApi.MixingHandlerType, cfg, wrappers, s_cachedMixingHandlers);
            if (applied > 0)
                StubLog.Dbg("[UtensilTiming] 兜底按 prefab 名匹配: " + cfg.PrefabName
                    + "（cook=" + cfg.Cook + " burn=" + cfg.Burn + "）× " + applied);
            return applied;
        }

        private static int ApplyByNameOfKind(Type handlerType, WrapperConfig cfg,
            System.Collections.Generic.List<WrapperConfig> wrappers, Component[] handlers)
        {
            if (handlerType == null || handlers == null)
                return 0;
            for (int i = 0; i < handlers.Length; i++)
            {
                var handler = handlers[i];
                if (handler == null)
                    continue;
                if (handler.GetComponent<UtensilTiming>() != null)
                    continue;
                if (!NamesMatch(handler.gameObject.name, cfg.PrefabName))
                    continue;
                if (IsUnderAnyWrapper(handler.transform, wrappers))
                    continue;
                ApplyValues(handler.gameObject, cfg.Cook, cfg.Burn, cfg.Mix, cfg.Over);
                return 1;
            }
            return 0;
        }

        private static bool NamesMatch(string instanceName, string prefabName)
        {
            if (string.IsNullOrEmpty(instanceName) || string.IsNullOrEmpty(prefabName))
                return false;
            return NormalizeInstanceName(instanceName) == prefabName;
        }

        private static string NormalizeInstanceName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            if (name.EndsWith(CloneSuffix, StringComparison.Ordinal))
                return name.Substring(0, name.Length - CloneSuffix.Length);
            return name;
        }

        /// <summary>是否处于任一带时间 tag 的包装子树内（受管领地，兜底不越界）。</summary>
        private static bool IsUnderAnyWrapper(Transform t, System.Collections.Generic.List<WrapperConfig> wrappers)
        {
            while (t != null)
            {
                for (int i = 0; i < wrappers.Count; i++)
                {
                    var w = wrappers[i];
                    if (w.Tag != null && w.Tag.transform == t)
                        return true;
                }
                t = t.parent;
            }
            return false;
        }

        /// <summary>"cook,burn,mix,over"（invariant 浮点）。格式不符全 0（= 未配置）。</summary>
        private static void Parse(string payload, out float cook, out float burn, out float mix, out float over)
        {
            cook = 0f;
            burn = 0f;
            mix = 0f;
            over = 0f;
            if (string.IsNullOrEmpty(payload))
                return;
            var parts = payload.Split(',');
            if (parts.Length < 4)
                return;
            cook = ParseFloatOrZero(parts[0]);
            burn = ParseFloatOrZero(parts[1]);
            mix = ParseFloatOrZero(parts[2]);
            over = ParseFloatOrZero(parts[3]);
        }

        private static float ParseFloatOrZero(string s)
        {
            float v;
            if (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;
            return 0f;
        }
    }
}
