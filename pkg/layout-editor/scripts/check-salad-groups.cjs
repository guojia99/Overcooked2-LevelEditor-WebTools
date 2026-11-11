#!/usr/bin/env node
// Exercise the production frontend and catalog functions without rebuilding unrelated catalogs.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../..');
const ts = require(path.join(root, 'layout-editor/web/node_modules/typescript'));
const read = p => fs.readFileSync(path.join(root, p), 'utf8');
const transpile = s => ts.transpileModule(s, {
  compilerOptions: {target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS},
}).outputText;
const frontend = {exports: {}};
vm.runInNewContext(transpile(read('layout-editor/web/src/recipeGroups.ts')), frontend);
const buildFile = path.join(__dirname, 'build-catalog.mjs');
let source = fs.readFileSync(buildFile, 'utf8').replace(/^\uFEFF?#![^\n]*\n/, '').replace(/^import .*;\r?\n/gm, '');
source = source.replace('path.dirname(fileURLToPath(import.meta.url))', JSON.stringify(path.dirname(buildFile))).replace(/main\(\);\s*$/, '');
const warnings = [];
const buildContext = {
  fs, path, console: {log: console.log, warn: s => warnings.push(s)},
};
const build = vm.runInNewContext(source + `
  ({deriveSaladSubgroups, scanRecipes, loadKnowledge, buildGuidIndex,
    loadDictionary, loadCommonW1Manifest, parseManualTable, scanIngredients})`, buildContext);
let cases = 0;
for (const [mustard, ketchup] of [
  ['mustard', 'ketchup'], ['DLC08_Mustard', 'DLC08_Ketchup'], ['dlc11_mustard', 'dlc11_ketchup'],
]) {
  for (let mask = 0; mask < 4; mask++) for (const isCustom of [false, true]) {
    const r = {id: 'salad', type: 'salad', group: 'core', isCustom,
      ingredients: ['dlc11_tomato', ...(mask & 1 ? [mustard] : []), ...(mask & 2 ? [ketchup] : [])]};
    const expected = [['no_sauce', 'mustard_only', 'ketchup_only', 'both_sauces'][mask],
      ...(!isCustom ? ['official'] : [])];
    assert.equal(JSON.stringify(frontend.exports.deriveSaladSubgroups(r)), JSON.stringify(expected));
    assert.equal(JSON.stringify(build.deriveSaladSubgroups(r)), JSON.stringify(expected));
    cases++;
    for (const leaves of [['LettuceSO'], ['LettuceSO', 'TomatoSO'], ['LettuceSO', 'TomatoSO', 'CucumberSO']]) {
      const story = {...r, ingredients: [...leaves, ...r.ingredients.slice(1)]};
      assert.equal(JSON.stringify(frontend.exports.deriveSaladSubgroups(story)), '["non_dlc11_official"]');
      assert.equal(JSON.stringify(build.deriveSaladSubgroups(story)), '["non_dlc11_official"]');
      cases++;
    }
    for (const leaves of [[], ['LettuceSO', 'dlc11_tomato'], ['TomatoSO', 'unknown']]) {
      const other = {...r, ingredients: [...leaves, ...r.ingredients.slice(1)]};
      assert.equal(JSON.stringify(frontend.exports.deriveSaladSubgroups(other)), JSON.stringify(expected));
      assert.equal(JSON.stringify(build.deriveSaladSubgroups(other)), JSON.stringify(expected));
      cases++;
    }
  }
}
for (const extra of [
  {type: 'burger'}, {group: 'levelset'}, {group: 'levelset', ingredients: ['LettuceSO']},
  {assetPath: 'Assets/LevelSets/test/custom_recipes/salad/x.asset'},
  {assetPath: 'Assets\\LevelSets\\test\\custom_recipes\\salad\\x.asset'},
]) {
  const r = {type: 'salad', isCustom: false, ...extra};
  assert.equal(JSON.stringify(frontend.exports.deriveSaladSubgroups(r)), '[]');
  assert.equal(JSON.stringify(build.deriveSaladSubgroups(r)), '[]');
}
const official = {id: 'official', type: 'salad', isCustom: false, ingredients: ['mustard']};
const web = {id: 'web', type: 'salad', isCustom: true, group: 'commonw3', ingredients: ['ketchup']};
const grouped = frontend.exports.groupSalads([official, web, {...official}]);
assert.equal(frontend.exports.SALAD_SUBGROUPS.find(g => g.id === 'non_dlc11_official').label, '非DLC11官方沙拉');
assert.equal(grouped.length, 3);
assert.equal(grouped.reduce((n, [, rs]) => n + rs.length, 0), 3);
assert.equal(new Set(grouped.flatMap(([, rs]) => rs.map(r => r.id))).size, 2);
console.log('PASS: ' + cases + ' sauce/provenance cases, local exclusions, cross-group unique ids');

// Extract the actual dialog closures, keeping the test independent of editor startup.
const dialogFile = ts.createSourceFile('recipesDialogs.ts', read('layout-editor/web/src/editor/ui/recipesDialogs.ts'), ts.ScriptTarget.Latest, true);
const closures = {};
function collect(node) {
  if (ts.isVariableDeclaration(node) && ['toggleSelect', 'wireListCards', 'updateGroupCounts',
    'updateSelectAllButtons', 'syncListSelectionUi', 'toggleBulkSelection'].includes(node.name.getText(dialogFile))) {
    closures[node.name.getText(dialogFile)] = node.initializer.getText(dialogFile);
  }
  ts.forEachChild(node, collect);
}
collect(dialogFile);
const selected = new Set(['official-a', 'official-b']);
const selectedIds = new Set(['official']);
const byGuid = new Map([['official-a', official], ['official-b', official], ['web', web]]);
function checkbox(value, id) {
  const classes = new Set();
  const card = {classList: {toggle: (name, on) => on ? classes.add(name) : classes.delete(name)}};
  return {value, dataset: {recipeId: id}, checked: selectedIds.has(id), classes,
    closest: () => card, addEventListener(event, callback) { this.change = callback; }};
}
const checkboxes = [checkbox('official-a', 'official'), checkbox('web', 'web'), checkbox('official-a', 'official')];
const count = {textContent: ''};
const group = {querySelector: () => count, querySelectorAll: () => checkboxes};
const groupBtn = {
  textContent: '', title: '', classes: new Set(),
  classList: {toggle: (name, on) => on ? groupBtn.classes.add(name) : groupBtn.classes.delete(name)},
  closest: selector => selector === '.rw-group' ? group : null,
  addEventListener(event, callback) { if (event === 'click') this.click = callback; },
};
const list = {querySelectorAll: selector => {
  if (selector === '.rw-group') return [group];
  if (selector === '.rw-group-header' || selector === '.rw-cat-select-all') return [];
  if (selector === '.rw-group-select-all') return [groupBtn];
  return checkboxes;
}};
const dialog = {selected, selectedIds, byGuid, document: {getElementById: id => id === 'rw-list' ? list : null}};
vm.runInNewContext(transpile(Object.entries(closures).map(([name, value]) => `const ${name} = ${value};`).join(';') + ';wireListCards(); updateGroupCounts();'), dialog);
assert.equal(count.textContent, '1');
assert.equal(groupBtn.textContent, '全选');
checkboxes[2].checked = false;
checkboxes[2].change();
assert.equal(selected.size, 0);
assert.equal(selectedIds.size, 0);
assert.ok(checkboxes.every(cb => !cb.checked && !cb.classes.has('selected')));
assert.equal(count.textContent, '0');
checkboxes[0].checked = true;
checkboxes[0].change();
assert.ok(checkboxes[2].checked && checkboxes[2].classes.has('selected'));
assert.equal(selected.size, 1);
assert.equal(count.textContent, '1');
assert.equal(groupBtn.textContent, '全选');
checkboxes[1].checked = true;
checkboxes[1].change();
assert.equal(count.textContent, '2');
assert.equal(groupBtn.textContent, '取消全选');
groupBtn.click({stopPropagation() {}});
assert.equal(selected.size, 0);
assert.equal(selectedIds.size, 0);
assert.equal(count.textContent, '0');
assert.equal(groupBtn.textContent, '全选');
assert.ok(checkboxes.every(cb => !cb.checked && !cb.classes.has('selected')));
groupBtn.click({stopPropagation() {}});
assert.equal(selectedIds.size, 2);
assert.equal(count.textContent, '2');
assert.equal(groupBtn.textContent, '取消全选');
assert.ok(checkboxes.every(cb => cb.checked && cb.classes.has('selected')));
console.log('PASS: production dialog closures synchronize duplicate cards, clear duplicate GUIDs, count unique ids and bulk-select groups');

const dictionary = build.loadDictionary();
for (const [id, names] of build.loadCommonW1Manifest().names) dictionary.set(id, names);
const manualPath = path.join(root, '使用手册.md');
const manual = build.parseManualTable(fs.existsSync(manualPath) ? fs.readFileSync(manualPath, 'utf8') : '');
const roots = [
  'Assets/common01/food', 'Assets/common02/food', 'Assets/common03/food/Ingredients',
  'Assets/common03/food/Recipes', 'Assets/common03/food/CookingSteps', 'Assets/common03/food/PlatingSteps',
  'Assets/commonW1/food', 'Assets/commonW1/pseudo_prefab_so/dlc11/food', 'Assets/commonW3/custom_recipes/salad',
];
const guidIndex = build.buildGuidIndex(roots);
const ingredients = build.scanIngredients(dictionary, manual);
for (const ing of ingredients) guidIndex.set(ing.guid, {id: ing.id, assetPath: ing.assetPath});
const {list: recipes} = build.scanRecipes(dictionary, manual, guidIndex, build.loadKnowledge(),
  new Set(ingredients.map(r => r.id)));
const salads = recipes.filter(r => r.type === 'salad');
const manifest = JSON.parse(read('layout-editor/scripts/data/salad-sauce-recipes.json'));
const prefix = 'Web_SauceSalad_Optional_';
const factory = read('Assets/Editor/LayoutEditor/LayoutEditorSaladSauceFactory.cs');
assert.equal(factory.match(/const string Prefix = "([^"]+)"/)[1] + 'Optional_', prefix, 'Factory anchor prefix changed');
assert.equal(manifest.bases.reduce((n, b) => n + b.aliases.length, 0), 4, 'Original alias count');
const expectedGroups = {mustard_only: 23, ketchup_only: 23, both_sauces: 23, no_sauce: 27, official: 6, non_dlc11_official: 12};
const storyIds = ['Salad_Plain_SO', 'Salad_Tomato_SO', 'Salad_Cucumber_SO'].flatMap(id =>
  [id, ...['Ketchup', 'Mustard', 'KetchupMustard'].map(sauce => 'Web_SauceSalad_' + id + '_' + sauce)]).sort();
const ids = rows => Array.from(rows, r => r.id).sort();
function checkSalads(rows, label) {
  assert.equal(rows.length, 108, label + ': selectable salads');
  assert.equal(new Set(ids(rows)).size, 108, label + ': unique salad IDs');
  assert.equal(new Set(rows.map(r => r.guid)).size, 108, label + ': unique salad GUIDs');
  assert.equal(rows.filter(r => r.group === 'commonw3').length, 99, label + ': commonW3');
  assert.equal(rows.filter(r => r.isCustom && !r.id.startsWith('Web_SauceSalad_')).length, 21, label + ': original Web');
  assert.deepEqual(ids(rows.filter(r => r.id.startsWith('Web_SauceSalad_'))), ids(manifest.recipes), label + ': 78 finals');
  assert.ok(rows.every(r => !r.id.startsWith(prefix)), label + ': internal anchor leaked');
  for (const r of rows) {
    assert.equal(JSON.stringify(r.saladSubgroups), JSON.stringify(frontend.exports.deriveSaladSubgroups(r)), label + ': ' + r.id);
    assert.ok(r.ingredients.every(i => !(/^[a-f0-9]{32}$/.test(i))), r.id + ' contains unresolved guid');
  }
  const groups = Object.fromEntries(Object.keys(expectedGroups).map(g => [g, rows.filter(r => r.saladSubgroups.includes(g)).length]));
  assert.deepEqual(groups, expectedGroups, label + ': exact group counts');
  const story = rows.filter(r => r.saladSubgroups.includes('non_dlc11_official'));
  assert.deepEqual(ids(story), storyIds, label + ': exact story members');
  assert.equal(story.filter(r => !r.isCustom).length, 3, label + ': story originals');
  assert.equal(story.filter(r => r.isCustom).length, 9, label + ': story sauce extensions');
  assert.ok(story.every(r => r.saladSubgroups.length === 1), label + ': story group must be exclusive');
  assert.ok(rows.filter(r => r.saladSubgroups.includes('official')).every(r => r.saladSubgroups.includes('no_sauce')), label + ': official/no-sauce overlap');
  for (const base of manifest.bases) for (const alias of base.aliases) {
    assert.ok(rows.some(r => r.id === alias), label + ': original alias removed: ' + alias);
  }
  console.log('PASS: ' + label + ': 108 salads = 99 commonW3 + 9 official; groups:', JSON.stringify(groups));
}
checkSalads(salads, 'production static scan');
assert.equal(manifest.optionals.length, 24);
for (const r of manifest.optionals) {
  assert.ok(r.id.startsWith(prefix));
  assert.ok(guidIndex.has(read('Assets/commonW3/custom_recipes/salad/' + r.id + '.asset.meta').match(/^guid: (\w+)/m)[1]), 'Anchor removed from dependency index');
}
// Run the production scanner with ordinary scores zero and anchors positive: identity, not score, controls exclusion.
const scoreVariant = source.replaceAll('const fields = parseUnityAsset(file);',
  'const fields = parseUnityAsset(file); fields.score = path.basename(file).startsWith("' + prefix + '") ? 100 : 0;');
const variantScan = vm.runInNewContext(scoreVariant + '\nscanRecipes', {...buildContext});
const variant = variantScan(dictionary, manual, guidIndex, build.loadKnowledge(), new Set(ingredients.map(r => r.id))).list;
assert.deepEqual(ids(variant), ids(recipes), 'Filtering must not depend on score');
assert.ok(variant.filter(r => r.isCustom).every(r => r.score === 0));
console.log('PASS: all 24 anchors remain indexed; zero-score recipes remain visible and positive-score anchors stay hidden');

if (process.argv.includes('--live')) (async () => {
  const host = process.env.OC2_UNITY_URL || 'http://10.211.55.3:8765';
  async function get(endpoint) {
    const res = await fetch(host + endpoint, {signal: AbortSignal.timeout(90000)});
    assert.ok(res.ok, endpoint + ': HTTP ' + res.status);
    const data = await res.json();
    assert.ok(Array.isArray(data.recipes), endpoint + ': missing recipes');
    assert.ok(data.recipes.every(r => !r.id.startsWith(prefix)), endpoint + ': internal anchor leaked');
    return data.recipes;
  }
  const normalize = rows => Array.from(rows, r => ({id: r.id, guid: r.guid, group: r.group,
    ingredients: [...r.ingredients].sort(), compositionIds: [...(r.compositionIds || [])].sort(),
    saladSubgroups: [...r.saladSubgroups].sort()})).sort((a, b) => a.id.localeCompare(b.id));
  const online = (await get('/api/recipes')).filter(r => r.type === 'salad');
  checkSalads(online, 'live catalog');
  assert.deepEqual(normalize(online), normalize(salads), 'Live/static scan mismatch');
  for (const output of ['public', 'dist']) {
    const files = ['core', 'dlc11', 'commonw3'].flatMap(group =>
      JSON.parse(read('layout-editor/web/' + output + '/recipes/' + group + '.json')).recipes);
    assert.ok(files.every(r => !r.id.startsWith(prefix)), output + ': internal anchor leaked');
    const published = files.filter(r => r.type === 'salad');
    checkSalads(published, output + ' catalog');
    assert.deepEqual(normalize(published), normalize(salads), output + ': stale catalog');
  }
  const managed = (await get('/api/custom-recipes?libraryId=commonw3')).filter(r => r.category === 'salad');
  const webSalads = salads.filter(r => r.group === 'commonw3');
  assert.deepEqual(ids(managed), ids(webSalads), 'Management/static salad IDs mismatch');
  const managedRows = rows => Array.from(rows, r => ({id: r.id, guid: r.guid,
    ingredients: [...r.ingredients].sort(), compositionIds: [...r.compositionIds].sort()})).sort((a, b) => a.id.localeCompare(b.id));
  assert.deepEqual(managedRows(managed), managedRows(webSalads), 'Management/static GUID or composition mismatch');
  console.log('PASS: live catalog, management list and public/dist catalogs agree; no internal anchors exposed');
})().catch(error => { console.error(error); process.exitCode = 1; });
