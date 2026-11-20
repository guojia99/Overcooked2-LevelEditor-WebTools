#!/usr/bin/env python3
"""Run complete cooking-guard cache probes against C# 4 / net8 API doubles.

Run: python3 layout-editor/scripts/check-cooking-guard-cache.py
Requires dotnet SDK 8; no Unity, packages, scene writes, or repository outputs.
Extracts LoadedPrefabOrNull, all three probes, and BundleDictionaryField verbatim.
Two builds model the private cache field being present and unavailable, without
patching the extracted members or assigning a static readonly field via reflection.
This exercises probe contracts, not Unity Prepare, native loading, or scene repair.
"""
import os
from pathlib import Path
import re
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Editor/LayoutEditor/LayoutEditorCookingUtensilGuard.cs"
METHODS = (
    "LoadedPrefabOrNull",
    "RealPrefabHasIngredientContainer",
    "RealPrefabIsSpawnableIngredient",
    "PlatePrefabHasGridSnap",
)
TOKENS = r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/|[{};]'


def extract(source, declaration, field=False):
    matches = list(re.finditer(declaration, source))
    if len(matches) != 1:
        raise AssertionError("Expected one production declaration: " + declaration)
    start = matches[0].start()
    depth = 0
    for token in re.finditer(TOKENS, source[start:]):
        value = token.group()
        if value == "{":
            depth += 1
        elif value == "}":
            depth -= 1
            if not field and depth == 0:
                return source[start:start + token.end()]
        elif field and value == ";" and depth == 0:
            return source[start:start + token.end()]
    raise AssertionError("Unterminated production declaration: " + declaration)


HARNESS = r'''
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using LevelEditor;
using LevelEditorStub;

static class Calls
{
    internal static int Getter, Init, DeInit, BundleLoad, SceneWrites, AssetLoad;
    internal static readonly List<string> AssetPaths = new List<string>();
    internal static Exception Forbidden(string operation)
    { return new Exception("Forbidden host operation: " + operation); }
}
namespace UnityEngine
{
    class Object
    {
        internal bool Destroyed;
        public static bool operator ==(Object left, Object right)
        {
            bool a = ReferenceEquals(left, null) || left.Destroyed;
            bool b = ReferenceEquals(right, null) || right.Destroyed;
            return a || b ? a == b : ReferenceEquals(left, right);
        }
        public static bool operator !=(Object left, Object right) { return !(left == right); }
        public override bool Equals(object value) { return ReferenceEquals(this, value); }
        public override int GetHashCode() { return base.GetHashCode(); }
        public static Object Instantiate(Object original)
        { Calls.SceneWrites++; throw Calls.Forbidden("Instantiate"); }
        public static void DestroyImmediate(Object target)
        { Calls.SceneWrites++; throw Calls.Forbidden("DestroyImmediate"); }
    }
    class ScriptableObject : Object { }
    class OrderDefinitionNode : ScriptableObject { }
    class Component : Object { internal GameObject gameObject; }
    class GameObject : Object
    {
        internal readonly Dictionary<string, Component> Components = new Dictionary<string, Component>();
        internal bool ThrowOnGetComponent;
        public Component GetComponent(string name)
        {
            if (Destroyed || ThrowOnGetComponent) throw new Exception("Unavailable component");
            Component component;
            return Components.TryGetValue(name, out component) ? component : null;
        }
        public T AddComponent<T>() where T : Component
        { Calls.SceneWrites++; throw Calls.Forbidden("AddComponent"); }
    }
    class AssetBundle : Object
    {
        internal Object Asset;
        internal bool ThrowOnLoad;
        public T LoadAsset<T>(string path) where T : Object
        {
            Calls.AssetLoad++;
            Calls.AssetPaths.Add(path);
            if (Destroyed || ThrowOnLoad) throw new Exception("LoadAsset unavailable");
            return Asset as T;
        }
        public static AssetBundle LoadFromFile(string path)
        { Calls.BundleLoad++; throw Calls.Forbidden("LoadFromFile"); }
    }
}
namespace LevelEditorStub
{
    class PseudoPrefabSO : ScriptableObject
    {
        public string prefabName, bundleName, assetPath;
        public UnityEngine.Object asset;
    }
}
namespace LevelEditor
{
    class PseudoPrefabManager : UnityEngine.Object
    {
        public static PseudoPrefabManager Instance;
#if NO_BUNDLE_FIELD
        // Deliberately no field called bundleDict: production GetField returns null.
        private Dictionary<string, AssetBundle> unavailableCache;
        internal Dictionary<string, AssetBundle> FixtureCache { get { return unavailableCache; } }
        internal PseudoPrefabManager(Dictionary<string, AssetBundle> cache) { unavailableCache = cache; }
#else
        private Dictionary<string, AssetBundle> bundleDict;
        internal Dictionary<string, AssetBundle> FixtureCache { get { return bundleDict; } }
        internal PseudoPrefabManager(Dictionary<string, AssetBundle> cache) { bundleDict = cache; }
#endif
        public static AssetBundle GetAssetBundle(string name)
        { Calls.Getter++; throw Calls.Forbidden("GetAssetBundle (loading getter)"); }
        public void Init() { Calls.Init++; throw Calls.Forbidden("Init"); }
        public void DeInit() { Calls.DeInit++; throw Calls.Forbidden("DeInit"); }
        public static void ResetAllPseudoPrefabs()
        { Calls.SceneWrites++; throw Calls.Forbidden("ResetAllPseudoPrefabs"); }
        public static void ClearAllPseudoPrefabs()
        { Calls.SceneWrites++; throw Calls.Forbidden("ClearAllPseudoPrefabs"); }
    }
}

class CookingGuardCacheChecks
{
    static int checks, failures;
#if NO_BUNDLE_FIELD
    static readonly bool HasField = false;
#else
    static readonly bool HasField = true;
#endif
    static void Check(bool ok, string label)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("FAIL: " + label); }
    }
    static GameObject Prefab(params string[] names)
    {
        var prefab = new GameObject();
        foreach (string name in names)
            prefab.Components[name] = new Component { gameObject = prefab };
        return prefab;
    }
    static PseudoPrefabSO So(UnityEngine.Object asset)
    {
        return new PseudoPrefabSO { prefabName = "fixture", bundleName = "common03/bundle47",
            assetPath = "assets/fixture.prefab", asset = asset };
    }
    static void Manager(AssetBundle bundle, bool includeKey)
    {
        var cache = new Dictionary<string, AssetBundle>();
        cache["unrelated"] = new AssetBundle();
        if (includeKey) cache["common03/bundle47"] = bundle;
        PseudoPrefabManager.Instance = new PseudoPrefabManager(cache);
    }
    static void Case(string label, PseudoPrefabSO so, GameObject expected,
        bool container, bool spawn, bool plate, int loads, bool helperThrows)
    {
        var manager = PseudoPrefabManager.Instance;
        var cache = ReferenceEquals(manager, null) ? null : manager.FixtureCache;
        var before = cache == null ? null : new Dictionary<string, AssetBundle>(cache);
        var asset = ReferenceEquals(so, null) ? null : so.asset;
        string bundleName = ReferenceEquals(so, null) ? null : so.bundleName;
        string assetPath = ReferenceEquals(so, null) ? null : so.assetPath;
        int loaded = Calls.AssetLoad;
        Calls.AssetPaths.Clear();
        GameObject actual = null;
        Exception caught = null;
        try { actual = LoadedPrefabOrNull(so); } catch (Exception ex) { caught = ex; }
        Check(helperThrows ? caught != null : caught == null && ReferenceEquals(actual, expected),
            label + ": helper result/exception");
        Check(RealPrefabHasIngredientContainer(so) == container, label + ": container");
        Check(RealPrefabIsSpawnableIngredient(so) == spawn, label + ": spawnable");
        Check(PlatePrefabHasGridSnap(so) == plate, label + ": plate");
        Check(Calls.AssetLoad - loaded == loads, label + ": loaded-cache asset reads only");
        Check(Calls.AssetPaths.TrueForAll(delegate(string path) { return path == assetPath; }),
            label + ": exact SO asset path");
        Check(Calls.Getter == 0, label + ": loading getter calls = 0");
        Check(Calls.Init == 0, label + ": native Init calls = 0");
        Check(Calls.DeInit == 0, label + ": native DeInit calls = 0");
        Check(Calls.BundleLoad == 0, label + ": bundle file loads = 0");
        Check(Calls.SceneWrites == 0, label + ": scene writes = 0");
        bool unchanged = ReferenceEquals(manager, PseudoPrefabManager.Instance)
            && (ReferenceEquals(manager, null) || ReferenceEquals(cache, manager.FixtureCache));
        if (cache != null)
        {
            unchanged = unchanged && before.Count == cache.Count;
            foreach (var pair in before)
            {
                AssetBundle value;
                unchanged = unchanged && cache.TryGetValue(pair.Key, out value)
                    && ReferenceEquals(value, pair.Value);
            }
        }
        Check(unchanged, label + ": manager/cache unchanged");
        Check(ReferenceEquals(so, null) || ReferenceEquals(asset, so.asset)
            && bundleName == so.bundleName && assetPath == so.assetPath,
            label + ": SO reference/paths unchanged");
    }
    static void Unknown(string label, PseudoPrefabSO so)
    { Case(label, so, null, true, true, true, 0, false); }
    static void Cached(string label, PseudoPrefabSO so, GameObject prefab,
        bool container, bool spawn, bool plate)
    {
        Manager(new AssetBundle { Asset = prefab }, true);
        Case(label, so, HasField ? prefab : null, !HasField || container,
            !HasField || spawn, !HasField || plate, HasField ? 4 : 0, false);
    }
    static int Main()
    {
        Check((BundleDictionaryField != null) == HasField, "production field lookup matches host shape");
        if (HasField)
            Check(BundleDictionaryField.IsPrivate && !BundleDictionaryField.IsStatic,
                "production lookup resolves private instance cache");
        var so = So(null);
        PseudoPrefabManager.Instance = null;
        Case("null SO", null, null, true, false, false, 0, false);
        Unknown("manager null", so);
        PseudoPrefabManager.Instance = new PseudoPrefabManager(null);
        Unknown("null cache", so);
        // Models post-Prepare/DeInit state without invoking native lifecycle APIs.
        PseudoPrefabManager.Instance = new PseudoPrefabManager(new Dictionary<string, AssetBundle>());
        Unknown("empty cache after Prepare", so);
        Manager(null, false);
        Unknown("missing bundle47 key", so);
        Manager(null, true);
        Unknown("null cached bundle", so);
        Manager(new AssetBundle { Destroyed = true }, true);
        Unknown("destroyed cached bundle", so);
        PseudoPrefabManager.Instance.Destroyed = true;
        Unknown("destroyed manager", so);

        Manager(new AssetBundle { Asset = Prefab() }, true);
        foreach (string missing in new string[] { null, "" })
        {
            var noBundle = So(null); noBundle.bundleName = missing;
            Unknown("missing bundle name: " + (missing ?? "null"), noBundle);
            var noPath = So(null); noPath.assetPath = missing;
            Unknown("missing asset path: " + (missing ?? "null"), noPath);
        }
        var deadSo = So(Prefab()); deadSo.Destroyed = true;
        Case("destroyed SO uses null contract", deadSo, null, true, false, false, 0, false);

        Cached("real negative: no recognized components", so, Prefab(), false, false, false);
        Cached("unrelated component is still invalid", so, Prefab("Transform"), false, false, false);
        foreach (string component in new string[] { "IngredientContainer", "WorkableItem",
            "IngredientPropertiesComponent", "Plate", "Glass", "EditorGridSnap" })
        {
            bool container = component == "IngredientContainer";
            bool plate = component == "EditorGridSnap";
            var prefab = Prefab(component);
            Cached("cached component " + component, so, prefab, container, !plate, plate);
            // Opposite cached answer proves direct SO GameObject takes priority.
            Manager(new AssetBundle { Asset = Prefab("IngredientContainer", "EditorGridSnap") }, true);
            Case("direct component " + component, So(prefab), prefab, container, !plate, plate, 0, false);
        }
        var valid = Prefab("IngredientContainer", "EditorGridSnap");
        Cached("cached valid prefab", so, valid, true, true, true);
        var destroyed = Prefab("IngredientContainer", "EditorGridSnap"); destroyed.Destroyed = true;
        Check(!ReferenceEquals(destroyed, null) && destroyed == null,
            "Unity destroyed GameObject retains reference but compares equal to null");
        Cached("destroyed cached GameObject fails open", so, destroyed, true, true, true);
        var invalidComponent = Prefab("IngredientContainer", "EditorGridSnap");
        foreach (var component in invalidComponent.Components.Values) component.Destroyed = true;
        Cached("destroyed components are real negative", so, invalidComponent, false, false, false);
        var throwsComponents = Prefab(); throwsComponents.ThrowOnGetComponent = true;
        Cached("component lookup throws", so, throwsComponents, true, true, true);

        Manager(new AssetBundle(), true);
        Case("cached asset missing", so, null, true, true, true, HasField ? 4 : 0, false);
        Manager(new AssetBundle { Asset = new OrderDefinitionNode() }, true);
        Case("cached SO node is not prefab", so, null, true, true, true, HasField ? 4 : 0, false);
        Manager(new AssetBundle { ThrowOnLoad = true }, true);
        Case("LoadAsset throws; probes fail open", so, null, true, true, true,
            HasField ? 4 : 0, HasField);
        Case("direct valid bypasses throwing bundle", So(valid), valid, true, true, true, 0, false);
        var invalid = Prefab();
        Case("direct invalid bypasses throwing bundle", So(invalid), invalid, false, false, false, 0, false);
        Manager(new AssetBundle { Asset = valid }, true);
        Case("direct invalid overrides valid cache", So(invalid), invalid, false, false, false, 0, false);
        Manager(new AssetBundle { Asset = invalid }, true);
        Case("direct valid overrides invalid cache", So(valid), valid, true, true, true, 0, false);
        Cached("destroyed direct GameObject falls back to cache", So(destroyed), valid, true, true, true);

        var componentAsset = new Component { gameObject = valid };
        Cached("component asset falls back to valid cache", So(componentAsset), valid, true, true, true);
        Cached("component asset cannot hide invalid cache", So(componentAsset), invalid, false, false, false);
        Cached("SO node asset falls back to cache", So(new OrderDefinitionNode()), invalid, false, false, false);
        PseudoPrefabManager.Instance = null;
        Unknown("component asset is not direct GameObject", So(componentAsset));
        Unknown("SO node asset is not direct GameObject", So(new OrderDefinitionNode()));
        Unknown("destroyed direct GameObject without cache", So(destroyed));
        var noMetadata = So(valid); noMetadata.bundleName = null; noMetadata.assetPath = "";
        Case("direct valid without manager or metadata", noMetadata, valid, true, true, true, 0, false);
        noMetadata = So(invalid); noMetadata.bundleName = ""; noMetadata.assetPath = null;
        Case("direct invalid without manager or metadata", noMetadata, invalid, false, false, false, 0, false);

        Console.WriteLine((failures == 0 ? "PASS: " : "FAILED: ") + checks
            + " cooking guard cache checks, " + failures + " failures (production code, C# 4; field "
            + (HasField ? "present" : "unavailable") + ").");
        Console.WriteLine("Calls: loading getter=" + Calls.Getter + ", Init=" + Calls.Init
            + ", DeInit=" + Calls.DeInit + ", bundle loads=" + Calls.BundleLoad
            + ", scene writes=" + Calls.SceneWrites + ".");
        return failures == 0 ? 0 : 1;
    }
'''


def main():
    source = SOURCE.read_text(encoding="utf-8")
    members = [extract(source, r"    (?:private|public) static [^\n]+\b" + name + r"\(")
               for name in METHODS]
    members.append(extract(source, r"    private static readonly FieldInfo BundleDictionaryField\s*=", field=True))
    program = HARNESS + "\n".join(members) + "\n}\n"
    env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1")
    total = 0
    # Keep generated source, restore metadata, and build products outside the repo.
    with tempfile.TemporaryDirectory(prefix="cooking-guard-cache-") as temp:
        for variant in ("present", "unavailable"):
            directory = Path(temp) / variant
            directory.mkdir()
            project = directory / "Checks.csproj"
            project.write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
                '<LangVersion>4</LangVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo>'
                '<EnableNETAnalyzers>false</EnableNETAnalyzers>'
                '<DefineConstants>' + ("NO_BUNDLE_FIELD" if variant == "unavailable" else "")
                + '</DefineConstants></PropertyGroup></Project>', encoding="utf-8")
            (directory / "Program.cs").write_text(program, encoding="utf-8")
            result = subprocess.run(["dotnet", "run", "--project", str(project)],
                                    env=env, timeout=90, capture_output=True, text=True)
            print(result.stdout, end="", flush=True)
            if result.stderr:
                print(result.stderr, end="", flush=True)
            if result.returncode:
                raise SystemExit(result.returncode)
            count = re.search(r"PASS: (\d+) cooking guard cache checks, 0 failures", result.stdout)
            if not count:
                raise AssertionError("Missing assertion count for " + variant)
            total += int(count.group(1))
    print("PASS: " + str(total) + " total cooking guard cache checks across both host shapes.")


if __name__ == "__main__":
    main()
