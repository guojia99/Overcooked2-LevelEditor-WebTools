export interface CatalogStackMeta {
  /** Vertical local Y when parented under Design/Utensils (plate ≈ 1, pot ≈ 0.6). */
  y: number;
  hostRule:
    | "counter_standard"
    | "cooker"
    | "frying_station"
    | "mixer"
    | "blender"
    | "barbeque"
    | "campfire";
}

export interface CatalogItem {
  id: string;
  guid: string;
  assetPath: string;
  category: string;
  theme: string | null;
  nameZh: string;
  nameEn: string;
  defaultParent: string;
  footprint: { cellsX: number; cellsZ: number };
  /** core = gameplay; decor = art / NPC props; floor = floor/background surfaces */
  layoutTier?: "core" | "decor" | "floor";
  /** Floor-layer classification (set for floor/background catalog items). */
  surfaceTier?: "floor" | "background";
  surfaceKind?:
    | "solid"
    | "raft"
    | "ice"
    | "snow"
    | "sand"
    | "alien"
    | "walkway"
    | "carpet"
    | "section"
    | "conveyor"
    | "decal"
    | "background";
  stack?: CatalogStackMeta;
  /** True when an extracted icon PNG exists under web/public/icons/catalog/<id>.png. */
  icon?: boolean;
}

export interface CatalogPaletteGroup {
  key: string;
  labelZh: string;
  labelEn: string;
  layoutTier: "core" | "decor" | "floor";
  itemCount: number;
}

export interface Catalog {
  generatedAt: string;
  schemaVersion?: number;
  gridCellSize: number;
  itemCount: number;
  items: CatalogItem[];
  byCategory: Record<string, CatalogItem[]>;
  paletteGroups?: CatalogPaletteGroup[];
}

export interface LayoutVector3 {
  x: number;
  y: number;
  z: number;
}

export interface LayoutDispenserStub {
  spawnerItemPrefabGuid?: string;
}

export interface LayoutFoodSpawnerStub {
  spawnInOrder?: boolean;
  attachmentPrefabGuids?: string[];
  weights?: number[];
  triggerTime?: number;
  triggerAtStart?: boolean;
}

export interface LayoutConveyorStub {
  conveySpeed?: number;
}

export interface LayoutTeleportalStub {
  exitPortalInstanceId?: string;
  portalColor?: number;
  doubleSided?: boolean;
}

export interface LayoutCookingUtensilStub {
  capacity?: number;
  allowedIngredientGuids?: string[];
}

export interface LayoutTravelatorStub {
  speed?: number;
}

export interface LayoutFlamethrowerStub {
  cookingRate?: number;
}

export interface LayoutCleanPlateStackStub {
  plateCount?: number;
  platePrefabGuid?: string;
}

export interface LayoutBurnerStub {
  fireMode?: number;
  airTime?: number;
  randomTargetOrder?: boolean;
  hideVisual?: boolean;
}

export interface LayoutPlayerStub {
  playerID?: number;
}

export interface LayoutServingStationStub {
  /** Legacy single binding (one PlateReturn). Kept for back-compat; superseded
   *  by plateReturnInstanceIds. */
  plateReturnInstanceId?: string;
  /** One-to-many binding to return stations (PlateReturn 脏盘台 and/or
   *  GlassReturn 脏杯台 — they share the same stub type). A serving station may
   *  bind to many returns; a return belongs to at most one serving station. */
  plateReturnInstanceIds?: string[];
}

export interface LayoutItem {
  instanceId: string;
  hierarchyPath: string;
  prefabGuid: string;
  prefabAssetPath: string;
  parentPath: string;
  displayName: string;
  localPosition: LayoutVector3;
  worldPosition?: LayoutVector3;
  /** Euler X in degrees — quad-based floor tiles lie flat via x=90. */
  localRotationX?: number;
  localRotationY: number;
  localScale?: LayoutVector3;
  footprint?: { cellsX: number; cellsZ: number };
  walkable?: boolean;
  stubKind?: string;
  dispenser?: LayoutDispenserStub;
  conveyor?: LayoutConveyorStub;
  teleportal?: LayoutTeleportalStub;
  foodSpawner?: LayoutFoodSpawnerStub;
  cookingUtensil?: LayoutCookingUtensilStub;
  travelator?: LayoutTravelatorStub;
  flamethrower?: LayoutFlamethrowerStub;
  cleanPlateStack?: LayoutCleanPlateStackStub;
  burner?: LayoutBurnerStub;
  player?: LayoutPlayerStub;
  servingStation?: LayoutServingStationStub;
}

export interface LayoutDocument {
  sceneAssetPath: string;
  items: LayoutItem[];
  /** Editable floor/background plane objects (scene-embedded). */
  floors?: FloorObject[];
  /** Read-only walkable collision rectangles. */
  walkable?: WalkableRect[];
  /** Read-only death configuration. */
  deathInfo?: DeathInfo;
}

export type SurfaceKind =
  | "solid"
  | "ice"
  | "snow"
  | "sand"
  | "alien"
  | "walkway"
  | "carpet"
  | "section"
  | "conveyor"
  | "background";

export interface FloorObject {
  instanceId: string;
  hierarchyPath: string;
  parentPath: string;
  displayName: string;
  surfaceKind: string;
  meshType: "plane" | "quad" | "prefab";
  meshFileId?: number;
  materialGuid?: string;
  materialAssetPath?: string;
  materialName?: string;
  localPosition: LayoutVector3;
  worldPosition: LayoutVector3;
  localRotationY: number;
  localScale: LayoutVector3;
  widthUnits: number;
  depthUnits: number;
  widthCells: number;
  depthCells: number;
  prefabGuid?: string;
  prefabAssetPath?: string;
  /** Themed floor: exact prefab-instance scale/rotation captured at merge time,
   *  reused on write-back while the rect size is unchanged (official scenes use
   *  non-cell-aligned scales like 4×2.5m — quantizing them would shift visuals). */
  prefabScale?: LayoutVector3;
  prefabScaleCellsW?: number;
  prefabScaleCellsD?: number;
  /** Euler X of the source prefab instance (default 90 for flat quads). */
  prefabRotX?: number;
  /** Optional manual tint color (hex) for a solid floor — a user-driven recolor
   *  feature independent of the floor's material / surface kind. */
  tintColor?: string;
  /** Whether the tint is active. When false the floor uses its real material
   *  even if tintColor is set (so you can switch materials without the tint
   *  overriding). */
  tintEnabled?: boolean;
  /** Image floor: a solid Plane textured by an image uploaded into the level's
   *  data dir (Assets/LevelSets/<set>/data/). Set together with imageMode. */
  imageTexturePath?: string;
  /** Image tiling mode: "tile" repeats the image once per cell; "stretch"
   *  stretches one copy across the whole floor rect. */
  imageMode?: "tile" | "stretch";
  /** Image opacity 0..1 (default 1 = fully opaque). */
  imageOpacity?: number;
}

export interface WalkableRect {
  surfaceType: "solid" | "ice";
  cx: number;
  cz: number;
  sx: number;
  sz: number;
  sourcePath?: string;
}

export interface KillPlaneInfo {
  hierarchyPath: string;
  respawnType: string;
  deathEffectName: string;
  cx: number;
  cz: number;
  sx: number;
  sz: number;
}

export interface DeathInfo {
  deathType: "water" | "goo" | "fall";
  deathEffectName: string;
  killPlanes: KillPlaneInfo[];
}

export interface FloorMaterial {
  guid: string;
  id: string;
  assetPath: string;
  nameZh: string;
  sizeTag?: string;
  /** Level set name or "common01"/"common02" (static floor-materials.json only). */
  source?: string;
}

export interface FloorMaterialCatalog {
  materials: FloorMaterial[];
}

export interface GridInfo {
  found: boolean;
  worldPosition: LayoutVector3;
  cellSize: LayoutVector3;
  gridHalfSizeX: number;
  gridHalfSizeZ: number;
  origin: number;
}

export interface LevelSetScene {
  assetPath: string;
  levelSet: string;
  sceneName: string;
}

export type FoodGroup = "core" | "custom" | "dlc02" | "dlc05" | string;

export interface IngredientEntry {
  guid: string;
  id: string;
  nameZh: string;
  nameEn?: string;
  assetPath: string;
  group?: FoodGroup;
  icon?: boolean;
}

export interface RecipeEntry {
  guid: string;
  id: string;
  nameZh: string;
  nameEn?: string;
  assetPath: string;
  cookingStep?: string;
  ingredients?: string[];
  ingredientCount?: number;
  cookingStepCount?: number;
  score?: number;
  isCustom?: boolean;
  group?: FoodGroup;
  /** Recipe family: burger / pizza / sushi / kebab / smoothie / … */
  type?: string;
  /** score-0 半成品（面糊/炸物部件/自选披萨部件），不可作为关卡菜谱 */
  intermediate?: boolean;
  icon?: boolean;
}

export interface CookingStepEntry {
  guid: string;
  id: string;
  nameZh: string;
  nameEn?: string;
  kind: "cooking" | "plating";
  assetPath: string;
  bundleName?: string;
  group?: FoodGroup;
}

export interface CookingStepCatalog {
  cookingSteps: CookingStepEntry[];
}

export interface DirectoryEvent {
  id: string;
  eventsZh: string[];
  desc: string;
}

export interface AudioTheme {
  key: string;
  directories: string[];
  ambiences: string[];
  bgm: string[];
  deathTheme: string;
}

export interface DeathTheme {
  key: string;
  effectIdHint: string;
  note: string;
}

export interface AmbienceLabel {
  name: string;
  zh: string;
}

export interface AudioKnowledge {
  baseBundles: string[];
  alwaysLoadedBundles: string[];
  mandatoryDirectoryIds: string[];
  directoryEvents: DirectoryEvent[];
  themes: AudioTheme[];
  deathThemes: DeathTheme[];
  ambienceLabels: AmbienceLabel[];
}

export interface AudioCatalog extends AudioKnowledge {
  music: MusicEntry[];
  audioDirectories: AudioDirectoryEntry[];
  deathEffects: DeathEffectEntry[];
}

export interface BundleAnalysis {
  base: string[];
  alwaysLoaded: string[];
  required: string[];
  current: string[];
  missing: string[];
  extras: string[];
}

export interface LevelRecipes {
  levelInfoAssetPath: string;
  levelName: string;
  recipeGuids: string[];
}

// ---------- Level admin (sets / levels / audio) ----------

export interface MusicEntry {
  guid: string;
  id: string;
  assetPath: string;
  bundleName: string;
  nameZh: string;
}

export interface MusicCatalog {
  music: MusicEntry[];
}

export interface AudioDirectoryEntry {
  guid: string;
  id: string;
  assetPath: string;
  bundleName: string;
  nameZh: string;
}

export interface AudioDirectoryCatalog extends AudioKnowledge {
  audioDirectories: AudioDirectoryEntry[];
}


export interface AmbienceCatalog {
  ambiences: string[];
  ambienceLabels: AmbienceLabel[];
}

export interface DeathEffectEntry {
  guid: string;
  id: string;
  assetPath: string;
  nameZh: string;
}

export interface DeathEffectCatalog {
  deathEffects: DeathEffectEntry[];
}

export interface LevelSetInfo {
  setName: string;
  assetPath: string;
  dataDir: string;
  levelSetName: string;
  levelSetNameZH: string;
  author: string;
  version: string;
  uid: string;
  levelCount: number;
}

export interface LevelSetList {
  sets: LevelSetInfo[];
}

export interface LevelSummary {
  assetPath: string;
  dataDir: string;
  levelName: string;
  levelNameZH: string;
  sceneName: string;
  sceneAssetPath: string;
  hasScreenshot: boolean;
  hasScene: boolean;
}

export interface LevelList {
  levels: LevelSummary[];
}

export interface PerPlayerConfig {
  exists: boolean;
  orderLifeTime: number;
  timeBetweenOrders: number;
  plateReturnTime: number;
  survivalTimeMultiplier: number;
  roundTime: number;
  oneStarScore: number;
  twoStarScore: number;
  threeStarScore: number;
  fourStarScore: number;
}

export interface AudioConfig {
  inLevelMusicGuid: string;
  inLevelMusicId: string;
  ambiences: string[];
  audioDirectoryGuids: string[];
  audioDirectoryIds: string[];
  onDeathEffectGuid: string;
  onDeathEffectId: string;
}

export interface LevelDetail {
  levelInfoAssetPath: string;
  levelName: string;
  levelNameZH: string;
  sceneName: string;
  sceneAssetPath: string;
  hasScreenshot: boolean;
  debugRecipeCount: number;
  disableDynamicParenting: boolean;
  dependencies: string[];
  configs: PerPlayerConfig[];
  audio: AudioConfig;
}
