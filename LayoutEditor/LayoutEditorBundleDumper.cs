using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dumps every object of every AssetBundle in Assets/StreamingAssets/Windows to
/// &lt;项目根&gt;/dump_bundle/ (mirrors layout-editor/scripts/dump-bundle-all.py):
///   Texture2D / Sprite  -> .png (解码后的图片)
///   TextAsset           -> .txt (utf-8 文本)
///   其他所有类型         -> .json (EditorJsonUtility 序列化)
/// Writes a manifest.json listing bundle / type / container / file / format.
/// </summary>
public static class LayoutEditorBundleDumper
{
    private const string Title = "Dump Bundle";

    private struct DumpEntry
    {
        public string bundle;
        public string type;
        public string container;
        public string file;
        public string format;
    }

    public static void Dump()
    {
        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../dump_bundle"));
        Directory.CreateDirectory(outDir);

        string windowsDir = Path.Combine(Application.streamingAssetsPath, "Windows");
        if (!Directory.Exists(windowsDir))
        {
            EditorUtility.DisplayDialog(Title,
                "找不到 Windows AssetBundle 目录: " + windowsDir, "OK");
            return;
        }

        // Collect bundle files: no extension (real bundles), exclude main manifest + .manifest + .meta
        var bundleNames = new List<string>();
        foreach (var f in Directory.GetFiles(windowsDir))
        {
            string name = Path.GetFileName(f);
            if (name.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            if (name == "Windows") continue;
            if (name.Contains(".")) continue;
            bundleNames.Add(name);
        }
        bundleNames.Sort();

        if (bundleNames.Count == 0)
        {
            EditorUtility.DisplayDialog(Title, "未找到任何 AssetBundle 文件。", "OK");
            return;
        }

        var entries = new List<DumpEntry>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var formats = new Dictionary<string, int>(StringComparer.Ordinal);
        var loadedByUs = new HashSet<string>();
        int totalOk = 0, totalFail = 0, totalBundles = bundleNames.Count;
        bool cancelled = false;

        try
        {
            for (int i = 0; i < totalBundles; i++)
            {
                string bn = bundleNames[i];
                float p = 0.08f + 0.82f * ((float)i / totalBundles);
                string label = string.Format("[{0}/{1}] {2} ...", i + 1, totalBundles, bn);
                if (EditorUtility.DisplayCancelableProgressBar(Title, label, p))
                {
                    cancelled = true;
                    break;
                }

                AssetBundle bundle = GetOrLoadBundle(windowsDir, bn, loadedByUs);
                if (bundle == null)
                {
                    totalFail++;
                    continue;
                }

                try
                {
                    string[] assetNames;
                    try
                    {
                        assetNames = bundle.GetAllAssetNames();
                    }
                    catch (InvalidOperationException)
                    {
                        continue; // scene bundle
                    }
                    if (assetNames.Length == 0)
                        continue;

                    foreach (string container in assetNames)
                    {
                        try
                        {
                            foreach (var asset in bundle.LoadAssetWithSubAssets(container, typeof(UnityEngine.Object)))
                            {
                                if (asset == null)
                                {
                                    totalFail++;
                                    continue;
                                }
                                string rel;
                                string fmt;
                                if (ExportObject(asset, container, outDir, used, formats, out rel, out fmt))
                                {
                                    entries.Add(new DumpEntry
                                    {
                                        bundle = bn,
                                        type = asset.GetType().Name,
                                        container = container,
                                        file = rel.Replace('\\', '/'),
                                        format = fmt
                                    });
                                    totalOk++;
                                }
                                else
                                {
                                    totalFail++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            totalFail++;
                            Debug.LogWarning(string.Format(
                                "Dump Bundle: failed to load {0} from {1}: {2}", container, bn, ex.Message));
                        }
                    }
                }
                finally
                {
                    if (loadedByUs.Contains(bn))
                    {
                        bundle.Unload(true);
                        loadedByUs.Remove(bn);
                    }
                }
            }

            if (cancelled)
                return;

            if (EditorUtility.DisplayCancelableProgressBar(Title,
                "Writing manifest...", 0.95f))
                return;

            string manifestPath = Path.Combine(outDir, "manifest.json");
            File.WriteAllText(manifestPath, BuildManifest(entries, formats, totalBundles, totalOk, totalFail),
                new UTF8Encoding(false));

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog(Title,
                string.Format("导出完成！\n\n成功: {0}\n失败: {1}\n输出目录: {2}",
                    totalOk, totalFail, outDir), "OK");
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
    // Helpers
    // -------------------------------------------------------------

    private static bool ExportObject(UnityEngine.Object asset, string container,
        string outDir, HashSet<string> used, Dictionary<string, int> formats,
        out string rel, out string fmt)
    {
        rel = null;
        fmt = null;

        var tex = asset as Texture2D;
        if (tex == null)
        {
            var sprite = asset as Sprite;
            if (sprite != null)
                tex = GetSpriteTexture(sprite);
        }

        if (tex != null && tex.width > 1 && tex.height > 1)
        {
            try
            {
                Texture2D readable = MakeReadable(tex);
                byte[] png;
                try
                {
                    png = readable.EncodeToPNG();
                }
                finally
                {
                    if (readable != tex)
                        UnityEngine.Object.DestroyImmediate(readable);
                }
                if (png == null || png.Length == 0)
                    return false;

                string p = DedupePath(outDir, CleanPath(container) + ".png", used);
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.WriteAllBytes(p, png);
                rel = RelativePath(outDir, p);
                fmt = "png";
                formats["png"] = (formats.ContainsKey("png") ? formats["png"] : 0) + 1;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(string.Format("Dump Bundle: PNG FAIL ({0}): {1} ({2})",
                    asset.GetType().Name, container, ex.Message));
            }
        }

        var textAsset = asset as TextAsset;
        if (textAsset != null)
        {
            try
            {
                byte[] raw = textAsset.bytes;
                string text;
                try
                {
                    text = new UTF8Encoding(false, true).GetString(raw);
                }
                catch (DecoderFallbackException)
                {
                    text = Encoding.GetEncoding(28591).GetString(raw); // latin-1
                }
                string p = DedupePath(outDir, CleanPath(container) + ".txt", used);
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.WriteAllText(p, text, new UTF8Encoding(false));
                rel = RelativePath(outDir, p);
                fmt = "txt";
                formats["txt"] = (formats.ContainsKey("txt") ? formats["txt"] : 0) + 1;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(string.Format("Dump Bundle: TEXT FAIL ({0}): {1} ({2})",
                    asset.GetType().Name, container, ex.Message));
            }
        }

        try
        {
            string json = EditorJsonUtility.ToJson(asset);
            if (string.IsNullOrEmpty(json) || json.Trim().Length == 0 || json == "{}")
                json = "{\"name\":\"" + EscapeJson(asset.name) + "\",\"type\":\"" + asset.GetType().Name + "\"}";
            string p = DedupePath(outDir, CleanPath(container) + ".json", used);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllText(p, json, new UTF8Encoding(false));
            rel = RelativePath(outDir, p);
            fmt = "json";
            formats["json"] = (formats.ContainsKey("json") ? formats["json"] : 0) + 1;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning(string.Format("Dump Bundle: JSON FAIL ({0}): {1} ({2})",
                asset.GetType().Name, container, ex.Message));
        }

        return false;
    }

    /// <summary>Try to reuse an already-loaded bundle via PseudoPrefabManager;
    /// otherwise load from disk and track it for later unloading.</summary>
    private static AssetBundle GetOrLoadBundle(string dir, string name, HashSet<string> loadedByUs)
    {
        try
        {
            var bundle = LevelEditor.PseudoPrefabManager.GetAssetBundle(name);
            if (bundle != null) return bundle;
        }
        catch (System.Exception) { }

        string path = Path.Combine(dir, name);
        if (!File.Exists(path)) return null;

        try
        {
            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle != null)
                loadedByUs.Add(name);
            return bundle;
        }
        catch (Exception ex)
        {
            Debug.LogWarning(string.Format(
                "Dump Bundle: cannot load bundle {0}: {1}", name, ex.Message));
            return null;
        }
    }

    private static Texture2D GetSpriteTexture(Sprite sprite)
    {
        try
        {
            Rect rect = sprite.textureRect;
            Texture2D tex = new Texture2D((int)rect.width, (int)rect.height,
                sprite.texture.format, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels(sprite.texture.GetPixels(
                (int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height));
            tex.Apply();
            return tex;
        }
        catch (Exception ex)
        {
            Debug.LogWarning(string.Format(
                "Dump Bundle: failed to read sprite {0}: {1}", sprite.name, ex.Message));
            return null;
        }
    }

    private static Texture2D MakeReadable(Texture2D source)
    {
        try
        {
            source.GetPixel(0, 0);
            return source;
        }
        catch (UnityException)
        {
            // Texture is not readable, blit via RenderTexture
        }

        RenderTexture rt = RenderTexture.GetTemporary(
            source.width, source.height, 0,
            RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Graphics.Blit(source, rt);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readable = new Texture2D(source.width, source.height,
            TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readable.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        return readable;
    }

    private static string RelativePath(string root, string path)
    {
        return path.Substring(root.Length + 1);
    }

    /// <summary>container -> filesystem-safe path; assets/ prefix normalized to Assets/.</summary>
    private static string CleanPath(string container)
    {
        if (string.IsNullOrEmpty(container))
            return "_unassigned";
        var parts = container.Replace('\\', '/').Split('/');
        var cleaned = new string[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            cleaned[i] = SanitizeSegment(parts[i]);
        if (cleaned.Length > 0 && cleaned[0].Equals("Assets", StringComparison.OrdinalIgnoreCase))
            cleaned[0] = "Assets";
        return string.Join("/", cleaned);
    }

    private static string SanitizeSegment(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "_unnamed";
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        string result = new string(chars).Trim();
        return result.Length == 0 ? "_unnamed" : result;
    }

    /// <summary>Same container with multiple objects: append _2, _3 … so nothing is overwritten.</summary>
    private static string DedupePath(string outDir, string rel, HashSet<string> used)
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
        return Path.Combine(outDir, candidate);
    }

    private static string BuildManifest(List<DumpEntry> entries, Dictionary<string, int> formats,
        int bundles, int totalOk, int totalFail)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"total\": " + totalOk + ",");
        sb.AppendLine("  \"failed\": " + totalFail + ",");
        sb.AppendLine("  \"formats\": {");
        var keys = new List<string>(formats.Keys);
        keys.Sort(StringComparer.Ordinal);
        for (int i = 0; i < keys.Count; i++)
            sb.AppendLine("    \"" + keys[i] + "\": " + formats[keys[i]] + (i < keys.Count - 1 ? "," : ""));
        sb.AppendLine("  },");
        sb.AppendLine("  \"bundles\": " + bundles + ",");
        sb.AppendLine("  \"objects\": [");
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            sb.Append("    {\"bundle\": \"" + EscapeJson(e.bundle)
                + "\", \"type\": \"" + EscapeJson(e.type)
                + "\", \"container\": \"" + EscapeJson(e.container)
                + "\", \"file\": \"" + EscapeJson(e.file)
                + "\", \"format\": \"" + EscapeJson(e.format) + "\"}");
            sb.AppendLine(i < entries.Count - 1 ? "," : "");
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
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
