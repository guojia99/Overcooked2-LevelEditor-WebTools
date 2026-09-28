using System.IO;
using System.Linq;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>commonW3 Web 果汁模型后处理：从已落盘的 OBJ/FBX 生成 prefab 并绑定 CustomRecipeSO.model。</summary>
public static class LayoutEditorCommonW3SmoothieBake
{
    private const string SmoothieRecipesDir = "Assets/commonW3/custom_recipes/smoothie";

    [MenuItem("Layout Editor/Bake commonW3 Smoothie Models", false, 204)]
    public static void BakeAllSmoothieModels()
    {
        AssetDatabase.Refresh();
        var recipePaths = Directory
            .GetFiles(SmoothieRecipesDir, "Web_Smoothie_*.asset", SearchOption.TopDirectoryOnly)
            .Select(p => p.Replace('\\', '/'))
            .OrderBy(p => p, System.StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (recipePaths.Length == 0)
        {
            EditorUtility.DisplayDialog("commonW3 Smoothie Models", "未找到 Web_Smoothie_*.asset 菜谱。", "OK");
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
            "commonW3 Smoothie Models",
            "完成：成功 " + ok + "，失败 " + fail + "（共 " + recipePaths.Length + " 道）。\n请执行 Build AssetBundles（commonW3）。",
            "OK");
    }
}
