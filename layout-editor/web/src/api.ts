import type {
  FloorMaterial,
  FloorMaterialCatalog,
  GridInfo,
  IngredientEntry,
  LayoutDocument,
  LevelRecipes,
  LevelSetScene,
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

export async function saveLayout(doc: LayoutDocument, snap: number): Promise<void> {
  const q = new URLSearchParams({ snap: String(snap) });
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

export { STALE_BRIDGE_MSG };
