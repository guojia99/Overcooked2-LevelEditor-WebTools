import { S } from "./state";
import { dom } from "./dom";
import { worldToCanvas } from "./coords";
import type { LightInfo } from "../types";

const PICK_RADIUS_PX = 14;

function lightWorldXZ(l: LightInfo): { x: number; z: number } {
  const p = l.position;
  return { x: p?.x ?? 0, z: p?.z ?? 0 };
}

/** 屏幕坐标命中测试（灯光层交互用）。 */
export function hitTestLights(cx: number, cy: number): LightInfo[] {
  const hits: { l: LightInfo; d: number }[] = [];
  for (const l of S.lights) {
    if (!l.enabled) continue;
    const { x, z } = lightWorldXZ(l);
    const p = worldToCanvas(x, z);
    const dx = p.x - cx;
    const dy = p.y - cy;
    const d = Math.hypot(dx, dy);
    if (d <= PICK_RADIUS_PX) hits.push({ l, d });
  }
  hits.sort((a, b) => a.d - b.d);
  return hits.map((h) => h.l);
}

function forwardXZ(l: LightInfo): { x: number; z: number } {
  const e = l.eulerAngles ?? { x: 0, y: 0, z: 0 };
  const pitch = (e.x * Math.PI) / 180;
  const yaw = (e.y * Math.PI) / 180;
  const cosT = Math.cos(pitch);
  const sinT = Math.sin(pitch);
  const cosP = Math.cos(yaw);
  const sinP = Math.sin(yaw);
  const fx = sinP * cosT;
  const fz = cosP * cosT;
  const len = Math.hypot(fx, fz) || 1;
  return { x: fx / len, z: fz / len };
}

export function drawSceneLights(): void {
  if (!S.showSceneLights && S.currentLayer !== "lights") return;
  if (S.lights.length === 0) return;

  const ctx = dom.ctx;
  for (const l of S.lights) {
    const { x, z } = lightWorldXZ(l);
    const c = worldToCanvas(x, z);
    const sel = S.selectedLightPath === l.hierarchyPath;
    const alpha = l.enabled ? 1 : 0.35;
    const col = l.color && /^#[0-9a-fA-F]{6}$/.test(l.color) ? l.color : "#ffeb3b";

    ctx.save();
    ctx.globalAlpha = alpha;

    if (l.lightType === 1 || l.lightType === 0) {
      const f = forwardXZ(l);
      const len = l.lightType === 1 ? 28 : 22;
      const tip = worldToCanvas(x + f.x * 1.2, z + f.z * 1.2);
      const tail = worldToCanvas(x - f.x * 0.3, z - f.z * 0.3);
      ctx.strokeStyle = sel ? "#fff" : col;
      ctx.lineWidth = sel ? 3 : 2;
      ctx.beginPath();
      ctx.moveTo(tail.x, tail.y);
      ctx.lineTo(tip.x, tip.y);
      ctx.stroke();
      const ang = Math.atan2(tip.y - tail.y, tip.x - tail.x);
      ctx.fillStyle = sel ? "#fff" : col;
      ctx.beginPath();
      ctx.moveTo(tip.x, tip.y);
      ctx.lineTo(tip.x - len * 0.35 * Math.cos(ang - 0.45), tip.y - len * 0.35 * Math.sin(ang - 0.45));
      ctx.lineTo(tip.x - len * 0.35 * Math.cos(ang + 0.45), tip.y - len * 0.35 * Math.sin(ang + 0.45));
      ctx.closePath();
      ctx.fill();
    }

    if (l.lightType === 0) {
      const f = forwardXZ(l);
      const half = ((l.spotAngle || 30) * Math.PI) / 180 / 2;
      const baseAng = Math.atan2(f.z, f.x);
      const r = 1.5;
      const p0 = worldToCanvas(x, z);
      const p1 = worldToCanvas(x + Math.cos(baseAng - half) * r, z + Math.sin(baseAng - half) * r);
      const p2 = worldToCanvas(x + Math.cos(baseAng + half) * r, z + Math.sin(baseAng + half) * r);
      ctx.fillStyle = sel ? "rgba(255,255,255,0.12)" : `${col}33`;
      ctx.beginPath();
      ctx.moveTo(p0.x, p0.y);
      ctx.lineTo(p1.x, p1.y);
      ctx.lineTo(p2.x, p2.y);
      ctx.closePath();
      ctx.fill();
    }

    ctx.beginPath();
    ctx.arc(c.x, c.y, sel ? 9 : 7, 0, Math.PI * 2);
    ctx.fillStyle = sel ? "#fff" : col;
    ctx.fill();
    ctx.strokeStyle = sel ? col : "rgba(0,0,0,0.5)";
    ctx.lineWidth = sel ? 2 : 1;
    ctx.stroke();

    if (S.currentLayer === "lights" && l.position) {
      ctx.fillStyle = "rgba(255,255,255,0.85)";
      ctx.font = "10px system-ui,sans-serif";
      ctx.textAlign = "center";
      ctx.fillText(`y=${Math.round(l.position.y * 10) / 10}`, c.x, c.y - 12);
    }

    ctx.restore();
  }
}
