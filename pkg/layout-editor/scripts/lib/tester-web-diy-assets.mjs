import { deflateSync } from 'node:zlib';
import { createHash } from 'node:crypto';

export const palette = [[224, 55, 57], [46, 119, 225], [153, 73, 35], [221, 159, 57],
  [165, 190, 202], [43, 160, 119], [242, 229, 192], [65, 66, 87]];

function crc32(bytes) {
  let crc = 0xffffffff;
  for (const b of bytes) {
    crc ^= b;
    for (let i = 0; i < 8; i++) crc = (crc >>> 1) ^ ((crc & 1) ? 0xedb88320 : 0);
  }
  return (crc ^ 0xffffffff) >>> 0;
}

export function png(width, height, pixel) {
  const chunk = (type, data) => {
    const tag = Buffer.from(type), n = Buffer.alloc(4), crc = Buffer.alloc(4);
    n.writeUInt32BE(data.length); crc.writeUInt32BE(crc32(Buffer.concat([tag, data])));
    return Buffer.concat([n, tag, data, crc]);
  };
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width); header.writeUInt32BE(height, 4); header[8] = 8; header[9] = 6;
  const raw = Buffer.alloc(height * (1 + width * 4));
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    const p = pixel(x, y);
    for (let c = 0; c < 4; c++) raw[y * (1 + width * 4) + 1 + x * 4 + c] = p[c];
  }
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', header),
    chunk('IDAT', deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
}

export function makeVisual(id, shape, colour = 0) {
  const lines = ['# Deterministic OC2 tester_web_diy source; metres; Y up', `mtllib ${id}.mtl`];
  let vertexCount = 0, objectCount = 0;
  const vertex = (v, n, mat, u = 0.5, t = 0.5) => {
    lines.push(`v ${v.map(x => x.toFixed(6)).join(' ')}`);
    lines.push(`vt ${((mat + 0.15 + u * 0.7) / 8).toFixed(6)} ${(0.15 + t * 0.7).toFixed(6)}`);
    lines.push(`vn ${n.map(x => x.toFixed(6)).join(' ')}`);
    return ++vertexCount;
  };
  const face = ids => lines.push(`f ${ids.map(i => `${i}/${i}/${i}`).join(' ')}`);
  const object = (name, mat) => lines.push(`o ${name}_${++objectCount}`, `usemtl ${id}_${mat}`);
  const box = (name, centre, size, mat) => {
    object(name, mat);
    for (let axis = 0; axis < 3; axis++) for (const sign of [-1, 1]) {
      const normal = [0, 0, 0]; normal[axis] = sign;
      const a = (axis + 1) % 3, b = (axis + 2) % 3;
      const ids = [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([u, v]) => {
        const p = [...centre]; p[axis] += sign * size[axis] / 2;
        p[a] += u * size[a] / 2; p[b] += v * size[b] / 2;
        return vertex(p, normal, mat, (u + 1) / 2, (v + 1) / 2);
      });
      face(sign > 0 ? ids : ids.reverse());
    }
  };
  const disk = (name, cx, y, cz, radius, mat, sign) => {
    object(name, mat);
    const c = vertex([cx, y, cz], [0, sign, 0], mat);
    const ring = [];
    for (let j = 0; j <= 20; j++) {
      const angle = Math.PI * 2 * j / 20;
      ring.push(vertex([cx + radius * Math.cos(angle), y, cz + radius * Math.sin(angle)],
        [0, sign, 0], mat, (Math.cos(angle) + 1) / 2, (Math.sin(angle) + 1) / 2));
    }
    for (let j = 0; j < 20; j++) face(sign > 0 ? [c, ring[j + 1], ring[j]] : [c, ring[j], ring[j + 1]]);
  };
  const cylinder = (name, x, y, z, r, h, mat) => {
    object(name, mat);
    const rows = [];
    for (let k = 0; k < 2; k++) {
      const row = [];
      for (let j = 0; j <= 20; j++) {
        const a = Math.PI * 2 * j / 20;
        row.push(vertex([x + r * Math.cos(a), y + h * k, z + r * Math.sin(a)],
          [Math.cos(a), 0, Math.sin(a)], mat, j / 20, k));
      }
      rows.push(row);
    }
    for (let j = 0; j < 20; j++) face([rows[0][j], rows[1][j], rows[1][j + 1], rows[0][j + 1]]);
    disk(`${name}_bottom`, x, y, z, r, mat, -1); disk(`${name}_top`, x, y + h, z, r, mat, 1);
  };
  const sphere = (name, centre, radius, mat, from = 0, to = Math.PI, cap = false) => {
    object(`${name}_shell`, mat);
    const rows = [];
    for (let k = 0; k <= 8; k++) {
      const theta = from + (to - from) * k / 8, row = [];
      for (let j = 0; j <= 20; j++) {
        const phi = Math.PI * 2 * j / 20;
        const n = [Math.sin(theta) * Math.cos(phi), Math.cos(theta), Math.sin(theta) * Math.sin(phi)];
        row.push(vertex(n.map((v, axis) => centre[axis] + radius * v), n, mat, j / 20, k / 8));
      }
      rows.push(row);
    }
    for (let k = 0; k < 8; k++) for (let j = 0; j < 20; j++)
      face([rows[k][j], rows[k][j + 1], rows[k + 1][j + 1], rows[k + 1][j]]);
    // Caps are separate OBJ objects and materials; no stretched faces bridge a cut.
    if (cap && from > 0) disk(`${name}_cut_cap_top`, centre[0], centre[1] + radius * Math.cos(from), centre[2], radius * Math.sin(from), 6, 1);
    if (cap && to < Math.PI) disk(`${name}_cut_cap_bottom`, centre[0], centre[1] + radius * Math.cos(to), centre[2], radius * Math.sin(to), 6, -1);
  };
  const basin = (name, y, width, depth, mat) => {
    box(`${name}_base`, [0, y + 0.035, 0], [width, 0.07, width], mat);
    box(`${name}_front`, [0, y + depth / 2, width / 2 - 0.035], [width, depth, 0.07], mat);
    box(`${name}_back`, [0, y + depth / 2, -width / 2 + 0.035], [width, depth, 0.07], mat);
    box(`${name}_left`, [-width / 2 + 0.035, y + depth / 2, 0], [0.07, depth, width - 0.14], mat);
    box(`${name}_right`, [width / 2 - 0.035, y + depth / 2, 0], [0.07, depth, width - 0.14], mat);
  };
  if (shape === 'ball') sphere('whole_ball', [0, 0.28, 0], 0.28, colour);
  else if (shape === 'cut') {
    for (let k = 0; k < 3; k++) sphere(`segment_${k}`, [0, 0.36 + (1 - k) * 0.075, 0], 0.28,
      colour, k * Math.PI / 3, (k + 1) * Math.PI / 3, true);
  } else if (shape === 'cooked' || shape === 'combo' || shape === 'mixed') {
    const count = shape === 'cooked' ? 1 : 3;
    for (let k = 0; k < count; k++) {
      const a = k * Math.PI * 2 / count;
      sphere(`cooked_piece_${k}`, [count === 1 ? 0 : Math.cos(a) * 0.19, 0.20, count === 1 ? 0 : Math.sin(a) * 0.19],
        count === 1 ? 0.25 : 0.18, shape === 'mixed' ? 3 : (k % 2 ? 3 : 2));
    }
  } else if (shape === 'counter' || shape === 'sink' || shape === 'splitter' || shape === 'burner') {
    box('cabinet', [0, 0.42, 0], [1.08, 0.84, 1.08], colour);
    if (shape === 'sink') {
      basin('sink_bowl', 0.84, 1.15, 0.20, 4);
      cylinder('tap', 0, 1.04, -0.4, 0.035, 0.28, 4);
      box('tap_spout', [0, 1.3, -0.25], [0.07, 0.07, 0.3], 4);
    } else if (shape === 'splitter') {
      basin('hopper', 0.85, 0.72, 0.32, 4);
      box('outlet', [0, 0.42, 0.59], [0.42, 0.22, 0.22], 7);
    } else {
      box('worktop', [0, 0.91, 0], [1.18, 0.14, 1.18], 6);
      if (shape === 'burner') cylinder('fire_ring', 0, 0.98, 0, 0.35, 0.08, 7);
    }
  } else if (shape.includes('stack') || shape === 'plate' || shape === 'dirty_plate') {
    const count = shape.includes('stack') ? 4 : 1;
    for (let k = 0; k < count; k++) cylinder(`plate_${k}`, 0, k * 0.065, 0, 0.43, 0.045, shape.includes('dirty') ? 2 : 6);
  } else {
    const width = shape === 'pot' ? 0.65 : 0.8, height = ['tray', 'roasting', 'griddle'].includes(shape) ? 0.12 : 0.38;
    basin(shape, 0, width, height, colour);
    if (shape === 'skewer' || shape === 'fork') box('handle', [0, 0.06, -0.58], [0.045, 0.045, 0.7], 7);
    if (shape === 'steamer') for (let k = -2; k <= 2; k++) box(`slat_${k}`, [k * 0.12, 0.4, 0], [0.055, 0.025, 0.64], 3);
  }
  const obj = Buffer.from(lines.join('\n') + '\n');
  const mtl = Buffer.from(palette.map((_, i) => `newmtl ${id}_${i}\nKa 1 1 1\nKd 1 1 1\nKs 0.05 0.05 0.05\nd 1\nillum 2\nmap_Kd ${id}_base_color.png\n`).join('\n'));
  const texture = png(128, 128, (x, y) => {
    const p = palette[Math.min(7, Math.floor(x / 16))], shade = 0.82 + 0.16 * (1 - y / 128) + ((x * 17 + y * 31) % 9) / 255;
    return [...p.map(c => Math.round(c * shade)), 255];
  });
  const icon = png(128, 128, (x, y) => {
    const cx = x - 64, cy = y - 66;
    const round = ['ball', 'cut', 'cooked', 'combo', 'mixed'].includes(shape);
    let inside = round ? cx * cx + cy * cy <= 48 * 48 : Math.abs(cx) < 48 && Math.abs(cy) < 38;
    if (shape === 'cut' && (Math.abs(cy - 16) < 4 || Math.abs(cy + 16) < 4)) inside = false;
    if (!inside) return [0, 0, 0, 0];
    let index = colour;
    if (shape === 'cut' && (Math.abs(cy - 11) < 4 || Math.abs(cy + 21) < 4)) index = 6;
    if (shape === 'combo') index = cx > 0 ? 3 : 2;
    const light = 0.78 + 0.22 * (1 - y / 128);
    return [...palette[index].map(c => Math.round(c * light)), 255];
  });
  const files = [{ fileName: `${id}.obj`, bytes: obj }, { fileName: `${id}.mtl`, bytes: mtl },
    { fileName: `${id}_base_color.png`, bytes: texture }, { fileName: `${id}_Icon.png`, bytes: icon }];
  return { files, vertexCount, objectCount, sha256: createHash('sha256').update(Buffer.concat(files.map(f => f.bytes))).digest('hex') };
}

export function contentPlan() {
  const item = (id, kind, templateId, nameZh, shape, colour, extra = {}) => ({ id, kind, templateId, nameZh, nameEn: id, shape, colour, ...extra });
  return [
    item('test_ingredient_workable_cut', 'ingredient', 'ingredient_unworkable', '测试红球切段', 'cut', 0),
    item('test_ingredient_workable', 'ingredient', 'ingredient_workable', '测试红球整颗', 'ball', 0, { nextId: 'test_ingredient_workable_cut' }),
    item('tweb_blue_cut', 'ingredient', 'ingredient_unworkable', '测试蓝球切段', 'cut', 1),
    item('tweb_blue', 'ingredient', 'ingredient_workable', '测试蓝球整颗', 'ball', 1, { nextId: 'tweb_blue_cut' }),
    item('tweb_ingredient_prep', 'ingredient', 'ingredient_preparation', '测试备餐型食材', 'prep', 5),
    ...[['counter', '普通工作台', 'counter'], ['counter_chopping', '切菜工作台', 'counter'], ['counter_sink', '水槽工作台', 'sink'], ['hotpot_burner', '火锅灶台', 'burner']]
      .map(([t, n, s], i) => item(`tweb_${t}`, 'counter', t, `测试${n}`, s, i)),
    ...[['utensil_plate', '盘子', 'plate'], ['utensil_tray', '通用托盘', 'tray'],
      ['clean_plate_stack', '干净盘堆', 'plate_stack'], ['dirty_plate', '脏盘', 'dirty_plate'], ['dirty_plate_stack', '脏盘堆', 'dirty_plate_stack'],
      ['clean_tray_stack', '干净托盘堆', 'plate_stack'], ['dirty_tray', '脏托盘', 'tray'], ['dirty_tray_stack', '脏托盘堆', 'dirty_plate_stack']]
      .map(([t, n, s]) => item(`tweb_${t}`, 'utensil', t, `测试${n}`, s, 4)),
    ...[['hotpot', '火锅', 'pot', 'Invalid_FloorBurner', 'HotPot'], ['roasting_tray', '烤盘', 'roasting', 'Oven', 'RoastingTray'],
      ['griddle_pan', '煎盘', 'griddle', 'FirePit', 'GriddlePan'], ['skewer', '烤串', 'skewer', 'Barbeque', 'KebabSkewer'],
      ['toasting_fork', '烤叉', 'fork', 'FirePit', 'ToastingFork'], ['pot', '汤锅', 'pot', 'Hob', 'Pot'], ['steamer', '蒸笼', 'steamer', 'Hob', 'Steamer']]
      .map(([t, n, s, station, step]) => item(`tweb_${t}`, 'utensil', `utensil_${t}`, `测试${n}`, s, 4, { station, step, capacity: t === 'toasting_fork' ? 1 : 4 })),
    item('tweb_sink', 'sink', 'sink', '测试独立水槽', 'sink', 5),
    item('tweb_tray', 'tray', 'tray', '测试槽位托盘', 'tray', 4),
    item('tweb_prep', 'prep', 'prep', '测试备餐容器', 'prep', 5),
    item('tweb_splitter', 'splitter', 'splitter', '测试分料机', 'splitter', 3),
    item('tweb_auto_cut', 'ingredient', 'ingredient_workable', '测试自动切片对照', 'ball', 3, { automaticCut: true }),
    item('tweb_glass_ingredient', 'ingredient', 'ingredient_unworkable', '测试玻璃杯食材', 'cut', 1, { plating: 'Glass' }),
    item('tweb_mug_ingredient', 'ingredient', 'ingredient_unworkable', '测试马克杯食材', 'cut', 2, { plating: 'Mug' }),
    item('tweb_splitter_odd', 'splitter', 'splitter', '测试不整除分料机', 'splitter', 1, { capacity: 5, splitNum: 2 }),
    item('tweb_prep_raw_only', 'prep', 'prep', '测试仅生料备餐容器', 'prep', 0, { rawOnly: true }),
  ];
}

export function recipePlan() {
  const recipe = (name, type, comps, shape, extra = {}) => ({ id: `tweb_r_${name}`, type, comps,
    nameZh: `测试${name}`, shape, colour: shape === 'ball' || shape === 'cut' ? 0 : 2, score: 60, ...extra });
  return [
    recipe('red', 'Ingredient', ['test_ingredient_workable_cut'], 'cut'),
    recipe('blue', 'Ingredient', ['tweb_blue_cut'], 'cut', { colour: 1 }),
    recipe('whole_red', 'Ingredient', ['test_ingredient_workable'], 'ball'),
    recipe('raw_pair', 'Composite', ['test_ingredient_workable_cut', 'tweb_blue_cut'], 'combo'),
    ...contentPlan().filter(i => i.step).map(i => recipe(`cooked_${i.step.toLowerCase()}`, 'Cooked',
      i.capacity === 1 ? ['test_ingredient_workable_cut'] : ['test_ingredient_workable_cut', 'tweb_blue_cut'], 'cooked', { step: i.step, score: 0 })),
    recipe('mixed_pair', 'Mixed', ['test_ingredient_workable_cut', 'tweb_blue_cut'], 'mixed', { mixIcon: 'MixerIcon', score: 0 }),
    recipe('mixed_double_red', 'Mixed', ['test_ingredient_workable_cut', 'test_ingredient_workable_cut'], 'mixed', { mixIcon: 'MixerIcon' }),
    recipe('cooked_red_triple', 'Cooked', ['test_ingredient_workable_cut', 'test_ingredient_workable_cut', 'test_ingredient_workable_cut'], 'combo', { step: 'RoastingTray' }),
    recipe('plated_pot', 'Composite', ['@cooked_pot'], 'cooked'),
    recipe('plated_steamer', 'Composite', ['@cooked_steamer'], 'cooked'),
    recipe('cooked_pair', 'Composite', ['@cooked_pot', '@cooked_griddlepan'], 'combo'),
    recipe('cooked_triple', 'Composite', ['@cooked_hotpot', '@cooked_steamer', '@cooked_kebabskewer'], 'combo'),
    recipe('raw_and_cooked', 'Composite', ['@cooked_toastingfork', 'tweb_blue_cut'], 'combo'),
    recipe('split_batch', 'Composite', ['test_ingredient_workable_cut', 'test_ingredient_workable_cut'], 'combo', { score: 0 }),
    recipe('auto_cut', 'Ingredient', ['tweb_auto_cut'], 'cut', { colour: 3 }),
    recipe('glass', 'Ingredient', ['tweb_glass_ingredient'], 'cut', { colour: 1, plating: 'Glass' }),
    recipe('mug', 'Ingredient', ['tweb_mug_ingredient'], 'cut', { colour: 2, plating: 'Mug' }),
  ];
}

export const lookupContentIds = ['tweb_tray', 'tweb_utensil_tray', 'tweb_prep', 'tweb_ingredient_prep', 'tweb_prep_raw_only'];
export const lookupCompositionIds = ['test_ingredient_workable_cut', 'tweb_blue_cut', '@cooked_hotpot', '@cooked_pot', '@cooked_griddlepan', '@cooked_pair', '@cooked_triple'];

export function normalizeLookupRow(row) {
  return { recipeGuid: row.recipeGuid || '', modelGuid: row.modelGuid || '', modelSoGuid: row.modelSoGuid || '',
    amountAllowed: row.amountAllowed, restrictedRecipeGuids: row.restrictedRecipeGuids || [] };
}

// Keep existing rows, limits, ordering and extensions; only append missing test entries.
export function includeLookupRows(existing, required) {
  const rows = structuredClone(existing || []);
  for (const row of required) if (!rows.some(r => r.recipeGuid === row.recipeGuid)) rows.push(structuredClone(row));
  return rows;
}

export function mergeHandoff(existing, generated) {
  const result = { ...existing, ...generated };
  for (const field of ['checks', 'recipeBundleRegistration', 'coverage'])
    if (generated[field]) result[field] = { ...existing[field], ...generated[field] };
  for (const [field, key] of [['assets', 'id'], ['recipes', 'id'], ['gaps', 'code']]) {
    const rows = new Map((existing[field] || []).map(r => [r[key], r]));
    for (const row of generated[field] || []) rows.set(row[key], { ...rows.get(row[key]), ...row });
    result[field] = [...rows.values()];
  }
  return result;
}
