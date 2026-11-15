import test from 'node:test';
import assert from 'node:assert/strict';
import { inflateSync } from 'node:zlib';
import { contentPlan, recipePlan, makeVisual, includeLookupRows, mergeHandoff } from './lib/tester-web-diy-assets.mjs';

test('all templates and kinds have unique IDs and acyclic dependencies', () => {
  const content = contentPlan(), recipes = recipePlan();
  assert.equal(content.length, 33);
  assert.equal(new Set(content.map(p => p.templateId)).size, 26);
  assert.equal(new Set(content.map(p => p.kind)).size, 7);
  assert.equal(new Set([...content, ...recipes].map(p => p.id)).size, content.length + recipes.length);
  assert.equal(recipes.length, 23);
  const contentIds = new Set(content.map(p => p.id)), knownRecipes = new Set();
  for (const p of content) if (p.nextId) assert.ok(contentIds.has(p.nextId));
  for (const p of recipes) {
    for (const c of p.comps) assert.ok(c.startsWith('@') ? knownRecipes.has(`tweb_r_${c.slice(1)}`) : contentIds.has(c), `${p.id}: unresolved dependency ${c}`);
    knownRecipes.add(p.id);
    if (p.step) {
      const pot = content.find(c => c.step === p.step);
      assert.ok(pot);
      assert.ok(p.comps.length <= pot.capacity);
      assert.ok(p.comps.every(c => ['test_ingredient_workable_cut', 'tweb_blue_cut'].includes(c)));
    }
  }
});

test('lookup merge preserves extra rows, restrictions and slot order without duplication', () => {
  const rows = [{ recipeGuid: 'a', modelGuid: 'old', amountAllowed: 2, restrictedRecipeGuids: ['b'] },
    { recipeGuid: 'extra', amountAllowed: 3 }];
  const required = [{ recipeGuid: 'a', modelGuid: 'new', amountAllowed: 1 }, { recipeGuid: 'c', amountAllowed: 1 }];
  const merged = includeLookupRows(rows, required);
  assert.deepEqual(merged.slice(0, 2), rows);
  assert.deepEqual(includeLookupRows(merged, required), merged);
  assert.equal(rows.length, 2);
});

test('handoff merge retains matching-agent evidence and unowned fields and rows', () => {
  const old = { lookupImplementation: { stableFiles: 30 }, otherEvidence: ['a'],
    assets: [{ id: 'a', proof: { stable: true }, guid: 'old' }, { id: 'external' }], recipes: [],
    gaps: [{ code: 'lookup', evidence: 'retained', status: 'old' }] };
  const merged = mergeHandoff(old, { assets: [{ id: 'a', guid: 'new', lookupRows: [1] }], recipes: [{ id: 'r' }],
    gaps: [{ code: 'lookup', status: 'verified' }] });
  assert.deepEqual(merged.lookupImplementation, old.lookupImplementation);
  assert.deepEqual(merged.assets[0].proof, { stable: true });
  assert.equal(merged.assets[1].id, 'external');
  assert.equal(merged.gaps[0].evidence, 'retained');
  assert.equal(merged.gaps[0].status, 'verified');
});

test('source visuals are deterministic, closed cuts have separate caps, OBJ indices are valid', () => {
  for (const p of [...contentPlan(), ...recipePlan()]) {
    const a = makeVisual(p.id, p.shape, p.colour), b = makeVisual(p.id, p.shape, p.colour);
    assert.equal(a.sha256, b.sha256);
    const obj = a.files[0].bytes.toString();
    const counts = ['v ', 'vt ', 'vn '].map(prefix => obj.split('\n').filter(l => l.startsWith(prefix)).length);
    assert.deepEqual(counts, [a.vertexCount, a.vertexCount, a.vertexCount]);
    for (const f of obj.split('\n').filter(l => l.startsWith('f '))) {
      assert.ok(f.split(' ').length >= 4);
      for (const v of f.slice(2).split(' ')) v.split('/').forEach((n, axis) => assert.ok(+n > 0 && +n <= counts[axis], `${p.id}: ${v}`));
    }
    assert.ok(obj.includes(`mtllib ${p.id}.mtl`));
    assert.ok(a.files[1].bytes.toString().includes(`map_Kd ${p.id}_base_color.png`));
    if (p.shape === 'cut') {
      assert.equal((obj.match(/^o segment_\d_shell/gm) || []).length, 3);
      assert.equal((obj.match(/^o segment_.*cut_cap/gm) || []).length, 4);
    }
  }
});

test('PNG icons and textures decode to 128 RGBA; icons have transparent margins', () => {
  for (const p of [...contentPlan(), ...recipePlan()]) for (const f of makeVisual(p.id, p.shape, p.colour).files.slice(2)) {
    const png = f.bytes;
    assert.equal(png.subarray(0, 8).toString('hex'), '89504e470d0a1a0a');
    assert.equal(png.readUInt32BE(16), 128); assert.equal(png.readUInt32BE(20), 128);
    assert.equal(png[24], 8); assert.equal(png[25], 6);
    const idat = [];
    for (let offset = 8; offset < png.length;) {
      const size = png.readUInt32BE(offset), tag = png.subarray(offset + 4, offset + 8).toString();
      if (tag === 'IDAT') idat.push(png.subarray(offset + 8, offset + 8 + size));
      offset += size + 12;
    }
    const raw = inflateSync(Buffer.concat(idat));
    assert.equal(raw.length, 128 * 513);
    if (f.fileName.endsWith('_Icon.png')) assert.equal(raw[4], 0);
  }
});
