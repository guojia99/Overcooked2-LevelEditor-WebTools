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
            ambienceLabels = k.ambienceLabels
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
            ambienceLabels = new AmbienceLabelDto[0]
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
