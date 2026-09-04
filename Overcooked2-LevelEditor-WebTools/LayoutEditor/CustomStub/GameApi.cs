using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// Assembly-CSharp（宿主程序集）类型的共享反射缓存。
    ///
    /// 铁律（2026-09-02 真机事故教训）：新增反射目标前先确认
    /// 「vanilla 游戏 + OC2DIYLevel 模组的 AppDomain 里真的存在这类型」——
    /// LevelEditor.* / PseudoPrefab 系列只存在于编辑器宿主，游戏侧没有，
    /// 绝不反射它们。本文件列出的类型均已逐一在 vanilla 源码里核验过。
    ///
    /// 所有静态字段初始化一律 Safe 包裹（GetMethod types 数组含 null 元素会抛
    /// ArgumentNullException，静态构造抛异常 = TypeInitializationException =
    /// 协程无声死亡）。
    /// </summary>
    internal static class GameApi
    {
        // ---- 基础工具 ----
        public static readonly Type GameObjectUtilsType = Find("GameObjectUtils");
        public static readonly MethodInfo SendTriggerMethod = Safe(delegate
        {
            return GameObjectUtilsType != null
                ? GameObjectUtilsType.GetMethod("SendTrigger", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(GameObject), typeof(string) }, null)
                : null;
        });

        // ---- 同步状态 / 服务端判定（游戏标准 API） ----
        public static readonly Type MultiplayerControllerType = Find("MultiplayerController");
        public static readonly MethodInfo IsSyncActiveMethod = Safe(delegate
        {
            return MultiplayerControllerType != null
                ? MultiplayerControllerType.GetMethod("IsSynchronisationActive",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly Type ConnectionStatusType = Find("ConnectionStatus");
        public static readonly MethodInfo IsHostMethod = Safe(delegate
        {
            return ConnectionStatusType != null
                ? ConnectionStatusType.GetMethod("IsHost", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly MethodInfo IsInSessionMethod = Safe(delegate
        {
            return ConnectionStatusType != null
                ? ConnectionStatusType.GetMethod("IsInSession", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null, Type.EmptyTypes, null)
                : null;
        });

        // ---- 火锅：灶台 / 锅 ----
        public static readonly Type CookingRegionType = Find("CookingRegion");
        public static readonly FieldInfo RegionTriggerAreaField = Field(CookingRegionType, "m_TriggerArea");
        public static readonly FieldInfo RegionFlameEffectsField = Field(CookingRegionType, "m_flameEffects");
        public static readonly FieldInfo RegionGlowEffectField = Field(CookingRegionType, "m_glowEffect");

        public static readonly Type ServerCookingRegionType = Find("ServerCookingRegion");
        public static readonly Type ClientCookingRegionType = Find("ClientCookingRegion");
        public static readonly FieldInfo ServerGridIndexField = Field(ServerCookingRegionType, "m_gridIndex");
        public static readonly FieldInfo ClientGridIndexField = Field(ClientCookingRegionType, "m_gridIndex");

        public static readonly Type ServerCookingHandlerType = Find("ServerCookingHandler");
        public static readonly MethodInfo HandlerIsCookedMethod = Safe(delegate
        {
            return ServerCookingHandlerType != null
                ? ServerCookingHandlerType.GetMethod("IsCooked", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly MethodInfo HandlerIsBurningMethod = Safe(delegate
        {
            return ServerCookingHandlerType != null
                ? ServerCookingHandlerType.GetMethod("IsBurning", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly MethodInfo HandlerCookMethod = Safe(delegate
        {
            return ServerCookingHandlerType != null
                ? ServerCookingHandlerType.GetMethod("Cook", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(float) }, null)
                : null;
        });

        public static readonly Type ServerIngredientContainerType = Find("ServerIngredientContainer");
        public static readonly MethodInfo HasContentsMethod = Safe(delegate
        {
            // 定义在 IngredientContainer 基类上（public），按声明类型查找。
            var owner = ServerIngredientContainerType != null ? ServerIngredientContainerType.BaseType : null;
            if (owner != null && owner.Name == "IngredientContainer")
                return owner.GetMethod("HasContents", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null);
            return ServerIngredientContainerType != null
                ? ServerIngredientContainerType.GetMethod("HasContents", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });

        public static readonly Type WokEffectsType = Find("ClientWokEffectsCosmeticDecisions");
        public static readonly MethodInfo WokEnterMethod = Safe(delegate
        {
            return WokEffectsType != null
                ? WokEffectsType.GetMethod("EnterCookingRegion", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly MethodInfo WokExitMethod = Safe(delegate
        {
            return WokEffectsType != null
                ? WokEffectsType.GetMethod("ExitCookingRegion", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });

        // ---- 火锅：汤面高度兜底（ContentsCosmeticDecisions / 容器容量 / 内容物，
        //      2026-09-03 从旧 LayoutRuntimeHotPot.FixSoupLevel 移植） ----
        public static readonly Type ContentsCosmeticType = Find("ContentsCosmeticDecisions");
        public static readonly FieldInfo ContentsObjectField = Field(ContentsCosmeticType, "m_contentsObject");
        public static readonly FieldInfo ContentsYEmptyField = Field(ContentsCosmeticType, "m_contentsYPositionWhenEmpty");
        public static readonly FieldInfo ContentsYFullField = Field(ContentsCosmeticType, "m_contentsYPositionWhenFull");
        public static readonly FieldInfo ContentsGameObjectField = Field(ContentsCosmeticType, "m_gameObject");
        public static readonly Type ClientIngredientContainerType = Find("ClientIngredientContainer");
        public static readonly MethodInfo GetContentsMethod = Safe(delegate
        {
            return ClientIngredientContainerType != null
                ? ClientIngredientContainerType.GetMethod("GetContents", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly FieldInfo ContainerCapacityField = Safe(delegate
        {
            // m_capacity 定义在 IngredientContainer 基类（public），按声明类型查找
            //（同 HasContentsMethod 模式）。
            var owner = ServerIngredientContainerType != null ? ServerIngredientContainerType.BaseType : null;
            if (owner != null && owner.Name == "IngredientContainer")
                return owner.GetField("m_capacity", BindingFlags.Public | BindingFlags.Instance);
            return ServerIngredientContainerType != null
                ? ServerIngredientContainerType.GetField("m_capacity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                : null;
        });

        // ---- 火锅：煮熟提示音 ----
        public static readonly Type GameUtilsType = Find("GameUtils");
        public static readonly Type GameOneShotAudioTagType = Find("GameOneShotAudioTag");
        public static readonly MethodInfo TriggerAudioMethod = Safe(delegate
        {
            return GameUtilsType != null && GameOneShotAudioTagType != null
                ? GameUtilsType.GetMethod("TriggerAudio", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { GameOneShotAudioTagType, typeof(int) }, null)
                : null;
        });
        public static readonly object ImCookedTag = Safe(delegate
        {
            return GameOneShotAudioTagType != null ? Enum.Parse(GameOneShotAudioTagType, "ImCooked") : null;
        });

        // ---- 可移动火锅：载具 / 会话 / 网格 ----
        public static readonly Type PushableObjectType = Find("PushableObject");
        public static readonly Type PilotMovementType = Find("ServerPilotMovement");
        public static readonly FieldInfo PilotGridTargetField = Field(PilotMovementType, "m_gridTarget");
        public static readonly FieldInfo PilotGridManagerField = Field(PilotMovementType, "m_gridManager");
        public static readonly FieldInfo PilotMinField = Field(PilotMovementType, "m_min");
        public static readonly FieldInfo PilotMaxField = Field(PilotMovementType, "m_max");
        public static readonly FieldInfo PilotExtentsField = Field(PilotMovementType, "m_extents");
        public static readonly FieldInfo PilotColliderField = Field(PilotMovementType, "m_collider");

        public static readonly Type RigidbodyMotionType = Find("RigidbodyMotion");
        public static readonly MethodInfo SetKinematicMethod = Safe(delegate
        {
            return RigidbodyMotionType != null
                ? RigidbodyMotionType.GetMethod("SetKinematic", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(bool) }, null)
                : null;
        });

        public static readonly Type ServerSessionInteractableType = Find("ServerSessionInteractable");
        public static readonly FieldInfo SessionField = Field(ServerSessionInteractableType, "m_session");
        public static readonly MethodInfo ServerSessionOnEndedMethod = Safe(delegate
        {
            return ServerSessionInteractableType != null
                ? ServerSessionInteractableType.GetMethod("OnSessionEnded",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });

        public static readonly Type ServerPushableObjectType = Find("ServerPushableObject");

        // PushableObject 成员（会话判定 / 抓取点）
        public static readonly FieldInfo PushableUseAttachPointsField = Field(PushableObjectType, "m_UseAttachPoints");
        public static readonly FieldInfo PushableAttachPointsField = Field(PushableObjectType, "m_AttachPoints");
        public static readonly FieldInfo PushableCentrePointField = Field(PushableObjectType, "m_CentrePoint");
        public static readonly FieldInfo PushableFakeColliderField = Field(PushableObjectType, "m_fakePlayerCollider");
        public static readonly MethodInfo PushableIsAttachedMethod = Safe(delegate
        {
            return PushableObjectType != null
                ? PushableObjectType.GetMethod("IsAttached", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(Transform) }, null)
                : null;
        });
        public static readonly Type ParentableInterfaceType = Safe(delegate
        {
            // 接口也走 Find（按名可寻）
            var t = Type.GetType("IParentable, Assembly-CSharp");
            if (t != null)
                return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm == null)
                    continue;
                t = asm.GetType("IParentable", false);
                if (t != null)
                    return t;
            }
            return null;
        });
        public static readonly MethodInfo GetAttachPointMethod = Safe(delegate
        {
            return ParentableInterfaceType != null
                ? ParentableInterfaceType.GetMethod("GetAttachPoint", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(GameObject) }, null)
                : null;
        });
        public static readonly Type DynamicLandscapeParentingType = Find("DynamicLandscapeParenting");
        public static readonly MethodInfo IsLocallyControlledMethod = Safe(delegate
        {
            // PlayerIDProvider 的接口方法（IPlayerIDProvider.IsLocallyControlled）
            var iface = Find("IPlayerIDProvider");
            return iface != null
                ? iface.GetMethod("IsLocallyControlled", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });

        public static readonly Type ContentsDisposalType = Find("ServerContentsDisposalBehaviour");
        public static readonly Type IDisposerInterfaceType = Find("IDisposer");
        public static readonly MethodInfo AddToDisposerMethod = Safe(delegate
        {
            return ContentsDisposalType != null && IDisposerInterfaceType != null
                ? ContentsDisposalType.GetMethod("AddToDisposer",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { IDisposerInterfaceType }, null)
                : null;
        });
        public static readonly Type RespawnColliderType = Find("RespawnCollider");
        public static readonly MethodInfo PilotAssignPlayerMethod = Safe(delegate
        {
            return PilotMovementType != null
                ? PilotMovementType.GetMethod("AssignPlayer", BindingFlags.Public | BindingFlags.Instance)
                : null;
        });

        public static readonly Type GridManagerType = Find("GridManager");
        public static readonly Type GridIndexType = Find("GridIndex");
        public static readonly MethodInfo GridDeoccupyMethod = Safe(delegate
        {
            return GridManagerType != null
                ? GridManagerType.GetMethod("DeoccupyGridRegion", BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { GridIndexType, GridIndexType }, null)
                : null;
        });
        public static readonly MethodInfo GridTryOccupyMethod = Safe(delegate
        {
            return GridManagerType != null
                ? GridManagerType.GetMethod("TryOccupyGridRegion", BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { GridIndexType, GridIndexType, typeof(GameObject) }, null)
                : null;
        });
        public static readonly MethodInfo GridLocationFromPosMethod = Safe(delegate
        {
            return GridManagerType != null
                ? GridManagerType.GetMethod("GetGridLocationFromPos", BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(Vector3) }, null)
                : null;
        });

        // ---- 可移动火锅：玩家脱离恢复 ----
        public static readonly Type PlayerControlsType = Find("PlayerControls");
        public static readonly FieldInfo PlayerGroundCastField = Field(PlayerControlsType, "m_groundCast");
        public static readonly FieldInfo PlayerApplyGravityField = Field(PlayerControlsType, "m_bApplyGravity");
        public static readonly PropertyInfo PlayerIDProviderProperty = Prop(PlayerControlsType, "PlayerIDProvider");
        public static readonly Type GroundCastType = Find("GroundCast");
        public static readonly MethodInfo GroundCastClearMethod = Safe(delegate
        {
            return GroundCastType != null
                ? GroundCastType.GetMethod("ClearGround", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly MethodInfo GroundCastForceUpdateMethod = Safe(delegate
        {
            return GroundCastType != null
                ? GroundCastType.GetMethod("ForceUpdateNow", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });

        // ---- 世界地图装饰 ----
        public static readonly Type WorldMapOptimizerType = Find("WorldMapSceneryOptimizer");
        public static readonly PropertyInfo OptimizerMeshProperty = Prop(WorldMapOptimizerType, "Mesh");
        public static readonly MethodInfo OptimizerEndMethod = Safe(delegate
        {
            return WorldMapOptimizerType != null
                ? WorldMapOptimizerType.GetMethod("End", BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { Find("FlipDirection") }, null)
                : null;
        });
        public static readonly object FlipUnfold = Safe(delegate
        {
            var t = Find("FlipDirection");
            return t != null ? Enum.Parse(t, "Unfold") : null;
        });

        // ---- 按钮复位（TriggerDisableScript 数据 + Interactable 状态轮询） ----
        public static readonly Type TriggerDisableType = Find("TriggerDisableScript");
        public static readonly FieldInfo DisableScriptField = Field(TriggerDisableType, "m_script");
        public static readonly FieldInfo DisableEnableTriggerField = Field(TriggerDisableType, "m_enableTrigger");

        // ---- 锅具食材许可表（CookableContainer.m_approvedContentsList） ----
        public static readonly Type CookableContainerType = Find("CookableContainer");
        public static readonly FieldInfo ApprovedContentsField = Field(CookableContainerType, "m_approvedContentsList");
        public static readonly Type OrderToPrefabLookupType = Find("OrderToPrefabLookup");
        public static readonly Type ContentPrefabLookupType = Safe(delegate
        {
            return OrderToPrefabLookupType != null
                ? OrderToPrefabLookupType.GetNestedType("ContentPrefabLookup", BindingFlags.Public)
                : null;
        });
        public static readonly FieldInfo LookupArrayField = Field(OrderToPrefabLookupType, "m_lookupArray");
        public static readonly FieldInfo LookupContentField = Field(ContentPrefabLookupType, "m_content");
        public static readonly FieldInfo LookupPrefabField = Field(ContentPrefabLookupType, "m_prefab");

        // ---- 锅上需要剥掉的游戏组件（可推动载具独占物理/交互） ----
        public static readonly Type InteractableType = Find("Interactable");
        public static readonly Type EditorGridSnapType = Find("EditorGridSnap");
        public static readonly Type AttachStationType = Find("AttachStation");

        // ---- Harmony 目标：宿主 KillPlane ----
        public static readonly MethodInfo RespawnObjectAddedMethod = Safe(delegate
        {
            var t = Find("ServerRespawnCollider");
            return t != null
                ? t.GetMethod("ObjectAdded", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(GameObject) }, null)
                : null;
        });
        // ============ 运行时辅助（不缓存，静态只读之外的部分） ============

        /// <summary>网络实体扫描+链接+StartSynchronising 是否全部完成。
        /// 反射失败（API 缺失）按 true 处理，保持旧行为兼容。</summary>
        internal static bool IsSynchronisationActive()
        {
            if (IsSyncActiveMethod == null)
                return true;
            try
            {
                return (bool)IsSyncActiveMethod.Invoke(null, null);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>本机是否服务端：ConnectionStatus.IsHost() || !IsInSession()。
        /// 反射失败按 true（单机场景为主）。</summary>
        internal static bool IsServerMachine()
        {
            if (IsHostMethod == null || IsInSessionMethod == null)
                return true;
            try
            {
                return (bool)IsHostMethod.Invoke(null, null)
                    || !(bool)IsInSessionMethod.Invoke(null, null);
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>按名找已加载的 AssetBundle（bundleName 即 bundle 文件名，如 bundle226）。</summary>
        internal static AssetBundle GetAssetBundle(string bundleName)
        {
            if (string.IsNullOrEmpty(bundleName))
                return null;
            foreach (var b in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (b == null)
                    continue;
                if (string.Equals(b.name, bundleName, StringComparison.OrdinalIgnoreCase))
                    return b;
            }
            return null;
        }

        /// <summary>GameObjectUtils.SendTrigger 等价（编译期不可引用扩展方法）。</summary>
        internal static void SendTrigger(GameObject target, string trigger)
        {
            if (target == null || string.IsNullOrEmpty(trigger) || SendTriggerMethod == null)
                return;
            try
            {
                SendTriggerMethod.Invoke(null, new object[] { target, trigger });
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[GameApi] SendTrigger 失败 " + trigger + " → " + target.name + ": " + ex.Message);
            }
        }

        /// <summary>场景里某游戏类型的全部实例（含未激活）。</summary>
        internal static UnityEngine.Object[] FindAll(Type type)
        {
            if (type == null)
                return new UnityEngine.Object[0];
            return (UnityEngine.Object[])UnityEngine.Object.FindObjectsOfType(type);
        }

        /// <summary>按类型在对象上取组件（类型为反射 Type）。</summary>
        internal static Component GetComponent(GameObject go, Type type)
        {
            if (go == null || type == null)
                return null;
            return go.GetComponent(type);
        }

        internal static Component GetComponentInChildren(GameObject go, Type type)
        {
            if (go == null || type == null)
                return null;
            return go.GetComponentInChildren(type);
        }

        internal static Component[] GetComponentsInChildren(GameObject go, Type type, bool includeInactive)
        {
            if (go == null || type == null)
                return new Component[0];
            return go.GetComponentsInChildren(type, includeInactive);
        }

        internal static Component GetComponentInParent(Component c, Type type)
        {
            if (c == null || type == null)
                return null;
            return c.GetComponentInParent(type);
        }

        /// <summary>变换祖先链上任一对象带指定类型组件。</summary>
        internal static bool HasComponentInParent(Transform t, Type type)
        {
            while (t != null)
            {
                if (t.GetComponent(type) != null)
                    return true;
                t = t.parent;
            }
            return false;
        }

        // ============ 反射基础 ============

        internal static T Safe<T>(Func<T> f) where T : class
        {
            try
            {
                return f();
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static Type Find(string typeName)
        {
            var t = Type.GetType(typeName + ", Assembly-CSharp");
            if (t != null)
                return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm == null)
                    continue;
                t = asm.GetType(typeName, false);
                if (t != null)
                    return t;
            }
            return null;
        }

        internal static FieldInfo Field(Type type, string fieldName)
        {
            return type != null
                ? type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                : null;
        }

        internal static PropertyInfo Prop(Type type, string propName)
        {
            return type != null
                ? type.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                : null;
        }
    }
}
