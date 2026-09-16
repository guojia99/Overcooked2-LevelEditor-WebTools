using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DLC → RecipeMatchList 自动映射（2026-09-15）。
///
/// 背景（早餐汉堡只能用面包接、不能用盘子接）：
///  汉堡夹心若引用官方 DLC 菜谱节点（如 commonW2 的 BreakfastLettuceBurger →
///  common02/food/Recipes/dlc05/Breakfast_Bacon_Egg，PseudoPrefabSORecipe/bundle247），
///  该节点既不会被 CollectCustomSubRecipesFromBurger 收进 optionalRecipeMatchListItems
///  （那里只收 CustomRecipeSO），includeRecipeMatchLists 又自「菜谱管理手动化」改版后
///  不再自动重建（见 LayoutEditorCatalogApi.SetLevelRecipes 注释），于是
///  LevelConfigSetup.SetupConfig 组出的 RecipeMatchList.m_recipes 里没有这个节点。
///  结果：盘子走 GameUtils.GetOrderPlatingPrefab 匹配失败拿不起来；而面包能接，
///  是因为 RecipeHelper 直接用 BurgerOptional.optionalSOs 造
///  PreparationContainer.m_containerRestrictions，绕过了匹配表。
///
/// 解决：按关卡实际引用的资产反推所属 DLC，自动并入对应的 matchlist 包装。
///  映射依据 = PseudoPrefabSO.assetPath 的 bundle 内实路径前缀
///  `Assets/downloadablecontent/&lt;dlcNN&gt;/...`（全库实测无例外）；
///  未命中前缀 = 本传内容（story 表由 CampaignLevelConfig 模板默认并入，无需处理）。
///
/// 铁律：
///  1. 只增不删 —— 用户在菜谱管理 Matchlist tab 手填的条目一律保留；
///  2. 必须 BundleFileExists 守卫 —— 包装指向的 bundle 不在 StreamingAssets 时禁止写入，
///     否则运行时 PseudoPrefabManager.LoadAsset&lt;RecipeMatchList&gt; 抛 KeyNotFoundException；
///  3. 必须在 EnsureWebDependencies 之前调用 —— 后者靠读 includeRecipeMatchLists
///     注册 bundle 依赖，晚了就漏包。
/// </summary>
public static class LayoutEditorMatchlistMap
{
    /// <summary>bundle 内实路径的 DLC 前缀。大小写不敏感（本地包装大多小写，
    ///  个别历史资产为 `Assets/DownloadableContent/...`）。</summary>
    private static readonly Regex DlcPathRegex = new Regex(
        @"^assets[\\/]downloadablecontent[\\/](dlc\d+)[\\/]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>单个资产 → matchlist key（"dlc05" 等）。
    ///  null = 本传内容或无法判定（story 表默认已并入，无需补）。</summary>
    internal static string MatchlistKeyOfAsset(UnityEngine.Object asset)
    {
        if (asset == null)
            return null;
        var pseudo = asset as PseudoPrefabSO;
        if (pseudo == null)
            return null;
        return MatchlistKeyOfBundlePath(pseudo.assetPath);
    }

    /// <summary>bundle 内实路径 → matchlist key。</summary>
    internal static string MatchlistKeyOfBundlePath(string bundleAssetPath)
    {
        if (string.IsNullOrEmpty(bundleAssetPath))
            return null;
        var m = DlcPathRegex.Match(bundleAssetPath);
        if (!m.Success)
            return null;
        return m.Groups[1].Value.ToLowerInvariant();
    }

    /// <summary>递归收集本关需要的 matchlist key（尚未按白名单/bundle 过滤）。</summary>
    internal static HashSet<string> CollectRequiredMatchlistKeys(LevelInfoSO info)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (info == null)
            return keys;
        var visited = new HashSet<UnityEngine.Object>();
        CollectFrom(info.recipes, keys, visited);
        CollectFrom(info.optionalRecipeMatchListItems, keys, visited);
        CollectFromPseudo(info.allIngredients, keys, visited);
        CollectFromPseudo(info.allCookingSteps, keys, visited);
        return keys;
    }

    private static void CollectFrom(ScriptableObject[] arr, HashSet<string> keys, HashSet<UnityEngine.Object> visited)
    {
        if (arr == null)
            return;
        for (int i = 0; i < arr.Length; i++)
            Walk(arr[i], keys, visited);
    }

    private static void CollectFromPseudo(PseudoPrefabSO[] arr, HashSet<string> keys, HashSet<UnityEngine.Object> visited)
    {
        if (arr == null)
            return;
        for (int i = 0; i < arr.Length; i++)
            Walk(arr[i], keys, visited);
    }

    /// <summary>单节点遍历：PseudoPrefabSO 是叶子（直接取 key）；
    ///  CustomRecipeSO 下钻组成/可选夹心/面包/模型/烹饪步骤/装盘容器。
    ///  visited 去重同时防环（自定义菜谱可互相引用）。</summary>
    private static void Walk(ScriptableObject so, HashSet<string> keys, HashSet<UnityEngine.Object> visited)
    {
        if (so == null || !visited.Add(so))
            return;

        var key = MatchlistKeyOfAsset(so);
        if (!string.IsNullOrEmpty(key))
            keys.Add(key);

        var custom = so as CustomRecipeSO;
        if (custom == null)
            return; // PseudoPrefabSO / PseudoPrefabSORecipe = 叶子

        CollectFrom(custom.compositionSOs, keys, visited);
        CollectFrom(custom.optionalSOs, keys, visited);
        Walk(custom.modelSO, keys, visited);
        Walk(custom.cookingStepSO, keys, visited);
        Walk(custom.platingStepSO, keys, visited);

        var burger = so as CustomRecipeOptionalBurgerSO;
        if (burger != null)
        {
            Walk(burger.bunSO, keys, visited);
            CollectFromPseudo(burger.ingredientModelSOs, keys, visited);
        }

        var pizza = so as CustomRecipeOptionalPizzaSO;
        if (pizza != null)
        {
            Walk(pizza.doughSO, keys, visited);
            CollectFromPseudo(pizza.rawPizzaIngredientPrefabSOs, keys, visited);
            CollectFromPseudo(pizza.cookedPizzaIngredientPrefabSOs, keys, visited);
        }
    }

    /// <summary>诊断结果：本关推断需要的 / 已有的 / 还缺的 / 因缺包装或缺 bundle 被跳过的。
    ///  JsonUtility 直接序列化（数组而非 List，避免旧版 Unity 的 List 序列化坑）。</summary>
    [Serializable]
    public class MatchlistSuggestionDto
    {
        public string[] required;
        public string[] current;
        public string[] missing;
        public string[] skipped;
    }

    private class Suggestion
    {
        public List<string> required = new List<string>();
        public List<string> current = new List<string>();
        public List<string> missing = new List<string>();
        public List<string> skipped = new List<string>();
    }

    /// <summary>GET /api/level/matchlists/suggest：只算不写。</summary>
    public static MatchlistSuggestionDto SuggestDto(string levelInfoAssetPath)
    {
        var info = string.IsNullOrEmpty(levelInfoAssetPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<LevelInfoSO>(levelInfoAssetPath);
        var s = Compute(info);
        return new MatchlistSuggestionDto
        {
            required = s.required.ToArray(),
            current = s.current.ToArray(),
            missing = s.missing.ToArray(),
            skipped = s.skipped.ToArray()
        };
    }

    private static Suggestion Compute(LevelInfoSO info)
    {
        var result = new Suggestion();
        if (info == null)
            return result;

        var currentKeys = new HashSet<string>(StringComparer.Ordinal);
        var existing = info.includeRecipeMatchLists;
        if (existing != null)
        {
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] == null)
                    continue;
                var p = AssetDatabase.GetAssetPath(existing[i]);
                if (string.IsNullOrEmpty(p))
                    continue;
                var k = LayoutEditorCatalogApi.MatchlistKeyOfPath(p);
                if (!string.IsNullOrEmpty(k) && currentKeys.Add(k))
                    result.current.Add(k);
            }
        }

        var needed = new List<string>(CollectRequiredMatchlistKeys(info));
        needed.Sort(StringComparer.Ordinal);
        for (int i = 0; i < needed.Count; i++)
        {
            var key = needed[i];
            result.required.Add(key);
            if (currentKeys.Contains(key))
                continue;

            string reason;
            var wrapper = ResolveWrapper(key, out reason);
            if (wrapper == null)
            {
                result.skipped.Add(key + "（" + reason + "）");
                continue;
            }
            result.missing.Add(key);
        }
        return result;
    }

    /// <summary>解析 matchlist 包装资产；不可用时返回 null 并给出原因。</summary>
    private static PseudoPrefabSO ResolveWrapper(string key, out string reason)
    {
        reason = null;
        if (!LayoutEditorCatalogApi.IsKnownMatchlistKey(key))
        {
            reason = "无 matchlist 包装资产";
            return null;
        }
        var path = LayoutEditorCatalogApi.MatchlistWrapperPathOf(key);
        var wrapper = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
        if (wrapper == null)
        {
            reason = "包装资产缺失: " + path;
            return null;
        }
        if (!LayoutEditorCatalogApi.BundleFileExists(wrapper.bundleName))
        {
            reason = "bundle 未构建: " + wrapper.bundleName;
            return null;
        }
        return wrapper;
    }

    /// <summary>按当前引用并集补全 includeRecipeMatchLists（只增不删）。
    ///  返回给前端展示的说明文本；null = 无变化。
    ///  调用点必须早于 EnsureWebDependencies（见类注释铁律 3）。</summary>
    public static string EnsureRequiredMatchlists(LevelInfoSO info)
    {
        if (info == null)
            return null;

        var suggestion = Compute(info);
        if (suggestion.skipped.Count > 0)
        {
            LayoutEditorLog.LogWarning("[Matchlist] 以下 DLC 匹配表无法自动补入，已跳过："
                + string.Join("、", suggestion.skipped.ToArray()));
        }
        if (suggestion.missing.Count == 0)
            return null;

        var list = new List<PseudoPrefabSO>();
        var seen = new HashSet<UnityEngine.Object>();
        var old = info.includeRecipeMatchLists;
        if (old != null)
        {
            for (int i = 0; i < old.Length; i++)
            {
                if (old[i] != null && seen.Add(old[i]))
                    list.Add(old[i]);
            }
        }

        var added = new List<string>();
        for (int i = 0; i < suggestion.missing.Count; i++)
        {
            var key = suggestion.missing[i];
            string reason;
            var wrapper = ResolveWrapper(key, out reason);
            if (wrapper == null || !seen.Add(wrapper))
                continue;
            list.Add(wrapper);
            added.Add(key);
        }
        if (added.Count == 0)
            return null;

        Undo.RecordObject(info, "Layout Editor Matchlists (auto)");
        info.includeRecipeMatchLists = list.ToArray();
        EditorUtility.SetDirty(info);

        LayoutEditorLog.Log("[Matchlist] 按当前菜谱自动补入 " + added.Count + " 张 DLC 匹配表: ["
            + string.Join(", ", added.ToArray()) + "] -> " + info.name);
        return "已自动补入 DLC 匹配表：" + string.Join("、", added.ToArray());
    }
}
