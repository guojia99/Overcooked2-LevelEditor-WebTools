import {
  S,
  CELL,
  EditorItem,
  EditorFloor
} from "./state";
import { newEditorKey } from "./coords";
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
import { moveBlockedAt } from "./items";
import { finalizeFloor } from "./floors";
import { updateFloorBar } from "./floorPalette";
import { closeModal } from "../modals";

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
  setStatus(`已复制 ${S.clipboard.length} 个物品（Ctrl/Cmd+V 粘贴）`);
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

export function pasteClipboard() {
  if (!S.clipboard.length) {
    setStatus("剪贴板为空（先 Ctrl/Cmd+V 复制）", false);
    return;
  }
  pushHistory();
  S.pasteRound++;
  const off = CELL * S.pasteRound;
  const pasted: string[] = [];
  let skipped = 0;
  for (const src of S.clipboard) {
    if (isPlayerItem(src)) {
      skipped++;
      continue;
    }
    const nx = src._wx + off;
    const nz = src._wz + off;
    if (moveBlockedAt(src, nx, nz)) {
      skipped++;
      continue;
    }
    const editorKey = newEditorKey();
    const copy = JSON.parse(JSON.stringify(src)) as EditorItem;
    copy._editorKey = editorKey;
    copy.instanceId = `new:copy:${crypto.randomUUID()}`;
    copy.hierarchyPath = copy.instanceId;
    copy._wx = nx;
    copy._wz = nz;
    copy.localPosition = {
      x: copy._wx - copy._parentWx,
      y: copy.localPosition.y,
      z: copy._wz - copy._parentWz,
    };
    copy.worldPosition = { x: copy._wx, y: copy.localPosition.y, z: copy._wz };
    S.items.push(copy);
    pasted.push(editorKey);
  }
  setSelection(pasted);
  hideDetail();
  hideContextMenu();
  draw();
  setStatus(`已粘贴 ${pasted.length} 个物品${skipped ? `（${skipped} 个因与玩家重叠被跳过）` : ""}`);
}

export function copyFloors(): void {
  const keys = [...S.selectedFloorKeys];
  if (!keys.length) {
    setStatus("没有选中地板可复制");
    return;
  }
  S.floorClipboard = keys
    .map((k) => S.floors.find((f) => f._key === k))
    .filter((f): f is EditorFloor => !!f)
    .map((f) => JSON.parse(JSON.stringify(f)) as EditorFloor);
  S.floorPasteRound = 0;
  if (!S.floorClipboard.length) {
    setStatus("没有选中地板可复制", false);
    return;
  }
  setStatus(`已复制 ${S.floorClipboard.length} 块地板（Ctrl/Cmd+V 粘贴）`);
}

export function cutFloors(): void {
  if (!S.selectedFloorKeys.size) {
    setStatus("没有选中地板可裁切", false);
    return;
  }
  copyFloors();
  pushHistory();
  S.floors = S.floors.filter((f) => !S.selectedFloorKeys.has(f._key));
  clearFloorSelection();
  closeModal();
  draw();
  updateFloorBar();
  setStatus(`已裁切 ${S.floorClipboard.length} 块地板（Ctrl/Cmd+V 粘贴）`);
}

export function pasteFloors(): void {
  if (!S.floorClipboard.length) {
    setStatus("地板剪贴板为空（先 Ctrl/Cmd+C 复制）", false);
    return;
  }
  pushHistory();
  S.floorPasteRound++;
  const off = CELL * S.floorPasteRound;
  const pastedKeys: string[] = [];
  for (const src of S.floorClipboard) {
    const key = newEditorKey();
    const copy = JSON.parse(JSON.stringify(src)) as EditorFloor;
    copy._key = key;
    copy.instanceId = `new:floor:${crypto.randomUUID()}`;
    copy.hierarchyPath = copy.instanceId;
    copy._wx = src._wx + off;
    copy._wz = src._wz + off;
    copy.localPosition = { x: copy._wx, y: copy.localPosition?.y ?? -0.05, z: copy._wz };
    copy.worldPosition = { x: copy._wx, y: copy.localPosition.y, z: copy._wz };
    S.floors.push(copy);
    pastedKeys.push(key);
  }
  for (const k of pastedKeys) {
    const f = S.floors.find((x) => x._key === k);
    if (f) finalizeFloor(f);
  }
  clearSelection();
  setFloorSelection(pastedKeys);
  closeModal();
  hideDetail();
  draw();
  updateFloorBar();
  setStatus(`已粘贴 ${pastedKeys.length} 块地板`);
}

export function duplicateFloors(): void {
  if (!S.selectedFloorKeys.size) {
    setStatus("没有选中地板可复制", false);
    return;
  }
  copyFloors();
  if (S.floorClipboard.length) pasteFloors();
}
