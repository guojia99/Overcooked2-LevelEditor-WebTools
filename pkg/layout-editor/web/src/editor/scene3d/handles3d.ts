/**
 * 3D 原生操作柄：地板四角改尺寸、空气墙/背景面改尺寸与高度、Y 轴拖动柄。
 *
 * 角柄的 edge 编码与 2D 的 hitTestFloorsAll 完全一致（"RT"/"RB"/"LT"/"LB"，
 * anchor = 对角），这样 3D 拖拽可以直接复用 dragFloorWorld 的缩放分支，
 * 不需要另写一套尺寸数学。
 */

import * as THREE from "three";
import { S, CELL, EditorFloor, EditorItem } from "../state";
import { normalizeRot } from "../coords";
import { isAirWallItem } from "../stubControls";
import { isResizableBackgroundItem } from "../catalog";
import { ORDER } from "./constants";
import { Scene3DCtx, clearGroup, floorByKey, itemByKey, tagPickable } from "./ctx";
import { floorSlabOf, gapBelowFor } from "./meshFloors";
import { itemBoxOf } from "./meshItems";
import { toSceneZ } from "./space";

const HANDLE_COLOR = 0xf9ab00;
const Y_HANDLE_COLOR = 0x63d3ff;

export type CornerEdge = "RT" | "RB" | "LT" | "LB";

/** 角柄本地偏移（与 2D 的 edge 命名一致：R=+x, T=+z）。 */
const CORNERS: { edge: CornerEdge; sx: number; sz: number }[] = [
  { edge: "RT", sx: 1, sz: 1 },
  { edge: "RB", sx: 1, sz: -1 },
  { edge: "LT", sx: -1, sz: 1 },
  { edge: "LB", sx: -1, sz: -1 },
];

export function rebuildHandles(ctx: Scene3DCtx): void {
  clearGroup(ctx.handleRoot);

  // 地板角柄：恰好选中 1 块、且不是 1×1（与 2D 规则一致）。
  if (S.selectedFloorKeys.size === 1) {
    const f = floorByKey(Array.from(S.selectedFloorKeys)[0]);
    if (f && !(f._wCells === 1 && f._dCells === 1)) addFloorHandles(ctx.handleRoot, f);
    if (f && S.yAxisDrag) addYHandle(ctx.handleRoot, f._wx, floorSlabOf(f, gapBelowFor(f)).topY, f._wz, "floor", f._key);
  }

  // 物件柄：空气墙 / 可改尺寸背景面。
  if (S.selectedKeys.size === 1) {
    const it = itemByKey(Array.from(S.selectedKeys)[0]);
    if (it) {
      if (isAirWallItem(it) || isResizableBackgroundItem(it)) addItemSizeHandles(ctx.handleRoot, it);
      if (isAirWallItem(it)) addItemTopHandle(ctx.handleRoot, it);
      if (S.yAxisDrag) {
        const b = itemBoxOf(it);
        addYHandle(ctx.handleRoot, b.cx, b.baseY + b.h, b.cz, "item", it._editorKey);
      }
    }
  }
}

function handleMesh(size: number, color: number): THREE.Mesh {
  return new THREE.Mesh(
    new THREE.BoxGeometry(size, size, size),
    new THREE.MeshBasicMaterial({ color, depthTest: false, transparent: true, opacity: 0.95 })
  );
}

function addFloorHandles(root: THREE.Group, f: EditorFloor): void {
  const slab = floorSlabOf(f, gapBelowFor(f));
  const rad = (normalizeRot(f.localRotationY) * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  const hw = slab.w / 2;
  const hd = slab.d / 2;
  const size = Math.min(0.32, Math.max(0.16, Math.min(slab.w, slab.d) * 0.12));

  for (const c of CORNERS) {
    const lx = c.sx * hw;
    const lz = c.sz * hd;
    // 本地 → 世界（与 floorLocalPoint 的逆变换一致）。
    const wx = f._wx + lx * cos - lz * sin;
    const wz = f._wz + lx * sin + lz * cos;
    const mesh = handleMesh(size, HANDLE_COLOR);
    mesh.position.set(wx, slab.topY + size * 0.4, toSceneZ(wz));
    mesh.renderOrder = ORDER.handle;
    tagPickable(mesh, {
      kind: "handle",
      key: f._key + ":" + c.edge,
      handle: { owner: "floor", ownerKey: f._key, edge: cornerToPickEdge(c.edge) },
    });
    mesh.userData.edgeCode = c.edge;
    root.add(mesh);
  }
}

function addItemSizeHandles(root: THREE.Group, it: EditorItem): void {
  const b = itemBoxOf(it);
  const rad = (-b.rotDeg * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  const size = Math.min(0.3, Math.max(0.15, Math.min(b.w, b.d) * 0.12));

  for (const c of CORNERS) {
    const lx = (c.sx * b.w) / 2;
    const lz = (c.sz * b.d) / 2;
    // three 侧 group 的旋转是 -rotDeg，这里做同样的正向旋转得到世界点。
    const wx = b.cx + lx * cos + lz * sin;
    const wz = b.cz - lx * sin + lz * cos;
    const mesh = handleMesh(size, HANDLE_COLOR);
    mesh.position.set(wx, b.baseY + b.h + size * 0.4, toSceneZ(wz));
    mesh.renderOrder = ORDER.handle;
    tagPickable(mesh, {
      kind: "handle",
      key: it._editorKey + ":" + c.edge,
      handle: { owner: "item", ownerKey: it._editorKey, edge: cornerToPickEdge(c.edge) },
    });
    mesh.userData.edgeCode = c.edge;
    root.add(mesh);
  }
}

/** 空气墙顶面柄：上下拖 = 改碰撞高度（格）。 */
function addItemTopHandle(root: THREE.Group, it: EditorItem): void {
  const b = itemBoxOf(it);
  const mesh = new THREE.Mesh(
    new THREE.ConeGeometry(0.16, 0.34, 10),
    new THREE.MeshBasicMaterial({ color: Y_HANDLE_COLOR, depthTest: false, transparent: true, opacity: 0.95 })
  );
  mesh.position.set(b.cx, b.baseY + b.h + 0.3, toSceneZ(b.cz));
  mesh.renderOrder = ORDER.handle;
  tagPickable(mesh, {
    kind: "handle",
    key: it._editorKey + ":top",
    handle: { owner: "item", ownerKey: it._editorKey, edge: "top" },
  });
  root.add(mesh);
}

/** Y 轴拖动柄（yAxisDrag 开启时显示）：竖直轴 + 顶端箭头。 */
function addYHandle(
  root: THREE.Group,
  x: number,
  y: number,
  z: number,
  owner: "floor" | "item",
  ownerKey: string
): void {
  const g = new THREE.Group();
  const shaft = new THREE.Mesh(
    new THREE.CylinderGeometry(0.035, 0.035, 1.1, 8),
    new THREE.MeshBasicMaterial({ color: Y_HANDLE_COLOR, depthTest: false, transparent: true, opacity: 0.9 })
  );
  shaft.position.y = 0.55;
  g.add(shaft);
  const tip = new THREE.Mesh(
    new THREE.ConeGeometry(0.13, 0.3, 10),
    new THREE.MeshBasicMaterial({ color: Y_HANDLE_COLOR, depthTest: false, transparent: true, opacity: 0.95 })
  );
  tip.position.y = 1.22;
  g.add(tip);
  g.position.set(x, y + 0.15, toSceneZ(z));
  g.traverse((o) => {
    o.renderOrder = ORDER.handle;
  });
  tagPickable(g, {
    kind: "handle",
    key: ownerKey + ":y",
    handle: { owner, ownerKey, edge: "y" },
  });
  root.add(g);
}

function cornerToPickEdge(edge: CornerEdge): "nw" | "ne" | "sw" | "se" {
  // R=+x, T=+z（世界系）→ 语义化方位，仅用于 Pickable 字段的可读性。
  if (edge === "RT") return "ne";
  if (edge === "RB") return "se";
  if (edge === "LT") return "nw";
  return "sw";
}

/** 从角柄 Pickable 反查 2D 口径的 edge 编码与对角锚点（世界坐标）。 */
export function cornerAnchorForFloor(f: EditorFloor, edge: CornerEdge): { x: number; z: number } {
  const rad = (normalizeRot(f.localRotationY) * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  const hw = (f._wCells * CELL) / 2;
  const hd = (f._dCells * CELL) / 2;
  // 对角：R↔L, T↔B
  const lx = edge.includes("R") ? -hw : hw;
  const lz = edge.includes("T") ? -hd : hd;
  return { x: f._wx + lx * cos - lz * sin, z: f._wz + lx * sin + lz * cos };
}
