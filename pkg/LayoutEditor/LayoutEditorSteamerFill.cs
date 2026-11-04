using System.Collections.Generic;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LayoutEditorSteamerFill
{
    public static void SyncSteamerIngredients(LevelInfoSO info)
    {
        if (info == null)
            return;
        RemoveRiceFromSteamers();
        ApplyIngredients(CollectSteamerInputs(info), IsSteamer, 1);
        ApplyIngredients(CollectMixerInputs(info), IsMixerBowl, 4);
    }

    /// <summary>米饭走煮锅，历史自动填充可能把 SushiRice 写进蒸笼；这里做一次清理。</summary>
    private static void RemoveRiceFromSteamers()
    {
        bool sceneDirty = false;
        foreach (var stub in Object.FindObjectsOfType<PseudoPrefabStub>())
        {
            if (stub == null || stub.gameObject == null || !IsSteamer(stub))
                continue;
            var derived = stub.gameObject.GetComponent<PseudoPrefabCookingUtensilStub>();
            if (derived == null || derived.allowedIngredientSOs == null)
                continue;
            var kept = new List<ScriptableObject>();
            bool removed = false;
            foreach (var so in derived.allowedIngredientSOs)
            {
                if (so == null)
                {
                    removed = true;
                    continue;
                }
                if (IsRice(so))
                {
                    removed = true;
                    continue;
                }
                kept.Add(so);
            }
            if (!removed)
                continue;
            derived.allowedIngredientSOs = kept.ToArray();
            sceneDirty = true;
        }
        if (sceneDirty)
            MarkScenesDirty();
    }

    private static bool IsRice(ScriptableObject so)
    {
        return so != null && (so.name == "SushiRiceSO" || so.name == "RiceSO");
    }

    private static void ApplyIngredients(
        List<ScriptableObject> required,
        System.Func<PseudoPrefabStub, bool> match,
        int capacity)
    {
        if (required.Count == 0)
            return;

        bool sceneDirty = false;
        foreach (var stub in Object.FindObjectsOfType<PseudoPrefabStub>())
        {
            if (stub == null || stub.gameObject == null || !match(stub))
                continue;
            var go = stub.gameObject;
            var derived = go.GetComponent<PseudoPrefabCookingUtensilStub>();
            if (derived == null)
            {
                var guids = new List<string>();
                foreach (var so in required)
                {
                    var path = AssetDatabase.GetAssetPath(so);
                    if (!string.IsNullOrEmpty(path))
                        guids.Add(AssetDatabase.AssetPathToGUID(path));
                }
                if (guids.Count == 0)
                    continue;
                var item = new LayoutItemDto
                {
                    stubKind = "CookingUtensil",
                    cookingUtensil = new LayoutCookingUtensilStubDto
                    {
                        capacity = capacity,
                        allowedIngredientGuids = guids.ToArray(),
                    },
                };
                LayoutEditorStubIO.ApplyStub(go, item);
                sceneDirty = true;
            }
            else if (MergeIngredients(derived, required))
            {
                sceneDirty = true;
            }
        }

        if (sceneDirty)
            MarkScenesDirty();
    }

    private static void MarkScenesDirty()
    {
        foreach (var stub in Object.FindObjectsOfType<PseudoPrefabCookingUtensilStub>())
        {
            if (stub == null || stub.gameObject == null)
                continue;
            var scene = stub.gameObject.scene;
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static List<ScriptableObject> CollectSteamerInputs(LevelInfoSO info)
    {
        var result = new List<ScriptableObject>();
        if (info == null || info.recipes == null)
            return result;
        var seen = new HashSet<Object>();
        foreach (var recipe in info.recipes)
        {
            var custom = recipe as CustomRecipeSO;
            if (custom == null)
                continue;
            if (LayoutEditorRecipeKnowledge.CustomCookingStep(custom) != "Steamer")
                continue;
            if (custom.compositionSOs == null)
                continue;
            foreach (var comp in custom.compositionSOs)
            {
                if (comp == null)
                    continue;
                if ((comp is CustomRecipeSO || comp is PseudoPrefabSO || comp is PseudoPrefabSORecipe)
                    && seen.Add(comp))
                    result.Add(comp);
                var sub = comp as CustomRecipeSO;
                if (sub == null || sub.compositionSOs == null)
                    continue;
                foreach (var subComp in sub.compositionSOs)
                {
                    if (subComp == null)
                        continue;
                    if ((subComp is CustomRecipeSO || subComp is PseudoPrefabSO || subComp is PseudoPrefabSORecipe)
                        && seen.Add(subComp))
                        result.Add(subComp);
                }
            }
        }
        return result;
    }

    private static List<ScriptableObject> CollectMixerInputs(LevelInfoSO info)
    {
        var result = new List<ScriptableObject>();
        if (info == null || info.recipes == null)
            return result;
        var seen = new HashSet<Object>();
        foreach (var recipe in info.recipes)
        {
            var custom = recipe as CustomRecipeSO;
            if (custom == null)
                continue;
            if (LayoutEditorRecipeKnowledge.CustomCookingStep(custom) != "Steamer")
                continue;
            if (custom.compositionSOs == null)
                continue;
            foreach (var comp in custom.compositionSOs)
            {
                var sub = comp as CustomRecipeSO;
                if (sub == null || sub.type != CustomRecipeSO.RecipeType.Mixed ||
                    sub.compositionSOs == null)
                    continue;
                foreach (var subComp in sub.compositionSOs)
                {
                    if (subComp == null || !(subComp is PseudoPrefabSO))
                        continue;
                    if (seen.Add(subComp))
                        result.Add(subComp);
                }
            }
        }
        return result;
    }

    private static bool IsSteamer(PseudoPrefabStub stub)
    {
        if (stub.gameObject == null)
            return false;
        if (stub.gameObject.name.IndexOf("steamer", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        var so = stub.pseudoPrefabSO;
        return so != null && so.prefabName != null &&
            so.prefabName.IndexOf("steamer", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsMixerBowl(PseudoPrefabStub stub)
    {
        if (stub.gameObject == null)
            return false;
        if (stub.gameObject.name.IndexOf("mixerbowl", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        var so = stub.pseudoPrefabSO;
        return so != null && so.prefabName != null &&
            so.prefabName.IndexOf("mixerbowl", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool MergeIngredients(PseudoPrefabCookingUtensilStub stub, List<ScriptableObject> extra)
    {
        var cur = stub.allowedIngredientSOs;
        var merged = new List<ScriptableObject>();
        var seen = new HashSet<Object>();
        if (cur != null)
        {
            foreach (var so in cur)
            {
                if (so != null && seen.Add(so))
                    merged.Add(so);
            }
        }
        bool changed = false;
        foreach (var so in extra)
        {
            if (so != null && seen.Add(so))
            {
                merged.Add(so);
                changed = true;
            }
        }
        if (!changed)
            return false;
        stub.allowedIngredientSOs = merged.ToArray();
        return true;
    }
}
