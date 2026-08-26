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
import { stubKindOf } from "./stubControls";
import { cleanOrphanedMoveControls, stopMovePreview } from "./moveControl";
import { cleanOrphanedButtonLinks } from "./buttonLinks";
import { cleanOrphanedButtonEvents } from "./buttonEvents";
import { itemLabel } from "./labels";
import {
  syncBackgroundForTheme,
  mergeRaftItemsIntoFloors,
  mergeThemedItemsIntoFloors
} from "./floors";
import { buildFloorPalette } from "./floorPalette";
import { buildDocument } from "./serialize";
import { clearSelection, clearFloorSelection } from "./selection";
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
  setDeathTheme,
  setKillPlaneBounds,
  fetchHealth
} from "../api";
import type {
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
      `${location.pathname}?scene=${encodeURIComponent(assetPath)}${location.hash}`
    );
    const doc = await fetchLayout(assetPath);
    const dupIds = countDuplicateInstanceIds(doc.items);
    // 过滤通用碰撞块（Col_Wall / Col_Floor 等场景辅助对象）：只有空气墙
    // （airWall=true，1×1×1.132）才作为核心层物品进入编辑器。
    S.items = doc.items
      .filter((raw) => !(raw.stubKind === "Collision" && raw.airWall !== true))
      .map((raw, index) => enrichItem(raw, `i${index}`));
    // 移动组必须先于 merge 赋值：merge*IntoFloors 里的「组成员跳过吸收」逻辑
    // 读取 S.moveControls，若此时尚为空/旧值，移动岛的主题地砖会被吸收成地板
    // 矩形、写回时以新 id 挂到 Art 下重发射——永久脱离移动组（testice MidIsland
    // 岛分裂实证）。
    S.moveControls = doc.moveControls?.groups ?? [];
    S.floors = (doc.floors ?? []).map((raw, index) => enrichFloor(raw, `f${index}`));
    mergeRaftItemsIntoFloors();
    mergeThemedItemsIntoFloors();
    S.walkable = doc.walkable ?? [];
    S.deathInfo = doc.deathInfo ?? null;
    S.cameraInfo = doc.cameraInfo ?? null;
    S.lights = doc.lights ?? [];
    S.switchLinks = doc.switchLinks ?? [];
    S.buttonLinks = doc.buttonLinks?.links ?? [];
    S.buttonEvents = doc.buttonEvents?.links ?? [];
    cleanOrphanedMoveControls();
    cleanOrphanedButtonLinks();
    cleanOrphanedButtonEvents();
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
    if (S.currentLayer === "floor") {
      buildFloorPalette((document.getElementById("palette-search") as HTMLInputElement)?.value ?? "", "floor");
    } else if (S.currentLayer === "background") {
      buildFloorPalette((document.getElementById("palette-search") as HTMLInputElement)?.value ?? "", "background");
    } else if (S.currentLayer === "move") {
      dom.paletteCats.innerHTML = "";
    }
    clearSelection();
    S.marqueeing = false;
    clearFloorSelection();
    hideDetail();
    S.history.clear();
    clearDirty();
    S.activeMoveGroupId = null;
    S.activeMoveEventIdx = null;
    S.selectedWaypointId = null;
    S.moveMode = "none";
    S.activeMoveTab = "members";
    S.movePickTargetGroupId = null;
    S.collapsedGroupIds = new Set<string>();
    stopMovePreview();
    S.expandedMemberId = null;
    if (S.currentLayer === "move") S.activeRightTab = "move";
    updatePanelTabButtons();
    draw();
    const floorNote = S.floors.length > 0 ? `、${S.floors.length} 块地板` : "";
    if (dupIds > 0) {
      setStatus(
        `已加载 ${S.items.length} 个物体（有 ${dupIds} 个重复 ID，请重新编译 Unity 后点「重新加载」）`,
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

    if (only) {
      await saveLayout(buildDocument(only), S.freeSnapStep, false, only);
      const scopeNote =
        only === "items"
          ? "仅核心物品，未修改地板/背景/装饰"
          : only === "decor"
            ? "仅装饰，未修改物品/地板/背景"
            : "仅地板/背景，未修改物品/装饰";
      setStatus(`写回成功（${scopeNote}）：请在 Unity Ctrl+S 保存场景`);
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

    const emptyDispensers = S.items.filter(
      (it) => stubKindOf(it) === "Dispenser" && !it.dispenser?.spawnerItemPrefabGuid
    );
    if (emptyDispensers.length > 0) {
      const names = emptyDispensers.map((it) => itemLabel(it)).join("、");
      setStatus(`以下食材箱未设置食材，将配置为空食材箱（不报错）：${names}`, true);
    }

    await saveLayout(buildDocument(""), S.freeSnapStep, S.autoWalkable, "");
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
      `写回成功${themeNote}${walkNote}${killNote}请在 Unity Ctrl+S 保存场景`
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
