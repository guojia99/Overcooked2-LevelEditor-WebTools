import type {
  AmbienceCatalog,
  AudioCatalog,
  AudioDirectoryCatalog,
  AudioExportManifest,
  AudioKnowledge,
  BundleAnalysis,
  CookingStepCatalog,
  CookingStepEntry,
  CounterAppearanceCatalog,
  CustomRecipeConfig,
  CustomRecipeEdit,
  CustomRecipeReferences,
  CustomRecipeSummary,
  DeathEffectCatalog,
  FloorMaterial,
  FloorMaterialCatalog,
  GridInfo,
  IconStatusList,
  IngredientEntry,
  LayoutDocument,
  LevelDetail,
  LevelList,
  LevelRecipes,
  LevelSetInfo,
  LevelSetList,
  LevelSetScene,
  MusicCatalog,
  PerPlayerConfig,
  RecipeEntry,
  SwitchMaterialCatalog,
  SwitchMaterialOption,
} from "./types";

const STALE_BRIDGE_MSG =
  "API 返回了网页而非 JSON。请在 Unity 中确认 Editor 脚本已编译无报错，停止并重新启动 Layout Editor Bridge。";

async function readApiJson<T>(r: Response): Promise<T> {
  const ct = r.headers.get("content-type") ?? "";
  const text = await r.text();
  if (text.trimStart().startsWith("<") || ct.includes("text/html")) {
    throw new Error(STALE_BRIDGE_MSG);
  }
  let data: T;
  try {
    data = JSON.parse(text) as T;
  } catch {
    throw new Error(`API 响应不是合法 JSON：${text.slice(0, 120)}`);
  }
  if (!r.ok) {
    const err = data as { error?: string };
    throw new Error(err.error ?? `请求失败 (${r.status})`);
  }
  return data;
}

export type HealthInfo = {
  ok: boolean;
  recipeApi?: boolean;
  schemaVersion?: number;
  knowledgeLoaded?: boolean;
  dictionaryLoaded?: boolean;
};

export async function fetchHealth(): Promise<boolean> {
  try {
    const info = await fetchHealthInfo();
    return info.ok;
  } catch {
    return false;
  }
}

export async function fetchHealthInfo(): Promise<HealthInfo> {
  const r = await fetch("/api/health");
  return readApiJson<HealthInfo>(r);
}

export async function fetchLevelSets(): Promise<LevelSetScene[]> {
  const r = await fetch("/api/level-sets");
  const data = await readApiJson<{ scenes?: LevelSetScene[] }>(r);
  return data.scenes ?? [];
}

export async function fetchLayout(assetPath: string): Promise<LayoutDocument> {
  const q = new URLSearchParams({ assetPath });
  const r = await fetch(`/api/scene/layout?${q}`);
  if (!r.ok) throw new Error("无法加载场景布局");
  return r.json();
}

/** only scopes the write-back to one layer: "" = full, "items" / "decor" / "floors". */
export async function saveLayout(doc: LayoutDocument, snap: number, syncWalkable = false, only = ""): Promise<void> {
  const q = new URLSearchParams({ snap: String(snap) });
  if (syncWalkable) q.set("syncWalkable", "1");
  if (only) q.set("only", only);
  const r = await fetch(`/api/scene/layout?${q}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(doc),
  });
  if (!r.ok) {
    const err = await r.json().catch(() => ({}));
    throw new Error(err.error ?? "写回失败");
  }
}

export async function fetchGrid(): Promise<GridInfo> {
  const r = await fetch("/api/grid");
  if (!r.ok) throw new Error("无法读取网格");
  return r.json();
}

export async function loadCatalog(): Promise<import("./types").Catalog> {
  const r = await fetch("/catalog.json");
  if (!r.ok) throw new Error("无法加载 catalog.json，请先运行 build-catalog");
  return r.json();
}

/** Static catalog JSON fallback (build-catalog.mjs output) when the Unity bridge is offline. */
async function fetchStaticCatalog<T>(fileName: string): Promise<T> {
  const r = await fetch(`/${fileName}`);
  if (!r.ok) throw new Error(`无法加载 ${fileName}，请先运行 build-catalog`);
  return r.json() as Promise<T>;
}

export async function fetchIngredients(): Promise<IngredientEntry[]> {
  try {
    const r = await fetch("/api/catalog/ingredients");
    const data = await readApiJson<{ ingredients?: IngredientEntry[] }>(r);
    return data.ingredients ?? [];
  } catch {
    const data = await fetchStaticCatalog<{ ingredients?: IngredientEntry[] }>("ingredients.json");
    return data.ingredients ?? [];
  }
}

export async function fetchFloorMaterials(levelSet: string): Promise<FloorMaterial[]> {
  try {
    const q = new URLSearchParams({ levelSet });
    const r = await fetch(`/api/catalog/floor-materials?${q}`);
    const data = await readApiJson<FloorMaterialCatalog>(r);
    return data.materials ?? [];
  } catch {
    const data = await fetchStaticCatalog<FloorMaterialCatalog>("floor-materials.json");
    const all = data.materials ?? [];
    if (!levelSet) return all;
    return [...all].sort((a, b) => {
      const ra = a.source === levelSet ? 1 : 0;
      const rb = b.source === levelSet ? 1 : 0;
      return rb - ra;
    });
  }
}

export async function fetchRecipeCatalog(levelSet: string): Promise<RecipeEntry[]> {
  let recipes: RecipeEntry[];
  try {
    const q = new URLSearchParams({ levelSet });
    const r = await fetch(`/api/recipes?${q}`);
    const data = await readApiJson<{ recipes?: RecipeEntry[] }>(r);
    recipes = data.recipes ?? [];
  } catch {
    const data = await fetchStaticCatalog<{ recipes?: RecipeEntry[] }>("recipes.json");
    recipes = data.recipes ?? [];
  }
  for (const r of recipes) {
    if (r.type === "sushi" && r.cookingStep === "Steamer") {
      r.cookingStep = "Pot";
    }
    if (r.type === "pizza" && r.id !== "Pizza_Olives" && r.ingredients) {
      const idx = r.ingredients.indexOf("DLC05_Dough");
      if (idx >= 0) r.ingredients[idx] = "DoughSO";
    }
  }
  return recipes;
}

export async function fetchCookingSteps(): Promise<CookingStepEntry[]> {
  const data = await fetchStaticCatalog<CookingStepCatalog>("cooking-steps.json");
  return data.cookingSteps ?? [];
}

export async function fetchLevelRecipes(assetPath: string): Promise<LevelRecipes> {
  const q = new URLSearchParams({ assetPath });
  const r = await fetch(`/api/level-recipes?${q}`);
  return readApiJson<LevelRecipes>(r);
}

export async function saveLevelRecipes(
  levelInfoAssetPath: string,
  recipeGuids: string[]
): Promise<void> {
  const r = await fetch("/api/level-recipes", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ levelInfoAssetPath, recipeGuids }),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

// ---------- Level admin ----------

export async function fetchSets(): Promise<LevelSetInfo[]> {
  const r = await fetch("/api/sets");
  const data = await readApiJson<LevelSetList>(r);
  return data.sets ?? [];
}

export async function fetchLevels(setName: string): Promise<LevelList["levels"]> {
  const set = encodeURIComponent(setName);
  const r = await fetch(`/api/sets/${set}/levels`);
  const data = await readApiJson<LevelList>(r);
  return data.levels ?? [];
}

export async function fetchLevelDetail(assetPath: string): Promise<LevelDetail> {
  const q = new URLSearchParams({ assetPath });
  const r = await fetch(`/api/level?${q}`);
  return readApiJson<LevelDetail>(r);
}

export async function fetchMusicCatalog(): Promise<MusicCatalog["music"]> {
  try {
    const r = await fetch("/api/catalog/music");
    const data = await readApiJson<MusicCatalog>(r);
    return data.music ?? [];
  } catch {
    const data = await fetchStaticCatalog<AudioCatalog>("audio-catalog.json");
    return data.music ?? [];
  }
}

export async function fetchAudioDirectoryCatalog(): Promise<AudioDirectoryCatalog["audioDirectories"]> {
  try {
    const r = await fetch("/api/catalog/audio-directories");
    const data = await readApiJson<AudioDirectoryCatalog>(r);
    return data.audioDirectories ?? [];
  } catch {
    const data = await fetchStaticCatalog<AudioCatalog>("audio-catalog.json");
    return data.audioDirectories ?? [];
  }
}

export async function fetchAmbiences(): Promise<string[]> {
  const r = await fetch("/api/catalog/ambiences");
  const data = await readApiJson<AmbienceCatalog>(r);
  return data.ambiences ?? [];
}

export async function fetchDeathEffects(): Promise<DeathEffectCatalog["deathEffects"]> {
  try {
    const r = await fetch("/api/catalog/death-effects");
    const data = await readApiJson<DeathEffectCatalog>(r);
    return data.deathEffects ?? [];
  } catch {
    const data = await fetchStaticCatalog<AudioCatalog>("audio-catalog.json");
    return data.deathEffects ?? [];
  }
}

export async function fetchAudioKnowledge(): Promise<AudioKnowledge> {
  const empty: AudioKnowledge = {
    baseBundles: ["bundle47"],
    alwaysLoadedBundles: ["bundle18"],
    mandatoryDirectoryIds: [],
    directoryEvents: [],
    themes: [],
    deathThemes: [],
    ambienceLabels: [],
    itemAudioRules: [],
  };
  try {
    const r = await fetch("/api/catalog/audio-directories");
    const data = await readApiJson<AudioDirectoryCatalog>(r);
    return {
      baseBundles: data.baseBundles ?? empty.baseBundles,
      alwaysLoadedBundles: data.alwaysLoadedBundles ?? empty.alwaysLoadedBundles,
      mandatoryDirectoryIds: data.mandatoryDirectoryIds ?? [],
      directoryEvents: data.directoryEvents ?? [],
      themes: data.themes ?? [],
      deathThemes: data.deathThemes ?? [],
      ambienceLabels: data.ambienceLabels ?? [],
      itemAudioRules: data.itemAudioRules ?? [],
    };
  } catch {
    const data = await fetchStaticCatalog<AudioCatalog>("audio-catalog.json");
    return {
      baseBundles: data.baseBundles ?? empty.baseBundles,
      alwaysLoadedBundles: data.alwaysLoadedBundles ?? empty.alwaysLoadedBundles,
      mandatoryDirectoryIds: data.mandatoryDirectoryIds ?? [],
      directoryEvents: data.directoryEvents ?? [],
      themes: data.themes ?? [],
      deathThemes: data.deathThemes ?? [],
      ambienceLabels: data.ambienceLabels ?? [],
      itemAudioRules: data.itemAudioRules ?? [],
    };
  }
}

export async function fetchBundleAnalysis(assetPath: string): Promise<BundleAnalysis> {
  const q = new URLSearchParams({ assetPath });
  const r = await fetch(`/api/level/bundles?${q}`);
  return readApiJson<BundleAnalysis>(r);
}

export interface BundleManifest {
  dependencies: { name: string; deps: string[] }[];
}

let _bundleGraph: Map<string, string[]> | null = null;

/** Loads the static AssetBundle dependency graph (bundle-manifest.json). Empty map on failure. */
export async function fetchBundleGraph(): Promise<Map<string, string[]>> {
  if (_bundleGraph) return _bundleGraph;
  try {
    const r = await fetch("/bundle-manifest.json");
    const data = await readApiJson<BundleManifest>(r);
    _bundleGraph = new Map(
      (data.dependencies ?? []).map((e) => [e.name, e.deps ?? []])
    );
  } catch {
    _bundleGraph = new Map();
  }
  return _bundleGraph;
}

/** Transitive closure of bundle seeds following the manifest dependency graph. */
export function bundleClosure(graph: Map<string, string[]>, seeds: Iterable<string>): Set<string> {
  const seen = new Set<string>();
  const stack: string[] = [];
  for (const s of seeds) if (s) stack.push(s);
  while (stack.length) {
    const b = stack.pop()!;
    if (!seen.add(b)) continue;
    const ds = graph.get(b);
    if (ds) for (const d of ds) if (!seen.has(d)) stack.push(d);
  }
  return seen;
}

// ---------- Audio export manifest ----------

let _audioExports: AudioExportManifest | null = null;

export async function fetchAudioExports(): Promise<AudioExportManifest | null> {
  if (_audioExports) return _audioExports;
  try {
    const r = await fetch("/api/audio/exports");
    if (r.ok) {
      _audioExports = await readApiJson<AudioExportManifest>(r);
      return _audioExports;
    }
  } catch { /* ignore */ }
  try {
    const r = await fetch("/audio-exports.json");
    if (r.ok) {
      _audioExports = await r.json();
      return _audioExports;
    }
  } catch { /* ignore */ }
  return null;
}

export function getAudioStreamUrl(relPath: string): string {
  return `/api/audio/stream?path=${encodeURIComponent(relPath)}`;
}

// ---------- Icon status ----------

export async function fetchIconsStatus(): Promise<IconStatusList> {
  const r = await fetch("/api/icons/status");
  return readApiJson<IconStatusList>(r);
}

export interface SetCreateBody {
  setName: string;
  levelSetName: string;
  levelSetNameZH: string;
  author: string;
}

export interface SetInfoUpdateBody {
  setName: string;
  levelSetName: string;
  levelSetNameZH: string;
  author: string;
  version: string;
}

export async function createSet(body: SetCreateBody): Promise<void> {
  const r = await fetch("/api/set/create", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

/** Permanently delete a level set folder and all its contents. */
export async function deleteSet(setName: string): Promise<void> {
  const r = await fetch("/api/set/delete", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ setName }),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

export async function updateSetInfo(body: SetInfoUpdateBody): Promise<void> {
  const r = await fetch("/api/set/info", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

export interface LevelCreateBody {
  setName: string;
  levelId: string;
  levelName: string;
  levelNameZH: string;
}

export async function createLevel(body: LevelCreateBody): Promise<void> {
  const r = await fetch("/api/level/create", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

export interface LevelInfoUpdateBody {
  assetPath: string;
  levelName: string;
  levelNameZH: string;
  sceneName: string;
  debugRecipeCount: number;
  disableDynamicParenting: boolean;
  dependencies: string[];
}

export async function updateLevelInfo(body: LevelInfoUpdateBody): Promise<void> {
  const r = await fetch("/api/level/info", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

export interface LevelConfigUpdateBody {
  assetPath: string;
  config_1p: PerPlayerConfig;
  config_2p: PerPlayerConfig;
  config_3p: PerPlayerConfig;
  config_4p: PerPlayerConfig;
}

export async function updateLevelConfig(body: LevelConfigUpdateBody): Promise<void> {
  const r = await fetch("/api/level/config", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

export interface LevelAudioUpdateBody {
  sceneAssetPath: string;
  inLevelMusicGuid: string;
  ambiences: string[];
  audioDirectoryGuids: string[];
  onDeathEffectGuid: string;
}

export async function updateLevelAudio(body: LevelAudioUpdateBody): Promise<void> {
  const r = await fetch("/api/level/audio", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

export async function deleteLevel(setName: string, levelId: string): Promise<void> {
  const r = await fetch("/api/level/delete", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ setName, levelId }),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

export async function fetchDeletePreview(setName: string, levelId: string): Promise<string[]> {
  const q = new URLSearchParams({ setName, levelId });
  const r = await fetch(`/api/level/delete-preview?${q}`);
  const data = await readApiJson<{ paths?: string[] }>(r);
  return data.paths ?? [];
}

export async function reloadPseudo(): Promise<void> {
  const r = await fetch("/api/reload", { method: "POST" });
  await readApiJson<{ ok?: boolean }>(r);
}

export async function setDeathTheme(sceneAssetPath: string, theme: string): Promise<void> {
  const r = await fetch("/api/scene/death", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ sceneAssetPath, theme }),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

export async function setKillPlaneBounds(
  sceneAssetPath: string,
  cx: number,
  cz: number,
  sx: number,
  sz: number
): Promise<void> {
  const r = await fetch("/api/scene/killplane", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ sceneAssetPath, cx, cz, sx, sz }),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

/** Upload an image-floor texture (base64) into the level set's data dir.
 *  Returns the asset path of the imported texture, e.g.
 *  "Assets/LevelSets/<set>/data/<fileName>". */
export async function uploadImageFloor(
  setName: string,
  fileName: string,
  base64: string
): Promise<string> {
  const r = await fetch("/api/level/image-upload", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ setName, fileName, base64 }),
  });
  const data = await readApiJson<{ texturePath?: string; error?: string }>(r);
  return data.texturePath ?? "";
}

/** URL that serves the raw bytes of a data-dir asset (for canvas preview). */
export function imageFloorUrl(texturePath: string): string {
  return `/api/level/data-file?path=${encodeURIComponent(texturePath)}`;
}

/** Upload a level screenshot image (base64) into the level's data dir.
 *  Assigns the imported Sprite to LevelInfoSO.screenshot. */
export async function uploadScreenshot(
  assetPath: string,
  fileName: string,
  base64: string
): Promise<string> {
  const r = await fetch("/api/level/screenshot-upload", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ assetPath, fileName, base64 }),
  });
  const data = await readApiJson<{ texturePath?: string; error?: string }>(r);
  return data.texturePath ?? "";
}

// ---------- Custom Recipe Management ----------

export async function fetchCustomRecipeConfig(setName: string): Promise<CustomRecipeConfig> {
  const q = new URLSearchParams({ setName });
  const r = await fetch(`/api/custom-recipes/config?${q}`);
  return readApiJson<CustomRecipeConfig>(r);
}

export async function fetchCustomRecipes(setName: string): Promise<CustomRecipeSummary[]> {
  const q = new URLSearchParams({ setName });
  const r = await fetch(`/api/custom-recipes?${q}`);
  const data = await readApiJson<{ recipes?: CustomRecipeSummary[] }>(r);
  return data.recipes ?? [];
}

export async function fetchCustomRecipeReferences(setName: string): Promise<CustomRecipeReferences> {
  const q = new URLSearchParams({ setName });
  const r = await fetch(`/api/custom-recipes/references?${q}`);
  return readApiJson<CustomRecipeReferences>(r);
}

export async function createCustomRecipe(body: CustomRecipeEdit): Promise<void> {
  const r = await fetch("/api/custom-recipes/create", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

export async function updateCustomRecipe(body: CustomRecipeEdit): Promise<void> {
  const r = await fetch("/api/custom-recipes/update", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

export async function deleteCustomRecipe(assetPath: string): Promise<void> {
  const r = await fetch("/api/custom-recipes/delete", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ assetPath }),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

export async function uploadCustomRecipeIcon(
  setName: string,
  recipeAssetPath: string,
  fileName: string,
  base64: string
): Promise<string> {
  const r = await fetch("/api/custom-recipes/upload-icon", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ setName, recipeAssetPath, fileName, base64 }),
  });
  const data = await readApiJson<{ texturePath?: string; error?: string }>(r);
  return data.texturePath ?? "";
}

export async function uploadCustomRecipeModel(
  setName: string,
  recipeAssetPath: string,
  fileName: string,
  base64: string
): Promise<CustomRecipeUploadResult> {
  return uploadCustomRecipeModelFiles(setName, recipeAssetPath, [{ fileName, base64 }]);
}

export interface CustomRecipeUploadFile {
  fileName: string;
  base64: string;
}

export interface CustomRecipeUploadResult {
  ok?: boolean;
  error?: string;
  /** Unity 导入后的模型原始尺寸（不含配置变换），用于按 Unity 实际尺寸校准缩放/位置。 */
  rawSizeX?: number;
  rawSizeY?: number;
  rawSizeZ?: number;
  rawMinY?: number;
}

/** 上传模型 + 贴图组合（第一个 .fbx/.obj 为主模型，其余 PNG/JPG 为贴图）。
 *  返回 Unity 导入后的原始尺寸，前端据此自动校准缩放/位置。 */
export async function uploadCustomRecipeModelFiles(
  setName: string,
  recipeAssetPath: string,
  files: CustomRecipeUploadFile[]
): Promise<CustomRecipeUploadResult> {
  const r = await fetch("/api/custom-recipes/upload-model", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ setName, recipeAssetPath, files }),
  });
  return readApiJson<CustomRecipeUploadResult>(r);
}

/** 列出菜谱 models 目录内的模型/贴图文件（供 3D 在线预览）。 */
export async function fetchCustomRecipeModelFiles(assetPath: string): Promise<string[]> {
  const r = await fetch(`/api/custom-recipes/model-files?assetPath=${encodeURIComponent(assetPath)}`);
  const data = await readApiJson<{ files?: string[] }>(r);
  return data.files ?? [];
}

export interface CustomRecipeScanDiag {
  setName: string;
  recipesDir: string;
  dirExists: boolean;
  scannedCount: number;
  loadedCount: number;
  fsAssets: string[];
}

export interface CustomRecipeDiagnose {
  assetPath: string;
  error?: string;
  modelDirect: boolean;
  modelSOBased: boolean;
  modelPath?: string;
  modelType?: string;
  modelStructure?: string;
  rendererCount: number;
  meshCount: number;
  materialCount: number;
  boundsMinX: number;
  boundsMinY: number;
  boundsMinZ: number;
  boundsSizeX: number;
  boundsSizeY: number;
  boundsSizeZ: number;
  compositionCount: number;
  cookingStepSet: boolean;
  platingStepSet: boolean;
  platingPrefabSet: boolean;
  modelScale: number;
  modelRotationX: number;
  modelRotationY: number;
  modelRotationZ: number;
  modelPositionX: number;
  modelPositionY: number;
  modelPositionZ: number;
}

/** 菜谱模型/装盘链路诊断（只读），排查"模型在游戏中不显示"。 */
export async function diagnoseCustomRecipe(assetPath: string): Promise<CustomRecipeDiagnose> {
  const r = await fetch(`/api/custom-recipes/diagnose?assetPath=${encodeURIComponent(assetPath)}`);
  return readApiJson<CustomRecipeDiagnose>(r);
}

/** 诊断：文件系统上实际存在哪些菜谱资产（桥为新代码时可用）。 */
export async function fetchCustomRecipeScanDiag(setName: string): Promise<CustomRecipeScanDiag> {
  const r = await fetch(`/api/custom-recipes/debug-scan?setName=${encodeURIComponent(setName)}`);
  const data = await readApiJson<CustomRecipeScanDiag>(r);
  return data;
}

export async function addCustomRecipeCategory(setName: string, id: string, zh: string, en: string): Promise<void> {
  const r = await fetch("/api/custom-recipes/category/add", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ setName, id, zh, en }),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

export async function renameCustomRecipeCategory(setName: string, oldId: string, newId: string, newZh: string, newEn: string): Promise<void> {
  const r = await fetch("/api/custom-recipes/category/rename", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ setName, oldId, newId, newZh, newEn }),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

export async function deleteCustomRecipeCategory(setName: string, category: string): Promise<void> {
  const r = await fetch("/api/custom-recipes/category/delete", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ setName, category }),
  });
  await readApiJson<{ ok?: boolean }>(r);
}

export async function fetchCounterAppearances(): Promise<CounterAppearanceCatalog> {
  const data = await fetchStaticCatalog<CounterAppearanceCatalog>("counter-appearances.json");
  return data;
}

export async function fetchSwitchMaterials(): Promise<SwitchMaterialOption[]> {
  const data = await fetchStaticCatalog<SwitchMaterialCatalog>("switch-materials.json");
  return data.materials ?? [];
}

export { STALE_BRIDGE_MSG };
