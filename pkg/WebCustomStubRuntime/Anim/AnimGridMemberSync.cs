using System;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 动画组成员「格子占位跟随」换装器（2026-09-24 v4）。
    ///
    /// 背景：参与传送带喂料的物件（ConveyorStation / TabletopConveyenceReceiver /
    /// TeleportalConveyenceReceiver，均 RequireComponent(StaticGridLocation)）在
    /// Awake 时按当时位置注册格子占用且**永不更新**（StaticGridLocation.cs:27-31）。
    /// 这些物件被开关动画组平移后，传送带仍向旧格子投料——喂不进新位置。
    ///
    /// vanilla 自带解法：DynamicGridLocation : StaticGridLocation，Update 逐帧按
    /// transform 位置重占格（DynamicGridLocation.cs:11-30）。本组件在伪预制 child
    /// 就绪后，把子树内所有 StaticGridLocation 换装成 DynamicGridLocation：
    ///  - DestroyImmediate 旧的（OnDestroy 会释放旧格占用）→ 反射 AddComponent
    ///    vanilla DynamicGridLocation（Awake 立即按当前位置重新占格）；
    ///  - 静止成员（子树无 StaticGridLocation，或永不移动）完全无副作用；
    ///  - 反射操作（WebCustomStubRuntime 编译期不引用 Assembly-CSharp）。
    ///
    /// 挂载：AnimGroupBakery.BakeGroup 对全部 item 成员挂载（wrapper 是真实场景
    /// 物体，随场景保存；无需 tag 自愈）。
    /// </summary>
    public class AnimGridMemberSync : MonoBehaviour
    {
        /// <summary>换装完成的子物体（防重复；child 重建后重跑）。</summary>
        private GameObject m_doneChild;

        private void Update()
        {
            if (m_doneChild != null)
                return;
            var pseudo = FindSelfComponent("PseudoPrefab");
            var childProp = pseudo != null
                ? pseudo.GetType().GetField("childGameObject")
                : null;
            var child = childProp != null ? childProp.GetValue(pseudo) as GameObject : null;
            if (child == null)
                return;
            m_doneChild = child;
            try
            {
                SwapGridLocations(child.transform);
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[AnimGridMemberSync] " + name + " 格子换装失败: " + ex.Message);
            }
        }

        /// <summary>子树内 StaticGridLocation → DynamicGridLocation。返回换装数。</summary>
        private int SwapGridLocations(Transform root)
        {
            var dynamicType = FindGameType("DynamicGridLocation");
            if (dynamicType == null)
            {
                StubLog.LogWarn("[AnimGridMemberSync] 找不到 vanilla DynamicGridLocation 类型，跳过换装");
                return 0;
            }
            int swapped = 0;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;
                var type = component.GetType();
                if (type.Name != "StaticGridLocation")
                    continue; // 精确名匹配：已换装的 DynamicGridLocation（子类）不再动
                var go = component.gameObject;
                DestroyImmediate(component);
                go.AddComponent(dynamicType);
                swapped++;
            }
            if (swapped > 0)
                StubLog.Log("[AnimGridMemberSync] " + name + " 换装 " + swapped +
                    " 个 DynamicGridLocation（占格将随动画跟随）");
            return swapped;
        }

        private static Type FindGameType(string typeName)
        {
            var t = Type.GetType(typeName + ", Assembly-CSharp");
            if (t != null)
                return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(typeName, false);
                if (t != null && t.Name == typeName)
                    return t;
            }
            return null;
        }

        private Component FindSelfComponent(string typeName)
        {
            foreach (var component in GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == typeName)
                    return component;
            }
            return null;
        }
    }
}
