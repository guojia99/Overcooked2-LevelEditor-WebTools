import { HistoryStack } from "../history";
import type {
  ButtonEventLink,
  ButtonLink,
  CameraInfo,
  CatalogItem,
  CounterAppearanceCatalog,
  DeathInfo,
  FloorMaterial,
  FloorObject,
  GridInfo,
  IngredientEntry,
  LayoutItem,
  LevelSetScene,
  LightInfo,
  MoveGroup,
  RecipeEntry,
  SwitchLink,
  SwitchMaterialOption,
  WalkableRect
} from "../types";

export type EditorItem = LayoutItem & {
  _editorKey: string;
  _wx: number;
  _wz: number;
  _parentWx: number;
  _parentWz: number;
};

export type EditorFloor = FloorObject & {
  _key: string;
  _wx: number;
  _wz: number;
  _wCells: number;
  _dCells: number;
};

/** 联合组合（palette「联合组合」分类）：一次放置多个物品并自动完成联动配置。 */
export interface ComboPart {
  /** 目录物品 id（catalogItemById 查找）。 */
  id: string;
  /** 相对主物品的偏移（格，1 格 = CELL 米）。 */
  dx: number;
  dz: number;
}

export interface ComboDef {
  id: string;
  nameZh: string;
  /** 自动配置说明（卡片副标题）。 */
  hint: string;
  /** 第一个为主物品（落在拖放点）。 */
  parts: ComboPart[];
  /** 全部放置成功后的自动联动配置（按 parts 顺序接收 EditorItem）。 */
  link: (items: EditorItem[]) => void;
}

export type LayerKey = "items" | "decor" | "floor" | "background" | "move";

/** Content categories that can be shown / hidden on the canvas per layer. */
export type VisibilityCategory = "items" | "decor" | "floors" | "background";

export interface LayerVisibility {
  items: boolean;
  decor: boolean;
  floors: boolean;
  background: boolean;
}

export function isFloorLikeLayer(layer: LayerKey): boolean {
  return layer === "floor" || layer === "background";
}

export function makeLayerVisibility(): Record<LayerKey, LayerVisibility> {
  const all = { items: true, decor: true, floors: true, background: true } as LayerVisibility;
  const noBg = { items: true, decor: true, floors: true, background: false } as LayerVisibility;
  return {
    items: noBg,
    decor: noBg,
    floor: all,
    background: all,
    move: noBg,
  };
}

export interface EditorSnapshot {
  items: EditorItem[];
  floors: EditorFloor[];
  bgThemeKey: string;
  /** Move-control groups (undo/redo must restore them too). */
  moveControls: MoveGroup[];
  /** 开关联动（按钮 → 断头台/饮料机/酱料机；undo/redo 一并恢复）。 */
  switchLinks: SwitchLink[];
  /** 按钮/压力开关 ↔ 移动组联动（undo/redo 一并恢复）。 */
  buttonLinks: ButtonLink[];
  /** 按钮 ↔ 事件组联动（undo/redo 一并恢复）。 */
  buttonEvents: ButtonEventLink[];
  /** 相机（背景色/FOV，undo/redo 一并恢复）。 */
  cameraInfo: CameraInfo | null;
  /** Art/Lights 非 prefab 灯光（undo/redo 一并恢复）。 */
  lights: LightInfo[];
}

/** Move layer interaction mode. "members" = pick/box-select members (items + floors);
 *  "waypoints" = click empty canvas to place waypoints. */
export type MoveMode = "none" | "members" | "waypoints";

/** Route preview playback state (pure front-end simulation). */
export interface MovePreview {
  playing: boolean;
  /** Elapsed simulation seconds since the preview started. */
  t: number;
  /** group id being previewed. */
  groupId: string;
}

export interface PickCandidate {
  title: string;
  sub: string;
  onPick: () => void;
}

export interface FloorHit {
  floor: EditorFloor;
  mode: "move" | "resize";
  edge: string;
  anchorX: number;
  anchorZ: number;
}

/** Layer-scoped write-back: "" = full, "items" / "decor" / "floors". */
export type SaveScope = "" | "items" | "decor" | "floors";

export const CELL = 1.2;
export const HALF_CELL = CELL / 2;
/** 磁吸半径：距半格网格 0.1 内才吸附到网格，其余位置按所选精度自由摆放。 */
export const MAGNET_THRESHOLD = 0.1;
export const PX_PER_UNIT = 48;

export const FOOTPRINT_BY_ID: Record<string, { cellsX: number; cellsZ: number }> = {
  ServingStation: { cellsX: 2, cellsZ: 1 },
  // 所有水槽 2×1
  Sink: { cellsX: 2, cellsZ: 1 },
  SinkGlass: { cellsX: 2, cellsZ: 1 },
  workstation_sink_mug_01_wood: { cellsX: 2, cellsZ: 1 },
  dlc09_workstation_sink_mug_01_wood: { cellsX: 2, cellsZ: 1 },
  dlc13_workstation_sink_01_wood: { cellsX: 2, cellsZ: 1 },
  workstation_sink_01_summer: { cellsX: 2, cellsZ: 1 },
  dlc08_workstation_01_tray_sink_circus: { cellsX: 2, cellsZ: 1 },
  dlc08_workstation_02_tray_sink_circus: { cellsX: 2, cellsZ: 1 },
  dlc08_workstation_03_tray_sink_circus: { cellsX: 2, cellsZ: 1 },
  GlassReturn: { cellsX: 1, cellsZ: 1 },
  utensil_large_pot_01: { cellsX: 2, cellsZ: 2 },
  utensil_dlc10_large_pot_01: { cellsX: 2, cellsZ: 2 },
  // 火锅地面灶台铺满 2×2（大锅 2×2 锅沿外架其上）
  cooking_region_floorburner: { cellsX: 2, cellsZ: 2 },
  dlc10_cooking_region_floorburner: { cellsX: 2, cellsZ: 2 },
  pushable_object: { cellsX: 2, cellsZ: 2 },
  dlc10_pushable_object: { cellsX: 2, cellsZ: 2 },
  // 断头台 2×1（切菜台）；大炮 2×2
  workstation_guillotine_01: { cellsX: 2, cellsZ: 1 },
  dlc08_cannon: { cellsX: 2, cellsZ: 2 },
  dlc09_cannon: { cellsX: 2, cellsZ: 2 },
  // 半格小型厨具/工具：1×1 向内缩 10%（0.9 格）；旧数据误设为 1×1
  utensil_bellows_01: { cellsX: 0.9, cellsZ: 0.9 },
  Bellows: { cellsX: 0.9, cellsZ: 0.9 },
  utensil_water_gun_01: { cellsX: 0.9, cellsZ: 0.9 },
  WaterGun: { cellsX: 0.9, cellsZ: 0.9 },
  FireExtinguisher: { cellsX: 0.9, cellsZ: 0.9 },
  utensil_fire_extinguisher_02: { cellsX: 0.9, cellsZ: 0.9 },
  dlc08_utensil_fire_extinguisher: { cellsX: 0.9, cellsZ: 0.9 },
  ToastingFork: { cellsX: 0.9, cellsZ: 0.9 },
  utensil_toasting_fork_01: { cellsX: 0.9, cellsZ: 0.9 },
  Skewer: { cellsX: 0.9, cellsZ: 0.9 },
  utensil_skewer_01: { cellsX: 0.9, cellsZ: 0.9 },
  MixerBowl: { cellsX: 0.9, cellsZ: 0.9 },
  BlenderCup: { cellsX: 0.9, cellsZ: 0.9 },
  utensil_blender_01: { cellsX: 0.9, cellsZ: 0.9 },
  Blender: { cellsX: 0.9, cellsZ: 0.9 },
  dlc02_utensil_mixer: { cellsX: 0.9, cellsZ: 0.9 },
  dlc03_utensil_mixer: { cellsX: 0.9, cellsZ: 0.9 },
  dlc05_utensil_mixer: { cellsX: 0.9, cellsZ: 0.9 },
  dlc07_utensil_mixer_01: { cellsX: 0.9, cellsZ: 0.9 },
  dlc08_utensil_mixer_01: { cellsX: 0.9, cellsZ: 0.9 },
  dlc09_utensil_mixer: { cellsX: 0.9, cellsZ: 0.9 },
  dlc13_utensil_mixer_01: { cellsX: 0.9, cellsZ: 0.9 },
  FryPan: { cellsX: 0.9, cellsZ: 0.9 },
  dlc02_utensil_frying_pan: { cellsX: 0.9, cellsZ: 0.9 },
  dlc05_utensil_frying_pan: { cellsX: 0.9, cellsZ: 0.9 },
  dlc07_utensil_frying_pan_01: { cellsX: 0.9, cellsZ: 0.9 },
  dlc08_utensil_frying_pan: { cellsX: 0.9, cellsZ: 0.9 },
  dlc09_utensil_frying_pan: { cellsX: 0.9, cellsZ: 0.9 },
  utensil_griddlepan: { cellsX: 0.9, cellsZ: 0.9 },
  GriddlePan: { cellsX: 0.9, cellsZ: 0.9 },
  utensil_big_ol_spoon: { cellsX: 0.9, cellsZ: 0.9 },
  utensil_dlc10_big_ol_spoon: { cellsX: 0.9, cellsZ: 0.9 },
  utensil_coalbucket_01: { cellsX: 0.9, cellsZ: 0.9 },
  p_dlc7_coal_bucket_coal_01: { cellsX: 0.9, cellsZ: 0.9 },
  utensil_ingredient_spray_01: { cellsX: 0.9, cellsZ: 0.9 },
  dlc09_utensil_ingredient_spray: { cellsX: 0.9, cellsZ: 0.9 },
};

/** 中心 pivot 道具（pivot 在多格footprint正中，工作位在根 ±半格）：根必须落在
 *  半格奇偶位（0.6 mod 1.2）长轴上，两个工作位才对齐格子中心。断头台 2×1 的
 *  counter 在 ±0.53，被宿主 EditorGridSnap 拉回整格即错位（左格拿不到）——
 *  后端 LayoutEditorGridSnapGuard 已解除其 X/Z 约束，前端磁吸同步固定奇偶位。 */
export const CENTER_PIVOT_PREFAB_IDS = new Set(["workstation_guillotine_01"]);

/** 全部编辑器可变状态（单例）。模块间共享，禁止顶层访问 DOM 的状态也在此。 */
export const S = {
  freeSnapStep: 0.01,
  catalogByGuid: new Map<string, CatalogItem>(),
  catalogById: new Map<string, CatalogItem>(),
  counterAppearances: null as CounterAppearanceCatalog | null,
  switchMaterialsCache: [] as SwitchMaterialOption[],
  items: [] as EditorItem[],
  floors: [] as EditorFloor[],
  walkable: [] as WalkableRect[],
  deathInfo: null as DeathInfo | null,
  /** 游戏相机（背景色/FOV；写回时随全量保存携带）。 */
  cameraInfo: null as CameraInfo | null,
  /** Art/Lights 非 prefab 灯光。 */
  lights: [] as LightInfo[],
  /** 画布上显示相机视野范围（FOV 视锥与地面交线）。 */
  showCameraFov: localStorage.getItem("showCameraFov") !== "0",
  bgThemeKey: "void",
  bgThemeDirty: false,
  autoKillPlane: false,
  autoWalkable: true,
  allowWorkstationOverlap: false,
  backgroundEditable: false,
  floorMaterials: [] as FloorMaterial[],
  selectedKey: null as string | null,
  selectedKeys: new Set<string>(),
  selectedFloorKey: null as string | null,
  selectedFloorKeys: new Set<string>(),
  currentLayer: "items" as LayerKey,
  /** 装饰层尺寸筛选：all / small / medium / large / xl（按 footprint 判定）。 */
  decorSizeFilter: "all" as "all" | "small" | "medium" | "large" | "xl",
  /** Per-layer canvas visibility of each content category. */
  layerVisibility: makeLayerVisibility(),
  scenePath: "",
  snapEnabled: true,
  showGrid: true,
  showCoords: true,
  hoverCx: -1,
  hoverCy: -1,
  gridInfo: null as GridInfo | null,
  moveControls: [] as MoveGroup[],
  switchLinks: [] as SwitchLink[],
  buttonLinks: [] as ButtonLink[],
  buttonEvents: [] as ButtonEventLink[],
  activeMoveGroupId: null as string | null,
  activeMoveEventIdx: null as number | null,
  selectedWaypointId: null as string | null,
  /** Active tab inside the move group editor. */
  activeMoveTab: "members" as "members" | "events" | "waypoints" | "settings",
  /** Ids of member groups currently collapsed (default: expanded). */
  collapsedGroupIds: new Set<string>(),
  /** Member group targeted by the next "框选添加" pick (null = ungrouped). */
  movePickTargetGroupId: null as string | null,
  /** Explicit interaction mode on the move layer (see MoveMode). */
  moveMode: "none" as MoveMode,
  /** Route preview (front-end simulation only). */
  movePreview: null as MovePreview | null,
  /** Event indices whose time-curve editor is expanded (collapsed by default). */
  openCurves: new Set<number>(),
  /** Event indices whose waypoint-pool strip is expanded (collapsed by default). */
  openWaypointPool: new Set<number>(),
  /** Canvas-placed waypoints are appended to the active event's route right away. */
  moveRouteAutoAdd: true,
  expandedMemberId: null as string | null,
  activeRightTab: "items" as "items" | "move" | "bevents",
  draggingWaypointId: null as string | null,
  scale: 1,
  panX: 0,
  panY: 0,
  dragCatalog: null as CatalogItem | null,
  dragCombo: null as ComboDef | null,
  dragItemKey: null as string | null,
  dragOffsetX: 0,
  dragOffsetZ: 0,
  dragGroupKeys: [] as string[],
  dragLastWx: 0,
  dragLastWz: 0,
  marqueeing: false,
  marqueeAdd: false,
  marqueeStartX: 0,
  marqueeStartY: 0,
  marqueeCurX: 0,
  marqueeCurY: 0,
  spaceHeld: false,
  panning: false,
  lastMx: 0,
  lastMy: 0,
  dragFloorKey: null as string | null,
  dragFloorGroupKeys: [] as string[],
  dragFloorMode: "move" as "move" | "resize",
  dragFloorEdge: "",
  dragFloorAnchorX: 0,
  dragFloorAnchorZ: 0,
  ingredientsCache: [] as IngredientEntry[],
  intermediatesCache: [] as RecipeEntry[],
  autoIntermediates: true,
  currentLevelSet: "",
  sceneListCache: [] as LevelSetScene[],
  fillIncludeMainDough: false,
  history: new HistoryStack<EditorSnapshot>(20),
  dirty: false,
  dragSnapshot: null as EditorSnapshot | null,
  clipboard: [] as EditorItem[],
  pasteRound: 0,
  floorClipboard: [] as EditorFloor[],
  /** 地板层复制携带的地板层物品（压力开关等 surface 物品，随地板一起复制/粘贴）。 */
  floorItemClipboard: [] as EditorItem[],
  floorPasteRound: 0,
  sceneItemListSig: "",
  paletteCollapsed: localStorage.getItem("paletteCollapsed") === "1",
  itemsPanelCollapsed: localStorage.getItem("itemsPanelCollapsed") === "1",
  corePaletteGroupMeta: new Map<string, string>(),
  pendingNewFloor: false,
  pendingNewFloorCat: null as CatalogItem | null,
  pendingNewAirFloor: false,
  bridgeWasUp: false,
  bridgeStopAlerted: false,
  bridgeFailCount: 0,
  teleportalLabels: new Map<string, string>(),
  paramLabels: new Map<string, string>(),
  paramColors: new Map<string, string>(),
};
