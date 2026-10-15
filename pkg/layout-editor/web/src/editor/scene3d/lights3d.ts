/**
 * 将 S.lights 同步为 three.js 灯光（预览用，不写回）。
 */

import * as THREE from "three";
import { S } from "../state";
import { Scene3DCtx, disposeObject } from "./ctx";
import { toSceneZ } from "./space";

const GROUP_NAME = "scene-lights-preview";

function parseColor(hex: string): number {
  if (hex && /^#[0-9a-fA-F]{6}$/.test(hex)) return parseInt(hex.slice(1), 16);
  return 0xffffff;
}

function eulerToDir(e: { x: number; y: number; z: number }): THREE.Vector3 {
  const pitch = (e.x * Math.PI) / 180;
  const yaw = (e.y * Math.PI) / 180;
  const cosT = Math.cos(pitch);
  const sinT = Math.sin(pitch);
  const cosP = Math.cos(yaw);
  const sinP = Math.sin(yaw);
  return new THREE.Vector3(sinP * cosT, -sinT, -cosP * cosT).normalize();
}

export function rebuildSceneLights(ctx: Scene3DCtx): void {
  const existing = ctx.scene.getObjectByName(GROUP_NAME);
  if (existing) disposeObject(existing);

  const active = S.showSceneLights || S.currentLayer === "lights";
  if (!active || S.lights.length === 0) return;

  const root = new THREE.Group();
  root.name = GROUP_NAME;

  for (const l of S.lights) {
    if (!l.enabled) continue;
    const p = l.position ?? { x: 0, y: 0, z: 0 };
    const col = parseColor(l.color);
    const inten = Math.max(0, l.intensity) * 0.85;

    if (l.lightType === 1) {
      const dl = new THREE.DirectionalLight(col, inten);
      const dir = eulerToDir(l.eulerAngles ?? { x: 50, y: 0, z: 0 });
      dl.position.set(p.x, p.y, toSceneZ(p.z));
      dl.target.position.set(p.x + dir.x * 4, p.y + dir.y * 4, toSceneZ(p.z) + dir.z * 4);
      root.add(dl);
      root.add(dl.target);
    } else if (l.lightType === 0) {
      const sl = new THREE.SpotLight(col, inten, l.range || 10, ((l.spotAngle || 30) * Math.PI) / 180);
      sl.position.set(p.x, p.y, toSceneZ(p.z));
      const dir = eulerToDir(l.eulerAngles ?? { x: 45, y: 0, z: 0 });
      sl.target.position.set(p.x + dir.x, p.y + dir.y, toSceneZ(p.z) + dir.z);
      root.add(sl);
      root.add(sl.target);
    } else {
      const pl = new THREE.PointLight(col, inten, l.range || 10);
      pl.position.set(p.x, p.y, toSceneZ(p.z));
      root.add(pl);
    }
  }

  ctx.scene.add(root);
}
