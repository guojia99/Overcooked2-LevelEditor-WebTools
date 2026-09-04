using System.Collections;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 火锅灶台定时开关（CustomStub 版，接替原 Assembly-CSharp-Patch 的
    /// LayoutRuntimeTimedCookingSwitch）。
    ///
    /// 背景：火锅灶台（cooking_region_floorburner / dlc10_cooking_region_floorburner）
    /// 的 child 自带 CookingRegion + TriggerDisableScript，宿主 ServerCookingRegion
    /// 与 CustomStub.HotPot 均以 CookingRegion.enabled 为烹饪门控——禁用即停火。
    ///
    /// 本组件按「开 m_onSeconds 秒 → 关 m_offSeconds 秒」循环切换子树内
    /// CookingRegion.enabled，并同步火焰 PFX（m_flameEffects / m_glowEffect）。
    /// 开局即启动；相同配置的灶台相位天然同步。m_enabled=false 时保持常开。
    ///
    /// 数据双通道（RandomCrate 同款约定）：
    ///  - 权威通道：本组件序列化字段（StubIO 写回时反射 AddComponent + 填字段）；
    ///  - 载体通道：SpecificPseudoPrefabTag.prefabTag = "TimedSwitch|<1|0>,<on>,<off>,<1|0>"
    ///    （invariant），组件缺失时由 EntryPoint 场景自愈解析还原。
    /// </summary>
    public class TimedCookingSwitch : MonoBehaviour
    {
        /** false = 配置保留但不生效（灶台保持常开）。 */
        public bool m_enabled = true;
        /** 开启期秒数（最小 3）。 */
        public float m_onSeconds = 30f;
        /** 关闭期秒数（最小 3）。 */
        public float m_offSeconds = 30f;
        /** 初始相位为开启（false = 开局先关 m_offSeconds 秒）。 */
        public bool m_startOn = true;

        private Behaviour m_region;
        private bool m_phaseOn;

        /// <summary>tag 载体前缀（EntryPoint 自愈与 StubIO 烘焙共用约定）。</summary>
        public const string TagPrefix = "TimedSwitch|";

        /// <summary>定时开关启用时是否处于「加热 / 有火」相位（HotPot 查询用）。</summary>
        public bool IsHeatingPhase()
        {
            return !m_enabled || m_phaseOn;
        }

        /// <summary>灶台子树内是否存在任一处于加热相位的定时开关（供 HotPot 按灶查询）。</summary>
        internal static bool IsHeatingAt(Transform regionOrAncestor)
        {
            if (regionOrAncestor == null)
                return true;
            var switches = regionOrAncestor.GetComponentsInChildren<TimedCookingSwitch>(true);
            for (int i = 0; i < switches.Length; i++)
            {
                if (switches[i] != null && switches[i].IsHeatingPhase())
                    return true;
            }
            // 祖先链（ CookingRegion 可能在开关对象的子级，也可能同对象/父级）
            var up = regionOrAncestor.GetComponentInParent<TimedCookingSwitch>();
            if (up != null)
                return up.IsHeatingPhase();
            return true; // 无定时开关 = 常开
        }

        private void OnEnable()
        {
            m_phaseOn = m_startOn;
            StartCoroutine(SafeRunner(Drive()));
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            // 编辑器停止 Play / 组件被移除：恢复灶台可用，不留禁用残留
            if (m_region != null)
                m_region.enabled = true;
            m_region = null;
        }

        /// <summary>外层枚举器：C#4 禁止在有 catch 的 try 里 yield，套异常捕获。
        /// 任何未捕获异常打到日志桥，不再静默死亡。</summary>
        private IEnumerator SafeRunner(IEnumerator inner)
        {
            while (true)
            {
                object current;
                bool hasNext;
                try
                {
                    hasNext = inner.MoveNext();
                    current = hasNext ? inner.Current : null;
                }
                catch (System.Exception ex)
                {
                    StubLog.LogWarn("[TimedSwitch] 协程异常退出: " + name + "\n" + ex);
                    yield break;
                }
                if (!hasNext)
                    yield break;
                yield return current;
            }
        }

        private IEnumerator Drive()
        {
            // 等 child 里的 CookingRegion（宿主/模组生成 child 后出现）
            var waitChild = 0f;
            while (m_region == null)
            {
                TryBindRegion();
                if (m_region != null)
                    break;
                waitChild += Time.unscaledDeltaTime;
                if (waitChild > 20f)
                {
                    StubLog.LogWarn("[TimedSwitch] 等待 CookingRegion 超时（20s），保持常开: " + name);
                    yield break;
                }
                yield return null;
            }
            StubLog.Log("[TimedSwitch] 绑定 CookingRegion: " + name
                + "（on=" + m_onSeconds.ToString("0.#") + "s off=" + m_offSeconds.ToString("0.#")
                + "s startOn=" + m_startOn + " enabled=" + m_enabled + "）");

            var waitOn = new WaitForSeconds(Mathf.Max(3f, m_onSeconds));
            var waitOff = new WaitForSeconds(Mathf.Max(3f, m_offSeconds));
            while (true)
            {
                ApplyRegionState();
                yield return m_phaseOn ? waitOn : waitOff;
                m_phaseOn = !m_phaseOn;
            }
        }

        private void TryBindRegion()
        {
            if (GameApi.CookingRegionType == null)
                return;
            var found = GetComponentsInChildren(GameApi.CookingRegionType, true);
            for (int i = 0; i < found.Length; i++)
            {
                var b = found[i] as Behaviour;
                if (b != null)
                {
                    m_region = b;
                    return;
                }
            }
        }

        private void ApplyRegionState()
        {
            if (m_region == null)
                return;
            bool on = IsHeatingPhase();
            m_region.enabled = on;
            SyncFlameVisuals(on);
        }

        private void SyncFlameVisuals(bool on)
        {
            try
            {
                if (GameApi.RegionFlameEffectsField != null)
                {
                    var effects = GameApi.RegionFlameEffectsField.GetValue(m_region) as ParticleSystem[];
                    if (effects != null)
                    {
                        foreach (var pfx in effects)
                        {
                            if (pfx == null)
                                continue;
                            if (on && !pfx.isPlaying)
                                pfx.Play();
                            else if (!on && pfx.isPlaying)
                                pfx.Stop();
                        }
                    }
                }
                if (GameApi.RegionGlowEffectField != null)
                {
                    var glow = GameApi.RegionGlowEffectField.GetValue(m_region) as ParticleSystem;
                    if (glow != null)
                    {
                        if (on && !glow.isPlaying)
                            glow.Play();
                        else if (!on && glow.isPlaying)
                            glow.Stop();
                    }
                }
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[TimedSwitch] 火焰同步失败: " + name + " " + ex.Message);
            }
        }
    }
}
