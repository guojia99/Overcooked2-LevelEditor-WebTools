using System;
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

        private Animator m_animator;
        private int[] m_stateHashes;
        private int[][] m_blockedHashes;
        private int m_lastHash;
        private bool m_wasInRun;
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

            // 按压封禁：处于封禁状态时持续 ResetTrigger（脚本 Update 先于 Animator
            // 求值，本帧到达的 SetTrigger 会在被消费前清掉——吞掉且不锁存）。
            if (m_pressTriggers != null && m_blockedHashes != null)
            {
                for (int i = 0; i < m_pressTriggers.Length && i < m_blockedHashes.Length; i++)
                {
                    if (string.IsNullOrEmpty(m_pressTriggers[i]))
                        continue;
                    var blocked = m_blockedHashes[i];
                    for (int j = 0; j < blocked.Length; j++)
                    {
                        if (si.shortNameHash == blocked[j])
                        {
                            m_animator.ResetTrigger(m_pressTriggers[i]);
                            break;
                        }
                    }
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

            // 按钮生命周期：进入 Run → 全部配对按钮 Disable（共轭：按一个双锁）；
            // 离开 Run（动画完成回 Ready）→ 全部 Reset（一起变绿）。
            // SendMessage 直达 child 上的 ServerTriggerDisableScript（网络通道，
            // 双端状态一致；客户端本机无 Server* 组件，落空无害——与 BLGo 同款）。
            if (nowInRun && !m_wasInRun)
            {
                m_runEnterTime = Time.time;
                m_runStuckWarned = false;
                SendToPairedButtons("Disable");
            }
            else if (!nowInRun && m_wasInRun)
            {
                m_runEnterTime = -1f;
                SendToPairedButtons("Reset");
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
                // 与宿主 SendTriggerToObject 相同的投递方式：SendMessage 按
                // 方法名直达 ITriggerReceiver（ServerTimedQueue.OnTrigger）。
                target.SendMessage("OnTrigger", m_goTriggers[i],
                    SendMessageOptions.DontRequireReceiver);
                StubLog.Dbg("[ButtonLogicRelay] " + name + " 进 " + m_stateNames[i] +
                    " → 发 " + m_goTriggers[i] + " 给 " + m_targetNames[i]);
            }
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
                var child = ResolveButtonChild(m_buttonRootNames[i]);
                if (child == null)
                    continue;
                // 复位触发名读各按钮实例自身的 m_enableTrigger（SwitchReenable 同款，
                // 拨动/踏板等其它开关 prefab 未必叫 "Reset"）；禁用侧统一 "Disable"
                // （香草按压链同款触发名，编辑器 Play 3.3.2 已实证）。
                var fire = trigger == "Reset" ? ResolveEnableTrigger(child) : trigger;
                child.SendMessage("OnTrigger", fire, SendMessageOptions.DontRequireReceiver);
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
                    WarnResolveOnce(rootName, "根上无活 PseudoPrefab，子树也无 TriggerDisableScript");
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
