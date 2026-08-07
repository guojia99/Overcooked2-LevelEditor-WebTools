using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class LayoutEditorLevelAdminApi
{
    private const string LevelSetsRoot = "Assets/LevelSets";
    private const string TemplateScene = "Assets/Template/s_template.unity";
    private const string TemplateConfig1p = "Assets/Template/config_1p.asset";
    private const string TemplateConfig2p = "Assets/Template/config_2p.asset";
    private const string TemplateConfig3p = "Assets/Template/config_3p.asset";
    private const string TemplateConfig4p = "Assets/Template/config_4p.asset";
    private const string ManagerScriptGuid = "851e97368ab5cac468f2317c10d7f6c7";

    // ==================== Catalogs ====================

    public static MusicCatalogDto ScanMusic()
    {
        var list = new List<MusicEntryDto>();
        var folders = new[] { "Assets/common01/pseudo_prefab_so/audio/music", "Assets/common02/pseudo_prefab_so/audio/music" };
        list.AddRange(ScanPseudoPrefabs(folders));
        var dto = new MusicCatalogDto { music = list.ToArray() };
        return dto;
    }

    public static AudioDirectoryCatalogDto ScanAudioDirectories()
    {
        // Scan both common01 (the *SO.asset variants that scenes actually reference, incl.
        // the 5 mandatory directories) and common02 (the non-SO variants). Mirrors build-catalog.mjs.
        var raw = ScanPseudoPrefabs(new[]
        {
            "Assets/common01/pseudo_prefab_so/audio/AudioDirectories",
            "Assets/common02/pseudo_prefab_so/audio/AudioDirectories"
        });
        var list = new List<AudioDirectoryEntryDto>();
        foreach (var m in raw)
            list.Add(new AudioDirectoryEntryDto { guid = m.guid, id = m.id, assetPath = m.assetPath, bundleName = m.bundleName, nameZh = m.nameZh });

        var k = LoadAudioKnowledge();
        return new AudioDirectoryCatalogDto
        {
            audioDirectories = list.ToArray(),
            baseBundles = k.baseBundles,
            alwaysLoadedBundles = k.alwaysLoadedBundles,
            mandatoryDirectoryIds = k.mandatoryDirectoryIds,
            directoryEvents = k.directoryEvents,
            themes = k.themes,
            deathThemes = k.deathThemes,
            ambienceLabels = k.ambienceLabels,
            itemAudioRules = k.itemAudioRules
        };
    }

    public static AmbienceCatalogDto ScanAmbiences()
    {
        var names = new List<string>(Enum.GetNames(typeof(PseudoPrefabManagerStub.GameLoopingAudioTag)));
        names.RemoveAll(n => n == "COUNT");
        var k = LoadAudioKnowledge();
        return new AmbienceCatalogDto
        {
            ambiences = names.ToArray(),
            ambienceLabels = k.ambienceLabels
        };
    }

    public static DeathEffectCatalogDto ScanDeathEffects()
    {
        var list = new List<DeathEffectEntryDto>();
        var roots = new[] { "Assets/common01/pseudo_prefab_so", "Assets/common02/pseudo_prefab_so" };
        var seen = new HashSet<string>();
        foreach (var root in roots)
        {
            if (!AssetDatabase.IsValidFolder(root))
                continue;
            foreach (var guid in AssetDatabase.FindAssets("t:PseudoPrefabSO", new[] { root }))
            {
                if (!seen.Add(guid))
                    continue;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var id = Path.GetFileNameWithoutExtension(path);
                if (!id.Contains("WaterSplash") && !id.Contains("DeathEffect"))
                    continue;
                list.Add(new DeathEffectEntryDto { guid = guid, id = id, assetPath = path, nameZh = id });
            }
        }
        return new DeathEffectCatalogDto { deathEffects = list.ToArray() };
    }

    private static List<MusicEntryDto> ScanPseudoPrefabs(string[] folders)
    {
        var list = new List<MusicEntryDto>();
        var seen = new HashSet<string>();
        foreach (var folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                continue;
            foreach (var guid in AssetDatabase.FindAssets("t:PseudoPrefabSO", new[] { folder }))
            {
                if (!seen.Add(guid))
                    continue;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
                var id = Path.GetFileNameWithoutExtension(path);
                string nameZh;
                string nameEn;
                LayoutEditorManualLookup.TryGet(id, out nameZh, out nameEn);
                if (string.IsNullOrEmpty(nameZh))
                    nameZh = so != null ? so.prefabName : id;
                list.Add(new MusicEntryDto
                {
                    guid = guid,
                    id = id,
                    assetPath = path,
                    bundleName = so != null ? so.bundleName : "",
                    nameZh = nameZh
                });
            }
        }
        list.Sort((a, b) => string.Compare(a.id, b.id, StringComparison.Ordinal));
        return list;
    }

    // ==================== Audio knowledge (shared JSON) ====================

    private static AudioDirectoryCatalogDto _audioKnowledge;
    private static bool _audioKnowledgeLoaded;

    /// <summary>Reload audio-knowledge.json (e.g. after editing the shared data file).</summary>
    public static void InvalidateAudioKnowledgeCache()
    {
        _audioKnowledgeLoaded = false;
    }

    /// <summary>Returns the shared audio knowledge (mandatory dirs, event legend, theme matrix,
    /// death themes, base/always-loaded bundles). Empty arrays when the JSON is missing.</summary>
    public static AudioDirectoryCatalogDto LoadAudioKnowledge()
    {
        if (_audioKnowledgeLoaded)
            return _audioKnowledge ?? (_audioKnowledge = EmptyKnowledge());
        _audioKnowledgeLoaded = true;

        var kPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../layout-editor/scripts/data/audio-knowledge.json"));
        string text = null;
        if (File.Exists(kPath))
        {
            try { text = File.ReadAllText(kPath); } catch { }
        }
        if (string.IsNullOrEmpty(text))
        {
            _audioKnowledge = EmptyKnowledge();
            return _audioKnowledge;
        }

        try
        {
            var dto = JsonUtility.FromJson<AudioDirectoryCatalogDto>(text);
            _audioKnowledge = dto ?? EmptyKnowledge();
        }
        catch
        {
            _audioKnowledge = EmptyKnowledge();
        }
        return _audioKnowledge;
    }

    private static AudioDirectoryCatalogDto EmptyKnowledge()
    {
        return new AudioDirectoryCatalogDto
        {
            audioDirectories = new AudioDirectoryEntryDto[0],
            baseBundles = new string[0],
            alwaysLoadedBundles = new string[0],
            mandatoryDirectoryIds = new string[0],
            directoryEvents = new DirectoryEventDto[0],
            themes = new AudioThemeDto[0],
            deathThemes = new AudioDeathThemeDto[0],
            ambienceLabels = new AmbienceLabelDto[0],
            itemAudioRules = new AudioItemRuleDto[0]
        };
    }

    // ==================== Sets ====================

    public static LevelSetListDto ScanSets()
    {
        var list = new List<LevelSetInfoDto>();
        if (!AssetDatabase.IsValidFolder(LevelSetsRoot))
            return new LevelSetListDto { sets = new LevelSetInfoDto[0] };

        foreach (var guid in AssetDatabase.FindAssets("t:LevelSetInfoSO", new[] { LevelSetsRoot }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<LevelSetInfoSO>(path);
            if (so == null)
                continue;
            var setName = SetNameFromPath(path);
            if (string.IsNullOrEmpty(setName))
                continue;
            list.Add(new LevelSetInfoDto
            {
                setName = setName,
                assetPath = path,
                dataDir = DirectoryName(path),
                levelSetName = so.levelSetName ?? "",
                levelSetNameZH = so.levelSetNameZH ?? "",
                author = so.author ?? "",
                version = so.version ?? "",
                uid = so.uid ?? "",
                levelCount = so.levelInfos != null ? so.levelInfos.Length : 0
            });
        }

        list.Sort((a, b) => string.Compare(a.setName, b.setName, StringComparison.Ordinal));
        return new LevelSetListDto { sets = list.ToArray() };
    }

    public static string CreateSet(LevelSetCreateDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.setName))
            return "缺少关卡集标识。";
        var setName = SanitizeName(dto.setName);
        if (string.IsNullOrEmpty(setName))
            return "关卡集标识只能包含字母数字和下划线。";

        var setDir = LevelSetsRoot + "/" + setName;
        if (AssetDatabase.IsValidFolder(setDir))
            return "关卡集已存在：" + setName;

        if (!AssetDatabase.IsValidFolder(LevelSetsRoot))
            AssetDatabase.CreateFolder("Assets", "LevelSets");
        AssetDatabase.CreateFolder(LevelSetsRoot, setName);
        AssetDatabase.CreateFolder(setDir, "data");
        AssetDatabase.CreateFolder(setDir, "scenes");
        AssetDatabase.CreateFolder(setDir, "custom_recipes");

        var so = ScriptableObject.CreateInstance<LevelSetInfoSO>();
        so.levelSetName = dto.levelSetName ?? setName;
        so.levelSetNameZH = dto.levelSetNameZH ?? setName;
        so.author = dto.author ?? "";
        so.version = "0.1";
        so.levelInfos = new LevelInfoSO[0];
        RefreshSetUid(so, true);
        AssetDatabase.CreateAsset(so, setDir + "/data/LevelSetInfo.asset");
        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SetAssetBundleName(setDir, setName + "/info_" + setName);
        return null;
    }

    /** Permanently delete a level set folder (and all its levels/scenes/assets).
     *  The web UI requires the user to type the setName to confirm. */
    public static string DeleteSet(string setName)
    {
        if (string.IsNullOrEmpty(setName))
            return "缺少关卡集标识。";
        var safe = SanitizeName(setName);
        if (string.IsNullOrEmpty(safe) || safe != setName)
            return "关卡集标识非法。";
        var setDir = LevelSetsRoot + "/" + safe;
        if (!AssetDatabase.IsValidFolder(setDir))
            return "关卡集不存在：" + safe;

        // Clear asset-bundle names on the folder's assets first so the bundle
        // manifest does not keep stale references after the folder is gone.
        foreach (var guid in AssetDatabase.FindAssets("", new[] { setDir }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(p);
            if (imp != null && !string.IsNullOrEmpty(imp.assetBundleName))
            {
                imp.SetAssetBundleNameAndVariant("", "");
                imp.SaveAndReimport();
            }
        }

        if (!AssetDatabase.DeleteAsset(setDir))
            return "删除失败（文件夹可能被占用，请关闭相关场景后重试）。";
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return null;
    }

    private static void SetAssetBundleName(string assetPath, string bundleName)
    {
        var importer = AssetImporter.GetAtPath(assetPath);
        if (importer != null && importer.assetBundleName != bundleName)
        {
            importer.assetBundleName = bundleName;
            importer.SaveAndReimport();
        }
    }

    public static string UpdateSetInfo(LevelSetInfoUpdateDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.setName))
            return "缺少关卡集标识。";
        var so = FindSetInfo(dto.setName);
        if (so == null)
            return "未找到关卡集：" + dto.setName;

        Undo.RecordObject(so, "Edit LevelSet Info");
        so.levelSetName = dto.levelSetName ?? so.levelSetName;
        so.levelSetNameZH = dto.levelSetNameZH ?? so.levelSetNameZH;
        so.author = dto.author ?? so.author;
        if (!string.IsNullOrEmpty(dto.version))
            so.version = dto.version;
        RefreshSetUid(so, false);
        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
        ReloadPseudo();
        return null;
    }

    private static void RefreshSetUid(LevelSetInfoSO so, bool newBase)
    {
        try
        {
            var t = so.GetType();
            var baseField = t.GetField("baseUID", BindingFlags.NonPublic | BindingFlags.Instance);
            if (baseField != null)
            {
                var current = baseField.GetValue(so) as string;
                if (newBase || string.IsNullOrEmpty(current))
                    baseField.SetValue(so, System.Guid.NewGuid().ToString());
            }
            var refresh = t.GetMethod("RefreshUID", BindingFlags.NonPublic | BindingFlags.Instance);
            if (refresh != null)
                refresh.Invoke(so, null);
        }
        catch
        {
        }
    }

    // ==================== Levels (list / detail) ====================

    public static LevelListDto ScanLevels(string setName)
    {
        var list = new List<LevelSummaryDto>();
        if (string.IsNullOrEmpty(setName))
            return new LevelListDto { levels = new LevelSummaryDto[0] };

        var dataDir = LevelSetsRoot + "/" + setName + "/data";
        if (!AssetDatabase.IsValidFolder(dataDir))
            return new LevelListDto { levels = new LevelSummaryDto[0] };

        foreach (var guid in AssetDatabase.FindAssets("t:LevelInfoSO", new[] { dataDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(path);
            if (so == null)
                continue;
            var sceneName = !string.IsNullOrEmpty(so.sceneName) ? so.sceneName : ("s_" + setName);
            var sceneAssetPath = LevelSetsRoot + "/" + setName + "/scenes/" + sceneName + ".unity";
            list.Add(new LevelSummaryDto
            {
                assetPath = path,
                dataDir = DirectoryName(path),
                levelName = so.levelName ?? "",
                levelNameZH = so.levelNameZH ?? "",
                sceneName = sceneName,
                sceneAssetPath = sceneAssetPath,
                hasScreenshot = so.screenshot != null,
                screenshotPath = so.screenshot != null ? AssetDatabase.GetAssetPath(so.screenshot) : "",
                hasScene = File.Exists(AbsPath(sceneAssetPath))
            });
        }

        list.Sort((a, b) => string.Compare(a.levelName, b.levelName, StringComparison.Ordinal));
        return new LevelListDto { levels = list.ToArray() };
    }

    public static LevelDetailDto GetLevel(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;
        var so = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(assetPath);
        if (so == null)
            return null;

        var setName = SetNameFromPath(assetPath);
        var sceneName = !string.IsNullOrEmpty(so.sceneName) ? so.sceneName : "";
        var sceneAssetPath = "";
        if (!string.IsNullOrEmpty(sceneName))
            sceneAssetPath = LevelSetsRoot + "/" + setName + "/scenes/" + sceneName + ".unity";

        var dto = new LevelDetailDto
        {
            levelInfoAssetPath = assetPath,
            levelName = so.levelName ?? "",
            levelNameZH = so.levelNameZH ?? "",
            sceneName = sceneName,
            sceneAssetPath = sceneAssetPath,
            hasScreenshot = so.screenshot != null,
            screenshotPath = so.screenshot != null ? AssetDatabase.GetAssetPath(so.screenshot) : "",
            debugRecipeCount = so.debugRecipeCount,
            disableDynamicParenting = so.disableDynamicParenting,
            dependencies = so.dependencies != null ? (string[])so.dependencies.Clone() : new string[0],
            configs = new[]
            {
                ConfigToDto(so.config_1p),
                ConfigToDto(so.config_2p),
                ConfigToDto(so.config_3p),
                ConfigToDto(so.config_4p)
            },
            audio = !string.IsNullOrEmpty(sceneAssetPath) && File.Exists(AbsPath(sceneAssetPath))
                ? ReadAudioFromScene(sceneAssetPath)
                : new AudioConfigDto { ambiences = new string[0], audioDirectoryGuids = new string[0], audioDirectoryIds = new string[0] }
        };
        return dto;
    }

    // ==================== Bundle dependency analysis ====================

    private static readonly HashSet<string> PseudoScriptGuids = new HashSet<string>
    {
        "0cff7c13895ab9e47a5e02d4619cc3b9", // PseudoPrefabSO
        "753d9e70603f6a140b05f30f176ec2dd", // PseudoPrefabSORecipe
        "83fb008bcc8e793429b02c178c430815", // CustomRecipeSO
        "60297950c88d0d646ac0eca5dc831262"  // CustomRecipeOptionalPizzaSO
    };

    private static Dictionary<string, List<string>> _bundleManifest;
    private static bool _bundleManifestLoaded;

    /// <summary>Loads the AssetBundle dependency graph (layout-editor/scripts/data/bundle-manifest.json,
    /// extracted from the game's Windows AssetBundleManifest). Empty graph when the file is missing.</summary>
    public static Dictionary<string, List<string>> LoadBundleManifest()
    {
        if (_bundleManifestLoaded)
            return _bundleManifest ?? (_bundleManifest = new Dictionary<string, List<string>>(StringComparer.Ordinal));
        _bundleManifestLoaded = true;
        var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../layout-editor/scripts/data/bundle-manifest.json"));
        string text = null;
        if (File.Exists(path))
        {
            try { text = File.ReadAllText(path); } catch { }
        }
        _bundleManifest = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(text))
            return _bundleManifest;
        try
        {
            var dto = JsonUtility.FromJson<BundleManifestDto>(text);
            if (dto != null && dto.dependencies != null)
            {
                foreach (var e in dto.dependencies)
                {
                    if (e == null || string.IsNullOrEmpty(e.name))
                        continue;
                    _bundleManifest[e.name] = new List<string>(e.deps ?? new string[0]);
                }
            }
        }
        catch
        {
            // leave empty graph
        }
        return _bundleManifest;
    }

    /// <summary>Transitive closure of a set of bundle names following the manifest's dependency edges
    /// (LoadAssetBundle loads each bundle's GetAllDependencies recursively, so this mirrors what the
    /// editor/game actually loads for the given declared dependencies).</summary>
    private static HashSet<string> BundleClosure(IEnumerable<string> seeds)
    {
        var manifest = LoadBundleManifest();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        foreach (var s in seeds)
            if (!string.IsNullOrEmpty(s))
                stack.Push(s);
        while (stack.Count > 0)
        {
            var b = stack.Pop();
            if (!seen.Add(b))
                continue;
            List<string> ds;
            if (manifest.TryGetValue(b, out ds) && ds != null)
                foreach (var d in ds)
                    if (!seen.Contains(d))
                        stack.Push(d);
        }
        return seen;
    }

    /// <summary>Scans ALL PseudoPrefabSO references in a level (LevelInfoSO recipe/ingredient/
    /// cooking-step/match-list arrays + every asset referenced by the scene) and reconciles against
    /// the TRANSITIVE closure of the declared LevelInfoSO.dependencies. A bundle is only "missing"
    /// (genuinely must-add) if the level references it directly and it is NOT reachable from the
    /// declared dependencies. baseBundles are always required; alwaysLoadedBundles are never flagged.</summary>
    public static BundleAnalysisDto AnalyzeBundles(string assetPath)
    {
        var k = LoadAudioKnowledge();
        var always = new HashSet<string>(k.alwaysLoadedBundles ?? new string[0], StringComparer.Ordinal);
        var baseBundles = new HashSet<string>(k.baseBundles ?? new string[0], StringComparer.Ordinal);
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in k.baseBundles ?? new string[0])
            referenced.Add(b);

        var current = new List<string>();
        var so = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<LevelInfoSO>(assetPath);
        if (so != null)
        {
            if (so.dependencies != null)
                current.AddRange(so.dependencies);
            CollectBundleNames(so.recipes, referenced);
            CollectBundleNames(so.allIngredients, referenced);
            CollectBundleNames(so.allCookingSteps, referenced);
            CollectBundleNames(so.includeRecipeMatchLists, referenced);
            CollectBundleNames(so.optionalRecipeMatchListItems, referenced);

            if (!string.IsNullOrEmpty(so.sceneName))
            {
                var setName = SetNameFromPath(assetPath);
                var scenePath = LevelSetsRoot + "/" + setName + "/scenes/" + so.sceneName + ".unity";
                if (File.Exists(AbsPath(scenePath)))
                    CollectSceneBundleNames(scenePath, referenced);
            }
        }

        referenced.RemoveWhere(b => always.Contains(b) || string.IsNullOrEmpty(b));

        // What actually loads = transitive closure of the declared dependencies (LoadAssetBundle
        // pulls each bundle's GetAllDependencies recursively). A bundle is only "missing" if the
        // level references it AND it is not reachable from the declared dependencies.
        var loaded = BundleClosure(current);

        var missing = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in referenced)
            if (!loaded.Contains(b))
                missing.Add(b);

        // A declared (non-base) dependency is "extra" if removing it still keeps every referenced
        // bundle within the closure — i.e. nothing the level uses actually needs it.
        var declaredDistinct = new List<string>();
        var seenD = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in current)
        {
            if (string.IsNullOrEmpty(b) || !seenD.Add(b))
                continue;
            declaredDistinct.Add(b);
        }
        var extras = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in declaredDistinct)
        {
            if (baseBundles.Contains(d))
                continue;
            var without = new List<string>(declaredDistinct);
            without.Remove(d);
            var reclo = BundleClosure(without);
            bool stillCovers = true;
            foreach (var b in referenced)
                if (!reclo.Contains(b)) { stillCovers = false; break; }
            if (stillCovers)
                extras.Add(d);
        }

        return new BundleAnalysisDto
        {
            @base = (k.baseBundles ?? new string[0]),
            alwaysLoaded = (k.alwaysLoadedBundles ?? new string[0]),
            required = SortedArray(referenced),
            current = DedupedArray(current),
            missing = SortedArray(missing),
            extras = SortedArray(extras)
        };
    }

    private static void CollectBundleNames(System.Collections.IList refs, HashSet<string> set)
    {
        if (refs == null)
            return;
        foreach (var obj in refs)
        {
            if (obj == null)
                continue;
            var bn = ReadBundleNameField(obj);
            if (!string.IsNullOrEmpty(bn))
                set.Add(bn);
        }
    }

    private static string ReadBundleNameField(object obj)
    {
        if (obj == null)
            return null;
        var f = obj.GetType().GetField("bundleName", BindingFlags.Public | BindingFlags.Instance);
        return f != null ? f.GetValue(obj) as string : null;
    }

    private static void CollectSceneBundleNames(string sceneAssetPath, HashSet<string> set)
    {
        string text;
        try { text = File.ReadAllText(AbsPath(sceneAssetPath)); }
        catch { return; }
        if (string.IsNullOrEmpty(text))
            return;

        var guids = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(text, @"guid:\s*([a-f0-9]{32})"))
            guids.Add(m.Groups[1].Value);

        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (string.IsNullOrEmpty(p) || !p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                continue;
            var bn = BundleNameFromAsset(p);
            if (!string.IsNullOrEmpty(bn))
                set.Add(bn);
        }
    }

    private static string BundleNameFromAsset(string assetPath)
    {
        string text;
        try { text = File.ReadAllText(AbsPath(assetPath)); }
        catch { return null; }
        if (string.IsNullOrEmpty(text))
            return null;

        bool isPseudo = false;
        foreach (var g in PseudoScriptGuids)
            if (text.IndexOf("guid: " + g, StringComparison.Ordinal) >= 0) { isPseudo = true; break; }
        if (!isPseudo)
            return null;

        var m = Regex.Match(text, @"bundleName:\s*(\S*)");
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string[] SortedArray(HashSet<string> set)
    {
        var list = new List<string>(set);
        list.Sort(StringComparer.Ordinal);
        return list.ToArray();
    }

    private static string[] DedupedArray(List<string> list)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var b in list)
            if (!string.IsNullOrEmpty(b) && seen.Add(b))
                result.Add(b);
        return result.ToArray();
    }

    // ==================== Levels (create / update / delete) ====================

    public static string CreateLevel(LevelCreateDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.setName) || string.IsNullOrEmpty(dto.levelId))
            return "缺少关卡集或关卡标识。";
        var setName = dto.setName;
        var levelId = SanitizeName(dto.levelId);
        if (string.IsNullOrEmpty(levelId))
            return "关卡标识只能包含字母数字和下划线。";

        var setDir = LevelSetsRoot + "/" + setName;
        if (!AssetDatabase.IsValidFolder(setDir))
            return "关卡集不存在：" + setName;

        var levelDataDir = setDir + "/data/" + levelId;
        if (AssetDatabase.IsValidFolder(levelDataDir))
            return "关卡已存在：" + levelId;

        var setInfo = FindSetInfo(setName);
        if (setInfo == null)
            return "未找到关卡集配置 LevelSetInfoSO。";

        AssetDatabase.CreateFolder(setDir + "/data", levelId);

        var c1Path = levelDataDir + "/config_1p.asset";
        var c2Path = levelDataDir + "/config_2p.asset";
        var c3Path = levelDataDir + "/config_3p.asset";
        var c4Path = levelDataDir + "/config_4p.asset";
        AssetDatabase.CopyAsset(TemplateConfig1p, c1Path);
        AssetDatabase.CopyAsset(TemplateConfig2p, c2Path);
        AssetDatabase.CopyAsset(TemplateConfig3p, c3Path);
        AssetDatabase.CopyAsset(TemplateConfig4p, c4Path);

        var config1 = AssetDatabase.LoadAssetAtPath<LevelConfigSetupPerPlayerCountSO>(c1Path);
        var config2 = AssetDatabase.LoadAssetAtPath<LevelConfigSetupPerPlayerCountSO>(c2Path);
        var config3 = AssetDatabase.LoadAssetAtPath<LevelConfigSetupPerPlayerCountSO>(c3Path);
        var config4 = AssetDatabase.LoadAssetAtPath<LevelConfigSetupPerPlayerCountSO>(c4Path);

        var sceneName = "s_" + levelId;
        var info = ScriptableObject.CreateInstance<LevelInfoSO>();
        info.levelName = !string.IsNullOrEmpty(dto.levelName) ? dto.levelName : levelId;
        info.levelNameZH = !string.IsNullOrEmpty(dto.levelNameZH) ? dto.levelNameZH : levelId;
        info.sceneName = sceneName;
        info.recipes = new ScriptableObject[0];
        info.debugRecipeCount = 0;
        info.disableDynamicParenting = true;
        info.config_1p = config1;
        info.config_2p = config2;
        info.config_3p = config3;
        info.config_4p = config4;
        info.dependencies = new[] { "bundle47" };
        var infoPath = levelDataDir + "/LevelInfo_" + levelId + ".asset";
        AssetDatabase.CreateAsset(info, infoPath);

        var scenePath = setDir + "/scenes/" + sceneName + ".unity";
        if (File.Exists(AbsPath(TemplateScene)))
        {
            AssetDatabase.CopyAsset(TemplateScene, scenePath);
            var prevActive = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(scenePath);
            ForcePrepareForBuilding();
            var stub = UnityEngine.Object.FindObjectOfType<PseudoPrefabManagerStub>();
            if (stub != null)
            {
                Undo.RecordObject(stub, "Bind new LevelInfo");
                stub.levelInfo = info;
                EditorUtility.SetDirty(stub);
            }
            var newScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(newScene);
            EditorSceneManager.SaveScene(newScene);
            if (!string.IsNullOrEmpty(prevActive) && File.Exists(AbsPath(prevActive)))
                EditorSceneManager.OpenScene(prevActive);
            SetAssetBundleName(scenePath, setName + "/" + sceneName);
        }

        var setInfoFresh = FindSetInfo(setName);
        var infoFresh = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(infoPath);
        if (setInfoFresh != null && infoFresh != null)
        {
            var newInfos = new List<LevelInfoSO>(setInfoFresh.levelInfos ?? new LevelInfoSO[0]) { infoFresh };
            Undo.RecordObject(setInfoFresh, "Add Level");
            setInfoFresh.levelInfos = newInfos.ToArray();
            EditorUtility.SetDirty(setInfoFresh);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ReloadPseudo();
        return null;
    }

    public static string UpdateLevelInfo(LevelInfoUpdateDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.assetPath))
            return "缺少关卡资源路径。";
        var so = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(dto.assetPath);
        if (so == null)
            return "未找到 LevelInfoSO。";

        Undo.RecordObject(so, "Edit Level Info");
        so.levelName = dto.levelName ?? so.levelName;
        so.levelNameZH = dto.levelNameZH ?? so.levelNameZH;
        if (!string.IsNullOrEmpty(dto.sceneName))
        {
            so.sceneName = dto.sceneName;
            var setName = SetNameFromPath(dto.assetPath);
            var scenePath = LevelSetsRoot + "/" + setName + "/scenes/" + dto.sceneName + ".unity";
            if (File.Exists(AbsPath(scenePath)))
                SetAssetBundleName(scenePath, setName + "/" + dto.sceneName);
        }
        so.debugRecipeCount = dto.debugRecipeCount;
        so.disableDynamicParenting = dto.disableDynamicParenting;
        so.dependencies = dto.dependencies != null ? (string[])dto.dependencies.Clone() : so.dependencies;
        EditorUtility.SetDirty(so);

        var stub = UnityEngine.Object.FindObjectOfType<PseudoPrefabManagerStub>();
        if (stub != null && stub.levelInfo == so)
            EditorUtility.SetDirty(stub);

        AssetDatabase.SaveAssets();
        ReloadPseudo();
        return null;
    }

    public static string UpdateLevelConfig(LevelConfigUpdateDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.assetPath))
            return "缺少关卡资源路径。";
        var so = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(dto.assetPath);
        if (so == null)
            return "未找到 LevelInfoSO。";

        ApplyConfig(dto.config_1p, so.config_1p, "1P");
        ApplyConfig(dto.config_2p, so.config_2p, "2P");
        ApplyConfig(dto.config_3p, so.config_3p, "3P");
        ApplyConfig(dto.config_4p, so.config_4p, "4P");

        AssetDatabase.SaveAssets();
        ReloadPseudo();
        return null;
    }

    public static string UpdateLevelAudio(LevelAudioUpdateDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.sceneAssetPath))
            return "缺少场景路径。";
        OpenScene(dto.sceneAssetPath);
        var stub = UnityEngine.Object.FindObjectOfType<PseudoPrefabManagerStub>();
        if (stub == null)
            return "场景中未找到 PseudoPrefabManagerStub。";

        Undo.RecordObject(stub, "Edit Level Audio");
        stub.InLevelMusicSO = LoadPseudoByGuid(dto.inLevelMusicGuid);

        var ambList = new List<PseudoPrefabManagerStub.GameLoopingAudioTag>();
        if (dto.ambiences != null)
        {
            foreach (var name in dto.ambiences)
            {
                if (string.IsNullOrEmpty(name) || name == "COUNT")
                    continue;
                try
                {
                    var t = (PseudoPrefabManagerStub.GameLoopingAudioTag)Enum.Parse(
                        typeof(PseudoPrefabManagerStub.GameLoopingAudioTag), name);
                    ambList.Add(t);
                }
                catch
                {
                }
            }
        }
        stub.InLevelAmbiences = ambList.ToArray();

        var dirList = new List<PseudoPrefabSO>();
        if (dto.audioDirectoryGuids != null)
        {
            foreach (var g in dto.audioDirectoryGuids)
            {
                var p = LoadPseudoByGuid(g);
                if (p != null)
                    dirList.Add(p);
            }
        }
        stub.AudioDirectorySOs = dirList.ToArray();

        stub.OnDeathEffectSO = LoadPseudoByGuid(dto.onDeathEffectGuid);

        AutoMergeAudioDependencies(stub);

        EditorUtility.SetDirty(stub);
        ForcePrepareForBuilding();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        ReloadPseudo();
        return null;
    }

    /// <summary>Ensures LevelInfoSO.dependencies can actually load the bundles required by the currently
    /// referenced audio SOs (BGM + directories + death effect). Only adds a bundle when it is NOT already
    /// reachable via the transitive closure of the existing dependencies (so commonly-loaded bundles like
    /// bundle9/16/18/47 and DLC bundles that already fall inside closure(bundle47) are NOT added). Authoritative
    /// bundle names are read directly from each PseudoPrefabSO.bundleName. Additive only.</summary>
    private static void AutoMergeAudioDependencies(PseudoPrefabManagerStub stub)
    {
        if (stub == null || stub.levelInfo == null)
            return;
        var k = LoadAudioKnowledge();
        var always = new HashSet<string>(k.alwaysLoadedBundles ?? new string[0], StringComparer.Ordinal);

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        // bundle47 (and any other baseBundles) is the foundation that every level must load; always ensure it.
        foreach (var b in k.baseBundles ?? new string[0])
            referenced.Add(b);
        if (stub.InLevelMusicSO != null && !string.IsNullOrEmpty(stub.InLevelMusicSO.bundleName))
            referenced.Add(stub.InLevelMusicSO.bundleName);
        if (stub.OnDeathEffectSO != null && !string.IsNullOrEmpty(stub.OnDeathEffectSO.bundleName))
            referenced.Add(stub.OnDeathEffectSO.bundleName);
        if (stub.AudioDirectorySOs != null)
            foreach (var d in stub.AudioDirectorySOs)
                if (d != null && !string.IsNullOrEmpty(d.bundleName))
                    referenced.Add(d.bundleName);
        referenced.RemoveWhere(b => always.Contains(b) || string.IsNullOrEmpty(b));
        if (referenced.Count == 0)
            return;

        var deps = new List<string>(stub.levelInfo.dependencies ?? new string[0]);
        var loaded = BundleClosure(deps);
        bool changed = false;
        foreach (var b in referenced)
        {
            // Add only when the bundle is genuinely NOT reachable from the current dependencies.
            // Thanks to bundle47's transitive closure this is usually a no-op; the typical
            // exceptions are bundle47 itself (if removed) and bundle11 (raft BGM, not in closure(bundle47)).
            if (loaded.Contains(b) || deps.Contains(b))
                continue;
            deps.Add(b);
            // extending the dependency list can only grow the closure; add this bundle's own closure.
            foreach (var c in BundleClosure(new[] { b }))
                loaded.Add(c);
            changed = true;
        }
        if (changed)
        {
            Undo.RecordObject(stub.levelInfo, "Auto-merge audio dependencies");
            stub.levelInfo.dependencies = deps.ToArray();
            EditorUtility.SetDirty(stub.levelInfo);
        }
    }

    public static AssetPathListDto PreviewDeleteLevel(string setName, string levelId)
    {
        var dto = new AssetPathListDto { paths = new string[0] };
        if (string.IsNullOrEmpty(setName) || string.IsNullOrEmpty(levelId))
            return dto;
        levelId = SanitizeName(levelId);
        if (string.IsNullOrEmpty(levelId) || levelId.Equals("LevelSetInfo", StringComparison.OrdinalIgnoreCase))
            return dto;

        var setDir = LevelSetsRoot + "/" + setName;
        var levelDataDir = setDir + "/data/" + levelId;
        var scenePath = setDir + "/scenes/s_" + levelId + ".unity";

        var list = new List<string>();
        if (File.Exists(AbsPath(scenePath)))
            list.Add(scenePath);
        if (AssetDatabase.IsValidFolder(levelDataDir))
        {
            var seen = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("", new[] { levelDataDir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(p) || !seen.Add(p))
                    continue;
                if (p.StartsWith(levelDataDir + "/", StringComparison.Ordinal))
                    list.Add(p);
            }
        }
        list.Sort((a, b) => string.Compare(a, b, StringComparison.Ordinal));
        dto.paths = list.ToArray();
        return dto;
    }

    public static string DeleteLevel(LevelDeleteDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.setName) || string.IsNullOrEmpty(dto.levelId))
            return "缺少关卡集或关卡标识。";
        var setName = dto.setName;
        var levelId = SanitizeName(dto.levelId);
        if (string.IsNullOrEmpty(levelId))
            return "关卡标识无效。";
        if (levelId.Equals("LevelSetInfo", StringComparison.OrdinalIgnoreCase))
            return "不能删除关卡集配置。";

        var setDir = LevelSetsRoot + "/" + setName;
        var levelDataDir = setDir + "/data/" + levelId;
        if (!AssetDatabase.IsValidFolder(levelDataDir))
            return "关卡不存在：" + levelId;

        var sceneName = "s_" + levelId;
        var scenePath = setDir + "/scenes/" + sceneName + ".unity";
        if (EditorSceneManager.GetActiveScene().path == scenePath)
        {
            var fallback = FindFallbackScene(setName, scenePath);
            if (!string.IsNullOrEmpty(fallback))
                EditorSceneManager.OpenScene(fallback);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        var setInfo = FindSetInfo(setName);
        if (setInfo != null)
        {
            var kept = new List<LevelInfoSO>();
            if (setInfo.levelInfos != null)
            {
                foreach (var li in setInfo.levelInfos)
                {
                    if (li == null)
                        continue;
                    var p = AssetDatabase.GetAssetPath(li);
                    if (p != null && p.StartsWith(levelDataDir + "/", StringComparison.Ordinal))
                        continue;
                    kept.Add(li);
                }
            }
            Undo.RecordObject(setInfo, "Remove Level");
            setInfo.levelInfos = kept.ToArray();
            EditorUtility.SetDirty(setInfo);
        }

        if (File.Exists(AbsPath(scenePath)))
            AssetDatabase.DeleteAsset(scenePath);
        AssetDatabase.DeleteAsset(levelDataDir);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ReloadPseudo();
        return null;
    }

    public static void Reload()
    {
        ReloadPseudo();
    }

    public static string SetDeathTheme(string sceneAssetPath, string theme)
    {
        if (string.IsNullOrEmpty(sceneAssetPath))
            return "缺少场景路径。";
        OpenScene(sceneAssetPath);
        var stub = UnityEngine.Object.FindObjectOfType<PseudoPrefabManagerStub>();
        if (stub == null)
            return "场景中未找到 PseudoPrefabManagerStub。";

        PseudoPrefabSO effect = null;
        if (theme == "water")
            effect = LoadPseudoByName("WaterSplash_Particle_003_SO");
        else if (theme == "goo")
            effect = LoadPseudoByName("WaterSplash_Particle_004_alien_SO");

        Undo.RecordObject(stub, "Death theme");
        stub.OnDeathEffectSO = effect;
        EditorUtility.SetDirty(stub);
        ForcePrepareForBuilding();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        // Reload Pseudo Assets to actually load the new death effect, restoring the editor UI
        // (matches the Tools workflow: Toggle Prepare For Building -> Save -> Reload Pseudo Assets).
        ReloadPseudo();
        return null;
    }

    public static string SetKillPlaneBounds(string sceneAssetPath, float cx, float cz, float sx, float sz)
    {
        if (string.IsNullOrEmpty(sceneAssetPath))
            return "缺少场景路径。";
        OpenScene(sceneAssetPath);

        var stub = UnityEngine.Object.FindObjectOfType<PseudoPrefabManagerStub>();
        GameObject kp = null;
        if (stub != null && stub.KillPlaneGO != null)
            kp = stub.KillPlaneGO;
        if (kp == null)
        {
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                var hit = FindByName(root.transform, "KillPlane");
                if (hit != null)
                {
                    kp = hit.gameObject;
                    break;
                }
            }
        }
        if (kp == null)
            return "未找到 KillPlane。";

        var col = kp.GetComponent<Collider>() ?? kp.GetComponentInChildren<Collider>();
        if (col == null)
            return "KillPlane 上未找到 Collider。";

        var b0 = col.bounds;
        if (b0.size.x <= 0.001f || b0.size.z <= 0.001f)
            return "KillPlane collider 尺寸异常。";

        var t = col.transform;
        var pivot = t.position;
        var c0 = b0.center;
        var kx = sx / b0.size.x;
        var kz = sz / b0.size.z;
        // World-space offset from the transform pivot to the collider center, before scaling.
        var offX = c0.x - pivot.x;
        var offZ = c0.z - pivot.z;

        Undo.RecordObject(t, "KillPlane bounds");
        t.localScale = new Vector3(t.localScale.x * kx, t.localScale.y, t.localScale.z * kz);

        // After scaling around the pivot, the new collider center sits at pivot + (off*k).
        // Solve for the pivot that places that center on (cx, cz) — analytically, without
        // re-reading col.bounds (a MeshCollider's bounds can be stale right after scaling).
        var newPivotX = cx - offX * kx;
        var newPivotZ = cz - offZ * kz;
        t.position = new Vector3(newPivotX, t.position.y, newPivotZ);

        EditorUtility.SetDirty(t);
        // Persist with the canonical workflow: Prepare (strip temp objects) -> Save -> Reload (restore UI).
        ForcePrepareForBuilding();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        ReloadPseudo();
        return null;
    }

    private static Transform FindByName(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindByName(root.GetChild(i), name);
            if (hit != null)
                return hit;
        }
        return null;
    }

    private static PseudoPrefabSO LoadPseudoByName(string name)
    {
        var folders = new[] { "Assets/common01/pseudo_prefab_so", "Assets/common02/pseudo_prefab_so" };
        foreach (var guid in AssetDatabase.FindAssets("t:PseudoPrefabSO", folders))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name)
                return AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(p);
        }
        return null;
    }

    // ==================== Helpers ====================

    private static void ReloadPseudo()
    {
        LayoutEditorPseudoReload.ReloadPseudoAssetsFull();
    }

    private static void ForcePrepareForBuilding()
    {
        var mgr = UnityEngine.Object.FindObjectOfType<LevelEditor.PseudoPrefabManager>();
        if (mgr == null)
            return;
        if (!mgr.prepareForBuilding)
        {
            mgr.prepareForBuilding = true;
            mgr.DeInit();
        }
    }

    private static string FindFallbackScene(string setName, string excludePath)
    {
        var scenesDir = LevelSetsRoot + "/" + setName + "/scenes";
        if (!AssetDatabase.IsValidFolder(scenesDir))
            return null;
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { scenesDir }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (p != excludePath && File.Exists(AbsPath(p)))
                return p;
        }
        return null;
    }

    private static LevelSetInfoSO FindSetInfo(string setName)
    {
        var dataDir = LevelSetsRoot + "/" + setName + "/data";
        if (!AssetDatabase.IsValidFolder(dataDir))
            return null;
        foreach (var guid in AssetDatabase.FindAssets("t:LevelSetInfoSO", new[] { dataDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<LevelSetInfoSO>(path);
            if (so != null)
                return so;
        }
        return null;
    }

    private static void ApplyConfig(PerPlayerConfigDto d, LevelConfigSetupPerPlayerCountSO c, string label)
    {
        if (d == null || c == null)
            return;
        Undo.RecordObject(c, "Edit " + label + " config");
        c.orderLifeTime = d.orderLifeTime;
        c.timeBetweenOrders = d.timeBetweenOrders;
        c.plateReturnTime = d.plateReturnTime;
        c.survivalTimeMultiplier = d.survivalTimeMultiplier;
        c.roundTime = d.roundTime;
        c.m_OneStarScore = d.oneStarScore;
        c.m_TwoStarScore = d.twoStarScore;
        c.m_ThreeStarScore = d.threeStarScore;
        c.m_FourStarScore = d.fourStarScore;
        EditorUtility.SetDirty(c);
    }

    private static PerPlayerConfigDto ConfigToDto(LevelConfigSetupPerPlayerCountSO c)
    {
        var d = new PerPlayerConfigDto { exists = c != null };
        if (c == null)
            return d;
        d.orderLifeTime = c.orderLifeTime;
        d.timeBetweenOrders = c.timeBetweenOrders;
        d.plateReturnTime = c.plateReturnTime;
        d.survivalTimeMultiplier = c.survivalTimeMultiplier;
        d.roundTime = c.roundTime;
        d.oneStarScore = c.m_OneStarScore;
        d.twoStarScore = c.m_TwoStarScore;
        d.threeStarScore = c.m_ThreeStarScore;
        d.fourStarScore = c.m_FourStarScore;
        return d;
    }

    private static PseudoPrefabSO LoadPseudoByGuid(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return null;
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
            return null;
        return AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
    }

    private static void OpenScene(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return;
        assetPath = assetPath.Replace('\\', '/');
        var active = EditorSceneManager.GetActiveScene();
        if (active.path == assetPath)
            return;
        if (File.Exists(AbsPath(assetPath)))
            EditorSceneManager.OpenScene(assetPath);
    }

    private static string AbsPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "";
        var dataPath = Application.dataPath.Replace('\\', '/');
        if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            return dataPath + assetPath.Substring("Assets".Length);
        return assetPath;
    }

    private static string SetNameFromPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "";
        var parts = assetPath.Replace('\\', '/').Split('/');
        if (parts.Length > 2 && parts[1] == "LevelSets")
            return parts[2];
        return "";
    }

    private static string DirectoryName(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "";
        var idx = assetPath.LastIndexOf('/');
        return idx >= 0 ? assetPath.Substring(0, idx) : assetPath;
    }

    private static string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        name = name.Trim();
        var sb = new System.Text.StringBuilder();
        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
                sb.Append(ch);
        }
        return sb.ToString();
    }

    private static AudioConfigDto ReadAudioFromScene(string sceneAssetPath)
    {
        var dto = new AudioConfigDto
        {
            ambiences = new string[0],
            audioDirectoryGuids = new string[0],
            audioDirectoryIds = new string[0]
        };
        var abs = AbsPath(sceneAssetPath);
        if (!File.Exists(abs))
            return dto;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(abs);
        }
        catch
        {
            return dto;
        }

        int managerIdx = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("guid: " + ManagerScriptGuid) && lines[i].Contains("m_Script"))
            {
                managerIdx = i;
                break;
            }
        }
        if (managerIdx < 0)
            return dto;

        var musicGuid = "";
        var deathGuid = "";
        var dirGuids = new List<string>();
        var ambIndices = new List<int>();
        var ambValueMap = BuildAmbienceValueMap();

        for (int i = managerIdx + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("--- !u!", StringComparison.Ordinal))
                break;
            if (line.Length == 0 || line[0] != ' ')
                continue;

            var trimmed = line.Trim();
            if (trimmed.StartsWith("InLevelMusicSO:", StringComparison.Ordinal))
                musicGuid = ExtractGuid(trimmed);
            else if (trimmed.StartsWith("OnDeathEffectSO:", StringComparison.Ordinal))
                deathGuid = ExtractGuid(trimmed);
            else if (trimmed.StartsWith("AudioDirectorySOs:", StringComparison.Ordinal))
            {
                if (trimmed.Contains("[]"))
                    continue;
                for (int j = i + 1; j < lines.Length; j++)
                {
                    var sub = lines[j].TrimStart();
                    if (!sub.StartsWith("- ", StringComparison.Ordinal) || !lines[j].StartsWith("  -", StringComparison.Ordinal))
                        break;
                    dirGuids.Add(ExtractGuid(sub));
                    i = j - 1;
                }
            }
            else if (trimmed.StartsWith("InLevelAmbiences:", StringComparison.Ordinal))
            {
                if (trimmed.Contains("[]"))
                    continue;
                for (int j = i + 1; j < lines.Length; j++)
                {
                    var sub = lines[j].TrimStart();
                    if (!sub.StartsWith("- ", StringComparison.Ordinal) || !lines[j].StartsWith("  -", StringComparison.Ordinal))
                        break;
                    var valStr = sub.Substring(2).Trim();
                    int val;
                    if (int.TryParse(valStr, out val))
                        ambIndices.Add(val);
                    i = j - 1;
                }
            }
        }

        dto.inLevelMusicGuid = musicGuid;
        dto.onDeathEffectGuid = deathGuid;
        dto.audioDirectoryGuids = dirGuids.ToArray();
        dto.audioDirectoryIds = IdsOf(dirGuids);
        var ambNames = new List<string>();
        foreach (var v in ambIndices)
        {
            string nm;
            if (ambValueMap.TryGetValue(v, out nm))
                ambNames.Add(nm);
        }
        dto.ambiences = ambNames.ToArray();
        dto.inLevelMusicId = IdOf(musicGuid);
        dto.onDeathEffectId = IdOf(deathGuid);
        return dto;
    }

    private static Dictionary<int, string> BuildAmbienceValueMap()
    {
        var map = new Dictionary<int, string>();
        var type = typeof(PseudoPrefabManagerStub.GameLoopingAudioTag);
        var values = (int[])Enum.GetValues(type);
        var names = Enum.GetNames(type);
        for (int i = 0; i < values.Length && i < names.Length; i++)
        {
            if (names[i] == "COUNT")
                continue;
            map[values[i]] = names[i];
        }
        return map;
    }

    private static string ExtractGuid(string line)
    {
        var key = "guid: ";
        var idx = line.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0)
            return "";
        var start = idx + key.Length;
        var end = start;
        while (end < line.Length && line[end] != ',' && line[end] != '}' && line[end] != ' ' && line[end] != '\r')
            end++;
        return line.Substring(start, end - start);
    }

    private static string IdOf(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return "";
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? "" : Path.GetFileNameWithoutExtension(path);
    }

    private static string[] IdsOf(List<string> guids)
    {
        var list = new List<string>();
        foreach (var g in guids)
        {
            var id = IdOf(g);
            if (!string.IsNullOrEmpty(id))
                list.Add(id);
        }
        return list.ToArray();
    }

    // ==================== Image floors ====================

    /** Write a base64-encoded image into Assets/LevelSets/<setName>/data/<fileName>,
     *  import it as a Texture2D, and return the texture's asset path. */
    public static string UploadImageFloor(string setName, string fileName, string base64, out string texturePath)
    {
        texturePath = null;
        setName = SanitizeName(setName);
        if (string.IsNullOrEmpty(setName))
            return "缺少关卡集名称。";
        if (string.IsNullOrEmpty(fileName))
            return "缺少图片文件名。";

        // Keep only the file name + sanitize; force a supported image extension.
        var rawName = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
            ext = ".png";
        foreach (var ch in System.IO.Path.GetInvalidFileNameChars())
            rawName = rawName.Replace(ch, '_');
        if (string.IsNullOrEmpty(rawName))
            rawName = "floor_image";
        var safeName = SanitizeName(rawName);
        if (string.IsNullOrEmpty(safeName))
            safeName = "floor_image";

        var setDir = LevelSetsRoot + "/" + setName;
        if (!AssetDatabase.IsValidFolder(setDir))
            return "关卡集不存在：" + setName;
        var dataDir = setDir + "/data";
        if (!AssetDatabase.IsValidFolder(dataDir))
            AssetDatabase.CreateFolder(setDir, "data");

        byte[] bytes;
        try
        {
            // Strip an optional data URL prefix, then decode.
            var b64 = base64 == null ? "" : base64.Trim();
            var comma = b64.IndexOf(',');
            if (comma >= 0 && b64.StartsWith("data:", StringComparison.Ordinal))
                b64 = b64.Substring(comma + 1);
            bytes = Convert.FromBase64String(b64);
        }
        catch
        {
            return "图片 base64 解码失败。";
        }
        if (bytes.Length == 0)
            return "图片数据为空。";

        var assetPath = dataDir + "/" + safeName + ext;
        var absPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        File.WriteAllBytes(absPath, bytes);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        // Make the texture suitable for floor use (readable not required, but
        // ensure it is not a normal-map / is sRGB).
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        texturePath = assetPath;
        return null;
    }

    // ==================== Screenshot upload ====================

    /** Upload a screenshot image for a level. Saves the image into the level's
     *  data directory, imports as a Sprite, and assigns it to LevelInfoSO.screenshot. */
    public static string UploadScreenshot(string assetPath, string fileName, string base64, out string texturePath)
    {
        texturePath = null;
        if (string.IsNullOrEmpty(assetPath))
            return "缺少关卡资源路径。";
        if (string.IsNullOrEmpty(fileName))
            return "缺少截图文件名。";

        var so = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(assetPath);
        if (so == null)
            return "未找到 LevelInfoSO：" + assetPath;

        var rawName = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
            ext = ".png";
        foreach (var ch in System.IO.Path.GetInvalidFileNameChars())
            rawName = rawName.Replace(ch, '_');
        if (string.IsNullOrEmpty(rawName))
            rawName = "screenshot";
        var safeName = SanitizeName(rawName);
        if (string.IsNullOrEmpty(safeName))
            safeName = "screenshot";

        var levelDataDir = DirectoryName(assetPath);
        if (!AssetDatabase.IsValidFolder(levelDataDir))
            return "关卡数据目录不存在：" + levelDataDir;

        byte[] bytes;
        try
        {
            var b64 = base64 == null ? "" : base64.Trim();
            var comma = b64.IndexOf(',');
            if (comma >= 0 && b64.StartsWith("data:", StringComparison.Ordinal))
                b64 = b64.Substring(comma + 1);
            bytes = Convert.FromBase64String(b64);
        }
        catch
        {
            return "截图 base64 解码失败。";
        }
        if (bytes.Length == 0)
            return "截图数据为空。";

        var imgAssetPath = levelDataDir + "/" + safeName + ext;
        var absPath = Path.Combine(Application.dataPath, imgAssetPath.Substring("Assets/".Length));
        File.WriteAllBytes(absPath, bytes);
        AssetDatabase.ImportAsset(imgAssetPath, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(imgAssetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imgAssetPath);
        if (sprite == null)
            return "截图导入后无法加载为 Sprite。";

        Undo.RecordObject(so, "Set Level Screenshot");
        so.screenshot = sprite;
        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
        ReloadPseudo();

        texturePath = imgAssetPath;
        return null;
    }

    // ==================== Custom Recipe Management ====================

    private const string CustomRecipesDir = "custom_recipes";
    private const string CommonCustomRecipesDir = "Assets/common01/food/CustomRecipes";
    private const int ProjectUidPrefix = 1000000;

    // ---------- 文件系统扫描（不依赖 AssetDatabase 索引） ----------

    public const string CustomRecipeScriptGuid = "83fb008bcc8e793429b02c178c430815";
    public const string OptionalBurgerScriptGuid = "e7bb274eb901e2042b1c49a42ecec9df";
    public const string OptionalPizzaScriptGuid = "60297950c88d0d646ac0eca5dc831262";
    public const string PseudoPrefabScriptGuid = "0cff7c13895ab9e47a5e02d4619cc3b9";
    public const string OriginalRecipeScriptGuid = "753d9e70603f6a140b05f30f176ec2dd";

    public class AssetRef
    {
        public string guid;
        public string assetPath;
    }

    /// <summary>CustomRecipeSO 及其 Optional 子类的全部脚本 guid。</summary>
    public static string[] CustomRecipeScriptGuids
    {
        get
        {
            return new[] { CustomRecipeScriptGuid, OptionalBurgerScriptGuid, OptionalPizzaScriptGuid };
        }
    }

    /// <summary>扫描目录下所有自定义菜谱资产（含 Optional 子类），按 guid 去重。</summary>
    public static List<AssetRef> ScanCustomRecipeAssets(string dirAssetPath)
    {
        var list = new List<AssetRef>();
        var seen = new HashSet<string>();
        foreach (var g in CustomRecipeScriptGuids)
        {
            foreach (var a in ScanAssetsByScript(dirAssetPath, g))
            {
                if (seen.Add(a.guid))
                    list.Add(a);
            }
        }
        return list;
    }

    public static bool AssetFolderExists(string dirAssetPath)
    {
        return !string.IsNullOrEmpty(dirAssetPath) && Directory.Exists(AbsPath(dirAssetPath));
    }

    public static string ReadAssetGuid(string metaPath)
    {
        if (string.IsNullOrEmpty(metaPath) || !File.Exists(metaPath))
            return null;
        try
        {
            foreach (var line in File.ReadAllLines(metaPath))
            {
                var t = line.Trim();
                if (t.StartsWith("guid: ", StringComparison.Ordinal))
                    return t.Substring(6).Trim();
            }
        }
        catch { }
        return null;
    }

    private static bool FileContainsGuid(string file, string guid)
    {
        try
        {
            using (var reader = new StreamReader(file))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.IndexOf(guid, StringComparison.Ordinal) >= 0)
                        return true;
                }
            }
        }
        catch { }
        return false;
    }

    /// <summary>按脚本 guid 递归扫描目录下的 .asset（meta guid + m_Script 校验）。
    ///  不依赖 AssetDatabase 索引 —— 索引过期或跨机器拷贝 Library 时 FindAssets 可能失效。</summary>
    public static List<AssetRef> ScanAssetsByScript(string dirAssetPath, string scriptGuid)
    {
        var list = new List<AssetRef>();
        var abs = AbsPath(dirAssetPath);
        if (!Directory.Exists(abs))
            return list;
        foreach (var file in Directory.GetFiles(abs, "*.asset", SearchOption.AllDirectories))
        {
            if (!FileContainsGuid(file, scriptGuid))
                continue;
            var metaPath = file + ".meta";
            var guid = ReadAssetGuid(metaPath);
            if (string.IsNullOrEmpty(guid))
                continue;
            var rel = file.Substring(abs.Length).Replace('\\', '/').TrimStart('/');
            list.Add(new AssetRef
            {
                guid = guid,
                assetPath = (dirAssetPath + "/" + rel).Replace("//", "/")
            });
        }
        return list;
    }

    public static CustomRecipeConfigDto GetOrCreateCustomRecipeConfig(string setName)
    {
        if (string.IsNullOrEmpty(setName))
            return new CustomRecipeConfigDto { uidPrefix = 0, nextSequence = 1, categories = new CustomRecipeCategoryDto[0] };

        var setDir = LevelSetsRoot + "/" + setName;
        var recipesDir = setDir + "/" + CustomRecipesDir;
        var configPath = recipesDir + "/CustomRecipeConfig.asset";
        var namesPath = recipesDir + "/names.json";

        if (!AssetFolderExists(recipesDir))
        {
            AssetDatabase.CreateFolder(setDir, CustomRecipesDir);
            var importer = AssetImporter.GetAtPath(recipesDir);
            if (importer != null)
            {
                importer.SetAssetBundleNameAndVariant(setName + "/custom_recipes", "");
                importer.SaveAndReimport();
            }
        }

        var config = AssetDatabase.LoadAssetAtPath<CustomRecipeConfigSO>(configPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<CustomRecipeConfigSO>();
            var random = new System.Random();
            int prefix;
            do
            {
                prefix = ProjectUidPrefix + random.Next(100000, 999999);
                config.uidPrefix = prefix;
            } while (IsUidPrefixConflicting(prefix));

            config.nextSequence = 1;
            config.categories = new CustomRecipeConfigSO.CustomRecipeCategoryEntry[0];
            AssetDatabase.CreateAsset(config, configPath);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        if (!File.Exists(AbsPath(namesPath)))
        {
            var emptyJson = "{\"schemaVersion\":1,\"names\":[]}";
            File.WriteAllText(AbsPath(namesPath), emptyJson, System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        var catDtos = new List<CustomRecipeCategoryDto>();
        if (config.categories != null)
        {
            foreach (var c in config.categories)
            {
                catDtos.Add(new CustomRecipeCategoryDto { id = c.id, zh = c.zh ?? c.id, en = c.en ?? c.id });
            }
        }

        return new CustomRecipeConfigDto
        {
            uidPrefix = config.uidPrefix,
            nextSequence = config.nextSequence,
            categories = catDtos.ToArray()
        };
    }

    private static bool IsUidPrefixConflicting(int prefix)
    {
        var assets = new List<AssetRef>();
        assets.AddRange(ScanCustomRecipeAssets(LevelSetsRoot));
        if (AssetFolderExists(CommonCustomRecipesDir))
            assets.AddRange(ScanCustomRecipeAssets(CommonCustomRecipesDir));
        foreach (var asset in assets)
        {
            var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(asset.assetPath);
            if (so != null && so.uID / 1000 == prefix)
                return true;
        }
        return false;
    }

    public static CustomRecipeSummaryDto[] ScanCustomRecipes(string setName)
    {
        var list = new List<CustomRecipeSummaryDto>();
        if (string.IsNullOrEmpty(setName))
            return list.ToArray();

        var recipesDir = LevelSetsRoot + "/" + setName + "/" + CustomRecipesDir;
        if (!AssetFolderExists(recipesDir))
            return list.ToArray();

        var namesDict = LoadCustomRecipeNames(setName);

        // 全部候选条目（本关卡集 + common01 官方 CustomRecipes），
        // 用于把组成里的子菜谱 id 解析为烹饪步骤与叶食材。
        var allEntries = BuildCustomRecipeEntryDtos(setName);
        var entryById = new Dictionary<string, RecipeEntryDto>(StringComparer.Ordinal);
        foreach (var e in allEntries)
        {
            if (!string.IsNullOrEmpty(e.id) && !entryById.ContainsKey(e.id))
                entryById[e.id] = e;
        }

        foreach (var asset in ScanCustomRecipeAssets(recipesDir))
        {
            var path = asset.assetPath;
            var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(path);
            if (so == null)
                continue;

            var guid = asset.guid;
            var id = Path.GetFileNameWithoutExtension(path);
            var category = "";
            var rel = path.Substring(recipesDir.Length + 1).Replace('\\', '/');
            var slash = rel.IndexOf('/');
            if (slash >= 0)
                category = rel.Substring(0, slash);

            var compIds = new List<string>();
            if (so.compositionSOs != null)
            {
                foreach (var c in so.compositionSOs)
                {
                    if (c == null) continue;
                    var cp = AssetDatabase.GetAssetPath(c);
                    if (!string.IsNullOrEmpty(cp))
                        compIds.Add(Path.GetFileNameWithoutExtension(cp));
                }
            }

            string stepId = "";
            if (so.cookingStepSO != null)
            {
                var sp = AssetDatabase.GetAssetPath(so.cookingStepSO);
                if (!string.IsNullOrEmpty(sp))
                    stepId = Path.GetFileNameWithoutExtension(sp);
            }

            string plateId = "";
            if (so.platingStepSO != null)
            {
                var pp = AssetDatabase.GetAssetPath(so.platingStepSO);
                if (!string.IsNullOrEmpty(pp))
                    plateId = Path.GetFileNameWithoutExtension(pp);
            }

            bool hasIcon = so.icon != null;
            bool hasModel = so.model != null;

            string nameZh = id;
            string nameEn = id;
            NameRow nr;
            if (namesDict.TryGetValue(id, out nr))
            {
                nameZh = nr.Zh;
                nameEn = nr.En;
            }

            RecipeEntryDto entry;
            entryById.TryGetValue(id, out entry);

            list.Add(new CustomRecipeSummaryDto
            {
                guid = guid,
                id = id,
                assetPath = path,
                recipeName = so.recipeName ?? id,
                nameZh = nameZh,
                nameEn = nameEn,
                uID = so.uID,
                score = so.score,
                category = category,
                type = so.type.ToString(),
                compositionIds = compIds.ToArray(),
                ingredients = entry != null ? entry.ingredients : new string[0],
                cookingGroups = entry != null ? LayoutEditorRecipeKnowledge.ComputeCookingGroups(entry, allEntries) : new RecipeCookingGroupDto[0],
                intermediate = so.score <= 0,
                group = LayoutEditorCatalogApi.FoodGroupOf(path),
                cookingStepId = stepId,
                platingStepId = plateId,
                hasIcon = hasIcon,
                hasModel = hasModel,
                modelScale = RecipeModelScale(path),
                modelRotationY = RecipeModelRotationY(path)
            });
        }

        list.Sort((a, b) => string.Compare(a.nameZh, b.nameZh, StringComparison.Ordinal));
        return list.ToArray();
    }

    /// <summary>CustomRecipeSO → RecipeEntryDto（叶食材展开 + 直接组成 + 步骤），
    ///  覆盖本关卡集目录与 common01 官方 CustomRecipes，供工序分组解析子菜谱。</summary>
    private static List<RecipeEntryDto> BuildCustomRecipeEntryDtos(string setName)
    {
        var list = new List<RecipeEntryDto>();
        var folders = new List<string>();

        var recipesDir = LevelSetsRoot + "/" + setName + "/" + CustomRecipesDir;
        if (AssetFolderExists(recipesDir))
            folders.Add(recipesDir);
        if (AssetFolderExists(CommonCustomRecipesDir))
            folders.Add(CommonCustomRecipesDir);

        foreach (var folder in folders)
        {
            foreach (var asset in ScanCustomRecipeAssets(folder))
            {
                var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(asset.assetPath);
                if (so == null)
                    continue;
                list.Add(new RecipeEntryDto
                {
                    guid = asset.guid,
                    id = Path.GetFileNameWithoutExtension(asset.assetPath),
                    assetPath = asset.assetPath,
                    cookingStep = LayoutEditorRecipeKnowledge.CustomCookingStep(so),
                    ingredients = LayoutEditorRecipeKnowledge.CustomIngredients(so).ToArray(),
                    compositionIds = DirectCompositionIds(so),
                    score = so.score,
                    isCustom = true,
                    group = LayoutEditorCatalogApi.FoodGroupOf(asset.assetPath),
                    type = so.type.ToString(),
                    intermediate = so.score <= 0
                });
            }
        }
        return list;
    }

    private static string[] DirectCompositionIds(CustomRecipeSO so)
    {
        if (so == null || so.compositionSOs == null)
            return new string[0];
        var ids = new List<string>();
        foreach (var c in so.compositionSOs)
        {
            if (c == null)
                continue;
            var cp = AssetDatabase.GetAssetPath(c);
            if (!string.IsNullOrEmpty(cp))
                ids.Add(Path.GetFileNameWithoutExtension(cp));
        }
        return ids.ToArray();
    }

    private static Dictionary<string, NameRow> LoadCustomRecipeNames(string setName)
    {
        var dict = new Dictionary<string, NameRow>(StringComparer.Ordinal);
        var namesPath = LevelSetsRoot + "/" + setName + "/" + CustomRecipesDir + "/names.json";
        var abs = AbsPath(namesPath);
        if (!File.Exists(abs))
            return dict;

        try
        {
            var text = File.ReadAllText(abs);
            var doc = JsonUtility.FromJson<CustomNamesDoc>(text);
            if (doc != null && doc.names != null)
            {
                foreach (var n in doc.names)
                {
                    if (n == null || string.IsNullOrEmpty(n.id))
                        continue;
                    dict[n.id] = new NameRow { Zh = n.zh ?? "", En = n.en ?? n.id };
                }
            }
        }
        catch { }

        return dict;
    }

    private struct NameRow
    {
        public string Zh;
        public string En;
    }

    [Serializable]
    private class CustomNamesDoc
    {
        public int schemaVersion;
        public CustomNamesEntry[] names;
    }

    [Serializable]
    private class CustomNamesEntry
    {
        public string id;
        public string zh;
        public string en;
    }

    public static CustomRecipeReferencesDto GetCustomRecipeReferences(string setName)
    {
        var dto = new CustomRecipeReferencesDto();

        var cookingSteps = new List<CustomRecipeReferenceEntryDto>();
        var platingSteps = new List<CustomRecipeReferenceEntryDto>();
        var iconSoList = new List<CustomRecipeReferenceEntryDto>();
        var seen = new HashSet<string>();

        // 装盘容器：PlatingSteps 目录（盘子/杯子…），运行时映射为 PlatingStepData。
        var containers = new List<CustomRecipeReferenceEntryDto>();
        var containerFolders = new[]
        {
            "Assets/common01/food/PlatingSteps",
            "Assets/common02/food/PlatingSteps"
        };
        foreach (var folder in containerFolders)
        {
            if (!AssetFolderExists(folder))
                continue;
            foreach (var asset in ScanAssetsByScript(folder, PseudoPrefabScriptGuid))
            {
                if (!seen.Add(asset.guid))
                    continue;
                var cid = Path.GetFileNameWithoutExtension(asset.assetPath);
                var cso = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(asset.assetPath);
                if (cso == null)
                    continue;
                string czh, cen;
                LayoutEditorManualLookup.TryGet(cid, out czh, out cen);
                containers.Add(new CustomRecipeReferenceEntryDto
                {
                    guid = asset.guid,
                    id = cid,
                    nameZh = czh,
                    nameEn = cen,
                    assetPath = asset.assetPath
                });
            }
        }
        dto.platingContainers = containers.ToArray();

        var stepFolders = new[]
        {
            "Assets/common01/food/CookingSteps",
            "Assets/common02/food/CookingSteps"
        };

        foreach (var folder in stepFolders)
        {
            if (!AssetFolderExists(folder))
                continue;
            foreach (var asset in ScanAssetsByScript(folder, PseudoPrefabScriptGuid))
            {
                if (!seen.Add(asset.guid))
                    continue;
                var path = asset.assetPath;
                var id = Path.GetFileNameWithoutExtension(path);
                var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
                if (so == null)
                    continue;

                string zh, en;
                LayoutEditorManualLookup.TryGet(id, out zh, out en);

                var entry = new CustomRecipeReferenceEntryDto
                {
                    guid = asset.guid,
                    id = id,
                    nameZh = zh,
                    nameEn = en,
                    assetPath = path
                };

                bool isIcon = id.EndsWith("Icon", StringComparison.Ordinal);
                bool isPlating = !isIcon && !LayoutEditorRecipeKnowledge.IsCookStep(id);

                if (isPlating)
                    platingSteps.Add(entry);
                else if (!isIcon)
                    cookingSteps.Add(entry);

                if (isIcon)
                    iconSoList.Add(entry);
            }
        }

        dto.cookingSteps = cookingSteps.ToArray();
        dto.platingSteps = platingSteps.ToArray();
        dto.icons = iconSoList.ToArray();

        var modelList = new List<CustomRecipeReferenceEntryDto>();
        var modelSeen = new HashSet<string>();

        var modelFolders = new List<string>
        {
            "Assets/common01/food/CustomRecipes",
        };

        if (!string.IsNullOrEmpty(setName))
        {
            var setRecipesDir = LevelSetsRoot + "/" + setName + "/" + CustomRecipesDir;
            if (AssetFolderExists(setRecipesDir))
                modelFolders.Add(setRecipesDir);
        }

        foreach (var folder in modelFolders)
        {
            foreach (var asset in ScanCustomRecipeAssets(folder))
            {
                var path = asset.assetPath;
                var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(path);
                // modelSO 已不再使用：可复用模型直接取 model 引用的 prefab
                if (so == null || so.model == null)
                    continue;

                var modelPath = AssetDatabase.GetAssetPath(so.model);
                if (string.IsNullOrEmpty(modelPath) || !modelSeen.Add(modelPath))
                    continue;

                var id = Path.GetFileNameWithoutExtension(modelPath);
                string zh, en;
                LayoutEditorManualLookup.TryGet(id, out zh, out en);

                if (string.IsNullOrEmpty(zh) || zh == id)
                    zh = so.recipeName ?? id;

                modelList.Add(new CustomRecipeReferenceEntryDto
                {
                    guid = ReadAssetGuid(modelPath + ".meta") ?? AssetDatabase.AssetPathToGUID(modelPath),
                    id = id,
                    recipeId = Path.GetFileNameWithoutExtension(path),
                    nameZh = zh,
                    nameEn = en,
                    assetPath = modelPath
                });
            }
        }

        dto.reusableModels = modelList.ToArray();
        dto.ingredients = new string[0];

        return dto;
    }

    /// <summary>解析自定义菜谱图标文件的绝对路径（从 icon 资产取真实文件，不依赖命名约定）。</summary>
    public static string ResolveRecipeIconFile(string recipeAssetPath, out string absPath, out string contentType)
    {
        absPath = null;
        contentType = "image/png";
        if (string.IsNullOrEmpty(recipeAssetPath))
            return "缺少菜谱路径。";
        var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(recipeAssetPath);
        if (so == null)
            return "未找到菜谱资源。";
        if (so.icon == null)
            return "该菜谱没有图标。";
        var iconPath = AssetDatabase.GetAssetPath(so.icon);
        if (string.IsNullOrEmpty(iconPath))
            return "无法解析图标路径。";
        absPath = AbsPath(iconPath);
        var ext = Path.GetExtension(absPath).ToLowerInvariant();
        switch (ext)
        {
            case ".jpg":
            case ".jpeg": contentType = "image/jpeg"; break;
            default: contentType = "image/png"; break;
        }
        return null;
    }

    /// <summary>菜谱所在 custom_recipes 目录的配置文件路径（找不到返回 null）。</summary>
    private static string CustomRecipeConfigPathFor(string recipeAssetPath)
    {
        if (string.IsNullOrEmpty(recipeAssetPath))
            return null;
        var dir = Path.GetDirectoryName(recipeAssetPath.Replace('\\', '/'));
        while (!string.IsNullOrEmpty(dir))
        {
            if (string.Equals(Path.GetFileName(dir), CustomRecipesDir, StringComparison.OrdinalIgnoreCase))
                return dir + "/CustomRecipeConfig.asset";
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir)
                break;
            dir = parent;
        }
        return null;
    }

    private static CustomRecipeConfigSO LoadCustomRecipeConfigFor(string recipeAssetPath)
    {
        var configPath = CustomRecipeConfigPathFor(recipeAssetPath);
        return string.IsNullOrEmpty(configPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<CustomRecipeConfigSO>(configPath);
    }

    private static CustomRecipeConfigSO.CustomRecipeTransformEntry FindModelTransform(
        CustomRecipeConfigSO config, string recipeAssetPath)
    {
        if (config == null || config.modelTransforms == null)
            return null;
        foreach (var e in config.modelTransforms)
            if (e != null && e.assetPath == recipeAssetPath)
                return e;
        return null;
    }

    /// <summary>从旧版 .asset YAML 中读取已序列化的 modelScale/modelRotationY 并迁移到配置
    ///  （一次性；CustomRecipeSO 回退为宿主原版后这些字段为未知字段，仍保留在 YAML 中）。</summary>
    private static void ImportLegacyModelTransform(CustomRecipeConfigSO config, string recipeAssetPath)
    {
        if (config == null || FindModelTransform(config, recipeAssetPath) != null)
            return;
        var abs = AbsPath(recipeAssetPath);
        if (string.IsNullOrEmpty(abs) || !File.Exists(abs))
            return;
        string yaml;
        try { yaml = File.ReadAllText(abs); }
        catch { return; }
        var scale = ParseYamlFloat(yaml, "modelScale:");
        var rotationY = ParseYamlFloat(yaml, "modelRotationY:");
        if (scale == null && rotationY == null)
            return;
        var list = new List<CustomRecipeConfigSO.CustomRecipeTransformEntry>(
            config.modelTransforms ?? new CustomRecipeConfigSO.CustomRecipeTransformEntry[0]);
        list.Add(new CustomRecipeConfigSO.CustomRecipeTransformEntry
        {
            assetPath = recipeAssetPath,
            scale = scale ?? 1f,
            rotationY = rotationY ?? 0f,
        });
        config.modelTransforms = list.ToArray();
        EditorUtility.SetDirty(config);
    }

    private static float? ParseYamlFloat(string yaml, string key)
    {
        var m = Regex.Match(yaml, key + @"\s*([-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?)");
        if (!m.Success)
            return null;
        float v;
        return float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out v) ? v : (float?)null;
    }

    private static float RecipeModelScale(string recipeAssetPath)
    {
        var config = LoadCustomRecipeConfigFor(recipeAssetPath);
        ImportLegacyModelTransform(config, recipeAssetPath);
        var e = FindModelTransform(config, recipeAssetPath);
        return e != null && e.scale > 0f ? e.scale : 1f;
    }

    private static float RecipeModelRotationY(string recipeAssetPath)
    {
        var config = LoadCustomRecipeConfigFor(recipeAssetPath);
        var e = FindModelTransform(config, recipeAssetPath);
        return e != null ? e.rotationY : 0f;
    }

    private static void SetRecipeModelTransform(string recipeAssetPath, float scale, float rotationY)
    {
        var config = LoadCustomRecipeConfigFor(recipeAssetPath);
        if (config == null)
        {
            // 配置不存在（如 common01 官方菜谱目录）时按需创建，仅用于存储模型变换。
            var configPath = CustomRecipeConfigPathFor(recipeAssetPath);
            if (string.IsNullOrEmpty(configPath) || !AssetFolderExists(Path.GetDirectoryName(configPath)))
                return;
            config = ScriptableObject.CreateInstance<CustomRecipeConfigSO>();
            config.uidPrefix = 0;
            config.nextSequence = 1;
            config.categories = new CustomRecipeConfigSO.CustomRecipeCategoryEntry[0];
            AssetDatabase.CreateAsset(config, configPath);
        }
        if (scale <= 0f)
            scale = 1f;
        var list = new List<CustomRecipeConfigSO.CustomRecipeTransformEntry>(
            config.modelTransforms ?? new CustomRecipeConfigSO.CustomRecipeTransformEntry[0]);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].assetPath == recipeAssetPath)
            {
                list[i].scale = scale;
                list[i].rotationY = rotationY;
                config.modelTransforms = list.ToArray();
                EditorUtility.SetDirty(config);
                return;
            }
        }
        list.Add(new CustomRecipeConfigSO.CustomRecipeTransformEntry
        {
            assetPath = recipeAssetPath,
            scale = scale,
            rotationY = rotationY,
        });
        config.modelTransforms = list.ToArray();
        EditorUtility.SetDirty(config);
    }

    /// <summary>把菜谱的 modelScale/modelRotationY 应用到 prefab 根节点（运行时直接生效）。
    ///  数值存储在插件自己的 CustomRecipeConfigSO 中，不修改宿主 CustomRecipeSO。</summary>
    private static void ApplyModelTransform(CustomRecipeSO so)
    {
        if (so == null || so.model == null)
            return;
        var path = AssetDatabase.GetAssetPath(so);
        if (string.IsNullOrEmpty(path))
            return;
        so.model.transform.localScale = Vector3.one * Mathf.Max(0.001f, RecipeModelScale(path));
        so.model.transform.localEulerAngles = new Vector3(0f, RecipeModelRotationY(path), 0f);
        EditorUtility.SetDirty(so.model);
    }

    /// <summary>从 FBX 字节中提取内嵌纹理引用名（RelativeFilename，如 model.fbm/Image_0.jpg）。
    ///  FBX 字符串属性格式：'S' 标记 + int32 长度 + 内容，按长度精确解析。</summary>
    private static List<string> ExtractFbxTextureReferences(byte[] fbx)
    {
        var list = new List<string>();
        if (fbx == null || fbx.Length < 64)
            return list;
        var marker = System.Text.Encoding.ASCII.GetBytes("RelativeFilename");
        for (int i = 0; i + marker.Length < fbx.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < marker.Length; j++)
            {
                if (fbx[i + j] != marker[j])
                {
                    match = false;
                    break;
                }
            }
            if (!match)
                continue;
            int p = i + marker.Length;
            if (p < fbx.Length && fbx[p] == 0x53) // 'S' 字符串类型标记
                p++;
            if (p + 4 > fbx.Length)
                continue;
            int len = BitConverter.ToInt32(fbx, p);
            p += 4;
            if (len <= 0 || len > 512 || p + len > fbx.Length)
                continue;
            var name = System.Text.Encoding.UTF8.GetString(fbx, p, len);
            var lower = name.ToLowerInvariant();
            if ((lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".tga")) &&
                !list.Contains(name))
                list.Add(name);
        }
        return list;
    }

    /// <summary>图片字节转码（Unity Texture2D），供贴图按 FBX 引用扩展名写入。</summary>
    private static byte[] ConvertImageBytes(byte[] src, string targetExt)
    {
        if (src == null || src.Length == 0)
            return null;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(src))
        {
            UnityEngine.Object.DestroyImmediate(tex);
            return null;
        }
        var lower = (targetExt ?? "").ToLowerInvariant();
        byte[] result = lower == ".jpg" || lower == ".jpeg"
            ? tex.EncodeToJPG(90)
            : tex.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(tex);
        return result;
    }

    /// <summary>解析预制体的网格源文件（.obj/.fbx），供 3D 预览中作为大小参考
    ///  （如披萨装盘 plated_mushroom_01 模型含盘子，可对比自定义模型的实际尺寸）。</summary>
    public static string ResolvePrefabMeshFile(string prefabAssetPath, out string absPath, out string contentType)
    {
        absPath = null;
        contentType = "application/octet-stream";
        if (string.IsNullOrEmpty(prefabAssetPath))
            return "缺少预制体路径。";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
        if (prefab == null)
            return "预制体不存在。";
        var mf = prefab.GetComponentInChildren<MeshFilter>(true);
        if (mf == null || mf.sharedMesh == null)
            return "预制体没有网格（无法作为参考模型）。";
        var meshPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
        if (string.IsNullOrEmpty(meshPath))
            return "无法解析网格路径。";
        var rootAbs = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        absPath = Path.GetFullPath(Path.Combine(rootAbs, meshPath.Replace('/', Path.DirectorySeparatorChar)));
        var ext = Path.GetExtension(meshPath).ToLowerInvariant();
        if (ext == ".obj")
            contentType = "text/plain";
        else if (ext == ".fbx")
            contentType = "application/octet-stream";
        else
            return "参考模型格式不支持（仅 .obj/.fbx）。";
        return null;
    }

    public static string CreateCustomRecipe(CustomRecipeEditDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.setName) || string.IsNullOrEmpty(dto.recipeName))
            return "缺少必要参数。";

        var setName = SanitizeName(dto.setName);
        var recipeName = SanitizeName(dto.recipeName);
        if (string.IsNullOrEmpty(setName) || string.IsNullOrEmpty(recipeName))
            return "标识只能包含字母数字和下划线。";

        if (!GloballyUniqueRecipeName(recipeName))
            return "菜谱名称「" + recipeName + "」已被其他关卡集使用。";

        var recipesDir = LevelSetsRoot + "/" + setName + "/" + CustomRecipesDir;
        if (!AssetFolderExists(recipesDir))
            return "请先访问自定义菜谱页面以初始化配置。";

        var configPath = recipesDir + "/CustomRecipeConfig.asset";
        var config = AssetDatabase.LoadAssetAtPath<CustomRecipeConfigSO>(configPath);
        if (config == null)
            return "配置文件丢失，请重新进入自定义菜谱页面。";

        var category = SanitizeName(dto.category ?? "Uncategorized");
        if (string.IsNullOrEmpty(category))
            category = "Uncategorized";

        var categoryDir = recipesDir + "/" + category;
        if (!AssetFolderExists(categoryDir))
        {
            AssetDatabase.CreateFolder(recipesDir, category);
            var cats = config.categories != null ? new List<CustomRecipeConfigSO.CustomRecipeCategoryEntry>(config.categories) : new List<CustomRecipeConfigSO.CustomRecipeCategoryEntry>();
            if (!cats.Exists(c => c.id == category))
            {
                cats.Add(new CustomRecipeConfigSO.CustomRecipeCategoryEntry { id = category, zh = category, en = category });
                config.categories = cats.ToArray();
                EditorUtility.SetDirty(config);
            }
        }

        int uid;
        do
        {
            uid = config.uidPrefix * 1000 + config.nextSequence;
            config.nextSequence++;
        } while (IsUidConflicting(uid));
        EditorUtility.SetDirty(config);

        var so = ScriptableObject.CreateInstance<CustomRecipeSO>();
        so.recipeName = recipeName;
        so.uID = uid;
        so.score = dto.score;

        if (dto.type == "Composite")
        {
            so.type = CustomRecipeSO.RecipeType.Composite;
            so.cookingStepSO = null;
            so.cookingStepIconSO = null;
        }
        else if (dto.type == "Mixed")
            so.type = CustomRecipeSO.RecipeType.Mixed;
        else
            so.type = CustomRecipeSO.RecipeType.Cooked;

        so.cookingProgress = (CustomRecipeSO.CookingProgress)(dto.cookingProgress >= 0 && dto.cookingProgress <= 2 ? dto.cookingProgress : 1);
        so.mixingProgress = (CustomRecipeSO.MixingProgress)(dto.mixingProgress >= 0 && dto.mixingProgress <= 2 ? dto.mixingProgress : 1);

        if (dto.compositionIds != null && dto.compositionIds.Length > 0)
        {
            var comps = new List<ScriptableObject>();
            foreach (var compId in dto.compositionIds)
            {
                if (string.IsNullOrEmpty(compId))
                    continue;
                var soFound = FindPseudoPrefabOrCustomRecipe(compId);
                if (soFound != null)
                    comps.Add(soFound);
            }
            so.compositionSOs = comps.ToArray();
        }

        if (!string.IsNullOrEmpty(dto.cookingStepId))
            so.cookingStepSO = FindPseudoPrefabById(dto.cookingStepId);

        if (!string.IsNullOrEmpty(dto.cookingStepIconId))
            so.cookingStepIconSO = FindPseudoPrefabById(dto.cookingStepIconId);

        if (!string.IsNullOrEmpty(dto.platingStepId))
            so.platingStepSO = FindPlatingContainerById(dto.platingStepId);

        if (!string.IsNullOrEmpty(dto.mixingIconId))
            so.mixingIconSO = FindPseudoPrefabById(dto.mixingIconId);

        if (!string.IsNullOrEmpty(dto.modelPrefabId))
        {
            // 复用已有模型：modelPrefabId = 菜谱 id / prefab 文件名；modelSO 不再使用
            var model = FindCustomRecipeModelByRecipeId(dto.modelPrefabId);
            if (model == null)
            {
                // 旧数据兼容：modelSO 文件名查找
                var oldSO = FindPseudoPrefabById(dto.modelPrefabId);
                if (oldSO != null && !string.IsNullOrEmpty(oldSO.assetPath))
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(oldSO.assetPath);
            }
            so.model = model;
            so.modelSO = null;
        }

        var assetPath = categoryDir + "/" + recipeName + ".asset";
        AssetDatabase.CreateAsset(so, assetPath);
        EditorUtility.SetDirty(so);

        var modelsDir = categoryDir + "/models";
        if (!AssetFolderExists(modelsDir))
            AssetDatabase.CreateFolder(categoryDir, "models");

        AssetDatabase.SaveAssets();
        SetRecipeModelTransform(assetPath, dto.modelScale, dto.modelRotationY);
        ApplyModelTransform(so);

        AddCustomRecipeName(setName, recipeName, dto.nameZh, dto.nameEn);

        return null;
    }

    public static string UpdateCustomRecipe(CustomRecipeEditDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.assetPath))
            return "缺少资源路径。";

        var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(dto.assetPath);
        if (so == null)
            return "未找到菜谱资源。";

        Undo.RecordObject(so, "Edit Custom Recipe");
        so.score = dto.score;

        if (dto.type == "Composite")
        {
            so.type = CustomRecipeSO.RecipeType.Composite;
            so.cookingStepSO = null;
            so.cookingStepIconSO = null;
        }
        else if (dto.type == "Mixed")
            so.type = CustomRecipeSO.RecipeType.Mixed;
        else
            so.type = CustomRecipeSO.RecipeType.Cooked;

        so.cookingProgress = (CustomRecipeSO.CookingProgress)(dto.cookingProgress >= 0 && dto.cookingProgress <= 2 ? dto.cookingProgress : 1);
        so.mixingProgress = (CustomRecipeSO.MixingProgress)(dto.mixingProgress >= 0 && dto.mixingProgress <= 2 ? dto.mixingProgress : 1);
        SetRecipeModelTransform(dto.assetPath, dto.modelScale, dto.modelRotationY);

        if (dto.compositionIds != null)
        {
            var comps = new List<ScriptableObject>();
            foreach (var compId in dto.compositionIds)
            {
                if (string.IsNullOrEmpty(compId))
                    continue;
                var soFound = FindPseudoPrefabOrCustomRecipe(compId);
                if (soFound != null)
                    comps.Add(soFound);
            }
            so.compositionSOs = comps.ToArray();
        }

        if (!string.IsNullOrEmpty(dto.cookingStepId))
            so.cookingStepSO = FindPseudoPrefabById(dto.cookingStepId);

        if (!string.IsNullOrEmpty(dto.cookingStepIconId))
            so.cookingStepIconSO = FindPseudoPrefabById(dto.cookingStepIconId);

        if (!string.IsNullOrEmpty(dto.platingStepId))
            so.platingStepSO = FindPlatingContainerById(dto.platingStepId);

        if (!string.IsNullOrEmpty(dto.mixingIconId))
            so.mixingIconSO = FindPseudoPrefabById(dto.mixingIconId);

        if (!string.IsNullOrEmpty(dto.modelPrefabId))
        {
            // 复用已有模型：modelPrefabId = 菜谱 id / prefab 文件名；modelSO 不再使用
            var model = FindCustomRecipeModelByRecipeId(dto.modelPrefabId);
            if (model == null)
            {
                // 旧数据兼容：modelSO 文件名查找
                var oldSO = FindPseudoPrefabById(dto.modelPrefabId);
                if (oldSO != null && !string.IsNullOrEmpty(oldSO.assetPath))
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(oldSO.assetPath);
            }
            so.model = model;
            so.modelSO = null;
        }

        EditorUtility.SetDirty(so);

        var setName = SetNameFromPath(dto.assetPath);
        var id = Path.GetFileNameWithoutExtension(dto.assetPath);
        UpdateCustomRecipeName(setName, id, dto.nameZh, dto.nameEn);

        AssetDatabase.SaveAssets();
        ApplyModelTransform(so);
        return null;
    }

    public static string DeleteCustomRecipe(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "缺少资源路径。";

        var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(assetPath);
        if (so == null)
            return "未找到菜谱资源。";

        var setName = SetNameFromPath(assetPath);
        var id = Path.GetFileNameWithoutExtension(assetPath);

        var dir = DirectoryName(assetPath);
        var modelsDir = dir + "/models";

        if (!AssetDatabase.DeleteAsset(assetPath))
            return "删除资源失败。";

        if (AssetFolderExists(modelsDir))
            AssetDatabase.DeleteAsset(modelsDir);

        RemoveCustomRecipeName(setName, id);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return null;
    }

    public static string UploadCustomRecipeIcon(CustomRecipeUploadDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.recipeAssetPath) || string.IsNullOrEmpty(dto.base64))
            return "缺少上传参数。";

        var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(dto.recipeAssetPath);
        if (so == null)
            return "未找到菜谱资源。";

        var dir = DirectoryName(dto.recipeAssetPath);
        var modelsDir = dir + "/models";
        if (!AssetFolderExists(modelsDir))
            AssetDatabase.CreateFolder(DirectoryName(dir), "models");

        // 统一命名为 <recipeName>_Icon.png，与卡片图标 URL（…/models/<id>_Icon.png）一致。
        var recipeId = Path.GetFileNameWithoutExtension(dto.recipeAssetPath);
        var safeName = SanitizeName(recipeId) + "_Icon.png";
        var imgAssetPath = modelsDir + "/" + safeName;
        if (File.Exists(AbsPath(imgAssetPath)))
            AssetDatabase.DeleteAsset(imgAssetPath);
        byte[] bytes;
        try
        {
            bytes = System.Convert.FromBase64String(dto.base64);
        }
        catch
        {
            return "图片数据解码失败。";
        }

        File.WriteAllBytes(AbsPath(imgAssetPath), bytes);
        AssetDatabase.Refresh();

        var texImporter = AssetImporter.GetAtPath(imgAssetPath) as TextureImporter;
        if (texImporter != null)
        {
            texImporter.textureType = TextureImporterType.Sprite;
            texImporter.SaveAndReimport();
        }

        AssetDatabase.Refresh();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imgAssetPath);
        if (sprite == null)
            return "图标导入后无法加载为 Sprite。";

        Undo.RecordObject(so, "Set Recipe Icon");
        so.icon = sprite;
        so.iconSO = null;

        // IconSO 不再需要：删除历史遗留的 <recipeName>IconSO.asset
        var iconSOId = Path.GetFileNameWithoutExtension(dto.recipeAssetPath) + "IconSO";
        var iconSOPath = modelsDir + "/" + iconSOId + ".asset";
        if (AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(iconSOPath) != null)
            AssetDatabase.DeleteAsset(iconSOPath);

        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
        return null;
    }

    public static string UploadCustomRecipeModel(CustomRecipeUploadDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.recipeAssetPath))
            return "缺少上传参数。";

        var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(dto.recipeAssetPath);
        if (so == null)
            return "未找到菜谱资源。";

        // 兼容单文件旧格式：转成 files 数组。
        var files = dto.files != null && dto.files.Length > 0
            ? dto.files
            : (!string.IsNullOrEmpty(dto.fileName) && !string.IsNullOrEmpty(dto.base64)
                ? new[] { new CustomRecipeUploadFileDto { fileName = dto.fileName, base64 = dto.base64 } }
                : null);
        if (files == null || files.Length == 0)
            return "缺少上传参数。";

        CustomRecipeUploadFileDto modelFile = null;
        foreach (var f in files)
        {
            if (f != null && IsModelFileName(f.fileName))
            {
                modelFile = f;
                break;
            }
        }
        if (modelFile == null)
            return "请选择 FBX 或 OBJ 模型文件。";

        var dir = DirectoryName(dto.recipeAssetPath);
        var modelsDir = dir + "/models";
        if (!AssetFolderExists(modelsDir))
            AssetDatabase.CreateFolder(DirectoryName(dir), "models");

        // 先清空旧模型/旧贴图（保留 <recipeName>_Icon.png 图标），再写入新文件。
        var recipeId = SanitizeName(Path.GetFileNameWithoutExtension(dto.recipeAssetPath));
        var ext = modelFile.fileName != null && modelFile.fileName.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)
            ? ".obj" : ".fbx";
        var safeName = recipeId + ext;
        var modelAssetPath = modelsDir + "/" + safeName;
        var prefabPath = modelAssetPath.Substring(0, modelAssetPath.Length - ext.Length) + ".prefab";

        ClearModelDirAssets(modelsDir, recipeId + "_Icon.png");

        // 写入全部文件（主模型统一命名为 <recipeName>.fbx/.obj，贴图保留原文件名）。
        string uploadedTexturePath = null;
        foreach (var f in files)
        {
            if (f == null || string.IsNullOrEmpty(f.fileName) || string.IsNullOrEmpty(f.base64))
                continue;
            byte[] bytes;
            try
            {
                bytes = System.Convert.FromBase64String(f.base64);
            }
            catch
            {
                return "文件「" + f.fileName + "」数据解码失败。";
            }
            var targetName = ReferenceEquals(f, modelFile)
                ? safeName
                : SanitizeUploadFileName(f.fileName);
            if (string.IsNullOrEmpty(targetName))
                continue;
            File.WriteAllBytes(AbsPath(modelsDir + "/" + targetName), bytes);
            if (uploadedTexturePath == null && !ReferenceEquals(f, modelFile))
                uploadedTexturePath = modelsDir + "/" + targetName;
        }

        AssetDatabase.Refresh();

        // 把上传的贴图按 FBX 内嵌纹理引用名写入（如 model.fbm/Image_0.jpg）：
        // 否则 three.js 预览与 Unity 导入都找不到贴图（灰色 / 红色材质）。
        var fbxAbs = AbsPath(modelAssetPath);
        if (File.Exists(fbxAbs) && ext == ".fbx")
        {
            var texRefs = ExtractFbxTextureReferences(File.ReadAllBytes(fbxAbs));
            if (texRefs.Count > 0)
            {
                var uploadedTextures = new List<CustomRecipeUploadFileDto>();
                foreach (var f in files)
                {
                    if (f == null || ReferenceEquals(f, modelFile))
                        continue;
                    if (f.fileName != null && !IsModelFileName(f.fileName))
                        uploadedTextures.Add(f);
                }
                if (uploadedTextures.Count > 0)
                {
                    var tex = PickTextureFile(uploadedTextures);
                    if (tex != null)
                    {
                        byte[] texBytes = null;
                        try { texBytes = System.Convert.FromBase64String(tex.base64); }
                        catch { }
                        if (texBytes != null && texBytes.Length > 0)
                        {
                            var modelsAbs = AbsPath(modelsDir);
                            foreach (var refName in texRefs)
                            {
                                var relName = refName.Replace('\\', '/');
                                if (relName.StartsWith("/", StringComparison.Ordinal) || relName.Contains(".."))
                                    continue;
                                var targetAbs = Path.GetFullPath(Path.Combine(modelsAbs, relName));
                                if (!targetAbs.StartsWith(modelsAbs, StringComparison.OrdinalIgnoreCase))
                                    continue;
                                var targetDir = Path.GetDirectoryName(targetAbs);
                                if (string.IsNullOrEmpty(targetDir))
                                    continue;
                                Directory.CreateDirectory(targetDir);
                                var refExt = Path.GetExtension(targetAbs).ToLowerInvariant();
                                var uploadedExt = Path.GetExtension(tex.fileName).ToLowerInvariant();
                                var outBytes = texBytes;
                                if (!string.Equals(refExt, uploadedExt, StringComparison.OrdinalIgnoreCase))
                                {
                                    var converted = ConvertImageBytes(texBytes, refExt);
                                    if (converted != null)
                                        outBytes = converted;
                                }
                                File.WriteAllBytes(targetAbs, outBytes);
                            }
                        }
                    }
                }
            }
        }

        AssetDatabase.Refresh();

        // 贴图内嵌由前端在浏览器完成（fbxFuse：FBX + PNG 合成彩色 FBX 后上传）；
        // 旧前端分开上传的贴图仍按 FBX 引用名写入（model.fbm）供预览与 Unity 导入。

        var importedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
        if (importedRoot == null)
            return "模型导入失败，请确认文件格式为 FBX 或 OBJ（贴图请使用 PNG/JPG）。";

        // 覆盖上传时强制重建 prefab：旧 prefab 可能引用已删除的贴图/旧材质（导致灰色）
        var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null)
            AssetDatabase.DeleteAsset(prefabPath);

        // 材质球（与参考做法一致：炒饭/芝士虾球的 prefab 内 MeshRenderer 直接引用 .mat，
        // .mat 的 _MainTex 引用上传贴图）。先建好 .mat 再赋给模型实例。
        Material mat = null;
        if (!string.IsNullOrEmpty(uploadedTexturePath))
        {
            var uploadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(uploadedTexturePath);
            if (uploadedTex != null)
                mat = EnsureRecipeMaterial(modelsDir, recipeId, uploadedTex);
        }

        // 必须先实例化到场景再 CreatePrefab：直接对模型资产 CreatePrefab 会丢失网格与子节点
        // （生成只有根 Transform 的空 prefab，导致无渲染器、材质球与贴图都挂不上）。
        var instance = UnityEngine.Object.Instantiate(importedRoot);
        instance.name = recipeId;
        try
        {
            if (mat != null)
            {
                foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = mat;
            }
            var prefabInstance = PrefabUtility.CreatePrefab(prefabPath, instance);
            if (prefabInstance == null)
                return "创建预制体失败。";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        // modelSO 不再放数据：模型直接引用 prefab（model 字段），运行时 GetModel 优先用 model。
        var modelSOId = Path.GetFileNameWithoutExtension(dto.recipeAssetPath) + "ModelSO";
        var modelSOPath = modelsDir + "/" + modelSOId + ".asset";
        if (AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(modelSOPath) != null)
            AssetDatabase.DeleteAsset(modelSOPath);

        Undo.RecordObject(so, "Set Recipe Model");
        so.modelSO = null;
        so.model = prefab;
        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();

        // 贴图做成材质球与模型做在一起：创建 Standard 材质（_MainTex = 上传贴图）并赋给模型
        ApplyTextureMaterial(so, prefab, modelsDir, uploadedTexturePath);
        // 上传的是带材质的 FBX：诊断材质贴图，缺失时用 FBX 导入的子资产贴图自动修复
        EnsureModelTextures(so, prefab, modelsDir, modelAssetPath);
        ApplyModelTransform(so);
        return null;
    }

    /// <summary>创建/更新菜谱材质球（Standard shader，_MainTex = 指定贴图，白色）。</summary>
    private static Material EnsureRecipeMaterial(string modelsDir, string recipeId, Texture2D tex)
    {
        if (tex == null)
            return null;
        var matPath = modelsDir + "/" + SanitizeName(recipeId) + "_Mat.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            var shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Diffuse");
            if (shader == null)
                return null;
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    /// <summary>上传带材质 FBX 后诊断：若模型材质贴图缺失（Unity 内嵌提取失败/引用断裂），
    ///  用 FBX 导入的子资产贴图（内嵌数据）或 models 目录贴图创建材质修复，避免纯灰色。</summary>
    private static void EnsureModelTextures(CustomRecipeSO so, GameObject prefab, string modelsDir, string modelAssetPath)
    {
        if (prefab == null || string.IsNullOrEmpty(modelAssetPath))
            return;
        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;
        bool needFix = false;
        foreach (var r in renderers)
        {
            if (r.sharedMaterial == null || r.sharedMaterial.mainTexture == null)
            {
                needFix = true;
                break;
            }
        }
        if (!needFix)
            return;

        // 修复源 1：FBX 导入的子资产贴图（内嵌数据提取）
        Texture2D tex = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(modelAssetPath))
        {
            var t = a as Texture2D;
            if (t != null && t.width > 2)
            {
                tex = t;
                break;
            }
        }
        // 修复源 2：models 目录的贴图文件
        if (tex == null && !string.IsNullOrEmpty(modelsDir) && Directory.Exists(AbsPath(modelsDir)))
        {
            foreach (var f in Directory.GetFiles(AbsPath(modelsDir)))
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                    continue;
                var name = Path.GetFileName(f);
                if (name.EndsWith("_Icon.png", StringComparison.OrdinalIgnoreCase))
                    continue;
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(modelsDir + "/" + name);
                if (tex != null)
                    break;
            }
        }
        if (tex == null)
            return;

        var recipeId = SanitizeName(so.recipeName ?? "Recipe");
        var mat = EnsureRecipeMaterial(modelsDir, recipeId, tex);
        if (mat == null)
            return;
        foreach (var r in renderers)
        {
            r.sharedMaterial = mat;
            EditorUtility.SetDirty(r);
        }
        EditorUtility.SetDirty(prefab);
        AssetDatabase.SaveAssets();
    }

    /// <summary>把上传的贴图制成材质球（Standard shader，_MainTex）并赋给模型的全部 MeshRenderer，
    ///  与参考菜谱（炒饭/芝士虾球）做法一致——模型自带材质，游戏内不再出现红/灰材质。</summary>
    private static void ApplyTextureMaterial(CustomRecipeSO so, GameObject prefab, string modelsDir, string texturePath)
    {
        if (prefab == null || string.IsNullOrEmpty(texturePath))
            return;
        var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0)
            return;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (tex == null)
            return;

        var recipeId = SanitizeName(so.recipeName ?? "Recipe");
        var mat = EnsureRecipeMaterial(modelsDir, recipeId, tex);
        if (mat == null)
            return;

        foreach (var r in renderers)
        {
            r.sharedMaterial = mat;
            EditorUtility.SetDirty(r);
        }

        EditorUtility.SetDirty(prefab);
        AssetDatabase.SaveAssets();
    }

    private static bool IsModelFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;
        var n = fileName.ToLowerInvariant();
        return n.EndsWith(".fbx") || n.EndsWith(".obj");
    }

    /// <summary>从上传的贴图文件中挑选彩色主贴图（优先 base_color/color/diffuse/albedo 命名，否则第一张）。</summary>
    private static CustomRecipeUploadFileDto PickTextureFile(List<CustomRecipeUploadFileDto> textures)
    {
        if (textures == null || textures.Count == 0)
            return null;
        foreach (var t in textures)
        {
            var n = (t.fileName ?? "").ToLowerInvariant();
            if (n.Contains("base_color") || n.Contains("color") || n.Contains("diffuse") || n.Contains("albedo") || n.Contains("_texture"))
                return t;
        }
        return textures[0];
    }

    /// <summary>上传文件名白名单：仅保留安全的 base 名与图片/模型扩展名。</summary>
    private static string SanitizeUploadFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrEmpty(name))
            return null;
        var ext = Path.GetExtension(name).ToLowerInvariant();
        var allowed = new[] { ".fbx", ".obj", ".png", ".jpg", ".jpeg", ".tga" };
        if (System.Array.IndexOf(allowed, ext) < 0)
            return null;
        return SanitizeName(Path.GetFileNameWithoutExtension(name)) + ext;
    }

    /// <summary>删除 models 目录内旧模型/贴图（保留图标文件）。</summary>
    private static void ClearModelDirAssets(string dirAssetPath, string keepFileName)
    {
        var abs = AbsPath(dirAssetPath);
        if (!Directory.Exists(abs))
            return;
        var extensions = new[] { ".fbx", ".obj", ".png", ".jpg", ".jpeg", ".tga" };
        foreach (var file in Directory.GetFiles(abs))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".meta", StringComparison.Ordinal))
                continue;
            if (string.Equals(name, keepFileName, StringComparison.OrdinalIgnoreCase))
                continue;
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (System.Array.IndexOf(extensions, ext) < 0)
                continue;
            AssetDatabase.DeleteAsset(dirAssetPath + "/" + name);
        }
    }

    /// <summary>列出菜谱 models 目录内的资源文件（供前端 3D 在线预览）。 */
    public static string[] ListCustomRecipeModelFiles(string recipeAssetPath)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(recipeAssetPath))
            return list.ToArray();
        var dir = DirectoryName(recipeAssetPath) + "/models";
        var abs = AbsPath(dir);
        if (!Directory.Exists(abs))
            return list.ToArray();
        foreach (var file in Directory.GetFiles(abs))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".meta", StringComparison.Ordinal))
                continue;
            var ext = Path.GetExtension(name).ToLowerInvariant();
            if (System.Array.IndexOf(new[] { ".fbx", ".obj", ".png", ".jpg", ".jpeg", ".tga" }, ext) >= 0)
                list.Add(name);
        }
        list.Sort(StringComparer.Ordinal);
        return list.ToArray();
    }

    public static string AddCustomRecipeCategory(CustomRecipeCategoryCreateDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.setName) || string.IsNullOrEmpty(dto.id))
            return "缺少参数。";

        var safeId = SanitizeName(dto.id);
        if (string.IsNullOrEmpty(safeId))
            return "分类ID只能包含字母数字和下划线。";

        var recipesDir = LevelSetsRoot + "/" + dto.setName + "/" + CustomRecipesDir;
        if (!AssetFolderExists(recipesDir))
            return "请先访问自定义菜谱页面。";

        var configPath = recipesDir + "/CustomRecipeConfig.asset";
        var config = AssetDatabase.LoadAssetAtPath<CustomRecipeConfigSO>(configPath);
        if (config == null)
            return "配置文件丢失。";

        var cats = config.categories != null ? new List<CustomRecipeConfigSO.CustomRecipeCategoryEntry>(config.categories) : new List<CustomRecipeConfigSO.CustomRecipeCategoryEntry>();
        if (cats.Exists(c => c.id == safeId))
            return "分类已存在。";

        AssetDatabase.CreateFolder(recipesDir, safeId);
        cats.Add(new CustomRecipeConfigSO.CustomRecipeCategoryEntry { id = safeId, zh = dto.zh ?? safeId, en = dto.en ?? safeId });
        config.categories = cats.ToArray();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return null;
    }

    public static string RenameCustomRecipeCategory(CustomRecipeCategoryRenameDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.setName) || string.IsNullOrEmpty(dto.oldId) || string.IsNullOrEmpty(dto.newId))
            return "缺少参数。";

        var recipesDir = LevelSetsRoot + "/" + dto.setName + "/" + CustomRecipesDir;
        var oldDir = recipesDir + "/" + dto.oldId;
        if (!AssetFolderExists(oldDir))
            return "原分类不存在。";

        var newSafe = SanitizeName(dto.newId);
        if (string.IsNullOrEmpty(newSafe))
            return "新分类ID非法。";

        if (dto.oldId == newSafe)
        {
            UpdateCategoryDisplay(dto.setName, dto.oldId, dto.newZh, dto.newEn);
            return null;
        }

        var newDir = recipesDir + "/" + newSafe;
        if (AssetFolderExists(newDir))
            return "新分类ID已存在。";

        var err = AssetDatabase.MoveAsset(oldDir, newDir);
        if (!string.IsNullOrEmpty(err))
            return err;

        var configPath = recipesDir + "/CustomRecipeConfig.asset";
        var config = AssetDatabase.LoadAssetAtPath<CustomRecipeConfigSO>(configPath);
        if (config != null)
        {
            var cats = new List<CustomRecipeConfigSO.CustomRecipeCategoryEntry>(config.categories ?? new CustomRecipeConfigSO.CustomRecipeCategoryEntry[0]);
            for (int i = 0; i < cats.Count; i++)
            {
                if (cats[i].id == dto.oldId)
                {
                    cats[i] = new CustomRecipeConfigSO.CustomRecipeCategoryEntry
                    {
                        id = newSafe,
                        zh = dto.newZh ?? newSafe,
                        en = dto.newEn ?? newSafe
                    };
                    break;
                }
            }
            config.categories = cats.ToArray();
            EditorUtility.SetDirty(config);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return null;
    }

    private static void UpdateCategoryDisplay(string setName, string catId, string zh, string en)
    {
        var recipesDir = LevelSetsRoot + "/" + setName + "/" + CustomRecipesDir;
        var configPath = recipesDir + "/CustomRecipeConfig.asset";
        var config = AssetDatabase.LoadAssetAtPath<CustomRecipeConfigSO>(configPath);
        if (config == null)
            return;

        var cats = new List<CustomRecipeConfigSO.CustomRecipeCategoryEntry>(config.categories ?? new CustomRecipeConfigSO.CustomRecipeCategoryEntry[0]);
        for (int i = 0; i < cats.Count; i++)
        {
            if (cats[i].id == catId)
            {
                cats[i] = new CustomRecipeConfigSO.CustomRecipeCategoryEntry { id = catId, zh = zh ?? catId, en = en ?? catId };
                break;
            }
        }
        config.categories = cats.ToArray();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
    }

    public static string DeleteCustomRecipeCategory(CustomRecipeCategoryDeleteDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.setName) || string.IsNullOrEmpty(dto.category))
            return "缺少参数。";

        var recipesDir = LevelSetsRoot + "/" + dto.setName + "/" + CustomRecipesDir;
        var categoryDir = recipesDir + "/" + dto.category;
        if (!AssetFolderExists(categoryDir))
            return "分类不存在。";

        var usingLevels = new List<string>();
        var dataDir = LevelSetsRoot + "/" + dto.setName + "/data";
        if (AssetFolderExists(dataDir))
        {
            foreach (var guid in AssetDatabase.FindAssets("t:LevelInfoSO", new[] { dataDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(path);
                if (info == null || info.recipes == null)
                    continue;

                foreach (var r in info.recipes)
                {
                    if (r == null) continue;
                    var rp = AssetDatabase.GetAssetPath(r);
                    if (rp == null) continue;
                    if (rp.StartsWith(categoryDir + "/", StringComparison.Ordinal))
                    {
                        usingLevels.Add(info.levelName ?? Path.GetFileName(DirectoryName(path)));
                        break;
                    }
                }
            }
        }

        if (usingLevels.Count > 0)
            return "分类「" + dto.category + "」正被以下关卡使用：\n" + string.Join("、", usingLevels.ToArray());

        if (!AssetDatabase.DeleteAsset(categoryDir))
            return "删除分类文件夹失败。";

        var configPath = recipesDir + "/CustomRecipeConfig.asset";
        var config = AssetDatabase.LoadAssetAtPath<CustomRecipeConfigSO>(configPath);
        if (config != null)
        {
            var cats = new List<CustomRecipeConfigSO.CustomRecipeCategoryEntry>(config.categories ?? new CustomRecipeConfigSO.CustomRecipeCategoryEntry[0]);
            cats.RemoveAll(c => c.id == dto.category);
            config.categories = cats.ToArray();
            EditorUtility.SetDirty(config);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return null;
    }

    private static bool GloballyUniqueRecipeName(string recipeName)
    {
        if (string.IsNullOrEmpty(recipeName))
            return false;

        foreach (var asset in ScanCustomRecipeAssets(LevelSetsRoot))
        {
            var id = Path.GetFileNameWithoutExtension(asset.assetPath);
            if (string.Equals(id, recipeName, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static bool IsUidConflicting(int uid)
    {
        var assets = new List<AssetRef>();
        assets.AddRange(ScanCustomRecipeAssets(LevelSetsRoot));
        if (AssetFolderExists(CommonCustomRecipesDir))
            assets.AddRange(ScanCustomRecipeAssets(CommonCustomRecipesDir));
        foreach (var asset in assets)
        {
            var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(asset.assetPath);
            if (so != null && so.uID == uid)
                return true;
        }
        return false;
    }

    /// <summary>PseudoPrefabSO 的查找目录（食材/烹饪步骤/装盘容器）。</summary>
    private static readonly string[] PseudoPrefabSearchFolders =
    {
        "Assets/common01/food/Ingredients",
        "Assets/common02/food/Ingredients",
        "Assets/common01/food/CookingSteps",
        "Assets/common02/food/CookingSteps",
        "Assets/common01/food/PlatingSteps",
        "Assets/common02/food/PlatingSteps",
    };

    /// <summary>全项目自定义菜谱查找目录（官方 + 全部关卡集）。</summary>
    private static List<string> CustomRecipeSearchFolders()
    {
        var folders = new List<string>();
        if (AssetFolderExists(CommonCustomRecipesDir))
            folders.Add(CommonCustomRecipesDir);
        var setsRoot = LevelSetsRoot;
        if (Directory.Exists(AbsPath(setsRoot)))
        {
            foreach (var dir in Directory.GetDirectories(AbsPath(setsRoot)))
            {
                var setName = Path.GetFileName(dir);
                var cr = setsRoot + "/" + setName + "/" + CustomRecipesDir;
                if (AssetFolderExists(cr))
                    folders.Add(cr);
            }
        }
        return folders;
    }

    private static ScriptableObject FindAssetByIdInFolders(string id, string scriptGuid, IList<string> folders)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (var folder in folders)
        {
            foreach (var asset in ScanAssetsByScript(folder, scriptGuid))
            {
                var name = Path.GetFileNameWithoutExtension(asset.assetPath);
                if (string.Equals(name, id, StringComparison.Ordinal))
                    return AssetDatabase.LoadAssetAtPath<ScriptableObject>(asset.assetPath);
            }
        }
        return null;
    }

    private static ScriptableObject FindPseudoPrefabOrCustomRecipe(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        var found = FindAssetByIdInFolders(id, PseudoPrefabScriptGuid, PseudoPrefabSearchFolders);
        if (found != null)
            return found;

        return FindAssetByIdInFolders(id, CustomRecipeScriptGuid, CustomRecipeSearchFolders());
    }

    private static PseudoPrefabSO FindPseudoPrefabById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        var so = FindAssetByIdInFolders(id, PseudoPrefabScriptGuid, PseudoPrefabSearchFolders);
        return so as PseudoPrefabSO;
    }

    /// <summary>装盘容器专用查找：仅限 PlatingSteps 目录、精确文件名匹配，
    ///  防止模糊匹配选到 pseudo_prefab_so 等目录的错误资产。</summary>
    private static PseudoPrefabSO FindPlatingContainerById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        var folders = new[]
        {
            "Assets/common01/food/PlatingSteps",
            "Assets/common02/food/PlatingSteps"
        };
        var so = FindAssetByIdInFolders(id, PseudoPrefabScriptGuid, folders);
        return so as PseudoPrefabSO;
    }

    /// <summary>按菜谱 id 找其模型（modelPrefabId 可能是 modelSO 名或来源菜谱 id，
    ///  兼容历史上传的任意命名）。</summary>
    /// <summary>按菜谱 id / prefab 文件名找其模型（modelPrefabId 可为菜谱 id 或 prefab 文件名）。</summary>
    private static GameObject FindCustomRecipeModelByRecipeId(string recipeId)
    {
        if (string.IsNullOrEmpty(recipeId))
            return null;

        foreach (var folder in CustomRecipeSearchFolders())
        {
            foreach (var asset in ScanCustomRecipeAssets(folder))
            {
                var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(asset.assetPath);
                if (so == null || so.model == null)
                    continue;
                var id = Path.GetFileNameWithoutExtension(asset.assetPath);
                var modelPath = AssetDatabase.GetAssetPath(so.model);
                var prefabName = string.IsNullOrEmpty(modelPath)
                    ? null
                    : Path.GetFileNameWithoutExtension(modelPath);
                if (string.Equals(id, recipeId, StringComparison.Ordinal) ||
                    string.Equals(so.recipeName, recipeId, StringComparison.Ordinal) ||
                    (prefabName != null && string.Equals(prefabName, recipeId, StringComparison.Ordinal)))
                    return so.model;
            }
        }
        return null;
    }

    private static void AddCustomRecipeName(string setName, string id, string zh, string en)
    {
        var namesPath = LevelSetsRoot + "/" + setName + "/" + CustomRecipesDir + "/names.json";
        var abs = AbsPath(namesPath);
        CustomNamesDoc doc;
        try
        {
            if (File.Exists(abs))
            {
                var text = File.ReadAllText(abs);
                doc = JsonUtility.FromJson<CustomNamesDoc>(text) ?? new CustomNamesDoc { schemaVersion = 1, names = new CustomNamesEntry[0] };
            }
            else
            {
                doc = new CustomNamesDoc { schemaVersion = 1, names = new CustomNamesEntry[0] };
            }
        }
        catch
        {
            doc = new CustomNamesDoc { schemaVersion = 1, names = new CustomNamesEntry[0] };
        }

        var list = new List<CustomNamesEntry>(doc.names ?? new CustomNamesEntry[0]);
        list.RemoveAll(e => e.id == id);
        list.Add(new CustomNamesEntry { id = id, zh = zh ?? "", en = en ?? id });
        list.Sort((a, b) => string.Compare(a.id, b.id, StringComparison.Ordinal));

        doc.names = list.ToArray();
        var json = JsonUtility.ToJson(doc, true);
        File.WriteAllText(abs, json, System.Text.Encoding.UTF8);
        AssetDatabase.Refresh();
    }

    private static void UpdateCustomRecipeName(string setName, string id, string zh, string en)
    {
        AddCustomRecipeName(setName, id, zh, en);
    }

    private static void RemoveCustomRecipeName(string setName, string id)
    {
        var namesPath = LevelSetsRoot + "/" + setName + "/" + CustomRecipesDir + "/names.json";
        var abs = AbsPath(namesPath);
        if (!File.Exists(abs))
            return;

        try
        {
            var text = File.ReadAllText(abs);
            var doc = JsonUtility.FromJson<CustomNamesDoc>(text);
            if (doc == null || doc.names == null)
                return;

            var list = new List<CustomNamesEntry>(doc.names);
            list.RemoveAll(e => e.id == id);
            doc.names = list.ToArray();

            var json = JsonUtility.ToJson(doc, true);
            File.WriteAllText(abs, json, System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();
        }
        catch { }
    }

    /** Resolve a project-relative asset path (e.g. Assets/LevelSets/x/data/y.png)
     *  to an absolute file path + content type. Only allows paths under the
     *  project Assets folder, to avoid arbitrary file reads. */
    public static string ResolveProjectFile(string relPath, out string absPath, out string contentType)
    {
        absPath = null;
        contentType = "application/octet-stream";
        if (string.IsNullOrEmpty(relPath))
            return "缺少路径。";
        relPath = relPath.Replace('\\', '/').Trim();
        if (!relPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            return "只允许访问 Assets 目录下的文件。";
        // Block path traversal.
        if (relPath.Contains(".."))
            return "非法路径。";

        absPath = Path.GetFullPath(Path.Combine(Path.Combine(Application.dataPath, ".."), relPath));
        var ext = Path.GetExtension(absPath).ToLowerInvariant();
        switch (ext)
        {
            case ".png": contentType = "image/png"; break;
            case ".jpg":
            case ".jpeg": contentType = "image/jpeg"; break;
            case ".svg": contentType = "image/svg+xml"; break;
            case ".gif": contentType = "image/gif"; break;
            case ".webp": contentType = "image/webp"; break;
            default: contentType = "application/octet-stream"; break;
        }
        return null;
    }
}
