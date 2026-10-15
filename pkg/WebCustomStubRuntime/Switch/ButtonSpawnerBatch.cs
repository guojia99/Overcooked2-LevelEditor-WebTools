using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 按钮生成器批次（2026-10-14）：食材生成器（AttachingFoodSpawner）改由按钮
    /// 触发——每按一次送出一批 N 个；第 1 个立刻送，之后每隔 m_intervalSeconds
    /// 再送 1 个直到满 N（落点站被占则该个持续补位，间隔为最小间距）。
    ///
    /// 挂载位置：Design/Speed Logic/SpnB_&lt;hash&gt; helper（SpeedControlBakery
    /// 烘焙）；目标生成器伪根由 m_targetPath 名字引用。
    ///
    /// 状态机（用户确认语义）：
    ///  空闲 Idle --按钮下降沿--> 批次中 Delivering
    ///  批次中：第 k 个（0 起）计划时刻 = 批次开始 + k×间隔；到点且落点站空闲
    ///   → SendTrigger(m_trigger) 复用原生 ServerTriggerAttachedSpawn 生成+联网；
    ///   站忙 → 持续重试补位；期间所有按压无效（不排队不计时）。
    ///  第 N 个实际生成完毕 → 冷却 Cooldown（起算 = 最后一个生成时刻，受补位
    ///   影响自然顺延）；冷却 m_cooldownSeconds 内按压无效；结束回 Idle。
    ///
    /// 与原生 TriggerTimer 的关系：绑定 spawner child 后立即停用其原生
    /// TriggerTimer（m_startTiming=false / m_startTrigger="" / m_triggerAtStart=
    /// false，反射写公开序列化字段）——间隔计时由本组件自持，杜绝原生自动
    /// 循环与批次双送。停用放运行时而非写回烘焙：写回主路径
    /// ReloadPseudoAssetsFull 会整体重建 child（override 必丢），且编辑器 Play
    /// 与真机自愈两条加载路径都在 ServerTriggerTimer.StartSynchronising（握手后
    /// 才 arm）之前完成本停用，时序安全。
    ///
    /// 时序铁律：Server/Client 同步器组件（ServerTriggerAttachedSpawn、其私有
    /// m_attachStation/m_conveyenceReceiver）握手后才挂/才赋值——必须等
    /// MultiplayerController.IsSynchronisationActive()（RandomCrate 同款）。
    /// 批次驱动仅服务端（IsServerMachine）；客户端组件空转——生成经原生网络
    /// 链呈现，无需客户端本地状态。
    ///
    /// 按压检测：SwitchReenable / CoaxialButtonGroup 同款轮询等价实现——监视
    /// 各按钮子树 TriggerDisableScript.m_script.enabled 下降沿。按下后 vanilla
    /// 立即置灰，本组件随即挂起按钮 SwitchReenable 的自动复位——批次 + 冷却
    /// 全程保持置灰不可再按，冷却结束经原版 ServerTriggerDisableScript 网络
    /// 通道补发复位触发点亮（双端同步；CannonGuard 同款，不直写
    /// Interactable.enabled）。
    ///
    /// 数据双通道：本组件（Bakery 反射烘焙，权威）+ tag 载体
    /// SpawnerBatch|E:&lt;1|0&gt;|T:&lt;目标名&gt;|N:&lt;按钮名,..&gt;|C:&lt;count&gt;
    /// |I:&lt;interval&gt;|D:&lt;cooldown&gt;（%,;&gt;| 做 %XX 转义）。
    /// </summary>
    public class ButtonSpawnerBatch : MonoBehaviour
    {
        /// <summary>false = 配置保留但不生效（原生 TriggerTimer 恢复自动循环）。</summary>
        public bool m_enabled = true;
        /// <summary>目标生成器伪根物体名（GameObject.Find + 缓存）。</summary>
        public string m_targetPath;
        /// <summary>触发源按钮伪根物体名（GameObject.Find 身份键；烘焙侧保证唯一名）。</summary>
        public string[] m_buttonPaths;
        /// <summary>每批数量（1 起；原生 TriggerAttachedSpawn.m_maxNumber 默认 50 封顶）。</summary>
        public int m_count = 1;
        /// <summary>批内送出间隔（秒；= DTO triggerTime 烘焙）。</summary>
        public float m_intervalSeconds = 5f;
        /// <summary>批次完成后冷却（秒；期间按压无效）。</summary>
        public float m_cooldownSeconds = 5f;

        public const string TagPrefix = "SpawnerBatch|";
        public const int MaxCount = 50;
        public const float MinIntervalSeconds = 0.1f;

        private class Member
        {
            public string rootName;
            public Behaviour watched;
            public bool lastEnabled = true;
            public bool bound;
            /// <summary>按钮上的 SwitchReenable（批次+冷却期间挂起，保持置灰）。</summary>
            public SwitchReenable reenable;
            /// <summary>复位触发名（TriggerDisableScript.m_enableTrigger，缺省 Reset）。</summary>
            public string enableTrigger = "Reset";
        }

        private enum State
        {
            Idle,
            Delivering,
            Cooldown
        }

        private readonly List<Member> m_members = new List<Member>();
        private State m_state = State.Idle;
        private int m_delivered;
        private float m_batchStart;
        private float m_cooldownStart;
        private float m_lastDeliverTime;
        private bool m_pressIgnoreLogged;

        // ---- 原生绑定（目标伪根子树） ----
        private GameObject m_targetRoot;
        private GameObject m_spawnGo;               // TriggerAttachedSpawn 所在物体（SendTrigger 目标）
        private string m_spawnTrigger;              // TriggerAttachedSpawn.m_trigger（实例读，不硬编码）
        private Component m_serverSpawn;            // ServerTriggerAttachedSpawn（server 侧，握手后）
        private bool m_spawnGoBound;
        private bool m_serverBound;
        private bool m_spawnBlockedWarned;
        private static bool s_loggedSelfCheck;

        private void OnEnable()
        {
            StartCoroutine(SafeRunner(Drive()));
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            // 组件中途被禁用/销毁时恢复按钮（防按钮永久置灰；场景卸载期
            // SendTrigger 可能抛，SetButtonsLocked 内部已 catch）
            if (m_state != State.Idle)
                SetButtonsLocked(false);
            m_state = State.Idle;
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
                    StubLog.LogWarn("[ButtonSpawnerBatch] 协程异常退出: " + name + "\n" + ex);
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
                StubLog.Dbg("[ButtonSpawnerBatch] 反射自检: TriggerAttachedSpawn=" +
                    (GameApi.TriggerAttachedSpawnType != null) + " ServerTriggerAttachedSpawn=" +
                    (GameApi.ServerTriggerAttachedSpawnType != null) + " AttachStation=" +
                    (GameApi.ServerAttachStationType != null));
            }

            if (!m_enabled)
            {
                StubLog.Dbg("[ButtonSpawnerBatch] 已禁用（配置保留）: " + name);
                yield break;
            }

            m_count = Mathf.Clamp(m_count, 1, MaxCount);
            m_intervalSeconds = Mathf.Max(MinIntervalSeconds, m_intervalSeconds);
            m_cooldownSeconds = Mathf.Max(0f, m_cooldownSeconds);

            m_members.Clear();
            foreach (var rootName in m_buttonPaths ?? new string[0])
            {
                if (string.IsNullOrEmpty(rootName)) continue;
                m_members.Add(new Member { rootName = rootName });
            }
            if (m_members.Count == 0)
            {
                StubLog.LogWarn("[ButtonSpawnerBatch] " + name + " 未绑定任何按钮，协程退出");
                yield break;
            }

            bool isServer = GameApi.IsServerMachine();

            // ---- 绑定阶段一：spawner child（TriggerAttachedSpawn 原生自带，随时可绑）
            float waited = 0f;
            bool slowPoll = false;
            while (!m_spawnGoBound)
            {
                TryBindSpawnGo();
                if (m_spawnGoBound)
                    break;
                waited += 0.25f;
                if (waited > 20f && !slowPoll)
                {
                    slowPoll = true;
                    StubLog.LogWarn("[ButtonSpawnerBatch] " + name +
                        " 等 TriggerAttachedSpawn 20s 未就绪，降为 1s 轮询继续等待");
                }
                yield return new WaitForSeconds(slowPoll ? 1f : 0.25f);
            }

            // ---- 绑定阶段二：按钮（各端都要绑——按压检测本地跑）
            waited = 0f;
            slowPoll = false;
            while (true)
            {
                bool allBound = true;
                for (int i = 0; i < m_members.Count; i++)
                {
                    var m = m_members[i];
                    if (!m.bound && !TryBindMember(m))
                        allBound = false;
                }
                if (allBound)
                    break;
                waited += 0.25f;
                if (waited > 20f && !slowPoll)
                {
                    slowPoll = true;
                    StubLog.LogWarn("[ButtonSpawnerBatch] " + name + " 有按钮 20s 未就绪，降为 1s 轮询继续等待");
                }
                yield return new WaitForSeconds(slowPoll ? 1f : 0.25f);
            }

            if (!isServer)
            {
                // 客户端：生成走服务端网络链，本地零动作（按压检测也不需要）。
                StubLog.Dbg("[ButtonSpawnerBatch] " + name + " 客户端空转（服务端驱动批次）");
                yield break;
            }

            // ---- 绑定阶段三（仅服务端）：等握手 → ServerTriggerAttachedSpawn
            waited = 0f;
            slowPoll = false;
            while (!m_serverBound)
            {
                if (GameApi.IsSynchronisationActive())
                    TryBindServerSpawn();
                if (m_serverBound)
                    break;
                waited += 0.25f;
                if (waited > 20f && !slowPoll)
                {
                    slowPoll = true;
                    if (waited > 60f)
                    {
                        StubLog.LogWarn("[ButtonSpawnerBatch] " + name +
                            " 等 ServerTriggerAttachedSpawn 60s 未就绪（非生成器场景？），协程退出");
                        yield break;
                    }
                }
                yield return new WaitForSeconds(slowPoll ? 1f : 0.25f);
            }

            StubLog.Log("[ButtonSpawnerBatch] " + name + " 启动：" + m_members.Count +
                " 按钮｜每批 " + m_count + " 个｜间隔 " + m_intervalSeconds.ToString("0.##") +
                "s｜冷却 " + m_cooldownSeconds.ToString("0.##") + "s｜触发名 " + m_spawnTrigger);

            // ---- 主循环（仅服务端）：下降沿 + 批次状态机
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

                TickBatch();

                yield return null;
            }
        }

        /// <summary>绑定生成目标：m_targetPath 伪根子树 TriggerAttachedSpawn，
        /// 记其所在物体与 m_trigger 名，并停用同物体原生 TriggerTimer 自动循环
        /// （m_startTiming/m_startTrigger/m_triggerAtStart 公开序列化字段反射写；
        /// 编辑器写回不可烘焙此处——ReloadPseudoAssetsFull 重建 child 必丢）。</summary>
        private void TryBindSpawnGo()
        {
            if (GameApi.TriggerAttachedSpawnType == null)
                return;
            if (m_targetRoot == null)
            {
                if (string.IsNullOrEmpty(m_targetPath))
                    return;
                m_targetRoot = GameObject.Find(m_targetPath);
                if (m_targetRoot == null)
                    return;
            }
            var found = m_targetRoot.GetComponentsInChildren(GameApi.TriggerAttachedSpawnType, true);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null) continue;
                m_spawnGo = found[i].gameObject;
                m_spawnTrigger = GameApi.AttachedSpawnTriggerField != null
                    ? GameApi.AttachedSpawnTriggerField.GetValue(found[i]) as string
                    : null;
                if (string.IsNullOrEmpty(m_spawnTrigger))
                    m_spawnTrigger = "Spawn";
                m_spawnGoBound = true;
                DisarmNativeTimer();
                return;
            }
        }

        /// <summary>停用 spawner child 的原生 TriggerTimer 自动循环（防双送；
        /// 字段缺失/异常只告警一次，原生循环继续也不会与批次冲突——批次送出
        /// 与原生 tick 都走同一空闲守卫，最多多送，绝不相卡）。</summary>
        private void DisarmNativeTimer()
        {
            try
            {
                var timerType = GameApi.TriggerTimerType;
                if (timerType == null || m_spawnGo == null)
                    return;
                var timers = m_spawnGo.GetComponentsInChildren(timerType, true);
                int disarmed = 0;
                for (int i = 0; i < timers.Length; i++)
                {
                    var t = timers[i];
                    if (t == null) continue;
                    if (GameApi.TriggerTimerStartTimingField != null)
                        GameApi.TriggerTimerStartTimingField.SetValue(t, false);
                    if (GameApi.TriggerTimerStartTriggerField != null)
                        GameApi.TriggerTimerStartTriggerField.SetValue(t, "");
                    if (GameApi.TriggerTimerAtStartField != null)
                        GameApi.TriggerTimerAtStartField.SetValue(t, false);
                    disarmed++;
                }
                if (disarmed > 0)
                    StubLog.Dbg("[ButtonSpawnerBatch] " + name + " 已停用原生 TriggerTimer × " +
                        disarmed + "（" + m_spawnGo.name + "）");
            }
            catch (System.Exception ex)
            {
                if (!m_spawnBlockedWarned)
                {
                    m_spawnBlockedWarned = true;
                    StubLog.LogWarn("[ButtonSpawnerBatch] 停用原生 TriggerTimer 失败: " +
                        name + " " + ex.Message);
                }
            }
        }

        /// <summary>绑定服务端同步器：目标伪根子树 ServerTriggerAttachedSpawn（握手后存在）。</summary>
        private void TryBindServerSpawn()
        {
            if (GameApi.ServerTriggerAttachedSpawnType == null || m_targetRoot == null)
                return;
            var found = m_targetRoot.GetComponentsInChildren(GameApi.ServerTriggerAttachedSpawnType, true);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null) continue;
                m_serverSpawn = found[i];
                m_serverBound = true;
                return;
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
            if (GameApi.DisableEnableTriggerField != null)
            {
                var et = GameApi.DisableEnableTriggerField.GetValue(found[0]) as string;
                if (!string.IsNullOrEmpty(et))
                    m.enableTrigger = et;
            }
            var reenables = root.GetComponentsInChildren<SwitchReenable>(true);
            if (reenables.Length > 0)
                m.reenable = reenables[0];
            m.lastEnabled = watched.enabled;
            m.bound = true;
            return true;
        }

        private void HandlePress(string buttonName)
        {
            if (m_state == State.Delivering)
            {
                if (!m_pressIgnoreLogged)
                {
                    m_pressIgnoreLogged = true;
                    StubLog.Dbg("[ButtonSpawnerBatch] " + name + " 批次进行中，按压忽略（" +
                        m_delivered + "/" + m_count + "）");
                }
                return;
            }
            if (m_state == State.Cooldown)
            {
                if (!m_pressIgnoreLogged)
                {
                    m_pressIgnoreLogged = true;
                    StubLog.Dbg("[ButtonSpawnerBatch] " + name + " 冷却中，按压忽略");
                }
                return;
            }
            m_state = State.Delivering;
            m_delivered = 0;
            m_batchStart = Time.time;
            m_pressIgnoreLogged = false;
            SetButtonsLocked(true);
            StubLog.Dbg("[ButtonSpawnerBatch] " + name + " 按下 " + buttonName +
                " → 批次开始（" + m_count + " 个，按钮置灰锁定至冷却结束）");
        }

        /// <summary>锁定/解锁全部触发按钮：按下后 vanilla 已把按钮置灰，锁定 =
        /// 挂起其 SwitchReenable 自动复位（保持置灰，批次+冷却期间不可再按）；
        /// 解锁 = 解除挂起并经原版 ServerTriggerDisableScript 网络通道补发复位
        /// 触发（双端同步点亮；CannonGuard 同款，不直写 Interactable.enabled）。
        /// 仅服务端调用（客户端组件空转，按钮灰/绿由网络同步呈现）。</summary>
        private void SetButtonsLocked(bool locked)
        {
            for (int i = 0; i < m_members.Count; i++)
            {
                var m = m_members[i];
                if (m.reenable != null)
                    m.reenable.m_suspended = locked;
                if (locked || m.watched == null || m.watched.enabled)
                    continue;
                try
                {
                    GameApi.SendTrigger(m.watched.gameObject, m.enableTrigger);
                }
                catch (System.Exception ex)
                {
                    StubLog.LogWarn("[ButtonSpawnerBatch] 按钮复位触发失败: " + name +
                        " " + m.rootName + " " + ex.Message);
                }
            }
        }

        /// <summary>批次推进：到计划时刻且落点站空闲 → 触发原生生成；满 N 进冷却。</summary>
        private void TickBatch()
        {
            if (m_state != State.Delivering)
            {
                if (m_state == State.Cooldown && Time.time - m_cooldownStart >= m_cooldownSeconds)
                {
                    m_state = State.Idle;
                    m_pressIgnoreLogged = false;
                    SetButtonsLocked(false);
                    StubLog.Dbg("[ButtonSpawnerBatch] " + name + " 冷却结束，按钮复位可按下一批");
                }
                return;
            }

            if (m_delivered >= m_count)
            {
                // 最后一个已生成 → 冷却起算（= 最后生成时刻，自然顺延）
                m_state = State.Cooldown;
                m_cooldownStart = m_lastDeliverTime;
                m_pressIgnoreLogged = false;
                StubLog.Dbg("[ButtonSpawnerBatch] " + name + " 批次完成（" + m_count +
                    " 个）→ 冷却 " + m_cooldownSeconds.ToString("0.##") + "s");
                return;
            }

            float due = m_batchStart + m_delivered * m_intervalSeconds;
            if (Time.time < due)
                return;
            if (!IsStationFree())
                return; // 补位等待：计划时刻已到，站空即送（保持最小间隔语义）

            GameApi.SendTrigger(m_spawnGo, m_spawnTrigger);
            m_lastDeliverTime = Time.time;
            m_delivered++;
            if (m_delivered == 1)
                m_pressIgnoreLogged = false;
        }

        /// <summary>落点站是否空闲（镜像 ServerTriggerAttachedSpawn.OnTrigger 守卫：
        /// m_attachStation.HasItem() || m_conveyenceReceiver.IsReceiving() = 忙）。
        /// 反射缺失时按空闲放行（原生 OnTrigger 内部守卫仍会兜底拒绝）。</summary>
        private bool IsStationFree()
        {
            try
            {
                if (m_serverSpawn == null)
                    return false;
                if (GameApi.ServerAttachedSpawnStationField != null && GameApi.AttachStationHasItemMethod != null)
                {
                    var station = GameApi.ServerAttachedSpawnStationField.GetValue(m_serverSpawn);
                    if (station == null)
                        return false; // 未就绪（StartSynchronising 未跑完）——按忙处理
                    if ((bool)GameApi.AttachStationHasItemMethod.Invoke(station, null))
                        return false;
                }
                if (GameApi.ServerAttachedSpawnReceiverField != null && GameApi.ConveyenceIsReceivingMethod != null)
                {
                    var receiver = GameApi.ServerAttachedSpawnReceiverField.GetValue(m_serverSpawn);
                    if (receiver != null &&
                        (bool)GameApi.ConveyenceIsReceivingMethod.Invoke(receiver, null))
                        return false;
                }
                return true;
            }
            catch (System.Exception ex)
            {
                if (!m_spawnBlockedWarned)
                {
                    m_spawnBlockedWarned = true;
                    StubLog.LogWarn("[ButtonSpawnerBatch] 落点站守卫反射失败（按空闲放行，原生守卫兜底）: " +
                        name + " " + ex.Message);
                }
                return true;
            }
        }
    }
}
