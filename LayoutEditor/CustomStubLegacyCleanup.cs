using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 清理旧版 Stub 残留（统一单程序集重构 v2.0 的一次性迁移工具）。
///
/// 旧每集体系（Stub_&lt;set&gt; 副本 + 每集 runtime bundle）已被统一
/// WebCustomStubRuntime + 依赖包 OC2DIYLevelRuntimeWLoader 取代。本菜单扫描并清理：
///  1. 关卡集源码残留：Assets/LevelSets/&lt;set&gt;/stub/（含 Stub_&lt;set&gt;.asmdef /
///     Stub_&lt;set&gt;.dll.bytes / 镜像母本的 .cs 副本）；
///  2. 旧构建产物：Assets/AssetBundles/&lt;set&gt;/runtime（每集 runtime bundle +
///     .manifest）——统一运行时改由 webcustomstub_runtime 分发，这些已无用；
///  3. web/public 旧 loader：layout-editor/web/public/OC2LevelRuntimeLoader.dll
///     （新版为 Loader.dll）；
///  4. 场景残留检测（只报告不改）：仍引用已删除 per-set 脚本 GUID 的 Missing Script
///     ——提示用 GUID 迁移工具/重新打开修复。
///
/// 先弹「扫描报告」确认，再执行删除；删除走 AssetDatabase（GUID 正常废弃）。
/// </summary>
public static class CustomStubLegacyCleanup
{
    private const string LevelSetsRoot = "Assets/LevelSets";
    private const string BundlesRoot = "Assets/AssetBundles";
    private const string OldLoaderPublic = "layout-editor/web/public/OC2LevelRuntimeLoader.dll";

    [MenuItem("Layout Editor/CustomStub/清理旧版 Stub 残留", false, 200)]
    public static void CleanupLegacy()
    {
        var stubDirs = new List<string>();
        var bundleRuntimes = new List<string>();     // 绝对路径
        var oldLoaders = new List<string>();          // 绝对路径

        var projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');

        // 1. 关卡集源码残留 stub/ 目录
        if (AssetDatabase.IsValidFolder(LevelSetsRoot))
        {
            foreach (var setFolder in AssetDatabase.GetSubFolders(LevelSetsRoot))
            {
                var stubDir = setFolder + "/stub";
                if (AssetDatabase.IsValidFolder(stubDir))
                    stubDirs.Add(stubDir);
            }
        }

        // 2. 旧构建产物 <set>/runtime（+ .manifest）
        var absBundles = projectRoot + "/" + BundlesRoot;
        if (Directory.Exists(absBundles))
        {
            foreach (var dir in Directory.GetDirectories(absBundles))
            {
                var runtime = Path.Combine(dir, "runtime");
                if (File.Exists(runtime))
                    bundleRuntimes.Add(runtime);
            }
        }

        // 3. web/public 旧 loader
        var oldLoaderAbs = projectRoot + "/" + OldLoaderPublic;
        if (File.Exists(oldLoaderAbs))
            oldLoaders.Add(oldLoaderAbs);

        // 4. 场景残留 Missing Script（只报告）
        var missingScenes = ScanScenesForMissingScripts();

        if (stubDirs.Count == 0 && bundleRuntimes.Count == 0 && oldLoaders.Count == 0)
        {
            var extra = missingScenes.Count > 0
                ? "\n\n⚠ 但检测到 " + missingScenes.Count + " 个场景含 Missing Script（可能是旧 stub 引用未迁移）：\n"
                  + string.Join("\n", missingScenes.ToArray())
                  + "\n\n请用「修复场景孤儿脚本引用」或重新打开这些场景处理。"
                : "";
            EditorUtility.DisplayDialog("清理旧版 Stub",
                "未发现旧版 Stub 源码/产物残留（已是统一运行时 v2.0 结构）。" + extra, "确定");
            return;
        }

        var sb = new StringBuilder();
        sb.Append("将删除以下旧版 Stub 残留：\n\n");
        if (stubDirs.Count > 0)
        {
            sb.Append("● 关卡集 stub 源码目录（" + stubDirs.Count + "）：\n");
            foreach (var d in stubDirs) sb.Append("   " + d + "\n");
            sb.Append("\n");
        }
        if (bundleRuntimes.Count > 0)
        {
            sb.Append("● 旧每集 runtime 构建产物（" + bundleRuntimes.Count + "）：\n");
            foreach (var f in bundleRuntimes) sb.Append("   " + Rel(projectRoot, f) + "（+ .manifest）\n");
            sb.Append("\n");
        }
        if (oldLoaders.Count > 0)
        {
            sb.Append("● 旧 loader（" + oldLoaders.Count + "）：\n");
            foreach (var f in oldLoaders) sb.Append("   " + Rel(projectRoot, f) + "（新版为 Loader.dll）\n");
            sb.Append("\n");
        }
        if (missingScenes.Count > 0)
        {
            sb.Append("⚠ 另检测到 " + missingScenes.Count + " 个场景含 Missing Script（仅提示，不自动改）：\n");
            foreach (var s in missingScenes) sb.Append("   " + s + "\n");
            sb.Append("\n");
        }
        sb.Append("删除不可撤销（走 AssetDatabase / File 删除），确定继续？");

        if (!EditorUtility.DisplayDialog("清理旧版 Stub", sb.ToString(), "删除", "取消"))
            return;

        var deleted = 0;
        var failed = 0;

        foreach (var d in stubDirs)
        {
            if (AssetDatabase.DeleteAsset(d)) deleted++;
            else { failed++; Debug.LogWarning("[CustomStub] 删除失败: " + d); }
        }

        foreach (var f in bundleRuntimes)
        {
            try
            {
                File.Delete(f);
                var manifest = f + ".manifest";
                if (File.Exists(manifest)) File.Delete(manifest);
                deleted++;
            }
            catch (Exception ex) { failed++; Debug.LogWarning("[CustomStub] 删除失败 " + f + ": " + ex.Message); }
        }

        foreach (var f in oldLoaders)
        {
            try { File.Delete(f); deleted++; }
            catch (Exception ex) { failed++; Debug.LogWarning("[CustomStub] 删除失败 " + f + ": " + ex.Message); }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("清理旧版 Stub",
            "完成：删除 " + deleted + " 项" + (failed > 0 ? "，失败 " + failed + " 项（详见 Console）" : "")
            + (missingScenes.Count > 0 ? "\n\n⚠ " + missingScenes.Count + " 个场景仍含 Missing Script，请另行处理。" : ""),
            "确定");
    }

    private static string Rel(string projectRoot, string abs)
    {
        var p = abs.Replace('\\', '/');
        var root = projectRoot.Replace('\\', '/') + "/";
        return p.StartsWith(root, StringComparison.Ordinal) ? p.Substring(root.Length) : p;
    }

    /// <summary>扫描所有关卡集场景，找含 Missing MonoBehaviour（m_Script guid 失效）的场景。
    /// 只做文本级快速检测（不打开场景）：YAML 里 MonoBehaviour 块 m_Script 的 guid
    /// 在 AssetDatabase 中查不到对应资产即视为可疑。</summary>
    private static List<string> ScanScenesForMissingScripts()
    {
        var result = new List<string>();
        try
        {
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { LevelSetsRoot });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(path))
                    continue;
                var abs = Application.dataPath.Replace("Assets", "") + path;
                if (!File.Exists(abs))
                    continue;
                if (SceneHasDanglingScript(abs))
                    result.Add(path);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomStub] 场景 Missing Script 扫描异常: " + ex.Message);
        }
        return result;
    }

    private static bool SceneHasDanglingScript(string absScenePath)
    {
        try
        {
            var text = File.ReadAllText(absScenePath);
            var idx = 0;
            const string marker = "m_Script: {fileID: 11500000, guid: ";
            while (true)
            {
                var at = text.IndexOf(marker, idx, StringComparison.Ordinal);
                if (at < 0)
                    break;
                var start = at + marker.Length;
                var end = text.IndexOf(',', start);
                if (end < 0)
                    break;
                var guid = text.Substring(start, end - start).Trim();
                idx = end;
                if (guid.Length != 32)
                    continue;
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                    return true; // guid 查不到资产 = Missing Script
            }
        }
        catch { }
        return false;
    }
}
