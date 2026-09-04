/** Raft plank dual-lattice layout matching official Overcooked raft scenes.
 *
 * Primary lattice: raft_raft_middle_01 on a CELL grid (visual body).
 * Secondary lattice: back/front/middle_02/03 at half-cell diagonal offsets
 * so log meshes interlock without the large voids of a single shared lattice.
 *
 * Offsets use exactly CELL/2 so Unity half-cell snap (0.6) does not drift positions.
 */

export const RAFT_CELL = 1.2;
export const RAFT_ROT_Y = 90;
const HALF = RAFT_CELL / 2;

export type RaftPiece = { id: string; dx: number; dz: number; rotY: number };

const SECONDARY_OFFSETS: ReadonlyArray<readonly [number, number]> = [
  [HALF, HALF],
  [HALF, -HALF],
  [-HALF, HALF],
  [-HALF, -HALF],
];

function posKey(dx: number, dz: number): string {
  return `${dx.toFixed(3)},${dz.toFixed(3)}`;
}

function addPiece(
  out: RaftPiece[],
  used: Set<string>,
  id: string,
  dx: number,
  dz: number
): void {
  const key = posKey(dx, dz);
  if (used.has(key)) return;
  used.add(key);
  out.push({ id, dx, dz, rotY: RAFT_ROT_Y });
}

/** Pick secondary prefab by position within the primary AABB. */
function pickSecondaryId(
  dx: number,
  dz: number,
  minX: number,
  maxX: number,
  minZ: number,
  maxZ: number,
  wCells: number,
  dCells: number
): string {
  const edgeTol = RAFT_CELL * 0.45;
  const nearNegX = dx - minX <= edgeTol;
  const nearPosX = maxX - dx <= edgeTol;
  const nearNegZ = dz - minZ <= edgeTol;
  const nearPosZ = maxZ - dz <= edgeTol;

  if (wCells > 1 && nearNegX && !nearPosX) return "raft_raft_back_01";
  if (wCells > 1 && nearPosX && !nearNegX) return "raft_raft_front_01";
  if (nearNegZ || nearPosZ) return "raft_raft_middle_03";

  const h =
    (((Math.round(dx * 100) * 31 + Math.round(dz * 100) * 17 + wCells * 7 + dCells * 13) %
      10) +
      10) %
    10;
  return h < 3 ? "raft_raft_middle_03" : "raft_raft_middle_02";
}

/**
 * Raft plank layout for a W×D raft floor (relative to floor center).
 * Returns primary middle_01 cells plus interlocking secondary pieces.
 */
export function raftPiecesForRect(wCells: number, dCells: number): RaftPiece[] {
  const w = Math.max(1, Math.floor(wCells));
  const d = Math.max(1, Math.floor(dCells));
  const out: RaftPiece[] = [];
  const used = new Set<string>();

  const ox = -((w - 1) / 2) * RAFT_CELL;
  const oz = -((d - 1) / 2) * RAFT_CELL;
  const minX = ox;
  const maxX = ox + (w - 1) * RAFT_CELL;
  const minZ = oz;
  const maxZ = oz + (d - 1) * RAFT_CELL;

  // 1) Primary lattice — solid middle_01 fill.
  for (let i = 0; i < w; i++) {
    for (let j = 0; j < d; j++) {
      addPiece(out, used, "raft_raft_middle_01", ox + i * RAFT_CELL, oz + j * RAFT_CELL);
    }
  }

  // 2) Secondary interlocking lattice at diagonal half-offsets.
  const expand = HALF + 0.05;
  const sMinX = minX - expand;
  const sMaxX = maxX + expand;
  const sMinZ = minZ - expand;
  const sMaxZ = maxZ + expand;

  for (let i = 0; i < w; i++) {
    for (let j = 0; j < d; j++) {
      const px = ox + i * RAFT_CELL;
      const pz = oz + j * RAFT_CELL;
      for (const [sx, sz] of SECONDARY_OFFSETS) {
        const dx = px + sx;
        const dz = pz + sz;
        if (dx < sMinX - 0.01 || dx > sMaxX + 0.01 || dz < sMinZ - 0.01 || dz > sMaxZ + 0.01) {
          continue;
        }
        const id = pickSecondaryId(dx, dz, minX, maxX, minZ, maxZ, w, d);
        addPiece(out, used, id, dx, dz);
      }
    }
  }

  return out;
}

/** Count primary (middle_01) pieces — equals wCells * dCells for a valid layout. */
export function raftPrimaryCount(pieces: RaftPiece[]): number {
  return pieces.filter((p) => p.id === "raft_raft_middle_01").length;
}

/** Assert layout invariants used by verify checks. */
export function assertRaftLayout(wCells: number, dCells: number, pieces: RaftPiece[]): void {
  const primary = raftPrimaryCount(pieces);
  if (primary !== wCells * dCells) {
    throw new Error(`raft primary count ${primary} != ${wCells * dCells}`);
  }
  const secondary = pieces.length - primary;
  if (wCells * dCells > 1 && secondary <= 0) {
    throw new Error("raft dual lattice produced no secondary pieces");
  }
  const keys = new Set(pieces.map((p) => posKey(p.dx, p.dz)));
  if (keys.size !== pieces.length) {
    throw new Error("raft layout has duplicate positions");
  }
}
