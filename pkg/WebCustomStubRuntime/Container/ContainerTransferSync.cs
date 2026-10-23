using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 盘子 / 手持食材容器向搅拌碗、烹饪锅转移（接替 Assembly-CSharp-Patch
    /// ServerPlate / ServerPreparationContainer 的 // patch 块）。
    /// </summary>
    internal static class ContainerTransferSync
    {
        private static readonly HashSet<string> s_warnedOnce = new HashSet<string>();

        private static void WarnOnce(string key, string msg)
        {
            if (!s_warnedOnce.Add(key))
                return;
            StubLog.LogWarn(msg);
        }

        private static bool IsMixableOrCookableTarget(object container)
        {
            var mb = container as MonoBehaviour;
            if (mb == null)
                return false;
            var go = mb.gameObject;
            if (GameApi.ServerMixableContainerType != null && go.GetComponent(GameApi.ServerMixableContainerType) != null)
                return true;
            if (GameApi.ServerCookableContainerType != null && go.GetComponent(GameApi.ServerCookableContainerType) != null)
                return true;
            return false;
        }

        internal static bool ServerPlateCanTransferPrefix(object __instance, object _container, ref bool __result)
        {
            try
            {
                if (!IsMixableOrCookableTarget(_container))
                    return true;
                var comp = __instance as Component;
                if (comp == null || _container == null)
                    return true;
                if (GameApi.ServerPlateIsReservedMethod != null
                    && (bool)GameApi.ServerPlateIsReservedMethod.Invoke(comp, null))
                {
                    __result = false;
                    return false;
                }
                var ing = GameApi.ServerPlateIngredientField != null
                    ? GameApi.ServerPlateIngredientField.GetValue(comp) : null;
                if (ing == null)
                {
                    __result = false;
                    return false;
                }
                int count = InvokeInt(ing, GameApi.IngredientGetContentsCountMethod);
                if (count <= 0)
                {
                    __result = false;
                    return false;
                }
                var contents = InvokeObj(ing, GameApi.IngredientGetContentsMethod) as Array;
                if (contents == null || !InvokeBool(_container, GameApi.IngredientCanTakeContentsMethod, contents))
                {
                    __result = false;
                    return false;
                }
                for (int i = 0; i < count; i++)
                {
                    var node = InvokeObj(ing, GameApi.IngredientGetContentsElementMethod, i);
                    if (node == null)
                        continue;
                    if (!InvokeBool(null, GameApi.AssembledCanCombineMethod, node, _container))
                    {
                        __result = false;
                        return false;
                    }
                }
                __result = true;
                return false;
            }
            catch (Exception ex)
            {
                WarnOnce("plateCan", "[CustomStub] ServerPlate.CanTransfer 前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        internal static bool ServerPrepCanTransferPrefix(object __instance, object _container, ref bool __result)
        {
            try
            {
                if (!IsMixableOrCookableTarget(_container))
                    return true;
                var comp = __instance as Component;
                if (comp == null)
                    return true;
                var itemContainer = GameApi.ServerPrepItemContainerField != null
                    ? GameApi.ServerPrepItemContainerField.GetValue(comp) : null;
                if (itemContainer != null && InvokeBool(itemContainer, GameApi.HasContentsMethod))
                {
                    __result = false;
                    return false;
                }
                var prep = GameApi.ServerPrepContainerField != null
                    ? GameApi.ServerPrepContainerField.GetValue(comp) : null;
                if (prep == null || GameApi.PrepIngredientOrderNodeField == null
                    || GameApi.IngredientAssembledNodeCtor == null
                    || GameApi.AssembledCanCombineMethod == null)
                    return true;
                var orderNode = GameApi.PrepIngredientOrderNodeField.GetValue(prep);
                var assembled = GameApi.IngredientAssembledNodeCtor.Invoke(new object[] { orderNode });
                __result = InvokeBool(null, GameApi.AssembledCanCombineMethod, assembled, _container);
                return false;
            }
            catch (Exception ex)
            {
                WarnOnce("prepCan", "[CustomStub] ServerPreparationContainer.CanTransfer 前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        internal static bool ServerPrepTransferPrefix(object __instance, object _carrier, object _container, bool _dontRemove)
        {
            try
            {
                if (!IsMixableOrCookableTarget(_container))
                    return true;
                var comp = __instance as Component;
                if (comp == null)
                    return true;
                var itemContainer = GameApi.ServerPrepItemContainerField != null
                    ? GameApi.ServerPrepItemContainerField.GetValue(comp) : null;
                if (itemContainer != null && InvokeBool(itemContainer, GameApi.HasContentsMethod))
                    return false;
                var prep = GameApi.ServerPrepContainerField != null
                    ? GameApi.ServerPrepContainerField.GetValue(comp) : null;
                if (prep == null || GameApi.PrepIngredientOrderNodeField == null
                    || GameApi.IngredientAssembledNodeCtor == null
                    || GameApi.AssembledCombineMethod == null)
                    return true;
                var orderNode = GameApi.PrepIngredientOrderNodeField.GetValue(prep);
                object assembled = GameApi.IngredientAssembledNodeCtor.Invoke(new object[] { orderNode });
                if (_dontRemove && GameApi.AssembledSimplifyMethod != null)
                    assembled = GameApi.AssembledSimplifyMethod.Invoke(assembled, null);
                GameApi.AssembledCombineMethod.Invoke(null, new object[] { assembled, _container, _dontRemove });
                if (!_dontRemove && GameApi.CarrierDestroyCarriedMethod != null)
                    GameApi.CarrierDestroyCarriedMethod.Invoke(_carrier, null);
                return false;
            }
            catch (Exception ex)
            {
                WarnOnce("prepXfer", "[CustomStub] ServerPreparationContainer.Transfer 前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        /// <summary>上游 PreparationContainer 补丁（Assembly-CSharp-Patch）：携带已烹饪
        /// CookableContainer 时返回完整 ClientCookableContainer.GetOrderComposition()，
        /// 而不是只取容器第 0 个食材。真机无源码覆盖，用前缀等价实现。</summary>
        internal static bool PrepGetOrderDefinitionPrefix(object __instance, GameObject _carriedItem,
            object _itemContainer, ref object __result)
        {
            try
            {
                if (_carriedItem == null || _itemContainer == null)
                    return true;
                if (GameApi.CookableContainerType == null
                    || _carriedItem.GetComponent(GameApi.CookableContainerType) == null)
                    return true;
                if (GameApi.ClientIngredientHasContentsMethod == null
                    || GameApi.ClientContainerCanTransferMethod == null
                    || GameApi.ClientCookableGetOrderCompositionMethod == null
                    || GameApi.AssembledDefinitionNodeType == null)
                    return true;
                var clientIngredient = _carriedItem.GetComponent("ClientIngredientContainer");
                if (clientIngredient == null)
                    return true;
                if (!(bool)GameApi.ClientIngredientHasContentsMethod.Invoke(clientIngredient, null))
                    return true;
                var transfer = GetInterfaceComponent(_carriedItem, GameApi.ContainerTransferBehaviourType);
                if (transfer == null)
                    return true;
                if (!(bool)GameApi.ClientContainerCanTransferMethod.Invoke(transfer, new[] { _itemContainer }))
                    return true;
                var cookable = _carriedItem.GetComponent("ClientCookableContainer");
                if (cookable == null)
                    return true;
                var cooked = GameApi.ClientCookableGetOrderCompositionMethod.Invoke(cookable, null);
                if (cooked == null)
                    return true;
                var arr = Array.CreateInstance(GameApi.AssembledDefinitionNodeType, 1);
                arr.SetValue(cooked, 0);
                __result = arr;
                return false;
            }
            catch (Exception ex)
            {
                WarnOnce("prepGetOrder", "[CustomStub] PreparationContainer.GetOrderDefinition 前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        /// <summary>上游 ServerPreparationContainer 补丁：已烹饪转移后调用 Empty() 清空
        /// 全部内容（原版只 RemoveIngredient(0)，2+ 食材会残留）。原方法已先移除 1 个，
        /// 这里后缀清空剩余内容，最终语义等价 Empty()。</summary>
        internal static void ServerPrepHandlePlacementPostfix(object __instance, object _carrier)
        {
            try
            {
                if (_carrier == null || GameApi.ICarrierInspectCarriedMethod == null
                    || GameApi.ServerIngredientContainerType == null
                    || GameApi.IngredientEmptyMethod == null)
                    return;
                var carried = GameApi.ICarrierInspectCarriedMethod.Invoke(_carrier, null) as GameObject;
                if (carried == null)
                    return;
                if (GameApi.CookableContainerType != null
                    && carried.GetComponent(GameApi.CookableContainerType) == null)
                    return;
                var ingredientContainer = carried.GetComponent(GameApi.ServerIngredientContainerType);
                if (ingredientContainer != null)
                    GameApi.IngredientEmptyMethod.Invoke(ingredientContainer, null);
            }
            catch (Exception ex)
            {
                WarnOnce("prepHandlePlacement", "[CustomStub] ServerPreparationContainer.HandlePlacement 后缀异常（忽略）: " + ex.Message);
            }
        }

        private static object GetInterfaceComponent(GameObject go, Type interfaceType)
        {
            if (go == null || interfaceType == null)
                return null;
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] != null && interfaceType.IsInstanceOfType(comps[i]))
                    return comps[i];
            }
            return null;
        }

        private static int InvokeInt(object target, MethodInfo method, params object[] args)
        {
            if (method == null)
                return 0;
            return (int)method.Invoke(target, args);
        }

        private static bool InvokeBool(object target, MethodInfo method, params object[] args)
        {
            if (method == null)
                return false;
            return (bool)method.Invoke(target, args);
        }

        private static object InvokeObj(object target, MethodInfo method, params object[] args)
        {
            if (method == null)
                return null;
            return method.Invoke(target, args);
        }
    }
}
