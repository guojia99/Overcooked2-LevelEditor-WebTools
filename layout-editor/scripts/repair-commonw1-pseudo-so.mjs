#!/usr/bin/env node
/**
 * 修复 commonW1 包装 prefab 悬空的 pseudoPrefabSO 引用。
 *
 * 背景：commonW1 的 art/food 装饰 prefab 全是「伪 prefab 空壳」，靠
 * pseudoPrefabSO 指向的 PseudoPrefabSO 资产（bundleName + assetPath）在运行时
 * 从 AssetBundle 加载真实模型。common03 对齐 upstream（b499bd15e）等清理后，
 * 68 个 prefab 引用的 SO guid 在工程里不存在 → stub.pseudoPrefabSO 反序列化为
 * null → PseudoPrefab.ResetChild() 里 LoadAsset 抛 NRE（PseudoPrefabManager.cs:343），
 * 子物体永不实例化 → 场景/游戏内「显示异常」（如 p_dlc4_map_bridge_02、
 * city_kevin_02）。
 *
 * 修复两条路径（guid 均保持与 prefab 引用一致，场景零改动）：
 *   1) decor-entries.json 内的条目：按确定性 guid = md5("pseudo:<id>") 重新生成
 *      SO，写到 commonW1/pseudo_prefab_so/<镜像 prefab 相对路径>；
 *   2) 食材装饰（guid 指向被删的 common03/Ingredients SO）：从 git 历史
 *      b499bd15e^ 原样恢复 .asset + .meta（guid 不变），落到
 *      commonW1/pseudo_prefab_so/<group>/decor/food/。
 *
 *   node layout-editor/scripts/repair-commonw1-pseudo-so.mjs [--dry]
 */
import crypto from "crypto";
import { execSync } from "child_process";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, "../..");
const W1_ROOT = path.join(repoRoot, "Assets/commonW1");
const PREFAB_ROOT = path.join(W1_ROOT, "prefabs");
const SO_ROOT = path.join(W1_ROOT, "pseudo_prefab_so");
const ENTRIES_PATH = path.join(repoRoot, "layout-editor/scripts/data/decor-entries.json");
const FOOD_SRC_COMMIT = "b499bd15e^"; // common03 对齐 upstream 前一版（食材 SO 尚在）

const DRY = process.argv.includes("--dry");
const SCRIPT_GUID = "0cff7c13895ab9e47a5e02d4619cc3b9"; // PseudoPrefabSO.cs

const newGuid = () => crypto.randomBytes(16).toString("hex");
const detGuid = (id) => crypto.createHash("md5").update(`pseudo:${id}`).digest("hex");

function write(file, content) {
  if (DRY) {
    console.log(`  [dry] ${path.relative(repoRoot, file)}`);
    return;
  }
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content, "utf8");
}

/** 目录 .meta：镜像现有 pseudo_prefab_so 目录（assetBundleName: commonW1）。 */
function ensureFolderMeta(dir) {
  const meta = dir + ".meta";
  if (fs.existsSync(meta)) return;
  const body = `fileFormatVersion: 2
guid: ${newGuid()}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName: commonW1
  assetBundleVariant:
`;
  if (DRY) {
    console.log(`  [dry] ${path.relative(repoRoot, meta)}`);
    return;
  }
  fs.writeFileSync(meta, body, "utf8");
}

function pseudoPrefabAsset(id, prefabName, bundleName, assetPath) {
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
  m_Script: {fileID: 11500000, guid: ${SCRIPT_GUID}, type: 3}
  m_Name: ${id}
  m_EditorClassIdentifier:
  prefabName: ${prefabName}
  bundleName: ${bundleName}
  assetPath: ${assetPath}
`;
}

const metaGuid = (metaPath) => {
  const m = fs.readFileSync(metaPath, "utf8").match(/^guid:\s*([a-f0-9]{32})/m);
  return m ? m[1] : null;
};

// ---------------------------------------------------------------------------
// 1. 收集工程内全部 guid + 悬空引用清单
// ---------------------------------------------------------------------------
console.log("扫描 commonW1 prefab 悬空 pseudoPrefabSO 引用…");
const projectGuids = new Set();
const walk = (dir, onFile) => {
  for (const n of fs.readdirSync(dir)) {
    const p = path.join(dir, n);
    if (fs.statSync(p).isDirectory()) walk(p, onFile);
    else onFile(p);
  }
};
walk(path.join(repoRoot, "Assets"), (p) => {
  if (p.endsWith(".meta")) {
    const g = metaGuid(p);
    if (g) projectGuids.add(g);
  }
});

const entries = {};
for (const e of JSON.parse(fs.readFileSync(ENTRIES_PATH, "utf8")).entries || []) {
  entries[e.id] = e;
}

const dangling = [];
walk(PREFAB_ROOT, (p) => {
  if (!p.endsWith(".prefab")) return;
  const txt = fs.readFileSync(p, "utf8");
  const m = txt.match(/pseudoPrefabSO:\s*\{fileID:\s*\d+,\s*guid:\s*([a-f0-9]{32}),/);
  if (!m) return;
  if (!projectGuids.has(m[1])) {
    dangling.push({ prefab: p, guid: m[1], id: path.basename(p, ".prefab") });
  }
});
console.log(`  悬空引用: ${dangling.length} 个`);

// ---------------------------------------------------------------------------
// 2. 修复
// ---------------------------------------------------------------------------
let fixedEntries = 0;
let fixedFood = 0;
const unresolved = [];

for (const { prefab, guid, id } of dangling) {
  const rel = path.relative(PREFAB_ROOT, prefab); // e.g. dlc04/art/dlc04/x.prefab
  const soRel = rel.replace(/\.prefab$/, ".asset");

  // 路径 A：decor-entries 重建（确定性 guid）
  const e = entries[id];
  if (e && guid === detGuid(id)) {
    const assetPath = "Assets/" + e.container.replace(/^assets\//, "");
    const soFile = path.join(SO_ROOT, soRel);
    write(soFile, pseudoPrefabAsset(id, id, e.bundle, assetPath));
    write(soFile + ".meta", `fileFormatVersion: 2\nguid: ${guid}\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName: commonW1\n  assetBundleVariant:\n`);
    fixedEntries++;
    console.log(`  + SO重建 ${soRel} -> ${e.bundle}`);
    continue;
  }

  // 路径 B：食材 SO 从 git 历史恢复（.meta 原样带回，guid 不变）
  const dlc = rel.split(path.sep)[0];
  const srcBase = `Assets/common03/Ingredients/${dlc}/${id}`;
  let restored = false;
  try {
    const asset = execSync(`git show ${FOOD_SRC_COMMIT}:${srcBase}.asset`, { cwd: repoRoot }).toString("utf8");
    const assetMeta = execSync(`git show ${FOOD_SRC_COMMIT}:${srcBase}.asset.meta`, { cwd: repoRoot }).toString("utf8");
    if (metaGuidOf(assetMeta) === guid) {
      const soFile = path.join(SO_ROOT, path.dirname(soRel), id + ".asset");
      write(soFile, asset);
      write(soFile + ".meta", assetMeta);
      fixedFood++;
      console.log(`  + SO恢复 ${path.dirname(soRel)}/${id}.asset (guid 保持)`);
      restored = true;
    }
  } catch (_) {
    /* git 里也没有，落入 unresolved */
  }
  if (restored) continue;

  // 路径 C（回退）：剥掉 dlcNN_ 前缀后在 pidmap 里找基础版 prefab
  // （如 dlc13_workstation_bin_01 → shared_kitchen/workstation_bin_01）。
  // 仅当引用 guid 是确定性 guid 时才可安全重建；控制台显式警告这是基础版回退。
  if (guid === detGuid(id)) {
    const baseId = id.replace(/^dlc\d+_/, "");
    const pidmapDir = path.join(__dirname, "oc2-import/.cache");
    let fallback = null;
    if (fs.existsSync(pidmapDir)) {
      for (const n of fs.readdirSync(pidmapDir)) {
        const mm = n.match(/^pidmap_(bundle\d+)\.json$/);
        if (!mm) continue;
        try {
          const d = JSON.parse(fs.readFileSync(path.join(pidmapDir, n), "utf8"));
          for (const container of Object.values(d)) {
            const base = container.rsplit?.("/", 1).pop() ?? container.split("/").pop();
            if (base === baseId + ".prefab") {
              fallback = { bundle: mm[1], container };
              break;
            }
          }
        } catch (_) {}
        if (fallback) break;
      }
    }
    if (fallback) {
      const assetPath = "Assets/" + fallback.container.replace(/^assets\//, "");
      const soFile = path.join(SO_ROOT, soRel);
      write(soFile, pseudoPrefabAsset(id, id, fallback.bundle, assetPath));
      write(soFile + ".meta", `fileFormatVersion: 2\nguid: ${guid}\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName: commonW1\n  assetBundleVariant:\n`);
      console.log(`  + SO回退 ${soRel} -> ${fallback.bundle}（基础版 ${baseId}，皮肤非 DLC 专属）`);
      fixedEntries++;
      continue;
    }
  }

  unresolved.push({ id, guid, prefab: rel });
}

function metaGuidOf(txt) {
  const m = txt.match(/^guid:\s*([a-f0-9]{32})/m);
  return m ? m[1] : null;
}

// 新目录补 folder meta（assetBundleName: commonW1）
if (!DRY) {
  walk(SO_ROOT, (p) => {
    if (fs.statSync(p).isDirectory()) ensureFolderMeta(p);
  });
  ensureFolderMeta(SO_ROOT);
}

// ---------------------------------------------------------------------------
// 3. 汇总
// ---------------------------------------------------------------------------
console.log(`\n完成：decor-entries 重建 ${fixedEntries} 个，git 恢复食材 ${fixedFood} 个。`);
if (unresolved.length) {
  console.log(`\n仍未解决 ${unresolved.length} 个（多为 map/water 网格类占位，无 bundle prefab 数据源）：`);
  for (const u of unresolved) console.log(`  - ${u.prefab} (guid ${u.guid})`);
}
