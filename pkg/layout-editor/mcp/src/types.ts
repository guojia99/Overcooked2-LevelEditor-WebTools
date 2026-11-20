export type JsonObject = Record<string, unknown>;

export type LayoutVector3 = { x: number; y: number; z: number };

export type GameMode = "Campaign" | "Dynamic" | "Horde";
export type PerPlayerConfig = JsonObject & {
  exists: boolean;
  orderLifeTime: number; timeBetweenOrders: number; plateReturnTime: number;
  survivalTimeMultiplier: number; roundTime: number;
  oneStarScore: number; twoStarScore: number; threeStarScore: number; fourStarScore: number;
  targetRepairCostMax: number;
  /** GET preserves positions (including "" for broken references), order and duplicates. */
  scriptedRecipes: string[];
  scriptedRecipeErrors: string[];
};
export type LevelDetail = JsonObject & {
  levelInfoAssetPath: string;
  gameMode: GameMode;
  enableRespawnBounds: boolean;
  configs: PerPlayerConfig[];
};
export type LevelModeChangeDto = {
  assetPath: string;
  gameMode: GameMode;
  /** Explicit user opt-in; omitted means false. Does not convert coin gates. */
  convertHordeObjects?: boolean;
};
export type LevelModeConfig = {
  assetPath: string;
  gameMode: GameMode;
  phases?: { id: string; durationSeconds: number; recipeGuids: string[] }[] | null;
  hordeConfigs?: {
    targetRepairCostMax: number;
    waves: { id: string; intervalSeconds: number; recipeGuids: string[]; enemyGuids: string[]; spawnTimes: number[] }[];
  }[] | null;
  targetHealth: number;
  targetRepairSpeed: number;
  targetRepairThreshold: number;
  kitchenHealth: number;
  minOrderCount: number;
  maxOrderCount: number;
  enemies?: { guid: string; id: string; nameZh: string; nameEn: string | null; assetPath: string }[] | null;
};

export type LayoutItem = {
  instanceId: string;
  hierarchyPath: string;
  prefabGuid: string;
  prefabAssetPath: string;
  parentPath: string;
  displayName: string;
  localPosition: LayoutVector3;
  worldPosition?: LayoutVector3;
  localRotationX?: number;
  localRotationY: number;
  localRotationZ?: number;
  localScale?: LayoutVector3;
  footprint?: { cellsX: number; cellsZ: number };
  walkable?: boolean;
  stubKind?: string;
  lockable?: { unlockCost: number };
  servingStation?: { orderUIOffset?: LayoutVector3; healthUIOffset?: LayoutVector3; [key: string]: unknown };
  [key: string]: unknown;
};

export type LayoutDocument = {
  sceneAssetPath: string;
  items: LayoutItem[];
  floors?: JsonObject[];
  [key: string]: unknown;
};

export type LevelSet = {
  setName: string;
  assetPath: string;
  dataDir: string;
  levelSetName: string;
  levelSetNameZH: string;
  author: string;
  version: string;
  uid: string;
  levelCount: number;
};

export type LevelSummary = {
  assetPath: string;
  dataDir: string;
  levelName: string;
  levelNameZH: string;
  sceneName: string;
  sceneAssetPath: string;
  hasScreenshot: boolean;
  hasScene: boolean;
  /** 分组名（data/<level>/group~/group.json；"" / 缺省 = 未分组）。 */
  group?: string;
  [key: string]: unknown;
};

export type Draft = {
  id: string;
  setName: string;
  levelId: string;
  sceneAssetPath: string;
  document: LayoutDocument;
  baseDocument: LayoutDocument;
  createdAt: string;
  updatedAt: string;
};

export type ToolResult = {
  ok: boolean;
  data?: unknown;
  warnings?: string[];
  error?: { code: string; message: string; retryable: boolean };
  operation: {
    name: string;
    kind: "read" | "compute" | "draft" | "write_scene" | "write_asset" | "build";
    sideEffect: "read" | "draft" | "write_scene" | "write_asset" | "build";
  };
  resource?: { setName?: string; levelId?: string; assetPath?: string; sceneAssetPath?: string };
  bridge: { online: boolean; url: string; schemaVersion?: number; stale?: boolean };
  timing: { estimatedMs: number; pollAfterMs: number | null; deadlineMs: number };
};
