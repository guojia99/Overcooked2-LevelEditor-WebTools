using System.Collections;
using LevelEditor;
using UnityEngine;

/// 火锅灶台定时开关（游戏编译，写回时烘焙到灶台伪根上，随场景保存）。
///
/// 背景：火锅灶台（cooking_region_floorburner / dlc10_cooking_region_floorburner）
/// 的 bundle child 自带 CookingRegion + TriggerDisableScript（Layout1/Layout2），
/// 宿主 ServerCookingRegion.UpdateSynchronising 与 LayoutRuntimeHotPot 均以
/// CookingRegion.enabled 为烹饪门控——禁用即停火（锅具不烹饪、火焰熄灭）。
///
/// 本组件在运行时按「开 m_onSeconds 秒 → 关 m_offSeconds 秒」自动循环切换
/// child 上 CookingRegion.enabled。开局即启动（无需玩家操作）；相同配置的
/// 灶台相位天然同步（同帧启动）。m_enabled=false 时循环不生效（保持 region 开启）。
public class LayoutRuntimeTimedCookingSwitch : MonoBehaviour
{
    /** false = 配置保留但不生效（灶台保持常开）。 */
    public bool m_enabled = true;
    /** 开启期秒数（最小 3）。 */
    public float m_onSeconds = 30f;
    /** 关闭期秒数（最小 3）。 */
    public float m_offSeconds = 30f;
    /** 初始相位为开启（false = 开局先关 m_offSeconds 秒）。 */
    public bool m_startOn = true;

    private CookingRegion m_region;
    private bool m_cycleStarted;
    /** 当前是否处于开启相位（与 m_startOn 初始值一致，随后交替翻转）。 */
    private bool m_phaseOn;
    private Coroutine m_endOfFrameDrive;

    /// 定时开关启用时，当前是否处于「加热 / 有火」相位（供 LayoutRuntimeHotPot 等查询）。
    public bool IsHeatingPhase()
    {
        return !m_enabled || m_phaseOn;
    }

    private void OnEnable()
    {
        m_cycleStarted = false;
        m_phaseOn = m_startOn;
        m_endOfFrameDrive = StartCoroutine(EndOfFrameDrive());
    }

    private void OnDisable()
    {
        if (m_endOfFrameDrive != null)
        {
            StopCoroutine(m_endOfFrameDrive);
            m_endOfFrameDrive = null;
        }
        // 编辑器停止 Play / 组件被移除：恢复灶台可用，不留禁用残留
        if (m_region != null)
            m_region.enabled = true;
        m_region = null;
        m_cycleStarted = false;
    }

    private IEnumerator EndOfFrameDrive()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();
            if (!TryBindRegion())
                continue;
            if (!m_cycleStarted)
            {
                m_phaseOn = m_startOn;
                m_cycleStarted = true;
                StartCoroutine(Cycle());
            }
            ApplyRegionState();
        }
    }

    private bool TryBindRegion()
    {
        if (m_region != null)
            return true;
        try
        {
            var pseudo = GetComponent<PseudoPrefab>();
            if (pseudo == null || pseudo.childGameObject == null)
                return false;
            m_region = pseudo.childGameObject.GetComponentInChildren<CookingRegion>();
            return m_region != null;
        }
        catch (System.Exception)
        {
            return false;
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
        if (m_region.m_flameEffects != null)
        {
            foreach (var pfx in m_region.m_flameEffects)
            {
                if (pfx == null)
                    continue;
                if (on && !pfx.isPlaying)
                    pfx.Play();
                else if (!on && pfx.isPlaying)
                    pfx.Stop();
            }
        }
        if (m_region.m_glowEffect != null)
        {
            if (on && !m_region.m_glowEffect.isPlaying)
                m_region.m_glowEffect.Play();
            else if (!on && m_region.m_glowEffect.isPlaying)
                m_region.m_glowEffect.Stop();
        }
    }

    private IEnumerator Cycle()
    {
        var waitOn = new WaitForSeconds(Mathf.Max(3f, m_onSeconds));
        var waitOff = new WaitForSeconds(Mathf.Max(3f, m_offSeconds));
        while (true)
        {
            if (m_region == null)
                yield break;
            yield return m_phaseOn ? waitOn : waitOff;
            m_phaseOn = !m_phaseOn;
        }
    }
}
