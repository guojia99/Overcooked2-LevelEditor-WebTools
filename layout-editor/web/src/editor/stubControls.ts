import {
  S,
  EditorItem
} from "./state";
import { dom } from "./dom";
import {
  prefabIdFromPath,
  escHtml
} from "./coords";
import { itemLabel } from "./labels";
import { pushHistory } from "./historyOps";
import { draw } from "./render";
import { setStatus } from "./status";
import { hideContextMenu } from "./ui/overlay";
import {
  openFoodSpawnerEditor,
  openIngredientMultiPicker
} from "../modals";
import { ingredientOptionLabel, visibleIngredients } from "../ingredientLabels";
import type { IngredientEntry } from "../types";
import {
  counterTypeOfItem,
  counterAppearanceOptions
} from "./catalog";
import {
  isGlassReturnItem,
  isMugReturnItem,
  isTrayReturnItem,
  plateReturns,
  glassReturns,
  mugReturns,
  trayReturns,
  computeReturnLabels,
  servingPlateReturn,
  servingGlassReturn,
  servingMugReturn,
  servingTrayReturn,
  setServingReturnOfType,
  servingStationsForReturn
} from "./servingLinks";
import { teleportals } from "./renderItems";
import {
  computeParamLabels,
  computeTeleportalLabels
} from "./renderItems";
import {
  PORTAL_COLOR_NAMES,
  BURNER_FIRE_MODES
} from "./ui/constants";
import {
  buttonLinkSummaryHtml,
  openButtonLinkModal,
  isButtonLinkSource
} from "./buttonLinks";
import {
  buttonEventSummaryHtml,
  openButtonEventModal,
  isButtonEventSource
} from "./buttonEvents";

export const STUB_KIND_BY_PREFAB_ID: Record<string, string> = {
  Dispenser: "Dispenser",
  Backpack: "Dispenser",
  dlc08_drink_machine: "Dispenser",
  dlc11_drink_dispenser: "Dispenser",
  dlc08_condiment_dispenser: "Dispenser",
  dlc11_condiment_dispenser: "Dispenser",
  AttachingFoodSpawner: "AttachingFoodSpawner",
  ConveyorStation: "Conveyor",
  Teleportal: "Teleportal",
  Pot: "CookingUtensil",
  FryPan: "CookingUtensil",
  Steamer: "CookingUtensil",
  FrierBasket: "CookingUtensil",
  MixerBowl: "CookingUtensil",
  ToastingFork: "CookingUtensil",
  GriddlePan: "CookingUtensil",
  Skewer: "CookingUtensil",
  // Blender 是搅拌机工作站（counters/service，SO=workstation_blender_01），不是锅具容器
  // （真实 prefab 无 IngredientContainer，误配成锅具运行时会 NRE）；便携搅拌机是 utensil_blender_01。
  BlenderCup: "CookingUtensil",
  // common03 厨具变体 wrapper（右键「锅具参数/额外食材」面板与换肤参数迁移依赖此表）
  utensil_large_pot_01: "CookingUtensil",
  utensil_dlc10_large_pot_01: "CookingUtensil",
  utensil_roasting_tray: "CookingUtensil",
  dlc09_utensil_roasting_tray: "CookingUtensil",
  dlc02_utensil_frying_pan: "CookingUtensil",
  dlc05_utensil_frying_pan: "CookingUtensil",
  dlc07_utensil_frying_pan_01: "CookingUtensil",
  dlc08_utensil_frying_pan: "CookingUtensil",
  dlc09_utensil_frying_pan: "CookingUtensil",
  dlc03_utensil_pot: "CookingUtensil",
  dlc07_utensil_pot_01: "CookingUtensil",
  dlc08_utensil_pot_01: "CookingUtensil",
  dlc09_utensil_pot: "CookingUtensil",
  dlc02_utensil_mixer: "CookingUtensil",
  dlc03_utensil_mixer: "CookingUtensil",
  dlc05_utensil_mixer: "CookingUtensil",
  dlc07_utensil_mixer_01: "CookingUtensil",
  dlc08_utensil_mixer_01: "CookingUtensil",
  dlc09_utensil_mixer: "CookingUtensil",
  dlc13_utensil_mixer_01: "CookingUtensil",
  dlc08_frierbasket: "CookingUtensil",
  utensil_griddlepan: "CookingUtensil",
  utensil_skewer_01: "CookingUtensil",
  utensil_toasting_fork_01: "CookingUtensil",
  utensil_blender_01: "CookingUtensil",
  CleanPlateStack: "CleanPlateStack",
  CleanGlassStack: "CleanPlateStack",
  // 容器堆变体（wrapper 无专属 stub，靠 stubKind 让后端补挂组件并推断容器 SO）
  dlc08_cleantraystack: "CleanPlateStack",
  cleanmugstack: "CleanPlateStack",
  cleanglassstack: "CleanPlateStack",
  dlc11_cleanglassstack: "CleanPlateStack",
  // 脏堆变体：自包含 DirtyPlateStack（真实 prefab 无独立盘子），不走 CleanPlateStack
  // 实例化盘子（否则 Setup 对无 EditorGridSnap 的盘子 NRE），保持普通道具。
  Travelator: "Travelator",
  Flamethrower: "Flamethrower",
  Burner: "Burner",
  Player: "Player",
  ServingStation: "ServingStation",
  PlateReturn: "PlateReturn",
  GlassReturn: "GlassReturn",
  // 回收台/上菜台 DLC 变体（wrapper 无专属 stub，靠 stubKind 让后端补挂组件）
  workstation_mug_return: "PlateReturn",
  dlc09_workstation_mug_return_winter: "PlateReturn",
  dlc13_workstation_plate_return: "PlateReturn",
  dlc08_workstation_tray_return: "PlateReturn",
  dlc11_workstation_glass_return_01: "GlassReturn",
  workstation_glass_return_01: "GlassReturn",
  dlc13_workstation_plate_station: "ServingStation",
  Switch: "Switch",
  // DLC8 按钮（断头台/果汁机/酱料机的触发开关）按 Switch 处理
  p_dlc08_button_drinks: "Switch",
  p_dlc08_button_condiments: "Switch",
  PressureSwitch: "PressureSwitch",
  MultiControlTerminal: "Terminal",
};

/** 酱料机可输出的酱料（黄芥末酱 / 番茄酱，含 DLC11 换皮，逻辑一致）。 */
/** 特殊分配器（饮料机/酱料机）按 DLC 可输出的食材（实测 bundle 组件，dlc 各自一套）：
 *  dlc08_drink_machine → 饮料1/2/3；dlc11_drink_dispenser → 橙味汽水+沙士汽水；
 *  dlc08_condiment_dispenser → 番茄酱+芥末酱；dlc11_condiment_dispenser → dlc11 番茄酱/芥末酱。 */
const DISPENSER_INGREDIENT_IDS: Record<string, Set<string>> = {
  dlc08_drink_machine: new Set(["drink01", "drink02", "drink03"]),
  dlc11_drink_dispenser: new Set(["orangesoda", "rootbeer"]),
  dlc08_condiment_dispenser: new Set(["ketchup", "mustard"]),
  dlc11_condiment_dispenser: new Set(["dlc11_ketchup", "dlc11_mustard"]),
};

/** 该分配器允许输出的食材 id 集合（非特殊分配器返回 null = 全部食材可选）。 */
function specialDispenserAllowedIds(item: EditorItem): Set<string> | null {
  const pid = prefabIdFromPath(item.prefabAssetPath);
  return DISPENSER_INGREDIENT_IDS[pid] ?? null;
}

/** 普通食材箱的禁选判定：node 型食材（无实体 prefab，如沙拉洋葱/汽水）放入食材箱会在
 *  运行时 PseudoPrefabDispenser.Setup 按 GameObject 加载失败而崩溃——只能经加工
 *  （如切洋葱）或专属机器（饮料机/酱料机）产出。 */
function crateIngredientDisabled(ing: IngredientEntry): string | null {
  if (ing.nodeOnly) return "node 型食材（无实体 prefab），食材箱无法生成（经加工或专属机器产出）";
  return null;
}

function specialDispenserType(item: EditorItem): "condiment" | "drink" | "" {
  const pid = prefabIdFromPath(item.prefabAssetPath);
  if (pid === "dlc08_condiment_dispenser" || pid === "dlc11_condiment_dispenser") return "condiment";
  if (pid === "dlc08_drink_machine" || pid === "dlc11_drink_dispenser") return "drink";
  return "";
}

export function stubKindOf(item: EditorItem): string {
  if (item.stubKind) return item.stubKind;
  const prefabId = prefabIdFromPath(item.prefabAssetPath);
  return STUB_KIND_BY_PREFAB_ID[prefabId] ?? "";
}

export function isCollisionItem(it: EditorItem): boolean {
  return stubKindOf(it) === "Collision";
}

/** 原版厨具容量（bundle 实测 IngredientContainer.m_capacity）：
 *  汤锅族=3（多食材组合）、煎锅/炸篮/蒸锅等=1、搅拌碗/搅拌杯/烤盘=4、烤串=3、
 *  火锅大锅=4（bundle226）、烤菜烤盘=4（bundle297）。子串规则与后端
 *  NativeUtensilCapacity 一致（large_pot/roasting_tray 先于 pot 判断）。 */
export function defaultUtensilCapacity(item: EditorItem): number {
  const prefabId = (prefabIdFromPath(item.prefabAssetPath) ?? "").toLowerCase();
  if (prefabId.includes("large_pot")) return 4;
  if (prefabId.includes("roasting_tray")) return 4;
  if (prefabId.includes("pot")) return 3;
  if (
    prefabId.includes("mixer") ||
    prefabId.includes("blender") ||
    prefabId.includes("griddle")
  )
    return 4;
  if (prefabId.includes("skewer")) return 3;
  return 1;
}

/** autofill 装填锅具时的容量兜底：未设置 → 原版默认；
 *  旧版 autofill 曾把汤锅默认成 1（放不进 3 种食材），凡容量仍为 1 且原版默认
 *  非 1 的（仅 Pot）视为历史污染，纠正回原版默认。用户手动设置的其他值不动。 */
export function utensilCapacityOrFix(item: EditorItem): number {
  const def = defaultUtensilCapacity(item);
  const cur = item.cookingUtensil?.capacity;
  if (cur == null) return def;
  if (def !== 1 && cur === 1) return def;
  return cur;
}

export function counterAppearanceHtml(item: EditorItem): string {
  const options = counterAppearanceOptions(item);
  if (!options.length) return "";
  const cur = item.pseudoPrefabGuid ?? "";
  const nameLookup = new Map(options.map((o) => [o.guid, o]));
  const curName = nameLookup.get(cur)?.nameZh ?? (cur ? "未知外观" : "默认外观");
  const opts = ['<option value="">— 默认外观 —</option>']
    .concat(
      options.map(
        (o) => `<option value="${o.guid}" ${o.guid === cur ? "selected" : ""}>${escHtml(o.nameZh || o.id)}</option>`
      )
    )
    .join("");
  const ct = counterTypeOfItem(item);
  const typeName = S.counterAppearances?.typeNames[ct!] ?? ct ?? "桌台";
  return `<div class="ctx-stub"><div class="ctx-stub-title">${typeName}外观</div>
    <label class="ctx-stub-row">外观 <select id="ctx-appear" class="ctx-input">${opts}</select></label>
    <div class="ctx-stub-row" style="font-size:11px;color:#8a909a">当前：${escHtml(curName)}</div></div>`;
}

export function switchMaterialHtml(item: EditorItem): string {
  if (!S.switchMaterialsCache.length) return "";
  const mats = S.switchMaterialsCache;
  const sw = item.switchStub ?? {};
  const activeOpts = ['<option value="">— 默认 —</option>']
    .concat(
      mats.map(
        (m) => `<option value="${m.guid}" ${(sw.activeMaterialGuid ?? "") === m.guid ? "selected" : ""}>${escHtml(m.nameZh || m.id)}</option>`
      )
    )
    .join("");
  const inactiveOpts = ['<option value="">— 默认 —</option>']
    .concat(
      mats.map(
        (m) => `<option value="${m.guid}" ${(sw.inactiveMaterialGuid ?? "") === m.guid ? "selected" : ""}>${escHtml(m.nameZh || m.id)}</option>`
      )
    )
    .join("");
  return `<label class="ctx-stub-row">未按下外观 <select id="ctx-sw-active" class="ctx-input">${activeOpts}</select></label>
    <label class="ctx-stub-row">按下外观 <select id="ctx-sw-inactive" class="ctx-input">${inactiveOpts}</select></label>`;
}

export function pressureSwitchMaterialHtml(item: EditorItem): string {
  if (!S.switchMaterialsCache.length) return "";
  const mats = S.switchMaterialsCache;
  const ps = item.pressureSwitch ?? {};
  const occOpts = ['<option value="">— 默认 —</option>']
    .concat(
      mats.map(
        (m) => `<option value="${m.guid}" ${(ps.occupiedMaterialGuid ?? "") === m.guid ? "selected" : ""}>${escHtml(m.nameZh || m.id)}</option>`
      )
    )
    .join("");
  const unoccOpts = ['<option value="">— 默认 —</option>']
    .concat(
      mats.map(
        (m) => `<option value="${m.guid}" ${(ps.unoccupiedMaterialGuid ?? "") === m.guid ? "selected" : ""}>${escHtml(m.nameZh || m.id)}</option>`
      )
    )
    .join("");
  return `<label class="ctx-stub-row">按下外观 <select id="ctx-ps-occ" class="ctx-input">${occOpts}</select></label>
    <label class="ctx-stub-row">松开外观 <select id="ctx-ps-unocc" class="ctx-input">${unoccOpts}</select></label>`;
}

export function stubControlsHtml(item: EditorItem): string {
  const kind = stubKindOf(item);
  computeParamLabels(); // refresh per-type sequence numbers so menus match the canvas
  switch (kind) {
    case "Dispenser": {
      const stype = specialDispenserType(item);
      const title = stype === "condiment" ? "酱料机参数" : stype === "drink" ? "饮料机参数" : "食材箱参数";
      const fieldLabel = stype === "condiment" ? "酱料" : stype === "drink" ? "饮料" : "食材";
      if (stype) {
        // 酱料机/饮料机：多选（游戏内开关循环切换，顺序即切换顺序）；
        // 多选列表存 item.soArray.pseudoPrefabGuids（PseudoPrefabSOArray），不选 = prefab 内置列表。
        const guids = item.soArray?.pseudoPrefabGuids?.length
          ? item.soArray.pseudoPrefabGuids
          : item.dispenser?.spawnerItemPrefabGuid
            ? [item.dispenser.spawnerItemPrefabGuid]
            : [];
        const names = guids.map((g) => {
          const ing = S.ingredientsCache.find((i) => i.guid === g);
          return ing ? escHtml(ingredientOptionLabel(ing)) : "?";
        });
        return `<div class="ctx-stub"><div class="ctx-stub-title">${title}</div>
          <div class="ctx-stub-row" style="font-size:11px">${names.length ? names.join(" → ") : "— 默认（机器内置列表）—"}</div>
          <button type="button" class="ctx-btn" id="ctx-stub-ing-pick">选择${fieldLabel}（可多选）…</button>
          <div class="ctx-stub-row" style="font-size:11px;color:#8a909a">多选后，联动的开关按下时按顺序循环切换${fieldLabel}；不选则使用机器内置列表</div></div>`;
      }
      const cur = item.dispenser?.spawnerItemPrefabGuid ?? "";
      const curIng = cur ? S.ingredientsCache.find((i) => i.guid === cur) : undefined;
      return `<div class="ctx-stub"><div class="ctx-stub-title">${title}</div>
        <label class="ctx-stub-row">${fieldLabel} <span class="ctx-input" style="opacity:${curIng ? 1 : 0.6}">${curIng ? escHtml(ingredientOptionLabel(curIng)) : "— 未设置 —"}</span>
        <button type="button" class="ctx-btn" id="ctx-stub-ing-pick">选择…</button></label>
        </div>`;
    }
    case "AttachingFoodSpawner": {
      const fs = item.foodSpawner ?? {};
      return `<div class="ctx-stub"><div class="ctx-stub-title">食材生成器参数</div>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-fs-order" ${fs.spawnInOrder !== false ? "checked" : ""}/> 按顺序生成</label>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-fs-start" ${fs.triggerAtStart !== false ? "checked" : ""}/> 开局触发</label>
        <label class="ctx-stub-row">触发间隔 <input type="number" id="ctx-fs-time" class="ctx-input" step="0.5" min="0" value="${fs.triggerTime ?? 5}"/> 秒</label>
        <button type="button" class="ctx-btn" id="ctx-fs-ings">食材列表…</button></div>`;
    }
    case "CookingUtensil": {
      const cu = item.cookingUtensil ?? {};
      const allowed = (cu.allowedIngredientGuids ?? []).length;
      return `<div class="ctx-stub"><div class="ctx-stub-title">厨具参数</div>
        <label class="ctx-stub-row">最多食材数 <input type="number" id="ctx-cu-cap" class="ctx-input" min="0" step="1" value="${cu.capacity ?? defaultUtensilCapacity(item)}"/></label>
        <button type="button" class="ctx-btn" id="ctx-cu-ings">额外食材 (${allowed > 0 ? `${allowed} 种` : "无 · 处理所有主线食材"})…</button></div>`;
    }
    case "Conveyor": {
      const sp = item.conveyor?.conveySpeed ?? 0.5;
      return `<div class="ctx-stub"><div class="ctx-stub-title">传送带参数</div>
        <label class="ctx-stub-row">速度 <input type="number" id="ctx-cv-speed" class="ctx-input" step="0.1" value="${sp}"/>（负值反向）</label></div>`;
    }
    case "Teleportal": {
      const tp = item.teleportal ?? {};
      S.teleportalLabels = computeTeleportalLabels();
      const colorOpts = PORTAL_COLOR_NAMES.map(
        (n, i) => `<option value="${i}" ${(tp.portalColor ?? 0) === i ? "selected" : ""}>${n}</option>`
      ).join("");
      const others = teleportals().filter((t) => t._editorKey !== item._editorKey);
      const exitOpts = ['<option value="">— 未绑定 —</option>']
        .concat(
          others.map(
            (t) =>
              `<option value="${t.instanceId}" ${tp.exitPortalInstanceId === t.instanceId ? "selected" : ""}>传送门 ${S.teleportalLabels.get(t.instanceId) ?? "?"}（${escHtml(itemLabel(t))}）</option>`
          )
        )
        .join("");
      return `<div class="ctx-stub"><div class="ctx-stub-title">传送门参数</div>
        <label class="ctx-stub-row">颜色 <select id="ctx-tp-color" class="ctx-input">${colorOpts}</select></label>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-tp-ds" ${tp.doubleSided ? "checked" : ""}/> 双向</label>
        <label class="ctx-stub-row">出口 <select id="ctx-tp-exit" class="ctx-input">${exitOpts}</select></label></div>`;
    }
    case "Travelator": {
      const sp = item.travelator?.speed ?? 2.5;
      return `<div class="ctx-stub"><div class="ctx-stub-title">移动地板参数</div>
        <label class="ctx-stub-row">速度 <input type="number" id="ctx-tv-speed" class="ctx-input" step="0.1" min="0" value="${sp}"/></label></div>`;
    }
    case "Flamethrower": {
      const rate = item.flamethrower?.cookingRate ?? 4;
      return `<div class="ctx-stub"><div class="ctx-stub-title">喷火器参数</div>
        <label class="ctx-stub-row">烹饪速率 <input type="number" id="ctx-ft-rate" class="ctx-input" step="0.5" min="0" value="${rate}"/></label></div>`;
    }
    case "CleanPlateStack": {
      const ps = item.cleanPlateStack ?? {};
      const title = prefabIdFromPath(item.prefabAssetPath) === "CleanGlassStack" ? "杯堆参数" : "盘子堆参数";
      return `<div class="ctx-stub"><div class="ctx-stub-title">${title}</div>
        <label class="ctx-stub-row">数量 <input type="number" id="ctx-ps-count" class="ctx-input" min="0" step="1" value="${ps.plateCount ?? 5}"/></label></div>`;
    }
    case "Burner": {
      const b = item.burner ?? {};
      const modeOpts = BURNER_FIRE_MODES.map(
        (n, i) => `<option value="${i}" ${(b.fireMode ?? 1) === i ? "selected" : ""}>${n}</option>`
      ).join("");
      return `<div class="ctx-stub"><div class="ctx-stub-title">火焰喷射器参数</div>
        <label class="ctx-stub-row">开火模式 <select id="ctx-bn-mode" class="ctx-input">${modeOpts}</select></label>
        <label class="ctx-stub-row">空中时间 <input type="number" id="ctx-bn-air" class="ctx-input" step="0.1" min="0" value="${b.airTime ?? 2}"/> 秒</label>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-bn-rand" ${b.randomTargetOrder ? "checked" : ""}/> 随机目标顺序</label>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-bn-hide" ${b.hideVisual ? "checked" : ""}/> 隐藏模型</label></div>`;
    }
    case "Switch": {
      const sw = item.switchStub ?? {};
      const matHtml = switchMaterialHtml(item);
      return `<div class="ctx-stub"><div class="ctx-stub-title">开关参数</div>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-sw-start" ${sw.startEnabled !== false ? "checked" : ""}/> 初始开启</label>
        ${matHtml}
        <div class="ctx-stub-title" style="margin-top:6px">联动目标（断头台/饮料机等）</div>
        <div id="ctx-sw-links"></div>
        <label class="ctx-stub-row"><select id="ctx-sw-linktarget" class="ctx-input"></select>
          <button type="button" class="ctx-btn" id="ctx-sw-linkadd">添加</button></label>
        <label class="ctx-stub-row">触发消息 <input id="ctx-sw-trigger" class="ctx-input" value="Switch" placeholder="Switch"/></label>
        <div class="ctx-stub-row" style="font-size:11px;color:#8a909a">按下按钮时对目标对象广播该消息（默认 Switch）</div>
        ${buttonEventSummaryHtml(item)}
        ${buttonLinkSummaryHtml(item)}</div>`;
    }
    case "PressureSwitch": {
      const matHtml = pressureSwitchMaterialHtml(item);
      return `<div class="ctx-stub"><div class="ctx-stub-title">压力开关参数</div>
        ${matHtml || '<div class="ctx-stub-row">此物件无用户可配置参数，配置内置于预制件中</div>'}
        ${buttonEventSummaryHtml(item)}
        ${buttonLinkSummaryHtml(item)}</div>`;
    }
    case "Terminal": {
      const t = item.terminal ?? {};
      const targetId = t.pilotableObjectInstanceId;
      let targetOpts = ['<option value="">— 未绑定 —</option>'];
      for (const i of S.items) {
        const kind = stubKindOf(i);
        if (kind === "Player") continue;
        if (!i.instanceId) continue;
        targetOpts.push(`<option value="${i.instanceId}" ${targetId === i.instanceId ? "selected" : ""}>${escHtml(itemLabel(i))}</option>`);
      }
      return `<div class="ctx-stub"><div class="ctx-stub-title">控制终端参数</div>
        <label class="ctx-stub-row">控制目标 <select id="ctx-tm-target" class="ctx-input">${targetOpts.join("")}</select></label>
        <div class="ctx-stub-row" style="font-size:11px;color:#8a909a">选择一个场景物件作为控制终端的目标</div></div>`;
    }
    case "Player": {
      return `<div class="ctx-stub"><div class="ctx-stub-title">玩家</div>
        <div class="ctx-stub-row">玩家固定在场景中，仅可拖动调整位置</div></div>`;
    }
    case "ServingStation": {
      const myNum = S.paramLabels.get(item.instanceId) ?? "?";
      const labels = computeReturnLabels();
      const plateId = servingPlateReturn(item);
      const glassId = servingGlassReturn(item);
      const mugId = servingMugReturn(item);
      const trayId = servingTrayReturn(item);
      const plateOpts = ['<option value="">— 未绑定 —</option>']
        .concat(
          plateReturns().map(
            (r) =>
              `<option value="${r.instanceId}" ${plateId === r.instanceId ? "selected" : ""}>${labels.get(r.instanceId) ?? "?"}（${escHtml(itemLabel(r))}）</option>`
          )
        )
        .join("");
      const glassOpts = ['<option value="">— 未绑定 —</option>']
        .concat(
          glassReturns().map(
            (r) =>
              `<option value="${r.instanceId}" ${glassId === r.instanceId ? "selected" : ""}>${labels.get(r.instanceId) ?? "?"}（${escHtml(itemLabel(r))}）</option>`
          )
        )
        .join("");
      const mugOpts = ['<option value="">— 未绑定 —</option>']
        .concat(
          mugReturns().map(
            (r) =>
              `<option value="${r.instanceId}" ${mugId === r.instanceId ? "selected" : ""}>${labels.get(r.instanceId) ?? "?"}（${escHtml(itemLabel(r))}）</option>`
          )
        )
        .join("");
      const trayOpts = ['<option value="">— 未绑定 —</option>']
        .concat(
          trayReturns().map(
            (r) =>
              `<option value="${r.instanceId}" ${trayId === r.instanceId ? "selected" : ""}>${labels.get(r.instanceId) ?? "?"}（${escHtml(itemLabel(r))}）</option>`
          )
        )
        .join("");
      return `<div class="ctx-stub"><div class="ctx-stub-title">上菜台${myNum} · 绑定回收台（各至多一个）</div>
        <label class="ctx-stub-row">脏盘台 <select id="ctx-ss-plate" class="ctx-input">${plateOpts}</select></label>
        <label class="ctx-stub-row">脏杯台 <select id="ctx-ss-glass" class="ctx-input">${glassOpts}</select></label>
        <label class="ctx-stub-row">脏马克杯台 <select id="ctx-ss-mug" class="ctx-input">${mugOpts}</select></label>
        <label class="ctx-stub-row">餐盘回收台 <select id="ctx-ss-tray" class="ctx-input">${trayOpts}</select></label>
        <div class="ctx-stub-row" style="font-size:11px;color:#8a909a">脏盘台与餐盘回收台互斥（同族容器，绑一个会自动替换另一个）；普通菜与套餐混搭请用两套上菜台。脏杯/马克杯回收台可与之并存，一个回收台可被多个上菜台共用</div></div>`;
    }
    case "PlateReturn":
    case "GlassReturn": {
      const labels = computeReturnLabels();
      const isGlass = isGlassReturnItem(item);
      const isMug = isMugReturnItem(item);
      const isTray = isTrayReturnItem(item);
      const typeZh = isTray ? "餐盘回收台" : isMug ? "脏马克杯台" : isGlass ? "脏杯台" : "脏盘台";
      const myNum = labels.get(item.instanceId) ?? "?";
      const bound = servingStationsForReturn(item.instanceId);
      const rows = bound
        .map((s) => {
          const sNum = S.paramLabels.get(s.instanceId) ?? "?";
          return `<div class="ctx-stub-row">· 上菜台${sNum}（${escHtml(itemLabel(s))}）</div>`;
        })
        .join("");
      const cleanLabel = isTray ? "餐盘" : isMug ? "马克杯" : isGlass ? "杯子" : "盘子";
      return `<div class="ctx-stub"><div class="ctx-stub-title">${typeZh}${myNum} · 被上菜台绑定（可一对多）</div>
        ${rows || '<div class="ctx-stub-row">未被任何上菜台绑定</div>'}
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-pr-clean" ${item.plateReturn?.returnClean ? "checked" : ""}/> 直接返回干净${cleanLabel}（returnClean）</label>
        <div class="ctx-stub-row" style="font-size:11px;color:#8a909a">请在对应上菜台的右键菜单里设置绑定</div></div>`;
    }
    default:
      return "";
  }
}

export function wireStubControls(item: EditorItem) {
  const kind = stubKindOf(item);

  const num = (id: string): HTMLInputElement | null =>
    document.getElementById(id) as HTMLInputElement | null;

  if (kind) {
  switch (kind) {
    case "Dispenser": {
      document.getElementById("ctx-stub-ing-pick")?.addEventListener("click", () => {
        const stype = specialDispenserType(item);
        // 全部食材可选（含未放开的 web 内置——弹窗内置灰禁选并注明原因）；
        // 酱料机/饮料机再按各自可输出列表收窄（dlc 各自一套，见 DISPENSER_INGREDIENT_IDS）
        let ings = visibleIngredients(S.ingredientsCache);
        const allowedIds = specialDispenserAllowedIds(item);
        if (allowedIds) ings = ings.filter((i) => allowedIds.has(i.id));
        ings = ings.sort((a, b) => a.nameZh.localeCompare(b.nameZh, "zh"));
        const fieldLabel = stype === "condiment" ? "酱料" : stype === "drink" ? "饮料" : "食材";
        hideContextMenu();
        if (stype) {
          // 酱料机/饮料机：多选（游戏内开关循环切换）。写入 soArray（PseudoPrefabSOArray）。
          const curGuids = item.soArray?.pseudoPrefabGuids?.length
            ? item.soArray.pseudoPrefabGuids
            : item.dispenser?.spawnerItemPrefabGuid
              ? [item.dispenser.spawnerItemPrefabGuid]
              : [];
          openIngredientMultiPicker(
            `${fieldLabel}选择（可多选）`,
            `勾选机器可输出的${fieldLabel}：游戏内按下联动开关按勾选顺序循环切换；全不选 = 使用机器内置列表`,
            ings,
            curGuids,
            (guids) => {
              pushHistory();
              item.stubKind = "Dispenser";
              item.dispenser = { spawnerItemPrefabGuid: guids[0] ?? "" };
              // 空数组也要传：后端据此清空场景组件里的旧列表（patcher 遇空数组则不动，用内置列表）
              item.soArray = { pseudoPrefabGuids: guids };
              draw();
              setStatus(
                guids.length
                  ? `已设置${guids.length} 种${fieldLabel}（开关循环切换，写回后生效）`
                  : `已恢复${fieldLabel}机内置列表（写回后生效）`
              );
            },
            undefined,
            { isDisabled: () => null }
          );
          return;
        }
        const cur = item.dispenser?.spawnerItemPrefabGuid ?? "";
        openIngredientMultiPicker(
          `${fieldLabel}选择`,
          `选择食材箱要生成的食材`,
          ings,
          cur ? [cur] : [],
          (guids) => {
            pushHistory();
            item.stubKind = "Dispenser";
            item.dispenser = { spawnerItemPrefabGuid: guids[0] ?? "" };
            draw();
            setStatus(guids[0] ? `已设置${fieldLabel}（写回后生效）` : `已清除${fieldLabel}设置（写回后生效）`);
          },
          undefined,
          { single: true, isDisabled: crateIngredientDisabled }
        );
      });
      break;
    }
    case "AttachingFoodSpawner": {
      const ensure = () => {
        item.stubKind = "AttachingFoodSpawner";
        if (!item.foodSpawner) item.foodSpawner = {};
        return item.foodSpawner;
      };
      num("ctx-fs-order")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().spawnInOrder = (e.target as HTMLInputElement).checked;
      });
      num("ctx-fs-start")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().triggerAtStart = (e.target as HTMLInputElement).checked;
      });
      num("ctx-fs-time")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (isFinite(v) && v >= 0) {
          pushHistory();
          ensure().triggerTime = v;
        }
      });
      document.getElementById("ctx-fs-ings")?.addEventListener("click", () => {
        ensure();
        hideContextMenu();
        openFoodSpawnerEditor(item, S.ingredientsCache, (patch) => {
          pushHistory();
          item.foodSpawner = patch;
          draw();
          setStatus("已更新食材生成器参数（写回后生效）");
        });
      });
      break;
    }
    case "CookingUtensil": {
      const ensure = () => {
        item.stubKind = "CookingUtensil";
        if (!item.cookingUtensil) item.cookingUtensil = {};
        if (item.cookingUtensil.capacity == null)
          item.cookingUtensil.capacity = defaultUtensilCapacity(item);
        return item.cookingUtensil;
      };
      num("ctx-cu-cap")?.addEventListener("change", (e) => {
        const v = parseInt((e.target as HTMLInputElement).value, 10);
        if (isFinite(v) && v >= 0) {
          pushHistory();
          ensure().capacity = v;
          setStatus(`锅具容量已设为 ${v}（写回后生效）`);
        }
      });
      document.getElementById("ctx-cu-ings")?.addEventListener("click", () => {
        ensure();
        hideContextMenu();
        openIngredientMultiPicker(
          "锅具 · 额外食材",
          "allowedIngredientSOs（不选 = 处理所有主线食材；选中的作为额外可煮食材）",
          S.ingredientsCache,
          item.cookingUtensil?.allowedIngredientGuids ?? [],
          (guids) => {
            pushHistory();
            const cu = ensure();
            cu.allowedIngredientGuids = guids;
            // capacity 缺省会写回 0（后端 int），导致锅具一个食材都放不进——始终兜底原版默认
            if (cu.capacity == null) cu.capacity = defaultUtensilCapacity(item);
            draw();
            setStatus("已更新锅具额外食材（写回后生效）");
          },
          S.intermediatesCache
        );
      });
      break;
    }
    case "Conveyor": {
      num("ctx-cv-speed")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (!isFinite(v)) return;
        pushHistory();
        item.stubKind = "Conveyor";
        item.conveyor = { conveySpeed: v };
        draw();
        setStatus(`传送带速度已设为 ${v}（写回后生效）`);
      });
      break;
    }
    case "Teleportal": {
      const ensure = () => {
        item.stubKind = "Teleportal";
        if (!item.teleportal) item.teleportal = { exitPortalInstanceId: "", portalColor: 0, doubleSided: false };
        return item.teleportal;
      };
      num("ctx-tp-color")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().portalColor = parseInt((e.target as HTMLSelectElement).value, 10) || 0;
        draw();
      });
      num("ctx-tp-ds")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().doubleSided = (e.target as HTMLInputElement).checked;
        draw();
      });
      num("ctx-tp-exit")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().exitPortalInstanceId = (e.target as HTMLSelectElement).value;
        draw();
        setStatus("已更新传送门配对（写回后生效）");
      });
      break;
    }
    case "Travelator": {
      num("ctx-tv-speed")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (!isFinite(v) || v < 0) return;
        pushHistory();
        item.stubKind = "Travelator";
        item.travelator = { speed: v };
        setStatus(`移动地板速度已设为 ${v}（写回后生效）`);
      });
      break;
    }
    case "Flamethrower": {
      num("ctx-ft-rate")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (!isFinite(v) || v < 0) return;
        pushHistory();
        item.stubKind = "Flamethrower";
        item.flamethrower = { cookingRate: v };
        setStatus(`喷火器烹饪速率已设为 ${v}（写回后生效）`);
      });
      break;
    }
    case "CleanPlateStack": {
      num("ctx-ps-count")?.addEventListener("change", (e) => {
        const v = parseInt((e.target as HTMLInputElement).value, 10);
        if (!isFinite(v) || v < 0) return;
        pushHistory();
        item.stubKind = "CleanPlateStack";
        if (!item.cleanPlateStack) item.cleanPlateStack = {};
        item.cleanPlateStack.plateCount = v;
        setStatus(`盘子堆数量已设为 ${v}（写回后生效）`);
      });
      break;
    }
    case "Burner": {
      const ensure = () => {
        item.stubKind = "Burner";
        if (!item.burner) item.burner = {};
        if (item.burner.fireMode == null) item.burner.fireMode = 1;
        if (item.burner.airTime == null) item.burner.airTime = 2;
        if (item.burner.randomTargetOrder == null) item.burner.randomTargetOrder = false;
        if (item.burner.hideVisual == null) item.burner.hideVisual = false;
        return item.burner;
      };
      num("ctx-bn-mode")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().fireMode = parseInt((e.target as HTMLSelectElement).value, 10) || 0;
      });
      num("ctx-bn-air")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (isFinite(v) && v >= 0) {
          pushHistory();
          ensure().airTime = v;
        }
      });
      num("ctx-bn-rand")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().randomTargetOrder = (e.target as HTMLInputElement).checked;
      });
      num("ctx-bn-hide")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().hideVisual = (e.target as HTMLInputElement).checked;
      });
      break;
    }
    case "Player": {
      break;
    }
    case "ServingStation": {
      num("ctx-ss-plate")?.addEventListener("change", (e) => {
        const id = (e.target as HTMLSelectElement).value;
        pushHistory();
        setServingReturnOfType(item, id || undefined, "plate");
        draw();
        setStatus("已更新脏盘台绑定（写回后生效）");
      });
      num("ctx-ss-glass")?.addEventListener("change", (e) => {
        const id = (e.target as HTMLSelectElement).value;
        pushHistory();
        setServingReturnOfType(item, id || undefined, "glass");
        draw();
        setStatus("已更新脏杯台绑定（写回后生效）");
      });
      num("ctx-ss-mug")?.addEventListener("change", (e) => {
        const id = (e.target as HTMLSelectElement).value;
        pushHistory();
        setServingReturnOfType(item, id || undefined, "mug");
        draw();
        setStatus("已更新脏马克杯台绑定（写回后生效）");
      });
      num("ctx-ss-tray")?.addEventListener("change", (e) => {
        const id = (e.target as HTMLSelectElement).value;
        pushHistory();
        setServingReturnOfType(item, id || undefined, "tray");
        draw();
        setStatus("已更新餐盘回收台绑定（写回后生效）");
      });
      break;
    }
    case "PlateReturn":
    case "GlassReturn": {
      num("ctx-pr-clean")?.addEventListener("change", (e) => {
        pushHistory();
        item.stubKind = isGlassReturnItem(item) ? "GlassReturn" : "PlateReturn";
        if (!item.plateReturn) item.plateReturn = {};
        item.plateReturn.returnClean = (e.target as HTMLInputElement).checked;
        draw();
        setStatus(`已${item.plateReturn.returnClean ? "开启" : "关闭"}直接返回干净${isGlassReturnItem(item) ? "杯子" : "盘子"}（写回后生效）`);
      });
      break;
    }
    case "Switch": {
      const ensure = () => {
        item.stubKind = "Switch";
        if (!item.switchStub) item.switchStub = {};
        return item.switchStub;
      };
      num("ctx-sw-start")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().startEnabled = (e.target as HTMLInputElement).checked;
        setStatus(`开关初始状态已设为 ${item.switchStub!.startEnabled ? "开启" : "关闭"}（写回后生效）`);
      });
      num("ctx-sw-active")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().activeMaterialGuid = (e.target as HTMLSelectElement).value || undefined;
        setStatus("已更新开关未按下外观（写回后生效）");
      });
      num("ctx-sw-inactive")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().inactiveMaterialGuid = (e.target as HTMLSelectElement).value || undefined;
        setStatus("已更新开关按下外观（写回后生效）");
      });

      // ---- 开关联动（switchLinks，文档级） ----
      const myId = item.instanceId ?? "";
      const myLinks = () => S.switchLinks.filter((l) => l.switchId === myId);
      const linkRowsHtml = () => {
        const links = myLinks();
        if (!links.length)
          return '<div class="ctx-stub-row" style="font-size:11px;color:#8a909a">未设置联动目标</div>';
        return links
          .map((l) => {
            const target = S.items.find((i) => i.instanceId === l.targetId);
            return `<div class="ctx-stub-row">→ ${escHtml(target ? itemLabel(target) : l.targetId)}
              <button type="button" class="ctx-btn" data-unlink="${escHtml(l.targetId)}">移除</button></div>`;
          })
          .join("");
      };
      const linkTargetOptsHtml = () => {
        const linked = new Set(myLinks().map((l) => l.targetId));
        return S.items
          .filter((i) => i.instanceId && i.instanceId !== myId && !linked.has(i.instanceId) && stubKindOf(i) !== "Player")
          .map((i) => `<option value="${escHtml(i.instanceId)}">${escHtml(itemLabel(i))}</option>`)
          .join("");
      };
      const refreshLinks = () => {
        const rows = document.getElementById("ctx-sw-links");
        if (rows) {
          rows.innerHTML = linkRowsHtml();
          rows.querySelectorAll<HTMLButtonElement>("[data-unlink]").forEach((btn) => {
            btn.addEventListener("click", () => {
              pushHistory();
              const tid = btn.dataset.unlink!;
              S.switchLinks = S.switchLinks.filter((l) => !(l.switchId === myId && l.targetId === tid));
              setStatus("已移除开关联动（写回后生效）");
              refreshLinks();
            });
          });
        }
        const sel = document.getElementById("ctx-sw-linktarget") as HTMLSelectElement | null;
        if (sel) sel.innerHTML = linkTargetOptsHtml();
        const trig = document.getElementById("ctx-sw-trigger") as HTMLInputElement | null;
        if (trig && document.activeElement !== trig) trig.value = myLinks()[0]?.trigger ?? "Switch";
      };
      refreshLinks();
      num("ctx-sw-linkadd")?.addEventListener("click", () => {
        const sel = document.getElementById("ctx-sw-linktarget") as HTMLSelectElement | null;
        const tid = sel?.value ?? "";
        if (!tid || !myId) return;
        pushHistory();
        const trigInput = document.getElementById("ctx-sw-trigger") as HTMLInputElement | null;
        let trigger = trigInput?.value.trim() || "";
        // 未自定义时按约定命名：switch_{目标prefabId}_{N}（N = 该开关已有链接数 + 1）
        if (!trigger || trigger === "Switch") {
          const target = S.items.find((i) => i.instanceId === tid);
          const prefabId = target ? prefabIdFromPath(target.prefabAssetPath) ?? "item" : "item";
          trigger = `switch_${prefabId}_${myLinks().length + 1}`;
          if (trigInput) trigInput.value = trigger;
        }
        S.switchLinks.push({ switchId: myId, targetId: tid, trigger });
        setStatus(`已添加开关联动（${trigger}，写回后生效）`);
        refreshLinks();
      });
      num("ctx-sw-trigger")?.addEventListener("change", () => {
        const trig = (document.getElementById("ctx-sw-trigger") as HTMLInputElement).value.trim() || "Switch";
        const links = myLinks();
        if (!links.length) return;
        pushHistory();
        for (const l of links) l.trigger = trig;
        setStatus(`已更新触发消息为 ${trig}（写回后生效）`);
      });
      break;
    }
    case "PressureSwitch": {
      const ensure = () => {
        item.stubKind = "PressureSwitch";
        if (!item.pressureSwitch) item.pressureSwitch = {};
        return item.pressureSwitch;
      };
      num("ctx-ps-occ")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().occupiedMaterialGuid = (e.target as HTMLSelectElement).value || undefined;
        setStatus("已更新压力开关按下外观（写回后生效）");
      });
      num("ctx-ps-unocc")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().unoccupiedMaterialGuid = (e.target as HTMLSelectElement).value || undefined;
        setStatus("已更新压力开关松开外观（写回后生效）");
      });
      break;
    }
    case "Terminal": {
      num("ctx-tm-target")?.addEventListener("change", (e) => {
        pushHistory();
        item.stubKind = "Terminal";
        if (!item.terminal) item.terminal = {};
        item.terminal.pilotableObjectInstanceId = (e.target as HTMLSelectElement).value;
        draw();
        setStatus("已更新控制终端目标（写回后生效）");
      });
      break;
    }
  }
  }

  if (isButtonEventSource(item)) {
    document.getElementById("ctx-bev-config")?.addEventListener("click", () => {
      hideContextMenu();
      openButtonEventModal(item);
    });
  }

  if (isButtonLinkSource(item)) {
    document.getElementById("ctx-bl-config")?.addEventListener("click", () => {
      hideContextMenu();
      openButtonLinkModal(item);
    });
  }

  wireCounterAppearance(item);
}

export function wireCounterAppearance(item: EditorItem) {
  const sel = document.getElementById("ctx-appear") as HTMLSelectElement | null;
  if (!sel) return;
  sel.addEventListener("change", () => {
    pushHistory();
    item.pseudoPrefabGuid = sel.value || undefined;
    draw();
    const options = counterAppearanceOptions(item);
    const nameLookup = new Map(options.map((o) => [o.guid, o]));
    const name = nameLookup.get(item.pseudoPrefabGuid ?? "")?.nameZh ?? "默认外观";
    setStatus(`已更新外观为：${name}（写回后生效）`);
    // Re-show context menu to update the display
    dom.ctxMenuEl.classList.add("hidden");
  });
}
