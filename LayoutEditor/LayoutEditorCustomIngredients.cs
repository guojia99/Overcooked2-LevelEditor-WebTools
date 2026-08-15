using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Web 内置（Assets/Editor/LayoutEditor/Import 源库）食材/菜谱/道具的"按需拷贝打包"机制。
///
/// 源库位于 Assets/Editor 下，无法进入 AssetBundle 构建；关卡保存时把用到的
/// Import 资产拷入 Assets/LevelSets/&lt;set&gt;/custom_web/（分配新 guid、
/// 改写引用），并把 &lt;set&gt;/custom_web 与食材自身的游戏 bundle 写入
/// LevelInfoSO.dependencies —— 这样 Tools/Build AssetBundles 后资产随关卡打包，
/// 运行时 PseudoPrefabManager 从依赖 bundle 按 bundleName+assetPath 加载 prefab。
///
/// Web 拷贝始终归为「Web内置」分组（与自定义菜谱 custom_recipes / 自定义食材
/// custom_ingredients 分开维护），选中的 Web 菜谱不会变成自定义菜谱。
///
/// 宿主文件一律不动；本类只新增文件（拷贝）与改写本关卡集数据。
/// </summary>
public static class LayoutEditorCustomIngredients
{
    public const string ImportRoot = "Assets/Editor/LayoutEditor/Import";
    public const string CustomDirName = "custom_web";

    /// <summary>本关卡集 custom_web 内容在本请求中发生变化（新建副本/修复 pseudo 副本），
    ///  供 SyncLevelInfo 决定是否需要重扫 bundle 依赖。</summary>
    private static bool _depsDirty;

    public static string CustomIngredientsDir(string levelSet)
    {
        return "Assets/LevelSets/" + levelSet + "/" + CustomDirName;
    }

    public static bool IsImportAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;
        return assetPath.IndexOf("/Editor/LayoutEditor/Import/", StringComparison.Ordinal) >= 0;
    }

    /// <summary>保存布局（/api/scene/layout）时调用：收集文档中的食材/道具引用，
    ///  拷贝 Import 资产到关卡集并改写 doc 引用。返回 sourceGuid → copyGuid 映射。</summary>
    public static Dictionary<string, string> EnsureDocCopies(string levelSet, LayoutDocumentDto doc)
    {
        var map = new Dictionary<string, string>();
        if (doc == null || doc.items == null)
            return map;
        if (ImportVersion() == "v0.0.0")
            return map; // v0.0.0：不执行任何同步/引用改写
        // 已完整同步时跳过对副本的逐文件刷新（写回性能优化）
        var refreshStale = !IsWebSynced(levelSet);
        foreach (var it in doc.items)
        {
            if (it == null)
                continue;
            AddCopyWithRefresh(map, levelSet, it.prefabGuid, refreshStale);
            AddCopyWithRefresh(map, levelSet, it.pseudoPrefabGuid, refreshStale);
            if (it.dispenser != null)
                AddCopyWithRefresh(map, levelSet, it.dispenser.spawnerItemPrefabGuid, refreshStale);
            if (it.foodSpawner != null && it.foodSpawner.attachmentPrefabGuids != null)
                foreach (var g in it.foodSpawner.attachmentPrefabGuids)
                    AddCopyWithRefresh(map, levelSet, g, refreshStale);
            if (it.cookingUtensil != null && it.cookingUtensil.allowedIngredientGuids != null)
                foreach (var g in it.cookingUtensil.allowedIngredientGuids)
                    AddCopyWithRefresh(map, levelSet, g, refreshStale);
            if (it.cleanPlateStack != null)
                AddCopyWithRefresh(map, levelSet, it.cleanPlateStack.platePrefabGuid, refreshStale);
            if (it.meshWithMaterial != null)
                AddCopyWithRefresh(map, levelSet, it.meshWithMaterial.pseudoPrefabGuid, refreshStale);
            if (it.soArray != null && it.soArray.pseudoPrefabGuids != null)
                foreach (var g in it.soArray.pseudoPrefabGuids)
                    AddCopyWithRefresh(map, levelSet, g, refreshStale);
        }
        if (map.Count > 0)
        {
            AssetDatabase.Refresh();
            EnsureFolderBundleName(levelSet);
            RewriteDoc(doc, map);
            foreach (var kv in map)
                Debug.Log("[LayoutEditor] Web 拷贝: " + kv.Key.Substring(0, 8) + " -> " + kv.Value.Substring(0, 8)
                    + " (" + AssetDatabase.GUIDToAssetPath(kv.Value) + ")");
        }
        return map;
    }

    /// <summary>保存菜谱（SetLevelRecipes）后调用：改写 LevelInfoSO 中 recipes /
    ///  allIngredients 对 Import 资产的引用为关卡集副本，并注册依赖。</summary>
    public static int SyncLevelInfo(string levelSet, LevelInfoSO info)
    {
        if (info == null)
            return 0;
        if (ImportVersion() == "v0.0.0")
            return 0; // v0.0.0：不执行任何同步
        var map = new Dictionary<string, string>();
        var refreshStale = !IsWebSynced(levelSet);
        if (info.recipes != null)
        {
            foreach (var r in info.recipes)
            {
                if (r == null)
                    continue;
                AddCopyWithRefresh(map, levelSet, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(r)), refreshStale);
            }
        }
        if (info.allIngredients != null)
        {
            foreach (var ing in info.allIngredients)
            {
                if (ing == null)
                    continue;
                AddCopyWithRefresh(map, levelSet, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(ing)), refreshStale);
            }
        }
        // 发生真实同步或本请求修复了副本（新建 pseudo）时，注册全部副本引用的 bundle 依赖
        // （道具伪副本 + 食材副本）；已完整同步且无变化时跳过重扫（写回性能优化）。
        if (refreshStale || _depsDirty)
        {
            EnsureAllWebBundleDependencies(levelSet, info);
            _depsDirty = false;
        }
        if (map.Count == 0)
            return 0;
        AssetDatabase.Refresh();
        EnsureFolderBundleName(levelSet);
        RewriteLevelInfo(info, map);
        return map.Count;
    }

    // -------------------------------------------------------------
    // Copies
    // -------------------------------------------------------------

    private static void AddCopy(Dictionary<string, string> map, string levelSet, string sourceGuid)
    {
        AddCopyWithRefresh(map, levelSet, sourceGuid, true);
    }

    /// <summary>refreshStale=false（已完整同步）时跳过对已存在副本的内容比对/刷新，
    ///  避免每次写回都对副本逐文件读改（性能优化）。</summary>
    private static void AddCopyWithRefresh(Dictionary<string, string> map, string levelSet, string sourceGuid, bool refreshStale)
    {
        if (string.IsNullOrEmpty(sourceGuid) || map.ContainsKey(sourceGuid))
            return;
        var path = AssetDatabase.GUIDToAssetPath(sourceGuid);
        if (!IsImportAsset(path))
            return;
        var copyGuid = CopyIntoSet(levelSet, sourceGuid, path, refreshStale);
        if (copyGuid != null)
            map[sourceGuid] = copyGuid;
    }

    /// <summary>把单个 Import 资产拷入关卡集 custom_web（新 guid）。
    ///  已存在同 id 副本时直接复用其 guid。返回副本 guid。</summary>
    private static string CopyIntoSet(string levelSet, string sourceGuid, string assetPath, bool refreshStale)
    {
        var fileName = Path.GetFileName(assetPath);
        var id = Path.GetFileNameWithoutExtension(assetPath);
        var isPrefab = assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

        string kind;
        string sub;
        string catSub = ""; // 道具的分类子目录（prefabs/<cat>、pseudo/<cat>）
        if (assetPath.IndexOf("/Import/Ingredients/", StringComparison.Ordinal) >= 0)
        {
            kind = "ing";
            sub = "Ingredients";
        }
        else if (assetPath.IndexOf("/Import/Recipes/", StringComparison.Ordinal) >= 0)
        {
            kind = "recipe";
            sub = "Recipes";
        }
        else if (assetPath.IndexOf("/Import/CookingSteps/", StringComparison.Ordinal) >= 0)
        {
            kind = "step";
            sub = "CookingSteps";
        }
        else if (isPrefab)
        {
            kind = "prop";
            sub = "prefabs";
            catSub = CategoryOfImportPath(assetPath);
        }
        else
        {
            kind = "pseudo";
            sub = "pseudo";
            catSub = CategoryOfImportPath(assetPath);
        }

        var targetDir = CustomIngredientsDir(levelSet) + "/" + sub + (catSub.Length > 0 ? "/" + catSub : "");
        var targetPath = targetDir + "/" + fileName;
        var existingGuid = LayoutEditorLevelAdminApi.ReadAssetGuid(AbsPath(targetPath + ".meta"));
        if (existingGuid != null)
        {
            // 已存在拷贝：guid 复用，但若 bundleName/assetPath 与源库不一致
            // （源库修正过，如食材 prefab 别名/bundle 归属修正）→ 刷新内容，保证
            // 运行时 LoadAsset 命中真实 prefab。已完整同步时跳过逐文件比对（性能）。
            if (refreshStale)
            {
                RefreshStaleCopy(assetPath, targetPath);
                if (isPrefab)
                    EnsurePrefabPseudo(levelSet, assetPath, targetPath, catSub, id);
            }
            // 防御「meta 在磁盘、AssetDatabase 却未注册该 guid」的脱同步：
            // 强制重新导入并验证 guid 可解析（保存菜谱时 GUIDToAssetPath 依赖此映射）。
            if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(existingGuid)))
            {
                LayoutEditorLog.LogWarning("[Web拷贝] 副本 guid 未在 AssetDatabase 注册，强制重新导入: "
                    + targetPath + " (" + existingGuid + ")");
                AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
            }
            if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(existingGuid)))
                LayoutEditorLog.LogWarning("[Web拷贝] 副本 guid 重新导入后仍无法解析: " + targetPath + " (" + existingGuid + ")");
            else
                LayoutEditorLog.Log("[Web拷贝] 复用副本: " + id + " -> " + existingGuid);
            return existingGuid;
        }

        Directory.CreateDirectory(AbsPath(targetDir));
        File.Copy(AbsPath(assetPath), AbsPath(targetPath), true);

        // 道具 prefab：内部 pseudoPrefabSO 引用必须指向 custom_web 伪副本（不引用 Import 源库）
        if (isPrefab)
            EnsurePrefabPseudo(levelSet, assetPath, targetPath, catSub, id);

        var copyGuid = StableGuid(levelSet, kind, id);
        File.WriteAllText(AbsPath(targetPath + ".meta"), MetaYaml(copyGuid), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
        // 导入后立即验证 guid 已注册：若 Unity 未能导入（如同名冲突/导入错误），
        // 后续保存菜谱时 GUIDToAssetPath 会静默失败，必须在此暴露。
        if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(copyGuid)))
        {
            LayoutEditorLog.LogWarning("[Web拷贝] 拷贝导入失败，guid 未注册: " + targetPath + " (" + copyGuid + ")");
            return null;
        }
        LayoutEditorLog.Log("[Web拷贝] 新建副本: " + assetPath + " -> " + targetPath + " (" + copyGuid + ")");
        return copyGuid;
    }

    /// <summary>确保预制件副本的 pseudoPrefabSO 指向关卡集 custom_web 伪副本
    ///  （新/旧副本统一修复，避免直接引用 Import 源库或悬空 guid）。
    ///  源伪资产缺失时不改写引用（源库资产仍在工程中可解析），仅记录警告。</summary>
    private static void EnsurePrefabPseudo(string levelSet, string assetPath, string targetPath, string catSub, string id)
    {
        var text = File.ReadAllText(AbsPath(targetPath));
        var m = System.Text.RegularExpressions.Regex.Match(
            text, @"pseudoPrefabSO:\s*\{fileID: 11400000, guid: ([a-f0-9]{32})");
        if (!m.Success)
            return;

        var sourcePseudoPath = assetPath.Replace("/Import/prefabs/", "/Import/pseudo_prefab_so/");
        if (!File.Exists(AbsPath(sourcePseudoPath)))
        {
            LayoutEditorLog.LogWarning("[Web拷贝] 道具 " + id + " 无源 pseudo 资产可拷，保持原引用: " + sourcePseudoPath);
            return;
        }

        var pseudoDir = CustomIngredientsDir(levelSet) + "/pseudo" + (catSub.Length > 0 ? "/" + catSub : "");
        Directory.CreateDirectory(AbsPath(pseudoDir));
        var pseudoTarget = pseudoDir + "/" + Path.GetFileName(sourcePseudoPath);
        var existingPseudoGuid = LayoutEditorLevelAdminApi.ReadAssetGuid(AbsPath(pseudoTarget + ".meta"));
        string pseudoGuid;
        if (existingPseudoGuid == null)
        {
            pseudoGuid = StableGuid(levelSet, "pseudo", id);
            File.Copy(AbsPath(sourcePseudoPath), AbsPath(pseudoTarget), true);
            File.WriteAllText(AbsPath(pseudoTarget + ".meta"), MetaYaml(pseudoGuid), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(pseudoTarget, ImportAssetOptions.ForceSynchronousImport);
        }
        else
        {
            pseudoGuid = existingPseudoGuid;
        }

        if (m.Groups[1].Value != pseudoGuid)
        {
            text = text.Replace(m.Groups[1].Value, pseudoGuid);
            File.WriteAllText(AbsPath(targetPath), text, new UTF8Encoding(false));
            LayoutEditorLog.Log("[Web拷贝] 改写副本 pseudo 引用: " + id + " -> " + pseudoGuid);
        }
    }

    /// <summary>已存在拷贝的过期刷新：仅比对 bundleName/assetPath 关键字段，
    ///  不一致（源库修正过）则用源库内容覆盖（guid 不变，引用无损）。
    ///  道具 prefab 拷贝因含改写后的 pseudo 引用，不比对这些字段，永不刷新。</summary>
    private static void RefreshStaleCopy(string sourcePath, string targetPath)
    {
        try
        {
            var src = File.ReadAllText(AbsPath(sourcePath));
            var tgt = File.ReadAllText(AbsPath(targetPath));
            if (src == tgt)
                return;
            var mSB = System.Text.RegularExpressions.Regex.Match(src, @"bundleName:\s*(\S+)");
            var mTB = System.Text.RegularExpressions.Regex.Match(tgt, @"bundleName:\s*(\S+)");
            var mSA = System.Text.RegularExpressions.Regex.Match(src, @"assetPath:\s*(\S+)");
            var mTA = System.Text.RegularExpressions.Regex.Match(tgt, @"assetPath:\s*(\S+)");
            if (mSB.Success && mTB.Success && mSA.Success && mTA.Success &&
                mSB.Groups[1].Value == mTB.Groups[1].Value &&
                mSA.Groups[1].Value == mTA.Groups[1].Value)
                return;
            File.Copy(AbsPath(sourcePath), AbsPath(targetPath), true);
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[LayoutEditor] 刷新过期 Web 拷贝: " + targetPath);
        }
        catch (Exception)
        {
            // 刷新失败不影响主流程
        }
    }

    /// <summary>确保 custom_web 文件夹打包名 = &lt;set&gt;/custom_web
    ///  （与 custom_recipes 同机制，Tools/Build AssetBundles 会打进 bundle）。</summary>
    private static void EnsureFolderBundleName(string levelSet)
    {
        var dir = CustomIngredientsDir(levelSet);
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(dir))
            return;
        var imp = AssetImporter.GetAtPath(dir);
        if (imp == null)
            return;
        var bundleName = levelSet + "/" + CustomDirName;
        if (imp.assetBundleName != bundleName)
        {
            imp.SetAssetBundleNameAndVariant(bundleName, "");
            imp.SaveAndReimport();
        }
    }

    /// <summary>无条件把 Import 源库全部 Web 内容（菜谱/食材/道具）同步到关卡集 custom_web。
    ///  Import 只作模板，最终引用一律指向关卡集副本（guid 稳定复用，重复调用幂等）。
    ///  在创建关卡与写回场景时调用。源库未变化且已同步时直接跳过（避免每次写回都全量扫描）。</summary>
    public static int SyncAllWebContent(string levelSet)
    {
        if (string.IsNullOrEmpty(levelSet))
            return 0;
        var importVersion = ImportVersion();
        if (importVersion == "v0.0.0")
            return 0; // v0.0.0：不执行任何同步动作
        var synced = IsWebSynced(levelSet, importVersion);
        int created = 0;
        if (!synced)
        {
            var alreadySynced = LayoutEditorLevelAdminApi.AssetFolderExists(CustomIngredientsDir(levelSet) + "/Recipes");
            var map = new Dictionary<string, string>();
            SyncAssetFolder(map, levelSet, ImportRoot + "/Recipes");
            SyncAssetFolder(map, levelSet, ImportRoot + "/Ingredients");
            SyncPrefabFolder(map, levelSet, ImportRoot + "/prefabs");
            if (map.Count == 0)
                return 0;
            if (!alreadySynced)
            {
                AssetDatabase.Refresh();
                EnsureFolderBundleName(levelSet);
            }
            else
            {
                EnsureFolderBundleName(levelSet);
            }
            WriteWebSyncStamp(levelSet, importVersion);
            created = map.Count;
        }
        // 无论是否已同步，都修复既有 prefab 副本：pseudoPrefabSO 一律指向 custom_web 伪副本
        // （早期副本可能仍引用 Import 源库伪资产；幂等，仅读改写指向 Import 的副本）
        var repaired = RepairAllPrefabCopies(levelSet);
        if (created > 0 || repaired > 0)
        {
            _depsDirty = true;
            AssetDatabase.Refresh();
            EnsureFolderBundleName(levelSet);
        }
        return created + repaired;
    }

    /// <summary>遍历 custom_web 全部 prefab 副本，确保 pseudoPrefabSO 指向 custom_web 伪副本
    ///  （不引用 Import 源库）。返回实际改写/新建 pseudo 副本的副本数。</summary>
    public static int RepairAllPrefabCopies(string levelSet)
    {
        var prefabsRoot = CustomIngredientsDir(levelSet) + "/prefabs";
        var absRoot = AbsPath(prefabsRoot);
        if (!Directory.Exists(absRoot))
            return 0;
        var importIndex = BuildImportPrefabIndex();
        var changed = 0;
        foreach (var file in Directory.GetFiles(absRoot, "*.prefab", SearchOption.AllDirectories))
        {
            var rel = file.Substring(absRoot.Length).TrimStart('/').Replace('\\', '/');
            var copyPath = prefabsRoot + "/" + rel;
            var id = Path.GetFileNameWithoutExtension(copyPath);
            var catSub = (Path.GetDirectoryName(rel) ?? "").Replace('\\', '/');
            var sourcePath = ImportRoot + "/prefabs/" + (catSub.Length > 0 ? catSub + "/" : "") + Path.GetFileName(copyPath);
            if (!File.Exists(AbsPath(sourcePath)))
            {
                // 副本分类与源库不一致（如部分 art 装饰直接落在 art/ 下）：按 id 反查源库
                if (!importIndex.TryGetValue(id, out sourcePath))
                    continue;
            }
            var before = File.ReadAllText(AbsPath(copyPath));
            EnsurePrefabPseudo(levelSet, sourcePath, copyPath, catSub, id);
            var after = File.ReadAllText(AbsPath(copyPath));
            if (after != before)
                changed++;
        }
        return changed;
    }

    /// <summary>Import/prefabs 下全部 prefab 的 id → 源路径索引（供副本反查）。</summary>
    private static Dictionary<string, string> BuildImportPrefabIndex()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var abs = AbsPath(ImportRoot + "/prefabs");
        if (!Directory.Exists(abs))
            return map;
        foreach (var f in Directory.GetFiles(abs, "*.prefab", SearchOption.AllDirectories))
        {
            var id = Path.GetFileNameWithoutExtension(f);
            if (map.ContainsKey(id))
                continue;
            var rel = f.Substring(abs.Length).TrimStart('/').Replace('\\', '/');
            map[id] = ImportRoot + "/prefabs/" + rel;
        }
        return map;
    }

    private const string WebSyncStampName = ".web-sync-stamp";

    private static string SyncStampPath(string levelSet)
    {
        return "Assets/LevelSets/" + levelSet + "/" + WebSyncStampName;
    }

    /// <summary>Import 源库内容签名：相对路径 + 最后修改时间（跳过 .meta），
    ///  用于判断源库是否变化。变化后下一次写回会触发全量同步。</summary>
    private static string ImportSignature()
    {
        using (var md5 = MD5.Create())
        {
            var sb = new StringBuilder();
            foreach (var dir in new[] { ImportRoot + "/Recipes", ImportRoot + "/Ingredients",
                                        ImportRoot + "/prefabs", ImportRoot + "/pseudo_prefab_so" })
            {
                var abs = AbsPath(dir);
                if (!Directory.Exists(abs))
                    continue;
                foreach (var f in Directory.GetFiles(abs, "*", SearchOption.AllDirectories))
                {
                    if (f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var rel = f.Substring(abs.Length).TrimStart('/').Replace('\\', '/');
                    sb.Append(rel).Append('|').Append(File.GetLastWriteTimeUtc(f).Ticks).Append('\n');
                }
            }
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            var h = new StringBuilder(32);
            for (int i = 0; i < 16; i++)
                h.Append(hash[i].ToString("x2"));
            return h.ToString();
        }
    }

    /// <summary>Import 源库版本号（Assets/Editor/LayoutEditor/Import/version.txt）。
    ///  缺失时返回 "v0.0.0"（表示不执行任何同步、页面 web 内置禁用）。</summary>
    public static string ImportVersion()
    {
        var p = AbsPath(ImportRoot + "/version.txt");
        if (!File.Exists(p))
            return "v0.0.0";
        try
        {
            return File.ReadAllText(p).Trim();
        }
        catch
        {
            return "v0.0.0";
        }
    }

    /// <summary>关卡集已完整同步、源库未变化且版本一致 → 跳过全量同步。</summary>
    private static bool IsWebSynced(string levelSet)
    {
        return IsWebSynced(levelSet, ImportVersion());
    }

    private static bool IsWebSynced(string levelSet, string importVersion)
    {
        if (string.IsNullOrEmpty(importVersion) || importVersion == "v0.0.0")
            return false;
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(CustomIngredientsDir(levelSet) + "/Recipes"))
            return false;
        var stampPath = AbsPath(SyncStampPath(levelSet));
        if (!File.Exists(stampPath))
            return false;
        try
        {
            var lines = File.ReadAllLines(stampPath);
            if (lines.Length < 2)
                return false;
            if (lines[0].Trim() != importVersion)
                return false;
            return lines[1].Trim() == ImportSignature();
        }
        catch
        {
            return false;
        }
    }

    private static void WriteWebSyncStamp(string levelSet)
    {
        WriteWebSyncStamp(levelSet, ImportVersion());
    }

    private static void WriteWebSyncStamp(string levelSet, string importVersion)
    {
        try
        {
            var text = (importVersion ?? "") + "\n" + ImportSignature();
            File.WriteAllText(AbsPath(SyncStampPath(levelSet)), text, new UTF8Encoding(false));
        }
        catch { }
    }

    private static void SyncAssetFolder(Dictionary<string, string> map, string levelSet, string folder)
    {
        var absFolder = AbsPath(folder);
        if (!Directory.Exists(absFolder))
            return;
        foreach (var file in Directory.GetFiles(absFolder, "*.asset", SearchOption.AllDirectories))
        {
            var rel = file.Substring(absFolder.Length).TrimStart('/').Replace('\\', '/');
            var assetPath = folder + "/" + rel;
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                guid = LayoutEditorLevelAdminApi.ReadAssetGuid(AbsPath(assetPath) + ".meta");
            if (!string.IsNullOrEmpty(guid))
                AddCopy(map, levelSet, guid);
        }
    }

    private static void SyncPrefabFolder(Dictionary<string, string> map, string levelSet, string folder)
    {
        var absFolder = AbsPath(folder);
        if (!Directory.Exists(absFolder))
            return;
        foreach (var file in Directory.GetFiles(absFolder, "*.prefab", SearchOption.AllDirectories))
        {
            var rel = file.Substring(absFolder.Length).TrimStart('/').Replace('\\', '/');
            var assetPath = folder + "/" + rel;
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                guid = LayoutEditorLevelAdminApi.ReadAssetGuid(AbsPath(assetPath) + ".meta");
            if (!string.IsNullOrEmpty(guid))
                AddCopy(map, levelSet, guid);
        }
    }

    // -------------------------------------------------------------
    // Reference rewriting
    // -------------------------------------------------------------

    private static void RewriteDoc(LayoutDocumentDto doc, Dictionary<string, string> map)
    {
        if (doc.items == null)
            return;
        foreach (var it in doc.items)
        {
            if (it == null)
                continue;
            it.prefabGuid = RewriteGuid(it.prefabGuid, map);
            it.pseudoPrefabGuid = RewriteGuid(it.pseudoPrefabGuid, map);
            if (it.dispenser != null)
                it.dispenser.spawnerItemPrefabGuid = RewriteGuid(it.dispenser.spawnerItemPrefabGuid, map);
            if (it.foodSpawner != null && it.foodSpawner.attachmentPrefabGuids != null)
                for (int i = 0; i < it.foodSpawner.attachmentPrefabGuids.Length; i++)
                    it.foodSpawner.attachmentPrefabGuids[i] = RewriteGuid(it.foodSpawner.attachmentPrefabGuids[i], map);
            if (it.cookingUtensil != null && it.cookingUtensil.allowedIngredientGuids != null)
                for (int i = 0; i < it.cookingUtensil.allowedIngredientGuids.Length; i++)
                    it.cookingUtensil.allowedIngredientGuids[i] = RewriteGuid(it.cookingUtensil.allowedIngredientGuids[i], map);
            if (it.cleanPlateStack != null)
                it.cleanPlateStack.platePrefabGuid = RewriteGuid(it.cleanPlateStack.platePrefabGuid, map);
            if (it.meshWithMaterial != null)
                it.meshWithMaterial.pseudoPrefabGuid = RewriteGuid(it.meshWithMaterial.pseudoPrefabGuid, map);
            if (it.soArray != null && it.soArray.pseudoPrefabGuids != null)
                for (int i = 0; i < it.soArray.pseudoPrefabGuids.Length; i++)
                    it.soArray.pseudoPrefabGuids[i] = RewriteGuid(it.soArray.pseudoPrefabGuids[i], map);
        }
    }

    private static string RewriteGuid(string guid, Dictionary<string, string> map)
    {
        if (string.IsNullOrEmpty(guid))
            return guid;
        string copyGuid;
        return map.TryGetValue(guid, out copyGuid) ? copyGuid : guid;
    }

    private static void RewriteLevelInfo(LevelInfoSO info, Dictionary<string, string> map)
    {
        if (info.allIngredients != null)
        {
            for (int i = 0; i < info.allIngredients.Length; i++)
            {
                var so = info.allIngredients[i];
                if (so == null)
                    continue;
                var copyGuid = RewriteGuid(
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(so)), map);
                var copyPath = AssetDatabase.GUIDToAssetPath(copyGuid);
                if (string.IsNullOrEmpty(copyPath) || copyPath == AssetDatabase.GetAssetPath(so))
                    continue;
                var copy = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(copyPath);
                if (copy != null)
                    info.allIngredients[i] = copy;
            }
        }
        if (info.recipes != null)
        {
            for (int i = 0; i < info.recipes.Length; i++)
            {
                var r = info.recipes[i];
                if (r == null)
                    continue;
                var copyGuid = RewriteGuid(
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(r)), map);
                var copyPath = AssetDatabase.GUIDToAssetPath(copyGuid);
                if (string.IsNullOrEmpty(copyPath) || copyPath == AssetDatabase.GetAssetPath(r))
                    continue;
                var copy = AssetDatabase.LoadAssetAtPath<ScriptableObject>(copyPath);
                if (copy != null)
                    info.recipes[i] = copy;
            }
        }
        EditorUtility.SetDirty(info);
    }

    // -------------------------------------------------------------
    // Dependencies
    // -------------------------------------------------------------

    /// <summary>把 custom_web 下全部副本引用的游戏 bundle 加入 LevelInfoSO.dependencies
    ///  （custom_web 包 + 道具伪副本 pseudo 的 bundleName + 食材副本的 bundleName），
    ///  确保运行时这些 bundle 被加载，补充未加载的 bundle。
    ///  仅当 bundle 已存在于 StreamingAssets 才写入，缺失跳过（避免宿主
    ///  PseudoPrefabManager.GetAssetBundle 抛 KeyNotFoundException）。</summary>
    public static void EnsureAllWebBundleDependencies(string levelSet, LevelInfoSO info)
    {
        if (info == null)
            return;
        var deps = new List<string>(info.dependencies ?? new string[0]);
        AddDependency(deps, levelSet + "/" + CustomDirName);
        var root = CustomIngredientsDir(levelSet);
        if (LayoutEditorLevelAdminApi.AssetFolderExists(root))
        {
            CollectPseudoBundleDeps(deps, root + "/pseudo");
            CollectPseudoBundleDeps(deps, root + "/Ingredients");
        }
        info.dependencies = deps.ToArray();
        EditorUtility.SetDirty(info);
    }

    private static void CollectPseudoBundleDeps(List<string> deps, string folder)
    {
        var absFolder = AbsPath(folder);
        if (!Directory.Exists(absFolder))
            return;
        foreach (var file in Directory.GetFiles(absFolder, "*.asset", SearchOption.AllDirectories))
        {
            var rel = file.Substring(absFolder.Length).TrimStart('/').Replace('\\', '/');
            var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(folder + "/" + rel);
            if (so != null && !string.IsNullOrEmpty(so.bundleName))
                AddDependency(deps, so.bundleName);
        }
    }

    private static void AddDependency(List<string> deps, string bundleName)
    {
        if (string.IsNullOrEmpty(bundleName))
            return;
        if (deps.Contains(bundleName))
            return;
        if (!LayoutEditorCatalogApi.BundleFileExists(bundleName))
            return;
        deps.Add(bundleName);
    }

    // -------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------

    /// <summary>关卡集内确定性 guid（md5(levelSet/kind/id)），重复生成保持一致。</summary>
    private static string StableGuid(string levelSet, string kind, string id)
    {
        using (var md5 = MD5.Create())
        {
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(levelSet + "/" + kind + "/" + id));
            var sb = new StringBuilder(32);
            for (int i = 0; i < 16; i++)
                sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }

    /// <summary>道具在源库中的分类子目录（prefabs/&lt;cat&gt;、pseudo_prefab_so/&lt;cat&gt;），
    ///  拷贝到关卡集时保留，保证调色板分类一致。</summary>
    private static string CategoryOfImportPath(string assetPath)
    {
        var idx = assetPath.IndexOf("/Import/prefabs/", StringComparison.Ordinal);
        var prefixLen = "/Import/prefabs/".Length;
        if (idx < 0)
        {
            idx = assetPath.IndexOf("/Import/pseudo_prefab_so/", StringComparison.Ordinal);
            prefixLen = "/Import/pseudo_prefab_so/".Length;
        }
        if (idx < 0)
            return "";
        var rest = assetPath.Substring(idx + prefixLen);
        var slash = rest.IndexOf('/');
        return slash < 0 ? "" : rest.Substring(0, slash);
    }

    private static string MetaYaml(string guid)
    {
        return "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n" +
            "  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n" +
            "  assetBundleName: \n  assetBundleVariant: \n";
    }

    private static string AbsPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "../" + assetPath)).Replace('\\', '/');
    }

    // -------------------------------------------------------------
    // Web 内置菜谱：显式安装 / 移除 / 依赖 / 修复
    // -------------------------------------------------------------

    /// <summary>把指定 Web 内置菜谱（Import 源库）显式拷入关卡集 custom_web，
    ///  连同其全部叶食材，改写分数为估算分，并迁移关卡集内 LevelInfo 的源引用。
    ///  返回 null=成功，否则为错误信息。</summary>
    public static string InstallRecipes(string levelSet, string[] ids)
    {
        if (string.IsNullOrEmpty(levelSet) || ids == null || ids.Length == 0)
            return "缺少参数。";
        var map = new Dictionary<string, string>();
        var failed = new List<string>();
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id))
                continue;
            var sourcePath = FindImportRecipePath(id);
            if (string.IsNullOrEmpty(sourcePath))
            {
                failed.Add(id);
                continue;
            }
            var sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            var copyGuid = CopyIntoSet(levelSet, sourceGuid, sourcePath, true);
            if (copyGuid == null)
            {
                failed.Add(id);
                continue;
            }
            map[sourceGuid] = copyGuid;
            // 叶食材一并拷入（含已存在副本复用）
            string step;
            string[] ings;
            if (LayoutEditorRecipeKnowledge.TryGetOriginal(id, out step, out ings))
            {
                foreach (var ing in ings)
                {
                    if (string.IsNullOrEmpty(ing))
                        continue;
                    var ingGuid = FindImportAssetGuid(ing, "Ingredients");
                    if (!string.IsNullOrEmpty(ingGuid))
                        AddCopy(map, levelSet, ingGuid);
                }
                RewriteCopyScore(levelSet, id, LayoutEditorRecipeKnowledge.EstimateWebRecipeScore(id, step, ings));
            }
        }
        if (failed.Count > 0)
            return "以下菜谱在 Web 内置源库中不存在：" + string.Join("、", failed.ToArray());
        if (map.Count == 0)
            return null;
        AssetDatabase.Refresh();
        EnsureFolderBundleName(levelSet);
        MigrateLevelInfoReferences(levelSet, map);
        return null;
    }

    /// <summary>移除已安装的 Web 内置菜谱副本。若被本关卡集任一关卡引用则拒绝，
    ///  在 usedByLevels 中返回使用关卡列表。</summary>
    public static WebRecipeUninstallResultDto UninstallRecipes(string levelSet, string[] ids)
    {
        var result = new WebRecipeUninstallResultDto { ok = false };
        if (string.IsNullOrEmpty(levelSet) || ids == null || ids.Length == 0)
        {
            result.error = "缺少参数。";
            return result;
        }
        var usedLevels = ReferencedByLevels(levelSet, ids);
        if (usedLevels.Count > 0)
        {
            result.error = "以下菜谱正被关卡使用，请先在关卡中取消勾选：";
            result.usedByLevels = usedLevels.ToArray();
            return result;
        }
        var failed = new List<string>();
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id))
                continue;
            var copyPath = CustomIngredientsDir(levelSet) + "/Recipes/" + id + ".asset";
            if (!File.Exists(AbsPath(copyPath)))
            {
                failed.Add(id);
                continue;
            }
            if (!AssetDatabase.DeleteAsset(copyPath))
                failed.Add(id);
        }
        if (failed.Count > 0)
        {
            result.error = "以下菜谱副本删除失败（可能不存在）：" + string.Join("、", failed.ToArray());
            return result;
        }
        AssetDatabase.Refresh();
        result.ok = true;
        return result;
    }

    /// <summary>注册已选 Web 内置菜谱所需的 bundle 依赖（custom_web 包 + 食材自身游戏 bundle），
    ///  供 SetLevelRecipes 在保存时调用（已装副本随关卡打包）。</summary>
    public static void EnsureWebDependencies(string levelSet, LevelInfoSO info)
    {
        if (info == null)
            return;
        var deps = new List<string>(info.dependencies ?? new string[0]);
        AddDependency(deps, levelSet + "/" + CustomDirName);
        if (info.recipes != null)
        {
            foreach (var r in info.recipes)
            {
                if (r == null)
                    continue;
                var rp = AssetDatabase.GetAssetPath(r);
                if (string.IsNullOrEmpty(rp) || rp.IndexOf(CustomDirName, StringComparison.Ordinal) < 0)
                    continue;
                var id = Path.GetFileNameWithoutExtension(rp);
                string step;
                string[] ings;
                if (LayoutEditorRecipeKnowledge.TryGetOriginal(id, out step, out ings))
                {
                    foreach (var ing in ings)
                        AddIngredientBundleDep(deps, levelSet, ing);
                }
            }
        }
        if (info.allIngredients != null)
        {
            foreach (var ing in info.allIngredients)
            {
                if (ing == null)
                    continue;
                var ip = AssetDatabase.GetAssetPath(ing);
                if (string.IsNullOrEmpty(ip) || ip.IndexOf(CustomDirName, StringComparison.Ordinal) < 0)
                    continue;
                if (!string.IsNullOrEmpty(ing.bundleName))
                    AddDependency(deps, ing.bundleName);
            }
        }
        // 道具伪副本引用的游戏 bundle（锅具/杯具等），确保运行时加载
        var pseudoRoot = CustomIngredientsDir(levelSet) + "/pseudo";
        if (LayoutEditorLevelAdminApi.AssetFolderExists(pseudoRoot))
            CollectPseudoBundleDeps(deps, pseudoRoot);
        info.dependencies = deps.ToArray();
        EditorUtility.SetDirty(info);
    }

    /// <summary>修复关卡集 custom_web：副本 meta guid 与 StableGuid 不一致则重写
    ///  （修复历史坏副本），并把 Web 菜谱副本分数归一为估算分。</summary>
    public static void RepairLevelSet(string levelSet)
    {
        var dir = CustomIngredientsDir(levelSet);
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(dir))
            return;
        AssetDatabase.Refresh();
        EnsureFolderBundleName(levelSet);
        var folders = new[] { dir + "/Recipes", dir + "/Ingredients" };
        foreach (var folder in folders)
        {
            if (!LayoutEditorLevelAdminApi.AssetFolderExists(folder))
                continue;
            foreach (var file in Directory.GetFiles(AbsPath(folder), "*.asset", SearchOption.AllDirectories))
            {
                var rel = file.Substring(AbsPath(folder).Length).Replace('\\', '/').TrimStart('/');
                var assetPath = (folder + "/" + rel).Replace("//", "/");
                var id = Path.GetFileNameWithoutExtension(assetPath);
                var kind = folder.EndsWith("/Ingredients", StringComparison.Ordinal) ? "ing" : "recipe";
                var stable = StableGuid(levelSet, kind, id);
                var metaPath = file + ".meta";
                var existing = LayoutEditorLevelAdminApi.ReadAssetGuid(metaPath);
                if (existing != stable)
                {
                    File.WriteAllText(metaPath, MetaYaml(stable), new UTF8Encoding(false));
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    Debug.Log("[LayoutEditor] 修复 Web 副本 guid: " + assetPath);
                }
            }
        }
        var recipesDir = dir + "/Recipes";
        if (LayoutEditorLevelAdminApi.AssetFolderExists(recipesDir))
        {
            foreach (var file in Directory.GetFiles(AbsPath(recipesDir), "*.asset", SearchOption.AllDirectories))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                string step;
                string[] ings;
                if (LayoutEditorRecipeKnowledge.TryGetOriginal(id, out step, out ings))
                    RewriteCopyScoreFile(file, LayoutEditorRecipeKnowledge.EstimateWebRecipeScore(id, step, ings));
            }
        }
    }

    // -------------------------------------------------------------
    // Web 内置菜谱辅助
    // -------------------------------------------------------------

    private static string FindImportRecipePath(string id)
    {
        var root = ImportRoot + "/Recipes";
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(root))
            return null;
        foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(root, LayoutEditorLevelAdminApi.OriginalRecipeScriptGuid))
        {
            if (string.Equals(Path.GetFileNameWithoutExtension(asset.assetPath), id, StringComparison.Ordinal))
                return asset.assetPath;
        }
        foreach (var g in LayoutEditorLevelAdminApi.CustomRecipeScriptGuids)
        {
            foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(root, g))
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(asset.assetPath), id, StringComparison.Ordinal))
                    return asset.assetPath;
            }
        }
        return null;
    }

    private static string FindImportAssetGuid(string id, string sub)
    {
        var root = ImportRoot + "/" + sub;
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(root))
            return null;
        foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(root, LayoutEditorLevelAdminApi.PseudoPrefabScriptGuid))
        {
            if (string.Equals(Path.GetFileNameWithoutExtension(asset.assetPath), id, StringComparison.Ordinal))
                return asset.guid;
        }
        return null;
    }

    /// <summary>遍历关卡集 data 目录下全部 LevelInfoSO，找出引用指定菜谱 id 的关卡名。</summary>
    private static List<string> ReferencedByLevels(string levelSet, string[] ids)
    {
        var usingLevels = new List<string>();
        var dataDir = "Assets/LevelSets/" + levelSet + "/data";
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(dataDir))
            return usingLevels;
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
                if (Array.IndexOf(ids, rid) >= 0)
                {
                    usingLevels.Add(info.levelName ?? Path.GetFileName(Path.GetDirectoryName(path)));
                    break;
                }
            }
        }
        return usingLevels;
    }

    /// <summary>把关卡集内 LevelInfo 对 Import 源 guid 的引用改写为 custom_web 副本 guid
    ///  （修复历史「保存时自动拷贝」遗留的源引用）。</summary>
    private static void MigrateLevelInfoReferences(string levelSet, Dictionary<string, string> map)
    {
        var dataDir = "Assets/LevelSets/" + levelSet + "/data";
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(dataDir))
            return;
        foreach (var guid in AssetDatabase.FindAssets("t:LevelInfoSO", new[] { dataDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(path);
            if (info == null || info.recipes == null)
                continue;
            var changed = false;
            for (int i = 0; i < info.recipes.Length; i++)
            {
                var r = info.recipes[i];
                if (r == null)
                    continue;
                var rg = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(r));
                string copyGuid;
                if (!string.IsNullOrEmpty(rg) && map.TryGetValue(rg, out copyGuid))
                {
                    var copyPath = AssetDatabase.GUIDToAssetPath(copyGuid);
                    if (!string.IsNullOrEmpty(copyPath))
                    {
                        info.recipes[i] = AssetDatabase.LoadAssetAtPath<ScriptableObject>(copyPath);
                        changed = true;
                    }
                }
            }
            if (changed)
                EditorUtility.SetDirty(info);
        }
    }

    private static void RewriteCopyScore(string levelSet, string id, int score)
    {
        var path = CustomIngredientsDir(levelSet) + "/Recipes/" + id + ".asset";
        if (!File.Exists(AbsPath(path)))
            return;
        RewriteCopyScoreFile(AbsPath(path), score);
    }

    private static void RewriteCopyScoreFile(string absFile, int score)
    {
        try
        {
            var text = File.ReadAllText(absFile);
            var m = System.Text.RegularExpressions.Regex.Match(text, @"^  score: \d+$",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            if (!m.Success || m.Value == "  score: " + score)
                return;
            text = text.Substring(0, m.Index) + "  score: " + score + text.Substring(m.Index + m.Length);
            File.WriteAllText(absFile, text, new UTF8Encoding(false));
            var rel = absFile.Substring(AbsPath("").Length).TrimStart('/').Replace('\\', '/');
            AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceSynchronousImport);
        }
        catch { }
    }

    private static void AddIngredientBundleDep(List<string> deps, string levelSet, string ingId)
    {
        var copyPath = CustomIngredientsDir(levelSet) + "/Ingredients/" + ingId + ".asset";
        var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(copyPath);
        if (so != null && !string.IsNullOrEmpty(so.bundleName))
            AddDependency(deps, so.bundleName);
    }
}
