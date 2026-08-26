using System.Reflection;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// 2×2 大莲花压力地板：占地中心在 n×1.2 两格几何中点，与 EditorGridSnap
    /// 格心 (0.6 mod 1.2) 差半格。禁用 bundle child 上的 EditorGridSnap，
    /// 避免场景重载 / Play 后地板偏移 0.6。
    ///
    /// 继承 PseudoPrefab（非 PseudoPrefabPressureSwitch）：wrapper 仅带通用
    /// PseudoPrefabStub，场景实例未必有 PseudoPrefabPressureSwitchStub。
    /// </summary>
    public class LayoutRuntimeLotusPressureSwitchLarge : PseudoPrefab
    {
        private static readonly FieldInfo ConstrainXField = typeof(EditorGridSnap).GetField(
            "m_constrainX", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ConstrainZField = typeof(EditorGridSnap).GetField(
            "m_constrainZ", BindingFlags.Instance | BindingFlags.NonPublic);

        public override void ResetChild()
        {
            base.ResetChild();
            DisableChildGridSnap();
        }

        private void DisableChildGridSnap()
        {
            if (childGameObject == null)
                return;
            var snap = childGameObject.GetComponent<EditorGridSnap>();
            if (snap == null)
                return;
            WriteConstrain(ConstrainXField, snap, false);
            WriteConstrain(ConstrainZField, snap, false);
            snap.enabled = false;
        }

        private static void WriteConstrain(FieldInfo field, EditorGridSnap snap, bool value)
        {
            if (field == null || snap == null)
                return;
            try
            {
                field.SetValue(snap, value);
            }
            catch
            {
            }
        }
    }
}
