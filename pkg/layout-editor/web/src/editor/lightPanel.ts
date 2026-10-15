import { S } from "./state";
import { escHtml } from "./coords";
import { pushHistory } from "./historyOps";
import { draw } from "./render";
import { modalBtnHtml } from "../ui/views/button";
import {
  duplicateLight,
  lightByPath,
  lightTypeLabel,
  normalizeLight,
} from "./lightsUtil";
import type { LightInfo } from "../types";

const SHADOW_LABELS: Record<number, string> = {
  0: "无",
  1: "硬阴影",
  2: "软阴影",
};

const LIGHTMAP_LABELS: Record<number, string> = {
  0: "Realtime",
  1: "Baked",
  2: "Mixed",
  4: "Auto",
};

const LIGHT_TYPE_EMOJI: Record<number, string> = {
  0: "🔦",
  1: "☀️",
  2: "💡",
  3: "▭",
};

function clampNum(v: number, min: number, max: number): number {
  const n = Number(v);
  if (!Number.isFinite(n)) return min;
  return Math.min(max, Math.max(min, n));
}

function ensurePos(l: LightInfo): void {
  if (!l.position) l.position = { x: 0, y: 0, z: 0 };
}

function bindLightForm(root: HTMLElement, path: string): void {
  const light = () => lightByPath(path);
  if (!light()) return;

  const numChange = (sel: string, apply: (l: LightInfo, v: number) => void) => {
    root.querySelector<HTMLInputElement>(sel)?.addEventListener("change", (e) => {
      const l = light();
      if (!l) return;
      pushHistory();
      apply(l, Number((e.target as HTMLInputElement).value));
      draw();
    });
  };

  root.querySelector<HTMLInputElement>("[data-lp-enable]")?.addEventListener("change", (e) => {
    const l = light();
    if (!l) return;
    pushHistory();
    l.enabled = (e.target as HTMLInputElement).checked;
    draw();
    renderLightPanel(document.getElementById("scene-items-body")!);
  });

  root.querySelector<HTMLSelectElement>("[data-lp-type]")?.addEventListener("change", (e) => {
    const l = light();
    if (!l) return;
    pushHistory();
    l.lightType = Number((e.target as HTMLSelectElement).value);
    draw();
    renderLightPanel(document.getElementById("scene-items-body")!);
  });

  root.querySelector<HTMLInputElement>("[data-lp-color]")?.addEventListener("input", (e) => {
    const l = light();
    if (!l) return;
    if (root.dataset.colorPushed !== "1") {
      pushHistory();
      root.dataset.colorPushed = "1";
    }
    l.color = (e.target as HTMLInputElement).value;
    draw();
  });
  root.querySelector<HTMLInputElement>("[data-lp-color]")?.addEventListener("change", () => {
    root.dataset.colorPushed = "";
  });

  ["x", "y", "z"].forEach((axis) => {
    numChange(`[data-lp-pos-${axis}]`, (l, v) => {
      ensurePos(l);
      if (!isFinite(v)) return;
      l.position![axis as "x" | "y" | "z"] = Math.round(v * 1000) / 1000;
      l.positionEdited = true;
    });
  });
  ["x", "y", "z"].forEach((axis) => {
    numChange(`[data-lp-rot-${axis}]`, (l, v) => {
      if (!l.eulerAngles) l.eulerAngles = { x: 0, y: 0, z: 0 };
      if (!isFinite(v)) return;
      l.eulerAngles[axis as "x" | "y" | "z"] = v;
      l.rotationEdited = true;
    });
  });

  numChange("[data-lp-intensity]", (l, v) => {
    l.intensity = Math.max(0, v);
  });
  numChange("[data-lp-range]", (l, v) => {
    l.range = Math.max(0.01, v);
  });
  numChange("[data-lp-spot]", (l, v) => {
    l.spotAngle = clampNum(v, 1, 179);
  });
  numChange("[data-lp-inner]", (l, v) => {
    l.innerSpotAngle = clampNum(v, 0, l.spotAngle);
  });
  numChange("[data-lp-shadow-str]", (l, v) => {
    l.shadowStrength = clampNum(v, 0, 1);
  });
  numChange("[data-lp-bounce]", (l, v) => {
    l.bounceIntensity = Math.max(0, v);
  });

  root.querySelector<HTMLSelectElement>("[data-lp-shadows]")?.addEventListener("change", (e) => {
    const l = light();
    if (!l) return;
    pushHistory();
    l.shadows = Number((e.target as HTMLSelectElement).value);
    draw();
  });
  root.querySelector<HTMLSelectElement>("[data-lp-lightmap]")?.addEventListener("change", (e) => {
    const l = light();
    if (!l) return;
    pushHistory();
    l.lightmapping = Number((e.target as HTMLSelectElement).value);
    draw();
  });

  root.querySelector<HTMLButtonElement>("[data-lp-del]")?.addEventListener("click", () => {
    const idx = S.lights.findIndex((x) => x.hierarchyPath === path);
    if (idx < 0) return;
    pushHistory();
    S.lights.splice(idx, 1);
    S.selectedLightPath = S.lights[0]?.hierarchyPath ?? null;
    renderLightPanel(document.getElementById("scene-items-body")!);
    draw();
  });
  root.querySelector<HTMLButtonElement>("[data-lp-dup]")?.addEventListener("click", () => {
    const l = light();
    if (!l) return;
    duplicateLight(l);
    renderLightPanel(document.getElementById("scene-items-body")!);
    draw();
  });
}

function mField(label: string, inner: string): string {
  return `<label class="m-field">${label}${inner}</label>`;
}

function detailHtml(l: LightInfo): string {
  const isSpot = l.lightType === 0;
  const isPoint = l.lightType === 2;
  const showRot = l.lightType === 0 || l.lightType === 1;
  const typeSel = [0, 1, 2]
    .map(
      (t) => `<option value="${t}" ${l.lightType === t ? "selected" : ""}>${lightTypeLabel(t)}</option>`
    )
    .join("");
  const shadowSel = [0, 1, 2]
    .map(
      (t) =>
        `<option value="${t}" ${(l.shadows ?? 0) === t ? "selected" : ""}>${SHADOW_LABELS[t] ?? t}</option>`
    )
    .join("");
  const lmSel = [0, 1, 2, 4]
    .map(
      (t) =>
        `<option value="${t}" ${(l.lightmapping ?? 2) === t ? "selected" : ""}>${LIGHTMAP_LABELS[t] ?? t}</option>`
    )
    .join("");
  const p = l.position ?? { x: 0, y: 0, z: 0 };
  const e = l.eulerAngles ?? { x: 0, y: 0, z: 0 };
  const colorVal = l.color?.match(/^#[0-9a-fA-F]{6}$/) ? l.color : "#ffffff";

  const rotFields = showRot
    ? `
      ${mField("俯仰 (°)", `<input type="number" data-lp-rot-x step="1" value="${e.x}" />`)}
      ${mField("朝向 (°)", `<input type="number" data-lp-rot-y step="1" value="${e.y}" />`)}
      ${mField("滚转 (°)", `<input type="number" data-lp-rot-z step="1" value="${e.z}" />`)}
    `
    : "";

  const rangeFields =
    isSpot || isPoint
      ? mField("范围", `<input type="number" data-lp-range min="0.01" step="0.5" value="${l.range}" />`)
      : "";
  const spotFields = isSpot
    ? mField("外角 (°)", `<input type="number" data-lp-spot min="1" max="179" step="1" value="${l.spotAngle}" />`) +
      mField("内角 (°)", `<input type="number" data-lp-inner min="0" max="179" step="1" value="${l.innerSpotAngle ?? 21}" />`)
    : "";

  return `
    <div class="light-panel-detail cr-section" data-lp-path="${escHtml(l.hierarchyPath)}">
      <div class="m-section-title">${escHtml(l.displayName || l.hierarchyPath)}</div>
      <div class="light-panel-path">${escHtml(l.hierarchyPath)}</div>
      <label class="m-field light-field-check">
        <span>启用</span>
        <input type="checkbox" data-lp-enable ${l.enabled ? "checked" : ""} />
      </label>

      <details class="cat-group light-form-section" open>
        <summary>变换</summary>
        <p class="light-palette-hint">画布拖动 XZ；<kbd>Shift</kbd>+拖动调高度 Y。</p>
        <div class="part-form light-form-grid">
          ${mField("位置 X", `<input type="number" data-lp-pos-x step="0.1" value="${p.x}" />`)}
          ${mField("位置 Y", `<input type="number" data-lp-pos-y step="0.1" value="${p.y}" />`)}
          ${mField("位置 Z", `<input type="number" data-lp-pos-z step="0.1" value="${p.z}" />`)}
          ${rotFields}
        </div>
      </details>

      <details class="cat-group light-form-section" open>
        <summary>光照</summary>
        <div class="part-form light-form-grid">
          ${mField("类型", `<select data-lp-type>${typeSel}</select>`)}
          ${mField("颜色", `<input type="color" data-lp-color value="${colorVal}" />`)}
          ${mField("强度", `<input type="number" data-lp-intensity min="0" step="0.05" value="${l.intensity}" />`)}
          ${rangeFields}
          ${spotFields}
        </div>
      </details>

      <details class="cat-group light-form-section" open>
        <summary>阴影与烘焙</summary>
        <div class="part-form light-form-grid">
          ${mField("阴影", `<select data-lp-shadows>${shadowSel}</select>`)}
          ${mField("阴影强度", `<input type="number" data-lp-shadow-str min="0" max="1" step="0.05" value="${l.shadowStrength ?? 1}" />`)}
          ${mField("Bounce", `<input type="number" data-lp-bounce min="0" step="0.1" value="${l.bounceIntensity ?? 1}" />`)}
          ${mField("Lightmapping", `<select data-lp-lightmap>${lmSel}</select>`)}
        </div>
      </details>

      <div class="m-actions-row light-panel-actions">
        ${modalBtnHtml("复制", "btn-small", { "data-lp-dup": "", type: "button" })}
        ${modalBtnHtml("删除", "btn-small", { "data-lp-del": "", type: "button" })}
      </div>
    </div>`;
}

export function renderLightPanel(body: HTMLElement): void {
  const sel = S.selectedLightPath;
  const n = S.lights.length;

  const rows = S.lights
    .map((l) => {
      const active = l.hierarchyPath === sel;
      const p = l.position;
      const posHint = p
        ? `(${Math.round(p.x * 10) / 10}, ${Math.round(p.y * 10) / 10}, ${Math.round(p.z * 10) / 10})`
        : "";
      const icon = l.enabled ? LIGHT_TYPE_EMOJI[l.lightType] ?? "💡" : "○";
      return (
        `<div class="scene-item-row${active ? " active" : ""}" data-light-path="${escHtml(l.hierarchyPath)}">` +
        `<span class="zh">${icon} ${escHtml(l.displayName || l.hierarchyPath)}</span> ` +
        `<span class="id">${escHtml(lightTypeLabel(l.lightType))}${posHint ? " · " + escHtml(posHint) : ""}</span>` +
        `</div>`
      );
    })
    .join("");

  const listBlock =
    n === 0
      ? `<div class="light-panel-empty muted">暂无灯光。在左侧选类型后于画布点击放置。</div>`
      : rows;

  body.innerHTML = `
    <div class="light-panel">
      <p class="light-palette-hint">Art/Lights 非 prefab 灯光；全量写回时同步到 Unity。</p>
      <details class="cat-group" open>
        <summary>💡 场景灯光（${n}）</summary>
        ${listBlock}
      </details>
      ${sel && lightByPath(sel) ? detailHtml(normalizeLight(lightByPath(sel)!)) : ""}
    </div>`;

  body.querySelectorAll<HTMLElement>(".scene-item-row[data-light-path]").forEach((row) => {
    row.addEventListener("click", () => {
      S.selectedLightPath = row.dataset.lightPath ?? null;
      renderLightPanel(body);
      draw();
    });
  });

  const detail = body.querySelector<HTMLElement>(".light-panel-detail");
  if (detail && sel) bindLightForm(detail, sel);
}
