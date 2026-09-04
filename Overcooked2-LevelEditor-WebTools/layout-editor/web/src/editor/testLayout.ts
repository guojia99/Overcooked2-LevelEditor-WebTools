/**
 * 测试布局生成器：一键生成一张 30×16 的测试关卡。
 *
 * 动作：
 *  1. 清空现有地板/物品/联动，铺一张 30×16 地板（居中于原点）。
 *  2. 相机 FOV 设为 56。
 *  3. 每种可建箱食材（prefab 型，排除 node 型酱料/汽水）各放一个食材箱并预设该食材，
 *     从地板最左上角开始顺序平铺。
 *  4. 所有核心层道具顺序平铺；需要开关组合的（饮料机/酱料机/断头台）默认用开关组合放置。
 *  5. 全部菜谱加入关卡；按菜谱自动给锅具装填食材（allowedIngredientGuids）。
 *
 * 网格对齐规则：物品按占格（<1 格按 1 格、1×1 按 1×1、2×2 按 2×2）在网格内居中，
 * 严格从左上角起铺、不超出地板。
 */
import { CELL, S, EditorItem, EditorFloor, type ComboDef } from "./state";
import type { CatalogItem } from "../types";
import { uuid, newEditorKey, prefabIdFromPath } from "./coords";
import { catalogItemById } from "./catalog";
import { addCombo, comboById, comboDisabledReason } from "./combos";
import { setSelection, setFloorSelection } from "./selection";
import { pushHistory } from "./historyOps";
import { draw } from "./render";
import { setStatus } from "./status";
import { openModal, closeModal } from "../modals";
import { fetchRecipeCatalog, fetchLevelRecipes, saveLevelRecipes } from "../api";
import { computeUtensilIngredientFill, functionalBaseId } from "./recipeKnowledge";
import { stubKindOf, utensilCapacityOrFix } from "./stubControls";

const FLOOR_W = 30;
const FLOOR_D = 16;
/** 相邻物品间隔（格）。0 = 紧凑平铺（130 食材箱 + 核心道具放满 30×16 必须无间隙）。 */
const GAP = 0;

/** 需要开关组合的核心层道具 → 对应组合 id。 */
const SWITCH_COMBOS = [
  "drink_switch",
  "drink_switch_icecream",
  "condiment_switch",
  "guillotine_switch",
];

/** 测试布局额外使用的组合（传送门互配，避免裸传送门无出口 NRE）。 */
const TEST_COMBOS = ["teleportal_pair"];

/** 组合自动带出、不再单独放置的道具。 */
const COMBO_AUTO_PARTS = new Set(["Switch"]);

/** 不参与平铺的道具（食材箱单独处理；Player/空气墙不可平铺）。 */
const SKIP_IDS = new Set(["Player", "AirWall", "Dispenser", "Backpack"]);

/** 不可从食材箱产出的食材（真实 prefab 无 ISpawnableItem，运行时 Setup NRE）。 */
const NON_SPAWNABLE_INGREDIENTS = new Set(["TurkeySO"]);

function makeItem(
  cat: CatalogItem,
  wx: number,
  wz: number,
  dispenserGuid?: string
): EditorItem {
  const id = `new:${cat.guid}:${uuid()}`;
  const item: EditorItem = {
    instanceId: id,
    _editorKey: newEditorKey(),
    hierarchyPath: id,
    prefabGuid: cat.guid,
    prefabAssetPath: cat.assetPath,
    parentPath: cat.defaultParent,
    displayName: cat.id,
    localPosition: { x: wx, y: 0, z: wz },
    worldPosition: { x: wx, y: 0, z: wz },
    localRotationY: 0,
    footprint: cat.footprint,
    _wx: wx,
    _wz: wz,
    _parentWx: 0,
    _parentWz: 0,
  };
  if (cat.id === "Dispenser") {
    item.stubKind = "Dispenser";
    item.dispenser = dispenserGuid ? { spawnerItemPrefabGuid: dispenserGuid } : {};
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
  if (cat.id === "ConveyorStation") {
    item.stubKind = "Conveyor";
    item.conveyor = { conveySpeed: 0.5 };
  }
  if (cat.id === "Teleportal") {
    item.stubKind = "Teleportal";
    item.teleportal = { exitPortalInstanceId: "", portalColor: 0, doubleSided: false };
  }
  return item;
}

function makeFloor(w: number, d: number): EditorFloor {
  const id = `new:floor:${uuid()}`;
  const defaultMat = S.floorMaterials.find((m) => /floor|blacktiles|path/i.test(m.id));
  return {
    instanceId: id,
    _key: newEditorKey(),
    hierarchyPath: id,
    parentPath: "Art/Ground",
    displayName: "Floor",
    surfaceKind: "solid",
    meshType: "plane",
    meshFileId: 10209,
    materialGuid: defaultMat?.guid,
    materialAssetPath: defaultMat?.assetPath,
    materialName: defaultMat?.id,
    localPosition: { x: 0, y: -0.05, z: 0 },
    worldPosition: { x: 0, y: -0.05, z: 0 },
    localRotationY: 0,
    localScale: { x: (w * CELL) / 10, y: 1, z: (d * CELL) / 10 },
    widthUnits: w * CELL,
    depthUnits: d * CELL,
    widthCells: w,
    depthCells: d,
    _wx: 0,
    _wz: 0,
    _wCells: w,
    _dCells: d,
  };
}

/** 组合实际占用的包围盒（格，从主件左缘对齐）。parts 以主件中心为基准偏移。 */
function comboBBox(def: ComboDef): { w: number; h: number } {
  const p0 = def.parts[0];
  const p0cat = p0 ? catalogItemById(p0.id) : undefined;
  const p0w = p0cat?.footprint.cellsX ?? 1;
  const p0h = p0cat?.footprint.cellsZ ?? 1;
  let right = p0w / 2;
  let bottom = p0h / 2;
  for (const p of def.parts) {
    const cat = catalogItemById(p.id);
    if (!cat) continue;
    right = Math.max(right, p.dx + cat.footprint.cellsX / 2);
    bottom = Math.max(bottom, p.dz + cat.footprint.cellsZ / 2);
  }
  return { w: p0w / 2 + right, h: p0h / 2 + bottom };
}

/** 点击「测试布局」：先弹窗确认，避免误触直接清空场景。 */
export function requestTestLayout(): void {
  const ingCount = S.ingredientsCache.filter((i) => !i.nodeOnly).length;
  const coreCount = [...S.catalogByGuid.values()].filter((c) => c.layoutTier === "core").length;
  openModal(
    "🧪 测试布局",
    `<p class="modal-hint" style="color:#c0392b">此操作将<b>清空当前场景</b>（地板 / 物品 / 联动；<b>玩家出生点保留</b>），并重新生成：</p>
     <ul style="margin:8px 0 8px 18px;line-height:1.8">
       <li>30×16 地板（左上角起铺）+ 相机 FOV 56</li>
       <li>${ingCount} 个食材箱（每种可建箱食材一个并预设食材）</li>
       <li>${coreCount} 个核心层道具顺序平铺（开关组合默认用组合）</li>
       <li>全部菜谱加入关卡 + 锅具按菜谱自动装填</li>
     </ul>`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn primary" id="test-layout-confirm">✅ 生成</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.getElementById("test-layout-confirm")?.addEventListener("click", () => {
    closeModal();
    void runTestLayout();
  });
}

export async function runTestLayout(): Promise<void> {
  if (!S.scenePath) {
    setStatus("请先加载场景", false);
    return;
  }

  pushHistory();

  // 保留玩家（出生点固定，测试布局不清除）
  const players = S.items.filter(
    (it) => it.stubKind === "Player" || S.catalogByGuid.get(it.prefabGuid ?? "")?.id === "Player"
  );

  // 清空现有内容（测试布局 = 全新占位）
  S.floors = [];
  S.items = [];
  S.switchLinks = [];
  S.buttonLinks = [];
  S.selectedKeys = new Set();
  S.selectedFloorKeys = new Set();

  // 地板 30×16 居中于原点
  const floor = makeFloor(FLOOR_W, FLOOR_D);
  S.floors.push(floor);
  setFloorSelection([floor._key]);
  // 让画布把整张地板视为可行走区域
  S.walkable = [{ surfaceType: "solid", cx: 0, cz: 0, sx: FLOOR_W * CELL, sz: FLOOR_D * CELL }];

  // 相机 FOV 56
  S.cameraInfo = {
    backgroundColor: S.cameraInfo?.backgroundColor ?? "#000000",
    fieldOfView: 56,
    position: S.cameraInfo?.position ?? { x: 0, y: 0, z: 0 },
    pitch: S.cameraInfo?.pitch ?? 0,
    yaw: S.cameraInfo?.yaw ?? 0,
    roll: S.cameraInfo?.roll ?? 0,
    nearClip: S.cameraInfo?.nearClip ?? 0.3,
    farClip: S.cameraInfo?.farClip ?? 1000,
  };

  const floorMinX = -(FLOOR_W / 2) * CELL;
  const floorMinZ = -(FLOOR_D / 2) * CELL;
  let curX = 0;
  let curZ = 0;
  let rowH = 0;
  let overflowed = false;

  /** 物品占格（<1 格按 1 格、1×1 按 1×1、2×2 按 2×2）。 */
  const spanX = (fp: { cellsX: number; cellsZ: number }) => Math.max(1, Math.ceil(fp.cellsX));
  const spanZ = (fp: { cellsX: number; cellsZ: number }) => Math.max(1, Math.ceil(fp.cellsZ));

  /** 在光标处占一个 sx×sz 的格子，返回其中心世界坐标（格子内居中）。 */
  const reserve = (sx: number, sz: number): { wx: number; wz: number } => {
    if (curX + sx > FLOOR_W) {
      curX = 0;
      curZ += rowH + GAP;
      rowH = 0;
    }
    if (curZ + sz > FLOOR_D) overflowed = true;
    const wx = floorMinX + (curX + sx / 2) * CELL;
    const wz = floorMinZ + (curZ + sz / 2) * CELL;
    curX += sx + GAP;
    rowH = Math.max(rowH, sz);
    return { wx, wz };
  };

  /** 放置单个道具：占格居中于网格。 */
  const placeItem = (cat: CatalogItem, dispenserGuid?: string) => {
    const { wx, wz } = reserve(spanX(cat.footprint), spanZ(cat.footprint));
    S.items.push(makeItem(cat, wx, wz, dispenserGuid));
  };

  // ① 全部食材箱：每种可建箱食材（prefab 型）一个箱并预设该食材
  let dispenserCount = 0;
  const dispenserCat = catalogItemById("Dispenser");
  if (dispenserCat) {
    for (const ing of S.ingredientsCache) {
      if (ing.nodeOnly) continue; // node 型（酱料/汽水）由专属机器产出，不入普通箱
      if (NON_SPAWNABLE_INGREDIENTS.has(ing.id)) continue; // 真实 prefab 无 ISpawnableItem
      placeItem(dispenserCat, ing.guid);
      dispenserCount++;
    }
  }

  // ② 核心层道具（需要开关组合的用组合放置）
  const comboByFirst = new Map<string, ComboDef>();
  for (const cid of [...SWITCH_COMBOS, ...TEST_COMBOS]) {
    const def = comboById(cid);
    if (def && def.parts.length > 0) comboByFirst.set(def.parts[0].id, def);
  }

  const coreItems = [...S.catalogByGuid.values()]
    .filter((c) => c.layoutTier === "core")
    .sort((a, b) => a.category.localeCompare(b.category) || a.id.localeCompare(b.id));

  let comboCount = 0;
  let itemCount = 0;
  for (const cat of coreItems) {
    if (SKIP_IDS.has(cat.id)) continue;
    if (COMBO_AUTO_PARTS.has(cat.id)) continue; // 组合自动带出

    const combo = comboByFirst.get(cat.id);
    if (combo && !comboDisabledReason(combo)) {
      const p0 = combo.parts[0];
      const p0cat = catalogItemById(p0.id);
      const { w, h } = comboBBox(combo);
      const sw = Math.max(1, Math.ceil(w));
      const sh = Math.max(1, Math.ceil(h));
      if (curX + sw > FLOOR_W) {
        curX = 0;
        curZ += rowH + GAP;
        rowH = 0;
      }
      if (curZ + sh > FLOOR_D) overflowed = true;
      // 主件在组合包围盒内对齐到网格（主件居中于其占格）
      const anchorX = floorMinX + (curX + (p0cat?.footprint.cellsX ?? 1) / 2) * CELL;
      const anchorZ = floorMinZ + (curZ + (p0cat?.footprint.cellsZ ?? 1) / 2) * CELL;
      addCombo(combo, anchorX, anchorZ);
      curX += sw + GAP;
      rowH = Math.max(rowH, sh);
      comboCount++;
      continue;
    }

    placeItem(cat);
    itemCount++;
  }

  // ③ 全部菜谱加入关卡 + 按菜谱给锅具装填食材
  let recipeNote = "";
  let utensilNote = "";
  try {
    const [recipes, level] = await Promise.all([
      fetchRecipeCatalog(S.currentLevelSet),
      fetchLevelRecipes(S.scenePath),
    ]);
    const orderable = recipes.filter((r) => !r.intermediate);
    if (level?.levelInfoAssetPath) {
      await saveLevelRecipes(
        level.levelInfoAssetPath,
        orderable.map((r) => r.guid)
      );
      recipeNote = `菜谱 ${orderable.length} 道`;
    } else {
      recipeNote = "未找到 LevelInfo，菜谱未写入";
    }

    // 锅具自动装填（与「锅具管理 → 按菜谱自动填充」同一套数据驱动）
    S.intermediatesCache = recipes.filter((r) => r.intermediate || r.isCustom);
    const fill = computeUtensilIngredientFill(orderable);
    if (fill.size > 0) {
      const ingGuid = new Map(S.ingredientsCache.map((i) => [i.id, i.guid]));
      const recipeGuid = new Map(recipes.map((r) => [r.id, r.guid]));
      let touched = 0;
      for (const it of S.items) {
        if (stubKindOf(it) !== "CookingUtensil") continue;
        const id = S.catalogByGuid.get(it.prefabGuid ?? "")?.id ?? prefabIdFromPath(it.prefabAssetPath ?? "");
        const f = fill.get(functionalBaseId(id ?? ""));
        if (!f) continue;
        const add: string[] = [];
        for (const iid of f.ings) {
          const g = ingGuid.get(iid);
          if (g) add.push(g);
        }
        for (const iid of f.intermediates) {
          const g = recipeGuid.get(iid);
          if (g) add.push(g);
        }
        if (!add.length) continue;
        it.stubKind = "CookingUtensil";
        if (!it.cookingUtensil) it.cookingUtensil = {};
        it.cookingUtensil.capacity = utensilCapacityOrFix(it);
        it.cookingUtensil.allowedIngredientGuids = add;
        touched++;
      }
      utensilNote = `锅具装填 ${touched} 个`;
    }
  } catch (e) {
    recipeNote = `菜谱/锅具配置失败：${(e as Error).message}`;
  }

  // 恢复玩家（保持原位置）
  for (const p of players) S.items.push(p);

  setSelection([]);
  S.dirty = true;
  draw();
  setStatus(
    `测试布局：30×${FLOOR_D} 地板 · FOV 56 · 食材箱 ${dispenserCount} · 核心道具 ${itemCount} + 组合 ${comboCount} · ${recipeNote} · ${utensilNote}` +
      (overflowed ? "（⚠ 部分物品超出地板，请调整）" : "") +
      "（写回后生效）"
  );
}
