/**
 * 叠加层（方案 §3 R6 / R7）：网格、走道、坠落区、地板接缝、相机视锥、
 * 联动连线（传送门 / 开关 / 终端 / 上菜台）、传送带方向箭头。
 *
 * 全部是半透明贴花：depthWrite:false + renderOrder 阶梯 + polygonOffset，
 * 否则它们与地板顶面共面会互相闪烁。整组每次同步全量重建——数量级只有几十，
 * 不值得做增量。
 */

import * as THREE from "three";
import { S, CELL, isFloorLikeLayer } from "../state";
import { floorWalkY } from "../floorHeight";
import { bgTheme } from "../../floorColors";
import { stubKindOf } from "../stubControls";
import { ORDER } from "./constants";
import { Scene3DCtx, disposeObject } from "./ctx";
import { decalMaterial, parseCssColor } from "./materials";
import { itemBoxOf } from "./meshItems";
import { toSceneZ, toSceneDir, forwardOf } from "./space";

const PORTAL_COLORS = ["#c792ea", "#82aaff", "#7bd88f", "#f78c6c", "#ffcb6b", "#ff6e9c"];

/** 静态层（网格 / 空洞底面）单独持有并按签名缓存：它每帧重建的代价远高于连线，
 *  而在拖动物件时它根本不会变。 */
let staticRoot: THREE.Group | null = null;
let staticSignature = "";

export function rebuildOverlays(ctx: Scene3DCtx): void {
  const root = ctx.overlayRoot;

  // 动态层：每次同步重建（连线要跟着物件走，数量级只有几十）。
  for (let i = root.children.length - 1; i >= 0; i--) {
    const child = root.children[i];
    if (child === staticRoot) continue;
    disposeObject(child);
  }

  syncStaticOverlays(ctx);

  if (isFloorLikeLayer(S.currentLayer)) {
    addWalkable(root);
    addKillPlanes(root);
  }
  addLinks(root);
  addDirectionArrows(root);
  if (S.showCameraFov) addCameraFrustum(root);

  root.traverse((o) => {
    o.userData.noPick = true;
  });
}

/** 网格 + 空洞底面：只有「最低行走面 / 网格开关 / 主题」变化时才重建。 */
function syncStaticOverlays(ctx: Scene3DCtx): void {
  let lowest = 0;
  for (const f of S.floors) {
    const y = floorWalkY(f);
    if (y < lowest) lowest = y;
  }
  const sig = [S.showGrid ? "1" : "0", lowest.toFixed(2), S.bgThemeKey, S.cameraInfo?.backgroundColor ?? ""].join("|");
  if (staticRoot && staticRoot.parent === ctx.overlayRoot && sig === staticSignature) return;

  if (staticRoot) disposeObject(staticRoot);
  staticRoot = new THREE.Group();
  staticRoot.name = "overlay-static";
  staticSignature = sig;
  addGrid(staticRoot, lowest);
  applyBackdrop(ctx);
  ctx.overlayRoot.add(staticRoot);
}

/** 底色与雾色跟随主题/相机背景色（与 2D draw() 的 camVoidBg 分支同口径）。 */
function applyBackdrop(ctx: Scene3DCtx): void {
  const theme = bgTheme(S.bgThemeKey);
  const camBg = S.bgThemeKey === "void" && isHexColor(S.cameraInfo?.backgroundColor)
    ? S.cameraInfo!.backgroundColor!
    : null;
  const col = parseCssColor(camBg ?? theme.fill ?? "#14171c").color;
  ctx.renderer.setClearColor(col, 1);
  if (ctx.scene.fog) (ctx.scene.fog as THREE.Fog).color.copy(col);
}

function isHexColor(v: string | undefined | null): boolean {
  return !!v && /^#[0-9a-f]{6}$/i.test(v);
}

/** 卸载时丢弃静态层缓存。 */
export function resetOverlayCache(): void {
  staticRoot = null;
  staticSignature = "";
}

/** R7：网格画在全部地板之下，避免与地面层共面。 */
function addGrid(root: THREE.Group, lowest: number): void {
  if (!S.showGrid) return;
  const y = lowest - 0.45;

  const span = 160;
  const grid = new THREE.GridHelper(span, Math.round(span / CELL), 0x3a4048, 0x2a2f36);
  grid.position.y = y;
  // Unity 物理格点：网格线相对单元中心偏半格（与 2D render.ts:101-103 一致）。
  grid.position.x = CELL / 2;
  grid.position.z = CELL / 2;
  const gm = grid.material as THREE.Material | THREE.Material[];
  const list = Array.isArray(gm) ? gm : [gm];
  for (const m of list) {
    m.transparent = true;
    m.opacity = 0.5;
    m.depthWrite = false;
  }
  grid.renderOrder = ORDER.grid;
  root.add(grid);

  // 空洞底面：一张大面片，替代 2D 的斜线填充，指示「没有地板 = 会掉下去」。
  // theme.hatch 是 rgba(...) 字符串，THREE.Color 解析不了，必须走 parseCssColor。
  const theme = bgTheme(S.bgThemeKey);
  const hatch = parseCssColor(theme.hatch || "rgba(130,132,142,0.10)");
  const plane = new THREE.Mesh(
    new THREE.PlaneGeometry(span, span),
    new THREE.MeshBasicMaterial({
      color: hatch.color,
      transparent: true,
      opacity: Math.max(0.08, Math.min(0.5, hatch.alpha * 3)),
      side: THREE.DoubleSide,
      depthWrite: false,
    })
  );
  plane.rotation.x = -Math.PI / 2;
  plane.position.y = y - 0.02;
  plane.renderOrder = ORDER.grid;
  root.add(plane);
}

/** 可行走碰撞矩形（只读数据，来自 Unity 导出）。 */
function addWalkable(root: THREE.Group): void {
  for (const w of S.walkable) {
    if ((w.sx ?? 0) <= 0.001 || (w.sz ?? 0) <= 0.001) continue;
    const mesh = new THREE.Mesh(
      new THREE.PlaneGeometry(w.sx, w.sz),
      decalMaterial(w.surfaceType === "ice" ? 0x78c8eb : 0x7bd88f, 0.12, ORDER.walkable)
    );
    mesh.rotation.x = -Math.PI / 2;
    mesh.position.set(w.cx, 0.012, toSceneZ(w.cz));
    mesh.renderOrder = ORDER.walkable;
    root.add(mesh);

    root.add(outlineRect(w.cx, 0.013, toSceneZ(w.cz), w.sx, w.sz, 0x7bd88f, 0.5, ORDER.walkable));
  }
}

/** 坠落区（KillPlane）。 */
function addKillPlanes(root: THREE.Group): void {
  const planes = S.deathInfo?.killPlanes ?? [];
  for (const kp of planes) {
    if ((kp.sx ?? 0) <= 0.001 || (kp.sz ?? 0) <= 0.001) continue;
    const mesh = new THREE.Mesh(
      new THREE.PlaneGeometry(kp.sx, kp.sz),
      decalMaterial(0xf28b82, 0.1, ORDER.killPlane)
    );
    mesh.rotation.x = -Math.PI / 2;
    mesh.position.set(kp.cx, -0.05, toSceneZ(kp.cz));
    mesh.renderOrder = ORDER.killPlane;
    root.add(mesh);
    root.add(outlineRect(kp.cx, -0.049, toSceneZ(kp.cz), kp.sx, kp.sz, 0xf28b82, 0.85, ORDER.killPlane));
  }
}

function outlineRect(
  cx: number,
  y: number,
  cz: number,
  sx: number,
  sz: number,
  color: number,
  opacity: number,
  order: number
): THREE.LineLoop {
  const hw = sx / 2;
  const hd = sz / 2;
  const geo = new THREE.BufferGeometry();
  geo.setAttribute(
    "position",
    new THREE.Float32BufferAttribute(
      [cx - hw, y, cz - hd, cx + hw, y, cz - hd, cx + hw, y, cz + hd, cx - hw, y, cz + hd],
      3
    )
  );
  const line = new THREE.LineLoop(
    geo,
    new THREE.LineBasicMaterial({ color, transparent: true, opacity, depthWrite: false })
  );
  line.renderOrder = order;
  return line;
}

/** 联动连线：传送门 / 开关 / 终端 / 上菜台↔回盘台。 */
function addLinks(root: THREE.Group): void {
  const byInst = new Map(S.items.map((i) => [i.instanceId, i]));
  const linkTop = (key: string): THREE.Vector3 | null => {
    const it = byInst.get(key);
    if (!it) return null;
    const b = itemBoxOf(it);
    return new THREE.Vector3(b.cx, b.baseY + b.h + 0.22, toSceneZ(b.cz));
  };

  // 传送门
  const drawnPairs = new Set<string>();
  for (const t of S.items) {
    if (stubKindOf(t) !== "Teleportal") continue;
    if (t.teleportal?.exitOnly) continue;
    const exitId = t.teleportal?.exitPortalInstanceId;
    if (!exitId || exitId === t.instanceId) continue;
    const pairKey = [t.instanceId, exitId].sort().join("|");
    if (drawnPairs.has(pairKey)) continue;
    drawnPairs.add(pairKey);
    const a = linkTop(t.instanceId);
    const b = linkTop(exitId);
    if (!a || !b) continue;
    const color = PORTAL_COLORS[t.teleportal?.portalColor ?? 0] ?? "#c792ea";
    root.add(linkLine(a, b, new THREE.Color(color), 0.65));
    root.add(arrowHead(a, b, new THREE.Color(color)));
  }

  // 开关 → 目标
  for (const l of S.switchLinks) {
    const a = linkTop(l.switchId);
    const b = linkTop(l.targetId);
    if (!a || !b) continue;
    root.add(linkLine(a, b, new THREE.Color(0xf9ab00), 0.55));
    root.add(arrowHead(a, b, new THREE.Color(0xf9ab00)));
  }

  // 控制终端 → 可驾驶目标
  for (const tm of S.items) {
    if (stubKindOf(tm) !== "Terminal") continue;
    const targetId = tm.terminal?.pilotableObjectInstanceId;
    if (!targetId) continue;
    const a = linkTop(tm.instanceId);
    const b = linkTop(targetId);
    if (!a || !b) continue;
    root.add(linkLine(a, b, new THREE.Color(0xc75be8), 0.6));
    root.add(arrowHead(a, b, new THREE.Color(0xc75be8)));
  }

  // 上菜台 → 回盘台
  for (const it of S.items) {
    const ids = it.servingStation?.plateReturnInstanceIds
      ?? (it.servingStation?.plateReturnInstanceId ? [it.servingStation.plateReturnInstanceId] : []);
    for (const rid of ids) {
      const a = linkTop(it.instanceId);
      const b = linkTop(rid);
      if (!a || !b) continue;
      root.add(linkLine(a, b, new THREE.Color(0x63d3ff), 0.5));
      root.add(arrowHead(a, b, new THREE.Color(0x63d3ff)));
    }
  }
}

function linkLine(a: THREE.Vector3, b: THREE.Vector3, color: THREE.Color, opacity: number): THREE.Line {
  // 抬一个弧顶，避免长连线被高物件穿插得支离破碎。
  const mid = a.clone().add(b).multiplyScalar(0.5);
  mid.y += Math.min(2.2, a.distanceTo(b) * 0.18);
  const curve = new THREE.QuadraticBezierCurve3(a, mid, b);
  const geo = new THREE.BufferGeometry().setFromPoints(curve.getPoints(24));
  const line = new THREE.Line(
    geo,
    new THREE.LineDashedMaterial({
      color,
      transparent: true,
      opacity,
      dashSize: 0.28,
      gapSize: 0.18,
      depthTest: false,
    })
  );
  line.computeLineDistances();
  line.renderOrder = ORDER.link;
  return line;
}

function arrowHead(a: THREE.Vector3, b: THREE.Vector3, color: THREE.Color): THREE.Mesh {
  const dir = b.clone().sub(a).normalize();
  const cone = new THREE.Mesh(
    new THREE.ConeGeometry(0.12, 0.34, 8),
    new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.85, depthTest: false })
  );
  cone.position.copy(b.clone().addScaledVector(dir, -0.3));
  cone.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir);
  cone.renderOrder = ORDER.link;
  return cone;
}

/** 传送带 / 移动板 / 食材生成器的方向箭头（贴在物件顶面）。 */
function addDirectionArrows(root: THREE.Group): void {
  for (const it of S.items) {
    const kind = stubKindOf(it);
    let color: number;
    let flip = 1;
    if (kind === "Conveyor") {
      color = 0xffcb6b;
      flip = (it.conveyor?.conveySpeed ?? 0.5) >= 0 ? 1 : -1;
    } else if (kind === "Travelator") {
      color = 0xffa657;
      flip = (it.travelator?.speed ?? 1) >= 0 ? 1 : -1;
    } else if (kind === "FoodSpawner") {
      color = 0x7bd889;
      flip = -1;
    } else {
      continue;
    }
    const b = itemBoxOf(it);
    const cone = new THREE.Mesh(
      new THREE.ConeGeometry(0.16, 0.42, 10),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.9, depthTest: false })
    );
    // 方向取物件前向（Unity 语义 forward=(sinθ,0,cosθ)，与盒顶的白色朝向标记同侧），
    // 再按 space.ts 换算到 three 场景。
    const f = forwardOf(b.rotDeg);
    const sd = toSceneDir(f.x * flip, f.z * flip);
    const dir = new THREE.Vector3(sd.x, 0, sd.z).normalize();
    cone.position.set(b.cx, b.baseY + b.h + 0.12, toSceneZ(b.cz));
    cone.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir);
    cone.renderOrder = ORDER.link;
    root.add(cone);
  }
}

/** 游戏相机视锥：直接沿用 2D render.ts 的 Unity 基向量数学。 */
function addCameraFrustum(root: THREE.Group): void {
  const ci = S.cameraInfo;
  if (!ci || !ci.position) return;
  const pitch = ((ci.pitch ?? 0) * Math.PI) / 180;
  const yaw = ((ci.yaw ?? 0) * Math.PI) / 180;
  const sinP = Math.sin(yaw);
  const cosP = Math.cos(yaw);
  const sinT = Math.sin(pitch);
  const cosT = Math.cos(pitch);
  const fwd = new THREE.Vector3(sinP * cosT, -sinT, cosP * cosT);
  const right = new THREE.Vector3(cosP, 0, -sinP);
  const up = new THREE.Vector3(sinT * sinP, cosT, sinT * cosP);

  const tanVy = Math.tan((((ci.fieldOfView ?? 60) * Math.PI) / 180) / 2);
  const tanVh = tanVy * (16 / 9);
  const origin = new THREE.Vector3(ci.position.x, ci.position.y, ci.position.z);
  const far = Math.min(60, ci.farClip ?? 60);

  const corners: THREE.Vector3[] = [];
  for (const sx of [-1, 1]) {
    for (const sy of [-1, 1]) {
      corners.push(
        origin
          .clone()
          .addScaledVector(fwd, far)
          .addScaledVector(right, sx * tanVh * far)
          .addScaledVector(up, sy * tanVy * far)
      );
    }
  }
  const order = [corners[0], corners[1], corners[3], corners[2]];
  const pts: THREE.Vector3[] = [];
  // 上面的基向量与角点全在编辑器/Unity 世界系里算，最后统一镜像到 three 场景。
  const toScenePt = (p: THREE.Vector3) => new THREE.Vector3(p.x, p.y, toSceneZ(p.z));
  for (let i = 0; i < 4; i++) {
    pts.push(toScenePt(origin), toScenePt(order[i]));
    pts.push(toScenePt(order[i]), toScenePt(order[(i + 1) % 4]));
  }
  const geo = new THREE.BufferGeometry().setFromPoints(pts);
  const lines = new THREE.LineSegments(
    geo,
    new THREE.LineBasicMaterial({ color: 0x8ab4f8, transparent: true, opacity: 0.45, depthWrite: false })
  );
  lines.renderOrder = ORDER.link;
  root.add(lines);
}
