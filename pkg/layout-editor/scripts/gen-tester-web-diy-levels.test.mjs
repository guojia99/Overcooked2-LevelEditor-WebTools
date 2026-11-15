import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { createServer } from 'node:http';
import { once } from 'node:events';
import { argumentsOf, apiClient, buildPlans, LEVELS, reconcile, run, serializedGuids, validateGeometry, checkConnectivity, validateHandoff } from './gen-tester-web-diy-levels.mjs';

const root = new URL('../../', import.meta.url);
const json = async (path) => JSON.parse(await readFile(new URL(path, root), 'utf8'));
const guid = (id) => createHash('md5').update(`test-fixture:${id}`).digest('hex');
const SET = 'tester_web_diy';
const ROOT = `Assets/LevelSets/${SET}`;
const field = (name, values) => `  ${name}:\n${values.map((g) => `  - {fileID: 11400000, guid: ${g}, type: 2}`).join('\n')}\n`;

async function fixture(custom = true) {
  const index = await json('layout-editor/web/public/catalog.json');
  const stock = (await Promise.all(index.itemChunks.map((file) => json(`layout-editor/web/public/${file}`)))).flatMap((c) => c.items);
  const recipes = (await Promise.all(['core', 'custom'].map((group) => json(`layout-editor/web/public/recipes/${group}.json`)))).flatMap((c) => c.recipes);
  const ingredients = (await json('layout-editor/web/public/ingredients.json')).ingredients;
  const materials = (await json('layout-editor/web/public/floor-materials.json')).materials;
  const cookingSteps = (await json('layout-editor/web/public/cooking-steps.json')).cookingSteps;
  const files = new Map();
  for (const recipe of recipes) files.set(recipe.assetPath, await readFile(new URL(recipe.assetPath, root), 'utf8'));
  const content = (id, kind, templateId, extra = {}) => ({ id, kind, templateId, guid: guid(`prefab:${id}`),
    assetPath: `${ROOT}/custom_content/${kind}/${id}/${id}.prefab`, warnings: [], ...extra });
  const items = custom ? [
    content('arbitrary_red', 'ingredient', 'ingredient_workable'),
    content('arbitrary_cut', 'ingredient', 'ingredient_unworkable'),
    content('arbitrary_counter', 'counter', 'counter_chopping'),
    content('arbitrary_pot', 'utensil', 'utensil_pot', { capacity: 4, stationTypes: ['Hob'], allowedIngredientGuids: [], modelGuids: [] }),
    content('arbitrary_sink', 'sink', 'sink'),
    content('arbitrary_tray', 'tray', 'tray'),
    content('arbitrary_prep', 'prep', 'prep'),
    content('arbitrary_plate', 'utensil', 'utensil_plate'),
    content('arbitrary_splitter', 'splitter', 'splitter', { inputRecipeGuid: guid('recipe:batch'), outputPrefabGuid: guid('so:arbitrary_cut'), splitNum: 2 }),
  ] : [];
  const soRefs = items.map((i) => ({ id: `${i.id}_SO`, guid: guid(`so:${i.id}`), assetPath: i.assetPath.replace('.prefab', '_SO.asset') }));
  ingredients.push(...soRefs.filter((r) => r.assetPath.includes('/ingredient/')).map((r) => ({ ...r, id: r.id.replace(/_SO$/, '') })));
  const pot = cookingSteps.find((s) => s.id === 'Pot');
  const customRecipe = (id, type, comps, step = '', score = 60) => {
    const r = { id: `TWDIY_${id}`, guid: guid(`recipe:${id}`), type, score, intermediate: score === 0,
      assetPath: `${ROOT}/custom_recipes/tests/${id}.asset`, platingStepId: 'Plate', cookingStepId: step };
    files.set(r.assetPath, field('compositionSOs', comps) + (step ? `  cookingStepSO: {fileID: 11400000, guid: ${pot.guid}, type: 2}\n` : ''));
    return r;
  };
  const customRecipes = custom ? [
    customRecipe('raw', 'Ingredient', [guid('so:arbitrary_cut')]),
    customRecipe('cooked', 'Cooked', [guid('so:arbitrary_cut'), guid('so:arbitrary_cut')], 'Pot', 0),
    customRecipe('plate_cooked', 'Composite', [guid('recipe:cooked')]),
    customRecipe('prep', 'Ingredient', [guid('so:arbitrary_prep')]),
    customRecipe('item', 'Item', [guid('so:arbitrary_plate')]),
    customRecipe('batch', 'Composite', [guid('so:arbitrary_cut'), guid('so:arbitrary_cut')], '', 0),
  ] : [];
  recipes.push(...customRecipes.map((r) => ({ ...r, cookingStep: r.cookingStepId, platingStep: r.platingStepId })));
  const customCatalog = items.filter((i) => !['ingredient', 'prep'].includes(i.kind)).map((i) => ({ ...i,
    customKind: i.kind, customStubKind: i.stationTypes ? 'CookingUtensil' : '',
    defaultParent: ['utensil', 'tray'].includes(i.kind) ? 'Design/Utensils' : 'Design/Counters',
    footprint: { cellsX: i.kind === 'sink' ? 2 : 1, cellsZ: 1 } }));
  const protectedLevel = { assetPath: `${ROOT}/data/test_diy_mm1/LevelInfo_test_diy_mm1.asset`, sceneName: 'test_diy_mm1', sceneAssetPath: `${ROOT}/scenes/test_diy_mm1.unity`, hasScene: true };
  const ctx = { health: { ok: true }, levels: [protectedLevel], stock, recipes, customRecipes, items, customCatalog, ingredients, materials,
    refs: { cookingSteps }, options: { soRefs, prefabs: [] }, raw: async (path) => {
      assert.ok(files.has(path), `Unexpected raw file ${path}`);
      return files.get(path);
    } };
  return { ctx, files, index, soRefs };
}

function bridge(data) {
  const { ctx, files, index, soRefs } = data;
  const state = new Map();
  const calls = [];
  for (const l of ctx.levels) { files.set(l.assetPath, 'untouched-original-info'); files.set(l.sceneAssetPath, 'untouched-original-scene'); }
  const api = async (url, body, text) => {
    const u = new URL(url, 'http://fixture');
    const path = u.pathname, param = (key) => u.searchParams.get(key);
    calls.push({ path, body: structuredClone(body) });
    const saved = [...state.values()].find((s) => s.detail.levelInfoAssetPath === (body?.assetPath || body?.levelInfoAssetPath || param('assetPath')) || s.layout.sceneAssetPath === (param('assetPath') || body?.sceneAssetPath));
    if (path === '/api/health') return { ok: true };
    if (path === `/api/sets/${SET}/levels`) return { levels: structuredClone(ctx.levels) };
    if (path === '/catalog.json') return index;
    if (path.startsWith('/catalog/')) return json(`layout-editor/web/public${path}`);
    if (path === '/api/custom-content/list') return { items: ctx.items };
    if (path === '/api/custom-content/catalog') return { items: ctx.customCatalog };
    if (path === '/api/custom-content/options') return { soRefs, prefabs: [] };
    if (path === '/api/custom-recipes') return { recipes: ctx.customRecipes };
    if (path === '/api/custom-recipes/references') return ctx.refs;
    if (path === '/api/recipes') return { recipes: ctx.recipes };
    if (path === '/api/catalog/ingredients') return { ingredients: structuredClone(ctx.ingredients) };
    if (path === '/api/catalog/floor-materials') return { materials: ctx.materials };
    if (path === '/api/level/data-file') {
      assert.equal(text, true);
      assert.ok(files.has(param('path')), param('path'));
      return files.get(param('path'));
    }
    if (path === '/api/level/create') {
      assert.ok(LEVELS.some((l) => l.id === body.levelId));
      assert.ok(!state.has(body.levelId), 'Created twice');
      const level = LEVELS.find((l) => l.id === body.levelId);
      const info = `${ROOT}/data/${level.id}/LevelInfo_${level.id}.asset`;
      const summary = { assetPath: info, sceneName: level.id, sceneAssetPath: level.scene, hasScene: true };
      ctx.levels.push(summary);
      const player = ctx.stock.find((c) => c.id === 'Player');
      state.set(level.id, { detail: { levelInfoAssetPath: info, levelName: body.levelName, sceneAssetPath: level.scene, dependencies: ['bundle47'], gridHalfSizeX: 20, gridHalfSizeY: 5, gridHalfSizeZ: 20,
        configs: Array.from({ length: 4 }, () => ({ exists: true, roundTime: 240, survivalTimeMultiplier: 1 })) },
      layout: { sceneAssetPath: level.scene,
        items: [1, 2].map((i) => ({ instanceId: `u:template:${i}`, hierarchyPath: `Design/Chefs/Player ${i}`, displayName: `Player ${i}`,
          prefabGuid: player.guid, prefabAssetPath: player.assetPath, footprint: player.footprint, stubKind: 'Player', worldPosition: { x: i * 2.4, y: 0, z: 0 } })),
        floors: [{ instanceId: 'u:template:floor', hierarchyPath: 'Art/Ground/Floor', parentPath: 'Art/Ground', displayName: 'Floor',
          worldPosition: { x: 5.4, y: -0.05, z: -3.6 }, localPosition: { x: 5.4, y: -0.05, z: -3.6 }, widthUnits: 24, depthUnits: 13.2 }],
        walkable: [], cameraInfo: { fieldOfView: 50 } },
      recipes: { levelInfoAssetPath: info, recipeGuids: [], optionalItems: [] }, readme: { exists: false } });
      files.set(info, ''); files.set(level.scene, 'created-scene');
      return { ok: true };
    }
    assert.ok(saved, `Unknown target ${url}`);
    if (path === '/api/level') return structuredClone(saved.detail);
    if (path === '/api/level/info') { Object.assign(saved.detail, body); return { ok: true }; }
    if (path === '/api/level/config') return { ok: true };
    if (path === '/api/level-recipes') {
      if (body) {
        saved.recipes.recipeGuids = body.recipeGuids;
        const allSteps = uniqueSteps(body.recipeGuids, ctx, files);
        files.set(body.levelInfoAssetPath, field('recipes', body.recipeGuids) + field('allCookingSteps', allSteps));
        return { ok: true };
      }
      return structuredClone(saved.recipes);
    }
    if (path === '/api/level/optional-items') {
      saved.recipes.optionalItems = body.guids.map((g) => ({ guid: g }));
      files.set(body.levelInfoAssetPath, files.get(body.levelInfoAssetPath) + field('optionalRecipeMatchListItems', body.guids));
      return { ok: true };
    }
    if (path === '/api/scene/layout') {
      if (body) {
        const idMap = new Map(body.items.map((it, i) => [it.instanceId, it.instanceId.startsWith('new:') ? `u:${i + 100}` : it.instanceId]));
        saved.layout = structuredClone(body);
        for (const i of saved.layout.items) {
          i.instanceId = idMap.get(i.instanceId);
          i.hierarchyPath = `${i.parentPath}/${i.displayName}`;
          if (i.servingStation) i.servingStation.plateReturnInstanceIds = i.servingStation.plateReturnInstanceIds.map((id) => idMap.get(id));
          if (i.stubKind === 'CookingUtensil' && !i.cookingUtensil) {
            const prefab = ctx.items.find((c) => c.guid === i.prefabGuid);
            i.cookingUtensil = { capacity: prefab?.capacity || 4, allowedIngredientGuids: prefab?.allowedIngredientGuids || [],
              stationTypes: prefab?.stationTypes || [], modelGuids: prefab?.modelGuids || [], modelSoGuids: prefab?.modelSoGuids || [] };
          }
        }
        for (const f of saved.layout.floors) { f.instanceId = `u:floor:${f.displayName}`; f.hierarchyPath = `${f.parentPath}/${f.displayName}`; }
        saved.layout.walkable = saved.layout.floors.map((f) => ({ cx: f.worldPosition.x, cz: f.worldPosition.z, sx: f.widthUnits, sz: f.depthUnits, surfaceType: 'solid' }));
        const supplied = saved.layout.items.filter((i) => i.dispenser).map((i) => i.dispenser.spawnerItemPrefabGuid);
        files.set(saved.detail.levelInfoAssetPath, files.get(saved.detail.levelInfoAssetPath) + field('allIngredients', supplied));
        files.set(body.sceneAssetPath, JSON.stringify(saved.layout));
        return { ok: true, warnings: [] };
      }
      return structuredClone(saved.layout);
    }
    if (path === '/api/level/readme') return saved.readme;
    if (path === '/api/level/readme-save') { saved.readme = { exists: true, html: body.html }; return saved.readme; }
    if (path === '/api/level/bundles') return { missing: [], current: saved.detail.dependencies };
    throw new Error(`Unexpected endpoint ${url}`);
  };
  return { api, state, calls };
}

function uniqueSteps(guids, ctx, files, seen = new Set()) {
  const steps = [];
  for (const g of guids) {
    if (seen.has(g)) continue;
    seen.add(g);
    const r = ctx.recipes.find((r) => r.guid === g);
    if (!r) continue;
    const raw = files.get(r.assetPath);
    steps.push(...serializedGuids(raw, 'cookingStepSO'), ...uniqueSteps(serializedGuids(raw, 'compositionSOs'), ctx, files, seen));
  }
  return [...new Set(steps)];
}

test('CLI defaults to dry-run and rejects unknown arguments', () => {
  assert.equal(argumentsOf([]).apply, false);
  assert.equal(argumentsOf(['--apply', '--skeleton']).skeleton, true);
  assert.throws(() => argumentsOf(['--set', 'test_diy_mm1']), /Unknown/);
  assert.throws(() => argumentsOf(['--base-url']), /requires/);
  assert.throws(() => argumentsOf(['--verify-only', '--apply']), /cannot be combined/);
});

test('raw field parser reads only the requested Unity reference array', () => {
  const a = guid('a'), b = guid('b');
  assert.deepEqual(serializedGuids(field('recipes', [a, b]) + field('allCookingSteps', [b]), 'recipes'), [a, b]);
  assert.deepEqual(serializedGuids('  recipes: []\n  other: 1\n', 'recipes'), []);
});

test('plans use arbitrary live IDs, include nested steps, item recipes and all seven kinds', async () => {
  const data = await fixture();
  // loadContext merges prep/item SO references from options, as the real API does.
  data.ctx.ingredients.push(...data.soRefs.filter((r) => !data.ctx.ingredients.some((i) => i.guid === r.guid)).map((r) => ({ ...r, id: r.id.replace(/_SO$/, '') })));
  const { plans, pending } = await buildPlans(data.ctx);
  assert.deepEqual(pending, []);
  assert.equal(plans.length, 4);
  for (const p of plans) validateGeometry(p);
  const cooking = plans[1], container = plans[2], control = plans[3];
  assert.ok(cooking.cookingStepGuids.length > 0);
  assert.ok(cooking.optionalGuids.includes(guid('recipe:cooked')));
  assert.ok(container.optionalGuids.includes(guid('recipe:batch')));
  assert.ok(container.items.some((i) => i.prefabGuid === guid('prefab:arbitrary_plate')));
  assert.ok(!container.items.some((i) => i.dispenser?.spawnerItemPrefabGuid === guid('so:arbitrary_plate')));
  assert.equal(new Set(control.customContent.map((i) => i.kind)).size, 7);
  assert.ok(control.items.some((i) => i.dispenser?.spawnerItemPrefabGuid === guid('so:arbitrary_red')));
  const pot = cooking.items.find((i) => i.displayName === 'TWDIY__Pot');
  assert.ok(pot.cookingUtensil.allowedIngredientGuids.includes(guid('so:arbitrary_cut')));
});

test('dry-run issues no POST and incomplete full apply fails before any write', async () => {
  const b = bridge(await fixture(false));
  const opts = argumentsOf([]);
  await run(opts, b.api, () => {});
  assert.equal(b.calls.filter((c) => c.body !== undefined).length, 0);
  await assert.rejects(run({ ...opts, apply: true }, b.api, () => {}), /Custom assets not ready/);
  assert.equal(b.calls.filter((c) => c.body !== undefined).length, 0);
});

test('handoff counts are dynamic and generated cut children are not Config assets', async () => {
  const data = await fixture();
  const handoff = { setName: SET, assets: data.ctx.items.map((i) => ({ ...i,
    soGuid: data.soRefs.find((s) => s.assetPath === i.assetPath.replace('.prefab', '_SO.asset')).guid,
    soPath: i.assetPath.replace('.prefab', '_SO.asset') })), recipes: data.ctx.customRecipes };
  data.ctx.items.push({ ...data.ctx.items[0], id: 'auto_Unworkable', guid: guid('auto-child') });
  validateHandoff(data.ctx, handoff);
  assert.equal(data.ctx.items.length, handoff.assets.length);
  assert.ok(!data.ctx.items.some((i) => i.id === 'auto_Unworkable'));
  assert.throws(() => validateHandoff(data.ctx, { ...handoff, assets: [...handoff.assets, handoff.assets[0]] }), /Duplicate/);
});

test('new supply containers, raw prep and odd splitter are planned without counting automatic cuts', async () => {
  const data = await fixture();
  const ingredient = data.ctx.items.find((i) => i.id === 'arbitrary_red');
  ingredient.platingStepId = 'Glass';
  data.ctx.items.find((i) => i.id === 'arbitrary_cut').platingStepId = 'Mug';
  data.ctx.items.find((i) => i.kind === 'splitter').splitNum = 2;
  data.ctx.ingredients.push(...data.soRefs.filter((r) => !data.ctx.ingredients.some((i) => i.guid === r.guid)));
  data.ctx.ingredients.push({ id: 'arbitrary_red_Unworkable', guid: guid('cut-child-so'),
    assetPath: ingredient.assetPath.replace('.prefab', '_Unworkable_SO.asset') });
  const { plans } = await buildPlans(data.ctx);
  for (const plan of plans) {
    for (const id of ['CleanGlassStack', 'GlassReturn', 'SinkGlass', 'cleanmugstack', 'workstation_mug_return', 'workstation_sink_mug_01_wood']) {
      assert.ok(plan.items.some((i) => i.displayName === `TWDIY__${id}`), `${plan.level.id}: ${id}`);
    }
    assert.ok(!plan.items.some((i) => i.dispenser?.spawnerItemPrefabGuid === guid('cut-child-so')));
  }
  for (const plan of [plans[2], plans[3]]) {
    assert.ok(plan.items.some((i) => i.prefabGuid === guid('prefab:arbitrary_splitter')));
    assert.ok(plan.items.some((i) => i.dispenser?.spawnerItemPrefabGuid === guid('so:arbitrary_prep')));
    assert.ok(plan.optionalGuids.includes(guid('recipe:batch')));
  }
});

test('stock skeleton creates four levels, saves, verifies, then resumes without duplicates', async () => {
  const b = bridge(await fixture(false));
  const opts = { ...argumentsOf([]), apply: true, skeleton: true };
  const result = await run(opts, b.api, () => {});
  assert.equal(result.results.length, 4);
  assert.ok(result.protectedAssetsUnchanged);
  for (const saved of b.state.values()) {
    assert.equal(saved.layout.items.filter((i) => i.stubKind === 'Player').length, 4);
    assert.equal(saved.layout.floors.length, 1);
  }
  const counts = [...b.state.values()].map((s) => s.layout.items.length);
  await run(opts, b.api, () => {});
  assert.deepEqual([...b.state.values()].map((s) => s.layout.items.length), counts);
  assert.equal(b.calls.filter((c) => c.path === '/api/level/create').length, 4);
  assert.ok(!b.calls.some((c) => c.body && JSON.stringify(c.body).includes('test_diy_mm1')));
});

test('skeleton-to-custom resume preserves hand edits and binds actual return IDs', async () => {
  const data = await fixture(true), b = bridge(data);
  const opts = { ...argumentsOf([]), apply: true };
  await run({ ...opts, skeleton: true }, b.api, () => {});
  const first = b.state.get(LEVELS[0].id);
  first.layout.items.push({ ...structuredClone(first.layout.items[0]), displayName: 'HandAddedCounter', instanceId: 'u:hand', hierarchyPath: 'Design/Counters/HandAddedCounter', worldPosition: { x: -15, y: 0, z: 5 } });
  const result = await run(opts, b.api, () => {});
  assert.equal(result.results.length, 4);
  assert.ok(first.layout.items.some((i) => i.displayName === 'HandAddedCounter'));
  const counts = [...b.state.values()].map((s) => s.layout.items.length);
  await run(opts, b.api, () => {});
  assert.deepEqual([...b.state.values()].map((s) => s.layout.items.length), counts);
  assert.equal(b.calls.filter((c) => c.path === '/api/level/create').length, 4);
});

test('existing destination without ownership marker is rejected without writes', async () => {
  const b = bridge(await fixture(false));
  await run({ ...argumentsOf([]), apply: true, skeleton: true }, b.api, () => {});
  b.state.get(LEVELS[0].id).detail.levelName = 'User-owned test';
  b.calls.length = 0;
  await assert.rejects(run({ ...argumentsOf([]), apply: true, skeleton: true }, b.api, () => {}), /not generator-owned/);
  assert.equal(b.calls.filter((c) => c.body !== undefined).length, 0);
});

test('uncertain layout write can be resumed after read-back', async () => {
  const b = bridge(await fixture(false));
  let failed = false;
  const flaky = async (...args) => {
    const result = await b.api(...args);
    if (!failed && args[0].startsWith('/api/scene/layout?') && args[1]) { failed = true; throw new Error('simulated disconnect after save'); }
    return result;
  };
  const opts = { ...argumentsOf([]), apply: true, skeleton: true };
  await assert.rejects(run(opts, flaky, () => {}), /simulated disconnect/);
  await run(opts, b.api, () => {});
  assert.equal(b.calls.filter((c) => c.path === '/api/level/create').length, 4);
  const first = b.state.get(LEVELS[0].id);
  assert.equal(new Set(first.layout.items.map((i) => i.displayName)).size, first.layout.items.length);
});

test('disconnect after CreateLevel resumes the saved template and keeps exactly four spawns', async () => {
  const b = bridge(await fixture(false));
  let failed = false;
  const flaky = async (...args) => {
    const result = await b.api(...args);
    if (!failed && args[0] === '/api/level/create') { failed = true; throw new Error('disconnect after create'); }
    return result;
  };
  const opts = { ...argumentsOf([]), apply: true, skeleton: true };
  await assert.rejects(run(opts, flaky, () => {}), /disconnect after create/);
  await run(opts, b.api, () => {});
  assert.equal(b.calls.filter((c) => c.path === '/api/level/create').length, 4);
  assert.equal(b.state.get(LEVELS[0].id).layout.items.filter((i) => i.stubKind === 'Player').length, 4);
});

test('read-back rejects cooking step dependencies silently dropped by the bridge', async () => {
  const b = bridge(await fixture(false));
  const broken = async (...args) => {
    const result = await b.api(...args);
    if (args[0].startsWith('/api/level/data-file?') && String(result).includes('  allCookingSteps:')) {
      return String(result).replace(/  allCookingSteps:[^\n]*(?:\n  -[^\n]*)*/, '  allCookingSteps: []');
    }
    return result;
  };
  await assert.rejects(run({ ...argumentsOf([]), apply: true, skeleton: true }, broken, () => {}), /allCookingSteps/);
});

test('API client blocks POST in dry-run before touching transport', async () => {
  await assert.rejects(apiClient(argumentsOf([]))('/api/level/create', {}), /dry-run cannot POST/);
});

test('Unity float read-back does not expand whole-cell floors on resume', async () => {
  const data = await fixture(false), { plans } = await buildPlans(data.ctx, { skeleton: true });
  const p = plans[0];
  const current = { sceneAssetPath: p.level.scene, items: [], floors: [{ ...structuredClone(p.floor),
    widthUnits: 43.20000076293945, depthUnits: 26.400001525878906, instanceId: 'u:floor' }] };
  const doc = reconcile(p, current), floor = doc.floors[0];
  assert.equal(floor.widthCells, 36); assert.equal(floor.depthCells, 22);
  assert.equal(floor.widthUnits, 43.2); assert.equal(floor.depthUnits, 26.4);
});

test('hotpot generation adds scanner name without changing role IDs', async () => {
  const data = await fixture();
  data.ctx.items.find((i) => i.id === 'arbitrary_pot').templateId = 'utensil_hotpot';
  data.ctx.ingredients.push(...data.soRefs.filter((r) => !data.ctx.ingredients.some((i) => i.guid === r.guid)));
  const { plans } = await buildPlans(data.ctx);
  for (const plan of plans.filter((p) => p.level.kinds.includes('utensil'))) {
    const hotpot = plan.items.find((i) => i.prefabGuid === guid('prefab:arbitrary_pot'));
    assert.equal(hotpot.displayName, 'TWDIY__custom_arbitrary_pot_large_pot');
    assert.equal(hotpot.instanceId, `new:${hotpot.prefabGuid}:${plan.level.id}:custom_arbitrary_pot`);
  }
  assert.equal(LEVELS.find((l) => l.id === 'twdiy_export_control').title, '全素材回归');
});

test('legacy hotpot rebuild keeps role and settings, remaps references, and rejects animation scenes', async () => {
  const data = await fixture(false), { plans } = await buildPlans(data.ctx, { skeleton: true });
  const plan = plans[0], base = structuredClone(plan.items[0]);
  const hotpot = { ...base, instanceId: 'new:stable:custom_hotpot', hierarchyPath: 'new:stable:custom_hotpot',
    displayName: 'TWDIY__custom_hotpot_large_pot', worldPosition: { x: 8, y: 0, z: 5 } };
  plan.items.push(hotpot);
  plan.customSettings = [{ id: 'hotpot', guid: hotpot.prefabGuid, templateId: 'utensil_hotpot' }];
  const old = { ...structuredClone(hotpot), instanceId: 'u:hotpot', hierarchyPath: 'Design/Counters/TWDIY__custom_hotpot', displayName: 'TWDIY__custom_hotpot' };
  old.cookingUtensil = { capacity: 4, allowedIngredientGuids: [], modelGuids: ['', guid('model')] };
  const current = { sceneAssetPath: plan.level.scene, items: [old], floors: [],
    links: [{ target: old.instanceId, targets: [old.instanceId], path: old.hierarchyPath }] };
  const doc = reconcile(plan, current);
  const rebuilt = doc.items.find((i) => i.instanceId === hotpot.instanceId);
  assert.equal(rebuilt.displayName, hotpot.displayName);
  assert.equal(rebuilt.hierarchyPath, hotpot.instanceId);
  assert.deepEqual(rebuilt.cookingUtensil, old.cookingUtensil);
  assert.deepEqual(rebuilt.worldPosition, old.worldPosition);
  assert.ok(!doc.items.some((i) => i.instanceId === old.instanceId));
  assert.equal(doc.links[0].target, hotpot.instanceId);
  assert.deepEqual(doc.links[0].targets, [hotpot.instanceId]);
  assert.equal(doc.links[0].path, `${old.parentPath}/${hotpot.displayName}`);
  assert.equal(current.items[0].instanceId, 'u:hotpot');
  assert.throws(() => reconcile(plan, { ...current, animControls: { groups: [{}] } }), /animation bindings/);
  assert.throws(() => reconcile(plan, { ...current, moveControls: { groups: [{}] } }), /animation bindings/);
  const alreadyNamed = structuredClone(current);
  alreadyNamed.items[0].displayName = hotpot.displayName;
  assert.equal(reconcile(plan, alreadyNamed).items.find((i) => i.displayName === hotpot.displayName).instanceId, old.instanceId);
  assert.equal(hotpot.instanceId, 'new:stable:custom_hotpot');
  assert.equal(doc.items.length, plan.items.length);
});

test('resume restores custom utensil ingredient whitelist without rebuilding sparse model slots', async () => {
  const b = bridge(await fixture());
  const opts = { ...argumentsOf([]), apply: true };
  await run(opts, b.api, () => {});
  const pot = b.state.get(LEVELS[1].id).layout.items.find((i) => i.prefabGuid === guid('prefab:arbitrary_pot'));
  pot.cookingUtensil.modelGuids = ['', guid('model')];
  pot.cookingUtensil.modelSoGuids = ['', guid('model-so')];
  const data = await fixture();
  data.ctx.ingredients.push(...data.soRefs.filter((r) => !data.ctx.ingredients.some((i) => i.guid === r.guid)));
  const { plans } = await buildPlans(data.ctx);
  const plan = plans[1];
  plan.customSettings.find((c) => c.id === 'arbitrary_pot').allowedIngredientGuids = [guid('so:arbitrary_cut')];
  const doc = reconcile(plan, b.state.get(LEVELS[1].id).layout);
  const saved = doc.items.find((i) => i.instanceId === pot.instanceId);
  assert.ok(saved.cookingUtensil.allowedIngredientGuids.includes(guid('so:arbitrary_cut')));
  assert.deepEqual(saved.cookingUtensil.modelGuids, ['', guid('model')]);
  assert.deepEqual(saved.cookingUtensil.modelSoGuids, ['', guid('model-so')]);
});

test('apply migrates legacy hotpot by deleting old ID and creating the stable role, then resumes unchanged', async () => {
  const data = await fixture();
  data.ctx.items.find((i) => i.id === 'arbitrary_pot').templateId = 'utensil_hotpot';
  const b = bridge(data), opts = { ...argumentsOf([]), apply: true };
  await run(opts, b.api, () => {});
  const saved = b.state.get(LEVELS[1].id);
  const pot = saved.layout.items.find((i) => i.prefabGuid === guid('prefab:arbitrary_pot'));
  pot.displayName = 'TWDIY__custom_arbitrary_pot';
  pot.instanceId = 'u:legacy-hotpot';
  pot.hierarchyPath = `${pot.parentPath}/${pot.displayName}`;
  const count = saved.layout.items.length;
  b.calls.length = 0;
  await run(opts, b.api, () => {});
  const payload = b.calls.find((c) => c.path === '/api/scene/layout' && c.body?.sceneAssetPath === LEVELS[1].scene).body;
  assert.ok(!payload.items.some((i) => i.instanceId === 'u:legacy-hotpot'));
  assert.ok(payload.items.some((i) => i.displayName === 'TWDIY__custom_arbitrary_pot_large_pot' &&
    i.instanceId === `new:${pot.prefabGuid}:${LEVELS[1].id}:custom_arbitrary_pot`));
  assert.equal(saved.layout.items.length, count);
  const newId = saved.layout.items.find((i) => i.prefabGuid === pot.prefabGuid).instanceId;
  await run(opts, b.api, () => {});
  assert.equal(saved.layout.items.find((i) => i.prefabGuid === pot.prefabGuid).instanceId, newId);
});

test('connectivity rejects an enclosed chef spawn', async () => {
  const data = await fixture(false), { plans } = await buildPlans(data.ctx, { skeleton: true });
  const p = plans[0], doc = { floors: [p.floor], items: structuredClone(p.items) };
  const player = doc.items.find((i) => i.stubKind === 'Player');
  const blocker = { ...structuredClone(doc.items[0]), displayName: 'BlockingCounter', worldPosition: { ...player.worldPosition } };
  doc.items.push(blocker);
  assert.throws(() => checkConnectivity(doc), /Blocked chef spawn/);
});

test('splitter whole-cell migration avoids saved obstacles and preserves identity', async () => {
  const data = await fixture();
  data.ctx.ingredients.push(...data.soRefs.filter((r) => !data.ctx.ingredients.some((i) => i.guid === r.guid)));
  const { plans } = await buildPlans(data.ctx);
  const plan = plans[2];
  const current = { sceneAssetPath: plan.level.scene, items: structuredClone(plan.items), floors: [structuredClone(plan.floor)] };
  const splitter = current.items.find((i) => i.prefabGuid === guid('prefab:arbitrary_splitter'));
  splitter.instanceId = 'u:saved-splitter';
  const obstacle = current.items.find((i) => i.displayName === 'TWDIY__Counter');
  splitter.worldPosition = { ...obstacle.worldPosition };
  splitter.localPosition = { ...splitter.worldPosition };
  const doc = reconcile(plan, current);
  const saved = doc.items.find((i) => i.instanceId === 'u:saved-splitter');
  assert.ok(Math.abs(saved.worldPosition.x / 1.2 - Math.round(saved.worldPosition.x / 1.2)) < 0.001);
  assert.ok(Math.abs(saved.worldPosition.z / 1.2 - Math.round(saved.worldPosition.z / 1.2)) < 0.001);
  validateGeometry({ items: doc.items, floor: doc.floors[0] });
});

test('verify-only validates all saved levels without POST requests', async () => {
  const b = bridge(await fixture(false));
  await run({ ...argumentsOf([]), apply: true, skeleton: true }, b.api, () => {});
  b.calls.length = 0;
  const result = await run({ ...argumentsOf([]), verifyOnly: true, skeleton: true }, b.api, () => {});
  assert.equal(result.results.length, 4);
  assert.equal(b.calls.filter((c) => c.body !== undefined).length, 0);
});

test('HTTP client round-trips JSON DTOs and raw Unity read-back over actual transport', async () => {
  const b = bridge(await fixture(false));
  const server = createServer(async (req, res) => {
    try {
      const chunks = [];
      for await (const chunk of req) chunks.push(chunk);
      const body = chunks.length ? JSON.parse(Buffer.concat(chunks).toString()) : undefined;
      const raw = req.url.startsWith('/api/level/data-file?');
      const result = await b.api(req.url, body, raw);
      res.writeHead(200, { 'Content-Type': raw ? 'application/octet-stream' : 'application/json' });
      res.end(raw ? result : JSON.stringify(result));
    } catch (e) {
      res.writeHead(400, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ error: e.message }));
    }
  });
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  try {
    const opts = { ...argumentsOf([]), apply: true, skeleton: true, baseUrl: `http://127.0.0.1:${server.address().port}` };
    const result = await run(opts, apiClient(opts), () => {});
    assert.equal(result.results.length, 4);
    assert.ok(result.results.every((r) => r.verified));
  } finally {
    server.closeAllConnections();
    await new Promise((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));
  }
});
