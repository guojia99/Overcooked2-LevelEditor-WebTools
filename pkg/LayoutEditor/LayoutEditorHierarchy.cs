using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LayoutEditorHierarchy
{
    public static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        var stack = new Stack<string>();
        var current = transform;
        while (current != null)
        {
            stack.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", stack.ToArray());
    }

    public static Transform FindByPath(string hierarchyPath)
    {
        if (string.IsNullOrEmpty(hierarchyPath))
            return null;

        var parts = hierarchyPath.Split('/');
        if (parts.Length == 0)
            return null;

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return null;

        Transform current = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == parts[0])
            {
                current = root.transform;
                break;
            }
        }

        if (current == null)
            return null;

        for (int i = 1; i < parts.Length; i++)
        {
            var child = current.Find(parts[i]);
            if (child == null)
            {
                for (int c = 0; c < current.childCount; c++)
                {
                    var ch = current.GetChild(c);
                    if (ch.name == parts[i])
                    {
                        child = ch;
                        break;
                    }
                }
            }

            if (child == null)
                return null;
            current = child;
        }

        return current;
    }

    public static Transform FindOrCreatePath(string hierarchyPath)
    {
        var existing = FindByPath(hierarchyPath);
        if (existing != null)
            return existing;

        var parts = hierarchyPath.Split('/');
        if (parts.Length == 0)
            return null;

        Transform current = FindByPath(parts[0]);
        if (current == null)
            return null;

        for (int i = 1; i < parts.Length; i++)
        {
            var child = current.Find(parts[i]);
            if (child == null)
            {
                var go = new GameObject(parts[i]);
                go.transform.SetParent(current, false);
                child = go.transform;
            }

            current = child;
        }

        return current;
    }

    /// <summary>递归创建 Assets 下文件夹（已存在则跳过）。</summary>
    public static void EnsureAssetFolder(string folderPath)
    {
        folderPath = (folderPath ?? "").Replace('\\', '/');
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            return;
        if (!folderPath.StartsWith("Assets/", System.StringComparison.Ordinal))
            return;
        var parts = folderPath.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
