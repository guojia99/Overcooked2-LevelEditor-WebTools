using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 摇杆组绑定的 Terminal：原版按主网格邻格扫描，地板挪到刁钻位置时邻格对不上。
    /// 本类用「同 pilot 组子物体」或「水平半格内」作靠近判定（交互补丁共用；灯不看邻近）。
    /// </summary>
    internal static class AnimPilotJoystickProximity
    {
        internal const float HalfCellMeters = 0.6f;
        private const float HalfCellSq = HalfCellMeters * HalfCellMeters;

        private static AnimPilotFloorMarker[] s_markers;
        private static int s_markersFrame = -1;

        internal static bool IsBoundPilotJoystickTerminal(Component terminalComp)
        {
            AnimPilotFloorMarker marker;
            return TryFindMarkerForTerminal(terminalComp, out marker);
        }

        internal static bool IsTerminalTriggerable(Component terminalComp)
        {
            if (terminalComp == null || GameApi.TerminalPilotableField == null)
                return false;
            try
            {
                if (GameApi.TerminalPilotableField.GetValue(terminalComp) == null)
                    return false;
            }
            catch
            {
                return false;
            }
            if (GameApi.InteractableType == null)
                return true;
            var interactable = GameApi.GetComponent(terminalComp.gameObject, GameApi.InteractableType) as Behaviour;
            if (interactable == null)
                return true;
            return interactable.enabled;
        }

        internal static bool IsPlayerNearPilotJoystick(Component terminalComp, Component player)
        {
            if (terminalComp == null || player == null)
                return false;
            AnimPilotFloorMarker marker;
            if (!TryFindMarkerForTerminal(terminalComp, out marker))
                return false;
            var pilotRoot = marker.transform;
            if (player.transform != null && player.transform.IsChildOf(pilotRoot))
                return true;
            Vector3 anchor = GetJoystickWorldAnchor(marker, terminalComp);
            Vector3 pos = player.transform.position;
            float dx = pos.x - anchor.x;
            float dz = pos.z - anchor.z;
            return dx * dx + dz * dz <= HalfCellSq;
        }

        internal static bool IsAnyLocalPlayerNearPilotJoystick(Component terminalComp)
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
                    if (IsPlayerNearPilotJoystick(terminalComp, pc))
                        return true;
                }
                catch
                {
                }
            }
            return false;
        }

        internal static bool TryFindMarkerForTerminal(Component terminalComp, out AnimPilotFloorMarker marker)
        {
            marker = null;
            if (terminalComp == null)
                return false;
            var markers = GetMarkersCached();
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m == null)
                    continue;
                var resolved = AnimPilotFloorDrive.ResolveTerminalForProximity(m.JoystickPseudoRoot, m.gameObject);
                if (resolved == null)
                    continue;
                if (resolved == terminalComp || resolved.gameObject == terminalComp.gameObject)
                {
                    marker = m;
                    return true;
                }
            }
            return false;
        }

        private static AnimPilotFloorMarker[] GetMarkersCached()
        {
            int frame = Time.frameCount;
            if (s_markers != null && s_markersFrame == frame)
                return s_markers;
            s_markersFrame = frame;
            s_markers = Object.FindObjectsOfType<AnimPilotFloorMarker>();
            if (s_markers == null)
                s_markers = new AnimPilotFloorMarker[0];
            return s_markers;
        }

        private static Vector3 GetJoystickWorldAnchor(AnimPilotFloorMarker marker, Component terminalComp)
        {
            if (marker != null && marker.JoystickPseudoRoot != null)
            {
                var jw = marker.JoystickPseudoRoot;
                var cols = jw.GetComponentsInChildren<Collider>();
                if (cols != null && cols.Length > 0)
                {
                    Bounds b = cols[0].bounds;
                    for (int i = 1; i < cols.Length; i++)
                    {
                        if (cols[i] != null)
                            b.Encapsulate(cols[i].bounds);
                    }
                    return b.center;
                }
                if (jw.transform != null)
                    return jw.transform.position;
            }
            if (terminalComp != null && terminalComp.transform != null)
                return terminalComp.transform.position;
            return Vector3.zero;
        }
    }
}
