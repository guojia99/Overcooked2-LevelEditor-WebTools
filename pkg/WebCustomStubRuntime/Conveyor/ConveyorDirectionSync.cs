using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 传送带站「按钮切换方向 / 按钮动画移动」运行时监视器（tag 自愈，保证在场）。
    ///
    /// 职责：
    ///  - 旋转/移动的【驱动】由开关动画组承担（节点环 Animator + ButtonLink）。
    ///  - 本组件负责【结果同步】：站体世界位姿【停稳后】（相邻两帧无变化）且
    ///    【自身无在途投递】（!IsConveying()）时，反射调用宿主
    ///    ServerConveyorStation.UpdateAdjacentReceiver() 刷新投递目标：
    ///    · 旋转 → 站体是包装的子物体，随动画一起转。GetNextGridIndex 用站体
    ///      transform.right（Rightwards 沿 -right，与皮带模型同向；Leftwards 沿
    ///      +right）。停稳后只调用宿主 UpdateAdjacentReceiver。不改枚举：前方
    ///      没有接收器时食物沿皮带冲出，翻到对面会和模型箭头相反。
    ///    · 平移 → 先把 GridLocation 组件的最新 GridIndex 写回
    ///      ServerConveyorStation.m_gridIndex（OnEnable 缓存副本，移动后恒旧值）。
    ///
    /// 为什么必须「停稳 + 空闲」双条件（2026-09-24 传送卡死事故）：
    ///  1. vanilla 旋转传送带从不边送边转——ServerTriggerAnimationOnConveyor 的
    ///     Pending 态等 !IsConveying() && !IsReceiving() 才进 Animating；
    ///  2. 若在投递【在途】时换掉 m_adjacentReceiver：EndConveyence() 会对【新】
    ///     接收器 InformEndingConveyToMe()，而 InformStartingConveyToMe() 给的是
    ///     【旧】的 → 旧接收器 m_receiving 永久卡 true → AllowPlacement 恒 false，
    ///     从此再也收不了菜（整排传送链卡死）；
    ///  3. 旋转【进行中】每帧刷新还会让 45°~135° 区间的 Round(right) 瞬间指向
    ///     侧向格子，误注册瞬态邻居。
    ///
    /// 历史：v1 曾在本组件里做按钮直连自转（OnTrigger 翻转），但宿主投递链
    /// ServerTriggerOnObject → GameObject.SendTrigger 只调用 ITriggerReceiver 组件，
    /// 普通公有方法 OnTrigger(string) 永远收不到消息，该链路已删除（单一驱动源原则）。
    /// </summary>
    public class ConveyorDirectionSync : MonoBehaviour
    {
        public const string TagPrefix = "ConveyorDirectionSync|";
        public bool m_enabled = true;

        private Component m_serverStation;
        private MethodInfo m_refreshMethod;
        private MethodInfo m_isConveyingMethod;
        private FieldInfo m_gridIndexField;
        private FieldInfo m_adjacentField;
        /// <summary>本站自身接收器（ServerConveyorStation.m_selfReceiver）。
        /// 门控只认它——下游接收器可能被别的传送带共享喂入。</summary>
        private FieldInfo m_selfReceiverField;
        private Transform m_stationTransform;
        // 当前位姿（每帧更新，用于检测运动结束）。
        private Quaternion m_currentRotation;
        private Vector3 m_currentPosition;
        private bool m_lastFrameMoved;
        // 已完成同步的位姿（待同步 = 当前 != 已同步 且 已停稳）。
        private Quaternion m_syncedRotation;
        private Vector3 m_syncedPosition;
        private bool m_haveSynced;
        /// <summary>开局基准旋转（Idle_0 位姿；位姿吸附的参考零点）。</summary>
        private Quaternion m_baseRotation;
        /// <summary>基准待重捕：OnEnable 时 station child 尚未实例化（真机 loader
        /// 运行期才建 child），基准退用了伪根自身旋转（失真）——首次解析到
        /// station 后重捕（3.6.0 联机「停位差几度不回正」修复之一）。</summary>
        private bool m_baseDirty;
        /// <summary>位姿判定阈值（米/度）：小于视为静止（写回吸附噪声容差）。</summary>
        private const float MoveEpsilon = 0.1f;
        private const float AngleEpsilon = 0.01f;
        /// <summary>停稳位姿吸附容差（度）：相对基准的 Y 残差距最近 90° 倍数
        /// 在容差内才吸附——非 90° 编排（45° 节点等）不受影响。</summary>
        private const float SnapTolerance = 5f;
        /// <summary>方向已变但刷新被门控阻塞的诊断日志间隔（秒）。</summary>
        private const float BlockedLogInterval = 2f;
        /// <summary>待刷新起始时间（&lt;0 = 当前无待刷新）。</summary>
        private float m_pendingSince = -1f;
        private string m_lastBlockReason;
        private float m_lastBlockLogTime;
        /// <summary>节点环 Animator（组根）。位姿检测可能漏掉某些层级组合，
        /// 用「动画回到 Idle」作为方向刷新的权威触发点。</summary>
        private Animator m_animator;
        private int[] m_idleHashes;
        private bool m_animatorResolved;
        private bool m_wasAnimating;
        private bool m_animSeen;
        /// <summary>动画停稳但位姿检测未命中时置位，保证刷新不只依赖变换比较。</summary>
        private bool m_animationSettledRefresh;
        private const int MaxIdleNodes = 16;
        /// <summary>本次节点动画开始时间（&lt;0 = 当前不在动画中）。</summary>
        private float m_animStartTime = -1f;
        private bool m_stuckRefreshDone;
        private int m_stateLogBudget = 16;
        private int m_lastTracedHash;
        private bool m_loggedHierarchy;
        /// <summary>节点动画超时阈值：正常节点 0.4s，1.5s 仍未离开该状态视为卡死。</summary>
        private const float AnimationStuckSeconds = 1.5f;
        /// <summary>停在整 90° 朝向时的低频兜底刷新间隔（秒）。</summary>
        private const float CardinalPollSeconds = 0.25f;
        private float m_nextCardinalRefreshAt;
        private string m_lastLoggedReceiver;
        private float m_lastMoveLogTime;

        private void OnEnable()
        {
            m_serverStation = null;
            m_refreshMethod = null;
            m_isConveyingMethod = null;
            m_gridIndexField = null;
            m_adjacentField = null;
            m_selfReceiverField = null;
            m_pendingSince = -1f;
            m_lastBlockReason = null;
            m_lastBlockLogTime = 0f;
            m_animator = null;
            m_idleHashes = null;
            m_animatorResolved = false;
            m_wasAnimating = false;
            m_animSeen = false;
            m_animationSettledRefresh = false;
            m_animStartTime = -1f;
            m_stuckRefreshDone = false;
            m_stateLogBudget = 16;
            m_lastTracedHash = 0;
            m_loggedHierarchy = false;
            m_nextCardinalRefreshAt = 0f;
            m_lastLoggedReceiver = null;
            m_lastMoveLogTime = 0f;
            m_stationTransform = FindStationTransform();
            m_currentRotation = m_stationTransform != null ? m_stationTransform.rotation : transform.rotation;
            m_currentPosition = m_stationTransform != null ? m_stationTransform.position : transform.position;
            m_syncedRotation = m_currentRotation;
            m_baseRotation = m_currentRotation;
            m_baseDirty = m_stationTransform == null;
            m_syncedPosition = m_currentPosition;
            m_haveSynced = true;
            m_lastFrameMoved = false;
        }

        private void LateUpdate()
        {
            if (!m_enabled)
                return;
            if (m_stationTransform == null)
            {
                m_stationTransform = FindStationTransform();
                // 基准重捕：OnEnable 时 child 未就绪 → 基准退用了伪根旋转（失真）。
                // 首次解析到 station 的这一帧重捕全部基准——此后才开始运动检测，
                // 解析帧的位姿差不会被误判为动画运动。自愈挂载早于玩家可操作，
                // 首个按压前必已完成重捕。
                if (m_stationTransform != null && m_baseDirty)
                {
                    m_currentRotation = m_stationTransform.rotation;
                    m_currentPosition = m_stationTransform.position;
                    m_syncedRotation = m_currentRotation;
                    m_baseRotation = m_currentRotation;
                    m_syncedPosition = m_currentPosition;
                    m_haveSynced = true;
                    m_baseDirty = false;
                    StubLog.Log("[ConveyorDirectionSync] " + name + " 基准重捕：station 晚就绪，以 " +
                        m_stationTransform.name + " 初始位姿为吸附基准（此前基准为伪根旋转，失真）");
                }
            }

            // 1) 运动检测：看动画实际驱动的包装（组根的直接子）。站体是它的子物体，
            //    若子物体朝向被别的逻辑钉住，只看站体就会漏掉旋转。
            var aim = AnimatedAim();
            var rotation = aim != null ? aim.rotation : transform.rotation;
            var position = aim != null ? aim.position : transform.position;
            bool movedNow = Quaternion.Angle(m_currentRotation, rotation) >= AngleEpsilon ||
                (m_currentPosition - position).magnitude >= MoveEpsilon;
            m_lastFrameMoved = movedNow;
            m_currentRotation = rotation;
            m_currentPosition = position;

            // 2) 停稳判定：待同步位姿存在（当前 != 已同步）且本帧无运动。
            bool posePending = !m_haveSynced ||
                Quaternion.Angle(m_syncedRotation, m_currentRotation) >= AngleEpsilon ||
                (m_syncedPosition - m_currentPosition).magnitude >= MoveEpsilon;
            // 2.1) 动画停稳兜底：节点环从节点动画回到 Idle = 一次方向切换完成。
            //      某些层级组合下位姿比较可能捕获不到（例如动画驱动的是伪根、
            //      站体朝向被伪预制实例化时机影响），此时仍必须刷新投递目标，
            //      否则「模型转了、食物还走旧方向」。
            bool settledNow = ObserveAnimationSettled();
            if (settledNow)
                m_animationSettledRefresh = true;

            EnsureStationResolved();
            bool conveying = IsStationConveying();

            // 2.2) 整 90° 朝向低频兜底（不依赖位姿比较 / Animator 状态）：
            //      只要站体已停在整格朝向且当前不在向外投递，就每 0.25s 重算
            //      一次投递目标。SetAdjacentReceiver 对同一目标早退，稳态零副作用；
            //      可覆盖「模型转了但位姿检测与 Animator 兜底都漏掉」的任意层级组合。
            bool cardinalTick = false;
            if (!conveying && Time.time >= m_nextCardinalRefreshAt)
            {
                m_nextCardinalRefreshAt = Time.time + CardinalPollSeconds;
                cardinalTick = IsNearCardinal(m_stationTransform != null ? m_stationTransform : aim);
            }

            bool pending = posePending || m_animationSettledRefresh || cardinalTick;
            if (!pending)
            {
                m_pendingSince = -1f;
                return;
            }
            if (settledNow)
                StubLog.Log("[ConveyorDirectionSync] " + DebugPath() + " 停稳: posePending=" +
                    posePending + " movedNow=" + movedNow + " pendingSince=" +
                    (m_pendingSince >= 0f ? (Time.time - m_pendingSince).ToString("0.##") : "-") +
                    " conveying=" + conveying + " stationEuler=" +
                    (m_stationTransform != null ? m_stationTransform.eulerAngles.ToString("0.#") : "无") +
                    " aimEuler=" + (aim != null ? aim.eulerAngles.ToString("0.#") : "无"));
            // 整 90° 低频兜底不受「本帧仍在动」影响：它只在站体已停到整格朝向时成立，
            // 且可绕开某些层级组合下 moved 标志长期为真导致的刷新饥饿。
            if (m_lastFrameMoved && !cardinalTick)
            {
                if (Time.time - m_lastMoveLogTime > 1f)
                {
                    m_lastMoveLogTime = Time.time;
                    StubLog.LogWarn("[ConveyorDirectionSync] " + DebugPath() +
                        " 待刷新但 moved 标志持续为真（aimEuler=" +
                        (aim != null ? aim.eulerAngles.ToString("0.#") : "无") +
                        " stationEuler=" +
                        (m_stationTransform != null ? m_stationTransform.eulerAngles.ToString("0.#") : "无") +
                        " cardinalTick=" + cardinalTick + "）");
                }
                return;
            }
            if (m_pendingSince < 0f)
                m_pendingSince = Time.time;
            if (settledNow && !posePending)
                StubLog.Log("[ConveyorDirectionSync] " + DebugPath() +
                    " 动画停稳但位姿检测未命中，按节点环完成刷新投递目标");

            // 2.5) 停稳位姿吸附（2026-09-25 真机「差几度」修复）：节点环 Idle 状态
            //     是空状态（无曲线 + WD=off），exit-time 瞬时转移的接管帧不写旋转，
            //     transform 会残留结束前最后一帧的采样（几度级残差，是否命中结束
            //     帧取决于帧时机——时准时不准）。停稳后把相对基准的 Y 残差吸附到
            //     最近 90° 倍数（容差 5°），吸附后位姿即精确节点姿态。
            SnapRotationToCardinal();

            // 2.6) 同步器就绪门禁：ServerConveyorStation.StartSynchronising 之前
            //      m_selfReceiver 仍为 null。此时调用 UpdateAdjacentReceiver 会让宿主
            //      SetAdjacentReceiver 启动 DelayRefreshStartConveying 协程，下一帧
            //      对 null 调 RefreshConveyTo() → 开局 NullReferenceException。
            //      等 StartSynchronising 赋值后再刷新；pending 标志保留，不会丢。
            if (!IsStationReady())
                return;

            // 3) 空闲判定：自身无在途投递才能换目标（否则旧接收器 m_receiving
            //    永久卡 true，再也收不了菜——见类注释事故分析）。在途时挂起，
            //    下一帧重试（投递最长 1/speed 秒后自然空闲）。
            if (m_refreshMethod == null && m_adjacentField == null)
                return;
            // 刷新投递目标只在【本站有物品正在向外投递】时挂起：这正是 9/24
            // 卡死事故的唯一危险窗口（EndConveyence 会对新接收器发
            // InformEndingConveyToMe）。上游送菜入本站（m_selfReceiver）与
            // 下游目标是否在接收（m_adjacentReceiver）都与「换掉 m_adjacentReceiver」
            // 无关——尤其共享垃圾桶会被别的传送带持续喂入，拿它当门控会把方向
            // 刷新永久挡死（180° 初始朝向垃圾桶的站必现「模型转了仍送垃圾桶」）。
            if (conveying)
            {
                LogBlockedRefresh(true, IsSelfReceiving());
                return;
            }

            // 4) 平移场景：把最新格位写回 ServerConveyorStation.m_gridIndex。
            if ((m_syncedPosition - m_currentPosition).magnitude >= MoveEpsilon)
                RefreshStationGridIndex();

            try
            {
                // 不改写包装/站体旋转，也不翻 Leftwards/Rightwards。朝向只由节点环
                // 曲线维持；食物沿站体 -right（与皮带模型同向）走。
                PushLiveGridIndex();
                if (m_refreshMethod == null)
                    return;
                m_refreshMethod.Invoke(m_serverStation, null);
                m_syncedRotation = m_currentRotation;
                m_syncedPosition = m_currentPosition;
                m_haveSynced = true;
                m_pendingSince = -1f;
                m_lastBlockReason = null;
                m_lastBlockLogTime = 0f;
                bool forceLog = posePending || m_animationSettledRefresh;
                m_animationSettledRefresh = false;
                LogReceiverState(forceLog);
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[ConveyorDirectionSync] 刷新相邻传送目标失败: " + ex.Message);
                m_refreshMethod = null;
            }
        }

        /// <summary>动画包装（组根直接子）优先，否则站体。</summary>
        private Transform AnimatedAim()
        {
            var wrapper = FindMemberWrapper();
            if (wrapper != null)
                return wrapper;
            return m_stationTransform != null ? m_stationTransform : transform;
        }

        private void PushLiveGridIndex()
        {
            if (m_serverStation == null)
                return;
            if (m_gridIndexField == null)
                return;
            object live = ReadLiveGridIndex();
            if (live == null)
                return;
            try
            {
                if (live.GetType() == m_gridIndexField.FieldType)
                    m_gridIndexField.SetValue(m_serverStation, live);
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[ConveyorDirectionSync] 写回格位失败: " + ex.Message);
            }
        }

        private object ReadLiveGridIndex()
        {
            var location = FindChildComponent("DynamicGridLocation") ??
                FindChildComponent("StaticGridLocation");
            if (location != null)
            {
                var prop = location.GetType().GetProperty("GridIndex");
                if (prop != null)
                {
                    var live = prop.GetValue(location, null);
                    if (live != null)
                        return live;
                }
            }
            if (m_gridIndexField == null || m_serverStation == null)
                return null;
            return m_gridIndexField.GetValue(m_serverStation);
        }

        /// <summary>本站自身接收器是否已由 StartSynchronising 赋值。未赋值时
        /// 宿主 SetAdjacentReceiver 会启动 DelayRefreshStartConveying 协程，下一帧
        /// 访问 null 的 m_selfReceiver 直接 NRE（开局过早刷新触发）。</summary>
        private bool IsStationReady()
        {
            if (m_selfReceiverField == null || m_serverStation == null)
                return false;
            try
            {
                return m_selfReceiverField.GetValue(m_serverStation) as Component != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>本站自身接收器是否正在接收上游送来的物品（= 原版
        /// ServerTriggerAnimationOnConveyor 的 m_receiver.IsReceiving()）。</summary>
        private bool IsSelfReceiving()
        {
            if (m_selfReceiverField == null || m_serverStation == null)
                return false;
            Component receiver;
            try
            {
                receiver = m_selfReceiverField.GetValue(m_serverStation) as Component;
            }
            catch (Exception)
            {
                return false;
            }
            return InvokeIsReceiving(receiver);
        }

        private static bool InvokeIsReceiving(Component receiver)
        {
            if (receiver == null)
                return false;
            var method = receiver.GetType().GetMethod("IsReceiving",
                BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
                return false;
            try
            {
                return (bool)method.Invoke(receiver, null);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>方向已变但刷新被门控持续阻塞时的常开诊断（2s 一次）。
        /// 之前门控静默返回，真机只看到「转完还往旧方向送」而无任何日志。</summary>
        private void LogBlockedRefresh(bool conveying, bool selfReceiving)
        {
            if (m_pendingSince < 0f)
                return;
            float waited = Time.time - m_pendingSince;
            if (waited < BlockedLogInterval)
                return;
            string reason = conveying
                ? (selfReceiving ? "本站投递在途+上游送菜中" : "本站投递在途")
                : "上游送菜入本站";
            if (m_lastBlockReason == reason &&
                Time.time - m_lastBlockLogTime < BlockedLogInterval)
                return;
            m_lastBlockReason = reason;
            m_lastBlockLogTime = Time.time;
            StubLog.LogWarn("[ConveyorDirectionSync] " + name + " 方向已变但刷新被阻塞 " +
                waited.ToString("0.#") + "s（" + reason + "，旧目标=" +
                AdjacentReceiverName() + "）");
        }

        private string AdjacentReceiverName()
        {
            if (m_adjacentField == null || m_serverStation == null)
                return "?";
            try
            {
                var receiver = m_adjacentField.GetValue(m_serverStation) as Component;
                return receiver != null ? receiver.gameObject.name : "无";
            }
            catch (Exception)
            {
                return "?";
            }
        }

        /// <summary>
        /// 节点环 Animator（向上找第一个，组根上的那个）解析：仅当控制器带
        /// "BLPress" 触发参数时启用「回到 Idle = 一次方向切换完成」的兜底触发。
        /// </summary>
        private void EnsureAnimatorResolved()
        {
            if (m_animatorResolved)
                return;
            var anim = GetComponentInParent<Animator>();
            if (anim == null || anim.runtimeAnimatorController == null)
            {
                m_animatorResolved = true; // 静态传送带：没有动画祖先，不启用兜底
                return;
            }
            bool hasAdvance = false;
            var parameters = anim.parameters;
            if (parameters.Length == 0)
                return; // 控制器尚未就绪，下一帧重试
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Trigger &&
                    parameters[i].name == "BLPress")
                {
                    hasAdvance = true;
                    break;
                }
            }
            if (!hasAdvance)
            {
                m_animatorResolved = true; // 非按钮节点环：本兜底不适用
                return;
            }
            var idles = new List<int>();
            for (int i = 0; i < MaxIdleNodes; i++)
            {
                int hash = Animator.StringToHash("Idle_" + i);
                if (!anim.HasState(0, hash))
                    break;
                idles.Add(hash);
            }
            if (idles.Count == 0)
            {
                m_animatorResolved = true;
                return;
            }
            m_animator = anim;
            m_idleHashes = idles.ToArray();
            m_animatorResolved = true;
            StubLog.Log("[ConveyorDirectionSync] " + name + " 节点环动画监视就绪：" +
                m_idleHashes.Length + " 个 Idle 状态（方向刷新不依赖位姿比较）");
        }

        /// <summary>本帧是否应触发一次方向刷新：节点动画回到 Idle，或节点动画
        /// 超时仍未离开（卡死在 FlipOn/FlipOff）。</summary>
        private bool ObserveAnimationSettled()
        {
            EnsureAnimatorResolved();
            if (m_animator == null || m_idleHashes == null || m_idleHashes.Length == 0)
                return false;
            bool inTransition = m_animator.IsInTransition(0);
            int hash = m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            bool isIdle = false;
            if (!inTransition)
            {
                for (int i = 0; i < m_idleHashes.Length; i++)
                {
                    if (m_idleHashes[i] == hash)
                    {
                        isIdle = true;
                        break;
                    }
                }
            }
            bool animating = !isIdle;
            if (animating)
            {
                if (m_animStartTime < 0f)
                {
                    m_animStartTime = Time.time;
                    m_stuckRefreshDone = false;
                }
            }
            else
            {
                m_animStartTime = -1f;
                m_stuckRefreshDone = false;
            }

            // 状态轨迹（每站前 16 次）：定位「某个站停在 FlipOn 不回 Idle」。
            if (hash != m_lastTracedHash)
            {
                if (m_stateLogBudget > 0)
                {
                    m_stateLogBudget--;
                    StubLog.Log("[ConveyorDirectionSync] " + name + " AniState=" +
                        CurrentClipName() + " inTransition=" + inTransition +
                        " idle=" + isIdle + " path=" + DebugPath());
                }
                m_lastTracedHash = hash;
            }

            if (animating && !m_animSeen)
            {
                m_animSeen = true;
                StubLog.Log("[ConveyorDirectionSync] " + name + " 首次检测到节点动画播放: " +
                    CurrentClipName());
            }

            bool settledNow = isIdle && m_wasAnimating;
            bool stuckNow = false;
            if (animating && !m_stuckRefreshDone && m_animStartTime >= 0f &&
                Time.time - m_animStartTime > AnimationStuckSeconds)
            {
                m_stuckRefreshDone = true;
                stuckNow = true;
                StubLog.LogWarn("[ConveyorDirectionSync] " + name +
                    " 节点动画超过 " + AnimationStuckSeconds.ToString("0.#") +
                    "s 未离开 " + CurrentClipName() + "，强制刷新投递目标");
            }
            m_wasAnimating = animating;
            return settledNow || stuckNow;
        }

        private string CurrentClipName()
        {
            if (m_animator == null)
                return "?";
            string name = ClipName(m_animator.GetCurrentAnimatorClipInfo(0));
            var next = m_animator.GetNextAnimatorClipInfo(0);
            if (next != null && next.Length > 0 && next[0].clip != null)
            {
                string n = ClipName(next);
                if (n != name)
                    name = name + " -> " + n;
            }
            return name;
        }

        private static string ClipName(AnimatorClipInfo[] clips)
        {
            if (clips == null || clips.Length == 0 || clips[0].clip == null)
                return "?";
            return clips[0].clip.name;
        }

        /// <summary>完整层级路径（同名站如 Design/Conveyors/ConveyorStation (1) 与
        /// Design/Animated Objects/传送带阵 1·2/ConveyorStation (1) 靠它区分）。</summary>
        private string DebugPath()
        {
            string p = name;
            var t = transform.parent;
            while (t != null)
            {
                p = t.name + "/" + p;
                t = t.parent;
            }
            return p;
        }

        private static FieldInfo GetFieldUp(Type type, string fieldName)
        {
            while (type != null)
            {
                var field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                    return field;
                type = type.BaseType;
            }
            return null;
        }

        private void EnsureStationResolved()
        {
            if (m_serverStation == null)
            {
                if (m_stationTransform == null)
                    return;
                m_serverStation = FindChildComponent("ServerConveyorStation");
            }
            if (m_serverStation == null)
                return;
            var type = m_serverStation.GetType();
            if (m_refreshMethod == null)
                m_refreshMethod = type.GetMethod("UpdateAdjacentReceiver",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
            if (m_isConveyingMethod == null)
                m_isConveyingMethod = type.GetMethod("IsConveying",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
            if (m_gridIndexField == null)
                m_gridIndexField = GetFieldUp(type, "m_gridIndex");
            if (m_adjacentField == null)
                m_adjacentField = GetFieldUp(type, "m_adjacentReceiver");
            if (m_selfReceiverField == null)
                m_selfReceiverField = GetFieldUp(type, "m_selfReceiver");
            if (!m_loggedHierarchy)
            {
                m_loggedHierarchy = true;
                var wrapper = FindMemberWrapper();
                StubLog.Log("[ConveyorDirectionSync] " + name + " 层级: self=" + transform.name +
                    " path=" + DebugPath() +
                    " station=" + (m_stationTransform != null ? m_stationTransform.name : "无") +
                    " wrapper=" + (wrapper != null ? wrapper.name : "无") +
                    " baseRot=" + m_baseRotation.eulerAngles.ToString("0.#"));
            }
        }

        /// <summary>被节点环动画驱动的祖先（组根 = 同时挂 Animator 与 TriggerQueue
        /// 的物体）。</summary>
        private Transform m_animatedAncestor;
        private bool m_ancestorResolved;

        private Transform FindAnimatedAncestor()
        {
            if (m_ancestorResolved)
                return m_animatedAncestor;
            m_ancestorResolved = true;
            var start = m_stationTransform != null ? m_stationTransform : transform;
            for (var t = start.parent; t != null; t = t.parent)
            {
                bool hasAnim = false, hasQueue = false;
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null) continue;
                    var n = c.GetType().Name;
                    if (n == "Animator") hasAnim = true;
                    else if (n == "TriggerQueue") hasQueue = true;
                }
                if (hasAnim && hasQueue)
                {
                    m_animatedAncestor = t;
                    break;
                }
            }
            return m_animatedAncestor;
        }

        /// <summary>曲线目标 wrapper：站点祖先链上、组根的直接子物体（clip 曲线
        /// path 解析到的正是它）。位姿修正必须写 wrapper——写 station 本体只改
        /// localRotation，下一次 wrapper 动画会带着残差叠加漂移；【绝不能写组根】
        /// （组根在原点时 90° 整数旋转会让成员绕原点公转飞出——2026-09-25
        /// 3.3.4「传送带乱飞」事故）。</summary>
        private Transform FindMemberWrapper()
        {
            var groupRoot = FindAnimatedAncestor();
            if (groupRoot == null)
                return null;
            var start = m_stationTransform != null ? m_stationTransform : transform;
            for (var t = start; t != null && t.parent != null; t = t.parent)
            {
                if (t.parent == groupRoot)
                    return t;
            }
            return null;
        }

        /// <summary>停稳位姿吸附：相对开局基准的 Y 旋转残差 → 最近 90° 倍数。
        /// 修正量（≤SnapTolerance 的小角度）以【相对旋转】施加在 wrapper 世界
        /// 旋转上——绕 wrapper 自身轴就地旋转，与节点 clip 的驱动方式一致，无
        /// 支点/基准姿态问题。非纯 Y 旋转（倾斜类编排）或残差超容差（非 90°
        /// 节点编排）一律不动。</summary>
        private void SnapRotationToCardinal()
        {
            float angle;
            Vector3 axis;
            (Quaternion.Inverse(m_baseRotation) * m_currentRotation).ToAngleAxis(out angle, out axis);
            if (angle < 0.05f)
                return; // 基准姿态，无需吸附
            if (Mathf.Abs(axis.y) < 0.99f)
                return; // 非纯 Y 旋转编排（倾斜/翻滚），不吸附
            if (axis.y < 0f)
                angle = -angle; // 归一化到绕 +Y 的有符号角（-180..180）
            var snapped = Mathf.Round(angle / 90f) * 90f;
            var delta = snapped - angle;
            if (Mathf.Abs(delta) < 0.05f || Mathf.Abs(delta) > SnapTolerance)
                return; // 已精确 / 距 90° 倍数过远（45° 等编排），不吸附

            var corr = Quaternion.AngleAxis(delta, Vector3.up);
            var wrapper = FindMemberWrapper();
            var host = wrapper != null ? wrapper
                : (m_stationTransform != null ? m_stationTransform : transform);
            // 节点环 Idle 定持 clip 已经每帧写精确姿态。再写包装旋转会和 Animator
            // 抢同一条 localRotation，Y=180° 的传送带会转到一半被拉回开局角。
            if (AnimatorDrives(host))
                return;
            host.rotation = corr * host.rotation;

            // 只更新本帧观测。已同步位姿必须等投递目标按新朝向写完再盖，
            // 否则吸附后若本帧仍在投递，下一帧会以为无需刷新，食物继续走旧邻居。
            var newStation = m_stationTransform != null ? m_stationTransform.rotation : transform.rotation;
            m_currentRotation = newStation;
            StubLog.Log("[ConveyorDirectionSync] " + name + " 停稳位姿吸附: " +
                angle.ToString("0.#") + "° → " + snapped.ToString("0.#") +
                "°（修正残差 " + delta.ToString("0.##") + "°，写于 " + host.name + "）");
        }

        /// <summary>该变换由上层 Animator 的曲线驱动（节点环包装）。吸附不得改它。</summary>
        private static bool AnimatorDrives(Transform host)
        {
            if (host == null)
                return false;
            var anim = host.GetComponentInParent<Animator>();
            return anim != null && anim.enabled && anim.runtimeAnimatorController != null;
        }

        private bool IsStationConveying()
        {
            if (m_isConveyingMethod == null)
                return false;
            try
            {
                return (bool)m_isConveyingMethod.Invoke(m_serverStation, null);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>把站体当前实际格位写回 ServerConveyorStation.m_gridIndex（全反射，
        /// GridIndex 为 Assembly-CSharp 结构体，装箱拷贝赋值）。</summary>
        private void RefreshStationGridIndex()
        {
            if (m_gridIndexField == null || m_serverStation == null)
                return;
            try
            {
                var location = FindChildComponent("StaticGridLocation") ??
                    FindChildComponent("DynamicGridLocation");
                if (location == null)
                    return;
                var prop = location.GetType().GetProperty("GridIndex");
                if (prop == null)
                    return;
                var fresh = prop.GetValue(location, null);
                if (fresh != null && fresh.GetType() == m_gridIndexField.FieldType)
                    m_gridIndexField.SetValue(m_serverStation, fresh);
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[ConveyorDirectionSync] 刷新格位失败: " + ex.Message);
            }
        }

        /// <summary>常开诊断：刷新后报告新投递目标（排查「旋转后不传送」直接看这行）。
        /// force=false 时仅在目标名变化时打印，避免「整 90° 低频兜底」刷屏。</summary>
        private void LogReceiverState(bool force)
        {
            if (m_adjacentField == null || m_serverStation == null)
                return;
            try
            {
                var receiver = m_adjacentField.GetValue(m_serverStation) as Component;
                string current = receiver != null ? receiver.gameObject.name : "无（该方向没有可接收的邻居）";
                if (!force && current == m_lastLoggedReceiver)
                    return;
                m_lastLoggedReceiver = current;
                StubLog.Log("[ConveyorDirectionSync] " + name + " 已刷新投递目标: " + current);
            }
            catch (Exception)
            {
                // 诊断失败不影响功能。
            }
        }

        /// <summary>站体当前是否停在相对基准的整 90° 朝向（误差 ≤SnapTolerance）。</summary>
        private bool IsNearCardinal(Transform aim)
        {
            if (aim == null)
                return false;
            float angle;
            Vector3 axis;
            (Quaternion.Inverse(m_baseRotation) * aim.rotation).ToAngleAxis(out angle, out axis);
            if (Mathf.Abs(axis.y) < 0.99f)
                return false;
            if (axis.y < 0f)
                angle = -angle;
            float snapped = Mathf.Round(angle / 90f) * 90f;
            return Mathf.Abs(Mathf.DeltaAngle(angle, snapped)) <= SnapTolerance;
        }

        private Transform FindStationTransform()
        {
            var station = FindChildComponent("ConveyorStation");
            return station != null ? station.transform : null;
        }

        private Component FindChildComponent(string typeName)
        {
            foreach (var component in GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetType().Name == typeName)
                    return component;
            }
            return null;
        }
    }
}
