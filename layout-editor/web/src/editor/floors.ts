import {
  S,
  CELL,
  HALF_CELL,
  EditorFloor,
  EditorItem
} from "./state";
import { snapFootprintCenter } from "../snap";
import {
  normalizeRot,
  canvasToWorld,
  newEditorKey,
  prefabIdFromPath
} from "./coords";
import { setStatus } from "./status";
import {
  draw,
  computeLevelBounds
} from "./render";
import { pushHistory } from "./historyOps";
import {
  setFloorSelection,
  clearSelection
} from "./selection";
import { hideDetail } from "./ui/overlay";
import {
  isThemeBackgroundPrefabId,
  themeBackgroundPrefabIds
} from "../floorColors";
import { tidyCatalogNameZh } from "../displayLabels";
import { catalogItemById } from "./catalog";
import { addFromCatalog } from "./items";
import type {
  CatalogItem,
  FloorMaterial
} from "../types";

export interface ThemedFloorNative {
  rotX: number;
  depthAxis: "y" | "z";
  cellsPerScaleX: number;
  cellsPerScaleZ: number;
}

/** Themed floor = a floor rect visualized by tiling a themed prefab (carpet,
 *  ice, snow, …) at write-back time, instead of a single Plane + material. */
export function isThemedFloor(f: EditorFloor): boolean {
  return !!f.prefabGuid && f.surfaceKind !== "raft";
}

/** 空气地板：仅有可行走 Col_AirFloor 碰撞盒，无可见 Plane。 */
export function isAirFloor(f: EditorFloor): boolean {
  return !!f.airFloor;
}

/** Ids that are clearly NOT area floor surfaces and must stay out of themed
 *  floor management: walkway pillars / roofs / ropes, the Red Carpet Entrance
 *  piece, and beach floor corner/edge trims (stretching them over an N×M rect
 *  makes no sense). They remain regular floor-layer items. */
export const THEMED_FLOOR_NOT_FLOOR_ID = /pillar|roof|rope|entrance|corner|edge|walkway/i;

/** True for catalog prefabs that can back a themed floor (excludes raft planks,
 *  conveyors, decals, floor sections and non-floor pieces like pillars/roofs). */
export function isThemedFloorPrefab(it: CatalogItem | undefined): boolean {
  if (!it || it.surfaceTier !== "floor") return false;
  const k = it.surfaceKind;
  if (k === "raft" || k === "conveyor" || k === "decal" || k === "section") return false;
  if (THEMED_FLOOR_NOT_FLOOR_ID.test(it.id)) return false;
  return true;
}

/** Native geometry per themed floor prefab (extracted from its bundle).
 *  The game's PseudoPrefab system spawns the real bundle prefab via
 *  `Instantiate(prefab, position, rotation, parent)` — the prefab root's
 *  baked ROTATION is discarded and replaced by the wrapper's world rotation;
 *  only its local SCALE survives. Consequences per prefab family:
 *  - Quad tiles (ice/snow/alien/carpet): the quad stands vertical at identity,
 *    so the wrapper MUST carry rotX=90 to lie flat; wrapper scale.x=width,
 *    scale.y=depth (the baked 1.2 scale makes wrapper scale 1 = one 1.2m cell).
 *  - Flat meshes (sand): already lie in XZ, so the wrapper stays at
 *    rotX=0 (rotX=90 would stand them up); wrapper scale.x=width, scale.z=depth.
 *  cellsPerScaleX/Z: footprint in cells per unit of wrapper scale. */
export const THEMED_FLOOR_NATIVE: Record<string, ThemedFloorNative> = {
  ice_floor_01: { rotX: 90, depthAxis: "y", cellsPerScaleX: 1, cellsPerScaleZ: 1 },
  snow_floor_01: { rotX: 90, depthAxis: "y", cellsPerScaleX: 1, cellsPerScaleZ: 1 },
  alien_floor_tile_01: { rotX: 90, depthAxis: "y", cellsPerScaleX: 1, cellsPerScaleZ: 1 },
  floor_carpet_purple: { rotX: 90, depthAxis: "y", cellsPerScaleX: 1, cellsPerScaleZ: 1 },
  sand_floor_01: { rotX: 0, depthAxis: "z", cellsPerScaleX: 1, cellsPerScaleZ: 1 },
  // OC1 大型地面（露营地地面）：整块平铺在 XZ 的巨型地面，原生尺寸 34×21 格
  dlc5_ground_camp: { rotX: 0, depthAxis: "z", cellsPerScaleX: 34, cellsPerScaleZ: 21 },
};

export const THEMED_FLOOR_NATIVE_DEFAULT: ThemedFloorNative = {
  rotX: 90,
  depthAxis: "y",
  cellsPerScaleX: 1,
  cellsPerScaleZ: 1,
};

export function themedFloorNative(cat: CatalogItem): ThemedFloorNative {
  return THEMED_FLOOR_NATIVE[cat.id] ?? THEMED_FLOOR_NATIVE_DEFAULT;
}

/** All catalog prefabs usable as themed floor tiles (excludes raft planks,
 *  conveyors and decals — those have their own workflows). */
export function themedFloorPrefabs(): CatalogItem[] {
  const list: CatalogItem[] = [];
  for (const it of S.catalogByGuid.values()) {
    if (isThemedFloorPrefab(it)) list.push(it);
  }
  return list.sort((a, b) => a.id.localeCompare(b.id));
}


export function dragFloor(f: EditorFloor, mx: number, my: number) {
  const { x: wx, z: wz } = canvasToWorld(mx, my);
  if (S.dragFloorMode === "move") {
    if (f.surfaceKind === "raft") {
      f._wx = wx;
      f._wz = wz;
      snapRaftCenterToGrid(f);
    } else {
      const snapped = snapFootprintCenter(wx, wz, f._wCells, f._dCells, f.localRotationY ?? 0, CELL, HALF_CELL);
      f._wx = snapped.x;
      f._wz = snapped.z;
    }
  } else {
    // Resize: opposite corner (anchor) stays fixed; compute new width/depth.
    const rad = (normalizeRot(f.localRotationY) * Math.PI) / 180;
    const cos = Math.cos(rad);
    const sin = Math.sin(rad);
    const dx = wx - S.dragFloorAnchorX;
    const dz = wz - S.dragFloorAnchorZ;
    const lx = dx * cos + dz * sin;
    const lz = -dx * sin + dz * cos;
    const newW = Math.max(1, Math.round((Math.abs(lx) / CELL) || 1));
    const newD = Math.max(1, Math.round((Math.abs(lz) / CELL) || 1));
    // Recenter so the anchor corner stays put.
    const signX = S.dragFloorEdge.includes("R") ? 1 : -1;
    const signZ = S.dragFloorEdge.includes("T") ? 1 : -1;
    const newCxLocal = signX * (newW * CELL) / 2;
    const newCzLocal = signZ * (newD * CELL) / 2;
    f._wx = S.dragFloorAnchorX + newCxLocal * cos - newCzLocal * sin;
    f._wz = S.dragFloorAnchorZ + newCxLocal * sin + newCzLocal * cos;
    f._wCells = newW;
    f._dCells = newD;
  }
}

export function snapRaftCenterToGrid(f: EditorFloor): void {
  if (f.surfaceKind !== "raft") return;
  const ox = S.gridInfo?.found ? S.gridInfo.worldPosition.x : 0;
  const oz = S.gridInfo?.found ? S.gridInfo.worldPosition.z : 0;
  const minX = f._wx - ((f._wCells - 1) / 2) * CELL;
  const minZ = f._wz - ((f._dCells - 1) / 2) * CELL;
  const sMinX = ox + Math.round((minX - ox) / CELL) * CELL;
  const sMinZ = oz + Math.round((minZ - oz) / CELL) * CELL;
  f._wx = sMinX + ((f._wCells - 1) / 2) * CELL;
  f._wz = sMinZ + ((f._dCells - 1) / 2) * CELL;
}

export function finalizeFloor(f: EditorFloor) {
  if (f.surfaceKind === "raft") {
    // Raft planks snap to the global CELL grid instead of the half-cell step.
    snapRaftCenterToGrid(f);
  } else {
    // Snap by footprint edges (same as items) so adjacent floors share a common grid edge
    // instead of independently rounding centers, which can create half-cell overlaps/gaps.
    const snapped = snapFootprintCenter(
      f._wx,
      f._wz,
      f._wCells,
      f._dCells,
      f.localRotationY ?? 0,
      CELL,
      HALF_CELL
    );
    f._wx = snapped.x;
    f._wz = snapped.z;
  }
  f.localPosition.x = f._wx;
  f.localPosition.z = f._wz;
  f.worldPosition.x = f._wx;
  f.worldPosition.z = f._wz;
  f.widthUnits = f._wCells * CELL;
  f.depthUnits = f._dCells * CELL;
  // Smart material match by size tag.
  if (f.surfaceKind !== "background" && f.surfaceKind !== "raft" && !f.prefabGuid && !f.airFloor)
    tryMatchFloorMaterialBySize(f);
}

export function tryMatchFloorMaterialBySize(f: EditorFloor) {
  const tag = `${f._wCells}x${f._dCells}`;
  const match = S.floorMaterials.find((m) => m.sizeTag === tag);
  if (match && match.guid !== f.materialGuid) {
    f.materialGuid = match.guid;
    f.materialAssetPath = match.assetPath;
    f.materialName = match.id;
    setStatus(`已按尺寸 ${tag} 自动匹配材质：${match.nameZh}`);
  }
}

export function pointInWalkable(wx: number, wz: number): boolean {
  for (const r of S.walkable) {
    if (Math.abs(wx - r.cx) <= r.sx / 2 + 0.01 && Math.abs(wz - r.cz) <= r.sz / 2 + 0.01)
      return true;
  }
  return false;
}

export function addFloorAt(wx: number, wz: number, themedCat?: CatalogItem | null) {
  pushHistory();
  const id = `new:floor:${crypto.randomUUID()}`;
  const key = newEditorKey();
  const w = 4;
  const d = 4;
  const snapped = snapFootprintCenter(wx, wz, w, d, 0, CELL, HALF_CELL);
  const defaultMat = S.floorMaterials.find((m) => /floor|blacktiles|path/i.test(m.id));
  const floor: EditorFloor = {
    instanceId: id,
    _key: key,
    hierarchyPath: id,
    parentPath: themedCat ? themedCat.defaultParent || "Art" : "Art/Ground",
    displayName: themedCat ? themedCat.id : "Floor",
    surfaceKind: themedCat ? (themedCat.surfaceKind ?? "solid") : "solid",
    meshType: themedCat ? "prefab" : "plane",
    meshFileId: themedCat ? 0 : 10209,
    prefabGuid: themedCat?.guid,
    prefabAssetPath: themedCat?.assetPath,
    materialGuid: themedCat ? undefined : defaultMat?.guid,
    materialAssetPath: themedCat ? undefined : defaultMat?.assetPath,
    materialName: themedCat ? undefined : defaultMat?.id,
    localPosition: { x: snapped.x, y: themedCat ? 0.01 : -0.05, z: snapped.z },
    worldPosition: { x: snapped.x, y: themedCat ? 0.01 : -0.05, z: snapped.z },
    localRotationY: 0,
    localScale: { x: (w * CELL) / 10, y: 1, z: (d * CELL) / 10 },
    widthUnits: w * CELL,
    depthUnits: d * CELL,
    widthCells: w,
    depthCells: d,
    _wx: snapped.x,
    _wz: snapped.z,
    _wCells: w,
    _dCells: d,
  };
  S.floors.push(floor);
  setFloorSelection([key]);
  draw();
  setStatus(
    themedCat
      ? `已新增主题地板：${tidyCatalogNameZh(themedCat.nameZh, themedCat.id)}（写回时生成单个缩放实例）`
      : "已新增地板（写回后生效）"
  );
}

export function addAirFloorAt(wx: number, wz: number) {
  pushHistory();
  const id = `new:floor:${crypto.randomUUID()}`;
  const key = newEditorKey();
  const w = 4;
  const d = 4;
  const snapped = snapFootprintCenter(wx, wz, w, d, 0, CELL, HALF_CELL);
  const floor: EditorFloor = {
    instanceId: id,
    _key: key,
    hierarchyPath: id,
    parentPath: "Design/Collision",
    displayName: "AirFloor",
    surfaceKind: "solid",
    meshType: "plane",
    meshFileId: 0,
    airFloor: true,
    localPosition: { x: snapped.x, y: 0, z: snapped.z },
    worldPosition: { x: snapped.x, y: 0, z: snapped.z },
    localRotationY: 0,
    localScale: { x: 1, y: 1, z: 1 },
    widthUnits: w * CELL,
    depthUnits: d * CELL,
    widthCells: w,
    depthCells: d,
    _wx: snapped.x,
    _wz: snapped.z,
    _wCells: w,
    _dCells: d,
  };
  S.floors.push(floor);
  setFloorSelection([key]);
  draw();
  setStatus("已新增空气地板（仅可行走，无可见地板，写回后生效）");
}

export function floorMatSummary(f: EditorFloor, matchedMat: FloorMaterial | undefined): string {
  if (f.airFloor) return "空气地板（仅可行走，无可见地板）";
  if (f.surfaceKind === "raft") return "木筏拼块（写回时生成）";
  if (isThemedFloor(f)) {
    const cat = S.catalogByGuid.get(f.prefabGuid!);
    return `主题地板（${cat ? tidyCatalogNameZh(cat.nameZh, cat.id) : (f.materialName ?? f.prefabGuid)} · 写回时生成单个缩放实例）`;
  }
  if (f.imageMode || f.imageTexturePath) {
    const rot = normalizeRot(f.imageRotation ?? 0);
    const rotTxt = rot ? ` · 旋转${rot}°` : "";
    return f.imageTexturePath
      ? `图片地板（${f.imageMode === "tile" ? "一格平铺" : "全部铺开"}${rotTxt} · ${f.imageTexturePath.split("/").pop() ?? ""}）`
      : "图片地板（未上传图片）";
  }
  if (f.tintEnabled) return `染色地板（颜色 ${f.tintColor ?? "#ffffff"}）`;
  return matchedMat?.nameZh ?? f.materialName ?? "无";
}

export function mergeRaftItemsIntoFloors(): void {
  const raftOf = (it: EditorItem) => S.catalogByGuid.get(it.prefabGuid)?.surfaceKind === "raft";
  const catalogId = (it: EditorItem) => S.catalogByGuid.get(it.prefabGuid)?.id ?? "";
  const isPrimary = (it: EditorItem) => catalogId(it) === "raft_raft_middle_01";
  const rafts = S.items.filter(raftOf);
  if (rafts.length === 0) return;

  const TOL = 0.05;
  /** Secondary↔primary nearest distance in official scenes is ~0.82–0.86. */
  const SECONDARY_LINK = 0.92;
  const parent = rafts.map((_, i) => i);
  const find = (a: number): number => {
    while (parent[a] !== a) {
      parent[a] = parent[parent[a]];
      a = parent[a];
    }
    return a;
  };
  const union = (a: number, b: number) => {
    parent[find(a)] = find(b);
  };
  for (let i = 0; i < rafts.length; i++) {
    for (let j = i + 1; j < rafts.length; j++) {
      const dx = Math.abs(rafts[i]._wx - rafts[j]._wx);
      const dz = Math.abs(rafts[i]._wz - rafts[j]._wz);
      if (Math.abs(dx - CELL) < TOL && dz < TOL) union(i, j);
      else if (Math.abs(dz - CELL) < TOL && dx < TOL) union(i, j);
      else if (Math.hypot(dx, dz) < SECONDARY_LINK) union(i, j);
    }
  }
  const clusters = new Map<number, EditorItem[]>();
  for (let i = 0; i < rafts.length; i++) {
    const root = find(i);
    const arr = clusters.get(root) ?? [];
    arr.push(rafts[i]);
    clusters.set(root, arr);
  }

  const trySolidRect = (
    lattice: EditorItem[]
  ): { w: number; d: number; cx: number; cz: number } | null => {
    if (lattice.length < 1) return null;
    const minPosX = Math.min(...lattice.map((it) => it._wx));
    const maxPosX = Math.max(...lattice.map((it) => it._wx));
    const minPosZ = Math.min(...lattice.map((it) => it._wz));
    const maxPosZ = Math.max(...lattice.map((it) => it._wz));
    const cells = new Set<string>();
    for (const it of lattice) {
      const fi = (it._wx - minPosX) / CELL;
      const fj = (it._wz - minPosZ) / CELL;
      if (Math.abs(fi - Math.round(fi)) > TOL / CELL || Math.abs(fj - Math.round(fj)) > TOL / CELL) {
        return null;
      }
      cells.add(`${Math.round(fi)},${Math.round(fj)}`);
    }
    const w = Math.round((maxPosX - minPosX) / CELL) + 1;
    const d = Math.round((maxPosZ - minPosZ) / CELL) + 1;
    if (cells.size !== lattice.length || cells.size !== w * d) return null;
    return {
      w,
      d,
      cx: (minPosX + maxPosX) / 2,
      cz: (minPosZ + maxPosZ) / 2,
    };
  };

  for (const cluster of clusters.values()) {
    if (cluster.length < 2) continue;

    const primaries = cluster.filter(isPrimary);
    let rect = trySolidRect(primaries);
    // Fallback: older single-lattice saves (back/front/middle all on one CELL grid).
    if (!rect) rect = trySolidRect(cluster);
    if (!rect) continue;
    // Require at least 2 lattice cells, or a dual-lattice cluster with secondaries.
    if (rect.w * rect.d < 2 && cluster.length < 2) continue;

    const removeKeys = new Set(cluster.map((it) => it._editorKey));
    S.items = S.items.filter((it) => !removeKeys.has(it._editorKey));

    const { w, d, cx: cxW, cz: czW } = rect;
    const key = newEditorKey();
    S.floors.push({
      instanceId: `new:raft:${crypto.randomUUID()}`,
      _key: key,
      hierarchyPath: `raft:${key}`,
      parentPath: "Art",
      displayName: "Raft",
      surfaceKind: "raft",
      meshType: "plane",
      meshFileId: 10209,
      localPosition: { x: cxW, y: -0.05, z: czW },
      worldPosition: { x: cxW, y: -0.05, z: czW },
      localRotationY: 0,
      localScale: { x: (w * CELL) / 10, y: 1, z: (d * CELL) / 10 },
      widthUnits: w * CELL,
      depthUnits: d * CELL,
      widthCells: w,
      depthCells: d,
      _wx: cxW,
      _wz: czW,
      _wCells: w,
      _dCells: d,
    });
  }
}

export function mergeThemedItemsIntoFloors(): void {
  const consumed = new Set<string>();
  for (const it of S.items) {
    const cat = S.catalogByGuid.get(it.prefabGuid);
    if (!isThemedFloorPrefab(cat)) continue;
    const c = cat!;

    // Instance scale is in the prefab's native units (cells, not meters — see
    // THEMED_FLOOR_NATIVE): quad tiles use scale.x=width / scale.y=depth with
    // rotX=90; flat meshes (sand / walkway) use scale.x=width / scale.z=depth.
    const nat = themedFloorNative(c);
    const sx = Math.abs(it.localScale?.x ?? 1) || 1;
    const sy = Math.abs(it.localScale?.y ?? 1) || 1;
    const sz = Math.abs(it.localScale?.z ?? 1) || 1;
    const rotX = it.localRotationX ?? nat.rotX;
    const w = Math.max(1, Math.round(sx * nat.cellsPerScaleX));
    const d = Math.max(
      1,
      Math.round((nat.depthAxis === "y" ? sy : sz) * nat.cellsPerScaleZ)
    );

    const key = newEditorKey();
    const y = it.localPosition?.y ?? 0;
    S.floors.push({
      instanceId: `new:themed:${crypto.randomUUID()}`,
      _key: key,
      hierarchyPath: `themed:${key}`,
      parentPath: c.defaultParent || "Art",
      displayName: c.id,
      surfaceKind: c.surfaceKind ?? "solid",
      meshType: "prefab",
      prefabGuid: c.guid,
      prefabAssetPath: c.assetPath,
      prefabScale: { x: sx, y: sy, z: sz },
      prefabScaleCellsW: w,
      prefabScaleCellsD: d,
      prefabRotX: rotX,
      localPosition: { x: it._wx, y, z: it._wz },
      worldPosition: { x: it._wx, y, z: it._wz },
      localRotationY: normalizeRot(it.localRotationY),
      localScale: { x: (w * CELL) / 10, y: 1, z: (d * CELL) / 10 },
      widthUnits: w * CELL,
      depthUnits: d * CELL,
      widthCells: w,
      depthCells: d,
      _wx: it._wx,
      _wz: it._wz,
      _wCells: w,
      _dCells: d,
    });
    consumed.add(it._editorKey);
  }
  if (consumed.size > 0) {
    S.items = S.items.filter((it) => !consumed.has(it._editorKey));
  }
}

export function removeBackgroundFloors(): number {
  const before = S.floors.length;
  S.floors = S.floors.filter((f) => f.surfaceKind !== "background");
  const alive = new Set(S.floors.map((f) => f._key));
  if ([...S.selectedFloorKeys].some((k) => !alive.has(k))) {
    setFloorSelection([...S.selectedFloorKeys].filter((k) => alive.has(k)));
  }
  return before - S.floors.length;
}

export function syncBackgroundForTheme(themeKey: string) {
  const wanted = themeBackgroundPrefabIds(themeKey);
  const wantedSet = new Set(wanted);

  // Drop theme-managed prefabs that don't match the selected theme.
  const removedItemKeys: string[] = [];
  S.items = S.items.filter((it) => {
    const pid = prefabIdFromPath(it.prefabAssetPath);
    if (!isThemeBackgroundPrefabId(pid)) return true;
    if (wantedSet.has(pid)) return true;
    removedItemKeys.push(it._editorKey);
    return false;
  });
  if (removedItemKeys.some((k) => S.selectedKeys.has(k))) {
    clearSelection();
    hideDetail();
  }

  // Environment backdrop planes (e.g. Art/raft_water/sky) are replaced by theme prefabs.
  // Only remove them when there is a concrete theme prefab to take their place;
  // the void theme has no background prefab, so hand-authored background floors
  // must be kept instead of being permanently deleted.
  const removedFloors = wanted.length > 0 ? removeBackgroundFloors() : 0;

  if (wanted.length === 0) {
    if (removedFloors > 0) draw();
    return;
  }

  const pid = wanted[0];
  const exists = S.items.some((it) => prefabIdFromPath(it.prefabAssetPath) === pid);
  if (exists) {
    if (removedFloors > 0) draw();
    return;
  }

  const bounds = computeLevelBounds();
  const wx = bounds?.cx ?? 0;
  const wz = (bounds?.cz ?? 0) + 2 * CELL;
  const cat = catalogItemById(pid);
  if (!cat) return;
  addFromCatalog(cat, wx, wz, false);
  setStatus(`已切换背景环境：${tidyCatalogNameZh(cat.nameZh, cat.id)}（${pid}）`);
  draw();
}
