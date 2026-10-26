// 分料机（IngredientSplitter）真机运行时模块。
// 这个文件不在 Unity 工程内编译（上级目录带 ~），由 IngredientSplitterRuntime.csproj
// 单独编译成 DLL，随 webcustomstub_runtime bundle 分发，Loader 在 WebCustomStubRuntime
// 之前加载。它直接引用游戏 Assembly-CSharp / LevelEditorStub / IngredientSplitterStub，
// 因此不能放进 WebCustomStubRuntime.asmdef（asmdef 不能引用 Assembly-CSharp）。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BitStream;
using LevelEditorStub;
using OC2DIYLevel.Modules.IngredientSplitterMod;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;
using MessageType = Team17.Online.Multiplayer.Messaging.MessageType;

namespace IngredientSplitterRuntime
{
    public class IngredientSplitterMessage : Serialisable
    {
        public int ingredientCnt;
        public SplitMessageType messageType;
        private const int ingredientCntBits = 6;
        private const int messageTypeBits = 1;

        public enum SplitMessageType
        {
            Add,
            Take,
        }

        public void Serialise(BitStreamWriter writer)
        {
            writer.Write((uint)messageType, messageTypeBits);
            writer.Write((uint)ingredientCnt, ingredientCntBits);
        }

        public bool Deserialise(BitStreamReader reader)
        {
            messageType = (SplitMessageType)reader.ReadUInt32(messageTypeBits);
            ingredientCnt = (int)reader.ReadUInt32(ingredientCntBits);
            return true;
        }
    }

    internal static class RuntimeRecipeNodes
    {
        internal static OrderDefinitionNode GetOrderDefinitionNode(ScriptableObject so)
        {
            if (so == null)
                return null;
            var pseudo = so as PseudoPrefabSO;
            if (pseudo != null)
                return LoadOrderNode(pseudo);
            var custom = so as CustomRecipeSO;
            if (custom == null)
                return null;
            if (custom.compositionSOs != null && custom.compositionSOs.Length > 0)
            {
                var first = custom.compositionSOs[0] as PseudoPrefabSO;
                if (first != null)
                    return LoadOrderNode(first);
            }
            return null;
        }

        internal static OrderDefinitionNode LoadOrderNode(PseudoPrefabSO so)
        {
            if (so == null)
                return null;
            try
            {
                var assetField = so.GetType().GetField("asset",
                    BindingFlags.Public | BindingFlags.Instance);
                var direct = assetField != null ? assetField.GetValue(so) : null;
                var node = direct as OrderDefinitionNode;
                if (node != null)
                    return node;
                if (string.IsNullOrEmpty(so.bundleName) || string.IsNullOrEmpty(so.assetPath))
                    return null;
                var bundles = AssetBundle.GetAllLoadedAssetBundles();
                foreach (var b in bundles)
                {
                    if (b == null || !string.Equals(b.name, so.bundleName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    return b.LoadAsset(so.assetPath, typeof(OrderDefinitionNode)) as OrderDefinitionNode;
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>与 LoadOrderNode 同款加载路径：优先 SO 直引，其次按 bundleName/assetPath
        /// 从已加载 bundle 取 GameObject（分料机 cookingUISO 指向 CookingUI.prefab）。</summary>
        internal static GameObject LoadPrefab(PseudoPrefabSO so)
        {
            if (so == null)
                return null;
            try
            {
                var assetField = so.GetType().GetField("asset",
                    BindingFlags.Public | BindingFlags.Instance);
                var direct = assetField != null ? assetField.GetValue(so) as GameObject : null;
                if (direct != null)
                    return direct;
                if (string.IsNullOrEmpty(so.bundleName) || string.IsNullOrEmpty(so.assetPath))
                    return null;
                var bundles = AssetBundle.GetAllLoadedAssetBundles();
                foreach (var b in bundles)
                {
                    if (b == null || !string.Equals(b.name, so.bundleName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    return b.LoadAsset(so.assetPath, typeof(GameObject)) as GameObject;
                }
            }
            catch
            {
            }
            return null;
        }

        internal static AssembledDefinitionNode OutputNode(GameObject prefab)
        {
            if (prefab == null)
                return null;
            var def = prefab.GetComponent<IOrderDefinition>();
            if (def == null)
                return null;
            return def.GetOrderComposition();
        }
    }

    public class ClientIngredientSplitter : ClientSynchroniserBase, IClientHandlePickup, IClientHandlePlacement
    {
        private IngredientSplitter splitter;
        private AssembledDefinitionNode inputAssembledNode;
        private AssembledDefinitionNode outputAssembledNode;
        private int ingredientCnt;
        private IngredientSplitterMessage message = new IngredientSplitterMessage();
        private CookingUIController cntUI;
        private bool counterUIWarned;

        public override EntityType GetEntityType()
        {
            return (EntityType)200;
        }

        public override void StartSynchronising(Component synchronisedObject)
        {
            base.StartSynchronising(synchronisedObject);
            splitter = (IngredientSplitter)synchronisedObject;
            NetworkUtils.RegisterSpawnablePrefab(gameObject, splitter.outputPrefab);
            var inputNode = RuntimeRecipeNodes.GetOrderDefinitionNode(splitter.inputRecipe);
            inputAssembledNode = inputNode != null ? inputNode.Simpilfy() : null;
            var outputNode = RuntimeRecipeNodes.OutputNode(splitter.outputPrefab);
            outputAssembledNode = outputNode != null ? outputNode.Simpilfy() : null;
            ingredientCnt = 0;
            EnsureCounterUI();
            UpdateVisuals();
        }

        /// <summary>上游进度条：cookingUISO 未配置/资源加载失败时静默跳过，绝不影响分料机主逻辑。</summary>
        private void EnsureCounterUI()
        {
            if (splitter == null || splitter.cookingUISO == null)
                return;

            var uiPrefab = RuntimeRecipeNodes.LoadPrefab(splitter.cookingUISO);
            if (uiPrefab == null)
            {
                if (!counterUIWarned)
                {
                    counterUIWarned = true;
                    Debug.LogWarning("[IngredientSplitter] cookingUISO 资源加载失败，跳过进度条: "
                        + splitter.cookingUISO.bundleName + ":" + splitter.cookingUISO.assetPath);
                }
                return;
            }

            GameObject ui = null;
            try
            {
                ui = GameUtils.InstantiateUIController(uiPrefab, "HoverIconCanvas");
            }
            catch (Exception e)
            {
                if (!counterUIWarned)
                {
                    counterUIWarned = true;
                    Debug.LogWarning("[IngredientSplitter] 进度条实例化失败: " + e.Message);
                }
                return;
            }
            if (ui == null)
                return;

            cntUI = ui.GetComponent<CookingUIController>();
            if (cntUI == null)
            {
                UnityEngine.Object.Destroy(ui);
                if (!counterUIWarned)
                {
                    counterUIWarned = true;
                    Debug.LogWarning("[IngredientSplitter] CookingUI prefab 缺少 CookingUIController，已跳过");
                }
                return;
            }

            var visualRoot = NetworkUtils.FindVisualRoot(gameObject);
            cntUI.SetFollowTransform(visualRoot != null ? visualRoot : transform, Vector3.zero);
            cntUI.SetFollowOffset(splitter.uiPrefabOffset);
            cntUI.SetState(CookingUIController.State.Progressing);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (cntUI != null)
                UnityEngine.Object.Destroy(cntUI.gameObject);
        }

        public override void ApplyServerEvent(Serialisable serialisable)
        {
            var msg = (IngredientSplitterMessage)serialisable;
            ingredientCnt = msg.ingredientCnt;
            UpdateVisuals();
            if (msg.messageType == IngredientSplitterMessage.SplitMessageType.Add)
            {
                var tag = (GameOneShotAudioTag)splitter.splitAudioTag;
                GameUtils.TriggerAudio(tag, gameObject.layer);
            }
        }

        public bool CanHandlePickup(ICarrier _carrier)
        {
            return ingredientCnt > 0;
        }

        public int GetPickupPriority()
        {
            return 1;
        }

        public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
        {
            GameObject carried = _carrier.InspectCarriedItem();
            if (MatchInput(carried))
                return ingredientCnt + splitter.splitNum <= splitter.capacity;
            if (MatchOutput(carried))
                return ingredientCnt < splitter.capacity;
            return false;
        }

        public bool MatchOutput(GameObject carried)
        {
            if (carried == null || outputAssembledNode == null)
                return false;
            var def = carried.GetComponent<IClientOrderDefinition>();
            if (def == null)
                return false;
            var assembled = def.GetOrderComposition().Simpilfy();
            if (AssembledDefinitionNode.MatchingAlreadySimple(assembled, outputAssembledNode))
            {
                var ingredientContainer = carried.GetComponent<IngredientContainer>();
                var prep = carried.GetComponent<PreparationContainer>();
                if (ingredientContainer != null && prep == null)
                    return false;
                bool flag1 = carried.GetComponent<WorkableItem>() != null;
                bool flag2 = splitter.outputPrefab.GetComponent<WorkableItem>() != null;
                return flag1 == flag2;
            }
            return false;
        }

        public bool MatchInput(GameObject carried)
        {
            if (carried == null || inputAssembledNode == null)
                return false;
            var def = carried.GetComponent<IClientOrderDefinition>();
            if (def == null)
                return false;
            return AssembledDefinitionNode.MatchingAlreadySimple(def.GetOrderComposition().Simpilfy(), inputAssembledNode);
        }

        public int GetPlacementPriority()
        {
            return 1;
        }

        private void UpdateVisuals()
        {
            if (splitter.ingredientModels != null && splitter.ingredientModels.Length > 0)
            {
                foreach (var model in splitter.ingredientModels)
                    if (model != null) model.gameObject.SetActive(false);
                int n = splitter.ingredientModels.Length;
                if (ingredientCnt >= splitter.capacity)
                    splitter.ingredientModels[n - 1].gameObject.SetActive(true);
                else if (ingredientCnt > 0)
                {
                    int k = Mathf.CeilToInt(MathUtils.ClampedRemap(ingredientCnt, 0, splitter.capacity - 1, -1, n - 2) - 0.001f);
                    if (k >= 0)
                        splitter.ingredientModels[k].gameObject.SetActive(true);
                }
            }

            if (cntUI != null)
            {
                cntUI.gameObject.SetActive(gameObject.activeInHierarchy);
                if (splitter.capacity > 0)
                    cntUI.SetProgress((float)ingredientCnt / splitter.capacity);
            }
        }

    }

    public class ServerIngredientSplitter : ServerSynchroniserBase, IHandlePlacement, IHandlePickup
    {
        private IngredientSplitter splitter;
        private AssembledDefinitionNode inputAssembledNode;
        private AssembledDefinitionNode outputAssembledNode;
        private int ingredientCnt;
        private IngredientSplitterMessage message = new IngredientSplitterMessage();

        public override EntityType GetEntityType()
        {
            return (EntityType)200;
        }

        public override void StartSynchronising(Component synchronisedObject)
        {
            base.StartSynchronising(synchronisedObject);
            splitter = (IngredientSplitter)synchronisedObject;
            NetworkUtils.RegisterSpawnablePrefab(gameObject, splitter.outputPrefab);
            var inputNode = RuntimeRecipeNodes.GetOrderDefinitionNode(splitter.inputRecipe);
            inputAssembledNode = inputNode != null ? inputNode.Simpilfy() : null;
            var outputNode = RuntimeRecipeNodes.OutputNode(splitter.outputPrefab);
            outputAssembledNode = outputNode != null ? outputNode.Simpilfy() : null;
            ingredientCnt = 0;
        }

        public bool CanHandlePickup(ICarrier _carrier)
        {
            return ingredientCnt > 0;
        }

        public void HandlePickup(ICarrier _carrier, Vector2 _directionXZ)
        {
            Vector3 position = gameObject.transform.position;
            var parentable = _carrier.AccessGameObject().RequestInterface<IParentable>();
            if (parentable as MonoBehaviour != null)
                position = parentable.GetAttachPoint(splitter.outputPrefab).position;
            GameObject output = NetworkUtils.ServerSpawnPrefab(gameObject, splitter.outputPrefab, position, Quaternion.identity);
            _carrier.CarryItem(output);
            ingredientCnt--;
            SendPickup();
        }

        public int GetPickupPriority()
        {
            return 1;
        }

        public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
        {
            GameObject carried = _carrier.InspectCarriedItem();
            if (MatchInput(carried))
                return ingredientCnt + splitter.splitNum <= splitter.capacity;
            if (MatchOutput(carried))
                return ingredientCnt < splitter.capacity;
            return false;
        }

        public bool MatchOutput(GameObject carried)
        {
            if (carried == null || outputAssembledNode == null)
                return false;
            var def = carried.GetComponent<IOrderDefinition>();
            if (def == null)
                return false;
            var assembled = def.GetOrderComposition().Simpilfy();
            if (AssembledDefinitionNode.MatchingAlreadySimple(assembled, outputAssembledNode))
            {
                var ingredientContainer = carried.GetComponent<IngredientContainer>();
                var prep = carried.GetComponent<PreparationContainer>();
                if (ingredientContainer != null && prep == null)
                    return false;
                bool flag1 = carried.GetComponent<WorkableItem>() != null;
                bool flag2 = splitter.outputPrefab.GetComponent<WorkableItem>() != null;
                return flag1 == flag2;
            }
            return false;
        }

        public bool MatchInput(GameObject carried)
        {
            if (carried == null || inputAssembledNode == null)
                return false;
            var def = carried.GetComponent<IOrderDefinition>();
            if (def == null)
                return false;
            return AssembledDefinitionNode.MatchingAlreadySimple(def.GetOrderComposition().Simpilfy(), inputAssembledNode);
        }

        public void HandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
        {
            GameObject carried = _carrier.InspectCarriedItem();
            if (MatchOutput(carried))
            {
                GameObject go = _carrier.TakeItem();
                NetworkUtils.DestroyObject(go);
                ingredientCnt += 1;
                SendPlacement();
            }
            else if (MatchInput(carried))
            {
                var serverIngredient = carried.GetComponent<ServerIngredientContainer>();
                var serverPrep = carried.GetComponent<ServerPreparationContainer>();
                if (serverIngredient != null && serverPrep == null)
                    serverIngredient.Empty();
                else
                {
                    GameObject go = _carrier.TakeItem();
                    NetworkUtils.DestroyObject(go);
                }
                ingredientCnt += splitter.splitNum;
                SendPlacement();
            }
        }

        public void OnFailedToPlace(GameObject _item)
        {
        }

        public int GetPlacementPriority()
        {
            return 1;
        }

        private void SendPickup()
        {
            message.messageType = IngredientSplitterMessage.SplitMessageType.Take;
            message.ingredientCnt = ingredientCnt;
            SendServerEvent(message);
        }

        private void SendPlacement()
        {
            message.messageType = IngredientSplitterMessage.SplitMessageType.Add;
            message.ingredientCnt = ingredientCnt;
            SendServerEvent(message);
        }
    }

    /// <summary>Loader/EntryPoint 反射调用的引导：注册同步类型 + 消息类型。</summary>
    public static class Bootstrap
    {
        private static bool s_done;
        private static bool s_messageRegistered;

        public static void Install()
        {
            if (s_done)
                return;
            s_done = true;
            try
            {
                var mcType = typeof(MultiplayerController);
                var field = mcType.GetField("m_EntitySerialiser",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                    return;
                var patchType = typeof(Bootstrap);
                var prefix = patchType.GetMethod("ScanEntitiesPrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var target = mcType.GetMethod("ScanEntities",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (target != null && prefix != null)
                {
                    var harmony = new HarmonyLib.Harmony("oc2.customstub.ingredientsplitter");
                    harmony.Patch(target, new HarmonyLib.HarmonyMethod(prefix));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[IngredientSplitter] 运行时模块安装失败: " + ex.Message);
            }
        }

        private static void ScanEntitiesPrefix(object __instance)
        {
            try
            {
                if (__instance == null)
                    return;
                // 零介入：本场景没有分料机时直接放行，不注册类型/消息。
                if (UnityEngine.Object.FindObjectsOfType<IngredientSplitter>().Length == 0)
                    return;
                if (!s_messageRegistered)
                {
                    var reg = typeof(SerialisationRegistry<EntityType>).GetMethod(
                        "RegisterMessageType", BindingFlags.Public | BindingFlags.Static);
                    if (reg != null)
                    {
                        reg.Invoke(null, new object[] { (EntityType)200, new IngredientSplitterMessage() });
                        s_messageRegistered = true;
                    }
                }
                var field = __instance.GetType().GetField("m_EntitySerialiser",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var registry = field != null ? field.GetValue(__instance) : null;
                if (registry == null)
                    return;
                var add = registry.GetType().GetMethod("AddSynchronisedType",
                    new[] { typeof(Type), typeof(SynchroniserConfig) });
                if (add == null)
                    return;
                var cfg = new SynchroniserConfig(InstancesPerGameObject.Single,
                    typeof(ServerIngredientSplitter), typeof(ClientIngredientSplitter));
                add.Invoke(registry, new object[] { typeof(IngredientSplitter), cfg });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[IngredientSplitter] 扫描前注册失败: " + ex.Message);
            }
        }
    }
}
