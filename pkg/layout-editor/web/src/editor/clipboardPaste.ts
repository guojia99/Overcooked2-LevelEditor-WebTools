import { S, EditorItem, EditorFloor } from "./state";
import {
  uuid,
  newEditorKey,
  syncItemLocalFromEditor,
  editorItemUnityWorldXZ,
} from "./coords";
import { isPlayerItem } from "./renderItems";
import { moveBlockedAt } from "./items";
import { remapRefsWithinItems } from "./stubRefs";
import { finalizeFloor } from "./floors";
import { stampNewItemPart, stampNewFloorPart } from "./partLevel";

export function selectionCentroid(items: { _wx: number; _wz: number }[]): { x: number; z: number } {
  if (!items.length) return { x: 0, z: 0 };
  let sx = 0;
  let sz = 0;
  for (const it of items) {
    sx += it._wx;
    sz += it._wz;
  }
  return { x: sx / items.length, z: sz / items.length };
}

export interface PasteItemsResult {
  pastedKeys: string[];
  pasteIdMap: Map<string, string>;
  pastedCopies: EditorItem[];
  skipped: number;
}

/** 将物品批次粘贴到场景（新 instanceId + 网格偏移）；同页与跨页共用。 */
export function pasteItemsWithOffset(
  srcItems: EditorItem[],
  offX: number,
  offZ: number
): PasteItemsResult {
  const pasted: string[] = [];
  const pastedCopies: EditorItem[] = [];
  const pasteIdMap = new Map<string, string>();
  let skipped = 0;
  for (const src of srcItems) {
    if (isPlayerItem(src)) {
      skipped++;
      continue;
    }
    const nx = src._wx + offX;
    const nz = src._wz + offZ;
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
    // 分 P：粘贴落点归入当前编辑的 P（跨 P 复制即「搬运布局」，决策 §3.2）。
    stampNewItemPart(copy);
    S.items.push(copy);
    pasted.push(editorKey);
    pastedCopies.push(copy);
    if (src.instanceId && src.instanceId !== copy.instanceId)
      pasteIdMap.set(src.instanceId, copy.instanceId);
  }
  remapRefsWithinItems(pastedCopies, pasteIdMap);
  return { pastedKeys: pasted, pasteIdMap, pastedCopies, skipped };
}

export interface PasteFloorsResult {
  pastedFloorKeys: string[];
  pastedItemKeys: string[];
  pasteIdMap: Map<string, string>;
  skippedItems: number;
}

/** 将地板批次粘贴到场景；同页与跨页共用。 */
export function pasteFloorsWithOffset(
  floorSrc: EditorFloor[],
  itemSrc: EditorItem[],
  offX: number,
  offZ: number
): PasteFloorsResult {
  const pastedKeys: string[] = [];
  for (const src of floorSrc) {
    const key = newEditorKey();
    const copy = JSON.parse(JSON.stringify(src)) as EditorFloor;
    copy._key = key;
    copy.instanceId = `new:floor:${uuid()}`;
    copy.hierarchyPath = copy.instanceId;
    copy._wx = src._wx + offX;
    copy._wz = src._wz + offZ;
    copy.localPosition = { x: copy._wx, y: copy.localPosition?.y ?? -0.05, z: copy._wz };
    copy.worldPosition = { x: copy._wx, y: copy.localPosition.y, z: copy._wz };
    // 分 P：粘贴地板同样归入当前 P。
    stampNewFloorPart(copy);
    S.floors.push(copy);
    pastedKeys.push(key);
  }
  for (const k of pastedKeys) {
    const f = S.floors.find((x) => x._key === k);
    if (f) finalizeFloor(f);
  }
  const itemResult = pasteItemsWithOffset(itemSrc, offX, offZ);
  return {
    pastedFloorKeys: pastedKeys,
    pastedItemKeys: itemResult.pastedKeys,
    pasteIdMap: itemResult.pasteIdMap,
    skippedItems: itemResult.skipped,
  };
}
