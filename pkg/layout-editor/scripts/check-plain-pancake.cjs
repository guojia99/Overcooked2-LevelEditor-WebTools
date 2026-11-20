#!/usr/bin/env node
// Read-only regression: exercise production scanners and TS functions without regenerating catalogs/dist.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../..');
const ts = require(path.join(root, 'layout-editor/web/node_modules/typescript'));
const read = p => fs.readFileSync(path.join(root, p), 'utf8').replace(/^\uFEFF/, '');
const plainJson = value => JSON.parse(JSON.stringify(value));
const md5 = value => crypto.createHash('md5').update(value).digest('hex');
const id = 'Web_MixedFlourEgg';
const assetPath = 'Assets/commonW5/custom_recipes/pancake/' + id + '.asset';
const guid = md5('commonw5-recipe:' + id);
const asset = read(assetPath);
const native = read('Assets/common03/food/Recipes_Patch/MixedFlourEgg.asset');
assert.match(asset, /m_Script: \{fileID: 11500000, guid: 753d9e70603f6a140b05f30f176ec2dd, type: 3\}/);
for (const field of ['prefabName', 'bundleName', 'assetPath']) {
  const value = text => text.match(new RegExp('^  ' + field + ': (.+)$', 'm'))[1].toLowerCase();
  assert.equal(value(asset), value(native), 'Native wrapper mismatch: ' + field);
}
assert.match(asset, /^  score: 0$/m);
assert.match(read(assetPath + '.meta'), new RegExp('^guid: ' + guid + '$', 'm'));
assert.match(read('Assets/commonW5/custom_recipes/pancake.meta'),
  new RegExp('^guid: ' + md5('commonw5-folder:Assets/commonW5/custom_recipes/pancake') + '$', 'm'));
const metadata = JSON.parse(read('layout-editor/scripts/data/recipe-knowledge.json')).recipes.filter(r => r.id === id);
assert.equal(metadata.length, 1, 'Unique knowledge id');
assert.deepEqual(metadata[0], {id, step: 'Mixer', ingredients: ['FlourSO', 'EggSO']});
const names = JSON.parse(read('Assets/commonW5/custom_recipes/names.json')).names.filter(r => r.id === id);
assert.deepEqual(names, [{id, zh: '原味松饼面糊', en: 'Plain Pancake Batter'}]);

const loadTs = (file, requireModule = name => { throw new Error('Unexpected import: ' + name); }, globals = {}) => {
  const context = {exports: {}, require: requireModule, ...globals};
  const js = ts.transpileModule(read(file), {
    compilerOptions: {target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS},
  }).outputText;
  vm.runInNewContext(js, context, {filename: file});
  return context.exports;
};
const groups = loadTs('layout-editor/web/src/recipeGroups.ts');
const labels = loadTs('layout-editor/web/src/ingredientLabels.ts');

// Load the real static scanner, omitting only its output-writing entrypoint.
const buildFile = path.join(__dirname, 'build-catalog.mjs');
const source = read('layout-editor/scripts/build-catalog.mjs')
  .replace(/^import .*;\r?\n/gm, '')
  .replace('path.dirname(fileURLToPath(import.meta.url))', JSON.stringify(__dirname))
  .replace(/main\(\);\s*$/, '');
const build = vm.runInNewContext(source + '\n({scanRecipes, loadKnowledge, buildGuidIndex, loadDictionary, parseManualTable})',
  {fs, path, process: {argv: []}, console: {log() {}, warn() {}}}, {filename: buildFile});
const guidIndex = build.buildGuidIndex([
  'Assets/common01/food', 'Assets/common02/food', 'Assets/common03/food',
  'Assets/commonW3/custom_recipes/salad', 'Assets/commonW5/custom_recipes/pancake',
]);
assert.equal(guidIndex.get(guid)?.id, id);
const knowledge = build.loadKnowledge();
const scan = build.scanRecipes(build.loadDictionary(), build.parseManualTable(''),
  guidIndex, knowledge, new Set(Object.values(knowledge.recipes).flatMap(r => r.ingredients ?? [])));
const recipes = plainJson(scan.list);
const batter = recipes.find(r => r.id === id);
assert.ok(batter, 'Narrow static scan root must discover typed wrapper');
assert.equal(recipes.filter(r => r.id === id).length, 1);
assert.equal(batter.guid, guid);
assert.equal(batter.assetPath, assetPath);
assert.equal(batter.group, 'commonw5');
assert.equal(batter.cookingStep, 'Mixer');
assert.equal(batter.score, 0);
assert.equal(batter.intermediate, true);
assert.equal(batter.isCustom, false);
assert.equal(batter.nameZh, '原味松饼面糊');
assert.equal(batter.nameEn, 'Plain Pancake Batter');
assert.deepEqual(batter.ingredients, ['FlourSO', 'EggSO']);

// The checked-in offline fallback must contain the exact production-scanner node.
const staticIndex = JSON.parse(read('layout-editor/web/public/recipes.json'));
assert.equal(staticIndex.groups.filter(group => group === 'commonw5').length, 1);
assert.equal(staticIndex.groupFiles.commonw5, 'recipes/commonw5.json');
assert.deepEqual(staticIndex.groups, Object.keys(staticIndex.groupFiles));
const staticGroups = Object.fromEntries(Object.entries(staticIndex.groupFiles).map(([group, file]) => {
  const data = JSON.parse(read('layout-editor/web/public/' + file));
  assert.equal(data.schemaVersion, staticIndex.schemaVersion, 'Static group schema: ' + group);
  assert.equal(data.group, group, 'Static group name: ' + group);
  assert.equal(data.itemCount, data.recipes.length, 'Static group count: ' + group);
  return [file, data];
}));
const staticBatterGroup = staticGroups[staticIndex.groupFiles.commonw5];
assert.equal(staticBatterGroup.itemCount, 1, 'Plain pancake fallback adds exactly one node');
assert.deepEqual(staticBatterGroup.recipes, [batter], 'Checked-in fallback must match production scanner');
assert.equal(staticIndex.itemCount, Object.values(staticGroups).reduce((sum, group) => sum + group.itemCount, 0),
  'Static index count must include every group');
assert.equal(Object.values(staticGroups).flatMap(group => group.recipes).filter(r => r.id === id).length, 1,
  'Static fallback must contain exactly one plain pancake batter');
const S = {intermediatesCache: recipes.filter(r => r.intermediate), ingredientsCache: [], items: [], catalogByGuid: new Map()};
const rk = loadTs('layout-editor/web/src/editor/recipeKnowledge.ts', name => {
  if (name === './state') return {S};
  if (name === './itemVariants') return {VARIANT_TO_BASE: {dlc05_utensil_frying_pan: 'FryPan'}};
  if (name === '../recipeGroups') return groups;
  if (name === '../autoScoreKnowledge') return {CHOP_INGREDIENT_PATTERN: /chopped/, CHOP_WHOLE_IDS: new Set()};
  if (name === '../ingredientLabels') return labels;
  if (name === '../api') return {};
  throw new Error('Unexpected recipeKnowledge import: ' + name);
});
assert.ok(rk.utensilIntermediateRecipes(recipes).some(r => r.id === id));
const get = name => {
  const r = recipes.find(r => r.id === name);
  assert.ok(r, 'Missing production recipe: ' + name);
  return r;
};
const plain = get('Cake_Plain_SO');
const chocolate = get('Cake_Chocolate_SO');
const strawberry = get('Cake_StrawberryPancake');
const blueberry = get('Cake_BlueberryPancake');
const expectedPan = {ings: [], intermediates: [id], capacity: 1, clear: false};
const expectedMixer = {ings: ['FlourSO', 'EggSO'], intermediates: [], capacity: 2, clear: false};
assert.equal(rk.findBatterIntermediateForRecipe(plain)?.id, id);
assert.equal(rk.recipeLacksIntermediate(plain), false);
let fill = rk.computeUtensilIngredientFill([plain]);
assert.deepEqual(plainJson(fill.get('FryPan')), expectedPan);
assert.deepEqual(plainJson(fill.get('MixerBowl')), expectedMixer);
let supplement = rk.computeIntermediatesForUtensils([plain]);
assert.deepEqual(plainJson(supplement.get('FryPan')), [id]);
assert.deepEqual(plainJson(supplement.get('MixerBowl')), ['FlourSO', 'EggSO']);
assert.notEqual(rk.findBatterIntermediateForRecipe(chocolate)?.id, id);
assert.ok(rk.findBatterIntermediateForRecipe(chocolate)?.ingredients.includes('ChocolateSO'));
assert.equal(rk.findBatterIntermediateForRecipe(strawberry)?.id, 'MixedFlourEggStrawberry');
assert.equal(rk.findBatterIntermediateForRecipe(blueberry)?.id, 'MixedFlourEggBlueberry');
for (const r of recipes.filter(r => !r.intermediate && r.id !== plain.id && !(r.compositionIds ?? []).includes(id))) {
  assert.notEqual(rk.findBatterIntermediateForRecipe(r)?.id, id, 'New batter leaked into production recipe: ' + r.id);
}
for (const extra of [
  {id: 'UserFlourEgg'}, {id: 'UserFlourEgg', ingredients: ['FlourSO', 'EggSO', 'ChocolateSO']},
  {id: 'Cake_Plain_SO', isCustom: true}, {id: 'Cake_Plain_SO', cookingStep: 'OvenCakeTin'},
  {id: 'Cake_Plain_SO', ingredients: ['FlourSO', 'EggSO', 'ChocolateSO']},
  {id: 'Cake_Plain_SO', ingredients: ['FlourSO']},
  {id: 'OtherFlourEgg', ingredients: ['dlc13_flour', 'dlc13_egg']},
]) {
  const r = {...plain, ...extra};
  assert.notEqual(rk.findBatterIntermediateForRecipe(r)?.id, id, 'Implicit match escaped original recipe guard');
  assert.ok(!JSON.stringify([...rk.computeIntermediatesForUtensils([r])]).includes(id));
}
const explicit = {...plain, id: 'ExplicitCustom', isCustom: true, compositionIds: [id]};
assert.equal(rk.findBatterIntermediateForRecipe(explicit)?.id, id);
assert.deepEqual(plainJson(rk.computeUtensilIngredientFill([explicit]).get('FryPan')), expectedPan);
assert.deepEqual(plainJson(rk.computeIntermediatesForUtensils([explicit]).get('FryPan')), [id]);
const explicitNode = {...explicit, ingredients: [id]};
assert.deepEqual(plainJson(rk.computeUtensilIngredientFill([explicitNode]).get('FryPan')), expectedPan);
assert.deepEqual(plainJson(rk.computeIntermediatesForUtensils([explicitNode]).get('FryPan')), [id]);
const withChocolate = rk.computeUtensilIngredientFill([plain, chocolate]);
assert.equal(withChocolate.get('FryPan').clear, false);
assert.equal(withChocolate.get('FryPan').capacity, 1);
assert.deepEqual(plainJson(withChocolate.get('FryPan').intermediates),
  [id, rk.findBatterIntermediateForRecipe(chocolate).id]);

// Multiple pan recipes share the pan, while capacity counts one portion, not the union of all leaves.
const meat = {...plain, id: 'PanMeat', type: 'burger', ingredients: ['MeatSO'],
  cookingGroups: [{step: 'FryingPan', ingredients: ['MeatSO']}]};
const compatible = [plain, strawberry, blueberry, meat];
const inputsBeforeFill = JSON.stringify({compatible, intermediates: S.intermediatesCache});
fill = rk.computeUtensilIngredientFill(compatible);
assert.deepEqual(plainJson(fill.get('FryPan')), {
  ings: ['MeatSO'], intermediates: [id, 'MixedFlourEggStrawberry', 'MixedFlourEggBlueberry'], capacity: 1, clear: false,
});
assert.equal(fill.get('MixerBowl').capacity, 3);
assert.deepEqual(new Set(fill.get('MixerBowl').ings), new Set(['FlourSO', 'EggSO', 'PancakeStrawberry', 'Blueberry']));
assert.equal(JSON.stringify({compatible, intermediates: S.intermediatesCache}), inputsBeforeFill, 'Fill must not mutate catalog inputs');
// A catalog without a chocolate batter must still clear the shared pan.
const fullCache = S.intermediatesCache;
S.intermediatesCache = fullCache.filter(r => !(r.ingredients ?? []).includes('ChocolateSO'));
assert.equal(rk.findBatterIntermediateForRecipe(chocolate), undefined);
assert.equal(rk.recipeLacksIntermediate(chocolate), true);
for (const selection of [[plain, chocolate], [chocolate, plain, strawberry, meat]]) {
  const f = rk.computeUtensilIngredientFill(selection);
  assert.equal(f.get('FryPan').clear, true);
  assert.equal(f.get('FryPan').capacity, 1);
  assert.equal(rk.computeIntermediatesForUtensils(selection).has('FryPan'), false,
    'Supplement must not undo the native lookup clear instruction');
}
const cache = S.intermediatesCache;
S.intermediatesCache = cache.filter(r => r.id !== id);
assert.equal(rk.recipeLacksIntermediate(plain), true);
assert.equal(rk.computeUtensilIngredientFill([plain, strawberry]).get('FryPan').clear, true);
assert.equal(rk.computeIntermediatesForUtensils([plain, strawberry]).has('FryPan'), false);
S.intermediatesCache = fullCache;

// Run the actual application helper against two pan skins, a mixer, and an unrelated part.
const manager = loadTs('layout-editor/web/src/editor/ui/utensilManager.ts', name => {
  if (name === '../state') return {S};
  if (name === '../recipeKnowledge') return rk;
  if (name === '../coords') return {prefabIdFromPath: p => path.basename(p ?? '', '.prefab')};
  if (name === '../partLevel') return {itemInCurrentPart: it => !it.otherPart};
  if (name === '../stubControls') return {stubKindOf: it => it.stubKind};
  if (name === '../../utensilRecipeFilters') return {emptyUtensilRecipeFilters: () => ({})};
  return {};
});
S.items = ['FryPan', 'dlc05_utensil_frying_pan', 'MixerBowl', 'Pot'].map(name => ({
  prefabAssetPath: name + '.prefab', stubKind: 'CookingUtensil', cookingUtensil: {allowedIngredientGuids: ['stale'], capacity: 9},
}));
S.items.push({...S.items[0], otherPart: true, cookingUtensil: {allowedIngredientGuids: ['other-part'], capacity: 9}});
const ingGuids = new Map(['FlourSO', 'EggSO', 'PancakeStrawberry', 'Blueberry', 'MeatSO'].map(i => [i, 'ing-' + i]));
const recipeGuids = new Map(recipes.map(r => [r.id, r.guid]));
const apply = selection => manager.applyUtensilIngredientFill(rk.computeUtensilIngredientFill(selection), ingGuids, recipeGuids);
assert.equal(apply([plain]), 3);
assert.deepEqual(plainJson(S.items[0].cookingUtensil), {allowedIngredientGuids: [guid], capacity: 1});
assert.deepEqual(plainJson(S.items[1].cookingUtensil), plainJson(S.items[0].cookingUtensil));
assert.deepEqual(plainJson(S.items[2].cookingUtensil), {allowedIngredientGuids: ['ing-FlourSO', 'ing-EggSO'], capacity: 2});
const snapshot = JSON.stringify(S.items);
apply([plain]);
assert.equal(JSON.stringify(S.items), snapshot, 'Applying the same fill twice must be idempotent');
S.intermediatesCache = cache;
apply([plain, chocolate]);
assert.deepEqual(plainJson(S.items[0].cookingUtensil), {allowedIngredientGuids: [], capacity: 1});
assert.deepEqual(plainJson(S.items[3].cookingUtensil), {allowedIngredientGuids: ['stale'], capacity: 9});
assert.deepEqual(plainJson(S.items[4].cookingUtensil), {allowedIngredientGuids: ['other-part'], capacity: 9});
S.intermediatesCache = fullCache;

async function checkNames() {
  for (const dynamic of [true, false]) {
    const api = loadTs('layout-editor/web/src/api.ts', undefined, {
      URLSearchParams, console: {debug() {}},
      fetch: async url => {
        if (url.startsWith('/api/recipes?')) {
          if (!dynamic) throw new Error('Offline');
          return {ok: true, headers: {get: () => 'application/json'},
            text: async () => JSON.stringify({recipes: [{...batter, nameEn: id}, {...chocolate, nameEn: 'Unchanged'}]})};
        }
        if (url === '/recipes.json') return {ok: true, json: async () => plainJson(staticIndex)};
        const group = staticGroups[url.slice(1)];
        if (group) return {ok: true, json: async () => plainJson(group)};
        throw new Error('Unexpected URL: ' + url);
      },
    });
    const result = await api.fetchRecipeCatalog('regression');
    const namedBatter = result.find(r => r.id === id);
    assert.ok(namedBatter, 'Dynamic and actual offline catalogs must contain plain pancake batter');
    assert.equal(namedBatter.nameZh, '原味松饼面糊');
    assert.equal(namedBatter.nameEn, 'Plain Pancake Batter');
    if (!dynamic) assert.deepEqual(plainJson(namedBatter), batter);
    if (dynamic) assert.equal(result[1].nameEn, 'Unchanged');
  }
}
checkNames().then(() => console.log('PASS: plain pancake asset/GUID, production static scan, checked-in fallback parity/index/counts, dynamic/offline names, guarded batter matching, multi-recipe fill/clear, variants and idempotent apply'))
  .catch(error => { console.error(error); process.exitCode = 1; });
