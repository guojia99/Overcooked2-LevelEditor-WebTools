using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

public static class LayoutEditorLevelModeStore
{
    private const string StoreFolder = "mode_config~";
    private const string StoreFile = "mode_config.json";
    private const string AssetFolder = "mode_assets";
    public const int MaxJsonBytes = 2097152;
    public const int MaxPhases = 64;
    public const int MaxWavesPerPlayer = 128;
    public const int MaxRecipesPerMenu = 256;
    public const int MaxEnemiesPerWave = 256;
    private const int MaxTotalEntries = 32768;
    private const int MaxSeconds = 86400;
    public const string HordeEnemyCatalogRoot = "Assets/common04/game_environment/horde/enemies";
    private static readonly string[] EnemyNames = {
        "horde_biscuit_a", "horde_candy_b", "horde_puddin_c", "unbread_a", "unbread_b", "unbread_c"
    };

    // GET is deliberately read-only, including when importing native assets without a sidecar.
    public static LevelModeConfigDto Load(string levelInfoPath, LevelInfoSO info)
    {
        var error = ValidateLevelPath(levelInfoPath, info);
        if (error != null) throw new ArgumentException(error);
        if (GetMode(info) == "Campaign") return LoadFromAssets(levelInfoPath, info);
        var path = GetStorePath(levelInfoPath);
        if (!File.Exists(path)) return LoadFromAssets(levelInfoPath, info);
        if (new FileInfo(path).Length > MaxJsonBytes) throw new InvalidDataException("模式 JSON 超过 2 MiB 上限。");
        var dto = ParseJson(File.ReadAllText(path));
        if (dto.gameMode != "Campaign" && dto.gameMode != "Dynamic" && dto.gameMode != "Horde")
            throw new InvalidDataException("模式 sidecar 包含未知模式。");
        if (dto.gameMode != GetMode(info))
        {
            // Undo/history can restore a native mode without restoring this non-Unity file.
            // Keep GET read-only and let the actual runtime graph win in that case.
            return LoadFromAssets(levelInfoPath, info);
        }
        dto.assetPath = levelInfoPath; // Renaming the level directory must not break its sidecar.
        dto.enemies = null;
        // Base-info edits are authoritative during read/sync; an explicit mode Save still
        // applies the supplied counts to both LevelInfo and its sidecar.
        dto.minOrderCount = info.minOrderCount;
        dto.maxOrderCount = info.maxOrderCount;
        error = Validate(dto);
        if (error != null) throw new InvalidDataException("模式 sidecar 校验失败：" + error);
        return dto;
    }

    // HTTP callers should use this before DTO deserialization, in addition to a bounded ReadBody.
    public static LevelModeConfigDto ParseJson(string json)
    {
        if (string.IsNullOrEmpty(json) || json.Length > MaxJsonBytes
            || Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
            throw new InvalidDataException("模式 JSON 为空或超过 2 MiB 上限。");
        var dto = JsonUtility.FromJson<LevelModeConfigDto>(json);
        if (dto == null) throw new InvalidDataException("模式 JSON 无效。");
        return dto;
    }

    public static string Save(string levelInfoPath, LevelInfoSO info, LevelModeConfigDto dto)
    {
        var block = LayoutEditorSafety.PlayModeWriteBlockReason();
        if (!string.IsNullOrEmpty(block)) return block;
        string error;
        try { error = ValidateForSave(levelInfoPath, info, dto); }
        catch (Exception ex) { return "模式配置校验失败：" + ex.Message; }
        if (error != null) return error;
        // Never normalize invalid input into valid data or mutate the caller's DTO.
        var saved = JsonUtility.FromJson<LevelModeConfigDto>(JsonUtility.ToJson(dto));
        saved.assetPath = levelInfoPath;
        saved.enemies = null;
        RecipeDataSnapshot snapshot;
        try { snapshot = new RecipeDataSnapshot(info); }
        catch (Exception ex) { return "模式配置快照失败：" + ex.Message; }
        try
        {
            Undo.RecordObject(info, "Save level mode config");
            if (!NativeMatches(levelInfoPath, info, saved))
                SynchronizeAssets(levelInfoPath, info, saved);
            ApplySettings(info, saved);
            SyncRecipeData(info);
            EditorUtility.SetDirty(info);
            AssetDatabase.SaveAssets();
            SaveFile(levelInfoPath, saved);
            snapshot.Commit();
            return null;
        }
        catch (Exception ex)
        {
            var recovery = snapshot.Rollback();
            return "保存模式配置失败：" + ex.Message
                + (recovery == null ? "（已恢复菜谱及派生资产）" : "；恢复不完整：" + recovery);
        }
        finally { snapshot.Dispose(); }
    }

    // Capture before changing menus/player configs. The caller owns its scene/Undo
    // transaction; Rollback restores recipe assets and removes only newly created revisions.
    public sealed class RecipeDataSnapshot : IDisposable
    {
        private readonly Dictionary<UnityEngine.Object, UnityEngine.Object> objects =
            new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        private readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly HashSet<string> revisions = new HashSet<string>(StringComparer.Ordinal);
        private readonly string modeRoot;
        private readonly bool hadModeRoot;
        private readonly string optionalPath;
        private readonly bool hadOptional;
        private int undoGroup = -1;

        public RecipeDataSnapshot(LevelInfoSO info)
        {
            var path = AssetDatabase.GetAssetPath(info);
            var error = ValidateLevelPath(path, info);
            if (error != null) throw new ArgumentException(error);
            modeRoot = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + AssetFolder;
            optionalPath = LayoutEditorBurgerApi.LevelBurgerOptionalAssetPath(path);
            hadModeRoot = AssetDatabase.IsValidFolder(modeRoot);
            hadOptional = AssetDatabase.LoadMainAssetAtPath(optionalPath) != null || File.Exists(AbsPath(optionalPath));
            try
            {
                CaptureObject(info);
                foreach (var config in PlayerConfigs(info)) CaptureObject(config);
                CaptureObject(AssetDatabase.LoadMainAssetAtPath(optionalPath));
                CaptureFile(optionalPath);
                CaptureFile(modeRoot);
                CaptureFile(GetStorePath(path));
                var parts = path.Split('/');
                var recipesRoot = "Assets/LevelSets/" + parts[2] + "/custom_recipes";
                CaptureObject(AssetDatabase.LoadAssetAtPath<CustomRecipeConfigSO>(recipesRoot + "/CustomRecipeConfig.asset"));
                CaptureFile(LayoutEditorLevelAdminApi.CustomRecipeNamesPath(null, optionalPath));
                if (hadModeRoot)
                    foreach (var folder in AssetDatabase.GetSubFolders(modeRoot)) revisions.Add(folder);
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                    if (!scene.IsValid() || !scene.isLoaded) continue;
                    var roots = scene.GetRootGameObjects();
                    var ownsScene = false;
                    foreach (var root in roots)
                        foreach (var manager in root.GetComponentsInChildren<PseudoPrefabManagerStub>(true))
                            if (manager.levelInfo == info) ownsScene = true;
                    if (ownsScene)
                        foreach (var root in roots) Undo.RegisterFullObjectHierarchyUndo(root, "Sync recipe consumers");
                }
            }
            catch { Dispose(); throw; }
        }

        private void CaptureObject(UnityEngine.Object asset)
        {
            if (asset == null || objects.ContainsKey(asset)) return;
            var copy = UnityEngine.Object.Instantiate(asset);
            copy.name = asset.name;
            objects.Add(asset, copy);
            CaptureFile(AssetDatabase.GetAssetPath(asset));
        }

        private void CaptureFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var absolute = Path.IsPathRooted(path) ? path : AbsPath(path);
            if (!files.ContainsKey(absolute)) files.Add(absolute, File.Exists(absolute) ? File.ReadAllBytes(absolute) : null);
            if (!files.ContainsKey(absolute + ".meta"))
                files.Add(absolute + ".meta", File.Exists(absolute + ".meta") ? File.ReadAllBytes(absolute + ".meta") : null);
        }

        public string Rollback()
        {
            var failures = new List<string>();
            try { if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup); }
            catch (Exception ex) { failures.Add("恢复场景：" + ex.Message); }
            foreach (var pair in objects)
            {
                try { EditorUtility.CopySerialized(pair.Value, pair.Key); EditorUtility.SetDirty(pair.Key); }
                catch (Exception ex) { failures.Add("恢复对象：" + ex.Message); }
            }
            // A nested helper may already have called SaveAssets. Restore exact disk
            // bytes without another global save that could flush unrelated dirty assets.
            try
            {
                if (!hadOptional && (AssetDatabase.LoadMainAssetAtPath(optionalPath) != null || File.Exists(AbsPath(optionalPath)))
                    && !AssetDatabase.DeleteAsset(optionalPath)) throw new IOException("无法清理 " + optionalPath);
                if (AssetDatabase.IsValidFolder(modeRoot))
                {
                    foreach (var folder in AssetDatabase.GetSubFolders(modeRoot))
                        if (!revisions.Contains(folder) && Path.GetFileName(folder).StartsWith("r_", StringComparison.Ordinal)
                            && !AssetDatabase.DeleteAsset(folder)) throw new IOException("无法清理 " + folder);
                    if (!hadModeRoot && AssetDatabase.GetSubFolders(modeRoot).Length == 0
                        && Directory.GetFiles(AbsPath(modeRoot)).Length == 0 && !AssetDatabase.DeleteAsset(modeRoot))
                        throw new IOException("无法清理 " + modeRoot);
                }
            }
            catch (Exception ex) { failures.Add("清理新增资产：" + ex.Message); }
            foreach (var pair in files)
            {
                try
                {
                    if (pair.Value == null) { if (File.Exists(pair.Key)) File.Delete(pair.Key); }
                    else
                    {
                        var current = File.Exists(pair.Key) ? File.ReadAllBytes(pair.Key) : null;
                        var same = current != null && current.Length == pair.Value.Length;
                        for (int i = 0; same && i < current.Length; i++) same = current[i] == pair.Value[i];
                        if (!same) File.WriteAllBytes(pair.Key, pair.Value);
                    }
                }
                catch (Exception ex) { failures.Add("恢复文件 " + pair.Key + "：" + ex.Message); }
            }
            return failures.Count == 0 ? null : string.Join("；", failures.ToArray());
        }

        public void Commit()
        {
            if (undoGroup >= 0) Undo.CollapseUndoOperations(undoGroup);
        }

        public void Dispose()
        {
            foreach (var copy in objects.Values) UnityEngine.Object.DestroyImmediate(copy);
            objects.Clear();
        }
    }

    public static string ValidateForSave(string levelInfoPath, LevelInfoSO info, LevelModeConfigDto dto)
    {
        var error = ValidateModeDataForSave(levelInfoPath, info, dto);
        if (error != null) return error;
        return LayoutEditorNativeModeValidation.ValidateForSave(levelInfoPath, info,
            dto.gameMode == "Dynamic" ? (int?)dto.phases.Length : null);
    }

    // A mode transaction temporarily has a target LevelInfo and a source controller.
    // Validate its asset data here; only normal Save/final validation may inspect the scene.
    private static string ValidateModeDataForSave(string levelInfoPath, LevelInfoSO info, LevelModeConfigDto dto)
    {
        var error = ValidateLevelPath(levelInfoPath, info);
        if (error != null) return error;
        if (dto == null) return "缺少模式配置。";
        if (!string.IsNullOrEmpty(dto.assetPath) && dto.assetPath != levelInfoPath)
            return "模式配置 assetPath 与请求关卡不一致。";
        if (dto.gameMode != GetMode(info))
            return "模式配置 API 不切换场景控制器，请使用 ChangeLevelMode。";
        if (dto.gameMode == "Campaign") return "Campaign 菜谱使用原有菜谱 API，不能写入模式 sidecar。";
        error = Validate(dto);
        if (error != null) return error;
        var configs = PlayerConfigs(info);
        for (int p = 0; p < configs.Length; p++)
            if (configs[p] == null) return "缺少 " + (p + 1) + "P 原生人数配置。";
        return null;
    }

    // Preflight the whole set before synchronizing any level. The caller must stop export on error.
    public static string SyncSavedForExport(string setName)
    {
        if (!SafeSegment(setName) || setName.IndexOf('/') >= 0) return "无效的关卡集名称。";
        var root = "Assets/LevelSets/" + setName + "/data";
        if (!AssetDatabase.IsValidFolder(root)) return "未找到关卡集数据目录。";
        var infos = new List<LevelInfoSO>();
        var paths = new List<string>();
        var configs = new List<LevelModeConfigDto>();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:LevelInfoSO", new[] { root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(path);
                var error = ValidateLevelPath(path, info);
                if (error != null) return path + "：" + error;
                if (GetMode(info) == "Campaign")
                {
                    infos.Add(info); paths.Add(path); configs.Add(null);
                    continue;
                }
                var dto = Load(path, info);
                error = ValidateForSave(path, info, dto);
                if (error != null) return path + "：" + error;
                infos.Add(info); paths.Add(path); configs.Add(dto);
            }
            for (int i = 0; i < infos.Count; i++)
            {
                var error = configs[i] == null ? SyncSaved(paths[i], infos[i]) : Save(paths[i], infos[i], configs[i]);
                if (error != null) return paths[i] + "：" + error;
            }
            return null;
        }
        catch (Exception ex) { return "同步导出模式配置失败：" + ex.Message; }
    }

    // Call after the legacy recipe synchronizers and before scene save / export build.
    // Resolve by exact scene path, never by whichever manager happens to be active.
    public static string SyncSavedForScene(string sceneAssetPath)
    {
        if (string.IsNullOrEmpty(sceneAssetPath)) return "缺少场景路径。";
        var parts = sceneAssetPath.Split('/');
        if (parts.Length != 5 || parts[0] != "Assets" || parts[1] != "LevelSets"
            || parts[3] != "scenes" || !SafeSegment(parts[2]) || !SafeSegment(parts[4])
            || !sceneAssetPath.EndsWith(".unity", StringComparison.Ordinal))
            return "场景路径必须位于 Assets/LevelSets/<set>/scenes/。";
        try { ValidatePhysicalPath(sceneAssetPath); }
        catch (Exception ex) { return "场景路径不可访问：" + ex.Message; }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sceneAssetPath) == null) return "未找到真实场景资源。";
        var dataRoot = "Assets/LevelSets/" + parts[2] + "/data";
        if (!AssetDatabase.IsValidFolder(dataRoot)) return "场景没有对应的关卡数据目录。";
        LevelInfoSO found = null;
        foreach (var guid in AssetDatabase.FindAssets("t:LevelInfoSO", new[] { dataRoot }))
        {
            var info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (info == null || info.sceneName != Path.GetFileNameWithoutExtension(sceneAssetPath)) continue;
            if (found != null) return "同一场景对应多个 LevelInfoSO，拒绝同步。";
            found = info;
        }
        if (found == null) return "未找到场景对应的 LevelInfoSO。";
        return SyncSaved(AssetDatabase.GetAssetPath(found), found);
    }

    public static string SyncSaved(string levelInfoPath, LevelInfoSO info)
    {
        var error = ValidateLevelPath(levelInfoPath, info);
        if (error != null) return error;
        if (GetMode(info) == "Campaign")
        {
            RecipeDataSnapshot snapshot;
            try { snapshot = new RecipeDataSnapshot(info); }
            catch (Exception ex) { return "Campaign 菜谱快照失败：" + ex.Message; }
            using (snapshot)
            {
                try { SyncRecipeData(info); AssetDatabase.SaveAssets(); snapshot.Commit(); return null; }
                catch (Exception ex)
                {
                    var recovery = snapshot.Rollback();
                    return "同步 Campaign 菜谱失败：" + ex.Message + (recovery == null ? "" : "；恢复不完整：" + recovery);
                }
            }
        }
        try { return Save(levelInfoPath, info, Load(levelInfoPath, info)); }
        catch (Exception ex) { return "读取模式配置失败：" + ex.Message; }
    }

    // Collect active native menus for dependency/cooking-step consumers without changing recipes.
    public static ScriptableObject[] CollectAllRecipes(LevelInfoSO info)
    {
        var result = new List<ScriptableObject>();
        var seen = new HashSet<ScriptableObject>();
        if (info == null) return result.ToArray();
        if (GetMode(info) == "Campaign") AddRecipes(info.recipes, result, seen);
        if (GetMode(info) == "Dynamic" && info.recipeListSOs != null)
            foreach (var list in info.recipeListSOs)
                if (list != null) AddRecipes(list.recipes, result, seen);
        foreach (var config in PlayerConfigs(info))
        {
            if (config == null) continue;
            if (GetMode(info) == "Campaign") AddRecipes(config.scriptedRecipes, result, seen);
            var hc = config.hordeLevelConfig;
            if (GetMode(info) != "Horde" || hc == null || hc.hordeWaveDataSOs == null) continue;
            foreach (var wave in hc.hordeWaveDataSOs)
                if (wave != null && wave.recipeListSO != null) AddRecipes(wave.recipeListSO.recipes, result, seen);
        }
        return result.ToArray();
    }

    // Native graph roots for dependency/export consumers; repeated spawn entries remain in waves.
    public static UnityEngine.Object[] CollectModeReferences(LevelInfoSO info)
    {
        var result = new List<UnityEngine.Object>();
        if (info == null) return result.ToArray();
        result.AddRange(CollectAllRecipes(info));
        result.AddRange(CollectHordeEnemies(info));
        if (GetMode(info) == "Dynamic" && info.recipeListSOs != null)
            foreach (var list in info.recipeListSOs)
                if (list != null && !result.Contains(list)) result.Add(list);
        foreach (var player in PlayerConfigs(info))
        {
            if (player == null) continue;
            if (!result.Contains(player)) result.Add(player);
            var hc = player.hordeLevelConfig;
            if (GetMode(info) != "Horde" || hc == null) continue;
            if (!result.Contains(hc)) result.Add(hc);
            if (hc.hordeWaveDataSOs == null) continue;
            foreach (var wave in hc.hordeWaveDataSOs)
            {
                if (wave == null) continue;
                if (!result.Contains(wave)) result.Add(wave);
                if (wave.recipeListSO != null && !result.Contains(wave.recipeListSO)) result.Add(wave.recipeListSO);
            }
        }
        return result.ToArray();
    }

    public static PseudoPrefabSO[] CollectHordeEnemies(LevelInfoSO info)
    {
        var result = new List<PseudoPrefabSO>();
        if (info == null || GetMode(info) != "Horde") return result.ToArray();
        foreach (var config in PlayerConfigs(info))
        {
            var hc = config != null ? config.hordeLevelConfig : null;
            if (hc == null || hc.hordeWaveDataSOs == null) continue;
            foreach (var wave in hc.hordeWaveDataSOs)
                if (wave != null && wave.enemies != null)
                    foreach (var enemy in wave.enemies)
                        if (enemy != null && !result.Contains(enemy)) result.Add(enemy);
        }
        return result.ToArray();
    }

    public static bool IsHordeEnemyPath(string path)
    {
        foreach (var name in EnemyNames)
            if (path == HordeEnemyCatalogRoot + "/" + name + ".asset") return true;
        return false;
    }

    public static string ValidateLevelPath(string path, LevelInfoSO info)
    {
        if (info == null || string.IsNullOrEmpty(path)) return "缺少 LevelInfoSO 或路径。";
        var parts = path.Split('/');
        if (parts.Length != 6 || parts[0] != "Assets" || parts[1] != "LevelSets"
            || parts[3] != "data" || !SafeSegment(parts[2]) || !SafeSegment(parts[4])
            || !SafeSegment(parts[5]) || !path.EndsWith(".asset", StringComparison.Ordinal))
            return "模式资产必须位于 Assets/LevelSets/<set>/data/<level>/。";
        if (AssetDatabase.GetAssetPath(info) != path || AssetDatabase.LoadMainAssetAtPath(path) != info)
            return "LevelInfoSO 与请求路径不一致。";
        try
        {
            ValidatePhysicalPath(path);
            ValidatePhysicalPath(Path.GetDirectoryName(path).Replace('\\', '/') + "/" + AssetFolder);
            ValidatePhysicalPath(Path.GetDirectoryName(path).Replace('\\', '/') + "/" + StoreFolder + "/" + StoreFile);
        }
        catch (Exception ex) { return "模式路径不可访问：" + ex.Message; }
        var root = Path.GetDirectoryName(path).Replace('\\', '/');
        foreach (var guid in AssetDatabase.FindAssets("t:LevelInfoSO", new[] { root }))
            if (AssetDatabase.GUIDToAssetPath(guid) != path)
                return "同一关卡目录含多个 LevelInfoSO，不能共用模式 sidecar。";
        return null;
    }

    private static bool SafeSegment(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 255 || value == "." || value == ".."
            || value.EndsWith(".", StringComparison.Ordinal) || value.Trim() != value
            || value.IndexOfAny(new[] { '/', '\\', ':', '~', '\0', '<', '>', '"', '|', '?', '*' }) >= 0) return false;
        foreach (var c in value) if (char.IsControl(c)) return false;
        return true;
    }

    private static void ValidatePhysicalPath(string assetPath)
    {
        var current = Directory.GetParent(Application.dataPath).FullName;
        foreach (var segment in assetPath.Split('/'))
        {
            current = Path.Combine(current, segment);
            if ((Directory.Exists(current) || File.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("不允许符号链接或重解析路径：" + assetPath);
        }
    }

    internal static LevelModeConfigDto LoadFromAssets(string path, LevelInfoSO info)
    {
        if (info == null) throw new ArgumentNullException("info");
        if (GetMode(info) == "Dynamic" && (info.recipeListSOs == null || info.roundTimes == null
            || info.recipeListSOs.Length > MaxPhases || info.roundTimes.Length != info.recipeListSOs.Length))
            throw new InvalidDataException("原生 Dynamic 菜谱阶段和时长必须等长且不超过 64。");
        var dto = new LevelModeConfigDto { assetPath = path, gameMode = GetMode(info),
            targetHealth = info.targetHealth, targetRepairSpeed = info.targetRepairSpeed,
            targetRepairThreshold = info.targetRepairThreshold, kitchenHealth = info.kitchenHealth,
            minOrderCount = info.minOrderCount, maxOrderCount = info.maxOrderCount };
        if (dto.gameMode == "Dynamic")
        {
            var phases = new List<LevelModeRecipePhaseDto>();
            for (int i = 0; info.recipeListSOs != null && i < info.recipeListSOs.Length; i++)
            {
                var list = info.recipeListSOs[i];
                phases.Add(new LevelModeRecipePhaseDto { id = "phase_" + (i + 1),
                    durationSeconds = info.roundTimes != null && i < info.roundTimes.Length ? info.roundTimes[i] : 0,
                    recipeGuids = AssetGuids(list != null ? list.recipes : null) });
            }
            dto.phases = phases.ToArray();
        }
        if (dto.gameMode == "Horde")
        {
            dto.hordeConfigs = new HordePlayerConfigDto[4];
            var configs = PlayerConfigs(info);
            for (int p = 0; p < 4; p++)
            {
                var hc = configs[p] != null ? configs[p].hordeLevelConfig : null;
                if (hc == null || hc.hordeWaveDataSOs == null || hc.hordeWaveDataSOs.Length > MaxWavesPerPlayer)
                    throw new InvalidDataException((p + 1) + "P 原生 Horde 配置缺失或超过 128 波。");
                var waves = new List<HordeWaveDto>();
                for (int w = 0; hc != null && hc.hordeWaveDataSOs != null && w < hc.hordeWaveDataSOs.Length; w++)
                {
                    var wave = hc.hordeWaveDataSOs[w];
                    waves.Add(wave == null ? null : new HordeWaveDto { id = "wave_" + (w + 1),
                        intervalSeconds = wave.intervalSeconds,
                        recipeGuids = AssetGuids(wave.recipeListSO != null ? wave.recipeListSO.recipes : null),
                        enemyGuids = AssetGuids(wave.enemies),
                        spawnTimes = wave.spawnTimes != null ? (int[])wave.spawnTimes.Clone() : null });
                }
                dto.hordeConfigs[p] = new HordePlayerConfigDto {
                    targetRepairCostMax = hc != null ? hc.targetRepairCostMax : 100, waves = waves.ToArray() };
            }
        }
        return dto;
    }

    private static void ApplySettings(LevelInfoSO info, LevelModeConfigDto dto)
    {
        info.targetHealth = dto.targetHealth;
        info.targetRepairSpeed = dto.targetRepairSpeed;
        info.targetRepairThreshold = dto.targetRepairThreshold;
        info.kitchenHealth = dto.kitchenHealth;
        info.minOrderCount = dto.minOrderCount;
        info.maxOrderCount = dto.maxOrderCount;
    }

    private static void SynchronizeAssets(string path, LevelInfoSO info, LevelModeConfigDto dto)
    {
        var root = Path.GetDirectoryName(path).Replace('\\', '/');
        var parent = root + "/" + AssetFolder;
        if (!AssetDatabase.IsValidFolder(parent) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(root, AssetFolder)))
            throw new IOException("无法创建模式资产目录。");
        var revision = "r_" + Guid.NewGuid().ToString("N");
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, revision))) throw new IOException("无法创建模式版本目录。");
        var folder = parent + "/" + revision;
        var importer = AssetImporter.GetAtPath(folder);
        if (importer == null) throw new IOException("无法读取模式版本目录 importer。");
        var bundle = AssetDatabase.GetImplicitAssetBundleName(path);
        if (string.IsNullOrEmpty(bundle)) bundle = path.Split('/')[2] + "/info_" + path.Split('/')[2];
        importer.assetBundleName = bundle;
        importer.SaveAndReimport();
        if (dto.gameMode == "Dynamic")
        {
            var lists = new RecipeListSO[dto.phases.Length];
            var times = new int[lists.Length];
            for (int i = 0; i < lists.Length; i++)
            {
                lists[i] = CreateRecipeList(folder + "/phase_" + (i + 1) + "_recipes.asset", dto.phases[i].recipeGuids);
                times[i] = dto.phases[i].durationSeconds;
            }
            info.recipeListSOs = lists;
            info.roundTimes = times;
            return;
        }
        var players = PlayerConfigs(info);
        var copies = new LevelConfigSetupPerPlayerCountSO[4];
        for (int p = 0; p < 4; p++)
        {
            // Clone even a local player config: historical levels can share it with another level.
            copies[p] = UnityEngine.Object.Instantiate(players[p]);
            CreateAsset(copies[p], folder + "/player_" + (p + 1) + ".asset");
            var source = players[p].hordeLevelConfig;
            var hc = source != null ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<HordeLevelConfigSO>();
            CreateAsset(hc, folder + "/horde_" + (p + 1) + ".asset");
            var config = dto.hordeConfigs[p];
            hc.targetRepairCostMax = config.targetRepairCostMax;
            hc.hordeWaveDataSOs = new HordeWaveDataSO[config.waves.Length];
            for (int w = 0; w < config.waves.Length; w++)
            {
                var waveDto = config.waves[w];
                var prefix = folder + "/player_" + (p + 1) + "_wave_" + (w + 1);
                var wave = ScriptableObject.CreateInstance<HordeWaveDataSO>();
                // Wave and menu are distinct assets; never delete stale or manually authored assets.
                wave.recipeListSO = CreateRecipeList(prefix + "_recipes.asset", waveDto.recipeGuids);
                wave.intervalSeconds = waveDto.intervalSeconds;
                wave.enemies = new PseudoPrefabSO[waveDto.enemyGuids.Length];
                for (int e = 0; e < wave.enemies.Length; e++) wave.enemies[e] = ResolveEnemy(waveDto.enemyGuids[e]);
                wave.spawnTimes = (int[])waveDto.spawnTimes.Clone();
                CreateAsset(wave, prefix + "_wave.asset");
                hc.hordeWaveDataSOs[w] = wave;
            }
            copies[p].hordeLevelConfig = hc;
            EditorUtility.SetDirty(hc);
            EditorUtility.SetDirty(copies[p]);
        }
        info.config_1p = copies[0]; info.config_2p = copies[1];
        info.config_3p = copies[2]; info.config_4p = copies[3];
    }

    // Append-only asset staging; the caller owns the LevelInfo/scene/sidecar commit and rollback.
    internal static void StageModeChange(string path, LevelInfoSO info, LevelModeConfigDto dto)
    {
        var error = ValidateModeDataForSave(path, info, dto);
        if (error != null) throw new InvalidDataException(error);
        SynchronizeAssets(path, info, dto);
        ApplySettings(info, dto);
        SyncRecipeData(info);
        EditorUtility.SetDirty(info);
    }

    internal static void CommitModeSidecar(string path, LevelModeConfigDto dto)
    {
        SaveFile(path, dto);
    }

    internal static void ValidateStagedModeChange(string path, LevelInfoSO info, LevelModeConfigDto dto)
    {
        var error = ValidateForSave(path, info, dto);
        if (error != null || !NativeMatches(path, info, dto))
            throw new InvalidDataException(error ?? "已生成的原生模式图与配置不一致或存在共享资产。");
    }

    internal static string ModeSidecarPath(string path)
    {
        return GetStorePath(path);
    }

    private static RecipeListSO CreateRecipeList(string path, string[] guids)
    {
        var list = ScriptableObject.CreateInstance<RecipeListSO>();
        list.recipes = new ScriptableObject[guids.Length];
        for (int i = 0; i < guids.Length; i++) list.recipes[i] = ResolveRecipe(guids[i]);
        CreateAsset(list, path);
        return list;
    }

    private static void CreateAsset(ScriptableObject asset, string path)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(AbsPath(path)))
            throw new IOException("模式资产路径已存在：" + path);
        asset.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        if (AssetDatabase.GetAssetPath(asset) != path) throw new IOException("模式资产创建失败：" + path);
        EditorUtility.SetDirty(asset);
    }

    private static bool NativeMatches(string path, LevelInfoSO info, LevelModeConfigDto dto)
    {
        LevelModeConfigDto actual;
        try { actual = LoadFromAssets(path, info); }
        catch (InvalidDataException) { return false; }
        // IDs are Web identities, not native asset names.
        if (dto.gameMode == "Dynamic")
        {
            if (actual.phases.Length != dto.phases.Length || info.roundTimes == null
                || info.roundTimes.Length != dto.phases.Length) return false;
            for (int i = 0; i < actual.phases.Length; i++) actual.phases[i].id = dto.phases[i].id;
        }
        else
        {
            for (int p = 0; p < 4; p++)
            {
                if (actual.hordeConfigs[p].waves.Length != dto.hordeConfigs[p].waves.Length) return false;
                for (int w = 0; w < actual.hordeConfigs[p].waves.Length; w++)
                {
                    if (actual.hordeConfigs[p].waves[w] == null) return false;
                    actual.hordeConfigs[p].waves[w].id = dto.hordeConfigs[p].waves[w].id;
                }
            }
        }
        // Only compare mode data. Inactive mode arrays are not serialized to native assets.
        actual.phases = dto.gameMode == "Dynamic" ? actual.phases : null;
        actual.hordeConfigs = dto.gameMode == "Horde" ? actual.hordeConfigs : null;
        var wanted = JsonUtility.FromJson<LevelModeConfigDto>(JsonUtility.ToJson(dto));
        wanted.phases = dto.gameMode == "Dynamic" ? wanted.phases : null;
        wanted.hordeConfigs = dto.gameMode == "Horde" ? wanted.hordeConfigs : null;
        // Scalar settings do not change graph ownership and must not generate a new revision.
        actual.targetHealth = wanted.targetHealth;
        actual.targetRepairSpeed = wanted.targetRepairSpeed;
        actual.targetRepairThreshold = wanted.targetRepairThreshold;
        actual.kitchenHealth = wanted.kitchenHealth;
        actual.minOrderCount = wanted.minOrderCount;
        actual.maxOrderCount = wanted.maxOrderCount;
        wanted.enemies = null;
        if (JsonUtility.ToJson(actual) != JsonUtility.ToJson(wanted)) return false;
        var owned = new HashSet<UnityEngine.Object>();
        if (dto.gameMode == "Dynamic")
        {
            foreach (var list in info.recipeListSOs) if (!IsOwned(path, list, owned)) return false;
        }
        else
        {
            foreach (var player in PlayerConfigs(info))
            {
                if (!IsOwned(path, player, owned) || !IsOwned(path, player.hordeLevelConfig, owned)) return false;
                foreach (var wave in player.hordeLevelConfig.hordeWaveDataSOs)
                    if (!IsOwned(path, wave, owned) || !IsOwned(path, wave.recipeListSO, owned)) return false;
            }
        }
        return true;
    }

    private static bool IsOwned(string infoPath, UnityEngine.Object asset, HashSet<UnityEngine.Object> seen)
    {
        var prefix = Path.GetDirectoryName(infoPath).Replace('\\', '/') + "/" + AssetFolder + "/r_";
        if (asset == null || !seen.Add(asset)) return false;
        var path = AssetDatabase.GetAssetPath(asset);
        if (!path.StartsWith(prefix, StringComparison.Ordinal)) return false;
        ValidatePhysicalPath(path);
        return true;
    }

    // Recipe saves and export may target a level whose scene is not currently loaded.
    internal static bool OwnsRecipeConsumer(LevelInfoSO info, Component component)
    {
        if (info == null || component == null) return false;
        var scene = component.gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded) return false;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var manager in root.GetComponentsInChildren<PseudoPrefabManagerStub>(true))
                if (manager.levelInfo == info) return true;
        return false;
    }

    public static void SyncRecipeData(LevelInfoSO info)
    {
        if (info == null) return;
        var error = ValidateLevelPath(AssetDatabase.GetAssetPath(info), info);
        if (error != null) throw new ArgumentException(error);
        LayoutEditorAllIngredientsFill.AutoFillIngredientsFromSelectedRecipes(info);
        LayoutEditorCatalogApi.SyncCookingStepsFromSelectedRecipes(info);
        LayoutEditorCatalogApi.SyncSpecialRecipeOptionals(info);
        LayoutEditorMatchlistMap.EnsureRequiredMatchlists(info);
        var parts = AssetDatabase.GetAssetPath(info).Split('/');
        LayoutEditorCustomIngredients.EnsureWebDependencies(parts.Length > 2 ? parts[2] : null, info, false);
        LayoutEditorRoastTrayFill.SyncRoastTrayIngredients(info);
        LayoutEditorHotPotFill.SyncHotPotIngredients(info);
        LayoutEditorSteamerFill.SyncSteamerIngredients(info);
        LayoutEditorPancakeFill.SyncLoadedScenes(info);
        EditorUtility.SetDirty(info);
    }

    internal static string Validate(LevelModeConfigDto dto)
    {
        if (dto == null) return "缺少模式配置。";
        if (dto.assetPath != null && dto.assetPath.Length > 1024) return "资源路径过长。";
        if (dto.enemies != null && dto.enemies.Length > EnemyNames.Length) return "敌人目录超过六种上限。";
        if (dto.minOrderCount < 1 || dto.maxOrderCount > 10 || dto.maxOrderCount < dto.minOrderCount)
            return "订单数量必须满足 1 <= minOrderCount <= maxOrderCount <= 10。";
        if (dto.targetHealth < 1 || dto.targetHealth > 1000000 || dto.kitchenHealth < 1 || dto.kitchenHealth > 1000000
            || !FiniteNonnegative(dto.targetRepairSpeed) || dto.targetRepairSpeed > 1000000
            || !FiniteNonnegative(dto.targetRepairThreshold) || dto.targetRepairThreshold > 1000000)
            return "生命值须在 1 到 1000000，修复参数须在 0 到 1000000 且为有限数。";
        var total = 0;
        if (dto.gameMode == "Dynamic")
        {
            if (dto.hordeConfigs != null && dto.hordeConfigs.Length != 0) return "Dynamic 不能包含 Horde 配置。";
            if (dto.phases == null || dto.phases.Length < 2 || dto.phases.Length > MaxPhases)
                return "Dynamic 阶段数必须在 2 到 64 之间。";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var phase in dto.phases)
            {
                if (phase == null || phase.durationSeconds < 1 || phase.durationSeconds > MaxSeconds)
                    return "每个 Dynamic 阶段时长须在 1 到 86400 秒。";
                if (!ValidId(phase.id) || !ids.Add(phase.id)) return "Dynamic 阶段 ID 必须非空、唯一且不超过 128 字符。";
                var error = ValidateRecipes(phase.recipeGuids);
                if (error != null) return "Dynamic " + phase.id + "：" + error;
                total += phase.recipeGuids.Length;
            }
        }
        else if (dto.gameMode == "Horde")
        {
            if (dto.phases != null && dto.phases.Length != 0) return "Horde 不能包含 Dynamic 阶段。";
            if (dto.hordeConfigs == null || dto.hordeConfigs.Length != 4) return "Horde 必须提供 1P 到 4P 四份配置。";
            for (int p = 0; p < 4; p++)
            {
                var config = dto.hordeConfigs[p];
                if (config == null || config.targetRepairCostMax < 0 || config.targetRepairCostMax > 1000000
                    || config.waves == null || config.waves.Length == 0 || config.waves.Length > MaxWavesPerPlayer)
                    return (p + 1) + "P 修复费用须在 0 到 1000000，波次数须在 1 到 128。";
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var wave in config.waves)
                {
                    if (wave == null || wave.intervalSeconds < 0 || wave.intervalSeconds > MaxSeconds)
                        return "波次不能为空，间隔须在 0 到 86400 秒。";
                    if (!ValidId(wave.id) || !ids.Add(wave.id)) return "同一人数的波次 ID 必须非空、唯一且不超过 128 字符。";
                    var error = ValidateRecipes(wave.recipeGuids);
                    if (error != null) return (p + 1) + "P " + wave.id + "：" + error;
                    if (wave.enemyGuids == null || wave.enemyGuids.Length == 0 || wave.enemyGuids.Length > MaxEnemiesPerWave || wave.spawnTimes == null
                        || wave.spawnTimes.Length != wave.enemyGuids.Length) return "敌人和 spawnTimes 必须非空且长度相同。";
                    total += wave.recipeGuids.Length + wave.enemyGuids.Length;
                    if (total > MaxTotalEntries) return "模式配置的菜谱和敌人总条目数不能超过 32768。";
                    for (int i = 0; i < wave.enemyGuids.Length; i++)
                    {
                        if (ResolveEnemy(wave.enemyGuids[i]) == null) return "敌人仅允许 common04/game_environment/horde/enemies 下六种 SO。";
                        if (wave.spawnTimes[i] < 0 || wave.spawnTimes[i] > MaxSeconds)
                            return "spawnTimes 必须在 0 到 86400 秒；同时间及重复同种敌人均允许。";
                    }
                }
            }
        }
        else return "未知的关卡模式。";
        // Apply the same byte limit before cloning/serializing caller-supplied DTOs on save.
        if (Encoding.UTF8.GetByteCount(JsonUtility.ToJson(dto, true)) > MaxJsonBytes) return "模式 JSON 超过 2 MiB 上限。";
        return null;
    }

    private static bool ValidId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 128 || id.Trim().Length == 0) return false;
        foreach (var c in id) if (char.IsControl(c)) return false;
        return true;
    }

    private static string ValidateRecipes(string[] guids)
    {
        if (guids == null || guids.Length == 0 || guids.Length > MaxRecipesPerMenu) return "每份菜单须有 1 到 256 道菜谱。";
        foreach (var guid in guids) if (ResolveRecipe(guid) == null) return "包含无效或非菜谱 GUID。";
        return null;
    }

    private static bool FiniteNonnegative(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
    }

    private static ScriptableObject ResolveRecipe(string guid)
    {
        if (!ValidGuid(guid)) return null;
        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
        return asset is PseudoPrefabSORecipe || asset is CustomRecipeSO ? asset : null;
    }

    private static PseudoPrefabSO ResolveEnemy(string guid)
    {
        if (!ValidGuid(guid)) return null;
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return IsHordeEnemyPath(path) ? AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path) : null;
    }

    private static bool ValidGuid(string guid)
    {
        if (guid == null || guid.Length != 32) return false;
        foreach (var c in guid)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
        return true;
    }

    private static void AddRecipes(ScriptableObject[] recipes, List<ScriptableObject> result, HashSet<ScriptableObject> seen)
    {
        if (recipes == null) return;
        foreach (var recipe in recipes) if (recipe != null && seen.Add(recipe)) result.Add(recipe);
    }

    private static string[] AssetGuids(UnityEngine.Object[] assets)
    {
        if (assets == null) return new string[0];
        if (assets.Length > MaxRecipesPerMenu)
            throw new InvalidDataException("原生菜单或敌人数组超过 256 条上限。");
        var result = new string[assets.Length];
        for (int i = 0; i < assets.Length; i++)
            result[i] = assets[i] != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(assets[i])) : "";
        return result;
    }

    private static LevelConfigSetupPerPlayerCountSO[] PlayerConfigs(LevelInfoSO info)
    {
        return new[] { info.config_1p, info.config_2p, info.config_3p, info.config_4p };
    }

    private static string GetMode(LevelInfoSO info)
    {
        if (info.levelType == LevelInfoSO.LevelType.Dynamic) return "Dynamic";
        if (info.levelType == LevelInfoSO.LevelType.Horde) return "Horde";
        return "Campaign";
    }

    private static string GetStorePath(string levelInfoPath)
    {
        ValidatePhysicalPath(Path.GetDirectoryName(levelInfoPath).Replace('\\', '/') + "/" + StoreFolder + "/" + StoreFile);
        return Path.Combine(Path.Combine(Path.GetDirectoryName(AbsPath(levelInfoPath)), StoreFolder), StoreFile);
    }

    private static void SaveFile(string levelInfoPath, LevelModeConfigDto dto)
    {
        var path = GetStorePath(levelInfoPath);
        var text = JsonUtility.ToJson(dto, true);
        if (Encoding.UTF8.GetByteCount(text) > MaxJsonBytes) throw new InvalidDataException("模式 JSON 超过 2 MiB 上限。");
        if (File.Exists(path) && new FileInfo(path).Length <= MaxJsonBytes && File.ReadAllText(path) == text) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, text);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static string AbsPath(string assetPath)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
