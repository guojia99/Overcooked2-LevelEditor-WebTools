#!/usr/bin/env python3
"""Compile the complete production optional-bundle helper against Unity doubles.

Run: python3 layout-editor/scripts/check-optional-bundle-preflight.py
Requires dotnet SDK 8. Uses net8.0 / C# 4, no Unity or NuGet packages.
The helper and CustomContentConfigSO compile unchanged. Exporter collection/build
methods, CustomIngredients.EnsureWebDependencies and PseudoReload's custom bundle
dependency repair compile as extracted methods; unrelated ingredient classifiers
and CommonW6 sync are doubles. Ownership normalization is an explicit boundary
double: legacy preflight fixtures retain their labels to test rejection/cleanup.
The real normalizer is exercised by check-set-bundle-ownership.py.
Export ordering/persistence are source contracts,
not an execution of the entire exporter or Unity serialization. Memory object
dependencies and serialized properties are modeled independently of disk YAML.
Native objects remain in dependency/path checks but fail if serialized by the
harness; only ScriptableObject/MonoBehaviour runtime fields may be traversed.
Info-bundle ownership is separate from source presence and dedicated bundle tags;
the production CustomContentConfigSO.Entry (including prefabPath) is used directly.
All generated sources, SDK state, fixtures and products live outside the repo.
Failures are real failures (including review repros), never expected-failure passes.
"""
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[2]
EDITOR = ROOT / "Assets/Editor/LayoutEditor"


def extract(source, declaration):
    matches = list(re.finditer(declaration, source))
    if len(matches) != 1:
        raise AssertionError("Expected one production declaration: " + declaration)
    match = matches[0]
    start = source.index("{", match.start())
    depth = 0
    tokens = r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/|[{}]'
    for token in re.finditer(tokens, source[start:]):
        value = token.group()
        if value == "{":
            depth += 1
        elif value == "}":
            depth -= 1
            if depth == 0:
                return source[match.start():start + token.end()]
    raise AssertionError("Unbalanced production declaration: " + declaration)


DOUBLES = r'''
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using LevelEditor;
using LevelEditorStub;

namespace UnityEngine
{
    public class Object {
        public static T Instantiate<T>(T value) where T : Object { return (T)value.MemberwiseClone(); }
        public static void DestroyImmediate(Object value) { }
    }
    public class ScriptableObject : Object { }
    public class MonoBehaviour : Object { }
    public class GameObject : Object { }
    public class NativeHugeArrays : Object { }
    public static class Application
    { public static string streamingAssetsPath { get { return Path.GetFullPath("Assets/StreamingAssets"); } } }
    public static class Debug { public static void Log(object value) { } }
    public class AssetBundleManifest
    {
        public string[] Produced = new string[0];
        public string[] GetAllAssetBundles() { return Produced; }
    }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string path;
        public bool isLoaded;
        public GameObject[] Roots;
        public bool IsValid() { return path != null; }
        public GameObject[] GetRootGameObjects() { return Roots ?? new GameObject[0]; }
    }
    public static class SceneManager
    {
        public static readonly List<Scene> Scenes = new List<Scene>();
        public static int sceneCount { get { return Scenes.Count; } }
        public static Scene GetSceneAt(int index) { return Scenes[index]; }
        public static Scene GetActiveScene() { return new Scene(); }
    }
}
namespace LevelEditor
{
    public class PseudoPrefabManager { public PseudoPrefabManagerStub stub; }
}
namespace LevelEditorStub
{
    public class PseudoPrefabManagerStub { public LevelInfoSO levelInfo; }
    public class PseudoPrefabSO : ScriptableObject { public string bundleName; }
    public class CustomRecipeSO : ScriptableObject { public PseudoPrefabSO platingStepSO; }
    public class LevelInfoSO : ScriptableObject
    {
        public string[] dependencies;
        public ScriptableObject[] recipes, optionalRecipeMatchListItems;
        public PseudoPrefabSO[] allIngredients, includeRecipeMatchLists, allCookingSteps;
    }
}
namespace UnityEditor
{
    public static class AssetDatabase
    {
        public static readonly Dictionary<string, object> Loaded = new Dictionary<string, object>();
        public static readonly Dictionary<string, UnityEngine.Object[]> SubAssets =
            new Dictionary<string, UnityEngine.Object[]>();
        public static readonly List<string> LoadAllQueries = new List<string>();
        public static readonly HashSet<object> DirtyAssets = new HashSet<object>();
        public static readonly Dictionary<string, string> Types = new Dictionary<string, string>();
        public static readonly Dictionary<string, string[]> Bundles =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<string, string[]> Edges = new Dictionary<string, string[]>();
        public static readonly List<string> Finds = new List<string>(), DependencyQueries = new List<string>();
        public static readonly List<string> Removed = new List<string>();
        public static readonly List<object> Dirty = new List<object>(), Undone = new List<object>();
        public static int Saves;
        public static void Reset()
        {
            Loaded.Clear(); SubAssets.Clear(); LoadAllQueries.Clear(); DirtyAssets.Clear();
            Types.Clear(); Bundles.Clear(); Edges.Clear();
            Finds.Clear(); DependencyQueries.Clear(); Removed.Clear(); Dirty.Clear(); Undone.Clear(); Saves = 0;
            AssetImporter.Items.Clear(); BuildPipeline.Calls = 0;
            LayoutEditorCommonW6Dependencies.Syncs = 0;
            EditorUtility.MemoryEdges.Clear(); EditorUtility.Collected.Clear();
            SerializedObject.Properties.Clear(); SerializedObject.Constructed.Clear(); SceneManager.Scenes.Clear();
        }
        public static T LoadAssetAtPath<T>(string path) where T : class
        { object value; return Loaded.TryGetValue(path, out value) ? value as T : null; }
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string path)
        {
            LoadAllQueries.Add(path);
            var assets = new List<UnityEngine.Object>();
            object main;
            if (Loaded.TryGetValue(path, out main))
            {
                if (!(main is UnityEngine.Object)) throw new Exception("Fixture asset is not a Unity object: " + path);
                assets.Add((UnityEngine.Object)main);
            }
            UnityEngine.Object[] subAssets;
            if (SubAssets.TryGetValue(path, out subAssets)) assets.AddRange(subAssets);
            return assets.ToArray();
        }
        public static string[] FindAssets(string filter, string[] folders)
        {
            if ((filter != "t:LevelInfoSO" && filter != "t:Scene") || folders.Length != 1)
                throw new Exception("Unexpected FindAssets query");
            Finds.Add(filter + "@" + folders[0]);
            var found = new List<string>();
            foreach (var pair in Types)
                if (pair.Value == filter && pair.Key.StartsWith(folders[0] + "/", StringComparison.Ordinal))
                    found.Add(pair.Key);
            return found.ToArray(); // Stable fixture IDs deliberately equal their asset paths.
        }
        public static string GUIDToAssetPath(string guid)
        { if (!Types.ContainsKey(guid)) throw new Exception("Unknown fixture GUID"); return guid; }
        public static string GetAssetPath(object asset)
        {
            foreach (var pair in Loaded) if (object.ReferenceEquals(pair.Value, asset)) return pair.Key;
            foreach (var pair in SubAssets)
                foreach (var child in pair.Value) if (object.ReferenceEquals(child, asset)) return pair.Key;
            return ""; // Scene/transient objects have no AssetDatabase path.
        }
        public static bool IsValidFolder(string path) { return Directory.Exists(path); }
        public static string[] GetAssetPathsFromAssetBundle(string name)
        {
            string[] paths;
            if (Removed.Contains(name)) return new string[0];
            if (!Bundles.TryGetValue(name, out paths))
            {
                var parts = name.Split('/');
                if (parts.Length == 2 && parts[1] == "info_" + parts[0]) return new string[0];
                throw new Exception("Unexpected bundle query: " + name);
            }
            return paths == null ? null : (string[])paths.Clone();
        }
        public static string[] GetAllAssetBundleNames()
        { return new List<string>(Bundles.Keys).ToArray(); }
        public static string[] GetDependencies(string path, bool recursive)
        {
            if (!recursive || !Edges.ContainsKey(path)) throw new Exception("Unexpected dependency query: " + path);
            DependencyQueries.Add(path);
            var seen = new HashSet<string>();
            var pending = new Queue<string>();
            pending.Enqueue(path);
            while (pending.Count > 0)
            {
                var next = pending.Dequeue();
                if (!seen.Add(next)) continue;
                string[] edges;
                if (Edges.TryGetValue(next, out edges)) foreach (var edge in edges) pending.Enqueue(edge);
            }
            return new List<string>(seen).ToArray();
        }
        public static string[] GetDependencies(string[] paths, bool recursive)
        {
            var result = new HashSet<string>();
            foreach (var path in paths) foreach (var dependency in GetDependencies(path, recursive)) result.Add(dependency);
            return new List<string>(result).ToArray();
        }
        public static string GetImplicitAssetBundleName(string path)
        { var importer = AssetImporter.GetAtPath(path); return importer == null ? "" : importer.assetBundleName; }
        public static string[] GetAssetBundleDependencies(string name, bool recursive)
        {
            var parts = name.Split('/');
            bool info = parts.Length == 2 && parts[1] == "info_" + parts[0];
            if (recursive || (!Bundles.ContainsKey(name) && !Removed.Contains(name) && !info))
                throw new Exception("Unexpected ownership query: " + name);
            return new string[0]; // Build tests use independent leaf bundles.
        }
        public static void SaveAssets() { Saves++; DirtyAssets.Clear(); }
        public static void RemoveAssetBundleName(string name, bool force)
        {
            if (force) throw new Exception("Forced bundle removal");
            Removed.Add(name); Bundles.Remove(name);
        }
    }
    public static class Undo
    { public static void RecordObject(object value, string label) { AssetDatabase.Undone.Add(value); } }
    public static class EditorUtility
    {
        public static readonly Dictionary<UnityEngine.Object, UnityEngine.Object[]> MemoryEdges =
            new Dictionary<UnityEngine.Object, UnityEngine.Object[]>();
        public static readonly HashSet<UnityEngine.Object> Collected = new HashSet<UnityEngine.Object>();
        public static void SetDirty(object value)
        { AssetDatabase.Dirty.Add(value); AssetDatabase.DirtyAssets.Add(value); }
        public static UnityEngine.Object[] CollectDependencies(UnityEngine.Object[] roots)
        {
            var seen = new HashSet<UnityEngine.Object>();
            var pending = new Queue<UnityEngine.Object>(roots);
            while (pending.Count > 0)
            {
                var next = pending.Dequeue();
                if (next == null || !seen.Add(next)) continue;
                Collected.Add(next);
                UnityEngine.Object[] edges;
                if (MemoryEdges.TryGetValue(next, out edges)) foreach (var edge in edges) pending.Enqueue(edge);
            }
            var result = new UnityEngine.Object[seen.Count]; seen.CopyTo(result); return result;
        }
    }
    public enum SerializedPropertyType { Generic, String, Integer }
    public class SerializedProperty
    {
        public string name, stringValue;
        public SerializedPropertyType propertyType;
        readonly List<SerializedProperty> entries;
        int index = -1;
        public SerializedProperty() { }
        public SerializedProperty(List<SerializedProperty> values) { entries = values; }
        public bool Next(bool enterChildren)
        {
            if (!enterChildren) throw new Exception("Must visit nested/hidden serialized fields");
            if (++index >= entries.Count) return false;
            var entry = entries[index]; name = entry.name; stringValue = entry.stringValue; propertyType = entry.propertyType;
            return true;
        }
    }
    public class SerializedObject
    {
        public static readonly HashSet<UnityEngine.Object> Constructed = new HashSet<UnityEngine.Object>();
        public static readonly Dictionary<UnityEngine.Object, List<SerializedProperty>> Properties =
            new Dictionary<UnityEngine.Object, List<SerializedProperty>>();
        readonly UnityEngine.Object asset;
        public SerializedObject(UnityEngine.Object value)
        {
            Constructed.Add(value);
            if (value is NativeHugeArrays) throw new Exception("Native huge-array serialization must be skipped");
            if (!(value is ScriptableObject) && !(value is MonoBehaviour))
                throw new Exception("Unexpected native object serialization");
            asset = value;
        }
        public SerializedProperty GetIterator()
        {
            var values = new List<SerializedProperty>();
            foreach (var field in asset.GetType().GetFields())
                if (field.FieldType == typeof(string))
                    values.Add(new SerializedProperty { name = field.Name,
                        stringValue = (string)field.GetValue(asset), propertyType = SerializedPropertyType.String });
            List<SerializedProperty> nested;
            if (Properties.TryGetValue(asset, out nested)) values.AddRange(nested);
            return new SerializedProperty(values);
        }
    }
    public class AssetImporter
    {
        public string assetBundleName, assetBundleVariant;
        public static readonly Dictionary<string, AssetImporter> Items = new Dictionary<string, AssetImporter>();
        public static AssetImporter GetAtPath(string path)
        { AssetImporter value; return Items.TryGetValue(path, out value) ? value : null; }
    }
    public struct AssetBundleBuild { public string assetBundleName; public string[] assetNames; }
    public enum BuildAssetBundleOptions { None }
    public enum BuildTarget { StandaloneWindows }
    public static class BuildPipeline
    {
        public static int Calls;
        public static AssetBundleManifest BuildAssetBundles(string root, AssetBundleBuild[] builds,
            BuildAssetBundleOptions options, BuildTarget target)
        {
            Calls++;
            throw new Exception("Unexpected real build: these cases must fail before BuildPipeline");
        }
    }
}
static class LayoutEditorPseudoReload
{
    public static bool ExportBuildInProgress;
    public static void ForcePrepareForExportStrict(bool requireManager)
    { throw new Exception("Unexpected Prepare"); }
}
static class LayoutEditorCommonW6Dependencies
{
    public static int Syncs;
    public static void SyncForScene(UnityEngine.SceneManagement.Scene scene, LevelInfoSO info) { Syncs++; }
}
static class LayoutEditorCatalogApi
{
    public static bool BundleFileExists(string name)
    { return File.Exists("Assets/StreamingAssets/Windows/" + name); }
}
static class LayoutEditorSaladSauceFactory
{
    public static void SyncOptionals(LevelInfoSO info) { }
    public static void AddDependencies(LevelInfoSO info, List<string> deps) { }
}
static class LayoutEditorPancakeFill
{ public static bool HasPlainPancake(LevelInfoSO info) { return false; } }
static class LayoutEditorSetBundleOwnership
{
    public static readonly List<string> Calls = new List<string>();
    public static void Normalize(string set) { Calls.Add(set); }
}
static class LayoutEditorLevelModeStore
{
    public static ScriptableObject[] CollectAllRecipes(LevelInfoSO info) { return info.recipes ?? new ScriptableObject[0]; }
    public static PseudoPrefabSO[] CollectHordeEnemies(LevelInfoSO info) { return new PseudoPrefabSO[0]; }
}
static class LayoutEditorRecipeKnowledge
{
    public static bool TryGetOriginal(string id, out string step, out string[] ingredients)
    { throw new Exception("Unexpected original recipe lookup"); }
}
'''

EXPORTER = r'''
static class ExporterContracts
{
    static HashSet<string> _emptyOptionalBundles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static AssetBundleManifest _buildManifest;
    static List<string> _builtBundleNames;
    const string BundlesRoot = "Assets/StreamingAssets/Windows";
    static string AbsPath(string path) { return Path.GetFullPath(path); }
    public static List<string> Collect(HashSet<string> empty, bool partial, params string[] sets)
    {
        _emptyOptionalBundles = empty;
        var scenes = new List<KeyValuePair<string, List<string>>>();
        foreach (var set in sets)
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/LevelSets/" + set }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!partial || Path.GetFileName(path) == "s_selected.unity") paths.Add(path);
            }
            scenes.Add(new KeyValuePair<string, List<string>>(set, paths));
        }
        return CollectLevelBundleNames(scenes, partial);
    }
    public static void Build(List<string> names) { BuildListedBundles(names, true); }
'''

INGREDIENTS = r'''
static class IngredientContracts
{
    static readonly HashSet<string> _pendingDocBundles = new HashSet<string>();
    static bool ReferencesCommonW2(LevelInfoSO info) { return false; }
    static bool ReferencesCommonW3(LevelInfoSO info) { return false; }
    static bool ReferencesCommonW5(LevelInfoSO info) { return false; }
    static bool IsCommon03Asset(string path) { return false; }
    static void AddWebAssetBundleDep(List<string> deps, string sub, string id)
    { throw new Exception("Unexpected web ingredient dependency"); }
'''

RELOAD = r'''
static class ReloadContracts
{
    public static void Restore(LevelInfoSO info)
    {
        var manager = new LevelEditor.PseudoPrefabManager();
        manager.stub = new PseudoPrefabManagerStub { levelInfo = info };
        EnsureCustomBundleDependency(manager);
    }
'''

TESTS = r'''
class OptionalBundleContracts
{
    static string root;
    static int cases, checks, failures;
    static string Folder(string set, string kind) { return "Assets/LevelSets/" + set + "/" + kind; }
    static void Check(bool ok, string label)
    { checks++; if (!ok) throw new Exception(label); }
    static void Write(string path, string text)
    { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, text); }
    static void Product(string name) { Write("Assets/StreamingAssets/Windows/" + name, "STALE PRODUCT"); }
    static void Set(string set)
    {
        foreach (var kind in new[] { "custom_content", "custom_recipes" })
            AssetDatabase.Bundles[set + "/" + kind] = new string[0];
    }
    static void Register(string path, UnityEngine.Object value, string type)
    {
        Write(path, "%YAML 1.1\n--- !u!114 &11400000\nMonoBehaviour:\n");
        AssetDatabase.Loaded[path] = value;
        if (type != null) AssetDatabase.Types[path] = type;
        AssetDatabase.Edges[path] = new string[0];
    }
    static LevelInfoSO Info(string set, params string[] dependencies)
    {
        var info = new LevelInfoSO { dependencies = dependencies };
        Register(Folder(set, "info") + "/LevelInfo.asset", info, "t:LevelInfoSO");
        return info;
    }
    static string Scene(string set, string name)
    {
        var path = Folder(set, "scenes") + "/" + name + ".unity";
        Register(path, new UnityEngine.Object(), "t:Scene");
        AssetImporter.Items[path] = new AssetImporter { assetBundleName = set + "/" + name };
        return path;
    }
    static HashSet<string> Run(params string[] sets)
    {
        LayoutEditorSetBundleOwnership.Calls.Clear();
        var result = LayoutEditorOptionalBundlePreflight.Run(new List<string>(sets));
        Check(string.Join(",", LayoutEditorSetBundleOwnership.Calls.ToArray()) == string.Join(",", sets),
            "preflight invokes ownership boundary once per requested set in order");
        return result;
    }
    static void NoWrites(string label)
    {
        Check(AssetDatabase.Undone.Count == 0 && AssetDatabase.Dirty.Count == 0
            && AssetDatabase.Saves == 0 && AssetDatabase.Removed.Count == 0, label);
    }
    static void Reject(Action action, string message)
    {
        Exception caught = null;
        try { action(); } catch (Exception ex) { caught = ex; }
        Check(caught != null && caught.Message.Contains(message),
            "expected rejection containing '" + message + "', got " + (caught == null ? "success" : caught.Message));
    }
    static void Case(string name, Action action)
    {
        cases++;
        Directory.SetCurrentDirectory(root);
        var directory = Path.Combine(root, "case-" + cases);
        Directory.CreateDirectory(directory); Directory.SetCurrentDirectory(directory);
        AssetDatabase.Reset();
        try { action(); Console.WriteLine("PASS: " + name); }
        catch (Exception ex) { failures++; Console.WriteLine("FAIL: " + name + ": " + ex.Message); }
    }
    static void EmptyChecks()
    {
        foreach (var mode in new[] { "missing", "directory", "metadata", "config", "null-entries", "folder-label", "null-paths" })
        {
            Case("empty optional bundles: " + mode, delegate {
                Set("alpha");
                foreach (var kind in new[] { "custom_content", "custom_recipes" })
                {
                    var folder = Folder("alpha", kind);
                    if (mode != "missing") Directory.CreateDirectory(folder);
                    if (mode == "metadata")
                        foreach (var name in new[] { "nested/file.meta", "names.json", "README.md", "notes.txt" })
                            Write(folder + "/" + name, "metadata");
                    if (mode == "config" || mode == "null-entries")
                    {
                        var config = new CustomContentConfigSO();
                        if (mode == "null-entries") config.entries = null;
                        Register(folder + (kind == "custom_content" ? "/Config.asset" : "/CustomRecipeConfig.asset"), config, null);
                    }
                    if (mode == "folder-label") AssetDatabase.Bundles["alpha/" + kind] = new[] { folder };
                    if (mode == "null-paths") AssetDatabase.Bundles["alpha/" + kind] = null;
                    Check(!LayoutEditorOptionalBundlePreflight.HasSourceContent("alpha", kind), "metadata is not source: " + kind);
                }
                var empty = Run("alpha");
                Check(empty.SetEquals(new[] { "alpha/custom_content", "alpha/custom_recipes" }), "both empty names excluded");
                Check(AssetDatabase.Saves == 0 && AssetDatabase.Dirty.Count == 0, "no LevelInfo writes without changes");
                if (mode == "folder-label") Check(AssetDatabase.Removed.Count == 0, "folder labels preserved");
            });
        }
        Case("old disk bundles never become source or fresh dependencies", delegate {
            Set("alpha"); Product("alpha/custom_content"); Product("alpha/custom_recipes");
            var info = Info("alpha", "commonW1");
            IngredientContracts.EnsureWebDependencies("alpha", info, false);
            Check(info.dependencies.Length == 0, "old products and shared declarations excluded by native-only collector");
            AssetDatabase.Dirty.Clear(); AssetDatabase.DirtyAssets.Clear(); // Authoring saved before export.
            Check(Run("alpha").Count == 2, "disk products do not prevent exclusion");
            Check(File.ReadAllText("Assets/StreamingAssets/Windows/alpha/custom_recipes") == "STALE PRODUCT", "old cache preserved");
        });
        Case("stale LevelInfo dependencies removed, other values/order/duplicates preserved", delegate {
            Set("alpha");
            var info = Info("alpha", "commonW1", "ALPHA/CUSTOM_CONTENT", "keep", "alpha/custom_recipes", "keep");
            var other = new LevelInfoSO { dependencies = null };
            Register(Folder("alpha", "info") + "/Other.asset", other, "t:LevelInfoSO");
            var unrelated = Info("outside", "alpha/custom_content");
            AssetDatabase.DirtyAssets.Add(unrelated);
            var empty = Run("alpha");
            Check(empty.Count == 2 && string.Join(",", info.dependencies) == "commonW1,keep,keep", "surgical cleanup");
            Check(other.dependencies == null && unrelated.dependencies[0] == "alpha/custom_content", "unchanged and outside infos preserved");
            Check(AssetDatabase.Undone.Count == 1 && object.ReferenceEquals(AssetDatabase.Undone[0], info)
                && AssetDatabase.Dirty.Count == 1 && object.ReferenceEquals(AssetDatabase.Dirty[0], info)
                && AssetDatabase.DirtyAssets.Contains(info), "only changed LevelInfo receives undo/dirty");
            Check(AssetDatabase.Saves == 0 && AssetDatabase.DirtyAssets.Contains(unrelated),
                "preflight never saves unrelated dirty assets; cleanup waits for normal export save");
        });
        Case("second preflight succeeds with LevelInfo still dirty from first cleanup", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
            Check(Run("alpha").Count == 2 && AssetDatabase.DirtyAssets.Contains(info), "first cleanup leaves info dirty");
            var before = info.dependencies;
            AssetDatabase.Undone.Clear(); AssetDatabase.Dirty.Clear();
            Check(Run("alpha").Count == 2, "second run accepts dirty state without real optional references");
            Check(object.ReferenceEquals(before, info.dependencies)
                && AssetDatabase.Undone.Count == 0 && AssetDatabase.Dirty.Count == 0 && AssetDatabase.Saves == 0,
                "second run needs no dependency writes and never saves assets");
        });
        foreach (var reverse in new[] { false, true })
            Case("cross-set stale dependency cleanup independent of set order: reverse=" + reverse, delegate {
                Set("alpha"); Set("beta");
                var alpha = Info("alpha", "keep", "beta/custom_content", "commonW1");
                var beta = Info("beta", "beta/custom_content", "other");
                var empty = reverse ? Run("beta", "alpha") : Run("alpha", "beta");
                Check(empty.Contains("beta/custom_content") && string.Join(",", alpha.dependencies) == "keep,commonW1"
                    && string.Join(",", beta.dependencies) == "other", "all infos cleaned after all candidates classified");
                Check(AssetDatabase.Undone.Count == 2 && AssetDatabase.Dirty.Count == 2
                    && AssetDatabase.DirtyAssets.Contains(alpha) && AssetDatabase.DirtyAssets.Contains(beta) && AssetDatabase.Saves == 0,
                    "both infos staged dirty without any global save");
            });
    }
    static void SourceChecks()
    {
        foreach (var kind in new[] { "custom_content", "custom_recipes" })
        {
            foreach (var ext in new[] { ".asset", ".prefab", ".png", ".fbx", ".bytes" })
                Case("untagged payload fails early: " + kind + ext, delegate {
                    Set("alpha"); var path = Folder("alpha", kind) + "/nested/New" + ext;
                    Write(path, "new payload");
                    Check(LayoutEditorOptionalBundlePreflight.HasSourceContent("alpha", kind), "payload recognized");
                    Reject(delegate { Run("alpha"); }, "alpha/" + kind); NoWrites("failed payload preflight never writes");
                });
            Case("tagged new payload retained without any bundle product: " + kind, delegate {
                Set("alpha"); var name = "alpha/" + kind;
                var path = Folder("alpha", kind) + "/New.prefab"; Write(path, "new payload");
                AssetDatabase.Bundles[name] = new[] { path };
                var info = Info("alpha");
                IngredientContracts.EnsureWebDependencies("alpha", info, true);
                Check(info.dependencies.Length == 0, "dedicated delivery bundle never becomes a native load declaration");
                Check(!File.Exists("Assets/StreamingAssets/Windows/" + name), "no old product supplied");
                AssetDatabase.Dirty.Clear(); AssetDatabase.DirtyAssets.Clear(); // Authoring saved before export.
                var empty = Run("alpha");
                Check(!empty.Contains(name), "valid new content cannot be omitted");
                var names = ExporterContracts.Collect(empty, false, "alpha");
                Check(names.Count == 1 && names[0] == "alpha/info_alpha", "native export roots use the info carrier");
            });
            Case("folder-only registration cannot hide untagged payload: " + kind, delegate {
                Set("alpha"); var folder = Folder("alpha", kind);
                Write(folder + "/New.prefab", "payload");
                AssetDatabase.Bundles["alpha/" + kind] = new[] { folder };
                Reject(delegate { Run("alpha"); }, "没有已登记的构建资产"); NoWrites("folder-only payload fails before cleanup");
            });
        }
        foreach (var missingDirectory in new[] { false, true })
            Case("nonempty config with missing resources is not empty: missing-directory=" + missingDirectory, delegate {
                Set("alpha");
                var config = new CustomContentConfigSO {
                    entries = new[] { new CustomContentConfigSO.Entry { prefabPath = Folder("alpha", "custom_content") + "/Missing.prefab" } }
                };
                var path = Folder("alpha", "custom_content") + "/Config.asset";
                if (!missingDirectory) Register(path, config, null);
                else AssetDatabase.Loaded[path] = config; // Loaded Unity cache and disk are independent.
                Check(LayoutEditorOptionalBundlePreflight.HasSourceContent("alpha", "custom_content"), "broken config still has content");
                Reject(delegate { Run("alpha"); }, "alpha/custom_content"); NoWrites("broken config never silently cleaned");
            });
        Case("empty inherited-bundle CustomRecipeConfig remains excludable", delegate {
            Set("alpha"); var path = Folder("alpha", "custom_recipes") + "/CustomRecipeConfig.asset";
            Register(path, new ScriptableObject(), null);
            AssetDatabase.Bundles["alpha/custom_recipes"] = new[] { path };
            Check(!LayoutEditorOptionalBundlePreflight.HasSourceContent("alpha", "custom_recipes"), "config is metadata");
            Check(Run("alpha").Contains("alpha/custom_recipes"), "tagged config alone incorrectly keeps an empty bundle");
        });
    }
    static void ReferenceChecks()
    {
        foreach (var kind in new[] { "custom_content", "custom_recipes" })
            foreach (var fromInfo in new[] { false, true })
                Case("serialized recursive dependency blocks: " + kind + "/info=" + fromInfo, delegate {
                    Set("alpha"); var info = Info("alpha", "alpha/" + kind, "keep");
                    var other = kind == "custom_content" ? "custom_recipes" : "custom_content";
                    var kept = Folder("alpha", other) + "/Kept.asset"; Write(kept, "payload");
                    AssetDatabase.Bundles["alpha/" + other] = new[] { kept };
                    var before = info.dependencies;
                    var scene = Scene("alpha", "s_selected");
                    var rootPath = fromInfo ? AssetDatabase.GetAssetPath(info) : scene;
                    var external = "Assets/shared/Bridge.asset";
                    Register(external, new ScriptableObject(), null);
                    AssetDatabase.Edges[rootPath] = new[] { external };
                    AssetDatabase.Edges[external] = new[] { Folder("alpha", kind) + "/Deleted.prefab", rootPath };
                    Reject(delegate { Run("alpha"); }, "实际资产引用");
                    Check(AssetDatabase.DependencyQueries.Contains(rootPath), "production GetDependencies queried root");
                    Check(object.ReferenceEquals(before, info.dependencies), "dependencies untouched"); NoWrites("reference failure is read-only");
                });
        foreach (var field in new[] { "bundleName", "m_bundleName" })
            foreach (var quote in new[] { "", "\"", "'" })
                Case("external SO runtime string blocks: " + field + "/quote=" + quote, delegate {
                    Set("alpha"); Info("alpha", "alpha/custom_recipes");
                    var scene = Scene("alpha", "s_selected"); var external = "Assets/commonW1/External.asset";
                    Register(external, new ScriptableObject(), null);
                    Write(external, "%YAML 1.1\nMonoBehaviour:\n  " + field + ": " + quote + "alpha/custom_recipes" + quote + "\n");
                    AssetDatabase.Edges[scene] = new[] { external };
                    Reject(delegate { Run("alpha"); }, "bundleName 引用"); NoWrites("external SO prevents omission and cleanup");
                });
        Case("unrelated string or unreferenced external SO does not block", delegate {
            Set("alpha"); var scene = Scene("alpha", "s_selected");
            Write(scene, "MonoBehaviour:\n  bundleName: alpha/custom_content_extra\n  note: alpha/custom_recipes\n");
            Register("Assets/shared/Unused.asset", new ScriptableObject(), null);
            Write("Assets/shared/Unused.asset", "bundleName: alpha/custom_content\n");
            Check(Run("alpha").Count == 2, "only exact reachable runtime references matter");
        });
        Case("unreadable reachable serialized asset fails closed", delegate {
            Set("alpha"); var scene = Scene("alpha", "s_selected");
            AssetDatabase.Edges[scene] = new[] { "Assets/shared/Missing.asset" };
            Reject(delegate { Run("alpha"); }, "无法核验引用文件"); NoWrites("unverifiable graph never writes");
        });
        Case("first kind cleanable, second kind referenced: no early writes", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_content", "alpha/custom_recipes", "keep");
            var before = info.dependencies; var scene = Scene("alpha", "s_selected");
            Write(scene, "MonoBehaviour:\n  bundleName: alpha/custom_recipes\n");
            Reject(delegate { Run("alpha"); }, "alpha/custom_recipes");
            Check(object.ReferenceEquals(before, info.dependencies), "first candidate did not mutate info"); NoWrites("no early candidate writes");
        });
        Case("first set cleanable, second set invalid: no early writes", delegate {
            Set("alpha"); Set("beta"); var info = Info("alpha", "alpha/custom_content", "keep");
            var before = info.dependencies; Write(Folder("beta", "custom_recipes") + "/New.asset", "payload");
            Reject(delegate { Run("alpha", "beta"); }, "beta/custom_recipes");
            Check(object.ReferenceEquals(before, info.dependencies), "first set dependencies not assigned"); NoWrites("entire invocation validates before commit");
        });
        Case("selected-level export protects every scene and LevelInfo in current sets", delegate {
            Set("alpha"); Set("beta"); Scene("alpha", "s_selected");
            var unselected = Scene("alpha", "s_unselected");
            var second = Info("beta", "beta/custom_content");
            Write(unselected, "bundleName: alpha/custom_content\n");
            Reject(delegate { Run("alpha", "beta"); }, "alpha/custom_content"); NoWrites("unselected scene prevents cleanup");
            Write(unselected, "MonoBehaviour:\n");
            Write(AssetDatabase.GetAssetPath(second), "bundleName: beta/custom_content\n");
            Reject(delegate { Run("alpha", "beta"); }, "beta/custom_content");
            Check(AssetDatabase.Finds.Contains("t:Scene@Assets/LevelSets/alpha")
                && AssetDatabase.Finds.Contains("t:LevelInfoSO@Assets/LevelSets/beta"), "all current sets scanned");
            NoWrites("later set's LevelInfo prevents earlier set cleanup");
        });
        Case("reference from another current set prevents target omission", delegate {
            Set("alpha"); Set("beta");
            var info = Info("beta", "beta/custom_content", "keep");
            var before = info.dependencies; var scene = Scene("alpha", "s_selected");
            var external = "Assets/shared/BetaWrapper.asset";
            Register(external, new ScriptableObject(), null);
            Write(external, "MonoBehaviour:\n  bundleName: beta/custom_content\n");
            AssetDatabase.Edges[scene] = new[] { external };
            Exception caught = null; HashSet<string> empty = null;
            try { empty = Run("alpha", "beta"); } catch (Exception ex) { caught = ex; }
            Check(caught != null && caught.Message.Contains("beta/custom_content"),
                "expected cross-set reference rejection, got " + (caught == null
                    ? "success; target omitted=" + empty.Contains("beta/custom_content")
                        + "; target dependencies=" + string.Join(",", info.dependencies) + "; saves=" + AssetDatabase.Saves
                    : caught.Message));
            Check(object.ReferenceEquals(before, info.dependencies), "cross-set use preserves target dependency");
            NoWrites("cross-set reference failure is read-only");
        });
    }
    static void MemoryChecks()
    {
        foreach (var subAsset in new[] { false, true })
            Case("changed runtime bundleName blocks despite old disk: subasset=" + subAsset, delegate {
                Set("alpha"); Set("beta");
                var info = Info("alpha", "alpha/custom_content", "beta/custom_content", "keep");
                var before = info.dependencies; var scene = Scene("beta", "s_selected");
                var external = "Assets/shared/Runtime.asset";
                var main = new PseudoPrefabSO { bundleName = "unrelated/old" };
                Register(external, main, null);
                Write(external, "MonoBehaviour:\n  bundleName: unrelated/old\n");
                PseudoPrefabSO dirty = main;
                if (subAsset)
                {
                    dirty = new PseudoPrefabSO();
                    AssetDatabase.SubAssets[external] = new UnityEngine.Object[] { null, dirty };
                    Check(!AssetDatabase.DirtyAssets.Contains(main), "main asset is clean; changed child must be inspected");
                }
                dirty.bundleName = "alpha/custom_content"; // Memory changed; serialized string still old.
                AssetDatabase.DirtyAssets.Add(dirty);
                AssetDatabase.Edges[scene] = new[] { external };
                Check(!File.ReadAllText(external).Contains(dirty.bundleName), "disk cannot reveal new runtime reference");
                Reject(delegate { Run("alpha", "beta"); }, "内存 bundleName 引用");
                Check(AssetDatabase.LoadAllQueries.Contains(external), "production loads all objects at reachable path");
                Check(AssetDatabase.DirtyAssets.Contains(dirty) && object.ReferenceEquals(before, info.dependencies),
                    "dirty asset and stale dependencies left untouched");
                NoWrites("dirty runtime reference blocks before undo, dirty calls, saves or bundle removal");
            });
        Case("dirty LevelInfo without real optional reference permits cleanup", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
            AssetDatabase.DirtyAssets.Add(info);
            Check(Run("alpha").Count == 2 && string.Join(",", info.dependencies) == "keep", "dirty state alone never blocks cleanup");
            Check(AssetDatabase.Undone.Count == 1 && AssetDatabase.Dirty.Count == 1 && AssetDatabase.Saves == 0,
                "only stale dependency changed, no global save");
        });
        Case("clean main/subassets and null subasset permit cleanup", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
            var external = "Assets/shared/Clean.asset";
            Register(external, new PseudoPrefabSO(), null);
            AssetDatabase.SubAssets[external] = new UnityEngine.Object[] { null, new PseudoPrefabSO() };
            AssetDatabase.Edges[AssetDatabase.GetAssetPath(info)] = new[] { external };
            Check(Run("alpha").Count == 2 && string.Join(",", info.dependencies) == "keep", "clean reference graph permits omission");
            Check(AssetDatabase.LoadAllQueries.Contains(external) && AssetDatabase.Saves == 0,
                "all referenced assets inspected without global save");
        });
        Case("unreferenced dirty SO does not block or get saved", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
            var unused = new PseudoPrefabSO { bundleName = "alpha/custom_content" };
            Register("Assets/shared/Unused.asset", unused, null); AssetDatabase.DirtyAssets.Add(unused);
            Check(Run("alpha").Count == 2 && string.Join(",", info.dependencies) == "keep", "unreferenced dirty asset permits cleanup");
            Check(AssetDatabase.DirtyAssets.Contains(unused) && AssetDatabase.Saves == 0
                && AssetDatabase.Dirty.Count == 1 && object.ReferenceEquals(AssetDatabase.Dirty[0], info),
                "preflight stages only LevelInfo and preserves unrelated dirty changes unsaved");
        });
        Case("reachable changed SO with unrelated bundle string permits cleanup", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
            var external = "Assets/shared/Changed.asset";
            var changed = new PseudoPrefabSO { bundleName = "unrelated/new" };
            Register(external, changed, null); AssetDatabase.DirtyAssets.Add(changed);
            AssetDatabase.Edges[AssetDatabase.GetAssetPath(info)] = new[] { external };
            Check(Run("alpha").Count == 2 && string.Join(",", info.dependencies) == "keep", "reachable unrelated changes allowed");
            Check(AssetDatabase.DirtyAssets.Contains(changed) && AssetDatabase.Saves == 0, "unrelated changes stay unsaved");
        });
        foreach (var behaviour in new[] { false, true })
          foreach (var field in new[] { "bundleName", "m_bundleName" })
            Case("nested nonvisible runtime field blocks: " + field + "/MonoBehaviour=" + behaviour, delegate {
                Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
                UnityEngine.Object runtime = behaviour ? (UnityEngine.Object)new MonoBehaviour() : new ScriptableObject();
                EditorUtility.MemoryEdges[info] = new[] { runtime };
                SerializedObject.Properties[runtime] = new List<SerializedProperty> {
                    new SerializedProperty { name = "nested", propertyType = SerializedPropertyType.Generic },
                    new SerializedProperty { name = field, stringValue = "alpha/custom_content", propertyType = SerializedPropertyType.String }
                };
                Reject(delegate { Run("alpha"); }, "内存 bundleName 引用");
                Check(SerializedObject.Constructed.Contains(runtime), "runtime script serialized despite native-object filter");
                NoWrites("nested memory string blocks before cleanup");
            });
        Case("only exact property names and string values count in memory", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
            SerializedObject.Properties[info] = new List<SerializedProperty> {
                new SerializedProperty { name = "note", stringValue = "alpha/custom_content", propertyType = SerializedPropertyType.String },
                new SerializedProperty { name = "bundleName", stringValue = "alpha/custom_content_extra", propertyType = SerializedPropertyType.String },
                new SerializedProperty { name = "m_bundleName", stringValue = "alpha/custom_content", propertyType = SerializedPropertyType.Integer }
            };
            Check(Run("alpha").Count == 2 && AssetDatabase.Saves == 0, "unrelated properties/values cannot falsely block");
        });
        Case("new memory reference closure adds payload path absent from disk dependencies", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
            var bridge = new ScriptableObject(); var payload = new ScriptableObject();
            Register("Assets/shared/NewBridge.asset", bridge, null);
            // Loaded cache may still contain a deleted/moved payload; no file creates source content.
            AssetDatabase.Loaded[Folder("alpha", "custom_content") + "/Missing.asset"] = payload;
            EditorUtility.MemoryEdges[info] = new UnityEngine.Object[] { bridge };
            EditorUtility.MemoryEdges[bridge] = new UnityEngine.Object[] { info, payload };
            Reject(delegate { Run("alpha"); }, "实际资产引用");
            Check(EditorUtility.Collected.Contains(payload), "current recursive memory closure reached payload");
            NoWrites("new object reference blocks before cleanup");
        });
        Case("new memory-only external SO reference contributes bundleName", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
            var external = new PseudoPrefabSO { bundleName = "alpha/custom_content" };
            Register("Assets/shared/NewRuntime.asset", external, null);
            EditorUtility.MemoryEdges[info] = new UnityEngine.Object[] { external };
            Reject(delegate { Run("alpha"); }, "内存 bundleName 引用"); NoWrites("new SO outside disk closure blocks");
        });
        foreach (var mode in new[] { "direct-string", "object-edge", "unselected-scene", "outside-set", "unloaded" })
            Case("loaded scene memory scan: " + mode, delegate {
                Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
                var path = Scene("alpha", mode == "unselected-scene" ? "s_unselected" : "s_selected");
                var rootObject = new GameObject();
                SceneManager.Scenes.Add(new UnityEngine.SceneManagement.Scene {
                    path = mode == "outside-set" ? "Assets/LevelSets/outside/scenes/s_other.unity" : path,
                    isLoaded = mode != "unloaded", Roots = new[] { rootObject }
                });
                var target = new PseudoPrefabSO { bundleName = "alpha/custom_content" };
                Register("Assets/shared/SceneRuntime.asset", target, null);
                if (mode == "direct-string")
                {
                    var component = new MonoBehaviour();
                    EditorUtility.MemoryEdges[rootObject] = new UnityEngine.Object[] { component };
                    SerializedObject.Properties[component] = new List<SerializedProperty> {
                        new SerializedProperty { name = "m_bundleName", stringValue = "alpha/custom_content", propertyType = SerializedPropertyType.String }
                    };
                }
                else EditorUtility.MemoryEdges[rootObject] = new UnityEngine.Object[] { target };
                if (mode == "outside-set" || mode == "unloaded")
                {
                    Check(Run("alpha").Count == 2 && !EditorUtility.Collected.Contains(rootObject), "unrelated/unloaded scene roots excluded");
                    Check(AssetDatabase.Saves == 0, "scene scan never saves");
                }
                else
                {
                    Reject(delegate { Run("alpha"); }, "内存 bundleName 引用");
                    Check(EditorUtility.Collected.Contains(rootObject), "loaded set scene root seeds current object closure");
                    Check(!SerializedObject.Constructed.Contains(rootObject), "GameObject itself never serialized");
                    NoWrites("loaded scene changes block without opening/saving scenes");
                }
            });
        Case("disk bundleName still blocks when memory string no longer matches", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
            var external = "Assets/shared/OldRuntime.asset";
            Register(external, new PseudoPrefabSO { bundleName = "unrelated/new" }, null);
            Write(external, "bundleName: alpha/custom_content\n");
            AssetDatabase.Edges[AssetDatabase.GetAssetPath(info)] = new[] { external };
            Reject(delegate { Run("alpha"); }, "运行时 bundleName 引用"); NoWrites("disk channel remains enforced");
        });
    }
    static void NativeAndYamlChecks()
    {
        Case("native huge arrays skipped while dependency closure is collected", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
            var native = new NativeHugeArrays();
            Register("Assets/shared/HugeMesh.asset", native, null);
            AssetDatabase.Edges[AssetDatabase.GetAssetPath(info)] = new[] { "Assets/shared/HugeMesh.asset" };
            var script = new ScriptableObject();
            EditorUtility.MemoryEdges[native] = new UnityEngine.Object[] { script };
            Check(Run("alpha").Count == 2 && string.Join(",", info.dependencies) == "keep", "huge native asset permits ordinary cleanup");
            Check(EditorUtility.Collected.Contains(native) && EditorUtility.Collected.Contains(script), "native object still participates in dependency collection");
            Check(!SerializedObject.Constructed.Contains(native) && SerializedObject.Constructed.Contains(script),
                "native constructor never reached but script dependency serialized");
            Check(AssetDatabase.Saves == 0, "native scan never saves");
        });
        Case("native payload from memory closure retains path-based protection", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
            var native = new NativeHugeArrays();
            AssetDatabase.Loaded[Folder("alpha", "custom_content") + "/DeletedMesh.fbx"] = native;
            EditorUtility.MemoryEdges[info] = new UnityEngine.Object[] { native };
            Reject(delegate { Run("alpha"); }, "实际资产引用");
            Check(EditorUtility.Collected.Contains(native) && !SerializedObject.Constructed.Contains(native),
                "native path added before skipping serialized fields");
            NoWrites("native payload reference prevents omission without serialization");
        });
        Case("native bridge still exposes scripted runtime reference", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
            var native = new NativeHugeArrays(); var component = new MonoBehaviour();
            EditorUtility.MemoryEdges[info] = new UnityEngine.Object[] { native };
            EditorUtility.MemoryEdges[native] = new UnityEngine.Object[] { component };
            SerializedObject.Properties[component] = new List<SerializedProperty> {
                new SerializedProperty { name = "bundleName", stringValue = "alpha/custom_content", propertyType = SerializedPropertyType.String }
            };
            Reject(delegate { Run("alpha"); }, "内存 bundleName 引用");
            Check(!SerializedObject.Constructed.Contains(native) && SerializedObject.Constructed.Contains(component),
                "native filter never prunes scripted dependencies");
            NoWrites("script reached through native object prevents cleanup");
        });
        foreach (var extension in new[] { ".asset", ".prefab", ".unity" })
          foreach (var field in new[] { "bundleName", "m_bundleName" })
           foreach (var quote in new[] { "", "\"", "'" })
            Case("unloadable component list YAML blocks: " + extension + "/" + field + "/quote=" + quote, delegate {
                Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
                var path = "Assets/shared/MissingScript" + extension;
                Write(path, "%YAML 1.1\nMonoBehaviour:\n  entries:\n  - " + field + ": " + quote + "alpha/custom_content" + quote + "\n");
                AssetDatabase.Edges[AssetDatabase.GetAssetPath(info)] = new[] { path };
                Check(AssetDatabase.LoadAllAssetsAtPath(path).Length == 0, "fixture component unavailable in memory");
                Reject(delegate { Run("alpha"); }, "运行时 bundleName 引用");
                Check(AssetDatabase.LoadAllQueries.Contains(path), "disk fallback used after failed object load");
                NoWrites("list YAML reference blocks despite missing runtime component");
            });
        Case("unrelated list YAML values do not block", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
            var path = "Assets/shared/Unrelated.prefab";
            Write(path, "entries:\n- note: alpha/custom_content\n- bundleName: alpha/custom_content_extra\n");
            AssetDatabase.Edges[AssetDatabase.GetAssetPath(info)] = new[] { path };
            Check(Run("alpha").Count == 2 && AssetDatabase.Saves == 0, "list regex still requires exact field/value");
        });
    }
    static string[] InfoOwnedSource(string set, string kind, LevelInfoSO info)
    {
        var paths = new List<string>(); var objects = new List<UnityEngine.Object>();
        for (int i = 0; i < 4; i++) paths.Add(Folder(set, kind) + "/Recipe" + i + ".asset");
        paths.Add(Folder(set, kind) + "/models/Model.prefab");
        paths.Add(Folder(set, kind) + "/models/Mesh.fbx");
        foreach (var path in paths)
        {
            var asset = new ScriptableObject(); Register(path, asset, null); objects.Add(asset);
        }
        if (kind == "custom_content")
            Register(Folder(set, kind) + "/Config.asset", new CustomContentConfigSO {
                entries = new[] { new CustomContentConfigSO.Entry { prefabPath = paths[4] } }
            }, null);
        var owned = new List<string>(paths); owned.Add(AssetDatabase.GetAssetPath(info));
        AssetDatabase.Bundles[set + "/info_" + set] = owned.ToArray();
        AssetDatabase.Edges[AssetDatabase.GetAssetPath(info)] = paths.ToArray();
        EditorUtility.MemoryEdges[info] = objects.ToArray();
        return paths.ToArray();
    }
    static void InfoOwnershipChecks()
    {
        Case("only unknown info bundle queries default to empty", delegate {
            Check(AssetDatabase.GetAssetPathsFromAssetBundle("alpha/info_alpha").Length == 0, "unknown info is empty");
            Reject(delegate { AssetDatabase.GetAssetPathsFromAssetBundle("alpha/custom_recipes"); }, "Unexpected bundle query");
            Reject(delegate { AssetDatabase.GetAssetPathsFromAssetBundle("alpha/info_beta"); }, "Unexpected bundle query");
            NoWrites("bundle query defaults have no mutations");
        });
        foreach (var kind in new[] { "custom_recipes", "custom_content" })
        {
            Case("all source in info permits GUID and memory path references: " + kind, delegate {
                Set("alpha"); var name = "alpha/" + kind;
                var info = Info("alpha", name, "keep"); var paths = InfoOwnedSource("alpha", kind, info);
                var scene = Scene("alpha", "s_selected"); AssetDatabase.Edges[scene] = paths;
                Check(LayoutEditorOptionalBundlePreflight.HasSourceContent("alpha", kind), "info ownership does not erase source presence");
                Check(!LayoutEditorOptionalBundlePreflight.NeedsDedicatedBundle("alpha", kind), "all payload/config targets are info-owned");
                var empty = Run("alpha");
                Check(empty.Contains(name) && string.Join(",", info.dependencies) == "keep", "only stale dedicated dependency removed");
                var collected = ExporterContracts.Collect(empty, false, "alpha");
                Check(collected.Count == 2 && collected.Contains("alpha/info_alpha") && collected.Contains("alpha/s_selected"), "export retains info and scene carriers and omits dedicated bundle");
                Check(AssetDatabase.DependencyQueries.Contains(scene) && EditorUtility.Collected.Contains((UnityEngine.Object)AssetDatabase.Loaded[paths[0]]),
                    "real disk and memory path checks both exercised");
                Check(AssetDatabase.Saves == 0 && AssetDatabase.Dirty.Count == 1, "preflight stages cleanup without saving");
            });
            Case("mixed info-owned and unassigned payload fails: " + kind, delegate {
                Set("alpha"); var info = Info("alpha", "alpha/" + kind, "keep");
                var paths = InfoOwnedSource("alpha", kind, info); var before = info.dependencies;
                AssetDatabase.Bundles["alpha/info_alpha"] = new[] { AssetDatabase.GetAssetPath(info), paths[0], paths[1], paths[2], paths[3], paths[4] };
                Check(LayoutEditorOptionalBundlePreflight.HasSourceContent("alpha", kind)
                    && LayoutEditorOptionalBundlePreflight.NeedsDedicatedBundle("alpha", kind), "one uncovered model prevents info-only exemption");
                Reject(delegate { Run("alpha"); }, "没有已登记的构建资产");
                Check(object.ReferenceEquals(before, info.dependencies), "mixed ownership preserves stale declarations on failure");
                NoWrites("mixed ownership failure is read-only");
            });
            foreach (var channel in new[] { "memory", "disk" })
                Case("info ownership cannot hide dedicated bundleName: " + kind + "/" + channel, delegate {
                    Set("alpha"); var name = "alpha/" + kind; var info = Info("alpha", name, "keep");
                    var paths = InfoOwnedSource("alpha", kind, info); var before = info.dependencies;
                    if (channel == "memory")
                        SerializedObject.Properties[(UnityEngine.Object)AssetDatabase.Loaded[paths[0]]] = new List<SerializedProperty> {
                            new SerializedProperty { name = "m_bundleName", stringValue = name, propertyType = SerializedPropertyType.String }
                        };
                    else Write(paths[0], "MonoBehaviour:\n  bundleName: " + name + "\n");
                    Reject(delegate { Run("alpha"); }, channel == "memory" ? "内存 bundleName 引用" : "运行时 bundleName 引用");
                    Check(object.ReferenceEquals(before, info.dependencies), "runtime dedicated reference prevents cleanup");
                    NoWrites("dedicated string reference blocks even when every payload is info-owned");
                });
            foreach (var replace in new[] { false, true })
                Case("daily registration skips info-carried dedicated dependency: " + kind + "/replace=" + replace, delegate {
                    Set("alpha"); var info = Info("alpha", "keep"); InfoOwnedSource("alpha", kind, info);
                    Product("alpha/" + kind);
                    IngredientContracts.EnsureWebDependencies("alpha", info, replace);
                    Check(info.dependencies.Length == 0,
                        "source plus old dedicated product cannot register info-carried content separately");
                    Check(AssetDatabase.Saves == 0, "daily registration never forces global save");
                });
        }
        foreach (var invalid in new[] { "missing-prefab", "empty-prefab", "null-entry" })
            Case("nonempty config requires every prefab in info: " + invalid, delegate {
                Set("alpha"); var info = Info("alpha", "alpha/custom_content", "keep");
                InfoOwnedSource("alpha", "custom_content", info);
                var config = AssetDatabase.LoadAssetAtPath<CustomContentConfigSO>(Folder("alpha", "custom_content") + "/Config.asset");
                config.entries = new[] { invalid == "null-entry" ? null : new CustomContentConfigSO.Entry {
                    prefabPath = invalid == "empty-prefab" ? "" : Folder("alpha", "custom_content") + "/Missing.prefab"
                } };
                Check(LayoutEditorOptionalBundlePreflight.HasSourceContent("alpha", "custom_content")
                    && LayoutEditorOptionalBundlePreflight.NeedsDedicatedBundle("alpha", "custom_content"), "covered payload does not conceal invalid config entry");
                Reject(delegate { Run("alpha"); }, "没有已登记的构建资产"); NoWrites("invalid config never gets info-only exemption");
            });
        Case("scene restore never re-adds info-carried recipes from old product", delegate {
            Set("alpha"); var info = Info("alpha", "alpha/custom_recipes", "keep");
            InfoOwnedSource("alpha", "custom_recipes", info); Product("alpha/custom_recipes");
            Check(Run("alpha").Contains("alpha/custom_recipes") && string.Join(",", info.dependencies) == "keep", "preflight removes obsolete dedicated dependency");
            var before = info.dependencies; var undos = AssetDatabase.Undone.Count; var dirties = AssetDatabase.Dirty.Count;
            ReloadContracts.Restore(info);
            Check(LayoutEditorCommonW6Dependencies.Syncs == 1 && info.dependencies.Length == 0, "restore cleans nonnative declarations without reviving dedicated bundle");
            Check(AssetDatabase.Undone.Count == undos && AssetDatabase.Dirty.Count == dirties + 1 && AssetDatabase.Saves == 1,
                "restore persists native-only cleanup once");
            before = info.dependencies; ReloadContracts.Restore(info);
            Check(object.ReferenceEquals(before, info.dependencies) && AssetDatabase.Saves == 1, "clean restore is idempotent");
        });
    }
    static void IntegrationChecks()
    {
        foreach (var partial in new[] { false, true })
            foreach (var foldersOnly in new[] { false, true })
                Case("ordinary required bundle never generally skipped: partial=" + partial + "/folder=" + foldersOnly, delegate {
                    Set("alpha"); var info = Info("alpha", "alpha/required", "commonW1");
                    AssetDatabase.Bundles["alpha/required"] = new string[0]; Product("alpha/required");
                    if (foldersOnly)
                    {
                        var folder = Folder("alpha", "required"); Directory.CreateDirectory(folder);
                        AssetDatabase.Bundles["alpha/required"] = new[] { folder };
                    }
                    if (!foldersOnly)
                    {
                        var before = info.dependencies;
                        Reject(delegate { Run("alpha"); }, "必需依赖无源资产");
                        Check(object.ReferenceEquals(before, info.dependencies), "explicit required dependency never cleaned");
                        NoWrites("explicit required zero-source bundle fails before preflight mutation");
                        Check(BuildPipeline.Calls == 0, "required zero-source dependency fails before build");
                        return;
                    }
                    var empty = Run("alpha");
                    Check(!empty.Contains("alpha/required") && info.dependencies.Length == 2, "folder-only required bundle is not zero registered paths");
                    var names = ExporterContracts.Collect(empty, partial, "alpha");
                    Check(names.Count == 1 && names[0] == "alpha/info_alpha", "native collector uses info delivery root");
                    Reject(delegate { ExporterContracts.Build(new List<string> { "alpha/required" }); }, foldersOnly ? "只有文件夹" : "无资产");
                    Check(BuildPipeline.Calls == 0, "ordinary missing source rejects before build despite stale disk");
                });
        Case("partial bundle collection excludes empties, retains selected scene/info", delegate {
            Set("alpha"); var selected = Scene("alpha", "s_selected"); var unselected = Scene("alpha", "s_unselected");
            var info = Info("alpha");
            AssetDatabase.Bundles["alpha/s_selected"] = new[] { selected };
            AssetDatabase.Bundles["alpha/s_unselected"] = new[] { unselected };
            AssetImporter.Items[selected] = new AssetImporter { assetBundleName = "alpha/s_selected" };
            AssetDatabase.Bundles["alpha/info_alpha"] = new[] { AssetDatabase.GetAssetPath(info) };
            // Folder-only label survives preflight name removal; collector must explicitly filter it.
            var folder = Folder("alpha", "custom_content"); Directory.CreateDirectory(folder);
            AssetDatabase.Bundles["alpha/custom_content"] = new[] { folder };
            var empty = Run("alpha");
            var names = ExporterContracts.Collect(empty, true, "alpha");
            Check(names.Count == 2 && names.Contains("alpha/s_selected") && names.Contains("alpha/info_alpha"), "partial delivery exact");
            Check(ExporterContracts.Collect(empty, false, "alpha").Contains("alpha/s_unselected"), "full collection retains unselected scene");
        });
        Case("CustomIngredients append/replace excludes custom delivery names and retains native references", delegate {
            Set("alpha"); var info = Info("alpha", "keep");
            Product("alpha/custom_recipes");
            IngredientContracts.EnsureWebDependencies("alpha", info, true);
            Check(info.dependencies.Length == 0, "replace does not revive stale disk or retain unrelated old declarations");
            Write(Folder("alpha", "custom_recipes") + "/New.asset", "new recipe");
            Write(Folder("alpha", "custom_content") + "/New.prefab", "new prefab");
            Product("bundle18");
            var native = new PseudoPrefabSO { bundleName = "bundle18" };
            Register("Assets/shared/Native.asset", native, null);
            info.allIngredients = new[] { native };
            info.dependencies = new[] { "keep" };
            IngredientContracts.EnsureWebDependencies("alpha", info, false);
            Check(string.Join(",", info.dependencies) == "bundle18", "append filters legacy names and collects existing native references");
            IngredientContracts.EnsureWebDependencies("alpha", info, false);
            Check(info.dependencies.Length == 1, "native registration idempotent");
            IngredientContracts.EnsureWebDependencies("alpha", info, true);
            Check(string.Join(",", info.dependencies) == "bundle18", "replace retains current native references only");
        });
        foreach (var mode in new[] { "missing", "directory", "config" })
            Case("scene restore never revives old optional bundle: " + mode, delegate {
                Set("alpha"); var info = Info("alpha", "alpha/custom_recipes", "keep");
                Product("alpha/custom_recipes");
                var folder = Folder("alpha", "custom_recipes");
                if (mode != "missing") Directory.CreateDirectory(folder);
                if (mode == "config") Register(folder + "/CustomRecipeConfig.asset", new ScriptableObject(), null);
                Check(Run("alpha").Contains("alpha/custom_recipes"), "preflight omits empty source despite old product");
                var before = info.dependencies; var undos = AssetDatabase.Undone.Count;
                var dirtyCalls = AssetDatabase.Dirty.Count; var removals = AssetDatabase.Removed.Count;
                ReloadContracts.Restore(info);
                Check(LayoutEditorCommonW6Dependencies.Syncs == 1, "actual restore method executed with CommonW6 sync double");
                Check(info.dependencies.Length == 0,
                    "restore cannot re-add stale optional dependency");
                Check(AssetDatabase.Undone.Count == undos && AssetDatabase.Dirty.Count == dirtyCalls + 1
                    && AssetDatabase.Removed.Count == removals && AssetDatabase.Saves == 1,
                    "restore persists native-only cleanup without registry mutation");
                before = info.dependencies; ReloadContracts.Restore(info);
                Check(object.ReferenceEquals(before, info.dependencies) && AssetDatabase.Saves == 1, "repeat restore neither writes nor saves");
            });
        foreach (var product in new[] { false, true })
            Case("scene restore filters legacy names regardless of optional product: product=" + product, delegate {
                var info = Info("alpha", "keep");
                Write(Folder("alpha", "custom_recipes") + "/New.asset", "payload");
                if (product) Product("alpha/custom_recipes");
                ReloadContracts.Restore(info);
                Check(info.dependencies.Length == 0, "source and old product never become native load declarations");
                Check(AssetDatabase.Saves == 1 && AssetDatabase.Undone.Count == 0 && AssetDatabase.Dirty.Count == 1,
                    "restore persists only native dependency cleanup");
                ReloadContracts.Restore(info);
                Check(info.dependencies.Length == 0 && AssetDatabase.Saves == 1,
                    "restore never duplicates dependency or saves twice");
            });
    }
    static void GhostRegistrationChecks()
    {
        foreach (var nullPaths in new[] { false, true })
            Case("unreferenced ghost_runtime registration safely removed: null-paths=" + nullPaths, delegate {
                Set("alpha"); var info = Info("alpha", "keep"); var before = info.dependencies;
                AssetDatabase.Bundles["alpha/ghost_runtime"] = nullPaths ? null : new string[0];
                Product("alpha/ghost_runtime");
                var empty = Run("alpha");
                Check(empty.Contains("alpha/ghost_runtime") && AssetDatabase.Removed.Contains("alpha/ghost_runtime"),
                    "unreferenced zero-source registration excluded and removed");
                Check(object.ReferenceEquals(before, info.dependencies) && AssetDatabase.Saves == 0
                    && AssetDatabase.Undone.Count == 0 && AssetDatabase.Dirty.Count == 0, "no dependency writes for ghost cleanup");
                Check(!ExporterContracts.Collect(empty, false, "alpha").Contains("alpha/ghost_runtime"), "ghost never reaches build collection");
                Check(File.Exists("Assets/StreamingAssets/Windows/alpha/ghost_runtime"), "stale product neither source nor deleted cache");
            });
        Case("ghost explicit dependency rejects case-insensitively", delegate {
            Set("alpha"); var info = Info("alpha", "ALPHA/GHOST_RUNTIME", "keep");
            var before = info.dependencies; AssetDatabase.Bundles["alpha/ghost_runtime"] = new string[0];
            Reject(delegate { Run("alpha"); }, "必需依赖无源资产");
            Check(object.ReferenceEquals(before, info.dependencies), "explicit declaration preserved"); NoWrites("explicit ghost dependency blocks cleanup");
        });
        foreach (var channel in new[] { "memory", "disk" })
            Case("SO runtime string blocks ghost omission: " + channel, delegate {
                Set("alpha"); var info = Info("alpha", "keep");
                AssetDatabase.Bundles["alpha/ghost_runtime"] = new string[0];
                var path = "Assets/shared/GhostReference.asset";
                var so = new PseudoPrefabSO { bundleName = channel == "memory" ? "alpha/ghost_runtime" : "unrelated" };
                Register(path, so, null);
                if (channel == "disk") Write(path, "entries:\n- bundleName: alpha/ghost_runtime\n");
                AssetDatabase.Edges[AssetDatabase.GetAssetPath(info)] = new[] { path };
                Reject(delegate { Run("alpha"); }, channel == "memory" ? "内存 bundleName 引用" : "运行时 bundleName 引用");
                NoWrites("runtime ghost reference prevents registry mutation");
            });
        Case("unrequested set and shared ghost registrations untouched", delegate {
            Set("alpha"); Info("alpha", "keep");
            foreach (var name in new[] { "beta/ghost_runtime", "alpha_extra/ghost_runtime", "shared_ghost" })
                AssetDatabase.Bundles[name] = new string[0];
            var empty = Run("alpha");
            foreach (var name in new[] { "beta/ghost_runtime", "alpha_extra/ghost_runtime", "shared_ghost" })
                Check(!empty.Contains(name) && AssetDatabase.Bundles.ContainsKey(name) && !AssetDatabase.Removed.Contains(name),
                    "only exact current set prefix considered: " + name);
        });
        foreach (var partial in new[] { false, true })
            Case("required info root with zero source never omitted: partial=" + partial, delegate {
                Set("alpha"); Info("alpha", "keep");
                AssetDatabase.Bundles["alpha/info_alpha"] = new string[0]; Product("alpha/info_alpha");
                var empty = Run("alpha");
                Check(!empty.Contains("alpha/info_alpha") && !AssetDatabase.Removed.Contains("alpha/info_alpha"), "info is never a ghost candidate");
                var names = ExporterContracts.Collect(empty, partial, "alpha");
                Check(names.Count == 1 && names[0] == "alpha/info_alpha", "info remains mandatory delivery root");
                Reject(delegate { ExporterContracts.Build(names); }, "无资产");
                Check(BuildPipeline.Calls == 0, "stale info product cannot bypass source validation");
            });
        Case("selected scene source root remains mandatory after ghost classification", delegate {
            Set("alpha"); Info("alpha", "keep"); var scene = Scene("alpha", "s_selected");
            AssetDatabase.Bundles["alpha/s_selected"] = new string[0];
            AssetImporter.Items[scene] = new AssetImporter { assetBundleName = "alpha/s_selected" };
            var empty = Run("alpha"); var names = ExporterContracts.Collect(empty, true, "alpha");
            Check(names.Contains("alpha/s_selected"), "explicit scene importer delivery root survives collector");
            Reject(delegate { ExporterContracts.Build(new List<string> { "alpha/s_selected" }); }, "无资产");
            Check(BuildPipeline.Calls == 0, "selected scene without source cannot silently ship old product");
        });
        foreach (var reverse in new[] { false, true })
            Case("cross-set explicit ghost dependency must block regardless of order: reverse=" + reverse, delegate {
                Set("alpha"); Set("beta");
                var alpha = Info("alpha", "keep"); var beta = Info("beta", "alpha/ghost_runtime", "keep");
                var before = beta.dependencies; AssetDatabase.Bundles["alpha/ghost_runtime"] = new string[0];
                Exception caught = null;
                try { if (reverse) Run("beta", "alpha"); else Run("alpha", "beta"); }
                catch (Exception ex) { caught = ex; }
                Check(caught != null && caught.Message.Contains("必需依赖无源资产"),
                    "expected explicit dependency rejection; got " + (caught == null
                        ? "success; beta dependencies=" + string.Join(",", beta.dependencies)
                            + "; ghost removed=" + AssetDatabase.Removed.Contains("alpha/ghost_runtime")
                        : caught.Message));
                Check(object.ReferenceEquals(before, beta.dependencies) && alpha.dependencies[0] == "keep", "all declarations unchanged on failure");
                NoWrites("cross-set required dependency blocks before any mutation");
            });
    }
    static int Main(string[] args)
    {
        root = args[0];
        EmptyChecks(); SourceChecks(); ReferenceChecks(); MemoryChecks(); NativeAndYamlChecks(); InfoOwnershipChecks(); IntegrationChecks(); GhostRegistrationChecks();
        Directory.SetCurrentDirectory(root);
        Console.WriteLine("RESULT: " + cases + " scenarios, " + checks + " assertions reached, " + failures + " failures.");
        Console.WriteLine("Scope: unchanged production preflight + extracted integration/native dependency methods, net8/C#4, Unity API doubles; ownership normalization boundary doubled; no live Unity import/build.");
        Console.WriteLine("Memory/disk channels verified; dirty state alone permits cleanup and repeated preflight without saving.");
        return failures == 0 ? 0 : 1;
    }
}
'''


def main():
    if not shutil.which("dotnet"):
        raise SystemExit("dotnet SDK 8 is required for optional-bundle preflight checks")
    exporter = (EDITOR / "LayoutEditorSetExporter.cs").read_text(encoding="utf-8")
    ingredients = (EDITOR / "LayoutEditorCustomIngredients.cs").read_text(encoding="utf-8")
    reload = (EDITOR / "LayoutEditorPseudoReload.cs").read_text(encoding="utf-8")
    core = extract(exporter, r"    private static bool RunExportCore\(")
    finish = extract(exporter, r"    private static void FinishPreparedExport\(")
    call = "_emptyOptionalBundles = LayoutEditorOptionalBundlePreflight.Run(setNames);"
    contracts = [
        (core.count(call) == 1, "export preflight receives all current sets once"),
        (call in core and core.index(call) < core.index("scenes.RemoveAll("),
         "preflight precedes selected-level filtering (full-set protection)"),
        (call in core and core.index(call) < core.index("OrphanCleanHook != null"),
         "preflight precedes destructive scene preparation"),
        ("AssetDatabase.SaveAssets();" in finish
         and finish.index("AssetDatabase.SaveAssets();") < finish.index("BuildListedBundles("),
         "normal FinishPreparedExport persists staged cleanup before bundle build (source contract)"),
    ]
    contract_failures = 0
    for ok, label in contracts:
        print(("PASS: " if ok else "FAIL: ") + label, flush=True)
        contract_failures += not ok
    exporter_methods = [extract(exporter, r"    private static [^\n]+\b" + name + r"\(")
                        for name in ("AddBundleName", "CollectLevelBundleNames", "BundleContainsOnlyScenes",
                                     "CollectBuildOwnership", "BuildListedBundles")]
    ingredient_methods = [extract(ingredients, r"    public static void EnsureWebDependencies\(string levelSet, LevelInfoSO info, bool replaceExisting\)")]
    ingredient_methods += [extract(ingredients, r"    private static [^\n]+\b" + name + r"\(")
                           for name in ("EnsureWebDependenciesCore", "DependencyRecipes", "AddDependency", "ReadBundleNameField")]
    constants = re.findall(r'    public const string (?:CustomContentDirName|Common\w+BundleName) = "[^"]+";', ingredients)
    if len(constants) != 9:
        raise AssertionError("Expected nine production ingredient bundle/directory constants")
    reload_method = extract(reload, r"    private static void EnsureCustomBundleDependency\(")
    helper = (EDITOR / "LayoutEditorOptionalBundlePreflight.cs").read_text(encoding="utf-8")
    if re.search(r'\b(?:IsDirty|GetDirtyCount)\s*\(', helper):
        raise AssertionError("Production helper must inspect references, never dirty APIs/counts")
    # Resolve TMPDIR overrides too: callers cannot redirect fixtures into this repo.
    temp_parent = Path(tempfile.gettempdir()).resolve()
    if temp_parent == ROOT or ROOT in temp_parent.parents:
        raise SystemExit("Temporary directory must be outside the repository: " + str(temp_parent))
    with tempfile.TemporaryDirectory(prefix="optional-bundle-preflight-", dir=temp_parent) as temp:
        directory = Path(temp)
        (directory / "Helper.cs").write_text(
            helper, encoding="utf-8")
        (directory / "Config.cs").write_text(
            (EDITOR / "CustomContentConfigSO.cs").read_text(encoding="utf-8"), encoding="utf-8")
        (directory / "Program.cs").write_text(
            DOUBLES + extract((EDITOR / "LayoutEditorNativeDependencies.cs").read_text(encoding="utf-8"),
                              r"public static class LayoutEditorNativeDependencies\b")
            + EXPORTER + "\n".join(exporter_methods) + "\n}\n"
            + INGREDIENTS + "\n".join(constants + ingredient_methods) + "\n}\n"
            + RELOAD + reload_method + "\n}\n" + TESTS,
            encoding="utf-8")
        project = directory / "Contracts.csproj"
        project.write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
            '<LangVersion>4</LangVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo>'
            '<EnableNETAnalyzers>false</EnableNETAnalyzers><NuGetAudit>false</NuGetAudit>'
            '</PropertyGroup></Project>', encoding="utf-8")
        (directory / "NuGet.Config").write_text(
            '<configuration><packageSources><clear /></packageSources></configuration>', encoding="utf-8")
        env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1",
                   DOTNET_NOLOGO="1", DOTNET_CLI_HOME=str(directory / "sdk-home"),
                   NUGET_PACKAGES=str(directory / "packages"), TMPDIR=str(directory))
        result = subprocess.run(["dotnet", "run", "--project", str(project), "--", str(directory)],
                                cwd=directory, env=env, timeout=90)
        raise SystemExit(result.returncode or (1 if contract_failures else 0))


if __name__ == "__main__":
    main()
