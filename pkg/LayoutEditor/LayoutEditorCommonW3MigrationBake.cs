using System.IO;
using System.Linq;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>commonW3 batch1 迁移模型后处理：炒饭 / 汤粥 / 冰淇淋 / 布丁 / 冰沙。</summary>
public static class LayoutEditorCommonW3MigrationBake
{
    private static readonly string[] CategoryDirs =
    {
        "Assets/commonW3/custom_recipes/fried_rice",
        "Assets/commonW3/custom_recipes/soup",
        "Assets/commonW3/custom_recipes/ice_cream",
        "Assets/commonW3/custom_recipes/pudding",
        "Assets/commonW3/custom_recipes/milk_slush",
    };

    [MenuItem("Layout Editor/Bake commonW3 Migration Models", false, 205)]
    public static void BakeAllMigrationModels()
    {
        AssetDatabase.Refresh();
        var recipePaths = CategoryDirs
            .Where(Directory.Exists)
            .SelectMany(dir =>
                Directory.GetFiles(dir, "Web_*.asset", SearchOption.TopDirectoryOnly)
                    .Select(p => p.Replace('\\', '/')))
            .OrderBy(p => p, System.StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (recipePaths.Length == 0)
        {
            EditorUtility.DisplayDialog("commonW3 Migration Models", "未找到 batch1 Web_*.asset 菜谱。", "OK");
            return;
        }

        var ok = 0;
        var fail = 0;
        foreach (var recipePath in recipePaths)
        {
            if (AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(recipePath) == null)
            {
                Debug.LogWarning("[commonW3] 跳过（菜谱不存在）: " + recipePath);
                fail++;
                continue;
            }
            var result = LayoutEditorLevelAdminApi.BakeCustomRecipeModelFromDisk(recipePath);
            if (result != null && result.ok)
            {
                Debug.Log("[commonW3] 已烘焙模型: " + recipePath);
                ok++;
            }
            else
            {
                Debug.LogError("[commonW3] 烘焙失败: " + recipePath + " — " + (result != null ? result.error : "未知错误"));
                fail++;
            }
        }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "commonW3 Migration Models",
            "完成：成功 " + ok + "，失败 " + fail + "（共 " + recipePaths.Length + " 道）。\n请执行 Build AssetBundles（commonW3）。",
            "OK");
    }
}
