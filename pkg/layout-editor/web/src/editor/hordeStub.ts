import type { LayoutItem, LayoutVector3 } from "../types";

const ROOT = "Assets/common04/game_environment/horde/prefabs/";

export function nativeHordeStubKind(item: Pick<LayoutItem, "prefabAssetPath" | "prefabGuid">): string {
  // Match the backend's path-first prefab resolution when an old GUID disagrees.
  if (item.prefabAssetPath) {
    if (item.prefabAssetPath === `${ROOT}Service_Window.prefab`) return "ServingStation";
    if (item.prefabAssetPath === `${ROOT}gate.prefab`) return "Lockable";
    return "";
  }
  if (item.prefabGuid === "23525467a89357149ac283bdbeb11d1e") return "ServingStation";
  if (item.prefabGuid === "9082ea11251999449830958b76c907cd") return "Lockable";
  return "";
}

export function normalizeUnlockCost(value: number | undefined): number {
  if (value == null || Number.isNaN(value)) return 100;
  return Math.min(2147483647, Math.max(0, Math.round(value)));
}

/** Shared by placement, import and save; ordinary serving stations stay untouched. */
export function normalizeHordeStub(item: LayoutItem): void {
  const nativeKind = nativeHordeStubKind(item);
  if (nativeKind) {
    item.stubKind = nativeKind;
    item.prefabAssetPath = `${ROOT}${nativeKind === "ServingStation" ? "Service_Window" : "gate"}.prefab`;
    item.prefabGuid = nativeKind === "ServingStation" ? "23525467a89357149ac283bdbeb11d1e" : "9082ea11251999449830958b76c907cd";
  }
  if (nativeKind === "ServingStation") {
    const vector = (v: LayoutVector3 | undefined): LayoutVector3 => ({
      x: Number.isFinite(v?.x) ? v!.x : 0,
      y: Number.isFinite(v?.y) ? v!.y : 0,
      z: Number.isFinite(v?.z) ? v!.z : 0,
    });
    item.servingStation = {
      ...item.servingStation,
      orderUIOffset: vector(item.servingStation?.orderUIOffset),
      healthUIOffset: vector(item.servingStation?.healthUIOffset),
    };
  }
  if (item.stubKind === "Lockable") {
    item.lockable = { ...item.lockable, unlockCost: normalizeUnlockCost(item.lockable?.unlockCost) };
  }
}
