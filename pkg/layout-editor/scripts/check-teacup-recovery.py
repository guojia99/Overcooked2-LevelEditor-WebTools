#!/usr/bin/env python3
"""Exercise actual teacup recovery methods with Unity mocks, then audit preloads.

Run: python3 layout-editor/scripts/check-teacup-recovery.py
Requires dotnet SDK 8 and UnityPy (for the read-only commonw5 bundle audit).
Use --skip-bundle for the C# behavior checks alone. This is not a Unity player
test or a net35 compatibility check. Generated C# and build outputs live in a
temporary directory; no Unity assets or production sources are written.
"""
import argparse
import hashlib
import os
from pathlib import Path
import re
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/WebCustomStubRuntime/CustomContent/DirtyTeacupMergeWatcher.cs"
BUNDLE = ROOT / "Assets/AssetBundles/commonw5"


def extract(source, name):
    match = re.search(r"        private (?:static )?[^\n]+\b" + name + r"\(", source)
    if not match:
        raise AssertionError("Missing production method: " + name)
    start = source.index("{", match.start())
    tokens = r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/|[{}]'
    depth = 0
    for token in re.finditer(tokens, source[start:]):
        if token.group() == "{":
            depth += 1
        elif token.group() == "}":
            depth -= 1
            if depth == 0:
                return source[match.start():start + token.end()]
    raise AssertionError("Unbalanced production method: " + name)


HARNESS = r'''
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using LevelEditorStub;

namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static bool operator ==(Object a, Object b)
        {
            bool an = ReferenceEquals(a, null) || (!ReferenceEquals(a, null) && a.Destroyed);
            bool bn = ReferenceEquals(b, null) || (!ReferenceEquals(b, null) && b.Destroyed);
            return an || bn ? an == bn : ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) { return !(a == b); }
        public override bool Equals(object other) { return ReferenceEquals(this, other); }
        public override int GetHashCode() { return base.GetHashCode(); }
    }
    public struct Scene
    {
        public bool Valid;
        public bool IsValid() { return Valid; }
    }
    public class Component : Object { public GameObject gameObject; }
    public class MonoBehaviour : Component { }
    public class GameObject : Object
    {
        public string name;
        public Scene scene;
        readonly List<Component> components = new List<Component>();
        public GameObject(string n) { name = n; }
        public T Add<T>(T c) where T : Component
        {
            c.gameObject = this;
            components.Add(c);
            return c;
        }
        public T GetComponent<T>() where T : Component
        {
            foreach (var c in components) if (c is T && c != null) return (T)c;
            return null;
        }
    }
    public class WaitForSeconds
    {
        public readonly float Seconds;
        public WaitForSeconds(float seconds) { Seconds = seconds; }
    }
    public static class Resources
    {
        public static Object[] Loaded = new Object[0];
        public static bool Throw;
        public static int Calls;
        public static Object[] FindObjectsOfTypeAll(Type t)
        {
            Calls++;
            if (Throw) throw new Exception("mock resource failure " + Calls);
            var result = new List<Object>();
            foreach (var o in Loaded)
                if (!ReferenceEquals(o, null) && t.IsAssignableFrom(o.GetType())) result.Add(o);
            return result.ToArray();
        }
    }
}
namespace LevelEditorStub
{
    public class SpecificPseudoPrefabTag : Component { public string prefabTag; }
}
class StackBase : Component
{
    // Deliberately private and inherited: exercise the real hierarchy lookup.
    private GameObject m_platePrefab;
    private GameObject m_washedPrefab;
    public void Wire(GameObject plate, GameObject washed)
    {
        m_platePrefab = plate;
        m_washedPrefab = washed;
    }
}
class DirtyPlateStack : StackBase { }
static class GameApi
{
    public static Type StackType = typeof(DirtyPlateStack);
    public static bool Server;
    public static Type Find(string name) { return name == "DirtyPlateStack" ? StackType : null; }
    public static bool IsServerMachine() { return Server; }
}
static class StubLog
{
    public static readonly List<string> Warnings = new List<string>();
    public static void LogWarn(string message) { Warnings.Add(message); }
    public static void Dbg(string message) { }
}
class Watcher : MonoBehaviour
{
    public GameObject m_stackPrefab;
    public int MergeCalls;
    public IEnumerator Begin() { return Start(); }
    public void Recover() { RestoreStackPrefab(); }
    private void MergeStep() { MergeCalls++; }
    // Production constants and methods are inserted below without rewriting.
__PRODUCTION__
}
class Tests
{
    static int checks;
    static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL: " + label);
        checks++;
        Console.WriteLine("PASS " + label);
    }
    static Watcher Fresh(params UnityEngine.Object[] loaded)
    {
        Resources.Loaded = loaded;
        Resources.Calls = 0;
        Resources.Throw = false;
        GameApi.StackType = typeof(DirtyPlateStack);
        GameApi.Server = false;
        StubLog.Warnings.Clear();
        return new GameObject("scene cup").Add(new Watcher());
    }
    static GameObject Plate(string name, string tag)
    {
        var p = new GameObject(name);
        if (tag != null) p.Add(new SpecificPseudoPrefabTag { prefabTag = tag });
        return p;
    }
    static DirtyPlateStack Stack(string name, GameObject plate, GameObject washed)
    {
        var s = new GameObject(name).Add(new DirtyPlateStack());
        s.Wire(plate, washed);
        return s;
    }
    static DirtyPlateStack Good()
    {
        return Stack("web_teacup_stack_dirty", Plate("web_teacup_dirty", "DirtyTeacup|"),
            new GameObject("web_teacup_stack_clean"));
    }
    static void Reject(DirtyPlateStack s, string label)
    {
        var w = Fresh(s);
        w.Recover();
        Check(w.m_stackPrefab == null, label);
    }
    static void Main()
    {
        var good = Good();
        foreach (var loaded in new UnityEngine.Object[][] {
            new UnityEngine.Object[0], new UnityEngine.Object[] { good },
            new UnityEngine.Object[] { good, Good() } })
        {
            var w = Fresh(loaded);
            var held = new GameObject("serialized reference");
            w.m_stackPrefab = held;
            var start = w.Begin();
            Check(start.MoveNext() && ReferenceEquals(w.m_stackPrefab, held)
                && Resources.Calls == 0 && StubLog.Warnings.Count == 0,
                "Start preserves held reference; loaded candidates=" + loaded.Length);
            GameApi.Server = true;
            start.MoveNext();
            Check(Resources.Calls == 0 && w.MergeCalls == 1
                && ((WaitForSeconds)start.Current).Seconds == 0.5f,
                "held reference skips all recovery waits and scans");
        }
        var watcher = Fresh();
        watcher.Recover();
        Check(watcher.m_stackPrefab == null, "no match stays null");
        watcher = Fresh(good);
        GameApi.StackType = null;
        watcher.Recover();
        Check(watcher.m_stackPrefab == null && Resources.Calls == 0, "missing native type is safe");
        watcher = Fresh(good);
        watcher.Recover();
        Check(ReferenceEquals(watcher.m_stackPrefab, good.gameObject),
            "unique correct asset restored using inherited private fields");
        watcher = Fresh(good, good);
        watcher.Recover();
        Check(ReferenceEquals(watcher.m_stackPrefab, good.gameObject),
            "duplicate enumeration of same identity is not ambiguous");
        foreach (var loaded in new UnityEngine.Object[][] {
            new UnityEngine.Object[] { good, Good() },
            new UnityEngine.Object[] { Good(), good } })
        {
            watcher = Fresh(loaded);
            watcher.Recover();
            Check(watcher.m_stackPrefab == null, "two distinct valid assets refuse recovery");
        }
        var scene = Good();
        scene.gameObject.scene = new Scene { Valid = true };
        Reject(scene, "scene stack with exact name rejected");
        var plate = Plate("web_teacup_dirty", "DirtyTeacup|");
        plate.scene = new Scene { Valid = true };
        Reject(Stack("web_teacup_stack_dirty", plate, new GameObject("web_teacup_stack_clean")),
            "scene plate reference rejected");
        var washed = new GameObject("web_teacup_stack_clean");
        washed.scene = new Scene { Valid = true };
        Reject(Stack("web_teacup_stack_dirty", Plate("web_teacup_dirty", "DirtyTeacup|"), washed),
            "scene washed reference rejected");
        foreach (var family in new string[] { "plate", "mug", "glass", "tray" })
        {
            Reject(Stack("dirty_" + family + "_stack", Plate("dirty_" + family, family + "|"),
                new GameObject("clean_" + family + "_stack")), "wrong family rejected: " + family);
            Reject(Stack("web_teacup_stack_dirty", Plate("dirty_" + family, family + "|"),
                new GameObject("web_teacup_stack_clean")), "wrong plate tag rejected: " + family);
            Reject(Stack("web_teacup_stack_dirty", Plate("web_teacup_dirty", "DirtyTeacup|"),
                new GameObject("clean_" + family + "_stack")), "wrong washed family rejected: " + family);
        }
        foreach (var tag in new string[] { null, "", "dirtyteacup|", "DirtyTeacupOther|" })
            Reject(Stack("web_teacup_stack_dirty", Plate("web_teacup_dirty", tag),
                new GameObject("web_teacup_stack_clean")), "missing/invalid tag rejected: " + tag);
        Reject(Stack("web_teacup_stack_dirty", null, new GameObject("web_teacup_stack_clean")),
            "null plate rejected");
        Reject(Stack("web_teacup_stack_dirty", Plate("web_teacup_dirty", "DirtyTeacup|"), null),
            "null washed rejected");
        Reject(Stack("web_teacup_stack_dirty(Clone)", Plate("web_teacup_dirty", "DirtyTeacup|"),
            new GameObject("web_teacup_stack_clean")), "clone-like asset name rejected");
        var dead = Good();
        dead.Destroyed = true;
        Reject(dead, "Unity destroyed component rejected");
        watcher = Fresh(null, new GameObject("irrelevant asset"), scene, good);
        watcher.Recover();
        Check(ReferenceEquals(watcher.m_stackPrefab, good.gameObject),
            "irrelevant and scene objects do not shadow unique valid template");
        watcher = Fresh(good);
        var loop = watcher.Begin();
        Check(loop.MoveNext() && ReferenceEquals(watcher.m_stackPrefab, good.gameObject)
            && Resources.Calls == 1, "Start restores before first poll yield");
        Check(((WaitForSeconds)loop.Current).Seconds == 0.5f,
            "successful immediate recovery enters normal polling without retry delay");
        watcher = Fresh();
        loop = watcher.Begin();
        Check(loop.MoveNext() && Resources.Calls == 1 && StubLog.Warnings.Count == 0
            && ((WaitForSeconds)loop.Current).Seconds == 1f,
            "missing template yields one second before retry without warning");
        Resources.Loaded = new UnityEngine.Object[] { good };
        Check(loop.MoveNext() && ReferenceEquals(watcher.m_stackPrefab, good.gameObject)
            && Resources.Calls == 2 && StubLog.Warnings.Count == 0,
            "late-loaded template recovers on second attempt");
        Check(((WaitForSeconds)loop.Current).Seconds == 0.5f,
            "late recovery resumes normal poll interval");
        int scans = Resources.Calls;
        loop.MoveNext();
        Check(watcher.MergeCalls == 0 && Resources.Calls == scans,
            "existing client predicate prevents merge and recovery does not repeat");
        GameApi.Server = true;
        loop.MoveNext();
        Check(watcher.MergeCalls == 1 && Resources.Calls == scans,
            "existing server predicate still enables merge after recovery");
        watcher = Fresh();
        loop = watcher.Begin();
        for (int i = 1; i <= 9; i++)
            Check(loop.MoveNext() && Resources.Calls == i
                && ((WaitForSeconds)loop.Current).Seconds == 1f,
                "final-attempt fixture waits one second; attempt=" + i);
        Resources.Loaded = new UnityEngine.Object[] { good };
        Check(loop.MoveNext() && Resources.Calls == 10
            && ReferenceEquals(watcher.m_stackPrefab, good.gameObject)
            && StubLog.Warnings.Count == 0 && ((WaitForSeconds)loop.Current).Seconds == 0.5f,
            "template arriving for tenth attempt still recovers");
        watcher = Fresh();
        loop = watcher.Begin();
        loop.MoveNext();
        var assigned = new GameObject("reference assigned while waiting");
        watcher.m_stackPrefab = assigned;
        Check(loop.MoveNext() && Resources.Calls == 1
            && ReferenceEquals(watcher.m_stackPrefab, assigned)
            && ((WaitForSeconds)loop.Current).Seconds == 0.5f,
            "reference assigned during wait bypasses remaining recovery scans");
        watcher = Fresh(good);
        Resources.Throw = true;
        loop = watcher.Begin();
        Check(loop.MoveNext() && StubLog.Warnings.Count == 0,
            "transient exception does not warn on first attempt");
        Resources.Throw = false;
        Check(loop.MoveNext() && Resources.Calls == 2
            && ReferenceEquals(watcher.m_stackPrefab, good.gameObject)
            && StubLog.Warnings.Count == 0,
            "recovery after transient exception succeeds without terminal warning");
        foreach (var failure in new string[] { "missing", "ambiguous", "exception" })
        {
            watcher = failure == "ambiguous" ? Fresh(good, Good()) : Fresh();
            Resources.Throw = failure == "exception";
            GameApi.Server = true;
            loop = watcher.Begin();
            for (int i = 1; i <= 9; i++)
                Check(loop.MoveNext() && Resources.Calls == i
                    && watcher.m_stackPrefab == null && watcher.MergeCalls == 0
                    && StubLog.Warnings.Count == 0
                    && ((WaitForSeconds)loop.Current).Seconds == 1f,
                    failure + " retries once per second without guessing/warning; attempt=" + i);
            Check(!loop.MoveNext() && Resources.Calls == 10
                && watcher.m_stackPrefab == null && watcher.MergeCalls == 0
                && StubLog.Warnings.Count == 1 && StubLog.Warnings[0].Contains("10"),
                failure + " stops at exactly ten attempts, warns once, never merges");
            if (failure == "exception")
                Check(StubLog.Warnings[0].Contains("mock resource failure 10"),
                    "terminal warning includes last recovery exception");
            Resources.Throw = false;
            Resources.Loaded = new UnityEngine.Object[] { good };
            Check(!loop.MoveNext() && Resources.Calls == 10 && watcher.MergeCalls == 0,
                failure + " coroutine stays finished after template arrives beyond limit");
        }
        Console.WriteLine("Behavior checks: " + checks + " passed (C# 4, net8 mock host).");
    }
}
'''


def audit_bundle(path):
    import UnityPy

    env = UnityPy.load(str(path))
    bundles = [obj.read() for obj in env.objects if obj.type.name == "AssetBundle"]
    assert len(bundles) == 1, "Expected one AssetBundle object"
    bundle = bundles[0]
    assert bundle.m_Name.lower() == "commonw5", bundle.m_Name
    entries = list(bundle.m_Container)
    cup_path = "assets/commonw5/prefabs/teacups/web_teacup_dirty.prefab"
    stack_path = "assets/commonw5/prefabs/teacups/web_teacup_stack_dirty.prefab"

    def gameobject_entry(key):
        hits = [entry for name, entry in entries
                if name == key and entry.asset.deref().type.name == "GameObject"]
        assert len(hits) == 1, "Expected one GameObject at " + key
        return hits[0]

    def tree(pointer):
        return pointer.deref().read_typetree()

    cup = gameobject_entry(cup_path)
    stack = gameobject_entry(stack_path)
    # Resolve in the serialized file owning these pointers, not a global path-ID map.
    owner = stack.asset.deref().assets_file

    def local(ref):
        assert ref["m_FileID"] == 0, "Unexpected external reference"
        return owner.objects[ref["m_PathID"]].read_typetree()

    stack_go = tree(stack.asset)
    cup_go = tree(cup.asset)
    assert stack_go["m_Name"] == "web_teacup_stack_dirty"
    assert cup_go["m_Name"] == "web_teacup_dirty"
    components = [local(c["component"]) for c in stack_go["m_Component"]]
    native = [c for c in components if "m_platePrefab" in c and "m_washedPrefab" in c]
    assert len(native) == 1, "Expected exactly one native dirty stack component"
    native = native[0]
    script = local(native["m_Script"])
    assert (script["m_ClassName"], script["m_AssemblyName"]) == (
        "DirtyPlateStack", "Assembly-CSharp.dll")
    assert native["m_platePrefab"]["m_PathID"] == cup.asset.path_id
    washed = local(native["m_washedPrefab"])
    assert washed["m_Name"] == "web_teacup_stack_clean"
    tags = [local(c["component"]) for c in cup_go["m_Component"]]
    assert any(c.get("prefabTag") == "DirtyTeacup|" for c in tags)
    watchers = [c for c in tags if "m_stackPrefab" in c]
    assert len(watchers) == 1
    assert watchers[0]["m_stackPrefab"]["m_PathID"] == stack.asset.path_id
    preload = bundle.m_PreloadTable[cup.preloadIndex:cup.preloadIndex + cup.preloadSize]
    loaded_ids = {p.path_id for p in preload if p.file_id == 0}
    required = {stack.asset.path_id, native["m_GameObject"]["m_PathID"],
                native["m_platePrefab"]["m_PathID"], native["m_washedPrefab"]["m_PathID"]}
    native_ids = [c["component"]["m_PathID"] for c in stack_go["m_Component"]
                  if local(c["component"]) == native]
    assert len(native_ids) == 1
    required.update(native_ids)
    assert required <= loaded_ids, "Cup preload omits recovery dependencies"
    native_in_bundle = [obj for obj in env.objects if obj.type.name == "MonoBehaviour"
                        and obj.assets_file is owner
                        and obj.read_typetree().get("m_Script") == native["m_Script"]]
    assert len(native_in_bundle) == 1, "Multiple native dirty stack components in commonw5"
    print("PASS bundle " + str(path.relative_to(ROOT) if path.is_relative_to(ROOT) else path), flush=True)
    print("  sha256=" + hashlib.sha256(path.read_bytes()).hexdigest(), flush=True)
    print("  Unity=" + str(owner.unity_version) + "; cup preload objects="
          + str(cup.preloadSize) + "; native DirtyPlateStack count=1", flush=True)
    print("  cup GameObject=" + str(cup.asset.path_id) + "; dirty stack GameObject="
          + str(stack.asset.path_id) + "; native component=" + str(native_ids[0]), flush=True)
    print("  Cup LoadAsset preloads native stack, dirty cup and washed stack. "
          "This audits serialized dependencies, not Unity player execution.", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--skip-bundle", action="store_true")
    parser.add_argument("--bundle", type=Path, default=BUNDLE)
    args = parser.parse_args()
    source = SOURCE.read_text(encoding="utf-8")
    members = [extract(source, name) for name in (
        "Start", "RestoreStackPrefab", "FindFieldInHierarchy")]
    for name in ("TagPrefix", "PollInterval"):
        match = re.search(r"        (?:public|private) const [^\n]+\b" + name + r" = [^;]+;", source)
        assert match, "Missing production constant: " + name
        members.append(match.group())
    print("Extracted production methods: Start, RestoreStackPrefab, FindFieldInHierarchy", flush=True)
    print("Source sha256=" + hashlib.sha256(source.encode()).hexdigest(), flush=True)
    with tempfile.TemporaryDirectory(prefix="teacup-recovery-") as temp:
        directory = Path(temp)
        project = directory / "Recovery.csproj"
        project.write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
            '<LangVersion>4</LangVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo>'
            '<EnableNETAnalyzers>false</EnableNETAnalyzers>'
            '</PropertyGroup></Project>', encoding="utf-8")
        (directory / "Program.cs").write_text(
            HARNESS.replace("__PRODUCTION__", "\n".join(members)), encoding="utf-8")
        subprocess.run(["dotnet", "run", "--project", str(project), "--verbosity", "quiet"],
                       check=True, env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1"))
    if args.skip_bundle:
        print("SKIP serialized bundle audit (--skip-bundle)", flush=True)
    else:
        audit_bundle(args.bundle.resolve())


if __name__ == "__main__":
    main()
