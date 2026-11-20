#!/usr/bin/env python3
"""Execute the production dependency helper; check critical caller boundaries."""
from pathlib import Path
import re
import subprocess
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
EDITOR = ROOT / 'Assets/Editor/LayoutEditor'

HARNESS = r'''
using System;
using System.Collections.Generic;
namespace LevelEditorStub { public class LevelInfoSO { public string[] dependencies; } }
namespace UnityEditor {
    public class AssetModificationProcessor { }
    public static class AssetDatabase {
        public static object asset; public static int loads;
        public static T LoadAssetAtPath<T>(string path) where T:class { loads++; return asset as T; }
    }
    public static class EditorUtility {
        public static int writes;
        public static void SetDirty(object value) { writes++; }
    }
}
class Checks {
    static void Assert(bool value, string label) { if (!value) throw new Exception(label); }
    static void Main() {
        foreach (var value in new[] { "bundle0", "bundle47", "bundle001", "bundle999999999999999999999999" })
            Assert(LayoutEditorNativeDependencies.IsNativeBundle(value), "accept " + value);
        string[] bad = { null, "", "bundle", "Bundle47", "bundle-1", "bundle1.0", "bundle47\n",
            " bundle47", "bundle47 ", "bundle١", "set/bundle47", "commonW6", "common04",
            "custom_recipes", "set/custom_recipes", "set/custom_content", "hash123" };
        foreach (var value in bad) {
            Assert(!LayoutEditorNativeDependencies.IsNativeBundle(value), "reject " + value);
            Assert(LayoutEditorNativeDependencies.Validate(new[] { "bundle47", value }) != null, "validation " + value);
        }
        var input = new List<string>(bad); input.AddRange(new[] { "bundle47", "bundle18", "bundle47" });
        var info = new LevelEditorStub.LevelInfoSO { dependencies = input.ToArray() };
        Assert(LayoutEditorNativeDependencies.Clean(info), "migration changed");
        Assert(string.Join(",", info.dependencies) == "bundle47,bundle18", "preserve order and dedup");
        Assert(UnityEditor.EditorUtility.writes == 1, "dirty exactly once");
        Assert(!LayoutEditorNativeDependencies.Clean(info), "idempotent");
        Assert(UnityEditor.EditorUtility.writes == 1, "no repeated dirty");
        Assert(LayoutEditorNativeDependencies.Validate(info.dependencies) == null, "valid readback");
        Assert(LayoutEditorNativeDependencies.Validate(null) == null, "omitted update");
        Assert(LayoutEditorNativeDependencies.Filter(null).Length == 0, "null filter");
        Assert(LayoutEditorNativeDependencies.Clean(new LevelEditorStub.LevelInfoSO()), "null migration");
        info.dependencies = new[] { "commonW5", "bundle18", "set/custom_recipes" };
        UnityEditor.AssetDatabase.asset = info;
        var paths = new[] { "Assets/commonW5/data.asset", "Assets/LevelSets/test/scenes/s_test.unity", "Assets/LevelSets/test/data/info.asset" };
        var guard = typeof(LayoutEditorNativeDependencySaveGuard).GetMethod("OnWillSaveAssets",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var saved = guard.Invoke(null, new object[] { paths });
        Assert(object.ReferenceEquals(saved, paths), "save paths preserved");
        Assert(UnityEditor.AssetDatabase.loads == 1, "only level asset saves inspected");
        Assert(string.Join(",", info.dependencies) == "bundle18", "save boundary cleans legacy caller");
        Console.WriteLine("PASS production helper: strict names, rejection, migration, dedup, idempotence");
    }
}
'''


SHARED_BUNDLES_HARNESS = r'''
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LevelEditor;

namespace UnityEngine {
    public class Object { }
    public class Material : Object { }
    public class ScriptableObject : Object { }
    public class Transform { public Transform parent; }
    public class GameObject : Object {
        public int scene;
        public Transform transform = new Transform();
    }
    public static class Resources {
        public static GameObject[] objects;
        public static int scans;
        public static T[] FindObjectsOfTypeAll<T>() where T : Object {
            scans++;
            return (T[])(object)objects;
        }
    }
}
namespace LevelEditorStub {
    public class LevelInfoSO : ScriptableObject { }
    public class PseudoPrefabSO : ScriptableObject { public string bundleName; }
    public class PseudoPrefabManagerStub { public LevelInfoSO levelInfo; }
}
namespace UnityEditor {
    public static class BuildPipeline { public static bool isBuildingPlayer; }
    public static class AssetDatabase {
        public static string GetAssetPath(UnityEngine.Object asset) {
            throw new Exception("GetAssetPath must not be called for collected dependencies");
        }
        public static string GetImplicitAssetBundleName(string path) {
            throw new Exception("GetImplicitAssetBundleName must not be called");
        }
    }
    public static class EditorUtility {
        public static UnityEngine.Object[] dependencies, roots;
        public static int collections;
        public static UnityEngine.Object[] CollectDependencies(UnityEngine.Object[] values) {
            collections++;
            roots = values;
            return dependencies;
        }
    }
}
namespace LevelEditor {
    public class PseudoPrefabManager {
        public bool prepareForBuilding;
        public GameObject gameObject = new GameObject { scene = 1 };
        public LevelEditorStub.PseudoPrefabManagerStub stub;
        public readonly List<string> loaded = new List<string>();
        private void LoadAssetBundle(string name, bool native) {
            if (native) throw new Exception("shared bundle must use false load flag");
            loaded.Add(name);
        }
    }
}
static class LayoutEditorAssetBundleLifetime {
    public static bool manifest = true;
    public static bool HasManifest(PseudoPrefabManager manager) { return manifest; }
}
static class LayoutEditorCatalogApi {
    public static readonly HashSet<string> existing = new HashSet<string>(StringComparer.Ordinal);
    public static readonly List<string> probes = new List<string>();
    public static bool BundleFileExists(string name) {
        probes.Add(name);
        return existing.Contains(name);
    }
}
class SharedBundleChecks {
    // A plain asset's similarly named field is not PseudoPrefabSO metadata.
    class PlainAsset : ScriptableObject { public string bundleName = "common_plain"; }
    static void Assert(bool value, string label) { if (!value) throw new Exception(label); }
    static LevelEditorStub.PseudoPrefabSO So(string name) {
        return new LevelEditorStub.PseudoPrefabSO { bundleName = name };
    }
    static void Reset() {
        EditorUtility.collections = Resources.scans = 0;
        EditorUtility.roots = null;
        LayoutEditorCatalogApi.probes.Clear();
    }
    static void Main() {
        var info = new LevelEditorStub.LevelInfoSO();
        var manager = new PseudoPrefabManager {
            stub = new LevelEditorStub.PseudoPrefabManagerStub { levelInfo = info }
        };
        var root = manager.gameObject;
        var otherRoot = new GameObject { scene = 1 };
        Resources.objects = new[] { root, otherRoot,
            new GameObject { scene = 1, transform = new Transform { parent = root.transform } },
            new GameObject { scene = 2 } };
        var shared = So("commonW1");
        EditorUtility.dependencies = new UnityEngine.Object[] {
            new Material(), new GameObject { scene = 1 }, new ScriptableObject(), new PlainAsset(),
            null, shared, shared, So("commonW1"), So("common01"), So("COMMONW2"),
            So("common_missing"), So(null), So(""), So("bundle47"), So("custom"),
            So("set/custom_content"), So("set/custom_recipes"), So("set/runtime")
        };
        // Even existing native/custom packages and the plain asset must be ignored.
        foreach (var name in new[] { "commonW1", "common01", "COMMONW2", "common_plain",
            "bundle47", "custom", "set/custom_content", "set/custom_recipes", "set/runtime" })
            LayoutEditorCatalogApi.existing.Add(name);
        LoadSharedBundlesPostfix(manager);
        Assert(EditorUtility.collections == 1 && Resources.scans == 1, "one dependency collection");
        var roots = new HashSet<UnityEngine.Object>(EditorUtility.roots);
        Assert(EditorUtility.roots.Length == 3 && roots.SetEquals(new UnityEngine.Object[] { info, root, otherRoot }),
            "collect level info and current-scene roots only");
        Assert(manager.loaded.Count == 3 && new HashSet<string>(manager.loaded).SetEquals(
            new[] { "commonW1", "common01", "COMMONW2" }), "existing common packages only, deduplicated");
        Assert(LayoutEditorCatalogApi.probes.Count == 4 && new HashSet<string>(LayoutEditorCatalogApi.probes).SetEquals(
            new[] { "commonW1", "common01", "COMMONW2", "common_missing" }),
            "only distinct common PseudoPrefabSO bundle names probe existence");
        Console.WriteLine("PASS production postfix: mixed dependencies, scene roots, existing common only, dedup, AssetDatabase sentinels");

        foreach (var gate in new[] { "prepare", "build", "no manifest" }) {
            Reset(); manager.loaded.Clear();
            manager.prepareForBuilding = gate == "prepare";
            BuildPipeline.isBuildingPlayer = gate == "build";
            LayoutEditorAssetBundleLifetime.manifest = gate != "no manifest";
            LoadSharedBundlesPostfix(manager);
            Assert(EditorUtility.collections == 0 && Resources.scans == 0
                && LayoutEditorCatalogApi.probes.Count == 0 && manager.loaded.Count == 0,
                gate + " must return before collection or loading");
        }
        manager.prepareForBuilding = BuildPipeline.isBuildingPlayer = false;
        LayoutEditorAssetBundleLifetime.manifest = true;
        EditorUtility.dependencies = new UnityEngine.Object[0];
        foreach (var stub in new[] { new LevelEditorStub.PseudoPrefabManagerStub(), null }) {
            Reset(); manager.stub = stub;
            LoadSharedBundlesPostfix(manager);
            Assert(EditorUtility.collections == 1 && EditorUtility.roots.Length == 2
                && new HashSet<UnityEngine.Object>(EditorUtility.roots).SetEquals(new[] { root, otherRoot })
                && manager.loaded.Count == 0 && LayoutEditorCatalogApi.probes.Count == 0,
                "missing stub or info still collects scene roots; empty dependencies load nothing");
        }
        Console.WriteLine("PASS production postfix: prepare/build/no-manifest gates, missing stub/info, empty dependencies");
    }
'''


def check_shared_bundles_postfix(source):
    declaration = r'    private static void LoadSharedBundlesPostfix\(PseudoPrefabManager __instance\)'
    matches = list(re.finditer(declaration, source))
    assert len(matches) == 1, 'Expected one production LoadSharedBundlesPostfix declaration'
    start = source.index('{', matches[0].end())
    depth = 0
    tokens = r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/|[{}]'
    for token in re.finditer(tokens, source[start:]):
        if token.group() == '{':
            depth += 1
        elif token.group() == '}':
            depth -= 1
            if depth == 0:
                postfix = source[matches[0].start():start + token.end()]
                break
    else:
        raise AssertionError('Unbalanced production LoadSharedBundlesPostfix declaration')
    with tempfile.TemporaryDirectory(prefix='oc2-shared-bundles-') as temp:
        out = Path(temp)
        (out / 'Harness.cs').write_text(SHARED_BUNDLES_HARNESS + postfix + '\n}\n')
        (out / 'Checks.csproj').write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion>'
            '<ImplicitUsings>disable</ImplicitUsings><GenerateAssemblyInfo>false</GenerateAssemblyInfo>'
            '</PropertyGroup></Project>')
        (out / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        subprocess.run(['dotnet', 'run', '--project', str(out / 'Checks.csproj'), '--verbosity', 'quiet'],
                       cwd=temp, check=True, timeout=90)


def main():
    with tempfile.TemporaryDirectory(prefix='oc2-native-deps-') as temp:
        out = Path(temp)
        (out / 'Harness.cs').write_text(HARNESS)
        version = subprocess.check_output(['dotnet', '--version'], text=True).strip().split('.')[0]
        (out / 'Checks.csproj').write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net' + version + '.0</TargetFramework><LangVersion>4</LangVersion>'
            '<ImplicitUsings>disable</ImplicitUsings><GenerateAssemblyInfo>false</GenerateAssemblyInfo>'
            '</PropertyGroup><ItemGroup><Compile Include="' + escape(str(EDITOR / 'LayoutEditorNativeDependencies.cs'))
            + '" /></ItemGroup></Project>')
        subprocess.run(['dotnet', 'run', '--project', str(out / 'Checks.csproj'), '--verbosity', 'quiet'], check=True)

    admin = (EDITOR / 'LayoutEditorLevelAdminApi.cs').read_text()
    update = admin[admin.index('public static string UpdateLevelInfo('):admin.index('private static string ApplyGridHalfSizeChange(')]
    assert update.index('LayoutEditorNativeDependencies.Validate') < update.index('ApplyGridHalfSizeChange(dto') < update.index('Undo.RecordObject')
    assert 'FilterSetDataDependencies' not in admin
    assert 'SetAssetBundleNameAndVariant(setName + "/custom_recipes"' not in admin
    for name in ['CustomIngredients', 'CommonW6Dependencies', 'LevelModeStore', 'LevelModeChange', 'LevelInfoSanitizer', 'StubIO']:
        source = (EDITOR / ('LayoutEditor' + name + '.cs')).read_text()
        for rhs in re.findall(r'info\.dependencies\s*=(?!=)\s*([^;]+);', source):
            assert 'LayoutEditorNativeDependencies.Filter(' in rhs or rhs in ['projection.dependencies', 'new string[0]'], (name, rhs)
    preview = (EDITOR / 'LayoutEditorPseudoReload.cs').read_text()
    assert 'deps.Add(customBundle)' not in preview
    repair = (EDITOR / 'LayoutEditorDependencyRepair.cs').read_text()
    assert 'LoadSharedBundlesPostfix' in repair and 'load.Invoke(__instance' in repair
    assert '.dependencies =' not in repair
    guard = (EDITOR / 'LayoutEditorNativeDependencies.cs').read_text()
    assert 'OnWillSaveAssets' in guard and 'AssetDatabase.LoadAssetAtPath<LevelInfoSO>(path)' in guard
    print('PASS caller contracts: atomic rejection, all owned writers filtered, shared loading independent')
    check_shared_bundles_postfix(repair)


if __name__ == '__main__':
    main()
