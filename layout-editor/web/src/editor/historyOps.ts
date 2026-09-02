import {
  S,
  EditorSnapshot
} from "./state";
import {
  clearSelection,
  clearFloorSelection
} from "./selection";
import {
  hideDetail,
  hideContextMenu,
  hidePickTip
} from "./ui/overlay";
import { draw } from "./render";
import { setStatus } from "./status";

export function snapshotState(): EditorSnapshot {
  return JSON.parse(JSON.stringify({
    items: S.items,
    floors: S.floors,
    bgThemeKey: S.bgThemeKey,
    animControls: S.animControls,
    switchLinks: S.switchLinks,
    buttonLinks: S.buttonLinks,
    buttonEvents: S.buttonEvents,
    cameraInfo: S.cameraInfo,
    lights: S.lights,
  })) as EditorSnapshot;
}

export function updateSaveIndicator(): void {
  const btn = document.getElementById("btn-save");
  if (btn) btn.textContent = S.dirty ? "写回 Unity *" : "写回 Unity";
}

export function markDirty(): void {
  S.dirty = true;
  updateSaveIndicator();
}

export function clearDirty(): void {
  S.dirty = false;
  updateSaveIndicator();
}

export function pushHistory(): void {
  S.history.push(snapshotState());
  markDirty();
}

export function applySnapshot(snap: EditorSnapshot): void {
  S.items = snap.items;
  S.floors = snap.floors;
  S.bgThemeKey = snap.bgThemeKey;
  S.animControls = snap.animControls;
  S.switchLinks = snap.switchLinks ?? [];
  S.buttonLinks = snap.buttonLinks ?? [];
  S.buttonEvents = snap.buttonEvents ?? [];
  S.cameraInfo = snap.cameraInfo ?? null;
  S.lights = snap.lights ?? [];
  // Restore anim-group selection only when the group still exists.
  if (S.activeAnimGroupId && !S.animControls.some((g) => g.id === S.activeAnimGroupId)) {
    S.activeAnimGroupId = null;
    S.activeAnimEventIdx = null;
    S.selectedWaypointId = null;
  }
  if (S.animMode !== "none") {
    S.animMode = "none";
  }
  clearSelection();
  clearFloorSelection();
  hideDetail();
  hideContextMenu();
  hidePickTip();
  markDirty();
  draw();
}

export function undo(): void {
  const snap = S.history.undo(snapshotState());
  if (!snap) {
    setStatus("没有可撤回的操作", false);
    return;
  }
  applySnapshot(snap);
  setStatus(`已撤回（还可撤回 ${S.history.size} 步，写回 Unity 后生效）`);
}

export function redo(): void {
  const snap = S.history.redo(snapshotState());
  if (!snap) {
    setStatus("没有可重做的操作", false);
    return;
  }
  applySnapshot(snap);
  setStatus(`已重做（还可重做 ${S.history.redoSize} 步，写回 Unity 后生效）`);
}

export function commitDragSnapshot(): void {
  if (!S.dragSnapshot) return;
  const snap = S.dragSnapshot;
  S.dragSnapshot = null;
  if (JSON.stringify(snapshotState()) !== JSON.stringify(snap)) {
    S.history.push(snap);
    markDirty();
  }
}
