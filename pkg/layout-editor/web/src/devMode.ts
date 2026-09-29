/** 共享菜谱库开发者模式（localStorage 开关 + 作用域类型）。 */

export type SharedLibraryId = "commonW2" | "commonW3";

export type RecipeAdminScope =
  | { kind: "set"; setName: string }
  | { kind: "library"; libraryId: SharedLibraryId };

const DEV_MODE_KEY = "crDeveloperMode";

export function isDeveloperModeEnabled(): boolean {
  return localStorage.getItem(DEV_MODE_KEY) === "1";
}

export function setDeveloperModeEnabled(on: boolean): void {
  localStorage.setItem(DEV_MODE_KEY, on ? "1" : "0");
}

export const SHARED_LIBRARIES: { id: SharedLibraryId; title: string; desc: string }[] = [
  {
    id: "commonW2",
    title: "commonW2 · Burger大全 / 炸物 / 意面",
    desc: "全关卡集共用的汉堡、炸物与意面扩展菜谱库。修改会影响所有引用该库的关卡集。",
  },
  {
    id: "commonW3",
    title: "commonW3 · 沙拉 / 果汁",
    desc: "Web 扩展沙拉与果汁菜谱库。修改会影响所有引用该库的关卡集。",
  },
];

export function libraryLabel(libraryId: SharedLibraryId): string {
  return SHARED_LIBRARIES.find((x) => x.id === libraryId)?.title ?? libraryId;
}

/** 资产是否属于指定共享库 custom_recipes 目录。 */
export function isInLibraryCustomRecipes(assetPath: string, libraryId: SharedLibraryId): boolean {
  const p = assetPath.replace(/\\/g, "/");
  return p.includes(`/${libraryId}/custom_recipes/`);
}

export function scopeTitle(scope: RecipeAdminScope): string {
  return scope.kind === "set" ? scope.setName : libraryLabel(scope.libraryId);
}
