using System.Collections;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 自动步道地板（Travelator）定时反转方向。
    ///
    /// 背景：宿主 Travelator（IMovingSurface 推人地板）方向 = 私有枚举
    /// m_directionXZ × transform.right，原版无任何方向切换机制。
    ///
    /// 反转语义（2026-09-13 用户决策）：【整体旋转】而非速度取反——
    /// 相位翻转时把 Travelator 所在物体的 localRotation 绕 Y 轴追加
    /// m_turnAngle 度（默认 180 = 掉头；+90/-90 = 转角，回来即转回
    /// 烘焙朝向），推人方向（transform.right 派生）与皮带模型/纹理朝向
    /// 一起转动，视觉上是"传送带转向"而不是"同一条皮带倒放"。
    /// m_speed 与材质 _speed 保持烘焙原值不动。
    ///
    /// 联机：Travelator 是各端本地求值的 IMovingSurface，同配置组件时间相位
    /// 天然同步（TimedCookingSwitch 同款约定），无需自定义网络消息。
    ///
    /// 数据双通道（TimedCookingSwitch 同款约定）：
    ///  - 权威通道：本组件序列化字段（StubIO 写回时反射 AddComponent + 填字段）；
    ///  - 载体通道：SpecificPseudoPrefabTag.prefabTag =
    ///    "TravelatorReverse|&lt;1|0&gt;,&lt;fwd&gt;,&lt;back&gt;,&lt;1|0&gt;[,&lt;angle&gt;]"（invariant；
    ///    angle 缺省 = 180，兼容 4 段旧载体），
    ///    组件缺失时由 EntryPoint 场景自愈解析还原。
    /// </summary>
    public class TravelatorReverser : MonoBehaviour
    {
        /** false = 配置保留但不生效（步道保持烘焙朝向）。 */
        public bool m_enabled = true;
        /** 正向期秒数（最小 1）。 */
        public float m_forwardSeconds = 10f;
        /** 反向期秒数（最小 1）。 */
        public float m_backwardSeconds = 10f;
        /** 初始相位为反向（false = 开局先正向 m_forwardSeconds 秒）。 */
        public bool m_startReversed = false;
        /** 反向相位转角（度，绕 Y；180 = 掉头，+90/-90 = 转角）。 */
        public float m_turnAngle = 180f;

        /// <summary>tag 载体前缀（EntryPoint 自愈与 StubIO 烘焙共用约定）。</summary>
        public const string TagPrefix = "TravelatorReverse|";

        private Component m_travelator;
        private Transform m_travTransform;
        private Quaternion m_baseLocalRotation;  // 绑定时刻的烘焙朝向（正向基准）
        private Quaternion m_turnRotation;       // 反向相位转角（Euler(0, m_turnAngle, 0)）
        private bool m_reversed;
        private bool m_bound;
        private bool m_firstFlipLogged;
        private bool m_applyWarned;

        /// <summary>相位再压间隔（帧，TimedSwitch 同款：防宿主/晚到逻辑覆盖）。</summary>
        private const int ReassertIntervalFrames = 10;

        private void OnEnable()
        {
            m_reversed = m_startReversed;
            StartCoroutine(SafeRunner(Drive()));
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            // 编辑器停止 Play / 组件被移除：恢复烘焙朝向，不留反转残留
            if (m_bound && m_travTransform != null)
                TryApplyDirection(false);
            m_bound = false;
            m_travelator = null;
            m_travTransform = null;
        }

        /// <summary>外层枚举器：C#4 禁止在有 catch 的 try 里 yield，套异常捕获。</summary>
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
                    StubLog.LogWarn("[TravelatorReverse] 协程异常退出: " + name + "\n" + ex);
                    yield break;
                }
                if (!hasNext)
                    yield break;
                yield return current;
            }
        }

        private IEnumerator Drive()
        {
            // 等子树里的 Travelator（宿主/模组生成 child 后出现）
            var waitChild = 0f;
            while (!m_bound)
            {
                TryBindTravelator();
                if (m_bound)
                    break;
                waitChild += Time.unscaledDeltaTime;
                if (waitChild > 20f)
                {
                    StubLog.LogWarn("[TravelatorReverse] 等待 Travelator 超时（20s），保持正向: " + name);
                    yield break;
                }
                yield return null;
            }

            if (!m_enabled)
            {
                TryApplyDirection(false);
                StubLog.Dbg("[TravelatorReverse] 已禁用（配置保留，保持正向）: " + name);
                yield break;
            }

            // 纵深防御：即使出现「OnEnable 先跑、配置后补写」的时序（AddComponent
            // 竞态 / 外部装配器），绑定完成时以最终 m_startReversed 重置相位。
            m_reversed = m_startReversed;
            m_turnRotation = Quaternion.Euler(0f, m_turnAngle, 0f);
            TryApplyDirection(m_reversed);
            StubLog.Dbg("[TravelatorReverse] 绑定 Travelator: " + name
                + "（fwd=" + m_forwardSeconds.ToString("0.#") + "s back=" + m_backwardSeconds.ToString("0.#")
                + "s startReversed=" + m_startReversed
                + " angle=" + m_turnAngle.ToString("0.#") + "°）");

            float waitFwd = Mathf.Max(1f, m_forwardSeconds);
            float waitBack = Mathf.Max(1f, m_backwardSeconds);
            float phaseLeft = m_reversed ? waitBack : waitFwd;
            var framesSinceApply = ReassertIntervalFrames; // 首帧立即压相
            while (true)
            {
                phaseLeft -= Time.deltaTime;
                var flipped = false;
                if (phaseLeft <= 0f)
                {
                    m_reversed = !m_reversed;
                    phaseLeft = m_reversed ? waitBack : waitFwd;
                    flipped = true;
                    if (!m_firstFlipLogged)
                    {
                        m_firstFlipLogged = true;
                        StubLog.Dbg("[TravelatorReverse] 首次相位翻转: " + name + " → " + (m_reversed ? "反" : "正"));
                    }
                }
                if (flipped || framesSinceApply++ >= ReassertIntervalFrames)
                {
                    framesSinceApply = 0;
                    if (m_travTransform == null)
                    {
                        StubLog.LogWarn("[TravelatorReverse] Travelator 已销毁，协程退出（重开关卡由自愈重建）: " + name);
                        yield break;
                    }
                    TryApplyDirection(m_reversed);
                }
                yield return null;
            }
        }

        private void TryBindTravelator()
        {
            if (GameApi.TravelatorType == null)
                return;
            var found = GetComponentsInChildren(GameApi.TravelatorType, true);
            for (int i = 0; i < found.Length; i++)
            {
                var c = found[i];
                if (c == null)
                    continue;
                m_travelator = c;
                m_travTransform = c.transform;
                m_baseLocalRotation = c.transform.localRotation;
                m_bound = true;
                return;
            }
        }

        /// <summary>压相：localRotation = 烘焙朝向（× 反相位追加 m_turnAngle 度绕 Y 旋转）。
        /// 周期调用——同类异常只报一次。</summary>
        private void TryApplyDirection(bool reversed)
        {
            if (!m_bound || m_travTransform == null)
                return;
            try
            {
                var target = reversed ? m_baseLocalRotation * m_turnRotation : m_baseLocalRotation;
                if (m_travTransform.localRotation != target)
                    m_travTransform.localRotation = target;
            }
            catch (System.Exception ex)
            {
                if (!m_applyWarned)
                {
                    m_applyWarned = true;
                    StubLog.LogWarn("[TravelatorReverse] 压相失败: " + name + " " + ex.Message);
                }
            }
        }
    }
}
