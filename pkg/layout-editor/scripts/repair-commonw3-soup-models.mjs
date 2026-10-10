#!/usr/bin/env node
/**
 * Repair commonW3 soup OBJ models: external .mat + obj.meta remapping + plating prefab.
 * Run: node layout-editor/scripts/repair-commonw3-soup-models.mjs [--apply]
 */
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const W3 = path.join(repoRoot, "Assets/commonW3");

const assetGuid = (rel) =>
  crypto.createHash("md5").update(`commonw3-asset:${rel}`).digest("hex");
const modelGuid = (id) =>
  crypto.createHash("md5").update(`commonw3-model:${id}`).digest("hex");

function readGuid(metaPath) {
  if (!fs.existsSync(metaPath)) return null;
  const m = fs.readFileSync(metaPath, "utf8").match(/^guid:\s*([a-f0-9]+)/m);
  return m ? m[1] : null;
}

function fileIds(seed) {
  const buf = crypto.createHash("sha256").update(seed).digest();
  const pick = (off) => {
    const n = buf.readUInt32BE(off) & 0x7fffffff;
    return n === 0 ? 100000001 : n;
  };
  return {
    rootGo: pick(0),
    rootTr: pick(4),
    meshGo: pick(8),
    meshTr: pick(12),
    meshFilter: pick(16),
    meshRenderer: pick(20),
    prefab: pick(24),
  };
}

const MAT_TEMPLATE = (name, texGuid) => `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 6
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_Name: ${name}
  m_Shader: {fileID: 7, guid: 0000000000000000f000000000000000, type: 0}
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
    - _BumpMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _DetailAlbedoMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _DetailMask:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _DetailNormalMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _EmissionMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _MainTex:
        m_Texture: {fileID: 2800000, guid: ${texGuid}, type: 3}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _MetallicGlossMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _OcclusionMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _ParallaxMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Floats:
    - _BumpScale: 1
    - _Cutoff: 0.5
    - _DetailNormalMapScale: 1
    - _DstBlend: 0
    - _GlossMapScale: 1
    - _Glossiness: 0.5
    - _GlossyReflections: 1
    - _Metallic: 0
    - _Mode: 0
    - _OcclusionStrength: 1
    - _Parallax: 0.02
    - _SmoothnessTextureChannel: 0
    - _SpecularHighlights: 1
    - _SrcBlend: 1
    - _UVSec: 0
    - _ZWrite: 1
    m_Colors:
    - _Color: {r: 1, g: 1, b: 1, a: 1}
    - _EmissionColor: {r: 0, g: 0, b: 0, a: 1}
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

function objMetaYaml(objGuid, matName, matGuid) {
  return `fileFormatVersion: 2
guid: ${objGuid}
ModelImporter:
  serializedVersion: 22
  fileIDToRecycleName:
    100000: default
    400000: default
    2100000: ${matName}
    2300000: default
    4300000: default
  externalObjects:
  - first:
      type: UnityEngine:Material
      assembly: UnityEngine.CoreModule
      name: ${matName}
    second: {fileID: 2100000, guid: ${matGuid}, type: 2}
  materials:
    importMaterials: 1
    materialName: 0
    materialSearch: 1
    materialLocation: 1
  meshes:
    globalScale: 1
    meshCompression: 0
    addColliders: 0
  isReadable: 1
  importAnimation: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}

function buildPrefabYaml(mid, ids, objGuid, matGuid, meshChildName = "Mesh") {
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1001 &100100000
Prefab:
  m_ObjectHideFlags: 1
  serializedVersion: 2
  m_Modification:
    m_TransformParent: {fileID: 0}
    m_Modifications: []
    m_RemovedComponents: []
  m_ParentPrefab: {fileID: 0}
  m_RootGameObject: {fileID: ${ids.rootGo}}
  m_IsPrefabParent: 1
--- !u!1 &${ids.meshGo}
GameObject:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  serializedVersion: 5
  m_Component:
  - component: {fileID: ${ids.meshTr}}
  - component: {fileID: ${ids.meshFilter}}
  - component: {fileID: ${ids.meshRenderer}}
  m_Layer: 0
  m_Name: ${meshChildName}
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!1 &${ids.rootGo}
GameObject:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  serializedVersion: 5
  m_Component:
  - component: {fileID: ${ids.rootTr}}
  m_Layer: 0
  m_Name: ${mid}
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &${ids.meshTr}
Transform:
  m_ObjectHideFlags: 1
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${ids.meshGo}}
  m_LocalRotation: {x: 0, y: -0, z: -0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: ${ids.rootTr}}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!4 &${ids.rootTr}
Transform:
  m_ObjectHideFlags: 1
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${ids.rootGo}}
  m_LocalRotation: {x: 0, y: -0, z: -0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children:
  - {fileID: ${ids.meshTr}}
  m_Father: {fileID: 0}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!23 &${ids.meshRenderer}
MeshRenderer:
  m_ObjectHideFlags: 1
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${ids.meshGo}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_Materials:
  - {fileID: 2100000, guid: ${matGuid}, type: 2}
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
--- !u!33 &${ids.meshFilter}
MeshFilter:
  m_ObjectHideFlags: 1
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 100100000}
  m_GameObject: {fileID: ${ids.meshGo}}
  m_Mesh: {fileID: 4300000, guid: ${objGuid}, type: 3}
`;
}

const PREFAB_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 100100000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

const MESH_IDS = [
  "Web_Soup_Carrot",
  "Web_Soup_Mesh_VegBowl",
  "Web_Soup_Mesh_FishBowl",
  "Web_Soup_FishPrawnPorridge",
];

/** recipeId -> meshId (prefab source) */
const RECIPE_PREFAB = [
  { recipeId: "Web_Soup_Carrot", meshId: "Web_Soup_Carrot", keepPrefabGuid: "86836f984881f7e4a966e8d7a24719b1", rootGo: 1285504947777672 },
  { recipeId: "Web_Soup_OnionCarrotPotato", meshId: "Web_Soup_Mesh_VegBowl" },
  { recipeId: "Web_Soup_TomatoFish", meshId: "Web_Soup_Mesh_FishBowl" },
  { recipeId: "Web_Soup_FishPrawnTomato", meshId: "Web_Soup_Mesh_FishBowl" },
  { recipeId: "Web_Soup_FishPrawnPorridge", meshId: "Web_Soup_FishPrawnPorridge" },
];

function relModelDir(mid) {
  return `Assets/commonW3/custom_recipes/soup/models/${mid}`;
}

function repairMesh(mid, apply) {
  const modelDir = relModelDir(mid);
  const absDir = path.join(repoRoot, modelDir);
  const matName = `mat_${mid}`;
  const matRel = `${modelDir}/${matName}.mat`;
  const texRel = `${modelDir}/${mid}_base_color.png`;
  const objRel = `${modelDir}/${mid}.obj`;
  const prefabRel = `${modelDir}/${mid}.prefab`;

  const objGuid = modelGuid(mid);
  const matGuid = assetGuid(matRel);
  const texGuid = readGuid(path.join(absDir, `${mid}_base_color.png.meta`)) ?? assetGuid(texRel);
  const prefabGuidExisting = readGuid(path.join(absDir, `${mid}.prefab.meta`));

  const actions = [];

  actions.push({
    path: matRel,
    content: MAT_TEMPLATE(matName, texGuid),
  });
  actions.push({
    path: `${matRel}.meta`,
    content: MAT_META(matGuid),
  });
  actions.push({
    path: `${objRel}.meta`,
    content: objMetaYaml(objGuid, matName, matGuid),
  });

  const ids = fileIds(`commonw3-soup-prefab:${mid}`);
  const prefabGuid = prefabGuidExisting ?? assetGuid(prefabRel);
  const keepLegacyPrefab = mid === "Web_Soup_Carrot" && prefabGuidExisting;
  if (!keepLegacyPrefab) {
    actions.push({
      path: prefabRel,
      content: buildPrefabYaml(mid, ids, objGuid, matGuid, mid),
    });
    if (!prefabGuidExisting) {
      actions.push({
        path: `${prefabRel}.meta`,
        content: PREFAB_META(prefabGuid),
      });
    }
  }

  const prefabRootGo =
    RECIPE_PREFAB.find((r) => r.meshId === mid && r.rootGo)?.rootGo ?? ids.rootGo;

  return {
    mid,
    matGuid,
    prefabGuid,
    prefabRootGo,
    actions,
  };
}

function patchCarrotPrefab(absPath, matGuid) {
  let yaml = fs.readFileSync(absPath, "utf8");
  yaml = yaml.replace(
    /m_Materials:\n  - \{fileID: 2100000, guid: [a-f0-9]+, type: 3\}/,
    `m_Materials:\n  - {fileID: 2100000, guid: ${matGuid}, type: 2}`,
  );
  fs.writeFileSync(absPath, yaml, "utf8");
}

function updateRecipeModel(recipeId, prefabGuid, rootGo) {
  const assetPath = path.join(W3, `custom_recipes/soup/${recipeId}.asset`);
  let yaml = fs.readFileSync(assetPath, "utf8");
  const modelLine = `model: {fileID: ${rootGo}, guid: ${prefabGuid}, type: 2}`;
  yaml = yaml.replace(/^  model: .+$/m, `  ${modelLine}`);
  fs.writeFileSync(assetPath, yaml, "utf8");
}

const apply = process.argv.includes("--apply");
const meshResults = MESH_IDS.map((mid) => repairMesh(mid, apply));

if (!apply) {
  console.log("Dry run — pass --apply to write files.\n");
  for (const r of meshResults) {
    console.log(r.mid, "prefab", r.prefabGuid, "mat", r.matGuid);
    for (const a of r.actions) console.log(" ", a.path);
  }
  process.exit(0);
}

for (const r of meshResults) {
  for (const a of r.actions) {
    const abs = path.join(repoRoot, a.path);
    fs.mkdirSync(path.dirname(abs), { recursive: true });
    fs.writeFileSync(abs, a.content, "utf8");
    console.log("wrote", a.path);
  }
  if (r.mid === "Web_Soup_Carrot") {
    patchCarrotPrefab(path.join(repoRoot, relModelDir(r.mid), `${r.mid}.prefab`), r.matGuid);
    console.log("patched Web_Soup_Carrot.prefab material");
  }
}

for (const rp of RECIPE_PREFAB) {
  const mesh = meshResults.find((m) => m.mid === rp.meshId);
  const prefabGuid = rp.keepPrefabGuid ?? mesh.prefabGuid;
  const rootGo = rp.rootGo ?? mesh.prefabRootGo;
  updateRecipeModel(rp.recipeId, prefabGuid, rootGo);
  console.log("recipe model", rp.recipeId, "->", prefabGuid, rootGo);
}

console.log("Done.");
