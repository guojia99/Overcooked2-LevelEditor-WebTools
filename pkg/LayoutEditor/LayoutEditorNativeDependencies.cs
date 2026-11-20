using System;
using System.Collections.Generic;
using LevelEditorStub;
using UnityEditor;

/// <summary>LevelInfo load declarations only. Never use for distribution manifests.</summary>
public static class LayoutEditorNativeDependencies
{
    public static bool IsNativeBundle(string name)
    {
        if (name == null || name.Length <= 6 || !name.StartsWith("bundle", StringComparison.Ordinal))
            return false;
        for (int i = 6; i < name.Length; i++)
            if (name[i] < '0' || name[i] > '9') return false;
        return true;
    }

    public static string Validate(IEnumerable<string> dependencies)
    {
        if (dependencies == null) return null;
        foreach (var name in dependencies)
            if (!IsNativeBundle(name))
                return "LevelInfoSO.dependencies only accepts ^bundle[0-9]+$: " + (name ?? "<null>");
        return null;
    }

    public static string[] Filter(IEnumerable<string> dependencies)
    {
        var kept = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (dependencies != null)
            foreach (var name in dependencies)
                if (IsNativeBundle(name) && seen.Add(name)) kept.Add(name);
        return kept.ToArray();
    }

    // Mark dirty here; the surrounding save/read/migration transaction owns SaveAssets.
    public static bool Clean(LevelInfoSO info)
    {
        if (info == null) return false;
        var filtered = Filter(info.dependencies);
        if (info.dependencies != null && info.dependencies.Length == filtered.Length) return false;
        info.dependencies = filtered;
        EditorUtility.SetDirty(info);
        return true;
    }
}

// Covers inspector saves, copied templates and legacy callers at the persistence boundary.
class LayoutEditorNativeDependencySaveGuard : AssetModificationProcessor
{
    private static string[] OnWillSaveAssets(string[] paths)
    {
        foreach (var path in paths)
        {
            if (!path.StartsWith("Assets/LevelSets/", StringComparison.Ordinal)
                || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;
            LayoutEditorNativeDependencies.Clean(AssetDatabase.LoadAssetAtPath<LevelInfoSO>(path));
        }
        return paths;
    }
}
