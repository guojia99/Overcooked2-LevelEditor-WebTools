using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 写回历史缓存：每次逻辑写回（web saveToUnity，最多 3 个 POST：layout → death → killplane）
/// 在仓库根 writeback_history/&lt;set&gt;/&lt;levelId&gt;/ 下缓存一条记录：
///   scene_before/after.unity.txt + info_before/after.asset.txt + diff.json + meta.json
/// 仅 /api/scene/layout POST 开记录（Begin）；death/killplane 归并进进行中的记录（Touch）。
/// 记录先以 pending_ 目录存在（含 state.json/语义快照，域重载安全），距最后活动 10 秒或
/// 下一次 Begin 时定稿；每关仅保留最近 15 条，超出自动滚动删除。
/// 所有 IO 异常吞掉（仅日志），绝不打断写回主流程。
/// </summary>
[InitializeOnLoad]
public static class LayoutEditorWriteBackHistory
{
    private const int KeepCount = 15;
    private const double GraceSeconds = 10.0;
    private const string RootName = "writeback_history";
    private const string PendingPrefix = "pending_";

    private static string _currentDir;
    private static string _currentScene;
    private static DateTime _lastSweepUtc = DateTime.MinValue;

    static LayoutEditorWriteBackHistory()
    {
        EditorApplication.update += SweepTick;
    }

    // ------------------------------------------------------------------ 公开入口

    /// <summary>layout POST 在任何修改之前调用：定稿旧 pending、开新 pending、缓存 before 快照。
    ///  sceneAssetPath 可为空（info-only 端点）：此时必须给 levelInfo，目录由 info 资产路径推导。</summary>
    public static void Begin(string endpoint, string sceneAssetPath, LevelInfoSO levelInfo)
    {
        try
        {
            FinalizeAllPending(true);
            if (string.IsNullOrEmpty(sceneAssetPath) && levelInfo == null)
            {
                LayoutEditorLog.LogWarning("[写回历史] 缺少 sceneAssetPath 与 levelInfo，本次操作不缓存。");
                return;
            }
            if (string.IsNullOrEmpty(sceneAssetPath) && levelInfo != null)
                sceneAssetPath = ScenePathForInfo(levelInfo);

            var now = DateTime.Now;
            var levelDir = LevelDirFor(sceneAssetPath, levelInfo);
            var pendingDir = Path.Combine(levelDir,
                PendingPrefix + now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + endpoint);
            Directory.CreateDirectory(pendingDir);

            string infoAssetPath = null;
            if (levelInfo != null)
                infoAssetPath = AssetDatabase.GetAssetPath(levelInfo);

            if (!string.IsNullOrEmpty(sceneAssetPath))
                CopyFile(SafeAbsPath(sceneAssetPath), Path.Combine(pendingDir, "scene_before.unity.txt"));
            if (!string.IsNullOrEmpty(infoAssetPath))
                CopyFile(SafeAbsPath(infoAssetPath), Path.Combine(pendingDir, "info_before.asset.txt"));

            if (levelInfo != null)
                WriteJsonFile(Path.Combine(pendingDir, "info_summary_before.json"),
                    SummarizeInfo(levelInfo, infoAssetPath));

            var state = new WriteBackStateDto
            {
                sceneAssetPath = sceneAssetPath ?? "",
                infoAssetPath = infoAssetPath ?? "",
                beginTimeText = now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                endpoints = new[] { endpoint }
            };
            WriteJsonFile(Path.Combine(pendingDir, "state.json"), state);

            _currentDir = pendingDir;
            _currentScene = sceneAssetPath;
            LayoutEditorLog.Log("[写回历史] 开记录 " + Path.GetFileName(pendingDir) +
                "（场景 " + Path.GetFileName(sceneAssetPath ?? "?") + "）");
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[写回历史] Begin 失败（不影响写回）：" + ex.Message);
        }
    }

    /// <summary>info 资产路径版 Begin（info-only 端点用：level/info、level/config、
    ///  level-recipes、optional-items、matchlists、screenshot-upload）。</summary>
    public static void BeginForInfo(string endpoint, string levelInfoAssetPath)
    {
        try
        {
            LevelInfoSO info = null;
            if (!string.IsNullOrEmpty(levelInfoAssetPath))
                info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(levelInfoAssetPath);
            Begin(endpoint, null, info);
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[写回历史] BeginForInfo 失败（不影响写回）：" + ex.Message);
        }
    }

    /// <summary>立即定稿当前 pending（单 POST 立即落盘型操作用：repair / level-info 等；
    ///  无 pending 或已定稿时 no-op）。</summary>
    public static void CommitNow()
    {
        try
        {
            if (!string.IsNullOrEmpty(_currentDir) && Directory.Exists(_currentDir))
                FinalizeOne(_currentDir);
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[写回历史] CommitNow 失败（不影响写回）：" + ex.Message);
        }
    }

    /// <summary>death/killplane POST 调用：归并进进行中的记录（宽限期刷新 + endpoints 登记）。
    ///  无进行中的记录则仅日志（独立调用不单独开记录）。</summary>
    public static void Touch(string endpoint, string sceneAssetPath)
    {
        try
        {
            var pendingDir = FindOpenPending(sceneAssetPath);
            if (pendingDir == null)
            {
                LayoutEditorLog.Log("[写回历史] " + endpoint + " 无进行中的写回记录，跳过缓存。");
                return;
            }

            var state = ReadState(pendingDir);
            if (state == null)
                return;
            var list = new List<string>(state.endpoints ?? new string[0]);
            if (!list.Contains(endpoint))
                list.Add(endpoint);
            state.endpoints = list.ToArray();
            WriteJsonFile(Path.Combine(pendingDir, "state.json"), state);
            // 重写已有文件不会更新目录 mtime，显式续期宽限窗口（定稿判定依据目录 mtime）。
            Directory.SetLastWriteTimeUtc(pendingDir, DateTime.UtcNow);        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[写回历史] Touch 失败（不影响写回）：" + ex.Message);
        }
    }

    /// <summary>Apply 在 OpenScene+Prepare 之后、任何改动之前调用（写回前语义快照）。</summary>
    public static void SupplySemanticBefore(string sceneAssetPath)
    {
        SupplySemantic(sceneAssetPath, "semantic_before.json");
    }

    /// <summary>SaveScene 之后、ReloadPseudoAssetsFull 之前调用（写回后语义快照，
    /// 覆盖式：killplane 之后的 Supply 会带上 layout+killplane 的累计变化）。
    /// 前后都取 stripped 状态，避免伪预制件子物体造成假差异。</summary>
    public static void SupplySemanticAfter(string sceneAssetPath)
    {
        SupplySemantic(sceneAssetPath, "semantic_after.json");
    }

    /// <summary>Apply 失败时调用：场景未保存（无 semantic_after）→ 丢弃 pending；
    /// 部分失败（场景已保存）→ 保留记录，交给宽限期清扫定稿。</summary>
    public static void Abort()
    {
        try
        {
            if (_currentDir != null && Directory.Exists(_currentDir))
            {
                if (File.Exists(Path.Combine(_currentDir, "semantic_after.json")))
                {
                    LayoutEditorLog.LogWarning("[写回历史] 写回部分失败（场景已保存），保留记录 " +
                        Path.GetFileName(_currentDir));
                }
                else
                {
                    Directory.Delete(_currentDir, true);
                    LayoutEditorLog.LogWarning("[写回历史] 写回失败，丢弃记录 " + Path.GetFileName(_currentDir));
                }
            }
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[写回历史] Abort 清理失败：" + ex.Message);
        }
        finally
        {
            _currentDir = null;
            _currentScene = null;
        }
    }

    // ------------------------------------------------------------------ 查询 API（关卡管理页「工具与历史」弹窗消费）

    /// <summary>列出某关最近的历史记录（新→旧）。segments 先做白名单校验防路径穿越。</summary>
    public static WriteBackHistoryListDto ListRecords(string set, string levelId)
    {
        var result = new List<WriteBackHistoryItemDto>();
        if (!IsValidSegment(set) || !IsValidSegment(levelId))
            return new WriteBackHistoryListDto { items = result.ToArray() };

        var levelDir = Path.Combine(Path.Combine(RootDir(), set), levelId);
        var dirs = new List<string>(SafeGetDirs(levelDir));
        dirs.Sort(ComparisonDescByName);
        foreach (var dir in dirs)
        {
            var name = Path.GetFileName(dir);
            if (name.StartsWith(PendingPrefix, StringComparison.Ordinal))
                continue;
            var meta = ReadJsonFile<WriteBackMetaDto>(Path.Combine(dir, "meta.json"));
            var diff = ReadJsonFile<WriteBackDiffDto>(Path.Combine(dir, "diff.json"));
            var item = new WriteBackHistoryItemDto
            {
                record = name,
                beginTime = meta != null ? meta.beginTime : name,
                finalizeTime = meta != null ? meta.finalizeTime : "",
                endpoints = meta != null && meta.endpoints != null ? meta.endpoints : new string[0],
                semanticAvailable = meta != null && meta.semanticAvailable,
                itemsBefore = meta != null ? meta.itemsBefore : -1,
                itemsAfter = meta != null ? meta.itemsAfter : -1,
                floorsBefore = meta != null ? meta.floorsBefore : -1,
                floorsAfter = meta != null ? meta.floorsAfter : -1,
                itemsAdded = diff != null && diff.itemsAdded != null ? diff.itemsAdded.Length : 0,
                itemsRemoved = diff != null && diff.itemsRemoved != null ? diff.itemsRemoved.Length : 0,
                itemsMoved = diff != null && diff.itemsMoved != null ? diff.itemsMoved.Length : 0,
                itemsChanged = diff != null && diff.itemsChanged != null ? diff.itemsChanged.Length : 0,
                floorsAdded = diff != null && diff.floorsAdded != null ? diff.floorsAdded.Length : 0,
                floorsRemoved = diff != null && diff.floorsRemoved != null ? diff.floorsRemoved.Length : 0,
                floorsChanged = diff != null && diff.floorsChanged != null ? diff.floorsChanged.Length : 0,
                cameraChanged = diff != null && diff.cameraChanged,
                infoMissing = diff == null || diff.info == null || diff.info.missing,
                sceneBytesBefore = diff != null ? diff.sceneBytesBefore : 0,
                sceneBytesAfter = diff != null ? diff.sceneBytesAfter : 0,
                infoBytesBefore = diff != null ? diff.infoBytesBefore : 0,
                infoBytesAfter = diff != null ? diff.infoBytesAfter : 0
            };
            result.Add(item);
        }
        return new WriteBackHistoryListDto { items = result.ToArray() };
    }

    /// <summary>读取一条记录的完整 meta + diff；找不到或参数非法返回 null。</summary>
    public static WriteBackHistoryDetailDto ReadRecord(string set, string levelId, string recordName)
    {
        var dir = RecordDir(set, levelId, recordName);
        if (dir == null)
            return null;
        var detail = new WriteBackHistoryDetailDto
        {
            record = recordName,
            meta = ReadJsonFile<WriteBackMetaDto>(Path.Combine(dir, "meta.json")),
            diff = ReadJsonFile<WriteBackDiffDto>(Path.Combine(dir, "diff.json")),
            // 恢复到画布需要完整 doc（doc_before/after.json；旧记录只有轻量语义快照，不可恢复）
            canRestoreBefore = File.Exists(Path.Combine(dir, "doc_before.json")),
            canRestoreAfter = File.Exists(Path.Combine(dir, "doc_after.json"))
        };
        if (detail.meta == null && detail.diff == null)
            return null;
        return detail;
    }

    /// <summary>读取一条记录的完整布局文档（side = before | after），用于「恢复到画布」；
    ///  无完整文档（旧记录/info-only 记录）返回 null。</summary>
    public static string ReadDocument(string set, string levelId, string recordName, string side)
    {
        var dir = RecordDir(set, levelId, recordName);
        if (dir == null)
            return null;
        var fileName = side == "before" ? "doc_before.json" : side == "after" ? "doc_after.json" : null;
        if (fileName == null)
            return null;
        var path = Path.Combine(dir, fileName);
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>记录目录（含白名单校验）；非法/不存在返回 null。</summary>
    private static string RecordDir(string set, string levelId, string recordName)
    {
        if (!IsValidSegment(set) || !IsValidSegment(levelId) || !IsValidSegment(recordName))
            return null;
        var dir = Path.Combine(Path.Combine(Path.Combine(RootDir(), set), levelId), recordName);
        return Directory.Exists(dir) ? dir : null;
    }

    /// <summary>路径段白名单：字母/数字/下划线/连字符/点，且不得含 ".."。</summary>
    private static bool IsValidSegment(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf("..") >= 0)
            return false;
        foreach (var c in s)
        {
            var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                || c == '_' || c == '-' || c == '.';
            if (!ok)
                return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ 定稿与清扫

    private static void SweepTick()
    {
        var utc = DateTime.UtcNow;
        if ((utc - _lastSweepUtc).TotalSeconds < 1.0)
            return;
        _lastSweepUtc = utc;
        FinalizeAllPending(false);
    }

    /// <summary>枚举全部 pending 目录；force=true 全部定稿，否则只定稿超过宽限期的。</summary>
    private static void FinalizeAllPending(bool force)
    {
        var root = RootDir();
        if (!Directory.Exists(root))
            return;
        var now = DateTime.Now;
        foreach (var setDir in SafeGetDirs(root))
        {
            foreach (var levelDir in SafeGetDirs(setDir))
            {
                foreach (var dir in SafeGetDirs(levelDir))
                {
                    if (!Path.GetFileName(dir).StartsWith(PendingPrefix, StringComparison.Ordinal))
                        continue;
                    if (!force && (now - Directory.GetLastWriteTime(dir)).TotalSeconds <= GraceSeconds)
                        continue;
                    FinalizeOne(dir);
                }
            }
        }
    }

    private static void FinalizeOne(string pendingDir)
    {
        var levelDir = Path.GetDirectoryName(pendingDir);
        var state = ReadState(pendingDir);
        if (state == null || (string.IsNullOrEmpty(state.sceneAssetPath) && string.IsNullOrEmpty(state.infoAssetPath)))
        {
            LayoutEditorLog.LogWarning("[写回历史] pending 缺少 state.json，丢弃：" + pendingDir);
            TryDeleteDir(pendingDir);
            ClearCurrentIf(pendingDir);
            return;
        }

        // after 快照：此刻磁盘 = 整个 POST 序列的最终状态（含 killplane 的 SaveScene、
        // death 的 SaveAssets）。sceneAssetPath 为空 = info-only 记录，无场景快照。
        if (!string.IsNullOrEmpty(state.sceneAssetPath))
            CopyFile(SafeAbsPath(state.sceneAssetPath), Path.Combine(pendingDir, "scene_after.unity.txt"));
        if (!string.IsNullOrEmpty(state.infoAssetPath))
            CopyFile(SafeAbsPath(state.infoAssetPath), Path.Combine(pendingDir, "info_after.asset.txt"));

        WriteBackInfoSummaryDto infoAfter = null;
        if (!string.IsNullOrEmpty(state.infoAssetPath))
        {
            var info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(state.infoAssetPath);
            if (info != null)
            {
                infoAfter = SummarizeInfo(info, state.infoAssetPath);
                WriteJsonFile(Path.Combine(pendingDir, "info_summary_after.json"), infoAfter);
            }
        }

        var before = ReadJsonFile<WriteBackSnapshotDto>(Path.Combine(pendingDir, "semantic_before.json"));
        var after = ReadJsonFile<WriteBackSnapshotDto>(Path.Combine(pendingDir, "semantic_after.json"));
        var infoBefore = ReadJsonFile<WriteBackInfoSummaryDto>(Path.Combine(pendingDir, "info_summary_before.json"));

        var diff = BuildDiff(state, before, after, infoBefore, infoAfter, pendingDir);
        WriteJsonFile(Path.Combine(pendingDir, "diff.json"), diff);

        var meta = new WriteBackMetaDto
        {
            beginTime = state.beginTimeText,
            finalizeTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
            sceneAssetPath = state.sceneAssetPath,
            infoAssetPath = state.infoAssetPath,
            endpoints = state.endpoints ?? new string[0],
            itemsBefore = before != null && before.items != null ? before.items.Length : -1,
            itemsAfter = after != null && after.items != null ? after.items.Length : -1,
            floorsBefore = before != null && before.floors != null ? before.floors.Length : -1,
            floorsAfter = after != null && after.floors != null ? after.floors.Length : -1,
            semanticAvailable = diff.semanticAvailable
        };
        WriteJsonFile(Path.Combine(pendingDir, "meta.json"), meta);

        var finalName = Path.GetFileName(pendingDir).Substring(PendingPrefix.Length);
        var finalDir = Path.Combine(levelDir, finalName);
        var suffix = 2;
        while (Directory.Exists(finalDir))
        {
            finalDir = Path.Combine(levelDir, finalName + "_" + suffix);
            suffix++;
        }
        Directory.Move(pendingDir, finalDir);
        ClearCurrentIf(pendingDir);
        RollingDelete(levelDir);

        var moved = diff.itemsMoved != null ? diff.itemsMoved.Length : 0;
        var added = diff.itemsAdded != null ? diff.itemsAdded.Length : 0;
        var removed = diff.itemsRemoved != null ? diff.itemsRemoved.Length : 0;
        LayoutEditorLog.Log("[写回历史] 已缓存 " + Path.GetFileName(finalDir) +
            "（物品 +" + added + "/-" + removed + "/移动" + moved + "）");
    }

    /// <summary>滚动删除：每关仅保留最近 KeepCount 条正式记录（pending 不占配额）。</summary>
    private static void RollingDelete(string levelDir)
    {
        var records = new List<string>();
        foreach (var dir in SafeGetDirs(levelDir))
        {
            if (!Path.GetFileName(dir).StartsWith(PendingPrefix, StringComparison.Ordinal))
                records.Add(dir);
        }
        records.Sort(ComparisonDescByName);
        for (var i = KeepCount; i < records.Count; i++)
        {
            TryDeleteDir(records[i]);
            LayoutEditorLog.Log("[写回历史] 滚动删除 " + Path.GetFileName(records[i]));
        }
    }

    private static int ComparisonDescByName(string a, string b)
    {
        return string.CompareOrdinal(Path.GetFileName(b), Path.GetFileName(a));
    }

    // ------------------------------------------------------------------ 语义快照

    private static void SupplySemantic(string sceneAssetPath, string fileName)
    {
        try
        {
            var pendingDir = FindOpenPending(sceneAssetPath);
            if (pendingDir == null)
                return;
            var doc = CaptureSnapshot();
            if (doc == null)
                return;
            // 双写：semantic_*.json = 轻量子集（diff 用，兼容旧记录）；doc_*.json = 完整
            // LayoutDocument（含 animControls/walkable/deathInfo/switchLinks/buttonLinks/
            // buttonEvents，供「恢复到画布」）。同一份导出，零额外遍历。
            WriteJsonFile(Path.Combine(pendingDir, fileName), SnapshotSubsetOf(doc));
            WriteJsonFile(Path.Combine(pendingDir, fileName.Replace("semantic_", "doc_")), doc);
            // 覆盖写不更新目录 mtime，显式续期宽限窗口。
            Directory.SetLastWriteTimeUtc(pendingDir, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[写回历史] 语义快照失败（不影响写回）：" + ex.Message);
        }
    }

    /// <summary>完整导出当前活动场景（ExportActiveScene），同时用于 diff 子集与恢复文档。</summary>
    private static LayoutDocumentDto CaptureSnapshot()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return null;
        return SceneLayoutExporter.ExportActiveScene();
    }

    private static WriteBackSnapshotDto SnapshotSubsetOf(LayoutDocumentDto doc)
    {
        return new WriteBackSnapshotDto
        {
            sceneAssetPath = doc.sceneAssetPath,
            items = doc.items,
            floors = doc.floors,
            cameraInfo = doc.cameraInfo,
            lights = doc.lights
        };
    }

    // ------------------------------------------------------------------ diff 生成

    private static WriteBackDiffDto BuildDiff(
        WriteBackStateDto state,
        WriteBackSnapshotDto before,
        WriteBackSnapshotDto after,
        WriteBackInfoSummaryDto infoBefore,
        WriteBackInfoSummaryDto infoAfter,
        string pendingDir)
    {
        var diff = new WriteBackDiffDto
        {
            recordedAt = state.beginTimeText,
            scenePath = state.sceneAssetPath,
            semanticAvailable = before != null && after != null
        };

        if (diff.semanticAvailable)
        {
            DiffEntries(before.items, after.items,
                out diff.itemsAdded, out diff.itemsRemoved, out diff.itemsMoved,
                out diff.itemsChanged, out diff.itemsReid, out diff.itemsUnchanged);
            DiffPlain(before.floors, after.floors,
                out diff.floorsAdded, out diff.floorsRemoved, out diff.floorsChanged,
                out diff.floorsUnchanged);
            var camChanges = new List<WriteBackFieldChange>();
            CollectFieldChanges(before.cameraInfo, after.cameraInfo, "", camChanges);
            diff.cameraChanged = camChanges.Count > 0;
            if (diff.cameraChanged)
                diff.cameraDetail = BuildDetail(camChanges);
            DiffLights(before.lights, after.lights,
                out diff.lightsAdded, out diff.lightsRemoved, out diff.lightsChanged,
                out diff.lightsUnchanged);
        }
        else
        {
            diff.itemsAdded = new DiffEntryDto[0];
            diff.itemsRemoved = new DiffEntryDto[0];
            diff.itemsMoved = new ItemMoveDto[0];
            diff.itemsChanged = new DiffEntryDto[0];
            diff.itemsReid = new DiffEntryDto[0];
            diff.floorsAdded = new DiffEntryDto[0];
            diff.floorsRemoved = new DiffEntryDto[0];
            diff.floorsChanged = new DiffEntryDto[0];
            diff.lightsAdded = new DiffEntryDto[0];
            diff.lightsRemoved = new DiffEntryDto[0];
            diff.lightsChanged = new DiffEntryDto[0];
        }

        diff.info = DiffInfo(infoBefore, infoAfter);
        diff.sceneBytesBefore = FileSize(Path.Combine(pendingDir, "scene_before.unity.txt"));
        diff.sceneBytesAfter = FileSize(Path.Combine(pendingDir, "scene_after.unity.txt"));
        diff.infoBytesBefore = FileSize(Path.Combine(pendingDir, "info_before.asset.txt"));
        diff.infoBytesAfter = FileSize(Path.Combine(pendingDir, "info_after.asset.txt"));
        return diff;
    }

    // ------------------------------------------------------------------ 字段级深度对比（精细 diff 核心）

    /// <summary>float 判等容差：远大于 transform 往返噪声（~1e-6），远小于最小编辑粒度（snap 0.01）。</summary>
    private const float FloatEps = 0.001f;

    private class WriteBackFieldChange
    {
        public string path;
        public string before;
        public string after;
    }

    /// <summary>反射递归对比两个 DTO：float 按容差、标量精确、嵌套类/数组递归。
    ///  产出变更字段路径清单 —— 既滤除浮点噪声（旧版 JSON 文本对比把 1e-6 往返误差
    ///  误报成「参数变化」），又拿到具体字段供分类与明细文案。</summary>
    private static void CollectFieldChanges(object before, object after, string prefix, List<WriteBackFieldChange> changes)
    {
        if (before == null && after == null)
            return;
        if (before == null || after == null)
        {
            changes.Add(new WriteBackFieldChange { path = prefix, before = ShortValue(before), after = ShortValue(after) });
            return;
        }
        var t = before.GetType();
        if (t != after.GetType())
        {
            changes.Add(new WriteBackFieldChange { path = prefix, before = ShortValue(before), after = ShortValue(after) });
            return;
        }

        if (t == typeof(float))
        {
            if (Mathf.Abs((float)before - (float)after) > FloatEps)
                changes.Add(new WriteBackFieldChange { path = prefix, before = ShortValue(before), after = ShortValue(after) });
            return;
        }
        if (t.IsPrimitive || t.IsEnum || t == typeof(string))
        {
            if (!Equals(before, after))
                changes.Add(new WriteBackFieldChange { path = prefix, before = ShortValue(before), after = ShortValue(after) });
            return;
        }

        if (t.IsArray)
        {
            var ab = (System.Array)before;
            var aa = (System.Array)after;
            if (ab.Length != aa.Length)
            {
                changes.Add(new WriteBackFieldChange
                {
                    path = prefix,
                    before = ab.Length + " 项",
                    after = aa.Length + " 项"
                });
                return;
            }
            for (var i = 0; i < ab.Length; i++)
                CollectFieldChanges(ab.GetValue(i), aa.GetValue(i), prefix + "[" + i + "]", changes);
            return;
        }

        foreach (var f in t.GetFields())
            CollectFieldChanges(f.GetValue(before), f.GetValue(after),
                prefix.Length == 0 ? f.Name : prefix + "." + f.Name, changes);
    }

    /// <summary>字段值短文本（明细文案用）：float 保留 3 位，bool → 开/关，null → 无。</summary>
    private static string ShortValue(object v)
    {
        if (v == null)
            return "无";
        if (v is float)
            return ((float)v).ToString("0.###", CultureInfo.InvariantCulture);
        if (v is bool)
            return (bool)v ? "开" : "关";
        var s = v.ToString();
        return s.Length > 60 ? s.Substring(0, 60) + "…" : s;
    }

    /// <summary>字段路径 → 变更分类。moved/rotated/scaled 归变换条目（from→to 展示），
    ///  其余进 changed 条目的 kinds 徽章；instanceId 单独归 reid。</summary>
    private static string KindOfPath(string path)
    {
        if (path == "instanceId")
            return "reid";
        if (path.StartsWith("worldPosition") || path.StartsWith("localPosition"))
            return "moved";
        if (path.StartsWith("localRotation"))
            return "rotated";
        if (path.StartsWith("localScale"))
            return "scaled";
        if (path.StartsWith("prefabGuid") || path.StartsWith("prefabAssetPath"))
            return "prefab";
        if (path.StartsWith("parentPath") || path.StartsWith("hierarchyPath"))
            return "hierarchy";
        return "config";
    }

    /// <summary>常用字段路径 → 中文标签（明细文案）；未映射返回原路径。</summary>
    private static string FieldLabel(string path)
    {
        var map = new Dictionary<string, string>
        {
            { "dispenser.spawnerItemPrefabGuid", "食材" },
            { "dispenser.randomItemGuids", "随机候选" },
            { "dispenser.randomWeights", "随机权重" },
            { "dispenser.questionMarkGuid", "问号图标" },
            { "timedSwitch.enabled", "定时启用" },
            { "timedSwitch.onSeconds", "定时开秒数" },
            { "timedSwitch.offSeconds", "定时关秒数" },
            { "timedSwitch.startOn", "定时初始相位" },
            { "conveyor.speed", "传送带速度" },
            { "conveyor.reversed", "传送带方向" },
            { "soArray.pseudoPrefabGuids", "候选列表" },
            { "pseudoPrefabGuid", "外观皮肤" },
            { "stubKind", "功能类型" },
            { "walkable", "可行走" },
            { "airWall", "空气墙" },
            { "footprint", "占地尺寸" },
            { "colliderCenter", "碰撞盒中心" },
            { "burner.fuelCostPerUse", "燃料消耗" },
            { "burner.fuelRegenSeconds", "燃料恢复秒数" },
            { "terminal.recallTime", "终端召回时间" },
            { "travelator.speed", "移动走道速度" },
            { "cannon.fuseSeconds", "大炮引信秒数" },
            { "cannon.projectileSpeed", "弹丸速度" },
            { "flamethrower.burnSeconds", "喷灯燃烧秒数" },
            { "worldPosition", "位置" },
            { "localPosition", "位置" },
            { "localRotationX", "旋转X" },
            { "localRotationY", "旋转Y" },
            { "localRotationZ", "旋转Z" },
            { "localScale", "缩放" }
        };
        string label;
        return map.TryGetValue(path, out label) ? label : path;
    }

    /// <summary>变更字段清单 → 明细文案（前 3 条「标签: 旧 → 新」，分号连接）。</summary>
    private static string BuildDetail(IEnumerable<WriteBackFieldChange> changes)
    {
        var sb = new System.Text.StringBuilder();
        var n = 0;
        foreach (var c in changes)
        {
            if (n >= 3)
            {
                sb.Append("；…");
                break;
            }
            if (n > 0)
                sb.Append("；");
            sb.Append(FieldLabel(c.path)).Append(": ").Append(c.before).Append(" → ").Append(c.after);
            n++;
        }
        return sb.ToString();
    }

    private static void DiffEntries(
        LayoutItemDto[] before,
        LayoutItemDto[] after,
        out DiffEntryDto[] added,
        out DiffEntryDto[] removed,
        out ItemMoveDto[] moved,
        out DiffEntryDto[] changed,
        out DiffEntryDto[] reid,
        out int unchanged)
    {
        var beforeMap = ItemMap(before);
        var afterMap = ItemMap(after);
        var addedList = new List<DiffEntryDto>();
        var removedList = new List<DiffEntryDto>();
        var movedList = new List<ItemMoveDto>();
        var changedList = new List<DiffEntryDto>();
        var reidList = new List<DiffEntryDto>();
        unchanged = 0;

        // 第一轮：instanceId 精确匹配。
        var matchOfBefore = new Dictionary<string, LayoutItemDto>();
        var usedAfterIds = new HashSet<string>();
        foreach (var kv in beforeMap)
        {
            LayoutItemDto a;
            if (afterMap.TryGetValue(kv.Key, out a))
            {
                matchOfBefore[kv.Key] = a;
                usedAfterIds.Add(kv.Key);
            }
        }

        // 第二轮（id 回退匹配）：写回给物品重新盖章 id 后（如「恢复到画布→写回」全量
        // new:restore id），按 prefab + 最近邻位置（<0.01）配对，避免整图误报增删。
        foreach (var bk in beforeMap.Keys)
        {
            if (matchOfBefore.ContainsKey(bk))
                continue;
            var b = beforeMap[bk];
            string bestKey = null;
            var bestDist = float.MaxValue;
            foreach (var kv in afterMap)
            {
                if (usedAfterIds.Contains(kv.Key))
                    continue;
                var a = kv.Value;
                if ((a.prefabGuid ?? "") != (b.prefabGuid ?? "") ||
                    (a.prefabAssetPath ?? "") != (b.prefabAssetPath ?? ""))
                    continue;
                var dist = ItemDistance(b, a);
                if (dist < 0.01f && dist < bestDist)
                {
                    bestDist = dist;
                    bestKey = kv.Key;
                }
            }
            if (bestKey != null)
            {
                usedAfterIds.Add(bestKey);
                matchOfBefore[bk] = afterMap[bestKey];
            }
        }

        foreach (var kv in beforeMap)
        {
            var b = kv.Value;
            LayoutItemDto a;
            if (!matchOfBefore.TryGetValue(kv.Key, out a))
            {
                removedList.Add(new DiffEntryDto { id = kv.Key, name = NameOf(b) });
                continue;
            }

            var changes = new List<WriteBackFieldChange>();
            CollectFieldChanges(b, a, "", changes);
            if (changes.Count == 0)
            {
                unchanged++;
                continue;
            }

            var idChanged = false;
            var transformKinds = new List<string>();
            var otherChanges = new List<WriteBackFieldChange>();
            var kindSet = new List<string>();
            foreach (var c in changes)
            {
                var kind = KindOfPath(c.path);
                if (kind == "reid")
                {
                    idChanged = true;
                    continue;
                }
                if (kind == "moved" || kind == "rotated" || kind == "scaled")
                {
                    if (!transformKinds.Contains(kind))
                        transformKinds.Add(kind);
                }
                else
                {
                    if (!kindSet.Contains(kind))
                        kindSet.Add(kind);
                    otherChanges.Add(c);
                }
            }

            if (idChanged && transformKinds.Count == 0 && kindSet.Count == 0)
            {
                // 仅 id 变（重编号）：内容未变，浅色单列、不计入变动统计。
                reidList.Add(new DiffEntryDto { id = kv.Key, name = NameOf(b), kinds = new[] { "reid" }, detail = "" });
                continue;
            }

            foreach (var kind in transformKinds)
            {
                movedList.Add(new ItemMoveDto
                {
                    id = kv.Key,
                    name = NameOf(b),
                    kind = kind,
                    fromPosition = TransformSummary(b, kind),
                    toPosition = TransformSummary(a, kind)
                });
            }
            if (kindSet.Count > 0)
            {
                changedList.Add(new DiffEntryDto
                {
                    id = kv.Key,
                    name = NameOf(b),
                    kinds = kindSet.ToArray(),
                    detail = BuildDetail(otherChanges)
                });
            }
            if (transformKinds.Count == 0 && kindSet.Count == 0)
                unchanged++;
        }

        foreach (var kv in afterMap)
        {
            if (!usedAfterIds.Contains(kv.Key))
                addedList.Add(new DiffEntryDto { id = kv.Key, name = NameOf(kv.Value) });
        }

        added = InitEntries(addedList);
        removed = InitEntries(removedList);
        moved = movedList.ToArray();
        changed = InitEntries(changedList);
        reid = InitEntries(reidList);
    }

    private static DiffEntryDto[] InitEntries(List<DiffEntryDto> list)
    {
        foreach (var e in list)
        {
            if (e.kinds == null)
                e.kinds = new string[0];
            if (e.detail == null)
                e.detail = "";
        }
        return list.ToArray();
    }

    private static float ItemDistance(LayoutItemDto a, LayoutItemDto b)
    {
        var pa = a.worldPosition != null ? a.worldPosition : a.localPosition;
        var pb = b.worldPosition != null ? b.worldPosition : b.localPosition;
        if (pa == null || pb == null)
            return float.MaxValue;
        return Mathf.Max(Mathf.Abs(pa.x - pb.x), Mathf.Max(Mathf.Abs(pa.y - pb.y), Mathf.Abs(pa.z - pb.z)));
    }

    /// <summary>变换摘要：moved = x,y,z；rotated = 欧拉角；scaled = 三轴缩放。</summary>
    private static string TransformSummary(LayoutItemDto item, string kind)
    {
        if (kind == "rotated")
            return string.Format(CultureInfo.InvariantCulture, "{0:0.#},{1:0.#},{2:0.#}",
                item.localRotationX, item.localRotationY, item.localRotationZ);
        if (kind == "scaled")
        {
            var s = item.localScale;
            return s != null
                ? string.Format(CultureInfo.InvariantCulture, "{0:0.###},{1:0.###},{2:0.###}", s.x, s.y, s.z)
                : "?";
        }
        return FormatPosition(item);
    }

    private static void DiffPlain(
        FloorDto[] before,
        FloorDto[] after,
        out DiffEntryDto[] added,
        out DiffEntryDto[] removed,
        out DiffEntryDto[] changed,
        out int unchanged)
    {
        var beforeMap = FloorMap(before);
        var afterMap = FloorMap(after);
        var addedList = new List<DiffEntryDto>();
        var removedList = new List<DiffEntryDto>();
        var changedList = new List<DiffEntryDto>();
        unchanged = 0;

        foreach (var kv in afterMap)
        {
            FloorDto b;
            if (!beforeMap.TryGetValue(kv.Key, out b))
            {
                addedList.Add(new DiffEntryDto { id = kv.Key, name = NameOf(kv.Value) });
                continue;
            }
            var changes = new List<WriteBackFieldChange>();
            CollectFieldChanges(b, kv.Value, "", changes);
            var real = new List<WriteBackFieldChange>();
            var kindSet = new List<string>();
            foreach (var c in changes)
            {
                var kind = KindOfPath(c.path);
                if (kind == "reid")
                    continue;
                if (!kindSet.Contains(kind))
                    kindSet.Add(kind);
                real.Add(c);
            }
            if (kindSet.Count > 0)
                changedList.Add(new DiffEntryDto
                {
                    id = kv.Key,
                    name = NameOf(kv.Value),
                    kinds = kindSet.ToArray(),
                    detail = BuildDetail(real)
                });
            else
                unchanged++;
        }
        foreach (var kv in beforeMap)
        {
            if (!afterMap.ContainsKey(kv.Key))
                removedList.Add(new DiffEntryDto { id = kv.Key, name = NameOf(kv.Value) });
        }

        added = InitEntries(addedList);
        removed = InitEntries(removedList);
        changed = InitEntries(changedList);
    }

    private static void DiffLights(
        LightInfoDto[] before,
        LightInfoDto[] after,
        out DiffEntryDto[] added,
        out DiffEntryDto[] removed,
        out DiffEntryDto[] changed,
        out int unchanged)
    {
        var beforeMap = new Dictionary<string, LightInfoDto>();
        var afterMap = new Dictionary<string, LightInfoDto>();
        FillMap(beforeMap, before);
        FillMap(afterMap, after);
        var addedList = new List<DiffEntryDto>();
        var removedList = new List<DiffEntryDto>();
        var changedList = new List<DiffEntryDto>();
        unchanged = 0;

        foreach (var kv in afterMap)
        {
            LightInfoDto b;
            if (!beforeMap.TryGetValue(kv.Key, out b))
            {
                addedList.Add(new DiffEntryDto { id = kv.Key, name = kv.Value.displayName });
                continue;
            }
            var changes = new List<WriteBackFieldChange>();
            CollectFieldChanges(b, kv.Value, "", changes);
            var kindSet = new List<string>();
            foreach (var c in changes)
            {
                var kind = KindOfPath(c.path);
                if (kind != "reid" && !kindSet.Contains(kind))
                    kindSet.Add(kind);
            }
            if (kindSet.Count > 0)
                changedList.Add(new DiffEntryDto
                {
                    id = kv.Key,
                    name = kv.Value.displayName,
                    kinds = kindSet.ToArray(),
                    detail = BuildDetail(changes)
                });
            else
                unchanged++;
        }
        foreach (var kv in beforeMap)
        {
            if (!afterMap.ContainsKey(kv.Key))
                removedList.Add(new DiffEntryDto { id = kv.Key, name = kv.Value.displayName });
        }

        added = InitEntries(addedList);
        removed = InitEntries(removedList);
        changed = InitEntries(changedList);
    }

    private static WriteBackInfoDiffDto DiffInfo(
        WriteBackInfoSummaryDto before,
        WriteBackInfoSummaryDto after)
    {
        var d = new WriteBackInfoDiffDto();
        if (before == null || after == null)
        {
            d.missing = true;
            d.recipesAdded = new string[0];
            d.recipesRemoved = new string[0];
            d.ingredientsAdded = new string[0];
            d.ingredientsRemoved = new string[0];
            d.dependenciesAdded = new string[0];
            d.dependenciesRemoved = new string[0];
            d.audioAdded = new string[0];
            d.audioRemoved = new string[0];
            d.ambiencesAdded = new string[0];
            d.ambiencesRemoved = new string[0];
            d.optionalItemsAdded = new string[0];
            d.optionalItemsRemoved = new string[0];
            d.matchlistsAdded = new string[0];
            d.matchlistsRemoved = new string[0];
            d.configsBefore = new string[0];
            d.configsAfter = new string[0];
            return d;
        }

        d.recipesAdded = AddedNames(before.recipes, after.recipes);
        d.recipesRemoved = AddedNames(after.recipes, before.recipes);
        d.ingredientsAdded = AddedNames(before.allIngredients, after.allIngredients);
        d.ingredientsRemoved = AddedNames(after.allIngredients, before.allIngredients);
        d.dependenciesAdded = AddedNames(before.dependencies, after.dependencies);
        d.dependenciesRemoved = AddedNames(after.dependencies, before.dependencies);
        d.audioAdded = AddedNames(before.audioDirectories, after.audioDirectories);
        d.audioRemoved = AddedNames(after.audioDirectories, before.audioDirectories);
        d.ambiencesAdded = AddedNames(before.inLevelAmbiences, after.inLevelAmbiences);
        d.ambiencesRemoved = AddedNames(after.inLevelAmbiences, before.inLevelAmbiences);
        d.optionalItemsAdded = AddedNames(before.optionalItems, after.optionalItems);
        d.optionalItemsRemoved = AddedNames(after.optionalItems, before.optionalItems);
        d.matchlistsAdded = AddedNames(before.matchlists, after.matchlists);
        d.matchlistsRemoved = AddedNames(after.matchlists, before.matchlists);
        d.configsBefore = before.configs != null ? (string[])before.configs.Clone() : new string[0];
        d.configsAfter = after.configs != null ? (string[])after.configs.Clone() : new string[0];
        d.screenshotBefore = before.screenshot;
        d.screenshotAfter = after.screenshot;
        d.deathEffectBefore = before.onDeathEffect;
        d.deathEffectAfter = after.onDeathEffect;
        d.levelNameBefore = before.levelName;
        d.levelNameAfter = after.levelName;
        d.minMaxOrdersBefore = before.minOrderCount + "-" + before.maxOrderCount;
        d.minMaxOrdersAfter = after.minOrderCount + "-" + after.maxOrderCount;
        return d;
    }

    // ------------------------------------------------------------------ info 摘要

    private static WriteBackInfoSummaryDto SummarizeInfo(LevelInfoSO info, string assetPath)
    {
        var ambiences = new List<string>();
        if (info.inLevelAmbiences != null)
        {
            foreach (var amb in info.inLevelAmbiences)
                ambiences.Add(amb.ToString());
        }
        return new WriteBackInfoSummaryDto
        {
            assetPath = assetPath ?? "",
            levelName = info.levelName ?? "",
            levelNameZH = info.levelNameZH ?? "",
            sceneName = info.sceneName ?? "",
            recipes = NamesOf(info.recipes),
            allIngredients = NamesOf(info.allIngredients),
            dependencies = info.dependencies != null ? (string[])info.dependencies.Clone() : new string[0],
            onDeathEffect = info.onDeathEffectSO != null ? info.onDeathEffectSO.name : "",
            audioDirectories = NamesOf(info.audioDirectorySOs),
            inLevelMusic = info.inLevelMusicSO != null ? info.inLevelMusicSO.name : "",
            minOrderCount = info.minOrderCount,
            maxOrderCount = info.maxOrderCount,
            inLevelAmbiences = ambiences.ToArray(),
            configs = new[]
            {
                ConfigSummary("1p", info.config_1p),
                ConfigSummary("2p", info.config_2p),
                ConfigSummary("3p", info.config_3p),
                ConfigSummary("4p", info.config_4p)
            },
            optionalItems = NamesOf(info.optionalRecipeMatchListItems),
            matchlists = NamesOf(info.includeRecipeMatchLists),
            screenshot = info.screenshot != null ? info.screenshot.name : ""
        };
    }

    /// <summary>单份玩家数配置的紧凑摘要（round/订单/回盘/星级分），供前后对比。</summary>
    private static string ConfigSummary(string label, LevelConfigSetupPerPlayerCountSO cfg)
    {
        if (cfg == null)
            return label + "p:未配置";
        return label + "p:round=" + cfg.roundTime +
               ",order=" + cfg.orderLifeTime +
               ",between=" + cfg.timeBetweenOrders +
               ",plate=" + cfg.plateReturnTime +
               ",surv=" + cfg.survivalTimeMultiplier.ToString("0.##", CultureInfo.InvariantCulture) +
               ",star=" + cfg.m_OneStarScore + "/" + cfg.m_TwoStarScore + "/" + cfg.m_ThreeStarScore + "/" + cfg.m_FourStarScore;
    }

    private static string[] NamesOf(UnityEngine.Object[] objects)
    {
        if (objects == null || objects.Length == 0)
            return new string[0];
        var names = new List<string>();
        foreach (var o in objects)
            if (o != null)
                names.Add(o.name);
        return names.ToArray();
    }

    // ------------------------------------------------------------------ 路径与 IO 工具

    private static string RootDir()
    {
        return Path.Combine(Path.Combine(Application.dataPath, ".."), RootName);
    }

    /// <summary>writeback_history/&lt;set&gt;/&lt;levelId&gt;；优先按场景路径推导，
    /// 场景路径缺失/非法时按 levelInfo 资产路径推导（info-only 端点）。</summary>
    private static string LevelDirFor(string sceneAssetPath, LevelInfoSO levelInfo)
    {
        var set = LayoutEditorLevelInfoResolver.LevelSetFromScenePath(sceneAssetPath);
        var levelId = "";
        if (!string.IsNullOrEmpty(sceneAssetPath))
        {
            var fileName = Path.GetFileNameWithoutExtension(sceneAssetPath);
            levelId = fileName.StartsWith("s_", StringComparison.Ordinal) && fileName.Length > 2
                ? fileName.Substring(2)
                : fileName;
        }
        if ((string.IsNullOrEmpty(set) || string.IsNullOrEmpty(levelId)) && levelInfo != null)
        {
            var infoPath = AssetDatabase.GetAssetPath(levelInfo);
            // Assets/LevelSets/<set>/data/<levelId>/LevelInfo_<levelId>.asset
            var parts = (infoPath ?? "").Replace('\\', '/').Split('/');
            if (parts.Length > 2 && parts[1] == "LevelSets")
            {
                if (string.IsNullOrEmpty(set))
                    set = parts[2];
                if (string.IsNullOrEmpty(levelId) && parts.Length > 4)
                    levelId = parts[4];
            }
        }
        if (string.IsNullOrEmpty(set))
            set = "_misc";
        if (string.IsNullOrEmpty(levelId))
            levelId = "_unknown";
        return Path.Combine(Path.Combine(RootDir(), set), levelId);
    }

    /// <summary>由 LevelInfoSO 反推场景资产路径（info-only 端点无 sceneAssetPath 时）：
    ///  Assets/LevelSets/&lt;set&gt;/scenes/&lt;sceneName&gt;.unity；推导失败返回 null。</summary>
    private static string ScenePathForInfo(LevelInfoSO levelInfo)
    {
        if (levelInfo == null || string.IsNullOrEmpty(levelInfo.sceneName))
            return null;
        var infoPath = AssetDatabase.GetAssetPath(levelInfo);
        var parts = (infoPath ?? "").Replace('\\', '/').Split('/');
        if (parts.Length > 2 && parts[1] == "LevelSets")
            return "Assets/LevelSets/" + parts[2] + "/scenes/" + levelInfo.sceneName + ".unity";
        return null;
    }

    /// <summary>Assets 相对路径 → 绝对路径；空/异常返回 null（调用方容忍缺失）。</summary>
    private static string SafeAbsPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;
        var normalized = assetPath.Replace('\\', '/');
        if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
            return normalized;
        var assetsRoot = Application.dataPath;
        return assetsRoot + normalized.Substring("Assets".Length);
    }

    private static string FindOpenPending(string sceneAssetPath)
    {
        if (string.IsNullOrEmpty(sceneAssetPath))
            return null;
        if (_currentDir != null && _currentScene == sceneAssetPath && Directory.Exists(_currentDir))
            return _currentDir;

        var root = RootDir();
        if (!Directory.Exists(root))
            return null;
        string best = null;
        foreach (var setDir in SafeGetDirs(root))
        {
            foreach (var levelDir in SafeGetDirs(setDir))
            {
                foreach (var dir in SafeGetDirs(levelDir))
                {
                    if (!Path.GetFileName(dir).StartsWith(PendingPrefix, StringComparison.Ordinal))
                        continue;
                    var state = ReadState(dir);
                    if (state == null || state.sceneAssetPath != sceneAssetPath)
                        continue;
                    if (best == null || string.CompareOrdinal(dir, best) > 0)
                        best = dir;
                }
            }
        }
        return best;
    }

    private static WriteBackStateDto ReadState(string pendingDir)
    {
        return ReadJsonFile<WriteBackStateDto>(Path.Combine(pendingDir, "state.json"));
    }

    private static void ClearCurrentIf(string pendingDir)
    {
        if (_currentDir == pendingDir)
        {
            _currentDir = null;
            _currentScene = null;
        }
    }

    private static string[] SafeGetDirs(string dir)
    {
        try
        {
            return Directory.Exists(dir) ? Directory.GetDirectories(dir) : new string[0];
        }
        catch (Exception)
        {
            return new string[0];
        }
    }

    private static void TryDeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[写回历史] 删除目录失败 " + dir + "：" + ex.Message);
        }
    }

    private static void CopyFile(string from, string to)
    {
        // 空路径（info-only 记录无场景路径等）静默跳过；路径非空但文件缺失才告警。
        if (string.IsNullOrEmpty(from))
            return;
        if (!File.Exists(from))
        {
            LayoutEditorLog.LogWarning("[写回历史] 快照源文件缺失：" + from);
            return;
        }
        File.Copy(from, to, true);
    }

    private static int FileSize(string path)
    {
        try
        {
            return File.Exists(path) ? (int)new FileInfo(path).Length : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static void WriteJsonFile(string path, object obj)
    {
        File.WriteAllText(path, JsonUtility.ToJson(obj, true));
    }

    private static T ReadJsonFile<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path))
                return null;
            var text = File.ReadAllText(path);
            return string.IsNullOrEmpty(text) ? null : JsonUtility.FromJson<T>(text);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ 小工具

    private static Dictionary<string, LayoutItemDto> ItemMap(LayoutItemDto[] items)
    {
        var map = new Dictionary<string, LayoutItemDto>();
        FillMap(map, items);
        return map;
    }

    private static Dictionary<string, FloorDto> FloorMap(FloorDto[] floors)
    {
        var map = new Dictionary<string, FloorDto>();
        FillMap(map, floors);
        return map;
    }

    private static void FillMap(Dictionary<string, LayoutItemDto> map, LayoutItemDto[] items)
    {
        if (items == null)
            return;
        foreach (var item in items)
        {
            if (item == null || string.IsNullOrEmpty(item.instanceId) || map.ContainsKey(item.instanceId))
                continue;
            map[item.instanceId] = item;
        }
    }

    private static void FillMap(Dictionary<string, FloorDto> map, FloorDto[] floors)
    {
        if (floors == null)
            return;
        foreach (var floor in floors)
        {
            if (floor == null || string.IsNullOrEmpty(floor.instanceId) || map.ContainsKey(floor.instanceId))
                continue;
            map[floor.instanceId] = floor;
        }
    }

    private static void FillMap(Dictionary<string, LightInfoDto> map, LightInfoDto[] lights)
    {
        if (lights == null)
            return;
        foreach (var light in lights)
        {
            if (light == null || string.IsNullOrEmpty(light.hierarchyPath) || map.ContainsKey(light.hierarchyPath))
                continue;
            map[light.hierarchyPath] = light;
        }
    }

    private static string NameOf(LayoutItemDto item)
    {
        if (item == null)
            return "?";
        return !string.IsNullOrEmpty(item.displayName) ? item.displayName : item.hierarchyPath;
    }

    private static string NameOf(FloorDto floor)
    {
        if (floor == null)
            return "?";
        return !string.IsNullOrEmpty(floor.displayName) ? floor.displayName : floor.hierarchyPath;
    }

    private static string FormatPosition(LayoutItemDto item)
    {
        var p = item.worldPosition != null ? item.worldPosition : item.localPosition;
        if (p == null)
            return "?";
        return string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##},{2:0.##}", p.x, p.y, p.z);
    }

    private static string[] AddedNames(string[] before, string[] after)
    {
        if (after == null || after.Length == 0)
            return new string[0];
        var set = new HashSet<string>(before ?? new string[0]);
        var result = new List<string>();
        foreach (var name in after)
            if (!set.Contains(name))
                result.Add(name);
        return result.ToArray();
    }
}

// ---------------------------------------------------------------------- 持久化 DTO（JsonUtility，全字段无属性）

[Serializable]
public class WriteBackStateDto
{
    public string sceneAssetPath;
    public string infoAssetPath;
    public string beginTimeText;
    public string[] endpoints;
}

[Serializable]
public class WriteBackSnapshotDto
{
    public string sceneAssetPath;
    public LayoutItemDto[] items;
    public FloorDto[] floors;
    public CameraInfoDto cameraInfo;
    public LightInfoDto[] lights;
}

[Serializable]
public class WriteBackInfoSummaryDto
{
    public string assetPath;
    public string levelName;
    public string levelNameZH;
    public string sceneName;
    public string[] recipes;
    public string[] allIngredients;
    public string[] dependencies;
    public string onDeathEffect;
    public string[] audioDirectories;
    public string inLevelMusic;
    public int minOrderCount;
    public int maxOrderCount;
    public string[] inLevelAmbiences;
    public string[] configs;
    public string[] optionalItems;
    public string[] matchlists;
    public string screenshot;
}

[Serializable]
public class WriteBackMetaDto
{
    public string beginTime;
    public string finalizeTime;
    public string sceneAssetPath;
    public string infoAssetPath;
    public string[] endpoints;
    public int itemsBefore;
    public int itemsAfter;
    public int floorsBefore;
    public int floorsAfter;
    public bool semanticAvailable;
}

[Serializable]
public class WriteBackDiffDto
{
    public string recordedAt;
    public string scenePath;
    public bool semanticAvailable;
    public DiffEntryDto[] itemsAdded;
    public DiffEntryDto[] itemsRemoved;
    public ItemMoveDto[] itemsMoved;
    public DiffEntryDto[] itemsChanged;
    /** 重编号：仅 instanceId 变（如恢复→写回后全量换 id），内容未变，不计入变动。 */
    public DiffEntryDto[] itemsReid;
    public int itemsUnchanged;
    public DiffEntryDto[] floorsAdded;
    public DiffEntryDto[] floorsRemoved;
    public DiffEntryDto[] floorsChanged;
    public int floorsUnchanged;
    public bool cameraChanged;
    public string cameraDetail;
    public DiffEntryDto[] lightsAdded;
    public DiffEntryDto[] lightsRemoved;
    public DiffEntryDto[] lightsChanged;
    public int lightsUnchanged;
    public WriteBackInfoDiffDto info;
    public int sceneBytesBefore;
    public int sceneBytesAfter;
    public int infoBytesBefore;
    public int infoBytesAfter;
}

[Serializable]
public class DiffEntryDto
{
    public string id;
    public string name;
    /** 变更分类（config | prefab | hierarchy；reid 条目为 ["reid"]）。 */
    public string[] kinds;
    /** 变更明细（前 3 条「字段: 旧 → 新」，中文标签优先）。 */
    public string detail;
}

[Serializable]
public class ItemMoveDto
{
    public string id;
    public string name;
    /** moved | rotated | scaled（前端据此选徽章颜色与文案）。 */
    public string kind;
    public string fromPosition;
    public string toPosition;
}

[Serializable]
public class WriteBackHistoryListDto
{
    public WriteBackHistoryItemDto[] items;
}

[Serializable]
public class WriteBackHistoryItemDto
{
    public string record;
    public string beginTime;
    public string finalizeTime;
    public string[] endpoints;
    public bool semanticAvailable;
    public int itemsBefore;
    public int itemsAfter;
    public int floorsBefore;
    public int floorsAfter;
    public int itemsAdded;
    public int itemsRemoved;
    public int itemsMoved;
    public int itemsChanged;
    public int floorsAdded;
    public int floorsRemoved;
    public int floorsChanged;
    public bool cameraChanged;
    public bool infoMissing;
    public int sceneBytesBefore;
    public int sceneBytesAfter;
    public int infoBytesBefore;
    public int infoBytesAfter;
}

[Serializable]
public class WriteBackHistoryDetailDto
{
    public string record;
    public WriteBackMetaDto meta;
    public WriteBackDiffDto diff;
    /** 写回前/后完整布局文档是否存在（旧记录只有轻量语义快照，不可恢复到画布）。 */
    public bool canRestoreBefore;
    public bool canRestoreAfter;
}

[Serializable]
public class WriteBackInfoDiffDto
{
    public bool missing;
    public string[] recipesAdded;
    public string[] recipesRemoved;
    public string[] ingredientsAdded;
    public string[] ingredientsRemoved;
    public string[] dependenciesAdded;
    public string[] dependenciesRemoved;
    public string[] audioAdded;
    public string[] audioRemoved;
    public string[] ambiencesAdded;
    public string[] ambiencesRemoved;
    public string[] optionalItemsAdded;
    public string[] optionalItemsRemoved;
    public string[] matchlistsAdded;
    public string[] matchlistsRemoved;
    public string[] configsBefore;
    public string[] configsAfter;
    public string screenshotBefore;
    public string screenshotAfter;
    public string deathEffectBefore;
    public string deathEffectAfter;
    public string levelNameBefore;
    public string levelNameAfter;
    public string minMaxOrdersBefore;
    public string minMaxOrdersAfter;
}
