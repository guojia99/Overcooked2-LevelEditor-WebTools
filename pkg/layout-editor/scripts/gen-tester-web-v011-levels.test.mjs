import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { LEVELS, buildLayout, validateLayout, renderSvg } from './lib/tester-web-v011-layouts.mjs';

const root = new URL('../web/public/', import.meta.url);
const json = path => JSON.parse(readFileSync(path, 'utf8'));
const catalog = json(new URL('catalog.json', root));
const stock = catalog.itemChunks.flatMap(path => json(new URL(path, root)).items);
const history = json(new URL('../../writeback_history/tester_web/testyimian/20261104_114148_layout/doc_after.json', import.meta.url));
const native = json(new URL('../../writeback_history/qa_native_modes_20261118/qa_horde/20261118_132517_layout/doc_after.json', import.meta.url));
const ingredientIds = ['TeaSO', 'milk', 'HoneycombSO', 'SeaweedSO', 'SushiRiceSO', 'SushiFishSO', 'SushiPrawnSO', 'CucumberSO', 'FishSO', 'FlourSO', 'PrawnSO', 'PastaSO', 'TortillaSO', 'RiceSO', 'BurritoMeatSO', 'BurritoChickenSO', 'MushroomSO', 'EggSO', 'hotdogbun', 'LettuceSO', 'TomatoSO', 'SpiceSO', 'SpringOnionSO', 'dlc11_ketchup'];
const ingredientGuids = new Map(history.items.filter(i => i.dispenser?.spawnerItemPrefabGuid).map(i => {
  const known = { '7ac8ffa91f182f14c8d14bfe5badd2ac': 'PastaSO', 'bbf0e92641aa4ab49bda6bc68117e01f': 'SushiFishSO', 'df4a04d1549f9ee41a991153bd723110': 'SushiPrawnSO' };
  return [known[i.dispenser.spawnerItemPrefabGuid], i.dispenser.spawnerItemPrefabGuid];
}));
const fixture = {
  stock,
  ingredients: ingredientIds.map((id, i) => ({ id, guid: ingredientGuids.get(id) || `ingredient_${i}`, assetPath: `Assets/fixtures/${id}.asset` })),
  recipes: [...new Set([...LEVELS.flatMap(l => l.recipeIds), 'Web_MixedFlourEgg', 'Web_Seafood_Pasta_FishNode', 'Web_Seafood_Pasta_PrawnNode'])].map(id => ({
    id, guid: id === 'Web_Seafood_Pasta_FishNode' ? 'e77083065ab1c8b8af7e65ff0b465127' : id === 'Web_Seafood_Pasta_PrawnNode' ? '160acf21e07d8a27f7f8d130e219e1ff' : `recipe_${id}`, assetPath: `Assets/fixtures/${id}.asset`,
    cookingGroups: id.startsWith('Web_Burrito') ? [{ step: 'Pot', ingredients: ['RiceSO'] }, { step: 'FryingPan', ingredients: ['BurritoMeatSO', 'BurritoChickenSO', 'MushroomSO'] }] : [],
  })),
  materials: [{ id: 'Floor', guid: 'floor_material', assetPath: 'Assets/commonW1/materials/Floor.mat' }],
};
const current = level => ({ ...structuredClone(native), sceneAssetPath: `Assets/LevelSets/tester_web/scenes/${level.id}.unity`,
  cameraInfo: { fieldOfView: 49, arbitraryManagerField: 'keep' }, lights: [{ name: 'keep-light' }],
  items: [...Array.from({ length: 4 }, (_, i) => ({ ...structuredClone(native.items.find(it => it.stubKind === 'Player')), instanceId: `u:player${i}`, hierarchyPath: `Design/Chefs/Player${i}` })),
    ...(level.mode === 'Horde' ? native.items.filter(i => ['Service_Window', 'gate'].some(id => stock.find(c => c.id === id).guid === i.prefabGuid)) : [])],
});
const item = (doc, role) => doc.items.find(i => i.displayName === `V011__${role}`);

for (const level of LEVELS) test(`${level.id}: deterministic, pure, connected, accessible geometry`, () => {
  const ctx = structuredClone(fixture), before = structuredClone(ctx), old = current(level), saved = structuredClone(old);
  const a = buildLayout(level.id, ctx, old), b = buildLayout(level.id, ctx, old);
  assert.deepEqual(a, b); assert.deepEqual(ctx, before); assert.deepEqual(old, saved);
  assert.deepEqual(a.cameraInfo, old.cameraInfo); assert.deepEqual(a.lights, old.lights);
  assert.equal(validateLayout(a).allSpawnsConnected, true);
  assert.equal(a.items.filter(i => i.stubKind === 'Player').length, 4);
  assert.ok(a.items.filter(i => i.stubKind !== 'Player').every(i => level.mode === 'Horde' && ['Lockable', 'ServingStation'].includes(i.stubKind) || i.instanceId.startsWith('new:')));
  assert.equal(a.items.filter(i => i.prefabGuid === stock.find(c => c.id === 'ChoppingCounter').guid).length >= 2, true);
  assert.ok(item(a, level.mode === 'Horde' ? 'plates' : level.id === 'v011_tea' ? 'web_teacup_stack_clean' : 'plates'));
  assert.deepEqual(buildLayout(level.id, ctx, a), a, 'authoring document can be regenerated');
  const svg = renderSvg(a, '<unsafe & title>');
  assert.match(svg, /&lt;unsafe &amp; title&gt;/); assert.doesNotMatch(svg, /<unsafe/);
});

test('pasta sparse lookup exactly matches the specified local history', () => {
  const doc = buildLayout('v011_pasta_burrito', fixture, current(LEVELS[2]));
  const expected = history.items.find(i => i.stubKind === 'CookingUtensil' && i.displayName === 'FryPan').cookingUtensil;
  for (const field of ['allowedIngredientGuids', 'modelGuids', 'modelSoGuids', 'capacity']) assert.deepEqual(item(doc, 'pasta_pan').cookingUtensil[field], expected[field]);
});

test('tea shared switch, stack counts, wash/return and distinct timings', () => {
  const doc = buildLayout('v011_tea', fixture, current(LEVELS[0]));
  assert.deepEqual(doc.switchLinks.map(l => l.switchId), [item(doc, 'switch').instanceId, item(doc, 'switch').instanceId]);
  assert.deepEqual([item(doc, 'kettle_1').kettle.heatSeconds, item(doc, 'kettle_2').kettle.heatSeconds], [5, 8]);
  assert.equal(item(doc, 'return').plateReturn.returnClean, false);
  assert.equal(item(doc, 'web_teacup_stack_dirty').cleanPlateStack.plateCount, 6);
  assert.equal(item(doc, 'sink').worldPosition.x, 8.5 * 1.2, 'two-cell sink has half-cell pivot');
});

test('Horde preserves native window identity, transforms, UI offsets and gate direction', () => {
  const old = current(LEVELS[4]), doc = buildLayout('v011_horde', fixture, old);
  const windows = doc.items.filter(i => i.stubKind === 'ServingStation');
  assert.equal(windows.length, 3);
  for (const w of windows) {
    const original = old.items.find(i => i.instanceId === w.instanceId);
    assert.deepEqual(w.worldPosition, original.worldPosition); assert.deepEqual(w.localPosition, original.localPosition);
    assert.equal(w.localRotationY, original.localRotationY); assert.deepEqual(w.servingStation.orderUIOffset, original.servingStation.orderUIOffset);
  }
  assert.equal(item(doc, 'gate').lockable.unlockCost, 30);
  assert.equal(item(doc, 'gate').instanceId, old.items.find(i => i.stubKind === 'Lockable').instanceId);
  assert.equal(item(doc, 'gate').colliderCenter.x, 0.6);
  assert.ok(item(doc, 'backroom_steamer'));
});

test('mechanism hold door, floor plate and northbound conveyor chain', () => {
  const doc = buildLayout('v011_mechanism', fixture, current(LEVELS[3]));
  assert.equal(item(doc, 'pressure').walkable, false);
  assert.equal(doc.animControls.groups[0].advanceMode, 'hold');
  assert.deepEqual(doc.animControls.groups[0].events.map(e => e.rotateDirection), ['ccw', 'cw']);
  assert.equal(doc.buttonLinks.links[0].sourceId, item(doc, 'pressure').instanceId);
  assert.equal(item(doc, 'conveyor_serve').conveyor.serve, true);
  assert.equal(item(doc, 'condiment').dispenser.spawnerItemPrefabGuid, fixture.ingredients.find(i => i.id === 'dlc11_ketchup').guid);
  assert.deepEqual(item(doc, 'condiment').soArray.pseudoPrefabGuids, [item(doc, 'condiment').dispenser.spawnerItemPrefabGuid]);
  assert.ok(doc.switchLinks.some(l => l.switchId === item(doc, 'condiment_button').instanceId && l.targetId === item(doc, 'condiment').instanceId && l.trigger === 'Next'));
  assert.equal(item(doc, 'pancake_pan').cookingUtensil.allowedIngredientGuids[0], fixture.recipes.find(r => r.id === 'Web_MixedFlourEgg').guid);
});

test('geometry rejects overlapping stations, blocked spawns, stranded stations and broken links', () => {
  const build = () => buildLayout('v011_tea', fixture, current(LEVELS[0]));
  let doc = build(); item(doc, 'kettle_2').worldPosition = structuredClone(item(doc, 'kettle_1').worldPosition);
  assert.throws(() => validateLayout(doc), /Overlap/);
  doc = build(); item(doc, 'player_1').worldPosition = structuredClone(item(doc, 'kettle_1').worldPosition);
  assert.throws(() => validateLayout(doc), /Blocked chef/);
  doc = build(); item(doc, 'kettle_1').worldPosition.x = 100;
  assert.throws(() => validateLayout(doc), /Outside floor/);
  doc = build(); doc.switchLinks[0].targetId = 'missing';
  assert.throws(() => validateLayout(doc), /Dangling switch/);
  doc = build(); item(doc, 'serve').servingStation.plateReturnInstanceIds = ['missing'];
  assert.throws(() => validateLayout(doc), /Dangling serving/);
});

test('requires exactly four reusable players and refuses a different target scene', () => {
  const old = current(LEVELS[0]); old.items.pop();
  assert.throws(() => buildLayout('v011_tea', fixture, old), /four|4 reusable/);
  assert.throws(() => buildLayout('v011_tea', fixture, current(LEVELS[1])), /scene mismatch/);
});

test('read-only live catalogs resolve all five layouts', { skip: !process.env.V011_CATALOG_URL }, async () => {
  const base = new URL(process.env.V011_CATALOG_URL);
  const get = async path => {
    const response = await fetch(new URL(path, base), { signal: AbortSignal.timeout(15000) });
    assert.equal(response.ok, true, path);
    return response.json();
  };
  const index = await get('/catalog.json');
  const ctx = { stock: [...(index.items || [])] };
  for (const path of index.itemChunks || []) ctx.stock.push(...(await get('/' + path)).items);
  ctx.ingredients = (await get('/api/catalog/ingredients?levelSet=tester_web')).ingredients;
  ctx.recipes = (await get('/api/recipes?levelSet=tester_web')).recipes;
  ctx.materials = (await get('/api/catalog/floor-materials?levelSet=tester_web')).materials;
  for (const level of LEVELS) assert.equal(validateLayout(buildLayout(level.id, ctx, current(level))).allSpawnsConnected, true);
});

test('flood fill rejects a functional station isolated from all chef spawns', () => {
  const doc = buildLayout('v011_tea', fixture, current(LEVELS[0]));
  const original = item(doc, 'kettle_1');
  doc.floors.push({ ...structuredClone(doc.floors[0]), instanceId: 'new:isolated_floor', displayName: 'V011__isolated_floor',
    worldPosition: { x: 30, y: -0.05, z: 0 }, widthUnits: 3.6, depthUnits: 3.6 });
  original.worldPosition.x = 30;
  assert.throws(() => validateLayout(doc), /Inaccessible station/);
});
