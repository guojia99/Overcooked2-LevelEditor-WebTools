#!/usr/bin/env python3
"""Exercise the actual staging builder with C# 4 and Unity API doubles.

Run: python3 layout-editor/scripts/check-staging-contracts.py
Requires dotnet SDK 8. No Unity, NuGet packages, Refresh, or repository outputs.
All builder methods compile unchanged; the harness models imported TextAsset
caches independently of disk and records per-asset imports and bundle repairs.
This checks staging behavior, not Unity's actual import or bundle serialization.
"""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Editor/LayoutEditor/LayoutStubDllBuilder.cs"

HARNESS = r'''
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

namespace UnityEngine
{
    public static class Application { public static string dataPath; }
    public class TextAsset { public byte[] bytes; }
    public class GameObject { public T GetComponent<T>() where T : class { return null; } }
    public static class Debug
    {
        public static readonly List<string> Logs = new List<string>();
        public static readonly List<string> Warnings = new List<string>();
        public static void Log(object value) { Logs.Add(value.ToString()); }
        public static void LogWarning(object value) { Warnings.Add(value.ToString()); }
        public static void LogException(Exception value) { throw new Exception("Unexpected LogException", value); }
    }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public bool IsValid() { return true; }
        public UnityEngine.GameObject[] GetRootGameObjects() { return new UnityEngine.GameObject[0]; }
    }
}
namespace LevelEditor
{
    public class PseudoPrefabManager
    {
        public static PseudoPrefabManager Instance = new PseudoPrefabManager();
        public bool prepareForBuilding;
        public void DeInit() { throw new Exception("Unexpected manager DeInit"); }
        public void Init() { throw new Exception("Unexpected manager Init"); }
    }
}
namespace UnityEditor
{
    public class InitializeOnLoadAttribute : Attribute { }
    public class MenuItem : Attribute { public MenuItem(string name, bool validate, int priority) { } }
    public enum ImportAssetOptions { Default, ForceUpdate }
    public enum BuildAssetBundleOptions { None, ForceRebuildAssetBundle }
    public enum BuildTarget { StandaloneWindows }
    public static class EditorApplication
    {
        public static bool isCompiling, isUpdating, isPlaying;
        public static Action delayCall;
    }
    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string text, string ok) { throw new Exception("Unexpected dialog"); }
    }
    public static class BuildPipeline
    {
        public static void BuildAssetBundles(string root, BuildAssetBundleOptions options, BuildTarget target)
        { throw new Exception("Unexpected bundle build"); }
    }
    public class AssetImporter
    {
        public string assetBundleName;
        public static int Reimports;
        public static readonly Dictionary<string, AssetImporter> Items = new Dictionary<string, AssetImporter>();
        public static AssetImporter GetAtPath(string path)
        { AssetImporter item; return Items.TryGetValue(path, out item) ? item : null; }
        public void SaveAndReimport() { Reimports++; }
    }
    public static class AssetDatabase
    {
        public static int Refreshes;
        public static readonly List<string> Imports = new List<string>();
        public static readonly List<string> Forced = new List<string>();
        public static readonly Dictionary<string, UnityEngine.TextAsset> Assets = new Dictionary<string, UnityEngine.TextAsset>();
        // Failure modes: a thrown import, or an import returning without an importer.
        public static string ThrowImport, DropImporter;
        public static void Refresh() { Refreshes++; }
        public static void ImportAsset(string path) { ImportAsset(path, ImportAssetOptions.Default); }
        public static void ImportAsset(string path, ImportAssetOptions options)
        {
            Imports.Add(path);
            if (options == ImportAssetOptions.ForceUpdate) Forced.Add(path);
            if (path == ThrowImport) throw new IOException("Import failed: " + path);
            if (!File.Exists(path)) throw new Exception("Unexpected missing import source: " + path);
            if (path == DropImporter) { AssetImporter.Items.Remove(path); return; }
            Assets[path] = new UnityEngine.TextAsset { bytes = File.ReadAllBytes(path) };
            if (!AssetImporter.Items.ContainsKey(path)) AssetImporter.Items[path] = new AssetImporter();
        }
        public static T LoadAssetAtPath<T>(string path) where T : class
        { UnityEngine.TextAsset asset; return Assets.TryGetValue(path, out asset) ? asset as T : null; }
    }
}
namespace UnityEditor.SceneManagement
{
    public static class EditorSceneManager
    { public static UnityEngine.SceneManagement.Scene GetActiveScene() { return new UnityEngine.SceneManagement.Scene(); } }
}
public static class LayoutEditorSetExporter
{
    public static Action<string> BeforeBuild;
    public static Func<string> RuntimeReadyGate, RuntimeStageState;
}

class StagingContracts
{
    static readonly DateTime Stamp = DateTime.UtcNow.AddDays(-10);
    static readonly string Root = LayoutStubDllBuilder.RuntimeRoot;
    static readonly string Bytes = Root + "/RuntimeDll/";
    static readonly string MainSource = "Library/ScriptAssemblies/" + LayoutStubDllBuilder.RuntimeAsmName + ".dll";
    static readonly string MainAsset = Bytes + LayoutStubDllBuilder.RuntimeAsmName + ".dll.bytes";
    static readonly string Protocol = Bytes + "BackpackProtocol.dll.bytes";
    static readonly string[] ModuleSources = {
        "Library/ScriptAssemblies/LevelEditorStub.dll",
        "Library/ScriptAssemblies/IngredientSplitterStub.dll",
        Root + "/Modules~/IngredientSplitter/bin/Release/IngredientSplitterRuntime.dll"
    };
    static int checks;
    static void Check(bool ok, string label)
    { checks++; if (!ok) throw new Exception("FAIL: " + label); }
    static object Call(string name, params object[] args)
    {
        var method = typeof(LayoutStubDllBuilder).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null) throw new Exception("Missing production method: " + name);
        try { return method.Invoke(null, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    static void Reject(Action action, string message, string label)
    {
        Exception caught = null;
        try { action(); } catch (Exception ex) { caught = ex; }
        Check(caught != null && caught.Message.Contains(message), label);
    }
    static void Write(string path, params byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, data);
        File.SetLastWriteTimeUtc(path, Stamp);
    }
    static bool Equal(string first, string second)
    { return (bool)Call("FilesHaveSameContent", first, second); }
    static string ModuleAsset(string source) { return Bytes + Path.GetFileName(source) + ".bytes"; }
    static void ResetCalls()
    {
        UnityEditor.AssetDatabase.Imports.Clear(); UnityEditor.AssetDatabase.Forced.Clear();
        UnityEditor.AssetDatabase.Refreshes = 0; UnityEditor.AssetImporter.Reimports = 0;
        UnityEngine.Debug.Logs.Clear(); UnityEngine.Debug.Warnings.Clear();
    }
    static void Fixture()
    {
        if (Directory.Exists("Assets")) Directory.Delete("Assets", true);
        if (Directory.Exists("Library")) Directory.Delete("Library", true);
        UnityEditor.AssetImporter.Items.Clear(); UnityEditor.AssetDatabase.Assets.Clear();
        UnityEditor.AssetDatabase.ThrowImport = null; UnityEditor.AssetDatabase.DropImporter = null;
        UnityEditor.EditorApplication.isCompiling = false; UnityEditor.EditorApplication.isUpdating = false;
        UnityEditor.EditorApplication.isPlaying = false; UnityEditor.EditorApplication.delayCall = null;
        Write(MainSource, 1, 2, 3);
        foreach (var source in ModuleSources) Write(source, 4, 5, 6);
        Write(Protocol, 7, 8, 9);
        LayoutStubDllBuilder.StageRuntime(true);
        ResetCalls();
    }
    static void OnlyImport(string path, bool force, string label)
    {
        Check(UnityEditor.AssetDatabase.Imports.Count == 1 && UnityEditor.AssetDatabase.Imports[0] == path,
            label + ": only expected asset imported");
        Check(UnityEditor.AssetDatabase.Forced.Count == (force ? 1 : 0)
            && (!force || UnityEditor.AssetDatabase.Forced[0] == path), label + ": expected ForceUpdate");
        Check(UnityEditor.AssetDatabase.Refreshes == 0, label + ": no full Refresh");
    }
    class ShortReads : MemoryStream
    {
        readonly int limit;
        public ShortReads(byte[] data, int size) : base(data) { limit = size; }
        public override int Read(byte[] buffer, int offset, int count)
        { return base.Read(buffer, offset, Math.Min(count, limit)); }
    }
    static void EqualityChecks()
    {
        foreach (var size in new[] { 0, 1, 8191, 8192, 8193, 65539 })
        {
            var data = new byte[size];
            for (var i = 0; i < size; i++) data[i] = (byte)(i % 251);
            Write("equality/a", data); Write("equality/b", data);
            File.SetLastWriteTimeUtc("equality/b", Stamp.AddDays(-1));
            Check(Equal("equality/a", "equality/b"), "equal bytes, different mtime: " + size);
            using (var first = new ShortReads(data, 71))
            using (var second = new ShortReads(data, 13))
                Check((bool)Call("StreamsHaveSameContent", first, second), "unequal partial read sizes: " + size);
            if (size == 0) continue;
            data[size - 1] ^= 1; Write("equality/b", data);
            Check(!Equal("equality/a", "equality/b"), "same length/mtime, changed tail: " + size);
        }
        Check(!Equal("equality/a", "equality/missing"), "missing destination unequal");
        Check(!Equal("equality/missing", "equality/a"), "missing source unequal");
        Write("equality/b", 1, 2);
        Check(!Equal("equality/a", "equality/b"), "different lengths unequal");
    }
    static void ContentAndCacheChecks()
    {
        Fixture();
        var stamp = File.GetLastWriteTimeUtc(MainAsset);
        File.SetLastWriteTimeUtc(MainSource, Stamp.AddDays(1));
        Check(!LayoutStubDllBuilder.StageRuntimeQuiet(), "unchanged content quiet false despite newer main mtime");
        Check(File.GetLastWriteTimeUtc(MainAsset) == stamp, "unchanged main not copied");
        Check(UnityEditor.AssetDatabase.Imports.Count == 0 && UnityEditor.AssetImporter.Reimports == 0,
            "unchanged main/modules/protocol never imported or reimported");
        Check(UnityEditor.AssetDatabase.Refreshes == 0, "unchanged stage never Refreshes");

        var sources = new List<string> { MainSource }; sources.AddRange(ModuleSources);
        foreach (var source in sources)
        {
            var asset = source == MainSource ? MainAsset : ModuleAsset(source);
            foreach (var missing in new[] { false, true })
            {
                Fixture();
                File.SetLastWriteTimeUtc(asset, Stamp.AddDays(-1));
                stamp = File.GetLastWriteTimeUtc(asset);
                if (missing) UnityEditor.AssetDatabase.Assets.Remove(asset);
                else UnityEditor.AssetDatabase.Assets[asset].bytes = new byte[] { 99, 98, 97 };
                Check(LayoutStubDllBuilder.StageRuntimeQuiet(), "same disk, imported stale/null staged: " + asset + "/" + missing);
                Check(File.GetLastWriteTimeUtc(asset) == stamp, "cache repair never copies: " + asset);
                Check((bool)Call("FileMatchesImportedAsset", Path.GetFullPath(asset), asset), "imported bytes repaired: " + asset);
                OnlyImport(asset, true, "cache repair " + asset);
                ResetCalls();
                Check(!LayoutStubDllBuilder.StageRuntimeQuiet() && UnityEditor.AssetDatabase.Imports.Count == 0,
                    "cache repair settles to zero imports: " + asset);
            }
            Fixture();
            if (source != MainSource) File.SetLastWriteTimeUtc(MainAsset, Stamp.AddDays(-1));
            stamp = File.GetLastWriteTimeUtc(MainAsset);
            Write(source, 11, 12, 13);
            Check(LayoutStubDllBuilder.StageRuntimeQuiet(), "same mtime/length changed content stages: " + source);
            Check(Equal(source, asset), "changed DLL copied: " + source);
            OnlyImport(asset, true, "content change " + source);
            if (source != MainSource)
                Check(File.GetLastWriteTimeUtc(MainAsset) == stamp, "module-only change leaves main file untouched");
        }
        foreach (var asset in new[] { MainAsset, ModuleAsset(ModuleSources[0]), Protocol })
        {
            Fixture(); UnityEditor.AssetImporter.Items[asset].assetBundleName = "wrong";
            Check(LayoutStubDllBuilder.StageRuntimeQuiet(), "bundle repaired: " + asset);
            Check(UnityEditor.AssetImporter.Items[asset].assetBundleName == LayoutStubDllBuilder.RuntimeBundleName,
                "correct bundle: " + asset);
            Check(UnityEditor.AssetDatabase.Imports.Count == 0 && UnityEditor.AssetImporter.Reimports == 1,
                "bundle-only repair uses SaveAndReimport only: " + asset);
            Fixture(); UnityEditor.AssetImporter.Items.Remove(asset);
            Check(LayoutStubDllBuilder.StageRuntimeQuiet(), "missing importer repaired: " + asset);
            OnlyImport(asset, false, "missing importer " + asset);
        }
        Fixture(); File.Delete(MainAsset);
        Check(LayoutStubDllBuilder.StageRuntimeQuiet() && Equal(MainSource, MainAsset), "missing main bytes recreated");
        OnlyImport(MainAsset, true, "missing main bytes");
        Fixture(); Write(Protocol, 9, 8, 7);
        Check(LayoutStubDllBuilder.StageRuntimeQuiet(), "protocol-only disk update staged");
        OnlyImport(Protocol, true, "protocol disk update");
    }
    static void FailureChecks()
    {
        foreach (var source in new[] { MainSource, ModuleSources[0] })
        {
            var asset = source == MainSource ? MainAsset : ModuleAsset(source);
            foreach (var throws in new[] { false, true })
            {
                Fixture(); UnityEditor.AssetDatabase.Assets.Remove(asset);
                if (throws) UnityEditor.AssetDatabase.ThrowImport = asset;
                else UnityEditor.AssetDatabase.DropImporter = asset;
                Reject(delegate { Call("StageDllAsset", Path.GetFullPath(source), asset); }, asset,
                    "StageDllAsset rejects thrown import/null importer: " + asset + "/" + throws);
                Check(UnityEngine.Debug.Logs.Count == 0, "failed import has no success log");
                if (source == MainSource)
                    Reject(delegate { LayoutStubDllBuilder.StageRuntime(true); }, asset, "main import failure blocks export");
                else
                {
                    Reject(delegate { Call("StageModuleDll", "LevelEditorStub", Path.GetFullPath(source), true); }, asset,
                        "strict module import failure throws");
                    Check(!(bool)Call("StageModuleDll", "LevelEditorStub", Path.GetFullPath(source), false)
                        && UnityEngine.Debug.Warnings.Count > 0, "optional module import failure warns and returns false");
                }
                Check(!LayoutStubDllBuilder.StageRuntimeQuiet(), "quiet import failure never reports success");
                OnlyImportCountNonzero(asset);
            }
        }
        Fixture(); File.Delete(ModuleSources[0]);
        Check(!(bool)Call("StageModuleDll", "LevelEditorStub", Path.GetFullPath(ModuleSources[0]), false),
            "optional missing module still returns false");
        Check(UnityEngine.Debug.Warnings.Count == 1, "optional missing module still warns");
        Reject(delegate { Call("StageModuleDll", "LevelEditorStub", Path.GetFullPath(ModuleSources[0]), true); },
            "LevelEditorStub.dll", "strict missing module blocks");
        Fixture(); UnityEditor.AssetDatabase.Assets.Remove(Protocol); UnityEditor.AssetDatabase.DropImporter = Protocol;
        Reject(delegate { LayoutStubDllBuilder.StageRuntime(true); }, "BackpackProtocol", "protocol importer failure blocks");
    }
    static void OnlyImportCountNonzero(string asset)
    {
        Check(UnityEditor.AssetDatabase.Imports.Count > 0
            && UnityEditor.AssetDatabase.Imports.TrueForAll(delegate(string path) { return path == asset; }),
            "failed import retries never import unrelated assets");
        Check(UnityEditor.AssetDatabase.Refreshes == 0, "failed import never causes full Refresh");
    }
    static void GuardChecks()
    {
        Fixture(); File.Delete(Protocol);
        Check(LayoutEditorSetExporter.RuntimeReadyGate() == null, "fresh gate checks compiled state only");
        Check(UnityEditor.AssetDatabase.Imports.Count == 0 && UnityEditor.AssetDatabase.Refreshes == 0,
            "fresh gate never stages or Refreshes");
        Reject(delegate { LayoutStubDllBuilder.StageRuntime(true); }, "BackpackProtocol", "missing protocol blocks unchanged main");
        Check(!LayoutStubDllBuilder.StageRuntimeQuiet() && UnityEngine.Debug.Warnings.Count > 0, "quiet checks missing protocol");
        Fixture();
        var protocolSource = Root + "/WebBackpack/Modules~/BackpackProtocol/Test.cs.txt";
        Write(protocolSource, 1); File.SetLastWriteTimeUtc(protocolSource, Stamp.AddSeconds(3));
        Reject(delegate { LayoutStubDllBuilder.StageRuntime(true); }, "BackpackProtocol", "stale protocol blocks unchanged main");
        Check(!LayoutStubDllBuilder.StageRuntimeQuiet(), "quiet checks stale protocol");
        foreach (var source in new[] { Root + "/Test.cs", Root + "/" + LayoutStubDllBuilder.RuntimeAsmName + ".asmdef" })
        {
            Fixture(); Write(source, 1); File.SetLastWriteTimeUtc(source, Stamp.AddSeconds(3));
            Check(LayoutStubDllBuilder.GetRuntimeStageState() == "stale", "source/asmdef freshness check");
            Reject(delegate { LayoutStubDllBuilder.StageRuntime(true); }, LayoutStubDllBuilder.RuntimeAsmName, "stale compiled state blocks staging");
            Check(!LayoutStubDllBuilder.StageRuntimeQuiet() && UnityEditor.AssetDatabase.Imports.Count == 0,
                "stale compiled state prevents quiet mutation");
            Check(LayoutEditorSetExporter.RuntimeReadyGate() == "compiling" && UnityEditor.AssetDatabase.Refreshes == 1,
                "stale compiled state still requests compilation");
        }
        Fixture(); File.Delete(MainSource);
        Reject(delegate { LayoutStubDllBuilder.StageRuntime(true); }, LayoutStubDllBuilder.RuntimeAsmName, "missing main blocks staging");
        Check(!LayoutStubDllBuilder.StageRuntimeQuiet(), "missing main quiet false");
        Check(LayoutEditorSetExporter.RuntimeReadyGate() == "compiling", "missing main gate requests compile");
        Fixture(); UnityEditor.EditorApplication.isCompiling = true;
        Check(LayoutEditorSetExporter.RuntimeReadyGate() == "compiling", "compiling gate preserved");
        Fixture(); UnityEditor.EditorApplication.isUpdating = true;
        Check(LayoutEditorSetExporter.RuntimeReadyGate() == "compiling", "updating gate preserved");
        Fixture(); File.Delete(MainSource); UnityEditor.EditorApplication.isPlaying = true;
        Check(LayoutEditorSetExporter.RuntimeReadyGate() != null && UnityEditor.AssetDatabase.Refreshes == 0,
            "Play mode blocks compile");
    }
    static void Main(string[] args)
    {
        Directory.SetCurrentDirectory(args[0]);
        UnityEngine.Application.dataPath = Path.Combine(args[0], "Assets");
        EqualityChecks(); ContentAndCacheChecks(); FailureChecks(); GuardChecks();
        Console.WriteLine("PASS: " + checks + " staging contract checks (actual builder source, C# 4; mocked Unity API).");
    }
}
'''


def main():
    if not shutil.which("dotnet"):
        raise SystemExit("dotnet SDK 8 is required for staging behavior checks")
    # Generated sources, fixtures, and build products never touch the repository.
    with tempfile.TemporaryDirectory(prefix="staging-contracts-") as temp:
        directory = Path(temp)
        (directory / "Builder.cs").write_text(SOURCE.read_text(encoding="utf-8"), encoding="utf-8")
        (directory / "Program.cs").write_text(HARNESS, encoding="utf-8")
        project = directory / "Contracts.csproj"
        project.write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
            '<LangVersion>4</LangVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo>'
            '<EnableNETAnalyzers>false</EnableNETAnalyzers>'
            '</PropertyGroup></Project>', encoding="utf-8")
        env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1")
        result = subprocess.run(["dotnet", "run", "--project", str(project), "--", str(directory)],
                                cwd=directory, env=env, timeout=90)
        raise SystemExit(result.returncode)


if __name__ == "__main__":
    main()
