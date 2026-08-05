using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class LayoutEditorSpriteDumper
{
    [Serializable]
    private class SpriteManifestEntry
    {
        public string name;
        public string file;
        public string bundle;
        public string type;
        public int width;
        public int height;
    }

    [Serializable]
    private class SpriteManifest
    {
        public int total;
        public int skipped;
        public SpriteManifestEntry[] sprites;
    }

    [MenuItem("Tools/Layout Editor/Dump All Sprites (C#)", false, 212)]
    public static void DumpAllSprites()
    {
        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../sprite-dump"));
        Directory.CreateDirectory(outDir);

        string windowsDir = Path.Combine(Application.streamingAssetsPath, "Windows");
        if (!Directory.Exists(windowsDir))
        {
            EditorUtility.DisplayDialog("Sprite Dump",
                "找不到 Windows AssetBundle 目录: " + windowsDir, "OK");
            return;
        }

        // Collect all bundle files: no extension (real bundles), exclude main manifest + .manifest + .meta
        var allFiles = Directory.GetFiles(windowsDir);
        var bundleNames = new List<string>();
        foreach (var f in allFiles)
        {
            string name = Path.GetFileName(f);
            if (name.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            if (name == "Windows") continue;
            if (name.Contains(".")) continue; // skip files with extensions (.meta already filtered, but belt-and-suspenders)
            bundleNames.Add(name);
        }
        bundleNames.Sort();

        if (bundleNames.Count == 0)
        {
            EditorUtility.DisplayDialog("Sprite Dump", "未找到任何 AssetBundle 文件。", "OK");
            return;
        }

        var allEntries = new List<SpriteManifestEntry>();
        var usedNames = new HashSet<string>();
        var loadedByUs = new HashSet<string>();
        int totalOk = 0, totalSkip = 0;
        int totalBundles = bundleNames.Count;

        try
        {
            for (int i = 0; i < totalBundles; i++)
            {
                string bn = bundleNames[i];
                float progress = (float)i / totalBundles;

                string label = string.Format("[{0}/{1}] {2} ...", i + 1, totalBundles, bn);
                if (EditorUtility.DisplayCancelableProgressBar("Dump Sprites", label, progress))
                    break;

                AssetBundle bundle = GetOrLoadBundle(windowsDir, bn, loadedByUs);
                if (bundle == null) continue;

                try
                {
                    // LoadAllAssets throws InvalidOperationException on scene bundles
                    UnityEngine.Object[] allAssets;
                    try
                    {
                        allAssets = bundle.LoadAllAssets();
                    }
                    catch (InvalidOperationException)
                    {
                        continue;
                    }

                    foreach (var asset in allAssets)
                    {
                        if (asset == null) continue;

                        string assetName = asset.name != null ? asset.name : "";
                        string assetType = "";

                        Texture2D tex = null;

                        Sprite sprite = asset as Sprite;
                        if (sprite != null)
                        {
                            assetType = "Sprite";
                            tex = GetSpriteTexture(sprite);
                        }
                        else
                        {
                            Texture2D tex2d = asset as Texture2D;
                            if (tex2d != null)
                            {
                                assetType = "Texture2D";
                                tex = tex2d;
                            }
                        }

                        if (tex == null) continue;
                        if (tex.width <= 1 || tex.height <= 1) continue;

                        string safe = SanitizeFilename(
                            string.IsNullOrEmpty(assetName) ? "_unnamed" : assetName);
                        string fname = safe + ".png";
                        int dup = 1;
                        while (usedNames.Contains(fname.ToLowerInvariant()))
                        {
                            dup++;
                            fname = safe + "_" + dup + ".png";
                        }
                        usedNames.Add(fname.ToLowerInvariant());

                        string destPath = Path.Combine(outDir, fname);

                        byte[] pngBytes = null;
                        try
                        {
                            Texture2D readable = MakeReadable(tex);
                            pngBytes = readable.EncodeToPNG();
                            if (readable != tex)
                                UnityEngine.Object.DestroyImmediate(readable);
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogWarning(string.Format(
                                "Sprite Dump: encode failed for {0}: {1}", assetName, ex.Message));
                            totalSkip++;
                            continue;
                        }

                        if (pngBytes == null || pngBytes.Length == 0)
                        {
                            totalSkip++;
                            continue;
                        }

                        File.WriteAllBytes(destPath, pngBytes);
                        totalOk++;

                        allEntries.Add(new SpriteManifestEntry
                        {
                            name = assetName,
                            file = fname,
                            bundle = bn,
                            type = assetType,
                            width = tex.width,
                            height = tex.height
                        });
                    }
                }
                finally
                {
                    // Only unload bundles we loaded ourselves
                    if (loadedByUs.Contains(bn))
                    {
                        bundle.Unload(true);
                        loadedByUs.Remove(bn);
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        var manifest = new SpriteManifest
        {
            total = totalOk,
            skipped = totalSkip,
            sprites = allEntries.ToArray()
        };

        string manifestPath = Path.Combine(outDir, "all-sprites.json");
        File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));

        EditorUtility.DisplayDialog("Sprite Dump",
            string.Format("导出完成！\n\n成功: {0}\n跳过: {1}\n输出目录: {2}",
                totalOk, totalSkip, outDir), "OK");
    }

    /// <summary>Try to reuse an already-loaded bundle via PseudoPrefabManager;
    /// otherwise load from disk and track it for later unloading.</summary>
    private static AssetBundle GetOrLoadBundle(string dir, string name, HashSet<string> loadedByUs)
    {
        // 1) Check PseudoPrefabManager's already-loaded bundles
        try
        {
            var bundle = LevelEditor.PseudoPrefabManager.GetAssetBundle(name);
            if (bundle != null) return bundle;
        }
        catch (KeyNotFoundException) { }

        // 2) Load from disk
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
            UnityEngine.Debug.LogWarning(string.Format(
                "Sprite Dump: cannot load bundle {0}: {1}", name, ex.Message));
            return null;
        }
    }

    private static Texture2D GetSpriteTexture(Sprite sprite)
    {
        if (sprite == null) return null;

        try
        {
            Rect rect = sprite.textureRect;
            Texture2D tex = new Texture2D((int)rect.width, (int)rect.height,
                sprite.texture.format, false);
            tex.filterMode = FilterMode.Point;

            Color[] pixels = sprite.texture.GetPixels(
                (int)rect.x, (int)rect.y,
                (int)rect.width, (int)rect.height);
            tex.SetPixels(pixels);
            tex.Apply();

            return tex;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning(string.Format(
                "Sprite Dump: failed to read sprite {0}: {1}", sprite.name, ex.Message));
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

    private static string SanitizeFilename(string s)
    {
        if (string.IsNullOrEmpty(s)) return "_";
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        }
        string result = new string(chars);
        if (result.Length > 180) result = result.Substring(0, 180);
        return result;
    }
}
