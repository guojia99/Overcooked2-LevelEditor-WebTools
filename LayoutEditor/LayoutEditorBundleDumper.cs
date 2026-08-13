using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dumps every object of every AssetBundle in Assets/StreamingAssets/Windows to
/// &lt;项目根&gt;/dump_bundle/ (mirrors layout-editor/scripts/dump-bundle-all.py):
///   Texture2D / Sprite  -> .png (解码后的图片)
///   Cubemap             -> .png x6 (每面一张 _px/_nx/_py/_ny/_pz/_nz)
///   TextAsset           -> .txt (utf-8 文本)
///   Mesh                -> .obj (Wavefront，与 UnityPy 参考实现一致)
///   AudioClip           -> .wav (GetData 解码 PCM + RIFF 头)
///   模型容器 (主资源为含网格的 GameObject，如 .fbx/.prefab，不论后缀) -> 单个 .obj
///     完整模型: 实例化层级、烘焙蒙皮网格 (SkinnedMeshRenderer.BakeMesh)、
///     按根物体局部空间合并；其 GameObject 与 Mesh 子资源都指向该文件
///   Font / 其他所有类型  -> .json (EditorJsonUtility 序列化)
/// 文件名规则: container 的扩展名替换为真实格式扩展名 (foo.fbx -> foo.obj)。
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
                            string modelFile;
                            TryExportContainerModel(bundle, container, outDir, used, formats, out modelFile);

                            foreach (var asset in bundle.LoadAssetWithSubAssets(container, typeof(UnityEngine.Object)))
                            {
                                if (asset == null)
                                {
                                    totalFail++;
                                    continue;
                                }
                                string rel;
                                string fmt;
                                if (ExportObject(asset, container, modelFile, outDir, used, formats, out rel, out fmt))
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

    private static bool ExportObject(UnityEngine.Object asset, string container, string modelFile,
        string outDir, HashSet<string> used, Dictionary<string, int> formats,
        out string rel, out string fmt)
    {
        rel = null;
        fmt = null;

        // Texture2D / Sprite -> PNG
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

                string p = DedupePath(outDir, WithRealExtension(CleanPath(container), ".png"), used);
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

        // Cubemap -> 6 face PNGs (_px/_nx/_py/_ny/_pz/_nz)
        var cube = asset as Cubemap;
        if (cube != null)
        {
            try
            {
                string stem = WithRealExtension(CleanPath(container), "");
                string first = null;
                for (int i = 0; i < CubeSuffixes.Length; i++)
                {
                    Color[] pixels = cube.GetPixels(CubeFaces[i]);
                    Texture2D faceTex = new Texture2D(cube.width, cube.width, TextureFormat.RGBA32, false);
                    string facePath;
                    try
                    {
                        faceTex.SetPixels(pixels);
                        faceTex.Apply();
                        facePath = DedupePath(outDir, stem + CubeSuffixes[i] + ".png", used);
                        Directory.CreateDirectory(Path.GetDirectoryName(facePath));
                        File.WriteAllBytes(facePath, faceTex.EncodeToPNG());
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(faceTex);
                    }
                    if (i == 0)
                        first = facePath;
                }
                rel = RelativePath(outDir, first);
                fmt = "png";
                formats["png"] = (formats.ContainsKey("png") ? formats["png"] : 0) + 1;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(string.Format("Dump Bundle: CUBEMAP FAIL ({0}): {1} ({2})",
                    asset.GetType().Name, container, ex.Message));
            }
        }

        // TextAsset -> txt
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
                string p = DedupePath(outDir, WithRealExtension(CleanPath(container), ".txt"), used);
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

        // Mesh -> container model OBJ (if the container was exported as a full model),
        // otherwise individual OBJ
        var mesh = asset as Mesh;
        if (mesh != null)
        {
            if (modelFile != null)
            {
                rel = modelFile;
                fmt = "obj";
                return true;
            }
            try
            {
                string obj = ExportMeshObj(mesh);
                if (!string.IsNullOrEmpty(obj))
                {
                    string p = DedupePath(outDir, WithRealExtension(CleanPath(container), ".obj"), used);
                    Directory.CreateDirectory(Path.GetDirectoryName(p));
                    File.WriteAllText(p, obj, new UTF8Encoding(false));
                    rel = RelativePath(outDir, p);
                    fmt = "obj";
                    formats["obj"] = (formats.ContainsKey("obj") ? formats["obj"] : 0) + 1;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(string.Format("Dump Bundle: OBJ FAIL ({0}): {1} ({2})",
                    asset.GetType().Name, container, ex.Message));
            }
        }

        // AudioClip -> WAV (decoded PCM)
        var clip = asset as AudioClip;
        if (clip != null)
        {
            try
            {
                if (clip.samples > 0 && clip.channels > 0 && clip.frequency > 0)
                {
                    float[] data = new float[clip.samples * clip.channels];
                    if (clip.GetData(data, 0))
                    {
                        byte[] wav = BuildWav(data, clip.channels, clip.frequency);
                        if (wav != null && wav.Length > 0)
                        {
                            string p = DedupePath(outDir, WithRealExtension(CleanPath(container), ".wav"), used);
                            Directory.CreateDirectory(Path.GetDirectoryName(p));
                            File.WriteAllBytes(p, wav);
                            rel = RelativePath(outDir, p);
                            fmt = "wav";
                            formats["wav"] = (formats.ContainsKey("wav") ? formats["wav"] : 0) + 1;
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(string.Format("Dump Bundle: AUDIO FAIL ({0}): {1} ({2})",
                    asset.GetType().Name, container, ex.Message));
            }
        }

        // GameObject -> container model OBJ (if the container was exported as a full model)
        if (asset is GameObject && modelFile != null)
        {
            rel = modelFile;
            fmt = "obj";
            return true;
        }

        // Everything else -> JSON
        try
        {
            string json = EditorJsonUtility.ToJson(asset);
            if (string.IsNullOrEmpty(json) || json.Trim().Length == 0 || json == "{}")
                json = "{\"name\":\"" + EscapeJson(asset.name) + "\",\"type\":\"" + asset.GetType().Name + "\"}";
            string p = DedupePath(outDir, WithRealExtension(CleanPath(container), ".json"), used);
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

    private static readonly CubemapFace[] CubeFaces = {
        CubemapFace.PositiveX, CubemapFace.NegativeX,
        CubemapFace.PositiveY, CubemapFace.NegativeY,
        CubemapFace.PositiveZ, CubemapFace.NegativeZ
    };

    private static readonly string[] CubeSuffixes = { "_px", "_nx", "_py", "_ny", "_pz", "_nz" };

    /// <summary>Replace the extension of the last path segment with newExt;
    /// append newExt when the path has no extension.</summary>
    private static string WithRealExtension(string rel, string newExt)
    {
        int slash = rel.LastIndexOf('/');
        int dot = rel.LastIndexOf('.');
        if (dot > slash)
            rel = rel.Substring(0, dot);
        return rel + newExt;
    }

    /// <summary>Export a container whose main asset is a GameObject with renderers
    /// (any suffix, e.g. .fbx/.prefab) as one complete model OBJ: instantiate the
    /// hierarchy, bake skinned meshes and merge everything in the root's local space.
    /// The GameObject and Mesh sub-assets of this container map to that file.</summary>
    private static bool TryExportContainerModel(AssetBundle bundle, string container,
        string outDir, HashSet<string> used, Dictionary<string, int> formats, out string rel)
    {
        rel = null;

        GameObject root;
        try
        {
            root = bundle.LoadAsset<GameObject>(container);
        }
        catch (Exception)
        {
            return false;
        }
        if (root == null)
            return false;

        GameObject instance = null;
        try
        {
            instance = (GameObject)UnityEngine.Object.Instantiate(root);
            string obj = BuildModelObj(instance);
            if (string.IsNullOrEmpty(obj))
                return false;

            string p = DedupePath(outDir, WithRealExtension(CleanPath(container), ".obj"), used);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllText(p, obj, new UTF8Encoding(false));
            rel = RelativePath(outDir, p);
            formats["obj"] = (formats.ContainsKey("obj") ? formats["obj"] : 0) + 1;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning(string.Format("Dump Bundle: MODEL FAIL ({0}): {1} ({2})",
                container, ex.Message));
            return false;
        }
        finally
        {
            if (instance != null)
                UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    /// <summary>Merge every MeshFilter / SkinnedMeshRenderer of the hierarchy into one
    /// OBJ text, transformed into the root's local space.</summary>
    private static string BuildModelObj(GameObject root)
    {
        Matrix4x4 rootInv = root.transform.worldToLocalMatrix;
        var sb = new StringBuilder();
        int offset = 0;

        var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter mf = meshFilters[i];
            if (mf.sharedMesh == null)
                continue;
            offset = AppendModelMesh(sb, mf.sharedMesh,
                rootInv * mf.transform.localToWorldMatrix, TransformPath(mf.transform), offset);
        }

        var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinned.Length; i++)
        {
            SkinnedMeshRenderer smr = skinned[i];
            if (smr.sharedMesh == null)
                continue;
            Mesh baked = new Mesh();
            try
            {
                smr.BakeMesh(baked);
                offset = AppendModelMesh(sb, baked,
                    rootInv * smr.transform.localToWorldMatrix, TransformPath(smr.transform), offset);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(string.Format("Dump Bundle: SKIN BAKE FAIL ({0}): {1} ({2})",
                    TransformPath(smr.transform), smr.sharedMesh.name, ex.Message));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baked);
            }
        }
        return offset > 0 ? sb.ToString() : null;
    }

    /// <summary>Append one mesh (transformed by m, vertex indices offset) to an OBJ
    /// StringBuilder; returns the new global vertex offset.</summary>
    private static int AppendModelMesh(StringBuilder sb, Mesh mesh, Matrix4x4 m,
        string group, int offset)
    {
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = m.MultiplyPoint3x4(vertices[i]);
            sb.Append("v ").Append(Fmt(-v.x)).Append(' ')
                .Append(Fmt(v.y)).Append(' ')
                .Append(Fmt(v.z)).Append('\n');
        }

        Vector2[] uv = mesh.uv;
        if (uv != null && uv.Length > 0)
        {
            for (int i = 0; i < uv.Length; i++)
                sb.Append("vt ").Append(Fmt(uv[i].x)).Append(' ')
                    .Append(Fmt(uv[i].y)).Append('\n');
        }

        Vector3[] normals = mesh.normals;
        if (normals != null && normals.Length > 0)
        {
            for (int i = 0; i < normals.Length; i++)
            {
                Vector3 n = m.MultiplyVector(normals[i]);
                n.Normalize();
                sb.Append("vn ").Append(Fmt(-n.x)).Append(' ')
                    .Append(Fmt(n.y)).Append(' ')
                    .Append(Fmt(n.z)).Append('\n');
            }
        }

        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            sb.Append("g ").Append(group).Append('_').Append(s).Append('\n');
            int[] tris = mesh.GetTriangles(s);
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i] + 1 + offset, b = tris[i + 1] + 1 + offset, c = tris[i + 2] + 1 + offset;
                sb.Append("f ").Append(c).Append('/').Append(c).Append('/').Append(c).Append(' ')
                    .Append(b).Append('/').Append(b).Append('/').Append(b).Append(' ')
                    .Append(a).Append('/').Append(a).Append('/').Append(a).Append('\n');
            }
        }
        return offset + vertices.Length;
    }

    /// <summary>Hierarchy path of a transform, e.g. "Root/Body/Arm".</summary>
    private static string TransformPath(Transform t)
    {
        string path = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }

    /// <summary>Export a Mesh as Wavefront OBJ text; mirrors UnityPy's
    /// export_mesh_obj (X axis negated, reversed winding).</summary>
    private static string ExportMeshObj(Mesh mesh)
    {
        var sb = new StringBuilder();
        sb.Append("g ").Append(mesh.name).Append('\n');

        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            sb.Append("v ").Append(Fmt(-v.x)).Append(' ')
                .Append(Fmt(v.y)).Append(' ')
                .Append(Fmt(v.z)).Append('\n');
        }

        Vector2[] uv = mesh.uv;
        if (uv != null && uv.Length > 0)
        {
            for (int i = 0; i < uv.Length; i++)
                sb.Append("vt ").Append(Fmt(uv[i].x)).Append(' ')
                    .Append(Fmt(uv[i].y)).Append('\n');
        }

        Vector3[] normals = mesh.normals;
        if (normals != null && normals.Length > 0)
        {
            for (int i = 0; i < normals.Length; i++)
            {
                Vector3 n = normals[i];
                sb.Append("vn ").Append(Fmt(-n.x)).Append(' ')
                    .Append(Fmt(n.y)).Append(' ')
                    .Append(Fmt(n.z)).Append('\n');
            }
        }

        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            sb.Append("g ").Append(mesh.name).Append('_').Append(s).Append('\n');
            int[] tris = mesh.GetTriangles(s);
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i] + 1, b = tris[i + 1] + 1, c = tris[i + 2] + 1;
                sb.Append("f ").Append(c).Append('/').Append(c).Append('/').Append(c).Append(' ')
                    .Append(b).Append('/').Append(b).Append('/').Append(b).Append(' ')
                    .Append(a).Append('/').Append(a).Append('/').Append(a).Append('\n');
            }
        }
        return sb.ToString();
    }

    private static string Fmt(float f)
    {
        if (float.IsNaN(f) || float.IsInfinity(f))
            return "0";
        return f.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>Encode interleaved float PCM (-1..1) as a 16-bit RIFF/WAVE byte array.</summary>
    private static byte[] BuildWav(float[] samples, int channels, int sampleRate)
    {
        if (samples == null || samples.Length == 0 || channels <= 0 || sampleRate <= 0)
            return null;

        int dataSize = samples.Length * 2;
        byte[] result;
        using (var ms = new MemoryStream(dataSize + 44))
        {
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + dataSize);
                bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1); // PCM
                bw.Write((short)channels);
                bw.Write(sampleRate);
                bw.Write(sampleRate * channels * 2); // byte rate
                bw.Write((short)(channels * 2));     // block align
                bw.Write((short)16);                 // bits per sample
                bw.Write(Encoding.ASCII.GetBytes("data"));
                bw.Write(dataSize);
                for (int i = 0; i < samples.Length; i++)
                {
                    float f = samples[i];
                    if (f > 1f) f = 1f;
                    else if (f < -1f) f = -1f;
                    bw.Write((short)(f * 32767f));
                }
                bw.Flush();
                result = ms.ToArray();
            }
        }
        return result;
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
