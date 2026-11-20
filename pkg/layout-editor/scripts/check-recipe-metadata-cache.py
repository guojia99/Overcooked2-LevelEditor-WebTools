#!/usr/bin/env python3
"""Extract production metadata collectors; compile C#4/net35 and run API doubles.

No Unity lifecycle calls, user scenes, or repository build outputs. net8 executes
the same source that is separately compiled against net35 reference assemblies.
"""
import os
from pathlib import Path
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
EDITOR = ROOT / "Assets/Editor/LayoutEditor"
TOKENS = r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/|[{};]'


def extract(source, name, field=False):
    pattern = r"    (?:private|public|internal) static [^\n]+\b" + name
    pattern += r"\s*(?:=|;)" if field else r"\("
    matches = list(re.finditer(pattern, source))
    assert len(matches) == 1, name
    start = matches[0].start()
    depth = 0
    for token in re.finditer(TOKENS, source[start:]):
        value = token.group()
        if value == "{":
            depth += 1
        elif value == "}":
            depth -= 1
            if depth == 0 and not field:
                return source[start:start + token.end()]
        elif value == ";" and depth == 0 and field:
            return source[start:start + token.end()]
    raise AssertionError(name)


HARNESS = r'''
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;
using LevelEditorStub;
namespace UnityEngine {
    public class Object {
        public string name, Path;
        public static object[] Dispensers = new object[0];
        public static T[] FindObjectsOfType<T>() { var list=new List<T>(); foreach(var o in Dispensers) if(o is T)list.Add((T)o);return list.ToArray(); }
    }
    public class ScriptableObject:Object { }
    public class GameObject:Object { }
}
namespace LevelEditorStub {
    public class PseudoPrefabSO:ScriptableObject { public string prefabName,bundleName,assetPath;public UnityEngine.Object asset; }
    public class PseudoPrefabSORecipe:PseudoPrefabSO { }
    public class CustomRecipeSO:ScriptableObject { public PseudoPrefabSO cookingStepSO;public ScriptableObject[] compositionSOs,optionalSOs; }
    public class CustomRecipeOptionalBurgerSO:CustomRecipeSO { public ScriptableObject bunSO; }
    public class CustomRecipeOptionalSandwichSO:CustomRecipeSO { public ScriptableObject toastSO;public ScriptableObject[] sauceSOs; }
    public class PseudoPrefabDispenserStub:UnityEngine.Object { public PseudoPrefabSO spawnerItemPrefabSO; }
    public class LevelInfoSO:ScriptableObject {
        public enum LevelType { Campaign,Dynamic,Horde }
        public LevelType levelType;public ScriptableObject[] recipes,ModeRecipes;public PseudoPrefabSO[] allCookingSteps,allIngredients;
    }
}
public class OrderDefinitionNode:ScriptableObject { }
public class CompositeOrderNode:OrderDefinitionNode { public OrderDefinitionNode[] m_composition; }
public class CookedCompositeOrderNode:CompositeOrderNode { public CookingStepData m_cookingStep; }
public class CookingStepData:ScriptableObject { public int m_uID; }
namespace UnityEditor {
    public static class AssetDatabase {
        public static Dictionary<string,PseudoPrefabSO> Assets=new Dictionary<string,PseudoPrefabSO>();
        public static string GetAssetPath(UnityEngine.Object o) { return o.Path ?? ""; }
        public static bool IsValidFolder(string folder) { return true; }
        public static string[] FindAssets(string filter,string[] folders) {
            var paths=new List<string>();foreach(var p in Assets.Keys)if(p.StartsWith(folders[0]+"/",StringComparison.Ordinal))paths.Add(p);return paths.ToArray();
        }
        public static string GUIDToAssetPath(string guid) { return guid; }
        public static T LoadAssetAtPath<T>(string path) where T:UnityEngine.Object { PseudoPrefabSO o;return Assets.TryGetValue(path,out o)?o as T:null; }
    }
    public static class Undo { public static void RecordObject(object o,string label) { } }
    public static class EditorUtility { public static void SetDirty(object o) { } }
}
namespace LevelEditor {
    public class PseudoPrefabManager {
        public static PseudoPrefabManager Instance;public bool prepareForBuilding,Manifest;
    }
    public static class RecipeHelper {
        public static int StepCalls,OrderCalls;public static bool ThrowStep,ThrowOrder;
        public static PseudoPrefabSO Missing;
        public static Dictionary<PseudoPrefabSO,CookingStepData> Data=new Dictionary<PseudoPrefabSO,CookingStepData>();
        public static OrderDefinitionNode Order;
        static void Ready() { if(!LayoutEditorCatalogApi.CacheReady())throw new Exception("Forbidden unready host getter"); }
        public static CookingStepData GetCookingStepData(PseudoPrefabSO so) {
            StepCalls++;Ready();if(ThrowStep)throw new Exception("step failure");if(so==Missing)return null;return Data[so];
        }
        public static OrderDefinitionNode GetOrderDefinitionNode(ScriptableObject so) {
            OrderCalls++;Ready();if(ThrowOrder)throw new Exception("order failure");return Order;
        }
    }
}
static class LayoutEditorAssetBundleLifetime {
    internal static bool HasManifest(LevelEditor.PseudoPrefabManager manager) { return manager.Manifest; }
}
static class LayoutEditorLog { public static void Log(string message) { } }
static class LayoutEditorLevelModeStore {
    public static int Calls;
    public static ScriptableObject[] CollectAllRecipes(LevelInfoSO info) {
        Calls++;return (info.levelType==LevelInfoSO.LevelType.Campaign?info.recipes:info.ModeRecipes)??new ScriptableObject[0];
    }
}
static class LayoutEditorOptionalRecipe { public static bool IsAssembly(CustomRecipeSO recipe) { return false; } }
static class LayoutEditorRoastTrayFill {
    public static Dictionary<string,PseudoPrefabSO> Ingredients=new Dictionary<string,PseudoPrefabSO>();
    public static PseudoPrefabSO LoadIngredientSo(string id) { PseudoPrefabSO so;return Ingredients.TryGetValue(id,out so)?so:null; }
}
static class LayoutEditorRecipeKnowledge {
    public static Dictionary<string,string> Steps=new Dictionary<string,string>();
    public static Dictionary<string,string[]> Ingredients=new Dictionary<string,string[]>();
    public static bool TryGetOriginal(string id,out string step,out string[] ingredients) {
        step=null;ingredients=null;if(id==null||!Steps.TryGetValue(id,out step))return false;Ingredients.TryGetValue(id,out ingredients);return true;
    }
}
public static class LayoutEditorCatalogApi {
__CATALOG__
    public static PseudoPrefabSO ByName(string step) { return FindCookingStepWrapperByName(step); }
    public static PseudoPrefabSO ByUid(int uid) { return FindCookingStepWrapperByUid(uid); }
    public static bool Enriched { get { return s_cookingStepWrappersEnriched; } }
    public static void Reset() { s_cookingStepWrappersByName=null;s_cookingStepWrappersByUid=null;s_cookingStepWrappersEnriched=false; }
}
public static class LayoutEditorAllIngredientsFill {
__INGREDIENTS__
}
static class Checks {
    static int count;
    static void Check(bool ok,string label) { count++;if(!ok)throw new Exception(label); }
    static bool Has(PseudoPrefabSO[] values,PseudoPrefabSO so) { return Array.IndexOf(values,so)>=0; }
    static void Known(string id,string step,params string[] ingredients) { LayoutEditorRecipeKnowledge.Steps[id]=step;LayoutEditorRecipeKnowledge.Ingredients[id]=ingredients; }
    static int Main() {
        var manager=new LevelEditor.PseudoPrefabManager { prepareForBuilding=true };
        LevelEditor.PseudoPrefabManager.Instance=manager;
        var wraps=new List<PseudoPrefabSO>();
        for(int i=0;i<33;i++) {
            var so=new PseudoPrefabSO { name="Step"+i,prefabName="alias"+i,Path="Assets/common01/food/CookingSteps/Step"+i+".asset" };
            wraps.Add(so);AssetDatabase.Assets[so.Path]=so;LevelEditor.RecipeHelper.Data[so]=new CookingStepData { m_uID=100+i };
        }
        Check(!LayoutEditorCatalogApi.CacheReady(),"prepared manager empty cache not ready");
        Check(LayoutEditorCatalogApi.ByName("Step0")==wraps[0],"file name resolved during Prepare");
        Check(LayoutEditorCatalogApi.ByName("ALIAS0")==wraps[0],"prefab alias preserves case insensitive lookup");
        Check(LayoutEditorCatalogApi.ByUid(100)==null&&!LayoutEditorCatalogApi.Enriched,"Prepare UID pending");
        Check(LevelEditor.RecipeHelper.StepCalls==0,"all 33 wrappers: zero getters in Prepare");
        manager.Manifest=true;
        Check(LayoutEditorCatalogApi.ByUid(100)==null,"Prepare blocks even with manifest/cache loaded");
        Check(LevelEditor.RecipeHelper.StepCalls==0,"loaded Prepare still zero getters");
        manager.Manifest=false;manager.prepareForBuilding=false;
        Check(LayoutEditorCatalogApi.ByUid(100)==null,"missing manifest pending");
        var old=new PseudoPrefabSO { name="authored",assetPath="legacy-path" };
        var ingredient=new PseudoPrefabSO { name="fresh",prefabName="fresh",asset=new GameObject() };
        var official=new PseudoPrefabSORecipe { Path="Assets/unknown.asset",prefabName="unknown" };
        var custom=new CustomRecipeSO { cookingStepSO=wraps[1],compositionSOs=new ScriptableObject[] { ingredient,official } };
        var info=new LevelInfoSO { recipes=new ScriptableObject[] { custom },allCookingSteps=new[] { old },allIngredients=new[] { old } };
        LayoutEditorCatalogApi.SyncCookingStepsFromSelectedRecipes(info);
        Check(Has(info.allCookingSteps,old)&&Has(info.allCookingSteps,wraps[1]),"unknown recipe preserves authored steps and adds SO steps");
        LayoutEditorAllIngredientsFill.AutoFillIngredients(info);
        Check(Has(info.allIngredients,old)&&Has(info.allIngredients,ingredient),"unknown nested recipe retains authored ingredients and data result");
        LayoutEditorAllIngredientsFill.AutoFillIngredientsFromSelectedRecipes(info);
        Check(Has(info.allIngredients,old)&&Has(info.allIngredients,ingredient),"selected recipe incomplete preserves old");
        Check(LevelEditor.RecipeHelper.StepCalls==0&&LevelEditor.RecipeHelper.OrderCalls==0,"missing manifest all collectors: zero host getters");
        var sceneIngredient=new PseudoPrefabSO { prefabName="scene",assetPath="assets/scene.prefab" };
        UnityEngine.Object.Dispensers=new object[] { new PseudoPrefabDispenserStub { spawnerItemPrefabSO=sceneIngredient } };
        foreach(bool manifest in new[] { false,true }) {
            manager.prepareForBuilding=true;manager.Manifest=manifest;
            LayoutEditorCatalogApi.SyncCookingStepsFromSelectedRecipes(info);
            LayoutEditorAllIngredientsFill.AutoFillIngredients(info);
            Check(Has(info.allCookingSteps,old)&&Has(info.allCookingSteps,wraps[1]),"Prepare retains old/new steps, manifest="+manifest);
            Check(Has(info.allIngredients,old)&&Has(info.allIngredients,ingredient)&&Has(info.allIngredients,sceneIngredient),"Prepare retains scene/recipe/author ingredients, manifest="+manifest);
            Check(LevelEditor.RecipeHelper.StepCalls==0&&LevelEditor.RecipeHelper.OrderCalls==0,"Prepare complete collectors zero getters, manifest="+manifest);
        }
        manager.prepareForBuilding=false;manager.Manifest=false;UnityEngine.Object.Dispensers=new object[0];
        info.recipes=new ScriptableObject[] { official,custom };
        LayoutEditorRecipeKnowledge.Steps["unknown"]="UnavailableStep";
        LayoutEditorRecipeKnowledge.Ingredients["unknown"]=new string[0];
        LayoutEditorCatalogApi.SyncCookingStepsFromSelectedRecipes(info);
        Check(Has(info.allCookingSteps,old)&&Has(info.allCookingSteps,wraps[1]),"missing named wrapper preserves old and still visits later recipe");
        Check(LevelEditor.RecipeHelper.OrderCalls==0,"missing named wrapper does not fall through to unready getter");
        info.recipes=new ScriptableObject[] { custom };
        Known("unknown","Step2","fresh");LayoutEditorRoastTrayFill.Ingredients["fresh"]=ingredient;
        LayoutEditorCatalogApi.SyncCookingStepsFromSelectedRecipes(info);
        Check(info.allCookingSteps.Length==2&&Has(info.allCookingSteps,wraps[2]),"knowledge completes steps without runtime");
        LayoutEditorAllIngredientsFill.AutoFillIngredients(info);
        Check(info.allIngredients.Length==1&&info.allIngredients[0]==ingredient,"knowledge completes ingredients without runtime");
        Known("unknown","Step2","missing");info.allIngredients=new[] { old };
        LayoutEditorAllIngredientsFill.AutoFillIngredientsFromSelectedRecipes(info);
        Check(Has(info.allIngredients,old)&&Has(info.allIngredients,ingredient),"missing ingredient SO keeps old and collected leaves");
        manager.Manifest=true;
        Check(LayoutEditorCatalogApi.ByName("Step0")==wraps[0]&&LevelEditor.RecipeHelper.StepCalls==0,"ready name lookup still does not enrich");
        LevelEditor.RecipeHelper.Missing=wraps[32];
        Check(LayoutEditorCatalogApi.ByUid(100)==null&&!LayoutEditorCatalogApi.Enriched,"partial UID mapping not published or sealed");
        Check(LevelEditor.RecipeHelper.StepCalls==33,"one lookup per unique wrapper, aliases deduplicated");
        LevelEditor.RecipeHelper.Missing=null;
        Check(LayoutEditorCatalogApi.ByUid(100)==wraps[0]&&LayoutEditorCatalogApi.Enriched,"pending UID retries successfully");
        Check(LevelEditor.RecipeHelper.StepCalls==66,"retry reads actual getters");
        for(int i=0;i<33;i++)Check(LayoutEditorCatalogApi.ByUid(100+i)==wraps[i],"ready UID unchanged "+i);
        Check(LevelEditor.RecipeHelper.StepCalls==66,"complete UID mapping cached");
        LayoutEditorCatalogApi.Reset();LevelEditor.RecipeHelper.ThrowStep=true;
        Check(LayoutEditorCatalogApi.ByUid(100)==null&&!LayoutEditorCatalogApi.Enriched,"throwing lookup stays retryable");
        LevelEditor.RecipeHelper.ThrowStep=false;
        Check(LayoutEditorCatalogApi.ByUid(100)==wraps[0],"retry after exception");
        official.prefabName="runtime";official.Path="Assets/runtime.asset";info.recipes=new ScriptableObject[] { official };
        LevelEditor.RecipeHelper.Order=new CookedCompositeOrderNode { m_cookingStep=new CookingStepData { m_uID=104 } };
        LayoutEditorCatalogApi.SyncCookingStepsFromSelectedRecipes(info);
        Check(info.allCookingSteps.Length==1&&info.allCookingSteps[0]==wraps[4],"ready runtime recipe maps exact UID");
        LevelEditor.RecipeHelper.Order=null;info.allCookingSteps=new[] { old };
        LayoutEditorCatalogApi.SyncCookingStepsFromSelectedRecipes(info);
        Check(Has(info.allCookingSteps,old),"null runtime result preserves author data");
        LevelEditor.RecipeHelper.ThrowOrder=true;
        LayoutEditorCatalogApi.SyncCookingStepsFromSelectedRecipes(info);
        Check(Has(info.allCookingSteps,old),"throwing runtime result preserves author data");
        LevelEditor.RecipeHelper.ThrowOrder=false;
        LevelEditor.RecipeHelper.Order=new CompositeOrderNode { m_composition=new OrderDefinitionNode[] {
            new CookedCompositeOrderNode { m_cookingStep=null },
            new CookedCompositeOrderNode { m_cookingStep=new CookingStepData { m_uID=105 } } } };
        LayoutEditorCatalogApi.SyncCookingStepsFromSelectedRecipes(info);
        Check(Has(info.allCookingSteps,old)&&Has(info.allCookingSteps,wraps[5]),"partial runtime tree preserves old and collects later valid branch");
        Known("runtime","",new string[0]);int orderCalls=LevelEditor.RecipeHelper.OrderCalls;
        LayoutEditorCatalogApi.SyncCookingStepsFromSelectedRecipes(info);
        Check(info.allCookingSteps.Length==0&&LevelEditor.RecipeHelper.OrderCalls==orderCalls,"known no-step recipe skips getter and permits complete rebuild");
        LayoutEditorAllIngredientsFill.AutoFillIngredientsFromSelectedRecipes(info);
        Check(info.allIngredients.Length==0,"known empty ingredients permits complete rebuild");
        LayoutEditorCatalogApi.Reset();AssetDatabase.Assets.Clear();
        Check(LayoutEditorCatalogApi.ByUid(100)==null&&!LayoutEditorCatalogApi.Enriched,"empty local scan not sealed");
        AssetDatabase.Assets[wraps[0].Path]=wraps[0];
        Check(LayoutEditorCatalogApi.ByUid(100)==wraps[0],"empty local scan retries after assets appear");
        manager.prepareForBuilding=true;manager.Manifest=false;
        var modeRecipe=new CustomRecipeSO { compositionSOs=new ScriptableObject[] { ingredient } };
        var unknownModeRecipe=new PseudoPrefabSORecipe { Path="Assets/unknown_mode.asset",prefabName="unknown_mode" };
        foreach(var mode in new[] { LevelInfoSO.LevelType.Dynamic,LevelInfoSO.LevelType.Horde })
        foreach(bool empty in new[] { false,true }) {
            var modeInfo=new LevelInfoSO { levelType=mode,recipes=empty?new ScriptableObject[0]:null,
                ModeRecipes=new ScriptableObject[] { modeRecipe },allIngredients=new[] { ingredient } };
            var originalRecipes=modeInfo.recipes;
            int calls=LayoutEditorLevelModeStore.Calls,steps=LevelEditor.RecipeHelper.StepCalls,orders=LevelEditor.RecipeHelper.OrderCalls;
            LayoutEditorAllIngredientsFill.AutoFillIngredients(modeInfo);
            Check(modeInfo.allIngredients.Length==1&&modeInfo.allIngredients[0]==ingredient,mode+" writeback keeps aggregated ingredients with empty Campaign recipes="+empty);
            Check(LayoutEditorLevelModeStore.Calls==calls+1&&ReferenceEquals(modeInfo.recipes,originalRecipes),mode+" writeback reads active menu without rewriting recipes");
            modeInfo.recipes=new ScriptableObject[0];
            calls=LayoutEditorLevelModeStore.Calls;
            LayoutEditorAllIngredientsFill.AutoFillIngredientsFromSelectedRecipes(modeInfo);
            Check(modeInfo.allIngredients.Length==1&&modeInfo.allIngredients[0]==ingredient&&LayoutEditorLevelModeStore.Calls==calls+1,mode+" selected ingredients read active menu with empty Campaign recipes");
            modeInfo.recipes=originalRecipes;modeInfo.ModeRecipes=new ScriptableObject[] { modeRecipe,unknownModeRecipe };modeInfo.allIngredients=new[] { old };
            LayoutEditorAllIngredientsFill.AutoFillIngredients(modeInfo);
            Check(Has(modeInfo.allIngredients,old)&&Has(modeInfo.allIngredients,ingredient),mode+" incomplete mode metadata retains authored and collected ingredients");
            Check(LevelEditor.RecipeHelper.StepCalls==steps&&LevelEditor.RecipeHelper.OrderCalls==orders,mode+" ingredient writeback uses zero host getters during Prepare");
        }
        LevelEditor.PseudoPrefabManager.Instance=null;
        Check(!LayoutEditorCatalogApi.CacheReady(),"null manager not ready");
        Console.WriteLine("PASS: "+count+" production metadata checks (C#4; no Unity scenes).");return 0;
    }
}
'''


def main():
    catalog = (EDITOR / "LayoutEditorCatalogApi.cs").read_text(encoding="utf-8")
    ingredients = (EDITOR / "LayoutEditorAllIngredientsFill.cs").read_text(encoding="utf-8")
    methods = ("SyncCookingStepsFromSelectedRecipes", "CollectCookingSteps",
               "CollectOfficialRecipeCookingSteps", "CollectCookingStepsFromOrder", "CacheReady",
               "EnsureCookingStepWrappers", "FindCookingStepWrapperByName", "FindCookingStepWrapperByUid")
    fields = ("s_cookingStepWrappersByName", "s_cookingStepWrappersByUid",
              "s_cookingStepWrappersEnriched", "CookingStepFolders")
    members = [extract(catalog, name) for name in methods]
    members += [extract(catalog, name, True) for name in fields]
    ingredient_methods = ("IsMachineOnlyIngredient", "AutoFillIngredients",
                          "AutoFillIngredientsFromSelectedRecipes", "PreserveIncompleteIngredients",
                          "CollectOfficialRecipeIngredients", "CollectCustomRecipeIngredients")
    ingredient_members = [extract(ingredients, name) for name in ingredient_methods]
    ingredient_members.append(extract(ingredients, "MachineOnlyIngredientIds", True))
    program = HARNESS.replace("__CATALOG__", "\n".join(members)).replace(
        "__INGREDIENTS__", "\n".join(ingredient_members))
    env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1")
    with tempfile.TemporaryDirectory(prefix="recipe-metadata-cache-") as temp:
        directory = Path(temp)
        (directory / "Program.cs").write_text(program, encoding="utf-8")
        for framework in ("net35", "net8.0"):
            project = directory / "Checks.csproj"
            package = ('<ItemGroup><PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies.net35" '
                       'Version="1.0.3" /></ItemGroup>') if framework == "net35" else ""
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                               '<TargetFramework>' + framework + '</TargetFramework><LangVersion>4</LangVersion>'
                               '<GenerateAssemblyInfo>false</GenerateAssemblyInfo><EnableNETAnalyzers>false</EnableNETAnalyzers>'
                               '<AutomaticallyUseReferenceAssemblyPackages>false</AutomaticallyUseReferenceAssemblyPackages>'
                               '</PropertyGroup>' + package + '</Project>', encoding="utf-8")
            command = ["dotnet", "build" if framework == "net35" else "run", "--project", str(project)]
            if framework == "net35":
                command = ["dotnet", "build", str(project), "--verbosity", "quiet"]
            subprocess.run(command, env=env, timeout=120, check=True)
            print("PASS: " + framework + " C#4 " + ("compile" if framework == "net35" else "execution"), flush=True)


if __name__ == "__main__":
    main()
