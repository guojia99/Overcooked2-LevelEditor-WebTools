#!/usr/bin/env node
/**
 * Append four tester_web_diy test levels through Unity, never edit Unity assets.
 *
 * node layout-editor/scripts/gen-tester-web-diy-levels.mjs
 * node layout-editor/scripts/gen-tester-web-diy-levels.mjs --apply --skeleton
 * node layout-editor/scripts/gen-tester-web-diy-levels.mjs --apply --base-url http://10.211.55.3:8765
 *
 * Default is a dry-run plan (no POST or filesystem writes). --skeleton installs only stock equipment
 * and stock orders while another agent prepares custom assets. Rerun without it
 * to add live custom content. No automatic POST retries: after an interrupted
 * write, rerun to discover the saved scene and reuse its real instance IDs.
 */
import { createHash } from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { readFile, writeFile } from 'node:fs/promises';

const SET = 'tester_web_diy';
const ROOT = `Assets/LevelSets/${SET}`;
const MARKER = 'gen-tester-web-diy-levels:v1';
const PREFIX = 'TWDIY__';
const CELL = 1.2;
const KINDS = ['ingredient', 'counter', 'utensil', 'sink', 'tray', 'prep', 'splitter'];
const V = (x = 0, y = 0, z = 0) => ({ x, y, z });
const unique = (xs) => [...new Set(xs)];
const hash = (s) => createHash('sha256').update(s).digest('hex');
const escapeHtml = (s) => String(s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const query = (path, params) => `${path}?${new URLSearchParams(params)}`;
const assert = (ok, message) => { if (!ok) throw new Error(message); };
const round = (x) => Math.round(x * 1000) / 1000;

export const LEVELS = [
  { id: 'twdiy_basic_processing', title: '基础加工', kinds: ['ingredient', 'counter', 'sink'] },
  { id: 'twdiy_cooking', title: '烹饪', kinds: ['ingredient', 'utensil', 'counter'] },
  { id: 'twdiy_containers_splitter', title: '容器分料', kinds: ['ingredient', 'prep', 'tray', 'splitter', 'sink', 'utensil'] },
  { id: 'twdiy_export_control', title: '全素材回归', kinds: KINDS },
].map((x) => ({ ...x, name: `[${MARKER}] ${x.id}`, scene: `${ROOT}/scenes/${x.id}.unity` }));

// The IDs are functional catalog IDs, not inferred asset GUIDs. The backend's
// cookingGroups is authoritative; these pairs also handle custom utensil hosts.
const STEPS = {
  Pot: ['Cooker', 'Pot'], FryingPan: ['Cooker', 'FryPan'],
  DeepFatFryer: ['FryingStation', 'FrierBasket'], OvenTray: ['Oven'],
  Steamer: ['Cooker', 'Steamer'], Mixer: ['Mixer', 'MixerBowl'],
  MixingBowl: ['Mixer', 'MixerBowl'], Blender: ['Blender', 'BlenderCup'],
  GriddlePan: ['Campfire', 'GriddlePan'], KebabSkewer: ['Barbeque', 'Skewer'],
  ToastingFork: ['Campfire', 'ToastingFork'],
  HotPot: ['web_cooking_region_floorburner', 'web_utensil_large_pot_01'],
  RoastingTray: ['Oven', 'utensil_roasting_tray'], OvenCakeTin: ['Mixer', 'MixerBowl', 'Oven'],
};
const HOSTS = {
  Hob: 'Cooker', Oven: 'Oven', DeepFatFryer: 'FryingStation', FirePit: 'Campfire',
  Barbeque: 'Barbeque', Flamethrower: 'Flamethrower', Invalid_FloorBurner: 'web_cooking_region_floorburner',
};
const CONTAINERS = {
  Plate: ['CleanPlateStack', 'PlateReturn', 'Sink'],
  Glass: ['CleanGlassStack', 'GlassReturn', 'SinkGlass'],
  Mug: ['cleanmugstack', 'workstation_mug_return', 'workstation_sink_mug_01_wood'],
  Tray: ['dlc08_cleantraystack', 'dlc08_workstation_tray_return', 'dlc08_workstation_01_tray_sink_circus'],
};
const STOCK_ORDERS = ['Salad_Plain_SO', 'Soup_Tomato_SO'];
const UTENSILS = new Set(['Pot', 'FryPan', 'FrierBasket', 'Steamer', 'MixerBowl', 'BlenderCup',
  'GriddlePan', 'Skewer', 'ToastingFork', 'utensil_roasting_tray', 'web_utensil_large_pot_01']);
const CAPACITY = { Pot: 3, MixerBowl: 4, BlenderCup: 4, Skewer: 3, utensil_roasting_tray: 4, web_utensil_large_pot_01: 4 };

export function argumentsOf(argv) {
  const opts = { apply: false, skeleton: false, baseUrl: 'http://10.211.55.3:8765', timeout: 120000 };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--apply') opts.apply = true;
    else if (arg === '--dry-run') opts.apply = false;
    else if (arg === '--skeleton') opts.skeleton = true;
    else if (arg === '--verify-only') opts.verifyOnly = true;
    else if (arg === '--help' || arg === '-h') opts.help = true;
    else if (arg === '--base-url') {
      assert(argv[i + 1] && !argv[i + 1].startsWith('--'), '--base-url requires a URL');
      opts.baseUrl = argv[++i];
    } else if (arg === '--timeout-ms') {
      opts.timeout = Number(argv[++i]);
      assert(Number.isFinite(opts.timeout) && opts.timeout >= 1000, '--timeout-ms requires milliseconds >= 1000');
    } else throw new Error(`Unknown option: ${arg}`);
  }
  const url = new URL(opts.baseUrl);
  assert(!(opts.verifyOnly && opts.apply), '--verify-only cannot be combined with --apply');
  assert(['http:', 'https:'].includes(url.protocol), 'Expected an HTTP(S) base URL');
  opts.baseUrl = url.origin;
  return opts;
}

export function apiClient(opts) {
  return async (path, body, text = false) => {
    const method = body === undefined ? 'GET' : 'POST';
    assert(method !== 'POST' || opts.apply, 'dry-run cannot POST');
    let response;
    try {
      response = await fetch(`${opts.baseUrl}${path}`, {
        method, signal: AbortSignal.timeout(path === '/api/health' ? Math.min(opts.timeout, 15000) : opts.timeout),
        headers: { Connection: 'close', ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
        ...(body === undefined ? {} : { body: JSON.stringify(body) }),
      });
    } catch (e) {
      throw new Error(`${method} ${path}: ${e.message}. ${method === 'POST' ? 'Write outcome uncertain; rerun to read back, do not blindly retry.' : 'Check Unity bridge connectivity.'}`);
    }
    let raw;
    try { raw = await response.text(); }
    catch (e) { throw new Error(`${method} ${path}: response interrupted: ${e.message}. Read back saved state before resuming.`); }
    if (text) {
      assert(response.ok, `${method} ${path}: HTTP ${response.status} ${raw.slice(0, 500)}`);
      return raw;
    }
    let data;
    try { data = JSON.parse(raw); } catch { throw new Error(`${method} ${path}: expected JSON, HTTP ${response.status}`); }
    assert(response.ok && !data.error && data.ok !== false,
      `${method} ${path}: HTTP ${response.status} ${data.error || data.message || raw.slice(0, 500)}`);
    return data;
  };
}

// Only reads Unity's serialized fields through the existing raw-file API. This
// deliberately is not a YAML writer or a general YAML parser.
export function serializedGuids(text, field) {
  const match = text.match(new RegExp(`^  ${field}:[^\\n]*(?:\\n  -[^\\n]*)*`, 'm'));
  return match ? [...match[0].matchAll(/guid: ([a-f0-9]{32})/g)].map((m) => m[1]) : [];
}

function exact(entries, id, label) {
  const matches = entries.filter((e) => e.id === id);
  const byGuid = new Map(matches.map((e) => [e.guid, e]));
  assert(byGuid.size === 1, `${label} ${id}: expected one exact ID, found ${byGuid.size}`);
  const entry = [...byGuid.values()][0];
  assert(/^[a-f0-9]{32}$/.test(entry.guid) && entry.assetPath, `${label} ${id}: missing live GUID/path`);
  return entry;
}

export async function loadContext(api) {
  const health = await api('/api/health');
  assert(health.ok, 'Unity health is not ready');
  const levels = (await api(`/api/sets/${SET}/levels`)).levels;
  assert(Array.isArray(levels), 'Missing level list');
  assert(levels.some((l) => l.sceneName === 'test_diy_mm1'), 'Protected test_diy_mm1 is not present; refusing this set');
  const index = await api('/catalog.json');
  const stock = [...(index.items || [])];
  for (const file of index.itemChunks || []) {
    assert(!file.includes('..') && !file.includes(':'), 'Invalid catalog chunk path');
    stock.push(...(await api(`/${file}`)).items);
  }
  const items = (await api(query('/api/custom-content/list', { setName: SET }))).items || [];
  const customCatalog = (await api(query('/api/custom-content/catalog', { setName: SET }))).items || [];
  const customRecipes = (await api(query('/api/custom-recipes', { setName: SET }))).recipes || [];
  const recipes = (await api(query('/api/recipes', { levelSet: SET }))).recipes || [];
  const ingredients = (await api(query('/api/catalog/ingredients', { levelSet: SET }))).ingredients || [];
  const materials = (await api(query('/api/catalog/floor-materials', { levelSet: SET }))).materials || [];
  const refs = await api(query('/api/custom-recipes/references', { setName: SET }));
  const options = await api(query('/api/custom-content/options', { setName: SET }));
  // The ingredient endpoint scans custom_content/ingredient only. Prep, plates,
  // and trays are also valid recipe composition SOs; resolve those from options.
  for (const ref of options.soRefs || []) {
    if (ref.assetPath.startsWith(`${ROOT}/custom_content/`) && ref.assetPath.endsWith('_SO.asset') && !ingredients.some((i) => i.guid === ref.guid)) {
      ingredients.push({ ...ref, id: ref.assetPath.split('/').pop().replace(/_SO\.asset$/, '') });
    }
  }
  const rawCache = new Map();
  const raw = async (path) => {
    assert(path.startsWith('Assets/') && !path.includes('..'), `Invalid asset path ${path}`);
    if (!rawCache.has(path)) rawCache.set(path, await api(query('/api/level/data-file', { path }), undefined, true));
    return rawCache.get(path);
  };
  return { health, levels, stock, items, customCatalog, customRecipes, recipes, ingredients, materials, refs, options, raw };
}

export function validateHandoff(ctx, handoff) {
  assert(handoff.setName === SET && Array.isArray(handoff.assets) && handoff.assets.length > 0 &&
    Array.isArray(handoff.recipes) && handoff.recipes.length > 0, 'Unexpected content handoff');
  for (const entries of [handoff.assets, handoff.recipes]) {
    assert(new Set(entries.map((e) => e.id)).size === entries.length &&
      new Set(entries.map((e) => e.guid)).size === entries.length, 'Duplicate handoff identity');
  }
  for (const expected of handoff.assets) {
    const actual = exact(ctx.items, expected.id, 'handoff asset');
    assert(actual.guid === expected.guid && actual.assetPath === expected.assetPath && actual.kind === expected.kind, `Handoff asset drift: ${expected.id}`);
    const so = [...ctx.ingredients, ...(ctx.options.soRefs || [])].find((r) => r.guid === expected.soGuid && r.assetPath === expected.soPath);
    assert(so, `Handoff SO does not resolve: ${expected.id}`);
  }
  for (const expected of handoff.recipes) {
    const actual = exact(ctx.customRecipes, expected.id, 'handoff recipe');
    assert(actual.guid === expected.guid && actual.assetPath === expected.assetPath, `Handoff recipe drift: ${expected.id}`);
  }
  // Scope this test run to the handoff, while retaining the full recipe catalog
  // for resolving nested references. Existing level orders are preserved later.
  ctx.items = ctx.items.filter((e) => handoff.assets.some((a) => a.guid === e.guid));
  ctx.customRecipes = ctx.customRecipes.filter((e) => handoff.recipes.some((r) => r.guid === e.guid));
}

async function recipeGraph(ctx, roots) {
  const recipes = new Map(ctx.recipes.concat(ctx.customRecipes.map((r) => ({ ...r, cookingStep: r.cookingStepId, platingStep: r.platingStepId }))).map((r) => [r.guid, r]));
  const ingredients = new Map(ctx.ingredients.map((r) => [r.guid, r]));
  const nodes = new Map();
  const visit = async (recipe, active = new Set()) => {
    assert(!active.has(recipe.guid), `Recipe dependency cycle at ${recipe.id}`);
    if (nodes.has(recipe.guid)) return;
    const source = await ctx.raw(recipe.assetPath);
    const node = { recipe, children: [], ingredients: [], steps: serializedGuids(source, 'cookingStepSO') };
    nodes.set(recipe.guid, node);
    const next = new Set([...active, recipe.guid]);
    for (const guid of [...serializedGuids(source, 'compositionSOs'), ...serializedGuids(source, 'optionalSOs')]) {
      if (recipes.has(guid)) {
        node.children.push(guid);
        await visit(recipes.get(guid), next);
      } else {
        assert(ingredients.has(guid), `${recipe.id}: composition GUID ${guid} not in live ingredient/recipe catalogs`);
        node.ingredients.push(guid);
      }
    }
    // Original game recipes are bundle pointers; their expanded ingredients and
    // cookingGroups come from the same knowledge API used by testLayout.ts.
    if (!serializedGuids(source, 'compositionSOs').length) {
      for (const id of recipe.ingredients || []) node.ingredients.push(exact(ctx.ingredients, id, 'ingredient').guid);
    }
  };
  for (const recipe of roots) await visit(recipe);
  return nodes;
}

function closure(graph, roots) {
  const seen = new Set();
  const visit = (guid) => {
    if (seen.has(guid)) return;
    const node = graph.get(guid);
    assert(node, `Missing recipe graph node ${guid}`);
    seen.add(guid);
    node.children.forEach(visit);
  };
  roots.forEach((r) => visit(r.guid));
  return [...seen].map((guid) => graph.get(guid));
}

export async function buildPlans(ctx, opts = {}) {
  const pending = KINDS.filter((kind) => !ctx.items.some((e) => e.kind === kind)).map((kind) => `missing custom kind: ${kind}`);
  const custom = opts.skeleton ? [] : ctx.customRecipes.filter((r) => r.assetPath.startsWith(`${ROOT}/custom_recipes/`) && !r.intermediate && r.score > 0 && !r.optionalKind);
  if (!custom.length) pending.push('no orderable custom recipes ready');
  const orders = STOCK_ORDERS.map((id) => exact(ctx.recipes, id, 'stock recipe'));
  const auxiliary = opts.skeleton ? [] : ctx.items.filter((e) => e.kind === 'splitter').map((e) => {
    const recipe = ctx.recipes.find((r) => r.guid === e.inputRecipeGuid);
    assert(recipe, `${e.id}: splitter input recipe is not available`);
    return recipe;
  });
  const allTestRecipes = opts.skeleton ? [] : ctx.customRecipes.filter((r) => !r.optionalKind);
  const graph = await recipeGraph(ctx, [...orders, ...allTestRecipes, ...auxiliary]);
  const ingredientsByGuid = new Map(ctx.ingredients.map((i) => [i.guid, i]));
  const stepByGuid = new Map((ctx.refs.cookingSteps || []).map((s) => [s.guid, s.id]));
  const ingredientFor = (e) => {
    const matches = ctx.ingredients.filter((i) => i.assetPath === e.assetPath.replace(/\.prefab$/, '_SO.asset'));
    assert(matches.length === 1, `${e.id}: expected one live ingredient SO in its directory, found ${matches.length}`);
    return matches[0];
  };
  const containerGuids = new Set((opts.skeleton ? [] : ctx.items).filter((i) => ['prep', 'tray'].includes(i.kind) || i.templateId === 'ingredient_preparation' || ['utensil_plate', 'utensil_tray'].includes(i.templateId)).map((i) => ingredientFor(i).guid));
  const splitterInputs = new Set(ctx.items.filter((i) => i.kind === 'splitter').map((i) => i.inputRecipeGuid));
  const cookingIds = (nodes) => unique(nodes.flatMap((n) => [n.recipe.cookingStep || n.recipe.cookingStepId || '',
    ...((n.recipe.type === 'Mixed' || n.recipe.mixing) ? ['MixingBowl'] : []), ...n.steps.map((g) => {
    assert(stepByGuid.has(g), `${n.recipe.id}: cooking step GUID ${g} absent from reference API`);
    return stepByGuid.get(g);
  }), ...(n.recipe.cookingGroups || []).map((g) => g.step)]).filter(Boolean));
  const classifications = new Map(custom.map((r) => {
    const nodes = closure(graph, [r]);
    const container = nodes.some((n) => splitterInputs.has(n.recipe.guid) || n.ingredients.some((g) => containerGuids.has(g))) || ['Tray', 'Glass', 'Mug'].includes(r.platingStepId);
    return [r.guid, container ? 'twdiy_containers_splitter' : cookingIds(nodes).length ? 'twdiy_cooking' : 'twdiy_basic_processing'];
  }));
  const material = ctx.materials.filter((m) => /floor|blacktiles|path/i.test(m.id) && !m.assetPath.includes('/LevelSets/')).sort((a, b) => a.assetPath.localeCompare(b.assetPath))[0];
  assert(material, 'No stock floor material available');
  const plans = [];
  for (const level of LEVELS) {
    const customOrders = custom.filter((r) => level.id === 'twdiy_export_control' || classifications.get(r.guid) === level.id ||
      (level.id === 'twdiy_containers_splitter' && closure(graph, [r]).some((n) => n.ingredients.length + n.children.length >= 2)));
    if (!customOrders.length) pending.push(`${level.id}: no custom order assigned`);
    const selected = [...orders, ...customOrders];
    const relevantContent = opts.skeleton ? [] : ctx.items.filter((e) => level.kinds.includes(e.kind));
    const nodes = closure(graph, [...selected, ...(level.id === 'twdiy_export_control' ? allTestRecipes : []), ...(level.kinds.includes('splitter') ? auxiliary : [])]);
    const items = [], supplies = [], process = [], service = [];
    const stock = (id) => exact(ctx.stock, id, 'stock prefab');
    const make = (cat, role, extra = {}) => {
      const name = `${PREFIX}${role}${cat.customKind === 'utensil' && ctx.items.some((e) => e.guid === cat.guid && e.templateId === 'utensil_hotpot') ? '_large_pot' : ''}`;
      const instanceId = `new:${cat.guid}:${level.id}:${role}`;
      const item = { instanceId, hierarchyPath: instanceId, prefabGuid: cat.guid, prefabAssetPath: cat.assetPath,
        parentPath: cat.defaultParent, displayName: name, localPosition: V(), worldPosition: V(),
        localRotationX: 0, localRotationY: 0, localRotationZ: 0, localScale: V(1, 1, 1),
        footprint: cat.footprint, walkable: false, ...extra };
      items.push(item);
      return item;
    };
    const add = (zone, id, role = id, extra = {}) => {
      const cat = stock(id);
      const item = make(cat, role, extra);
      zone.push({ item, cat });
      return item;
    };
    add(process, 'ChoppingCounter');
    add(process, 'Counter');
    add(process, 'Cooker');
    add(process, 'FryPan', 'FryPan', { stubKind: 'CookingUtensil' });
    add(process, 'Pot', 'Pot', { stubKind: 'CookingUtensil', cookingUtensil: { capacity: 3 } });
    const returns = [];
    const plating = unique(['Plate', ...selected.map((r) => r.platingStepId || r.platingStep || 'Plate'),
      ...relevantContent.filter((e) => ['ingredient', 'prep'].includes(e.kind)).map((e) => e.platingStepId || 'Plate')]);
    for (const step of plating) {
      assert(CONTAINERS[step], `Unsupported plating step ${step}; supply an explicit container mapping before applying`);
      const [stack, ret, sink] = CONTAINERS[step];
      add(service, stack, stack, { stubKind: 'CleanPlateStack', cleanPlateStack: { plateCount: 8 } });
      returns.push(add(service, ret, ret, { stubKind: ret === 'GlassReturn' ? 'GlassReturn' : 'PlateReturn', plateReturn: { returnClean: false } }));
      add(service, sink);
    }
    const serving = add(service, 'ServingStation', 'ServingStation', { stubKind: 'ServingStation' });
    const leafGuids = unique(nodes.flatMap((n) => n.ingredients));
    const suppliedGuids = new Set(leafGuids);
    for (const e of relevantContent) {
      assert(!(e.warnings || []).length, `${e.id}: ${e.warnings.join('; ')}`);
      if (['ingredient', 'prep'].includes(e.kind)) suppliedGuids.add(ingredientFor(e).guid);
      if (['utensil', 'counter', 'sink', 'tray', 'splitter'].includes(e.kind)) {
        const cat = exact(ctx.customCatalog, e.id, 'custom prefab');
        assert(cat.guid === e.guid && cat.assetPath === e.assetPath, `${e.id}: list/catalog GUID mismatch`);
        const extra = cat.customStubKind ? { stubKind: cat.customStubKind } : {};
        // Keep custom prefab settings, including sparse model arrays. Passing a
        // reconstructed list DTO would lose empty model slots (the list omits them).
        const item = make(cat, `custom_${e.id}`, extra);
        process.push({ item, cat, content: e });
        if (e.kind === 'utensil' && cat.customStubKind === 'CookingUtensil') {
          assert(e.capacity > 0 && e.stationTypes?.length, `${e.id}: invalid capacity/station types`);
          for (const type of e.stationTypes) {
            assert(HOSTS[type], `${e.id}: unsupported station ${type}`);
            if (!items.some((it) => it.prefabGuid === stock(HOSTS[type]).guid)) add(process, HOSTS[type]);
          }
        }
        if (e.kind === 'splitter') {
          assert(e.inputRecipeGuid && e.outputPrefabGuid && e.splitNum > 0, `${e.id}: incomplete splitter references`);
          assert(ctx.recipes.some((r) => r.guid === e.inputRecipeGuid), `${e.id}: input recipe GUID does not resolve`);
          assert([...ctx.ingredients, ...ctx.options.soRefs, ...ctx.options.prefabs].some((r) => r.guid === e.outputPrefabGuid), `${e.id}: output GUID does not resolve`);
        }
      }
    }
    for (const step of cookingIds(nodes)) {
      assert(STEPS[step], `Unsupported cooking step ${step}; explicit equipment mapping required`);
      for (const id of STEPS[step]) {
        if (items.some((i) => i.prefabGuid === stock(id).guid)) continue;
        add(process, id, id, UTENSILS.has(id) ? { stubKind: 'CookingUtensil' } : {});
      }
    }
    for (const guid of suppliedGuids) {
      const ing = ingredientsByGuid.get(guid);
      assert(ing && !ing.nodeOnly && ing.id !== 'TurkeySO', `Cannot safely put ingredient ${guid} in a crate`);
      const content = ctx.items.find((e) => {
        const dir = e.assetPath.slice(0, e.assetPath.lastIndexOf('/') + 1);
        return ing.assetPath.startsWith(dir) && !ing.assetPath.slice(dir.length).includes('/');
      });
      if (content && ['utensil', 'tray'].includes(content.kind)) {
        if (!items.some((i) => i.prefabGuid === content.guid)) {
          const cat = exact(ctx.customCatalog, content.id, 'custom composition item');
          process.push({ item: make(cat, `custom_${content.id}`), cat });
        }
        continue;
      }
      // The GUID here is the PseudoPrefabSO from the ingredient API, never the
      // custom-content list's prefab GUID.
      add(supplies, 'Dispenser', `crate_${ing.id}`, { stubKind: 'Dispenser', dispenser: { spawnerItemPrefabGuid: guid } });
    }
    // Mirror testLayout's direct/intermediate distinction. Only configure stock
    // vessels; custom vessels retain the asset agent's allowed/model slot pairs.
    for (const entry of process) {
      if (entry.content || !UTENSILS.has(entry.cat.id)) continue;
      const matched = nodes.filter((n) => cookingIds([n]).some((step) => STEPS[step]?.includes(entry.cat.id)));
      // LoadIngredientSOs accepts PseudoPrefabSO only. CustomRecipeSO children
      // belong in optional match items, never in a vessel's raw SO whitelist.
      const allowed = unique(matched.flatMap((n) => n.ingredients));
      if (allowed.length) entry.item.cookingUtensil = {
        capacity: Math.max(CAPACITY[entry.cat.id] || 1, ...matched.map((n) => n.ingredients.length + n.children.length)),
        allowedIngredientGuids: allowed,
      };
    }
    let maxZ = 7 * CELL;
    for (const [zone, minX] of [[supplies, -17], [process, -5], [service, 7]]) {
      let x = 0, row = 0, rowDepth = 0;
      for (const { item, cat } of zone) {
        const w = Math.max(1, Math.ceil(cat.footprint.cellsX)), d = Math.max(1, Math.ceil(cat.footprint.cellsZ));
        assert(w <= 10, `${cat.id}: footprint too wide`);
        if (x + w > 10) { x = 0; row += rowDepth + 2; rowDepth = 0; }
        // Small movable vessels are reachable on the floor; stock catalog stack
        // metadata is not applied without a matching host under the vessel.
        const pos = V(round((minX + x + w / 2) * CELL), 0, round((-7 + row + d / 2) * CELL));
        item.localPosition = { ...pos }; item.worldPosition = { ...pos };
        maxZ = Math.max(maxZ, pos.z + d * CELL / 2);
        x += w + 1; rowDepth = Math.max(rowDepth, d);
      }
    }
    for (let i = 0; i < 4; i++) {
      const player = make(stock('Player'), `player_${i + 1}`, { stubKind: 'Player', player: { playerID: 11 } });
      player.localPosition = player.worldPosition = V((-3 + i * 2) * CELL, 0, -10 * CELL);
    }
    serving.servingStation = { plateReturnInstanceIds: returns.map((r) => r.instanceId) };
    const depth = Math.ceil((maxZ + 14 * CELL) / CELL), centerZ = round((-12 * CELL + (maxZ + 2 * CELL)) / 2);
    assert(depth <= 90, `${level.id}: layout exceeds supported grid size`);
    const fid = `new:floor:${level.id}:main`;
    const floor = { instanceId: fid, hierarchyPath: fid, parentPath: 'Art/Ground', displayName: `${PREFIX}floor`,
      surfaceKind: 'solid', meshType: 'plane', meshFileId: 10209,
      materialGuid: material.guid, materialAssetPath: material.assetPath, materialName: material.id,
      localPosition: V(0, -0.05, centerZ), worldPosition: V(0, -0.05, centerZ), localRotationY: 0,
      localScale: V(36 * CELL / 10, 1, depth * CELL / 10),
      widthUnits: 36 * CELL, depthUnits: depth * CELL, widthCells: 36, depthCells: depth };
    const requiredSteps = unique(nodes.flatMap((n) => n.steps));
    const selectedSteps = new Set(closure(graph, selected).flatMap((n) => n.steps));
    assert(requiredSteps.every((g) => selectedSteps.has(g)), `${level.id}: splitter-only cooked input needs allCookingSteps API support or an orderable recipe using that input`);
    const plan = { level, items, floor, recipes: selected, nodes,
      supplyRefs: [...suppliedGuids].map((g) => ingredientsByGuid.get(g)),
      optionalGuids: unique(nodes.filter((n) => !selected.some((r) => r.guid === n.recipe.guid)).map((n) => n.recipe.guid)),
      cookingStepGuids: unique(nodes.flatMap((n) => n.steps)),
      customContent: relevantContent.map((e) => ({ id: e.id, kind: e.kind, guid: e.guid, assetPath: e.assetPath })),
      customSettings: relevantContent.filter((e) => e.kind === 'utensil' && e.stationTypes?.length),
      readme: `<p>${MARKER}</p><h2>${escapeHtml(level.title)}</h2><p>West: ingredient crates. Centre: processing and cooking. East: plating, serving and dirty returns. Four chef spawns in the south.</p><p>Stage: ${opts.skeleton ? 'stock skeleton; custom assets pending' : 'live custom assets'}. Rerun the generator to resume.</p>` };
    validateGeometry(plan);
    plans.push(plan);
  }
  return { plans, pending: unique(pending), graph };
}

export function validateGeometry(plan) {
  const names = new Set();
  const floor = plan.floor;
  for (const item of plan.items) {
    assert(!names.has(item.displayName), `Duplicate generated role ${item.displayName}`);
    names.add(item.displayName);
    const p = item.worldPosition, f = floor.worldPosition;
    assert(Math.abs(p.x - f.x) + item.footprint.cellsX * CELL / 2 <= floor.widthUnits / 2 + 0.01 &&
      Math.abs(p.z - f.z) + item.footprint.cellsZ * CELL / 2 <= floor.depthUnits / 2 + 0.01,
    `${item.displayName} outside floor`);
    for (const other of plan.items) {
      if (other === item) break;
      const q = other.worldPosition;
      assert(Math.abs(p.x - q.x) >= (item.footprint.cellsX + other.footprint.cellsX) * CELL / 2 - 0.01 ||
        Math.abs(p.z - q.z) >= (item.footprint.cellsZ + other.footprint.cellsZ) * CELL / 2 - 0.01,
      `Overlapping generated items: ${item.displayName}, ${other.displayName}`);
    }
  }
}

export function checkConnectivity(doc) {
  const floor = doc.floors.find((f) => f.displayName === `${PREFIX}floor`);
  assert(floor && Math.abs(floor.localRotationY || 0) < 0.01, 'Connectivity requires an axis-aligned solid floor');
  const step = 0.3, radius = 0.3;
  const xmin = floor.worldPosition.x - floor.widthUnits / 2, zmin = floor.worldPosition.z - floor.depthUnits / 2;
  const nx = Math.floor(floor.widthUnits / step), nz = Math.floor(floor.depthUnits / step);
  const players = doc.items.filter((i) => i.stubKind === 'Player');
  const obstacles = doc.items.filter((i) => i.stubKind !== 'Player' && i.footprint && (i.worldPosition || i.localPosition));
  const rects = obstacles.map((i) => {
    const p = i.worldPosition || i.localPosition, rot = Math.round((i.localRotationY || 0) / 90) % 2;
    return { i, x: p.x, z: p.z, hx: (rot ? i.footprint.cellsZ : i.footprint.cellsX) * CELL / 2, hz: (rot ? i.footprint.cellsX : i.footprint.cellsZ) * CELL / 2 };
  });
  const open = new Uint8Array(nx * nz), reached = new Uint8Array(nx * nz);
  const xy = (index) => ({ x: xmin + (index % nx + 0.5) * step, z: zmin + (Math.floor(index / nx) + 0.5) * step });
  for (let z = 0; z < nz; z++) for (let x = 0; x < nx; x++) {
    const index = z * nx + x, p = xy(index);
    if (x * step < radius || z * step < radius || (nx - x - 1) * step < radius || (nz - z - 1) * step < radius) continue;
    if (!rects.some((r) => Math.abs(p.x - r.x) < r.hx + radius - 0.01 && Math.abs(p.z - r.z) < r.hz + radius - 0.01)) open[index] = 1;
  }
  const playerCells = players.map((i) => {
    const p = i.worldPosition || i.localPosition;
    const x = Math.floor((p.x - xmin) / step), z = Math.floor((p.z - zmin) / step);
    assert(x >= 0 && x < nx && z >= 0 && z < nz && open[z * nx + x], `Blocked chef spawn: ${i.displayName}`);
    return z * nx + x;
  });
  assert(playerCells.length === 4, 'Expected exactly four chef spawns');
  const queue = [playerCells[0]]; reached[queue[0]] = 1;
  for (let head = 0; head < queue.length; head++) {
    const at = queue[head], x = at % nx, z = Math.floor(at / nx);
    for (const [dx, dz] of [[-1, 0], [1, 0], [0, -1], [0, 1]]) {
      const xx = x + dx, zz = z + dz, next = zz * nx + xx;
      if (xx < 0 || xx >= nx || zz < 0 || zz >= nz || !open[next] || reached[next]) continue;
      reached[next] = 1; queue.push(next);
    }
  }
  const inaccessible = rects.filter((r) => !queue.some((index) => {
    const p = xy(index), dx = Math.max(0, Math.abs(p.x - r.x) - r.hx), dz = Math.max(0, Math.abs(p.z - r.z) - r.hz);
    return dx * dx + dz * dz <= 0.85 * 0.85;
  })).map((r) => r.i.displayName);
  const result = { method: '0.3m grid, 0.3m chef clearance, footprint obstacles',
    allSpawnsConnected: playerCells.every((i) => reached[i]), accessibleObjects: rects.length - inaccessible.length,
    checkedObjects: rects.length, inaccessible, reachableAreaM2: round(queue.length * step * step) };
  assert(result.allSpawnsConnected && !inaccessible.length, `Layout connectivity failed: ${JSON.stringify(result)}`);
  return result;
}

export function reconcile(plan, current) {
  assert(current.sceneAssetPath === plan.level.scene, 'Scene path mismatch; refusing write');
  assert(!current.partLevel?.enabled, 'Generated test levels must not overwrite a part-level scene');
  const doc = structuredClone(current);
  doc.items ||= []; doc.floors ||= [];
  // A pristine generator-owned copy of s_template has two stock players and one
  // Floor. Recognize it from read-back too, so disconnects after create can resume.
  const templateCopy = !doc.items.some((i) => i.displayName?.startsWith(PREFIX)) &&
    doc.items.every((i) => i.stubKind === 'Player' && /^Player(?: ?[1-4])?$/.test(i.displayName));
  if (templateCopy) {
    doc.items = [];
    doc.floors = doc.floors.filter((f) => !(f.displayName === 'Floor' && f.parentPath === 'Art/Ground'));
  }
  const idMap = new Map();
  const replacements = new Map();
  for (const item of plan.items) {
    const hotpot = plan.customSettings?.find((c) => c.guid === item.prefabGuid && c.templateId === 'utensil_hotpot');
    const legacyHotpotName = hotpot && item.displayName === `${PREFIX}custom_${hotpot.id}_large_pot` ? `${PREFIX}custom_${hotpot.id}` : null;
    const matches = doc.items.filter((i) => i.displayName === item.displayName || (legacyHotpotName && i.displayName === legacyHotpotName && i.prefabGuid === item.prefabGuid));
    assert(matches.length <= 1, `Ambiguous generated role ${item.displayName}`);
    const old = matches[0];
    assert(!old || old.prefabGuid === item.prefabGuid, `Asset identity changed for ${item.displayName}; refusing replacement`);
    if (old) {
      if (old.displayName === legacyHotpotName) {
        assert(!doc.animControls?.groups?.length && !doc.moveControls?.groups?.length,
          'Refusing hotpot rebuild in a scene with animation bindings');
        // Omit the old root so Apply deletes it, then create the same generated
        // role through new:. Never rename an existing editor object globally.
        replacements.set(old.instanceId, item.instanceId);
        if (old.hierarchyPath) replacements.set(old.hierarchyPath, `${old.parentPath}/${item.displayName}`);
        old.instanceId = item.instanceId;
        old.hierarchyPath = item.instanceId;
        old.displayName = item.displayName;
      }
      idMap.set(item.instanceId, old.instanceId);
      const custom = plan.customSettings?.find((c) => c.guid === item.prefabGuid);
      if (custom && old.cookingUtensil) {
        old.cookingUtensil.allowedIngredientGuids = unique([...(old.cookingUtensil.allowedIngredientGuids || []), ...(custom.allowedIngredientGuids || [])]);
      }
      if (item.cookingUtensil?.allowedIngredientGuids) old.cookingUtensil = {
        ...old.cookingUtensil,
        capacity: Math.max(old.cookingUtensil?.capacity || 1, item.cookingUtensil.capacity),
        allowedIngredientGuids: unique([...(old.cookingUtensil?.allowedIngredientGuids || []), ...item.cookingUtensil.allowedIngredientGuids]),
      };
    } else {
      const added = structuredClone(item);
      // Additional stock skeleton/custom runs can pack differently. Keep saved
      // objects where they are and move only a colliding addition into free rows.
      let attempts = 0;
      while (doc.items.some((other) => {
        if (other.stubKind === 'Collision' || !other.footprint) return false;
        const q = other.worldPosition || other.localPosition, p = added.worldPosition;
        return q && Math.abs(p.y - q.y) < 0.1 &&
          Math.abs(p.x - q.x) < (added.footprint.cellsX + other.footprint.cellsX) * CELL / 2 - 0.01 &&
          Math.abs(p.z - q.z) < (added.footprint.cellsZ + other.footprint.cellsZ) * CELL / 2 - 0.01;
      })) {
        assert(++attempts < 90, `No free position for ${added.displayName}`);
        added.worldPosition.z = round(added.worldPosition.z + CELL);
        added.localPosition.z = added.worldPosition.z;
      }
      doc.items.push(added); idMap.set(item.instanceId, item.instanceId);
    }
  }
  const remap = (value) => {
    if (typeof value === 'string') return replacements.get(value) || value;
    if (Array.isArray(value)) return value.map(remap);
    if (value && typeof value === 'object') {
      for (const key of Object.keys(value)) value[key] = remap(value[key]);
    }
    return value;
  };
  if (replacements.size) remap(doc);
  // The backend splitter migration snaps roots to whole cells. Pack against
  // that final position, including splitters saved by an interrupted older run.
  for (const content of plan.customContent.filter((c) => c.kind === 'splitter')) {
    const splitter = doc.items.find((i) => i.prefabGuid === content.guid && i.displayName === `${PREFIX}custom_${content.id}`);
    if (!splitter) continue;
    const p = splitter.worldPosition;
    p.x = round(Math.round(p.x / CELL) * CELL);
    p.z = round(Math.round(p.z / CELL) * CELL);
    let attempts = 0;
    while (doc.items.some((other) => {
      if (other === splitter || other.stubKind === 'Collision' || !other.footprint) return false;
      const q = other.worldPosition || other.localPosition;
      return q && Math.abs(p.y - q.y) < 0.1 &&
        Math.abs(p.x - q.x) < (splitter.footprint.cellsX + other.footprint.cellsX) * CELL / 2 - 0.01 &&
        Math.abs(p.z - q.z) < (splitter.footprint.cellsZ + other.footprint.cellsZ) * CELL / 2 - 0.01;
    })) {
      assert(++attempts < 90, `No free position for ${splitter.displayName}`);
      p.z = round(p.z + CELL);
    }
    splitter.localPosition = { ...p };
  }
  const serving = doc.items.find((i) => i.displayName === `${PREFIX}ServingStation`);
  const plannedServing = plan.items.find((i) => i.displayName === serving.displayName);
  serving.servingStation = {
    plateReturnInstanceIds: unique([...(serving.servingStation?.plateReturnInstanceIds || []), ...plannedServing.servingStation.plateReturnInstanceIds.map((id) => idMap.get(id))]),
  };
  const expandedFloor = structuredClone(plan.floor);
  const maxZ = Math.max(expandedFloor.worldPosition.z + expandedFloor.depthUnits / 2,
    ...doc.items.filter((i) => i.displayName?.startsWith(PREFIX)).map((i) => (i.worldPosition || i.localPosition).z + i.footprint.cellsZ * CELL / 2 + CELL));
  const minZ = expandedFloor.worldPosition.z - expandedFloor.depthUnits / 2;
  expandedFloor.depthUnits = round(maxZ - minZ);
  expandedFloor.depthCells = Math.ceil(expandedFloor.depthUnits / CELL - 0.0001);
  expandedFloor.depthUnits = round(expandedFloor.depthCells * CELL);
  expandedFloor.localPosition.z = expandedFloor.worldPosition.z = (maxZ + minZ) / 2;
  expandedFloor.localScale.z = expandedFloor.depthUnits / 10;
  assert(maxZ / CELL < 48, 'Resumed layout exceeds grid limit');
  const oldFloor = doc.floors.find((f) => f.displayName === expandedFloor.displayName);
  if (oldFloor) {
    // Expand to cover additions; do not shrink a hand-adjusted/generated floor.
    const left = Math.min(oldFloor.worldPosition.x - oldFloor.widthUnits / 2, -plan.floor.widthUnits / 2);
    const right = Math.max(oldFloor.worldPosition.x + oldFloor.widthUnits / 2, plan.floor.widthUnits / 2);
    const bottom = Math.min(oldFloor.worldPosition.z - oldFloor.depthUnits / 2, expandedFloor.worldPosition.z - expandedFloor.depthUnits / 2);
    const top = Math.max(oldFloor.worldPosition.z + oldFloor.depthUnits / 2, expandedFloor.worldPosition.z + expandedFloor.depthUnits / 2);
    oldFloor.worldPosition = V((left + right) / 2, oldFloor.worldPosition.y, (bottom + top) / 2);
    oldFloor.localPosition = { ...oldFloor.worldPosition };
    oldFloor.widthCells = Math.ceil((right - left) / CELL - 0.0001);
    oldFloor.depthCells = Math.ceil((top - bottom) / CELL - 0.0001);
    oldFloor.widthUnits = round(oldFloor.widthCells * CELL); oldFloor.depthUnits = round(oldFloor.depthCells * CELL);
    oldFloor.localScale = V(oldFloor.widthUnits / 10, 1, oldFloor.depthUnits / 10);
  } else doc.floors.push(expandedFloor);
  if (doc.cameraInfo) doc.cameraInfo.fieldOfView = 56;
  assert(templateCopy || current.items.every((i) => doc.items.some((j) => (replacements.get(i.instanceId) || i.instanceId) === j.instanceId)), 'Existing item unexpectedly removed');
  const owned = doc.items.filter((i) => i.displayName?.startsWith(PREFIX));
  validateGeometry({ items: owned, floor: doc.floors.find((f) => f.displayName === plan.floor.displayName) });
  checkConnectivity(doc);
  return doc;
}

function identity(level, summary, detail) {
  assert(summary.sceneName === level.id && summary.sceneAssetPath === level.scene, `Unexpected existing scene for ${level.id}`);
  assert(detail.levelName === level.name, `${level.id} is not generator-owned; refusing modification`);
  assert(detail.levelInfoAssetPath === summary.assetPath && detail.sceneAssetPath === level.scene, 'LevelInfo/scene binding mismatch');
}

export async function verifyLevel(api, plan, infoPath, previous, recipeGuids, expected) {
  const layout = await api(query('/api/scene/layout', { assetPath: plan.level.scene }));
  const recipes = await api(query('/api/level-recipes', { assetPath: plan.level.scene }));
  assert(layout.sceneAssetPath === plan.level.scene && recipes.levelInfoAssetPath === infoPath, 'Read-back scene binding mismatch');
  for (const item of plan.items) {
    const actual = layout.items.filter((i) => i.displayName === item.displayName);
    assert(actual.length === 1 && actual[0].prefabGuid === item.prefabGuid, `Read-back missing/duplicate asset: ${item.displayName}`);
    if (item.dispenser) assert(actual[0].dispenser?.spawnerItemPrefabGuid === item.dispenser.spawnerItemPrefabGuid, `Crate SO mismatch: ${item.displayName}`);
    if (item.player) assert(actual[0].stubKind === 'Player', `Missing spawn stub: ${item.displayName}`);
    if (expected) {
      const wanted = expected.items.find((i) => i.displayName === item.displayName);
      const pos = actual[0].worldPosition, target = wanted.worldPosition;
      assert(pos && ['x', 'y', 'z'].every((axis) => Math.abs(pos[axis] - target[axis]) < 0.03), `Saved position drifted: ${item.displayName}; expected ${JSON.stringify(target)}, actual ${JSON.stringify(pos)}`);
      if (wanted.cookingUtensil?.allowedIngredientGuids) {
        assert(wanted.cookingUtensil.allowedIngredientGuids.every((g) => actual[0].cookingUtensil?.allowedIngredientGuids?.includes(g)), `Utensil ingredient dependency dropped: ${item.displayName}`);
      }
    }
  }
  for (const old of previous.items || []) {
    assert(layout.items.some((i) => i.instanceId === old.instanceId && i.prefabGuid === old.prefabGuid), `Existing scene item disappeared: ${old.hierarchyPath}`);
  }
  for (const content of plan.customSettings || []) {
    const item = layout.items.find((i) => i.prefabGuid === content.guid);
    const settings = item?.cookingUtensil;
    assert(settings && settings.capacity >= content.capacity, `Custom utensil capacity lost: ${content.id}`);
    for (const field of ['allowedIngredientGuids', 'stationTypes']) {
      assert((content[field] || []).every((g) => settings[field]?.includes(g)), `Custom utensil ${field} lost: ${content.id}`);
    }
    // List API omits empty slots; compare nonempty model order while preserving
    // scene DTO sparse slots on subsequent saves.
    for (const field of ['modelGuids', 'modelSoGuids']) {
      assert(JSON.stringify((settings[field] || []).filter(Boolean)) === JSON.stringify(content[field] || []), `Custom utensil model pairing lost: ${content.id}`);
    }
  }
  const floor = layout.floors.find((f) => f.displayName === plan.floor.displayName);
  assert(floor && !floor.airFloor && floor.widthUnits >= plan.floor.widthUnits - 0.02 && floor.depthUnits >= plan.floor.depthUnits - 0.02, 'Generated solid floor missing or undersized');
  assert(layout.walkable?.some((w) => Math.abs(w.cx - floor.worldPosition.x) < 0.03 && Math.abs(w.cz - floor.worldPosition.z) < 0.03 && w.sx >= floor.widthUnits - 0.03 && w.sz >= floor.depthUnits - 0.03), 'Floor collision not regenerated');
  const serving = layout.items.find((i) => i.displayName === `${PREFIX}ServingStation`);
  const returnIds = serving.servingStation?.plateReturnInstanceIds || [];
  assert(returnIds.length && returnIds.every((id) => layout.items.some((i) => i.instanceId === id && ['PlateReturn', 'GlassReturn'].includes(i.stubKind))), 'Broken serving/return references');
  assert(recipeGuids.every((g) => recipes.recipeGuids.includes(g)), 'Saved recipe GUIDs were dropped');
  const info = await api(query('/api/level/data-file', { path: infoPath }), undefined, true);
  assert(recipeGuids.every((g) => serializedGuids(info, 'recipes').includes(g)), 'Disk LevelInfo recipes mismatch');
  assert(plan.optionalGuids.every((g) => serializedGuids(info, 'optionalRecipeMatchListItems').includes(g)), 'Disk optional match items missing intermediate/splitter recipes');
  assert(plan.cookingStepGuids.every((g) => serializedGuids(info, 'allCookingSteps').includes(g)), 'Disk allCookingSteps missing recursive recipe dependencies');
  const customSupplies = (plan.supplyRefs || []).filter((r) => r.assetPath.startsWith(`${ROOT}/custom_content/`) &&
    layout.items.some((i) => i.dispenser?.spawnerItemPrefabGuid === r.guid));
  assert(customSupplies.every((r) => serializedGuids(info, 'allIngredients').includes(r.guid)), 'Disk allIngredients missing custom crate/prep SOs');
  const bundles = await api(query('/api/level/bundles', { assetPath: infoPath }));
  const dependencyDiagnosticNotes = [];
  let missing = bundles.missing || [];
  if (missing.includes('assetPath:')) {
    let directAssetEvidence = false;
    for (const ref of plan.supplyRefs || []) {
      if (!ref.assetPath.startsWith(`${ROOT}/custom_content/`)) continue;
      const source = await api(query('/api/level/data-file', { path: ref.assetPath }), undefined, true);
      if (/^  bundleName:[ \t]*\r?\n  assetPath:[ \t]*\r?\n  asset: \{[^\n]*guid: [a-f0-9]{32}/m.test(source)) {
        directAssetEvidence = true; break;
      }
    }
    assert(directAssetEvidence, 'Unexplained assetPath: dependency; no direct-asset SO evidence');
    missing = missing.filter((b) => b !== 'assetPath:');
    dependencyDiagnosticNotes.push('API false positive assetPath:; BundleNameFromAsset regex crosses empty bundleName newline; direct asset reference verified');
  }
  assert(!missing.length, `Missing registered bundle dependencies: ${missing.join(', ')}`);
  return { level: plan.level.id, scene: plan.level.scene, items: layout.items.length,
    customContent: plan.customContent.length, recipes: recipes.recipeGuids.length,
    recipeIds: recipes.recipeIds, optionalRecipeCount: (recipes.optionalItems || []).length,
    customSupplies: plan.customContent.filter((e) => ['ingredient', 'prep'].includes(e.kind)).map((e) => {
      const ingDir = e.assetPath.slice(0, e.assetPath.lastIndexOf('/') + 1);
      const supplied = layout.items.find((i) => i.dispenser && plan.items.some((p) => p.displayName === i.displayName && p.dispenser?.spawnerItemPrefabGuid === i.dispenser.spawnerItemPrefabGuid) &&
        plan.supplyRefs?.some((s) => s.guid === i.dispenser.spawnerItemPrefabGuid && s.assetPath.startsWith(ingDir)));
      assert(supplied, `Custom supply missing: ${e.id}`);
      return { id: e.id, crate: supplied.displayName, soGuid: supplied.dispenser.spawnerItemPrefabGuid };
    }),
    connectivity: checkConnectivity(layout),
    dependencyDiagnosticNotes,
    cookingSteps: serializedGuids(info, 'allCookingSteps'), dependencies: bundles.current, verified: true };
}

export async function run(opts, api = apiClient(opts), log = console.log) {
  const ctx = await loadContext(api);
  if (opts.handoff && !opts.skeleton) validateHandoff(ctx, opts.handoff);
  const { plans, pending } = await buildPlans(ctx, opts);
  const summary = { mode: opts.verifyOnly ? 'verify-only' : opts.apply ? opts.skeleton ? 'apply-stock-skeleton' : 'apply-custom' : 'dry-run',
    set: SET, protectedLevel: 'test_diy_mm1', pendingAssets: pending,
    levels: plans.map((p) => ({ id: p.level.id, scene: p.level.scene, items: p.items.length, floorCells: [p.floor.widthCells, p.floor.depthCells],
      recipes: p.recipes.map((r) => ({ id: r.id, guid: r.guid })), customContent: p.customContent })) };
  // Inspect existing destinations even in dry-run. Never open the protected map.
  for (const plan of plans) {
    const existing = ctx.levels.find((l) => l.sceneName === plan.level.id);
    if (existing) identity(plan.level, existing, await api(query('/api/level', { assetPath: existing.assetPath })));
  }
  log(JSON.stringify(summary, null, 2));
  if (opts.verifyOnly) {
    const results = [];
    for (const plan of plans) {
      const level = ctx.levels.find((l) => l.sceneName === plan.level.id);
      assert(level, `Missing generated level ${plan.level.id}`);
      const result = await verifyLevel(api, plan, level.assetPath, { items: [] }, plan.recipes.map((r) => r.guid));
      const registered = await api(query('/api/level-recipes', { assetPath: plan.level.scene }));
      if (plan.level.id === 'twdiy_export_control' && opts.handoff) {
        const covered = new Set([...registered.recipeGuids, ...(registered.optionalItems || []).map((r) => r.guid)]);
        assert(opts.handoff.recipes.every((r) => covered.has(r.guid)), `Full regression does not register all ${opts.handoff.recipes.length} handoff recipes`);
        result.handoffRecipesRegistered = opts.handoff.recipes.length;
      }
      results.push(result); log(JSON.stringify(result));
    }
    return { ...summary, results };
  }
  if (!opts.apply) return summary;
  assert(opts.skeleton || !pending.length, `Custom assets not ready: ${pending.join('; ')}. Wait for the asset agent, or explicitly use --skeleton.`);
  const protectedPaths = unique(ctx.levels.filter((l) => !LEVELS.some((p) => p.id === l.sceneName)).flatMap((l) => [l.assetPath, l.sceneAssetPath]).filter(Boolean));
  const before = new Map();
  for (const path of protectedPaths) before.set(path, hash(await ctx.raw(path)));
  const results = [];
  try {
    for (const plan of plans) {
      let levels = (await api(`/api/sets/${SET}/levels`)).levels;
      let existing = levels.find((l) => l.sceneName === plan.level.id);
      if (!existing) {
        log(`Creating ${plan.level.id}`);
        await api('/api/level/create', { setName: SET, levelId: plan.level.id, levelName: plan.level.name, levelNameZH: plan.level.title });
        levels = (await api(`/api/sets/${SET}/levels`)).levels;
        existing = levels.find((l) => l.sceneName === plan.level.id);
        assert(existing?.hasScene, `Create did not persist ${plan.level.id}; inspect Unity, do not recreate blindly`);
      }
      const detail = await api(query('/api/level', { assetPath: existing.assetPath }));
      identity(plan.level, existing, detail);
      const previousRecipes = await api(query('/api/level-recipes', { assetPath: plan.level.scene }));
      const recipeGuids = unique([...(previousRecipes.recipeGuids || []), ...plan.recipes.map((r) => r.guid)]);
      assert(previousRecipes.levelInfoAssetPath === detail.levelInfoAssetPath, 'Recipe target is not the generated LevelInfo');
      log(`${plan.level.id}: saving ${recipeGuids.length} recipes and recursive dependencies`);
      await api('/api/level-recipes', { levelInfoAssetPath: detail.levelInfoAssetPath, recipeGuids });
      if (plan.optionalGuids.length) {
        const refreshedRecipes = await api(query('/api/level-recipes', { assetPath: plan.level.scene }));
        await api('/api/level/optional-items', { levelInfoAssetPath: detail.levelInfoAssetPath,
          guids: unique([...(refreshedRecipes.optionalItems || []).map((e) => e.guid), ...plan.optionalGuids]) });
      }
      // Recipe saves can rebuild preview objects. Read their current IDs only
      // after those saves, immediately before reconciling the layout.
      const current = await api(query('/api/scene/layout', { assetPath: plan.level.scene }));
      const doc = reconcile(plan, current);
      // Grid is used by interactive objects, not empty floor margins. Avoid an
      // unnecessary GridBake.SaveScene on a preview scene: upstream save guards
      // display a blocking Prepare For Building dialog on that path.
      const gridHalfSizeX = Math.max(detail.gridHalfSizeX, Math.ceil(Math.max(...doc.items.map((i) => Math.abs(i.worldPosition.x) + i.footprint.cellsX * CELL / 2)) / CELL - 0.0001));
      const gridHalfSizeZ = Math.max(detail.gridHalfSizeZ, Math.ceil(Math.max(...doc.items.map((i) => Math.abs(i.worldPosition.z) + i.footprint.cellsZ * CELL / 2)) / CELL - 0.0001));
      const freshDetail = await api(query('/api/level', { assetPath: detail.levelInfoAssetPath }));
      log(`${plan.level.id}: saving grid and order configuration`);
      if (freshDetail.gridHalfSizeX !== gridHalfSizeX || freshDetail.gridHalfSizeZ !== gridHalfSizeZ || freshDetail.minOrderCount !== 2 || freshDetail.maxOrderCount !== 4 || freshDetail.levelNameZH !== plan.level.title) {
        await api('/api/level/info', { ...freshDetail, assetPath: detail.levelInfoAssetPath, levelNameZH: plan.level.title, gridHalfSizeX, gridHalfSizeZ, minOrderCount: 2, maxOrderCount: 4 });
      }
      const configs = {};
      for (let i = 0; i < 4; i++) {
        assert(detail.configs?.[i]?.exists, `Missing ${i + 1}P config for ${plan.level.id}`);
        configs[`config_${i + 1}p`] = { ...detail.configs[i], roundTime: 1800, orderLifeTime: 600, timeBetweenOrders: 8, plateReturnTime: 3 };
      }
      if (Object.entries(configs).some(([key, value]) => {
        const current = detail.configs[Number(key.match(/\d/)[0]) - 1];
        return Object.keys(value).some((field) => value[field] !== current[field]);
      })) await api('/api/level/config', { assetPath: detail.levelInfoAssetPath, ...configs });
      log(`${plan.level.id}: saving ${doc.items.length} items and floor collisions`);
      const result = await api('/api/scene/layout?snap=0.01&syncWalkable=1', doc);
      assert(!result.warnings?.length, `Write-back warnings: ${result.warnings.join('; ')}. Inspect saved scene before resuming.`);
      const readme = await api(query('/api/level/readme', { assetPath: detail.levelInfoAssetPath }));
      // Ownership is carried by CreateLevel's name, so even a disconnect before
      // readme-save is recoverable. Keep any user-authored notes on later runs.
      if (!readme.exists || readme.html?.includes(MARKER)) await api('/api/level/readme-save', { assetPath: detail.levelInfoAssetPath, html: plan.readme });
      const retained = { items: current.items.filter((i) => doc.items.some((j) => j.instanceId === i.instanceId)) };
      const verified = await verifyLevel(api, plan, detail.levelInfoAssetPath, retained, recipeGuids, doc);
      results.push(verified);
      log(JSON.stringify(verified));
    }
  } finally {
    for (const [path, digest] of before) {
      const after = await api(query('/api/level/data-file', { path }), undefined, true);
      assert(hash(after) === digest, `Protected existing asset changed: ${path}`);
    }
    const after = (await api(`/api/sets/${SET}/levels`)).levels;
    for (const old of ctx.levels) assert(after.some((l) => l.assetPath === old.assetPath && l.sceneAssetPath === old.sceneAssetPath), `Existing level removed: ${old.sceneName}`);
  }
  log(JSON.stringify({ stage: opts.skeleton ? 'stock-skeleton' : 'custom', verifiedLevels: results.length,
    protectedAssetsUnchanged: true, pendingAssets: pending }));
  return { ...summary, results, protectedAssetsUnchanged: true };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const opts = argumentsOf(process.argv.slice(2));
    if (!opts.help && !opts.skeleton) opts.handoff = JSON.parse(await readFile(new URL('../tmp/tester_web_diy/handoff.json', import.meta.url), 'utf8'));
    if (opts.help) console.log('Usage: node gen-tester-web-diy-levels.mjs [--apply | --verify-only] [--skeleton] [--base-url http://10.211.55.3:8765] [--timeout-ms 120000]\nDefault: dry-run. --apply --skeleton creates stock facilities first; rerun --apply after all seven custom kinds and orders are ready.');
    else {
      const result = await run(opts);
      if (opts.apply) await writeFile(new URL('../tmp/tester_web_diy/levels-result.json', import.meta.url), JSON.stringify({ verifiedAt: new Date().toISOString(), ...result }, null, 2) + '\n');
      if (opts.verifyOnly) await writeFile(new URL('../tmp/tester_web_diy/levels-verification.json', import.meta.url), JSON.stringify({ verifiedAt: new Date().toISOString(), ...result }, null, 2) + '\n');
    }
  } catch (e) {
    console.error(e.stack || e.message);
    process.exitCode = 1;
  }
}
