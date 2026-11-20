using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

// A frozen, preflighted YAML copy plan. Never open or modify the source scene.
internal sealed class LayoutEditorLevelTemplate
{
    internal sealed class Entry
    {
        internal string source, target, guid, text, original, metadata;
    }

    internal readonly List<Entry> entries = new List<Entry>();
    internal LevelInfoSO info;
    internal LevelConfigSetupPerPlayerCountSO[] configs;
    internal string infoPath, scenePath;
    private static readonly Regex References = new Regex(@"\bguid:\s*([0-9a-fA-F]{32})\b");

    internal static string Root(string mode) { return "Assets/Template/" + mode.ToLowerInvariant(); }
    internal static string InfoPath(string mode)
    {
        return Root(mode) + "/levelinfo_template" + (mode == "Campaign" ? "" : "_" + mode.ToLowerInvariant()) + ".asset";
    }

    internal static string ScenePath(string mode)
    {
        return Root(mode) + "/s_template" + (mode == "Campaign" ? "" : "_" + mode.ToLowerInvariant()) + ".unity";
    }

    internal static string ConfigPath(string mode, int player)
    {
        return Root(mode) + "/config_" + player + "p" + (mode == "Campaign" ? "" : "_template_" + mode.ToLowerInvariant()) + ".asset";
    }

    internal static LayoutEditorLevelTemplate Read(string mode, string dataDir, string scenePath, string infoPath)
    {
        var plan = new LayoutEditorLevelTemplate { infoPath = infoPath, scenePath = scenePath };
        var root = Root(mode);
        var sourceInfo = InfoPath(mode);
        var sourceScene = ScenePath(mode);
        plan.info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(sourceInfo);
        if (plan.info == null || plan.info.levelType.ToString() != mode)
            throw new InvalidDataException("无效的模式 LevelInfo 模板：" + sourceInfo);
        plan.configs = new LevelConfigSetupPerPlayerCountSO[4];
        var configPaths = new string[4];
        for (int p = 0; p < 4; p++)
        {
            configPaths[p] = ConfigPath(mode, p + 1);
            plan.configs[p] = AssetDatabase.LoadAssetAtPath<LevelConfigSetupPerPlayerCountSO>(configPaths[p]);
            if (plan.configs[p] == null) throw new InvalidDataException("缺少人数配置模板：" + configPaths[p]);
        }
        if (!File.Exists(Absolute(sourceScene))) throw new FileNotFoundException("缺少场景模板：" + sourceScene);
        var files = Directory.GetFiles(Absolute(root), "*", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            var source = root + "/" + file.Substring(Absolute(root).Length + 1).Replace('\\', '/');
            var extension = Path.GetExtension(source);
            if (extension != ".asset" && extension != ".unity" && extension != ".mat"
                && extension != ".controller" && extension != ".anim" && extension != ".prefab")
                throw new InvalidDataException("模板含不支持的私有资产：" + source);
            var original = File.ReadAllText(file);
            var text = original;
            if (!text.StartsWith("%YAML", StringComparison.Ordinal)) throw new InvalidDataException("模板资产必须为 YAML：" + source);
            if (source == sourceInfo)
            {
                for (int p = 0; p < 4; p++)
                {
                    var binding = new Regex(@"(?m)^(  config_" + (p + 1) + @"p:\s*)\{[^\r\n]*\}");
                    if (binding.Matches(text).Count != 1) throw new InvalidDataException("模板人数绑定不唯一：" + source);
                    text = binding.Replace(text, "${1}{fileID: 11400000, guid: " + AssetDatabase.AssetPathToGUID(configPaths[p]) + ", type: 2}");
                }
            }
            var guid = AssetDatabase.AssetPathToGUID(source);
            if (string.IsNullOrEmpty(guid) || !guids.Add(guid)) throw new InvalidDataException("模板 GUID 缺失或重复：" + source);
            var metadata = File.Exists(file + ".meta") ? File.ReadAllText(file + ".meta") : "";
            if (!Regex.IsMatch(metadata, @"(?m)^guid: " + guid + @"\s*$"))
                throw new InvalidDataException("模板 metadata 缺失或 GUID 不一致：" + source);
            var target = source == sourceInfo ? infoPath : source == sourceScene ? scenePath
                : dataDir + "/template_assets/" + source.Substring(root.Length + 1);
            for (int p = 0; p < 4; p++) if (source == configPaths[p]) target = dataDir + "/config_" + (p + 1) + "p.asset";
            if (File.Exists(Absolute(target)) || AssetDatabase.LoadMainAssetAtPath(target) != null)
                throw new IOException("目标资产已存在：" + target);
            plan.entries.Add(new Entry { source = source, target = target, guid = guid, text = text, original = original, metadata = metadata });
        }
        foreach (var entry in plan.entries)
            foreach (Match match in References.Matches(entry.text))
            {
                var guid = match.Groups[1].Value;
                if (guids.Contains(guid) || guid.StartsWith("0000000000000000", StringComparison.Ordinal)) continue;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !File.Exists(Absolute(path)))
                    throw new InvalidDataException("模板含缺失引用：" + entry.source + " → " + guid);
                if (path.StartsWith("Assets/Template/", StringComparison.Ordinal))
                    throw new InvalidDataException("模板含跨模式私有引用：" + entry.source + " → " + path);
                if (path.StartsWith("Assets/LevelSets/", StringComparison.Ordinal))
                    throw new InvalidDataException("模板不能引用其他关卡的私有资产：" + entry.source + " → " + path);
            }
        var scenes = plan.entries.FindAll(delegate(Entry e) { return e.source.EndsWith(".unity", StringComparison.Ordinal); });
        if (scenes.Count != 1 || scenes[0].source != sourceScene)
            throw new InvalidDataException("模式模板必须恰好有一个场景。");
        var infoReference = new Regex(@"(?m)^\s*levelInfo:\s*\{fileID:\s*11400000,\s*guid:\s*" + AssetDatabase.AssetPathToGUID(sourceInfo) + @",\s*type:\s*2\s*\}");
        if (infoReference.Matches(scenes[0].text).Count != 1
            || Regex.Matches(scenes[0].text, @"(?m)^\s*levelInfo:").Count != 1)
            throw new InvalidDataException("场景必须唯一绑定所选模式 LevelInfo 模板。");
        return plan;
    }

    internal void Copy()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
            if (!File.Exists(Absolute(entry.source)) || File.ReadAllText(Absolute(entry.source)) != entry.original
                || !File.Exists(Absolute(entry.source) + ".meta") || File.ReadAllText(Absolute(entry.source) + ".meta") != entry.metadata
                || AssetDatabase.AssetPathToGUID(entry.source) != entry.guid)
                throw new IOException("模板在预检后发生变化：" + entry.source);
        foreach (var entry in entries)
        {
            EnsureFolder(Path.GetDirectoryName(entry.target).Replace('\\', '/'));
            if (File.Exists(Absolute(entry.target)) || File.Exists(Absolute(entry.target) + ".meta"))
                throw new IOException("目标资产在预检后已存在：" + entry.target);
            if (!AssetDatabase.CopyAsset(entry.source, entry.target)) throw new IOException("复制模板失败：" + entry.source);
            var guid = AssetDatabase.AssetPathToGUID(entry.target);
            if (string.IsNullOrEmpty(guid) || guid == entry.guid) throw new IOException("副本 GUID 无效：" + entry.target);
            map.Add(entry.guid, guid);
        }
        foreach (var entry in entries) File.WriteAllText(Absolute(entry.target), Remap(entry.text, map));
        // CopyAsset imports assets internally; never open a scene until every binding is rewritten.
        foreach (var entry in entries)
        {
            AssetDatabase.ImportAsset(entry.target, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(entry.target);
            if (importer == null) throw new IOException("无法读取副本 importer：" + entry.target);
            importer.SetAssetBundleNameAndVariant("", "");
            importer.SaveAndReimport();
        }
    }

    internal static void NormalizeLabels(string dataDir, string scenePath, string setName)
    {
        var paths = new List<string> { dataDir };
        foreach (var path in AssetDatabase.GetAllAssetPaths())
            if (path.StartsWith(dataDir + "/", StringComparison.Ordinal)) paths.Add(path);
        paths.Add(scenePath);
        foreach (var path in paths)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null) throw new IOException("无法设置本关 bundle 归属：" + path);
            var expected = path == scenePath ? setName + "/" + Path.GetFileNameWithoutExtension(scenePath) : "";
            if (importer.assetBundleName == expected && string.IsNullOrEmpty(importer.assetBundleVariant)) continue;
            importer.SetAssetBundleNameAndVariant(expected, "");
            importer.SaveAndReimport();
        }
    }

    internal static string[] Cleanup(string[] paths)
    {
        var errors = new List<string>();
        foreach (var path in paths)
        {
            try
            {
                if (!File.Exists(Absolute(path)) && !Directory.Exists(Absolute(path))
                    && !File.Exists(Absolute(path) + ".meta") && AssetDatabase.LoadMainAssetAtPath(path) == null) continue;
                if (!AssetDatabase.DeleteAsset(path) || File.Exists(Absolute(path)) || Directory.Exists(Absolute(path))
                    || File.Exists(Absolute(path) + ".meta")) errors.Add("DeleteAsset 失败，残留：" + path);
            }
            catch (Exception ex) { errors.Add("清理失败：" + path + "：" + ex.Message); }
        }
        return errors.ToArray();
    }

    internal static string Remap(string text, IDictionary<string, string> map)
    {
        return References.Replace(text, delegate(Match match) {
            string replacement;
            return map.TryGetValue(match.Groups[1].Value, out replacement)
                ? match.Value.Substring(0, match.Groups[1].Index - match.Index) + replacement : match.Value;
        });
    }

    internal void RemoveSupersededModeAssets()
    {
        var retained = new HashSet<string>(AssetDatabase.GetDependencies(new[] { infoPath, scenePath }, true), StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (retained.Contains(entry.target)) continue;
            var asset = AssetDatabase.LoadMainAssetAtPath(entry.target);
            if (!(asset is LevelConfigSetupPerPlayerCountSO) && !(asset is HordeLevelConfigSO)
                && !(asset is HordeWaveDataSO) && !(asset is RecipeListSO)) continue;
            var errors = Cleanup(new[] { entry.target });
            if (errors.Length > 0) throw new IOException(string.Join("；", errors));
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path))))
            throw new IOException("无法创建模板副本目录：" + path);
    }

    private static string Absolute(string path)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar));
    }
}
