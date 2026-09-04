using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LevelEditor;
using LevelEditorStub;

/// <summary>
/// 可移动火锅编辑器预览（2026-09-03）。
///
/// 背景：web_utensil_large_pot_01_pushable 的载具由宿主 PseudoPrefab 在编辑模式
/// 实例化（可见），但「锅体装配到载具上」发生在 CustomStub.PushablePot 运行时
/// （游戏/Play）——编辑模式下只见载具不见锅。旧版（WebTools 时代）由游戏侧
/// LayoutRuntimePushablePot.ResetChild 在编辑模式完成装配所以可见；CustomStub 化
/// 后无编辑模式等价物，本脚本补齐：编辑模式下按运行时同款参数实例化预览锅
/// （同一 bundle prefab + 同款剥件），进 Play 前销毁（运行时由 CustomStub 权威
/// 装配，避免双锅）。
///
/// 触发点：域重载 / 场景打开 / 退出 Play / 写回后的伪资产重载（宿主重建载具后
/// 30 秒轮询窗口内补挂；写回主路径 ReloadPseudoAssetsFull 会销毁载具连同预览锅，
/// 本脚本随宿主重初始化自动补回）。
/// </summary>
[InitializeOnLoad]
public static class LayoutEditorPushablePotPreview
{
    private const string CarrierPrefabName = "pushable_object";
    private static bool _armed;
    private static double _deadline;
    private static double _nextScan;
    private static readonly List<GameObject> s_previews = new List<GameObject>();

    static LayoutEditorPushablePotPreview()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (scene, mode) => Arm();
        Arm();
    }

    private static void Arm()
    {
        if (Application.isPlaying)
            return;
        _armed = true;
        // 宿主 PseudoPrefabManager 初始化 / bundle 加载都可能滞后（写回主路径会整体
        // 卸载重载 bundle），给足窗口；期间 0.5s 节流扫描。
        _deadline = EditorApplication.timeSinceStartup + 30.0;
        _nextScan = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Stop()
    {
        _armed = false;
        EditorApplication.update -= Tick;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // 进 Play 前销毁预览：运行时由 CustomStub.PushablePot 权威装配
            DestroyPreviews();
            Stop();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            Arm();
        }
    }

    private static void Tick()
    {
        if (!_armed || Application.isPlaying)
        {
            Stop();
            return;
        }
        if (EditorApplication.timeSinceStartup > _deadline)
        {
            Stop();
            return;
        }
        if (EditorApplication.timeSinceStartup < _nextScan)
            return;
        _nextScan = EditorApplication.timeSinceStartup + 0.5;

        bool pending = false;
        foreach (var pp in Object.FindObjectsOfType<PseudoPrefab>())
        {
            if (pp == null)
                continue;
            var baseStub = pp.GetComponent<PseudoPrefabStub>();
            if (baseStub == null || baseStub.pseudoPrefabSO == null
                || baseStub.pseudoPrefabSO.prefabName != CarrierPrefabName)
                continue;
            var carrier = pp.childGameObject;
            if (carrier == null)
            {
                pending = true; // 宿主尚未实例化载具，窗口内重试
                continue;
            }
            if (HasPot(carrier))
                continue;
            TryAttachPreview(pp.gameObject, carrier);
        }
        if (!pending)
            Stop();
    }

    private static bool HasPot(GameObject carrier)
    {
        foreach (Transform child in carrier.transform)
        {
            if (child != null && child.name.IndexOf("large_pot", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static void TryAttachPreview(GameObject wrapper, GameObject carrier)
    {
        // 锅 SO：soArray 槽 0（与运行时 PushablePot.PotSO 同源）
        var soArray = wrapper.GetComponent<PseudoPrefabSOArray>();
        if (soArray == null || soArray.pseudoPrefabSOs == null || soArray.pseudoPrefabSOs.Length == 0)
            return;
        var potSO = soArray.pseudoPrefabSOs[0];
        if (potSO == null)
            return;
        var potPrefab = PseudoPrefabManager.LoadAsset<GameObject>(potSO);
        if (potPrefab == null)
            return; // bundle 未加载：等下一次扫描（窗口内重试）
        var pot = (GameObject)PrefabUtility.InstantiatePrefab(potPrefab);
        if (pot == null)
            pot = Object.Instantiate(potPrefab);
        pot.name = potPrefab.name;
        pot.transform.SetParent(carrier.transform, false);
        pot.transform.localPosition = Vector3.zero;
        pot.transform.localRotation = Quaternion.identity;
        // 与运行时 PushablePot.Assemble 同款剥件：物理/交互/吸附归载具，Collider 保留
        StripAll(pot, typeof(Rigidbody));
        StripAll(pot, typeof(Interactable));
        StripAll(pot, typeof(EditorGridSnap));
        StripAll(pot, typeof(AttachStation));
        s_previews.Add(pot);
        LayoutEditorLog.Log("[PushablePotPreview] 编辑模式预览锅已挂载: " + wrapper.name
            + " → " + pot.name + "（进 Play 前自动移除，运行时由 CustomStub 装配）");
    }

    private static void StripAll(GameObject root, System.Type type)
    {
        if (type == null)
            return;
        foreach (var c in root.GetComponentsInChildren(type, true))
        {
            if (c != null)
                Object.DestroyImmediate(c);
        }
    }

    private static void DestroyPreviews()
    {
        for (int i = 0; i < s_previews.Count; i++)
        {
            var go = s_previews[i];
            if (go != null)
                Object.DestroyImmediate(go);
        }
        s_previews.Clear();
    }
}
