#!/usr/bin/env node
// Independent of the original 21 recipes and their icons.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const dir = 'Assets/commonW3/custom_recipes/salad';
const md5 = s => crypto.createHash('md5').update(s).digest('hex');
const read = p => fs.readFileSync(path.join(root, p), 'utf8').replace(/^\uFEFF/, '');
const guid = p => read(p + '.meta').match(/^guid: (\w+)/m)[1];
const ref = g => `{fileID: 11400000, guid: ${g}, type: 2}`;
const ingredientPaths = {
  LettuceSO: ['Assets/common01/food/Ingredients/LettuceSO.asset', 19478],
  TomatoSO: ['Assets/common01/food/Ingredients/TomatoSO.asset', 15580],
  CucumberSO: ['Assets/common01/food/Ingredients/CucumberSO.asset', 37422],
  dlc11_lettuce: ['Assets/common03/food/Ingredients/dlc11/dlc11_lettuce.asset', 152142],
  dlc11_tomato: ['Assets/common03/food/Ingredients/dlc11/dlc11_tomato.asset', 151504],
  dlc11_cucumber: ['Assets/common03/food/Ingredients/dlc11/dlc11_cucumber.asset', 151714],
  dlc11_corn: ['Assets/common03/food/Ingredients/dlc11/dlc11_corn.asset', 37874],
  dlc11_onion_salad: ['Assets/common03/food/Ingredients/dlc11/dlc11_onion_salad.asset', 390802],
  dlc11_ketchup: ['Assets/commonW1/pseudo_prefab_so/dlc11/food/dlc11_ketchup.asset', 890084],
  dlc11_mustard: ['Assets/commonW1/pseudo_prefab_so/dlc11/food/dlc11_mustard.asset', 890076],
};
const ingredients = Object.fromEntries(Object.entries(ingredientPaths).map(([id, [assetPath, uid]]) =>
  [id, { id, assetPath, uid, guid: guid(assetPath) }]));
const groups = ['core', 'dlc11', 'commonw3'];
const manifestPath = 'layout-editor/scripts/data/salad-sauce-recipes.json';
const previous = fs.existsSync(path.join(root, manifestPath)) ? JSON.parse(read(manifestPath)) : null;
const sources = previous ? previous.bases.flatMap(b => [b.id, ...b.aliases].map(id => ({
  id, group: b.group, guid: b.sourceGuid, assetPath: b.sourceAssetPath,
  nameZh: b.nameZh, nameEn: b.nameEn, ingredients: b.ingredients,
}))) : groups.flatMap(group => JSON.parse(read(`layout-editor/web/public/recipes/${group}.json`)).recipes
  .filter(r => !r.id.startsWith('Web_SauceSalad_') && r.type === 'salad' && !r.intermediate && (!r.saladSubgroups || r.saladSubgroups.includes('no_sauce')))
  .map(r => ({ ...r, group })));
assert.equal(sources.length, 30, 'Expected original story 3 + DLC11 6 + Web 21');
const key = ids => ids.map(id => { assert.ok(ingredients[id], id); return ingredients[id].uid; }).sort((a,b) => a-b).join(',');
const bases = [];
for (const r of sources) {
  const ids = r.compositionIds || r.ingredients;
  const k = key(ids);
  let base = bases.find(b => b.uidKey === k);
  if (base) { base.aliases.push(r.id); continue; }
  base = { id: r.id, group: r.group, sourceGuid: r.guid, sourceAssetPath: r.assetPath,
    nameZh: r.nameZh, nameEn: r.nameEn, ingredients: ids, uidKey: k, aliases: [],
    family: r.group === 'core' ? 'Story' : 'DLC11' };
  bases.push(base);
}
assert.equal(bases.length, 26, 'Ingredient UID deduplication must yield 26 bases');
const sauces = [
  { id: 'Ketchup', zh: '红酱', en: 'Ketchup', ingredients: ['dlc11_ketchup'] },
  { id: 'Mustard', zh: '黄酱', en: 'Mustard', ingredients: ['dlc11_mustard'] },
  { id: 'KetchupMustard', zh: '双酱', en: 'Ketchup & Mustard', ingredients: ['dlc11_ketchup', 'dlc11_mustard'] },
];
const recipes = bases.flatMap((base, i) => sauces.map((s, j) => {
  const id = `Web_SauceSalad_${base.id}_${s.id}`;
  return { id, uid: 58322401 + i * 3 + j, guid: md5('commonw3-recipe:' + id),
    iconGuid: md5('commonw3-icon:' + id), iconPath: `${dir}/icons/${id}.png`,
    assetPath: `${dir}/${id}.asset`, baseId: base.id, sourceIds: [base.id, ...base.aliases],
    family: base.family, sauce: s.id, nameZh: `${base.nameZh}·${s.zh}`, nameEn: `${base.nameEn} (${s.en})`,
    ingredients: [...base.ingredients, ...s.ingredients], score: (base.ingredients.length + s.ingredients.length) * 20 };
}));
assert.equal(new Set(recipes.map(r => key(r.ingredients))).size, 78);
const optionals = ['Story', 'DLC11'].flatMap(family => {
  const ids = [...new Set(bases.filter(b => b.family === family).flatMap(b => b.ingredients))];
  return ids.flatMap((anchor, i) => sauces.map((s, j) => ({
    id: `Web_SauceSalad_Optional_${family}_${anchor}_${s.id}`, family,
    uid: 58322601 + (family === 'Story' ? 0 : 30) + i * 3 + j,
    ingredients: [anchor, ...s.ingredients], optional: ids.filter(id => id !== anchor), score: 0,
  })));
});
const manifest = { schemaVersion: 1, bundle: 'commonW3',
  sauceUidPolicy: { canonical: { ketchup: 890084, mustard: 890076 },
    saladOnlyAliases: { '158482': 890084, '17094': 890076 },
    nativeDispenserVisualSlots: { mustard: 0, ketchup: 1 } },
  ingredients, bases, recipes, optionals };
const outputs = new Map();
const meta = g => `fileFormatVersion: 2\nguid: ${g}\nNativeFormatImporter:\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
function add(p, text) { outputs.set(p, text); }
function asset(p, text, g) { add(p, text); if (!fs.existsSync(path.join(root, p + '.meta'))) add(p + '.meta', meta(g));
  else assert.equal(guid(p), g, `Existing GUID differs: ${p}`); }
for (const family of ['Story', 'DLC11']) {
  const id = `Web_SauceSalad_${family}_Model`;
  const prefabPath = `${dir}/models/${id}.prefab`;
  const modelPath = `${dir}/models/${id}.asset`;
  if (fs.existsSync(path.join(root, prefabPath)) && read(modelPath).includes('asset: {fileID:') && !read(modelPath).includes('asset: {fileID: 0}')) continue;
  const directRef = fs.existsSync(path.join(root, modelPath))
    ? read(modelPath).match(/^  asset: (.*)$/m)?.[1] || '{fileID: 0}' : '{fileID: 0}';
  asset(modelPath, `%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_Script: {fileID: 11500000, guid: 0cff7c13895ab9e47a5e02d4619cc3b9, type: 3}\n  m_Name: ${id}\n  prefabName: ${id}\n  bundleName: commonW3\n  assetPath: ${prefabPath}\n  asset: ${directRef}\n`, md5('commonw3-salad-model:' + family));
  // Unity factory sets the actual prefab local fileID on this direct reference.
}
for (const id of ['Web_SauceSalad_Story_Model', 'Web_SauceSalad_DLC11_Model', 'SaladKetchupVisual', 'SaladMustardVisual']) {
  const p = `${dir}/models/${id}.prefab.meta`;
  if (!fs.existsSync(path.join(root, p))) add(p, `fileFormatVersion: 2\nguid: ${md5('commonw3-salad-prefab:' + id)}\nNativeFormatImporter:\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
}
function recipe(r, optional = false) {
  const id = r.id;
  const template = read(`${dir}/Web_Salad_Lettuce.asset`);
  let text = template.replace(/m_Name: .*/, 'm_Name: ' + id).replace(/recipeName: .*/, 'recipeName: ' + id)
    .replace(/uID: \d+/, 'uID: ' + r.uid).replace(/score: \d+/, 'score: ' + r.score)
    .replace(/modelSO: .*/, 'modelSO: ' + ref(md5('commonw3-salad-model:' + r.family)))
    .replace(/icon: .*/, optional ? 'icon: {fileID: 0}' : `icon: {fileID: 21300000, guid: ${r.iconGuid}, type: 3}`)
    .replace(/compositionSOs:[\s\S]*?  cookingStepSO:/, `compositionSOs:\n${r.ingredients.map(id => '  - ' + ref(ingredients[id].guid)).join('\n')}\n  optionalSOs:${r.optional?.length ? '\n' + r.optional.map(id => '  - ' + ref(ingredients[id].guid)).join('\n') : ' []'}\n  cookingStepSO:`);
  asset(`${dir}/${id}.asset`, text, md5('commonw3-recipe:' + id));
}
recipes.forEach(r => recipe(r));
optionals.forEach(r => recipe(r, true));
const namesPath = 'Assets/commonW3/custom_recipes/names.json';
const names = JSON.parse(read(namesPath));
names.names = [...names.names.filter(r => !r.id.startsWith('Web_SauceSalad_')), ...recipes.map(r => ({ id: r.id, zh: r.nameZh, en: r.nameEn }))];
add(namesPath, '\uFEFF' + JSON.stringify(names, null, 4));
const configPath = 'Assets/commonW3/custom_recipes/CustomRecipeConfig.asset';
add(configPath, read(configPath).replace(/nextSequence: (\d+)/, (_, n) => 'nextSequence: ' + Math.max(+n, 680)));
add(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
const allIds = [...recipes, ...optionals].map(r => r.uid);
assert.equal(new Set(allIds).size, allIds.length, 'Generated UID collision');
assert.equal(new Set(recipes.map(r => r.id)).size, 78, 'Generated ID collision');
const apply = process.argv.includes('--apply');
const check = process.argv.includes('--check');
let changed = 0;
const expected = new Set([...recipes, ...optionals].map(r => r.id + '.asset'));
for (const name of fs.readdirSync(path.join(root, dir))) {
  if (!name.startsWith('Web_SauceSalad_') || !name.endsWith('.asset') || expected.has(name)) continue;
  changed++;
  if (apply) {
    fs.unlinkSync(path.join(root, dir, name));
    if (fs.existsSync(path.join(root, dir, name + '.meta'))) fs.unlinkSync(path.join(root, dir, name + '.meta'));
  }
}
for (const [p, content] of outputs) {
  const abs = path.join(root, p);
  if (fs.existsSync(abs) && fs.readFileSync(abs, 'utf8') === content) continue;
  changed++;
  if (apply) { fs.mkdirSync(path.dirname(abs), { recursive: true }); fs.writeFileSync(abs, content); }
}
console.log(JSON.stringify({ bases: bases.length, finalRecipes: recipes.length, optionalAnchors: optionals.length, changed, applied: apply }));
if (check) assert.equal(changed, 0, 'Run generator --apply first');
// Composition owns PNGs and preserves existing real icons and TextureImporter GUIDs.
if (apply || check) execFileSync(process.env.PYTHON || 'python3',
  [path.join(root, 'layout-editor/scripts/compose-salad-sauce-icons.py'), apply ? '--apply' : '--check'],
  { cwd: root, stdio: 'inherit' });
