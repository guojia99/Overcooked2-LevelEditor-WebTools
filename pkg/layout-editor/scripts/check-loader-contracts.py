#!/usr/bin/env python3
"""Compile actual Loader methods and exercise IO, queues, and real DLL loading.

Run: python3 layout-editor/scripts/check-loader-contracts.py
Requires dotnet SDK 8. No Unity, NuGet packages, or distribution outputs are used.
"""
import os
from pathlib import Path
import re
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/WebCustomStubRuntime/Loader~/Loader.cs"
METHODS = [
    "LoadDependenciesAndRuntime", "GetDependencyBundleNames",
    "DiscoverLegacyDependencyBundleNames", "ParseCommonBundleIndex",
    "ScanFilesystem", "ScanRootOnWorker", "ReadRequires",
    "DetectManifestSetName", "RegisterManifestKeyOwner", "ReadStubLevels",
    "CompareSemver", "SplitVer", "IsNumericVersion",
    "OnQueueFinished", "FindIdenticalLoadedAssembly", "LoadFromBytes",
]


def extract(source, declaration):
    match = re.search(declaration, source)
    if not match:
        raise AssertionError("Missing production declaration: " + declaration)
    start = source.index("{", match.start())
    depth = 0
    # Ignore braces inside C# strings, characters, and comments.
    tokens = r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/|[{}]'
    for token in re.finditer(tokens, source[start:]):
        value = token.group()
        if value == "{":
            depth += 1
        elif value == "}":
            depth -= 1
            if depth == 0:
                return source[match.start():start + token.end()]
    raise AssertionError("Unbalanced production declaration")


HARNESS = r'''
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
class LoaderContracts
{
    static int _depLoadState;
    static bool _dependencyLoadFailed;
    static bool _queueRunning;
    static int _qBundlesDone;
    static int _qAssembliesLoaded;
    static readonly List<byte[]> PendingRaw = new List<byte[]>();
    static readonly HashSet<string> LoadedNames = new HashSet<string>();
    static readonly Dictionary<string, string> LoadedSources = new Dictionary<string, string>();
    static readonly Dictionary<Assembly, byte[]> LoadedAssemblyBytes = new Dictionary<Assembly, byte[]>();
    static string[] WebManifest = new string[0];
    static bool _anyWebExportedSet;
    static readonly HashSet<string> LoadedBundles = new HashSet<string>();
    static readonly List<LoadOp> _pendingOps = new List<LoadOp>();
    static readonly List<string> warnings = new List<string>();
    static readonly List<string> infos = new List<string>();
    static readonly List<string> players = new List<string>();
    static readonly HashSet<string> externalBundles = new HashSet<string>();
    static string ownDir;
    static int checks;
    static string OwnDir() { return ownDir; }
    static bool IsBundleLoaded(string name) { return externalBundles.Contains(name); }
    static void EnsureQueueRunning() { }
    static void LogI(string message) { infos.Add(message); }
    static void LogP(string message) { players.Add(message); }
    static void LogW(string message) { warnings.Add(message); }
    static void Check(bool ok, string label)
    {
        checks++;
        if (!ok) throw new Exception("FAIL: " + label);
    }
    static void Write(string path, string text)
    {
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }
    static string Set(string root, string name, string requires, string manifest)
    {
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        Write(Path.Combine(dir, "info_" + name), "bundle");
        Write(Path.Combine(dir, name + CustomRuntimeSuffix), "bundle");
        if (requires != null) Write(Path.Combine(dir, "requires.txt"), requires);
        if (manifest != null) Write(Path.Combine(dir, "stub_levels.txt"), manifest);
        return dir;
    }
    static ScanResult Scan(string root)
    {
        return ScanFilesystem(new List<string> { root }, true);
    }
    static bool Warns(List<string> messages, string value)
    {
        return messages.Exists(delegate(string s) { return s.Contains(value); });
    }
    static void Queue(string dir, bool active)
    {
        ownDir = dir;
        _depLoadState = 0;
        _pendingOps.Clear();
        _queueRunning = true;
        _qBundlesDone = 0;
        _qAssembliesLoaded = 0;
        _anyWebExportedSet = active;
        warnings.Clear();
        infos.Clear();
        players.Clear();
        LoadDependenciesAndRuntime();
    }
    static void Main(string[] args)
    {
        var root = args[0];
        foreach (var v in new[] { "3.10.0", "3.10.9", "3.10", "4.0.0", "4.0.0.0", "0" })
            Check(IsNumericVersion(v), "numeric version " + v);
        foreach (var v in new[] { "", "v3.10.0", "3.10.x", "3..10", "3.10.", ".3", "+3.10", "-1", "3.10.0-beta", "3.10.0+1", "3.10.0 (date)", "3.10.0\n4.0.0", "2147483648.0", "３.１０", " 3.10.0" })
            Check(!IsNumericVersion(v), "reject version " + v);
        Check(CompareSemver("3.10.0", "3.9.99") > 0, "numeric not lexical");
        Check(CompareSemver("3.10", "3.10.0") == 0, "missing segments zero");
        try { CompareSemver("4.0.0", "bad"); Check(false, "invalid compare throws"); }
        catch (FormatException) { checks++; }

        var levels = Path.Combine(root, "levels");
        Directory.CreateDirectory(levels);
        Set(levels, "legacy", "3.10.0", null);
        var scan = Scan(levels);
        Check(!scan.AnyStubLevelsFile && scan.ManifestEntries.Count == 0 && scan.RuntimeFiles.Count == 0,
            "no manifest never activates or loads custom runtime");
        Set(levels, "renamed", "\ufeff3.10.0\r\n", "# historical 3.10 format\ns_old|speedctl,spawnerbatch\ns_empty|\n*|legacy\ns_bad|a|b\ns/back|a\ns\\back|a\n");
        var renamed = Path.Combine(levels, "renamed");
        File.Move(Path.Combine(renamed, "info_renamed"), Path.Combine(renamed, "info_internal"));
        Set(levels, "empty", "3.10.9", "# no features\n");
        Set(levels, "new", "4.0.0", "s_new|unknown_future_feature");
        Set(levels, "future", "4.0.1", "s_no|stub");
        Set(levels, "missing", null, "s_no|stub");
        Set(levels, "blank", "", "s_no|stub");
        Set(levels, "invalid", "3.10.0suffix", "s_no|stub");
        var unreadable = Set(levels, "unreadable", null, "s_no|stub");
        Directory.CreateDirectory(Path.Combine(unreadable, "requires.txt"));
        var badUtf8 = Set(levels, "badUtf8", "3.10.0", "s_no|stub");
        File.WriteAllBytes(Path.Combine(badUtf8, "requires.txt"), new byte[] { 0xff });
        var badManifest = Set(levels, "badManifest", "3.10.0", null);
        Directory.CreateDirectory(Path.Combine(badManifest, "stub_levels.txt"));
        scan = Scan(levels);
        Check(scan.AnyStubLevelsFile && scan.ManifestEntries.Count == 4, "mixed sets isolated");
        Check(scan.ManifestEntries.Contains("internal|s_old|speedctl,spawnerbatch"), "3.10 and renamed key");
        Check(scan.ManifestEntries.Contains("internal|s_empty|"), "empty feature accepted");
        Check(scan.ManifestEntries.Contains("new|s_new|unknown_future_feature"), "feature passthrough");
        Check(scan.ManifestEntries.Contains("missing|s_no|stub") && Warns(scan.Warns, "未声明最低依赖版本"),
            "missing requires preserves declared manifest with explicit warning");
        Check(scan.RuntimeFiles.Count == 4, "only accepted sets have custom runtime");
        Check(scan.AliasedSets.Count == 1, "rename diagnostic retained");
        foreach (var name in new[] { "future", "missing", "blank", "invalid", "unreadable", "badUtf8", "badManifest" })
            Check(Warns(scan.Warns, "[" + name + "]"), "set diagnostic " + name);
        Check(scan.Warns.FindAll(delegate(string w) { return w.Contains("stub_levels.txt 第"); }).Count == 4,
            "wildcard, extra separator, both path separators rejected");
        string[] features;
        Check(LookupManifestLevel(scan.ManifestEntries.ToArray(), "internal", "unlisted", out features) == LevelMiss,
            "unchanged EntryPoint legacy branch cannot match rejected wildcard");
        Check(LookupManifestLevel(scan.ManifestEntries.ToArray(), "internal", "s_empty", out features) == LevelExact
            && features.Length == 0, "unchanged EntryPoint accepts empty features exactly");
        Check(LookupManifestLevel(new[] { "internal|s_named|legacy" }, "internal", "s_named", out features) == LevelExact,
            "legacy feature word alone does not trigger legacy wildcard branch");
        var emptyRoot = Path.Combine(root, "emptyRoot");
        Directory.CreateDirectory(emptyRoot);
        Set(emptyRoot, "emptyOnly", null, "");
        scan = Scan(emptyRoot);
        Check(scan.AnyStubLevelsFile && scan.ManifestEntries.Count == 0, "empty manifest still activates dependencies");
        Check(Warns(scan.Warns, "未声明最低依赖版本") && scan.RuntimeFiles.Count == 1,
            "missing requires with empty manifest preserves runtime compatibility");
        var missingRoot = Path.Combine(root, "missingRoot");
        Directory.CreateDirectory(missingRoot);
        Set(missingRoot, "old", null, "s_old|stub");
        scan = Scan(missingRoot);
        Check(scan.AnyStubLevelsFile && scan.ManifestEntries.Count == 1 && scan.RuntimeFiles.Count == 1,
            "missing requires standalone set activates without borrowing another set");

        var deps = Path.Combine(root, "deps");
        Directory.CreateDirectory(deps);
        foreach (var name in new[] { "commonw10", "commonW2", "common04", "common01", "commonW+3", "commonW4.manifest", "hash_abc123", "Loader.dll", "notes.txt", RuntimeBundleFileName })
            Write(Path.Combine(deps, name), "bundle");
        warnings.Clear();
        var names = GetDependencyBundleNames(deps);
        Check(string.Join(",", names.ToArray()) == "commonW2,commonw10,common04", "legacy numeric discovery, actual filename case");
        Check(warnings.Count == 0, "absent dependency manifest is silent");
        var manifest = Path.Combine(deps, DependencyManifestFileName);
        Write(manifest, "\ufeffhash_abc123\r\ncommonw10\ncommonW2\n");
        Queue(deps, true);
        Check(_pendingOps.Count == 4, "manifest excludes unlisted files");
        Check(_pendingOps[0].Kind == LoadOpKind.Runtime, "runtime retains priority outside manifest");
        Check(_pendingOps[1].Name == "hash_abc123" && _pendingOps[2].Name == "commonw10" && _pendingOps[3].Name == "commonW2",
            "queue preserves transitive dependency order");
        LoadDependenciesAndRuntime();
        Check(_pendingOps.Count == 4, "repeated queue setup is idempotent");
        externalBundles.Add("hash_abc123");
        Queue(deps, true);
        Check(_pendingOps.Count == 3 && _pendingOps[1].Name == "commonw10", "already loaded dependency skipped without reordering");
        externalBundles.Clear();
        Queue(deps, false);
        Check(_pendingOps.Count == 0 && _depLoadState == 0, "dormant loader does not queue dependencies or runtime");
        foreach (var invalid in new[] { "../escape", "sub/file", "sub\\file", ".", "..", "C:bundle", "*", "name.", "bad\0name", RuntimeBundleFileName, "runtime", "set_custom_runtime", DependencyManifestFileName, "missing_bundle", "hash_abc123\nHASH_ABC123" })
        {
            Write(manifest, "commonW2\n" + invalid);
            Queue(deps, true);
            Check(_pendingOps.Count == 1 && _pendingOps[0].Kind == LoadOpKind.Runtime
                && _dependencyLoadFailed && warnings.Count >= 2, "invalid list rejected atomically, runtime separate: " + invalid);
            OnQueueFinished();
            Check(Warns(warnings, "依赖加载失败") && players.Count == 1
                && !Warns(infos, "异步加载队列完成"), "failure cannot report queue success");
        }
        File.WriteAllBytes(manifest, new byte[] { 0xff, 0xfe });
        warnings.Clear();
        Check(GetDependencyBundleNames(deps).Count == 0 && Warns(warnings, "UTF-8"), "invalid UTF-8 no fallback");
        File.Delete(manifest);
        Directory.CreateDirectory(manifest);
        warnings.Clear();
        Check(GetDependencyBundleNames(deps).Count == 0 && warnings.Count > 0, "unreadable manifest no fallback");
        Directory.Delete(manifest);
        Write(manifest, "\n");
        _dependencyLoadFailed = false;
        warnings.Clear();
        Check(GetDependencyBundleNames(deps).Count == 0 && warnings.Count == 0, "empty dependency list no discovery");
        Queue(deps, true);
        OnQueueFinished();
        Check(!_dependencyLoadFailed && Warns(infos, "异步加载队列完成") && players.Count == 0, "valid queue completion");

        var first = File.ReadAllBytes(args[1]);
        var changed = File.ReadAllBytes(args[2]);
        var external = Assembly.Load(File.ReadAllBytes(args[3]));
        warnings.Clear();
        Check(FindIdenticalLoadedAssembly("ExternalStub.dll.bytes", File.ReadAllBytes(args[3])) == null
            && Warns(warnings, "不可核验"), "external locationless content is not assumed identical");
        var before = AppDomain.CurrentDomain.GetAssemblies().Length;
        var newCount = _qAssembliesLoaded;
        Check(LoadFromBytes("LevelEditorStub.dll.bytes", first), "first real assembly load");
        var afterFirst = AppDomain.CurrentDomain.GetAssemblies().Length;
        var firstAsm = FindIdenticalLoadedAssembly("LevelEditorStub.dll.bytes", first);
        Check(firstAsm != null && afterFirst > before, "loaded raw bytes registered in AppDomain");
        Check(LoadFromBytes("LevelEditorStub.dll.bytes", (byte[])first.Clone()), "identical byte copy usable");
        Check(AppDomain.CurrentDomain.GetAssemblies().Length == afterFirst && _qAssembliesLoaded == newCount + 1,
            "duplicate does not call Assembly.Load or count as new");
        Check(FindIdenticalLoadedAssembly("LevelEditorStub.dll.bytes", changed) == null, "same name/version different content not skipped");
        var firstEntry = firstAsm.GetType("CustomStub.EntryPoint");
        Check((int)firstEntry.GetField("Calls").GetValue(null) == 1, "identical reuse does not reinstall");
        infos.Clear();
        warnings.Clear();
        // Deliberately misleading filename must not evade the post-load actual-name guard.
        Check(!LoadFromBytes("misleading_name.dll.bytes", changed), "different content conflict reports unsupported");
        Assembly changedAsm = null;
        foreach (var candidate in AppDomain.CurrentDomain.GetAssemblies())
            if (candidate != firstAsm && candidate.GetName().Name == "LevelEditorStub") changedAsm = candidate;
        Check(changedAsm != null && changedAsm != firstAsm && changedAsm.FullName == firstAsm.FullName,
            "fixtures really share full assembly identity but differ in content");
        Check((string)firstAsm.GetType("Content").GetMethod("Value").Invoke(null, null) == "first"
            && (string)changedAsm.GetType("Content").GetMethod("Value").Invoke(null, null) == "changed",
            "different LevelEditorStub contents are not replaced by version-only reuse");
        Check((int)changedAsm.GetType("CustomStub.EntryPoint").GetField("Calls").GetValue(null) == 0,
            "conflicting content never installs");
        Check(LoadedSources["LevelEditorStub"] == "LevelEditorStub.dll.bytes"
            && !LoadedAssemblyBytes.ContainsKey(changedAsm) && _qAssembliesLoaded == newCount + 1,
            "conflict preserves original source, registry, and accepted load count");
        Check(Warns(warnings, "程序集冲突") && !Warns(infos, "已加载关卡程序集")
            && !Warns(infos, "EntryPoint.Install"), "conflict warns without support or install success");
        Check(!LoadFromBytes("LevelEditorStub.dll.bytes", (byte[])changed.Clone()),
            "repeated conflicting content remains unsupported");
        Check(PendingRaw.Count == 0, "conflicts never enter PendingRaw retry");
        Check(LoadFromBytes("original_again.dll.bytes", (byte[])first.Clone())
            && (int)firstEntry.GetField("Calls").GetValue(null) == 1, "original stays usable after conflict");
        Console.WriteLine("PASS: " + checks + " Loader contract checks (production methods, C# 4).");
    }
'''


def main():
    source = SOURCE.read_text(encoding="utf-8")
    members = [extract(source, r"        private static [^\n]+\b" + name + r"\(")
               for name in METHODS]
    members += [extract(source, r"        private (?:sealed class|enum) " + name + r"\b")
                for name in ["ScanResult", "LoadOp", "LoadOpKind"]]
    members += [re.search(r"        (?:public|private) const string " + name + r" = [^;]+;", source).group()
                for name in ["PluginVersion", "RuntimeBundleFileName", "CustomRuntimeSuffix", "DependencyManifestFileName"]]
    entrypoint = (ROOT / "Assets/WebCustomStubRuntime/core/EntryPoint.cs").read_text(encoding="utf-8")
    members.append(extract(entrypoint, r"        private static int LookupManifestLevel\("))
    members += [re.search(r"        private const int " + name + r" = [^;]+;", entrypoint).group()
                for name in ["LevelMiss", "LevelExact", "LevelLegacy"]]
    # All generated files and fixtures are isolated from the repository.
    with tempfile.TemporaryDirectory(prefix="loader-contracts-") as temp:
        directory = Path(temp)
        (directory / "Contracts.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
            '<LangVersion>4</LangVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo>'
            '<EnableNETAnalyzers>false</EnableNETAnalyzers>'
            '</PropertyGroup></Project>', encoding="utf-8")
        (directory / "Program.cs").write_text(HARNESS + "\n".join(members) + "\n}", encoding="utf-8")
        env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1")
        fixtures = []
        for name, content in [("LevelEditorStub", "first"), ("LevelEditorStub", "changed"), ("ExternalStub", "external")]:
            fixture = directory / content
            fixture.mkdir()
            project = fixture / "Fixture.csproj"
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                               '<TargetFramework>net8.0</TargetFramework><AssemblyName>' + name
                               + '</AssemblyName><AssemblyVersion>0.0.0.0</AssemblyVersion>'
                               '</PropertyGroup></Project>', encoding="utf-8")
            (fixture / "Content.cs").write_text('public class Content { public static string Value() { return "'
                                               + content + '"; } } '
                                               'namespace CustomStub { public static class EntryPoint { '
                                               'public static int Calls; public static bool Install() { Calls++; return true; } } }',
                                               encoding="utf-8")
            subprocess.run(["dotnet", "build", str(project), "--nologo", "-v:q"], check=True, env=env)
            fixtures.append(str(fixture / "bin/Debug/net8.0" / (name + ".dll")))
        # Exclude fixture sources from the executable project's default recursive glob.
        project = directory / "Contracts.csproj"
        project.write_text(project.read_text().replace('</Project>', '<ItemGroup><Compile Remove="first/**/*.cs;changed/**/*.cs;external/**/*.cs" /></ItemGroup></Project>'), encoding="utf-8")
        subprocess.run(["dotnet", "run", "--project", str(directory / "Contracts.csproj"),
                        "--", str(directory)] + fixtures, check=True, env=env)


if __name__ == "__main__":
    main()
