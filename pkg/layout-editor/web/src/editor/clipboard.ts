import {
  S,
  EditorItem,
  EditorFloor
} from "./state";
import { pasteGridDelta, pastePointerWorld } from "./coords";
import { isPlayerItem } from "./renderItems";
import { deleteSelected } from "./items";
import {
  selectionKeys,
  setSelection,
  clearSelection,
  setFloorSelection,
  clearFloorSelection
} from "./selection";
import {
  hideDetail,
  hideContextMenu
} from "./ui/overlay";
import { draw } from "./render";
import { pushHistory } from "./historyOps";
import { setStatus } from "./status";
import { updateFloorBar } from "./floorPalette";
import { closeModal } from "../modals";
import { isSurfaceItem } from "../floorColors";
import { publishCrossTabClipboard } from "./crossTabClipboard";
import {
  selectionCentroid,
  pasteItemsWithOffset,
  pasteFloorsWithOffset,
} from "./clipboardPaste";

export { selectionCentroid, pasteItemsWithOffset, pasteFloorsWithOffset } from "./clipboardPaste";

function pasteOffsetFromPointer(
  anchor: { x: number; z: number },
  canvasMx?: number,
  canvasMy?: number
): { dx: number; dz: number } {
  const ptr = pastePointerWorld(canvasMx, canvasMy);
  return pasteGridDelta(anchor.x, anchor.z, ptr.x, ptr.z);
}

function crossTabSuffix(): string {
  return publishCrossTabClipboard() ? "；已同步跨页剪贴板" : "";
}

export function copySelection() {
  const keys = selectionKeys();
  if (!keys.length) {
    setStatus("没有选中物品可复制");
    return;
  }
  S.clipboard = keys
    .map((k) => S.items.find((i) => i._editorKey === k))
    .filter((i): i is EditorItem => !!i && !isPlayerItem(i))
    .map((i) => JSON.parse(JSON.stringify(i)) as EditorItem);
  S.pasteRound = 0;
  if (!S.clipboard.length) {
    setStatus("玩家不可复制", false);
    return;
  }
  setStatus(`已复制 ${S.clipboard.length} 个物品（Ctrl/Cmd+V 粘贴）${crossTabSuffix()}`);
}

export function cutSelection() {
  const keys = selectionKeys();
  if (!keys.length) {
    setStatus("没有选中物品可裁切", false);
    return;
  }
  copySelection();
  deleteSelected();
  setStatus(`已裁切 ${S.clipboard.length} 个物品（Ctrl/Cmd+V 粘贴，Ctrl/Cmd+Z 撤回）`);
}

export function pasteClipboard(canvasMx?: number, canvasMy?: number) {
  if (!S.clipboard.length) {
    setStatus("剪贴板为空（先 Ctrl/Cmd+C 复制）", false);
    return;
  }
  pushHistory();
  const anchor = selectionCentroid(S.clipboard);
  const { dx: offX, dz: offZ } = pasteOffsetFromPointer(anchor, canvasMx, canvasMy);
  const { pastedKeys, skipped } = pasteItemsWithOffset(S.clipboard, offX, offZ);
  setSelection(pastedKeys);
  hideDetail();
  hideContextMenu();
  draw();
  setStatus(`已粘贴 ${pastedKeys.length} 个物品${skipped ? `（${skipped} 个因与玩家重叠被跳过）` : ""}`);
}

export function copyFloors(): void {
  const keys = [...S.selectedFloorKeys];
  // 地板层同时选中的地板层物品（压力开关等 surface 物品）随地板一起复制。
  const itemKeys = selectionKeys().filter((k) => {
    const it = S.items.find((i) => i._editorKey === k);
    return it && isSurfaceItem(S.catalogByGuid.get(it.prefabGuid));
  });
  if (!keys.length && !itemKeys.length) {
    setStatus("没有选中地板可复制");
    return;
  }
  S.floorClipboard = keys
    .map((k) => S.floors.find((f) => f._key === k))
    .filter((f): f is EditorFloor => !!f)
    .map((f) => JSON.parse(JSON.stringify(f)) as EditorFloor);
  S.floorItemClipboard = itemKeys
    .map((k) => S.items.find((i) => i._editorKey === k))
    .filter((i): i is EditorItem => !!i)
    .map((i) => JSON.parse(JSON.stringify(i)) as EditorItem);
  S.floorPasteRound = 0;
  if (!S.floorClipboard.length && !S.floorItemClipboard.length) {
    setStatus("没有选中地板可复制", false);
    return;
  }
  setStatus(
    `已复制 ${S.floorClipboard.length} 块地板${S.floorItemClipboard.length ? `、${S.floorItemClipboard.length} 个地板物品` : ""}（Ctrl/Cmd+V 粘贴）${crossTabSuffix()}`
  );
}

export function cutFloors(): void {
  if (!S.selectedFloorKeys.size && !selectionKeys().length) {
    setStatus("没有选中地板可裁切", false);
    return;
  }
  copyFloors();
  if (!S.floorClipboard.length && !S.floorItemClipboard.length) return;
  pushHistory();
  const killItems = new Set(S.floorItemClipboard.map((i) => i._editorKey));
  S.floors = S.floors.filter((f) => !S.selectedFloorKeys.has(f._key));
  S.items = S.items.filter((i) => !killItems.has(i._editorKey));
  clearFloorSelection();
  clearSelection();
  closeModal();
  draw();
  updateFloorBar();
  setStatus(
    `已裁切 ${S.floorClipboard.length} 块地板${S.floorItemClipboard.length ? `、${S.floorItemClipboard.length} 个地板物品` : ""}（Ctrl/Cmd+V 粘贴）`
  );
}

export function pasteFloors(canvasMx?: number, canvasMy?: number): void {
  if (!S.floorClipboard.length && !S.floorItemClipboard.length) {
    setStatus("地板剪贴板为空（先 Ctrl/Cmd+C 复制）", false);
    return;
  }
  pushHistory();
  const allAnchors = [...S.floorClipboard, ...S.floorItemClipboard];
  const anchor = selectionCentroid(allAnchors);
  const { dx: offX, dz: offZ } = pasteOffsetFromPointer(anchor, canvasMx, canvasMy);
  const result = pasteFloorsWithOffset(S.floorClipboard, S.floorItemClipboard, offX, offZ);
  clearSelection();
  setFloorSelection(result.pastedFloorKeys);
  setSelection(result.pastedItemKeys);
  closeModal();
  hideDetail();
  draw();
  updateFloorBar();
  setStatus(
    `已粘贴 ${result.pastedFloorKeys.length} 块地板${result.pastedItemKeys.length ? `、${result.pastedItemKeys.length} 个地板物品` : ""}`
  );
}

export function duplicateFloors(): void {
  if (!S.selectedFloorKeys.size && !selectionKeys().length) {
    setStatus("没有选中地板可复制", false);
    return;
  }
  copyFloors();
  if (S.floorClipboard.length || S.floorItemClipboard.length) pasteFloors();
}
