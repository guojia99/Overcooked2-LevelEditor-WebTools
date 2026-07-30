import type {
  AmbienceCatalog,
  AudioDirectoryCatalog,
  DeathEffectCatalog,
  FloorMaterial,
  FloorMaterialCatalog,
  GridInfo,
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

export type HealthInfo = { ok: boolean; recipeApi?: boolean };

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

export async function saveLayout(doc: LayoutDocument, snap: number, syncWalkable = false): Promise<void> {
  const q = new URLSearchParams({ snap: String(snap) });
  if (syncWalkable) q.set("syncWalkable", "1");
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

export async function fetchIngredients(): Promise<IngredientEntry[]> {
  const r = await fetch("/api/catalog/ingredients");
  const data = await readApiJson<{ ingredients?: IngredientEntry[] }>(r);
  return data.ingredients ?? [];
}

export async function fetchFloorMaterials(levelSet: string): Promise<FloorMaterial[]> {
  const q = new URLSearchParams({ levelSet });
  const r = await fetch(`/api/catalog/floor-materials?${q}`);
  const data = await readApiJson<FloorMaterialCatalog>(r);
  return data.materials ?? [];
}

export async function fetchRecipeCatalog(levelSet: string): Promise<RecipeEntry[]> {
  const q = new URLSearchParams({ levelSet });
  const r = await fetch(`/api/recipes?${q}`);
  const data = await readApiJson<{ recipes?: RecipeEntry[] }>(r);
  return data.recipes ?? [];
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
  const r = await fetch("/api/catalog/music");
  const data = await readApiJson<MusicCatalog>(r);
  return data.music ?? [];
}

export async function fetchAudioDirectoryCatalog(): Promise<AudioDirectoryCatalog["audioDirectories"]> {
  const r = await fetch("/api/catalog/audio-directories");
  const data = await readApiJson<AudioDirectoryCatalog>(r);
  return data.audioDirectories ?? [];
}

export async function fetchAmbiences(): Promise<string[]> {
  const r = await fetch("/api/catalog/ambiences");
  const data = await readApiJson<AmbienceCatalog>(r);
  return data.ambiences ?? [];
}

export async function fetchDeathEffects(): Promise<DeathEffectCatalog["deathEffects"]> {
  const r = await fetch("/api/catalog/death-effects");
  const data = await readApiJson<DeathEffectCatalog>(r);
  return data.deathEffects ?? [];
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

export { STALE_BRIDGE_MSG };
