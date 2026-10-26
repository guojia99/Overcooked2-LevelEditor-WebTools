using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 装饰层缩略图导出器（左侧调色板大图预览数据源）。
///
/// 与 JiaMap 的缩略图导出思路一致：pseudo prefab 需要先从
/// Assets/StreamingAssets/Windows 的 bundle 中物化真实网格，再用 45° 正交相机
/// 渲染成 PNG。本文件不依赖 JiaMap 程序集，保持 LayoutEditor 独立。
///
/// 输出：layout-editor/web/public/icons/decor-preview/&lt;guid&gt;.png
/// 命名按 guid（catalog id 可能重名，guid 唯一），前端由 manifest 查表。
///
/// 铁律：batchmode 导出时禁加 -nographics（RenderTexture 读回会全灰）。
/// </summary>
public static class LayoutEditorDecorThumbnailExporter
{
    private const int ThumbSize = 192;
    private const string OutSubDir = "icons/decor-preview";
    private const string MenuRoot = "Layout Editor/素材导出 (Asset Export)/";

    private static string OutDir
    {
        get { return Path.Combine(LayoutEditorPaths.WebPublicRoot, OutSubDir); }
    }

    /// <summary>排障用：设置环境变量 LAYOUT_DECOR_DEBUG_ID=某个 catalog id 时打印层级/材质/像素统计。</summary>
    private static string DebugItemId
    {
        get { return Environment.GetEnvironmentVariable("LAYOUT_DECOR_DEBUG_ID") ?? ""; }
    }

    [MenuItem(MenuRoot + "导出装饰物缩略图 (Export Decor Thumbnails)", false, 2)]
    public static void ExportMenu()
    {
        Export(false);
    }

    [MenuItem(MenuRoot + "强制重导装饰物缩略图 (Re-export Decor Thumbnails)", false, 3)]
    public static void ExportAllMenu()
    {
        Export(true);
    }

    public static void Export(bool force)
    {
        string publicRoot = LayoutEditorPaths.WebPublicRoot;
        string catalogPath = Path.Combine(publicRoot, "catalog.json");
        if (!File.Exists(catalogPath))
        {
            Debug.LogError("[DecorThumb] 缺少 catalog.json（先运行 layout-editor/scripts/build-catalog.mjs）: " + catalogPath);
            return;
        }

        List<CatalogItemDto> items;
        try
        {
            items = LoadDecorItems(publicRoot, catalogPath);
        }
        catch (Exception ex)
        {
            Debug.LogError("[DecorThumb] 读取 catalog 失败: " + ex.Message);
            return;
        }
        if (items.Count == 0)
        {
            Debug.LogWarning("[DecorThumb] catalog 中没有 layoutTier=decor 条目");
            return;
        }

        Directory.CreateDirectory(OutDir);
        var loader = new BundleLoader();
        var tempRoot = new GameObject("__LayoutDecorThumbRoot");
        tempRoot.transform.position = new Vector3(7000f, 0f, 7000f);
        var camGo = new GameObject("__LayoutDecorThumbCam");
        camGo.transform.SetParent(tempRoot.transform);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.orthographic = true;
        cam.allowHDR = false;
        cam.allowMSAA = false;

        var lightGo = new GameObject("__LayoutDecorThumbLight");
        lightGo.transform.SetParent(tempRoot.transform);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var rt = new RenderTexture(ThumbSize, ThumbSize, 24, RenderTextureFormat.ARGB32);
        rt.name = "__LayoutDecorThumbRT";
        int rendered = 0;
        int skipped = 0;
        int missing = 0;
        int failed = 0;
        var missingLog = new List<string>();
        bool cancelled = false;

        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                        "导出装饰物缩略图",
                        string.Format("{0} ({1}/{2})", item.id, i + 1, items.Count),
                        (float)i / items.Count))
                {
                    cancelled = true;
                    break;
                }

                string pngPath = Path.Combine(OutDir, item.guid + ".png");
                if (!force && File.Exists(pngPath))
                {
                    skipped++;
                    continue;
                }
                if (force && File.Exists(pngPath))
                {
                    try { File.Delete(pngPath); }
                    catch (Exception ex) { Debug.LogWarning("[DecorThumb] 删除旧图失败 " + pngPath + ": " + ex.Message); }
                }

                GameObject inst = null;
                List<Material> patchedMats = null;
                try
                {
                    inst = SpawnAndMaterialize(item, loader);
                    if (inst == null)
                    {
                        missing++;
                        missingLog.Add(item.id + "（prefab 或 bundle 资源缺失）");
                        continue;
                    }

                    inst.transform.SetParent(tempRoot.transform, false);
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localRotation = Quaternion.identity;
                    inst.transform.localScale = Vector3.one;
                    ActivateRenderableHierarchy(inst);
                    SimulateParticles(inst);

                    if (!HasRenderableVisual(inst))
                    {
                        missing++;
                        missingLog.Add(item.id + "（无网格/精灵/粒子）");
                        continue;
                    }

                    LayoutEditorThumbMaterialFix.DebugItemId = item.id == DebugItemId ? item.id : "";
                    patchedMats = LayoutEditorThumbMaterialFix.FixRenderers(inst, FallbackColorFor(item));
                    if (item.id == DebugItemId)
                        LogHierarchy(inst);
                    RenderItem(inst, cam, rt, pngPath, item.id);
                    rendered++;
                }
                catch (Exception ex)
                {
                    failed++;
                    missingLog.Add(item.id + "（渲染异常: " + ex.Message + "）");
                    Debug.LogWarning("[DecorThumb] " + item.id + " 渲染失败: " + ex.Message);
                }
                finally
                {
                    if (cam != null)
                        cam.targetTexture = null;
                    RenderTexture.active = null;
                    if (patchedMats != null)
                    {
                        for (int k = 0; k < patchedMats.Count; k++)
                        {
                            if (patchedMats[k] != null)
                                UnityEngine.Object.DestroyImmediate(patchedMats[k]);
                        }
                    }
                    if (inst != null)
                        UnityEngine.Object.DestroyImmediate(inst);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            RenderTexture.active = null;
            if (cam != null)
                cam.targetTexture = null;
            if (rt != null)
            {
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
            if (tempRoot != null)
                UnityEngine.Object.DestroyImmediate(tempRoot);
            loader.UnloadAll();
        }

        Debug.Log(string.Format(
            "[DecorThumb] {0}：完成 {1} / 跳过已存在 {2} / 缺失 {3} / 失败 {4}（共 {5}）→ {6}",
            cancelled ? "已取消" : "完成",
            rendered, skipped, missing, failed, items.Count, OutDir));
        if (missingLog.Count > 0)
        {
            int print = Mathf.Min(missingLog.Count, 120);
            var sb = new StringBuilder();
            sb.AppendLine("[DecorThumb] 缺失/失败清单（前 " + print + " 条，共 " + missingLog.Count + "）：");
            for (int i = 0; i < print; i++)
                sb.AppendLine("  " + missingLog[i]);
            Debug.LogWarning(sb.ToString());
        }
    }

    // ------------------------------------------------------------------ catalog

    [Serializable]
    private sealed class CatalogIndexDto
    {
        public string[] itemChunks;
    }

    [Serializable]
    private sealed class CatalogChunkDto
    {
        public CatalogItemDto[] items;
    }

    [Serializable]
    private sealed class CatalogItemDto
    {
        public string id;
        public string guid;
        public string assetPath;
        public string layoutTier;
        public string category;
        public string theme;
        public string typeKind;
        public string usage;
    }

    private static List<CatalogItemDto> LoadDecorItems(string publicRoot, string catalogPath)
    {
        var result = new List<CatalogItemDto>();
        var seenGuid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = JsonUtility.FromJson<CatalogIndexDto>(File.ReadAllText(catalogPath));
        if (index == null || index.itemChunks == null)
            return result;

        for (int c = 0; c < index.itemChunks.Length; c++)
        {
            string chunkPath = Path.Combine(publicRoot, index.itemChunks[c].Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(chunkPath))
                continue;
            var chunk = JsonUtility.FromJson<CatalogChunkDto>(File.ReadAllText(chunkPath));
            if (chunk == null || chunk.items == null)
                continue;
            for (int i = 0; i < chunk.items.Length; i++)
            {
                var it = chunk.items[i];
                if (it == null || it.layoutTier != "decor")
                    continue;
                if (string.IsNullOrEmpty(it.guid))
                    continue;
                if (!seenGuid.Add(it.guid))
                    continue;
                result.Add(it);
            }
        }
        result.Sort(delegate (CatalogItemDto a, CatalogItemDto b)
        {
            return string.CompareOrdinal(a.id, b.id);
        });
        return result;
    }

    // ------------------------------------------------------------- bundle loading

    private sealed class BundleLoader
    {
        private readonly Dictionary<string, AssetBundle> bundles = new Dictionary<string, AssetBundle>();
        private readonly HashSet<string> borrowed = new HashSet<string>();
        private AssetBundleManifest manifest;
        private bool manifestTried;

        public GameObject LoadPrefab(PseudoPrefabSO so)
        {
            if (so == null)
                return null;
            if (so.asset != null)
            {
                var direct = so.asset as GameObject;
                if (direct != null)
                    return direct;
            }

            string assetPath = string.IsNullOrEmpty(so.assetPath) ? "" : so.assetPath.Replace('\\', '/');
            if (!string.IsNullOrEmpty(so.bundleName) && !string.IsNullOrEmpty(assetPath))
            {
                var bundle = LoadBundleWithDeps(so.bundleName);
                if (bundle != null)
                {
                    var prefab = bundle.LoadAsset<GameObject>(assetPath);
                    if (prefab != null)
                        return prefab;
                }
            }

            // 兜底：部分 commonW1 包装 SO 直接引用工程内 prefab。
            if (!string.IsNullOrEmpty(assetPath))
            {
                var local = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (local != null)
                    return local;
            }
            return null;
        }

        private AssetBundle LoadBundleWithDeps(string name)
        {
            AssetBundle bundle;
            if (bundles.TryGetValue(name, out bundle) && bundle != null)
                return bundle;

            bundle = LoadBundleInternal(name);
            if (bundle == null)
                return null;

            EnsureManifest();
            if (manifest != null)
            {
                string[] deps = manifest.GetAllDependencies(name);
                int loaded = 0;
                var missingDeps = new List<string>();
                for (int i = 0; i < deps.Length; i++)
                {
                    AssetBundle dep;
                    if (!bundles.TryGetValue(deps[i], out dep) || dep == null)
                        dep = LoadBundleInternal(deps[i]);
                    if (dep != null) loaded++;
                    else missingDeps.Add(deps[i]);
                }
                if (missingDeps.Count > 0 && string.Equals(name, "bundle47", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning("[DecorThumb] bundle47 依赖缺失 " + missingDeps.Count + " 个："
                        + string.Join(",", missingDeps.ToArray()));
                }
            }
            return bundle;
        }

        private AssetBundle LoadBundleInternal(string name)
        {
            foreach (var b in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (b != null && string.Equals(b.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    bundles[name] = b;
                    borrowed.Add(name);
                    return b;
                }
            }

            string path = Path.Combine(Application.streamingAssetsPath, "Windows/" + name).Replace("\\", "/");
            if (!File.Exists(path))
                return null;
            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle != null)
                bundles[name] = bundle;
            return bundle;
        }

        private void EnsureManifest()
        {
            if (manifestTried)
                return;
            manifestTried = true;
            // 游戏原版 manifest 文件是 StreamingAssets/Windows/Windows。
            var manifestBundle = LoadBundleInternal("Windows");
            if (manifestBundle == null)
            {
                Debug.LogWarning("[DecorThumb] 未找到 StreamingAssets/Windows/Windows manifest bundle");
                return;
            }
            manifest = manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            if (manifest == null)
                Debug.LogWarning("[DecorThumb] manifest bundle 内未找到 AssetBundleManifest 资产");
        }

        public void UnloadAll()
        {
            foreach (var kv in bundles)
            {
                if (kv.Value != null && !borrowed.Contains(kv.Key))
                    kv.Value.Unload(false);
            }
            bundles.Clear();
            borrowed.Clear();
            manifest = null;
        }
    }

    // --------------------------------------------------------------- pseudo prefab

    private static GameObject SpawnAndMaterialize(CatalogItemDto item, BundleLoader loader)
    {
        string path = item.assetPath;
        if (!string.IsNullOrEmpty(item.guid))
        {
            string guidPath = AssetDatabase.GUIDToAssetPath(item.guid);
            if (!string.IsNullOrEmpty(guidPath))
                path = guidPath;
        }
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            return null;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            return null;

        var go = UnityEngine.Object.Instantiate(prefab) as GameObject;
        if (go == null)
            return null;
        go.name = item.id;

        try
        {
            // PseudoPrefab 是 [ExecuteInEditMode]；batchmode 下避免其 Start 触发
            // PseudoPrefabManager.Instance NRE。
            DisablePseudoPrefabComponents(go);
            MaterializePseudoRecursive(go, loader);
            DisablePseudoPrefabComponents(go);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DecorThumb] " + item.id + " 物化异常（按残件渲染）: " + ex.Message);
        }
        return go;
    }

    private static void DisablePseudoPrefabComponents(GameObject root)
    {
        var pseudos = root.GetComponentsInChildren<LevelEditor.PseudoPrefab>(true);
        for (int i = 0; i < pseudos.Length; i++)
        {
            if (pseudos[i] != null)
                pseudos[i].enabled = false;
        }
    }

    private static void MaterializePseudoRecursive(GameObject root, BundleLoader loader)
    {
        var done = new HashSet<int>();
        for (int round = 0; round < 4; round++)
        {
            var stubs = root.GetComponentsInChildren<PseudoPrefabStub>(true);
            if (stubs == null || stubs.Length == 0)
                return;

            bool didWork = false;
            for (int i = 0; i < stubs.Length; i++)
            {
                var stub = stubs[i];
                if (stub == null)
                    continue;
                int instanceId = stub.GetInstanceID();
                if (!done.Add(instanceId))
                    continue;
                var so = stub.pseudoPrefabSO;
                if (so == null)
                    continue;
                if (HasMaterializedChild(stub.transform, so.prefabName))
                    continue;
                var real = loader.LoadPrefab(so);
                if (real == null)
                    continue;
                var child = UnityEngine.Object.Instantiate(real, stub.transform.position, stub.transform.rotation, stub.transform) as GameObject;
                if (child == null)
                    continue;
                child.name = so.prefabName;
                child.transform.localScale = Vector3.one;
                didWork = true;
            }
            if (!didWork)
                return;
        }
    }

    private static bool HasMaterializedChild(Transform parent, string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
            return false;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child != null && child.name == prefabName)
                return true;
        }
        return false;
    }

    /** 无贴图白模/黑模的分类兜底色：优先保留主题特征（雪/海滩/太空），其次按 typeKind。 */
    private static Color FallbackColorFor(CatalogItemDto item)
    {
        string kind = (item.typeKind ?? "").ToLowerInvariant();
        string usage = (item.usage ?? "").ToLowerInvariant();
        string category = (item.category ?? "").ToLowerInvariant();
        string theme = (item.theme ?? "").ToLowerInvariant();
        string id = (item.id ?? "").ToLowerInvariant();
        string all = kind + " " + usage + " " + category + " " + theme + " " + id;

        if (theme.IndexOf("christmas", StringComparison.Ordinal) >= 0
            || theme.IndexOf("arctic", StringComparison.Ordinal) >= 0
            || theme.IndexOf("snow", StringComparison.Ordinal) >= 0
            || all.IndexOf("snow", StringComparison.Ordinal) >= 0)
            return new Color(0.84f, 0.90f, 0.96f);
        if (theme.IndexOf("space", StringComparison.Ordinal) >= 0
            || all.IndexOf("alien", StringComparison.Ordinal) >= 0)
            return new Color(0.56f, 0.48f, 0.78f);
        // 颜色变体（鹦鹉/道路标线/球等）优先按 id 里的颜色词着色。
        string idWords = " " + id.Replace('_', ' ').Replace('-', ' ') + " ";
        if (idWords.IndexOf(" blue ", StringComparison.Ordinal) >= 0)
            return new Color(0.34f, 0.56f, 0.92f);
        if (idWords.IndexOf(" red ", StringComparison.Ordinal) >= 0)
            return new Color(0.86f, 0.34f, 0.32f);
        if (idWords.IndexOf(" purple ", StringComparison.Ordinal) >= 0)
            return new Color(0.64f, 0.44f, 0.86f);
        if (idWords.IndexOf(" green ", StringComparison.Ordinal) >= 0)
            return new Color(0.38f, 0.68f, 0.38f);
        if (idWords.IndexOf(" yellow ", StringComparison.Ordinal) >= 0)
            return new Color(0.92f, 0.80f, 0.30f);
        if (idWords.IndexOf(" orange ", StringComparison.Ordinal) >= 0)
            return new Color(0.92f, 0.56f, 0.26f);
        if (idWords.IndexOf(" pink ", StringComparison.Ordinal) >= 0)
            return new Color(0.92f, 0.56f, 0.76f);
        if (idWords.IndexOf(" white ", StringComparison.Ordinal) >= 0)
            return new Color(0.90f, 0.90f, 0.90f);
        if (idWords.IndexOf(" black ", StringComparison.Ordinal) >= 0)
            return new Color(0.24f, 0.24f, 0.27f);

        if (theme.IndexOf("beach", StringComparison.Ordinal) >= 0
            || all.IndexOf("sand", StringComparison.Ordinal) >= 0)
            return new Color(0.78f, 0.70f, 0.52f);
        if (theme.IndexOf("graveyard", StringComparison.Ordinal) >= 0
            || theme.IndexOf("swamp", StringComparison.Ordinal) >= 0)
            return new Color(0.42f, 0.50f, 0.43f);

        if (kind == "water")
            return new Color(0.32f, 0.62f, 0.82f);
        if (kind == "vegetation")
            return new Color(0.38f, 0.64f, 0.36f);
        if (kind == "ground")
            return new Color(0.62f, 0.58f, 0.50f);
        if (kind == "rock")
            return new Color(0.56f, 0.57f, 0.59f);
        if (kind == "building")
            return new Color(0.72f, 0.66f, 0.56f);
        if (kind == "lighting")
            return new Color(0.95f, 0.76f, 0.36f);
        if (kind == "furniture")
            return new Color(0.68f, 0.50f, 0.34f);
        if (kind == "creature")
            return new Color(0.80f, 0.62f, 0.48f);
        if (kind == "fx" || kind == "ambience")
            return new Color(0.78f, 0.62f, 0.94f);

        if (all.IndexOf("water", StringComparison.Ordinal) >= 0
            || all.IndexOf("pool", StringComparison.Ordinal) >= 0
            || all.IndexOf("river", StringComparison.Ordinal) >= 0
            || all.IndexOf("ripple", StringComparison.Ordinal) >= 0)
            return new Color(0.32f, 0.62f, 0.82f);
        if (all.IndexOf("grass", StringComparison.Ordinal) >= 0
            || all.IndexOf("tree", StringComparison.Ordinal) >= 0
            || all.IndexOf("plant", StringComparison.Ordinal) >= 0
            || all.IndexOf("leaf", StringComparison.Ordinal) >= 0
            || all.IndexOf("bush", StringComparison.Ordinal) >= 0)
            return new Color(0.38f, 0.64f, 0.36f);
        if (all.IndexOf("path", StringComparison.Ordinal) >= 0
            || all.IndexOf("road", StringComparison.Ordinal) >= 0
            || all.IndexOf("pavement", StringComparison.Ordinal) >= 0
            || all.IndexOf("tile", StringComparison.Ordinal) >= 0
            || all.IndexOf("floor", StringComparison.Ordinal) >= 0)
            return new Color(0.62f, 0.58f, 0.50f);
        if (all.IndexOf("wall", StringComparison.Ordinal) >= 0
            || all.IndexOf("brick", StringComparison.Ordinal) >= 0
            || all.IndexOf("roof", StringComparison.Ordinal) >= 0
            || all.IndexOf("keep", StringComparison.Ordinal) >= 0)
            return new Color(0.72f, 0.66f, 0.56f);
        if (all.IndexOf("lantern", StringComparison.Ordinal) >= 0
            || all.IndexOf("lamp", StringComparison.Ordinal) >= 0
            || all.IndexOf("torch", StringComparison.Ordinal) >= 0)
            return new Color(0.95f, 0.76f, 0.36f);
        return new Color(0.56f, 0.62f, 0.72f);
    }

    // ------------------------------------------------------------------ rendering

    /// <summary>预览时强制启用被预制体默认关闭的可见子节点/渲染器（灯、灯笼、部分合成菜）。</summary>
    private static void ActivateRenderableHierarchy(GameObject root)
    {
        var transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t != null && !t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
        }
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = true;
        }
        DisablePseudoPrefabComponents(root);
    }

    private static void SimulateParticles(GameObject root)
    {
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null)
                continue;
            try { ps.Simulate(0.6f, true, true); }
            catch { }
        }
    }

    private static bool HasRenderableVisual(GameObject root)
    {
        var mfs = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < mfs.Length; i++)
        {
            if (mfs[i] != null && mfs[i].sharedMesh != null)
                return true;
        }
        var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < smrs.Length; i++)
        {
            if (smrs[i] != null && smrs[i].sharedMesh != null)
                return true;
        }
        var sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null && sprites[i].sprite != null)
                return true;
        }
        var particles = root.GetComponentsInChildren<ParticleSystem>(true);
        return particles != null && particles.Length > 0;
    }

    private static void LogHierarchy(GameObject root)
    {
        Debug.Log("[DecorThumb][Debug] root=" + root.name + " pos=" + root.transform.position);
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null)
                continue;
            var mats = r.sharedMaterials;
            string matDesc = "";
            for (int j = 0; j < mats.Length; j++)
            {
                var m = mats[j];
                matDesc += j == 0 ? "" : ", ";
                if (m == null)
                {
                    matDesc += "<null>";
                }
                else
                {
                    string texName = m.mainTexture == null ? "<null>" : m.mainTexture.name;
                    Color tint = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    matDesc += m.name + "[" + (m.shader == null ? "null" : m.shader.name) + "]"
                        + " tex=" + texName + " tint=" + tint;
                }
            }
            Debug.Log("[DecorThumb][Debug]   " + r.GetType().Name
                + " obj=" + r.gameObject.name
                + " active=" + r.gameObject.activeInHierarchy
                + " enabled=" + r.enabled
                + " bounds=" + r.bounds
                + " mats=" + matDesc);
        }
    }

    private static Bounds CalcBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasStatic = false;
        Bounds b = new Bounds(root.transform.position, Vector3.one);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || r is ParticleSystemRenderer)
                continue;
            if (!IsRendererUsable(r))
                continue;
            var rb = r.bounds;
            if (rb.size.sqrMagnitude < 1e-8f)
                continue;
            if (!hasStatic) { b = rb; hasStatic = true; }
            else b.Encapsulate(rb);
        }
        if (hasStatic)
            return b;

        // 纯粒子/特效：用粒子 renderer 的 bounds 取景。
        bool hasParticle = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || !(r is ParticleSystemRenderer))
                continue;
            var rb = r.bounds;
            if (rb.size.sqrMagnitude < 1e-8f)
                continue;
            if (!hasParticle) { b = rb; hasParticle = true; }
            else b.Encapsulate(rb);
        }
        return b;
    }

    private static bool IsRendererUsable(Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh == null)
            return false;
        var smr = r as SkinnedMeshRenderer;
        if (smr != null && smr.sharedMesh == null)
            return false;
        var sr = r as SpriteRenderer;
        if (sr != null && sr.sprite == null)
            return false;
        return true;
    }

    private static void RenderItem(GameObject inst, Camera cam, RenderTexture rt, string pngPath, string itemId)
    {
        Bounds b = CalcBounds(inst);
        float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z, 0.01f);
        cam.orthographicSize = maxDim * 0.75f;
        float dist = maxDim * 2.5f + 1f;
        cam.nearClipPlane = -maxDim * 3f - 10f;
        cam.farClipPlane = maxDim * 4f + 10f;
        Vector3 center = b.center;
        cam.transform.position = center + new Vector3(dist, dist * 0.8f, -dist);
        cam.transform.LookAt(center);
        if (itemId == DebugItemId)
            Debug.Log("[DecorThumb][Debug] bounds=" + b + " maxDim=" + maxDim + " camPos=" + cam.transform.position);

        RenderTexture.active = rt;
        cam.targetTexture = rt;
        cam.Render();

        var tex = new Texture2D(ThumbSize, ThumbSize, TextureFormat.ARGB32, false);
        try
        {
            tex.ReadPixels(new Rect(0, 0, ThumbSize, ThumbSize), 0, 0);
            tex.Apply();
            Color32[] pixels = tex.GetPixels32();
            if (itemId == DebugItemId)
            {
                int opaque = 0;
                int maxAlpha = 0;
                int magenta = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a > 20) opaque++;
                    if (pixels[i].a > maxAlpha) maxAlpha = pixels[i].a;
                    if (pixels[i].r > 235 && pixels[i].g < 20 && pixels[i].b > 235) magenta++;
                }
                Debug.Log("[DecorThumb][Debug] pixels opaque=" + opaque + " maxAlpha=" + maxAlpha + " magenta=" + magenta);
                try { File.WriteAllBytes(pngPath + ".debug.png", tex.EncodeToPNG()); }
                catch { }
            }
            if (IsBlank(pixels))
                throw new InvalidOperationException("渲染结果为空（无可见像素）");
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
        }
        finally
        {
            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    private static bool IsBlank(Color32[] pixels)
    {
        int opaque = 0;
        int magenta = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            if (p.a <= 20)
                continue;
            opaque++;
            if (p.r > 235 && p.g < 20 && p.b > 235)
                magenta++;
        }
        if (opaque < pixels.Length / 200)
            return true;
        if (magenta > opaque * 4 / 5)
            return true;
        return false;
    }
}

/// <summary>
/// 缩略图材质兜底（自包含，不依赖 JiaMap 程序集）。
/// 把 renderer 的材质克隆到 LayoutEditor/ThumbVertexColor，用 SerializedObject
/// 枚举贴图/颜色槽位，避免 bundle 材质 shader 丢失导致洋红/白模。
/// </summary>
internal static class LayoutEditorThumbMaterialFix
{
    public static string DebugItemId = "";

    private static Shader s_thumbShader;
    private static bool s_shaderTried;

    private static readonly string[] NonDiffuseHints =
    {
        "bump", "normal", "nrm", "rmea", "mask", "detail", "metallic", "spec",
        "occlusion", "emiss", "gloss", "rough", "height", "noise", "gradient",
        "lut", "ramp", "shadow", "displace", "flow", "ripple"
    };

    private static Shader ThumbShader
    {
        get
        {
            if (!s_shaderTried)
            {
                s_shaderTried = true;
                s_thumbShader = Shader.Find("LayoutEditor/ThumbVertexColor");
                if (s_thumbShader == null)
                    s_thumbShader = Shader.Find("Standard");
                if (s_thumbShader == null)
                    s_thumbShader = Shader.Find("Unlit/Texture");
            }
            return s_thumbShader;
        }
    }

    public static List<Material> FixRenderers(GameObject go, Color fallbackColor)
    {
        var patched = new List<Material>();
        var shader = ThumbShader;
        if (shader == null)
            return patched;

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        int staticVisuals = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && !(renderers[i] is ParticleSystemRenderer))
                staticVisuals++;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || r is SpriteRenderer)
                continue;
            if (r is ParticleSystemRenderer)
            {
                // 有静态网格时关掉粒子：预览只保留主体，避免默认粒子 shader
                // 在编辑器里回退成洋红并把整张图污染。
                if (staticVisuals > 0)
                    r.enabled = false;
                continue;
            }
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                var fallback = CloneAsThumb(null, shader, fallbackColor);
                if (fallback != null)
                {
                    patched.Add(fallback);
                    r.sharedMaterials = new Material[] { fallback };
                }
                continue;
            }
            var next = new Material[mats.Length];
            for (int j = 0; j < mats.Length; j++)
            {
                next[j] = CloneAsThumb(mats[j], shader, fallbackColor);
                if (next[j] != null)
                    patched.Add(next[j]);
            }
            r.sharedMaterials = next;
        }
        return patched;
    }

    private static Material CloneAsThumb(Material src, Shader shader, Color fallbackColor)
    {
        if (shader == null)
            return null;
        var mat = new Material(shader);
        if (src == null)
        {
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", fallbackColor);
            return mat;
        }
        mat.name = src.name + "_thumb";

        Texture2D tex;
        Color color;
        ExtractDiffuse(src, out tex, out color);
        // bundle 内材质偶发贴图丢失且主色为纯黑/纯白 → 用分类兜底色，避免白模/黑剪影。
        if (tex == null && (IsNearWhite(color) || IsNearBlack(color)))
            color = fallbackColor;
        if (!string.IsNullOrEmpty(DebugItemId))
        {
            Debug.Log("[DecorThumb][Debug] srcMat=" + src.name
                + " shader=" + (src.shader == null ? "null" : src.shader.name)
                + " mainTex=" + (src.mainTexture == null ? "<null>" : src.mainTexture.name)
                + " extractedTex=" + (tex == null ? "<null>" : tex.name)
                + " color=" + color);
        }
        if (tex != null && mat.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        return mat;
    }

    private static void ExtractDiffuse(Material src, out Texture2D tex, out Color color)
    {
        tex = null;
        color = Color.white;
        try
        {
            var so = new SerializedObject(src);
            var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            Texture2D fallbackTex = null;
            if (texEnvs != null && texEnvs.isArray)
            {
                for (int i = 0; i < texEnvs.arraySize; i++)
                {
                    var pair = texEnvs.GetArrayElementAtIndex(i);
                    var nameProp = pair.FindPropertyRelative("first.name");
                    var texProp = pair.FindPropertyRelative("second.m_Texture");
                    if (nameProp == null || texProp == null)
                        continue;
                    var t = texProp.objectReferenceValue as Texture2D;
                    if (t == null)
                        continue;
                    string slot = (nameProp.stringValue ?? "").ToLowerInvariant();
                    if (fallbackTex == null)
                        fallbackTex = t;
                    if (IsNonDiffuseSlot(slot))
                        continue;
                    tex = t;
                    break;
                }
            }
            if (tex == null)
                tex = fallbackTex;

            var colors = so.FindProperty("m_SavedProperties.m_Colors");
            if (colors != null && colors.isArray)
            {
                string[] preferred = { "_Color", "_MaskColor", "_Colour", "_TintColor", "_BaseColor" };
                for (int i = 0; i < preferred.Length; i++)
                {
                    Color c;
                    if (TryGetColor(colors, preferred[i], out c))
                    {
                        color = c;
                        break;
                    }
                }
                if (color == Color.white)
                {
                    for (int i = 0; i < colors.arraySize; i++)
                    {
                        var pair = colors.GetArrayElementAtIndex(i);
                        var cp = pair.FindPropertyRelative("second");
                        if (cp == null)
                            continue;
                        Color c = cp.colorValue;
                        if (Mathf.Abs(c.r - 1f) > 0.01f || Mathf.Abs(c.g - 1f) > 0.01f || Mathf.Abs(c.b - 1f) > 0.01f)
                        {
                            color = c;
                            break;
                        }
                    }
                }
            }
        }
        catch
        {
            // 材质损坏时保持白色，避免导出中断。
        }
    }

    private static bool IsNonDiffuseSlot(string slot)
    {
        for (int i = 0; i < NonDiffuseHints.Length; i++)
        {
            if (slot.IndexOf(NonDiffuseHints[i], StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }

    private static bool IsNearWhite(Color c)
    {
        return c.r > 0.88f && c.g > 0.88f && c.b > 0.88f;
    }

    private static bool IsNearBlack(Color c)
    {
        return c.r < 0.06f && c.g < 0.06f && c.b < 0.06f;
    }

    private static bool TryGetColor(SerializedProperty colors, string key, out Color color)
    {
        color = Color.white;
        for (int i = 0; i < colors.arraySize; i++)
        {
            var pair = colors.GetArrayElementAtIndex(i);
            var nameProp = pair.FindPropertyRelative("first.name");
            var colorProp = pair.FindPropertyRelative("second");
            if (nameProp == null || colorProp == null)
                continue;
            if (!string.Equals(nameProp.stringValue, key, StringComparison.OrdinalIgnoreCase))
                continue;
            color = colorProp.colorValue;
            return true;
        }
        return false;
    }
}
