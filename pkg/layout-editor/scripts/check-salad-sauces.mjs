#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const read = p => fs.readFileSync(path.join(root, p), 'utf8').replace(/^\uFEFF/, '');
const m = JSON.parse(read('layout-editor/scripts/data/salad-sauce-recipes.json'));
const dir = 'Assets/commonW3/custom_recipes/salad';
const md5 = s => crypto.createHash('md5').update(s).digest('hex');
assert.equal(m.bases.length, 26);
assert.equal(m.recipes.length, 78);
assert.equal(m.optionals.length, 24);
assert.deepEqual(m.sauceUidPolicy.nativeDispenserVisualSlots, { mustard: 0, ketchup: 1 });
const uids = new Set();
const compositions = new Set();
const originals = execFileSync('git', ['ls-files', dir], { cwd: root, encoding: 'utf8' }).trim().split('\n');
let unchanged = 0;
for (const p of originals) {
  if (!/\/(Web_(?:L?Salad)_.*\.asset(?:\.meta)?|icons\/.*)$/.test(p)) continue;
  const before = execFileSync('git', ['show', 'HEAD:' + p], { cwd: root });
  assert.ok(before.equals(fs.readFileSync(path.join(root, p))), 'Original changed: ' + p);
  unchanged++;
}
for (const r of [...m.recipes, ...m.optionals]) {
  const p = `${dir}/${r.id}.asset`;
  const text = read(p);
  assert.ok(!uids.has(r.uid), 'UID collision'); uids.add(r.uid);
  assert.equal(+text.match(/uID: (\d+)/)[1], r.uid);
  assert.equal(read(p + '.meta').match(/^guid: (\w+)/m)[1], md5('commonw3-recipe:' + r.id));
  const actual = [...text.match(/compositionSOs:([\s\S]*?)  optionalSOs:/)[1].matchAll(/guid: (\w+)/g)].map(x => x[1]);
  assert.deepEqual(actual, r.ingredients.map(id => m.ingredients[id].guid));
  assert.equal(new Set(actual).size, actual.length, 'Repeated ingredient reference');
  if (r.score > 0) {
    const key = r.ingredients.map(id => m.ingredients[id].uid).sort((a,b) => a-b).join(',');
    assert.ok(!compositions.has(key), 'Final composition duplicate'); compositions.add(key);
    assert.equal(r.iconGuid, md5('commonw3-icon:' + r.id));
  } else {
    assert.ok(r.ingredients.some(id => !id.includes('ketchup') && !id.includes('mustard')), 'Anchor missing');
  }
}
const names = JSON.parse(read('Assets/commonW3/custom_recipes/names.json')).names;
assert.equal(names.filter(n => n.id.startsWith('Web_SauceSalad_')).length, 78);
assert.equal(new Set(names.map(n => n.id)).size, names.length, 'Duplicate name ID');
for (const p of fs.readdirSync(path.join(root, dir)).filter(n => n.endsWith('.asset') && !n.startsWith('Web_SauceSalad_'))) {
  const uid = +read(`${dir}/${p}`).match(/uID: (\d+)/)[1];
  assert.ok(!uids.has(uid), 'Generated/existing UID collision');
}
for (const family of ['Story', 'DLC11']) {
  const p = `${dir}/models/Web_SauceSalad_${family}_Model.prefab`;
  const text = read(p);
  assert.ok(!text.includes('guid: 00000000000000000000000000000000'), 'Invalid script reference');
  assert.ok(text.includes('m_prefabLookup: {fileID: 0}'));
  assert.ok(text.includes('m_LocalPosition: {x: 0, y: 0, z: 0}'));
  assert.ok(text.includes('m_LocalScale: {x: 1, y: 1, z: 1}'));
  assert.ok(read(`${dir}/models/Web_SauceSalad_${family}_Model.asset`).includes('guid: ' + read(p + '.meta').match(/^guid: (\w+)/m)[1]));
}
console.log(JSON.stringify({ ok: true, bases: 26, finals: 78, anchors: 24, uniqueUids: uids.size, originalFilesUnchanged: unchanged }));
if (process.argv.includes('--live')) {
  const host = process.env.OC2_UNITY_URL || 'http://10.211.55.3:8765';
  async function request(method) {
    const res = await fetch(host + '/api/recipes/salad-sauce', { method, ...(method === 'POST' ? { body: '' } : {}), signal: AbortSignal.timeout(90000) });
    const result = await res.json();
    assert.ok(result.ok, JSON.stringify(result));
    assert.ok(result.nativeChecks >= 3088 && result.colourEvidence.length >= 32, 'Missing actual native machine/colour regression');
    return result;
  }
  const files = fs.readdirSync(path.join(root, dir, 'models')).filter(n => /(?:Visual|_Model)\.(?:prefab|asset)(?:\.meta)?$/.test(n));
  const digest = () => Object.fromEntries(files.map(n => [n, crypto.createHash('sha256').update(fs.readFileSync(path.join(root, dir, 'models', n))).digest('hex')]));
  await request('POST');
  const first = digest();
  const rebuilt = await request('POST');
  assert.deepEqual(digest(), first, 'Unity factory is not byte-idempotent');
  const readback = await request('GET');
  assert.deepEqual(readback, rebuilt, 'Readback differs from rebuild validation');
  console.log(JSON.stringify({ live: readback, factoryIdempotentFiles: files.length }));
  if (process.argv.includes('--report')) {
    const reportDir = path.join(root, 'layout-editor/reports/salad-sauce-integration');
    fs.mkdirSync(reportDir, { recursive: true });
    fs.writeFileSync(path.join(reportDir, 'live-validation.json'), JSON.stringify({
      checkedAt: new Date().toISOString(), unityUrl: host, ...readback,
      factoryIdempotentFiles: first, originalFilesUnchanged: unchanged,
      pending: ['Windows player-driven host/client gameplay and final visual approval remain unverified; isolated bundle build results are recorded separately in bundle-validation.json'],
    }, null, 2) + '\n');
    const lines = ['# Salad Sauce Recipe Mapping', '',
      'Each base maps to exactly three final recipes. Full ingredient/GUID/icon data lives in scripts/data/salad-sauce-recipes.json.', '',
      '| Base | Ketchup ID / UID | Mustard ID / UID | Both ID / UID |', '|---|---|---|---|'];
    for (const b of m.bases) {
      const recipes = m.recipes.filter(r => r.baseId === b.id);
      lines.push(`| ${b.id}${b.aliases.length ? ' (aliases: ' + b.aliases.join(', ') + ')' : ''} | ${recipes.map(r => r.id + ' / ' + r.uid).join(' | ')} |`);
    }
    fs.writeFileSync(path.join(reportDir, 'recipe-mapping.md'), lines.join('\n') + '\n');
  }
}
