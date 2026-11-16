#!/usr/bin/env python3
"""Read-only backend regression checks. Run with Python 3 and dotnet SDK 8.

Compiles the unchanged PancakeFill and RecipeKnowledge against Unity doubles
(C#4/net8), using the real knowledge JSON and extracted production dependency
collector, recipe classifier and DTOs. Hook ordering is checked as source
contracts, not live Unity execution. Serialized array doubles deliberately
duplicate the last element on growth, as Unity does. No scenes are opened/saved.

--net35 additionally compiles the entire standalone LayoutEditor directory
against installed Unity 2017 assemblies and cached net35 reference assemblies.
All generated files/build products stay outside the repository.
"""
import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape


ROOT = Path(__file__).resolve().parents[2]
EDITOR = ROOT / "Assets/Editor/LayoutEditor"


def extract(source, declaration):
    matches = list(re.finditer(declaration, source))
    assert len(matches) == 1, "Expected one declaration: " + declaration
    match = matches[0]
    start = source.index("{", match.start())
    depth = 0
    tokens = r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/|[{}]'
    for token in re.finditer(tokens, source[start:]):
        if token.group() == "{":
            depth += 1
        elif token.group() == "}":
            depth -= 1
            if depth == 0:
                return source[match.start():start + token.end()]
    raise AssertionError("Unbalanced declaration: " + declaration)


DOUBLES = r'''
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using LevelEditorStub;

namespace UnityEngine
{
    public class Object { public string name; }
    public class ScriptableObject : Object { }
    public class Component : Object { public GameObject gameObject; public Transform transform { get { return gameObject.transform; } } }
    public class Transform { public GameObject gameObject; public bool ManagerEnvironment; }
    public class GameObject : Object
    {
        public bool Active = true;
        public Transform transform;
        public readonly List<GameObject> Children = new List<GameObject>();
        public readonly List<Component> Components = new List<Component>();
        public GameObject(string n) { name = n; transform = new Transform { gameObject = this }; }
        public T Add<T>(T value) where T : Component
        { value.gameObject = this; Components.Add(value); return value; }
        public T GetComponent<T>() where T : Component
        { foreach (var c in Components) if (c is T) return (T)c; return null; }
        public T[] GetComponentsInChildren<T>(bool inactive) where T : Component
        {
            var result = new List<T>();
            if (Active || inactive)
            {
                foreach (var c in Components) if (c is T) result.Add((T)c);
                foreach (var child in Children) result.AddRange(child.GetComponentsInChildren<T>(inactive));
            }
            return result.ToArray();
        }
    }
    public static class Application { public static string dataPath; }
    public static class Mathf { public static int Clamp(int n, int lo, int hi) { return Math.Min(hi, Math.Max(lo, n)); } }
    public static class JsonUtility
    {
        public static T FromJson<T>(string text)
        { return System.Text.Json.JsonSerializer.Deserialize<T>(text, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }); }
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
        public GameObject[] GetRootGameObjects()
        { if (!isLoaded) throw new Exception("Cannot traverse an unloading/restoring scene"); return Roots; }
    }
    public static class SceneManager
    {
        public static readonly List<Scene> Scenes = new List<Scene>();
        public static int sceneCount { get { return Scenes.Count; } }
        public static Scene GetSceneAt(int i) { return Scenes[i]; }
    }
}
namespace LevelEditorStub
{
    public class PseudoPrefabSO : ScriptableObject { public string prefabName, bundleName; }
    public class PseudoPrefabSORecipe : PseudoPrefabSO { }
    public class CustomRecipeSO : ScriptableObject
    { public ScriptableObject[] compositionSOs, optionalSOs; public PseudoPrefabSO cookingStepSO, platingStepSO; }
    public class LevelInfoSO : ScriptableObject
    {
        public ScriptableObject[] recipes, optionalRecipeMatchListItems;
        public PseudoPrefabSO[] allIngredients, allCookingSteps, includeRecipeMatchLists;
        public string[] dependencies;
    }
    public class PseudoPrefabManagerStub : Component { public LevelInfoSO levelInfo; }
    public class PseudoPrefabStub : Component { public PseudoPrefabSO pseudoPrefabSO; }
    public class PseudoPrefabCookingUtensilStub : PseudoPrefabStub
    {
        public ScriptableObject[] allowedIngredientSOs = new ScriptableObject[0];
        public GameObject[] models = new GameObject[0];
        public PseudoPrefabSO[] modelSOs = new PseudoPrefabSO[0];
        public int capacity;
    }
}
namespace UnityEditor
{
    public static class EditorApplication { public static bool isPlayingOrWillChangePlaymode; }
    public static class AssetDatabase
    {
        public static readonly Dictionary<string, UnityEngine.Object> Assets = new Dictionary<string, UnityEngine.Object>();
        public static T LoadAssetAtPath<T>(string path) where T : class
        { UnityEngine.Object o; return Assets.TryGetValue(path, out o) ? o as T : null; }
        public static string GetAssetPath(UnityEngine.Object value)
        { foreach (var pair in Assets) if (object.ReferenceEquals(pair.Value, value)) return pair.Key; return ""; }
    }
    public static class Undo
    { public static readonly List<object> Recorded = new List<object>(); public static void RecordObject(object o, string label) { Recorded.Add(o); } }
    public static class EditorUtility
    { public static readonly List<object> Dirty = new List<object>(); public static void SetDirty(object o) { Dirty.Add(o); } }
    public class SerializedObject
    {
        readonly object target;
        public SerializedObject(object o) { target = o; }
        public SerializedProperty FindProperty(string name) { return new SerializedProperty(target, target.GetType().GetField(name), -1); }
        public void ApplyModifiedProperties() { }
    }
    public class SerializedProperty
    {
        readonly object target;
        readonly FieldInfo field;
        readonly int index;
        public SerializedProperty(object o, FieldInfo f, int i)
        { if (f == null) throw new Exception("Missing serialized field"); target = o; field = f; index = i; }
        public int intValue { get { return (int)field.GetValue(target); } set { field.SetValue(target, value); } }
        public int arraySize
        {
            get { return ((Array)field.GetValue(target)).Length; }
            set
            {
                var old = (Array)field.GetValue(target);
                var next = Array.CreateInstance(field.FieldType.GetElementType(), value);
                Array.Copy(old, next, Math.Min(old.Length, value));
                for (int i = old.Length; i < value && old.Length > 0; i++) next.SetValue(old.GetValue(old.Length - 1), i);
                field.SetValue(target, next);
            }
        }
        public UnityEngine.Object objectReferenceValue
        {
            get { return (UnityEngine.Object)((Array)field.GetValue(target)).GetValue(index); }
            set { ((Array)field.GetValue(target)).SetValue(value, index); }
        }
        public SerializedProperty GetArrayElementAtIndex(int i) { return new SerializedProperty(target, field, i); }
        public void InsertArrayElementAtIndex(int i)
        {
            var old = (Array)field.GetValue(target);
            arraySize = old.Length + 1;
            var next = (Array)field.GetValue(target);
            Array.Copy(old, i, next, i + 1, old.Length - i);
        }
    }
}
namespace UnityEditor.SceneManagement
{
    public static class EditorSceneManager
    { public static readonly List<string> Dirty = new List<string>(); public static void MarkSceneDirty(Scene s) { Dirty.Add(s.path); } }
}
static class LayoutEditorLog
{ public static readonly List<string> Warnings = new List<string>(); public static void LogWarning(string s) { Warnings.Add(s); } }
static class LayoutEditorStubIO
{
    public static int Promotions;
    public static bool IsUnderManagerEnvironment(Transform t) { return t.ManagerEnvironment; }
    public static void ApplyStub(GameObject go, LayoutItemDto dto)
    {
        if (dto.stubKind != "CookingUtensil") throw new Exception("Unexpected promotion");
        var old = go.GetComponent<PseudoPrefabStub>(); go.Components.Remove(old);
        go.Add(new PseudoPrefabCookingUtensilStub { pseudoPrefabSO = old.pseudoPrefabSO, capacity = dto.cookingUtensil.capacity });
        Promotions++;
    }
}
class LayoutItemDto { public string stubKind; public LayoutCookingUtensilStubDto cookingUtensil; }
class LayoutCookingUtensilStubDto { public int capacity; }
static class LayoutEditorOptionalRecipe
{
    public static bool IsAssembly(CustomRecipeSO so) { throw new Exception("Unexpected custom traversal"); }
    public static void CollectAssemblyIngredientRefs(CustomRecipeSO so, List<ScriptableObject> refs) { throw new Exception("Unexpected assembly"); }
}
static class LayoutEditorOptionalBundlePreflight
{ public static bool NeedsDedicatedBundle(string set, string kind) { throw new Exception("Unexpected set scan"); } }
static class LayoutEditorSaladSauceFactory
{
    public static void SyncOptionals(LevelInfoSO info) { }
    public static void AddDependencies(LevelInfoSO info, List<string> deps) { }
}
static class LayoutEditorCatalogApi
{
    public static readonly HashSet<string> Bundles = new HashSet<string>();
    public static bool BundleFileExists(string name) { return Bundles.Contains(name); }
    public static string IngredientCatalogId(PseudoPrefabSO so, string id) { return id; }
__TYPE__
}
static class LayoutEditorCustomIngredients
{
    static readonly HashSet<string> _pendingDocBundles = new HashSet<string>();
    static bool ReferencesCommonW2(LevelInfoSO i) { return false; }
    static bool ReferencesCommonW3(LevelInfoSO i) { return false; }
    static bool ReferencesCommonW5(LevelInfoSO i) { return false; }
    static bool IsCommon03Asset(string path) { return false; }
    static void AddWebAssetBundleDep(List<string> deps, string sub, string id) { throw new Exception("Unexpected common03 dependency"); }
__DEPENDENCIES__
}
'''

TESTS = r'''
class Tests
{
    static int checks, cases;
    static PseudoPrefabSO pan;
    static PseudoPrefabSORecipe batter;
    static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    static T Register<T>(string id, T value) where T : UnityEngine.Object
    { AssetDatabase.Assets["Assets/recipes/" + AssetDatabase.Assets.Count + "/" + id + ".asset"] = value; return value; }
    static PseudoPrefabSORecipe Recipe(string id)
    { return Register(id, new PseudoPrefabSORecipe { prefabName = id }); }
    static LevelInfoSO Info(params ScriptableObject[] extra)
    {
        var recipes = new List<ScriptableObject> { Recipe("Cake_Plain_SO") }; recipes.AddRange(extra);
        return new LevelInfoSO { recipes = recipes.ToArray(), dependencies = new[] { "keep" } };
    }
    static Scene Scene(LevelInfoSO info, params GameObject[] pans)
    {
        var manager = new GameObject("manager"); manager.Add(new PseudoPrefabManagerStub { levelInfo = info });
        var root = new GameObject("root"); root.Children.AddRange(pans);
        return new Scene { path = "fixture-" + cases + "-" + SceneManager.sceneCount, isLoaded = true, Roots = new[] { manager, root } };
    }
    static PseudoPrefabCookingUtensilStub Pan(params ScriptableObject[] inputs)
    { return new GameObject("pan").Add(new PseudoPrefabCookingUtensilStub { pseudoPrefabSO = pan, allowedIngredientSOs = inputs, capacity = 1 }); }
    static void Fresh()
    {
        AssetDatabase.Assets.Clear(); SceneManager.Scenes.Clear(); EditorUtility.Dirty.Clear(); Undo.Recorded.Clear();
        UnityEditor.SceneManagement.EditorSceneManager.Dirty.Clear(); LayoutEditorLog.Warnings.Clear();
        LayoutEditorCatalogApi.Bundles.Clear(); LayoutEditorStubIO.Promotions = 0; EditorApplication.isPlayingOrWillChangePlaymode = false;
        pan = new PseudoPrefabSO(); batter = new PseudoPrefabSORecipe();
        AssetDatabase.Assets["Assets/common01/pseudo_prefab_so/utensils/FryPanSO.asset"] = pan;
        AssetDatabase.Assets[LayoutEditorPancakeFill.BatterAssetPath] = batter;
    }
    static void Case(string name, Action action)
    { cases++; Fresh(); action(); Console.WriteLine("PASS " + name); }
    static void NoPanWrites()
    { Check(Undo.Recorded.Count == 0 && LayoutEditorStubIO.Promotions == 0 && UnityEditor.SceneManagement.EditorSceneManager.Dirty.Count == 0, "unexpected pan/scene mutation"); }
    static void Main(string[] args)
    {
        Application.dataPath = args[0] + "/Assets";
        Check(LayoutEditorRecipeKnowledge.KnowledgeFileLoaded, "real knowledge JSON must load, not silently fall back");
        Case("strict plain recipe detection", delegate {
            Check(!LayoutEditorPancakeFill.HasPlainPancake(null), "null info");
            Check(!LayoutEditorPancakeFill.HasPlainPancake(new LevelInfoSO()), "null recipes");
            foreach (var r in new ScriptableObject[] { null, Register("Cake_Plain_SO", new PseudoPrefabSO()),
                Register("Cake_Plain_SO", new CustomRecipeSO()), Recipe("Cake_Chocolate_SO"), Recipe("Cake_StrawberryPancake") })
                Check(!LayoutEditorPancakeFill.HasPlainPancake(new LevelInfoSO { recipes = new[] { r } }), "false positive plain recipe");
            Check(LayoutEditorPancakeFill.HasPlainPancake(Info()), "plain enabled");
        });
        Case("scene identity, inactive descendants, manager rig and prefab isolation", delegate {
            var info = Info(); var otherInfo = Info();
            LayoutEditorCatalogApi.Bundles.Add("commonW5"); LayoutEditorCatalogApi.Bundles.Add("bundle18");
            var inactive = Pan(); inactive.gameObject.Active = false;
            var rig = Pan(); rig.transform.ManagerEnvironment = true;
            var wrong = Pan(); wrong.pseudoPrefabSO = new PseudoPrefabSO();
            var asset = Pan(); var other = Pan(); var orphan = Pan(); var unloaded = Pan();
            var scene = Scene(info, inactive.gameObject, rig.gameObject, wrong.gameObject);
            SceneManager.Scenes.Add(scene); SceneManager.Scenes.Add(Scene(otherInfo, other.gameObject));
            var noManager = Scene(info, orphan.gameObject); noManager.Roots = new[] { orphan.gameObject }; SceneManager.Scenes.Add(noManager);
            var notLoaded = Scene(info, unloaded.gameObject); notLoaded.isLoaded = false; SceneManager.Scenes.Add(notLoaded);
            LayoutEditorPancakeFill.SyncLoadedScenes(info);
            Check(inactive.allowedIngredientSOs.Length == 1 && inactive.allowedIngredientSOs[0] == batter, "inactive target missed");
            foreach (var p in new[] { rig, wrong, asset, other, orphan, unloaded }) Check(p.allowedIngredientSOs.Length == 0, "isolation breach");
            Check(Undo.Recorded.Count == 1 && UnityEditor.SceneManagement.EditorSceneManager.Dirty.Count == 1, "only target dirty");
            Check(otherInfo.dependencies.Length == 1, "unrelated info changed");
            int undo = Undo.Recorded.Count;
            Check(LayoutEditorPancakeFill.SyncForScene(scene, info) == 0 && Undo.Recorded.Count == undo, "repeat not idempotent");
        });
        Case("invalid/unloaded/play/no-plain guards are read-only", delegate {
            var p = Pan(); var info = Info(); var scene = Scene(info, p.gameObject);
            Check(LayoutEditorPancakeFill.SyncForScene(new Scene(), info) == 0, "invalid scene");
            var unloaded = scene; unloaded.isLoaded = false; Check(LayoutEditorPancakeFill.SyncForScene(unloaded, info) == 0, "unloaded scene");
            EditorApplication.isPlayingOrWillChangePlaymode = true;
            Check(LayoutEditorPancakeFill.SyncForScene(scene, info) == 0, "play transition");
            EditorApplication.isPlayingOrWillChangePlaymode = false; info.recipes = new ScriptableObject[] { Recipe("Cake_Chocolate_SO") };
            Check(LayoutEditorPancakeFill.SyncForScene(scene, info) == 0 && p.allowedIngredientSOs.Length == 0, "no plain"); NoPanWrites();
        });
        Case("safe plain-only promotion and capacity repair", delegate {
            var info = Info(); var go = new GameObject("basic pan"); go.Add(new PseudoPrefabStub { pseudoPrefabSO = pan });
            var p = Pan(); p.capacity = 0; var scene = Scene(info, go, p.gameObject);
            Check(LayoutEditorPancakeFill.SyncForScene(scene, info) == 2, "promotion/repair count");
            var promoted = go.GetComponent<PseudoPrefabCookingUtensilStub>();
            Check(LayoutEditorStubIO.Promotions == 1 && promoted.capacity == 1 && p.capacity == 1, "promotion/capacity");
            Check(promoted.allowedIngredientSOs[0] == batter && promoted.models[0] == null && promoted.modelSOs[0] == null, "promoted batter/models");
            Check(LayoutEditorPancakeFill.SyncForScene(scene, info) == 0, "promotion repeat");
        });
        foreach (var id in new[] { "Salad_Plain_SO", "Sushi_PlainFish_SO", "Fry_Chips_SO", "Cake_Honey_SO" })
            Case("safe non-pan selection: " + id, delegate {
                var p = Pan(); var info = Info(Recipe(id));
                Check(LayoutEditorPancakeFill.SyncForScene(Scene(info, p.gameObject), info) == 1 && p.allowedIngredientSOs[0] == batter, "safe mixed selection must fill");
            });
        foreach (var id in new[] { "Burger_Plain_SO", "Pasta_MeatOnly_SO", "Pasta_TomatoOnly_SO", "Cake_Chocolate_SO", "Cake_StrawberryPancake", "UnknownPanRecipe", "md_burger_fries" })
            foreach (var reverse in new[] { false, true })
                Case("empty native pan safety: " + id + "/reverse=" + reverse, delegate {
                    var info = Info(Recipe(id)); if (reverse) Array.Reverse(info.recipes);
                    var p = Pan(); p.models = new[] { new GameObject("keep trailing model") }; p.modelSOs = new[] { new PseudoPrefabSO() };
                    var inputs = p.allowedIngredientSOs; var models = p.models; var sos = p.modelSOs;
                    var go = new GameObject("base pan"); go.Add(new PseudoPrefabStub { pseudoPrefabSO = pan });
                    var scene = Scene(info, p.gameObject, go);
                    Check(LayoutEditorPancakeFill.SyncForScene(scene, info) == 0, "mixed empty pan narrowed");
                    Check(object.ReferenceEquals(inputs, p.allowedIngredientSOs) && object.ReferenceEquals(models, p.models)
                        && object.ReferenceEquals(sos, p.modelSOs) && p.capacity == 1, "preserved arrays/capacity changed");
                    Check(go.GetComponent<PseudoPrefabCookingUtensilStub>() == null, "unsafe base pan promoted");
                    Check(LayoutEditorLog.Warnings.Count == 1 && LayoutEditorLog.Warnings[0].Contains(id)
                        && LayoutEditorLog.Warnings[0].Contains(scene.path) && LayoutEditorLog.Warnings[0].Contains("2 empty"), "missing actionable aggregate reason");
                    NoPanWrites(); Check(LayoutEditorPancakeFill.SyncForScene(scene, info) == 0, "preserve repeat"); NoPanWrites();
                });
        foreach (var kind in new[] { "custom", "decorative", "null" })
            Case("unverified selection preserves native lookup: " + kind, delegate {
                ScriptableObject r = kind == "custom" ? (ScriptableObject)Register("CustomDish", new CustomRecipeSO())
                    : kind == "decorative" ? Register("Decorative", new PseudoPrefabSO()) : null;
                var p = Pan(); var info = Info(r);
                Check(LayoutEditorPancakeFill.SyncForScene(Scene(info, p.gameObject), info) == 0 && p.allowedIngredientSOs.Length == 0, "unverified selection narrowed");
                Check(LayoutEditorLog.Warnings.Count == 1, "unverified selection must report"); NoPanWrites();
            });
        Case("known original aliases resolve without false unknown guard", delegate {
            var salad = Recipe("RenamedSalad"); salad.prefabName = "Salad_Plain";
            var p = Pan(); var info = Info(salad);
            Check(LayoutEditorPancakeFill.SyncForScene(Scene(info, p.gameObject), info) == 1, "known non-pan alias");
        });
        Case("canonical alias classification retains indirect pan steps", delegate {
            // Hotdog's final step is Pot; its onion needs FryingPan. The file name
            // deliberately provides no type hint, so the resolved key must be used.
            var hotdog = Recipe("RenamedDish"); hotdog.prefabName = "hotdog_onions";
            string step; string[] ingredients;
            Check(LayoutEditorRecipeKnowledge.TryGetOriginal("hotdog_onions", out step, out ingredients), "fixture must be known");
            Check(step != "FryingPan", "fixture must exercise indirect group");
            var p = Pan(); var info = Info(hotdog);
            Check(LayoutEditorPancakeFill.SyncForScene(Scene(info, p.gameObject), info) == 0, "renamed indirect pan dish narrowed"); NoPanWrites();
        });
        Case("empty and fully configured mixed pans coexist; complete explicit fill remains unchanged", delegate {
            var meat = new PseudoPrefabSO(); var chocolate = new PseudoPrefabSORecipe();
            var empty = Pan(); var configured = Pan(meat, chocolate); var complete = Pan(meat, chocolate, batter);
            var completeInputs = complete.allowedIngredientSOs; var completeModels = complete.models;
            var info = Info(Recipe("Burger_Plain_SO"), Recipe("Pasta_MeatOnly_SO"), Recipe("Cake_Chocolate_SO"));
            var scene = Scene(info, empty.gameObject, configured.gameObject, complete.gameObject);
            Check(LayoutEditorPancakeFill.SyncForScene(scene, info) == 1 && empty.allowedIngredientSOs.Length == 0
                && configured.allowedIngredientSOs.Length == 3 && configured.allowedIngredientSOs[2] == batter, "per-pan mixed handling");
            Check(object.ReferenceEquals(completeInputs, complete.allowedIngredientSOs) && object.ReferenceEquals(completeModels, complete.models)
                && Undo.Recorded.Count == 1, "complete explicit auto-fill touched");
            Check(LayoutEditorPancakeFill.SyncForScene(scene, info) == 0 && Undo.Recorded.Count == 1, "mixed repeat");
        });
        foreach (var count in new[] { 0, 1, 2, 4 })
            Case("configured mixed pan preserves alignment; model slots=" + count, delegate {
                var a = new PseudoPrefabSO(); var b = new PseudoPrefabSORecipe(); var p = Pan(a, b); p.capacity = 7;
                p.models = new GameObject[count]; p.modelSOs = new PseudoPrefabSO[count];
                for (int i = 0; i < count; i++) { p.models[i] = new GameObject("model" + i); p.modelSOs[i] = new PseudoPrefabSO(); }
                var models = (GameObject[])p.models.Clone(); var sos = (PseudoPrefabSO[])p.modelSOs.Clone();
                var info = Info(Recipe("Burger_Plain_SO"), Recipe("Cake_Chocolate_SO")); var scene = Scene(info, p.gameObject);
                Check(LayoutEditorPancakeFill.SyncForScene(scene, info) == 1, "configured pan not supplemented");
                Check(p.allowedIngredientSOs.Length == 3 && p.allowedIngredientSOs[0] == a && p.allowedIngredientSOs[1] == b
                    && p.allowedIngredientSOs[2] == batter && p.capacity == 7, "configured order/capacity");
                Check(p.models[2] == null && p.modelSOs[2] == null && p.models.Length == Math.Max(3, count + (count > 2 ? 1 : 0)), "new slots must be null");
                for (int i = 0; i < count; i++) Check(p.models[i < 2 ? i : i + 1] == models[i]
                    && p.modelSOs[i < 2 ? i : i + 1] == sos[i], "old model alignment lost");
                var inputs = p.allowedIngredientSOs; var after = p.models; int undo = Undo.Recorded.Count;
                Check(LayoutEditorPancakeFill.SyncForScene(scene, info) == 0 && Undo.Recorded.Count == undo
                    && object.ReferenceEquals(inputs, p.allowedIngredientSOs) && object.ReferenceEquals(after, p.models), "configured repeat changed arrays");
            });
        Case("existing batter is neither duplicated nor reordered; repair only invalid capacity", delegate {
            var p = Pan(new PseudoPrefabSO(), batter); p.capacity = -1; var model = new GameObject("held"); p.models = new[] { model };
            var info = Info(); var scene = Scene(info, p.gameObject); var inputs = p.allowedIngredientSOs; var models = p.models;
            Check(LayoutEditorPancakeFill.SyncForScene(scene, info) == 1 && p.capacity == 1 && p.allowedIngredientSOs.Length == 2
                && object.ReferenceEquals(inputs, p.allowedIngredientSOs) && object.ReferenceEquals(models, p.models), "existing batter modified");
            Check(LayoutEditorPancakeFill.SyncForScene(scene, info) == 0, "capacity repeat");
        });
        foreach (var missing in new[] { "batter", "pan" })
            Case("missing asset is safe: " + missing, delegate {
                var p = Pan(); var info = Info(); var scene = Scene(info, p.gameObject);
                AssetDatabase.Assets.Remove(missing == "batter" ? LayoutEditorPancakeFill.BatterAssetPath : "Assets/common01/pseudo_prefab_so/utensils/FryPanSO.asset");
                Check(LayoutEditorPancakeFill.SyncForScene(scene, info) == 0 && p.allowedIngredientSOs.Length == 0, "missing asset changed pan"); NoPanWrites();
            });
        foreach (var mask in new[] { 0, 1, 2, 3 })
            Case("production gated dependencies mask=" + mask, delegate {
                if ((mask & 1) != 0) LayoutEditorCatalogApi.Bundles.Add("commonW5");
                if ((mask & 2) != 0) LayoutEditorCatalogApi.Bundles.Add("bundle18");
                var p = Pan(); var info = Info(); info.dependencies = new[] { "keep", "keep", "other/custom_recipes" };
                var scene = Scene(info, p.gameObject); LayoutEditorPancakeFill.SyncForScene(scene, info);
                Check(string.Join(",", info.dependencies).StartsWith("keep,keep,other/custom_recipes", StringComparison.Ordinal), "dependencies replaced");
                Check((Array.IndexOf(info.dependencies, "commonW5") >= 0) == ((mask & 1) != 0)
                    && (Array.IndexOf(info.dependencies, "bundle18") >= 0) == ((mask & 2) != 0), "missing bundle registered or existing bundle omitted");
                var before = info.dependencies; int dirty = EditorUtility.Dirty.Count;
                LayoutEditorPancakeFill.SyncForScene(scene, info);
                Check(object.ReferenceEquals(before, info.dependencies) && dirty == EditorUtility.Dirty.Count, "dependency repeat writes");
            });
        Case("null dependencies repaired; mixed-preserved pan still registers available dependencies", delegate {
            LayoutEditorCatalogApi.Bundles.Add("commonW5"); LayoutEditorCatalogApi.Bundles.Add("bundle18");
            var p = Pan(); var info = Info(Recipe("Burger_Plain_SO")); info.dependencies = null;
            Check(LayoutEditorPancakeFill.SyncForScene(Scene(info, p.gameObject), info) == 0, "mixed pan changed");
            Check(string.Join(",", info.dependencies) == "commonW5,bundle18", "null deps not repaired"); NoPanWrites();
        });
        foreach (var id in new[] { "plain-only", "Burger_Plain_SO", "Pasta_MeatOnly_SO" })
            Case("missing knowledge JSON uses conservative production fallback: " + id, delegate {
                string dataPath = Application.dataPath;
                try
                {
                    Application.dataPath = Path.Combine(Path.GetTempPath(), "absent-pancake-knowledge/Assets");
                    LayoutEditorRecipeKnowledge.InvalidateCache();
                    Check(!LayoutEditorRecipeKnowledge.KnowledgeFileLoaded, "fallback fixture found knowledge unexpectedly");
                    var info = id == "plain-only" ? Info() : Info(Recipe(id)); var p = Pan();
                    int changed = LayoutEditorPancakeFill.SyncForScene(Scene(info, p.gameObject), info);
                    Check(changed == (id == "plain-only" ? 1 : 0), "fallback mixed-pan safety");
                }
                finally { Application.dataPath = dataPath; LayoutEditorRecipeKnowledge.InvalidateCache(); }
            });
        Console.WriteLine("RESULT: " + cases + " scenarios, " + checks + " assertions; complete production helper + knowledge, extracted dependency collector, C#4/net8 doubles.");
    }
}
'''


def source_contracts():
    reload = (EDITOR / "LayoutEditorPseudoReload.cs").read_text(encoding="utf-8-sig")
    exporter = (EDITOR / "LayoutEditorSetExporter.cs").read_text(encoding="utf-8-sig")
    call = "LayoutEditorPancakeFill.SyncForScene("

    def before(text, first, second, label):
        assert text.index(first) < text.index(second), label
        print("PASS source contract: " + label, flush=True)

    for name, next_call in (("ReloadPseudoAssets", '"ReloadSceneLayoutPrefabs"'),
                            ("ReloadPseudoAssetsFull", "SafeReinit(manager)")):
        method = extract(reload, r"    public static void " + name + r"\(")
        before(method, call, next_call, name + " sync precedes reload/native reinit")
    safe = extract(reload, r"    internal static void SafeInit\(")
    before(safe, call, "string[] persistentDependencies", "SafeInit sync precedes dependency snapshot")
    before(safe, call, "LayoutEditorDispenserIconFix.PreloadAndSeed", "SafeInit sync precedes preload")
    before(safe, call, "manager.Init();", "SafeInit sync precedes native Init")
    guard = (EDITOR / "LayoutEditorDependencyRepair.cs").read_text(encoding="utf-8-sig")
    assert 'typeof(PseudoPrefabManager).GetMethod("Init")' in guard
    assert "LayoutEditorPseudoReload.SafeInit(__instance)" in extract(guard, r"    private static bool InitPrefix\(")
    print("PASS source contract: edit-mode Init Harmony prefix routes OnEnable/Start to SafeInit", flush=True)
    settle = extract(exporter, r"    private static void SettleCurrentScene\(")
    before(settle, call, "ForcePrepareForExportStrict", "export sync precedes DeInit/prepare")
    before(settle, call, "SaveScenePrepared", "export sync precedes scene save")
    finish = extract(exporter, r"    private static void FinishPreparedExport\(")
    before(finish, "AssetDatabase.SaveAssets();", "BuildListedBundles(", "export saves dependency assets before build")
    catalog = (EDITOR / "LayoutEditorCatalogApi.cs").read_text(encoding="utf-8-sig")
    call_index = catalog.index("LayoutEditorPancakeFill.SyncLoadedScenes(info)")
    assert call_index < catalog.index("AssetDatabase.SaveAssets();", call_index)
    applier = (EDITOR / "SceneLayoutApplier.cs").read_text(encoding="utf-8-sig")
    before(applier, call, "LayoutEditorPseudoReload.SaveScenePrepared(scene", "layout apply sync precedes save")
    native = (ROOT / "Assets/Scripts/LevelEditor/PseudoPrefabCookingUtensil.cs").read_text(encoding="utf-8-sig")
    assert "if (!cookingUtensilStub.allowedIngredientSOs.IsEmpty())" in native
    assert "cookableContainer.m_approvedContentsList = newLookup;" in native
    print("PASS source contract: nonempty allowedIngredientSOs replaces native lookup", flush=True)


def net35(directory, env, unity):
    framework = Path(os.environ.get("NUGET_PACKAGES", str(Path.home() / ".nuget/packages")))
    candidates = sorted(framework.glob("microsoft.netframework.referenceassemblies.net35/*/build/.NETFramework/v3.5"))
    assert candidates, "Cached Microsoft.NETFramework.ReferenceAssemblies.net35 required for --net35"
    refs = list((unity / "UnityEngine").glob("*.dll")) + [unity / "UnityEditor.dll"]
    refs += [ROOT / "Library/ScriptAssemblies" / name for name in
             ("Assembly-CSharp.dll", "Assembly-CSharp-firstpass.dll", "LevelEditorStub.dll")]
    refs += [ROOT / "Assets/Plugins/0Harmony.dll"]
    assert all(p.is_file() for p in refs) and len(refs) > 6, "Missing Unity/host DLLs"
    path = directory / "net35"
    path.mkdir()
    (path / "Standalone.csproj").write_text(
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net35</TargetFramework>'
        '<LangVersion>4</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
        '<AutomaticallyUseReferenceAssemblyPackages>false</AutomaticallyUseReferenceAssemblyPackages>'
        '<FrameworkPathOverride>' + escape(str(candidates[-1])) + '</FrameworkPathOverride>'
        '<GenerateAssemblyInfo>false</GenerateAssemblyInfo><NuGetAudit>false</NuGetAudit>'
        '</PropertyGroup><ItemGroup><Compile Include="' + escape(str(EDITOR / "**/*.cs")) + '" />'
        + ''.join('<Reference Include="' + escape(str(p)) + '" />' for p in refs)
        + '</ItemGroup></Project>', encoding="utf-8")
    result = subprocess.run(["dotnet", "build", str(path / "Standalone.csproj"), "--verbosity", "quiet"],
                            cwd=directory, env=env, capture_output=True, text=True, timeout=120)
    if result.returncode:
        print(result.stdout + result.stderr, flush=True)
        result.check_returncode()
    warnings = {line.split(" [", 1)[0] for line in result.stdout.splitlines() if ": warning " in line}
    print("Standalone build: 0 errors, " + str(len(warnings)) + " distinct warnings", flush=True)
    print("PASS entire standalone LayoutEditor: C#4/net35, installed Unity/host DLLs", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--net35", action="store_true")
    parser.add_argument("--unity-managed", type=Path,
                        default=Path("/Applications/Unity/Hub/Editor/2017.4.8f1/Unity.app/Contents/Managed"))
    args = parser.parse_args()
    assert shutil.which("dotnet"), "dotnet SDK 8 required"
    source_contracts()
    catalog = (EDITOR / "LayoutEditorCatalogApi.cs").read_text(encoding="utf-8-sig")
    ingredients = (EDITOR / "LayoutEditorCustomIngredients.cs").read_text(encoding="utf-8-sig")
    models = (EDITOR / "LayoutEditorModels.cs").read_text(encoding="utf-8-sig")
    dependencies = re.findall(r'    public const string (?:CustomContentDirName|Common\w+BundleName) = "[^"]+";', ingredients)
    assert len(dependencies) == 9
    dependencies += [extract(ingredients, r"    public static void EnsureWebDependencies\(string levelSet, LevelInfoSO info\)"),
                     extract(ingredients, r"    public static void EnsureWebDependencies\(string levelSet, LevelInfoSO info, bool replaceExisting\)")]
    dependencies += [extract(ingredients, r"    private static [^\n]+\b" + name + r"\(")
                     for name in ("AddDependency", "ReadBundleNameField")]
    doubles = DOUBLES.replace("__TYPE__", extract(catalog, r"    internal static string RecipeTypeOf\("))
    doubles = doubles.replace("__DEPENDENCIES__", "\n".join(dependencies))
    dtos = "\n".join(extract(models, r"public class " + name + r"\b")
                     for name in ("RecipeEntryDto", "RecipeCookingGroupDto", "RecipeIngredientStepDto"))
    temp_parent = Path(tempfile.gettempdir()).resolve()
    assert temp_parent != ROOT and ROOT not in temp_parent.parents, "Temp directory must be outside repo"
    with tempfile.TemporaryDirectory(prefix="plain-pancake-backend-", dir=temp_parent) as temp:
        directory = Path(temp)
        (directory / "NuGet.Config").write_text('<configuration><packageSources><clear /></packageSources></configuration>', encoding="utf-8")
        (directory / "Program.cs").write_text(doubles + dtos + TESTS, encoding="utf-8")
        for name in ("LayoutEditorPancakeFill.cs", "LayoutEditorRecipeKnowledge.cs"):
            (directory / name).write_text((EDITOR / name).read_text(encoding="utf-8-sig"), encoding="utf-8")
        (directory / "Checks.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion>'
            '<GenerateAssemblyInfo>false</GenerateAssemblyInfo><EnableNETAnalyzers>false</EnableNETAnalyzers>'
            '<NuGetAudit>false</NuGetAudit></PropertyGroup></Project>', encoding="utf-8")
        env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1",
                   DOTNET_NOLOGO="1", DOTNET_CLI_HOME=str(directory / "sdk-home"), TMPDIR=str(directory))
        subprocess.run(["dotnet", "run", "--project", str(directory / "Checks.csproj"), "--", str(ROOT)],
                       cwd=directory, env=env, check=True, timeout=90)
        if args.net35:
            net35(directory, env, args.unity_managed)
        else:
            print("SKIP net35 standalone build (enable with --net35)", flush=True)
    print("LIMIT: Unity API doubles + source ordering, no Unity serialization/player or scene mutation. "
          "First OnEnable may run before scene.isLoaded; helper safely defers that case.", flush=True)


if __name__ == "__main__":
    main()
