#!/usr/bin/env python3
"""Execute production template/sidecar guards without opening Unity or project scenes.

Checks real template bindings, part-level separation, symlink refusal and write
ordering. The full scene transaction requires Unity integration validation.
"""
from pathlib import Path
import os
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Assets/Editor/LayoutEditor/LayoutEditorLevelModeChange.cs'


def extract(source, name):
    match = re.search(r'    private static [^\n]+\b' + name + r'\(', source)
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
using System.Reflection;
using System.Text.RegularExpressions;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using LevelEditorStub;
using GameModes.Horde;
public static class Application { public static string dataPath; }
namespace UnityEngine {
    public class Object { public static int Destroyed;public static void DestroyImmediate(Object o){Destroyed++;} }
    public class ScriptableObject:Object { public static T CreateInstance<T>() where T:new(){return new T();} }
    public class Component:Object {
        public GameObject gameObject;
        public T GetComponent<T>() where T:class { return gameObject.GetComponent<T>(); }
        public T GetComponentInParent<T>() where T:class { return gameObject.GetComponent<T>(); }
    }
    public class GameObject : Object {
        public string name="fixture";public bool activeInHierarchy=true;public List<Component> Components=new List<Component>();
        public T Add<T>(T c) where T:Component { c.gameObject=this;Components.Add(c);return c; }
        public T GetComponent<T>() where T:class {foreach(var c in Components)if(c is T)return c as T;return null;}
        public T[] GetComponentsInChildren<T>(bool inactive) where T:class {var r=new List<T>();foreach(var c in Components)if(c is T)r.Add(c as T);return r.ToArray();}
        public T GetComponentInChildren<T>(bool inactive) where T:class {return GetComponent<T>();}
    }
    public static class JsonUtility {
        public static T FromJson<T>(string s) { return System.Text.Json.JsonSerializer.Deserialize<T>(s,
            new System.Text.Json.JsonSerializerOptions { IncludeFields=true }); }
    }
}
namespace UnityEngine.SceneManagement { public struct Scene { public GameObject[] Roots;public GameObject[] GetRootGameObjects(){return Roots;} } }
namespace LevelEditor { public class PseudoPrefab:Component { } }
namespace LevelEditorStub {
    public class PseudoPrefabSORecipe:ScriptableObject { }
    public class CustomRecipeSO:ScriptableObject { }
    public class LevelConfigSetupPerPlayerCountSO:ScriptableObject { public ScriptableObject[] scriptedRecipes; }
    public class LevelInfoSO:ScriptableObject {
        public enum LevelType { Campaign,Dynamic,Horde }
        public LevelType levelType;public bool enableRespawnBounds=true;
        public ScriptableObject[] recipes;public int[] roundTimes;
        public LevelConfigSetupPerPlayerCountSO config_1p,config_2p,config_3p,config_4p;
    }
    public class PseudoPrefabSO:UnityEngine.Object { public string prefabName,bundleName,assetPath;public UnityEngine.Object asset; }
    public class PseudoPrefabServingStationStub:Component { public PseudoPrefabSO pseudoPrefabSO; }
    public class PseudoPrefabServingStationHordeStub:PseudoPrefabServingStationStub { }
    public class PseudoPrefabLockableStub:Component { }
}
namespace GameModes.Horde { public class HordeTarget:Component { }public class HordeLockable:Component { } }
public class PlateStation:Component { }public class Interactable:Component { }
public class FlowControllerBase {
    public Dictionary<string,SerializedProperty> Fields=new Dictionary<string,SerializedProperty>();
}
public class CampaignFlowController:FlowControllerBase { }
public class DynamicFlowController:CampaignFlowController { }
public class HordeFlowController:FlowControllerBase { }
public class RecipeFlowGUI:UnityEngine.Object { }
namespace UnityEditor {
    public static class AssetDatabase {
        public static Dictionary<string,UnityEngine.Object> Assets=new Dictionary<string,UnityEngine.Object>();
        public static T LoadAssetAtPath<T>(string p) where T:UnityEngine.Object { UnityEngine.Object o;return Assets.TryGetValue(p,out o)?o as T:null; }
        public static string GetAssetPath(UnityEngine.Object o){foreach(var pair in Assets)if(pair.Value==o)return pair.Key;return "";}
        public static string AssetPathToGUID(string p){return p=="recipe"?new string('a',32):"";}
        public static string GUIDToAssetPath(string g){return g==new string('a',32)?"recipe":"";}
    }
    public class SerializedProperty {
        public string propertyPath;public int intValue; public UnityEngine.Object objectReferenceValue;
        public List<SerializedProperty> Elements=new List<SerializedProperty>();
        public int arraySize { get{return Elements.Count;}set{Elements.Clear();for(int i=0;i<value;i++)Elements.Add(new SerializedProperty());} }
        public SerializedProperty GetArrayElementAtIndex(int i){return Elements[i];}
        public SerializedProperty Clone(){return new SerializedProperty { propertyPath=propertyPath,intValue=intValue,objectReferenceValue=objectReferenceValue,Elements=new List<SerializedProperty>(Elements) };}
    }
    public class SerializedObject {
        FlowControllerBase target;public SerializedObject(FlowControllerBase o){target=o;}
        public SerializedProperty FindProperty(string name){SerializedProperty p;return target.Fields.TryGetValue(name,out p)?p:null;}
        public void CopyFromSerializedProperty(SerializedProperty p){target.Fields[p.propertyPath]=p.Clone();}
        public void ApplyModifiedProperties(){}
    }
}
public class PartLevelDto { public bool enabled; }
public class PartLevelStoreDto { public PartLevelDto partLevel; }
public class OrderSequenceConfigDto { public bool enabled;public OrderSequenceGroupDto[] groups; }
public class OrderSequenceGroupDto { public string[] recipeGuids; }
public static class LayoutEditorLevelAdminApi {
    public static OrderSequenceConfigDto Saved;public static bool FailRead;public static string ReadPath;
    public static OrderSequenceConfigDto LoadLevelOrderSequence(string p){ReadPath=p;if(FailRead)throw new InvalidDataException("unreadable sequence");return Saved;}
}
public static class LayoutEditorLevelModeStore {
    public static ScriptableObject[] CollectAllRecipes(LevelInfoSO info){return info.recipes;}
}
static class Checks {
__METHODS__
    static int count;
    static void Check(bool value,string name) { count++;if(!value)throw new Exception(name); }
    static void Reject(Action action,string name) { bool rejected=false;try{action();}catch(Exception){rejected=true;}Check(rejected,name); }
    static void Field(FlowControllerBase flow,string name,int value){flow.Fields.Add(name,new SerializedProperty{propertyPath=name,intValue=value});}
    static void Main(string[] args) {
        string root=args[0],temp=args[1];
        var expected=new Dictionary<string,string>();expected.Add("campaign","0c70fdc0bdf8b644e82e3ebc3f7a8140");
        expected.Add("dynamic","99bb5a5f765b6ec45a0f25fe04896f61");expected.Add("horde","b2cd1545740ed1543855a0c8468b1896");
        foreach(var pair in expected) {
            string file=pair.Key=="campaign"?"s_template.unity":"s_template_"+pair.Key+".unity";
            string text=File.ReadAllText(Path.Combine(root,"Assets/Template/"+pair.Key+"/"+file));
            string manager=ReadTemplateManager(text);
            Check(manager.Contains("levelInfo:"),"manager block");Check(manager.Contains("configTemplateSO:"),"config reference");
            Check(TemplatePrefabGuid(text,manager)==pair.Value,"native environment reference "+pair.Key);
            Reject(delegate { ReadTemplateManager(text+"\n"+text); },"duplicate managers");
            Reject(delegate { TemplatePrefabGuid(text,manager.Replace("FlowManagerGO:","MissingFlow:")); },"missing flow binding");
        }
        string info="Assets/LevelSets/test/data/renamed/LevelInfo.asset",scene="Assets/LevelSets/test/scenes/s_one.unity";
        Application.dataPath=Path.Combine(temp,"Assets");Directory.CreateDirectory(Application.dataPath);
        RejectPartLevel(info,scene);Check(true,"no sidecar allowed");
        string own=Absolute("Assets/LevelSets/test/data/renamed/part_level~/part_level.json");
        string legacy=Absolute("Assets/LevelSets/test/data/one/part_level~/part_level.json");
        Directory.CreateDirectory(Path.GetDirectoryName(own));Directory.CreateDirectory(Path.GetDirectoryName(legacy));
        File.WriteAllText(own,"{\"partLevel\":{\"enabled\":true}}");
        Reject(delegate{RejectPartLevel(info,scene);},"info-relative enabled partLevel");
        File.WriteAllText(own,"{\"partLevel\":{\"enabled\":false}}");
        File.WriteAllText(legacy,"{\"partLevel\":{\"enabled\":true}}");
        Reject(delegate{RejectPartLevel(info,scene);},"legacy scene-relative enabled partLevel");
        File.WriteAllText(legacy,"{\"partLevel\":{\"enabled\":false}}");RejectPartLevel(info,scene);Check(true,"disabled partLevel allowed");
        File.WriteAllText(own,"bad-json");Reject(delegate{RejectPartLevel(info,scene);},"invalid JSON fails closed");
        File.WriteAllText(own,new string('x',512*1024+1));Reject(delegate{RejectPartLevel(info,scene);},"oversize JSON fails closed");
        File.Delete(own);File.CreateSymbolicLink(own,legacy);Reject(delegate{RejectPartLevel(info,scene);},"linked sidecar fails closed");
        using(var pe=new PEReader(File.OpenRead(args[2]))) {
            var metadata=pe.GetMetadataReader();bool found=false;
            foreach(var handle in metadata.TypeDefinitions) {
                var type=metadata.GetTypeDefinition(handle);
                if(metadata.GetString(type.Name)!="EditorUtility")continue;
                foreach(var mh in type.GetMethods())if(metadata.GetString(metadata.GetMethodDefinition(mh).Name)=="IsDirty")found=true;
            }
            Check(found,"Unity2017 EditorUtility.IsDirty exists for reflection guard");
        }
        var dynamicSource=new DynamicFlowController();var dynamicTarget=new DynamicFlowController();var ui=new RecipeFlowGUI();
        foreach(var name in new[]{"m_gameConfig","m_teamMonitor","m_maxOrdersAllowed","m_transitions"})Field(dynamicSource,name,9);
        Field(dynamicTarget,"m_teamMonitor.m_recipeBarUIController",0);Field(dynamicSource,"m_Script",99);Field(dynamicTarget,"m_Script",3);
        dynamicSource.Fields["m_transitions"].arraySize=4;var t1=new GameObject();var t2=new GameObject();
        CopyFlow(dynamicSource,dynamicTarget,ui,new[]{t1,t2},7);
        Check(dynamicTarget.Fields["m_Script"].intValue==3,"never copy m_Script");
        Check(dynamicTarget.Fields["m_maxOrdersAllowed"].intValue==7,"order count uses target LevelInfo");
        Check(dynamicTarget.Fields["m_teamMonitor.m_recipeBarUIController"].objectReferenceValue==ui,"bind local RecipeUI");
        Check(dynamicTarget.Fields["m_transitions"].arraySize==2,"replace template transition count");
        Check(dynamicTarget.Fields["m_transitions"].GetArrayElementAtIndex(0).objectReferenceValue==t1,"local transition one");
        Check(dynamicTarget.Fields["m_transitions"].GetArrayElementAtIndex(1).objectReferenceValue==t2,"local transition two");
        Check(dynamicSource.Fields["m_transitions"].arraySize==4,"template transition array unchanged");
        var hordeSource=new HordeFlowController();var hordeTarget=new HordeFlowController();
        foreach(var name in new[]{"m_gameConfig","m_waveNumberUIPrefab","m_waveNumberUIDelay","m_waveNumberUILocalisationTag","m_uiPrefab"})Field(hordeSource,name,5);
        CopyFlow(hordeSource,hordeTarget,null,null,0);
        Check(hordeTarget.Fields.Count==5,"copy exactly horde whitelist");
        Check(hordeTarget.Fields["m_waveNumberUIDelay"].intValue==5,"preserve native horde delay");
        var targetObject=new GameObject();var targetStub=targetObject.Add(new PseudoPrefabServingStationHordeStub { pseudoPrefabSO=new PseudoPrefabSO() });
        var sceneWithTarget=new Scene { Roots=new[]{targetObject} };
        ValidateHordeObjects(sceneWithTarget,"Horde");Check(true,"horde target stub accepted");
        Reject(delegate{ValidateHordeObjects(new Scene{Roots=new GameObject[0]},"Horde");},"horde requires target");
        targetObject.activeInHierarchy=false;Reject(delegate{ValidateHordeObjects(sceneWithTarget,"Horde");},"inactive horde target insufficient");targetObject.activeInHierarchy=true;
        Reject(delegate{ValidateHordeObjects(sceneWithTarget,"Campaign");},"cannot retain horde stub in campaign");
        targetStub.pseudoPrefabSO=null;Reject(delegate{ValidateHordeObjects(sceneWithTarget,"Horde");},"horde stub must have SO");targetStub.pseudoPrefabSO=new PseudoPrefabSO();
        var normal=new GameObject();normal.Add(new PseudoPrefabServingStationStub());
        Reject(delegate{ValidateHordeObjects(new Scene{Roots=new[]{targetObject,normal}},"Horde");},"normal delivery stub cannot mix with horde");
        var raw=new GameObject();raw.Add(new PlateStation());
        Reject(delegate{ValidateHordeObjects(new Scene{Roots=new[]{targetObject,raw}},"Horde");},"plain PlateStation cannot mix with horde");
        raw.Add(new HordeTarget());Reject(delegate{ValidateHordeObjects(new Scene{Roots=new[]{raw}},"Horde");},"target requires Interactable");
        raw.Add(new Interactable());ValidateHordeObjects(new Scene{Roots=new[]{raw}},"Horde");Check(true,"complete native HordeTarget accepted");
        var locked=new GameObject();locked.Add(new HordeLockable());Reject(delegate{ValidateHordeObjects(new Scene{Roots=new[]{locked}},"Dynamic");},"lockable cannot survive switch out");
        ValidateHordeObjects(new Scene{Roots=new[]{normal}},"Campaign");Check(true,"normal campaign scene accepted");
        const string normalPath="Assets/common01/prefabs/counters/ServingStation.prefab";
        const string hordePath="Assets/common04/game_environment/horde/prefabs/Service_Window.prefab";
        var normalSO=new PseudoPrefabSO{prefabName="station",bundleName="common01",assetPath="station"};
        var hordeSO=new PseudoPrefabSO{prefabName="window",bundleName="bundle40",assetPath="window"};
        AssetDatabase.Assets.Add("Assets/common01/pseudo_prefab_so/counters/ServingStationSO.asset",normalSO);
        AssetDatabase.Assets.Add("Assets/common04/game_environment/horde/prefabs/Service_Window.asset",hordeSO);
        var normalPrefab=new GameObject();normalPrefab.Add(new PseudoPrefabServingStationStub{pseudoPrefabSO=normalSO});
        var hordePrefab=new GameObject();hordePrefab.Add(new PseudoPrefabServingStationHordeStub{pseudoPrefabSO=hordeSO});
        AssetDatabase.Assets.Add(normalPath,normalPrefab);AssetDatabase.Assets.Add(hordePath,hordePrefab);
        var toHorde=new GameObject();toHorde.Add(new PseudoPrefabServingStationHordeStub{pseudoPrefabSO=new PseudoPrefabSO{prefabName="window",bundleName="bundle40",assetPath="window"}});
        var toNormal=new GameObject();toNormal.Add(new PseudoPrefabServingStationStub{pseudoPrefabSO=new PseudoPrefabSO{prefabName="station",bundleName="common01",assetPath="station"}});
        for(int i=0;i<20;i++){
            Check(ResolveStationPrefab(toHorde,normalPrefab)==hordePrefab,"ordinary to Horde exports target identity");
            Check(ResolveStationPrefab(toNormal,hordePrefab)==normalPrefab,"Horde to ordinary exports target identity");
            Check(ResolveStationPrefab(toHorde,hordePrefab)==hordePrefab,"Horde identity stable");
        }
        Check(ResolveStationPrefab(new GameObject(),normalPrefab)==normalPrefab,"unrelated prefab unaffected");
        var custom=new GameObject();custom.Add(new PseudoPrefabServingStationStub{pseudoPrefabSO=new PseudoPrefabSO{prefabName="custom"}});
        Check(ResolveStationPrefab(custom,hordePrefab)==hordePrefab,"custom station untouched");
        AssetDatabase.Assets.Remove(hordePath);
        Reject(delegate{ResolveStationPrefab(toHorde,normalPrefab);},"missing target prefab fails instead of exporting old identity");
        var recipe=new PseudoPrefabSORecipe();AssetDatabase.Assets.Add("recipe",recipe);
        foreach(LevelInfoSO.LevelType original in Enum.GetValues(typeof(LevelInfoSO.LevelType)))
        foreach(var targetMode in new[]{"Campaign","Dynamic","Horde"})
        foreach(var webEnabled in new[]{false,true})
        foreach(var native in new[]{0,1,2}){
            var sequence=native==0?new ScriptableObject[0]:new ScriptableObject[]{native==1?recipe:null};
            var player=new LevelConfigSetupPerPlayerCountSO{scriptedRecipes=sequence};
            var value=new LevelInfoSO{levelType=original,recipes=new[]{recipe},roundTimes=new[]{17,29},
                config_1p=player,config_2p=player,config_3p=player,config_4p=player};
            var recipes=value.recipes;var times=value.roundTimes;
            var web=new OrderSequenceConfigDto{enabled=webEnabled,groups=new[]{new OrderSequenceGroupDto{recipeGuids=new[]{new string('a',32)}}}};
            LayoutEditorLevelAdminApi.Saved=web;
            bool rejected=false;string message="";int destroyed=UnityEngine.Object.Destroyed;
            try{ValidateTargetOrderSequence(info,value,targetMode);}catch(InvalidDataException ex){rejected=true;message=ex.Message;}
            bool shouldReject=webEnabled&&(native!=0||targetMode!="Campaign")||native==2&&targetMode=="Campaign";
            Check(rejected==shouldReject,"sequence policy projects "+original+" -> "+targetMode+" web="+webEnabled+" native="+native);
            Check(value.levelType==original&&value.enableRespawnBounds&&value.recipes==recipes&&value.roundTimes==times
                &&value.config_1p==player&&value.config_4p==player&&player.scriptedRecipes==sequence,"preflight preserves original fields and all sequence arrays");
            Check(web.enabled==webEnabled&&web.groups[0].recipeGuids.Length==1,"preflight never disables or deletes Web authoring");
            Check(UnityEngine.Object.Destroyed==destroyed+1,"projection disposed on success and rejection");
            Check(LayoutEditorLevelAdminApi.ReadPath==info,"sidecar uses original asset path, not transient projection");
            if(webEnabled&&shouldReject)Check(message.Contains("显式"),"conflict requires explicit resolution");
        }
        LayoutEditorLevelAdminApi.FailRead=true;
        Reject(delegate{ValidateTargetOrderSequence(info,new LevelInfoSO(),"Dynamic");},"unreadable Web sequence rejects before mutation");
        LayoutEditorLevelAdminApi.FailRead=false;
        Console.WriteLine("PASS "+count+" production template/sidecar guard checks; no Unity scene writes.");
    }
}
'''


def main():
    source = SOURCE.read_text(encoding='utf-8-sig')
    # These are ordering assertions, not simulated scene/Undo execution.
    transaction = source[source.index('public static string ChangeLevelMode'):source.index('private static Type FlowType')]
    stage = transaction.index('Undo.IncrementCurrentGroup()')
    assert transaction.index('ValidateTargetOrderSequence(') < transaction.index('RejectDirtyAsset(')
    assert transaction.index('ValidateTargetOrderSequence(') < transaction.index('BuildModeData(')
    assert transaction.index('ValidateTargetOrderSequence(') < transaction.index('PrepareSceneForWrite(')
    assert transaction.index('RejectPartLevel(') < stage
    assert transaction.index('ValidateReferencesToFlow(') < stage
    assert transaction.index('FindTransitions(') < stage
    assert transaction.index('ValidateChanged(') < transaction.index('AssetDatabase.SaveAssets()')
    assert transaction.index('ValidateChanged(') < transaction.index('EditorSceneManager.SaveScene(')
    assert 'Undo.RevertAllDownToGroup(group)' in transaction
    assert 'File.WriteAllBytes(Absolute(scenePath), sceneBytes)' in transaction
    assert 'File.WriteAllBytes(Absolute(dto.assetPath), infoBytes)' in transaction
    assert 'EditorSceneManager.RestoreSceneManagerSetup(previousSetup)' in transaction
    assert 'Instantiate(template.root)' not in source
    assert 'info.enableRespawnBounds =' not in transaction, 'mode changes must preserve authored respawn bounds'
    assert transaction.index('info.enableRespawnBounds != backup.enableRespawnBounds') < transaction.index('AssetDatabase.SaveAssets()')
    conversions = extract(source, 'ApplyStationConversions')
    assert conversions.index('Undo.RegisterFullObjectHierarchyUndo') < conversions.index('Undo.DestroyObjectImmediate')
    assert 'PrefabUtility.ConnectGameObjectToPrefab' in conversions
    assert 'PrefabUtility.GetPrefabParent' in extract(source, 'ValidateStationConversions')
    methods = '\n'.join(extract(source, name) for name in
                        ('ReadTemplateManager', 'TemplatePrefabGuid', 'RejectPartLevel', 'RejectLinkPath', 'Absolute', 'CopyFlow', 'Require', 'ValidateHordeObjects', 'IsNativeStationSO', 'ValidateTargetOrderSequence'))
    methods += '\n' + extract(source.replace('internal static GameObject ResolveStationPrefab', 'private static GameObject ResolveStationPrefab'), 'ResolveStationPrefab')
    unity = Path('/Applications/Unity/Hub/Editor/2017.4.8f1/Unity.app/Contents/Managed/UnityEditor.dll')
    if not unity.is_file():
        unity = ROOT / 'layout-editor/offline-check/bin/standalone/UnityEditor.dll'
    with tempfile.TemporaryDirectory(prefix='oc2-mode-guards-') as temp:
        out = Path(temp)
        (out / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (out / 'Program.cs').write_text(HARNESS.replace('__METHODS__', methods), encoding='utf-8')
        project = out / 'Checks.csproj'
        project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                          '<TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion>'
                          '<GenerateAssemblyInfo>false</GenerateAssemblyInfo><NuGetAudit>false</NuGetAudit>'
                          '</PropertyGroup><ItemGroup><Compile Include="' + str(SOURCE.with_name('LayoutEditorOrderSequencePolicy.cs')) + '" /></ItemGroup></Project>')
        subprocess.run(['dotnet', 'run', '--project', str(project), '--', str(ROOT), temp, str(unity)],
                       cwd=out, env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1'), check=True, timeout=120)
    print('PASS transaction source-order checks. LIMIT: Undo, prefab overrides and Unity serialization need live integration QA.')


if __name__ == '__main__':
    main()
