#!/usr/bin/env python3
"""Exercise extracted exporter production code with C# 4 and API doubles.

Run: python3 layout-editor/scripts/check-export-contracts.py
Requires dotnet SDK 8; no Unity, export, NuGet packages, or repository outputs.
Full methods cover dependency ordering, build ownership, and dependency delivery.
Feature checks execute the complete CollectActiveSceneStubFeatures method with
scene API doubles, including pure-vanilla PartSlots and empty/absent slot roots.
The mock removes preview children; Unity's Prepare/serialization are not executed.
"""
import os
from pathlib import Path
import re
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Editor/LayoutEditor/LayoutEditorSetExporter.cs"
METHODS = [
    "IsBaseGameBundle", "OrderDependencyBundles", "VisitDependencyBundle", "CompareSemver",
    "CollectBuildOwnership", "AddBundleName", "BuildListedBundles",
    "AddDependencyEntries", "TryGetCommonWIndex", "DeleteStaleDependencyOutput",
]


def extract(source, declaration):
    matches = list(re.finditer(declaration, source))
    if len(matches) != 1:
        raise AssertionError("Expected one production declaration: " + declaration)
    match = matches[0]
    start = source.index("{", match.start())
    depth = 0
    # Ignore braces inside strings, characters, and comments, as in loader checks.
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
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;
using LevelEditorStub;

// These doubles describe API inputs/outputs; they do not reimplement the sorter
// or feature classifier. Unexpected API calls fail rather than silently succeed.
static class AssetDatabase
{
    internal static readonly Dictionary<string, string[]> Dependencies =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    internal static readonly Dictionary<string, string[]> Assets =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    internal static readonly List<string> Queries = new List<string>();
    internal static readonly HashSet<string> Folders = new HashSet<string>();
    public static string[] GetAssetBundleDependencies(string name, bool recursive)
    {
        if (recursive) throw new Exception("Expected direct dependency query");
        Queries.Add(name);
        string[] result;
        if (!Dependencies.TryGetValue(name, out result))
            throw new Exception("Unexpected bundle query: " + name);
        return result == null ? null : (string[])result.Clone();
    }
    public static string[] GetAssetPathsFromAssetBundle(string name)
    {
        string[] result;
        if (!Assets.TryGetValue(name, out result))
            throw new Exception("Unexpected asset query: " + name);
        return result;
    }
    public static bool IsValidFolder(string path) { return Folders.Contains(path); }
}
class AssetBundleManifest
{
    internal string[] Produced = new string[0];
    internal readonly Dictionary<string, string[]> Dependencies =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    public string[] GetAllAssetBundles() { return Produced; }
    public string[] GetAllDependencies(string name)
    {
        string[] result;
        return Dependencies.TryGetValue(name, out result) ? result : new string[0];
    }
}
struct AssetBundleBuild { public string assetBundleName; public string[] assetNames; }
enum BuildAssetBundleOptions { None }
enum BuildTarget { StandaloneWindows }
static class BuildPipeline
{
    internal static AssetBundleManifest Result;
    internal static AssetBundleBuild[] Builds;
    public static AssetBundleManifest BuildAssetBundles(string root, AssetBundleBuild[] builds,
        BuildAssetBundleOptions options, BuildTarget target)
    {
        if (!LayoutEditorPseudoReload.ExportBuildInProgress)
            throw new Exception("Build guard missing");
        Builds = builds;
        return Result;
    }
}
static class LayoutEditorPseudoReload
{
    internal static bool ExportBuildInProgress;
    internal static bool LastRequireManager;
    public static void ForcePrepareForExportStrict(bool requireManager)
    { LastRequireManager = requireManager; }
}
static class Debug
{
    public static void Log(string message) { }
    public static void LogWarning(string message) { }
}
static class LayoutEditorZipWriter
{
    public class ZipEntrySource
    {
        internal string Name, Path;
        public ZipEntrySource(string name, string path) { Name = name; Path = path; }
    }
}
namespace UnityEngine
{
    class Object
    {
        internal static PseudoPrefabStub[] Stubs = new PseudoPrefabStub[0];
        public static T[] FindObjectsOfType<T>()
        {
            if (typeof(T) == typeof(PseudoPrefabStub)) return (T[])(object)Stubs;
            if (typeof(T) == typeof(MonoBehaviour) || typeof(T) == typeof(Transform)
                || typeof(T) == typeof(SpecificPseudoPrefabTag)
                || typeof(T) == typeof(SetupCannonStub)
                || typeof(T) == typeof(SetupCustomPrefabStub)
                || typeof(T) == typeof(DestroyComponentOfChildStub)
                || typeof(T) == typeof(PseudoPrefabSwitchStub)) return new T[0];
            throw new Exception("Unexpected scene API type: " + typeof(T));
        }
    }
    class MonoBehaviour : Object { public GameObject gameObject; }
    class Transform : Object { public string name; public int childCount; }
    class GameObject : Object
    {
        internal static readonly Dictionary<string, GameObject> Roots = new Dictionary<string, GameObject>();
        public string name;
        public Transform transform = new Transform();
        public static GameObject Find(string path)
        { GameObject result; return Roots.TryGetValue(path, out result) ? result : null; }
    }
}
namespace UnityEngine.SceneManagement
{
    struct Scene { public string path; }
    static class SceneManager { public static Scene GetActiveScene() { return new Scene(); } }
}
namespace LevelEditorStub
{
    class SpecificPseudoPrefabTag { public string prefabTag; }
    class SetupCannonStub { }
    class SetupCustomPrefabStub { }
    class DestroyComponentOfChildStub { }
    class PseudoPrefabSwitchStub { public bool startEnabled; }
}
class LevelInfoSO { }
static class LayoutEditorLevelInfoResolver
{ public static LevelInfoSO ResolveForScene(string path) { return null; } }
static class LayoutEditorSaladSauceFactory
{ public static bool UsesSauces(LevelInfoSO info) { return false; } }
class PseudoPrefabSO { internal string prefabName; }
class PseudoPrefabStub
{
    internal PseudoPrefabSO pseudoPrefabSO;
    internal string name;
    internal object childGameObject;
}
class PseudoPrefabTerminalStub : PseudoPrefabStub { internal object pilotableObject; }

class ExportContracts
{
    static bool AssetTextReferencesCommonW(string path) { return false; }
    const string BundlesRoot = "bundles";
    static string RuntimeBundleName;
    static string root;
    static int checks, failures;
    static AssetBundleManifest _buildManifest;
    static List<string> _builtBundleNames, _dependencyBundleNames;
    static string AbsPath(string path) { return Path.Combine(root, path); }
    static string ProjectRootAbsPath() { return root; }
    static string ExportRootAbsPath() { return root; }
    static void SyncLoaderDllForExport() { }
    static void EnsureLoaderMatchesRuntime() { }
    static void Check(bool ok, string label)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("FAIL: " + label); }
    }
    static void Reject(Action action, string message, string label)
    {
        Exception caught = null;
        try { action(); } catch (Exception ex) { caught = ex; }
        Check(caught != null && caught.Message.Contains(message), label);
    }
    static void Graph(params string[] names)
    {
        AssetDatabase.Dependencies.Clear();
        AssetDatabase.Queries.Clear();
        foreach (var name in names) AssetDatabase.Dependencies[name] = new string[0];
    }
    static List<string> Order(params string[] names)
    { return OrderDependencyBundles(new List<string>(names)); }
    static string Join(List<string> names) { return string.Join(",", names.ToArray()); }
    static void Product(string name)
    {
        var path = AbsPath(BundlesRoot) + "/" + name;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, "fixture, not an actual Unity bundle");
    }
    static void BuildFixture(params string[] names)
    {
        Graph(names);
        AssetDatabase.Assets.Clear();
        AssetDatabase.Folders.Clear();
        BuildPipeline.Builds = null;
        BuildPipeline.Result = new AssetBundleManifest();
        BuildPipeline.Result.Produced = names;
        foreach (var name in names)
        {
            AssetDatabase.Assets[name] = new[] { "Assets/fixture/" + name + ".asset" };
            BuildPipeline.Result.Dependencies[name] = new string[0];
            Product(name);
        }
        _buildManifest = null;
        _builtBundleNames = null;
    }
    static List<LayoutEditorZipWriter.ZipEntrySource> Deliver()
    {
        var entries = new List<LayoutEditorZipWriter.ZipEntrySource>();
        AddDependencyEntries(entries, new List<string> { "fixture" }, true);
        return entries;
    }
    static void DependencyChecks()
    {
        Check(CompareSemver("4.1.0", "4.0.0") >= 0,
            "Loader release newer than Runtime SSOT is compatible");
        Check(CompareSemver("4.0.0", "4.1.0") < 0,
            "Loader release below Runtime SSOT is incompatible");
        Check(CompareSemver("4.1.0", "4.1.0") == 0,
            "equal Loader and Runtime versions are compatible");
        foreach (var name in new[] { "common", "COMMON01", "common01_textures", "COMMON01_TEXTURES", "common02/bundle18", "common03", "common04/hash" })
            Check(IsBaseGameBundle(name), "base prerequisite recognized: " + name);
        foreach (var name in new[] { "commonw1", "commonW2", "common05", "common010", "common04hash", "common01_textures_extra", "hash_abc123" })
            Check(!IsBaseGameBundle(name), "web/hash not classified as base: " + name);
        Graph("commonw1", "commonw2", "commonw5", "hash_abc123");
        AssetDatabase.Dependencies["commonw1"] = new[] { "commonw2" };
        AssetDatabase.Dependencies["commonw5"] = new[] { "hash_abc123" };
        Check(Join(Order("commonw1")) == "commonw2,commonw1", "W1 -> W2 dependency precedes parent");
        Check(Join(Order("commonw5")) == "hash_abc123,commonw5", "W5 -> hash closure delivered");
        AssetDatabase.Dependencies["commonw1"] = new[] { "commonw5", "commonw2", "COMMONW2" };
        AssetDatabase.Dependencies["commonw2"] = new[] { "hash_abc123" };
        var ordered = Order("commonw1", "COMMONW1", "commonw5", "hash_abc123");
        Check(Join(ordered) == "hash_abc123,commonw2,commonw5,commonw1", "diamond, duplicate edges/roots, case-insensitive dedup and sorted siblings");
        foreach (var pair in AssetDatabase.Dependencies)
            foreach (var dep in pair.Value)
                Check(ordered.FindIndex(delegate(string n) { return n.Equals(dep, StringComparison.OrdinalIgnoreCase); })
                    < ordered.FindIndex(delegate(string n) { return n.Equals(pair.Key, StringComparison.OrdinalIgnoreCase); }),
                    "topological edge " + pair.Key + " -> " + dep);
        AssetDatabase.Queries.Clear();
        Order("commonw1", "COMMONW1", "commonw2");
        Check(AssetDatabase.Queries.Count == 4, "shared dependencies queried once, direct queries only");
        Graph("commonw1");
        AssetDatabase.Dependencies["commonw1"] = new[] { "common", "common01/bundle18", "COMMON04/hash" };
        Check(Join(Order("commonw1", "common02")) == "commonw1"
            && AssetDatabase.Queries.Count == 1, "base prerequisite remains external and is never traversed/rebuilt");
        Graph("commonw2", "common01", "common01_textures", "common");
        AssetDatabase.Dependencies["commonw2"] = new[] { "common01" };
        AssetDatabase.Dependencies["common01"] = new[] { "common01_textures", "common" };
        Check(Join(Order("commonw2")) == "commonw2"
            && AssetDatabase.Queries.Count == 2,
            "traverse common01 as prerequisite root but skip its externally supplied texture bundle");
        Graph("commonw2", "common01", "common01_textures", "texture_support", "common");
        AssetDatabase.Dependencies["commonw2"] = new[] { "common01" };
        AssetDatabase.Dependencies["common01"] = new[] { "common01_textures", "common" };
        AssetDatabase.Dependencies["common01_textures"] = new[] { "texture_support" };
        Check(Join(Order("commonw2")) == "commonw2"
            && AssetDatabase.Queries.Count == 2,
            "externally supplied common01_textures and its dependencies are not delivered");
        foreach (var invalid in new[] { "../escape", "sub/file", "sub\\file", ".", "..", "name.manifest", "C:bundle", "a|b", " name", "name " })
        {
            Reject(delegate { Order(invalid); }, "文件名", "reject illegal root: " + invalid);
            AssetDatabase.Dependencies["commonw1"] = new[] { invalid };
            Reject(delegate { Order("commonw1"); }, "文件名", "reject illegal transitive path: " + invalid);
        }
        Graph("commonw1", "commonw2", "hash_abc123");
        AssetDatabase.Dependencies["commonw1"] = new[] { "COMMONW1" };
        Reject(delegate { Order("commonw1"); }, "循环", "case-insensitive self cycle");
        AssetDatabase.Dependencies["commonw1"] = new[] { "commonw2" };
        AssetDatabase.Dependencies["commonw2"] = new[] { "hash_abc123" };
        AssetDatabase.Dependencies["hash_abc123"] = new[] { "commonw1" };
        Reject(delegate { Order("commonw1"); }, "循环", "three-node cycle");
        AssetDatabase.Dependencies["hash_abc123"] = null;
        Check(Join(Order("commonw1")) == "hash_abc123,commonw2,commonw1", "fresh traversal after rejected cycle; null dependencies are leaves");
        Check(Order().Count == 0, "empty graph");
    }
    static void OwnershipChecks()
    {
        BuildFixture("fixture/s_selected", "fixture/info_fixture", "commonw1", "commonw2", "game_common01_bundle18");
        AssetDatabase.Dependencies["fixture/s_selected"] = new[] { "commonw1" };
        AssetDatabase.Dependencies["fixture/info_fixture"] = new[] { "COMMONW1" };
        AssetDatabase.Dependencies["commonw1"] = new[] { "commonw2" };
        AssetDatabase.Dependencies["commonw2"] = new[] { "game_common01_bundle18" };
        var roots = new List<string> { "fixture/s_selected", "fixture/info_fixture" };
        var expected = "fixture/s_selected,fixture/info_fixture,commonw1,commonw2,game_common01_bundle18";
        Check(Join(CollectBuildOwnership(roots)) == expected, "levels -> W1 -> W2 -> base ownership closure");
        AssetDatabase.Queries.Clear();
        BuildListedBundles(roots, true);
        var mapped = new List<string>();
        foreach (var build in BuildPipeline.Builds)
        {
            mapped.Add(build.assetBundleName);
            Check(build.assetNames.Length == 1
                && build.assetNames[0] == AssetDatabase.Assets[build.assetBundleName][0],
                "asset stays in original bundle map: " + build.assetBundleName);
        }
        Check(Join(mapped) == expected && AssetDatabase.Queries.Count == 5, "shared and base ownership built once");
        Check(Join(_builtBundleNames) == "fixture/s_selected,fixture/info_fixture", "levels delivery roots exclude shared/base ownership");
        Check(!object.ReferenceEquals(roots, _builtBundleNames), "delivery roots copied independently of caller list");
        roots.Add("unrequested");
        Check(_builtBundleNames.Count == 2, "caller mutation cannot add an ownership bundle to delivery");

        Graph("fixture/s_selected", "commonw1", "game_common01_bundle18");
        AssetDatabase.Dependencies["fixture/s_selected"] = new[] { "COMMONW1", "commonw1" };
        AssetDatabase.Dependencies["commonw1"] = new[] { "game_common01_bundle18", "FIXTURE/S_SELECTED" };
        AssetDatabase.Dependencies["game_common01_bundle18"] = new[] { "COMMONW1", "game_common01_bundle18" };
        Check(Join(CollectBuildOwnership(new List<string> { "FIXTURE\\s_selected", "fixture/s_selected" }))
            == "fixture/s_selected,commonw1,game_common01_bundle18", "ownership cycle, self edge and case-duplicate roots terminate with unique normalized names");
        Check(AssetDatabase.Queries.Count == 3, "cyclic ownership queries each bundle once");
        Graph("leaf");
        AssetDatabase.Dependencies["leaf"] = null;
        Check(Join(CollectBuildOwnership(new List<string> { "", null, "LEAF" })) == "leaf", "ownership ignores empty roots and accepts null dependency leaf");
        Check(CollectBuildOwnership(null).Count == 0, "null ownership roots");
        Check(CollectBuildOwnership(new List<string>()).Count == 0, "empty ownership roots");

        BuildFixture("fixture/s_selected", "commonw1");
        AssetDatabase.Dependencies["fixture/s_selected"] = new[] { "commonw1" };
        AssetDatabase.Assets["commonw1"] = new string[0];
        Reject(delegate { BuildListedBundles(new List<string> { "fixture/s_selected" }, true); },
            "无资产", "missing shared ownership source rejects stale disk product");
        Check(BuildPipeline.Builds == null, "missing shared source fails before BuildPipeline");
        AssetDatabase.Assets["commonw1"] = new[] { "Assets/shared_folder" };
        AssetDatabase.Folders.Add("Assets/shared_folder");
        Reject(delegate { BuildListedBundles(new List<string> { "fixture/s_selected" }, true); },
            "只有文件夹", "folder-only shared ownership rejects build");

        BuildFixture("fixture/s_selected", "commonw1");
        AssetDatabase.Dependencies["fixture/s_selected"] = new[] { "commonw1" };
        BuildPipeline.Result.Produced = new[] { "fixture/s_selected" };
        Reject(delegate { BuildListedBundles(new List<string> { "fixture/s_selected" }, true); },
            "未产出预期 bundle: commonw1", "ownership-only bundle also requires current manifest membership");
        Check(!LayoutEditorPseudoReload.ExportBuildInProgress, "ownership validation failure restores build guard");
    }
    static void BuildAndDeliveryChecks()
    {
        BuildFixture("commonw1", "commonw2", "hash_abc123", RuntimeBundleName,
            "common01", "common01_textures");
        AssetDatabase.Dependencies["commonw1"] = new[] { "commonw2", "hash_abc123" };
        AssetDatabase.Dependencies["commonw2"] = new[] { "common01", "common01_textures" };
        Product("unlisted_stale");
        Product("commonw1.manifest");
        Product("commonw1.meta");
        var names = new List<string> { "hash_abc123", "commonw2", "commonw1", RuntimeBundleName };
        BuildListedBundles(names, false);
        Check(object.ReferenceEquals(_buildManifest, BuildPipeline.Result), "keep actual returned manifest for delivery");
        Check(Join(_builtBundleNames) == Join(names) && BuildPipeline.Builds.Length == 6,
            "delivery roots preserved while external base prerequisites participate in build map");
        Check(!LayoutEditorPseudoReload.ExportBuildInProgress && !LayoutEditorPseudoReload.LastRequireManager, "build guard restored; deps need no manager");
        _dependencyBundleNames = names;
        _buildManifest.Dependencies["commonw1"] = new[] { "commonw2", "hash_abc123", "common01", "common01_textures" };
        _buildManifest.Dependencies["commonw2"] = new[] { "common01", "common01_textures" };
        var entries = Deliver();
        Check(entries.Count == 5, "all explicit bundles and dependency manifest delivered");
        Check(entries[0].Name == "OC2DIYLevelRuntimeWLoader/hash_abc123"
            && entries[1].Name == "OC2DIYLevelRuntimeWLoader/commonW2"
            && entries[2].Name == "OC2DIYLevelRuntimeWLoader/commonW1", "zip preserves hash and commonW dependency order without external base textures");
        Check(File.ReadAllText(ExportRootAbsPath() + "/._bundle_dependencies.txt").Replace("\r", "")
            == "hash_abc123\ncommonW2\ncommonW1\n", "manifest excludes external base bundles/runtime while retaining web hash dependencies");
        Check(File.Exists(AbsPath(BundlesRoot) + "/commonw1.manifest")
            && File.Exists(AbsPath(BundlesRoot) + "/unlisted_stale"), "incremental cache preserved without delivery leakage");
        _buildManifest.Dependencies["commonw1"] = new[] { "not_in_closure" };
        Reject(delegate { Deliver(); }, "闭包不完整", "manifest reveals omitted transitive bundle");
        _buildManifest.Dependencies["commonw1"] = new string[0];
        var produced = _buildManifest.Produced;
        _buildManifest.Produced = new[] { "commonw1", "commonw2", "common01_textures", RuntimeBundleName };
        Reject(delegate { Deliver(); }, "未出现在本次构建 manifest: hash_abc123",
            "delivery independently rejects existing product absent from current manifest");
        _buildManifest.Produced = produced;
        File.Delete(AbsPath(BundlesRoot) + "/hash_abc123");
        Reject(delegate { Deliver(); }, "产物缺失", "missing dependency product stops delivery");
        _buildManifest = null;
        Reject(delegate { Deliver(); }, "清单缺失", "old directory cannot replace current manifest");
        _buildManifest = new AssetBundleManifest();
        _dependencyBundleNames = null;
        Reject(delegate { Deliver(); }, "清单缺失", "missing current dependency list rejects delivery");

        BuildFixture("commonw1");
        BuildPipeline.Result.Produced = new string[0];
        Reject(delegate { BuildListedBundles(new List<string> { "commonw1" }, true); }, "未产出", "stale file cannot replace manifest membership");
        Check(!LayoutEditorPseudoReload.ExportBuildInProgress && LayoutEditorPseudoReload.LastRequireManager, "guard restored on manifest failure");
        BuildFixture("commonw1");
        File.Delete(AbsPath(BundlesRoot) + "/commonw1");
        Reject(delegate { BuildListedBundles(new List<string> { "commonw1" }, false); }, "未产出", "manifest membership cannot replace missing file");
        Check(!LayoutEditorPseudoReload.ExportBuildInProgress, "guard restored on file failure");
        BuildFixture("commonw1");
        BuildPipeline.Result = null;
        Reject(delegate { BuildListedBundles(new List<string> { "commonw1" }, false); }, "返回 null", "null manifest rejects build");
        Check(!LayoutEditorPseudoReload.ExportBuildInProgress, "guard restored on null manifest");
        BuildFixture("commonw1");
        AssetDatabase.Assets["commonw1"] = new[] { "Assets/fixture", "Assets/fixture/a.asset" };
        AssetDatabase.Folders.Add("Assets/fixture");
        BuildListedBundles(new List<string> { "commonw1" }, false);
        Check(BuildPipeline.Builds[0].assetNames.Length == 1
            && BuildPipeline.Builds[0].assetNames[0] == "Assets/fixture/a.asset", "build excludes folders");
        AssetDatabase.Assets["commonw1"] = new string[0];
        Reject(delegate { BuildListedBundles(new List<string> { "commonw1" }, false); }, "无资产", "all requested sources empty rejects build");
        AssetDatabase.Assets["commonw1"] = new[] { "Assets/fixture" };
        Reject(delegate { BuildListedBundles(new List<string> { "commonw1" }, false); }, "只有文件夹", "folder-only build rejects stale products");

        // Keep regressions for formerly skipped source-less runtime/scene roots.
        // Supply graph leaves so rejection must come from production source checks.
        BuildFixture("commonw1");
        Product(RuntimeBundleName);
        AssetDatabase.Dependencies[RuntimeBundleName] = new string[0];
        AssetDatabase.Assets[RuntimeBundleName] = new string[0];
        _dependencyBundleNames = new List<string> { "commonw1", RuntimeBundleName };
        Reject(delegate {
            BuildListedBundles(_dependencyBundleNames, false);
            Deliver();
        }, RuntimeBundleName, "requested runtime absent from current manifest must not ship stale disk product");
        Check(BuildPipeline.Builds == null, "missing runtime source fails before BuildPipeline");

        BuildFixture("fixture/info_fixture");
        AssetDatabase.Dependencies["fixture/s_selected"] = new string[0];
        AssetDatabase.Assets["fixture/s_selected"] = new string[0];
        Reject(delegate {
            BuildListedBundles(new List<string> { "fixture/info_fixture", "fixture/s_selected" }, true);
        }, "s_selected", "selected level with no source assets must not silently disappear from delivery list");
        Check(BuildPipeline.Builds == null, "missing selected scene source fails before BuildPipeline");
    }
    static PseudoPrefabStub Stub(string prefabName, bool terminal, bool bound)
    {
        PseudoPrefabStub stub = terminal ? new PseudoPrefabTerminalStub() : new PseudoPrefabStub();
        stub.name = "renamed wrapper";
        stub.childGameObject = new object();
        stub.pseudoPrefabSO = new PseudoPrefabSO();
        stub.pseudoPrefabSO.prefabName = prefabName;
        if (terminal && bound) ((PseudoPrefabTerminalStub)stub).pilotableObject = new object();
        return stub;
    }
    static void Features(PseudoPrefabStub stub, string expected, string label)
    {
        // Model the post-Prepare state, without pretending to execute Prepare.
        if (stub != null) stub.childGameObject = null;
        UnityEngine.Object.Stubs = new[] { stub };
        var actual = CollectActiveSceneStubFeatures();
        actual.Sort(StringComparer.Ordinal);
        Check(Join(actual) == expected, label);
    }
    static void FeatureChecks()
    {
        Features(Stub("web_utensil_large_pot_01", false, false), "hotpot", "renamed static pot SO survives removed child");
        Features(Stub("WEB_utensil_large_POT_01_PUSHABLE", false, false), "hotpot,pushable", "renamed pushable pot SO survives removed child, case-insensitive");
        Features(Stub("web_pushable_object", false, false), "hotpot,pushable", "pushable_object SO fallback");
        Features(Stub("ControlTerminal", false, false), "terminal", "unbound ControlTerminal on base Stub activates terminal");
        Features(Stub("controlterminal", true, false), "terminal", "unbound specialized terminal activates terminal");
        Features(Stub("ControlTerminal", true, true), "", "bound terminal does not activate terminal");
        Features(Stub("ordinary_counter", false, false), "", "ordinary object has no features");
        Features(Stub("ordinary_counter", true, false), "terminal", "unbound terminal subtype retains its persisted identity");
        Features(Stub("ControlTerminal_Marker", false, false), "", "joystick lamp marker is not a terminal");
        Features(Stub(null, false, false), "", "null prefab name");
        Features(new PseudoPrefabStub(), "", "missing SO");
        Features(null, "", "missing Stub");
        var pot = Stub("web_utensil_large_pot_01", false, false);
        pot.childGameObject = null;
        UnityEngine.Object.Stubs = new[] { pot, pot, Stub("ControlTerminal", false, false) };
        var features = CollectActiveSceneStubFeatures();
        features.Sort(StringComparer.Ordinal);
        Check(Join(features) == "hotpot,terminal", "repeated persisted Stubs deduplicate features");
        UnityEngine.Object.Stubs = new[] { Stub("ordinary_counter", false, false) };
        Check(CollectActiveSceneStubFeatures().Count == 0, "vanilla ordinary scene stays dormant");
        GameObject.Roots["Design/PartSlots"] = new GameObject();
        Check(CollectActiveSceneStubFeatures().Count == 0, "empty PartSlots stays dormant");
        GameObject.Roots["Design/PartSlots"].transform.childCount = 1;
        Check(Join(CollectActiveSceneStubFeatures()) == "web", "pure vanilla part scene activates existing web gate");
        GameObject.Roots.Remove("Design/PartSlots");
        GameObject.Roots["PartSlots"] = new GameObject();
        GameObject.Roots["PartSlots"].transform.childCount = 2;
        Check(CollectActiveSceneStubFeatures().Count == 0, "unrelated PartSlots name stays dormant");
        GameObject.Roots.Clear();
    }
    static int Main(string[] args)
    {
        root = args[0];
        RuntimeBundleName = args[1];
        DependencyChecks();
        OwnershipChecks();
        BuildAndDeliveryChecks();
        FeatureChecks();
        Console.WriteLine((failures == 0 ? "PASS: " : "FAILED: ") + checks
            + " exporter contract checks, " + failures + " failures (production code, C# 4).");
        Console.WriteLine("Feature scope: complete production collector with scene API doubles; not Unity Prepare/bundle serialization.");
        return failures == 0 ? 0 : 1;
    }
'''


def main():
    source = SOURCE.read_text(encoding="utf-8")
    members = [extract(source, r"    private static [^\n]+\b" + name + r"\(")
               for name in METHODS]
    collect = extract(source, r"    public static List<string> CollectActiveSceneStubFeatures\(")
    members.append(collect)
    for name in ("StubTagFeatureMap", "StubComponentFeatureMap"):
        members.append(extract(source, r"    private static readonly Dictionary<string, string> " + name + r"\s*=") + ";")
    entry = (ROOT / "Assets/WebCustomStubRuntime/core/EntryPoint.cs").read_text(encoding="utf-8")
    process = extract(entry, r"        private static void ProcessScene\(")
    assert process.index("if (lookup == LevelMiss)") < process.index("s_activeFeatures = features;")
    assert 'ActivateCore("stub 清单命中 ' in process
    assert process.index("s_activeFeatures = features;") < process.rindex("HealScene(scene);")
    heal = extract(entry, r"        internal static void HealScene\(")
    assert 'var hasPartSlots = GameObject.Find("Design/PartSlots") != null;' in heal
    assert "PartSlotRespawnGuard.OnSceneChanged(hasPartSlots);" in heal
    assert re.search(r"if \(hasPartSlots\)\s+PartSlotRespawnGuard.EnsurePatches\(\);", heal)
    print("PASS: EntryPoint manifest hit reaches existing PartSlotRespawnGuard installation (source contract).", flush=True)
    runtime = re.search(r'private const string RuntimeBundleName = "([^"]+)";', source)
    if not runtime:
        raise AssertionError("Missing production RuntimeBundleName")
    # All C# sources, build outputs and fixture products stay outside the repo.
    with tempfile.TemporaryDirectory(prefix="export-contracts-") as temp:
        directory = Path(temp)
        project = directory / "Contracts.csproj"
        project.write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
            '<LangVersion>4</LangVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo>'
            '<EnableNETAnalyzers>false</EnableNETAnalyzers>'
            '</PropertyGroup></Project>', encoding="utf-8")
        (directory / "Program.cs").write_text(DOUBLES + "\n".join(members) + "\n}", encoding="utf-8")
        env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1")
        result = subprocess.run(["dotnet", "run", "--project", str(project), "--",
                                 str(directory), runtime.group(1)], env=env, timeout=90)
        raise SystemExit(result.returncode)


if __name__ == "__main__":
    main()
