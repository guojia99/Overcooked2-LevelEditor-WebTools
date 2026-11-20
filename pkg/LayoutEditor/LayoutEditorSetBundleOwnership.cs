using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

/// <summary>Native per-set layout: one info bundle and one bundle per scene.</summary>
public static class LayoutEditorSetBundleOwnership
{
    public static void Normalize(string setName)
    {
        if (string.IsNullOrEmpty(setName) || !Regex.IsMatch(setName, @"^[a-z0-9_]+$"))
            throw new InvalidOperationException("Invalid level set name: " + setName);
        var root = "Assets/LevelSets/" + setName;
        if (!AssetDatabase.IsValidFolder(root))
            throw new InvalidOperationException("Missing level set: " + root);
        var paths = new List<string>();
        paths.Add(root);
        foreach (var path in AssetDatabase.GetAllAssetPaths())
            if (path.StartsWith(root + "/", StringComparison.Ordinal))
                paths.Add(path);

        var sceneNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        sceneNames.Add("info_" + setName);
        foreach (var path in paths)
            if (!AssetDatabase.IsValidFolder(path) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                if (!path.StartsWith(root + "/scenes/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Move scene into the set scenes directory: " + path);
                if (!sceneNames.Add(Path.GetFileNameWithoutExtension(path)))
                    throw new InvalidOperationException("Duplicate scene bundle name: " + path);
            }
        // A string lookup cannot be repaired by moving its asset into info.
        // Reject before changing labels; a direct SO.asset reference needs authoring repair.
        var references = new HashSet<string>(paths, StringComparer.Ordinal);
        foreach (var path in AssetDatabase.GetDependencies(paths.ToArray(), true)) references.Add(path);
        foreach (var path in references)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".asset" && ext != ".prefab" && ext != ".unity") continue;
            if (!File.Exists(path))
                throw new IOException("Cannot inspect bundle reference: " + path);
            var match = Regex.Match(File.ReadAllText(path),
                @"(?mi)^\s*(?:-\s+)?(?:bundleName|m_bundleName):\s*[""']?((?:[^\s""']*/)?custom_(?:recipes|content))[""']?\s*$");
            if (match.Success)
                throw new InvalidOperationException("Legacy custom bundle string requires direct asset reference repair: "
                    + match.Groups[1].Value + " <- " + path);
        }
        var oldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null) continue;
            var expected = "";
            if (path == root)
                expected = setName + "/info_" + setName;
            else if (!AssetDatabase.IsValidFolder(path) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                expected = setName + "/" + Path.GetFileNameWithoutExtension(path);
            }
            if (importer.assetBundleName == expected && string.IsNullOrEmpty(importer.assetBundleVariant)) continue;
            if (!string.IsNullOrEmpty(importer.assetBundleName))
                oldNames.Add(importer.assetBundleName + (string.IsNullOrEmpty(importer.assetBundleVariant)
                    ? "" : "." + importer.assetBundleVariant));
            importer.SetAssetBundleNameAndVariant(expected, "");
            importer.SaveAndReimport();
        }
        foreach (var name in oldNames)
            if ((AssetDatabase.GetAssetPathsFromAssetBundle(name) ?? new string[0]).Length == 0)
                AssetDatabase.RemoveAssetBundleName(name, false);
    }
}
