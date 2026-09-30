using System.Collections.Generic;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 节点环动画「步数对账」校正器（3.6.0 联机偶发丢步修复）。
    ///
    /// 根因（联机偶发「传送带方向没转 / 差 90° / 动画没完整执行」）：按钮链每一步
    /// = 主机 ServerTimedQueue 广播一条 TimedQueueMessage(index=0)，全端
    /// ClientTimedQueue 到点 SetTrigger(BLPress)。网络突刺/时钟追赶把两条消息挤进
    /// 同一帧时，ClientTimedQueue.UpdateSynchronising 会一帧连发两次 DoEvent，两次
    /// SetTrigger(BLPress) 被 Mecanim 单值 Trigger 合并为一次状态过渡 → 该端永久
    /// 少推进一个节点（差 90°；ConveyorDirectionSync 的 ≤5° 停稳吸附救不了）。
    /// 单机走本地回环、事件天然分帧，必现不了——「联机偶发」的结构性原因。
    ///
    /// 修复（对账 + 瞬跳终态，2026-09-29 用户决策）：
    ///  - Harmony postfix（HarmonyPatches.ClientTimedQueueApplyServerEventPostfix /
    ///    ClientTriggerQueueDoEventPostfix，由 EntryPoint.EnsureNodeRingAuditPatches
    ///    按需安装）把「权威步数」（收到的 index==0 QueueEvent 计数——主机每次
    ///    BLGo 恰发一条）与「SetTrigger 次数」（诊断证据）交给本组件；
    ///  - 本组件每帧观察 Idle 序号沿环推进的位移，累计「本地步数」；
    ///  - 落后且已停在 Idle（非过渡、非播放中）→ Animator.Play(Idle_目标) 瞬跳
    ///    终态——Idle 定持 clip 是两键常值（精确节点姿态），Play 后立即对齐；
    ///    瞬跳同时把本地步数直接对齐权威（绕环多步 / 差 ≥ 环长同样一次闭合）；
    ///  - 本地超前只告警（不应发生；Cancel 消息会把权威回退对齐本地）。
    ///
    /// 主机也挂（本地回环同路径）：主机事件分帧从不合并，对账恒平 = 零介入，
    /// 且两端日志可对比（丢步直接证据 = SetTrigger 计数 > 本地步数）。
    ///
    /// 挂载：HealScene → HealNodeRingSync 扫 Design/Animated Objects 组根，免 tag。
    /// 判定信号 = controller 参数表含 "BLPress" 触发参数（烘焙器 AnimGroupBakery
    /// 只给 press 节点环添加该参数；非按钮的自动/机器联动组没有）——非节点环组
    /// 解析失败后自禁用，零每帧开销。
    /// </summary>
    public class NodeRingSync : MonoBehaviour
    {
        /// <summary>烘焙器 AnimGroupBakery.PressAdvanceTrigger 的镜像常量。</summary>
        public const string AdvanceParameter = "BLPress";

        /// <summary>HasState 探测上限（烘焙器节点环远小于此）。</summary>
        private const int MaxRingNodes = 32;

        /// <summary>补步动作节流（秒）：观察每帧跑（轻量），补步最多 5 次/秒，
        /// 防意外场景下的补-观察-补震荡。</summary>
        private const float AuditInterval = 0.2f;

        // ---- 静态注册表：postfix 按同步器 gameObject 实例 ID O(1) 寻址（零分配） ----
        private static readonly Dictionary<int, NodeRingSync> s_registry =
            new Dictionary<int, NodeRingSync>();

        /// <summary>postfix 寻址：同步器组件（与 TriggerQueue/本组件同物体的
        /// Client* 同步器）→ 对应校正器；无则 null（非节点环实体零开销）。</summary>
        internal static NodeRingSync FindFor(Component synchroniser)
        {
            if (synchroniser == null)
                return null;
            NodeRingSync sync;
            return s_registry.TryGetValue(synchroniser.gameObject.GetInstanceID(), out sync)
                ? sync : null;
        }

        private Animator m_animator;
        private Dictionary<int, int> m_idleIndexByHash;
        private int[] m_idleHashes;
        private bool m_ringResolved;
        private bool m_ringOk;

        /// <summary>权威步数：收到的 index==0 QueueEvent 计数（= 主机 BLGo 次数）。</summary>
        private int m_authoritativeSteps;
        /// <summary>本地步数：Idle 序号沿环推进位移的累计（+ 瞬跳补步）。</summary>
        private int m_localSteps;
        /// <summary>上一次观察到的 Idle 序号（-1 = 尚未见到 Idle）。</summary>
        private int m_lastIdleIndex = -1;
        /// <summary>SetTrigger(BLPress) 次数（DoEvent postfix 计数，仅诊断）。</summary>
        private int m_triggerFires;
        /// <summary>本地超前告警去重（每组件一次）。</summary>
        private bool m_aheadWarned;
        private float m_lastAuditTime;

        private void OnEnable()
        {
            s_registry[gameObject.GetInstanceID()] = this;
            m_ringResolved = false;
            m_ringOk = false;
            m_authoritativeSteps = 0;
            m_localSteps = 0;
            m_lastIdleIndex = -1;
            m_triggerFires = 0;
            m_aheadWarned = false;
            m_lastAuditTime = 0f;
        }

        private void OnDisable()
        {
            s_registry.Remove(gameObject.GetInstanceID());
        }

        private void Update()
        {
            if (!m_ringResolved)
                ResolveRing();
            if (!m_ringOk)
                return;
            ObserveStep();
            Audit();
        }

        /// <summary>判定并解析节点环：controller 参数表含 BLPress 触发参数 = 按钮
        /// press 节点环组；再按 Idle_0..Idle_n 顺序探测 HasState 建立序号表。
        /// 失败（非节点环组 / 无 Animator）自禁用——组件保留供 HealScene 判重，
        /// 注册表命中后 OnAuthoritativeEvent/OnTriggerFired 均 no-op。</summary>
        private void ResolveRing()
        {
            m_ringResolved = true;
            m_animator = GetComponent<Animator>();
            if (m_animator == null || m_animator.runtimeAnimatorController == null)
            {
                enabled = false;
                return;
            }
            bool hasAdvance = false;
            var parameters = m_animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Trigger &&
                    parameters[i].name == AdvanceParameter)
                {
                    hasAdvance = true;
                    break;
                }
            }
            if (!hasAdvance)
            {
                enabled = false; // 非按钮节点环（自动/机器联动组）：不介入
                return;
            }
            var idles = new List<int>();
            var indexByHash = new Dictionary<int, int>();
            for (int i = 0; i < MaxRingNodes; i++)
            {
                var hash = Animator.StringToHash("Idle_" + i);
                if (!m_animator.HasState(0, hash))
                    break;
                idles.Add(hash);
                indexByHash[hash] = i;
            }
            if (idles.Count == 0)
            {
                enabled = false;
                return;
            }
            m_idleHashes = idles.ToArray();
            m_idleIndexByHash = indexByHash;
            m_ringOk = true;
            StubLog.Dbg("[NodeRingSync] " + name + " 节点环就绪：" + m_idleHashes.Length +
                " 节点（角色=" + GameApi.RoleLabel() + "）");
        }

        /// <summary>观察 Idle 序号位移并计步。烘焙器环拓扑恒 +1 推进
        /// （Idle_i --BLPress--> 节点i --exit--> Idle_{i+1} 环回），位移按
        /// (cur-last+n)%n 归到 +1 方向。瞬跳前 m_lastIdleIndex 已先行登记目标，
        /// Play 后观察不到变化 → 不会重复计步。</summary>
        private void ObserveStep()
        {
            if (m_animator.IsInTransition(0))
                return; // 过渡帧 hash 不稳定，不采样
            var si = m_animator.GetCurrentAnimatorStateInfo(0);
            int idleIndex;
            if (!m_idleIndexByHash.TryGetValue(si.shortNameHash, out idleIndex))
                return; // 节点播放中
            if (m_lastIdleIndex < 0)
            {
                m_lastIdleIndex = idleIndex;
                return;
            }
            if (idleIndex == m_lastIdleIndex)
                return;
            int n = m_idleHashes.Length;
            int delta = idleIndex - m_lastIdleIndex;
            if (delta < 0)
                delta += n;
            m_localSteps += delta;
            m_lastIdleIndex = idleIndex;
        }

        /// <summary>对账补步：落后且停在 Idle → 瞬跳终态。正在播放/过渡中不补
        /// （等播完；Mecanim 的 Trigger 锁存通常会把迟到事件自然追平，真正丢的
        /// 步在回到 Idle 后由这里闭合）。</summary>
        private void Audit()
        {
            int deficit = m_authoritativeSteps - m_localSteps;
            if (deficit <= 0)
            {
                if (deficit < 0 && !m_aheadWarned)
                {
                    m_aheadWarned = true;
                    StubLog.LogWarn("[NodeRingSync] " + name + " 本地步数(" + m_localSteps +
                        ")超前权威(" + m_authoritativeSteps + ")——不应发生，请连同两端日志反馈" +
                        "（SetTrigger 计 " + m_triggerFires + "）");
                }
                return;
            }
            if (Time.realtimeSinceStartup - m_lastAuditTime < AuditInterval)
                return;
            if (m_animator.IsInTransition(0))
                return;
            var si = m_animator.GetCurrentAnimatorStateInfo(0);
            int currentIdle;
            if (!m_idleIndexByHash.TryGetValue(si.shortNameHash, out currentIdle))
                return; // 节点播放中，等完成
            int n = m_idleHashes.Length;
            int target = (currentIdle + deficit) % n;
            // 先行登记目标序号并整额计入本地步数：Play 之后的观察不再产生位移
            // （观察到 target == m_lastIdleIndex），绕环多步同样一次闭合。
            m_lastIdleIndex = target;
            m_localSteps = m_authoritativeSteps;
            m_animator.Play(m_idleHashes[target], 0, 0f);
            m_lastAuditTime = Time.realtimeSinceStartup;
            // 常开日志：联机丢步是本次修复目标，每次补步必须两端可比对。
            // SetTrigger 计数 > 补步前本地步数 = 同帧合并丢步的直接证据。
            StubLog.Log("[NodeRingSync] " + name + " 补步瞬跳：权威 " + m_authoritativeSteps +
                " 步、本地落后 " + deficit + " 步 → Play Idle_" + target +
                "（SetTrigger 计 " + m_triggerFires + "，角色=" + GameApi.RoleLabel() + "）");
        }

        /// <summary>ApplyServerEvent postfix 转发：QueueEvent 计权威步
        /// （只认 index==0——节点环队列只有单事件，index&gt;0 属于多事件
        /// 自动组，本组件不挂那些实体，防御性忽略）。计数不依赖环解析
        /// （注册先于首帧 ResolveRing 的消息也不漏计；非环组计数无读者，
        /// 无害）。</summary>
        internal void OnAuthoritativeEvent(int index)
        {
            if (index == 0)
                m_authoritativeSteps++;
        }

        /// <summary>ApplyServerEvent postfix 转发：队列取消——未执行的权威事件
        /// 作废，权威回退对齐本地（节点环烘焙无 cancelTrigger，此路径不应出现，
        /// 告警可见）。</summary>
        internal void OnAuthoritativeCancel()
        {
            if (m_ringOk && m_authoritativeSteps != m_localSteps)
            {
                StubLog.LogWarn("[NodeRingSync] " + name + " 收到队列取消：权威 " +
                    m_authoritativeSteps + " → " + m_localSteps + "（节点环烘焙无 cancel，" +
                    "请反馈此日志）");
                m_authoritativeSteps = m_localSteps;
            }
        }

        /// <summary>DoEvent postfix 转发（仅诊断计数：SetTrigger 次数 vs 本地步数
        /// 的差值 = 同帧合并丢步的直接证据）。</summary>
        internal void OnTriggerFired(int index)
        {
            if (index == 0)
                m_triggerFires++;
        }

        /// <summary>组根是否忙（ButtonLogicRelay 分发 BLGo 前的门控之一）：
        /// 对账未平（含待补步）/ 过渡中 / 节点播放中。</summary>
        internal bool IsBusy()
        {
            if (!m_ringOk)
                return false;
            if (m_authoritativeSteps > m_localSteps)
                return true;
            if (m_animator.IsInTransition(0))
                return true;
            var si = m_animator.GetCurrentAnimatorStateInfo(0);
            return !m_idleIndexByHash.ContainsKey(si.shortNameHash);
        }
    }
}
