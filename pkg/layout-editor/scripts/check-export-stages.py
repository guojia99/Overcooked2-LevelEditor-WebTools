#!/usr/bin/env python3
"""Test extracted SetExporter stages and DTOs with net8.0 / C# 4.

Run: python3 layout-editor/scripts/check-export-stages.py
Requires dotnet SDK 8. Production sources are read-only; all generated files
stay in a temporary directory. No Unity, bundles, exports, or NuGet packages.
SessionState and JSON are API doubles, not Unity domain reload/serialization.
The actual gate, restore, resume and five-argument StartExport methods execute;
scene validation/build scheduling dependencies are doubles and builds never run.
"""
import os
from pathlib import Path
import re
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Editor/LayoutEditor/LayoutEditorSetExporter.cs"
MODELS = SOURCE.with_name("LayoutEditorModels.cs")
SERVER = SOURCE.with_name("LayoutEditorHttpServer.cs")
TOKENS = r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/|[{}]'
METHODS = [
    "GetStatus", "SetPhase", "FindStageLocked", "ResetStagesLocked",
    "SnapshotStagesLocked", "SealStageLocked", "SetStageProgress",
    "RecordExportError", "CompleteStagesLocked", "TryGateRuntimeReady",
    "RestorePendingStages", "ClearPendingExport", "ResumePendingExport",
    "ScheduleResumeRetry", "ResumeRetryTick",
]
FIELDS = [
    "LevelSetsRoot", "_phaseTimer", "StagePhases", "_stages", "_lock",
    "_status", "_setName", "_setNames", "_phase", "_message", "_error",
    "_zipFileName", "_zipAbsPath", "_fileCount", "_usesCustomStub", "_mode",
    "_depsVersion", "_selectedLevels", "_setInfoMutatedAssetPath",
    "_setInfoMutatedSetName", "_setInfoSavedLevelInfos", "PendingKey",
    "PendingAtKey", "PendingStagesKey", "_resumeNextCheck", "_deferredCompile",
]


def extract(source, declaration):
    matches = list(re.finditer(declaration, source))
    if len(matches) != 1:
        raise AssertionError("Expected one production declaration: " + declaration)
    match = matches[0]
    start = source.index("{", match.start())
    depth = 0
    for token in re.finditer(TOKENS, source[start:]):
        if token.group() == "{":
            depth += 1
        elif token.group() == "}":
            depth -= 1
            if depth == 0:
                return source[match.start():start + token.end()]
    raise AssertionError("Unbalanced production declaration: " + declaration)


def fastpath_contract(source, server, methods):
    # Fail closed if the managed-only closure gains an unreviewed call. In
    # particular Debug/JsonUtility/SessionState must not enter this read path.
    allowed = {"GetStatus", "SnapshotStagesLocked", "Add", "SetExportStatusDto",
               "SetExportStageDto", "List", "lock", "foreach", "if"}
    for name in ("GetStatus", "SnapshotStagesLocked"):
        code = re.sub(TOKENS, lambda m: m.group() if m.group() in "{}" else "", methods[name])
        calls = set(re.findall(r"\b(\w+)\s*(?:<[^<>]+>)?\s*\(", code))
        if calls - allowed:
            raise AssertionError(name + " fastpath has unreviewed calls: " + str(sorted(calls - allowed)))
        if re.search(r"\b(UnityEngine|UnityEditor|EditorApplication|AssetDatabase|Debug|JsonUtility|SessionState)\b", code):
            raise AssertionError(name + " fastpath touches a Unity API")
    fast = extract(server, r"    private static bool TryServeExportFastPath\(")
    branch = extract(fast, r"            if \(isStatus\)")
    if not re.search(r"LayoutEditorJson\.ToJson\(LayoutEditorSetExporter\.GetStatus\(\)\)", branch):
        raise AssertionError("Listener status fastpath no longer serializes GetStatus")
    if '"/api/set/export/status"' not in fast:
        raise AssertionError("Missing listener status route")
    constructor = extract(source, r"    static LayoutEditorSetExporter\(\)")
    if "RestorePendingStages();" not in constructor or "delayCall += ResumePendingExport;" not in constructor:
        raise AssertionError("Domain reload no longer restores stages and schedules resume")
    print("PASS: source contracts: managed-only GetStatus closure, listener fastpath, reload wiring", flush=True)


HARNESS = r'''
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Text.Json;

static class ApiGuard
{
    internal static bool Forbidden;
    internal static void Touch()
    { if (Forbidden) throw new Exception("Unity API reached from GetStatus"); }
}
static class Debug
{
    public static void Log(string value) { ApiGuard.Touch(); }
    public static void LogWarning(string value) { ApiGuard.Touch(); }
}
static class SessionState
{
    internal static readonly Dictionary<string, string> Values = new Dictionary<string, string>();
    public static void SetString(string key, string value) { ApiGuard.Touch(); Values[key] = value; }
    public static string GetString(string key, string fallback)
    { ApiGuard.Touch(); string value; return Values.TryGetValue(key, out value) ? value : fallback; }
}
static class JsonUtility
{
    static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
    public static string ToJson(object value)
    { ApiGuard.Touch(); return JsonSerializer.Serialize(value, value.GetType(), Options); }
    public static T FromJson<T>(string value)
    {
        ApiGuard.Touch();
        // Reflect public fields so the production private snapshot type needs no
        // visibility or constructor changes for the net8 serializer double.
        object result = Activator.CreateInstance(typeof(T), true);
        using (var document = JsonDocument.Parse(value))
            foreach (var field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                JsonElement element;
                if (document.RootElement.TryGetProperty(field.Name, out element))
                    field.SetValue(result, JsonSerializer.Deserialize(element.GetRawText(), field.FieldType, Options));
            }
        return (T)result;
    }
}
static class EditorApplication
{
    public static Action delayCall, update;
    public static double timeSinceStartup { get { ApiGuard.Touch(); return 10.0; } }
}
static class AssetDatabase
{
    public static bool IsValidFolder(string path)
    { ApiGuard.Touch(); return path == "Assets/LevelSets/fixture"; }
}
static class LayoutEditorWebAssetExporter
{
    public static SetExportStatusDto GetStatus()
    { ApiGuard.Touch(); return new SetExportStatusDto { status = "idle" }; }
}
class LevelInfoSO { }

class ExportStages
{
    public static Func<string> RuntimeReadyGate;
    static int checks, failures, groups;
    static void RecoverSetInfoBackups() { ApiGuard.Touch(); }
    static string StubVersionValue() { return "1.0.0"; }
    static string NormalizeDepsVersion(string value) { return value ?? "1.0.0"; }
    static string NormalizeSelectedLevels(List<string> sets, string mode,
        List<string> selected, out List<string> result)
    { result = selected == null ? null : new List<string>(selected); return null; }
    static void RunExport() { throw new Exception("Build callback must never run in stage harness"); }
    static void Check(bool condition, string label)
    {
        checks++;
        if (!condition) { failures++; Console.WriteLine("FAIL: " + label); }
    }
    static void Test(string name, Action body)
    {
        groups++;
        int before = failures;
        try { body(); }
        catch (Exception ex) { Check(false, name + ": " + ex); }
        Console.WriteLine((before == failures ? "PASS: " : "FAILED: ") + name);
    }
    static void Fresh(string mode)
    {
        _phaseTimer.Reset(); _phase = ""; _status = "idle"; _error = "";
        _message = ""; _setName = ""; _deferredCompile = false;
        SessionState.Values.Clear(); EditorApplication.delayCall = null;
        EditorApplication.update = null; RuntimeReadyGate = null;
        Check(StartExport(new List<string> { "fixture" }, mode, "2.3.4", null, null) == null,
            "actual StartExport accepts fixture");
    }
    static SetExportStageDto Stage(SetExportStatusDto status, string phase)
    { return status.stages.Find(delegate(SetExportStageDto s) { return s.phase == phase; }); }
    static void FixedStages()
    {
        var expected = new[] { "queued", "starting", "compile", "preflight", "orphan-clean",
            "dual-stub-clean", "prepare", "build", "package", "zip", "restore" };
        foreach (string mode in new[] { "all", "levels", "deps" })
        {
            Fresh(mode);
            var status = GetStatus();
            Check(status.stages.Count == 11, mode + " has exactly eleven stages");
            for (int i = 0; i < expected.Length; i++)
            {
                var stage = status.stages[i];
                bool skipped = mode == "deps" && i >= 3 && i <= 6;
                Check(stage.phase == expected[i], mode + " ordered phase " + i);
                Check(stage.state == (i == 0 ? "running" : skipped ? "skipped" : "waiting"),
                    mode + " initial state " + expected[i]);
                Check(stage.completed == 0 && stage.total == 0 && stage.elapsedMs >= 0,
                    mode + " initial counters " + expected[i]);
            }
        }
    }
    static void Timing()
    {
        Fresh("all"); SetPhase("prepare", "first");
        FindStageLocked("prepare").elapsedMs = 123;
        Thread.Sleep(30);
        long timer = _phaseTimer.ElapsedMilliseconds;
        long before = Stage(GetStatus(), "prepare").elapsedMs;
        SetPhase("prepare", "second");
        Check(_phaseTimer.IsRunning && _phaseTimer.ElapsedMilliseconds >= timer,
            "repeated prepare does not reset Stopwatch");
        Check(FindStageLocked("prepare").elapsedMs == 123
            && Stage(GetStatus(), "prepare").elapsedMs >= before,
            "repeated prepare preserves base and accumulated elapsed");
        Check(Stage(GetStatus(), "prepare").message == "second", "repeated phase updates message");
        Thread.Sleep(25);
        long active = Stage(GetStatus(), "prepare").elapsedMs;
        Check(active > before, "active snapshot elapsed continues increasing");
        Check(FindStageLocked("prepare").elapsedMs == 123, "polling never accumulates into stored elapsed");
        SetPhase("build", "building");
        long sealedMs = FindStageLocked("prepare").elapsedMs;
        Check(sealedMs >= active && FindStageLocked("prepare").state == "done", "different phase seals elapsed");
        Thread.Sleep(25);
        Check(Stage(GetStatus(), "prepare").elapsedMs == sealedMs, "sealed phase stops accumulating");
        Check(Stage(GetStatus(), "build").elapsedMs > 0 && FindStageLocked("build").elapsedMs == 0,
            "new phase has independent running timer");
    }
    static void CopyAndFastpath()
    {
        Fresh("all"); SetPhase("prepare", "original"); SetStageProgress(2, 7);
        var copy = GetStatus(); var stage = Stage(copy, "prepare");
        stage.phase = "tampered"; stage.state = "error"; stage.elapsedMs = -1;
        stage.completed = 99; stage.total = -2; stage.message = "tampered";
        copy.status = "error"; copy.setName = "tampered"; copy.message = "tampered";
        copy.stages.Clear(); copy.stages.Add(new SetExportStageDto { phase = "injected" });
        var fresh = GetStatus(); var actual = Stage(fresh, "prepare");
        Check(!object.ReferenceEquals(copy, fresh) && !object.ReferenceEquals(copy.stages, fresh.stages)
            && !object.ReferenceEquals(stage, actual), "status, list and each stage are independent");
        Check(fresh.status == "running" && fresh.setName == "fixture" && fresh.stages.Count == 11
            && actual.state == "running" && actual.elapsedMs >= 0 && actual.completed == 2
            && actual.total == 7 && actual.message == "original", "caller mutation cannot change internal state");
        Exception caught = null;
        ApiGuard.Forbidden = true;
        try
        {
            var thread = new Thread(delegate() {
                try { for (int i = 0; i < 100; i++) GetStatus(); }
                catch (Exception ex) { caught = ex; }
            });
            thread.Start(); thread.Join();
        }
        finally { ApiGuard.Forbidden = false; }
        Check(caught == null, "listener-thread GetStatus never invokes API doubles");
    }
    static void Progress()
    {
        Fresh("all"); SetPhase("prepare", "counting");
        int[,] cases = { { -1, 5, 0, 5 }, { 9, 5, 5, 5 }, { 3, -5, 0, 0 },
            { -1, -1, 0, 0 }, { 2, 5, 2, 5 }, { 1, 0, 0, 0 },
            { int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue },
            { int.MinValue, int.MinValue, 0, 0 } };
        for (int i = 0; i < cases.GetLength(0); i++)
        {
            SetStageProgress(cases[i, 0], cases[i, 1]);
            var s = Stage(GetStatus(), "prepare");
            Check(s.completed == cases[i, 2] && s.total == cases[i, 3], "progress clamps case " + i);
        }
    }
    static void ErrorAndRestore()
    {
        Fresh("all"); SetPhase("build", "build"); Thread.Sleep(20);
        RecordExportError("original build failure", false);
        var failed = GetStatus();
        Check(failed.status == "running" && failed.error == "original build failure"
            && Stage(failed, "build").state == "error", "nonterminal failure keeps session running");
        long elapsed = Stage(failed, "build").elapsedMs;
        SetPhase("restore", "restore preview");
        var restoring = GetStatus();
        Check(Stage(restoring, "build").state == "error"
            && Stage(restoring, "build").message == "original build failure"
            && Stage(restoring, "build").elapsedMs == elapsed, "original failed stage survives restore");
        Check(restoring.status == "running" && restoring.phase == "restore"
            && Stage(restoring, "restore").state == "running", "restore runs independently");
        Thread.Sleep(20); lock (_lock) { CompleteStagesLocked(); }
        var done = GetStatus();
        Check(done.status == "error" && done.phase == "error" && done.error == "original build failure"
            && done.message == done.error, "whole session ends error after successful restore");
        Check(Stage(done, "build").state == "error" && Stage(done, "restore").state == "done"
            && Stage(done, "restore").elapsedMs > 0 && !_phaseTimer.IsRunning, "restore completion preserves failure and seals timer");
        Fresh("all"); SetPhase("build", "build"); RecordExportError("first", false);
        SetPhase("restore", "restore"); RecordExportError("second", false);
        lock (_lock) { CompleteStagesLocked(); }
        Check(GetStatus().error == "first" && FindStageLocked("restore").state == "error",
            "restore failure keeps first error and its own failed stage");
        Fresh("all"); SetPhase("starting", "gate"); RecordExportError("fatal gate", true);
        Check(GetStatus().status == "error" && FindStageLocked("starting").state == "error",
            "terminal gate error completes session");
    }
    static void Success()
    {
        Fresh("all");
        foreach (string phase in new[] { "starting", "preflight", "orphan-clean", "dual-stub-clean",
            "prepare", "build", "package", "zip", "restore" }) SetPhase(phase, phase);
        lock (_lock) { CompleteStagesLocked(); }
        var done = GetStatus();
        Check(done.status == "done" && done.phase == "done" && done.error == "", "successful session completes");
        foreach (var stage in done.stages)
            Check(stage.state == (stage.phase == "compile" ? "skipped" : "done"),
                "success final state " + stage.phase);
        long elapsed = Stage(done, "restore").elapsedMs; Thread.Sleep(20);
        Check(Stage(GetStatus(), "restore").elapsedMs == elapsed, "completed timer is stable");
    }
    static void Suspend()
    {
        Fresh("levels"); _selectedLevels = new List<string> { "s_selected" };
        SetPhase("starting", "start"); FindStageLocked("starting").elapsedMs = 211;
        RuntimeReadyGate = delegate { return "compiling"; };
        Check(!TryGateRuntimeReady(), "actual gate suspends compiling export");
        Check(_deferredCompile && GetStatus().phase == "compile" && EditorApplication.update != null,
            "suspension marks compile and schedules retry");
        var json = SessionState.GetString(PendingStagesKey, "");
        var saved = JsonUtility.FromJson<PendingStagesSnapshot>(json);
        Check(saved.setName == "fixture" && saved.mode == "levels" && saved.stages.Count == 11
            && saved.stages.Find(delegate(SetExportStageDto s) { return s.phase == "starting"; }).elapsedMs >= 211,
            "SessionState snapshot contains actual accumulated stages and metadata");
        Check(SessionState.GetString(PendingKey, "") == "fixture\tlevels\t1.0.0\tsel=s_selected",
            "pending task retains selected levels and normalized levels-mode version");
        FindStageLocked("compile").elapsedMs = 999;
        Check(SessionState.GetString(PendingStagesKey, "") == json, "persisted JSON detached from live stages");
    }
    static void ReloadMemory()
    {
        _phaseTimer.Reset(); _stages = new List<SetExportStageDto>();
        _phase = ""; _status = "idle"; _mode = "all"; _setName = ""; _deferredCompile = false;
        EditorApplication.delayCall = null; EditorApplication.update = null;
    }
    static void PersistenceAndResume()
    {
        Suspend();
        var saved = JsonUtility.FromJson<PendingStagesSnapshot>(SessionState.GetString(PendingStagesKey, ""));
        var compile = saved.stages.Find(delegate(SetExportStageDto s) { return s.phase == "compile"; });
        compile.elapsedMs = 431; compile.completed = 3; compile.total = 9;
        SessionState.SetString(PendingStagesKey, JsonUtility.ToJson(saved));
        SessionState.SetString(PendingAtKey, DateTime.UtcNow.AddMilliseconds(-350).Ticks.ToString());
        ReloadMemory(); RestorePendingStages();
        long restoredBase = FindStageLocked("compile").elapsedMs;
        Check(restoredBase >= 781 && _deferredCompile && _phaseTimer.IsRunning,
            "reload adds nonnegative wall-time compilation gap to saved elapsed");
        Check(GetStatus().setName == "fixture" && GetStatus().mode == "levels"
            && FindStageLocked("compile").completed == 3 && FindStageLocked("compile").total == 9,
            "reload restores snapshot metadata and progress");
        Thread.Sleep(25);
        long beforeResume = Stage(GetStatus(), "compile").elapsedMs;
        long starting = FindStageLocked("starting").elapsedMs;
        RuntimeReadyGate = delegate { return null; };
        ResumePendingExport();
        var resumed = GetStatus();
        Check(resumed.status == "running" && resumed.phase == "queued" && !_deferredCompile
            && EditorApplication.delayCall != null, "actual ResumePendingExport starts actual StartExport");
        Check(Stage(resumed, "compile").state == "done" && Stage(resumed, "compile").elapsedMs >= beforeResume
            && Stage(resumed, "starting").elapsedMs == starting, "resume retains compile gap, active time and previous phases");
        Check(_selectedLevels != null && _selectedLevels.Count == 1 && _selectedLevels[0] == "s_selected"
            && _depsVersion == "1.0.0", "resume hands selection and normalized version to StartExport");
        Check(SessionState.GetString(PendingKey, "x") == "" && SessionState.GetString(PendingAtKey, "x") == ""
            && SessionState.GetString(PendingStagesKey, "x") == "", "resume clears all persisted keys");
        SetPhase("starting", "again"); Thread.Sleep(25); SetPhase("prepare", "prepare");
        Check(FindStageLocked("starting").elapsedMs > starting, "revisited starting phase adds elapsed rather than replacing it");
        SetPhase("restore", "restore"); lock (_lock) { CompleteStagesLocked(); }
        Check(GetStatus().status == "done" && FindStageLocked("compile").elapsedMs >= beforeResume,
            "resumed success retains compile history through completion");
        foreach (string stamp in new[] { DateTime.UtcNow.AddMinutes(1).Ticks.ToString(), "0", "invalid" })
        {
            SessionState.SetString(PendingKey, "fixture\tlevels\t2.3.4\tsel=");
            SessionState.SetString(PendingAtKey, stamp);
            SessionState.SetString(PendingStagesKey, JsonUtility.ToJson(saved));
            ReloadMemory(); RestorePendingStages();
            Check(FindStageLocked("compile").elapsedMs == 431,
                "future/missing/invalid timestamp cannot add negative compilation gap: " + stamp);
        }
        compile.elapsedMs = -500;
        SessionState.SetString(PendingStagesKey, JsonUtility.ToJson(saved));
        ReloadMemory(); RestorePendingStages();
        Check(FindStageLocked("compile").elapsedMs == 0, "negative persisted elapsed clamps to zero");
        Fresh("deps");
        SetPhase("starting", "deps start");
        RuntimeReadyGate = delegate { return "compiling"; };
        TryGateRuntimeReady(); Thread.Sleep(25);
        long firstCompile = Stage(GetStatus(), "compile").elapsedMs;
        RuntimeReadyGate = delegate { return null; }; ResumePendingExport();
        Check(_depsVersion == "2.3.4", "deps resume preserves explicit dependency version");
        SetPhase("starting", "second start");
        RuntimeReadyGate = delegate { return "compiling"; }; TryGateRuntimeReady();
        Thread.Sleep(25);
        long secondCompile = Stage(GetStatus(), "compile").elapsedMs;
        Check(secondCompile > firstCompile, "second compile suspension adds to first compile elapsed");
        RuntimeReadyGate = delegate { return null; }; ResumePendingExport();
        Check(FindStageLocked("compile").elapsedMs >= secondCompile,
            "second resume preserves cumulative compilation elapsed");
        foreach (string phase in new[] { "preflight", "orphan-clean", "dual-stub-clean", "prepare" })
            Check(FindStageLocked(phase).state == "skipped", "deps skip survives repeated resume: " + phase);
    }
    static int Main()
    {
        Test("fixed eleven stages and deps skips", FixedStages);
        Test("repeat phase, live elapsed and sealed elapsed", Timing);
        Test("deep copies and background GetStatus API guard", CopyAndFastpath);
        Test("SetStageProgress clamps", Progress);
        Test("failed stage, independent restore and terminal error", ErrorAndRestore);
        Test("successful completion skips unexecuted compile", Success);
        Test("SessionState snapshot, reload gaps and real resume accumulation", PersistenceAndResume);
        Console.WriteLine((failures == 0 ? "PASS: " : "FAILED: ") + checks + " stage checks in "
            + groups + " groups, " + failures + " failures (production methods/DTOs, net8.0, C# 4).");
        Console.WriteLine("Scope: API/JSON doubles; no Unity domain reload, scene restoration or bundle build executed.");
        return failures == 0 ? 0 : 1;
    }
'''


def main():
    source = SOURCE.read_text(encoding="utf-8")
    models = MODELS.read_text(encoding="utf-8")
    methods = {name: extract(source, r"    (?:private|public) static [^\n]+\b" + name + r"\(")
               for name in METHODS}
    fastpath_contract(source, SERVER.read_text(encoding="utf-8"), methods)
    members = []
    for name in FIELDS:
        matches = list(re.finditer(r"^    private (?:const|static) [^\n;]+\b" + re.escape(name)
                                  + r"\s*(?:=[^;]+)?;", source, re.MULTILINE))
        if len(matches) != 1:
            raise AssertionError("Expected one production field: " + name)
        members.append(matches[0].group())
    members.append(extract(source, r"    private class PendingStagesSnapshot\b"))
    members.extend(methods.values())
    members.append(extract(source, r"    private static string StartExport\(List<string> setNames, string mode, string depsVersion,\s*List<string> selectedLevels, List<SetExportStageDto> resumedStages\)"))
    dtos = "\n".join(extract(models, r"public class " + name + r"\b")
                     for name in ("SetExportStatusDto", "SetExportStageDto"))
    with tempfile.TemporaryDirectory(prefix="export-stages-") as temp:
        directory = Path(temp)
        project = directory / "Stages.csproj"
        project.write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
            '<LangVersion>4</LangVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo>'
            '<EnableNETAnalyzers>false</EnableNETAnalyzers>'
            '<NoWarn>0414</NoWarn>'
            '</PropertyGroup></Project>', encoding="utf-8")
        (directory / "Program.cs").write_text(HARNESS + "\n".join(members) + "\n}\n" + dtos,
                                               encoding="utf-8")
        env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
        result = subprocess.run(["dotnet", "run", "--project", str(project)], env=env, timeout=90)
        raise SystemExit(result.returncode)


if __name__ == "__main__":
    main()
