/**
 * 跨标签页素材剪贴板（纯前端）：localStorage + BroadcastChannel。
 * 同页 Ctrl/Cmd+V 仍读 S.clipboard；跨页用 Ctrl/Cmd+Shift+V。
 * 含 switchLinks / coaxialLinks / buttonEvents / 物品内引用；不含动画组与 buttonLinks。
 */
import type { ButtonEventLink, CoaxialLink, SwitchLink } from "../types";
import { ingredientIdByGuid, itemLayerOfIt } from "./catalog";
import { isSurfaceItem } from "../floorColors";
import {
  pasteFloorsWithOffset,
  pasteItemsWithOffset,
  selectionCentroid,
} from "./clipboardPaste";
import { pasteGridDelta, pastePointerWorld } from "./coords";
import { pushHistory } from "./historyOps";
import { draw } from "./render";
import { isPlayerItem } from "./renderItems";
import {
  setFloorSelection,
  setSelection,
  clearSelection,
  clearFloorSelection,
} from "./selection";
import { hideDetail, hideContextMenu } from "./ui/overlay";
import { closeModal } from "../modals";
import { updateFloorBar } from "./floorPalette";
import { setStatus } from "./status";
import {
  remapButtonEventsForPaste,
  remapCoaxialLinksForPaste,
  remapSwitchLinksForPaste,
  cleanOrphanedStubRefs,
} from "./stubRefs";
import { cleanOrphanedButtonEvents } from "./buttonEvents";
import { cleanOrphanedCoaxialLinks } from "./buttonLinks";
import {
  S,
  EditorItem,
  EditorFloor,
  LayerKey,
  isFloorLikeLayer,
} from "./state";

const STORAGE_KEY = "layout-editor:cross-clipboard";
const CHANNEL_NAME = "layout-editor-clipboard-v1";
/** localStorage 约 5MB；留余量避免写入失败。 */
const MAX_CROSS_TAB_BYTES = 4_500_000;

const LAYER_LABEL: Record<LayerKey, string> = {
  items: "核心层",
  decor: "装饰层",
  floor: "地板层",
  background: "背景层",
  anim: "动画层",
};

export interface CrossTabClipboardPayload {
  version: 1;
  copiedAt: number;
  sourceLayer: LayerKey;
  sourceScenePath?: string;
  kind: "items" | "floors";
  items?: EditorItem[];
  floors?: EditorFloor[];
  floorItems?: EditorItem[];
  switchLinks?: SwitchLink[];
  coaxialLinks?: CoaxialLink[];
  buttonEvents?: ButtonEventLink[];
}

export interface CrossTabValidation {
  ok: boolean;
  errors: string[];
  warnings: string[];
}

let broadcastChannel: BroadcastChannel | null = null;

function getChannel(): BroadcastChannel | null {
  if (broadcastChannel !== null) return broadcastChannel;
  try {
    broadcastChannel = new BroadcastChannel(CHANNEL_NAME);
  } catch {
    broadcastChannel = null;
  }
  return broadcastChannel;
}

function collectInstanceIds(items: EditorItem[]): Set<string> {
  const ids = new Set<string>();
  for (const it of items) {
    if (it.instanceId) ids.add(it.instanceId);
  }
  return ids;
}

function filterSwitchLinks(ids: Set<string>): SwitchLink[] {
  return S.switchLinks
    .filter((l) => ids.has(l.switchId) && ids.has(l.targetId))
    .map((l) => JSON.parse(JSON.stringify(l)) as SwitchLink);
}

function filterCoaxialLinks(ids: Set<string>): CoaxialLink[] {
  return S.coaxialLinks
    .filter(
      (l) =>
        l.sourceIds.length >= 2 &&
        l.sourceIds.every((id) => ids.has(id)) &&
        (l.targetIds ?? []).every((id) => ids.has(id))
    )
    .map((l) => JSON.parse(JSON.stringify(l)) as CoaxialLink);
}

function filterButtonEvents(ids: Set<string>): ButtonEventLink[] {
  return S.buttonEvents
    .filter((l) => {
      if (!ids.has(l.sourceId)) return false;
      for (const g of l.groups) {
        for (const e of g.events) {
          if (!ids.has(e.targetId)) return false;
        }
      }
      return true;
    })
    .map((l) => JSON.parse(JSON.stringify(l)) as ButtonEventLink);
}

function itemsForCrossTabExport(): EditorItem[] {
  if (isFloorLikeLayer(S.currentLayer)) {
    return S.floorItemClipboard.map((i) => JSON.parse(JSON.stringify(i)) as EditorItem);
  }
  let items = S.clipboard.map((i) => JSON.parse(JSON.stringify(i)) as EditorItem);
  if (S.currentLayer === "items" || S.currentLayer === "decor") {
    items = items.filter((it) => itemLayerOfIt(it) === S.currentLayer);
  }
  return items.filter((it) => !isPlayerItem(it));
}

function buildCrossTabPayload(): CrossTabClipboardPayload | null {
  if (isFloorLikeLayer(S.currentLayer)) {
    if (!S.floorClipboard.length && !S.floorItemClipboard.length) return null;
    const floorItems = itemsForCrossTabExport();
    const ids = collectInstanceIds(floorItems);
    return {
      version: 1,
      copiedAt: Date.now(),
      sourceLayer: S.currentLayer,
      sourceScenePath: S.scenePath,
      kind: "floors",
      floors: S.floorClipboard.map((f) => JSON.parse(JSON.stringify(f)) as EditorFloor),
      floorItems,
      switchLinks: filterSwitchLinks(ids),
      coaxialLinks: filterCoaxialLinks(ids),
      buttonEvents: filterButtonEvents(ids),
    };
  }

  const items = itemsForCrossTabExport();
  if (!items.length) return null;
  const ids = collectInstanceIds(items);
  return {
    version: 1,
    copiedAt: Date.now(),
    sourceLayer: S.currentLayer,
    sourceScenePath: S.scenePath,
    kind: "items",
    items,
    switchLinks: filterSwitchLinks(ids),
    coaxialLinks: filterCoaxialLinks(ids),
    buttonEvents: filterButtonEvents(ids),
  };
}

function allPayloadItems(payload: CrossTabClipboardPayload): EditorItem[] {
  return [...(payload.items ?? []), ...(payload.floorItems ?? [])];
}

export function readCrossTabPayload(): CrossTabClipboardPayload | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as CrossTabClipboardPayload;
    if (parsed?.version !== 1) return null;
    return parsed;
  } catch {
    return null;
  }
}

function refreshCrossTabMetaFromStorage(): void {
  const payload = readCrossTabPayload();
  if (!payload) {
    S.crossTabClipboardMeta = null;
    return;
  }
  S.crossTabClipboardMeta = {
    available: true,
    sourceLayer: payload.sourceLayer,
    copiedAt: payload.copiedAt,
  };
}

export function initCrossTabClipboardListener(): void {
  refreshCrossTabMetaFromStorage();
  window.addEventListener("storage", (e) => {
    if (e.key === STORAGE_KEY) refreshCrossTabMetaFromStorage();
  });
  const ch = getChannel();
  if (ch) {
    ch.onmessage = () => {
      refreshCrossTabMetaFromStorage();
      const meta = S.crossTabClipboardMeta;
      if (meta?.available && meta.sourceLayer === S.currentLayer) {
        setStatus(
          `可 Shift+V 跨页粘贴（来自${LAYER_LABEL[meta.sourceLayer]}）`,
          false
        );
      }
    };
  }
}

/** 复制成功后旁路写入跨页存储；失败不影响同页剪贴板。 */
export function publishCrossTabClipboard(): boolean {
  try {
    const payload = buildCrossTabPayload();
    if (!payload) return false;
    const json = JSON.stringify(payload);
    if (json.length > MAX_CROSS_TAB_BYTES) {
      setStatus("跨页剪贴板过大，未同步到其他标签页", false);
      return false;
    }
    localStorage.setItem(STORAGE_KEY, json);
    getChannel()?.postMessage({ type: "updated" });
    refreshCrossTabMetaFromStorage();
    return true;
  } catch (e) {
    console.warn("[crossTabClipboard] publish failed", e);
    return false;
  }
}

export function hasCrossTabClipboard(): boolean {
  return !!S.crossTabClipboardMeta?.available;
}

export function validateCrossTabPayload(
  payload: CrossTabClipboardPayload
): CrossTabValidation {
  const errors: string[] = [];
  const warnings: string[] = [];

  if (payload.version !== 1) {
    errors.push("跨页剪贴板版本不兼容");
    return { ok: false, errors, warnings };
  }

  const itemCount =
    (payload.items?.length ?? 0) + (payload.floorItems?.length ?? 0);
  const floorCount = payload.floors?.length ?? 0;
  if (!itemCount && !floorCount) {
    errors.push("跨页剪贴板为空");
    return { ok: false, errors, warnings };
  }

  if (payload.sourceLayer !== S.currentLayer) {
    errors.push(
      `请在${LAYER_LABEL[payload.sourceLayer]}粘贴（当前为${LAYER_LABEL[S.currentLayer]}）`
    );
  }

  const expectFloors = isFloorLikeLayer(S.currentLayer);
  if (expectFloors && payload.kind !== "floors") {
    errors.push("地板/背景层只能粘贴地板素材");
  }
  if (!expectFloors && payload.kind !== "items") {
    errors.push("物品层只能粘贴物品素材");
  }

  const payloadItems = allPayloadItems(payload);
  const ids = collectInstanceIds(payloadItems);
  const floorMatGuids = new Set(S.floorMaterials.map((m) => m.guid));
  const warnedGuids = new Set<string>();

  /** 机器 soArray / dispenser 可引用 catalog 伪 prefab 或食材目录 guid。 */
  function isKnownOutputGuid(guid: string): boolean {
    return S.catalogByGuid.has(guid) || !!ingredientIdByGuid(guid);
  }

  for (const it of payloadItems) {
    if (!S.catalogByGuid.has(it.prefabGuid)) {
      errors.push(`目录中缺少物品 ${it.prefabGuid}`);
    }
    const guids = [...(it.soArray?.pseudoPrefabGuids ?? [])];
    if (it.dispenser?.spawnerItemPrefabGuid) guids.push(it.dispenser.spawnerItemPrefabGuid);
    for (const g of guids) {
      if (!g || isKnownOutputGuid(g) || warnedGuids.has(g)) continue;
      warnedGuids.add(g);
      warnings.push(`食材/产出 guid ${g} 在本场景目录中未找到`);
    }
  }

  for (const f of payload.floors ?? []) {
    if (f.materialGuid && !floorMatGuids.has(f.materialGuid)) {
      warnings.push(`地板材质 ${f.materialGuid} 在本场景不可用`);
    }
  }

  for (const l of payload.switchLinks ?? []) {
    if (!ids.has(l.switchId) || !ids.has(l.targetId)) {
      errors.push("开关联动数据不完整");
      break;
    }
  }
  for (const l of payload.coaxialLinks ?? []) {
    if (!l.sourceIds.every((id) => ids.has(id))) {
      errors.push("同轴按钮组数据不完整");
      break;
    }
    if (!(l.targetIds ?? []).every((id) => ids.has(id))) {
      errors.push("同轴按钮组目标数据不完整");
      break;
    }
  }
  for (const l of payload.buttonEvents ?? []) {
    if (!ids.has(l.sourceId)) {
      errors.push("按钮事件组数据不完整");
      break;
    }
    for (const g of l.groups) {
      for (const e of g.events) {
        if (!ids.has(e.targetId)) {
          errors.push("按钮事件组目标数据不完整");
          break;
        }
      }
    }
  }

  return { ok: errors.length === 0, errors, warnings };
}

export function canPasteCrossTab(): boolean {
  const payload = readCrossTabPayload();
  if (!payload) return false;
  return validateCrossTabPayload(payload).ok;
}

function mergeDocLinks(
  payload: CrossTabClipboardPayload,
  pasteIdMap: Map<string, string>
): void {
  if (payload.switchLinks?.length) {
    S.switchLinks.push(...remapSwitchLinksForPaste(payload.switchLinks, pasteIdMap));
  }
  if (payload.coaxialLinks?.length) {
    S.coaxialLinks.push(...remapCoaxialLinksForPaste(payload.coaxialLinks, pasteIdMap));
  }
  if (payload.buttonEvents?.length) {
    S.buttonEvents.push(...remapButtonEventsForPaste(payload.buttonEvents, pasteIdMap));
  }
}

export function pasteCrossTabClipboard(canvasMx?: number, canvasMy?: number): void {
  const payload = readCrossTabPayload();
  if (!payload) {
    setStatus("跨页剪贴板为空", false);
    return;
  }

  const validation = validateCrossTabPayload(payload);
  if (!validation.ok) {
    setStatus(validation.errors.join("；"), false);
    return;
  }

  pushHistory();

  let statusParts: string[] = [];
  let pasteIdMap = new Map<string, string>();

  if (payload.kind === "floors") {
    const floors = payload.floors ?? [];
    const floorItems = payload.floorItems ?? [];
    const anchor = selectionCentroid([...floors, ...floorItems]);
    const ptr = pastePointerWorld(canvasMx, canvasMy);
    const { dx: offX, dz: offZ } = pasteGridDelta(anchor.x, anchor.z, ptr.x, ptr.z);

    const result = pasteFloorsWithOffset(floors, floorItems, offX, offZ);
    pasteIdMap = result.pasteIdMap;
    mergeDocLinks(payload, pasteIdMap);
    cleanOrphanedStubRefs();
    cleanOrphanedButtonEvents();
    cleanOrphanedCoaxialLinks();

    clearSelection();
    setFloorSelection(result.pastedFloorKeys);
    setSelection(result.pastedItemKeys);
    closeModal();
    statusParts.push(
      `已跨页粘贴 ${result.pastedFloorKeys.length} 块地板` +
        (result.pastedItemKeys.length
          ? `、${result.pastedItemKeys.length} 个地板物品`
          : "") +
        (result.skippedItems ? `（${result.skippedItems} 个物品因重叠跳过）` : "")
    );
    updateFloorBar();
  } else {
    const items = payload.items ?? [];
    const anchor = selectionCentroid(items);
    const ptr = pastePointerWorld(canvasMx, canvasMy);
    const { dx: offX, dz: offZ } = pasteGridDelta(anchor.x, anchor.z, ptr.x, ptr.z);

    const result = pasteItemsWithOffset(items, offX, offZ);
    pasteIdMap = result.pasteIdMap;
    mergeDocLinks(payload, pasteIdMap);
    cleanOrphanedStubRefs();
    cleanOrphanedButtonEvents();
    cleanOrphanedCoaxialLinks();

    setSelection(result.pastedKeys);
    statusParts.push(
      `已跨页粘贴 ${result.pastedKeys.length} 个物品` +
        (result.skipped ? `（${result.skipped} 个因与玩家重叠被跳过）` : "")
    );
  }

  hideDetail();
  hideContextMenu();
  draw();

  if (validation.warnings.length) {
    statusParts.push(validation.warnings[0]);
  }
  setStatus(statusParts.join("；"));
}
