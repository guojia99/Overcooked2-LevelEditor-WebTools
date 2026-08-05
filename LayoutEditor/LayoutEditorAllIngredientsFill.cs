using System.Reflection;
using LevelEditorStub;
using UnityEngine;

/// <summary>
/// Invokes the private "Auto Fill All Ingredients" logic on LevelInfoSOEditor via reflection,
/// so this logic stays single-sourced in Assets/Editor/LevelInfoSOEditor.cs.
/// </summary>
public static class LayoutEditorAllIngredientsFill
{
    public static void AutoFillIngredients(LevelInfoSO levelInfo)
    {
        var method = typeof(LevelInfoSOEditor).GetMethod("AutoFillIngredients",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            Debug.LogError("Layout Editor: LevelInfoSOEditor.AutoFillIngredients not found.");
            return;
        }
        // The method does not use any instance state; a throwaway Editor instance is enough.
        var editor = ScriptableObject.CreateInstance<LevelInfoSOEditor>();
        try
        {
            method.Invoke(editor, new object[] { levelInfo });
        }
        finally
        {
            Object.DestroyImmediate(editor);
        }
    }
}
