using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 【已退役】旧的传送带按钮动画中继（Animator + 烘焙 controller + 宿主
    /// TriggerAnimationOnConveyor）。该方案依赖烘焙期子物体在场，且宿主组件会在开局
    /// 自动开播导致「自动旋转 / 按钮无效」，已被 <see cref="ConveyorDirectionSync"/>
    /// 的纯 Transform 运行时旋转取代（2026-09-24）。
    ///
    /// 保留空壳仅为兼容历史场景里可能残留的组件实例与序列化字段，使其完全惰性、
    /// 不再新增任何 Animator / 宿主组件、不再响应触发，避免与新机制冲突。
    /// </summary>
    public class ConveyorAnimationReceiver : MonoBehaviour
    {
        // 历史序列化字段，保留以免旧场景反序列化报错；不再使用。
        public RuntimeAnimatorController m_controller;

        // 惰性：不 Awake 装配、不响应 OnTrigger。旋转全部由 ConveyorDirectionSync 负责。
        public void OnTrigger(string trigger)
        {
            // no-op（退役）。
        }
    }
}
