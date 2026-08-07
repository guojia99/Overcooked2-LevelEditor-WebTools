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
            foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(root, LayoutEditorLevelAdminApi.PseudoPrefabScriptGuid))
            {
                if (!seen.Add(asset.guid))
                    continue;

                var path = asset.assetPath;
                var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
                if (so == null)
                    continue;

                var id = Path.GetFileNameWithoutExtension(path);
                string nameZh;
                string nameEn;
                LayoutEditorManualLookup.TryGet(id, out nameZh, out nameEn);
                list.Add(new IngredientEntryDto
                {
                    guid = asset.guid,
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

    /** "core" / "custom" / "dlc02" / "dlc05" / "levelset" — mirrors foodGroupOf in build-catalog.mjs. */
    internal static string FoodGroupOf(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "core";
        if (assetPath.IndexOf("/custom_recipes/", StringComparison.Ordinal) >= 0)
            return "levelset";
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
        if (mapped == "cake" && id == "Cake_Chocolate_SO")
            return "pancake";
        if (mapped == "cake" && id == "Cake_Plain_SO")
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
            if (LayoutEditorLevelAdminApi.AssetFolderExists(levelData))
                folders.Add(levelData);

            var customRecipesDir = "Assets/LevelSets/" + levelSet + "/custom_recipes";
            if (LayoutEditorLevelAdminApi.AssetFolderExists(customRecipesDir))
                folders.Add(customRecipesDir);
        }

        var seen = new HashSet<string>();
        for (int f = 0; f < folders.Count; f++)
        {
            if (!LayoutEditorLevelAdminApi.AssetFolderExists(folders[f]))
                continue;

            // 自定义菜谱（CustomRecipeSO + Optional 子类）与原始菜谱（PseudoPrefabSORecipe）
            // 分别按脚本 guid 扫描（不依赖 AssetDatabase 索引）。
            var customAssets = LayoutEditorLevelAdminApi.ScanCustomRecipeAssets(folders[f]);
            var originalAssets = LayoutEditorLevelAdminApi.ScanAssetsByScript(folders[f], LayoutEditorLevelAdminApi.OriginalRecipeScriptGuid);
            for (int i = 0; i < customAssets.Count + originalAssets.Count; i++)
            {
                var asset = i < customAssets.Count ? customAssets[i] : originalAssets[i - customAssets.Count];
                if (!seen.Add(asset.guid))
                    continue;

                var path = asset.assetPath;
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
                var group = FoodGroupOf(path);
                if (group == "levelset")
                    LayoutEditorManualLookup.TryGetLevelSetName(levelSet, id, out zh, out en);
                else
                    LayoutEditorManualLookup.TryGet(id, out zh, out en);

                string step;
                string[] ings;
                string[] compositionIds = null;
                int ingCount;
                int cookCount;
                int score;
                if (isCustom)
                {
                    step = LayoutEditorRecipeKnowledge.CustomCookingStep(custom);
                    ings = LayoutEditorRecipeKnowledge.CustomIngredients(custom).ToArray();
                    compositionIds = DirectCompositionIds(custom);
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
                    guid = asset.guid,
                    id = id,
                    nameZh = zh,
                    nameEn = en,
                    assetPath = path,
                    cookingStep = step,
                    ingredients = ings,
                    compositionIds = compositionIds,
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

        foreach (var r in list)
        {
            r.cookingGroups = LayoutEditorRecipeKnowledge.ComputeCookingGroups(r, list);
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

    /// <summary>StreamingAssets/Windows 下是否存在该 bundle 文件（插件只把已构建的 bundle
    ///  写入 dependencies，避免宿主原版 PseudoPrefabManager 因缺失 bundle 抛异常）。</summary>
    private static bool BundleFileExists(string bundleName)
    {
        if (string.IsNullOrEmpty(bundleName))
            return false;
        var path = Path.Combine(Application.streamingAssetsPath, "Windows/" + bundleName).Replace('\\', '/');
        return File.Exists(path);
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

        // 自动把本关卡集的自定义菜谱 bundle 加入 dependencies：
        // 运行时 PseudoPrefabManager 只加载 dependencies 中的 bundle，
        // 缺少则自定义菜谱的模型/贴图引用无法解析（装盘空、材质红/灰）。
        // 只加入磁盘上已存在的 bundle：宿主原版 PseudoPrefabManager 对缺失 bundle 会抛
        // KeyNotFoundException，未构建（如自定义菜谱 bundle 构建前）时不能写进 dependencies。
        var pathParts = (update.levelInfoAssetPath ?? "").Replace('\\', '/').Split('/');
        if (pathParts.Length > 2 && pathParts[1] == "LevelSets")
        {
            var deps = new List<string>(info.dependencies ?? new string[0]);
            var customBundle = pathParts[2] + "/custom_recipes";
            if (System.Array.IndexOf(deps.ToArray(), customBundle) < 0 && BundleFileExists(customBundle))
                deps.Add(customBundle);

            // 自定义菜谱引用的外部资产 bundle（装盘容器等，如 Glass=bundle162）一并确保加载
            foreach (var r in recipes)
            {
                var custom = r as CustomRecipeSO;
                if (custom == null || custom.platingStepSO == null)
                    continue;
                var b = custom.platingStepSO.bundleName;
                if (!string.IsNullOrEmpty(b) && System.Array.IndexOf(deps.ToArray(), b) < 0 && BundleFileExists(b))
                    deps.Add(b);
            }

            info.dependencies = deps.ToArray();
        }

        // Auto-populate allIngredients: run the same "Auto Fill All Ingredients" logic
        // as the LevelInfoSO inspector button (story/include match lists + scene dispensers).
        LayoutEditorAllIngredientsFill.AutoFillIngredients(info);

        // (Disabled) Auto-populate allIngredients from selected recipes
        // Only register ingredients NOT in the core/original RecipeMatchList
        // (DLC/custom-exclusive ingredients only — core ingredients are already in the game's built-in matching)
        /*{
            var coreIngs = new HashSet<string>(StringComparer.Ordinal)
            {
                "ChoppedBunSO", "DoughSO", "MeatSO", "CheeseSO", "LettuceSO", "TomatoSO",
                "TortillaSO", "BurritoChickenSO", "BurritoMeatSO",
                "FlourSO", "EggSO", "HoneycombSO", "ChocolateSO", "CarrotSO",
                "PotatoSO", "NuggetChickenSO", "OnionSO", "PastaSO",
                "PepperoniSO", "ChickenSO", "CucumberSO", "FishSO", "PrawnSO",
                "SeaweedSO", "SushiRiceSO", "SushiFishSO", "SushiPrawnSO",
            };
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
                        var pathKey = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(original));
                        if (LayoutEditorRecipeKnowledge.TryGetOriginal(pathKey, out step, out originalIngs) ||
                            LayoutEditorRecipeKnowledge.TryGetOriginal(original.prefabName + "_SO", out step, out originalIngs) ||
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
                    if (string.IsNullOrEmpty(ingId))
                        continue;
                    // Skip core ingredients already covered by the game's built-in RecipeMatchList
                    if (coreIngs.Contains(ingId))
                        continue;
                    if (!seenIngIds.Add(ingId))
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
        }*/

        // Auto-populate optionalRecipeMatchListItems: 每次保存自动重建。
        // 规则：只有食材组成（无嵌套子菜谱）的菜谱（如煎蛋 = 鸡蛋）不需要注册——
        // 其匹配由食材 + 烹饪步骤天然覆盖；组合了其他菜谱的（如鸡蛋汉堡 = 煎蛋 + 面包）
        // 以及 DLC 原始菜谱必须注册，否则运行时内置 RecipeMatchList 无法匹配。
        {
            var existing = new HashSet<ScriptableObject>();
            foreach (var r in recipes)
            {
                string id = null;
                var path = AssetDatabase.GetAssetPath(r);
                if (!string.IsNullOrEmpty(path))
                    id = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(id))
                    continue;
                var type = RecipeTypeOf(id);
                var group = FoodGroupOf(path);
                var custom = r as CustomRecipeSO;
                List<string> customIngs = custom != null
                    ? LayoutEditorRecipeKnowledge.CustomIngredients(custom)
                    : null;

                if (custom != null)
                {
                    // 自定义菜谱：仅嵌套了其他菜谱（子菜谱/中间产物）的组合需要注册
                    if (HasSubRecipe(custom))
                        existing.Add(r);
                }
                else if (group.StartsWith("dlc", StringComparison.Ordinal))
                {
                    // DLC 原始菜谱：游戏内置匹配表不含，需注册
                    existing.Add(r);
                }

                // Pizza: add 自选披萨 optionals; mushroom pizza additionally needs 蘑菇披萨
                bool isPizza = type == "pizza";
                if (!isPizza && customIngs != null)
                    isPizza = customIngs.Contains("DLC05_Dough") || customIngs.Contains("DoughSO");
                if (isPizza)
                {
                    AddOptionalGuids(existing, new[] { "c8a3b9520d25f674a89e274226dee7cf", "b38643b6c45e859479f6105f5d0ec839" });
                    bool mushroom = id.IndexOf("Mushroom", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (customIngs != null && customIngs.Contains("MushroomSO"));
                    if (mushroom)
                        AddOptionalGuids(existing, new[] { "1072f0ef3ba328546a7a5bb84d983d6e" });
                }
            }
            info.optionalRecipeMatchListItems = new ScriptableObject[existing.Count];
            existing.CopyTo(info.optionalRecipeMatchListItems);
        }

        EditorUtility.SetDirty(info);

        var manager = UnityEngine.Object.FindObjectOfType<PseudoPrefabManagerStub>();
        if (manager != null && manager.levelInfo == info)
            EditorUtility.SetDirty(manager);

        return null;
    }

    private static void AddOptionalGuids(HashSet<ScriptableObject> existing, string[] guids)
    {
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (!string.IsNullOrEmpty(path))
            {
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so != null)
                    existing.Add(so);
            }
        }
    }

    /// <summary>自定义菜谱的组成里是否嵌套了其他菜谱（子菜谱/中间产物）。
    ///  纯食材组成的菜谱（如煎蛋 = 鸡蛋）无需注册 optionalRecipeMatchListItems。</summary>
    private static bool HasSubRecipe(CustomRecipeSO so)
    {
        if (so == null || so.compositionSOs == null)
            return false;
        foreach (var c in so.compositionSOs)
        {
            if (c is CustomRecipeSO)
                return true;
        }
        return false;
    }

    private static string[] DirectCompositionIds(CustomRecipeSO so)
    {
        if (so == null || so.compositionSOs == null)
            return null;
        var ids = new List<string>();
        foreach (var c in so.compositionSOs)
        {
            if (c == null)
                continue;
            var cp = AssetDatabase.GetAssetPath(c);
            if (!string.IsNullOrEmpty(cp))
                ids.Add(Path.GetFileNameWithoutExtension(cp));
        }
        return ids.ToArray();
    }
}
