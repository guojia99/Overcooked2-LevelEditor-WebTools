#!/usr/bin/env node
/**
 * Generate Assets/commonW1/prefabs/backgrounds/** placeholder prefabs (+ .meta only).
 * Uses built-in Plane/Quad meshes — no FBX import required.
 */
import crypto from "crypto";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, "../..");
const manifestPath = path.join(__dirname, "data/commonw1-backgrounds.json");
const commonW1Root = path.join(repoRoot, "Assets/commonW1");
const prefabRoot = path.join(commonW1Root, "prefabs/backgrounds");

const CELL = 1.2;
const PLANE_UNITS = 10;

function newGuid() {
  return crypto.randomBytes(16).toString("hex");
}

function readGuid(metaPath) {
  if (!fs.existsSync(metaPath)) return null;
  const m = fs.readFileSync(metaPath, "utf8").match(/^guid:\s*([a-f0-9]+)/m);
  return m ? m[1] : null;
}

function writeFolderMeta(absPath, bundleName) {
  const metaPath = absPath + ".meta";
  const guid = readGuid(metaPath) || newGuid();
  fs.mkdirSync(absPath, { recursive: true });
  fs.writeFileSync(
    metaPath,
    `fileFormatVersion: 2
guid: ${guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: ${bundleName}
  assetBundleVariant: 
`,
    "utf8"
  );
  return guid;
}

function fileIds(seed) {
  const buf = crypto.createHash("sha256").update(seed).digest();
  const pick = (off) => {
    const n = buf.readUInt32BE(off) & 0x7fffffff;
    return n === 0 ? 100000001 : n;
  };
  return {
    go: pick(0),
    tr: pick(4),
    mf: pick(8),
    mr: pick(12),
    prefab: pick(16),
  };
}

function scaleFor(cellsX, cellsZ, orientation) {
  const sx = (cellsX * CELL) / PLANE_UNITS;
  const sz = (cellsZ * CELL) / PLANE_UNITS;
  if (orientation === "standing") {
    return { x: sx, y: sz, z: 1, rotX: 0.7071068, rotW: 0.7071068, eulerX: 90, mesh: 10210 };
  }
  return { x: sx, y: 1, z: sz, rotX: 0, rotW: 1, eulerX: 0, mesh: 10209 };
}

function buildPrefabYaml(id, cellsX, cellsZ, orientation) {
  const ids = fileIds(id);
  const sc = scaleFor(cellsX, cellsZ, orientation);
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1001 &${ids.prefab}
Prefab:
  m_ObjectHideFlags: 1
  serializedVersion: 2
  m_Modification:
    m_TransformParent: {fileID: 0}
    m_Modifications: []
    m_RemovedComponents: []
  m_ParentPrefab: {fileID: 0}
  m_RootGameObject: {fileID: ${ids.go}}
  m_IsPrefabParent: 1
--- !u!1 &${ids.go}
GameObject:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: ${ids.prefab}}
  serializedVersion: 5
  m_Component:
  - component: {fileID: ${ids.tr}}
  - component: {fileID: ${ids.mf}}
  - component: {fileID: ${ids.mr}}
  m_Layer: 0
  m_Name: ${id}
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &${ids.tr}
Transform:
  m_ObjectHideFlags: 1
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: ${ids.prefab}}
  m_GameObject: {fileID: ${ids.go}}
  m_LocalRotation: {x: ${sc.rotX}, y: 0, z: 0, w: ${sc.rotW}}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: ${sc.x}, y: ${sc.y}, z: ${sc.z}}
  m_Children: []
  m_Father: {fileID: 0}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {x: ${sc.eulerX}, y: 0, z: 0}
--- !u!33 &${ids.mf}
MeshFilter:
  m_ObjectHideFlags: 1
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: ${ids.prefab}}
  m_GameObject: {fileID: ${ids.go}}
  m_Mesh: {fileID: ${sc.mesh}, guid: 0000000000000000e000000000000000, type: 0}
--- !u!23 &${ids.mr}
MeshRenderer:
  m_ObjectHideFlags: 1
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: ${ids.prefab}}
  m_GameObject: {fileID: ${ids.go}}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_Materials:
  - {fileID: 10303, guid: 0000000000000000f000000000000000, type: 0}
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
`;
}

function writePrefab(theme, item) {
  const dir = path.join(prefabRoot, theme);
  fs.mkdirSync(dir, { recursive: true });
  writeFolderMeta(dir, "commonW1");

  const prefabPath = path.join(dir, `${item.id}.prefab`);
  const metaPath = prefabPath + ".meta";
  const guid = readGuid(metaPath) || newGuid();

  fs.writeFileSync(prefabPath, buildPrefabYaml(item.id, item.cellsX, item.cellsZ, item.orientation || "flat"), "utf8");
  fs.writeFileSync(
    metaPath,
    `fileFormatVersion: 2
guid: ${guid}
PrefabImporter:
  externalObjects: {}
  userData: 
  assetBundleName: commonW1
  assetBundleVariant: 
`,
    "utf8"
  );
  return `Assets/commonW1/prefabs/backgrounds/${theme}/${item.id}.prefab`;
}

function main() {
  const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
  writeFolderMeta(commonW1Root, "commonW1");
  writeFolderMeta(path.join(commonW1Root, "prefabs"), "commonW1");
  writeFolderMeta(prefabRoot, "commonW1");

  const written = [];
  for (const item of manifest.items || []) {
    if (!item.id || !item.theme) {
      console.warn("Skip invalid manifest entry:", item);
      continue;
    }
    written.push(writePrefab(item.theme, item));
  }
  console.log(`Generated ${written.length} commonW1 background prefabs.`);
  for (const p of written) console.log(`  ${p}`);
}

main();
