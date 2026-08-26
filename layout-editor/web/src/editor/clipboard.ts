import {
  S,
  CELL,
  EditorItem,
  EditorFloor
} from "./state";
import { uuid, newEditorKey, syncItemLocalFromEditor, editorItemUnityWorldXZ } from "./coords";
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
import { isSurfaceItem } from "../floorColors";

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
    copy.instanceId = `new:copy:${uuid()}`;
    copy.hierarchyPath = copy.instanceId;
    copy._wx = nx;
    copy._wz = nz;
    syncItemLocalFromEditor(copy);
    const u = editorItemUnityWorldXZ(copy);
    copy.worldPosition = { x: u.x, y: copy.localPosition.y, z: u.z };
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
    `已复制 ${S.floorClipboard.length} 块地板${S.floorItemClipboard.length ? `、${S.floorItemClipboard.length} 个地板物品` : ""}（Ctrl/Cmd+V 粘贴）`
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

export function pasteFloors(): void {
  if (!S.floorClipboard.length && !S.floorItemClipboard.length) {
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
    copy.instanceId = `new:floor:${uuid()}`;
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
  const pastedItems: string[] = [];
  for (const src of S.floorItemClipboard) {
    const nx = src._wx + off;
    const nz = src._wz + off;
    if (moveBlockedAt(src, nx, nz)) continue;
    const editorKey = newEditorKey();
    const copy = JSON.parse(JSON.stringify(src)) as EditorItem;
    copy._editorKey = editorKey;
    copy.instanceId = `new:copy:${uuid()}`;
    copy.hierarchyPath = copy.instanceId;
    copy._wx = nx;
    copy._wz = nz;
    syncItemLocalFromEditor(copy);
    const u = editorItemUnityWorldXZ(copy);
    copy.worldPosition = { x: u.x, y: copy.localPosition.y, z: u.z };
    S.items.push(copy);
    pastedItems.push(editorKey);
  }
  clearSelection();
  setFloorSelection(pastedKeys);
  setSelection(pastedItems);
  closeModal();
  hideDetail();
  draw();
  updateFloorBar();
  setStatus(
    `已粘贴 ${pastedKeys.length} 块地板${pastedItems.length ? `、${pastedItems.length} 个地板物品` : ""}`
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
