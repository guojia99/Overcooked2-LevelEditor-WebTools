using System;
using System.Collections.Generic;
using System.IO;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

public static class LayoutEditorCatalogApi
{
    /// <summary>meta 直读 guid 与 AssetDatabase 注册 guid 脱同步（插件直写 meta /
    ///  陈旧内存注册）时强制重导入修复，返回 AssetDatabase 认可的 guid。
    ///  保存菜谱时 GUIDToAssetPath 依赖 AssetDatabase 映射，此处必须保证一致。</summary>
    private static string HealGuidDesync(string assetPath, string metaGuid)
    {
        var dbGuid = AssetDatabase.AssetPathToGUID(assetPath);
        if (dbGuid == metaGuid)
            return metaGuid;
        LayoutEditorLog.LogWarning("[Catalog] guid 脱同步（meta=" + metaGuid + ", AssetDatabase="
            + (string.IsNullOrEmpty(dbGuid) ? "<未注册>" : dbGuid) + "），强制重导入: " + assetPath);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        var after = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(after))
            LayoutEditorLog.LogWarning("[Catalog] 重导入后 guid 仍无法解析: " + assetPath);
        return string.IsNullOrEmpty(after) ? metaGuid : after;
    }

    public static IngredientCatalogDto ScanIngredients()
    {
        var list = new List<IngredientEntryDto>();
        var roots = new List<string>
        {
            "Assets/common01/food/Ingredients",
            "Assets/common02/food/Ingredients",
            // Web 内置源库：游戏 DLC 食材（保存时按需拷贝到关卡集 custom_web）
            "Assets/Editor/LayoutEditor/Import/Ingredients"
        };
        roots.AddRange(LayoutEditorLevelAdminApi.LevelSetCustomIngredientFolders());

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

                var guid = HealGuidDesync(path, asset.guid);
                var id = Path.GetFileNameWithoutExtension(path);
                string nameZh;
                string nameEn;
                LayoutEditorManualLookup.TryGet(id, out nameZh, out nameEn);
                var entry = new IngredientEntryDto
                {
                    guid = guid,
                    id = id,
                    nameZh = nameZh,
                    nameEn = nameEn,
                    assetPath = path,
                    group = FoodGroupOf(path)
                };
                // 数据保留全部条目（Web 内置源库 + 关卡集副本）：
                // 已放置的场景物品可能引用任一 guid，去重只在 UI 展示层进行。
                list.Add(entry);
            }
        }

        list.Sort((a, b) => string.Compare(a.nameZh, b.nameZh, StringComparison.Ordinal));
        return new IngredientCatalogDto { ingredients = list.ToArray() };
    }

    /** "core" / "custom" / "dlcXX" / "levelset" / "web" — mirrors foodGroupOf in build-catalog.mjs. */
    internal static string FoodGroupOf(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "core";
        // Web 内置源库（游戏 DLC 内容，保存时自动打包到关卡集）——必须在 dlc 正则前判断。
        if (assetPath.IndexOf("/Editor/LayoutEditor/Import/", StringComparison.Ordinal) >= 0)
            return "web";
        if (assetPath.IndexOf("/custom_recipes/", StringComparison.Ordinal) >= 0)
            return "levelset";
        // Web 拷贝目录（与自定义食材 custom_ingredients 分开）：始终归 Web内置 分组
        if (assetPath.IndexOf("/custom_web/", StringComparison.Ordinal) >= 0)
            return "web";
        // 已拷入关卡集的自定义食材（与 custom_recipes 同机制打包）。
        if (assetPath.IndexOf("/custom_ingredients/", StringComparison.Ordinal) >= 0)
            return "levelset";
        if (assetPath.IndexOf("/CustomRecipes/", StringComparison.Ordinal) >= 0)
            return "custom";
        var m = System.Text.RegularExpressions.Regex.Match(assetPath, @"/(dlc\d+)/");
        if (m.Success)
            return m.Groups[1].Value;
        return "core";
    }

    /** Recipe family derived from the recipe id (mirrors recipeTypeOf in build-catalog.mjs).
     *  New-DLC ids are lowercase with dlcXX_ prefixes; matching is substring-based
     *  (case-insensitive), ordered so the most specific family wins. */
    internal static string RecipeTypeOf(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "other";
        var lower = id.ToLowerInvariant();
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
        if (mapped == "cake" && lower.IndexOf("pancake", StringComparison.Ordinal) >= 0)
            return "pancake";
        if (mapped == "cake" && (id == "Cake_Chocolate_SO" || id == "Cake_Plain_SO"))
            return "pancake";
        if (mapped != null)
            return mapped;
        if (lower.IndexOf("fruitplatter", StringComparison.Ordinal) >= 0)
            return "fruitplatter";
        if (lower.IndexOf("moonpie", StringComparison.Ordinal) >= 0)
            return "moonpie";
        if (lower.IndexOf("christmaspudding", StringComparison.Ordinal) >= 0)
            return "pudding";
        if (lower.IndexOf("hotpot", StringComparison.Ordinal) >= 0)
            return "hotpot";
        if (lower.IndexOf("hotchoc", StringComparison.Ordinal) >= 0)
            return "hotchocolate";
        if (lower.IndexOf("sodafloat", StringComparison.Ordinal) >= 0 || lower.IndexOf("float", StringComparison.Ordinal) >= 0)
            return "float";
        if (lower.IndexOf("icecream", StringComparison.Ordinal) >= 0)
            return "icecream";
        if (lower.IndexOf("donut", StringComparison.Ordinal) >= 0)
            return "donut";
        if (lower.IndexOf("hotdog", StringComparison.Ordinal) >= 0 || lower.IndexOf("frankfurter", StringComparison.Ordinal) >= 0)
            return "hotdog";
        if (lower.IndexOf("fruitpie", StringComparison.Ordinal) >= 0)
            return "pie";
        // 烤棉花糖归入「棉花糖饼干」（smores）组（先于 roast 判定，避免被 roast 抢先）
        if (lower.IndexOf("roastedmarshmallow", StringComparison.Ordinal) >= 0)
            return "smores";
        if (lower.IndexOf("roast", StringComparison.Ordinal) >= 0)
            return "roast";
        if (lower.IndexOf("fried", StringComparison.Ordinal) >= 0)
            return "fry";
        if (lower.IndexOf("cheesestick", StringComparison.Ordinal) >= 0 || lower.IndexOf("onionrings", StringComparison.Ordinal) >= 0)
            return "fry";
        if (lower.IndexOf("smoothie", StringComparison.Ordinal) >= 0)
            return "smoothie";
        if (lower.IndexOf("kebob", StringComparison.Ordinal) >= 0)
            return "kebab";
        if (lower.IndexOf("burger", StringComparison.Ordinal) >= 0)
            return "burger";
        if (lower.IndexOf("pancake", StringComparison.Ordinal) >= 0)
            return "pancake";
        if (lower.IndexOf("salad", StringComparison.Ordinal) >= 0 ||
            (lower.IndexOf("cucumber", StringComparison.Ordinal) >= 0 && lower.IndexOf("onion", StringComparison.Ordinal) >= 0) ||
            (lower.IndexOf("tomato_", StringComparison.Ordinal) >= 0 && lower.IndexOf("onion", StringComparison.Ordinal) >= 0))
            return "salad";
        if (lower.IndexOf("soup", StringComparison.Ordinal) >= 0)
            return "soup";
        if (lower.IndexOf("pizza", StringComparison.Ordinal) >= 0)
            return "pizza";
        if (lower.IndexOf("pasta", StringComparison.Ordinal) >= 0)
            return "pasta";
        if (lower.IndexOf("sushi", StringComparison.Ordinal) >= 0)
            return "sushi";
        if (lower.IndexOf("burrito", StringComparison.Ordinal) >= 0)
            return "burrito";
        if (lower.IndexOf("smores", StringComparison.Ordinal) >= 0 ||
            lower.IndexOf("roastedmarshmallow", StringComparison.Ordinal) >= 0)
            return "smores";
        if (lower.IndexOf("breakfast", StringComparison.Ordinal) >= 0)
            return "breakfast";
        if (lower.IndexOf("steamed", StringComparison.Ordinal) >= 0)
            return "steamed";
        if (lower.IndexOf("mixed", StringComparison.Ordinal) >= 0)
            return "batter";
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
            // Web 内置源库：游戏 DLC 菜谱（保存时按需拷贝到关卡集 custom_web）
            "Assets/Editor/LayoutEditor/Import/Recipes"
        };

        if (!string.IsNullOrEmpty(levelSet))
        {
            var levelData = "Assets/LevelSets/" + levelSet + "/data";
            if (LayoutEditorLevelAdminApi.AssetFolderExists(levelData))
                folders.Add(levelData);

            var customRecipesDir = "Assets/LevelSets/" + levelSet + "/custom_recipes";
            if (LayoutEditorLevelAdminApi.AssetFolderExists(customRecipesDir))
                folders.Add(customRecipesDir);

            var customWebDir = "Assets/LevelSets/" + levelSet + "/custom_web";
            if (LayoutEditorLevelAdminApi.AssetFolderExists(customWebDir))
                folders.Add(customWebDir);
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

                var guid = HealGuidDesync(path, asset.guid);
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

                // Web 内置菜谱：用难度估算分覆盖资产里的 100（对齐游戏攻略：20×食材+难度加成）。
                // 中间产物（资产 score<=0，如烤棉花糖/冰淇淋）保持 0 分、不作为关卡菜谱。
                if (group == "web" && score > 0)
                    score = LayoutEditorRecipeKnowledge.EstimateWebRecipeScore(id, step, ings);

                list.Add(new RecipeEntryDto
                {
                    guid = guid,
                    id = id,
                    nameZh = zh,
                    nameEn = en,
                    assetPath = path,
                    cookingStep = step,
                    platingStep = isCustom && custom.platingStepSO != null
                        ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(custom.platingStepSO))
                        : "",
                    ingredients = ings,
                    compositionIds = compositionIds,
                    ingredientCount = ingCount,
                    cookingStepCount = cookCount,
                    score = score,
                    isCustom = isCustom,
                    group = group,
                    type = RecipeTypeOf(id),
                    intermediate = score <= 0
                });
            }
        }

        // 数据保留全部条目（Web 内置源库 + 关卡集副本）；去重只在 UI 展示层进行。
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
        {
            LayoutEditorLog.LogWarning("[Recipes] GetLevelRecipes: 未找到 LevelInfoSO, scene=" + sceneAssetPath);
            return new LevelRecipesDto { recipeGuids = new string[0], recipeIds = new string[0] };
        }

        var guids = new List<string>();
        var ids = new List<string>();
        var removedNonRecipe = new List<string>();
        if (info.recipes != null)
        {
            for (int i = 0; i < info.recipes.Length; i++)
            {
                var r = info.recipes[i];
                if (r == null)
                    continue;
                // 历史脏引用：recipes 里混入的非菜谱资产（如食材 SO）不返回给前端
                // （否则前端会把它们当已选菜谱回传），并在此从 LevelInfo 中清除。
                if (!(r is PseudoPrefabSORecipe) && !(r is CustomRecipeSO))
                {
                    removedNonRecipe.Add(AssetDatabase.GetAssetPath(r));
                    info.recipes[i] = null;
                    continue;
                }
                var path = AssetDatabase.GetAssetPath(r);
                if (!string.IsNullOrEmpty(path))
                {
                    guids.Add(AssetDatabase.AssetPathToGUID(path));
                    ids.Add(Path.GetFileNameWithoutExtension(path));
                }
            }
        }
        if (removedNonRecipe.Count > 0)
        {
            LayoutEditorLog.LogWarning("[Recipes] LevelInfo.recipes 混入非菜谱资产，已清理: "
                + string.Join(", ", removedNonRecipe.ToArray()));
            var kept = new List<ScriptableObject>();
            foreach (var r in info.recipes)
                if (r != null)
                    kept.Add(r);
            info.recipes = kept.ToArray();
            EditorUtility.SetDirty(info);
            AssetDatabase.SaveAssets();
        }

        LayoutEditorLog.Log("[Recipes] GetLevelRecipes: scene=" + sceneAssetPath
            + " -> levelInfo=" + AssetDatabase.GetAssetPath(info)
            + ", 已选 " + guids.Count + " 道: guid=[" + string.Join(", ", guids.ToArray())
            + "] id=[" + string.Join(", ", ids.ToArray()) + "]");

        return new LevelRecipesDto
        {
            levelInfoAssetPath = AssetDatabase.GetAssetPath(info),
            levelName = info.levelName,
            recipeGuids = guids.ToArray(),
            recipeIds = ids.ToArray()
        };
    }

    /// <summary>StreamingAssets/Windows 下是否存在该 bundle 文件（插件只把已构建的 bundle
    ///  写入 dependencies，避免宿主原版 PseudoPrefabManager 因缺失 bundle 抛异常）。</summary>
    public static bool BundleFileExists(string bundleName)
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

        LayoutEditorLog.Log("[Recipes] SetLevelRecipes: levelInfo=" + update.levelInfoAssetPath
            + ", 收到 guid " + (update.recipeGuids != null ? update.recipeGuids.Length : 0) + " 个: ["
            + (update.recipeGuids != null ? string.Join(", ", update.recipeGuids) : "") + "]");

        // 关卡集名（Import 源 guid → custom_web 已安装副本替换时用）。
        string levelSet = null;
        var pathParts = (update.levelInfoAssetPath ?? "").Replace('\\', '/').Split('/');
        if (pathParts.Length > 2 && pathParts[1] == "LevelSets")
            levelSet = pathParts[2];

        var recipes = new List<ScriptableObject>();
        var dropped = new List<string>();
        if (update.recipeGuids != null)
        {
            for (int i = 0; i < update.recipeGuids.Length; i++)
            {
                var g = update.recipeGuids[i];
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(path))
                {
                    LayoutEditorLog.LogWarning("[Recipes] guid 无法解析，丢弃（AssetDatabase 未导入或 guid 已失效）: " + g);
                    dropped.Add(g);
                    continue;
                }
                // Import 源库（Assets/Editor/...）资产只是参考，不能写入 LevelInfo（无法打包）。
                // 若本关卡集 custom_web 已有同 id 副本则替换为副本，否则丢弃。
                if (LayoutEditorCustomIngredients.IsImportAsset(path))
                {
                    var srcId = Path.GetFileNameWithoutExtension(path);
                    var copy = FindInstalledWebCopy(levelSet, "Recipes", srcId);
                    if (copy != null)
                    {
                        LayoutEditorLog.Log("[Recipes] Import 源 guid 已替换为 custom_web 副本: " + srcId + " (" + g + ")");
                        recipes.Add(copy);
                    }
                    else
                    {
                        LayoutEditorLog.LogWarning("[Recipes] 收到 Import 源 guid 且本关卡集无已安装副本，丢弃: " + srcId + " (" + g + ")");
                        dropped.Add(srcId);
                    }
                    continue;
                }
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so != null)
                {
                    // 类型校验：recipes 只接受菜谱资产（原始菜谱/自定义菜谱）。
                    // 历史脏引用（如食材 SO 混入 recipes）在此剔除，不计入错误——保存即清理。
                    if (!(so is PseudoPrefabSORecipe) && !(so is CustomRecipeSO))
                    {
                        LayoutEditorLog.LogWarning("[Recipes] guid 指向非菜谱资产，已从保存集中剔除: "
                            + path + " (" + g + ")");
                        continue;
                    }
                    recipes.Add(so);
                }
                else
                {
                    LayoutEditorLog.LogWarning("[Recipes] 资产加载失败，丢弃: " + path + " (" + g + ")");
                    dropped.Add(Path.GetFileNameWithoutExtension(path));
                }
            }
        }

        Undo.RecordObject(info, "Layout Editor Recipes");
        info.recipes = recipes.ToArray();

        // 自动把本关卡集的自定义菜谱 bundle 加入 dependencies：
        // 运行时 PseudoPrefabManager 只加载 dependencies 中的 bundle，
        // 缺少则自定义菜谱的模型/贴图引用无法解析（装盘空、材质红/灰）。
        // 只加入磁盘上已存在的 bundle：宿主原版 PseudoPrefabManager 对缺失 bundle 会抛
        // KeyNotFoundException，未构建（如自定义菜谱 bundle 构建前）时不能写进 dependencies。
        if (levelSet != null)
        {
            var deps = new List<string>(info.dependencies ?? new string[0]);
            var customBundle = levelSet + "/custom_recipes";
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

        // 注册已选 Web 内置菜谱的 bundle 依赖（custom_web 包 + 食材自身游戏 bundle）。
        if (levelSet != null)
            LayoutEditorCustomIngredients.EnsureWebDependencies(levelSet, info);

        // 注意：Web 内置菜谱不再在保存时自动拷入 custom_web（改为「内置菜谱管理」显式安装，
        // 见 /api/web-recipes/install）。选择菜谱只接受已安装副本或核心/自定义菜谱。

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

        // 立即落盘：仅 SetDirty 的改动会在域重载（改任意 C#）或忘记手动保存时丢失。
        AssetDatabase.SaveAssets();

        var writtenIds = new List<string>();
        foreach (var r in recipes)
        {
            var rp = AssetDatabase.GetAssetPath(r);
            writtenIds.Add(string.IsNullOrEmpty(rp) ? "?" : Path.GetFileNameWithoutExtension(rp));
        }
        LayoutEditorLog.Log("[Recipes] SetLevelRecipes 完成: 写入 " + recipes.Count + " 道 [" +
            string.Join(", ", writtenIds.ToArray()) + "]" +
            (dropped.Count > 0 ? ", 丢弃 " + dropped.Count + " 个" : ""));

        if (dropped.Count > 0)
            return "已写入 " + recipes.Count + " 道菜谱；以下菜谱未能写入（guid 无法解析，或未安装到本关卡集的内置菜谱）："
                + string.Join("、", dropped.ToArray());
        return null;
    }

    /// <summary>本关卡集 custom_web 中同 id 的已安装 Web 副本（Recipes/Ingredients 子目录）。
    ///  找不到返回 null。</summary>
    private static ScriptableObject FindInstalledWebCopy(string levelSet, string sub, string id)
    {
        if (string.IsNullOrEmpty(levelSet) || string.IsNullOrEmpty(id))
            return null;
        var copyPath = LayoutEditorCustomIngredients.CustomIngredientsDir(levelSet) + "/" + sub + "/" + id + ".asset";
        return AssetDatabase.LoadAssetAtPath<ScriptableObject>(copyPath);
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
