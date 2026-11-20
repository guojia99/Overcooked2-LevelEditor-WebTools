#!/usr/bin/env python3
"""Run production optional edit guards, full UpdateCustomRecipe and Replace in C# 4.

Unity/asset IO and unrelated model/name helpers are doubles. The actual upstream
Platter class compiles unchanged; no Unity assets are created or modified.
Burger workbench preflight ordering is a source contract, not an execution test.
"""
import os
from pathlib import Path
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
EDITOR = ROOT / 'Assets/Editor/LayoutEditor'


def extract(source, declaration):
    matches = list(re.finditer(declaration, source))
    assert len(matches) == 1, declaration
    start = source.index('{', matches[0].start())
    depth = 0
    for token in re.finditer(r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|//[^\n]*|/\*[\s\S]*?\*/|[{}]', source[start:]):
        if token.group() == '{':
            depth += 1
        elif token.group() == '}':
            depth -= 1
            if depth == 0:
                return source[matches[0].start():start + token.end()]
    raise AssertionError(declaration)


HARNESS = r'''
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using LevelEditorStub;
namespace UnityEngine {
    public class Object { public string name; }
    public class ScriptableObject : Object { }
    public class GameObject : Object { }
    public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c) { x=a;y=b;z=c; } }
    public class SerializeField : Attribute { }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string value) { } }
    public class CreateAssetMenuAttribute : Attribute { public string menuName;public int order; }
}
namespace LevelEditorStub {
    public interface ICustomRecipeOptional { }
    public class PseudoPrefabSO : ScriptableObject { public string assetPath; }
    public class CustomRecipeSO : ScriptableObject {
        public enum RecipeType { Composite, Mixed, Ingredient, Item, Cooked }
        public enum CookingProgress { Raw, Cooked, Burnt }
        public enum MixingProgress { Raw, Mixed, Overmixed }
        public RecipeType type;
        public CookingProgress cookingProgress;
        public MixingProgress mixingProgress;
        public int score;
        public ScriptableObject[] compositionSOs,optionalSOs;
        public PseudoPrefabSO cookingStepSO,cookingStepIconSO,platingStepSO,mixingIconSO,modelSO;
        public GameObject model;
    }
    public class CustomRecipeOptionalBurgerSO : CustomRecipeSO, ICustomRecipeOptional {
        public PseudoPrefabSO bunSO; public PseudoPrefabSO[] ingredientModelSOs;
    }
    public class CustomRecipeOptionalPizzaSO : CustomRecipeSO, ICustomRecipeOptional {
        public PseudoPrefabSO doughSO; public PseudoPrefabSO[] rawPizzaIngredientPrefabSOs,cookedPizzaIngredientPrefabSOs;
    }
    public class CustomRecipeOptionalSandwichSO : CustomRecipeSO, ICustomRecipeOptional {
        public PseudoPrefabSO toastSO;public ScriptableObject[] sauceSOs;public PseudoPrefabSO[] ingredientModelSOs;
    }
    public class FutureOptional : CustomRecipeSO, ICustomRecipeOptional { public string futureData = "keep"; }
}
public static class Effects { public static int Writes,Resolves; }
namespace UnityEditor {
    public static class AssetDatabase {
        public static Dictionary<string,UnityEngine.Object> Assets = new Dictionary<string,UnityEngine.Object>();
        public static T LoadAssetAtPath<T>(string path) where T : class {
            UnityEngine.Object value;return Assets.TryGetValue(path,out value)?value as T:null;
        }
        public static string GetAssetPath(UnityEngine.Object value) {
            foreach(var pair in Assets) if(object.ReferenceEquals(pair.Value,value)) return pair.Key;
            return "";
        }
        public static string AssetPathToGUID(string path) { return path; }
        public static void SaveAssets() { Effects.Writes++; }
    }
    public static class Undo { public static void RecordObject(object o,string label) { Effects.Writes++; } }
    public static class EditorUtility { public static void SetDirty(object o) { Effects.Writes++; } }
}
public static class LayoutEditorLog { public static void Log(string value) { } }
public static class LayoutEditorLevelAdminApi {
    public const string LevelSetsRoot = "Assets/LevelSets";
    public static string SetNameFromPath(string path) { return "test"; }
    public static string CustomRecipeNamesPath(string set,string path) { return path; }
    public static void UpdateCustomRecipeName(string path,string id,string zh,string en) { Effects.Writes++; }
    public static string ResolveCompositionIds(string[] ids,out ScriptableObject[] result) {
        Effects.Resolves++;result=new ScriptableObject[0];return ids!=null&&ids.Length>0?"unresolved":null;
    }
    public static PseudoPrefabSO FindPseudoPrefabById(string id) { return new PseudoPrefabSO(); }
    public static PseudoPrefabSO FindPlatingContainerById(string id) { return new PseudoPrefabSO(); }
    public static GameObject FindCustomRecipeModelByRecipeId(string id) { return null; }
    public static GameObject LoadGameObjectGuid(string id) { return null; }
    public static void SetRecipeModelTransform(string path,params float[] values) { Effects.Writes++; }
    public static void ApplyModelTransform(CustomRecipeSO so) { Effects.Writes++; }
    public static void ApplySandwichFields(CustomRecipeOptionalSandwichSO so,CustomRecipeEditDto dto) { Effects.Writes++; }
    __UPDATE__
}
public static class LayoutEditorBurgerApi {
    public class BurgerCandidateDto { public string id,assetPath,bundleName;public bool bundleAvailable; }
    public static BurgerCandidateDto[] CollectBuns() {
        return new[] { new BurgerCandidateDto { id="ChoppedBunSO",assetPath="target/ChoppedBunSO.asset",bundleAvailable=true } };
    }
    public static bool IsBurgerBunId(string id) { return id.IndexOf("ChoppedBun",StringComparison.OrdinalIgnoreCase)>=0; }
}
public static class LayoutEditorBunSwapApi {
    public const int BunFieldLayerIndex = -1;
    __BUN__
}
__DTO__
public static class Program {
    static int checks;
    static void Check(bool value,string message) { checks++;if(!value) throw new Exception(message); }
    static Dictionary<FieldInfo,object> Snapshot(CustomRecipeSO so) {
        var result=new Dictionary<FieldInfo,object>();
        foreach(var field in so.GetType().GetFields()) result.Add(field,field.GetValue(so));
        return result;
    }
    static void Unchanged(CustomRecipeSO so,Dictionary<FieldInfo,object> before,ScriptableObject[] comps,ScriptableObject[] opts) {
        foreach(var pair in before) Check(object.Equals(pair.Value,pair.Key.GetValue(so)),"field preserved: "+pair.Key.Name);
        Check(so.compositionSOs.Length==1&&so.compositionSOs[0]==comps[0],"composition contents preserved");
        Check(so.optionalSOs.Length==1&&so.optionalSOs[0]==opts[0],"optional contents preserved");
    }
    static CustomRecipeSO Seed(CustomRecipeSO so) {
        so.type=CustomRecipeSO.RecipeType.Composite;so.score=123;
        so.compositionSOs=new ScriptableObject[] { AssetDatabase.Assets["old/DLC08_ChoppedBun.asset"] as ScriptableObject };
        so.optionalSOs=new ScriptableObject[] {new PseudoPrefabSO()};
        so.cookingStepSO=new PseudoPrefabSO();so.cookingStepIconSO=new PseudoPrefabSO();
        so.platingStepSO=new PseudoPrefabSO();so.model=new GameObject();so.modelSO=new PseudoPrefabSO();
        return so;
    }
    public static void Main() {
        const string root="Assets/LevelSets/test/custom_recipes/";
        AssetDatabase.Assets.Add("old/DLC08_ChoppedBun.asset",new PseudoPrefabSO());
        AssetDatabase.Assets.Add("target/ChoppedBunSO.asset",new PseudoPrefabSO());
        var platter=(CustomRecipeOptionalPlatterSO)Seed(new CustomRecipeOptionalPlatterSO());
        platter.ingredientModelSOs=new[] { new PseudoPrefabSO() };platter.ingredientModels=new[] {new GameObject()};
        var denied=new[] {platter,Seed(new FutureOptional())};
        Check(!LayoutEditorOptionalRecipe.CanEdit(null),"null is not editable");
        foreach(var known in new CustomRecipeSO[] {new CustomRecipeSO(),new CustomRecipeOptionalBurgerSO(),
            new CustomRecipeOptionalPizzaSO(),new CustomRecipeOptionalSandwichSO()})
            Check(LayoutEditorOptionalRecipe.CanEdit(known),"supported types remain editable");
        for(int i=0;i<denied.Length;i++) {
            var so=denied[i];var path=root+i+".asset";AssetDatabase.Assets.Add(path,so);
            var snapshot=Snapshot(so);var comps=(ScriptableObject[])so.compositionSOs.Clone();
            var opts=(ScriptableObject[])so.optionalSOs.Clone();
            Check(LayoutEditorOptionalRecipe.IsAssembly(so)&&LayoutEditorOptionalRecipe.KindOf(so)=="","unknown remains assembly");
            Check(!LayoutEditorOptionalRecipe.CanEdit(so),"unknown is not editable");
            foreach(var type in new[] {"Composite","Cooked","Ingredient"}) {
                Effects.Writes=0;Effects.Resolves=0;
                var error=LayoutEditorLevelAdminApi.UpdateCustomRecipe(new CustomRecipeEditDto {
                    assetPath=path,type=type,score=1,optionalKind="sandwich",compositionIds=new[] {"bad"} });
                Check(error!=null&&error.Contains(so.GetType().Name)&&error.Contains("Web"),"clear type-specific rejection");
                Check(Effects.Writes==0&&Effects.Resolves==0,"reject before resolve/Undo/sidecars/name/save");
                Unchanged(so,snapshot,comps,opts);
            }
            foreach(var dry in new[] {true,false}) {
                Effects.Writes=0;
                var result=LayoutEditorBunSwapApi.Replace(new LayoutEditorBunSwapApi.BunReplaceRequestDto {
                    setName="test",targetBunId="ChoppedBunSO",assetPaths=new[] {path},dryRun=dry });
                Check(result.error==null&&result.changed==0&&result.layers==0,"unknown-only batch changes nothing");
                Check(result.skipped.Length==1&&result.skipped[0].Contains(so.GetType().Name),"batch explains unsupported type");
                Check(Effects.Writes==0,"unknown-only batch has no writes or save");Unchanged(so,snapshot,comps,opts);
            }
        }
        var normal=Seed(new CustomRecipeSO());AssetDatabase.Assets.Add(root+"normal.asset",normal);
        var burger=(CustomRecipeOptionalBurgerSO)Seed(new CustomRecipeOptionalBurgerSO());
        burger.bunSO=(PseudoPrefabSO)AssetDatabase.Assets["old/DLC08_ChoppedBun.asset"];
        AssetDatabase.Assets.Add(root+"burger.asset",burger);
        foreach(var dry in new[] {true,false}) {
            Effects.Writes=0;
            var result=LayoutEditorBunSwapApi.Replace(new LayoutEditorBunSwapApi.BunReplaceRequestDto {
                setName="test",targetBunId="ChoppedBunSO",dryRun=dry,
                assetPaths=new[] {root+"0.asset",root+"normal.asset",root+"1.asset",root+"burger.asset"} });
            Check(result.changed==2&&result.layers==3&&result.skipped.Length==2,"mixed batch retains supported behavior");
            Check(dry?Effects.Writes==0:Effects.Writes>0,"dry run versus apply");
            Check(normal.compositionSOs[0]==AssetDatabase.Assets[dry?"old/DLC08_ChoppedBun.asset":"target/ChoppedBunSO.asset"],"batch readback");
        }
        var optional=normal.optionalSOs;Effects.Writes=0;
        Check(LayoutEditorLevelAdminApi.UpdateCustomRecipe(new CustomRecipeEditDto {
            assetPath=root+"normal.asset",type="Composite",score=9,compositionIds=new string[0] })==null,"normal update succeeds");
        Check(normal.score==9&&normal.compositionSOs.Length==0&&normal.platingStepSO==null,"normal update readback");
        Check(normal.optionalSOs==optional&&Effects.Writes>0,"normal optional preserved; writes exercised");
        Console.WriteLine("PASS "+checks+" optional edit guard checks (C# 4; production update/batch and upstream Platter; Unity doubles).");
    }
}
'''


def main():
    admin = (EDITOR / 'LayoutEditorLevelAdminApi.cs').read_text()
    bun = (EDITOR / 'LayoutEditorBunSwapApi.cs').read_text()
    burger = (EDITOR / 'LayoutEditorBurgerApi.cs').read_text()
    update = extract(admin, r'public static string UpdateCustomRecipe\(')
    create_burger = extract(burger, r'public static string CreateBurger\(')
    assert create_burger.index('LayoutEditorOptionalRecipe.CanEdit(recipe)') < create_burger.index('EnsureLevelSetCategory(')
    assert create_burger.index('LayoutEditorOptionalRecipe.CanEdit(recipe)') < create_burger.index('Undo.RecordObject(')
    server = (EDITOR / 'LayoutEditorHttpServer.cs').read_text()
    assert 'LayoutEditorLevelAdminApi.UpdateCustomRecipe(dto)' in server
    parts = [extract(bun, r'public class ' + name + r'\b') for name in ('BunReplaceRequestDto', 'BunReplaceResultDto')]
    parts += [extract(bun, r'(?:public|private|internal) static [^\n]+\b' + name + r'\(')
              for name in ('Replace', 'Norm', 'SetRecipesDir', 'IsInSetCustomRecipes', 'TryReadBun', 'BuildWarnings')]
    dto = extract((EDITOR / 'LayoutEditorModels.cs').read_text(), r'public class CustomRecipeEditDto\b')
    with tempfile.TemporaryDirectory(prefix='optional-edit-guard-') as temp:
        out = Path(temp)
        (out / 'Program.cs').write_text(HARNESS.replace('__UPDATE__', update).replace('__BUN__', '\n'.join(parts)).replace('__DTO__', dto))
        (out / 'Optional.cs').write_text((EDITOR / 'LayoutEditorOptionalRecipe.cs').read_text())
        (out / 'Platter.cs').write_text((ROOT / 'Assets/Scripts/LevelEditorStub/CustomRecipeOptionalPlatterSO.cs').read_text())
        (out / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion>'
            '<GenerateAssemblyInfo>false</GenerateAssemblyInfo><EnableNETAnalyzers>false</EnableNETAnalyzers>'
            '</PropertyGroup></Project>')
        subprocess.run(['dotnet', 'run', '--project', str(out / 'Checks.csproj')],
            env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1'), check=True, timeout=120)


if __name__ == '__main__':
    main()
