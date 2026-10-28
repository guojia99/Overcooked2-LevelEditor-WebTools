using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 半成品入杯/盘回落判定（移植自被禁改的 GameUtils.CanStillFormOrder 补丁，
    /// 2026-10-27）。原版 Plate.CanPlaceOnPlate 只在「放进去后恰好等于某个配方」
    /// 时放行，空杯放牛奶/蜂蜜、热水+牛奶 这类半成品组合被弹回，玩家必须先按
    /// 配方顺序操作（例如先放茶叶凑成纯茶）。本类复刻补丁逻辑：组合是某个同
    /// platingStep 配方的真子集（还能继续加料凑成）→ 放行。
    /// 全部经反射访问宿主类型；任何成员缺失/异常 → 恒 false（=原版行为兜底）。
    /// </summary>
    internal static class PlatePartialOrderFallback
    {
        private static readonly System.Collections.Generic.HashSet<string> s_warned =
            new System.Collections.Generic.HashSet<string>();

        private static bool s_init;
        private static bool s_failed;
        private static MethodInfo s_getAllOrderNodes;   // GameUtils.GetAllOrderNodes()（宿主私有静态）
        private static Type s_orderDefType;             // OrderDefinitionNode
        private static FieldInfo s_odnPlatingPrefab;    // m_platingPrefab
        private static FieldInfo s_odnPlatingStep;      // m_platingStep
        private static MethodInfo s_odnConvert;         // Convert()
        private static Type s_compositeType;            // CompositeAssembledNode
        private static FieldInfo s_compComposition;     // m_composition
        private static FieldInfo s_compOptional;        // m_optional
        private static MethodInfo s_compContains;       // static Contains(superSet, subSet)
        private static Type s_assembledType;            // AssembledDefinitionNode
        private static MethodInfo s_simplify;           // Simpilfy()（宿主拼写如此）
        private static FieldInfo s_nullNode;            // AssembledDefinitionNode.NullNode

        private static void WarnOnce(string key, string msg)
        {
            if (s_warned.Add(key))
                StubLog.LogWarn(msg);
        }

        private static bool EnsureInit()
        {
            if (s_failed)
                return false;
            if (s_init)
                return true;
            s_init = true;
            try
            {
                Type gameUtils = GameApi.Find("GameUtils");
                s_orderDefType = GameApi.Find("OrderDefinitionNode");
                s_compositeType = GameApi.Find("CompositeAssembledNode");
                s_assembledType = GameApi.Find("AssembledDefinitionNode");
                if (gameUtils == null || s_orderDefType == null
                    || s_compositeType == null || s_assembledType == null)
                {
                    WarnOnce("types", "[CustomStub] 半成品入杯回落：宿主类型缺失，功能禁用");
                    s_failed = true;
                    return false;
                }
                s_getAllOrderNodes = gameUtils.GetMethod("GetAllOrderNodes",
                    BindingFlags.NonPublic | BindingFlags.Static);
                s_odnPlatingPrefab = s_orderDefType.GetField("m_platingPrefab",
                    BindingFlags.Public | BindingFlags.Instance);
                s_odnPlatingStep = s_orderDefType.GetField("m_platingStep",
                    BindingFlags.Public | BindingFlags.Instance);
                s_odnConvert = s_orderDefType.GetMethod("Convert",
                    BindingFlags.Public | BindingFlags.Instance);
                s_compComposition = s_compositeType.GetField("m_composition",
                    BindingFlags.Public | BindingFlags.Instance);
                s_compOptional = s_compositeType.GetField("m_optional",
                    BindingFlags.Public | BindingFlags.Instance);
                s_compContains = s_compositeType.GetMethod("Contains",
                    BindingFlags.Public | BindingFlags.Static);
                s_simplify = s_assembledType.GetMethod("Simpilfy",
                    BindingFlags.Public | BindingFlags.Instance);
                s_nullNode = s_assembledType.GetField("NullNode",
                    BindingFlags.Public | BindingFlags.Static);
                if (s_getAllOrderNodes == null || s_odnPlatingPrefab == null
                    || s_odnPlatingStep == null || s_odnConvert == null
                    || s_compComposition == null || s_compOptional == null
                    || s_compContains == null || s_simplify == null || s_nullNode == null)
                {
                    WarnOnce("members", "[CustomStub] 半成品入杯回落：宿主成员缺失，功能禁用");
                    s_failed = true;
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                WarnOnce("initEx", "[CustomStub] 半成品入杯回落初始化异常，功能禁用: " + ex.Message);
                s_failed = true;
                return false;
            }
        }

        /// <summary>m_composition ∪ m_optional（保持宿主数组元素类型，
        /// Contains 反射调用需要强类型数组）。</summary>
        private static Array Union(Array a, Array b)
        {
            ArrayList list = new ArrayList();
            if (a != null)
            {
                foreach (object o in a)
                    list.Add(o);
            }
            if (b != null)
            {
                foreach (object o in b)
                    list.Add(o);
            }
            Array result = Array.CreateInstance(s_assembledType, list.Count);
            list.CopyTo(result);
            return result;
        }

        /// <summary>与宿主补丁 GameUtils.CanStillFormOrder 等价：
        /// node 化简后的组成是某同 platingStep 配方化简组成的真子集 → true。</summary>
        public static bool CanStillFormOrder(object node, object platingStep)
        {
            if (!EnsureInit())
                return false;
            try
            {
                if (node == null || platingStep == null)
                    return false;
                object simple = s_simplify.Invoke(node, null);
                if (simple == null || ReferenceEquals(simple, s_nullNode.GetValue(null)))
                    return false;
                Array subSet;
                if (s_compositeType.IsInstanceOfType(simple))
                {
                    subSet = Union((Array)s_compComposition.GetValue(simple),
                        (Array)s_compOptional.GetValue(simple));
                }
                else
                {
                    subSet = Array.CreateInstance(s_assembledType, 1);
                    subSet.SetValue(simple, 0);
                }
                if (subSet.Length == 0)
                    return false;
                Array orders = s_getAllOrderNodes.Invoke(null, null) as Array;
                if (orders == null)
                    return false;
                for (int i = 0; i < orders.Length; i++)
                {
                    object order = orders.GetValue(i);
                    if (order == null)
                        continue;
                    // m_platingPrefab 假 null（已销毁 ScriptableObject 引用）一并挡掉
                    UnityEngine.Object prefab = s_odnPlatingPrefab.GetValue(order) as UnityEngine.Object;
                    if (prefab == null)
                        continue;
                    if (!ReferenceEquals(s_odnPlatingStep.GetValue(order), platingStep))
                        continue;
                    object recipeRoot = s_odnConvert.Invoke(order, null);
                    if (recipeRoot == null)
                        continue;
                    object recipeSimple = s_simplify.Invoke(recipeRoot, null);
                    if (!s_compositeType.IsInstanceOfType(recipeSimple))
                        continue;
                    Array superSet = Union((Array)s_compComposition.GetValue(recipeSimple),
                        (Array)s_compOptional.GetValue(recipeSimple));
                    if (superSet.Length > subSet.Length
                        && (bool)s_compContains.Invoke(null, new object[] { superSet, subSet }))
                        return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                WarnOnce("judgeEx", "[CustomStub] 半成品入杯回落判定异常（按原版拒绝处理）: " + ex.Message);
                return false;
            }
        }
    }
}
