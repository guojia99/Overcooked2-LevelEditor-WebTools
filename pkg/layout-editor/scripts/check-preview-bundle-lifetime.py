#!/usr/bin/env python3
"""Offline preview bundle lifetime regression checks (Python 3, dotnet SDK 8).

Compiles the complete, unchanged production lifetime helper and Harmony guard
installer as C#4/net8 against small Unity/editor/manager doubles. Each scenario
runs in a fresh process, including real helper static initialization and event
subscriptions. SafeInit/dependency filtering are instrumented boundaries, not
copies of their production implementations; integration order is source checked.
No Unity scenes, live HTTP calls, third-party packages, or repository build files.
"""

import hashlib
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[2]
EDITOR = ROOT / "Assets/Editor/LayoutEditor"
TEMP_PARENT = Path("/var/folders/p8/b3ryxj6s1bd4wgsspxzhfnw40000gn/T/opencode")
SOURCES = ("LayoutEditorAssetBundleLifetime.cs", "LayoutEditorDependencyRepair.cs",
           "LayoutEditorPseudoReload.cs")


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def code_only(source):
    """Mask comments/literals while preserving offsets and executable tokens."""
    tokens = r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/'
    return re.sub(tokens, lambda match: re.sub(r"[^\n]", " ", match.group()), source)


def method(source, name):
    code = code_only(source)
    matches = list(re.finditer(
        r"\b(?:internal|private|public)\s+static\s+[\w\[\]]+\s+" + name + r"\s*\([^)]*\)\s*\{", code))
    require(len(matches) == 1, "Expected one method declaration: " + name)
    start = code.index("{", matches[0].start())
    depth = 1
    for index in range(start + 1, len(code)):
        depth += (code[index] == "{") - (code[index] == "}")
        if depth == 0:
            return source[start + 1:index]
    raise AssertionError("Unbalanced method: " + name)


def source_contracts(sources):
    safe = code_only(method(sources["LayoutEditorPseudoReload.cs"], "SafeInit"))
    guard_source = sources["LayoutEditorDependencyRepair.cs"]
    reset = code_only(method(guard_source, "ResetChildPrefix"))
    count = 0

    def contract(condition, label):
        nonlocal count
        require(condition, label)
        count += 1
        print("PASS source contract: " + label, flush=True)

    def before(first, second, label):
        left, right = safe.find(first), safe.find(second)
        contract(left >= 0 and right >= 0 and left < right, label)

    contract(re.match(r"\s*if\s*\(\s*!LayoutEditorAssetBundleLifetime\.CanRepair\(manager\)\s*\)\s*"
                      r"\{\s*LayoutEditorAssetBundleLifetime\.QueueRecovery\(\);\s*return\s*;\s*\}",
                      safe) is not None,
             "SafeInit blocked branch queues deferred recovery before returning or touching previews")
    before("LayoutEditorAssetBundleLifetime.ClearStaleState(",
           "AssetBundle.GetAllLoadedAssetBundles(", "SafeInit clears stale state before loaded-bundle adoption scan")
    before("LayoutEditorAssetBundleLifetime.ClearStaleState(",
           "bundles.Add(", "SafeInit clears stale state before adopting a bundle")
    before("LayoutEditorAssetBundleLifetime.ClearStaleState(",
           "LayoutEditorDispenserIconFix.PreloadAndSeed(", "SafeInit clears stale state before dispenser preload")
    before("LayoutEditorAssetBundleLifetime.EnsureBundlesLoaded(",
           "RecipeHelper.", "SafeInit loads bundles before first RecipeHelper/atlas access")
    before("GetPreviewDependencies(", "LayoutEditorAssetBundleLifetime.ClearStaleState(",
           "SafeInit selects preview dependencies before cache validation")
    contract(re.search(r"if\s*\(\s*!LayoutEditorAssetBundleLifetime\.CanResetChild\(__instance\)\s*\)\s*"
                       r"(?:\{\s*)?return\s+false\s*;", reset) is not None
             and reset.find("CanResetChild(") < reset.find("TryResetCustomModelPreview("),
             "ResetChild prefix gates native/custom reset on deferred lifetime guard")
    contract(re.search(r"\b(?:SafeInit|Init)\s*\(", reset) is None,
             "ResetChild prefix never directly nests manager Init")
    # String arguments are meaningful here, so retain literals but drop comments.
    contract(re.search(r'GetMethod\("ResetChild"\)[\s\S]*?GetMethod\("ResetChildPrefix",', guard_source) is not None,
             "Harmony installer attaches the ResetChild prefix")
    init = code_only(method(guard_source, "InitPrefix"))
    contract("LayoutEditorPseudoReload.SafeInit(__instance)" in init
             and "LayoutEditorPseudoReload.CallingOriginalInit" in init,
             "native Init prefix routes restoration through SafeInit with recursion escape")
    recover = code_only(method(sources["LayoutEditorAssetBundleLifetime.cs"], "Recover"))
    for repair in ("RepairMissingPseudoMaterials", "RepairBrokenPressureSwitchChildren"):
        repair_index = recover.find("LayoutEditorPseudoReload." + repair + "(")
        late_index = recover.find("pseudo.LateSetup(")
        contract(0 <= repair_index < late_index,
                 "recovery " + repair + " precedes pseudo LateSetup")
    return count


DOUBLES = r'''
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEditor;
using LevelEditor;
using LevelEditorStub;

namespace UnityEngine
{
    public class MissingReferenceException : Exception { }
    public class Object
    {
        public static readonly List<Object> SceneObjects = new List<Object>();
        public bool Destroyed;
        public string name;
        public static T[] FindObjectsOfType<T>() where T : Object
        {
            var result = new List<T>();
            foreach (var value in SceneObjects)
                if (value is T && !value.Destroyed) result.Add((T)value);
            return result.ToArray();
        }
        // A dead native object still has a managed wrapper. Object casts and
        // ReferenceEquals must distinguish that wrapper from actual CLR null.
        public static bool operator ==(Object a, Object b)
        {
            bool an = ReferenceEquals(a, null), bn = ReferenceEquals(b, null);
            if (an && bn) return true;
            if (an) return b.Destroyed;
            if (bn) return a.Destroyed;
            return ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) { return !(a == b); }
        public override bool Equals(object other) { return ReferenceEquals(this, other); }
        public override int GetHashCode() { return base.GetHashCode(); }
        protected void RequireAlive() { if (Destroyed) throw new MissingReferenceException(); }
    }
    public class Component : Object
    {
        public object Attached;
        public T GetComponent<T>() where T : class { return Attached as T; }
    }
    public class Material : Object { }
    public class AssetBundle : Object
    {
        public int Unloads;
        public bool UnloadedAssets;
        public void Unload(bool all)
        { RequireAlive(); Unloads++; UnloadedAssets = all; Destroyed = true; }
    }
    public class AssetBundleManifest : Object
    {
        public int Reads;
        public readonly Dictionary<string, string[]> Edges = new Dictionary<string, string[]>();
        public string[] GetAllDependencies(string bundle)
        {
            RequireAlive(); Reads++;
            var result = new HashSet<string>();
            var pending = new Queue<string>(); pending.Enqueue(bundle);
            while (pending.Count > 0)
            {
                string[] edges;
                if (!Edges.TryGetValue(pending.Dequeue(), out edges)) continue;
                foreach (string edge in edges) if (result.Add(edge)) pending.Enqueue(edge);
            }
            return new List<string>(result).ToArray();
        }
    }
    public static class Application { public static bool isPlaying; }
    public static class Debug
    { public static void Log(string s) { } public static void LogError(string s) { throw new Exception(s); } }
}
namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Class)] public class InitializeOnLoadAttribute : Attribute { }
    public enum PlayModeStateChange { EnteredEditMode, ExitingEditMode, EnteredPlayMode, ExitingPlayMode }
    public static class EditorApplication
    {
        public static bool isPlayingOrWillChangePlaymode, isCompiling, isUpdating;
        public static double timeSinceStartup = 100;
        public static event Action update;
        public static event Action<PlayModeStateChange> playModeStateChanged;
        public static int Subscribers { get { return update == null ? 0 : update.GetInvocationList().Length; } }
        public static void Tick(double elapsed)
        {
            timeSinceStartup += elapsed;
            var callback = update; if (callback != null) callback();
        }
        public static void Mode(PlayModeStateChange state)
        { var callback = playModeStateChanged; if (callback != null) callback(state); }
    }
    public static class BuildPipeline { public static bool isBuildingPlayer; }
}
namespace HarmonyLib
{
    public class HarmonyMethod
    { public MethodInfo Method; public HarmonyMethod(MethodInfo method) { Method = method; } }
    public class Harmony
    {
        public static readonly Dictionary<string, MethodInfo> Prefixes = new Dictionary<string, MethodInfo>();
        public Harmony(string id) { }
        public void Patch(MethodInfo original, HarmonyMethod prefix)
        {
            if (original == null || prefix == null || prefix.Method == null) throw new Exception("Missing patch method");
            Prefixes.Add(original.DeclaringType.Name + "." + original.Name, prefix.Method);
        }
    }
}
namespace LevelEditorStub
{
    public class LevelInfoSO : UnityEngine.Object { public string[] dependencies; }
    public class PseudoPrefabManagerStub : Component { public LevelInfoSO levelInfo; }
    public class PseudoPrefabSO : UnityEngine.Object { public UnityEngine.Object asset; public string bundleName; }
    public class PseudoPrefabStub : Component { public PseudoPrefabSO pseudoPrefabSO; }
}
namespace LevelEditor
{
    public class PseudoPrefabManager : Component
    {
        private Dictionary<string, AssetBundle> bundleDict = new Dictionary<string, AssetBundle>();
        private AssetBundleManifest assetBundleManifest;
        public static PseudoPrefabManager Instance;
        public PseudoPrefabManagerStub stub;
        public bool prepareForBuilding;
        public Dictionary<string, Material> editedMaterials = new Dictionary<string, Material>();
        public int Loads;
        public Action OnLoad;
        public Dictionary<string, AssetBundle> Cache { get { return bundleDict; } set { bundleDict = value; } }
        public AssetBundleManifest Manifest { get { return assetBundleManifest; } set { assetBundleManifest = value; } }
        private void EnsureLoadAllAssetBundles() { Loads++; if (OnLoad != null) OnLoad(); }
        public void Init() { throw new Exception("Native Init is not simulated"); }
    }
    public class PseudoPrefab : Component
    {
        public int LateCalls;
        public Action OnLateSetup;
        public virtual void ResetChild() { }
        public virtual void LateSetup() { LateCalls++; if (OnLateSetup != null) OnLateSetup(); }
    }
    public class PseudoPrefabReplaceMaterial : PseudoPrefab { public override void ResetChild() { } }
    public class SetupCustomPrefab : Component
    {
        public int LateCalls;
        public Action OnLateSetup;
        public void LateSetup() { LateCalls++; if (OnLateSetup != null) OnLateSetup(); }
    }
}
static class LayoutEditorLog
{
    public static readonly List<string> Warnings = new List<string>();
    public static void Log(string text) { }
    public static void LogWarning(string text) { Warnings.Add(text); }
}
static class LayoutEditorPushablePotPreview
{ public static int Arms; public static void Arm() { Arms++; } }
static class LayoutEditorPseudoReload
{
    public static bool ExportBuildInProgress, CallingOriginalInit;
    public static int Inits, Children, Materials, Switches, Filters, CustomResets, ResetDepth;
    public static Action<PseudoPrefabManager> InitAction;
    public static Action RepairAction;
    public static string[] Filtered;
    public static PseudoPrefabManager FilterManager;
    public static LevelInfoSO FilterInfo;
    public static bool CustomHandled;
    public static string[] GetPreviewDependencies(PseudoPrefabManager manager, LevelInfoSO info)
    { Filters++; FilterManager = manager; FilterInfo = info; return Filtered ?? info.dependencies; }
    public static void SafeInit(PseudoPrefabManager manager)
    {
        Inits++;
        if (ResetDepth != 0) throw new Exception("Nested Init during ResetChild");
        if (InitAction != null) InitAction(manager);
    }
    public static void RepairPseudoPrefabChildren() { Children++; if (RepairAction != null) RepairAction(); }
    public static void RepairMissingPseudoMaterials() { Materials++; }
    public static void RepairBrokenPressureSwitchChildren() { Switches++; }
    public static bool TryResetCustomModelPreview(PseudoPrefab pseudo) { CustomResets++; return CustomHandled; }
}
'''


TESTS = r'''
static class Checks
{
    static int assertions;
    static readonly Dictionary<string, Action> Cases = new Dictionary<string, Action>();
    static void Check(bool ok, string message)
    { assertions++; if (!ok) throw new Exception(message); }
    static PseudoPrefabManager Manager(bool healthy)
    {
        var m = new PseudoPrefabManager();
        m.stub = new PseudoPrefabManagerStub { levelInfo = new LevelInfoSO { dependencies = new string[] { "kitchen" } } };
        m.Attached = m.stub;
        m.editedMaterials.Add("old", new Material());
        PseudoPrefabManager.Instance = m;
        if (healthy) MakeHealthy(m);
        return m;
    }
    static void MakeHealthy(PseudoPrefabManager m)
    {
        m.Cache = new Dictionary<string, AssetBundle>();
        m.Cache.Add("Windows", new AssetBundle());
        m.Cache.Add("kitchen", new AssetBundle());
        m.Manifest = new AssetBundleManifest();
    }
    static PseudoPrefab Pseudo(string bundle)
    { return new PseudoPrefab { Attached = new PseudoPrefabStub { pseudoPrefabSO = new PseudoPrefabSO { bundleName = bundle } } }; }
    static void Boot()
    { RuntimeHelpers.RunClassConstructor(typeof(LayoutEditorAssetBundleLifetime).TypeHandle); }
    static bool ResetPrefix(PseudoPrefab pseudo)
    {
        RuntimeHelpers.RunClassConstructor(typeof(LayoutEditorDependencyRepair).TypeHandle);
        LayoutEditorPseudoReload.ResetDepth++;
        string key = pseudo.GetType().GetMethod("ResetChild").DeclaringType.Name + ".ResetChild";
        try { return (bool)HarmonyLib.Harmony.Prefixes[key].Invoke(null, new object[] { pseudo }); }
        finally { LayoutEditorPseudoReload.ResetDepth--; }
    }
    static void NoInit()
    { Check(LayoutEditorPseudoReload.Inits == 0, "unexpected manager reinitialization"); }
    static void NoPreviews()
    {
        Check(LayoutEditorPseudoReload.CustomResets == 0, "blocked reset reached custom preview path");
        Check(LayoutEditorPseudoReload.Children == 0 && LayoutEditorPushablePotPreview.Arms == 0,
            "blocked recovery rebuilt previews");
        Check(LayoutEditorPseudoReload.Materials == 0 && LayoutEditorPseudoReload.Switches == 0,
            "blocked recovery repaired previews");
    }
    static void Completed()
    {
        Check(LayoutEditorPseudoReload.Children == 1, "children not rebuilt once");
        Check(LayoutEditorPushablePotPreview.Arms == 1, "pot preview not armed once");
        Check(LayoutEditorPseudoReload.Materials == 1 && LayoutEditorPseudoReload.Switches == 1, "post recovery repairs missing");
        Check(EditorApplication.Subscribers == 0, "recovery remains subscribed after success");
    }
    static void Register()
    {
        Cases.Add("fake-null wrappers differ from CLR null", delegate {
            var b = new AssetBundle { Destroyed = true };
            var manifest = new AssetBundleManifest { Destroyed = true };
            Check(b == null && manifest == null, "destroyed objects must compare Unity-null");
            Check((object)b != null && (object)manifest != null, "managed wrapper was lost");
            Check(!object.ReferenceEquals(b, null), "fake-null fixture is actually null");
            bool threw = false;
            try { manifest.GetAllDependencies("kitchen"); } catch (MissingReferenceException) { threw = true; }
            Check(threw, "destroyed manifest incorrectly permits native access");
        });
        Cases.Add("fresh and preloader-only caches need no cleanup but cannot reset", delegate {
            var m = Manager(false);
            Check(LayoutEditorAssetBundleLifetime.InvalidStateReason(m, m.stub.levelInfo.dependencies) == null, "fresh cache rejected");
            Check(!ResetPrefix(Pseudo("kitchen")), "fresh cache allowed native reset without manifest");
            m.Cache.Add("kitchen", new AssetBundle());
            Check(LayoutEditorAssetBundleLifetime.InvalidStateReason(m, m.stub.levelInfo.dependencies) == null, "preloaded partial cache rejected");
            Check(!LayoutEditorAssetBundleLifetime.ClearStaleState(m, m.stub.levelInfo.dependencies), "fresh cache cleared");
            Check(m.Cache.Count == 1 && m.editedMaterials.Count == 1, "fresh preload/materials changed");
            Check(!ResetPrefix(Pseudo("kitchen")), "preloader-only cache allowed native reset without manifest");
            NoInit(); NoPreviews();
        });
        Cases.Add("valid small manifest cache is a true no-op", delegate {
            var m = Manager(true); var cache = m.Cache; var manifest = m.Manifest;
            var bundle = cache["kitchen"]; var material = m.editedMaterials["old"];
            Check(LayoutEditorAssetBundleLifetime.HasManifest(m), "live manifest not detected");
            Check(LayoutEditorAssetBundleLifetime.InvalidStateReason(m, new string[] { null, "", "kitchen" }) == null, "small cache rejected");
            Check(!LayoutEditorAssetBundleLifetime.ClearStaleState(m, m.stub.levelInfo.dependencies), "healthy cache cleared");
            Check(object.ReferenceEquals(m.Cache, cache) && object.ReferenceEquals(m.Manifest, manifest), "healthy cache replaced");
            Check(bundle.Unloads == 0 && object.ReferenceEquals(m.editedMaterials["old"], material), "healthy assets touched");
        });
        Cases.Add("destroyed manifest is cleared without native reads", delegate {
            var m = Manager(true); var dead = m.Manifest; dead.Destroyed = true;
            Check(!LayoutEditorAssetBundleLifetime.HasManifest(m), "fake-null manifest treated as live");
            Check(LayoutEditorAssetBundleLifetime.InvalidStateReason(m, m.stub.levelInfo.dependencies) != null, "destroyed manifest accepted");
            Check(LayoutEditorAssetBundleLifetime.ClearStaleState(m, m.stub.levelInfo.dependencies), "destroyed manifest not cleared");
            Check(object.ReferenceEquals(m.Manifest, null) && dead.Reads == 0, "dead native manifest accessed or retained");
        });
        Cases.Add("destroyed and true-null bundles invalidate even fresh caches", delegate {
            foreach (bool destroyed in new bool[] { true, false })
            {
                var m = Manager(false); var dead = destroyed ? new AssetBundle { Destroyed = true } : null;
                m.Cache.Add("kitchen", dead);
                Check(LayoutEditorAssetBundleLifetime.InvalidStateReason(m, null) != null, "null bundle accepted");
                Check(LayoutEditorAssetBundleLifetime.ClearStaleState(m, null), "null bundle not cleared");
                Check(m.Cache.Count == 0, "null bundle retained");
                if (destroyed) Check(dead.Unloads == 0, "destroyed bundle unloaded again");
            }
        });
        Cases.Add("missing Windows manifest bundle invalidates cache", delegate {
            var m = Manager(true); m.Cache.Remove("Windows");
            Check(LayoutEditorAssetBundleLifetime.InvalidStateReason(m, null) != null, "manifest without owner accepted");
        });
        Cases.Add("missing direct dependency invalidates cache", delegate {
            var m = Manager(true); m.Cache.Remove("kitchen");
            Check(LayoutEditorAssetBundleLifetime.InvalidStateReason(m, m.stub.levelInfo.dependencies) != null, "missing direct dependency accepted");
        });
        Cases.Add("missing indirect dependency invalidates complete direct set", delegate {
            var m = Manager(true);
            m.Manifest.Edges.Add("kitchen", new string[] { "shared" });
            m.Manifest.Edges.Add("shared", new string[] { "textures" });
            m.Cache.Add("shared", new AssetBundle());
            Check(LayoutEditorAssetBundleLifetime.InvalidStateReason(m, m.stub.levelInfo.dependencies) != null, "second-hop dependency ignored");
            m.Cache.Add("textures", new AssetBundle());
            Check(LayoutEditorAssetBundleLifetime.InvalidStateReason(m, m.stub.levelInfo.dependencies) == null, "complete transitive cache rejected");
            Check(LayoutEditorAssetBundleLifetime.InvalidStateReason(m, null) == null, "null dependency list rejected");
        });
        Cases.Add("true-null dictionary is recreated and material state cleared", delegate {
            var m = Manager(true); m.Cache = null;
            Check(LayoutEditorAssetBundleLifetime.InvalidStateReason(m, null) != null, "null dictionary accepted");
            Check(LayoutEditorAssetBundleLifetime.ClearStaleState(m, null), "null dictionary not repaired");
            Check(m.Cache != null && m.Cache.Count == 0, "dictionary not recreated");
            Check(object.ReferenceEquals(m.Manifest, null) && m.editedMaterials.Count == 0, "stale manifest/material state retained");
            Check(!LayoutEditorAssetBundleLifetime.ClearStaleState(m, null), "clearing fresh state is not idempotent");
        });
        Cases.Add("clear unloads each live bundle once including aliases", delegate {
            var m = Manager(true); var cache = m.Cache;
            var owner = cache["Windows"]; var shared = cache["kitchen"];
            var dead = new AssetBundle { Destroyed = true };
            cache.Add("alias", shared); cache.Add("dead", dead); cache.Add("null", null);
            Check(LayoutEditorAssetBundleLifetime.ClearStaleState(m, null), "stale cache not cleared");
            Check(owner.Unloads == 1 && shared.Unloads == 1 && dead.Unloads == 0, "unload duplicated or skipped");
            Check(owner.UnloadedAssets && shared.UnloadedAssets, "unload must release loaded assets");
            Check(object.ReferenceEquals(cache, m.Cache) && cache.Count == 0, "existing dictionary not cleared in place");
            Check(object.ReferenceEquals(m.Manifest, null) && m.editedMaterials.Count == 0, "manifest/material cache retained");
            Check(!LayoutEditorAssetBundleLifetime.ClearStaleState(m, null), "cleanup repeated");
        });
        Cases.Add("cleanup tolerates absent material cache", delegate {
            var m = Manager(true); m.editedMaterials = null; m.Manifest.Destroyed = true;
            Check(LayoutEditorAssetBundleLifetime.ClearStaleState(m, null), "null material cache blocks cleanup");
            Check(m.editedMaterials == null && m.Cache.Count == 0, "null material cache mishandled");
        });
        var guards = new Dictionary<string, Action<PseudoPrefabManager>>();
        guards.Add("play", delegate(PseudoPrefabManager m) { Application.isPlaying = true; });
        guards.Add("transition", delegate(PseudoPrefabManager m) { EditorApplication.isPlayingOrWillChangePlaymode = true; });
        guards.Add("compile", delegate(PseudoPrefabManager m) { EditorApplication.isCompiling = true; });
        guards.Add("import", delegate(PseudoPrefabManager m) { EditorApplication.isUpdating = true; });
        guards.Add("build", delegate(PseudoPrefabManager m) { BuildPipeline.isBuildingPlayer = true; });
        guards.Add("prepare", delegate(PseudoPrefabManager m) { m.prepareForBuilding = true; });
        guards.Add("export", delegate(PseudoPrefabManager m) { LayoutEditorPseudoReload.ExportBuildInProgress = true; });
        foreach (var guard in guards)
        {
            var set = guard.Value;
            Cases.Add("repair guard: " + guard.Key, delegate {
                var m = Manager(true); var live = m.Cache["kitchen"]; var manifest = m.Manifest;
                manifest.Destroyed = true; set(m);
                Check(!LayoutEditorAssetBundleLifetime.CanRepair(m), "unsafe repair allowed");
                Check(!LayoutEditorAssetBundleLifetime.ClearStaleState(m, m.stub.levelInfo.dependencies), "guarded cache mutated");
                Check(live.Unloads == 0 && m.Cache.Count == 2 && m.editedMaterials.Count == 1, "guard failed to preserve state");
                Check(object.ReferenceEquals(m.Manifest, manifest), "guard replaced manifest");
                Boot(); EditorApplication.Tick(0); NoInit();
                Check(LayoutEditorPseudoReload.Filters == 0, "unsafe recovery inspected dependencies");
            });
            if (guard.Key == "play") continue;
            foreach (string state in new string[] { "missing manifest", "empty dictionary", "healthy" })
            {
                string cacheState = state;
                Cases.Add("edit reset guard: " + guard.Key + " / " + state, delegate {
                    var m = Manager(true); var cache = m.Cache; var manifest = m.Manifest;
                    if (cacheState == "missing manifest") m.Manifest = null;
                    if (cacheState == "empty dictionary") cache.Clear();
                    var expectedManifest = m.Manifest; int count = cache.Count;
                    var live = count > 0 ? cache["kitchen"] : null;
                    set(m);
                    Check(!ResetPrefix(Pseudo("kitchen")), "edit guard allowed native reset: " + cacheState);
                    Check(EditorApplication.Subscribers == 1, "blocked reset did not queue exactly one recovery");
                    Check(!ResetPrefix(Pseudo("kitchen")), "repeated guarded reset allowed");
                    EditorApplication.Tick(0); NoInit(); NoPreviews();
                    Check(EditorApplication.Subscribers == 1, "guarded recovery lost or duplicated callback");
                    Check(LayoutEditorPseudoReload.Filters == 0 && manifest.Reads == 0,
                        "edit guard inspected dependencies or native manifest");
                    Check(object.ReferenceEquals(m.Cache, cache) && cache.Count == count
                        && object.ReferenceEquals(m.Manifest, expectedManifest), "guarded cache changed");
                    Check(m.editedMaterials.Count == 1 && (live == null || live.Unloads == 0),
                        "guarded assets or materials changed");
                });
            }
        }
        Cases.Add("play reset gate preserves native ownership without validating cache", delegate {
            Application.isPlaying = true;
            Check(LayoutEditorAssetBundleLifetime.CanResetChild(Pseudo("kitchen")), "play reset gate changed");
            var m = Manager(false);
            Check(LayoutEditorAssetBundleLifetime.CanResetChild(Pseudo("kitchen")), "play empty cache intercepted");
            m.Destroyed = true;
            Check(LayoutEditorAssetBundleLifetime.CanResetChild(Pseudo("kitchen")), "play destroyed manager intercepted");
            EditorApplication.Tick(0); NoInit(); NoPreviews();
            Check(LayoutEditorPseudoReload.Filters == 0, "play gate inspected dependencies");
        });
        foreach (bool destroyed in new bool[] { false, true })
        {
            bool dead = destroyed;
            Cases.Add("edit reset blocks " + (dead ? "destroyed" : "missing") + " manager", delegate {
                var m = dead ? Manager(true) : null;
                if (dead) m.Destroyed = true;
                Check(!LayoutEditorAssetBundleLifetime.CanRepair(m), "absent manager accepted for repair");
                Check(!ResetPrefix(Pseudo("kitchen")), "absent manager allowed native reset");
                EditorApplication.Tick(0); NoInit(); NoPreviews();
                Check(EditorApplication.Subscribers == 1, "temporarily absent manager loses recovery");
                Check(LayoutEditorPseudoReload.Filters == 0, "absent manager inspected dependencies");
            });
        }
        Cases.Add("native loader reflection invokes exactly once", delegate {
            var m = Manager(false); int calls = 0;
            m.OnLoad = delegate { calls++; MakeHealthy(m); };
            LayoutEditorAssetBundleLifetime.EnsureBundlesLoaded(m);
            Check(calls == 1 && m.Loads == 1 && LayoutEditorAssetBundleLifetime.HasManifest(m), "native loader not invoked");
        });
        Cases.Add("ResetChild defers through installed prefix without nested Init", delegate {
            var m = Manager(false); var pseudo = Pseudo("kitchen");
            LayoutEditorPseudoReload.InitAction = MakeHealthy;
            Check(!ResetPrefix(pseudo), "unsafe ResetChild was not suppressed");
            NoInit(); Check(LayoutEditorPseudoReload.CustomResets == 0, "custom reset ran before recovery");
            Check(EditorApplication.Subscribers == 1, "deferred recovery not queued exactly once");
            Check(!ResetPrefix(pseudo), "repeated unsafe reset allowed"); NoInit();
            Check(EditorApplication.Subscribers == 1, "duplicate update callbacks");
            EditorApplication.Tick(0);
            Check(LayoutEditorPseudoReload.Inits == 1, "deferred Init missing"); Completed();
            Check(ResetPrefix(pseudo), "healthy ResetChild remains suppressed");
            Check(LayoutEditorPseudoReload.CustomResets == 1, "healthy custom reset path skipped");
            Check(LayoutEditorAssetBundleLifetime.HasManifest(m), "recovery did not install manifest");
        });
        Cases.Add("healthy reset preserves normal and custom paths without reinit", delegate {
            Manager(true); var pseudo = Pseudo("kitchen");
            Check(ResetPrefix(pseudo), "normal native reset suppressed");
            LayoutEditorPseudoReload.CustomHandled = true;
            Check(!ResetPrefix(pseudo), "custom handled reset still invokes native path");
            NoInit(); EditorApplication.Tick(0); NoInit();
            Check(LayoutEditorPseudoReload.Children == 0 && LayoutEditorPushablePotPreview.Arms == 0, "healthy previews rebuilt");
        });
        Cases.Add("material replacement override is guarded by its own installed prefix", delegate {
            Manager(false);
            var pseudo = new PseudoPrefabReplaceMaterial { Attached = new PseudoPrefabStub {
                pseudoPrefabSO = new PseudoPrefabSO { bundleName = "kitchen" } } };
            LayoutEditorPseudoReload.InitAction = MakeHealthy;
            Check(!ResetPrefix(pseudo), "unsafe material replacement override was not suppressed"); NoInit();
            Check(HarmonyLib.Harmony.Prefixes.ContainsKey("PseudoPrefab.ResetChild"), "base reset patch lost");
            Check(HarmonyLib.Harmony.Prefixes.ContainsKey("PseudoPrefabManager.Init"), "manager Init patch lost");
            Check(LayoutEditorPseudoReload.CustomResets == 0, "override reached custom reset while unsafe");
            EditorApplication.Tick(0); Completed();
            Check(ResetPrefix(pseudo), "healthy material replacement override suppressed");
            Check(LayoutEditorPseudoReload.Inits == 1, "override recovery initialized more than once");
        });
        Cases.Add("ResetChild validates the requested bundle", delegate {
            Manager(true);
            Check(!LayoutEditorAssetBundleLifetime.CanResetChild(Pseudo("missing")), "missing preview bundle allowed");
            NoInit();
            Check(EditorApplication.Subscribers == 1, "missing bundle did not queue recovery");
        });
        Cases.Add("asset-backed and stubless reset do not demand a bundle", delegate {
            Manager(true);
            var pseudo = Pseudo("unbuilt-local");
            ((PseudoPrefabStub)pseudo.Attached).pseudoPrefabSO.asset = new UnityEngine.Object();
            Check(LayoutEditorAssetBundleLifetime.CanResetChild(pseudo), "local direct asset incorrectly demands bundle");
            Check(LayoutEditorAssetBundleLifetime.CanResetChild(new PseudoPrefab()), "stubless preview rejected"); NoInit();
        });
        foreach (string state in new string[] { "missing manifest", "destroyed manifest", "empty dictionary", "missing bundle", "healthy" })
        {
            string cacheState = state;
            Cases.Add("CallingOriginalInit reset validates cache: " + state, delegate {
                var m = Manager(true);
                if (cacheState == "missing manifest") m.Manifest = null;
                if (cacheState == "destroyed manifest") m.Manifest.Destroyed = true;
                if (cacheState == "empty dictionary") m.Cache.Clear();
                if (cacheState == "missing bundle") m.Cache.Remove("kitchen");
                Boot(); EditorApplication.Mode(PlayModeStateChange.ExitingEditMode);
                LayoutEditorPseudoReload.CallingOriginalInit = true;
                bool healthy = cacheState == "healthy";
                Check(ResetPrefix(Pseudo("kitchen")) == healthy, "native Init reset ignored cache readiness");
                Check(EditorApplication.Subscribers == (healthy ? 0 : 1), "native Init reset queued wrong recovery");
                EditorApplication.Tick(0); NoInit();
                Check(LayoutEditorPseudoReload.CustomResets == (healthy ? 1 : 0), "unsafe native Init reached custom path");
                Check(LayoutEditorPseudoReload.Children == 0 && LayoutEditorPushablePotPreview.Arms == 0,
                    "native Init reset synchronously rebuilt previews");
            });
        }
        Cases.Add("domain-load recovery waits for scene info then succeeds", delegate {
            var m = Manager(false); var stub = m.stub; m.stub = null; stub.levelInfo = null;
            Boot(); Check(EditorApplication.Subscribers == 1, "domain load did not schedule recovery");
            EditorApplication.Tick(0); NoInit();
            Check(object.ReferenceEquals(m.stub, stub), "manager component stub not adopted");
            Check(EditorApplication.Subscribers == 1, "missing info discarded recovery");
            stub.levelInfo = new LevelInfoSO { dependencies = new string[] { "kitchen" } };
            LayoutEditorPseudoReload.InitAction = MakeHealthy;
            EditorApplication.Tick(0.6);
            Check(LayoutEditorPseudoReload.Inits == 1, "restored scene did not recover"); Completed();
        });
        Cases.Add("recovery uses filtered dependencies without mutating persisted list", delegate {
            var m = Manager(true); var persisted = new string[] { "kitchen", "unbuilt-local" };
            m.stub.levelInfo.dependencies = persisted;
            LayoutEditorPseudoReload.Filtered = new string[] { "kitchen" };
            Boot(); EditorApplication.Tick(0); NoInit();
            Check(LayoutEditorPseudoReload.Filters == 1, "preview dependency filter not called");
            Check(object.ReferenceEquals(LayoutEditorPseudoReload.FilterManager, m)
                && object.ReferenceEquals(LayoutEditorPseudoReload.FilterInfo, m.stub.levelInfo), "filter used wrong context");
            Check(object.ReferenceEquals(m.stub.levelInfo.dependencies, persisted) && persisted.Length == 2, "persisted dependency list mutated");
            Check(EditorApplication.Subscribers == 0, "healthy filtered cache recovery did not settle");
        });
        Cases.Add("filtered dependency absent from cache still triggers recovery", delegate {
            var m = Manager(true); m.Cache.Remove("kitchen");
            LayoutEditorPseudoReload.Filtered = new string[] { "kitchen" };
            LayoutEditorPseudoReload.InitAction = MakeHealthy;
            Boot(); EditorApplication.Tick(0);
            Check(LayoutEditorPseudoReload.Filters >= 1 && LayoutEditorPseudoReload.Inits == 1, "filtered required dependency ignored"); Completed();
        });
        Cases.Add("post-init validation uses preview filter rather than persisted dependencies", delegate {
            var m = Manager(false); var persisted = new string[] { "kitchen", "unbuilt-local" };
            m.stub.levelInfo.dependencies = persisted;
            LayoutEditorPseudoReload.Filtered = new string[] { "kitchen" };
            LayoutEditorPseudoReload.InitAction = MakeHealthy;
            Boot(); EditorApplication.Tick(0);
            Check(LayoutEditorPseudoReload.Inits == 1, "filtered recovery failed"); Completed();
            Check(object.ReferenceEquals(m.stub.levelInfo.dependencies, persisted), "post-init recovery changed persisted dependencies");
            Check(!m.Cache.ContainsKey("unbuilt-local"), "fixture unexpectedly loaded unbuilt dependency");
            EditorApplication.Tick(1);
            Check(LayoutEditorPseudoReload.Inits == 1, "persisted-only dependency caused redundant retry");
        });
        Cases.Add("partial Init with live manifest must retry missing dependencies", delegate {
            Manager(false);
            LayoutEditorPseudoReload.InitAction = delegate(PseudoPrefabManager m) {
                MakeHealthy(m);
                if (LayoutEditorPseudoReload.Inits == 1) m.Cache.Remove("kitchen");
            };
            Boot(); EditorApplication.Tick(0);
            Check(LayoutEditorPseudoReload.Inits == 1 && EditorApplication.Subscribers == 1, "live manifest hid incomplete recovery");
            Check(LayoutEditorPseudoReload.Children == 0 && LayoutEditorPushablePotPreview.Arms == 0, "partial cache rebuilt previews");
            EditorApplication.Tick(0.6);
            Check(LayoutEditorPseudoReload.Inits == 2, "incomplete cache did not retry"); Completed();
        });
        Cases.Add("recovery succeeds on second deferred attempt", delegate {
            Manager(false); var pseudo = Pseudo("kitchen");
            LayoutEditorPseudoReload.InitAction = delegate(PseudoPrefabManager m) {
                Check(!ResetPrefix(pseudo), "recovering Init allowed reset before populating cache");
                Check(LayoutEditorPseudoReload.CustomResets == 0, "recovering Init reached custom reset before loading");
                if (LayoutEditorPseudoReload.Inits == 2)
                {
                    MakeHealthy(m);
                    Check(ResetPrefix(pseudo), "recovering Init blocked reset after populating cache");
                    Check(LayoutEditorPseudoReload.CustomResets == 1, "ready recovery skipped custom reset path");
                }
            };
            Boot(); EditorApplication.Tick(0);
            Check(LayoutEditorPseudoReload.Inits == 1 && EditorApplication.Subscribers == 1, "first failure did not retain retry");
            Check(LayoutEditorPseudoReload.Children == 0 && LayoutEditorPseudoReload.Materials == 0, "failed Init repaired previews prematurely");
            EditorApplication.Tick(0.1); Check(LayoutEditorPseudoReload.Inits == 1, "retry not deferred/throttled");
            EditorApplication.Tick(0.5);
            Check(LayoutEditorPseudoReload.Inits == 2, "second attempt missing"); Completed();
        });
        Cases.Add("repeated ResetChild cannot replenish two-attempt retry budget", delegate {
            Manager(false); var pseudo = Pseudo("kitchen"); Boot(); EditorApplication.Tick(0);
            Check(LayoutEditorPseudoReload.Inits == 1, "first attempt missing");
            for (int i = 0; i < 5; i++) Check(!LayoutEditorAssetBundleLifetime.CanResetChild(pseudo), "unsafe reset allowed during retry");
            Check(EditorApplication.Subscribers == 1, "repeated reset duplicated recovery");
            EditorApplication.Tick(0.6);
            Check(LayoutEditorPseudoReload.Inits == 2 && EditorApplication.Subscribers == 0, "retry budget reset or not stopped at two");
            Check(LayoutEditorLog.Warnings.Count == 1, "exhausted recovery did not warn once");
            EditorApplication.Tick(1); Check(LayoutEditorPseudoReload.Inits == 2, "third automatic attempt ran");
            for (int i = 0; i < 5; i++)
            {
                Check(!LayoutEditorAssetBundleLifetime.CanResetChild(pseudo), "exhausted recovery allows unsafe reset");
                Check(EditorApplication.Subscribers == 0, "ResetChild rearmed exhausted recovery");
                EditorApplication.Tick(0.6);
            }
            EditorApplication.Tick(16);
            Check(!LayoutEditorAssetBundleLifetime.CanResetChild(pseudo), "expired exhausted recovery allows unsafe reset");
            Check(EditorApplication.Subscribers == 0, "deadline expiry replenished exhausted retry budget");
            EditorApplication.Tick(0.6);
            Check(LayoutEditorPseudoReload.Inits == 2 && LayoutEditorLog.Warnings.Count == 1, "exhausted recovery retried or warned again");
            Check(LayoutEditorPseudoReload.Children == 0 && LayoutEditorPseudoReload.Materials == 0, "failed recovery changed previews");
        });
        Cases.Add("new edit entry grants a fresh two-attempt budget before old deadline", delegate {
            Manager(false); Boot(); EditorApplication.Tick(0); EditorApplication.Tick(0.6);
            Check(LayoutEditorPseudoReload.Inits == 2 && EditorApplication.Subscribers == 0, "initial budget not exhausted");
            LayoutEditorPseudoReload.InitAction = delegate(PseudoPrefabManager m) {
                if (LayoutEditorPseudoReload.Inits == 4) MakeHealthy(m);
            };
            EditorApplication.Mode(PlayModeStateChange.EnteredEditMode);
            Check(EditorApplication.Subscribers == 1, "edit entry did not restart exhausted recovery");
            EditorApplication.Tick(0);
            Check(LayoutEditorPseudoReload.Inits == 3 && EditorApplication.Subscribers == 1, "new edit entry did not reset retry/check window");
            EditorApplication.Tick(0.6);
            Check(LayoutEditorPseudoReload.Inits == 4, "new edit entry did not grant second fresh attempt"); Completed();
            Check(LayoutEditorLog.Warnings.Count == 1, "fresh successful recovery emitted another exhaustion warning");
        });
        Cases.Add("direct stale cleanup preserves deferred rebuild after SafeInit repairs cache", delegate {
            var m = Manager(true); Boot(); EditorApplication.Tick(0); NoInit();
            Check(EditorApplication.Subscribers == 0, "initial healthy recovery did not settle");
            LayoutEditorPseudoReload.Materials = 0; LayoutEditorPseudoReload.Switches = 0;
            m.Manifest.Destroyed = true;
            Check(LayoutEditorAssetBundleLifetime.ClearStaleState(m, m.stub.levelInfo.dependencies), "direct stale cleanup failed");
            Check(EditorApplication.Subscribers == 1, "direct stale cleanup did not schedule recovery");
            Check(LayoutEditorPseudoReload.Children == 0, "cleanup rebuilt previews synchronously");
            LayoutEditorPseudoReload.InitAction = MakeHealthy;
            LayoutEditorPseudoReload.SafeInit(m);
            Check(LayoutEditorAssetBundleLifetime.InvalidStateReason(m, m.stub.levelInfo.dependencies) == null
                && LayoutEditorAssetBundleLifetime.HasManifest(m), "SafeInit boundary did not repair cache");
            Check(LayoutEditorPseudoReload.Children == 0, "previews rebuilt before deferred update");
            EditorApplication.Tick(0.6); Completed();
            Check(LayoutEditorPseudoReload.Inits == 1, "valid cache unnecessarily reinitialized during deferred rebuild");
            EditorApplication.Mode(PlayModeStateChange.EnteredEditMode); EditorApplication.Tick(0);
            Check(LayoutEditorPseudoReload.Children == 1 && LayoutEditorPushablePotPreview.Arms == 1, "pending rebuild not consumed exactly once");
        });
        Cases.Add("rebuild runs LateSetup after all replacements and isolates each object failure", delegate {
            Manager(false); LayoutEditorPseudoReload.InitAction = MakeHealthy;
            var trace = new List<string>();
            var retired = new PseudoPrefab(); UnityEngine.Object.SceneObjects.Add(retired);
            var bad = new PseudoPrefab { name = "broken-normal" };
            var linked = new PseudoPrefabReplaceMaterial { name = "linked-normal" };
            var badCustom = new SetupCustomPrefab { name = "broken-custom" };
            var linkedCustom = new SetupCustomPrefab { name = "linked-custom" };
            UnityEngine.Object normalTarget = retired, customTarget = retired;
            int replacements = 0;
            Action verifyLinks = delegate {
                Check(replacements == 3 && LayoutEditorPseudoReload.Children == 1, "LateSetup ran before every replacement completed");
                Check(object.ReferenceEquals(normalTarget, linked) && object.ReferenceEquals(customTarget, linkedCustom), "LateSetup saw stale cross-object targets");
                Check(LayoutEditorPushablePotPreview.Arms == 0, "pot preview armed before LateSetup finished");
            };
            bad.OnLateSetup = delegate {
                verifyLinks(); trace.Add("late:broken-normal"); throw new InvalidOperationException("normal fixture failure");
            };
            linked.OnLateSetup = delegate { verifyLinks(); trace.Add("late:linked-normal"); };
            badCustom.OnLateSetup = delegate {
                verifyLinks(); trace.Add("late:broken-custom"); throw new InvalidOperationException("custom fixture failure");
            };
            linkedCustom.OnLateSetup = delegate { verifyLinks(); trace.Add("late:linked-custom"); };
            LayoutEditorPseudoReload.RepairAction = delegate {
                retired.Destroyed = true;
                UnityEngine.Object.SceneObjects.Add(bad); UnityEngine.Object.SceneObjects.Add(linked);
                normalTarget = linked; replacements++; trace.Add("replace:normal");
                UnityEngine.Object.SceneObjects.Add(badCustom); replacements++; trace.Add("replace:broken-custom");
                UnityEngine.Object.SceneObjects.Add(linkedCustom);
                customTarget = linkedCustom; replacements++; trace.Add("replace:linked-custom");
            };
            Boot(); EditorApplication.Tick(0); Completed();
            Check(string.Join(",", trace.ToArray()) == "replace:normal,replace:broken-custom,replace:linked-custom,late:broken-normal,late:linked-normal,late:broken-custom,late:linked-custom",
                "LateSetup ordering or failure isolation changed");
            Check(retired.LateCalls == 0 && bad.LateCalls == 1 && linked.LateCalls == 1
                && badCustom.LateCalls == 1 && linkedCustom.LateCalls == 1, "LateSetup missed or repeated replacement objects");
            Check(LayoutEditorLog.Warnings.Count == 2, "per-object failures not independently reported");
            Check(LayoutEditorLog.Warnings[0].Contains("broken-normal") && LayoutEditorLog.Warnings[1].Contains("broken-custom"),
                "LateSetup warnings lack failing object identity");
            EditorApplication.Tick(1);
            Check(linked.LateCalls == 1 && linkedCustom.LateCalls == 1, "completed LateSetup repeated");
        });
        Cases.Add("play transitions cancel and edit entry reschedules recovery", delegate {
            Manager(false); LayoutEditorPseudoReload.InitAction = MakeHealthy; Boot();
            foreach (var state in new PlayModeStateChange[] { PlayModeStateChange.ExitingEditMode,
                PlayModeStateChange.EnteredPlayMode, PlayModeStateChange.ExitingPlayMode })
            {
                EditorApplication.Mode(state);
                Check(EditorApplication.Subscribers == 0, "non-edit transition left recovery running");
                EditorApplication.Tick(0.6); NoInit();
                EditorApplication.Mode(PlayModeStateChange.EnteredEditMode);
                Check(EditorApplication.Subscribers == 1, "edit entry failed to queue recovery");
            }
            EditorApplication.Tick(0.6); Check(LayoutEditorPseudoReload.Inits == 1, "edit return did not recover"); Completed();
        });
        foreach (bool importing in new bool[] { false, true })
        {
            bool import = importing;
            Cases.Add("transient " + (import ? "import" : "compile") + " guard recovers before reset", delegate {
                var m = Manager(false); var pseudo = Pseudo("kitchen");
                LayoutEditorPseudoReload.InitAction = delegate(PseudoPrefabManager manager) {
                    LayoutEditorPseudoReload.CallingOriginalInit = true;
                    try
                    {
                        Check(!ResetPrefix(pseudo), "original recovery Init allowed reset before loading");
                        MakeHealthy(manager);
                        Check(ResetPrefix(pseudo), "original recovery Init blocked healthy children");
                    }
                    finally { LayoutEditorPseudoReload.CallingOriginalInit = false; }
                };
                EditorApplication.isCompiling = !import; EditorApplication.isUpdating = import;
                Check(!ResetPrefix(pseudo), "transient guard allowed empty-cache reset");
                EditorApplication.Tick(0); NoInit(); NoPreviews();
                Check(EditorApplication.Subscribers == 1, "transient guard discarded recovery");
                EditorApplication.isCompiling = false; EditorApplication.isUpdating = false;
                EditorApplication.Tick(0.6);
                Check(LayoutEditorPseudoReload.Inits == 1, "recovery lost after transient guard cleared"); Completed();
                Check(LayoutEditorAssetBundleLifetime.HasManifest(m) && ResetPrefix(pseudo), "recovered reset failed");
                Check(LayoutEditorPseudoReload.CustomResets == 2, "ready Init/final reset paths missing");
            });
        }
        Cases.Add("CallingOriginalInit postpones recovery without spending attempts", delegate {
            Manager(false); LayoutEditorPseudoReload.InitAction = MakeHealthy;
            LayoutEditorPseudoReload.CallingOriginalInit = true; Boot();
            EditorApplication.Tick(0); EditorApplication.Tick(0.6); EditorApplication.Tick(0.6); NoInit();
            LayoutEditorPseudoReload.CallingOriginalInit = false; EditorApplication.Tick(0.6);
            Check(LayoutEditorPseudoReload.Inits == 1, "guard consumed retry budget"); Completed();
        });
        Cases.Add("healthy edit-mode recovery never reinitializes or rebuilds children", delegate {
            var m = Manager(true); var bundle = m.Cache["kitchen"]; Boot(); EditorApplication.Tick(0); NoInit();
            Check(LayoutEditorPseudoReload.Children == 0 && LayoutEditorPushablePotPreview.Arms == 0, "normal recovery rebuilt previews");
            Check(LayoutEditorPseudoReload.Materials == 1 && LayoutEditorPseudoReload.Switches == 1, "normal light repairs missing");
            Check(bundle.Unloads == 0 && m.editedMaterials.Count == 1, "healthy cache changed");
            Check(EditorApplication.Subscribers == 0, "normal recovery did not unsubscribe");
            EditorApplication.Tick(1); NoInit();
            Check(LayoutEditorPseudoReload.Materials == 1, "settled recovery still polling");
        });
        Cases.Add("recovery deadline expires and a later edit entry can retry", delegate {
            PseudoPrefabManager.Instance = null; Boot(); EditorApplication.Tick(16);
            Check(EditorApplication.Subscribers == 0, "expired recovery still polling"); NoInit();
            Manager(false); LayoutEditorPseudoReload.InitAction = MakeHealthy;
            EditorApplication.Mode(PlayModeStateChange.EnteredEditMode); EditorApplication.Tick(0);
            Check(LayoutEditorPseudoReload.Inits == 1, "new recovery window did not start"); Completed();
        });
        Cases.Add("unexpected recovery exception stops callbacks and permits later recovery", delegate {
            Manager(false); LayoutEditorPseudoReload.InitAction = delegate(PseudoPrefabManager m) { throw new InvalidOperationException("fixture failure"); };
            Boot(); EditorApplication.Tick(0);
            Check(LayoutEditorPseudoReload.Inits == 1 && EditorApplication.Subscribers == 0, "exception leaves recovery spinning");
            Check(LayoutEditorLog.Warnings.Count == 1, "exception not reported");
            LayoutEditorPseudoReload.InitAction = MakeHealthy;
            EditorApplication.Mode(PlayModeStateChange.EnteredEditMode); EditorApplication.Tick(0.6);
            Check(LayoutEditorPseudoReload.Inits == 2, "recovering flag was not released"); Completed();
        });
        Cases.Add("Init prefix preserves play and original-Init escape paths", delegate {
            var m = Manager(false);
            RuntimeHelpers.RunClassConstructor(typeof(LayoutEditorDependencyRepair).TypeHandle);
            var prefix = HarmonyLib.Harmony.Prefixes["PseudoPrefabManager.Init"];
            Application.isPlaying = true;
            Check((bool)prefix.Invoke(null, new object[] { m }), "play Init intercepted"); NoInit();
            Application.isPlaying = false; LayoutEditorPseudoReload.CallingOriginalInit = true;
            Check((bool)prefix.Invoke(null, new object[] { m }), "original Init recursion escape lost"); NoInit();
            LayoutEditorPseudoReload.CallingOriginalInit = false;
            Check(!(bool)prefix.Invoke(null, new object[] { m }), "edit Init not intercepted");
            Check(LayoutEditorPseudoReload.Inits == 1, "edit Init not routed through SafeInit");
        });
    }
    public static int Main(string[] args)
    {
        Register();
        if (args.Length == 1 && args[0] == "--list")
        { foreach (string name in Cases.Keys) Console.WriteLine(name); return 0; }
        if (args.Length != 1 || !Cases.ContainsKey(args[0])) return 2;
        try
        {
            Cases[args[0]]();
            Console.WriteLine("PASS: " + args[0]);
            Console.WriteLine("ASSERTIONS: " + assertions);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL: " + args[0] + "\n" + ex); return 1; }
    }
}
'''


def run(command, directory, env, timeout=120):
    result = subprocess.run(command, cwd=directory, env=env, text=True,
                            capture_output=True, timeout=timeout)
    if result.returncode:
        raise RuntimeError("Command failed: " + " ".join(map(str, command)) + "\n"
                           + result.stdout + result.stderr)
    return result.stdout


def main():
    require(shutil.which("dotnet") is not None, "dotnet SDK 8 required")
    require(TEMP_PARENT.is_dir(), "Approved temporary directory is missing: " + str(TEMP_PARENT))
    require(TEMP_PARENT != ROOT and ROOT not in TEMP_PARENT.parents, "Temporary projects must stay outside repo")
    # Capture once so contracts and compilation describe the same revision, then
    # reject concurrent source drift instead of reporting stale passing checks.
    snapshots = {name: (EDITOR / name).read_bytes() for name in SOURCES}
    sources = {name: data.decode("utf-8-sig") for name, data in snapshots.items()}
    contracts = source_contracts(sources)
    with tempfile.TemporaryDirectory(prefix="preview-bundle-lifetime-", dir=TEMP_PARENT) as temp:
        directory = Path(temp)
        (directory / "NuGet.Config").write_text(
            '<configuration><packageSources><clear /></packageSources></configuration>', encoding="utf-8")
        (directory / "Checks.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion>'
            '<GenerateAssemblyInfo>false</GenerateAssemblyInfo><EnableNETAnalyzers>false</EnableNETAnalyzers>'
            '<NuGetAudit>false</NuGetAudit><RestoreIgnoreFailedSources>false</RestoreIgnoreFailedSources>'
            '</PropertyGroup></Project>', encoding="utf-8")
        (directory / "Program.cs").write_text(DOUBLES + TESTS, encoding="utf-8")
        for name in SOURCES[:2]:
            (directory / name).write_bytes(snapshots[name])
        env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1",
                   DOTNET_NOLOGO="1", DOTNET_CLI_HOME=str(directory / "sdk-home"),
                   TMPDIR=str(directory), PYTHONDONTWRITEBYTECODE="1")
        run(["dotnet", "build", str(directory / "Checks.csproj"), "--verbosity", "quiet",
             "--disable-build-servers"], directory, env)
        executable = directory / "bin/Debug/net8.0/Checks.dll"
        scenarios = run(["dotnet", str(executable), "--list"], directory, env).splitlines()
        require(len(scenarios) >= 30 and len(set(scenarios)) == len(scenarios), "Missing or duplicate scenarios")
        assertions = 0
        failures = []
        for scenario in scenarios:
            try:
                output = run(["dotnet", str(executable), scenario], directory, env, timeout=30)
                match = re.search(r"^ASSERTIONS: (\d+)$", output, re.MULTILINE)
                require(match is not None, "Missing assertion count: " + scenario)
                assertions += int(match.group(1))
                print("PASS: " + scenario, flush=True)
            except (AssertionError, RuntimeError, subprocess.TimeoutExpired) as error:
                failures.append(str(error))
                print(str(error), flush=True)
        changed = [name for name in SOURCES if (EDITOR / name).read_bytes() != snapshots[name]]
        require(not changed, "Production sources changed during checks; rerun against latest: " + ", ".join(changed))
        require(not failures, str(len(failures)) + " behavioral scenario(s) failed")
    digest = hashlib.sha256(snapshots[SOURCES[0]]).hexdigest()[:12]
    print("RESULT: " + str(len(scenarios)) + " behavioral scenarios, " + str(assertions)
          + " assertions, " + str(contracts) + " source contracts; C#4/net8; helper SHA256 " + digest, flush=True)
    print("LIMIT: Unity fake-null/editor events and manager loader are doubles; Harmony prefixes are invoked "
          "directly, not patched. SafeInit ordering is source checked; its execution and the real dependency "
          "filter, native bundle loading, Unity 2017/net35 compatibility, scene restoration, atlas rendering "
          "and domain reload timing require Unity validation. No scenes mutated or HTTP calls made.", flush=True)


if __name__ == "__main__":
    main()
