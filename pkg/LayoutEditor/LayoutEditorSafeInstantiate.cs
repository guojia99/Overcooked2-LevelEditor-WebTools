using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 导出预览专用的安全实例化：临时把 prefab 资产置为 inactive，无父级 Instantiate
/// 得到 inactive 克隆；移除会在编辑态执行运行时初始化的组件
/// （SetupCustomPrefab / PseudoPrefab）后，再挂父级并激活。
///
/// 背景：资源包里的 prefab 可能带 [ExecuteInEditMode] 的运行时脚本；直接
/// Instantiate 会触发 SetupCustomPrefab.Awake → PseudoPrefabManager.Instance NRE。
/// 缩略图/3D 导出只需要网格与材质，不需要这些运行时组件。
/// </summary>
public static class LayoutEditorSafeInstantiate
{
    /// <summary>在原点实例化（调用方可随后重设父级/位置）。</summary>
    public static GameObject Instantiate(GameObject prefab)
    {
        return InstantiateInternal(prefab, null, Vector3.zero, Quaternion.identity, false);
    }

    /// <summary>按世界坐标实例化根对象。</summary>
    public static GameObject InstantiateAt(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return InstantiateInternal(prefab, null, position, rotation, true);
    }

    /// <summary>作为子对象实例化（localPosition/localRotation 归零，scale=1）。</summary>
    public static GameObject InstantiateUnder(GameObject prefab, Transform parent)
    {
        return InstantiateInternal(prefab, parent, Vector3.zero, Quaternion.identity, false);
    }

    private static GameObject InstantiateInternal(GameObject prefab, Transform parent,
        Vector3 position, Quaternion rotation, bool worldPosition)
    {
        if (prefab == null)
            return null;

        // 不能直接 Instantiate(activePrefab)：会立即触发 SetupCustomPrefab.Awake。
        // 先挂到 inactive 容器克隆（Awake 延后），剥掉运行时组件后再挂父级/激活。
        // 注意：不要修改 prefab 资产本身（commonW2 等项目内 prefab 会被 Unity 误保存）。
        var holder = new GameObject("__LayoutEditorSafeInstantiate");
        holder.SetActive(false);
        GameObject inst = null;
        try
        {
            inst = UnityEngine.Object.Instantiate(prefab, holder.transform) as GameObject;
            if (inst == null)
                return null;

            StripRuntimeSetup(inst);

            if (parent != null)
            {
                inst.transform.SetParent(parent, false);
                inst.transform.localPosition = position;
                inst.transform.localRotation = rotation;
                inst.transform.localScale = Vector3.one;
            }
            else
            {
                inst.transform.SetParent(null, true);
                if (worldPosition)
                {
                    inst.transform.position = position;
                    inst.transform.rotation = rotation;
                }
            }

            inst.SetActive(true);
            return inst;
        }
        catch
        {
            if (inst != null)
                UnityEngine.Object.DestroyImmediate(inst);
            throw;
        }
        finally
        {
            if (holder != null)
                UnityEngine.Object.DestroyImmediate(holder);
        }
    }

    private static void StripRuntimeSetup(GameObject inst)
    {
        var setups = inst.GetComponentsInChildren<LevelEditor.SetupCustomPrefab>(true);
        for (int i = 0; i < setups.Length; i++)
        {
            if (setups[i] != null)
                UnityEngine.Object.DestroyImmediate(setups[i]);
        }

        var pseudos = inst.GetComponentsInChildren<LevelEditor.PseudoPrefab>(true);
        for (int i = 0; i < pseudos.Length; i++)
        {
            if (pseudos[i] != null)
                UnityEngine.Object.DestroyImmediate(pseudos[i]);
        }
    }
}
