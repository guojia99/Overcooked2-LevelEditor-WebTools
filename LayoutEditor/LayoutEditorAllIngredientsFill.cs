using System.Collections.Generic;
using LevelEditor;
using LevelEditorStub;
using UnityEngine;

/// <summary>
/// Auto-populates LevelInfoSO.allIngredients from story/include RecipeMatchLists,
/// scene dispenser spawners, and custom recipe leaf ingredients.
/// Implemented directly inside the Layout Editor plugin so the plugin stays
/// self-contained (no reflection dependency on Assets/Editor/LevelInfoSOEditor.cs).
/// </summary>
public static class LayoutEditorAllIngredientsFill
{
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

        levelInfo.allIngredients = allIngredients.ToArray();
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
            PseudoPrefabSO pseudo = comp as PseudoPrefabSO;
            if (pseudo == null)
                continue;
            if (!ingredients.Contains(pseudo))
                ingredients.Add(pseudo);
        }
    }
}
