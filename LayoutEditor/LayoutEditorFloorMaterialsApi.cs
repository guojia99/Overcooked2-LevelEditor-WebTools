using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

/// <summary>
/// Scans the project for swappable floor/background materials, prioritising the
/// active level set's own materials folder (mat_*floor*, mat_city_path*, ...).
/// </summary>
public static class LayoutEditorFloorMaterialsApi
{
    private static readonly Regex SizeTagRegex = new Regex(@"_([0-9]+)x([0-9]+)(?:_|$)", RegexOptions.Compiled);

    public static FloorMaterialCatalogDto Scan(string levelSet)
    {
        var folders = new List<string>();
        var setRoot = "Assets/LevelSets/" + (levelSet ?? "") + "/materials";
        if (AssetDatabase.IsValidFolder(setRoot))
            folders.Add(setRoot);

        // Fallback / shared swatch sources.
        foreach (var shared in new[] { "Assets/LevelSets", "Assets/common01/materials", "Assets/common02/materials" })
        {
            if (AssetDatabase.IsValidFolder(shared) && !folders.Contains(shared))
                folders.Add(shared);
        }

        var list = new List<FloorMaterialDto>();
        var seen = new HashSet<string>();
        for (int f = 0; f < folders.Count; f++)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folders[f] }))
            {
                if (!seen.Add(guid))
                    continue;

                var path = AssetDatabase.GUIDToAssetPath(guid);
                var id = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(id))
                    continue;

                list.Add(new FloorMaterialDto
                {
                    guid = guid,
                    id = id,
                    assetPath = path,
                    nameZh = TidyName(id),
                    sizeTag = SizeTagOf(id),
                });
            }
        }

        list.Sort((a, b) =>
        {
            var ra = FloorRelevance(a.id);
            var rb = FloorRelevance(b.id);
            if (ra != rb) return rb.CompareTo(ra);
            return string.Compare(a.nameZh, b.nameZh, StringComparison.Ordinal);
        });

        return new FloorMaterialCatalogDto { materials = list.ToArray() };
    }

    private static int FloorRelevance(string id)
    {
        var n = (id ?? "").ToLowerInvariant();
        if (n.Contains("floor"))
            return 100;
        if (n.Contains("raft"))
            return 95;
        if (n.Contains("blacktiles") || n.Contains("carpet"))
            return 90;
        if (n.Contains("path"))
            return 80;
        if (n.Contains("sky") || n.Contains("background"))
            return 70;
        return 0;
    }

    private static string SizeTagOf(string id)
    {
        var m = SizeTagRegex.Match(id ?? "");
        if (!m.Success)
            return "";
        return m.Groups[1].Value + "x" + m.Groups[2].Value;
    }

    private static string TidyName(string id)
    {
        var n = id ?? "";
        if (n.StartsWith("mat_", StringComparison.OrdinalIgnoreCase))
            n = n.Substring(4);
        return n.Replace('_', ' ');
    }
}
