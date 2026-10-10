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
