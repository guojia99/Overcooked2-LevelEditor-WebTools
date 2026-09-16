#!/usr/bin/env node
/**
 * gen-burger-model-pool.mjs —— 从 dump_bundle 提取「可用作汉堡堆叠层的原版模型」清单。
 *
 * 产出 layout-editor/scripts/data/burger-model-pool.json（入库，约 80KB），
 * 后端汉堡工作台读它列出可绑定的原版模型指针，**不依赖 7.7GB 的 dump_bundle 本体**。
 *
 * 筛选规则：
 *   - type == GameObject 且 container 以 .prefab 结尾
 *   - container 落在 任意目录下的 prefabs/{plated,ingredients,recipes,meals}/ 中
 *   - 所在 bundle 必须存在于 Assets/StreamingAssets/Windows（否则运行时 LoadAsset 会崩）
 *
 * 用法：node layout-editor/scripts/gen-burger-model-pool.mjs
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, "../..");
const MANIFEST = path.join(repoRoot, "dump_bundle/manifest.json");
const STREAMING = path.join(repoRoot, "Assets/StreamingAssets/Windows");
const OUT = path.join(here, "data/burger-model-pool.json");

const KIND_RE = /\/prefabs\/(plated|ingredients|recipes|meals)\//;
const DLC_RE = /^assets[\\/]downloadablecontent[\\/](dlc\d+)[\\/]/i;
/** 名字里带 plated/prep/recipe 的通常是「摆在容器里的成品形态」，最适合当堆叠层。 */
const STACKABLE_NAME_RE = /(plated|prep|recipe)/i;

/** 与后端 LayoutEditorBurgerApi 的名称归一保持同一口径，用于「按食材名推荐模型」。 */
function normalizeId(name) {
  let s = name.toLowerCase();
  for (const p of [/^p_dlc\d+_/, /^dlc\d+_/, /^dlc\d+/, /^m_/]) s = s.replace(p, "");
  s = s.replace(/^(plated|prep|recipe|chopped|ui)_/, "");
  s = s.replace(/_\d+$/, "");
  s = s.replace(/[^a-z]/g, "");
  s = s.replace(/so$/, "");
  return s;
}

function main() {
  if (!fs.existsSync(MANIFEST)) {
    console.error(
      "未找到 dump_bundle/manifest.json。\n" +
        "先在 Unity 里跑 Tools → Layout Editor → Dump Bundles，或执行 layout-editor/scripts/dump-bundle-all.py。"
    );
    process.exit(1);
  }
  const haveStreaming = fs.existsSync(STREAMING);
  if (!haveStreaming) {
    console.warn("⚠ 未找到 Assets/StreamingAssets/Windows，跳过 bundle 存在性过滤（产物可能含不可用条目）。");
  }
  const bundleExists = (b) => !haveStreaming || fs.existsSync(path.join(STREAMING, b));

  const manifest = JSON.parse(fs.readFileSync(MANIFEST, "utf8"));
  const seen = new Set();
  const items = [];
  let skippedMissingBundle = 0;

  for (const o of manifest.objects ?? []) {
    if (o.type !== "GameObject") continue;
    const container = o.container ?? "";
    if (!container.endsWith(".prefab")) continue;
    const m = KIND_RE.exec(container);
    if (!m) continue;
    if (!bundleExists(o.bundle)) {
      skippedMissingBundle++;
      continue;
    }
    const id = container.slice(container.lastIndexOf("/") + 1, -".prefab".length);
    const key = o.bundle + "|" + container;
    if (seen.has(key)) continue;
    seen.add(key);
    const dlc = DLC_RE.exec(container);
    items.push({
      id,
      bundle: o.bundle,
      // 逐字符照抄 container：PseudoPrefabSO.assetPath 大小写敏感，不可改写
      assetPath: container,
      kind: m[1],
      dlc: dlc ? dlc[1].toLowerCase() : "",
      norm: normalizeId(id),
      stackable: STACKABLE_NAME_RE.test(id),
    });
  }

  items.sort((a, b) => a.id.localeCompare(b.id) || a.bundle.localeCompare(b.bundle));

  fs.mkdirSync(path.dirname(OUT), { recursive: true });
  fs.writeFileSync(
    OUT,
    JSON.stringify(
      {
        generatedAt: new Date().toISOString(),
        source: "dump_bundle/manifest.json",
        note: "汉堡堆叠层可绑定的原版模型池；assetPath 逐字符照抄 bundle 内实名（大小写敏感）。",
        count: items.length,
        items,
      },
      null,
      1
    ) + "\n",
    "utf8"
  );

  const byKind = items.reduce((acc, x) => ((acc[x.kind] = (acc[x.kind] ?? 0) + 1), acc), {});
  const bundles = new Set(items.map((x) => x.bundle));
  console.log(`已写出 ${path.relative(repoRoot, OUT)}`);
  console.log(`  条目 ${items.length}（${JSON.stringify(byKind)}），来自 ${bundles.size} 个 bundle`);
  console.log(`  其中名字含 plated/prep/recipe（最适合当堆叠层）：${items.filter((x) => x.stackable).length}`);
  if (skippedMissingBundle > 0) console.log(`  跳过 ${skippedMissingBundle} 条（bundle 不在 StreamingAssets）`);
}

main();
