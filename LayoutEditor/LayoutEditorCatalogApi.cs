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
            // 通用内容源库（Assets/common03）：与 common01/common02 同级直接扫描。
            "Assets/common03/Ingredients"
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

    /** "core" / "custom" / "dlcXX" / "levelset" — mirrors foodGroupOf in build-catalog.mjs.
     *  common03 通用内容按 dlc 子目录归 dlcXX，无 dlc 目录则归 core（无 web 分组）。 */
    internal static string FoodGroupOf(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "core";
        if (assetPath.IndexOf("/custom_recipes/", StringComparison.Ordinal) >= 0)
            return "levelset";
        // 旧 Web 拷贝目录（机制已废弃，仅兼容历史数据）：按通用内容处理
        // （无 dlc 目录 → core）。
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
        // md_* 套餐（组装类：成品/子产物 + 餐盘上菜）优先于 burger 子串判定。
        if (lower.StartsWith("md_", StringComparison.Ordinal))
            return "mealdeal";
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
            // 通用内容源库（Assets/common03）：与 common01/common02 同级直接扫描。
            "Assets/common03/Recipes"
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

                // common03 通用菜谱：用难度估算分覆盖资产里的 100（对齐游戏攻略：20×食材+难度加成）。
                // 中间产物（资产 score<=0，如烤棉花糖/冰淇淋）保持 0 分、不作为关卡菜谱。
                if (path.IndexOf("/common03/", StringComparison.Ordinal) >= 0 && score > 0)
                    score = LayoutEditorRecipeKnowledge.EstimateCommon03RecipeScore(id, step, ings);

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
                    intermediate = score <= 0,
                    mixing = isCustom && custom.type == CustomRecipeSO.RecipeType.Mixed
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

        // 关卡集名（注册 custom_recipes / Web 内置 bundle 依赖时用）。
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
                // 历史 Import 源库（Assets/Editor/...，已迁移到 Assets/common03）引用不可写入。
                if (path.IndexOf("/Editor/LayoutEditor/Import/", StringComparison.Ordinal) >= 0)
                {
                    LayoutEditorLog.LogWarning("[Recipes] 收到历史 Import 源 guid（源库已迁移 common03），丢弃: "
                        + Path.GetFileNameWithoutExtension(path) + " (" + g + ")");
                    dropped.Add(Path.GetFileNameWithoutExtension(path));
                    continue;
                }
                // common03 资产可直接写入 LevelInfo：随 common03 bundle 打包。
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

        // 注册已选 common03 菜谱的 bundle 依赖（common03 包 + 食材自身游戏 bundle）。
        if (levelSet != null)
            LayoutEditorCustomIngredients.EnsureWebDependencies(levelSet, info);

        // 注意：common03 菜谱直接引用 Assets/common03 内资产（guid 来自静态 JSON），
        // 随 common03 bundle 打包，不再有 custom_web 拷贝/安装步骤。

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
        // 以及 DLC/Web 原始菜谱必须注册，否则运行时内置 RecipeMatchList 无法匹配。
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
                    // DLC 原始菜谱（含 common03 通用内容按 dlc 子目录归组）：游戏内置匹配表不含，需注册。
                    existing.Add(r);
                }

                // Hotdog: 自由拼接（自选热狗）——注册游戏内置的可选热狗菜谱（optional_bun_*
                // / optional_frankfurter_* / optional_onions_* / optionalhotdogs）与酱料
                // （番茄酱/芥末酱，node 型食材，只能走 optionalRecipeMatchListItems，不能进
                // allIngredients——宿主 GetIngredientOrItemOrderNode 会按 GameObject 加载而崩溃）。
                // 只加与所选热狗同 DLC 的条目：dlc11 可选菜谱/酱料指向 bundle428/427，若关卡
                // dependencies 未含对应 bundle，运行时 LoadAsset 会抛 KeyNotFoundException。
                bool isHotdog = type == "hotdog"
                    || id.IndexOf("hotdog", StringComparison.OrdinalIgnoreCase) >= 0
                    || id.IndexOf("frankfurter", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isHotdog)
                {
                    bool isDlc11 = path.IndexOf("/dlc11/", StringComparison.Ordinal) >= 0
                        || id.IndexOf("dlc11", StringComparison.OrdinalIgnoreCase) >= 0;
                    AddHotdogOptionalRecipes(existing, isDlc11);
                    AddHotdogCondiments(existing, isDlc11);
                    AddHotdogBoiledFrankfurter(existing, isDlc11);
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

        // Auto-populate includeRecipeMatchLists: 按所选菜谱所属 DLC，自动填入该 DLC 的
        // recipematchlist（PseudoPrefabSO 资产，位于 common03/pseudo_prefab_so/matchlists/）。
        // 运行时 SetupConfig 会把它并入关卡匹配表（GetAllOrderNodes 取并集），一次带齐该 DLC 的
        // 整套匹配节点（食材/订单/可选自由拼接/套餐/烹饪步骤），避免手工逐项列 optionalRecipeMatchListItems。
        {
            var dlcSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in recipes)
            {
                var rp = AssetDatabase.GetAssetPath(r);
                var dlc = DlcOfPath(rp);
                if (!string.IsNullOrEmpty(dlc))
                    dlcSet.Add(dlc);
            }
            var includeLists = new List<PseudoPrefabSO>();
            foreach (var dlc in dlcSet)
            {
                var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(
                    "Assets/common03/pseudo_prefab_so/matchlists/" + dlc + "_recipematchlist.asset");
                if (so != null)
                    includeLists.Add(so);
            }
            info.includeRecipeMatchLists = includeLists.ToArray();
        }

        // 烤菜烤盘「默认能放」：把所选烤菜菜谱的叶食材追加为场景烤盘 stub 的额外食材
        // （覆盖「先摆放烤盘、后选菜谱/再保存」的顺序；与 SceneLayoutApplier.Apply 的
        // 调用幂等，只增不删）。
        LayoutEditorRoastTrayFill.EnsureRoastTrayIngredients(info);

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

    /// <summary>从资产路径提取所属 DLC（/dlcNN/ 子目录；combineddlc 特判）。
    ///  用于 includeRecipeMatchLists 自动填充与该 DLC 对应的 recipematchlist。</summary>
    private static string DlcOfPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;
        var m = System.Text.RegularExpressions.Regex.Match(assetPath, @"/(dlc\d{2})/");
        if (m.Success)
            return m.Groups[1].Value;
        if (assetPath.IndexOf("/combineddlc/", StringComparison.Ordinal) >= 0)
            return "combineddlc";
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

    /// <summary>Hotdog 自由拼接：注册 common03 里游戏内置的可选热狗菜谱
    ///  （optional_bun_* / optional_frankfurter_* / optional_onions_* / optionalhotdogs）。
    ///  它们在 optionalRecipeMatchListItems 中按 OrderDefinitionNode 加载（PseudoPrefabSORecipe
    ///  走 PseudoPrefabSO 分支），从而让游戏能匹配「自由组装」的热狗（任意面包/香肠/浇头组合）。
    ///  <paramref name="dlc11"/> 只扫 dlc11 变体，否则只扫 dlc08（避免引入关卡未依赖的 bundle）。</summary>
    private static void AddHotdogOptionalRecipes(HashSet<ScriptableObject> existing, bool dlc11)
    {
        string[] roots = dlc11
            ? new[] { "Assets/common03/Recipes/dlc11" }
            : new[] { "Assets/common03/Recipes/dlc08" };
        foreach (var root in roots)
        {
            if (!AssetDatabase.IsValidFolder(root))
                continue;
            foreach (var guid in AssetDatabase.FindAssets("t:PseudoPrefabSORecipe", new[] { root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(name) ||
                    name.IndexOf("optional", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so != null)
                    existing.Add(so);
            }
        }
    }

    /// <summary>Hotdog 酱料：番茄酱/芥末酱（dlc08 或 dlc11）的 node 型食材 SO。
    ///  只能进 optionalRecipeMatchListItems（宿主 allIngredients 加载路径按 GameObject
    ///  加载，node 型无 prefab 会返回 null 崩溃）；此处按 id 从 common03/Ingredients 解析。</summary>
    private static void AddHotdogCondiments(HashSet<ScriptableObject> existing, bool dlc11)
    {
        string[] rootAndIds = dlc11
            ? new[] { "Assets/common03/Ingredients/dlc11/dlc11_ketchup.asset",
                      "Assets/common03/Ingredients/dlc11/dlc11_mustard.asset" }
            : new[] { "Assets/common03/Ingredients/dlc08/ketchup.asset",
                      "Assets/common03/Ingredients/dlc08/mustard.asset" };
        foreach (var path in rootAndIds)
        {
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (so != null)
                existing.Add(so);
        }
    }

    /// <summary>煮热狗肠（boiledfrankfurter，热狗烹饪中间产物）：
    ///  它的 OrderDefinitionNode 自带 m_platingStep + m_platingPrefab（可单独装盘），
    ///  必须进匹配表，否则玩家煮熟的肠单独放上盘子时 Plate.CanPlaceOnPlate 的
    ///  GetOrderPlatingPrefab 找不到对应节点而无法放盘。</summary>
    private static void AddHotdogBoiledFrankfurter(HashSet<ScriptableObject> existing, bool dlc11)
    {
        string[] paths = dlc11
            ? new[] { "Assets/common03/Recipes/dlc11/dlc11_boiledfrankfurter.asset" }
            : new[] { "Assets/common03/Recipes/dlc08/boiledfrankfurter.asset" };
        foreach (var p in paths)
        {
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(p);
            if (so != null)
                existing.Add(so);
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
