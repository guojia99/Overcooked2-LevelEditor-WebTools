using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 随机触发器（3.9.0）：一个信号输入 → 按权重从候选列表随机选一个发出。
    /// 最终信号 = 信号名 + 分隔符（默认 ":"）+ 参数值（参数可编程：固定 / 候选 /
    /// 随机字符串 / 随机 N 位数值 / 数值区间）；参数未配置时只发信号名。
    ///
    /// 输入双通道（任选其一或组合）：
    ///  - 信号输入：公有 OnTrigger(string)（SendMessage 生态——事件组 SMB /
    ///    BLRelay 分发 / TriggerPasser / 其他 RandomTrigger 级联均可达）；
    ///    ⚠ stub 组件收不到 vanilla 接口通道（GameObjectUtils.SendTrigger 只认
    ///    ITriggerReceiver，ConveyorDirectionSync 事故铁律），vanilla 源需经
    ///    SendMessage 中继转发。m_inputTrigger 空 = 匹配任意输入信号。
    ///  - 按钮直连：m_buttonRootNames 轮询 TriggerDisableScript.m_script.enabled
    ///    下降沿（SwitchReenable/CoaxialButtonGroup 同款等价轮询），任一按钮按下
    ///    即掷骰；按钮回绿由既有自动复位链管理，本组件不碰按钮生命周期。
    ///
    /// 冷却限流：m_cooldownSeconds &gt; 0 时，一次成功掷发后 N 秒内忽略一切输入
    /// （0 = 不限流）。掷骰与派发仅服务端执行（GameApi.IsServerMachine，
    /// ButtonLogicRelay/Coaxial 同语义——两端各掷必然分叉，服务端单点发出后
    /// 效果经 vanilla 网络免费复制到客户端）。
    ///
    /// 派发：目标物体 SendMessage("OnTrigger", 最终信号)（vanilla Server* 公有
    /// OnTrigger 与 stub 组件双可达的超集通道，BLRelay 同款）；目标名空 = 发给
    /// 自身所在物体（自环防抖：派发期间的回流信号直接丢弃）。目标解析
    /// GameObject.Find + 字典缓存 + 假 null 修剪。
    ///
    /// 数据双通道：本组件（烘焙反射挂载，编辑器侧）+ tag 载体
    /// RandomTrigger|I:&lt;输入&gt;|C:&lt;冷却&gt;|S:&lt;分隔符&gt;|N:&lt;按钮,..&gt;
    /// |E:&lt;信号&gt;,&lt;权重&gt;,&lt;目标&gt;[,&lt;参数spec&gt;];..
    /// （%,;&gt;|/@ 做 %XX 转义，RandomTriggerBakery 编码对称；EntryPoint 自愈解析）。
    /// 参数 spec：f 固定 / p候选/候选 / s长度[@字符集] / d位数 / r最小/最大/小数位。
    /// </summary>
    public class RandomTrigger : MonoBehaviour
    {
        public const string TagPrefix = "RandomTrigger|";

        private const string Version = "v1(" + StubVersion.Value + ")";

        // ---- 参数模式（ParamSpec.mode）
        public const int ParamNone = 0;         // 无参数：只发信号名
        public const int ParamFixed = 1;        // 固定字符串
        public const int ParamPick = 2;         // 从候选列表随机选一个
        public const int ParamRandomString = 3; // 随机字符串（长度 + 可选字符集）
        public const int ParamDigits = 4;       // 随机 N 位整数
        public const int ParamRange = 5;        // 数值区间 [min,max]（可含小数位）

        /// <summary>随机字符串默认字符集（大小写字母 + 数字）。</summary>
        public const string DefaultCharset =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        /// <summary>参数值生成规则（[Serializable] 供烘焙与 Inspector 编辑）。</summary>
        [Serializable]
        public class ParamSpec
        {
            public int mode;                    // ParamNone..ParamRange
            public string fixedValue = "";      // ParamFixed
            public string[] candidates;         // ParamPick（≥2 由编辑器校验）
            public int stringLength = 4;        // ParamRandomString（1~64 钳制）
            public string charset = "";         // ParamRandomString（空 = DefaultCharset）
            public int digits = 1;              // ParamDigits（1~9 钳制）
            public float minValue;              // ParamRange
            public float maxValue = 1f;         // ParamRange
            public int decimals;                // ParamRange 小数位（0 = 整数，0~6 钳制）
        }

        /// <summary>候选输出（信号名 + 参数规则 + 权重 + 目标）。</summary>
        [Serializable]
        public class Entry
        {
            public string triggerName = "";     // 信号名（非空，编辑器校验）
            public ParamSpec param;             // null / mode=ParamNone = 无参数
            public float weight = 1f;           // 权重（≥0；全 0 视为等权）
            public string targetName = "";      // 目标物体名（空 = 自身物体）
        }

        /// <summary>输入信号名（空 = 匹配任意输入）。</summary>
        public string m_inputTrigger = "";
        /// <summary>冷却限流秒数（触发一次后 N 秒内忽略输入；0 = 不限流）。</summary>
        public float m_cooldownSeconds;
        /// <summary>参数分隔符（信号名与参数值之间的连接，默认 ":"）。</summary>
        public string m_separator = ":";
        /// <summary>源按钮伪根物体名（GameObject.Find 身份键；空数组 = 纯信号输入）。</summary>
        public string[] m_buttonRootNames;
        /// <summary>候选列表（≥2 由编辑器校验）。</summary>
        public Entry[] m_entries;

        private class Member
        {
            public string rootName;
            public GameObject child;
            public Behaviour watched;
            public bool lastEnabled = true;
            public bool bound;
        }

        private readonly List<Member> m_members = new List<Member>();
        private readonly Dictionary<string, GameObject> m_targetCache =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private HashSet<string> m_resolveWarned;

        private float m_lastFireTime = -999999f;
        private bool m_dispatching;          // 自环防抖（目标=自身时 SendMessage 回流）
        private bool m_cooldownLoggedOnce;
        private bool m_noEntryLogged;
        private static bool s_loggedSelfCheck;
        private static bool s_loggedVersion;

        private void OnTrigger(string trigger)
        {
            // SendMessage 生态入口（BLRelay/事件组 SMB/TriggerPasser/RT 级联）。
            if (m_dispatching)
                return; // 自己发出的信号回流（目标=自身），丢弃防自环
            if (!string.IsNullOrEmpty(m_inputTrigger) && trigger != m_inputTrigger)
                return;
            TryFire("信号[" + (trigger ?? "") + "]");
        }

        private IEnumerator Start()
        {
            if (!s_loggedVersion)
            {
                s_loggedVersion = true;
                StubLog.Log("[RandomTrigger] " + Version + " 就绪（随机信号分发器）");
            }
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
                    StubLog.LogWarn("[RandomTrigger] 协程异常退出: " + name + "\n" + ex);
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
                StubLog.Dbg("[RandomTrigger] 反射自检: TriggerDisableScript=" +
                    (GameApi.TriggerDisableType != null) + " SendTrigger=" +
                    (GameApi.SendTriggerMethod != null));
            }

            m_members.Clear();
            foreach (var rootName in m_buttonRootNames ?? new string[0])
            {
                if (string.IsNullOrEmpty(rootName)) continue;
                m_members.Add(new Member { rootName = rootName });
            }
            if (m_members.Count == 0)
            {
                // 纯信号输入模式：事件驱动（OnTrigger），无轮询开销。
                StubLog.Log("[RandomTrigger] " + name + " 启动（信号输入模式）｜输入=" +
                    (string.IsNullOrEmpty(m_inputTrigger) ? "<任意>" : m_inputTrigger) +
                    "｜冷却 " + m_cooldownSeconds.ToString("0.##", CultureInfo.InvariantCulture) +
                    "s｜候选 " + (m_entries != null ? m_entries.Length : 0) + " 个｜分隔符 \"" +
                    (m_separator ?? "") + "\"");
                yield break;
            }

            // ---- 绑定阶段：全部源按钮的 TriggerDisableScript 就绪才进入主循环
            //（未就绪 = 未按下检测不可用；缺按钮不阻断其余功能，告警后继续等）。
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
                    StubLog.LogWarn("[RandomTrigger] " + name + " 有 " + unbound +
                        " 个源按钮 20s 未就绪（被删/改名/非按钮？），降为 1s 轮询继续等待");
                }
                yield return new WaitForSeconds(slowPoll ? 1f : 0.25f);
            }

            StubLog.Log("[RandomTrigger] " + name + " 启动（按钮直连 × " + m_members.Count +
                "）｜输入=" + (string.IsNullOrEmpty(m_inputTrigger) ? "<任意>" : m_inputTrigger) +
                "｜冷却 " + m_cooldownSeconds.ToString("0.##", CultureInfo.InvariantCulture) +
                "s｜候选 " + (m_entries != null ? m_entries.Length : 0) + " 个｜分隔符 \"" +
                (m_separator ?? "") + "\"");

            // ---- 主循环：源按钮下降沿检测（按下 = 掷骰）
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
                        TryFire("按钮[" + m.rootName + "]");
                    m.lastEnabled = nowEnabled;
                }
                yield return null;
            }
        }

        /// <summary>绑定单个源按钮：子树扫 TriggerDisableScript（vanilla，真机可解析）。</summary>
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
            m.lastEnabled = watched.enabled;
            m.bound = true;
            return true;
        }

        // ---------------------------------------------------------------- 掷骰与派发

        /// <summary>冷却检查 → 服务端权威掷骰 → 组装最终信号 → SendMessage 派发。</summary>
        private void TryFire(string source)
        {
            if (m_entries == null || m_entries.Length == 0)
            {
                if (!m_noEntryLogged)
                {
                    m_noEntryLogged = true;
                    StubLog.LogWarn("[RandomTrigger] " + name + " 无候选配置，忽略输入（后续静默）");
                }
                return;
            }
            if (m_cooldownSeconds > 0f && Time.time - m_lastFireTime < m_cooldownSeconds)
            {
                if (!m_cooldownLoggedOnce)
                {
                    m_cooldownLoggedOnce = true;
                    StubLog.Log("[RandomTrigger] " + name + " 冷却限流（" +
                        m_cooldownSeconds.ToString("0.##", CultureInfo.InvariantCulture) +
                        "s）内忽略输入（后续静默）｜来源 " + source);
                }
                else
                {
                    StubLog.Dbg("[RandomTrigger] " + name + " 冷却限流忽略输入｜来源 " + source);
                }
                return;
            }
            if (!GameApi.IsServerMachine())
                return; // 客户端不掷骰：服务端单点发出，效果经 vanilla 网络复制

            int index = RollIndex();
            if (index < 0) return;
            var entry = m_entries[index];

            string param = ResolveParam(entry.param);
            string final = entry.triggerName ?? "";
            if (param != null)
                final = final + (string.IsNullOrEmpty(m_separator) ? ":" : m_separator) + param;

            GameObject target = string.IsNullOrEmpty(entry.targetName)
                ? gameObject
                : ResolveTarget(entry.targetName);
            if (target == null)
            {
                // 目标未命中的掷骰不占用冷却（没发出去不算触发过），下次输入可立刻重掷。
                StubLog.LogWarn("[RandomTrigger] " + name + " 掷中 #" + index + " 但目标 \"" +
                    entry.targetName + "\" 未命中（被删/改名？），本次丢弃");
                return;
            }

            int fired = 0;
            try
            {
                m_dispatching = true;
                target.SendMessage("OnTrigger", final, SendMessageOptions.DontRequireReceiver);
                fired = 1;
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[RandomTrigger] " + name + " 派发异常 " + target.name + ": " + ex.Message);
            }
            finally
            {
                m_dispatching = false;
            }
            if (fired > 0)
            {
                // 冷却只在成功派发后起算（语义：没发出去不算触发过）。
                m_lastFireTime = Time.time;
                StubLog.Log("[RandomTrigger] " + name + " " + source + " → 掷中 #" + (index + 1) + "/" +
                    m_entries.Length + "（w=" + entry.weight.ToString("0.##", CultureInfo.InvariantCulture) +
                    "）→ \"" + final + "\" → " + target.name);
            }
        }

        /// <summary>加权独立掷骰：权重 &gt;0 求和按比例；全 0/负视为等权。
        /// 防御：跳过 null / 触发名为空的坏条目（Inspector 手改数组可留空槽，
        /// 命中会 NRE 中断后续触发）——等权与兜底都只在有效项里取。</summary>
        private int RollIndex()
        {
            var entries = m_entries;
            float total = 0f;
            bool anyPositive = false;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null) continue;
                if (entries[i].weight > 0f) { anyPositive = true; total += entries[i].weight; }
            }
            if (!anyPositive)
            {
                var valid = new List<int>();
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i] != null && !string.IsNullOrEmpty(entries[i].triggerName))
                        valid.Add(i);
                }
                if (valid.Count == 0) return -1;
                return valid[UnityEngine.Random.Range(0, valid.Count)];
            }
            float r = UnityEngine.Random.value * total;
            float acc = 0f;
            int last = -1;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null || entries[i].weight <= 0f) continue;
                last = i;
                acc += entries[i].weight;
                if (r <= acc) return i;
            }
            return last; // 浮点边界兜底（最后一个有效项；anyPositive 保证 ≥1）
        }

        /// <summary>按 ParamSpec 生成参数值；返回 null = 无参数（只发信号名）。</summary>
        private string ResolveParam(ParamSpec p)
        {
            if (p == null || p.mode == ParamNone) return null;
            try
            {
                switch (p.mode)
                {
                    case ParamFixed:
                        return p.fixedValue ?? "";
                    case ParamPick:
                        {
                            var cands = p.candidates;
                            if (cands == null || cands.Length == 0) return null;
                            return cands[UnityEngine.Random.Range(0, cands.Length)];
                        }
                    case ParamRandomString:
                        {
                            int len = Mathf.Clamp(p.stringLength, 1, 64);
                            var cs = string.IsNullOrEmpty(p.charset) ? DefaultCharset : p.charset;
                            var sb = new StringBuilder(len);
                            for (int i = 0; i < len; i++)
                                sb.Append(cs[UnityEngine.Random.Range(0, cs.Length)]);
                            return sb.ToString();
                        }
                    case ParamDigits:
                        {
                            // N 位整数：[10^(N-1), 10^N - 1]；1 位 = 0~9（含 0）。
                            int d = Mathf.Clamp(p.digits, 1, 9);
                            long lo = 1;
                            for (int i = 1; i < d; i++) lo *= 10;
                            long hi = lo * 10 - 1;
                            if (d == 1) lo = 0;
                            double v = lo + UnityEngine.Random.value * (hi - lo + 1);
                            long n = (long)Math.Floor(v);
                            if (n > hi) n = hi;
                            return n.ToString(CultureInfo.InvariantCulture);
                        }
                    case ParamRange:
                        {
                            float min = p.minValue;
                            float max = p.maxValue;
                            if (max < min) { var t = min; min = max; max = t; }
                            double v = min + (double)UnityEngine.Random.value * (max - min);
                            int dec = Mathf.Clamp(p.decimals, 0, 6);
                            if (dec == 0)
                                return ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
                            return v.ToString("F" + dec.ToString(CultureInfo.InvariantCulture),
                                CultureInfo.InvariantCulture);
                        }
                }
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[RandomTrigger] " + name + " 参数生成异常（按无参数发出）: " + ex.Message);
            }
            return null;
        }

        // ---------------------------------------------------------------- 目标解析

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
            StubLog.LogWarn("[RandomTrigger] " + name + " 解析失败 " + key + "：" + reason);
        }
    }
}
