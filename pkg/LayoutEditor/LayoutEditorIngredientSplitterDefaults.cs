using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 上游分料机进度条（ClientIngredientSplitter）依赖 IngredientSplitter.cookingUISO。
/// 上游模板与旧场景默认未赋值，编辑器 Play 会在 StartSynchronising 空引用。
/// 这里在场景打开/进入 Play 前按需补默认 UI，只填空值，不覆盖已有配置。
/// </summary>
[InitializeOnLoad]
public static class LayoutEditorIngredientSplitterDefaults
{
    private const string CookingUI = "Assets/common04/_supplies/CookingUI.asset";

    static LayoutEditorIngredientSplitterDefaults()
    {
        EditorApplication.delayCall += OnDomainReload;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnDomainReload()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        EnsureAllOpenScenes();
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EnsureAllOpenScenes();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            EnsureAllOpenScenes();
    }

    [MenuItem("Layout Editor/Fix Ingredient Splitter CookingUI")]
    private static void FixMenu()
    {
        EnsureAllOpenScenes();
    }

    private static void EnsureAllOpenScenes()
    {
        var cookingUI = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(CookingUI);
        if (cookingUI == null)
            return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;
            var roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var behaviours = roots[r].GetComponentsInChildren<MonoBehaviour>(true);
                for (int b = 0; b < behaviours.Length; b++)
                {
                    var mb = behaviours[b];
                    if (mb == null || mb.GetType().Name != "IngredientSplitter")
                        continue;
                    var so = new SerializedObject(mb);
                    var p = so.FindProperty("cookingUISO");
                    if (p == null || p.objectReferenceValue != null)
                        continue;
                    Undo.RecordObject(mb, "Layout Editor Splitter CookingUI Default");
                    p.objectReferenceValue = cookingUI;
                    so.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
        }
    }
}
