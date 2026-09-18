/**
 * 3D 视口共享上下文 + 场景图对象登记表。
 *
 * 纪律：historyOps.applySnapshot() 会整体替换 S.items / S.floors 的对象身份
 * （JSON 深拷贝还原），所以这里一律按 _editorKey / _key 索引，绝不持有 EditorItem
 * 对象引用，否则撤销一次就全是野指针。
 */

import * as THREE from "three";
import { S, EditorItem, EditorFloor } from "../state";
import { OrbitCam } from "./camera3d";

/** 场景图里一个可拾取对象的身份。挂在 Object3D.userData 上供 raycast 反查。 */
export interface Pickable {
  kind: "item" | "floor" | "waypoint" | "handle";
  /** item = _editorKey；floor = _key；waypoint = waypoint id。 */
  key: string;
  /** handle 专用：角柄方位与归属。 */
  handle?: {
    owner: "floor" | "item";
    ownerKey: string;
    /** 角柄：四角之一；轴柄：y。 */
    edge: "nw" | "ne" | "sw" | "se" | "y" | "top";
  };
}

/** 一个物件在场景里的全部可视对象（盒体 + 线框 + 贴图/文字 sprite）。 */
export interface ItemNode {
  group: THREE.Group;
  /** 几何特征串，变了才重建 mesh，否则只更新 transform。 */
  signature: string;
  /** 贴花特征串（图标三态 + 标签）。图标异步到达时只换贴花，不动几何。 */
  decalSignature: string;
}

export interface FloorNode {
  group: THREE.Group;
  signature: string;
}

export interface Scene3DCtx {
  scene: THREE.Scene;
  cam: OrbitCam;
  renderer: THREE.WebGLRenderer;
  canvas: HTMLCanvasElement;
  /** 物件节点表，key = _editorKey。 */
  items: Map<string, ItemNode>;
  /** 地板节点表，key = _key。 */
  floors: Map<string, FloorNode>;
  /** 叠加层根节点（每帧整体重建，量小）。 */
  overlayRoot: THREE.Group;
  /** 动画路点/路线根节点。 */
  animRoot: THREE.Group;
  /** 角柄根节点（选中态才有内容）。 */
  handleRoot: THREE.Group;
  /** 标脏，下一帧同步。 */
  invalidate(): void;
}

/** 按 key 取回当前 state 里的物件（撤销后数组身份会换，必须每次重查）。 */
export function itemByKey(key: string): EditorItem | undefined {
  for (const it of S.items) {
    if (it._editorKey === key) return it;
  }
  return undefined;
}

export function floorByKey(key: string): EditorFloor | undefined {
  for (const f of S.floors) {
    if (f._key === key) return f;
  }
  return undefined;
}

/** 递归销毁并释放几何/材质，避免切换视图泄漏显存。
 *  注意：文字/图标纹理是【全局共享】的（同名物件共用一张），绝不能在这里
 *  dispose，否则销毁一个物件会把其它所有同名物件的贴图一起弄坏。 */
export function disposeObject(obj: THREE.Object3D): void {
  obj.traverse((o) => {
    const mesh = o as THREE.Mesh;
    if (mesh.geometry) mesh.geometry.dispose();
    const mat = (o as THREE.Mesh).material as THREE.Material | THREE.Material[] | undefined;
    if (!mat) return;
    const list = Array.isArray(mat) ? mat : [mat];
    for (const m of list) {
      const asBasic = m as THREE.MeshBasicMaterial;
      if (asBasic.map && !asBasic.map.userData.shared) asBasic.map.dispose();
      m.dispose();
    }
  });
  if (obj.parent) obj.parent.remove(obj);
}

export function clearGroup(group: THREE.Group): void {
  for (let i = group.children.length - 1; i >= 0; i--) {
    disposeObject(group.children[i]);
  }
  group.clear();
}

/** 给整棵子树打上可拾取身份。 */
export function tagPickable(obj: THREE.Object3D, p: Pickable): void {
  obj.traverse((o) => {
    o.userData.pick = p;
  });
}
