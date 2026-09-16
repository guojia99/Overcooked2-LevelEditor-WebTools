using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 在 dump_bundle 基础上，对 png / jpg 图片做单独的导出管理。
/// 按 container 路径把图片分为两类，统一输出到 &lt;项目根&gt;/dump_images/：
///   dump_images/建模贴图/  —— 模型 / fbx / obj / map 相关贴图
///                          （models、scenes、meshes、fbxexporters、particles、
///                           postprocessing、standard assets、textures 等目录）
///   dump_images/素材图/    —— 背景图、icon、UI 素材等其余图片
/// 同时输出 dump_images/manifest.json 记录每张图片的分类、container 与来源文件。
/// </summary>
public static class LayoutEditorImageExporter
{
    private const string Title = "Export Images";

    private static readonly string[] ModelSegments =
    {
        "models", "meshes", "fbxexporters", "scenes", "materials",
        "textures", "particles", "postprocessing", "post-processing",
        "post processing", "standard assets", "effects", "skyboxes"
    };

    private struct ImageEntry
    {
        public string category;
        public string container;
        public string file;
        public string source;
    }

    public static void Export()
    {
        string sourceDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../dump_bundle/Assets"));
        if (!Directory.Exists(sourceDir))
        {
            bool runDump = EditorUtility.DisplayDialog(Title,
                "找不到 dump_bundle 导出目录：\n" + sourceDir +
                "\n\n是否先执行『导出 Bundle 全部内容』生成 dump_bundle，然后继续导出素材图？",
                "是", "否");
            if (!runDump)
                return;
            LayoutEditorBundleDumper.Dump();
            if (!Directory.Exists(sourceDir))
            {
                EditorUtility.DisplayDialog(Title, "dump_bundle 未生成，导出已取消。", "OK");
                return;
            }
        }

        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../dump_images"));
        string modelDir = Path.Combine(outDir, "建模贴图");
        string assetDir = Path.Combine(outDir, "素材图");

        var files = CollectImages(sourceDir);
        if (files.Count == 0)
        {
            EditorUtility.DisplayDialog(Title, "dump_bundle 中未找到任何 png / jpg 图片。", "OK");
            return;
        }

        if (Directory.Exists(outDir))
            Directory.Delete(outDir, true);
        Directory.CreateDirectory(modelDir);
        Directory.CreateDirectory(assetDir);

        int ok = 0, fail = 0;
        var entries = new List<ImageEntry>();
        var usedModel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedAsset = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool cancelled = false;

        try
        {
            for (int i = 0; i < files.Count; i++)
            {
                string src = files[i];
                string rel = RelativeTo(sourceDir, src);
                if (EditorUtility.DisplayCancelableProgressBar(Title,
                    string.Format("[{0}/{1}] {2}", i + 1, files.Count, rel),
                    (float)i / files.Count))
                {
                    cancelled = true;
                    break;
                }

                string container = "Assets/" + rel.Replace('\\', '/');
                bool isModel = IsModelTexture(container);
                string category = isModel ? "建模贴图" : "素材图";
                var used = isModel ? usedModel : usedAsset;
                string catDir = isModel ? modelDir : assetDir;

                string destRel = DedupePath(used, StripAssetsPrefix(container));
                string dest = Path.Combine(catDir, destRel.Replace('/', Path.DirectorySeparatorChar));
                try
                {
                    string parent = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(parent))
                        Directory.CreateDirectory(parent);
                    File.Copy(src, dest, false);
                    ok++;
                    entries.Add(new ImageEntry
                    {
                        category = category,
                        container = container,
                        file = (category + "/" + destRel).Replace('\\', '/'),
                        source = "dump_bundle/Assets/" + rel.Replace('\\', '/')
                    });
                }
                catch (Exception ex)
                {
                    fail++;
                    Debug.LogWarning(string.Format("{0}: copy failed ({1}): {2}", Title, src, ex.Message));
                }
            }

            if (cancelled)
                return;

            if (EditorUtility.DisplayCancelableProgressBar(Title, "Writing manifest...", 0.99f))
                return;

            File.WriteAllText(Path.Combine(outDir, "manifest.json"),
                BuildManifest(entries, fail), new System.Text.UTF8Encoding(false));

            EditorUtility.ClearProgressBar();
            int modelCount = 0, assetCount = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].category == "建模贴图") modelCount++;
                else assetCount++;
            }
            EditorUtility.DisplayDialog(Title,
                string.Format("导出完成！\n\n成功: {0}    失败: {1}\n建模贴图: {2}\n素材图: {3}\n\n输出目录:\n{4}",
                    ok, fail, modelCount, assetCount, outDir), "OK");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError(Title + " failed: " + ex);
            EditorUtility.DisplayDialog(Title + " Error", ex.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // -------------------------------------------------------------
    // Classification
    // -------------------------------------------------------------

    /// <summary>按 container 路径判断图片是否属于建模/map 贴图；
    /// 任一路径段命中模型相关目录即视为建模贴图，其余归为素材图。</summary>
    private static bool IsModelTexture(string container)
    {
        string[] parts = container.Replace('\\', '/').Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            string seg = parts[i].ToLowerInvariant();
            for (int j = 0; j < ModelSegments.Length; j++)
            {
                if (seg == ModelSegments[j])
                    return true;
            }
        }
        return false;
    }

    /// <summary>去掉 container 开头的 Assets/ 前缀，作为分类目录下的相对路径。</summary>
    private static string StripAssetsPrefix(string container)
    {
        string c = container.Replace('\\', '/');
        if (c.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            c = c.Substring("Assets/".Length);
        return c;
    }

    // -------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------

    private static List<string> CollectImages(string root)
    {
        var list = new List<string>();
        foreach (string f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(f);
            if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                list.Add(f);
        }
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    private static string RelativeTo(string root, string path)
    {
        return path.Substring(root.Length + 1);
    }

    /// <summary>同一相对路径出现多次时追加 _2, _3 …，避免覆盖。</summary>
    private static string DedupePath(HashSet<string> used, string rel)
    {
        string basePath = rel;
        string ext = Path.GetExtension(rel);
        if (!string.IsNullOrEmpty(ext))
            basePath = rel.Substring(0, rel.Length - ext.Length);

        string candidate = rel;
        int counter = 1;
        while (used.Contains(candidate))
        {
            counter++;
            candidate = basePath + "_" + counter + ext;
        }
        used.Add(candidate);
        return candidate;
    }

    private static string BuildManifest(List<ImageEntry> entries, int fail)
    {
        int modelCount = 0, assetCount = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].category == "建模贴图") modelCount++;
            else assetCount++;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"total\": " + entries.Count + ",");
        sb.AppendLine("  \"failed\": " + fail + ",");
        sb.AppendLine("  \"categories\": {");
        sb.AppendLine("    \"建模贴图\": " + modelCount + ",");
        sb.AppendLine("    \"素材图\": " + assetCount);
        sb.AppendLine("  },");
        sb.AppendLine("  \"generatedAt\": \"" + DateTime.UtcNow.ToString("o") + "\",");
        sb.AppendLine("  \"images\": [");
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            sb.Append("    {\"category\": \"" + EscapeJson(e.category)
                + "\", \"container\": \"" + EscapeJson(e.container)
                + "\", \"file\": \"" + EscapeJson(e.file)
                + "\", \"source\": \"" + EscapeJson(e.source) + "\"}");
            sb.AppendLine(i < entries.Count - 1 ? "," : "");
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
