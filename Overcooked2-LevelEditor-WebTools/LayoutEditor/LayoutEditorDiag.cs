using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 诊断工具：验证运行时 PseudoPrefabManager.LoadAsset 对各类 assetPath 的加载结果。
/// 使用：Tools/Layout Editor → 诊断 AssetBundle 加载，然后看 Console。
/// 目的：定位「添加 Web 内置菜单后 Play 报 NullReferenceException
///  (RecipeHelper.GetIngredientOrderNode)」的根因 —— allIngredients/食材箱引用的
///  PseudoPrefabSO 在运行时 LoadAsset 返回 null。
/// </summary>
public static class LayoutEditorDiag
{
    [MenuItem("Layout Editor/诊断 AssetBundle 加载", false, 300)]
    public static void DiagAssetLoad()
    {
        var dir = Path.Combine(Application.streamingAssetsPath, "Windows");
        Debug.Log("[LayoutEditor-Diag] bundle 目录: " + dir);

        var b47 = AssetBundle.LoadFromFile(Path.Combine(dir, "bundle47"));
        if (b47 == null) { Debug.LogError("[LayoutEditor-Diag] bundle47 加载失败"); return; }
        var b354 = AssetBundle.LoadFromFile(Path.Combine(dir, "bundle354"));
        if (b354 != null) Debug.Log("[LayoutEditor-Diag] bundle354 加载成功");
        else Debug.LogError("[LayoutEditor-Diag] bundle354 加载失败");

        // 1) 基础食材 assetPath 变体（ChocolateSO.assetPath = 反斜杠大写）
        var variants = new[]
        {
            @"Assets\prefabs\overcooked_legacy\ingredients\Chocolate.prefab", // 原样（反斜杠）
            "Assets/prefabs/overcooked_legacy/ingredients/Chocolate.prefab",  // 正斜杠大写
            "assets/prefabs/overcooked_legacy/ingredients/chocolate.prefab",  // 正斜杠小写（容器原样）
        };
        foreach (var p in variants)
        {
            var go = b47.LoadAsset<GameObject>(p);
            Debug.Log("[LayoutEditor-Diag] bundle47 '" + p + "' -> " + (go != null ? "OK" : "NULL"));
        }

        // 2) Web 拷贝食材（dlc08_bun）：assetPath 为正斜杠大写
        var dlc = new[]
        {
            "Assets/downloadablecontent/dlc08/dlc_assets/prefabs/ingredients/dlc08_bun.prefab",
            "assets/downloadablecontent/dlc08/dlc_assets/prefabs/ingredients/dlc08_bun.prefab",
        };
        if (b354 != null)
        {
            foreach (var p in dlc)
            {
                var go = b354.LoadAsset<GameObject>(p);
                Debug.Log("[LayoutEditor-Diag] bundle354 '" + p + "' -> " + (go != null ? "OK" : "NULL"));
            }
            // 3) 菜谱资产（chickenburger，assetPath 指向 orderdefinitions）
            var r = b354.LoadAsset<GameObject>(
                "Assets/downloadablecontent/dlc08/dlc_assets/data/orderdefinitions/recipeitems/chickenburger.asset");
            Debug.Log("[LayoutEditor-Diag] bundle354 recipeitems/chickenburger.asset -> " + (r != null ? "OK" : "NULL"));
        }

        b47.Unload(true);
        if (b354 != null) b354.Unload(true);
    }
}
