using System;
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
    ///    · 旋转 → GetNextGridIndex 用 transform.right 派生，翻转后指向反侧邻居；
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
        /// <summary>位姿判定阈值（米/度）：小于视为静止（写回吸附噪声容差）。</summary>
        private const float MoveEpsilon = 0.1f;
        private const float AngleEpsilon = 0.01f;
        /// <summary>停稳位姿吸附容差（度）：相对基准的 Y 残差距最近 90° 倍数
        /// 在容差内才吸附——非 90° 编排（45° 节点等）不受影响。</summary>
        private const float SnapTolerance = 5f;

        private void OnEnable()
        {
            m_serverStation = null;
            m_refreshMethod = null;
            m_isConveyingMethod = null;
            m_gridIndexField = null;
            m_adjacentField = null;
            m_stationTransform = FindStationTransform();
            m_currentRotation = m_stationTransform != null ? m_stationTransform.rotation : transform.rotation;
            m_currentPosition = m_stationTransform != null ? m_stationTransform.position : transform.position;
            m_syncedRotation = m_currentRotation;
            m_baseRotation = m_currentRotation;
            m_syncedPosition = m_currentPosition;
            m_haveSynced = true;
            m_lastFrameMoved = false;
        }

        private void LateUpdate()
        {
            if (!m_enabled)
                return;
            if (m_stationTransform == null)
                m_stationTransform = FindStationTransform();

            // 1) 运动检测：本帧位姿相对上一帧是否变化（动画进行中）。
            var rotation = m_stationTransform != null ? m_stationTransform.rotation : transform.rotation;
            var position = m_stationTransform != null ? m_stationTransform.position : transform.position;
            bool movedNow = Quaternion.Angle(m_currentRotation, rotation) >= AngleEpsilon ||
                (m_currentPosition - position).magnitude >= MoveEpsilon;
            m_lastFrameMoved = movedNow;
            m_currentRotation = rotation;
            m_currentPosition = position;

            // 2) 停稳判定：待同步位姿存在（当前 != 已同步）且本帧无运动。
            bool pending = !m_haveSynced ||
                Quaternion.Angle(m_syncedRotation, m_currentRotation) >= AngleEpsilon ||
                (m_syncedPosition - m_currentPosition).magnitude >= MoveEpsilon;
            if (!pending || m_lastFrameMoved)
                return;

            // 2.5) 停稳位姿吸附（2026-09-25 真机「差几度」修复）：节点环 Idle 状态
            //     是空状态（无曲线 + WD=off），exit-time 瞬时转移的接管帧不写旋转，
            //     transform 会残留结束前最后一帧的采样（几度级残差，是否命中结束
            //     帧取决于帧时机——时准时不准）。停稳后把相对基准的 Y 残差吸附到
            //     最近 90° 倍数（容差 5°），吸附后位姿即精确节点姿态。
            SnapRotationToCardinal();
            rotation = m_stationTransform != null ? m_stationTransform.rotation : transform.rotation;

            // 3) 空闲判定：自身无在途投递才能换目标（否则旧接收器 m_receiving
            //    永久卡 true，再也收不了菜——见类注释事故分析）。在途时挂起，
            //    下一帧重试（投递最长 1/speed 秒后自然空闲）。
            EnsureStationResolved();
            if (m_refreshMethod == null)
                return;
            if (IsStationConveying())
                return;

            // 4) 平移场景：把最新格位写回 ServerConveyorStation.m_gridIndex。
            if ((m_syncedPosition - m_currentPosition).magnitude >= MoveEpsilon)
                RefreshStationGridIndex();

            try
            {
                m_refreshMethod.Invoke(m_serverStation, null);
                m_syncedRotation = m_currentRotation;
                m_syncedPosition = m_currentPosition;
                m_haveSynced = true;
                LogReceiverState();
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[ConveyorDirectionSync] 刷新相邻传送目标失败: " + ex.Message);
                m_refreshMethod = null;
            }
        }

        private void EnsureStationResolved()
        {
            if (m_serverStation != null || m_stationTransform == null)
                return;
            m_serverStation = FindChildComponent("ServerConveyorStation");
            if (m_serverStation != null)
            {
                var type = m_serverStation.GetType();
                m_refreshMethod = type.GetMethod("UpdateAdjacentReceiver",
                    BindingFlags.Public | BindingFlags.Instance);
                m_isConveyingMethod = type.GetMethod("IsConveying",
                    BindingFlags.Public | BindingFlags.Instance);
                m_gridIndexField = type.GetField("m_gridIndex",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                m_adjacentField = type.GetField("m_adjacentReceiver",
                    BindingFlags.NonPublic | BindingFlags.Instance);
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
            host.rotation = corr * host.rotation;

            var newStation = m_stationTransform != null ? m_stationTransform.rotation : transform.rotation;
            m_currentRotation = newStation;
            m_syncedRotation = newStation;
            StubLog.Log("[ConveyorDirectionSync] " + name + " 停稳位姿吸附: " +
                angle.ToString("0.#") + "° → " + snapped.ToString("0.#") +
                "°（修正残差 " + delta.ToString("0.##") + "°，写于 " + host.name + "）");
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

        /// <summary>常开诊断：刷新后报告新投递目标（排查「旋转后不传送」直接看这行）。</summary>
        private void LogReceiverState()
        {
            if (m_adjacentField == null || m_serverStation == null)
                return;
            try
            {
                var receiver = m_adjacentField.GetValue(m_serverStation) as Component;
                StubLog.Log("[ConveyorDirectionSync] " + name + " 已刷新投递目标: " +
                    (receiver != null ? receiver.gameObject.name : "无（该方向没有可接收的邻居）"));
            }
            catch (Exception)
            {
                // 诊断失败不影响功能。
            }
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
