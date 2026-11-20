/** Pure layout authoring. The caller owns scene creation, recipes and writeback. */
const CELL = 1.2;
const PREFIX = 'V011__';
const v = (x = 0, y = 0, z = 0) => ({ x, y, z });
const round = n => Math.round(n * 10000) / 10000;
const check = (ok, message) => { if (!ok) throw new Error(message); };
const clone = value => structuredClone(value);
const exact = (entries, id) => {
  const found = new Map(entries.filter(e => e.id === id).map(e => [e.guid, e]));
  check(found.size === 1, `Catalog identity missing/ambiguous: ${id}`);
  const entry = [...found.values()][0];
  check(entry.guid && entry.assetPath, `Catalog identity incomplete: ${id}`);
  return entry;
};

export const LEVELS = [
  { id: 'v011_tea', title: '茶饮与热水器', recipeIds: ['Web_MilkTea_Tea', 'Web_MilkTea_MilkTea', 'Web_MilkTea_HoneyTea', 'Web_MilkTea_HoneyMilkTea'], mode: 'Campaign' },
  { id: 'v011_japanese', title: '统一日料', recipeIds: ['Web_Sushi_Prawn', 'Web_Sushi_Prawn_Cucumber', 'Web_Sushi_GrandSlam', 'Web_Sushi_SashimiCombo', 'Web_Steamed_Fish', 'Web_Steamed_Prawns'], mode: 'Campaign' },
  { id: 'v011_pasta_burrito', title: '意面与卷饼', recipeIds: ['Web_Fish_Pasta', 'Web_Prawn_Pasta', 'Web_Seafood_Pasta', 'Web_Burrito_Meat_Chicken', 'Web_Burrito_Mushroom_Chicken'], mode: 'Campaign' },
  { id: 'v011_mechanism', title: '压力门、传送带与早餐', recipeIds: ['Cake_Plain_SO', 'Web_V011_Spice_SpringOnion_Sandwich', 'Web_V011_Garden_Sandwich'], mode: 'Campaign' },
  { id: 'v011_horde', title: '敌群服务窗口', recipeIds: ['Web_Sushi_SashimiCombo', 'Web_Steamed_Fish'], mode: 'Horde' },
];

// Appearance SOs are not placement catalog items. Verified against the shipped
// counter-appearances.json; ingredient and recipe identities always come from ctx.
const SKINS = {
  CounterAnniversarySO: '4ca6af2a458abb1dcdd951edbc609001', CounterJungleSO: 'f003394d7dffb22700efedd141e3da21',
  CookerWood13SO: '98e4be4aea2bf45e19440a6dc77d318b', DispenserMedievalSO: 'c0d8a1bf646090cb5c55fcea981624a3',
  DispenserWinterSO: '82d3beb09aa94e18ff944809c445f6da', DispenserCircusSO: 'e4822bdca1075451cf86345f922da943',
  DispenserFirewoodSO: 'c0c0d8f595d92729fc8e1920cc490271', DispenserWood13SO: '0839e011bc99a0c1236922544e2670c4',
  DispenserChristmasSO: 'f3ceada80f7fba999b1df8399b7fcfee',
};

// Exact sparse lookup slots from testyimian/20261104_114148_layout/doc_after.json.
const PASTA_MODELS = {
  modelGuids: ['', '', '', '949a48257d6a82246918e3af3e81524e'],
  modelSoGuids: ['', '', 'f099b5194b8af4747789845c829644e5', ''],
};

function rect(item) {
  const p = item.worldPosition || item.localPosition;
  const r = (item.localRotationY || 0) * Math.PI / 180;
  const c = item.colliderCenter || v();
  const sx = item.localScale?.x ?? 1, sz = item.localScale?.z ?? 1;
  return { item, x: p.x + c.x * Math.cos(r) + c.z * Math.sin(r), z: p.z - c.x * Math.sin(r) + c.z * Math.cos(r),
    hx: (Math.abs(Math.cos(r)) * item.footprint.cellsX * sx + Math.abs(Math.sin(r)) * item.footprint.cellsZ * sz) * CELL / 2,
    hz: (Math.abs(Math.sin(r)) * item.footprint.cellsX * sx + Math.abs(Math.cos(r)) * item.footprint.cellsZ * sz) * CELL / 2 };
}
const overlaps = (a, b, epsilon = 0.015) => Math.abs(a.x - b.x) < a.hx + b.hx - epsilon && Math.abs(a.z - b.z) < a.hz + b.hz - epsilon;

/** Returns a full replacement document; never mutates ctx/current or accesses IO.
 * Reuses only four Players and (Horde) the three windows and one gate by GUID.
 * All other roots are recreated with deterministic new: IDs, even on a rerun.
 */
export function buildLayout(levelId, ctx, current) {
  const level = LEVELS.find(l => l.id === levelId);
  check(level, `Unknown level: ${levelId}`);
  check(current && !current.partLevel?.enabled, 'A non-part-level current layout is required');
  check(current.sceneAssetPath === `Assets/LevelSets/tester_web/scenes/${levelId}.unity`, 'Target scene mismatch');
  for (const field of ['stock', 'ingredients', 'recipes', 'materials']) check(Array.isArray(ctx[field]), `ctx.${field} must be an array`);
  const doc = clone(current);
  Object.assign(doc, { items: [], floors: [], walkable: [], switchLinks: [], animControls: { groups: [] },
    buttonLinks: { links: [] }, buttonEvents: { links: [] }, coaxialLinks: { links: [] }, randomTriggers: { links: [] } });
  delete doc.moveControls;
  const cat = id => exact(ctx.stock, id);
  const ing = id => exact(ctx.ingredients, id).guid;
  const recipe = id => exact(ctx.recipes, id);
  const skin = (item, id) => { item.pseudoPrefabGuid = ctx.stock.find(e => e.id === id)?.guid || SKINS[id]; check(item.pseudoPrefabGuid, `Missing skin ${id}`); return item; };
  const make = (id, role, x, z, extra = {}, rotation = 0, y = 0) => {
    const c = cat(id), instanceId = `new:${c.guid}:${levelId}:${role}`;
    const item = { instanceId, hierarchyPath: instanceId, prefabGuid: c.guid, prefabAssetPath: c.assetPath,
      parentPath: c.defaultParent, displayName: PREFIX + role, localPosition: v(round(x * CELL), y, round(z * CELL)),
      worldPosition: v(round(x * CELL), y, round(z * CELL)), localRotationX: 0, localRotationY: rotation,
      localRotationZ: 0, localScale: v(1, 1, 1), footprint: clone(c.footprint), walkable: false, ...extra };
    doc.items.push(item); return item;
  };
  const at = (id, role, x, z, extra, rotation) => make(id, role, x, z, extra, rotation);
  const counter = (role, x, z) => {
    const item = at('Counter', role, x, z);
    if (levelId === 'v011_tea') skin(item, 'CounterAnniversarySO');
    if (levelId === 'v011_pasta_burrito') skin(item, 'CounterJungleSO');
    return item;
  };
  const stack = (id, role, host, extra = {}) => make(id, role, host.worldPosition.x / CELL, host.worldPosition.z / CELL, extra, 0, cat(id).stack?.y ?? 0.5);
  const utensil = (id, role, x, z, allowed, capacity = 1, extra = {}) => {
    const host = at(id === 'MixerBowl' ? 'Mixer' : 'Cooker', role + '_host', x, z);
    if (levelId === 'v011_japanese' && id !== 'MixerBowl') skin(host, 'CookerWood13SO');
    return stack(id, role, host, { stubKind: 'CookingUtensil', cookingUtensil: { capacity, allowedIngredientGuids: allowed.map(ing), ...extra } });
  };
  const crate = (id, x, z, index = 0) => {
    const item = at('Dispenser', 'crate_' + id, x, z, { stubKind: 'Dispenser', dispenser: { spawnerItemPrefabGuid: ing(id) } });
    const skins = levelId === 'v011_japanese' ? ['DispenserWood13SO'] : levelId === 'v011_pasta_burrito' ? ['DispenserMedievalSO', 'DispenserFirewoodSO', 'DispenserWood13SO'] : levelId === 'v011_mechanism' ? ['DispenserWinterSO', 'DispenserCircusSO', 'DispenserChristmasSO'] : [];
    if (skins.length) skin(item, skins[index % skins.length]);
    return item;
  };
  const floor = (role, x, z, width, depth) => {
    const material = ctx.materials.find(m => m.id === 'DLC_OnionHouse_Floor' && m.guid && m.assetPath && !m.assetPath.includes('/LevelSets/'))
      || ctx.materials.find(m => m.id === 'Floor' && m.guid && m.assetPath);
    check(material, 'No shared floor material');
    const instanceId = `new:floor:${levelId}:${role}`;
    doc.floors.push({ instanceId, hierarchyPath: instanceId, parentPath: 'Art/Ground', displayName: PREFIX + role,
      surfaceKind: 'solid', meshType: 'plane', meshFileId: 10209, materialGuid: material.guid, materialAssetPath: material.assetPath,
      materialName: material.id, localPosition: v(x * CELL, -0.05, z * CELL), worldPosition: v(x * CELL, -0.05, z * CELL),
      localRotationY: 0, localScale: v(width * CELL / 10, 1, depth * CELL / 10), widthUnits: width * CELL, depthUnits: depth * CELL,
      widthCells: width, depthCells: depth, materialTilingW: 1, materialTilingD: 1 });
  };
  const reuse = (id, count) => {
    const found = (current.items || []).filter(i => i.prefabGuid === cat(id).guid).sort((a, b) => a.instanceId.localeCompare(b.instanceId));
    check(found.length === count, `Expected ${count} reusable ${id}, got ${found.length}`);
    return found.map(clone);
  };
  const players = reuse('Player', 4);
  players.forEach((p, i) => {
    p.displayName = PREFIX + 'player_' + (i + 1); p.stubKind = 'Player';
    const pos = level.mode === 'Horde' ? v((i % 2 ? 8 : 1) * CELL, 0, (i < 2 ? 0 : -2) * CELL) : v((i % 2 ? 8 : 1) * CELL, 0, (i < 2 ? -2 : -4) * CELL);
    p.localPosition = clone(pos); p.worldPosition = pos; doc.items.push(p);
  });
  let serving, ret;
  const service = (tea = false, mechanism = false) => {
    serving = at('ServingStation', 'serve', mechanism ? 4.5 : tea ? 5.5 : 7.5, mechanism || tea ? 0 : -6, { stubKind: 'ServingStation' }, mechanism || tea ? 0 : 180);
    ret = at(tea ? 'web_teacup_return' : 'PlateReturn', 'return', tea ? 7 : 10, tea ? 0 : -5, { stubKind: 'PlateReturn', plateReturn: { returnClean: false } });
    at(tea ? 'web_sink_teacup' : 'Sink', 'sink', tea ? 8.5 : 10, tea ? 0 : -1.5, {}, tea ? 0 : 90);
    serving.servingStation = { plateReturnInstanceIds: [ret.instanceId], plateReturnInstanceId: ret.instanceId };
    if (!tea) stack('CleanPlateStack', 'plates', counter('plates_host', 9, -6), { stubKind: 'CleanPlateStack', cleanPlateStack: { plateCount: 8 } });
  };
  const basic = () => {
    at('ChoppingCounter', 'chop_1', -1, -1);
    at('ChoppingCounter', 'chop_2', -1, -4);
    at('Bin', 'bin', -1, -5);
  };
  if (levelId === 'v011_tea') {
    service(true); basic();
    const sw = at('Switch', 'switch', 1, 0, { stubKind: 'Switch', switchStub: { startEnabled: true, idleColor: '#32965b', pressedColor: '#ff9b32' } });
    [0, 2].forEach((x, i) => {
      const kettle = at('web_kettle_hotwater', 'kettle_' + (i + 1), x, 0, { stubKind: 'Kettle', kettle: { heatSeconds: i ? 8 : 5, switchInstanceId: sw.instanceId } });
      doc.switchLinks.push({ switchId: sw.instanceId, targetId: kettle.instanceId, trigger: 'Switch' });
    });
    crate('TeaSO', 3, 0); crate('milk', -1, -3); crate('HoneycombSO', 0, -6);
    ['web_teacup_stack_clean', 'web_teacup', 'web_teacup_stack_dirty', 'web_teacup_dirty'].forEach((id, i) => {
      stack(id, id, counter('cup_host_' + i, 3 + i, -3), id.includes('stack') ? { stubKind: 'CleanPlateStack', cleanPlateStack: { plateCount: 6 } } : {});
    });
  } else if (levelId === 'v011_japanese') {
    service(); basic();
    ['SeaweedSO', 'SushiRiceSO', 'SushiFishSO', 'SushiPrawnSO', 'CucumberSO', 'FishSO', 'FlourSO', 'PrawnSO'].forEach((id, i) => crate(id, i, 0, i));
    utensil('Pot', 'rice_pot', 0, -6, ['SushiRiceSO']);
    utensil('Steamer', 'steamer', 1, -6, ['FishSO']);
    utensil('MixerBowl', 'shumai_bowl', 2, -6, ['FlourSO', 'PrawnSO'], 2);
    [3, 4, 5].forEach(x => counter('island_' + x, x, -3));
  } else if (levelId === 'v011_pasta_burrito') {
    service(); basic();
    ['PastaSO', 'SushiFishSO', 'SushiPrawnSO', 'TortillaSO', 'RiceSO', 'BurritoMeatSO', 'BurritoChickenSO', 'MushroomSO'].forEach((id, i) => crate(id, i, 0, i));
    const groups = level.recipeIds.filter(id => id.startsWith('Web_Burrito')).flatMap(id => recipe(id).cookingGroups || []);
    const allowed = step => [...new Set(groups.filter(g => g.step === step).flatMap(g => g.ingredients))];
    check(allowed('Pot').length && allowed('FryingPan').length, 'Missing live burrito cookingGroups');
    utensil('Pot', 'pasta_pot', 0, -6, ['PastaSO']);
    utensil('Pot', 'burrito_pot', 1, -6, allowed('Pot'));
    const pan = utensil('FryPan', 'pasta_pan', 2, -6, ['SushiFishSO', 'SushiPrawnSO'], 1, clone(PASTA_MODELS));
    pan.cookingUtensil.allowedIngredientGuids.push(recipe('Web_Seafood_Pasta_FishNode').guid, recipe('Web_Seafood_Pasta_PrawnNode').guid);
    utensil('FryPan', 'burrito_pan_1', 3, -6, allowed('FryingPan'));
    utensil('FryPan', 'burrito_pan_2', 4, -6, allowed('FryingPan'));
    [3, 4, 5].forEach(x => counter('island_' + x, x, -3));
  } else if (levelId === 'v011_mechanism') {
    service(false, true); basic();
    ['FlourSO', 'EggSO', 'hotdogbun', 'LettuceSO', 'TomatoSO'].forEach((id, i) => crate(id, i, -6, i));
    utensil('MixerBowl', 'pancake_bowl', 5, -6, ['FlourSO', 'EggSO'], 2);
    const pan = utensil('FryPan', 'pancake_pan', 6, -6, [], 1);
    pan.cookingUtensil.allowedIngredientGuids = [recipe('Web_MixedFlourEgg').guid];
    const sauce = at('dlc11_condiment_dispenser', 'condiment', 7, -6, { stubKind: 'Dispenser',
      dispenser: { spawnerItemPrefabGuid: ing('dlc11_ketchup') }, soArray: { pseudoPrefabGuids: [ing('dlc11_ketchup')] } });
    const sauceButton = at('p_dlc08_button_condiments', 'condiment_button', 8, -6, { stubKind: 'Switch',
      switchStub: { startEnabled: true } });
    doc.switchLinks.push({ switchId: sauceButton.instanceId, targetId: sauce.instanceId, trigger: 'Next' });
    const ps = at('PressureSwitch', 'pressure', 2.5, -2.5, { stubKind: 'PressureSwitch', pressureSwitch: { idleColor: '#32965b', pressedColor: '#ff9b32' } });
    // Hinge at the north edge of the one-cell doorway; local collider centre
    // (0,-.470) follows the real door leaf instead of a centred one-cell square.
    const door = at('p_dlc5_door_01', 'pantry_door', -1, -2.6, { colliderCenter: v(0, 0.987, -0.470), footprint: { cellsX: 0.208 / CELL, cellsZ: 0.934 / CELL } });
    const group = { id: PREFIX + 'door_hold', displayName: PREFIX + 'door_hold', groupKind: 'members', triggerMode: 'button', advanceMode: 'hold',
      itemInstanceIds: [door.instanceId], floorInstanceIds: [], objectInstanceIds: [], startDelay: 0, loop: false, loopDelay: 0, waypoints: [],
      events: ['ccw', 'cw'].map((direction, i) => ({ id: PREFIX + 'door_' + i, type: 'rotate', delay: 0, rotateDegrees: 90, rotateDirection: direction, rotateSeconds: 0.4 })) };
    doc.animControls.groups.push(group);
    doc.buttonLinks.links.push({ id: PREFIX + 'pressure_link', sourceId: ps.instanceId, groupNames: [group.displayName], lockUntilFinished: false });
     at('ConveyorStation', 'conveyor', 4.5, -2, { stubKind: 'Conveyor', conveyor: {
       conveySpeed: 0.5, serve: false, buttonControlled: false, speedControl: { enabled: false, levels: [] }
     } }, 90);
    at('web_conveyor_station_serve', 'conveyor_serve', 4.5, -1, { stubKind: 'Conveyor', conveyor: { conveySpeed: 0.5, serve: true } }, 90);
    counter('serve_guard_left', 3.5, -1); counter('serve_guard_right', 5.5, -1);
    floor('pantry_floor', -3, -3, 3, 5);
    for (let j = -1; j >= -5; j--) counter('pantry_west_' + j, -4, j);
    for (const j of [-1, -5]) for (const x of [-3, -2]) counter(`pantry_edge_${x}_${j}`, x, j);
    crate('SpiceSO', -3, -2, 0); crate('SpringOnionSO', -3, -4, 1);
  } else {
    const windows = reuse('Service_Window', 3), gate = reuse('gate', 1)[0];
    ret = at('PlateReturn', 'return', 9.5, -2, { stubKind: 'PlateReturn', plateReturn: { returnClean: false } });
    windows.sort((a, b) => a.worldPosition.x - b.worldPosition.x).forEach((w, i) => {
      check(Math.abs(w.worldPosition.x - i * 4.8) < 0.03 && Math.abs(w.worldPosition.z - 2.4) < 0.03 && Math.abs(w.localRotationY) < 0.01, 'Unsupported Horde window template geometry');
      w.displayName = PREFIX + 'window_' + i; w.stubKind = 'ServingStation';
      w.servingStation = { ...w.servingStation, plateReturnInstanceId: ret.instanceId, plateReturnInstanceIds: [ret.instanceId] }; doc.items.push(w);
    });
    check(Math.abs(gate.localRotationY) < 0.01, 'Unsupported Horde gate orientation');
    gate.displayName = PREFIX + 'gate'; gate.stubKind = 'Lockable'; gate.lockable = { ...gate.lockable, unlockCost: 30 };
    gate.localPosition = v(4.2, 0, -4.8); gate.worldPosition = clone(gate.localPosition);
    gate.colliderCenter = v(0.6, 0, 0); doc.items.push(gate);
    at('Sink', 'sink', 9.5, 0.5, {}, 90);
    ['SushiFishSO', 'SushiPrawnSO', 'FishSO'].forEach((id, i) => crate(id, 0.5 + i, -4));
    at('ChoppingCounter', 'chop_1', -1.5, 0); at('ChoppingCounter', 'chop_2', -1.5, -2);
    utensil('Steamer', 'steamer', 4, 0, ['FishSO']);
    stack('CleanPlateStack', 'plates', counter('plates_host', 6.5, -4), { stubKind: 'CleanPlateStack', cleanPlateStack: { plateCount: 8 } });
    at('Bin', 'bin', 8.5, -4);
    // Kitchen uses half-cell X centres to meet the centred native windows.
    for (let x = -1.5; x <= 9.5; x++) for (const z of [2, -4]) {
      const candidate = { x: x * CELL, z: z * CELL, hx: 0.6, hz: 0.6 };
      if (!doc.items.filter(i => i.stubKind !== 'Player' && i.worldPosition.y < 0.2).some(i => overlaps(candidate, rect(i)))) counter(`wall_${x}_${z}`, x, z);
    }
    for (const x of [-1.5, 9.5]) for (let z = -3; z < 2; z++) {
      if (!doc.items.filter(i => i.stubKind !== 'Player').some(i => overlaps({ x: x * CELL, z: z * CELL, hx: 0.6, hz: 0.6 }, rect(i)))) counter(`side_${x}_${z}`, x, z);
    }
    floor('floor', 4, -1, 12, 7); floor('apron', 4, 5.5, 12, 6);
    floor('backroom_floor', 4, -6.5, 4, 4);
    for (const x of [2.5, 5.5]) for (let z = -5; z >= -8; z--) counter(`room_${x}_${z}`, x, z);
    for (const x of [3.5, 4.5]) counter('room_south_' + x, x, -8);
    utensil('Steamer', 'backroom_steamer', 3.5, -7, ['FishSO']);
    at('ChoppingCounter', 'backroom_chop', 4.5, -7);
  }
  if (level.mode !== 'Horde') {
    floor('floor', 4.5, -3, 12, 7);
    // Fill every unclaimed perimeter cell. Multi-cell stations claim by AABB,
    // and the pantry doorway is deliberately left for the hinged door.
    for (let j = 0; j >= -6; j--) for (let i = -1; i <= 10; i++) {
      if (i !== -1 && i !== 10 && j !== 0 && j !== -6) continue;
      if (levelId === 'v011_mechanism' && i === -1 && j === -2) continue;
      const cell = { x: i * CELL, z: j * CELL, hx: 0.6, hz: 0.6 };
      if (!doc.items.filter(it => it.stubKind !== 'Player' && it.worldPosition.y < 0.2).some(it => overlaps(cell, rect(it)))) counter(`wall_${i}_${j}`, i, j);
    }
  }
  validateLayout(doc);
  return doc;
}

/** Throws on invalid geometry/references; returns a concise diagnostic on success.
 * Static reachability treats pressure plates and open doors/gates as traversable.
 * Physics, animated closed doors and actual attach points still require Play.
 */
export function validateLayout(doc) {
  const ids = new Set(), names = new Set();
  for (const item of [...doc.items, ...(doc.floors || [])]) {
    check(!ids.has(item.instanceId), `Duplicate instanceId: ${item.instanceId}`); ids.add(item.instanceId);
    check(!names.has(item.displayName), `Duplicate name: ${item.displayName}`); names.add(item.displayName);
    const nativeRoot = (item.stubKind === 'Player' && /\/Player\.prefab$/.test(item.prefabAssetPath || ''))
      || (['ServingStation', 'Lockable'].includes(item.stubKind) && /\/(Service_Window|gate)\.prefab$/.test(item.prefabAssetPath || ''));
    check(item.displayName.startsWith(PREFIX) || nativeRoot, `Unexpected unowned item: ${item.displayName}`);
  }
  const players = doc.items.filter(i => i.stubKind === 'Player');
  check(players.length === 4, 'Expected four Players');
  const solids = doc.items.filter(i => i.stubKind !== 'Player' && (i.worldPosition || i.localPosition).y < 0.2);
  const rectangles = solids.map(rect);
  const floors = (doc.floors || []).map(f => ({ x: f.worldPosition.x, z: f.worldPosition.z, hx: f.widthUnits / 2, hz: f.depthUnits / 2 }));
  check(floors.length, 'Missing floor');
  for (let i = 0; i < rectangles.length; i++) {
    const a = rectangles[i];
    check(floors.some(f => Math.abs(a.x - f.x) + a.hx <= f.hx + 0.02 && Math.abs(a.z - f.z) + a.hz <= f.hz + 0.02), `Outside floor: ${a.item.displayName}`);
    for (const b of rectangles.slice(0, i)) check(!overlaps(a, b), `Overlap: ${a.item.displayName} / ${b.item.displayName}`);
  }
  for (const item of doc.items.filter(i => (i.worldPosition || i.localPosition).y >= 0.2)) {
    check(solids.some(host => {
      const a = rect(host), b = rect(item);
      return Math.abs(a.x - b.x) < 0.02 && Math.abs(a.z - b.z) < 0.02;
    }), `Missing stack host: ${item.displayName}`);
  }
  const byId = new Map(doc.items.map(i => [i.instanceId, i]));
  for (const link of doc.switchLinks || []) check(byId.has(link.switchId) && byId.has(link.targetId), 'Dangling switch link');
  for (const item of doc.items) {
    if (item.stubKind === 'Kettle') check(doc.switchLinks.some(l => l.targetId === item.instanceId && l.switchId === item.kettle.switchInstanceId), 'Unlinked kettle');
    if (item.stubKind === 'PressureSwitch') check(item.walkable === false, 'PressureSwitch must not generate Col_Floor');
    if (item.stubKind === 'ServingStation') check(item.servingStation?.plateReturnInstanceIds?.length && item.servingStation.plateReturnInstanceIds.every(id => byId.get(id)?.stubKind === 'PlateReturn'), 'Dangling serving return');
  }
  for (const group of doc.animControls?.groups || []) {
    check(group.itemInstanceIds.every(id => byId.has(id)), 'Dangling animation member');
    if (group.advanceMode === 'hold') check(group.events.length === 2 && group.events.every(e => e.type === 'rotate'), 'Invalid hold events');
  }
  for (const link of doc.buttonLinks?.links || []) check(byId.get(link.sourceId)?.stubKind === 'PressureSwitch' && link.groupNames.every(name => doc.animControls.groups.some(g => g.displayName === name)), 'Dangling pressure link');
  const step = 0.3, radius = 0.27;
  const xmin = Math.min(...floors.map(f => f.x - f.hx)), zmin = Math.min(...floors.map(f => f.z - f.hz));
  const nx = Math.ceil((Math.max(...floors.map(f => f.x + f.hx)) - xmin) / step);
  const nz = Math.ceil((Math.max(...floors.map(f => f.z + f.hz)) - zmin) / step);
  const obstacles = rectangles.filter(r => !['PressureSwitch', 'Lockable'].includes(r.item.stubKind) && !r.item.displayName.endsWith('pantry_door'));
  const point = index => ({ x: xmin + (index % nx + 0.5) * step, z: zmin + (Math.floor(index / nx) + 0.5) * step });
  const open = new Uint8Array(nx * nz), reached = new Uint8Array(nx * nz);
  for (let i = 0; i < open.length; i++) {
    const p = point(i);
    if ([[-radius, -radius], [-radius, radius], [radius, -radius], [radius, radius]].every(([dx, dz]) =>
      floors.some(f => Math.abs(p.x + dx - f.x) < f.hx + 0.001 && Math.abs(p.z + dz - f.z) < f.hz + 0.001)) &&
      !obstacles.some(r => Math.abs(p.x - r.x) < r.hx + radius - 0.01 && Math.abs(p.z - r.z) < r.hz + radius - 0.01)) open[i] = 1;
  }
  const indices = players.map(p => Math.floor((p.worldPosition.z - zmin) / step) * nx + Math.floor((p.worldPosition.x - xmin) / step));
  check(indices.every(i => open[i]), 'Blocked chef spawn');
  const queue = [indices[0]]; reached[queue[0]] = 1;
  for (let head = 0; head < queue.length; head++) {
    const i = queue[head], x = i % nx, z = Math.floor(i / nx);
    for (const [dx, dz] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
      const xx = x + dx, zz = z + dz, next = zz * nx + xx;
      if (xx >= 0 && xx < nx && zz >= 0 && zz < nz && open[next] && !reached[next]) { reached[next] = 1; queue.push(next); }
    }
  }
  check(indices.every(i => reached[i]), 'Disconnected chef spawns');
  const reachableStations = rectangles.filter(r => !/V011__(wall_|side_|room_|pantry_edge_|pantry_west_)/.test(r.item.displayName) &&
    !(r.item.displayName === PREFIX + 'serve' && doc.items.some(i => i.conveyor?.serve)));
  for (const r of reachableStations) check(queue.some(i => {
    const p = point(i), dx = Math.max(0, Math.abs(p.x - r.x) - r.hx), dz = Math.max(0, Math.abs(p.z - r.z) - r.hz);
    return dx * dx + dz * dz < 0.85 ** 2;
  }), `Inaccessible station: ${r.item.displayName}`);
  const serve = doc.items.find(i => i.displayName === PREFIX + 'conveyor_serve');
  if (serve) {
    const station = doc.items.find(i => i.displayName === PREFIX + 'serve');
    const feed = doc.items.find(i => i.displayName === PREFIX + 'conveyor');
    check(serve.conveyor.serve && [serve, feed].every(i => i.localRotationY === 90) &&
      Math.abs(station.worldPosition.x - serve.worldPosition.x) < 0.01 && Math.abs(station.worldPosition.z - serve.worldPosition.z - CELL) < 0.01 &&
      Math.abs(feed.worldPosition.x - serve.worldPosition.x) < 0.01 && Math.abs(serve.worldPosition.z - feed.worldPosition.z - CELL) < 0.01, 'Broken northbound serving conveyor chain');
  }
  return { allSpawnsConnected: true, accessibleObjects: reachableStations.length, reachableAreaM2: round(queue.length * step * step), items: doc.items.length };
}

export function renderSvg(doc, title = '') {
  const esc = s => String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const floors = doc.floors || [];
  const left = Math.min(...floors.map(f => f.worldPosition.x - f.widthUnits / 2)) - 0.5;
  const top = -Math.max(...floors.map(f => f.worldPosition.z + f.depthUnits / 2)) - 1;
  const right = Math.max(...floors.map(f => f.worldPosition.x + f.widthUnits / 2)) + 0.5;
  const bottom = -Math.min(...floors.map(f => f.worldPosition.z - f.depthUnits / 2)) + 0.5;
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${left} ${top} ${right - left} ${bottom - top}" width="1100"><title>${esc(title)}</title><rect x="${left}" y="${top}" width="${right - left}" height="${bottom - top}" fill="#101b26"/><text x="${left + 0.3}" y="${top + 0.55}" fill="white" font-size="0.35">${esc(title)}</text>${floors.map(f => `<rect x="${f.worldPosition.x - f.widthUnits / 2}" y="${-f.worldPosition.z - f.depthUnits / 2}" width="${f.widthUnits}" height="${f.depthUnits}" fill="#d9d3c2" stroke="#8b8577" stroke-width="0.02"/>`).join('')}${doc.items.map(i => {
    const r = rect(i), raised = i.worldPosition.y >= 0.2;
    return `<g><title>${esc(i.displayName)} (${esc(i.stubKind || 'Counter')})</title><rect x="${r.x - r.hx}" y="${-r.z - r.hz}" width="${2 * r.hx}" height="${2 * r.hz}" fill="${i.stubKind === 'Player' ? '#dc7446' : raised ? '#eccb68' : '#4e807d'}" fill-opacity="0.85" stroke="#18363c" stroke-width="0.025"/><text x="${r.x}" y="${-r.z}" text-anchor="middle" dominant-baseline="middle" fill="#102329" font-size="0.15">${esc(i.displayName.slice(PREFIX.length))}</text></g>`;
  }).join('')}</svg>`;
}
