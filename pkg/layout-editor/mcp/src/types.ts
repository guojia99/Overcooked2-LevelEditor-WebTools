export type JsonObject = Record<string, unknown>;

export type LayoutVector3 = { x: number; y: number; z: number };

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
    kind: "read" | "compute" | "draft" | "write_scene" | "build";
    sideEffect: "read" | "draft" | "write_scene" | "build";
  };
  resource?: { setName?: string; levelId?: string; sceneAssetPath?: string };
  bridge: { online: boolean; url: string; schemaVersion?: number; stale?: boolean };
  timing: { estimatedMs: number; pollAfterMs: number | null; deadlineMs: number };
};
