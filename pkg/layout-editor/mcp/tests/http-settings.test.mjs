import assert from "node:assert/strict";
import { readFileSync, mkdtempSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { execFileSync } from "node:child_process";
import { createRequire } from "node:module";
import { test } from "node:test";
import { settingsToolDefinitions } from "../dist/level-settings.js";

// Compile and execute extracted production RPC/settings methods in C#4.
// JSON and persistence doubles do not validate real Unity JsonUtility or assets.
function block(source, marker) {
  const start = source.indexOf(marker);
  assert.notEqual(start, -1, marker);
  const brace = source.indexOf("{", start);
  const tokens = /@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|\/\/[^\n]*|\/\*[\s\S]*?\*\/|[{}]/g;
  tokens.lastIndex = brace;
  let depth = 0;
  for (let match; (match = tokens.exec(source));) {
    if (match[0] === "{") depth++;
    if (match[0] === "}" && --depth === 0) return source.slice(start, tokens.lastIndex);
  }
  throw new Error(marker);
}

test("Unity HTTP production RPC validates settings/conversion and verifies single-save readback in C#4", { timeout: 120000 }, () => {
  const root = new URL("../../../", import.meta.url);
  const http = readFileSync(new URL("Assets/Editor/LayoutEditor/LayoutEditorHttpServer.cs", root), "utf8");
  const models = readFileSync(new URL("Assets/Editor/LayoutEditor/LayoutEditorModels.cs", root), "utf8");
  const methods = ["private static string HandleMcpRpc(", "private sealed class McpModeToolException", "private static string McpModeErrorResult(", "private static void RequireMcpModePath(", "private sealed class McpSettingsArguments", "private static LevelDetailDto ReadMcpLevelDetail(", "private static string HandleMcpSettingsTool(", "private static Dictionary<string, string> McpObjectProperties(", "private static string McpResult(", "private static string McpError(", "private static string JsonString(", "private static string EscapeJson(", "private static string ExtractJsonProperty("].map((m) => block(http, m).replace("private sealed class McpSettingsArguments", "[Serializable] private sealed class McpSettingsArguments")).join("\n");
  const dtos = ["McpRpcRequestDto", "McpRpcParamsDto", "McpArgumentsDto", "PerPlayerConfigDto", "LevelDetailDto", "LevelInfoUpdateDto", "LevelConfigUpdateDto"].map((name) => block(models, `public class ${name}\n`)).join("\n");
  const harness = `using System; using System.IO; using System.Text; using System.Collections.Generic;
using System.Text.Json;
public static class JsonUtility {
 static JsonSerializerOptions Options=new JsonSerializerOptions{IncludeFields=true};
 public static T FromJson<T>(string json){return JsonSerializer.Deserialize<T>(json,Options);}
 public static string ToJson(object value){return JsonSerializer.Serialize(value,Options);}
}
public static class LayoutEditorJson { public static string ToJson(object value){return JsonUtility.ToJson(value);} }
public class LayoutDocumentDto{} public class AudioConfigDto{} public class LevelModeConfigDto{}
public static class LayoutEditorLevelModeStore { public const int MaxJsonBytes=2097152; public static void ParseJson(string json){} }
public static class LayoutEditorWriteBackHistory { public static int commits,aborts; public static void BeginForInfo(string kind,string path){} public static void CommitNow(){commits++;} public static void Abort(){aborts++;} }
${dtos}
public static class LayoutEditorLevelAdminApi {
 public static LevelDetailDto Detail; public static int reads,writes; public static bool Fail,Corrupt; public static LevelInfoUpdateDto Info; public static LevelConfigUpdateDto Config;
 public static LevelDetailDto GetLevel(string path){reads++;var value=JsonUtility.FromJson<LevelDetailDto>(JsonUtility.ToJson(Detail)); if(Corrupt&&reads>1)value.configs[1].scriptedRecipes=new[]{Checks.A};return value;}
 public static string UpdateLevelInfo(LevelInfoUpdateDto info){writes++;Info=info;if(Fail)throw new Exception("lost");Detail.enableRespawnBounds=info.enableRespawnBounds;return null;}
 public static string UpdateLevelConfig(LevelConfigUpdateDto config){writes++;Config=config;if(Fail)throw new Exception("lost");for(int p=1;p<=4;p++){var selected=(PerPlayerConfigDto)typeof(LevelConfigUpdateDto).GetField("config_"+p+"p").GetValue(config);if(selected!=null){Detail.configs[p-1]=selected;selected.scriptedRecipeErrors=new string[0];}}return null;}
}
public class Rpc {
 ${methods}
 private static void ValidateMcpModeJson(string json,Type type){}
 private static string HandleMcpTool(string name,McpArgumentsDto args){return "{\\"convert\\":"+(args.convertHordeObjects?"true":"false")+"}";}
 public static string Call(string json){return HandleMcpRpc(json);}
}
public class Checks {
 public const string A="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",B="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
 const string Path="Assets/LevelSets/test/data/one/info.asset";
 static int count;
 static void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
 static void Reset(string mode){LayoutEditorLevelAdminApi.reads=0;LayoutEditorLevelAdminApi.writes=0;LayoutEditorLevelAdminApi.Fail=false;LayoutEditorLevelAdminApi.Corrupt=false;LayoutEditorLevelAdminApi.Config=null;var configs=new PerPlayerConfigDto[4];for(int p=0;p<4;p++)configs[p]=new PerPlayerConfigDto{exists=true,roundTime=777,orderLifeTime=99,survivalTimeMultiplier=1.25f,targetRepairCostMax=123,scriptedRecipes=new[]{A,"",B},scriptedRecipeErrors=new[]{"missing"}};LayoutEditorLevelAdminApi.Detail=new LevelDetailDto{levelInfoAssetPath=Path,gameMode=mode,enableRespawnBounds=true,debugRecipeCount=7,disableDynamicParenting=true,gridHalfSizeX=12,gridHalfSizeY=8,gridHalfSizeZ=13,minOrderCount=2,maxOrderCount=4,dependencies=new[]{"bundle1"},configs=configs};}
 static string Call(string name,string args){return Rpc.Call("{\\"jsonrpc\\":\\"2.0\\",\\"id\\":1,\\"method\\":\\"tools/call\\",\\"params\\":{\\"name\\":\\""+name+"\\",\\"arguments\\":"+args+"}}");}
 static string Args(string rest){return "{\\"assetPath\\":\\""+Path+"\\",\\"confirm\\":true,"+rest+"}";}
 static void Invalid(string name,string args){Reset("Campaign");var result=Call(name,args);Check(result.Contains("INVALID_ARGUMENT"),"reject "+args);Check(LayoutEditorLevelAdminApi.writes==0&&LayoutEditorLevelAdminApi.reads==0,"no calls before validation");}
 public static void Main(){
 const string R="oc2_save_level_respawn_bounds",S="oc2_save_native_scripted_recipes",M="oc2_change_level_mode";
 foreach(var mode in new[]{"Campaign","Dynamic","Horde"})foreach(var flag in new[]{"true","false"}){Reset(mode);Check(Call(R,Args("\\"enableRespawnBounds\\":"+flag)).Contains("\\"verified\\":true"),"respawn verified");var i=LayoutEditorLevelAdminApi.Info;Check(i.updateEnableRespawnBounds&&i.enableRespawnBounds==(flag=="true")&&i.debugRecipeCount==7&&i.disableDynamicParenting&&i.gridHalfSizeY==8&&i.dependencies[0]=="bundle1","info merged");Check(LayoutEditorLevelAdminApi.reads==2&&LayoutEditorLevelAdminApi.writes==1,"GET save GET");}
 foreach(var guids in new[]{"[]","[\\""+B+"\\",\\""+A+"\\",\\""+B+"\\"]"}){Reset("Campaign");Check(Call(S,Args("\\"playerCount\\":2,\\"scriptedRecipes\\":"+guids)).Contains("\\"verified\\":true"),"sequence verified");var c=LayoutEditorLevelAdminApi.Config;Check(c.config_1p==null&&c.config_3p==null&&c.config_4p==null&&c.config_2p.roundTime==777&&c.config_2p.orderLifeTime==99&&c.config_2p.survivalTimeMultiplier==1.25f&&c.config_2p.targetRepairCostMax==123,"only selected merged");Check(c.config_2p.scriptedRecipes.Length==(guids=="[]"?0:3),"order duplicates clear");}
 foreach(var raw in new[]{"null","0","1","\\"false\\"","[]","{}"})Invalid(R,Args("\\"enableRespawnBounds\\":"+raw));
 foreach(var raw in new[]{"null","\\"1\\"","true","0","5","1.5"})Invalid(S,Args("\\"playerCount\\":"+raw+",\\"scriptedRecipes\\":[]"));
 foreach(var raw in new[]{"null","{}","\\"abc\\"","[\\"\\"]","[1]","[null]"})Invalid(S,Args("\\"playerCount\\":1,\\"scriptedRecipes\\":"+raw));
 Invalid(S,Args("\\"playerCount\\":1"));Invalid(R,Args("\\"enableRespawnBounds\\":false,\\"unknown\\":true"));Invalid(R,Args("\\"enableRespawnBounds\\":false,\\"enableRespawnBounds\\":true"));
 Invalid(R,"{\\"assetPath\\":\\""+Path+"\\",\\"enableRespawnBounds\\":true,\\"confirm\\":\\"true\\"}");
 foreach(var mode in new[]{"Dynamic","Horde"}){Reset(mode);Check(Call(S,Args("\\"playerCount\\":1,\\"scriptedRecipes\\":[]")).Contains("INVALID_ARGUMENT")&&LayoutEditorLevelAdminApi.writes==0,"non Campaign zero writes");}
 Reset("Campaign");LayoutEditorLevelAdminApi.Detail.configs[1].exists=false;Check(Call(S,Args("\\"playerCount\\":2,\\"scriptedRecipes\\":[]")).Contains("INVALID_ARGUMENT"),"missing config");
 Reset("Campaign");LayoutEditorLevelAdminApi.Fail=true;Check(Call(R,Args("\\"enableRespawnBounds\\":false")).Contains("WRITE_UNCONFIRMED")&&LayoutEditorLevelAdminApi.writes==1&&LayoutEditorLevelAdminApi.reads==1,"lost write no retry");
 Reset("Campaign");LayoutEditorLevelAdminApi.Corrupt=true;Check(Call(S,Args("\\"playerCount\\":2,\\"scriptedRecipes\\":[]")).Contains("READBACK_FAILED")&&LayoutEditorLevelAdminApi.writes==1,"bad readback no retry");
 foreach(var flag in new[]{"true","false"}){Reset("Campaign");Check(Call(M,Args("\\"gameMode\\":\\"Horde\\",\\"convertHordeObjects\\":"+flag)).Contains("\\"convert\\":"+flag),"conversion boolean accepted");}
 foreach(var flag in new[]{"null","0","1","\\"true\\"","[]","{}"})Invalid(M,Args("\\"gameMode\\":\\"Horde\\",\\"convertHordeObjects\\":"+flag));
 Console.WriteLine("PASS "+count+" production C#4 RPC assertions");Console.WriteLine(LayoutEditorMcpApi.ToolsJson());
 }
}`;
  const temp = mkdtempSync(join(tmpdir(), "oc2-http-settings-"));
  try {
    writeFileSync(join(temp, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
    writeFileSync(join(temp, "Program.cs"), harness);
    const apiPath = new URL("Assets/Editor/LayoutEditor/LayoutEditorMcpApi.cs", root).pathname;
    writeFileSync(join(temp, "Checks.csproj"), `<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><LangVersion>4</LangVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><Compile Include="${apiPath}" /></ItemGroup></Project>`);
    const output = execFileSync("dotnet", ["run", "--project", join(temp, "Checks.csproj")], { encoding: "utf8", timeout: 110000, maxBuffer: 2 * 1024 * 1024 });
    assert.match(output, /PASS \d+ production C#4 RPC assertions/);
    const tools = JSON.parse(output.trim().split("\n").at(-1));
    const require = createRequire(import.meta.url);
    const sdkRequire = createRequire(require.resolve("@modelcontextprotocol/sdk/server/index.js"));
    const Ajv = sdkRequire("ajv/dist/2020.js").default;
    const ajv = new Ajv({ strict: false });
    for (const name of ["oc2_save_level_respawn_bounds", "oc2_save_native_scripted_recipes"]) {
      const tool = tools.find((t) => t.name === name);
      assert.equal(tool.sideEffect, "write_asset");
      assert.equal(tool.inputSchema.additionalProperties, false);
      assert.equal(tool.inputSchema.properties.confirm.const, true);
      assert.ok(new RegExp(tool.inputSchema.properties.assetPath.pattern).test("Assets/LevelSets/test/data/one/info.asset"));
      assert.equal(tool.safeToRetry, false);
      const httpValid = ajv.compile(tool.inputSchema);
      const stdioValid = ajv.compile(settingsToolDefinitions.find((t) => t.name === name).inputSchema);
      const path = "Assets/LevelSets/test/data/one/info.asset";
      const input = name === "oc2_save_level_respawn_bounds" ? { assetPath: path, enableRespawnBounds: false, confirm: true } : { assetPath: path, playerCount: 3, scriptedRecipes: [], confirm: true };
      for (const candidate of [input, { ...input, confirm: false }, { ...input, confirm: "true" }, { ...input, unknown: true }, { ...input, assetPath: "bad" }, { ...input, playerCount: "3" }, { ...input, scriptedRecipes: null }]) {
        assert.equal(httpValid(candidate), stdioValid(candidate), JSON.stringify(candidate));
      }
    }
  } finally { rmSync(temp, { recursive: true, force: true }); }
});
