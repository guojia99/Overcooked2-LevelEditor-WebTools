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
    internal const string LevelSetsRoot = "Assets/LevelSets";
    private const string TemplateScene = "Assets/Template/s_template.unity";
    private const string TemplateConfig1p = "Assets/Template/config_1p.asset";
    private const string TemplateConfig2p = "Assets/Template/config_2p.asset";
    private const string TemplateConfig3p = "Assets/Template/config_3p.asset";
    private const string TemplateConfig4p = "Assets/Template/config_4p.asset";
    private const string TemplateLevelInfo = "Assets/Template/levelinfo_template.asset";
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
            availableAmbiences = k.availableAmbiences,
            directoryEvents = k.directoryEvents,
            themes = k.themes,
            deathThemes = k.deathThemes,
            ambienceLabels = k.ambienceLabels,
            itemAudioRules = k.itemAudioRules
        };
    }

    /// <summary>audio-knowledge.json 的 availableAmbiences 记录了所有实际存在于至少一个
    /// AudioDirectoryData 的 GameLoopingAudioTag。枚举里另有 6 个死值（WashingUp/Sizzling 等）
    /// 没有任何音频资源，被选进 inLevelAmbiences 后运行时宿主 AudioManager.FindEntry 会对
    /// 空列表取下标直接越界崩溃。返回 null 表示知识库未提供（不过滤）。</summary>
    public static HashSet<string> GetAvailableAmbiences()
    {
        var k = LoadAudioKnowledge();
        if (k.availableAmbiences == null || k.availableAmbiences.Length == 0)
            return null;
        var set = new HashSet<string>();
        foreach (var n in k.availableAmbiences)
            set.Add(n);
        return set;
    }

    /// <summary>剔除 inLevelAmbiences 中没有任何 AudioDirectoryData 条目的死枚举值
    /// （否则运行时 AudioManager.FindEntry 越界）。返回被移除的名字，无知识库时不动。</summary>
    public static List<string> StripInvalidAmbiences(LevelInfoSO info)
    {
        if (info == null || info.inLevelAmbiences == null || info.inLevelAmbiences.Length == 0)
            return null;
        var available = GetAvailableAmbiences();
        if (available == null)
            return null;
        var kept = new List<LevelInfoSO.GameLoopingAudioTag>();
        var removed = new List<string>();
        foreach (var t in info.inLevelAmbiences)
        {
            if (available.Contains(t.ToString()))
                kept.Add(t);
            else
                removed.Add(t.ToString());
        }
        if (removed.Count > 0)
            info.inLevelAmbiences = kept.ToArray();
        return removed.Count > 0 ? removed : null;
    }

    public static AmbienceCatalogDto ScanAmbiences()
    {
        var names = new List<string>(Enum.GetNames(typeof(LevelInfoSO.GameLoopingAudioTag)));
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
            // 自动修复：历史关卡集可能漏设根目录 AssetBundle（Docs/zh 构建步骤 3），
            // 列表时补齐，保证下次 Build AssetBundles 能打出 info_<set>。
            EnsureSetInfoBundle(setName);
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
        // 通用内容（Assets/common03）直接引用、随 common03 bundle 打包，
        // 不再同步 custom_web 拷贝（SyncAllWebContent 已退役为 no-op）。
        LayoutEditorCustomIngredients.SyncAllWebContent(setName);
        // Docs/zh 构建步骤 3：关卡集根目录 AssetBundle = "<set>/info_<set>"
        EnsureSetInfoBundle(setName);
        ReloadPseudo();
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

    /// <summary>确保关卡集根目录的 AssetBundle 名为 "&lt;set&gt;/info_&lt;set&gt;"（Docs/zh 构建步骤 3）。
    ///  缺它则 info bundle 不打包，Assets/AssetBundles/&lt;set&gt;/ 下没有 info_&lt;set&gt;，
    ///  游戏加载不到关卡集配置（LevelSetInfo/LevelInfo/config 等 data/ 下资产）。
    ///  幂等且保守：只补**空值**（历史关卡集漏设时），已设置过的（含历史 test_level 等旧名）不改。</summary>
    public static void EnsureSetInfoBundle(string setName)
    {
        if (string.IsNullOrEmpty(setName))
            return;
        var setDir = LevelSetsRoot + "/" + setName;
        if (!AssetDatabase.IsValidFolder(setDir))
            return;
        var importer = AssetImporter.GetAtPath(setDir);
        if (importer == null)
            return;
        if (!string.IsNullOrEmpty(importer.assetBundleName))
            return;
        var expected = setName + "/info_" + setName;
        importer.assetBundleName = expected;
        importer.SaveAndReimport();
        Debug.Log("[LevelAdmin] 关卡集 AssetBundle 已设为 " + expected);
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

        // 按 LevelSetInfo.levelInfos 的记录顺序（即创建顺序）排列；
        // 未登记进 levelInfos 的游离关卡排在末尾，回退为 levelName 字典序。
        var orderMap = new Dictionary<string, int>();
        var setInfo = FindSetInfo(setName);
        if (setInfo != null && setInfo.levelInfos != null)
        {
            for (int i = 0; i < setInfo.levelInfos.Length; i++)
            {
                var li = setInfo.levelInfos[i];
                if (li == null)
                    continue;
                var p = AssetDatabase.GetAssetPath(li);
                if (!string.IsNullOrEmpty(p) && !orderMap.ContainsKey(p))
                    orderMap.Add(p, i);
            }
        }

        list.Sort((a, b) =>
        {
            int ia;
            int ib;
            if (!orderMap.TryGetValue(a.assetPath, out ia)) ia = int.MaxValue;
            if (!orderMap.TryGetValue(b.assetPath, out ib)) ib = int.MaxValue;
            if (ia != ib) return ia.CompareTo(ib);
            return string.Compare(a.levelName, b.levelName, StringComparison.Ordinal);
        });
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

        // 网格半宽：场景 GridManager 为唯一权威存储（LevelEditorStub 共享程序集禁改，
        // 2026-09-12 起 LevelInfoSO 不再持有该配置）——直接读场景磁盘生效值
        // （prefab 覆盖 / env prefab 默认值），关卡配置弹窗显示即真值。
        int gridHalfX, gridHalfZ;
        LayoutEditorGridBake.ReadSceneMainGridHalfSize(sceneAssetPath, out gridHalfX, out gridHalfZ);

        string summaryBgPath;
        float summaryBgDim;
        ReadSummaryBg(assetPath, out summaryBgPath, out summaryBgDim);

        var dto = new LevelDetailDto
        {
            levelInfoAssetPath = assetPath,
            levelName = so.levelName ?? "",
            levelNameZH = so.levelNameZH ?? "",
            sceneName = sceneName,
            sceneAssetPath = sceneAssetPath,
            hasScreenshot = so.screenshot != null,
            screenshotPath = so.screenshot != null ? AssetDatabase.GetAssetPath(so.screenshot) : "",
            summaryBgPath = summaryBgPath,
            summaryBgDim = summaryBgDim,
            debugRecipeCount = so.debugRecipeCount,
            disableDynamicParenting = so.disableDynamicParenting,
            minOrderCount = ClampOrderCount(so.minOrderCount, 2),
            maxOrderCount = ClampOrderCount(so.maxOrderCount, 5),
            gridHalfSizeX = gridHalfX,
            gridHalfSizeZ = gridHalfZ,
            dependencies = so.dependencies != null ? (string[])so.dependencies.Clone() : new string[0],
            configs = new[]
            {
                ConfigToDto(so.config_1p),
                ConfigToDto(so.config_2p),
                ConfigToDto(so.config_3p),
                ConfigToDto(so.config_4p)
            },
            audio = ReadAudioFromLevelInfo(so)
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
        var visited = new HashSet<UnityEngine.Object>();
        foreach (var obj in refs)
        {
            if (obj == null)
                continue;
            CollectBundleNamesDeep(obj as UnityEngine.Object, set, visited);
        }
    }

    /// <summary>递归收集 SO 引用闭包里的 bundleName：PseudoPrefabSO 读自身字段即可；
    /// CustomRecipeSO 系（含 OptionalBurger/OptionalPizza 子类）无 bundleName 字段，
    /// 需继续下钻 composition/optional/model/platingStep/cookingStep/bun/ingredientModel
    /// 等嵌套引用（含嵌套 CustomRecipeSO），否则 modelSO 指向的 dlc bundle 不会报 missing。</summary>
    private static void CollectBundleNamesDeep(UnityEngine.Object obj, HashSet<string> set, HashSet<UnityEngine.Object> visited)
    {
        if (obj == null || !visited.Add(obj))
            return;
        var bn = ReadBundleNameField(obj);
        if (!string.IsNullOrEmpty(bn))
        {
            set.Add(bn);
            return; // PseudoPrefabSO 是叶子：其余字段只有名字字符串
        }
        if (!(obj is CustomRecipeSO))
            return;
        var fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < fields.Length; i++)
        {
            var ft = fields[i].FieldType;
            if (typeof(ScriptableObject).IsAssignableFrom(ft))
            {
                CollectBundleNamesDeep(fields[i].GetValue(obj) as UnityEngine.Object, set, visited);
            }
            else if (ft.IsArray && typeof(ScriptableObject).IsAssignableFrom(ft.GetElementType()))
            {
                var arr = fields[i].GetValue(obj) as UnityEngine.Object[];
                if (arr == null)
                    continue;
                for (int j = 0; j < arr.Length; j++)
                    CollectBundleNamesDeep(arr[j], set, visited);
            }
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

        // 2026-09-22 起关卡 id 不再自带 s_ 前缀：场景名 = levelId（旧关卡的 s_ 历史名保留，
        // 可用 /api/level/rename 重命名时统一迁移到新约定）。
        var sceneName = levelId;
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
        ApplyTemplateAudioDefaults(info);
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

        // 网格半宽：场景 GridManager 为唯一权威存储（LevelInfoSO 禁改，2026-09-12）。
        // 值有变化时要求该关卡场景当前在编辑器中打开并即时烘焙进场景 prefab 覆盖；
        // 场景未打开则整体拒绝（在任何字段写入之前返回，保证请求原子性）。
        var gridErr = ApplyGridHalfSizeChange(dto, so);
        if (gridErr != null)
            return gridErr;

        Undo.RecordObject(so, "Edit Level Info");
        so.levelName = dto.levelName ?? so.levelName;
        so.levelNameZH = dto.levelNameZH ?? so.levelNameZH;
        // sceneName 不再支持表单直改（直改字符串不挪文件会导致场景失联），
        // 统一走 /api/level/rename 原子改名（2026-09-22）。
        so.debugRecipeCount = dto.debugRecipeCount;
        so.disableDynamicParenting = dto.disableDynamicParenting;
        so.minOrderCount = ClampOrderCount(dto.minOrderCount, so.minOrderCount);
        so.maxOrderCount = ClampOrderCount(dto.maxOrderCount, so.maxOrderCount);
        if (so.minOrderCount > so.maxOrderCount)
            so.maxOrderCount = so.minOrderCount;
        so.dependencies = dto.dependencies != null ? (string[])dto.dependencies.Clone() : so.dependencies;
        EditorUtility.SetDirty(so);

        var stub = UnityEngine.Object.FindObjectOfType<PseudoPrefabManagerStub>();
        if (stub != null && stub.levelInfo == so)
            EditorUtility.SetDirty(stub);

        AssetDatabase.SaveAssets();

        ReloadPseudo();
        return null;
    }

    /// <summary>网格半宽变更应用（场景 GridManager 为唯一权威存储，LevelInfoSO 禁改）。
    /// dto 值与场景磁盘生效值一致 = 无变更，放行；有变更则要求该关卡场景当前已打开，
    /// 即时烘焙为场景 prefab 覆盖并落盘；未打开返回错误文案（调用方在任何字段写入前
    /// 调用本方法，保证请求原子性）。</summary>
    private static string ApplyGridHalfSizeChange(LevelInfoUpdateDto dto, LevelInfoSO so)
    {
        var wantX = Mathf.Clamp(dto.gridHalfSizeX, 0, 50);
        var wantZ = Mathf.Clamp(dto.gridHalfSizeZ, 0, 50);
        var setName = SetNameFromPath(dto.assetPath);
        var sceneAssetPath = "";
        if (!string.IsNullOrEmpty(so.sceneName))
            sceneAssetPath = LevelSetsRoot + "/" + setName + "/scenes/" + so.sceneName + ".unity";

        int curX, curZ;
        LayoutEditorGridBake.ReadSceneMainGridHalfSize(sceneAssetPath, out curX, out curZ);
        if (wantX == curX && wantZ == curZ)
            return null; // 无变更

        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(sceneAssetPath);
        if (!scene.IsValid() || !scene.isLoaded)
            return "网格半宽有修改，但网格值直接存储在场景 GridManager 上（不再放 LevelInfoSO），"
                + "而该关卡场景当前未在编辑器中打开——请先打开关卡场景再保存；其他设置未改动。";

        var warn = LayoutEditorGridBake.BakeSceneHalfSize(scene, wantX, wantZ);
        if (!string.IsNullOrEmpty(warn))
            return warn;
        LayoutEditorLog.Log("[LayoutEditor] 网格半宽已烘焙进场景: " + sceneAssetPath
            + "（" + curX + "," + curZ + " → " + wantX + "," + wantZ + "）");
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
        // 音频配置已迁移到 LevelInfoSO；场景里的 stub 仅保留 levelInfo 引用。
        var info = LayoutEditorLevelInfoResolver.ResolveForScene(dto.sceneAssetPath);
        if (info == null)
            return "未找到该场景对应的 LevelInfoSO，请先为关卡绑定 LevelInfo。";

        // 自定义 BGM：customMusicFile 非空 → 直引 data/bgm/ 下的 clip（无 SO 包装，
        // 随 info_<set> bundle 打包导出），并清空 inLevelMusicSO（互斥）；
        // 空 → 回到原 SO 引用通道并清空直引。
        AudioClip customClip = null;
        if (!string.IsNullOrEmpty(dto.customMusicFile))
        {
            var setNameForBgm = SetNameOfAssetPath(AssetDatabase.GetAssetPath(info));
            customClip = LoadCustomMusicClip(setNameForBgm, dto.customMusicFile);
            if (customClip == null)
                return "自定义 BGM 文件不存在：" + dto.customMusicFile + "（set=" + setNameForBgm + "）";
        }

        Undo.RecordObject(info, "Edit Level Audio");
        info.inLevelMusic = customClip;
        info.inLevelMusicSO = customClip != null ? null : LoadPseudoByGuid(dto.inLevelMusicGuid);

        var ambList = new List<LevelInfoSO.GameLoopingAudioTag>();
        var ambAvailable = GetAvailableAmbiences();
        if (dto.ambiences != null)
        {
            foreach (var name in dto.ambiences)
            {
                if (string.IsNullOrEmpty(name) || name == "COUNT")
                    continue;
                // 无任何 AudioDirectoryData 条目的死枚举值会让运行时 FindEntry 越界，直接拒绝
                if (ambAvailable != null && !ambAvailable.Contains(name))
                {
                    Debug.LogWarning("[LayoutEditor] dropped invalid ambience (no audio resource): " + name);
                    continue;
                }
                try
                {
                    var t = (LevelInfoSO.GameLoopingAudioTag)Enum.Parse(
                        typeof(LevelInfoSO.GameLoopingAudioTag), name);
                    ambList.Add(t);
                }
                catch
                {
                }
            }
        }
        info.inLevelAmbiences = ambList.ToArray();

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
        info.audioDirectorySOs = dirList.ToArray();

        info.onDeathEffectSO = LoadPseudoByGuid(dto.onDeathEffectGuid);

        AutoMergeAudioDependencies(info);

        EditorUtility.SetDirty(info);
        ForcePrepareForBuilding();
        AssetDatabase.SaveAssets();
        ReloadPseudo();
        return null;
    }

    /// <summary>Ensures LevelInfoSO.dependencies can actually load the bundles required by the currently
    /// referenced audio SOs (BGM + directories + death effect). Only adds a bundle when it is NOT already
    /// reachable via the transitive closure of the existing dependencies (so commonly-loaded bundles like
    /// bundle9/16/18/47 and DLC bundles that already fall inside closure(bundle47) are NOT added). Authoritative
    /// bundle names are read directly from each PseudoPrefabSO.bundleName. Additive only.</summary>
    private static void AutoMergeAudioDependencies(LevelInfoSO info)
    {
        if (info == null)
            return;
        var k = LoadAudioKnowledge();
        var always = new HashSet<string>(k.alwaysLoadedBundles ?? new string[0], StringComparer.Ordinal);

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        // bundle47 (and any other baseBundles) is the foundation that every level must load; always ensure it.
        foreach (var b in k.baseBundles ?? new string[0])
            referenced.Add(b);
        // 旧关卡的 LevelInfoSO 可能缺少音频字段，全部做空值防御。
        if (info.inLevelMusicSO != null && !string.IsNullOrEmpty(info.inLevelMusicSO.bundleName))
            referenced.Add(info.inLevelMusicSO.bundleName);
        if (info.onDeathEffectSO != null && !string.IsNullOrEmpty(info.onDeathEffectSO.bundleName))
            referenced.Add(info.onDeathEffectSO.bundleName);
        if (info.audioDirectorySOs != null)
            foreach (var d in info.audioDirectorySOs)
                if (d != null && !string.IsNullOrEmpty(d.bundleName))
                    referenced.Add(d.bundleName);
        referenced.RemoveWhere(b => always.Contains(b) || string.IsNullOrEmpty(b));
        if (referenced.Count == 0)
            return;

        var deps = new List<string>(info.dependencies ?? new string[0]);
        var loaded = BundleClosure(deps);
        bool changed = false;
        foreach (var b in referenced)
        {
            // Add only when the bundle is genuinely NOT reachable from the current dependencies.
            // Thanks to bundle47's transitive closure this is usually a no-op; the typical
            // exceptions are bundle47 itself (if removed) and bundle11 (raft BGM, not in closure(bundle47)).
            // 只补入磁盘上已存在的 bundle，避免宿主原版 PseudoPrefabManager 场景打开时崩溃。
            if (loaded.Contains(b) || deps.Contains(b) || !LayoutEditorCatalogApi.BundleFileExists(b))
                continue;
            deps.Add(b);
            // extending the dependency list can only grow the closure; add this bundle's own closure.
            foreach (var c in BundleClosure(new[] { b }))
                loaded.Add(c);
            changed = true;
        }
        if (changed)
        {
            Undo.RecordObject(info, "Auto-merge audio dependencies");
            info.dependencies = deps.ToArray();
            EditorUtility.SetDirty(info);
        }
    }

    /// <summary>公开入口：合并当前音频引用（BGM/死亡音效/音频目录）所需的 bundle 依赖。
    /// 回写流程在 Fill All AudioDirectorySOs 之后调用，保证新增的 DLC 音频目录 bundle
    /// 已写入 dependencies，运行时宿主可正常加载。</summary>
    public static void MergeAudioDependencies(LevelInfoSO info)
    {
        AutoMergeAudioDependencies(info);
    }

    // ==================== Custom BGM (data/bgm/) ====================
    // 自定义关卡 BGM：.ogg/.wav 文件存 Assets/LevelSets/<set>/data/bgm/，
    // 由 LevelInfoSO.inLevelMusic 直引（无 PseudoPrefabSO 包装），随关卡集
    // 根目录 bundle（<set>/info_<set>）自动打包导出，真机经 OC2DIYLevel 加载。
    // 编辑器 Play 优先级：PseudoPrefabManager 取 inLevelMusic 直引 > inLevelMusicSO。

    /** 自定义 BGM 在关卡集内的目录（相对 set 根，asset path 形式）。 */
    private const string CustomBgmRelFolder = "data/bgm";
    /** 单文件上传上限（字节）。 */
    public const long CustomBgmMaxBytes = 20L * 1024 * 1024;

    private static string CustomBgmDirAssetPath(string setName)
    {
        return LevelSetsRoot + "/" + setName + "/" + CustomBgmRelFolder;
    }

    /// <summary>"Assets/LevelSets/&lt;set&gt;/..." → &lt;set&gt;；不在 LevelSets 下返回 ""。</summary>
    private static string SetNameOfAssetPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "";
        var prefix = LevelSetsRoot + "/";
        if (!assetPath.StartsWith(prefix, StringComparison.Ordinal))
            return "";
        var rest = assetPath.Substring(prefix.Length);
        var slash = rest.IndexOf('/');
        return slash > 0 ? rest.Substring(0, slash) : rest;
    }

    /// <summary>列出关卡集 data/bgm/ 下全部自定义 BGM（含时长/大小/引用状态）。</summary>
    public static CustomMusicListDto ListCustomMusic(string setName)
    {
        var dto = new CustomMusicListDto { files = new CustomMusicEntryDto[0] };
        if (string.IsNullOrEmpty(setName))
            return dto;
        var dir = CustomBgmDirAssetPath(setName);
        if (!AssetDatabase.IsValidFolder(dir))
            return dto;

        var list = new List<CustomMusicEntryDto>();
        var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { dir });
        foreach (var guid in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var fileName = Path.GetFileName(assetPath);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            var abs = AbsPath(assetPath);
            long size = File.Exists(abs) ? new FileInfo(abs).Length : 0;
            list.Add(new CustomMusicEntryDto
            {
                fileName = fileName,
                name = Path.GetFileNameWithoutExtension(fileName),
                sizeBytes = size,
                lengthSec = clip != null ? clip.length : 0f,
                used = FindCustomMusicUsers(setName, assetPath).Count > 0
            });
        }
        list.Sort((a, b) => string.CompareOrdinal(a.fileName, b.fileName));
        dto.files = list.ToArray();
        return dto;
    }

    /// <summary>保存自定义 BGM 字节到 data/bgm/&lt;name&gt;.ogg|.wav 并导入工程。
    /// 返回 null 成功（result 给出导入结果），否则返回错误信息。</summary>
    public static string SaveCustomMusic(string setName, string rawFileName, byte[] data, out CustomMusicUploadResultDto result)
    {
        result = null;
        if (string.IsNullOrEmpty(setName) || SanitizeName(setName) != setName)
            return "关卡集标识非法：" + setName;
        if (data == null || data.Length == 0)
            return "上传内容为空。";
        if (data.Length > CustomBgmMaxBytes)
            return "文件超过上限 20MB。";
        var ext = Path.GetExtension(rawFileName ?? "").ToLowerInvariant();
        if (ext != ".ogg" && ext != ".wav")
            return "仅支持 .ogg / .wav 落盘（请在网页端压缩转码）。";

        var baseName = SanitizeName(Path.GetFileNameWithoutExtension(rawFileName));
        if (string.IsNullOrEmpty(baseName))
            return "文件名非法（仅保留字母数字下划线后为空）。";
        var fileName = baseName + ext;

        var setDir = LevelSetsRoot + "/" + setName;
        if (!AssetDatabase.IsValidFolder(setDir))
            return "关卡集不存在：" + setName;
        // 逐级建 data/bgm 目录（data 正常已存在）
        var dir = setDir;
        foreach (var part in CustomBgmRelFolder.Split('/'))
        {
            var child = dir + "/" + part;
            if (!AssetDatabase.IsValidFolder(child))
                AssetDatabase.CreateFolder(dir, part);
            dir = child;
        }

        var assetPath = CustomBgmDirAssetPath(setName) + "/" + fileName;
        var abs = AbsPath(assetPath);
        File.WriteAllBytes(abs, data);
        // 新文件入库：ForceSynchronousImport 保证随后的 AudioImporter / LoadAssetAtPath 可用
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 导入设置：BGM 用流式加载 + Vorbis（WAV 保持 PCM），后台加载防主线程卡顿。
        var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
        if (importer == null)
            return "导入失败：Unity 无法识别该音频文件（" + fileName + "）。";
        importer.forceToMono = false;
        importer.loadInBackground = true;
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = ext == ".ogg" ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
        // bundle 归属与 data/ 其余资产一致：读取关卡集根目录 meta 的实际 bundle 名
        // （历史关卡集可能是 test_level/info_test_level 等旧名，不能假设 <set>/info_<set>），
        // 显式写到文件 meta 上，保证随 info bundle 打包导出。
        var setImporter = AssetImporter.GetAtPath(setDir);
        var infoBundle = setImporter != null ? setImporter.assetBundleName : null;
        if (!string.IsNullOrEmpty(infoBundle) && importer.assetBundleName != infoBundle)
            importer.SetAssetBundleNameAndVariant(infoBundle, "");
        importer.SaveAndReimport();

        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        if (clip == null)
            return "导入完成但无法加载为 AudioClip（格式可能损坏）。";
        AssetDatabase.SaveAssets();

        result = new CustomMusicUploadResultDto
        {
            fileName = fileName,
            assetPath = assetPath,
            guid = AssetDatabase.AssetPathToGUID(assetPath),
            sizeBytes = new FileInfo(abs).Length,
            lengthSec = clip.length
        };
        return null;
    }

    /// <summary>删除自定义 BGM（连同 .meta）。被关卡引用时拒绝并返回引用方列表。</summary>
    public static string DeleteCustomMusic(string setName, string fileName)
    {
        if (string.IsNullOrEmpty(setName) || SanitizeName(setName) != setName)
            return "关卡集标识非法：" + setName;
        var safeFile = Path.GetFileName(fileName ?? "");
        var ext = Path.GetExtension(safeFile).ToLowerInvariant();
        if (ext != ".ogg" && ext != ".wav")
            return "仅允许删除 data/bgm/ 下的 .ogg/.wav 文件。";
        var assetPath = CustomBgmDirAssetPath(setName) + "/" + safeFile;
        if (!AssetDatabase.IsValidFolder(CustomBgmDirAssetPath(setName)) || !File.Exists(AbsPath(assetPath)))
            return "文件不存在：" + safeFile;

        var users = FindCustomMusicUsers(setName, assetPath);
        if (users.Count > 0)
            return "该 BGM 正在被关卡引用（" + string.Join("、", users.ToArray()) + "），请先在关卡音频配置中改用其他 BGM。";

        if (!AssetDatabase.DeleteAsset(assetPath))
            return "删除失败（文件可能被占用）。";
        AssetDatabase.SaveAssets();
        return null;
    }

    /// <summary>扫描关卡集内引用了指定自定义 BGM 的 LevelInfoSO（返回关卡名列表）。</summary>
    private static List<string> FindCustomMusicUsers(string setName, string bgmAssetPath)
    {
        var users = new List<string>();
        var dataDir = LevelSetsRoot + "/" + setName + "/data";
        if (!AssetDatabase.IsValidFolder(dataDir))
            return users;
        foreach (var guid in AssetDatabase.FindAssets("t:LevelInfoSO", new[] { dataDir }))
        {
            var info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (info == null || info.inLevelMusic == null)
                continue;
            if (AssetDatabase.GetAssetPath(info.inLevelMusic) == bgmAssetPath)
            {
                var dirName = Path.GetFileName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(info)));
                users.Add(string.IsNullOrEmpty(info.levelNameZH) ? dirName : info.levelNameZH);
            }
        }
        return users;
    }

    /// <summary>加载关卡集 data/bgm/ 下指定文件名为 AudioClip；不存在返回 null。</summary>
    private static AudioClip LoadCustomMusicClip(string setName, string fileName)
    {
        if (string.IsNullOrEmpty(setName) || string.IsNullOrEmpty(fileName))
            return null;
        var assetPath = CustomBgmDirAssetPath(setName) + "/" + Path.GetFileName(fileName);
        return File.Exists(AbsPath(assetPath)) ? AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath) : null;
    }

    /// <summary>自定义 BGM 流式试听的磁盘绝对路径（HttpServer 用；不校验存在性）。</summary>
    public static string CustomMusicAbsPath(string setName, string fileName)
    {
        var safeFile = Path.GetFileName(fileName ?? "");
        return AbsPath(CustomBgmDirAssetPath(setName) + "/" + safeFile);
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
        // sceneName 不再强制 s_ 前缀（2026-09-22）：以 LevelInfoSO.sceneName 为锚点，
        // 兜底尝试新旧两种文件名约定。
        var scenePath = ResolveLevelScenePath(setName, levelId, levelDataDir);

        var list = new List<string>();
        if (!string.IsNullOrEmpty(scenePath))
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

        // sceneName 不再强制 s_ 前缀：以 LevelInfoSO.sceneName 为锚点定位场景（可空）。
        var scenePath = ResolveLevelScenePath(setName, levelId, levelDataDir);
        if (!string.IsNullOrEmpty(scenePath) && EditorSceneManager.GetActiveScene().path == scenePath)
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

        if (!string.IsNullOrEmpty(scenePath) && File.Exists(AbsPath(scenePath)))
            AssetDatabase.DeleteAsset(scenePath);
        AssetDatabase.DeleteAsset(levelDataDir);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ReloadPseudo();
        return null;
    }

    /// <summary>关卡场景定位（2026-09-22 起 sceneName 不再强制 s_ 前缀）：
    /// 优先读 data/&lt;levelId&gt;/ 下 LevelInfoSO.sceneName（权威字段），缺失/失联时
    /// 兜底尝试 s_&lt;levelId&gt;.unity（旧约定）与 &lt;levelId&gt;.unity（新约定）。
    /// 找不到返回 null（调用方按“无场景”处理）。</summary>
    private static string ResolveLevelScenePath(string setName, string levelId, string levelDataDir)
    {
        var scenesDir = LevelSetsRoot + "/" + setName + "/scenes";
        var candidates = new List<string>();
        if (AssetDatabase.IsValidFolder(levelDataDir))
        {
            foreach (var guid in AssetDatabase.FindAssets("t:LevelInfoSO", new[] { levelDataDir }))
            {
                var so = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so != null && !string.IsNullOrEmpty(so.sceneName))
                    candidates.Add(so.sceneName);
            }
        }
        candidates.Add("s_" + levelId);
        candidates.Add(levelId);
        foreach (var sceneName in candidates)
        {
            if (string.IsNullOrEmpty(sceneName))
                continue;
            var p = scenesDir + "/" + sceneName + ".unity";
            if (File.Exists(AbsPath(p)))
                return p;
        }
        return null;
    }

    // ==================== Levels (rename) ====================

    /// <summary>重命名关卡 id（原子改名，2026-09-22）：
    /// ① scenes/&lt;旧场景名&gt;.unity → scenes/&lt;newId&gt;.unity（旧 s_ 前缀关卡顺势迁移到无前缀新约定；
    ///    MoveAsset 保 GUID，场景内 stub.levelInfo / Animator 引用不断链）；
    /// ② 场景 AssetBundle 名显式覆盖为 &lt;set&gt;/&lt;newId&gt;（EnsureSceneBundleNames 只补空值，必须显式改）；
    /// ③ data/&lt;oldId&gt;/ → data/&lt;newId&gt;/，内部 LevelInfo_&lt;实际名&gt;.asset → LevelInfo_&lt;newId&gt;.asset
    ///    （config/截图/summary_bg~ / assignment~ / readme~ 全部随目录迁移）；
    /// ④ LevelInfoSO.sceneName = newId；
    /// ⑤ animations/ 下旧场景名前缀资产改 &lt;newId&gt;_ 前缀（GUID 保持，改名不留孤儿资产）；
    /// ⑥ writeback_history/&lt;set&gt;/&lt;oldId&gt;/ 目录搬移。
    /// LevelSetInfoSO.levelInfos 为 GUID 引用，文件移动不影响，无需改写。
    /// 全部校验通过后才动第一个文件；重命名后玩家侧 zip 需重新导出。</summary>
    public static string RenameLevel(LevelRenameDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.setName) || string.IsNullOrEmpty(dto.levelId))
            return "缺少关卡集或关卡标识。";
        var setName = dto.setName;
        var oldId = SanitizeName(dto.levelId);
        var newId = SanitizeName(dto.newLevelId);
        if (string.IsNullOrEmpty(oldId) || string.IsNullOrEmpty(newId))
            return "关卡标识只能包含字母数字和下划线。";
        if (oldId == newId)
            return null;
        if (newId.Equals("LevelSetInfo", StringComparison.OrdinalIgnoreCase))
            return "新关卡标识非法。";

        var setDir = LevelSetsRoot + "/" + setName;
        var oldDataDir = setDir + "/data/" + oldId;
        if (!AssetDatabase.IsValidFolder(oldDataDir))
            return "关卡不存在：" + oldId;
        if (AssetDatabase.IsValidFolder(setDir + "/data/" + newId))
            return "目标关卡已存在：" + newId;

        // ---- 定位 LevelInfoSO（文件名允许历史偏差，如 LevelInfo_Test_1.asset）----
        LevelInfoSO info = null;
        var infoPath = "";
        foreach (var guid in AssetDatabase.FindAssets("t:LevelInfoSO", new[] { oldDataDir }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(p);
            if (so != null)
            {
                info = so;
                infoPath = p;
                break;
            }
        }
        if (info == null)
            return "未找到该关卡的 LevelInfoSO。";

        // ---- 定位旧场景（sceneName 为锚点；失联时按新旧两种约定兜底）----
        var oldScenePath = ResolveLevelScenePath(setName, oldId, oldDataDir);
        var newScenePath = setDir + "/scenes/" + newId + ".unity";
        if (File.Exists(AbsPath(newScenePath)))
            return "目标场景已存在：" + newId + ".unity";

        // 目标场景正被打开时先切走（MoveAsset 不允许移动打开中的场景）
        if (!string.IsNullOrEmpty(oldScenePath) && EditorSceneManager.GetActiveScene().path == oldScenePath)
        {
            var fallback = FindFallbackScene(setName, oldScenePath);
            if (!string.IsNullOrEmpty(fallback))
                EditorSceneManager.OpenScene(fallback);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        var oldSceneName = !string.IsNullOrEmpty(oldScenePath)
            ? Path.GetFileNameWithoutExtension(oldScenePath)
            : "";

        // ---- 1. 场景改名 + 显式覆盖 bundle 名 ----
        if (!string.IsNullOrEmpty(oldScenePath))
        {
            var errScene = AssetDatabase.MoveAsset(oldScenePath, newScenePath);
            if (!string.IsNullOrEmpty(errScene))
                return "场景改名失败：" + errScene;
            SetAssetBundleName(newScenePath, setName + "/" + newId);
        }

        // ---- 2. data 目录改名 + LevelInfo 资产改名 ----
        var newDataDir = setDir + "/data/" + newId;
        var errDir = AssetDatabase.MoveAsset(oldDataDir, newDataDir);
        if (!string.IsNullOrEmpty(errDir))
            return "关卡数据目录改名失败：" + errDir;
        var infoFileName = infoPath.Substring(infoPath.LastIndexOf('/') + 1);
        var newInfoPath = newDataDir + "/LevelInfo_" + newId + ".asset";
        var currentInfoPath = newDataDir + "/" + infoFileName;
        if (!string.Equals(currentInfoPath, newInfoPath, StringComparison.Ordinal))
        {
            var errInfo = AssetDatabase.MoveAsset(currentInfoPath, newInfoPath);
            if (!string.IsNullOrEmpty(errInfo))
                return "LevelInfo 资产改名失败：" + errInfo;
        }

        // ---- 3. LevelInfoSO.sceneName 同步 ----
        Undo.RecordObject(info, "Rename Level");
        info.sceneName = newId;
        EditorUtility.SetDirty(info);

        // ---- 4. animations/ 前缀迁移（GUID 保持，场景 Animator 引用不断链）----
        if (!string.IsNullOrEmpty(oldSceneName))
            MigratePrefixedAssets(setDir + "/animations", oldSceneName + "_", newId + "_");

        // ---- 5. 写回历史目录搬移（非 Unity 资产，直接文件系统移动）----
        try
        {
            var histRoot = Path.Combine(Path.Combine(Application.dataPath, ".."), "writeback_history");
            var oldHist = Path.Combine(Path.Combine(histRoot, setName), oldId);
            if (Directory.Exists(oldHist))
            {
                var newHist = Path.Combine(Path.Combine(histRoot, setName), newId);
                if (!Directory.Exists(newHist))
                    Directory.Move(oldHist, newHist);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LevelAdmin] 写回历史目录搬移失败（不影响关卡本身）：" + e.Message);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ReloadPseudo();
        return null;
    }

    /// <summary>把 animDir 下 oldPrefix 开头的动画资产（.controller/.anim，目录平铺无子层级）
    /// 改名为 newPrefix 开头。AssetDatabase.MoveAsset 同步搬 .meta、GUID 不变，场景内
    /// Animator 引用不受影响。逐文件容错：单个失败记 warning 不中断整体改名。</summary>
    private static void MigratePrefixedAssets(string animDir, string oldPrefix, string newPrefix)
    {
        if (!AssetDatabase.IsValidFolder(animDir))
            return;
        string[] files;
        try { files = Directory.GetFiles(AbsPath(animDir)); }
        catch { return; }
        foreach (var f in files)
        {
            var name = Path.GetFileName(f);
            if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!name.StartsWith(oldPrefix, StringComparison.Ordinal))
                continue;
            var oldAsset = animDir + "/" + name;
            var newAsset = animDir + "/" + newPrefix + name.Substring(oldPrefix.Length);
            if (File.Exists(AbsPath(newAsset)))
                continue;
            var err = AssetDatabase.MoveAsset(oldAsset, newAsset);
            if (!string.IsNullOrEmpty(err))
                Debug.LogWarning("[LevelAdmin] 动画资产改名跳过 " + name + "：" + err);
        }
    }

    // ==================== Levels (reorder) ====================

    /// <summary>按前端给定的顺序重写 LevelSetInfoSO.levelInfos（即游戏内关卡顺序）。</summary>
    public static string ReorderLevels(LevelReorderDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.setName) || dto.levelIds == null || dto.levelIds.Length == 0)
            return "缺少关卡集或排序列表。";
        var setName = dto.setName;
        var setInfo = FindSetInfo(setName);
        if (setInfo == null)
            return "未找到关卡集配置 LevelSetInfoSO。";

        var dataDir = LevelSetsRoot + "/" + setName + "/data";
        var byId = new Dictionary<string, LevelInfoSO>();
        if (AssetDatabase.IsValidFolder(dataDir))
        {
            foreach (var guid in AssetDatabase.FindAssets("t:LevelInfoSO", new[] { dataDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(path);
                if (so == null)
                    continue;
                var p = path.Replace('\\', '/');
                var dir = p.Substring(0, p.LastIndexOf('/')); // .../data/<levelId>
                var id = dir.Substring(dir.LastIndexOf('/') + 1);
                byId[id] = so;
            }
        }

        var ordered = new List<LevelInfoSO>();
        var seen = new HashSet<string>();
        foreach (var rawId in dto.levelIds)
        {
            var id = rawId == null ? "" : rawId.Trim();
            if (id.Length == 0)
                return "关卡标识无效。";
            if (!seen.Add(id))
                return "关卡重复：" + id;
            LevelInfoSO so;
            if (!byId.TryGetValue(id, out so))
                return "关卡不存在：" + id;
            ordered.Add(so);
        }

        // 发送列表必须恰好覆盖 data/ 下的全部关卡，避免并发修改时静默丢失关卡。
        if (seen.Count != byId.Count)
            return "关卡列表与当前数据不一致，请刷新页面后重试。";

        // 指向 data/ 之外（或已丢失）的旧引用保留在末尾，避免丢数据。
        if (setInfo.levelInfos != null)
        {
            foreach (var li in setInfo.levelInfos)
            {
                if (li == null)
                    continue;
                var p = AssetDatabase.GetAssetPath(li);
                if (string.IsNullOrEmpty(p) || !p.Replace('\\', '/').StartsWith(dataDir + "/", StringComparison.Ordinal))
                    ordered.Add(li);
            }
        }

        Undo.RecordObject(setInfo, "Reorder Levels");
        setInfo.levelInfos = ordered.ToArray();
        EditorUtility.SetDirty(setInfo);
        AssetDatabase.SaveAssets();
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
        // onDeathEffectSO 已迁移到 LevelInfoSO。
        var info = LayoutEditorLevelInfoResolver.ResolveForScene(sceneAssetPath);
        if (info == null)
            return "未找到该场景对应的 LevelInfoSO，请先为关卡绑定 LevelInfo。";

        PseudoPrefabSO effect = null;
        if (theme == "water")
            effect = LoadPseudoByName("WaterSplash_Particle_003_SO");
        else if (theme == "goo")
            effect = LoadPseudoByName("WaterSplash_Particle_004_alien_SO");

        Undo.RecordObject(info, "Death theme");
        info.onDeathEffectSO = effect;
        EditorUtility.SetDirty(info);
        ForcePrepareForBuilding();
        AssetDatabase.SaveAssets();
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
        // 写回历史：SaveScene 后、Reload 前取语义快照（覆盖 layout 的 after，
        // 携带 layout+killplane 累计变化；无进行中记录时为 no-op）。
        LayoutEditorWriteBackHistory.SupplySemanticAfter(sceneAssetPath);
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

    /// <summary>将订单数量限制在 [1, 10]（与 LevelInfoSO 字段上的 Range 一致）；
    /// 旧关卡未序列化该字段时为 0，回退到传入的默认值。</summary>
    private static int ClampOrderCount(int value, int fallback)
    {
        if (value < 1 || value > 10)
            return fallback;
        return value;
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

    internal static string SanitizeName(string name)
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

    private static AudioConfigDto ReadAudioFromLevelInfo(LevelInfoSO info)
    {
        var dto = new AudioConfigDto
        {
            ambiences = new string[0],
            audioDirectoryGuids = new string[0],
            audioDirectoryIds = new string[0]
        };
        if (info == null)
            return dto;

        // 旧关卡的 LevelInfoSO 可能缺少音频字段（反序列化为 null），全部做空值防御。
        var musicGuid = GuidOf(info.inLevelMusicSO);
        var deathGuid = GuidOf(info.onDeathEffectSO);

        // 自定义 BGM 直引（inLevelMusic）：任何直引 clip 都按自定义处理显示。
        if (info.inLevelMusic != null)
        {
            var customPath = AssetDatabase.GetAssetPath(info.inLevelMusic);
            if (!string.IsNullOrEmpty(customPath))
            {
                dto.customMusicFile = Path.GetFileName(customPath);
                dto.customMusicName = Path.GetFileNameWithoutExtension(customPath);
                dto.customMusicSec = info.inLevelMusic.length;
            }
        }

        var dirGuids = new List<string>();
        if (info.audioDirectorySOs != null)
        {
            foreach (var d in info.audioDirectorySOs)
            {
                var g = GuidOf(d);
                if (!string.IsNullOrEmpty(g))
                    dirGuids.Add(g);
            }
        }

        var ambValueMap = BuildAmbienceValueMap();
        var ambNames = new List<string>();
        if (info.inLevelAmbiences != null)
        {
            foreach (var v in info.inLevelAmbiences)
            {
                string nm;
                if (ambValueMap.TryGetValue((int)v, out nm))
                    ambNames.Add(nm);
            }
        }

        dto.inLevelMusicGuid = musicGuid;
        dto.onDeathEffectGuid = deathGuid;
        dto.audioDirectoryGuids = dirGuids.ToArray();
        dto.audioDirectoryIds = IdsOf(dirGuids);
        dto.ambiences = ambNames.ToArray();
        dto.inLevelMusicId = IdOf(musicGuid);
        dto.onDeathEffectId = IdOf(deathGuid);
        return dto;
    }

    private static string GuidOf(UnityEngine.Object obj)
    {
        if (obj == null)
            return "";
        var path = AssetDatabase.GetAssetPath(obj);
        return string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
    }

    /// <summary>新建关卡时从 levelinfo 模板拷贝默认音频配置（BGM / 环境音 / AudioDirectory / 死亡特效）。
    /// 模板缺失或字段为空时退化为空数组，保证新资产音频字段不为 null。</summary>
    private static void ApplyTemplateAudioDefaults(LevelInfoSO info)
    {
        var tpl = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(TemplateLevelInfo);
        if (tpl != null)
        {
            info.inLevelMusicSO = tpl.inLevelMusicSO;
            info.inLevelAmbiences = tpl.inLevelAmbiences != null
                ? (LevelInfoSO.GameLoopingAudioTag[])tpl.inLevelAmbiences.Clone()
                : new LevelInfoSO.GameLoopingAudioTag[0];
            info.audioDirectorySOs = tpl.audioDirectorySOs != null
                ? (PseudoPrefabSO[])tpl.audioDirectorySOs.Clone()
                : new PseudoPrefabSO[0];
            info.onDeathEffectSO = tpl.onDeathEffectSO;
        }
        else
        {
            info.inLevelAmbiences = new LevelInfoSO.GameLoopingAudioTag[0];
            info.audioDirectorySOs = new PseudoPrefabSO[0];
        }
    }

    private static Dictionary<int, string> BuildAmbienceValueMap()
    {
        var map = new Dictionary<int, string>();
        var type = typeof(LevelInfoSO.GameLoopingAudioTag);
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

    // ==================== Summary export background ====================

    /// <summary>汇总页导出背景图目录：关卡 data 目录下的 <c>summary_bg~</c>。
    ///  `~` 结尾的目录被 Unity 完全忽略（不导入、无 .meta、不进任何 AssetBundle），
    ///  所以背景图不会被关卡集根目录 folder 级 assetBundleName（&lt;set&gt;/info_&lt;set&gt;）
    ///  打进玩家分发包；同时仍在 Assets 树内，随 git 跟关卡一起版本管理，
    ///  读取可直接复用 /api/level/data-file（只校验 Assets/ 前缀）。</summary>
    private const string SummaryBgDirName = "summary_bg~";
    private const string SummaryBgFileStem = "bg";
    private const string SummaryBgMetaFile = "meta.json";
    public const float SummaryBgDefaultDim = 0.35f;

    private static string SummaryBgDir(string levelInfoAssetPath)
    {
        var dir = DirectoryName(levelInfoAssetPath);
        if (string.IsNullOrEmpty(dir))
            return "";
        return dir + "/" + SummaryBgDirName;
    }

    /// <summary>读取关卡的汇总导出背景图（资源路径 + 遮罩浓度）。无背景图时 path = ""。</summary>
    public static void ReadSummaryBg(string levelInfoAssetPath, out string path, out float dim)
    {
        path = "";
        dim = SummaryBgDefaultDim;
        var dir = SummaryBgDir(levelInfoAssetPath);
        if (string.IsNullOrEmpty(dir))
            return;
        var absDir = AbsPath(dir);
        if (!Directory.Exists(absDir))
            return;
        foreach (var ext in SummaryBgExtensions)
        {
            var rel = dir + "/" + SummaryBgFileStem + ext;
            if (File.Exists(AbsPath(rel)))
            {
                path = rel;
                break;
            }
        }
        var metaAbs = Path.Combine(absDir, SummaryBgMetaFile);
        if (File.Exists(metaAbs))
        {
            try
            {
                var meta = JsonUtility.FromJson<SummaryBgMetaDto>(File.ReadAllText(metaAbs));
                if (meta != null && meta.dim >= 0f && meta.dim <= 0.9f)
                    dim = meta.dim;
            }
            catch
            {
                // 坏 meta 用默认值，不影响背景图本身
            }
        }
    }

    private static readonly string[] SummaryBgExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp" };

    private static void WriteSummaryBgMeta(string dir, float dim)
    {
        var absDir = AbsPath(dir);
        if (!Directory.Exists(absDir))
            Directory.CreateDirectory(absDir);
        var meta = new SummaryBgMetaDto();
        meta.dim = Mathf.Clamp(dim, 0f, 0.9f);
        File.WriteAllText(Path.Combine(absDir, SummaryBgMetaFile), JsonUtility.ToJson(meta), System.Text.Encoding.UTF8);
    }

    private static void DeleteSummaryBgImages(string dir)
    {
        var absDir = AbsPath(dir);
        if (!Directory.Exists(absDir))
            return;
        foreach (var ext in SummaryBgExtensions)
        {
            var f = Path.Combine(absDir, SummaryBgFileStem + ext);
            if (File.Exists(f))
                File.Delete(f);
        }
    }

    /** 上传汇总导出背景图。写进关卡 data 目录的 summary_bg~ 子目录（Unity 忽略），
     *  不调用 AssetDatabase.ImportAsset —— 这不是 Unity 资产，只给网页导出用。 */
    public static string UploadSummaryBg(string levelInfoAssetPath, string fileName, string base64, float dim, out string path, out float outDim)
    {
        path = "";
        outDim = Mathf.Clamp(dim, 0f, 0.9f);
        if (string.IsNullOrEmpty(levelInfoAssetPath))
            return "缺少关卡资源路径。";
        var so = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(levelInfoAssetPath);
        if (so == null)
            return "未找到 LevelInfoSO：" + levelInfoAssetPath;

        var ext = string.IsNullOrEmpty(fileName) ? ".png" : Path.GetExtension(fileName).ToLowerInvariant();
        var extOk = false;
        foreach (var e in SummaryBgExtensions)
        {
            if (e == ext)
                extOk = true;
        }
        if (!extOk)
            ext = ".png";

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
            return "背景图 base64 解码失败。";
        }
        if (bytes == null || bytes.Length == 0)
            return "背景图数据为空。";

        var dir = SummaryBgDir(levelInfoAssetPath);
        if (string.IsNullOrEmpty(dir))
            return "关卡数据目录不存在。";
        var absDir = AbsPath(dir);
        if (!Directory.Exists(absDir))
            Directory.CreateDirectory(absDir);
        DeleteSummaryBgImages(dir);
        File.WriteAllBytes(Path.Combine(absDir, SummaryBgFileStem + ext), bytes);
        WriteSummaryBgMeta(dir, outDim);

        path = dir + "/" + SummaryBgFileStem + ext;
        return null;
    }

    /** 只更新遮罩浓度（不换图）。 */
    public static string SetSummaryBgDim(string levelInfoAssetPath, float dim, out string path, out float outDim)
    {
        path = "";
        outDim = Mathf.Clamp(dim, 0f, 0.9f);
        if (string.IsNullOrEmpty(levelInfoAssetPath))
            return "缺少关卡资源路径。";
        var dir = SummaryBgDir(levelInfoAssetPath);
        if (string.IsNullOrEmpty(dir))
            return "关卡数据目录不存在。";
        WriteSummaryBgMeta(dir, outDim);
        float ignored;
        ReadSummaryBg(levelInfoAssetPath, out path, out ignored);
        return null;
    }

    /** 清除汇总导出背景图（连同 meta 一起删掉整个 summary_bg~ 目录）。 */
    public static string ClearSummaryBg(string levelInfoAssetPath)
    {
        if (string.IsNullOrEmpty(levelInfoAssetPath))
            return "缺少关卡资源路径。";
        var dir = SummaryBgDir(levelInfoAssetPath);
        if (string.IsNullOrEmpty(dir))
            return "关卡数据目录不存在。";
        var absDir = AbsPath(dir);
        try
        {
            if (Directory.Exists(absDir))
                Directory.Delete(absDir, true);
        }
        catch (Exception e)
        {
            return "删除背景图失败：" + e.Message;
        }
        return null;
    }

    // ==================== Level assignment & readme（关卡 data 目录数据文件） ====================

    /// <summary>菜谱分工配置目录 <c>assignment~</c> 与汇总页 readme 目录 <c>readme~</c>。
    ///  与 summary_bg~ 同范式：关卡 data 目录（LevelInfoSO 所在目录）下的 `~` 结尾目录
    ///  被 Unity 完全忽略（不导入、无 .meta、不进 AssetBundle），不会被打进玩家分发包，
    ///  但仍在 Assets 树内随 git 版本管理。</summary>
    private const string AssignmentDirName = "assignment~";
    private const string AssignmentFileName = "assignment.json";
    private const string ReadmeDirName = "readme~";
    private const string ReadmeFileName = "readme.json";
    private const int AssignmentMaxBytes = 512 * 1024;
    private const int ReadmeMaxBytes = 256 * 1024;

    private static string LevelDataSubDir(string levelInfoAssetPath, string dirName)
    {
        var dir = DirectoryName(levelInfoAssetPath);
        if (string.IsNullOrEmpty(dir))
            return "";
        return dir + "/" + dirName;
    }

    /// <summary>关卡 data 目录写守卫：只允许 LevelSets 内的 LevelInfoSO 路径，防越界写/路径穿越。</summary>
    private static string ValidateLevelInfoPath(string levelInfoAssetPath)
    {
        if (string.IsNullOrEmpty(levelInfoAssetPath))
            return "缺少关卡资源路径。";
        levelInfoAssetPath = levelInfoAssetPath.Replace('\\', '/').Trim();
        if (levelInfoAssetPath.Contains(".."))
            return "非法路径。";
        if (!levelInfoAssetPath.StartsWith("Assets/LevelSets/", StringComparison.OrdinalIgnoreCase))
            return "只支持关卡集内的关卡（Assets/LevelSets/…）。";
        return null;
    }

    private static string ReadLevelDataText(string levelInfoAssetPath, string dirName, string fileName, out bool exists)
    {
        exists = false;
        var dir = LevelDataSubDir(levelInfoAssetPath, dirName);
        if (string.IsNullOrEmpty(dir))
            return "";
        var abs = Path.Combine(AbsPath(dir), fileName);
        if (!File.Exists(abs))
            return "";
        try
        {
            exists = true;
            return File.ReadAllText(abs, System.Text.Encoding.UTF8);
        }
        catch (Exception e)
        {
            exists = false;
            return "";
        }
    }

    private static string WriteLevelDataText(string levelInfoAssetPath, string dirName, string fileName, string text, int maxBytes)
    {
        var err = ValidateLevelInfoPath(levelInfoAssetPath);
        if (err != null)
            return err;
        if (text == null)
            text = "";
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        if (bytes.Length > maxBytes)
            return "内容过大（上限 " + (maxBytes / 1024) + "KB）。";
        var dir = LevelDataSubDir(levelInfoAssetPath, dirName);
        if (string.IsNullOrEmpty(dir))
            return "关卡数据目录不存在。";
        var absDir = AbsPath(dir);
        if (!Directory.Exists(absDir))
            Directory.CreateDirectory(absDir);
        // `~` 目录不是 Unity 资产，无需 AssetDatabase.Refresh（与 UploadSummaryBg 一致）。
        File.WriteAllText(Path.Combine(absDir, fileName), text, System.Text.Encoding.UTF8);
        return null;
    }

    private static string ClearLevelDataDir(string levelInfoAssetPath, string dirName)
    {
        var err = ValidateLevelInfoPath(levelInfoAssetPath);
        if (err != null)
            return err;
        var dir = LevelDataSubDir(levelInfoAssetPath, dirName);
        if (string.IsNullOrEmpty(dir))
            return "关卡数据目录不存在。";
        var absDir = AbsPath(dir);
        try
        {
            if (Directory.Exists(absDir))
                Directory.Delete(absDir, true);
        }
        catch (Exception e)
        {
            return "删除失败：" + e.Message;
        }
        return null;
    }

    /** 读取菜谱分工配置（assignment~/assignment.json 原文）。 */
    public static string ReadLevelAssignment(string levelInfoAssetPath, out AssignmentResultDto result)
    {
        result = new AssignmentResultDto();
        var err = ValidateLevelInfoPath(levelInfoAssetPath);
        if (err != null)
            return err;
        result.json = ReadLevelDataText(levelInfoAssetPath, AssignmentDirName, AssignmentFileName, out result.exists);
        return null;
    }

    /** 保存菜谱分工配置。JSON 内部结构由前端负责，这里只做轻校验（对象字面量 + 大小上限）。 */
    public static string SaveLevelAssignment(string levelInfoAssetPath, string json)
    {
        var trimmed = json == null ? "" : json.Trim();
        if (trimmed.Length > 0 && trimmed[0] != '{')
            return "分工配置必须是 JSON 对象。";
        return WriteLevelDataText(levelInfoAssetPath, AssignmentDirName, AssignmentFileName, trimmed, AssignmentMaxBytes);
    }

    /** 清除菜谱分工配置（删除整个 assignment~ 目录）。 */
    public static string ClearLevelAssignment(string levelInfoAssetPath)
    {
        return ClearLevelDataDir(levelInfoAssetPath, AssignmentDirName);
    }

    /** 读取汇总页 readme（readme~/readme.json → html 字段）。 */
    public static string ReadLevelReadme(string levelInfoAssetPath, out ReadmeResultDto result)
    {
        result = new ReadmeResultDto();
        var err = ValidateLevelInfoPath(levelInfoAssetPath);
        if (err != null)
            return err;
        var raw = ReadLevelDataText(levelInfoAssetPath, ReadmeDirName, ReadmeFileName, out result.exists);
        if (!result.exists)
            return null;
        try
        {
            var file = JsonUtility.FromJson<ReadmeFileDto>(raw);
            result.html = file == null || file.html == null ? "" : file.html;
        }
        catch
        {
            // 坏文件按空内容处理（不阻塞页面渲染），exists 仍为 true 便于重新保存覆盖
            result.html = "";
        }
        return null;
    }

    /** 保存汇总页 readme（HTML 白名单净化在前端完成，这里限长度 + 包一层 schemaVersion）。 */
    public static string SaveLevelReadme(string levelInfoAssetPath, string html)
    {
        var file = new ReadmeFileDto();
        file.schemaVersion = 1;
        file.html = html == null ? "" : html;
        return WriteLevelDataText(levelInfoAssetPath, ReadmeDirName, ReadmeFileName, JsonUtility.ToJson(file), ReadmeMaxBytes);
    }

    /** 清除汇总页 readme（删除整个 readme~ 目录）。 */
    public static string ClearLevelReadme(string levelInfoAssetPath)
    {
        return ClearLevelDataDir(levelInfoAssetPath, ReadmeDirName);
    }

    // ==================== Custom Recipe Management ====================

    private const string CustomRecipesDir = "custom_recipes";
    private const string CommonCustomRecipesDir = "Assets/common01/food/CustomRecipes";
    /// <summary>Burger大全共享库（Assets/commonW2，打包为 commonw2 bundle）：
    ///  独立于关卡集的汉堡自定义菜谱，在所有关卡集的菜谱管理页以「burger」分类并入，
    ///  关卡选用后由 EnsureWebDependencies 注入 commonW2 bundle 依赖。</summary>
    public const string CommonW2RecipesDir = "Assets/commonW2/custom_recipes";
    /// <summary>沙拉大全共享库（Assets/commonW3，打包为 commonW3 bundle）：
    ///  DLC11 食材全排列的自定义沙拉（Web 前缀命名，与官方组成零重复），
    ///  在所有关卡集的菜谱管理页以「commonw3」分类并入，关卡选用后注入 commonW3 bundle 依赖。</summary>
    public const string CommonW3RecipesDir = "Assets/commonW3/custom_recipes";
    /// <summary>Burger大全分类 id（commonW2 内菜谱的固定 category）。</summary>
    public const string BurgerCategoryId = "burger";
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

        // Burger大全（commonW2 共享库）作为固定分类并入，前端 chips/新建表单直接使用。
        if (AssetFolderExists(CommonW2RecipesDir) && !catDtos.Exists(c => c.id == BurgerCategoryId))
            catDtos.Add(new CustomRecipeCategoryDto { id = BurgerCategoryId, zh = "🍔 Burger大全", en = "Burger" });

        var subDtos = new List<CustomRecipeSubcategoryDto>();
        if (config.subcategories != null)
        {
            foreach (var s in config.subcategories)
            {
                if (s == null || string.IsNullOrEmpty(s.id))
                    continue;
                subDtos.Add(new CustomRecipeSubcategoryDto
                {
                    id = s.id,
                    parent = s.parent ?? "",
                    zh = string.IsNullOrEmpty(s.zh) ? s.id : s.zh,
                    en = string.IsNullOrEmpty(s.en) ? s.id : s.en,
                    order = s.order
                });
            }
        }
        // Burger大全的固定二级分类（目录即事实，此处只给显示名与排序）。
        if (AssetFolderExists(CommonW2RecipesDir))
        {
            foreach (var s in BurgerSubcategories())
            {
                if (!subDtos.Exists(x => x.parent == s.parent && x.id == s.id))
                    subDtos.Add(s);
            }
        }

        return new CustomRecipeConfigDto
        {
            uidPrefix = config.uidPrefix,
            nextSequence = config.nextSequence,
            categories = catDtos.ToArray(),
            subcategories = subDtos.ToArray()
        };
    }

    /// <summary>Burger大全（commonW2）二级分类显示表。
    ///  归属以目录为准（burger/&lt;子目录&gt;/），此表只决定中文名与排列顺序。
    ///  与 layout-editor/scripts/split-burger-subcategories.mjs 的 SUBCATEGORIES
    ///  和前端 recipeTypes.ts 的 BURGER_SUBTYPE_ZH 三处保持一致。</summary>
    internal static List<CustomRecipeSubcategoryDto> BurgerSubcategories()
    {
        var list = new List<CustomRecipeSubcategoryDto>();
        AddBurgerSub(list, "classic", "经典汉堡", "Classic", 10);
        AddBurgerSub(list, "deluxe", "豪华汉堡", "Deluxe", 20);
        AddBurgerSub(list, "mega", "巨无霸汉堡", "Mega", 30);
        AddBurgerSub(list, "breakfast", "早餐汉堡", "Breakfast", 40);
        AddBurgerSub(list, "seafood", "海鲜汉堡", "Seafood", 50);
        AddBurgerSub(list, "veggie", "素食汉堡", "Veggie", 60);
        AddBurgerSub(list, "filling", "夹心/中间产物", "Filling", 70);
        AddBurgerSub(list, "assembly", "组装定义", "Assembly", 80);
        return list;
    }

    private static void AddBurgerSub(List<CustomRecipeSubcategoryDto> list, string id, string zh, string en, int order)
    {
        list.Add(new CustomRecipeSubcategoryDto
        {
            id = id,
            parent = BurgerCategoryId,
            zh = zh,
            en = en,
            order = order
        });
    }

    private static bool IsUidPrefixConflicting(int prefix)
    {
        var assets = new List<AssetRef>();
        assets.AddRange(ScanCustomRecipeAssets(LevelSetsRoot));
        if (AssetFolderExists(CommonCustomRecipesDir))
            assets.AddRange(ScanCustomRecipeAssets(CommonCustomRecipesDir));
        if (AssetFolderExists(CommonW2RecipesDir))
            assets.AddRange(ScanCustomRecipeAssets(CommonW2RecipesDir));
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

        // 扫描根：本关卡集 custom_recipes + commonW2 Burger大全共享库（后者对所有关卡集可见）。
        var scanRoots = new List<string>();
        var setRecipesDir = LevelSetsRoot + "/" + setName + "/" + CustomRecipesDir;
        if (AssetFolderExists(setRecipesDir))
            scanRoots.Add(setRecipesDir);
        if (AssetFolderExists(CommonW2RecipesDir))
            scanRoots.Add(CommonW2RecipesDir);
        if (scanRoots.Count == 0)
            return list.ToArray();

        var namesDict = LoadCustomRecipeNames(setName);
        foreach (var kv in LoadCustomRecipeNamesFromDir(CommonW2RecipesDir))
            namesDict[kv.Key] = kv.Value;

        // 全部候选条目（本关卡集 + common01 官方 CustomRecipes + commonW2 Burger大全），
        // 用于把组成里的子菜谱 id 解析为烹饪步骤与叶食材。
        var allEntries = BuildCustomRecipeEntryDtos(setName);
        var entryById = new Dictionary<string, RecipeEntryDto>(StringComparer.Ordinal);
        foreach (var e in allEntries)
        {
            if (!string.IsNullOrEmpty(e.id) && !entryById.ContainsKey(e.id))
                entryById[e.id] = e;
        }

        foreach (var recipesDir in scanRoots)
        foreach (var asset in ScanCustomRecipeAssets(recipesDir))
        {
            var path = asset.assetPath;
            var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(path);
            if (so == null)
                continue;

            var guid = asset.guid;
            var id = Path.GetFileNameWithoutExtension(path);
            var category = "";
            var subcategory = "";
            var rel = path.Substring(recipesDir.Length + 1).Replace('\\', '/');
            var slash = rel.IndexOf('/');
            if (slash >= 0)
            {
                category = rel.Substring(0, slash);
                // 二级分类 = 再下一层目录名（Burger大全细分类：burger/breakfast/xxx.asset）。
                // 共享资源目录（models/icons）里没有菜谱资产，不会误命中。
                var rest = rel.Substring(slash + 1);
                var slash2 = rest.IndexOf('/');
                if (slash2 >= 0)
                    subcategory = rest.Substring(0, slash2);
            }

            var compIds = new List<string>();
            if (so.compositionSOs != null)
            {
                foreach (var c in so.compositionSOs)
                {
                    if (c == null) continue;
                    var cp = AssetDatabase.GetAssetPath(c);
                    if (string.IsNullOrEmpty(cp))
                        continue;
                    var fileId = Path.GetFileNameWithoutExtension(cp);
                    // 食材组成规范化为目录 id（DLC 食材目录 id 与文件名不同，如 cherry
                    // vs DLC07_Cherry）：前端编辑表单/菜谱卡片按目录 id 匹配名称与图标。
                    // 官方菜谱（PseudoPrefabSORecipe）与自定义子菜谱保持资产文件名——
                    // 菜谱目录（ScanRecipes）的官方菜谱 id 就是文件名，规范化会错开。
                    var recipeSo = c as PseudoPrefabSORecipe;
                    if (recipeSo != null)
                        compIds.Add(fileId);
                    else
                    {
                        var ingSo = c as PseudoPrefabSO;
                        compIds.Add(ingSo != null
                            ? LayoutEditorCatalogApi.IngredientCatalogId(ingSo, fileId)
                            : fileId);
                    }
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

            string stepIconId = "";
            if (so.cookingStepIconSO != null)
            {
                var sip = AssetDatabase.GetAssetPath(so.cookingStepIconSO);
                if (!string.IsNullOrEmpty(sip))
                    stepIconId = Path.GetFileNameWithoutExtension(sip);
            }

            string mixIconId = "";
            if (so.mixingIconSO != null)
            {
                var mip = AssetDatabase.GetAssetPath(so.mixingIconSO);
                if (!string.IsNullOrEmpty(mip))
                    mixIconId = Path.GetFileNameWithoutExtension(mip);
            }

            bool hasIcon = so.icon != null;
            bool hasModel = so.model != null;
            // 图标状态：共享占位图（文件名含 Generic）不代表这道菜，前端需降级到食材图标
            var iconState = "none";
            if (so.icon != null)
            {
                var iconAssetPath = AssetDatabase.GetAssetPath(so.icon);
                iconState = !string.IsNullOrEmpty(iconAssetPath)
                    && Path.GetFileNameWithoutExtension(iconAssetPath)
                        .IndexOf("Generic", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "generic"
                    : "own";
            }
            // 可预览 = 上传约定目录有网格，或 model 引用的 prefab 能解析出网格源文件
            var modelSource = ResolveRecipeModelSource(path);
            bool previewable = modelSource != null && !string.IsNullOrEmpty(modelSource.modelFile);

            string nameZh = id;
            string nameEn = id;
            NameRow nr;
            // names.json 以 recipeName 为 id；兼容按文件名命中的历史数据。
            if ((so.recipeName != null && namesDict.TryGetValue(so.recipeName, out nr)) || namesDict.TryGetValue(id, out nr))
            {
                nameZh = nr.Zh;
                nameEn = nr.En;
            }

            RecipeEntryDto entry;
            entryById.TryGetValue(id, out entry);

            Vector3 bsz;
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
                subcategory = subcategory,
                type = so.type.ToString(),
                optionalKind = so is CustomRecipeOptionalBurgerSO ? "burger"
                    : so is CustomRecipeOptionalPizzaSO ? "pizza" : "",
                isFinishedBurger = !(so is CustomRecipeOptionalBurgerSO)
                    && !(so is CustomRecipeOptionalPizzaSO)
                    && LayoutEditorBurgerApi.IsFinishedBurgerRecipe(so),
                compositionIds = compIds.ToArray(),
                ingredients = entry != null ? entry.ingredients : new string[0],
                cookingGroups = entry != null ? LayoutEditorRecipeKnowledge.ComputeCookingGroups(entry, allEntries) : new RecipeCookingGroupDto[0],
                intermediate = so.score <= 0,
                group = LayoutEditorCatalogApi.FoodGroupOf(path),
                cookingStepId = stepId,
                cookingStepIconId = stepIconId,
                platingStepId = plateId,
                mixingIconId = mixIconId,
                hasIcon = hasIcon,
                iconState = iconState,
                hasModel = hasModel,
                hasModelSO = so.modelSO != null,
                previewable = previewable,
                modelScale = RecipeModelScale(path),
                modelRotationY = RecipeModelRotationY(path),
                modelRotationX = RecipeModelTransform(path).rotationX,
                modelRotationZ = RecipeModelTransform(path).rotationZ,
                modelPositionX = RecipeModelTransform(path).positionX,
                modelPositionY = RecipeModelTransform(path).positionY,
                modelPositionZ = RecipeModelTransform(path).positionZ,
                modelPivotX = RecipeModelTransform(path).pivotX,
                modelPivotY = RecipeModelTransform(path).pivotY,
                modelPivotZ = RecipeModelTransform(path).pivotZ,
                boundsMinY = ModelBoundsOf(so.model, out bsz).min.y,
                boundsSizeX = bsz.x,
                boundsSizeY = bsz.y,
                boundsSizeZ = bsz.z
            });
        }

        list.Sort((a, b) => string.Compare(a.nameZh, b.nameZh, StringComparison.Ordinal));
        return list.ToArray();
    }

    /// <summary>计算模型（prefab/FBX 导入资产）在世界坐标的包围盒（含配置变换），
    ///  供前端按 Unity 实际导入尺寸校准自动适配（three.js 预览尺寸可能与 Unity 不一致，
    ///  如部分 FBX 的 Lcl Scaling 单位换算差异）。</summary>
    private static Bounds ModelBoundsOf(GameObject model, out Vector3 size)
    {
        size = Vector3.zero;
        if (model == null)
            return new Bounds();
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds();
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        size = b.size;
        return b;
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
        // commonW2 Burger大全：子菜谱（煎肉饼/炸虾等中间产物）参与工序分组解析。
        if (AssetFolderExists(CommonW2RecipesDir))
            folders.Add(CommonW2RecipesDir);

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
        return LoadCustomRecipeNamesFromDir(LevelSetsRoot + "/" + setName + "/" + CustomRecipesDir);
    }

    /// <summary>指定 custom_recipes 库的 names.json（recipeName → 中文名），供其它模块展示用。</summary>
    internal static Dictionary<string, string> LoadCustomRecipeZhMap(string recipesDir)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in LoadCustomRecipeNamesFromDir(recipesDir))
            map[kv.Key] = kv.Value.Zh;
        return map;
    }

    /// <summary>names.json 中英名（recipeName / 文件名 id）。</summary>
    internal static bool TryGetCustomRecipeDisplayName(string recipesDir, string key, out string zh, out string en)
    {
        zh = key ?? "";
        en = key ?? "";
        if (string.IsNullOrEmpty(recipesDir) || string.IsNullOrEmpty(key))
            return false;
        NameRow row;
        var dict = LoadCustomRecipeNamesFromDir(recipesDir);
        if (dict.TryGetValue(key, out row))
        {
            zh = string.IsNullOrEmpty(row.Zh) ? key : row.Zh;
            en = string.IsNullOrEmpty(row.En) ? key : row.En;
            return true;
        }
        return false;
    }

    /// <summary>按 custom_recipes 目录读取 names.json（id → 中英名），目录不存在时返回空表。</summary>
    private static Dictionary<string, NameRow> LoadCustomRecipeNamesFromDir(string recipesDir)
    {
        var dict = new Dictionary<string, NameRow>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(recipesDir))
            return dict;
        var namesPath = recipesDir + "/names.json";
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
            "Assets/common02/food/PlatingSteps",
            // 马克杯（Mug）在通用内容源库 common03
            "Assets/common03/food/PlatingSteps"
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

        // commonW2 Burger大全：中间产物模型（煎鸡肉饼/炸虾饼等 prefab）可被复用。
        if (AssetFolderExists(CommonW2RecipesDir))
            modelFolders.Add(CommonW2RecipesDir);

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
        if (so.icon != null)
        {
            var iconPath = AssetDatabase.GetAssetPath(so.icon);
            if (!string.IsNullOrEmpty(iconPath))
            {
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
        }

        // 回退：icon 引用丢失时仍按 models/<id>/<id>_Icon.png 约定找文件。
        var recipeId = SanitizeName(Path.GetFileNameWithoutExtension(recipeAssetPath));
        var modelsDir = RecipeModelsDir(recipeAssetPath);
        var pngPath = modelsDir + "/" + recipeId + "_Icon.png";
        var jpgPath = modelsDir + "/" + recipeId + "_Icon.jpg";
        if (File.Exists(AbsPath(pngPath)))
        {
            absPath = AbsPath(pngPath);
            contentType = "image/png";
            return null;
        }
        if (File.Exists(AbsPath(jpgPath)))
        {
            absPath = AbsPath(jpgPath);
            contentType = "image/jpeg";
            return null;
        }
        return "该菜谱没有图标。";
    }

    /// <summary>菜谱专属 models 子目录：<分类>/models/<recipeId>/（每个菜谱一个文件夹，模型文件互不干扰）。</summary>
    private static string RecipeModelsDir(string recipeAssetPath)
    {
        var id = Path.GetFileNameWithoutExtension(recipeAssetPath);
        return DirectoryName(recipeAssetPath) + "/models/" + (string.IsNullOrEmpty(id) ? "model" : SanitizeName(id));
    }

    /// <summary>确保菜谱专属 models 子目录存在并返回其路径。</summary>
    private static string EnsureRecipeModelsDir(string recipeAssetPath)
    {
        var dir = RecipeModelsDir(recipeAssetPath);
        var parent = Path.GetDirectoryName(dir.Replace('\\', '/'));
        if (string.IsNullOrEmpty(parent))
            return dir;
        if (!AssetFolderExists(parent))
        {
            var grand = Path.GetDirectoryName(parent.Replace('\\', '/'));
            if (string.IsNullOrEmpty(grand) || !AssetFolderExists(grand))
                return dir;
            AssetDatabase.CreateFolder(grand, Path.GetFileName(parent));
        }
        if (!AssetFolderExists(dir))
            AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
        var absDir = AbsPath(dir);
        if (!Directory.Exists(absDir))
            Directory.CreateDirectory(absDir);
        AssetDatabase.Refresh();
        return dir;
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

    /// <summary>按配置条目构建完整变换（scale + 三轴旋转 + 三轴位置；无条目时取默认）。</summary>
    private static CustomRecipeConfigSO.CustomRecipeTransformEntry RecipeModelTransform(string recipeAssetPath)
    {
        var config = LoadCustomRecipeConfigFor(recipeAssetPath);
        ImportLegacyModelTransform(config, recipeAssetPath);
        var e = FindModelTransform(config, recipeAssetPath);
        if (e == null)
            return new CustomRecipeConfigSO.CustomRecipeTransformEntry { assetPath = recipeAssetPath, scale = 1f };
        return e;
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
        var e = RecipeModelTransform(recipeAssetPath);
        return e.scale > 0f ? e.scale : 1f;
    }

    private static float RecipeModelRotationY(string recipeAssetPath)
    {
        return RecipeModelTransform(recipeAssetPath).rotationY;
    }

    private static void SetRecipeModelTransform(string recipeAssetPath, float scale, float rotationY,
        float rotationX, float rotationZ, float positionX, float positionY, float positionZ,
        float pivotX, float pivotY, float pivotZ)
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
                list[i].rotationX = rotationX;
                list[i].rotationZ = rotationZ;
                list[i].positionX = positionX;
                list[i].positionY = positionY;
                list[i].positionZ = positionZ;
                list[i].pivotX = pivotX;
                list[i].pivotY = pivotY;
                list[i].pivotZ = pivotZ;
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
            rotationX = rotationX,
            rotationZ = rotationZ,
            positionX = positionX,
            positionY = positionY,
            positionZ = positionZ,
            pivotX = pivotX,
            pivotY = pivotY,
            pivotZ = pivotZ,
        });
        config.modelTransforms = list.ToArray();
        EditorUtility.SetDirty(config);
    }

    /// <summary>把菜谱的模型变换（缩放/旋转/位置/原点）应用到装盘模型。
    ///  游戏运行时（ClientAttachedOrderCosmeticDecisions）在实例化后会重置模型根节点的
    ///  localPosition/localRotation，因此变换必须放在「子节点」上才能生效：
    ///  这里把模型重建为「空根 + 变换节点 + 模型」结构——变换（缩放/旋转/位置）应用到
    ///  变换节点，模型原点偏移（pivot）应用到最内层模型节点，旋转/缩放绕偏移后的原点。
    ///  复用/共享模型会先克隆到本菜谱 models 目录，避免影响其他菜谱。
    ///  数值存储在插件自己的 CustomRecipeConfigSO 中，不修改宿主 CustomRecipeSO。</summary>
    private static void ApplyModelTransform(CustomRecipeSO so)
    {
        if (so == null || so.model == null)
            return;
        var recipePath = AssetDatabase.GetAssetPath(so);
        if (string.IsNullOrEmpty(recipePath))
            return;
        var t = RecipeModelTransform(recipePath);
        var modelPath = AssetDatabase.GetAssetPath(so.model);
        if (string.IsNullOrEmpty(modelPath))
            return;

        var modelsDir = RecipeModelsDir(recipePath);
        if (!AssetFolderExists(modelsDir))
            return;
        var id = Path.GetFileNameWithoutExtension(recipePath);
        var ownPrefabPath = modelsDir + "/" + id + ".prefab";
        var isPrefab = modelPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        var isOwnPrefab = isPrefab && modelPath.StartsWith(modelsDir + "/", StringComparison.OrdinalIgnoreCase);

        // 三级结构判定：根无渲染器、其第一子节点也无渲染器且还有子节点（变换节点 → 模型节点）
        if (isOwnPrefab && so.model.GetComponent<Renderer>() == null && so.model.transform.childCount > 0)
        {
            var tx = so.model.transform.GetChild(0);
            if (tx.GetComponent<Renderer>() == null && tx.childCount > 0)
            {
                // 已是「空根 + 变换节点 + 模型」结构：变换应用到变换节点，原点偏移应用到模型节点
                ApplyTransformToChild(tx, t);
                ApplyPivotToModel(tx.GetChild(0), t);
                EditorUtility.SetDirty(so.model);
                AssetDatabase.SaveAssets();
                return;
            }
        }

        // 旧结构（单节点/双节点 prefab）、复用其他菜谱的模型、或 FBX/OBJ 直引：
        // 统一重建为「空根 + 变换节点 + 模型」prefab（复用模型克隆到本菜谱目录，避免互相覆盖）
        var targetPath = isOwnPrefab ? modelPath : ownPrefabPath;
        var prefab = RebuildPlatingPrefab(targetPath, so.model, t);
        if (prefab == null)
            return;

        Undo.RecordObject(so, "Apply Recipe Model Transform");
        so.model = prefab;
        so.modelSO = null;
        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
    }

    /// <summary>把变换（缩放/旋转/位置）应用到变换节点（游戏只重置根节点，子节点变换在运行时保留）。</summary>
    private static void ApplyTransformToChild(Transform child, CustomRecipeConfigSO.CustomRecipeTransformEntry t)
    {
        child.localScale = Vector3.one * Mathf.Max(0.0001f, t.scale > 0f ? t.scale : 1f);
        child.localEulerAngles = new Vector3(t.rotationX, t.rotationY, t.rotationZ);
        child.localPosition = new Vector3(t.positionX, t.positionY, t.positionZ);
    }

    /// <summary>把模型原点偏移应用到最内层模型节点：模型平移使旋转/缩放绕偏移后的原点。</summary>
    private static void ApplyPivotToModel(Transform model, CustomRecipeConfigSO.CustomRecipeTransformEntry t)
    {
        model.localPosition = new Vector3(t.pivotX, t.pivotY, t.pivotZ);
    }

    /// <summary>重建装盘 prefab：空根 + 变换节点（缩放/旋转/位置）+ 模型节点（原点偏移）。
    ///  覆盖同名 prefab（保留 guid，避免引用失效）。</summary>
    private static GameObject RebuildPlatingPrefab(string prefabPath, GameObject source,
        CustomRecipeConfigSO.CustomRecipeTransformEntry t)
    {
        if (source == null || string.IsNullOrEmpty(prefabPath))
            return null;
        var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
        try
        {
            var tx = new GameObject(source.name + "_Transform");
            tx.transform.SetParent(root.transform, false);
            ApplyTransformToChild(tx.transform, t);
            var model = UnityEngine.Object.Instantiate(source);
            model.name = source.name;
            model.transform.SetParent(tx.transform, false);
            ApplyPivotToModel(model.transform, t);
            var prefabInstance = PrefabUtility.CreatePrefab(prefabPath, root);
            return prefabInstance;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LayoutEditor] 重建装盘 prefab 失败: " + prefabPath + " - " + ex.Message);
            return null;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>菜谱模型/装盘链路诊断（只读）：检查模型引用、prefab 渲染完整性、
    ///  组成/步骤资产可解析性与模型变换值，用于排查"模型在游戏中不显示"。</summary>
    public static CustomRecipeDiagnoseDto DiagnoseCustomRecipe(string assetPath)
    {
        var dto = new CustomRecipeDiagnoseDto { assetPath = assetPath };
        if (string.IsNullOrEmpty(assetPath))
        {
            dto.error = "缺少资源路径。";
            return dto;
        }
        var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(assetPath);
        if (so == null)
        {
            dto.error = "未找到菜谱资源（CustomRecipeSO 无法加载）。";
            return dto;
        }

        // 模型引用：运行时 GetModel() 优先 model 直引（modelSO 为 null 时即 so.model）
        dto.modelDirect = so.model != null;
        dto.modelSOBased = so.modelSO != null;
        dto.platingPrefabSet = so.model != null || (so.modelSO != null && so.modelSO.assetPath != null);
        if (so.model != null)
        {
            var modelPath = AssetDatabase.GetAssetPath(so.model);
            dto.modelPath = modelPath;
            dto.modelType = string.IsNullOrEmpty(modelPath) ? "unknown"
                : modelPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? "prefab" : "asset";
            if (dto.modelType == "prefab")
            {
                dto.modelStructure = so.model.GetComponent<Renderer>() != null
                    ? "single-node（旧结构，保存后自动重建为 root+child）"
                    : "root+child（新结构，变换在子节点上，游戏中生效）";
            }
            else
            {
                dto.modelStructure = "fbx-direct（保存后自动转为本菜谱 prefab）";
            }
            var renderers = so.model.GetComponentsInChildren<Renderer>(true);
            dto.rendererCount = renderers.Length;
            int meshOk = 0;
            int matOk = 0;
            foreach (var r in renderers)
            {
                if (r is SkinnedMeshRenderer)
                {
                    if (((SkinnedMeshRenderer)r).sharedMesh != null)
                        meshOk++;
                }
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                        meshOk++;
                }
                if (r.sharedMaterials != null && r.sharedMaterials.Length > 0 && r.sharedMaterials[0] != null)
                    matOk++;
            }
            dto.meshCount = meshOk;
            dto.materialCount = matOk;
            // Unity 导入后的模型世界包围盒（含当前 prefab 变换；判断实际朝向/薄轴）
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    b.Encapsulate(renderers[i].bounds);
                dto.boundsMinX = b.min.x;
                dto.boundsMinY = b.min.y;
                dto.boundsMinZ = b.min.z;
                dto.boundsSizeX = b.size.x;
                dto.boundsSizeY = b.size.y;
                dto.boundsSizeZ = b.size.z;
            }
        }

        if (so.compositionSOs != null)
            dto.compositionCount = so.compositionSOs.Length;
        dto.cookingStepSet = so.cookingStepSO != null;
        dto.platingStepSet = so.platingStepSO != null;

        var t = RecipeModelTransform(assetPath);
        dto.modelScale = t.scale;
        dto.modelRotationX = t.rotationX;
        dto.modelRotationY = t.rotationY;
        dto.modelRotationZ = t.rotationZ;
        dto.modelPositionX = t.positionX;
        dto.modelPositionY = t.positionY;
        dto.modelPositionZ = t.positionZ;
        dto.modelPivotX = t.pivotX;
        dto.modelPivotY = t.pivotY;
        dto.modelPivotZ = t.pivotZ;
        return dto;
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

        var category = SanitizeName(dto.category ?? "Uncategorized");
        if (string.IsNullOrEmpty(category))
            category = "Uncategorized";

        // 「burger」是 commonW2 Burger大全共享库的保留分类：共享库只读，
        // 新建汉堡请走汉堡工作台（产出归属当前关卡集）。
        if (string.Equals(category, BurgerCategoryId, StringComparison.Ordinal))
            return "「burger」为 Burger大全共享库保留分类，不可作为本关卡集的新建目标；新建汉堡请使用汉堡工作台。";

        var recipesDir = LevelSetsRoot + "/" + setName + "/" + CustomRecipesDir;
        if (!AssetFolderExists(recipesDir))
            return "请先访问自定义菜谱页面以初始化配置。";

        var configPath = recipesDir + "/CustomRecipeConfig.asset";
        var config = AssetDatabase.LoadAssetAtPath<CustomRecipeConfigSO>(configPath);
        if (config == null)
            return "配置文件丢失，请重新进入自定义菜谱页面。";

        // 组成项预校验：全部解析成功才继续（在创建目录/分配 uid 等任何改动之前，
        // 报错路径零副作用）。
        ScriptableObject[] resolvedComps;
        var resolveErr = ResolveCompositionIds(dto.compositionIds, out resolvedComps);
        if (resolveErr != null)
            return resolveErr;

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
            so.compositionSOs = resolvedComps;

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

        so.optionalSOs = new ScriptableObject[0];

        var assetPath = categoryDir + "/" + recipeName + ".asset";
        AssetDatabase.CreateAsset(so, assetPath);
        EditorUtility.SetDirty(so);

        var modelsDir = EnsureRecipeModelsDir(assetPath);
        AssetDatabase.SaveAssets();
        SetRecipeModelTransform(assetPath, dto.modelScale, dto.modelRotationY,
            dto.modelRotationX, dto.modelRotationZ, dto.modelPositionX, dto.modelPositionY, dto.modelPositionZ,
            dto.modelPivotX, dto.modelPivotY, dto.modelPivotZ);
        ApplyModelTransform(so);

        AddCustomRecipeName(CustomRecipeNamesPath(null, assetPath), recipeName, dto.nameZh, dto.nameEn);

        return null;
    }

    public static string UpdateCustomRecipe(CustomRecipeEditDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.assetPath))
            return "缺少资源路径。";

        var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(dto.assetPath);
        if (so == null)
            return "未找到菜谱资源。";

        // 组成项预校验：全部解析成功才进入改动（Undo.RecordObject 之前，
        // 报错路径零副作用、无半保存状态）。
        ScriptableObject[] resolvedComps;
        var resolveErr = ResolveCompositionIds(dto.compositionIds, out resolvedComps);
        if (resolveErr != null)
            return resolveErr;

        Undo.RecordObject(so, "Edit Custom Recipe");
        so.score = dto.score;

        if (dto.type == "Composite")
        {
            // 组合菜：纯组装，无需烹饪与装盘
            so.type = CustomRecipeSO.RecipeType.Composite;
            so.cookingStepSO = null;
            so.cookingStepIconSO = null;
            so.platingStepSO = null;
        }
        else if (dto.type == "Mixed")
        {
            // 搅拌菜：只有搅拌步骤，无需烹饪
            so.type = CustomRecipeSO.RecipeType.Mixed;
            so.cookingStepSO = null;
            so.cookingStepIconSO = null;
        }
        else
            so.type = CustomRecipeSO.RecipeType.Cooked;

        so.cookingProgress = (CustomRecipeSO.CookingProgress)(dto.cookingProgress >= 0 && dto.cookingProgress <= 2 ? dto.cookingProgress : 1);
        so.mixingProgress = (CustomRecipeSO.MixingProgress)(dto.mixingProgress >= 0 && dto.mixingProgress <= 2 ? dto.mixingProgress : 1);
        SetRecipeModelTransform(dto.assetPath, dto.modelScale, dto.modelRotationY,
            dto.modelRotationX, dto.modelRotationZ, dto.modelPositionX, dto.modelPositionY, dto.modelPositionZ,
            dto.modelPivotX, dto.modelPivotY, dto.modelPivotZ);

        if (dto.compositionIds != null)
            so.compositionSOs = resolvedComps;

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

        // 组装定义子类（OptionalBurger/OptionalPizza）的 optionalSOs 是核心数据，
        // 通用编辑表单不展示该字段，此处不能清空（由汉堡工作台等专用入口管理）。
        if (!(so is CustomRecipeOptionalBurgerSO) && !(so is CustomRecipeOptionalPizzaSO))
            so.optionalSOs = new ScriptableObject[0];

        EditorUtility.SetDirty(so);

        var setName = SetNameFromPath(dto.assetPath);
        var id = Path.GetFileNameWithoutExtension(dto.assetPath);
        UpdateCustomRecipeName(CustomRecipeNamesPath(setName, dto.assetPath), id, dto.nameZh, dto.nameEn);

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
        var modelsDir = RecipeModelsDir(assetPath);

        if (!AssetDatabase.DeleteAsset(assetPath))
            return "删除资源失败。";

        // 每个菜谱一个 models 子目录：直接删除本菜谱的子目录（其他菜谱不受影响）。
        if (AssetFolderExists(modelsDir))
            AssetDatabase.DeleteAsset(modelsDir);

        RemoveCustomRecipeName(CustomRecipeNamesPath(setName, assetPath), id);
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
        var modelsDir = EnsureRecipeModelsDir(dto.recipeAssetPath);

        // 统一命名为 <recipeName>_Icon.png，与卡片图标 URL（…/models/<id>/<id>_Icon.png）一致。
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
        AssetDatabase.ImportAsset(imgAssetPath, ImportAssetOptions.ForceUpdate);

        var texImporter = AssetImporter.GetAtPath(imgAssetPath) as TextureImporter;
        if (texImporter != null)
        {
            texImporter.textureType = TextureImporterType.Sprite;
            texImporter.spriteImportMode = SpriteImportMode.Single;
            texImporter.sRGBTexture = true;
            texImporter.alphaIsTransparency = true;
            // 上传即瘦身：图标按默认档导入（只写 .meta，不改源 PNG）。
            if (texImporter.maxTextureSize > LayoutEditorCustomRecipeOptimizeApi.DefaultTextureMaxSize)
                texImporter.maxTextureSize = LayoutEditorCustomRecipeOptimizeApi.DefaultTextureMaxSize;
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

    /// <summary>若 models 目录已有 _Icon 图但 SO 未引用，尝试重新导入并写回 icon 字段。</summary>
    public static void TryRelinkRecipeIconFromDisk(string recipeAssetPath)
    {
        if (string.IsNullOrEmpty(recipeAssetPath))
            return;
        var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(recipeAssetPath);
        if (so == null || so.icon != null)
            return;
        var recipeId = SanitizeName(Path.GetFileNameWithoutExtension(recipeAssetPath));
        var pngPath = RecipeModelsDir(recipeAssetPath) + "/" + recipeId + "_Icon.png";
        if (!File.Exists(AbsPath(pngPath)))
            return;
        var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        if (sprite == null)
            return;
        Undo.RecordObject(so, "Relink Recipe Icon");
        so.icon = sprite;
        so.iconSO = null;
        EditorUtility.SetDirty(so);
    }

    public static CustomRecipeUploadResultDto UploadCustomRecipeModel(CustomRecipeUploadDto dto)
    {
        var okResult = new CustomRecipeUploadResultDto { ok = true };
        if (dto == null || string.IsNullOrEmpty(dto.recipeAssetPath))
            return new CustomRecipeUploadResultDto { ok = false, error = "缺少上传参数。" };

        var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(dto.recipeAssetPath);
        if (so == null)
            return new CustomRecipeUploadResultDto { ok = false, error = "未找到菜谱资源。" };

        // 兼容单文件旧格式：转成 files 数组。
        var files = dto.files != null && dto.files.Length > 0
            ? dto.files
            : (!string.IsNullOrEmpty(dto.fileName) && !string.IsNullOrEmpty(dto.base64)
                ? new[] { new CustomRecipeUploadFileDto { fileName = dto.fileName, base64 = dto.base64 } }
                : null);
        // 模板网格模式允许「一个文件都不传」（纯模板 + 模板自带贴图）。
        if (files == null)
            files = new CustomRecipeUploadFileDto[0];
        if (files.Length == 0 && !dto.useTemplateMesh)
            return new CustomRecipeUploadResultDto { ok = false, error = "缺少上传参数。" };

        CustomRecipeUploadFileDto modelFile = null;
        foreach (var f in files)
        {
            if (f != null && IsModelFileName(f.fileName))
            {
                modelFile = f;
                break;
            }
        }
        // 模板网格模式：没上传模型文件时，从内置模板库拷一份 FBX 字节过来改名。
        // 只拷 .fbx 不拷 .meta —— 新文件由 Unity 生成全新 guid，与模板彻底解耦，
        // 也避免 prefab 反向引用 commonW2 网格把整个 commonW2 bundle 拖成依赖。
        string templateMeshAbs = null;
        if (modelFile == null && dto.useTemplateMesh)
        {
            string tplErr;
            templateMeshAbs = ResolveTemplateMeshAbsPath(dto.templateMeshId, out tplErr);
            if (templateMeshAbs == null)
                return new CustomRecipeUploadResultDto { ok = false, error = tplErr };
        }
        if (modelFile == null && templateMeshAbs == null)
            return new CustomRecipeUploadResultDto { ok = false, error = "请选择 FBX 或 OBJ 模型文件（或勾选使用模板网格）。" };

        var dir = DirectoryName(dto.recipeAssetPath);
        var modelsDir = EnsureRecipeModelsDir(dto.recipeAssetPath);

        // 先清空本条菜谱子目录里的旧模型/旧贴图（保留 <recipeName>_Icon.png 图标），再写入新文件。
        var recipeId = SanitizeName(Path.GetFileNameWithoutExtension(dto.recipeAssetPath));
        var ext = modelFile != null
            && modelFile.fileName != null
            && modelFile.fileName.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)
            ? ".obj" : ".fbx";
        var safeName = recipeId + ext;
        var modelAssetPath = modelsDir + "/" + safeName;
        var prefabPath = modelAssetPath.Substring(0, modelAssetPath.Length - ext.Length) + ".prefab";

        ClearRecipeModelFiles(modelsDir, recipeId);

        // 模板网格：字节拷贝 + 改名（.meta 不拷）
        if (templateMeshAbs != null)
        {
            try
            {
                File.Copy(templateMeshAbs, AbsPath(modelAssetPath), true);
            }
            catch (Exception ex)
            {
                return new CustomRecipeUploadResultDto
                {
                    ok = false,
                    error = "模板网格拷贝失败：" + ex.Message
                };
            }
        }

        // 写入全部文件（主模型统一命名为 <recipeName>.fbx/.obj，贴图保留原文件名）。
        var uploadedTexturePaths = new List<KeyValuePair<CustomRecipeUploadFileDto, string>>();
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
                return new CustomRecipeUploadResultDto { ok = false, error = "文件「" + f.fileName + "」数据解码失败。" };
            }
            var targetName = modelFile != null && ReferenceEquals(f, modelFile)
                ? safeName
                : SanitizeUploadFileName(f.fileName);
            if (string.IsNullOrEmpty(targetName))
                continue;
            File.WriteAllBytes(AbsPath(modelsDir + "/" + targetName), bytes);
            if (modelFile == null || !ReferenceEquals(f, modelFile))
                uploadedTexturePaths.Add(new KeyValuePair<CustomRecipeUploadFileDto, string>(f, modelsDir + "/" + targetName));
        }

        AssetDatabase.Refresh();

        // 上传即瘦身：贴图一律按默认档导入（只写 .meta 的 maxTextureSize，不改源文件，
        // 源图更小时保持原尺寸不放大）；normal 贴图额外标记为 NormalMap，
        // 避免 Unity 按默认贴图类型导入法线图。
        foreach (var kv in uploadedTexturePaths)
        {
            var p = kv.Value;
            if (string.IsNullOrEmpty(p))
                continue;
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp == null)
                continue;
            var changed = false;
            if (p.IndexOf("_normal.", StringComparison.OrdinalIgnoreCase) >= 0
                && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                changed = true;
            }
            if (imp.maxTextureSize != LayoutEditorCustomRecipeOptimizeApi.DefaultTextureMaxSize)
            {
                imp.maxTextureSize = LayoutEditorCustomRecipeOptimizeApi.DefaultTextureMaxSize;
                changed = true;
            }
            if (changed)
                imp.SaveAndReimport();
        }

        // 上传即瘦身：FBX/OBJ 默认导入档（顶点量化 Medium + 关读写副本 + 网格优化）。
        LayoutEditorCustomRecipeOptimizeApi.TryApplyDefaultModelSettings(modelAssetPath);

        // FBX 内部贴图引用名已由前端在上传前改写为贴图落盘文件名（fbxTextureRename），
        // 服务端不再按引用名复制贴图。

        var importedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
        if (importedRoot == null)
            return new CustomRecipeUploadResultDto { ok = false, error = "模型导入失败，请确认文件格式为 FBX 或 OBJ（贴图请使用 PNG/JPG）。" };

        // 覆盖上传时强制重建 prefab：旧 prefab 可能引用已删除的贴图/旧材质（导致灰色）
        var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null)
            AssetDatabase.DeleteAsset(prefabPath);

        // 不创建额外材质球：FBX 内部贴图引用名已改写为贴图落盘文件名，
        // Unity 导入 FBX 时自动把贴图链接进内嵌材质（与美术直接拖 FBX + 贴图使用一致）。
        // 直接生成「空根 + 变换节点 + 模型」结构（游戏运行时重置根节点变换，变换必须放子节点；
        // 原点偏移 pivot 应用到最内层模型节点）。
        // 必须先实例化到场景再 CreatePrefab：直接对模型资产 CreatePrefab 会丢失网格与子节点
        // （生成只有根 Transform 的空 prefab，导致无渲染器）。
        var t = RecipeModelTransform(dto.recipeAssetPath);
        var root = new GameObject(recipeId);
        try
        {
            var tx = new GameObject(recipeId + "_Transform");
            tx.transform.SetParent(root.transform, false);
            ApplyTransformToChild(tx.transform, t);
            var model = UnityEngine.Object.Instantiate(importedRoot);
            model.name = importedRoot.name;
            model.transform.SetParent(tx.transform, false);
            ApplyPivotToModel(model.transform, t);
            var prefabInstance = PrefabUtility.CreatePrefab(prefabPath, root);
            if (prefabInstance == null)
                return new CustomRecipeUploadResultDto { ok = false, error = "创建预制体失败。" };
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LayoutEditor] 创建装盘 prefab 失败: " + prefabPath + " - " + ex.Message);
            return new CustomRecipeUploadResultDto { ok = false, error = "创建预制体失败。" };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
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

        // 返回 Unity 导入后的原始尺寸（不含配置变换），供前端按 Unity 实际尺寸自动校准。
        // prefab 子节点已应用 t 的变换：原始尺寸 = 世界包围盒 ÷ 变换缩放（平移不影响 size；
        // 旋转只交换 X/Z，max 不变）。
        var scaleFactor = t.scale > 0f ? t.scale : 1f;
        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            okResult.rawSizeX = b.size.x / scaleFactor;
            okResult.rawSizeY = b.size.y / scaleFactor;
            okResult.rawSizeZ = b.size.z / scaleFactor;
            okResult.rawMinY = (b.min.y - t.positionY) / scaleFactor;
        }
        return okResult;
    }

    private static bool IsModelFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;
        var n = fileName.ToLowerInvariant();
        return n.EndsWith(".fbx") || n.EndsWith(".obj");
    }

    /// <summary>内置模板网格库：commonW2 汉堡大全的自制模型目录。
    ///  这些 FBX 是「只读模板」—— 模板网格模式只**拷贝字节**到目标菜谱目录并改名，
    ///  绝不修改模板本身，也绝不让生成的 prefab 反向引用模板网格
    ///  （否则关卡集本地夹心会把整个 commonW2 bundle 拖成依赖）。</summary>
    internal const string TemplateMeshDir = "Assets/commonW2/custom_recipes/burger/models";

    /// <summary>默认模板网格：棱角球肉饼形，配纯色贴图即可当夹心。</summary>
    internal const string DefaultTemplateMeshId = "FriedFishCake";

    /// <summary>GET /api/custom-recipes/model-templates：可用模板网格列表。</summary>
    public static CustomRecipeModelTemplateListDto ListTemplateMeshes()
    {
        var list = new List<CustomRecipeModelTemplateDto>();
        var abs = AbsPath(TemplateMeshDir);
        if (Directory.Exists(abs))
        {
            foreach (var file in Directory.GetFiles(abs, "*.fbx"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                list.Add(new CustomRecipeModelTemplateDto
                {
                    id = id,
                    assetPath = TemplateMeshDir + "/" + Path.GetFileName(file),
                    isDefault = string.Equals(id, DefaultTemplateMeshId, StringComparison.Ordinal),
                    sizeBytes = (int)new FileInfo(file).Length
                });
            }
        }
        list.Sort(delegate (CustomRecipeModelTemplateDto a, CustomRecipeModelTemplateDto b)
        {
            if (a.isDefault != b.isDefault)
                return a.isDefault ? -1 : 1;
            return string.CompareOrdinal(a.id, b.id);
        });
        return new CustomRecipeModelTemplateListDto { templates = list.ToArray() };
    }

    /// <summary>模板 id → 绝对路径；不可用时返回 null 并给出原因。
    ///  id 走白名单式校验（只认模板目录下实际存在的 .fbx），杜绝路径穿越。</summary>
    private static string ResolveTemplateMeshAbsPath(string templateMeshId, out string error)
    {
        error = null;
        var id = SanitizeName(string.IsNullOrEmpty(templateMeshId) ? DefaultTemplateMeshId : templateMeshId);
        if (string.IsNullOrEmpty(id))
        {
            error = "模板网格 id 非法。";
            return null;
        }
        var abs = AbsPath(TemplateMeshDir + "/" + id + ".fbx");
        if (!File.Exists(abs))
        {
            error = "模板网格不存在：" + TemplateMeshDir + "/" + id + ".fbx";
            return null;
        }
        return abs;
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
        var allowed = new[] { ".fbx", ".obj", ".mtl", ".png", ".jpg", ".jpeg", ".tga" };
        if (System.Array.IndexOf(allowed, ext) < 0)
            return null;
        return SanitizeName(Path.GetFileNameWithoutExtension(name)) + ext;
    }

    /// <summary>删除 models 子目录内本条菜谱（按 recipeId 前缀）的旧模型/贴图/材质球，
    ///  保留 <recipeId>_Icon.png 图标。每个菜谱一个 models 子目录，不影响其他菜谱。</summary>
    private static void ClearRecipeModelFiles(string dirAssetPath, string recipeId)
    {
        var abs = AbsPath(dirAssetPath);
        if (!Directory.Exists(abs) || string.IsNullOrEmpty(recipeId))
            return;
        var extensions = new[] { ".fbx", ".obj", ".mtl", ".png", ".jpg", ".jpeg", ".tga", ".mat" };
        var prefix = recipeId + "_";
        foreach (var file in Directory.GetFiles(abs))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".meta", StringComparison.Ordinal))
                continue;
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (System.Array.IndexOf(extensions, ext) < 0)
                continue;
            // 图标文件（<recipeId>_Icon.*）始终保留
            if (name.StartsWith(prefix + "Icon", StringComparison.OrdinalIgnoreCase))
                continue;
            // 仅匹配本菜谱命名的文件：主模型 <recipeId>.fbx/.obj、贴图 <recipeId>_*.png 等
            var baseName = Path.GetFileNameWithoutExtension(name);
            if (string.Equals(baseName, recipeId, StringComparison.OrdinalIgnoreCase) ||
                baseName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.DeleteAsset(dirAssetPath + "/" + name);
            }
        }
    }

    /// <summary>列出菜谱 models 目录内的资源文件（供前端 3D 在线预览）。 */
    /// <summary>GET /api/custom-recipes/model-source：解析一道菜谱「可供网页 3D 预览的网格来源」。
    ///
    ///  ListCustomRecipeModelFiles 只看上传约定目录 &lt;菜谱目录&gt;/models/&lt;id&gt;/，
    ///  但 commonW2 的夹心/中间产物是另一种形态：`CustomRecipeSO.model` 直接引用
    ///  `commonW2/custom_recipes/burger/models/` 这个**扁平共享目录**里的 prefab
    ///  （四件套 fbx+png+mat+prefab）。只查约定目录会一律报「尚未上传模型文件」。
    ///
    ///  因此这里两路解析：
    ///   1) 约定目录里有 .fbx/.obj → 返回该目录 + 目录内全部模型/贴图文件（原行为）；
    ///   2) 否则看 so.model → MeshFilter.sharedMesh 的源文件（.fbx/.obj）→ 返回其所在目录，
    ///      文件列表只收「该 prefab 实际用到的」：网格源文件 + 各 Renderer 材质的主贴图，
    ///      避免把扁平目录下几十张无关贴图全灌给 three.js。
    ///  modelSO（bundle 指针）没有本地网格，返回空——网页确实无法预览。</summary>
    public static CustomRecipeModelSourceDto ResolveRecipeModelSource(string recipeAssetPath)
    {
        var result = new CustomRecipeModelSourceDto { dirAssetPath = "", modelFile = "", files = new string[0] };
        if (string.IsNullOrEmpty(recipeAssetPath))
            return result;

        // 1) 上传约定目录
        var uploadDir = RecipeModelsDir(recipeAssetPath);
        var uploadFiles = ListCustomRecipeModelFiles(recipeAssetPath);
        for (int i = 0; i < uploadFiles.Length; i++)
        {
            var ext = Path.GetExtension(uploadFiles[i]).ToLowerInvariant();
            if (ext == ".fbx" || ext == ".obj")
            {
                result.dirAssetPath = uploadDir;
                result.modelFile = uploadFiles[i];
                result.files = uploadFiles;
                return result;
            }
        }

        // 2) so.model 引用的 prefab（commonW2 扁平模型库等）
        var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(recipeAssetPath);
        if (so == null || so.model == null)
            return result;
        var meshFilter = so.model.GetComponentInChildren<MeshFilter>(true);
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return result;
        var meshPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
        if (string.IsNullOrEmpty(meshPath))
            return result;
        var meshExt = Path.GetExtension(meshPath).ToLowerInvariant();
        if (meshExt != ".fbx" && meshExt != ".obj")
            return result;

        var dir = DirectoryName(meshPath);
        var files = new List<string>();
        files.Add(Path.GetFileName(meshPath));

        // 该 prefab 各材质的主贴图（同目录的才收，跨目录 three.js 也拼不出 URL）
        var renderers = so.model.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var mats = renderers[i].sharedMaterials;
            if (mats == null)
                continue;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null || mats[m].mainTexture == null)
                    continue;
                var texPath = AssetDatabase.GetAssetPath(mats[m].mainTexture);
                if (string.IsNullOrEmpty(texPath))
                    continue;
                if (!string.Equals(DirectoryName(texPath), dir, StringComparison.Ordinal))
                    continue;
                var texName = Path.GetFileName(texPath);
                if (!files.Contains(texName))
                    files.Add(texName);
            }
        }
        // OBJ 的 MTL 同名伴随文件
        if (meshExt == ".obj")
        {
            var mtl = Path.GetFileNameWithoutExtension(meshPath) + ".mtl";
            if (File.Exists(AbsPath(dir + "/" + mtl)) && !files.Contains(mtl))
                files.Add(mtl);
        }

        result.dirAssetPath = dir;
        result.modelFile = Path.GetFileName(meshPath);
        result.files = files.ToArray();
        return result;
    }

    public static string[] ListCustomRecipeModelFiles(string recipeAssetPath)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(recipeAssetPath))
            return list.ToArray();
        var dir = RecipeModelsDir(recipeAssetPath);
        var abs = AbsPath(dir);
        if (!Directory.Exists(abs))
            return list.ToArray();
        foreach (var file in Directory.GetFiles(abs))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".meta", StringComparison.Ordinal))
                continue;
            var ext = Path.GetExtension(name).ToLowerInvariant();
            if (System.Array.IndexOf(new[] { ".fbx", ".obj", ".mtl", ".png", ".jpg", ".jpeg", ".tga" }, ext) >= 0)
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
        // 「burger」为 commonW2 Burger大全共享库保留分类 id，关卡集不可占用。
        if (string.Equals(safeId, BurgerCategoryId, StringComparison.Ordinal))
            return "「burger」为 Burger大全共享库保留分类 id，请换一个（如 burgers、hamburger）。";

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
        // 「burger」为 commonW2 Burger大全共享库保留分类 id，关卡集不可占用。
        if (string.Equals(newSafe, BurgerCategoryId, StringComparison.Ordinal) && dto.oldId != BurgerCategoryId)
            return "「burger」为 Burger大全共享库保留分类 id，请换一个（如 burgers、hamburger）。";

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

    /// <summary>（已废弃）Web 内置菜谱库扫描：原合并 Import 源库与 custom_web 副本状态。
    ///  通用内容改为 common03 直接引用 + 前端静态 JSON 后，后端不再动态下发 web 组
    ///  （ScanRecipes 已改扫 common03），本接口仅返回历史 custom_web 残留（通常为空）。</summary>
    public static WebRecipeLibraryDto ScanWebRecipeLibrary(string setName)
    {
        var dto = new WebRecipeLibraryDto { setName = setName, recipes = new WebRecipeEntryDto[0] };
        if (string.IsNullOrEmpty(setName))
            return dto;

        var catalog = LayoutEditorCatalogApi.ScanRecipes("");
        var list = new List<WebRecipeEntryDto>();
        foreach (var r in catalog.recipes)
        {
            if (r.group != "web" || r.intermediate)
                continue;
            if (r.id == "chocolatesmoothie")
                continue;
            list.Add(new WebRecipeEntryDto
            {
                guid = r.guid,
                id = r.id,
                nameZh = r.nameZh,
                nameEn = r.nameEn,
                assetPath = r.assetPath,
                cookingStep = r.cookingStep,
                ingredients = r.ingredients,
                score = r.score,
                type = r.type,
            });
        }
        if (list.Count == 0)
            return dto;

        // 已装副本：custom_web/Recipes 下同 id 资产 → 副本 guid
        var installedGuid = new Dictionary<string, string>(StringComparer.Ordinal);
        var customWebRecipes = LayoutEditorCustomIngredients.CustomIngredientsDir(setName) + "/Recipes";
        if (AssetFolderExists(customWebRecipes))
        {
            foreach (var asset in ScanAssetsByScript(customWebRecipes, OriginalRecipeScriptGuid))
            {
                var id = Path.GetFileNameWithoutExtension(asset.assetPath);
                if (!installedGuid.ContainsKey(id))
                    installedGuid[id] = asset.guid;
            }
        }

        // 被引用：遍历关卡集 LevelInfo.recipes 的文件名
        var usedLevelsByRecipe = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var dataDir = LevelSetsRoot + "/" + setName + "/data";
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
                    if (r == null)
                        continue;
                    var rp = AssetDatabase.GetAssetPath(r);
                    var rid = string.IsNullOrEmpty(rp) ? "" : Path.GetFileNameWithoutExtension(rp);
                    if (string.IsNullOrEmpty(rid))
                        continue;
                    List<string> lv;
                    if (!usedLevelsByRecipe.TryGetValue(rid, out lv))
                    {
                        lv = new List<string>();
                        usedLevelsByRecipe[rid] = lv;
                    }
                    var lname = info.levelName ?? Path.GetFileName(Path.GetDirectoryName(path));
                    if (!lv.Contains(lname))
                        lv.Add(lname);
                }
            }
        }

        // 去重：规范化中文名聚簇，代表 = 最高 DLC（保留 DLC 后缀）
        var groups = new Dictionary<string, List<WebRecipeEntryDto>>(StringComparer.Ordinal);
        foreach (var e in list)
        {
            var key = NormalizeDishName(e.nameZh);
            List<WebRecipeEntryDto> g;
            if (!groups.TryGetValue(key, out g))
            {
                g = new List<WebRecipeEntryDto>();
                groups[key] = g;
            }
            g.Add(e);
        }
        var result = new List<WebRecipeEntryDto>();
        foreach (var kv in groups)
        {
            var arr = kv.Value;
            var rep = arr[0];
            foreach (var e in arr)
                if (DlcNumber(e.id) > DlcNumber(rep.id))
                    rep = e;
            foreach (var e in arr)
            {
                e.dupKey = kv.Key;
                e.representative = e.id == rep.id;
                string ig;
                if (installedGuid.TryGetValue(e.id, out ig))
                {
                    e.installed = true;
                    e.installedGuid = ig;
                }
                List<string> lv;
                if (usedLevelsByRecipe.TryGetValue(e.id, out lv))
                {
                    e.referenced = true;
                    e.referencedBy = lv.ToArray();
                }
                result.Add(e);
            }
        }
        result.Sort((a, b) => string.Compare(a.nameZh, b.nameZh, StringComparison.Ordinal));
        dto.recipes = result.ToArray();
        return dto;
    }

    /// <summary>规范化菜名（去 DLC 后缀与空白），作为 Web 内置菜谱去重键。</summary>
    private static string NormalizeDishName(string zh)
    {
        if (string.IsNullOrEmpty(zh))
            return "";
        var s = System.Text.RegularExpressions.Regex.Replace(zh, @"·?DLC\d+", "");
        return s.Replace("（）", "").Replace("()", "").Replace("·", "").Replace(" ", "").Trim();
    }

    /// <summary>菜谱 id 的 DLC 编号（无 dlcXX_ 前缀为 0），用于去重代表选择。</summary>
    private static int DlcNumber(string id)
    {
        var m = System.Text.RegularExpressions.Regex.Match(id ?? "", @"^dlc(\d+)_");
        if (m.Success)
        {
            int v;
            return int.TryParse(m.Groups[1].Value, out v) ? v : 0;
        }
        return 0;
    }

    internal static bool GloballyUniqueRecipeName(string recipeName)
    {
        if (string.IsNullOrEmpty(recipeName))
            return false;

        foreach (var asset in ScanCustomRecipeAssets(LevelSetsRoot))
        {
            var id = Path.GetFileNameWithoutExtension(asset.assetPath);
            if (string.Equals(id, recipeName, StringComparison.Ordinal))
                return false;
        }
        if (AssetFolderExists(CommonW2RecipesDir))
        {
            foreach (var asset in ScanCustomRecipeAssets(CommonW2RecipesDir))
            {
                var id = Path.GetFileNameWithoutExtension(asset.assetPath);
                if (string.Equals(id, recipeName, StringComparison.Ordinal))
                    return false;
            }
        }
        return true;
    }

    internal static bool IsUidConflicting(int uid)
    {
        var assets = new List<AssetRef>();
        assets.AddRange(ScanCustomRecipeAssets(LevelSetsRoot));
        if (AssetFolderExists(CommonCustomRecipesDir))
            assets.AddRange(ScanCustomRecipeAssets(CommonCustomRecipesDir));
        if (AssetFolderExists(CommonW2RecipesDir))
            assets.AddRange(ScanCustomRecipeAssets(CommonW2RecipesDir));
        foreach (var asset in assets)
        {
            var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(asset.assetPath);
            if (so != null && so.uID == uid)
                return true;
        }
        return false;
    }

    /// <summary>PseudoPrefabSO 的查找目录（食材/烹饪步骤/装盘容器）。
    ///  含通用内容源库（Assets/common03，游戏 DLC 内容，直接打包为 common03 bundle）。</summary>
    private static readonly string[] PseudoPrefabSearchFoldersCore =
    {
        "Assets/common01/food/Ingredients",
        "Assets/common02/food/Ingredients",
        "Assets/common01/food/CookingSteps",
        "Assets/common02/food/CookingSteps",
        "Assets/common01/food/PlatingSteps",
        "Assets/common02/food/PlatingSteps",
        "Assets/common03/food/Ingredients",
        "Assets/common03/food/CookingSteps",
        "Assets/common03/food/PlatingSteps",
    };

    private static string[] _pseudoPrefabSearchFolders;

    /// <summary>PseudoPrefabSO 查找根 = 固定目录 + commonW1 的 food 子目录。
    ///
    ///  commonW1 补充（2026-09-15）：dlc09/dlc10/dlc11/dlc13 的 30 个食材 SO 只存在于
    ///  Assets/commonW1/pseudo_prefab_so/&lt;dlcNN&gt;/food/，此前不在搜索根里 →
    ///  allIngredients 自动填充静默漏掉、FindPseudoPrefabOrCustomRecipe 解析不到
    ///  （Docs/zh/matchlist-mapping.md 第六节「LoadIngredientSo 盲区」记录的 30 项）。
    ///  这里按目录发现动态并入，一次修复全部盲区，且不改动任何 guid。</summary>
    internal static string[] PseudoPrefabSearchFolders
    {
        get
        {
            if (_pseudoPrefabSearchFolders != null)
                return _pseudoPrefabSearchFolders;
            var list = new List<string>(PseudoPrefabSearchFoldersCore);
            list.AddRange(CommonW1FoodFolders());
            _pseudoPrefabSearchFolders = list.ToArray();
            return _pseudoPrefabSearchFolders;
        }
    }

    /// <summary>Assets/commonW1/pseudo_prefab_so/&lt;dlcNN&gt;/food 目录集合（存在才返回）。</summary>
    internal static List<string> CommonW1FoodFolders()
    {
        var folders = new List<string>();
        const string root = "Assets/commonW1/pseudo_prefab_so";
        var abs = AbsPath(root);
        if (!Directory.Exists(abs))
            return folders;
        foreach (var dir in Directory.GetDirectories(abs))
        {
            var foodDir = root + "/" + Path.GetFileName(dir) + "/food";
            if (AssetFolderExists(foodDir))
                folders.Add(foodDir);
        }
        folders.Sort(StringComparer.Ordinal);
        return folders;
    }

    /// <summary>全部关卡集的旧 custom_web/Ingredients 目录（Web 内置食材拷贝，
    ///  机制已废弃，仅为兼容读取历史数据保留）。</summary>
    public static List<string> LevelSetCustomIngredientFolders()
    {
        var folders = new List<string>();
        var setsRoot = LevelSetsRoot;
        if (Directory.Exists(AbsPath(setsRoot)))
        {
            foreach (var dir in Directory.GetDirectories(AbsPath(setsRoot)))
            {
                var setName = Path.GetFileName(dir);
                var ci = setsRoot + "/" + setName + "/custom_web/Ingredients";
                if (AssetFolderExists(ci))
                    folders.Add(ci);
            }
        }
        return folders;
    }

    /// <summary>全项目自定义菜谱查找目录（官方 + commonW2 Burger大全 + 全部关卡集）。</summary>
    private static List<string> CustomRecipeSearchFolders()
    {
        var folders = new List<string>();
        if (AssetFolderExists(CommonCustomRecipesDir))
            folders.Add(CommonCustomRecipesDir);
        if (AssetFolderExists(CommonW2RecipesDir))
            folders.Add(CommonW2RecipesDir);
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
        // 规范 id 回退：DLC 食材的目录 id 取全小写 prefabName（IngredientCatalogId 规则），
        // 与资产文件名不同（如 cherry vs DLC07_Cherry，51 个 DLC 食材全体）。此前仅按
        // 文件名精确匹配，前端从食材目录选出的 id 解析不到 → 保存时被静默丢弃。
        // 这里用与目录生成同一个函数计算规范 id 再比对，按构造保证一致。
        foreach (var folder in folders)
        {
            foreach (var asset in ScanAssetsByScript(folder, scriptGuid))
            {
                var pp = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(asset.assetPath);
                if (pp == null)
                    continue;
                var fileId = Path.GetFileNameWithoutExtension(asset.assetPath);
                var canonicalId = LayoutEditorCatalogApi.IngredientCatalogId(pp, fileId);
                if (!string.Equals(canonicalId, fileId, StringComparison.Ordinal) &&
                    string.Equals(canonicalId, id, StringComparison.Ordinal))
                    return pp;
            }
        }
        return null;
    }

    /// <summary>官方菜谱（PseudoPrefabSORecipe）查找目录：自定义组装菜可直接引用官方成品菜。</summary>
    internal static readonly string[] OfficialRecipeSearchFolders =
    {
        "Assets/common01/food/Recipes",
        "Assets/common02/food/Recipes",
        "Assets/common03/food/Recipes",
    };

    /// <summary>组成 id 列表 → SO 引用（保存前预校验）。
    ///
    ///  任一非空 id 解析不到（FindPseudoPrefabOrCustomRecipe 返回 null）即返回错误
    /// 「无法解析以下组成项，已取消保存：…」并整体取消保存——杜绝历史静默丢数据
    /// （DLC 食材目录 id 与文件名不一致时组成项被直接跳过，保存后消失）。
    /// 调用方必须在改动任何 SO/配置之前先做本校验，保证报错路径零副作用。</summary>
    private static string ResolveCompositionIds(string[] compositionIds, out ScriptableObject[] resolved)
    {
        resolved = new ScriptableObject[0];
        if (compositionIds == null || compositionIds.Length == 0)
            return null;
        var comps = new List<ScriptableObject>();
        var unresolved = new List<string>();
        foreach (var compId in compositionIds)
        {
            if (string.IsNullOrEmpty(compId))
                continue;
            var soFound = FindPseudoPrefabOrCustomRecipe(compId);
            if (soFound != null)
                comps.Add(soFound);
            else
                unresolved.Add(compId);
        }
        if (unresolved.Count > 0)
            return "无法解析以下组成项，已取消保存：" + string.Join("、", unresolved.ToArray());
        resolved = comps.ToArray();
        return null;
    }

    internal static ScriptableObject FindPseudoPrefabOrCustomRecipe(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        var found = FindAssetByIdInFolders(id, PseudoPrefabScriptGuid, PseudoPrefabSearchFolders);
        if (found != null)
            return found;

        // 官方成品菜（组装组成里允许直接引用，如 Burger_Plain_SO）
        found = FindAssetByIdInFolders(id, OriginalRecipeScriptGuid, OfficialRecipeSearchFolders);
        if (found != null)
            return found;

        return FindAssetByIdInFolders(id, CustomRecipeScriptGuid, CustomRecipeSearchFolders());
    }

    internal static PseudoPrefabSO FindPseudoPrefabById(string id)
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
            "Assets/common02/food/PlatingSteps",
            // 马克杯（Mug）在通用内容源库 common03
            "Assets/common03/food/PlatingSteps"
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

    /// <summary>定位 names.json：优先按菜谱资产路径向上找 custom_recipes 目录
    ///  （兼容 commonW2 Burger大全等非 LevelSets 库），否则按关卡集名拼装。</summary>
    internal static string CustomRecipeNamesPath(string setName, string recipeAssetPath)
    {
        if (!string.IsNullOrEmpty(recipeAssetPath))
        {
            var dir = Path.GetDirectoryName(recipeAssetPath.Replace('\\', '/'));
            while (!string.IsNullOrEmpty(dir))
            {
                if (string.Equals(Path.GetFileName(dir), CustomRecipesDir, StringComparison.OrdinalIgnoreCase))
                    return dir + "/names.json";
                var parent = Path.GetDirectoryName(dir);
                if (parent == dir)
                    break;
                dir = parent;
            }
        }
        return string.IsNullOrEmpty(setName)
            ? null
            : LevelSetsRoot + "/" + setName + "/" + CustomRecipesDir + "/names.json";
    }

    internal static void AddCustomRecipeName(string namesPath, string id, string zh, string en)
    {
        if (string.IsNullOrEmpty(namesPath))
            return;
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

    private static void UpdateCustomRecipeName(string namesPath, string id, string zh, string en)
    {
        AddCustomRecipeName(namesPath, id, zh, en);
    }

    private static void RemoveCustomRecipeName(string namesPath, string id)
    {
        if (string.IsNullOrEmpty(namesPath))
            return;
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
