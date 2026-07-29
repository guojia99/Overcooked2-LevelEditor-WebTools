using System;
using System.Collections.Generic;
using System.IO;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

public static class LayoutEditorCatalogApi
{
    public static IngredientCatalogDto ScanIngredients()
    {
        var list = new List<IngredientEntryDto>();
        var root = "Assets/common01/food/Ingredients";
        if (!AssetDatabase.IsValidFolder(root))
            return new IngredientCatalogDto { ingredients = new IngredientEntryDto[0] };

        foreach (var guid in AssetDatabase.FindAssets("t:PseudoPrefabSO", new[] { root }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
            if (so == null)
                continue;

            var id = Path.GetFileNameWithoutExtension(path);
            string nameZh;
            string nameEn;
            LayoutEditorManualLookup.TryGet(id, out nameZh, out nameEn);
            list.Add(new IngredientEntryDto
            {
                guid = guid,
                id = id,
                nameZh = nameZh,
                nameEn = nameEn,
                assetPath = path
            });
        }

        list.Sort((a, b) => string.Compare(a.nameZh, b.nameZh, StringComparison.Ordinal));
        return new IngredientCatalogDto { ingredients = list.ToArray() };
    }

    public static RecipeCatalogDto ScanRecipes(string levelSet)
    {
        var list = new List<RecipeEntryDto>();
        var folders = new List<string> { "Assets/common01/food/Recipes", "Assets/common01/food/CustomRecipes" };

        if (!string.IsNullOrEmpty(levelSet))
        {
            var levelData = "Assets/LevelSets/" + levelSet + "/data";
            if (AssetDatabase.IsValidFolder(levelData))
                folders.Add(levelData);
        }

        var seen = new HashSet<string>();
        for (int f = 0; f < folders.Count; f++)
        {
            if (!AssetDatabase.IsValidFolder(folders[f]))
                continue;

            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { folders[f] }))
            {
                if (!seen.Add(guid))
                    continue;

                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null)
                    continue;

                if (!(so is PseudoPrefabSORecipe) && !(so is CustomRecipeSO))
                    continue;

                var id = Path.GetFileNameWithoutExtension(path);
                list.Add(new RecipeEntryDto
                {
                    guid = guid,
                    id = id,
                    nameZh = RecipeDisplayName(so, id),
                    assetPath = path
                });
            }
        }

        list.Sort((a, b) => string.Compare(a.nameZh, b.nameZh, StringComparison.Ordinal));
        return new RecipeCatalogDto { recipes = list.ToArray() };
    }

    public static LevelRecipesDto GetLevelRecipes(string sceneAssetPath)
    {
        var info = LayoutEditorLevelInfoResolver.ResolveForScene(sceneAssetPath);
        if (info == null)
            return new LevelRecipesDto { recipeGuids = new string[0] };

        var guids = new List<string>();
        if (info.recipes != null)
        {
            for (int i = 0; i < info.recipes.Length; i++)
            {
                if (info.recipes[i] == null)
                    continue;
                var path = AssetDatabase.GetAssetPath(info.recipes[i]);
                if (!string.IsNullOrEmpty(path))
                    guids.Add(AssetDatabase.AssetPathToGUID(path));
            }
        }

        return new LevelRecipesDto
        {
            levelInfoAssetPath = AssetDatabase.GetAssetPath(info),
            levelName = info.levelName,
            recipeGuids = guids.ToArray()
        };
    }

    public static string SetLevelRecipes(LevelRecipesUpdateDto update)
    {
        if (update == null || string.IsNullOrEmpty(update.levelInfoAssetPath))
            return "Missing levelInfoAssetPath.";

        var info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(update.levelInfoAssetPath);
        if (info == null)
            return "LevelInfoSO not found.";

        var recipes = new List<ScriptableObject>();
        if (update.recipeGuids != null)
        {
            for (int i = 0; i < update.recipeGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(update.recipeGuids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so != null)
                    recipes.Add(so);
            }
        }

        Undo.RecordObject(info, "Layout Editor Recipes");
        info.recipes = recipes.ToArray();
        EditorUtility.SetDirty(info);

        var manager = UnityEngine.Object.FindObjectOfType<PseudoPrefabManagerStub>();
        if (manager != null && manager.levelInfo == info)
            EditorUtility.SetDirty(manager);

        return null;
    }

    private static string RecipeDisplayName(ScriptableObject so, string id)
    {
        var custom = so as CustomRecipeSO;
        if (custom != null && !string.IsNullOrEmpty(custom.recipeName))
            return custom.recipeName;

        if (id.EndsWith("_SO", StringComparison.Ordinal))
            return id.Substring(0, id.Length - 3).Replace('_', ' ');
        return id;
    }
}
