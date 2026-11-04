using System;
using System.Reflection;
using HarmonyLib;
using LevelEditor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 宿主 PseudoPrefabPressureSwitch.Setup 首行直接访问
/// PressureSwitchCosmeticDecisions.m_buttonBit.sharedMaterial。bundle 重载/
/// PseudoPrefab 重入时 m_buttonBit 可能指向已销毁的 Pad MeshRenderer，导致
/// ResetChild 抛 MissingReferenceException（每次开场景/写回刷一条红错）。
///
/// 编辑器侧最小修复：Setup 前把失效引用重新指到当前 child 的 Pad
/// MeshRenderer。host 文件只读，不改 Assets/Scripts；真机不经过 LevelEditor.*
/// 伪 prefab（仅编辑器宿主），因此该补丁只装编辑器。
/// </summary>
[InitializeOnLoad]
static class LayoutEditorPressureSwitchSetupPatch
{
    private const string HarmonyId = "oc2.layouteditor.pressureswitchsetup";

    static LayoutEditorPressureSwitchSetupPatch()
    {
        try
        {
            var target = typeof(PseudoPrefabPressureSwitch).GetMethod(
                "Setup", BindingFlags.Public | BindingFlags.Instance);
            if (target == null)
            {
                Debug.LogWarning("[pseudo-repair] 未找到 PseudoPrefabPressureSwitch.Setup，跳过压力开关修复补丁");
                return;
            }
            var prefix = typeof(LayoutEditorPressureSwitchSetupPatch).GetMethod(
                "Prefix", BindingFlags.NonPublic | BindingFlags.Static);
            var harmony = new Harmony(HarmonyId);
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[pseudo-repair] 压力开关 Setup 修复补丁安装失败: " + ex.Message);
        }
    }

    private static void Prefix(PseudoPrefabPressureSwitch __instance)
    {
        try
        {
            if (__instance == null)
                return;

            // 先修复材质 SO：错误的选择（如把 Switch.prefab 的 SO 当材质）会让
            // 宿主 LoadAsset<Material> 失败并触发 DeInit/Init 重入，正是 child
            // 在 Setup 期间被销毁的根因。修复后 Setup 不再重入。
            var pressureStub = __instance.GetComponent<LevelEditorStub.PseudoPrefabPressureSwitchStub>();
            if (pressureStub != null)
                LayoutEditorSwitchMaterialRepair.SanitizePressureSwitch(pressureStub);

            var child = __instance.childGameObject;
            if (child == null)
                return;
            var cos = child.GetComponent("PressureSwitchCosmeticDecisions");
            if (cos == null)
                return;
            var bitField = cos.GetType().GetField("m_buttonBit",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (bitField == null)
                return;
            var bit = bitField.GetValue(cos) as MeshRenderer;
            if (bit != null)
                return; // 引用正常（Unity fake-null 对已销毁对象返回 null）

            MeshRenderer pad = null;
            var renderers = child.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null)
                    continue;
                if (string.Equals(r.gameObject.name, "Pad", StringComparison.Ordinal))
                {
                    pad = r;
                    break;
                }
            }
            if (pad == null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null)
                        continue;
                    if (r.gameObject.name.IndexOf("Pad", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pad = r;
                        break;
                    }
                }
            }
            if (pad == null)
                return;
            bitField.SetValue(cos, pad);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[pseudo-repair] 压力开关 Setup 前缀修复失败: " + ex.Message);
        }
    }
}
