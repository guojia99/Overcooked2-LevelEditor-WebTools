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

        // ---- 网络实体扫描窗口（2026-09-15 v14，联机 ID 错位事故） ----
        //
        // 背景铁律：EntitySerialisationRegistry 的实体 ID 是【按扫描命中顺序递增的
        // FIFO】（EntitySerialisationRegistry.cs:123-129 填 1..1022，:439-442 Dequeue），
        // 与对象身份无关。主客机各自本地跑一遍 LinkAllEntitiesToSynchronisationScripts，
        // 靠「两边遍历出的对象序列一致」保证 ID 对齐。而该链接循环【每 0.1 秒 yield
        // 一帧】（:197-201 / :228-232）、每个类型现场 GetComponentsInChildren——
        // 扫描窗口期内 Instantiate 出来的对象会插进序列的随机位置，两台机器插入点不同
        // ⇒ 从该点起全部实体 ID 整体错位 ⇒ 客机完全不能动、模型丢失。
        //
        // 因此运行时新建网络实体（目前只有 PushablePot 的大锅）必须满足：
        //   扫描开始【前】完成，或扫描彻底结束【后】再做（后者只影响该对象自身）。
        public static readonly MethodInfo ScanEntitiesMethod = Safe(delegate
        {
            return MultiplayerControllerType != null
                ? MultiplayerControllerType.GetMethod("ScanEntities",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(Action) }, null)
                : null;
        });
        public static readonly MethodInfo StartSynchronisationMethod = Safe(delegate
        {
            return MultiplayerControllerType != null
                ? MultiplayerControllerType.GetMethod("StartSynchronisation",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        /// <summary>MultiplayerController.ScanActive（扫描协程是否在跑，:50-56）。</summary>
        public static readonly PropertyInfo ScanActiveProperty = Safe(delegate
        {
            return MultiplayerControllerType != null
                ? MultiplayerControllerType.GetProperty("ScanActive",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                : null;
        });

        // ---- 实体注册表（诊断：实体指纹 / 单对象注册自检） ----
        public static readonly Type EntityRegistryType = Find("EntitySerialisationRegistry");
        /// <summary>链接循环进行中标志（EntitySerialisationRegistry.cs:41/165/235）——
        /// 比 ScanActive 更精确地圈出「正在分配实体 ID」的致命窗口，两者取或。</summary>
        public static readonly FieldInfo EntityLinkingFlagField = Safe(delegate
        {
            return EntityRegistryType != null
                ? EntityRegistryType.GetField("s_bLinkingEntities",
                    BindingFlags.NonPublic | BindingFlags.Static)
                : null;
        });
        public static readonly MethodInfo EntityGetEntryMethod = Safe(delegate
        {
            return EntityRegistryType != null
                ? EntityRegistryType.GetMethod("GetEntry", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(GameObject) }, null)
                : null;
        });
        public static readonly MethodInfo EntityGetIdMethod = Safe(delegate
        {
            return EntityRegistryType != null
                ? EntityRegistryType.GetMethod("GetId", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(GameObject) }, null)
                : null;
        });
        public static readonly FieldInfo EntitiesByGameObjectField = Safe(delegate
        {
            return EntityRegistryType != null
                ? EntityRegistryType.GetField("m_EntitiesByGameObject",
                    BindingFlags.Public | BindingFlags.Static)
                : null;
        });

        // ---- 关卡网络时序打点（诊断：主客机状态机对比） ----
        // ClientKitchenLoader 的状态机是所有联机时序问题的主干（LoadedKitchen →
        // ScanNetworkEntities → ScannedNetworkEntities → StartSynchronising →
        // StartEntities），但它一条日志都不打。这两个私有无参方法是其中两个
        // 关键节点，用无参前缀打点最安全（不依赖宿主参数名/类型）。
        public static readonly Type ClientKitchenLoaderType = Find("ClientKitchenLoader");
        public static readonly MethodInfo KitchenScannedEntitiesMethod = Safe(delegate
        {
            return ClientKitchenLoaderType != null
                ? ClientKitchenLoaderType.GetMethod("ScannedEntities",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly MethodInfo KitchenStartEntitiesMethod = Safe(delegate
        {
            return ClientKitchenLoaderType != null
                ? ClientKitchenLoaderType.GetMethod("StartEntities",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });

        // ---- 火锅：灶台 / 锅 ----
        public static readonly Type CookingRegionType = Find("CookingRegion");
        public static readonly FieldInfo RegionTriggerAreaField = Field(CookingRegionType, "m_TriggerArea");
        public static readonly FieldInfo RegionFlameEffectsField = Field(CookingRegionType, "m_flameEffects");
        public static readonly FieldInfo RegionGlowEffectField = Field(CookingRegionType, "m_glowEffect");
        public static readonly FieldInfo RegionBurnerRendererField = Field(CookingRegionType, "m_burnerRenderer");

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

        // ---- 锅具时间参数（UtensilTiming + HarmonyPatches 共用；全部 vanilla 类型） ----
        public static readonly Type CookingHandlerType = Find("CookingHandler");
        public static readonly FieldInfo CookingTimeField = Field(CookingHandlerType, "m_cookingtime");
        public static readonly Type MixingHandlerType = Find("MixingHandler");
        public static readonly FieldInfo MixingTimeField = Field(MixingHandlerType, "m_mixingTime");

        public static readonly Type ClientCookingHandlerType = Find("ClientCookingHandler");
        public static readonly Type ServerMixingHandlerType = Find("ServerMixingHandler");
        public static readonly Type ClientMixingHandlerType = Find("ClientMixingHandler");

        // ---- 客户端「锅在灶台上」标志（IClientCookingRegionNotified 实现，
        //      ClientCookingHandler.m_isInCookingRegion）----
        // vanilla 只由 ClientCookingRegion 经 GridManager.GetGridOccupant(m_gridIndex)
        // + 同格复核来驱动（ClientCookingRegion.UpdateSynchronising）——可移动火锅的
        // 动态格子过不了这道判定，标志恒为 false。而 ClientCookingHandler.ApplyServerUpdate
        // 在标志为 false 时会把「即将烧糊」(OverDoing) 等非 Progressing 状态强制显示成
        // Idle（图标被吞）。HotPot 按触发区直接驱动这两个方法补上该标志。
        public static readonly MethodInfo ClientCookingEnterRegionMethod = Safe(delegate
        {
            return ClientCookingHandlerType != null
                ? ClientCookingHandlerType.GetMethod("EnterCookingRegion", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly MethodInfo ClientCookingExitRegionMethod = Safe(delegate
        {
            return ClientCookingHandlerType != null
                ? ClientCookingHandlerType.GetMethod("ExitCookingRegion", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });

        public static readonly MethodInfo ServerGetCookingProgressMethod = Safe(delegate
        {
            return ServerCookingHandlerType != null
                ? ServerCookingHandlerType.GetMethod("GetCookingProgress", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly MethodInfo ServerGetMixingProgressMethod = Safe(delegate
        {
            return ServerMixingHandlerType != null
                ? ServerMixingHandlerType.GetMethod("GetMixingProgress", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly MethodInfo ClientGetCookingProgressMethod = Safe(delegate
        {
            return ClientCookingHandlerType != null
                ? ClientCookingHandlerType.GetMethod("GetCookingProgress", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly MethodInfo ClientGetMixingProgressMethod = Safe(delegate
        {
            return ClientMixingHandlerType != null
                ? ClientMixingHandlerType.GetMethod("GetMixingProgress", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });

        // 同步消息字段（CookingStateMessage / MixingStateMessage 全 public）
        public static readonly FieldInfo ServerCookingDataField = Field(ServerCookingHandlerType, "m_ServerData");
        public static readonly Type CookingStateMessageType = Find("CookingStateMessage");
        public static readonly FieldInfo CookingMsgProgressField = Field(CookingStateMessageType, "m_cookingProgress");
        public static readonly FieldInfo CookingMsgStateField = Field(CookingStateMessageType, "m_cookingState");
        public static readonly FieldInfo ServerMixingDataField = Field(ServerMixingHandlerType, "m_serverData");
        public static readonly Type MixingStateMessageType = Find("MixingStateMessage");
        public static readonly FieldInfo MixingMsgProgressField = Field(MixingStateMessageType, "m_mixingProgress");
        public static readonly FieldInfo MixingMsgStateField = Field(MixingStateMessageType, "m_mixingState");

        // 状态变更回调（ServerCookingHandler 私有事件 / ServerMixingHandler 公有字段）
        public static readonly FieldInfo ServerCookingStateChangedField = Field(ServerCookingHandlerType, "m_cookingStateChangedCallback");
        public static readonly FieldInfo ServerMixingStateChangedField = Field(ServerMixingHandlerType, "m_stateChangedCallback");

        // 嵌套枚举（CookedCompositeOrderNode.CookingProgress / MixedCompositeOrderNode.MixingProgress /
        // CookingUIController.State）——按名取值，勿依赖枚举数值顺序以外的信息。
        public static readonly Type CookingProgressEnum = Safe(delegate
        {
            var owner = Find("CookedCompositeOrderNode");
            return owner != null ? owner.GetNestedType("CookingProgress") : null;
        });
        public static readonly Type MixingProgressEnum = Safe(delegate
        {
            var owner = Find("MixedCompositeOrderNode");
            return owner != null ? owner.GetNestedType("MixingProgress") : null;
        });
        public static readonly Type UIStateEnum = Safe(delegate
        {
            var owner = Find("CookingUIController");
            return owner != null ? owner.GetNestedType("State") : null;
        });

        // Harmony 目标：阈值判定与 UI 换算（vanilla 硬编码 煮糊=2×煮熟 / 过混=2×混合）
        public static readonly MethodInfo CookingHandlerGetCookedStateMethod = Safe(delegate
        {
            return CookingHandlerType != null
                ? CookingHandlerType.GetMethod("GetCookedOrderState", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(float) }, null)
                : null;
        });
        public static readonly MethodInfo MixingHandlerGetMixedStateMethod = Safe(delegate
        {
            return MixingHandlerType != null
                ? MixingHandlerType.GetMethod("GetMixedOrderState", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(float) }, null)
                : null;
        });
        public static readonly MethodInfo ServerMixingIsOverMixedMethod = Safe(delegate
        {
            return ServerMixingHandlerType != null
                ? ServerMixingHandlerType.GetMethod("IsOverMixed", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly MethodInfo ClientCookingIsBurningMethod = Safe(delegate
        {
            return ClientCookingHandlerType != null
                ? ClientCookingHandlerType.GetMethod("IsBurning", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly MethodInfo ClientMixingIsOverMixedMethod = Safe(delegate
        {
            return ClientMixingHandlerType != null
                ? ClientMixingHandlerType.GetMethod("IsOverMixed", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        public static readonly MethodInfo ServerCookingSetProgressMethod = Safe(delegate
        {
            return ServerCookingHandlerType != null
                ? ServerCookingHandlerType.GetMethod("SetCookingProgress", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(float) }, null)
                : null;
        });
        public static readonly MethodInfo ServerMixingSetProgressMethod = Safe(delegate
        {
            return ServerMixingHandlerType != null
                ? ServerMixingHandlerType.GetMethod("SetMixingProgress", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(float) }, null)
                : null;
        });
        public static readonly MethodInfo ClientCookingApplyUpdateMethod = Safe(delegate
        {
            var serialisable = Find("Team17.Online.Multiplayer.Messaging.Serialisable");
            return serialisable != null && ClientCookingHandlerType != null
                ? ClientCookingHandlerType.GetMethod("ApplyServerUpdate", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { serialisable }, null)
                : null;
        });
        public static readonly MethodInfo ClientMixingApplyUpdateMethod = Safe(delegate
        {
            var serialisable = Find("Team17.Online.Multiplayer.Messaging.Serialisable");
            return serialisable != null && ClientMixingHandlerType != null
                ? ClientMixingHandlerType.GetMethod("ApplyServerUpdate", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { serialisable }, null)
                : null;
        });

        // 客户端进度 UI（ClientCookingHandler.m_gui / ClientMixingHandler.m_progressUI）
        public static readonly FieldInfo ClientCookingGuiField = Field(ClientCookingHandlerType, "m_gui");
        public static readonly FieldInfo ClientMixingUiField = Field(ClientMixingHandlerType, "m_progressUI");
        public static readonly MethodInfo UiSetOverDoingMethod = Safe(delegate
        {
            var ui = Find("CookingUIController");
            return ui != null
                ? ui.GetMethod("SetOverDoingAmount", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(float) }, null)
                : null;
        });
        public static readonly MethodInfo ClampedRemapMethod = Safe(delegate
        {
            var mathUtils = Find("MathUtils");
            return mathUtils != null
                ? mathUtils.GetMethod("ClampedRemap", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(float), typeof(float), typeof(float), typeof(float), typeof(float) }, null)
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
        // 仅用于 v14 的「大锅实体注册自检」：这两个同步器是否挂上，直接对应
        // 「食材能不能丢进去」与「汤面会不会一开局就显示」（ClientContentsCosmetic
        // Decisions.StartSynchronising 的 m_contentsObject.SetActive(false) 是汤面
        // 唯一的隐藏点，没挂 = 开局就有汤）。
        public static readonly Type ClientCookableContainerType = Find("ClientCookableContainer");
        public static readonly Type ClientContentsCosmeticType = Find("ClientContentsCosmeticDecisions");
        public static readonly MethodInfo GetContentsMethod = Safe(delegate
        {
            return ClientIngredientContainerType != null
                ? ClientIngredientContainerType.GetMethod("GetContents", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });

        // ---- 火锅：锅内视觉残留兜底（2026-09-14） ----
        // WokCosmeticDecisions（漂浮食材模型）实例由 ClientCookableContainer 挂在锅视觉根下，
        // m_container 是其生成内容物的父节点（基类 MealCosmeticDecisions protected 字段）。
        // IClientOrderDefinition 用于沿父级找回所属锅（镜像 FindOrderDefinition 上溯）。
        public static readonly Type MealCosmeticType = Find("MealCosmeticDecisions");
        public static readonly FieldInfo MealContainerField = Field(MealCosmeticType, "m_container");
        public static readonly Type ClientOrderDefinitionType = Find("IClientOrderDefinition");
        // 注：容量字段统一走 IngredientCapacityField（声明于 IngredientContainer，见下方
        // 「锅具容量」段）。此处曾有一个 ContainerCapacityField，按
        // ServerIngredientContainer.BaseType 找 m_capacity——而 ServerIngredientContainer
        // 是 ServerSynchroniserBase 的子类、只【持有】IngredientContainer 引用
        // （ServerIngredientContainer.cs:41-45），基类名判断永远不成立 ⇒ 常年为 null
        // ⇒ HotPot.RelayoutContents 每次早退（汤面高度重摆从未生效）。已删除（2026-09-15）。

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
            // 具体类 PlayerIDProvider 上的公共方法（PlayerIDProvider.cs:40）。
            // 曾错写成反射 "IPlayerIDProvider" 接口——宿主根本没有这个接口
            //（PlayerControls.PlayerIDProvider 属性返回的就是具体类，PlayerControls.cs:392），
            // 于是常年为 null，PushableVoidFall 的本地玩家判定一直走兜底分支。
            var t = Find("PlayerIDProvider");
            return t != null
                ? t.GetMethod("IsLocallyControlled", BindingFlags.Public | BindingFlags.Instance,
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

        // ---- 自动步道定时反转（TravelatorReverser；vanilla 类型） ----
        public static readonly Type TravelatorType = Find("Travelator");

        // ---- 传送门单向（TeleportalExitOnly；全部 vanilla 类型） ----
        //  Teleportal.m_exitPortal 是方向的唯一来源（public 字段）；
        //  Server/ClientTeleportal 在 StartSynchronising 里把出口的 receivers
        //  缓存进私有 m_exitReceivers（ServerTeleportal.cs:60-63 / ClientTeleportal.cs:51-54），
        //  清 m_exitPortal 若晚于握手则必须同时清缓存，否则该门仍会发送。
        public static readonly Type TeleportalType = Find("Teleportal");
        public static readonly FieldInfo TeleportalExitPortalField = Field(TeleportalType, "m_exitPortal");
        public static readonly Type ServerTeleportalType = Find("ServerTeleportal");
        public static readonly FieldInfo ServerTeleportalExitReceiversField = Field(ServerTeleportalType, "m_exitReceivers");
        public static readonly Type ClientTeleportalType = Find("ClientTeleportal");
        public static readonly FieldInfo ClientTeleportalExitReceiversField = Field(ClientTeleportalType, "m_exitReceivers");

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
        public static readonly FieldInfo LookupCacheFlagField = Field(OrderToPrefabLookupType, "m_cachedAssembledOrderNodes");
        public static readonly MethodInfo LookupCacheMethod = Safe(delegate
        {
            return OrderToPrefabLookupType != null
                ? OrderToPrefabLookupType.GetMethod("CacheAssembledOrderNodes",
                    BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)
                : null;
        });

        // ---- 火锅：锅内漂浮食材视觉表（WokCosmeticDecisions.m_prefabLookups，
        //      许可表合并重建后把 extras 并入视觉表，否则额外食材进锅不显示。
        //      均为 vanilla 类型；m_prefabLookups 是 WokCosmeticDecisions 的
        //      private 嵌套 struct（PrefabLookups），装箱后读两个 lookup SO 引用） ----
        public static readonly FieldInfo CookableCosmeticsPrefabField = Field(CookableContainerType, "m_cosmeticsPrefab");
        public static readonly Type WokCosmeticType = Find("WokCosmeticDecisions");
        public static readonly FieldInfo WokPrefabLookupsField = Field(WokCosmeticType, "m_prefabLookups");
        public static readonly Type WokPrefabLookupsType = Safe(delegate
        {
            return WokCosmeticType != null
                ? WokCosmeticType.GetNestedType("PrefabLookups", BindingFlags.NonPublic)
                : null;
        });
        public static readonly FieldInfo WokRawLookupField = Field(WokPrefabLookupsType, "m_rawPrefabLookup");
        public static readonly FieldInfo WokCookedLookupField = Field(WokPrefabLookupsType, "m_cookedPrefabLookup");

        // ---- 可移动火锅：容量（IngredientContainer.m_capacity；装配时写，
        //      >0 才覆盖真实 prefab 原版值） ----
        public static readonly Type IngredientContainerType = Find("IngredientContainer");
        public static readonly FieldInfo IngredientCapacityField = Field(IngredientContainerType, "m_capacity");

        // ---- 锅上需要剥掉的游戏组件（可推动载具独占物理/交互） ----
        public static readonly Type InteractableType = Find("Interactable");
        public static readonly Type EditorGridSnapType = Find("EditorGridSnap");
        public static readonly Type AttachStationType = Find("AttachStation");

        // ---- 终端防线（未绑定可操控对象的降级终端防 NRE） ----
        public static readonly Type TerminalType = Find("Terminal");
        public static readonly FieldInfo TerminalPilotableField = Field(TerminalType, "m_pilotableObject");
        public static readonly Type ClientTerminalCosmeticType = Find("ClientTerminalCosmeticDecisions");
        public static readonly Type ForwardTriggerToTargetType = Find("ForwardTriggerToTarget");

        // ---- Harmony 目标：宿主 KillPlane ----
        public static readonly MethodInfo RespawnObjectAddedMethod = Safe(delegate
        {
            var t = Find("ServerRespawnCollider");
            return t != null
                ? t.GetMethod("ObjectAdded", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(GameObject) }, null)
                : null;
        });

        // ---- Harmony 目标：相机出发点偏移（CameraAuthoredOffset；vanilla 类型） ----
        public static readonly Type MultiplayerCameraType = Find("MultiplayerCamera");
        public static readonly MethodInfo MultiplayerCameraGetIdealLocationMethod = Safe(delegate
        {
            return MultiplayerCameraType != null
                ? MultiplayerCameraType.GetMethod("GetIdealLocation",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });

        // ---- Harmony 目标：触发区占用联机同步 + fallPad 清理（TriggerZoneOccupancySync；
        // 接替 Assembly-CSharp-Patch ServerTriggerZone/ClientTriggerZone 源码覆盖补丁。
        // 全部 vanilla 类型，游戏 AppDomain 均存在） ----
        public static readonly Type ServerTriggerZoneType = Find("ServerTriggerZone");
        public static readonly Type TriggerZoneType = Find("TriggerZone");
        public static readonly Type TriggerZoneMessageType = Find("TriggerZoneMessage");
        public static readonly Type ClientTriggerZoneType = Find("ClientTriggerZone");
        public static readonly Type ClientSynchroniserBaseType = Find("ClientSynchroniserBase");
        public static readonly FieldInfo ServerTriggerZoneTriggerField = Field(ServerTriggerZoneType, "m_triggerZone");
        public static readonly FieldInfo ServerTriggerZoneDataField = Field(ServerTriggerZoneType, "m_data");
        public static readonly FieldInfo ServerTriggerZoneCollidersField = Field(ServerTriggerZoneType, "m_collidersOccupying");
        public static readonly FieldInfo FallPadField = Field(TriggerZoneType, "m_fallPad");
        public static readonly FieldInfo TriggerZoneMsgOccupiedField = Field(TriggerZoneMessageType, "m_occupied");
        public static readonly FieldInfo ClientTriggerZoneOccupiedField = Field(ClientTriggerZoneType, "m_occupied");
        public static readonly MethodInfo TriggerZoneMsgInitialiseMethod = Safe(delegate
        {
            return TriggerZoneMessageType != null
                ? TriggerZoneMessageType.GetMethod("Initialise",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(bool) }, null)
                : null;
        });
        public static readonly MethodInfo SendServerEventMethod = Safe(delegate
        {
            var st = Find("ServerSynchroniserBase");
            var serialisable = Find("Serialisable");
            return st != null && serialisable != null
                ? st.GetMethod("SendServerEvent",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { serialisable }, null)
                : null;
        });
        public static readonly MethodInfo ServerTriggerZoneOnTriggerEnterMethod = Safe(delegate
        {
            return ServerTriggerZoneType != null
                ? ServerTriggerZoneType.GetMethod("OnTriggerEnter",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(Collider) }, null)
                : null;
        });
        public static readonly MethodInfo ServerTriggerZoneOnTriggerExitMethod = Safe(delegate
        {
            return ServerTriggerZoneType != null
                ? ServerTriggerZoneType.GetMethod("OnTriggerExit",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(Collider) }, null)
                : null;
        });
        public static readonly MethodInfo ServerTriggerZoneUpdateSynchronisingMethod = Safe(delegate
        {
            return ServerTriggerZoneType != null
                ? ServerTriggerZoneType.GetMethod("UpdateSynchronising",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                : null;
        });
        /// <summary>ClientSynchroniserBase.ApplyServerEvent(Serialisable)——vanilla
        /// ClientTriggerZone 不 override 该虚方法，补丁打在基类上+类型门控。</summary>
        public static readonly MethodInfo ClientApplyServerEventMethod = Safe(delegate
        {
            var serialisable = Find("Serialisable");
            return ClientSynchroniserBaseType != null && serialisable != null
                ? ClientSynchroniserBaseType.GetMethod("ApplyServerEvent",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { serialisable }, null)
                : null;
        });

        // ---- Harmony 目标：随机箱空气取出拦截（ServerPickupItemSpawner.HandlePickup，
        // 参数 (ICarrier, Vector2)——ICarrier 为宿主接口，编译期不可引用，仅按名取方法） ----
        public static readonly MethodInfo ServerPickupHandlePickupMethod = Safe(delegate
        {
            var t = Find("ServerPickupItemSpawner");
            return t != null
                ? t.GetMethod("HandlePickup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                : null;
        });
        // ============ 运行时辅助（不缓存，静态只读之外的部分） ============

        /// <summary>网络实体扫描+链接+StartSynchronising 是否全部完成。
        /// 反射失败（API 缺失）按 true 处理，保持旧行为兼容。</summary>
        private static bool s_syncInvokeFailLogged;

        internal static bool IsSynchronisationActive()
        {
            if (IsSyncActiveMethod == null)
                return true;
            try
            {
                return (bool)IsSyncActiveMethod.Invoke(null, null);
            }
            catch (Exception ex)
            {
                // 调用失败=「等同步」永不完成 → 随机箱/装配链全卡死，必须可见（只报一次）
                if (!s_syncInvokeFailLogged)
                {
                    s_syncInvokeFailLogged = true;
                    StubLog.LogWarn("[GameApi] IsSynchronisationActive 调用异常（按 false 处理，同步等待可能卡死）: " + ex.Message);
                }
                return false;
            }
        }

        /// <summary>本机是否服务端：ConnectionStatus.IsHost() || !IsInSession()。
        /// 反射失败按 true（单机场景为主）。</summary>
        private static bool s_isServerInvokeFailLogged;

        internal static bool IsServerMachine()
        {
            if (IsHostMethod == null || IsInSessionMethod == null)
                return true;
            try
            {
                return (bool)IsHostMethod.Invoke(null, null)
                    || !(bool)IsInSessionMethod.Invoke(null, null);
            }
            catch (Exception ex)
            {
                if (!s_isServerInvokeFailLogged)
                {
                    s_isServerInvokeFailLogged = true;
                    StubLog.LogWarn("[GameApi] IsServerMachine 调用异常（按 true=服务端处理）: " + ex.Message);
                }
                return true;
            }
        }

        /// <summary>本机角色（日志用）：主机 / 客机 / 单机。</summary>
        internal static string RoleLabel()
        {
            if (IsHostMethod == null || IsInSessionMethod == null)
                return "未知";
            try
            {
                if (!(bool)IsInSessionMethod.Invoke(null, null))
                    return "单机";
                return (bool)IsHostMethod.Invoke(null, null) ? "主机" : "客机";
            }
            catch (Exception)
            {
                return "未知";
            }
        }

        /// <summary>是否处于联机会话中（非单机）。反射失败按 false。</summary>
        internal static bool IsInSession()
        {
            if (IsInSessionMethod == null)
                return false;
            try
            {
                return (bool)IsInSessionMethod.Invoke(null, null);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---- 网络实体扫描窗口判定（v14） ----

        private static Component s_multiplayerController;

        /// <summary>场景里的 MultiplayerController 实例（缓存；Unity 假 null 自动失效后重找）。</summary>
        internal static Component GetMultiplayerController()
        {
            if (s_multiplayerController != null)
                return s_multiplayerController;
            if (MultiplayerControllerType == null)
                return null;
            s_multiplayerController = UnityEngine.Object.FindObjectOfType(MultiplayerControllerType) as Component;
            return s_multiplayerController;
        }

        private static bool s_scanActiveFailLogged;

        /// <summary>网络实体扫描/链接是否正在进行（= 正在分配实体 ID 的致命窗口）。
        /// 取 MultiplayerController.ScanActive 与 EntitySerialisationRegistry
        /// .s_bLinkingEntities 的【或】：前者覆盖整段扫描协程（更宽、更安全），
        /// 后者即便前者反射失败也能圈住真正分配 ID 的循环。
        /// 反射全失败时返回 false（保持旧行为：不阻塞装配）。</summary>
        internal static bool IsEntityScanActive()
        {
            try
            {
                if (EntityLinkingFlagField != null)
                {
                    var linking = EntityLinkingFlagField.GetValue(null);
                    if (linking is bool && (bool)linking)
                        return true;
                }
                if (ScanActiveProperty != null)
                {
                    var mc = GetMultiplayerController();
                    if (mc != null)
                    {
                        var active = ScanActiveProperty.GetValue(mc, null);
                        if (active is bool && (bool)active)
                            return true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!s_scanActiveFailLogged)
                {
                    s_scanActiveFailLogged = true;
                    StubLog.LogWarn("[GameApi] 实体扫描状态查询异常（按「未在扫描」处理）: " + ex.Message);
                }
            }
            return false;
        }

        /// <summary>对象是否已在实体注册表里（= 拿到了网络实体 ID 与同步组件）。
        /// 反射缺失时返回 true（不误报，诊断用途宁可漏报不可错报）。</summary>
        internal static bool HasEntityEntry(GameObject go)
        {
            if (go == null || EntityGetEntryMethod == null)
                return true;
            try
            {
                return EntityGetEntryMethod.Invoke(null, new object[] { go }) != null;
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>对象的网络实体 ID（0 = 未注册 / 反射缺失）。</summary>
        internal static uint GetEntityId(GameObject go)
        {
            if (go == null || EntityGetIdMethod == null)
                return 0u;
            try
            {
                var raw = EntityGetIdMethod.Invoke(null, new object[] { go });
                return raw is uint ? (uint)raw : 0u;
            }
            catch (Exception)
            {
                return 0u;
            }
        }

        /// <summary>实体注册表的 GameObject → entry 字典（诊断指纹用；失败返回 null）。</summary>
        internal static System.Collections.IDictionary GetEntitiesByGameObject()
        {
            if (EntitiesByGameObjectField == null)
                return null;
            try
            {
                return EntitiesByGameObjectField.GetValue(null) as System.Collections.IDictionary;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>反射自检汇总：枚举全部缓存字段，列出为 null 的（=反射目标在
        /// 游戏 AppDomain 不存在或签名不匹配）。由 EntryPoint.Install 末尾调一次——
        /// 「反射了游戏侧不存在的类型」类事故的直接证据。</summary>
        internal static void DumpReflectionSelfCheck()
        {
            try
            {
                var missing = new List<string>();
                var total = 0;
                foreach (var f in typeof(GameApi).GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    total++;
                    object v;
                    try
                    {
                        v = f.GetValue(null);
                    }
                    catch (Exception)
                    {
                        missing.Add(f.Name + "(读取异常)");
                        continue;
                    }
                    if (v == null)
                        missing.Add(f.Name);
                }
                if (missing.Count == 0)
                    StubLog.Dbg("[GameApi] 反射自检: 全部命中（" + total + " 项）");
                else
                    StubLog.LogWarn("[GameApi] 反射自检: " + total + " 项中 " + missing.Count
                        + " 项未命中: " + string.Join(", ", missing.ToArray()));
                if (s_fallbackResolved != null && s_fallbackResolved.Count > 0)
                    StubLog.LogWarn("[GameApi] 以下类型靠【简单名暴力扫描】兜底命中（请把命名空间补进 NamespaceCandidates）: "
                        + string.Join(", ", s_fallbackResolved.ToArray()));
                if (s_findFailures != null && s_findFailures.Count > 0)
                    StubLog.LogWarn("[GameApi] 类型解析过程中吞掉了异常（已按未命中处理）: "
                        + string.Join(", ", s_findFailures.ToArray()));
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[GameApi] 反射自检自身异常: " + ex.Message);
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

        /// <summary>GameObjectUtils.SendTrigger 等价（编译期不可引用扩展方法）。
        /// 异常加 once-flag（v12）：调用方 SwitchReenable 在轮询路径上，
        /// 持续性失败原本会逐次刷屏。</summary>
        private static bool s_sendTriggerFailLogged;

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
                if (!s_sendTriggerFailLogged)
                {
                    s_sendTriggerFailLogged = true;
                    StubLog.LogWarn("[GameApi] SendTrigger 失败 " + trigger + " → " + target.name + ": " + ex.Message);
                }
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

        /// <summary>命名空间候选表（2026-09-15 v14）：宿主把网络消息层类型放在
        /// Team17.Online.Multiplayer.Messaging 下，而本文件历来按【简单名】反射——
        /// ClientSynchroniserBase / ServerSynchroniserBase / Serialisable /
        /// EntitySerialisationRegistry 全部因此落空（真机自检常年报 5 项未命中，
        /// 直接后果是「触发区占用联机同步」的收发两端补丁从未真正装上）。
        /// 新增 type 时若简单名查不到，优先往这张表里补前缀，别依赖下面的暴力兜底。
        ///
        /// ⚠ 必须懒初始化（2026-09-15 实机事故）：Find 会被本类【静态字段初始化器】
        /// 调用，而静态字段严格按【文本顺序】初始化——本表声明在文件底部，
        /// 写成 `static readonly string[] X = {...}` 时，文件顶部那些 `= Find("…")`
        /// 执行时它还是 null → for 循环 NRE → 静态构造抛
        /// TypeInitializationException → GameApi 整个类永久不可用 → 全部 stub 功能
        /// 连同可移动火锅模型一起消失。凡是 Find 依赖的状态，一律懒初始化。</summary>
        private static string[] s_namespaceCandidates;

        private static string[] GetNamespaceCandidates()
        {
            if (s_namespaceCandidates == null)
                s_namespaceCandidates = new[]
                {
                    "Team17.Online.Multiplayer.Messaging.",
                    "Team17.Online.Multiplayer.",
                    "Team17.Online.",
                    "Team17.",
                    "BitStream.",
                };
            return s_namespaceCandidates;
        }

        /// <summary>类型解析缓存（含解析失败的 null，避免每次重跑暴力兜底）。
        /// 懒初始化：Find 会被本类【静态字段初始化器】调用，而静态字段按文本顺序
        /// 初始化——若写成字段初始化器且声明在后面，前面的 Find 调用会读到 null
        /// 字典并抛 NRE，被 Safe 吞掉后表现为「所有反射目标全 null」。</summary>
        private static Dictionary<string, Type> s_typeCache;

        /// <summary>走了「暴力按简单名扫描」兜底才找到的类型名（诊断用，由
        /// DumpReflectionSelfCheck 汇总上报——提醒把命名空间补进候选表）。
        /// 同理不能用字段初始化器（见 s_typeCache 注释）。</summary>
        private static List<string> s_fallbackResolved;

        internal static Type Find(string typeName)
        {
            // 绝不抛（2026-09-15 事故防线）：Find 跑在静态字段初始化器里，任何异常
            // 都会变成 TypeInitializationException 把整个 GameApi 永久毒化——
            // 届时所有 stub 组件全灭，且日志只剩一句看不出真因的类型初始化失败。
            // 宁可返回 null（该反射目标退化为「未命中」，自检会报出来）。
            try
            {
                if (string.IsNullOrEmpty(typeName))
                    return null;
                if (s_typeCache == null)
                    s_typeCache = new Dictionary<string, Type>();
                Type cached;
                if (s_typeCache.TryGetValue(typeName, out cached))
                    return cached;
                var resolved = FindUncached(typeName);
                s_typeCache[typeName] = resolved;
                return resolved;
            }
            catch (Exception ex)
            {
                RecordFindFailure(typeName, ex);
                return null;
            }
        }

        /// <summary>Find 内部异常记录（不能在静态初始化期打日志——StubLog 的桥接
        /// 反射可能尚未就绪；统一攒着由 DumpReflectionSelfCheck 报出）。</summary>
        private static List<string> s_findFailures;

        private static void RecordFindFailure(string typeName, Exception ex)
        {
            try
            {
                if (s_findFailures == null)
                    s_findFailures = new List<string>();
                if (s_findFailures.Count < 10)
                    s_findFailures.Add(typeName + "(" + ex.GetType().Name + ": " + ex.Message + ")");
            }
            catch (Exception)
            {
                // 记日志都失败就彻底放弃，绝不把异常传回静态初始化器
            }
        }

        private static Type FindUncached(string typeName)
        {
            // 1) 简单名 + Assembly-CSharp（绝大多数宿主类型走这条）
            var t = Type.GetType(typeName + ", Assembly-CSharp");
            if (t != null)
                return t;
            // 2) 简单名 + 全 AppDomain
            t = ProbeAllAssemblies(typeName);
            if (t != null)
                return t;
            // 3) 已知命名空间前缀重试
            var candidates = GetNamespaceCandidates();
            for (int i = 0; i < candidates.Length; i++)
            {
                var full = candidates[i] + typeName;
                t = Type.GetType(full + ", Assembly-CSharp");
                if (t != null)
                    return t;
                t = ProbeAllAssemblies(full);
                if (t != null)
                    return t;
            }
            // 4) 兜底：逐程序集按【简单名】暴力匹配（Assembly-CSharp 优先，避免
            //    与其它模组的同名类型撞车）。命中会记进 s_fallbackResolved 上报。
            t = ProbeBySimpleName(typeName);
            if (t != null)
            {
                if (s_fallbackResolved == null)
                    s_fallbackResolved = new List<string>();
                s_fallbackResolved.Add(typeName + "→" + t.FullName);
            }
            return t;
        }

        private static Type ProbeAllAssemblies(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm == null)
                    continue;
                Type t;
                try
                {
                    t = asm.GetType(fullName, false);
                }
                catch (Exception)
                {
                    continue; // 动态/残缺程序集的 GetType 可能抛异常，跳过
                }
                if (t != null)
                    return t;
            }
            return null;
        }

        /// <summary>简单名 → 类型 的全量索引（暴力兜底用，整个会话只建一次）。
        /// 不建索引而每次重扫的话，每个未命中的名字都要把所有程序集 GetTypes()
        /// 跑一遍（Assembly-CSharp 上万类型），静态初始化期会被拖慢一大截。</summary>
        private static Dictionary<string, Type> s_simpleNameIndex;

        private static Type ProbeBySimpleName(string simpleName)
        {
            if (s_simpleNameIndex == null)
                s_simpleNameIndex = BuildSimpleNameIndex();
            Type t;
            return s_simpleNameIndex.TryGetValue(simpleName, out t) ? t : null;
        }

        private static Dictionary<string, Type> BuildSimpleNameIndex()
        {
            var index = new Dictionary<string, Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            // 两轮：先 Assembly-CSharp（宿主优先，避免与其它模组的同名类型撞车），
            // 再其余程序集；先到的不被覆盖。
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var asm in assemblies)
                {
                    if (asm == null)
                        continue;
                    bool isHost;
                    try
                    {
                        isHost = asm.GetName().Name == "Assembly-CSharp";
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if ((pass == 0) != isHost)
                        continue;
                    Type[] types;
                    try
                    {
                        types = asm.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types; // 部分可用，含 null 项
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (types == null)
                        continue;
                    for (int i = 0; i < types.Length; i++)
                    {
                        var t = types[i];
                        if (t == null || index.ContainsKey(t.Name))
                            continue;
                        index[t.Name] = t;
                    }
                }
            }
            return index;
        }

        internal static FieldInfo Field(Type type, string fieldName)
        {
            // 同 Find：跑在静态初始化器里，绝不抛
            try
            {
                return type != null
                    ? type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    : null;
            }
            catch (Exception ex)
            {
                RecordFindFailure((type != null ? type.Name : "<null>") + "." + fieldName, ex);
                return null;
            }
        }

        internal static PropertyInfo Prop(Type type, string propName)
        {
            try
            {
                return type != null
                    ? type.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    : null;
            }
            catch (Exception ex)
            {
                RecordFindFailure((type != null ? type.Name : "<null>") + "." + propName, ex);
                return null;
            }
        }
    }
}
