using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using GameModes.Horde;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// This transaction deliberately operates on an already-open, clean scene. It never
// instantiates an environment or opens a template scene (ExecuteInEditMode is unsafe there).
public static class LayoutEditorLevelModeChange
{
    private sealed class Template
    {
        public LevelInfoSO info;
        public FlowControllerBase flow;
        public GameObject root;
        public RecipeFlowGUI recipeUI;
        public PseudoPrefabSO config, recipe, hordeUI, waveUI;
    }

    private sealed class TransformState
    {
        public Transform parent;
        public Vector3 position, scale;
        public Quaternion rotation;
    }

    private sealed class StationConversion
    {
        public GameObject gameObject;
        public PseudoPrefabServingStationStub stub;
        public LevelEditor.PseudoPrefab runtime;
        public PseudoPrefabPlateReturnStub plateReturn;
        public PseudoPrefabPlateReturnStub[] plateReturns;
        public SpecificPseudoPrefabTag tag;
        public PseudoPrefabSO targetSO;
        public GameObject targetPrefab;
        public string name;
        public bool active;
        public bool horde;
    }

    public static string ChangeLevelMode(LevelModeChangeDto dto)
    {
        var block = LayoutEditorSafety.PlayModeWriteBlockReason();
        if (!string.IsNullOrEmpty(block)) return block;
        if (LayoutEditorPseudoReload.ExportBuildInProgress || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return "编译、导入或导出进行中，不能切换模式。";
        if (dto == null || string.IsNullOrEmpty(dto.assetPath)
            || (dto.gameMode != "Campaign" && dto.gameMode != "Dynamic" && dto.gameMode != "Horde"))
            return "缺少 LevelInfo 路径或模式（Campaign/Dynamic/Horde）。";
        var info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(dto.assetPath);
        var error = LayoutEditorLevelModeStore.ValidateLevelPath(dto.assetPath, info);
        if (error != null) return error;
        var parts = dto.assetPath.Split('/');
        if (string.IsNullOrEmpty(info.sceneName) || info.sceneName.IndexOfAny(new[] { '/', '\\', ':', '.' }) >= 0)
            return "LevelInfo.sceneName 无效。";
        var scenePath = "Assets/LevelSets/" + parts[2] + "/scenes/" + info.sceneName + ".unity";
        var scene = SceneManager.GetSceneByPath(scenePath);
        if (!scene.IsValid() || !scene.isLoaded || SceneManager.GetActiveScene() != scene)
            return "请先打开并激活目标关卡场景，再切换模式。";
        for (int s = 0; s < SceneManager.sceneCount; s++)
            if (SceneManager.GetSceneAt(s).isDirty) return "请先保存所有已打开场景，再切换模式。";

        LevelInfoSO backup = null;
        byte[] sceneBytes = null, infoBytes = null, sidecarBytes = null;
        string sidecarPath = null;
        var group = -1;
        var diskTouched = false;
        var committed = false;
        var previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            ValidateTargetOrderSequence(dto.assetPath, info, dto.gameMode);
            RejectDirtyAsset(info);
            RejectLinkPath(scenePath);
            if (dto.gameMode != "Campaign") RejectPartLevel(dto.assetPath, scenePath);
            var manager = FindManager(scene, info);
            var host = manager.GetComponent<LevelEditor.PseudoPrefabManager>();
            if (host == null || host.stub != manager) throw new InvalidDataException("场景 Manager 缺少有效原生宿主绑定。");
            var oldFlow = GetFlow(manager, scene);
            // Native DeInit destroys every pseudo wrapper child. Reject authored children
            // before that first Prepare, not after the evidence has already disappeared.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var pseudo in root.GetComponentsInChildren<LevelEditor.PseudoPrefab>(true))
                    for (int child = 0; child < pseudo.transform.childCount; child++)
                    {
                        var go = pseudo.transform.GetChild(child).gameObject;
                        if (go != pseudo.childGameObject || EditorUtility.IsPersistent(go))
                            throw new InvalidDataException("伪预制体包含非临时子物件，切换前请先将其移到独立父节点：" + pseudo.name);
                    }
            if (!oldFlow.enabled || !oldFlow.gameObject.activeInHierarchy)
                throw new InvalidDataException("FlowManager 控制器被禁用，不能安全迁移运行状态。");
            if (oldFlow.GetType() != FlowType(info.levelType.ToString()))
                throw new InvalidDataException("原生控制器与 LevelInfo 模式不一致，拒绝迁移。");
            var debuggers = FindDebuggers(scene, oldFlow);
            ValidateReferencesToFlow(scene, oldFlow, debuggers);
            ValidateManager(manager, scene);
            var stationConversions = new List<StationConversion>();
            if (dto.convertHordeObjects && info.levelType.ToString() != dto.gameMode
                && (info.levelType == LevelInfoSO.LevelType.Horde || dto.gameMode == "Horde"))
                stationConversions = PlanStationConversions(scene, dto.gameMode);
            else ValidateHordeObjects(scene, dto.gameMode);
            // DeInit is global in this upstream host; other pseudo scenes must be closed first.
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var other = SceneManager.GetSceneAt(s);
                if (other == scene || !other.isLoaded) continue;
                foreach (var root in other.GetRootGameObjects())
                    if (root.GetComponentInChildren<LevelEditor.PseudoPrefabManager>(true) != null)
                        throw new InvalidDataException("请先关闭其他伪预制体场景：" + other.path);
            }
            if (info.levelType.ToString() == dto.gameMode)
                return LayoutEditorNativeModeValidation.ValidateScene(scene, info);
            ValidateFlowReplacementDependencies(oldFlow, FlowType(dto.gameMode));
            var template = ReadTemplate(dto.gameMode);
            var mode = BuildModeData(dto.assetPath, info, template, dto.gameMode);
            foreach (var player in new[] { info.config_1p, info.config_2p, info.config_3p, info.config_4p })
                if (player == null) throw new InvalidDataException("缺少原生 1P–4P 人数配置，不能切换模式。");
            ValidateBundles(template);
            var transitions = dto.gameMode == "Dynamic" ? FindTransitions(manager, mode.phases.Length - 1) : null;
            var existingRecipeUI = FindRecipeUI(manager, scene);
            var recipeUI = dto.gameMode == "Horde" ? null : existingRecipeUI;
            Transform uiParent = null;
            if (dto.gameMode != "Horde" && recipeUI == null)
                uiParent = FindTemplateParent(template, manager.FlowManagerGO.transform.root);
            ValidateTemplateProperties(template.flow);
            foreach (var player in new[] { info.config_1p, info.config_2p, info.config_3p, info.config_4p })
                RejectDirtyAsset(player);
            sceneBytes = File.ReadAllBytes(Absolute(scenePath));
            infoBytes = File.ReadAllBytes(Absolute(dto.assetPath));
            sidecarPath = LayoutEditorLevelModeStore.ModeSidecarPath(dto.assetPath);
            if (File.Exists(sidecarPath) && new FileInfo(sidecarPath).Length > LayoutEditorLevelModeStore.MaxJsonBytes)
                throw new InvalidDataException("模式 sidecar 超过大小上限，不能安全捕获。");
            sidecarBytes = File.Exists(sidecarPath) ? File.ReadAllBytes(sidecarPath) : null;
            backup = UnityEngine.Object.Instantiate(info);
            backup.name = info.name;
            Undo.IncrementCurrentGroup();
            group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Change native level mode");
            Undo.RegisterCompleteObjectUndo(info, "Change level mode");
            Undo.RegisterCompleteObjectUndo(manager, "Change mode bindings");

            // Asset import callbacks can flush a dirty LevelInfo during staging. Treat all
            // mutations as potentially persisted, not only our final SaveAssets/SaveScene calls.
            diskTouched = true;
            LayoutEditorPseudoReload.PrepareSceneForWrite(scene, true);
            // Capture authored objects after DeInit removes transient pseudo children.
            var oldObjects = CollectObjects(scene);
            var oldCameras = CaptureCameras(scene);
            var oldTransforms = CaptureTransforms(scene);
            var managerReferences = CaptureManagerReferences(manager);
            ApplyStationConversions(dto.assetPath, stationConversions);
            ValidateHordeObjects(scene, dto.gameMode);
            var sourceRecipes = LayoutEditorLevelModeStore.CollectAllRecipes(info);
            info.levelType = (LevelInfoSO.LevelType)Enum.Parse(typeof(LevelInfoSO.LevelType), dto.gameMode);
            info.disableDynamicParenting = template.info.disableDynamicParenting;
            if (dto.gameMode == "Horde")
            {
                if (existingRecipeUI != null)
                {
                    Undo.RecordObject(existingRecipeUI.gameObject, "Hide campaign recipe UI");
                    existingRecipeUI.gameObject.SetActive(false);
                }
                manager.RecipeUIGO = null;
            }
            else
            {
                if (recipeUI == null)
                {
                    var ui = UnityEngine.Object.Instantiate(template.recipeUI.gameObject);
                    Undo.RegisterCreatedObjectUndo(ui, "Create recipe UI");
                    ui.name = template.recipeUI.gameObject.name;
                    ui.transform.SetParent(uiParent, false);
                    recipeUI = ui.GetComponent<RecipeFlowGUI>();
                    RejectExternalPrefabReferences(ui);
                }
                Undo.RecordObject(recipeUI.gameObject, "Enable recipe UI");
                recipeUI.gameObject.SetActive(true);
                manager.RecipeUIGO = recipeUI.gameObject;
            }
            if (dto.gameMode == "Campaign")
            {
                ClonePlayers(dto.assetPath, info);
                if (info.recipes == null || info.recipes.Length == 0) info.recipes = sourceRecipes;
                info.debugRecipeCount = 0;
            }
            else if (dto.gameMode == "Dynamic") ClonePlayers(dto.assetPath, info);
            var next = (FlowControllerBase)Undo.AddComponent(manager.FlowManagerGO, FlowType(dto.gameMode));
            if (next == null || next == oldFlow || next.GetType() != FlowType(dto.gameMode))
                throw new InvalidDataException("添加目标 FlowController 失败，可能存在组件互斥约束。");
            CopyFlow(template.flow, next, recipeUI, transitions, info.maxOrderCount);
            CloneGameConfig(dto.assetPath, next);
            // Every serialized reference to the removed component was preflighted above.
            foreach (var debug in debuggers)
            {
                Undo.RecordObject(debug, "Rebind Debug.flowController");
                var serialized = new SerializedObject(debug);
                Require(serialized, "flowController").objectReferenceValue = next;
                serialized.ApplyModifiedProperties();
            }
            Undo.DestroyObjectImmediate(oldFlow);
            if (oldFlow != null || GetFlow(manager, scene) != next)
                throw new InvalidDataException("旧 FlowController 未能移除，可能仍被 RequireComponent 依赖。");
            manager.configTemplateSO = template.config;
            manager.RecipeUISO = template.recipe;
            manager.hordeModeUISO = template.hordeUI;
            manager.hordeWaveNumberUISO = template.waveUI;
            if (mode != null) LayoutEditorLevelModeStore.StageModeChange(dto.assetPath, info, mode);
            MergeModeDependencies(template, info);
            var dependencies = new List<string>(info.dependencies ?? new string[0]);
            foreach (var conversion in stationConversions)
                if (!string.IsNullOrEmpty(conversion.targetSO.bundleName) && !dependencies.Contains(conversion.targetSO.bundleName))
                    dependencies.Add(conversion.targetSO.bundleName);
            info.dependencies = LayoutEditorNativeDependencies.Filter(dependencies);
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            foreach (var debug in debuggers) PrefabUtility.RecordPrefabInstancePropertyModifications(debug);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(info);
            // Host prepare clears runtime-prefab fields; validate again after it, before any save.
            LayoutEditorPseudoReload.PrepareSceneForWrite(scene, true);
            if (mode != null) LayoutEditorLevelModeStore.ValidateStagedModeChange(dto.assetPath, info, mode);
            ValidateStationConversions(stationConversions);
            ValidateChanged(scene, manager, next, debuggers, info, oldObjects, oldFlow, oldCameras);
            var sceneError = LayoutEditorNativeModeValidation.ValidateScene(scene, info);
            if (sceneError != null) throw new InvalidDataException(sceneError);
            var managerSerialized = new SerializedObject(manager);
            foreach (var pair in managerReferences)
                if (Require(managerSerialized, pair.Key).objectReferenceValue != pair.Value)
                    throw new InvalidDataException("迁移意外修改了 Manager 引用：" + pair.Key);
            foreach (var pair in oldTransforms)
                if (pair.Key == null || pair.Key.parent != pair.Value.parent || pair.Key.localPosition != pair.Value.position
                    || pair.Key.localRotation != pair.Value.rotation || pair.Key.localScale != pair.Value.scale)
                    throw new InvalidDataException("迁移意外修改了原场景物件的层级或变换。");
            if (info.enableRespawnBounds != backup.enableRespawnBounds)
                throw new InvalidDataException("迁移意外修改了用户的重生边界设置。");
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("保存切换后的场景失败。");
            if (dto.gameMode != "Campaign") LayoutEditorLevelModeStore.CommitModeSidecar(dto.assetPath, mode);
            Undo.CollapseUndoOperations(group);
            committed = true;
            return null;
        }
        catch (Exception ex)
        {
            var failures = new List<string>();
            // Undo failure must not prevent disk restoration, and a disk error must not prevent
            // restoring the other snapshots. Report each failed recovery step independently.
            try { if (group >= 0) Undo.RevertAllDownToGroup(group); }
            catch (Exception restore) { failures.Add("Undo：" + restore.Message); }
            try { if (backup != null) EditorUtility.CopySerialized(backup, info); }
            catch (Exception restore) { failures.Add("内存 LevelInfo：" + restore.Message); }
            if (diskTouched)
            {
                try { File.WriteAllBytes(Absolute(scenePath), sceneBytes); }
                catch (Exception restore) { failures.Add("场景文件：" + restore.Message); }
                try { File.WriteAllBytes(Absolute(dto.assetPath), infoBytes); }
                catch (Exception restore) { failures.Add("LevelInfo 文件：" + restore.Message); }
                try
                {
                    if (sidecarBytes == null) { if (File.Exists(sidecarPath)) File.Delete(sidecarPath); }
                    else File.WriteAllBytes(sidecarPath, sidecarBytes);
                }
                catch (Exception restore) { failures.Add("模式 sidecar：" + restore.Message); }
            }
            try
            {
                // DeInit touches transient state outside Undo. Reload the original disk scene
                // even for an in-memory failure, then restore other clean scene tabs/active state.
                if (group >= 0 && failures.Count == 0)
                {
                    AssetDatabase.ImportAsset(dto.assetPath, ImportAssetOptions.ForceUpdate);
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }
            catch (Exception restore) { failures.Add("恢复已打开场景：" + restore.Message); }
            var recovery = failures.Count == 0 ? "" : "；恢复不完整，请停止编辑并从历史备份恢复：" + string.Join("；", failures.ToArray());
            return "切换模式失败：" + ex.Message + recovery;
        }
        finally
        {
            if (backup != null) UnityEngine.Object.DestroyImmediate(backup);
            // New revision assets are intentionally retained unreferenced after a failed stage.
            if (!committed && group >= 0) Debug.LogWarning("[LevelMode] Mode change aborted; see returned recovery status.");
        }
    }

    private static void ValidateTargetOrderSequence(string path, LevelInfoSO info, string mode)
    {
        var web = LayoutEditorLevelAdminApi.LoadLevelOrderSequence(path);
        var projection = ScriptableObject.CreateInstance<LevelInfoSO>();
        try
        {
            // Keep the exact authored arrays/config references, including dormant
            // native sequences. Only the projected mode changes during preflight.
            foreach (var field in typeof(LevelInfoSO).GetFields(BindingFlags.Instance | BindingFlags.Public))
                field.SetValue(projection, field.GetValue(info));
            projection.levelType = (LevelInfoSO.LevelType)Enum.Parse(typeof(LevelInfoSO.LevelType), mode);
            var error = LayoutEditorOrderSequencePolicy.Validate(projection, web, null);
            if (error != null) throw new InvalidDataException("目标模式订单顺序不兼容：" + error);
        }
        finally { UnityEngine.Object.DestroyImmediate(projection); }
    }

    private static Type FlowType(string mode)
    {
        if (mode == "Campaign") return typeof(CampaignFlowController);
        if (mode == "Dynamic") return typeof(DynamicFlowController);
        if (mode == "Horde") return typeof(HordeFlowController);
        throw new InvalidDataException("不支持此关卡模式：" + mode);
    }

    private static void ValidateFlowReplacementDependencies(FlowControllerBase oldFlow, Type nextType)
    {
        foreach (var component in oldFlow.gameObject.GetComponents<Component>())
        {
            if (component == null || component == oldFlow) continue;
            if (component is ServerFlowControllerBase || component is ClientFlowControllerBase)
                throw new InvalidDataException("FlowManager 残留运行时 Server/Client 控制器，请先退出运行状态并重新加载场景。");
            foreach (RequireComponent requirement in component.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                foreach (var field in typeof(RequireComponent).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.FieldType != typeof(Type)) continue;
                    var required = field.GetValue(requirement) as Type;
                    if (required != null && required.IsAssignableFrom(oldFlow.GetType()) && !required.IsAssignableFrom(nextType))
                        throw new InvalidDataException("不能移除旧控制器：" + component.GetType().Name
                            + " 的 RequireComponent 依赖 " + required.Name + "，目标模式不能满足。");
                }
        }
    }

    private static PseudoPrefabManagerStub FindManager(Scene scene, LevelInfoSO info)
    {
        PseudoPrefabManagerStub found = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var manager in root.GetComponentsInChildren<PseudoPrefabManagerStub>(true))
            {
                if (found != null) throw new InvalidDataException("场景必须恰好有一个 Manager。");
                found = manager;
            }
        if (found == null || found.levelInfo != info) throw new InvalidDataException("场景 Manager 未绑定请求的 LevelInfo。");
        return found;
    }

    private static FlowControllerBase GetFlow(PseudoPrefabManagerStub manager, Scene scene)
    {
        if (manager.FlowManagerGO == null || manager.FlowManagerGO.scene != scene)
            throw new InvalidDataException("Manager.FlowManagerGO 缺失或跨场景。");
        var flows = manager.FlowManagerGO.GetComponents<FlowControllerBase>();
        if (flows.Length != 1) throw new InvalidDataException("FlowManager 必须恰好有一个原生 FlowController。");
        if (manager.FlowManagerGO.GetComponent<LevelIntroFlowroutine>() == null)
            throw new InvalidDataException("FlowManager 缺少 LevelIntroFlowroutine。");
        return flows[0];
    }

    private static void ValidateManager(PseudoPrefabManagerStub manager, Scene scene)
    {
        var objects = new[] { manager.AudioManagerGO, manager.BootstrapManagerGO, manager.PlayerSwitchingManagerGO };
        foreach (var obj in objects)
            if (obj == null || obj.scene != scene) throw new InvalidDataException("Manager 必需引用缺失或跨场景。");
        if (manager.AudioManagerGO.GetComponent<CampaignAudioManager>() == null
            || manager.BootstrapManagerGO.GetComponent<KitchenBootstrapManager>() == null
            || manager.PlayerSwitchingManagerGO.GetComponent<PlayerSwitchingManager>() == null)
            throw new InvalidDataException("Manager 的音频、Bootstrap 或玩家管理器组件缺失。");
    }

    private static void RejectDirtyAsset(UnityEngine.Object asset)
    {
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var check = typeof(EditorUtility).GetMethod("IsDirty", flags, null, new[] { typeof(int) }, null);
        if (check == null) check = typeof(EditorUtility).GetMethod("IsDirty", flags, null, new[] { typeof(UnityEngine.Object) }, null);
        if (check == null) throw new InvalidDataException("当前 Unity 缺少资产 dirty 检查能力，拒绝模式切换。");
        var parameter = check.GetParameters()[0].ParameterType == typeof(int) ? (object)asset.GetInstanceID() : asset;
        if ((bool)check.Invoke(null, new[] { parameter })) throw new InvalidDataException("资产有未保存修改，请先保存：" + AssetDatabase.GetAssetPath(asset));
    }

    private static List<StationConversion> PlanStationConversions(Scene scene, string mode)
    {
        var result = new List<StationConversion>();
        var horde = mode == "Horde";
        var targetPath = horde ? "Assets/common04/game_environment/horde/prefabs/Service_Window.asset"
            : "Assets/common01/pseudo_prefab_so/counters/ServingStationSO.asset";
        var prefabPath = horde ? "Assets/common04/game_environment/horde/prefabs/Service_Window.prefab"
            : "Assets/common01/prefabs/counters/ServingStation.prefab";
        var target = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(targetPath);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var stubType = horde ? typeof(PseudoPrefabServingStationHordeStub) : typeof(PseudoPrefabServingStationStub);
        var runtimeType = horde ? typeof(LevelEditor.PseudoPrefabServingStationHorde) : typeof(LevelEditor.PseudoPrefabServingStation);
        if (target == null || prefab == null || prefab.GetComponent(stubType) == null || prefab.GetComponent(runtimeType) == null
            || ((PseudoPrefabStub)prefab.GetComponent(stubType)).pseudoPrefabSO != target)
            throw new InvalidDataException("原生站台转换模板或 SO 绑定缺失：" + prefabPath);
        if (target.asset == null && (string.IsNullOrEmpty(target.bundleName) || !LayoutEditorCatalogApi.BundleFileExists(target.bundleName)))
            throw new InvalidDataException("原生站台运行资源 bundle 缺失：" + target.bundleName);
        var activeTargets = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (!horde && (root.GetComponentInChildren<PseudoPrefabLockableStub>(true) != null
                || root.GetComponentInChildren<HordeLockable>(true) != null))
                throw new InvalidDataException("请先删除 Horde 金币解锁门并保存，再转换模式；站台转换不会删除或改造金币门。");
            foreach (var raw in root.GetComponentsInChildren<PlateStation>(true))
                if (raw.GetComponentInParent<LevelEditor.PseudoPrefab>() == null)
                    throw new InvalidDataException("自动转换仅支持原生站台 wrapper，不处理 full-game PlateStation。");
            foreach (var raw in root.GetComponentsInChildren<HordeTarget>(true))
                if (raw.GetComponentInParent<LevelEditor.PseudoPrefab>() == null)
                    throw new InvalidDataException("自动转换不处理 full-game HordeTarget。");
            foreach (var station in root.GetComponentsInChildren<PseudoPrefabServingStationStub>(true))
            {
                var isHorde = station.GetType() == typeof(PseudoPrefabServingStationHordeStub);
                if (!isHorde && station.GetType() != typeof(PseudoPrefabServingStationStub))
                    throw new InvalidDataException("未知站台 Stub 子类型，拒绝自动转换：" + station.GetType().Name);
                var runtimes = station.GetComponents<LevelEditor.PseudoPrefab>();
                var expected = isHorde ? typeof(LevelEditor.PseudoPrefabServingStationHorde) : typeof(LevelEditor.PseudoPrefabServingStation);
                if (station.GetComponents<PseudoPrefabStub>().Length != 1 || runtimes.Length != 1 || runtimes[0].GetType() != expected
                    || station.pseudoPrefabSO == null || !station.enabled || !runtimes[0].enabled)
                    throw new InvalidDataException("站台需要唯一且启用的原生 Stub/runtime 配对及有效 SO：" + station.name);
                if (!IsNativeStationSO(station.pseudoPrefabSO, isHorde))
                    throw new InvalidDataException("站台 SO 不属于受支持的上游原生站台，拒绝更改外观：" + station.name);
                if (station.GetComponent<PlateStation>() != null || station.GetComponent<HordeTarget>() != null)
                    throw new InvalidDataException("站台 wrapper 本体混有 full-game 组件，拒绝自动转换：" + station.name);
                if (station.gameObject.activeInHierarchy) activeTargets++;
                if (isHorde == horde) continue;
                var tag = station.GetComponent<SpecificPseudoPrefabTag>();
                if (tag != null && !string.IsNullOrEmpty(tag.prefabTag) && tag.prefabTag != "ServingStation")
                    throw new InvalidDataException("站台有自定义行为标签，拒绝自动替换：" + station.name);
                if (station.GetComponent<DestroyComponentOfChildStub>() != null || station.GetComponent<LevelEditor.SetupCustomPrefab>() != null)
                    throw new InvalidDataException("站台包含自定义子物件处理，拒绝自动转换：" + station.name);
                var returns = station.plateReturns != null ? (PseudoPrefabPlateReturnStub[])station.plateReturns.Clone() : null;
                if (station.plateReturn != null && station.plateReturn.gameObject.scene != scene)
                    throw new InvalidDataException("站台回收绑定跨场景。");
                if (returns != null)
                    foreach (var item in returns)
                        if (item == null || item.gameObject.scene != scene) throw new InvalidDataException("站台回收数组含缺失或跨场景引用。");
                result.Add(new StationConversion { gameObject = station.gameObject, stub = station, runtime = runtimes[0],
                    plateReturn = station.plateReturn, plateReturns = returns, tag = tag, targetSO = target, targetPrefab = prefab,
                    name = station.gameObject.name, active = station.gameObject.activeSelf, horde = horde });
            }
        }
        if (horde && activeTargets == 0) throw new InvalidDataException("切入 Horde 需要至少一个激活的原生站台 wrapper，转换不会自动新增站台。");
        // Component references cannot silently become missing when a subtype is replaced.
        foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidDataException("场景含 Missing Script。");
                var serialized = new SerializedObject(component);
                var iterator = serialized.GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                    foreach (var conversion in result)
                        if (iterator.objectReferenceValue == conversion.stub || iterator.objectReferenceValue == conversion.runtime)
                            throw new InvalidDataException("站台待替换组件被额外序列化字段引用：" + component.GetType().Name + "." + iterator.propertyPath);
                }
            }
        return result;
    }

    private static bool IsNativeStationSO(PseudoPrefabSO source, bool horde)
    {
        if (source.GetType() != typeof(PseudoPrefabSO)) return false;
        var paths = horde ? new[] { "Assets/common04/game_environment/horde/prefabs/Service_Window.asset" }
            : new[] { "Assets/common01/pseudo_prefab_so/counters/ServingStationSO.asset",
                "Assets/common01/pseudo_prefab_so/counters/ServingStationSlimSO.asset",
                "Assets/common01/pseudo_prefab_so/counters/ServingStationSlimNoBlockSO.asset" };
        foreach (var path in paths)
        {
            var native = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
            if (native != null && source.prefabName == native.prefabName && source.bundleName == native.bundleName
                && source.assetPath == native.assetPath && source.asset == native.asset) return true;
        }
        return false;
    }

    internal static GameObject ResolveStationPrefab(GameObject go, GameObject source)
    {
        var station = go.GetComponent<PseudoPrefabServingStationStub>();
        if (station == null || station.pseudoPrefabSO == null) return source;
        var horde = station.GetType() == typeof(PseudoPrefabServingStationHordeStub);
        if (!horde && station.GetType() != typeof(PseudoPrefabServingStationStub)) return source;
        if (!IsNativeStationSO(station.pseudoPrefabSO, horde)) return source;
        var sourceStub = source != null ? source.GetComponent<PseudoPrefabServingStationStub>() : null;
        if (sourceStub != null && sourceStub.GetType() == station.GetType()) return source;
        // Converted wrappers in existing scenes can retain their old prefab link.
        var paths = horde ? new[] { "Assets/common04/game_environment/horde/prefabs/Service_Window.prefab" }
            : new[] { "Assets/common01/prefabs/counters/ServingStation.prefab" };
        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var native = prefab != null ? prefab.GetComponent<PseudoPrefabServingStationStub>() : null;
            if (native == null || native.GetType() != station.GetType() || native.pseudoPrefabSO == null) continue;
            var so = station.pseudoPrefabSO;
            var expected = native.pseudoPrefabSO;
            if (so.prefabName == expected.prefabName && so.bundleName == expected.bundleName
                && so.assetPath == expected.assetPath && so.asset == expected.asset) return prefab;
        }
        throw new InvalidDataException("无法解析原生站台的 prefab 身份：" + go.name);
    }

    private static void ApplyStationConversions(string infoPath, List<StationConversion> conversions)
    {
        foreach (var conversion in conversions)
        {
            // Prepare must remove preview children first; never remove authored child objects here.
            if (conversion.runtime.childGameObject != null || conversion.gameObject.transform.childCount != 0)
                throw new InvalidDataException("站台在 Prepare 后仍有子物件，拒绝替换组件。");
            Undo.RegisterFullObjectHierarchyUndo(conversion.gameObject, "Convert station prefab identity");
            Undo.DestroyObjectImmediate(conversion.runtime);
            Undo.DestroyObjectImmediate(conversion.stub);
            if (conversion.runtime != null || conversion.stub != null) throw new InvalidDataException("站台旧组件移除失败。");
            var type = conversion.horde ? typeof(PseudoPrefabServingStationHordeStub) : typeof(PseudoPrefabServingStationStub);
            var stub = (PseudoPrefabServingStationStub)Undo.AddComponent(conversion.gameObject, type);
            if (stub == null || stub.GetType() != type) throw new InvalidDataException("创建目标站台 Stub 失败。");
            var so = UnityEngine.Object.Instantiate(conversion.targetSO);
            so.name = conversion.targetSO.name;
            var path = Path.GetDirectoryName(infoPath).Replace('\\', '/') + "/mode_station_" + Guid.NewGuid().ToString("N") + ".asset";
            AssetDatabase.CreateAsset(so, path);
            if (AssetDatabase.GetAssetPath(so) != path) throw new IOException("站台本关 SO 创建失败。");
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null) throw new IOException("站台 SO importer 缺失。");
            var bundle = AssetDatabase.GetImplicitAssetBundleName(infoPath);
            if (string.IsNullOrEmpty(bundle)) bundle = infoPath.Split('/')[2] + "/info_" + infoPath.Split('/')[2];
            importer.assetBundleName = bundle;
            AssetDatabase.WriteImportSettingsIfDirty(path);
            stub.pseudoPrefabSO = so;
            stub.plateReturn = conversion.plateReturn;
            stub.plateReturns = conversion.plateReturns;
            var hordeStub = stub as PseudoPrefabServingStationHordeStub;
            if (hordeStub != null) { hordeStub.orderUIOffset = Vector3.zero; hordeStub.healthUIOffset = Vector3.zero; }
            var runtimeType = conversion.horde ? typeof(LevelEditor.PseudoPrefabServingStationHorde) : typeof(LevelEditor.PseudoPrefabServingStation);
            var runtime = (LevelEditor.PseudoPrefab)Undo.AddComponent(conversion.gameObject, runtimeType);
            if (runtime == null || runtime.GetType() != runtimeType) throw new InvalidDataException("创建目标站台 runtime 失败。");
            // Keep native behavior tags aligned with the replacement SO; GO identity never changes.
            if (conversion.tag != null)
            {
                Undo.RecordObject(conversion.tag, "Convert station visual tag");
                conversion.tag.prefabTag = conversion.horde ? "" : "ServingStation";
                PrefabUtility.RecordPrefabInstancePropertyModifications(conversion.tag);
            }
            conversion.stub = stub; conversion.runtime = runtime;
            // Stub repair also reads the prefab parent: an old Horde link would
            // otherwise promote a converted Campaign station back to Horde.
            if (PrefabUtility.ConnectGameObjectToPrefab(conversion.gameObject, conversion.targetPrefab) != conversion.gameObject
                || PrefabUtility.GetPrefabParent(conversion.gameObject) != conversion.targetPrefab)
                throw new InvalidDataException("站台目标 prefab 绑定失败。");
            EditorUtility.SetDirty(stub);
            PrefabUtility.RecordPrefabInstancePropertyModifications(stub);
        }
    }

    private static void ValidateStationConversions(List<StationConversion> conversions)
    {
        foreach (var conversion in conversions)
        {
            var stub = conversion.stub;
            if (conversion.gameObject == null || stub == null || conversion.runtime == null
                || stub.gameObject != conversion.gameObject || conversion.runtime.gameObject != conversion.gameObject
                || conversion.gameObject.GetComponents<PseudoPrefabStub>().Length != 1
                || conversion.gameObject.GetComponents<LevelEditor.PseudoPrefab>().Length != 1
                || stub.plateReturn != conversion.plateReturn || stub.pseudoPrefabSO == null
                || !AssetDatabase.GetAssetPath(stub.pseudoPrefabSO).StartsWith("Assets/LevelSets/", StringComparison.Ordinal))
                throw new InvalidDataException("站台原子转换校验失败。");
            if (stub.GetType() != (conversion.horde ? typeof(PseudoPrefabServingStationHordeStub) : typeof(PseudoPrefabServingStationStub))
                || conversion.runtime.GetType() != (conversion.horde ? typeof(LevelEditor.PseudoPrefabServingStationHorde) : typeof(LevelEditor.PseudoPrefabServingStation))
                || !IsNativeStationSO(stub.pseudoPrefabSO, conversion.horde))
                throw new InvalidDataException("站台目标类型或原生 SO 数据不一致。");
            if (PrefabUtility.GetPrefabParent(conversion.gameObject) != conversion.targetPrefab
                || ResolveStationPrefab(conversion.gameObject, conversion.targetPrefab) != conversion.targetPrefab)
                throw new InvalidDataException("站台转换后 prefab 身份不一致。");
            if (conversion.gameObject.name != conversion.name || conversion.gameObject.activeSelf != conversion.active)
                throw new InvalidDataException("站台转换改变了名称或激活状态。");
            var hordeStub = stub as PseudoPrefabServingStationHordeStub;
            if (hordeStub != null && (hordeStub.orderUIOffset != Vector3.zero || hordeStub.healthUIOffset != Vector3.zero))
                throw new InvalidDataException("新转换 Horde 站台的 UI 偏移必须归零。");
            var expected = conversion.plateReturns;
            var actual = stub.plateReturns;
            if ((expected == null) != (actual == null) || (expected != null && expected.Length != actual.Length))
                throw new InvalidDataException("站台回收数组长度被改变。");
            for (int i = 0; expected != null && i < expected.Length; i++)
                if (expected[i] != actual[i]) throw new InvalidDataException("站台回收绑定被改变。");
        }
    }

    private static void ValidateHordeObjects(Scene scene, string mode)
    {
        var targets = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (mode != "Horde" && (root.GetComponentInChildren<PseudoPrefabLockableStub>(true) != null
                || root.GetComponentInChildren<HordeLockable>(true) != null))
                throw new InvalidDataException("场景仍有 Horde 金币解锁门，请先移除或替换后再切出 Horde。");
            if (mode == "Horde")
            {
                foreach (var station in root.GetComponentsInChildren<PseudoPrefabServingStationStub>(true))
                    if (!(station is PseudoPrefabServingStationHordeStub))
                        throw new InvalidDataException("切入 Horde 前请将普通交付口替换为敌群交付门；普通 PlateStation 会导致原生 Horde 投递索引越界。");
                foreach (var station in root.GetComponentsInChildren<PlateStation>(true))
                    if (station.GetComponentInParent<LevelEditor.PseudoPrefab>() == null && station.GetComponent<HordeTarget>() == null)
                        throw new InvalidDataException("存在未绑定 HordeTarget 的原生 PlateStation，不能切入 Horde。");
            }
            foreach (var stub in root.GetComponentsInChildren<PseudoPrefabServingStationHordeStub>(true))
            {
                if (mode != "Horde") throw new InvalidDataException("场景仍有 Horde 专用交付门；请先移除或替换后再切出 Horde。");
                if (stub.pseudoPrefabSO == null) throw new InvalidDataException("Horde 交付门缺少原生 SO。");
                if (stub.gameObject.activeInHierarchy) targets++;
            }
            foreach (var target in root.GetComponentsInChildren<HordeTarget>(true))
            {
                // Ignore runtime preview descendants; the persistent Horde stub was counted above.
                if (target.GetComponentInParent<LevelEditor.PseudoPrefab>() != null) continue;
                if (mode != "Horde") throw new InvalidDataException("场景仍有原生 HordeTarget，不能保留该组件切入 Campaign/Dynamic。");
                if (target.GetComponent<PlateStation>() == null || target.GetComponent<Interactable>() == null)
                    throw new InvalidDataException("HordeTarget 缺少 PlateStation/Interactable。");
                if (target.gameObject.activeInHierarchy) targets++;
            }
        }
        if (mode == "Horde" && targets == 0)
            throw new InvalidDataException("切入 Horde 前请先摆放至少一个原生敌群交付门（HordeTarget）；普通交付口不能作为敌人目标。");
    }

    private static List<LevelEditor.Debugger> FindDebuggers(Scene scene, FlowControllerBase flow)
    {
        var result = new List<LevelEditor.Debugger>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var debug in root.GetComponentsInChildren<LevelEditor.Debugger>(true))
            {
                if (Require(new SerializedObject(debug), "flowController").objectReferenceValue != flow)
                    throw new InvalidDataException("Debug.flowController 缺失或未指向 Manager 的 FlowManager。");
                result.Add(debug);
            }
        if (result.Count != 1) throw new InvalidDataException("需要恰好一个已绑定的原生 Debug 组件。");
        return result;
    }

    private static void ValidateReferencesToFlow(Scene scene, FlowControllerBase flow, List<LevelEditor.Debugger> debuggers)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidDataException("场景含 Missing Script，不能安全迁移。");
                if (component == flow) continue;
                var iterator = new SerializedObject(component).GetIterator();
                while (iterator.Next(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == flow
                        && !(component is LevelEditor.Debugger && iterator.propertyPath == "flowController"
                            && debuggers.Contains((LevelEditor.Debugger)component)))
                        throw new InvalidDataException("控制器被额外字段引用，拒绝破坏引用：" + component.GetType().Name + "." + iterator.propertyPath);
            }
    }

    private static void RejectPartLevel(string infoPath, string scenePath)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        paths.Add(Path.GetDirectoryName(infoPath).Replace('\\', '/') + "/part_level~/part_level.json");
        var name = Path.GetFileNameWithoutExtension(scenePath);
        if (name.StartsWith("s_", StringComparison.Ordinal)) name = name.Substring(2);
        paths.Add("Assets/LevelSets/" + infoPath.Split('/')[2] + "/data/" + name + "/part_level~/part_level.json");
        foreach (var path in paths)
        {
            RejectLinkPath(path);
            var abs = Absolute(path);
            if (!File.Exists(abs)) continue;
            if (new FileInfo(abs).Length > 512 * 1024) throw new InvalidDataException("分 P sidecar 超过大小上限，拒绝猜测模式。");
            var store = JsonUtility.FromJson<PartLevelStoreDto>(File.ReadAllText(abs));
            if (store == null) throw new InvalidDataException("分 P sidecar 无效。");
            if (store.partLevel != null && store.partLevel.enabled)
                throw new InvalidDataException("Web 分 P 不可逆，不能转换为原生 Dynamic/Horde；请新建独立原生模式关卡。");
        }
    }

    internal static void ValidateNativePartLevel(string infoPath, string scenePath)
    {
        RejectPartLevel(infoPath, scenePath);
    }

    internal static void ValidateNativeHordeObjects(Scene scene)
    {
        ValidateHordeObjects(scene, "Horde");
    }

    private static Template ReadTemplate(string mode)
    {
        var path = LayoutEditorLevelTemplate.ScenePath(mode);
        var text = File.ReadAllText(Absolute(path));
        var data = ReadTemplateManager(text);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(TemplatePrefabGuid(text, data)));
        if (prefab == null) throw new InvalidDataException("模板原生环境 prefab 丢失。");
        var flows = prefab.GetComponentsInChildren<FlowControllerBase>(true);
        if (flows.Length != 1 || flows[0].GetType() != FlowType(mode)) throw new InvalidDataException("模板 FlowController 类型不匹配。");
        var result = new Template();
        result.root = prefab; result.flow = flows[0];
        result.info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(LayoutEditorLevelTemplate.InfoPath(mode));
        if (result.info == null || result.info.levelType.ToString() != mode) throw new InvalidDataException("模板 LevelInfo 模式不匹配。");
        result.config = ReadPseudo(data, "configTemplateSO", true);
        result.recipe = ReadPseudo(data, "RecipeUISO", true);
        result.hordeUI = ReadPseudo(data, "hordeModeUISO", mode == "Horde");
        result.waveUI = ReadPseudo(data, "hordeWaveNumberUISO", mode == "Horde");
        if (mode != "Horde")
        {
            var campaign = (CampaignFlowController)result.flow;
            if (campaign.m_teamMonitor == null || campaign.m_teamMonitor.m_recipeBarUIController == null)
                throw new InvalidDataException("模板 TeamMonitor/RecipeFlowGUI 缺失。");
            result.recipeUI = campaign.m_teamMonitor.m_recipeBarUIController;
        }
        return result;
    }

    private static string ReadTemplateManager(string text)
    {
        var matches = Regex.Matches(text, @"(?ms)^--- !u!114 &[^\r\n]+\r?\nMonoBehaviour:\r?\n(?:(?!^---).)*?^  levelInfo:[^\r\n]*(?:(?!^---).)*");
        if (matches.Count != 1) throw new InvalidDataException("模板必须恰好有一个序列化 Manager。");
        return matches[0].Value;
    }

    private static string TemplatePrefabGuid(string text, string manager)
    {
        var flowId = Regex.Match(manager, @"(?m)^  FlowManagerGO: \{fileID: (\d+)\}");
        if (!flowId.Success) throw new InvalidDataException("模板 FlowManagerGO 缺失。");
        var matches = Regex.Matches(text, @"(?ms)^--- !u!1 &" + flowId.Groups[1].Value
            + @" stripped\r?\nGameObject:\r?\n  m_PrefabParentObject: \{fileID: \d+, guid: ([0-9a-f]{32}),\s*type: 2\}");
        if (matches.Count != 1) throw new InvalidDataException("模板 FlowManager 不是唯一可验证的原生 prefab 引用。");
        return matches[0].Groups[1].Value;
    }

    private static PseudoPrefabSO ReadPseudo(string text, string field, bool required)
    {
        var match = Regex.Match(text, @"(?m)^  " + field + @": \{fileID: 11400000, guid: ([0-9a-f]{32}),\s*type: 2\}");
        var asset = match.Success ? AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(AssetDatabase.GUIDToAssetPath(match.Groups[1].Value)) : null;
        if (required && asset == null) throw new InvalidDataException("模板缺少 " + field);
        return asset;
    }

    private static LevelModeConfigDto BuildModeData(string path, LevelInfoSO info, Template template, string mode)
    {
        if (mode == "Campaign") return null;
        var preview = UnityEngine.Object.Instantiate(info);
        try
        {
            preview.levelType = (LevelInfoSO.LevelType)Enum.Parse(typeof(LevelInfoSO.LevelType), mode);
            LevelModeConfigDto dto = null;
            try
            {
                dto = LayoutEditorLevelModeStore.LoadFromAssets(path, preview);
                if (LayoutEditorLevelModeStore.Validate(dto) != null) dto = null;
            }
            catch (InvalidDataException) { }
            if (dto == null)
            {
                var defaults = UnityEngine.Object.Instantiate(template.info);
                try
                {
                    // Upstream templates have reused config GUIDs in some revisions. Resolve
                    // explicit mode filenames, as CreateLevel does, instead of trusting collisions.
                    var configs = new LevelConfigSetupPerPlayerCountSO[4];
                    for (int p = 0; p < configs.Length; p++)
                    {
                        configs[p] = AssetDatabase.LoadAssetAtPath<LevelConfigSetupPerPlayerCountSO>(LayoutEditorLevelTemplate.ConfigPath(mode, p + 1));
                        if (configs[p] == null) throw new InvalidDataException("缺少目标模式人数模板：" + (p + 1) + "P");
                    }
                    defaults.config_1p = configs[0]; defaults.config_2p = configs[1];
                    defaults.config_3p = configs[2]; defaults.config_4p = configs[3];
                    dto = LayoutEditorLevelModeStore.LoadFromAssets(path, defaults);
                }
                finally { UnityEngine.Object.DestroyImmediate(defaults); }
            }
            var error = LayoutEditorLevelModeStore.Validate(dto);
            if (error != null) throw new InvalidDataException("目标模式初始配置无效：" + error);
            return dto;
        }
        finally { UnityEngine.Object.DestroyImmediate(preview); }
    }

    private static RecipeFlowGUI FindRecipeUI(PseudoPrefabManagerStub manager, Scene scene)
    {
        var candidates = manager.FlowManagerGO.transform.root.GetComponentsInChildren<RecipeFlowGUI>(true);
        if (candidates.Length > 1) throw new InvalidDataException("环境含多个 RecipeFlowGUI，拒绝猜测绑定。");
        if (manager.RecipeUIGO != null && (candidates.Length != 1 || manager.RecipeUIGO != candidates[0].gameObject))
            throw new InvalidDataException("Manager.RecipeUIGO 不属于同一环境或绑定不一致。");
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static Transform FindTemplateParent(Template template, Transform targetRoot)
    {
        var names = new List<string>();
        var parent = template.recipeUI.transform.parent;
        while (parent != null && parent != template.root.transform) { names.Insert(0, parent.name); parent = parent.parent; }
        if (parent == null) throw new InvalidDataException("模板 RecipeUI 不在环境内。");
        var found = targetRoot;
        foreach (var name in names)
        {
            Transform next = null;
            foreach (Transform child in found)
                if (child.name == name) { if (next != null) throw new InvalidDataException("RecipeUI 父路径重名。"); next = child; }
            if (next == null) throw new InvalidDataException("场景缺少 RecipeUI 父路径：" + name);
            found = next;
        }
        return found;
    }

    private static GameObject[] FindTransitions(PseudoPrefabManagerStub manager, int count)
    {
        var scene = manager.gameObject.scene;
        return LayoutEditorNativeModeValidation.FindTransitions(scene, GetFlow(manager, scene), count + 1);
    }

    private static void ValidateTemplateProperties(FlowControllerBase flow)
    {
        if (flow.m_gameConfig == null) throw new InvalidDataException("模板 FlowController 缺少 GameConfig。");
        var serialized = new SerializedObject(flow);
        Require(serialized, "m_gameConfig");
        if (flow is CampaignFlowController) Require(serialized, "m_teamMonitor.m_recipeBarUIController");
        if (flow is DynamicFlowController) Require(serialized, "m_transitions");
        if (flow is HordeFlowController)
        { Require(serialized, "m_uiPrefab"); Require(serialized, "m_waveNumberUIPrefab"); }
    }

    private static void ValidateBundles(Template template)
    {
        foreach (var asset in new[] { template.config, template.recipe, template.hordeUI, template.waveUI })
            if (asset != null && asset.asset == null && (string.IsNullOrEmpty(asset.bundleName)
                || !LayoutEditorCatalogApi.BundleFileExists(asset.bundleName)))
                throw new InvalidDataException("模式模板的运行资源 bundle 缺失：" + AssetDatabase.GetAssetPath(asset));
    }

    private static void MergeModeDependencies(Template template, LevelInfoSO info)
    {
        var deps = new List<string>(info.dependencies ?? new string[0]);
        foreach (var asset in new[] { template.config, template.recipe, template.hordeUI, template.waveUI })
            if (asset != null && !string.IsNullOrEmpty(asset.bundleName)
                && LayoutEditorCatalogApi.BundleFileExists(asset.bundleName) && !deps.Contains(asset.bundleName))
                deps.Add(asset.bundleName);
        info.dependencies = LayoutEditorNativeDependencies.Filter(deps);
    }

    private static void CopyFlow(FlowControllerBase source, FlowControllerBase target, RecipeFlowGUI ui, GameObject[] transitions, int orders)
    {
        var from = new SerializedObject(source);
        var to = new SerializedObject(target);
        var fields = new List<string>();
        fields.Add("m_gameConfig");
        if (source is CampaignFlowController) { fields.Add("m_teamMonitor"); fields.Add("m_maxOrdersAllowed"); }
        if (source is DynamicFlowController) fields.Add("m_transitions");
        if (source is HordeFlowController)
        {
            fields.Add("m_waveNumberUIPrefab"); fields.Add("m_waveNumberUIDelay");
            fields.Add("m_waveNumberUILocalisationTag"); fields.Add("m_uiPrefab");
        }
        foreach (var field in fields) to.CopyFromSerializedProperty(Require(from, field));
        if (target is CampaignFlowController)
        {
            Require(to, "m_teamMonitor.m_recipeBarUIController").objectReferenceValue = ui;
            Require(to, "m_maxOrdersAllowed").intValue = orders;
        }
        if (target is DynamicFlowController)
        {
            var array = Require(to, "m_transitions"); array.arraySize = transitions.Length;
            for (int i = 0; i < transitions.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = transitions[i];
        }
        to.ApplyModifiedProperties();
    }

    private static void CloneGameConfig(string path, FlowControllerBase flow)
    {
        var config = UnityEngine.Object.Instantiate(flow.m_gameConfig);
        var assetPath = Path.GetDirectoryName(path).Replace('\\', '/') + "/mode_gameconfig_" + Guid.NewGuid().ToString("N") + ".asset";
        AssetDatabase.CreateAsset(config, assetPath);
        if (AssetDatabase.GetAssetPath(config) != assetPath) throw new IOException("创建独立 GameConfig 失败。");
        var serialized = new SerializedObject(flow);
        Require(serialized, "m_gameConfig").objectReferenceValue = config;
        serialized.ApplyModifiedProperties();
    }

    private static void ClonePlayers(string path, LevelInfoSO info)
    {
        var players = new[] { info.config_1p, info.config_2p, info.config_3p, info.config_4p };
        foreach (var player in players) if (player == null) throw new InvalidDataException("缺少原生人数配置。");
        var folder = Path.GetDirectoryName(path).Replace('\\', '/');
        var name = "mode_change_" + Guid.NewGuid().ToString("N");
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(folder, name))) throw new IOException("创建独立人数配置目录失败。");
        var importer = AssetImporter.GetAtPath(folder + "/" + name);
        importer.assetBundleName = AssetDatabase.GetImplicitAssetBundleName(path);
        importer.SaveAndReimport();
        for (int p = 0; p < players.Length; p++)
        {
            players[p] = UnityEngine.Object.Instantiate(players[p]);
            var playerPath = folder + "/" + name + "/player_" + (p + 1) + ".asset";
            AssetDatabase.CreateAsset(players[p], playerPath);
            if (AssetDatabase.GetAssetPath(players[p]) != playerPath) throw new IOException("独立人数配置创建失败。");
        }
        info.config_1p = players[0]; info.config_2p = players[1]; info.config_3p = players[2]; info.config_4p = players[3];
    }

    private static void RejectExternalPrefabReferences(GameObject root)
    {
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) throw new InvalidDataException("RecipeUI 克隆含 Missing Script。");
            var iterator = new SerializedObject(component).GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                var obj = iterator.objectReferenceValue;
                var c = obj as Component; var go = obj as GameObject;
                var transform = c != null ? c.transform : go != null ? go.transform : null;
                if (transform != null && transform != root.transform && !transform.IsChildOf(root.transform))
                    throw new InvalidDataException("RecipeUI 仍引用外部模板物件：" + iterator.propertyPath);
            }
        }
    }

    private static HashSet<GameObject> CollectObjects(Scene scene)
    {
        var result = new HashSet<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if ((transform.gameObject.hideFlags & HideFlags.DontSave) == 0) result.Add(transform.gameObject);
        return result;
    }

    private static Dictionary<Camera, Matrix4x4> CaptureCameras(Scene scene)
    {
        var result = new Dictionary<Camera, Matrix4x4>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var camera in root.GetComponentsInChildren<Camera>(true)) result.Add(camera, camera.transform.localToWorldMatrix);
        return result;
    }

    private static Dictionary<string, UnityEngine.Object> CaptureManagerReferences(PseudoPrefabManagerStub manager)
    {
        var result = new Dictionary<string, UnityEngine.Object>();
        var iterator = new SerializedObject(manager).GetIterator();
        while (iterator.Next(true))
        {
            var path = iterator.propertyPath;
            if (iterator.propertyType != SerializedPropertyType.ObjectReference || path == "configTemplateSO"
                || path == "hordeModeUISO" || path == "hordeWaveNumberUISO" || path == "RecipeUIGO" || path == "RecipeUISO") continue;
            result.Add(path, iterator.objectReferenceValue);
        }
        return result;
    }

    private static Dictionary<Transform, TransformState> CaptureTransforms(Scene scene)
    {
        var result = new Dictionary<Transform, TransformState>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                result.Add(t, new TransformState { parent = t.parent, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
        return result;
    }

    private static void ValidateChanged(Scene scene, PseudoPrefabManagerStub manager, FlowControllerBase flow,
        List<LevelEditor.Debugger> debuggers, LevelInfoSO info, HashSet<GameObject> objects,
        FlowControllerBase removed, Dictionary<Camera, Matrix4x4> cameras)
    {
        if (GetFlow(manager, scene) != flow || flow.GetType() != FlowType(info.levelType.ToString()) || removed != null)
            throw new InvalidDataException("迁移后的控制器绑定校验失败。");
        foreach (var debug in debuggers)
            if (Require(new SerializedObject(debug), "flowController").objectReferenceValue != flow)
                throw new InvalidDataException("Debug 控制器重绑定失败。");
        foreach (var obj in objects) if (obj == null) throw new InvalidDataException("迁移意外删除了原场景物件。");
        foreach (var pair in cameras)
            if (pair.Key == null || pair.Key.transform.localToWorldMatrix != pair.Value)
                throw new InvalidDataException("迁移意外修改了相机。");
        if (manager.configTemplateSO == null || flow.m_gameConfig == null) throw new InvalidDataException("模式模板或 GameConfig 丢失。");
        var campaign = flow as CampaignFlowController;
        if (campaign != null && (manager.RecipeUIGO == null || campaign.m_teamMonitor == null
            || campaign.m_teamMonitor.m_recipeBarUIController != manager.RecipeUIGO.GetComponent<RecipeFlowGUI>()))
            throw new InvalidDataException("Campaign/Dynamic RecipeUI 绑定失败。");
        if (flow is HordeFlowController && (manager.RecipeUIGO != null || manager.hordeModeUISO == null || manager.hordeWaveNumberUISO == null))
            throw new InvalidDataException("Horde UI 引用校验失败。");
        ValidateManager(manager, scene);
    }

    private static SerializedProperty Require(SerializedObject obj, string path)
    {
        var property = obj.FindProperty(path);
        if (property == null) throw new InvalidDataException("缺少序列化字段：" + path);
        return property;
    }

    private static void RejectLinkPath(string path)
    {
        var current = Directory.GetParent(Application.dataPath).FullName;
        foreach (var part in path.Split('/'))
        {
            current = Path.Combine(current, part);
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("模式切换拒绝符号链接路径。");
        }
    }

    private static string Absolute(string path)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar));
    }
}
