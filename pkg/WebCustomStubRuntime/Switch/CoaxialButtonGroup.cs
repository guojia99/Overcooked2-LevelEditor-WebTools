using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 同轴按钮组（2026-09-28）：≥2 个按钮组成一组，从第一个按钮按下起在
    /// m_windowSeconds（默认 1s，可调 0.35~5s）窗口内组内全部按钮先后按下才触发
    /// 目标事件；窗口超时未集齐则已按按钮自动弹回、不触发任何事件。
    ///
    /// 行为（「一个按下去之后切换为关闭状态，另外一个按下，触发事件」）：
    ///  - 按下即时 vanilla 自禁用（按钮保持红态/关闭状态），回绿由本组件统一管理；
    ///  - 集齐 → 对全部目标 GameApi.SendTrigger（与直连联动同通道，服务器权威，
    ///    客户端侧 Server* 组件缺失自然落空），锁定 ≥0.35s（防抖，与
    ///    ButtonLogicRelay.MinPressIntervalSeconds 同款最短按压间隔）后全部成员
    ///    弹回，可立刻开始下一轮；
    ///  - 超时 → 仅弹回已按成员，零触发。
    ///
    /// 按压检测：SwitchReenable 同款轮询等价实现——监视各按钮子树
    /// TriggerDisableScript.m_script.enabled 的下降沿（true→false = 被按下），
    /// 复位触发名按实例 m_enableTrigger 读取（不硬编码 "Reset"）。
    ///
    /// 数据双通道：本组件（烘焙反射挂载）+ tag 载体
    /// Coaxial|W:&lt;window&gt;|N:&lt;按钮根名,..&gt;|T:&lt;目标名&gt;,&lt;触发&gt;;..
    /// （%,;&gt;| 做 %XX 转义，与 BLRelay tag 同款；EntryPoint.HealObject 解析回填）。
    ///
    /// 联机：按压/禁用状态由 vanilla 网络同步呈现到各端，本组件每端各跑一份；
    /// 目标触发仅服务端 Server* 组件响应——与 ButtonLogicRelay 同语义。
    /// </summary>
    public class CoaxialButtonGroup : MonoBehaviour
    {
        /// <summary>成员按钮伪根物体名（GameObject.Find 身份键；烘焙侧保证 _BLn 唯一名）。</summary>
        public string[] m_buttonRootNames;
        /// <summary>同按窗口（秒，0.35~5；从第一个按钮按下起计时）。</summary>
        public float m_windowSeconds = 1f;
        /// <summary>目标机器根物体名（GameObject.Find + 缓存）。</summary>
        public string[] m_targetNames;
        /// <summary>与 m_targetNames 平行：广播给目标的触发消息（断头台 Chop 等）。</summary>
        public string[] m_targetTriggers;

        public const string TagPrefix = "Coaxial|";
        public const float MinWindowSeconds = 0.35f;
        public const float MaxWindowSeconds = 5f;
        /// <summary>成功触发后的最短锁定（秒）——防抖 + 与按钮最短按压间隔对齐。</summary>
        public const float MinLockoutSeconds = 0.35f;

        private class Member
        {
            public string rootName;
            public GameObject child;
            public Behaviour watched;
            public string enableTrigger = "Reset";
            public bool lastEnabled = true;
            public bool bound;
        }

        private readonly List<Member> m_members = new List<Member>();
        private readonly Dictionary<string, GameObject> m_targetCache =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private HashSet<string> m_resolveWarned;

        private float m_roundStart = -1f;
        private readonly List<Member> m_pressed = new List<Member>();
        private float m_lockUntil = -1f;
        private bool m_firedOnceLogged;
        private bool m_timeoutLogged;

        private static bool s_loggedSelfCheck;

        private void Awake()
        {
            m_windowSeconds = Mathf.Clamp(m_windowSeconds, MinWindowSeconds, MaxWindowSeconds);
        }

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
                    StubLog.LogWarn("[CoaxialButtonGroup] 协程异常退出: " + name + "\n" + ex);
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
                StubLog.Dbg("[CoaxialButtonGroup] 反射自检: TriggerDisableScript=" +
                    (GameApi.TriggerDisableType != null) + " SendTrigger=" +
                    (GameApi.SendTriggerMethod != null));
            }

            m_members.Clear();
            foreach (var rootName in m_buttonRootNames ?? new string[0])
            {
                if (string.IsNullOrEmpty(rootName)) continue;
                m_members.Add(new Member { rootName = rootName });
            }
            if (m_members.Count < 2)
            {
                StubLog.LogWarn("[CoaxialButtonGroup] " + name + " 成员不足 2 个（" +
                    m_members.Count + "），协程退出");
                yield break;
            }

            // ---- 绑定阶段：全部成员的 TriggerDisableScript 就绪才进入主循环
            //（缺一个就无法「集齐」，宁可整体不工作也不降级误触发）。
            float waited = 0f;
            bool slowPoll = false;
            while (true)
            {
                int unbound = 0;
                for (int i = 0; i < m_members.Count; i++)
                {
                    var m = m_members[i];
                    if (!m.bound && !TryBindMember(m)) unbound++;
                }
                if (unbound == 0) break;
                waited += 0.25f;
                if (waited > 20f && !slowPoll)
                {
                    slowPoll = true;
                    StubLog.LogWarn("[CoaxialButtonGroup] " + name + " 有 " + unbound +
                        " 个按钮 20s 未就绪（被删/改名/非按钮？），降为 1s 轮询继续等待");
                }
                yield return new WaitForSeconds(slowPoll ? 1f : 0.25f);
            }

            StubLog.Log("[CoaxialButtonGroup] " + name + " 启动：" + m_members.Count +
                " 按钮｜窗口 " + m_windowSeconds.ToString("0.##") + "s｜目标 " +
                (m_targetNames != null ? m_targetNames.Length : 0) + " 个（" +
                JoinNames() + "）");

            // ---- 主循环：下降沿检测 + 窗口超时
            while (true)
            {
                for (int i = 0; i < m_members.Count; i++)
                {
                    var m = m_members[i];
                    if (m.watched == null)
                    {
                        WarnResolveOnce(m.rootName, "监视的按钮脚本已销毁");
                        continue;
                    }
                    bool nowEnabled = m.watched.enabled;
                    if (m.lastEnabled && !nowEnabled)
                        HandlePress(m);
                    m.lastEnabled = nowEnabled;
                }

                if (m_roundStart >= 0f && Time.time - m_roundStart > m_windowSeconds)
                    HandleTimeout();

                yield return null;
            }
        }

        private string JoinNames()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < m_members.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(m_members[i].rootName);
            }
            return sb.ToString();
        }

        /// <summary>绑定单个成员：按钮子树扫 TriggerDisableScript（vanilla，
        /// 真机可解析），读其禁用目标 Behaviour 与复位触发名。</summary>
        private bool TryBindMember(Member m)
        {
            if (GameApi.TriggerDisableType == null || GameApi.DisableScriptField == null)
                return false;
            var root = GameObject.Find(m.rootName);
            if (root == null)
            {
                WarnResolveOnce(m.rootName, "GameObject.Find 未命中（被删/改名/未激活）");
                return false;
            }
            var found = root.GetComponentsInChildren(GameApi.TriggerDisableType, true);
            if (found.Length == 0) return false;
            var watched = GameApi.DisableScriptField.GetValue(found[0]) as Behaviour;
            if (watched == null) return false;
            m.child = found[0].gameObject;
            m.watched = watched;
            m.enableTrigger = GameApi.DisableEnableTriggerField != null
                ? GameApi.DisableEnableTriggerField.GetValue(found[0]) as string
                : null;
            if (string.IsNullOrEmpty(m.enableTrigger))
                m.enableTrigger = "Reset";
            m.lastEnabled = watched.enabled;
            m.bound = true;
            return true;
        }

        private void HandlePress(Member m)
        {
            if (Time.time < m_lockUntil)
                return; // 锁定窗口内的迟到按压（正常应已被禁用收不到）
            if (m_roundStart < 0f)
            {
                m_roundStart = Time.time;
                m_pressed.Clear();
            }
            if (!m_pressed.Contains(m))
                m_pressed.Add(m);
            StubLog.Dbg("[CoaxialButtonGroup] " + name + " 按下 " + m.rootName +
                "（" + m_pressed.Count + "/" + m_members.Count + "）");
            if (m_pressed.Count >= m_members.Count)
                HandleSuccess();
        }

        /// <summary>集齐：广播全部目标 → 锁定 0.35s → 全部成员弹回（可立刻下一轮）。</summary>
        private void HandleSuccess()
        {
            m_roundStart = -1f;
            m_pressed.Clear();
            int fired = 0;
            var targets = m_targetNames ?? new string[0];
            var triggers = m_targetTriggers ?? new string[0];
            for (int i = 0; i < targets.Length && i < triggers.Length; i++)
            {
                if (string.IsNullOrEmpty(targets[i]) || string.IsNullOrEmpty(triggers[i])) continue;
                var target = ResolveTarget(targets[i]);
                if (target == null) continue;
                GameApi.SendTrigger(target, triggers[i]);
                fired++;
            }
            StubLog.Log("[CoaxialButtonGroup] " + name + " 集齐 " + m_members.Count +
                " 按钮同按成功 → 触发 " + fired + " 个目标");
            m_lockUntil = Time.time + MinLockoutSeconds;
            StartCoroutine(ReenableAllLater(MinLockoutSeconds));
        }

        /// <summary>窗口超时：仅弹回已按成员，零触发。</summary>
        private void HandleTimeout()
        {
            m_roundStart = -1f;
            for (int i = 0; i < m_pressed.Count; i++)
                SendEnable(m_pressed[i]);
            if (!m_timeoutLogged)
            {
                m_timeoutLogged = true;
                StubLog.Log("[CoaxialButtonGroup] " + name + " 窗口 " +
                    m_windowSeconds.ToString("0.##") + "s 超时未集齐，弹回 " +
                    m_pressed.Count + " 个已按按钮（不触发）——后续超时静默");
            }
            m_pressed.Clear();
        }

        private IEnumerator ReenableAllLater(float delay)
        {
            yield return new WaitForSeconds(delay);
            int sent = 0;
            for (int i = 0; i < m_members.Count; i++)
            {
                if (m_members[i].watched == null) continue;
                SendEnable(m_members[i]);
                sent++;
            }
            if (!m_firedOnceLogged)
            {
                m_firedOnceLogged = true;
                StubLog.Dbg("[CoaxialButtonGroup] " + name + " 成功后回绿 " + sent + "/" +
                    m_members.Count + " 个按钮");
            }
        }

        /// <summary>向按钮 child 发复位触发（读实例 m_enableTrigger，缺省 Reset）。</summary>
        private void SendEnable(Member m)
        {
            if (m.child == null) return;
            GameApi.SendTrigger(m.child, m.enableTrigger);
        }

        private GameObject ResolveTarget(string name_)
        {
            GameObject go;
            if (m_targetCache.TryGetValue(name_, out go) && go != null)
                return go;
            go = GameObject.Find(name_);
            if (go != null)
                m_targetCache[name_] = go;
            else
                WarnResolveOnce(name_, "目标未命中（被删/改名？）");
            return go;
        }

        /// <summary>解析失败告警去重（每名字每会话一次）。</summary>
        private void WarnResolveOnce(string key, string reason)
        {
            if (m_resolveWarned == null)
                m_resolveWarned = new HashSet<string>();
            if (!m_resolveWarned.Add(key + "|" + reason))
                return;
            StubLog.LogWarn("[CoaxialButtonGroup] " + name + " 解析失败 " + key + "：" + reason);
        }
    }
}
