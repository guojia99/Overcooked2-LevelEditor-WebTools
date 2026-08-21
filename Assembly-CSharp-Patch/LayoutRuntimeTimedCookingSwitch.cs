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
    private bool m_running;

    private void Update()
    {
        if (m_running)
            return;
        try
        {
            var pseudo = GetComponent<PseudoPrefab>();
            if (pseudo == null)
                return;
            var child = pseudo.childGameObject;
            if (child == null)
                return; // child 尚未生成（ResetChild 未执行），下帧再试
            if (m_region == null)
                m_region = child.GetComponentInChildren<CookingRegion>();
            if (m_region == null)
                return;
        }
        catch (System.Exception)
        {
            return; // ResetChild 中途销毁/重建，下帧重试
        }

        m_running = true;
        StartCoroutine(Cycle());
    }

    private IEnumerator Cycle()
    {
        bool on = m_startOn;
        var waitOn = new WaitForSeconds(Mathf.Max(3f, m_onSeconds));
        var waitOff = new WaitForSeconds(Mathf.Max(3f, m_offSeconds));
        while (true)
        {
            if (m_region == null)
                yield break; // 灶台被销毁
            // 非启用状态：保持开启，等配置重新生效（编辑器写回后重建组件）
            m_region.enabled = !m_enabled || on;
            yield return on ? waitOn : waitOff;
            on = !on;
        }
    }

    private void OnDisable()
    {
        // 编辑器停止 Play / 组件被移除：恢复灶台可用，不留禁用残留
        if (m_region != null)
            m_region.enabled = true;
    }
}
