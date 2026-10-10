using UnityEngine;

namespace CustomStub
{
    /// <summary>摇杆组 Terminal：邻格扫描失败时，半格内 / 同 pilot 组仍允许 CanInteract。</summary>
    internal static class AnimPilotJoystickInteract
    {
        internal static void ApplyProximityCanInteract(Component interactableSync, Component player, ref bool result)
        {
            if (result || interactableSync == null || player == null || GameApi.TerminalType == null)
                return;
            var terminal = ResolveTerminalFromInteractable(interactableSync);
            if (terminal == null)
                return;
            if (!AnimPilotJoystickProximity.IsBoundPilotJoystickTerminal(terminal))
                return;
            if (!AnimPilotJoystickProximity.IsTerminalTriggerable(terminal))
                return;
            if (!AnimPilotJoystickProximity.IsPlayerNearPilotJoystick(terminal, player))
                return;
            result = true;
        }

        /// <summary>邻格未命中时，半格内优先把 pilot 摇杆 ClientInteractable 写入 InteractionObjects。</summary>
        internal static void TryInjectPilotJoystickInteractionObjects(Component playerControls,
            object interactionObjects)
        {
            if (playerControls == null || interactionObjects == null
                || GameApi.InteractionObjectsInteractableField == null
                || GameApi.ClientInteractableType == null)
                return;
            if (AnimPilotInteractionInjectGuards.HasPickupOrPlacement(interactionObjects))
                return;
            Component clientInteract;
            if (!TryFindNearestPilotClientInteractable(playerControls, out clientInteract))
                return;
            try
            {
                var current = GameApi.InteractionObjectsInteractableField.GetValue(interactionObjects);
                if (current == clientInteract)
                    return;
                GameApi.InteractionObjectsInteractableField.SetValue(interactionObjects, clientInteract);
            }
            catch
            {
            }
        }

        /// <summary>按交互键时邻格扫描失败：直接 TriggerInteractable 摇杆。</summary>
        internal static void TryTriggerNearbyPilotJoystickOnUsePressed(Component clientControlsImpl,
            bool justPressed)
        {
            if (!justPressed || clientControlsImpl == null
                || GameApi.ClientTriggerInteractableMethod == null)
                return;
            if (GameApi.ClientGetCurrentlyInteractingMethod != null)
            {
                try
                {
                    var busy = GameApi.ClientGetCurrentlyInteractingMethod.Invoke(clientControlsImpl, null);
                    if (busy != null)
                        return;
                }
                catch
                {
                }
            }
            Component clientInteract;
            if (!TryFindNearestPilotClientInteractable(clientControlsImpl, out clientInteract))
                return;
            try
            {
                GameApi.ClientTriggerInteractableMethod.Invoke(clientControlsImpl,
                    new object[] { clientInteract });
            }
            catch
            {
            }
        }

        private static bool TryFindNearestPilotClientInteractable(Component player,
            out Component clientInteractable)
        {
            clientInteractable = null;
            if (player == null)
                return false;
            var markers = Object.FindObjectsOfType<AnimPilotFloorMarker>();
            if (markers == null || markers.Length == 0)
                return false;
            float bestSq = float.MaxValue;
            Component best = null;
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null)
                    continue;
                var terminal = AnimPilotFloorDrive.ResolveTerminalForProximity(marker.JoystickPseudoRoot,
                    marker.gameObject);
                if (terminal == null)
                    continue;
                if (!AnimPilotJoystickProximity.IsBoundPilotJoystickTerminal(terminal))
                    continue;
                if (!AnimPilotJoystickProximity.IsTerminalTriggerable(terminal))
                    continue;
                if (!AnimPilotJoystickProximity.IsPlayerNearPilotJoystick(terminal, player))
                    continue;
                var ci = ResolveClientInteractable(terminal);
                if (ci == null)
                    continue;
                Vector3 anchor = player.transform.position;
                Vector3 joy = terminal.transform.position;
                float dx = anchor.x - joy.x;
                float dz = anchor.z - joy.z;
                float sq = dx * dx + dz * dz;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = ci;
                }
            }
            if (best == null)
                return false;
            clientInteractable = best;
            return true;
        }

        private static Component ResolveTerminalFromInteractable(Component interactableOrHighlight)
        {
            if (interactableOrHighlight == null || GameApi.TerminalType == null)
                return null;
            var go = interactableOrHighlight.gameObject;
            var terminal = GameApi.GetComponent(go, GameApi.TerminalType) as Component;
            if (terminal != null)
                return terminal;
            terminal = GameApi.GetComponentInParent(interactableOrHighlight, GameApi.TerminalType);
            if (terminal != null)
                return terminal;
            return GameApi.GetComponentInChildren(go, GameApi.TerminalType);
        }

        private static Component ResolveClientInteractable(Component terminal)
        {
            if (terminal == null || GameApi.ClientInteractableType == null)
                return null;
            var go = terminal.gameObject;
            var ci = GameApi.GetComponent(go, GameApi.ClientInteractableType);
            if (ci != null)
                return ci;
            return GameApi.GetComponentInChildren(go, GameApi.ClientInteractableType);
        }
    }
}
