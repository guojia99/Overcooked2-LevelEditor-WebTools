#!/usr/bin/env python3
"""Execute production native mode import/staging on real template YAML graphs.

The methods are extracted verbatim; only Unity object/asset persistence is doubled.
This complements the unchanged copier and CreateLevel transaction behavior tests.
"""
import os
from pathlib import Path
import subprocess
import tempfile
import runpy

ROOT = Path(__file__).resolve().parents[2]
extract = runpy.run_path(str(Path(__file__).with_name('check-level-create-transaction.py')))['extract']

HARNESS = r'''
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;
namespace UnityEngine {
    public class Object {
        public string name,path;
        public static T Instantiate<T>(T o) where T:Object{return (T)o.MemberwiseClone();}
    }
    public class ScriptableObject:Object {public static T CreateInstance<T>() where T:ScriptableObject,new(){return new T();}}
}
public class PseudoPrefabSO:ScriptableObject { }
public class RecipeListSO:ScriptableObject {public ScriptableObject[] recipes;}
public class HordeWaveDataSO:ScriptableObject {public RecipeListSO recipeListSO;public int intervalSeconds;public PseudoPrefabSO[] enemies;public int[] spawnTimes;}
public class HordeLevelConfigSO:ScriptableObject {public int targetRepairCostMax;public HordeWaveDataSO[] hordeWaveDataSOs;}
public class LevelConfigSetupPerPlayerCountSO:ScriptableObject {public HordeLevelConfigSO hordeLevelConfig;public int plateReturnTime,roundTime,orderLifeTime;}
public class LevelInfoSO:ScriptableObject {
    public enum LevelType {Campaign,Dynamic,Horde}
    public LevelType levelType;public RecipeListSO[] recipeListSOs;public int[] roundTimes;
    public LevelConfigSetupPerPlayerCountSO config_1p,config_2p,config_3p,config_4p;
    public int targetHealth,kitchenHealth,minOrderCount,maxOrderCount;public float targetRepairSpeed,targetRepairThreshold;
}
public class LevelModeConfigDto {
    public string assetPath,gameMode;public int targetHealth,kitchenHealth,minOrderCount,maxOrderCount;
    public float targetRepairSpeed,targetRepairThreshold;public LevelModeRecipePhaseDto[] phases;public HordePlayerConfigDto[] hordeConfigs;
}
public class LevelModeRecipePhaseDto {public string id;public int durationSeconds;public string[] recipeGuids;}
public class HordePlayerConfigDto {public int targetRepairCostMax;public HordeWaveDto[] waves;}
public class HordeWaveDto {public string id;public int intervalSeconds;public string[] recipeGuids,enemyGuids;public int[] spawnTimes;}
namespace UnityEditor {
    public class AssetImporter {public string assetBundleName;public static AssetImporter GetAtPath(string p){return new AssetImporter();}public void SaveAndReimport(){}}
    public static class EditorUtility {public static void SetDirty(Object o){} }
    public static class AssetDatabase {
        public static Dictionary<string,Object> Assets=new Dictionary<string,Object>();
        public static Dictionary<string,string> Guids=new Dictionary<string,string>();
        public static bool IsValidFolder(string p){return false;}
        public static string CreateFolder(string p,string n){return "folder";}
        public static string GetImplicitAssetBundleName(string p){return "set/info_set";}
        public static Object LoadMainAssetAtPath(string p){Object o;return Assets.TryGetValue(p,out o)?o:null;}
        public static void CreateAsset(Object o,string p){o.path=p;Assets.Add(p,o);Guids.Add(p,Guid.NewGuid().ToString("N"));}
        public static string GetAssetPath(Object o){return o.path;}
        public static string AssetPathToGUID(string p){return Guids[p];}
        public static string GUIDToAssetPath(string g){foreach(var p in Guids)if(p.Value==g)return p.Key;throw new Exception("unknown guid "+g);}
    }
}
static class Store {
    const string AssetFolder="mode_assets";const int MaxPhases=64,MaxWavesPerPlayer=128,MaxRecipesPerMenu=256;
    static ScriptableObject ResolveRecipe(string g){return (ScriptableObject)AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(g));}
    static PseudoPrefabSO ResolveEnemy(string g){return (PseudoPrefabSO)ResolveRecipe(g);}
    static string AbsPath(string p){return p;}
__METHODS__
    public static void Stage(string p,LevelInfoSO i,LevelModeConfigDto dto){SynchronizeAssets(p,i,dto);ApplySettings(i,dto);}
}
static class Checks {
    static int count;
    static string root;
    static void Check(bool ok,string m){count++;if(!ok)throw new Exception(m);}
    static int Number(string t,string field){return int.Parse(Regex.Match(t,@"(?m)^  "+field+@": (\d+)").Groups[1].Value);}
    static string[] Refs(string t,string field){
        var block=Regex.Match(t,@"(?m)^  "+field+@":\s*\r?\n((?:  -[^\r\n]*\r?\n?)*)");
        var result=new List<string>();foreach(Match m in Regex.Matches(block.Groups[1].Value,@"guid: ([0-9a-f]{32})"))result.Add(m.Groups[1].Value);return result.ToArray();
    }
    static string Ref(string t,string field){return Regex.Match(t,@"(?m)^  "+field+@": \{fileID: \d+, guid: ([0-9a-f]{32})").Groups[1].Value;}
    static int[] Ints(string t,string field){
        string hex=Regex.Match(t,@"(?m)^  "+field+@": ([0-9a-f]+)").Groups[1].Value;
        var r=new int[hex.Length/8];for(int i=0;i<r.Length;i++){var b=new byte[4];for(int j=0;j<4;j++)b[j]=Convert.ToByte(hex.Substring(i*8+j*2,2),16);r[i]=BitConverter.ToInt32(b,0);}return r;
    }
    static string GuidFor(string path){return Regex.Match(File.ReadAllText(Path.Combine(root,path+".meta")),@"(?m)^guid: ([a-f0-9]{32})").Groups[1].Value;}
    static ScriptableObject Read(string path){
        Object cached;if(AssetDatabase.Assets.TryGetValue(path,out cached))return (ScriptableObject)cached;
        string text=File.ReadAllText(Path.Combine(root,path));ScriptableObject result;
        if(text.Contains("  hordeLevelConfig:"))result=new LevelConfigSetupPerPlayerCountSO{
            plateReturnTime=Number(text,"plateReturnTime"),roundTime=Number(text,"roundTime"),orderLifeTime=Number(text,"orderLifeTime"),
            hordeLevelConfig=Ref(text,"hordeLevelConfig")==""?null:(HordeLevelConfigSO)ReadGuid(Ref(text,"hordeLevelConfig"),false)};
        else if(text.Contains("  hordeWaveDataSOs:")){
            var refs=Refs(text,"hordeWaveDataSOs");var waves=new HordeWaveDataSO[refs.Length];for(int i=0;i<waves.Length;i++)waves[i]=(HordeWaveDataSO)ReadGuid(refs[i],false);
            result=new HordeLevelConfigSO{targetRepairCostMax=Number(text,"targetRepairCostMax"),hordeWaveDataSOs=waves};
        } else if(text.Contains("  intervalSeconds:")){
            var refs=Refs(text,"enemies");var enemies=new PseudoPrefabSO[refs.Length];for(int i=0;i<enemies.Length;i++)enemies[i]=(PseudoPrefabSO)ReadGuid(refs[i],true);
            result=new HordeWaveDataSO{intervalSeconds=Number(text,"intervalSeconds"),recipeListSO=(RecipeListSO)ReadGuid(Ref(text,"recipeListSO"),false),enemies=enemies,spawnTimes=Ints(text,"spawnTimes")};
        } else {var refs=Refs(text,"recipes");var recipes=new ScriptableObject[refs.Length];for(int i=0;i<refs.Length;i++)recipes[i]=ReadGuid(refs[i],true);result=new RecipeListSO{recipes=recipes};}
        result.path=path;AssetDatabase.Assets.Add(path,result);AssetDatabase.Guids.Add(path,GuidFor(path));return result;
    }
    static ScriptableObject ReadGuid(string guid,bool external){
        foreach(var p in AssetDatabase.Guids)if(p.Value==guid)return (ScriptableObject)AssetDatabase.Assets[p.Key];
        foreach(var file in Directory.GetFiles(Path.Combine(root,"Assets/Template"),"*.meta",SearchOption.AllDirectories))
            if(File.ReadAllText(file).Contains("guid: "+guid))return Read(file.Substring(root.Length+1,file.Length-root.Length-6).Replace('\\','/'));
        if(!external)throw new Exception("missing native fixture "+guid);
        var o=new PseudoPrefabSO{path="external/"+guid};AssetDatabase.Assets.Add(o.path,o);AssetDatabase.Guids.Add(o.path,guid);return o;
    }
    static LevelInfoSO Info(string mode){
        string key=mode.ToLowerInvariant(),suffix=mode=="Campaign"?"":"_"+key;
        string text=File.ReadAllText(Path.Combine(root,"Assets/Template/"+key+"/levelinfo_template"+suffix+".asset"));
        var info=new LevelInfoSO{levelType=(LevelInfoSO.LevelType)Enum.Parse(typeof(LevelInfoSO.LevelType),mode),
            minOrderCount=Number(text,"minOrderCount"),maxOrderCount=Number(text,"maxOrderCount"),targetHealth=Number(text,"targetHealth"),
            kitchenHealth=Number(text,"kitchenHealth"),targetRepairSpeed=Number(text,"targetRepairSpeed"),targetRepairThreshold=Number(text,"targetRepairThreshold")};
        var players=new LevelConfigSetupPerPlayerCountSO[4];
        for(int p=0;p<4;p++)players[p]=(LevelConfigSetupPerPlayerCountSO)Read("Assets/Template/"+key+"/config_"+(p+1)+"p_template_"+key+".asset");
        info.config_1p=players[0];info.config_2p=players[1];info.config_3p=players[2];info.config_4p=players[3];
        if(mode=="Dynamic"){
            var refs=Refs(text,"recipeListSOs");info.recipeListSOs=new RecipeListSO[refs.Length];for(int i=0;i<refs.Length;i++)info.recipeListSOs[i]=(RecipeListSO)ReadGuid(refs[i],false);
            info.roundTimes=Ints(text,"roundTimes");
        }return info;
    }
    static LevelConfigSetupPerPlayerCountSO[] Players(LevelInfoSO i){return new[]{i.config_1p,i.config_2p,i.config_3p,i.config_4p};}
    static void Main(string[] args){
        root=args[0];
        foreach(var mode in new[]{"Dynamic","Horde"}){
            var source=Info(mode);var a=Object.Instantiate(source);var b=Object.Instantiate(source);
            var original=Store.LoadFromAssets("source",source);var first=Store.LoadFromAssets("Assets/LevelSets/set/data/a/Info.asset",a);
            var second=Store.LoadFromAssets("Assets/LevelSets/set/data/b/Info.asset",b);
            Store.Stage(first.assetPath,a,first);Store.Stage(second.assetPath,b,second);
            Check(a.maxOrderCount==source.maxOrderCount&&a.minOrderCount==source.minOrderCount&&a.targetRepairSpeed==source.targetRepairSpeed,"native scalar settings retained");
            if(mode=="Dynamic"){
                Check(first.phases.Length==3&&original.phases.Length==3,"real three-stage template imported");
                for(int i=0;i<3;i++){
                    Check(a.recipeListSOs[i]!=source.recipeListSOs[i]&&a.recipeListSOs[i]!=b.recipeListSOs[i],"phase assets private per level");
                    Check(a.roundTimes[i]==source.roundTimes[i]&&a.roundTimes[i]==15,"default phase time retained");
                    Check(a.recipeListSOs[i].recipes[0]==source.recipeListSOs[i].recipes[0],"public recipe retained");
                }
                a.recipeListSOs[0].recipes=new ScriptableObject[0];Check(source.recipeListSOs[0].recipes.Length==1&&b.recipeListSOs[0].recipes.Length==1,"editing A phase leaves template/B unchanged");
            } else {
                var ap=Players(a);var bp=Players(b);var sp=Players(source);var seen=new HashSet<Object>();
                for(int p=0;p<4;p++){
                    Check(seen.Add(ap[p])&&seen.Add(ap[p].hordeLevelConfig),"each player/config distinct");
                    Check(ap[p]!=sp[p]&&ap[p]!=bp[p]&&ap[p].hordeLevelConfig!=sp[p].hordeLevelConfig,"no shared player or horde config");
                    Check(ap[p].plateReturnTime==sp[p].plateReturnTime&&ap[p].roundTime==sp[p].roundTime&&ap[p].orderLifeTime==sp[p].orderLifeTime,"per-player timing retained");
                    Check(ap[p].hordeLevelConfig.targetRepairCostMax==sp[p].hordeLevelConfig.targetRepairCostMax,"repair cost retained");
                    var aw=ap[p].hordeLevelConfig.hordeWaveDataSOs;var sw=sp[p].hordeLevelConfig.hordeWaveDataSOs;
                    Check(aw.Length==2&&sw.Length==2,"real two-wave template imported");
                    for(int w=0;w<aw.Length;w++){
                        Check(seen.Add(aw[w])&&seen.Add(aw[w].recipeListSO),"each wave/menu distinct across players");
                        Check(aw[w].intervalSeconds==sw[w].intervalSeconds&&aw[w].recipeListSO.recipes[0]==sw[w].recipeListSO.recipes[0],"wave defaults and menu retained");
                        Check(aw[w].enemies.Length==sw[w].enemies.Length&&aw[w].spawnTimes.Length==sw[w].spawnTimes.Length,"enemy counts retained");
                        for(int e=0;e<aw[w].enemies.Length;e++)Check(aw[w].enemies[e]==sw[w].enemies[e]&&aw[w].spawnTimes[e]==sw[w].spawnTimes[e],"enemy/time pair retained");
                    }
                }
                ap[0].hordeLevelConfig.hordeWaveDataSOs[0].spawnTimes[0]=99;
                Check(sp[0].hordeLevelConfig.hordeWaveDataSOs[0].spawnTimes[0]==0&&bp[0].hordeLevelConfig.hordeWaveDataSOs[0].spawnTimes[0]==0
                    &&ap[1].hordeLevelConfig.hordeWaveDataSOs[0].spawnTimes[0]==0,"A edit leaves template/B/other player unchanged");
            }
        }
        Console.WriteLine("PASS "+count+" production mode initialization checks from actual Dynamic/Horde YAML graphs; asset persistence is doubled.");
    }
}
'''


def main():
    source = (ROOT / 'Assets/Editor/LayoutEditor/LayoutEditorLevelModeStore.cs').read_text(encoding='utf-8-sig')
    methods = '\n'.join(extract(source, name) for name in (
        'LoadFromAssets', 'SynchronizeAssets', 'ApplySettings', 'CreateRecipeList', 'CreateAsset', 'AssetGuids', 'PlayerConfigs', 'GetMode'))
    with tempfile.TemporaryDirectory(prefix='oc2-mode-initialization-') as temp:
        out = Path(temp)
        (out / 'Program.cs').write_text(HARNESS.replace('__METHODS__', methods))
        (out / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (out / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo>'
            '<NuGetAudit>false</NuGetAudit></PropertyGroup></Project>')
        subprocess.run(['dotnet', 'run', '--project', str(out / 'Checks.csproj'), '--', str(ROOT)], cwd=out,
                       env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1'), check=True, timeout=120)


if __name__ == '__main__':
    main()
