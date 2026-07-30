using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
        var raw = ScanPseudoPrefabs(new[] { "Assets/common02/pseudo_prefab_so/audio/AudioDirectories" });
        var list = new List<AudioDirectoryEntryDto>();
        foreach (var m in raw)
            list.Add(new AudioDirectoryEntryDto { guid = m.guid, id = m.id, assetPath = m.assetPath, bundleName = m.bundleName, nameZh = m.nameZh });
        return new AudioDirectoryCatalogDto { audioDirectories = list.ToArray() };
    }

    public static AmbienceCatalogDto ScanAmbiences()
    {
        var names = new List<string>(Enum.GetNames(typeof(PseudoPrefabManagerStub.GameLoopingAudioTag)));
        names.RemoveAll(n => n == "COUNT");
        return new AmbienceCatalogDto { ambiences = names.ToArray() };
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
        return null;
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
            so.sceneName = dto.sceneName;
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

        EditorUtility.SetDirty(stub);
        ForcePrepareForBuilding();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        ReloadPseudo();
        return null;
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
        // Reload Pseudo Assets so the new death effect is actually loaded (per manual),
        // then strip temp objects again so the user's Ctrl+S won't trip the save validator.
        ReloadPseudo();
        ForcePrepareForBuilding();
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
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
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
}
