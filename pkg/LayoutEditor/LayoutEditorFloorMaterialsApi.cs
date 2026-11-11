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
    // 变体名段顺序固定：<base>[_tilingWxH][_rm#][_m##_s##]。tiling/rm 后缀须容忍
    // 尾随段（带金属度/光滑度/渲染模式覆盖的变体才解析得出前面的段）。
    private static readonly Regex TilingSuffixRegex = new Regex(@"_tiling([0-9]+)x([0-9]+)(?:_rm[0-9]+)?(?:_m[0-9]+_s[0-9]+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PbrSuffixRegex = new Regex(@"_m([0-9]+)_s([0-9]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RenderModeSuffixRegex = new Regex(@"_rm([0-3])(?:_m[0-9]+_s[0-9]+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

    /// <summary>PBR 覆盖后缀（写回烘焙变体材质名尾段 _m&lt;0-100&gt;_s&lt;0-100&gt;，
    ///  百分比整数编码）→ (metallic, smoothness) 0..1。仅实心材质地板使用。</summary>
    public static bool TryParsePbrSuffix(string materialName, out float metallic, out float smoothness)
    {
        metallic = 0f;
        smoothness = 0f;
        if (string.IsNullOrEmpty(materialName))
            return false;
        var m = PbrSuffixRegex.Match(materialName);
        if (!m.Success)
            return false;
        int mi, si;
        if (!int.TryParse(m.Groups[1].Value, out mi) || !int.TryParse(m.Groups[2].Value, out si))
            return false;
        if (mi < 0 || mi > 100 || si < 0 || si > 100)
            return false;
        metallic = mi / 100f;
        smoothness = si / 100f;
        return true;
    }

    /// <summary>去掉材质名尾段的 PBR 后缀 _m##_s##（无则原样返回）。</summary>
    public static string StripPbrSuffix(string materialName)
    {
        if (string.IsNullOrEmpty(materialName))
            return materialName;
        var m = PbrSuffixRegex.Match(materialName);
        if (!m.Success)
            return materialName;
        return materialName.Substring(0, m.Index);
    }

    /// <summary>渲染模式覆盖后缀（变体名中段 _rm&lt;0-3&gt;，0=不透明 1=镂空
    ///  2=渐隐 3=透明）。命中返回 true 并给出模式值。</summary>
    public static bool TryParseRenderModeSuffix(string materialName, out int renderMode)
    {
        renderMode = 0;
        if (string.IsNullOrEmpty(materialName))
            return false;
        var m = RenderModeSuffixRegex.Match(materialName);
        if (!m.Success)
            return false;
        int rm;
        if (!int.TryParse(m.Groups[1].Value, out rm) || rm < 0 || rm > 3)
            return false;
        renderMode = rm;
        return true;
    }

    /// <summary>去掉材质名中的渲染模式后缀 _rm#（含其尾随的 _m##_s## 一并剥到
    ///  段首；无则原样返回）。</summary>
    public static string StripRenderModeSuffix(string materialName)
    {
        if (string.IsNullOrEmpty(materialName))
            return materialName;
        var m = RenderModeSuffixRegex.Match(materialName);
        if (!m.Success)
            return materialName;
        return materialName.Substring(0, m.Index);
    }

    /// <summary>剥离全部运行时烘焙后缀（_m##_s##、_rm# 与 _tilingWxH，任意组合），
    ///  还原 catalog 基材质名。TryResolveFloorMaterialByName 用它做基名匹配。</summary>
    public static string StripFloorVariantSuffixes(string materialName)
    {
        return StripMaterialTilingSuffix(StripPbrSuffix(StripRenderModeSuffix(materialName)));
    }

    /// <summary>地板 PBR 覆盖变体材质名的后缀段（"m50_s40" 形如 _m##_s## 前带下划线）。</summary>
    public static string PbrVariantSuffix(float metallic, float smoothness)
    {
        return "_m" + Mathf.RoundToInt(Mathf.Clamp01(metallic) * 100f)
            + "_s" + Mathf.RoundToInt(Mathf.Clamp01(smoothness) * 100f);
    }

    /// <summary>地板渲染模式覆盖变体材质名的后缀段（_rm0.._rm3）。</summary>
    public static string RenderModeVariantSuffix(int renderMode)
    {
        return "_rm" + Mathf.Clamp(renderMode, 0, 3);
    }

    /// <summary>按剥离后缀后的材质 id 在工程中查找 .mat 资产。</summary>
    public static bool TryResolveFloorMaterialByName(string materialName, out string guid, out string assetPath)
    {
        guid = null;
        assetPath = null;
        var baseName = StripFloorVariantSuffixes(materialName);
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
        // commonW2/materials：oc2_dlc_story 补充的 DLC 地板材质（与 build-catalog.mjs
        // scanFloorMaterials 的共享根列表保持同步）。
        foreach (var shared in new[] { "Assets/LevelSets", "Assets/common01/materials", "Assets/common02/materials", "Assets/commonW1/materials", "Assets/commonW2/materials" })
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
                    icon = File.Exists(FloorMaterialIconPath(id)),
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

    /** 材质贴图缩略图路径（extract-floormat-icons.py 生成到 web/public，
     *  查源目录而非 dist，不受前端构建时机影响）。
     *  注意：Unity 2017 的 .NET 框架级 Path.Combine 无 3 参重载，须链式两参。 */
    private static string FloorMaterialIconPath(string id)
    {
        var dir = Path.Combine(Application.dataPath, "../layout-editor/web/public");
        dir = Path.Combine(dir, "icons/floor-materials");
        return Path.Combine(dir, id + ".png");
    }

    private static string TidyName(string id)
    {
        var n = id ?? "";
        if (n.StartsWith("mat_", StringComparison.OrdinalIgnoreCase))
            n = n.Substring(4);
        return n.Replace('_', ' ');
    }
}
