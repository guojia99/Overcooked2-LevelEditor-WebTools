#!/usr/bin/env python3
"""Execute production partial-order fallback and Harmony postfix with C#4 API doubles.

No Unity/scene writes. Tests processing identity, duplicate counts, assembly and
scene scope; doubles do not validate native networking or Harmony detour installation.
"""
from pathlib import Path
import os
import subprocess
import tempfile
import argparse
from xml.sax.saxutils import escape

from importlib.machinery import SourceFileLoader

ROOT = Path(__file__).resolve().parents[2]
extract = SourceFileLoader('guard_extract', str(Path(__file__).with_name('check-cooking-guard-cache.py'))).load_module().extract
RUNTIME = ROOT / 'Assets/WebCustomStubRuntime/core'

HARNESS = r'''
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
 public class Object { }
 public class Component : Object { public GameObject gameObject; }
 public class GameObject : Object {
  public Scene scene = new Scene(); public Component[] components = new Component[0];
  public T[] GetComponents<T>() where T : Component { return (T[])(object)components; }
 }
 public class Scene { public string path; }
}
public class AssembledDefinitionNode {
 public static AssembledDefinitionNode NullNode = new AssembledDefinitionNode();
 public string id;
 public virtual AssembledDefinitionNode Simpilfy() { return this; }
 public static bool Match(AssembledDefinitionNode a, AssembledDefinitionNode b) {
  if (a.GetType()!=b.GetType()) return false;
  var x=a as Processed; var y=b as Processed;
  if(x!=null && (x.state!=y.state || x.step!=y.step)) return false;
  var c=a as CompositeAssembledNode; var d=b as CompositeAssembledNode;
  return c==null ? a.id==b.id : CompositeAssembledNode.Contains(c.m_composition,d.m_composition)
   && CompositeAssembledNode.Contains(d.m_composition,c.m_composition);
 }
}
public class CompositeAssembledNode : AssembledDefinitionNode {
 public AssembledDefinitionNode[] m_composition=new AssembledDefinitionNode[0], m_optional=new AssembledDefinitionNode[0];
 public override AssembledDefinitionNode Simpilfy() {
  if(m_composition.Length==1 && m_optional.Length==0) return m_composition[0].Simpilfy();
  return this;
 }
 public static bool Contains(AssembledDefinitionNode[] sup, AssembledDefinitionNode[] sub) {
  bool[] used=new bool[sup.Length];
  foreach(var n in sub) { bool found=false; for(int j=0;j<sup.Length;j++)
   if(!used[j] && AssembledDefinitionNode.Match(n,sup[j])) {used[j]=true;found=true;break;}
   if(!found)return false; } return true;
 }
}
public class Processed : CompositeAssembledNode {
 public string state="done",step="pot";
 public override AssembledDefinitionNode Simpilfy() {
  if(state=="raw") return new CompositeAssembledNode {m_composition=m_composition}.Simpilfy();
  return this;
 }
}
public class Cooked : Processed { }
public class Mixed : Processed { }
public class OrderDefinitionNode {
 public UnityEngine.Object m_platingPrefab=new UnityEngine.Object(); public object m_platingStep; public AssembledDefinitionNode root;
 public AssembledDefinitionNode Convert(){return root;}
}
public static class GameUtils {
 public static OrderDefinitionNode[] Orders;
 private static OrderDefinitionNode[] GetAllOrderNodes(){return Orders;}
}
public interface IOrderDefinition { AssembledDefinitionNode GetOrderComposition(); }
public class Source : Component,IOrderDefinition {
 public AssembledDefinitionNode node;
 public AssembledDefinitionNode GetOrderComposition(){return node;}
}
public class Adapter { public AssembledDefinitionNode[] contents; public AssembledDefinitionNode[] GetContents(){return contents;} }
public class Plate : Component {
 public object m_platingStep;
 public AssembledDefinitionNode GetOrderComposition(AssembledDefinitionNode[] c) {
  return c.Length==1 && c[0] is CompositeAssembledNode ? c[0] : new CompositeAssembledNode{m_composition=c};
 }
}
namespace CustomStub {
 static class GameApi { public static Type Find(string s){return typeof(GameApi).Assembly.GetType(s);} }
 static class StubLog { public static void LogWarn(string s){Console.WriteLine(s);} public static void Dbg(string s){} }
 static partial class HarmonyPatches {
  static void WarnOnce(string key,string message){throw new Exception(message);}
  internal static bool Run(Plate plate,GameObject source,Adapter adapter,int count,bool result) {
   CanPlaceOnPlateFallbackPostfix(plate,source,adapter,count,ref result);return result;
  }
 }
}
class Program {
 static int checks;
 static object step=new object();
 static AssembledDefinitionNode T=new AssembledDefinitionNode{id="tomato"}, L=new AssembledDefinitionNode{id="lettuce"};
 static CompositeAssembledNode C(params AssembledDefinitionNode[] n){return new CompositeAssembledNode{m_composition=n};}
 static void Expect(string name,bool expected,bool actual){checks++;if(expected!=actual)throw new Exception(name);}
 static bool Judge(AssembledDefinitionNode input,AssembledDefinitionNode recipe) {
  GameUtils.Orders=new[]{new OrderDefinitionNode{root=recipe,m_platingStep=step}};
  return CustomStub.PlatePartialOrderFallback.CanStillFormOrder(input.Simpilfy(),step,input);
 }
 static int Main(){
  foreach(string state in new[]{"raw","done","burnt"}) {
   var soup=new Cooked{state=state,m_composition=new[]{T,T,T}};
   Expect("ingredient cannot form processed soup "+state,false,Judge(T,soup));
   Expect("one cooked tomato cannot form soup "+state,false,Judge(new Cooked{state=state,m_composition=new[]{T}},soup));
   Expect("two cooked tomatoes cannot form soup "+state,false,Judge(new Cooked{state=state,m_composition=new[]{T,T}},soup));
   Expect("mixed root is atomic "+state,false,Judge(new Mixed{state=state,m_composition=new[]{T}},new Mixed{state=state,m_composition=new[]{T,T}}));
  }
  Expect("salad partial",true,Judge(T,C(T,L)));
  Expect("tea two components",true,Judge(C(T,L),C(T,L,T)));
  Expect("duplicate counts rejected",false,Judge(C(T,T),C(T,L,L)));
  Expect("duplicate counts accepted",true,Judge(C(T,T),C(T,T,L)));
  Expect("wrong ingredient",false,Judge(L,C(T,T)));
  var cooked=new Cooked{m_composition=new[]{T}};
  Expect("cooked component in assembly",true,Judge(cooked,C(cooked,L)));
  Expect("cooked component wrapped in composite",true,Judge(C(cooked),C(cooked,L)));
  var mixed=new Mixed{m_composition=new[]{T}};
  Expect("mixed component in assembly",true,Judge(mixed,C(mixed,L)));
  Expect("cooked component alongside raw salad",true,Judge(C(cooked,T),C(cooked,T,L)));
  Expect("mixed component alongside raw salad",true,Judge(C(mixed,T),C(mixed,T,L)));
  Expect("processed contents cannot become raw salad",false,Judge(cooked,C(T,L)));
  Expect("mixed contents cannot become raw salad",false,Judge(mixed,C(T,L)));
  Expect("complete assembly needs no fallback",false,Judge(C(T,L),C(T,L)));
  Expect("wrong cooking step",false,Judge(new Cooked{step="fry",m_composition=new[]{T}},C(cooked,L)));
  Expect("burnt component",false,Judge(new Cooked{state="burnt",m_composition=new[]{T}},C(cooked,L)));
  Expect("raw source before simplification",false,Judge(new Cooked{state="raw",m_composition=new[]{T}},C(T,L)));
  Expect("unmixed source before simplification",false,Judge(new Mixed{state="raw",m_composition=new[]{T}},C(T,L)));
  Expect("raw candidate child",false,Judge(T,C(new Cooked{state="raw",m_composition=new[]{T}},L)));
  Expect("nested raw source",false,Judge(C(C(new Cooked{state="raw",m_composition=new[]{T}})),C(T,L)));
  Expect("nested raw candidate",false,Judge(T,C(C(new Cooked{state="raw",m_composition=new[]{T}}),L)));
  Expect("empty assembly",false,Judge(C(),C(T,L)));
  Expect("null content",false,CustomStub.PlatePartialOrderFallback.CanStillFormOrder(C((AssembledDefinitionNode)null),step,T));
  var optional=C(T);optional.m_optional=new[]{L};
  Expect("optional assembly",true,Judge(T,optional));
  GameUtils.Orders=new[]{new OrderDefinitionNode{root=C(T,L),m_platingStep=new object()}};
  Expect("different plating step",false,CustomStub.PlatePartialOrderFallback.CanStillFormOrder(T,step,T));
  GameUtils.Orders[0].m_platingStep=step;GameUtils.Orders[0].m_platingPrefab=null;
  Expect("no model",false,CustomStub.PlatePartialOrderFallback.CanStillFormOrder(T,step,T));
  GameUtils.Orders=new[]{new OrderDefinitionNode{root=C(T,L),m_platingStep=step}};
  foreach(string path in new[]{"Assets/LevelSets/test/scenes/s_1.unity","assets/levelsets/TEST/scenes/s_1.unity",
   "Assets/Official/s_1.unity","","Assets/LevelSets/test/other/s_1.unity"}) {
   var plate=new Plate{gameObject=new GameObject(),m_platingStep=step};plate.gameObject.scene.path=path;
   var source=new Source{node=T};var go=new GameObject{components=new Component[]{source}};
   var adapter=new Adapter{contents=new[]{T}};
   bool web=path.IndexOf("/scenes/",StringComparison.OrdinalIgnoreCase)>=0;
   Expect("postfix scene "+path,web,CustomStub.HarmonyPatches.Run(plate,go,adapter,0,false));
   Expect("original success preserved "+path,true,CustomStub.HarmonyPatches.Run(plate,go,adapter,0,true));
   Expect("transfer early refusal "+path,false,CustomStub.HarmonyPatches.Run(plate,go,adapter,1,false));
   source.node=new Cooked{state="raw",m_composition=new[]{T}};
   Expect("postfix raw source "+path,false,CustomStub.HarmonyPatches.Run(plate,go,adapter,0,false));
   source.node=cooked;adapter.contents=new[]{cooked};
   GameUtils.Orders=new[]{new OrderDefinitionNode{root=C(cooked,L),m_platingStep=step}};
   Expect("postfix cooked assembly "+path,web,CustomStub.HarmonyPatches.Run(plate,go,adapter,0,false));
   GameUtils.Orders=new[]{new OrderDefinitionNode{root=new Cooked{m_composition=new[]{T,T,T}},m_platingStep=step}};
   Expect("postfix soup refusal "+path,false,CustomStub.HarmonyPatches.Run(plate,go,adapter,0,false));
   Expect("native complete soup success "+path,true,CustomStub.HarmonyPatches.Run(plate,go,adapter,0,true));
   GameUtils.Orders=new[]{new OrderDefinitionNode{root=C(T,L),m_platingStep=step}};
  }
  Console.WriteLine("PASS: "+checks+" behavior assertions (production fallback/postfix, C#4/net8 API doubles)");return 0;
 }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--compile-runtime', action='store_true', help='Also compile full runtime C#4/net35 using offline-check host references')
    args = parser.parse_args()
    source = (RUNTIME / 'HarmonyPatches.cs').read_text()
    members = [extract(source, r'private static (?:System\.Reflection\.(?:MethodInfo|FieldInfo)|System\.Type) ' + name + r'\s*;', field=True)
               for name in ('s_plateGetContents', 's_plateGetContentsType', 's_plateGetComposition', 's_plateStepField', 's_plateSourceDefinitionType')]
    members += [extract(source, r'private static (?:int|void) ' + name + r'\(')
                for name in ('SnapshotContentsCount', 'CanPlaceOnPlateFallbackPostfix')]
    with tempfile.TemporaryDirectory(prefix='oc2-plate-') as tmp:
        folder = Path(tmp)
        (folder / 'Harness.cs').write_text(HARNESS)
        (folder / 'Hooks.cs').write_text('using UnityEngine; namespace CustomStub { static partial class HarmonyPatches {\n' + '\n'.join(members) + '\n}}')
        (folder / 'Fallback.cs').write_text((RUNTIME / 'PlatePartialOrderFallback.cs').read_text())
        (folder / 'Check.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo><NuGetAudit>false</NuGetAudit></PropertyGroup></Project>')
        subprocess.run(['dotnet', 'run', '--project', str(folder / 'Check.csproj'), '-v:q'], check=True,
                       env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1'))
        if args.compile_runtime:
            build_folder = folder / 'runtime'
            build_folder.mkdir()
            project = build_folder / 'Runtime.csproj'
            refs = list(Path('/Applications/Unity/Hub/Editor/2017.4.8f1/Unity.app/Contents/Managed/UnityEngine').glob('*.dll'))
            refs += [ROOT / 'layout-editor/offline-check/bin' / name for name in ('Assembly-CSharp.dll', 'LevelEditorStub.dll')]
            refs += [ROOT / 'Assets/Plugins/0Harmony.dll']
            assert len(refs) > 10 and all(p.is_file() for p in refs), 'Unity/offline-check reference DLLs required'
            sources = [p for p in RUNTIME.parent.rglob('*.cs') if not any(part.endswith('~') for part in p.relative_to(RUNTIME.parent).parts)]
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net35</TargetFramework><LangVersion>4</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><GenerateAssemblyInfo>false</GenerateAssemblyInfo><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" />'
                + ''.join('<Compile Include="' + escape(str(p)) + '" />' for p in sources)
                + ''.join('<Reference Include="' + escape(str(p)) + '" />' for p in refs)
                + '</ItemGroup></Project>')
            subprocess.run(['dotnet', 'build', str(project), '--nologo', '-v:q'], check=True)
            print('PASS: full runtime C#4/net35 compile; existing offline host references, temporary outputs only')


if __name__ == '__main__':
    main()
