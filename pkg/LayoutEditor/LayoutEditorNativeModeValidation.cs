using System;
using System.Collections.Generic;
using System.IO;
using GameModes.Horde;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only preflight. Call before MissingScriptCleanup, Prepare, scene save or build.
public static class LayoutEditorNativeModeValidation
{
    // phaseCount describes the proposed SO graph during a config save/mode transaction.
    // Export and Apply omit it to validate the currently committed native graph.
    public static string ValidateScene(Scene scene, LevelInfoSO info, int? phaseCount = null)
    {
        if (info == null) return "缺少 LevelInfoSO。";
        if (info.levelType == LevelInfoSO.LevelType.Campaign) return null;
        try
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath(AssetDatabase.GetAssetPath(info), info))
                throw new InvalidDataException("请加载与 LevelInfoSO 路径对应的目标场景后校验原生模式。");
            LayoutEditorLevelModeChange.ValidateNativePartLevel(AssetDatabase.GetAssetPath(info), scene.path);
            PseudoPrefabManagerStub manager = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                RejectMissingScripts(root);
                foreach (var candidate in root.GetComponentsInChildren<PseudoPrefabManagerStub>(true))
                {
                    if (manager != null) throw new InvalidDataException("目标场景必须恰好有一个 Manager。");
                    manager = candidate;
                }
            }
            if (manager == null || manager.levelInfo != info)
                throw new InvalidDataException("目标场景 Manager 未绑定请求的 LevelInfoSO。");
            var host = manager.GetComponent<LevelEditor.PseudoPrefabManager>();
            if (host == null || host.stub != manager || !host.enabled || !manager.gameObject.activeInHierarchy)
                throw new InvalidDataException("Manager 缺少启用的原生宿主绑定。");
            var flowGO = RequireSceneObject(manager.FlowManagerGO, scene, "FlowManagerGO");
            var flows = flowGO.GetComponents<FlowControllerBase>();
            if (flows.Length != 1 || !flows[0].enabled || !flowGO.activeInHierarchy
                || flowGO.GetComponent<LevelIntroFlowroutine>() == null || flows[0].m_gameConfig == null)
                throw new InvalidDataException("FlowManager 需要唯一启用的原生 flow、LevelIntroFlowroutine 和 GameConfig。");
            RequireManagerComponent<CampaignAudioManager>(manager.AudioManagerGO, scene, "AudioManagerGO");
            RequireManagerComponent<KitchenBootstrapManager>(manager.BootstrapManagerGO, scene, "BootstrapManagerGO");
            RequireManagerComponent<PlayerSwitchingManager>(manager.PlayerSwitchingManagerGO, scene, "PlayerSwitchingManagerGO");
            ValidatePseudo(manager.configTemplateSO, "configTemplateSO");
            foreach (var player in new[] { info.config_1p, info.config_2p, info.config_3p, info.config_4p })
                if (player == null) throw new InvalidDataException("缺少原生 1P–4P 人数配置。");
            if (info.levelType == LevelInfoSO.LevelType.Dynamic)
            {
                var flow = flows[0] as DynamicFlowController;
                if (flow == null || flow.GetType() != typeof(DynamicFlowController))
                    throw new InvalidDataException("Dynamic 必须绑定原生 DynamicFlowController。");
                var count = phaseCount.HasValue ? phaseCount.Value : NativePhaseCount(info);
                ValidateTransitions(scene, flow.m_transitions, count);
                var ui = RequireSceneObject(manager.RecipeUIGO, scene, "RecipeUIGO").GetComponent<RecipeFlowGUI>();
                if (ui == null || !ui.gameObject.activeInHierarchy || flow.m_teamMonitor == null
                    || flow.m_teamMonitor.m_recipeBarUIController != ui)
                    throw new InvalidDataException("Dynamic TeamMonitor/RecipeFlowGUI 绑定无效。");
                ValidatePseudo(manager.RecipeUISO, "RecipeUISO");
                if (manager.configTemplateSO.asset != null && !(manager.configTemplateSO.asset is KitchenLevelConfigBase))
                    throw new InvalidDataException("Dynamic configTemplateSO 必须是 KitchenLevelConfigBase 模板。");
            }
            else if (info.levelType == LevelInfoSO.LevelType.Horde)
            {
                var flow = flows[0] as HordeFlowController;
                if (flow == null || flow.GetType() != typeof(HordeFlowController))
                    throw new InvalidDataException("Horde 必须绑定原生 HordeFlowController。");
                if (manager.RecipeUIGO != null)
                    throw new InvalidDataException("Horde 不能绑定 Campaign RecipeUIGO。");
                // Prepare deliberately clears these runtime prefab fields. The persistent SO
                // bindings are authoritative; never require preview-only prefabs to survive it.
                var serialized = new SerializedObject(flow);
                Require(serialized, "m_uiPrefab");
                Require(serialized, "m_waveNumberUIPrefab");
                var delay = Require(serialized, "m_waveNumberUIDelay").floatValue;
                if (float.IsNaN(delay) || float.IsInfinity(delay) || delay < 0
                    || string.IsNullOrEmpty(Require(serialized, "m_waveNumberUILocalisationTag").stringValue))
                    throw new InvalidDataException("Horde 波次 UI 延迟或本地化字段无效。");
                ValidatePseudo(manager.hordeModeUISO, "hordeModeUISO");
                ValidatePseudo(manager.hordeWaveNumberUISO, "hordeWaveNumberUISO");
                ValidateHordeUI(manager.hordeModeUISO.asset, false);
                ValidateHordeUI(manager.hordeWaveNumberUISO.asset, true);
                var template = manager.configTemplateSO.asset;
                if (template != null && (!(template is HordeLevelConfig) || ((HordeLevelConfig)template).m_flowroutineData == null))
                    throw new InvalidDataException("Horde configTemplateSO 类型或 m_flowroutineData 无效。");
                LayoutEditorLevelModeChange.ValidateNativeHordeObjects(scene);
            }
            else throw new InvalidDataException("未知的原生关卡模式。");
            return null;
        }
        catch (Exception ex) { return "原生模式场景校验失败：" + ex.Message; }
    }

    internal static string ValidateForSave(string infoPath, LevelInfoSO info, int? phaseCount)
    {
        try
        {
            var path = ScenePath(infoPath, info);
            LayoutEditorLevelModeChange.ValidateNativePartLevel(infoPath, path);
            var scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded) return ValidateScene(scene, info, phaseCount);
            if (phaseCount.HasValue && (info.recipeListSOs == null || info.roundTimes == null
                || phaseCount.Value != info.recipeListSOs.Length || phaseCount.Value != info.roundTimes.Length))
                return "修改原生 Dynamic 阶段数必须先打开目标场景并配置 N−1 个有效原生转场。";
            // Menu/timing-only saves can run with a closed scene. Apply/export must still
            // unconditionally call ValidateScene once they load the target, before cleanup.
            return null;
        }
        catch (Exception ex) { return "原生模式保存校验失败：" + ex.Message; }
    }

    private static string ScenePath(string infoPath, LevelInfoSO info)
    {
        var parts = (infoPath ?? "").Split('/');
        if (parts.Length != 6 || parts[0] != "Assets" || parts[1] != "LevelSets" || parts[3] != "data"
            || string.IsNullOrEmpty(info.sceneName) || info.sceneName.IndexOfAny(new[] { '/', '\\', ':', '.' }) >= 0)
            throw new InvalidDataException("LevelInfoSO 路径或 sceneName 无效。");
        return "Assets/LevelSets/" + parts[2] + "/scenes/" + info.sceneName + ".unity";
    }

    private static int NativePhaseCount(LevelInfoSO info)
    {
        if (info.recipeListSOs == null || info.roundTimes == null || info.recipeListSOs.Length != info.roundTimes.Length)
            throw new InvalidDataException("Dynamic 原生菜单与时长数组必须等长。");
        foreach (var menu in info.recipeListSOs)
            if (menu == null) throw new InvalidDataException("Dynamic 原生阶段菜单缺失。");
        return info.recipeListSOs.Length;
    }

    // Preserve the actual flow order. For a scene without a Dynamic flow, use the existing
    // scene hierarchy order only when the entire candidate set matches the requested count.
    internal static GameObject[] FindTransitions(Scene scene, FlowControllerBase flow, int phaseCount)
    {
        var dynamicFlow = flow as DynamicFlowController;
        GameObject[] result;
        if (dynamicFlow != null) result = dynamicFlow.m_transitions;
        else
        {
            var found = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transition in root.GetComponentsInChildren<AnimatedDynamicTransition>(true))
                    found.Add(transition.gameObject);
            if (found.Count != phaseCount - 1)
                throw new InvalidDataException("切入 Dynamic 需要已有 flow.m_transitions，或本场景层级顺序中恰好 N−1 个已有原生转场；不能复制模板地形。");
            result = found.ToArray();
        }
        ValidateTransitions(scene, result, phaseCount);
        return result;
    }

    private static void ValidateTransitions(Scene scene, GameObject[] transitions, int phaseCount)
    {
        if (phaseCount < 2 || phaseCount > LayoutEditorLevelModeStore.MaxPhases
            || transitions == null || transitions.Length != phaseCount - 1)
            throw new InvalidDataException("Dynamic 的 flow.m_transitions 必须恰好包含 N−1 个转场（N 为 2–64 个原生阶段）。");
        var seen = new HashSet<GameObject>();
        for (int i = 0; i < transitions.Length; i++)
        {
            var go = RequireSceneObject(transitions[i], scene, "m_transitions[" + i + "]");
            if (!seen.Add(go)) throw new InvalidDataException("Dynamic 转场数组不能重复引用同一物件。");
            RejectMissingScripts(go);
            var components = go.GetComponents<DynamicTransitionBase>();
            if (components.Length != 1 || !(components[0] is AnimatedDynamicTransition))
                throw new InvalidDataException("每个转场物件必须有唯一 AnimatedDynamicTransition。");
            var transition = (AnimatedDynamicTransition)components[0];
            var type = transition.GetType();
            if (type != typeof(AnimatedDynamicTransition) && type != typeof(BeachAnimatedDynamicTransition)
                && type != typeof(CampsiteAnimatedDynamicTransition))
                throw new InvalidDataException("不支持此原生转场类型：" + type.Name);
            if (!transition.enabled) throw new InvalidDataException("原生转场组件被禁用。");
            var serialized = new SerializedObject(transition);
            var world = RequireReference<Animator>(serialized, "m_worldAnimator", scene);
            ValidateAnimator(world, scene, Require(serialized, "m_worldAnimatorTrigger").stringValue, AnimatorControllerParameterType.Trigger);
            ValidateAnimator(world, scene, "IsTransitioning", AnimatorControllerParameterType.Bool);
            if (transition is BeachAnimatedDynamicTransition)
            {
                RequireReference<Transform>(serialized, "m_leavingCounterLayoutRoot", scene);
                RequireReference<Collider>(serialized, "m_tideCollider", scene);
                var tide = RequireReference<Animator>(serialized, "m_tideAnimator", scene);
                ValidateAnimator(tide, scene, "TideInScene", AnimatorControllerParameterType.Bool);
                ValidateAnimator(tide, scene, "IsTideTransitioning", AnimatorControllerParameterType.Bool);
            }
            if (transition is CampsiteAnimatedDynamicTransition)
            {
                RequireReference<Transform>(serialized, "m_leavingRaftLayoutRoot", scene);
                RequireReference<Collider>(serialized, "m_leavingRaftRespawnCollider", scene);
                var entering = Require(serialized, "m_enteringRaftLayoutRoot");
                if (entering.objectReferenceValue != null || entering.objectReferenceInstanceIDValue != 0)
                    RequireReference<Transform>(serialized, "m_enteringRaftLayoutRoot", scene);
                if (go.GetComponent<RespawnCollider>() == null)
                    throw new InvalidDataException("Campsite 转场物件缺少 RespawnCollider。");
                var lakeProperty = Require(serialized, "m_lakeAnimator");
                var lake = lakeProperty.objectReferenceValue as Animator;
                if (lakeProperty.objectReferenceValue != null || lakeProperty.objectReferenceInstanceIDValue != 0)
                {
                    ValidateAnimator(lake, scene, "RaftsAreChanging", AnimatorControllerParameterType.Bool);
                    ValidateAnimator(lake, scene, "IsArtInMotion", AnimatorControllerParameterType.Bool);
                }
            }
        }
    }

    private static void ValidateAnimator(Animator animator, Scene scene, string parameter, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.gameObject.scene != scene || animator.runtimeAnimatorController == null
            || !animator.enabled || string.IsNullOrEmpty(parameter))
            throw new InvalidDataException("转场 Animator/controller/参数缺失、被禁用或跨场景。");
        RejectMissingScripts(animator.gameObject);
        var controller = animator.runtimeAnimatorController;
        var overrides = new HashSet<RuntimeAnimatorController>();
        while (controller is AnimatorOverrideController)
        {
            if (!overrides.Add(controller)) throw new InvalidDataException("Animator override controller 循环引用。");
            controller = ((AnimatorOverrideController)controller).runtimeAnimatorController;
        }
        var native = controller as UnityEditor.Animations.AnimatorController;
        if (native == null || native.layers.Length == 0)
            throw new InvalidDataException("Animator 缺少有效的原生 controller/layer。");
        foreach (var layer in native.layers)
        {
            if (layer.stateMachine == null) throw new InvalidDataException("Animator controller 缺少 state machine。");
            ValidateStateMachine(layer.stateMachine, new HashSet<UnityEditor.Animations.AnimatorStateMachine>());
        }
        foreach (var entry in native.parameters)
            if (entry.name == parameter && entry.type == type) return;
        throw new InvalidDataException("Animator 缺少 " + parameter + "（" + type + "）参数。");
    }

    private static void ValidateStateMachine(UnityEditor.Animations.AnimatorStateMachine machine,
        HashSet<UnityEditor.Animations.AnimatorStateMachine> visited)
    {
        if (!visited.Add(machine)) return;
        foreach (var behaviour in machine.behaviours)
            if (behaviour == null) throw new InvalidDataException("Animator state machine 含 Missing Script behaviour，不能清理删除。");
        foreach (var child in machine.states)
        {
            if (child.state == null) throw new InvalidDataException("Animator controller 含缺失 state。");
            foreach (var behaviour in child.state.behaviours)
                if (behaviour == null) throw new InvalidDataException("Animator state 含 Missing Script behaviour，不能清理删除。");
        }
        foreach (var child in machine.stateMachines)
        {
            if (child.stateMachine == null) throw new InvalidDataException("Animator controller 含缺失子 state machine。");
            ValidateStateMachine(child.stateMachine, visited);
        }
    }

    private static T RequireReference<T>(SerializedObject serialized, string field, Scene scene) where T : Component
    {
        var component = Require(serialized, field).objectReferenceValue as T;
        if (component == null) throw new InvalidDataException("缺少转场字段：" + field);
        RequireSceneObject(component.gameObject, scene, field);
        return component;
    }

    private static GameObject RequireSceneObject(GameObject go, Scene scene, string field)
    {
        if (go == null || go.scene != scene) throw new InvalidDataException(field + " 缺失或跨场景。");
        return go;
    }

    private static void RequireManagerComponent<T>(GameObject go, Scene scene, string field) where T : Component
    {
        if (RequireSceneObject(go, scene, field).GetComponent<T>() == null)
            throw new InvalidDataException(field + " 缺少 " + typeof(T).Name);
    }

    private static void ValidatePseudo(PseudoPrefabSO pseudo, string field)
    {
        if (pseudo == null) throw new InvalidDataException("Manager 缺少 " + field);
        if (pseudo.asset != null)
        {
            var go = pseudo.asset as GameObject;
            if (go != null) RejectMissingScripts(go);
        }
        else if (string.IsNullOrEmpty(pseudo.assetPath) || string.IsNullOrEmpty(pseudo.bundleName)
            || !LayoutEditorCatalogApi.BundleFileExists(pseudo.bundleName))
            throw new InvalidDataException(field + " 缺少资源或有效 bundle/assetPath。");
    }

    private static void ValidateHordeUI(UnityEngine.Object asset, bool waveNumber)
    {
        // Bundle-backed assets are bound at runtime by the manager, not serialized on flow.
        if (asset == null) return;
        var go = asset as GameObject;
        if (go == null || go.GetComponent<UIControllerBase>() == null
            || (waveNumber && go.GetComponentInChildren<T17Text>(true) == null))
            throw new InvalidDataException("Horde UI 模板缺少 UIControllerBase 或波次 T17Text。");
    }

    private static void RejectMissingScripts(GameObject root)
    {
        foreach (var component in root.GetComponentsInChildren<Component>(true))
            if (component == null)
                throw new InvalidDataException("原生模式含 Missing Script，必须修复其行为后再保存/导出；不能删除掩盖。");
    }

    private static SerializedProperty Require(SerializedObject serialized, string field)
    {
        var property = serialized.FindProperty(field);
        if (property == null) throw new InvalidDataException("缺少原生序列化字段：" + field);
        return property;
    }
}
