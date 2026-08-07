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

export interface LayoutPlateReturnStub {
  /** True = return clean plates/glasses directly; false (default) = dirty ones. */
  returnClean?: boolean;
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

export interface LayoutSwitchStub {
  startEnabled?: boolean;
  activeMaterialGuid?: string;
  inactiveMaterialGuid?: string;
}

export interface LayoutPressureSwitchStub {
  occupiedMaterialGuid?: string;
  unoccupiedMaterialGuid?: string;
}

export interface LayoutMeshWithMaterialStub {
  pseudoPrefabGuid?: string;
  materialGuid?: string;
}

export interface LayoutSOArrayStub {
  pseudoPrefabGuids?: string[];
}

export interface LayoutTerminalStub {
  pilotableObjectInstanceId?: string;
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
  plateReturn?: LayoutPlateReturnStub;
  switchStub?: LayoutSwitchStub;
  pressureSwitch?: LayoutPressureSwitchStub;
  terminal?: LayoutTerminalStub;
  /** Counter/Dispenser etc. appearance SO guid (base PseudoPrefabStub.pseudoPrefabSO). */
  pseudoPrefabGuid?: string;
  meshWithMaterial?: LayoutMeshWithMaterialStub;
  soArray?: LayoutSOArrayStub;
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
  /** Image rotation in degrees, snapped to 90° steps (0/90/180/270, clockwise
   *  viewed from above). Default 0. */
  imageRotation?: number;
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
  /** Direct composition ids for custom recipes (sub-recipe ids and/or ingredient ids). */
  compositionIds?: string[];
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

export interface AudioItemRule {
  items: string[];
  theme?: string;
  directories?: string[];
  ambiences?: string[];
  labelZh: string;
}

export interface AudioKnowledge {
  baseBundles: string[];
  alwaysLoadedBundles: string[];
  mandatoryDirectoryIds: string[];
  directoryEvents: DirectoryEvent[];
  themes: AudioTheme[];
  deathThemes: DeathTheme[];
  ambienceLabels: AmbienceLabel[];
  itemAudioRules: AudioItemRule[];
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

// ---------- Audio export manifest ----------

export interface AudioExportManifest {
  generatedAt: string;
  bgm: AudioExportBgm[];
  sfx: AudioExportSfxDir[];
  ambiences: AudioExportAmbience[];
}

export interface AudioExportBgm {
  guid: string;
  id: string;
  nameZh: string;
  filename: string;
}

export interface AudioExportSfxDir {
  id: string;
  nameZh: string;
  clips: AudioExportSfxClip[];
}

export interface AudioExportSfxClip {
  tag: string;
  type: "oneshot" | "looping" | "looping_start" | "looping_end";
  filename: string;
}

export interface AudioExportAmbience {
  tag: string;
  found: boolean;
  filename?: string;
  dirId?: string;
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
  /** Asset path of the screenshot texture ("" when none), serve via api.imageFloorUrl. */
  screenshotPath?: string;
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
  /** Asset path of the screenshot texture ("" when none), serve via api.imageFloorUrl. */
  screenshotPath?: string;
  debugRecipeCount: number;
  disableDynamicParenting: boolean;
  dependencies: string[];
  configs: PerPlayerConfig[];
  audio: AudioConfig;
}

// ---------- Custom Recipe Management ----------

export interface CustomRecipeCategory {
  id: string;
  zh: string;
  en: string;
}

export interface CustomRecipeConfig {
  uidPrefix: number;
  nextSequence: number;
  categories: CustomRecipeCategory[];
}

export interface CustomRecipeSummary {
  guid: string;
  id: string;
  assetPath: string;
  recipeName: string;
  nameZh: string;
  nameEn: string;
  uID: number;
  score: number;
  category: string;
  type: string;
  /** Direct composition ids (ingredient ids and/or sub-recipe ids). */
  compositionIds: string[];
  /** Leaf ingredients, recursively expanded through sub-recipes. */
  ingredients: string[];
  /** Composition-aware cooking groups for the recipe-card UI. */
  cookingGroups?: RecipeCookingGroup[];
  /** True when score <= 0 (半成品), used for badges and the sub-recipe picker. */
  intermediate: boolean;
  /** "core" / "custom" / "dlc02" / "dlc05" / "levelset". */
  group?: string;
  cookingStepId: string;
  platingStepId: string;
  hasIcon: boolean;
  hasModel: boolean;
  /** 模型在游戏中的缩放与朝向（应用到 prefab 根节点，运行时直接生效）。 */
  modelScale: number;
  modelRotationY: number;
}

/** One ingredient group of a recipe card: ingredients cooked together in the
 *  same step (step = "" means a raw group with no utensil). */
export interface RecipeCookingGroup {
  step: string;
  utensils: string[];
  ingredients: string[];
}

export interface CustomRecipeEdit {
  setName?: string;
  assetPath?: string;
  recipeName: string;
  nameZh: string;
  nameEn: string;
  category: string;
  score: number;
  type: string;
  compositionIds: string[];
  cookingStepId: string;
  cookingStepIconId: string;
  platingStepId: string;
  mixingIconId: string;
  modelPrefabId: string;
  cookingProgress: number;
  mixingProgress: number;
  modelScale: number;
  modelRotationY: number;
}

export interface CustomRecipeReferenceEntry {
  guid: string;
  id: string;
  /** Source recipe id owning this model ("" for standalone entries). */
  recipeId?: string;
  nameZh: string;
  nameEn: string;
  assetPath: string;
}

export interface CustomRecipeReferences {
  cookingSteps: CustomRecipeReferenceEntry[];
  platingSteps: CustomRecipeReferenceEntry[];
  /** 装盘容器（盘子/杯子等），运行时映射为 PlatingStepData。 */
  platingContainers: CustomRecipeReferenceEntry[];
  icons: CustomRecipeReferenceEntry[];
  reusableModels: CustomRecipeReferenceEntry[];
  ingredients: string[];
}

// ---------- Counter Appearance ----------

export interface CounterAppearanceOption {
  guid: string;
  id: string;
  assetPath: string;
  nameZh: string;
  nameEn: string;
  theme: string;
  themeName: string;
}

export interface CounterAppearanceCatalog {
  generatedAt: string;
  schemaVersion?: number;
  typeNames: Record<string, string>;
  themeNames: Record<string, string>;
  byType: Record<string, CounterAppearanceOption[]>;
}

export interface SwitchMaterialOption {
  guid: string;
  id: string;
  assetPath: string;
  nameZh: string;
  nameEn: string;
}

export interface SwitchMaterialCatalog {
  generatedAt: string;
  schemaVersion?: number;
  materials: SwitchMaterialOption[];
}

// ---------- Icon status ----------

export interface IconStatusItem {
  id: string;
  nameZh: string;
  nameEn: string;
  hasIcon: boolean;
  group: string;
  type: string;
}

export interface IconStatusList {
  missingRecipes: IconStatusItem[];
  missingIngredients: IconStatusItem[];
  totalRecipes: number;
  totalIngredients: number;
  recipesWithIcon: number;
  ingredientsWithIcon: number;
}
