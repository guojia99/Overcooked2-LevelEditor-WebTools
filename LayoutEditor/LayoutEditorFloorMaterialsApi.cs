using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scans the project for swappable floor/background materials, prioritising the
/// active level set's own materials folder (mat_*floor*, mat_city_path*, ...).
/// </summary>
public static class LayoutEditorFloorMaterialsApi
{
    private static readonly Regex SizeTagRegex = new Regex(@"_([0-9]+)x([0-9]+)(?:_|$)", RegexOptions.Compiled);
    private static readonly Regex TilingSuffixRegex = new Regex(@"_tiling([0-9]+)x([0-9]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>运行时烘焙材质名 mat_foo_tiling12x4 → (12, 4)。</summary>
    public static bool TryParseMaterialTilingSuffix(string materialName, out int tilingW, out int tilingD)
    {
        tilingW = 0;
        tilingD = 0;
        if (string.IsNullOrEmpty(materialName))
            return false;
        var m = TilingSuffixRegex.Match(materialName);
        if (!m.Success)
            return false;
        return int.TryParse(m.Groups[1].Value, out tilingW)
            && int.TryParse(m.Groups[2].Value, out tilingD)
            && tilingW > 0 && tilingD > 0;
    }

    /// <summary>去掉 SceneLayoutApplier 烘焙材质名上的 _tilingWxH 后缀。</summary>
    public static string StripMaterialTilingSuffix(string materialName)
    {
        if (string.IsNullOrEmpty(materialName))
            return materialName;
        var m = TilingSuffixRegex.Match(materialName);
        if (!m.Success)
            return materialName;
        return materialName.Substring(0, m.Index);
    }

    /// <summary>按剥离后缀后的材质 id 在工程中查找 .mat 资产。</summary>
    public static bool TryResolveFloorMaterialByName(string materialName, out string guid, out string assetPath)
    {
        guid = null;
        assetPath = null;
        var baseName = StripMaterialTilingSuffix(materialName);
        if (string.IsNullOrEmpty(baseName))
            return false;

        foreach (var found in AssetDatabase.FindAssets("t:Material " + baseName))
        {
            var path = AssetDatabase.GUIDToAssetPath(found);
            var id = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(id, baseName, StringComparison.OrdinalIgnoreCase))
            {
                guid = found;
                assetPath = path;
                return true;
            }
        }
        return false;
    }

    /// <summary>从 ApplyMaterialFloorDirectTiling 烘焙的 _MainTex ST 反推 tiling 格数。</summary>
    public static bool TryInferTilingFromMainTex(Material mat, out int tilingW, out int tilingD)
    {
        tilingW = 0;
        tilingD = 0;
        if (mat == null || !mat.HasProperty("_MainTex"))
            return false;

        var scale = mat.GetTextureScale("_MainTex");
        var offset = mat.GetTextureOffset("_MainTex");
        var w = Mathf.RoundToInt(Mathf.Abs(scale.x));
        var d = Mathf.RoundToInt(Mathf.Abs(scale.y));
        if (w <= 0 || d <= 0)
            return false;

        // Direct tiling: scale.x = -tilingW, scale.y = tilingD, offset.x = tilingW.
        if (Mathf.Approximately(Mathf.Abs(offset.x), w))
        {
            tilingW = w;
            tilingD = d;
            return true;
        }
        return false;
    }

    public static FloorMaterialCatalogDto Scan(string levelSet)
    {
        var folders = new List<string>();
        var setRoot = "Assets/LevelSets/" + (levelSet ?? "") + "/materials";
        if (AssetDatabase.IsValidFolder(setRoot))
            folders.Add(setRoot);

        // Fallback / shared swatch sources.
        foreach (var shared in new[] { "Assets/LevelSets", "Assets/common01/materials", "Assets/common02/materials", "Assets/common03/materials" })
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

                // 自定义菜谱/自定义食材目录里的 .mat（导入模型的附带材质等）不是地板
                // 材质——LevelSets 根目录递归扫描会误收（前端落入「其他」组），跳过。
                if (path.IndexOf("/custom_recipes/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/custom_ingredients/", StringComparison.OrdinalIgnoreCase) >= 0)
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
