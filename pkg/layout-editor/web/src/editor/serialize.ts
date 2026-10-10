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
  prefabIdFromPath,
  editorItemUnityWorldXZ
} from "./coords";
import {
  itemLayerOfIt,
  catalogItemById,
  ingredientIdByGuid,
  isResizableBackgroundItem,
  planeCatalogFootprint
} from "./catalog";
import {
  isThemedFloor,
  themedFloorNative,
  finalizeFloor
} from "./floors";
import { raftPiecesForRect } from "../raft";
import { STUB_KIND_BY_PREFAB_ID, isIngredientSprayId } from "./stubControls";
import { partLevelActive, partIdOf } from "./partLevel";
import type {
  LayoutItem,
  FloorObject,
  LayoutDocument,
  AnimControlData,
  ButtonLinkData,
  ButtonEventData,
  CoaxialLinkData
} from "../types";

/** 分 P：P 层物件/生成物（木筏拼板、主题地板瓦片）写回 slot 根下的子目录。
 *  defaultParent 形如 "Design/Counters" 取末段；其余（Art 等）原样挂在 slot 下。 */
function partParentPath(pid: string, defaultParent: string | undefined): string {
  const dp = defaultParent ?? "";
  const sub = dp.startsWith("Design/") && dp.length > "Design/".length ? dp.slice("Design/".length) : dp || "Art";
  return `Design/PartSlots/${pid}/${sub}`;
}

export function serializeItemForDoc({ _editorKey, _wx, _wz, _parentWx, _parentWz, ...rest }: EditorItem): LayoutItem {
  const fp = isResizableBackgroundItem(rest)
    ? planeCatalogFootprint(rest)
    : resolveFootprint(rest);
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
  if (rest.stubKind === "CookingUtensil" && serPid === "web_utensil_large_pot_01_pushable") {
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
  if (rest.timedSwitch) {
    rest.timedSwitch = {
      enabled: rest.timedSwitch.enabled === true,
      onSeconds: rest.timedSwitch.onSeconds ?? 30,
      offSeconds: rest.timedSwitch.offSeconds ?? 30,
      startOn: rest.timedSwitch.startOn !== false,
    };
  }
  // 步道定时反转：非 Travelator 物品上的一律丢弃（防污染，对齐后端守卫）；
  // 秒数下限 1（后端写回同款钳制）。
  if (rest.travelator?.timedReverse) {
    if (rest.stubKind !== "Travelator") {
      delete rest.travelator.timedReverse;
    } else {
      const tr = rest.travelator.timedReverse;
      rest.travelator.timedReverse = {
        enabled: tr.enabled === true,
        forwardSeconds: Math.max(1, tr.forwardSeconds ?? 10),
        backwardSeconds: Math.max(1, tr.backwardSeconds ?? 10),
        startReversed: tr.startReversed === true,
        turnAngle: tr.turnAngle ?? 180,
      };
    }
  }
  if (rest.stubKind === "IngredientDecor" || rest.ingredientDecor) {
    const ingGuid = rest.ingredientDecor?.ingredientGuid || rest.prefabGuid;
    let ingId = prefabIdFromPath(rest.prefabAssetPath);
    if (!ingId && ingGuid) ingId = ingredientIdByGuid(ingGuid);
    const wrapper = ingId ? catalogItemById(ingId) : undefined;
    if (wrapper?.category === "decor/food") {
      rest.prefabGuid = wrapper.guid;
      rest.prefabAssetPath = wrapper.assetPath;
    }
    delete rest.stubKind;
    delete rest.ingredientDecor;
  }
  if (rest.prefabAssetPath?.includes("/common03/prefabs/") && rest.prefabAssetPath.includes("/decor/")) {
    const decorId = prefabIdFromPath(rest.prefabAssetPath);
    const wrapper = decorId ? catalogItemById(decorId) : undefined;
    if (wrapper?.assetPath.includes("/commonW1/")) {
      rest.prefabGuid = wrapper.guid;
      rest.prefabAssetPath = wrapper.assetPath;
    }
  }
  const ly = rest.localPosition?.y ?? 0;
  // 空气斜坡：起点高度跟随物件 Y（唯一真源=localPosition.y），序列化时同步进 slope
  // 参数——后端写回以 worldPosition 为锚，slope.startY 供展示/校验用。
  if (rest.airSlope && rest.slope) {
    rest.slope = { ...rest.slope, startY: ly };
  }
  const unityXZ = editorItemUnityWorldXZ({ _wx, _wz, localRotationY: rest.localRotationY, prefabAssetPath: rest.prefabAssetPath, prefabGuid: rest.prefabGuid });
  // 分 P：enabled 时 partId 必填（缺省补 "base"，决策 §5.2）；普关剥离 part 字段。
  if (partLevelActive()) {
    // 玩家固定在场景（Chefs 根），不参与 P 替换：一律钉在 base。
    rest.partId = rest.stubKind === "Player" ? "base" : partIdOf(rest);
    if (rest.partId !== "base") {
      // P 层物件写回 slot 根下：Design/PartSlots/<pid>/<defaultParent 末段或 Art>。
      // 布局期 slot 根在原点（世界坐标=设计坐标），烘焙链末尾统一 park。
      const dp = cat?.defaultParent ?? "";
      const sub = dp.startsWith("Design/") && dp.length > "Design/".length ? dp.slice("Design/".length) : dp || "Art";
      rest.parentPath = `Design/PartSlots/${rest.partId}/${sub}`;
    }
  } else {
    delete rest.partId;
    delete rest.partScoped;
    delete rest.survivesPartSwitch;
  }
  return {
    ...rest,
    footprint: fp,
    localPosition: { x: unityXZ.x - _parentWx, y: ly, z: unityXZ.z - _parentWz },
    worldPosition: { x: unityXZ.x, y: ly, z: unityXZ.z },
    walkable: isAirWall || rest.airSlope === true || isPressureSwitch
      ? false
      : !isRaftPlank && (!!(cat && cat.surfaceTier === "floor") || isAirBalloonBridgeX3),
  };
}

export function serializeFloorsForDoc(): FloorObject[] {
  // Keep raft floors in floors[] so Unity SyncWalkableToFloors builds one Col_Floor
  // per raft rect. ApplyFloors skips surfaceKind=="raft" (no Plane mesh).
  return S.floors.map(({ _key, _wx, _wz, _wCells, _dCells, ...rest }) => {
    // 分 P：地板 partId 同物件归一化（普关剥离）；P 层地板写回 slot Ground 下，
    // 可行走碰撞作为 plane 子物体随 slot 根移动（SyncWalkableToFloors 分流）。
    if (partLevelActive()) {
      rest.partId = partIdOf(rest);
      if (rest.partId !== "base") rest.parentPath = `Design/PartSlots/${rest.partId}/Ground`;
    } else {
      delete rest.partId;
    }
    return {
    ...rest,
    widthCells: _wCells,
    depthCells: _dCells,
    widthUnits: _wCells * CELL,
    depthUnits: _dCells * CELL,
    worldPosition: { x: _wx, y: rest.localPosition?.y ?? -0.05, z: _wz },
    localPosition: { x: _wx, y: rest.localPosition?.y ?? -0.05, z: _wz },
    };
  });
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
      // 拼板跟随地板自身高度（含负高度下沉木筏），碰撞盒由地板矩形的 walkY 决定。
      const py = f.localPosition?.y ?? 0;
      const raftPid = partLevelActive() ? partIdOf(f) : undefined;
      raftItems.push({
        instanceId: id,
        hierarchyPath: id,
        prefabGuid: cat.guid,
        prefabAssetPath: cat.assetPath,
        // 分 P：木筏拼板跟随所属地板的阶段（写回 slot 根下）。
        parentPath:
          raftPid && raftPid !== "base" ? partParentPath(raftPid, cat.defaultParent) : cat.defaultParent,
        displayName: cat.id,
        localPosition: { x: px, y: py, z: pz },
        worldPosition: { x: px, y: py, z: pz },
        localRotationY: p.rotY,
        footprint: cat.footprint,
        // Walkability comes from the retained raft floor rect (one Col_Floor),
        // not per-plank — dual lattice would otherwise stack overlapping colliders.
        walkable: false,
        partId: raftPid,
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
    // The themed item IS the floor's scene object — share the floor's own
    // instanceId (always a unique "new:…" id) instead of minting a fresh one.
    // Unifying the ids lets the backend resolve the floor to this GameObject
    // (createdObjects[floor.instanceId]) so anim-group bindings and the walkable
    // Col_Floor attach to it; otherwise the floor id maps to nothing and the
    // themed floor never joins its move group ("绑定地板写回后没绑定").
    const id = f.instanceId && f.instanceId.startsWith("new:")
      ? f.instanceId
      : `new:themed:${uuid()}`;
    const themedPid = partLevelActive() ? partIdOf(f) : undefined;
    themedItems.push({
      instanceId: id,
      hierarchyPath: id,
      prefabGuid: cat.guid,
      prefabAssetPath: cat.assetPath,
      // 分 P：主题地板瓦片跟随所属地板的阶段（写回 slot 根下）。
      parentPath:
        themedPid && themedPid !== "base" ? partParentPath(themedPid, cat.defaultParent) : cat.defaultParent,
      displayName: cat.id,
      localPosition: { x: f._wx, y: f.localPosition?.y ?? 0, z: f._wz },
      worldPosition: { x: f._wx, y: f.localPosition?.y ?? 0, z: f._wz },
      localRotationX: keepOrig ? (f.prefabRotX ?? nat.rotX) : nat.rotX,
      localRotationY: normalizeRot(f.localRotationY),
      localScale: sc,
      footprint: cat.footprint,
      walkable: false,
      partId: themedPid,
    });
  }
  return themedItems;
}

export function buildDocument(only: SaveScope = ""): LayoutDocument {
  const animDoc = (): AnimControlData | undefined => {
    // Move controls are baked straight into the scene (no external config) and are
    // only ever written on FULL saves — scoped saves must not touch existing groups.
    if (only) return undefined;
    // 分 P：带 partId 的动画组组根强制迁入 slot（组根随 slot 根移动，决策 D3）；
    // 转场 fx 组 startTrigger 自动接 PartSwitch_<from>_<to>（转场开始即演出）。
    if (partLevelActive() && S.partLevel) {
      for (const g of S.animControls) {
        if (g.partId && g.partId !== "base" && g.groupKind === "members") {
          const safeName = (g.displayName || g.id).replace(/[/\\?%*:|"<>.]/g, "_");
          g.groupHierarchyPath = `Design/PartSlots/${g.partId}/AnimGroups/${safeName}`;
        }
        if (g.groupKind === "fx") {
          const t = (S.partLevel.transitions ?? []).find((tr) => tr.fxGroupId === g.id);
          if (t) g.startTrigger = `PartSwitch_${t.fromPartId}_${t.toPartId}`;
        }
      }
    }
    return { groups: S.animControls };
  };

  // 按钮↔动画组联动引用动画组，与 animControls 一样只在全量保存时携带。
  const buttonLinkDoc = (): ButtonLinkData | undefined => {
    if (only) return undefined;
    return { links: S.buttonLinks };
  };

  // 按钮↔事件组联动引用场景物品，与 animControls 一样只在全量保存时携带。
  const buttonEventDoc = (): ButtonEventData | undefined => {
    if (only) return undefined;
    return { links: S.buttonEvents };
  };

  // 同轴按钮组引用场景物品，与 animControls 一样只在全量保存时携带。
  const coaxialLinkDoc = (): CoaxialLinkData | undefined => {
    if (only) return undefined;
    return { links: S.coaxialLinks };
  };

  if (only === "items" || only === "decor") {
    return {
      sceneAssetPath: S.scenePath,
      hasCeilingHeight: false,
      // 分 P 元数据随所有作用域携带（决策 D5：物件全量携带该层所有 P，
      // 桥侧 part_level~ 存档合并；后端 part 分桶删除在 M2 落地）。
      partLevel: S.partLevel ?? undefined,
      items: S.items
        .filter((it) => itemLayerOfIt(it) === only)
        .map(serializeItemForDoc),
      floors: undefined,
      animControls: animDoc(),
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
      hasCeilingHeight: false,
      partLevel: S.partLevel ?? undefined,
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
      animControls: animDoc(),
    };
  }

  return {
    sceneAssetPath: S.scenePath,
    hasCeilingHeight: S.hasCeilingHeight,
    ceilingHeight: Math.max(0, Math.min(10, S.ceilingHeight)),
    partLevel: S.partLevel ?? undefined,
    items: S.items.map(serializeItemForDoc).concat(raftItems).concat(themedItems),
    floors: serializeFloorsForDoc(),
    animControls: animDoc(),
    switchLinks: S.switchLinks,
    buttonLinks: buttonLinkDoc(),
    buttonEvents: buttonEventDoc(),
    coaxialLinks: coaxialLinkDoc(),
    // 相机与灯光仅随全量写回（作用域保存不携带，Unity 侧 likewise no-op）。
    // cameraInfo 为 null 时省略字段（JSON.stringify 丢弃 undefined），
    // 后端 JsonUtility 对缺失字段得 null → ApplyCameraInfo no-op。
    cameraInfo: S.cameraInfo ?? undefined,
    lights: S.lights,
  };
}

export function scopedSaveMeta(): { scope: SaveScope; label: string; title: string } {
  if (S.currentLayer === "anim") {
    return { scope: "", label: "🎬 动画组+全部", title: "动画层写回：连同物品/地板/背景一起保存（动画组需要完整文档）" };
  }
  if (S.currentLayer === "decor") {
    return { scope: "decor", label: "🎯 仅装饰", title: "仅写回装饰（不修改物品、地板、背景）" };
  }
  if (S.currentLayer === "floor" || S.currentLayer === "background") {
    return {
      scope: "floors",
      label: "🎯 仅地板与背景",
      title: "写回地板矩形、表面物与背景 prefab（不修改核心物品与装饰）",
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
