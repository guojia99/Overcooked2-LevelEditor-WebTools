using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 分 P（多阶段布局）配置的持久化与往返盖章。
/// 规格见 layout-editor/docs/10-分P关卡功能规格.md（附录 A 决策 D9/D10）。
///
/// 真源 = Assets/LevelSets/&lt;set&gt;/data/&lt;levelId&gt;/part_level~/part_level.json
/// （`~` 结尾目录 Unity 完全忽略 → 不进 AssetBundle、不触发导入，随 git 管理；
/// 同 assignment~/readme~ 范式）。场景内没有任何分 P 载体：
/// - GET /api/scene/layout：导出后按 hierarchyPath 合并 partLevel + 物件/地板 partId；
/// - POST /api/scene/layout：写回成功后按写回结果重导出场景、重盖章落盘
///   （"new:" id 经 SceneLayoutApplier.LastNewIdRemap 换算成本会话 u: id）。
///
/// key 用 hierarchyPath 而非 u: InstanceID —— InstanceID 跨 Unity 会话不稳定
/// （u: 仅会话内有效），hierarchyPath 跨会话稳定，与 AnimGroupSource 嵌入源
/// 的「按 hierarchyPath 重盖章」范式一致（动画烘焙 reparent 后路径变化由
/// 「写回后重导出盖章」天然吸收）。
/// </summary>
public static class LayoutEditorPartLevelStore
{
    private const int StoreSchemaVersion = 1;
    private const string DirName = "part_level~";
    private const string FileName = "part_level.json";
    private const int MaxBytes = 512 * 1024;

    // ---------- 路径推导（镜像 LayoutEditorWriteBackHistory.LevelDirFor 的场景侧逻辑） ----------

    /// <summary>场景资产路径 → part_level~ 目录的 Assets 相对路径；非法返回 null。</summary>
    private static string StoreAssetDir(string sceneAssetPath)
    {
        if (string.IsNullOrEmpty(sceneAssetPath))
            return null;
        var p = sceneAssetPath.Replace('\\', '/');
        var parts = p.Split('/');
        if (parts.Length < 4 || parts[0] != "Assets" || parts[1] != "LevelSets")
            return null;
        var set = parts[2];
        var fileName = Path.GetFileNameWithoutExtension(p);
        // 旧场景名 s_&lt;id&gt; 顺势迁移为无前缀（同 RenameLevel 约定），两种都接受。
        var levelId = fileName.StartsWith("s_", System.StringComparison.Ordinal) && fileName.Length > 2
            ? fileName.Substring(2)
            : fileName;
        return "Assets/LevelSets/" + set + "/data/" + levelId + "/" + DirName;
    }

    private static string StoreAbsPath(string sceneAssetPath)
    {
        var assetDir = StoreAssetDir(sceneAssetPath);
        if (string.IsNullOrEmpty(assetDir))
            return null;
        return Path.Combine(Path.Combine(Application.dataPath, ".."), assetDir.Replace('/', Path.DirectorySeparatorChar)) + Path.DirectorySeparatorChar + FileName;
    }

    // ---------- 读写 ----------

    public static PartLevelStoreDto Load(string sceneAssetPath)
    {
        var abs = StoreAbsPath(sceneAssetPath);
        if (string.IsNullOrEmpty(abs) || !File.Exists(abs))
            return null;
        try
        {
            var json = File.ReadAllText(abs);
            if (string.IsNullOrEmpty(json))
                return null;
            var store = JsonUtility.FromJson<PartLevelStoreDto>(json);
            return store;
        }
        catch (System.Exception e)
        {
            LayoutEditorLog.LogWarning("[PartLevel] 读取失败（按普关处理）：" + e.Message);
            return null;
        }
    }

    private static void Save(string sceneAssetPath, PartLevelStoreDto store)
    {
        var abs = StoreAbsPath(sceneAssetPath);
        if (string.IsNullOrEmpty(abs))
            return;
        store.schemaVersion = StoreSchemaVersion;
        var json = JsonUtility.ToJson(store, true);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        if (bytes.Length > MaxBytes)
        {
            LayoutEditorLog.LogWarning("[PartLevel] 配置过大（" + (bytes.Length / 1024) + "KB），放弃落盘");
            return;
        }
        var dir = Path.GetDirectoryName(abs);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        // `~` 目录不是 Unity 资产，无需 AssetDatabase.Refresh（同 assignment~）。
        File.WriteAllText(abs, json, System.Text.Encoding.UTF8);
    }

    // ---------- 合并 / 校验 / 落盘 ----------

    /// <summary>GET 导出合并：partLevel + 物件/地板 partId 按 hierarchyPath 盖章。</summary>
    public static void MergeIntoExport(LayoutDocumentDto doc)
    {
        if (doc == null)
            return;
        var store = Load(doc.sceneAssetPath);
        if (store == null)
            return;
        if (store.partLevel != null && store.partLevel.enabled)
            doc.partLevel = store.partLevel;
        else if (doc.partLevel == null)
            doc.partLevel = store.partLevel; // enabled=false 的历史档也如实回传
        StampItems(doc.items, store.itemPaths, store.itemParts);
        if (doc.floors != null && store.floorPaths != null)
        {
            var byPath = BuildPathMap(store.floorPaths, store.floorParts);
            for (var i = 0; i < doc.floors.Length; i++)
            {
                var f = doc.floors[i];
                if (f == null || string.IsNullOrEmpty(f.hierarchyPath))
                    continue;
                string part;
                if (byPath.TryGetValue(f.hierarchyPath, out part) && !string.IsNullOrEmpty(part))
                    f.partId = part;
            }
        }
    }

    private static void StampItems(LayoutItemDto[] items, string[] paths, string[] parts)
    {
        if (items == null || paths == null || parts == null)
            return;
        var byPath = BuildPathMap(paths, parts);
        for (var i = 0; i < items.Length; i++)
        {
            var it = items[i];
            if (it == null || string.IsNullOrEmpty(it.hierarchyPath))
                continue;
            string part;
            if (byPath.TryGetValue(it.hierarchyPath, out part) && !string.IsNullOrEmpty(part))
                it.partId = part;
        }
    }

    private static Dictionary<string, string> BuildPathMap(string[] paths, string[] parts)
    {
        var map = new Dictionary<string, string>();
        if (paths == null || parts == null)
            return map;
        var n = Mathf.Min(paths.Length, parts.Length);
        for (var i = 0; i < n; i++)
        {
            if (string.IsNullOrEmpty(paths[i]) || string.IsNullOrEmpty(parts[i]))
                continue;
            map[paths[i]] = parts[i];
        }
        return map;
    }

    /// <summary>不可逆守卫：已有 enabled 存档而新文档不带 enabled partLevel → 拒绝写回。
    /// 返回 null = 通过；非空 = 错误信息（HttpServer 直接 400）。</summary>
    public static string ValidateNotDowngrade(string sceneAssetPath, LayoutDocumentDto doc)
    {
        var store = Load(sceneAssetPath);
        if (store == null || store.partLevel == null || !store.partLevel.enabled)
            return null;
        if (doc == null || doc.partLevel == null || !doc.partLevel.enabled)
            return "该关卡已保存为「分 P 关卡」，不可降级为普通单阶段关卡（请通过写回历史或副本关卡回退）。";
        if (doc.partLevel.parts == null || doc.partLevel.parts.Length == 0)
            return "分 P 关卡必须定义至少一个阶段（parts[] 不能为空）。";
        return null;
    }

    /// <summary>POST 写回成功后落盘。普关（doc 无 partLevel 且无存档）为 no-op。
    /// 重盖章流程：doc 的 partId（含 new:→u: 换算）为权威 → 重导出场景拿最终
    /// hierarchyPath → 与旧存档按 path 合并（scoped 写回未携带的物件保留旧章）→
    /// 只保留场景中仍存在的条目（删除的物件自动清章）。</summary>
    public static void SaveAfterApply(string sceneAssetPath, LayoutDocumentDto doc)
    {
        var existing = Load(sceneAssetPath);
        if (doc == null || (doc.partLevel == null && existing == null))
            return;

        var store = existing ?? new PartLevelStoreDto();
        if (doc.partLevel != null)
            store.partLevel = doc.partLevel;
        if (store.partLevel == null)
            return;

        // 旧章（scoped 写回未覆盖的物件沿用）。
        var oldItemParts = BuildPathMap(store.itemPaths, store.itemParts);
        var oldFloorParts = BuildPathMap(store.floorPaths, store.floorParts);

        // 本次文档权威映射：instanceId → partId（new: 换算成会话 u: id）。
        var idToPart = new Dictionary<string, string>();
        CollectDocParts(doc.items, doc, idToPart);
        if (doc.floors != null)
            CollectDocParts(doc.floors, doc, idToPart);

        // 重导出场景（写回 + 动画烘焙 reparent 之后的状态）拿最终路径。
        var freshItems = SceneLayoutExporter.ExportFromScene();
        var freshFloors = SceneFloorExporter.ExportFromScene();

        var itemPaths = new List<string>();
        var itemPartsOut = new List<string>();
        StampFresh(freshItems == null ? null : freshItems.ToArray(), idToPart, oldItemParts, itemPaths, itemPartsOut);

        var floorPaths = new List<string>();
        var floorPartsOut = new List<string>();
        StampFreshFloors(freshFloors, idToPart, oldFloorParts, floorPaths, floorPartsOut);

        store.itemPaths = itemPaths.ToArray();
        store.itemParts = itemPartsOut.ToArray();
        store.floorPaths = floorPaths.ToArray();
        store.floorParts = floorPartsOut.ToArray();
        Save(sceneAssetPath, store);
    }

    private static void CollectDocParts(LayoutItemDto[] items, LayoutDocumentDto doc, Dictionary<string, string> idToPart)
    {
        if (items == null)
            return;
        for (var i = 0; i < items.Length; i++)
        {
            var it = items[i];
            if (it == null || string.IsNullOrEmpty(it.partId) || string.IsNullOrEmpty(it.instanceId))
                continue;
            idToPart[it.instanceId] = it.partId;
        }
    }

    private static void CollectDocParts(FloorDto[] floors, LayoutDocumentDto doc, Dictionary<string, string> idToPart)
    {
        if (floors == null)
            return;
        for (var i = 0; i < floors.Length; i++)
        {
            var f = floors[i];
            if (f == null || string.IsNullOrEmpty(f.partId) || string.IsNullOrEmpty(f.instanceId))
                continue;
            idToPart[f.instanceId] = f.partId;
        }
    }

    private static void StampFresh(LayoutItemDto[] fresh, Dictionary<string, string> idToPart,
        Dictionary<string, string> oldByPath, List<string> outPaths, List<string> outParts)
    {
        if (fresh == null)
            return;
        var seen = new HashSet<string>();
        for (var i = 0; i < fresh.Length; i++)
        {
            var it = fresh[i];
            if (it == null || string.IsNullOrEmpty(it.hierarchyPath))
                continue;
            var part = ResolvePart(it.instanceId, it.hierarchyPath, idToPart, oldByPath);
            if (string.IsNullOrEmpty(part) || part == "base")
                continue;
            if (seen.Add(it.hierarchyPath))
            {
                outPaths.Add(it.hierarchyPath);
                outParts.Add(part);
            }
        }
    }

    private static void StampFreshFloors(List<FloorDto> fresh, Dictionary<string, string> idToPart,
        Dictionary<string, string> oldByPath, List<string> outPaths, List<string> outParts)
    {
        if (fresh == null)
            return;
        var seen = new HashSet<string>();
        for (var i = 0; i < fresh.Count; i++)
        {
            var f = fresh[i];
            if (f == null || string.IsNullOrEmpty(f.hierarchyPath))
                continue;
            var part = ResolvePart(f.instanceId, f.hierarchyPath, idToPart, oldByPath);
            if (string.IsNullOrEmpty(part) || part == "base")
                continue;
            if (seen.Add(f.hierarchyPath))
            {
                outPaths.Add(f.hierarchyPath);
                outParts.Add(part);
            }
        }
    }

    /// <summary>权威顺序：本次文档 instanceId（u: 或 new:）&gt; 本次创建的新 id 反查
    /// （fresh 导出已是 u: id，经 Applier 换算表反查回文档的 new: 键）&gt; 旧存档按 path。</summary>
    private static string ResolvePart(string instanceId, string hierarchyPath,
        Dictionary<string, string> idToPart, Dictionary<string, string> oldByPath)
    {
        if (!string.IsNullOrEmpty(instanceId))
        {
            string part;
            if (idToPart.TryGetValue(instanceId, out part) && !string.IsNullOrEmpty(part))
                return part;
            // fresh 导出携带的是写回后会话 u: id；本次新建物件在文档里是 "new:" 键，
            // 经 Applier 的换算表（new: → "u:<instanceID>"）反查回文档 partId。
            var remap = SceneLayoutApplier.LastNewIdRemap;
            if (remap != null)
            {
                foreach (var kv in remap)
                {
                    if (kv.Value != instanceId)
                        continue;
                    string viaNew;
                    if (idToPart.TryGetValue(kv.Key, out viaNew) && !string.IsNullOrEmpty(viaNew))
                        return viaNew;
                }
            }
        }
        if (!string.IsNullOrEmpty(hierarchyPath))
        {
            string part;
            if (oldByPath.TryGetValue(hierarchyPath, out part))
                return part;
        }
        return null;
    }
}
