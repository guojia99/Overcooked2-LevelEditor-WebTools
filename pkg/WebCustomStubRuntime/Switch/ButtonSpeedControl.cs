using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 按钮控速（2026-10-14）：按一次按钮，步带（Travelator）或传送带
    /// （ConveyorStation）循环切到下一档速度。
    ///
    /// 挂载位置：Design/Speed Logic/SpdCtl_&lt;hash&gt; helper（SpeedControlBakery
    /// 烘焙；步带/传送带伪根上已有 TravelatorReverse|/ConveyorDirectionSync|
    /// 单用途 tag，本组件不得挤占——helper 独立承载组件与 tag）。
    ///
    /// 目标二选一（m_targetPath 伪根子树自动识别，一个目标只会有其一）：
    ///  - Travelator：写 m_speed + 反射调私有 SetSpeedOnMaterial() 重写材质
    ///    _speed（皮带纹理滚动必须与推人速度一致；Start 只调一次，运行时改速
    ///    必须手动补调）；
    ///  - ConveyorStation：写 m_conveySpeed（ServerConveyorStation 每次投递开始
    ///    时读取 1/速度 为时长，改写对在途投递零影响，无需视觉刷新）。
    ///
    /// 按压检测：SwitchReenable / CoaxialButtonGroup 同款轮询等价实现——监视
    /// 各按钮子树 TriggerDisableScript.m_script.enabled 的下降沿
    /// （true→false = 被按下）。按钮复位由烘焙侧默认 SwitchReenable|0.35 承担
    /// （本组件不管按钮红绿）。vanilla 触发通道投不进 stub 组件（仓库铁律），
    /// 故不走 objectToTrigger。
    ///
    /// 联机：按压/禁用状态由 vanilla 网络同步呈现到各端，本组件每端各跑一份、
    /// 各自应用档位——同配置同按压序列，结果天然一致（TravelatorReverser 同款
    /// 论证），无需网络消息与服务端裁决。
    ///
    /// 数据双通道：本组件（Bakery 反射烘焙，权威）+ tag 载体
    /// SpeedCtl|E:&lt;1|0&gt;|T:&lt;目标名&gt;|N:&lt;按钮名,..&gt;|L:&lt;s1,s2,..&gt;
    /// （%,;&gt;| 做 %XX 转义；EntryPoint.HealObject 解析还原，组件在真机是
    /// Missing Script 死件）。
    /// </summary>
    public class ButtonSpeedControl : MonoBehaviour
    {
        /// <summary>false = 配置保留但不生效（保持烘焙速度）。</summary>
        public bool m_enabled = true;
        /// <summary>速度目标伪根物体名（GameObject.Find + 缓存；子树找 Travelator/ConveyorStation）。</summary>
        public string m_targetPath;
        /// <summary>触发源按钮伪根物体名（GameObject.Find 身份键；烘焙侧保证唯一名）。</summary>
        public string[] m_buttonPaths;
        /// <summary>速度档位（正=正向，负=反向；每按一次切到下一档，尾档回卷首档）。</summary>
        public float[] m_speeds;

        public const string TagPrefix = "SpeedCtl|";
        /// <summary>最少档数（低于此值组件无意义，协程退出并告警）。</summary>
        public const int MinLevels = 2;

        private class Member
        {
            public string rootName;
            public Behaviour watched;
            public bool lastEnabled = true;
            public bool bound;
        }

        private readonly List<Member> m_members = new List<Member>();
        private GameObject m_targetRoot;
        private Component m_travelator;
        private Component m_conveyor;
        private bool m_targetBound;
        private int m_speedIndex;
        private bool m_applyWarned;
        private static bool s_loggedSelfCheck;

        private void OnEnable()
        {
            StartCoroutine(SafeRunner(Drive()));
        }

        private void OnDisable()
        {
            StopAllCoroutines();
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
                    StubLog.LogWarn("[ButtonSpeedControl] 协程异常退出: " + name + "\n" + ex);
                    yield break;
                }
                if (!hasNext)
                    yield break;
                yield return current;
            }
        }

        private IEnumerator Drive()
        {
            if (!s_loggedSelfCheck)
            {
                s_loggedSelfCheck = true;
                StubLog.Dbg("[ButtonSpeedControl] 反射自检: TriggerDisableScript=" +
                    (GameApi.TriggerDisableType != null) + " Travelator=" +
                    (GameApi.TravelatorType != null) + " ConveyorStation=" +
                    (GameApi.ConveyorStationType != null));
            }

            if (m_speeds == null || m_speeds.Length < MinLevels)
            {
                StubLog.LogWarn("[ButtonSpeedControl] " + name + " 速度档位不足 " + MinLevels +
                    " 个，协程退出");
                yield break;
            }

            if (!m_enabled)
            {
                StubLog.Dbg("[ButtonSpeedControl] 已禁用（配置保留）: " + name);
                yield break;
            }

            m_members.Clear();
            foreach (var rootName in m_buttonPaths ?? new string[0])
            {
                if (string.IsNullOrEmpty(rootName)) continue;
                m_members.Add(new Member { rootName = rootName });
            }
            if (m_members.Count == 0)
            {
                StubLog.LogWarn("[ButtonSpeedControl] " + name + " 未绑定任何按钮，协程退出");
                yield break;
            }

            // ---- 绑定阶段：目标（子树 Travelator/ConveyorStation）与按钮就绪才进主循环
            float waited = 0f;
            bool slowPoll = false;
            while (true)
            {
                if (!m_targetBound)
                    TryBindTarget();
                bool allButtonsBound = true;
                for (int i = 0; i < m_members.Count; i++)
                {
                    var m = m_members[i];
                    if (!m.bound && !TryBindMember(m))
                        allButtonsBound = false;
                }
                if (m_targetBound && allButtonsBound)
                    break;
                waited += 0.25f;
                if (waited > 20f && !slowPoll)
                {
                    slowPoll = true;
                    StubLog.LogWarn("[ButtonSpeedControl] " + name + " 目标或按钮 20s 未就绪" +
                        "（target=" + m_targetBound + "），降为 1s 轮询继续等待");
                }
                yield return new WaitForSeconds(slowPoll ? 1f : 0.25f);
            }

            // 初始压第 0 档（与烘焙侧写入的原生速度一致；防 heal 路径组件在而
            // 原生字段被还原的场景）
            m_speedIndex = 0;
            ApplySpeed(m_speeds[0]);

            StubLog.Log("[ButtonSpeedControl] " + name + " 启动：" + m_members.Count +
                " 按钮｜目标=" + (m_travelator != null ? "步带" : "传送带") + "｜档位 " +
                m_speeds.Length + " 档");

            // ---- 主循环：下降沿检测 → 循环切档
            while (true)
            {
                for (int i = 0; i < m_members.Count; i++)
                {
                    var m = m_members[i];
                    if (m.watched == null)
                        continue;
                    bool nowEnabled = m.watched.enabled;
                    if (m.lastEnabled && !nowEnabled)
                        HandlePress(m.rootName);
                    m.lastEnabled = nowEnabled;
                }
                yield return null;
            }
        }

        /// <summary>绑定速度目标：m_targetPath 伪根子树优先找 Travelator，
        /// 否则 ConveyorStation。</summary>
        private void TryBindTarget()
        {
            if (m_targetRoot == null)
            {
                if (string.IsNullOrEmpty(m_targetPath))
                    return;
                m_targetRoot = GameObject.Find(m_targetPath);
                if (m_targetRoot == null)
                    return;
            }
            if (GameApi.TravelatorType != null)
            {
                var found = m_targetRoot.GetComponentsInChildren(GameApi.TravelatorType, true);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] == null) continue;
                    m_travelator = found[i];
                    m_targetBound = true;
                    return;
                }
            }
            if (GameApi.ConveyorStationType != null)
            {
                var found = m_targetRoot.GetComponentsInChildren(GameApi.ConveyorStationType, true);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] == null) continue;
                    m_conveyor = found[i];
                    m_targetBound = true;
                    return;
                }
            }
        }

        /// <summary>绑定单个按钮：子树扫 TriggerDisableScript（vanilla，真机可解析）。</summary>
        private bool TryBindMember(Member m)
        {
            if (GameApi.TriggerDisableType == null || GameApi.DisableScriptField == null)
                return false;
            var root = GameObject.Find(m.rootName);
            if (root == null)
                return false;
            var found = root.GetComponentsInChildren(GameApi.TriggerDisableType, true);
            if (found.Length == 0)
                return false;
            var watched = GameApi.DisableScriptField.GetValue(found[0]) as Behaviour;
            if (watched == null)
                return false;
            m.watched = watched;
            m.lastEnabled = watched.enabled;
            m.bound = true;
            return true;
        }

        private void HandlePress(string buttonName)
        {
            m_speedIndex = (m_speedIndex + 1) % m_speeds.Length;
            float level = m_speeds[m_speedIndex];
            ApplySpeed(level);
            StubLog.Dbg("[ButtonSpeedControl] " + name + " 按下 " + buttonName +
                " → 档位 " + (m_speedIndex + 1) + "/" + m_speeds.Length +
                " 速度=" + level.ToString("0.###"));
        }

        /// <summary>压速度：Travelator 双写（字段+材质），ConveyorStation 单写。
        /// 周期调用路径——同类异常只报一次。</summary>
        private void ApplySpeed(float level)
        {
            try
            {
                if (m_travelator != null)
                {
                    if (GameApi.TravelatorSpeedField != null)
                        GameApi.TravelatorSpeedField.SetValue(m_travelator, level);
                    if (GameApi.TravelatorSetSpeedOnMaterialMethod != null)
                        GameApi.TravelatorSetSpeedOnMaterialMethod.Invoke(m_travelator, null);
                }
                else if (m_conveyor != null)
                {
                    if (GameApi.ConveyorSpeedField != null)
                        GameApi.ConveyorSpeedField.SetValue(m_conveyor, level);
                }
            }
            catch (System.Exception ex)
            {
                if (!m_applyWarned)
                {
                    m_applyWarned = true;
                    StubLog.LogWarn("[ButtonSpeedControl] 压速度失败: " + name + " " + ex.Message);
                }
            }
        }
    }
}
