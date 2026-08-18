using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LevelEditor;
using LevelEditorStub;

/// <summary>
/// 奶油喷罐（IngredientSpray）m_OrderPrefab 运行时补齐。
///
/// 背景：喷罐的 ServerIngredientSpray 通过 CarrierAdapter.InspectCarriedItem()
/// 返回 IngredientSpray.m_OrderPrefab（奶油 GameObject）供放置判定（盘子/马克杯的
/// CanPlaceOnPlate 链）查询——为 null 时对它 RequestInterface 直接抛
/// UnassignedReferenceException。bundle 实测：dlc09_utensil_ingredient_spray
/// （bundle405）的 m_OrderPrefab 指向同 bundle 的 DLC09_WhippedCream，正常；
/// 而 dlc03 的 utensil_ingredient_spray_01（bundle210）该字段在游戏原版数据里
/// 就是空的（原版 dlc03 关卡没有盘子，从未触发）——Web 编辑器把它放进带盘子的
/// 关卡即崩。
///
/// 处理：进入 Play 后，宿主 PseudoPrefab.ResetChild 从 bundle 重新实例化真实
/// prefab（场景内改动不保留），本补丁在 child 就绪后按喷罐皮肤加载对应奶油
/// prefab（common_w Ingredients SO → 同 bundle 直读），补上 m_OrderPrefab。
/// 喷罐与奶油同 bundle（210/405），依赖必然一起加载。
/// </summary>
[InitializeOnLoad]
static class LayoutEditorIngredientSprayPatch
{
    /// <summary>喷罐 prefab 名 → 奶油 PseudoPrefabSO（common_w/Ingredients）。</summary>
    private static readonly Dictionary<string, string> SprayCreamSoPaths =
        new Dictionary<string, string>
        {
            { "utensil_ingredient_spray_01", "Assets/common_w/Ingredients/dlc03/whippedcream.asset" },
            { "dlc09_utensil_ingredient_spray", "Assets/common_w/Ingredients/dlc09/dlc09_whippedcream.asset" },
        };

    private static bool _armed;
    private static double _deadline;

    static LayoutEditorIngredientSprayPatch()
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
        bool sawSpray = false;
        foreach (var stub in UnityEngine.Object.FindObjectsOfType<PseudoPrefabStub>())
        {
            // 防御：Play 模式进出 / ResetChild 重建期间，stub 或 child 可能
            // 在空检查之后被宿主销毁，访问其成员会抛 MissingReferenceException
            try
            {
                var pseudoSO = stub.pseudoPrefabSO;
                if (pseudoSO == null)
                    continue;
                string soPath;
                if (!SprayCreamSoPaths.TryGetValue(pseudoSO.prefabName, out soPath))
                    continue;
                sawSpray = true;
                var pseudo = stub.GetComponent<PseudoPrefab>();
                var child = pseudo != null ? pseudo.childGameObject : null;
                if (child == null)
                {
                    // ResetChild 尚未执行到该对象，下一帧再试
                    pending = true;
                    continue;
                }
                Patch(child, pseudoSO.prefabName, soPath);
            }
            catch (Exception)
            {
                // 对象被销毁：ResetChild 重建中则下一帧重试；退出 Play 则被终止
                pending = true;
            }
        }
        // 场景没有喷罐（或已全部补齐）即可停止轮询
        if (!pending && !sawSpray)
        {
            _armed = false;
            EditorApplication.update -= Tick;
        }
    }

    private static void Patch(GameObject child, string sprayPrefabName, string creamSoPath)
    {
        // Unity 重载 ==：已销毁的对象此处判空为 true，直接跳过
        if (child == null)
            return;
        var spray = child.GetComponent<IngredientSpray>();
        if (spray == null)
            spray = child.GetComponentInChildren<IngredientSpray>();
        if (spray == null || spray.m_OrderPrefab != null)
            return;

        var creamSo = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(creamSoPath);
        if (creamSo == null)
        {
            Debug.LogWarning("[LayoutEditor] ingredient spray: 找不到奶油 SO " + creamSoPath);
            return;
        }
        // 直读 bundle（绕过 PseudoPrefabManager.LoadAsset 对 null 结果触发的
        // DeInit/Init 连锁重置）；m_OrderPrefab 在原版数据里即指向 bundle 内的
        // 奶油 prefab 资产本体（非实例），此处保持同形态。
        AssetBundle bundle = PseudoPrefabManager.GetAssetBundle(creamSo.bundleName);
        if (bundle == null)
        {
            Debug.LogWarning("[LayoutEditor] ingredient spray: bundle 未加载 " + creamSo.bundleName);
            return;
        }
        GameObject creamPrefab = bundle.LoadAsset<GameObject>(creamSo.assetPath);
        if (creamPrefab == null)
        {
            Debug.LogWarning("[LayoutEditor] ingredient spray: bundle 内无奶油 prefab " + creamSo.assetPath);
            return;
        }
        spray.m_OrderPrefab = creamPrefab;
        Debug.Log("[LayoutEditor] ingredient spray: 已补齐 " + sprayPrefabName + " 的 m_OrderPrefab -> " + creamSo.prefabName);
    }
}
