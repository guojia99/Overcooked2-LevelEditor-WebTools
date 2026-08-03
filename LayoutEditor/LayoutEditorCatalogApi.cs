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
        var roots = new[] { "Assets/common01/food/Ingredients", "Assets/common02/food/Ingredients" };
        var seen = new HashSet<string>();

        foreach (var root in roots)
        {
            if (!AssetDatabase.IsValidFolder(root))
                continue;

            foreach (var guid in AssetDatabase.FindAssets("t:PseudoPrefabSO", new[] { root }))
            {
                if (!seen.Add(guid))
                    continue;

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
                    assetPath = path,
                    group = FoodGroupOf(path)
                });
            }
        }

        list.Sort((a, b) => string.Compare(a.nameZh, b.nameZh, StringComparison.Ordinal));
        return new IngredientCatalogDto { ingredients = list.ToArray() };
    }

    /** "core" / "custom" / "dlc02" / "dlc05" — mirrors foodGroupOf in build-catalog.mjs. */
    internal static string FoodGroupOf(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "core";
        if (assetPath.IndexOf("/CustomRecipes/", StringComparison.Ordinal) >= 0)
            return "custom";
        var m = System.Text.RegularExpressions.Regex.Match(assetPath, @"/(dlc\d+)/");
        if (m.Success)
            return m.Groups[1].Value;
        return "core";
    }

    /** Recipe family derived from the recipe id (mirrors recipeTypeOf in build-catalog.mjs). */
    internal static string RecipeTypeOf(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "other";
        var head = id.Split('_')[0];
        string mapped = null;
        switch (head)
        {
            case "Burger": mapped = "burger"; break;
            case "Burrito": mapped = "burrito"; break;
            case "Cake": mapped = "cake"; break;
            case "Fry":
            case "Fried": mapped = "fry"; break;
            case "Pasta": mapped = "pasta"; break;
            case "Pizza": mapped = "pizza"; break;
            case "Salad": mapped = "salad"; break;
            case "Steamed": mapped = "steamed"; break;
            case "Sushi": mapped = "sushi"; break;
            case "Kebob": mapped = "kebab"; break;
            case "Smoothie": mapped = "smoothie"; break;
            case "Breakfast": mapped = "breakfast"; break;
            case "Smores": mapped = "smores"; break;
            case "Mixed": mapped = "batter"; break;
            case "Mushroom": mapped = "pizza"; break;
            case "Soup": mapped = "soup"; break;
        }
        if (mapped == "cake" && id.IndexOf("Pancake", StringComparison.Ordinal) >= 0)
            return "pancake";
        if (mapped != null)
            return mapped;
        if (id.StartsWith("Fried", StringComparison.Ordinal) || id.StartsWith("Fry", StringComparison.Ordinal))
            return "fry";
        if (id.StartsWith("Mixed", StringComparison.Ordinal))
            return "batter";
        if (id.IndexOf("Pancake", StringComparison.Ordinal) >= 0)
            return "pancake";
        if (id.IndexOf("Soup", StringComparison.Ordinal) >= 0)
            return "soup";
        return "other";
    }

    public static RecipeCatalogDto ScanRecipes(string levelSet)
    {
        var list = new List<RecipeEntryDto>();
        var folders = new List<string>
        {
            "Assets/common01/food/Recipes",
            "Assets/common01/food/CustomRecipes",
            "Assets/common02/food/Recipes",
        };

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
                var custom = so as CustomRecipeSO;
                var isCustom = custom != null;

                string zh;
                string en;
                LayoutEditorManualLookup.TryGet(id, out zh, out en);

                string step;
                string[] ings;
                int ingCount;
                int cookCount;
                int score;
                if (isCustom)
                {
                    step = LayoutEditorRecipeKnowledge.CustomCookingStep(custom);
                    ings = LayoutEditorRecipeKnowledge.CustomIngredients(custom).ToArray();
                    LayoutEditorRecipeKnowledge.CustomStats(custom, out ingCount, out cookCount);
                    if (ingCount == 0) ingCount = ings.Length;
                    score = custom.score;
                }
                else
                {
                    var original = so as PseudoPrefabSORecipe;
                    if (LayoutEditorRecipeKnowledge.IsSkipped(id) ||
                        (original != null && LayoutEditorRecipeKnowledge.IsSkipped(original.prefabName)))
                        continue;
                    if (!LayoutEditorRecipeKnowledge.TryGetOriginal(id, out step, out ings) &&
                        (original == null || !LayoutEditorRecipeKnowledge.TryGetOriginal(original.prefabName + "_SO", out step, out ings)))
                        continue;
                    ingCount = ings.Length;
                    cookCount = LayoutEditorRecipeKnowledge.IsCookStep(step) ? 1 : 0;
                    score = original != null ? original.score : 0;
                }

                list.Add(new RecipeEntryDto
                {
                    guid = guid,
                    id = id,
                    nameZh = zh,
                    nameEn = en,
                    assetPath = path,
                    cookingStep = step,
                    ingredients = ings,
                    ingredientCount = ingCount,
                    cookingStepCount = cookCount,
                    score = score,
                    isCustom = isCustom,
                    group = FoodGroupOf(path),
                    type = RecipeTypeOf(id),
                    intermediate = score <= 0
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

        // Auto-populate allIngredients from selected recipes
        {
            var allIngs = new List<PseudoPrefabSO>();
            var seenIngIds = new HashSet<string>(StringComparer.Ordinal);
            var ingLookup = new Dictionary<string, PseudoPrefabSO>(StringComparer.Ordinal);

            foreach (var r in recipes)
            {
                List<string> ingIds = null;
                var custom = r as CustomRecipeSO;
                if (custom != null)
                {
                    ingIds = LayoutEditorRecipeKnowledge.CustomIngredients(custom);
                }
                else
                {
                    var original = r as PseudoPrefabSORecipe;
                    if (original != null)
                    {
                        string step;
                        string[] originalIngs;
                        if (LayoutEditorRecipeKnowledge.TryGetOriginal(original.prefabName + "_SO", out step, out originalIngs) ||
                            LayoutEditorRecipeKnowledge.TryGetOriginal(original.prefabName, out step, out originalIngs))
                        {
                            ingIds = new List<string>(originalIngs);
                        }
                    }
                }
                if (ingIds == null)
                    continue;
                foreach (var ingId in ingIds)
                {
                    if (string.IsNullOrEmpty(ingId) || !seenIngIds.Add(ingId))
                        continue;
                    PseudoPrefabSO ingSO;
                    if (!ingLookup.TryGetValue(ingId, out ingSO))
                    {
                        var ingGuids = AssetDatabase.FindAssets(ingId + " t:PseudoPrefabSO");
                        if (ingGuids.Length > 0)
                        {
                            var ingPath = AssetDatabase.GUIDToAssetPath(ingGuids[0]);
                            ingSO = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(ingPath);
                            ingLookup[ingId] = ingSO;
                        }
                    }
                    if (ingSO != null)
                        allIngs.Add(ingSO);
                }
            }
            info.allIngredients = allIngs.ToArray();
        }

        // Auto-populate optionalRecipeMatchListItems for pizza levels
        {
            bool hasPizza = false;
            foreach (var r in recipes)
            {
                var id = r.name;
                if (string.IsNullOrEmpty(id))
                {
                    var path = AssetDatabase.GetAssetPath(r);
                    id = string.IsNullOrEmpty(path) ? "" : System.IO.Path.GetFileNameWithoutExtension(path);
                }
                var type = RecipeTypeOf(id);
                if (type == "pizza")
                {
                    hasPizza = true;
                    break;
                }
            }
            if (hasPizza)
            {
                var existing = info.optionalRecipeMatchListItems != null
                    ? new HashSet<ScriptableObject>(info.optionalRecipeMatchListItems)
                    : new HashSet<ScriptableObject>();
                var optionalGuids = new[]
                {
                    "c8a3b9520d25f674a89e274226dee7cf", // Pizza_Optional_Uncooked_SO
                    "b38643b6c45e859479f6105f5d0ec839", // Pizza_Optional_Cooked_SO
                };
                foreach (var g in optionalGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                        if (so != null)
                            existing.Add(so);
                    }
                }
                info.optionalRecipeMatchListItems = new ScriptableObject[existing.Count];
                existing.CopyTo(info.optionalRecipeMatchListItems);
            }
        }

        EditorUtility.SetDirty(info);

        var manager = UnityEngine.Object.FindObjectOfType<PseudoPrefabManagerStub>();
        if (manager != null && manager.levelInfo == info)
            EditorUtility.SetDirty(manager);

        return null;
    }
}
