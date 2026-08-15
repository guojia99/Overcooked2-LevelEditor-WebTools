import { HistoryStack } from "../history";
import type {
  CatalogItem,
  CounterAppearanceCatalog,
  DeathInfo,
  FloorMaterial,
  FloorObject,
  GridInfo,
  IngredientEntry,
  LayoutItem,
  LevelSetScene,
  MoveGroup,
  RecipeEntry,
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
  GlassReturn: { cellsX: 1, cellsZ: 1 },
  utensil_large_pot_01: { cellsX: 2, cellsZ: 2 },
  utensil_dlc10_large_pot_01: { cellsX: 2, cellsZ: 2 },
  pushable_object: { cellsX: 2, cellsZ: 2 },
  dlc10_pushable_object: { cellsX: 2, cellsZ: 2 },
};

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
  activeRightTab: "items" as "items" | "move",
  draggingWaypointId: null as string | null,
  scale: 1,
  panX: 0,
  panY: 0,
  dragCatalog: null as CatalogItem | null,
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
  floorPasteRound: 0,
  sceneItemListSig: "",
  paletteCollapsed: localStorage.getItem("paletteCollapsed") === "1",
  itemsPanelCollapsed: localStorage.getItem("itemsPanelCollapsed") === "1",
  showPaletteVariants: localStorage.getItem("showPaletteVariants") === "1",
  webSyncVersion: "",
  webSyncDisabled: false,
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
