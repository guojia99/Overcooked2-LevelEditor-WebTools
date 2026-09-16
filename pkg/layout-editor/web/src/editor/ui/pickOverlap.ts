import { S, EditorItem } from "../state";
import type { PickCandidate, PickTipHeaderAction, FloorHit } from "../state";
import { prefabIdFromPath } from "../coords";
import { itemLabel } from "../labels";
import { isThemedFloor } from "../floors";
import { surfaceKindLabelZh } from "../../floorColors";
import { floorWalkY, floorLayerIndex } from "../floorHeight";
import {
  isSelected,
  setSelection,
  clearSelection,
  setFloorSelection,
  clearFloorSelection,
} from "../selection";
import { draw } from "../render";
import { showPickTip, type PickTipOptions } from "./pickTip";
import { hideDetail, hideContextMenu } from "./overlay";

export interface ItemOverlapPickHandlers {
  onPickItem?: (it: EditorItem) => void;
}

export interface SurfaceOverlapPickHandlers {
  onPickFloor?: (fh: FloorHit) => void;
  onPickItem?: (it: EditorItem) => void;
}

export function showItemOverlapPickTip(
  hits: EditorItem[],
  clientX: number,
  clientY: number,
  shiftToggle: boolean,
  headerAction?: PickTipHeaderAction,
  handlers?: ItemOverlapPickHandlers
): void {
  const build = (): PickCandidate[] =>
    hits.map((it) => ({
      title: itemLabel(it),
      sub: prefabIdFromPath(it.prefabAssetPath) || "—",
      selected: isSelected(it._editorKey),
      onPick: () => {
        if (shiftToggle) {
          if (isSelected(it._editorKey)) S.selectedKeys.delete(it._editorKey);
          else S.selectedKeys.add(it._editorKey);
          S.selectedKey = it._editorKey;
          draw();
        } else if (handlers?.onPickItem) {
          handlers.onPickItem(it);
        } else {
          setSelection([it._editorKey]);
          draw();
        }
      },
    }));

  const opts: PickTipOptions = {
    toggleMode: shiftToggle,
    rebuild: build,
    headerAction: shiftToggle ? undefined : headerAction,
  };
  hideDetail();
  hideContextMenu();
  showPickTip(build(), clientX, clientY, opts);
}

export function showSurfaceOverlapPickTip(
  fHits: FloorHit[],
  surfaceHits: EditorItem[],
  clientX: number,
  clientY: number,
  shiftToggle: boolean,
  headerAction?: PickTipHeaderAction,
  handlers?: SurfaceOverlapPickHandlers
): void {
  const build = (): PickCandidate[] => {
    const candidates: PickCandidate[] = [];
    for (const fh of fHits) {
      const key = fh.floor._key;
      const fhH = floorWalkY(fh.floor);
      const hTag = fhH > 0.005 ? ` · h=${fhH.toFixed(2)} L${floorLayerIndex(fhH)}` : "";
      candidates.push({
        title: `${surfaceKindLabelZh(fh.floor.surfaceKind)} ${fh.floor._wCells}×${fh.floor._dCells}格${hTag}`,
        sub: `地板 · ${isThemedFloor(fh.floor) ? fh.floor.displayName : (fh.floor.materialName ?? "无材质")}`,
        selected: S.selectedFloorKeys.has(key),
        onPick: () => {
          if (shiftToggle) {
            if (S.selectedFloorKeys.has(key)) S.selectedFloorKeys.delete(key);
            else S.selectedFloorKeys.add(key);
            S.selectedFloorKey = key;
            draw();
          } else if (handlers?.onPickFloor) {
            handlers.onPickFloor(fh);
          } else {
            setFloorSelection([key]);
            clearSelection();
            draw();
          }
        },
      });
    }
    for (const it of surfaceHits) {
      candidates.push({
        title: itemLabel(it),
        sub: `${surfaceKindLabelZh(S.catalogByGuid.get(it.prefabGuid)?.surfaceKind)} · ${prefabIdFromPath(it.prefabAssetPath)}`,
        selected: isSelected(it._editorKey),
        onPick: () => {
          if (shiftToggle) {
            if (isSelected(it._editorKey)) S.selectedKeys.delete(it._editorKey);
            else S.selectedKeys.add(it._editorKey);
            S.selectedKey = it._editorKey;
            draw();
          } else if (handlers?.onPickItem) {
            handlers.onPickItem(it);
          } else {
            clearFloorSelection();
            setSelection([it._editorKey]);
            draw();
          }
        },
      });
    }
    return candidates;
  };

  const opts: PickTipOptions = {
    toggleMode: shiftToggle,
    rebuild: build,
    headerAction: shiftToggle ? undefined : headerAction,
  };
  hideDetail();
  hideContextMenu();
  showPickTip(build(), clientX, clientY, opts);
}

/** 动画组成员模式：物品 + 空气地板重叠候选。 */
export function showAnimMemberOverlapPickTip(
  hits: EditorItem[],
  fHit: FloorHit["floor"],
  clientX: number,
  clientY: number,
  shiftToggle: boolean,
  onChanged: () => void
): void {
  const build = (): PickCandidate[] => {
    const candidates: PickCandidate[] = hits.map((it) => ({
      title: itemLabel(it),
      sub: `物品 · ${prefabIdFromPath(it.prefabAssetPath)}`,
      selected: isSelected(it._editorKey),
      onPick: () => {
        if (shiftToggle) {
          if (isSelected(it._editorKey)) S.selectedKeys.delete(it._editorKey);
          else S.selectedKeys.add(it._editorKey);
          S.selectedKey = it._editorKey;
        } else {
          setSelection([it._editorKey]);
          clearFloorSelection();
        }
        onChanged();
      },
    }));
    candidates.push({
      title: `空气地板 ${fHit._wCells}×${fHit._dCells}格`,
      sub: "地板 · 仅碰撞盒（入组后碰撞随组移动）",
      selected: S.selectedFloorKeys.has(fHit._key),
      onPick: () => {
        if (shiftToggle) {
          if (S.selectedFloorKeys.has(fHit._key)) S.selectedFloorKeys.delete(fHit._key);
          else S.selectedFloorKeys.add(fHit._key);
          S.selectedFloorKey = fHit._key;
        } else {
          clearSelection();
          setFloorSelection([fHit._key]);
        }
        onChanged();
      },
    });
    return candidates;
  };

  hideDetail();
  hideContextMenu();
  showPickTip(build(), clientX, clientY, {
    toggleMode: shiftToggle,
    rebuild: build,
  });
}
