using System;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 开关/压力开关外观材质 SO 校验与修复。
///
/// 背景：Switch/PressureSwitch 的「外观」下拉曾经把同目录下的
/// PseudoPrefabSO（Switch.asset / PressureSwitch.asset，实际指向 prefab）也算进
/// 材质列表；选中后 stub.occupiedMaterialSO 指向一个 GameObject 资产。
/// 宿主 PseudoPrefabPressureSwitch.Setup 用 LoadAsset&lt;Material&gt; 读取失败，
/// 会触发 PseudoPrefabManager.DeInit/Init 重入，销毁正在 Setup 的 child，
/// 最终在 m_buttonBit.sharedMaterial 抛 MissingReferenceException。
///
/// 本类在编辑器侧把非 .mat 的 SO 重置回 prefab 默认/标准开关材质，供
/// Setup Harmony 前缀、场景自愈和写回导出共同使用。
/// </summary>
public static class LayoutEditorSwitchMaterialRepair
{
    public const string DefaultActiveSoPath =
        "Assets/common01/pseudo_prefab_so/mechanisms/Switch/mat_sk_switch_01.asset";
    public const string DefaultInactiveSoPath =
        "Assets/common01/pseudo_prefab_so/mechanisms/Switch/mat_sk_switch_unoccupied.asset";

    /** PseudoPrefabSO 是否指向真正的 Material（.mat 或 asset 直引 Material）。 */
    public static bool IsMaterialSO(PseudoPrefabSO so)
    {
        if (so == null)
            return false;
        if (so.asset is Material)
            return true;
        var path = (so.assetPath ?? "").Replace('\\', '/');
        return path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase);
    }

    /** 标准开关材质 SO（AssetDatabase 直读；编辑器/编辑器 Play 可用）。 */
    public static PseudoPrefabSO DefaultFor(string defaultPath)
    {
        if (string.IsNullOrEmpty(defaultPath))
            return null;
        var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(defaultPath);
        return IsMaterialSO(so) ? so : null;
    }

    /** 从 prefab 资产读取默认材质 SO（实例上的覆写不参与）。 */
    public static PseudoPrefabSO PrefabDefault(GameObject instance, string stubField)
    {
        if (instance == null || string.IsNullOrEmpty(stubField))
            return null;
        var parent = PrefabUtility.GetPrefabParent(instance) as GameObject;
        if (parent == null)
            return null;
        var pressure = parent.GetComponent<PseudoPrefabPressureSwitchStub>();
        if (pressure != null)
        {
            if (stubField == "occupiedMaterialSO") return pressure.occupiedMaterialSO;
            if (stubField == "unoccupiedMaterialSO") return pressure.unoccupiedMaterialSO;
        }
        var sw = parent.GetComponent<PseudoPrefabSwitchStub>();
        if (sw != null)
        {
            if (stubField == "activeMaterial") return sw.activeMaterial;
            if (stubField == "inactiveMaterial") return sw.inactiveMaterial;
        }
        return null;
    }

    /** 当前 SO 有效则保留；否则依次回落 prefab 默认、标准材质。 */
    public static PseudoPrefabSO ResolveOrDefault(GameObject instance, PseudoPrefabSO current,
        string stubField, string defaultPath)
    {
        if (IsMaterialSO(current))
            return current;
        var prefabDefault = PrefabDefault(instance, stubField);
        if (IsMaterialSO(prefabDefault))
            return prefabDefault;
        return DefaultFor(defaultPath);
    }

    public static bool SanitizePressureSwitch(PseudoPrefabPressureSwitchStub stub)
    {
        if (stub == null)
            return false;
        bool changed = false;
        var occ = ResolveOrDefault(stub.gameObject, stub.occupiedMaterialSO,
            "occupiedMaterialSO", DefaultActiveSoPath);
        if (occ != null && occ != stub.occupiedMaterialSO)
        {
            if (!Application.isPlaying)
                Undo.RecordObject(stub, "Layout Editor Pressure Switch Material Repair");
            stub.occupiedMaterialSO = occ;
            changed = true;
        }
        var un = ResolveOrDefault(stub.gameObject, stub.unoccupiedMaterialSO,
            "unoccupiedMaterialSO", DefaultInactiveSoPath);
        if (un != null && un != stub.unoccupiedMaterialSO)
        {
            if (!Application.isPlaying)
                Undo.RecordObject(stub, "Layout Editor Pressure Switch Material Repair");
            stub.unoccupiedMaterialSO = un;
            changed = true;
        }
        if (changed)
        {
            if (!Application.isPlaying)
                EditorUtility.SetDirty(stub);
            LayoutEditorLog.Log("[pseudo-repair] 压力开关外观材质 SO 修复: " + stub.name);
        }
        return changed;
    }

    public static bool SanitizeSwitch(PseudoPrefabSwitchStub stub)
    {
        if (stub == null)
            return false;
        bool changed = false;
        var active = ResolveOrDefault(stub.gameObject, stub.activeMaterial,
            "activeMaterial", DefaultActiveSoPath);
        if (active != null && active != stub.activeMaterial)
        {
            if (!Application.isPlaying)
                Undo.RecordObject(stub, "Layout Editor Switch Material Repair");
            stub.activeMaterial = active;
            changed = true;
        }
        var inactive = ResolveOrDefault(stub.gameObject, stub.inactiveMaterial,
            "inactiveMaterial", DefaultInactiveSoPath);
        if (inactive != null && inactive != stub.inactiveMaterial)
        {
            if (!Application.isPlaying)
                Undo.RecordObject(stub, "Layout Editor Switch Material Repair");
            stub.inactiveMaterial = inactive;
            changed = true;
        }
        if (changed)
        {
            if (!Application.isPlaying)
                EditorUtility.SetDirty(stub);
            LayoutEditorLog.Log("[pseudo-repair] 开关外观材质 SO 修复: " + stub.name);
        }
        return changed;
    }

    /** 场景内全部开关/压力开关材质 SO 修复（开场景/写回/域重载兜底）。 */
    public static int SanitizeAllInScene()
    {
        int changed = 0;
        var pressureSwitches = UnityEngine.Object.FindObjectsOfType<PseudoPrefabPressureSwitchStub>();
        for (int i = 0; i < pressureSwitches.Length; i++)
        {
            if (SanitizePressureSwitch(pressureSwitches[i]))
                changed++;
        }
        var switches = UnityEngine.Object.FindObjectsOfType<PseudoPrefabSwitchStub>();
        for (int i = 0; i < switches.Length; i++)
        {
            if (SanitizeSwitch(switches[i]))
                changed++;
        }
        if (changed > 0)
            LayoutEditorLog.Log("[pseudo-repair] 开关外观材质 SO 修复: " + changed + " 个");
        return changed;
    }
}
