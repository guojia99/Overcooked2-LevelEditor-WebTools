import type { EditorItem } from "./state";
import { S } from "./state";
import { itemLayerOfIt } from "./catalog";
import { itemLabel } from "./labels";
import { isSelected } from "./selection";

/** Display-name key for decor layer grouping / batch hide (matches right panel). */
export function decorGroupKey(it: EditorItem): string {
  return itemLabel(it);
}

export function isDecorGroupHidden(it: EditorItem): boolean {
  if (itemLayerOfIt(it) !== "decor") return false;
  return S.decorHiddenGroups.has(decorGroupKey(it));
}

/** Decor group hidden and not exempt via selection (PS-style). */
export function isItemHiddenByDecorGroup(it: EditorItem): boolean {
  if (!isDecorGroupHidden(it)) return false;
  return !isSelected(it._editorKey);
}
