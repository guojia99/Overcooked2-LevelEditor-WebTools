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
import { toSceneZ, applySceneEuler } from "./space";

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

    // 路线：按事件顺序把路点连成折线（与 2D 的虚线路线同义）；
    // 段有贝塞尔控制点时采样成曲线（16 点/段，与烘焙端 SampleRouteKeys 同密度）。
    for (const evt of g.events) {
      const ids = evt.waypointIds ?? [];
      if (ids.length < 2) continue;
      const wps = ids
        .map((id) => g.waypoints.find((w) => w.id === id))
        .filter(Boolean) as NonNullable<typeof g.waypoints[number]>[];
      if (wps.length < 2) continue;
      const pts: THREE.Vector3[] = [];
      const pushPt = (x: number, z: number) => pts.push(new THREE.Vector3(x, WAYPOINT_Y, toSceneZ(z)));
      pushPt(wps[0].x, wps[0].z);
      for (let i = 1; i < wps.length; i++) {
        const a = wps[i - 1];
        const b = wps[i];
        if (a.ctrlHas) {
          const c = { x: a.ctrlX ?? a.x, z: a.ctrlZ ?? a.z };
          const n = 16;
          for (let j = 1; j <= n; j++) {
            const t = j / n;
            const im = 1 - t;
            pushPt(
              im * im * a.x + 2 * im * t * c.x + t * t * b.x,
              im * im * a.z + 2 * im * t * c.z + t * t * b.z
            );
          }
        } else {
          pushPt(b.x, b.z);
        }
      }

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

      // 段中点方向箭头（贝塞尔段取 B(0.5)）
      for (let i = 0; i + 1 < wps.length; i++) {
        const a = wps[i];
        const b = wps[i + 1];
        const a3 = new THREE.Vector3(a.x, WAYPOINT_Y, toSceneZ(a.z));
        const b3 = new THREE.Vector3(b.x, WAYPOINT_Y, toSceneZ(b.z));
        if (a.ctrlHas) {
          const c = { x: a.ctrlX ?? a.x, z: a.ctrlZ ?? a.z };
          const c3 = new THREE.Vector3(c.x, WAYPOINT_Y, toSceneZ(c.z));
          const mid = new THREE.Vector3(
            0.25 * a3.x + 0.5 * c3.x + 0.25 * b3.x,
            WAYPOINT_Y,
            0.25 * a3.z + 0.5 * c3.z + 0.25 * b3.z
          );
          const dir = b3.clone().sub(a3).normalize();
          root.add(arrowAt(mid, dir, 0x8ab4f8, alpha));
        } else {
          root.add(segmentArrow(a3, b3, 0x8ab4f8, alpha));
        }
        // 控制点把手：小八面体 + 到段两端的细线（激活组）。
        if (isActive && a.ctrlHas) {
          const c = { x: a.ctrlX ?? a.x, z: a.ctrlZ ?? a.z };
          const c3 = new THREE.Vector3(c.x, WAYPOINT_Y, toSceneZ(c.z));
          const handle = new THREE.BufferGeometry().setFromPoints([a3, c3, b3]);
          const hline = new THREE.Line(
            handle,
            new THREE.LineBasicMaterial({ color: 0x4fb6e8, transparent: true, opacity: 0.5 * alpha, depthTest: false })
          );
          hline.renderOrder = ORDER.route;
          hline.userData.noPick = true;
          root.add(hline);
          const oct = new THREE.Mesh(
            new THREE.OctahedronGeometry(0.14),
            new THREE.MeshBasicMaterial({ color: 0x4fb6e8, transparent: true, opacity: 0.9 * alpha, depthTest: false })
          );
          oct.position.copy(c3);
          oct.renderOrder = ORDER.waypoint;
          oct.userData.noPick = true;
          root.add(oct);
        }
      }
      // 循环标记
      if ((evt.loop || evt.pingpong) && pts.length >= 2) {
        root.add(loopBadge(pts[pts.length - 1], evt.pingpong ? "⇄" : "↻", alpha));
      }
    }

    // 公转事件：圆心十字 + 每成员轨道圆 + 方向箭头。
    for (const evt of g.events) {
      if (evt.type !== "orbit") continue;
      addOrbitRing(root, g, evt, isActive, alpha);
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
  dir.normalize();
  return arrowAt(a.clone().addScaledVector(dir, b.clone().sub(a).length() / 2), dir, color, alpha);
}

function arrowAt(at: THREE.Vector3, dir: THREE.Vector3, color: number, alpha: number): THREE.Mesh {
  const cone = new THREE.Mesh(
    new THREE.ConeGeometry(0.1, 0.26, 8),
    new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.9 * alpha, depthTest: false })
  );
  cone.position.copy(at);
  cone.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir);
  cone.renderOrder = ORDER.route;
  cone.userData.noPick = true;
  return cone;
}

/** orbit 事件 3D 表现：圆心十字（两根短线 + 徽标）+ 每成员虚线轨道圆 + 切向箭头。 */
function addOrbitRing(
  root: THREE.Group,
  group: typeof S.animControls[number],
  evt: NonNullable<typeof S.animControls[number]["events"][number]>,
  isActive: boolean,
  alpha: number
): void {
  const cx = evt.orbitCenterX ?? 0;
  const cz = evt.orbitCenterZ ?? 0;
  const staticIds = new Set((group.memberStatic ?? []).map((m) => m.instanceId));
  const memberIds = [...group.itemInstanceIds, ...group.floorInstanceIds, ...group.objectInstanceIds];
  const col = 0x4fb6e8;
  const ringPts: THREE.Vector3[] = [];
  for (let i = 0; i <= 64; i++) ringPts.push(new THREE.Vector3());
  for (const id of memberIds) {
    if (staticIds.has(id)) continue;
    let sp: { x: number; z: number } | null = null;
    const it = S.items.find((x2) => x2.instanceId === id);
    if (it) sp = { x: it._wx, z: it._wz };
    else {
      const f = S.floors.find((x2) => x2.instanceId === id);
      if (f) sp = { x: f._wx, z: f._wz };
    }
    if (!sp) continue;
    const r = Math.hypot(sp.x - cx, sp.z - cz);
    if (r < 0.05) continue;
    for (let i = 0; i <= 64; i++) {
      const a = (i / 64) * Math.PI * 2;
      ringPts[i].set(cx + r * Math.cos(a), WAYPOINT_Y, toSceneZ(cz + r * Math.sin(a)));
    }
    const geo = new THREE.BufferGeometry().setFromPoints(ringPts.slice());
    const line = new THREE.Line(
      geo,
      new THREE.LineDashedMaterial({ color: col, transparent: true, opacity: 0.6 * alpha, dashSize: 0.25, gapSize: 0.2, depthTest: false })
    );
    line.computeLineDistances();
    line.renderOrder = ORDER.route;
    line.userData.noPick = true;
    root.add(line);
    if (isActive) {
      // 成员起始方位的切向箭头（与 orbitPos 的 cw/ccw 同向：ccw = 圆上角度减小方向）。
      const theta0 = Math.atan2(sp.z - cz, sp.x - cx);
      const ccw = evt.orbitDirection === "ccw";
      const ta = theta0 + (ccw ? -Math.PI / 2 : Math.PI / 2);
      const dir = new THREE.Vector3(Math.cos(ta), 0, -Math.sin(ta));
      const at = new THREE.Vector3(sp.x, WAYPOINT_Y, toSceneZ(sp.z));
      root.add(arrowAt(at, dir, col, alpha));
    }
  }
  // 圆心十字。
  const cross = new THREE.BufferGeometry().setFromPoints([
    new THREE.Vector3(cx - 0.4, WAYPOINT_Y, toSceneZ(cz)),
    new THREE.Vector3(cx + 0.4, WAYPOINT_Y, toSceneZ(cz)),
    new THREE.Vector3(cx, WAYPOINT_Y, toSceneZ(cz)), // polyline 连笔
    new THREE.Vector3(cx, WAYPOINT_Y, toSceneZ(cz - 0.4)),
    new THREE.Vector3(cx, WAYPOINT_Y, toSceneZ(cz + 0.4)),
  ]);
  const cline = new THREE.Line(
    cross,
    new THREE.LineBasicMaterial({ color: col, transparent: true, opacity: 0.9 * alpha, depthTest: false })
  );
  cline.renderOrder = ORDER.route;
  cline.userData.noPick = true;
  root.add(cline);
  if (isActive) {
    root.add(loopBadge(new THREE.Vector3(cx, WAYPOINT_Y, toSceneZ(cz)), "🪐", alpha));
  }
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

/** 预览播放：成员在模拟位姿上画半透明幽灵盒（真实物件仍在原位）；
 *  scale = 缩放倍率、tiltX/tiltZ = 翻转角（applySceneEuler 与 Unity 欧拉同语义）、
 *  alpha = 透明度（fade 淡入淡出）。 */
function addPreviewGhosts(root: THREE.Group): void {
  const poses = previewMemberPositions();
  if (!poses) return;
  for (const it of S.items) {
    const pose = poses.get(it.instanceId);
    if (!pose) continue;
    const b = itemBoxOf(it);
    const psc = Math.max(0.001, pose.scale ?? 1);
    const pac = Math.max(0, Math.min(1, pose.alpha ?? 1));
    if (pac <= 0.01) continue; // 隐身：不画 ghost
    const ghost = new THREE.Mesh(
      new THREE.BoxGeometry(b.w * psc, b.h * psc, b.d * psc),
      new THREE.MeshLambertMaterial({ color: 0xf9ab00, transparent: true, opacity: 0.42 * pac })
    );
    ghost.position.set(pose.x, (pose.y ?? b.baseY) + (b.h * psc) / 2, toSceneZ(pose.z));
    const ry = pose.rotY != null ? pose.rotY : b.rotDeg;
    applySceneEuler(ghost, pose.tiltX ?? 0, ry, pose.tiltZ ?? 0);
    ghost.renderOrder = ORDER.item;
    ghost.userData.noPick = true;
    root.add(ghost);
  }
}
