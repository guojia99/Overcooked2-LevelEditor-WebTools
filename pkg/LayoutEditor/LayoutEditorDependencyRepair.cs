using System.Collections.Generic;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 宿主原版 PseudoPrefabManager 在场景打开（OnEnable → Init）时对缺失的 bundle 会抛
/// KeyNotFoundException（LoadAssetBundle 里 bundleDict[key]）。插件不能修改宿主代码，
/// 因此在 Unity 启动（domain reload）后扫描 LevelSets 下所有 LevelInfoSO，
/// 把 dependencies 中【插件自己的】自定义菜谱 bundle（&lt;set&gt;/custom_recipes）移除——
/// 仅当该 bundle 磁盘上不存在时。游戏自身的 bundle 一律不动（缺失属于环境问题，
/// 移除会静默缺包且无法恢复）。bundle 构建后由插件（LayoutEditorPseudoReload /
/// LayoutEditorCatalogApi）按需补回。
/// </summary>
[InitializeOnLoad]
static class LayoutEditorDependencyRepair
{
    static LayoutEditorDependencyRepair()
    {
        EditorApplication.delayCall += RunRepairPass;
    }

    [MenuItem("Layout Editor/Repair Missing Bundle Dependencies", false, 210)]
    private static void RunRepairPass()
    {
        try
        {
            RepairMissingBundleDependencies();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[LayoutEditor] bundle dependency repair skipped: " + ex.Message);
        }
    }

    private static void RepairMissingBundleDependencies()
    {
        bool anyChanged = false;
        foreach (var guid in AssetDatabase.FindAssets("t:LevelInfoSO"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/LevelSets/", System.StringComparison.Ordinal))
                continue;
            var info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(path);
            if (info == null || info.dependencies == null || info.dependencies.Length == 0)
                continue;

            var kept = new List<string>(info.dependencies.Length);
            bool changed = false;
            foreach (var b in info.dependencies)
            {
                // 只处理插件自己的 bundle（命名约定：<set>/custom_recipes）。
                // 游戏自身的 bundle（bundle47 等）缺失属于环境问题：自动移除会导致
                // 运行时静默缺包，且插件没有恢复机制；bundle 拷入后修复也不该动它们。
                if (!b.EndsWith("/custom_recipes", System.StringComparison.OrdinalIgnoreCase) ||
                    LayoutEditorCatalogApi.BundleFileExists(b))
                {
                    kept.Add(b);
                    continue;
                }
                changed = true;
                Debug.Log("[LayoutEditor] 从 LevelInfoSO 移除缺失的自定义菜谱 bundle 依赖: " + b +
                    " ← " + path + "（构建 bundle 后会在重新加载时自动补回）");
            }
            if (!changed)
                continue;

            Undo.RecordObject(info, "Layout Editor Repair Missing Bundles");
            info.dependencies = kept.ToArray();
            EditorUtility.SetDirty(info);
            anyChanged = true;
        }

        if (anyChanged)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[LayoutEditor] 已修复 LevelInfoSO 依赖中的缺失 bundle。");
        }
    }
}
