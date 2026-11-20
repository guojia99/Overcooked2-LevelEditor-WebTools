using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Normalize native ownership, then validate obsolete optional bundle references.</summary>
public static class LayoutEditorOptionalBundlePreflight
{
    public static bool HasSourceContent(string setName, string kind)
    {
        var folder = "Assets/LevelSets/" + setName + "/" + kind;
        if (kind == "custom_content")
        {
            var config = AssetDatabase.LoadAssetAtPath<CustomContentConfigSO>(folder + "/Config.asset");
            if (config != null && config.entries != null && config.entries.Length > 0)
                return true; // Broken entries are not empty content: report rather than discard.
        }
        if (!Directory.Exists(folder))
            return false;
        foreach (var path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            if (IsPayloadPath(path, folder))
                return true;
        return false;
    }

    private static bool IsPayloadPath(string path, string folder)
    {
        path = path.Replace('\\', '/');
        folder = folder.Replace('\\', '/');
        if (string.Equals(path, folder + "/Config.asset", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, folder + "/CustomRecipeConfig.asset", StringComparison.OrdinalIgnoreCase))
            return false;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext != ".meta" && ext != ".json" && ext != ".txt" && ext != ".md";
    }

    public static bool NeedsDedicatedBundle(string setName, string kind)
    {
        return HasSourceContent(setName, kind) && !SourceIsInInfoBundle(setName, kind);
    }

    private static bool SourceIsInInfoBundle(string setName, string kind)
    {
        var folder = "Assets/LevelSets/" + setName + "/" + kind;
        var owned = new HashSet<string>(AssetDatabase.GetAssetPathsFromAssetBundle(
            setName + "/info_" + setName) ?? new string[0], StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(folder))
            return false;
        var found = false;
        foreach (var path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            if (!IsPayloadPath(path, folder))
                continue;
            if (!owned.Contains(path.Replace('\\', '/')))
                return false;
            found = true;
        }
        if (kind == "custom_content")
        {
            var config = AssetDatabase.LoadAssetAtPath<CustomContentConfigSO>(folder + "/Config.asset");
            foreach (var entry in config != null && config.entries != null
                ? config.entries : new CustomContentConfigSO.Entry[0])
                if (entry == null || string.IsNullOrEmpty(entry.prefabPath) || !owned.Contains(entry.prefabPath))
                    return false;
        }
        return found;
    }

    public static HashSet<string> Run(List<string> setNames)
    {
        foreach (var setName in setNames)
            LayoutEditorSetBundleOwnership.Normalize(setName);
        var empty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changes = new List<LevelInfoSO>();
        var replacements = new List<string[]>();
        var infos = new List<LevelInfoSO>();
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // A merged export may reference another included set through an SO.
        foreach (var setName in setNames)
        {
            var setRoot = "Assets/LevelSets/" + setName;
            foreach (var guid in AssetDatabase.FindAssets("t:LevelInfoSO", new[] { setRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(path);
                if (info == null)
                    throw new Exception("空包预检无法读取 LevelInfo: " + path);
                infos.Add(info);
                roots.Add(path);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { setRoot }))
                roots.Add(AssetDatabase.GUIDToAssetPath(guid));
        }
        HashSet<string> references = null;
        var memoryBundleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var setName in setNames)
        {
            var setRoot = "Assets/LevelSets/" + setName;

            var candidates = new List<string>();
            var infoOwned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kind in new[] { "custom_content", "custom_recipes" })
            {
                var bundleName = setName + "/" + kind;
                var paths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
                var hasBuildAssets = false;
                foreach (var path in paths ?? new string[0])
                {
                    if (!AssetDatabase.IsValidFolder(path)
                        && IsPayloadPath(path, setRoot + "/" + kind))
                        hasBuildAssets = true;
                }
                if (hasBuildAssets)
                    continue;
                if (HasSourceContent(setName, kind))
                {
                    if (!SourceIsInInfoBundle(setName, kind))
                        throw new Exception("可选 bundle 有内容但没有已登记的构建资产: " + bundleName
                            + "，请修复该目录内资产的 bundle 标签后重试（不复用旧产物）。");
                    infoOwned.Add(bundleName);
                }
                candidates.Add(bundleName);
            }
            foreach (var bundleName in AssetDatabase.GetAllAssetBundleNames())
            {
                if (!bundleName.StartsWith(setName + "/", StringComparison.OrdinalIgnoreCase)
                    || candidates.Contains(bundleName)
                    || string.Equals(bundleName, setName + "/info_" + setName, StringComparison.OrdinalIgnoreCase)
                    || (AssetDatabase.GetAssetPathsFromAssetBundle(bundleName) ?? new string[0]).Length != 0)
                    continue;
                foreach (var info in infos)
                    foreach (var dependency in info.dependencies ?? new string[0])
                        if (string.Equals(dependency, bundleName, StringComparison.OrdinalIgnoreCase))
                            throw new Exception("必需依赖无源资产: " + bundleName + " <- " + AssetDatabase.GetAssetPath(info));
                candidates.Add(bundleName);
            }
            if (candidates.Count == 0)
                continue;

            // Serialized GUID references and runtime bundleName strings both count.
            // LevelInfo.dependencies alone is a load declaration, not proof of usage.
            if (references == null)
            {
                references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var root in roots)
                {
                    references.Add(root);
                    foreach (var dependency in AssetDatabase.GetDependencies(root, true))
                        references.Add(dependency);
                }
                var rootObjects = new HashSet<UnityEngine.Object>();
                foreach (var reference in references)
                {
                    var extension = Path.GetExtension(reference).ToLowerInvariant();
                    if (extension != ".asset" && extension != ".prefab")
                        continue; // Keep native paths as evidence without importing textures/audio/models.
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(reference))
                    {
                        if (asset != null)
                            rootObjects.Add(asset);
                    }
                }
                // Loaded scenes can have new references absent from their disk YAML.
                // Inspect their current roots without opening or saving any scene.
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.IsValid() || !scene.isLoaded || !roots.Contains(scene.path))
                        continue;
                    foreach (var rootObject in scene.GetRootGameObjects())
                        rootObjects.Add(rootObject);
                }
                var objects = new UnityEngine.Object[rootObjects.Count];
                rootObjects.CopyTo(objects);
                foreach (var asset in EditorUtility.CollectDependencies(objects))
                    if (asset != null)
                        rootObjects.Add(asset);
                foreach (var asset in rootObjects)
                {
                    var path = AssetDatabase.GetAssetPath(asset);
                    if (!string.IsNullOrEmpty(path))
                        references.Add(path);
                    if (!(asset is ScriptableObject) && !(asset is MonoBehaviour))
                        continue; // Native mesh/texture arrays cannot contain our runtime fields.
                    var serialized = new SerializedObject(asset);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType == SerializedPropertyType.String
                            && (property.name == "bundleName" || property.name == "m_bundleName"))
                            memoryBundleNames.Add(property.stringValue);
                    }
                }
            }
            foreach (var bundleName in candidates)
            {
                if (memoryBundleNames.Contains(bundleName))
                    throw new Exception("空包仍有内存 bundleName 引用: " + bundleName);
                var folder = setRoot + "/" + bundleName.Substring(bundleName.LastIndexOf('/') + 1);
                var pattern = @"(?m)^\s*(?:-\s+)?(?:bundleName|m_bundleName):\s*[""']?"
                    + Regex.Escape(bundleName) + @"[""']?\s*$";
                foreach (var reference in references)
                {
                    if (reference.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)
                        && IsPayloadPath(reference, folder) && !infoOwned.Contains(bundleName))
                        throw new Exception("空包仍有实际资产引用: " + bundleName + " <- " + reference);
                    var ext = Path.GetExtension(reference).ToLowerInvariant();
                    if (ext != ".asset" && ext != ".prefab" && ext != ".unity")
                        continue;
                    if (!File.Exists(reference))
                        throw new Exception("空包预检无法核验引用文件: " + reference);
                    if (Regex.IsMatch(File.ReadAllText(reference), pattern))
                        throw new Exception("空包仍有运行时 bundleName 引用: " + bundleName + " <- " + reference);
                }
                empty.Add(bundleName);
            }
        }
        foreach (var info in infos)
        {
            var kept = new List<string>();
            var changed = false;
            foreach (var dependency in info.dependencies ?? new string[0])
            {
                if (empty.Contains(dependency))
                    changed = true;
                else
                    kept.Add(dependency);
            }
            if (changed)
            {
                changes.Add(info);
                replacements.Add(kept.ToArray());
            }
        }
        // Do not write any cleanup until every candidate has passed validation.
        for (int i = 0; i < changes.Count; i++)
        {
            Undo.RecordObject(changes[i], "Remove empty optional bundle dependency");
            changes[i].dependencies = replacements[i];
            EditorUtility.SetDirty(changes[i]);
            Debug.Log("[SetExporter] 已清理空可选包依赖: " + AssetDatabase.GetAssetPath(changes[i]));
        }
        // The normal export SaveAssets persists these changes before building.
        // Do not save unrelated dirty assets during this early validation step.
        foreach (var name in empty)
        {
            if ((AssetDatabase.GetAssetPathsFromAssetBundle(name) ?? new string[0]).Length == 0)
                AssetDatabase.RemoveAssetBundleName(name, false);
            Debug.Log("[SetExporter] 已排除无需独立交付的可选包（空包或内容已归 info 包，且无独立包引用）: " + name);
        }
        return empty;
    }
}
