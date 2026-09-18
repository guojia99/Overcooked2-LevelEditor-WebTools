/**
 * 地板 → 3D 板块（方案 §3 R1–R3）。
 *
 * R1：板的【顶面】严格等于 floorWalkY(f)，而不是 localPosition.y。
 *     地面层的三个历史 Y（实心 -0.05 / 主题 +0.01 / 空气 0）行走面都是 0，
 *     直接用视觉 Y 会让它们在 3D 里差 6 厘米互相闪；用行走面则天然归一。
 *     板厚向下拉伸，等价于把 Unity 烘焙的 Col_Floor（size.y=0.4, center.y=-0.2）
 *     体积画出来——不是美术近似，是权威几何。
 * R3：同高共面重叠靠 polygonOffset 解决，几何坐标保持精确；序号来自与 2D 完全
 *     相同的排序（按 floorWalkY 升序），所以「2D 里谁盖住谁，3D 里就是谁」。
 */

import * as THREE from "three";
import { CELL, EditorFloor, S } from "../state";
import { floorWalkY } from "../floorHeight";
import { surfacePaint } from "../../floorColors";
import { getFloorImage } from "../iconCaches";
import { effectiveMaterialTiling } from "../floors";
import { ORDER, POLYGON_OFFSET_STEP, floorSlabThickness } from "./constants";
import { parseCssColor, iconTexture } from "./materials";
import { tagPickable } from "./ctx";
import { toSceneZ, toSceneRotY } from "./space";

export interface FloorSlab {
  cx: number;
  cz: number;
  /** 顶面世界 Y（= 行走面）。 */
  topY: number;
  w: number;
  d: number;
  thickness: number;
  rotDeg: number;
}

/** 地板板块几何。所有 3D 拾取/角柄/框选都以它为唯一口径。 */
export function floorSlabOf(f: EditorFloor, gapBelow?: number): FloorSlab {
  return {
    cx: f._wx,
    cz: f._wz,
    topY: floorWalkY(f),
    w: Math.max(0.01, f._wCells * CELL),
    d: Math.max(0.01, f._dCells * CELL),
    thickness: floorSlabThickness(gapBelow),
    rotDeg: f.localRotationY ?? 0,
  };
}

/**
 * 2D 同款排序：按行走面升序。返回 key → 序号，供 polygonOffset 与 renderOrder 使用。
 * 与 renderFloors.ts:325 的 `[...S.floors].sort((a,b)=>floorWalkY(a)-floorWalkY(b))` 同构。
 */
export function floorDrawOrder(): Map<string, number> {
  const sorted = [...S.floors].sort((a, b) => floorWalkY(a) - floorWalkY(b));
  const out = new Map<string, number>();
  sorted.forEach((f, i) => out.set(f._key, i));
  return out;
}

/** auto 档需要的「下方最近一层间距」：同一 XZ 区域内下一层地板的高度差。 */
export function gapBelowFor(f: EditorFloor): number | undefined {
  const y = floorWalkY(f);
  let best: number | undefined;
  for (const o of S.floors) {
    if (o._key === f._key) continue;
    const oy = floorWalkY(o);
    if (oy >= y - 1e-6) continue;
    const gap = y - oy;
    if (best == null || gap < best) best = gap;
  }
  return best;
}

export function floorSignature(f: EditorFloor, order: number, selected: boolean, dimmed: boolean): string {
  const s = floorSlabOf(f, gapBelowFor(f));
  return [
    f.surfaceKind,
    f.airFloor ? "air" : "solid",
    s.w.toFixed(3),
    s.d.toFixed(3),
    s.topY.toFixed(3),
    s.thickness.toFixed(3),
    s.rotDeg.toFixed(1),
    f.tintEnabled ? f.tintColor ?? "" : "",
    f.imageTexturePath ?? "",
    f.imageMode ?? "",
    String(f.imageOpacity ?? ""),
    String(f.imageRotation ?? ""),
    f.materialName ?? "",
    String(order),
    selected ? "1" : "0",
    dimmed ? "1" : "0",
  ].join("|");
}

export interface BuildFloorOpts {
  order: number;
  selected: boolean;
  dimmed: boolean;
}

export function buildFloorNode(f: EditorFloor, opts: BuildFloorOpts): THREE.Group {
  const slab = floorSlabOf(f, gapBelowFor(f));
  const group = new THREE.Group();
  group.name = "floor:" + f._key;

  const paint = surfacePaint(f.surfaceKind, opts.selected);
  const tint = f.tintEnabled && f.tintColor ? parseCssColor(f.tintColor) : null;
  const base = tint ? tint : parseCssColor(paint.fill);
  const stroke = parseCssColor(paint.stroke);

  if (f.airFloor) {
    group.add(buildAirFloor(slab, opts));
  } else {
    const geo = new THREE.BoxGeometry(slab.w, slab.thickness, slab.d);
    const opacity = opts.dimmed ? 0.22 : Math.max(0.55, base.alpha);
    const mat = new THREE.MeshLambertMaterial({
      color: base.color,
      transparent: opacity < 1,
      opacity,
      side: THREE.FrontSide, // R4
      depthWrite: opacity > 0.9,
      polygonOffset: true, // R3
      polygonOffsetFactor: -1,
      polygonOffsetUnits: -opts.order * POLYGON_OFFSET_STEP,
    });
    const img = f.imageTexturePath ? getFloorImage(f.imageTexturePath) : null;
    const tex = img ? iconTexture(img) : null;
    if (tex) {
      const t = tex.clone();
      t.needsUpdate = true;
      // clone 会连 userData 一起拷（含 shared 标记），这里要清掉，
      // 否则每块地板的独立副本永远不会被释放。
      t.userData.shared = false;
      t.wrapS = THREE.RepeatWrapping;
      t.wrapT = THREE.RepeatWrapping;
      if (f.imageMode === "tile") {
        t.repeat.set(Math.max(1, f._wCells), Math.max(1, f._dCells));
      }
      mat.map = t;
      mat.opacity = Math.max(0.05, f.imageOpacity ?? 1) * (opts.dimmed ? 0.3 : 1);
      mat.transparent = mat.opacity < 1;
      mat.color = new THREE.Color(0xffffff);
    } else {
      applyMaterialTilingHint(group, f, slab, stroke, opts);
    }
    const mesh = new THREE.Mesh(geo, mat);
    // R1：顶面贴行走面 ⇒ 盒心在 topY - 厚/2。
    mesh.position.y = slab.topY - slab.thickness / 2;
    mesh.renderOrder = ORDER.floor + opts.order * 0.001;
    group.add(mesh);

    const edges = new THREE.LineSegments(
      new THREE.EdgesGeometry(geo),
      new THREE.LineBasicMaterial({
        color: stroke.color,
        transparent: true,
        opacity: opts.dimmed ? 0.2 : opts.selected ? 0.95 : 0.6,
      })
    );
    edges.position.y = mesh.position.y;
    edges.renderOrder = ORDER.floor + 0.5;
    group.add(edges);
  }

  group.position.set(slab.cx, 0, toSceneZ(slab.cz));
  group.rotation.y = toSceneRotY(slab.rotDeg);

  tagPickable(group, { kind: "floor", key: f._key });
  if (opts.dimmed) {
    group.traverse((o) => {
      o.userData.noPick = true;
    });
  }
  return group;
}

/** 空气地板：游戏里没有可见网格，只有 Ground 层碰撞盒。沿用 2D 的琥珀虚线语义。 */
function buildAirFloor(slab: FloorSlab, opts: BuildFloorOpts): THREE.Group {
  const g = new THREE.Group();
  const geo = new THREE.BoxGeometry(slab.w, slab.thickness, slab.d);
  const mat = new THREE.MeshBasicMaterial({
    color: 0xf2b134,
    transparent: true,
    opacity: opts.dimmed ? 0.06 : opts.selected ? 0.28 : 0.14,
    side: THREE.FrontSide,
    depthWrite: false,
  });
  const mesh = new THREE.Mesh(geo, mat);
  mesh.position.y = slab.topY - slab.thickness / 2;
  mesh.renderOrder = ORDER.floor + opts.order * 0.001;
  g.add(mesh);

  const edges = new THREE.LineSegments(
    new THREE.EdgesGeometry(geo),
    new THREE.LineDashedMaterial({
      color: 0xf2b134,
      dashSize: 0.18,
      gapSize: 0.12,
      transparent: true,
      opacity: opts.dimmed ? 0.2 : 0.9,
    })
  );
  edges.computeLineDistances();
  edges.position.y = mesh.position.y;
  edges.renderOrder = ORDER.floor + 0.5;
  g.add(edges);
  return g;
}

/** 材质平铺提示线：与 2D 顶面的细网格同义，画在板顶。 */
function applyMaterialTilingHint(
  group: THREE.Group,
  f: EditorFloor,
  slab: FloorSlab,
  stroke: { color: THREE.Color },
  opts: BuildFloorOpts
): void {
  if (opts.dimmed) return;
  const tiling = effectiveMaterialTiling(f);
  const tw = Math.max(1, Math.round(tiling.w));
  const td = Math.max(1, Math.round(tiling.d));
  if (tw <= 1 && td <= 1) return;

  const pts: number[] = [];
  const y = slab.topY + 0.003;
  for (let i = 1; i < tw; i++) {
    const x = -slab.w / 2 + (slab.w * i) / tw;
    pts.push(x, y, -slab.d / 2, x, y, slab.d / 2);
  }
  for (let j = 1; j < td; j++) {
    const z = -slab.d / 2 + (slab.d * j) / td;
    pts.push(-slab.w / 2, y, z, slab.w / 2, y, z);
  }
  if (!pts.length) return;
  const geo = new THREE.BufferGeometry();
  geo.setAttribute("position", new THREE.Float32BufferAttribute(pts, 3));
  const lines = new THREE.LineSegments(
    geo,
    new THREE.LineBasicMaterial({ color: stroke.color, transparent: true, opacity: 0.25, depthWrite: false })
  );
  lines.renderOrder = ORDER.floorSeam;
  lines.userData.noPick = true;
  group.add(lines);
}
