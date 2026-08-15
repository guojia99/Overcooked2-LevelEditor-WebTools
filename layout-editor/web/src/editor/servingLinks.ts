import {
  worldToCanvas,
  prefabIdFromPath
} from "./coords";
import {
  S,
  EditorItem
} from "./state";
import { dom } from "./dom";

export function isServingStationItem(item: EditorItem): boolean {
  return item.stubKind === "ServingStation" || prefabIdFromPath(item.prefabAssetPath) === "ServingStation";
}

export function isPlateReturnItem(item: EditorItem): boolean {
  const id = prefabIdFromPath(item.prefabAssetPath);
  return id === "PlateReturn" || id === "dlc13_workstation_plate_return";
}

export function isGlassReturnItem(item: EditorItem): boolean {
  const id = prefabIdFromPath(item.prefabAssetPath);
  return id === "GlassReturn" || id === "dlc11_workstation_glass_return_01";
}

export function isMugReturnItem(item: EditorItem): boolean {
  const id = prefabIdFromPath(item.prefabAssetPath);
  return id === "workstation_mug_return" || id === "dlc09_workstation_mug_return_winter";
}

export function isTrayReturnItem(item: EditorItem): boolean {
  return prefabIdFromPath(item.prefabAssetPath) === "dlc08_workstation_tray_return";
}

export function isReturnStationItem(item: EditorItem): boolean {
  return isPlateReturnItem(item) || isGlassReturnItem(item) || isMugReturnItem(item) || isTrayReturnItem(item);
}

export function servingStations(): EditorItem[] {
  return S.items.filter(isServingStationItem);
}

export function returnStations(): EditorItem[] {
  return S.items.filter(isReturnStationItem);
}

export function plateReturns(): EditorItem[] {
  return S.items.filter(isPlateReturnItem);
}

export function glassReturns(): EditorItem[] {
  return S.items.filter(isGlassReturnItem);
}

export function mugReturns(): EditorItem[] {
  return S.items.filter(isMugReturnItem);
}

export function trayReturns(): EditorItem[] {
  return S.items.filter(isTrayReturnItem);
}

export function computeReturnLabels(): Map<string, string> {
  const label = new Map<string, string>();
  let p = 0;
  let g = 0;
  let m = 0;
  let t = 0;
  for (const r of returnStations()) {
    if (isMugReturnItem(r)) label.set(r.instanceId, `脏马克杯台${++m}`);
    else if (isTrayReturnItem(r)) label.set(r.instanceId, `餐盘回收台${++t}`);
    else if (isGlassReturnItem(r)) label.set(r.instanceId, `脏杯台${++g}`);
    else label.set(r.instanceId, `脏盘台${++p}`);
  }
  return label;
}

export function servingBoundReturns(s: EditorItem): string[] {
  const ss = s.servingStation;
  if (!ss) return [];
  if (ss.plateReturnInstanceIds && ss.plateReturnInstanceIds.length) return [...ss.plateReturnInstanceIds];
  if (ss.plateReturnInstanceId) return [ss.plateReturnInstanceId];
  return [];
}

export function setServingReturns(s: EditorItem, ids: string[]): void {
  s.stubKind = "ServingStation";
  s.servingStation = {
    plateReturnInstanceIds: ids,
    plateReturnInstanceId: ids[0] ?? "",
  };
}

export function servingPlateReturn(s: EditorItem): string | undefined {
  return servingBoundReturns(s).find((id) => {
    const r = S.items.find((i) => i.instanceId === id);
    return r && isPlateReturnItem(r);
  });
}

export function servingGlassReturn(s: EditorItem): string | undefined {
  return servingBoundReturns(s).find((id) => {
    const r = S.items.find((i) => i.instanceId === id);
    return r && isGlassReturnItem(r);
  });
}

export function servingMugReturn(s: EditorItem): string | undefined {
  return servingBoundReturns(s).find((id) => {
    const r = S.items.find((i) => i.instanceId === id);
    return r && isMugReturnItem(r);
  });
}

export function servingTrayReturn(s: EditorItem): string | undefined {
  return servingBoundReturns(s).find((id) => {
    const r = S.items.find((i) => i.instanceId === id);
    return r && isTrayReturnItem(r);
  });
}

export type ServingReturnKind = "plate" | "glass" | "mug" | "tray";

export function setServingReturnOfType(s: EditorItem, returnId: string | undefined, kind: ServingReturnKind): void {
  const keep = (kind2: ServingReturnKind): string | undefined => {
    if (kind === kind2) return returnId;
    if (kind2 === "plate") return servingPlateReturn(s);
    if (kind2 === "glass") return servingGlassReturn(s);
    if (kind2 === "mug") return servingMugReturn(s);
    return servingTrayReturn(s);
  };
  const ids = [keep("plate"), keep("glass"), keep("mug"), keep("tray")].filter((x): x is string => !!x);
  setServingReturns(s, ids);
}

export function servingStationsForReturn(returnInstanceId: string): EditorItem[] {
  return servingStations().filter((s) => servingBoundReturns(s).includes(returnInstanceId));
}

export function drawServingLinks() {
  const byInst = new Map(S.items.map((i) => [i.instanceId, i]));
  for (const s of servingStations()) {
    for (const prId of servingBoundReturns(s)) {
      const p = byInst.get(prId);
      if (!p) continue;
      const a = worldToCanvas(s._wx, s._wz);
      const b = worldToCanvas(p._wx, p._wz);
    dom.ctx.save();
    dom.ctx.strokeStyle = "#7bd889";
    dom.ctx.globalAlpha = 0.5;
    dom.ctx.lineWidth = 1.5;
    dom.ctx.setLineDash([6, 4]);
    dom.ctx.beginPath();
    dom.ctx.moveTo(a.x, a.y);
    dom.ctx.lineTo(b.x, b.y);
    dom.ctx.stroke();
    dom.ctx.setLineDash([]);
    const rad = Math.atan2(b.y - a.y, b.x - a.x);
    const ah = 8 * Math.max(0.6, S.scale);
    dom.ctx.fillStyle = "#7bd889";
    dom.ctx.beginPath();
    dom.ctx.moveTo(b.x, b.y);
    dom.ctx.lineTo(b.x - Math.cos(rad - 0.45) * ah, b.y - Math.sin(rad - 0.45) * ah);
    dom.ctx.lineTo(b.x - Math.cos(rad + 0.45) * ah, b.y - Math.sin(rad + 0.45) * ah);
    dom.ctx.closePath();
    dom.ctx.fill();
    dom.ctx.restore();
    }
  }
}
