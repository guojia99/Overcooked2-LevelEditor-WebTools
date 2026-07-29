export interface CatalogStackMeta {
  /** Vertical local Y when parented under Design/Utensils (plate ≈ 1, pot ≈ 0.6). */
  y: number;
  hostRule: "counter_standard" | "cooker" | "frying_station" | "mixer";
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
    | "ice"
    | "snow"
    | "sand"
    | "alien"
    | "walkway"
    | "carpet"
    | "section"
    | "conveyor"
    | "background";
  stack?: CatalogStackMeta;
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

export interface LayoutItem {
  instanceId: string;
  hierarchyPath: string;
  prefabGuid: string;
  prefabAssetPath: string;
  parentPath: string;
  displayName: string;
  localPosition: LayoutVector3;
  worldPosition?: LayoutVector3;
  localRotationY: number;
  footprint?: { cellsX: number; cellsZ: number };
  stubKind?: string;
  dispenser?: LayoutDispenserStub;
  foodSpawner?: LayoutFoodSpawnerStub;
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

export interface IngredientEntry {
  guid: string;
  id: string;
  nameZh: string;
  nameEn?: string;
  assetPath: string;
}

export interface RecipeEntry {
  guid: string;
  id: string;
  nameZh: string;
  assetPath: string;
}

export interface LevelRecipes {
  levelInfoAssetPath: string;
  levelName: string;
  recipeGuids: string[];
}
