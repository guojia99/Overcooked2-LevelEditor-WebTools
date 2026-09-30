/**
 * 使用 meshoptimizer 对焊接网格减面并压缩顶点缓冲。
 */
import { MeshoptSimplifier } from "../../web/node_modules/meshoptimizer/meshopt_simplifier.js";

let simplifierReady = false;

export async function ensureMeshSimplifier() {
  if (!simplifierReady) {
    await MeshoptSimplifier.ready;
    simplifierReady = true;
  }
}

const REMOVED = 2 ** 32 - 1;

/**
 * @param {Float32Array} positions 每顶点 3 分量
 * @param {Float32Array} attrs 每顶点 UV 等属性（2 分量）
 * @param {Uint32Array} indices 三角形索引
 */
export function compactWeldedMesh(positions, attrs, indices) {
  const idx = indices instanceof Uint32Array ? indices : new Uint32Array(indices);
  const [remap, unique] = MeshoptSimplifier.compactMesh(idx);
  const newPos = new Float32Array(unique * 3);
  const newAttr = new Float32Array(unique * 2);
  for (let old = 0; old < remap.length; old++) {
    const nw = remap[old];
    if (nw === REMOVED) continue;
    newPos[nw * 3] = positions[old * 3];
    newPos[nw * 3 + 1] = positions[old * 3 + 1];
    newPos[nw * 3 + 2] = positions[old * 3 + 2];
    newAttr[nw * 2] = attrs[old * 2];
    newAttr[nw * 2 + 1] = attrs[old * 2 + 1];
  }
  return { positions: newPos, attrs: newAttr, indices: idx, vertexCount: unique };
}

/**
 * @param {Float32Array} positions
 * @param {Float32Array} attrs UV (u,v)
 * @param {Uint32Array} indices
 * @param {{ maxFaces: number; targetError?: number }} opts
 */
export function simplifyWeldedMesh(positions, attrs, indices, opts) {
  const maxFaces = opts.maxFaces;
  const targetError = opts.targetError ?? 0.01;
  const faceCount = indices.length / 3;
  if (faceCount <= maxFaces) {
    return compactWeldedMesh(positions, attrs, indices);
  }

  const targetIndexCount = maxFaces * 3;
  const [simplified] = MeshoptSimplifier.simplifyWithAttributes(
    indices,
    positions,
    3,
    attrs,
    2,
    [1, 1],
    null,
    targetIndexCount,
    targetError,
  );

  return compactWeldedMesh(positions, attrs, simplified);
}
