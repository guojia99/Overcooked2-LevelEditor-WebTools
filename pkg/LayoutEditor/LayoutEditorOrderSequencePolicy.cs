using System;
using System.Collections.Generic;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>Shared native/Web order-sequence policy for config saves, write-back and export.
/// Validation never changes assets, arrays or authoring JSON. Native sequences are Campaign-only.</summary>
public static class LayoutEditorOrderSequencePolicy
{
    public static LevelConfigSetupPerPlayerCountSO[] Configs(LevelInfoSO info)
    {
        return new[] { info.config_1p, info.config_2p, info.config_3p, info.config_4p };
    }

    public static string[] Read(LevelConfigSetupPerPlayerCountSO config, out string[] errors)
    {
        var messages = new List<string>();
        var recipes = config == null ? null : config.scriptedRecipes;
        var ids = new string[recipes == null ? 0 : recipes.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            var recipe = recipes[i];
            var path = recipe == null ? "" : AssetDatabase.GetAssetPath(recipe);
            ids[i] = string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
            if (!(recipe is PseudoPrefabSORecipe) && !(recipe is CustomRecipeSO)
                || string.IsNullOrEmpty(ids[i]))
                messages.Add("第 " + (i + 1) + " 项菜谱引用缺失、不是菜谱资产或没有 GUID。");
        }
        errors = messages.ToArray();
        return ids;
    }

    public static string Resolve(string[] ids, out ScriptableObject[] recipes)
    {
        recipes = ids == null ? null : new ScriptableObject[ids.Length];
        if (ids == null) return null;
        for (int i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            if (string.IsNullOrEmpty(id) || id.Length != 32 || !IsHex(id))
                return "第 " + (i + 1) + " 项必须是 32 位菜谱 GUID。";
            var path = AssetDatabase.GUIDToAssetPath(id);
            var recipe = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (!(recipe is PseudoPrefabSORecipe) && !(recipe is CustomRecipeSO))
                return "第 " + (i + 1) + " 项菜谱 GUID 无法解析为菜谱资产：" + id;
            recipes[i] = recipe;
        }
        return null;
    }

    private static bool IsHex(string id)
    {
        foreach (var ch in id)
            if (!(ch >= '0' && ch <= '9') && !(ch >= 'a' && ch <= 'f') && !(ch >= 'A' && ch <= 'F')) return false;
        return true;
    }

    /// <summary>overrides null = persisted value for that player; [] = explicit clear.
    /// Reject enabled Web sequences whenever any effective native array has entries, even broken ones.</summary>
    public static string Validate(LevelInfoSO info, OrderSequenceConfigDto web, ScriptableObject[][] overrides)
    {
        var configs = Configs(info);
        for (int p = 0; p < configs.Length; p++)
        {
            var replacement = overrides == null ? null : overrides[p];
            var recipes = replacement ?? (configs[p] == null ? null : configs[p].scriptedRecipes);
            if (recipes == null || recipes.Length == 0) continue;
            if (web != null && web.enabled)
                return "Web OrderSequence 与 " + (p + 1) + "P 原生 scriptedRecipes 非空序列冲突；请显式禁用 Web 顺序或清空全部原生序列。";
            // Other modes retain dormant native sequences, including legacy broken references.
            if (info.levelType != LevelInfoSO.LevelType.Campaign) continue;
            string[] errors;
            if (replacement == null)
            {
                Read(configs[p], out errors);
                if (errors.Length > 0) return (p + 1) + "P scriptedRecipes：" + errors[0];
            }
        }
        if (web != null && web.enabled && info.levelType != LevelInfoSO.LevelType.Campaign)
            return "Web OrderSequence 仅支持 Campaign；请显式禁用后继续。";
        if (web != null && web.enabled && web.groups != null)
        {
            var available = new HashSet<ScriptableObject>(LayoutEditorLevelModeStore.CollectAllRecipes(info));
            for (int g = 0; g < web.groups.Length; g++)
            {
                ScriptableObject[] recipes;
                if (web.groups[g] == null) return "Web OrderSequence 组数据缺失。";
                var error = Resolve(web.groups[g].recipeGuids, out recipes);
                if (error != null) return "Web OrderSequence 第 " + (g + 1) + " 组：" + error;
                if (recipes == null) return "Web OrderSequence 菜谱列表缺失。";
                for (int r = 0; r < recipes.Length; r++)
                    if (!available.Contains(recipes[r]))
                        return "Web OrderSequence 第 " + (g + 1) + " 组第 " + (r + 1) + " 项不在本关有效菜谱集合，请先配置菜谱。";
            }
        }
        return null;
    }

    public static string ValidateSaved(LevelInfoSO info)
    {
        if (info == null) return null;
        try
        {
            var web = LayoutEditorLevelAdminApi.LoadLevelOrderSequence(AssetDatabase.GetAssetPath(info));
            return Validate(info, web, null);
        }
        catch (Exception ex) { return "订单顺序配置读取失败：" + ex.Message; }
    }
}
