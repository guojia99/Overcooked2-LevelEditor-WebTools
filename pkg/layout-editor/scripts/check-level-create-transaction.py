#!/usr/bin/env python3
"""Execute production CreateLevel with injected Unity/asset-operation failures.

The template copier is tested unchanged in check-level-template-copy.py. Here its
double isolates CreateLevel's orchestration/rollback, including setup, backup,
partial folder/copy failures, binding-before-open, store and deletion failures.
"""
import os
from pathlib import Path
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]


def extract(source, name):
    match = re.search(r'    (?:public|private|internal) static [^\n]+\b' + name + r'\(', source)
    assert match, name
    start = source.index('{', match.start())
    depth = 0
    for token in re.finditer(r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/|[{}]', source[start:]):
        if token.group() == '{':
            depth += 1
        elif token.group() == '}':
            depth -= 1
            if depth == 0:
                return source[match.start():start + token.end()]
    raise AssertionError(name)


HARNESS = r'''
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using LevelEditorStub;
static class State {
    public static string failure;
    public static bool copied,saved,opened,cleaned,restored,sourceOpened,clearedLabels;
    public static List<string> events=new List<string>();
    public static void Step(string s){events.Add(s);if(failure==s)throw new IOException("injected "+s);}
}
namespace UnityEngine {
    public class Object {
        public string name;
        public static T Instantiate<T>(T o) where T:Object {
            State.Step(o is LevelSetInfoSO?"backup":"preview");
            var set=o as LevelSetInfoSO;if(set!=null)return (T)(Object)new LevelSetInfoSO{levelInfos=set.levelInfos};
            return (T)(Object)((LevelInfoSO)(Object)o).Clone();
        }
        public static void DestroyImmediate(Object o){}
    }
    public class ScriptableObject:Object { }
    public class GameObject {
        public T[] GetComponentsInChildren<T>(bool inactive) where T:class{return new[]{new PseudoPrefabManagerStub{levelInfo=AssetDatabase.info} as T};}
    }
}
namespace LevelEditorStub {
    public class LevelInfoSO:UnityEngine.ScriptableObject {
        public enum LevelType { Campaign,Dynamic,Horde }
        public LevelType levelType;
        public string levelName,levelNameZH,sceneName;
        public int[] recipes=new[]{7},roundTimes=new[]{15,15,15};
        public int maxOrderCount=8;
        public bool disableDynamicParenting,enableRespawnBounds=true;
        public LevelConfigSetupPerPlayerCountSO config_1p,config_2p,config_3p,config_4p;
        public LevelInfoSO Clone(){return (LevelInfoSO)MemberwiseClone();}
    }
    public class LevelSetInfoSO:UnityEngine.Object {public LevelInfoSO[] levelInfos;}
    public class LevelConfigSetupPerPlayerCountSO:UnityEngine.ScriptableObject {public int plateReturnTime=7;}
    public class PseudoPrefabManagerStub {public LevelInfoSO levelInfo;}
}
namespace UnityEngine.SceneManagement {
    public struct Scene {
        public bool isDirty;public UnityEngine.GameObject[] GetRootGameObjects(){return new[]{new UnityEngine.GameObject()};}
    }
    public static class SceneManager {
        public static int sceneCount=1;
        public static Scene GetSceneAt(int i){return new Scene();}
    }
}
namespace UnityEditor.SceneManagement {
    public class SceneSetup { }
    public static class EditorSceneManager {
        public static SceneSetup[] GetSceneManagerSetup(){State.Step("setup");return new SceneSetup[0];}
        public static UnityEngine.SceneManagement.Scene OpenScene(string path){
            State.sourceOpened=path.StartsWith("Assets/Template/");
            if(!State.copied||!State.saved||!State.clearedLabels)throw new Exception("opened before local bindings/settings/labels complete");
            State.Step("open");State.opened=true;return new UnityEngine.SceneManagement.Scene();
        }
        public static void RestoreSceneManagerSetup(SceneSetup[] setup){State.restored=true;State.Step("restore");}
    }
}
namespace UnityEditor {
    public class AssetImporter {
        public string assetBundleName,assetBundleVariant="";
        public static AssetImporter GetAtPath(string p){return new AssetImporter{assetBundleName=p=="Assets/LevelSets/test"?"test/info_test":""};}
    }
    public static class AssetDatabase {
        public static LevelInfoSO info;
        public static string GetAssetPath(UnityEngine.Object o){return "";}
        public static string GetImplicitAssetBundleName(string p){throw new Exception("unsafe implicit bundle query");}
        public static bool IsValidFolder(string p){return Directory.Exists(p);}
        public static string CreateFolder(string p,string n){Directory.CreateDirectory(p+"/"+n);State.Step("folder");return "guid";}
        public static T LoadAssetAtPath<T>(string p) where T:UnityEngine.Object{return info as T;}
        public static void SaveAssets(){State.Step("saveassets");State.saved=true;}
        public static void Refresh(){State.Step("refresh");}
    }
    public static class EditorUtility {
        public static void SetDirty(UnityEngine.Object o){}
        public static void CopySerialized(UnityEngine.Object a,UnityEngine.Object b){((LevelSetInfoSO)b).levelInfos=((LevelSetInfoSO)a).levelInfos;}
    }
    public static class Undo {public static void RecordObject(UnityEngine.Object o,string s){} }
}
public class LevelCreateDto {public string setName,levelId,levelName,levelNameZH,gameMode;}
public class LevelModeConfigDto { }
static class LayoutEditorSafety {public static string PlayModeWriteBlockReason(){return null;}}
static class LayoutEditorLog {public static void Log(string s){} public static void LogWarning(string s){} }
static class LayoutEditorNativeDependencies {public static void Clean(LevelInfoSO i){} }
static class LayoutEditorAllIngredientsFill {public static void AutoFillIngredientsFromSelectedRecipes(LevelInfoSO i){State.Step("ingredients");}}
static class LayoutEditorCatalogApi {public static void SyncCookingStepsFromSelectedRecipes(LevelInfoSO i){}}
static class LayoutEditorMatchlistMap {public static void EnsureRequiredMatchlists(LevelInfoSO i){}}
static class LayoutEditorLevelModeStore {
    public static LevelModeConfigDto LoadFromAssets(string p,LevelInfoSO i){State.Step("moderead");return new LevelModeConfigDto();}
    public static string Validate(LevelModeConfigDto d){return null;}
    public static string Save(string p,LevelInfoSO i,LevelModeConfigDto d){State.Step("modestore");return State.failure=="store_error"?"injected store error":null;}
}
static class LayoutEditorNativeModeValidation {
    public static string ValidateScene(UnityEngine.SceneManagement.Scene s,LevelInfoSO i){State.Step("validate");return null;}
}
static class LayoutEditorPseudoReload {public static void SaveScenePrepared(UnityEngine.SceneManagement.Scene s,bool b){State.Step("savescene");}}
static class LayoutEditorLayoutMigrations {public static void MarkLevelCreated(string s){State.Step("migration");}}
class LayoutEditorLevelTemplate {
    public LevelInfoSO info;public LevelConfigSetupPerPlayerCountSO[] configs;
    string scene,infoPath;
    public static LayoutEditorLevelTemplate Read(string mode,string data,string scene,string infoPath){
        State.Step("preflight");var c=new LevelConfigSetupPerPlayerCountSO();
        return new LayoutEditorLevelTemplate{scene=scene,infoPath=infoPath,
            info=new LevelInfoSO{levelType=(LevelInfoSO.LevelType)Enum.Parse(typeof(LevelInfoSO.LevelType),mode)},configs=new[]{c,c,c,c}};
    }
    public void Copy(){
        File.WriteAllText(infoPath,"local info");File.WriteAllText(scene,"local scene");State.Step("copy");
        AssetDatabase.info=info.Clone();AssetDatabase.info.config_1p=configs[0];AssetDatabase.info.config_2p=configs[1];
        AssetDatabase.info.config_3p=configs[2];AssetDatabase.info.config_4p=configs[3];State.copied=true;
    }
    public static void NormalizeLabels(string d,string s,string set){State.Step("labels");State.clearedLabels=true;}
    public void RemoveSupersededModeAssets(){State.Step("superseded");}
    public static string[] Cleanup(string[] paths){
        State.cleaned=true;
        if(State.failure=="reload_delete")return new[]{"DeleteAsset 失败，残留："+paths[0]};
        foreach(var p in paths){if(File.Exists(p))File.Delete(p);if(Directory.Exists(p))Directory.Delete(p,true);}return new string[0];
    }
}
static class Admin {
    const string LevelSetsRoot="Assets/LevelSets";
    public static LevelSetInfoSO set;
    static bool IsLowerIdent(string s){return System.Text.RegularExpressions.Regex.IsMatch(s,"^[a-z0-9_]+$");}
    static string AbsPath(string s){return Path.GetFullPath(s);}
    static LevelSetInfoSO FindSetInfo(string s){return set;}
    static void ReloadPseudo(){State.Step("reload");if(State.failure=="reload_delete")throw new IOException("late failure");}
__METHODS__
}
static class Checks {
    static int count;
    static void Check(bool ok,string m){count++;if(!ok)throw new Exception(m);}
    static void Reset(){
        State.copied=State.saved=State.opened=State.cleaned=State.restored=State.sourceOpened=State.clearedLabels=false;
        State.events.Clear();AssetDatabase.info=null;Admin.set=new LevelSetInfoSO{levelInfos=new[]{new LevelInfoSO()}};
    }
    static void Main(string[] args){
        Directory.SetCurrentDirectory(args[0]);Directory.CreateDirectory("Assets/LevelSets/test/data");Directory.CreateDirectory("Assets/LevelSets/test/scenes");
        foreach(var mode in new[]{"Campaign","Dynamic","Horde"}){
            Reset();State.failure=null;
            Check(Admin.CreateLevel(new LevelCreateDto{setName="test",levelId="new",gameMode=mode})==null,"create succeeds "+mode);
            Check(Admin.set.levelInfos.Length==2,"registered once");Check(!State.sourceOpened&&State.restored,"opens only target and restores setup");
            Check(AssetDatabase.info.recipes[0]==7,"template recipes retained");
            Check(AssetDatabase.info.roundTimes[0]==15&&AssetDatabase.info.maxOrderCount==8&&AssetDatabase.info.enableRespawnBounds,"native defaults retained");
            Check(AssetDatabase.info.config_1p.plateReturnTime==7,"selected config retained");
            if(mode!="Campaign")Check(State.events.IndexOf("modestore")<State.events.IndexOf("open"),"mode graph finalized before open");
            LayoutEditorLevelTemplate.Cleanup(new[]{"Assets/LevelSets/test/scenes/new.unity","Assets/LevelSets/test/data/new"});
        }
        foreach(var failure in new[]{"preflight","preview","moderead","setup","backup","folder","copy","modestore","store_error","saveassets","labels","superseded","open","validate","savescene","restore","migration","refresh","reload","reload_delete"}){
            Reset();State.failure=failure;
            string error=Admin.CreateLevel(new LevelCreateDto{setName="test",levelId="new",gameMode="Horde"});
            if(failure=="reload_delete"){
                Check(error!=null&&error.Contains("DeleteAsset")&&error.Contains("scenes/new.unity"),"deletion failure added to returned creation error");
                Check(Admin.set.levelInfos.Length==1,"registration restored despite deletion failure");
                State.failure=null;LayoutEditorLevelTemplate.Cleanup(new[]{"Assets/LevelSets/test/scenes/new.unity","Assets/LevelSets/test/data/new"});
            } else {
                Check(error!=null,"failure surfaced "+failure);
                Check(Admin.set.levelInfos.Length==1,"registration restored "+failure);
                Check(!File.Exists("Assets/LevelSets/test/scenes/new.unity")&&!Directory.Exists("Assets/LevelSets/test/data/new"),"no partial output "+failure);
                Check(!State.sourceOpened,"never opens source "+failure);
                if(failure=="restore")Check(error.Contains("恢复原场景失败"),"scene restoration failure reported alongside creation error");
            }
        }
        Console.WriteLine("PASS "+count+" production CreateLevel orchestration/failure checks; Unity APIs and store are doubles.");
    }
}
'''


def main():
    source = (ROOT / 'Assets/Editor/LayoutEditor/LayoutEditorLevelAdminApi.cs').read_text(encoding='utf-8-sig')
    methods = '\n'.join(extract(source, name) for name in ('CreateLevel', 'NormalizeLevelMode'))
    with tempfile.TemporaryDirectory(prefix='oc2-create-transaction-') as temp:
        out = Path(temp)
        (out / 'Program.cs').write_text(HARNESS.replace('__METHODS__', methods))
        (out / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (out / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo>'
            '<NuGetAudit>false</NuGetAudit></PropertyGroup></Project>')
        subprocess.run(['dotnet', 'run', '--project', str(out / 'Checks.csproj'), '--', temp], cwd=out,
                       env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1'), check=True, timeout=120)


if __name__ == '__main__':
    main()
