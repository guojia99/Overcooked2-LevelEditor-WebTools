/**
 * 将 FBX 网格导出为 OBJ（归一化到参考包围盒），供 Web 单果汁莓果类修正比例。
 * 参考：Web_Smoothie_Grape 在 Unity 中的杯体比例（Y=1，X/Z 与葡萄一致）。
 */
import { parseBinary } from "fbx-parser";
import { simplifyWeldedMesh } from "./mesh-simplify.mjs";

/** commonW3 手持杯体目标面数（Web 端盘展示，约 25k 三角面）。 */
export const DEFAULT_MAX_FACE_COUNT = 25_000;

/** @type {{ min: number[]; max: number[]; size: number[] }} */
export const SMOOTHIE_REFERENCE_BOUNDS = {
  min: [-0.4573975205421448, 0, -0.2774657905101776],
  max: [0.4573975205421448, 1, 0.27746590971946716],
  size: [0.9147950410842896, 1, 0.5549317002296448],
};

/** 端盘/手持杯体 X 基准左偏，再按杯宽 5% 右调（与 Y 轴归一化尺度一致）。 */
export const HELD_CUP_TRANSLATE_X = -0.1 + SMOOTHIE_REFERENCE_BOUNDS.size[0] * 0.05;
export const HELD_CUP_TRANSLATE = [HELD_CUP_TRANSLATE_X, 0, 0.01];

/** 冰淇淋导出：保持 FBX 比例后整体放大（底面中心为枢轴）。 */
export const ICE_CREAM_UNIFORM_SCALE = 1.1;
/** 冰淇淋导出：按当前高度整体上移（比例）。 */
export const ICE_CREAM_LIFT_HEIGHT_RATIO = 0.15;

/** @type {Record<string, { uniformScale?: number; liftByHeightRatio?: number; translate?: number[] }>} */
export const ICE_CREAM_MESH_TUNING = {};

/** @param {string} recipeId */
export function iceCreamExportOpts(recipeId) {
  const tuning = ICE_CREAM_MESH_TUNING[recipeId];
  return {
    normalizeToReference: false,
    uniformScale: tuning?.uniformScale ?? ICE_CREAM_UNIFORM_SCALE,
    liftByHeightRatio: tuning?.liftByHeightRatio ?? ICE_CREAM_LIFT_HEIGHT_RATIO,
    translate: tuning?.translate,
  };
}

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
 * 以底面中心为枢轴做均匀缩放（手持物放大时保持落地对齐）。
 * @param {number[][]} points
 * @param {number} scale
 */
function scaleVerticesUniform(points, scale) {
  if (scale === 1 || !points.length) return;
  let minX = Infinity;
  let minY = Infinity;
  let minZ = Infinity;
  let maxX = -Infinity;
  let maxZ = -Infinity;
  for (const p of points) {
    minX = Math.min(minX, p[0]);
    minY = Math.min(minY, p[1]);
    minZ = Math.min(minZ, p[2]);
    maxX = Math.max(maxX, p[0]);
    maxZ = Math.max(maxZ, p[2]);
  }
  const px = (minX + maxX) / 2;
  const py = minY;
  const pz = (minZ + maxZ) / 2;
  for (const p of points) {
    p[0] = px + (p[0] - px) * scale;
    p[1] = py + (p[1] - py) * scale;
    p[2] = pz + (p[2] - pz) * scale;
  }
}

/**
 * 按模型当前高度比例整体上移（+Y）。
 * @param {number[][]} points
 * @param {number} ratio
 */
function liftVerticesByHeightRatio(points, ratio) {
  if (!ratio || !points.length) return;
  let minY = Infinity;
  let maxY = -Infinity;
  for (const p of points) {
    minY = Math.min(minY, p[1]);
    maxY = Math.max(maxY, p[1]);
  }
  const dy = (maxY - minY) * ratio;
  if (dy === 0) return;
  for (const p of points) p[1] += dy;
}

/**
 * @typedef {{
 *   bodyScaleXZ?: number;
 *   uniformScale?: number;
 *   liftByHeightRatio?: number;
 *   translate?: number[];
 *   translateBodyOnly?: boolean;
 *   garnish?: { isGarnish: (p: number[]) => boolean; scale?: number; translate?: number[] };
 * }} SmoothieMeshTuning
 */

/** @type {Record<string, SmoothieMeshTuning>} */
export const RECIPE_MESH_TUNING = {
  Web_Smoothie_Grape: { translate: HELD_CUP_TRANSLATE },
  Web_Smoothie_Orange: { translate: HELD_CUP_TRANSLATE },
  Web_Smoothie_Peach: { translate: HELD_CUP_TRANSLATE },
  Web_Smoothie_Blueberry: {
    bodyScaleXZ: 1.4,
    translate: HELD_CUP_TRANSLATE,
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
    translate: HELD_CUP_TRANSLATE,
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
    translate: HELD_CUP_TRANSLATE,
    translateBodyOnly: true,
    garnish: {
      isGarnish: (p) =>
        p[1] > 0.935 && p[0] > 0.13 && p[0] < 0.3 && Math.abs(p[2]) < 0.25,
      scale: 2.2,
      translate: [-0.02, 0.006, -0.022],
    },
  },
  Web_MilkSlush_Strawberry: { translate: HELD_CUP_TRANSLATE },
  Web_MilkSlush_Grape: { translate: HELD_CUP_TRANSLATE },
  Web_MilkSlush_Orange: { translate: HELD_CUP_TRANSLATE },
  Web_MilkSlush_Peach: { translate: HELD_CUP_TRANSLATE },
  Web_MilkSlush_Blueberry: { translate: HELD_CUP_TRANSLATE },
  Web_MilkSlush_Blackberry: { translate: HELD_CUP_TRANSLATE },
  Web_MilkSlush_Raspberry: { translate: HELD_CUP_TRANSLATE },
  Web_MilkSlush_Banana: { translate: HELD_CUP_TRANSLATE },
  Web_MilkSlush_Melon: { translate: HELD_CUP_TRANSLATE },
  Web_MilkSlush_Pineapple: { translate: HELD_CUP_TRANSLATE },
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

  if (tuning.uniformScale && tuning.uniformScale !== 1) {
    scaleVerticesUniform(points, tuning.uniformScale);
  }
  if (tuning.liftByHeightRatio) {
    liftVerticesByHeightRatio(points, tuning.liftByHeightRatio);
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
 * @param {{ recipeId: string; baseColorFile: string; referenceBounds?: typeof SMOOTHIE_REFERENCE_BOUNDS; normalizeToReference?: boolean; uniformScale?: number; liftByHeightRatio?: number; translate?: number[]; materialName?: string; meshTuning?: SmoothieMeshTuning; maxFaceCount?: number }} opts
 * @returns {{ obj: string; mtl: string; vertexCount: number; faceCount: number; sourceFaceCount: number }}
 */
export function fbxToNormalizedObj(fbxBytes, opts) {
  const {
    recipeId,
    baseColorFile,
    referenceBounds = SMOOTHIE_REFERENCE_BOUNDS,
    normalizeToReference = true,
    uniformScale = 1,
    liftByHeightRatio = 0,
    translate,
    materialName = `mat_${recipeId}`,
    meshTuning = RECIPE_MESH_TUNING[recipeId] ??
      (recipeId.startsWith("Web_MilkSlush_") ? { translate: HELD_CUP_TRANSLATE } : undefined),
    maxFaceCount = DEFAULT_MAX_FACE_COUNT,
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

  const uniqueCount = vertices.length / 3;
  const normalized = new Array(uniqueCount);
  if (normalizeToReference) {
    const src = boundsOf(vertices);
    const ref = referenceBounds;
    for (let vi = 0; vi < uniqueCount; vi++) {
      normalized[vi] = normalizeVertex(
        [vertices[vi * 3], vertices[vi * 3 + 1], vertices[vi * 3 + 2]],
        src,
        ref,
      );
    }
    applyMeshTuning(normalized, meshTuning);
  } else {
    for (let vi = 0; vi < uniqueCount; vi++) {
      normalized[vi] = [vertices[vi * 3], vertices[vi * 3 + 1], vertices[vi * 3 + 2]];
    }
  }
  if (uniformScale !== 1) scaleVerticesUniform(normalized, uniformScale);
  if (liftByHeightRatio) liftVerticesByHeightRatio(normalized, liftByHeightRatio);
  if (translate) {
    const [dx, dy, dz] = translate;
    for (const p of normalized) {
      p[0] += dx;
      p[1] += dy;
      p[2] += dz;
    }
  }

  const weldMap = new Map();
  const posList = [];
  const attrList = [];
  const triIndices = [];

  const weldCorner = (vi, ui) => {
    const key = `${vi}:${ui}`;
    let w = weldMap.get(key);
    if (w === undefined) {
      w = posList.length / 3;
      const p = normalized[vi];
      posList.push(p[0], p[1], p[2]);
      if (uvs) {
        attrList.push(uvs[ui * 2] ?? 0, uvs[ui * 2 + 1] ?? 0);
      } else {
        attrList.push(0, 0);
      }
      weldMap.set(key, w);
    }
    return w;
  };

  let corner = 0;
  let sourceFaceCount = 0;
  const polyCorners = [];

  for (const raw of polyIdx) {
    const end = raw < 0;
    const vi = end ? -raw - 1 : raw;
    const ui = uvIdx ? (uvIdx[corner] ?? 0) : 0;
    polyCorners.push(weldCorner(vi, ui));
    corner++;
    if (end) {
      if (polyCorners.length >= 3) {
        sourceFaceCount += polyCorners.length - 2;
        for (let i = 1; i < polyCorners.length - 1; i++) {
          triIndices.push(polyCorners[0], polyCorners[i], polyCorners[i + 1]);
        }
      }
      polyCorners.length = 0;
    }
  }

  const { positions: outPos, attrs: outAttr, indices: outIdx, vertexCount } = simplifyWeldedMesh(
    new Float32Array(posList),
    new Float32Array(attrList),
    new Uint32Array(triIndices),
    { maxFaces: maxFaceCount },
  );

  const objLines = [`mtllib ${recipeId}.mtl`, `o ${recipeId}`, `usemtl ${materialName}`];
  const vLines = [];
  const vtLines = [];
  for (let i = 0; i < vertexCount; i++) {
    vLines.push(
      `v ${outPos[i * 3].toFixed(6)} ${outPos[i * 3 + 1].toFixed(6)} ${outPos[i * 3 + 2].toFixed(6)}`,
    );
    vtLines.push(`vt ${outAttr[i * 2].toFixed(6)} ${outAttr[i * 2 + 1].toFixed(6)}`);
  }

  const faceLines = [];
  for (let i = 0; i < outIdx.length; i += 3) {
    const a = outIdx[i] + 1;
    const b = outIdx[i + 1] + 1;
    const c = outIdx[i + 2] + 1;
    faceLines.push(`f ${a}/${a} ${b}/${b} ${c}/${c}`);
  }
  const faceCount = outIdx.length / 3;

  const mtl = `newmtl ${materialName}\nKd 1.000 1.000 1.000\nmap_Kd ${baseColorFile}\n`;
  let obj = objLines.join("\n");
  if (vLines.length) obj += "\n" + vLines.join("\n");
  if (vtLines.length) obj += "\n" + vtLines.join("\n");
  if (faceLines.length) obj += "\n" + faceLines.join("\n");
  obj += "\n";

  return {
    obj,
    mtl,
    vertexCount,
    faceCount,
    sourceFaceCount,
  };
}
