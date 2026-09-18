/**
 * 物件 → 3D 盒体。
 *
 * 几何来源：XZ 用 itemWorldAABB（已含 pivot 三族修正与旋转换轴），
 * 高度用 catalog.height（全量实测），底面用 catalog.baseY（盒底相对 pivot 的偏移，
 * 中心 pivot 的锅具为负）。配色直接复用 2D 的 paintStyleForItem，保证两视图同色。
 *
 * R4：材质一律 FrontSide —— 物件盒底与地板顶天然共面，背面剔除掉就不存在深度争夺。
 */

import * as THREE from "three";
import { S, CELL, EditorItem } from "../state";
import { catalogItemForGuidOrPath, isBackgroundPlaneCat, itemPlaneCells, ingredientIdByGuid } from "../catalog";
import { resolveFootprint, itemScaleX, itemScaleZ, itemVisualCenterXZ } from "../coords";
import { itemDisplayRotationY } from "../renderItems";
import { isCollisionItem, isAirWallItem } from "../stubControls";
import { airWallCells, airWallHeightCells } from "../items";
import { isSurfaceItem } from "../../floorColors";
import { paintStyleForItem } from "../../itemColors";
import { isSelected } from "../selection";
import { itemLabel } from "../labels";
import { prefabIdFromPath } from "../coords";
import {
  getIngredientIcon,
  getRecipeIcon,
  getCatalogIcon,
  getQuestionMarkIcon,
} from "../iconCaches";
import {
  FALLBACK_ITEM_HEIGHT,
  FALLBACK_DECOR_HEIGHT,
  MIN_ITEM_HEIGHT,
  MAX_ITEM_HEIGHT,
  MAX_PIVOT_OFFSET,
  MIN_THIN_HEIGHT,
  ORDER,
} from "./constants";
import { tagPickable, disposeObject } from "./ctx";
import { labelTexture, iconTexture, parseCssColor } from "./materials";
import { toSceneZ, toSceneRotY } from "./space";

export interface ItemBox {
  /** 盒体中心 XZ（视觉中心，已修正 pivot 三族）。 */
  cx: number;
  cz: number;
  /** 盒底世界 Y。 */
  baseY: number;
  /** 尺寸（世界单位）。 */
  w: number;
  d: number;
  h: number;
  /** 显示朝向（度，Unity 左手系）。 */
  rotDeg: number;
  kind: "normal" | "airwall" | "collision" | "surface" | "plane";
}

/**
 * 单次同步内的盒体缓存。
 *
 * 一次 syncScene 里同一个物件的盒体会被反复求值：签名、堆叠配对、建节点、
 * 叠加层连线与方向箭头 …… 每次都要跑目录查表 + resolveFootprint + pivot 修正。
 * 上千件物件时这是同步开销的大头（图标批量加载会连续触发多帧同步，表现为卡顿）。
 * 缓存只在同步期间开启，同步之外（拖拽中途读取）一律实时计算，避免读到陈旧值。
 */
let boxCacheOn = false;
let boxCache = new WeakMap<EditorItem, ItemBox>();

export function beginBoxCache(): void {
  boxCacheOn = true;
  boxCache = new WeakMap<EditorItem, ItemBox>();
}

export function endBoxCache(): void {
  boxCacheOn = false;
}

/** 物件的 3D 盒体参数。所有 3D 几何/拾取/框选都以它为唯一口径。 */
export function itemBoxOf(item: EditorItem): ItemBox {
  if (boxCacheOn) {
    const hit = boxCache.get(item);
    if (hit) return hit;
  }
  const box = computeItemBox(item);
  if (boxCacheOn) boxCache.set(item, box);
  return box;
}

function computeItemBox(item: EditorItem): ItemBox {
  const cat = catalogItemForGuidOrPath(item.prefabGuid, item.prefabAssetPath);
  const center = itemVisualCenterXZ(item);
  const rotDeg = itemDisplayRotationY(item);
  const itemY = item.localPosition?.y ?? 0;

  if (isAirWallItem(item)) {
    const c = airWallCells(item);
    return {
      cx: item._wx,
      cz: item._wz,
      // 空气墙的碰撞盒以 colliderCenter 为准，默认从地面往上长。
      baseY: item.localPosition?.y ?? 0,
      w: c.wCells * CELL,
      d: c.dCells * CELL,
      h: Math.max(MIN_ITEM_HEIGHT, airWallHeightCells(item) * CELL),
      rotDeg: 0,
      kind: "airwall",
    };
  }
  if (isCollisionItem(item)) {
    const fp = resolveFootprint(item);
    return {
      cx: item._wx,
      cz: item._wz,
      baseY: item.localPosition?.y ?? 0,
      w: fp.cellsX * CELL * itemScaleX(item),
      d: fp.cellsZ * CELL * itemScaleZ(item),
      h: Math.max(MIN_ITEM_HEIGHT, CELL),
      rotDeg,
      kind: "collision",
    };
  }

  // 可改尺寸的背景面（含 rotX=90 的水面 quad，深度轴在 localScale.y）。
  if (isBackgroundPlaneCat(cat)) {
    const planeH = Math.max(MIN_THIN_HEIGHT, cat?.height ?? MIN_THIN_HEIGHT);
    const c = itemPlaneCells(item);
    return {
      cx: center.x,
      cz: center.z,
      baseY: itemY + pivotOffset(cat?.baseY, false),
      w: c.wCells * CELL,
      d: c.dCells * CELL,
      h: planeH,
      rotDeg,
      kind: "plane",
    };
  }

  const fp = resolveFootprint(item);
  const w = fp.cellsX * CELL * itemScaleX(item);
  const d = fp.cellsZ * CELL * itemScaleZ(item);
  const surface = isSurfaceItem(cat);
  // 兜底高度按层级区分：装饰件测不到网格多半是小件/扁平件，核心玩法件才给 1 米。
  const decorish =
    cat?.layoutTier === "decor" || cat?.category === "art" || (cat?.category ?? "").startsWith("decor/");
  const fallback = surface
    ? MIN_THIN_HEIGHT
    : decorish
      ? FALLBACK_DECOR_HEIGHT
      : FALLBACK_ITEM_HEIGHT;
  const rawH = cat?.height ?? cat?.footprint?.sizeY ?? fallback;
  const h = surface
    ? Math.max(MIN_THIN_HEIGHT, Math.min(rawH, 0.2))
    : Math.min(MAX_ITEM_HEIGHT, Math.max(MIN_ITEM_HEIGHT, rawH));
  // 高度被钳制 = 天空盒/动态舞台这类巨物，它的 baseY（如 -309）不是 pivot 修正，
  // 照搬会把盒子沉到地底几百米。这种情况直接锚在物件自身 Y 上往上长。
  const clamped = !surface && rawH > MAX_ITEM_HEIGHT;
  return {
    cx: center.x,
    cz: center.z,
    baseY: itemY + pivotOffset(cat?.baseY, clamped),
    w,
    d,
    h,
    rotDeg,
    kind: surface ? "surface" : "normal",
  };
}

/** pivot 偏移安全钳制：只拦「巨物 / 绝对值离谱」两类背景板伪影，
 *  正常道具（哪怕偏移远大于自身高度，如墙顶盖板、悬挂绳索）一律照用。 */
function pivotOffset(baseY: number | undefined, clamped: boolean): number {
  if (baseY == null || !Number.isFinite(baseY)) return 0;
  if (clamped) return 0;
  if (Math.abs(baseY) > MAX_PIVOT_OFFSET) return 0;
  return baseY;
}

/**
 * 图标解析：与 2D 的优先级链一致（随机箱问号 → 食材 → 成品菜 → catalog 图标）。
 *
 * 返回值区分三态，这是图标能否正常显示的关键：
 *   expected=false            → 本来就没有图标，画文字标签
 *   expected=true, img=null   → 有图标但还在下载中
 *   expected=true, img=<img>  → 可以贴了
 *
 * iconCaches 的 getXxxIcon 首次调用只会发起下载并返回 null，加载完成后才通过
 * setRedraw→draw() 回调通知。若签名不编码这个「已就绪」状态，图标加载完时签名
 * 不变，diff 会走「只更新 transform」的快路径，节点永不重建 —— 表现就是进页面
 * 图标不出现、直到点一下（改变选中态）才冒出来。
 */
function itemIcon(item: EditorItem): { expected: boolean; img: HTMLImageElement | null } {
  const cat = catalogItemForGuidOrPath(item.prefabGuid, item.prefabAssetPath);
  const pid = prefabIdFromPath(item.prefabAssetPath);
  if (item.stubKind === "Dispenser" || pid === "Dispenser" || pid === "Backpack") {
    const rnd = item.dispenser?.randomItemGuids?.length ?? 0;
    if (rnd > 0) return { expected: true, img: getQuestionMarkIcon(item.dispenser?.questionMarkGuid ?? "") };
    const guid = item.dispenser?.spawnerItemPrefabGuid;
    if (guid) {
      const ingId = ingredientIdByGuid(guid);
      if (ingId) return { expected: true, img: getIngredientIcon(ingId) };
    }
  }
  if (!cat) return { expected: false, img: null };
  if (cat.ingredientDecor) return { expected: true, img: getIngredientIcon(cat.id) };
  if (cat.recipeDecor) return { expected: true, img: getRecipeIcon(cat.id) };
  if (cat.icon) return { expected: true, img: getCatalogIcon(cat.id) };
  return { expected: false, img: null };
}

/**
 * 几何特征串：只有它变了才整块重建 mesh。
 *
 * 刻意【不含】图标/文字状态 —— 图标是异步到达的，若混在一起，几百个图标陆续
 * 加载就会触发几百次「拆掉盒体+线框+材质再重建」，表现为持续几秒的卡顿。
 * 贴花变化走 itemDecalSignature + applyItemDecal 的轻量路径。
 */
export function itemGeomSignature(item: EditorItem, dimmed: boolean, hasGuest: boolean, isGuest: boolean): string {
  const b = itemBoxOf(item);
  return [
    item.prefabGuid,
    b.kind,
    b.w.toFixed(3),
    b.d.toFixed(3),
    b.h.toFixed(3),
    isSelected(item._editorKey) ? "1" : "0",
    dimmed ? "1" : "0",
    hasGuest ? "1" : "0",
    isGuest ? "1" : "0",
  ].join("|");
}

/** 贴花特征串：图标三态 + 标签文字。变化时只换顶面贴花，不碰几何。 */
export function itemDecalSignature(item: EditorItem, dimmed: boolean): string {
  if (dimmed) return "-";
  return [iconSigOf(item), itemLabel(item), S.paramLabels.get(item.instanceId) ?? ""].join("|");
}

/** 图标三态的签名片段：txt=画文字 / wait=等图 / img…=已就绪。 */
function iconSigOf(item: EditorItem): string {
  const { expected, img } = itemIcon(item);
  if (!expected) return "txt";
  if (!img) return "wait";
  // 带上尺寸与来源：换了图（如随机箱切问号样式）也能触发更新。
  return "img" + img.naturalWidth + "x" + img.naturalHeight + ":" + img.src;
}

/**
 * 重建顶面贴花（图标或文字），复用已有的盒体节点。
 * 「有图标但还没下载完」时两者都不画 —— 否则会先闪一下文字再跳成图标。
 */
export function applyItemDecal(group: THREE.Group, item: EditorItem, dimmed: boolean): void {
  for (let i = group.children.length - 1; i >= 0; i--) {
    const c = group.children[i];
    if (c.userData.decal) disposeObject(c);
  }
  if (dimmed) return;

  const box = itemBoxOf(item);
  const { expected, img } = itemIcon(item);
  let node: THREE.Object3D | null = null;
  if (img) node = iconDecal(img, box);
  else if (!expected) node = textSprite(itemLabel(item), box);
  if (!node) return;
  node.userData.decal = true;
  node.userData.noPick = true;
  group.add(node);
}

export interface BuildItemOpts {
  /** 非当前图层：半透明且不可拾取。 */
  dimmed: boolean;
  /** 该物件是堆叠宿主，且身上正放着厨具 —— 需要透出来让人看见里面那件。 */
  hasGuest?: boolean;
  /** 该物件是被堆叠的厨具（放在宿主身上）。 */
  isGuest?: boolean;
}

export function buildItemNode(item: EditorItem, opts: BuildItemOpts): THREE.Group {
  const cat = catalogItemForGuidOrPath(item.prefabGuid, item.prefabAssetPath);
  const box = itemBoxOf(item);
  const selected = isSelected(item._editorKey);
  const paint = paintStyleForItem(cat, item.parentPath, selected);
  const fill = parseCssColor(paint.fill);
  const stroke = parseCssColor(paint.stroke);

  const group = new THREE.Group();
  group.name = "item:" + item._editorKey;

  const isGhostBox = box.kind === "airwall" || box.kind === "collision";
  let opacity = opts.dimmed
    ? Math.min(0.28, fill.alpha)
    : isGhostBox
      ? 0.22
      : fill.alpha;
  // 嵌套可见性：搅拌碗放进搅拌台后完全被宿主盒体包住，2D 靠画序区分，
  // 3D 有深度缓冲就彻底看不见了。宿主降到半透明，让里面那件透出来。
  if (opts.hasGuest && !opts.dimmed && !selected) opacity = Math.min(opacity, 0.42);

  const geo = new THREE.BoxGeometry(box.w, box.h, box.d);
  const mat = new THREE.MeshLambertMaterial({
    color: fill.color,
    transparent: opacity < 1,
    opacity,
    side: THREE.FrontSide, // R4
    depthWrite: opacity > 0.85,
  });
  const mesh = new THREE.Mesh(geo, mat);
  mesh.position.y = box.h / 2;
  mesh.renderOrder = box.kind === "surface" ? ORDER.surfaceItem : isGhostBox ? ORDER.airWall : ORDER.item;
  if (box.kind === "surface" || box.kind === "plane") {
    // R3/R5：薄片贴在地板顶面上，靠 polygonOffset 压住共面闪烁。
    mat.polygonOffset = true;
    mat.polygonOffsetFactor = -1;
    mat.polygonOffsetUnits = -2;
  }
  group.add(mesh);

  // 线框：选中/空气墙时加粗可读性；被堆叠的厨具永远压在宿主之上，
  // 保证「台子里有个碗」一眼可辨。
  const edges = new THREE.LineSegments(
    new THREE.EdgesGeometry(geo),
    new THREE.LineBasicMaterial({
      color: opts.isGuest && !opts.dimmed ? new THREE.Color(0xffffff) : stroke.color,
      transparent: true,
      opacity: opts.dimmed ? 0.25 : isGhostBox ? 0.9 : opts.isGuest ? 0.95 : 0.75,
      depthTest: !isGhostBox && !opts.isGuest,
    })
  );
  edges.position.y = box.h / 2;
  edges.renderOrder = opts.isGuest ? ORDER.item + 3 : ORDER.item + 1;
  group.add(edges);

  // 正面标记：与 2D 的白色朝向三角同义，贴在物件【前脸】（局部 +Z）上方。
  if (box.kind === "normal" && !opts.dimmed) {
    const markW = Math.min(box.w * 0.5, CELL * 0.4);
    const marker = new THREE.Mesh(
      new THREE.PlaneGeometry(markW, Math.min(box.d * 0.18, CELL * 0.16)),
      new THREE.MeshBasicMaterial({
        color: 0xffffff,
        transparent: true,
        opacity: 0.75,
        side: THREE.DoubleSide,
        depthWrite: false,
      })
    );
    marker.rotation.x = -Math.PI / 2;
    marker.position.set(0, box.h + 0.004, box.d / 2 - Math.min(box.d * 0.12, CELL * 0.12));
    marker.renderOrder = ORDER.item + 2;
    group.add(marker);
  }

  // 顶面贴花（图标/文字）：与几何分开维护，图标异步到达时只换它。
  applyItemDecal(group, item, opts.dimmed);

  group.position.set(box.cx, box.baseY, toSceneZ(box.cz));
  // 手性换算见 space.ts：镜像 Z 后朝向是 π-θ，不是简单取负。
  group.rotation.y = toSceneRotY(box.rotDeg);

  tagPickable(group, { kind: "item", key: item._editorKey });
  if (opts.dimmed) {
    // 非当前层：只显示不可拾取（与 2D hitTestAll 的图层门一致）。
    group.traverse((o) => {
      o.userData.noPick = true;
    });
  }
  return group;
}

/**
 * 图标贴在物件【顶面】上（平铺贴花），而不是浮在半空。
 *
 * 与 2D 对齐：2D 把食材/锅具图标画在物件方框【内部】，所以 3D 的对应物是顶面
 * 贴花，而不是悬空 billboard —— 后者会让人分不清图标属于哪个物件（尤其堆叠时）。
 */
function iconDecal(img: HTMLImageElement, box: ItemBox): THREE.Mesh | null {
  const tex = iconTexture(img);
  if (!tex) return null;
  const aspect = img.naturalWidth > 0 && img.naturalHeight > 0 ? img.naturalWidth / img.naturalHeight : 1;
  // 贴满顶面的 78%，长边受较短的那条边约束，保证不溢出盒子。
  const fit = Math.min(box.w, box.d) * 0.78;
  const w = aspect >= 1 ? fit : fit * aspect;
  const h = aspect >= 1 ? fit / aspect : fit;
  const mesh = new THREE.Mesh(
    new THREE.PlaneGeometry(w, h),
    new THREE.MeshBasicMaterial({
      map: tex,
      transparent: true,
      side: THREE.DoubleSide,
      depthWrite: false,
      polygonOffset: true,
      polygonOffsetFactor: -2,
      polygonOffsetUnits: -4,
    })
  );
  mesh.rotation.x = -Math.PI / 2;
  // 贴花的「上」朝向物件前方，读图方向与 2D 一致。
  mesh.rotation.z = Math.PI;
  mesh.position.set(0, box.h + 0.006, 0);
  mesh.renderOrder = ORDER.label;
  mesh.userData.noPick = true;
  return mesh;
}

/** 文字标签：紧贴盒顶（不再悬空），无图标时才用。 */
function textSprite(text: string, box: ItemBox): THREE.Sprite | null {
  const tex = labelTexture(text);
  if (!tex) return null;
  const canvas = tex.image as HTMLCanvasElement;
  const h = 0.3;
  const sprite = new THREE.Sprite(
    new THREE.SpriteMaterial({ map: tex, transparent: true, depthTest: false })
  );
  sprite.scale.set((h * canvas.width) / canvas.height, h, 1);
  // 半个字高 + 一点余量：视觉上「坐」在盒顶而不是飘在上方。
  sprite.position.y = box.h + h * 0.5 + 0.02;
  sprite.renderOrder = ORDER.label;
  sprite.userData.noPick = true;
  return sprite;
}
