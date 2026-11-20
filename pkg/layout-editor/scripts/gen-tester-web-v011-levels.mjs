#!/usr/bin/env node
/** Append owned v0.11 maps through Unity. POSTs are never retried.
 * Default dry-run only reads catalogs/list/health, without opening any scene.
 * Artifacts and the immutable protection baseline live in tmp/tester_web_v011.
 */
import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';
import { apiClient, serializedGuids } from './gen-tester-web-diy-levels.mjs';
import { LEVELS, buildLayout, validateLayout, renderSvg } from './lib/tester-web-v011-layouts.mjs';
import { ensureSandwich } from './lib/tester-web-v011-recipes.mjs';

const SET = 'tester_web';
const MARKER = '[gen-tester-web-v011:v1] ';
const GROUP = 'v0.11\u9053\u5177';
const OUT = new URL('../tmp/tester_web_v011/', import.meta.url);
const assert = (ok, message) => { if (!ok) throw new Error(message); };
const query = (path, params) => `${path}?${new URLSearchParams(params)}`;
const digest = text => createHash('sha256').update(text).digest('hex');
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const unique = values => [...new Set(values)];
const sceneOf = id => `Assets/LevelSets/${SET}/scenes/${id}.unity`;
const save = (name, value) => writeFile(new URL(name, OUT), JSON.stringify(value, null, 2) + '\n');
async function saved(name) {
  try { return JSON.parse(await readFile(new URL(name, OUT), 'utf8')); }
  catch (error) { if (error.code === 'ENOENT') return null; throw error; }
}
function exact(rows, id) {
  const found = new Map(rows.filter(row => row.id === id).map(row => [row.guid, row]));
  assert(found.size === 1 && [...found.values()][0].guid, `Missing/ambiguous catalog ID: ${id}`);
  return [...found.values()][0];
}

export function argumentsOf(argv) {
  const opts = { apply: false, verifyOnly: false, baseUrl: 'http://10.211.55.3:8765', timeout: 120000 };
  let mode;
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (['--apply', '--dry-run', '--verify-only'].includes(arg)) {
      assert(!mode || mode === arg, 'Choose only one of --dry-run, --apply, --verify-only');
      mode = arg; opts.apply = arg === '--apply'; opts.verifyOnly = arg === '--verify-only';
    } else if (arg === '--help' || arg === '-h') opts.help = true;
    else if (['--base-url', '--timeout-ms', '--level'].includes(arg)) {
      assert(argv[i + 1] && !argv[i + 1].startsWith('--'), `${arg} requires a value`);
      const value = argv[++i];
      if (arg === '--base-url') opts.baseUrl = value;
      if (arg === '--timeout-ms') opts.timeout = Number(value);
      if (arg === '--level') opts.level = value;
    } else throw new Error(`Unknown argument: ${arg}`);
  }
  assert(Number.isFinite(opts.timeout) && opts.timeout >= 1000, 'Invalid timeout');
  const url = new URL(opts.baseUrl);
  assert(['http:', 'https:'].includes(url.protocol), 'Expected HTTP(S) base URL');
  opts.baseUrl = url.origin;
  assert(!opts.level || LEVELS.some(level => level.id === opts.level), `Unknown level: ${opts.level}`);
  return opts;
}

export async function loadContext(api) {
  const health = await api('/api/health');
  assert(health.ok, 'Unity is not ready');
  const levels = (await api(`/api/sets/${SET}/levels`)).levels;
  assert(Array.isArray(levels), 'Missing level list');
  const catalog = await api('/catalog.json');
  const stock = [...(catalog.items || [])];
  for (const file of catalog.itemChunks || []) {
    assert(!file.includes('..') && !file.includes(':'), `Invalid catalog chunk: ${file}`);
    stock.push(...(await api('/' + file)).items);
  }
  const ingredients = (await api(`/api/catalog/ingredients?levelSet=${SET}`)).ingredients;
  const materials = (await api(`/api/catalog/floor-materials?levelSet=${SET}`)).materials;
  const recipes = (await api(`/api/recipes?levelSet=${SET}`)).recipes;
  const custom = (await api(`/api/custom-recipes?setName=${SET}`)).recipes || [];
  const refs = await api(`/api/custom-recipes/references?setName=${SET}`);
  // Include reference-only cooking/plating/intermediate entries without replacing
  // the expanded cookingGroups supplied by the main recipe catalog.
  for (const row of [...custom, ...(refs.recipes || []), ...(refs.platingSteps || []), ...(refs.platingContainers || [])]) {
    if (!recipes.some(recipe => recipe.id === row.id)) recipes.push(row);
  }
  return { health, levels, stock, ingredients, materials, recipes, refs };
}

function identity(level, summary, detail) {
  assert(summary && summary.sceneName === level.id && summary.sceneAssetPath === sceneOf(level.id), `Scene binding mismatch: ${level.id}`);
  assert(detail.levelName === MARKER + level.id, `Unowned target: ${level.id}`);
  assert(detail.levelInfoAssetPath === summary.assetPath && detail.sceneAssetPath === summary.sceneAssetPath && detail.gameMode === level.mode,
    `LevelInfo/mode binding mismatch: ${level.id}`);
}

export function completePlayers(current, ctx) {
  const doc = structuredClone(current), cat = exact(ctx.stock, 'Player');
  const players = doc.items.filter(item => item.prefabGuid === cat.guid);
  assert(players.length <= 4, `Unexpected Player count: ${players.length}`);
  for (let i = players.length; i < 4; i++) {
    const instanceId = `new:${cat.guid}:v011:player_${i + 1}`;
    doc.items.push({ instanceId, hierarchyPath: instanceId, prefabGuid: cat.guid, prefabAssetPath: cat.assetPath,
      parentPath: cat.defaultParent, displayName: `Player ${i + 1}`, stubKind: 'Player', player: { playerID: 11 },
      localPosition: { x: 0, y: 0, z: 0 }, worldPosition: { x: 0, y: 0, z: 0 },
      localRotationX: 0, localRotationY: 0, localRotationZ: 0, localScale: { x: 1, y: 1, z: 1 },
      footprint: structuredClone(cat.footprint), walkable: false });
  }
  return doc;
}

export function hordePlan(fresh, ctx) {
  assert(fresh.gameMode === 'Horde' && fresh.hordeConfigs?.length === 4, 'Expected four Horde configs');
  const doc = structuredClone(fresh);
  const enemies = ['unbread_a', 'unbread_b', 'unbread_c', 'horde_biscuit_a', 'horde_candy_b', 'horde_puddin_c'];
  const schedules = [
    [[[0, 0]], [[1, 0], [2, 6], [3, 12], [4, 18], [5, 24]]],
    [[[0, 0], [1, 5]], [[2, 0], [3, 5], [4, 10], [5, 15], [0, 20], [3, 25]]],
    [[[0, 0], [1, 4], [2, 8], [3, 12]], [[4, 0], [5, 4], [0, 8], [1, 12], [2, 16], [3, 20]]],
    [enemies.map((_, i) => [i, i * 3]), enemies.map((_, i) => [i, i * 3])],
  ];
  const menu = LEVELS.find(level => level.mode === 'Horde').recipeIds.map(id => exact(ctx.recipes, id).guid);
  doc.hordeConfigs.forEach((config, p) => {
    config.waves = schedules[p].map((events, w) => ({ ...(config.waves?.[w] || {}), id: `v011_${p + 1}p_wave_${w + 1}`,
      intervalSeconds: 10, recipeGuids: w === 0 ? menu.slice(0, 1) : menu,
      enemyGuids: events.map(([index]) => exact(fresh.enemies, enemies[index]).guid), spawnTimes: events.map(([, time]) => time) }));
  });
  return doc;
}

const CONFIG = { roundTime: 240, orderLifeTime: 180, timeBetweenOrders: 15, plateReturnTime: 5,
  oneStarScore: 20, twoStarScore: 60, threeStarScore: 120, fourStarScore: 180 };
export function campaignConfigs(detail) {
  assert(detail.configs?.length === 4 && detail.configs.every(config => config.exists), 'Missing Campaign player configs');
  return Object.fromEntries(detail.configs.map((config, i) => {
    assert(Number.isFinite(config.survivalTimeMultiplier), 'Missing survivalTimeMultiplier');
    return [`config_${i + 1}p`, { ...CONFIG, survivalTimeMultiplier: config.survivalTimeMultiplier }];
  }));
}

export function verifyReadback(desired, actual) {
  assert(actual.sceneAssetPath === desired.sceneAssetPath, 'Readback scene mismatch');
  const wanted = [...desired.items, ...(desired.floors || [])], got = [...actual.items, ...(actual.floors || [])];
  assert(wanted.length === got.length, `Readback root count differs: wanted ${wanted.length}, got ${got.length}`);
  const matched = new Map(), used = new Set(), ids = new Map();
  for (const item of wanted) {
    // Existing roots are moved but not renamed by SceneLayoutApplier. Unity IDs
    // may also change after a scene reopen; hierarchyPath is the persisted key.
    const candidates = got.filter(candidate => !used.has(candidate) &&
      (candidate.instanceId === item.instanceId || candidate.hierarchyPath === item.hierarchyPath || candidate.displayName.replace(/_BL\d+$/, '') === item.displayName));
    assert(candidates.length === 1, `Readback identity ambiguous/missing: ${item.displayName}`);
    const found = candidates[0];
    assert(found, `Readback missing name: ${item.displayName}`);
    used.add(found); matched.set(item.instanceId, found);
    ids.set(item.instanceId, found.instanceId);
  }
  const remap = value => typeof value === 'string' ? ids.get(value) || value : Array.isArray(value) ? value.map(remap) :
    value && typeof value === 'object' ? Object.fromEntries(Object.entries(value).map(([key, val]) => [key, remap(val)])) : value;
  const compare = (expected, found, path) => {
    if (typeof expected === 'number') assert(Number.isFinite(found) && Math.abs(expected - found) <= 0.03, `${path}: expected ${expected}, actual ${found}`);
    else if (Array.isArray(expected)) {
      assert(Array.isArray(found) && expected.length === found.length, `${path}: array length mismatch`);
      expected.forEach((value, i) => compare(value, found[i], `${path}[${i}]`));
    } else if (expected && typeof expected === 'object') {
      for (const [key, value] of Object.entries(expected)) compare(value, found?.[key], `${path}.${key}`);
    } else assert(expected === found, `${path}: expected ${JSON.stringify(expected)}, actual ${JSON.stringify(found)}`);
  };
  for (const item of wanted) {
    const found = matched.get(item.instanceId);
    for (const field of ['prefabGuid', 'pseudoPrefabGuid', 'stubKind', 'worldPosition', 'localRotationX', 'localRotationY', 'localRotationZ',
      'dispenser', 'player', 'kettle', 'switchStub', 'pressureSwitch', 'conveyor', 'cleanPlateStack', 'plateReturn', 'servingStation', 'lockable',
      'soArray', 'widthUnits', 'depthUnits']) {
      if (item[field] !== undefined && !(field === 'pseudoPrefabGuid' && item[field] && !found[field])) {
        if (field === 'conveyor' && item.displayName === 'V011__conveyor' && found.prefabGuid === 'bb223a6f8672d664ba56afc60936e34a') {
          // The stock ConveyorStation has no serve/direction-sync tag fields;
          // its exported stub booleans are prefab-derived rather than persisted.
          const expected = { ...remap(item[field]), serve: found[field]?.serve, buttonControlled: found[field]?.buttonControlled };
          compare(expected, found[field], `${item.displayName}.${field}`);
        } else compare(remap(item[field]), found[field], `${item.displayName}.${field}`);
      }
    }
    if (item.cookingUtensil) {
      for (const [key, value] of Object.entries(item.cookingUtensil)) {
        // Fill helpers may append dependencies; sparse cosmetic slots stay exact.
        if (key === 'allowedIngredientGuids') assert(value.every(guid => found.cookingUtensil?.[key]?.includes(guid)), `${item.displayName}: dropped utensil SO`);
        else compare(value, found.cookingUtensil?.[key], `${item.displayName}.cookingUtensil.${key}`);
      }
    }
  }
  for (const field of ['switchLinks', 'animControls']) compare(remap(desired[field]), actual[field], field);
  const buttonLinks = value => ({ ...value, links: (value?.links || []).map(link => {
    const { id, ...semantic } = link;
    return semantic;
  }) });
  compare(remap(buttonLinks(desired.buttonLinks)), buttonLinks(actual.buttonLinks), 'buttonLinks');
  for (const floor of desired.floors || []) {
    const found = matched.get(floor.instanceId);
    assert(found.materialName?.replace(/_tiling\d+x\d+$/, '') === floor.materialName, `${floor.displayName}: floor material base mismatch`);
    assert(found.materialTilingW === floor.widthCells && found.materialTilingD === floor.depthCells,
      `${floor.displayName}: floor tiling mismatch`);
    assert(actual.walkable?.some(w => Math.abs(w.cx - found.worldPosition.x) < 0.03 && Math.abs(w.cz - found.worldPosition.z) < 0.03 &&
      w.sx >= floor.widthUnits - 0.03 && w.sz >= floor.depthUnits - 0.03), `Missing/undersized floor collision: ${floor.displayName}`);
  }
  // Validate the real readback. Never substitute desired floors or coordinates.
  return validateLayout(actual);
}

async function protectionSnapshot(api, levels) {
  const snapshot = {};
  for (const level of levels) {
    const raw = path => api(query('/api/level/data-file', { path }), undefined, true);
    const group = await api(query('/api/level/group', { assetPath: level.assetPath }));
    const groupPath = level.assetPath.slice(0, level.assetPath.lastIndexOf('/')) + '/group~/group.json';
    snapshot[level.sceneName] = { assetPath: level.assetPath, sceneAssetPath: level.sceneAssetPath,
      scene: digest(await raw(level.sceneAssetPath)), info: digest(await raw(level.assetPath)),
      group: group.exists ? digest(await raw(groupPath)) : null };
  }
  return snapshot;
}

export async function run(opts, transport = apiClient(opts), log = console.log) {
  // Covers writes made by ensureSandwich too. A warning response is persisted
  // before stopping; no subsequent mutation is allowed in this invocation.
  const api = async (path, body, text) => {
    const result = await transport(path, body, text);
    if (body !== undefined && result.warnings?.length) {
      await save('write-warning.json', { path, warnings: result.warnings, at: new Date().toISOString() });
      if (body.sceneAssetPath) {
        const readback = await transport(query('/api/scene/layout', { assetPath: body.sceneAssetPath }));
        await save('write-warning-readback.json', readback);
      }
      throw new Error(`Write warnings at ${path}: ${result.warnings.join('; ')}. Paused; inspect readback before resuming.`);
    }
    return result;
  };
  const ctx = await loadContext(api);
  const selected = LEVELS.filter(level => !opts.level || opts.level === level.id);
  const summary = { mode: opts.verifyOnly ? 'verify-only' : opts.apply ? 'apply' : 'dry-run', set: SET,
    levels: selected.map(level => ({ ...level, exists: ctx.levels.some(row => row.sceneName === level.id) })) };
  for (const level of selected) for (const id of level.recipeIds) {
    if (!id.startsWith('Web_V011_')) exact(ctx.recipes, id);
  }
  if (!opts.apply && !opts.verifyOnly) {
    log(JSON.stringify(summary, null, 2));
    return summary;
  }
  await mkdir(OUT, { recursive: true });
  const protectedLevels = ctx.levels.filter(row => !LEVELS.some(level => level.id === row.sceneName));
  assert(protectedLevels.length === 7, `Expected seven protected old levels; got ${protectedLevels.length}`);
  const before = await protectionSnapshot(api, protectedLevels);
  let baseline = await saved('protected-baseline.json');
  if (!baseline) {
    assert(!opts.verifyOnly, 'Missing protection baseline; run --apply first');
    baseline = { set: SET, baseUrl: opts.baseUrl, createdAt: new Date().toISOString(), assets: before };
    await writeFile(new URL('protected-baseline.json', OUT), JSON.stringify(baseline, null, 2) + '\n', { flag: 'wx' });
  }
  assert(baseline.set === SET && baseline.baseUrl === opts.baseUrl && same(baseline.assets, before), 'Protected baseline drift before run; refusing writes');
  const report = { ...summary, startedAt: new Date().toISOString(), results: [], play: 'not executed', export: 'not executed', runtime: 'not executed' };
  let failure;
  try {
    // Check ownership of every existing destination before any mutation.
    for (const level of selected) {
      const row = ctx.levels.find(item => item.sceneName === level.id);
      if (row) identity(level, row, await api(query('/api/level', { assetPath: row.assetPath })));
      else assert(!opts.verifyOnly, `Missing target: ${level.id}`);
    }
    for (const level of selected) {
      log(`${level.id}: ${opts.verifyOnly ? 'verify' : 'create/menu/layout'}`);
      let levels = (await api(`/api/sets/${SET}/levels`)).levels;
      let row = levels.find(item => item.sceneName === level.id);
      if (!row) {
        await api('/api/level/create', { setName: SET, levelId: level.id, levelName: MARKER + level.id, levelNameZH: level.title, gameMode: level.mode });
        levels = (await api(`/api/sets/${SET}/levels`)).levels;
        row = levels.find(item => item.sceneName === level.id);
        assert(row?.hasScene, `Create not confirmed: ${level.id}; inspect list before resuming`);
      }
      let detail = await api(query('/api/level', { assetPath: row.assetPath }));
      identity(level, row, detail);
      const originalGrid = Object.fromEntries(['X', 'Y', 'Z'].map(axis => [axis, detail[`gridHalfSize${axis}`]]));
      const originalHordeConfigs = level.mode === 'Horde' ? structuredClone(detail.configs) : null;
      if (opts.apply) {
        const existingDoc = await api(query('/api/scene/layout', { assetPath: row.sceneAssetPath }));
        if (existingDoc.items.some(item => item.displayName.startsWith('V011__'))) {
          const expectedNames = new Set([...existingDoc.items, ...(existingDoc.floors || [])].filter(item => item.displayName.startsWith('V011__')).map(item => item.displayName.replace(/_BL\d+$/, '')));
          const minimum = level.id === 'v011_tea' ? ['serve', 'return', 'floor', 'kettle_1', 'kettle_2']
            : level.id === 'v011_mechanism' ? ['serve', 'return', 'floor', 'pressure', 'condiment', 'condiment_button'] : ['serve', 'return', 'floor'];
          for (const role of minimum) assert(expectedNames.has(`V011__${role}`), `Partial owned scene detected: ${level.id}; inspect before resuming`);
        }
      }
      if (opts.apply && level.id === 'v011_mechanism') {
        await ensureSandwich(api, ctx, { apply: true });
        const fresh = await loadContext(api);
        ctx.recipes = fresh.recipes;
      }
      const menu = level.recipeIds.map(id => exact(ctx.recipes, id).guid);
      let expectedMode;
      if (level.mode === 'Horde') {
        const fresh = await api(query('/api/level/mode-config', { assetPath: row.assetPath }));
        expectedMode = hordePlan(fresh, ctx);
        if (opts.apply) {
          await save(`${level.id}-desired-mode.json`, expectedMode);
          await api('/api/level/mode-config', expectedMode);
        }
        const read = await api(query('/api/level/mode-config', { assetPath: row.assetPath }));
        await save(`${level.id}-readback-mode.json`, read);
        for (const key of Object.keys(expectedMode)) assert(same(expectedMode[key], read[key]), `Horde readback mismatch: ${key}`);
      } else if (opts.apply) {
        await api('/api/level-recipes', { levelInfoAssetPath: row.assetPath, recipeGuids: menu });
        if (level.id === 'v011_mechanism') {
          const fresh = await api(query('/api/level-recipes', { assetPath: row.sceneAssetPath }));
          await api('/api/level/optional-items', { levelInfoAssetPath: row.assetPath,
            guids: unique([...(fresh.optionalItems || []).map(item => item.guid), exact(ctx.recipes, 'Web_V011_SandwichAssembly').guid]) });
        }
      }
      const current = await api(query('/api/scene/layout', { assetPath: row.sceneAssetPath }));
      let desired = opts.apply ? buildLayout(level.id, ctx, completePlayers(current, ctx)) : await saved(`${level.id}-desired.json`);
      assert(desired, `Missing desired artifact for ${level.id}; cannot verify intended positions`);
      validateLayout(desired);
      await save(`${level.id}-desired.json`, desired);
      await writeFile(new URL(`${level.id}-desired.svg`, OUT), renderSvg(desired, level.title));
      if (opts.apply) await api('/api/scene/layout?snap=0.01&syncWalkable=1', desired);
      let actual = await api(query('/api/scene/layout', { assetPath: row.sceneAssetPath }));
      // Unity retains native Player prefab names; bind desired roles to the
      // actual player roots by their prefab identity and stable player ID.
      const actualPlayers = actual.items.filter(item => item.prefabGuid === exact(ctx.stock, 'Player').guid);
      const desiredPlayers = desired.items.filter(item => item.prefabGuid === exact(ctx.stock, 'Player').guid).sort((a, b) => a.player.playerID - b.player.playerID);
      for (const player of desiredPlayers) {
        const found = actualPlayers.find(item => item.player?.playerID === player.player.playerID);
        if (found) player.hierarchyPath = found.hierarchyPath;
      }
      await save(`${level.id}-desired.json`, desired);
      await save(`${level.id}-readback.json`, actual);
      await writeFile(new URL(`${level.id}-readback.svg`, OUT), renderSvg(actual, level.title));
      const geometry = verifyReadback(desired, actual);
      if (opts.apply) {
        detail = await api(query('/api/level', { assetPath: row.assetPath }));
        // Campaign template configs may still resolve to shared template assets
        // in some editor builds; do not mutate those through /api/level/config.
        // Send only the info DTO, retaining fresh grid/dependency values.
        const fresh = await api(query('/api/level', { assetPath: row.assetPath }));
        const info = Object.fromEntries(['levelName', 'levelNameZH', 'debugRecipeCount', 'disableDynamicParenting', 'gridHalfSizeX', 'gridHalfSizeY', 'gridHalfSizeZ', 'dependencies', 'minOrderCount', 'maxOrderCount'].map(key => [key, fresh[key]]));
        await api('/api/level/info', { ...info, assetPath: row.assetPath, levelNameZH: level.title, minOrderCount: 2, maxOrderCount: 4 });
        await api('/api/level/readme-save', { assetPath: row.assetPath,
          html: `<p>${MARKER + level.id}</p><h2>${level.title}</h2><p>Four chef spawns. Menu: ${level.recipeIds.join(', ')}.</p><p>API readback only; Play, export and host/client validation not executed. Sandwich order icons are intentionally omitted.</p>` });
        await api('/api/level/group-save', { setName: SET, items: [{ levelId: level.id, assetPath: row.assetPath, group: GROUP }] });
        // Layout GET activates this target immediately before camera capture.
        actual = await api(query('/api/scene/layout', { assetPath: row.sceneAssetPath }));
        assert(actual.sceneAssetPath === row.sceneAssetPath, 'Capture target is not active');
        const shot = await api('/api/level/screenshot-capture', { assetPath: row.assetPath, width: 1600, height: 900, quality: 90 });
        assert(shot.base64, 'Empty screenshot payload');
        const base64 = shot.base64.replace(/^data:image\/jpeg;base64,/, '');
        const bytes = Buffer.from(base64, 'base64');
        assert(bytes[0] === 0xff && bytes[1] === 0xd8, 'Screenshot is not JPEG');
        await writeFile(new URL(`${level.id}.jpg`, OUT), bytes);
        const upload = await api('/api/level/screenshot-upload', { assetPath: row.assetPath, fileName: `${level.id}.jpg`, base64 });
        const screenshotDetail = await api(query('/api/level', { assetPath: row.assetPath }));
        assert(upload.texturePath && screenshotDetail.hasScreenshot && screenshotDetail.screenshotPath === upload.texturePath, 'Screenshot upload readback mismatch');
      }
      detail = await api(query('/api/level', { assetPath: row.assetPath }));
      identity(level, row, detail);
      assert(detail.levelNameZH === level.title && detail.minOrderCount === 2 && detail.maxOrderCount === 4, 'Info readback mismatch');
      for (const axis of ['X', 'Y', 'Z']) assert(detail[`gridHalfSize${axis}`] === originalGrid[axis], `Grid ${axis} changed`);
      if (originalHordeConfigs) assert(same(detail.configs, originalHordeConfigs), 'Horde per-player scalar configs changed');
      if (level.mode === 'Campaign') {
        assert(detail.configs?.length === 4 && detail.configs.every(config => config.exists), 'Campaign template configs missing');
        const recipes = await api(query('/api/level-recipes', { assetPath: row.sceneAssetPath }));
        assert(same([...recipes.recipeGuids].sort(), [...menu].sort()), 'Campaign menu is not exact');
        if (level.id === 'v011_mechanism') assert(recipes.optionalItems.some(item => item.guid === exact(ctx.recipes, 'Web_V011_SandwichAssembly').guid), 'Missing sandwich optional');
      }
      const raw = await api(query('/api/level/data-file', { path: row.assetPath }), undefined, true);
      if (level.mode === 'Campaign') assert(same(serializedGuids(raw, 'recipes').sort(), [...menu].sort()), 'Disk Campaign menu mismatch');
      const registeredSteps = serializedGuids(raw, 'allCookingSteps');
      const visited = new Set();
      const inspectRecipe = async recipe => {
        if (visited.has(recipe.guid)) return;
        visited.add(recipe.guid);
        assert(recipe.assetPath?.startsWith('Assets/'), `Missing recipe asset path: ${recipe.id}`);
        const source = await api(query('/api/level/data-file', { path: recipe.assetPath }), undefined, true);
        for (const guid of serializedGuids(source, 'cookingStepSO')) assert(registeredSteps.includes(guid), `Unregistered cooking step for ${recipe.id}: ${guid}`);
        for (const guid of [...serializedGuids(source, 'compositionSOs'), ...serializedGuids(source, 'optionalSOs')]) {
          const child = ctx.recipes.find(item => item.guid === guid);
          if (child) await inspectRecipe(child);
        }
      };
      for (const id of level.recipeIds) await inspectRecipe(exact(ctx.recipes, id));
      const ingredientGuids = serializedGuids(raw, 'allIngredients');
      for (const item of actual.items.filter(item => item.stubKind === 'Dispenser' && item.dispenser?.spawnerItemPrefabGuid)) {
        const ingredient = ctx.ingredients.find(row => row.guid === item.dispenser.spawnerItemPrefabGuid);
        if (ingredient?.assetPath?.startsWith('Assets/LevelSets/'))
          assert(ingredientGuids.includes(item.dispenser.spawnerItemPrefabGuid), `Unregistered crate SO: ${item.displayName}`);
      }
      if (level.id === 'v011_tea') assert(ingredientGuids.includes(exact(ctx.ingredients, 'HotWaterSO').guid), 'Missing HotWater ingredient');
      const bundles = await api(query('/api/level/bundles', { assetPath: row.assetPath }));
      assert(!(bundles.missing || []).length, `Missing dependencies: ${(bundles.missing || []).join(', ')}`);
      const group = await api(query('/api/level/group', { assetPath: row.assetPath }));
      assert(group.group === GROUP, 'Group readback mismatch');
      const readme = await api(query('/api/level/readme', { assetPath: row.assetPath }));
      assert(readme.html?.includes(MARKER + level.id), 'Readme readback mismatch');
      const result = { id: level.id, geometry, recipes: level.recipeIds, cookingSteps: serializedGuids(raw, 'allCookingSteps'),
        dependencies: bundles.current, verified: true };
      await save(`${level.id}-report.json`, result);
      report.results.push(result);
      log(`${level.id}: verified ${actual.items.length} items`);
    }
    if (opts.apply) {
      const levels = (await api(`/api/sets/${SET}/levels`)).levels;
      const items = [];
      for (const level of LEVELS) {
        const row = levels.find(item => item.sceneName === level.id);
        if (!row) continue;
        identity(level, row, await api(query('/api/level', { assetPath: row.assetPath })));
        items.push({ levelId: level.id, assetPath: row.assetPath, group: GROUP });
      }
      await api('/api/level/group-save', { setName: SET, items });
      for (const item of items) assert((await api(query('/api/level/group', { assetPath: item.assetPath }))).group === GROUP, `Final group mismatch: ${item.levelId}`);
    }
  } catch (error) { failure = error; report.error = error.message; }
  finally {
    try {
      const levels = (await api(`/api/sets/${SET}/levels`)).levels;
      for (const row of protectedLevels) assert(levels.some(item => item.sceneName === row.sceneName && item.assetPath === row.assetPath && item.sceneAssetPath === row.sceneAssetPath), `Protected level removed: ${row.sceneName}`);
      const after = await protectionSnapshot(api, protectedLevels);
      await save('protected-final.json', after);
      assert(same(before, after) && same(baseline.assets, after), 'Protected old scene/info/group hashes changed');
      report.protectedAssetsUnchanged = true;
    } catch (error) {
      report.protectionError = error.message;
      failure = failure ? new AggregateError([failure, error], 'Generation and protection checks failed') : error;
    }
    report.finishedAt = new Date().toISOString();
    await save('report.json', report);
  }
  if (failure) throw failure;
  return report;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const opts = argumentsOf(process.argv.slice(2));
    if (opts.help) console.log('Usage: node gen-tester-web-v011-levels.mjs [--dry-run | --apply | --verify-only] [--level ID] [--base-url URL] [--timeout-ms 120000]\nDefault: GET-only catalogs/list, no scene opening. Apply is sequential and stops on warnings. Resume by rerunning after inspecting saved state; POSTs are never retried.');
    else await run(opts);
  } catch (error) { console.error(error.stack || error.message); process.exitCode = 1; }
}
