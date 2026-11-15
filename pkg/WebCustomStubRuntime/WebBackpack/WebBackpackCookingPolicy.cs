using System;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    public partial class WebBackpackStorage
    {
        private static bool HasCookableIngredient(GameObject item)
        {
            var type = GameApi.Find("CookableIngredient");
            return item != null && type != null && item.GetComponent(type) != null;
        }

        // The native cooked enum stays Raw during partial cooking. Only exact zero progress
        // is losslessly recreated by spawning the known raw prefab; no cooked state is stored.
        internal static bool IsUncookedPhysical(GameObject item, bool prefab)
        {
            if (!HasCookableIngredient(item)) return true;
            try
            {
                var type = GameApi.Find("ServerCookingHandler");
                var server = type != null ? item.GetComponent(type) : null;
                var get = type != null ? type.GetMethod("GetCookingProgress", Type.EmptyTypes) : null;
                if (server == null) return prefab && CookablePrefabNode(item) != null;
                if (get == null) return false;
                return (float)get.Invoke(server, null) == 0f && CookablePrefabNode(item) != null;
            }
            catch (Exception) { return false; }
        }

        private static object CookablePrefabNode(GameObject prefab)
        {
            if (!HasCookableIngredient(prefab)) return null;
            try
            {
                var component = prefab.GetComponent(GameApi.Find("CookableIngredient"));
                var field = component.GetType().GetField("m_ingredientOrderNode");
                var ingredient = field != null ? field.GetValue(component) : null;
                var handlerType = GameApi.Find("CookingHandler");
                var handler = handlerType != null ? prefab.GetComponent(handlerType) : null;
                if (ingredient == null || handler == null || GameApi.NodeConvertMethod == null) return null;
                var step = handlerType.GetField("m_cookingType").GetValue(handler);
                float time = (float)handlerType.GetField("m_cookingtime").GetValue(handler);
                if (step == null || !(time > 0f) || float.IsInfinity(time)) return null;
                var simple = GameApi.NodeConvertMethod.Invoke(ingredient, null);
                if (!IsBareHandNodeType(simple)) return null;
                var type = GameApi.Find("CookedCompositeAssembledNode");
                if (type == null || GameApi.AssembledDefinitionNodeType == null) return null;
                var node = Activator.CreateInstance(type);
                var composition = Array.CreateInstance(GameApi.AssembledDefinitionNodeType, 1);
                composition.SetValue(simple, 0);
                type.GetField("m_composition").SetValue(node, composition);
                type.GetField("m_cookingStep").SetValue(node, step);
                var progress = type.GetField("m_progress");
                progress.SetValue(node, Enum.Parse(progress.FieldType, "Raw"));
                type.GetField("m_recordedProgress").SetValue(node, (float?)0f);
                return node;
            }
            catch (Exception) { return null; }
        }

        private static object RuntimeCookableNode(GameObject item)
        {
            try
            {
                var type = GameApi.Find("ServerCookableIngredient");
                var server = type != null ? item.GetComponent(type) : null;
                var get = type != null ? type.GetMethod("GetOrderComposition", Type.EmptyTypes) : null;
                return server != null && get != null ? get.Invoke(server, null) : null;
            }
            catch (Exception) { return null; }
        }

        internal static object StorageNodeFromPrefab(GameObject prefab)
        {
            if (HasCookableIngredient(prefab))
                return IsUncookedPhysical(prefab, true) ? CookablePrefabNode(prefab) : null;
            return NodeFromPrefab(prefab) ?? NodeFromWorkableSource(prefab);
        }

        internal static bool IsStorablePhysicalPrefab(GameObject prefab, bool workableChain)
        {
            if (HasCookableIngredient(prefab))
                return IsUncookedPhysical(prefab, true);
            return workableChain || IsSimpleIngredientPrefab(prefab);
        }

        private static bool IsExactRawCookableNode(object node, GameObject prefab)
        {
            try
            {
                var expected = CookablePrefabNode(prefab);
                if (node == null || expected == null || node.GetType() != expected.GetType()) return false;
                var type = node.GetType();
                var progress = type.GetField("m_progress");
                var recorded = type.GetField("m_recordedProgress").GetValue(node);
                if (progress.GetValue(node).ToString() != "Raw" || !(recorded is float) || (float)recorded != 0f) return false;
                var step = type.GetField("m_cookingStep").GetValue(node);
                var expectedStep = type.GetField("m_cookingStep").GetValue(expected);
                if (!ReferenceEquals(step, expectedStep)) return false;
                var composition = type.GetField("m_composition").GetValue(node) as Array;
                var expectedComposition = type.GetField("m_composition").GetValue(expected) as Array;
                var optional = type.GetField("m_optional").GetValue(node) as Array;
                return composition != null && composition.Length == 1 && expectedComposition != null
                    && composition.GetValue(0).Equals(expectedComposition.GetValue(0))
                    && (optional == null || optional.Length == 0);
            }
            catch (Exception) { return false; }
        }

        private static bool CanPlateStoredSlot(StoredSlot slot)
        {
            // Raw/part-worked physical forms must leave by hand to preserve Workable state.
            // A finished chopping-stage raw meat has no Workable and may transfer its original
            // Raw CookedComposite node to a plate, just like the native ingredient transfer.
            return slot.Entry == null || !slot.HasWorkable;
        }
    }
}
