using System.Collections.Generic;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 可移动火锅在 walkable 空洞/水面上方时的坠落与重生（CustomStub 版，接替原
    /// LayoutRuntimePushableVoidFall）。
    ///
    /// 核心原则（对齐官方 ServerUtensilRespawnBehaviour 的重生方式）：
    /// 游戏运行期间【绝不】Destroy / 重建载具或锅模型——载具与锅在关卡加载扫描
    /// （EntitySerialisationRegistry.LinkAllEntitiesToSynchronisationScripts）时被注册
    /// 为网络实体并获得同步组件，运行时重建出来的对象没有实体注册（「空气锅」根因）。
    ///
    /// 拖动中玩家落水 → 只结束抓取会话（锅原地松手）；锅自身是否坠落由未拖动状态下
    /// 的 5 点 footprint 支撑检测独立判定。坠落结束用 SetActive(false) 隐藏载具，
    /// 5 秒后归位再激活，并恢复 collider / pilot / 网格占用。
    ///
    /// KillPlane 跳过与玩家脱离两个钩子由 HarmonyPatches（ServerRespawnCollider.
    /// ObjectAdded prefix）调用，接替原 ServerRespawnCollider 补丁。
    /// Ticker 由 EntryPoint.Install 创建。
    /// </summary>
    internal static class PushableVoidFall
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
        private static bool s_attachChainChecked;
        private static Collider[] s_killPlaneColliders;
        private static bool s_killPlanesReady;

        private static readonly Dictionary<Transform, PotTrackState> s_states =
            new Dictionary<Transform, PotTrackState>();

        private class PotTrackState
        {
            public bool SpawnRecorded;
            public Vector3 SpawnPosition;
            public Quaternion SpawnRotation;
            public Vector3 WrapperPosition;
            public Quaternion WrapperRotation;
            public bool IsFalling;
            public bool HiddenForRespawn;
            public float RespawnAt = -1f;
            public int VoidStreak;
            public int PlayerVoidStreak;
            public float RespawnGraceUntil;
            public Component Pushable;
            public Component Pilot;
            public Rigidbody Rigidbody;
            public GameObject Carrier;
        }

        internal static void Tick()
        {
            EnsureGroundMask();
            if (s_groundMask == 0)
                return;

            if (!s_attachChainChecked)
            {
                s_attachChainChecked = true;
                StubLog.Log("[VoidFall] attach 反射自检: UseAttachPoints=" + (GameApi.PushableUseAttachPointsField != null)
                    + " AttachPoints=" + (GameApi.PushableAttachPointsField != null)
                    + " IParentable=" + (GameApi.ParentableInterfaceType != null)
                    + " GetAttachPoint=" + (GameApi.GetAttachPointMethod != null)
                    + " IsAttached=" + (GameApi.PushableIsAttachedMethod != null)
                    + "（任一 False 时拖拽判定退化为 parent 兜底，锅不会从手里掉）");
            }

            PruneStates();
            TickPendingRespawns();

            var targets = Object.FindObjectsOfType<PushableVoidFallTarget>();
            for (int t = 0; t < targets.Length; t++)
            {
                var target = targets[t];
                if (target == null)
                    continue;

                var pushable = GameApi.GetComponent(target.gameObject, GameApi.PushableObjectType);
                if (pushable == null)
                    pushable = GameApi.GetComponentInParent(target, GameApi.PushableObjectType);
                if (pushable == null)
                    continue;

                var wrapper = target.transform.parent;
                if (wrapper == null)
                    wrapper = target.transform;

                var pilot = GameApi.GetComponentInChildren(target.gameObject, GameApi.PilotMovementType);
                var rb = target.GetComponent<Rigidbody>();
                if (rb == null)
                    rb = target.GetComponentInChildren<Rigidbody>();
                var col = target.GetComponent<Collider>();
                if (col == null)
                    col = target.GetComponentInChildren<Collider>();

                var state = GetState(wrapper);
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

                bool attached = IsAnyoneAttached(pushable, wrapper);
                if (attached)
                {
                    // 拖动中的玩家脚下悬空（正往水里掉）：只负责「松手」，锅绝不跟着坠。
                    // 锅自身是否坠落，由松手后下一 tick 起走未拖动分支的
                    // footprint 检测独立判定（大部分在岸上就不落水）——
                    // 即「人没落水锅不掉手；掉下之后再重新判定锅是否落水」。
                    if (IsDraggingPlayerOverVoid(pushable))
                        state.PlayerVoidStreak++;
                    else
                        state.PlayerVoidStreak = 0;
                    state.VoidStreak = 0;
                    if (state.PlayerVoidStreak < AttachedVoidConfirmTicks)
                        continue;
                    state.PlayerVoidStreak = 0;
                    EndPushableSession(pushable);
                    // 无前缀安全网（2026-09-04 事故）：Harmony 不可用时 KillPlane 前缀
                    // 缺席，宿主原生重生会把「父物体是即将隐藏载具」的玩家重生协程弄死
                    //（= 玩家挂锅落水回不来）。松手路径上按 parent 关系兜底脱离——
                    // session 语义判定（IsAttachedTo 反射）漏掉的挂载由此兜住。
                    ForceUnparentPlayers(wrapper);
                    StubLog.Log("[VoidFall] 拖动玩家悬空，原地松手: " + wrapper.name);
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

        // ============ 公开钩子（HarmonyPatches 调用） ============

        /// <summary>可移动火锅由本补丁负责坠落/重生，始终跳过宿主 KillPlane，
        /// 避免宿主 DestroyEntity 式重生（实体连同模型永久销毁）。</summary>
        internal static bool ShouldIgnoreKillPlane(GameObject gameObject)
        {
            if (gameObject == null)
                return true;
            // 两个白名单（与旧 LayoutRuntimePushableVoidFall 对齐）：载具标记组件，
            // 以及 wrapper 根上的 PushablePot 装配器（载具是 wrapper 的子孙）。
            return GameApi.HasComponentInParent(gameObject.transform, typeof(PushableVoidFallTarget))
                || GameApi.HasComponentInParent(gameObject.transform, typeof(PushablePot));
        }

        /// <summary>玩家触 KillPlane 重生前强制脱离火锅，避免仍挂在锅上：
        /// a) 玩家成为即将 SetActive(false) 载具的子物体（整条重生协程随父物体失效）；
        /// b) ClientChefSynchroniser 的 parent 状态与服务器不一致。</summary>
        internal static void DetachPlayerFromVoidFallPots(GameObject playerObject)
        {
            if (playerObject == null)
                return;
            var playerControls = GameApi.GetComponent(playerObject, GameApi.PlayerControlsType);
            if (playerControls == null)
                return;

            var targets = Object.FindObjectsOfType<PushableVoidFallTarget>();
            for (int t = 0; t < targets.Length; t++)
            {
                var target = targets[t];
                if (target == null)
                    continue;
                var pushable = GameApi.GetComponent(target.gameObject, GameApi.PushableObjectType);
                if (pushable == null || !IsAttachedTo(pushable, playerControls.transform))
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

        // ============ 内部实现（对齐原 LayoutRuntimePushableVoidFall） ============

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

        private static void CachePotRefs(PotTrackState state, Component pushable, Component pilot,
            Rigidbody rb, GameObject carrier)
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
            var planes = GameApi.FindAll(GameApi.RespawnColliderType);
            if (planes == null || planes.Length == 0)
            {
                s_killPlaneColliders = new Collider[0];
                return;
            }
            var cols = new List<Collider>();
            for (int i = 0; i < planes.Length; i++)
            {
                var comp = planes[i] as Component;
                if (comp == null)
                    continue;
                var col = comp.GetComponent<Collider>();
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

        /// <summary>坠落中的锅何时隐藏（2026-09-04 调整：不能「一触碰落水点就消失」）。
        /// 原判定在 killplane 顶 +0.25 即隐藏（视觉=贴水面瞬间消失）；现改为沉到
        /// 水面以下 SinkBelowSurface 再隐藏——坠落期间锅的碰撞体已全部关闭
        /// （BeginPotFall→SetCollidersEnabled(false)），宿主 KillPlane 碰不到它，
        /// 晚隐藏无宿主重生风险。绝对深度 RespawnFallDepth 兜底（无 killplane 场景）。</summary>
        private const float SinkBelowSurface = 0.8f;

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
                // 沉到水面以下 SinkBelowSurface 再隐藏（原 +0.25 → 观感「一碰水就消失」）
                if (pos.y > b.max.y - SinkBelowSurface)
                    continue;
                if (pos.x < b.min.x || pos.x > b.max.x || pos.z < b.min.z || pos.z > b.max.z)
                    continue;
                return true;
            }
            return false;
        }

        private static bool IsDraggingPlayerOverVoid(Component pushable)
        {
            if (pushable == null)
                return false;
            var players = GameApi.FindAll(GameApi.PlayerControlsType);
            for (int i = 0; i < players.Length; i++)
            {
                var pc = players[i] as Component;
                if (pc == null || !IsAttachedTo(pushable, pc.transform))
                    continue;
                var pos = pc.transform.position;
                if (!HasGroundSupport(pos.x, pos.z))
                    return true;
            }
            return false;
        }

        /// <summary>是否有人在拖拽锅。两路判定（2026-09-04 加固）：
        ///  1) attach-point 子物体计数（原判定，与旧版一致）；
        ///  2) 兜底：任一玩家 transform 仍 parent 在 wrapper 之下——拖拽会话会把
        ///     玩家挂到载具 attach point 上，parent 关系是拖拽的物理事实。反射链
        ///     （UseAttachPoints/AttachPoints/IParentable）任何一环失效时，原判定
        ///     误报「无人拖拽」→ 锅从手里直接坠（用户实测反馈）。</summary>
        private static bool IsAnyoneAttached(Component pushable, Transform wrapper)
        {
            if (IsAnyoneAttachedByAttachPoints(pushable))
                return true;
            return AnyPlayerParentedUnder(wrapper);
        }

        private static bool AnyPlayerParentedUnder(Transform wrapper)
        {
            if (wrapper == null)
                return false;
            var players = GameApi.FindAll(GameApi.PlayerControlsType);
            for (int i = 0; i < players.Length; i++)
            {
                var pc = players[i] as Component;
                if (pc == null)
                    continue;
                var parent = pc.transform.parent;
                if (parent != null && parent.IsChildOf(wrapper))
                    return true;
            }
            return false;
        }

        private static bool IsAnyoneAttachedByAttachPoints(Component pushable)
        {
            if (pushable == null)
                return false;
            try
            {
                bool useAttachPoints = GameApi.PushableUseAttachPointsField != null
                    && (bool)GameApi.PushableUseAttachPointsField.GetValue(pushable);
                if (useAttachPoints && GameApi.PushableAttachPointsField != null)
                {
                    var attachPoints = GameApi.PushableAttachPointsField.GetValue(pushable) as System.Array;
                    if (attachPoints != null)
                    {
                        for (int i = 0; i < attachPoints.Length; i++)
                        {
                            var ap = attachPoints.GetValue(i) as Component;
                            if (ap == null)
                                continue;
                            var parentable = GameApi.GetComponent(ap.gameObject, GameApi.ParentableInterfaceType);
                            if (parentable == null || GameApi.GetAttachPointMethod == null)
                                continue;
                            var attachPoint = GameApi.GetAttachPointMethod.Invoke(parentable,
                                new object[] { ap.gameObject }) as Transform;
                            if (attachPoint != null && attachPoint.childCount > 0)
                                return true;
                        }
                        return false;
                    }
                }
                if (GameApi.PushableCentrePointField != null)
                {
                    var centre = GameApi.PushableCentrePointField.GetValue(pushable) as Transform;
                    if (centre != null)
                        return centre.childCount > 0;
                }
            }
            catch (System.Exception)
            {
            }
            return false;
        }

        private static bool IsAttachedTo(Component pushable, Transform tr)
        {
            if (pushable == null || tr == null || GameApi.PushableIsAttachedMethod == null)
                return false;
            try
            {
                return (bool)GameApi.PushableIsAttachedMethod.Invoke(pushable, new object[] { tr });
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static void BeginPotFall(Transform wrapper, Component pushable, PotTrackState state)
        {
            if (wrapper == null || state == null)
                return;

            state.IsFalling = true;
            state.HiddenForRespawn = false;
            state.RespawnAt = -1f;

            if (pushable != null && IsAnyoneAttached(pushable, wrapper))
                EndPushableSession(pushable);

            // 无前缀安全网：坠落开始前按 parent 关系强制弹出全部乘员——玩家绝不能
            // 挂在载具上随它坠入 KillPlane（Harmony 缺席时宿主原生重生会卡死玩家；
            // 前缀在场时本调用是无害幂等兜底）。
            ForceUnparentPlayers(wrapper);

            SetCollidersEnabled(wrapper, false, state.Pushable);

            if (state.Pilot != null)
            {
                ReleasePilotGrid(state.Pilot);
                ClearPilotGridTarget(state.Pilot);
                ((Behaviour)state.Pilot).enabled = false;
            }

            if (state.Rigidbody != null)
            {
                SetKinematic(state.Rigidbody, true);
                state.Rigidbody.useGravity = false;
                state.Rigidbody.velocity = Vector3.zero;
                state.Rigidbody.angularVelocity = Vector3.zero;
            }

            StubLog.Log("[VoidFall] 锅开始坠落: " + wrapper.name);
        }

        private static void ApplyControlledFall(Transform wrapper, PotTrackState state)
        {
            if (state == null)
                return;

            if (state.Pilot != null && ((Behaviour)state.Pilot).enabled)
            {
                ClearPilotGridTarget(state.Pilot);
                ((Behaviour)state.Pilot).enabled = false;
            }

            if (state.Rigidbody != null)
            {
                SetKinematic(state.Rigidbody, true);
                state.Rigidbody.useGravity = false;
                state.Rigidbody.velocity = Vector3.zero;
                state.Rigidbody.angularVelocity = Vector3.zero;
            }

            // 坠落直接移动载具（物理与视觉的真正主体）；wrapper 保持不动
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

        private static void BeginSubmerge(Transform wrapper, Component pushable, PotTrackState state)
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

            // 官方道具重生同款隐藏：SetActive(false)——实体注册与同步组件原样保留
            var carrier = state.Carrier != null ? state.Carrier : FindCarrierFallback(wrapper);
            if (carrier != null && carrier.activeSelf)
                carrier.SetActive(false);

            StubLog.Log("[VoidFall] 锅沉没，" + RespawnDelay + "s 后重生: " + wrapper.name);
        }

        private static void RespawnPot(Transform wrapper, PotTrackState state)
        {
            if (wrapper == null || state == null || !state.SpawnRecorded)
                return;

            try
            {
                EndPushableSession(state.Pushable);
                ForceUnparentPlayers(wrapper);

                wrapper.SetPositionAndRotation(state.WrapperPosition, state.WrapperRotation);

                var carrier = state.Carrier != null ? state.Carrier : FindCarrierFallback(wrapper);
                if (carrier != null)
                {
                    carrier.transform.SetPositionAndRotation(state.SpawnPosition, state.SpawnRotation);
                    if (!carrier.activeSelf)
                        carrier.SetActive(true);
                }

                SetCollidersEnabled(wrapper, true, state.Pushable);

                if (state.Pilot != null)
                {
                    ClearPilotGridTarget(state.Pilot);
                    OccupyPilotGridNear(state.Pilot);
                    ((Behaviour)state.Pilot).enabled = true;
                }

                if (state.Rigidbody != null)
                {
                    state.Rigidbody.velocity = Vector3.zero;
                    state.Rigidbody.angularVelocity = Vector3.zero;
                    SetKinematic(state.Rigidbody, true);
                    state.Rigidbody.useGravity = false;
                    // ServerPilotMovement 恢复 tick 后自行接管运动
                }

                // ---- 复活强化带（2026-09-04「占位还在、模型不见」）----
                // 重生链主体与旧版逐行等价，但用户实测模型消失。三条兜底 + 诊断快照：
                EnsureRespawnVisibility(wrapper, carrier);
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[VoidFall] 重生失败 (" + wrapper.name + "): " + ex);
            }
            finally
            {
                // 无论成败都必须清标志：否则过期 RespawnAt 每 2 帧重试，刷屏
                state.IsFalling = false;
                state.HiddenForRespawn = false;
                state.RespawnAt = -1f;
                state.VoidStreak = 0;
                state.PlayerVoidStreak = 0;
                state.RespawnGraceUntil = Time.time + RespawnGraceSeconds;
            }

            StubLog.Log("[VoidFall] 锅已重生回开局点: " + wrapper.name);
        }

        /// <summary>重生可见性兜底 + 诊断（2026-09-04）：
        ///  1) 载具子树全部 Renderer 强制 enabled（防任何路径下的渲染器被关）；
        ///  2) 锅体（CustomStub.PushablePot 装配的 large_pot 子物体）若缺失，
        ///     摘掉载具上的 PushableVoidFallTarget 标记——PushablePot 监视循环
        ///     会在 0.5s 内发现标记消失并重新装配（旧标记在=已装配=永不重建）；
        ///  3) 打印可见性快照（activeSelf/activeInHierarchy/渲染器数/锅体/标记），
        ///     下次复现时日志直接指认消失环节。</summary>
        private static void EnsureRespawnVisibility(Transform wrapper, GameObject carrier)
        {
            if (carrier == null)
            {
                StubLog.LogWarn("[VoidFall] 重生诊断: 载具引用失效（被销毁/重建）: " + wrapper.name);
                return;
            }

            int reEnabled = 0;
            var renderers = carrier.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r != null && !r.enabled)
                {
                    r.enabled = true;
                    reEnabled++;
                }
            }

            // 父链被禁用变体：activeSelf=true 但 activeInHierarchy=false——沿祖先链
            // 激活到 wrapper 为止（渲染与协程都随层级失活，占位（网格/碰撞数据在
            // 服务器侧）却仍在 = 用户看到的「占位还在模型不见」的一种成因）。
            if (!carrier.activeInHierarchy)
            {
                var node = carrier.transform.parent;
                while (node != null && node != wrapper)
                {
                    if (!node.gameObject.activeSelf)
                    {
                        node.gameObject.SetActive(true);
                        StubLog.LogWarn("[VoidFall] 重生强化: 父节点被禁用，已激活: " + node.name);
                    }
                    node = node.parent;
                }
                if (wrapper != null && !wrapper.gameObject.activeSelf)
                {
                    wrapper.gameObject.SetActive(true);
                    StubLog.LogWarn("[VoidFall] 重生强化: wrapper 被禁用，已激活: " + wrapper.name);
                }
            }

            bool potPresent = false;
            foreach (Transform child in carrier.transform)
            {
                if (child != null && child.name.IndexOf("large_pot", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    potPresent = true;
                    break;
                }
            }
            if (!potPresent)
            {
                var marker = carrier.GetComponent<PushableVoidFallTarget>();
                if (marker != null)
                {
                    Object.Destroy(marker);
                    StubLog.LogWarn("[VoidFall] 重生强化: 锅体缺失，已摘标记触发 PushablePot 重装配: " + wrapper.name);
                }
            }

            StubLog.Log("[VoidFall] 重生诊断: " + wrapper.name
                + " carrier=" + carrier.name
                + " activeSelf=" + carrier.activeSelf
                + " activeInHierarchy=" + carrier.activeInHierarchy
                + " renderers=" + renderers.Length + "（重开 " + reEnabled + "）"
                + " 锅体=" + (potPresent ? "在" : "缺")
                + " 标记=" + (carrier.GetComponent<PushableVoidFallTarget>() != null ? "在" : "无"));
        }

        private static GameObject FindCarrierFallback(Transform wrapper)
        {
            if (wrapper == null || wrapper.childCount == 0)
                return null;
            return wrapper.GetChild(0).gameObject;
        }

        /// <summary>任何仍挂在 wrapper 之下（attach point）的玩家强制脱离父级
        /// （载具即将 SetActive(false)，玩家随父失活 = 重生协程停摆）。</summary>
        private static void ForceUnparentPlayers(Transform wrapper)
        {
            if (wrapper == null)
                return;
            var players = GameApi.FindAll(GameApi.PlayerControlsType);
            for (int i = 0; i < players.Length; i++)
            {
                var pc = players[i] as Component;
                if (pc == null)
                    continue;
                var parent = pc.transform.parent;
                if (parent == null || !parent.IsChildOf(wrapper))
                    continue;
                pc.transform.SetParent(null, true);
                RestoreDetachedPlayer(pc);
            }
        }

        /// <summary>结束 PushableObject 的交互会话（host/单机生效）。
        /// 只有 m_session 非空才走结束流程并广播结束消息（无条件结束会发 interacterID=0
        /// 的重复消息，客户端 NRE 后 SetInteractionSuppressed(false) 不再执行，
        /// 从此无法再抓取——旧版踩过的坑）。</summary>
        private static void EndPushableSession(Component pushable)
        {
            if (pushable == null)
                return;

            var serverPushable = GameApi.GetComponent(pushable.gameObject, GameApi.ServerPushableObjectType);
            if (serverPushable == null)
                return;

            if (GameApi.SessionField == null)
                return;

            object session;
            try
            {
                session = GameApi.SessionField.GetValue(serverPushable);
            }
            catch
            {
                return;
            }

            if (session == null)
                return;

            DetachAllPlayersFromPushable(pushable);

            GameApi.Safe<object>(delegate
            {
                var m = session.GetType().GetMethod("OnSessionEnded",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                    null, System.Type.EmptyTypes, null);
                if (m != null)
                    m.Invoke(session, null);
                return null;
            });

            if (GameApi.ServerSessionOnEndedMethod != null)
            {
                try
                {
                    // 宿主原版流程：SynchroniseInteractionState(null) 广播结束消息 →
                    // 解除交互抑制 → 清 m_session
                    GameApi.ServerSessionOnEndedMethod.Invoke(serverPushable, null);
                }
                catch
                {
                }
            }
        }

        private static void DetachAllPlayersFromPushable(Component pushable)
        {
            if (pushable == null)
                return;

            var pilot = GameApi.GetComponent(pushable.gameObject, GameApi.PilotMovementType);
            if (pilot != null && GameApi.PilotAssignPlayerMethod != null)
            {
                try
                {
                    // AssignPlayer(ControlSchemeData)：null = 解除指派
                    GameApi.PilotAssignPlayerMethod.Invoke(pilot, new object[] { null });
                }
                catch
                {
                }
            }

            var players = GameApi.FindAll(GameApi.PlayerControlsType);
            for (int i = 0; i < players.Length; i++)
            {
                var pc = players[i] as Component;
                if (pc == null || !IsAttachedTo(pushable, pc.transform))
                    continue;
                pc.transform.SetParent(null, true);
                RestoreDetachedPlayer(pc);
            }

            if (GameApi.PushableFakeColliderField != null)
            {
                var fake = GameApi.PushableFakeColliderField.GetValue(pushable) as Collider;
                if (fake != null)
                    fake.enabled = false;
            }
        }

        /// <summary>会话结束后的玩家状态恢复（与 ServerPushableObject.UserSession.
        /// OnSessionEnded 等价）。只处理本机控制的玩家：远程 chef 克隆由
        /// ClientChefSynchroniser 保持 kinematic，强行恢复物会让远程玩家失控。</summary>
        private static void RestoreDetachedPlayer(Component pc)
        {
            if (pc == null)
                return;

            if (!IsLocallyControlled(pc))
                return;

            ((Behaviour)pc).enabled = true;

            var rb = pc.GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = false;

            if (GameApi.DynamicLandscapeParentingType != null)
            {
                var dlp = GameApi.GetComponent(pc.gameObject, GameApi.DynamicLandscapeParentingType) as Behaviour;
                if (dlp != null)
                    dlp.enabled = true;
            }

            if (GameApi.PlayerGroundCastField != null)
            {
                var groundCast = GameApi.PlayerGroundCastField.GetValue(pc);
                if (groundCast != null)
                {
                    try
                    {
                        if (GameApi.GroundCastClearMethod != null)
                            GameApi.GroundCastClearMethod.Invoke(groundCast, null);
                        if (GameApi.GroundCastForceUpdateMethod != null)
                            GameApi.GroundCastForceUpdateMethod.Invoke(groundCast, null);
                    }
                    catch
                    {
                    }
                }
            }

            if (GameApi.PlayerApplyGravityField != null)
            {
                try
                {
                    GameApi.PlayerApplyGravityField.SetValue(pc, true);
                }
                catch
                {
                }
            }
        }

        private static bool IsLocallyControlled(Component pc)
        {
            if (GameApi.PlayerIDProviderProperty == null || GameApi.IsLocallyControlledMethod == null)
                return true; // 无法判定时按本机处理（旧行为）
            try
            {
                var provider = GameApi.PlayerIDProviderProperty.GetValue(pc, null);
                if (provider == null)
                    return true;
                return (bool)GameApi.IsLocallyControlledMethod.Invoke(provider, null);
            }
            catch (System.Exception)
            {
                return true;
            }
        }

        private static void DisposePotContents(Transform wrapper)
        {
            if (wrapper == null || GameApi.ContentsDisposalType == null || GameApi.AddToDisposerMethod == null)
                return;
            var disposals = GameApi.GetComponentsInChildren(wrapper.gameObject, GameApi.ContentsDisposalType, true);
            for (int i = 0; i < disposals.Length; i++)
            {
                if (disposals[i] == null)
                    continue;
                try
                {
                    // AddToDisposer(null)（单参 IDisposer 重载）：传 null 即倒掉全部内容
                    GameApi.AddToDisposerMethod.Invoke(disposals[i], new object[] { null });
                }
                catch
                {
                }
            }
        }

        private static void ClearPilotGridTarget(Component pilot)
        {
            if (pilot == null || GameApi.PilotGridTargetField == null)
                return;
            try
            {
                GameApi.PilotGridTargetField.SetValue(pilot, null);
            }
            catch
            {
            }
        }

        /// <summary>释放载具在 GridManager 占用的格子（ServerPilotMovement.m_min/m_max），
        /// 否则坠落点附近的可走格子被永久占用。</summary>
        private static void ReleasePilotGrid(Component pilot)
        {
            if (pilot == null || GameApi.GridDeoccupyMethod == null
                || GameApi.PilotGridManagerField == null || GameApi.PilotMinField == null || GameApi.PilotMaxField == null)
                return;
            try
            {
                var grid = GameApi.PilotGridManagerField.GetValue(pilot);
                if (grid == null)
                    return;
                var min = GameApi.PilotMinField.GetValue(pilot);
                var max = GameApi.PilotMaxField.GetValue(pilot);
                GameApi.GridDeoccupyMethod.Invoke(grid, new[] { min, max });
            }
            catch
            {
            }
        }

        /// <summary>重生点重新占用网格（复刻 ServerPilotMovement.StartSynchronising 的
        /// 初始占用）。失败时静默跳过——首次被拖动时会自行修复占用。</summary>
        private static void OccupyPilotGridNear(Component pilot)
        {
            if (pilot == null || GameApi.GridTryOccupyMethod == null || GameApi.GridLocationFromPosMethod == null
                || GameApi.PilotGridManagerField == null || GameApi.PilotMinField == null
                || GameApi.PilotMaxField == null || GameApi.PilotExtentsField == null
                || GameApi.PilotColliderField == null)
                return;
            try
            {
                var grid = GameApi.PilotGridManagerField.GetValue(pilot);
                var col = GameApi.PilotColliderField.GetValue(pilot) as Collider;
                if (grid == null || col == null)
                    return;
                Vector3 extents = (Vector3)GameApi.PilotExtentsField.GetValue(pilot);
                Vector3 center = col.bounds.center;
                var min = GameApi.GridLocationFromPosMethod.Invoke(grid, new object[] { center - extents });
                var max = GameApi.GridLocationFromPosMethod.Invoke(grid, new object[] { center + extents });
                bool ok = (bool)GameApi.GridTryOccupyMethod.Invoke(grid, new[] { min, max, pilot.gameObject });
                if (ok)
                {
                    GameApi.PilotMinField.SetValue(pilot, min);
                    GameApi.PilotMaxField.SetValue(pilot, max);
                }
            }
            catch
            {
            }
        }

        private static void SetCollidersEnabled(Transform root, bool enabled, Component pushable)
        {
            if (root == null)
                return;
            Collider fakeCollider = null;
            if (pushable != null && GameApi.PushableFakeColliderField != null)
                fakeCollider = GameApi.PushableFakeColliderField.GetValue(pushable) as Collider;
            var cols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null)
                    continue;
                if (enabled && cols[i] == fakeCollider)
                {
                    // fakePlayerCollider 只在拖动会话期间启用（宿主约定）
                    cols[i].enabled = false;
                    continue;
                }
                cols[i].enabled = enabled;
            }
        }

        private static void SetKinematic(Rigidbody rb, bool kinematic)
        {
            var motion = rb.GetComponent(GameApi.RigidbodyMotionType);
            if (motion != null && GameApi.SetKinematicMethod != null)
            {
                try
                {
                    GameApi.SetKinematicMethod.Invoke(motion, new object[] { kinematic });
                    return;
                }
                catch
                {
                }
            }
            rb.isKinematic = kinematic;
        }
    }
}
