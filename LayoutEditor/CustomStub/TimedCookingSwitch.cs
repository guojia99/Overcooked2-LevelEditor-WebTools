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

        // ---- 火焰视觉资产（绑定后一次性建立，见 BindFlameAssets） ----
        // 灶台视觉 = ① m_flameEffects[] 火焰粒子 + ② m_glowEffect 辉光
        //          + ③ m_burnerRenderer 材质 _EmissiveColour（炉体发光）。
        // 宿主 ClientCookingRegion 本应每帧轮询 region.enabled 并跑完整过渡
        // （粒子+辉光+材质渐变+音效），但同步实体未建立/未驱动时无人接管；
        // 反射字段缺失时旧版静默跳过 → 「逻辑已关、火焰常燃」。此处双通道：
        // 首选宿主字段（与 ClientCookingRegion 同源）；缺失/空时回退收集本
        // 物体子树全部粒子（灶台子树内粒子只有火焰/辉光；锅是独立物品不在此树）。
        private ParticleSystem[] m_flames = null;
        private ParticleSystem m_glow = null;
        private Material[] m_burnerMats = null;
        private Color[] m_emissiveBase = null;
        private bool m_flameSourceIsFallback = false;

        private static readonly int BurnerEmissParam = Shader.PropertyToID("_EmissiveColour");

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

            // 纵深防御：即使出现「OnEnable 先跑、配置后补写」的时序（AddComponent
            // 竞态 / 外部装配器），绑定完成时也以最终 m_startOn 重置相位——
            // 避免以默认值（true）固化的初始相位把 startOn=false 的开局关窗口吃掉。
            m_phaseOn = m_startOn;

            BindFlameAssets();

            // 逐帧压相（2026-09-07 修复「startOn=false 开局逻辑已关、火焰常燃」）：
            // 旧版只在相位边界应用一次——绑定当帧火焰粒子尚未 isPlaying（playOnAwake
            // 迟一拍）时 Stop 空转，粒子随后自燃并烧满整个关相位；宿主点火过渡若在
            // 绑定后触发同样漏压。改为每帧强制 region.enabled + 火焰状态，晚到的
            // 点火/重燃下一帧即被压回。
            float waitOn = Mathf.Max(3f, m_onSeconds);
            float waitOff = Mathf.Max(3f, m_offSeconds);
            float phaseLeft = m_phaseOn ? waitOn : waitOff;
            while (true)
            {
                ApplyRegionState();
                phaseLeft -= Time.deltaTime;
                if (phaseLeft <= 0f)
                {
                    m_phaseOn = !m_phaseOn;
                    phaseLeft = m_phaseOn ? waitOn : waitOff;
                }
                yield return null;
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

        /// <summary>建立火焰视觉资产清单（绑定成功后一次）。
        ///  首选宿主字段通道（m_flameEffects/m_glowEffect/m_burnerRenderer，
        ///  与 ClientCookingRegion 同源）；字段缺失或空数组时回退收集本物体
        ///  子树全部粒子。材质发光缓存 _EmissiveColour 原色供关相位置 0。</summary>
        private void BindFlameAssets()
        {
            m_flames = null;
            m_glow = null;
            m_flameSourceIsFallback = false;
            try
            {
                if (GameApi.RegionFlameEffectsField != null)
                    m_flames = GameApi.RegionFlameEffectsField.GetValue(m_region) as ParticleSystem[];
                if (m_flames != null)
                {
                    // 剔除 null 槽位（数组可能带空尾）
                    var live = new System.Collections.Generic.List<ParticleSystem>();
                    for (int i = 0; i < m_flames.Length; i++)
                        if (m_flames[i] != null) live.Add(m_flames[i]);
                    m_flames = live.Count > 0 ? live.ToArray() : null;
                }
                if (GameApi.RegionGlowEffectField != null)
                    m_glow = GameApi.RegionGlowEffectField.GetValue(m_region) as ParticleSystem;

                // 回退：宿主字段不可用/为空（类型解析失败或该皮肤未配）——
                // 收集子树全部粒子（火焰+辉光都在其中，全量即熄火语义；
                // 与字段通道并存幂等，Play/Stop 重复无副作用）。
                if (m_flames == null)
                {
                    m_flames = GetComponentsInChildren<ParticleSystem>(true);
                    m_flameSourceIsFallback = m_flames != null && m_flames.Length > 0;
                }

                // 材质发光：m_burnerRenderer 的实例化材质（仅保留含参数的槽位）
                if (GameApi.RegionBurnerRendererField != null)
                {
                    var rend = GameApi.RegionBurnerRendererField.GetValue(m_region) as Renderer;
                    if (rend != null)
                    {
                        var mats = rend.materials;
                        var keep = new System.Collections.Generic.List<Material>();
                        var colors = new System.Collections.Generic.List<Color>();
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] != null && mats[i].HasProperty(BurnerEmissParam))
                            {
                                keep.Add(mats[i]);
                                colors.Add(mats[i].GetColor(BurnerEmissParam));
                            }
                        }
                        if (keep.Count > 0)
                        {
                            m_burnerMats = keep.ToArray();
                            m_emissiveBase = colors.ToArray();
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[TimedSwitch] 火焰资产收集失败（视觉兜底降级）: " + name + " " + ex.Message);
            }
            StubLog.Log("[TimedSwitch] 火焰资产: " + name
                + " 粒子=" + (m_flames != null ? m_flames.Length.ToString() : "0")
                + (m_glow != null ? "+辉光" : "")
                + " 材质=" + (m_burnerMats != null ? m_burnerMats.Length.ToString() : "0")
                + (m_flameSourceIsFallback ? "（子树回退）" : "（宿主字段）"));
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
                if (m_flames != null)
                {
                    for (int i = 0; i < m_flames.Length; i++)
                    {
                        var pfx = m_flames[i];
                        if (pfx == null)
                            continue;
                        if (on && !pfx.isPlaying)
                            pfx.Play();
                        else if (!on && pfx.isPlaying)
                            pfx.Stop();
                    }
                }
                if (m_glow != null)
                {
                    if (on && !m_glow.isPlaying)
                        m_glow.Play();
                    else if (!on && m_glow.isPlaying)
                        m_glow.Stop();
                }
                // 材质发光：关相位清零（黑+alpha0；仅置 alpha 在忽略 alpha 的
                // 发光 shader 下炉体仍然亮着），开相位恢复缓存原色
                // （宿主渐变版为逐帧插值；此处开关即达，视觉语义一致）。
                if (m_burnerMats != null && m_emissiveBase != null)
                {
                    for (int j = 0; j < m_burnerMats.Length; j++)
                    {
                        if (m_burnerMats[j] == null)
                            continue;
                        Color c = on ? m_emissiveBase[j] : new Color(0f, 0f, 0f, 0f);
                        if (m_burnerMats[j].GetColor(BurnerEmissParam) != c)
                            m_burnerMats[j].SetColor(BurnerEmissParam, c);
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
