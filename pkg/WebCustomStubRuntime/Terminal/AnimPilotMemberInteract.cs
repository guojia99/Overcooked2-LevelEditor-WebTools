using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 摇杆地板组内工作台/道具：邻格扫描在平台停到半格或非格心对齐时易失效，
    /// 半格内按水平距离兜底 CanInteract / InteractionObjects（半径刻意小于 1.2m
    /// 邻格间距，且 CanInteract 仅授予最近一台，避免半格布局下多台互抢）。
    /// （摇杆 Terminal 仍由 AnimPilotJoystickInteract 处理。）
    /// </summary>
    internal static class AnimPilotMemberInteract
    {
        /// <summary>半格 0.6m + 余量：覆盖半格对齐停靠，够不到相邻工作台格心（1.2m）。</summary>
        internal const float MemberInteractMeters = AnimPilotJoystickProximity.HalfCellMeters + 0.08f;
        internal const float MemberInteractSq = MemberInteractMeters * MemberInteractMeters;
        private const string PilotFloorWalkBoxName = "PilotFloor";

        internal static void ApplyProximityCanInteract(Component interactableSync, Component player, ref bool result)
        {
            if (result || interactableSync == null || player == null)
                return;
            if (IsPilotJoystickTerminalHierarchy(interactableSync.transform))
                return;
            AnimPilotFloorMarker marker;
            if (!AnimPilotMemberProximity.TryGetPilotMarkerForTransform(interactableSync.transform, out marker))
                return;
            if (!AnimPilotMemberProximity.IsPlayerNearTransform(marker, player, interactableSync.transform))
                return;
            if (!IsNearestPilotMemberInteractable(interactableSync, player))
                return;
            result = true;
        }

        internal static void TryInjectPilotMemberInteractionObjects(Component playerControls,
            object interactionObjects)
        {
            if (playerControls == null || interactionObjects == null
                || GameApi.InteractionObjectsInteractableField == null
                || GameApi.ClientInteractableType == null)
                return;
            if (AnimPilotInteractionInjectGuards.HasPickupOrPlacement(interactionObjects))
                return;
            try
            {
                var current = GameApi.InteractionObjectsInteractableField.GetValue(interactionObjects);
                if (current != null)
                    return;
            }
            catch
            {
                return;
            }
            Component clientInteract;
            if (!TryFindNearestPilotMemberClientInteractable(playerControls, out clientInteract))
                return;
            try
            {
                GameApi.InteractionObjectsInteractableField.SetValue(interactionObjects, clientInteract);
            }
            catch
            {
            }
        }

        internal static void TryTriggerNearbyPilotMemberOnUsePressed(Component clientControls, bool justPressed)
        {
            if (!justPressed || clientControls == null
                || GameApi.ClientTriggerInteractableMethod == null)
                return;
            if (GameApi.ClientGetCurrentlyInteractingMethod != null)
            {
                try
                {
                    var busy = GameApi.ClientGetCurrentlyInteractingMethod.Invoke(clientControls, null);
                    if (busy != null)
                        return;
                }
                catch
                {
                }
            }
            Component clientInteract;
            if (!TryFindNearestPilotMemberClientInteractable(clientControls, out clientInteract))
                return;
            try
            {
                GameApi.ClientTriggerInteractableMethod.Invoke(clientControls,
                    new object[] { clientInteract });
            }
            catch
            {
            }
        }

        private static bool TryFindNearestPilotMemberClientInteractable(Component player,
            out Component clientInteractable)
        {
            clientInteractable = null;
            if (player == null || GameApi.ClientInteractableType == null)
                return false;
            var markers = Object.FindObjectsOfType<AnimPilotFloorMarker>();
            if (markers == null || markers.Length == 0)
                return false;
            float bestSq = float.MaxValue;
            Component best = null;
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null || marker.transform == null)
                    continue;
                var pilotRoot = marker.transform;
                for (int c = 0; c < pilotRoot.childCount; c++)
                {
                    var member = pilotRoot.GetChild(c);
                    if (member.name == PilotFloorWalkBoxName)
                        continue;
                    var list = GameApi.GetComponentsInChildren(member.gameObject,
                        GameApi.ClientInteractableType, true);
                    if (list == null)
                        continue;
                    for (int j = 0; j < list.Length; j++)
                    {
                        var ci = list[j] as Component;
                        if (ci == null || ci.transform == null)
                            continue;
                        if (IsPilotJoystickTerminalHierarchy(ci.transform))
                            continue;
                        if (IsRuntimePlateStackChildInteractable(ci))
                            continue;
                        if (!AnimPilotMemberProximity.IsPlayerNearTransform(marker, player, ci.transform))
                            continue;
                        float sq = HorizontalDistanceSq(player.transform.position,
                            AnimPilotMemberProximity.GetWorldAnchor(ci.transform));
                        if (sq < bestSq)
                        {
                            bestSq = sq;
                            best = ci;
                        }
                    }
                }
            }
            if (best == null)
                return false;
            clientInteractable = best;
            return true;
        }

        private static bool IsPilotJoystickTerminalHierarchy(Transform t)
        {
            if (t == null || GameApi.TerminalType == null)
                return false;
            var terminal = GameApi.GetComponentInParent(t, GameApi.TerminalType);
            if (terminal == null)
                terminal = GameApi.GetComponent(t.gameObject, GameApi.TerminalType);
            if (terminal == null)
                return false;
            return AnimPilotJoystickProximity.IsBoundPilotJoystickTerminal(terminal);
        }

        private static bool IsRuntimePlateStackChildInteractable(Component clientInteractable)
        {
            if (clientInteractable == null || GameApi.ClientPlateStackBaseType == null)
                return false;
            var stack = GameApi.GetComponentInParent(clientInteractable, GameApi.ClientPlateStackBaseType);
            if (stack == null)
                return false;
            return stack.gameObject != clientInteractable.gameObject;
        }

        /// <summary>兜底 CanInteract 只给距玩家最近的一台组内 ClientInteractable（防多台同时 true）。</summary>
        private static bool IsNearestPilotMemberInteractable(Component interactableSync, Component player)
        {
            Component nearest;
            if (!TryFindNearestPilotMemberClientInteractable(player, out nearest))
                return false;
            return IsSamePilotMemberInteractableTarget(interactableSync, nearest);
        }

        private static bool IsSamePilotMemberInteractableTarget(Component a, Component b)
        {
            if (a == null || b == null)
                return false;
            if (a == b)
                return true;
            if (a.gameObject == b.gameObject)
                return true;
            var ta = a.transform;
            var tb = b.transform;
            if (ta == null || tb == null)
                return false;
            if (tb.IsChildOf(ta) || ta.IsChildOf(tb))
                return true;
            return false;
        }

        private static float HorizontalDistanceSq(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }

    internal static class AnimPilotMemberProximity
    {
        private const string PilotFloorWalkBoxName = "PilotFloor";

        internal static bool TryGetPilotMarkerForTransform(Transform t, out AnimPilotFloorMarker marker)
        {
            marker = null;
            if (t == null)
                return false;
            var markers = Object.FindObjectsOfType<AnimPilotFloorMarker>();
            if (markers == null)
                return false;
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m == null || m.transform == null)
                    continue;
                if (t == m.transform || t.IsChildOf(m.transform))
                {
                    marker = m;
                    return true;
                }
            }
            return false;
        }

        internal static bool IsPlayerNearTransform(AnimPilotFloorMarker marker, Component player, Transform target)
        {
            if (marker == null || player == null || player.transform == null || target == null)
                return false;
            return HorizontalDistanceSq(player.transform.position, GetWorldAnchor(target))
                <= AnimPilotMemberInteract.MemberInteractSq;
        }

        internal static bool IsAnyLocalPlayerNearTransform(AnimPilotFloorMarker marker, Transform target)
        {
            if (GameApi.PlayerControlsType == null || GameApi.PlayerIDProviderProperty == null
                || GameApi.IsLocallyControlledMethod == null)
                return false;
            var players = GameApi.FindAll(GameApi.PlayerControlsType);
            if (players == null || players.Length == 0)
                return false;
            for (int i = 0; i < players.Length; i++)
            {
                var pc = players[i] as Component;
                if (pc == null)
                    continue;
                try
                {
                    var provider = GameApi.PlayerIDProviderProperty.GetValue(pc, null);
                    if (provider == null)
                        continue;
                    if (!(bool)GameApi.IsLocallyControlledMethod.Invoke(provider, null))
                        continue;
                    if (IsPlayerNearTransform(marker, pc, target))
                        return true;
                }
                catch
                {
                }
            }
            return false;
        }

        internal static Vector3 GetWorldAnchor(Transform t)
        {
            if (t == null)
                return Vector3.zero;
            // 水平距离用成员根 XZ（半格布局上大碰撞盒会把中心拽向邻台，放大抢判范围）。
            var memberRoot = GetPilotMemberRoot(t);
            if (memberRoot != null)
                return memberRoot.position;
            return t.position;
        }

        private static Transform GetPilotMemberRoot(Transform t)
        {
            if (t == null)
                return null;
            Transform cur = t;
            while (cur.parent != null)
            {
                var parent = cur.parent;
                if (parent.GetComponent<AnimPilotFloorMarker>() != null)
                {
                    if (cur.name == PilotFloorWalkBoxName)
                        return t;
                    return cur;
                }
                cur = parent;
            }
            return t;
        }

        private static float HorizontalDistanceSq(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
