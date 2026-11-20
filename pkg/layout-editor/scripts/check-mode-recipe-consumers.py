#!/usr/bin/env python3
"""Execute native menu, ingredient/step, dependency, Optional and utensil consumers.

C#4 doubles exercise extracted production methods; source contracts check save wiring.
No Unity scene writes.
"""
import os
from pathlib import Path
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
EDITOR = ROOT / 'Assets/Editor/LayoutEditor'


def extract(source, name, parameters=''):
    matches = list(re.finditer(r'    (?:public|private|internal) static [^\n]+\b' + name + r'\(' + parameters, source))
    assert len(matches) == 1, name
    start = source.index('{', matches[0].start())
    depth = 0
    for token in re.finditer(r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/|[{}]', source[start:]):
        if token.group() == '{':
            depth += 1
        elif token.group() == '}':
            depth -= 1
            if depth == 0:
                return source[matches[0].start():start + token.end()]
    raise AssertionError(name)


HARNESS = r'''
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;
namespace UnityEngine {
    public class Object {
        public string path;
        public static int Destroyed;
        public static T Instantiate<T>(T original) where T:Object { return (T)original.MemberwiseClone(); }
        public static void DestroyImmediate(Object o) { Destroyed++; }
    }
    public class ScriptableObject : Object { }
    public class Component : Object { public GameObject gameObject; }
    public class GameObject : Object {
        public Scene scene;
        internal PseudoPrefabManagerStub[] Managers=new PseudoPrefabManagerStub[0];
        public T[] GetComponentsInChildren<T>(bool inactive) { return (T[])(object)Managers; }
    }
    public class Scene {
        public bool valid, isLoaded;
        public GameObject[] roots;
        public bool IsValid() { return valid; }
        public GameObject[] GetRootGameObjects() { return roots; }
    }
}
class PseudoPrefabManagerStub : Component { public LevelInfoSO levelInfo; }
class PseudoPrefabSO : ScriptableObject { public string bundleName,assetPath,prefabName; public UnityEngine.Object asset; }
class PseudoPrefabSORecipe : PseudoPrefabSO { }
class CustomRecipeSO : ScriptableObject {
    public PseudoPrefabSO cookingStepSO,platingStepSO,modelSO;
    public ScriptableObject[] compositionSOs, optionalSOs;
    public string recipeName; public int score;
    public enum RecipeType { Composite, Cooked, Mixed }
    public RecipeType type;
}
class CustomRecipeOptionalBurgerSO : CustomRecipeSO { public ScriptableObject bunSO; }
class CustomRecipeOptionalSandwichSO : CustomRecipeSO { public ScriptableObject toastSO; public ScriptableObject[] sauceSOs; }
static class LayoutEditorOptionalRecipe {
    public static bool IsAssembly(CustomRecipeSO r) { return r is CustomRecipeOptionalBurgerSO; }
    public static void CollectAssemblySoRefs(CustomRecipeSO r,List<ScriptableObject> refs) {
        var burger=r as CustomRecipeOptionalBurgerSO;if(burger!=null&&burger.bunSO!=null)refs.Add(burger.bunSO);
    }
}
static class LayoutEditorMatchlistMap {
    private static readonly Regex DlcPathRegex = new Regex(@"^assets[\\/]downloadablecontent[\\/](dlc\d+)[\\/]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
__MATCHLIST__
}
static class LayoutEditorRecipeKnowledge {
    public static readonly Dictionary<string,string> Steps=new Dictionary<string,string>();
    public static readonly Dictionary<string,string[]> Ingredients=new Dictionary<string,string[]>();
    public static bool TryGetOriginal(string id,out string step,out string[] ingredients) {
        step=null;ingredients=null;if(id==null||!Steps.TryGetValue(id,out step))return false;
        return Ingredients.TryGetValue(id,out ingredients);
    }
    public static string CustomCookingStep(CustomRecipeSO r) { return r.cookingStepSO==null?"":r.cookingStepSO.name; }
}
static class LayoutEditorAllIngredientsFill {
    static readonly string[] MachineOnlyIngredientIds=new[]{"ketchup"};
__INGREDIENTS__
}
static class LayoutEditorRoastTrayFill {
    public static readonly Dictionary<string,PseudoPrefabSO> Ingredients=new Dictionary<string,PseudoPrefabSO>();
    public static PseudoPrefabSO LoadIngredientSo(string id) { PseudoPrefabSO so;return Ingredients.TryGetValue(id,out so)?so:null; }
__ROAST__
}
static class LayoutEditorHotPotFill {
    static List<PseudoPrefabSO> FindHotPotPermutationSOs(HashSet<string> bundles) { return new List<PseudoPrefabSO>(); }
__HOTPOT__
}
static class LayoutEditorSteamerFill {
    public static List<ScriptableObject> SteamerInputs(LevelInfoSO info) { return CollectSteamerInputs(info); }
    public static List<ScriptableObject> MixerInputs(LevelInfoSO info) { return CollectMixerInputs(info); }
__STEAMER__
}
static class LayoutEditorPancakeFill {
__PANCAKE__
}
static class LayoutEditorCatalogApi {
    public static readonly HashSet<string> Existing=new HashSet<string>();
    public static bool BundleFileExists(string name) { return Existing.Contains(name); }
}
static class LayoutEditorNativeDependencies {
    public static bool IsNativeBundle(string name) { return name!=null&&System.Text.RegularExpressions.Regex.IsMatch(name,@"\Abundle[0-9]+\z"); }
    public static string[] Filter(List<string> values) {
        var result=new List<string>();foreach(var value in values)if(IsNativeBundle(value)&&!result.Contains(value))result.Add(value);
        return result.ToArray();
    }
}
static class LayoutEditorSaladSauceFactory {
    public static LevelInfoSO Passed;
    public static void SyncOptionals(LevelInfoSO info) { Passed=info; }
    public static void AddDependencies(LevelInfoSO info,List<string> deps) { }
}
static class NativeDependencies {
    const string Common03BundleName="common03",CommonW1BundleName="commonW1",CommonW2BundleName="commonW2",
        CommonW3BundleName="commonW3",CommonW4BundleName="commonW4",CommonW5BundleName="commonW5";
    static HashSet<string> _pendingDocBundles=new HashSet<string>();
    static bool ReferencesCommonW2(LevelInfoSO info) { return false; }
    static bool ReferencesCommonW3(LevelInfoSO info) { return false; }
    static bool ReferencesCommonW5(LevelInfoSO info) { return false; }
    static bool IsCommon03Asset(string path) { return false; }
    static void AddWebAssetBundleDep(List<string> deps,string sub,string id) { }
__NATIVE_DEPS__
}
static class SpecialOptionals {
    public static List<ScriptableObject> Burgers;
    static string SyncBurgerOptionalsForSavedRecipes(string path,string set,List<ScriptableObject> recipes,LevelInfoSO info) {
        Burgers=recipes;return "burger note";
    }
__SPECIAL__
}
class Menu : ScriptableObject { public ScriptableObject[] recipes; }
class Wave : ScriptableObject { public Menu recipeListSO; public PseudoPrefabSO[] enemies; }
class Horde : ScriptableObject { public Wave[] hordeWaveDataSOs; }
class LevelConfigSetupPerPlayerCountSO : ScriptableObject {
    public ScriptableObject[] scriptedRecipes; public Horde hordeLevelConfig;
}
class LevelInfoSO : ScriptableObject {
    public enum LevelType { Campaign, Dynamic, Horde }
    public LevelType levelType;
    public ScriptableObject[] recipes, optionalRecipeMatchListItems;
    public Menu[] recipeListSOs;
    public LevelConfigSetupPerPlayerCountSO config_1p,config_2p,config_3p,config_4p;
    public PseudoPrefabSO[] allIngredients,allCookingSteps,includeRecipeMatchLists;
    public string[] dependencies;
    public string levelName;
}
class CustomRecipeCategoryDeleteDto { public string setName, category; }
class CustomRecipeConfigSO : ScriptableObject {
    public struct CustomRecipeCategoryEntry { public string id; }
    public CustomRecipeCategoryEntry[] categories;
}
static class AssetDatabase {
    public static readonly Dictionary<string,UnityEngine.Object> Assets=new Dictionary<string,UnityEngine.Object>();
    public static int Deleted;
    public static readonly Dictionary<string,string[]> Dependencies=new Dictionary<string,string[]>();
    public static readonly Dictionary<string,string> Bundles=new Dictionary<string,string>();
    public static string GetImplicitAssetBundleName(string path) { string value;return Bundles.TryGetValue(path,out value)?value:null; }
    public static string GetAssetPath(UnityEngine.Object o) { return o == null ? "" : o.path ?? ""; }
    public static string[] FindAssets(string filter,string[] roots) {
        var result=new List<string>();
        foreach(var pair in Assets)if(pair.Value is LevelInfoSO&&pair.Key.StartsWith(roots[0]+"/"))result.Add(pair.Key);
        return result.ToArray();
    }
    public static string GUIDToAssetPath(string guid) { return guid; }
    public static string[] GetDependencies(string[] paths,bool recursive) {
        var result=new List<string>(paths);var pending=new Queue<string>(paths);
        while(pending.Count>0) {
            string[] children;if(!Dependencies.TryGetValue(pending.Dequeue(),out children))continue;
            foreach(var path in children)if(!result.Contains(path)) { result.Add(path);pending.Enqueue(path); }
        }
        return result.ToArray();
    }
    public static T LoadAssetAtPath<T>(string path) where T:UnityEngine.Object {
        UnityEngine.Object value;return Assets.TryGetValue(path,out value)?value as T:null;
    }
    public static bool DeleteAsset(string path) { Deleted++;return true; }
    public static void SaveAssets() { }
    public static void Refresh() { }
}
static class Undo { public static void RecordObject(object o,string name) { } }
static class EditorUtility { public static void SetDirty(object o) { } }
static class LayoutEditorLog { public static void Log(string s) { } }
static class LayoutEditorLevelModeStore {
__STORE__
}
static class DependencyConsumer {
__DEPENDENCY_WRAPPER__
    public static LevelInfoSO Passed;
    public static bool Fail;
    private static void EnsureWebDependenciesCore(string set,LevelInfoSO info,bool replace) {
        Passed=info;
        if(Fail)throw new Exception("fixture core failure");
        info.dependencies=new[]{"mode-bundle"};
        info.optionalRecipeMatchListItems=new ScriptableObject[]{info};
    }
}
class Checks {
    const string LevelSetsRoot="Assets/LevelSets", CustomRecipesDir="custom_recipes";
    static bool AssetFolderExists(string path) { return true; }
    static string DirectoryName(string path) { return Path.GetDirectoryName(path); }
    // Ordinary SO leaves are complete; an unknown official recipe is unresolved.
    static bool CollectOfficialRecipeCookingSteps(ScriptableObject so, HashSet<PseudoPrefabSO> steps)
    { return !(so is PseudoPrefabSORecipe); }
__CONSUMERS__
    static int count;
    static void Check(bool ok,string label) { count++;if(!ok)throw new Exception(label); }
    static bool Has(UnityEngine.Object[] refs,UnityEngine.Object item) { return Array.IndexOf(refs,item)>=0; }
    static void Main() {
        var stale=new CustomRecipeSO();
        var step1=new PseudoPrefabSO { path="step1" };var step2=new PseudoPrefabSO { path="step2" };
        var a=new CustomRecipeSO { cookingStepSO=step1 };
        var b=new CustomRecipeOptionalBurgerSO { bunSO=new CustomRecipeSO { cookingStepSO=step2 } };
        a.compositionSOs=new ScriptableObject[]{a,b}; // Cycle and nested bun traversal.
        var menu1=new Menu {recipes=new ScriptableObject[]{null,a}};
        var menu2=new Menu {recipes=new ScriptableObject[]{a,b}};
        var enemy=new PseudoPrefabSO { path="enemy",bundleName="enemy-bundle" };
        var wave1=new Wave {recipeListSO=menu1,enemies=new[]{enemy,enemy}};
        var wave2=new Wave {recipeListSO=menu2,enemies=new[]{enemy}};
        var player=new LevelConfigSetupPerPlayerCountSO {scriptedRecipes=new ScriptableObject[]{b},
            hordeLevelConfig=new Horde {hordeWaveDataSOs=new[]{wave1,wave2}}};
        var info=new LevelInfoSO {levelType=LevelInfoSO.LevelType.Dynamic,recipes=new ScriptableObject[]{stale},
            recipeListSOs=new[]{menu1,null,menu2},config_4p=player};
        var original=info.recipes;
        var recipes=LayoutEditorLevelModeStore.CollectAllRecipes(info);
        Check(recipes.Length==2 && Has(recipes,a) && Has(recipes,b) && !Has(recipes,stale),"Dynamic aggregates all menus, deduplicates, ignores stale Campaign recipes");
        Check(LayoutEditorLevelModeStore.CollectHordeEnemies(info).Length==0,"Dynamic ignores inactive Horde enemies");
        SyncCookingStepsFromSelectedRecipes(info);
        Check(info.allCookingSteps.Length==2,"Dynamic nested/cyclic recipe steps and bun collected");
        var roots=CollectUsageRoots(info);
        Check(Has(roots,menu1)&&Has(roots,menu2)&&Has(roots,a)&&Has(roots,b),"Dynamic roots include menus and recipes");
        Check(!Has(roots,enemy)&&!Has(roots,stale),"Dynamic roots exclude inactive recipes/enemies");
        Check(object.ReferenceEquals(info.recipes,original),"Dynamic consumers preserve recipes array");
        int destroyed=UnityEngine.Object.Destroyed;
        DependencyConsumer.EnsureWebDependencies("fixture",info,false);
        Check(DependencyConsumer.Passed!=info&&DependencyConsumer.Passed.recipes.Length==2,
            "legacy dependency helpers see aggregate menu on disposable Info");
        Check(info.dependencies[0]=="mode-bundle"&&info.optionalRecipeMatchListItems==DependencyConsumer.Passed.optionalRecipeMatchListItems,
            "dependency projection copies only derived outputs back");
        Check(UnityEngine.Object.Destroyed==destroyed+1&&object.ReferenceEquals(info.recipes,original),
            "dependency projection destroyed and real recipes preserved");
        DependencyConsumer.Fail=true;
        try { DependencyConsumer.EnsureWebDependencies("fixture",info,true);throw new Exception("expected core error"); }
        catch(Exception ex) { Check(ex.Message=="fixture core failure","dependency failure propagates"); }
        Check(UnityEngine.Object.Destroyed==destroyed+2&&object.ReferenceEquals(info.recipes,original),
            "failed dependency projection also destroyed without menu mutation");
        DependencyConsumer.Fail=false;
        info.levelType=LevelInfoSO.LevelType.Horde;
        info.config_1p=new LevelConfigSetupPerPlayerCountSO(); // A later player must still be scanned.
        recipes=LayoutEditorLevelModeStore.CollectAllRecipes(info);
        Check(recipes.Length==2&&Has(recipes,a)&&Has(recipes,b),"Horde aggregates every 4P wave menu");
        var enemies=LayoutEditorLevelModeStore.CollectHordeEnemies(info);
        Check(enemies.Length==1&&enemies[0]==enemy,"Horde enemies deduplicate");
        Check(wave1.enemies.Length==2,"repeated wave enemy spawns stay intact");
        info.allCookingSteps=null;SyncCookingStepsFromSelectedRecipes(info);
        Check(info.allCookingSteps.Length==2,"Horde cooking steps aggregate waves");
        roots=CollectUsageRoots(info);
        Check(Has(roots,player)&&Has(roots,player.hordeLevelConfig)&&Has(roots,wave1)&&Has(roots,wave2)
            &&Has(roots,menu2)&&Has(roots,enemy),"Horde roots retain native graph and enemy");
        Check(!Has(roots,stale)&&object.ReferenceEquals(info.recipes,original),"Horde ignores stale Campaign recipes without rewriting");
        info.levelType=LevelInfoSO.LevelType.Campaign;
        info.optionalRecipeMatchListItems=null;
        info.allCookingSteps=new[]{step1};SyncCookingStepsFromSelectedRecipes(info);
        Check(info.allCookingSteps.Length==1&&info.allCookingSteps[0]==step2,"Campaign cooking steps include 4P scripted recipe bun");
        roots=CollectUsageRoots(info);
        Check(Has(roots,stale)&&Has(roots,b)&&!Has(roots,a)&&!Has(roots,enemy)&&!Has(roots,menu1),
            "Campaign usage roots include scripted but exclude inactive phase and Horde menus");
        destroyed=UnityEngine.Object.Destroyed;
        DependencyConsumer.EnsureWebDependencies("fixture",info,false);
        Check(DependencyConsumer.Passed!=info&&Has(DependencyConsumer.Passed.recipes,b)&&UnityEngine.Object.Destroyed==destroyed+1,
            "Campaign legacy dependency helpers see scripted orders on disposable projection");
        Check(ReferenceEquals(info.recipes,original),"Campaign dependency projection preserves ordinary orders");
        var scripted=new[]{a,b,a};
        info.config_1p=new LevelConfigSetupPerPlayerCountSO {scriptedRecipes=scripted};
        info.config_2p=new LevelConfigSetupPerPlayerCountSO {scriptedRecipes=new[]{b}};
        info.config_3p=new LevelConfigSetupPerPlayerCountSO {scriptedRecipes=new[]{a}};
        Check(LayoutEditorLevelModeStore.CollectAllRecipes(info).Length==3,"Campaign unions ordinary and all four scripted menus");
        Check(ReferenceEquals(info.config_1p.scriptedRecipes,scripted)&&scripted.Length==3&&scripted[0]==scripted[2],
            "Campaign scripted order, repetition and array identity preserved");
        var category="Assets/LevelSets/fixture/custom_recipes/protected";
        a.path=category+"/nested.asset";b.path="Assets/LevelSets/fixture/custom_recipes/other/parent.asset";
        b.compositionSOs=new ScriptableObject[]{a};
        var dto=new CustomRecipeCategoryDeleteDto {setName="fixture",category="protected"};
        foreach(var mode in new[]{LevelInfoSO.LevelType.Campaign,LevelInfoSO.LevelType.Dynamic,LevelInfoSO.LevelType.Horde}) {
            info.levelType=mode;info.path="Assets/LevelSets/fixture/data/level/info.asset";info.levelName="fixture-level";
            info.recipes=new ScriptableObject[0];info.allCookingSteps=null;info.optionalRecipeMatchListItems=null;
            info.config_1p=null;info.config_2p=null;info.config_3p=null;
            menu1.recipes=new ScriptableObject[]{b,b};menu2.recipes=new ScriptableObject[0];
            AssetDatabase.Assets[info.path]=info;
            int deleted=AssetDatabase.Deleted;
            Check(DeleteCustomRecipeCategory(dto)!=null&&AssetDatabase.Deleted==deleted,mode+" nested menu recipe blocks category deletion");
            Check(menu1.recipes.Length==2&&menu1.recipes[0]==menu1.recipes[1],mode+" deletion scan preserves duplicate menu entries");
        }
        AssetDatabase.Assets.Clear();
        Check(DeleteCustomRecipeCategory(dto)==null&&AssetDatabase.Deleted==1,"unused category can be deleted");
        TestConsumers();
        foreach(var mode in new[]{LevelInfoSO.LevelType.Campaign,LevelInfoSO.LevelType.Dynamic,LevelInfoSO.LevelType.Horde}) {
            var authored=new PseudoPrefabSO { path="authored" };
            var partial=new CustomRecipeSO { cookingStepSO=step2,
                compositionSOs=new ScriptableObject[]{new PseudoPrefabSORecipe(),a} };
            var selected=new ScriptableObject[]{partial};
            var partialMenu=new Menu { recipes=selected };
            var partialInfo=new LevelInfoSO { levelType=mode,recipes=selected,recipeListSOs=new[]{partialMenu},
                config_4p=new LevelConfigSetupPerPlayerCountSO { hordeLevelConfig=new Horde {
                    hordeWaveDataSOs=new[]{new Wave { recipeListSO=partialMenu }} } },allCookingSteps=new[]{authored} };
            SyncCookingStepsFromSelectedRecipes(partialInfo);
            Check(partialInfo.allCookingSteps.Length==3&&Has(partialInfo.allCookingSteps,authored)
                &&Has(partialInfo.allCookingSteps,step1)&&Has(partialInfo.allCookingSteps,step2),
                mode+" partial traversal retains authored steps and visits later nested recipes");
            partial.compositionSOs=new ScriptableObject[]{new PseudoPrefabSO()};
            SyncCookingStepsFromSelectedRecipes(partialInfo);
            Check(partialInfo.allCookingSteps.Length==1&&partialInfo.allCookingSteps[0]==step2,
                mode+" complete traversal treats ordinary SO as leaf and removes stale steps");
        }
        Check(CollectUsageRoots(null).Length==0&&LayoutEditorLevelModeStore.CollectAllRecipes(null).Length==0,"null Info safe");
        Console.WriteLine("PASS: "+count+" production menu/ingredient/step/matchlist/dependency/Optional/utensil/deletion checks (C#4).");
    }

    static void TestConsumers() {
        var milkWater=new CustomRecipeSO {recipeName="Web_MilkTea_HotWater"};
        var sushiCombo=new CustomRecipeSO {recipeName="Web_Sushi_OptionalCombo"};
        AssetDatabase.Assets["Assets/commonW5/custom_recipes/milk_tea/Web_MilkTea_HotWater.asset"]=milkWater;
        AssetDatabase.Assets["Assets/commonW5/custom_recipes/sushi/Web_Sushi_OptionalCombo.asset"]=sushiCombo;
        var leaf=new PseudoPrefabSO {name="FlourSO",asset=new GameObject(),path="Assets/leaf.asset",bundleName="bundle18"};
        var authored=new PseudoPrefabSO {name="authored",asset=new GameObject()};
        var mixed=new CustomRecipeSO {type=CustomRecipeSO.RecipeType.Mixed,compositionSOs=new ScriptableObject[]{leaf},score=0};
        var steamer=new CustomRecipeSO {cookingStepSO=new PseudoPrefabSO {name="Steamer",assetPath="Assets/DownloadableContent/DLC05/step.asset"},compositionSOs=new ScriptableObject[]{mixed}};
        var hot=new PseudoPrefabSORecipe {path="Assets/hot.asset",bundleName="bundle47"};
        var roast=new PseudoPrefabSORecipe {path="Assets/roast.asset"};
        var pancake=new PseudoPrefabSORecipe {path="Assets/Cake_Plain_SO.asset"};
        LayoutEditorRecipeKnowledge.Steps["hot"]="HotPot";LayoutEditorRecipeKnowledge.Ingredients["hot"]=new[]{"leaf"};
        LayoutEditorRecipeKnowledge.Steps["roast"]="RoastingTray";LayoutEditorRecipeKnowledge.Ingredients["roast"]=new[]{"leaf"};
        LayoutEditorRecipeKnowledge.Steps["Cake_Plain_SO"]="FryingPan";LayoutEditorRecipeKnowledge.Ingredients["Cake_Plain_SO"]=new[]{"leaf"};
        LayoutEditorRoastTrayFill.Ingredients["leaf"]=leaf;
        var milk=new CustomRecipeSO {recipeName="Web_MilkTea_Test",score=20};
        var sushi=new CustomRecipeSO {recipeName="Web_Sushi_Test",score=20};
        var unknown=new PseudoPrefabSORecipe {path="Assets/unknown.asset"};
        var enemy=new PseudoPrefabSO {path="Assets/enemy.asset",bundleName="bundle354"};
        var missing=new PseudoPrefabSO {path="Assets/missing.asset",bundleName="bundle999"};
        var shared=new PseudoPrefabSO {path="Assets/shared.asset",bundleName="commonW5"};
        foreach(var asset in new PseudoPrefabSO[]{leaf,enemy,missing,shared})AssetDatabase.Assets[asset.path]=asset;
        LayoutEditorCatalogApi.Existing.UnionWith(new[]{"bundle18","bundle47","bundle354","bundle600","commonW5"});
        AssetDatabase.Bundles[missing.path]="bundle600";
        var active=new ScriptableObject[]{hot,roast,pancake,steamer,milk,sushi,hot};
        var owner=new LevelInfoSO();var otherOwner=new LevelInfoSO();
        var scene=new Scene {valid=true,isLoaded=true};
        var root=new GameObject {scene=scene,Managers=new[]{new PseudoPrefabManagerStub {levelInfo=owner}}};
        scene.roots=new[]{root};var component=new Component {gameObject=root};
        Check(LayoutEditorLevelModeStore.OwnsRecipeConsumer(owner,component),"utensil mutation allowed in owning level scene");
        Check(!LayoutEditorLevelModeStore.OwnsRecipeConsumer(otherOwner,component),"utensil mutation excluded from another level scene");
        scene.isLoaded=false;
        Check(!LayoutEditorLevelModeStore.OwnsRecipeConsumer(owner,component),"unloading scene defers utensil mutation");
        Check(!LayoutEditorLevelModeStore.OwnsRecipeConsumer(null,component)&&!LayoutEditorLevelModeStore.OwnsRecipeConsumer(owner,null),"utensil ownership null safe");
        foreach(var mode in new[]{LevelInfoSO.LevelType.Campaign,LevelInfoSO.LevelType.Dynamic,LevelInfoSO.LevelType.Horde})
        for(int p=0;p<4;p++) {
            var menu=new Menu {recipes=active};
            var wave=new Wave {recipeListSO=menu,enemies=new[]{enemy,enemy}};
            var configs=new LevelConfigSetupPerPlayerCountSO[4];
            configs[p]=new LevelConfigSetupPerPlayerCountSO {scriptedRecipes=active,hordeLevelConfig=new Horde {hordeWaveDataSOs=new[]{wave}}};
            var inactive=new ScriptableObject[]{unknown};
            var level=new LevelInfoSO {path="Assets/LevelSets/fixture/data/level/info.asset",levelType=mode,recipes=inactive,
                recipeListSOs=mode==LevelInfoSO.LevelType.Dynamic?new[]{new Menu {recipes=inactive},menu}:null,
                config_1p=configs[0],config_2p=configs[1],config_3p=configs[2],config_4p=configs[3],allIngredients=new[]{authored}};
            // Campaign's ordinary menu can be empty: scripted-only dishes still supply all consumers.
            if(mode==LevelInfoSO.LevelType.Campaign)level.recipes=new ScriptableObject[0];
            var ordinary=level.recipes;
            LayoutEditorAllIngredientsFill.AutoFillIngredientsFromSelectedRecipes(level);
            Check(Has(level.allIngredients,leaf),mode+" "+p+"P selected ingredients consume active menu");
            if(mode==LevelInfoSO.LevelType.Dynamic)Check(Has(level.allIngredients,authored),"incomplete phase preserves authored ingredient");
            Check(LayoutEditorHotPotFill.CollectHotPotNodes(level).Contains(leaf),mode+" "+p+"P hotpot input");
            Check(LayoutEditorRoastTrayFill.CollectRoastTrayIngredients(level).Contains(leaf),mode+" "+p+"P roast input");
            Check(LayoutEditorSteamerFill.SteamerInputs(level).Contains(mixed),mode+" "+p+"P steamer input");
            Check(LayoutEditorSteamerFill.MixerInputs(level).Contains(leaf),mode+" "+p+"P mixer input");
            Check(LayoutEditorPancakeFill.HasPlainPancake(level),mode+" "+p+"P pancake detection");
            Check(LayoutEditorMatchlistMap.CollectRequiredMatchlistKeys(level).Contains("dlc05"),mode+" "+p+"P nested cooking-step matchlist");
            int destroyed=UnityEngine.Object.Destroyed;
            SpecialOptionals.SyncSpecialRecipeOptionals(level);
            Check(Has(level.optionalRecipeMatchListItems,milkWater)&&Has(level.optionalRecipeMatchListItems,sushiCombo)
                &&Has(level.optionalRecipeMatchListItems,mixed),mode+" "+p+"P milk/sushi/mixed registration");
            Check(SpecialOptionals.Burgers.Contains(milk)&&SpecialOptionals.Burgers.Contains(steamer),mode+" "+p+"P burger registration input union");
            Check(LayoutEditorSaladSauceFactory.Passed!=level&&LayoutEditorSaladSauceFactory.Passed.levelType==mode
                &&Has(LayoutEditorSaladSauceFactory.Passed.recipes,milk)&&UnityEngine.Object.Destroyed==destroyed+1,
                mode+" salad projection keeps mode and destroys clone");
            SpecialOptionals.SyncSpecialRecipeOptionals(level);
            Check(level.optionalRecipeMatchListItems.Length==3,mode+" special registration idempotent");
            AssetDatabase.Dependencies[hot.path]=new[]{leaf.path,missing.path,shared.path};
            NativeDependencies.EnsureWebDependencies("fixture",level,true);
            Check(Array.IndexOf(level.dependencies,"bundle18")>=0&&Array.IndexOf(level.dependencies,"bundle999")<0
                &&Array.IndexOf(level.dependencies,"bundle600")>=0&&Array.IndexOf(level.dependencies,"commonW5")<0,
                mode+" existing native bundles only, including nested wrappers and native importer labels");
            Check((Array.IndexOf(level.dependencies,"bundle354")>=0)==(mode==LevelInfoSO.LevelType.Horde),mode+" enemy deps only for active Horde");
            Check(ReferenceEquals(level.recipes,ordinary)&&ReferenceEquals(menu.recipes,active)
                &&ReferenceEquals(configs[p].scriptedRecipes,active)&&active.Length==7&&active[0]==active[6]
                &&wave.enemies.Length==2,mode+" all menu arrays and repeated entries preserved");
        }
    }
}
'''


def main():
    store = (EDITOR / 'LayoutEditorLevelModeStore.cs').read_text()
    admin = (EDITOR / 'LayoutEditorLevelAdminApi.cs').read_text()
    catalog = (EDITOR / 'LayoutEditorCatalogApi.cs').read_text()
    custom = (EDITOR / 'LayoutEditorCustomIngredients.cs').read_text()
    matchlist = (EDITOR / 'LayoutEditorMatchlistMap.cs').read_text()
    for method, call in [('AnalyzeBundles', 'CollectBundleNames(CollectUsageRoots(so), referenced)'),
                         ('ScanResourceUsage', 'CollectUsagePaths(CollectUsageRoots(so), paths)')]:
        body = extract(admin, method)
        assert call in body and 'so.recipes' not in body, method
    for source, method in [(catalog, 'SyncCookingStepsFromSelectedRecipes'),
                           (matchlist, 'CollectRequiredMatchlistKeys'), (custom, 'DependencyRecipes')]:
        body = extract(source, method)
        assert 'CollectAllRecipes(info)' in body and 'info.recipes' not in body, method
    assert 'SyncRecipeData(' not in re.sub(r'//[^\n]*', '', custom)
    core = extract(custom, 'EnsureWebDependenciesCore')
    assert 'info.recipes' not in core and 'var recipes = DependencyRecipes(info)' in core
    assert 'CollectHordeEnemies(info)' in core and 'AssetDatabase.GetDependencies(paths.ToArray(), true)' in core
    assert 'CollectModeReferences(info)' not in core, 'inactive player fields must not enter dependency closure'
    assert 'AddDependency(deps, pseudo.bundleName)' in core
    assert custom.count('foreach (var r in DependencyRecipes(info))') == 3
    assert 'projection.recipes = LayoutEditorLevelModeStore.CollectAllRecipes(info)' in custom
    assert 'finally { UnityEngine.Object.DestroyImmediate(projection); }' in custom
    sync = extract(store, 'SyncRecipeData')
    assert 'projection' not in sync
    for call in ['AutoFillIngredientsFromSelectedRecipes', 'SyncCookingStepsFromSelectedRecipes',
                 'SyncSpecialRecipeOptionals', 'EnsureRequiredMatchlists', 'SyncRoastTrayIngredients',
                 'SyncHotPotIngredients', 'SyncSteamerIngredients', 'SyncLoadedScenes']:
        assert call + '(info)' in sync, call
    setter = extract(catalog, 'SetLevelRecipes')
    assert setter.index('info.levelType != LevelInfoSO.LevelType.Campaign') < setter.index('Undo.RecordObject(')
    assert setter.index('info.levelType != LevelInfoSO.LevelType.Campaign') < setter.index('GUIDToAssetPath(')
    special = extract(catalog, 'SyncSpecialRecipeOptionals')
    for call in ['CollectAllRecipes(info)', 'SyncBurgerOptionalsForSavedRecipes(', 'SyncMilkTeaIntermediate(',
                 'SyncSushiOptionalCombo(', 'SyncCustomSubRecipeOptionals(']:
        assert call in special, call
    ingredients = (EDITOR / 'LayoutEditorAllIngredientsFill.cs').read_text()
    for method in ['AutoFillIngredients', 'AutoFillIngredientsFromSelectedRecipes']:
        assert 'CollectAllRecipes(levelInfo)' in extract(ingredients, method), method
    for file, methods in [('HotPot', ['CollectHotPotNodes']), ('RoastTray', ['CollectRoastTrayIngredients']),
                          ('Steamer', ['CollectSteamerInputs', 'CollectMixerInputs']),
                          ('Pancake', ['HasPlainPancake', 'EmptyPanRestrictionReason'])]:
        source = (EDITOR / ('LayoutEditor' + file + 'Fill.cs')).read_text()
        for method in methods:
            assert 'CollectAllRecipes(info)' in extract(source, method), method
        if file != 'Pancake':
            assert 'OwnsRecipeConsumer(info, stub)' in source, file
        else:
            assert 'Array.IndexOf(deps, "commonW5")' not in source, 'shared bundle cannot keep native repair firing'
    print('PASS: shared usage roots, aggregate dependency/matchlist/cooking wiring and nonrecursive projection source contracts.', flush=True)
    members = '\n'.join(extract(store, name) for name in (
        'CollectAllRecipes', 'CollectModeReferences', 'CollectHordeEnemies', 'AddRecipes', 'PlayerConfigs', 'GetMode', 'OwnsRecipeConsumer'))
    consumers = '\n'.join([extract(admin, name) for name in ('CollectUsageRoots', 'CollectUsagePaths',
        'CollectUsagePathsDeep', 'DeleteCustomRecipeCategory')] + [
        extract(catalog, 'SyncCookingStepsFromSelectedRecipes'), extract(catalog, 'CollectCookingSteps')])
    consumer_methods = {
        '__INGREDIENTS__': (ingredients, ['AutoFillIngredientsFromSelectedRecipes', 'CollectCustomRecipeIngredients',
            'CollectOfficialRecipeIngredients', 'PreserveIncompleteIngredients', 'IsMachineOnlyIngredient']),
        '__SPECIAL__': (catalog, ['SyncSpecialRecipeOptionals', 'SyncMilkTeaIntermediate', 'SyncSushiOptionalCombo',
            'SyncCustomSubRecipeOptionals', 'AddNestedCustomSubRecipes']),
        '__NATIVE_DEPS__': (custom, ['EnsureWebDependencies', 'EnsureWebDependenciesCore', 'DependencyRecipes',
            'AddDependency', 'ReadBundleNameField']),
        '__ROAST__': ((EDITOR / 'LayoutEditorRoastTrayFill.cs').read_text(), ['CollectRoastTrayIngredients']),
        '__HOTPOT__': ((EDITOR / 'LayoutEditorHotPotFill.cs').read_text(), ['CollectHotPotNodes']),
        '__STEAMER__': ((EDITOR / 'LayoutEditorSteamerFill.cs').read_text(), ['CollectSteamerInputs', 'CollectMixerInputs', 'IsSushiNotForSteamer']),
        '__PANCAKE__': ((EDITOR / 'LayoutEditorPancakeFill.cs').read_text(), ['HasPlainPancake']),
        '__MATCHLIST__': (matchlist, ['CollectRequiredMatchlistKeys', 'CollectFrom', 'CollectFromPseudo', 'Walk',
            'MatchlistKeyOfAsset', 'MatchlistKeyOfBundlePath']),
    }
    with tempfile.TemporaryDirectory(prefix='mode-recipe-consumers-') as temp:
        folder = Path(temp)
        (folder / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion>'
            '<GenerateAssemblyInfo>false</GenerateAssemblyInfo><EnableNETAnalyzers>false</EnableNETAnalyzers>'
            '<NoWarn>0649</NoWarn></PropertyGroup></Project>')
        # name is used only for production log output.
        harness = HARNESS.replace('public string path;', 'public string path; public string name;')
        wrapper = extract(custom, 'EnsureWebDependencies',
            r'string levelSet, LevelInfoSO info, bool replaceExisting\)')
        harness = harness.replace('__STORE__', members).replace('__CONSUMERS__', consumers).replace('__DEPENDENCY_WRAPPER__', wrapper)
        for marker, (source, methods) in consumer_methods.items():
            harness = harness.replace(marker, '\n'.join(extract(source, method,
                r'string levelSet, LevelInfoSO info, bool replaceExisting\)' if method == 'EnsureWebDependencies' else '')
                for method in methods))
        (folder / 'Program.cs').write_text(harness)
        result = subprocess.run(['dotnet', 'run', '--project', str(folder / 'Checks.csproj')],
            env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1'), timeout=90)
        if result.returncode == 0:
            import sys
            subprocess.run([sys.executable, str(Path(__file__).with_name('check-mode-recipe-transaction.py'))],
                check=True, timeout=120)
        raise SystemExit(result.returncode)


if __name__ == '__main__':
    main()
