import { S } from "./state";
import { addNewLight, lightTypeLabel } from "./lightsUtil";
import { draw } from "./render";
import { renderRightPanel } from "./panels";
import { setStatus } from "./status";

const LIGHT_TOOLS: { type: number; emoji: string; sub: string }[] = [
  { type: 2, emoji: "💡", sub: "画布点击放置" },
  { type: 1, emoji: "☀️", sub: "画布点击放置" },
  { type: 0, emoji: "🔦", sub: "画布点击放置" },
];

/** 灯光层左侧工具区：与目录 palette 同结构/主题 token。 */
export function buildLightPalette(): void {
  const cats = document.getElementById("palette-cats");
  if (!cats) return;
  const pending = S.pendingNewLightType;

  const toolCards = LIGHT_TOOLS
    .map(
      (t) => `
      <div
        class="palette-card palette-cat-lights palette-card--tool${pending === t.type ? " palette-card-armed" : ""}"
        data-light-type="${t.type}"
        role="button"
        tabindex="0"
      >
        <div class="zh">${t.emoji} ${lightTypeLabel(t.type)}</div>
        <div class="sub">${t.sub}</div>
      </div>`
    )
    .join("");

  cats.innerHTML = `
    <div class="light-palette">
      <p class="light-palette-hint">
        选一种灯后在画布点击放置。<kbd>Shift</kbd>+拖动可调高度 Y；也可在右侧改坐标。
      </p>
      <details class="cat-group" open>
        <summary>新建灯光</summary>
        <div class="palette-grid light-palette-grid">${toolCards}</div>
      </details>
      <details class="cat-group" open>
        <summary>快捷</summary>
        <div class="palette-grid light-palette-grid">
          <div class="palette-card palette-cat-other palette-card--tool" id="light-add-origin" role="button" tabindex="0">
            <div class="zh">📍 原地点光</div>
            <div class="sub">落在 (0, 0) 附近</div>
          </div>
        </div>
      </details>
    </div>`;

  const arm = (type: number) => {
    S.pendingNewLightType = type;
    setStatus(`已选 ${lightTypeLabel(type)}：在画布点击放置`, true);
    buildLightPalette();
  };

  cats.querySelectorAll<HTMLElement>("[data-light-type]").forEach((el) => {
    const t = Number(el.dataset.lightType);
    const onActivate = () => {
      if (!Number.isFinite(t)) return;
      arm(t);
    };
    el.addEventListener("click", onActivate);
    el.addEventListener("keydown", (e) => {
      if (e.key === "Enter" || e.key === " ") {
        e.preventDefault();
        onActivate();
      }
    });
  });

  const origin = cats.querySelector("#light-add-origin");
  const placeOrigin = () => {
    addNewLight(2, 0, 0);
    S.pendingNewLightType = null;
    renderRightPanel();
    draw();
    buildLightPalette();
  };
  origin?.addEventListener("click", placeOrigin);
  origin?.addEventListener("keydown", (e) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      placeOrigin();
    }
  });
}
