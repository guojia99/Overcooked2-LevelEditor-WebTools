using System.Collections.Generic;
using LevelEditor;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Auto-populates LevelInfoSO.allIngredients from story/include RecipeMatchLists,
/// scene dispenser spawners, and custom recipe leaf ingredients.
/// Implemented directly inside the Layout Editor plugin so the plugin stays
/// self-contained (no reflection dependency on Assets/Editor/LevelInfoSOEditor.cs).
/// </summary>
public static class LayoutEditorAllIngredientsFill
{
    /// <summary>复刻宿主 LevelInfoSOEditor 的 "Fill All AudioDirectorySOs" 按钮：
    /// 把 common02 音频目录下的全部 PseudoPrefabSO 填进 audioDirectorySOs。
    /// 与宿主按钮一致扫描 common02（运行时 assetPath 用正斜杠，能从 bundle 内容加载），
    /// 插件侧防御：跳过 bundle 文件尚未构建的目录，避免宿主 PseudoPrefabManager 因缺失 bundle 抛异常。</summary>
    public static void FillAllAudioDirectorySOs(LevelInfoSO levelInfo)
    {
        if (levelInfo == null)
            return;
        const string folder = "Assets/common02/pseudo_prefab_so/audio/AudioDirectories";
        var list = new List<PseudoPrefabSO>();
        if (AssetDatabase.IsValidFolder(folder))
        {
            foreach (var guid in AssetDatabase.FindAssets("t:PseudoPrefabSO", new[] { folder }))
            {
                var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so == null)
                    continue;
                if (!string.IsNullOrEmpty(so.bundleName) && !LayoutEditorCatalogApi.BundleFileExists(so.bundleName))
                    continue;
                if (!list.Contains(so))
                    list.Add(so);
            }
        }
        levelInfo.audioDirectorySOs = list.ToArray();
    }

    public static void AutoFillIngredients(LevelInfoSO levelInfo)
    {
        List<PseudoPrefabSO> allIngredients = new List<PseudoPrefabSO>();

        // TheRecipeMatchList / includeRecipeMatchLists：收集内置食材节点（用于后续判重）。
        // 依赖 AssetBundle（bundle18 等），Editor 模式下可能未加载（KeyNotFoundException）——
        // 失败时跳过，不影响自定义菜谱与场景食材的收集。
        List<OrderDefinitionNode> allOrderDefinitionNodes = new List<OrderDefinitionNode>();
        try
        {
            if (!levelInfo.excludeStoryRecipeMatchList)
            {
                PseudoPrefabSO theRecipeMatchListSO = ScriptableObject.CreateInstance<PseudoPrefabSO>();
                theRecipeMatchListSO.bundleName = "bundle18";
                theRecipeMatchListSO.assetPath = "Assets/data/recipedata/TheRecipeMatchList.asset";
                RecipeMatchList recipeMatchList = PseudoPrefabManager.LoadAsset<RecipeMatchList>(theRecipeMatchListSO);
                foreach (OrderDefinitionNode orderDefinitionNode in recipeMatchList.m_recipes)
                    if (orderDefinitionNode is IngredientOrderNode || orderDefinitionNode is ItemOrderNode)
                        allOrderDefinitionNodes.Add(orderDefinitionNode);
                Object.DestroyImmediate(theRecipeMatchListSO);
            }
            if (!levelInfo.includeRecipeMatchLists.IsEmpty())
            {
                foreach (PseudoPrefabSO matchListSO in levelInfo.includeRecipeMatchLists)
                {
                    RecipeMatchList recipeMatchList = PseudoPrefabManager.LoadAsset<RecipeMatchList>(matchListSO);
                    foreach (OrderDefinitionNode orderDefinitionNode in recipeMatchList.m_recipes)
                        if (orderDefinitionNode is IngredientOrderNode || orderDefinitionNode is ItemOrderNode)
                            allOrderDefinitionNodes.Add(orderDefinitionNode);
                }
            }
        }
        catch { }

        // 场景食材生成器：直接收集 PseudoPrefabSO（不依赖 AssetBundle 加载与节点判重）
        foreach (PseudoPrefabDispenserStub dispenserStub in Object.FindObjectsOfType<PseudoPrefabDispenserStub>())
        {
            PseudoPrefabSO pseudo = dispenserStub.spawnerItemPrefabSO;
            if (pseudo == null || allIngredients.Contains(pseudo))
                continue;
            allIngredients.Add(pseudo);
        }

        // 自定义菜谱（含中间产物/子菜谱组成的递归展开）：把用到的全部叶食材
        // 主动加入 allIngredients，保证运行时食材生成器/锅具能产出与匹配。
        // 注：直接收集 PseudoPrefabSO（不依赖 AssetBundle 加载与 match-list 判重，
        // 与核心食材重复也无害——allIngredients 是附加匹配集）。
        if (levelInfo.recipes != null)
        {
            var customSeen = new HashSet<CustomRecipeSO>();
            foreach (ScriptableObject recipeSO in levelInfo.recipes)
            {
                if (recipeSO is CustomRecipeSO)
                    CollectCustomRecipeIngredients((CustomRecipeSO)recipeSO, allIngredients, customSeen);
            }
        }

        // 过滤：运行时宿主（LevelConfigSetup.SetupConfig / PseudoPrefabDispenser.Setup）
        // 会对 allIngredients 逐项 PseudoPrefabManager.LoadAsset<GameObject>(assetPath)，
        // 路径无法匹配 bundle 内容器时返回 null 并抛 NullReferenceException。
        // 原版关卡 allIngredients 恒为空（基础食材由游戏内置 RecipeMatchList 匹配），
        // 因此这里只保留"内置匹配未覆盖"且"assetPath 指向真实 prefab"的食材：
        //  - assetPath 为反斜杠旧格式（基础食材）一律不写入；
        //  - assetPath 不以 .prefab 结尾（bundle 中无该食材 prefab，如番茄酱/芥末/汽水）
        //    也无法加载，一并排除（这些浇头由游戏内置/场景内嵌匹配处理）。
        var filtered = new List<PseudoPrefabSO>();
        foreach (PseudoPrefabSO pseudo in allIngredients)
        {
            if (pseudo == null)
                continue;
            var bundlePath = pseudo.assetPath;
            if (string.IsNullOrEmpty(bundlePath))
                continue;
            if (bundlePath.IndexOf('\\') >= 0)
                continue;
            if (!bundlePath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                continue;
            filtered.Add(pseudo);
        }

        levelInfo.allIngredients = filtered.ToArray();
    }

    /// <summary>保存菜谱（SetLevelRecipes）专用：仅根据当前已选菜谱重建 allIngredients，
    ///  不扫描场景食材箱；覆盖写入，清除历史遗留的无关食材。</summary>
    public static void AutoFillIngredientsFromSelectedRecipes(LevelInfoSO levelInfo)
    {
        if (levelInfo == null)
        {
            return;
        }
        var allIngredients = new List<PseudoPrefabSO>();
        if (levelInfo.recipes != null)
        {
            var customSeen = new HashSet<CustomRecipeSO>();
            foreach (ScriptableObject recipeSO in levelInfo.recipes)
            {
                if (recipeSO is CustomRecipeSO)
                {
                    CollectCustomRecipeIngredients((CustomRecipeSO)recipeSO, allIngredients, customSeen);
                    continue;
                }
                var original = recipeSO as PseudoPrefabSORecipe;
                if (original == null)
                {
                    continue;
                }
                var pathKey = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(original));
                string step;
                string[] ings;
                if (!LayoutEditorRecipeKnowledge.TryGetOriginal(pathKey, out step, out ings) &&
                    !LayoutEditorRecipeKnowledge.TryGetOriginal(original.prefabName + "_SO", out step, out ings) &&
                    !LayoutEditorRecipeKnowledge.TryGetOriginal(original.prefabName, out step, out ings))
                {
                    continue;
                }
                foreach (var ingId in ings)
                {
                    var ingSo = LayoutEditorRoastTrayFill.LoadIngredientSo(ingId);
                    if (ingSo != null && !allIngredients.Contains(ingSo))
                    {
                        allIngredients.Add(ingSo);
                    }
                }
            }
        }
        var filtered = new List<PseudoPrefabSO>();
        foreach (PseudoPrefabSO pseudo in allIngredients)
        {
            if (pseudo == null)
            {
                continue;
            }
            var bundlePath = pseudo.assetPath;
            if (string.IsNullOrEmpty(bundlePath))
            {
                continue;
            }
            if (bundlePath.IndexOf('\\') >= 0)
            {
                continue;
            }
            if (!bundlePath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            filtered.Add(pseudo);
        }
        levelInfo.allIngredients = filtered.ToArray();
    }

    private static void CollectCustomRecipeIngredients(
        CustomRecipeSO recipe,
        List<PseudoPrefabSO> ingredients,
        HashSet<CustomRecipeSO> seen)
    {
        if (recipe == null || !seen.Add(recipe))
            return;
        if (recipe.compositionSOs == null)
            return;
        foreach (ScriptableObject comp in recipe.compositionSOs)
        {
            if (comp == null)
                continue;
            CustomRecipeSO sub = comp as CustomRecipeSO;
            if (sub != null)
            {
                CollectCustomRecipeIngredients(sub, ingredients, seen);
                continue;
            }
            PseudoPrefabSORecipe officialSub = comp as PseudoPrefabSORecipe;
            if (officialSub != null)
            {
                var pathKey = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(officialSub));
                string step;
                string[] ings;
                if (!LayoutEditorRecipeKnowledge.TryGetOriginal(pathKey, out step, out ings) &&
                    !LayoutEditorRecipeKnowledge.TryGetOriginal(officialSub.prefabName + "_SO", out step, out ings) &&
                    !LayoutEditorRecipeKnowledge.TryGetOriginal(officialSub.prefabName, out step, out ings))
                {
                    continue;
                }
                foreach (var ingId in ings)
                {
                    var ingSo = LayoutEditorRoastTrayFill.LoadIngredientSo(ingId);
                    if (ingSo != null && !ingredients.Contains(ingSo))
                    {
                        ingredients.Add(ingSo);
                    }
                }
                continue;
            }
            PseudoPrefabSO pseudo = comp as PseudoPrefabSO;
            if (pseudo == null)
                continue;
            if (!ingredients.Contains(pseudo))
                ingredients.Add(pseudo);
        }
    }
}
