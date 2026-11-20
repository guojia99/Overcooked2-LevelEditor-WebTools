#!/usr/bin/env python3
"""Run the production native-mode validator against Unity API doubles (C# 4).

No translated/simulated validation algorithm: the complete production .cs file
is compiled unchanged. Integration ordering is checked in the actual callers.
Unity serialization, bundle loading, Play and transaction rollback still need QA.
"""
import os
from pathlib import Path
import re
import subprocess
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
EDITOR = ROOT / 'Assets/Editor/LayoutEditor'


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
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using LevelEditorStub;
using GameModes.Horde;
namespace UnityEngine {
    public class Object { public string name; }
    public class Component:Object {
        public GameObject gameObject; public bool enabled=true;
        public Dictionary<string,SerializedProperty> fields=new Dictionary<string,SerializedProperty>();
        public T GetComponent<T>() where T:class {return gameObject.GetComponent<T>();}
        public T GetComponentInParent<T>() where T:class {return gameObject.GetComponent<T>();}
    }
    public class GameObject:Object {
        public Scene scene; public bool activeInHierarchy=true;
        public List<Component> components=new List<Component>();
        public List<GameObject> children=new List<GameObject>();
        public GameObject(Scene s){scene=s;}
        public T Add<T>(T c) where T:Component {c.gameObject=this;components.Add(c);return c;}
        public T GetComponent<T>() where T:class {foreach(var c in components)if(c is T)return c as T;return null;}
        public T[] GetComponents<T>() where T:class {var r=new List<T>();foreach(var c in components)if(c is T)r.Add(c as T);return r.ToArray();}
        public T[] GetComponentsInChildren<T>(bool inactive) where T:class {
            var r=new List<T>();foreach(var c in components)if(c is T || c==null)r.Add(c as T);
            foreach(var child in children)r.AddRange(child.GetComponentsInChildren<T>(inactive));return r.ToArray();
        }
        public T GetComponentInChildren<T>(bool inactive) where T:class {
            foreach(var c in GetComponentsInChildren<T>(inactive))if(c!=null)return c;return null;
        }
    }
    public class Transform:Component { }
    public class Collider:Component { }
    public class RuntimeAnimatorController:Object { }
    public class AnimatorOverrideController:RuntimeAnimatorController {public RuntimeAnimatorController runtimeAnimatorController;}
    public enum AnimatorControllerParameterType {Trigger,Bool,Float}
    public class AnimatorControllerParameter {public string name;public AnimatorControllerParameterType type;}
    public class Animator:Component {public RuntimeAnimatorController runtimeAnimatorController;}
    public class StateMachineBehaviour:Object { }
}
namespace UnityEngine.SceneManagement {
    public struct Scene {
        public int id;public string path;public bool isLoaded;public GameObject[] roots;
        public bool IsValid(){return id!=0;}
        public GameObject[] GetRootGameObjects(){return roots;}
        public static bool operator ==(Scene a,Scene b){return a.id==b.id;}
        public static bool operator !=(Scene a,Scene b){return a.id!=b.id;}
        public override bool Equals(object o){return o is Scene && ((Scene)o).id==id;}
        public override int GetHashCode(){return id;}
    }
    public static class SceneManager {
        public static Scene loaded;
        public static Scene GetSceneByPath(string path){return loaded.path==path?loaded:new Scene();}
    }
}
namespace UnityEditor {
    public class SerializedProperty {
        public UnityEngine.Object objectReferenceValue;public int objectReferenceInstanceIDValue;
        public string stringValue;public float floatValue;
    }
    public class SerializedObject {
        Component target;public SerializedObject(Component c){target=c;}
        public SerializedProperty FindProperty(string name){SerializedProperty p;return target.fields.TryGetValue(name,out p)?p:null;}
    }
    public static class AssetDatabase {public static string GetAssetPath(UnityEngine.Object o){return Checks.InfoPath;}}
}
namespace UnityEditor.Animations {
    public class AnimatorController:RuntimeAnimatorController {
        public AnimatorControllerLayer[] layers;public AnimatorControllerParameter[] parameters;
    }
    public class AnimatorControllerLayer {public AnimatorStateMachine stateMachine;}
    public class AnimatorStateMachine:UnityEngine.Object {
        public StateMachineBehaviour[] behaviours=new StateMachineBehaviour[0];
        public ChildAnimatorState[] states=new ChildAnimatorState[0];
        public ChildAnimatorStateMachine[] stateMachines=new ChildAnimatorStateMachine[0];
    }
    public class ChildAnimatorState {public AnimatorState state;}
    public class AnimatorState {public StateMachineBehaviour[] behaviours=new StateMachineBehaviour[0];}
    public class ChildAnimatorStateMachine {public AnimatorStateMachine stateMachine;}
}
namespace LevelEditorStub {
    public class PseudoPrefabSO:UnityEngine.Object {public UnityEngine.Object asset;public string assetPath,bundleName;}
    public class LevelInfoSO:UnityEngine.Object {
        public enum LevelType {Campaign,Dynamic,Horde}
        public LevelType levelType;public string sceneName="test";
        public UnityEngine.Object[] recipeListSOs;public int[] roundTimes;
        public Player config_1p=new Player(),config_2p=new Player(),config_3p=new Player(),config_4p=new Player();
    }
    public class Player { }
    public class PseudoPrefabManagerStub:Component {
        public LevelInfoSO levelInfo;
        public GameObject FlowManagerGO,AudioManagerGO,BootstrapManagerGO,PlayerSwitchingManagerGO,RecipeUIGO;
        public PseudoPrefabSO configTemplateSO,RecipeUISO,hordeModeUISO,hordeWaveNumberUISO;
    }
    public class PseudoPrefabServingStationStub:Component { }
    public class PseudoPrefabServingStationHordeStub:PseudoPrefabServingStationStub {public PseudoPrefabSO pseudoPrefabSO;}
    public class PseudoPrefabLockableStub:Component { }
}
namespace LevelEditor {
    public class PseudoPrefabManager:Component {public PseudoPrefabManagerStub stub;}
    public class PseudoPrefab:Component { }
}
public class FlowControllerBase:Component {public UnityEngine.Object m_gameConfig=new UnityEngine.Object();}
public class CampaignFlowController:FlowControllerBase {public Team m_teamMonitor;}
public class DynamicFlowController:CampaignFlowController {public GameObject[] m_transitions;}
public class Team {public RecipeFlowGUI m_recipeBarUIController;}
public class KitchenLevelConfigBase:UnityEngine.Object { }
public class LevelIntroFlowroutine:Component { }
public class CampaignAudioManager:Component { }
public class KitchenBootstrapManager:Component { }
public class PlayerSwitchingManager:Component { }
public class RecipeFlowGUI:Component { }
public class UIControllerBase:Component { }
public class T17Text:Component { }
public class DynamicTransitionBase:Component { }
public class AnimatedDynamicTransition:DynamicTransitionBase { }
public class BeachAnimatedDynamicTransition:AnimatedDynamicTransition { }
public class CampsiteAnimatedDynamicTransition:AnimatedDynamicTransition { }
public class OtherTransition:AnimatedDynamicTransition { }
public class RespawnCollider:Component { }
public class PlateStation:Component { }
public class Interactable:Component { }
namespace GameModes.Horde {
    public class HordeFlowController:FlowControllerBase { }
    public class HordeLevelConfig:UnityEngine.Object {public object m_flowroutineData;}
    public class HordeTarget:Component { }
    public class HordeLockable:Component { }
}
public static class LayoutEditorCatalogApi {public static bool BundleFileExists(string s){return s=="available";}}
public static class LayoutEditorLevelModeStore {public const int MaxPhases=64;}
public static class LayoutEditorLevelModeChange {
    public static bool part;
    internal static void ValidateNativePartLevel(string a,string b){if(part)throw new InvalidDataException("partLevel");}
    internal static void ValidateNativeHordeObjects(Scene scene){ValidateHordeObjects(scene,"Horde");}
__HORDE_GUARD__
}
static class Checks {
    public const string InfoPath="Assets/LevelSets/test/data/test/LevelInfo_test.asset";
    static int count;
    static void Check(bool ok,string label){count++;if(!ok)throw new Exception(label);}
    static void Reject(Action action,string label){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,label);}
    static void Bad(Scene scene,LevelInfoSO info,string label){Check(LayoutEditorNativeModeValidation.ValidateScene(scene,info)!=null,label);}
    static void Good(Scene scene,LevelInfoSO info,string label){string e=LayoutEditorNativeModeValidation.ValidateScene(scene,info);Check(e==null,label+": "+e);}
    static SerializedProperty Ref(UnityEngine.Object o){return new SerializedProperty{objectReferenceValue=o};}
    static Animator Anim(Scene scene,params string[] names){
        var go=new GameObject(scene);var anim=go.Add(new Animator());
        var controller=new UnityEditor.Animations.AnimatorController();
        controller.layers=new[]{new UnityEditor.Animations.AnimatorControllerLayer{stateMachine=new UnityEditor.Animations.AnimatorStateMachine()}};
        var parameters=new List<AnimatorControllerParameter>();
        foreach(var name in names)parameters.Add(new AnimatorControllerParameter{name=name,type=name=="Start"?AnimatorControllerParameterType.Trigger:AnimatorControllerParameterType.Bool});
        controller.parameters=parameters.ToArray();anim.runtimeAnimatorController=controller;return anim;
    }
    static GameObject Transition(Scene scene,AnimatedDynamicTransition component){
        var go=new GameObject(scene);go.Add(component);
        component.fields.Add("m_worldAnimator",Ref(Anim(scene,"Start","IsTransitioning")));
        component.fields.Add("m_worldAnimatorTrigger",new SerializedProperty{stringValue="Start"});
        var beach=component as BeachAnimatedDynamicTransition;
        if(beach!=null){
            component.fields.Add("m_leavingCounterLayoutRoot",Ref(new GameObject(scene).Add(new Transform())));
            component.fields.Add("m_tideCollider",Ref(new GameObject(scene).Add(new Collider())));
            component.fields.Add("m_tideAnimator",Ref(Anim(scene,"TideInScene","IsTideTransitioning")));
        }
        var camp=component as CampsiteAnimatedDynamicTransition;
        if(camp!=null){
            component.fields.Add("m_leavingRaftLayoutRoot",Ref(new GameObject(scene).Add(new Transform())));
            component.fields.Add("m_leavingRaftRespawnCollider",Ref(new GameObject(scene).Add(new Collider())));
            component.fields.Add("m_enteringRaftLayoutRoot",Ref(new GameObject(scene).Add(new Transform())));
            component.fields.Add("m_lakeAnimator",Ref(null));go.Add(new RespawnCollider());
        }
        return go;
    }
    static PseudoPrefabSO Pseudo(){return new PseudoPrefabSO{bundleName="available",assetPath="native/asset"};}
    static Scene SceneWithManager(out LevelInfoSO info,out PseudoPrefabManagerStub manager,out DynamicFlowController flow){
        var scene=new Scene{id=1,path="Assets/LevelSets/test/scenes/test.unity",isLoaded=true};
        var go=new GameObject(scene);manager=go.Add(new PseudoPrefabManagerStub());
        go.Add(new LevelEditor.PseudoPrefabManager{stub=manager});
        info=new LevelInfoSO{levelType=LevelInfoSO.LevelType.Dynamic,recipeListSOs=new[]{new UnityEngine.Object(),new UnityEngine.Object()},roundTimes=new[]{5,5}};
        manager.levelInfo=info;manager.configTemplateSO=Pseudo();manager.RecipeUISO=Pseudo();
        manager.FlowManagerGO=new GameObject(scene);manager.FlowManagerGO.Add(new LevelIntroFlowroutine());
        flow=manager.FlowManagerGO.Add(new DynamicFlowController());
        manager.AudioManagerGO=new GameObject(scene);manager.AudioManagerGO.Add(new CampaignAudioManager());
        manager.BootstrapManagerGO=new GameObject(scene);manager.BootstrapManagerGO.Add(new KitchenBootstrapManager());
        manager.PlayerSwitchingManagerGO=new GameObject(scene);manager.PlayerSwitchingManagerGO.Add(new PlayerSwitchingManager());
        manager.RecipeUIGO=new GameObject(scene);var ui=manager.RecipeUIGO.Add(new RecipeFlowGUI());
        flow.m_teamMonitor=new Team{m_recipeBarUIController=ui};
        var transition=Transition(scene,new AnimatedDynamicTransition());flow.m_transitions=new[]{transition};
        // The transition is a sibling root, deliberately outside the FlowManager subtree.
        scene.roots=new[]{go,manager.FlowManagerGO,manager.AudioManagerGO,manager.BootstrapManagerGO,manager.PlayerSwitchingManagerGO,manager.RecipeUIGO,transition};
        return scene;
    }
    static void Main(){
        LevelInfoSO info;PseudoPrefabManagerStub manager;DynamicFlowController flow;
        var scene=SceneWithManager(out info,out manager,out flow);var other=new Scene{id=2,path="other",isLoaded=true,roots=new GameObject[0]};
        Good(scene,info,"valid base transition on sibling root");
        Bad(other,info,"wrong scene rejected");var unloaded=scene;unloaded.isLoaded=false;Bad(unloaded,info,"unloaded rejected");
        var original=flow.m_transitions;flow.m_transitions=null;Bad(scene,info,"null array");
        flow.m_transitions=new GameObject[0];Bad(scene,info,"too few");
        flow.m_transitions=new[]{original[0],original[0]};Bad(scene,info,"too many");
        Check(LayoutEditorNativeModeValidation.ValidateScene(scene,info,3)!=null,"duplicate array with right count");
        flow.m_transitions=new GameObject[]{null};Bad(scene,info,"null transition");
        var foreign=Transition(other,new AnimatedDynamicTransition());flow.m_transitions=new[]{foreign};Bad(scene,info,"foreign transition");
        flow.m_transitions=original;var component=original[0].GetComponent<AnimatedDynamicTransition>();
        var parentRoot=new GameObject(scene);parentRoot.children.Add(original[0]);scene.roots[6]=parentRoot;
        Good(scene,info,"inactive transition root accepted before client activation");
        original[0].activeInHierarchy=false;Good(scene,info,"transition may start inactive");original[0].activeInHierarchy=true;
        original[0].Add(new DynamicTransitionBase());Bad(scene,info,"multiple transition components");original[0].components.RemoveAt(1);
        original[0].components.Add(null);Bad(scene,info,"transition missing script");original[0].components.RemoveAt(1);
        component.enabled=false;Bad(scene,info,"disabled transition");component.enabled=true;
        var world=(Animator)component.fields["m_worldAnimator"].objectReferenceValue;
        component.fields["m_worldAnimator"].objectReferenceValue=null;Bad(scene,info,"world animator null");
        component.fields["m_worldAnimator"].objectReferenceValue=world;world.gameObject.scene=other;Bad(scene,info,"foreign world animator");world.gameObject.scene=scene;
        component.fields["m_worldAnimatorTrigger"].stringValue="";Bad(scene,info,"empty trigger");component.fields["m_worldAnimatorTrigger"].stringValue="Start";
        var controller=(UnityEditor.Animations.AnimatorController)world.runtimeAnimatorController;
        controller.parameters[0].type=AnimatorControllerParameterType.Bool;Bad(scene,info,"wrong trigger type");controller.parameters[0].type=AnimatorControllerParameterType.Trigger;
        controller.parameters[1].name="wrong";Bad(scene,info,"missing IsTransitioning");controller.parameters[1].name="IsTransitioning";
        world.runtimeAnimatorController=null;Bad(scene,info,"missing controller");world.runtimeAnimatorController=controller;
        var layers=controller.layers;controller.layers=new UnityEditor.Animations.AnimatorControllerLayer[0];Bad(scene,info,"empty controller");controller.layers=layers;
        var machine=layers[0].stateMachine;machine.behaviours=new StateMachineBehaviour[]{null};Bad(scene,info,"missing machine behaviour");machine.behaviours=new StateMachineBehaviour[0];
        var state=new UnityEditor.Animations.AnimatorState{behaviours=new StateMachineBehaviour[]{null}};
        machine.states=new[]{new UnityEditor.Animations.ChildAnimatorState{state=state}};Bad(scene,info,"missing state behaviour");machine.states=new UnityEditor.Animations.ChildAnimatorState[0];
        var nested=new UnityEditor.Animations.AnimatorStateMachine{behaviours=new StateMachineBehaviour[]{null}};
        machine.stateMachines=new[]{new UnityEditor.Animations.ChildAnimatorStateMachine{stateMachine=nested}};Bad(scene,info,"nested missing behaviour");machine.stateMachines=new UnityEditor.Animations.ChildAnimatorStateMachine[0];
        world.runtimeAnimatorController=new AnimatorOverrideController{runtimeAnimatorController=controller};Good(scene,info,"override valid");
        world.runtimeAnimatorController=new AnimatorOverrideController();Bad(scene,info,"override without base");world.runtimeAnimatorController=controller;
        var overrideCycle=new AnimatorOverrideController();overrideCycle.runtimeAnimatorController=overrideCycle;world.runtimeAnimatorController=overrideCycle;Bad(scene,info,"override cycle");world.runtimeAnimatorController=controller;
        var beach=Transition(scene,new BeachAnimatedDynamicTransition());flow.m_transitions=new[]{beach};Good(scene,info,"Beach supported");
        var beachComponent=beach.GetComponent<BeachAnimatedDynamicTransition>();
        foreach(var field in new[]{"m_leavingCounterLayoutRoot","m_tideCollider","m_tideAnimator"}){
            var saved=beachComponent.fields[field].objectReferenceValue;beachComponent.fields[field].objectReferenceValue=null;Bad(scene,info,"Beach missing "+field);beachComponent.fields[field].objectReferenceValue=saved;
        }
        var tide=(Animator)beachComponent.fields["m_tideAnimator"].objectReferenceValue;
        var tideController=(UnityEditor.Animations.AnimatorController)tide.runtimeAnimatorController;
        foreach(var p in tideController.parameters){p.type=AnimatorControllerParameterType.Float;Bad(scene,info,"Beach bool "+p.name);p.type=AnimatorControllerParameterType.Bool;}
        var camp=Transition(scene,new CampsiteAnimatedDynamicTransition());flow.m_transitions=new[]{camp};Good(scene,info,"Campsite null lake allowed");
        var campComponent=camp.GetComponent<CampsiteAnimatedDynamicTransition>();
        foreach(var field in new[]{"m_leavingRaftLayoutRoot","m_leavingRaftRespawnCollider"}){
            var saved=campComponent.fields[field].objectReferenceValue;campComponent.fields[field].objectReferenceValue=null;Bad(scene,info,"Campsite missing "+field);campComponent.fields[field].objectReferenceValue=saved;
        }
        var respawn=camp.GetComponent<RespawnCollider>();camp.components.Remove(respawn);Bad(scene,info,"Campsite missing RespawnCollider");camp.components.Add(respawn);
        campComponent.fields["m_enteringRaftLayoutRoot"].objectReferenceValue=null;Good(scene,info,"Campsite optional entering root may be null per author guide");
        campComponent.fields["m_lakeAnimator"].objectReferenceInstanceIDValue=5;Bad(scene,info,"broken optional lake reference");campComponent.fields["m_lakeAnimator"].objectReferenceInstanceIDValue=0;
        var lake=Anim(scene,"RaftsAreChanging","IsArtInMotion");campComponent.fields["m_lakeAnimator"].objectReferenceValue=lake;Good(scene,info,"Campsite valid lake");
        foreach(var p in ((UnityEditor.Animations.AnimatorController)lake.runtimeAnimatorController).parameters){p.type=AnimatorControllerParameterType.Trigger;Bad(scene,info,"lake bool "+p.name);p.type=AnimatorControllerParameterType.Bool;}
        flow.m_transitions=new[]{Transition(scene,new OtherTransition())};Bad(scene,info,"unsupported subtype rejected");flow.m_transitions=original;
        var ordered=new[]{camp,beach};flow.m_transitions=ordered;
        Check(object.ReferenceEquals(LayoutEditorNativeModeValidation.FindTransitions(scene,flow,3),ordered),"actual flow array order preserved");
        Reject(delegate{LayoutEditorNativeModeValidation.FindTransitions(scene,flow,2);},"flow count never replaced by hierarchy");flow.m_transitions=original;
        var fallback=LayoutEditorNativeModeValidation.FindTransitions(scene,new CampaignFlowController(),2);
        Check(fallback.Length==1 && fallback[0]==original[0],"scene scoped sibling fallback");
        Reject(delegate{LayoutEditorNativeModeValidation.FindTransitions(scene,new CampaignFlowController(),3);},"no dummy transitions");
        parentRoot.children.Add(beach);
        fallback=LayoutEditorNativeModeValidation.FindTransitions(scene,new CampaignFlowController(),3);
        Check(fallback[0]==original[0] && fallback[1]==beach,"existing hierarchy order reused for migration");parentRoot.children.Remove(beach);
        info.roundTimes=new[]{5};Bad(scene,info,"native SO count mismatch");info.roundTimes=new[]{5,5};
        info.recipeListSOs[0]=null;Bad(scene,info,"missing native menu");info.recipeListSOs[0]=new UnityEngine.Object();
        Check(LayoutEditorNativeModeValidation.ValidateScene(scene,info,1)!=null,"phase minimum");
        Check(LayoutEditorNativeModeValidation.ValidateScene(scene,info,65)!=null,"phase maximum");
        SceneManager.loaded=other;
        Check(LayoutEditorNativeModeValidation.ValidateForSave(InfoPath,info,2)==null,"closed target unchanged count allowed despite other loaded scene");
        Check(LayoutEditorNativeModeValidation.ValidateForSave(InfoPath,info,3)!=null,"closed target changed count blocked");
        info.roundTimes=new[]{5};Check(LayoutEditorNativeModeValidation.ValidateForSave(InfoPath,info,2)!=null,"closed timing count mismatch blocked");info.roundTimes=new[]{5,5};
        SceneManager.loaded=scene;Check(LayoutEditorNativeModeValidation.ValidateForSave(InfoPath,info,3)!=null,"loaded target checks transition count");
        var third=Transition(scene,new AnimatedDynamicTransition());flow.m_transitions=new[]{original[0],third};
        Check(LayoutEditorNativeModeValidation.ValidateForSave(InfoPath,info,3)==null,"proposed phase count validated before SO rewrite");flow.m_transitions=original;
        LayoutEditorLevelModeChange.part=true;Bad(scene,info,"partLevel conflicts");
        Check(LayoutEditorNativeModeValidation.ValidateForSave(InfoPath,info,2)!=null,"save rejects partLevel");LayoutEditorLevelModeChange.part=false;
        var oldInfo=manager.levelInfo;manager.levelInfo=new LevelInfoSO();Bad(scene,info,"manager identity mismatch");manager.levelInfo=oldInfo;
        var duplicate=scene.roots[0].Add(new PseudoPrefabManagerStub());Bad(scene,info,"duplicate scene manager");scene.roots[0].components.Remove(duplicate);
        manager.FlowManagerGO.scene=other;Bad(scene,info,"cross scene FlowManager");manager.FlowManagerGO.scene=scene;
        flow.m_gameConfig=null;Bad(scene,info,"missing GameConfig");flow.m_gameConfig=new UnityEngine.Object();
        flow.enabled=false;Bad(scene,info,"disabled flow");flow.enabled=true;
        var secondFlow=manager.FlowManagerGO.Add(new CampaignFlowController());Bad(scene,info,"duplicate flow");manager.FlowManagerGO.components.Remove(secondFlow);
        var audio=manager.AudioManagerGO;manager.AudioManagerGO=null;Bad(scene,info,"missing audio manager");manager.AudioManagerGO=audio;
        audio.scene=other;Bad(scene,info,"foreign audio manager");audio.scene=scene;
        var recipe=manager.RecipeUIGO;manager.RecipeUIGO=null;Bad(scene,info,"missing Dynamic RecipeUI");manager.RecipeUIGO=recipe;
        flow.m_teamMonitor.m_recipeBarUIController=null;Bad(scene,info,"unbound RecipeUI");flow.m_teamMonitor.m_recipeBarUIController=recipe.GetComponent<RecipeFlowGUI>();
        scene.roots[0].components.Add(null);Bad(scene,info,"non-transition scene missing script");scene.roots[0].components.RemoveAt(2);
        info.levelType=LevelInfoSO.LevelType.Horde;Bad(scene,info,"Horde wrong flow type");
        manager.FlowManagerGO.components.Remove(flow);var horde=manager.FlowManagerGO.Add(new HordeFlowController());
        horde.fields.Add("m_uiPrefab",Ref(null));horde.fields.Add("m_waveNumberUIPrefab",Ref(null));
        horde.fields.Add("m_waveNumberUIDelay",new SerializedProperty{floatValue=1});horde.fields.Add("m_waveNumberUILocalisationTag",new SerializedProperty{stringValue="Horde.Wave"});
        manager.RecipeUIGO=null;manager.hordeModeUISO=Pseudo();manager.hordeWaveNumberUISO=Pseudo();
        var target=scene.roots[0].Add(new PseudoPrefabServingStationHordeStub{pseudoPrefabSO=Pseudo()});
        Good(scene,info,"Horde cleared runtime UI fields accepted using SO bindings");
        var uiSO=manager.hordeModeUISO;manager.hordeModeUISO=null;Bad(scene,info,"missing Horde UI SO");manager.hordeModeUISO=uiSO;
        var waveSO=manager.hordeWaveNumberUISO;manager.hordeWaveNumberUISO=null;Bad(scene,info,"missing wave UI SO");manager.hordeWaveNumberUISO=waveSO;
        var configSO=manager.configTemplateSO;manager.configTemplateSO=null;Bad(scene,info,"missing Horde config template");manager.configTemplateSO=configSO;
        configSO.asset=new KitchenLevelConfigBase();Bad(scene,info,"wrong Horde template type");
        configSO.asset=new HordeLevelConfig();Bad(scene,info,"missing Horde flowroutine data");
        configSO.asset=new HordeLevelConfig{m_flowroutineData=new object()};Good(scene,info,"typed Horde template valid");configSO.asset=null;
        horde.fields["m_waveNumberUIDelay"].floatValue=float.NaN;Bad(scene,info,"invalid Horde UI delay");horde.fields["m_waveNumberUIDelay"].floatValue=1;
        horde.fields["m_waveNumberUILocalisationTag"].stringValue="";Bad(scene,info,"empty localization");horde.fields["m_waveNumberUILocalisationTag"].stringValue="Horde.Wave";
        var directUI=new GameObject(scene);uiSO.asset=directUI;Bad(scene,info,"UI controller required");directUI.Add(new UIControllerBase());Good(scene,info,"direct Horde UI valid");
        directUI.components.Add(null);Bad(scene,info,"Horde UI missing script");directUI.components.RemoveAt(1);
        waveSO.asset=directUI;Bad(scene,info,"wave text required");directUI.Add(new T17Text());Good(scene,info,"wave text valid");
        scene.roots[0].components.Remove(target);Bad(scene,info,"Horde target required");scene.roots[0].components.Add(target);
        info.config_4p=null;Bad(scene,info,"missing player config");info.config_4p=new Player();
        scene.roots[0].components.Add(null);Bad(scene,info,"Horde missing script rejected");scene.roots[0].components.RemoveAt(scene.roots[0].components.Count-1);
        info.levelType=LevelInfoSO.LevelType.Campaign;
        Check(LayoutEditorNativeModeValidation.ValidateScene(new Scene(),info)==null,"Campaign unchanged");
        Console.WriteLine("PASS "+count+" checks executing unchanged production validator plus production Horde guard; C#4 doubles, no Unity writes.");
    }
}
'''


def main():
    validator = EDITOR / 'LayoutEditorNativeModeValidation.cs'
    source = validator.read_text(encoding='utf-8-sig')
    store = (EDITOR / 'LayoutEditorLevelModeStore.cs').read_text(encoding='utf-8-sig')
    admin = (EDITOR / 'LayoutEditorLevelAdminApi.cs').read_text(encoding='utf-8-sig')
    change = (EDITOR / 'LayoutEditorLevelModeChange.cs').read_text(encoding='utf-8-sig')
    save = extract(store, 'ValidateForSave')
    assert 'LayoutEditorNativeModeValidation.ValidateForSave(levelInfoPath, info,' in save
    assert 'dto.gameMode == "Dynamic" ? (int?)dto.phases.Length : null' in save
    create = extract(admin, 'CreateLevel')
    assert create.index('OpenScene(scenePath)') < create.index('ValidateScene(newScene, info)') < create.index('SaveScenePrepared(newScene, false)')
    assert 'if (sceneError != null) throw' in create
    assert 'LayoutEditorLevelTemplate.Cleanup(new[] { scenePath, levelDataDir })' in create
    assert create.index('try\n        {\n            previousSetup') < create.index('AssetDatabase.CreateFolder(')
    transaction = extract(change, 'ChangeLevelMode')
    assert transaction.index('Undo.DestroyObjectImmediate(oldFlow)') < transaction.index('StageModeChange(')
    assert transaction.index('manager.hordeWaveNumberUISO = template.waveUI') < transaction.index('StageModeChange(')
    assert transaction.index('ValidateScene(scene, info);', transaction.index('ValidateChanged(')) < transaction.index('AssetDatabase.SaveAssets()')
    assert 'LayoutEditorNativeModeValidation.FindTransitions(' in extract(change, 'FindTransitions')
    assert '禁用分 P' not in change
    assert 'FindObjectsOfType' not in source and 'GetActiveScene' not in source
    assert not re.search(r'Destroy|RemoveMissingScripts|ApplyModifiedProperties|SetDirty|SaveScene\(', source)
    host = ROOT / 'Assets/Scripts/Assembly-CSharp'
    # Verify names against the real runtime consumers, not only the API doubles.
    for file, fields in {
        'AnimatedDynamicTransition.cs': ['m_worldAnimator', 'm_worldAnimatorTrigger', 'IsTransitioning'],
        'BeachAnimatedDynamicTransition.cs': ['m_leavingCounterLayoutRoot', 'm_tideAnimator', 'm_tideCollider', 'TideInScene', 'IsTideTransitioning'],
        'CampsiteAnimatedDynamicTransition.cs': ['m_leavingRaftLayoutRoot', 'm_leavingRaftRespawnCollider', 'm_enteringRaftLayoutRoot', 'm_lakeAnimator', 'RaftsAreChanging', 'IsArtInMotion'],
        'GameModes/Horde/HordeFlowController.cs': ['m_uiPrefab', 'm_waveNumberUIPrefab', 'm_waveNumberUIDelay', 'm_waveNumberUILocalisationTag'],
    }.items():
        runtime = (host / file).read_text(encoding='utf-8-sig')
        for field in fields:
            assert field in runtime and field in source, (file, field)
    runtime = (host / 'ClientDynamicFlowController.cs').read_text(encoding='utf-8-sig')
    assert 'int index = _newPhase - 1;' in runtime and 'm_transitions[index]' in runtime
    template = (ROOT / 'Assets/Template/dynamic/s_template_dynamic.unity').read_text()
    for field in ('m_leavingCounterLayoutRoot:', 'm_leavingRaftLayoutRoot:', 'm_transitions.Array.data[0]', 'm_transitions.Array.data[1]'):
        assert field in template, field
    with tempfile.TemporaryDirectory(prefix='oc2-native-mode-') as temp:
        out = Path(temp)
        (out / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (out / 'Program.cs').write_text(HARNESS.replace('__HORDE_GUARD__', extract(change, 'ValidateHordeObjects')), encoding='utf-8')
        project = out / 'Checks.csproj'
        project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                           '<TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion>'
                           '<GenerateAssemblyInfo>false</GenerateAssemblyInfo><NuGetAudit>false</NuGetAudit>'
                           '</PropertyGroup><ItemGroup><Compile Include="' + escape(str(validator)) + '" />'
                           '</ItemGroup></Project>')
        subprocess.run(['dotnet', 'run', '--project', str(project)], cwd=out,
                       env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1'), check=True, timeout=120)
    print('PASS caller source-order/rollback/read-only contracts. LIMIT: no live Unity serialization, Play or rollback execution.')


if __name__ == '__main__':
    main()
