import "./style.css";
import {
  fetchFloorMaterials,
  fetchGrid,
  fetchHealth,
  fetchHealthInfo,
  fetchIngredients,
  STALE_BRIDGE_MSG,
  fetchLayout,
  fetchLevelRecipes,
  fetchLevelSets,
  fetchRecipeCatalog,
  loadCatalog,
  saveLayout,
  saveLevelRecipes,
} from "./api";
import {
  closeModal,
  openFoodSpawnerEditor,
  openIngredientPicker,
  openModal,
  openRecipePicker,
} from "./modals";
import { ingredientNameZh } from "./ingredientLabels";
import { tidyCatalogNameZh } from "./displayLabels";
import { paintStyleForItem } from "./itemColors";
import {
  deathLabelZh,
  isSurfaceItem,
  materialBilingual,
  surfaceKindLabelZh,
  surfacePaint,
  voidFill,
} from "./floorColors";
import { snapFootprintCenter, snapValue } from "./snap";
import {
  drawLayerForItem,
  findStackHost,
  hostRuleLabelZh,
  isStackUtensilCatalog,
  trySnapUtensilToHost,
} from "./stacking";
import type {
  CatalogItem,
  DeathInfo,
  FloorMaterial,
  FloorObject,
  GridInfo,
  LayoutItem,
  LayoutDocument,
  WalkableRect,
} from "./types";

const CELL = 1.2;
const PX_PER_UNIT = 48;

const FOOTPRINT_BY_ID: Record<string, { cellsX: number; cellsZ: number }> = {
  ServingStation: { cellsX: 2, cellsZ: 1 },
  Sink: { cellsX: 2, cellsZ: 1 },
};

type EditorItem = LayoutItem & {
  _editorKey: string;
  _wx: number;
  _wz: number;
  _parentWx: number;
  _parentWz: number;
};

type EditorFloor = FloorObject & {
  _key: string;
  _wx: number;
  _wz: number;
  _wCells: number;
  _dCells: number;
};

let catalogByGuid = new Map<string, CatalogItem>();
let items: EditorItem[] = [];
let floors: EditorFloor[] = [];
let walkable: WalkableRect[] = [];
let deathInfo: DeathInfo | null = null;
let floorMaterials: FloorMaterial[] = [];
let selectedKey: string | null = null;
let selectedFloorKey: string | null = null;
let currentLayer: "items" | "floor" = "items";
let scenePath = "";
let snapStep = 0.6;
let showGrid = true;
let gridInfo: GridInfo | null = null;

let scale = 1;
let panX = 0;
let panY = 0;

let dragCatalog: CatalogItem | null = null;
let dragItemKey: string | null = null;
let dragOffsetX = 0;
let dragOffsetZ = 0;
let spaceHeld = false;
let panning = false;
let lastMx = 0;
let lastMy = 0;

let dragFloorKey: string | null = null;
let dragFloorMode: "move" | "resize" = "move";
let dragFloorEdge: string = "";
let dragFloorAnchorX = 0;
let dragFloorAnchorZ = 0;

let ingredientsCache: import("./types").IngredientEntry[] = [];
let currentLevelSet = "";

function levelSetFromScenePath(assetPath: string): string {
  const parts = assetPath.replace(/\\/g, "/").split("/");
  const i = parts.indexOf("LevelSets");
  return i >= 0 && parts.length > i + 1 ? parts[i + 1] : "";
}

function openStubEditorForItem(item: EditorItem) {
  const prefabId = prefabIdFromPath(item.prefabAssetPath);
  const kind = item.stubKind || (prefabId === "Dispenser" ? "Dispenser" : prefabId === "AttachingFoodSpawner" ? "AttachingFoodSpawner" : "");

  if (kind === "Dispenser") {
    openIngredientPicker(
      ingredientsCache,
      item.dispenser?.spawnerItemPrefabGuid,
      (guid) => {
        item.stubKind = "Dispenser";
        item.dispenser = { spawnerItemPrefabGuid: guid };
        draw();
        setStatus("已设置食材箱食材（写回后生效）");
      }
    );
    return;
  }

  if (kind === "AttachingFoodSpawner") {
    item.stubKind = "AttachingFoodSpawner";
    if (!item.foodSpawner) item.foodSpawner = {};
    openFoodSpawnerEditor(item, ingredientsCache, (patch) => {
      item.foodSpawner = patch;
      draw();
      setStatus("已更新食材生成器参数");
    });
  }
}

async function openRecipesDialog() {
  if (!scenePath) {
    setStatus("请先选择场景", false);
    return;
  }
  const health = await fetchHealthInfo();
  if (!health.recipeApi) {
    setStatus(STALE_BRIDGE_MSG, false);
    return;
  }
  try {
    const [recipes, level] = await Promise.all([
      fetchRecipeCatalog(currentLevelSet),
      fetchLevelRecipes(scenePath),
    ]);
    if (!level.levelInfoAssetPath) {
      setStatus("未找到该场景对应的 LevelInfoSO", false);
      return;
    }
    openRecipePicker(recipes, level.recipeGuids ?? [], level.levelName, async (guids) => {
      await saveLevelRecipes(level.levelInfoAssetPath, guids);
      setStatus("菜谱已写入 LevelInfo（请在 Unity 保存资源）");
    });
  } catch (e) {
    setStatus((e as Error).message, false);
  }
}

const app = document.getElementById("app")!;
app.innerHTML = `
  <div class="toolbar">
    <label>场景</label>
    <select id="scene-select"><option value="">加载中…</option></select>
    <button id="btn-reload">重新加载</button>
    <button id="btn-save" class="primary">写回 Unity</button>
    <button id="btn-recipes" type="button">菜谱…</button>
    <div class="layer-tabs" id="layer-tabs">
      <button type="button" data-layer="items" class="layer-tab active">📦 物品层</button>
      <button type="button" data-layer="floor" class="layer-tab">🗺️ 地板 / 背景层</button>
    </div>
    <label><input type="checkbox" id="snap-half" checked /> 半格 (0.6)</label>
    <label><input type="checkbox" id="show-grid" checked /> 显示网格</label>
    <span id="status" class="status">连接中…</span>
  </div>
  <div class="main">
    <aside class="palette">
      <div class="palette-header">
        <input type="search" id="palette-search" placeholder="搜索 prefab…" />
      </div>
      <div class="palette-cats" id="palette-cats"></div>
    </aside>
    <div class="canvas-wrap">
      <canvas id="canvas"></canvas>
      <div id="item-detail" class="item-detail hidden" role="dialog"></div>
      <div id="floor-bar" class="floor-bar hidden"></div>
      <div class="hint">空格+拖动平移 · 右键详情 · Del 删除 · R 旋转 · 滚轮缩放</div>
    </div>
  </div>
`;

const sceneSelect = document.getElementById("scene-select") as HTMLSelectElement;
const statusEl = document.getElementById("status")!;
const paletteCats = document.getElementById("palette-cats")!;
const canvas = document.getElementById("canvas") as HTMLCanvasElement;
const ctx = canvas.getContext("2d")!;
const detailEl = document.getElementById("item-detail")!;
const floorBar = document.getElementById("floor-bar")!;

function setStatus(text: string, ok = true) {
  statusEl.textContent = text;
  statusEl.className = "status " + (ok ? "ok" : "err");
}

function normalizeRot(deg: number): number {
  return ((deg % 360) + 360) % 360;
}

function prefabIdFromPath(assetPath: string | undefined): string {
  if (!assetPath) return "";
  const name = assetPath.split("/").pop() ?? "";
  return name.replace(/\.prefab$/i, "");
}

function resolveFootprint(item: LayoutItem): { cellsX: number; cellsZ: number } {
  const cx = item.footprint?.cellsX ?? 0;
  const cz = item.footprint?.cellsZ ?? 0;
  if (cx > 0 && cz > 0) return { cellsX: cx, cellsZ: cz };

  const cat = catalogByGuid.get(item.prefabGuid);
  if (cat?.footprint && cat.footprint.cellsX > 0 && cat.footprint.cellsZ > 0) {
    return { cellsX: cat.footprint.cellsX, cellsZ: cat.footprint.cellsZ };
  }

  const id = prefabIdFromPath(item.prefabAssetPath) || cat?.id || "";
  const known = FOOTPRINT_BY_ID[id];
  if (known) return known;

  return { cellsX: 1, cellsZ: 1 };
}

function worldToCanvas(wx: number, wz: number): { x: number; y: number } {
  const cx = canvas.width / 2 + panX + wx * PX_PER_UNIT * scale;
  const cy = canvas.height / 2 + panY - wz * PX_PER_UNIT * scale;
  return { x: cx, y: cy };
}

function canvasToWorld(cx: number, cy: number): { x: number; z: number } {
  const wx = (cx - canvas.width / 2 - panX) / (PX_PER_UNIT * scale);
  const wz = -(cy - canvas.height / 2 - panY) / (PX_PER_UNIT * scale);
  return { x: wx, z: wz };
}

function newEditorKey(): string {
  return crypto.randomUUID();
}

function enrichItem(raw: LayoutItem, editorKey: string): EditorItem {
  const wp = raw.worldPosition ?? raw.localPosition;
  const fp = resolveFootprint(raw);
  return {
    ...raw,
    _editorKey: editorKey,
    footprint: fp,
    _wx: wp.x,
    _wz: wp.z,
    _parentWx: wp.x - raw.localPosition.x,
    _parentWz: wp.z - raw.localPosition.z,
  };
}

function enrichFloor(raw: FloorObject, key: string): EditorFloor {
  const wp = raw.worldPosition ?? raw.localPosition;
  const w = raw.widthCells > 0 ? raw.widthCells : Math.max(1, Math.round(raw.widthUnits / CELL));
  const d = raw.depthCells > 0 ? raw.depthCells : Math.max(1, Math.round(raw.depthUnits / CELL));
  return {
    ...raw,
    _key: key,
    _wx: wp.x,
    _wz: wp.z,
    _wCells: w,
    _dCells: d,
  };
}

function itemLabel(item: EditorItem): string {
  const id = prefabIdFromPath(item.prefabAssetPath);
  const isDispenser = item.stubKind === "Dispenser" || id === "Dispenser";
  if (isDispenser) {
    const ingZh = ingredientNameZh(ingredientsCache, item.dispenser?.spawnerItemPrefabGuid);
    if (ingZh !== "未设置") return ingZh;
  }

  const cat = catalogByGuid.get(item.prefabGuid);
  if (cat?.nameZh) return tidyCatalogNameZh(cat.nameZh);
  if (item.displayName) return tidyCatalogNameZh(item.displayName);
  return id || "?";
}

function updateCanvasCursor() {
  canvas.classList.remove("pan-ready", "pan-active");
  if (panning) canvas.classList.add("pan-active");
  else if (spaceHeld) canvas.classList.add("pan-ready");
}

function wrapTextLines(ctx: CanvasRenderingContext2D, text: string, maxWidth: number): string[] {
  if (maxWidth <= 4) return [text.slice(0, 1)];
  const chars = Array.from(text);
  const lines: string[] = [];
  let line = "";
  for (const ch of chars) {
    const test = line + ch;
    if (ctx.measureText(test).width > maxWidth && line.length > 0) {
      lines.push(line);
      line = ch;
    } else {
      line = test;
    }
  }
  if (line) lines.push(line);
  return lines.length ? lines : [""];
}

function drawLabelInBox(
  ctx: CanvasRenderingContext2D,
  text: string,
  boxW: number,
  boxH: number
) {
  const pad = 3;
  const innerW = Math.max(4, boxW - pad * 2);
  const innerH = Math.max(4, boxH - pad * 2);
  let fontSize = Math.max(8, Math.min(12, innerH * 0.32, 11 * scale));

  for (let attempt = 0; attempt < 6; attempt++) {
    ctx.font = `${fontSize}px system-ui, sans-serif`;
    const lines = wrapTextLines(ctx, text, innerW);
    const lineHeight = fontSize * 1.12;
    if (lines.length * lineHeight <= innerH || fontSize <= 8) {
      const startY = -((lines.length - 1) * lineHeight) / 2;
      for (let i = 0; i < lines.length; i++) {
        ctx.fillText(lines[i], 0, startY + i * lineHeight);
      }
      return;
    }
    fontSize -= 1;
  }
}

function draw() {
  const w = canvas.clientWidth;
  const h = canvas.clientHeight;
  if (canvas.width !== w || canvas.height !== h) {
    canvas.width = w;
    canvas.height = h;
  }

  const onFloor = currentLayer === "floor";
  ctx.fillStyle = onFloor ? voidFill(deathInfo?.deathType) : "#1a1d23";
  ctx.fillRect(0, 0, w, h);

  if (showGrid) drawGrid();

  if (onFloor) {
    drawWalkable();
    drawFloorPlanes(false);
    drawFloorAdjacentSeams();
    drawFloorPlanes(true);
    drawSurfaceItems(false);
  } else {
    const sorted = [...items].sort(
      (a, b) => drawLayerForItem(a, catalogByGuid) - drawLayerForItem(b, catalogByGuid)
    );
    for (const item of sorted) {
      const selected = item._editorKey === selectedKey;
      drawItem(item, selected);
    }
  }

  updateFloorBar();
}

function drawWalkable() {
  for (const r of walkable) {
    const a = worldToCanvas(r.cx - r.sx / 2, r.cz + r.sz / 2);
    const b = worldToCanvas(r.cx + r.sx / 2, r.cz - r.sz / 2);
    const x = Math.min(a.x, b.x);
    const y = Math.min(a.y, b.y);
    const bw = Math.abs(b.x - a.x);
    const bh = Math.abs(b.y - a.y);
    const ice = r.surfaceType === "ice";
    ctx.fillStyle = ice ? "rgba(120,200,235,0.16)" : "rgba(150,140,120,0.14)";
    ctx.fillRect(x, y, bw, bh);
    ctx.strokeStyle = ice ? "rgba(90,170,210,0.4)" : "rgba(160,150,130,0.3)";
    ctx.setLineDash([4, 3]);
    ctx.lineWidth = 1;
    ctx.strokeRect(x, y, bw, bh);
    ctx.setLineDash([]);
  }
}

function drawSurfaceItems(highlight: boolean) {
  const sorted = [...items]
    .filter((it) => isSurfaceItem(catalogByGuid.get(it.prefabGuid)))
    .sort((a, b) => drawLayerForItem(a, catalogByGuid) - drawLayerForItem(b, catalogByGuid));
  for (const item of sorted) {
    const selected = highlight && item._editorKey === selectedKey;
    drawItem(item, selected);
  }
}

function drawFloorPlanes(highlight: boolean) {
  for (const f of floors) {
    const selected = highlight && f._key === selectedFloorKey;
    drawFloorPlane(f, selected, false);
  }
}

function floorRectPx(f: EditorFloor) {
  const center = worldToCanvas(f._wx, f._wz);
  const cellPx = CELL * PX_PER_UNIT * scale;
  const bw = f._wCells * cellPx;
  const bh = f._dCells * cellPx;
  const rot = normalizeRot(f.localRotationY);
  return { center, bw, bh, rot };
}

function drawFloorPlane(f: EditorFloor, selected: boolean, ghost: boolean) {
  const { center, bw, bh, rot } = floorRectPx(f);
  const paint = surfacePaint(f.surfaceKind, selected);

  ctx.save();
  ctx.translate(center.x, center.y);
  ctx.rotate((-rot * Math.PI) / 180);

  ctx.fillStyle = paint.fill;
  ctx.fillRect(-bw / 2, -bh / 2, bw, bh);

  // Dashed outer border to convey the floor surface.
  ctx.strokeStyle = paint.stroke;
  ctx.lineWidth = selected ? 2.5 : ghost ? 1 : 1.5;
  ctx.setLineDash(selected ? [] : [7, 4]);
  ctx.strokeRect(-bw / 2, -bh / 2, bw, bh);
  ctx.setLineDash([]);

  // Faint dashed internal cell grid → "perspective" tiling.
  const cellPx = CELL * PX_PER_UNIT * scale;
  if (!ghost && cellPx > 6) {
    ctx.strokeStyle = "rgba(255,255,255,0.10)";
    ctx.lineWidth = 1;
    ctx.setLineDash([3, 4]);
    for (let i = 1; i < f._wCells; i++) {
      const x = -bw / 2 + i * cellPx;
      ctx.beginPath();
      ctx.moveTo(x, -bh / 2);
      ctx.lineTo(x, bh / 2);
      ctx.stroke();
    }
    for (let j = 1; j < f._dCells; j++) {
      const y = -bh / 2 + j * cellPx;
      ctx.beginPath();
      ctx.moveTo(-bw / 2, y);
      ctx.lineTo(bw / 2, y);
      ctx.stroke();
    }
    ctx.setLineDash([]);
  }

  // Resize handles when selected.
  if (selected && !ghost) {
    ctx.fillStyle = "#f9ab00";
    for (const hx of [-bw / 2, bw / 2]) {
      for (const hy of [-bh / 2, bh / 2]) {
        ctx.fillRect(hx - 3, hy - 3, 6, 6);
      }
    }
  }

  // Label: size in cells + kind emoji.
  ctx.beginPath();
  ctx.rect(-bw / 2 + 2, -bh / 2 + 2, bw - 4, bh - 4);
  ctx.clip();
  ctx.fillStyle = paint.label;
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  const emoji = paint.emoji ? paint.emoji + " " : "";
  drawLabelInBox(ctx, `${emoji}${f._wCells}×${f._dCells}格`, bw - 6, bh - 6);
  ctx.restore();
}

/** Draw a brighter dashed seam wherever two floor rectangles share an edge. */
function drawFloorAdjacentSeams() {
  if (floors.length < 2) return;
  const tol = CELL * 0.35;
  for (let i = 0; i < floors.length; i++) {
    for (let j = i + 1; j < floors.length; j++) {
      const a = floors[i];
      const b = floors[j];
      const aL = a._wx - (a._wCells * CELL) / 2;
      const aR = a._wx + (a._wCells * CELL) / 2;
      const aT = a._wz + (a._dCells * CELL) / 2;
      const aB = a._wz - (a._dCells * CELL) / 2;
      const bL = b._wx - (b._wCells * CELL) / 2;
      const bR = b._wx + (b._wCells * CELL) / 2;
      const bT = b._wz + (b._dCells * CELL) / 2;
      const bB = b._wz - (b._dCells * CELL) / 2;

      // Vertical shared edge (left/right touch, z ranges overlap).
      const zLo = Math.max(aB, bB);
      const zHi = Math.min(aT, bT);
      if (zHi - zLo > tol) {
        if (Math.abs(aR - bL) <= tol) drawSeam(aR, zLo, aR, zHi);
        else if (Math.abs(aL - bR) <= tol) drawSeam(aL, zLo, aL, zHi);
      }
      // Horizontal shared edge (top/bottom touch, x ranges overlap).
      const xLo = Math.max(aL, bL);
      const xHi = Math.min(aR, bR);
      if (xHi - xLo > tol) {
        if (Math.abs(aT - bB) <= tol) drawSeam(xLo, aT, xHi, aT);
        else if (Math.abs(aB - bT) <= tol) drawSeam(xLo, aB, xHi, aB);
      }
    }
  }
  ctx.setLineDash([]);
}

function drawSeam(wx1: number, wz1: number, wx2: number, wz2: number) {
  const p1 = worldToCanvas(wx1, wz1);
  const p2 = worldToCanvas(wx2, wz2);
  ctx.strokeStyle = "rgba(255,235,170,0.5)";
  ctx.lineWidth = 1.5;
  ctx.setLineDash([5, 4]);
  ctx.beginPath();
  ctx.moveTo(p1.x, p1.y);
  ctx.lineTo(p2.x, p2.y);
  ctx.stroke();
}

function floorLocalPoint(f: EditorFloor, wx: number, wz: number): { lx: number; lz: number } {
  const dx = wx - f._wx;
  const dz = wz - f._wz;
  const rad = (normalizeRot(f.localRotationY) * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  return { lx: dx * cos + dz * sin, lz: -dx * sin + dz * cos };
}

function hitTestFloor(wx: number, wz: number): {
  floor: EditorFloor;
  mode: "move" | "resize";
  edge: string;
  anchorX: number;
  anchorZ: number;
} | null {
  for (let i = floors.length - 1; i >= 0; i--) {
    const f = floors[i];
    const hw = (f._wCells * CELL) / 2;
    const hh = (f._dCells * CELL) / 2;
    const { lx, lz } = floorLocalPoint(f, wx, wz);
    if (Math.abs(lx) > hw || Math.abs(lz) > hh) continue;

    // Corner handle hit → resize.
    const handleTol = Math.max(CELL * 0.5, 0.9);
    const nearLeft = lx < -hw + handleTol;
    const nearRight = lx > hw - handleTol;
    const nearBottom = lz < -hh + handleTol;
    const nearTop = lz > hh - handleTol;
    if ((nearLeft || nearRight) && (nearBottom || nearTop)) {
      const edge = `${nearRight ? "R" : "L"}${nearTop ? "T" : "B"}`;
      // Anchor = opposite corner in world space.
      const ax = nearRight ? -hw : hw;
      const az = nearTop ? -hh : hh;
      const rad = (normalizeRot(f.localRotationY) * Math.PI) / 180;
      const cos = Math.cos(rad);
      const sin = Math.sin(rad);
      const anchorX = f._wx + ax * cos - az * sin;
      const anchorZ = f._wz + ax * sin + az * cos;
      return { floor: f, mode: "resize", edge, anchorX, anchorZ };
    }
    return { floor: f, mode: "move", edge: "", anchorX: 0, anchorZ: 0 };
  }
  return null;
}

function dragFloor(f: EditorFloor, mx: number, my: number) {
  const { x: wx, z: wz } = canvasToWorld(mx, my);
  if (dragFloorMode === "move") {
    f._wx = snapValue(wx, snapStep);
    f._wz = snapValue(wz, snapStep);
  } else {
    // Resize: opposite corner (anchor) stays fixed; compute new width/depth.
    const rad = (normalizeRot(f.localRotationY) * Math.PI) / 180;
    const cos = Math.cos(rad);
    const sin = Math.sin(rad);
    const dx = wx - dragFloorAnchorX;
    const dz = wz - dragFloorAnchorZ;
    const lx = dx * cos + dz * sin;
    const lz = -dx * sin + dz * cos;
    const newW = Math.max(1, Math.round((Math.abs(lx) / CELL) || 1));
    const newD = Math.max(1, Math.round((Math.abs(lz) / CELL) || 1));
    // Recenter so the anchor corner stays put.
    const signX = dragFloorEdge.includes("R") ? 1 : -1;
    const signZ = dragFloorEdge.includes("T") ? 1 : -1;
    const newCxLocal = signX * (newW * CELL) / 2;
    const newCzLocal = signZ * (newD * CELL) / 2;
    f._wx = dragFloorAnchorX + newCxLocal * cos - newCzLocal * sin;
    f._wz = dragFloorAnchorZ + newCxLocal * sin + newCzLocal * cos;
    f._wCells = newW;
    f._dCells = newD;
  }
}

function finalizeFloor(f: EditorFloor) {
  f._wx = snapValue(f._wx, snapStep);
  f._wz = snapValue(f._wz, snapStep);
  f.localPosition.x = f._wx;
  f.localPosition.z = f._wz;
  f.worldPosition.x = f._wx;
  f.worldPosition.z = f._wz;
  f.widthUnits = f._wCells * CELL;
  f.depthUnits = f._dCells * CELL;
  // Smart material match by size tag.
  if (f.surfaceKind !== "background") tryMatchFloorMaterialBySize(f);
}

function tryMatchFloorMaterialBySize(f: EditorFloor) {
  const tag = `${f._wCells}x${f._dCells}`;
  const match = floorMaterials.find((m) => m.sizeTag === tag);
  if (match && match.guid !== f.materialGuid) {
    f.materialGuid = match.guid;
    f.materialAssetPath = match.assetPath;
    f.materialName = match.id;
    setStatus(`已按尺寸 ${tag} 自动匹配材质：${match.nameZh}`);
  }
}

function pointInWalkable(wx: number, wz: number): boolean {
  for (const r of walkable) {
    if (Math.abs(wx - r.cx) <= r.sx / 2 + 0.01 && Math.abs(wz - r.cz) <= r.sz / 2 + 0.01)
      return true;
  }
  return false;
}

/** Returns a warning string if an item sits over a void/water zone, else null. */
function itemVoidWarning(item: EditorItem): string | null {
  if (walkable.length === 0) return null;
  if (pointInWalkable(item._wx, item._wz)) return null;
  const name = itemLabel(item);
  switch (deathInfo?.deathType) {
    case "water":
      return `⚠「${name}」位于水面/空洞上方（玩家会落水），已保留但请确认`;
    case "goo":
      return `⚠「${name}」位于黏液/空洞上方（玩家会坠入），已保留但请确认`;
    default:
      return `⚠「${name}」位于空洞上方（会坠落），已保留但请确认`;
  }
}

function warnItemVoid(item: EditorItem) {
  const w = itemVoidWarning(item);
  if (w) setStatus(w, false);
}

function addFloorAt(wx: number, wz: number) {  const snapped = snapValue(wx, snapStep);
  const snappedZ = snapValue(wz, snapStep);
  const id = `new:floor:${crypto.randomUUID()}`;
  const key = newEditorKey();
  const w = 4;
  const d = 4;
  const defaultMat = floorMaterials.find((m) => /floor|blacktiles|path/i.test(m.id));
  const floor: EditorFloor = {
    instanceId: id,
    _key: key,
    hierarchyPath: id,
    parentPath: "Art/Ground",
    displayName: "Floor",
    surfaceKind: "solid",
    meshType: "plane",
    meshFileId: 10209,
    materialGuid: defaultMat?.guid,
    materialAssetPath: defaultMat?.assetPath,
    materialName: defaultMat?.id,
    localPosition: { x: snapped, y: -0.05, z: snappedZ },
    worldPosition: { x: snapped, y: -0.05, z: snappedZ },
    localRotationY: 0,
    localScale: { x: (w * CELL) / 10, y: 1, z: (d * CELL) / 10 },
    widthUnits: w * CELL,
    depthUnits: d * CELL,
    widthCells: w,
    depthCells: d,
    _wx: snapped,
    _wz: snappedZ,
    _wCells: w,
    _dCells: d,
  };
  floors.push(floor);
  selectedFloorKey = key;
  draw();
  setStatus("已新增地板（写回后生效）");
}

function showFloorDetail(f: EditorFloor, clientX: number, clientY: number) {
  const matchedMat = floorMaterials.find((m) => m.guid === f.materialGuid);
  const areaCells = f._wCells * f._dCells;
  const matRows = floorMaterials
    .map((m) => {
      const bl = materialBilingual(m.id);
      return `<button type="button" class="mat-pick${m.guid === f.materialGuid ? " active" : ""}" data-guid="${m.guid}"><span class="mat-id">${bl.zh}</span><span class="mat-sub">${bl.en}</span></button>`;
    })
    .join("");
  detailEl.innerHTML = `
    <h3>${surfaceKindLabelZh(f.surfaceKind)} · ${f.displayName}</h3>
    <dl>
      <dt>类型</dt><dd>${surfaceKindLabelZh(f.surfaceKind)}${f.meshType === "plane" ? "（Plane 平面）" : f.meshType === "quad" ? "（Quad）" : ""}</dd>
      <dt>尺寸</dt><dd>${f._wCells} × ${f._dCells} 格 (${(f._wCells * CELL).toFixed(1)} × ${(f._dCells * CELL).toFixed(1)} m，${areaCells} 格)</dd>
      <dt>材质</dt><dd>${matchedMat?.nameZh ?? f.materialName ?? "无"}</dd>
      <dt>坐标</dt><dd>x ${f._wx.toFixed(2)}, z ${f._wz.toFixed(2)}</dd>
      <dt>旋转</dt><dd>${normalizeRot(f.localRotationY)}°</dd>
      <dt>死亡类型</dt><dd>${deathLabelZh(deathInfo)}（只读）</dd>
    </dl>
    <div class="floor-edit-row">
      <label>宽(格) <input type="number" min="1" id="fe-w" value="${f._wCells}" /></label>
      <label>高(格) <input type="number" min="1" id="fe-d" value="${f._dCells}" /></label>
      <button id="fe-apply">应用尺寸</button>
    </div>
    <div class="mat-pick-title">切换材质（点击应用）</div>
    <div class="mat-pick-list">${matRows || '<div class="mat-pick-empty">当前关卡集无材质</div>'}</div>
    <p class="close-hint">左键拖动移动 · 拖角点缩放 · 右键此面板切换材质 · Esc 关闭</p>
  `;
  detailEl.classList.remove("hidden");
  positionDetail(clientX, clientY);

  document.getElementById("fe-apply")!.addEventListener("click", () => {
    const wv = parseInt((document.getElementById("fe-w") as HTMLInputElement).value, 10);
    const dv = parseInt((document.getElementById("fe-d") as HTMLInputElement).value, 10);
    if (wv > 0) f._wCells = wv;
    if (dv > 0) f._dCells = dv;
    finalizeFloor(f);
    draw();
    showFloorDetail(f, clientX, clientY);
  });

  detailEl.querySelectorAll<HTMLButtonElement>(".mat-pick").forEach((btn) => {
    btn.addEventListener("click", () => {
      const m = floorMaterials.find((x) => x.guid === btn.dataset.guid);
      if (!m) return;
      f.materialGuid = m.guid;
      f.materialAssetPath = m.assetPath;
      f.materialName = m.id;
      draw();
      setStatus(`已切换材质：${m.nameZh}（写回后生效）`);
      showFloorDetail(f, clientX, clientY);
    });
  });
}

function positionDetail(clientX: number, clientY: number) {
  const margin = 8;
  let left = clientX + margin;
  let top = clientY + margin;
  detailEl.style.left = `${left}px`;
  detailEl.style.top = `${top}px`;
  requestAnimationFrame(() => {
    const rect = detailEl.getBoundingClientRect();
    if (rect.right > window.innerWidth - margin) {
      left = Math.max(margin, clientX - rect.width - margin);
      detailEl.style.left = `${left}px`;
    }
    if (rect.bottom > window.innerHeight - margin) {
      top = Math.max(margin, clientY - rect.height - margin);
      detailEl.style.top = `${top}px`;
    }
  });
}

function drawGrid() {
  const w = canvas.width;
  const h = canvas.height;
  const step = CELL * PX_PER_UNIT * scale;
  if (step < 4) return;

  let ox = w / 2 + panX;
  let oy = h / 2 + panY;
  let halfX = 20;
  let halfZ = 20;

  if (gridInfo?.found) {
    const c = worldToCanvas(gridInfo.worldPosition.x, gridInfo.worldPosition.z);
    ox = c.x;
    oy = c.y;
    const cs = gridInfo.cellSize?.x ?? CELL;
    halfX = gridInfo.gridHalfSizeX || 10;
    halfZ = gridInfo.gridHalfSizeZ || 10;
    const gw = cs * PX_PER_UNIT * scale;
    ctx.strokeStyle = "rgba(61, 107, 243, 0.35)";
    ctx.lineWidth = 1;
    ctx.strokeRect(ox - halfX * gw, oy - halfZ * gw, halfX * 2 * gw, halfZ * 2 * gw);
  }

  ctx.strokeStyle = "rgba(255,255,255,0.06)";
  ctx.lineWidth = 1;
  const startX = (ox % step) - step;
  for (let x = startX; x < w; x += step) {
    ctx.beginPath();
    ctx.moveTo(x, 0);
    ctx.lineTo(x, h);
    ctx.stroke();
  }
  const startY = (oy % step) - step;
  for (let y = startY; y < h; y += step) {
    ctx.beginPath();
    ctx.moveTo(0, y);
    ctx.lineTo(w, y);
    ctx.stroke();
  }
}

function drawItem(item: EditorItem, selected: boolean) {
  const cat = catalogByGuid.get(item.prefabGuid);
  const fp = resolveFootprint(item);
  const rot = normalizeRot(item.localRotationY);
  const center = worldToCanvas(item._wx, item._wz);
  const cellPx = CELL * PX_PER_UNIT * scale;
  const w = fp.cellsX * cellPx;
  const h = fp.cellsZ * cellPx;
  const isUtensil = isStackUtensilCatalog(cat);
  const paint = paintStyleForItem(cat, item.parentPath, selected);

  ctx.save();
  ctx.translate(center.x, center.y);
  ctx.rotate((-rot * Math.PI) / 180);

  const inset = isUtensil ? Math.min(cellPx * 0.22, 10) : 0;
  const bw = Math.max(4, w - inset * 2);
  const bh = Math.max(4, h - inset * 2);

  ctx.fillStyle = paint.fill;
  ctx.fillRect(-bw / 2, -bh / 2, bw, bh);

  ctx.strokeStyle = paint.stroke;
  ctx.lineWidth = selected ? 2 : 1;
  ctx.strokeRect(-bw / 2, -bh / 2, bw, bh);

  if (!isUtensil && (fp.cellsX > 1 || fp.cellsZ > 1)) {
    ctx.strokeStyle = "rgba(255,255,255,0.15)";
    ctx.lineWidth = 1;
    for (let i = 1; i < fp.cellsX; i++) {
      const x = -w / 2 + i * cellPx;
      ctx.beginPath();
      ctx.moveTo(x, -h / 2);
      ctx.lineTo(x, h / 2);
      ctx.stroke();
    }
    for (let j = 1; j < fp.cellsZ; j++) {
      const y = -h / 2 + j * cellPx;
      ctx.beginPath();
      ctx.moveTo(-w / 2, y);
      ctx.lineTo(w / 2, y);
      ctx.stroke();
    }
  }

  ctx.beginPath();
  ctx.rect(-bw / 2 + 2, -bh / 2 + 2, bw - 4, bh - 4);
  ctx.clip();

  ctx.fillStyle = paint.label;
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  drawLabelInBox(ctx, itemLabel(item), bw - 4, bh - 4);

  ctx.restore();
}

function worldToItemLocal(item: EditorItem, wx: number, wz: number): { lx: number; lz: number } {
  const dx = wx - item._wx;
  const dz = wz - item._wz;
  const rad = (normalizeRot(item.localRotationY) * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  return {
    lx: dx * cos + dz * sin,
    lz: -dx * sin + dz * cos,
  };
}

function hitTest(wx: number, wz: number): EditorItem | null {
  const sorted = [...items].sort(
    (a, b) => drawLayerForItem(b, catalogByGuid) - drawLayerForItem(a, catalogByGuid)
  );
  for (const item of sorted) {
    const fp = resolveFootprint(item);
    const { lx, lz } = worldToItemLocal(item, wx, wz);
    const hw = (fp.cellsX * CELL) / 2;
    const hh = (fp.cellsZ * CELL) / 2;
    if (Math.abs(lx) <= hw && Math.abs(lz) <= hh) return item;
  }
  return null;
}

function hideDetail() {
  detailEl.classList.add("hidden");
}

function stackDetailHtml(item: EditorItem, cat: CatalogItem | undefined): string {
  if (!cat?.stack) return "";
  const host = findStackHost(item, cat, item._wx, item._wz, items, catalogByGuid);
  const ruleLabel = hostRuleLabelZh(cat.stack.hostRule);
  if (host) {
    const hostCat = catalogByGuid.get(host.prefabGuid);
    const hostLabel = itemLabel(host);
    const hostId = prefabIdFromPath(host.prefabAssetPath) || hostCat?.id || "—";
    return `<dt>叠放</dt><dd>堆叠于「${hostLabel}」（${hostId}）之上；规则：${ruleLabel}；本地高度 Y=${cat.stack.y}</dd>`;
  }
  return `<dt>叠放</dt><dd>应堆叠在${ruleLabel}上（当前未对齐到有效载体）；本地高度 Y=${cat.stack.y}</dd>`;
}

function showDetail(item: EditorItem, clientX: number, clientY: number) {
  const cat = catalogByGuid.get(item.prefabGuid);
  const fp = resolveFootprint(item);
  const id = prefabIdFromPath(item.prefabAssetPath) || cat?.id || "—";

  detailEl.innerHTML = `
    <h3>${itemLabel(item)}</h3>
    <dl>
      <dt>Prefab ID</dt><dd>${id}</dd>
      <dt>中文名</dt><dd>${cat?.nameZh ? tidyCatalogNameZh(cat.nameZh) : "—"}</dd>
      <dt>资源路径</dt><dd>${item.prefabAssetPath || "—"}</dd>
      <dt>层级路径</dt><dd>${item.hierarchyPath}</dd>
      <dt>父节点</dt><dd>${item.parentPath || "—"}</dd>
      <dt>占地</dt><dd>${fp.cellsX} × ${fp.cellsZ} 格 (${(fp.cellsX * CELL).toFixed(1)} × ${(fp.cellsZ * CELL).toFixed(1)} m)</dd>
      <dt>本地坐标</dt><dd>x ${item.localPosition.x.toFixed(2)}, y ${item.localPosition.y.toFixed(2)}, z ${item.localPosition.z.toFixed(2)}</dd>
      <dt>旋转 Y</dt><dd>${normalizeRot(item.localRotationY)}°</dd>
      <dt>分类</dt><dd>${cat?.layoutTier === "decor" ? "装饰道具" : "核心玩法"} · ${cat?.nameZh ? tidyCatalogNameZh(cat.nameZh) : cat?.category ?? "—"}</dd>
      ${stackDetailHtml(item, cat)}
      ${item.stubKind === "Dispenser" ? `<dt>食材</dt><dd>${ingredientNameZh(ingredientsCache, item.dispenser?.spawnerItemPrefabGuid)}</dd>` : ""}
    </dl>
    <p class="close-hint">右键可配置食材箱/生成器 · Esc 关闭</p>
  `;

  const margin = 8;
  let left = clientX + margin;
  let top = clientY + margin;
  detailEl.classList.remove("hidden");
  detailEl.style.left = `${left}px`;
  detailEl.style.top = `${top}px`;

  requestAnimationFrame(() => {
    const rect = detailEl.getBoundingClientRect();
    if (rect.right > window.innerWidth - margin) {
      left = Math.max(margin, clientX - rect.width - margin);
      detailEl.style.left = `${left}px`;
    }
    if (rect.bottom > window.innerHeight - margin) {
      top = Math.max(margin, clientY - rect.height - margin);
      detailEl.style.top = `${top}px`;
    }
  });
}

function snapItemWorld(item: EditorItem, wx: number, wz: number): { x: number; z: number } {
  const fp = resolveFootprint(item);
  return snapFootprintCenter(wx, wz, fp.cellsX, fp.cellsZ, item.localRotationY, CELL, snapStep);
}

function refreshUtensilStacks() {
  for (const item of items) {
    const cat = catalogByGuid.get(item.prefabGuid);
    trySnapUtensilToHost(item, cat, items, catalogByGuid);
  }
}

function syncLocalFromWorld(item: EditorItem) {
  const snapped = snapItemWorld(item, item._wx, item._wz);
  item._wx = snapped.x;
  item._wz = snapped.z;
  const cat = catalogByGuid.get(item.prefabGuid);
  if (cat?.stack) {
    trySnapUtensilToHost(item, cat, items, catalogByGuid);
  }
  item.localPosition.x = snapValue(item._wx - item._parentWx, snapStep);
  item.localPosition.z = snapValue(item._wz - item._parentWz, snapStep);
  if (cat?.stack) {
    item.localPosition.y = cat.stack.y;
  }
}

function countDuplicateInstanceIds(list: LayoutItem[]): number {
  const seen = new Set<string>();
  let dup = 0;
  for (const it of list) {
    const id = it.instanceId;
    if (!id || id.startsWith("new:")) continue;
    if (seen.has(id)) dup++;
    else seen.add(id);
  }
  return dup;
}

function buildDocument(): LayoutDocument {
  return {
    sceneAssetPath: scenePath,
    items: items.map(({ _editorKey, _wx, _wz, _parentWx, _parentWz, ...rest }) => {
      const fp = resolveFootprint(rest);
      return { ...rest, footprint: fp };
    }),
    floors: floors.map(({ _key, _wx, _wz, _wCells, _dCells, ...rest }) => ({
      ...rest,
      widthCells: _wCells,
      depthCells: _dCells,
      widthUnits: _wCells * CELL,
      depthUnits: _dCells * CELL,
    })),
  };
}

function buildPalette(catalog: import("./types").Catalog, filter: string) {
  paletteCats.innerHTML = "";
  const q = filter.trim().toLowerCase();

  const groups =
    catalog.paletteGroups ??
    Object.keys(catalog.byCategory)
      .sort()
      .map((key) => ({
        key,
        labelZh: key,
        labelEn: key,
        layoutTier: key.startsWith("art/") ? ("decor" as const) : ("core" as const),
        itemCount: catalog.byCategory[key].length,
      }));

  for (const group of groups) {
    const list = (catalog.byCategory[group.key] ?? []).filter((it) => {
      if (!q) return true;
      return (
        it.id.toLowerCase().includes(q) ||
        it.nameZh.toLowerCase().includes(q) ||
        it.nameEn.toLowerCase().includes(q) ||
        it.assetPath.toLowerCase().includes(q)
      );
    });
    if (list.length === 0) continue;

    const details = document.createElement("details");
    details.className = "cat-group";
    details.dataset.tier = group.layoutTier;
    details.open = group.layoutTier === "core";
    const summary = document.createElement("summary");
    summary.textContent = `${group.labelZh} (${list.length})`;
    summary.title = group.labelEn;
    details.appendChild(summary);

    const max = group.layoutTier === "decor" ? 80 : list.length;
    for (let i = 0; i < Math.min(max, list.length); i++) {
      const it = list[i];
      const row = document.createElement("div");
      row.className = "palette-item";
      if (it.layoutTier === "decor") row.classList.add("palette-decor");
      row.draggable = true;
      row.dataset.guid = it.guid;
      const sub = it.stack
        ? `<div class="sub">叠放 · 高度 ${it.stack.y}m</div>`
        : it.layoutTier === "decor"
          ? `<div class="sub">装饰</div>`
          : "";
      row.innerHTML = `<div class="zh">${tidyCatalogNameZh(it.nameZh)}</div><div class="id">${it.nameEn} · ${it.id}</div>${sub}`;
      row.addEventListener("dragstart", (e) => {
        dragCatalog = it;
        e.dataTransfer?.setData("text/plain", it.guid);
      });
      row.addEventListener("dragend", () => {
        dragCatalog = null;
      });
      details.appendChild(row);
    }
    if (list.length > max) {
      const more = document.createElement("div");
      more.className = "palette-item";
      more.style.color = "#9aa0a6";
      more.textContent = `… 还有 ${list.length - max} 项，请搜索缩小范围`;
      details.appendChild(more);
    }
    paletteCats.appendChild(details);
  }
}

function buildFloorPalette() {
  paletteCats.innerHTML = "";

  const addBtn = document.createElement("button");
  addBtn.className = "palette-add-floor";
  addBtn.textContent = "+ 新增地板（在画布点击放置）";
  addBtn.addEventListener("click", () => {
    pendingNewFloor = true;
    setStatus("在画布上点击以放置新地板");
    canvas.style.cursor = "crosshair";
  });
  paletteCats.appendChild(addBtn);

  // Surface catalog (floor/background prefabs) as draggable tiles.
  const surfaceItems = [];
  for (const it of catalogByGuid.values()) if (isSurfaceItem(it)) surfaceItems.push(it);
  surfaceItems.sort((a, b) => a.id.localeCompare(b.id));
  if (surfaceItems.length > 0) {
    const t2 = document.createElement("div");
    t2.className = "palette-section-title";
    t2.textContent = "地板拼块 / 背景道具（拖到画布放置）";
    paletteCats.appendChild(t2);
    const tileGrid = document.createElement("div");
    tileGrid.className = "palette-tile-grid";
    for (const it of surfaceItems) {
      const row = document.createElement("div");
      row.className = "palette-item palette-tile";
      row.draggable = true;
      row.dataset.guid = it.guid;
      row.innerHTML = `<div class="zh">${tidyCatalogNameZh(it.nameZh)}</div><div class="id">${it.id}</div>`;
      row.addEventListener("dragstart", (e) => {
        dragCatalog = it;
        e.dataTransfer?.setData("text/plain", it.guid);
      });
      row.addEventListener("dragend", () => {
        dragCatalog = null;
      });
      tileGrid.appendChild(row);
    }
    paletteCats.appendChild(tileGrid);
  }
}

let pendingNewFloor = false;

function updateFloorBar() {
  if (currentLayer !== "floor") {
    floorBar.classList.add("hidden");
    return;
  }
  floorBar.classList.remove("hidden");
  const f = floors.find((x) => x._key === selectedFloorKey);
  if (!f) {
    floorBar.innerHTML = `<span class="fb-info">${deathLabelZh(deathInfo)} · 共 ${floors.length} 块地板</span>`;
    return;
  }
  const areaCells = f._wCells * f._dCells;
  floorBar.innerHTML = `
    <span class="fb-info"><b>${surfaceKindLabelZh(f.surfaceKind)}</b> · ${f._wCells}×${f._dCells}格 (${(f._wCells * CELL).toFixed(1)}×${(f._dCells * CELL).toFixed(1)}m, ${areaCells}格) · ${f.materialName ?? "无材质"}</span>
    <span class="fb-hint">拖动移动 · 拖角点缩放 · 右键详情 · Del 删除</span>
  `;
}

function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  return tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT" || target.isContentEditable;
}

async function init() {
  const ok = await fetchHealth();
  const healthInfo = await fetchHealthInfo().catch(() => ({ ok: false, recipeApi: false }));
  setStatus(
    ok
      ? healthInfo.recipeApi
        ? "已连接 Unity（含菜谱 API）"
        : "已连接 Unity（请重启 Bridge 以使用菜谱）"
      : "未连接 Unity（请先启动 Bridge）",
    ok
  );

  const catalog = await loadCatalog();
  for (const it of catalog.items) catalogByGuid.set(it.guid, it);
  ingredientsCache = await fetchIngredients().catch(() => []);
  buildPalette(catalog, "");

  const scenes = await fetchLevelSets().catch(() => []);
  sceneSelect.innerHTML = '<option value="">— 选择场景 —</option>';
  for (const s of scenes) {
    const opt = document.createElement("option");
    opt.value = s.assetPath;
    opt.textContent = `${s.levelSet} / ${s.sceneName}`;
    sceneSelect.appendChild(opt);
  }

  document.getElementById("palette-search")!.addEventListener("input", (e) => {
    buildPalette(catalog, (e.target as HTMLInputElement).value);
  });

  sceneSelect.addEventListener("change", () => {
    if (sceneSelect.value) void loadScene(sceneSelect.value);
  });

  document.getElementById("btn-reload")!.addEventListener("click", () => {
    if (scenePath) void loadScene(scenePath);
  });

  document.getElementById("btn-save")!.addEventListener("click", () => void saveToUnity());

  document.getElementById("btn-recipes")!.addEventListener("click", () => void openRecipesDialog());

  document.getElementById("snap-half")!.addEventListener("change", (e) => {
    snapStep = (e.target as HTMLInputElement).checked ? 0.6 : CELL;
  });

  document.getElementById("show-grid")!.addEventListener("change", (e) => {
    showGrid = (e.target as HTMLInputElement).checked;
    draw();
  });

  document.querySelectorAll<HTMLButtonElement>(".layer-tab").forEach((btn) => {
    btn.addEventListener("click", () => {
      const layer = btn.dataset.layer as "items" | "floor";
      if (layer === currentLayer) return;
      currentLayer = layer;
      document.querySelectorAll(".layer-tab").forEach((b) => b.classList.toggle("active", b === btn));
      selectedKey = null;
      selectedFloorKey = null;
      hideDetail();
      pendingNewFloor = false;
      canvas.style.cursor = "";
      if (layer === "floor") buildFloorPalette();
      else buildPalette(catalog, (document.getElementById("palette-search") as HTMLInputElement)?.value ?? "");
      draw();
    });
  });

  setupCanvas();
  requestAnimationFrame(draw);

  if (scenes.length > 0) {
    const guojia = scenes.find((s) => s.assetPath.includes("guojia"));
    const pick = guojia ?? scenes[0];
    sceneSelect.value = pick.assetPath;
    await loadScene(pick.assetPath);
  }

  startBridgeWatch();
}

let bridgeWasUp = false;
let bridgeStopAlerted = false;

function startBridgeWatch() {
  bridgeWasUp = true;
  bridgeStopAlerted = false;
  window.setInterval(async () => {
    const up = await fetchHealth();
    if (!up && bridgeWasUp && !bridgeStopAlerted) {
      bridgeStopAlerted = true;
      showBridgeStoppedModal();
      setStatus("未连接 Unity（后台服务已停止）", false);
    } else if (up) {
      bridgeStopAlerted = false;
    }
    bridgeWasUp = up;
  }, 3000);
}

function showBridgeStoppedModal() {
  openModal(
    "后台服务已停止",
    `<p>Layout Editor 的后台 Bridge 服务已断开。</p>
     <p>最常见的原因是 <b>Unity 进入了 Play 模式</b>（Play 时编辑器服务会暂停），也可能是服务被手动停止。</p>
     <p>请退出 Play 模式后，在 Unity <b>Tools → Layout Editor → Start Server</b> 重新启动，然后刷新本页。</p>`,
    `<button type="button" class="modal-btn primary" data-ok>知道了</button>`
  );
  document.querySelector("[data-ok]")?.addEventListener("click", closeModal);
}

async function loadScene(assetPath: string) {
  try {
    setStatus("加载场景…");
    scenePath = assetPath;
    currentLevelSet = levelSetFromScenePath(assetPath);
    const doc = await fetchLayout(assetPath);
    const dupIds = countDuplicateInstanceIds(doc.items);
    items = doc.items.map((raw, index) => enrichItem(raw, `i${index}`));
    floors = (doc.floors ?? []).map((raw, index) => enrichFloor(raw, `f${index}`));
    walkable = doc.walkable ?? [];
    deathInfo = doc.deathInfo ?? null;
    refreshUtensilStacks();
    gridInfo = await fetchGrid();
    floorMaterials = await fetchFloorMaterials(currentLevelSet).catch(() => []);
    if (currentLayer === "floor") buildFloorPalette();
    selectedKey = null;
    selectedFloorKey = null;
    hideDetail();
    draw();
    const floorNote = floors.length > 0 ? `、${floors.length} 块地板` : "";
    if (dupIds > 0) {
      setStatus(
        `已加载 ${items.length} 个物体（有 ${dupIds} 个重复 ID，请重新编译 Unity 后点「重新加载」）`,
        false
      );
    } else {
      setStatus(`已加载 ${items.length} 个物体${floorNote}`);
    }
  } catch (e) {
    setStatus((e as Error).message, false);
  }
}

async function saveToUnity() {
  try {
    setStatus("写回中…");
    await saveLayout(buildDocument(), snapStep);
    setStatus("写回成功：已 Prepare + Reload Pseudo，请在 Unity Ctrl+S 保存场景");
    await loadScene(scenePath);
  } catch (e) {
    setStatus((e as Error).message, false);
  }
}

function addFromCatalog(cat: CatalogItem, wx: number, wz: number) {
  const id = `new:${cat.guid}:${crypto.randomUUID()}`;
  const editorKey = newEditorKey();
  const snapped = snapFootprintCenter(wx, wz, cat.footprint.cellsX, cat.footprint.cellsZ, 0, CELL, snapStep);
  const item: EditorItem = {
    instanceId: id,
    _editorKey: editorKey,
    hierarchyPath: id,
    prefabGuid: cat.guid,
    prefabAssetPath: cat.assetPath,
    parentPath: cat.defaultParent,
    displayName: cat.id,
    localPosition: { x: snapped.x, y: 0, z: snapped.z },
    worldPosition: { x: snapped.x, y: 0, z: snapped.z },
    localRotationY: 0,
    footprint: cat.footprint,
    _wx: snapped.x,
    _wz: snapped.z,
    _parentWx: 0,
    _parentWz: 0,
  };
  if (cat.id === "Dispenser") {
    item.stubKind = "Dispenser";
    item.dispenser = {};
  }
  if (cat.id === "AttachingFoodSpawner") {
    item.stubKind = "AttachingFoodSpawner";
    item.foodSpawner = {
      spawnInOrder: true,
      triggerAtStart: true,
      triggerTime: 5,
      attachmentPrefabGuids: [],
      weights: [],
    };
  }
  items.push(item);
  trySnapUtensilToHost(item, cat, items, catalogByGuid);
  selectedKey = editorKey;
  draw();
  warnItemVoid(item);
}

function setupCanvas() {
  canvas.addEventListener("wheel", (e) => {
    e.preventDefault();
    const factor = e.deltaY > 0 ? 0.9 : 1.1;
    scale = Math.min(4, Math.max(0.25, scale * factor));
    draw();
  });

  canvas.addEventListener("mousedown", (e) => {
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;
    const { x: wx, z: wz } = canvasToWorld(mx, my);

    if (e.button === 1 || (e.button === 0 && e.altKey) || (e.button === 0 && spaceHeld)) {
      panning = true;
      lastMx = mx;
      lastMy = my;
      hideDetail();
      updateCanvasCursor();
      return;
    }

    if (e.button === 2) return;
    if (e.button !== 0) return;

    if (currentLayer === "floor") {
      if (pendingNewFloor) {
        pendingNewFloor = false;
        canvas.style.cursor = "";
        addFloorAt(wx, wz);
        return;
      }
      const fHit = hitTestFloor(wx, wz);
      if (fHit) {
        selectedFloorKey = fHit.floor._key;
        selectedKey = null;
        dragFloorKey = fHit.floor._key;
        dragFloorMode = fHit.mode;
        dragFloorEdge = fHit.edge;
        dragFloorAnchorX = fHit.anchorX;
        dragFloorAnchorZ = fHit.anchorZ;
      } else {
        selectedFloorKey = null;
        const itemHit = hitTest(wx, wz);
        if (itemHit && isSurfaceItem(catalogByGuid.get(itemHit.prefabGuid))) {
          selectedKey = itemHit._editorKey;
          dragItemKey = itemHit._editorKey;
          dragOffsetX = wx - itemHit._wx;
          dragOffsetZ = wz - itemHit._wz;
        } else {
          selectedKey = null;
          hideDetail();
        }
      }
      draw();
      return;
    }

    const hit = hitTest(wx, wz);
    if (hit) {
      selectedKey = hit._editorKey;
      dragItemKey = hit._editorKey;
      dragOffsetX = wx - hit._wx;
      dragOffsetZ = wz - hit._wz;
    } else {
      selectedKey = null;
      hideDetail();
    }
    draw();
  });

  canvas.addEventListener("contextmenu", (e) => {
    e.preventDefault();
    const rect = canvas.getBoundingClientRect();
    const { x: wx, z: wz } = canvasToWorld(e.clientX - rect.left, e.clientY - rect.top);
    if (currentLayer === "floor") {
      const fHit = hitTestFloor(wx, wz);
      if (fHit) {
        selectedFloorKey = fHit.floor._key;
        showFloorDetail(fHit.floor, e.clientX, e.clientY);
        draw();
      } else {
        hideDetail();
      }
      return;
    }
    const hit = hitTest(wx, wz);
    if (hit) {
      selectedKey = hit._editorKey;
      const prefabId = prefabIdFromPath(hit.prefabAssetPath);
      if (hit.stubKind === "Dispenser" || hit.stubKind === "AttachingFoodSpawner" || prefabId === "Dispenser" || prefabId === "AttachingFoodSpawner") {
        openStubEditorForItem(hit);
      } else {
        showDetail(hit, e.clientX, e.clientY);
      }
      draw();
    } else {
      hideDetail();
    }
  });

  window.addEventListener("mousemove", (e) => {
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;

    if (panning) {
      panX += mx - lastMx;
      panY += my - lastMy;
      lastMx = mx;
      lastMy = my;
      draw();
      return;
    }

    if (dragFloorKey) {
      const f = floors.find((x) => x._key === dragFloorKey);
      if (f) dragFloor(f, mx, my);
      draw();
      return;
    }

    if (!dragItemKey) return;
    const item = items.find((i) => i._editorKey === dragItemKey);
    if (!item) return;
    const { x: wx, z: wz } = canvasToWorld(mx, my);
    item._wx = wx - dragOffsetX;
    item._wz = wz - dragOffsetZ;
    syncLocalFromWorld(item);
    draw();
  });

  window.addEventListener("mouseup", () => {
    if (panning) {
      panning = false;
      updateCanvasCursor();
    }
    if (dragFloorKey) {
      const f = floors.find((x) => x._key === dragFloorKey);
      if (f) finalizeFloor(f);
    }
    dragFloorKey = null;
    if (dragItemKey) {
      const item = items.find((i) => i._editorKey === dragItemKey);
      if (item) {
        syncLocalFromWorld(item);
        warnItemVoid(item);
      }
    }
    dragItemKey = null;
  });

  canvas.addEventListener("dragover", (e) => {
    e.preventDefault();
  });

  canvas.addEventListener("drop", (e) => {
    e.preventDefault();
    const guid = e.dataTransfer?.getData("text/plain");
    const cat = guid ? catalogByGuid.get(guid) : dragCatalog;
    if (!cat) return;
    const rect = canvas.getBoundingClientRect();
    const { x: wx, z: wz } = canvasToWorld(e.clientX - rect.left, e.clientY - rect.top);
    addFromCatalog(cat, wx, wz);
  });

  window.addEventListener("keydown", (e) => {
    if (e.code === "Space" && !isTypingTarget(e.target)) {
      e.preventDefault();
      if (!spaceHeld) {
        spaceHeld = true;
        updateCanvasCursor();
      }
    }

    if (e.key === "Escape") {
      hideDetail();
      closeModal();
      pendingNewFloor = false;
      canvas.style.cursor = "";
    }

    if (currentLayer === "floor") {
      if (isTypingTarget(e.target)) return;
      if (selectedFloorKey) {
        const f = floors.find((x) => x._key === selectedFloorKey);
        if (f) {
          if (e.key === "Delete" || e.key === "Backspace") {
            floors = floors.filter((x) => x._key !== selectedFloorKey);
            selectedFloorKey = null;
            hideDetail();
            draw();
          }
          if (e.key === "r" || e.key === "R") {
            f.localRotationY = (f.localRotationY + 90) % 360;
            draw();
          }
        }
        return;
      }
      if (selectedKey) {
        const item = items.find((i) => i._editorKey === selectedKey);
        if (item && isSurfaceItem(catalogByGuid.get(item.prefabGuid))) {
          if (e.key === "Delete" || e.key === "Backspace") {
            items = items.filter((i) => i._editorKey !== selectedKey);
            selectedKey = null;
            hideDetail();
            draw();
          }
          if (e.key === "r" || e.key === "R") {
            item.localRotationY = (item.localRotationY + 90) % 360;
            syncLocalFromWorld(item);
            draw();
          }
        }
      }
      return;
    }

    if (!selectedKey) return;
    const item = items.find((i) => i._editorKey === selectedKey);
    if (!item) return;

    if (isTypingTarget(e.target)) return;

    if (e.key === "Delete" || e.key === "Backspace") {
      items = items.filter((i) => i._editorKey !== selectedKey);
      selectedKey = null;
      hideDetail();
      draw();
    }
    if (e.key === "r" || e.key === "R") {
      item.localRotationY = (item.localRotationY + 90) % 360;
      syncLocalFromWorld(item);
      draw();
    }
  });

  window.addEventListener("keyup", (e) => {
    if (e.code === "Space") {
      spaceHeld = false;
      panning = false;
      updateCanvasCursor();
    }
  });

  window.addEventListener("blur", () => {
    spaceHeld = false;
    panning = false;
    updateCanvasCursor();
  });

  document.addEventListener("mousedown", (e) => {
    if (!detailEl.classList.contains("hidden") && !detailEl.contains(e.target as Node)) {
      hideDetail();
    }
  });

  window.addEventListener("resize", () => draw());
}

void init();
