const SET = 'tester_web';
const RECIPE_IDS = {
  assembly: 'Web_V011_SandwichAssembly',
  spice: 'Web_V011_Spice_SpringOnion_Sandwich',
  garden: 'Web_V011_Garden_Sandwich',
};
const SANDWICH_ASSET_ROOT = 'Assets/LevelSets/tester_web/custom_recipes/';
const assert = (ok, message) => { if (!ok) throw new Error(message); };
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);

const OBJ = `mtllib sandwich.mtl
o sandwich
usemtl bread_bottom
v -0.45 0.00 -0.25
v 0.45 0.00 -0.25
v 0.45 0.12 -0.25
v -0.45 0.12 -0.25
v -0.45 0.00 0.25
v 0.45 0.00 0.25
v 0.45 0.12 0.25
v -0.45 0.12 0.25
f 1 2 3 4
f 5 8 7 6
f 1 5 6 2
f 2 6 7 3
f 3 7 8 4
f 5 1 4 8
usemtl filling
v -0.40 0.12 -0.22
v 0.40 0.12 -0.22
v 0.40 0.20 -0.22
v -0.40 0.20 -0.22
v -0.40 0.12 0.22
v 0.40 0.12 0.22
v 0.40 0.20 0.22
v -0.40 0.20 0.22
f 9 10 11 12
f 13 16 15 14
f 9 13 14 10
f 10 14 15 11
f 11 15 16 12
f 13 9 12 16
usemtl bread_top
v -0.45 0.20 -0.25
v 0.45 0.20 -0.25
v 0.45 0.32 -0.25
v -0.45 0.32 -0.25
v -0.45 0.20 0.25
v 0.45 0.20 0.25
v 0.45 0.32 0.25
v -0.45 0.32 0.25
f 17 18 19 20
f 21 24 23 22
f 17 21 22 18
f 18 22 23 19
f 19 23 24 20
f 21 17 20 24
`;
const MTL = `newmtl bread_bottom
Kd 0.78 0.48 0.20
Ka 0.15 0.08 0.02
newmtl filling
Kd 0.28 0.62 0.16
Ka 0.04 0.12 0.02
newmtl bread_top
Kd 0.92 0.68 0.34
Ka 0.16 0.10 0.03
`;

function bytesBase64(value) {
  return Buffer.from(value, 'utf8').toString('base64');
}

function plan(ctx) {
  assert(ctx && Array.isArray(ctx.ingredients) && Array.isArray(ctx.recipes), 'ctx.ingredients and ctx.recipes must be live arrays');
  const ingredient = id => {
    const rows = ctx.ingredients.filter(row => row.id === id);
    assert(rows.length === 1 && rows[0].guid, `Ingredient identity missing or ambiguous: ${id}`);
    return rows[0];
  };
  const liveNames = new Set(ctx.ingredients.map(row => row.id));
  for (const id of ['hotdogbun', 'SpiceSO', 'SpringOnionSO', 'LettuceSO', 'TomatoSO', 'dlc11_ketchup']) ingredient(id);
  assert(liveNames.has('dlc11_ketchup'), 'Required available sauce dlc11_ketchup is missing');
  const plate = (ctx.refs?.platingContainers || []).find(row => row.id === 'Plate');
  assert(plate?.guid, 'Live plating-container references must contain Plate');

  const bread = ingredient('hotdogbun');
  const spice = ingredient('SpiceSO');
  const onion = ingredient('SpringOnionSO');
  const lettuce = ingredient('LettuceSO');
  const tomato = ingredient('TomatoSO');
  const ketchup = ingredient('dlc11_ketchup');
  const recipeById = new Map(ctx.recipes.map(row => [row.id, row]));
  const rows = [
    { id: RECIPE_IDS.assembly, kind: 'assembly', type: 'Composite', score: 0, compositionIds: [],
      optionalKind: 'sandwich', toastGuid: bread.guid, toastId: bread.assetPath.split('/').pop().replace(/\.asset$/, ''),
      fillingGuids: [spice.guid, onion.guid, lettuce.guid, tomato.guid], fillingIds: [spice.id, onion.id, lettuce.id, tomato.id],
      sauceGuids: [ketchup.guid], sauceIds: [ketchup.id], ingredientContainerCapacity: 3, platingStepId: 'Plate', platingStepGuid: plate.guid },
    { id: RECIPE_IDS.spice, kind: 'order', type: 'Composite', score: 60,
      compositionIds: ['hotdogbun', 'SpiceSO', 'SpringOnionSO'], platingStepId: 'Plate', platingStepGuid: plate.guid, modelPrefabId: RECIPE_IDS.assembly },
    { id: RECIPE_IDS.garden, kind: 'order', type: 'Composite', score: 40,
      compositionIds: ['hotdogbun', 'LettuceSO', 'TomatoSO', 'dlc11_ketchup'], platingStepId: 'Plate', platingStepGuid: plate.guid, modelPrefabId: RECIPE_IDS.assembly },
  ];
  for (const row of rows) {
    if (row.kind === 'order') {
      row.compositionIds = row.compositionIds.map(id => {
        if (id === 'hotdogbun' || id === 'SpiceSO' || id === 'SpringOnionSO' || id === 'LettuceSO' || id === 'TomatoSO' || id === 'dlc11_ketchup') return id;
        throw new Error(`Unexpected composition id ${id}`);
      });
      for (const id of row.compositionIds) ingredient(id);
    }
    const old = recipeById.get(row.id);
    if (old) assert(old.assetPath && old.assetPath.startsWith(SANDWICH_ASSET_ROOT), `Recipe ownership conflict: ${row.id}`);
  }
  return rows;
}

function expectedRecipe(row, resolvedComposition) {
  const base = { id: row.id, type: row.type, score: row.score, compositionIds: resolvedComposition,
    platingStepId: row.platingStepId, modelPrefabId: row.modelPrefabId || '' };
  if (row.kind === 'assembly') Object.assign(base, { optionalKind: 'sandwich', toastId: row.toastId,
    fillingIds: row.fillingIds, sauceIds: row.sauceIds, sandwichCapacity: 3 });
  return base;
}

function matches(row, expected) {
  return row && row.type === expected.type && row.score === expected.score &&
    same(row.compositionIds || [], expected.compositionIds) && row.platingStepId === expected.platingStepId &&
    (expected.kind !== 'order' || row.modelPrefabId === RECIPE_IDS.assembly) &&
      (expected.kind !== 'assembly' || (row.optionalKind === 'sandwich' && row.toastId === expected.toastId &&
      same(row.fillingIds || [], expected.fillingIds) && same(row.sauceIds || [], expected.sauceIds) && row.sandwichCapacity === 3));
}

function modelFiles() {
  return [{ fileName: 'Web_V011_SandwichAssembly.obj', base64: bytesBase64(OBJ) },
    { fileName: 'sandwich.mtl', base64: bytesBase64(MTL) }];
}

/** Ensure the three v0.11 sandwich recipes. Dry-run resolves and returns a plan without API calls. */
export async function ensureSandwich(api, ctx, { apply = false } = {}) {
  const rows = plan(ctx);
  const planned = rows.map(row => ({ ...row, expected: expectedRecipe(row, row.compositionIds) }));
  if (!apply) return { dryRun: true, plan: planned };
  assert(typeof api === 'function', 'api must be the gen-tester-web-diy-levels apiClient');

  await api(`/api/custom-recipes/config?setName=${encodeURIComponent(SET)}`);
  let live = (await api(`/api/custom-recipes?setName=${encodeURIComponent(SET)}`)).recipes || [];
  const find = id => live.find(item => item.id === id);
  const updateAndRead = async (path, body, id, validate) => {
    await api(path, body);
    live = (await api(`/api/custom-recipes?setName=${encodeURIComponent(SET)}`)).recipes || [];
    const item = find(id);
    assert(validate(item), `Readback mismatch after ${path}: ${id}`);
    return item;
  };
  const assertNoConflict = row => {
    const current = find(row.id);
    if (current) assert(current.assetPath && current.assetPath.startsWith(SANDWICH_ASSET_ROOT), `Recipe ownership conflict: ${row.id}`);
    return current;
  };

  for (const row of rows.filter(item => item.kind === 'assembly')) {
    const current = assertNoConflict(row);
    const expected = expectedRecipe(row, row.compositionIds);
    if (!current) {
      await api('/api/custom-recipes/create', { setName: SET, recipeName: row.id, nameZh: row.id, nameEn: row.id,
        category: 'tester_web_v011', score: row.score, type: row.type, compositionIds: row.compositionIds,
        platingStepId: row.platingStepId, modelScale: 1, cookingProgress: 1, mixingProgress: 1,
        ...(row.modelPrefabId ? { modelPrefabId: row.modelPrefabId } : {}),
        ...(row.kind === 'assembly' ? { optionalKind: row.optionalKind, toastGuid: row.toastGuid,
          fillingGuids: row.fillingGuids, sauceGuids: row.sauceGuids, ingredientContainerCapacity: row.ingredientContainerCapacity } : {}) });
      live = (await api(`/api/custom-recipes?setName=${encodeURIComponent(SET)}`)).recipes || [];
      assert(find(row.id), `Created recipe did not read back: ${row.id}`);
    }
  }

  let assembly = find(RECIPE_IDS.assembly);
  assert(assembly && assembly.assetPath, 'Assembly recipe missing assetPath after create/readback');
  if (!assembly.hasModel) {
    await api('/api/custom-recipes/upload-model', { setName: SET, recipeAssetPath: assembly.assetPath, files: modelFiles() });
    live = (await api(`/api/custom-recipes?setName=${encodeURIComponent(SET)}`)).recipes || [];
    assembly = find(RECIPE_IDS.assembly);
    assert(assembly && assembly.hasModel, 'Assembly model upload did not read back with hasModel=true');
  }

  const assemblyModelUpdate = { setName: SET, assetPath: assembly.assetPath, recipeName: assembly.recipeName || assembly.id,
    nameZh: assembly.nameZh || assembly.id, nameEn: assembly.nameEn || assembly.id, category: assembly.category || 'tester_web_v011',
    score: 0, type: 'Composite', compositionIds: [], platingStepId: assembly.platingStepId || 'Plate',
    modelPrefabId: RECIPE_IDS.assembly, cookingProgress: 1, mixingProgress: 1,
    modelScale: assembly.modelScale || 1, modelRotationY: assembly.modelRotationY || 0,
    modelRotationX: assembly.modelRotationX || 0, modelRotationZ: assembly.modelRotationZ || 0,
    modelPositionX: assembly.modelPositionX || 0, modelPositionY: assembly.modelPositionY || 0, modelPositionZ: assembly.modelPositionZ || 0,
    modelPivotX: assembly.modelPivotX || 0, modelPivotY: assembly.modelPivotY || 0, modelPivotZ: assembly.modelPivotZ || 0,
    optionalKind: 'sandwich', toastGuid: plan(ctx)[0].toastGuid,
    fillingGuids: plan(ctx)[0].fillingGuids, sauceGuids: plan(ctx)[0].sauceGuids, ingredientContainerCapacity: 3 };
  if (!assembly.hasModel || !assembly.previewable || assembly.toastId !== plan(ctx)[0].toastId ||
      !same(assembly.fillingIds || [], plan(ctx)[0].fillingIds) || !same(assembly.sauceIds || [], plan(ctx)[0].sauceIds) ||
      assembly.sandwichCapacity !== 3) {
    assembly = await updateAndRead('/api/custom-recipes/update', assemblyModelUpdate, RECIPE_IDS.assembly,
      item => item && item.hasModel && item.optionalKind === 'sandwich' && item.toastId === plan(ctx)[0].toastId &&
        same(item.fillingIds || [], plan(ctx)[0].fillingIds) && same(item.sauceIds || [], plan(ctx)[0].sauceIds) && item.sandwichCapacity === 3);
  }

  const orders = [];
  for (const row of rows.filter(item => item.kind === 'order')) {
    const current = find(row.id), expected = expectedRecipe(row, row.compositionIds);
    if (!current) {
      await api('/api/custom-recipes/create', { setName: SET, recipeName: row.id, nameZh: row.id, nameEn: row.id,
        category: 'tester_web_v011', score: row.score, type: 'Composite', compositionIds: row.compositionIds,
        platingStepId: 'Plate', modelPrefabId: RECIPE_IDS.assembly, modelScale: 1, cookingProgress: 1, mixingProgress: 1 });
      live = (await api(`/api/custom-recipes?setName=${encodeURIComponent(SET)}`)).recipes || [];
      assert(matches(find(row.id), expected) && find(row.id).hasModel, `Created order recipe readback mismatch: ${row.id}`);
    } else if (!matches(current, expected) || !current.hasModel) {
      assert(current.assetPath && current.assetPath.startsWith(SANDWICH_ASSET_ROOT), `Recipe identity conflict: ${row.id}`);
      await updateAndRead('/api/custom-recipes/update', { setName: SET, assetPath: current.assetPath, recipeName: current.id,
        nameZh: current.nameZh || current.id, nameEn: current.nameEn || current.id, category: current.category || 'tester_web_v011',
        score: row.score, type: 'Composite', compositionIds: row.compositionIds, platingStepId: 'Plate',
        modelPrefabId: RECIPE_IDS.assembly, cookingProgress: current.cookingProgress ?? 1, mixingProgress: current.mixingProgress ?? 1,
        modelScale: current.modelScale || 1, modelRotationY: current.modelRotationY || 0,
        modelRotationX: current.modelRotationX || 0, modelRotationZ: current.modelRotationZ || 0,
        modelPositionX: current.modelPositionX || 0, modelPositionY: current.modelPositionY || 0, modelPositionZ: current.modelPositionZ || 0,
        modelPivotX: current.modelPivotX || 0, modelPivotY: current.modelPivotY || 0, modelPivotZ: current.modelPivotZ || 0 }, row.id,
      item => item && item.hasModel && item.type === 'Composite' && item.score === row.score &&
        same(item.compositionIds || [], row.compositionIds) && item.platingStepId === 'Plate');
    }
    const item = find(row.id);
    assert(matches(item, expected) && item.hasModel, `Order readback mismatch: ${row.id}`);
    orders.push(item);
  }
  assembly = find(RECIPE_IDS.assembly);
  assert(assembly && assembly.hasModel, 'Final assembly readback is missing its model');
  return { assembly, orders };
}
