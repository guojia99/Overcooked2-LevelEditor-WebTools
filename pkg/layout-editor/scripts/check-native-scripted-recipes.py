#!/usr/bin/env python3
"""Execute production config/info saves and sequence policy with API doubles, C#4.

No Unity scene/asset writes. Unity serialization, HTTP JsonUtility and Play need live QA.
"""
import os
from pathlib import Path
import re
import subprocess
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
EDITOR = ROOT / 'Assets/Editor/LayoutEditor'


def block(source, pattern):
    match = re.search(pattern, source)
    assert match, pattern
    start = source.index('{', match.start())
    depth = 0
    tokens = r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/|[{}]'
    for token in re.finditer(tokens, source[start:]):
        if token.group() == '{':
            depth += 1
        elif token.group() == '}':
            depth -= 1
            if depth == 0:
                return source[match.start():start + token.end()]
    raise AssertionError(pattern)


HARNESS = r'''
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using LevelEditorStub;
using Object = UnityEngine.Object;
namespace UnityEngine {
    public class Object {
        public string path,name;
        public static T FindObjectOfType<T>() where T:class { return null; }
        public static T[] FindObjectsOfType<T>() where T:class { return new T[0]; }
        public static Object[] FindObjectsOfType(Type t) { return new Object[0]; }
        public static T Instantiate<T>(T value) where T:Object { return (T)value.MemberwiseClone(); }
        public static void DestroyImmediate(Object value){}
    }
    public class ScriptableObject:Object { }
    public class Component:Object { public GameObject gameObject;public int data; }
    public class MonoBehaviour:Component { }
    public class GameObject:Object {
        public List<Component> components=new List<Component>();
        public T[] GetComponentsInChildren<T>(bool all) where T:class {
            var result=new List<T>();foreach(var c in components)if(c is T)result.Add(c as T);return result.ToArray();
        }
        public Component GetComponent(Type type){return null;}
        public T GetComponent<T>() where T:class{return null;}
    }
    public static class Application { public static string streamingAssetsPath="no-bundles"; }
    public class AssetBundle:Object {
        public static AssetBundle LoadFromFile(string path){return null;}
        public T LoadAsset<T>(string path) where T:class{return null;}
        public void Unload(bool all){}
    }
    public static class JsonUtility { public static object Next; public static T FromJson<T>(string s) { return (T)Next; } }
}
namespace LevelEditorStub {
    public class PseudoPrefabSORecipe:ScriptableObject { public Object asset;public string bundleName,assetPath; }
    public class CustomRecipeSO:ScriptableObject { public int uID; }
    public class SpecificPseudoPrefabTag:Component { public string prefabTag; }
    public class Horde { public int targetRepairCostMax; }
    public class LevelConfigSetupPerPlayerCountSO:ScriptableObject {
        public ScriptableObject[] scriptedRecipes; public Horde hordeLevelConfig;
        public int orderLifeTime,timeBetweenOrders,plateReturnTime,roundTime;
        public float survivalTimeMultiplier;
        public int m_OneStarScore,m_TwoStarScore,m_ThreeStarScore,m_FourStarScore;
    }
    public class LevelInfoSO:ScriptableObject {
        public enum LevelType { Campaign, Dynamic, Horde }
        public LevelType levelType; public bool enableRespawnBounds,disableDynamicParenting;
        public string levelName,levelNameZH; public string[] dependencies;
        public int debugRecipeCount,minOrderCount,maxOrderCount;
        public ScriptableObject[] recipes=new ScriptableObject[0];
        public LevelConfigSetupPerPlayerCountSO config_1p,config_2p,config_3p,config_4p;
    }
    public class PseudoPrefabManagerStub:Component { public LevelInfoSO levelInfo; }
}
namespace UnityEngine.SceneManagement {
    public struct Scene {
        public bool isLoaded,isDirty,valid;
        public GameObject[] roots;
        public bool IsValid(){return valid;}
        public GameObject[] GetRootGameObjects(){return roots??new GameObject[0];}
    }
    public static class SceneManager {
        public static Scene[] scenes=new Scene[0];
        public static int sceneCount {get{return scenes.Length;}}
        public static Scene GetSceneAt(int n){return scenes[n];}
        public static Scene GetActiveScene(){return new Scene{valid=true,isLoaded=true};}
    }
}
namespace LevelEditor { }
public class OrderDefinitionNode:ScriptableObject {public int m_uID;}
public static class RecipeHelper {
    public static int Resolves;
    public static OrderDefinitionNode GetOrderDefinitionNode(ScriptableObject so){Resolves++;throw new Exception("bundle unavailable");}
}
public static class LayoutEditorStubIO {public static Type FindCustomStubType(string set,string name){return null;}}
public static class LayoutEditorLog { public static void Log(string s){}public static void LogWarning(string s){} }
namespace UnityEditor.SceneManagement {
    public static class EditorSceneManager { public static void MarkSceneDirty(UnityEngine.SceneManagement.Scene s){} }
}
namespace UnityEditor {
    public enum ImportAssetOptions {ForceSynchronousImport}
    public static class AssetDatabase {
        public static Dictionary<string,ScriptableObject> assets=new Dictionary<string,ScriptableObject>();
        public static int saves;public static bool FailSave;
        public static string GetAssetPath(Object o) { return o.path; }
        public static string GUIDToAssetPath(string g) { return assets.ContainsKey(g)?assets[g].path:""; }
        public static string AssetPathToGUID(string p) { foreach(var a in assets) if(a.Value.path==p)return a.Key;return ""; }
        public static T LoadAssetAtPath<T>(string p) where T:class { foreach(var a in assets)if(a.Value.path==p)return a.Value as T;return null; }
        public static string[] FindAssets(string filter,string[] roots){
            var ids=new List<string>();foreach(var entry in assets)foreach(var root in roots)if(entry.Value.path.StartsWith(root+"/"))ids.Add(entry.Key);return ids.ToArray();
        }
        public static Object[] LoadAllAssetsAtPath(string p){var a=LoadAssetAtPath<ScriptableObject>(p);return a==null?new Object[0]:new Object[]{a};}
        public static void Refresh(ImportAssetOptions options){
            var remove=new List<string>();foreach(var entry in assets)if(entry.Value.path.EndsWith(".asset")&&!File.Exists(entry.Value.path))remove.Add(entry.Key);
            foreach(var id in remove)assets.Remove(id);
        }
        public static void SaveAssets() {
            saves++;
            foreach(var entry in assets)if(entry.Value.path.EndsWith(".asset"))File.WriteAllText(entry.Value.path,"saved-"+saves);
            if(FailSave && saves>1)throw new IOException("injected final save failure after disk write");
        }
    }
    public static class Undo {
        public static int writes,group,rollbacks;
        private static Dictionary<GameObject,List<Component>> hierarchy=new Dictionary<GameObject,List<Component>>();
        private static Dictionary<Component,int> values=new Dictionary<Component,int>();
        public static void RecordObject(Object o,string s){writes++;}
        public static void IncrementCurrentGroup(){group++;hierarchy.Clear();values.Clear();}
        public static int GetCurrentGroup(){return group;}
        public static void SetCurrentGroupName(string n){}
        public static void CollapseUndoOperations(int n){}
        public static void RegisterFullObjectHierarchyUndo(GameObject go,string n){
            hierarchy[go]=new List<Component>(go.components);foreach(var c in go.components)values[c]=c.data;
        }
        public static void RevertAllDownToGroup(int n){
            rollbacks++;foreach(var entry in hierarchy)entry.Key.components=entry.Value;
            foreach(var entry in values)entry.Key.data=entry.Value;
        }
        public static T AddComponent<T>(GameObject go) where T:Component,new(){return new T{gameObject=go};}
        public static Component AddComponent(GameObject go,Type type){return null;}
        public static void DestroyObjectImmediate(Object o){}
    }
    public static class EditorUtility {
        public static void SetDirty(Object o) { }
        public static void CopySerialized(Object from,Object to){foreach(var field in from.GetType().GetFields())field.SetValue(to,field.GetValue(from));}
    }
}
public static class LayoutEditorLevelModeStore {
    public static bool FailSync;public static int Syncs;
    public static Component Consumer;
    public static ScriptableObject Derived=new LevelConfigSetupPerPlayerCountSO();
    public static string ValidateLevelPath(string path,LevelInfoSO info){return null;}
    public static ScriptableObject[] CollectAllRecipes(LevelInfoSO info){return info.recipes;}
    public static void SyncRecipeData(LevelInfoSO info){
        Syncs++;info.dependencies=new[]{"bundle18"};info.debugRecipeCount=99;
        if(Consumer!=null){Consumer.data=999;Consumer.gameObject.components.Add(new Component{data=999});}
        var root=Path.GetDirectoryName(info.path);
        var optional=AssetDatabase.LoadAssetAtPath<LevelConfigSetupPerPlayerCountSO>(root+"/optional.asset");
        optional.roundTime=999;
        var counter=AssetDatabase.LoadAssetAtPath<LevelConfigSetupPerPlayerCountSO>("Assets/LevelSets/test/custom_recipes/CustomRecipeConfig.asset");
        counter.roundTime++;
        File.WriteAllText(root+"/names.json","changed");
        File.WriteAllText("Assets/LevelSets/test/custom_recipes/names.json","changed");
        Directory.CreateDirectory(root+"/generated");
        File.WriteAllText(root+"/generated/new.asset","generated");
        AssetDatabase.SaveAssets();
        if(FailSync)throw new InvalidOperationException("injected sync failure after derived writes");
    }
}
public static class LayoutEditorNativeDependencies {
    public static string Validate(string[] ids){return null;}
    public static string[] Filter(string[] ids){return ids;}
    public static void Clean(LevelInfoSO info){}
}
public static class LayoutEditorAllIngredientsFill { public static void AutoFillIngredientsFromSelectedRecipes(LevelInfoSO i){} }
public static class LayoutEditorCatalogApi { public static void SyncCookingStepsFromSelectedRecipes(LevelInfoSO i){} }
public static class LayoutEditorMatchlistMap { public static void EnsureRequiredMatchlists(LevelInfoSO i){} }
public static class LayoutEditorLevelAdminApi {
    public static OrderSequenceConfigDto Saved;
    public static string Written;
    private const string OrderSeqDirName="order_seq~",OrderSeqFileName="order_seq.json";
    private const int OrderSeqMaxBytes=262144;
    private static string ValidateLevelInfoPath(string p){return null;}
    private static string WriteLevelDataText(string p,string d,string f,string text,int max){Written=text;return null;}
    public static int SequenceReads;
    public static OrderSequenceConfigDto LoadLevelOrderSequence(string p){SequenceReads++;return Saved;}
    private static string ApplyGridHalfSizeChange(LevelInfoUpdateDto d,LevelInfoSO i){return null;}
    private static int ClampOrderCount(int n,int fallback){return n>0?n:fallback;}
    private static string DirectoryName(string path){return "Assets/LevelSets/test/data/level";}
    private static void ReloadPseudo(){}
    __METHODS__
    public static PerPlayerConfigDto Read(LevelConfigSetupPerPlayerCountSO c){return ConfigToDto(c);}
}
__DTOS__
class Checks {
    const string A="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",B="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",Bad="cccccccccccccccccccccccccccccccc";
    const string InfoPath="Assets/LevelSets/test/data/level/info.asset";
    static ScriptableObject a,b;
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;}
    static LevelInfoSO Fresh(){
        var info=new LevelInfoSO{path=InfoPath,enableRespawnBounds=true,recipes=new[]{a,b}};
        var configs=new LevelConfigSetupPerPlayerCountSO[4];
        for(int p=0;p<4;p++)configs[p]=new LevelConfigSetupPerPlayerCountSO{path="Assets/LevelSets/test/data/level/c"+p,roundTime=240,scriptedRecipes=new[]{a}};
        info.config_1p=configs[0];info.config_2p=configs[1];info.config_3p=configs[2];info.config_4p=configs[3];
        AssetDatabase.assets["info"]=info;LayoutEditorLevelAdminApi.Saved=null;
        Directory.CreateDirectory("Assets/LevelSets/test/data/level");
        Directory.CreateDirectory("Assets/LevelSets/test/custom_recipes");
        for(int p=0;p<4;p++){configs[p].path+=".asset";AssetDatabase.assets["config"+p]=configs[p];}
        AssetDatabase.assets["optional"]=new LevelConfigSetupPerPlayerCountSO{path="Assets/LevelSets/test/data/level/optional.asset",roundTime=17};
        AssetDatabase.assets["counter"]=new LevelConfigSetupPerPlayerCountSO{path="Assets/LevelSets/test/custom_recipes/CustomRecipeConfig.asset",roundTime=41};
        File.WriteAllText("Assets/LevelSets/test/data/level/names.json","oldnames");
        File.WriteAllText("Assets/LevelSets/test/custom_recipes/names.json","oldsetnames");
        foreach(var entry in AssetDatabase.assets)if(entry.Value.path.EndsWith(".asset"))File.WriteAllText(entry.Value.path,"before-"+entry.Key);
        if(Directory.Exists("Assets/LevelSets/test/data/level/generated"))Directory.Delete("Assets/LevelSets/test/data/level/generated",true);
        Undo.writes=0;AssetDatabase.saves=0;AssetDatabase.FailSave=false;LayoutEditorLevelModeStore.FailSync=false;LayoutEditorLevelModeStore.Syncs=0;
        return info;
    }
    static LevelConfigUpdateDto Request(){return new LevelConfigUpdateDto{assetPath=InfoPath};}
    static OrderSequenceConfigDto Web(){return new OrderSequenceConfigDto{enabled=true,endBehavior="loop",groups=new[]{new OrderSequenceGroupDto{recipeGuids=new[]{A}}}};}
    static void Main(){
        a=new PseudoPrefabSORecipe{path="a"};b=new CustomRecipeSO{path="b"};
        AssetDatabase.assets[A]=a;AssetDatabase.assets[B]=b;
        var info=Fresh();var request=Request();
        request.config_1p=new PerPlayerConfigDto{roundTime=300};
        Check(LayoutEditorLevelAdminApi.UpdateLevelConfig(request)==null,"legacy timing accepted");
        Check(info.config_1p.scriptedRecipes[0]==a,"omitted sequence preserved");
        info=Fresh();request=Request();request.config_1p=new PerPlayerConfigDto{scriptedRecipes=new[]{B,A,B},roundTime=300};
        Check(LayoutEditorLevelAdminApi.UpdateLevelConfig(request)==null,"duplicate sequence accepted");
        Check(info.config_1p.scriptedRecipes.Length==3 && info.config_1p.scriptedRecipes[0]==b && info.config_1p.scriptedRecipes[2]==b,"duplicates and ordering preserved");
        Check(LayoutEditorLevelModeStore.Syncs==1 && info.dependencies[0]=="bundle18","complete shared SyncRecipeData used once");
        var read=LayoutEditorLevelAdminApi.Read(info.config_1p);
        Check(string.Join(",",read.scriptedRecipes)==B+","+A+","+B,"GET exact order");
        info=Fresh();request=Request();request.config_1p=new PerPlayerConfigDto{scriptedRecipes=new[]{B},roundTime=999};
        request.config_4p=new PerPlayerConfigDto{scriptedRecipes=new[]{Bad}};
        Check(LayoutEditorLevelAdminApi.UpdateLevelConfig(request)!=null,"bad 4P rejects entire request");
        Check(Undo.writes==0 && AssetDatabase.saves==0 && info.config_1p.roundTime==240 && info.config_1p.scriptedRecipes[0]==a,"bad 4P causes zero mutation");
        foreach(var bad in new[]{"", "x",new string('z',32),Bad}){
            ScriptableObject[] resolved;Check(LayoutEditorOrderSequencePolicy.Resolve(new[]{bad},out resolved)!=null,"invalid GUID rejected");
        }
        AssetDatabase.assets[Bad]=new ScriptableObject{path="notrecipe"};
        ScriptableObject[] invalid;Check(LayoutEditorOrderSequencePolicy.Resolve(new[]{Bad},out invalid)!=null,"wrong asset type rejected");
        info=Fresh();info.config_1p.scriptedRecipes=new[]{a,null,b};read=LayoutEditorLevelAdminApi.Read(info.config_1p);
        Check(read.scriptedRecipes.Length==3 && read.scriptedRecipes[1]=="" && read.scriptedRecipeErrors.Length==1,"GET keeps broken slot and diagnostic");
        info=Fresh();info.levelType=LevelInfoSO.LevelType.Dynamic;request=Request();request.config_1p=new PerPlayerConfigDto{scriptedRecipes=new string[0]};
        Check(LayoutEditorLevelAdminApi.UpdateLevelConfig(request)!=null && Undo.writes==0,"Dynamic explicit clear rejected");
        request.config_1p.scriptedRecipes=null;
        Check(LayoutEditorLevelAdminApi.UpdateLevelConfig(request)==null && info.config_1p.scriptedRecipes[0]==a,"Dynamic omitted preserves");
        info=Fresh();LayoutEditorLevelAdminApi.Saved=Web();request=Request();request.config_1p=new PerPlayerConfigDto{scriptedRecipes=new[]{B}};
        Check(LayoutEditorLevelAdminApi.UpdateLevelConfig(request)!=null && Undo.writes==0,"native write blocked by Web");
        request.config_1p=new PerPlayerConfigDto{scriptedRecipes=new string[0]};
        Check(LayoutEditorLevelAdminApi.UpdateLevelConfig(request)!=null && Undo.writes==0,"clear only 1P still conflicts with remaining players");
        request.config_2p=new PerPlayerConfigDto{scriptedRecipes=new string[0]};request.config_3p=new PerPlayerConfigDto{scriptedRecipes=new string[0]};request.config_4p=new PerPlayerConfigDto{scriptedRecipes=new string[0]};
        Check(LayoutEditorLevelAdminApi.UpdateLevelConfig(request)==null,"clear all players resolves conflict atomically");
        Check(info.config_1p.scriptedRecipes.Length==0 && info.config_4p.scriptedRecipes.Length==0,"[] clears");
        info=Fresh();JsonUtility.Next=Web();LayoutEditorLevelAdminApi.Written=null;
        Check(LayoutEditorLevelAdminApi.SaveLevelOrderSequence(InfoPath,"{}")!=null && LayoutEditorLevelAdminApi.Written==null,"Web write blocked by native");
        ((OrderSequenceConfigDto)JsonUtility.Next).enabled=false;
        Check(LayoutEditorLevelAdminApi.SaveLevelOrderSequence(InfoPath,"{}") ==null && info.config_1p.scriptedRecipes.Length==1,"disabled Web preserves native");
        Check(LayoutEditorLevelAdminApi.SaveLevelOrderSequence(InfoPath,"")!=null,"empty Web JSON rejected");
        info=Fresh();info.config_1p.scriptedRecipes=new ScriptableObject[0];info.config_2p.scriptedRecipes=new ScriptableObject[0];info.config_3p.scriptedRecipes=new ScriptableObject[0];info.config_4p.scriptedRecipes=new ScriptableObject[0];
        Check(LayoutEditorOrderSequencePolicy.Validate(info,Web(),null)==null,"Web with empty native accepted");
        info.recipes=new ScriptableObject[0];Check(LayoutEditorOrderSequencePolicy.Validate(info,Web(),null)!=null,"Web missing active menu reference rejected");
        info=Fresh();var update=new LevelInfoUpdateDto{assetPath=InfoPath};
        Check(LayoutEditorLevelAdminApi.UpdateLevelInfo(update)==null && info.enableRespawnBounds,"legacy info request preserves respawn bounds");
        update.updateEnableRespawnBounds=true;Check(LayoutEditorLevelAdminApi.UpdateLevelInfo(update)==null && !info.enableRespawnBounds,"explicit false writes");
        update.enableRespawnBounds=true;Check(LayoutEditorLevelAdminApi.UpdateLevelInfo(update)==null && info.enableRespawnBounds,"explicit true writes");
        foreach(var failSync in new[]{true,false}){
            info=Fresh();info.debugRecipeCount=23;info.dependencies=new[]{"bundle1"};
            var managerRoot=new GameObject();managerRoot.components.Add(new PseudoPrefabManagerStub{levelInfo=info,gameObject=managerRoot});
            var consumerRoot=new GameObject();var consumer=new Component{data=31,gameObject=consumerRoot};consumerRoot.components.Add(consumer);
            LayoutEditorLevelModeStore.Consumer=consumer;
            UnityEngine.SceneManagement.SceneManager.scenes=new[]{new UnityEngine.SceneManagement.Scene{valid=true,isLoaded=true,roots=new[]{managerRoot,consumerRoot}}};
            request=Request();request.config_1p=new PerPlayerConfigDto{roundTime=600,scriptedRecipes=new[]{B}};
            request.config_4p=new PerPlayerConfigDto{roundTime=900,scriptedRecipes=new[]{A,B}};
            LayoutEditorLevelModeStore.FailSync=failSync;AssetDatabase.FailSave=!failSync;
            var error=LayoutEditorLevelAdminApi.UpdateLevelConfig(request);
            Check(error!=null && error.Contains("已恢复"),"failed sync/save reports restoration");
            Check(error.Contains(failSync ? "injected sync failure" : "injected final save failure"),"original write error survives rollback and is returned to caller");
            Check(info.config_1p.roundTime==240 && info.config_4p.roundTime==240 && info.config_1p.scriptedRecipes[0]==a && info.config_4p.scriptedRecipes.Length==1,"all player values and references restored");
            Check(info.debugRecipeCount==23 && info.dependencies[0]=="bundle1","LevelInfo unsaved memory restored");
            Check(((LevelConfigSetupPerPlayerCountSO)AssetDatabase.assets["optional"]).roundTime==17 && ((LevelConfigSetupPerPlayerCountSO)AssetDatabase.assets["counter"]).roundTime==41,"derived Optional and UID counter restored");
            Check(File.ReadAllText(InfoPath)=="before-info" && File.ReadAllText(info.config_1p.path)=="before-config0","disk bytes restored independently of dirty memory");
            Check(File.ReadAllText("Assets/LevelSets/test/custom_recipes/names.json")=="oldsetnames" && File.ReadAllText("Assets/LevelSets/test/data/level/names.json")=="oldnames","name sidecars restored");
            Check(!Directory.Exists("Assets/LevelSets/test/data/level/generated"),"new derived asset and directory removed");
            Check(consumer.data==31 && consumerRoot.components.Count==1,"existing scene consumer and new component restored, even on a separate root from manager");
            LayoutEditorLevelModeStore.Consumer=null;UnityEngine.SceneManagement.SceneManager.scenes=new UnityEngine.SceneManagement.Scene[0];
        }
        info=Fresh();((PseudoPrefabSORecipe)a).asset=null;
        var beforeWrites=Undo.writes;
        try {LayoutEditorOrderSequenceBake.Prepare(info);Check(false,"unresolvable native UID must fail early");}
        catch(InvalidOperationException){Check(Undo.writes==beforeWrites,"native UID failure occurs before scene mutation");}
        ((PseudoPrefabSORecipe)a).asset=new OrderDefinitionNode{m_uID=123};((CustomRecipeSO)b).uID=456;
        Check(LayoutEditorOrderSequenceBake.Prepare(info)!=null,"native direct UID preflight succeeds");
        foreach(var config in LayoutEditorOrderSequencePolicy.Configs(info))config.scriptedRecipes=new ScriptableObject[0];
        LayoutEditorLevelAdminApi.Saved=Web();LayoutEditorLevelAdminApi.Saved.groups[0].recipeGuids=new[]{A,B,A};
        var prepared=LayoutEditorOrderSequenceBake.Prepare(info);
        Check(string.Join(",",Array.ConvertAll(prepared.uids,delegate(int uid){return uid.ToString();}))=="123,456,123","prepared Web retains duplicate UID order");
        ((PseudoPrefabSORecipe)a).asset=null;
        var resolves=RecipeHelper.Resolves;var reads=LayoutEditorLevelAdminApi.SequenceReads;
        LayoutEditorOrderSequenceBake.ApplyPreparedForOpenScene(info,prepared);
        Check(resolves==RecipeHelper.Resolves && reads==LayoutEditorLevelAdminApi.SequenceReads,"late bake never resolves UID or reloads authoring JSON");
        try {LayoutEditorOrderSequenceBake.Prepare(info);Check(false,"unresolvable Web UID must fail early");}
        catch(InvalidOperationException){Check(true,"Web missing UID rejected by prepare");}
        Console.WriteLine("PASS "+count+" production C#4 assertions");
    }
}
'''


def main():
    admin = (EDITOR / 'LayoutEditorLevelAdminApi.cs').read_text(encoding='utf-8-sig')
    models = (EDITOR / 'LayoutEditorModels.cs').read_text(encoding='utf-8-sig')
    methods = '\n'.join(block(admin, r'    (?:public|private) static [^\n]+\b' + name + r'\(') for name in (
        'UpdateLevelConfig', 'ApplyConfig', 'ConfigToDto', 'UpdateLevelInfo', 'SaveLevelOrderSequence', 'ValidateOrderSequenceConfig'))
    dtos = '\n'.join(block(models, r'public class ' + name + r'\b') for name in (
        'PerPlayerConfigDto', 'LevelConfigUpdateDto', 'LevelInfoUpdateDto', 'OrderSequenceConfigDto', 'OrderSequenceGroupDto'))
    applier = (EDITOR / 'SceneLayoutApplier.cs').read_text()
    exporter = (EDITOR / 'LayoutEditorSetExporter.cs').read_text()
    assert applier.index('LayoutEditorOrderSequenceBake.Prepare(nativeInfo)') < applier.index('PrepareSceneForApply()') < applier.index('RemoveUnmatchedSceneItems(before,')
    assert 'ApplyPreparedForOpenScene(steamerLevelInfo, preparedOrders)' in applier
    assert 'return "订单顺序烘焙失败，未保存场景；内存布局可能已改动，请重新加载核实：" + e.Message;' in applier
    prepare_index = exporter.index('LayoutEditorOrderSequenceBake.Prepare(modeInfo)')
    assert prepare_index < exporter.index('ForcePrepareForExportStrict(true)', prepare_index) < exporter.index('var orderSeqWarn = LayoutEditorOrderSequenceBake.ApplyPreparedForOpenScene(orderInfo, preparedOrders)')
    assert 'throw new Exception("订单顺序烘焙失败，已停止导出：" + e.Message, e);' in exporter
    with tempfile.TemporaryDirectory(prefix='oc2-native-scripted-') as temp:
        out = Path(temp)
        (out / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (out / 'Program.cs').write_text(HARNESS.replace('__METHODS__', methods).replace('__DTOS__', dtos))
        (out / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo>'
            '<NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><Compile Include="'
            + escape(str(EDITOR / 'LayoutEditorOrderSequencePolicy.cs')) + '" /><Compile Include="'
            + escape(str(EDITOR / 'LayoutEditorConfigTransaction.cs')) + '" /><Compile Include="'
            + escape(str(EDITOR / 'LayoutEditorOrderSequenceBake.cs')) + '" /></ItemGroup></Project>')
        subprocess.run(['dotnet', 'run', '--project', str(out / 'Checks.csproj')], cwd=out,
                       env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1'), check=True, timeout=120)
    print('PASS write-back/export guard placement; no live Unity serialization or Play executed.')


if __name__ == '__main__':
    main()
