using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomStub
{
    internal static class WebBackpackRecoveryIsolation
    {
        internal static bool RecoveryCall;
        internal static object RecoveryCarrier;

        internal static void Install(Harmony harmony, List<MethodBase> targets)
        {
            var patched = new HashSet<MethodBase>();
            var prefix = new HarmonyMethod(typeof(WebBackpackRecoveryIsolation).GetMethod("Allow", BindingFlags.Static | BindingFlags.NonPublic));
            var carrier = GameApi.Find("ServerPlayerAttachmentCarrier");
            var controls = GameApi.Find("ServerPlayerControlsImpl_Default");
            if (carrier == null || controls == null) throw new MissingMemberException("recovery carrier/control API");
            foreach (var type in carrier.Assembly.GetTypes())
            {
                if (type.IsInterface || type.IsAbstract) continue;
                bool transfer = Implements(type, "IContainerTransferBehaviour");
                bool throwable = Implements(type, "IThrowable");
                bool catcher = Implements(type, "IHandleCatch");
                bool pickup = Implements(type, "IHandlePickup");
                bool placement = Implements(type, "IHandlePlacement");
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    string name = method.Name;
                    bool match = (type == carrier && (name == "TakeItem" || name == "DestroyCarriedItem" || name == "CarryItem"))
                        || (type == controls && (name == "OnChefEvent" || name == "Update_Impl" || name.StartsWith("Receive", StringComparison.Ordinal)))
                        || (transfer && name == "TransferToContainer")
                        || (throwable && name == "HandleThrow")
                        || (catcher && name == "HandleCatch")
                        || (pickup && name == "HandlePickup")
                        || (placement && name == "HandlePlacement")
                        || (type.Name == "ServerAttachStation" && name == "AddItem")
                        || (type.Name == "ServerPhysicalAttachment" && name == "Attach");
                    if (!match || method.IsAbstract || !patched.Add(method)) continue;
                    targets.Add(method);
                    harmony.Patch(method, prefix);
                }
            }
        }

        private static bool Implements(Type type, string name)
        {
            foreach (var contract in type.GetInterfaces()) if (contract.Name == name) return true;
            return false;
        }

        private static bool Reserved(object value)
        {
            if (value == null) return false;
            var component = value as Component;
            var go = value as GameObject;
            if (component != null) go = component.gameObject;
            if (go != null && WebBackpackStorage.IsRecoveryReserved(go)) return true;
            GameObject held;
            if (WebBackpackStorage.InspectForRecovery(value, out held) && WebBackpackStorage.IsRecoveryReserved(held)) return true;
            return false;
        }

        private static bool Allow(object __instance, object[] __args, MethodBase __originalMethod)
        {
            if (!WebBackpackStorage.HasRecoveryReservations) return true;
            if (RecoveryCall && ReferenceEquals(__instance, RecoveryCarrier)
                && (__originalMethod.Name == "TakeItem" || __originalMethod.Name == "DestroyCarriedItem")) return true;
            if (Reserved(__instance)) return false;
            foreach (var arg in __args) if (Reserved(arg)) return false;
            // Native OnChefEvent ignores enabled flags, so disabling PlayerControls alone cannot
            // isolate an outstanding transfer. Gate this exact carrier's dispatcher as well.
            if (__instance != null && __instance.GetType().Name == "ServerPlayerControlsImpl_Default")
            {
                var field = __instance.GetType().GetField("m_iCarrier", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && Reserved(field.GetValue(__instance))) return false;
            }
            return true;
        }
    }
}
