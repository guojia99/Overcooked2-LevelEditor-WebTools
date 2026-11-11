#!/usr/bin/env node
// Web 寿司家族（commonW5/custom_recipes/sushi）：
//   菜谱：Web_Sushi_Prawn（Web 鲜虾寿司 60 分 = 海苔+煮寿司米+寿司虾）
//         Web_Sushi_Prawn_Cucumber（Web 虾什锦寿司 80 分 = 海苔+煮寿司米+寿司虾+青瓜）
//         Web_Sushi_GrandSlam（Web 大满贯寿司 100 分 = 海苔+煮寿司米+寿司鱼+寿司虾+青瓜）
//         Web_Sushi_SashimiCombo（Web 生鱼生虾拼盘 40 分 = 寿司鱼+寿司虾直接装盘，
//           模型=CompositeSushi 载体→查找表 [鱼+虾]→鱼虾拼盘组合模板，真图图标）
//   中间产物：Web_Sushi_RicePrep（type=Cooked：SushiRiceSO + Pot，score=0）。
//   结构完全对齐卷饼家族（Web_Burrito_RicePrep = Cooked(Rice+Pot) + 成品 Composite
//   直引 common01 食材 PseudoPrefabSO）：运行时 RecipeHelper / WebCustomStubRuntime
//   生成的 CookedCompositeOrderNode 与原版 BoiledSushiRice 结构等价匹配。
//   模型占位（原版，编号经原版 SushiCosmeticPrefabs 查找表实证）：
//     m_recipe_sushi_01=鱼生寿司、02=生鱼单盘、03=黄瓜寿司、04=什锦寿司。
//   ⚠ commonW1 旧 SO 命名与实际内容不符（Sushi_Cucumber_SO→02、Sushi_All_SO→03），
//   故鲜虾直接引 Sushi_Fish_SO（→01，名副其实），虾什锦新建本地
//   PseudoPrefabSO 直指 m_recipe_sushi_03。
//   ✅ 大满贯寿司用本地 5 件套模型（原 4 件外扩 + 中央虾寿司）：网格/贴图由
//   layout-editor/scripts/sushi/gen_sushi_grandslam_model.py 生成，本脚本只生成
//   .mat / shader / prefab / meta 并把菜谱 model 指向该 prefab。
//   含虾组合的「卷兜底」视觉（OptionalSushi 载体 + 原版查找表无虾条目 →
//   Plated_Sushi_Wrong_01）由 WebCustomStubRuntime/Sushi/SushiPrawnCombos 运行时
//   扩展查找表修复；含「鱼+虾」的中间组合（跨原版两张 optional 表，无节点可
//   匹配 → 黑块）由 Web_Sushi_OptionalCombo 通配可选节点（保存菜谱时
//   SyncSushiOptionalCombo 自动注册）+ 查找表中间态条目（卷）修复；
//   图标：鲜虾/虾什锦/大满贯均用 backup_rebuild 真图（绝对路径），
//   目标同名 PNG 覆盖写入（meta 不变）：
//     Assets/commonW5/custom_recipes/sushi/icons/Web_Sushi_*.png
//     layout-editor/web/public/icons/recipes/Web_Sushi_*.png
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const W5 = "Assets/commonW5";
const apply = process.argv.includes("--apply");

const md5 = (s) => crypto.createHash("md5").update(s).digest("hex");
const recipeGuid = (id) => md5(`commonw5-recipe:${id}`);
const recipeIconGuid = (id) => md5(`commonw5-recipe-icon:${id}`);
const folderGuid = (rel) => md5(`commonw5-folder:${rel}`);
const sushiModelGuid = (id) => md5(`commonw5-sushi-model:${id}`);
const localModelGuid = (kind, id) => md5(`commonw5-sushi-model2:${kind}:${id}`);

// ── 大满贯寿司 5 件套本地模型（gen_sushi_grandslam_model.py 生成网格/贴图） ──
const GRANDSLAM_MODEL_ID = "Web_Sushi_Model_GrandSlam";
const GRANDSLAM_MESH = "m_recipe_sushi_05";
const GRANDSLAM_MODEL_GUID = localModelGuid("prefab", GRANDSLAM_MODEL_ID);
const GRANDSLAM_MESH_GUID = localModelGuid("mesh", GRANDSLAM_MESH);
const GRANDSLAM_SHADER_GUID = localModelGuid("shader", "WebSushiFood");
const grandslamModelJson = JSON.parse(
  fs.readFileSync(path.join(repoRoot, "layout-editor/scripts/data/sushi-grandslam-model.json"), "utf8"),
);
const grandslamMaterials = grandslamModelJson.materials;

const SCRIPT_CUSTOM_RECIPE = "83fb008bcc8e793429b02c178c430815";
const SCRIPT_RECIPE_CONFIG = "5edb07060a0c6c140aa72ffb8fd3f021";
const SCRIPT_PSEUDO_PREFAB = "0cff7c13895ab9e47a5e02d4619cc3b9";
const UID_PREFIX = 58323;
const NEXT_SEQUENCE = 29;

function readGuid(rel) {
  const abs = path.join(repoRoot, rel);
  if (!fs.existsSync(abs)) throw new Error("缺少资产 meta: " + rel);
  const m = fs.readFileSync(abs, "utf8").match(/^guid: ([0-9a-f]{32})$/m);
  if (!m) throw new Error("无法解析 guid: " + rel);
  return m[1];
}

const GUID = {
  plate: readGuid("Assets/common01/food/PlatingSteps/Plate.asset.meta"),
  pot: readGuid("Assets/common01/food/CookingSteps/Pot.asset.meta"),
  potIcon: readGuid("Assets/common01/food/CookingSteps/PotIcon.asset.meta"),
  seaweed: readGuid("Assets/common01/food/Ingredients/SeaweedSO.asset.meta"),
  sushiRice: readGuid("Assets/common01/food/Ingredients/SushiRiceSO.asset.meta"),
  sushiPrawn: readGuid("Assets/common01/food/Ingredients/SushiPrawnSO.asset.meta"),
  sushiFish: readGuid("Assets/common01/food/Ingredients/SushiFishSO.asset.meta"),
  cucumber: readGuid("Assets/common01/food/Ingredients/CucumberSO.asset.meta"),
  // 米饭装盘视觉（复用卷饼家族已验证的 plated_rice 模型，只引用不修改）
  riceModel: readGuid("Assets/commonW5/custom_recipes/burrito/models/Model_Plated_Rice.asset.meta"),
  // 原版寿司成品模型：01（鱼寿司，commonW1 SO 名实相符直引）；
  //  03/04 由下方 MODEL_ASSETS 生成（commonW1 同名 SO 实际指向 02/03，不可用）。
  modelPrawn: readGuid("Assets/commonW1/pseudo_prefab_so/core/decor/recipes/Sushi_Fish_SO.asset.meta"),
  modelPrawnCucumber: sushiModelGuid("Web_Sushi_Model_Cucumber"),
  modelGrandSlam: sushiModelGuid("Web_Sushi_Model_All"),
};

// 本地模型 PseudoPrefabSO（bundle47 原版成品模型直指，避免 commonW1 命名陷阱）。
const MODEL_ASSETS = [
  {
    id: "Web_Sushi_Model_Cucumber",
    note: "黄瓜寿司成品模型（m_recipe_sushi_03）",
    prefabName: "m_recipe_sushi_03",
  },
  {
    id: "Web_Sushi_Model_All",
    note: "什锦寿司成品模型（m_recipe_sushi_04）",
    prefabName: "m_recipe_sushi_04",
  },
  {
    id: "Web_Sushi_CompositeCarrier",
    note: "原版寿司装盘载体（CompositeSushi，组合查找表驱动视觉）",
    prefabName: "CompositeSushi",
    subPath: "overcooked_legacy/meals",
  },
];

const RECIPES = [
  {
    id: "Web_Sushi_RicePrep",
    zh: "Web 寿司米饭（中间）",
    en: "Web Sushi Rice (Intermediate)",
    type: 2,
    score: 0,
    uid: 58323023,
    composition: [{ guid: GUID.sushiRice }],
    modelSO: GUID.riceModel,
    iconSource: null,
    cooking: true,
    mixing: false,
  },
  {
    id: "Web_Sushi_Prawn",
    zh: "Web 鲜虾寿司",
    en: "Web Prawn Sushi",
    type: 1,
    score: 60,
    uid: 58323024,
    composition: [{ guid: GUID.seaweed }, { recipe: "Web_Sushi_RicePrep" }, { guid: GUID.sushiPrawn }],
    modelSO: GUID.modelPrawn,
    iconSource: "/Users/guojia/Desktop/oc2_editor/backup_rebuild_20260917_132624/虾寿司.png",
    cooking: false,
    mixing: false,
  },
  {
    id: "Web_Sushi_Prawn_Cucumber",
    zh: "Web 虾什锦寿司",
    en: "Web Prawn & Cucumber Sushi",
    type: 1,
    score: 80,
    uid: 58323025,
    composition: [
      { guid: GUID.seaweed },
      { recipe: "Web_Sushi_RicePrep" },
      { guid: GUID.sushiPrawn },
      { guid: GUID.cucumber },
    ],
    modelSO: GUID.modelPrawnCucumber,
    iconSource: "/Users/guojia/Desktop/oc2_editor/backup_rebuild_20260917_132624/虾什锦寿司.png",
    cooking: false,
    mixing: false,
  },
  {
    id: "Web_Sushi_GrandSlam",
    zh: "Web 大满贯寿司",
    en: "Web Grand Slam Sushi",
    type: 1,
    score: 100,
    uid: 58323026,
    composition: [
      { guid: GUID.seaweed },
      { recipe: "Web_Sushi_RicePrep" },
      { guid: GUID.sushiFish },
      { guid: GUID.sushiPrawn },
      { guid: GUID.cucumber },
    ],
    modelGuid: GRANDSLAM_MODEL_GUID,
    iconSource: "/Users/guojia/Desktop/oc2_editor/backup_rebuild_20260917_132624/大满贯寿司.png",
    cooking: false,
    mixing: false,
  },
  {
    // 生鱼生虾拼盘：两片刺身直接装盘（同原版 生鱼片/生虾片 各 20 分 → 拼盘 40）。
    // 模型走 CompositeSushi 载体 → 查找表 [鱼+虾] → 鱼虾拼盘组合模板
    //（SushiPrawnCombos 运行时追加，与中间态同一视觉）。
    id: "Web_Sushi_SashimiCombo",
    zh: "Web 生鱼生虾拼盘",
    en: "Web Fish & Prawn Sashimi",
    type: 1,
    score: 40,
    uid: 58323028,
    composition: [{ guid: GUID.sushiFish }, { guid: GUID.sushiPrawn }],
    modelSO: sushiModelGuid("Web_Sushi_CompositeCarrier"),
    iconSource: "/Users/guojia/Desktop/oc2_editor/backup_rebuild_20260917_132624/鱼虾.png",
    cooking: false,
    mixing: false,
  },
  {
    // 通配可选节点（原版 OptionalSushi 的五材版）：composition 空 + optional=
    // [海苔,米饭,青瓜,鱼,虾]，装盘=Plate、模型=原版 CompositeSushi 载体。
    // 注册进关卡 optionalRecipeMatchListItems 后，含「鱼+虾」的任意中间组合
    // 都能匹配到节点 → 载体按（运行时扩展后的）SushiCosmeticPrefabs 查找表
    // 渲染卷/成品视觉，而不是 GetOrderPlatingPrefab=null 的黑块。
    id: "Web_Sushi_OptionalCombo",
    zh: "Web 寿司组合通配（可选匹配）",
    en: "Web Sushi Combo Wildcard (Optional)",
    type: 1,
    score: 0,
    uid: 58323027,
    composition: [],
    optional: [
      { guid: GUID.seaweed },
      { recipe: "Web_Sushi_RicePrep" },
      { guid: GUID.cucumber },
      { guid: GUID.sushiFish },
      { guid: GUID.sushiPrawn },
    ],
    modelSO: sushiModelGuid("Web_Sushi_CompositeCarrier"),
    iconSource: null,
    cooking: false,
    mixing: false,
  },
];

const recipeById = new Map(RECIPES.map((r) => [r.id, r]));

function compositionRef(c) {
  if (c.recipe) {
    if (!recipeById.has(c.recipe)) throw new Error("未知组成菜谱: " + c.recipe);
    return `{fileID: 11400000, guid: ${recipeGuid(c.recipe)}, type: 2}`;
  }
  return `{fileID: 11400000, guid: ${c.guid}, type: 2}`;
}

function recipeYaml(r) {
  const compField = r.composition.length
    ? "  compositionSOs:\n" + r.composition.map((c) => `  - ${compositionRef(c)}`).join("\n")
    : "  compositionSOs: []";
  const optField = r.optional && r.optional.length
    ? "  optionalSOs:\n" + r.optional.map((c) => `  - ${compositionRef(c)}`).join("\n")
    : "  optionalSOs: []";
  const plating = `{fileID: 11400000, guid: ${GUID.plate}, type: 2}`;
  const modelSO = r.modelSO
    ? `{fileID: 11400000, guid: ${r.modelSO}, type: 2}`
    : "{fileID: 0}";
  const model = r.modelGuid
    ? `{fileID: 1709254077376921, guid: ${r.modelGuid}, type: 2}`
    : "{fileID: 0}";
  const icon = r.iconSource
    ? `{fileID: 21300000, guid: ${recipeIconGuid(r.id)}, type: 3}`
    : "{fileID: 0}";
  const cooking = r.cooking
    ? `  cookingStepSO: {fileID: 11400000, guid: ${GUID.pot}, type: 2}
  cookingStepIconSO: {fileID: 11400000, guid: ${GUID.potIcon}, type: 2}`
    : `  cookingStepSO: {fileID: 0}
  cookingStepIconSO: {fileID: 0}`;
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${SCRIPT_CUSTOM_RECIPE}, type: 3}
  m_Name: ${r.id}
  m_EditorClassIdentifier:
  type: ${r.type}
  recipeName: ${r.id}
  uID: ${r.uid}
  score: ${r.score}
  platingStepSO: ${plating}
  modelSO: ${modelSO}
  model: ${model}
  iconSO: {fileID: 0}
  icon: ${icon}
${compField}
${optField}
${cooking}
  cookingStepIcon: {fileID: 0}
  cookingProgress: ${r.cooking ? 1 : 0}
  mixingIconSO: {fileID: 0}
  mixingIcon: {fileID: 0}
  mixingProgress: 0
`;
}

function pseudoYaml(id, prefabName, subPath) {
  const dir = subPath || "meals";
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${SCRIPT_PSEUDO_PREFAB}, type: 3}
  m_Name: ${id}
  m_EditorClassIdentifier:
  prefabName: ${prefabName}
  bundleName: bundle47
  assetPath: Assets/prefabs/${dir}/${prefabName}.prefab
  asset: {fileID: 0}
`;
}

const ASSET_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
`;

const PREFAB_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 100100000
  userData:
  assetBundleName:
  assetBundleVariant:
`;

const MAT_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 2100000
  userData:
  assetBundleName:
  assetBundleVariant:
`;

const FOLDER_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
`;

const spriteMetaTemplate = fs.readFileSync(
  path.join(repoRoot, `${W5}/custom_recipes/burrito/icons/Web_Burrito_Meat_Chicken.png.meta`),
  "utf8",
);

const SPRITE_META = (guid) => spriteMetaTemplate.replace(/^guid: [0-9a-f]{32}$/m, `guid: ${guid}`);

// ── 大满贯寿司本地模型（5 件套）──────────────────────────────────────────
const objMetaTemplate = fs.readFileSync(
  path.join(repoRoot, `${W5}/kettle/meshes/m_kettle_anniversary_01.obj.meta`),
  "utf8",
);
const texMetaTemplate = fs.readFileSync(
  path.join(repoRoot, `${W5}/custom_recipes/pasta/models/textures/t_ingredients_prawn_fried_01_d.png.meta`),
  "utf8",
);
const TEX_META = (guid) =>
  texMetaTemplate.replace(/^guid: [0-9a-f]{32}$/m, `guid: ${guid}`);
const SHADER_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
ShaderImporter:
  externalObjects: {}
  defaultTextures: []
  userData:
  assetBundleName:
  assetBundleVariant:
`;

function fmtFloat(v) {
  if (Number.isInteger(v)) return v.toString();
  return String(parseFloat(v.toPrecision(8)));
}

function objMeta(guid, meshName, submeshCount) {
  let text = objMetaTemplate.replace(/^guid: [0-9a-f]{32}$/m, `guid: ${guid}`);
  let entries = "    100000: //RootNode\n    400000: //RootNode\n    9500000: //RootNode\n";
  for (let i = 0; i < submeshCount; i++) {
    entries += `    ${100002 + i}: ${meshName}_${i}\n`;
    entries += `    ${400002 + i}: ${meshName}_${i}\n`;
    entries += `    ${2100000 + i}: ${meshName}_${i}Mat\n`;
    entries += `    ${2300000 + i}: ${meshName}_${i}\n`;
    entries += `    ${3300000 + i}: ${meshName}_${i}\n`;
  }
  entries += `    4300000: ${meshName}_0\n`;
  return text.replace(/  fileIDToRecycleName:\n(?:    \d+: .*\n)+/, `  fileIDToRecycleName:\n${entries}`);
}

function sushiMaterialYaml(info) {
  const colorLines = Object.entries(info.colors ?? {})
    .map(
      ([k, v]) =>
        `    - ${k}: {r: ${fmtFloat(v[0])}, g: ${fmtFloat(v[1])}, b: ${fmtFloat(v[2])}, a: ${fmtFloat(v[3])}}`,
    )
    .join("\n");
  const floatLines = Object.entries(info.floats ?? {})
    .map(([k, v]) => `    - ${k}: ${fmtFloat(v)}`)
    .join("\n");
  const texLines = info.texture
    ? `    - _DiffuseMap:
        m_Texture: {fileID: 2800000, guid: ${localModelGuid("tex", info.texture)}, type: 3}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _MainTex:
        m_Texture: {fileID: 2800000, guid: ${localModelGuid("tex", info.texture)}, type: 3}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}`
    : "";
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 6
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_Name: ${info.id}
  m_Shader: {fileID: 4800000, guid: ${GRANDSLAM_SHADER_GUID}, type: 3}
  m_ShaderKeywords:
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
${texLines}
    m_Floats:
${floatLines}
    m_Colors:
${colorLines}
`;
}

const SUSHI_SHADER_SRC = `Shader "Custom/WebSushiFood" {
    Properties {
        _MainTex ("Main Texture (preview)", 2D) = "white" {}
        _DiffuseMap ("Diffuse Map", 2D) = "white" {}
        _MaskColor ("Mask Color", Color) = (0.5, 0.5, 0.5, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0.04
        _Roughness ("Roughness", Range(0, 1)) = 0.5
        _Brightness ("Brightness", Range(0, 1)) = 0
        _highlight_colour ("Highlight Colour", Color) = (0.91, 0.87, 0.78, 1)
    }
    SubShader {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _DiffuseMap;
        struct Input { float2 uv_DiffuseMap; };
        half _Metallic;
        half _Roughness;
        half _Brightness;
        fixed4 _highlight_colour;
        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D(_DiffuseMap, IN.uv_DiffuseMap);
            fixed3 lit = lerp(c.rgb, c.rgb * _highlight_colour.rgb * 2.0, _Brightness);
            o.Albedo = lit;
            o.Metallic = _Metallic;
            o.Smoothness = 1.0 - _Roughness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "VertexLit"
}
`;

function grandslamPrefabYaml() {
  const goId = 1709254077376921;
  const trId = 4730242686860875;
  const mfId = 33724804207875695;
  const mrId = 23618682377417806;
  const riId = 114703437598541787;
  const mats = grandslamMaterials
    .map((m) => `  - {fileID: 2100000, guid: ${localModelGuid("mat", m.id)}, type: 2}`)
    .join("\n");
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &${goId}
GameObject:
  serializedVersion: 5
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_Component:
  - component: {fileID: ${trId}}
  - component: {fileID: ${mfId}}
  - component: {fileID: ${mrId}}
  - component: {fileID: ${riId}}
  m_Layer: 0
  m_Name: ${GRANDSLAM_MODEL_ID}
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &${trId}
Transform:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${goId}}
  m_LocalRotation: {x: 0, y: -0, z: -0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {x: 0, y: -0, z: 0}
--- !u!33 &${mfId}
MeshFilter:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${goId}}
  m_Mesh: {fileID: 4300000, guid: ${GRANDSLAM_MESH_GUID}, type: 3}
--- !u!23 &${mrId}
MeshRenderer:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${goId}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_MotionVectors: 0
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 0
  m_Materials:
${mats}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 0
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
--- !u!114 &${riId}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${goId}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 34856dccebb76b09e2df91366afead1a, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  lightmapIndex: -1
  lightmapScaleOffset: {x: 0, y: 0, z: 0, w: 0}
--- !u!1001 &100100000
Prefab:
  serializedVersion: 2
  m_ObjectHideFlags: 1
  m_Modification:
    m_TransformParent: {fileID: 0}
    m_Modifications: []
    m_RemovedComponents: []
  m_ParentPrefab: {fileID: 0}
  m_RootGameObject: {fileID: ${goId}}
  m_IsPrefabParent: 1
`;
}

const plan = [];

function addFile(rel, content, note) {
  plan.push({ rel, content, note });
}

function addCopy(rel, srcRel, note) {
  plan.push({ rel, srcRel, note });
}

function buildPlan() {
  const sushiDir = `${W5}/custom_recipes/sushi`;
  addFile(`${sushiDir}.meta`, FOLDER_META(folderGuid(sushiDir)), "sushi 目录");
  addFile(`${sushiDir}/icons.meta`, FOLDER_META(folderGuid(`${sushiDir}/icons`)), "sushi icons 目录");
  addFile(`${sushiDir}/models.meta`, FOLDER_META(folderGuid(`${sushiDir}/models`)), "sushi models 目录");

  // 大满贯 5 件套本地模型（OBJ/PNG 由 gen_sushi_grandslam_model.py 生成）
  const modelRoot = `${sushiDir}/models`;
  const meshDir = `${modelRoot}/meshes`;
  const texDir = meshDir;
  const matDir = `${modelRoot}/materials`;
  addFile(`${meshDir}.meta`, FOLDER_META(folderGuid(meshDir)), "sushi model meshes 目录（OBJ + 贴图同目录，网页预览可带上贴图）");
  addFile(`${matDir}.meta`, FOLDER_META(folderGuid(matDir)), "sushi model materials 目录");

  const meshAbs = path.join(repoRoot, `${meshDir}/${GRANDSLAM_MESH}.obj`);
  if (!fs.existsSync(meshAbs)) {
    throw new Error(
      "缺少 5 件套网格，先运行: python3 layout-editor/scripts/sushi/gen_sushi_grandslam_model.py",
    );
  }
  addFile(
    `${meshDir}/${GRANDSLAM_MESH}.obj.meta`,
    objMeta(GRANDSLAM_MESH_GUID, GRANDSLAM_MESH, grandslamMaterials.length),
    "大满贯寿司网格 meta（5 子网格）",
  );

  const seenTextures = new Set();
  for (const m of grandslamMaterials) {
    if (!fs.existsSync(path.join(repoRoot, `${texDir}/${m.texture}.png`))) {
      throw new Error(
        "缺少 5 件套贴图，先运行: python3 layout-editor/scripts/sushi/gen_sushi_grandslam_model.py",
      );
    }
    if (!seenTextures.has(m.texture)) {
      seenTextures.add(m.texture);
      addFile(
        `${texDir}/${m.texture}.png.meta`,
        TEX_META(localModelGuid("tex", m.texture)),
        `大满贯寿司贴图 meta ${m.texture}`,
      );
    }
    addFile(
      `${matDir}/${m.id}.mat`,
      sushiMaterialYaml(m),
      `大满贯寿司材质 ${m.id}`,
    );
    addFile(
      `${matDir}/${m.id}.mat.meta`,
      MAT_META(localModelGuid("mat", m.id)),
      `大满贯寿司材质 meta ${m.id}`,
    );
  }

  addFile(`${modelRoot}/WebSushiFood.shader`, SUSHI_SHADER_SRC, "大满贯寿司本地 shader");
  addFile(`${modelRoot}/WebSushiFood.shader.meta`, SHADER_META(GRANDSLAM_SHADER_GUID), "大满贯寿司 shader meta");
  addFile(
    `${modelRoot}/${GRANDSLAM_MODEL_ID}.prefab`,
    grandslamPrefabYaml(),
    "大满贯寿司 5 件套 prefab",
  );
  addFile(
    `${modelRoot}/${GRANDSLAM_MODEL_ID}.prefab.meta`,
    PREFAB_META(GRANDSLAM_MODEL_GUID),
    "大满贯寿司 prefab meta",
  );

  for (const m of MODEL_ASSETS) {
    addFile(
      `${sushiDir}/models/${m.id}.asset`,
      pseudoYaml(m.id, m.prefabName, m.subPath),
      `${m.id}  guid=${sushiModelGuid(m.id)}  (${m.note})`,
    );
    addFile(
      `${sushiDir}/models/${m.id}.asset.meta`,
      ASSET_META(sushiModelGuid(m.id)),
      `${m.id} meta`,
    );
  }

  for (const r of RECIPES) {
    addFile(
      `${sushiDir}/${r.id}.asset`,
      recipeYaml(r),
      `${r.id}  uID=${r.uid}  guid=${recipeGuid(r.id)}`,
    );
    addFile(
      `${sushiDir}/${r.id}.asset.meta`,
      ASSET_META(recipeGuid(r.id)),
      `${r.id} meta`,
    );
    if (!r.iconSource) continue;
    // 图标源支持绝对路径（用户提供的真图在仓库外）；源缺失但目标已存在时
    // 跳过该图标的拷贝（保留现有文件），源与目标都没有才报错。
    const srcAbs = path.isAbsolute(r.iconSource)
      ? r.iconSource
      : path.join(repoRoot, r.iconSource);
    const iconRels = [`${sushiDir}/icons/${r.id}.png`];
    const srcMissing = !fs.existsSync(srcAbs);
    if (srcMissing) {
      const anyDest = iconRels.some((rel) => fs.existsSync(path.join(repoRoot, rel)));
      if (!anyDest) throw new Error("缺少图标源且无既有目标: " + r.iconSource);
    } else {
      addCopy(iconRels[0], r.iconSource, `${r.id} 图标`);
      addCopy(`layout-editor/web/public/icons/recipes/${r.id}.png`, r.iconSource, `${r.id} web 图标`);
      if (fs.existsSync(path.join(repoRoot, "layout-editor/web/dist"))) {
        addCopy(`layout-editor/web/dist/icons/recipes/${r.id}.png`, r.iconSource, `${r.id} dist 图标`);
      }
    }
    addFile(`${iconRels[0]}.meta`, SPRITE_META(recipeIconGuid(r.id)), `${r.id} 图标 meta`);
  }

  mergeNames();
  mergeConfig();
  validateUids();
}

function mergeNames() {
  const rel = `${W5}/custom_recipes/names.json`;
  const abs = path.join(repoRoot, rel);
  const data = JSON.parse(fs.readFileSync(abs, "utf8").replace(/^\uFEFF/, ""));
  data.names = data.names ?? [];
  const byId = new Map(data.names.map((n) => [n.id, n]));
  for (const r of RECIPES) {
    const prev = byId.get(r.id);
    if (prev) {
      prev.zh = r.zh;
      prev.en = r.en;
    } else {
      data.names.push({ id: r.id, zh: r.zh, en: r.en });
    }
  }
  addFile(rel, "\uFEFF" + JSON.stringify(data, null, 2) + "\n", "names.json 合并");
}

function mergeConfig() {
  const rel = `${W5}/custom_recipes/CustomRecipeConfig.asset`;
  const abs = path.join(repoRoot, rel);
  let text = fs.readFileSync(abs, "utf8");
  if (!/id: sushi\b/.test(text)) {
    const marker = "  subcategories:";
    const idx = text.indexOf(marker);
    if (idx < 0) throw new Error("CustomRecipeConfig 缺少 subcategories 段");
    const category = `  - id: sushi
    zh: "\\u5BFF\\u53F8"
    en: Sushi
`;
    text = text.slice(0, idx) + category + text.slice(idx);
  }
  text = text.replace(/nextSequence: \d+/, (m) => {
    const cur = Number(m.split(":")[1].trim());
    return `nextSequence: ${Math.max(cur, NEXT_SEQUENCE)}`;
  });
  addFile(rel, text, "CustomRecipeConfig 合并（sushi 分类 / nextSequence）");
}

function validateUids() {
  const roots = [
    "Assets/commonW2/custom_recipes",
    "Assets/commonW3/custom_recipes",
    `${W5}/custom_recipes`,
  ];
  const own = new Set(RECIPES.map((r) => r.id));
  const seen = new Map();
  for (const root of roots) {
    const absRoot = path.join(repoRoot, root);
    if (!fs.existsSync(absRoot)) continue;
    for (const file of walk(absRoot, (n) => n.endsWith(".asset"))) {
      if (file.includes("/models/") || file.includes("/model_so/")) continue;
      const text = fs.readFileSync(file, "utf8");
      if (!text.includes(SCRIPT_CUSTOM_RECIPE)) continue;
      const m = text.match(/^\s*uID: (\d+)$/m);
      if (!m) continue;
      const uid = m[1];
      const id = path.basename(file, ".asset");
      if (seen.has(uid) && seen.get(uid) !== id) {
        throw new Error(`uID 冲突 ${uid}: ${seen.get(uid)} ↔ ${id}`);
      }
      seen.set(uid, id);
    }
  }
  for (const r of RECIPES) {
    const existing = seen.get(String(r.uid));
    if (existing && existing !== r.id) {
      throw new Error(`新 uID ${r.uid} 已被 ${existing} 占用`);
    }
  }
}

function walk(dir, pick) {
  const out = [];
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, ent.name);
    if (ent.isDirectory()) out.push(...walk(p, pick));
    else if (pick(p)) out.push(p);
  }
  return out;
}

buildPlan();

console.log(`=== gen-commonw5-sushi（${apply ? "APPLY" : "DRY-RUN"}） ===`);
for (const r of RECIPES) {
  console.log(`  recipe ${r.id}  uID=${r.uid}  score=${r.score}  guid=${recipeGuid(r.id)}`);
}
console.log(`  uidPrefix=${UID_PREFIX}  nextSequence=${NEXT_SEQUENCE}`);
console.log("");
for (const item of plan) {
  console.log(`  ${apply ? "写入" : "计划"} ${item.rel}  (${item.note})`);
}

if (apply) {
  for (const item of plan) {
    const abs = path.join(repoRoot, item.rel);
    fs.mkdirSync(path.dirname(abs), { recursive: true });
    if (item.srcRel) {
      const srcAbs = path.isAbsolute(item.srcRel) ? item.srcRel : path.join(repoRoot, item.srcRel);
      fs.copyFileSync(srcAbs, abs);
    }
    else fs.writeFileSync(abs, item.content, "utf8");
  }
  console.log(`\n完成：${plan.length} 个文件已写入。`);
} else {
  console.log(`\n（dry-run：加 --apply 落盘，共 ${plan.length} 个文件）`);
}
