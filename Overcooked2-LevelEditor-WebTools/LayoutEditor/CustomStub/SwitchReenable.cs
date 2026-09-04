using System.Collections;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 按钮按压后自动复位（CustomStub 版，接替原 LayoutRuntimeSwitchReenable）。
    ///
    /// 背景：宿主 TriggerDisableScript 收到 Disable 触发会禁用目标 Behaviour
    /// （通常是 Interactable），按钮变灰不可再按。原版关卡由场景手工接线复位，
    /// 编辑器关卡没有——表现为按钮只能按一次。
    ///
    /// 原实现监听 "Disable" 触发（ITriggerReceiver，stub 程序集无法编译期实现
    /// 宿主接口），本版改为轮询等价实现：监视按钮 child 上 TriggerDisableScript
    /// 的 m_script.enabled 下降沿（true→false = 被按下），m_resetDelay 秒后向该
    /// 对象补发 m_enableTrigger（各实例自己的复位触发名，比硬编码 "Reset" 更稳）。
    ///
    /// 数据双通道：本组件（StubIO 反射烘焙）+ tag 载体 "SwitchReenable|&lt;delay&gt;"。
    /// </summary>
    public class SwitchReenable : MonoBehaviour
    {
        /** 按压后自动复位延时（秒）。 */
        public float m_resetDelay = 0.35f;

        /// <summary>tag 载体前缀。</summary>
        public const string TagPrefix = "SwitchReenable|";

        private Behaviour m_watched;
        private string m_enableTrigger;
        private bool m_lastEnabled = true;
        private bool m_bound;

        private static bool s_loggedSelfCheck;

        private IEnumerator Start()
        {
            var inner = RunInner();
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
                    StubLog.LogWarn("[SwitchReenable] 协程异常退出: " + name + "\n" + ex);
                    yield break;
                }
                if (!hasNext)
                    yield break;
                yield return current;
            }
        }

        private IEnumerator RunInner()
        {
            if (!s_loggedSelfCheck)
            {
                s_loggedSelfCheck = true;
                StubLog.Log("[SwitchReenable] 反射自检: TriggerDisableScript=" + (GameApi.TriggerDisableType != null)
                    + " SendTrigger=" + (GameApi.SendTriggerMethod != null));
            }

            float waited = 0f;
            while (!m_bound)
            {
                TryBind();
                if (m_bound)
                    break;
                waited += Time.unscaledDeltaTime;
                if (waited > 20f)
                {
                    StubLog.LogWarn("[SwitchReenable] 等待 TriggerDisableScript 超时（20s）: " + name);
                    yield break;
                }
                yield return new WaitForSeconds(0.25f);
            }
            StubLog.Log("[SwitchReenable] 绑定按钮: " + name + " → " + m_watched.gameObject.name
                + "（复位延时 " + m_resetDelay.ToString("0.##") + "s）");

            while (true)
            {
                if (m_watched == null)
                    yield break; // 按钮被销毁（重开关卡由自愈重建）
                bool enabledNow = m_watched.enabled;
                if (m_lastEnabled && !enabledNow)
                {
                    // 下降沿 = 按下 → 复位计时
                    yield return new WaitForSeconds(Mathf.Max(0.05f, m_resetDelay));
                    if (m_watched != null && !m_watched.enabled)
                        GameApi.SendTrigger(m_watched.gameObject, m_enableTrigger);
                }
                m_lastEnabled = enabledNow;
                yield return null;
            }
        }

        private void TryBind()
        {
            if (GameApi.TriggerDisableType == null || GameApi.DisableScriptField == null)
                return;
            var found = GetComponentsInChildren(GameApi.TriggerDisableType, true);
            for (int i = 0; i < found.Length; i++)
            {
                var script = GameApi.DisableScriptField.GetValue(found[i]) as Behaviour;
                if (script == null)
                    continue;
                m_watched = script;
                m_enableTrigger = GameApi.DisableEnableTriggerField != null
                    ? GameApi.DisableEnableTriggerField.GetValue(found[i]) as string
                    : null;
                if (string.IsNullOrEmpty(m_enableTrigger))
                    m_enableTrigger = "Reset";
                m_lastEnabled = script.enabled;
                m_bound = true;
                return;
            }
        }
    }
}
