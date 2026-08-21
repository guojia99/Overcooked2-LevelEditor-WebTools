import {
  S,
  CELL,
  EditorItem,
  SaveScope
} from "./state";
import {
  uuid,
  normalizeRot,
  resolveFootprint,
  prefabIdFromPath
} from "./coords";
import {
  itemLayerOfIt,
  catalogItemById
} from "./catalog";
import {
  isThemedFloor,
  themedFloorNative,
  finalizeFloor
} from "./floors";
import { raftPiecesForRect } from "../raft";
import { STUB_KIND_BY_PREFAB_ID, isIngredientSprayId } from "./stubControls";
import type {
  LayoutItem,
  FloorObject,
  LayoutDocument,
  MoveControlData,
  ButtonLinkData,
  ButtonEventData
} from "../types";

export function serializeItemForDoc({ _editorKey, _wx, _wz, _parentWx, _parentWz, ...rest }: EditorItem): LayoutItem {
  const fp = resolveFootprint(rest);
  const cat = S.catalogByGuid.get(rest.prefabGuid);
  // 新放置物品 stubKind 为空：按 prefab id 映射补默认 stubKind（回收台/容器堆等
  // wrapper 无专属 stub 的道具靠它让后端补挂组件），已显式设置的优先。
  if (!rest.stubKind) {
    const mapId = cat?.id ?? "";
    if (mapId && STUB_KIND_BY_PREFAB_ID[mapId]) rest.stubKind = STUB_KIND_BY_PREFAB_ID[mapId];
  }
  // 喷雾喷罐不是锅具容器：历史/误配的 CookingUtensil stubKind 一律清除，
  // 防止后端 ApplyStub 补挂 PseudoPrefabCookingUtensil（宿主 Setup 对无容器的
  // child 抛 NRE）。
  if (rest.stubKind === "CookingUtensil" && isIngredientSprayId(prefabIdFromPath(rest.prefabAssetPath))) {
    rest.stubKind = "";
    rest.cookingUtensil = undefined;
  }
  // 可移动火锅：stubKind 保持空（不挂 CookingUtensil stub，宿主 Setup NRE）——
  // 但 cookingUtensil 字段保留（allowedIngredientGuids 会落到载体组件上）。
  const serPid = prefabIdFromPath(rest.prefabAssetPath);
  if (rest.stubKind === "CookingUtensil" && serPid === "utensil_large_pot_01_pushable") {
    rest.stubKind = "";
  }
  // Raft planks already expanded below are walkable:false; other floor prefabs stay walkable.
  const isRaftPlank = cat?.surfaceKind === "raft";
  // 空气墙是隐形碰撞块，不生成可行走 Col_Floor
  const isAirWall = rest.stubKind === "Collision" && rest.airWall === true;
  // 压力开关（含莲花变体）是自带碰撞的可踩踏机制：若 walkable:true，后端会为它生成
  // 一块隐形 Col_Floor，Play 期莲花底下出现「空气地板」。保持 walkable:false。
  const isPressureSwitch = rest.stubKind === "PressureSwitch";
  // 热气球桥（三格）真实 prefab 无 Ground 层碰撞（bundle 实测仅 x5 自带），
  // 按 footprint 生成 Col_Floor 才可走；x5 已自带碰撞，不重复生成。
  const isAirBalloonBridgeX3 = prefabIdFromPath(rest.prefabAssetPath) === "air_balloon_bridge_x3";
  return {
    ...rest,
    footprint: fp,
    worldPosition: { x: _wx, y: rest.localPosition?.y ?? 0, z: _wz },
    walkable: isAirWall || isPressureSwitch
      ? false
      : !isRaftPlank && (!!(cat && cat.surfaceTier === "floor") || isAirBalloonBridgeX3),
  };
}

export function serializeFloorsForDoc(): FloorObject[] {
  // Keep raft floors in floors[] so Unity SyncWalkableToFloors builds one Col_Floor
  // per raft rect. ApplyFloors skips surfaceKind=="raft" (no Plane mesh).
  return S.floors.map(({ _key, _wx, _wz, _wCells, _dCells, ...rest }) => ({
    ...rest,
    widthCells: _wCells,
    depthCells: _dCells,
    widthUnits: _wCells * CELL,
    depthUnits: _dCells * CELL,
    worldPosition: { x: _wx, y: rest.localPosition?.y ?? -0.05, z: _wz },
    localPosition: { x: _wx, y: rest.localPosition?.y ?? -0.05, z: _wz },
  }));
}

export function buildRaftItemsForDoc(): LayoutItem[] {
  const raftItems: LayoutItem[] = [];
  const missingIds = new Set<string>();
  for (const f of S.floors) {
    if (f.surfaceKind !== "raft") continue;
    for (const p of raftPiecesForRect(f._wCells, f._dCells)) {
      const cat = catalogItemById(p.id);
      if (!cat) {
        missingIds.add(p.id);
        continue;
      }
      const id = `new:raft:${uuid()}`;
      const px = f._wx + p.dx;
      const pz = f._wz + p.dz;
      raftItems.push({
        instanceId: id,
        hierarchyPath: id,
        prefabGuid: cat.guid,
        prefabAssetPath: cat.assetPath,
        parentPath: cat.defaultParent,
        displayName: cat.id,
        localPosition: { x: px, y: 0, z: pz },
        worldPosition: { x: px, y: 0, z: pz },
        localRotationY: p.rotY,
        footprint: cat.footprint,
        // Walkability comes from the retained raft floor rect (one Col_Floor),
        // not per-plank — dual lattice would otherwise stack overlapping colliders.
        walkable: false,
      });
    }
  }
  if (missingIds.size > 0) {
    throw new Error(
      `木筏拼块目录缺失：${[...missingIds].join(", ")}（请重新生成 catalog.json）`
    );
  }
  return raftItems;
}

export function buildThemedItemsForDoc(): LayoutItem[] {
  const themedItems: LayoutItem[] = [];
  for (const f of S.floors) {
    if (!isThemedFloor(f)) continue;
    const cat = S.catalogByGuid.get(f.prefabGuid!);
    if (!cat) {
      throw new Error(`主题地板 prefab 不在 catalog 中：${f.prefabGuid}（请重新生成 catalog.json）`);
    }
    // Preserve the exact original instance scale/rotation while the rect is
    // unchanged; after a resize, recompute from the prefab's native geometry.
    const nat = themedFloorNative(cat);
    const keepOrig =
      f.prefabScale && f._wCells === f.prefabScaleCellsW && f._dCells === f.prefabScaleCellsD;
    const sc = keepOrig
      ? f.prefabScale!
      : nat.depthAxis === "y"
        ? { x: f._wCells / nat.cellsPerScaleX, y: f._dCells / nat.cellsPerScaleZ, z: 1 }
        : { x: f._wCells / nat.cellsPerScaleX, y: 1, z: f._dCells / nat.cellsPerScaleZ };
    const id = `new:themed:${uuid()}`;
    themedItems.push({
      instanceId: id,
      hierarchyPath: id,
      prefabGuid: cat.guid,
      prefabAssetPath: cat.assetPath,
      parentPath: cat.defaultParent,
      displayName: cat.id,
      localPosition: { x: f._wx, y: f.localPosition?.y ?? 0, z: f._wz },
      worldPosition: { x: f._wx, y: f.localPosition?.y ?? 0, z: f._wz },
      localRotationX: keepOrig ? (f.prefabRotX ?? nat.rotX) : nat.rotX,
      localRotationY: normalizeRot(f.localRotationY),
      localScale: sc,
      footprint: cat.footprint,
      walkable: false,
    });
  }
  return themedItems;
}

export function buildDocument(only: SaveScope = ""): LayoutDocument {
  const moveDoc = (): MoveControlData | undefined => {
    // Move controls are baked straight into the scene (no external config) and are
    // only ever written on FULL saves — scoped saves must not touch existing groups.
    if (only) return undefined;
    return { groups: S.moveControls };
  };

  // 按钮↔移动组联动引用移动组，与 moveControls 一样只在全量保存时携带。
  const buttonLinkDoc = (): ButtonLinkData | undefined => {
    if (only) return undefined;
    return { links: S.buttonLinks };
  };

  // 按钮↔事件组联动引用场景物品，与 moveControls 一样只在全量保存时携带。
  const buttonEventDoc = (): ButtonEventData | undefined => {
    if (only) return undefined;
    return { links: S.buttonEvents };
  };

  if (only === "items" || only === "decor") {
    return {
      sceneAssetPath: S.scenePath,
      items: S.items
        .filter((it) => itemLayerOfIt(it) === only)
        .map(serializeItemForDoc),
      floors: undefined,
      moveControls: moveDoc(),
      // 开关联动随 items/decor 作用域一起写（链接两端都在物品层）
      switchLinks: S.switchLinks,
    };
  }

  // Re-finalize so write-back coords match the active snap step (avoids drift vs Unity apply).
  for (const f of S.floors) finalizeFloor(f);

  const raftItems = buildRaftItemsForDoc();
  const themedItems = buildThemedItemsForDoc();

  if (only === "floors") {
    return {
      sceneAssetPath: S.scenePath,
      // Surface-tier prefab items (travelators, water/background props, …) ride
      // along so Unity can move/delete them; themed/raft are regenerated.
      items: S.items
        .filter(
          (it) => itemLayerOfIt(it) === "floor" || itemLayerOfIt(it) === "background"
        )
        .map(serializeItemForDoc)
        .concat(raftItems)
        .concat(themedItems),
      floors: serializeFloorsForDoc(),
      moveControls: moveDoc(),
    };
  }

  return {
    sceneAssetPath: S.scenePath,
    items: S.items.map(serializeItemForDoc).concat(raftItems).concat(themedItems),
    floors: serializeFloorsForDoc(),
    moveControls: moveDoc(),
    switchLinks: S.switchLinks,
    buttonLinks: buttonLinkDoc(),
    buttonEvents: buttonEventDoc(),
    // 相机与灯光仅随全量写回（作用域保存不携带，Unity 侧 likewise no-op）。
    // cameraInfo 为 null 时省略字段（JSON.stringify 丢弃 undefined），
    // 后端 JsonUtility 对缺失字段得 null → ApplyCameraInfo no-op。
    cameraInfo: S.cameraInfo ?? undefined,
    lights: S.lights,
  };
}

export function scopedSaveMeta(): { scope: SaveScope; label: string; title: string } {
  if (S.currentLayer === "move") {
    return { scope: "", label: "🎬 移动组+全部", title: "移动层写回：连同物品/地板/背景一起保存（移动组需要完整文档）" };
  }
  if (S.currentLayer === "decor") {
    return { scope: "decor", label: "🎯 仅装饰", title: "仅写回装饰（不修改物品、地板、背景）" };
  }
  if (S.currentLayer === "floor" || S.currentLayer === "background") {
    return {
      scope: "floors",
      label: S.currentLayer === "background" ? "🎯 仅背景" : "🎯 仅地板",
      title: "仅写回地板 / 背景（不修改物品、装饰）",
    };
  }
  return { scope: "items", label: "🎯 仅核心物品", title: "仅写回核心物品（不修改地板、背景、装饰）" };
}

export function refreshScopedSaveButton(): void {
  const btn = document.getElementById("btn-save-items");
  if (!btn) return;
  const meta = scopedSaveMeta();
  btn.textContent = meta.label;
  btn.title = meta.title;
}
