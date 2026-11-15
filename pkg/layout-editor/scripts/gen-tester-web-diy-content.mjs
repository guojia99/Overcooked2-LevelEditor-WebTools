#!/usr/bin/env node
// Unity owns all prefab/SO/.meta writes. This client only authors source art and HTTP requests.
import fs from 'node:fs/promises';
import path from 'node:path';
import http from 'node:http';
import https from 'node:https';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { contentPlan, recipePlan, makeVisual, lookupContentIds, lookupCompositionIds,
  normalizeLookupRow, includeLookupRows, mergeHandoff } from './lib/tester-web-diy-assets.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const sourceDir = path.join(root, 'Docs/test/assets/tester_web_diy');
const setName = 'tester_web_diy';
const assetRoot = `Assets/LevelSets/${setName}/`;
const args = process.argv.slice(2);
const flags = new Set(['--apply', '--dry-run', '--sources-only', '--verify-only', '--help']);
const valued = new Set(['--base-url', '--output-dir', '--timeout-ms']);
const cli = {};
for (let i = 0; i < args.length; i++) {
  const arg = args[i];
  if (flags.has(arg)) cli[arg.slice(2)] = true;
  else if (valued.has(arg) && args[i + 1] && !args[i + 1].startsWith('--')) cli[arg.slice(2)] = args[++i];
  else throw new Error(`Unknown or incomplete argument: ${arg}`);
}
if (cli.help) {
  console.log('Usage: node layout-editor/scripts/gen-tester-web-diy-content.mjs [--apply | --verify-only | --sources-only] [--base-url URL] [--output-dir DIR] [--timeout-ms 120000]\nDefault: offline dry-run, no HTTP or filesystem writes. --sources-only authors art, no Unity writes. --apply resumes from live readback.');
  process.exit(0);
}
if ([cli.apply, cli['verify-only'], cli['sources-only'], cli['dry-run']].filter(Boolean).length > 1)
  throw new Error('Choose one execution mode.');
const baseUrl = (cli['base-url'] || 'http://10.211.55.3:8765').replace(/\/$/, '');
if (!['http:', 'https:'].includes(new URL(baseUrl).protocol)) throw new Error('Expected HTTP(S) base URL.');
const timeout = Number(cli['timeout-ms'] || 120000);
if (!Number.isFinite(timeout) || timeout < 1000) throw new Error('Invalid timeout.');
const outputDir = path.resolve(root, cli['output-dir'] || 'layout-editor/tmp/tester_web_diy');
if (outputDir === sourceDir || outputDir.startsWith(sourceDir + path.sep)) throw new Error('Reports must stay outside source art.');
const content = contentPlan(), recipes = recipePlan();
const hash = value => createHash('sha256').update(typeof value === 'string' || Buffer.isBuffer(value) ? value : JSON.stringify(value)).digest('hex');
const equal = (a, b) => JSON.stringify(a ?? null) === JSON.stringify(b ?? null);
const art = new Map([...content, ...recipes].map(p => [p.id, makeVisual(p.id, p.shape, p.colour)]));
const sourceManifest = { schemaVersion: 1, generator: 'gen-tester-web-diy-content.mjs',
  planSha256: hash({ content, recipes }),
  content: content.map(p => ({ ...p, sourceSha256: art.get(p.id).sha256 })),
  recipes: recipes.map(p => ({ ...p, sourceSha256: art.get(p.id).sha256 })),
  files: [...art].flatMap(([id, visual]) => visual.files.map(f => ({ path: `${id}/${f.fileName}`, bytes: f.bytes.length, sha256: hash(f.bytes) }))) };

if (!cli.apply && !cli['verify-only'] && !cli['sources-only']) {
  console.log(JSON.stringify({ mode: 'dry-run', setName, baseUrl, contentCount: content.length, recipeCount: recipes.length,
    templateCount: new Set(content.map(p => p.templateId)).size, kinds: [...new Set(content.map(p => p.kind))],
    recipeTypes: [...new Set(recipes.map(p => p.type))], sourceFileCount: sourceManifest.files.length,
    planSha256: sourceManifest.planSha256, content: content.map(p => ({ id: p.id, kind: p.kind, templateId: p.templateId })),
    recipes: recipes.map(p => ({ id: p.id, type: p.type, comps: p.comps, cookingStepId: p.step || '' })) }, null, 2));
  process.exit(0);
}

async function writeJson(file, value) {
  const temporary = file + '.tmp';
  await fs.writeFile(temporary, JSON.stringify(value, null, 2) + '\n');
  await fs.rename(temporary, file);
}
async function writeSources() {
  await fs.mkdir(sourceDir, { recursive: true });
  for (const [id, visual] of art) {
    await fs.mkdir(path.join(sourceDir, id), { recursive: true });
    for (const f of visual.files) await fs.writeFile(path.join(sourceDir, id, f.fileName), f.bytes);
  }
  await writeJson(path.join(sourceDir, 'sources.manifest.json'), sourceManifest);
}
if (cli['sources-only']) {
  await writeSources();
  console.log(JSON.stringify({ mode: 'sources-only', sourceDir, files: sourceManifest.files.length, planSha256: sourceManifest.planSha256 }));
  process.exit(0);
}

await fs.mkdir(outputDir, { recursive: true });
const reportFile = path.join(outputDir, cli['verify-only'] ? 'verification.json' : 'manifest.json');
const checkpointFile = path.join(outputDir, 'checkpoint.json');
let checkpoint = { schemaVersion: 1, setName, operations: {} };
try { checkpoint = JSON.parse(await fs.readFile(checkpointFile, 'utf8')); }
catch (e) { if (e.code !== 'ENOENT') throw e; }
if (checkpoint.setName !== setName || checkpoint.schemaVersion !== 1) throw new Error('Unexpected checkpoint owner/version.');
const report = { schemaVersion: 1, setName, baseUrl, mode: cli.apply ? 'apply' : 'verify-only',
  startedAt: new Date().toISOString(), planSha256: sourceManifest.planSha256, status: 'running',
  assets: [], recipes: [], operations: [], validation: [], gaps: [
    { code: 'tray-lookup-authoring', status: 'implemented-live-readback-verified', detail: 'traySlots supports three owned slot lists. Generator appends missing required rows and preserves existing rows and limits; runtime acceptance remains untested.' },
    { code: 'prep-restriction-authoring', status: 'implemented-live-readback-verified', detail: 'lookupRows supports persistent prep restriction/cosmetics mappings; both original prep assets retain seven rows. Runtime transfer remains untested.' },
    { code: 'utensil-recipe-type', status: 'unsupported', detail: 'There is no Utensil recipe type. Item is accepted by the API but requires ItemPropertiesComponent/ItemOrderNode at runtime; custom plate/tray templates have neither. Composition resolution also searches custom_content/ingredient only, excluding utensil SOs. Plate/tray Item orders are deliberately omitted rather than creating unplayable recipes.' },
    { code: 'cooked-subrecipe-whitelist', status: 'unsupported', detail: 'allowedIngredientGuids resolves PseudoPrefabSO only, not CustomRecipeSO. A mixed-subrecipe-to-roasting order cannot be whitelisted with this API; the plan uses three raw red cuts instead. Mixed pair orders require stock mixing equipment.' },
    { code: 'setup-component-resolution', status: 'resolved-live-readback-verified', detail: 'Backend now resolves SetupCustom types. Sink washing/drying paths and custom clean stack binding are verified.' },
    { code: 'recipe-bundle-registration', status: 'resolved-live-readback-verified', detail: 'Existing custom_recipes bundle assignment is repaired; verified from folder meta over HTTP.' },
    { code: 'fbx-channel', status: 'not-tested', detail: 'Only OBJ + MTL + PNG source/import channels were exercised; no FBX validation claimed.' },
    { code: 'runtime-scope', status: 'not-tested', detail: 'This generator does not create/open/save levels, enter Play, export bundles, or test in-game multiplayer. HTTP asset/readback checks are not runtime acceptance.' },
  ] };
let lastProgress = 0;
function progress(message) {
  lastProgress = Date.now();
  console.log(`[${new Date().toISOString()}] ${message}`);
}
const heartbeat = setInterval(() => { if (Date.now() - lastProgress >= 45000) progress(`working: ${report.operations.length} operations; ${report.validation.length} checks`); }, 45000);
heartbeat.unref();
async function save() {
  report.updatedAt = new Date().toISOString();
  report.partialAssets = items.filter(i => content.some(p => p.id === i.id));
  report.partialRecipes = liveRecipes.filter(i => recipes.some(p => p.id === i.id));
  await writeJson(reportFile, report);
  if (cli.apply) await writeJson(checkpointFile, checkpoint);
}

function singleRequest(endpoint, payload, binary = false) {
  return new Promise((resolve, reject) => {
    const url = new URL(endpoint, baseUrl), data = payload === undefined ? null : Buffer.from(JSON.stringify(payload));
    const transport = url.protocol === 'https:' ? https : http;
    const req = transport.request(url, { method: data ? 'POST' : 'GET', headers: data ?
      { 'Content-Type': 'application/json; charset=utf-8', 'Content-Length': data.length } : {} }, response => {
      const chunks = []; let length = 0;
      response.on('data', chunk => {
        length += chunk.length;
        if (length > 32 * 1024 * 1024) req.destroy(new Error('Response exceeds 32 MiB.'));
        else chunks.push(chunk);
      });
      response.on('error', reject);
      response.on('end', () => {
        clearTimeout(timer);
        const bytes = Buffer.concat(chunks);
        if (binary && response.statusCode === 200) return resolve(bytes);
        let body;
        try { body = JSON.parse(bytes.toString('utf8').replace(/^\uFEFF/, '')); }
        catch { return reject(new Error(`Non-JSON HTTP ${response.statusCode}: ${endpoint}`)); }
        if (response.statusCode >= 400 || body.ok === false || body.error)
          return reject(new Error(`HTTP ${response.statusCode} ${endpoint}: ${body.error || 'ok=false'}`));
        resolve(body);
      });
    });
    const timer = setTimeout(() => req.destroy(new Error(`Request timeout after ${timeout}ms: ${endpoint}`)), timeout);
    req.on('error', e => { clearTimeout(timer); reject(e); });
    req.end(data);
  });
}
async function request(endpoint, payload, binary = false) {
  for (let attempt = 0; ; attempt++) {
    try { return await singleRequest(endpoint, payload, binary); }
    catch (e) {
      if (payload !== undefined || attempt >= 3 || /^HTTP |^Non-JSON/.test(e.message)) throw e;
      progress(`read reconnect ${attempt + 1}/3: ${endpoint}`);
      await new Promise(resolve => setTimeout(resolve, 2000 * (attempt + 1)));
    }
  }
}
const query = (endpoint, params) => endpoint + '?' + new URLSearchParams(params);
const listContent = async () => (await request(query('/api/custom-content/list', { setName }))).items || [];
const listRecipes = async () => ((await request(query('/api/custom-recipes', { setName }))).recipes || [])
  .filter(r => r.assetPath.startsWith(assetRoot + 'custom_recipes/'));
let items = [], liveRecipes = [], options = {}, refs = {}, ingredients = [];
let persistedIconIds = new Set();

function check(label, passed, detail = '') {
  report.validation.push({ label, passed: !!passed, detail });
  if (!passed) progress(`FAIL ${label}: ${detail}`);
  return !!passed;
}
function assert(value, message) { if (!value) throw new Error(message); return value; }
function one(rows, predicate, label) {
  const matches = (rows || []).filter(predicate);
  return assert(matches.length === 1 && matches[0], `${label}: expected one match, found ${matches.length}`);
}
const contentById = id => one(items, x => x.id === id, id);
const recipeById = id => one(liveRecipes, x => x.id === id, id);
async function loadOptions() { options = await request(query('/api/custom-content/options', { setName })); }
function soFor(id) {
  const item = contentById(id), expected = item.assetPath.replace(/\.prefab$/, '_SO.asset');
  return one(options.soRefs, r => r.assetPath === expected, `SO ${id}`);
}
function modelFor(id) {
  const item = contentById(id), dir = path.posix.dirname(item.assetPath);
  return one(options.modelPrefabs, r => r.assetPath === `${dir}/models/${id}_Model.prefab`, `model ${id}`);
}
function compositionFor(id) {
  if (id.startsWith('@')) return recipeById(`tweb_r_${id.slice(1)}`).id;
  const so = soFor(id);
  const fromCatalog = ingredients.filter(r => r.assetPath === so.assetPath);
  return fromCatalog.length === 1 ? fromCatalog[0].id : so.id;
}
function reference(rows, id, label) {
  // FindAssetByIdInFolders uses common01 -> common02 -> common03 precedence.
  const matches = (rows || []).filter(r => r.id === id).sort((a, b) => a.assetPath < b.assetPath ? -1 : a.assetPath > b.assetPath ? 1 : 0);
  return assert(matches[0], `${label} ${id}: not returned by references`);
}
const uploadFiles = id => art.get(id).files.filter(f => !f.fileName.endsWith('_Icon.png'))
  .map(f => ({ fileName: f.fileName, base64: f.bytes.toString('base64') }));
const iconBase64 = id => art.get(id).files.find(f => f.fileName.endsWith('_Icon.png')).bytes.toString('base64');

async function mutate(key, endpoint, dto, readback, valid) {
  assert(cli.apply, 'Mutation attempted outside --apply.');
  const entry = { key, endpoint, requestSha256: hash(dto), status: 'sending' };
  report.operations.push(entry);
  checkpoint.operations[key] = entry;
  await save();
  let writeError;
  try { await request(endpoint, dto); }
  catch (e) { writeError = e; }
  // Always read before deciding whether a failed/ambiguous request can be resumed.
  let read;
  try { read = await readback(); }
  catch (e) {
    entry.status = 'uncertain'; entry.error = writeError?.message || 'Readback failed'; entry.readbackError = e.message;
    await save(); throw new Error(`${key}: uncertain write; readback failed: ${e.message}. Rerun only after service recovery.`);
  }
  if (!valid(read)) {
    entry.status = 'failed'; entry.error = writeError?.message || 'Readback mismatch';
    await save(); throw new Error(`${key}: ${entry.error}. Live state re-read; no automatic write retry.`);
  }
  entry.status = writeError ? 'recovered-by-readback' : 'verified';
  if (writeError) entry.responseError = writeError.message;
  await save();
  return read;
}

function contentFields(p) {
  const fields = { nameZh: p.nameZh, nameEn: p.nameEn, modelScale: 1 };
  if (p.nextId) fields.nextPrefabGuid = contentById(p.nextId).guid;
  if (p.plating) fields.platingStepId = reference(refs.platingContainers, p.plating, 'plating').id;
  if (p.station) Object.assign(fields, { capacity: p.capacity, stationTypes: [p.station],
    allowedIngredientGuids: ['test_ingredient_workable_cut', 'tweb_blue_cut'].map(id => soFor(id).guid),
    modelGuids: ['test_ingredient_workable_cut', 'tweb_blue_cut'].map(id => modelFor(id).guid), modelSoGuids: [] });
  if (p.kind === 'sink' || p.templateId === 'counter_sink') Object.assign(fields, {
    cleanPlateStackGuid: soFor('tweb_clean_plate_stack').guid, washingPartPath: 'WashingPart', dryingPartPath: 'DryingPart' });
  if (p.kind === 'splitter') Object.assign(fields, { capacity: p.capacity || 8, splitNum: p.splitNum || 2,
    inputRecipeGuid: recipeById('tweb_r_split_batch').guid, outputPrefabGuid: soFor('test_ingredient_workable_cut').guid,
    splitAudioTag: (options.audioTags || []).includes('PutDown') ? 'PutDown' : '' });
  return fields;
}
function fieldsMatch(item, fields) {
  return Object.entries(fields).every(([key, value]) => key === 'splitAudioTag' && !value ||
    (Array.isArray(value) && value.length === 0 ? (item[key] || []).length === 0 : equal(item[key], value)));
}
function contentValid(p, row, fields) {
  return row && row.templateId === p.templateId && row.kind === p.kind && row.hasModel && row.iconUrl &&
    (!(p.kind === 'ingredient' || p.kind === 'prep') || row.nodePath && row.orderUid > 0) &&
    !(row.warnings || []).length && fieldsMatch(row, fields);
}
async function ensureContent(p) {
  const before = items.find(i => i.id === p.id);
  if (before) assert(before.kind === p.kind && before.templateId === p.templateId && before.assetPath.startsWith(assetRoot), `Identity conflict: ${p.id}`);
  const fields = contentFields(p);
  let sameArt = false;
  if (contentValid(p, before, fields)) {
    const files = art.get(p.id).files;
    const icon = await request(query('/api/custom-content/icon', { assetPath: before.assetPath }), undefined, true);
    sameArt = hash(icon) === hash(files[3].bytes);
    if (sameArt) for (const f of files.slice(0, 3)) {
      const bytes = await request(query('/api/custom-content/model-file', { assetPath: before.assetPath, file: f.fileName }), undefined, true);
      if (hash(bytes) !== hash(f.bytes)) sameArt = false;
    }
  }
  if (sameArt && persistedIconIds.has(p.id)) { progress(`reuse asset ${p.id}`); return; }
  if (sameArt) {
    await mutate(`content-icon-index:${p.id}`, '/api/custom-content/update',
      { ...fields, setName, id: p.id, kind: p.kind, templateId: p.templateId, assetPath: before.assetPath,
        orderUid: before.orderUid || 0, iconBase64: iconBase64(p.id) },
      async () => { items = await listContent(); return items.find(i => i.id === p.id); },
      row => contentValid(p, row, fields) && row.guid === before.guid);
    progress(`repaired persisted icon index ${p.id}`);
    return;
  }
  const dto = { setName, id: p.id, kind: p.kind, templateId: p.templateId, ...fields,
    orderUid: before?.orderUid || 0, iconBase64: iconBase64(p.id), modelFiles: uploadFiles(p.id) };
  if (before) dto.assetPath = before.assetPath;
  let after = await mutate(`content:${p.id}`, `/api/custom-content/${before ? 'update' : 'create'}`, dto,
    async () => { items = await listContent(); return items.find(i => i.id === p.id); },
    row => contentValid(p, row && { ...row, iconUrl: row.iconUrl || 'pending-index-repair' }, fields) && (!before || before.guid === row.guid));
  const configAfter = (await request(query('/api/level/data-file', { path: `${assetRoot}custom_content/Config.asset` }), undefined, true)).toString('utf8');
  const persistedEntry = configAfter.split(/\n  - id: /).slice(1).find(b => b.startsWith(p.id + '\n')) || '';
  if (!after.iconUrl || !persistedEntry.includes(`iconPath: ${assetRoot}custom_content/${p.kind}/${p.id}/${p.id}_Icon.png`)) {
    const guid = after.guid;
    after = await mutate(`content-icon-index:${p.id}`, '/api/custom-content/update',
      { ...fields, setName, id: p.id, kind: p.kind, templateId: p.templateId, assetPath: after.assetPath,
        orderUid: after.orderUid || 0, iconBase64: iconBase64(p.id) },
      async () => { items = await listContent(); return items.find(i => i.id === p.id); },
      row => contentValid(p, row, fields) && row.guid === guid);
  }
  progress(`${before ? 'updated' : 'created'} asset ${p.id} ${after.guid}`);
}

function recipeFields(p) {
  const fields = { setName, recipeName: p.id, nameZh: p.nameZh, nameEn: p.id, category: 'tester_web_diy',
    score: p.score, type: p.type, compositionIds: p.comps.map(compositionFor),
    cookingProgress: 1, mixingProgress: 1, modelScale: 1 };
  if (p.step) fields.cookingStepId = reference(refs.cookingSteps, p.step, 'cooking step').id;
  if (p.mixIcon) fields.mixingIconId = reference(refs.icons, p.mixIcon, 'mixing icon').id;
  if (p.type !== 'Item') fields.platingStepId = reference(refs.platingContainers, p.plating || 'Plate', 'plating').id;
  return fields;
}
function recipeFieldsMatch(row, fields) {
  return row && ['type', 'score', 'nameZh', 'nameEn', 'compositionIds', 'cookingStepId', 'platingStepId', 'mixingIconId']
    .every(k => equal(row[k] || (k === 'score' ? 0 : ''), fields[k] || (k === 'score' ? 0 : '')));
}
async function ensureRecipe(p) {
  const fields = recipeFields(p), before = liveRecipes.find(r => r.id === p.id);
  if (before) assert(before.assetPath.startsWith(`${assetRoot}custom_recipes/tester_web_diy/`), `Recipe ownership conflict: ${p.id}`);
  if (!recipeFieldsMatch(before, fields)) {
    if (before) fields.assetPath = before.assetPath;
    await mutate(`recipe:${p.id}`, `/api/custom-recipes/${before ? 'update' : 'create'}`, fields,
      async () => { liveRecipes = await listRecipes(); return liveRecipes.find(r => r.id === p.id); },
      row => recipeFieldsMatch(row, fields) && (!before || before.guid === row.guid));
  }
  let current = recipeById(p.id);
  if (!current.hasModel || !current.previewable) {
    const result = await mutate(`recipe-model:${p.id}`, '/api/custom-recipes/upload-model',
      { setName, recipeAssetPath: current.assetPath, files: uploadFiles(p.id) },
      async () => { liveRecipes = await listRecipes(); return liveRecipes.find(r => r.id === p.id); },
      row => row?.hasModel && row.previewable && row.guid === current.guid);
    current = result;
  }
  if (!current.hasIcon || current.iconState !== 'own') {
    await mutate(`recipe-icon:${p.id}`, '/api/custom-recipes/upload-icon',
      { setName, recipeAssetPath: current.assetPath, fileName: `${p.id}_Icon.png`, base64: iconBase64(p.id) },
      async () => { liveRecipes = await listRecipes(); return liveRecipes.find(r => r.id === p.id); },
      row => row?.hasIcon && row.iconState === 'own' && row.guid === current.guid);
  }
  progress(`recipe ready ${p.id} ${recipeById(p.id).guid}`);
}

async function requiredLookupRows(rawOnly = false) {
  const result = [];
  for (const id of lookupCompositionIds.slice(0, rawOnly ? 2 : undefined)) {
    if (id.startsWith('@')) {
      const recipe = recipeById(`tweb_r_${id.slice(1)}`);
      const raw = (await request(query('/api/level/data-file', { path: recipe.assetPath }), undefined, true)).toString('utf8');
      const modelGuid = raw.match(/^  model:\s*\{[^}]*guid:\s*([a-f0-9]{32})/mi)?.[1];
      assert(modelGuid, `Missing cooked model for lookup: ${recipe.id}`);
      result.push(normalizeLookupRow({ recipeGuid: recipe.guid, modelGuid, amountAllowed: 1 }));
    } else result.push(normalizeLookupRow({ recipeGuid: soFor(id).guid, modelGuid: modelFor(id).guid, amountAllowed: 1 }));
  }
  return result;
}

async function ensureLookups() {
  for (const id of lookupContentIds) {
    const before = contentById(id), p = content.find(p => p.id === id);
    const required = await requiredLookupRows(p.rawOnly);
    if (p.rawOnly && before.lookupRows?.some(r => required.some(x => x.recipeGuid === r.recipeGuid)))
      assert(before.lookupRows.every(r => required.some(x => x.recipeGuid === r.recipeGuid)),
        `${id}: unexpected extra row in raw-only control; preserving live lookup and stopping instead of deleting it`);
    const fields = before.kind === 'tray' || before.templateId === 'utensil_tray'
      ? { traySlots: Array.from({ length: 3 }, (_, i) => ({ rows: includeLookupRows(before.traySlots?.[i]?.rows, required) })) }
      : { lookupRows: p.rawOnly ? required : includeLookupRows(before.lookupRows, required) };
    const normalize = row => fields.traySlots ? (row.traySlots || []).map(s => s.rows.map(normalizeLookupRow)) : (row.lookupRows || []).map(normalizeLookupRow);
    const expected = fields.traySlots ? fields.traySlots.map(s => s.rows.map(normalizeLookupRow)) : fields.lookupRows.map(normalizeLookupRow);
    if (equal(normalize(before), expected)) continue;
    await mutate(`lookup:${id}`, '/api/custom-content/update', { setName, assetPath: before.assetPath,
      kind: before.kind, id, templateId: before.templateId, modelScale: before.modelScale || 1, ...fields },
      async () => { items = await listContent(); return contentById(id); },
      row => row.guid === before.guid && equal(normalize(row), expected) &&
        ['lookupGuid', 'cosmeticsPrefabGuid', 'traySlotGuid'].every(k => p.rawOnly && before.lookupRows?.length !== 2 || !before[k] || row[k] === before[k]));
  }
}

async function verifyAll(beforeItems, beforeRecipes) {
  items = await listContent(); liveRecipes = await listRecipes(); await loadOptions();
  ingredients = (await request(query('/api/catalog/ingredients', { levelSet: setName }))).ingredients || [];
  const catalog = (await request(query('/api/custom-content/catalog', { setName }))).items || [];
  const recipeFolderMeta = (await request(query('/api/level/data-file', { path: `${assetRoot}custom_recipes.meta` }), undefined, true)).toString('utf8');
  report.recipeBundleRegistration = { expected: `${setName}/custom_recipes`,
    actual: recipeFolderMeta.match(/^\s*assetBundleName:[ \t]*(.*)$/m)?.[1]?.trim() || '' };
  check('recipe-bundle-registration', report.recipeBundleRegistration.actual === report.recipeBundleRegistration.expected);
  const config = (await request(query('/api/level/data-file', { path: `${assetRoot}custom_content/Config.asset` }), undefined, true)).toString('utf8');
  for (const p of content) {
    const row = items.find(i => i.id === p.id);
    if (!check(`asset:${p.id}`, !!row, 'present')) continue;
    const old = beforeItems.find(i => i.id === p.id);
    check(`identity:${p.id}`, (!old || row.guid === old.guid) && row.kind === p.kind && row.templateId === p.templateId, 'GUID, kind and template preserved');
    const fields = contentFields(p);
    check(`fields:${p.id}`, contentValid(p, row, fields), JSON.stringify({ warnings: row.warnings, nodePath: row.nodePath, hasModel: row.hasModel }));
    const so = soFor(p.id), model = modelFor(p.id);
    const entry = { ...row, soGuid: so.guid, soPath: so.assetPath, modelGuid: model.guid, modelPath: model.assetPath,
      sourceSha256: art.get(p.id).sha256, compositionId: compositionFor(p.id), placeable: catalog.some(c => c.guid === row.guid) };
    report.assets.push(entry);
    const configEntry = config.split(/\n  - id: /).slice(1).find(block => block.startsWith(p.id + '\n')) || '';
    check(`persisted-icon-index:${p.id}`, configEntry.includes(`iconPath: ${path.posix.dirname(row.assetPath)}/${p.id}_Icon.png`));
    if (row.nodePath) {
      const node = (await request(query('/api/level/data-file', { path: row.nodePath }), undefined, true)).toString('utf8');
      const sprite = node.match(/m_iconSprite:\s*\{[^}]*guid:\s*([a-f0-9]{32})/i)?.[1];
      const lid = node.match(/m_atlasTexture:\s*\{[^}]*guid:\s*([a-f0-9]{32})/i)?.[1];
      check(`node-icon-wiring:${p.id}`, sprite && sprite === lid && new RegExp(`m_uID:\\s*${row.orderUid}\\b`).test(node), 'OrderNode sprite and crate lid point at the same imported icon');
      entry.nodeIconGuid = sprite || '';
    }
    if (p.kind !== 'ingredient' && p.kind !== 'prep') check(`catalog:${p.id}`, entry.placeable);
    const icon = await request(query('/api/custom-content/icon', { assetPath: row.assetPath }), undefined, true);
    check(`icon:${p.id}`, hash(icon) === hash(art.get(p.id).files[3].bytes) && icon.readUInt32BE(16) === 128 && icon.readUInt32BE(20) === 128, '128x128 PNG bytes match source');
    const source = await request(query('/api/custom-content/model-source', { assetPath: row.assetPath }));
    check(`model-source:${p.id}`, source.modelFile === `${p.id}.obj` && ['obj', 'mtl', 'png'].every(ext => (source.files || []).some(f => f.endsWith('.' + ext))), JSON.stringify(source));
    // Read the imported OBJ/MTL/texture bytes back rather than trusting only hasModel.
    for (const file of art.get(p.id).files.slice(0, 3)) {
      const bytes = await request(query('/api/custom-content/model-file', { assetPath: row.assetPath, file: file.fileName }), undefined, true);
      check(`model-file:${p.id}:${path.extname(file.fileName)}`, hash(bytes) === hash(file.bytes));
    }
    if (p.nextId) check(`cut-chain:${p.id}`, row.nextPrefabGuid === contentById(p.nextId).guid && row.orderUid !== contentById(p.nextId).orderUid);
    if (p.automaticCut) {
      const nextPath = row.assetPath.replace(/\.prefab$/, '_Unworkable.prefab');
      const meta = (await request(query('/api/level/data-file', { path: nextPath + '.meta' }), undefined, true)).toString('utf8');
      const nextGuid = meta.match(/^guid:\s*([a-f0-9]{32})/m)?.[1];
      const next = (await request(query('/api/level/data-file', { path: nextPath }), undefined, true)).toString('utf8');
      const nodeMeta = (await request(query('/api/level/data-file', { path: row.nodePath + '.meta' }), undefined, true)).toString('utf8');
      const nodeGuid = nodeMeta.match(/^guid:\s*([a-f0-9]{32})/m)?.[1];
      check(`automatic-cut:${p.id}`, nextGuid && row.nextPrefabGuid === nextGuid && nodeGuid && next.includes('guid: ' + nodeGuid), 'Automatic Unity clone exists, references owned Node and nextPrefab points to it');
      entry.automaticCutPath = nextPath; entry.automaticCutGuid = nextGuid;
    }
    if (lookupContentIds.includes(p.id)) {
      const required = await requiredLookupRows(p.rawOnly);
      const slots = row.templateId === 'utensil_tray' || row.kind === 'tray' ? row.traySlots || [] : [{ rows: row.lookupRows || [] }];
      check(`lookup-slot-count:${p.id}`, slots.length === (row.templateId === 'utensil_tray' || row.kind === 'tray' ? 3 : 1));
      for (const [i, slot] of slots.entries()) {
        check(`lookup-required-rows:${p.id}:${i}`, required.every(r => slot.rows.some(x =>
          x.recipeGuid === r.recipeGuid && x.modelGuid === r.modelGuid && !x.modelSoGuid && x.amountAllowed > 0)));
        const oldRows = old?.traySlots?.[i]?.rows || old?.lookupRows || [];
        if (!p.rawOnly || old?.lookupRows?.length === 2)
          check(`lookup-existing-preserved:${p.id}:${i}`, oldRows.every(r => slot.rows.some(x => equal(normalizeLookupRow(x), normalizeLookupRow(r)))));
      }
      if (row.lookupRows?.length) check(`prep-cosmetics:${p.id}`, row.lookupGuid && row.lookupGuid === row.cosmeticsLookupGuid && row.cosmeticsPrefabGuid);
      for (const k of ['lookupGuid', 'cosmeticsPrefabGuid', 'traySlotGuid']) if (old?.[k]) check(`lookup-guid:${p.id}:${k}`, old[k] === row[k]);
      if (p.rawOnly) check(`raw-cooked-mismatch:${p.id}`, row.lookupRows.length === 2 && !row.lookupRows.some(x => x.recipeGuid === recipeById('tweb_r_cooked_pot').guid), 'Raw red/blue accepted by table; cooked Pot deliberately absent. Play rejection untested.');
    }
    if (p.kind === 'splitter' && p.capacity === 5) check(`split-remainder:${p.id}`, row.capacity === 5 && row.splitNum === 2 && row.capacity % row.splitNum === 1 && row.inputRecipeGuid === recipeById('tweb_r_split_batch').guid);
    if (p.station) check(`utensil-pairs:${p.id}`, row.allowedIngredientGuids.length === row.modelGuids.length && row.modelGuids.length === 2 && !row.modelSoGuids?.length);
    await save(); progress(`verified asset ${p.id}`);
  }
  for (const p of recipes) {
    const row = liveRecipes.find(r => r.id === p.id);
    if (!check(`recipe:${p.id}`, !!row)) continue;
    const old = beforeRecipes.find(r => r.id === p.id);
    check(`recipe-fields:${p.id}`, recipeFieldsMatch(row, recipeFields(p)) && (!old || old.guid === row.guid), 'type, composition readback, step, plating, GUID');
    check(`recipe-visual:${p.id}`, row.hasModel && row.previewable && row.hasIcon && row.iconState === 'own');
    const diag = await request(query('/api/custom-recipes/diagnose', { assetPath: row.assetPath }));
    check(`recipe-mesh:${p.id}`, !diag.error && diag.rendererCount > 0 && diag.meshCount > 0 && diag.materialCount > 0 && diag.boundsSizeX > 0 && diag.boundsSizeY > 0,
      JSON.stringify(diag));
    if (p.step) check(`recipe-cooking:${p.id}`, diag.cookingStepSet);
    const icon = await request(query('/api/custom-recipes/icon', { assetPath: row.assetPath }), undefined, true);
    check(`recipe-icon:${p.id}`, hash(icon) === hash(art.get(p.id).files[3].bytes) && icon.readUInt32BE(16) === 128 && icon.readUInt32BE(20) === 128);
    const source = await request(query('/api/custom-recipes/model-source', { assetPath: row.assetPath }));
    check(`recipe-source:${p.id}`, source.modelFile === `${p.id}.obj`);
    for (const file of art.get(p.id).files.slice(0, 3)) {
      const bytes = await request(query('/api/level/data-file', { path: `${source.dirAssetPath}/${file.fileName}` }), undefined, true);
      check(`recipe-file:${p.id}:${path.extname(file.fileName)}`, hash(bytes) === hash(file.bytes));
    }
    const rawRecipe = (await request(query('/api/level/data-file', { path: row.assetPath }), undefined, true)).toString('utf8');
    const modelGuid = rawRecipe.match(/^  model:\s*\{[^}]*guid:\s*([a-f0-9]{32})/mi)?.[1] || '';
    const iconGuid = rawRecipe.match(/^  icon:\s*\{[^}]*guid:\s*([a-f0-9]{32})/mi)?.[1] || '';
    check(`recipe-direct-guids:${p.id}`, modelGuid && iconGuid);
    report.recipes.push({ ...row, modelGuid, modelPath: diag.modelPath, iconGuid, sourceSha256: art.get(p.id).sha256, diagnose: diag });
    await save(); progress(`verified recipe ${p.id}`);
  }
  const plannedIds = new Set(content.map(p => p.id)), recipeIds = new Set(recipes.map(p => p.id));
  check('unrelated-assets-preserved', beforeItems.filter(i => !plannedIds.has(i.id)).every(i => items.some(a => a.id === i.id && a.guid === i.guid)));
  check('unrelated-recipes-preserved', beforeRecipes.filter(i => !recipeIds.has(i.id)).every(i => liveRecipes.some(a => a.id === i.id && a.guid === i.guid)));
  check('template-coverage', new Set(report.assets.map(i => i.templateId)).size === 26);
  check('kind-coverage', new Set(report.assets.map(i => i.kind)).size === 7);
  check('recipe-type-coverage', ['Ingredient', 'Composite', 'Cooked', 'Mixed'].every(t => report.recipes.some(r => r.type === t)));
}

try {
  progress(`health check ${baseUrl}; ${content.length} assets, ${recipes.length} recipes`);
  report.health = await request('/api/health');
  const templates = (await request('/api/custom-content/templates')).templates || [];
  const meta = await request('/api/custom-content/meta');
  report.templates = templates.map(t => ({ id: t.id, kind: t.kind, assetPath: t.assetPath }));
  for (const p of content) {
    assert(templates.some(t => t.id === p.templateId && t.kind === p.kind), `Missing live template: ${p.templateId}`);
    if (p.station) assert(meta.stationTypes.some(t => t.name === p.station), `Unsupported station: ${p.station}`);
  }
  items = await listContent(); liveRecipes = await listRecipes();
  const beforeItems = structuredClone(items), beforeRecipes = structuredClone(liveRecipes);
  report.before = { assets: beforeItems.map(i => ({ id: i.id, guid: i.guid, nodePath: i.nodePath, iconUrl: i.iconUrl })),
    recipes: beforeRecipes.map(r => ({ id: r.id, guid: r.guid })) };
  refs = await request(query('/api/custom-recipes/references', { setName }));
  await loadOptions();
  if (cli.apply) {
    await writeSources();
    if (items.length) {
      const config = (await request(query('/api/level/data-file', { path: `${assetRoot}custom_content/Config.asset` }), undefined, true)).toString('utf8');
      persistedIconIds = new Set(content.filter(p => {
        const block = config.split(/\n  - id: /).slice(1).find(b => b.startsWith(p.id + '\n')) || '';
        return block.includes(`iconPath: ${assetRoot}custom_content/${p.kind}/${p.id}/${p.id}_Icon.png`);
      }).map(p => p.id));
    }
    // Dependencies first: cut ingredients -> whole ingredients -> stacks -> sinks -> recipes -> splitter.
    const deferred = new Set(['tweb_counter_sink', 'tweb_sink', ...content.filter(p => p.kind === 'splitter').map(p => p.id)]);
    for (const p of content.filter(p => !deferred.has(p.id))) {
      await ensureContent(p);
      if (p.id === 'tweb_blue_cut') await loadOptions();
    }
    await loadOptions();
    for (const id of ['tweb_counter_sink', 'tweb_sink']) await ensureContent(content.find(p => p.id === id));
    ingredients = (await request(query('/api/catalog/ingredients', { levelSet: setName }))).ingredients || [];
    // This GET initializes recipe configuration; deliberately never used by offline dry-run.
    await request(query('/api/custom-recipes/config', { setName }));
    for (const p of recipes) await ensureRecipe(p);
    await loadOptions();
    for (const p of content.filter(p => p.kind === 'splitter')) await ensureContent(p);
    await ensureLookups();
  }
  await verifyAll(beforeItems, beforeRecipes);
  const failures = report.validation.filter(v => !v.passed);
  report.status = failures.length ? 'validation-failed' : 'verified-with-documented-gaps';
  report.summary = { assets: report.assets.length, recipes: report.recipes.length,
    checks: report.validation.length, failures: failures.length, writes: report.operations.length,
    templateCount: new Set(report.assets.map(i => i.templateId)).size,
    newAssets: report.assets.filter(a => !beforeItems.some(b => b.id === a.id)).length,
    newRecipes: report.recipes.filter(a => !beforeRecipes.some(b => b.id === a.id)).length };
  await save();
  if (!failures.length) {
    const handoffFile = path.join(outputDir, 'handoff.json');
    let previous = {};
    try { previous = JSON.parse(await fs.readFile(handoffFile, 'utf8')); }
    catch (e) { if (e.code !== 'ENOENT') throw e; }
    assert(!previous.setName || previous.setName === setName, 'Handoff owner mismatch');
    await writeJson(handoffFile, mergeHandoff(previous, {
    schemaVersion: 1, setName, status: report.status, verifiedAt: report.updatedAt,
    checks: report.summary, recipeBundleRegistration: report.recipeBundleRegistration, gaps: report.gaps,
    assets: report.assets.map(a => ({ id: a.id, kind: a.kind, templateId: a.templateId, nameZh: a.nameZh,
      guid: a.guid, assetPath: a.assetPath, soGuid: a.soGuid, soPath: a.soPath, compositionId: a.compositionId,
      modelGuid: a.modelGuid, modelPath: a.modelPath, nodePath: a.nodePath, nodeIconGuid: a.nodeIconGuid,
      orderUid: a.orderUid, placeable: a.placeable, nextPrefabGuid: a.nextPrefabGuid, capacity: a.capacity,
      stationTypes: a.stationTypes, allowedIngredientGuids: a.allowedIngredientGuids, modelGuids: a.modelGuids,
      modelSoGuids: a.modelSoGuids, inputRecipeGuid: a.inputRecipeGuid, outputPrefabGuid: a.outputPrefabGuid,
      splitNum: a.splitNum, platingStepId: a.platingStepId, automaticCutPath: a.automaticCutPath,
      automaticCutGuid: a.automaticCutGuid, lookupRows: a.lookupRows, traySlots: a.traySlots,
      lookupGuid: a.lookupGuid, cosmeticsLookupGuid: a.cosmeticsLookupGuid,
      cosmeticsPrefabGuid: a.cosmeticsPrefabGuid, traySlotGuid: a.traySlotGuid,
      cleanPlateStackGuid: a.cleanPlateStackGuid, washingPartPath: a.washingPartPath, dryingPartPath: a.dryingPartPath })),
    recipes: report.recipes.map(r => ({ id: r.id, guid: r.guid, assetPath: r.assetPath, type: r.type,
      uID: r.uID, score: r.score, compositionIds: r.compositionIds, cookingStepId: r.cookingStepId,
      platingStepId: r.platingStepId, modelGuid: r.modelGuid, modelPath: r.modelPath, iconGuid: r.iconGuid })),
    coverage: { automaticCut: 'tweb_auto_cut', plating: ['tweb_r_glass', 'tweb_r_mug'],
      unevenSplitter: 'tweb_splitter_odd', rawOnlyPrep: 'tweb_prep_raw_only',
      rawCookedComparison: { raw: 'test_ingredient_workable_cut', cooked: 'tweb_r_cooked_pot',
        acceptsBoth: 'tweb_prep', acceptsRawOnly: 'tweb_prep_raw_only', runtimeTested: false }, fbxTested: false },
  }));
  }
  console.log(JSON.stringify({ reportFile, ...report.summary, status: report.status }));
  if (failures.length) process.exitCode = 1;
} catch (e) {
  report.status = 'blocked'; report.error = e.message;
  // Even on partial failure export every planned asset/recipe GUID that a final read can establish.
  try {
    items = await listContent(); liveRecipes = await listRecipes();
    report.partialAssets = items.filter(i => content.some(p => p.id === i.id));
    report.partialRecipes = liveRecipes.filter(i => recipes.some(p => p.id === i.id));
  } catch (readError) { report.finalReadbackError = readError.message; }
  await save(); console.error(`${e.message}\nReport: ${reportFile}`); process.exitCode = 1;
} finally { clearInterval(heartbeat); }
