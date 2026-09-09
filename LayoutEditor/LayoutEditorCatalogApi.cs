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
            "Assets/common03/food/Ingredients"
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
        // commonW2 Burger大全共享库：独立分组（先于 /custom_recipes/ 判定）。
        if (assetPath.IndexOf("/commonW2/", StringComparison.Ordinal) >= 0)
            return "burger";
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
        // 正式版 common03 改名 md_* → DLC08_MD_*（dlcXX_md_ 前缀同样命中）。
        if (lower.StartsWith("md_", StringComparison.Ordinal)
            || (lower.StartsWith("dlc", StringComparison.Ordinal) && lower.Contains("_md_")))
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
            "Assets/common03/food/Recipes"
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

        // Burger大全（commonW2 共享汉堡库）：所有关卡集的关卡均可选用。
        if (LayoutEditorLevelAdminApi.AssetFolderExists(LayoutEditorLevelAdminApi.CommonW2RecipesDir))
            folders.Add(LayoutEditorLevelAdminApi.CommonW2RecipesDir);

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
                else if (group == "burger")
                {
                    // Burger大全（commonW2）：按 recipeName 查共享库 names.json。
                    var burgerNames = LayoutEditorLevelAdminApi.LoadCustomRecipeZhMap(LayoutEditorLevelAdminApi.CommonW2RecipesDir);
                    var nameKey = custom != null && !string.IsNullOrEmpty(custom.recipeName) ? custom.recipeName : id;
                    if (!burgerNames.TryGetValue(nameKey, out zh) || string.IsNullOrEmpty(zh))
                        zh = id;
                    en = nameKey;
                }
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
                    type = group == "burger" ? "burger" : RecipeTypeOf(id),
                    intermediate = score <= 0,
                    mixing = isCustom && custom.type == CustomRecipeSO.RecipeType.Mixed,
                    optionalKind = custom is CustomRecipeOptionalBurgerSO ? "burger"
                        : custom is CustomRecipeOptionalPizzaSO ? "pizza" : ""
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
            return new LevelRecipesDto
            {
                recipeGuids = new string[0],
                recipeIds = new string[0],
                optionalItems = new LevelOptionalItemDto[0],
                matchlists = new LevelMatchlistDto[0]
            };
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
            recipeIds = ids.ToArray(),
            optionalItems = BuildOptionalItemDtos(info),
            matchlists = BuildMatchlistDtos(info)
        };
    }

    /// <summary>LevelInfoSO.optionalRecipeMatchListItems → dto（菜谱管理 Optional tab 回显）。
    ///  kind 分类与 OptionalPresets 的候选一致，前端据此渲染类型徽章。</summary>
    private static LevelOptionalItemDto[] BuildOptionalItemDtos(LevelInfoSO info)
    {
        if (info == null || info.optionalRecipeMatchListItems == null)
            return new LevelOptionalItemDto[0];
        var list = new List<LevelOptionalItemDto>();
        var seen = new HashSet<ScriptableObject>();
        foreach (var so in info.optionalRecipeMatchListItems)
        {
            if (so == null || !seen.Add(so))
                continue;
            var p = AssetDatabase.GetAssetPath(so);
            if (string.IsNullOrEmpty(p))
                continue;
            var id = Path.GetFileNameWithoutExtension(p);
            list.Add(OptionalItemDtoFromSo(so));
        }
        return list.ToArray();
    }

    private static LevelOptionalItemDto OptionalItemDtoFromSo(ScriptableObject so)
    {
        if (so == null)
            return null;
        var p = AssetDatabase.GetAssetPath(so);
        if (string.IsNullOrEmpty(p))
            return null;
        var id = Path.GetFileNameWithoutExtension(p);
        return new LevelOptionalItemDto
        {
            guid = AssetDatabase.AssetPathToGUID(p),
            id = id,
            group = FoodGroupOf(p),
            kind = ClassifyOptionalItem(so, id)
        };
    }

    private static string ClassifyOptionalItem(ScriptableObject so, string id)
    {
        var lower = (id ?? "").ToLowerInvariant();
        if (so is CustomRecipeOptionalBurgerSO)
            return "burger-optional";
        if (so is CustomRecipeSO)
            return "custom-recipe";
        if (Array.IndexOf(PizzaOptionalGuids, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(so))) >= 0)
            return "pizza-optional";
        if (lower.Contains("ketchup") || lower.Contains("mustard"))
            return "condiment";
        if (lower.Contains("boiledfrankfurter"))
            return "boiledfrankfurter";
        if (so is PseudoPrefabSORecipe && lower.Contains("optional"))
            return "hotdog-optional";
        if (so is PseudoPrefabSORecipe)
            return "recipe";
        return "node";
    }

    /// <summary>LevelInfoSO.includeRecipeMatchLists → dto（菜谱管理 Matchlist tab 回显）。</summary>
    private static LevelMatchlistDto[] BuildMatchlistDtos(LevelInfoSO info)
    {
        if (info == null || info.includeRecipeMatchLists == null)
            return new LevelMatchlistDto[0];
        var list = new List<LevelMatchlistDto>();
        foreach (var ml in info.includeRecipeMatchLists)
        {
            if (ml == null)
                continue;
            var p = AssetDatabase.GetAssetPath(ml);
            if (string.IsNullOrEmpty(p))
                continue;
            list.Add(new LevelMatchlistDto
            {
                key = MatchlistKeyOfPath(p),
                guid = AssetDatabase.AssetPathToGUID(p),
                bundleName = ml.bundleName
            });
        }
        return list.ToArray();
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

        // optionalRecipeMatchListItems / includeRecipeMatchLists 不再自动重建：
        // 由菜谱管理的「Optional 参数管理」「Matchlist 管理」两个 tab 手动配置
        //（SetOptionalItems / SetMatchlists，支持披萨/Hotdog 一键填充候选），
        // 保存菜谱只写 recipes——已有条目不会被覆盖，也不会因取消勾选菜谱被清掉。

        // 仅根据当前已选菜谱覆盖重建 allIngredients（不扫描场景食材箱，清除无关遗留食材）。
        LayoutEditorAllIngredientsFill.AutoFillIngredientsFromSelectedRecipes(info);

        // 按当前 LevelInfo 引用覆盖重建 bundle 依赖（不保留旧菜谱遗留的 dependencies）。
        if (levelSet != null)
            LayoutEditorCustomIngredients.EnsureWebDependencies(levelSet, info, true);

        // 烤盘/火锅：按当前菜谱同步 allowedIngredientSOs（覆盖，不保留旧菜谱遗留项）。
        LayoutEditorRoastTrayFill.SyncRoastTrayIngredients(info);
        LayoutEditorHotPotFill.SyncHotPotIngredients(info);

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

    // ============================================================
    // Optional / Matchlist 手动管理（菜谱管理两个 tab；替代旧自动填充）
    // ============================================================

    /// <summary>commonW1 matchlist 包装白名单（key → Assets/commonW1/.../core/matchlists/&lt;key&gt;_recipematchlist.asset）。</summary>
    private static readonly string[] MatchlistKeys =
    {
        "dlc02", "dlc03", "dlc04", "dlc05", "dlc07",
        "dlc08", "dlc09", "dlc10", "dlc11", "dlc13", "combineddlc",
    };

    /// <summary>自选披萨 optional 部件 guid（前 2 个通用；第 3 个为蘑菇披萨变体）。
    ///  与旧 SetLevelRecipes 自动填充同源。</summary>
    private static readonly string[] PizzaOptionalGuids =
    {
        "c8a3b9520d25f674a89e274226dee7cf",
        "b38643b6c45e859479f6105f5d0ec839",
        "1072f0ef3ba328546a7a5bb84d983d6e",
    };

    private static string MatchlistWrapperPath(string key)
    {
        return "Assets/commonW1/pseudo_prefab_so/core/matchlists/" + key + "_recipematchlist.asset";
    }

    private static string MatchlistKeyOfPath(string assetPath)
    {
        var name = Path.GetFileNameWithoutExtension(assetPath ?? "");
        const string suffix = "_recipematchlist";
        if (name.EndsWith(suffix, StringComparison.Ordinal))
            name = name.Substring(0, name.Length - suffix.Length);
        return name;
    }

    /// <summary>一键填充候选：hotdog（按 DLC 两套：可选菜谱 + 酱料 + 水煮香肠）与披萨部件。
    ///  guid 与旧自动填充逻辑同源（CollectHotdog* / PizzaOptionalGuids），
    ///  web 端「披萨/Hotdog 一键填充」按钮据此把候选加进列表，是否写回由用户决定。</summary>
    public static OptionalPresetsDto GetOptionalPresets()
    {
        var items = new List<OptionalPresetItemDto>();
        var hot08 = CollectHotdogSet(false);
        var hot11 = CollectHotdogSet(true);
        var hot08Guids = new List<string>();
        var hot11Guids = new List<string>();

        Action<ScriptableObject, string, string> add = (so, kind, zh) =>
        {
            var p = AssetDatabase.GetAssetPath(so);
            if (string.IsNullOrEmpty(p))
                return;
            var g = AssetDatabase.AssetPathToGUID(p);
            var id = Path.GetFileNameWithoutExtension(p);
            items.Add(new OptionalPresetItemDto
            {
                guid = g,
                id = id,
                group = FoodGroupOf(p),
                kind = kind,
                nameZh = zh ?? ""
            });
        };
        foreach (var so in hot08)
        {
            add(so, PresetKindOf(so), PresetZhOf(so));
            var g = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(so));
            if (!string.IsNullOrEmpty(g))
                hot08Guids.Add(g);
        }
        foreach (var so in hot11)
        {
            add(so, PresetKindOf(so), PresetZhOf(so));
            var g = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(so));
            if (!string.IsNullOrEmpty(g))
                hot11Guids.Add(g);
        }
        foreach (var g in PizzaOptionalGuids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (string.IsNullOrEmpty(p))
                continue;
            items.Add(new OptionalPresetItemDto
            {
                guid = g,
                id = Path.GetFileNameWithoutExtension(p),
                group = FoodGroupOf(p),
                kind = "pizza-optional",
                nameZh = ""
            });
        }

        // Burger大全（commonW2）组装定义：汉堡关卡的 optionalRecipeMatchListItems 候选。
        if (LayoutEditorLevelAdminApi.AssetFolderExists(LayoutEditorLevelAdminApi.CommonW2RecipesDir))
        {
            var zhMap = LayoutEditorLevelAdminApi.LoadCustomRecipeZhMap(LayoutEditorLevelAdminApi.CommonW2RecipesDir);
            foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(
                         LayoutEditorLevelAdminApi.CommonW2RecipesDir,
                         LayoutEditorLevelAdminApi.OptionalBurgerScriptGuid))
            {
                var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(asset.assetPath);
                var id = Path.GetFileNameWithoutExtension(asset.assetPath);
                var nameKey = so != null && !string.IsNullOrEmpty(so.recipeName) ? so.recipeName : id;
                string zh;
                if (!zhMap.TryGetValue(nameKey, out zh) || string.IsNullOrEmpty(zh))
                    zh = id;
                items.Add(new OptionalPresetItemDto
                {
                    guid = asset.guid,
                    id = id,
                    group = "burger",
                    kind = "burger-optional",
                    nameZh = zh
                });
            }
        }

        return new OptionalPresetsDto
        {
            items = items.ToArray(),
            pizzaFillGuids = new[] { PizzaOptionalGuids[0], PizzaOptionalGuids[1] },
            pizzaMushroomGuids = new[] { PizzaOptionalGuids[2] },
            hotdogFillGuidsDlc08 = hot08Guids.ToArray(),
            hotdogFillGuidsDlc11 = hot11Guids.ToArray(),
        };
    }

    /// <summary>按所选成品汉堡夹心全集同步本关 BurgerOptional，并返回 optional 候选 guid：
    ///  [BurgerOptional] + CustomRecipeSO 中间产物。</summary>
    public static string[] ComputeBurgerOptionalFillGuids(string levelInfoAssetPath, string[] selectedRecipeGuids)
    {
        var intermediates = new HashSet<ScriptableObject>();
        var fillerGuids = new HashSet<string>(StringComparer.Ordinal);

        string levelSet = null;
        var pathParts = (levelInfoAssetPath ?? "").Replace('\\', '/').Split('/');
        if (pathParts.Length > 2 && pathParts[1] == "LevelSets")
            levelSet = pathParts[2];

        var selectedBurgers = new List<CustomRecipeSO>();
        if (selectedRecipeGuids != null)
        {
            for (int i = 0; i < selectedRecipeGuids.Length; i++)
            {
                var g = selectedRecipeGuids[i];
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(path))
                {
                    LayoutEditorLog.LogWarning("[Optional] 汉堡填充：菜谱 guid 无法解析: " + g);
                    continue;
                }
                var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(path);
                if (so == null || !LayoutEditorBurgerApi.IsFinishedBurgerRecipe(so))
                    continue;
                selectedBurgers.Add(so);
                CollectCustomSubRecipesFromBurger(so, intermediates, fillerGuids);
            }
        }

        if (selectedBurgers.Count == 0)
            return new string[0];

        CustomRecipeOptionalBurgerSO levelOptional;
        var createErr = LayoutEditorBurgerApi.FindOrCreateLevelBurgerOptional(
            levelInfoAssetPath, levelSet, out levelOptional);
        if (createErr != null)
        {
            LayoutEditorLog.LogWarning("[Optional] 汉堡填充：" + createErr);
            return new string[0];
        }

        LayoutEditorBurgerApi.SyncLevelBurgerOptionalFromBurgers(levelOptional, selectedBurgers);

        if (!LayoutEditorBurgerApi.BurgerOptionalHasFillerLayers(levelOptional))
        {
            LayoutEditorLog.LogWarning("[Optional] 汉堡填充：所选汉堡均无夹心层（纯面包），无需注册 BurgerOptional");
            return new string[0];
        }

        var guids = new List<string>();
        var seenGuids = new HashSet<string>(StringComparer.Ordinal);
        AppendOptionalGuid(levelOptional, guids, seenGuids);
        foreach (var sub in intermediates)
        {
            if (sub == null || sub == levelOptional)
                continue;
            AppendOptionalGuid(sub, guids, seenGuids);
        }

        LayoutEditorLog.Log("[Optional] ComputeBurgerOptionalFillGuids: " + guids.Count + " 条 ["
            + string.Join(", ", guids.ToArray()) + "]");
        return guids.ToArray();
    }

    private static void AppendOptionalGuid(ScriptableObject so, List<string> guids, HashSet<string> seenGuids)
    {
        if (so == null)
            return;
        var p = AssetDatabase.GetAssetPath(so);
        if (string.IsNullOrEmpty(p))
            return;
        var guid = AssetDatabase.AssetPathToGUID(p);
        if (string.IsNullOrEmpty(guid) || !seenGuids.Add(guid))
            return;
        guids.Add(guid);
    }

    public static BurgerOptionalComputeResultDto ComputeBurgerOptionalFill(BurgerOptionalComputeRequestDto req)
    {
        if (req == null)
            return new BurgerOptionalComputeResultDto
            {
                guids = new string[0],
                items = new LevelOptionalItemDto[0]
            };
        var guids = ComputeBurgerOptionalFillGuids(req.levelInfoAssetPath, req.recipeGuids);
        var items = new List<LevelOptionalItemDto>();
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path))
                continue;
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            var dto = OptionalItemDtoFromSo(so);
            if (dto != null)
                items.Add(dto);
        }
        return new BurgerOptionalComputeResultDto
        {
            guids = guids,
            items = items.ToArray()
        };
    }

    private static void CollectCustomSubRecipesFromBurger(
        CustomRecipeSO recipe,
        HashSet<ScriptableObject> resultSet,
        HashSet<string> fillerGuids)
    {
        foreach (var c in recipe.compositionSOs ?? new ScriptableObject[0])
        {
            if (c == null)
                continue;
            var cp = AssetDatabase.GetAssetPath(c);
            if (string.IsNullOrEmpty(cp))
                continue;
            var id = Path.GetFileNameWithoutExtension(cp);
            if (LayoutEditorBurgerApi.IsBurgerBunId(id))
                continue;
            var g = AssetDatabase.AssetPathToGUID(cp);
            if (!string.IsNullOrEmpty(g))
                fillerGuids.Add(g);
            var sub = c as CustomRecipeSO;
            if (sub == null)
                continue;
            if (resultSet.Add(sub))
                AddNestedCustomSubRecipes(sub, resultSet);
        }
    }

    private static string PresetKindOf(ScriptableObject so)
    {
        var p = AssetDatabase.GetAssetPath(so);
        return ClassifyOptionalItem(so, Path.GetFileNameWithoutExtension(p ?? ""));
    }

    private static string PresetZhOf(ScriptableObject so)
    {
        var lower = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(so) ?? "").ToLowerInvariant();
        if (lower.Contains("ketchup")) return "番茄酱";
        if (lower.Contains("mustard")) return "芥末酱";
        if (lower.Contains("boiledfrankfurter")) return "水煮热狗肠";
        return "";
    }

    /// <summary>覆盖写入 optionalRecipeMatchListItems（菜谱管理 Optional tab 的「写回」）。
    ///  guid 解析失败的条目跳过并列名回告警；写后重建 bundle 依赖（EnsureWebDependencies
    ///  含 optional 条目扫描）并立即落盘。返回 null = 成功，否则为告警文本。</summary>
    public static string SetOptionalItems(LevelOptionalItemsUpdateDto update)
    {
        if (update == null || string.IsNullOrEmpty(update.levelInfoAssetPath))
            return "Missing levelInfoAssetPath.";
        var info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(update.levelInfoAssetPath);
        if (info == null)
            return "LevelInfoSO not found.";

        var arr = new List<ScriptableObject>();
        var dropped = new List<string>();
        var seen = new HashSet<UnityEngine.Object>();
        if (update.guids != null)
        {
            foreach (var g in update.guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var so = string.IsNullOrEmpty(path)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null)
                {
                    dropped.Add(string.IsNullOrEmpty(path) ? g : Path.GetFileNameWithoutExtension(path));
                    continue;
                }
                if (seen.Add(so))
                    arr.Add(so);
            }
        }

        Undo.RecordObject(info, "Layout Editor Optional Items");
        info.optionalRecipeMatchListItems = arr.ToArray();
        FinalizeLevelInfoWrite(info, update.levelInfoAssetPath);

        LayoutEditorLog.Log("[Optional] SetOptionalItems 完成: 写入 " + arr.Count + " 条 ["
            + string.Join(", ", arr.ConvertAll(x => x.name).ToArray()) + "]"
            + (dropped.Count > 0 ? ", 跳过 " + dropped.Count + " 条" : ""));
        if (dropped.Count > 0)
            return "已写入 " + arr.Count + " 条；以下条目 guid 无法解析，已跳过：" + string.Join("、", dropped.ToArray());
        return null;
    }

    /// <summary>覆盖写入 includeRecipeMatchLists（菜谱管理 Matchlist tab 的「写回」）。
    ///  key 不在白名单（MatchlistKeys）的跳过并列名。返回 null = 成功，否则为告警文本。</summary>
    public static string SetMatchlists(LevelMatchlistsUpdateDto update)
    {
        if (update == null || string.IsNullOrEmpty(update.levelInfoAssetPath))
            return "Missing levelInfoAssetPath.";
        var info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(update.levelInfoAssetPath);
        if (info == null)
            return "LevelInfoSO not found.";

        var arr = new List<PseudoPrefabSO>();
        var dropped = new List<string>();
        if (update.keys != null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in update.keys)
            {
                if (string.IsNullOrEmpty(key) || !seen.Add(key))
                    continue;
                if (Array.IndexOf(MatchlistKeys, key) < 0)
                {
                    dropped.Add(key);
                    continue;
                }
                var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(MatchlistWrapperPath(key));
                if (so == null)
                {
                    dropped.Add(key + "（包装资产缺失）");
                    continue;
                }
                arr.Add(so);
            }
        }

        Undo.RecordObject(info, "Layout Editor Matchlists");
        info.includeRecipeMatchLists = arr.ToArray();
        FinalizeLevelInfoWrite(info, update.levelInfoAssetPath);

        LayoutEditorLog.Log("[Matchlist] SetMatchlists 完成: 写入 " + arr.Count + " 个 ["
            + string.Join(", ", arr.ConvertAll(x => x.prefabName).ToArray()) + "]"
            + (dropped.Count > 0 ? ", 跳过 " + dropped.Count + " 个" : ""));
        if (dropped.Count > 0)
            return "已写入 " + arr.Count + " 个；以下 key 无效或资产缺失，已跳过：" + string.Join("、", dropped.ToArray());
        return null;
    }

    /// <summary>LevelInfo 写入收尾：重建 bundle 依赖 + SetDirty（含 PseudoPrefabManagerStub）
    ///  + 立即 SaveAssets（防域重载丢失）。与 SetLevelRecipes 尾部同款。</summary>
    private static void FinalizeLevelInfoWrite(LevelInfoSO info, string levelInfoAssetPath)
    {
        string levelSet = null;
        var pathParts = (levelInfoAssetPath ?? "").Replace('\\', '/').Split('/');
        if (pathParts.Length > 2 && pathParts[1] == "LevelSets")
            levelSet = pathParts[2];
        if (levelSet != null)
            LayoutEditorCustomIngredients.EnsureWebDependencies(levelSet, info, true);
        EditorUtility.SetDirty(info);
        var manager = UnityEngine.Object.FindObjectOfType<PseudoPrefabManagerStub>();
        if (manager != null && manager.levelInfo == info)
            EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();
    }

    /// <summary>Hotdog 一键填充集合（可选菜谱 + 酱料 + 水煮香肠，按 DLC 两套）。
    ///  GetOptionalPresets 据此生成候选 guid；旧自动填充逻辑同源。</summary>
    private static List<ScriptableObject> CollectHotdogSet(bool dlc11)
    {
        var set = new HashSet<ScriptableObject>();
        CollectHotdogOptionalRecipes(set, dlc11);
        CollectHotdogCondiments(set, dlc11);
        CollectHotdogBoiledFrankfurter(set, dlc11);
        return new List<ScriptableObject>(set);
    }

    /// <summary>Hotdog 自由拼接：common03 里游戏内置的可选热狗菜谱
    ///  （optional_bun_* / optional_frankfurter_* / optional_onions_* / optionalhotdogs）。
    ///  它们在 optionalRecipeMatchListItems 中按 OrderDefinitionNode 加载（PseudoPrefabSORecipe
    ///  走 PseudoPrefabSO 分支），从而让游戏能匹配「自由组装」的热狗（任意面包/香肠/浇头组合）。
    ///  <paramref name="dlc11"/> 只扫 dlc11 变体，否则只扫 dlc08（避免引入关卡未依赖的 bundle）。</summary>
    private static void CollectHotdogOptionalRecipes(HashSet<ScriptableObject> existing, bool dlc11)
    {
        string[] roots = dlc11
            ? new[] { "Assets/common03/food/Recipes/dlc11" }
            : new[] { "Assets/common03/food/Recipes/dlc08" };
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
    private static void CollectHotdogCondiments(HashSet<ScriptableObject> existing, bool dlc11)
    {
        string[] rootAndIds = dlc11
            ? new[] { "Assets/commonW1/pseudo_prefab_so/dlc11/food/dlc11_ketchup.asset",
                      "Assets/commonW1/pseudo_prefab_so/dlc11/food/dlc11_mustard.asset" }
            : new[] { "Assets/common03/food/Ingredients/dlc08/DLC08_Ketchup.asset",
                      "Assets/common03/food/Ingredients/dlc08/DLC08_Mustard.asset" };
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
    private static void CollectHotdogBoiledFrankfurter(HashSet<ScriptableObject> existing, bool dlc11)
    {
        string[] paths = dlc11
            ? new[] { "Assets/commonW1/pseudo_prefab_so/dlc11/food/dlc11_boiledfrankfurter.asset" }
            : new[] { "Assets/common03/food/Recipes/dlc08/DLC08_boiledfrankfurter.asset" };
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

    /// <summary>登记组成里的自定义子菜谱（面糊等中间产物），否则运行时匹配表无法识别
    ///  搅拌产物节点（参考热狗关 AddHotdogBoiledFrankfurter）。</summary>
    private static void AddNestedCustomSubRecipes(CustomRecipeSO so, HashSet<ScriptableObject> existing)
    {
        if (so == null || so.compositionSOs == null)
            return;
        foreach (var c in so.compositionSOs)
        {
            CustomRecipeSO sub = c as CustomRecipeSO;
            if (sub != null && existing.Add(sub))
                AddNestedCustomSubRecipes(sub, existing);
        }
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
