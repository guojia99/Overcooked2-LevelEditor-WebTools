using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LayoutEditorMissingScriptCleanup
{
    public static int RemoveMissingScripts(Scene scene)
    {
        if (!scene.IsValid())
            return 0;
        var removed = 0;
        var roots = scene.GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
                continue;
            try
            {
                removed += Clean(roots[i]);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[LayoutEditor] 清理 Missing Script 失败 " + roots[i].name + ": " + ex.Message);
            }
        }
        if (removed > 0)
            EditorSceneManager.MarkSceneDirty(scene);
        return removed;
    }

    private static int Clean(GameObject go)
    {
        var removed = 0;
        var serialized = new SerializedObject(go);
        var components = serialized.FindProperty("m_Component");
        if (components != null && components.isArray)
        {
            var missingComponents = new List<Component>();
            var nullIndices = new List<int>();
            for (var i = 0; i < components.arraySize; i++)
            {
                var element = components.GetArrayElementAtIndex(i);
                var componentProperty = element.FindPropertyRelative("component");
                var component = componentProperty != null
                    ? componentProperty.objectReferenceValue
                    : null;
                if (component == null)
                {
                    nullIndices.Add(i);
                    continue;
                }
                var behaviour = component as MonoBehaviour;
                if (behaviour == null)
                    continue;
                var missing = false;
                try
                {
                    missing = MonoScript.FromMonoBehaviour(behaviour) == null;
                }
                catch
                {
                    missing = true;
                }
                if (missing)
                    missingComponents.Add(behaviour);
            }

            for (var i = 0; i < missingComponents.Count; i++)
            {
                Object.DestroyImmediate(missingComponents[i], true);
                removed++;
            }

            for (var i = nullIndices.Count - 1; i >= 0; i--)
            {
                var index = nullIndices[i];
                var before = components.arraySize;
                components.DeleteArrayElementAtIndex(index);
                if (components.arraySize == before)
                    components.DeleteArrayElementAtIndex(index);
                removed++;
            }
            if (nullIndices.Count > 0)
                serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        var transform = go.transform;
        for (var i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child != null)
                removed += Clean(child.gameObject);
        }
        return removed;
    }
}
