using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 按钮联动 helper 的「状态 → 消息」分发器（2026-09-24 v3）。
    ///
    /// 背景：ButtonLinkBakery 原先把「进入 Run_i 状态 → 向组根 i 发 BLGo_i」烘焙成
    /// controller 内嵌的 SendTriggerToObject StateMachineBehaviour——该 SMB 在
    /// Unity 2017.4 的资产往返中从未可靠持久化（回导 ReadSendTriggers 恒空、
    /// 运行期按压无分发），是「按钮无法触发动画组」的根因。场景组件的持久性
    /// 已被 helper 上的 TriggerOnAnimator 完成中继实证，故把分发逻辑移到本组件。
    ///
    /// 职责（挂在 Design/Button Logic/&lt;helper&gt; 上，与 Animator 同物体）：
    ///  - 轮询自身 Animator 当前状态：进入 m_stateNames 列出的 Run 状态时，
    ///    用 SendMessage("OnTrigger", goTrigger) 把并行列表里的触发名发给对应
    ///    组根（与宿主 SendTriggerToObject SMB 同款投递方式，ServerTimedQueue
    ///    可收）；每状态可配多条（共轭模式 ARun 同时启动 A 方各组）。
    ///  - 锁定（lockWhileRunning）：处于任一 Run 状态期间持续 ResetTrigger 全部
    ///    按压触发名（替代 ClearTriggerDuringState SMB，按压被吞不锁存）。
    ///  - 换状态时清空全部 done 触发名（防上一组迟到的 BLDone 在状态环回后
    ///    误触发下一轮过渡——原 ClearTrigger(done) SMB 的职责）。
    ///
    /// 联机：开关按压经宿主 TriggerOnAnimator 网络同步到各端 helper Animator；
    /// 本组件在每端都会跑，但 BLGo 只有服务器侧的 ServerTimedQueue 会响应
    /// （客户端没有 Server* 同步器，SendMessage 落空无害）——实际驱动仍是
    /// 服务器权威，与原版按钮链路一致。
    ///
    /// 按钮 child 解析双通道（2026-09-25 12:47 真机「按一次永红」残留修复）：
    ///  通道 1 = 伪根 PseudoPrefab.childGameObject（编辑器 Play 专用——该脚本
    ///  属编辑器程序集，真机 Missing Script）；通道 2 = 子树扫 TriggerDisableScript
    ///  （vanilla，真机可解析；按压受理时 child 必已实例化）。复位触发名按实例
    ///  m_enableTrigger 读取，不硬编码 "Reset"。
    /// </summary>
    public class ButtonLogicRelay : MonoBehaviour
    {
    /// <summary>按压触发名（顺序联动 1 个；共轭 2 个）。处于其封禁状态时持续吞掉。</summary>
    public string[] m_pressTriggers;
    /// <summary>与 m_pressTriggers 平行：封禁状态名列表（逗号分隔）。处于其中任一
    /// 状态时 ResetTrigger 对应按压名——顺序联动 = 全部 Run_*（锁定模式），
    /// 共轭 = 对方就绪态 + 双方运行态（互斥 + 不可再按）。</summary>
    public string[] m_pressBlockedStates;
    /// <summary>需要分发的 Animator 状态名（"Run_0".."Run_n" / "ARun" / "BRun"）。</summary>
    public string[] m_stateNames;
    /// <summary>与 m_stateNames 平行：进入该状态时要发的触发名。</summary>
    public string[] m_goTriggers;
    /// <summary>与 m_stateNames 平行：目标组根物体名（GameObject.Find）。</summary>
    public string[] m_targetNames;
        /// <summary>全部完成触发名（BLDone_*）：每次状态切换时清空防锁存。</summary>
        public string[] m_doneTriggers;
        /// <summary>配对按钮的伪根物体名（含主源/共控/共轭对方）。进入 Run 状态 →
        ///  向各按钮 child 发 "Disable"（按一个、双锁）；离开 Run（动画完成）→
        ///  向各按钮 child 发 "Reset"（一起变绿）。真机上这是纯 ButtonLink 按钮
        ///  唯一的回绿通道（SwitchReenable 只随机器联动烘焙，2026-09-25 真机
        ///  「按一次永红」事故根因）。</summary>
        public string[] m_buttonRootNames;
        /// <summary>互锁对（BtnPair）：在 AReady/BReady 切换时一升一降，而非共轭式同时 Reset。</summary>
        public bool m_interlockPair;
        public string m_interlockSideA;
        public string m_interlockSideB;

        private Animator m_animator;
        private int[] m_stateHashes;
        private int[][] m_blockedHashes;
        private int m_lastHash;
        private bool m_wasInRun;
        private int m_aReadyHash;
        private int m_bReadyHash;
        private bool m_interlockBootstrapped;
        private float m_runEnterTime = -1f;
        private bool m_runStuckWarned;
        private HashSet<string> m_resolveWarned;
        private readonly Dictionary<string, GameObject> m_targetCache =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject> m_buttonChildCache =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);

        /// <summary>Run 状态卡死告警阈值（秒）：动画完成信号（BLDone）丢失时按钮
        /// 永久红锁且零日志——超时告警把这类故障从「玩家口述」变成日志可见。</summary>
        private const float RunStuckWarnSeconds = 30f;

        /// <summary>按压最小间隔（秒，防抖）：任一次运行期总时长不足此值时，
        /// 回绿/换手延迟补足，且窗口内到达的按压触发一律吞掉（快速动画/脉冲
        /// 场景下连按只算一次，2026-09-28 防抖需求）。</summary>
        private const float MinPressIntervalSeconds = 0.35f;

        /// <summary>防抖窗口截止时刻（Time.time；-1 = 无窗口）。</summary>
        private float m_pressUnblockTime = -1f;

        private void Awake()
        {
            RebuildHashes();
        }

        private void OnEnable()
        {
            m_lastHash = 0;
            m_wasInRun = false;
            m_runEnterTime = -1f;
            m_runStuckWarned = false;
            m_interlockBootstrapped = false;
            m_pressUnblockTime = -1f;
        }

        private void Start()
        {
            BootstrapInterlockVisual();
            LogStartupSummary();
        }

        /// <summary>启动摘要（一次性，常开日志）：模式（互锁/共轭/顺序）、互锁 A/B
        /// 侧名、初始 Animator 状态、分发条数——「互锁配置是否真正生效」的第一
        /// 诊断行。若此处打出【共轭】而期望互锁，即 |X: tag / relay 字段断链
        /// （旧烘焙场景未重新写回，或 WriteRelayTag 未写入互锁侧）。</summary>
        private void LogStartupSummary()
        {
            if (m_animator == null)
                m_animator = GetComponent<Animator>();
            string mode;
            bool interlock = m_interlockPair;
            if (interlock)
                mode = "互锁（A=" + m_interlockSideA + " B=" + m_interlockSideB + "）";
            else if (m_buttonRootNames != null && m_buttonRootNames.Length > 1)
                mode = "共轭（" + m_buttonRootNames.Length + " 按钮）";
            else
                mode = "顺序/单源";
            int dispatch = m_stateNames != null ? m_stateNames.Length : 0;
            string line = "[ButtonLogicRelay] " + name + " 启动：模式=" + mode +
                "｜初始状态=" + CurrentStateLabel() + "｜分发 " + dispatch + " 条";
            // 互锁/共轭是玩法级配置（每关数量少）→ 常开；顺序联动可能很多条 → 诊断级。
            if (interlock || (m_buttonRootNames != null && m_buttonRootNames.Length > 1))
                StubLog.Log(line);
            else
                StubLog.Dbg(line);
        }

        /// <summary>当前 Animator 状态的可读名（已知状态名比对；未知打 hash）。</summary>
        private string CurrentStateLabel()
        {
            if (m_animator == null)
                return "无 Animator";
            var si = m_animator.GetCurrentAnimatorStateInfo(0);
            if (si.shortNameHash == m_aReadyHash) return "AReady";
            if (si.shortNameHash == m_bReadyHash) return "BReady";
            if (m_stateHashes != null && m_stateNames != null)
            {
                for (int i = 0; i < m_stateHashes.Length && i < m_stateNames.Length; i++)
                {
                    if (si.shortNameHash == m_stateHashes[i])
                        return m_stateNames[i];
                }
            }
            return "state#" + si.shortNameHash;
        }

        /// <summary>烘焙器写完字段后调用（反射）；也可在 Inspector 改动后手动生效。</summary>
        public void RebuildHashes()
        {
            m_stateHashes = new int[m_stateNames == null ? 0 : m_stateNames.Length];
            for (int i = 0; i < m_stateHashes.Length; i++)
                m_stateHashes[i] = Animator.StringToHash(m_stateNames[i]);
            m_blockedHashes = new int[m_pressBlockedStates == null ? 0 : m_pressBlockedStates.Length][];
            for (int i = 0; i < m_blockedHashes.Length; i++)
            {
                var parts = (m_pressBlockedStates[i] ?? "").Split(',');
                var list = new List<int>();
                for (int j = 0; j < parts.Length; j++)
                {
                    var nm = parts[j].Trim();
                    if (nm.Length > 0)
                        list.Add(Animator.StringToHash(nm));
                }
                m_blockedHashes[i] = list.ToArray();
            }
            m_targetCache.Clear();
            m_aReadyHash = Animator.StringToHash("AReady");
            m_bReadyHash = Animator.StringToHash("BReady");
        }

        private void BootstrapInterlockVisual()
        {
            if (!m_interlockPair || m_interlockBootstrapped)
                return;
            if (m_animator == null)
                m_animator = GetComponent<Animator>();
            if (m_animator == null)
                return;
            var si = m_animator.GetCurrentAnimatorStateInfo(0);
            if (si.shortNameHash == m_aReadyHash)
                ApplyInterlockVisual(true);
            else if (si.shortNameHash == m_bReadyHash)
                ApplyInterlockVisual(false);
            m_interlockBootstrapped = true;
        }

        private void Update()
        {
            if (m_animator == null)
                m_animator = GetComponent<Animator>();
            if (m_animator == null)
                return;
            if (m_stateHashes == null || m_stateHashes.Length == 0)
                RebuildHashes();

            var si = m_animator.GetCurrentAnimatorStateInfo(0);

            // 按压封禁：处于封禁状态或防抖窗口内时持续 ResetTrigger（脚本 Update
            // 先于 Animator 求值，本帧到达的 SetTrigger 会在被消费前清掉——吞掉
            // 且不锁存）。
            if (m_pressTriggers != null && m_blockedHashes != null)
            {
                bool debouncing = Time.time < m_pressUnblockTime;
                for (int i = 0; i < m_pressTriggers.Length && i < m_blockedHashes.Length; i++)
                {
                    if (string.IsNullOrEmpty(m_pressTriggers[i]))
                        continue;
                    bool swallow = debouncing;
                    if (!swallow)
                    {
                        var blocked = m_blockedHashes[i];
                        for (int j = 0; j < blocked.Length; j++)
                        {
                            if (si.shortNameHash == blocked[j])
                            {
                                swallow = true;
                                break;
                            }
                        }
                    }
                    if (swallow)
                        m_animator.ResetTrigger(m_pressTriggers[i]);
                }
            }

            if (si.shortNameHash == m_lastHash)
                return;
            m_lastHash = si.shortNameHash;

            bool nowInRun = false;
            for (int i = 0; i < m_stateHashes.Length; i++)
                if (si.shortNameHash == m_stateHashes[i]) { nowInRun = true; break; }

            // 换状态：清空全部 done 触发名（新组的 BLDone 在进入之后才会到达，
            // 不会被误清；防止迟到的旧 BLDone 在环回后误触发）。
            if (m_doneTriggers != null)
            {
                for (int i = 0; i < m_doneTriggers.Length; i++)
                {
                    if (!string.IsNullOrEmpty(m_doneTriggers[i]))
                        m_animator.ResetTrigger(m_doneTriggers[i]);
                }
            }

            if (m_interlockPair)
            {
                if (si.shortNameHash == m_aReadyHash)
                    ApplyInterlockVisual(true, m_wasInRun ? RunDeficit() : 0f);
                else if (si.shortNameHash == m_bReadyHash)
                    ApplyInterlockVisual(false, m_wasInRun ? RunDeficit() : 0f);
                if (nowInRun && !m_wasInRun)
                {
                    m_runEnterTime = Time.time;
                    m_runStuckWarned = false;
                    SendToPairedButtons("Disable");
                }
                else if (!nowInRun && m_wasInRun)
                {
                    // 换手防抖：动画快于最短间隔时，登记吞按压窗口（抬起侧的
                    // 延迟回绿由 ApplyInterlockVisual 的 upDelay 承担）。
                    SchedulePressDebounce(RunDeficit());
                    m_runEnterTime = -1f;
                }
            }
            else if (m_buttonRootNames != null && m_buttonRootNames.Length > 0)
            {
                // 共轭：进入 Run → 全部配对按钮 Disable；离开 Run（动画完成）→ 全部 Reset
                // （一起变绿）。运行期不足最短间隔时延迟 Reset 补足防抖。
                if (nowInRun && !m_wasInRun)
                {
                    m_runEnterTime = Time.time;
                    m_runStuckWarned = false;
                    SendToPairedButtons("Disable");
                }
                else if (!nowInRun && m_wasInRun)
                {
                    float deficit = RunDeficit();
                    SchedulePressDebounce(deficit);
                    m_runEnterTime = -1f;
                    if (deficit > 0f)
                    {
                        StartCoroutine(PairedButtonsLater("Reset", deficit));
                        StubLog.Log("[ButtonLogicRelay] " + name + " 运行期不足 " +
                            MinPressIntervalSeconds.ToString("0.##") + "s，延迟 " +
                            deficit.ToString("0.##") + "s 回绿（防抖）");
                    }
                    else
                        SendToPairedButtons("Reset");
                }
            }
            m_wasInRun = nowInRun;

            // Run 卡死看门狗：BLDone 丢失（组被删/TriggerQueue 断线/完成中继失效）
            // 时按钮永久红锁且此前零日志。超时一次告警，恢复（离开 Run）自动复位。
            if (m_wasInRun && m_runEnterTime >= 0f && !m_runStuckWarned &&
                Time.time - m_runEnterTime > RunStuckWarnSeconds)
            {
                m_runStuckWarned = true;
                StubLog.LogWarn("[ButtonLogicRelay] " + name + " 停留在运行态超过 " +
                    (int)RunStuckWarnSeconds + "s——完成信号（BLDone）疑似丢失，按钮将保持红锁" +
                    "（检查动画组成员与完成中继接线）");
            }

            for (int i = 0; i < m_stateHashes.Length; i++)
            {
                if (si.shortNameHash != m_stateHashes[i] || i >= m_goTriggers.Length)
                    continue;
                var target = ResolveTarget(i);
                if (target == null)
                    continue;
                DispatchGo(i, target);
            }
        }

        // ---- 3.6.0 主机侧 BLGo 派发门控（vanilla ServerTriggerAnimationOnConveyor
        //      的 Pending 语义复刻 + 节点环防重入） ----
        //
        // 背景：vanilla 旋转传送带从不边送边转——Pending 态等 !IsConveying() &&
        // !IsReceiving() 才进 Animating 并广播；自定义按钮动画组链路此前没有该
        // 门控，投递在途时旋转会让旧接收器 m_receiving 永久卡 true（2026-09-24
        // 整排传送卡死事故同源风险）。另一层：目标组还在播上一节点（或对账
        // 未平）时立即重入队列，会放大联机同帧合并丢步窗口。
        //
        // 仅主机侧生效（客户端 BLGo 无 Server 同步器响应，落空无害，维持直发）；
        // 门控等待 = 玩家侧感知为「按下后动画稍迟启动」，与 vanilla 传送带行为
        // 一致；超时 fail-open 强发（行为同旧版）并告警可见。

        /// <summary>门控轮询间隔（秒）。</summary>
        private const float DispatchPollSeconds = 0.05f;
        /// <summary>门控等待超时（秒）：覆盖 0.4s 节点动画 + 1/speed 投递 + 余量；
        /// 超时强发（fail-open）+ 告警。</summary>
        private const float DispatchGateTimeoutSeconds = 2f;
        /// <summary>按目标名累计的待发 BLGo 数（同一目标单泵协程，门控期间新到
        /// 的按压累计而非丢弃——helper 出 Run 后封锁按压，正常时序不会到这里，
        /// 防极端等待窗口的第二按丢失）。</summary>
        private readonly Dictionary<string, int> m_goQueued =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private void DispatchGo(int i, GameObject target)
        {
            if (!GameApi.IsServerMachine())
            {
                // 客机：BLGo 落空无害（无 Server 同步器），直发维持原行为。
                target.SendMessage("OnTrigger", m_goTriggers[i], SendMessageOptions.DontRequireReceiver);
                return;
            }
            string reason;
            if (CanDispatchNow(target, out reason))
            {
                target.SendMessage("OnTrigger", m_goTriggers[i], SendMessageOptions.DontRequireReceiver);
                StubLog.Dbg("[ButtonLogicRelay] " + name + " 进 " + m_stateNames[i] +
                    " → 发 " + m_goTriggers[i] + " 给 " + m_targetNames[i]);
                return;
            }
            int queued;
            if (m_goQueued.TryGetValue(target.name, out queued))
            {
                m_goQueued[target.name] = queued + 1;
                StubLog.Log("[ButtonLogicRelay] " + name + " 派发门控累计：" + m_goTriggers[i] +
                    " → " + target.name + "（" + reason + "，待发 " + (queued + 1) + " 次）");
                return;
            }
            m_goQueued[target.name] = 1;
            StartCoroutine(DispatchWhenReady(i, target, reason));
        }

        private bool CanDispatchNow(GameObject target, out string reason)
        {
            reason = null;
            var ring = target.GetComponent<NodeRingSync>();
            if (ring != null && ring.IsBusy())
            {
                reason = "节点动画进行中/对账未平";
                return false;
            }
            if (HasConveyingMember(target))
            {
                reason = "传送带在途投递";
                return false;
            }
            return true;
        }

        private IEnumerator DispatchWhenReady(int i, GameObject target, string reason)
        {
            float startedAt = Time.time;
            while (true)
            {
                float deadline = Time.time + DispatchGateTimeoutSeconds;
                string block = reason;
                bool open = false;
                while (Time.time < deadline)
                {
                    if (target == null)
                    {
                        m_goQueued.Remove(m_targetNames[i]);
                        yield break; // 目标被删
                    }
                    if (CanDispatchNow(target, out block))
                    {
                        open = true;
                        break;
                    }
                    yield return new WaitForSeconds(DispatchPollSeconds);
                }
                if (!open && target != null && !CanDispatchNow(target, out block))
                {
                    StubLog.LogWarn("[ButtonLogicRelay] " + name + " 派发门控超时 " +
                        DispatchGateTimeoutSeconds + "s 强发 " + m_goTriggers[i] + " → " +
                        m_targetNames[i] + "（阻塞原因=" + block + "，fail-open=行为同旧版）");
                }
                if (target == null)
                {
                    m_goQueued.Remove(m_targetNames[i]);
                    yield break;
                }
                target.SendMessage("OnTrigger", m_goTriggers[i], SendMessageOptions.DontRequireReceiver);
                StubLog.Log("[ButtonLogicRelay] " + name + " 延迟派发 " + m_goTriggers[i] +
                    " → " + m_targetNames[i] + "（" + reason + "，等待 " +
                    (Time.time - startedAt).ToString("0.##") + "s 后放行）");
                int remaining;
                if (!m_goQueued.TryGetValue(target.name, out remaining) || remaining <= 1)
                {
                    m_goQueued.Remove(target.name);
                    yield break;
                }
                m_goQueued[target.name] = remaining - 1;
                startedAt = Time.time;
                // 继续服务累计的后续 BLGo（等待原因重新判定）。
                reason = "连续按压累计";
            }
        }

        // ---- ServerConveyorStation 投递状态反射（组件名扫描 + 懒缓存，
        //      ConveyorDirectionSync 同款模式；ServerConveyorStation 只在主机存在，
        //      与门控仅主机执行一致） ----
        private static System.Reflection.MethodInfo s_isConveyingMethod;
        private static System.Reflection.FieldInfo s_receivingField;
        private static bool s_stationReflectionResolved;
        private static bool s_stationReflectionWarned;

        /// <summary>组根子树内是否有正在投递/被投递的传送带站（IsConveying() ||
        /// m_receiving）。反射失败 = 不门控（fail-open，行为同旧版）。</summary>
        private static bool HasConveyingMember(GameObject groupRoot)
        {
            if (groupRoot == null)
                return false;
            var components = groupRoot.GetComponentsInChildren<Component>(true);
            for (int c = 0; c < components.Length; c++)
            {
                var st = components[c];
                if (st == null || st.GetType().Name != "ServerConveyorStation")
                    continue;
                if (!s_stationReflectionResolved)
                {
                    s_stationReflectionResolved = true;
                    var type = st.GetType();
                    s_isConveyingMethod = type.GetMethod("IsConveying",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    s_receivingField = type.GetField("m_receiving",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }
                try
                {
                    if (s_isConveyingMethod != null && (bool)s_isConveyingMethod.Invoke(st, null))
                        return true;
                    if (s_receivingField != null && s_receivingField.FieldType == typeof(bool) &&
                        (bool)s_receivingField.GetValue(st))
                        return true;
                }
                catch (Exception ex)
                {
                    if (!s_stationReflectionWarned)
                    {
                        s_stationReflectionWarned = true;
                        StubLog.LogWarn("[ButtonLogicRelay] 传送带投递状态反射异常（门控降级 fail-open）: " +
                            ex.Message);
                    }
                    return false;
                }
            }
            return false;
        }

        private void OnDisable()
        {
            m_goQueued.Clear();
        }

        /// <summary>互锁：A 抬起时 B 按下，反之亦然。每次翻转打投递结果
        /// （✓/✗）——状态机已换手但按钮外观未变时，靠这行区分「消息没发出」
        /// （✗，见 WarnResolveOnce 的根名/child 解析失败）与「已发出」（✓，
        /// 则是按钮 child 侧接收问题）。upDelay：抬起侧延迟回绿（运行期不足
        /// 最短按压间隔时的防抖补足；按下侧总是立即）。</summary>
        private void ApplyInterlockVisual(bool aUp, float upDelay = 0f)
        {
            bool aSent = true;
            bool bSent = true;
            if (!string.IsNullOrEmpty(m_interlockSideA))
            {
                if (aUp && upDelay > 0f)
                    StartCoroutine(ButtonSendLater(m_interlockSideA, "Reset", upDelay));
                else
                    aSent = SendToButtonRoot(m_interlockSideA, aUp ? "Reset" : "Disable");
            }
            if (!string.IsNullOrEmpty(m_interlockSideB))
            {
                if (!aUp && upDelay > 0f)
                    StartCoroutine(ButtonSendLater(m_interlockSideB, "Reset", upDelay));
                else
                    bSent = SendToButtonRoot(m_interlockSideB, aUp ? "Disable" : "Reset");
            }
            StubLog.Log("[ButtonLogicRelay] " + name + " 互锁翻转：" +
                (aUp ? "A 抬起 / B 按下" : "B 抬起 / A 按下") +
                "（A " + DeliveryMark(aSent) + "，B " + DeliveryMark(bSent) + ")" +
                (upDelay > 0f ? "，抬起侧延迟 " + upDelay.ToString("0.##") + "s 回绿（防抖）" : ""));
        }

        private static string DeliveryMark(bool sent)
        {
            return sent ? "✓" : "✗ 未投递";
        }

        /// <summary>运行期距最短按压间隔的差额（秒；已足额或无记录 = 0）。</summary>
        private float RunDeficit()
        {
            if (m_runEnterTime < 0f)
                return 0f;
            float d = MinPressIntervalSeconds - (Time.time - m_runEnterTime);
            return d > 0f ? d : 0f;
        }

        /// <summary>登记防抖窗口：窗口内到达的按压触发一律吞掉（与封禁状态吞除
        /// 同一通道，见 Update 开头）。</summary>
        private void SchedulePressDebounce(float deficit)
        {
            if (deficit <= 0f)
                return;
            float until = Time.time + deficit;
            if (until > m_pressUnblockTime)
                m_pressUnblockTime = until;
        }

        private IEnumerator PairedButtonsLater(string trigger, float delay)
        {
            yield return new WaitForSeconds(delay);
            SendToPairedButtons(trigger);
        }

        private IEnumerator ButtonSendLater(string rootName, string trigger, float delay)
        {
            yield return new WaitForSeconds(delay);
            SendToButtonRoot(rootName, trigger);
        }

        /// <summary>向全部配对按钮的 child 广播 Disable/Reset（伪根→PseudoPrefab.
        /// childGameObject 反射解析，真机回落扫描解析，懒加载缓存）。</summary>
        private void SendToPairedButtons(string trigger)
        {
            if (m_buttonRootNames == null || m_buttonRootNames.Length == 0)
                return;
            int sent = 0;
            for (int i = 0; i < m_buttonRootNames.Length; i++)
            {
                if (SendToButtonRoot(m_buttonRootNames[i], trigger))
                    sent++;
            }
            // sent/total 全量打点：部分投递失败（sent < total）一眼可见，
            // 失败明细由 WarnResolveOnce 一次性告警补充（2026-09-25 12:47 真机
            // 事故的教训——原实现 sent==0 时完全静默，日志零线索）。
            StubLog.Log("[ButtonLogicRelay] " + name + " → " + sent + "/" + m_buttonRootNames.Length +
                " 个按钮 " + (trigger == "Disable" ? "锁定（动画进行中）" : "解锁（动画完成，变绿）"));
        }

        /// <summary>读按钮 child 子树 TriggerDisableScript 的复位触发名
        /// （m_enableTrigger；缺失回落 "Reset"）。</summary>
        private string ResolveEnableTrigger(GameObject child)
        {
            var script = FindDisableScript(child);
            if (script != null && GameApi.DisableEnableTriggerField != null)
            {
                var t = GameApi.DisableEnableTriggerField.GetValue(script) as string;
                if (!string.IsNullOrEmpty(t))
                    return t;
            }
            return "Reset";
        }

        private bool SendToButtonRoot(string rootName, string trigger)
        {
            var child = ResolveButtonChild(rootName);
            if (child == null)
                return false;
            var fire = trigger == "Reset" ? ResolveEnableTrigger(child) : trigger;
            child.SendMessage("OnTrigger", fire, SendMessageOptions.DontRequireReceiver);
            return true;
        }

        private GameObject ResolveButtonChild(string rootName)
        {
            if (string.IsNullOrEmpty(rootName))
                return null;
            GameObject cached;
            if (m_buttonChildCache.TryGetValue(rootName, out cached) && cached != null)
                return cached;
            var root = GameObject.Find(rootName);
            if (root == null)
            {
                WarnResolveOnce(rootName, "GameObject.Find 未命中（根被删/改名/未激活）");
                return null;
            }
            // 通道 1（编辑器 Play）：伪根上 PseudoPrefab.childGameObject（LevelEditor
            // 命名空间，反射取）。真机该脚本属编辑器专用程序集，不随包分发——
            // Missing Script 死件，本通道静默失败（2026-09-25 12:47 真机日志实证：
            // Disable/Reset 全部无人投递 → 纯 ButtonLink 按钮按一次永红）。
            Component pseudo = null;
            foreach (var c in root.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == "PseudoPrefab")
                {
                    pseudo = c;
                    break;
                }
            }
            var childField = pseudo != null
                ? pseudo.GetType().GetField("childGameObject")
                : null;
            var child = childField != null ? childField.GetValue(pseudo) as GameObject : null;
            if (child == null)
            {
                // 通道 2（真机兜底）：按钮 child 由加载器在运行时实例化到伪根下，
                // 直接扫子树 TriggerDisableScript（vanilla 组件，真机可解析；按压
                // 受理时 child 必已就绪）——SwitchReenable.TryBind 同款、真机已
                // 验证的模式。Disable/Reset 的接收方正是该物体。
                var disableScript = FindDisableScript(root);
                if (disableScript == null)
                {
                    WarnResolveOnce(rootName, "根上无活 PseudoPrefab，子树也无 TriggerDisableScript" +
                        "（GameObject.Find 命中的可能不是按钮——名称撞车或场景为旧烘焙，重写回生成 _BL 唯一名可根治）");
                    return null;
                }
                child = disableScript.gameObject;
            }
            m_buttonChildCache[rootName] = child;
            return child;
        }

        /// <summary>对象子树里找 TriggerDisableScript（香草组件，真机可解析）。</summary>
        private Component FindDisableScript(GameObject go)
        {
            if (go == null || GameApi.TriggerDisableType == null)
                return null;
            var found = go.GetComponentsInChildren(GameApi.TriggerDisableType, true);
            return found.Length > 0 ? found[0] : null;
        }

        /// <summary>child 解析失败告警去重（每按钮根每会话一次）——Run 进出各投递
        /// 一次，持续刷屏会淹没日志。</summary>
        private void WarnResolveOnce(string rootName, string reason)
        {
            if (m_resolveWarned == null)
                m_resolveWarned = new HashSet<string>();
            if (!m_resolveWarned.Add(rootName))
                return;
            StubLog.LogWarn("[ButtonLogicRelay] 按钮 child 解析失败 " + name + " ← " + rootName +
                "：" + reason + "（Disable/Reset 无法投递，该按钮将无法回绿）");
        }

        private GameObject ResolveTarget(int i)
        {
            if (m_targetNames == null || i >= m_targetNames.Length ||
                string.IsNullOrEmpty(m_targetNames[i]))
                return null;
            var name = m_targetNames[i];
            GameObject go;
            if (m_targetCache.TryGetValue(name, out go) && go != null)
                return go;
            go = GameObject.Find(name);
            if (go != null)
                m_targetCache[name] = go;
            else
                StubLog.LogWarn("[ButtonLogicRelay] 找不到目标组根 " + name +
                    "（组被删除或改名？）");
            return go;
        }
    }
}
