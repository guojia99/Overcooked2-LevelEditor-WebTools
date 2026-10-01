using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using LevelEditorStub;

/// <summary>
/// 桌台皮肤（counterAppearance）材质球提取工具：
/// 遍历 common01/02/03 + commonW1 各 pseudo_prefab_so/counters 下的 PseudoPrefabSO，
/// 按 bundleName 从 StreamingAssets/Windows 加载真实 prefab，取渲染器材质球主贴图，
/// 产出两份资产供 web 前端展示皮肤：
///   ① web/public/icons/counter-skins/&lt;Type&gt;_&lt;Theme|Default&gt;.png —— 128×128 中心裁切缩略图
///   ② layout-editor/scripts/data/counter-skin-icons.json —— guid → { color, file }（build-catalog.mjs 合并进
///     counter-appearances.json 的 color/icon 字段；键 = SO .meta guid，与扫描器同一权威）
/// 主色 = 贴图均色（alpha&lt;0.5 像素跳过，全透明则不跳）；默认皮肤画布回退现行配色，无需单独条目。
/// 幂等可重跑（全量覆盖）；菜单：Layout Editor → 素材导出 (Asset Export) → 导出桌台皮肤图标与主色。
/// </summary>
public static class LayoutEditorCounterSkinIconExport
{
    private const int IconSize = 128;

    // 与 build-catalog.mjs COUNTER_TYPE_NAMES_ZH 同源（仅键；最长前缀匹配）
    internal static readonly string[] CounterTypes = new string[]
    {
        "ConveyorStation", "ChoppingCounter", "CounterCorner", "FryingStation",
        "ServingStation", "PlateReturn", "Dispenser", "Counter", "Sink",
        "Bin", "Cooker", "Oven", "Mixer",
    };

    [MenuItem("Layout Editor/素材导出 (Asset Export)/导出桌台皮肤图标与主色 (Export Skin Icons)", false, 0)]
    public static void ExportAll()
    {
        Array.Sort(CounterTypes, (a, b) => b.Length.CompareTo(a.Length)); // 最长前缀优先
        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string iconDir = Path.Combine(repoRoot, "layout-editor/web/public/icons/counter-skins").Replace("\\", "/");
        string sidecarPath = Path.Combine(repoRoot, "layout-editor/scripts/data/counter-skin-icons.json").Replace("\\", "/");
        Directory.CreateDirectory(iconDir);

        List<string> soFiles = CollectCounterSoFiles();
        if (soFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("桌台皮肤提取", "未找到任何 counters 伪预制件 SO（pseudo_prefab_so/counters）", "确定");
            return;
        }

        // own = 本工具 LoadFromFile 的（可 Unload）；借自 PseudoPrefabManager 的已加载 bundle 绝不能卸
        var own = new List<AssetBundle>();
        var bundles = new Dictionary<string, AssetBundle>();
        var results = new List<Dictionary<string, string>>(); // 每条: guid/id/type/theme/color/file/error
        // 依赖清单：自行加载的 bundle 需按 manifest 递归拉起依赖（材质/贴图常在依赖包）
        AssetBundleManifest manifest = LoadManifest(bundles, own);
        if (manifest == null)
            Debug.LogWarning("[LayoutEditor/Export] Windows manifest 加载失败：自加载 bundle 缺依赖时对应皮肤可能失败");
        int done = 0, written = 0;
        try
        {
            for (int i = 0; i < soFiles.Count; i++)
            {
                string file = soFiles[i];
                EditorUtility.DisplayProgressBar("桌台皮肤提取", file, (float)i / soFiles.Count);
                var row = ExtractOne(repoRoot, iconDir, bundles, own, manifest, file);
                if (row != null)
                {
                    results.Add(row);
                    if (string.IsNullOrEmpty(row["error"])) written++;
                }
                done++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            foreach (AssetBundle b in own) b.Unload(true);
            bundles.Clear();
        }

        WriteSidecar(sidecarPath, results);

        int failed = 0;
        var sb = new StringBuilder();
        foreach (var r in results)
        {
            if (!string.IsNullOrEmpty(r["error"]))
            {
                failed++;
                if (sb.Length < 1500) sb.Append("\n").Append(r["id"]).Append(": ").Append(r["error"]);
            }
        }
        string report = "共 " + done + " 个皮肤 SO，成功 " + written + "，失败 " + failed +
            "\n图标 → layout-editor/web/public/icons/counter-skins/" +
            "\n主色 → layout-editor/scripts/data/counter-skin-icons.json（重跑 build-catalog.mjs 合并进 counter-appearances.json）";
        if (sb.Length > 0) report += "\n失败明细（前若干条）:" + sb;
        Debug.Log("[LayoutEditor/Export] " + report.Replace("\n", " | "));
        EditorUtility.DisplayDialog("桌台皮肤提取", report, "确定");
    }

    internal static List<string> CollectCounterSoFiles()
    {
        var files = new List<string>();
        string dataPath = Application.dataPath;
        var roots = new List<string> { "common01/pseudo_prefab_so/counters", "common02/pseudo_prefab_so/counters", "common03/pseudo_prefab_so/counters" };
        string w1Root = Path.Combine(dataPath, "commonW1/pseudo_prefab_so").Replace("\\", "/");
        if (Directory.Exists(w1Root))
        {
            foreach (string dir in Directory.GetDirectories(w1Root))
            {
                string counters = Path.Combine(dir, "counters").Replace("\\", "/");
                if (Directory.Exists(counters)) roots.Add("commonW1/pseudo_prefab_so/" + Path.GetFileName(dir) + "/counters");
            }
        }
        foreach (string rel in roots)
        {
            string abs = Path.Combine(dataPath, rel).Replace("\\", "/");
            if (!Directory.Exists(abs)) continue;
            foreach (string f in Directory.GetFiles(abs, "*.asset"))
            {
                if (f.EndsWith(".meta")) continue; // "*.asset" glob 在 Mono 下会误匹配 .asset.meta
                files.Add("Assets/" + rel + "/" + Path.GetFileName(f));
            }
        }
        return files;
    }

    /// <summary>
    /// 取 bundle：优先复用全局已加载（PseudoPrefabManager 等持有；Unity 2017 对同名 bundle
    /// 重复 LoadFromFile 返回 null，且借来的绝不能 Unload）→ 否则自行加载并按 manifest
    /// 递归拉起依赖（自行加载的一律记入 own，结束时统一 Unload）。
    /// </summary>
    internal static AssetBundle EnsureBundleLoaded(string name, Dictionary<string, AssetBundle> bundles, List<AssetBundle> own, AssetBundleManifest manifest)
    {
        AssetBundle bundle;
        if (bundles.TryGetValue(name, out bundle) && bundle != null) return bundle;
        foreach (AssetBundle loaded in AssetBundle.GetAllLoadedAssetBundles())
        {
            if (loaded != null && loaded.name == name)
            {
                bundles[name] = loaded;
                return loaded;
            }
        }
        string bundlePath = Path.Combine(Application.streamingAssetsPath, "Windows/" + name).Replace("\\", "/");
        if (!File.Exists(bundlePath)) return null;
        bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null) return null;
        bundles[name] = bundle;
        own.Add(bundle);
        if (manifest != null)
        {
            string[] deps = manifest.GetAllDependencies(name);
            foreach (string dep in deps) EnsureBundleLoaded(dep, bundles, own, manifest);
        }
        return bundle;
    }

    internal static AssetBundleManifest LoadManifest(Dictionary<string, AssetBundle> bundles, List<AssetBundle> own)
    {
        AssetBundle mb = EnsureBundleLoaded("Windows", bundles, own, null);
        if (mb == null) return null;
        return mb.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
    }

    /// <summary>提取一个 SO。返回结果行（error 空 = 成功）；SO 不是皮肤/无类型时返回 null。</summary>
    private static Dictionary<string, string> ExtractOne(string repoRoot, string iconDir, Dictionary<string, AssetBundle> bundles, List<AssetBundle> own, AssetBundleManifest manifest, string assetPath)
    {
        var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(assetPath) as PseudoPrefabSO;
        if (so == null) return null;
        string id = Path.GetFileNameWithoutExtension(assetPath);

        string type = null;
        foreach (string ct in CounterTypes)
        {
            if (id.StartsWith(ct)) { type = ct; break; } // 列表已按长度降序排列（最长前缀优先）
        }
        if (type == null) return null;

        string theme = id.Substring(type.Length);
        if (theme.EndsWith("SO")) theme = theme.Substring(0, theme.Length - 2);
        string fileKey = type + "_" + (string.IsNullOrEmpty(theme) ? "Default" : theme);

        var row = new Dictionary<string, string>();
        row["guid"] = AssetDatabase.AssetPathToGUID(assetPath);
        row["id"] = id;
        row["type"] = type;
        row["theme"] = theme;
        row["file"] = fileKey + ".png";
        row["color"] = "";
        row["error"] = "";

        try
        {
            AssetBundle bundle = EnsureBundleLoaded(so.bundleName, bundles, own, manifest);
            if (bundle == null) { row["error"] = "bundle 加载失败(含依赖): " + so.bundleName; return row; }

            var prefab = bundle.LoadAsset<GameObject>(so.assetPath);
            if (prefab == null) { row["error"] = "prefab 加载失败: " + so.assetPath; return row; }

            Material mat;
            Texture2D tex = FindDiffuseTexture(prefab, out mat);
            if (tex == null) { row["error"] = "无 diffuse 贴图（渲染器/材质槽全空）"; return row; }

            // bundle 贴图多半 isReadable=false → RenderTexture 拷贝为可读（等比缩到 ≤128，省内存且均色几乎不变）
            Texture2D small = MakeReadable(tex, IconSize);
            if (small == null) { row["error"] = "贴图可读化失败"; return row; }
            try
            {
                Color avg = AverageColor(small);
                // 材质 _Color tint（RedSlim/BlueSlim 等皮肤靠 tint 上色，diffuse 本体是灰图）
                if (mat != null && mat.HasProperty("_Color"))
                {
                    Color tint = mat.GetColor("_Color");
                    avg = new Color(avg.r * tint.r, avg.g * tint.g, avg.b * tint.b, 1f);
                }
                row["color"] = ColorToHex(avg);
                // 桌台贴图是 UV atlas（台面/边沿/桌腿/装饰拼图），不能中心裁切——整图等比居中到 128×128 透明方图
                Texture2D icon = PadToSquare(small, IconSize);
                try { File.WriteAllBytes(Path.Combine(iconDir, fileKey + ".png"), icon.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(icon); }
            }
            finally { UnityEngine.Object.DestroyImmediate(small); }
        }
        catch (Exception ex)
        {
            row["error"] = ex.GetType().Name + ": " + ex.Message;
        }
        return row;
    }

    internal static readonly string[] TexSlotPriority = new string[] { "_MainTex", "_DiffuseMap", "_Diffuse", "_Albedo", "_DiffuseTex" };

    /// <summary>单材质选 diffuse 贴图（槽名优先级 + shader 属性枚举兜底），并带出 _Color tint。
    ///  IconExport（均色）与 3dExport（贴图 PNG/俯拍替换材质）共用，槽名选择单一来源。</summary>
    internal static Texture2D DiffuseOfMaterial(Material m, out Color tint)
    {
        tint = Color.white;
        if (m == null || m.shader == null) return null;
        if (m.HasProperty("_Color")) tint = m.GetColor("_Color");
        foreach (string slot in TexSlotPriority)
        {
            if (!m.HasProperty(slot)) continue;
            var t = m.GetTexture(slot) as Texture2D;
            if (t != null) return t;
        }
        string[] names = null;
        try
        {
            // Unity 2017 无 Material.GetTexturePropertyNames（2019.1+），用 ShaderUtil 枚举纹理槽
            int propCount = ShaderUtil.GetPropertyCount(m.shader);
            var slots = new List<string>();
            for (int pi = 0; pi < propCount; pi++)
            {
                if (ShaderUtil.GetPropertyType(m.shader, pi) == ShaderUtil.ShaderPropertyType.TexEnv)
                    slots.Add(ShaderUtil.GetPropertyName(m.shader, pi));
            }
            names = slots.ToArray();
        }
        catch (Exception) { names = null; }
        if (names == null) return null;
        string best = null;
        foreach (string n in names)
        {
            if (m.GetTexture(n) == null) continue;
            string ln = n.ToLowerInvariant();
            if (ln.EndsWith("_n") || ln.Contains("normal") || ln.EndsWith("_ao") || ln.Contains("rmeao") ||
                ln.EndsWith("_s") || ln.Contains("spec") || ln.Contains("mask") || ln.Contains("detail") ||
                ln.Contains("lightmap") || ln.Contains("emiss")) continue;
            if (best == null) best = n;
            if (ln.Contains("diff") || ln.Contains("albedo") || ln.Contains("col")) { best = n; break; }
        }
        if (best == null) return null;
        return m.GetTexture(best) as Texture2D;
    }

    /// <summary>
    /// 取 prefab 内首个可用 diffuse 贴图（含 inactive；遍历所有渲染器 × 全部材质槽 × shader 纹理槽）。
    /// Material.mainTexture 只认 _MainTex，游戏桌台 shader 主槽多为 _DiffuseMap，故按槽名优先级
    /// + shader 属性枚举兜底（规避法线/AO/高光/遮罩/自发光槽）。out mat 供主色乘材质 tint。
    /// </summary>
    internal static Texture2D FindDiffuseTexture(GameObject prefab, out Material outMat)
    {
        outMat = null;
        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers == null) return null;
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            var mats = r.sharedMaterials; // 材质槽数组：桌台常是多槽（台面/侧面）
            if (mats == null) continue;
            foreach (Material m in mats)
            {
                if (m == null || m.shader == null) continue;
                Color tint;
                var t = DiffuseOfMaterial(m, out tint);
                if (t != null) { outMat = m; return t; }
            }
        }
        return null;
    }

    /// <summary>GPU 拷贝为可读 ARGB32 纹理（maxSide>0 时等比缩到不超过该边长，节省内存）。</summary>
    internal static Texture2D MakeReadable(Texture2D src, int maxSide)
    {
        int w = src.width, h = src.height;
        if (maxSide > 0 && Math.Max(w, h) > maxSide)
        {
            float scale = (float)maxSide / Math.Max(w, h);
            w = Math.Max(1, Mathf.RoundToInt(w * scale));
            h = Math.Max(1, Mathf.RoundToInt(h * scale));
        }
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var readable = new Texture2D(w, h, TextureFormat.ARGB32, false, false);
        readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        readable.Apply(false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return readable;
    }

    private static Color AverageColor(Texture2D tex)
    {
        Color[] px = tex.GetPixels();
        double r = 0, g = 0, b = 0; int n = 0, opaque = 0;
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a < 0.5f) continue;
            r += px[i].r; g += px[i].g; b += px[i].b; opaque++;
        }
        if (opaque == 0) // 全透明贴图：不跳 alpha
        {
            for (int i = 0; i < px.Length; i++) { r += px[i].r; g += px[i].g; b += px[i].b; }
            n = px.Length;
        }
        else n = opaque;
        return n == 0 ? Color.gray : new Color((float)(r / n), (float)(g / n), (float)(b / n), 1f);
    }

    /// <summary>整图居中放入 size×size 透明方图（桌台贴图是 UV atlas，不能裁切）。</summary>
    private static Texture2D PadToSquare(Texture2D src, int size)
    {
        var dst = new Texture2D(size, size, TextureFormat.ARGB32, false, false);
        var outPx = new Color[size * size]; // 默认全透明
        Color[] px = src.GetPixels();
        int w = src.width, h = src.height;
        int ox = (size - w) / 2, oy = (size - h) / 2;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
                outPx[(y + oy) * size + (x + ox)] = px[y * w + x];
        }
        dst.SetPixels(outPx);
        dst.Apply(false);
        return dst;
    }

    private static string ColorToHex(Color c)
    {
        var c32 = (Color32)c;
        return string.Format("#{0:x2}{1:x2}{2:x2}", c32.r, c32.g, c32.b);
    }

    private static void WriteSidecar(string path, List<Dictionary<string, string>> results)
    {
        var sb = new StringBuilder();
        sb.Append("{\n  \"schemaVersion\": 1,\n");
        sb.Append("  \"comment\": \"Counter skin material colors + thumbnail files, extracted from game bundles by Layout Editor menu '导出桌台皮肤图标与主色'. Keys = PseudoPrefabSO .meta guid. Consumed by build-catalog.mjs scanCounterAppearances -> counter-appearances.json color/icon fields. Regenerate via Unity menu, then re-run build-catalog + npm build.\",\n");
        sb.Append("  \"items\": {\n");
        int n = 0;
        foreach (var r in results)
        {
            if (string.IsNullOrEmpty(r["error"])) n++;
        }
        int emitted = 0;
        foreach (var r in results)
        {
            if (!string.IsNullOrEmpty(r["error"])) continue;
            sb.Append("    \"").Append(r["guid"]).Append("\": { \"color\": \"").Append(r["color"])
              .Append("\", \"file\": \"").Append(r["file"]).Append("\", \"id\": \"").Append(r["id"]).Append("\" }");
            emitted++;
            if (emitted < n) sb.Append(",");
            sb.Append("\n");
        }
        sb.Append("  },\n");
        // 失败明细（诊断用；build-catalog 忽略该段）
        sb.Append("  \"failures\": {\n");
        int f = 0, fTotal = 0;
        foreach (var r in results)
        {
            if (string.IsNullOrEmpty(r["error"])) continue;
            fTotal++;
        }
        foreach (var r in results)
        {
            if (string.IsNullOrEmpty(r["error"])) continue;
            sb.Append("    \"").Append(r["id"]).Append("\": \"").Append(r["error"].Replace("\"", "'")).Append("\"");
            f++;
            if (f < fTotal) sb.Append(",");
            sb.Append("\n");
        }
        sb.Append("  }\n}\n");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }
}
