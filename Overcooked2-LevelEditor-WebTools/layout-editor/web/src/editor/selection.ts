import { S } from "./state";

export function isSelected(key: string): boolean {
  return S.selectedKeys.has(key);
}

export function selectionKeys(): string[] {
  return Array.from(S.selectedKeys);
}

export function setSelection(keys: string[], primary?: string): void {
  S.selectedKeys = new Set(keys);
  S.selectedKey = keys.length ? primary ?? keys[keys.length - 1] : null;
}

export function clearSelection(): void {
  S.selectedKeys = new Set<string>();
  S.selectedKey = null;
}

export function setFloorSelection(keys: string[], primary?: string): void {
  S.selectedFloorKeys = new Set(keys);
  S.selectedFloorKey = keys.length ? primary ?? keys[keys.length - 1] : null;
}

export function clearFloorSelection(): void {
  S.selectedFloorKeys = new Set<string>();
  S.selectedFloorKey = null;
}
