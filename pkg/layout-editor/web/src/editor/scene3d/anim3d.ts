/**
 * 动画组 3D 可视化：路点球 + 路线折线 + 方向箭头 + 成员标记 + 预览位姿。
 *
 * 与 2D 的 drawAnimControlOverlay 对等，但路点是真正的 3D 可拾取实体
 * （tagPickable → picking → input3d 拖动/删除），满足「路点必须能在 3D 视口里
 * 直接放置和拖动」的需求。
 */

import * as THREE from "three";
import { S, CELL } from "../state";
import { previewMemberPositions } from "../animControl";
import { floorWalkY } from "../floorHeight";
import { ORDER } from "./constants";
import { Scene3DCtx, clearGroup, tagPickable } from "./ctx";
import { labelTexture } from "./materials";
import { itemBoxOf } from "./meshItems";
import { toSceneZ, toSceneRotY } from "./space";

/** 路点球半径：与 2D 的 WAYPOINT_HIT_RADIUS(CELL*0.5) 同口径，保证手感一致。 */
export const WAYPOINT_RADIUS_3D = CELL * 0.32;
/** 路点悬浮高度：抬离地面便于在 3D 里点中。 */
export const WAYPOINT_Y = 0.5;

export function rebuildAnim(ctx: Scene3DCtx): void {
  clearGroup(ctx.animRoot);
  if (S.currentLayer !== "anim") return;

  const root = ctx.animRoot;
  const active = S.animControls.find((g) => g.id === S.activeAnimGroupId) ?? null;
  const groups = active ? [active] : S.animControls;

  for (const g of groups) {
    const isActive = active != null && g.id === active.id;
    const alpha = isActive ? 1 : 0.35;

    // 路线：按事件顺序把路点连成折线（与 2D 的虚线路线同义）。
    for (const evt of g.events) {
      const ids = evt.waypointIds ?? [];
      if (ids.length < 2) continue;
      const pts: THREE.Vector3[] = [];
      for (const id of ids) {
        const wp = g.waypoints.find((w) => w.id === id);
        if (wp) pts.push(new THREE.Vector3(wp.x, WAYPOINT_Y, toSceneZ(wp.z)));
      }
      if (pts.length < 2) continue;

      const geo = new THREE.BufferGeometry().setFromPoints(pts);
      const line = new THREE.Line(
        geo,
        new THREE.LineDashedMaterial({
          color: 0x8ab4f8,
          transparent: true,
          opacity: 0.85 * alpha,
          dashSize: 0.3,
          gapSize: 0.2,
          depthTest: false,
        })
      );
      line.computeLineDistances();
      line.renderOrder = ORDER.route;
      line.userData.noPick = true;
      root.add(line);

      // 段中点方向箭头
      for (let i = 0; i + 1 < pts.length; i++) {
        root.add(segmentArrow(pts[i], pts[i + 1], 0x8ab4f8, alpha));
      }
      // 循环标记
      if ((evt.loop || evt.pingpong) && pts.length >= 2) {
        root.add(loopBadge(pts[pts.length - 1], evt.pingpong ? "⇄" : "↻", alpha));
      }
    }

    // 路点球
    g.waypoints.forEach((wp, idx) => {
      const selected = S.selectedWaypointId === wp.id;
      const mesh = new THREE.Mesh(
        new THREE.SphereGeometry(WAYPOINT_RADIUS_3D, 18, 12),
        new THREE.MeshLambertMaterial({
          color: selected ? 0xf9ab00 : 0x8ab4f8,
          transparent: true,
          opacity: 0.92 * alpha,
          depthTest: false,
        })
      );
      mesh.position.set(wp.x, WAYPOINT_Y, toSceneZ(wp.z));
      mesh.renderOrder = ORDER.waypoint;
      if (isActive) {
        tagPickable(mesh, { kind: "waypoint", key: wp.id });
      } else {
        mesh.userData.noPick = true;
      }
      root.add(mesh);

      // 序号 + 竖直投影线（帮助读出路点在地面的位置）
      const tex = labelTexture(String(idx + 1));
      if (tex) {
        const canvas = tex.image as HTMLCanvasElement;
        const sprite = new THREE.Sprite(
          new THREE.SpriteMaterial({ map: tex, transparent: true, depthTest: false, opacity: alpha })
        );
        const h = 0.3;
        sprite.scale.set((h * canvas.width) / canvas.height, h, 1);
        sprite.position.set(wp.x, WAYPOINT_Y + WAYPOINT_RADIUS_3D + 0.24, toSceneZ(wp.z));
        sprite.renderOrder = ORDER.label;
        sprite.userData.noPick = true;
        root.add(sprite);
      }
      root.add(dropLine(wp.x, toSceneZ(wp.z), WAYPOINT_Y, alpha));
    });

    // 成员标记：在成员顶部画一个色环，指示它归属当前动画组
    if (isActive) addMemberMarkers(root, g.itemInstanceIds, g.floorInstanceIds);
  }

  // 预览：把成员画到模拟位姿上（半透明幽灵）
  addPreviewGhosts(root);

  root.traverse((o) => {
    if (o.userData.pick == null) o.userData.noPick = true;
  });
}

function segmentArrow(a: THREE.Vector3, b: THREE.Vector3, color: number, alpha: number): THREE.Mesh {
  const dir = b.clone().sub(a);
  const len = dir.length();
  dir.normalize();
  const cone = new THREE.Mesh(
    new THREE.ConeGeometry(0.1, 0.26, 8),
    new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.9 * alpha, depthTest: false })
  );
  cone.position.copy(a.clone().addScaledVector(dir, len / 2));
  cone.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir);
  cone.renderOrder = ORDER.route;
  cone.userData.noPick = true;
  return cone;
}

function loopBadge(at: THREE.Vector3, glyph: string, alpha: number): THREE.Object3D {
  const tex = labelTexture(glyph);
  if (!tex) return new THREE.Object3D();
  const canvas = tex.image as HTMLCanvasElement;
  const sprite = new THREE.Sprite(
    new THREE.SpriteMaterial({ map: tex, transparent: true, depthTest: false, opacity: alpha })
  );
  const h = 0.36;
  sprite.scale.set((h * canvas.width) / canvas.height, h, 1);
  sprite.position.set(at.x, at.y + 0.55, at.z);
  sprite.renderOrder = ORDER.label;
  sprite.userData.noPick = true;
  return sprite;
}

function dropLine(x: number, z: number, y: number, alpha: number): THREE.Line {
  const geo = new THREE.BufferGeometry().setFromPoints([
    new THREE.Vector3(x, 0.01, z),
    new THREE.Vector3(x, y, z),
  ]);
  const line = new THREE.Line(
    geo,
    new THREE.LineBasicMaterial({ color: 0x8ab4f8, transparent: true, opacity: 0.35 * alpha, depthTest: false })
  );
  line.renderOrder = ORDER.route;
  line.userData.noPick = true;
  return line;
}

function addMemberMarkers(root: THREE.Group, itemIds: string[], floorIds: string[]): void {
  const idSet = new Set(itemIds);
  for (const it of S.items) {
    if (!idSet.has(it.instanceId)) continue;
    const b = itemBoxOf(it);
    const ring = new THREE.Mesh(
      new THREE.RingGeometry(Math.max(0.2, Math.min(b.w, b.d) * 0.4), Math.max(0.3, Math.min(b.w, b.d) * 0.52), 20),
      new THREE.MeshBasicMaterial({
        color: 0x8ab4f8,
        transparent: true,
        opacity: 0.85,
        side: THREE.DoubleSide,
        depthTest: false,
      })
    );
    ring.rotation.x = -Math.PI / 2;
    ring.position.set(b.cx, b.baseY + b.h + 0.06, toSceneZ(b.cz));
    ring.renderOrder = ORDER.route;
    ring.userData.noPick = true;
    root.add(ring);
  }

  const floorSet = new Set(floorIds);
  for (const f of S.floors) {
    if (!floorSet.has(f.instanceId)) continue;
    const ring = new THREE.Mesh(
      new THREE.RingGeometry(0.3, 0.44, 20),
      new THREE.MeshBasicMaterial({
        color: 0x63d3ff,
        transparent: true,
        opacity: 0.85,
        side: THREE.DoubleSide,
        depthTest: false,
      })
    );
    ring.rotation.x = -Math.PI / 2;
    ring.position.set(f._wx, floorWalkY(f) + 0.05, toSceneZ(f._wz));
    ring.renderOrder = ORDER.route;
    ring.userData.noPick = true;
    root.add(ring);
  }
}

/** 预览播放：成员在模拟位姿上画半透明幽灵盒（真实物件仍在原位）。 */
function addPreviewGhosts(root: THREE.Group): void {
  const poses = previewMemberPositions();
  if (!poses) return;
  for (const it of S.items) {
    const pose = poses.get(it.instanceId);
    if (!pose) continue;
    const b = itemBoxOf(it);
    const ghost = new THREE.Mesh(
      new THREE.BoxGeometry(b.w, b.h, b.d),
      new THREE.MeshLambertMaterial({ color: 0xf9ab00, transparent: true, opacity: 0.42 })
    );
    ghost.position.set(pose.x, (pose.y ?? b.baseY) + b.h / 2, toSceneZ(pose.z));
    ghost.rotation.y = toSceneRotY(pose.rotY ?? b.rotDeg);
    ghost.renderOrder = ORDER.item;
    ghost.userData.noPick = true;
    root.add(ghost);
  }
}
