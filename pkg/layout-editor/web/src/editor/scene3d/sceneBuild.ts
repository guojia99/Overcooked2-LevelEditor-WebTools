/**
 * S.items / S.floors → three 场景图的增量同步。
 *
 * draw() 在 3D 模式下只是标脏，真正的同步在这里做，且是【增量】的：
 * 只有 signature（几何/配色/选中/图层态的特征串）变化才重建 mesh，否则只更新
 * transform。原因：draw() 在全仓被调用约 200 处，拖动时每帧都会触发，若每次全量
 * 重建上千个盒子，帧率会直接塌掉。思路取自 panels.ts:309 的签名防抖。
 *
 * 纪律：一律按 _editorKey / _key 索引。historyOps.applySnapshot() 撤销时会整体
 * 替换 S.items 的对象身份，持有对象引用必然野指针。
 */

import * as THREE from "three";
import { S, isFloorLikeLayer } from "../state";
import { isActiveItemLayer, itemCategoryOf, floorCategoryOf, categoryVisible } from "../catalog";
import { itemInHeightFilter, floorInHeightFilter } from "../floorHeight";
import { isSurfaceItem } from "../../floorColors";
import { isStackHostCatalog, isStackUtensilCatalog } from "../../stacking";
import { catalogItemForGuidOrPath } from "../catalog";
import { Scene3DCtx, disposeObject } from "./ctx";
import {
  buildItemNode,
  itemGeomSignature,
  itemDecalSignature,
  applyItemDecal,
  itemBoxOf,
  beginBoxCache,
  endBoxCache,
} from "./meshItems";
import { buildFloorNode, floorSignature, floorDrawOrder } from "./meshFloors";
import { toSceneZ, toSceneRotY } from "./space";
import { rebuildOverlays } from "./overlays3d";
import { rebuildAnim } from "./anim3d";
import { rebuildHandles } from "./handles3d";

/** 物件是否参与 3D 渲染（与 2D 的可见性口径一致）。 */
export function itemVisible3D(it: Parameters<typeof itemCategoryOf>[0] & { localPosition?: { y: number } }): boolean {
  if (!categoryVisible(itemCategoryOf(it))) return false;
  return itemInHeightFilter(it as never);
}

/** 物件是否可拾取：必须在当前图层（与 hitTestAll 的图层门一致）。 */
export function itemPickable3D(it: Parameters<typeof isActiveItemLayer>[0]): boolean {
  const cat = catalogItemForGuidOrPath(it.prefabGuid, it.prefabAssetPath);
  // 地板层/背景层：表面物件可选，普通物件只作为参照。
  if (isFloorLikeLayer(S.currentLayer)) return isSurfaceItem(cat);
  if (S.currentLayer === "anim") return true;
  return isActiveItemLayer(it);
}

export function syncScene(ctx: Scene3DCtx): void {
  // 整个同步过程共享一份盒体缓存：同一物件的盒体在签名/堆叠/建节点/叠加层里
  // 会被求值四五次，缓存后每次同步只算一遍。
  beginBoxCache();
  try {
    syncItems(ctx);
    syncFloors(ctx);
    rebuildOverlays(ctx);
    rebuildAnim(ctx);
    rebuildHandles(ctx);
  } finally {
    endBoxCache();
  }
}

/**
 * 堆叠关系：厨具放在宿主（台面/灶台/搅拌台）上时，厨具盒体整个被包在宿主盒体
 * 里，3D 有深度缓冲就彻底看不见。这里识别出「宿主 ↔ 客体」两端，交给 meshItems
 * 做半透明 + 高亮线框的区分。
 *
 * 判定沿用 2D 的堆叠模型（stacking.ts）：客体是带 stack.hostRule 的厨具且抬了高，
 * 宿主是 XZ 覆盖它、且盒体顶面高于客体盒底的那件。
 */
function computeStackPairs(): { hosts: Set<string>; guests: Set<string> } {
  const hosts = new Set<string>();
  const guests = new Set<string>();

  // 单趟扫描同时收齐宿主与客体：早期版本对每个客体都重扫一遍全部物件，
  // 上千件时是 O(客体×物件) 的目录查表，属于同步卡顿的主要来源。
  const hostList: { key: string; box: ReturnType<typeof itemBoxOf> }[] = [];
  const guestList: { key: string; box: ReturnType<typeof itemBoxOf> }[] = [];
  for (const it of S.items) {
    const cat = catalogItemForGuidOrPath(it.prefabGuid, it.prefabAssetPath);
    if (isStackUtensilCatalog(cat)) {
      if ((it.localPosition?.y ?? 0) > 0.02) guestList.push({ key: it._editorKey, box: itemBoxOf(it) });
    } else if (isStackHostCatalog(cat)) {
      hostList.push({ key: it._editorKey, box: itemBoxOf(it) });
    }
  }
  if (!guestList.length || !hostList.length) return { hosts, guests };

  for (const guest of guestList) {
    const gb = guest.box;
    let best: { key: string; area: number } | null = null;
    for (const host of hostList) {
      const hb = host.box;
      // 客体盒底要落在宿主盒体的竖直区间内，才算「嵌在里面」。
      if (gb.baseY >= hb.baseY + hb.h - 1e-6) continue;
      if (Math.abs(gb.cx - hb.cx) > hb.w / 2 + 1e-6) continue;
      if (Math.abs(gb.cz - hb.cz) > hb.d / 2 + 1e-6) continue;
      const area = hb.w * hb.d;
      if (!best || area < best.area) best = { key: host.key, area };
    }
    if (best) {
      hosts.add(best.key);
      guests.add(guest.key);
    }
  }
  return { hosts, guests };
}

function syncItems(ctx: Scene3DCtx): void {
  const alive = new Set<string>();
  const { hosts, guests } = computeStackPairs();

  for (const it of S.items) {
    if (!itemVisible3D(it)) continue;
    alive.add(it._editorKey);

    const dimmed = !itemPickable3D(it);
    const hasGuest = hosts.has(it._editorKey);
    const isGuest = guests.has(it._editorKey);
    const geomSig = itemGeomSignature(it, dimmed, hasGuest, isGuest);
    const decalSig = itemDecalSignature(it, dimmed);
    const node = ctx.items.get(it._editorKey);

    if (node && node.signature === geomSig) {
      // 几何没变：只刷新 transform（拖动的快路径）。
      const box = itemBoxOf(it);
      node.group.position.set(box.cx, box.baseY, toSceneZ(box.cz));
      node.group.rotation.y = toSceneRotY(box.rotDeg);
      // 图标是异步到达的：只换顶面贴花，不重建盒体，避免加载期整片卡顿。
      if (node.decalSignature !== decalSig) {
        applyItemDecal(node.group, it, dimmed);
        node.decalSignature = decalSig;
      }
      continue;
    }

    if (node) disposeObject(node.group);
    const group = buildItemNode(it, { dimmed, hasGuest, isGuest });
    ctx.scene.add(group);
    ctx.items.set(it._editorKey, { group, signature: geomSig, decalSignature: decalSig });
  }

  for (const [key, node] of ctx.items) {
    if (alive.has(key)) continue;
    disposeObject(node.group);
    ctx.items.delete(key);
  }
}

function syncFloors(ctx: Scene3DCtx): void {
  const alive = new Set<string>();
  const order = floorDrawOrder();
  const floorLayer = isFloorLikeLayer(S.currentLayer);

  for (const f of S.floors) {
    if (!categoryVisible(floorCategoryOf(f))) continue;
    if (!floorInHeightFilter(f)) continue;
    alive.add(f._key);

    const isBg = f.surfaceKind === "background";
    // 背景地板只在背景层可选，其余层作为参照（与 renderFloors 的严格分离一致）。
    const dimmed = !floorLayer || (isBg ? S.currentLayer !== "background" : S.currentLayer === "background");
    const selected = S.selectedFloorKeys.has(f._key);
    const idx = order.get(f._key) ?? 0;
    const sig = floorSignature(f, idx, selected, dimmed);
    const node = ctx.floors.get(f._key);

    if (node && node.signature === sig) {
      node.group.position.set(f._wx, 0, toSceneZ(f._wz));
      node.group.rotation.y = toSceneRotY(f.localRotationY ?? 0);
      continue;
    }

    if (node) disposeObject(node.group);
    const group = buildFloorNode(f, { order: idx, selected, dimmed });
    ctx.scene.add(group);
    ctx.floors.set(f._key, { group, signature: sig });
  }

  for (const [key, node] of ctx.floors) {
    if (alive.has(key)) continue;
    disposeObject(node.group);
    ctx.floors.delete(key);
  }
}

/** 卸载时清空全部登记节点。 */
export function clearScene(ctx: Scene3DCtx): void {
  for (const node of ctx.items.values()) disposeObject(node.group);
  ctx.items.clear();
  for (const node of ctx.floors.values()) disposeObject(node.group);
  ctx.floors.clear();
}

/** 场景包围点集（相机 fit 用）。 */
export function scenePoints(): { x: number; z: number }[] {
  const pts: { x: number; z: number }[] = [];
  for (const f of S.floors) {
    if (f.surfaceKind === "background") continue;
    pts.push({ x: f._wx, z: f._wz });
  }
  for (const it of S.items) {
    if (!itemVisible3D(it)) continue;
    pts.push({ x: it._wx, z: it._wz });
  }
  return pts;
}

export function boundsOfScene(): THREE.Box3 | null {
  const pts = scenePoints();
  if (!pts.length) return null;
  const box = new THREE.Box3();
  for (const p of pts) box.expandByPoint(new THREE.Vector3(p.x, 0, p.z));
  return box;
}
