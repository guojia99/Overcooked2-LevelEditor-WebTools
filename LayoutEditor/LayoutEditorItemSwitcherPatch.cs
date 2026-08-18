using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LevelEditor;
using LevelEditorStub;

/// <summary>
/// 酱料机/饮料机「多酱料循环切换」运行时补丁。
///
/// 背景：这类机器（dlc08/dlc11 饮料机/酱料机）的真实 prefab 内含
/// PickupItemSwitcher（m_itemPrefabs 循环列表 + m_switchTrigger 触发消息），
/// 开关发送触发消息即按列表循环切换输出。宿主 PseudoPrefab 在 Play 时会
/// ResetChild 重新从 bundle 实例化真实 prefab（场景里对 childGameObject 的
/// 修改全部丢失），且宿主没有读取该列表的 stub 字段，因此：
///   - 编辑器写回时把多选列表序列化到 pseudo GameObject 的
///     PseudoPrefabSOArray（宿主运行时组件，随场景保存；见 LayoutEditorStubIO）；
///   - 进入 Play 后由本补丁把 soArray 中的食材逐个从 bundle 加载：
///     饮料机（PickupItemSwitcher）按 GameObject 加载写 m_itemPrefabs；
///     酱料机（PlacementItemSwitcher）按 IngredientOrderNode 加载写 m_ingredients。
/// 注意：食材 SO 对酱料机指向 bundle 内的 .asset（IngredientOrderNode），对饮料机
/// 指向 .prefab。必须按正确类型直读 bundle（LoadBundleAsset），不能用宿主
/// PseudoPrefabManager.LoadAsset —— 它对 null 加载结果会触发 DeInit/Init 全量重置，
/// 在 Play 期反复触发会卡死加载界面。
/// 未配置多选列表的机器完全不受影响（使用 prefab 内置列表）。
/// </summary>
[InitializeOnLoad]
static class LayoutEditorItemSwitcherPatch
{
    private static bool _armed;
    private static double _deadline;

    static LayoutEditorItemSwitcherPatch()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _armed = true;
            // 兜底超时：PseudoPrefabManager 缺失/初始化失败时不至于每帧空转
            _deadline = EditorApplication.timeSinceStartup + 10.0;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            _armed = false;
            EditorApplication.update -= Tick;
        }
    }

    private static void Tick()
    {
        if (!_armed || !Application.isPlaying ||
            EditorApplication.timeSinceStartup > _deadline)
        {
            _armed = false;
            EditorApplication.update -= Tick;
            return;
        }
        var manager = PseudoPrefabManager.Instance;
        if (manager == null)
            return;

        bool pending = false;
        foreach (var stub in UnityEngine.Object.FindObjectsOfType<PseudoPrefabStub>())
        {
            // 防御：Play 模式进出 / ResetChild 重建期间，stub 或 child 可能
            // 在空检查之后被宿主销毁，访问其成员会抛 MissingReferenceException
            try
            {
                var soArray = stub.GetComponent<PseudoPrefabSOArray>();
                if (soArray == null || soArray.pseudoPrefabSOs == null || soArray.pseudoPrefabSOs.Length == 0)
                    continue;
                var pseudoSO = stub.pseudoPrefabSO;
                if (pseudoSO == null || !IsSpecialDispenser(pseudoSO.prefabName))
                    continue;
                var pseudo = stub.GetComponent<PseudoPrefab>();
                var child = pseudo != null ? pseudo.childGameObject : null;
                if (child == null)
                {
                    // ResetChild 尚未执行到该对象，下一帧再试
                    pending = true;
                    continue;
                }
                Patch(child, soArray.pseudoPrefabSOs);
            }
            catch (Exception)
            {
                // 对象被销毁：若是 ResetChild 重建中则下一帧重试；
                // 若是退出 Play，则下一帧会被 isPlaying 检查终止
                pending = true;
            }
        }
        if (!pending)
        {
            _armed = false;
            EditorApplication.update -= Tick;
        }
    }

    private static bool IsSpecialDispenser(string prefabName)
    {
        return prefabName == "dlc08_drink_machine" || prefabName == "dlc11_drink_dispenser"
            || prefabName == "dlc08_condiment_dispenser" || prefabName == "dlc11_condiment_dispenser";
    }

    private static void Patch(GameObject child, PseudoPrefabSO[] sos)
    {
        // Unity 重载 ==：已销毁的对象此处判空为 true，直接跳过
        if (child == null)
            return;

        var switcher = child.GetComponent<PickupItemSwitcher>();
        var placementSwitcher = child.GetComponent<PlacementItemSwitcher>();
        if (switcher == null && placementSwitcher == null)
            return;

        var prefabs = new List<GameObject>();
        var nodes = new List<IngredientOrderNode>();
        foreach (var so in sos)
        {
            if (so == null)
                continue;
            try
            {
                // 饮料机食材是 GameObject prefab，酱料机食材是 IngredientOrderNode asset
                // （assetPath 指向 bundle 内的 .asset）。先按 GameObject 加载，失败再按
                // IngredientOrderNode 加载。全程用 bundle.LoadAsset 直读，避开宿主
                // PseudoPrefabManager.LoadAsset<T> 对 null 结果触发的 DeInit/Init 连锁重置
                // （否则每次 tick 都会全量卸载重载 bundle、重建全部 child，导致卡死加载界面）。
                GameObject prefab = LoadBundleAsset<GameObject>(so);
                if (prefab != null)
                {
                    prefabs.Add(prefab);
                    // 项目按 C# 4 编译：不用 ?. ，手写空判断
                    var ipc = prefab.GetComponent<IngredientPropertiesComponent>();
                    if (ipc != null)
                    {
                        var nodeField = typeof(IngredientPropertiesComponent)
                            .GetField("m_ingredientOrderNode", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (nodeField != null)
                        {
                            var node = nodeField.GetValue(ipc) as IngredientOrderNode;
                            if (node != null)
                                nodes.Add(node);
                        }
                    }
                }
                else
                {
                    IngredientOrderNode node = LoadBundleAsset<IngredientOrderNode>(so);
                    if (node != null)
                        nodes.Add(node);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[LayoutEditor] item switcher: 加载食材失败 " + so.prefabName + ": " + ex.Message);
            }
        }

        // 主流形态：PickupItemSwitcher（开关触发 → 循环切换 PickupItemSpawner 输出）
        if (switcher != null && prefabs.Count > 0)
        {
            switcher.m_itemPrefabs = prefabs.ToArray();
            if (string.IsNullOrEmpty(switcher.m_switchTrigger))
                switcher.m_switchTrigger = "Switch";
            var spawner = child.GetComponent<PickupItemSpawner>();
            if (spawner != null)
                spawner.m_itemPrefab = prefabs[0];
        }

        // 变体形态：PlacementItemSwitcher（放置时加料；m_ingredients 是 IngredientOrderNode）
        if (placementSwitcher != null && nodes.Count > 0)
        {
            placementSwitcher.m_ingredients = nodes.ToArray();
            if (string.IsNullOrEmpty(placementSwitcher.m_switchTrigger))
                placementSwitcher.m_switchTrigger = "Switch";
        }
    }

    /// <summary>直读 bundle 资源（绕过 PseudoPrefabManager.LoadAsset 的 DeInit/Init 副作用）。</summary>
    private static T LoadBundleAsset<T>(PseudoPrefabSO so) where T : UnityEngine.Object
    {
        AssetBundle bundle = PseudoPrefabManager.GetAssetBundle(so.bundleName);
        if (bundle == null)
            return null;
        return bundle.LoadAsset<T>(so.assetPath);
    }
}
