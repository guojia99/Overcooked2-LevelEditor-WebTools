#!/usr/bin/env python3
"""Compile the unchanged production template copier against filesystem-backed API doubles.

Real template YAML is copied only into a temporary fixture. No Unity invocation,
project assets, source metadata or scenes are written by this test.
"""
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Assets/Editor/LayoutEditor/LayoutEditorLevelTemplate.cs'

HARNESS = r'''
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using LevelEditorStub;
namespace UnityEngine {
    public class Object { }
    public static class Application { public static string dataPath; }
}
namespace LevelEditorStub {
    public class LevelInfoSO:UnityEngine.Object {
        public enum LevelType { Campaign,Dynamic,Horde }
        public LevelType levelType;
    }
    public class LevelConfigSetupPerPlayerCountSO:UnityEngine.Object { }
    public class HordeLevelConfigSO:UnityEngine.Object { }
    public class HordeWaveDataSO:UnityEngine.Object { }
    public class RecipeListSO:UnityEngine.Object { }
}
namespace UnityEditor {
    public enum ImportAssetOptions { ForceUpdate }
    public class AssetImporter {
        public string assetBundleName="old/bundle",assetBundleVariant="old";
        public static Dictionary<string,AssetImporter> All=new Dictionary<string,AssetImporter>();
        public static AssetImporter GetAtPath(string p) {
            if(!File.Exists(p)&&!Directory.Exists(p))return null;
            AssetImporter i;if(!All.TryGetValue(p,out i)){i=new AssetImporter();All.Add(p,i);}return i;
        }
        public void SetAssetBundleNameAndVariant(string n,string v){assetBundleName=n;assetBundleVariant=v;}
        public void SaveAndReimport(){}
    }
    public static class AssetDatabase {
        public static Dictionary<string,string> Guids=new Dictionary<string,string>();
        public static string FailCopy,FailDelete,ThrowDelete;
        public static int Copies;
        public static Action OnImport;
        public static string AssetPathToGUID(string p){string g;return Guids.TryGetValue(p,out g)?g:"";}
        public static string GUIDToAssetPath(string g){foreach(var p in Guids)if(p.Value==g)return p.Key;return "";}
        public static T LoadAssetAtPath<T>(string p) where T:UnityEngine.Object {
            if(!File.Exists(p))return null;
            UnityEngine.Object o=null;
            if(typeof(T)==typeof(LevelInfoSO)){
                var m=Regex.Match(File.ReadAllText(p),@"(?m)^  levelType: (\d+)");
                if(m.Success)o=new LevelInfoSO{levelType=(LevelInfoSO.LevelType)int.Parse(m.Groups[1].Value)};
            } else if(typeof(T)==typeof(LevelConfigSetupPerPlayerCountSO))o=new LevelConfigSetupPerPlayerCountSO();
            return o as T;
        }
        public static UnityEngine.Object LoadMainAssetAtPath(string p){
            if(!File.Exists(p))return null;
            string t=File.ReadAllText(p);
            if(t.Contains("  hordeLevelConfig:"))return new LevelConfigSetupPerPlayerCountSO();
            if(t.Contains("  hordeWaveDataSOs:"))return new HordeLevelConfigSO();
            if(t.Contains("  intervalSeconds:"))return new HordeWaveDataSO();
            if(t.Contains("  recipes:")&&!t.Contains("  levelType:"))return new RecipeListSO();
            return new UnityEngine.Object();
        }
        public static bool IsValidFolder(string p){return Directory.Exists(p);}
        public static string CreateFolder(string p,string name){Directory.CreateDirectory(Path.Combine(p,name));return "folder";}
        public static bool CopyAsset(string s,string d){
            Copies++;
            if(s==FailCopy)return false;
            File.Copy(s,d);var g=Guid.NewGuid().ToString("N");Guids.Add(d,g);
            File.WriteAllText(d+".meta","guid: "+g);AssetImporter.GetAtPath(d);return true;
        }
        public static void ImportAsset(string p,ImportAssetOptions o){if(OnImport!=null)OnImport();}
        public static string[] GetAllAssetPaths(){return new List<string>(Guids.Keys).ToArray();}
        public static string[] GetDependencies(string[] paths,bool recursive){
            var result=new HashSet<string>();var todo=new Queue<string>(paths);
            while(todo.Count>0){string p=todo.Dequeue();if(!result.Add(p)||!File.Exists(p))continue;
                foreach(Match m in Regex.Matches(File.ReadAllText(p),@"guid:\s*([a-f0-9]{32})")){
                    string dep=GUIDToAssetPath(m.Groups[1].Value);if(dep!="")todo.Enqueue(dep);
                }
            }return new List<string>(result).ToArray();
        }
        public static bool DeleteAsset(string p){
            if(p==ThrowDelete)throw new IOException("injected delete failure");
            if(p==FailDelete)return false;
            if(Directory.Exists(p))Directory.Delete(p,true);else if(File.Exists(p))File.Delete(p);
            if(File.Exists(p+".meta"))File.Delete(p+".meta");
            var remove=new List<string>();foreach(var pair in Guids)if(pair.Key==p||pair.Key.StartsWith(p+"/"))remove.Add(pair.Key);
            foreach(var key in remove)Guids.Remove(key);return true;
        }
    }
}
static class Checks {
    static int count;
    static void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
    static void Reject(Action action,string message){bool rejected=false;try{action();}catch(IOException){rejected=true;}catch(InvalidDataException){rejected=true;}Check(rejected,message);}
    static string Data(string id){return "Assets/LevelSets/test/data/"+id;}
    static string Scene(string id){return "Assets/LevelSets/test/scenes/"+id+".unity";}
    static string Info(string id){return Data(id)+"/LevelInfo.asset";}
    static LayoutEditorLevelTemplate Plan(string mode,string id){return LayoutEditorLevelTemplate.Read(mode,Data(id),Scene(id),Info(id));}
    static void Main(string[] args){
        Directory.SetCurrentDirectory(args[0]);UnityEngine.Application.dataPath=Path.GetFullPath("Assets");
        foreach(var line in File.ReadAllLines(args[1])){var parts=line.Split('\t');AssetDatabase.Guids.Add(parts[0],parts[1]);}
        var originals=new Dictionary<string,string>();foreach(var p in Directory.GetFiles("Assets/Template","*",SearchOption.AllDirectories))originals.Add(p,File.ReadAllText(p));
        foreach(var mode in new[]{"Campaign","Dynamic","Horde"}){
            string id=mode.ToLowerInvariant();var a=Plan(mode,id+"_a");var b=Plan(mode,id+"_b");
            Check(AssetDatabase.Copies==0,"preflight performs no copies");
            Check(a.entries.Count==Directory.GetFiles(LayoutEditorLevelTemplate.Root(mode),"*",SearchOption.AllDirectories).Length/2,"all private assets discovered");
            foreach(var e in a.entries)Check(e.source.StartsWith(LayoutEditorLevelTemplate.Root(mode)+"/"),"only selected mode copied");
            AssetDatabase.OnImport=delegate{
                foreach(var e in a.entries){
                    Check(File.Exists(e.target),"all copies exist before import");
                    string t=File.ReadAllText(e.target);
                    foreach(var source in a.entries)Check(!t.Contains(source.guid),"all source private GUIDs rebound before first import");
                }
            };
            a.Copy();AssetDatabase.OnImport=null;b.Copy();
            var am=new Dictionary<string,string>();var bm=new Dictionary<string,string>();
            foreach(var e in a.entries)am.Add(e.guid,AssetDatabase.AssetPathToGUID(e.target));
            foreach(var e in b.entries)bm.Add(e.guid,AssetDatabase.AssetPathToGUID(e.target));
            foreach(var e in a.entries){
                string text=File.ReadAllText(e.target);
                Check(text==LayoutEditorLevelTemplate.Remap(e.text,am),"production remap matches complete YAML");
                Check(am[e.guid]!=bm[e.guid]&&am[e.guid]!=e.guid,"two levels and template have distinct GUIDs");
                foreach(Match m in Regex.Matches(e.text,@"guid:\s*([0-9a-f]{32})")){
                    string g=m.Groups[1].Value;if(!am.ContainsKey(g))Check(text.Contains(g),"public GUID preserved");
                }
            }
            string info=File.ReadAllText(Info(id+"_a"));
            for(int p=1;p<=4;p++)Check(info.Contains("config_"+p+"p: {fileID: 11400000, guid: "+AssetDatabase.AssetPathToGUID(Data(id+"_a")+"/config_"+p+"p.asset")),"explicit selected config binding");
            if(mode=="Campaign")Check(info.Contains("b98d38299de379745b64461474cd7943"),"campaign recipe retained");
            if(mode=="Dynamic"){
                Check(info.Contains("roundTimes: 0f0000000f0000000f000000"),"three default durations retained");
                Check(info.Contains("disableDynamicParenting: 0")&&info.Contains("enableRespawnBounds: 1"),"dynamic native settings retained");
                Check(File.ReadAllText(Data(id+"_a")+"/config_1p.asset").Contains("plateReturnTime: 7"),"dynamic config beats erroneous horde reference");
                Check(!Directory.Exists(Data(id+"_a")+"/template_assets/horde"),"no horde graph copied into dynamic");
            }
            // Simulate the store replacing native SO roots, then execute real cleanup.
            if(mode!="Campaign"){
                string keep=info;
                foreach(var e in a.entries){
                    var asset=AssetDatabase.LoadMainAssetAtPath(e.target);
                    if(asset is RecipeListSO || (mode=="Horde"&&asset is LevelConfigSetupPerPlayerCountSO))
                        keep=keep.Replace(AssetDatabase.AssetPathToGUID(e.target),"eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
                }
                File.WriteAllText(Info(id+"_a"),keep);a.RemoveSupersededModeAssets();
                foreach(var e in a.entries){
                    var isConfig=e.target.Contains("/config_");var isMode=e.source.Contains("recipelist")||e.source.Contains("/horde_");
                    if(isMode||(mode=="Horde"&&isConfig))Check(!File.Exists(e.target),"superseded mode graph removed");
                    if(e.source.EndsWith(".mat")||e.source.EndsWith(".controller")||e.source.EndsWith(".anim"))Check(File.Exists(e.target),"scene private asset retained");
                }
            }
            LayoutEditorLevelTemplate.NormalizeLabels(Data(id+"_a"),Scene(id+"_a"),"test");
            foreach(var pair in AssetImporter.All){
                if(!File.Exists(pair.Key)&&!Directory.Exists(pair.Key))continue;
                if(pair.Key.StartsWith(Data(id+"_a")+"/"))Check(pair.Value.assetBundleName==""&&pair.Value.assetBundleVariant=="","nested explicit labels cleared");
            }
            Check(AssetImporter.GetAtPath(Data(id+"_a")).assetBundleName=="","data root also inherits set info bundle");
            Check(AssetImporter.GetAtPath(Scene(id+"_a")).assetBundleName=="test/"+id+"_a","scene has own bundle");
            Check(LayoutEditorLevelTemplate.Cleanup(new[]{Scene(id+"_a"),Data(id+"_a"),Scene(id+"_b"),Data(id+"_b")}).Length==0,"complete cleanup");
            AssetDatabase.Copies=0;
        }
        foreach(var pair in originals)Check(File.ReadAllText(pair.Key)==pair.Value,"source YAML and metadata unchanged");
        string cfg="Assets/Template/campaign/config_2p.asset";string saved=File.ReadAllText(cfg);File.Delete(cfg);
        Reject(delegate{Plan("Campaign","missing");},"missing config rejected before mutation");File.WriteAllText(cfg,saved);
        Check(!Directory.Exists(Data("missing")),"missing template leaves no target");
        var changed=Plan("Campaign","changed");File.AppendAllText(cfg,"\n");
        Reject(delegate{changed.Copy();},"source changed after preflight rejected");File.WriteAllText(cfg,saved);
        Check(!Directory.Exists(Data("changed")),"changed source rejected before first copy");
        var metaChanged=Plan("Campaign","meta_changed");string meta=File.ReadAllText(cfg+".meta");File.AppendAllText(cfg+".meta","\nuserData: changed\n");
        Reject(delegate{metaChanged.Copy();},"metadata changed after preflight rejected");File.WriteAllText(cfg+".meta",meta);
        Check(!Directory.Exists(Data("meta_changed")),"metadata drift rejected before output");
        string anim="Assets/Template/dynamic/animations/template_dynamic_tide_in.anim";string animText=File.ReadAllText(anim);File.Delete(anim);
        Reject(delegate{Plan("Dynamic","missing_clip");},"missing private clip rejected before mutation");File.WriteAllText(anim,animText);
        Check(!Directory.Exists(Data("missing_clip")),"missing clip leaves no output");
        string sceneSource="Assets/Template/campaign/s_template.unity";string sceneText=File.ReadAllText(sceneSource);
        File.Delete(sceneSource);Reject(delegate{Plan("Campaign","missing_scene");},"missing scene rejected before output");File.WriteAllText(sceneSource,sceneText);
        File.WriteAllText(sceneSource,sceneText+"\n  unexpected: {fileID: 11400000, guid: 2a0a742a3db85c044bca46c03fd09888, type: 2}\n");
        Reject(delegate{Plan("Campaign","cross_mode");},"cross-mode reference rejected instead of copying horde graph");
        File.WriteAllText(sceneSource,sceneText+"\n  levelInfo: {fileID: 11400000, guid: dc922b3302c559542befcfaf53d8bb0c, type: 2}\n");
        Reject(delegate{Plan("Campaign","duplicate_manager");},"duplicate manager rejected before output");
        File.WriteAllText(sceneSource,sceneText);
        Directory.CreateDirectory(Data("collision"));File.WriteAllText(Info("collision"),"existing content");
        Reject(delegate{Plan("Campaign","collision");},"existing target rejected");
        Check(File.ReadAllText(Info("collision"))=="existing content","existing target untouched");
        LayoutEditorLevelTemplate.Cleanup(new[]{Data("collision")});
        var partial=Plan("Dynamic","partial");AssetDatabase.FailCopy=partial.entries[partial.entries.Count-1].source;
        Reject(delegate{partial.Copy();},"late copy failure surfaced");AssetDatabase.FailCopy=null;
        Check(LayoutEditorLevelTemplate.Cleanup(new[]{Scene("partial"),Data("partial")}).Length==0,"partial copy cleaned including scene and metas");
        Directory.CreateDirectory(Data("delete_fail"));File.WriteAllText(Scene("delete_fail"),"fixture");
        AssetDatabase.FailDelete=Scene("delete_fail");AssetDatabase.ThrowDelete=Data("delete_fail");
        var failures=LayoutEditorLevelTemplate.Cleanup(new[]{Scene("delete_fail"),Data("delete_fail")});
        Check(failures.Length==2&&failures[0].Contains(Scene("delete_fail"))&&failures[1].Contains(Data("delete_fail")),"false and throwing deletions both reported");
        AssetDatabase.FailDelete=null;AssetDatabase.ThrowDelete=null;LayoutEditorLevelTemplate.Cleanup(new[]{Scene("delete_fail"),Data("delete_fail")});
        Console.WriteLine("PASS "+count+" production copier checks with real template YAML; Unity integration not executed.");
    }
}
'''


def main():
    with tempfile.TemporaryDirectory(prefix='oc2-template-copy-') as temp:
        out = Path(temp)
        shutil.copytree(ROOT / 'Assets/Template', out / 'Assets/Template')
        (out / 'Assets/LevelSets/test/data').mkdir(parents=True)
        (out / 'Assets/LevelSets/test/scenes').mkdir(parents=True)
        # Resolve just GUIDs used by real templates. External resources are immutable
        # existence fixtures; the production algorithm never copies their contents.
        needed = set()
        for file in (ROOT / 'Assets/Template').rglob('*'):
            if file.is_file():
                needed.update(re.findall(r'guid:\s*([a-f0-9]{32})', file.read_text(encoding='utf-8-sig')))
        index = {}
        for folder, _, files in os.walk(ROOT / 'Assets'):
            for name in files:
                if not name.endswith('.meta'):
                    continue
                meta = Path(folder) / name
                match = re.search(r'(?m)^guid: ([a-f0-9]{32})', meta.read_text(encoding='utf-8-sig'))
                if match and match[1] in needed:
                    asset = meta.with_suffix('')
                    index[str(asset.relative_to(ROOT))] = match[1]
        missing = {g for g in needed - set(index.values()) if not g.startswith('0000000000000000')}
        assert not missing, 'Unresolved real template GUIDs: ' + str(missing)
        for relative in index:
            fixture = out / relative
            if not fixture.exists():
                fixture.parent.mkdir(parents=True, exist_ok=True)
                fixture.write_text('external immutable existence fixture')
        (out / 'index.tsv').write_text('\n'.join(p + '\t' + g for p, g in index.items()))
        (out / 'Program.cs').write_text(HARNESS)
        (out / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (out / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion>'
            '<GenerateAssemblyInfo>false</GenerateAssemblyInfo><NuGetAudit>false</NuGetAudit>'
            '<EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
            '</PropertyGroup><ItemGroup><Compile Include="Program.cs" /><Compile Include="' + escape(str(SOURCE)) + '" />'
            '</ItemGroup></Project>')
        subprocess.run(['dotnet', 'run', '--project', str(out / 'Checks.csproj'), '--', temp, str(out / 'index.tsv')],
                       cwd=out, env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1'), check=True, timeout=120)
    print('LIMIT: API doubles do not execute Unity import callbacks, serialization, scene setup or native mode-store commits.')


if __name__ == '__main__':
    main()
