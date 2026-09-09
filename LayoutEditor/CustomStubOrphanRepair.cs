using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// CustomStub 孤儿脚本引用修复（迁移损坏）。
///
/// 背景（2026-09-09 排查实证）：老烘焙时代（Assembly-CSharp 补丁期）曾把
/// TimedCookingSwitch 等组件【无差别批量挂载】到几乎所有放置物上（Counter/地板/
/// 墙/Player/美术件，全默认值），这些组件的脚本 GUID 在 stub 目录子文件夹化迁移
/// 后失效，场景里留下大量 Missing Script 孤儿组件（全项目 3268 个，单场景最多
/// 745 个）。真机不炸（Missing Script 被跳过）但纯属垃圾数据。
///
/// 本工具按【组件自身字段签名】分类处置（不按 guid——同一孤儿 guid 下存在
/// 带字段/无字段两种变体）：
///  - 已识别类型 + 宿主是合法载体（TimedCookingSwitch 仅灶台：源 prefab 路径含
///    cooking_region/floorburner）→ guid 替换为该集当前 stub guid（复活并保留配置）；
///  - 已识别类型但宿主非载体（批量误挂僵尸）、无字段空脚本 → 删除组件块
///   （连带清理无引用的 stripped GameObject 块 / 普通 GameObject 的 m_Component 条目）；
///  - 带真实配置的未知签名（如旧 LayoutRuntimeSwitchLink：m_targetRoots+m_trigger）
///    → 只报告不动（删除会丢真实配置）。
/// 同趟检测缺失 prefab 实例（Prefab 文档源 guid 不可解析）→ 只列入报告，
/// 实际清理用 LayoutEditorSceneRepair.RemoveBrokenPrefabInstances（需打开场景）。
/// 安全：打开中的目标场景先保存、修复后从磁盘重开；写入前备份到
/// writeback_history/orphan_repair/；先扫描出报告，确认后才执行。
/// </summary>
public static class CustomStubOrphanRepair
{
    private const string BackupRoot = "writeback_history/orphan_repair";

    // 已识别签名 → CustomStub 类型（字段名集合精确匹配）
    private static readonly string[] TimedSwitchFields = { "m_enabled", "m_onSeconds", "m_offSeconds", "m_startOn" };
    private static readonly string[] SwitchLinkFields = { "m_targetRoots", "m_trigger" };

    // 合法载体判定（仅这些类型有载体要求；其余已识别类型一律复活）
    private static readonly string[] BurnerPathHints = { "cooking_region", "floorburner" };

    private static readonly HashSet<string> BaseFields = new HashSet<string>
    {
        "m_ObjectHideFlags", "m_PrefabParentObject", "m_PrefabInternal", "m_GameObject",
        "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier"
    };

    private static readonly HashSet<string> BuiltinGuids = new HashSet<string>
    {
        "0000000000000000f000000000000000", "0000000000000000e000000000000000"
    };

    [MenuItem("Layout Editor/CustomStub/修复场景孤儿脚本引用（迁移损坏）")]
    public static void Run()
    {
        try
        {
            // 打开中的目标场景：先保存（防脏数据丢失）、非激活的关闭、激活的记录待修复后
            // 从磁盘重开——文本改写与 Unity 内存副本不再打架（2026-09-09：原拒绝执行，
            // 用户要求直接修复）。
            var reopenPath = PrepareOpenTargetScenes();

            var guidIndex = BuildGuidIndex();
            var plans = new List<ScenePlan>();
            foreach (var scenePath in AllLevelSetScenes())
            {
                var plan = ScanScene(scenePath, guidIndex);
                if (plan != null && plan.HasWork)
                    plans.Add(plan);
            }

            if (plans.Count == 0)
            {
                EditorUtility.DisplayDialog("孤儿脚本引用修复", "全部关卡集场景扫描完毕：没有发现孤儿脚本引用。", "好");
                return;
            }

            var summary = BuildSummary(plans);
            LayoutEditorLog.Log("[孤儿修复] 扫描报告:\n" + summary);

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var actionable = false;
            foreach (var p in plans)
            {
                if (p.CountOf(0) + p.CountOf(1) > 0)
                {
                    actionable = true;
                    break;
                }
            }
            if (!actionable)
            {
                // 全是「仅报告」项（含缺失 prefab 实例提示）——不写场景，只落报告
                var reportOnlyPath = WriteReport(stamp, summary, null);
                EditorUtility.DisplayDialog("孤儿脚本引用修复",
                    "扫描完成：没有需要修复的孤儿组件（详见报告）。\n\n报告: " + reportOnlyPath, "好");
                return;
            }

            if (!EditorUtility.DisplayDialog("孤儿脚本引用修复（迁移损坏）",
                summary + "\n\n写入前自动备份到 " + BackupRoot + "/<时间戳>/。确认执行？",
                "执行修复", "取消"))
                return;

            var report = new StringBuilder();
            foreach (var plan in plans)
                ExecuteScene(plan, guidIndex, stamp, report);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 激活场景原本开着 → 从磁盘重开，载入修复后的版本（避免内存旧副本覆写）
            if (reopenPath != null)
                EditorSceneManager.OpenScene(reopenPath, OpenSceneMode.Single);

            var reportPath = WriteReport(stamp, summary, report);
            LayoutEditorLog.Log("[孤儿修复] 执行完成，报告: " + reportPath);
            EditorUtility.DisplayDialog("孤儿脚本引用修复",
                "修复完成。\n\n报告: " + reportPath + "\n\n受影响关卡集需重新导出才会进游戏包。", "好");
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[孤儿修复] 执行异常: " + ex);
            EditorUtility.DisplayDialog("孤儿脚本引用修复", "执行异常（详见日志）:\n" + ex.Message, "知道了");
        }
    }

    // ============================== 数据 ==============================

    private class Orphan
    {
        public string CompFileId;
        public string ScriptGuid;
        public string GoFileId;
        public string Kind;       // TimedCookingSwitch / LayoutRuntimeSwitchLink / Empty / Unknown:...
        public string HostPath;   // 宿主源 prefab 路径（非 prefab 实例为 null）
        public int Action;        // 0=删除 1=复活 2=仅报告
        public string NewGuid;    // Action=1 时有效
    }

    private class ScenePlan
    {
        public string Path;
        public List<Orphan> Orphans = new List<Orphan>();
        public int MissingPrefabInstances;

        public bool HasWork
        {
            get
            {
                foreach (var o in Orphans)
                    if (o.Action != 2)
                        return true;
                return Orphans.Count > 0 || MissingPrefabInstances > 0;
            }
        }

        public int CountOf(int action)
        {
            var n = 0;
            foreach (var o in Orphans)
                if (o.Action == action)
                    n++;
            return n;
        }
    }

    private class Doc
    {
        public int ClassId;
        public string FileId;
        public bool Stripped;
        public string FullText;   // 含头部行的完整文档块
        public bool Removed;
        public string NewText;    // 编辑后的完整文档块（null = 未改）
    }

    // ============================== 扫描 ==============================

    /// <summary>处理打开中的 LevelSets 目标场景：脏的先保存；非激活的关闭（从层级窗口
    /// 移除，磁盘文件不动）；激活的返回其路径（修复后从磁盘重开）。无目标场景打开返回 null。</summary>
    private static string PrepareOpenTargetScenes()
    {
        var active = EditorSceneManager.GetActiveScene();
        string reopenPath = null;
        var toClose = new List<UnityEngine.SceneManagement.Scene>();
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var s = EditorSceneManager.GetSceneAt(i);
            if (!s.IsValid() || !s.path.StartsWith("Assets/LevelSets/", StringComparison.Ordinal))
                continue;
            if (s.isDirty)
                EditorSceneManager.SaveScene(s);
            if (s == active)
                reopenPath = s.path;
            else
                toClose.Add(s);
        }
        foreach (var s in toClose)
            EditorSceneManager.CloseScene(s, true);
        return reopenPath;
    }

    private static Dictionary<string, string> BuildGuidIndex()
    {
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var meta in Directory.GetFiles("Assets", "*.meta", SearchOption.AllDirectories))
        {
            string head;
            using (var r = new StreamReader(meta))
            {
                var buf = new char[400];
                var n = r.Read(buf, 0, buf.Length);
                head = new string(buf, 0, n);
            }
            var m = Regex.Match(head, @"^guid:\s*([0-9a-f]{32})", RegexOptions.Multiline);
            if (m.Success)
                index[m.Groups[1].Value] = meta.Substring(0, meta.Length - ".meta".Length);
        }
        return index;
    }

    private static List<string> AllLevelSetScenes()
    {
        var list = new List<string>();
        var root = "Assets/LevelSets";
        if (!Directory.Exists(root))
            return list;
        foreach (var setDir in Directory.GetDirectories(root))
        {
            var scenesDir = Path.Combine(setDir, "scenes");
            if (!Directory.Exists(scenesDir))
                continue;
            list.AddRange(Directory.GetFiles(scenesDir, "*.unity", SearchOption.AllDirectories));
        }
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    /// <summary>扫描单场景：找孤儿组件、分类、定处置。纯只读。</summary>
    private static ScenePlan ScanScene(string scenePath, Dictionary<string, string> guidIndex)
    {
        var text = File.ReadAllText(scenePath);
        var docs = SplitDocs(text);

        // 第一趟：prefab 源 / stripped GO 归属
        var prefabSrc = new Dictionary<string, string>(StringComparer.Ordinal);      // prefab 文档 fileID → 源 prefab guid
        var strippedGoPrefab = new Dictionary<string, string>(StringComparer.Ordinal); // stripped GO fileID → prefab 文档 fileID
        var missingPrefabInstances = 0;
        foreach (var d in docs)
        {
            if (d.ClassId == 1001)
            {
                var src = Regex.Match(d.FullText, @"m_(?:SourcePrefab|ParentPrefab):\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})");
                if (src.Success)
                {
                    prefabSrc[d.FileId] = src.Groups[1].Value;
                    // 缺失 prefab 实例（报告用）
                    if (!guidIndex.ContainsKey(src.Groups[1].Value) && !BuiltinGuids.Contains(src.Groups[1].Value))
                        missingPrefabInstances++;
                }
            }
            else if (d.ClassId == 1 && d.Stripped)
            {
                var pi = Regex.Match(d.FullText, @"m_PrefabInternal:\s*\{fileID:\s*(\d+)\}");
                if (pi.Success)
                    strippedGoPrefab[d.FileId] = pi.Groups[1].Value;
            }
        }

        ScenePlan plan = null;
        foreach (var d in docs)
        {
            if (d.ClassId != 114)
                continue;
            var sm = Regex.Match(d.FullText, @"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-f]{32})");
            if (!sm.Success)
                continue;
            var guid = sm.Groups[1].Value;
            if (guidIndex.ContainsKey(guid) || BuiltinGuids.Contains(guid))
                continue;

            var om = Regex.Match(d.FullText, @"m_GameObject:\s*\{fileID:\s*(\d+)\}");
            var orphan = new Orphan();
            orphan.CompFileId = d.FileId;
            orphan.ScriptGuid = guid;
            orphan.GoFileId = om.Success ? om.Groups[1].Value : null;
            orphan.Kind = Classify(ExtractFieldNames(d.FullText));

            // 宿主源 prefab 路径
            string prefabFileId;
            string srcGuid;
            string hostPath = null;
            if (orphan.GoFileId != null && strippedGoPrefab.TryGetValue(orphan.GoFileId, out prefabFileId)
                && prefabSrc.TryGetValue(prefabFileId, out srcGuid))
                guidIndex.TryGetValue(srcGuid, out hostPath);
            orphan.HostPath = hostPath;

            DecideAction(orphan, scenePath);
            plan = EnsurePlan(ref plan, scenePath);
            plan.Orphans.Add(orphan);
        }
        if (plan != null)
            plan.MissingPrefabInstances = missingPrefabInstances;
        else if (missingPrefabInstances > 0)
        {
            plan = EnsurePlan(ref plan, scenePath);
            plan.MissingPrefabInstances = missingPrefabInstances;
        }
        return plan;
    }

    private static ScenePlan EnsurePlan(ref ScenePlan plan, string scenePath)
    {
        if (plan == null)
        {
            plan = new ScenePlan();
            plan.Path = scenePath;
        }
        return plan;
    }

    /// <summary>提取组件自定义字段名（排除 MonoBehaviour 基础字段）。</summary>
    private static List<string> ExtractFieldNames(string docText)
    {
        var names = new List<string>();
        foreach (Match m in Regex.Matches(docText, @"^  (m_\w+):", RegexOptions.Multiline))
        {
            var n = m.Groups[1].Value;
            if (!BaseFields.Contains(n))
                names.Add(n);
        }
        return names;
    }

    private static string Classify(List<string> fields)
    {
        if (SignatureEquals(fields, TimedSwitchFields))
            return "TimedCookingSwitch";
        if (SignatureEquals(fields, SwitchLinkFields))
            return "LayoutRuntimeSwitchLink(旧开关联动,含真实配置)";
        if (fields.Count == 0)
            return "Empty(空字段)";
        return "Unknown(" + string.Join(",", fields.ToArray()) + ")";
    }

    private static bool SignatureEquals(List<string> fields, string[] signature)
    {
        if (fields.Count != signature.Length)
            return false;
        foreach (var f in signature)
            if (!fields.Contains(f))
                return false;
        return true;
    }

    /// <summary>处置决策：合法载体→复活（换当前 guid）；僵尸/空脚本→删除；未知→仅报告。</summary>
    private static void DecideAction(Orphan orphan, string scenePath)
    {
        orphan.Action = 2; // 默认仅报告（未知类型绝不自动动）
        if (orphan.Kind == "TimedCookingSwitch")
        {
            if (IsBurnerCarrier(orphan.HostPath))
            {
                var newGuid = LoadStubScriptGuid(scenePath, "Switch/TimedCookingSwitch.cs");
                if (newGuid != null)
                {
                    orphan.Action = 1;
                    orphan.NewGuid = newGuid;
                }
                return;
            }
            orphan.Action = 0; // 批量误挂僵尸
            return;
        }
        if (orphan.Kind == "Empty(空字段)")
        {
            orphan.Action = 0;
            return;
        }
        // LayoutRuntimeSwitchLink / Unknown：仅报告
    }

    private static bool IsBurnerCarrier(string hostPath)
    {
        if (hostPath == null)
            return false;
        foreach (var hint in BurnerPathHints)
            if (hostPath.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        return false;
    }

    /// <summary>读场景所属关卡集 stub 里某脚本的当前 guid（复活替换目标）。</summary>
    private static string LoadStubScriptGuid(string scenePath, string stubRelative)
    {
        // scenePath = Assets/LevelSets/<set>/scenes/xxx.unity
        var parts = scenePath.Split('/');
        if (parts.Length < 3)
            return null;
        var setRoot = parts[0] + "/" + parts[1] + "/" + parts[2];
        var meta = setRoot + "/stub/" + stubRelative + ".meta";
        if (!File.Exists(meta))
            return null;
        var m = Regex.Match(File.ReadAllText(meta), @"^guid:\s*([0-9a-f]{32})", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value : null;
    }

    // ============================== 执行 ==============================

    private static void ExecuteScene(ScenePlan plan, Dictionary<string, string> guidIndex, string stamp, StringBuilder report)
    {
        var text = File.ReadAllText(plan.Path);
        var preambleLen = 0;
        var docs = SplitDocs(text, out preambleLen);

        var removedCompIds = new HashSet<string>(StringComparer.Ordinal);
        var hostGoOfRemoved = new Dictionary<string, string>(StringComparer.Ordinal); // compId → goId
        foreach (var orphan in plan.Orphans)
        {
            var doc = FindDoc(docs, orphan.CompFileId);
            if (doc == null)
                continue;
            if (orphan.Action == 1)
            {
                // 复活：仅替换 m_Script 行的 guid（保留全部配置值）
                doc.NewText = Regex.Replace(doc.FullText,
                    @"(m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*)" + orphan.ScriptGuid,
                    "${1}" + orphan.NewGuid);
                report.AppendLine("复活 " + orphan.Kind + " " + orphan.CompFileId + " @ " + (orphan.HostPath ?? "(场景对象)"));
            }
            else if (orphan.Action == 0)
            {
                // 安全性复检：除自身与宿主 GO 的 m_Component 条目外不得有其他引用
                if (CountFileIdRefs(docs, orphan.CompFileId, orphan.CompFileId, orphan.GoFileId) > 0)
                {
                    report.AppendLine("跳过（存在额外引用，改列报告） " + orphan.Kind + " " + orphan.CompFileId);
                    continue;
                }
                doc.Removed = true;
                removedCompIds.Add(orphan.CompFileId);
                if (orphan.GoFileId != null)
                    hostGoOfRemoved[orphan.CompFileId] = orphan.GoFileId;
                report.AppendLine("删除 " + orphan.Kind + " " + orphan.CompFileId + " @ " + (orphan.HostPath ?? "(场景对象)"));
            }
            else
            {
                report.AppendLine("仅报告 " + orphan.Kind + " " + orphan.CompFileId + " guid=" + orphan.ScriptGuid
                    + " @ " + (orphan.HostPath ?? "(场景对象)"));
            }
        }

        if (removedCompIds.Count == 0 && !HasEdits(docs))
            return;

        // 普通 GameObject 宿主：从 m_Component 列表移除条目
        foreach (var pair in hostGoOfRemoved)
        {
            var goDoc = FindDoc(docs, pair.Value);
            if (goDoc == null || goDoc.ClassId != 1 || goDoc.Stripped)
                continue; // stripped GO 的组件表在 prefab 侧，场景里无条目可清
            var src = goDoc.NewText ?? goDoc.FullText;
            var edited = Regex.Replace(src,
                @"^[ \t]*-[ \t]*(?:component:|\d+:)[ \t]*\{fileID:[ \t]*" + pair.Key + @"[ \t]*\}[ \t]*\r?\n",
                "", RegexOptions.Multiline);
            goDoc.NewText = edited;
        }

        // stripped GO 连带清理：删除的组件若挂在 stripped GO 上，且该 GO 不再被任何
        // 保留文档引用 → 一并删除该 stripped GO 块
        var candidateStripped = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in hostGoOfRemoved)
            candidateStripped.Add(pair.Value);
        foreach (var goId in candidateStripped)
        {
            var goDoc = FindDoc(docs, goId);
            if (goDoc == null || goDoc.ClassId != 1 || !goDoc.Stripped)
                continue;
            if (CountFileIdRefs(docs, goId, null, null) > 0)
                continue; // 仍有保留文档引用它
            goDoc.Removed = true;
            report.AppendLine("连带删除 stripped GameObject " + goId);
        }

        // 重组文本
        var sb = new StringBuilder(text.Length);
        sb.Append(text.Substring(0, preambleLen));
        foreach (var d in docs)
        {
            if (d.Removed)
                continue;
            sb.Append(d.NewText ?? d.FullText);
        }

        // 备份后写入
        var backupPath = BackupRoot + "/" + stamp + "/" + plan.Path;
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
        File.Copy(plan.Path, backupPath, true);
        File.WriteAllText(plan.Path, sb.ToString());
    }

    private static bool HasEdits(List<Doc> docs)
    {
        foreach (var d in docs)
            if (d.NewText != null)
                return true;
        return false;
    }

    /// <summary>统计保留文档中对某 fileID 的引用数（排除 excludeCompId 自身文档与
    /// excludeGoId 宿主文档——宿主的 m_Component 条目是预期引用，随后会被清掉）。</summary>
    private static int CountFileIdRefs(List<Doc> docs, string fileId, string excludeCompId, string excludeGoId)
    {
        var n = 0;
        foreach (var d in docs)
        {
            if (d.Removed)
                continue;
            if (excludeCompId != null && d.FileId == excludeCompId)
                continue;
            if (excludeGoId != null && d.FileId == excludeGoId)
                continue;
            var body = d.NewText ?? d.FullText;
            if (Regex.IsMatch(body, @"fileID:\s*" + fileId + @"\b"))
                n++;
        }
        return n;
    }

    private static Doc FindDoc(List<Doc> docs, string fileId)
    {
        foreach (var d in docs)
            if (d.FileId == fileId)
                return d;
        return null;
    }

    // ============================== YAML 文档切分 ==============================

    private static List<Doc> SplitDocs(string text)
    {
        int preambleLen;
        return SplitDocs(text, out preambleLen);
    }

    private static List<Doc> SplitDocs(string text, out int preambleLen)
    {
        var docs = new List<Doc>();
        var matches = Regex.Matches(text, @"^--- !u!(\d+) &(\d+)( stripped)?[ \t]*\r?$", RegexOptions.Multiline);
        preambleLen = matches.Count > 0 ? matches[0].Index : text.Length;
        for (int i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            var d = new Doc();
            d.ClassId = int.Parse(matches[i].Groups[1].Value);
            d.FileId = matches[i].Groups[2].Value;
            d.Stripped = matches[i].Groups[3].Success;
            d.FullText = text.Substring(start, end - start);
            docs.Add(d);
        }
        return docs;
    }

    // ============================== 报告 ==============================

    /// <summary>落报告文件（目录可能尚不存在——本轮全是仅报告项时没有备份动作），返回路径。</summary>
    private static string WriteReport(string stamp, string summary, StringBuilder executionDetail)
    {
        var path = BackupRoot + "/" + stamp + "/report.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, summary
            + (executionDetail != null ? "\n\n==== 执行明细 ====\n" + executionDetail : ""));
        return path;
    }

    private static string BuildSummary(List<ScenePlan> plans)
    {
        var sb = new StringBuilder();
        var totalRemove = 0;
        var totalRevive = 0;
        var totalReport = 0;
        var totalMissingPrefab = 0;
        foreach (var p in plans)
        {
            var rm = p.CountOf(0);
            var rv = p.CountOf(1);
            var rp = p.CountOf(2);
            totalRemove += rm;
            totalRevive += rv;
            totalReport += rp;
            totalMissingPrefab += p.MissingPrefabInstances;
            sb.AppendLine(p.Path.Replace("Assets/LevelSets/", "")
                + "：删除 " + rm + " / 复活 " + rv + " / 仅报告 " + rp
                + (p.MissingPrefabInstances > 0 ? " / 缺失 prefab 实例 " + p.MissingPrefabInstances : ""));
            // 仅报告项明细（含真实配置的必须可见）
            foreach (var o in p.Orphans)
            {
                if (o.Action == 2)
                    sb.AppendLine("    [仅报告] " + o.Kind + " guid=" + o.ScriptGuid
                        + " @ " + (o.HostPath ?? "(场景对象)"));
            }
        }
        sb.Insert(0, "扫描完成：共 " + plans.Count + " 个场景有孤儿引用。\n总计：删除 "
            + totalRemove + " / 复活 " + totalRevive + " / 仅报告 " + totalReport
            + (totalMissingPrefab > 0 ? " / 缺失 prefab 实例 " + totalMissingPrefab + " 处" : "") + "\n\n");
        return sb.ToString();
    }
}
