using System;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 传送带站「按钮切换方向」运行时监视器（tag 自愈，保证在场）。
    ///
    /// 职责（2026-09-24 v2 重构后的定位）：
    ///  - 旋转的【驱动】已移交开关动画组（节点环 Animator，见 AnimGroupBakery 的
    ///    press 拓扑 + ButtonLink）：按钮 → helper Animator → 组根 TriggerQueue →
    ///    clip 旋转组成员（传送带伪根 wrapper）→ 站体 child 随动。
    ///  - 本组件只负责【结果同步】：LateUpdate 检测站体世界朝向变化（无论谁改的——
    ///    动画组、外部脚本），反射调用宿主 ServerConveyorStation.UpdateAdjacentReceiver()
    ///    刷新实际传送目标格（GetNextGridIndex 用 transform.right 派生，旋转后必须重算）。
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
        private Quaternion m_lastRotation;
        private Transform m_stationTransform;

        private void OnEnable()
        {
            m_serverStation = null;
            m_refreshMethod = null;
            m_stationTransform = FindStationTransform();
            m_lastRotation = m_stationTransform != null ? m_stationTransform.rotation : transform.rotation;
        }

        private void LateUpdate()
        {
            if (!m_enabled)
                return;
            if (m_stationTransform == null)
                m_stationTransform = FindStationTransform();

            // 旋转变化（动画组驱动或外部旋转）后刷新宿主缓存的相邻接收器。
            var currentRotation = m_stationTransform != null ? m_stationTransform.rotation : transform.rotation;
            if (Quaternion.Angle(m_lastRotation, currentRotation) < 0.01f)
                return;
            m_lastRotation = currentRotation;
            if (m_serverStation == null)
            {
                m_serverStation = FindChildComponent("ServerConveyorStation");
                if (m_serverStation != null)
                    m_refreshMethod = m_serverStation.GetType().GetMethod("UpdateAdjacentReceiver",
                        BindingFlags.Public | BindingFlags.Instance);
            }
            if (m_refreshMethod == null)
                return;
            try
            {
                m_refreshMethod.Invoke(m_serverStation, null);
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[ConveyorDirectionSync] 刷新相邻传送目标失败: " + ex.Message);
                m_refreshMethod = null;
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
