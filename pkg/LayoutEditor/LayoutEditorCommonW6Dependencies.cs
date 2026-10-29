using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// commonW6 去本地化后的依赖同步：
///   SO 的 bundleName/assetPath 现在直接指向游戏原 bundle（bundle163/167/428…），
///   但 LevelInfoSO.dependencies 里通常没有这些 bundle。打开场景 / 导出 prepare 时
///   扫描场景引用（wrapper prefab + PseudoPrefabSO 覆写），把实际存在的目标 bundle
///   补进 dependencies，避免 PseudoPrefabManager.GetAssetBundle 抛 KeyNotFoundException。
/// 只写实际存在于 StreamingAssets/Windows 的 bundle；SO guid/场景 guid 不变。
/// </summary>
public static class LayoutEditorCommonW6Dependencies
{
    public const string CommonW6Root = "Assets/commonW6";
    public const string CommonW6Bundle = "commonW6";

    private static readonly Regex GuidRegex =
        new Regex(@"guid:\s*([a-f0-9]{32})", RegexOptions.Compiled);
    private static readonly Regex PseudoRefRegex =
        new Regex(@"pseudoPrefabSO:\s*\{[^}]*guid:\s*([a-f0-9]{32})", RegexOptions.Compiled);
    private static readonly Regex MaterialRefRegex =
        new Regex(@"materialSO:\s*\{[^}]*guid:\s*([a-f0-9]{32})", RegexOptions.Compiled);

    /// <summary>同步当前场景对应 LevelInfo 的 commonW6 / 目标 bundle 依赖。返回新增条数。</summary>
    public static int SyncForScene(Scene scene, LevelInfoSO info)
    {
        if (info == null || !scene.IsValid())
            return 0;
        string scenePath = scene.path;
        if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
            return 0;

        var bundles = new HashSet<string>(StringComparer.Ordinal);
        var guids = new HashSet<string>(StringComparer.Ordinal);
        string text;
        try
        {
            text = File.ReadAllText(scenePath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[commonW6] 读取场景失败 " + scenePath + ": " + ex.Message);
            return 0;
        }
        foreach (Match m in GuidRegex.Matches(text))
            guids.Add(m.Groups[1].Value);

        bool referencedCommonW6 = false;
        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (string.IsNullOrEmpty(path))
                continue;
            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith(CommonW6Root + "/", StringComparison.Ordinal))
                referencedCommonW6 = true;

            if (normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                string ptext;
                try { ptext = File.ReadAllText(path); }
                catch { continue; }
                foreach (Match m in PseudoRefRegex.Matches(ptext))
                    AddSoBundle(m.Groups[1].Value, bundles);
                foreach (Match m in MaterialRefRegex.Matches(ptext))
                    AddSoBundle(m.Groups[1].Value, bundles);
            }
            else if (normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                AddSoBundle(g, bundles);
            }
        }

        if (referencedCommonW6 && BundleExists(CommonW6Bundle))
            bundles.Add(CommonW6Bundle);

        // 只登记实际存在的 bundle，缺失的跳过（避免宿主 KeyNotFoundException）。
        var verified = new List<string>();
        foreach (string b in bundles)
        {
            if (b == CommonW6Bundle || BundleExists(b))
                verified.Add(b);
        }
        if (verified.Count == 0)
            return 0;

        var deps = new List<string>(info.dependencies ?? new string[0]);
        int added = 0;
        for (int i = 0; i < verified.Count; i++)
        {
            if (!deps.Contains(verified[i]))
            {
                deps.Add(verified[i]);
                added++;
            }
        }
        if (added == 0)
            return 0;

        Undo.RecordObject(info, "Layout Editor Sync commonW6 Dependencies");
        info.dependencies = deps.ToArray();
        EditorUtility.SetDirty(info);
        AssetDatabase.SaveAssets();
        Debug.Log("[commonW6] 同步依赖 +" + added + "（" + scenePath + "）："
            + string.Join(", ", verified.ToArray()));
        return added;
    }

    /// <summary>导出 prepare 用：按场景路径解析 LevelInfo 后同步。</summary>
    public static int SyncForScenePath(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath))
            return 0;
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != scenePath)
            return 0;
        var info = LayoutEditorLevelInfoResolver.ResolveForScene(scenePath);
        return SyncForScene(scene, info);
    }

    /// <summary>供 LayoutEditorCustomIngredients 的依赖收集复用：
    /// wrapper prefab -> 自身/覆写 PseudoPrefabSO 的 bundleName；SO 资产直接读取。</summary>
    public static void CollectBundleNamesForAsset(string assetPath, HashSet<string> bundles)
    {
        if (string.IsNullOrEmpty(assetPath) || bundles == null)
            return;
        string normalized = assetPath.Replace('\\', '/');
        if (normalized.StartsWith(CommonW6Root + "/", StringComparison.Ordinal)
            && BundleExists(CommonW6Bundle))
            bundles.Add(CommonW6Bundle);
        if (normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            string text;
            try { text = File.ReadAllText(assetPath); }
            catch { return; }
            foreach (Match m in PseudoRefRegex.Matches(text))
                AddSoBundle(m.Groups[1].Value, bundles);
            foreach (Match m in MaterialRefRegex.Matches(text))
                AddSoBundle(m.Groups[1].Value, bundles);
        }
        else if (normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        {
            var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(assetPath);
            if (so != null && !string.IsNullOrEmpty(so.bundleName))
                bundles.Add(so.bundleName);
        }
    }

    public static void CollectBundleNamesForGuid(string guid, HashSet<string> bundles)
    {
        if (string.IsNullOrEmpty(guid) || bundles == null)
            return;
        string path = AssetDatabase.GUIDToAssetPath(guid);
        CollectBundleNamesForAsset(path, bundles);
    }

    private static void AddSoBundle(string guid, HashSet<string> bundles)
    {
        if (string.IsNullOrEmpty(guid))
            return;
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            return;
        var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
        if (so == null || string.IsNullOrEmpty(so.bundleName))
            return;
        bundles.Add(so.bundleName);
    }

    private static bool BundleExists(string bundleName)
    {
        if (string.IsNullOrEmpty(bundleName))
            return false;
        string path = Path.Combine(Application.streamingAssetsPath, "Windows/" + bundleName)
            .Replace('\\', '/');
        if (File.Exists(path))
            return true;
        // Unity 构建产物统一小写（commonW6 -> commonw6）；macOS 文件系统区分大小写。
        string lower = Path.Combine(Application.streamingAssetsPath,
            "Windows/" + bundleName.ToLowerInvariant()).Replace('\\', '/');
        return File.Exists(lower);
    }

    [MenuItem("Layout Editor/诊断/同步 commonW6 装饰依赖", false, 41)]
    private static void SyncActiveSceneMenu()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("commonW6 依赖", "请先打开一个关卡场景。", "确定");
            return;
        }
        var info = LayoutEditorLevelInfoResolver.ResolveForScene(scene.path);
        int added = SyncForScene(scene, info);
        if (added > 0)
            LayoutEditorPseudoReload.ReloadPseudoAssetsFull();
        EditorUtility.DisplayDialog("commonW6 依赖",
            added > 0 ? ("已补 " + added + " 个 bundle 依赖，并重载伪 prefab。")
                      : "依赖已是最新。", "确定");
    }
}
