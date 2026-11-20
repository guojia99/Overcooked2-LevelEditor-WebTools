using System;
using System.IO;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Add the native plain-pancake batter to this level's frying pans.</summary>
public static class LayoutEditorPancakeFill
{
    public const string BatterAssetPath = "Assets/commonW5/custom_recipes/pancake/Web_MixedFlourEgg.asset";
    private const string FryPanAssetPath = "Assets/common01/pseudo_prefab_so/utensils/FryPanSO.asset";

    public static bool HasPlainPancake(LevelInfoSO info)
    {
        if (info == null)
            return false;
        foreach (var recipe in LayoutEditorLevelModeStore.CollectAllRecipes(info))
        {
            // Decorative SOs and custom/flavoured recipes must not enable this fix.
            if (recipe is PseudoPrefabSORecipe && string.Equals(
                Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(recipe)),
                "Cake_Plain_SO", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public static void SyncLoadedScenes(LevelInfoSO info)
    {
        if (!HasPlainPancake(info))
            return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            SyncForScene(SceneManager.GetSceneAt(i), info);
    }

    public static int SyncForScene(Scene scene, LevelInfoSO info)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid()
            || !scene.isLoaded || !HasPlainPancake(info))
            return 0;
        var roots = scene.GetRootGameObjects();
        bool matches = false;
        foreach (var root in roots)
            foreach (var manager in root.GetComponentsInChildren<PseudoPrefabManagerStub>(true))
                if (manager.levelInfo == info)
                    matches = true;
        if (!matches)
            return 0;

        // Register through the normal gated collector, including for old scenes.
        var deps = info.dependencies ?? new string[0];
        // Shared commonW5 delivery is separate; it must never enter dependencies
        // or keep this native dependency repair firing on every scene reload.
        if (Array.IndexOf(deps, "bundle18") < 0 && LayoutEditorCatalogApi.BundleFileExists("bundle18"))
            LayoutEditorCustomIngredients.EnsureWebDependencies(null, info);

        var batter = AssetDatabase.LoadAssetAtPath<PseudoPrefabSORecipe>(BatterAssetPath);
        var fryPan = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(FryPanAssetPath);
        if (batter == null || fryPan == null)
            return 0;

        int changed = 0;
        int preserved = 0;
        string emptyPanReason = EmptyPanRestrictionReason(info);
        foreach (var root in roots)
        {
            // Root-scoped traversal includes inactive pans and excludes prefab assets.
            foreach (var stub in root.GetComponentsInChildren<PseudoPrefabStub>(true))
            {
                if (stub == null || stub.pseudoPrefabSO != fryPan
                    || LayoutEditorStubIO.IsUnderManagerEnvironment(stub.transform))
                    continue;
                var go = stub.gameObject;
                var utensil = go.GetComponent<PseudoPrefabCookingUtensilStub>();
                // Empty means the full native lookup, not an unconfigured whitelist.
                // A batter-only replacement would reject other selected pan dishes.
                if (emptyPanReason != null && (utensil == null
                    || new SerializedObject(utensil).FindProperty("allowedIngredientSOs").arraySize == 0))
                {
                    preserved++;
                    continue;
                }
                bool promoted = utensil == null;
                if (promoted)
                {
                    LayoutEditorStubIO.ApplyStub(go, new LayoutItemDto
                    {
                        stubKind = "CookingUtensil",
                        cookingUtensil = new LayoutCookingUtensilStubDto { capacity = 1 }
                    });
                    utensil = go.GetComponent<PseudoPrefabCookingUtensilStub>();
                    if (utensil == null)
                        continue;
                }

                var serialized = new SerializedObject(utensil);
                var ingredients = serialized.FindProperty("allowedIngredientSOs");
                bool contains = false;
                for (int i = 0; i < ingredients.arraySize; i++)
                    if (ingredients.GetArrayElementAtIndex(i).objectReferenceValue == batter)
                        contains = true;
                var capacity = serialized.FindProperty("capacity");
                if (contains && capacity.intValue > 0 && !promoted)
                    continue;

                Undo.RecordObject(utensil, "Layout Editor Plain Pancake");
                if (capacity.intValue <= 0)
                    capacity.intValue = 1;
                if (!contains)
                {
                    int index = ingredients.arraySize;
                    ingredients.arraySize = index + 1;
                    ingredients.GetArrayElementAtIndex(index).objectReferenceValue = batter;
                    InsertNullModelSlot(serialized.FindProperty("models"), index);
                    InsertNullModelSlot(serialized.FindProperty("modelSOs"), index);
                }
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(utensil);
                changed++;
            }
        }
        if (changed > 0)
            EditorSceneManager.MarkSceneDirty(scene);
        if (preserved > 0)
            LayoutEditorLog.LogWarning("[plain-pancake] Preserved native lookup on " + preserved
                + " empty frying pan(s) in " + scene.path + ": " + emptyPanReason
                + "; use explicit utensil auto-fill for the complete selected recipe inputs.");
        return changed;
    }

    private static string EmptyPanRestrictionReason(LevelInfoSO info)
    {
        foreach (var recipe in LayoutEditorLevelModeStore.CollectAllRecipes(info))
        {
            string id = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(recipe));
            var original = recipe as PseudoPrefabSORecipe;
            if (original != null && id == "Cake_Plain_SO")
                continue;
            // The knowledge APIs provide leaves/groups, not a complete set of native
            // pan nodes. Do not guess nested/custom batter or cooked-node mappings.
            if (original == null)
                return "selected custom/unknown recipe " + id + " has unverified pan inputs";
            string step = "", plating;
            string[] ingredients = new string[0], composition = new string[0];
            bool orderable;
            string knowledgeId = null;
            foreach (var candidate in new[] { id, original.prefabName + "_SO", original.prefabName })
                if (LayoutEditorRecipeKnowledge.TryGetOriginalEntry(candidate, out step, out ingredients,
                    out composition, out plating, out orderable))
                {
                    knowledgeId = candidate;
                    break;
                }
            if (knowledgeId == null)
                return "selected recipe " + id + " is absent from recipe knowledge";
            if (composition.Length > 0)
                return "selected recipe " + id + " has unverified nested pan inputs";
            var entry = new RecipeEntryDto
            {
                id = knowledgeId, type = LayoutEditorCatalogApi.RecipeTypeOf(knowledgeId),
                cookingStep = step, ingredients = ingredients
            };
            if (step == "FryingPan")
                return "selected recipe " + id + " also requires frying-pan inputs";
            foreach (var group in LayoutEditorRecipeKnowledge.ComputeCookingGroups(entry, null))
                if (group.step == "FryingPan")
                    return "selected recipe " + id + " also requires frying-pan inputs";
        }
        return null;
    }

    private static void InsertNullModelSlot(SerializedProperty models, int index)
    {
        // Unity duplicates the last entry when growing an array: clear new slots
        // explicitly, preserving every existing model and its ingredient index.
        if (models.arraySize > index)
            models.InsertArrayElementAtIndex(index);
        else
        {
            int previousSize = models.arraySize;
            models.arraySize = index + 1;
            for (int i = previousSize; i < index; i++)
                models.GetArrayElementAtIndex(i).objectReferenceValue = null;
        }
        models.GetArrayElementAtIndex(index).objectReferenceValue = null;
    }
}
