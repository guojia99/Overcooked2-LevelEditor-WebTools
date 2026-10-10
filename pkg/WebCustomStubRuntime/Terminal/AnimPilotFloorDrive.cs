using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomStub
{
    /// <summary>
    /// 摇杆遥控地板组（AnimPilotFloorMarker + 组根 ServerPilotMovement）运行时修复：
    ///  - 未进入终端会话时冻结 pilot 位移/吸附，并释放区域占格；
    ///  - 静止或松杆时不持久占用 GridRegion（避免压住组内脏杯台 DynamicGridLocation）；
    ///  - 终端会话期间把厨师 parent 到 pilot，防止地板移走厨师坠空。
    /// 全部经 Harmony 挂钩宿主 ServerPilotMovement / ClientPilotMovement，不改 Assembly-CSharp。
    /// </summary>
    internal static class AnimPilotFloorDrive
    {
        private const string LegacyAuthorityTypeName = "LevelEditor.PilotDriveAuthority";
        private const float SettleVelocitySq = 0.0004f;
        private const float PositionLockEpsilonSq = 1e-6f;

        private static readonly Dictionary<int, ChefParentState> s_parentByPilotId =
            new Dictionary<int, ChefParentState>();

        private struct PositionLockState
        {
            public Vector3 WorldPos;
            public Component PilotMovement;
            public Transform SyncRoot;
        }

        private static readonly Dictionary<int, PositionLockState> s_positionLocksByPilotSyncId =
            new Dictionary<int, PositionLockState>();

        private struct ChefParentState
        {
            public GameObject Chef;
            public bool Parented;
        }

        internal static bool ProbeHasAnimPilotFloor()
        {
            if (Object.FindObjectOfType<AnimPilotFloorMarker>() != null)
                return true;
            return ProbeLegacyAuthorityMarker();
        }

        private static bool ProbeLegacyAuthorityMarker()
        {
            var all = Object.FindObjectsOfType<MonoBehaviour>();
            for (int i = 0; i < all.Length; i++)
            {
                var mb = all[i];
                if (mb != null && mb.GetType().FullName == LegacyAuthorityTypeName)
                    return true;
            }
            return false;
        }

        internal static bool IsAnimPilotGroup(GameObject go)
        {
            if (go == null)
                return false;
            if (go.GetComponent<AnimPilotFloorMarker>() != null)
                return true;
            return HasLegacyAuthority(go);
        }

        private static bool HasLegacyAuthority(GameObject go)
        {
            var behaviours = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb != null && mb.GetType().FullName == LegacyAuthorityTypeName)
                    return true;
            }
            return false;
        }

        private static AnimPilotFloorMarker GetMarker(GameObject pilotRoot)
        {
            return pilotRoot != null ? pilotRoot.GetComponent<AnimPilotFloorMarker>() : null;
        }

        /// <summary>是否跑原版 ServerPilotMovement.Update（会话/scheme 生命周期与动作键退出）。</summary>
        private static bool AllowVanillaPilotUpdate(GameObject pilotRoot)
        {
            var marker = GetMarker(pilotRoot);
            if (marker != null)
                return AllowVanillaPilotUpdateForMarker(pilotRoot, marker.JoystickPseudoRoot);
            if (HasLegacyAuthority(pilotRoot))
                return AllowVanillaPilotUpdateForMarker(pilotRoot, TryReadLegacyJoystickRoot(pilotRoot));
            return true;
        }

        private static bool AllowVanillaPilotUpdateForMarker(GameObject pilotRoot, GameObject boundJoystick)
        {
            if (boundJoystick == null)
                return HasAssignedControlScheme(pilotRoot);
            var terminal = ResolveTerminal(boundJoystick, pilotRoot);
            if (terminal != null && IsTerminalSessionActive(terminal))
                return true;
            return HasAssignedControlScheme(pilotRoot);
        }

        /// <summary>与 JoystickMarkerLink 一致：m_session 非空 = 终端占用中。</summary>
        private static bool IsTerminalSessionActive(Component terminal)
        {
            if (terminal == null || GameApi.SessionField == null)
                return false;
            var termGo = terminal.gameObject;
            if (GameApi.ServerTerminalType != null)
            {
                var st = GameApi.GetComponent(termGo, GameApi.ServerTerminalType);
                if (st != null)
                {
                    try
                    {
                        if (GameApi.SessionField.GetValue(st) != null)
                            return true;
                    }
                    catch
                    {
                    }
                }
            }
            if (GameApi.ServerSessionInteractableType != null)
            {
                var ss = GameApi.GetComponent(termGo, GameApi.ServerSessionInteractableType);
                if (ss != null)
                {
                    try
                    {
                        if (GameApi.SessionField.GetValue(ss) != null)
                            return true;
                    }
                    catch
                    {
                    }
                }
            }
            return false;
        }

        private static GameObject TryReadLegacyJoystickRoot(GameObject pilotRoot)
        {
            var behaviours = pilotRoot.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb == null || mb.GetType().FullName != LegacyAuthorityTypeName)
                    continue;
                var field = mb.GetType().GetField("boundTerminalPseudoRoot",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field == null)
                    field = mb.GetType().GetField("boundTerminalPseudoRoot",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (field != null)
                    return field.GetValue(mb) as GameObject;
            }
            return null;
        }

        private static bool HasAssignedControlScheme(GameObject pilotRoot)
        {
            if (GameApi.PilotMovementType == null || GameApi.PilotControlSchemeField == null)
                return false;
            var sync = pilotRoot.GetComponent(GameApi.PilotMovementType);
            if (sync == null)
                return false;
            return GameApi.PilotControlSchemeField.GetValue(sync) != null;
        }

        internal static Component ResolveTerminalForProximity(GameObject joystickPseudoRoot, GameObject pilotRoot)
        {
            return ResolveTerminal(joystickPseudoRoot, pilotRoot);
        }

        private static Component ResolveTerminal(GameObject joystickPseudoRoot, GameObject pilotRoot)
        {
            if (joystickPseudoRoot != null && GameApi.TerminalType != null)
            {
                var ppType = GameApi.Find("PseudoPrefab");
                if (ppType != null)
                {
                    var pp = joystickPseudoRoot.GetComponent(ppType);
                    if (pp != null)
                    {
                        var childField = ppType.GetField("childGameObject");
                        if (childField != null)
                        {
                            var child = childField.GetValue(pp) as GameObject;
                            if (child != null)
                            {
                                var onChild = GameApi.GetComponent(child, GameApi.TerminalType);
                                if (onChild != null)
                                    return onChild;
                            }
                        }
                    }
                }
                var fromPseudo = GameApi.GetComponentInChildren(joystickPseudoRoot, GameApi.TerminalType);
                if (fromPseudo != null)
                    return fromPseudo;
            }
            if (pilotRoot == null || GameApi.TerminalType == null || GameApi.TerminalPilotableField == null)
                return null;
            var pilotVanilla = GameApi.GetComponent(pilotRoot, GameApi.PilotMovementVanillaType);
            var terminals = GameApi.GetComponentsInChildren(pilotRoot, GameApi.TerminalType, true);
            for (int i = 0; i < terminals.Length; i++)
            {
                var t = terminals[i];
                if (t == null)
                    continue;
                var po = GameApi.TerminalPilotableField.GetValue(t);
                if (po != null && pilotVanilla != null && po == pilotVanilla)
                    return t;
            }
            return terminals.Length > 0 ? terminals[0] : null;
        }

        internal static bool ServerUpdatePrefix(object serverPilot)
        {
            var comp = serverPilot as Component;
            if (comp == null || !IsAnimPilotGroup(comp.gameObject))
                return true;
            if (!AllowVanillaPilotUpdate(comp.gameObject))
            {
                ApplyFrozenTick(comp);
                return false;
            }
            var scheme = GetControlScheme(comp);
            if (scheme == null)
            {
                ApplyFrozenTick(comp);
                return false;
            }
            var pilotMove = GetPilotMovement(comp);
            if (HasDriveInput(scheme))
            {
                ClearPositionLock(comp);
                if (pilotMove != null)
                    SetPilotKinematic(pilotMove, false);
                return true;
            }
            if (GetGridTarget(comp) != null)
            {
                if (!IsPilotNearlyStill(comp))
                {
                    ClearPositionLock(comp);
                    if (pilotMove != null)
                        SetPilotKinematic(pilotMove, false);
                    return true;
                }
                ClearGridTarget(comp);
            }
            if (pilotMove != null)
                ApplyIdlePilotFreeze(pilotMove);
            RememberPositionLock(comp);
            EnforcePositionLock(comp, pilotMove);
            return true;
        }

        private static bool IsPilotStickIdle(Component serverPilot, object scheme)
        {
            if (scheme == null)
                return false;
            if (HasDriveInput(scheme))
                return false;
            if (GetGridTarget(serverPilot) != null)
                return false;
            return true;
        }

        /// <summary>物理步之间维持锚定 + 终端/ scheme 对账（EntryPoint ticker）。</summary>
        internal static void TickPositionLocks()
        {
            if (s_positionLocksByPilotSyncId.Count > 0)
            {
                var keys = new List<int>(s_positionLocksByPilotSyncId.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    int id = keys[i];
                    PositionLockState state;
                    if (!s_positionLocksByPilotSyncId.TryGetValue(id, out state))
                        continue;
                    if (state.SyncRoot == null)
                    {
                        s_positionLocksByPilotSyncId.Remove(id);
                        continue;
                    }
                    EnforcePositionLockState(state);
                }
            }
            if (Time.frameCount % 30 != 0)
                return;
            if (!GameApi.IsServerMachine())
                return;
            TickReconcilePilotTerminalState();
        }

        private static void TickReconcilePilotTerminalState()
        {
            var markers = Object.FindObjectsOfType<AnimPilotFloorMarker>();
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null)
                    continue;
                var pilotRoot = marker.gameObject;
                if (GameApi.PilotMovementType == null)
                    continue;
                var serverPilot = pilotRoot.GetComponent(GameApi.PilotMovementType) as Component;
                if (serverPilot == null)
                    continue;
                var terminal = ResolveTerminal(marker.JoystickPseudoRoot, pilotRoot);
                bool sessionActive = terminal != null && IsTerminalSessionActive(terminal);
                TryEnsureTerminalPilotableBinding(terminal, pilotRoot);
                object scheme = GetControlScheme(serverPilot);
                if (!sessionActive && scheme != null)
                    TryClearPilotAssignment(serverPilot);
                if (!sessionActive && scheme == null)
                    ClearPositionLock(serverPilot);
            }
        }

        private static void TryEnsureTerminalPilotableBinding(Component terminal, GameObject pilotRoot)
        {
            if (terminal == null || pilotRoot == null || GameApi.TerminalPilotableField == null
                || GameApi.PilotMovementVanillaType == null)
                return;
            try
            {
                var pilotVanilla = GameApi.GetComponent(pilotRoot, GameApi.PilotMovementVanillaType);
                if (pilotVanilla == null)
                    return;
                var current = GameApi.TerminalPilotableField.GetValue(terminal);
                if (current == pilotVanilla)
                    return;
                GameApi.TerminalPilotableField.SetValue(terminal, pilotVanilla);
            }
            catch
            {
            }
        }

        private static void TryClearPilotAssignment(Component serverPilot)
        {
            if (serverPilot == null || GameApi.PilotAssignPlayerMethod == null)
                return;
            try
            {
                GameApi.PilotAssignPlayerMethod.Invoke(serverPilot, new object[] { null });
            }
            catch
            {
            }
        }

        private static Vector3 SnapPilotRootToNearestGrid(Component serverPilot, Vector3 worldPos)
        {
            if (serverPilot == null || GameApi.PilotGridManagerField == null
                || GameApi.GridNearestPositionMethod == null)
                return worldPos;
            try
            {
                var grid = GameApi.PilotGridManagerField.GetValue(serverPilot);
                if (grid == null)
                    return worldPos;
                return (Vector3)GameApi.GridNearestPositionMethod.Invoke(grid, new object[] { worldPos });
            }
            catch
            {
                return worldPos;
            }
        }

        private static void RememberPositionLock(Component serverPilot)
        {
            if (serverPilot == null || serverPilot.transform == null)
                return;
            var pos = serverPilot.transform.position;
            pos = SnapPilotRootToNearestGrid(serverPilot, pos);
            s_positionLocksByPilotSyncId[serverPilot.GetInstanceID()] = new PositionLockState
            {
                WorldPos = pos,
                PilotMovement = GetPilotMovement(serverPilot),
                SyncRoot = serverPilot.transform
            };
        }

        private static void ClearPositionLock(Component serverPilot)
        {
            if (serverPilot == null)
                return;
            s_positionLocksByPilotSyncId.Remove(serverPilot.GetInstanceID());
        }

        private static void EnforcePositionLock(Component serverPilot, Component pilotMovement)
        {
            if (serverPilot == null || serverPilot.transform == null)
                return;
            PositionLockState state;
            if (!s_positionLocksByPilotSyncId.TryGetValue(serverPilot.GetInstanceID(), out state))
                return;
            if (pilotMovement != null)
                state.PilotMovement = pilotMovement;
            state.SyncRoot = serverPilot.transform;
            EnforcePositionLockState(state);
            s_positionLocksByPilotSyncId[serverPilot.GetInstanceID()] = state;
        }

        private static void EnforcePositionLockState(PositionLockState state)
        {
            if (state.SyncRoot == null)
                return;
            var t = state.SyncRoot;
            if ((t.position - state.WorldPos).sqrMagnitude <= PositionLockEpsilonSq)
                return;
            t.position = state.WorldPos;
            if (state.PilotMovement != null)
                ApplyIdlePilotFreeze(state.PilotMovement);
        }

        private static void ApplyIdlePilotFreeze(Component pilotMovement)
        {
            if (pilotMovement == null)
                return;
            SetPilotVelocity(pilotMovement, Vector3.zero);
            SetPilotKinematic(pilotMovement, true);
            var rb = pilotMovement.GetComponent<Rigidbody>();
            if (rb == null)
                return;
            if (GameApi.RigidbodyMotionType != null && GameApi.SetKinematicMethod != null)
            {
                var motion = rb.GetComponent(GameApi.RigidbodyMotionType);
                if (motion != null)
                {
                    try
                    {
                        GameApi.SetKinematicMethod.Invoke(motion, new object[] { true });
                    }
                    catch
                    {
                    }
                }
            }
            rb.isKinematic = true;
        }

        internal static void ServerUpdatePostfix(object serverPilot)
        {
            var comp = serverPilot as Component;
            if (comp == null || !IsAnimPilotGroup(comp.gameObject))
                return;
            var scheme = GetControlScheme(comp);
            var pilotMove = GetPilotMovement(comp);
            if (!AllowVanillaPilotUpdate(comp.gameObject) || scheme == null)
            {
                if (pilotMove != null)
                    ApplyIdlePilotFreeze(pilotMove);
                if (scheme == null)
                    ClearPositionLock(comp);
                else
                {
                    RememberPositionLock(comp);
                    EnforcePositionLock(comp, pilotMove);
                }
                ReleaseRegionalGrid(comp);
                return;
            }
            if (IsPilotStickIdle(comp, scheme))
            {
                if (pilotMove != null)
                    ApplyIdlePilotFreeze(pilotMove);
                RememberPositionLock(comp);
                EnforcePositionLock(comp, pilotMove);
            }
            if (HasDriveInput(scheme))
                return;
            if (GetGridTarget(comp) != null)
                return;
            if (!IsPilotNearlyStill(comp))
                return;
            DeoccupyRegionalGridOnly(comp);
        }

        internal static void ServerStartPostfix(object serverPilot)
        {
            var comp = serverPilot as Component;
            if (comp == null || !IsAnimPilotGroup(comp.gameObject))
                return;
            ReleaseRegionalGrid(comp);
        }

        internal static void ServerAssignPostfix(object serverPilot, object scheme)
        {
            var comp = serverPilot as Component;
            if (comp == null || !IsAnimPilotGroup(comp.gameObject))
                return;
            if (scheme == null)
            {
                EndChefParent(comp);
                ReleaseRegionalGrid(comp);
                ClearPositionLock(comp);
                var frozenMove = GetPilotMovement(comp);
                if (frozenMove != null)
                    ApplyIdlePilotFreeze(frozenMove);
                return;
            }
            ClearPositionLock(comp);
            var pilotMove = GetPilotMovement(comp);
            if (pilotMove != null)
                SetPilotKinematic(pilotMove, false);
            ReleaseRegionalGrid(comp);
            BeginChefParent(comp, scheme);
        }

        internal static void ClientAssignAvatarPostfix(object clientPilot, GameObject avatar)
        {
            var comp = clientPilot as Component;
            if (comp == null || !IsAnimPilotGroup(comp.gameObject))
                return;
            if (avatar == null)
            {
                EndChefParent(comp);
                return;
            }
            if (!IsLocallyControlled(avatar))
                return;
            var pilotMove = GetPilotMovement(comp);
            if (pilotMove == null || !ShouldParentChef(pilotMove))
                return;
            ParentChef(avatar, pilotMove.transform);
            RememberParent(comp, avatar, true);
        }

        /// <summary>场景自愈：Design/Pilot Objects 组根补 AnimPilotFloorMarker 并回填摇杆伪根。</summary>
        internal static int HealPilotFloorMarkersInScene()
        {
            var root = GameObject.Find("Design/Pilot Objects");
            if (root == null)
                return 0;
            int healed = 0;
            var tr = root.transform;
            for (int i = 0; i < tr.childCount; i++)
            {
                var group = tr.GetChild(i);
                if (GameApi.PilotMovementVanillaType == null
                    || group.gameObject.GetComponent(GameApi.PilotMovementVanillaType) == null)
                    continue;
                var marker = group.GetComponent<AnimPilotFloorMarker>();
                if (marker == null)
                {
                    marker = group.gameObject.AddComponent<AnimPilotFloorMarker>();
                    healed++;
                }
                if (marker.JoystickPseudoRoot == null)
                {
                    var joy = FindJoystickPseudoRootForPilot(group.gameObject);
                    if (joy != null)
                        marker.JoystickPseudoRoot = joy;
                }
            }
            return healed;
        }

        private static GameObject FindJoystickPseudoRootForPilot(GameObject pilotRoot)
        {
            if (pilotRoot == null || GameApi.TerminalType == null
                || GameApi.TerminalPilotableField == null || GameApi.PilotMovementVanillaType == null)
                return null;
            var pilotVanilla = GameApi.GetComponent(pilotRoot, GameApi.PilotMovementVanillaType);
            if (pilotVanilla == null)
                return null;
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return null;
            var roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var sceneRoot = roots[r];
                if (sceneRoot == null)
                    continue;
                var terminals = GameApi.GetComponentsInChildren(sceneRoot, GameApi.TerminalType, true);
                for (int t = 0; t < terminals.Length; t++)
                {
                    var term = terminals[t];
                    if (term == null)
                        continue;
                    object po;
                    try
                    {
                        po = GameApi.TerminalPilotableField.GetValue(term);
                    }
                    catch
                    {
                        continue;
                    }
                    if (po == null || po != pilotVanilla)
                        continue;
                    var termComp = term as Component;
                    if (termComp == null || termComp.transform == null)
                        continue;
                    if (termComp.transform.parent != null)
                        return termComp.transform.parent.gameObject;
                    return termComp.gameObject;
                }
            }
            return null;
        }

        private static void ApplyFrozenTick(Component serverPilot)
        {
            var pilotMove = GetPilotMovement(serverPilot);
            if (pilotMove != null)
                ApplyIdlePilotFreeze(pilotMove);
            RememberPositionLock(serverPilot);
            EnforcePositionLock(serverPilot, pilotMove);
            ReleaseRegionalGrid(serverPilot);
        }

        private static void BeginChefParent(Component serverPilot, object scheme)
        {
            var pilotMove = GetPilotMovement(serverPilot);
            if (pilotMove == null || !ShouldParentChef(pilotMove))
                return;
            var chef = GetChefFromScheme(scheme);
            if (chef == null)
                return;
            ParentChef(chef, pilotMove.transform);
            RememberParent(serverPilot, chef, true);
        }

        private static void EndChefParent(Component serverPilot)
        {
            int id = serverPilot.GetInstanceID();
            ChefParentState state;
            if (!s_parentByPilotId.TryGetValue(id, out state) || !state.Parented || state.Chef == null)
            {
                s_parentByPilotId.Remove(id);
                return;
            }
            UnparentChef(state.Chef);
            s_parentByPilotId.Remove(id);
        }

        private static void RememberParent(Component pilotSync, GameObject chef, bool parented)
        {
            s_parentByPilotId[pilotSync.GetInstanceID()] = new ChefParentState
            {
                Chef = chef,
                Parented = parented
            };
        }

        private static bool ShouldParentChef(Component pilotMovement)
        {
            return pilotMovement != null && IsAnimPilotGroup(pilotMovement.gameObject);
        }

        private static void ParentChef(GameObject chef, Transform pilotTransform)
        {
            if (chef == null || pilotTransform == null)
                return;
            var dlp = chef.GetComponent(GameApi.DynamicLandscapeParentingType) as Behaviour;
            if (dlp != null)
                dlp.enabled = false;
            chef.transform.SetParent(pilotTransform, true);
        }

        private static void UnparentChef(GameObject chef)
        {
            if (chef == null)
                return;
            chef.transform.SetParent(null, true);
            var dlp = chef.GetComponent(GameApi.DynamicLandscapeParentingType) as Behaviour;
            if (dlp != null)
                dlp.enabled = true;
            var groundCast = chef.GetComponent(GameApi.GroundCastType);
            if (groundCast != null)
            {
                if (GameApi.GroundCastClearMethod != null)
                    GameApi.GroundCastClearMethod.Invoke(groundCast, null);
                if (GameApi.GroundCastForceUpdateMethod != null)
                    GameApi.GroundCastForceUpdateMethod.Invoke(groundCast, null);
            }
        }

        private static GameObject GetChefFromScheme(object scheme)
        {
            if (scheme == null || GameApi.ControlSchemeControlsField == null)
                return null;
            var controls = GameApi.ControlSchemeControlsField.GetValue(scheme) as Component;
            return controls != null ? controls.gameObject : null;
        }

        private static object GetControlScheme(Component serverPilot)
        {
            if (GameApi.PilotControlSchemeField == null)
                return null;
            return GameApi.PilotControlSchemeField.GetValue(serverPilot);
        }

        private static bool HasDriveInput(object scheme)
        {
            if (scheme == null)
                return false;
            try
            {
                var moveX = scheme.GetType().GetField("m_moveX");
                var moveY = scheme.GetType().GetField("m_moveY");
                if (moveX == null || moveY == null)
                    return false;
                var lx = moveX.GetValue(scheme);
                var ly = moveY.GetValue(scheme);
                float x = (float)lx.GetType().GetMethod("GetValue").Invoke(lx, null);
                float y = (float)ly.GetType().GetMethod("GetValue").Invoke(ly, null);
                float z = -y;
                return x * x + z * z > 0.04f;
            }
            catch
            {
                return false;
            }
        }

        private static Component GetPilotMovement(Component serverPilot)
        {
            if (GameApi.PilotMovementObjField == null)
                return null;
            return GameApi.PilotMovementObjField.GetValue(serverPilot) as Component;
        }

        private static void SetPilotVelocity(Component pilotMovement, Vector3 v)
        {
            if (pilotMovement == null || GameApi.PilotRigidbodyMotionProperty == null
                || GameApi.RigidbodySetVelocityMethod == null)
                return;
            try
            {
                var motion = GameApi.PilotRigidbodyMotionProperty.GetValue(pilotMovement, null);
                if (motion != null)
                    GameApi.RigidbodySetVelocityMethod.Invoke(motion, new object[] { v });
            }
            catch
            {
            }
        }

        private static void SetPilotKinematic(Component pilotMovement, bool kinematic)
        {
            if (pilotMovement == null || GameApi.PilotRigidbodyMotionProperty == null
                || GameApi.SetKinematicMethod == null)
                return;
            try
            {
                var motion = GameApi.PilotRigidbodyMotionProperty.GetValue(pilotMovement, null);
                if (motion != null)
                    GameApi.SetKinematicMethod.Invoke(motion, new object[] { kinematic });
            }
            catch
            {
            }
        }

        private static object GetGridTarget(Component serverPilot)
        {
            if (serverPilot == null || GameApi.PilotGridTargetField == null)
                return null;
            try
            {
                return GameApi.PilotGridTargetField.GetValue(serverPilot);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsPilotNearlyStill(Component serverPilot)
        {
            var pilotMove = GetPilotMovement(serverPilot);
            if (pilotMove == null)
                return true;
            try
            {
                if (GameApi.PilotRigidbodyMotionProperty == null)
                    return true;
                var motion = GameApi.PilotRigidbodyMotionProperty.GetValue(pilotMove, null);
                if (motion == null)
                    return true;
                var velProp = motion.GetType().GetProperty("Velocity");
                if (velProp == null)
                    return true;
                var vel = (Vector3)velProp.GetValue(motion, null);
                return vel.sqrMagnitude <= SettleVelocitySq;
            }
            catch
            {
                return true;
            }
        }

        private static void ClearGridTarget(Component serverPilot)
        {
            if (GameApi.PilotGridTargetField == null)
                return;
            try
            {
                GameApi.PilotGridTargetField.SetValue(serverPilot, null);
            }
            catch
            {
            }
        }

        private static void DeoccupyRegionalGridOnly(Component serverPilot)
        {
            if (serverPilot == null || GameApi.GridDeoccupyMethod == null
                || GameApi.PilotGridManagerField == null || GameApi.PilotMinField == null
                || GameApi.PilotMaxField == null)
                return;
            try
            {
                var grid = GameApi.PilotGridManagerField.GetValue(serverPilot);
                if (grid == null)
                    return;
                var min = GameApi.PilotMinField.GetValue(serverPilot);
                var max = GameApi.PilotMaxField.GetValue(serverPilot);
                GameApi.GridDeoccupyMethod.Invoke(grid, new[] { min, max });
            }
            catch
            {
            }
        }

        internal static void ReleaseRegionalGrid(Component serverPilot)
        {
            if (serverPilot == null)
                return;
            ClearGridTarget(serverPilot);
            DeoccupyRegionalGridOnly(serverPilot);
        }

        private static bool IsLocallyControlled(GameObject go)
        {
            if (go == null || GameApi.PlayerIDProviderProperty == null
                || GameApi.IsLocallyControlledMethod == null)
                return false;
            try
            {
                var pc = go.GetComponent(GameApi.PlayerControlsType);
                if (pc == null)
                    return false;
                var provider = GameApi.PlayerIDProviderProperty.GetValue(pc, null);
                if (provider == null)
                    return false;
                return (bool)GameApi.IsLocallyControlledMethod.Invoke(provider, null);
            }
            catch
            {
                return false;
            }
        }
    }
}
