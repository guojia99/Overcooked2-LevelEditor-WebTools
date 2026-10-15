import type { LightInfo, LayoutVector3 } from "../types";
import { S } from "./state";
import { pushHistory } from "./historyOps";

/** LightType 枚举：0=Spot 1=Directional 2=Point 3=Area。 */
export const LIGHT_TYPE_LABELS: Record<number, string> = {
  0: "聚光灯",
  1: "方向光",
  2: "点光源",
  3: "面光源",
};

export function lightTypeLabel(t: number): string {
  return LIGHT_TYPE_LABELS[t] ?? `类型${t}`;
}

/** 厨师头灯 Spot range（对齐 Unity Light.range，默认 10）。 */
export function clampChefHeadLightRange(v: number | undefined): number {
  const n = Number(v);
  if (!Number.isFinite(n) || n <= 0) return 10;
  return Math.max(0.01, Math.min(100, n));
}

/** 与 ChefDarkEnvironmentBakery.DayLightPath 一致。 */
export const CHEF_HEADLIGHT_DAY_PATH = "Art/Lights/day";
const TEMPLATE_DAY_INTENSITY = 0.7;
const DARK_DAY_INTENSITY = 0;

/** 厨师头灯开/关时同步布局 JSON 中的太阳光强度，避免写回被 0.7 覆盖暗关。 */
export function syncDayLightForChefHeadlights(enabled: boolean): void {
  const intensity = enabled ? DARK_DAY_INTENSITY : TEMPLATE_DAY_INTENSITY;
  const existing = S.lights.find((l) => l.hierarchyPath === CHEF_HEADLIGHT_DAY_PATH);
  if (existing) {
    existing.intensity = intensity;
    return;
  }
  S.lights.push({
    hierarchyPath: CHEF_HEADLIGHT_DAY_PATH,
    displayName: "day",
    lightType: 1,
    color: "#ffeed7",
    intensity,
    range: 10,
    spotAngle: 30,
    enabled: true,
    positionEdited: false,
    rotationEdited: false,
    shadows: 1,
    shadowStrength: 1,
    bounceIntensity: 1,
    innerSpotAngle: 21,
    lightmapping: 4,
  });
}

const ZERO: LayoutVector3 = { x: 0, y: 0, z: 0 };

/** 从 Unity 导出或旧 JSON 补齐缺省字段（不擅自 positionEdited）。 */
export function normalizeLight(raw: LightInfo): LightInfo {
  const pos = raw.position ?? ZERO;
  return {
    ...raw,
    position: { x: pos.x ?? 0, y: pos.y ?? 0, z: pos.z ?? 0 },
    eulerAngles: raw.eulerAngles ?? ZERO,
    shadows: raw.shadows ?? 0,
    shadowStrength: raw.shadowStrength ?? 1,
    bounceIntensity: raw.bounceIntensity ?? 1,
    innerSpotAngle: raw.innerSpotAngle ?? 21,
    lightmapping: raw.lightmapping ?? 2,
  };
}

export function normalizeLights(list: LightInfo[]): LightInfo[] {
  return list.map(normalizeLight);
}

export function defaultLightHeight(): number {
  const ch = S.ceilingHeight;
  if (S.hasCeilingHeight && ch > 0) return Math.round(ch * 0.85 * 100) / 100;
  return 6;
}

export function lightByPath(path: string | null): LightInfo | undefined {
  if (!path) return undefined;
  return S.lights.find((l) => l.hierarchyPath === path);
}

export function addNewLight(lightType: number, wx = 0, wz = 0): void {
  const used = new Set(S.lights.map((l) => l.hierarchyPath));
  let n = S.lights.length + 1;
  let path = `Art/Lights/light${n}`;
  while (used.has(path)) {
    n++;
    path = `Art/Lights/light${n}`;
  }
  pushHistory();
  const y = defaultLightHeight();
  S.lights.push(
    normalizeLight({
      hierarchyPath: path,
      displayName: `light${n}`,
      lightType,
      color: "#ffffff",
      intensity: 1,
      range: 10,
      spotAngle: 30,
      enabled: true,
      position: { x: wx, y, z: wz },
      positionEdited: true,
      eulerAngles: lightType === 1 ? { x: 50, y: 0, z: 0 } : { x: 0, y: 0, z: 0 },
      rotationEdited: lightType === 1 || lightType === 0,
      shadows: 0,
      shadowStrength: 1,
      bounceIntensity: 1,
      innerSpotAngle: 21,
      lightmapping: 2,
    })
  );
  S.selectedLightPath = path;
}

export function duplicateLight(src: LightInfo): void {
  const used = new Set(S.lights.map((l) => l.hierarchyPath));
  let n = S.lights.length + 1;
  let path = `Art/Lights/${src.displayName || "light"}_copy${n}`;
  while (used.has(path)) {
    n++;
    path = `Art/Lights/${src.displayName || "light"}_copy${n}`;
  }
  pushHistory();
  const pos = src.position ?? ZERO;
  S.lights.push(
    normalizeLight({
      ...src,
      hierarchyPath: path,
      displayName: `${src.displayName || "light"}_copy`,
      position: { x: pos.x + 0.5, y: pos.y, z: pos.z + 0.5 },
      positionEdited: true,
      rotationEdited: src.rotationEdited ?? false,
    })
  );
  S.selectedLightPath = path;
}
