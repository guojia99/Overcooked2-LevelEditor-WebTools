using System;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 杯/盘容量与玻璃杯碰撞体调校（移植自被禁改的 Plate.Awake 补丁，2026-10-27）。
    /// 原补丁给 Plate 新增 Awake：IngredientContainer.m_capacity=100（杯/盘可续料
    /// 凑单，不被默认容量卡死）+ Glass 杯型（m_uID=1740450）BoxCollider 居中放大。
    /// 宿主 Plate 原本没有 Awake 可供 Harmony 挂载；而 m_capacity 是被
    /// Server/ClientIngredientContainer 每次检查时实时读取的（非同步时锁存），
    /// 故改为周期扫描调校（EntryPoint.StubTicker 每 30 帧一次）——生成后极短
    /// 延迟内生效，语义与 Awake 注入等价。全部反射；成员缺失 = 禁用该子项，
    /// 绝不影响原版行为。
    /// </summary>
    internal static class PlateCupTuning
    {
        private const int TargetCapacity = 100;
        private const int GlassUid = 1740450;
        private static readonly Vector3 GlassCenter = new Vector3(0f, 0.36f, 0f);
        private static readonly Vector3 GlassSize = new Vector3(0.5f, 0.73f, 0.5f);

        private static readonly System.Collections.Generic.HashSet<string> s_warned =
            new System.Collections.Generic.HashSet<string>();

        private static Type s_plateType;
        private static FieldInfo s_stepField;       // Plate.m_platingStep
        private static FieldInfo s_capacityField;   // IngredientContainer.m_capacity
        private static FieldInfo s_uidField;        // PlatingStepData.m_uID
        private static bool s_disabled;

        private static void WarnOnce(string key, string msg)
        {
            if (s_warned.Add(key))
                StubLog.LogWarn(msg);
        }

        public static void Tick()
        {
            if (s_disabled)
                return;
            try
            {
                if (s_plateType == null)
                {
                    s_plateType = GameApi.Find("Plate");
                    if (s_plateType == null)
                    {
                        WarnOnce("plateType", "[CustomStub] 杯盘调校：Plate 类型缺失，功能禁用");
                        s_disabled = true;
                        return;
                    }
                    s_stepField = s_plateType.GetField("m_platingStep",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (s_stepField == null)
                    {
                        WarnOnce("stepField", "[CustomStub] 杯盘调校：Plate.m_platingStep 缺失，功能禁用");
                        s_disabled = true;
                        return;
                    }
                }
                var plates = UnityEngine.Object.FindObjectsOfType(s_plateType);
                for (int i = 0; i < plates.Length; i++)
                {
                    var plate = plates[i] as Component;
                    if (plate != null)
                        Tune(plate);
                }
            }
            catch (Exception ex)
            {
                WarnOnce("tickEx", "[CustomStub] 杯盘调校扫描异常: " + ex.Message);
            }
        }

        private static void Tune(Component plate)
        {
            try
            {
                // 容量 → 100（实时读取，调高立即对后续检查生效）
                var container = plate.GetComponent("IngredientContainer");
                if (container != null)
                {
                    if (s_capacityField == null)
                    {
                        s_capacityField = container.GetType().GetField("m_capacity",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (s_capacityField == null)
                            WarnOnce("capacityField", "[CustomStub] 杯盘调校：IngredientContainer.m_capacity 缺失，容量调校禁用");
                    }
                    if (s_capacityField != null)
                    {
                        object cur = s_capacityField.GetValue(container);
                        if (!(cur is int) || (int)cur != TargetCapacity)
                            s_capacityField.SetValue(container, TargetCapacity);
                    }
                }
                // Glass 杯型碰撞体
                var step = s_stepField.GetValue(plate) as UnityEngine.Object;
                if (step == null || step.name != "Glass")
                    return;
                if (s_uidField == null)
                {
                    s_uidField = step.GetType().GetField("m_uID",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (s_uidField == null)
                    {
                        WarnOnce("uidField", "[CustomStub] 杯盘调校：PlatingStepData.m_uID 缺失，Glass 碰撞体调校禁用");
                        return;
                    }
                }
                if (Convert.ToInt32(s_uidField.GetValue(step)) != GlassUid)
                    return;
                var col = plate.GetComponent<BoxCollider>();
                if (col == null)
                    return;
                if (col.center != GlassCenter)
                    col.center = GlassCenter;
                if (col.size != GlassSize)
                    col.size = GlassSize;
            }
            catch (Exception ex)
            {
                WarnOnce("tuneEx", "[CustomStub] 杯盘调校异常: " + ex.Message);
            }
        }
    }
}
