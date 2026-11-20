#!/usr/bin/env python3
"""Execute production Save, snapshot, sidecar writer and Burger mutations with C#4 doubles.

AssetDatabase/Undo are doubled; actual temporary disk files exercise rollback and
sidecar Move/Replace failure after BurgerApi's nested SaveAssets. No Unity writes.
"""
import os
from pathlib import Path
import re
import runpy
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
EDITOR = ROOT / 'Assets/Editor/LayoutEditor'
extract = runpy.run_path(str(Path(__file__).with_name('check-mode-recipe-consumers.py')))['extract']


HARNESS = r'''
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
namespace UnityEngine {
    public class Object {
        public string name,path;
        public static int Copies,Destroyed;
        public static T Instantiate<T>(T value) where T:Object {
            Copies++;var copy=(T)value.MemberwiseClone();Fields(value,copy);return copy;
        }
        public static void Fields(Object source,Object target) {
            foreach(var field in source.GetType().GetFields()) {
                if(field.IsStatic)continue;
                var value=field.GetValue(source);var array=value as Array;
                field.SetValue(target,array==null?value:array.Clone());
            }
        }
        public static void DestroyImmediate(Object value) { Destroyed++; }
    }
    public class ScriptableObject:Object { }
    public class GameObject:Object {
        public T[] GetComponentsInChildren<T>(bool inactive) { return new T[0]; }
    }
    public static class JsonUtility {
        public static string ToJson<T>(T value,bool pretty=false) { return System.Text.Json.JsonSerializer.Serialize(value,new System.Text.Json.JsonSerializerOptions {IncludeFields=true}); }
        public static T FromJson<T>(string json) { return System.Text.Json.JsonSerializer.Deserialize<T>(json,new System.Text.Json.JsonSerializerOptions {IncludeFields=true}); }
    }
}
namespace UnityEngine.SceneManagement {
    public class Scene {
        public bool isLoaded;public bool IsValid() { return false; }
        public GameObject[] GetRootGameObjects() { return new GameObject[0]; }
    }
    public static class SceneManager {
        public static int sceneCount { get { return 0; } }
        public static Scene GetSceneAt(int i) { return new Scene(); }
    }
}
class PseudoPrefabManagerStub { public LevelInfoSO levelInfo; }
class PseudoPrefabSO:ScriptableObject { }
class CustomRecipeSO:ScriptableObject {
    public ScriptableObject[] compositionSOs,optionalSOs;
    public GameObject model;public PseudoPrefabSO modelSO;public string recipeName;
}
class CustomRecipeOptionalBurgerSO:CustomRecipeSO {
    public PseudoPrefabSO bunSO;public PseudoPrefabSO[] ingredientModelSOs;
    public GameObject[] ingredientModels;public int ingredientContainerCapacity,uID;
}
class CustomRecipeConfigSO:ScriptableObject { public int uidPrefix=12,nextSequence=5; }
class LevelConfigSetupPerPlayerCountSO:ScriptableObject { public ScriptableObject[] scriptedRecipes; }
class LevelInfoSO:ScriptableObject {
    public string[] dependencies;public ScriptableObject[] recipes,optionalRecipeMatchListItems;
    public LevelConfigSetupPerPlayerCountSO config_1p,config_2p,config_3p,config_4p;
    public int revision;
}
class LevelModeConfigDto { public string assetPath,gameMode;public object enemies; }
static class LayoutEditorSafety { public static string PlayModeWriteBlockReason() { return null; } }
static class LayoutEditorLog { public static void Log(string s) { } public static void LogWarning(string s) { } }
static class Undo {
    static int group;public static void RecordObject(object value,string label) { }
    public static void IncrementCurrentGroup() { group++; }public static int GetCurrentGroup() { return group; }
    public static void RegisterFullObjectHierarchyUndo(object value,string label) { }
    public static void RevertAllDownToGroup(int value) { }public static void CollapseUndoOperations(int value) { }
}
static class EditorUtility {
    public static void SetDirty(object value) { }
    public static void CopySerialized(UnityEngine.Object source,UnityEngine.Object target) { UnityEngine.Object.Fields(source,target); }
}
// Forward real file operations, injecting only the final sidecar commit failure.
static class File {
    public static bool Fail;public static int SidecarAttempts;
    public static bool Exists(string path) { return System.IO.File.Exists(path); }
    public static byte[] ReadAllBytes(string path) { return System.IO.File.ReadAllBytes(path); }
    public static string ReadAllText(string path) { return System.IO.File.ReadAllText(path); }
    public static void WriteAllBytes(string path,byte[] bytes) { System.IO.File.WriteAllBytes(path,bytes); }
    public static void WriteAllText(string path,string text) { System.IO.File.WriteAllText(path,text); }
    public static void Delete(string path) { System.IO.File.Delete(path); }
    static void Commit() { SidecarAttempts++;if(Fail)throw new IOException("sidecar commit injected failure"); }
    public static void Move(string source,string target) { Commit();System.IO.File.Move(source,target); }
    public static void Replace(string source,string target,string backup) { Commit();System.IO.File.Move(source,target,true); }
}
static class AssetDatabase {
    public static readonly Dictionary<string,UnityEngine.Object> Assets=new Dictionary<string,UnityEngine.Object>();
    public static int Saves;
    public static string GetAssetPath(UnityEngine.Object value) { return value==null?"":value.path; }
    public static T LoadAssetAtPath<T>(string path) where T:UnityEngine.Object {
        UnityEngine.Object value;return Assets.TryGetValue(path,out value)?value as T:null;
    }
    public static UnityEngine.Object LoadMainAssetAtPath(string path) { return LoadAssetAtPath<UnityEngine.Object>(path); }
    public static string AssetPathToGUID(string path) { return path; }public static string GUIDToAssetPath(string path) { return path; }
    public static bool IsValidFolder(string path) { return Directory.Exists(path); }
    public static string[] GetSubFolders(string path) { return Directory.GetDirectories(path); }
    public static bool DeleteAsset(string path) {
        var remove=new List<string>();foreach(var key in Assets.Keys)if(key==path||key.StartsWith(path+"/"))remove.Add(key);
        foreach(var key in remove)Assets.Remove(key);
        if(Directory.Exists(path))Directory.Delete(path,true);else if(File.Exists(path))File.Delete(path);
        if(File.Exists(path+".meta"))File.Delete(path+".meta");return true;
    }
    public static bool CopyAsset(string source,string target) {
        var copy=UnityEngine.Object.Instantiate(Assets[source]);copy.path=target;Assets.Add(target,copy);
        File.WriteAllText(target,"copied optional");File.WriteAllText(target+".meta","new-guid");return true;
    }
    public static string State(UnityEngine.Object value) {
        var result=new StringBuilder();
        foreach(var field in value.GetType().GetFields()) {
            if(field.IsStatic)continue;result.Append(field.Name+"=");var v=field.GetValue(value);var array=v as Array;
            if(array!=null)foreach(var item in array)result.Append(Ref(item)+",");else result.Append(Ref(v));
            result.Append(";");
        }
        return result.ToString();
    }
    static string Ref(object value) { var asset=value as UnityEngine.Object;return asset!=null?asset.path:value==null?"null":value.ToString(); }
    public static void SaveAssets() {
        Saves++;foreach(var pair in Assets)if(File.Exists(pair.Key))File.WriteAllText(pair.Key,State(pair.Value));
    }
}
static class LayoutEditorLevelAdminApi {
    public const string LevelSetsRoot="Assets/LevelSets";
    public static bool AssetFolderExists(string path) { return Directory.Exists(path); }
    public static bool IsUidConflicting(int uid) { return false; }
    public static string CustomRecipeNamesPath(string set,string path) { return Path.GetDirectoryName(path)+"/names.json"; }
    public static void AddCustomRecipeName(string path,string id,string zh,string en) { File.WriteAllText(path,"allocated name"); }
}
static class LayoutEditorBurgerApi {
    const string LevelBurgerOptionalFileName="BurgerOptional";
    public static CustomRecipeOptionalBurgerSO Template;
    static CustomRecipeOptionalBurgerSO FindCommonW2BurgerModelAssembly() { return Template; }
    static bool IsFinishedBurgerRecipe(CustomRecipeSO recipe) { return true; }
    static bool IsBurgerBunId(string id) { return id=="bun"; }
    static void CollectCommonW2LayerModelBindings(out Dictionary<string,PseudoPrefabSO> a,out Dictionary<string,GameObject> b) {
        a=new Dictionary<string,PseudoPrefabSO>();b=new Dictionary<string,GameObject>();
    }
    static void ResolveLayerModel(ScriptableObject layer,Dictionary<string,PseudoPrefabSO> a,Dictionary<string,GameObject> b,
        Dictionary<string,PseudoPrefabSO> c,Dictionary<string,GameObject> d,out PseudoPrefabSO so,out GameObject go) { so=null;go=null; }
__BURGER__
}
static class LayoutEditorLevelModeStore {
    const string AssetFolder="mode_assets";const int MaxJsonBytes=2097152;
    public static bool Rebuild,SyncFail;
    static string AbsPath(string path) { return Path.GetFullPath(path); }
    static string GetStorePath(string path) { return Path.Combine(Path.GetDirectoryName(path),"mode_config~/mode_config.json"); }
    static string ValidateLevelPath(string path,LevelInfoSO info) { return info==null?"missing":null; }
    static string ValidateForSave(string path,LevelInfoSO info,LevelModeConfigDto dto) { return null; }
    static bool NativeMatches(string path,LevelInfoSO info,LevelModeConfigDto dto) { return !Rebuild; }
    static void SynchronizeAssets(string path,LevelInfoSO info,LevelModeConfigDto dto) {
        var root=Path.GetDirectoryName(path)+"/mode_assets";Directory.CreateDirectory(root+"/r_new");
        File.WriteAllText(root+".meta","new root meta");File.WriteAllText(root+"/r_new.meta","new revision meta");
        File.WriteAllText(root+"/r_new/menu.asset","new menu");info.revision++;
    }
    static void ApplySettings(LevelInfoSO info,LevelModeConfigDto dto) { info.dependencies=new[]{"bundle47"}; }
    static string GetMode(LevelInfoSO info) { return "Campaign"; }
    static LevelModeConfigDto Load(string path,LevelInfoSO info) { return new LevelModeConfigDto(); }
    static void SyncRecipeData(LevelInfoSO info) {
        CustomRecipeOptionalBurgerSO optional;
        var error=LayoutEditorBurgerApi.FindOrCreateLevelBurgerOptional(info.path,"fixture",out optional);
        if(error!=null)throw new Exception(error);
        LayoutEditorBurgerApi.SyncLevelBurgerOptionalFromBurgers(optional,new[]{(CustomRecipeSO)info.recipes[0]});
        info.optionalRecipeMatchListItems=new ScriptableObject[]{optional};
        if(SyncFail)throw new IOException("consumer failure");
    }
__STORE__
}
class Checks {
    static int count;static void Check(bool ok,string label) { count++;if(!ok)throw new Exception(label); }
    static void Main(string[] args) {
        Directory.SetCurrentDirectory(args[0]);
        foreach(bool existing in new[]{false,true})foreach(bool rebuild in new[]{false,true})foreach(bool oldRevision in new[]{false,true}) {
            var root="Assets/LevelSets/fixture/data/level";Directory.CreateDirectory(root);
            var recipesRoot="Assets/LevelSets/fixture/custom_recipes";Directory.CreateDirectory(recipesRoot);
            AssetDatabase.Assets.Clear();
            var bun=new PseudoPrefabSO {path="bun.asset",name="bun"};
            var filling=new PseudoPrefabSO {path="filling.asset",name="filling"};
            var burger=new CustomRecipeSO {compositionSOs=new ScriptableObject[]{bun,filling,filling},path="burger.asset"};
            var oldMenu=new ScriptableObject[]{burger,burger};
            var player=new LevelConfigSetupPerPlayerCountSO {path=root+"/player.asset",scriptedRecipes=oldMenu};
            var info=new LevelInfoSO {path=root+"/info.asset",recipes=oldMenu,config_4p=player,dependencies=new[]{"bundle18"}};
            var config=new CustomRecipeConfigSO {path=recipesRoot+"/CustomRecipeConfig.asset"};
            var template=new CustomRecipeOptionalBurgerSO {path="template.asset"};
            LayoutEditorBurgerApi.Template=template;
            foreach(var asset in new UnityEngine.Object[]{info,player,config,template}) {
                AssetDatabase.Assets.Add(asset.path,asset);File.WriteAllText(asset.path,"pre-request "+asset.path);
            }
            var optionalPath=root+"/BurgerOptional.asset";
            if(oldRevision) {
                Directory.CreateDirectory(root+"/mode_assets/r_old");
                File.WriteAllText(root+"/mode_assets/r_old/menu.asset","old menu");
                File.WriteAllText(root+"/mode_assets.meta","old mode root guid");
            }
            if(existing) {
                var optional=new CustomRecipeOptionalBurgerSO {path=optionalPath,bunSO=filling,optionalSOs=new ScriptableObject[]{bun},ingredientContainerCapacity=9};
                AssetDatabase.Assets.Add(optionalPath,optional);File.WriteAllText(optionalPath,"old optional disk");File.WriteAllText(optionalPath+".meta","old-guid");
            }
            var names=root+"/names.json";File.WriteAllText(names,"old names");
            var sidecar=root+"/mode_config~/mode_config.json";
            if(existing) { Directory.CreateDirectory(Path.GetDirectoryName(sidecar));File.WriteAllText(sidecar,"old sidecar"); }
            var bytes=File.ReadAllBytes(info.path);var oldState=AssetDatabase.State(info);
            var optionalBefore=AssetDatabase.LoadMainAssetAtPath(optionalPath);
            var optionalState=optionalBefore==null?null:AssetDatabase.State(optionalBefore);
            LayoutEditorLevelModeStore.Rebuild=rebuild;File.Fail=true;
            int copies=UnityEngine.Object.Copies,destroyed=UnityEngine.Object.Destroyed,saves=AssetDatabase.Saves,attempts=File.SidecarAttempts;
            var error=LayoutEditorLevelModeStore.Save(info.path,info,new LevelModeConfigDto {gameMode="Dynamic"});
            Check(error!=null&&error.Contains("sidecar commit injected failure"),"actual final sidecar failure returned");
            Check(File.SidecarAttempts==attempts+1&&AssetDatabase.Saves>saves,"failure occurs after nested Burger SaveAssets");
            Check(AssetDatabase.State(info)==oldState&&info.recipes[0]==burger&&info.recipes[1]==burger,"old menu semantics/repetitions restored");
            Check(Convert.ToBase64String(File.ReadAllBytes(info.path))==Convert.ToBase64String(bytes),"exact LevelInfo disk restored");
            Check(config.nextSequence==5&&File.ReadAllText(config.path)=="pre-request "+config.path,"UID counter memory/disk restored");
            Check(File.ReadAllText(names)=="old names","names bytes restored");
            if(existing) {
                Check(AssetDatabase.LoadMainAssetAtPath(optionalPath)==optionalBefore&&AssetDatabase.State(optionalBefore)==optionalState,"existing Optional identity and fields restored");
                Check(File.ReadAllText(optionalPath)=="old optional disk"&&File.ReadAllText(optionalPath+".meta")=="old-guid","existing Optional disk and GUID restored");
                Check(File.ReadAllText(sidecar)=="old sidecar","existing sidecar retained");
            } else Check(!File.Exists(optionalPath)&&!File.Exists(optionalPath+".meta")&&AssetDatabase.LoadMainAssetAtPath(optionalPath)==null,"new Optional orphan and meta removed");
            Check(!Directory.Exists(root+"/mode_assets/r_new"),"new mode revisions cleaned");
            if(oldRevision)Check(File.ReadAllText(root+"/mode_assets/r_old/menu.asset")=="old menu"
                &&File.ReadAllText(root+"/mode_assets.meta")=="old mode root guid","old revision and root metadata retained");
            else Check(!Directory.Exists(root+"/mode_assets")&&!File.Exists(root+"/mode_assets.meta"),"new mode parent/meta cleaned");
            Check(Directory.GetFiles(root,"*.tmp",SearchOption.AllDirectories).Length==0,"sidecar temporary file cleaned");
            Check(UnityEngine.Object.Copies-copies==UnityEngine.Object.Destroyed-destroyed+(existing?0:1),"snapshot copies disposed (new asset copy excluded)");
            File.Fail=false;
            LayoutEditorLevelModeStore.SyncFail=true;
            error=LayoutEditorLevelModeStore.SyncSaved(info.path,info);
            Check(error!=null&&error.Contains("consumer failure")&&AssetDatabase.State(info)==oldState,"Campaign full sync rolls back downstream failure");
            Check(config.nextSequence==5&&AssetDatabase.LoadMainAssetAtPath(optionalPath)==optionalBefore,"Campaign rollback restores UID/Optional ownership");
            LayoutEditorLevelModeStore.SyncFail=false;attempts=File.SidecarAttempts;
            Check(LayoutEditorLevelModeStore.SyncSaved(info.path,info)==null&&File.SidecarAttempts==attempts,"Campaign full sync succeeds without writing mode sidecar");
            Check(LayoutEditorLevelModeStore.Save(info.path,info,new LevelModeConfigDto {gameMode="Dynamic"})==null,"subsequent successful save");
            var committed=AssetDatabase.LoadAssetAtPath<CustomRecipeOptionalBurgerSO>(optionalPath);
            Check(committed.bunSO==bun&&committed.optionalSOs.Length==2&&committed.optionalSOs[0]==filling&&committed.optionalSOs[1]==filling,"success commits actual Burger layers");
            Check(File.ReadAllText(sidecar).Contains("Dynamic"),"success sidecar readback");
            Directory.Delete("Assets",true);File.Delete("template.asset");
        }
        Console.WriteLine("PASS "+count+" production Save/Burger/snapshot/sidecar failure checks (C#4; real temporary files, Unity doubles).");
    }
}
'''


def main():
    store = (EDITOR / 'LayoutEditorLevelModeStore.cs').read_text()
    burger = (EDITOR / 'LayoutEditorBurgerApi.cs').read_text()
    # Extract the entire production nested helper, including constructors/instance methods.
    nested = re.search(r'    public sealed class RecipeDataSnapshot[\s\S]*?(?=\n    public static string ValidateForSave)', store)
    assert nested, 'snapshot helper'
    members = nested.group() + '\n' + '\n'.join(extract(store, name) for name in ('Save', 'SaveFile', 'PlayerConfigs', 'SyncSaved'))
    methods = '\n'.join(extract(burger, name) for name in ('LevelBurgerOptionalAssetPath',
        'FindOrCreateLevelBurgerOptional', 'SyncLevelBurgerOptionalFromBurgers'))
    assert 'SyncRecipeData(info)' in extract(store, 'SyncSaved')
    assert 'configs.Add(null)' in extract(store, 'SyncSavedForExport')
    for file, variable in [('SceneLayoutApplier.cs', 'steamerLevelInfo'), ('LayoutEditorSetExporter.cs', 'modeInfo')]:
        source = (EDITOR / file).read_text()
        call = source.index('LayoutEditorLevelModeStore.SyncSavedForScene(')
        assert 'LevelType.Campaign' not in source[max(0, call-180):call], file
    with tempfile.TemporaryDirectory(prefix='mode-recipe-transaction-') as temp:
        out = Path(temp)
        (out / 'Program.cs').write_text(HARNESS.replace('__STORE__', members).replace('__BURGER__', methods))
        (out / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion>'
            '<GenerateAssemblyInfo>false</GenerateAssemblyInfo><EnableNETAnalyzers>false</EnableNETAnalyzers>'
            '<NoWarn>0649</NoWarn></PropertyGroup></Project>')
        work = out / 'files'
        work.mkdir()
        subprocess.run(['dotnet', 'run', '--project', str(out / 'Checks.csproj'), '--', str(work)],
            env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1'), check=True, timeout=120)


if __name__ == '__main__':
    main()
