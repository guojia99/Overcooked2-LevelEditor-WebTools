using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// 标记可移动火锅载具（由 LayoutRuntimePushablePot.ResetChild 挂到 childGameObject）。
    /// LayoutRuntimePushableVoidFall 只处理带此标记的对象，避免误伤其他 PushableObject。
    /// </summary>
    public class LayoutPushableVoidFallTarget : MonoBehaviour
    {
    }

    /// <summary>
    /// 可移动火锅在 walkable 空洞/水面上方时的坠落与重生补丁。
    ///
    /// 核心原则（对齐官方 ServerUtensilRespawnBehaviour 的重生方式）：
    /// 游戏运行期间【绝不】Destroy / 重建载具或锅模型。
    /// 载具（pushable_object）和锅（utensil_large_pot_01）在关卡加载扫描
    /// （EntitySerialisationRegistry.LinkAllEntitiesToSynchronisationScripts）时被注册为
    /// 网络实体并获得 ServerInteractable / ServerPushableObject / ServerPilotMovement /
    /// ServerCookableContainer 等组件——这些组件只在那一次扫描里 AddComponent，
    /// bundle prefab 本身没有。运行时 Instantiate 重建出来的对象没有实体注册、没有
    /// 同步组件，会变成不可交互、不可同步的「空气锅」（前几轮修复失败的根因）。
    ///
    /// 拖动中玩家落水 → 只结束抓取会话（锅原地松手），锅不跟随坠落；
    /// 锅自身是否坠落由未拖动状态下的 5 点 footprint 支撑检测独立判定。
    ///
    /// 因此：坠落结束用 SetActive(false) 隐藏载具（实体与组件原封不动），
    /// 5 秒后把载具设回开局世界姿态（拖动移动的是载具的 Rigidbody，wrapper 不动，
    /// 故必须归位载具本身）再 SetActive(true)，并恢复 collider / pilot / 网格占用。
    /// </summary>
    public static class LayoutRuntimePushableVoidFall
    {
        private const float RayStartY = 2.5f;
        private const float RayDistance = 5f;
        private const float FootprintHalf = 0.55f;
        private const float FallSpeed = 8f;
        private const float RespawnFallDepth = 2.5f;
        private const float RespawnDelay = 5f;
        private const float RespawnGraceSeconds = 2f;
        private const int VoidConfirmTicks = 4;
        private const int AttachedVoidConfirmTicks = 2;

        private static int s_groundMask;
        private static bool s_groundMaskReady;
        private static Collider[] s_killPlaneColliders;
        private static bool s_killPlanesReady;

        private static FieldInfo s_pilotGridTargetField;
        private static FieldInfo s_pilotGridManagerField;
        private static FieldInfo s_pilotMinField;
        private static FieldInfo s_pilotMaxField;
        private static FieldInfo s_pilotExtentsField;
        private static FieldInfo s_pilotColliderField;
        private static FieldInfo s_sessionField;
        private static FieldInfo s_playerGroundCastField;
        private static FieldInfo s_playerApplyGravityField;
        private static MethodInfo s_sessionOnEndedMethod;
        private static MethodInfo s_serverSessionOnEndedMethod;

        private static readonly Dictionary<Transform, PotTrackState> s_states =
            new Dictionary<Transform, PotTrackState>();

        private static VoidFallTicker s_ticker;

        private class PotTrackState
        {
            public bool SpawnRecorded;
            /// <summary>载具（carrier）开局世界姿态——重生目标。拖动时物理移动的是
            /// 载具的 Rigidbody（ServerPilotMovement → RigidbodyMotion），wrapper 从不移动，
            /// 所以必须记录/恢复载具自己的姿态，否则锅会重生在「落水点」（局部偏移残留）。</summary>
            public Vector3 SpawnPosition;
            public Quaternion SpawnRotation;
            /// <summary>wrapper 开局世界姿态（防御性恢复用）。</summary>
            public Vector3 WrapperPosition;
            public Quaternion WrapperRotation;
            public bool IsFalling;
            public bool HiddenForRespawn;
            public float RespawnAt = -1f;
            public int VoidStreak;
            public int PlayerVoidStreak;
            public float RespawnGraceUntil;
            public PushableObject Pushable;
            public ServerPilotMovement Pilot;
            public Rigidbody Rigidbody;
            public GameObject Carrier;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (s_ticker != null)
                return;
            var go = new GameObject("LayoutRuntimePushableVoidFall");
            Object.DontDestroyOnLoad(go);
            s_ticker = go.AddComponent<VoidFallTicker>();
        }

        private class VoidFallTicker : MonoBehaviour
        {
            private void Update()
            {
                if (Time.frameCount % 2 != 0)
                    return;
                try
                {
                    LayoutRuntimePushableVoidFall.Tick();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[LayoutRuntimePushableVoidFall] tick skipped: " + ex.Message);
                }
            }
        }

        private static void Tick()
        {
            EnsureGroundMask();
            if (s_groundMask == 0)
                return;

            PruneStates();
            TickPendingRespawns();

            foreach (var target in Object.FindObjectsOfType<LayoutPushableVoidFallTarget>())
            {
                if (target == null)
                    continue;

                var pushable = target.GetComponent<PushableObject>();
                if (pushable == null)
                    pushable = target.GetComponentInParent<PushableObject>();
                if (pushable == null)
                    continue;

                var wrapper = target.transform.parent;
                if (wrapper == null)
                    wrapper = target.transform;

                var pilot = target.GetComponent<ServerPilotMovement>();
                if (pilot == null)
                    pilot = target.GetComponentInChildren<ServerPilotMovement>();
                var rb = target.GetComponent<Rigidbody>();
                if (rb == null)
                    rb = target.GetComponentInChildren<Rigidbody>();
                var col = target.GetComponent<Collider>();
                if (col == null)
                    col = target.GetComponentInChildren<Collider>();

                PotTrackState state = GetState(wrapper);
                CachePotRefs(state, pushable, pilot, rb, target.gameObject);
                RecordSpawnPose(state, wrapper, target.transform);
                if (state.HiddenForRespawn)
                    continue;

                if (Time.time < state.RespawnGraceUntil)
                    continue;

                var sample = col != null ? col.bounds.center : target.transform.position;
                if (state.IsFalling)
                {
                    ApplyControlledFall(wrapper, state);
                    if (ShouldSubmerge(state, GetFallTransform(wrapper, state).position))
                        BeginSubmerge(wrapper, pushable, state);
                    continue;
                }

                bool attached = IsAnyoneAttached(pushable);
                if (attached)
                {
                    // 拖动中的玩家脚下悬空（正往水里掉）：只负责「松手」，锅绝不跟着坠。
                    // 锅自身是否坠落，由松手后下一 tick 起走未拖动分支的
                    // footprint 检测独立判定（大部分在岸上就不落水）。
                    if (IsDraggingPlayerOverVoid(pushable))
                        state.PlayerVoidStreak++;
                    else
                        state.PlayerVoidStreak = 0;
                    state.VoidStreak = 0;
                    if (state.PlayerVoidStreak < AttachedVoidConfirmTicks)
                        continue;
                    state.PlayerVoidStreak = 0;
                    EndPushableSession(pushable);
                    Debug.Log("[VoidFall] dragging player over void, pot released in place: " + wrapper.name);
                    continue;
                }
                if (IsFootprintOverVoid(sample.x, sample.z))
                    state.VoidStreak++;
                else
                    state.VoidStreak = 0;
                state.PlayerVoidStreak = 0;
                if (state.VoidStreak < VoidConfirmTicks)
                    continue;

                BeginPotFall(wrapper, pushable, state);
            }
        }

        private static void RecordSpawnPose(PotTrackState state, Transform wrapper, Transform carrier)
        {
            if (state == null || state.SpawnRecorded || wrapper == null)
                return;
            state.WrapperPosition = wrapper.position;
            state.WrapperRotation = wrapper.rotation;
            if (carrier != null)
            {
                // 拖动移动的是载具（Rigidbody），重生以载具的开局世界姿态为准
                state.SpawnPosition = carrier.position;
                state.SpawnRotation = carrier.rotation;
            }
            else
            {
                state.SpawnPosition = wrapper.position;
                state.SpawnRotation = wrapper.rotation;
            }
            state.SpawnRecorded = true;
        }

        /// <summary>坠落/沉没判定使用的变换：优先载具（它才是被物理移动的对象）。</summary>
        private static Transform GetFallTransform(Transform wrapper, PotTrackState state)
        {
            if (state != null && state.Carrier != null)
                return state.Carrier.transform;
            return wrapper;
        }

        private static PotTrackState GetState(Transform wrapper)
        {
            PotTrackState state;
            if (!s_states.TryGetValue(wrapper, out state))
            {
                state = new PotTrackState();
                s_states[wrapper] = state;
            }
            return state;
        }

        private static void PruneStates()
        {
            if (s_states.Count == 0)
                return;
            var dead = new List<Transform>();
            foreach (var pair in s_states)
            {
                if (pair.Key == null)
                    dead.Add(pair.Key);
            }
            for (int i = 0; i < dead.Count; i++)
                s_states.Remove(dead[i]);
        }

        private static void TickPendingRespawns()
        {
            if (s_states.Count == 0)
                return;

            var ready = new List<Transform>();
            foreach (var pair in s_states)
            {
                var wrapper = pair.Key;
                var state = pair.Value;
                if (wrapper == null || state == null || !state.HiddenForRespawn)
                    continue;
                if (state.RespawnAt > 0f && Time.time >= state.RespawnAt)
                    ready.Add(wrapper);
            }

            for (int i = 0; i < ready.Count; i++)
            {
                var wrapper = ready[i];
                if (wrapper == null)
                    continue;
                PotTrackState state;
                if (!s_states.TryGetValue(wrapper, out state) || state == null)
                    continue;
                RespawnPot(wrapper, state);
            }
        }

        private static void CachePotRefs(
            PotTrackState state,
            PushableObject pushable,
            ServerPilotMovement pilot,
            Rigidbody rb,
            GameObject carrier)
        {
            if (state == null)
                return;
            if (pushable != null)
                state.Pushable = pushable;
            if (pilot != null)
                state.Pilot = pilot;
            if (rb != null)
                state.Rigidbody = rb;
            if (carrier != null)
                state.Carrier = carrier;
        }

        private static bool IsFootprintOverVoid(float cx, float cz)
        {
            if (!HasGroundSupport(cx, cz))
                return true;
            if (!HasGroundSupport(cx - FootprintHalf, cz - FootprintHalf))
                return true;
            if (!HasGroundSupport(cx - FootprintHalf, cz + FootprintHalf))
                return true;
            if (!HasGroundSupport(cx + FootprintHalf, cz - FootprintHalf))
                return true;
            if (!HasGroundSupport(cx + FootprintHalf, cz + FootprintHalf))
                return true;
            return false;
        }

        private static void EnsureGroundMask()
        {
            if (s_groundMaskReady)
                return;
            s_groundMaskReady = true;
            int layer = LayerMask.NameToLayer("Ground");
            s_groundMask = layer >= 0 ? (1 << layer) : (1 << 9);
        }

        private static void EnsureKillPlanes()
        {
            if (s_killPlanesReady)
                return;
            s_killPlanesReady = true;
            var planes = Object.FindObjectsOfType<RespawnCollider>();
            if (planes == null || planes.Length == 0)
            {
                s_killPlaneColliders = new Collider[0];
                return;
            }
            var cols = new List<Collider>();
            for (int i = 0; i < planes.Length; i++)
            {
                if (planes[i] == null)
                    continue;
                var col = planes[i].GetComponent<Collider>();
                if (col != null)
                    cols.Add(col);
            }
            s_killPlaneColliders = cols.ToArray();
        }

        private static bool HasGroundSupport(float x, float z)
        {
            return Physics.Raycast(
                new Vector3(x, RayStartY, z),
                Vector3.down,
                RayDistance,
                s_groundMask,
                QueryTriggerInteraction.Ignore);
        }

        private static bool ShouldSubmerge(PotTrackState state, Vector3 pos)
        {
            if (state == null || !state.SpawnRecorded)
                return false;
            if (pos.y < state.SpawnPosition.y - RespawnFallDepth)
                return true;
            EnsureKillPlanes();
            if (s_killPlaneColliders == null)
                return false;
            for (int i = 0; i < s_killPlaneColliders.Length; i++)
            {
                var col = s_killPlaneColliders[i];
                if (col == null)
                    continue;
                var b = col.bounds;
                if (pos.y > b.max.y + 0.25f)
                    continue;
                if (pos.x < b.min.x || pos.x > b.max.x || pos.z < b.min.z || pos.z > b.max.z)
                    continue;
                return true;
            }
            return false;
        }

        private static bool IsDraggingPlayerOverVoid(PushableObject pushable)
        {
            if (pushable == null)
                return false;
            foreach (var pc in Object.FindObjectsOfType<PlayerControls>())
            {
                if (pc == null || !pushable.IsAttached(pc.transform))
                    continue;
                var pos = pc.transform.position;
                if (!HasGroundSupport(pos.x, pos.z))
                    return true;
            }
            return false;
        }

        private static bool IsAnyoneAttached(PushableObject pushable)
        {
            if (pushable == null)
                return false;
            if (pushable.m_UseAttachPoints && pushable.m_AttachPoints != null)
            {
                for (int i = 0; i < pushable.m_AttachPoints.Length; i++)
                {
                    var ap = pushable.m_AttachPoints[i];
                    if (ap == null)
                        continue;
                    var parentable = ap.RequestInterface<IParentable>();
                    if (parentable == null)
                        continue;
                    var attachPoint = parentable.GetAttachPoint(ap.gameObject);
                    if (attachPoint != null && attachPoint.childCount > 0)
                        return true;
                }
                return false;
            }
            if (pushable.m_CentrePoint != null)
                return pushable.m_CentrePoint.transform.childCount > 0;
            return false;
        }

        /// <summary>
        /// 可移动火锅由本补丁负责坠落/重生，始终跳过宿主 KillPlane，
        /// 避免宿主对锅走 DestroyEntity（会把实体连同模型永久销毁）。
        /// </summary>
        public static bool ShouldIgnoreKillPlane(GameObject gameObject)
        {
            if (gameObject == null)
                return true;
            if (gameObject.GetComponentInParent<LayoutPushableVoidFallTarget>() != null)
                return true;
            return gameObject.GetComponentInParent<LayoutRuntimePushablePot>() != null;
        }

        /// <summary>
        /// 玩家触 KillPlane 重生前强制脱离火锅，避免仍挂在锅上：
        /// a) 玩家成为即将 SetActive(false) 载具的子物体（整条重生协程随父物体一起失效，卡死）；
        /// b) ClientChefSynchroniser 的 parent 状态与服务器不一致。
        /// </summary>
        public static void DetachPlayerFromVoidFallPots(GameObject playerObject)
        {
            if (playerObject == null)
                return;
            var playerControls = playerObject.GetComponent<PlayerControls>();
            if (playerControls == null)
                return;

            foreach (var target in Object.FindObjectsOfType<LayoutPushableVoidFallTarget>())
            {
                if (target == null)
                    continue;
                var pushable = target.GetComponent<PushableObject>();
                if (pushable == null || !pushable.IsAttached(playerControls.transform))
                    continue;
                EndPushableSession(pushable);
                // 兜底：session 已不存在但玩家 parent 仍挂在 attach point 上
                var parent = playerControls.transform.parent;
                if (parent != null && parent.IsChildOf(pushable.transform))
                {
                    playerControls.transform.SetParent(null, true);
                    RestoreDetachedPlayer(playerControls);
                }
            }
        }

        private static void BeginPotFall(Transform wrapper, PushableObject pushable, PotTrackState state)
        {
            if (wrapper == null || state == null)
                return;

            state.IsFalling = true;
            state.HiddenForRespawn = false;
            state.RespawnAt = -1f;

            if (pushable != null && IsAnyoneAttached(pushable))
                EndPushableSession(pushable);

            SetCollidersEnabled(wrapper, false, null);

            if (state.Pilot != null)
            {
                ReleasePilotGrid(state.Pilot);
                ClearPilotGridTarget(state.Pilot);
                state.Pilot.enabled = false;
            }

            if (state.Rigidbody != null)
            {
                var motion = state.Rigidbody.GetComponent<RigidbodyMotion>();
                if (motion != null)
                    motion.SetKinematic(true);
                else
                    state.Rigidbody.isKinematic = true;
                state.Rigidbody.useGravity = false;
                state.Rigidbody.velocity = Vector3.zero;
                state.Rigidbody.angularVelocity = Vector3.zero;
            }

            Debug.Log("[VoidFall] pot falling: " + wrapper.name);
        }

        private static void ApplyControlledFall(Transform wrapper, PotTrackState state)
        {
            if (state == null)
                return;

            if (state.Pilot != null && state.Pilot.enabled)
            {
                ClearPilotGridTarget(state.Pilot);
                state.Pilot.enabled = false;
            }

            if (state.Rigidbody != null)
            {
                var motion = state.Rigidbody.GetComponent<RigidbodyMotion>();
                if (motion != null)
                    motion.SetKinematic(true);
                else if (!state.Rigidbody.isKinematic)
                    state.Rigidbody.isKinematic = true;
                state.Rigidbody.useGravity = false;
                state.Rigidbody.velocity = Vector3.zero;
                state.Rigidbody.angularVelocity = Vector3.zero;
            }

            // 坠落直接移动载具（物理与视觉的真正主体）；wrapper 保持不动，
            // 载具的局部偏移在 RespawnPot 里被显式归位。
            var fallTransform = GetFallTransform(wrapper, state);
            if (fallTransform != null)
            {
                var pos = fallTransform.position;
                if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z))
                {
                    if (state.SpawnRecorded)
                        fallTransform.position = state.SpawnPosition;
                    state.IsFalling = false;
                    return;
                }
                fallTransform.position = pos + Vector3.down * (FallSpeed * Time.deltaTime);
            }
        }

        private static void BeginSubmerge(Transform wrapper, PushableObject pushable, PotTrackState state)
        {
            if (wrapper == null || state == null)
                return;

            EndPushableSession(pushable);
            ForceUnparentPlayers(wrapper);
            DisposePotContents(wrapper);

            state.IsFalling = false;
            state.HiddenForRespawn = true;
            state.RespawnAt = Time.time + RespawnDelay;
            state.VoidStreak = 0;
            state.PlayerVoidStreak = 0;

            // 官方道具重生同款隐藏方式：SetActive(false)。
            // 实体注册、Server*/Client* 同步组件、网格占用全部原样保留在对象上，
            // 重生时 SetActive(true) 即恢复全部功能。Renderer.enabled / Collider.enabled
            // 逐个开关会留下半激活状态的实体（Trigger 列表残留、容器 trigger 失效等），
            // 且子物体被单独禁用的状态不会随 SetActive 恢复，是前一版「空气锅」的帮凶之一。
            var carrier = state.Carrier != null ? state.Carrier : FindCarrierFallback(wrapper);
            if (carrier != null && carrier.activeSelf)
                carrier.SetActive(false);

            Debug.Log("[VoidFall] pot submerged, respawn in " + RespawnDelay + "s: " + wrapper.name);
        }

        /// <summary>
        /// 原位重生：不 Destroy、不 Instantiate、不 ResetChild。
        /// 载具设回开局世界姿态并 SetActive(true)；wrapper 防御性归位；
        /// 恢复 collider / pilot（含网格重占用）与 Rigidbody 静置状态。
        /// </summary>
        private static void RespawnPot(Transform wrapper, PotTrackState state)
        {
            if (wrapper == null || state == null || !state.SpawnRecorded)
                return;

            try
            {
                EndPushableSession(state.Pushable);
                ForceUnparentPlayers(wrapper);

                // 归位分两步：
                // 1) wrapper 拉回开局姿态（防御性——正常对局中 wrapper 不会被移动）；
                // 2) 载具直接设回开局「世界」姿态。关键在第 2 步：玩家拖锅时物理移动的
                //    是载具的 Rigidbody，载具相对 wrapper 留下局部偏移；只归位 wrapper
                //    的话锅会带着偏移重生在落水点，而不是开局放置点。
                wrapper.SetPositionAndRotation(state.WrapperPosition, state.WrapperRotation);

                var carrier = state.Carrier != null ? state.Carrier : FindCarrierFallback(wrapper);
                if (carrier != null)
                {
                    carrier.transform.SetPositionAndRotation(state.SpawnPosition, state.SpawnRotation);
                    if (!carrier.activeSelf)
                        carrier.SetActive(true);
                }

                // collider 恢复：BeginPotFall 全部禁用了；这里全部重开，
                // 但 m_fakePlayerCollider 属于会话期专用碰撞体，静置状态必须保持关闭。
                SetCollidersEnabled(wrapper, true, state.Pushable);

                if (state.Pilot != null)
                {
                    ClearPilotGridTarget(state.Pilot);
                    OccupyPilotGridNear(state.Pilot);
                    state.Pilot.enabled = true;
                }

                if (state.Rigidbody != null)
                {
                    state.Rigidbody.velocity = Vector3.zero;
                    state.Rigidbody.angularVelocity = Vector3.zero;
                    var motion = state.Rigidbody.GetComponent<RigidbodyMotion>();
                    if (motion != null)
                        motion.SetKinematic(true);
                    else
                        state.Rigidbody.isKinematic = true;
                    state.Rigidbody.useGravity = false;
                    // ServerPilotMovement 恢复 tick 后会自行 SetKinematic(false) 并接管运动
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[VoidFall] pot respawn failed (" + wrapper.name + "): " + ex);
            }
            finally
            {
                // 无论成败都必须清标志：否则 RespawnAt 已过期会每 2 帧重试一次，
                // 把单次错误放大成刷屏 + 永远不可见的锅。
                state.IsFalling = false;
                state.HiddenForRespawn = false;
                state.RespawnAt = -1f;
                state.VoidStreak = 0;
                state.PlayerVoidStreak = 0;
                state.RespawnGraceUntil = Time.time + RespawnGraceSeconds;
            }

            Debug.Log("[VoidFall] pot respawned at spawn point: " + wrapper.name);
        }

        private static GameObject FindCarrierFallback(Transform wrapper)
        {
            if (wrapper == null || wrapper.childCount == 0)
                return null;
            return wrapper.GetChild(0).gameObject;
        }

        /// <summary>
        /// 任何仍挂在 wrapper 之下（attach point）的玩家强制脱离父级。
        /// 载具即将 SetActive(false)：玩家若仍是其子物体，会随父物体一起失活，
        /// 重生协程、输入、渲染全部停摆——这正是「双人双双落水卡死」的形态。
        /// </summary>
        private static void ForceUnparentPlayers(Transform wrapper)
        {
            if (wrapper == null)
                return;
            foreach (var pc in Object.FindObjectsOfType<PlayerControls>())
            {
                if (pc == null)
                    continue;
                var parent = pc.transform.parent;
                if (parent == null || !parent.IsChildOf(wrapper))
                    continue;
                pc.transform.SetParent(null, true);
                RestoreDetachedPlayer(pc);
            }
        }

        /// <summary>
        /// 结束 PushableObject 的交互会话（host/单机生效；纯客户端没有
        /// ServerPushableObject，其会话由服务器下发的 SessionInteractableMessage 结束）。
        ///
        /// 关键：只有 m_session 非空才走结束流程并广播结束消息。
        /// 之前的版本无条件反射调用 OnSessionEnded，一次坠落周期会发出 2~3 份
        /// interacterID=0 的结束消息；第二份到达客户端时 ClientSessionInteractable.
        /// OnSessionEnded 里 m_session 已是 null → NRE 打断客户端消息分发，且
        /// SetInteractionSuppressed(false) 不再执行，客户端从此无法再抓取。
        /// </summary>
        private static void EndPushableSession(PushableObject pushable)
        {
            if (pushable == null)
                return;

            var serverPushable = pushable.GetComponent<ServerPushableObject>();
            if (serverPushable == null)
                return;

            EnsureField(ref s_sessionField, typeof(ServerSessionInteractable), "m_session");
            if (s_sessionField == null)
                return;

            object session;
            try
            {
                session = s_sessionField.GetValue(serverPushable);
            }
            catch
            {
                return;
            }

            if (session == null)
                return;

            DetachAllPlayersFromPushable(pushable);

            EnsureMethod(ref s_sessionOnEndedMethod, session.GetType(), "OnSessionEnded");
            if (s_sessionOnEndedMethod != null)
            {
                try
                {
                    s_sessionOnEndedMethod.Invoke(session, null);
                }
                catch
                {
                }
            }

            EnsureMethod(ref s_serverSessionOnEndedMethod, typeof(ServerSessionInteractable), "OnSessionEnded");
            if (s_serverSessionOnEndedMethod != null)
            {
                try
                {
                    // 宿主原版流程：SynchroniseInteractionState(null) 广播结束消息
                    // （客户端 ClientPushableObject 会话同步收尾）→ 解除交互抑制 → 清 m_session
                    s_serverSessionOnEndedMethod.Invoke(serverPushable, null);
                }
                catch
                {
                }
            }
        }

        private static void DetachAllPlayersFromPushable(PushableObject pushable)
        {
            if (pushable == null)
                return;

            var pilot = pushable.GetComponent<ServerPilotMovement>();
            if (pilot != null)
            {
                try
                {
                    pilot.AssignPlayer(null);
                }
                catch
                {
                }
            }

            foreach (var pc in Object.FindObjectsOfType<PlayerControls>())
            {
                if (pc == null || !pushable.IsAttached(pc.transform))
                    continue;
                pc.transform.SetParent(null, true);
                RestoreDetachedPlayer(pc);
            }

            if (pushable.m_fakePlayerCollider != null)
                pushable.m_fakePlayerCollider.enabled = false;
        }

        /// <summary>会话结束后的玩家状态恢复（与 ServerPushableObject.UserSession.OnSessionEnded 等价）。
        /// 只处理本机控制的玩家：远程玩家的 chef 克隆由 ClientChefSynchroniser 保持
        /// kinematic 并通过网络同步，强行恢复物理会让客户端远程玩家失控。</summary>
        private static void RestoreDetachedPlayer(PlayerControls pc)
        {
            if (pc == null)
                return;

            var idProvider = pc.PlayerIDProvider;
            if (idProvider != null && !idProvider.IsLocallyControlled())
                return;

            pc.enabled = true;

            var rb = pc.GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = false;

            var dynamicLandscapeParenting = pc.gameObject.RequestComponent<DynamicLandscapeParenting>();
            if (dynamicLandscapeParenting != null)
                dynamicLandscapeParenting.enabled = true;

            EnsureField(ref s_playerGroundCastField, typeof(PlayerControls), "m_groundCast");
            var groundCast = s_playerGroundCastField != null
                ? s_playerGroundCastField.GetValue(pc) as GroundCast
                : null;
            if (groundCast != null)
            {
                try
                {
                    groundCast.ClearGround();
                    groundCast.ForceUpdateNow();
                }
                catch
                {
                }
            }

            EnsureField(ref s_playerApplyGravityField, typeof(PlayerControls), "m_bApplyGravity");
            if (s_playerApplyGravityField != null)
            {
                try
                {
                    s_playerApplyGravityField.SetValue(pc, true);
                }
                catch
                {
                }
            }
        }

        private static void DisposePotContents(Transform wrapper)
        {
            if (wrapper == null)
                return;
            var disposals = wrapper.GetComponentsInChildren<ServerContentsDisposalBehaviour>(true);
            for (int i = 0; i < disposals.Length; i++)
            {
                if (disposals[i] == null)
                    continue;
                try
                {
                    disposals[i].AddToDisposer(null);
                }
                catch
                {
                }
            }
        }

        private static void ClearPilotGridTarget(ServerPilotMovement pilot)
        {
            if (pilot == null)
                return;
            EnsureField(ref s_pilotGridTargetField, typeof(ServerPilotMovement), "m_gridTarget");
            if (s_pilotGridTargetField == null)
                return;
            try
            {
                s_pilotGridTargetField.SetValue(pilot, null);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 释放载具在 GridManager 占用的格子（ServerPilotMovement 的 m_min/m_max）。
        /// 载具传送/隐藏后 pilot 不会再移动它，旧格子若不释放会永远占用坠落点附近
        /// 的可走格子（GridManager.TryOccupyGridRegion 对包括自己在内的任何占用者都失败）。
        /// </summary>
        private static void ReleasePilotGrid(ServerPilotMovement pilot)
        {
            if (pilot == null)
                return;
            EnsureField(ref s_pilotGridManagerField, typeof(ServerPilotMovement), "m_gridManager");
            EnsureField(ref s_pilotMinField, typeof(ServerPilotMovement), "m_min");
            EnsureField(ref s_pilotMaxField, typeof(ServerPilotMovement), "m_max");
            if (s_pilotGridManagerField == null || s_pilotMinField == null || s_pilotMaxField == null)
                return;
            try
            {
                var grid = s_pilotGridManagerField.GetValue(pilot) as GridManager;
                if (grid == null)
                    return;
                GridIndex min = (GridIndex)s_pilotMinField.GetValue(pilot);
                GridIndex max = (GridIndex)s_pilotMaxField.GetValue(pilot);
                grid.DeoccupyGridRegion(min, max);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 重生点重新占用网格（复刻 ServerPilotMovement.StartSynchronising 的初始占用）。
        /// 失败（格子被占）时静默跳过——载具仍由物理碰撞阻挡，首次被拖动时会自行修复占用。
        /// </summary>
        private static void OccupyPilotGridNear(ServerPilotMovement pilot)
        {
            if (pilot == null)
                return;
            EnsureField(ref s_pilotGridManagerField, typeof(ServerPilotMovement), "m_gridManager");
            EnsureField(ref s_pilotMinField, typeof(ServerPilotMovement), "m_min");
            EnsureField(ref s_pilotMaxField, typeof(ServerPilotMovement), "m_max");
            EnsureField(ref s_pilotExtentsField, typeof(ServerPilotMovement), "m_extents");
            EnsureField(ref s_pilotColliderField, typeof(ServerPilotMovement), "m_collider");
            if (s_pilotGridManagerField == null || s_pilotMinField == null ||
                s_pilotMaxField == null || s_pilotExtentsField == null || s_pilotColliderField == null)
                return;
            try
            {
                var grid = s_pilotGridManagerField.GetValue(pilot) as GridManager;
                var col = s_pilotColliderField.GetValue(pilot) as Collider;
                if (grid == null || col == null)
                    return;
                Vector3 extents = (Vector3)s_pilotExtentsField.GetValue(pilot);
                Vector3 center = col.bounds.center;
                GridIndex min = grid.GetGridLocationFromPos(center - extents);
                GridIndex max = grid.GetGridLocationFromPos(center + extents);
                if (grid.TryOccupyGridRegion(min, max, pilot.gameObject))
                {
                    s_pilotMinField.SetValue(pilot, min);
                    s_pilotMaxField.SetValue(pilot, max);
                }
            }
            catch
            {
            }
        }

        private static void SetCollidersEnabled(Transform root, bool enabled, PushableObject pushable)
        {
            if (root == null)
                return;
            var fakeCollider = pushable != null ? pushable.m_fakePlayerCollider : null;
            var cols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null)
                    continue;
                if (enabled && cols[i] == fakeCollider)
                {
                    // fakePlayerCollider 只在拖动会话期间启用（宿主约定），静置时必须关闭
                    cols[i].enabled = false;
                    continue;
                }
                cols[i].enabled = enabled;
            }
        }

        private static void EnsureField(ref FieldInfo field, System.Type type, string name)
        {
            if (field != null)
                return;
            field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private static void EnsureMethod(ref MethodInfo method, System.Type type, string name)
        {
            if (method != null)
                return;
            method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
