using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using LevelEditorStub;

/// <summary>
/// 桌台皮肤 3D 资产导出（俯视图 + 模型）：
/// 遍历 counters 伪预制件 SO（与 IconExport 同源），按 bundle 加载真实 prefab，
/// 临时实例化后产出三件套到 exports/counter-skins/（repo 根，git 忽略，类比 audio-exports/）：
///   ① <Type>_<Theme>_top.png —— 正交相机俯拍（Sprites/Default 无光照直出 diffuse×tint，
///      透明背景，宽高比 = 模型 X:Z 包围盒，长边 256）→ 2D 画布「🎨 桌台皮肤」开关直接绘制
///   ② <Type>_<Theme>.obj/.mtl + _m&lt;i&gt;.png —— 世界变换烘平网格 + 材质 diffuse（≤1024）
///      → 前端 openModelPreview 3D 预览（OBJ 轴/绕序惯例与 BundleDumper 一致，three.js 已验证）
///   ③ manifest.json —— fileKey → { top, obj, mtl, mats[{tex,tint}], size{x,y,z}, minY }, failures
/// HTTP 由 LayoutEditorHttpServer 的 /api/counter-skins/3d/&lt;name&gt; 只读路由提供。
/// 幂等全量覆盖；菜单：Layout Editor → 素材导出 (Asset Export) → 导出桌台皮肤 3D 模型与俯视图。
/// </summary>
public static class LayoutEditorCounterSkin3dExport
{
    private const int TopTexSize = 256;
    private const int MaxMatTexSize = 1024;
    private const float Padding = 1.04f;

    [MenuItem("Layout Editor/素材导出 (Asset Export)/导出桌台皮肤 3D 模型与俯视图 (Export Skin 3D)", false, 1)]
    public static void ExportAll()
    {
        Array.Sort(LayoutEditorCounterSkinIconExport.CounterTypes, (a, b) => b.Length.CompareTo(a.Length));
        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string outDir = Path.Combine(repoRoot, "exports/counter-skins").Replace("\\", "/");
        Directory.CreateDirectory(outDir);

        List<string> soFiles = LayoutEditorCounterSkinIconExport.CollectCounterSoFiles();
        if (soFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("桌台 3D 导出", "未找到任何 counters 伪预制件 SO（pseudo_prefab_so/counters）", "确定");
            return;
        }

        var own = new List<AssetBundle>();
        var bundles = new Dictionary<string, AssetBundle>();
        AssetBundleManifest manifest = LayoutEditorCounterSkinIconExport.LoadManifest(bundles, own);
        if (manifest == null)
            Debug.LogWarning("[LayoutEditor/Export] Windows manifest 加载失败：自加载 bundle 缺依赖时对应皮肤可能失败");

        var items = new List<Dictionary<string, string>>(); // fileKey/type/theme/top/obj/matsN/size/minY/error
        int done = 0;
        try
        {
            for (int i = 0; i < soFiles.Count; i++)
            {
                string file = soFiles[i];
                EditorUtility.DisplayProgressBar("桌台 3D 导出", file, (float)i / soFiles.Count);
                var row = ExtractOne(outDir, bundles, own, manifest, file);
                if (row != null) { items.Add(row); done++; }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            foreach (AssetBundle b in own) b.Unload(true);
            bundles.Clear();
        }

        WriteManifest(Path.Combine(outDir, "manifest.json").Replace("\\", "/"), items);

        int ok = 0, failed = 0;
        var sb = new StringBuilder();
        foreach (var r in items)
        {
            if (string.IsNullOrEmpty(r["error"])) ok++;
            else
            {
                failed++;
                if (sb.Length < 1500) sb.Append("\n").Append(r["type"]).Append("_").Append(r["theme"]).Append(": ").Append(r["error"]);
            }
        }
        string report = "共 " + done + " 个皮肤，成功 " + ok + "，失败 " + failed +
            "\n输出 → exports/counter-skins/（manifest.json + _top.png + .obj/.mtl/_m*.png）" +
            "\n前端经 /api/counter-skins/3d/ 路由读取，无需重新构建前端";
        if (sb.Length > 0) report += "\n失败明细（前若干条）:" + sb;
        Debug.Log("[LayoutEditor/Export] " + report.Replace("\n", " | "));
        EditorUtility.DisplayDialog("桌台 3D 导出", report, "确定");
    }

    private class SkinMatInfo
    {
        public Texture2D tex;      // diffuse（可能 null = 纯色槽）
        public Color tint = Color.white;
    }

    private static Dictionary<string, string> ExtractOne(string outDir, Dictionary<string, AssetBundle> bundles, List<AssetBundle> own, AssetBundleManifest manifest, string assetPath)
    {
        var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(assetPath) as PseudoPrefabSO;
        if (so == null) return null;
        string id = Path.GetFileNameWithoutExtension(assetPath);
        string type = null;
        foreach (string ct in LayoutEditorCounterSkinIconExport.CounterTypes)
        {
            if (id.StartsWith(ct)) { type = ct; break; }
        }
        if (type == null) return null;
        string theme = id.Substring(type.Length);
        if (theme.EndsWith("SO")) theme = theme.Substring(0, theme.Length - 2);
        string fileKey = type + "_" + (string.IsNullOrEmpty(theme) ? "Default" : theme);

        var row = new Dictionary<string, string>();
        row["fileKey"] = fileKey;
        row["type"] = type;
        row["theme"] = string.IsNullOrEmpty(theme) ? "Default" : theme;
        row["error"] = "";

        GameObject inst = null;
        RenderTexture rt = null;
        GameObject camGo = null;
        try
        {
            AssetBundle bundle = LayoutEditorCounterSkinIconExport.EnsureBundleLoaded(so.bundleName, bundles, own, manifest);
            if (bundle == null) { row["error"] = "bundle 加载失败: " + so.bundleName; return row; }
            var prefab = bundle.LoadAsset<GameObject>(so.assetPath);
            if (prefab == null) { row["error"] = "prefab 加载失败: " + so.assetPath; return row; }

            Vector3 offset = new Vector3(-5000f, 0f, -5000f);
            inst = (GameObject)UnityEngine.Object.Instantiate(prefab, offset, Quaternion.identity);

            // ① 仅收集参与渲染（active+enabled）的渲染器；先取各槽 diffuse+tint（材质替换前）
            var renderers = inst.GetComponentsInChildren<Renderer>(false);
            var list = new List<Renderer>();
            foreach (Renderer r in renderers)
                if (r != null && r.enabled) list.Add(r);
            if (list.Count == 0) { row["error"] = "无可渲染网格"; return row; }

            var slots = new List<List<SkinMatInfo>>(); // per renderer: per submesh
            foreach (Renderer r in list)
            {
                var per = new List<SkinMatInfo>();
                var mats = r.sharedMaterials;
                int subCount = SubMeshCountOf(r);
                for (int i = 0; i < subCount; i++)
                {
                    var info = new SkinMatInfo();
                    if (mats != null && i < mats.Length && mats[i] != null)
                    {
                        Color tint;
                        info.tex = LayoutEditorCounterSkinIconExport.DiffuseOfMaterial(mats[i], out tint);
                        info.tint = tint;
                    }
                    per.Add(info);
                }
                slots.Add(per);
            }

            // ② 包围盒（世界）
            Bounds bounds = list[0].bounds;
            for (int i = 1; i < list.Count; i++) bounds.Encapsulate(list[i].bounds);
            float extX = bounds.extents.x * Padding, extZ = bounds.extents.z * Padding;
            float maxExt = Math.Max(extX, extZ);
            int w = Math.Max(8, Mathf.RoundToInt(TopTexSize * extX / maxExt));
            int h = Math.Max(8, Mathf.RoundToInt(TopTexSize * extZ / maxExt));

            // ③ 材质替换为 Sprites/Default（无光照直出 diffuse×tint，透明背景）
            Shader unlitShader = Shader.Find("Sprites/Default");
            if (unlitShader == null) { row["error"] = "Sprites/Default shader 缺失"; return row; }
            var tempMats = new List<Material>();
            for (int ri = 0; ri < list.Count; ri++)
            {
                var per = slots[ri];
                var newMats = new Material[per.Count];
                for (int mi = 0; mi < per.Count; mi++)
                {
                    var m = new Material(unlitShader);
                    if (per[mi].tex != null) m.mainTexture = per[mi].tex;
                    m.SetColor("_Color", ApplyTint(per[mi]));
                    tempMats.Add(m);
                    newMats[mi] = m;
                }
                list[ri].sharedMaterials = newMats;
            }

            // ④ 正交俯拍：Euler(90,180,0) → 画面上=世界 -Z（与画布 z 向下一致）
            camGo = new GameObject("CounterSkinTopCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.orthographicSize = maxExt;
            cam.aspect = (float)w / h;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = bounds.max.y - offset.y + 50f;
            cam.transform.position = new Vector3(bounds.center.x, bounds.max.y + 20f, bounds.center.z);
            cam.transform.rotation = Quaternion.Euler(90f, 180f, 0f);

            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var png = new Texture2D(w, h, TextureFormat.ARGB32, false, false);
            png.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            png.Apply(false);
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(outDir, fileKey + "_top.png"), png.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(png);
            row["top"] = fileKey + "_top.png";

            // ⑤ OBJ/MTL：世界变换烘平（减去实例偏移），轴/绕序与 BundleDumper.ExportMeshObj 惯例一致
            var texIndex = new Dictionary<Texture2D, int>();   // 贴图去重索引
            var matFiles = new List<string>();                 // _m<i>.png 文件名（与 usemtl 序号一致）
            var matTints = new List<Color>();
            var obj = new StringBuilder();
            var mtl = new StringBuilder();
            int vBase = 0;
            obj.Append("# OC2 counter skin ").Append(fileKey).Append('\n')
               .Append("mtllib ").Append(fileKey).Append(".mtl").Append('\n');
            mtl.Append("# ").Append(fileKey).Append('\n');

            for (int ri = 0; ri < list.Count; ri++)
            {
                Renderer r = list[ri];
                Mesh mesh = MeshOf(r);
                if (mesh == null) continue;
                Matrix4x4 m = r.transform.localToWorldMatrix;
                Vector3[] verts = mesh.vertices;
                Vector2[] uvs = mesh.uv;
                int vCount = verts.Length;
                for (int i = 0; i < vCount; i++)
                {
                    Vector3 v = m.MultiplyPoint3x4(verts[i]) - offset;
                    obj.Append("v ").Append(Fmt(-v.x)).Append(' ').Append(Fmt(v.y)).Append(' ').Append(Fmt(v.z)).Append('\n');
                }
                if (uvs != null && uvs.Length > 0)
                {
                    for (int i = 0; i < vCount; i++)
                        obj.Append("vt ").Append(Fmt(uvs[i].x)).Append(' ').Append(Fmt(uvs[i].y)).Append('\n');
                }
                bool hasUv = uvs != null && uvs.Length >= vCount && vCount > 0;
                var per = slots[ri];
                for (int s = 0; s < mesh.subMeshCount && s < per.Count; s++)
                {
                    int matIdx;
                    Texture2D tex = per[s].tex;
                    if (tex != null && texIndex.TryGetValue(tex, out matIdx)) { /* 复用 */ }
                    else
                    {
                        matIdx = matFiles.Count;
                        if (tex != null)
                        {
                            texIndex[tex] = matIdx;
                            string matFile = fileKey + "_m" + matIdx + ".png";
                            var readable = LayoutEditorCounterSkinIconExport.MakeReadable(tex, MaxMatTexSize);
                            if (readable != null)
                            {
                                try { File.WriteAllBytes(Path.Combine(outDir, matFile), readable.EncodeToPNG()); }
                                finally { UnityEngine.Object.DestroyImmediate(readable); }
                                matFiles.Add(matFile);
                            }
                            else matFiles.Add("");
                        }
                        else matFiles.Add("");
                        matTints.Add(per[s].tint);
                        mtl.Append("newmtl ").Append(fileKey).Append("_m").Append(matIdx).Append('\n')
                           .Append("Kd ").Append(Fmt(per[s].tint.r)).Append(' ').Append(Fmt(per[s].tint.g)).Append(' ').Append(Fmt(per[s].tint.b)).Append('\n')
                           .Append(matFiles[matIdx].Length > 0 ? ("map_Kd " + matFiles[matIdx] + "\n") : "")
                           .Append('\n');
                    }
                    obj.Append("g ").Append(r.name).Append('_').Append(s).Append('\n')
                       .Append("usemtl ").Append(fileKey).Append("_m").Append(matIdx).Append('\n');
                    int[] tris = mesh.GetTriangles(s);
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        // x 镜像 → 绕序反转 (c,b,a)，索引随 vBase 平移；有 UV 时 v/vt 同号（dump 惯例）
                        int a = tris[i] + 1 + vBase, b = tris[i + 1] + 1 + vBase, c = tris[i + 2] + 1 + vBase;
                        if (hasUv)
                            obj.Append("f ").Append(c).Append('/').Append(c).Append(' ')
                               .Append(b).Append('/').Append(b).Append(' ')
                               .Append(a).Append('/').Append(a).Append('\n');
                        else
                            obj.Append("f ").Append(c).Append(' ').Append(b).Append(' ').Append(a).Append('\n');
                    }
                }
                vBase += vCount;
            }
            File.WriteAllText(Path.Combine(outDir, fileKey + ".obj"), obj.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outDir, fileKey + ".mtl"), mtl.ToString(), new UTF8Encoding(false));
            row["obj"] = fileKey + ".obj";
            row["mtl"] = fileKey + ".mtl";
            var matsWithTex = new StringBuilder();
            for (int i = 0; i < matFiles.Count; i++)
                matsWithTex.Append(matsWithTex.Length > 0 ? "," : "").Append(matFiles[i]);
            row["mats"] = matsWithTex.ToString();
            row["sizeX"] = Fmt(bounds.size.x);
            row["sizeY"] = Fmt(bounds.size.y);
            row["sizeZ"] = Fmt(bounds.size.z);
            row["minY"] = Fmt(bounds.min.y - offset.y);
        }
        catch (Exception ex)
        {
            row["error"] = ex.GetType().Name + ": " + ex.Message;
        }
        finally
        {
            if (rt != null) { RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(rt); }
            if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
            if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
        }
        return row;
    }

    private static Color ApplyTint(SkinMatInfo info)
    {
        // 无贴图槽：直接用 tint 色（白 tint 会不可见 → 用中灰兜底）；有贴图：贴图 × tint
        if (info.tex == null)
        {
            Color c = info.tint;
            if (c.r > 0.92f && c.g > 0.92f && c.b > 0.92f) return new Color(0.75f, 0.75f, 0.75f, 1f);
            return new Color(c.r, c.g, c.b, 1f);
        }
        Color t = info.tint;
        return new Color(Mathf.Clamp01(t.r), Mathf.Clamp01(t.g), Mathf.Clamp01(t.b), 1f);
    }

    private static Mesh MeshOf(Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return mf.sharedMesh;
        var smr = r as SkinnedMeshRenderer;
        if (smr != null && smr.sharedMesh != null) return smr.sharedMesh;
        return null;
    }

    private static int SubMeshCountOf(Renderer r)
    {
        Mesh m = MeshOf(r);
        return m != null ? m.subMeshCount : 0;
    }

    private static string Fmt(float f)
    {
        if (float.IsNaN(f) || float.IsInfinity(f)) return "0";
        return f.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void WriteManifest(string path, List<Dictionary<string, string>> items)
    {
        var sb = new StringBuilder();
        sb.Append("{\n  \"generatedAt\": \"").Append(DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss")).Append("\",\n");
        sb.Append("  \"comment\": \"Counter skin top-view renders + flattened OBJ/MTL, exported by Unity menu '导出桌台皮肤 3D 模型与俯视图'. Served read-only via /api/counter-skins/3d/<name>. fileKey = <Type>_<Theme|Default> matches counter-appearances.json icon field.\",\n");
        sb.Append("  \"items\": {\n");
        int n = 0;
        foreach (var r in items)
            if (string.IsNullOrEmpty(r["error"])) n++;
        int emitted = 0;
        foreach (var r in items)
        {
            if (!string.IsNullOrEmpty(r["error"])) continue;
            var mats = new StringBuilder();
            string[] files = r["mats"].Length > 0 ? r["mats"].Split(',') : new string[0];
            for (int i = 0; i < files.Length; i++)
            {
                if (mats.Length > 0) mats.Append(",");
                mats.Append("{ \"tex\": \"").Append(files[i]).Append("\" }");
            }
            sb.Append("    \"").Append(r["fileKey"]).Append("\": { \"top\": \"").Append(r["top"])
              .Append("\", \"obj\": \"").Append(r["obj"])
              .Append("\", \"mtl\": \"").Append(r["mtl"])
              .Append("\", \"mats\": [").Append(mats)
              .Append("], \"size\": { \"x\": ").Append(r["sizeX"])
              .Append(", \"y\": ").Append(r["sizeY"])
              .Append(", \"z\": ").Append(r["sizeZ"])
              .Append(" }, \"minY\": ").Append(r["minY"]).Append(" }");
            emitted++;
            if (emitted < n) sb.Append(",");
            sb.Append("\n");
        }
        sb.Append("  },\n  \"failures\": {\n");
        int f = 0, fTotal = 0;
        foreach (var r in items)
            if (!string.IsNullOrEmpty(r["error"])) fTotal++;
        foreach (var r in items)
        {
            if (string.IsNullOrEmpty(r["error"])) continue;
            sb.Append("    \"").Append(r["fileKey"]).Append("\": \"").Append(r["error"].Replace("\"", "'")).Append("\"");
            f++;
            if (f < fTotal) sb.Append(",");
            sb.Append("\n");
        }
        sb.Append("  }\n}\n");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }
}
