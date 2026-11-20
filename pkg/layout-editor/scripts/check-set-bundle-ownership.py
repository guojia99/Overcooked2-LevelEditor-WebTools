#!/usr/bin/env python3
"""Execute production ownership/collection with Unity doubles under C#4."""
from pathlib import Path
import os
import subprocess
import tempfile
import sys
from importlib.util import spec_from_file_location, module_from_spec

ROOT = Path(__file__).resolve().parents[2]
EDITOR = ROOT / "Assets/Editor/LayoutEditor"
spec = spec_from_file_location("preflight", Path(__file__).with_name("check-optional-bundle-preflight.py"))
module = module_from_spec(spec)
sys.dont_write_bytecode = True
spec.loader.exec_module(module)
exporter = (EDITOR / "LayoutEditorSetExporter.cs").read_text()
content = (EDITOR / "LayoutEditorCustomContentApi.cs").read_text()
methods = "\n".join(module.extract(exporter, r"    private static [^\n]+\b" + name + r"\(")
                    for name in ("AddBundleName", "CollectLevelBundleNames", "CollectBuildOwnership"))
setter = module.extract(content, r"    private static void InheritInfoBundle\(")
HARNESS = r'''
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
namespace UnityEditor {
 public class AssetImporter {
  public string assetBundleName = "", assetBundleVariant = "";
  public static Dictionary<string, AssetImporter> Items = new Dictionary<string, AssetImporter>();
  public static int Writes;
  public static AssetImporter GetAtPath(string p) { AssetImporter i; return Items.TryGetValue(p, out i) ? i : null; }
  public void SetAssetBundleNameAndVariant(string n, string v) { assetBundleName=n; assetBundleVariant=v; }
  public void SaveAndReimport() { Writes++; }
 }
 public static class AssetDatabase {
  public static Dictionary<string,string[]> Deps = new Dictionary<string,string[]>();
  public static List<string> Removed = new List<string>();
  public static bool IsValidFolder(string p) { return Directory.Exists(p); }
  public static string[] GetAllAssetPaths() { var a=new string[AssetImporter.Items.Count]; AssetImporter.Items.Keys.CopyTo(a,0); return a; }
  public static string[] GetDependencies(string[] p, bool recursive) { return GetAllAssetPaths(); }
  public static string[] GetAssetBundleDependencies(string n, bool recursive) { string[] a; return Deps.TryGetValue(n,out a)?a:new string[0]; }
  public static string[] GetAssetPathsFromAssetBundle(string n) {
   var a=new List<string>(); foreach(var p in AssetImporter.Items) if(p.Value.assetBundleName==n) a.Add(p.Key); return a.ToArray();
  }
  public static void RemoveAssetBundleName(string n, bool force) { if(GetAssetPathsFromAssetBundle(n).Length!=0) throw new Exception("removed live name"); Removed.Add(n); }
 }
}
class Program {
 const string LevelSetsRoot="Assets/LevelSets";
 static int checks;
 static void Check(bool ok) { checks++; if(!ok) throw new Exception("assertion "+checks); }
 static void Asset(string p,string n,string v,bool folder) {
  if(folder) Directory.CreateDirectory(p); else { Directory.CreateDirectory(Path.GetDirectoryName(p)); File.WriteAllText(p, ""); }
  UnityEditor.AssetImporter.Items[p]=new UnityEditor.AssetImporter { assetBundleName=n,assetBundleVariant=v };
 }
 static void Main() {
  var root="Assets/LevelSets/alpha";
  Asset(root,"old/info_old","variant",true);
  Asset(root+"/scenes","old/scenes","",true);
  Asset(root+"/scenes/s_one.unity","old/s_one","x",false);
  Asset(root+"/scenes/nested/s_two.unity","old/s_two","",false);
  Asset(root+"/custom_content","alpha/custom_content","",true);
  Asset(root+"/custom_content/model.prefab","alpha/custom_content","v",false);
  Asset(root+"/custom_recipes/recipe.asset","alpha/custom_recipes","",false);
  Asset(root+"/data/revision","old/data","",true);
  Asset("Assets/commonW5/shared.asset","commonw5","shared",false);
  Asset("Assets/LevelSets/beta/data.asset","beta/info_beta","",false);
  LayoutEditorSetBundleOwnership.Normalize("alpha");
  foreach(var p in UnityEditor.AssetImporter.Items) {
   if(!p.Key.StartsWith(root+"/") && p.Key!=root) continue;
   var expected=p.Key==root?"alpha/info_alpha":p.Key.EndsWith(".unity")?"alpha/"+Path.GetFileNameWithoutExtension(p.Key):"";
   Check(p.Value.assetBundleName==expected && p.Value.assetBundleVariant=="");
  }
  Check(UnityEditor.AssetImporter.Items["Assets/commonW5/shared.asset"].assetBundleVariant=="shared");
  Check(UnityEditor.AssetImporter.Items["Assets/LevelSets/beta/data.asset"].assetBundleName=="beta/info_beta");
  Check(UnityEditor.AssetDatabase.Removed.Contains("old/info_old.variant"));
  int writes=UnityEditor.AssetImporter.Writes;
  LayoutEditorSetBundleOwnership.Normalize("alpha"); Check(writes==UnityEditor.AssetImporter.Writes);
  var pairs=new List<KeyValuePair<string,List<string>>>();
  pairs.Add(new KeyValuePair<string,List<string>>("alpha",new List<string>(new[]{root+"/scenes/s_one.unity"})));
  var names=CollectLevelBundleNames(pairs,true);
  Check(names.Count==2 && names.Contains("alpha/info_alpha") && names.Contains("alpha/s_one"));
  pairs.Add(new KeyValuePair<string,List<string>>("beta",new List<string>()));
  var merged=CollectLevelBundleNames(pairs,false);
  Check(merged.Count==3 && merged.Contains("beta/info_beta"));
  UnityEditor.AssetDatabase.Deps["alpha/info_alpha"]=new[]{"commonw5"};
  UnityEditor.AssetDatabase.Deps["commonw5"]=new[]{"common01"};
  Check(CollectBuildOwnership(names).Count==4); Check(names.Count==2);
  Asset(root+"/custom_content/copied.prefab","common04","v",false);
  InheritInfoBundle(root+"/custom_content/copied.prefab");
  Check(UnityEditor.AssetImporter.Items[root+"/custom_content/copied.prefab"].assetBundleName=="");
  Check(UnityEditor.AssetImporter.Items[root+"/custom_content/copied.prefab"].assetBundleVariant=="");
  File.WriteAllText(root+"/custom_recipes/recipe.asset","- bundleName: old/custom_recipes\n");
  writes=UnityEditor.AssetImporter.Writes;
  bool rejected=false;
  try { LayoutEditorSetBundleOwnership.Normalize("alpha"); } catch(InvalidOperationException e) { rejected=e.Message.Contains("recipe.asset"); }
  Check(rejected); Check(writes==UnityEditor.AssetImporter.Writes);
  File.WriteAllText(root+"/custom_recipes/recipe.asset","");
  Asset(root+"/outside.unity","old/outside","",false);
  rejected=false; try { LayoutEditorSetBundleOwnership.Normalize("alpha"); } catch(InvalidOperationException) { rejected=true; }
  Check(rejected); Check(writes==UnityEditor.AssetImporter.Writes);
  UnityEditor.AssetImporter.Items.Remove(root+"/outside.unity");
  Asset(root+"/scenes/info_alpha.unity","old/collision","",false);
  rejected=false; try { LayoutEditorSetBundleOwnership.Normalize("alpha"); } catch(InvalidOperationException) { rejected=true; }
  Check(rejected); Check(writes==UnityEditor.AssetImporter.Writes);
  UnityEditor.AssetImporter.Items.Remove(root+"/scenes/info_alpha.unity");
  File.WriteAllText(root+"/custom_recipes/recipe.asset","m_bundleName: 'CUSTOM_CONTENT'\n");
  rejected=false; try { LayoutEditorSetBundleOwnership.Normalize("alpha"); } catch(InvalidOperationException) { rejected=true; }
  Check(rejected); Check(writes==UnityEditor.AssetImporter.Writes);
  File.WriteAllText(root+"/custom_recipes/recipe.asset","");
  Asset(root+"/scenes/other/s_one.unity","duplicate","",false);
  rejected=false; try { LayoutEditorSetBundleOwnership.Normalize("alpha"); } catch(InvalidOperationException) { rejected=true; }
  Check(rejected); Check(writes==UnityEditor.AssetImporter.Writes);
  Console.WriteLine("PASS: "+checks+" ownership/partial/shared/string-reference assertions (production C#4 methods; Unity doubles)");
 }
'''

with tempfile.TemporaryDirectory(prefix="set-ownership-") as temp:
    directory = Path(temp)
    (directory / "Ownership.cs").write_text((EDITOR / "LayoutEditorSetBundleOwnership.cs").read_text())
    (directory / "Program.cs").write_text(HARNESS + methods + setter + "\n}\n")
    (directory / "Check.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo></PropertyGroup></Project>')
    (directory / "NuGet.Config").write_text('<configuration><packageSources><clear /></packageSources></configuration>')
    env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
    result = subprocess.run(["dotnet", "run", "--project", str(directory / "Check.csproj")], cwd=temp, env=env, timeout=90)
    raise SystemExit(result.returncode)
