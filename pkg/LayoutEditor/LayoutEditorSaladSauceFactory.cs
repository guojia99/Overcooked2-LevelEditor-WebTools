using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LevelEditor;
using LevelEditorStub;
using HarmonyLib;

// No scenes or LevelInfo assets are written by the factory/validation endpoint.
public static class LayoutEditorSaladSauceFactory
{
    const string Root = "Assets/commonW3/custom_recipes/salad/";
    const string Prefix = "Web_SauceSalad_";
    const float KetchupY = 0.20f, MustardY = KetchupY + 0.006f;
    [Serializable] sealed class Manifest { public Base[] bases; }
    [Serializable] sealed class Base { public string id; public string sourceAssetPath; public string uidKey; public string[] aliases; }
    static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static bool UsesSauces(LevelInfoSO info)
    {
        if (info == null) return false;
        foreach (ScriptableObject so in info.recipes ?? new ScriptableObject[0])
        {
            CustomRecipeSO r = so as CustomRecipeSO;
            if (r != null && r.recipeName != null && r.recipeName.StartsWith(Prefix, StringComparison.Ordinal) && r.score > 0) return true;
        }
        return false;
    }

    public static void SyncOptionals(LevelInfoSO info)
    {
        if (info == null) return;
        bool story = false, dlc = false;
        foreach (ScriptableObject so in info.recipes ?? new ScriptableObject[0])
        {
            CustomRecipeSO r = so as CustomRecipeSO;
            if (r == null || r.score <= 0 || r.recipeName == null || !r.recipeName.StartsWith(Prefix, StringComparison.Ordinal)) continue;
            if (r.modelSO != null && r.modelSO.name.IndexOf("Story", StringComparison.Ordinal) >= 0) story = true;
            else dlc = true;
        }
        List<ScriptableObject> merged = new List<ScriptableObject>();
        foreach (ScriptableObject old in info.optionalRecipeMatchListItems ?? new ScriptableObject[0])
        {
            if (old == null || !old.name.StartsWith(Prefix + "Optional_", StringComparison.Ordinal)) merged.Add(old);
        }
        foreach (string g in AssetDatabase.FindAssets("t:CustomRecipeSO", new string[] { Root.TrimEnd('/') }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            string id = Path.GetFileNameWithoutExtension(p);
            if ((story && id.StartsWith(Prefix + "Optional_Story_", StringComparison.Ordinal))
                || (dlc && id.StartsWith(Prefix + "Optional_DLC11_", StringComparison.Ordinal)))
                merged.Add(AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(p));
        }
        if (story || dlc)
        {
            // Pure-node sauces are deliberately excluded from allIngredients by the
            // generic filler. Register them here for network UID deserialization.
            List<string> nodes = new List<string>();
            nodes.Add("Assets/commonW1/pseudo_prefab_so/dlc11/food/dlc11_ketchup.asset");
            nodes.Add("Assets/commonW1/pseudo_prefab_so/dlc11/food/dlc11_mustard.asset");
            if (story) foreach (string id in new string[] { "LettuceSO", "TomatoSO", "CucumberSO" })
                nodes.Add("Assets/common01/food/Ingredients/" + id + ".asset");
            if (dlc) foreach (string id in new string[] { "lettuce", "tomato", "cucumber", "corn", "onion_salad" })
                nodes.Add("Assets/common03/food/Ingredients/dlc11/dlc11_" + id + ".asset");
            foreach (string path in nodes)
            {
                ScriptableObject node = Required<PseudoPrefabSO>(path);
                if (!merged.Contains(node)) merged.Add(node);
            }
        }
        info.optionalRecipeMatchListItems = merged.ToArray();
    }

    public static void AddDependencies(LevelInfoSO info, List<string> deps)
    {
        if (!UsesSauces(info)) return;
        // Runtime original lookups use native nodes and meshes, loaded by SO at Awake.
        // Both sauce families are explicit; aliases never enter final recipe composition.
        foreach (string bundle in new string[] { "bundle18", "bundle47", "bundle427", "bundle428", "bundle354" })
            if (!deps.Contains(bundle) && LayoutEditorCatalogApi.BundleFileExists(bundle)) deps.Add(bundle);
    }

    [MenuItem("Layout Editor/Recipes/Rebuild Salad Sauce Assets")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before rebuilding salad assets");
        MakeVisual("SaladKetchup", KetchupY);
        MakeVisual("SaladMustard", MustardY);
        MakeCarrier("Story", "Assets/common02/food/story_meals/CompositeSalad.asset");
        MakeCarrier("DLC11", "Assets/commonW3/pseudo_prefab_so/dlc11_compositesalad.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("[SaladSauce] Rebuilt two visuals and two native carriers in commonW3");
    }

    static Type Find(string name)
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(name);
            if (t != null) return t;
        }
        throw new TypeLoadException(name);
    }

    static FieldInfo Field(Type t, string name)
    {
        while (t != null)
        {
            FieldInfo f = t.GetField(name, Flags | BindingFlags.DeclaredOnly);
            if (f != null) return f;
            t = t.BaseType;
        }
        throw new MissingFieldException(name);
    }

    static T Required<T>(string p) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(p);
        if (asset == null) throw new InvalidOperationException("Asset missing: " + p);
        return asset;
    }

    static GameObject SavePrefab(string p, GameObject go)
    {
        GameObject old = AssetDatabase.LoadAssetAtPath<GameObject>(p);
        return old == null ? PrefabUtility.CreatePrefab(p, go) : PrefabUtility.ReplacePrefab(go, old, ReplacePrefabOptions.ReplaceNameBased);
    }

    static void MakeVisual(string id, float y)
    {
        string p = Root + "models/" + id;
        Mesh mesh = null;
        foreach (UnityEngine.Object a in AssetDatabase.LoadAllAssetsAtPath(p + ".fbx"))
            if (a is Mesh) { if (mesh != null) throw new InvalidOperationException("Expected one mesh: " + p); mesh = (Mesh)a; }
        if (mesh == null) throw new InvalidOperationException("FBX has no mesh: " + p);
        GameObject root = new GameObject(id + "Visual");
        try
        {
            GameObject child = new GameObject("Sauce");
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(0f, y, 0f);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = Required<Material>(p + ".mat");
            SavePrefab(p + "Visual.prefab", root);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void EnsureBundle(string name)
    {
        foreach (AssetBundle b in AssetBundle.GetAllLoadedAssetBundles()) if (b.name == name) return;
        // Native manager resolves transitive bundle dependencies against its manifest.
        object mgr = PseudoPrefabManager.Instance;
        if (mgr == null) throw new InvalidOperationException("PseudoPrefabManager must be initialized");
        typeof(PseudoPrefabManager).GetMethod("LoadAssetBundle", Flags).Invoke(mgr, new object[] { name, false });
    }

    static void MakeCarrier(string family, string sourcePath)
    {
        PseudoPrefabSO source = Required<PseudoPrefabSO>(sourcePath);
        EnsureBundle(source.bundleName);
        GameObject original = PseudoPrefabManager.LoadAsset(source);
        if (original == null) throw new InvalidOperationException("Native carrier missing: " + source.assetPath);
        // Only the two native behaviour types are carried. Loaded bundle roots have
        // nonpersistent MonoScripts and can contain corrupt editor-only transform hints.
        Check(original.GetComponent(Find("AssignableOrderDefinition")) != null
            && original.GetComponent(Find("SaladCosmeticDecisions")) != null, "original carrier shape");
        GameObject clone = new GameObject();
        try
        {
            string id = Prefix + family + "_Model";
            clone.name = id;
            clone.transform.position = Vector3.zero;
            clone.transform.rotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;
            clone.AddComponent(Find("AssignableOrderDefinition"));
            clone.AddComponent(Find("SaladCosmeticDecisions"));
            Component cosmetic = clone.GetComponent(Find("SaladCosmeticDecisions"));
            if (cosmetic == null) throw new InvalidOperationException("Native carrier has no salad cosmetics");
            // Bundle objects are not persistent project assets. Keep only SO references;
            // Awake clones and fills the original lookup before the native Start.
            Field(cosmetic.GetType(), "m_prefabLookup").SetValue(cosmetic, null);
            Component assignable = clone.GetComponent(Find("AssignableOrderDefinition"));
            Field(assignable.GetType(), "m_orderData").SetValue(assignable, null);
            Component visuals = clone.AddComponent(Find("CustomStub.SaladSauceVisuals"));
            Type t = visuals.GetType();
            Field(t, "originalCarrier").SetValue(visuals, source);
            Field(t, "ketchup").SetValue(visuals, Required<PseudoPrefabSO>("Assets/commonW1/pseudo_prefab_so/dlc11/food/dlc11_ketchup.asset"));
            Field(t, "mustard").SetValue(visuals, Required<PseudoPrefabSO>("Assets/commonW1/pseudo_prefab_so/dlc11/food/dlc11_mustard.asset"));
            Field(t, "ketchupAlias").SetValue(visuals, Required<PseudoPrefabSO>("Assets/common03/food/Ingredients/dlc08/DLC08_Ketchup.asset"));
            Field(t, "mustardAlias").SetValue(visuals, Required<PseudoPrefabSO>("Assets/common03/food/Ingredients/dlc08/DLC08_Mustard.asset"));
            Field(t, "ketchupVisual").SetValue(visuals, Required<GameObject>(Root + "models/SaladKetchupVisual.prefab"));
            Field(t, "mustardVisual").SetValue(visuals, Required<GameObject>(Root + "models/SaladMustardVisual.prefab"));
            GameObject prefab = SavePrefab(Root + "models/" + id + ".prefab", clone);
            PseudoPrefabSO model = Required<PseudoPrefabSO>(Root + "models/" + id + ".asset");
            model.asset = prefab;
            EditorUtility.SetDirty(model);
        }
        finally { UnityEngine.Object.DestroyImmediate(clone); }
    }

    public static string Run(string action)
    {
        try
        {
            if (action == "rebuild") Rebuild();
            return Validate();
        }
        catch (Exception ex) { return "{\"ok\":false,\"error\":" + LayoutEditorJson.ToJson(ex.ToString()) + "}"; }
    }

    const string BuildRoot = "Library/SaladSauceBundleBuild";
    const string BuildStatePath = BuildRoot + "/result.json";
    static bool bundleBuildPending;
    [Serializable] sealed class BundleValidation
    {
        public bool ok;
        public string state, startedUtc, completedUtc, error, outputDirectory;
        public string target = "StandaloneWindows";
        public string runtimeStageState, runtimeDllSha256, stagedDllSha256, bundledDllSha256;
        public int icons, finalRecipes, optionalAnchors, carrierChecks;
        public bool scenesUnchanged;
        public BundleDetails[] bundles;
        public string[] meshes, textureHashDuplicates, textureHashesUnavailable;
        public string[] visualGeometry;
        public string[] colourEvidence;
    }
    [Serializable] sealed class BundleDetails
    {
        public string name, sha256;
        public long bytes;
        public string[] assets, dependencies, textures;
    }
    static string Hash(byte[] bytes)
    {
        using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    static string SceneState()
    {
        List<string> scenes = new List<string>();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            UnityEngine.SceneManagement.Scene s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            scenes.Add(s.path + "|" + s.GetHashCode() + "|" + s.isDirty);
        }
        return string.Join(";", scenes.ToArray());
    }
    static void BuildGuard()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Exit Play mode and wait for compilation/import before building");
        Check(LayoutStubDllBuilder.GetRuntimeStageState() == "fresh", "runtime DLL must be fresh before build");
        Check(!EditorUtility.scriptCompilationFailed, "script compilation failed");
    }
    public static string BundleBuildStatus()
    {
        return File.Exists(BuildStatePath) ? File.ReadAllText(BuildStatePath) : "{\"ok\":true,\"state\":\"idle\"}";
    }
    public static string StartBundleBuild()
    {
        try
        {
            if (bundleBuildPending) return BundleBuildStatus();
            BuildGuard();
            Directory.CreateDirectory(BuildRoot);
            BundleValidation report = new BundleValidation();
            report.state = "queued"; report.startedUtc = DateTime.UtcNow.ToString("o");
            File.WriteAllText(BuildStatePath, JsonUtility.ToJson(report, true));
            bundleBuildPending = true;
            EditorApplication.delayCall += delegate { BuildBundles(report); };
            return BundleBuildStatus();
        }
        catch (Exception ex) { return "{\"ok\":false,\"error\":" + LayoutEditorJson.ToJson(ex.ToString()) + "}"; }
    }
    static void BuildBundles(BundleValidation report)
    {
        string sceneBefore = SceneState();
        List<AssetBundle> loaded = new List<AssetBundle>();
        try
        {
            BuildGuard();
            report.state = "building";
            report.outputDirectory = Path.GetFullPath(BuildRoot + "/Windows");
            Directory.CreateDirectory(report.outputDirectory);
            File.WriteAllText(BuildStatePath, JsonUtility.ToJson(report, true));
            LayoutStubDllBuilder.StageRuntime(true);
            BuildGuard();
            const string staged = "Assets/WebCustomStubRuntime/RuntimeDll/WebCustomStubRuntime.dll.bytes";
            report.runtimeStageState = LayoutStubDllBuilder.GetRuntimeStageState();
            report.runtimeDllSha256 = Hash(File.ReadAllBytes("Library/ScriptAssemblies/WebCustomStubRuntime.dll"));
            report.stagedDllSha256 = Hash(File.ReadAllBytes(staged));
            Check(report.runtimeDllSha256 == report.stagedDllSha256, "staged runtime mismatch");
            string[] names = new string[] { "commonw3", "webcustomstub_runtime" };
            AssetBundleBuild[] builds = new AssetBundleBuild[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                builds[i].assetBundleName = names[i];
                builds[i].assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(names[i]);
                Check(builds[i].assetNames.Length > 0, "empty bundle: " + names[i]);
                foreach (string p in builds[i].assetNames)
                    Check(!p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && !p.StartsWith("Assets/LevelSets/", StringComparison.OrdinalIgnoreCase), "user asset in build: " + p);
            }
            // Explicit map and isolated output: no scene prepare/save or global bundle edits.
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(report.outputDirectory, builds,
                BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows);
            Check(manifest != null, "BuildPipeline returned no manifest");
            report.bundles = new BundleDetails[names.Length];
            Dictionary<string, List<string>> textureHashes = new Dictionary<string, List<string>>();
            List<string> unavailable = new List<string>();
            List<string> meshReport = new List<string>();
            List<string> geometry = new List<string>();
            List<string> colours = new List<string>();
            for (int i = 0; i < names.Length; i++)
            {
                BundleDetails detail = new BundleDetails();
                detail.name = names[i];
                string file = Path.Combine(report.outputDirectory, names[i]);
                detail.bytes = new FileInfo(file).Length;
                detail.sha256 = Hash(File.ReadAllBytes(file));
                detail.dependencies = manifest.GetAllDependencies(names[i]);
                AssetBundle bundle = AssetBundle.LoadFromFile(file);
                Check(bundle != null, "cannot read built bundle: " + file);
                loaded.Add(bundle);
                detail.assets = bundle.GetAllAssetNames();
                List<string> textures = new List<string>();
                foreach (Texture2D texture in bundle.LoadAllAssets<Texture2D>())
                {
                    string entry = detail.name + ":" + texture.name + ":" + texture.width + "x" + texture.height;
                    textures.Add(entry);
                    // Unity 2017 exposes readability through serialization, not Texture2D.
                    SerializedProperty readable = new SerializedObject(texture).FindProperty("m_IsReadable");
                    if (readable == null || !readable.boolValue) { unavailable.Add(entry); continue; }
                    byte[] raw = texture.GetRawTextureData();
                    if (raw == null || raw.Length == 0) { unavailable.Add(entry); continue; }
                    string hash = texture.width + "x" + texture.height + ":" + texture.format + ":" + Hash(raw);
                    List<string> matches;
                    if (!textureHashes.TryGetValue(hash, out matches)) { matches = new List<string>(); textureHashes.Add(hash, matches); }
                    matches.Add(entry);
                }
                detail.textures = textures.ToArray();
                report.bundles[i] = detail;
                if (i == 1)
                {
                    TextAsset dll = bundle.LoadAsset<TextAsset>(staged.ToLowerInvariant());
                    Check(dll != null, "runtime DLL missing in bundle");
                    report.bundledDllSha256 = Hash(dll.bytes);
                    Check(report.bundledDllSha256 == report.runtimeDllSha256, "built runtime mismatch");
                }
                else
                {
                    foreach (string p in detail.assets)
                    {
                        if (p.StartsWith(Root.ToLowerInvariant() + "icons/" + Prefix.ToLowerInvariant()) && p.EndsWith(".png"))
                        { Check(bundle.LoadAsset<Texture2D>(p) != null, "missing icon " + p); report.icons++; }
                        if (p.StartsWith(Root.ToLowerInvariant()) && p.EndsWith(".asset"))
                        {
                            CustomRecipeSO recipe = bundle.LoadAsset<CustomRecipeSO>(p);
                            if (recipe != null && recipe.name.StartsWith(Prefix, StringComparison.Ordinal))
                            { if (recipe.score > 0) { report.finalRecipes++; Check(recipe.icon != null, "recipe icon missing " + p); } else report.optionalAnchors++; }
                        }
                    }
                    foreach (string id in new string[] { "SaladKetchup", "SaladMustard" })
                    {
                        GameObject visual = bundle.LoadAsset<GameObject>((Root + "models/" + id + "Visual.prefab").ToLowerInvariant());
                        Check(visual != null, "missing visual " + id);
                        geometry.Add(CheckVisual(visual, id, "bundle"));
                        colours.Add(CheckSauceColour(visual, id == "SaladKetchup" ? "red" : "yellow", "bundle:" + id));
                        MeshFilter mf = visual.GetComponentInChildren<MeshFilter>();
                        MeshRenderer mr = visual.GetComponentInChildren<MeshRenderer>();
                        Check(mf != null && mf.sharedMesh != null && mr != null && mr.sharedMaterial != null && mr.sharedMaterial.mainTexture != null, "mesh/material/texture chain " + id);
                        meshReport.Add(id + ":tri=" + mf.sharedMesh.triangles.Length / 3 + ":material=" + mr.sharedMaterial.name + ":texture=" + mr.sharedMaterial.mainTexture.name);
                    }
                    foreach (string family in new string[] { "Story", "DLC11" })
                    {
                        GameObject carrier = bundle.LoadAsset<GameObject>((Root + "models/" + Prefix + family + "_Model.prefab").ToLowerInvariant());
                        Check(carrier != null, "missing carrier " + family);
                        Component cosmetic = carrier.GetComponent(Find("SaladCosmeticDecisions"));
                        Component visual = carrier.GetComponent(Find("CustomStub.SaladSauceVisuals"));
                        Check(cosmetic != null && visual != null && carrier.GetComponent(Find("AssignableOrderDefinition")) != null, "carrier scripts " + family);
                        Check(Field(cosmetic.GetType(), "m_prefabLookup").GetValue(cosmetic) == null, "carrier lookup must be initialized by runtime Awake");
                        foreach (string field in new string[] { "originalCarrier", "ketchup", "mustard", "ketchupAlias", "mustardAlias", "ketchupVisual", "mustardVisual" })
                            Check(Field(visual.GetType(), field).GetValue(visual) as UnityEngine.Object != null, "carrier reference " + field);
                        report.carrierChecks++;
                    }
                }
            }
            List<string> duplicates = new List<string>();
            foreach (KeyValuePair<string, List<string>> pair in textureHashes)
                if (pair.Value.Count > 1) duplicates.Add(pair.Key + "|" + string.Join("|", pair.Value.ToArray()));
            report.textureHashDuplicates = duplicates.ToArray();
            report.textureHashesUnavailable = unavailable.ToArray();
            report.meshes = meshReport.ToArray();
            report.visualGeometry = geometry.ToArray();
            report.colourEvidence = colours.ToArray();
            Check(report.icons == 78 && report.finalRecipes == 78 && report.optionalAnchors == 24 && report.carrierChecks == 2, "bundle content counts");
            report.ok = true; report.state = "completed";
        }
        catch (Exception ex) { report.ok = false; report.state = "failed"; report.error = ex.ToString(); }
        finally
        {
            foreach (AssetBundle bundle in loaded) bundle.Unload(true);
            report.scenesUnchanged = sceneBefore == SceneState();
            if (!report.scenesUnchanged) { report.ok = false; report.state = "failed"; report.error += " Scene state changed"; }
            report.completedUtc = DateTime.UtcNow.ToString("o");
            File.WriteAllText(BuildStatePath, JsonUtility.ToJson(report, true));
            bundleBuildPending = false;
        }
    }

    public static string Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before running isolated salad validation");
        foreach (string b in new string[] { "bundle18", "bundle47", "bundle427", "bundle428", "bundle354" }) EnsureBundle(b);
        int finals = 0, optionals = 0;
        HashSet<int> uids = new HashSet<int>();
        foreach (string g in AssetDatabase.FindAssets("t:CustomRecipeSO", new string[] { Root.TrimEnd('/') }))
        {
            CustomRecipeSO r = Required<CustomRecipeSO>(AssetDatabase.GUIDToAssetPath(g));
            if (r.recipeName == null || !r.recipeName.StartsWith(Prefix, StringComparison.Ordinal)) continue;
            if (!uids.Add(r.uID)) throw new InvalidOperationException("Duplicate recipe UID");
            if (r.score > 0) finals++; else optionals++;
            if (r.GetModel() == null) throw new InvalidOperationException("Missing model: " + r.name);
        }
        if (finals != 78 || optionals != 24) throw new InvalidOperationException("Expected 78 finals / 24 anchors, got " + finals + "/" + optionals);
        List<string> meshes = new List<string>();
        List<string> geometry = new List<string>();
        int checks = ValidateNative(geometry);
        List<string> colours = new List<string>();
        checks += ValidateMachineColours(colours);
        foreach (PseudoPrefabStub stub in UnityEngine.Object.FindObjectsOfType<PseudoPrefabStub>())
        {
            if (stub.pseudoPrefabSO == null || stub.pseudoPrefabSO.prefabName.IndexOf("condiment_dispenser", StringComparison.Ordinal) < 0) continue;
            PseudoPrefab pseudo = stub.GetComponent<PseudoPrefab>();
            GameObject child = pseudo == null ? null : pseudo.childGameObject;
            IngredientPropertiesComponent properties = child == null ? null : child.GetComponent<IngredientPropertiesComponent>();
            IngredientAssembledNode node = properties == null ? null : properties.GetOrderComposition() as IngredientAssembledNode;
            if (node == null) continue;
            IngredientOrderNode ingredient = node.m_ingriedientOrderNode;
            PlacementItemSwitcher switcher = child.GetComponent<PlacementItemSwitcher>();
            int index = switcher == null ? -1 : Array.IndexOf(switcher.m_ingredients, ingredient);
            int slot = (int)Find("CustomStub.SaladSauceRuntime").GetMethod("DispenserVisualSlot").Invoke(null, new object[] { ingredient.m_uID });
            colours.Add("liveEdit:" + stub.name + ":node=" + ingredient.name + ":uid=" + ingredient.m_uID + ":configuredIndex=" + index + ":nativeVisualSlot=" + slot + ":readOnly=true");
        }
        foreach (string id in new string[] { "SaladKetchup", "SaladMustard" })
        {
            geometry.Add(CheckVisual(Required<GameObject>(Root + "models/" + id + "Visual.prefab"), id, "asset"));
            foreach (UnityEngine.Object a in AssetDatabase.LoadAllAssetsAtPath(Root + "models/" + id + ".fbx"))
            {
                Mesh m = a as Mesh;
                if (m != null) meshes.Add(id + ":" + Unsupported.GetLocalIdentifierInFile(m.GetInstanceID()) + ":tri=" + (m.triangles.Length / 3));
            }
        }
        return "{\"ok\":true,\"finalRecipes\":" + finals + ",\"optionalAnchors\":" + optionals
            + ",\"nativeChecks\":" + checks
            + ",\"colourEvidence\":[" + string.Join(",", colours.ConvertAll(delegate(string s) { return LayoutEditorJson.ToJson(s); }).ToArray()) + "]"
            + ",\"visualGeometry\":[" + string.Join(",", geometry.ConvertAll(delegate(string s) { return LayoutEditorJson.ToJson(s); }).ToArray()) + "]"
            + ",\"meshes\":[" + string.Join(",", meshes.ConvertAll(delegate(string s) { return LayoutEditorJson.ToJson(s); }).ToArray()) + "]}";
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Salad native validation: " + message);
    }

    // Read the rendered texture through a temporary GPU copy; imported and
    // bundled native textures are intentionally not CPU-readable. Sample the
    // actual mesh UVs, not the filename or unused atlas background.
    static string CheckSauceColour(GameObject visual, string expected, string context)
    {
        MeshFilter filter = visual.GetComponentInChildren<MeshFilter>(true);
        Check(filter != null, context + " mesh");
        Material material = filter.GetComponent<Renderer>().sharedMaterial;
        string property = material.HasProperty("_MainTex") ? "_MainTex" : "_ColourAlpha";
        Texture texture = material.GetTexture(property);
        Check(texture != null, context + " texture");
        RenderTexture previous = RenderTexture.active;
        RenderTexture copy = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        Texture2D pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        Color mean = Color.clear;
        try
        {
            Graphics.Blit(texture, copy);
            RenderTexture.active = copy;
            pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            pixels.Apply();
            Vector2 scale = material.GetTextureScale(property), offset = material.GetTextureOffset(property);
            Vector2[] uvs = filter.sharedMesh.uv;
            foreach (Vector2 uv in uvs)
            {
                Color c = pixels.GetPixelBilinear(uv.x * scale.x + offset.x, uv.y * scale.y + offset.y);
                if (QualitySettings.activeColorSpace == ColorSpace.Linear) c = c.gamma;
                mean += c / uvs.Length;
            }
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(copy);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
        // The native bottle atlas includes glass and metal; its UV mean ratios
        // are 0.95 (yellow) and 0.75 (red). Isolated sauce textures are 0.59/0.12.
        bool bottle = filter.sharedMesh.name.StartsWith("Condiment_", StringComparison.Ordinal);
        bool correct = bottle
            ? (expected == "yellow" ? mean.g > mean.r * 0.85f : mean.g < mean.r * 0.80f) && mean.r > mean.b * 1.5f
            : expected == "red" ? mean.g < mean.r * 0.3f : mean.g > mean.r * 0.5f && mean.b < mean.r * 0.2f;
        Check(correct, context + " actual colour " + expected + " mean=" + mean + " texture=" + texture.name + " material=" + material.name + " space=" + QualitySettings.activeColorSpace);
        return context + ":" + expected + ":mesh=" + filter.sharedMesh.name + ":material=" + material.name + ":texture=" + texture.name + ":mean=" + mean;
    }

    static int ValidateMachineColours(List<string> evidence)
    {
        Type rules = Find("CustomStub.SaladSauceRuntime");
        BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        FieldInfo activeField = rules.GetField("s_active", flags);
        bool active = (bool)activeField.GetValue(null);
        MethodInfo activate = rules.GetMethod("SetActive", flags);
        int checks = 0;
        try
        {
            activate.Invoke(null, new object[] { true });
            foreach (string family in new string[] { "dlc08", "dlc11" })
            {
                PseudoPrefabSO machineSO = Required<PseudoPrefabSO>("Assets/common03/pseudo_prefab_so/mechanisms/" + family + "_condiment_dispenser.asset");
                EnsureBundle(machineSO.bundleName);
                GameObject native = PseudoPrefabManager.LoadAsset(machineSO);
                IngredientOrderNode[] original = native.GetComponent<PlacementItemSwitcher>().m_ingredients;
                Check(original.Length == 2 && original[0].name.ToLowerInvariant().Contains("mustard") && original[1].name.ToLowerInvariant().Contains("ketchup"), family + " untouched native slot order");
                GameObject machine = UnityEngine.Object.Instantiate(native);
                machine.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    PlacementItemSwitcher switcher = machine.GetComponent<PlacementItemSwitcher>();
                    IngredientPropertiesComponent properties = machine.GetComponent<IngredientPropertiesComponent>();
                    Animator animator = machine.GetComponentInChildren<Animator>();
                    Check(animator != null && animator.runtimeAnimatorController != null, family + " native machine animator");
                    animator.Rebind();
                    ClientCondimentDispenserCosmeticDecisions client = machine.AddComponent<ClientCondimentDispenserCosmeticDecisions>();
                    client.StartSynchronising(machine.GetComponent<CondimentDispenserCosmeticDecisions>());
                    IngredientOrderNode lettuce = RecipeHelper.GetIngredientOrderNode(Required<PseudoPrefabSO>("Assets/common01/food/Ingredients/LettuceSO.asset"));
                    foreach (string saladFamily in new string[] { "Story", "DLC11" })
                    {
                        GameObject carrier = UnityEngine.Object.Instantiate(Required<GameObject>(Root + "models/" + Prefix + saladFamily + "_Model.prefab"));
                        carrier.hideFlags = HideFlags.HideAndDontSave;
                        try
                        {
                            Component visual = carrier.GetComponent(Find("CustomStub.SaladSauceVisuals"));
                            visual.GetType().GetMethod("BuildLookup").Invoke(visual, null);
                            Component cosmetic = carrier.GetComponent(Find("SaladCosmeticDecisions"));
                            OrderToPrefabLookup lookup = Field(cosmetic.GetType(), "m_prefabLookup").GetValue(cosmetic) as OrderToPrefabLookup;
                            TestContents both = new TestContents(); both.AddIngredient(lettuce.Convert());
                            for (int slot = 0; slot < original.Length; slot++)
                            {
                                IngredientOrderNode node = original[slot];
                                string colour = slot == 0 ? "yellow" : "red";
                                Check(node.m_iconSprite != null && node.m_iconSprite.name.ToLowerInvariant().Contains(slot == 0 ? "mustard" : "ketchup"), family + " native icon identity " + node.name);
                                Check(slot == 0 ? node.m_colour.g > node.m_colour.r * 0.6f : node.m_colour.r > node.m_colour.g * 2f, family + " native ingredient colour");
                                Transform bottle = null;
                                foreach (Transform t in machine.GetComponentsInChildren<Transform>(true)) if (t.name == "Condiment_" + (slot + 1)) bottle = t;
                                Check(bottle != null, family + " native bottle");
                                evidence.Add(CheckSauceColour(bottle.gameObject, colour, family + ":nativeSlot=" + slot));
                                Check((int)rules.GetMethod("DispenserVisualSlot").Invoke(null, new object[] { node.m_uID }) == slot, family + " runtime slot agrees with native");
                                // Reproduce the shipped bug: configured order overwrites
                                // the child BEFORE the editor patch, including a single sauce.
                                for (int mode = 0; mode < 2; mode++)
                                {
                                    switcher.m_ingredients = mode == 0 ? new IngredientOrderNode[] { original[1], original[0] } : new IngredientOrderNode[] { node };
                                    properties.SetIngredientOrderNode(node);
                                    animator.ResetTrigger("Condiment1"); animator.ResetTrigger("Condiment2");
                                    activeField.SetValue(null, false);
                                    client.OnItemSwitched();
                                    int unpatchedSlot = mode == 0 ? 1 - slot : 0;
                                    Check(animator.GetBool("Condiment" + (unpatchedSlot + 1)), family + " unpatched native index reproduces bug");
                                    activeField.SetValue(null, true);
                                    client.OnItemSwitched();
                                    Check(animator.GetBool("Condiment" + (slot + 1)), family + " runtime correct trigger for " + node.name + " mode=" + mode);
                                    Check(!animator.GetBool("Condiment" + (2 - slot)), family + " runtime wrong trigger cleared");
                                    CondimentDispenserCosmeticDecisions effects = machine.GetComponent<CondimentDispenserCosmeticDecisions>();
                                    Check(effects.m_particleEffects[slot].name == (slot == 0 ? "MustardSquirt" : "KetchupSquirt"), family + " native effect identity");
                                    // Exercise the same patched callback; suppress only audio
                                    // during isolated edit-mode validation (no AudioManager).
                                    MethodInfo audio = typeof(GameUtils).GetMethod("TriggerAudio", BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(GameOneShotAudioTag), typeof(int) }, null);
                                    Harmony silent = new Harmony("oc2.salad.validation.audio");
                                    try
                                    {
                                        silent.Patch(audio, new HarmonyMethod(typeof(LayoutEditorSaladSauceFactory).GetMethod("ValidationMuteAudio", flags)));
                                        client.OnPickupItem();
                                        for (int e = 0; e < effects.m_particleEffects.Length; e++) Check(effects.m_particleEffects[e].activeSelf == (e == slot), family + " native pickup effect selection");
                                    }
                                    finally { silent.UnpatchSelf(); }
                                    checks += 6;
                                }
                                AnimationClip selected = null;
                                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips) if (clip.name.EndsWith("Condeiment" + (slot + 1), StringComparison.Ordinal)) selected = clip;
                                Check(selected != null, family + " native slot clip");
                                selected.SampleAnimation(animator.gameObject, selected.length);
                                Check(bottle.gameObject.activeSelf, family + " clip enables selected bottle");
                                foreach (Transform t in machine.GetComponentsInChildren<Transform>(true))
                                    if (t.name == "Condiment_" + (2 - slot)) Check(!t.gameObject.activeSelf, family + " clip hides other bottle");
                                TestContents single = new TestContents(); single.AddIngredient(lettuce.Convert());
                                AssembledNodeTransfer.CombineWithContents(node.Convert(), single, true);
                                IngredientAssembledNode normalized = single.GetContentsElement(1) as IngredientAssembledNode;
                                Check(normalized != null && normalized.m_ingriedientOrderNode.name.ToLowerInvariant().Contains(slot == 0 ? "mustard" : "ketchup"), family + " normalization identity");
                                evidence.Add(CheckSauceColour(lookup.GetPrefabForNode(normalized), colour, family + "->" + saladFamily + ":uid=" + node.m_uID + "->" + normalized.m_ingriedientOrderNode.m_uID + ":icon=" + node.m_iconSprite.name));
                                evidence.Add(CheckSauceColour(lookup.GetPrefabForNode(node.Convert()), colour, family + "->" + saladFamily + ":directLookup"));
                                AssembledNodeTransfer.CombineWithContents(node.Convert(), both, true);
                                AssetBundle nativeOrders = PseudoPrefabManager.GetAssetBundle(family == "dlc08" ? "bundle354" : "bundle427");
                                OrderDefinitionNode hotdog = nativeOrders.LoadAsset<OrderDefinitionNode>("Assets/downloadablecontent/" + family + "/dlc_assets/data/orderdefinitions/recipeitems/" + (family == "dlc08" ? "hotdog_plain" : "dlc11_hotdog_plain") + ".asset");
                                Check(hotdog != null, family + " native hotdog definition");
                                TestContents dog = new TestContents(); dog.AddIngredient(hotdog.Convert());
                                AssembledDefinitionNode source = node.Convert();
                                AssembledNodeTransfer.CombineWithContents(source, dog, true);
                                Check(ReferenceEquals(source, dog.GetContentsElement(1)), family + " hotdog source identity untouched");
                                Check(!(bool)rules.GetMethod("RejectContents").Invoke(null, new object[] { hotdog.Convert(), source }), family + " hotdog bypass");
                                evidence.Add(family + ":node=" + node.name + ":uid=" + node.m_uID + ":trigger=Condiment" + (slot + 1) + ":clip=" + selected.name + ":single/reversed=pass:hotdogIdentity=unchanged");
                                checks += 11;
                            }
                            Check(both.GetContentsCount() == 3, family + " both sauces");
                            CheckSauceColour(lookup.GetPrefabForNode(both.GetContentsElement(1)), "yellow", family + " both yellow");
                            CheckSauceColour(lookup.GetPrefabForNode(both.GetContentsElement(2)), "red", family + " both red");
                            checks += 3;
                        }
                        finally { UnityEngine.Object.DestroyImmediate(carrier); }
                    }
                    PseudoPrefabSO red = Required<PseudoPrefabSO>(family == "dlc08" ? "Assets/common03/food/Ingredients/dlc08/DLC08_Ketchup.asset" : "Assets/commonW1/pseudo_prefab_so/dlc11/food/dlc11_ketchup.asset");
                    PseudoPrefabSO yellow = Required<PseudoPrefabSO>(family == "dlc08" ? "Assets/common03/food/Ingredients/dlc08/DLC08_Mustard.asset" : "Assets/commonW1/pseudo_prefab_so/dlc11/food/dlc11_mustard.asset");
                    switcher.m_ingredients = new IngredientOrderNode[] { original[1], original[0] };
                    typeof(LayoutEditorItemSwitcherPatch).GetMethod("Patch", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { machine, new PseudoPrefabSO[] { red, yellow }, machineSO });
                    Check(switcher.m_ingredients[0] == original[0] && switcher.m_ingredients[1] == original[1], family + " editor uses untouched bundle order");
                    Check(((IngredientAssembledNode)properties.GetOrderComposition()).m_ingriedientOrderNode == original[0], family + " editor initial node matches native slot");
                    checks += 2;
                }
                finally { UnityEngine.Object.DestroyImmediate(machine); }
            }
        }
        finally { activate.Invoke(null, new object[] { active }); }
        return checks;
    }

    static string CheckVisual(GameObject visual, string id, string context)
    {
        float y = id == "SaladKetchup" ? KetchupY : MustardY;
        Transform sauce = visual.transform.Find("Sauce");
        Check(sauce != null, context + " Sauce child " + id);
        Check((sauce.localPosition - new Vector3(0f, y, 0f)).sqrMagnitude < 0.00000001f
            && sauce.localScale == Vector3.one && Quaternion.Angle(sauce.localRotation, Quaternion.identity) < 0.001f, context + " visual transform " + id);
        MeshFilter filter = sauce.GetComponent<MeshFilter>();
        Renderer renderer = sauce.GetComponent<Renderer>();
        Check(filter != null && filter.sharedMesh != null && renderer != null, context + " geometry " + id);
        Bounds mesh = filter.sharedMesh.bounds;
        float lift = visual.transform.TransformVector(new Vector3(0f, KetchupY, 0f)).y;
        float expectedCenter = sauce.TransformPoint(mesh.center).y;
        Check(lift > 0f && Mathf.Abs(renderer.bounds.center.y - expectedCenter) < 0.0001f
            && Mathf.Abs(renderer.bounds.size.y - mesh.size.y * sauce.lossyScale.y) < 0.0001f, context + " raised renderer bounds " + id);
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0}:{1}:y={2:F6}:boundsY={3:F6}..{4:F6}:lift={5:F6}:previousBoundsY={6:F6}..{7:F6}",
            context, id, sauce.localPosition.y, renderer.bounds.min.y, renderer.bounds.max.y, lift, renderer.bounds.min.y - lift, renderer.bounds.max.y - lift);
    }

    static int ValidateNative(List<string> geometry)
    {
        Type rules = Find("CustomStub.SaladSauceRuntime");
        MethodInfo reject = rules.GetMethod("RejectContents");
        List<CustomRecipeSO> anchors = new List<CustomRecipeSO>();
        List<CustomRecipeSO> finals = new List<CustomRecipeSO>();
        foreach (string g in AssetDatabase.FindAssets("t:CustomRecipeSO", new string[] { Root.TrimEnd('/') }))
        {
            CustomRecipeSO r = Required<CustomRecipeSO>(AssetDatabase.GUIDToAssetPath(g));
            if (!r.name.StartsWith(Prefix, StringComparison.Ordinal)) continue;
            if (r.score > 0) finals.Add(r); else anchors.Add(r);
        }
        IngredientOrderNode red = RecipeHelper.GetIngredientOrderNode(Required<PseudoPrefabSO>("Assets/commonW1/pseudo_prefab_so/dlc11/food/dlc11_ketchup.asset"));
        IngredientOrderNode yellow = RecipeHelper.GetIngredientOrderNode(Required<PseudoPrefabSO>("Assets/commonW1/pseudo_prefab_so/dlc11/food/dlc11_mustard.asset"));
        Check(red.m_uID == 890084 && yellow.m_uID == 890076, "canonical sauce UID");
        int checks = 1;
        Dictionary<CustomRecipeSO, OrderDefinitionNode> anchorNodes = new Dictionary<CustomRecipeSO, OrderDefinitionNode>();
        foreach (CustomRecipeSO a in anchors) anchorNodes.Add(a, RecipeHelper.GetOrderDefinitionNode(a));
        try
        {
        foreach (CustomRecipeSO r in finals)
        {
            OrderDefinitionNode node = RecipeHelper.GetOrderDefinitionNode(r);
            CompositeAssembledNode full = node.Convert() as CompositeAssembledNode;
            Check(full != null, r.name + " composite");
            List<AssembledDefinitionNode> vegetables = new List<AssembledDefinitionNode>();
            List<AssembledDefinitionNode> sauces = new List<AssembledDefinitionNode>();
            foreach (AssembledDefinitionNode leaf in full.m_composition)
            {
                IngredientAssembledNode ing = leaf as IngredientAssembledNode;
                Check(ing != null, r.name + " leaf");
                int uid = ing.m_ingriedientOrderNode.m_uID;
                if (uid == 890084 || uid == 890076) sauces.Add(leaf); else vegetables.Add(leaf);
            }
            // Every nonempty subset of this recipe, including either dual-sauce order,
            // must have a native plating model. No pure-sauce/empty anchor can match.
            for (int mask = 1; mask < (1 << vegetables.Count); mask++)
            {
                for (int sm = 1; sm < (1 << sauces.Count); sm++)
                {
                    List<AssembledDefinitionNode> leaves = new List<AssembledDefinitionNode>();
                    for (int i = 0; i < vegetables.Count; i++) if ((mask & (1 << i)) != 0) leaves.Add(vegetables[i]);
                    for (int i = 0; i < sauces.Count; i++) if ((sm & (1 << i)) != 0) leaves.Add(sauces[i]);
                    CompositeAssembledNode partial = new CompositeAssembledNode();
                    partial.m_composition = leaves.ToArray();
                    bool match = false;
                    foreach (CustomRecipeSO a in anchors)
                        if (a.modelSO == r.modelSO && AssembledDefinitionNode.Matching(partial, anchorNodes[a])) { match = true; break; }
                    Check(match, r.name + " partial mask=" + mask + "/" + sm);
                    foreach (AssembledDefinitionNode sauce in sauces)
                    {
                        bool duplicate = leaves.Contains(sauce);
                        Check((bool)reject.Invoke(null, new object[] { partial, sauce }) == duplicate, r.name + " duplicate guard");
                        checks++;
                    }
                    checks++;
                }
            }
            foreach (AssembledDefinitionNode sauce in sauces)
            {
                Check((bool)reject.Invoke(null, new object[] { new AssembledDefinitionNode[0], sauce }), "empty plate rejects sauce");
                checks++;
            }
            UnityEngine.Object.DestroyImmediate(node);
        }
        foreach (CustomRecipeSO a in anchors)
        {
            OrderDefinitionNode n = anchorNodes[a];
            Check(!AssembledDefinitionNode.Matching(red.Convert(), n), "red-only must not match");
            Check(!AssembledDefinitionNode.Matching(yellow.Convert(), n), "yellow-only must not match");
            checks += 2;
        }
        foreach (string family in new string[] { "Story", "DLC11" })
        {
            GameObject prefab = Required<GameObject>(Root + "models/" + Prefix + family + "_Model.prefab");
            GameObject go = UnityEngine.Object.Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                foreach (Component c in go.GetComponents<Component>()) Check(c != null, "missing carrier script");
                Component visual = go.GetComponent(Find("CustomStub.SaladSauceVisuals"));
                FieldInfo mustardField = Field(visual.GetType(), "mustard");
                object savedMustard = mustardField.GetValue(visual);
                mustardField.SetValue(visual, null);
                bool failed = false;
                try { visual.GetType().GetMethod("BuildLookup").Invoke(visual, null); }
                catch (TargetInvocationException) { failed = true; }
                Check(failed && Field(visual.GetType(), "m_lookup").GetValue(visual) == null, "failed visual lookup is not published");
                mustardField.SetValue(visual, savedMustard);
                visual.GetType().GetMethod("BuildLookup").Invoke(visual, null);
                Component cosmetic = go.GetComponent(Find("SaladCosmeticDecisions"));
                OrderToPrefabLookup lookup = Field(cosmetic.GetType(), "m_prefabLookup").GetValue(cosmetic) as OrderToPrefabLookup;
                Check(lookup.GetPrefabForNode(red.Convert()) != null, "red visual");
                Check(lookup.GetPrefabForNode(yellow.Convert()) != null, "yellow visual");
                foreach (string id in new string[] { "Ketchup", "Mustard" })
                {
                    IngredientOrderNode alias = RecipeHelper.GetIngredientOrderNode(Required<PseudoPrefabSO>("Assets/common03/food/Ingredients/dlc08/DLC08_" + id + ".asset"));
                    Check(lookup.GetPrefabForNode(alias.Convert()) != null, "alias visual " + id);
                }
                Check(go.GetComponentsInChildren<Renderer>(true).Length == 0, "carrier has no ghost renderers");
                string ingredientPath = family == "Story" ? "Assets/common01/food/Ingredients/LettuceSO.asset" : "Assets/common03/food/Ingredients/dlc11/dlc11_lettuce.asset";
                IngredientOrderNode vegetable = RecipeHelper.GetIngredientOrderNode(Required<PseudoPrefabSO>(ingredientPath));
                Check(lookup.GetPrefabForNode(vegetable.Convert()) != null, "original vegetable visual preserved");
                // Exercise native callbacks and OverlapModelsMealDecisions rendering,
                // without entering Play or creating synchronized entities.
                Component assignable = go.GetComponent(Find("AssignableOrderDefinition"));
                cosmetic.GetType().GetMethod("Start", Flags).Invoke(cosmetic, null);
                CompositeAssembledNode partial = new CompositeAssembledNode();
                partial.m_composition = new AssembledDefinitionNode[] { vegetable.Convert(), red.Convert(), yellow.Convert() };
                assignable.GetType().GetMethod("SetOrderComposition").Invoke(assignable, new object[] { partial });
                Check(go.GetComponentsInChildren<MeshRenderer>(true).Length >= 3, "native partial salad renders both sauces and vegetable");
                foreach (string id in new string[] { "SaladKetchup", "SaladMustard" })
                {
                    GameObject expected = Required<GameObject>(Root + "models/" + id + "Visual.prefab");
                    Mesh mesh = expected.GetComponentInChildren<MeshFilter>().sharedMesh;
                    int rendered = 0;
                    foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.sharedMesh == mesh)
                        {
                            geometry.Add(CheckVisual(filter.transform.parent.gameObject, id, family));
                            rendered++;
                        }
                    Check(rendered == 1, family + " renders one " + id);
                    checks++;
                }
                checks += 9;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        checks += ValidateSourcesAndIntegration(finals);
        checks += ValidatePlacementPatches();
        checks += ValidateMalformedNodes();
        checks += ValidateFeatureGate(finals[0]);
        return checks;
        }
        finally
        {
            foreach (OrderDefinitionNode n in anchorNodes.Values) UnityEngine.Object.DestroyImmediate(n);
        }
    }

    static int ValidateSourcesAndIntegration(List<CustomRecipeSO> finals)
    {
        string manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../layout-editor/scripts/data/salad-sauce-recipes.json"));
        Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
        Check(manifest.bases.Length == 26, "manifest bases");
        int checks = 1;
        foreach (Base b in manifest.bases)
        {
            ScriptableObject source = Required<ScriptableObject>(b.sourceAssetPath);
            OrderDefinitionNode node = RecipeHelper.GetOrderDefinitionNode(source);
            List<int> ids = new List<int>();
            foreach (AssembledDefinitionNode leaf in node.Convert())
            {
                IngredientAssembledNode ing = leaf as IngredientAssembledNode;
                Check(ing != null, "original source leaf: " + b.id);
                ids.Add(ing.m_ingriedientOrderNode.m_uID);
            }
            ids.Sort();
            Check(string.Join(",", ids.ConvertAll(delegate(int id) { return id.ToString(); }).ToArray()) == b.uidKey, "source UID composition: " + b.id);
            checks++;
            if (source is CustomRecipeSO) UnityEngine.Object.DestroyImmediate(node);
        }
        LevelInfoSO info = ScriptableObject.CreateInstance<LevelInfoSO>();
        try
        {
            info.recipes = finals.ToArray();
            SyncOptionals(info);
            int count = info.optionalRecipeMatchListItems.Length;
            SyncOptionals(info);
            Check(info.optionalRecipeMatchListItems.Length == count, "optional integration idempotent");
            Check(info.recipes.Length == 78, "optional anchors never become orders");
            int anchors = 0;
            HashSet<int> leafUids = new HashSet<int>();
            foreach (ScriptableObject so in info.optionalRecipeMatchListItems)
            {
                if (so is CustomRecipeSO) anchors++;
                else
                {
                    IngredientOrderNode leaf = RecipeHelper.GetIngredientOrderNode(so as PseudoPrefabSO);
                    Check(leaf != null, "registered leaf resolves");
                    leafUids.Add(leaf.m_uID);
                }
            }
            Check(anchors == 24 && leafUids.Count == 10, "24 optional anchors plus 8 vegetables and 2 canonical sauces");
            List<string> deps = new List<string>();
            AddDependencies(info, deps);
            foreach (string b in new string[] { "bundle18", "bundle47", "bundle427", "bundle428", "bundle354" }) Check(deps.Contains(b), "dependency " + b);
            info.recipes = new ScriptableObject[0];
            SyncOptionals(info);
            Check(!UsesSauces(info), "deselected sauce recipes remove export feature");
            foreach (ScriptableObject so in info.optionalRecipeMatchListItems)
                Check(so == null || !so.name.StartsWith(Prefix + "Optional_", StringComparison.Ordinal), "deselected optional anchors removed");
            deps.Clear(); AddDependencies(info, deps);
            Check(deps.Count == 0, "no sauce-only dependencies added without sauce recipes");
            checks += 11;
        }
        finally { UnityEngine.Object.DestroyImmediate(info); }
        return checks;
    }

    sealed class TestContents : IIngredientContents
    {
        readonly List<AssembledDefinitionNode> items = new List<AssembledDefinitionNode>();
        public bool CanAddIngredient(AssembledDefinitionNode node) { return true; }
        public bool CanTakeContents(AssembledDefinitionNode[] nodes) { return true; }
        public void AddIngredient(AssembledDefinitionNode node) { items.Add(node); }
        public AssembledDefinitionNode RemoveIngredient(int index) { AssembledDefinitionNode n = items[index]; items.RemoveAt(index); return n; }
        public AssembledDefinitionNode GetContentsElement(int index) { return items[index]; }
        public int GetContentsCount() { return items.Count; }
        public AssembledDefinitionNode[] GetContents() { return items.ToArray(); }
        public void Empty() { items.Clear(); }
        public bool HasContents() { return items.Count > 0; }
    }

    sealed class TestCarrier : ICarrier, ICarrierPlacement
    {
        public GameObject item;
        public void RegisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> callback) { }
        public void UnregisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> callback) { }
        public void CarryItem(GameObject value) { item = value; }
        public GameObject TakeItem() { GameObject value = item; item = null; return value; }
        public void DestroyCarriedItem() { item = null; }
        public GameObject InspectCarriedItem() { return item; }
        public GameObject AccessGameObject() { return item; }
    }

    static int ValidatePlacementPatches()
    {
        Type rules = Find("CustomStub.SaladSauceRuntime");
        MethodInfo activate = rules.GetMethod("SetActive", BindingFlags.Static | BindingFlags.NonPublic);
        bool active = (bool)rules.GetField("s_active", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        GameObject source = new GameObject("Salad sauce validation source");
        GameObject platePrefab = PseudoPrefabManager.LoadAsset(Required<PseudoPrefabSO>("Assets/common01/pseudo_prefab_so/utensils/PlateSO.asset"));
        GameObject plateObject = UnityEngine.Object.Instantiate(platePrefab);
        source.hideFlags = HideFlags.HideAndDontSave;
        plateObject.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            activate.Invoke(null, new object[] { true });
            IngredientPropertiesComponent properties = source.AddComponent<IngredientPropertiesComponent>();
            source.AddComponent<IngredientToContainerBehaviour>();
            Plate plate = plateObject.GetComponent<Plate>();
            Check(properties != null, "source IngredientPropertiesComponent");
            Check(plate != null, "test Plate component");
            // Prefix must reject BEFORE a real/native or temporary adapter changes.
            TestContents empty = new TestContents();
            IngredientOrderNode alias = RecipeHelper.GetIngredientOrderNode(Required<PseudoPrefabSO>("Assets/common03/food/Ingredients/dlc08/DLC08_Ketchup.asset"));
            IngredientOrderNode lettuce = RecipeHelper.GetIngredientOrderNode(Required<PseudoPrefabSO>("Assets/common01/food/Ingredients/LettuceSO.asset"));
            IngredientOrderNode red = RecipeHelper.GetIngredientOrderNode(Required<PseudoPrefabSO>("Assets/commonW1/pseudo_prefab_so/dlc11/food/dlc11_ketchup.asset"));
            IngredientOrderNode yellowAlias = RecipeHelper.GetIngredientOrderNode(Required<PseudoPrefabSO>("Assets/common03/food/Ingredients/dlc08/DLC08_Mustard.asset"));
            Check(alias.m_uID == 158482 && yellowAlias.m_uID == 17094, "alias UIDs");
            foreach (IngredientOrderNode sauce in new IngredientOrderNode[] { alias, red, yellowAlias })
            {
                properties.SetIngredientOrderNode(sauce);
                Check(!(bool)typeof(Plate).GetMethod("CanPlaceOnPlate").Invoke(plate, new object[] { source, empty }) && empty.GetContentsCount() == 0, "Plate prefix empty pre-transfer");
            }
            TestContents salad = new TestContents();
            salad.AddIngredient(lettuce.Convert());
            AssembledNodeTransfer.CombineWithContents(alias.Convert(), salad, true);
            Check(((IngredientAssembledNode)salad.GetContentsElement(1)).m_ingriedientOrderNode.m_uID == 890084, "DLC08 transfer normalization");
            Check(alias.m_uID == 158482, "source sauce node unchanged");
            TestContents actualTransfer = new TestContents(); actualTransfer.AddIngredient(lettuce.Convert());
            Check(AssembledNodeTransfer.CanCombineWithContents(alias.Convert(), actualTransfer), "native CanCombine accepts salad alias");
            AssembledNodeTransfer.CombineWithContents(alias.Convert(), actualTransfer, false);
            Check(((IngredientAssembledNode)actualTransfer.GetContentsElement(1)).m_ingriedientOrderNode.m_uID == 890084, "non-temporary server transfer normalizes UID");
            properties.SetIngredientOrderNode(alias);
            Check(!(bool)typeof(Plate).GetMethod("CanPlaceOnPlate").Invoke(plate, new object[] { source, salad }) && salad.GetContentsCount() == 2, "duplicate alias rejected before transfer");
            AssembledNodeTransfer.CombineWithContents(yellowAlias.Convert(), salad, true);
            Check(((IngredientAssembledNode)salad.GetContentsElement(2)).m_ingriedientOrderNode.m_uID == 890076, "second sauce normalization");
            TestContents reverse = new TestContents();
            reverse.AddIngredient(lettuce.Convert());
            AssembledNodeTransfer.CombineWithContents(yellowAlias.Convert(), reverse, true);
            AssembledNodeTransfer.CombineWithContents(alias.Convert(), reverse, true);
            Check(reverse.GetContentsCount() == 3, "reverse sauce order");
            CompositeAssembledNode nested = new CompositeAssembledNode();
            nested.m_composition = new AssembledDefinitionNode[] { lettuce.Convert(), alias.Convert() };
            TestContents target = new TestContents();
            AssembledNodeTransfer.CombineWithContents(nested, target, true);
            CompositeAssembledNode normalized = target.GetContentsElement(0) as CompositeAssembledNode;
            Check(normalized != null && !ReferenceEquals(normalized, nested), "nested alias copied");
            Check(((IngredientAssembledNode)normalized.m_composition[1]).m_ingriedientOrderNode.m_uID == 890084
                && ((IngredientAssembledNode)nested.m_composition[1]).m_ingriedientOrderNode.m_uID == 158482, "nested source remains unmodified");
            TestContents nativeTransfer = new TestContents();
            AssembledNodeTransfer.CombineWithContents(normalized, nativeTransfer, true);
            Check(ReferenceEquals(nativeTransfer.GetContentsElement(0), normalized), "canonical composite identity preserved");
            int nativeChecks = ValidateNativePlateAcceptance(plate, source, properties, lettuce, alias, yellowAlias);
            ServerPlacementContainer placement = plateObject.AddComponent<ServerPlacementContainer>();
            ServerIngredientContainer container = plateObject.AddComponent<ServerIngredientContainer>();
            Field(container.GetType(), "m_contents").SetValue(container, new List<AssembledDefinitionNode>());
            TestCarrier carrier = new TestCarrier(); carrier.item = source;
            Check(!placement.CanHandlePlacement(carrier, Vector2.zero, default(PlacementContext)), "server empty CanHandlePlacement guard");
            placement.HandlePlacement(carrier, Vector2.zero, default(PlacementContext));
            Check(container.GetContentsCount() == 0, "server empty HandlePlacement no-op");
            Field(container.GetType(), "m_contents").SetValue(container, new List<AssembledDefinitionNode>(salad.GetContents()));
            placement.HandlePlacement(carrier, Vector2.zero, default(PlacementContext));
            Check(container.GetContentsCount() == 3, "server duplicate HandlePlacement no-op");
            ServerPlacementContainer saucePlacement = source.AddComponent<ServerPlacementContainer>();
            properties.SetIngredientOrderNode(alias);
            carrier.item = plateObject;
            Check(!saucePlacement.CanHandlePlacement(carrier, Vector2.zero, default(PlacementContext)), "carried plate duplicate CanHandlePlacement guard");
            saucePlacement.HandlePlacement(carrier, Vector2.zero, default(PlacementContext));
            Check(container.GetContentsCount() == 3, "carried plate duplicate HandlePlacement no-op");
            // A non-salad ingredient bypasses the salad guard, preserving hotdog rules.
            IngredientOrderNode frank = ScriptableObject.CreateInstance<IngredientOrderNode>();
            try
            {
                frank.m_uID = 70664;
                TestContents hotdog = new TestContents(); hotdog.AddIngredient(frank.Convert());
                AssembledNodeTransfer.CombineWithContents(alias.Convert(), hotdog, true);
                Check(((IngredientAssembledNode)hotdog.GetContentsElement(1)).m_ingriedientOrderNode.m_uID == 158482, "hotdog sauce UID unchanged");
            }
            finally { UnityEngine.Object.DestroyImmediate(frank); }
            MethodInfo reject = rules.GetMethod("RejectContents");
            OrderDefinitionNode hotdogOrder = RecipeHelper.GetOrderDefinitionNode(Required<PseudoPrefabSORecipe>("Assets/common03/food/Recipes/dlc08/DLC08_Hotdog_Plain.asset"));
            Check(!(bool)reject.Invoke(null, new object[] { hotdogOrder.Convert(), alias.Convert() }), "native hotdog bypasses salad rejection");
            Check(!(bool)reject.Invoke(null, new object[] { hotdogOrder.Convert(), yellowAlias.Convert() }), "native hotdog mustard bypasses salad rejection");
            TestContents hotdogTarget = new TestContents(); hotdogTarget.AddIngredient(hotdogOrder.Convert());
            object[] hotdogArgs = new object[] { alias.Convert(), hotdogTarget };
            object hotdogSource = hotdogArgs[0];
            rules.GetMethod("BeforeCombine", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, hotdogArgs);
            Check(ReferenceEquals(hotdogArgs[0], hotdogSource), "native hotdog normalization preserves source identity");
            return 25 + nativeChecks;
        }
        finally
        {
            activate.Invoke(null, new object[] { active });
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(plateObject);
        }
    }

    static OrderDefinitionNode[] s_validationOrders;
    static AssembledDefinitionNode[] s_validationSimple;
    static bool ValidationOrders(ref OrderDefinitionNode[] __result) { __result = s_validationOrders; return false; }
    static bool ValidationSimple(ref AssembledDefinitionNode[] __result) { __result = s_validationSimple; return false; }
    static bool ValidationRejectPlate(ref bool __result) { __result = false; return false; }
    static bool ValidationMuteAudio() { return false; }

    static int ValidateFeatureGate(CustomRecipeSO recipe)
    {
        Type rules = Find("CustomStub.SaladSauceRuntime");
        BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        MethodInfo gate = rules.GetMethod("SetSceneActive", flags);
        MethodInfo combine = rules.GetMethod("BeforeCombine", flags);
        FieldInfo activeField = rules.GetField("s_active", flags);
        bool previous = (bool)activeField.GetValue(null);
        LevelInfoSO info = ScriptableObject.CreateInstance<LevelInfoSO>();
        IngredientOrderNode alias = RecipeHelper.GetIngredientOrderNode(Required<PseudoPrefabSO>("Assets/common03/food/Ingredients/dlc08/DLC08_Ketchup.asset"));
        IngredientOrderNode lettuce = RecipeHelper.GetIngredientOrderNode(Required<PseudoPrefabSO>("Assets/common01/food/Ingredients/LettuceSO.asset"));
        try
        {
            info.recipes = new ScriptableObject[] { null, recipe };
            gate.Invoke(null, new object[] { null, info });
            Check((bool)activeField.GetValue(null), "Play gate enables selected sauce salad");
            gate.Invoke(null, new object[] { new string[0], info });
            Check(!(bool)activeField.GetValue(null), "manifest without feature overrides selected recipe");
            TestContents target = new TestContents(); target.AddIngredient(lettuce.Convert());
            AssembledDefinitionNode source = alias.Convert();
            object[] args = new object[] { source, target };
            combine.Invoke(null, args);
            Check(ReferenceEquals(args[0], source), "disabled gate leaves alias unchanged");
            gate.Invoke(null, new object[] { new string[] { "SALADSAUCE" }, null });
            Check((bool)activeField.GetValue(null), "game manifest gate is case insensitive and independent of editor info");
            info.recipes = new ScriptableObject[] { null };
            gate.Invoke(null, new object[] { null, info });
            Check(!(bool)activeField.GetValue(null), "Play gate disables on next non-sauce level");
            gate.Invoke(null, new object[] { null, null });
            Check(!(bool)activeField.GetValue(null), "missing Play manager remains inactive");
            return 6;
        }
        finally
        {
            rules.GetMethod("SetActive", flags).Invoke(null, new object[] { previous });
            UnityEngine.Object.DestroyImmediate(info);
        }
    }

    static int ValidateMalformedNodes()
    {
        Type rules = Find("CustomStub.SaladSauceRuntime");
        MethodInfo combine = rules.GetMethod("BeforeCombine", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo activate = rules.GetMethod("SetActive", BindingFlags.Static | BindingFlags.NonPublic);
        bool active = (bool)rules.GetField("s_active", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        IngredientOrderNode alias = RecipeHelper.GetIngredientOrderNode(Required<PseudoPrefabSO>("Assets/common03/food/Ingredients/dlc08/DLC08_Ketchup.asset"));
        IngredientOrderNode lettuce = RecipeHelper.GetIngredientOrderNode(Required<PseudoPrefabSO>("Assets/common01/food/Ingredients/LettuceSO.asset"));
        try
        {
            activate.Invoke(null, new object[] { true });
            CompositeAssembledNode broken = new CompositeAssembledNode();
            broken.m_composition = null;
            CompositeAssembledNode nested = new CompositeAssembledNode();
            nested.m_composition = new AssembledDefinitionNode[] { lettuce.Convert(), alias.Convert(), broken };
            object[] args = new object[] { nested, new TestContents() };
            combine.Invoke(null, args);
            Check(ReferenceEquals(args[0], nested), "malformed tree bypasses normalization without mutation");
            nested.m_composition[2] = new IngredientAssembledNode();
            combine.Invoke(null, args);
            Check(ReferenceEquals(args[0], nested), "missing ingredient definition bypasses normalization");
            nested.m_composition[2] = null;
            combine.Invoke(null, args);
            Check(ReferenceEquals(args[0], nested), "null leaf bypasses normalization");
            return 3;
        }
        finally { activate.Invoke(null, new object[] { active }); }
    }

    static int ValidateNativePlateAcceptance(Plate plate, GameObject source, IngredientPropertiesComponent properties,
        IngredientOrderNode lettuce, IngredientOrderNode redAlias, IngredientOrderNode yellowAlias)
    {
        Harmony test = new Harmony("oc2.salad.isolated-validation");
        Harmony fallback = new Harmony("oc2.salad.validation-fallback");
        BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        MethodInfo orders = typeof(GameUtils).GetMethod("GetAllOrderNodes", flags);
        MethodInfo simple = typeof(GameUtils).GetMethod("GetAllOrderNodesSimplified", flags);
        FieldInfo last = typeof(GameUtils).GetField("s_LastMatchedOrderIndex", flags);
        object previousLast = last.GetValue(null);
        List<OrderDefinitionNode> allocated = new List<OrderDefinitionNode>();
        try
        {
            Type patches = Find("CustomStub.HarmonyPatches");
            fallback.Patch(typeof(Plate).GetMethod("CanPlaceOnPlate"),
                new HarmonyMethod(patches.GetMethod("CanPlaceOnPlateFallbackPrefix", flags)),
                new HarmonyMethod(patches.GetMethod("CanPlaceOnPlateFallbackPostfix", flags)));
            List<OrderDefinitionNode> list = new List<OrderDefinitionNode>();
            // Native no-sauce optional first models the mixed official/custom case.
            PseudoPrefabSO original = ScriptableObject.CreateInstance<PseudoPrefabSO>();
            try
            {
                original.bundleName = "bundle18";
                original.assetPath = "Assets/data/orderdefinitions/recipeitems/salads/optionalsalad.asset";
                list.Add(PseudoPrefabManager.LoadAsset<OrderDefinitionNode>(original));
            }
            finally { UnityEngine.Object.DestroyImmediate(original); }
            Check(list[0] != null, "official no-sauce optional exists");
            foreach (string g in AssetDatabase.FindAssets("t:CustomRecipeSO", new string[] { Root.TrimEnd('/') }))
            {
                CustomRecipeSO r = Required<CustomRecipeSO>(AssetDatabase.GUIDToAssetPath(g));
                if (r.modelSO == null || r.modelSO.name.IndexOf("Story", StringComparison.Ordinal) < 0) continue;
                OrderDefinitionNode node = RecipeHelper.GetOrderDefinitionNode(r);
                allocated.Add(node); list.Add(node);
            }
            // Mirror the level's UID registration. Native Deserialise must resolve
            // canonical sauces even though a dispenser supplied a DLC08 alias.
            LevelInfoSO info = ScriptableObject.CreateInstance<LevelInfoSO>();
            try
            {
                info.recipes = new ScriptableObject[] { Required<CustomRecipeSO>(Root + Prefix + "Salad_Plain_SO_Ketchup.asset") };
                SyncOptionals(info);
                foreach (ScriptableObject so in info.optionalRecipeMatchListItems)
                {
                    PseudoPrefabSO leaf = so as PseudoPrefabSO;
                    if (leaf != null) list.Add(RecipeHelper.GetIngredientOrderNode(leaf));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(info); }
            s_validationOrders = list.ToArray();
            s_validationSimple = list.ConvertAll(delegate(OrderDefinitionNode n) { return n.Convert().Simpilfy(); }).ToArray();
            test.Patch(orders, new HarmonyMethod(typeof(LayoutEditorSaladSauceFactory).GetMethod("ValidationOrders", flags)));
            test.Patch(simple, new HarmonyMethod(typeof(LayoutEditorSaladSauceFactory).GetMethod("ValidationSimple", flags)));
            TestContents contents = new TestContents();
            contents.AddIngredient(lettuce.Convert());
            last.SetValue(null, 0);
            properties.SetIngredientOrderNode(redAlias);
            Check(plate.CanPlaceOnPlate(source, contents), "actual Plate accepts partial lettuce + red");
            Check(contents.GetContentsCount() == 2, "accepted sauce transfers to temporary adapter");
            IngredientOrderNode tomato = RecipeHelper.GetIngredientOrderNode(Required<PseudoPrefabSO>("Assets/common01/food/Ingredients/TomatoSO.asset"));
            properties.SetIngredientOrderNode(tomato);
            Check(plate.CanPlaceOnPlate(source, contents), "actual Plate adds vegetable after sauce");
            properties.SetIngredientOrderNode(yellowAlias);
            Check(plate.CanPlaceOnPlate(source, contents), "actual Plate accepts second sauce");
            Check(!plate.CanPlaceOnPlate(source, contents) && contents.GetContentsCount() == 4, "actual Plate rejects duplicate without adapter mutation");
            GameObject model = GameUtils.GetOrderPlatingPrefab(plate.GetOrderComposition(contents.GetContents()), plate.m_platingStep);
            Check(model != null && model.name == Prefix + "Story_Model", "sauce carrier wins over original no-sauce optional");
            TestContents reverse = new TestContents(); reverse.AddIngredient(lettuce.Convert());
            properties.SetIngredientOrderNode(yellowAlias);
            Check(plate.CanPlaceOnPlate(source, reverse), "actual Plate yellow first");
            properties.SetIngredientOrderNode(redAlias);
            Check(plate.CanPlaceOnPlate(source, reverse), "actual Plate red second");
            Check(!plate.CanPlaceOnPlate(source, reverse) && reverse.GetContentsCount() == 3, "actual Plate duplicate red");
            Harmony rejectFirst = new Harmony("oc2.salad.validation-reject-first");
            MethodInfo canPlace = typeof(Plate).GetMethod("CanPlaceOnPlate");
            try
            {
                HarmonyMethod prefix = new HarmonyMethod(typeof(LayoutEditorSaladSauceFactory).GetMethod("ValidationRejectPlate", flags));
                prefix.priority = Priority.First;
                rejectFirst.Patch(canPlace, prefix);
                TestContents untouched = new TestContents(); untouched.AddIngredient(lettuce.Convert());
                Check((bool)Find("CustomStub.PlatePartialOrderFallback").GetMethod("CanStillFormOrder").Invoke(null,
                    new object[] { plate.GetOrderComposition(untouched.GetContents()), plate.m_platingStep }), "fallback fixture could accept untouched partial salad");
                Check(!plate.CanPlaceOnPlate(source, untouched) && untouched.GetContentsCount() == 1, "fallback must preserve rejection when its snapshot prefix is skipped");
            }
            finally { rejectFirst.UnpatchSelf(); }
            CompositeAssembledNode sent = plate.GetOrderComposition(reverse.GetContents()) as CompositeAssembledNode;
            System.Collections.Generic.FastList<byte> buffer = new System.Collections.Generic.FastList<byte>(256);
            BitStream.BitStreamWriter writer = new BitStream.BitStreamWriter(buffer);
            sent.Serialise(writer);
            CompositeAssembledNode received = new CompositeAssembledNode();
            Check(received.Deserialise(new BitStream.BitStreamReader(buffer._items, (uint)writer.GetUsedBitCount())), "native network deserialization");
            Check(received.m_composition.Length == 3 && AssembledDefinitionNode.Matching(sent, received), "network roundtrip preserves partial salad and canonical sauce UIDs");
            Check(GameUtils.GetOrderPlatingPrefab(received, plate.m_platingStep) == model, "deserialized partial salad selects same carrier");
            return 15;
        }
        finally
        {
            test.UnpatchSelf();
            fallback.UnpatchSelf();
            last.SetValue(null, previousLast);
            s_validationOrders = null; s_validationSimple = null;
            foreach (OrderDefinitionNode node in allocated) UnityEngine.Object.DestroyImmediate(node);
        }
    }
}
