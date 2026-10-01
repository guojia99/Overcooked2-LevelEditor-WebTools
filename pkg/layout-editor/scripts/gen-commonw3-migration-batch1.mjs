#!/usr/bin/env node
/**
 * commonW3 batch1 迁移：炒饭 / 汤粥 / 布丁（19 道；冰淇淋与冰沙见 gen-commonw3-dairy-drinks.mjs）。
 *
 * 用法：
 *   node gen-commonw3-migration-batch1.mjs           # dry-run + 输出 manifest MD
 *   node gen-commonw3-migration-batch1.mjs --apply # 落盘资产
 */
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";
import { fileURLToPath } from "node:url";
const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const workspaceRoot = path.resolve(repoRoot, "..");
const BACKUP = path.join(workspaceRoot, "backup_rebuild_20260917_132624/自定义菜谱");
const BACKUP_GH = path.join(
  workspaceRoot,
  "backup_rebuild_20260917_132624/backup_20260826/github_commit/Overcooked2-LevelEditor/Assets",
);
const SHARED_CUP_OBJ = path.join(workspaceRoot, "backup_rebuild_20260917_132624/Models/什锦果汁/_shared/什锦果汁.obj");
const W3 = "Assets/commonW3";
const CONFIG_PATH = path.join(repoRoot, `${W3}/custom_recipes/CustomRecipeConfig.asset`);
const MANIFEST_MD = path.join(
  workspaceRoot,
  "backup_rebuild_20260917_132624/自定义菜谱/_audit_docs/00-迁移清单-commonW3-batch1.md",
);

const SCRIPT_CUSTOM_RECIPE = "83fb008bcc8e793429b02c178c430815";
const SCRIPT_RECIPE_CONFIG = "5edb07060a0c6c140aa72ffb8fd3f021";
const PLATE_GUID = "02b04fdf6fef0e944870fd5e06375ed1";
const GLASS_PLATING_GUID = "9f2781f346fbd6d42a008e004f255d92";
const BLENDER_ICON_GUID = "7a77fe98553178640a4b8a48d10057bc";
const UID_PREFIX = 58322;
const FIRST_UID = 58322041;

const ING_REMAP = {
  "13f127b195d477141b0422e5886c1bcb": "ef868e413995bdf5ea3c0058f6dd5db0",
  "4a8901e43e0536a4faaa264fadb5fb06": "af1fe45a85aaf6448f00b5bdd59a7002",
  "13499ece0da45f64b971ec281f21d512": "73c28675ea0968be18f65b2d948e3227",
  "8a0032285a7249f40b7997a2633c3b38": "c0721b5197a5193a62cac90b2e010560",
  "bbbd95bd8e66dc042ba4b11aa9f47fb3": "1e87aab4d91d5e2b460a8de8fe55b737",
};

const RECIPE_REMAP = {
  "3b5f08e2c4f54534d808805d83e8ed20": "Web_FriedRice_RicePrep",
  "9810d2d5084c9c9438fd78a104d1d223": "Web_Pudding_Mix_EggMilk",
  "23d36417a19b2ad4a8f847ba5ae8cd4d": "Web_Pudding_Mix_EggMilkStrawberry",
  "2594b2f0c6e910741a36d0712c29c5e4": "Web_Pudding_Mix_EggMilkOrange",
};

const OFFICIAL_MODEL = {
  soup_onion: { fileID: 1766377274927934, guid: "afb547816bf79ba47959f18e2561b9bf" },
  soup_tomato_egg: { fileID: 1469280750590456, guid: "c50350d2567af284ebeb5856b57ace32" },
};

const CATEGORIES = [
  { id: "fried_rice", zh: "炒饭", en: "Fried Rice" },
  { id: "pudding", zh: "布丁", en: "Pudding" },
  { id: "soup", zh: "汤粥", en: "Soup" },
];

/** @type {import('./gen-commonw3-migration-batch1.types').RecipeDef[]} */
const RECIPES = [
  {
    id: "Web_FriedRice_RicePrep",
    category: "fried_rice",
    zh: "Web 炒饭煮米（中间）",
    en: "Web Steamed Rice (Intermediate)",
    backup: "custom_recipes/Fried_Rice/Rice_For_Fried_Rice.asset",
    intermediate: true,
    fixType: 2,
  },
  {
    id: "Web_FriedRice_Egg",
    category: "fried_rice",
    zh: "Web 蛋炒饭",
    en: "Web Egg Fried Rice",
    backup: "custom_recipes/Fried_Rice/Fried_rice_SO.asset",
    icon: "custom_recipes/Fried_Rice/models/Fried_rice_Icon.png",
    modelShareGroup: "fried_rice_base",
    meshId: "Web_FriedRice_Mesh_Base",
    meshSrc: "custom_recipes/Fried_Rice/models/Fried_rice.fbx",
    meshKind: "fbx",
  },
  {
    id: "Web_FriedRice_Lettuce",
    category: "fried_rice",
    zh: "Web 生菜炒饭",
    en: "Web Lettuce Fried Rice",
    backup: "custom_recipes/Fried_Rice/Fried_rice_Cabbage_SO.asset",
    icon: "custom_recipes/Fried_Rice/models/Fried_rice_Cabbage_Icon.png",
    modelShareGroup: "fried_rice_base",
    meshId: "Web_FriedRice_Mesh_Base",
    meshSrc: "custom_recipes/Fried_Rice/models/Fried_rice.fbx",
    meshKind: "fbx",
  },
  {
    id: "Web_FriedRice_Carrot",
    category: "fried_rice",
    zh: "Web 胡萝卜炒饭",
    en: "Web Carrot Fried Rice",
    backup: "custom_recipes/Fried_Rice/Fried_rice_Carrot_SO.asset",
    icon: "custom_recipes/Fried_Rice/models/萝卜炒饭ui.png",
    modelShareGroup: "fried_rice_carrot",
    meshId: "Web_FriedRice_Mesh_Carrot",
    meshSrc: "custom_recipes/Fried_Rice/models/胡萝卜蛋炒饭.fbx",
    meshKind: "fbx",
  },
  {
    id: "Web_FriedRice_CarrotPepperoni",
    category: "fried_rice",
    zh: "Web 胡萝卜香肠炒饭",
    en: "Web Carrot Pepperoni Fried Rice",
    backup: "custom_recipes/Fried_Rice/Fried_rice_Carrot_Pepp_SO.asset",
    icon: "custom_recipes/Fried_Rice/models/肠炒饭ui.png",
    meshId: "Web_FriedRice_CarrotPepperoni",
    meshSrc: "custom_recipes/Fried_Rice/models/肠胡萝卜蛋炒饭.fbx",
    meshKind: "fbx",
  },
  {
    id: "Web_FriedRice_LettuceCarrot",
    category: "fried_rice",
    zh: "Web 生菜胡萝卜炒饭",
    en: "Web Lettuce Carrot Fried Rice",
    backup: "custom_recipes/Fried_Rice/Fried_rice_with_Lettuce_Carrots_SO.asset",
    icon: "custom_recipes/Fried_Rice/models/Fried_rice_with_Lettuce_Carrots_Icon.png",
    modelShareGroup: "fried_rice_carrot",
    meshId: "Web_FriedRice_Mesh_Carrot",
    meshSrc: "custom_recipes/Fried_Rice/models/胡萝卜蛋炒饭.fbx",
    meshKind: "fbx",
  },
  {
    id: "Web_Soup_Carrot",
    category: "soup",
    zh: "Web 胡萝卜汤",
    en: "Web Carrot Soup",
    backup: "custom_recipes/Soup/Soup_Carrot_SO.asset",
    icon: "custom_recipes/Soup/Models/萝卜汤ui.png",
    meshId: "Web_Soup_Carrot",
    meshSrc: "custom_recipes/Soup/Models/胡萝卜汤.obj",
    meshKind: "obj",
    textureSrc: "custom_recipes/Soup/Models/胡萝卜汤.png",
  },
  {
    id: "Web_Soup_OnionCarrotPotato",
    category: "soup",
    zh: "Web 蔬菜浓汤",
    en: "Web Vegetable Chowder",
    backup: "custom_recipes/Soup/Soup_OnionCarrotPotato_SO.asset",
    icon: "common01/food/CustomRecipes/Soup/models/ui_soup_onion_01.png",
    iconFromGh: true,
    meshId: "Web_Soup_Mesh_VegBowl",
    meshSrc: "custom_recipes/Soup/Models/m_plated_fishprawn_soup_01.obj",
    meshKind: "obj",
    textureSrc: "custom_recipes/Soup/Models/萝卜汤ui.png",
    modelDataIssue: "misplaced_fish_prefab",
  },
  {
    id: "Web_Soup_TomatoFish",
    category: "soup",
    zh: "Web 番茄鱼汤",
    en: "Web Tomato Fish Soup",
    backup: "custom_recipes/Soup/Soup_TomatoFish_SO.asset",
    icon: "custom_recipes/Soup/Models/番茄鱼汤UI.png",
    modelShareGroup: "soup_fish_bowl",
    meshId: "Web_Soup_Mesh_FishBowl",
    meshSrc: "custom_recipes/Soup/Models/m_plated_fishprawn_soup_01.obj",
    meshKind: "obj",
    textureSrc: "custom_recipes/Soup/Models/t_fishprawnsoup_01.png",
  },
  {
    id: "Web_Soup_FishPrawnTomato",
    category: "soup",
    zh: "Web 鱼虾番茄汤",
    en: "Web Fish Prawn Tomato Soup",
    backup: "custom_recipes/Soup/Soup_FishPrawnTomato_SO.asset",
    icon: "common01/food/CustomRecipes/Soup/models/ui_soup_tomato_01.png",
    iconFromGh: true,
    modelShareGroup: "soup_fish_bowl",
    meshId: "Web_Soup_Mesh_FishBowl",
    meshSrc: "custom_recipes/Soup/Models/m_plated_fishprawn_soup_01.obj",
    meshKind: "obj",
    textureSrc: "custom_recipes/Soup/Models/t_fishprawnsoup_01.png",
  },
  {
    id: "Web_Soup_FishPrawnPorridge",
    category: "soup",
    zh: "Web 鱼虾粥",
    en: "Web Fish Prawn Porridge",
    backup: "custom_recipes/Soup/Porridge_FishandPrawn_OS.asset",
    icon: "custom_recipes/Soup/Models/鱼虾粥ui.png",
    meshId: "Web_Soup_FishPrawnPorridge",
    meshSrc: "custom_recipes/Soup/Models/鱼虾粥.obj",
    meshKind: "obj",
    textureSrc: "custom_recipes/Soup/Models/鱼虾粥.png",
  },
  {
    id: "Web_Soup_MushroomOnionTomato",
    category: "soup",
    zh: "Web 蘑菇洋葱番茄汤",
    en: "Web Mushroom Onion Tomato Soup",
    backup: "li_recipes/Soup/Soup_MushroomOnionTomato_SO.asset",
    icon: "li_recipes/Soup/1.png",
    fixType: 2,
    officialModel: "soup_onion",
  },
  {
    id: "Web_Soup_RiceEggTomato",
    category: "soup",
    zh: "Web 番茄鸡蛋烩饭汤",
    en: "Web Tomato Rice Egg Soup",
    backup: "li_recipes/Soup/Soup_RiceEggTomato_SO.asset",
    icon: "li_recipes/Soup/2.png",
    fixType: 2,
    officialModel: "soup_tomato_egg",
  },
  {
    id: "Web_Pudding_Mix_EggMilk",
    category: "pudding",
    zh: "Web 蛋奶糊（中间）",
    en: "Web Egg Milk Batter (Intermediate)",
    backup: "li_recipes/Pudding布丁/Mixed_EggMilk.asset",
    intermediate: true,
  },
  {
    id: "Web_Pudding_Mix_EggMilkOrange",
    category: "pudding",
    zh: "Web 橙子蛋奶糊（中间）",
    en: "Web Orange Egg Milk Batter (Intermediate)",
    backup: "li_recipes/Pudding布丁/Mixed_EggMilkOrange.asset",
    intermediate: true,
  },
  {
    id: "Web_Pudding_Mix_EggMilkStrawberry",
    category: "pudding",
    zh: "Web 草莓蛋奶糊（中间）",
    en: "Web Strawberry Egg Milk Batter (Intermediate)",
    backup: "li_recipes/Pudding布丁/Mixed_EggMilkStrawberry.asset",
    intermediate: true,
  },
  {
    id: "Web_Pudding_Plain",
    category: "pudding",
    zh: "Web 布丁",
    en: "Web Pudding",
    backup: "li_recipes/Pudding布丁/Cake_Pudding.asset",
    icon: "li_recipes/Pudding布丁/12/12.png",
    meshId: "Web_Pudding_Plain",
    meshSrc: "li_recipes/Pudding布丁/12/12.fbx",
    meshKind: "fbx",
    textureSrc: "li_recipes/Pudding布丁/12/texture_pbr_20250901.png",
  },
  {
    id: "Web_Pudding_Strawberry",
    category: "pudding",
    zh: "Web 草莓布丁",
    en: "Web Strawberry Pudding",
    backup: "li_recipes/Pudding布丁/Cake_StrawberryPudding.asset",
    icon: "li_recipes/Pudding布丁/15/15.png",
    meshId: "Web_Pudding_Strawberry",
    meshSrc: "li_recipes/Pudding布丁/15/15.fbx",
    meshKind: "fbx",
    textureSrc: "li_recipes/Pudding布丁/15/texture_pbr_20250901.png",
  },
  {
    id: "Web_Pudding_Orange",
    category: "pudding",
    zh: "Web 橙子布丁",
    en: "Web Orange Pudding",
    backup: "li_recipes/Pudding布丁/Cake_OrangePudding.asset",
    icon: "li_recipes/Pudding布丁/20/20.png",
    meshId: "Web_Pudding_Orange",
    meshSrc: "li_recipes/Pudding布丁/20/20.fbx",
    meshKind: "fbx",
    textureSrc: "li_recipes/Pudding布丁/20/texture_pbr_20250901.png",
  },
];

const md5 = (s) => crypto.createHash("md5").update(s).digest("hex");
const folderGuid = (rel) => md5(`commonw3-folder:${rel}`);
const recipeGuid = (id) => md5(`commonw3-recipe:${id}`);
const iconGuid = (id) => md5(`commonw3-icon:${id}`);
const modelGuid = (id) => md5(`commonw3-model:${id}`);
const assetGuid = (rel) => md5(`commonw3-asset:${rel}`);

const yamlStr = (s) =>
  s.replace(/[^\x20-\x7e]/g, (ch) => {
    const hex = ch.codePointAt(0).toString(16).toUpperCase().padStart(4, "0");
    return `\\u${hex}`;
  });

function resolveBackup(rel) {
  if (rel.startsWith("common01/")) return path.join(BACKUP_GH, rel);
  return path.join(BACKUP, rel);
}

function parseBackupAsset(rel) {
  const abs = resolveBackup(rel);
  const raw = fs.readFileSync(abs, "utf8");
  const scalar = (k) => {
    const m = raw.match(new RegExp(`^  ${k}: (.+)$`, "m"));
    if (!m) return null;
    const v = m[1].trim();
    if (v === "{fileID: 0}") return null;
    const g = v.match(/guid: ([a-f0-9]+)/);
    if (g) return g[1];
    if (/^\d+$/.test(v)) return parseInt(v, 10);
    return v;
  };
  const compositions = [];
  const sec = raw.match(/compositionSOs:\n((?:  - .+\n)+)/);
  if (sec) for (const m of sec[1].matchAll(/guid: ([a-f0-9]+)/g)) compositions.push(m[1]);
  return {
    type: scalar("type"),
    oldScore: scalar("score") ?? 0,
    cookingStepSO: scalar("cookingStepSO"),
    cookingStepIconSO: scalar("cookingStepIconSO"),
    cookingProgress: scalar("cookingProgress") ?? 0,
    mixingIconSO: scalar("mixingIconSO"),
    mixingProgress: scalar("mixingProgress") ?? 0,
    platingStepSO: scalar("platingStepSO"),
    compositionSOs: compositions,
  };
}

function remapCompositionGuid(g) {
  if (RECIPE_REMAP[g]) return recipeGuid(RECIPE_REMAP[g]);
  if (ING_REMAP[g]) return ING_REMAP[g];
  return g;
}

const recipeById = new Map(RECIPES.map((r) => [r.id, r]));

function expandLeaves(recipeId, seen = new Set()) {
  if (seen.has(recipeId)) return [];
  seen.add(recipeId);
  const r = recipeById.get(recipeId);
  const parsed = parseBackupAsset(r.backup);
  const out = [];
  for (const g of parsed.compositionSOs) {
    const rid = RECIPE_REMAP[g];
    if (rid) out.push(...expandLeaves(rid, seen));
    else out.push(remapCompositionGuid(g));
  }
  return out;
}

function countCookingChain(recipeId, seen = new Set()) {
  if (seen.has(recipeId)) return { cooking: 0, mixing: 0 };
  seen.add(recipeId);
  const r = recipeById.get(recipeId);
  const p = parseBackupAsset(r.backup);
  let cooking = p.cookingStepSO ? 1 : 0;
  let mixing = p.mixingIconSO ? 1 : 0;
  if (r.clearCooking) cooking = 0;
  for (const g of p.compositionSOs) {
    const rid = RECIPE_REMAP[g];
    if (rid) {
      const sub = countCookingChain(rid, seen);
      cooking += sub.cooking;
      mixing += sub.mixing;
    }
  }
  return { cooking, mixing };
}

function calcScore(r) {
  if (r.intermediate) return 0;
  const leaves = expandLeaves(r.id);
  const { cooking, mixing } = countCookingChain(r.id);
  const hasCooking = cooking > 0;
  const steps = leaves.length + cooking + mixing - (hasCooking ? 1 : 0);
  return 20 * steps;
}

function meshMd5(rel) {
  const abs = resolveBackup(rel);
  if (!fs.existsSync(abs)) return null;
  return md5(fs.readFileSync(abs));
}

function compressImage(src, dest, max = 512) {
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
    return Buffer.from(parsed.b64, "base64");
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

function texDiskName(recipeId, cls, ext = ".png") {
  return `${recipeId}_${cls}${ext}`;
}

/** FBX 模型引用的 fileID：Unity 导入模型的根对象恒为 100000；仅当 Unity 生成的
 *  .meta 子资产表（fileIDToRecycleName）明确列出 100002（Blender「根+子节点」结构，
 *  如 Web_Smoothie_Grape 的 "葡萄.001"）时才引用子对象以避开根节点变换。
 *  此前对 fbx 一律硬编码 100002 —— 单 Model 节点的 Maya/Max 导出（炒饭/布丁 8 道）
 *  并无 100002 子对象，引用悬空 → m_platingPrefab=null → 烤完无法装盘（2026-10-01 修复）。
 *  对齐 gen-commonw3-smoothies.mjs 的 meta 检测；meta 缺失/骨架时安全默认 100000。 */
function fbxModelFileID(r) {
  const mid = r.meshId ?? r.id;
  const metaPath = `${W3}/custom_recipes/${r.category}/models/${mid}/${mid}.fbx.meta`;
  try {
    if (fs.existsSync(metaPath) && /^\s+100002:/m.test(fs.readFileSync(metaPath, "utf8"))) {
      return 100002;
    }
  } catch {
    /* meta 不可读时按安全默认 */
  }
  return 100000;
}

function modelRefFor(r) {
  if (r.intermediate || r.officialModel) {
    if (r.officialModel) {
      const o = OFFICIAL_MODEL[r.officialModel];
      return { fileID: o.fileID, guid: o.guid, type: 2 };
    }
    return null;
  }
  const mid = r.meshId ?? r.id;
  const fileID =
    r.meshKind === "obj" || r.meshKind === "sharedCupObj" ? 100000 : fbxModelFileID(r);
  return { fileID, guid: modelGuid(mid), type: 3 };
}

function buildRecipeYaml(r, uid, parsed, score) {
  const type = r.fixType ?? parsed.type ?? 2;
  const cookingStepSO = r.clearCooking ? null : parsed.cookingStepSO;
  const cookingStepIconSO = r.clearCooking ? null : parsed.cookingStepIconSO;
  const cookingProgress = r.clearCooking ? 0 : parsed.cookingProgress ?? 0;
  const mixingIconSO = parsed.mixingIconSO;
  const mixingProgress = parsed.mixingProgress ?? 0;
  const plating = parsed.platingStepSO;
  const modelRef = modelRefFor(r);
  const compLines = parsed.compositionSOs
    .map((g) => `  - {fileID: 11400000, guid: ${remapCompositionGuid(g)}, type: 2}`)
    .join("\n");

  const iconBlock = r.intermediate
    ? "iconSO: {fileID: 0}\n  icon: {fileID: 0}"
    : `iconSO: {fileID: 0}\n  icon: {fileID: 21300000, guid: ${iconGuid(r.id)}, type: 3}`;

  const modelBlock = modelRef
    ? `model: {fileID: ${modelRef.fileID}, guid: ${modelRef.guid}, type: ${modelRef.type}}`
    : "model: {fileID: 0}";

  const cookBlock = cookingStepSO
    ? `cookingStepSO: {fileID: 11400000, guid: ${cookingStepSO}, type: 2}`
    : "cookingStepSO: {fileID: 0}";
  const cookIconBlock = cookingStepIconSO
    ? `cookingStepIconSO: {fileID: 11400000, guid: ${cookingStepIconSO}, type: 2}`
    : "cookingStepIconSO: {fileID: 0}";
  const mixBlock = mixingIconSO
    ? `mixingIconSO: {fileID: 11400000, guid: ${mixingIconSO}, type: 2}`
    : "mixingIconSO: {fileID: 0}";
  const plateBlock = plating
    ? `platingStepSO: {fileID: 11400000, guid: ${plating}, type: 2}`
    : "platingStepSO: {fileID: 0}";

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
  type: ${type}
  recipeName: ${r.id}
  uID: ${uid}
  score: ${score}
  ${plateBlock}
  modelSO: {fileID: 0}
  ${modelBlock}
  ${iconBlock}
  compositionSOs:
${compLines}
  optionalSOs: []
  ${cookBlock}
  ${cookIconBlock}
  cookingStepIcon: {fileID: 0}
  cookingProgress: ${cookingProgress}
  ${mixBlock}
  mixingIcon: {fileID: 0}
  mixingProgress: ${mixingProgress}
`;
}

// --- meta templates (abbreviated) ---
const FOLDER_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

const ASSET_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

const TEXT_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

const ICON_META = (guid) => `fileFormatVersion: 2
guid: ${guid}
TextureImporter:
  fileIDToRecycleName: {}
  externalObjects: {}
  serializedVersion: 4
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
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
  spriteMode: 1
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
  textureType: 8
  textureShape: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  platformSettings:
  - buildTarget: DefaultTexturePlatform
    maxTextureSize: 512
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
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: -1
    aniso: -1
    mipBias: -1
    wrapU: 1
    wrapV: 1
    wrapW: -1
  nPOTScale: 1
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

const OBJ_META = (guid) => `fileFormatVersion: 2
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

const sharedMeshWritten = new Set();

function writeSharedMesh(r, add) {
  const mid = r.meshId;
  if (!mid || sharedMeshWritten.has(mid)) return;
  sharedMeshWritten.add(mid);
  const cat = r.category;
  const modelDir = `${W3}/custom_recipes/${cat}/models/${mid}`;
  add(`${modelDir}.meta`, FOLDER_META(folderGuid(modelDir)), null);

  const srcAbs = resolveBackup(r.meshSrc);
  const baseColorName = `${mid}_base_color.png`;
  const baseColorRel = `${modelDir}/${baseColorName}`;

  if (r.meshKind === "fbx") {
    const disk = texDiskName(mid, "base_color");
    const texSrc = r.textureSrc ? resolveBackup(r.textureSrc) : null;
    const fbxRel = `${modelDir}/${mid}.fbx`;
    let fbxBytes = fs.readFileSync(srcAbs);
    if (texSrc && fs.existsSync(texSrc)) {
      const tmpTex = path.join(repoRoot, "layout-editor/scripts/.tmp-base.png");
      compressImage(texSrc, tmpTex);
      const diskNames = { base_color: disk };
      fbxBytes = renameFbxOnDisk(fbxBytes, diskNames);
      add(baseColorRel, fs.readFileSync(tmpTex), TEXTURE_META, assetGuid(baseColorRel), {
        binary: true,
        note: "base_color",
      });
      try {
        fs.unlinkSync(tmpTex);
      } catch {
        /* ignore */
      }
    }
    add(fbxRel, fbxBytes, FBX_META, modelGuid(mid), { binary: true, note: r.meshSrc });
  } else if (r.meshKind === "obj") {
    const objRel = `${modelDir}/${mid}.obj`;
    const mtlRel = `${modelDir}/${mid}.mtl`;
    const texSrc = resolveBackup(r.textureSrc);
    const tmpTex = path.join(repoRoot, "layout-editor/scripts/.tmp-base.png");
    compressImage(texSrc, tmpTex);
    add(baseColorRel, fs.readFileSync(tmpTex), TEXTURE_META, assetGuid(baseColorRel), {
      binary: true,
    });
    try {
      fs.unlinkSync(tmpTex);
    } catch {
      /* ignore */
    }
    const objRaw = fs.readFileSync(srcAbs, "utf8");
    const mtl = `newmtl mat_${mid}\nKd 1 1 1\nmap_Kd ${baseColorName}\n`;
    const obj = `mtllib ${mid}.mtl\no ${mid}\nusemtl mat_${mid}\n${objRaw.replace(/^mtllib.*\n/m, "").replace(/^#.*\n/gm, "")}`;
    add(mtlRel, mtl, TEXT_META, assetGuid(mtlRel));
    add(objRel, obj, OBJ_META, modelGuid(mid), { note: r.meshSrc });
  } else if (r.meshKind === "sharedCupObj" && mid === "Web_MilkSlush_SharedCup") {
    if (!fs.existsSync(SHARED_CUP_OBJ)) throw new Error(`共享杯体缺失: ${SHARED_CUP_OBJ}`);
    const texSrc = resolveBackup("custom_recipes/YinLiao/Models/菠萝牛奶冰沙ui.png");
    const tmpTex = path.join(repoRoot, "layout-editor/scripts/.tmp-cup.png");
    compressImage(texSrc, tmpTex, 512);
    add(baseColorRel, fs.readFileSync(tmpTex), TEXTURE_META, assetGuid(baseColorRel), { binary: true });
    try {
      fs.unlinkSync(tmpTex);
    } catch {
      /* ignore */
    }
    const cupRaw = fs.readFileSync(SHARED_CUP_OBJ, "utf8");
    const obj = cupRaw
      .replace(/^mtllib.*\n/m, `mtllib ${mid}.mtl\n`)
      .replace(/^o .*\n/m, `o ${mid}\n`)
      .replace(/^usemtl .*\n/m, `usemtl mat_${mid}\n`);
    const mtl = `newmtl mat_${mid}\nKd 1 1 1\nmap_Kd ${baseColorName}\n`;
    add(`${modelDir}/${mid}.mtl`, mtl, TEXT_META, assetGuid(`${modelDir}/${mid}.mtl`));
    add(`${modelDir}/${mid}.obj`, obj, OBJ_META, modelGuid(mid), {
      note: "什锦果汁杯体（113MB 源 FBX 网格过密，改用已压缩杯模）",
    });
  }
}

function buildPlan() {
  const out = [];
  const add = (rel, content, metaFn, metaGuidVal, opts = {}) => {
    const g = metaGuidVal ?? assetGuid(rel);
    const meta = metaFn ? metaFn(g) : null;
    out.push({ rel, content, meta, binary: !!opts.binary, note: opts.note ?? "" });
  };

  for (const c of CATEGORIES) {
    const base = `${W3}/custom_recipes/${c.id}`;
    add(`${base}.meta`, FOLDER_META(folderGuid(base)), null);
    add(`${base}/icons.meta`, FOLDER_META(folderGuid(`${base}/icons`)), null);
    add(`${base}/models.meta`, FOLDER_META(folderGuid(`${base}/models`)), null);
  }

  const auditRows = [];
  const meshGroups = new Map();

  RECIPES.forEach((r, i) => {
    const uid = FIRST_UID + i;
    const parsed = parseBackupAsset(r.backup);
    const score = calcScore(r);
    const meshHash = r.meshSrc ? meshMd5(r.meshSrc) : null;
    if (r.modelShareGroup) {
      if (!meshGroups.has(r.modelShareGroup)) meshGroups.set(r.modelShareGroup, []);
      meshGroups.get(r.modelShareGroup).push(r.id);
    }
    auditRows.push({
      id: r.id,
      uid,
      oldScore: parsed.oldScore,
      newScore: score,
      meshSrc: r.meshSrc ?? (r.officialModel ? `official:${r.officialModel}` : "—"),
      meshMd5: meshHash ?? "—",
      modelShareGroup: r.modelShareGroup ?? "",
      modelDataIssue: r.modelDataIssue ?? "",
      targetModelGuid: modelRefFor(r)?.guid ?? "—",
    });

    const assetRel = `${W3}/custom_recipes/${r.category}/${r.id}.asset`;
    add(assetRel, buildRecipeYaml(r, uid, parsed, score), ASSET_META, recipeGuid(r.id), {
      note: `${r.zh} uid=${uid} score ${parsed.oldScore}→${score}`,
    });

    if (!r.intermediate && r.icon) {
      const iconRel = `${W3}/custom_recipes/${r.category}/icons/${r.id}.png`;
      const iconSrc = resolveBackup(r.icon);
      const tmp = path.join(repoRoot, "layout-editor/scripts/.tmp-icon.png");
      compressImage(iconSrc, tmp, 128);
      add(iconRel, fs.readFileSync(tmp), ICON_META, iconGuid(r.id), { binary: true });
      try {
        fs.unlinkSync(tmp);
      } catch {
        /* ignore */
      }
    }

    if (r.meshId && !r.officialModel) {
      const leader = RECIPES.find((x) => x.meshId === r.meshId && x.meshSrc);
      if (leader) writeSharedMesh(leader, add);
    }
  });

  add(`${W3}/custom_recipes/names.json`, buildNamesJson(), TEXT_META, null);
  add(`${W3}/custom_recipes/CustomRecipeConfig.asset`, buildConfigAsset(), ASSET_META, null);

  return { plan: out, auditRows, meshGroups };
}

function buildNamesJson() {
  const p = path.join(repoRoot, "Assets/commonW3/custom_recipes/names.json");
  const existing = fs.existsSync(p)
    ? JSON.parse(fs.readFileSync(p, "utf8").replace(/^\uFEFF/, ""))
    : { schemaVersion: 1, names: [] };
  const batchIds = new Set(RECIPES.map((r) => r.id));
  const names = (existing.names ?? []).filter((n) => !batchIds.has(n.id));
  for (const r of RECIPES) names.push({ id: r.id, zh: r.zh, en: r.en });
  return "\uFEFF" + JSON.stringify({ schemaVersion: 1, names }, null, 2) + "\n";
}

function buildConfigAsset() {
  const existingRaw = fs.existsSync(CONFIG_PATH)
    ? fs.readFileSync(CONFIG_PATH, "utf8")
    : "";
  const existingNext = /^  nextSequence: (\d+)/m.exec(existingRaw)?.[1];
  const calculatedNext = FIRST_UID + RECIPES.length;
  const nextSequence = Math.max(Number(existingNext) || 0, calculatedNext);
  const cats = [
    { id: "salad", zh: "沙拉大全", en: "Salad" },
    { id: "smoothie", zh: "Web 果汁大全", en: "Web Smoothie" },
    ...CATEGORIES,
    { id: "ice_cream", zh: "冰淇淋", en: "Ice Cream" },
    { id: "milk_slush", zh: "牛奶冰沙", en: "Milk Slush" },
  ];
  const catYaml = cats
    .map((c) => `  - id: ${c.id}\n    zh: "${yamlStr(c.zh)}"\n    en: ${c.en}`)
    .join("\n");
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
  m_Script: {fileID: 11500000, guid: ${SCRIPT_RECIPE_CONFIG}, type: 3}
  m_Name: CustomRecipeConfig
  m_EditorClassIdentifier: 
  uidPrefix: ${UID_PREFIX}
  nextSequence: ${nextSequence}
  categories:
${catYaml}
  subcategories: []
  modelTransforms: []
`;
}

function writeManifest(auditRows, meshGroups) {
  const lines = [
    "# commonW3 batch1 迁移清单",
    "",
    `生成时间：${new Date().toISOString()}`,
    "",
    "## 模型共用审计",
    "",
    "| recipe | sourceModel | meshMd5 | shareGroup | dataIssue | targetModelGuid |",
    "|--------|-------------|---------|------------|-----------|-----------------|",
  ];
  for (const a of auditRows) {
    lines.push(
      `| ${a.id} | ${a.meshSrc} | ${a.meshMd5} | ${a.modelShareGroup || "—"} | ${a.modelDataIssue || "—"} | ${a.targetModelGuid} |`,
    );
  }
  lines.push("", "## 分数对照", "", "| recipe | uID | 旧分 | 新分 |", "|--------|-----|------|------|");
  for (const a of auditRows) {
    lines.push(`| ${a.id} | ${a.uid} | ${a.oldScore} | ${a.newScore} |`);
  }
  lines.push("", "## 共用组", "");
  for (const [g, ids] of meshGroups) {
    lines.push(`- **${g}**: ${ids.join(", ")}`);
  }
  fs.mkdirSync(path.dirname(MANIFEST_MD), { recursive: true });
  fs.writeFileSync(MANIFEST_MD, lines.join("\n") + "\n", "utf8");
}

const LAYOUT_RECIPE_ICONS = path.join(repoRoot, "layout-editor/web/public/icons/recipes");
const LAYOUT_DIST_RECIPE_ICONS = path.join(repoRoot, "layout-editor/web/dist/icons/recipes");

function syncLayoutEditorIcons() {
  fs.mkdirSync(LAYOUT_RECIPE_ICONS, { recursive: true });
  const hasDist = fs.existsSync(path.dirname(LAYOUT_DIST_RECIPE_ICONS));
  if (hasDist) fs.mkdirSync(LAYOUT_DIST_RECIPE_ICONS, { recursive: true });
  for (const r of RECIPES) {
    if (r.intermediate || !r.icon) continue;
    const src = path.join(repoRoot, `Assets/commonW3/custom_recipes/${r.category}/icons/${r.id}.png`);
    if (!fs.existsSync(src)) continue;
    fs.copyFileSync(src, path.join(LAYOUT_RECIPE_ICONS, `${r.id}.png`));
    if (hasDist) fs.copyFileSync(src, path.join(LAYOUT_DIST_RECIPE_ICONS, `${r.id}.png`));
  }
}

const apply = process.argv.includes("--apply");
const syncIconsOnly = process.argv.includes("--sync-icons");
if (syncIconsOnly) {
  syncLayoutEditorIcons();
  console.log("已同步 batch1 图标到 layout-editor。");
  process.exit(0);
}
const { plan, auditRows, meshGroups } = buildPlan();
writeManifest(auditRows, meshGroups);

for (const item of plan) {
  if (!apply) {
    console.log(`[dry] ${item.rel}${item.note ? ` (${item.note})` : ""}`);
    continue;
  }
  const abs = path.join(repoRoot, item.rel);
  fs.mkdirSync(path.dirname(abs), { recursive: true });
  if (item.binary) fs.writeFileSync(abs, item.content);
  else fs.writeFileSync(abs, item.content, "utf8");
  if (item.meta) fs.writeFileSync(abs + ".meta", item.meta, "utf8");
  console.log(`写入 ${item.rel}${item.note ? ` (${item.note})` : ""}`);
}

if (!apply) {
  console.log(`\ndry-run：${plan.length} 个文件；manifest → ${MANIFEST_MD}`);
  console.log("加 --apply 执行落盘。");
} else {
  syncLayoutEditorIcons();
  console.log(`\n完成：${plan.length} 个文件已写入。`);
  console.log("如需重新烘焙走 web 菜谱管理流程 → Build AssetBundles（commonW3）");
}
