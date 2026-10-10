#!/usr/bin/env node
/**
 * 修复 commonW3 炒饭 FBX：补 external base_color 贴图 + 改写 FBX 内引用。
 * 生菜 / 生菜胡萝卜拆独立 mesh（不再与蛋炒饭 / 胡萝卜炒饭共用贴图）。
 *
 *   node layout-editor/scripts/repair-commonw3-fried-rice-models.mjs
 *   node layout-editor/scripts/repair-commonw3-fried-rice-models.mjs --apply
 */
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const workspaceRoot = path.resolve(repoRoot, "..");
const BACKUP = path.join(workspaceRoot, "backup_rebuild_20260917_132624/自定义菜谱");
const W3 = path.join(repoRoot, "Assets/commonW3");
const CAT = "fried_rice";

const modelGuid = (id) => crypto.createHash("md5").update(`commonw3-model:${id}`).digest("hex");
const assetGuid = (rel) => crypto.createHash("md5").update(`commonw3-asset:${rel}`).digest("hex");
const folderGuid = (rel) => crypto.createHash("md5").update(`commonw3-folder:${rel}`).digest("hex");

function resolveBackup(rel) {
  const abs = path.join(BACKUP, rel);
  if (!fs.existsSync(abs)) throw new Error(`backup 缺失: ${rel}`);
  return abs;
}

function compressImage(src, dest, max = 1024) {
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  try {
    execSync(`sips -Z ${max} "${src}" --out "${dest}"`, { stdio: "pipe" });
  } catch {
    fs.copyFileSync(src, dest);
  }
}

function renameFbxOnDisk(fbxBytes, diskNames) {
  const helper = path.join(repoRoot, "layout-editor/scripts/.rename-fbx-textures-tmp.mjs");
  const payloadPath = path.join(repoRoot, "layout-editor/scripts/.rename-fbx-textures-payload.json");
  const outPath = path.join(repoRoot, "layout-editor/scripts/.rename-fbx-textures-out.json");
  fs.writeFileSync(
    helper,
    `import fs from "node:fs";
import { renameFbxTextureRefs } from "../web/src/fbxTextureRename.ts";
const payload = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const raw = Uint8Array.from(Buffer.from(payload.b64, "base64"));
const r = renameFbxTextureRefs(raw, payload.diskNames ?? {});
fs.writeFileSync(process.argv[3], JSON.stringify({ b64: Buffer.from(r.bytes).toString("base64"), renamed: r.renamed }));
`,
    "utf8",
  );
  try {
    fs.writeFileSync(
      payloadPath,
      JSON.stringify({ b64: Buffer.from(fbxBytes).toString("base64"), diskNames }),
      "utf8",
    );
    execSync(`node --experimental-strip-types "${helper}" "${payloadPath}" "${outPath}"`, {
      encoding: "utf8",
      maxBuffer: 128 * 1024 * 1024,
    });
    const parsed = JSON.parse(fs.readFileSync(outPath, "utf8"));
    return { bytes: Buffer.from(parsed.b64, "base64"), renamed: parsed.renamed };
  } finally {
    for (const p of [helper, payloadPath, outPath]) {
      try {
        fs.unlinkSync(p);
      } catch {
        /* ignore */
      }
    }
  }
}

const FOLDER_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

const FBX_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
ModelImporter:
  serializedVersion: 22
  fileIDToRecycleName: {}
  externalObjects: {}
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

const TEXTURE_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
TextureImporter:
  fileIDToRecycleName: {}
  externalObjects: {}
  serializedVersion: 4
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: -1
    aniso: -1
    mipBias: -1
    wrapU: 1
    wrapV: 1
    wrapW: -1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  platformSettings:
  - buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
  spritePackingTag: 
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

/** @type {Array<{ meshId: string; fbxBackup: string; texBackup: string; recipes: string[]; create?: boolean }>} */
const JOBS = [
  {
    meshId: "Web_FriedRice_Mesh_Base",
    fbxBackup: "custom_recipes/Fried_Rice/models/Fried_rice.fbx",
    texBackup: "custom_recipes/Fried_Rice/models/炒饭.png",
    recipes: ["Web_FriedRice_Egg"],
  },
  {
    meshId: "Web_FriedRice_Lettuce",
    fbxBackup: "custom_recipes/Fried_Rice/models/Fried_rice.fbx",
    texBackup: "custom_recipes/Fried_Rice/models/Fried_rice_Cabbage_Icon.png",
    recipes: ["Web_FriedRice_Lettuce"],
    create: true,
  },
  {
    meshId: "Web_FriedRice_Mesh_Carrot",
    fbxBackup: "custom_recipes/Fried_Rice/models/胡萝卜蛋炒饭.fbx",
    texBackup: "custom_recipes/Fried_Rice/models/萝卜炒饭ui.png",
    recipes: ["Web_FriedRice_Carrot"],
  },
  {
    meshId: "Web_FriedRice_LettuceCarrot",
    fbxBackup: "custom_recipes/Fried_Rice/models/胡萝卜蛋炒饭.fbx",
    texBackup: "custom_recipes/Fried_Rice/models/Fried_rice_with_Lettuce_Carrots_Icon.png",
    recipes: ["Web_FriedRice_LettuceCarrot"],
    create: true,
  },
  {
    meshId: "Web_FriedRice_CarrotPepperoni",
    fbxBackup: "custom_recipes/Fried_Rice/models/肠胡萝卜蛋炒饭.fbx",
    texBackup: "custom_recipes/Fried_Rice/models/肠炒饭ui.png",
    recipes: ["Web_FriedRice_CarrotPepperoni"],
  },
];

function patchRecipeModelGuid(recipeId, meshId) {
  const assetPath = path.join(W3, `custom_recipes/${CAT}/${recipeId}.asset`);
  const guid = modelGuid(meshId);
  const yaml = fs.readFileSync(assetPath, "utf8");
  if (yaml.includes(`guid: ${guid}, type: 3}`)) return false;
  const next = yaml.replace(
    /model: \{fileID: \d+, guid: [a-f0-9]+, type: 3\}/,
    `model: {fileID: 100000, guid: ${guid}, type: 3}`,
  );
  if (next === yaml) throw new Error(`${recipeId}: 未找到 model 字段可替换`);
  fs.writeFileSync(assetPath, next);
  return true;
}

function repairMesh(job, apply) {
  const relRoot = `Assets/commonW3/custom_recipes/${CAT}/models`;
  const modelDir = `${relRoot}/${job.meshId}`;
  const absDir = path.join(repoRoot, modelDir);
  const fbxRel = `${modelDir}/${job.meshId}.fbx`;
  const texName = `${job.meshId}_base_color.png`;
  const texRel = `${modelDir}/${texName}`;
  const mg = modelGuid(job.meshId);
  const tg = assetGuid(texRel);

  const fbxSrc = fs.readFileSync(resolveBackup(job.fbxBackup));
  const diskNames = { base_color: texName };
  const { bytes: fbxOut, renamed } = renameFbxOnDisk(fbxSrc, diskNames);

  const lines = [
    `${job.meshId}: fbx←${job.fbxBackup} tex←${job.texBackup} renamed=${renamed} guid=${mg}`,
    `  → ${fbxRel}`,
    `  → ${texRel}`,
    `  recipes: ${job.recipes.join(", ")}`,
  ];

  if (!apply) return lines;

  fs.mkdirSync(absDir, { recursive: true });
  if (job.create) {
    const modelsDirRel = `${relRoot}`;
    const modelsDirMeta = path.join(repoRoot, `${modelsDirRel}.meta`);
    if (!fs.existsSync(modelsDirMeta)) {
      fs.writeFileSync(modelsDirMeta, FOLDER_META(folderGuid(modelsDirRel)));
    }
    const dirMetaPath = path.join(repoRoot, `${modelDir}.meta`);
    if (!fs.existsSync(dirMetaPath)) {
      fs.writeFileSync(dirMetaPath, FOLDER_META(folderGuid(modelDir)));
    }
    const fbxMetaPath = path.join(absDir, `${job.meshId}.fbx.meta`);
    if (!fs.existsSync(fbxMetaPath)) {
      fs.writeFileSync(fbxMetaPath, FBX_META(mg));
    }
  }

  const tmpTex = path.join(repoRoot, "layout-editor/scripts/.tmp-fried-rice-tex.png");
  compressImage(resolveBackup(job.texBackup), tmpTex);
  fs.writeFileSync(path.join(absDir, `${job.meshId}.fbx`), fbxOut);
  fs.writeFileSync(path.join(absDir, texName), fs.readFileSync(tmpTex));
  fs.writeFileSync(path.join(absDir, `${texName}.meta`), TEXTURE_META(tg));
  try {
    fs.unlinkSync(tmpTex);
  } catch {
    /* ignore */
  }

  const fbxMetaPath = path.join(absDir, `${job.meshId}.fbx.meta`);
  if (fs.existsSync(fbxMetaPath)) {
    const cur = fs.readFileSync(fbxMetaPath, "utf8");
    if (!cur.includes(`guid: ${mg}`)) {
      fs.writeFileSync(fbxMetaPath, FBX_META(mg));
    }
  }

  for (const rid of job.recipes) {
    if (patchRecipeModelGuid(rid, job.meshId)) {
      lines.push(`  patched recipe ${rid} → model guid ${modelGuid(job.meshId)}`);
    }
  }

  return lines;
}

const apply = process.argv.includes("--apply");
console.log(apply ? "=== APPLY ===" : "=== DRY RUN (pass --apply to write) ===\n");
for (const job of JOBS) {
  for (const line of repairMesh(job, apply)) console.log(line);
  console.log("");
}
if (!apply) {
  console.log("未写入。确认后执行: node layout-editor/scripts/repair-commonw3-fried-rice-models.mjs --apply");
}
