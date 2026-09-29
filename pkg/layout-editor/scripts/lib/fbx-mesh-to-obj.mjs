/**
 * 将 FBX 网格导出为 OBJ（归一化到参考包围盒），供 Web 单果汁莓果类修正比例。
 * 参考：Web_Smoothie_Grape 在 Unity 中的杯体比例（Y=1，X/Z 与葡萄一致）。
 */
import { parseBinary } from "fbx-parser";

/** @type {{ min: number[]; max: number[]; size: number[] }} */
export const SMOOTHIE_REFERENCE_BOUNDS = {
  min: [-0.4573975205421448, 0, -0.2774657905101776],
  max: [0.4573975205421448, 1, 0.27746590971946716],
  size: [0.9147950410842896, 1, 0.5549317002296448],
};

function findAll(node, name, out) {
  if (!node) return;
  if (Array.isArray(node)) {
    for (const c of node) findAll(c, name, out);
    return;
  }
  if (typeof node !== "object") return;
  if (node.name === name) out.push(node);
  for (const v of Object.values(node)) findAll(v, name, out);
}

function boundsOf(vertices) {
  const min = [Infinity, Infinity, Infinity];
  const max = [-Infinity, -Infinity, -Infinity];
  for (let i = 0; i < vertices.length; i += 3) {
    for (let a = 0; a < 3; a++) {
      const v = vertices[i + a];
      min[a] = Math.min(min[a], v);
      max[a] = Math.max(max[a], v);
    }
  }
  const size = max.map((v, i) => v - min[i]);
  return { min, max, size };
}

/**
 * XZ 居中 + 底面对齐 + 统一水平缩放（保持圆柱截面）+ 独立 Y 缩放。
 * @param {number[]} v
 * @param {{ min: number[]; max: number[]; size: number[] }} src
 * @param {typeof SMOOTHIE_REFERENCE_BOUNDS} ref
 */
function normalizeVertex(v, src, ref) {
  const cx = (src.min[0] + src.max[0]) / 2;
  const cz = (src.min[2] + src.max[2]) / 2;
  const local = [v[0] - cx, v[1] - src.min[1], v[2] - cz];
  const sY = ref.size[1] / (src.size[1] > 1e-8 ? src.size[1] : 1);
  const sXZ = Math.min(
    ref.size[0] / (src.size[0] > 1e-8 ? src.size[0] : 1),
    ref.size[2] / (src.size[2] > 1e-8 ? src.size[2] : 1)
  );
  return [local[0] * sXZ, local[1] * sY, local[2] * sXZ];
}

/**
 * @typedef {{
 *   bodyScaleXZ?: number;
 *   translate?: number[];
 *   translateBodyOnly?: boolean;
 *   garnish?: { isGarnish: (p: number[]) => boolean; scale?: number; translate?: number[] };
 * }} SmoothieMeshTuning
 */

/** @type {Record<string, SmoothieMeshTuning>} */
export const RECIPE_MESH_TUNING = {
  Web_Smoothie_Blueberry: {
    bodyScaleXZ: 1.4,
    translate: [0.015, 0, 0.01],
    translateBodyOnly: true,
    garnish: {
      isGarnish: (p) =>
        p[1] > 0.935 && p[2] < -0.12 && Math.abs(p[0]) < 0.12,
      scale: 2.0,
      translate: [0, 0.008, 0.028],
    },
  },
  Web_Smoothie_Blackberry: {
    bodyScaleXZ: 1.4,
    translate: [-0.035, 0, 0.01],
    translateBodyOnly: true,
    garnish: {
      isGarnish: (p) =>
        p[1] > 0.935 && p[0] > 0.14 && p[0] < 0.25 && Math.abs(p[2]) < 0.12,
      scale: 2.8,
      translate: [-0.018, 0.006, -0.018],
    },
  },
  Web_Smoothie_Raspberry: {
    bodyScaleXZ: 1.4,
    translate: [-0.035, 0, 0.01],
    translateBodyOnly: true,
    garnish: {
      isGarnish: (p) =>
        p[1] > 0.935 && p[0] > 0.13 && p[0] < 0.3 && Math.abs(p[2]) < 0.25,
      scale: 2.2,
      translate: [-0.02, 0.006, -0.022],
    },
  },
};

/**
 * @param {number[][]} points
 * @param {SmoothieMeshTuning | undefined} tuning
 */
function applyMeshTuning(points, tuning) {
  if (!tuning) return;

  const garnishMask = tuning.garnish
    ? points.map((p) => tuning.garnish.isGarnish(p))
    : null;

  if (tuning.bodyScaleXZ && tuning.bodyScaleXZ !== 1) {
    const s = tuning.bodyScaleXZ;
    for (let i = 0; i < points.length; i++) {
      if (garnishMask && garnishMask[i]) continue;
      points[i][0] *= s;
      points[i][2] *= s;
    }
  }

  if (tuning.translate) {
    const [dx, dy, dz] = tuning.translate;
    const bodyOnly = Boolean(tuning.translateBodyOnly && garnishMask);
    for (let i = 0; i < points.length; i++) {
      if (bodyOnly && garnishMask[i]) continue;
      points[i][0] += dx;
      points[i][1] += dy;
      points[i][2] += dz;
    }
  }

  if (!tuning.garnish) return;
  const { isGarnish, scale = 1, translate: gTranslate } = tuning.garnish;
  const indices = [];
  for (let i = 0; i < points.length; i++) {
    if (isGarnish(points[i])) indices.push(i);
  }
  if (!indices.length) return;

  let cx = 0;
  let cy = 0;
  let cz = 0;
  for (const i of indices) {
    cx += points[i][0];
    cy += points[i][1];
    cz += points[i][2];
  }
  const n = indices.length;
  cx /= n;
  cy /= n;
  cz /= n;

  for (const i of indices) {
    const p = points[i];
    p[0] = cx + (p[0] - cx) * scale;
    p[1] = cy + (p[1] - cy) * scale;
    p[2] = cz + (p[2] - cz) * scale;
  }

  if (gTranslate) {
    const [gx, gy, gz] = gTranslate;
    for (const i of indices) {
      points[i][0] += gx;
      points[i][1] += gy;
      points[i][2] += gz;
    }
  }
}

/**
 * @param {Uint8Array|Buffer} fbxBytes
 * @param {{ recipeId: string; baseColorFile: string; referenceBounds?: typeof SMOOTHIE_REFERENCE_BOUNDS; materialName?: string; meshTuning?: SmoothieMeshTuning }} opts
 * @returns {{ obj: string; mtl: string; vertexCount: number; faceCount: number }}
 */
export function fbxToNormalizedObj(fbxBytes, opts) {
  const {
    recipeId,
    baseColorFile,
    referenceBounds = SMOOTHIE_REFERENCE_BOUNDS,
    materialName = `mat_${recipeId}`,
    meshTuning = RECIPE_MESH_TUNING[recipeId],
  } = opts;
  const fbx = parseBinary(fbxBytes);
  const vertNodes = [];
  const polyNodes = [];
  const uvNodes = [];
  const uvIndexNodes = [];
  findAll(fbx, "Vertices", vertNodes);
  findAll(fbx, "PolygonVertexIndex", polyNodes);
  findAll(fbx, "UV", uvNodes);
  findAll(fbx, "UVIndex", uvIndexNodes);

  if (!vertNodes.length || !polyNodes.length) {
    throw new Error(`${recipeId}: FBX 缺少 Vertices / PolygonVertexIndex`);
  }

  const vertices = [...vertNodes[0].props[0]];
  const polyIdx = [...polyNodes[0].props[0]];
  const uvs = uvNodes.length ? [...uvNodes[0].props[0]] : null;
  const uvIdx = uvIndexNodes.length ? [...uvIndexNodes[0].props[0]] : null;

  const src = boundsOf(vertices);
  const ref = referenceBounds;

  const uniqueCount = vertices.length / 3;
  const normalized = new Array(uniqueCount);
  for (let vi = 0; vi < uniqueCount; vi++) {
    normalized[vi] = normalizeVertex(
      [vertices[vi * 3], vertices[vi * 3 + 1], vertices[vi * 3 + 2]],
      src,
      ref
    );
  }
  applyMeshTuning(normalized, meshTuning);

  const objLines = [`mtllib ${recipeId}.mtl`, `o ${recipeId}`, `usemtl ${materialName}`];
  const vLines = [];
  const vtLines = [];
  const faceLines = [];

  let corner = 0;
  let faceCount = 0;
  const corners = [];

  for (const raw of polyIdx) {
    const end = raw < 0;
    const vi = end ? -raw - 1 : raw;
    const p = normalized[vi];
    vLines.push(`v ${p[0].toFixed(6)} ${p[1].toFixed(6)} ${p[2].toFixed(6)}`);

    if (uvs && uvIdx) {
      const ui = uvIdx[corner] ?? 0;
      const u = uvs[ui * 2] ?? 0;
      const v = uvs[ui * 2 + 1] ?? 0;
      vtLines.push(`vt ${u.toFixed(6)} ${v.toFixed(6)}`);
      corners.push({ v: vLines.length, vt: vtLines.length });
    } else {
      corners.push({ v: vLines.length, vt: 0 });
    }

    corner++;
    if (end) {
      if (corners.length >= 3) {
        const parts = corners.map((c) => (c.vt ? `${c.v}/${c.vt}` : `${c.v}`));
        faceLines.push(`f ${parts.join(" ")}`);
        faceCount++;
      }
      corners.length = 0;
    }
  }

  const mtl = `newmtl ${materialName}\nKd 1.000 1.000 1.000\nmap_Kd ${baseColorFile}\n`;
  let obj = objLines.join("\n");
  if (vLines.length) obj += "\n" + vLines.join("\n");
  if (vtLines.length) obj += "\n" + vtLines.join("\n");
  if (faceLines.length) obj += "\n" + faceLines.join("\n");
  obj += "\n";

  return {
    obj,
    mtl,
    vertexCount: vLines.length,
    faceCount,
  };
}
