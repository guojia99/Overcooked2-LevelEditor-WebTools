import {
  S,
  SaveScope
} from "./state";
import { dom } from "./dom";
import { uuid, prefabIdFromPath, escHtml, newEditorKey } from "./coords";
import { setStatus } from "./status";
import { levelSetFromScenePath } from "./catalog";
import { isPlayerItem } from "./renderItems";
import { enrichItem, enrichFloor, checkPlayerCollisions, checkWorkstationCollisions, refreshUtensilStacks } from "./items";
import { stubKindOf, normalizeMachineLinkTriggers } from "./stubControls";
import { cleanOrphanedAnimControls, stopAnimPreview } from "./animControl";
import { cleanOrphanedButtonLinks } from "./buttonLinks";
import { cleanOrphanedButtonEvents } from "./buttonEvents";
import { cleanOrphanedStubRefs } from "./stubRefs";
import { itemLabel } from "./labels";
import {
  syncBackgroundForTheme,
  mergeRaftItemsIntoFloors,
  mergeThemedItemsIntoFloors,
  repairFloorMaterialsFromCatalog
} from "./floors";
import { buildFloorPalette } from "./floorPalette";
import { buildDocument } from "./serialize";
import { clearSelection, clearFloorSelection } from "./selection";
import { resetOverlapMarqueePending } from "./input";
import { pushHistory } from "./historyOps";
import {
  hideDetail,
  hideContextMenu
} from "./ui/overlay";
import {
  clearDirty,
  markDirty
} from "./historyOps";
import {
  draw,
  computeLevelBounds
} from "./render";
import { updatePanelTabButtons } from "./panels";
import {
  openModal,
  closeModal
} from "../modals";
import {
  showBusy,
  hideBusy
} from "../busy";
import {
  BG_THEMES,
  bgTheme,
  bgThemeKeyForDeathType,
  inferBgThemeFromItems
} from "../floorColors";
import {
  saveLayout,
  fetchLayout,
  fetchGrid,
  fetchFloorMaterials,
  fetchWriteBackHistoryDoc,
  setDeathTheme,
  setKillPlaneBounds,
  fetchHealth
} from "../api";
import type {
  LayoutDocument,
  LayoutItem,
  FloorObject
} from "../types";

export function selectSceneInDropdowns(assetPath: string): void {
  if (!assetPath) return;
  const scene = S.sceneListCache.find((s) => s.assetPath === assetPath);
  if (!scene) return;
  const setSelect = document.getElementById("set-select") as HTMLSelectElement | null;
  if (setSelect) {
    setSelect.value = scene.levelSet;
    const filtered = S.sceneListCache.filter((s) => s.levelSet === scene.levelSet);
    dom.sceneSelect.innerHTML = '<option value="">— 选择关卡 —</option>';
    for (const s of filtered) {
      const opt = document.createElement("option");
      opt.value = s.assetPath;
      opt.textContent = s.sceneName;
      dom.sceneSelect.appendChild(opt);
    }
  }
  dom.sceneSelect.value = assetPath;
}

export function countDuplicateInstanceIds(list: LayoutItem[]): number {
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

export async function loadScene(assetPath: string) {
  showBusy("加载场景…");
  try {
    setStatus("加载场景…");
    S.scenePath = assetPath;
    S.currentLevelSet = levelSetFromScenePath(assetPath);
    window.history.replaceState(
      null,
      "",
      `/layout?scene=${encodeURIComponent(assetPath)}`
    );
    const doc = await fetchLayout(assetPath);
    const { dupIds, dedupedStacks } = await applyLayoutDocument(doc);
    const floorNote = S.floors.length > 0 ? `、${S.floors.length} 块地板` : "";
    if (dupIds > 0) {
      setStatus(
        `已加载 ${S.items.length} 个物体（有 ${dupIds} 个重复 ID，请重新编译 Unity 后点「重新加载」）`,
        false
      );
    } else if (dedupedStacks > 0) {
      setStatus(
        `已加载 ${S.items.length} 个物体${floorNote}（自动清除了 ${dedupedStacks} 个完全重叠的重复物品；请写回一次，Unity 侧的同位残留会被一并清理）`,
        false
      );
    } else {
      setStatus(`已加载 ${S.items.length} 个物体${floorNote}`);
    }
  } catch (e) {
    setStatus((e as Error).message, false);
  } finally {
    hideBusy();
  }
}

export interface ApplyDocOptions {
  /** 恢复/覆盖模式：不清 undo 栈、不 clearDirty，改为 markDirty（落盘交给用户手动写回）。 */
  markDirtyAfter?: boolean;
}

/** 把一份完整布局文档灌入编辑器状态并重绘（loadScene 与「恢复写回快照」共用管线；
 *  不负责取数，也不改 S.scenePath）。返回加载诊断计数供调用方组织状态栏文案。 */
export async function applyLayoutDocument(
  doc: LayoutDocument,
  opts?: ApplyDocOptions
): Promise<{ dupIds: number; dedupedStacks: number }> {
  const dupIds = countDuplicateInstanceIds(doc.items);
  // 过滤通用碰撞块（Col_Wall / Col_Floor 等场景辅助对象）：只有空气墙
  //（airWall=true，1×1×1.132）才作为核心层物品进入编辑器。
  S.items = doc.items
    .filter((raw) => !(raw.stubKind === "Collision" && raw.airWall !== true))
    .map((raw, index) => enrichItem(raw, `i${index}`));
  // 动画组必须先于 merge 赋值：merge*IntoFloors 里的「组成员跳过吸收」逻辑
  // 读取 S.animControls，若此时尚为空/旧值，移动岛的主题地砖会被吸收成地板
  // 矩形、写回时以新 id 挂到 Art 下重发射——永久脱离动画组（testice MidIsland
  // 岛分裂实证）。
  S.animControls = (doc.animControls ?? doc.moveControls)?.groups ?? [];
  S.floors = (doc.floors ?? []).map((raw, index) => enrichFloor(raw, `f${index}`));
  mergeRaftItemsIntoFloors();
  mergeThemedItemsIntoFloors();
  // 场景导出端自动去重：同 prefab 且 XYZ 完全同位（<0.01）的物品在画布上
  // 100% 重叠、视觉不可见（88 个重叠炮看起来就是 1 个）——历史克隆残留。
  // 这里保留首条、其余直接丢弃；下次写回由 Unity 侧 RemoveUnmatchedSceneItems
  // 清理场景残留，同时 Applier 的防堆叠守卫兜底。闭环：重新加载 → 自动去重 →
  // 写回 → 场景干净。注意：必须比较 Y——同 XZ 不同高度（多层关卡/墙面立柱）
  // 是合法摆设，不能误删；也必须在 S.items 赋值之后执行（旧版在赋值前对
  // 上一场景的残留数据去重，等于从未生效）。
  let dedupedStacks = 0;
  {
    const seenPos = new Map<string, { wx: number; wy: number; wz: number }>();
    S.items = S.items.filter((it) => {
      const key = it.prefabGuid ?? it.prefabAssetPath ?? "?";
      if (it._wx == null || it._wz == null) return true;
      const wy = it.worldPosition?.y ?? it.localPosition?.y ?? 0;
      const prev = seenPos.get(key);
      if (
        prev &&
        Math.abs(prev.wx - it._wx) < 0.01 &&
        Math.abs(prev.wz - it._wz) < 0.01 &&
        Math.abs(prev.wy - wy) < 0.01
      ) {
        dedupedStacks++;
        return false;
      }
      if (!prev) seenPos.set(key, { wx: it._wx, wy, wz: it._wz });
      return true;
    });
  }
  S.walkable = doc.walkable ?? [];
  S.deathInfo = doc.deathInfo ?? null;
  S.cameraInfo = doc.cameraInfo ?? null;
  S.lights = doc.lights ?? [];
  S.switchLinks = doc.switchLinks ?? [];
  // 旧式自定义触发名（switch_*）对机器目标已失效（真机不响应），归一化为原生触发名
  normalizeMachineLinkTriggers();
  S.buttonLinks = doc.buttonLinks?.links ?? [];
  S.buttonEvents = doc.buttonEvents?.links ?? [];
  cleanOrphanedAnimControls();
  cleanOrphanedButtonLinks();
  cleanOrphanedButtonEvents();
  // 自动去重/导出变更会移除物品：同步清理开关联动与上菜台/传送门/终端/加热炉
  // 的悬空绑定引用（否则写回时被 Unity 侧静默丢弃 → 绑定失效要重新绑）。
  cleanOrphanedStubRefs();
  const itemTheme = inferBgThemeFromItems(S.items);
  const deathThemeKey = bgThemeKeyForDeathType(S.deathInfo?.deathType);
  const sceneThemeKey = itemTheme ?? deathThemeKey;
  const savedTheme = localStorage.getItem("bgTheme:" + S.scenePath);
  const normalizedSaved =
    savedTheme === "lava" ? "void" : savedTheme;
  if (normalizedSaved && BG_THEMES.some((t) => t.key === normalizedSaved)) {
    S.bgThemeKey = normalizedSaved;
  } else {
    S.bgThemeKey = sceneThemeKey;
  }
  if (S.bgThemeKey === "lava") S.bgThemeKey = "void";
  S.bgThemeDirty = S.bgThemeKey !== sceneThemeKey;
  refreshUtensilStacks();
  S.gridInfo = await fetchGrid();
  S.floorMaterials = await fetchFloorMaterials(S.currentLevelSet).catch(() => []);
  repairFloorMaterialsFromCatalog();
  if (S.currentLayer === "floor") {
    buildFloorPalette((document.getElementById("palette-search") as HTMLInputElement)?.value ?? "", "floor");
  } else if (S.currentLayer === "background") {
    buildFloorPalette((document.getElementById("palette-search") as HTMLInputElement)?.value ?? "", "background");
  } else if (S.currentLayer === "anim") {
    dom.paletteCats.innerHTML = "";
  }
  clearSelection();
  S.marqueeing = false;
  resetOverlapMarqueePending();
  clearFloorSelection();
  hideDetail();
  if (opts?.markDirtyAfter) {
    // 恢复模式：保留 undo 栈（恢复前状态已 pushHistory），标脏等待用户手动写回。
    markDirty();
  } else {
    S.history.clear();
    clearDirty();
  }
  S.activeAnimGroupId = null;
  S.activeAnimEventIdx = null;
  S.selectedWaypointId = null;
  S.animMode = "none";
  S.activeAnimTab = "members";
  S.animPickTargetGroupId = null;
  S.collapsedGroupIds = new Set<string>();
  stopAnimPreview();
  S.expandedMemberId = null;
  if (S.currentLayer === "anim") S.activeRightTab = "anim";
  updatePanelTabButtons();
  draw();
  return { dupIds, dedupedStacks };
}

/** 与后端 LayoutEditorWriteBackHistory.LevelDirFor 同口径：set/levelId 推导。 */
function writeBackSetOfPath(sceneAssetPath: string): string {
  const parts = (sceneAssetPath ?? "").replace(/\\/g, "/").split("/");
  return parts.length > 2 && parts[1] === "LevelSets" ? parts[2] : "_misc";
}

function writeBackLevelIdOfPath(sceneAssetPath: string): string {
  const fileName = ((sceneAssetPath ?? "").split("/").pop() ?? "").replace(/\.unity$/, "");
  return fileName.startsWith("s_") && fileName.length > 2 ? fileName.slice(2) : fileName || "_unknown";
}

/** 把一条写回历史的完整快照恢复到画布（仅前端状态 + markDirty；落盘交给用户
 *  手动点击「💾 写回 Unity」）。全量重建：所有物品/地板换新 id，写回时先删后建、
 *  彻底覆盖，不受快照 u: 实例 id 与当前场景 id 漂移的影响；动画组成员/开关联动/
 *  按钮事件的 instanceId 引用同步经 idMap 重映射（否则会被 cleanOrphaned* 清洗掉）。 */
export async function restoreWriteBackSnapshot(
  record: string,
  side: "before" | "after",
  label?: string
): Promise<void> {
  if (!S.scenePath || !record) {
    setStatus("缺少当前场景或记录参数，无法恢复", false);
    return;
  }
  const sideText = side === "before" ? "这次操作之前" : "这次操作完成时";
  showBusy(`正在把画布恢复到${sideText}…`);
  try {
    const doc = await fetchWriteBackHistoryDoc(
      writeBackSetOfPath(S.scenePath),
      writeBackLevelIdOfPath(S.scenePath),
      record,
      side
    );
    const idMap = new Map<string, string>();
    doc.items = (doc.items ?? []).map((raw) => {
      const clone = JSON.parse(JSON.stringify(raw)) as LayoutItem;
      const nextId = `new:restore:${uuid()}`;
      if (clone.instanceId) idMap.set(clone.instanceId, nextId);
      clone.instanceId = nextId;
      clone.hierarchyPath = nextId;
      return clone;
    });
    doc.floors = (doc.floors ?? []).map((raw) => {
      const clone = JSON.parse(JSON.stringify(raw)) as FloorObject;
      const nextId = `new:floor:${uuid()}`;
      if (clone.instanceId) idMap.set(clone.instanceId, nextId);
      clone.instanceId = nextId;
      clone.hierarchyPath = nextId;
      return clone;
    });
    const mapId = (id?: string): string => (id != null && idMap.has(id) ? idMap.get(id)! : id ?? "");
    for (const it of doc.items) {
      if (it.teleportal) it.teleportal.exitPortalInstanceId = mapId(it.teleportal.exitPortalInstanceId);
      if (it.servingStation) {
        const ss = it.servingStation;
        if (ss.plateReturnInstanceId) ss.plateReturnInstanceId = mapId(ss.plateReturnInstanceId);
        if (ss.plateReturnInstanceIds) {
          ss.plateReturnInstanceIds = ss.plateReturnInstanceIds.map(mapId);
          ss.plateReturnInstanceId = ss.plateReturnInstanceIds[0] ?? "";
        }
      }
    }
    for (const g of doc.animControls?.groups ?? []) {
      g.itemInstanceIds = (g.itemInstanceIds ?? []).map(mapId);
      g.floorInstanceIds = (g.floorInstanceIds ?? []).map(mapId);
      // objectInstanceIds = 普通场景对象（不在 items/floors 内），idMap 覆盖不到，保持原值。
      for (const mo of g.memberOffsets ?? []) mo.instanceId = mapId(mo.instanceId);
      for (const ms of g.memberStatic ?? []) ms.instanceId = mapId(ms.instanceId);
      for (const mg of g.memberGroups ?? []) mg.memberInstanceIds = (mg.memberInstanceIds ?? []).map(mapId);
    }
    for (const l of doc.switchLinks ?? []) {
      l.switchId = mapId(l.switchId);
      l.targetId = mapId(l.targetId);
    }
    for (const l of doc.buttonLinks?.links ?? []) l.sourceId = mapId(l.sourceId);
    for (const l of doc.buttonEvents?.links ?? []) {
      l.sourceId = mapId(l.sourceId);
      for (const grp of l.groups ?? []) for (const ev of grp.events ?? []) ev.targetId = mapId(ev.targetId);
    }
    pushHistory(); // 恢复前的画布状态入 undo 栈（Ctrl+Z 可撤回一次）
    await applyLayoutDocument(doc, { markDirtyAfter: true });
    setStatus(
      `已把画布恢复到${sideText}的状态${label ? `（${label} 的记录）` : ""}：${S.items.length} 物品 / ${S.floors.length} 地板，` +
        `目前是未保存修改（可 Ctrl+Z 撤回）——确认后点「💾 写回 Unity」才会写入场景`
    );
  } catch (e) {
    setStatus((e as Error).message, false);
  } finally {
    hideBusy();
  }
}

/** 写回成功状态追加 Unity 侧透传的告警（绑定丢弃等）：状态栏保持"成功"色调，
 *  但明细可见——此前只进 Unity 日志，用户直到游戏里才发现绑定失效。 */
function applySaveStatus(base: string, warnings: string[]): string {
  if (!warnings.length) return base;
  const short = warnings
    .map((w) => w.replace(/^\[LayoutEditor\]\s*/, ""))
    .slice(0, 2);
  return `${base}；⚠ ${warnings.length} 条绑定告警：${short.join("；")}${warnings.length > short.length ? " 等" : ""}`;
}

export async function saveToUnity(only: SaveScope = ""): Promise<boolean> {
  showBusy("写回 Unity…");
  try {
    setStatus("写回中…");

    const collisions = checkPlayerCollisions();
    if (collisions.length > 0) {
      setStatus(`写回取消：玩家与物品存在碰撞 — ${collisions.join("；")}`, false);
      return false;
    }

    const wsCollisions = checkWorkstationCollisions();
    if (!S.allowWorkstationOverlap && wsCollisions.length > 0) {
      setStatus(`写回取消：工作台之间存在重叠 — ${wsCollisions.join("；")}`, false);
      return false;
    }

    // 写回前堆叠检测：同 guid 且 XYZ 完全同位（<0.01）的多条物品 = 重复数据
    //（画布上完全重叠、视觉不可见；历史导出/放置 bug 的残留）。直接写回会随
    //「写回→导出→再写回」循环成倍克隆（test12 大炮堆叠事故）。阻断并列出明细。
    // 注意：必须比较 Y——同 XZ 不同高度（多层关卡、墙面立柱等）是合法摆设，
    // 仅比 XZ 会把正常物品误判成堆叠（莲花烛误拦截事故）。
    {
      const seen = new Map<string, { label: string; wx: number; wy: number; wz: number }>();
      const stacks: string[] = [];
      for (const it of S.items) {
        const key = it.prefabGuid ?? it.prefabAssetPath ?? "?";
        const wx = it._wx;
        const wz = it._wz;
        if (wx == null || wz == null) continue;
        const wy = it.worldPosition?.y ?? it.localPosition?.y ?? 0;
        const prev = seen.get(key);
        if (
          prev &&
          Math.abs(prev.wx - wx) < 0.01 &&
          Math.abs(prev.wz - wz) < 0.01 &&
          Math.abs(prev.wy - wy) < 0.01
        ) {
          stacks.push(`${prev.label} 与 ${itemLabel(it)} 完全重叠`);
        } else if (!prev) {
          seen.set(key, { label: itemLabel(it), wx, wy, wz });
        }
      }
      if (stacks.length > 0) {
        setStatus(`写回取消：检测到 ${stacks.length} 处完全重叠的重复物品（请在画布选中删除多余一份）— ${stacks.slice(0, 4).join("；")}${stacks.length > 4 ? " 等" : ""}`, false);
        return false;
      }
    }

    if (only) {
      const warnings = await saveLayout(buildDocument(only), S.freeSnapStep, false, only);
      const scopeNote =
        only === "items"
          ? "仅核心物品，未修改地板/背景/装饰"
          : only === "decor"
            ? "仅装饰，未修改物品/地板/背景"
            : "仅地板/背景，未修改物品/装饰";
      setStatus(applySaveStatus(`写回成功（${scopeNote}）：请在 Unity Ctrl+S 保存场景`, warnings));
      S.history.clear();
      clearDirty();
      await loadScene(S.scenePath);
      return true;
    }

    const itemTheme = inferBgThemeFromItems(S.items);
    const deathThemeKey = bgThemeKeyForDeathType(S.deathInfo?.deathType);
    const sceneThemeKey = itemTheme ?? deathThemeKey;
    const expectedDeathType = bgTheme(S.bgThemeKey).deathType;
    const needsDeathWrite = S.deathInfo?.deathType !== expectedDeathType;
    const needsThemeWrite = S.bgThemeDirty || S.bgThemeKey !== sceneThemeKey || needsDeathWrite;

    const itemsBeforeSync = S.items.length;
    syncBackgroundForTheme(S.bgThemeKey);
    const addedBg = S.items.length > itemsBeforeSync;

    // 写回强校验：普通食材箱（含背包，不含饮料/酱料机）必须配 1 种食材；
    // 随机食材箱必须 ≥2 种候选。违规阻断写回并列出明细（后端同款兜底）。
    const violations: string[] = [];
    for (const it of S.items) {
      if (stubKindOf(it) !== "Dispenser" || !it.dispenser) continue;
      const pid = prefabIdFromPath(it.prefabAssetPath ?? "");
      if (
        pid === "dlc08_drink_machine" || pid === "dlc11_drink_dispenser" ||
        pid === "dlc08_condiment_dispenser" || pid === "dlc11_condiment_dispenser"
      )
        continue;
      const rndCount = it.dispenser.randomItemGuids?.length ?? 0;
      const isRandom = pid === "RandomDispenser" || rndCount > 0;
      if (isRandom) {
        if (rndCount < 2) violations.push(`${itemLabel(it)}（随机食材箱至少 2 种候选）`);
      } else if (!it.dispenser.spawnerItemPrefabGuid) {
        violations.push(`${itemLabel(it)}（普通食材箱未设置食材）`);
      }
    }
    if (violations.length > 0) {
      setStatus(`写回被阻断，请先修复 ${violations.length} 处：${violations.join("、")}`, true);
      return false;
    }

    const warnings = await saveLayout(buildDocument(""), S.freeSnapStep, S.autoWalkable, "");
    if (needsThemeWrite) {
      await setDeathTheme(S.scenePath, S.bgThemeKey);
    }
    const bounds = computeLevelBounds();
    if (bounds && S.autoKillPlane) {
      try {
        await setKillPlaneBounds(S.scenePath, bounds.cx, bounds.cz, bounds.sx, bounds.sz);
      } catch (kpErr) {
        setStatus(`坠落区配置失败：${(kpErr as Error).message}`, false);
      }
    }
    const themeNote = needsThemeWrite
      ? `，背景死亡效果已应用（${bgTheme(S.bgThemeKey).labelZh}）`
      : addedBg
        ? `，已补齐背景环境 prefab（${bgTheme(S.bgThemeKey).labelZh}）`
        : "";
    const walkNote = S.autoWalkable ? "，可行走碰撞体已按地板重新生成（地板间空隙=坠落坑）" : "";
    const killNote = bounds && S.autoKillPlane ? "：坠落区已覆盖整关，" : "：";
    setStatus(
      applySaveStatus(`写回成功${themeNote}${walkNote}${killNote}请在 Unity Ctrl+S 保存场景`, warnings)
    );
    S.bgThemeDirty = false;
    S.history.clear();
    clearDirty();
    await loadScene(S.scenePath);
    return true;
  } catch (e) {
    setStatus((e as Error).message, false);
    return false;
  } finally {
    hideBusy();
  }
}

export function openSyncLayoutDialog(): void {
  if (!S.scenePath) {
    setStatus("请先选择场景", false);
    return;
  }
  const others = S.sceneListCache.filter((s) => s.assetPath !== S.scenePath);
  if (!others.length) {
    setStatus("没有其他可同步的场景", false);
    return;
  }
  const opts = others
    .map(
      (s) =>
        `<option value="${escHtml(s.assetPath)}">${escHtml(s.levelSet)} / ${escHtml(s.sceneName)}</option>`
    )
    .join("");
  openModal(
    "同步其他关卡的布局",
    `<label class="m-field">来源关卡<select id="sync-src">${opts}</select></label>
     <p class="modal-hint" style="color:#f28b82">将把来源关卡的<b>道具、地板与背景主题</b>复制到当前图，<b>覆盖当前图的全部内容</b>。仅修改前端数据（写回 Unity 后才落盘），可用 Ctrl+Z 撤回一次。</p>`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn danger" data-ok>覆盖并同步</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", () => {
    const sel = document.getElementById("sync-src") as HTMLSelectElement;
    closeModal();
    void syncLayoutFromScene(sel.value);
  });
}

export async function syncLayoutFromScene(otherPath: string): Promise<void> {
  showBusy("读取来源场景布局…");
  try {
    const doc = await fetchLayout(otherPath);
    pushHistory();
    const keepPlayers = S.items.filter(isPlayerItem);
    const idMap = new Map<string, string>();
    S.items = doc.items
      .filter((raw) => prefabIdFromPath(raw.prefabAssetPath) !== "Player")
      .filter((raw) => !(raw.stubKind === "Collision" && raw.airWall !== true))
      .map((raw) => {
        const it = enrichItem(JSON.parse(JSON.stringify(raw)) as LayoutItem, newEditorKey());
        const nextId = `new:sync:${uuid()}`;
        if (raw.instanceId) idMap.set(raw.instanceId, nextId);
        it.instanceId = nextId;
        it.hierarchyPath = nextId;
        return it;
      });
    S.items.push(...keepPlayers);
    for (const it of S.items) {
      const exitId = it.teleportal?.exitPortalInstanceId;
      if (exitId && idMap.has(exitId) && it.teleportal) {
        it.teleportal.exitPortalInstanceId = idMap.get(exitId)!;
      }
      const prId = it.servingStation?.plateReturnInstanceId;
      if (prId && idMap.has(prId) && it.servingStation) {
        it.servingStation.plateReturnInstanceId = idMap.get(prId)!;
      }
      // Remap one-to-many return bindings.
      if (it.servingStation?.plateReturnInstanceIds) {
        it.servingStation.plateReturnInstanceIds = it.servingStation.plateReturnInstanceIds.map(
          (id) => (idMap.has(id) ? idMap.get(id)! : id)
        );
        it.servingStation.plateReturnInstanceId = it.servingStation.plateReturnInstanceIds[0] ?? "";
      }
    }
    S.floors = (doc.floors ?? []).map((raw) => {
      const f = enrichFloor(JSON.parse(JSON.stringify(raw)) as FloorObject, newEditorKey());
      f.instanceId = `new:floor:${uuid()}`;
      f.hierarchyPath = f.instanceId;
      return f;
    });
    mergeRaftItemsIntoFloors();
    mergeThemedItemsIntoFloors();
    const theme = inferBgThemeFromItems(S.items);
    if (theme) {
      S.bgThemeKey = theme;
      S.bgThemeDirty = true;
      localStorage.setItem("bgTheme:" + S.scenePath, S.bgThemeKey);
    }
    clearSelection();
    clearFloorSelection();
    hideDetail();
    hideContextMenu();
    markDirty();
    draw();
    setStatus(
      `已同步 ${S.items.length} 个道具、${S.floors.length} 块地板（覆盖当前图，写回 Unity 后生效，可 Ctrl+Z 撤回）`
    );
  } catch (e) {
    setStatus((e as Error).message, false);
  } finally {
    hideBusy();
  }
}

let bridgeWatchSuspended = false;

/** 暂停桥接健康探测。导出关卡集（Build AssetBundles）会阻塞 Unity 主线程泵数分钟，
 *  期间 /api/health 得不到响应，探测会误报“后台服务已停止”；长任务前挂起、结束后恢复。 */
export function suspendBridgeWatch(): void {
  bridgeWatchSuspended = true;
}

export function resumeBridgeWatch(): void {
  bridgeWatchSuspended = false;
  S.bridgeWasUp = true;
  S.bridgeFailCount = 0;
  S.bridgeStopAlerted = false;
}

export function startBridgeWatch() {
  S.bridgeWasUp = true;
  S.bridgeStopAlerted = false;
  S.bridgeFailCount = 0;
  window.setInterval(async () => {
    if (bridgeWatchSuspended) return;
    const up = await fetchHealth();
    if (up) {
      S.bridgeFailCount = 0;
      S.bridgeStopAlerted = false;
    } else if (S.bridgeWasUp) {
      S.bridgeFailCount++;
      if (S.bridgeFailCount >= 3 && !S.bridgeStopAlerted) {
        S.bridgeStopAlerted = true;
        showBridgeStoppedModal();
        setStatus("未连接 Unity（后台服务已停止）", false);
      }
    }
    S.bridgeWasUp = up;
  }, 3000);
}

export function showBridgeStoppedModal() {
  openModal(
    "后台服务已停止",
    `<p>Layout Editor 的后台 Bridge 服务已断开。</p>
     <p>最常见的原因是 <b>Unity 进入了 Play 模式</b>（Play 时编辑器服务会暂停），也可能是服务被手动停止。</p>
     <p>请退出 Play 模式后，在 Unity <b>Tools → Layout Editor → Start Server</b> 重新启动，然后刷新本页。</p>`,
    `<button type="button" class="modal-btn primary" data-ok>知道了</button>`
  );
  document.querySelector("[data-ok]")?.addEventListener("click", closeModal);
}

export function confirmLeaveIfDirty(action: () => void): void {
  if (!S.dirty) {
    action();
    return;
  }
  openModal(
    "有未保存的修改",
    `<p>当前关卡的布局修改尚未写回 Unity，离开后修改将丢失。</p>`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn danger" data-leave>直接离开</button>
     <button type="button" class="modal-btn primary" data-save>写回并离开</button>`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", () => {
    closeModal();
    selectSceneInDropdowns(S.scenePath);
  });
  document.querySelector("[data-leave]")?.addEventListener("click", () => {
    closeModal();
    action();
  });
  document.querySelector("[data-save]")?.addEventListener("click", () => {
    closeModal();
    void saveToUnity().then((ok) => {
      if (ok) action();
      else selectSceneInDropdowns(S.scenePath);
    });
  });
}

export function warnIfBridgeOutdated(health: import("../api").HealthInfo, catalogSchemaVersion: number) {
  if (!health.ok) return;
  const reasons: string[] = [];
  if ((health.schemaVersion ?? 1) < catalogSchemaVersion)
    reasons.push(`桥接版本 v${health.schemaVersion ?? 1} < 资源目录 v${catalogSchemaVersion}`);
  if (health.knowledgeLoaded === false) reasons.push("缺少 recipe-knowledge.json");
  if (health.dictionaryLoaded === false) reasons.push("缺少 names-dictionary.json");
  if (reasons.length === 0) return;
  setStatus(
    `桥接端资源数据过旧（${reasons.join("；")}），请升级 Unity 工程中的 layout-editor 文件并重启 Bridge`,
    false
  );
}
